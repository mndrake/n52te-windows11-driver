// The 0x1C00-byte image of one keymap, as uploaded to the keypad in 14 chunks of 0x200 bytes
// and as read back (docs/reverse-engineering/03-usb-protocol.md, "Keymap image").
using System;
using System.Collections.Generic;
using System.IO;

namespace N52te
{
    internal static class KeymapImage
    {
        public const int Size = 0x1C00;
        public const int ChunkSize = 0x200;
        public const int Chunks = Size / ChunkSize;
        public const int CodeOffset = 0x08, P1Offset = 0x23, P2Offset = 0x3E, P3Offset = 0x59, MacroOffset = 0x100;

        // Upload header, as n52teHid.exe writes it. A read-back image carries 1C 00 00 01 KM+1 instead.
        public static void SetUploadHeader(byte[] image, int km)
        {
            byte[] header = { 0x1C, (byte)KeypadLayout.InputCount, 0x00, 0x01, (byte)km, 0, 0, 0 };
            Buffer.BlockCopy(header, 0, image, 0, header.Length);
        }

        public static bool IsUploadImage(byte[] image, int km)
        {
            return image.Length == Size && image[0] == 0x1C && image[1] == KeypadLayout.InputCount && image[4] == km;
        }

        public static byte[] WithUploadHeader(byte[] source, int km)
        {
            if (source.Length != Size) throw new ArgumentException(string.Format("a keymap image is {0} bytes, not {1}", Size, source.Length));
            var image = (byte[])source.Clone();
            SetUploadHeader(image, km);
            return image;
        }

        public static byte[] Build(InputRecord[] records, int km)
        {
            if (records.Length != KeypadLayout.InputCount) throw new ArgumentException("a keymap has 27 inputs");
            var image = new byte[Size];
            SetUploadHeader(image, km);
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                var r = records[n - 1];
                int slot = KeypadLayout.Slot(n);
                image[CodeOffset + slot] = r.Code;
                image[P1Offset + slot] = (byte)(r.Options[0] + 1);
                image[P2Offset + slot] = (byte)(r.Options[1] + 1);
                image[P3Offset + slot] = (byte)(r.Options[2] + 1);
                byte[] fw = r.FirmwareSequence ?? Macro.ToFirmware(r.Sequence);
                Buffer.BlockCopy(fw, 0, image, MacroOffset + slot * 0x100, 0x100);
            }
            return image;
        }

        // Records of an image (uploaded or read back). Macros that don't convert to the profile
        // form without loss keep their firmware bytes (InputRecord.FirmwareSequence).
        public static InputRecord[] Parse(byte[] image)
        {
            if (image.Length != Size) throw new ArgumentException(string.Format("a keymap image is {0} bytes, not {1}", Size, image.Length));
            var records = new InputRecord[KeypadLayout.InputCount];
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                int slot = KeypadLayout.Slot(n);
                var r = new InputRecord
                {
                    Code = image[CodeOffset + slot],
                    Options = new[] { (byte)(image[P1Offset + slot] - 1), (byte)(image[P2Offset + slot] - 1), (byte)(image[P3Offset + slot] - 1) },
                };
                int macro = MacroOffset + slot * 0x100;
                byte[] seq = Macro.FromFirmware(image, macro);
                if (seq != null && Same(Macro.ToFirmware(seq), 0, image, macro, 0x100)) r.Sequence = seq;
                else
                {
                    r.FirmwareSequence = new byte[0x100];
                    Buffer.BlockCopy(image, macro, r.FirmwareSequence, 0, 0x100);
                }
                records[n - 1] = r;
            }
            return records;
        }

        // Bytes from CodeOffset on; the header differs between upload and read-back.
        public static int FirstDifference(byte[] a, byte[] b)
        {
            for (int i = CodeOffset; i < Size; i++) if (a[i] != b[i]) return i;
            return -1;
        }

        public static bool Same(byte[] a, int aOffset, byte[] b, int bOffset, int count)
        {
            for (int i = 0; i < count; i++) if (a[aOffset + i] != b[bOffset + i]) return false;
            return true;
        }

        // What GET_RESULT returns after a chunk: XOR of the even-offset bytes, XOR of the odd ones.
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

        public static void Describe(string title, InputRecord[] records, TextWriter o)
        {
            o.WriteLine("== " + title);
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                var r = records[n - 1];
                o.WriteLine("  {0,2} {1,-22} {2:X2}  {3}", n, KeypadLayout.InputNames[n - 1], r.Code, DescribeRecord(n, r));
            }
        }

        public static string DescribeRecord(int input, InputRecord r)
        {
            if (r.Code == FirmwareCodes.Disabled) return "disabled";
            if (r.Code == KeypadLayout.PcActionMarker[input - 1])
                return string.Format("PC-side action type 0x{0:X2} (needs Belkin's service; does nothing on Windows 11)", r.FirmwareSequence != null ? r.FirmwareSequence[0] : r.Sequence[0]);
            if (r.Code == KeypadLayout.MacroMarker[input - 1])
            {
                if (r.FirmwareSequence != null) return "macro (raw): " + Hex.Of(r.FirmwareSequence, 0, 16) + " ...";
                if (r.Sequence[0] == 0) return "empty macro (does nothing)";
                return "macro, " + InputRecord.DescribeOptions(r.Options) + ": " + Macro.Describe(r.Sequence);
            }
            string what = FirmwareCodes.Name(r.Code);
            if (FirmwareCodes.IsKeymapSwitch(r.Code)) what = "KEYMAP " + what;
            else if (FirmwareCodes.IsCommand(r.Code)) what += " (command; sends nothing on Windows 11)";
            return what;
        }

        public static void Describe(int km, byte[] image, TextWriter o)
        {
            Describe(string.Format("Keymap {0} ({1}), header {2}", km, KeypadLayout.KeymapNames[km], Hex.Of(image, 0, 5)), Parse(image), o);
        }

        public static List<string> Warnings(InputRecord[] records)
        {
            var w = new List<string>();
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
                if (records[n - 1].FirmwareSequence != null && records[n - 1].Code == KeypadLayout.MacroMarker[n - 1])
                    w.Add(KeypadLayout.InputNames[n - 1] + ": macro bytes kept as they are (not in the usual form)");
            return w;
        }
    }
}
