// Belkin n52te v2.1.2 profile files (User_Profiles\*.ini), read and written so that profiles
// move between this tool and Belkin's editor (docs/reverse-engineering/04 and 07):
//
//   [Setting]
//   Name=Default
//   Exe=
//   Switch Flag=0
//   Key map=0                 start keymap (0 Blue, 1 Red, 2 Green)
//   [macro]
//   Value=<hex>               3 keymaps x 27 inputs x 0x104-byte records, then one checksum byte
//
// The checksum byte is the 8-bit sum of the record bytes; Windows' WritePrivateProfileStruct
// appends it, and Belkin's editor rejects a value whose checksum doesn't match.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace N52te
{
    internal static class ProfileIni
    {
        public const int RecordsSize = KeypadLayout.KeymapCount * KeypadLayout.InputCount * InputRecord.Size; // 0x5244
        public const int BlobSize = RecordsSize + 1;

        public static Profile Read(string path, List<string> warnings)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string section = "";
            foreach (string raw in File.ReadAllLines(path, Encoding.Default))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']') { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                int eq = line.IndexOf('=');
                if (eq > 0) values[section + "/" + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            string hex;
            if (!values.TryGetValue("macro/Value", out hex)) throw new InvalidDataException(path + " is not an n52te profile: no [macro] Value= entry");
            byte[] blob = FromHex(hex);
            if (blob == null) throw new InvalidDataException(path + ": the Value= entry is not valid hex");
            if (blob.Length != BlobSize) throw new InvalidDataException(string.Format("{0}: Value= holds {1} bytes, expected {2}", path, blob.Length, BlobSize));
            if (blob[RecordsSize] != Checksum(blob))
                warnings.Add(string.Format("{0}: checksum byte is {1:X2}, expected {2:X2}; Belkin's editor would reject this file", path, blob[RecordsSize], Checksum(blob)));

            var p = FromBlob(blob);
            string s;
            if (values.TryGetValue("Setting/Name", out s)) p.Name = s;
            if (values.TryGetValue("Setting/Exe", out s)) p.Exe = s;
            if (values.TryGetValue("Setting/Switch Flag", out s)) p.SwitchFlag = s;
            int km;
            if (values.TryGetValue("Setting/Key map", out s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out km) && km >= 0 && km <= 2)
                p.StartKeymap = km;
            return p;
        }

        public static Profile FromBlob(byte[] blob)
        {
            var p = new Profile();
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
                for (int n = 0; n < KeypadLayout.InputCount; n++)
                    p.Keymaps[k][n] = InputRecord.FromBlob(blob, (k * KeypadLayout.InputCount + n) * InputRecord.Size);
            return p;
        }

        public static byte[] ToBlob(Profile p)
        {
            var blob = new byte[BlobSize];
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
                for (int n = 0; n < KeypadLayout.InputCount; n++)
                    p.Keymaps[k][n].CopyTo(blob, (k * KeypadLayout.InputCount + n) * InputRecord.Size);
            blob[RecordsSize] = Checksum(blob);
            return blob;
        }

        public static byte Checksum(byte[] blob)
        {
            int sum = 0;
            for (int i = 0; i < RecordsSize; i++) sum += blob[i];
            return (byte)sum;
        }

        public static string ToText(Profile p)
        {
            var sb = new StringBuilder();
            sb.Append("[Setting]\r\n");
            sb.Append("Name=").Append(OneLine(p.Name)).Append("\r\n");
            sb.Append("Exe=").Append(OneLine(p.Exe)).Append("\r\n");
            sb.Append("Switch Flag=").Append(OneLine(p.SwitchFlag)).Append("\r\n");
            sb.Append("Key map=").Append(p.StartKeymap.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("[macro]\r\n");
            sb.Append("Value=").Append(ToHex(ToBlob(p))).Append("\r\n");
            return sb.ToString();
        }

        public static void Write(Profile p, string path)
        {
            File.WriteAllText(path, ToText(p), Encoding.Default);
        }

        // A profile made from three keymap images (e.g. read from the keypad).
        public static Profile FromImages(byte[][] images, int startKeymap, string name, List<string> warnings)
        {
            var p = new Profile { Name = name, StartKeymap = startKeymap };
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            {
                InputRecord[] records = KeymapImage.Parse(images[k]);
                foreach (string w in KeymapImage.Warnings(records)) warnings.Add(KeypadLayout.KeymapNames[k] + " keymap, " + w + "; the .ini can't hold them, so this input's macro is lost in the file");
                p.Keymaps[k] = records;
            }
            return p;
        }

        private static string OneLine(string s) { return (s ?? "").Replace("\r", " ").Replace("\n", " "); }

        private static string ToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("X2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static byte[] FromHex(string s)
        {
            if (s.Length % 2 != 0) return null;
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
            {
                int v;
                if (!int.TryParse(s.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return null;
                b[i] = (byte)v;
            }
            return b;
        }
    }
}
