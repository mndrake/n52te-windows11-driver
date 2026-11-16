// Recovered by decompiling the installer from Instalar-n52te.exe v1.1.0 (ILSpy 11.2, C# 5).
// Build with build.ps1. User-facing text translated to US English.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
internal static class Installer
{
	private const string INSTALL_DIR = "C:\\Program Files\\n52te";

	private static TextBox log;

	private static Button btnInstall;

	private static Button btnReboot;

	private static Button btnClose;

	private static bool needReboot = false;

	// The editor ships as the standard AutoHotkey v2 interpreter plus its readable script,
	// not as a compiled exe: compiled AutoHotkey programs trip antivirus heuristics
	// (Microsoft Defender flagged one build as Program:Win32/Contebrew.A!ml).
	private const string AHK_EXE = "AutoHotkey64.exe";

	private const string EDITOR_SCRIPT = "n52te Editor.ahk";

	private static readonly string[,] PAYLOAD = new string[9, 2]
	{
		{ "RES_REMAPPER", "n52te_interception.exe" },
		{ "RES_DLL", "interception.dll" },
		{ "RES_AHK", AHK_EXE },
		{ "RES_AHK_LICENSE", "AutoHotkey license.txt" },
		{ "RES_EDITOR", EDITOR_SCRIPT },
		{ "RES_DRIVER", "install-interception.exe" },
		{ "RES_ICO", "infomalena.ico" },
		{ "RES_FAVICON", "favicon.png" },
		{ "RES_FOOTER", "logo_footer.png" }
	};

	[STAThread]
	private static void Main()
	{
		Application.EnableVisualStyles();
		Form form = new Form();
		form.Text = "Install n52te for Windows 11";
		form.Width = 640;
		form.Height = 480;
		form.StartPosition = FormStartPosition.CenterScreen;
		form.FormBorderStyle = FormBorderStyle.FixedDialog;
		form.MaximizeBox = false;
		form.BackColor = Color.FromArgb(30, 30, 30);
		Form form2 = form;
		try
		{
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RES_ICO"))
			{
				if (stream != null)
				{
					form2.Icon = new Icon(stream);
				}
			}
		}
		catch
		{
		}
		PictureBox pictureBox = new PictureBox();
		pictureBox.Left = 530;
		pictureBox.Top = 10;
		pictureBox.Width = 80;
		pictureBox.Height = 80;
		pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
		PictureBox pictureBox2 = pictureBox;
		try
		{
			using (Stream stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("RES_LOGO"))
			{
				if (stream2 != null)
				{
					pictureBox2.Image = Image.FromStream(stream2);
				}
			}
		}
		catch
		{
		}
		Label label = new Label();
		label.Text = "Belkin n52te Keypad Installer";
		label.ForeColor = Color.White;
		label.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
		label.Left = 18;
		label.Top = 14;
		label.Width = 500;
		label.Height = 28;
		Label value = label;
		Label label2 = new Label();
		label2.Text = "Installs the driver, the remapper and the editor, and leaves the n52te ready to use.\nRequires administrator rights (UAC) and may ask you to restart at the end.\nby INFOMALENA Software Development";
		label2.ForeColor = Color.Silver;
		label2.Font = new Font("Segoe UI", 8.5f);
		label2.Left = 20;
		label2.Top = 42;
		label2.Width = 500;
		label2.Height = 44;
		Label value2 = label2;
		TextBox textBox = new TextBox();
		textBox.Multiline = true;
		textBox.ReadOnly = true;
		textBox.ScrollBars = ScrollBars.Vertical;
		textBox.Left = 20;
		textBox.Top = 86;
		textBox.Width = 584;
		textBox.Height = 286;
		textBox.BackColor = Color.FromArgb(20, 20, 20);
		textBox.ForeColor = Color.FromArgb(120, 230, 140);
		textBox.Font = new Font("Consolas", 9f);
		textBox.BorderStyle = BorderStyle.FixedSingle;
		log = textBox;
		Button button = new Button();
		button.Text = "Install";
		button.Left = 20;
		button.Top = 384;
		button.Width = 160;
		button.Height = 40;
		button.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
		button.BackColor = Color.FromArgb(45, 125, 70);
		button.ForeColor = Color.White;
		button.FlatStyle = FlatStyle.Flat;
		btnInstall = button;
		Button button2 = new Button();
		button2.Text = "Restart now";
		button2.Left = 330;
		button2.Top = 386;
		button2.Width = 140;
		button2.Height = 36;
		button2.Font = new Font("Segoe UI", 10f);
		button2.Enabled = false;
		button2.BackColor = Color.FromArgb(60, 60, 60);
		button2.ForeColor = Color.White;
		button2.FlatStyle = FlatStyle.Flat;
		btnReboot = button2;
		Button button3 = new Button();
		button3.Text = "Close";
		button3.Left = 480;
		button3.Top = 386;
		button3.Width = 120;
		button3.Height = 36;
		button3.Font = new Font("Segoe UI", 10f);
		button3.BackColor = Color.FromArgb(60, 60, 60);
		button3.ForeColor = Color.White;
		button3.FlatStyle = FlatStyle.Flat;
		btnClose = button3;
		btnInstall.Click += (object s, EventArgs e) =>
		{
			btnInstall.Enabled = false;
			Thread thread = new Thread(RunInstall);
			thread.IsBackground = true;
			thread.Start();
		};
		if (Environment.CommandLine.IndexOf("/auto", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			form2.Shown += (object s, EventArgs e) =>
			{
				btnInstall.Enabled = false;
				Thread thread = new Thread(RunInstall);
				thread.IsBackground = true;
				thread.Start();
			};
		}
		btnReboot.Click += (object s, EventArgs e) =>
		{
			try
			{
				Process.Start("shutdown", "/r /t 3");
			}
			catch
			{
			}
		};
		btnClose.Click += (object s, EventArgs e) =>
		{
			Application.Exit();
		};
		form2.Controls.Add(pictureBox2);
		form2.Controls.Add(value);
		form2.Controls.Add(value2);
		form2.Controls.Add(log);
		form2.Controls.Add(btnInstall);
		form2.Controls.Add(btnReboot);
		form2.Controls.Add(btnClose);
		Log("Ready to install. Click 'Install'.");
		Application.Run(form2);
	}

	private static void Log(string s)
	{
		if (log.InvokeRequired)
		{
			log.Invoke((Action)(() =>
			{
				Log(s);
			}));
		}
		else
		{
			log.AppendText(s + "\r\n");
		}
	}

	private static void SetReboot(bool on)
	{
		if (btnReboot.InvokeRequired)
		{
			btnReboot.Invoke((Action)(() =>
			{
				SetReboot(on);
			}));
		}
		else
		{
			btnReboot.Enabled = on;
			btnReboot.BackColor = (on ? Color.FromArgb(180, 110, 30) : Color.FromArgb(60, 60, 60));
		}
	}

	private static void RunInstall()
	{
		try
		{
			Log("== 1/6 Preparing the install folder ==");
			Directory.CreateDirectory("C:\\Program Files\\n52te");
			Log("   C:\\Program Files\\n52te");
			Log("== 2/6 Extracting files ==");
			StopRunningEditors();
			for (int i = 0; i < PAYLOAD.GetLength(0); i++)
			{
				string outPath = Path.Combine("C:\\Program Files\\n52te", PAYLOAD[i, 1]);
				Extract(PAYLOAD[i, 0], outPath);
				Log("   " + PAYLOAD[i, 1]);
			}
			CleanStale();
			Log("== 3/6 Removing old Belkin driver leftovers ==");
			CleanBelkin();
			CleanBelkinFilter();
			Log("== 4/6 Interception driver ==");
			string exe = Path.Combine("C:\\Program Files\\n52te", "install-interception.exe");
			if (DriverInstalled() && DriverRunningOnce())
			{
				Log("   already installed and running: not reinstalling (no restart needed).");
			}
			else
			{
				int num = Run(exe, "/install");
				Log("   install-interception /install -> exit code " + num);
			}
			Log("== 5/6 Creating configuration and shortcuts ==");
			CreateDefaultConfig();
			// The editor shortcut runs: AutoHotkey64.exe "C:\Program Files\n52te\n52te Editor.ahk"
			string text = Path.Combine("C:\\Program Files\\n52te", AHK_EXE);
			string editorArgs = "\"" + Path.Combine("C:\\Program Files\\n52te", EDITOR_SCRIPT) + "\"";
			string icon = Path.Combine("C:\\Program Files\\n52te", "infomalena.ico");
			string text2 = Path.Combine("C:\\Program Files\\n52te", "n52te_interception.exe");
			string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "n52te");
			Directory.CreateDirectory(text3);
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
			// Remove the Spanish-named shortcuts left by v1.1.0 and earlier.
			DeleteOldShortcut(Path.Combine(text3, "Editor n52te.lnk"));
			DeleteOldShortcut(Path.Combine(text3, "Remapeador n52te.lnk"));
			DeleteOldShortcut(Path.Combine(folderPath, "Editor n52te.lnk"));
			MakeShortcut(Path.Combine(text3, "n52te Editor.lnk"), text, editorArgs, "C:\\Program Files\\n52te", icon, "n52te button editor");
			MakeShortcut(Path.Combine(folderPath, "n52te Editor.lnk"), text, editorArgs, "C:\\Program Files\\n52te", icon, "n52te button editor");
			MakeShortcut(Path.Combine(text3, "n52te Remapper.lnk"), text2, "", "C:\\Program Files\\n52te", icon, "n52te remapper (runs in the background)");
			Log("   shortcuts created on the Desktop and in the Start menu");
			Log("== 6/6 Checking the driver ==");
			if (DriverRunning())
			{
				Log("   The driver is already RUNNING.");
				Log("   Starting the remapper...");
				try
				{
					// Start it through Explorer so it runs as the normal user, not as admin like
					// this installer. Users can edit its config, and the config can launch
					// programs ("run" actions), so the remapper must never run elevated.
					Process.Start("explorer.exe", "\"" + text2 + "\"");
				}
				catch (Exception ex)
				{
					Log("   (couldn't start it: " + ex.Message + ")");
				}
				Log("");
				Log("==========================================================");
				Log(" INSTALLATION COMPLETE. The n52te is ready to use.");
				Log(" Open 'n52te Editor' (on the Desktop) to configure the buttons.");
				Log(" To keep it always on, check 'Start the remapper with Windows'.");
				Log("==========================================================");
			}
			else
			{
				needReboot = true;
				SetReboot(true);
				Log("   The driver needs a RESTART to activate.");
				Log("");
				Log("==========================================================");
				Log(" ALMOST DONE: restart the computer to activate the driver.");
				Log(" Click 'Restart now', or restart whenever you like.");
				Log(" After restarting, open 'n52te Editor' and check");
				Log(" 'Start the remapper with Windows' so the n52te always works.");
				Log("==========================================================");
			}
		}
		catch (Exception ex)
		{
			Log("[ERROR] " + ex.Message);
			Log("If the problem persists, open an issue and include the text shown here.");
		}
	}

	private static void Extract(string res, string outPath)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		using (Stream stream = executingAssembly.GetManifestResourceStream(res))
		{
			if (stream == null)
			{
				throw new Exception("resource not found: " + res);
			}
			int num = 0;
			while (true)
			{
				bool flag = true;
				try
				{
					using (FileStream destination = File.Create(outPath))
					{
						stream.CopyTo(destination);
						break;
					}
				}
				catch (IOException)
				{
					if (num >= 3)
					{
						throw;
					}
					TryStopPath(outPath);
					Thread.Sleep(600);
					stream.Position = 0L;
				}
				num++;
			}
		}
	}

	// Stops only processes running this exact file, so an AutoHotkey64.exe the user runs
	// elsewhere (their own scripts) is never touched.
	private static void TryStopPath(string fullPath)
	{
		try
		{
			foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullPath)))
			{
				try
				{
					if (string.Equals(process.MainModule.FileName, fullPath, StringComparison.OrdinalIgnoreCase))
					{
						process.Kill();
						process.WaitForExit(2000);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	// Closes the editor before its files are replaced: this version's (AutoHotkey64.exe in
	// the install folder) and the compiled editors of earlier versions.
	private static void StopRunningEditors()
	{
		TryStopPath(Path.Combine("C:\\Program Files\\n52te", AHK_EXE));
		TryStop("n52te Editor");
		TryStop("Editor n52te");
	}

	private static void TryStop(string procName)
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName(procName);
			foreach (Process process in processesByName)
			{
				process.Kill();
				process.WaitForExit(2000);
			}
		}
		catch
		{
		}
	}

	private static void CleanBelkin()
	{
		string text = RunCapture("pnputil", "/enum-drivers");
		string[] array = text.Replace("\r", "").Split('\n');
		string text2 = null;
		int num = 0;
		string[] array2 = array;
		foreach (string text3 in array2)
		{
			string text4 = text3.Trim();
			int num2 = text4.IndexOf("oem", StringComparison.OrdinalIgnoreCase);
			if (num2 >= 0 && text4.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
			{
				int num3 = text4.IndexOf(':');
				text2 = ((num3 >= 0) ? text4.Substring(num3 + 1) : text4).Trim();
			}
			if ((text4.IndexOf("jmtflter", StringComparison.OrdinalIgnoreCase) >= 0 || text4.IndexOf("jmtfltr", StringComparison.OrdinalIgnoreCase) >= 0 || (text4.IndexOf("usbicp", StringComparison.OrdinalIgnoreCase) >= 0 && text2 != null) || text4.IndexOf("vhidmini", StringComparison.OrdinalIgnoreCase) >= 0) && text2 != null && text2.StartsWith("oem", StringComparison.OrdinalIgnoreCase))
			{
				Log("   removing " + text2 + " ...");
				Run("pnputil", "/delete-driver " + text2 + " /uninstall /force");
				num++;
				text2 = null;
			}
		}
		if (num == 0)
		{
			Log("   (no Belkin driver leftovers found)");
		}
	}

	[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
	private static extern bool MoveFileEx(string existing, string newName, int flags);

	private const int MOVEFILE_DELAY_UNTIL_REBOOT = 4;

	// Deleting the Belkin driver packages (above) leaves Belkin's USB filter behind: the
	// "jmtfltr" service, JmtFltr.sys, and LowerFilters entries on the n52te's device nodes.
	// On PCs where Memory Integrity is off the 2008 filter still loads, and a stale filter
	// entry can keep the keypad's mouse interface (the wheel) from starting.
	// See docs/original-driver-analysis.md.
	private static void CleanBelkinFilter()
	{
		int removed = 0;
		foreach (string iface in new string[2] { "VID_050D&PID_0200&MI_00", "VID_050D&PID_0200&MI_01" })
		{
			try
			{
				using (RegistryKey parent = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Enum\\USB\\" + iface))
				{
					if (parent == null)
					{
						continue;
					}
					foreach (string instance in parent.GetSubKeyNames())
					{
						using (RegistryKey node = parent.OpenSubKey(instance))
						{
							string[] filters = (node == null) ? null : (node.GetValue("LowerFilters") as string[]);
							if (filters == null || Array.FindIndex(filters, (string f) => string.Equals(f, "JmtFltr", StringComparison.OrdinalIgnoreCase)) < 0)
							{
								continue;
							}
						}
						// Removing the node drops its filter entries; Windows sets it up again
						// with its standard driver on the next scan or replug.
						string id = "USB\\" + iface + "\\" + instance;
						int code = Run("pnputil", "/remove-device \"" + id + "\"");
						Log("   removed device entry with the old Belkin filter: " + id + " (code " + code + ")");
						removed++;
					}
				}
			}
			catch (Exception ex)
			{
				Log("   (couldn't check " + iface + ": " + ex.Message + ")");
			}
		}
		if (removed > 0)
		{
			Run("pnputil", "/scan-devices");
		}
		using (RegistryKey svc = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\jmtfltr"))
		{
			if (svc == null)
			{
				if (removed == 0)
				{
					Log("   (no Belkin filter leftovers found)");
				}
				return;
			}
		}
		int sc = Run("sc", "delete jmtfltr");
		Log("   removed the old Belkin filter service jmtfltr (sc delete -> code " + sc + ")");
		string sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers\\JmtFltr.sys");
		if (File.Exists(sys))
		{
			try
			{
				File.Delete(sys);
				Log("   deleted " + sys);
			}
			catch
			{
				// Still loaded: delete it during the next restart.
				bool ok = MoveFileEx(sys, null, MOVEFILE_DELAY_UNTIL_REBOOT);
				Log(ok ? "   " + sys + " is in use; it will be deleted at the next restart" : "   (couldn't delete " + sys + ")");
			}
		}
	}

	private static void CreateDefaultConfig()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "n52te");
		Directory.CreateDirectory(text);
		// The editor and the remapper run as the normal (non-admin) user and must be able
		// to write here: n52te_config.ini, teclados.txt and aprender.req/.res. Files this
		// installer creates as admin are otherwise read-only for them ("Access is denied").
		// /T also fixes files left by earlier versions. S-1-5-32-545 = BUILTIN\Users.
		int num = Run("icacls", "\"" + text + "\" /grant *S-1-5-32-545:(OI)(CI)M /T /Q");
		Log((num == 0) ? "   gave users write access to " + text : "   (couldn't set permissions on " + text + ": icacls exit code " + num + ")");
		string text2 = Path.Combine(text, "n52te_config.ini");
		if (File.Exists(text2))
		{
			Log("   kept the existing configuration (" + text2 + ")");
			return;
		}
		string[] contents = new string[21]
		{
			"[Botones]", "B01=key|Enter", "B02=key|F14", "B03=key|F15", "B04=key|F16", "B05=key|F17", "B06=key|F18", "B07=key|F19", "B08=key|F20", "B09=key|F21",
			"B10=key|F22", "B11=key|F23", "B12=key|F24", "B13=key|Media_Play_Pause", "B14=key|Volume_Mute", "BALT=none|", "BDOWN=none|", "BESP=none|", "BLEFT=none|", "BRIGHT=none|",
			"BUP=none|"
		};
		File.WriteAllLines(text2, contents);
		Log("   created the default configuration");
	}

	private static void CleanStale()
	{
		// Compiled editors of earlier versions: "Editor n52te.exe" (v1.1.0 and earlier, Spanish)
		// and "n52te Editor.exe" (first English builds). AutoHotkey64.exe is current again.
		string[] array = new string[5] { "Editor n52te.ahk", "Editor n52te.exe", "n52te Editor.exe", "n52.ico", "n52te_config.ini" };
		string[] array2 = array;
		foreach (string text in array2)
		{
			try
			{
				string path = Path.Combine("C:\\Program Files\\n52te", text);
				if (File.Exists(path))
				{
					File.Delete(path);
					Log("   removed old file: " + text);
				}
			}
			catch
			{
			}
		}
	}

	private static void DeleteOldShortcut(string lnk)
	{
		try
		{
			if (File.Exists(lnk))
			{
				File.Delete(lnk);
				Log("   removed old shortcut: " + Path.GetFileName(lnk));
			}
		}
		catch
		{
		}
	}

	private static bool DriverRunningOnce()
	{
		string text = RunCapture("sc", "query keyboard");
		return text.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool DriverInstalled()
	{
		try
		{
			using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e96b-e325-11ce-bfc1-08002be10318}"))
			{
				if (registryKey == null)
				{
					return false;
				}
				string[] array = registryKey.GetValue("UpperFilters") as string[];
				if (array == null)
				{
					return false;
				}
				string[] array2 = array;
				foreach (string a in array2)
				{
					if (string.Equals(a, "keyboard", StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool DriverRunning()
	{
		for (int i = 0; i < 4; i++)
		{
			if (DriverRunningOnce())
			{
				return true;
			}
			Thread.Sleep(800);
		}
		return false;
	}

	private static void MakeShortcut(string lnk, string target, string args, string workdir, string icon, string desc)
	{
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			dynamic val = Activator.CreateInstance(typeFromProgID);
			dynamic val2 = val.CreateShortcut(lnk);
			val2.TargetPath = target;
			val2.Arguments = args;
			val2.WorkingDirectory = workdir;
			val2.IconLocation = icon;
			val2.Description = desc;
			val2.Save();
		}
		catch (Exception ex)
		{
			Log("   (couldn't create shortcut: " + ex.Message + ")");
		}
	}

	private static int Run(string exe, string args)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(exe, args);
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		ProcessStartInfo startInfo = processStartInfo;
		using (Process process = Process.Start(startInfo))
		{
			process.WaitForExit();
			return process.ExitCode;
		}
	}

	private static string RunCapture(string exe, string args)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(exe, args);
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.RedirectStandardOutput = true;
		ProcessStartInfo startInfo = processStartInfo;
		using (Process process = Process.Start(startInfo))
		{
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return result;
		}
	}
}
