// Install-n52te.exe: installs the n52te Editor and n52te-config into C:\Program Files\n52te, and
// cleans up after earlier versions: the AutoHotkey editor, the Interception remapper and its
// autostart, old Belkin driver leftovers and, if the user agrees, the Interception driver itself.
// Built by build.ps1 with the in-box .NET Framework compiler (C# 5); runs as administrator
// (app.manifest).
//
// Safety: the keyboard and mouse class filter lists must always keep kbdclass / mouclass. A list
// that names a filter whose driver is gone stops every keyboard (or mouse) from starting, so the
// Interception entries are removed from the lists *before* its services and files, and a restore
// point is made first. See docs/onboard-editor-plan.md, "Safety rules".
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("n52te Editor Setup")]
[assembly: AssemblyVersion("2.0.0.0")]

internal static class Installer
{
    private const string InstallDir = @"C:\Program Files\n52te";
    private const string EditorExe = "n52te Editor.exe";
    private const string ConfigExe = "n52te-config.exe";
    private const string UsbDkHelper = @"C:\Program Files\UsbDk Runtime Library\UsbDkHelper.dll";
    private const string UsbDkPage = "https://github.com/daynix/UsbDk/releases/tag/v1.0.22";
    private const string KeyboardClass = @"SYSTEM\CurrentControlSet\Control\Class\{4D36E96B-E325-11CE-BFC1-08002BE10318}";
    private const string MouseClass = @"SYSTEM\CurrentControlSet\Control\Class\{4D36E96F-E325-11CE-BFC1-08002BE10318}";

    private static readonly string[,] Payload = { { "RES_EDITOR", EditorExe }, { "RES_CONFIG", ConfigExe } };

    // Files of earlier versions (remapper, AutoHotkey editor, branding, compiled editors).
    private static readonly string[] OldFiles =
    {
        "n52te_interception.exe", "interception.dll", "install-interception.exe", "AutoHotkey64.exe", "AutoHotkey license.txt",
        "n52te Editor.ahk", "Editor n52te.ahk", "Editor n52te.exe", "infomalena.ico", "favicon.png", "logo_footer.png", "n52.ico", "n52te_config.ini",
    };

    private static Form window;
    private static TextBox log;
    private static Button install, restart, close;
    private static bool needRestart;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Color back = Color.FromArgb(16, 17, 20), field = Color.FromArgb(24, 26, 30), text = Color.FromArgb(225, 228, 232);
        window = new Form
        {
            Text = "n52te Editor Setup", ClientSize = new Size(620, 440), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, BackColor = back, ForeColor = text,
            Font = new Font("Segoe UI", 9f), AutoScaleMode = AutoScaleMode.Font,
        };
        var title = new Label { Text = "n52te Editor", Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.FromArgb(120, 180, 240), Location = new Point(18, 12), AutoSize = true };
        var intro = new Label
        {
            Location = new Point(20, 50), Size = new Size(580, 52), ForeColor = Color.FromArgb(170, 174, 180),
            Text = "Installs the n52te Editor, which writes your keymaps into the Belkin n52te so the keypad runs them itself, and n52te-config, its command-line tool. " +
                   "Removes what earlier versions installed (the remapper, its editor and, if you agree, the Interception driver).",
        };
        log = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(20, 108), Size = new Size(580, 270),
            BackColor = field, ForeColor = text, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle,
        };
        install = MakeButton("Install", new Point(20, 392), 140);
        install.BackColor = Color.FromArgb(40, 90, 150);
        restart = MakeButton("Restart now", new Point(340, 392), 130);
        restart.Enabled = false;
        close = MakeButton("Close", new Point(480, 392), 120);
        install.Click += (s, e) =>
        {
            install.Enabled = false;
            close.Enabled = false;
            new Thread(Run) { IsBackground = true }.Start();
        };
        restart.Click += (s, e) => { try { Process.Start("shutdown", "/r /t 5"); } catch (Exception) { } };
        close.Click += (s, e) => window.Close();
        window.Controls.AddRange(new Control[] { title, intro, log, install, restart, close });
        Log("Ready. Click Install. (Your n52te can stay plugged in.)");
        Application.Run(window);
    }

    private static Button MakeButton(string text, Point at, int width)
    {
        return new Button { Text = text, Location = at, Size = new Size(width, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 42, 48), ForeColor = Color.White };
    }

    private static void Ui(Action a)
    {
        if (window.InvokeRequired) window.Invoke(a); else a();
    }

    private static void Log(string s) { Ui(() => log.AppendText(s + "\r\n")); }

    private static bool Ask(string question)
    {
        bool yes = false;
        Ui(() => yes = MessageBox.Show(window, question, "n52te Editor Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        return yes;
    }

    private static void Run()
    {
        try
        {
            Log("== 1/6 Closing n52te programs ==");
            StopPrograms();

            Log("== 2/6 Removing the earlier version (remapper and its editor) ==");
            RemoveOldVersion();

            Log("== 3/6 Removing old Belkin driver leftovers ==");
            CleanBelkin();
            CleanBelkinFilter();

            Log("== 4/6 Interception driver ==");
            RemoveInterception();

            Log("== 5/6 Installing ==");
            Directory.CreateDirectory(InstallDir);
            for (int i = 0; i < Payload.GetLength(0); i++)
            {
                Extract(Payload[i, 0], Path.Combine(InstallDir, Payload[i, 1]));
                Log("   " + Path.Combine(InstallDir, Payload[i, 1]));
            }
            foreach (string old in OldFiles) DeleteFile(Path.Combine(InstallDir, old));
            MakeShortcuts();

            Log("== 6/6 UsbDk ==");
            CheckUsbDk();

            Log("");
            if (needRestart)
            {
                Ui(() => { restart.Enabled = true; restart.BackColor = Color.FromArgb(170, 110, 30); });
                Log("DONE. Restart Windows to finish removing the old driver, then open 'n52te Editor'.");
            }
            else Log("DONE. Open 'n52te Editor' from the Start menu or the Desktop.");
        }
        catch (Exception ex)
        {
            Log("[ERROR] " + ex.Message);
            Log("Nothing more was changed after this error. Please open an issue with the text shown here.");
        }
        finally
        {
            Ui(() => close.Enabled = true);
        }
    }

    // ---- earlier versions ----

    private static void StopPrograms()
    {
        foreach (string name in new[] { "n52te Editor", "Editor n52te", "n52te_interception" }) Stop(name);
        StopPath(Path.Combine(InstallDir, "AutoHotkey64.exe")); // only the copy in our folder, never the user's own AutoHotkey
    }

    private static void Stop(string processName)
    {
        foreach (Process p in Process.GetProcessesByName(processName))
        {
            try { p.Kill(); p.WaitForExit(3000); Log("   closed " + processName); } catch (Exception) { }
        }
    }

    private static void StopPath(string fullPath)
    {
        foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullPath)))
        {
            try
            {
                if (string.Equals(p.MainModule.FileName, fullPath, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(3000); Log("   closed " + fullPath); }
            }
            catch (Exception) { }
        }
    }

    private static void RemoveOldVersion()
    {
        int found = 0;
        // The old editor's "Start the remapper with Windows" (per user).
        using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
        {
            string v = run == null ? null : run.GetValue("n52te") as string;
            if (v != null && v.IndexOf("n52te_interception", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                run.DeleteValue("n52te", false);
                Log("   removed the remapper's autostart");
                found++;
            }
        }
        string menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "n52te");
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        foreach (string lnk in new[]
        {
            Path.Combine(menu, "n52te Remapper.lnk"), Path.Combine(menu, "Editor n52te.lnk"), Path.Combine(menu, "Remapeador n52te.lnk"),
            Path.Combine(menu, "n52te Editor.lnk"), Path.Combine(desktop, "n52te Editor.lnk"), Path.Combine(desktop, "Editor n52te.lnk"),
        })
            if (DeleteFile(lnk)) found++;
        foreach (string old in OldFiles) if (DeleteFile(Path.Combine(InstallDir, old))) found++;
        string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "n52te", "n52te_config.ini");
        if (File.Exists(settings)) Log("   kept the old remapper's button settings in " + settings + " (the new editor doesn't use them)");
        if (found == 0) Log("   (no earlier version found)");
    }

    private static bool DeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            Log("   removed " + path);
            return true;
        }
        catch (Exception ex)
        {
            Log("   (couldn't remove " + path + ": " + ex.Message + ")");
            return false;
        }
    }

    // ---- Interception ----

    private static string[] Filters(string classKey)
    {
        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(classKey))
            return k == null ? new string[0] : (k.GetValue("UpperFilters") as string[] ?? new string[0]);
    }

    private static bool ServiceExists(string name)
    {
        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name)) return k != null;
    }

    private static bool Names(string[] list, string name)
    {
        return Array.FindIndex(list, f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase)) >= 0;
    }

    private static void RemoveInterception()
    {
        string drivers = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers");
        string[] kbd = Filters(KeyboardClass), mou = Filters(MouseClass);
        bool present = Names(kbd, "keyboard") || Names(mou, "mouse") || ServiceExists("keyboard") || ServiceExists("mouse")
            || File.Exists(Path.Combine(drivers, "keyboard.sys")) || File.Exists(Path.Combine(drivers, "mouse.sys"));
        if (!present)
        {
            Log("   not installed");
            return;
        }
        Log("   found (keyboard filter list: " + string.Join(", ", kbd) + "; mouse: " + string.Join(", ", mou) + ")");
        if (!Ask("The Interception driver from the earlier version is still installed. The new editor doesn't need it, and Windows can't turn on Memory Integrity while it is there.\n\n" +
                 "Remove it now? A restore point is created first, and Windows needs a restart afterwards."))
        {
            Log("   kept (you can remove it later by running this installer again)");
            return;
        }
        if (!CreateRestorePoint("Before removing Interception (n52te)") &&
            !Ask("Windows didn't create a restore point (it allows one per 24 hours, or System Protection is off). Remove Interception anyway?"))
        {
            Log("   kept");
            return;
        }

        // 1. Take Interception out of the filter lists first, keeping Windows' own class drivers.
        SetFilters(KeyboardClass, kbd, "keyboard", "kbdclass");
        SetFilters(MouseClass, mou, "mouse", "mouclass");
        // 2. Then its services, 3. then its files (kept in a backup folder).
        foreach (string svc in new[] { "keyboard", "mouse" })
            if (ServiceExists(svc)) Log("   sc delete " + svc + " -> exit code " + RunProcess("sc", "delete " + svc));
        string backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "n52te", "removed-drivers");
        foreach (string sys in new[] { "keyboard.sys", "mouse.sys" })
        {
            string path = Path.Combine(drivers, sys);
            if (!File.Exists(path)) continue;
            try
            {
                Directory.CreateDirectory(backup);
                File.Copy(path, Path.Combine(backup, sys), true);
                File.Delete(path);
                Log("   moved " + path + " to " + backup);
            }
            catch (Exception)
            {
                bool ok = MoveFileEx(path, null, 4); // MOVEFILE_DELAY_UNTIL_REBOOT
                Log(ok ? "   " + path + " is in use; it will be deleted when Windows restarts" : "   (couldn't remove " + path + ")");
            }
        }
        needRestart = true;
        Log("   removed; restart Windows to finish");
    }

    private static void SetFilters(string classKey, string[] current, string remove, string required)
    {
        var list = new List<string>();
        foreach (string f in current) if (!string.Equals(f, remove, StringComparison.OrdinalIgnoreCase) && f.Length > 0) list.Add(f);
        if (!Names(list.ToArray(), required)) list.Insert(0, required);
        if (list.Count == current.Length && Names(current, required)) return; // nothing to change
        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(classKey, true))
            k.SetValue("UpperFilters", list.ToArray(), RegistryValueKind.MultiString);
        Log("   " + (classKey == KeyboardClass ? "keyboard" : "mouse") + " filter list: " + string.Join(", ", current) + " -> " + string.Join(", ", list.ToArray()));
    }

    private static bool CreateRestorePoint(string description)
    {
        try
        {
            var before = CountRestorePoints();
            using (var sr = new ManagementClass(@"\\.\root\default", "SystemRestore", null))
            {
                ManagementBaseObject args = sr.GetMethodParameters("CreateRestorePoint");
                args["Description"] = description;
                args["RestorePointType"] = 12; // MODIFY_SETTINGS
                args["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
                ManagementBaseObject result = sr.InvokeMethod("CreateRestorePoint", args, null);
                uint code = (uint)result["ReturnValue"];
                bool made = code == 0 && CountRestorePoints() > before;
                Log(made ? "   restore point created: " + description : "   no restore point was created (code " + code + ")");
                return made;
            }
        }
        catch (Exception ex)
        {
            Log("   couldn't create a restore point: " + ex.Message);
            return false;
        }
    }

    private static int CountRestorePoints()
    {
        try
        {
            using (var s = new ManagementObjectSearcher(@"root\default", "SELECT SequenceNumber FROM SystemRestore"))
                return s.Get().Count;
        }
        catch (Exception) { return -1; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existing, string newName, int flags);

    // ---- old Belkin software ----

    // Belkin's 2008 driver packages (JmtFltr USB filter, ICP, virtual HID) can't load on Windows 11
    // with Memory Integrity on, and a stale filter entry can keep the keypad's mouse side from starting.
    private static void CleanBelkin()
    {
        string current = null;
        int removed = 0;
        foreach (string raw in RunCapture("pnputil", "/enum-drivers").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.IndexOf("oem", StringComparison.OrdinalIgnoreCase) >= 0 && line.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
            {
                int colon = line.IndexOf(':');
                current = (colon >= 0 ? line.Substring(colon + 1) : line).Trim();
            }
            bool belkin = line.IndexOf("jmtflter", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("jmtfltr", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("usbicp", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("vhidmini", StringComparison.OrdinalIgnoreCase) >= 0;
            if (belkin && current != null && current.StartsWith("oem", StringComparison.OrdinalIgnoreCase))
            {
                Log("   removing driver package " + current + " -> exit code " + RunProcess("pnputil", "/delete-driver " + current + " /uninstall /force"));
                removed++;
                current = null;
            }
        }
        if (removed == 0) Log("   (no Belkin driver packages found)");
    }

    private static void CleanBelkinFilter()
    {
        int removed = 0;
        foreach (string iface in new[] { "VID_050D&PID_0200&MI_00", "VID_050D&PID_0200&MI_01" })
        {
            using (RegistryKey parent = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB\" + iface))
            {
                if (parent == null) continue;
                foreach (string instance in parent.GetSubKeyNames())
                {
                    using (RegistryKey node = parent.OpenSubKey(instance))
                    {
                        string[] filters = node == null ? null : node.GetValue("LowerFilters") as string[];
                        if (filters == null || !Names(filters, "JmtFltr")) continue;
                    }
                    // Removing the device entry drops the stale filter; Windows sets the device up
                    // again with its own driver on the next scan.
                    string id = @"USB\" + iface + @"\" + instance;
                    Log("   removed device entry with the Belkin filter: " + id + " (exit code " + RunProcess("pnputil", "/remove-device \"" + id + "\"") + ")");
                    removed++;
                }
            }
        }
        if (removed > 0) RunProcess("pnputil", "/scan-devices");
        if (ServiceExists("jmtfltr"))
        {
            Log("   removed the Belkin filter service jmtfltr (exit code " + RunProcess("sc", "delete jmtfltr") + ")");
            string sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\JmtFltr.sys");
            if (File.Exists(sys))
            {
                try { File.Delete(sys); Log("   deleted " + sys); }
                catch (Exception) { Log(MoveFileEx(sys, null, 4) ? "   " + sys + " will be deleted when Windows restarts" : "   (couldn't delete " + sys + ")"); }
            }
        }
        else if (removed == 0) Log("   (no Belkin filter leftovers found)");
    }

    // ---- files, shortcuts, UsbDk ----

    private static void Extract(string resource, string path)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {
            if (s == null) throw new Exception("resource missing from the installer: " + resource);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (FileStream f = File.Create(path)) s.CopyTo(f);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 3) throw;
                    StopPath(path);
                    Thread.Sleep(600);
                    s.Position = 0;
                }
            }
        }
    }

    private static void MakeShortcuts()
    {
        string menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "n52te");
        Directory.CreateDirectory(menu);
        string editor = Path.Combine(InstallDir, EditorExe);
        Shortcut(Path.Combine(menu, "n52te Editor.lnk"), editor, "", editor, "Edit the n52te's onboard keymaps");
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "n52te Editor.lnk"), editor, "", editor, "Edit the n52te's onboard keymaps");
        string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        Shortcut(Path.Combine(menu, "n52te Keymaps (command prompt).lnk"), cmd,
            "/k \"set \"PATH=" + InstallDir + ";%PATH%\" & n52te-config info & echo. & n52te-config\"", cmd, "n52te-config: read, switch and write the n52te's onboard keymaps");
        Log("   shortcuts: Start menu (n52te Editor, n52te Keymaps) and Desktop (n52te Editor)");
    }

    private static void Shortcut(string lnk, string target, string args, string icon, string description)
    {
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic s = shell.CreateShortcut(lnk);
            s.TargetPath = target;
            s.Arguments = args;
            s.WorkingDirectory = InstallDir;
            s.IconLocation = icon;
            s.Description = description;
            s.Save();
        }
        catch (Exception ex)
        {
            Log("   (couldn't create " + lnk + ": " + ex.Message + ")");
        }
    }

    private static void CheckUsbDk()
    {
        if (File.Exists(UsbDkHelper))
        {
            Log("   UsbDk is installed: the editor can read and write the keypad.");
            return;
        }
        Log("   UsbDk is NOT installed. The editor needs it to reach the keypad:");
        Log("   install UsbDk 1.0.22 x64 (UsbDk_1.0.22_x64.msi) from " + UsbDkPage + ", then restart Windows.");
        if (Ask("The editor needs UsbDk (free, signed by Red Hat) to talk to the keypad. Open its download page now?\n\nInstall UsbDk_1.0.22_x64.msi, then restart Windows."))
        {
            try { Process.Start(UsbDkPage); } catch (Exception) { }
        }
    }

    private static int RunProcess(string exe, string args)
    {
        using (Process p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true }))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    private static string RunCapture(string exe, string args)
    {
        using (Process p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }
    }
}
