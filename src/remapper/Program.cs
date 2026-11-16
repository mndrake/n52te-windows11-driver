// Recovered by decompiling n52te_interception.exe from Instalar-n52te.exe v1.1.0 (ILSpy 11.2, C# 5).
// Build with build.ps1. User-facing text translated to US English; on-disk names
// (aprender.req/.res, teclados.txt, INI sections Dispositivo/Origen/Botones)
// are unchanged so existing configs and the editor keep working.

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

	// Button id (B01..B14, BESP, BALT, BUP.., BWHEEL) by learned scancode ([Origen]) or by
	// the factory virtual key in SRC (buttons that weren't learned).
	private static Dictionary<int, string> idBySc = new Dictionary<int, string>();

	private static Dictionary<int, string> idByVk = new Dictionary<int, string>();

	// One mapping table per keymap, like the original Belkin software:
	// 0 = Blue ([Botones], the default), 1 = Red ([Botones.Red]), 2 = Green ([Botones.Green]).
	private static readonly string[] LAYER_SECTION = new string[3] { "Botones", "Botones.Red", "Botones.Green" };

	private static readonly string[] LAYER_NAME = new string[3] { "Blue", "Red", "Green" };

	private static Dictionary<string, Mapping>[] layers = new Dictionary<string, Mapping>[3]
	{
		new Dictionary<string, Mapping>(),
		new Dictionary<string, Mapping>(),
		new Dictionary<string, Mapping>()
	};

	private static volatile int layer = 0;

	// Keymap to return to when a "hold" keymap button is released.
	private static int holdReturn = -1;

	private static string keymapFile;

	// The mapping chosen when each button went down, so its release is handled the same way
	// even if the keymap changed while it was held (no stuck keys).
	private static Dictionary<string, Mapping> downMap = new Dictionary<string, Mapping>();

	private static Dictionary<string, bool> srcDown = new Dictionary<string, bool>();

	private static Predicate predMouse;

	private static volatile bool learnSwallowMouse = false;

	// Learn/Identify code the editor recognizes as the wheel click (not a real scancode).
	private const uint WHEEL_CLICK_CODE = 0x1004u;

	private const ushort MOUSE_MIDDLE_DOWN = 0x10;

	private const ushort MOUSE_MIDDLE_UP = 0x20;

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
		{ "BRIGHT", 39 },
		// Scroll-wheel click: arrives from the n52te's mouse interface as a middle click.
		{ "BWHEEL", 4 }
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

	[DllImport("interception.dll")]
	private static extern int interception_is_mouse(int device);

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

	private static void Main(string[] args)
	{
		verbose = Array.Exists(args, (string a) => string.Equals(a, "/verbose", StringComparison.OrdinalIgnoreCase));
		Console.OutputEncoding = Encoding.UTF8;
		Console.WriteLine("==========================================================");
		Console.WriteLine(" n52te REMAPPER (Interception) - per-device separation");
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
		keymapFile = Path.Combine(cfgDir, "keymap.txt");
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
			Console.WriteLine("[ERROR] Couldn't create the Interception context. Is the driver installed, and did you restart?");
			Pause();
			return;
		}
		ListKeyboards(true);
		predKeep = interception_is_keyboard;
		interception_set_filter(ctx, predKeep, ushort.MaxValue);
		SetMouseFilter();
		SetLayer(0);
		Console.CancelKeyPress += (object s, ConsoleCancelEventArgs e) =>
		{
			e.Cancel = true;
			running = false;
		};
		Thread thread = new Thread(ConfigWatcher);
		thread.IsBackground = true;
		thread.Start();
		Console.WriteLine("[OK] Remapper running. Close this window (or press Ctrl+C) to stop and release the keyboard.");
		Loop();
		interception_destroy_context(ctx);
		Console.WriteLine("Stopped. Keyboard released.");
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
			if (interception_is_mouse(num) != 0)
			{
				HandleMouse(num, array);
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
			string id = ButtonId(num4, num2);
			Mapping value = (id == null) ? null : PickMapping(id, flag);
			Trace(id, num4, flag, value);
			if (value == null)
			{
				interception_send(ctx, num, array, 1u);
				continue;
			}
			Execute(id, value, flag);
		}
	}

	// "/verbose" on the command line logs every n52te button event (for troubleshooting).
	private static bool verbose = false;

	private static void Trace(string id, uint sc, bool isUp, Mapping map)
	{
		if (verbose)
		{
			Console.WriteLine("[EVENT] " + (id ?? "?") + " sc=" + sc.ToString("X2") + (isUp ? " up  " : " down") + " keymap=" + LAYER_NAME[layer] + " -> " + ((map == null) ? "pass through" : (map.Type + "|" + map.Value)));
		}
	}

	// Only the n52te's mouse interface is filtered, and only for middle-button events
	// (the wheel click): the user's own mouse and all mouse movement are never captured.
	private static void SetMouseFilter()
	{
		predMouse = (int device) => (interception_is_mouse(device) != 0 && IsN52te(device)) ? 1 : 0;
		interception_set_filter(ctx, predMouse, MOUSE_MIDDLE_DOWN | MOUSE_MIDDLE_UP);
	}

	private static void HandleMouse(int device, byte[] stroke)
	{
		int state = stroke[0] | (stroke[1] << 8);
		bool down = (state & MOUSE_MIDDLE_DOWN) != 0;
		bool up = (state & MOUSE_MIDDLE_UP) != 0;
		if ((!down && !up) || !IsN52te(device))
		{
			interception_send(ctx, device, stroke, 1u);
			return;
		}
		if (learnSwallowMouse && up)
		{
			learnSwallowMouse = false;
			return;
		}
		if (learnArmed && down && EditorForeground())
		{
			learnArmed = false;
			learnSwallowMouse = true;
			WriteLearnResult(HardwareId(device), WHEEL_CLICK_CODE);
			return;
		}
		if (EditorForeground())
		{
			interception_send(ctx, device, stroke, 1u);
			return;
		}
		Mapping map = PickMapping("BWHEEL", up);
		Trace("BWHEEL", WHEEL_CLICK_CODE, up, map);
		if (map == null)
		{
			interception_send(ctx, device, stroke, 1u);
			return;
		}
		Execute("BWHEEL", map, up);
	}

	private static string ButtonId(uint sc, ushort rawCode)
	{
		string id;
		if (idBySc.TryGetValue((int)sc, out id))
		{
			return id;
		}
		uint vk = MapVirtualKey(sc, 3u);
		if (vk == 0)
		{
			vk = MapVirtualKey(rawCode, 1u);
		}
		if (vk != 0 && idByVk.TryGetValue((int)vk, out id))
		{
			return id;
		}
		return null;
	}

	// Mapping for a button press/release; null means "pass the original key through".
	private static Mapping PickMapping(string id, bool isUp)
	{
		Mapping map;
		if (!isUp)
		{
			if (!downMap.TryGetValue(id, out map))
			{
				map = CurrentMapping(id);
				downMap[id] = map;
			}
			return map;
		}
		if (downMap.TryGetValue(id, out map))
		{
			downMap.Remove(id);
			return map;
		}
		return CurrentMapping(id);
	}

	private static Mapping CurrentMapping(string id)
	{
		Dictionary<string, Mapping>[] l = layers;
		Mapping map;
		// A keymap-switch button set in Blue works in every keymap, so you can never get stuck.
		if (l[0].TryGetValue(id, out map) && map.Type == "keymap")
		{
			return map;
		}
		l[layer].TryGetValue(id, out map);
		return map;
	}

	// "blue", "red", "green" (toggle), "<color> hold" (only while held), or "cycle".
	// Returns 0..2 for a color, -1 for cycle, -2 if invalid.
	private static int KeymapTarget(string value, out bool hold)
	{
		string v = value.Trim().ToLowerInvariant();
		hold = v.EndsWith(" hold");
		if (hold)
		{
			v = v.Substring(0, v.Length - 5).Trim();
		}
		if (v == "cycle")
		{
			return hold ? -2 : -1;
		}
		for (int i = 0; i < 3; i++)
		{
			if (v == LAYER_NAME[i].ToLowerInvariant())
			{
				return i;
			}
		}
		return -2;
	}

	private static void SetLayer(int l)
	{
		layer = l;
		Console.WriteLine("[KEYMAP] " + LAYER_NAME[l]);
		try
		{
			Console.Title = "n52te Remapper - " + LAYER_NAME[l] + " keymap";
		}
		catch
		{
		}
		try
		{
			File.WriteAllText(keymapFile, LAYER_NAME[l], new UTF8Encoding(false));
		}
		catch
		{
		}
	}

	private static void Execute(string id, Mapping map, bool isUp)
	{
		bool value;
		srcDown.TryGetValue(id, out value);
		srcDown[id] = !isUp;
		if (map.Type == "keymap")
		{
			bool hold;
			int target = KeymapTarget(map.Value, out hold);
			if (!isUp && !value)
			{
				if (target == -1)
				{
					SetLayer((layer + 1) % 3);
				}
				else if (target >= 0)
				{
					if (hold)
					{
						holdReturn = layer;
					}
					SetLayer(target);
				}
			}
			else if (isUp && hold && holdReturn >= 0)
			{
				SetLayer(holdReturn);
				holdReturn = -1;
			}
			return;
		}
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
		fgVal = stringBuilder.ToString() == "n52te Editor";
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
				stringBuilder.AppendLine((flag2 ? "[n52te] " : "[other] ") + "dev" + i + "  " + text);
			}
		}
		// The n52te's mouse interface (scroll wheel + wheel click). Other mice are not listed.
		for (int j = 11; j <= 20; j++)
		{
			string text2 = HardwareId(j);
			if (!string.IsNullOrEmpty(text2) && MatchesN52te(text2))
			{
				stringBuilder.AppendLine("[n52te] dev" + j + "  mouse (wheel click)  " + text2);
			}
		}
		if (print)
		{
			Console.WriteLine("Keyboards (Interception id 1..10) and the n52te's wheel (11..20):");
			Console.Write((stringBuilder.Length > 0) ? stringBuilder.ToString() : "  (none)\n");
			Console.WriteLine("n52te ids: " + string.Join(", ", deviceIds));
			Console.WriteLine(flag ? "[OK] n52te found. Only ITS keys are remapped; all other keyboards are left untouched." : "[WARNING] Can't see the n52te right now. If it's connected, use 'Learn keys' in the editor.");
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
			Console.WriteLine("[ERR learn] " + ex.Message);
		}
		Console.WriteLine("[LEARN] scancode " + sc.ToString("X2") + " from " + ((hw == "") ? "(no id)" : hw));
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
		Dictionary<string, Mapping>[] maps = new Dictionary<string, Mapping>[3]
		{
			new Dictionary<string, Mapping>(),
			new Dictionary<string, Mapping>(),
			new Dictionary<string, Mapping>()
		};
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
					else if (Array.IndexOf(LAYER_SECTION, text) >= 0 && SRC.ContainsKey(text4))
					{
						int num2 = text5.IndexOf('|');
						Mapping mapping = new Mapping();
						mapping.Type = ((num2 >= 0) ? text5.Substring(0, num2) : text5);
						mapping.Value = ((num2 >= 0) ? text5.Substring(num2 + 1) : "");
						if (mapping.Type == "none" || mapping.Value == "")
						{
							continue;
						}
						if (mapping.Type == "key" && !ResolveKey(mapping))
						{
							continue;
						}
						bool hold;
						if (mapping.Type == "keymap" && KeymapTarget(mapping.Value, out hold) == -2)
						{
							Console.WriteLine("[ERR config] unknown keymap action: " + mapping.Value);
							continue;
						}
						maps[Array.IndexOf(LAYER_SECTION, text)][text4] = mapping;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[ERR config] " + ex.Message);
		}
		Dictionary<int, string> bySc = new Dictionary<int, string>();
		Dictionary<int, string> byVk = new Dictionary<int, string>();
		foreach (KeyValuePair<string, int> item in SRC)
		{
			if (item.Key != "BWHEEL" && dictionary2.TryGetValue(item.Key, out result))
			{
				bySc[result] = item.Key;
			}
			else if (item.Key != "BWHEEL")
			{
				byVk[item.Value] = item.Key;
			}
		}
		idBySc = bySc;
		idByVk = byVk;
		layers = maps;
		bool anySwitch = false;
		foreach (Dictionary<string, Mapping> m in maps)
		{
			foreach (Mapping v in m.Values)
			{
				if (v.Type == "keymap")
				{
					anySwitch = true;
				}
			}
		}
		if (!anySwitch && layer != 0 && ctx != IntPtr.Zero)
		{
			// No way left to switch keymaps: go back to Blue instead of staying stuck.
			SetLayer(0);
		}
		if (list.Count > 0)
		{
			deviceIds = list.ToArray();
		}
		n52teCache = new Dictionary<int, bool>();
		Console.WriteLine("[CONFIG] buttons remapped: Blue " + maps[0].Count + ", Red " + maps[1].Count + ", Green " + maps[2].Count + " (" + dictionary2.Count + " with a learned key) " + (first ? "" : "(reloaded)"));
		if (!first && ctx != IntPtr.Zero)
		{
			ListKeyboards(false);
			SetMouseFilter();
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
			Console.WriteLine("[ERR config] unknown target key: " + m.Value);
			return false;
		}
		}
	}

	private static void Pause()
	{
		Console.WriteLine("Press ENTER to close.");
		Console.ReadLine();
	}
}
