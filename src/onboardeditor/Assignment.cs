// How an input's assignment is shown in its label box, and the editor's right-click menu items
// (Belkin's menu, minus what can't work on Windows 11).
using System;

namespace N52te
{
    internal static class Assignment
    {
        public static string Short(int input, InputRecord r)
        {
            if (r.Code == FirmwareCodes.Disabled) return "Disabled";
            if (r.Code == KeypadLayout.PcActionMarker[input - 1]) return "PC action (off)";
            if (r.Code == KeypadLayout.MacroMarker[input - 1])
            {
                if (r.FirmwareSequence != null) return "Macro";
                if (r.Sequence[0] == 0) return "Empty macro";
                return "Macro: " + Macro.ToText(r.Sequence);
            }
            switch (r.Code)
            {
                case 0xD9: return "Cycle Keymaps";
                case 0xDA: return "Blue Toggle";
                case 0xDB: return "Blue Momentary";
                case 0xDC: return "Green Toggle";
                case 0xDD: return "Green Momentary";
                case 0xDE: return "Red Toggle";
                case 0xDF: return "Red Momentary";
            }
            return FirmwareCodes.Name(r.Code);
        }

        // Shown greyed: does nothing on Windows 11.
        public static bool IsInactive(int input, InputRecord r)
        {
            return r.Code == FirmwareCodes.Disabled || r.Code == KeypadLayout.PcActionMarker[input - 1] || FirmwareCodes.IsCommand(r.Code)
                || (r.Code == KeypadLayout.MacroMarker[input - 1] && r.FirmwareSequence == null && r.Sequence[0] == 0);
        }

        public sealed class Item
        {
            public readonly string Text;
            public readonly byte Code;
            public readonly string Note; // set: shown greyed with this explanation

            public bool Works { get { return Note != NothingOnWindows; } }

            public Item(string text, byte code) { Text = text; Code = code; }
            public Item(string text, byte code, string note) { Text = text; Code = code; Note = note; }
        }

        private const string NothingOnWindows = "The keypad sends nothing for this on Windows 11 (Belkin's background service did it).";
        private const string NoWindowsKey = "The keypad sends it, but Windows has no key for it; most programs ignore it.";

        public static readonly Item[] Keymap =
        {
            new Item("Cycle Keymaps", 0xD9),
            new Item("Blue Keymap Toggle", 0xDA), new Item("Blue Keymap Momentary", 0xDB),
            new Item("Green Keymap Toggle", 0xDC), new Item("Green Keymap Momentary", 0xDD),
            new Item("Red Keymap Toggle", 0xDE), new Item("Red Keymap Momentary", 0xDF),
        };

        public static readonly Item[] Mouse =
        {
            new Item("Left Mouse Button", 0xD0), new Item("Middle Mouse Button", 0xD2), new Item("Right Mouse Button", 0xD1),
            new Item("Scroll Wheel Up", 0xD4), new Item("Scroll Wheel Down", 0xD3),
            new Item("Pointer Up", 0xD8), new Item("Pointer Down", 0xD7), new Item("Pointer Left", 0xD6), new Item("Pointer Right", 0xD5),
        };

        public static readonly Item[] BasicCommands =
        {
            new Item("Cut", 0xC0, NothingOnWindows), new Item("Copy", 0xC1, NothingOnWindows), new Item("Paste", 0xC2, NothingOnWindows),
            new Item("Undo", 0xC3, NothingOnWindows), new Item("All", 0xC4, NothingOnWindows), new Item("Find", 0xC5, NothingOnWindows),
            new Item("New", 0xC6, NothingOnWindows), new Item("Save", 0xC7, NothingOnWindows), new Item("Print", 0xC8, NothingOnWindows),
        };

        public static readonly Item[] AdditionalCommands =
        {
            new Item("Swap Windows", 0xC9, NothingOnWindows), new Item("Close Windows", 0xCA, NothingOnWindows),
            new Item("Open Explorer", 0xF3), new Item("Run", 0xCB, NothingOnWindows), new Item("Show Desktop", 0xCC, NothingOnWindows),
            new Item("Zoom In", 0xF4, NoWindowsKey), new Item("Zoom Out", 0xF5, NoWindowsKey),
            new Item("Maximize Window", 0xCD, NothingOnWindows), new Item("Minimize Window", 0xCE, NothingOnWindows),
        };

        public static readonly Item[] Media =
        {
            new Item("Play/Pause", 0xEC), new Item("Stop", 0xEA), new Item("Previous Track", 0xE9), new Item("Next Track", 0xE8),
            new Item("Shuffle", 0xF9, NoWindowsKey), new Item("Volume Up", 0xEE), new Item("Volume Down", 0xEF), new Item("Mute", 0xED),
        };

        public static readonly Item[] MoreMedia =
        {
            new Item("Media Player", 0xF0), new Item("Mail", 0xF1), new Item("Calculator", 0xF2), new Item("Search", 0xF8),
            new Item("Browser Home", 0xFA), new Item("Browser Back", 0xFB), new Item("Browser Forward", 0xFC),
            new Item("Browser Stop", 0xFD), new Item("Browser Refresh", 0xFE), new Item("Browser Favorites", 0xFF),
            new Item("Eject", 0xEB, NoWindowsKey), new Item("Zoom 100%", 0xF6, NoWindowsKey), new Item("Rotate Picture", 0xF7, NoWindowsKey),
        };
    }
}
