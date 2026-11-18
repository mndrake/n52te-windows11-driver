namespace N52te;

/// <summary>
/// A hotkey chord the keypad sends for a Companion action: Ctrl+Alt+Shift+key, typed by a firmware
/// macro and caught by the Companion with RegisterHotKey.
/// </summary>
public sealed record HotkeyChord(byte FirmwareKey, int VirtualKey, string KeyName)
{
    public const int ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    public const int Modifiers = ModControl | ModAlt | ModShift | ModNoRepeat;

    public string Display => $"Ctrl+Alt+Shift+{KeyName}";

    /// <summary>The macro the keypad plays: hold Ctrl, Alt, Shift, tap the key, release in reverse order.</summary>
    public List<MacroStep> Macro() => new()
    {
        new(FirmwareCodes.LeftCtrl, true), new(FirmwareCodes.LeftAlt, true), new(FirmwareCodes.LeftShift, true),
        new(FirmwareKey, true), new(FirmwareKey, false),
        new(FirmwareCodes.LeftShift, false), new(FirmwareCodes.LeftAlt, false), new(FirmwareCodes.LeftCtrl, false),
    };
}

public sealed record CompanionBinding(Keymap Keymap, int Input, CompanionAction Action, HotkeyChord Chord);

public static class CompanionChords
{
    /// <summary>
    /// Chords in allocation order. Only keys whose virtual-key code doesn't depend on Num Lock or Shift
    /// (so no numpad digits), and no Win key (Ctrl+Alt+Shift+Win is the Office key).
    /// </summary>
    public static readonly IReadOnlyList<HotkeyChord> All = new HotkeyChord[]
    {
        new(0x18, 0x70, "F1"), new(0x20, 0x71, "F2"), new(0x23, 0x72, "F3"), new(0x25, 0x73, "F4"),
        new(0x55, 0x74, "F5"), new(0x3D, 0x75, "F6"), new(0x43, 0x76, "F7"), new(0x40, 0x77, "F8"),
        new(0x50, 0x78, "F9"), new(0x51, 0x79, "F10"), new(0x61, 0x7A, "F11"), new(0x59, 0x7B, "F12"),
        new(0x5E, 0x6F, "Numpad /"), new(0x6E, 0x6A, "Numpad *"), new(0x6F, 0x6D, "Numpad -"), new(0x72, 0x6B, "Numpad +"),
        new(0x58, 0x2D, "Insert"), new(0x70, 0x24, "Home"), new(0x71, 0x23, "End"),
        new(0x68, 0x21, "Page Up"), new(0x69, 0x22, "Page Down"), new(0x79, 0x91, "Scroll Lock"),
    };

    /// <summary>
    /// Assigns chords to the profile's Companion actions in keymap-then-input order. Identical actions
    /// share a chord, so one action used in several keymaps costs one chord.
    /// </summary>
    public static List<CompanionBinding> Allocate(Profile profile)
    {
        var chordFor = new Dictionary<CompanionAction, HotkeyChord>();
        var bindings = new List<CompanionBinding>();
        for (int k = 0; k < KeypadLayout.KeymapCount; k++)
        {
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                if (profile.Keymaps[k].Inputs[n - 1].Action is not CompanionAction action) continue;
                if (!chordFor.TryGetValue(action, out var chord))
                {
                    if (chordFor.Count >= All.Count)
                        throw new ProfileException($"Too many different Companion actions: the limit is {All.Count}.");
                    chord = All[chordFor.Count];
                    chordFor[action] = chord;
                }
                bindings.Add(new CompanionBinding((Keymap)k, n, action, chord));
            }
        }
        return bindings;
    }
}
