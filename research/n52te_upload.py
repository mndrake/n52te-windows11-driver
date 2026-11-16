#!/usr/bin/env python3
"""Write a keymap image into the n52te's onboard memory. EXPERIMENTAL: this writes to the keypad.

Follows the upload routine of Belkin's n52teHid.exe (0x406250, see
docs/reverse-engineering/03-usb-protocol.md):

  for chunk in 0..13:
      SET_REPORT (bmRequestType 0x21, bRequest 0x09), wValue 1, wIndex = KM << 8 | chunk,
                 0x200 bytes of the 0x1C00-byte image
      GET_REPORT (0xA1, 0x01), wValue 2, wIndex 0, 2 bytes -> must equal the chunk checksum
                 (XOR of even-offset bytes, XOR of odd-offset bytes)
  on a checksum mismatch the whole keymap is sent again, up to 3 attempts (like the original)

The image header is always rewritten to the upload form 1C 1B 00 01 KM 00 00 00, because the
keypad reports a different header on read-back (1C 00 00 01 KM+1).

Linux refuses interface-recipient requests whose wIndex low byte isn't a claimed interface, so
chunks >= 2 go out as device-recipient requests (0x20); the firmware reads the chunk number from
wIndex either way (verified for read-back).

Modes:
  --roundtrip KM          read keymap KM, upload exactly that, read it back and compare.
                          Changes nothing on the keypad if the upload path works.
  --image FILE KM         upload FILE (0x1C00 bytes) as keymap KM, then read back and compare.
                          Refuses unless --yes is given.
  --set KM INPUT CODE     read keymap KM, set input INPUT (1..27, see decode_n52te_profile.py)
                          to firmware code CODE (hex, e.g. 2F = B), upload, read back and
                          compare. Only the code byte changes. Refuses unless --yes is given.

Keymap numbers are the firmware's: 0 = Blue, 1 = Red, 2 = Green. Never sends command 7.
Needs python3, pyusb, root (Linux). Raw images are saved next to this script.
"""
import os
import sys
import time

import usb.core
import usb.util

VID, PID = 0x050D, 0x0200
CHUNK, CHUNKS = 0x200, 14
SIZE = CHUNK * CHUNKS
HERE = os.path.dirname(os.path.abspath(__file__))


def checksum(data):
    x0 = x1 = 0
    for i in range(0, len(data), 2):
        x0 ^= data[i]
        if i + 1 < len(data):
            x1 ^= data[i + 1]
    return bytes([x0, x1])


def upload_header(image, km):
    return bytes([0x1C, 0x1B, 0x00, 0x01, km, 0, 0, 0]) + image[8:]


class Keypad:
    def __init__(self):
        self.dev = usb.core.find(idVendor=VID, idProduct=PID)
        if self.dev is None:
            sys.exit("n52te (050d:0200) not found. Is it attached to this machine/VM?")
        self.detached = []
        for i in (0, 1):
            try:
                if self.dev.is_kernel_driver_active(i):
                    self.dev.detach_kernel_driver(i)
                    self.detached.append(i)
            except (usb.core.USBError, NotImplementedError) as e:
                print("  (could not detach kernel driver from interface %d: %s)" % (i, e))

    def close(self):
        usb.util.dispose_resources(self.dev)
        for i in self.detached:
            try:
                self.dev.attach_kernel_driver(i)
            except usb.core.USBError as e:
                print("  (could not reattach kernel driver to interface %d: %s)" % (i, e))

    def _xfer(self, rtype_iface, rtype_dev, request, value, windex, data_or_len):
        try:
            return self.dev.ctrl_transfer(rtype_iface, request, value, windex, data_or_len, timeout=3000)
        except (usb.core.USBError, ValueError) as e:
            if windex & 0xFF in (0, 1):
                raise
            return self.dev.ctrl_transfer(rtype_dev, request, value, windex, data_or_len, timeout=3000)

    def read_keymap(self, km):
        image = bytearray()
        for chunk in range(CHUNKS):
            image += bytes(self._xfer(0xA1, 0xA0, 0x01, 3, (km << 8) | chunk, CHUNK))
        return bytes(image)

    def write_keymap(self, km, image, attempts=3):
        assert len(image) == SIZE and image[:5] == bytes([0x1C, 0x1B, 0x00, 0x01, km])
        for attempt in range(1, attempts + 1):
            ok = True
            for chunk in range(CHUNKS):
                data = image[chunk * CHUNK:(chunk + 1) * CHUNK]
                n = self._xfer(0x21, 0x20, 0x09, 1, (km << 8) | chunk, data)
                result = bytes(self.dev.ctrl_transfer(0xA1, 0x01, 2, 0, 2, timeout=3000))
                want = checksum(data)
                print("  attempt %d chunk %2d: sent %d bytes, GET_RESULT %s, expected %s %s"
                      % (attempt, chunk, n, result.hex(" "), want.hex(" "), "ok" if result == want else "MISMATCH"))
                if result != want:
                    ok = False
                    break
            if ok:
                return True
            time.sleep(0.5)
        return False


def save(name, data):
    path = os.path.join(HERE, name)
    with open(path, "wb") as f:
        f.write(data)
    print("  saved " + path)


def compare(before, after):
    if before[8:] == after[8:]:
        print("RESULT: keymap body identical after the upload (header before %s, after %s)" % (before[:8].hex(" "), after[:8].hex(" ")))
        return True
    diffs = [i for i in range(8, SIZE) if before[i] != after[i]]
    print("RESULT: %d bytes differ, first at 0x%X: before %02X after %02X" % (len(diffs), diffs[0], before[diffs[0]], after[diffs[0]]))
    return False


def main(argv):
    set_input = None
    if len(argv) >= 2 and argv[0] == "--roundtrip":
        km, image_file = int(argv[1]), None
    elif len(argv) >= 3 and argv[0] == "--image":
        image_file, km = argv[1], int(argv[2])
        if "--yes" not in argv:
            sys.exit("refusing to write %s without --yes" % image_file)
    elif len(argv) >= 4 and argv[0] == "--set":
        km, image_file = int(argv[1]), None
        set_input, set_code = int(argv[2]), int(argv[3], 16)
        if not 1 <= set_input <= 27 or not 0 <= set_code <= 0xFF:
            sys.exit("INPUT must be 1..27 and CODE 00..FF")
        if "--yes" not in argv:
            sys.exit("refusing to change the keypad without --yes")
    else:
        sys.exit(__doc__)
    if km not in (0, 1, 2):
        sys.exit("keymap must be 0, 1 or 2")

    pad = Keypad()
    try:
        r = bytes(pad.dev.ctrl_transfer(0xA1, 0x01, 5, 0, 8, timeout=3000))
        print("active keymap before: %d (reply %s)" % (r[5], r.hex(" ")))
        before = pad.read_keymap(km)
        save("upload-before-km%d.bin" % km, before)
        if image_file:
            with open(image_file, "rb") as f:
                source = f.read()
            if len(source) != SIZE:
                sys.exit("%s is %d bytes, expected %d" % (image_file, len(source), SIZE))
        else:
            source = before
        if set_input:
            sys.path.insert(0, HERE)
            from decode_n52te_profile import INPUTS, MACRO_MARK, name
            slot = MACRO_MARK[set_input - 1] - 0xA0
            old = source[0x08 + slot]
            source = source[:0x08 + slot] + bytes([set_code]) + source[0x09 + slot:]
            print("setting input %d (%s, slot %d): %02X (%s) -> %02X (%s)"
                  % (set_input, INPUTS[set_input - 1], slot, old, name(old), set_code, name(set_code)))
        image = upload_header(source, km)
        print("uploading keymap %d: header %s, body checksum %s" % (km, image[:8].hex(" "), checksum(image).hex(" ")))
        if not pad.write_keymap(km, image):
            print("RESULT: upload failed (checksum mismatch after 3 attempts); keymap may be partly written")
            return 1
        time.sleep(0.5)
        after = pad.read_keymap(km)
        save("upload-after-km%d.bin" % km, after)
        same = compare(before if not (image_file or set_input) else source, after)
        r = bytes(pad.dev.ctrl_transfer(0xA1, 0x01, 5, 0, 8, timeout=3000))
        print("active keymap after: %d (reply %s)" % (r[5], r.hex(" ")))
        return 0 if same else 2
    finally:
        pad.close()


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
