namespace N52te;

/// <summary>One of the keypad's 27 programmable inputs.</summary>
public sealed record InputInfo(
    int Number,
    string Id,
    string Name,
    byte MacroMarker,
    byte PcActionMarker,
    byte FactoryCode);

/// <summary>
/// The 27 inputs in record order, with the per-input marker codes the firmware uses
/// (n52teCfg.exe tables at 0x4a487c, 0x4a4898 and 0x4a4860).
/// </summary>
public static class KeypadLayout
{
    public const int InputCount = 27;
    public const int KeymapCount = 3;

    public static readonly IReadOnlyList<InputInfo> Inputs = new[]
    {
        new InputInfo(1, "K01", "Key 01", 0xA0, 0x7C, 0x13),
        new InputInfo(2, "K02", "Key 02", 0xA4, 0x7E, 0x12),
        new InputInfo(3, "K03", "Key 03", 0xA8, 0x80, 0x1A),
        new InputInfo(4, "K04", "Key 04", 0xAC, 0x81, 0x22),
        new InputInfo(5, "K05", "Key 05", 0xB0, 0x82, 0x2A),
        new InputInfo(6, "K06", "Key 06", 0xA1, 0x84, 0x1B),
        new InputInfo(7, "K07", "Key 07", 0xA5, 0x85, 0x14),
        new InputInfo(8, "K08", "Key 08", 0xA9, 0x87, 0x1C),
        new InputInfo(9, "K09", "Key 09", 0xAD, 0x89, 0x24),
        new InputInfo(10, "K10", "Key 10", 0xB1, 0x8A, 0x2C),
        new InputInfo(11, "K11", "Key 11", 0xA2, 0x8B, 0x83),
        new InputInfo(12, "K12", "Key 12", 0xA6, 0x8C, 0x16),
        new InputInfo(13, "K13", "Key 13", 0xAA, 0x8D, 0x1E),
        new InputInfo(14, "K14", "Key 14", 0xAE, 0x8F, 0x26),
        new InputInfo(15, "THUMB", "Thumb button", 0xA3, 0x90, 0x7D),
        new InputInfo(16, "WHEEL_UP", "Wheel up", 0xBA, 0x91, 0xD4),
        new InputInfo(17, "WHEEL_CLICK", "Wheel click", 0xB8, 0x92, 0xD2),
        new InputInfo(18, "WHEEL_DOWN", "Wheel down", 0xB9, 0x93, 0xD3),
        new InputInfo(19, "PAD", "Thumb pad", 0xB2, 0x94, 0x57),
        new InputInfo(20, "UP", "D-pad up", 0xB3, 0x95, 0x75),
        new InputInfo(21, "RIGHT", "D-pad right", 0xAB, 0x96, 0x5F),
        new InputInfo(22, "DOWN", "D-pad down", 0xAF, 0x97, 0x67),
        new InputInfo(23, "LEFT", "D-pad left", 0xA7, 0x99, 0x77),
        new InputInfo(24, "UP_RIGHT", "D-pad up+right", 0xB4, 0x9B, 0xB4),
        new InputInfo(25, "DOWN_RIGHT", "D-pad down+right", 0xB5, 0x9D, 0xB5),
        new InputInfo(26, "DOWN_LEFT", "D-pad down+left", 0xB6, 0x9E, 0xB6),
        new InputInfo(27, "UP_LEFT", "D-pad up+left", 0xB7, 0x9F, 0xB7),
    };

    public static InputInfo Input(int number) =>
        number is >= 1 and <= InputCount
            ? Inputs[number - 1]
            : throw new ArgumentOutOfRangeException(nameof(number), number, "Inputs are numbered 1–27.");

    /// <summary>The factory profile (identical in all three keymaps), as in Belkin's Default.ini.</summary>
    public static Profile FactoryProfile()
    {
        var profile = new Profile { Name = "Default" };
        for (int k = 0; k < KeymapCount; k++)
        {
            for (int n = 1; n <= InputCount; n++)
            {
                profile.Keymaps[k].Inputs[n - 1] = new InputAssignment(FactoryAction(n));
            }
        }
        return profile;
    }

    public static InputAction FactoryAction(int number)
    {
        var info = Input(number);
        return number switch
        {
            24 => Diagonal(0x75, 0x5F),
            25 => Diagonal(0x67, 0x5F),
            26 => Diagonal(0x67, 0x77),
            27 => Diagonal(0x75, 0x77),
            _ when FirmwareCodes.IsMouse(info.FactoryCode) => new MouseAction((MouseFunction)info.FactoryCode),
            _ => new KeyAction(info.FactoryCode),
        };
    }

    private static MacroAction Diagonal(byte first, byte second) => new(new List<MacroStep>
    {
        new(first, true), new(second, true), new(first, false), new(second, false),
    });
}
