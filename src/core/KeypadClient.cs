// The keypad's configuration protocol on top of a transport (docs/reverse-engineering/03):
// HID class SET_REPORT / GET_REPORT with the command number in wValue.
using System;
using System.IO;
using System.Threading;

namespace N52te
{
    internal sealed class KeypadClient : IDisposable
    {
        private readonly IKeypadTransport transport;

        public KeypadClient(IKeypadTransport transport) { this.transport = transport; }

        public static KeypadClient OpenUsbDk() { return new KeypadClient(new UsbDkTransport()); }

        public IKeypadTransport Transport { get { return transport; } }

        public void Dispose() { transport.Dispose(); }

        // Command 7 (IOCTL_ENTER_ICP) would put the keypad into its firmware-update mode, the one
        // command that could leave it unusable. Every class request goes through here, so it can't
        // be sent by mistake. (Internal rather than private so the self-test can check the guard.)
        internal byte[] Control(byte requestType, byte request, ushort value, ushort index, byte[] dataOut, int lengthIn)
        {
            if ((requestType & 0x60) == 0x20 && value == 7)
                throw new InvalidOperationException("command 7 (ENTER_ICP, firmware update) is never sent");
            return transport.Control(requestType, request, value, index, dataOut, lengthIn);
        }

        // Requests that worked from Linux and Windows: interface recipient for wIndex low byte 0/1,
        // device recipient for higher chunk numbers.
        private byte[] Get(ushort value, ushort index, int length)
        {
            return Control((byte)((index & 0xFF) <= 1 ? 0xA1 : 0xA0), 0x01, value, index, null, length);
        }

        private void Set(ushort value, ushort index, byte[] data)
        {
            Control((byte)((index & 0xFF) <= 1 ? 0x21 : 0x20), 0x09, value, index, data, 0);
        }

        // 8-byte command block: 00 08 00 SUB 00 VALUE b0^b2^b4 b1^b3^b5.
        public static byte[] Block(byte sub, byte value)
        {
            byte[] b = { 0x00, 0x08, 0x00, sub, 0x00, value, 0, 0 };
            b[6] = (byte)(b[0] ^ b[2] ^ b[4]);
            b[7] = (byte)(b[1] ^ b[3] ^ b[5]);
            return b;
        }

        public static bool ChecksumOk(byte[] r)
        {
            return r.Length == 8 && r[6] == (r[0] ^ r[2] ^ r[4]) && r[7] == (r[1] ^ r[3] ^ r[5]);
        }

        public byte[] Firmware() { return Get(6, 0, 8); }

        public byte[] ProfileId() { return Get(5, 0, 8); }

        public int ActiveKeymap()
        {
            byte[] r = ProfileId();
            if (!ChecksumOk(r) || r[5] > 2) throw new IOException("unexpected GET_PROFILEID reply " + Hex.Of(r));
            return r[5];
        }

        public void SelectKeymap(int km) { Set(4, 0, Block(3, (byte)km)); }

        // SET_POLLINGRATE (9), sub-command 4: keyboard report interval in ms (8 = 125 Hz, 2 = 500 Hz,
        // 1 = 1000 Hz). Kept across unplugging; takes effect when the keypad reconnects.
        public void SetPollingInterval(byte ms)
        {
            if (ms != 1 && ms != 2 && ms != 8) throw new ArgumentException("polling interval must be 1, 2 or 8 ms");
            Set(9, 0, Block(4, ms));
        }

        // GET_POLLINGRATE (10): 00 08 01 06 03 INTERVAL x0 x1.
        public byte[] PollingRate() { return Get(10, 0, 8); }

        // Standard GET_DESCRIPTOR(CONFIGURATION), read-only.
        public byte[] ConfigurationDescriptor() { return transport.Control(0x80, 0x06, 0x0200, 0, null, 255); }

        public byte[] ReadKeymap(int km)
        {
            byte[] image = new byte[KeymapImage.Size];
            for (int chunk = 0; chunk < KeymapImage.Chunks; chunk++)
            {
                byte[] d = Get(3, (ushort)((km << 8) | chunk), KeymapImage.ChunkSize);
                if (d.Length != KeymapImage.ChunkSize) throw new IOException("chunk " + chunk + " returned " + d.Length + " bytes");
                Buffer.BlockCopy(d, 0, image, chunk * KeymapImage.ChunkSize, KeymapImage.ChunkSize);
            }
            return image;
        }

        // Like n52teHid.exe: every chunk is checked with GET_RESULT; the whole keymap is resent on a
        // mismatch, up to three attempts.
        public bool WriteKeymap(int km, byte[] image, TextWriter log)
        {
            if (!KeymapImage.IsUploadImage(image, km)) throw new ArgumentException("not an upload image for keymap " + km);
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                bool ok = true;
                for (int chunk = 0; chunk < KeymapImage.Chunks && ok; chunk++)
                {
                    byte[] data = new byte[KeymapImage.ChunkSize];
                    Buffer.BlockCopy(image, chunk * KeymapImage.ChunkSize, data, 0, KeymapImage.ChunkSize);
                    Set(1, (ushort)((km << 8) | chunk), data);
                    byte[] result = Get(2, 0, 2);
                    byte[] want = KeymapImage.ChunkChecksum(image, chunk * KeymapImage.ChunkSize, KeymapImage.ChunkSize);
                    ok = result.Length == 2 && result[0] == want[0] && result[1] == want[1];
                    if (log != null) log.WriteLine("  attempt {0} chunk {1,2}: GET_RESULT {2}, expected {3} {4}", attempt, chunk, Hex.Of(result), Hex.Of(want), ok ? "ok" : "MISMATCH");
                }
                if (ok) return true;
                Thread.Sleep(500);
            }
            return false;
        }

        // Writes, then reads back and compares. Returns null on success, else what went wrong.
        public string WriteAndVerify(int km, byte[] image, TextWriter log)
        {
            if (!WriteKeymap(km, image, log)) return "write failed after 3 attempts";
            byte[] after = ReadKeymap(km);
            int d = KeymapImage.FirstDifference(after, image);
            return d < 0 ? null : string.Format("read-back differs at 0x{0:X}: wrote {1:X2}, read {2:X2}", d, image[d], after[d]);
        }
    }
}
