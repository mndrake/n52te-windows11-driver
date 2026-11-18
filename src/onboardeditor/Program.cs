// n52te Editor: edits the Belkin n52te's three onboard keymaps and writes them into the keypad,
// which then runs them itself (keys, mouse, wheel, macros, keymap switching with the LED).
// Built with the in-box .NET Framework compiler (C# 5) from src\core\*.cs and src\onboardeditor\*.cs.
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace N52te
{
    internal static class EditorProgram
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Development aid: render the window (factory profile, no keypad) to a PNG and exit.
            //   "n52te Editor.exe" --screenshot out.png [--skin SKIN_DIR] [--hover INPUT] [--macro]
            int i = Array.IndexOf(args, "--screenshot");
            if (i >= 0 && i + 1 < args.Length)
            {
                int s = Array.IndexOf(args, "--skin"), h = Array.IndexOf(args, "--hover");
                using (var f = new MainForm(false))
                {
                    f.SaveScreenshot(args[i + 1], s >= 0 && s + 1 < args.Length ? args[s + 1] : null,
                        h >= 0 && h + 1 < args.Length ? int.Parse(args[h + 1], CultureInfo.InvariantCulture) : 0, Array.IndexOf(args, "--macro") >= 0);
                }
                return 0;
            }

            // Development aid: Belkin-style prompts rendered to PNGs in a folder.
            if (args.Length == 2 && args[0] == "--screenshot-dialogs")
            {
                SkinDialog.SaveSample(System.IO.Path.Combine(args[1], "dialog-save.png"), 1.5f, "Save Changes", "Do you want to save your changes to 'Racing'?", null, "Yes", "No", "Cancel");
                SkinDialog.SaveSample(System.IO.Path.Combine(args[1], "dialog-import.png"), 1.5f, "Profile Save Warning!", "A profile with this file name already exists. A new name must be provided in order to continue importing this profile.", null, "Save as", "Cancel");
                SkinDialog.SaveSample(System.IO.Path.Combine(args[1], "dialog-apply.png"), 1.5f, "Apply Changes", "3 changes to write to the keypad. It doesn't type for the few seconds this takes; each keymap that changes is backed up first.",
                    "Red keymap, Key 01: Tab -> macro, run 1 time: +A -A (1000 ms) +B -B\nRed keymap, Key 02: Q -> Mouse Left\nstart keymap: Blue -> Red", "Apply", "Cancel");
                return 0;
            }

            Application.Run(new MainForm(true));
            return 0;
        }
    }
}
