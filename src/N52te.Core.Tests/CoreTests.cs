namespace N52te.Tests;

public class KeymapImageTests
{
    [Fact]
    public void FactoryImageHasHeaderCodesOptionsAndDiagonalMacro()
    {
        var compiled = ProfileCompiler.Compile(KeypadLayout.FactoryProfile());
        var image = KeymapImage.Build(compiled.Keymaps[1], Keymap.Green);

        Assert.Equal(new byte[] { 0x1C, 0x1B, 0x00, 0x01, 0x01 }, image[..5]);
        Assert.Equal(0x13, image[0x08]);          // input 1 = Tab
        Assert.Equal(0xD4, image[0x08 + 15]);     // input 16 = wheel up = firmware Scroll up
        Assert.Equal(0xB4, image[0x08 + 23]);     // input 24 = its macro marker
        Assert.Equal(2, image[0x23]);             // options 01 01 00, each +1
        Assert.Equal(2, image[0x3E]);
        Assert.Equal(1, image[0x59]);
        Assert.Equal(new byte[] { 0x75, 0x01, 0x5F, 0x01, 0x75, 0x00, 0x5F, 0x00, 0x00 },
            image[(0x100 + 23 * 0x100)..(0x100 + 23 * 0x100 + 9)]);
        Assert.Equal(14, KeymapImage.ChunkCount);
    }

    [Fact]
    public void DelaysAreMergedIntoThePreviousStepFlags()
    {
        var steps = new List<MacroStep> { new(0x14, true, 3), new(0x14, false) };
        var profileSeq = Macro.ToProfileSequence(steps);
        Assert.Equal(new byte[] { 0x14, 0x01, 0xFF, 0x03, 0x14, 0x00 }, profileSeq[..6]);

        var firmware = Macro.ToFirmwareSequence(profileSeq);
        Assert.Equal(new byte[] { 0x14, 0x07, 0x14, 0x00, 0x00 }, firmware[..5]);
    }

    [Fact]
    public void FullMacroEndingOnPressGetsItsReleaseAppended()
    {
        // 127 steps, one of them with a delay: 128 pairs in the profile form, 127 entries once the
        // delay is merged, and the last step is a press.
        var steps = new List<MacroStep> { new(0x12, true, 1) };
        steps.AddRange(Enumerable.Range(0, 125).Select(_ => new MacroStep(0x14, false)));
        steps.Add(new MacroStep(0x22, true));
        Assert.Equal(127, steps.Count);
        Assert.Equal(128, Macro.PairCount(steps));
        var firmware = Macro.ToFirmwareSequence(Macro.ToProfileSequence(steps));
        // 127 entries (the delay merged), then the closing release of 0x22.
        Assert.Equal(0x22, firmware[2 * 126]);
        Assert.Equal(0x01, firmware[2 * 126 + 1]);
        Assert.Equal(0x22, firmware[2 * 127]);
        Assert.Equal(0x00, firmware[2 * 127 + 1]);
    }

    [Fact]
    public void ChecksumIsXorOfEvenAndOddBytes()
    {
        Assert.Equal(((byte)(0x01 ^ 0x03), (byte)(0x02 ^ 0x04)), KeymapImage.Checksum(new byte[] { 1, 2, 3, 4 }));
    }

    [Theory]
    [InlineData(Keymap.Blue, "000800030000000B")]
    [InlineData(Keymap.Green, "000800030001000A")]
    [InlineData(Keymap.Red, "0008000300020009")]
    public void SelectKeymapBlocksMatchTheDocumentedExamples(Keymap keymap, string hex)
    {
        Assert.Equal(hex, Convert.ToHexString(CommandBlock.SelectKeymap(keymap)));
        Assert.True(CommandBlock.IsValid(CommandBlock.SelectKeymap(keymap)));
    }

    [Fact]
    public void PollingRateBlock()
    {
        Assert.Equal("000800040001000D", Convert.ToHexString(CommandBlock.PollingRate(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandBlock.PollingRate(4));
    }
}

public class ProfileModelTests
{
    [Fact]
    public void KeymapSwitchIsWrittenToAllThreeKeymaps()
    {
        var p = KeypadLayout.FactoryProfile();
        p.Set(Keymap.Green, 15, new KeymapSwitchAction(KeymapSwitch.Cycle));
        foreach (var k in p.Keymaps) Assert.Equal(new KeymapSwitchAction(KeymapSwitch.Cycle), k.Inputs[14].Action);
        p.Set(Keymap.Red, 1, new KeyAction(0x18));
        Assert.Equal(new KeyAction(0x13), p.Get(Keymap.Blue, 1).Action);
        Assert.Equal(new KeyAction(0x18), p.Get(Keymap.Red, 1).Action);
    }

    [Fact]
    public void EveryActionTypeRoundTripsThroughJson()
    {
        var p = KeypadLayout.FactoryProfile();
        p.Name = "Test";
        p.StartKeymap = Keymap.Red;
        p.PollingIntervalMs = 1;
        p.Set(Keymap.Blue, 1, new MouseAction(MouseFunction.LeftButton));
        p.Set(Keymap.Blue, 2, new KeymapSwitchAction(KeymapSwitch.RedToggle));
        p.Set(Keymap.Blue, 3, new MacroAction(new List<MacroStep> { new(0x12, true, 4), new(0x12, false) }));
        p.Set(Keymap.Blue, 4, new DisabledAction());
        p.Set(Keymap.Blue, 5, new CompanionAction(CompanionKind.LaunchProgram, @"C:\Windows\notepad.exe"));
        p.Set(Keymap.Blue, 6, new LegacyPcAction(5, 0));
        p.Set(Keymap.Blue, 7, new RawAction(0x33, Enumerable.Repeat((byte)7, 256).ToArray()));
        p.AppRules.Add(new AppRule("game.exe", Keymap.Green));

        var json = ProfileJson.Serialize(p);
        var back = ProfileJson.Deserialize(json);

        Assert.Contains("\"type\": \"companion\"", json);
        Assert.Equal("Test", back.Name);
        Assert.Equal(Keymap.Red, back.StartKeymap);
        Assert.Equal(1, back.PollingIntervalMs);
        Assert.Equal(new AppRule("game.exe", Keymap.Green), back.AppRules.Single());
        AssertSameRecords(ProfileCompiler.Compile(p), ProfileCompiler.Compile(back));
    }

    [Fact]
    public void DecodeRecognisesEachRecordKind()
    {
        var pc = new InputRecord(0x7C, new byte[256], new byte[] { 1, 1, 0 });
        pc.Sequence[0] = 4;
        pc.Sequence[1] = 2;
        Assert.Equal(new LegacyPcAction(4, 2), ProfileCompiler.Decode(1, pc).Action);
        Assert.IsType<DisabledAction>(ProfileCompiler.Decode(1, new InputRecord(0, new byte[256], new byte[3])).Action);
        Assert.Equal(new MouseAction(MouseFunction.ScrollUp), ProfileCompiler.Decode(16, new InputRecord(0xD4, new byte[256], new byte[3])).Action);
        var odd = new byte[256];
        odd[0] = 0x14;
        odd[1] = 0x05;      // flags other than press/release: not a plain macro
        Assert.IsType<RawAction>(ProfileCompiler.Decode(1, new InputRecord(0xA0, odd, new byte[3])).Action);
    }

    [Fact]
    public void KeyActionMayNotUseTheInputsOwnMarkerCodes()
    {
        Assert.Throws<ProfileException>(() => ProfileCompiler.Encode(1, new InputAssignment(new KeyAction(0x7C))));
        Assert.Throws<ProfileException>(() => ProfileCompiler.Encode(1, new InputAssignment(new KeyAction(0xA0))));
        Assert.Throws<ProfileException>(() => ProfileCompiler.Encode(1, new InputAssignment(new KeyAction(0xD0))));
    }

    internal static void AssertSameRecords(CompiledProfile a, CompiledProfile b)
    {
        for (int k = 0; k < 3; k++)
            for (int n = 0; n < 27; n++)
                Assert.Equal(a.Keymaps[k][n].ToBytes(), b.Keymaps[k][n].ToBytes());
    }
}

public class TextAndCompanionTests
{
    [Fact]
    public void TextMacroUsesShiftForUppercaseAndSymbols()
    {
        var steps = TextMacro.Build("Hi!");
        Assert.Equal(new List<MacroStep>
        {
            new(0x83, true), new(0x35, true), new(0x35, false), new(0x83, false),
            new(0x3A, true), new(0x3A, false),
            new(0x83, true), new(0x11, true), new(0x11, false), new(0x83, false),
        }, steps);
    }

    [Fact]
    public void TextMacroRejectsLongTextAndUnknownCharacters()
    {
        Assert.Throws<ProfileException>(() => TextMacro.Build(new string('a', 65)));
        Assert.Equal(128, TextMacro.Build(new string('a', 64)).Count);
        Assert.Throws<ProfileException>(() => TextMacro.Build("é"));
    }

    [Fact]
    public void CompanionActionsGetChordsAndBecomeMacros()
    {
        var p = KeypadLayout.FactoryProfile();
        var launch = new CompanionAction(CompanionKind.LaunchProgram, "notepad.exe");
        p.Set(Keymap.Blue, 1, launch);
        p.Set(Keymap.Red, 1, launch);
        p.Set(Keymap.Blue, 2, new CompanionAction(CompanionKind.ShowDesktop));

        var compiled = ProfileCompiler.Compile(p);
        Assert.Equal(3, compiled.Companion.Count);
        Assert.Equal(2, compiled.Companion.Select(b => b.Chord).Distinct().Count());
        Assert.Equal("Ctrl+Alt+Shift+F1", compiled.Companion[0].Chord.Display);

        var record = compiled.Keymaps[0][0];
        Assert.Equal(0xA0, record.Code);   // input 1's macro marker
        Assert.Equal(new byte[] { 0x88, 1, 0x7D, 1, 0x83, 1, 0x18, 1, 0x18, 0, 0x83, 0, 0x7D, 0, 0x88, 0 }, record.Sequence[..16]);
    }

    [Fact]
    public void TooManyDistinctCompanionActionsIsAnError()
    {
        var p = KeypadLayout.FactoryProfile();
        for (int n = 1; n <= CompanionChords.All.Count + 1; n++)
            p.Set(Keymap.Blue, n, new CompanionAction(CompanionKind.OpenUrl, $"https://example.com/{n}"));
        Assert.Throws<ProfileException>(() => ProfileCompiler.Compile(p));
        Assert.Contains(ProfileValidator.Validate(p), i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void ChordKeysAreUnique()
    {
        Assert.Equal(CompanionChords.All.Count, CompanionChords.All.Select(c => c.VirtualKey).Distinct().Count());
        Assert.Equal(CompanionChords.All.Count, CompanionChords.All.Select(c => c.FirmwareKey).Distinct().Count());
        Assert.All(CompanionChords.All, c => Assert.True(FirmwareCodes.Keys.ContainsKey(c.FirmwareKey)));
    }
}

public class ValidatorTests
{
    [Fact]
    public void FactoryProfileIsClean() => Assert.Empty(ProfileValidator.Validate(KeypadLayout.FactoryProfile()));

    [Fact]
    public void ToggleAwayWithoutWayBackIsFlagged()
    {
        var p = KeypadLayout.FactoryProfile();
        p.Set(Keymap.Blue, 15, new KeymapSwitchAction(KeymapSwitch.GreenToggle));
        Assert.Contains(ProfileValidator.Validate(p), i => i.Message.Contains("toggles back"));
        p.Set(Keymap.Blue, 14, new KeymapSwitchAction(KeymapSwitch.BlueToggle));
        Assert.DoesNotContain(ProfileValidator.Validate(p), i => i.Message.Contains("toggles back"));
    }

    [Fact]
    public void LegacyAndEmptyCompanionActionsAreFlagged()
    {
        var p = KeypadLayout.FactoryProfile();
        p.Set(Keymap.Blue, 1, new LegacyPcAction(5, 0));
        p.Set(Keymap.Blue, 2, new CompanionAction(CompanionKind.LaunchProgram, ""));
        var issues = ProfileValidator.Validate(p);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Warning && i.Input == 1);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Input == 2);
    }
}

public class WindowsKeysTests
{
    [Fact]
    public void EveryMappedVirtualKeyLandsOnANamedFirmwareKey()
    {
        Assert.All(WindowsKeys.MappedVirtualKeys, vk => Assert.True(FirmwareCodes.Keys.ContainsKey(WindowsKeys.ToFirmware(vk)!.Value), $"VK 0x{vk:X2}"));
        Assert.Equal((byte)0x14, WindowsKeys.ToFirmware(0x41));   // A
        Assert.Equal((byte)0x77, WindowsKeys.ToFirmware(0x25));   // Left
        Assert.Null(WindowsKeys.ToFirmware(0x07));
    }

    [Fact]
    public void ChordVirtualKeysAgreeWithTheRecorderMap()
    {
        Assert.All(CompanionChords.All, c => Assert.Equal(c.FirmwareKey, WindowsKeys.ToFirmware(c.VirtualKey)));
    }
}
