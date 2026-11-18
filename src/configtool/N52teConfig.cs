// n52te-config: read, switch and write the Belkin n52te's onboard keymaps from Windows.
//
// The keypad runs its keymaps itself (keys, mouse, wheel, macros, keymap switching and the LED).
// This tool talks to its configuration protocol (docs/reverse-engineering/03-usb-protocol.md)
// through the core library in src/core. Transport: UsbDk, which lends the keypad to this program
// while it runs; the keypad does NOT type meanwhile.
//
// Build (in-box .NET Framework compiler, C# 5):
//   csc /platform:x64 /out:n52te-config.exe src\core\*.cs src\configtool\*.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace N52te
{
    internal static class Program
    {
        private const string Usage =
@"n52te-config - read, switch and write the Belkin n52te's onboard keymaps (via UsbDk).
The keypad doesn't type while this runs; Windows gets it back when it exits.
Keymaps: blue (0), red (1), green (2). Inputs: 1-14 keys, 15 round thumb button, 16-18 wheel
up/click/down, 19 thumb pad, 20-23 D-pad up/right/down/left, 24-27 diagonals.

Keypad, read-only:
  n52te-config info                     firmware version and active keymap
  n52te-config read [0|1|2|all] [DIR]   read keymaps back, decode them, save keymapN.bin in DIR
  n52te-config export FILE.ini [NAME]   save all three keymaps as a Belkin v2.1.2 profile
  n52te-config descriptor               USB poll interval of each endpoint
  n52te-config listen [SECONDS]         print the raw key and mouse reports (diagnostic)

Keypad, changes (each write backs up the keymap first and verifies by reading it back):
  n52te-config select blue|red|green    make that keymap active (the LED follows; kept after unplugging)
  n52te-config polling [125|500|1000]   keyboard polling rate in Hz (applies after replugging)
  n52te-config apply FILE.ini --yes     write a Belkin profile: all three keymaps, then its start keymap
                                        (--dry-run instead of --yes: show what would change)
  n52te-config macro KM INPUT ""STEPS"" [--mode once|held|untilnext] [--times N] [--interval MS] --yes
                                        put a macro on one input, e.g. ""H I"", ""+LShift A -LShift"",
                                        ""A 1000ms B"", ""Mouse_Left""; mode: run N times (1-10), repeat
                                        while held, or repeat until the next key (interval 50-250 ms)
  n52te-config set KM INPUT CODE --yes  set one input to a firmware code (hex), verify
  n52te-config write FILE KM --yes      write a saved keymap image (e.g. a backup) as keymap KM
  n52te-config roundtrip 0|1|2          write a keymap back unchanged and verify it

No keypad needed:
  n52te-config show FILE                decode a keymap image (.bin) or a profile (.ini)
  n52te-config selftest [DEFAULT.INI]   check this tool against known data and a simulated keypad

Backups and read-back images go to Documents\n52te keymaps (read: unless DIR is given).";

        // Durable and writable by the user wherever the tool is run from: it's installed under
        // Program Files, and a backup must never fail because the current folder is read-only.
        private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "n52te keymaps");

        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "/?") { Console.WriteLine(Usage); return 1; }
            try
            {
                return Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("error: " + e.Message);
                return 2;
            }
        }

        private static bool HasYes(string[] args) { return Array.IndexOf(args, "--yes") >= 0; }

        private static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static int Run(string[] args)
        {
            string cmd = args[0].ToLowerInvariant();
            switch (cmd)
            {
                case "selftest":
                    return SelfTest.Run(args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("N52TE_BELKIN_DEFAULT_INI"), DataDir, Console.Out);
                case "show":
                    if (args.Length < 2) throw new ArgumentException("usage: show FILE");
                    return Show(args[1]);
            }

            using (var pad = KeypadClient.OpenUsbDk())
            {
                byte[] fw = pad.Firmware();
                byte[] pid = pad.ProfileId();
                Console.WriteLine("n52te firmware reply {0} ({1}); active keymap {2} ({3})", Hex.Of(fw), KeypadClient.ChecksumOk(fw) ? "checksum ok" : "checksum MISMATCH", pid[5], pid[5] < 3 ? KeypadLayout.KeymapNames[pid[5]] : "?");
                switch (cmd)
                {
                    case "info":
                        return 0;

                    case "listen":
                        ((UsbDkTransport)pad.Transport).Listen(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 20, Console.Out);
                        return 0;

                    case "descriptor":
                    {
                        byte[] d = pad.ConfigurationDescriptor();
                        Console.WriteLine("configuration descriptor ({0} bytes): {1}", d.Length, Hex.Of(d));
                        for (int i = 0; i + 1 < d.Length && d[i] > 0; i += d[i])
                        {
                            if (d[i + 1] == 4 && i + 8 < d.Length)
                                Console.WriteLine("  interface {0}: class {1:X2} subclass {2:X2} protocol {3:X2}", d[i + 2], d[i + 5], d[i + 6], d[i + 7]);
                            else if (d[i + 1] == 5 && i + 6 < d.Length)
                                Console.WriteLine("  endpoint {0:X2}: attributes {1:X2}, max packet {2}, poll interval {3} ms", d[i + 2], d[i + 3], d[i + 4] | (d[i + 5] << 8), d[i + 6]);
                        }
                        return 0;
                    }

                    case "polling":
                    {
                        if (args.Length > 1)
                        {
                            byte ms;
                            switch (args[1])
                            {
                                case "125": ms = 8; break;
                                case "500": ms = 2; break;
                                case "1000": ms = 1; break;
                                default: throw new ArgumentException("polling rate must be 125, 500 or 1000");
                            }
                            pad.SetPollingInterval(ms);
                            Console.WriteLine("sent SET_POLLINGRATE: {0} Hz (interval {1} ms); replug the keypad to apply it", args[1], ms);
                        }
                        byte[] r = pad.PollingRate();
                        bool ok = KeypadClient.ChecksumOk(r);
                        Console.WriteLine("GET_POLLINGRATE reply {0}: {1}", Hex.Of(r), ok && r[5] > 0 ? string.Format("interval {0} ms ({1} Hz)", r[5], 1000 / r[5]) : "unexpected reply");
                        return 0;
                    }

                    case "select":
                    {
                        if (args.Length < 2) throw new ArgumentException("usage: select blue|red|green");
                        int km = KeypadLayout.ParseKeymap(args[1]);
                        pad.SelectKeymap(km);
                        int now = pad.ActiveKeymap();
                        Console.WriteLine("selected keymap {0} ({1}); keypad now reports {2}", km, KeypadLayout.KeymapNames[km], now);
                        return now == km ? 0 : 3;
                    }

                    case "read":
                    {
                        string which = args.Length > 1 ? args[1] : "all";
                        string dir = args.Length > 2 ? args[2] : DataDir;
                        Directory.CreateDirectory(dir);
                        int[] kms = which == "all" ? new[] { 0, 1, 2 } : new[] { KeypadLayout.ParseKeymap(which) };
                        foreach (int km in kms)
                        {
                            byte[] image = pad.ReadKeymap(km);
                            string path = Path.Combine(dir, "keymap" + km + ".bin");
                            File.WriteAllBytes(path, image);
                            KeymapImage.Describe(km, image, Console.Out);
                            Console.WriteLine("  saved " + Path.GetFullPath(path));
                        }
                        return 0;
                    }

                    case "export":
                    {
                        if (args.Length < 2) throw new ArgumentException("usage: export FILE.ini [NAME]");
                        var images = new byte[3][];
                        for (int km = 0; km < 3; km++) images[km] = pad.ReadKeymap(km);
                        var warnings = new List<string>();
                        string name = args.Length > 2 ? args[2] : Path.GetFileNameWithoutExtension(args[1]);
                        Profile p = ProfileIni.FromImages(images, pad.ActiveKeymap(), name, warnings);
                        ProfileIni.Write(p, args[1]);
                        foreach (string w in warnings) Console.WriteLine("warning: " + w);
                        Console.WriteLine("saved the keypad's three keymaps as profile '{0}' (start keymap {1}): {2}", p.Name, KeypadLayout.KeymapNames[p.StartKeymap], Path.GetFullPath(args[1]));
                        return 0;
                    }

                    case "apply":
                        return Apply(pad, args);

                    case "macro":
                        return SetMacro(pad, args);

                    case "roundtrip":
                    case "set":
                    case "write":
                    {
                        int km;
                        byte[] source;
                        byte[] before;
                        if (cmd == "roundtrip")
                        {
                            if (args.Length < 2) throw new ArgumentException("usage: roundtrip 0|1|2");
                            km = KeypadLayout.ParseKeymap(args[1]);
                            before = pad.ReadKeymap(km);
                            source = before;
                        }
                        else if (cmd == "set")
                        {
                            if (args.Length < 4 || !HasYes(args)) throw new ArgumentException("usage: set KM INPUT CODE --yes");
                            km = KeypadLayout.ParseKeymap(args[1]);
                            int input = int.Parse(args[2], CultureInfo.InvariantCulture);
                            int code = Convert.ToInt32(args[3], 16);
                            KeypadLayout.CheckInput(input);
                            if (code < 0 || code > 0xFF) throw new ArgumentException("CODE must be 00-FF");
                            before = pad.ReadKeymap(km);
                            source = (byte[])before.Clone();
                            int slot = KeypadLayout.Slot(input);
                            Console.WriteLine("setting input {0} ({1}, slot {2}): {3:X2} ({4}) -> {5:X2} ({6})", input, KeypadLayout.InputNames[input - 1], slot, source[KeymapImage.CodeOffset + slot], FirmwareCodes.Name(source[KeymapImage.CodeOffset + slot]), code, FirmwareCodes.Name(code));
                            source[KeymapImage.CodeOffset + slot] = (byte)code;
                        }
                        else
                        {
                            if (args.Length < 3 || !HasYes(args)) throw new ArgumentException("usage: write FILE KM --yes");
                            source = File.ReadAllBytes(args[1]);
                            if (source.Length != KeymapImage.Size) throw new ArgumentException(args[1] + " is " + source.Length + " bytes, expected " + KeymapImage.Size);
                            km = KeypadLayout.ParseKeymap(args[2]);
                            before = pad.ReadKeymap(km);
                        }
                        return Write(pad, km, before, KeymapImage.WithUploadHeader(source, km)) ? 0 : 4;
                    }
                }
            }
            Console.WriteLine(Usage);
            return 1;
        }

        private static string Backup(int km, byte[] before)
        {
            Directory.CreateDirectory(DataDir);
            string backup = Path.Combine(DataDir, string.Format(CultureInfo.InvariantCulture, "keymap{0}-before-{1:yyyyMMdd-HHmmss}.bin", km, DateTime.Now));
            File.WriteAllBytes(backup, before);
            Console.WriteLine("backup of keymap {0} ({1}) before writing: {2}", km, KeypadLayout.KeymapNames[km], Path.GetFullPath(backup));
            return backup;
        }

        private static bool Write(KeypadClient pad, int km, byte[] before, byte[] image)
        {
            string backup = Backup(km, before);
            string error = pad.WriteAndVerify(km, image, Console.Out);
            if (error != null)
            {
                Console.WriteLine("RESULT: keymap {0}: {1}; restore with: n52te-config write \"{2}\" {0} --yes", km, error, backup);
                return false;
            }
            Console.WriteLine("RESULT: keymap {0} ({1}) written and verified (read-back identical)", km, KeypadLayout.KeymapNames[km]);
            return true;
        }

        private static int Apply(KeypadClient pad, string[] args)
        {
            bool dryRun = Array.IndexOf(args, "--dry-run") >= 0;
            if (args.Length < 2 || !(HasYes(args) || dryRun)) throw new ArgumentException("usage: apply FILE.ini --yes|--dry-run");
            var warnings = new List<string>();
            Profile p = ProfileIni.Read(args[1], warnings);
            warnings.AddRange(p.Check());
            foreach (string w in warnings) Console.WriteLine("warning: " + w);
            Console.WriteLine("profile '{0}', start keymap {1}", p.Name, KeypadLayout.KeymapNames[p.StartKeymap]);
            for (int km = 0; km < KeypadLayout.KeymapCount; km++)
            {
                byte[] image = KeymapImage.Build(p.Keymaps[km], km);
                byte[] before = pad.ReadKeymap(km);
                if (KeymapImage.FirstDifference(before, image) < 0)
                {
                    Console.WriteLine("keymap {0} ({1}) already matches the profile; not written", km, KeypadLayout.KeymapNames[km]);
                    continue;
                }
                if (dryRun)
                {
                    InputRecord[] current = KeymapImage.Parse(before);
                    Console.WriteLine("keymap {0} ({1}) would change:", km, KeypadLayout.KeymapNames[km]);
                    for (int n = 1; n <= KeypadLayout.InputCount; n++)
                    {
                        string a = KeymapImage.DescribeRecord(n, current[n - 1]), b = KeymapImage.DescribeRecord(n, p.Keymaps[km][n - 1]);
                        if (a != b || current[n - 1].Code != p.Keymaps[km][n - 1].Code) Console.WriteLine("  {0,2} {1,-22} {2}  ->  {3}", n, KeypadLayout.InputNames[n - 1], a, b);
                    }
                    continue;
                }
                if (!Write(pad, km, before, image)) return 4;
            }
            if (dryRun)
            {
                Console.WriteLine("dry run: nothing written; the start keymap would be {0}", KeypadLayout.KeymapNames[p.StartKeymap]);
                return 0;
            }
            pad.SelectKeymap(p.StartKeymap);
            int now = pad.ActiveKeymap();
            Console.WriteLine("active keymap: {0} ({1})", now, KeypadLayout.KeymapNames[now]);
            return now == p.StartKeymap ? 0 : 3;
        }

        private static int SetMacro(KeypadClient pad, string[] args)
        {
            if (args.Length < 4 || !HasYes(args))
                throw new ArgumentException("usage: macro KM INPUT \"STEPS\" [--mode once|held|untilnext] [--times N] [--interval MS] --yes");
            int km = KeypadLayout.ParseKeymap(args[1]);
            int input = int.Parse(args[2], CultureInfo.InvariantCulture);
            KeypadLayout.CheckInput(input);
            byte[] seq = Macro.Parse(args[3]);

            string modeText = Option(args, "--mode") ?? "once";
            byte mode;
            switch (modeText.ToLowerInvariant())
            {
                case "once": case "times": mode = 0; break;
                case "held": mode = 1; break;
                case "untilnext": mode = 2; break;
                default: throw new ArgumentException("--mode must be once, held or untilnext");
            }
            int times = int.Parse(Option(args, "--times") ?? "1", CultureInfo.InvariantCulture);
            int interval = int.Parse(Option(args, "--interval") ?? "50", CultureInfo.InvariantCulture);
            if (times < 1 || times > 10) throw new ArgumentException("--times must be 1-10");
            if (interval < 50 || interval > 250 || interval % 50 != 0) throw new ArgumentException("--interval must be 50, 100, 150, 200 or 250 ms");

            byte[] before = pad.ReadKeymap(km);
            InputRecord[] records = KeymapImage.Parse(before);
            var r = records[input - 1];
            r.Code = KeypadLayout.MacroMarker[input - 1];
            r.Sequence = seq;
            r.FirmwareSequence = null;
            r.Options = new[] { mode, (byte)(times - 1), (byte)(interval / 50 - 1) };
            Console.WriteLine("{0} keymap, input {1} ({2}): macro, {3}: {4}", KeypadLayout.KeymapNames[km], input, KeypadLayout.InputNames[input - 1], InputRecord.DescribeOptions(r.Options), Macro.Describe(seq));
            return Write(pad, km, before, KeymapImage.Build(records, km)) ? 0 : 4;
        }

        private static int Show(string path)
        {
            if (path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
            {
                var warnings = new List<string>();
                Profile p = ProfileIni.Read(path, warnings);
                warnings.AddRange(p.Check());
                Console.WriteLine("profile '{0}', start keymap {1} ({2})", p.Name, p.StartKeymap, KeypadLayout.KeymapNames[p.StartKeymap]);
                for (int km = 0; km < KeypadLayout.KeymapCount; km++)
                    KeymapImage.Describe(string.Format("Keymap {0} ({1})", km, KeypadLayout.KeymapNames[km]), p.Keymaps[km], Console.Out);
                foreach (string w in warnings) Console.WriteLine("warning: " + w);
                return 0;
            }
            byte[] image = File.ReadAllBytes(path);
            int k = image.Length == KeymapImage.Size && image[1] == 0 ? image[4] - 1 : image.Length == KeymapImage.Size ? image[4] : 0;
            if (k < 0 || k > 2) k = 0;
            KeymapImage.Describe(k, image, Console.Out);
            return 0;
        }
    }
}
