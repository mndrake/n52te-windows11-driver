// n52te-config selftest: checks the core library without a keypad.
//
//   * slot table, command blocks, macro conversion (against byte sequences verified on hardware);
//   * the factory profile against Belkin's User_Profiles\Default.ini, if given (not in the repo);
//   * every saved keymap image in Documents\n52te keymaps: image -> records -> image must be
//     byte-identical, and records -> .ini blob -> records too;
//   * the protocol code (write, checksums, verify, the command-7 guard) against a simulated keypad.
using System;
using System.Collections.Generic;
using System.IO;

namespace N52te
{
    internal static class SelfTest
    {
        private static int failures;
        private static TextWriter o;

        public static int Run(string defaultIni, string imagesDir, TextWriter output)
        {
            o = output;
            failures = 0;
            Layout();
            CommandBlocks();
            Macros();
            Factory(defaultIni);
            Images(imagesDir);
            Protocol();
            o.WriteLine(failures == 0 ? "selftest: all checks passed" : "selftest: " + failures + " check(s) FAILED");
            return failures == 0 ? 0 : 6;
        }

        private static void Check(bool ok, string what)
        {
            o.WriteLine("  {0} {1}", ok ? "ok  " : "FAIL", what);
            if (!ok) failures++;
        }

        private static bool Eq(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static byte[] Bytes(params int[] v)
        {
            var b = new byte[v.Length];
            for (int i = 0; i < v.Length; i++) b[i] = (byte)v[i];
            return b;
        }

        private static byte[] Head(byte[] b, int n)
        {
            var r = new byte[n];
            Buffer.BlockCopy(b, 0, r, 0, n);
            return r;
        }

        private static void Layout()
        {
            o.WriteLine("layout");
            var seen = new bool[KeypadLayout.InputCount];
            bool ok = true;
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                int s = KeypadLayout.Slot(n);
                if (s < 0 || s >= KeypadLayout.InputCount || seen[s]) ok = false; else seen[s] = true;
            }
            Check(ok, "the 27 inputs map to the 27 slots one to one");
            Check(KeypadLayout.Slot(16) == 26 && KeypadLayout.Slot(23) == 7, "wheel up is slot 26, D-pad left slot 7 (as decoded on hardware)");
        }

        private static void CommandBlocks()
        {
            o.WriteLine("command blocks");
            Check(Eq(KeypadClient.Block(3, 1), Bytes(0x00, 0x08, 0x00, 0x03, 0x00, 0x01, 0x00, 0x0A)), "select keymap 1 = 00 08 00 03 00 01 00 0A");
            Check(Eq(KeypadClient.Block(4, 1), Bytes(0x00, 0x08, 0x00, 0x04, 0x00, 0x01, 0x00, 0x0D)), "polling 1000 Hz = 00 08 00 04 00 01 00 0D");
            Check(KeypadClient.ChecksumOk(Bytes(0x00, 0x08, 0x01, 0x06, 0x03, 0x08, 0x02, 0x06)), "GET_POLLINGRATE reply seen on hardware has a valid checksum");
        }

        private static void Macros()
        {
            o.WriteLine("macros");
            // Typed "a", paused about a second, then "b" on the keypad (18 Nov 2026).
            byte[] fw = Macro.ToFirmware(Macro.Parse("A 1000ms B"));
            Check(Eq(Head(fw, 10), Bytes(0x14, 0x01, 0x14, 0x28, 0x2F, 0x01, 0x2F, 0x00, 0, 0)), "\"A 1000ms B\" uploads as 14 01 14 28 2F 01 2F 00 (verified on hardware)");
            Check(Eq(Head(Macro.Parse("+L_Shift +H -H -LShift"), 8), Bytes(0x83, 1, 0x35, 1, 0x35, 0, 0x83, 0)), "press/release tokens and names with spaces");
            Check(Eq(Head(Macro.Parse("0x35 100ms 50ms"), 6), Bytes(0x35, 1, 0x35, 0, 0xFF, 3)), "hex codes; consecutive delays add up");
            Check(Eq(Head(Macro.ToFirmware(Macro.Parse("+H")), 6), Bytes(0x35, 1, 0x35, 0, 0, 0)), "a macro ending on a press gets its release (as n52teHid.exe)");
            Check(Eq(Head(Macro.Parse("Mouse_Left"), 4), Bytes(0xD0, 1, 0xD0, 0)), "mouse button by name");
            bool threw = false;
            try { Macro.Parse("1000ms H"); } catch (ArgumentException) { threw = true; }
            Check(threw, "a macro can't start with a delay");
            threw = false;
            try { Macro.Parse("NoSuchKey"); } catch (ArgumentException) { threw = true; }
            Check(threw, "unknown key names are refused");
            byte[] back = Macro.FromFirmware(fw, 0);
            Check(back != null && Eq(Macro.ToFirmware(back), fw), "firmware form -> profile form -> firmware form");
        }

        private static void Factory(string defaultIni)
        {
            o.WriteLine("factory profile");
            Profile f = Profile.Factory();
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            {
                byte[] img = KeymapImage.Build(f.Keymaps[k], k);
                Check(KeymapImage.FirstDifference(KeymapImage.Build(KeymapImage.Parse(img), k), img) < 0, "factory " + KeypadLayout.KeymapNames[k] + " keymap: image -> records -> image");
            }
            if (string.IsNullOrEmpty(defaultIni))
            {
                o.WriteLine("  skip Belkin's Default.ini not given (selftest PATH or N52TE_BELKIN_DEFAULT_INI)");
                return;
            }
            var warnings = new List<string>();
            Profile d = ProfileIni.Read(defaultIni, warnings);
            Check(warnings.Count == 0, "Default.ini reads without warnings" + (warnings.Count > 0 ? ": " + warnings[0] : ""));
            byte[] blob = ProfileIni.ToBlob(d);
            Check(blob[ProfileIni.RecordsSize] == 0x9A, "Default.ini checksum byte is 9A, the 8-bit sum of the records");
            Check(Eq(ProfileIni.ToBlob(f), blob), "built-in factory profile is byte-identical with Default.ini");
            string text = File.ReadAllText(defaultIni, System.Text.Encoding.Default);
            Check(ProfileIni.ToText(d) == text, "Default.ini written back is identical to the file");
        }

        private static void Images(string dir)
        {
            o.WriteLine("saved keymap images in " + dir);
            if (!Directory.Exists(dir)) { o.WriteLine("  skip folder not found"); return; }
            int count = 0, raw = 0;
            foreach (string path in Directory.GetFiles(dir, "*.bin", SearchOption.AllDirectories))
            {
                byte[] img = File.ReadAllBytes(path);
                if (img.Length != KeymapImage.Size || img[0] != 0x1C) continue;
                int km = img[1] == 0 ? img[4] - 1 : img[4]; // read-back header 1C 00 00 01 KM+1
                if (km < 0 || km > 2) km = 0;
                count++;
                InputRecord[] recs = KeymapImage.Parse(img);
                foreach (var r in recs) if (r.FirmwareSequence != null) raw++;
                int d = KeymapImage.FirstDifference(KeymapImage.Build(recs, km), img);
                if (d >= 0) Check(false, Path.GetFileName(path) + ": image -> records -> image differs at 0x" + d.ToString("X"));

                var p = new Profile();
                p.Keymaps[0] = recs;
                Profile q = ProfileIni.FromBlob(ProfileIni.ToBlob(p));
                bool same = true;
                for (int n = 0; n < KeypadLayout.InputCount; n++)
                {
                    if (recs[n].FirmwareSequence != null) continue; // can't be held in a .ini
                    var a = new byte[InputRecord.Size];
                    var b = new byte[InputRecord.Size];
                    recs[n].CopyTo(a, 0);
                    q.Keymaps[0][n].CopyTo(b, 0);
                    if (!Eq(a, b)) same = false;
                }
                if (!same) Check(false, Path.GetFileName(path) + ": records -> .ini blob -> records differs");
            }
            if (count == 0) o.WriteLine("  skip no saved keymap images (n52te-config read saves some)");
            else Check(true, count + " image(s): image -> records -> image byte-identical, records survive a .ini round trip" + (raw > 0 ? " (" + raw + " record(s) kept as raw macro bytes)" : ""));
        }

        // A keypad that answers like the real one: keeps uploaded chunks, returns their checksum,
        // reads them back, and records whether command 7 ever reached it.
        private sealed class SimulatedKeypad : IKeypadTransport
        {
            public readonly byte[][] Keymaps = { new byte[KeymapImage.Size], new byte[KeymapImage.Size], new byte[KeymapImage.Size] };
            public bool SawCommand7;
            public int Active;
            private byte[] lastChecksum = { 0, 0 };

            public byte[] Control(byte requestType, byte request, ushort value, ushort index, byte[] dataOut, int lengthIn)
            {
                if ((requestType & 0x60) == 0x20 && value == 7) SawCommand7 = true;
                int km = index >> 8, chunk = index & 0xFF;
                if (request == 0x09 && value == 1)
                {
                    Buffer.BlockCopy(dataOut, 0, Keymaps[km], chunk * KeymapImage.ChunkSize, KeymapImage.ChunkSize);
                    lastChecksum = KeymapImage.ChunkChecksum(dataOut, 0, dataOut.Length);
                    return null;
                }
                if (request == 0x09 && value == 4) { Active = dataOut[5]; return null; }
                if (request == 0x01 && value == 2) return (byte[])lastChecksum.Clone();
                if (request == 0x01 && value == 3)
                {
                    var d = new byte[KeymapImage.ChunkSize];
                    Buffer.BlockCopy(Keymaps[km], chunk * KeymapImage.ChunkSize, d, 0, d.Length);
                    return d;
                }
                if (request == 0x01 && value == 5) return KeypadClient.Block(3, (byte)Active);
                return null;
            }

            public void Dispose() { }
        }

        private static void Protocol()
        {
            o.WriteLine("protocol (simulated keypad)");
            var sim = new SimulatedKeypad();
            using (var pad = new KeypadClient(sim))
            {
                byte[] img = KeymapImage.Build(Profile.Factory().Keymaps[1], 1);
                Check(pad.WriteAndVerify(1, img, null) == null, "write a keymap with per-chunk checksums, read it back, compare");
                Check(KeymapImage.FirstDifference(sim.Keymaps[1], img) < 0, "the simulated keypad holds exactly the uploaded image");
                pad.SelectKeymap(2);
                Check(pad.ActiveKeymap() == 2, "select keymap, read it back");
                bool refused = false;
                try { pad.Control(0x21, 0x09, 7, 0, new byte[1], 0); } catch (InvalidOperationException) { refused = true; }
                Check(refused && !sim.SawCommand7, "command 7 (firmware update) is refused before it reaches the keypad");
            }
        }
    }
}
