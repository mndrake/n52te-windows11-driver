// The n52te's 27 programmable inputs and three keymaps.
// Tables from n52teCfg.exe v2.1.2 (0x4a487c macro markers, 0x4a4898 PC-action markers, 0x4a4860
// factory codes); slot order verified on hardware (docs/reverse-engineering/03-usb-protocol.md).
using System;

namespace N52te
{
    internal static class KeypadLayout
    {
        public const int InputCount = 27;
        public const int KeymapCount = 3;

        // Firmware keymap numbers: 0 = Blue, 1 = Red, 2 = Green (verified on hardware).
        public static readonly string[] KeymapNames = { "Blue", "Red", "Green" };

        public static readonly string[] InputNames =
        {
            "Key 01", "Key 02", "Key 03", "Key 04", "Key 05", "Key 06", "Key 07", "Key 08", "Key 09", "Key 10",
            "Key 11", "Key 12", "Key 13", "Key 14", "Thumb button (round)", "Wheel up", "Wheel click", "Wheel down",
            "Thumb pad", "D-pad up", "D-pad right", "D-pad down", "D-pad left",
            "D-pad up+right", "D-pad down+right", "D-pad down+left", "D-pad up+left",
        };

        // Per-input macro marker. Writing it as the input's code plays the macro in the input's slot.
        // The input's slot in the keymap image (key-matrix order) is marker - 0xA0.
        public static readonly byte[] MacroMarker =
        {
            0xA0, 0xA4, 0xA8, 0xAC, 0xB0, 0xA1, 0xA5, 0xA9, 0xAD, 0xB1, 0xA2, 0xA6, 0xAA, 0xAE, 0xA3,
            0xBA, 0xB8, 0xB9, 0xB2, 0xB3, 0xAB, 0xAF, 0xA7, 0xB4, 0xB5, 0xB6, 0xB7,
        };

        // Per-input PC-action marker: Belkin's service ran these; nothing does on Windows 11.
        public static readonly byte[] PcActionMarker =
        {
            0x7C, 0x7E, 0x80, 0x81, 0x82, 0x84, 0x85, 0x87, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8F, 0x90,
            0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x99, 0x9B, 0x9D, 0x9E, 0x9F,
        };

        // Factory codes (identical in all three keymaps). Inputs 24-27 are macro markers: the
        // factory profile plays a two-key macro on the D-pad diagonals.
        public static readonly byte[] FactoryCode =
        {
            0x13, 0x12, 0x1A, 0x22, 0x2A, 0x1B, 0x14, 0x1C, 0x24, 0x2C, 0x83, 0x16, 0x1E, 0x26, 0x7D,
            0xD4, 0xD2, 0xD3, 0x57, 0x75, 0x5F, 0x67, 0x77, 0xB4, 0xB5, 0xB6, 0xB7,
        };

        public static int Slot(int input)
        {
            CheckInput(input);
            return MacroMarker[input - 1] - 0xA0;
        }

        public static void CheckInput(int input)
        {
            if (input < 1 || input > InputCount) throw new ArgumentOutOfRangeException("input", input, "inputs are numbered 1-27");
        }

        public static int ParseKeymap(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "0": case "blue": return 0;
                case "1": case "red": return 1;
                case "2": case "green": return 2;
            }
            throw new ArgumentException("keymap must be blue/red/green or 0/1/2: " + s);
        }
    }
}
