// Recovered by decompiling n52te_interception.exe from Instalar-n52te.exe v1.1.0 (ILSpy 11.2, C# 5).
// Kept as close to the original as possible; build with build.ps1.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
internal static class Program
{
	private delegate int Predicate(int device);

	[StructLayout(LayoutKind.Explicit, Size = 40)]
	private struct INPUT
	{
		[FieldOffset(0)]
		public uint type;

		[FieldOffset(8)]
		public ushort wVk;

		[FieldOffset(10)]
		public ushort wScan;

		[FieldOffset(12)]
		public uint kFlags;

		[FieldOffset(16)]
		public uint kTime;

		[FieldOffset(24)]
		public UIntPtr kExtra;

		[FieldOffset(8)]
		public int dx;

		[FieldOffset(12)]
		public int dy;

		[FieldOffset(16)]
		public uint mouseData;

		[FieldOffset(20)]
		public uint mFlags;

		[FieldOffset(24)]
		public uint mTime;

		[FieldOffset(32)]
		public UIntPtr mExtra;
	}

	private class Mapping
	{
		public string Type;

		public string Value;

		public int TargetVk;

		public uint MDown;

		public uint MUp;

		public uint MData;
	}

	private const ushort FILTER_KEY_ALL = ushort.MaxValue;

	private const int STROKE = 20;

	private const uint KEYEVENTF_KEYUP = 2u;

	private const uint KEYEVENTF_UNICODE = 4u;

	private const uint KEYEVENTF_EXT = 1u;

	private static readonly HashSet<int> EXT = new HashSet<int>
	{
		33, 34, 35, 36, 37, 38, 39, 40, 45, 46,
		44, 163, 165, 91, 92, 93, 144, 173, 174, 175,
		176, 177, 178, 179
	};

	private static IntPtr ctx;

	private static Predicate predKeep;

	private static string iniPath;

	private static DateTime iniTime = DateTime.MinValue;

	private static Dictionary<int, Mapping> mapByVk = new Dictionary<int, Mapping>();

	private static Dictionary<int, Mapping> mapBySc = new Dictionary<int, Mapping>();

	private static Dictionary<int, bool> srcDown = new Dictionary<int, bool>();

	private static Dictionary<int, bool> n52teCache = new Dictionary<int, bool>();

	private static string[] deviceIds = new string[1] { "VID_050D" };

	private static string cfgDir;

	private static string learnReq;

	private static string learnRes;

	private static string kbdList;

	private static volatile bool learnArmed = false;

	private static int learnSwallow = -1;

	private static volatile bool running = true;

	private static Dictionary<string, int> KEYNAME = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "F1", 112 },
		{ "F2", 113 },
		{ "F3", 114 },
		{ "F4", 115 },
		{ "F5", 116 },
		{ "F6", 117 },
		{ "F7", 118 },
		{ "F8", 119 },
		{ "F9", 120 },
		{ "F10", 121 },
		{ "F11", 122 },
		{ "F12", 123 },
		{ "F13", 124 },
		{ "F14", 125 },
		{ "F15", 126 },
		{ "F16", 127 },
		{ "F17", 128 },
		{ "F18", 129 },
		{ "F19", 130 },
		{ "F20", 131 },
		{ "F21", 132 },
		{ "F22", 133 },
		{ "F23", 134 },
		{ "F24", 135 },
		{ "Enter", 13 },
		{ "Space", 32 },
		{ "Esc", 27 },
		{ "Escape", 27 },
		{ "Tab", 9 },
		{ "Backspace", 8 },
		{ "BS", 8 },
		{ "Delete", 46 },
		{ "Del", 46 },
		{ "Insert", 45 },
		{ "Home", 36 },
		{ "End", 35 },
		{ "PgUp", 33 },
		{ "PgDn", 34 },
		{ "Up", 38 },
		{ "Down", 40 },
		{ "Left", 37 },
		{ "Right", 39 },
		{ "LCtrl", 162 },
		{ "RCtrl", 163 },
		{ "LShift", 160 },
		{ "RShift", 161 },
		{ "LAlt", 164 },
		{ "RAlt", 165 },
		{ "LWin", 91 },
		{ "RWin", 92 },
		{ "AppsKey", 93 },
		{ "CapsLock", 20 },
		{ "PrintScreen", 44 },
		{ "Media_Play_Pause", 179 },
		{ "Media_Next", 176 },
		{ "Media_Prev", 177 },
		{ "Media_Stop", 178 },
		{ "Volume_Up", 175 },
		{ "Volume_Down", 174 },
		{ "Volume_Mute", 173 },
		{ "Browser_Back", 166 },
		{ "Browser_Forward", 167 },
		{ "Numpad0", 96 },
		{ "Numpad1", 97 },
		{ "Numpad2", 98 },
		{ "Numpad3", 99 },
		{ "Numpad4", 100 },
		{ "Numpad5", 101 },
		{ "Numpad6", 102 },
		{ "Numpad7", 103 },
		{ "Numpad8", 104 },
		{ "Numpad9", 105 }
	};

	private static long fgTick = -1000L;

	private static bool fgVal = false;

	private static readonly Stopwatch fgClock = Stopwatch.StartNew();

	private static Dictionary<string, int> SRC = new Dictionary<string, int>
	{
		{ "B01", 9 },
		{ "B02", 81 },
		{ "B03", 87 },
		{ "B04", 69 },
		{ "B05", 82 },
		{ "B06", 20 },
		{ "B07", 65 },
		{ "B08", 83 },
		{ "B09", 68 },
		{ "B10", 70 },
		{ "B11", 160 },
		{ "B12", 90 },
		{ "B13", 88 },
		{ "B14", 67 },
		{ "BESP", 32 },
		{ "BALT", 164 },
		{ "BUP", 38 },
		{ "BDOWN", 40 },
		{ "BLEFT", 37 },
		{ "BRIGHT", 39 }
	};

	[DllImport("interception.dll")]
	private static extern IntPtr interception_create_context();

	[DllImport("interception.dll")]
	private static extern void interception_destroy_context(IntPtr ctx);

	[DllImport("interception.dll")]
	private static extern void interception_set_filter(IntPtr ctx, Predicate predicate, ushort filter);

	[DllImport("interception.dll")]
	private static extern int interception_wait(IntPtr ctx);

	[DllImport("interception.dll")]
	private static extern int interception_receive(IntPtr ctx, int device, byte[] stroke, uint nstroke);

	[DllImport("interception.dll")]
	private static extern int interception_send(IntPtr ctx, int device, byte[] stroke, uint nstroke);

	[DllImport("interception.dll")]
	private static extern uint interception_get_hardware_id(IntPtr ctx, int device, byte[] buf, uint size);

	[DllImport("interception.dll")]
	private static extern int interception_is_keyboard(int device);

	[DllImport("user32.dll")]
	private static extern uint SendInput(uint n, INPUT[] inputs, int size);

	[DllImport("user32.dll")]
	private static extern short VkKeyScan(char c);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint code, uint t);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);

	private static void Main()
	{
		Console.OutputEncoding = Encoding.UTF8;
		Console.WriteLine("==========================================================");
		Console.WriteLine(" REMAPEADOR n52te (Interception) - separacion por dispositivo");
		Console.WriteLine("==========================================================");
		cfgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "n52te");
		try
		{
			Directory.CreateDirectory(cfgDir);
		}
		catch
		{
		}
		iniPath = Path.Combine(cfgDir, "n52te_config.ini");
		learnReq = Path.Combine(cfgDir, "aprender.req");
		learnRes = Path.Combine(cfgDir, "aprender.res");
		kbdList = Path.Combine(cfgDir, "teclados.txt");
		try
		{
			File.Delete(learnReq);
		}
		catch
		{
		}
		string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "n52te_config.ini");
		if (!File.Exists(iniPath) && File.Exists(text))
		{
			try
			{
				File.Copy(text, iniPath);
			}
			catch
			{
			}
		}
		LoadConfig(true);
		ctx = interception_create_context();
		if (ctx == IntPtr.Zero)
		{
			Console.WriteLine("[ERROR] no se pudo crear el contexto. ¿Esta instalado el driver y reiniciaste?");
			Pause();
			return;
		}
		ListKeyboards(true);
		predKeep = interception_is_keyboard;
		interception_set_filter(ctx, predKeep, ushort.MaxValue);
		Console.CancelKeyPress += (object s, ConsoleCancelEventArgs e) =>
		{
			e.Cancel = true;
			running = false;
		};
		Thread thread = new Thread(ConfigWatcher);
		thread.IsBackground = true;
		thread.Start();
		Console.WriteLine("[OK] remapeador activo. Cierra esta ventana (o Ctrl+C) para parar y liberar el teclado.");
		Loop();
		interception_destroy_context(ctx);
		Console.WriteLine("Detenido. Teclado liberado.");
	}

	private static void Loop()
	{
		byte[] array = new byte[20];
		while (running)
		{
			int num = interception_wait(ctx);
			if (num <= 0 || interception_receive(ctx, num, array, 1u) <= 0)
			{
				continue;
			}
			ushort num2 = (ushort)(array[0] | (array[1] << 8));
			ushort num3 = (ushort)(array[2] | (array[3] << 8));
			bool flag = (num3 & 1) != 0;
			uint num4 = (uint)(((num3 & 2) != 0) ? (num2 | 0xE000) : num2);
			if (learnSwallow == (int)num4 && flag)
			{
				learnSwallow = -1;
				continue;
			}
			if (learnArmed && !flag && EditorForeground())
			{
				learnArmed = false;
				learnSwallow = (int)num4;
				WriteLearnResult(HardwareId(num), num4);
				continue;
			}
			if (!IsN52te(num))
			{
				interception_send(ctx, num, array, 1u);
				continue;
			}
			if (EditorForeground())
			{
				interception_send(ctx, num, array, 1u);
				continue;
			}
			Mapping value;
			int srcVk;
			if (mapBySc.TryGetValue((int)num4, out value))
			{
				srcVk = (int)(0x10000 | num4);
			}
			else
			{
				uint num5 = MapVirtualKey(num4, 3u);
				if (num5 == 0)
				{
					num5 = MapVirtualKey(num2, 1u);
				}
				if (num5 == 0 || !mapByVk.TryGetValue((int)num5, out value))
				{
					interception_send(ctx, num, array, 1u);
					continue;
				}
				srcVk = (int)num5;
			}
			Execute(srcVk, value, flag);
		}
	}

	private static void Execute(int srcVk, Mapping map, bool isUp)
	{
		bool value;
		srcDown.TryGetValue(srcVk, out value);
		srcDown[srcVk] = !isUp;
		if (map.Type == "key")
		{
			if (map.MDown != 0)
			{
				if (!isUp)
				{
					if (!value)
					{
						SendMouse(map.MDown, map.MData);
					}
				}
				else
				{
					SendMouse(map.MUp, map.MData);
				}
			}
			else
			{
				SendKey(map.TargetVk, isUp);
			}
		}
		else
		{
			if (isUp || value)
			{
				return;
			}
			if (map.Type == "send")
			{
				SendSpec(map.Value);
			}
			else if (map.Type == "text")
			{
				SendText(map.Value);
			}
			else if (map.Type == "run")
			{
				try
				{
					Process.Start(map.Value);
				}
				catch (Exception ex)
				{
					Console.WriteLine("[ERR run] " + ex.Message);
				}
			}
		}
	}

	private static void SendKey(int vk, bool up)
	{
		INPUT[] array = new INPUT[1];
		array[0].type = 1u;
		array[0].wVk = (ushort)vk;
		array[0].wScan = (ushort)MapVirtualKey((uint)vk, 0u);
		uint num = (up ? 2u : 0u);
		if (EXT.Contains(vk))
		{
			num |= 1;
		}
		array[0].kFlags = num;
		SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
	}

	private static void SendMouse(uint flag, uint data)
	{
		INPUT[] array = new INPUT[1];
		array[0].type = 0u;
		array[0].mFlags = flag;
		array[0].mouseData = data;
		SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
	}

	private static void SendText(string s)
	{
		foreach (char wScan in s)
		{
			INPUT[] array = new INPUT[2];
			array[0].type = 1u;
			array[0].wScan = wScan;
			array[0].kFlags = 4u;
			array[1].type = 1u;
			array[1].wScan = wScan;
			array[1].kFlags = 6u;
			SendInput(2u, array, Marshal.SizeOf(typeof(INPUT)));
		}
	}

	private static void SendSpec(string spec)
	{
		List<int> list = new List<int>();
		int num = 0;
		while (num < spec.Length)
		{
			char c = spec[num];
			switch (c)
			{
			case '^':
				list.Add(162);
				num++;
				continue;
			case '!':
				list.Add(164);
				num++;
				continue;
			case '+':
				list.Add(160);
				num++;
				continue;
			case '#':
				list.Add(91);
				num++;
				continue;
			}
			int num2 = 1;
			int value;
			if (c == '{')
			{
				int num3 = spec.IndexOf('}', num);
				if (num3 < 0)
				{
					return;
				}
				string text = spec.Substring(num + 1, num3 - num - 1).Trim();
				num = num3 + 1;
				int num4 = text.LastIndexOf(' ');
				int result;
				if (num4 > 0 && int.TryParse(text.Substring(num4 + 1), out result))
				{
					num2 = result;
					text = text.Substring(0, num4);
				}
				if (!KEYNAME.TryGetValue(text, out value))
				{
					if (text.Length != 1)
					{
						continue;
					}
					value = VkOf(text[0], list);
				}
			}
			else
			{
				value = VkOf(c, list);
				num++;
			}
			foreach (int item in list)
			{
				SendKey(item, false);
			}
			for (int i = 0; i < num2; i++)
			{
				SendKey(value, false);
				SendKey(value, true);
			}
			for (int current = list.Count - 1; current >= 0; current--)
			{
				SendKey(list[current], true);
			}
			list.Clear();
		}
	}

	private static int VkOf(char c, List<int> mods)
	{
		short num = VkKeyScan(c);
		if (((num >> 8) & 1) != 0 && !mods.Contains(160))
		{
			mods.Add(160);
		}
		return num & 0xFF;
	}

	private static bool EditorForeground()
	{
		long elapsedMilliseconds = fgClock.ElapsedMilliseconds;
		if (elapsedMilliseconds - fgTick < 150)
		{
			return fgVal;
		}
		fgTick = elapsedMilliseconds;
		StringBuilder stringBuilder = new StringBuilder(64);
		GetWindowText(GetForegroundWindow(), stringBuilder, 64);
		fgVal = stringBuilder.ToString() == "Editor n52te";
		return fgVal;
	}

	private static string HardwareId(int device)
	{
		byte[] array = new byte[4096];
		uint num = interception_get_hardware_id(ctx, device, array, (uint)array.Length);
		if (num == 0 || num > array.Length)
		{
			return "";
		}
		string text = Encoding.Unicode.GetString(array, 0, (int)num);
		char[] trimChars = new char[1];
		return text.TrimEnd(trimChars).Replace('\0', ' ');
	}

	private static bool MatchesN52te(string hw)
	{
		string[] array = deviceIds;
		foreach (string value in array)
		{
			if (hw.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsN52te(int device)
	{
		bool value;
		if (n52teCache.TryGetValue(device, out value))
		{
			return value;
		}
		value = MatchesN52te(HardwareId(device));
		n52teCache[device] = value;
		return value;
	}

	private static void ListKeyboards(bool print)
	{
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		for (int i = 1; i <= 10; i++)
		{
			string text = HardwareId(i);
			if (!string.IsNullOrEmpty(text))
			{
				bool flag2 = MatchesN52te(text);
				if (flag2)
				{
					flag = true;
				}
				stringBuilder.AppendLine((flag2 ? "[n52te] " : "[otro ] ") + "dev" + i + "  " + text);
			}
		}
		if (print)
		{
			Console.WriteLine("Teclados (id Interception 1..10):");
			Console.Write((stringBuilder.Length > 0) ? stringBuilder.ToString() : "  (ninguno)\n");
			Console.WriteLine("Ids del n52te: " + string.Join(", ", deviceIds));
			Console.WriteLine(flag ? "[OK] n52te localizado. Solo SUS teclas se remapean; el resto de teclados quedan intactos." : "[AVISO] no veo el n52te ahora mismo. Si esta conectado, usa 'Aprender teclas' en el editor.");
		}
		try
		{
			File.WriteAllText(kbdList, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	private static void WriteLearnResult(string hw, uint sc)
	{
		try
		{
			string text = learnRes + ".tmp";
			File.WriteAllText(text, hw + "\n" + sc.ToString("X2") + "\n", Encoding.UTF8);
			if (File.Exists(learnRes))
			{
				File.Delete(learnRes);
			}
			File.Move(text, learnRes);
			File.Delete(learnReq);
		}
		catch (Exception ex)
		{
			Console.WriteLine("[ERR aprender] " + ex.Message);
		}
		Console.WriteLine("[APRENDER] scancode " + sc.ToString("X2") + " de " + ((hw == "") ? "(sin id)" : hw));
	}

	private static void ConfigWatcher()
	{
		int num = 0;
		while (running)
		{
			Thread.Sleep(200);
			try
			{
				learnArmed = File.Exists(learnReq);
			}
			catch
			{
			}
			if (++num % 5 != 0)
			{
				continue;
			}
			try
			{
				if (File.Exists(iniPath) && File.GetLastWriteTimeUtc(iniPath) != iniTime)
				{
					LoadConfig(false);
				}
			}
			catch
			{
			}
		}
	}

	private static void LoadConfig(bool first)
	{
		Dictionary<string, Mapping> dictionary = new Dictionary<string, Mapping>();
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		List<string> list = new List<string>();
		int result;
		try
		{
			if (File.Exists(iniPath))
			{
				iniTime = File.GetLastWriteTimeUtc(iniPath);
				string text = "";
				string[] array = File.ReadAllLines(iniPath);
				foreach (string text2 in array)
				{
					string text3 = text2.Trim();
					if (text3.StartsWith("["))
					{
						text = text3.Trim('[', ']');
						continue;
					}
					int num = text3.IndexOf('=');
					if (num < 1)
					{
						continue;
					}
					string text4 = text3.Substring(0, num).Trim();
					string text5 = text3.Substring(num + 1).Trim();
					if (text == "Dispositivo" && text4 == "Ids")
					{
						string[] array2 = text5.Split(',');
						foreach (string text6 in array2)
						{
							if (text6.Trim() != "")
							{
								list.Add(text6.Trim());
							}
						}
					}
					else if (text == "Origen")
					{
						if (SRC.ContainsKey(text4) && int.TryParse(text5, NumberStyles.HexNumber, null, out result) && result > 0)
						{
							dictionary2[text4] = result;
						}
					}
					else if (!(text != "Botones") && SRC.ContainsKey(text4))
					{
						int num2 = text5.IndexOf('|');
						Mapping mapping = new Mapping();
						mapping.Type = ((num2 >= 0) ? text5.Substring(0, num2) : text5);
						mapping.Value = ((num2 >= 0) ? text5.Substring(num2 + 1) : "");
						if (!(mapping.Type == "none") && !(mapping.Value == "") && (!(mapping.Type == "key") || ResolveKey(mapping)))
						{
							dictionary[text4] = mapping;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[ERR config] " + ex.Message);
		}
		Dictionary<int, Mapping> dictionary3 = new Dictionary<int, Mapping>();
		Dictionary<int, Mapping> dictionary4 = new Dictionary<int, Mapping>();
		foreach (KeyValuePair<string, Mapping> item in dictionary)
		{
			if (dictionary2.TryGetValue(item.Key, out result))
			{
				dictionary4[result] = item.Value;
			}
			else
			{
				dictionary3[SRC[item.Key]] = item.Value;
			}
		}
		mapByVk = dictionary3;
		mapBySc = dictionary4;
		if (list.Count > 0)
		{
			deviceIds = list.ToArray();
		}
		n52teCache = new Dictionary<int, bool>();
		Console.WriteLine("[CONFIG] " + dictionary.Count + " botones remapeados (" + dictionary2.Count + " con tecla aprendida) " + (first ? "" : "(recargado)"));
		if (!first && ctx != IntPtr.Zero)
		{
			ListKeyboards(false);
		}
	}

	private static bool ResolveKey(Mapping m)
	{
		switch (m.Value.ToUpperInvariant())
		{
		case "LBUTTON":
			m.MDown = 2u;
			m.MUp = 4u;
			return true;
		case "RBUTTON":
			m.MDown = 8u;
			m.MUp = 16u;
			return true;
		case "MBUTTON":
			m.MDown = 32u;
			m.MUp = 64u;
			return true;
		case "XBUTTON1":
			m.MDown = 128u;
			m.MUp = 256u;
			m.MData = 1u;
			return true;
		case "XBUTTON2":
			m.MDown = 128u;
			m.MUp = 256u;
			m.MData = 2u;
			return true;
		default:
		{
			int value;
			if (KEYNAME.TryGetValue(m.Value, out value))
			{
				m.TargetVk = value;
				return true;
			}
			if (m.Value.Length == 1)
			{
				m.TargetVk = VkKeyScan(m.Value[0]) & 0xFF;
				return true;
			}
			Console.WriteLine("[ERR config] tecla destino desconocida: " + m.Value);
			return false;
		}
		}
	}

	private static void Pause()
	{
		Console.WriteLine("Pulsa ENTER para cerrar.");
		Console.ReadLine();
	}
}
