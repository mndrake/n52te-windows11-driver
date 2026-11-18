// Whole-profile operations shared by n52te-config and the editor: read the keypad into a profile,
// compare profiles, and write a profile (backup first, only keymaps that differ, verify, then
// select the start keymap).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace N52te
{
    internal static class KeypadSession
    {
        public static readonly string BackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "n52te keymaps");

        public static Profile Read(KeypadClient pad, string name, List<string> warnings)
        {
            var images = new byte[KeypadLayout.KeymapCount][];
            for (int km = 0; km < KeypadLayout.KeymapCount; km++) images[km] = pad.ReadKeymap(km);
            return ProfileIni.FromImages(images, pad.ActiveKeymap(), name, warnings);
        }

        // One line per input whose assignment differs, e.g. "Red keymap, Key 05: R -> macro ...".
        public static List<string> Differences(Profile before, Profile after)
        {
            var d = new List<string>();
            for (int km = 0; km < KeypadLayout.KeymapCount; km++)
            {
                byte[] a = KeymapImage.Build(before.Keymaps[km], km), b = KeymapImage.Build(after.Keymaps[km], km);
                if (KeymapImage.FirstDifference(a, b) < 0) continue;
                InputRecord[] ra = KeymapImage.Parse(a), rb = KeymapImage.Parse(b);
                for (int n = 1; n <= KeypadLayout.InputCount; n++)
                {
                    int slot = KeypadLayout.Slot(n);
                    bool same = a[KeymapImage.CodeOffset + slot] == b[KeymapImage.CodeOffset + slot]
                        && a[KeymapImage.P1Offset + slot] == b[KeymapImage.P1Offset + slot]
                        && a[KeymapImage.P2Offset + slot] == b[KeymapImage.P2Offset + slot]
                        && a[KeymapImage.P3Offset + slot] == b[KeymapImage.P3Offset + slot]
                        && KeymapImage.Same(a, KeymapImage.MacroOffset + slot * 0x100, b, KeymapImage.MacroOffset + slot * 0x100, 0x100);
                    if (!same)
                        d.Add(string.Format("{0} keymap, {1}: {2} -> {3}", KeypadLayout.KeymapNames[km], KeypadLayout.InputNames[n - 1],
                            KeymapImage.DescribeRecord(n, ra[n - 1]), KeymapImage.DescribeRecord(n, rb[n - 1])));
                }
            }
            if (before.StartKeymap != after.StartKeymap)
                d.Add("start keymap: " + KeypadLayout.KeymapNames[before.StartKeymap] + " -> " + KeypadLayout.KeymapNames[after.StartKeymap]);
            return d;
        }

        // Returns null on success, else what went wrong (with how to restore). log gets progress lines.
        public static string Apply(KeypadClient pad, Profile p, Action<string> log)
        {
            Directory.CreateDirectory(BackupDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            for (int km = 0; km < KeypadLayout.KeymapCount; km++)
            {
                string name = KeypadLayout.KeymapNames[km];
                byte[] image = KeymapImage.Build(p.Keymaps[km], km);
                byte[] before = pad.ReadKeymap(km);
                if (KeymapImage.FirstDifference(before, image) < 0)
                {
                    log(string.Format("keymap {0} ({1}) already matches; not written", km, name));
                    continue;
                }
                string backup = Path.Combine(BackupDir, string.Format("keymap{0}-before-{1}.bin", km, stamp));
                File.WriteAllBytes(backup, before);
                log(string.Format("backup of keymap {0} ({1}): {2}", km, name, backup));
                log(string.Format("writing keymap {0} ({1})...", km, name));
                string error = pad.WriteAndVerify(km, image, null);
                if (error != null)
                    return string.Format("keymap {0} ({1}): {2}. Restore it with: n52te-config write \"{3}\" {0} --yes", km, name, error, backup);
                log(string.Format("keymap {0} ({1}) written and verified", km, name));
            }
            pad.SelectKeymap(p.StartKeymap);
            int now = pad.ActiveKeymap();
            if (now != p.StartKeymap) return "the keypad didn't switch to the start keymap (it reports " + now + ")";
            log("active keymap: " + KeypadLayout.KeymapNames[now]);
            return null;
        }
    }
}
