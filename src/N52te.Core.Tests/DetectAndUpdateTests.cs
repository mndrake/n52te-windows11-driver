namespace N52te.Tests;

public class RawKeyTests
{
    [Theory]
    [InlineData(0x25, 0x4B, true, 0x77)]    // grey Left
    [InlineData(0x25, 0x4B, false, 0x63)]   // numpad 4 without Num Lock
    [InlineData(0x10, 0x2A, false, 0x83)]   // Left Shift
    [InlineData(0x10, 0x36, false, 0x86)]   // Right Shift
    [InlineData(0x11, 0x1D, true, 0x8E)]    // Right Ctrl
    [InlineData(0x12, 0x38, false, 0x7D)]   // Left Alt
    [InlineData(0x0D, 0x1C, true, 0x74)]    // Numpad Enter
    [InlineData(0x41, 0x1E, false, 0x14)]   // A
    public void TranslatesRawKeys(int vk, int make, bool extended, int expected) =>
        Assert.Equal((byte)expected, WindowsKeys.FromRawKey(vk, make, extended));

    [Fact]
    public void IgnoresFakeShiftAndUnknownKeys()
    {
        Assert.Null(WindowsKeys.FromRawKey(0x10, 0x2A, true));
        Assert.Null(WindowsKeys.FromRawKey(0xFF, 0x00, false));
    }
}

public class MappingDetectorTests
{
    private static DetectedKey P(byte c) => new(c, true);
    private static DetectedKey R(byte c) => new(c, false);

    [Fact]
    public void SingleKeyWithAutoRepeat() =>
        Assert.Equal(new KeyAction(0x13), MappingDetector.Interpret(new DetectedEvent[] { P(0x13), P(0x13), P(0x13), R(0x13) }));

    [Fact]
    public void DiagonalBecomesAMacro()
    {
        var action = Assert.IsType<MacroAction>(MappingDetector.Interpret(new DetectedEvent[] { P(0x75), P(0x5F), R(0x75), R(0x5F) }));
        Assert.True(((MacroAction)KeypadLayout.FactoryAction(24)).Steps.SequenceEqual(action.Steps));
    }

    [Fact]
    public void KeyStillHeldAtTheEndIsReleasedInTheMacro()
    {
        var action = Assert.IsType<MacroAction>(MappingDetector.Interpret(new DetectedEvent[] { P(0x88), P(0x26), R(0x26) }));
        Assert.Equal(new MacroStep(0x88, false), action.Steps[^1]);
    }

    [Fact]
    public void MouseEvents()
    {
        Assert.Equal(new MouseAction(MouseFunction.ScrollUp), MappingDetector.Interpret(new DetectedEvent[] { new DetectedMouse(DetectedMouseKind.WheelUp) }));
        Assert.Equal(new MouseAction(MouseFunction.MiddleButton), MappingDetector.Interpret(new DetectedEvent[]
            { new DetectedMouse(DetectedMouseKind.MoveLeft), new DetectedMouse(DetectedMouseKind.MiddleDown), new DetectedMouse(DetectedMouseKind.MiddleUp) }));
        Assert.Equal(new MouseAction(MouseFunction.PointerDown), MappingDetector.Interpret(new DetectedEvent[]
            { new DetectedMouse(DetectedMouseKind.MoveDown), new DetectedMouse(DetectedMouseKind.MoveDown), new DetectedMouse(DetectedMouseKind.MoveLeft) }));
    }

    [Fact]
    public void NothingSentGivesNull() => Assert.Null(MappingDetector.Interpret(Array.Empty<DetectedEvent>()));
}

public class UpdateTests
{
    private const string Releases = """
    [
      {"tag_name":"v1.3.0","draft":false,"html_url":"u1","assets":[{"name":"Instalar-n52te.exe","browser_download_url":"x"}]},
      {"tag_name":"editor-v0.1.7","draft":false,"html_url":"u7","assets":[
        {"name":"n52te-standalone.zip","browser_download_url":"zip7"},{"name":"SHA256SUMS.txt","browser_download_url":"sum7"}]},
      {"tag_name":"editor-v0.1.12","draft":false,"html_url":"u12","assets":[
        {"name":"n52te-standalone.zip","browser_download_url":"zip12"},{"name":"SHA256SUMS.txt","browser_download_url":"sum12"}]},
      {"tag_name":"editor-v0.1.13","draft":true,"html_url":"u13","assets":[
        {"name":"n52te-standalone.zip","browser_download_url":"zip13"},{"name":"SHA256SUMS.txt","browser_download_url":"sum13"}]},
      {"tag_name":"editor-v0.1.14","draft":false,"html_url":"u14","assets":[{"name":"n52te-standalone.zip","browser_download_url":"zip14"}]}
    ]
    """;

    [Fact]
    public void PicksTheNewestCompleteEditorRelease()
    {
        var r = Updates.PickNewest(Releases);
        Assert.NotNull(r);
        Assert.Equal(new Version(0, 1, 12), r!.Version);
        Assert.Equal("zip12", r.ZipUrl);
        Assert.Equal("sum12", r.SumsUrl);
    }

    [Fact]
    public void ParsesSha256Sums()
    {
        var sums = Updates.ParseSums("1101E526FF518BAECFD25CAF5B63061850C2029F960D695E909E0FC76E9EC256  n52te-editor.exe\r\nbad line\n");
        Assert.Equal("1101e526ff518baecfd25caf5b63061850c2029f960d695e909e0fc76e9ec256", sums["n52te-editor.exe"]);
        Assert.Single(sums);
    }
}
