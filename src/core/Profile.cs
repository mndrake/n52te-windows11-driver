// Profile model: three keymaps of 27 input records, in the form Belkin's editor keeps them
// (docs/reverse-engineering/04-onboard-profile-and-codes.md, "Record layout").
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace N52te
{
    // One input: what it does (code), its macro (profile form) and the three option bytes
    // (editor form, i.e. before the +1 the upload adds).
    internal sealed class InputRecord
    {
        public const int Size = 0x104;
        public const int SequenceSize = 0x100;

        public byte Code;
        public byte[] Sequence = new byte[SequenceSize];
        public byte[] Options = { 1, 1, 0 }; // Belkin's "new profile" value

        // Set when a keypad image held macro bytes that don't convert to the profile form
        // without loss; used as-is when the record is written back.
        public byte[] FirmwareSequence;

        public InputRecord Clone()
        {
            var r = new InputRecord { Code = Code, Sequence = (byte[])Sequence.Clone(), Options = (byte[])Options.Clone() };
            if (FirmwareSequence != null) r.FirmwareSequence = (byte[])FirmwareSequence.Clone();
            return r;
        }

        public void CopyTo(byte[] blob, int offset)
        {
            blob[offset] = Code;
            Buffer.BlockCopy(Sequence, 0, blob, offset + 1, SequenceSize);
            Buffer.BlockCopy(Options, 0, blob, offset + 1 + SequenceSize, 3);
        }

        public static InputRecord FromBlob(byte[] blob, int offset)
        {
            var r = new InputRecord { Code = blob[offset] };
            Buffer.BlockCopy(blob, offset + 1, r.Sequence, 0, SequenceSize);
            r.Options = new byte[3];
            Buffer.BlockCopy(blob, offset + 1 + SequenceSize, r.Options, 0, 3);
            return r;
        }

        // Macro options in the editor's terms (docs/reverse-engineering/07, verified on hardware).
        public static string DescribeOptions(byte[] o)
        {
            string mode;
            switch (o[0])
            {
                case 0: mode = string.Format("run {0} time{1}", o[1] + 1, o[1] == 0 ? "" : "s"); break;
                case 1: mode = string.Format("repeat while held, every {0} ms", (o[2] + 1) * 50); break;
                case 2: mode = string.Format("repeat until next key, every {0} ms", (o[2] + 1) * 50); break;
                default: mode = string.Format("mode byte {0:X2}: the keypad won't play it", o[0]); break;
            }
            return mode;
        }
    }

    internal sealed class Profile
    {
        public string Name = "Profile";
        public string Exe = "";
        public string SwitchFlag = "0";
        public int StartKeymap;
        public InputRecord[][] Keymaps = new InputRecord[KeypadLayout.KeymapCount][];

        public Profile()
        {
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            {
                Keymaps[k] = new InputRecord[KeypadLayout.InputCount];
                for (int n = 0; n < KeypadLayout.InputCount; n++) Keymaps[k][n] = new InputRecord();
            }
        }

        // Belkin's factory profile (User_Profiles\Default.ini): the same layout in all three keymaps.
        public static Profile Factory()
        {
            var p = new Profile { Name = "Default" };
            byte[][] diagonals =
            {
                new byte[] { 0x75, 1, 0x5F, 1, 0x75, 0, 0x5F, 0 }, // up+right
                new byte[] { 0x67, 1, 0x5F, 1, 0x67, 0, 0x5F, 0 }, // down+right
                new byte[] { 0x67, 1, 0x77, 1, 0x67, 0, 0x77, 0 }, // down+left
                new byte[] { 0x75, 1, 0x77, 1, 0x75, 0, 0x77, 0 }, // up+left
            };
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            {
                for (int n = 1; n <= KeypadLayout.InputCount; n++)
                {
                    var r = p.Keymaps[k][n - 1];
                    r.Code = KeypadLayout.FactoryCode[n - 1];
                    if (n >= 24) Buffer.BlockCopy(diagonals[n - 24], 0, r.Sequence, 0, 8);
                }
            }
            return p;
        }

        // Problems the keypad would show, worth telling the user before writing.
        public List<string> Check()
        {
            var issues = new List<string>();
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                string input = KeypadLayout.InputNames[n - 1];
                bool anySwitch = false, allSame = true;
                for (int k = 0; k < KeypadLayout.KeymapCount; k++)
                {
                    var r = Keymaps[k][n - 1];
                    if (FirmwareCodes.IsKeymapSwitch(r.Code)) anySwitch = true;
                    if (r.Code != Keymaps[0][n - 1].Code) allSame = false;
                    string where = string.Format("{0} keymap, {1}", KeypadLayout.KeymapNames[k], input);
                    if (r.Code == KeypadLayout.MacroMarker[n - 1] && r.Options[0] > 2 && (r.Sequence[0] != 0 || r.FirmwareSequence != null))
                        issues.Add(where + ": macro mode byte " + r.Options[0].ToString("X2", CultureInfo.InvariantCulture) + " is invalid; the keypad won't play this macro");
                    if (r.Code == KeypadLayout.PcActionMarker[n - 1])
                        issues.Add(where + ": PC-side action (Belkin's service ran it); does nothing on Windows 11");
                    else if (FirmwareCodes.IsCommand(r.Code))
                        issues.Add(where + ": command '" + FirmwareCodes.Name(r.Code) + "' sends nothing on Windows 11");
                }
                if (anySwitch && !allSame)
                    issues.Add(input + ": a keymap switch must have the same code in all three keymaps; otherwise the keypad can flip between keymaps");
            }
            return issues;
        }
    }

    // The two macro forms: the profile form (Belkin's editor and .ini files) has delay steps
    // (FF, n x 50 ms); the firmware form (keymap image) folds each delay into the previous step's
    // flags (bits 1-7). Converted like n52teHid.exe 0x4065c0 (docs/reverse-engineering/03).
    internal static class Macro
    {
        public const int MaxPairs = 128;
        public const int MaxDelayUnits = 127;
        public const int DelayUnitMs = 50;

        public static byte[] ToFirmware(byte[] profile)
        {
            var fw = new byte[InputRecord.SequenceSize];
            int count = 0;
            for (int i = 0; i + 1 < profile.Length && i < 2 * MaxPairs; i += 2)
            {
                byte code = profile[i], value = profile[i + 1];
                if (code == 0) break; // end of macro
                if (code == FirmwareCodes.DelayStep)
                {
                    if (count > 0) fw[2 * count - 1] |= (byte)(value << 1);
                    continue;
                }
                fw[2 * count] = code;
                fw[2 * count + 1] = value;
                count++;
            }
            // A macro that ends on a press gets the matching release.
            if (count > 0 && count < MaxPairs && (fw[2 * count - 1] & 1) != 0)
            {
                fw[2 * count] = fw[2 * count - 2];
                fw[2 * count + 1] = 0;
            }
            return fw;
        }

        // Null if the bytes don't convert back without loss (data after the end, or too long).
        public static byte[] FromFirmware(byte[] image, int offset)
        {
            var profile = new byte[InputRecord.SequenceSize];
            int p = 0, i = 0;
            for (; i < 2 * MaxPairs; i += 2)
            {
                byte code = image[offset + i], flags = image[offset + i + 1];
                if (code == 0) break;
                if (p + 2 > profile.Length) return null;
                profile[p++] = code;
                profile[p++] = (byte)(flags & 1);
                if ((flags >> 1) != 0)
                {
                    if (p + 2 > profile.Length) return null;
                    profile[p++] = FirmwareCodes.DelayStep;
                    profile[p++] = (byte)(flags >> 1);
                }
            }
            for (; i < 2 * MaxPairs; i++) if (image[offset + i] != 0) return null;
            return profile;
        }

        // Steps as text, e.g. "H" (tap), "+L Shift" (press), "-L Shift" (release), "1000ms" (delay
        // after the previous step), "0x35" (a code in hex). Separated by spaces; names with
        // spaces are written without them or with underscores ("LShift", "Mouse_Left").
        public static byte[] Parse(string text)
        {
            var steps = new List<byte>();
            foreach (string raw in text.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw;
                if (t.EndsWith("ms", StringComparison.OrdinalIgnoreCase) && t.Length > 2 && char.IsDigit(t[0]))
                {
                    int ms;
                    if (!int.TryParse(t.Substring(0, t.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out ms) || ms <= 0 || ms % DelayUnitMs != 0)
                        throw new ArgumentException("delay '" + t + "' must be a multiple of 50 ms");
                    if (steps.Count == 0) throw new ArgumentException("a macro can't start with a delay");
                    int units = ms / DelayUnitMs;
                    int last = steps.Count - 2;
                    if (steps[last] == FirmwareCodes.DelayStep) units += steps[last + 1];
                    if (units > MaxDelayUnits) throw new ArgumentException("delays after one step add up to more than 6350 ms");
                    if (steps[last] == FirmwareCodes.DelayStep) steps[last + 1] = (byte)units;
                    else { steps.Add(FirmwareCodes.DelayStep); steps.Add((byte)units); }
                    continue;
                }
                int mode = 0; // 0 tap, 1 press, 2 release
                if (t.Length > 1 && (t[0] == '+' || t[0] == '-')) { mode = t[0] == '+' ? 1 : 2; t = t.Substring(1); }
                byte code;
                if (!FirmwareCodes.TryParse(t, out code) || code == 0 || code == FirmwareCodes.DelayStep)
                    throw new ArgumentException("unknown key '" + raw + "' (use a key name such as H, LShift, Mouse_Left, or a hex code such as 0x35)");
                if (mode != 2) { steps.Add(code); steps.Add(1); }
                if (mode != 1) { steps.Add(code); steps.Add(0); }
            }
            if (steps.Count == 0) throw new ArgumentException("the macro has no steps");
            if (steps.Count > 2 * MaxPairs) throw new ArgumentException(string.Format("the macro needs {0} steps; the keypad holds {1} (delays count as steps)", steps.Count / 2, MaxPairs));
            var seq = new byte[InputRecord.SequenceSize];
            steps.CopyTo(seq);
            return seq;
        }

        public static string Describe(byte[] profile)
        {
            var parts = new List<string>();
            for (int i = 0; i + 1 < profile.Length; i += 2)
            {
                byte code = profile[i], v = profile[i + 1];
                if (code == 0) break;
                if (code == FirmwareCodes.DelayStep) parts.Add("(" + v * DelayUnitMs + " ms)");
                else parts.Add((v != 0 ? "+" : "-") + FirmwareCodes.Name(code));
            }
            return parts.Count == 0 ? "(empty)" : string.Join(" ", parts.ToArray());
        }
    }

    internal static class Hex
    {
        public static string Of(byte[] b) { return Of(b, 0, b.Length); }

        public static string Of(byte[] b, int offset, int count)
        {
            var sb = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(b[offset + i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
