namespace N52te;

/// <summary>Macro limits and the two macro byte formats.</summary>
public static class Macro
{
    /// <summary>A record's sequence holds 128 (code, flags) pairs; each delay also uses a pair.</summary>
    public const int MaxPairs = 128;

    /// <summary>Largest delay value (7 bits in the uploaded flags byte).</summary>
    public const int MaxDelayUnits = 127;

    /// <summary>Inferred: Belkin's 6350 ms maximum = 127 × 50 ms. Not yet confirmed on hardware.</summary>
    public const int DelayUnitMs = 50;

    /// <summary>Number of pairs a macro occupies in a profile record.</summary>
    public static int PairCount(IEnumerable<MacroStep> steps) => steps.Sum(s => s.DelayUnits > 0 ? 2 : 1);

    /// <summary>Profile/editor form: (code, press) pairs, with a (0xFF, units) pair after a step that has a delay.</summary>
    public static byte[] ToProfileSequence(IReadOnlyList<MacroStep> steps)
    {
        if (PairCount(steps) > MaxPairs)
            throw new ProfileException($"Macro needs {PairCount(steps)} steps; the keypad holds {MaxPairs} (delays count as steps).");
        var seq = new byte[2 * MaxPairs];
        int p = 0;
        foreach (var step in steps)
        {
            if (step.Code is FirmwareCodes.Disabled or FirmwareCodes.DelayStep)
                throw new ProfileException($"Code 0x{step.Code:X2} can't be used as a macro key.");
            if (step.DelayUnits is < 0 or > MaxDelayUnits)
                throw new ProfileException($"Delay must be 0–{MaxDelayUnits} units ({MaxDelayUnits * DelayUnitMs} ms).");
            seq[p++] = step.Code;
            seq[p++] = (byte)(step.Press ? 1 : 0);
            if (step.DelayUnits > 0)
            {
                seq[p++] = FirmwareCodes.DelayStep;
                seq[p++] = (byte)step.DelayUnits;
            }
        }
        return seq;
    }

    /// <summary>
    /// Parses the profile form back into steps. Returns null if the bytes don't form a plain macro
    /// (so the caller keeps the record raw).
    /// </summary>
    public static List<MacroStep>? FromProfileSequence(ReadOnlySpan<byte> seq)
    {
        var steps = new List<MacroStep>();
        int i = 0;
        for (; i + 1 < seq.Length; i += 2)
        {
            byte code = seq[i], flags = seq[i + 1];
            if (code == 0 && flags == 0) break;
            if (code == FirmwareCodes.DelayStep)
            {
                if (steps.Count == 0 || steps[^1].DelayUnits != 0 || flags > MaxDelayUnits) return null;
                steps[^1] = steps[^1] with { DelayUnits = flags };
                continue;
            }
            if (code == 0 || flags > 1) return null;
            steps.Add(new MacroStep(code, flags == 1));
        }
        for (; i < seq.Length; i++)
            if (seq[i] != 0) return null;
        return steps;
    }

    /// <summary>
    /// Converts the profile form into the form uploaded to the keypad, exactly as Belkin's n52teHid.exe
    /// did (0x4065c0): delay entries are merged into the previous step's flags (bits 1–7), and a macro
    /// filling all slots that ends on a press is closed with that key's release.
    /// </summary>
    public static byte[] ToFirmwareSequence(ReadOnlySpan<byte> profileSequence)
    {
        var output = new byte[2 * MaxPairs];
        int count = 0;
        for (int i = 0; i + 1 < profileSequence.Length && i < 2 * MaxPairs; i += 2)
        {
            byte code = profileSequence[i], value = profileSequence[i + 1];
            if (code == FirmwareCodes.DelayStep)
            {
                if (count > 0) output[2 * count - 1] |= (byte)(value << 1);
            }
            else
            {
                output[2 * count] = code;
                output[2 * count + 1] = value;
                count++;
            }
        }
        if (count is > 0 and < MaxPairs && (output[2 * count - 1] & 1) != 0)
        {
            output[2 * count] = output[2 * count - 2];
            output[2 * count + 1] = 0;
        }
        return output;
    }

    /// <summary>Press and release one key.</summary>
    public static IEnumerable<MacroStep> Tap(byte code)
    {
        yield return new MacroStep(code, true);
        yield return new MacroStep(code, false);
    }
}

/// <summary>Builds a macro that types text, using US keyboard positions.</summary>
public static class TextMacro
{
    private static readonly Dictionary<char, (byte Code, bool Shift)> Map = BuildMap();

    private static Dictionary<char, (byte, bool)> BuildMap()
    {
        var map = new Dictionary<char, (byte, bool)>();
        void Add(string plain, string shifted, byte code)
        {
            foreach (var c in plain) map[c] = (code, false);
            foreach (var c in shifted) map[c] = (code, true);
        }
        byte[] letters = { 0x14, 0x2F, 0x26, 0x24, 0x22, 0x2C, 0x2D, 0x35, 0x3A, 0x34, 0x3C, 0x44, 0x36,
                           0x37, 0x42, 0x4A, 0x12, 0x2A, 0x1C, 0x2B, 0x32, 0x2E, 0x1A, 0x1E, 0x33, 0x16 };
        for (int i = 0; i < 26; i++) Add(((char)('a' + i)).ToString(), ((char)('A' + i)).ToString(), letters[i]);
        byte[] digits = { 0x49, 0x11, 0x19, 0x21, 0x29, 0x28, 0x30, 0x31, 0x39, 0x41 };
        const string shiftedDigits = ")!@#$%^&*(";
        for (int i = 0; i < 10; i++) Add(((char)('0' + i)).ToString(), shiftedDigits[i].ToString(), digits[i]);
        Add(" ", "", 0x57);
        Add("\n", "", 0x56);
        Add("\t", "", 0x13);
        Add("-", "_", 0x48);
        Add("=", "+", 0x38);
        Add("[", "{", 0x4B);
        Add("]", "}", 0x3B);
        Add("\\", "|", 0x54);
        Add(";", ":", 0x4C);
        Add("'", "\"", 0x4D);
        Add(",", "<", 0x3E);
        Add(".", ">", 0x46);
        Add("/", "?", 0x4F);
        Add("`", "~", 0x10);
        return map;
    }

    public static bool CanType(char c) => Map.ContainsKey(c == '\r' ? '\n' : c);

    /// <summary>Builds the steps for <paramref name="text"/>, or throws if a character can't be typed or it doesn't fit.</summary>
    public static List<MacroStep> Build(string text)
    {
        var steps = new List<MacroStep>();
        foreach (var raw in text.Replace("\r\n", "\n"))
        {
            var c = raw == '\r' ? '\n' : raw;
            if (!Map.TryGetValue(c, out var key))
                throw new ProfileException($"The keypad can't type '{c}' (US keyboard characters only).");
            if (key.Shift) steps.Add(new MacroStep(FirmwareCodes.LeftShift, true));
            steps.AddRange(Macro.Tap(key.Code));
            if (key.Shift) steps.Add(new MacroStep(FirmwareCodes.LeftShift, false));
        }
        if (steps.Count > Macro.MaxPairs)
            throw new ProfileException($"Text is too long for the keypad: needs {steps.Count} steps, the limit is {Macro.MaxPairs}. Use a Companion 'Type text' action instead.");
        return steps;
    }
}

public sealed class ProfileException : Exception
{
    public ProfileException(string message) : base(message) { }
}
