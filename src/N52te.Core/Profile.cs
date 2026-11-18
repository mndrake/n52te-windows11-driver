using System.Text.Json.Serialization;

namespace N52te;

/// <summary>A complete keypad profile: three keymaps of 27 inputs, plus Companion settings.</summary>
public sealed class Profile
{
    public string Name { get; set; } = "Untitled";

    /// <summary>Keymap the keypad switches to after an upload.</summary>
    public Keymap StartKeymap { get; set; } = Keymap.Blue;

    /// <summary>Polling interval in ms (8 = 125 Hz, 2 = 500 Hz, 1 = 1000 Hz); null leaves the keypad's setting alone.</summary>
    public int? PollingIntervalMs { get; set; }

    public KeymapData[] Keymaps { get; set; } = { new(), new(), new() };

    /// <summary>Companion rules: select a keymap when an application comes to the foreground.</summary>
    public List<AppRule> AppRules { get; set; } = new();

    /// <summary>Last byte of a Belkin profile blob (meaning unknown; 0x9A in the factory profile).</summary>
    public byte BelkinTrailer { get; set; } = 0x9A;

    public InputAssignment Get(Keymap keymap, int input) => Keymaps[(int)keymap].Inputs[input - 1];

    /// <summary>
    /// Assigns an action. Keymap switches are written to all three keymaps, like the Belkin editor did,
    /// so the same button switches keymaps whichever keymap is active.
    /// </summary>
    public void Set(Keymap keymap, int input, InputAction action, byte[]? options = null)
    {
        KeypadLayout.Input(input);
        var assignment = new InputAssignment(action, options ?? InputAssignment.DefaultOptions());
        if (action is KeymapSwitchAction)
        {
            foreach (var km in Keymaps) km.Inputs[input - 1] = assignment;
        }
        else
        {
            Keymaps[(int)keymap].Inputs[input - 1] = assignment;
        }
    }
}

public sealed class KeymapData
{
    public InputAssignment[] Inputs { get; set; } =
        Enumerable.Range(1, KeypadLayout.InputCount).Select(n => new InputAssignment(KeypadLayout.FactoryAction(n))).ToArray();
}

/// <summary>An input's action plus its three option bytes (factory 01 01 00, meaning not yet confirmed).</summary>
public sealed record InputAssignment
{
    [JsonConstructor]
    public InputAssignment(InputAction action, byte[] options)
    {
        Action = action;
        Options = options;
    }

    public InputAssignment(InputAction action) : this(action, DefaultOptions()) { }

    public InputAction Action { get; init; }

    public byte[] Options { get; init; }

    public static byte[] DefaultOptions() => new byte[] { 0x01, 0x01, 0x00 };
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyAction), "key")]
[JsonDerivedType(typeof(MouseAction), "mouse")]
[JsonDerivedType(typeof(KeymapSwitchAction), "keymap")]
[JsonDerivedType(typeof(MacroAction), "macro")]
[JsonDerivedType(typeof(DisabledAction), "disabled")]
[JsonDerivedType(typeof(CompanionAction), "companion")]
[JsonDerivedType(typeof(LegacyPcAction), "legacyPc")]
[JsonDerivedType(typeof(RawAction), "raw")]
public abstract record InputAction
{
    public abstract string Describe();
}

/// <summary>A single key, media key or firmware command.</summary>
public sealed record KeyAction(byte Code) : InputAction
{
    public override string Describe() => FirmwareCodes.Name(Code);
}

public sealed record MouseAction(MouseFunction Function) : InputAction
{
    public override string Describe() => Function switch
    {
        MouseFunction.LeftButton => "Mouse left button",
        MouseFunction.RightButton => "Mouse right button",
        MouseFunction.MiddleButton => "Mouse middle button",
        MouseFunction.ScrollUp => "Scroll up",
        MouseFunction.ScrollDown => "Scroll down",
        MouseFunction.PointerLeft => "Pointer left",
        MouseFunction.PointerRight => "Pointer right",
        MouseFunction.PointerUp => "Pointer up",
        MouseFunction.PointerDown => "Pointer down",
        _ => Function.ToString(),
    };
}

public sealed record KeymapSwitchAction(KeymapSwitch Switch) : InputAction
{
    public override string Describe() => Switch switch
    {
        KeymapSwitch.Cycle => "Cycle keymaps",
        KeymapSwitch.BlueToggle => "Blue keymap (toggle)",
        KeymapSwitch.BlueMomentary => "Blue keymap (hold)",
        KeymapSwitch.GreenToggle => "Green keymap (toggle)",
        KeymapSwitch.GreenMomentary => "Green keymap (hold)",
        KeymapSwitch.RedToggle => "Red keymap (toggle)",
        KeymapSwitch.RedMomentary => "Red keymap (hold)",
        _ => Switch.ToString(),
    };
}

/// <summary>One macro step: press or release a key, then wait <see cref="DelayUnits"/> × <see cref="Macro.DelayUnitMs"/>.</summary>
public sealed record MacroStep(byte Code, bool Press, int DelayUnits = 0);

public sealed record MacroAction(List<MacroStep> Steps) : InputAction
{
    public override string Describe() => Steps.Count == 0 ? "Macro (empty)" : $"Macro ({Steps.Count} steps)";
}

public sealed record DisabledAction : InputAction
{
    public override string Describe() => "Disabled";
}

/// <summary>An action the Companion app performs when the keypad sends this input's hotkey chord.</summary>
public sealed record CompanionAction(CompanionKind Kind, string Argument = "") : InputAction
{
    public override string Describe() => Kind switch
    {
        CompanionKind.LaunchProgram => $"Launch {Path.GetFileName(Argument)}",
        CompanionKind.OpenUrl => $"Open {Argument}",
        CompanionKind.TypeText => "Type text",
        _ => Kind switch
        {
            CompanionKind.MinimizeWindow => "Minimize window",
            CompanionKind.MaximizeWindow => "Maximize window",
            CompanionKind.CloseWindow => "Close window",
            CompanionKind.ShowDesktop => "Show desktop",
            CompanionKind.ZoomIn => "Zoom in",
            CompanionKind.ZoomOut => "Zoom out",
            _ => Kind.ToString(),
        },
    } + " (Companion)";
}

public enum CompanionKind
{
    LaunchProgram,
    OpenUrl,
    MinimizeWindow,
    MaximizeWindow,
    CloseWindow,
    ShowDesktop,
    ZoomIn,
    ZoomOut,
    TypeText,
}

/// <summary>
/// A PC-side action from an old Belkin profile. It needed Belkin's n52teHid.exe and does nothing on
/// Windows 11; kept so imported profiles round-trip unchanged.
/// </summary>
public sealed record LegacyPcAction(byte PcType, byte Parameter) : InputAction
{
    public override string Describe() => $"Belkin PC action {PcType} (not supported)";
}

/// <summary>A record this program doesn't interpret, kept byte-for-byte.</summary>
public sealed record RawAction(byte Code, byte[] Sequence) : InputAction
{
    public override string Describe() => $"Unrecognised (code 0x{Code:X2})";
}

/// <summary>Companion rule: when a process with this executable name is in the foreground, select a keymap.</summary>
public sealed record AppRule(string ExecutableName, Keymap Keymap);
