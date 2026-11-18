namespace N52te;

/// <summary>One input's 0x104-byte record: code, 256-byte sequence (profile form), three option bytes.</summary>
public sealed record InputRecord(byte Code, byte[] Sequence, byte[] Options)
{
    public const int Size = 0x104;

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        bytes[0] = Code;
        Sequence.CopyTo(bytes, 1);
        Options.CopyTo(bytes, 0x101);
        return bytes;
    }

    public static InputRecord FromBytes(ReadOnlySpan<byte> bytes) =>
        new(bytes[0], bytes.Slice(1, 0x100).ToArray(), bytes.Slice(0x101, 3).ToArray());
}

/// <summary>A profile turned into keypad records, plus the Companion hotkeys it relies on.</summary>
public sealed record CompiledProfile(InputRecord[][] Keymaps, IReadOnlyList<CompanionBinding> Companion, Keymap StartKeymap);

/// <summary>Converts between the profile model and the keypad's record format.</summary>
public static class ProfileCompiler
{
    public static CompiledProfile Compile(Profile profile)
    {
        var bindings = CompanionChords.Allocate(profile);
        var keymaps = new InputRecord[KeypadLayout.KeymapCount][];
        for (int k = 0; k < KeypadLayout.KeymapCount; k++)
        {
            keymaps[k] = new InputRecord[KeypadLayout.InputCount];
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                var assignment = profile.Keymaps[k].Inputs[n - 1];
                if (assignment.Options.Length != 3)
                    throw new ProfileException($"Input {n}: options must be 3 bytes.");
                var chord = bindings.FirstOrDefault(b => (int)b.Keymap == k && b.Input == n)?.Chord;
                keymaps[k][n - 1] = Encode(n, assignment, chord);
            }
        }
        return new CompiledProfile(keymaps, bindings, profile.StartKeymap);
    }

    public static InputRecord Encode(int input, InputAssignment assignment, HotkeyChord? chord = null)
    {
        var info = KeypadLayout.Input(input);
        var empty = new byte[0x100];
        var options = (byte[])assignment.Options.Clone();
        switch (assignment.Action)
        {
            case KeyAction key:
                if (key.Code == FirmwareCodes.Disabled || FirmwareCodes.IsMouse(key.Code) || FirmwareCodes.IsKeymapSwitch(key.Code)
                    || key.Code == info.MacroMarker || key.Code == info.PcActionMarker)
                    throw new ProfileException($"Input {input}: code 0x{key.Code:X2} is not a key for this input.");
                return new InputRecord(key.Code, empty, options);
            case MouseAction mouse:
                return new InputRecord((byte)mouse.Function, empty, options);
            case KeymapSwitchAction sw:
                return new InputRecord((byte)sw.Switch, empty, options);
            case DisabledAction:
                return new InputRecord(FirmwareCodes.Disabled, empty, options);
            case MacroAction macro:
                return new InputRecord(info.MacroMarker, Macro.ToProfileSequence(macro.Steps), options);
            case CompanionAction:
                if (chord is null) throw new ProfileException($"Input {input}: Companion action has no hotkey assigned.");
                return new InputRecord(info.MacroMarker, Macro.ToProfileSequence(chord.Macro()), options);
            case LegacyPcAction pc:
                var seq = new byte[0x100];
                seq[0] = pc.PcType;
                seq[1] = pc.Parameter;
                return new InputRecord(info.PcActionMarker, seq, options);
            case RawAction raw:
                if (raw.Sequence.Length != 0x100) throw new ProfileException($"Input {input}: raw sequence must be 256 bytes.");
                return new InputRecord(raw.Code, (byte[])raw.Sequence.Clone(), options);
            default:
                throw new ProfileException($"Input {input}: unsupported action {assignment.Action.GetType().Name}.");
        }
    }

    /// <summary>
    /// Interprets a record from a Belkin profile. Companion chords are not recognised here; they come
    /// back as macros (the Companion mapping lives in this program's JSON profile).
    /// </summary>
    public static InputAssignment Decode(int input, InputRecord record)
    {
        var info = KeypadLayout.Input(input);
        var options = (byte[])record.Options.Clone();
        bool seqEmpty = record.Sequence.All(b => b == 0);
        InputAction action;
        if (record.Code == info.MacroMarker && Macro.FromProfileSequence(record.Sequence) is { } steps)
            action = new MacroAction(steps);
        else if (record.Code == info.PcActionMarker && record.Sequence.Skip(2).All(b => b == 0))
            action = new LegacyPcAction(record.Sequence[0], record.Sequence[1]);
        else if (!seqEmpty)
            action = new RawAction(record.Code, (byte[])record.Sequence.Clone());
        else if (record.Code == FirmwareCodes.Disabled)
            action = new DisabledAction();
        else if (FirmwareCodes.IsMouse(record.Code))
            action = new MouseAction((MouseFunction)record.Code);
        else if (FirmwareCodes.IsKeymapSwitch(record.Code))
            action = new KeymapSwitchAction((KeymapSwitch)record.Code);
        else if (record.Code == info.MacroMarker)
            action = new MacroAction(new List<MacroStep>());
        else
            action = new KeyAction(record.Code);
        return new InputAssignment(action, options);
    }
}

/// <summary>The 0x1C00-byte image of one keymap, as uploaded in 14 chunks of 0x200 bytes.</summary>
public static class KeymapImage
{
    public const int Size = 0x1C00;
    public const int ChunkSize = 0x200;
    public const int ChunkCount = Size / ChunkSize;

    public static byte[] Build(IReadOnlyList<InputRecord> records, Keymap keymap)
    {
        if (records.Count != KeypadLayout.InputCount) throw new ArgumentException("A keymap has 27 inputs.", nameof(records));
        var image = new byte[Size];
        image[0] = 0x1C;
        image[1] = (byte)KeypadLayout.InputCount;
        image[2] = 0x00;
        image[3] = 0x01;
        image[4] = (byte)keymap;
        for (int i = 0; i < KeypadLayout.InputCount; i++)
        {
            var r = records[i];
            image[0x08 + i] = r.Code;
            image[0x23 + i] = (byte)(r.Options[0] + 1);
            image[0x3E + i] = (byte)(r.Options[1] + 1);
            image[0x59 + i] = (byte)(r.Options[2] + 1);
            Macro.ToFirmwareSequence(r.Sequence).CopyTo(image, 0x100 + i * 0x100);
        }
        return image;
    }

    public static byte[] Chunk(byte[] image, int index) => image.AsSpan(index * ChunkSize, ChunkSize).ToArray();

    /// <summary>What the keypad returns after each chunk (GET_REPORT 2): XOR of even bytes, XOR of odd bytes.</summary>
    public static (byte Even, byte Odd) Checksum(ReadOnlySpan<byte> data)
    {
        byte even = 0, odd = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if ((i & 1) == 0) even ^= data[i];
            else odd ^= data[i];
        }
        return (even, odd);
    }
}

/// <summary>The 8-byte command blocks: 00 08 00 SUB 00 VALUE b0^b2^b4 b1^b3^b5.</summary>
public static class CommandBlock
{
    public const byte SubKeymap = 3;
    public const byte SubPollingRate = 4;

    public static byte[] Build(byte sub, byte value)
    {
        var b = new byte[] { 0x00, 0x08, 0x00, sub, 0x00, value, 0, 0 };
        b[6] = (byte)(b[0] ^ b[2] ^ b[4]);
        b[7] = (byte)(b[1] ^ b[3] ^ b[5]);
        return b;
    }

    public static byte[] SelectKeymap(Keymap keymap) => Build(SubKeymap, (byte)keymap);

    public static byte[] PollingRate(int intervalMs) => intervalMs is 1 or 2 or 8
        ? Build(SubPollingRate, (byte)intervalMs)
        : throw new ArgumentOutOfRangeException(nameof(intervalMs), intervalMs, "Polling interval must be 1, 2 or 8 ms.");

    public static bool IsValid(ReadOnlySpan<byte> b) =>
        b.Length >= 8 && b[6] == (byte)(b[0] ^ b[2] ^ b[4]) && b[7] == (byte)(b[1] ^ b[3] ^ b[5]);
}
