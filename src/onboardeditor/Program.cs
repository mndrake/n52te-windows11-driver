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
            //   "n52te Editor.exe" --screenshot out.png [--skin SKIN_DIR] [--hover INPUT]
            int i = Array.IndexOf(args, "--screenshot");
            if (i >= 0 && i + 1 < args.Length)
            {
                int s = Array.IndexOf(args, "--skin"), h = Array.IndexOf(args, "--hover");
                using (var f = new MainForm(false))
                {
                    f.SaveScreenshot(args[i + 1], s >= 0 && s + 1 < args.Length ? args[s + 1] : null,
                        h >= 0 && h + 1 < args.Length ? int.Parse(args[h + 1], CultureInfo.InvariantCulture) : 0);
                }
                return 0;
            }

            Application.Run(new MainForm(true));
            return 0;
        }
    }
}
