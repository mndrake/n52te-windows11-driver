// n52te-monitor: log what Windows receives from the n52te (keyboard, mouse, consumer/media and
// system-control reports). Read-only; uses Raw Input, no driver. Mouse and media events are logged
// whichever window has the focus, but on this PC Windows delivered keyboard events only while the
// monitor's own window had the focus, so click into it before pressing keys.
//
//   n52te-monitor [LOGFILE] [--all]      --all also logs every other keyboard/mouse
//
// Build: csc /nologo /platform:x64 /target:exe /r:System.Windows.Forms.dll /out:n52te-monitor.exe N52teMonitor.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal sealed class Monitor : Form
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE { public ushort UsagePage; public ushort Usage; public uint Flags; public IntPtr Target; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);

    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007, RIDEV_INPUTSINK = 0x100;
    private const int HeaderSize = 24; // RAWINPUTHEADER on x64

    private readonly Dictionary<IntPtr, string> names = new Dictionary<IntPtr, string>();
    private readonly TextWriter log;
    private readonly bool all;

    public Monitor(TextWriter log, bool all)
    {
        this.log = log;
        this.all = all;
        Text = "n52te monitor: click here, then press keys";
        Width = 520;
        Height = 160;
        TopMost = true;
        var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new System.Drawing.Font("Consolas", 12) };
        // What the focused text box gets through ordinary window messages, to compare with Raw Input.
        box.KeyDown += (s, e) => Write("text box KeyDown " + e.KeyCode + (e.Modifiers != Keys.None ? " (" + e.Modifiers + ")" : ""));
        box.KeyPress += (s, e) => Write("text box char '" + e.KeyChar + "'");
        Activated += (s, e) => Write("monitor window has the focus");
        Deactivate += (s, e) => Write("monitor window lost the focus");
        Controls.Add(box);
        CreateHandle();
        var devs = new[]
        {
            new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x06, Flags = RIDEV_INPUTSINK, Target = Handle }, // keyboard
            new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x02, Flags = RIDEV_INPUTSINK, Target = Handle }, // mouse
            new RAWINPUTDEVICE { UsagePage = 0x0C, Usage = 0x01, Flags = RIDEV_INPUTSINK, Target = Handle }, // consumer control
            new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x80, Flags = RIDEV_INPUTSINK, Target = Handle }, // system control
        };
        if (!RegisterRawInputDevices(devs, (uint)devs.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            throw new Exception("RegisterRawInputDevices failed, error " + Marshal.GetLastWin32Error());
    }

    private string Name(IntPtr device)
    {
        string n;
        if (names.TryGetValue(device, out n)) return n;
        uint size = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, null, ref size);
        var sb = new StringBuilder((int)size + 1);
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, sb, ref size);
        n = sb.ToString();
        names[device] = n;
        return n;
    }

    private static string Short(string name)
    {
        // \\?\HID#VID_050D&PID_0200&MI_01&Col02#... -> MI_01&Col02
        int i = name.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return name.Length > 40 ? name.Substring(0, 40) : name;
        int j = name.IndexOf('#', i);
        return name.Substring(i + 1, (j < 0 ? name.Length : j) - i - 1);
    }

    public void Write(string line)
    {
        line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line;
        Console.WriteLine(line);
        log.WriteLine(line);
        log.Flush();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            try { OnInput(m.LParam); }
            catch (Exception e) { Write("error reading raw input: " + e.Message); }
        }
        base.WndProc(ref m);
    }

    private void OnInput(IntPtr hRaw)
    {
        uint size = 0;
        GetRawInputData(hRaw, RID_INPUT, IntPtr.Zero, ref size, HeaderSize);
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(hRaw, RID_INPUT, buf, ref size, HeaderSize) != size) return;
            int type = Marshal.ReadInt32(buf, 0);
            IntPtr device = Marshal.ReadIntPtr(buf, 8);
            string name = device == IntPtr.Zero ? "(injected)" : Name(device);
            bool n52 = name.IndexOf("VID_050D", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!n52 && !all) return;
            string who = (n52 ? "n52te " : "other ") + Short(name);
            if (type == 1) // keyboard
            {
                ushort make = (ushort)Marshal.ReadInt16(buf, HeaderSize);
                ushort flags = (ushort)Marshal.ReadInt16(buf, HeaderSize + 2);
                ushort vk = (ushort)Marshal.ReadInt16(buf, HeaderSize + 6);
                Write(string.Format("{0} KEY {1,-4} vk {2:X2} {3,-14} scan {4:X2}{5}", who, (flags & 1) != 0 ? "up" : "down", vk, (Keys)vk, make, (flags & 2) != 0 ? " E0" : ""));
            }
            else if (type == 0) // mouse
            {
                if (!n52) return; // --all is for keyboards; other mice only flood the log
                ushort mflags = (ushort)Marshal.ReadInt16(buf, HeaderSize);
                ushort buttons = (ushort)Marshal.ReadInt16(buf, HeaderSize + 4);
                short data = Marshal.ReadInt16(buf, HeaderSize + 6);
                int dx = Marshal.ReadInt32(buf, HeaderSize + 12), dy = Marshal.ReadInt32(buf, HeaderSize + 16);
                var parts = new List<string>();
                string[] bn = { "L down", "L up", "R down", "R up", "M down", "M up", "X1 down", "X1 up", "X2 down", "X2 up" };
                for (int b = 0; b < bn.Length; b++) if ((buttons & (1 << b)) != 0) parts.Add(bn[b]);
                if ((buttons & 0x400) != 0) parts.Add("wheel " + data);
                if ((buttons & 0x800) != 0) parts.Add("hwheel " + data);
                if (dx != 0 || dy != 0) parts.Add(string.Format("move {0},{1}{2}", dx, dy, (mflags & 1) != 0 ? " (absolute)" : ""));
                if (parts.Count > 0) Write(who + " MOUSE " + string.Join(", ", parts.ToArray()));
            }
            else // HID: consumer / system control
            {
                int sizeHid = Marshal.ReadInt32(buf, HeaderSize), count = Marshal.ReadInt32(buf, HeaderSize + 4);
                var bytes = new byte[sizeHid * count];
                Marshal.Copy(IntPtr.Add(buf, HeaderSize + 8), bytes, 0, bytes.Length);
                Write(who + " HID " + BitConverter.ToString(bytes).Replace("-", " "));
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string path = null;
        bool all = false;
        foreach (string a in args) { if (a == "--all") all = true; else path = a; }
        if (path == null) path = "n52te-monitor.log";
        using (var log = new StreamWriter(path, true))
        {
            var mon = new Monitor(log, all);
            mon.Write("monitor started, logging " + (all ? "all devices" : "the n52te only") + " to " + Path.GetFullPath(path));
            Application.Run(mon);
        }
        return 0;
    }
}
