using System.Runtime.InteropServices;
using System.Text;

namespace N52te.Editor;

/// <summary>
/// Windows Raw Input: tells this window which device every key press, click and scroll came from.
/// Only active while the window is in the foreground. No hook, no driver.
/// </summary>
internal static class RawInput
{
    public const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007;
    private const uint RIDEV_REMOVE = 0x1;
    private const int RIM_TYPEMOUSE = 0, RIM_TYPEKEYBOARD = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetRawInputDeviceInfoW")]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder? data, ref uint size);

    private static int HeaderSize => 8 + 2 * IntPtr.Size;

    public static void Register(IntPtr window) => Set(window, 0);

    public static void Unregister() => Set(IntPtr.Zero, RIDEV_REMOVE);

    private static void Set(IntPtr window, uint flags)
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE { UsagePage = 1, Usage = 6, Flags = flags, Target = window },   // keyboards
            new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = flags, Target = window },   // mice
        };
        RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    private static readonly Dictionary<IntPtr, string> Names = new();

    public static string DeviceName(IntPtr device)
    {
        if (device == IntPtr.Zero) return "";
        if (Names.TryGetValue(device, out var cached)) return cached;
        uint size = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, null, ref size);
        var sb = new StringBuilder((int)size + 1);
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, sb, ref size);
        return Names[device] = sb.ToString();
    }

    /// <summary>Reads a WM_INPUT message into zero or more events, with the name of the device that sent them.</summary>
    public static (string Device, List<DetectedEvent> Events)? Read(IntPtr lParam)
    {
        uint size = 0;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)HeaderSize);
        if (size == 0) return null;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)HeaderSize) != size) return null;
            int type = Marshal.ReadInt32(buffer, 0);
            var device = Marshal.ReadIntPtr(buffer, 8);
            int h = HeaderSize;
            var events = new List<DetectedEvent>();
            if (type == RIM_TYPEKEYBOARD)
            {
                int make = (ushort)Marshal.ReadInt16(buffer, h);
                int flags = (ushort)Marshal.ReadInt16(buffer, h + 2);
                int vk = (ushort)Marshal.ReadInt16(buffer, h + 6);
                if ((flags & 0x4) == 0 && WindowsKeys.FromRawKey(vk, make, (flags & 0x2) != 0) is { } code)   // skip E1 (Pause) sequences
                    events.Add(new DetectedKey(code, (flags & 0x1) == 0));
            }
            else if (type == RIM_TYPEMOUSE)
            {
                int moveFlags = (ushort)Marshal.ReadInt16(buffer, h);
                int buttons = (ushort)Marshal.ReadInt16(buffer, h + 4);
                short wheel = Marshal.ReadInt16(buffer, h + 6);
                int dx = Marshal.ReadInt32(buffer, h + 12), dy = Marshal.ReadInt32(buffer, h + 16);
                void If(int bit, DetectedMouseKind kind) { if ((buttons & bit) != 0) events.Add(new DetectedMouse(kind)); }
                If(0x01, DetectedMouseKind.LeftDown); If(0x02, DetectedMouseKind.LeftUp);
                If(0x04, DetectedMouseKind.RightDown); If(0x08, DetectedMouseKind.RightUp);
                If(0x10, DetectedMouseKind.MiddleDown); If(0x20, DetectedMouseKind.MiddleUp);
                if ((buttons & 0x400) != 0 && wheel != 0) events.Add(new DetectedMouse(wheel > 0 ? DetectedMouseKind.WheelUp : DetectedMouseKind.WheelDown));
                if ((moveFlags & 0x1) == 0 && (dx != 0 || dy != 0))   // relative movement only
                {
                    if (Math.Abs(dx) >= Math.Abs(dy)) events.Add(new DetectedMouse(dx < 0 ? DetectedMouseKind.MoveLeft : DetectedMouseKind.MoveRight));
                    else events.Add(new DetectedMouse(dy < 0 ? DetectedMouseKind.MoveUp : DetectedMouseKind.MoveDown));
                }
            }
            return (DeviceName(device), events);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
