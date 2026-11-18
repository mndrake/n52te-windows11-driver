namespace N52te.Tests;

/// <summary>A pretend keypad: answers GET_RESULT with the checksum of the last chunk, optionally corrupting some replies.</summary>
internal sealed class FakeKeypad : IKeypadTransport
{
    public readonly List<(byte Command, ushort Index, byte[] Data)> Sets = new();
    public readonly List<(byte Command, ushort Index)> Gets = new();
    public int CorruptReplies;
    private byte[] lastChunk = Array.Empty<byte>();
    public byte ActiveKeymap;

    public void SetReport(byte command, ushort index, ReadOnlySpan<byte> data)
    {
        Sets.Add((command, index, data.ToArray()));
        if (command == KeypadCommand.SendSetting) lastChunk = data.ToArray();
        if (command == KeypadCommand.SetProfileId) ActiveKeymap = data[5];
    }

    public byte[] GetReport(byte command, ushort index, int length)
    {
        Gets.Add((command, index));
        if (command == KeypadCommand.GetResult)
        {
            var (even, odd) = KeymapImage.Checksum(lastChunk);
            if (CorruptReplies > 0) { CorruptReplies--; even ^= 0xFF; }
            return new[] { even, odd };
        }
        if (command == KeypadCommand.GetProfileId) return CommandBlock.SelectKeymap((Keymap)ActiveKeymap);
        return new byte[length];
    }

    public void Dispose() { }
}

public class KeypadClientTests
{
    [Fact]
    public void UploadSendsDriverModeThen42ChunksThenSelectsStartKeymap()
    {
        var fake = new FakeKeypad();
        var profile = KeypadLayout.FactoryProfile();
        profile.StartKeymap = Keymap.Green;
        new KeypadClient(fake).Upload(ProfileCompiler.Compile(profile));

        Assert.Equal(KeypadCommand.DriverMode, fake.Sets[0].Command);
        var chunks = fake.Sets.Where(s => s.Command == KeypadCommand.SendSetting).ToList();
        Assert.Equal(42, chunks.Count);
        Assert.Equal(new ushort[] { 0x0000, 0x0001, 0x000D }, new[] { chunks[0].Index, chunks[1].Index, chunks[13].Index });
        Assert.Equal(new ushort[] { 0x0100, 0x020D }, new[] { chunks[14].Index, chunks[41].Index });
        Assert.All(chunks, c => Assert.Equal(0x200, c.Data.Length));
        Assert.Equal(42, fake.Gets.Count(g => g.Command == KeypadCommand.GetResult));
        Assert.Equal(CommandBlock.SelectKeymap(Keymap.Green), fake.Sets[^1].Data);
        Assert.Equal(Keymap.Green, new KeypadClient(fake).GetActiveKeymap());
    }

    [Fact]
    public void BadChecksumResendsTheWholeKeymap()
    {
        var fake = new FakeKeypad { CorruptReplies = 1 };
        new KeypadClient(fake).Upload(ProfileCompiler.Compile(KeypadLayout.FactoryProfile()));
        Assert.Equal(43, fake.Sets.Count(s => s.Command == KeypadCommand.SendSetting));   // chunk 0 failed, keymap 0 resent
        Assert.Equal(0x0000, fake.Sets.Where(s => s.Command == KeypadCommand.SendSetting).ElementAt(1).Index);
    }

    [Fact]
    public void GivesUpAfterThreeFailedAttempts()
    {
        var fake = new FakeKeypad { CorruptReplies = 3 };
        Assert.Throws<KeypadException>(() => new KeypadClient(fake).Upload(ProfileCompiler.Compile(KeypadLayout.FactoryProfile())));
        Assert.DoesNotContain(fake.Sets, s => s.Command == KeypadCommand.SetProfileId);
    }

    [Fact]
    public void GuardRefusesFirmwareUpdateAndOversizedTransfers()
    {
        var guarded = new GuardedTransport(new FakeKeypad());
        Assert.Throws<InvalidOperationException>(() => guarded.SetReport(KeypadCommand.EnterIcp, 0, new byte[1]));
        Assert.Throws<InvalidOperationException>(() => guarded.SetReport(KeypadCommand.SendSetting, 0, new byte[0x201]));
        Assert.Throws<InvalidOperationException>(() => guarded.GetReport(KeypadCommand.EnterIcp, 0, 1));
        guarded.SetReport(KeypadCommand.SetProfileId, 0, CommandBlock.SelectKeymap(Keymap.Blue));
    }
}

public class BelkinProfileTests
{
    /// <summary>Belkin's Default.ini is not in the repository; point this variable at a local copy to run the byte-exact tests.</summary>
    private static string? DefaultIni => Environment.GetEnvironmentVariable("N52TE_BELKIN_DEFAULT_INI") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [Fact]
    public void ExportThenImportRoundTrips()
    {
        var p = KeypadLayout.FactoryProfile();
        p.Name = "Round trip";
        p.StartKeymap = Keymap.Red;
        p.Set(Keymap.Green, 3, new MacroAction(TextMacro.Build("gg")));
        p.Set(Keymap.Red, 16, new KeyAction(0x18));

        var text = BelkinProfileFile.Export(p);
        Assert.StartsWith("[Setting]\r\nName=Round trip\r\nExe=\r\nSwitch Flag=0\r\nKey map=2\r\n[macro]\r\nValue=", text);
        var back = BelkinProfileFile.Import(text);
        Assert.Equal(Keymap.Red, back.StartKeymap);
        ProfileModelTests.AssertSameRecords(ProfileCompiler.Compile(p), ProfileCompiler.Compile(back));
    }

    [Fact]
    public void BelkinDefaultIniMatchesFactoryProfileAndRoundTripsByteForByte()
    {
        if (DefaultIni is not { } path) return;   // not available: covered by the synthetic tests above
        var original = File.ReadAllText(path);
        var imported = BelkinProfileFile.Import(original);

        ProfileModelTests.AssertSameRecords(ProfileCompiler.Compile(KeypadLayout.FactoryProfile()), ProfileCompiler.Compile(imported));
        Assert.Equal(original, BelkinProfileFile.Export(imported));
        Assert.Empty(ProfileValidator.Validate(imported));
    }
}
