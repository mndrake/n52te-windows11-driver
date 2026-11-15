#!/usr/bin/env python3
"""Decode a Belkin n52te v2.1.2 profile (*.ini, [macro] Value=<hex>).

The blob is the editor's copy of the keypad's onboard (Synapse) memory:
3 keymaps (0=Blue, 1=Green, 2=Red) x 27 inputs x 0x104-byte records, plus
one trailing byte. Each record is:

    +0x000  code     firmware key code of the input (table below)
    +0x001  seq      128 x (code, flags) pairs; flags bit0 = 1 press / 0 release,
                     delays are stored as code 0xFF entries in the editor copy
    +0x101  p1, p2, p3  per-input options (factory: 01 01 00)

Usage: decode_n52te_profile.py Default.ini
"""
import re
import sys

# Firmware key codes, extracted from n52teCfg.exe v2.1.2 (code -> label).
CODES = {
    0x10: "~", 0x11: "1", 0x12: "Q", 0x13: "Tab", 0x14: "A", 0x15: "Esc", 0x16: "Z",
    0x17: "MuHenkan", 0x18: "F1", 0x19: "2", 0x1A: "W", 0x1B: "Caps Lock", 0x1C: "S",
    0x1E: "X", 0x20: "F2", 0x21: "3", 0x22: "E", 0x23: "F3", 0x24: "D", 0x25: "F4",
    0x26: "C", 0x27: "Hanguel", 0x28: "5", 0x29: "4", 0x2A: "R", 0x2B: "T", 0x2C: "F",
    0x2D: "G", 0x2E: "V", 0x2F: "B", 0x30: "6", 0x31: "7", 0x32: "U", 0x33: "Y",
    0x34: "J", 0x35: "H", 0x36: "M", 0x37: "N", 0x38: "=", 0x39: "8", 0x3A: "I",
    0x3B: "]", 0x3C: "K", 0x3D: "F6", 0x3E: ",", 0x3F: "`", 0x40: "F8", 0x41: "9",
    0x42: "O", 0x43: "F7", 0x44: "L", 0x45: "Henkan", 0x46: ".", 0x47: "Katakana",
    0x48: "-", 0x49: "0", 0x4A: "P", 0x4B: "[", 0x4C: ";", 0x4D: "'", 0x4E: "102nd key",
    0x4F: "/", 0x50: "F9", 0x51: "F10", 0x53: "Backspace", 0x54: "\\", 0x55: "F5",
    0x56: "Enter", 0x57: "Space", 0x58: "Insert", 0x59: "F12", 0x5A: "Num8",
    0x5B: "Num5", 0x5C: "Num2", 0x5D: "Num0", 0x5E: "Num/", 0x5F: "Right",
    0x60: "Delete", 0x61: "F11", 0x62: "Num7", 0x63: "Num4", 0x64: "Num1",
    0x66: "Num Lock", 0x67: "Down", 0x68: "Page Up", 0x69: "Page Down", 0x6A: "Num9",
    0x6B: "Num6", 0x6C: "Num3", 0x6D: "Num.", 0x6E: "Num*", 0x6F: "Num-", 0x70: "Home",
    0x71: "End", 0x72: "Num+", 0x74: "NumEnter", 0x75: "Up", 0x76: "Pause/Break",
    0x77: "Left", 0x79: "Scroll Lock", 0x7A: "PrtScr", 0x7D: "L Alt", 0x7F: "R Alt",
    0x83: "L Shift", 0x86: "R Shift", 0x87: "Profile", 0x88: "L Ctrl", 0x8E: "R Ctrl",
    0x8F: "Mute", 0x9A: "App", 0x9B: "DPad NE", 0x9C: "Win", 0x9D: "DPad SE",
    0x9E: "DPad SW", 0x9F: "DPad NW",
    # Firmware-executed pseudo keys (handled inside the keypad, not by Windows)
    0xC0: "Cut", 0xC1: "Copy", 0xC2: "Paste",
    0xD0: "MOUSE Left Button", 0xD1: "MOUSE Right Button", 0xD2: "MOUSE Middle Button",
    0xD3: "MOUSE Scroll down", 0xD4: "MOUSE Scroll up",
    0xD5: "MOUSE X-axis left", 0xD6: "MOUSE X-axis right",
    0xD7: "MOUSE Y-axis left", 0xD8: "MOUSE Y-axis right",
    0xD9: "KEYMAP Cycle", 0xDA: "KEYMAP Blue toggle", 0xDB: "KEYMAP Blue momentary",
    0xDC: "KEYMAP Green toggle", 0xDD: "KEYMAP Green momentary",
    0xDE: "KEYMAP Red toggle", 0xDF: "KEYMAP Red momentary",
    0xE8: "Next Track", 0xE9: "Prev Track", 0xEB: "Eject", 0xEC: "Play/Pause",
    0xEE: "Volume Up", 0xEF: "Volume Down", 0xF0: "Media",
}

# Physical input for each of the 27 record slots (from the factory profile + manual).
INPUTS = [
    "Key 01", "Key 02", "Key 03", "Key 04", "Key 05", "Key 06", "Key 07", "Key 08",
    "Key 09", "Key 10", "Key 11", "Key 12", "Key 13", "Key 14",
    "Thumb button (round)", "Wheel up", "Wheel click", "Wheel down",
    "Thumb pad", "D-pad up", "D-pad right", "D-pad down", "D-pad left",
    "D-pad up+right", "D-pad down+right", "D-pad down+left", "D-pad up+left",
]
KEYMAPS = ["Blue", "Green", "Red"]
REC = 0x104


def name(code):
    return CODES.get(code, f"0x{code:02X}")


def main(path):
    text = open(path, encoding="latin-1").read()
    m = re.search(r"Value=([0-9A-Fa-f]+)", text)
    if not m:
        sys.exit("no [macro] Value= blob found")
    blob = bytes.fromhex(m.group(1))
    print(f"{path}: {len(blob)} bytes")
    for km, km_name in enumerate(KEYMAPS):
        print(f"\n== Keymap {km} ({km_name})")
        for i, label in enumerate(INPUTS):
            rec = blob[(km * 27 + i) * REC:(km * 27 + i + 1) * REC]
            code, seq, opts = rec[0], rec[1:0x101], rec[0x101:0x104]
            steps = []
            for j in range(0, 0x100, 2):
                c, f = seq[j], seq[j + 1]
                if c == 0 and f == 0:
                    break
                steps.append(("+" if f & 1 else "-") + name(c))
            macro = f"  macro: {' '.join(steps)}" if steps else ""
            print(f"  {i + 1:2d} {label:<22} {name(code):<22} opts={opts.hex()}{macro}")
    tail = blob[3 * 27 * REC:]
    print(f"\ntrailing bytes: {tail.hex() or '(none)'}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Default.ini")
