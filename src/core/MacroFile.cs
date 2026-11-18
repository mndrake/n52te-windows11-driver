// Saved macros in the format of Belkin's editor (User Macros\<name>.ini, docs/reverse-engineering/07):
//
//   [Setting]
//   Behaviour=0          0 run times, 1 repeat while pressed, 2 repeat until next key (p1)
//   RepeatTimes=0        run count - 1 (p2)
//   RepeatInterval=0     interval / 50 ms - 1 (p3)
//   IgnoreDelay=0        recorder setting only; never reaches the keypad
//   Macro=<hex>          128 (code, flags) steps in the profile form, then a checksum byte
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace N52te
{
    internal sealed class SavedMacro
    {
        public string Name = "";
        public byte[] Sequence = new byte[InputRecord.SequenceSize];
        public byte[] Options = { 0, 0, 0 };
        public bool IgnoreDelay;

        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "n52te", "Macros");

        public static string PathFor(string name) { return Path.Combine(Folder, name + ".ini"); }

        public static List<string> List()
        {
            var names = new List<string>();
            if (Directory.Exists(Folder))
                foreach (string f in Directory.GetFiles(Folder, "*.ini")) names.Add(Path.GetFileNameWithoutExtension(f));
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static SavedMacro Read(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(path, Encoding.Default))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq > 0 && line[0] != '[' && line[0] != ';') values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            var m = new SavedMacro { Name = Path.GetFileNameWithoutExtension(path) };
            string hex;
            if (!values.TryGetValue("Macro", out hex) || hex.Length < 2 * InputRecord.SequenceSize)
                throw new InvalidDataException(path + " is not a macro file: no Macro= entry");
            for (int i = 0; i < InputRecord.SequenceSize; i++)
                m.Sequence[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            m.Options[0] = (byte)Int(values, "Behaviour", 0);
            m.Options[1] = (byte)Int(values, "RepeatTimes", 1);
            m.Options[2] = (byte)Int(values, "RepeatInterval", 1);
            m.IgnoreDelay = Int(values, "IgnoreDelay", 0) != 0;
            return m;
        }

        // Missing keys get the defaults Belkin's editor uses when it reads a macro file.
        private static int Int(Dictionary<string, string> values, string key, int fallback)
        {
            string s;
            int v;
            return values.TryGetValue(key, out s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            sb.Append("[Setting]\r\n");
            sb.Append("Behaviour=").Append(Options[0]).Append("\r\n");
            sb.Append("RepeatTimes=").Append(Options[1]).Append("\r\n");
            sb.Append("RepeatInterval=").Append(Options[2]).Append("\r\n");
            sb.Append("IgnoreDelay=").Append(IgnoreDelay ? 1 : 0).Append("\r\n");
            int sum = 0;
            sb.Append("Macro=");
            foreach (byte b in Sequence)
            {
                sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                sum += b;
            }
            sb.Append(((byte)sum).ToString("X2", CultureInfo.InvariantCulture)).Append("\r\n"); // WritePrivateProfileStruct's checksum byte
            File.WriteAllText(path, sb.ToString(), Encoding.Default);
        }

        // Belkin's editor warns before saving a macro that presses more keys than it releases.
        public static bool PressesMoreThanReleases(byte[] seq)
        {
            int balance = 0;
            for (int i = 0; i + 1 < seq.Length; i += 2)
            {
                if (seq[i] == 0) break;
                if (seq[i] == FirmwareCodes.DelayStep) continue;
                balance += seq[i + 1] != 0 ? 1 : -1;
            }
            return balance > 0;
        }
    }
}
