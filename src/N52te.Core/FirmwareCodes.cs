namespace N52te;

/// <summary>
/// The keypad firmware's key codes. Keys use Razer's own numbering, not HID usages.
/// Extracted from the code→name tables of Belkin's n52teCfg.exe v2.1.2
/// (see docs/reverse-engineering/04-onboard-profile-and-codes.md).
/// </summary>
public static class FirmwareCodes
{
    public const byte Disabled = 0x00;
    public const byte LeftCtrl = 0x88;
    public const byte LeftAlt = 0x7D;
    public const byte LeftShift = 0x83;

    /// <summary>Delay step marker in the editor/profile form of a macro.</summary>
    public const byte DelayStep = 0xFF;

    /// <summary>Single keys, media keys and commands a "key" action may use, with their labels.</summary>
    public static readonly IReadOnlyDictionary<byte, string> Keys = new Dictionary<byte, string>
    {
        [0x10] = "` ~", [0x11] = "1", [0x12] = "Q", [0x13] = "Tab", [0x14] = "A", [0x15] = "Esc",
        [0x16] = "Z", [0x17] = "MuHenkan (JP)", [0x18] = "F1", [0x19] = "2", [0x1A] = "W",
        [0x1B] = "Caps Lock", [0x1C] = "S", [0x1E] = "X", [0x20] = "F2", [0x21] = "3", [0x22] = "E",
        [0x23] = "F3", [0x24] = "D", [0x25] = "F4", [0x26] = "C", [0x27] = "Hangul (KR)", [0x28] = "5",
        [0x29] = "4", [0x2A] = "R", [0x2B] = "T", [0x2C] = "F", [0x2D] = "G", [0x2E] = "V", [0x2F] = "B",
        [0x30] = "6", [0x31] = "7", [0x32] = "U", [0x33] = "Y", [0x34] = "J", [0x35] = "H", [0x36] = "M",
        [0x37] = "N", [0x38] = "=", [0x39] = "8", [0x3A] = "I", [0x3B] = "]", [0x3C] = "K", [0x3D] = "F6",
        [0x3E] = ",", [0x3F] = "` (JP)", [0x40] = "F8", [0x41] = "9", [0x42] = "O", [0x43] = "F7",
        [0x44] = "L", [0x45] = "Henkan (JP)", [0x46] = ".", [0x47] = "Katakana (JP)", [0x48] = "-",
        [0x49] = "0", [0x4A] = "P", [0x4B] = "[", [0x4C] = ";", [0x4D] = "'", [0x4E] = "Extra key (102-key layout)",
        [0x4F] = "/", [0x50] = "F9", [0x51] = "F10", [0x52] = "Code 0x52 (JP Dos/V)", [0x53] = "Backspace",
        [0x54] = "\\", [0x55] = "F5", [0x56] = "Enter", [0x57] = "Space", [0x58] = "Insert", [0x59] = "F12",
        [0x5A] = "Numpad 8", [0x5B] = "Numpad 5", [0x5C] = "Numpad 2", [0x5D] = "Numpad 0",
        [0x5E] = "Numpad /", [0x5F] = "Right", [0x60] = "Delete", [0x61] = "F11", [0x62] = "Numpad 7",
        [0x63] = "Numpad 4", [0x64] = "Numpad 1", [0x66] = "Num Lock", [0x67] = "Down", [0x68] = "Page Up",
        [0x69] = "Page Down", [0x6A] = "Numpad 9", [0x6B] = "Numpad 6", [0x6C] = "Numpad 3",
        [0x6D] = "Numpad .", [0x6E] = "Numpad *", [0x6F] = "Numpad -", [0x70] = "Home", [0x71] = "End",
        [0x72] = "Numpad +", [0x74] = "Numpad Enter", [0x75] = "Up", [0x76] = "Pause/Break", [0x77] = "Left",
        [0x79] = "Scroll Lock", [0x7A] = "Print Screen", [0x7D] = "Left Alt", [0x7F] = "Right Alt",
        [0x83] = "Left Shift", [0x86] = "Right Shift", [0x88] = "Left Ctrl", [0x8E] = "Right Ctrl",
        [0x9A] = "Menu (App)", [0x9C] = "Windows",
        // Commands executed by the firmware (inferred).
        [0xC0] = "Cut", [0xC1] = "Copy", [0xC2] = "Paste", [0xC3] = "Undo", [0xC4] = "Select All",
        [0xC5] = "Find", [0xC6] = "New", [0xC7] = "Save", [0xC8] = "Print", [0xC9] = "Swap Windows",
        [0xCA] = "Close Window", [0xCB] = "Run", [0xCC] = "Show Desktop",
        // Media and WWW keys.
        [0xE8] = "Next Track", [0xE9] = "Previous Track", [0xEA] = "Stop", [0xEB] = "Eject",
        [0xEC] = "Play/Pause", [0xED] = "Mute", [0xEE] = "Volume Up", [0xEF] = "Volume Down",
        [0xF0] = "Media Player", [0xF1] = "Mail", [0xF2] = "Calculator", [0xF3] = "Explorer",
        [0xF4] = "Zoom In", [0xF5] = "Zoom Out", [0xF6] = "Zoom 100%", [0xF7] = "Rotate Picture",
        [0xF8] = "Search", [0xF9] = "Shuffle", [0xFA] = "WWW Home", [0xFB] = "WWW Back",
        [0xFC] = "WWW Forward", [0xFD] = "WWW Stop", [0xFE] = "WWW Refresh", [0xFF] = "WWW Favorites",
    };

    public static string Name(byte code) =>
        Keys.TryGetValue(code, out var name) ? name : $"Code 0x{code:X2}";

    public static bool IsMouse(byte code) => code is >= 0xD0 and <= 0xD8;
    public static bool IsKeymapSwitch(byte code) => code is >= 0xD9 and <= 0xDF;
}

/// <summary>Firmware mouse functions (codes 0xD0–0xD8), executed by the keypad on its mouse interface.</summary>
public enum MouseFunction : byte
{
    LeftButton = 0xD0,
    RightButton = 0xD1,
    MiddleButton = 0xD2,
    ScrollDown = 0xD3,
    ScrollUp = 0xD4,
    PointerLeft = 0xD5,
    PointerRight = 0xD6,
    /// <summary>"Y-axis left" in the Belkin editor; the "Axis Up" choice wrote it (direction inferred).</summary>
    PointerUp = 0xD7,
    /// <summary>"Y-axis right" in the Belkin editor; the "Axis Down" choice wrote it (direction inferred).</summary>
    PointerDown = 0xD8,
}

/// <summary>Firmware keymap switches (codes 0xD9–0xDF). The LED shows the active keymap.</summary>
public enum KeymapSwitch : byte
{
    Cycle = 0xD9,
    BlueToggle = 0xDA,
    BlueMomentary = 0xDB,
    GreenToggle = 0xDC,
    GreenMomentary = 0xDD,
    RedToggle = 0xDE,
    RedMomentary = 0xDF,
}

/// <summary>Keymap index as stored by the keypad. Colour order follows the Belkin editor (unverified on hardware).</summary>
public enum Keymap
{
    Blue = 0,
    Green = 1,
    Red = 2,
}
