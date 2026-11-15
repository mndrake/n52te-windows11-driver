// Recovered by decompiling the installer from Instalar-n52te.exe v1.1.0 (ILSpy 11.2, C# 5).
// Kept as close to the original as possible; build with build.ps1.

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

	private static readonly string[,] PAYLOAD = new string[7, 2]
	{
		{ "RES_REMAPPER", "n52te_interception.exe" },
		{ "RES_DLL", "interception.dll" },
		{ "RES_EDITOR", "Editor n52te.exe" },
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
		form.Text = "Instalar n52te para Windows 11";
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
		label.Text = "Instalador del keypad Belkin n52te";
		label.ForeColor = Color.White;
		label.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
		label.Left = 18;
		label.Top = 14;
		label.Width = 500;
		label.Height = 28;
		Label value = label;
		Label label2 = new Label();
		label2.Text = "Instala el driver, el remapeador y el editor, y deja el n52te funcionando.\nNecesita permisos de administrador (UAC) y puede pedir reiniciar al final.\nby INFOMALENA Software Development";
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
		button.Text = "Instalar";
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
		button2.Text = "Reiniciar ahora";
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
		button3.Text = "Cerrar";
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
		Log("Listo para instalar. Pulsa 'Instalar'.");
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
			Log("== 1/6 Preparando carpeta de instalacion ==");
			Directory.CreateDirectory("C:\\Program Files\\n52te");
			Log("   C:\\Program Files\\n52te");
			Log("== 2/6 Extrayendo archivos ==");
			for (int i = 0; i < PAYLOAD.GetLength(0); i++)
			{
				string outPath = Path.Combine("C:\\Program Files\\n52te", PAYLOAD[i, 1]);
				Extract(PAYLOAD[i, 0], outPath);
				Log("   " + PAYLOAD[i, 1]);
			}
			CleanStale();
			Log("== 3/6 Limpiando restos del driver antiguo de Belkin ==");
			CleanBelkin();
			Log("== 4/6 Driver Interception ==");
			string exe = Path.Combine("C:\\Program Files\\n52te", "install-interception.exe");
			if (DriverInstalled() && DriverRunningOnce())
			{
				Log("   ya esta instalado y activo: no se reinstala (sin reinicio).");
			}
			else
			{
				int num = Run(exe, "/install");
				Log("   install-interception /install -> codigo " + num);
			}
			Log("== 5/6 Creando configuracion y accesos directos ==");
			CreateDefaultConfig();
			string text = Path.Combine("C:\\Program Files\\n52te", "Editor n52te.exe");
			string icon = Path.Combine("C:\\Program Files\\n52te", "infomalena.ico");
			string text2 = Path.Combine("C:\\Program Files\\n52te", "n52te_interception.exe");
			string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "n52te");
			Directory.CreateDirectory(text3);
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
			MakeShortcut(Path.Combine(text3, "Editor n52te.lnk"), text, "", "C:\\Program Files\\n52te", text, "Editor de botones del n52te");
			MakeShortcut(Path.Combine(folderPath, "Editor n52te.lnk"), text, "", "C:\\Program Files\\n52te", text, "Editor de botones del n52te");
			MakeShortcut(Path.Combine(text3, "Remapeador n52te.lnk"), text2, "", "C:\\Program Files\\n52te", icon, "Remapeador del n52te (segundo plano)");
			Log("   accesos directos en Escritorio y menu Inicio");
			Log("== 6/6 Comprobando el driver ==");
			if (DriverRunning())
			{
				Log("   El driver ya esta ACTIVO.");
				Log("   Arrancando el remapeador...");
				try
				{
					ProcessStartInfo processStartInfo = new ProcessStartInfo(text2);
					processStartInfo.WorkingDirectory = "C:\\Program Files\\n52te";
					processStartInfo.UseShellExecute = false;
					ProcessStartInfo startInfo = processStartInfo;
					Process.Start(startInfo);
				}
				catch (Exception ex)
				{
					Log("   (no se pudo arrancar: " + ex.Message + ")");
				}
				Log("");
				Log("==========================================================");
				Log(" INSTALACION COMPLETADA. El n52te ya esta funcionando.");
				Log(" Abre 'Editor n52te' (Escritorio) para configurar los botones.");
				Log(" En el editor marca 'Iniciar con Windows' si lo quieres siempre.");
				Log("==========================================================");
			}
			else
			{
				needReboot = true;
				SetReboot(true);
				Log("   El driver necesita un REINICIO para activarse.");
				Log("");
				Log("==========================================================");
				Log(" CASI LISTO: reinicia el equipo para activar el driver.");
				Log(" Pulsa 'Reiniciar ahora' o reinicia cuando quieras.");
				Log(" Tras reiniciar, abre 'Editor n52te' y marca");
				Log(" 'Iniciar con Windows' para que el n52te funcione siempre.");
				Log("==========================================================");
			}
		}
		catch (Exception ex)
		{
			Log("[ERROR] " + ex.Message);
			Log("Si el problema persiste, dime que pone aqui.");
		}
	}

	private static void Extract(string res, string outPath)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		using (Stream stream = executingAssembly.GetManifestResourceStream(res))
		{
			if (stream == null)
			{
				throw new Exception("recurso no encontrado: " + res);
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
					TryStop(Path.GetFileNameWithoutExtension(outPath));
					Thread.Sleep(600);
					stream.Position = 0L;
				}
				num++;
			}
		}
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
				Log("   eliminando " + text2 + " ...");
				Run("pnputil", "/delete-driver " + text2 + " /uninstall /force");
				num++;
				text2 = null;
			}
		}
		if (num == 0)
		{
			Log("   (no habia restos del driver Belkin)");
		}
	}

	private static void CreateDefaultConfig()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "n52te");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, "n52te_config.ini");
		if (File.Exists(text2))
		{
			Log("   configuracion existente conservada (" + text2 + ")");
			return;
		}
		string[] contents = new string[21]
		{
			"[Botones]", "B01=key|Enter", "B02=key|F14", "B03=key|F15", "B04=key|F16", "B05=key|F17", "B06=key|F18", "B07=key|F19", "B08=key|F20", "B09=key|F21",
			"B10=key|F22", "B11=key|F23", "B12=key|F24", "B13=key|Media_Play_Pause", "B14=key|Volume_Mute", "BALT=none|", "BDOWN=none|", "BESP=none|", "BLEFT=none|", "BRIGHT=none|",
			"BUP=none|"
		};
		File.WriteAllLines(text2, contents);
		Log("   configuracion por defecto creada");
	}

	private static void CleanStale()
	{
		string[] array = new string[4] { "AutoHotkey64.exe", "Editor n52te.ahk", "n52.ico", "n52te_config.ini" };
		string[] array2 = array;
		foreach (string text in array2)
		{
			try
			{
				string path = Path.Combine("C:\\Program Files\\n52te", text);
				if (File.Exists(path))
				{
					File.Delete(path);
					Log("   limpiado archivo antiguo: " + text);
				}
			}
			catch
			{
			}
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
			Log("   (no se pudo crear acceso directo: " + ex.Message + ")");
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
