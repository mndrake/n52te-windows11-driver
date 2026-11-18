// How the PC talks to the keypad: raw control transfers on endpoint 0, which Windows' HID API
// can't send (docs/reverse-engineering/03-usb-protocol.md). The only implementation is UsbDk
// (https://github.com/daynix/UsbDk), which lends the keypad to this program while it runs. The
// keypad does NOT type meanwhile; Windows gets it back when the transport is disposed or the
// program exits (also if it crashes).
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace N52te
{
    internal interface IKeypadTransport : IDisposable
    {
        // One control transfer. For IN requests (requestType bit 7) returns up to lengthIn bytes;
        // for OUT requests sends dataOut and returns null.
        byte[] Control(byte requestType, byte request, ushort value, ushort index, byte[] dataOut, int lengthIn);
    }

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
        public static extern uint WaitForMultipleObjects(uint count, IntPtr[] handles, bool waitAll, uint milliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool CancelIoEx(IntPtr file, IntPtr overlapped);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr handle);

        public static bool IsInstalled { get { return File.Exists(HelperPath); } }

        public static void Load()
        {
            if (!IsInstalled)
                throw new Exception("UsbDk is not installed (" + HelperPath + " not found). Install UsbDk 1.0.22 x64 from https://github.com/daynix/UsbDk/releases.");
            if (LoadLibrary(HelperPath) == IntPtr.Zero)
                throw new Exception("couldn't load " + HelperPath + " (error " + Marshal.GetLastWin32Error() + ")");
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

    internal sealed class UsbDkTransport : IKeypadTransport
    {
        public const ushort Vid = 0x050D, Pid = 0x0200;

        private IntPtr devices;
        private IntPtr handle;
        private readonly IntPtr evt;

        public UsbDkTransport()
        {
            UsbDk.Load();
            uint count;
            if (!UsbDk.UsbDk_GetDevicesList(out devices, out count))
                throw new Exception("UsbDk couldn't list USB devices (is the UsbDk service running?)");
            IntPtr found = IntPtr.Zero;
            for (int i = 0; i < count; i++)
            {
                IntPtr info = devices + i * UsbDk.DeviceInfoSize;
                IntPtr desc = info + UsbDk.DescriptorOffset;
                if (Marshal.ReadByte(desc, 0) != 18 || Marshal.ReadByte(desc, 1) != 1) continue; // not a device descriptor
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
                throw new Exception("UsbDk couldn't take over the keypad (error " + err + "). Close programs using it and try again; if it persists, replug the keypad.");
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
            if (evt != IntPtr.Zero) UsbDk.CloseHandle(evt);
        }

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
                if (!isIn && length > 0) Marshal.Copy(dataOut, 0, buffer + 8, length);
                for (int i = 0; i < UsbDk.RequestSize; i += 8) Marshal.WriteInt64(req, i, 0);
                Marshal.WriteInt64(req, 8, buffer.ToInt64());
                Marshal.WriteInt64(req, 16, 8 + length);
                for (int i = 0; i < 32; i += 8) Marshal.WriteInt64(ov, i, 0);
                Marshal.WriteIntPtr(ov, 24, evt);

                int r = isIn ? UsbDk.UsbDk_ReadPipe(handle, req, ov) : UsbDk.UsbDk_WritePipe(handle, req, ov);
                if (r == 0)
                    throw new IOException(string.Format("control transfer {0:X2} {1:X2} wValue {2} wIndex {3:X4} failed (error {4})", requestType, request, value, index, Marshal.GetLastWin32Error()));
                if (r == 2 && UsbDk.WaitForSingleObject(evt, 5000) != 0)
                    throw new IOException("control transfer timed out");
                long status = Marshal.ReadInt64(req, 56);
                long transferred = Marshal.ReadInt64(req, 48);
                if (status != 0)
                    throw new IOException(string.Format("control transfer {0:X2} {1:X2} wValue {2} wIndex {3:X4}: USBD status 0x{4:X8}", requestType, request, value, index, status));
                if (!isIn) return null;
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
                uint w = UsbDk.WaitForMultipleObjects(2, evts, false, (uint)Math.Max(1, (end - DateTime.Now).TotalMilliseconds));
                if (w > 1) break; // timeout
                int i = (int)w;
                long status = Marshal.ReadInt64(reqs[i], 56);
                int got = (int)Math.Min(64, Marshal.ReadInt64(reqs[i], 48));
                var data = new byte[Math.Max(0, got)];
                Marshal.Copy(bufs[i], data, 0, data.Length);
                o.WriteLine("{0:HH:mm:ss.fff} EP {1:X2}: {2}{3}", DateTime.Now, eps[i], Hex.Of(data), status != 0 ? string.Format(" (USBD status 0x{0:X8})", status) : "");
                start(i);
            }
            // Abort the pending reads before the redirect is stopped; otherwise UsbDk keeps the
            // keypad redirected after this program exits (seen on hardware: error 183 next time, and
            // the keypad types nothing until it is replugged). The handle from UsbDk_StartRedirect is
            // the helper's own pointer, not a file handle, so CancelIoEx on it does nothing.
            foreach (byte ep in eps) UsbDk.UsbDk_AbortPipe(handle, ep);
            if (UsbDk.WaitForMultipleObjects(2, evts, true, 2000) > 1)
            {
                IntPtr sys = UsbDk.UsbDk_GetRedirectorSystemHandle(handle);
                if (sys != IntPtr.Zero) UsbDk.CancelIoEx(sys, IntPtr.Zero);
                UsbDk.WaitForMultipleObjects(2, evts, true, 2000);
            }
        }
    }
}
