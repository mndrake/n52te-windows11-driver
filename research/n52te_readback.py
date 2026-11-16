#!/usr/bin/env python3
"""Read a keymap back from the n52te's onboard memory and decode it. Read-only.

Uses GET_GROUP_SETTING from docs/reverse-engineering/03-usb-protocol.md:
GET_REPORT (bmRequestType 0xA1, bRequest 0x01), wValue 3, wIndex = keymap << 8 | chunk,
0x200 bytes per chunk, 14 chunks = the 0x1C00-byte keymap image:

  0x000  1C 1B 00 01 KM            header
  0x008  27 bytes                  firmware code of each input
  0x023 / 0x03E / 0x059            option bytes p1/p2/p3 of each input (+1)
  0x100 + n*0x100                  macro of input n (128 x [code][flags])

The original service never sent this request, so it is untested apart from this tool.
Linux treats the low byte of wIndex as an interface number for interface-recipient
requests, so chunks >= 2 are retried with a device-recipient request (0xA0).

Keymap numbers are the firmware's: 0 = Blue, 1 = Red, 2 = Green (see n52te_keymap.py).
Raw images are saved as keymap<N>.bin next to this script.

Needs python3, pyusb and root (Linux). Usage: n52te_readback.py [0|1|2 ...]   (default 0)
"""
import os
import sys

import usb.core
import usb.util

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from decode_n52te_profile import CODES, INPUTS, MACRO_MARK, PC_MARK, PC_TYPES  # noqa: E402

VID, PID = 0x050D, 0x0200
FIRMWARE_KEYMAPS = {0: "Blue", 1: "Red", 2: "Green"}
CHUNK, CHUNKS = 0x200, 14


def name(code):
    return CODES.get(code, "0x%02X" % code)


def read_image(dev, km):
    image, notes = bytearray(), []
    for chunk in range(CHUNKS):
        windex = (km << 8) | chunk
        data = None
        for rtype in (0xA1, 0xA0):
            try:
                data = bytes(dev.ctrl_transfer(rtype, 0x01, 3, windex, CHUNK, timeout=2000))
                if rtype != 0xA1:
                    notes.append("chunk %d read with device recipient" % chunk)
                break
            except (usb.core.USBError, ValueError) as e:
                last = e
        if data is None:
            notes.append("chunk %d failed: %s" % (chunk, last))
            break
        if len(data) != CHUNK:
            notes.append("chunk %d returned %d bytes" % (chunk, len(data)))
        image += data
    return bytes(image), notes


def decode(km, image):
    # The image stores inputs in key-matrix order, not in input order: the slot of an input
    # is its macro marker minus 0xA0 (verified on hardware: decoding this way reproduces the
    # keys each control actually sends). The same slot indexes its macro at 0x100 + slot*0x100.
    print("\n== Keymap %d (%s): %d bytes read, header %s" % (km, FIRMWARE_KEYMAPS.get(km, "?"), len(image), image[:5].hex(" ")))
    if len(image) < 0x74:
        print("   too short to decode")
        return
    for i, label in enumerate(INPUTS):
        slot = MACRO_MARK[i] - 0xA0
        code = image[0x08 + slot]
        opts = bytes(image[o + slot] - 1 & 0xFF for o in (0x23, 0x3E, 0x59))
        start = 0x100 + slot * 0x100
        seq = image[start:start + 0x100] if len(image) >= start + 0x100 else b""
        if code == PC_MARK[i]:
            what = "PC-side action: %s (param %d)" % (PC_TYPES.get(seq[0], "type 0x%02X" % seq[0]), seq[1]) if seq else "PC-side action (macro chunk not read)"
        elif code == 0:
            what = "disabled"
        elif code == MACRO_MARK[i]:
            if seq:
                steps = []
                for j in range(0, 0x100, 2):
                    c, f = seq[j], seq[j + 1]
                    if c == 0 and f == 0:
                        break
                    steps.append(("+" if f & 1 else "-") + name(c) + ("(%dms)" % ((f >> 1) * 50) if f >> 1 else ""))
                what = "macro: " + " ".join(steps)
            else:
                what = "macro (macro chunk not read)"
        else:
            what = name(code)
        print("  %2d %-22s slot %2d  %-4s %s   opts=%s" % (i + 1, label, slot, "%02X" % code, what, opts.hex()))


def main(argv):
    keymaps = [int(a) for a in argv] or [0]
    dev = usb.core.find(idVendor=VID, idProduct=PID)
    if dev is None:
        sys.exit("n52te (050d:0200) not found. Is it attached to this machine/VM?")
    detached = []
    for i in (0, 1):
        try:
            if dev.is_kernel_driver_active(i):
                dev.detach_kernel_driver(i)
                detached.append(i)
        except (usb.core.USBError, NotImplementedError) as e:
            print("  (could not detach kernel driver from interface %d: %s)" % (i, e))
    try:
        for km in keymaps:
            image, notes = read_image(dev, km)
            path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "keymap%d.bin" % km)
            with open(path, "wb") as f:
                f.write(image)
            for n in notes:
                print("  note: " + n)
            print("  saved %s" % path)
            decode(km, image)
    finally:
        usb.util.dispose_resources(dev)
        for i in detached:
            try:
                dev.attach_kernel_driver(i)
            except usb.core.USBError as e:
                print("  (could not reattach kernel driver to interface %d: %s)" % (i, e))


if __name__ == "__main__":
    main(sys.argv[1:])
