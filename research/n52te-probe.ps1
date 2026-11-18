<#
.SYNOPSIS
  Read-only probe of a Belkin/Razer n52te keypad on Windows 10/11.

.DESCRIPTION
  Lists every HID interface of the keypad (default: USB vendor ID 050D), with
  what Windows' HID parser reports about it (top-level usage, report lengths,
  report IDs, input/output/feature items). It then asks the keypad's USB hub
  for the device, configuration and HID report descriptors, the same way USB
  viewer tools do, and decodes them.

  Nothing is written to the keypad and no driver or registry setting is
  changed. With -TryGet the probe also issues HID GET_FEATURE requests for
  the feature report IDs the keypad declares. These are read-only requests.

  The report is printed, saved to the Desktop and copied to the clipboard.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\n52te-probe.ps1
  powershell -ExecutionPolicy Bypass -File .\n52te-probe.ps1 -TryGet
#>
param(
    [switch]$TryGet,
    [string]$Vid = '050D',
    [string]$Out = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'n52te-probe.txt')
)

$source = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class N52teProbe
{
    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public int DevInst; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

    const int DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;
    const int CM_DRP_ADDRESS = 0x1D;
    const int HIDP_STATUS_SUCCESS = 0x00110000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, GENERIC_WRITE = 0x40000000;
    const uint IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION = 0x220410;
    static Guid GUID_DEVINTERFACE_USB_HUB = new Guid("f18a0e88-c30c-11d0-8815-00a0c906bed8");

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwnd, int flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, ref SP_DEVINFO_DATA info);
    [DllImport("setupapi.dll")]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("cfgmgr32.dll")]
    static extern int CM_Get_Parent(out int parent, int devInst, int flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_ID(int devInst, StringBuilder buffer, int length, int flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_DevNode_Registry_Property(int devInst, int property, out int type, byte[] buffer, ref int length, int flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_Interface_List_Size(out int length, ref Guid g, string deviceId, int flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_Interface_List(ref Guid g, string deviceId, char[] buffer, int length, int flags);

    [DllImport("hid.dll")]
    static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr pp);
    [DllImport("hid.dll")]
    static extern bool HidD_FreePreparsedData(IntPtr pp);
    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool HidD_GetProductString(SafeFileHandle h, byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetFeature(SafeFileHandle h, byte[] buffer, int length);
    [DllImport("hid.dll")]
    static extern int HidP_GetCaps(IntPtr pp, byte[] caps);
    [DllImport("hid.dll")]
    static extern int HidP_GetButtonCaps(int reportType, IntPtr caps, ref ushort length, IntPtr pp);
    [DllImport("hid.dll")]
    static extern int HidP_GetValueCaps(int reportType, IntPtr caps, ref ushort length, IntPtr pp);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuffer, int inLength, byte[] outBuffer, int outLength, out int returned, IntPtr overlapped);

    static StringBuilder log;
    static void W(string fmt, params object[] args) { log.AppendLine(args.Length == 0 ? fmt : String.Format(fmt, args)); }

    static string Hex(byte[] data, int offset, int count)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0 && i % 16 == 0) sb.Append("\r\n    ");
            else if (i > 0) sb.Append(' ');
            sb.Append(data[offset + i].ToString("X2"));
        }
        return sb.ToString();
    }

    static string DeviceId(int devInst)
    {
        StringBuilder sb = new StringBuilder(512);
        return CM_Get_Device_ID(devInst, sb, sb.Capacity, 0) == 0 ? sb.ToString() : "";
    }

    public static string Run(string vid, bool tryGet)
    {
        log = new StringBuilder();
        W("n52te probe - {0:u}", DateTime.Now);
        W("Windows {0}, {1}-bit process", Environment.OSVersion.Version, IntPtr.Size * 8);
        W("Looking for HID interfaces with VID_{0}. Read-only{1}.", vid.ToUpperInvariant(), tryGet ? " (GET_FEATURE requests enabled)" : "");
        W("");

        Guid hidGuid;
        HidD_GetHidGuid(out hidGuid);
        IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) { W("SetupDiGetClassDevs failed: {0}", Marshal.GetLastWin32Error()); return log.ToString(); }

        Dictionary<int, string> usbDevices = new Dictionary<int, string>();
        int found = 0;
        try
        {
            for (int index = 0; ; index++)
            {
                SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref ifData)) break;

                SP_DEVINFO_DATA info = new SP_DEVINFO_DATA();
                info.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                int required;
                SetupDiGetDeviceInterfaceDetail(set, ref ifData, IntPtr.Zero, 0, out required, ref info);
                IntPtr detail = Marshal.AllocHGlobal(required);
                string path;
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref ifData, detail, required, out required, ref info)) continue;
                    path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                }
                finally { Marshal.FreeHGlobal(detail); }

                if (path.IndexOf("vid_" + vid, StringComparison.OrdinalIgnoreCase) < 0) continue;
                found++;
                string hidId = DeviceId(info.DevInst);
                W("=== HID interface {0}", found);
                W("  Device ID : {0}", hidId);
                W("  Path      : {0}", path);
                DescribeHid(path, tryGet);

                int usbDev = FindUsbDevice(info.DevInst);
                if (usbDev != 0 && !usbDevices.ContainsKey(usbDev)) usbDevices[usbDev] = DeviceId(usbDev);
                W("");
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }

        if (found == 0)
        {
            W("No HID interface with VID_{0} is present. Plug the n52te in (directly, not through a KVM) and run the probe again.", vid);
            return log.ToString();
        }

        foreach (KeyValuePair<int, string> dev in usbDevices) DescribeUsb(dev.Key, dev.Value);
        return log.ToString();
    }

    // Walk up from the HID devnode to the USB device (composite parent) node.
    static int FindUsbDevice(int devInst)
    {
        int current = devInst;
        for (int depth = 0; depth < 6; depth++)
        {
            int parent;
            if (CM_Get_Parent(out parent, current, 0) != 0) return 0;
            string id = DeviceId(parent);
            if (id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) && id.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) < 0)
                return parent;
            current = parent;
        }
        return 0;
    }

    static void DescribeHid(string path, bool tryGet)
    {
        using (SafeFileHandle h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
        {
            if (h.IsInvalid) { W("  (cannot open for query: error {0})", Marshal.GetLastWin32Error()); return; }

            HIDD_ATTRIBUTES a = new HIDD_ATTRIBUTES();
            a.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
            if (HidD_GetAttributes(h, ref a))
                W("  VID/PID   : {0:X4}/{1:X4}, version {2:X4}", a.VendorID, a.ProductID, a.VersionNumber);
            byte[] name = new byte[256];
            if (HidD_GetProductString(h, name, name.Length))
                W("  Product   : {0}", Encoding.Unicode.GetString(name).TrimEnd('\0'));

            IntPtr pp;
            if (!HidD_GetPreparsedData(h, out pp)) { W("  (no preparsed data: error {0})", Marshal.GetLastWin32Error()); return; }
            try
            {
                byte[] caps = new byte[64];
                if (HidP_GetCaps(pp, caps) != HIDP_STATUS_SUCCESS) { W("  HidP_GetCaps failed"); return; }
                ushort usage = BitConverter.ToUInt16(caps, 0), page = BitConverter.ToUInt16(caps, 2);
                W("  Top usage : page 0x{0:X4} usage 0x{1:X4} ({2})", page, usage, UsageName(page, usage));
                W("  Report len: input {0}, output {1}, feature {2} bytes (including report ID)",
                    BitConverter.ToUInt16(caps, 4), BitConverter.ToUInt16(caps, 6), BitConverter.ToUInt16(caps, 8));

                List<int> featureIds = new List<int>();
                string[] kinds = { "Input", "Output", "Feature" };
                for (int type = 0; type < 3; type++)
                {
                    ushort nButtons = BitConverter.ToUInt16(caps, 46 + type * 6);
                    ushort nValues = BitConverter.ToUInt16(caps, 48 + type * 6);
                    if (nButtons + nValues == 0) continue;
                    W("  {0} items:", kinds[type]);
                    ListCaps(type, nButtons, true, pp, type == 2 ? featureIds : null);
                    ListCaps(type, nValues, false, pp, type == 2 ? featureIds : null);
                }

                if (tryGet && featureIds.Count > 0)
                {
                    int len = BitConverter.ToUInt16(caps, 8);
                    foreach (int id in featureIds)
                    {
                        byte[] buf = new byte[Math.Max(len, 2)];
                        buf[0] = (byte)id;
                        if (HidD_GetFeature(h, buf, buf.Length))
                            W("  GET_FEATURE id {0}: {1}", id, Hex(buf, 0, buf.Length));
                        else
                            W("  GET_FEATURE id {0}: failed, error {1}", id, Marshal.GetLastWin32Error());
                    }
                }
            }
            finally { HidD_FreePreparsedData(pp); }
        }
    }

    static void ListCaps(int type, ushort count, bool buttons, IntPtr pp, List<int> ids)
    {
        if (count == 0) return;
        const int size = 72;
        IntPtr buf = Marshal.AllocHGlobal(size * count);
        try
        {
            ushort n = count;
            int status = buttons ? HidP_GetButtonCaps(type, buf, ref n, pp) : HidP_GetValueCaps(type, buf, ref n, pp);
            if (status != HIDP_STATUS_SUCCESS) { W("    ({0} caps failed: 0x{1:X8})", buttons ? "button" : "value", status); return; }
            for (int i = 0; i < n; i++)
            {
                IntPtr c = new IntPtr(buf.ToInt64() + i * size);
                ushort page = (ushort)Marshal.ReadInt16(c, 0);
                byte reportId = Marshal.ReadByte(c, 2);
                bool isRange = Marshal.ReadByte(c, 12) != 0;
                ushort u1 = (ushort)Marshal.ReadInt16(c, 56), u2 = (ushort)Marshal.ReadInt16(c, 58);
                string usages = isRange ? String.Format("0x{0:X2}-0x{1:X2}", u1, u2) : String.Format("0x{0:X2}", u1);
                if (ids != null && !ids.Contains(reportId)) ids.Add(reportId);
                if (buttons)
                    W("    report {0,3}  button  page 0x{1:X4} usage {2}", reportId, page, usages);
                else
                    W("    report {0,3}  value   page 0x{1:X4} usage {2}  {3} x {4} bit, logical {5}..{6}",
                        reportId, page, usages, (ushort)Marshal.ReadInt16(c, 20), (ushort)Marshal.ReadInt16(c, 18),
                        Marshal.ReadInt32(c, 40), Marshal.ReadInt32(c, 44));
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    static string UsageName(ushort page, ushort usage)
    {
        if (page == 1 && usage == 6) return "keyboard";
        if (page == 1 && usage == 2) return "mouse";
        if (page == 1 && usage == 4) return "joystick";
        if (page == 1 && usage == 0x80) return "system control";
        if (page == 0x0C) return "consumer control";
        if (page >= 0xFF00) return "vendor-defined";
        return "other";
    }

    // ---- USB descriptors through the parent hub ---------------------------------------

    static void DescribeUsb(int devInst, string deviceId)
    {
        W("=== USB device {0}", deviceId);
        int length = 4;
        byte[] addr = new byte[4];
        int type;
        if (CM_Get_DevNode_Registry_Property(devInst, CM_DRP_ADDRESS, out type, addr, ref length, 0) != 0)
        { W("  (no port address; cannot query descriptors)"); return; }
        int port = BitConverter.ToInt32(addr, 0);
        int hub;
        if (CM_Get_Parent(out hub, devInst, 0) != 0) { W("  (no parent hub)"); return; }
        string hubId = DeviceId(hub);
        string hubPath = HubInterface(hubId);
        W("  Hub       : {0}, port {1}", hubId, port);
        if (hubPath == null) { W("  (hub has no device interface; cannot query descriptors)"); return; }

        using (SafeFileHandle h = CreateFile(hubPath, GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
        {
            if (h.IsInvalid) { W("  (cannot open hub: error {0}; run as administrator)", Marshal.GetLastWin32Error()); return; }

            byte[] dev = GetDescriptor(h, port, 0x80, 0x0100, 0, 18);
            if (dev == null) return;
            W("  Device descriptor: {0}", Hex(dev, 0, dev.Length));
            if (dev.Length >= 18)
                W("    USB {0:X}.{1:X2}, class {2:X2}/{3:X2}/{4:X2}, EP0 {5} bytes, VID {6:X4} PID {7:X4} rev {8:X4}, {9} configuration(s)",
                    dev[3], dev[2], dev[4], dev[5], dev[6], dev[7], BitConverter.ToUInt16(dev, 8), BitConverter.ToUInt16(dev, 10),
                    BitConverter.ToUInt16(dev, 12), dev[17]);

            byte[] head = GetDescriptor(h, port, 0x80, 0x0200, 0, 9);
            if (head == null || head.Length < 4) return;
            byte[] cfg = GetDescriptor(h, port, 0x80, 0x0200, 0, BitConverter.ToUInt16(head, 2));
            if (cfg == null) return;
            W("  Configuration descriptor ({0} bytes):", cfg.Length);
            W("    {0}", Hex(cfg, 0, cfg.Length));

            List<int[]> reports = new List<int[]>();   // {interface, length}
            int currentIf = -1;
            for (int p = 0; p + 1 < cfg.Length && cfg[p] >= 2; p += cfg[p])
            {
                int len = cfg[p], dt = cfg[p + 1];
                if (dt == 2 && len >= 9)
                    W("    Configuration: {0} interface(s), attributes 0x{1:X2}, {2} mA", cfg[p + 4], cfg[p + 7], cfg[p + 8] * 2);
                else if (dt == 4 && len >= 9)
                {
                    currentIf = cfg[p + 2];
                    W("    Interface {0} alt {1}: class {2:X2} subclass {3:X2} protocol {4:X2}, {5} endpoint(s)",
                        cfg[p + 2], cfg[p + 3], cfg[p + 5], cfg[p + 6], cfg[p + 7], cfg[p + 4]);
                }
                else if (dt == 0x21 && len >= 9)
                {
                    W("      HID {0:X}.{1:X2}, country {2}, report descriptor {3} bytes", cfg[p + 3], cfg[p + 2], cfg[p + 4], BitConverter.ToUInt16(cfg, p + 7));
                    reports.Add(new int[] { currentIf, BitConverter.ToUInt16(cfg, p + 7) });
                }
                else if (dt == 5 && len >= 7)
                    W("      Endpoint 0x{0:X2} ({1}), {2}, max packet {3}, interval {4}",
                        cfg[p + 2], (cfg[p + 2] & 0x80) != 0 ? "IN" : "OUT",
                        new string[] { "control", "isochronous", "bulk", "interrupt" }[cfg[p + 3] & 3],
                        BitConverter.ToUInt16(cfg, p + 4), cfg[p + 6]);
                else
                    W("    Descriptor type 0x{0:X2}, {1} bytes", dt, len);
            }

            foreach (int[] r in reports)
            {
                byte[] rd = GetDescriptor(h, port, 0x81, 0x2200, r[0], r[1]);
                W("");
                W("  HID report descriptor, interface {0}:", r[0]);
                if (rd == null) continue;
                W("    {0}", Hex(rd, 0, rd.Length));
                DecodeReportDescriptor(rd);
            }
        }
        W("");
    }

    static string HubInterface(string hubId)
    {
        int length;
        if (CM_Get_Device_Interface_List_Size(out length, ref GUID_DEVINTERFACE_USB_HUB, hubId, 0) != 0 || length <= 1) return null;
        char[] buf = new char[length];
        if (CM_Get_Device_Interface_List(ref GUID_DEVINTERFACE_USB_HUB, hubId, buf, length, 0) != 0) return null;
        string s = new string(buf);
        int end = s.IndexOf('\0');
        return end > 0 ? s.Substring(0, end) : null;
    }

    // Standard GET_DESCRIPTOR through IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION (read-only).
    static byte[] GetDescriptor(SafeFileHandle hub, int port, byte bmRequest, int wValue, int wIndex, int wLength)
    {
        byte[] req = new byte[12 + wLength];
        BitConverter.GetBytes(port).CopyTo(req, 0);
        req[4] = bmRequest;
        req[5] = 6;                       // GET_DESCRIPTOR
        BitConverter.GetBytes((ushort)wValue).CopyTo(req, 6);
        BitConverter.GetBytes((ushort)wIndex).CopyTo(req, 8);
        BitConverter.GetBytes((ushort)wLength).CopyTo(req, 10);
        int returned;
        if (!DeviceIoControl(hub, IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, req, req.Length, req, req.Length, out returned, IntPtr.Zero))
        {
            W("  GET_DESCRIPTOR 0x{0:X4} index {1} failed: error {2}", wValue, wIndex, Marshal.GetLastWin32Error());
            return null;
        }
        int n = Math.Max(0, returned - 12);
        byte[] data = new byte[n];
        Array.Copy(req, 12, data, 0, n);
        return data;
    }

    static void DecodeReportDescriptor(byte[] d)
    {
        string[] main = { "", "", "", "", "", "", "", "", "Input", "Output", "Collection", "Feature", "End Collection" };
        string[] global = { "Usage Page", "Logical Min", "Logical Max", "Physical Min", "Physical Max", "Unit Exponent", "Unit", "Report Size", "Report ID", "Report Count", "Push", "Pop" };
        string[] local = { "Usage", "Usage Min", "Usage Max" };
        int indent = 0;
        for (int p = 0; p < d.Length; )
        {
            byte prefix = d[p];
            if (prefix == 0xFE) { W("      (long item)"); break; }
            int size = new int[] { 0, 1, 2, 4 }[prefix & 3];
            int kind = (prefix >> 2) & 3, tag = prefix >> 4;
            uint value = 0;
            for (int i = 0; i < size && p + 1 + i < d.Length; i++) value |= (uint)d[p + 1 + i] << (8 * i);
            p += 1 + size;
            string name = kind == 0 ? (tag < main.Length ? main[tag] : "?") : kind == 1 ? (tag < global.Length ? global[tag] : "?") : (tag < local.Length ? local[tag] : "Local " + tag);
            if (kind == 0 && tag == 12) indent = Math.Max(0, indent - 1);
            string detail = String.Format("0x{0:X}", value);
            if (kind == 0 && (tag == 8 || tag == 9 || tag == 11))
                detail += ((value & 1) != 0 ? " Constant" : " Data") + ((value & 2) != 0 ? " Variable" : " Array") + ((value & 4) != 0 ? " Relative" : " Absolute");
            W("      {0}{1} ({2})", new string(' ', indent * 2), name, detail);
            if (kind == 0 && tag == 10) indent++;
        }
    }
}
'@

$ErrorActionPreference = 'Stop'
if (-not ('N52teProbe' -as [type])) { Add-Type -TypeDefinition $source -Language CSharp }

$report = [N52teProbe]::Run($Vid, $TryGet.IsPresent)
$report
try { $report | Out-File -FilePath $Out -Encoding utf8; "Saved to $Out" } catch { "Could not save to $Out : $_" }
try { $report | Set-Clipboard; 'Copied to the clipboard.' } catch { }
