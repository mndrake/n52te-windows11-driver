// n52te-config: read, switch and write the Belkin n52te's onboard keymaps from Windows.
//
// The keypad runs its keymaps itself (keys, mouse, wheel, macros, keymap switching and the LED).
// This tool talks to its configuration protocol, documented in
// docs/reverse-engineering/03-usb-protocol.md and verified on hardware with the research/*.py tools.
//
// Transport: UsbDk (https://github.com/daynix/UsbDk), which lends the keypad to this program
// while it runs. The keypad does NOT type while this tool is connected; Windows gets it back
// when the tool exits (also if it crashes).
//
// Keymap numbers are the firmware's: 0 = Blue, 1 = Red, 2 = Green.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class UsbDk
{
    private const string HelperPath = @"C:\Program Files\UsbDk Runtime Library\UsbDkHelper.dll";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string path);

    [DllImport("UsbDkHelper.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UsbDk_GetDevicesList(out IntPtr devices, out uint count);

    [DllImport("UsbDkHelper.dll")]
    public static extern void UsbDk_ReleaseDevicesList(IntPtr devices);

    [DllImport("UsbDkHelper.dll", SetLastError = true)]
    public static extern IntPtr UsbDk_StartRedirect(IntPtr deviceId);

    [DllImport("UsbDkHelper.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UsbDk_StopRedirect(IntPtr handle);

    [DllImport("UsbDkHelper.dll", SetLastError = true)]
    public static extern int UsbDk_WritePipe(IntPtr handle, IntPtr request, IntPtr overlapped);

    [DllImport("UsbDkHelper.dll", SetLastError = true)]
    public static extern int UsbDk_ReadPipe(IntPtr handle, IntPtr request, IntPtr overlapped);

    [DllImport("UsbDkHelper.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UsbDk_AbortPipe(IntPtr handle, ulong pipeAddress);

    [DllImport("UsbDkHelper.dll")]
    public static extern IntPtr UsbDk_GetRedirectorSystemHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateEvent(IntPtr attributes, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr handle);

    public static void Load()
    {
        if (!File.Exists(HelperPath))
        {
            throw new Exception("UsbDk is not installed (" + HelperPath + " not found). Install UsbDk 1.0.22 x64 from https://github.com/daynix/UsbDk/releases.");
        }
        if (LoadLibrary(HelperPath) == IntPtr.Zero)
        {
            throw new Exception("couldn't load " + HelperPath + " (error " + Marshal.GetLastWin32Error() + ")");
        }
    }

    // USB_DK_DEVICE_INFO: ID (2 x 200 WCHAR = 800 bytes), FilterID, Port, Speed (ULONG64),
    // USB_DEVICE_DESCRIPTOR (18 bytes, packed), padded to 848 bytes.
    public const int DeviceInfoSize = 848;
    public const int DescriptorOffset = 824;

    // USB_DK_TRANSFER_REQUEST: EndpointAddress, Buffer, BufferLength, TransferType,
    // IsochronousPacketsArraySize, IsochronousPacketsArray, Result{BytesTransferred, UsbdStatus,
    // IsochronousResultsArray}: 9 x 8 bytes.
    public const int RequestSize = 72;
}

internal sealed class Keypad : IDisposable
{
    public const ushort Vid = 0x050D, Pid = 0x0200;
    public const int Chunk = 0x200, Chunks = 14, ImageSize = Chunk * Chunks;
    public static readonly string[] KeymapNames = { "Blue", "Red", "Green" };

    private IntPtr devices;
    private IntPtr handle;
    private readonly IntPtr evt;

    public Keypad()
    {
        UsbDk.Load();
        uint count;
        if (!UsbDk.UsbDk_GetDevicesList(out devices, out count))
        {
            throw new Exception("UsbDk couldn't list USB devices (is the UsbDk service running?)");
        }
        IntPtr found = IntPtr.Zero;
        for (int i = 0; i < count; i++)
        {
            IntPtr info = devices + i * UsbDk.DeviceInfoSize;
            IntPtr desc = info + UsbDk.DescriptorOffset;
            if (Marshal.ReadByte(desc, 0) != 18 || Marshal.ReadByte(desc, 1) != 1)
            {
                continue; // not a device descriptor: layout mismatch or odd device
            }
            if ((ushort)Marshal.ReadInt16(desc, 8) == Vid && (ushort)Marshal.ReadInt16(desc, 10) == Pid)
            {
                found = info;
                break;
            }
        }
        if (found == IntPtr.Zero)
        {
            UsbDk.UsbDk_ReleaseDevicesList(devices);
            devices = IntPtr.Zero;
            throw new Exception("n52te (050D:0200) not found by UsbDk. Is it plugged in (and not attached to a VM)?");
        }
        handle = UsbDk.UsbDk_StartRedirect(found); // the device ID is the first member of the info struct
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            int err = Marshal.GetLastWin32Error();
            UsbDk.UsbDk_ReleaseDevicesList(devices);
            devices = IntPtr.Zero;
            throw new Exception("UsbDk couldn't take over the keypad (error " + err + "). Close programs using it and try again.");
        }
        evt = UsbDk.CreateEvent(IntPtr.Zero, true, false, null);
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
        {
            UsbDk.UsbDk_StopRedirect(handle); // gives the keypad back to Windows
            handle = IntPtr.Zero;
        }
        if (devices != IntPtr.Zero)
        {
            UsbDk.UsbDk_ReleaseDevicesList(devices);
            devices = IntPtr.Zero;
        }
        if (evt != IntPtr.Zero)
        {
            UsbDk.CloseHandle(evt);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint WaitForMultipleObjects(uint count, IntPtr[] handles, bool waitAll, uint milliseconds);

    // Diagnostic: print the raw input reports the keypad sends on its interrupt endpoints
    // (0x81 keyboard, 0x82 mouse/consumer) for a number of seconds. Windows sees none of them
    // meanwhile. Buffers of reads still pending at the end are deliberately not freed: the
    // redirect is stopped right after, which cancels them.
    public void Listen(int seconds, TextWriter o)
    {
        byte[] eps = { 0x81, 0x82 };
        var bufs = new IntPtr[2];
        var reqs = new IntPtr[2];
        var ovs = new IntPtr[2];
        var evts = new IntPtr[2];
        for (int i = 0; i < 2; i++)
        {
            bufs[i] = Marshal.AllocHGlobal(64);
            reqs[i] = Marshal.AllocHGlobal(UsbDk.RequestSize);
            ovs[i] = Marshal.AllocHGlobal(32);
            evts[i] = UsbDk.CreateEvent(IntPtr.Zero, true, false, null);
        }
        Action<int> start = i =>
        {
            for (int k = 0; k < UsbDk.RequestSize; k += 8) Marshal.WriteInt64(reqs[i], k, 0);
            Marshal.WriteInt64(reqs[i], 0, eps[i]);
            Marshal.WriteInt64(reqs[i], 8, bufs[i].ToInt64());
            Marshal.WriteInt64(reqs[i], 16, 64);
            Marshal.WriteInt64(reqs[i], 24, 2); // InterruptTransferType
            for (int k = 0; k < 32; k += 8) Marshal.WriteInt64(ovs[i], k, 0);
            Marshal.WriteIntPtr(ovs[i], 24, evts[i]);
            if (UsbDk.UsbDk_ReadPipe(handle, reqs[i], ovs[i]) == 0)
                throw new IOException(string.Format("interrupt read on endpoint {0:X2} failed (error {1})", eps[i], Marshal.GetLastWin32Error()));
        };
        start(0);
        start(1);
        o.WriteLine("listening on endpoints 81 and 82 for {0} s: press the keypad's buttons", seconds);
        DateTime end = DateTime.Now.AddSeconds(seconds);
        while (DateTime.Now < end)
        {
            uint w = WaitForMultipleObjects(2, evts, false, (uint)Math.Max(1, (end - DateTime.Now).TotalMilliseconds));
            if (w > 1) break; // timeout
            int i = (int)w;
            long status = Marshal.ReadInt64(reqs[i], 56);
            int got = (int)Math.Min(64, Marshal.ReadInt64(reqs[i], 48));
            var data = new byte[Math.Max(0, got)];
            Marshal.Copy(bufs[i], data, 0, data.Length);
            o.WriteLine("{0:HH:mm:ss.fff} EP {1:X2}: {2}{3}", DateTime.Now, eps[i], Hex(data), status != 0 ? string.Format(" (USBD status 0x{0:X8})", status) : "");
            start(i);
        }
        // Abort the pending reads before the redirect is stopped; otherwise UsbDk keeps the
        // keypad redirected after this program exits (seen on hardware: error 183 next time, and
        // the keypad types nothing until it is replugged). The handle from UsbDk_StartRedirect is
        // the helper's own pointer, not a file handle, so CancelIoEx on it does nothing.
        foreach (byte ep in eps) UsbDk.UsbDk_AbortPipe(handle, ep);
        if (WaitForMultipleObjects(2, evts, true, 2000) > 1)
        {
            IntPtr sys = UsbDk.UsbDk_GetRedirectorSystemHandle(handle);
            if (sys != IntPtr.Zero) CancelIoEx(sys, IntPtr.Zero);
            WaitForMultipleObjects(2, evts, true, 2000);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool CancelIoEx(IntPtr file, IntPtr overlapped);

    // One control transfer on endpoint 0. For IN requests returns the data; for OUT returns null.
    public byte[] Control(byte requestType, byte request, ushort value, ushort index, byte[] dataOut, int lengthIn)
    {
        bool isIn = (requestType & 0x80) != 0;
        int length = isIn ? lengthIn : (dataOut == null ? 0 : dataOut.Length);
        IntPtr buffer = Marshal.AllocHGlobal(8 + length);
        IntPtr req = Marshal.AllocHGlobal(UsbDk.RequestSize);
        IntPtr ov = Marshal.AllocHGlobal(32);
        try
        {
            byte[] setup = { requestType, request, (byte)value, (byte)(value >> 8), (byte)index, (byte)(index >> 8), (byte)length, (byte)(length >> 8) };
            Marshal.Copy(setup, 0, buffer, 8);
            if (!isIn && length > 0)
            {
                Marshal.Copy(dataOut, 0, buffer + 8, length);
            }
            for (int i = 0; i < UsbDk.RequestSize; i += 8) Marshal.WriteInt64(req, i, 0);
            Marshal.WriteInt64(req, 8, buffer.ToInt64());
            Marshal.WriteInt64(req, 16, 8 + length);
            for (int i = 0; i < 32; i += 8) Marshal.WriteInt64(ov, i, 0);
            Marshal.WriteIntPtr(ov, 24, evt);

            int r = isIn ? UsbDk.UsbDk_ReadPipe(handle, req, ov) : UsbDk.UsbDk_WritePipe(handle, req, ov);
            if (r == 0)
            {
                throw new IOException(string.Format("control transfer {0:X2} {1:X2} wValue {2} wIndex {3:X4} failed (error {4})", requestType, request, value, index, Marshal.GetLastWin32Error()));
            }
            if (r == 2 && UsbDk.WaitForSingleObject(evt, 5000) != 0)
            {
                throw new IOException("control transfer timed out");
            }
            long status = Marshal.ReadInt64(req, 56);
            long transferred = Marshal.ReadInt64(req, 48);
            if (status != 0)
            {
                throw new IOException(string.Format("control transfer {0:X2} {1:X2} wValue {2} wIndex {3:X4}: USBD status 0x{4:X8}", requestType, request, value, index, status));
            }
            if (!isIn)
            {
                return null;
            }
            // BytesTransferred may or may not include the 8-byte setup packet; data always follows it.
            int got = (int)Math.Min(lengthIn, transferred >= 8 + lengthIn ? transferred - 8 : transferred);
            byte[] data = new byte[got];
            Marshal.Copy(buffer + 8, data, 0, got);
            return data;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(req);
            Marshal.FreeHGlobal(ov);
        }
    }

    // Requests that worked from Linux: interface recipient for wIndex low byte 0/1,
    // device recipient for higher chunk numbers.
    // Command 7 (IOCTL_ENTER_ICP) would put the keypad into its firmware-update mode. Never send it.
    private static void Guard(ushort value)
    {
        if (value == 7) throw new InvalidOperationException("command 7 (ENTER_ICP, firmware update) is never sent");
    }

    private byte[] Get(ushort value, ushort index, int length)
    {
        Guard(value);
        return Control((byte)((index & 0xFF) <= 1 ? 0xA1 : 0xA0), 0x01, value, index, null, length);
    }

    private void Set(ushort value, ushort index, byte[] data)
    {
        Guard(value);
        Control((byte)((index & 0xFF) <= 1 ? 0x21 : 0x20), 0x09, value, index, data, 0);
    }

    public static bool ChecksumOk(byte[] r)
    {
        return r.Length == 8 && r[6] == (r[0] ^ r[2] ^ r[4]) && r[7] == (r[1] ^ r[3] ^ r[5]);
    }

    public static byte[] Block(byte sub, byte value)
    {
        byte[] b = { 0x00, 0x08, 0x00, sub, 0x00, value, 0, 0 };
        b[6] = (byte)(b[0] ^ b[2] ^ b[4]);
        b[7] = (byte)(b[1] ^ b[3] ^ b[5]);
        return b;
    }

    public static byte[] ChunkChecksum(byte[] data, int offset, int count)
    {
        byte x0 = 0, x1 = 0;
        for (int i = 0; i < count; i += 2)
        {
            x0 ^= data[offset + i];
            if (i + 1 < count) x1 ^= data[offset + i + 1];
        }
        return new[] { x0, x1 };
    }

    public byte[] Firmware() { return Get(6, 0, 8); }

    public byte[] ProfileId() { return Get(5, 0, 8); }

    public void SelectKeymap(int km) { Set(4, 0, Block(3, (byte)km)); }

    // SET_POLLINGRATE (9), sub-command 4: report interval in ms (8 = 125 Hz, 2 = 500 Hz, 1 = 1000 Hz).
    public void SetPollingInterval(byte ms) { Set(9, 0, Block(4, ms)); }

    // GET_POLLINGRATE (10): never used by Belkin's software; reply format unknown.
    public byte[] PollingRate() { return Get(10, 0, 8); }

    public byte[] ReadKeymap(int km)
    {
        byte[] image = new byte[ImageSize];
        for (int chunk = 0; chunk < Chunks; chunk++)
        {
            byte[] d = Get(3, (ushort)((km << 8) | chunk), Chunk);
            if (d.Length != Chunk) throw new IOException("chunk " + chunk + " returned " + d.Length + " bytes");
            Buffer.BlockCopy(d, 0, image, chunk * Chunk, Chunk);
        }
        return image;
    }

    // Like n52teHid.exe: every chunk is checked with GET_RESULT; the whole keymap is resent on a
    // mismatch, up to three attempts.
    public bool WriteKeymap(int km, byte[] image, TextWriter log)
    {
        if (image.Length != ImageSize || image[0] != 0x1C || image[1] != 0x1B || image[4] != km)
        {
            throw new ArgumentException("not an upload image for keymap " + km);
        }
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            bool ok = true;
            for (int chunk = 0; chunk < Chunks && ok; chunk++)
            {
                byte[] data = new byte[Chunk];
                Buffer.BlockCopy(image, chunk * Chunk, data, 0, Chunk);
                Set(1, (ushort)((km << 8) | chunk), data);
                byte[] result = Get(2, 0, 2);
                byte[] want = ChunkChecksum(image, chunk * Chunk, Chunk);
                ok = result.Length == 2 && result[0] == want[0] && result[1] == want[1];
                log.WriteLine("  attempt {0} chunk {1,2}: GET_RESULT {2}, expected {3} {4}", attempt, chunk, Hex(result), Hex(want), ok ? "ok" : "MISMATCH");
            }
            if (ok) return true;
            Thread.Sleep(500);
        }
        return false;
    }

    public static byte[] UploadImage(byte[] source, int km)
    {
        byte[] image = (byte[])source.Clone();
        byte[] header = { 0x1C, 0x1B, 0x00, 0x01, (byte)km, 0, 0, 0 };
        Buffer.BlockCopy(header, 0, image, 0, 8);
        return image;
    }

    public static string Hex(byte[] b)
    {
        return BitConverter.ToString(b).Replace('-', ' ').ToLowerInvariant();
    }
}

internal static class Profile
{
    // Firmware key codes (from n52teCfg.exe v2.1.2; see research/decode_n52te_profile.py).
    public static readonly Dictionary<int, string> Codes = new Dictionary<int, string>
    {
        {0x10,"~"},{0x11,"1"},{0x12,"Q"},{0x13,"Tab"},{0x14,"A"},{0x15,"Esc"},{0x16,"Z"},{0x17,"MuHenkan"},{0x18,"F1"},
        {0x19,"2"},{0x1A,"W"},{0x1B,"Caps Lock"},{0x1C,"S"},{0x1E,"X"},{0x20,"F2"},{0x21,"3"},{0x22,"E"},{0x23,"F3"},
        {0x24,"D"},{0x25,"F4"},{0x26,"C"},{0x27,"Hanguel"},{0x28,"5"},{0x29,"4"},{0x2A,"R"},{0x2B,"T"},{0x2C,"F"},
        {0x2D,"G"},{0x2E,"V"},{0x2F,"B"},{0x30,"6"},{0x31,"7"},{0x32,"U"},{0x33,"Y"},{0x34,"J"},{0x35,"H"},{0x36,"M"},
        {0x37,"N"},{0x38,"="},{0x39,"8"},{0x3A,"I"},{0x3B,"]"},{0x3C,"K"},{0x3D,"F6"},{0x3E,","},{0x3F,"`"},{0x40,"F8"},
        {0x41,"9"},{0x42,"O"},{0x43,"F7"},{0x44,"L"},{0x45,"Henkan"},{0x46,"."},{0x47,"Katakana"},{0x48,"-"},{0x49,"0"},
        {0x4A,"P"},{0x4B,"["},{0x4C,";"},{0x4D,"'"},{0x4E,"102nd key"},{0x4F,"/"},{0x50,"F9"},{0x51,"F10"},
        {0x53,"Backspace"},{0x54,"\\"},{0x55,"F5"},{0x56,"Enter"},{0x57,"Space"},{0x58,"Insert"},{0x59,"F12"},
        {0x5A,"Num8"},{0x5B,"Num5"},{0x5C,"Num2"},{0x5D,"Num0"},{0x5E,"Num/"},{0x5F,"Right"},{0x60,"Delete"},
        {0x61,"F11"},{0x62,"Num7"},{0x63,"Num4"},{0x64,"Num1"},{0x66,"Num Lock"},{0x67,"Down"},{0x68,"Page Up"},
        {0x69,"Page Down"},{0x6A,"Num9"},{0x6B,"Num6"},{0x6C,"Num3"},{0x6D,"Num."},{0x6E,"Num*"},{0x6F,"Num-"},
        {0x70,"Home"},{0x71,"End"},{0x72,"Num+"},{0x74,"NumEnter"},{0x75,"Up"},{0x76,"Pause/Break"},{0x77,"Left"},
        {0x79,"Scroll Lock"},{0x7A,"PrtScr"},{0x7D,"L Alt"},{0x7F,"R Alt"},{0x83,"L Shift"},{0x86,"R Shift"},
        {0x87,"Profile"},{0x88,"L Ctrl"},{0x8E,"R Ctrl"},{0x8F,"Mute"},{0x9A,"App"},{0x9B,"DPad NE"},{0x9C,"Win"},
        {0x9D,"DPad SE"},{0x9E,"DPad SW"},{0x9F,"DPad NW"},
        {0xC0,"Cut"},{0xC1,"Copy"},{0xC2,"Paste"},
        {0xD0,"MOUSE Left Button"},{0xD1,"MOUSE Right Button"},{0xD2,"MOUSE Middle Button"},
        {0xD3,"MOUSE Scroll down"},{0xD4,"MOUSE Scroll up"},{0xD5,"MOUSE X-axis left"},{0xD6,"MOUSE X-axis right"},
        {0xD7,"MOUSE Y-axis left"},{0xD8,"MOUSE Y-axis right"},
        {0xD9,"KEYMAP Cycle"},{0xDA,"KEYMAP switch DA"},{0xDB,"KEYMAP switch DB"},{0xDC,"KEYMAP switch DC"},
        {0xDD,"KEYMAP switch DD"},{0xDE,"KEYMAP switch DE"},{0xDF,"KEYMAP switch DF"},
        {0xE8,"Next Track"},{0xE9,"Prev Track"},{0xEB,"Eject"},{0xEC,"Play/Pause"},{0xEE,"Volume Up"},
        {0xEF,"Volume Down"},{0xF0,"Media"},
    };

    public static readonly string[] Inputs =
    {
        "Key 01", "Key 02", "Key 03", "Key 04", "Key 05", "Key 06", "Key 07", "Key 08", "Key 09", "Key 10",
        "Key 11", "Key 12", "Key 13", "Key 14", "Thumb button (round)", "Wheel up", "Wheel click", "Wheel down",
        "Thumb pad", "D-pad up", "D-pad right", "D-pad down", "D-pad left",
        "D-pad up+right", "D-pad down+right", "D-pad down+left", "D-pad up+left",
    };

    // Per-input macro marker; the input's slot in the keymap image is marker - 0xA0.
    public static readonly byte[] MacroMark =
    {
        0xA0, 0xA4, 0xA8, 0xAC, 0xB0, 0xA1, 0xA5, 0xA9, 0xAD, 0xB1, 0xA2, 0xA6, 0xAA, 0xAE, 0xA3,
        0xBA, 0xB8, 0xB9, 0xB2, 0xB3, 0xAB, 0xAF, 0xA7, 0xB4, 0xB5, 0xB6, 0xB7,
    };

    public static readonly byte[] PcMark =
    {
        0x7C, 0x7E, 0x80, 0x81, 0x82, 0x84, 0x85, 0x87, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8F, 0x90,
        0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x99, 0x9B, 0x9D, 0x9E, 0x9F,
    };

    public static int Slot(int input) { return MacroMark[input - 1] - 0xA0; }

    public static string Name(int code)
    {
        string n;
        return Codes.TryGetValue(code, out n) ? n : string.Format("0x{0:X2}", code);
    }

    public static void Decode(int km, byte[] image, TextWriter o)
    {
        o.WriteLine("== Keymap {0} ({1}), header {2}", km, Keypad.KeymapNames[km], Keypad.Hex(new[] { image[0], image[1], image[2], image[3], image[4] }));
        for (int input = 1; input <= 27; input++)
        {
            int slot = Slot(input);
            int code = image[0x08 + slot];
            int macro = 0x100 + slot * 0x100;
            string what;
            if (code == 0) what = "disabled";
            else if (code == PcMark[input - 1]) what = string.Format("PC-side action type 0x{0:X2} (needs Belkin's service; dead on Windows 11)", image[macro]);
            else if (code == MacroMark[input - 1])
            {
                var steps = new List<string>();
                for (int j = 0; j < 0x100; j += 2)
                {
                    byte c = image[macro + j], f = image[macro + j + 1];
                    if (c == 0 && f == 0) break;
                    steps.Add(((f & 1) != 0 ? "+" : "-") + Name(c) + ((f >> 1) != 0 ? "(" + (f >> 1) * 50 + "ms)" : ""));
                }
                what = "macro: " + (steps.Count == 0 ? "(empty)" : string.Join(" ", steps.ToArray()));
            }
            else what = Name(code);
            o.WriteLine("  {0,2} {1,-22} {2:X2}  {3}", input, Inputs[input - 1], code, what);
        }
    }
}

internal static class Program
{
    private const string Usage =
@"n52te-config - read, switch and write the Belkin n52te's onboard keymaps (via UsbDk).
The keypad doesn't type while this runs; Windows gets it back when it exits.
Keymaps: 0 = blue, 1 = red, 2 = green.

  n52te-config info                     firmware version and active keymap
  n52te-config select blue|red|green    make that keymap active (the LED follows; kept after unplugging)
  n52te-config polling [125|500|1000]   set the USB polling rate (Hz); always shows the keypad's reply
  n52te-config read [0|1|2|all] [DIR]   read keymaps back, decode them, save keymapN.bin in DIR
  n52te-config roundtrip 0|1|2          write a keymap back unchanged and verify it (tests the write path)
  n52te-config set KM INPUT CODE --yes  set one input (1-27) of keymap KM to a firmware code (hex), verify
  n52te-config write FILE KM --yes      write a saved keymap image (e.g. a backup) as keymap KM, verify

Every write first saves the keymap's current contents as a backup. Backups and read-back
images go to Documents\n52te keymaps (read: unless DIR is given).";

    // Durable and writable by the user wherever the tool is run from: it's installed under
    // Program Files, and a backup must never fail because the current folder is read-only.
    private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "n52te keymaps");

    private static int Main(string[] args)
    {
        if (args.Length == 0) { Console.WriteLine(Usage); return 1; }
        try
        {
            return Run(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 2;
        }
    }

    private static int ParseKeymap(string s)
    {
        switch (s.ToLowerInvariant())
        {
            case "0": case "blue": return 0;
            case "1": case "red": return 1;
            case "2": case "green": return 2;
        }
        throw new ArgumentException("keymap must be blue/red/green or 0/1/2: " + s);
    }

    private static bool HasYes(string[] args) { return Array.IndexOf(args, "--yes") >= 0; }

    private static int Run(string[] args)
    {
        string cmd = args[0].ToLowerInvariant();
        using (var pad = new Keypad())
        {
            byte[] fw = pad.Firmware();
            byte[] pid = pad.ProfileId();
            Console.WriteLine("n52te firmware reply {0} ({1}); active keymap {2} ({3})", Keypad.Hex(fw), Keypad.ChecksumOk(fw) ? "checksum ok" : "checksum MISMATCH", pid[5], pid[5] < 3 ? Keypad.KeymapNames[pid[5]] : "?");
            switch (cmd)
            {
                case "info":
                    return 0;

                case "listen":
                    pad.Listen(args.Length > 1 ? int.Parse(args[1]) : 20, Console.Out);
                    return 0;

                case "descriptor":
                {
                    // Standard GET_DESCRIPTOR(CONFIGURATION), read-only: shows each endpoint's
                    // bInterval, which is how often Windows polls it.
                    byte[] d = pad.Control(0x80, 0x06, 0x0200, 0, null, 255);
                    Console.WriteLine("configuration descriptor ({0} bytes): {1}", d.Length, Keypad.Hex(d));
                    for (int i = 0; i + 1 < d.Length && d[i] > 0; i += d[i])
                    {
                        if (d[i + 1] == 4 && i + 8 < d.Length)
                            Console.WriteLine("  interface {0}: class {1:X2} subclass {2:X2} protocol {3:X2}", d[i + 2], d[i + 5], d[i + 6], d[i + 7]);
                        else if (d[i + 1] == 5 && i + 6 < d.Length)
                            Console.WriteLine("  endpoint {0:X2}: attributes {1:X2}, max packet {2}, bInterval {3}", d[i + 2], d[i + 3], d[i + 4] | (d[i + 5] << 8), d[i + 6]);
                    }
                    return 0;
                }

                case "polling":
                {
                    if (args.Length > 1)
                    {
                        byte ms;
                        switch (args[1])
                        {
                            case "125": ms = 8; break;
                            case "500": ms = 2; break;
                            case "1000": ms = 1; break;
                            default: throw new ArgumentException("polling rate must be 125, 500 or 1000");
                        }
                        pad.SetPollingInterval(ms);
                        Console.WriteLine("sent SET_POLLINGRATE: {0} Hz (interval {1} ms)", args[1], ms);
                    }
                    byte[] r = pad.PollingRate();
                    Console.WriteLine("GET_POLLINGRATE reply {0} ({1})", Keypad.Hex(r), Keypad.ChecksumOk(r) ? "checksum ok" : "checksum MISMATCH or unknown format");
                    return 0;
                }

                case "select":
                {
                    int km = ParseKeymap(args[1]);
                    pad.SelectKeymap(km);
                    byte[] after = pad.ProfileId();
                    Console.WriteLine("selected keymap {0} ({1}); keypad now reports {2}", km, Keypad.KeymapNames[km], after[5]);
                    return after[5] == km ? 0 : 3;
                }

                case "read":
                {
                    string which = args.Length > 1 ? args[1] : "all";
                    string dir = args.Length > 2 ? args[2] : DataDir;
                    Directory.CreateDirectory(dir);
                    int[] kms = which == "all" ? new[] { 0, 1, 2 } : new[] { ParseKeymap(which) };
                    foreach (int km in kms)
                    {
                        byte[] image = pad.ReadKeymap(km);
                        string path = Path.Combine(dir, "keymap" + km + ".bin");
                        File.WriteAllBytes(path, image);
                        Profile.Decode(km, image, Console.Out);
                        Console.WriteLine("  saved " + Path.GetFullPath(path));
                    }
                    return 0;
                }

                case "roundtrip":
                case "set":
                case "write":
                {
                    int km;
                    byte[] source;
                    byte[] before;
                    if (cmd == "roundtrip")
                    {
                        km = ParseKeymap(args[1]);
                        before = pad.ReadKeymap(km);
                        source = before;
                    }
                    else if (cmd == "set")
                    {
                        if (args.Length < 4 || !HasYes(args)) throw new ArgumentException("usage: set KM INPUT CODE --yes");
                        km = ParseKeymap(args[1]);
                        int input = int.Parse(args[2]);
                        int code = Convert.ToInt32(args[3], 16);
                        if (input < 1 || input > 27 || code < 0 || code > 0xFF) throw new ArgumentException("INPUT must be 1-27 and CODE 00-FF");
                        before = pad.ReadKeymap(km);
                        source = (byte[])before.Clone();
                        int slot = Profile.Slot(input);
                        Console.WriteLine("setting input {0} ({1}, slot {2}): {3:X2} ({4}) -> {5:X2} ({6})", input, Profile.Inputs[input - 1], slot, source[0x08 + slot], Profile.Name(source[0x08 + slot]), code, Profile.Name(code));
                        source[0x08 + slot] = (byte)code;
                    }
                    else
                    {
                        if (args.Length < 3 || !HasYes(args)) throw new ArgumentException("usage: write FILE KM --yes");
                        source = File.ReadAllBytes(args[1]);
                        if (source.Length != Keypad.ImageSize) throw new ArgumentException(args[1] + " is " + source.Length + " bytes, expected " + Keypad.ImageSize);
                        km = ParseKeymap(args[2]);
                        before = pad.ReadKeymap(km);
                    }
                    Directory.CreateDirectory(DataDir);
                    string backup = Path.Combine(DataDir, string.Format("keymap{0}-before-{1:yyyyMMdd-HHmmss}.bin", km, DateTime.Now));
                    File.WriteAllBytes(backup, before);
                    Console.WriteLine("backup of keymap {0} before writing: {1}", km, Path.GetFullPath(backup));
                    byte[] image = Keypad.UploadImage(source, km);
                    if (!pad.WriteKeymap(km, image, Console.Out))
                    {
                        Console.WriteLine("RESULT: write failed after 3 attempts; restore with: n52te-config write \"{0}\" {1} --yes", backup, km);
                        return 4;
                    }
                    byte[] after = pad.ReadKeymap(km);
                    bool same = true;
                    for (int i = 8; i < Keypad.ImageSize; i++) if (after[i] != image[i]) { same = false; Console.WriteLine("RESULT: differs at 0x{0:X}: wrote {1:X2}, read {2:X2}", i, image[i], after[i]); break; }
                    if (same) Console.WriteLine("RESULT: keymap {0} written and verified (read-back identical)", km);
                    return same ? 0 : 5;
                }
            }
            Console.WriteLine(Usage);
            return 1;
        }
    }
}
