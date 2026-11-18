namespace N52te;

/// <summary>
/// Sends the keypad's HID class requests: SET_REPORT/GET_REPORT with wValue = command and the given
/// wIndex. Implementations: standard HID or temporary WinUSB, chosen after the probe (see the plan).
/// </summary>
public interface IKeypadTransport : IDisposable
{
    void SetReport(byte command, ushort index, ReadOnlySpan<byte> data);
    byte[] GetReport(byte command, ushort index, int length);
}

/// <summary>Command numbers (wValue), from Belkin's JmtFltr.sys.</summary>
public static class KeypadCommand
{
    public const byte SendSetting = 1;
    public const byte GetResult = 2;
    public const byte GetGroupSetting = 3;
    public const byte SetProfileId = 4;
    public const byte GetProfileId = 5;
    public const byte GetFirmwareVersion = 6;
    /// <summary>Firmware update mode. Never sent by this program.</summary>
    public const byte EnterIcp = 7;
    public const byte DriverMode = 8;
    public const byte SetPollingRate = 9;
    public const byte GetPollingRate = 10;

    public static readonly IReadOnlySet<byte> AllowedSet = new HashSet<byte> { SendSetting, SetProfileId, DriverMode, SetPollingRate };
    public static readonly IReadOnlySet<byte> AllowedGet = new HashSet<byte> { GetResult, GetGroupSetting, GetProfileId, GetFirmwareVersion, DriverMode, GetPollingRate };
}

/// <summary>
/// Wraps a transport and refuses anything outside the documented commands: never the firmware-update
/// command, never more than 0x200 bytes.
/// </summary>
public sealed class GuardedTransport : IKeypadTransport
{
    public const int MaxTransfer = 0x200;
    private readonly IKeypadTransport inner;

    public GuardedTransport(IKeypadTransport inner) => this.inner = inner;

    public void SetReport(byte command, ushort index, ReadOnlySpan<byte> data)
    {
        if (!KeypadCommand.AllowedSet.Contains(command))
            throw new InvalidOperationException($"Refusing to send command {command} to the keypad.");
        if (data.Length is 0 or > MaxTransfer)
            throw new InvalidOperationException($"Refusing a {data.Length}-byte transfer (limit {MaxTransfer}).");
        inner.SetReport(command, index, data);
    }

    public byte[] GetReport(byte command, ushort index, int length)
    {
        if (!KeypadCommand.AllowedGet.Contains(command))
            throw new InvalidOperationException($"Refusing to request command {command} from the keypad.");
        if (length is <= 0 or > MaxTransfer)
            throw new InvalidOperationException($"Refusing a {length}-byte transfer (limit {MaxTransfer}).");
        return inner.GetReport(command, index, length);
    }

    public void Dispose() => inner.Dispose();
}

public sealed record UploadProgress(Keymap Keymap, int Chunk, int Attempt);

public sealed class KeypadException : Exception
{
    public KeypadException(string message) : base(message) { }
}

/// <summary>High-level keypad operations, following the sequence Belkin's n52teHid.exe used.</summary>
public sealed class KeypadClient
{
    public const int AttemptsPerKeymap = 3;
    private readonly IKeypadTransport transport;

    public KeypadClient(IKeypadTransport transport) =>
        this.transport = transport is GuardedTransport ? transport : new GuardedTransport(transport);

    /// <summary>The original sent this once when the keypad appeared and after every resume. Effect unknown.</summary>
    public void SetDriverMode() => transport.SetReport(KeypadCommand.DriverMode, 0, new byte[] { 0x00 });

    /// <summary>Uploads all three keymaps (14 chunks each, checksum after every chunk), then selects the start keymap.</summary>
    public void Upload(CompiledProfile profile, IProgress<UploadProgress>? progress = null)
    {
        SetDriverMode();
        for (int k = 0; k < KeypadLayout.KeymapCount; k++)
        {
            var image = KeymapImage.Build(profile.Keymaps[k], (Keymap)k);
            UploadKeymap(image, (Keymap)k, progress);
        }
        SelectKeymap(profile.StartKeymap);
    }

    public void UploadKeymap(byte[] image, Keymap keymap, IProgress<UploadProgress>? progress = null)
    {
        if (image.Length != KeymapImage.Size) throw new ArgumentException("Keymap image must be 0x1C00 bytes.", nameof(image));
        for (int attempt = 1; attempt <= AttemptsPerKeymap; attempt++)
        {
            if (TrySendKeymap(image, keymap, attempt, progress)) return;
        }
        throw new KeypadException($"The keypad didn't confirm the {keymap} keymap after {AttemptsPerKeymap} attempts. Its previous profile may still be active; try again before unplugging it.");
    }

    private bool TrySendKeymap(byte[] image, Keymap keymap, int attempt, IProgress<UploadProgress>? progress)
    {
        for (int c = 0; c < KeymapImage.ChunkCount; c++)
        {
            progress?.Report(new UploadProgress(keymap, c, attempt));
            var chunk = KeymapImage.Chunk(image, c);
            transport.SetReport(KeypadCommand.SendSetting, (ushort)(((int)keymap << 8) | c), chunk);
            var reply = transport.GetReport(KeypadCommand.GetResult, 0, 2);
            var (even, odd) = KeymapImage.Checksum(chunk);
            if (reply.Length < 2 || reply[0] != even || reply[1] != odd) return false;
        }
        return true;
    }

    public void SelectKeymap(Keymap keymap) =>
        transport.SetReport(KeypadCommand.SetProfileId, 0, CommandBlock.SelectKeymap(keymap));

    public void SetPollingInterval(int intervalMs) =>
        transport.SetReport(KeypadCommand.SetPollingRate, 0, CommandBlock.PollingRate(intervalMs));

    /// <summary>The keypad's active keymap, or null if the reply fails its checksum.</summary>
    public Keymap? GetActiveKeymap()
    {
        var reply = transport.GetReport(KeypadCommand.GetProfileId, 0, 8);
        return CommandBlock.IsValid(reply) && reply[5] < 3 ? (Keymap)reply[5] : null;
    }

    public byte[] GetFirmwareVersion() => transport.GetReport(KeypadCommand.GetFirmwareVersion, 0, 8);
}
