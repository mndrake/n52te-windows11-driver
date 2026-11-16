#!/usr/bin/env python3
"""Read or select the n52te's active onboard keymap (and so its LED colour).

Written from docs/reverse-engineering/03-usb-protocol.md, not from Belkin code. The
protocol there comes from static analysis only, so treat this as an experiment:

  GET_FWVERSION   GET_REPORT  (bmRequestType 0xA1, bRequest 0x01), wValue 6, 8 bytes
  GET_PROFILEID   GET_REPORT, wValue 5, 8-byte reply, byte 5 = active keymap
  SET_PROFILEID   SET_REPORT  (bmRequestType 0x21, bRequest 0x09), wValue 4,
                  data 00 08 00 03 00 KM (b0^b2^b4) (b1^b3^b5)

Tested on a real n52te (firmware reply 00 08 01 04 65 00 64 0c) through an Alpine Linux
VM: both GET requests and SET_PROFILEID work on interface 0, and the LED follows.
Keymap numbers on the hardware: 0 = Blue (verified), 1 = Red (verified: the keypad
reported 1 while its LED was red), 2 = Green (inferred). The original editor's menu
order (Blue/Green/Red) does NOT match the firmware numbers. The selected keymap is kept
across unplugging (the keypad came back Blue after a replug).
Nothing is written unless you name a keymap.

Needs Linux (or another OS where libusb can send raw control transfers to an HID
device), python3 and pyusb, and root. The kernel's usbhid driver is detached from
the keypad while this runs and reattached at the end.

Usage:  n52te_keymap.py [read | blue | green | red | 0 | 1 | 2] [--interface N]
"""
import sys

import usb.core
import usb.util

VID, PID = 0x050D, 0x0200
NAMES = {0: "Blue", 1: "Red", 2: "Green"}  # firmware numbers; 2 = Green is inferred


def block(sub, value):
    """8-byte command block: 00 08 00 SUB 00 VALUE x0 x1."""
    b = [0x00, 0x08, 0x00, sub, 0x00, value]
    return bytes(b + [b[0] ^ b[2] ^ b[4], b[1] ^ b[3] ^ b[5]])


def checksum_ok(r):
    return len(r) == 8 and r[6] == r[0] ^ r[2] ^ r[4] and r[7] == r[1] ^ r[3] ^ r[5]


def main(argv):
    action, iface = "read", 0
    args = list(argv)
    if "--interface" in args:
        i = args.index("--interface")
        iface = int(args[i + 1])
        del args[i:i + 2]
    if args:
        action = args[0].lower()
    target = None
    if action in ("blue", "green", "red"):
        target = {"blue": 0, "red": 1, "green": 2}[action]
    elif action in ("0", "1", "2"):
        target = int(action)
    elif action != "read":
        sys.exit(__doc__)

    dev = usb.core.find(idVendor=VID, idProduct=PID)
    if dev is None:
        sys.exit("n52te (050d:0200) not found. Is it attached to this machine/VM?")
    print("found n52te: bus %s address %s, interface used for requests: %d" % (dev.bus, dev.address, iface))

    detached = []
    for i in (0, 1):
        try:
            if dev.is_kernel_driver_active(i):
                dev.detach_kernel_driver(i)
                detached.append(i)
        except (usb.core.USBError, NotImplementedError) as e:
            print("  (could not detach kernel driver from interface %d: %s)" % (i, e))

    def get(cmd):
        return bytes(dev.ctrl_transfer(0xA1, 0x01, cmd, iface, 8, timeout=1000))

    def show(label, r):
        print("%-14s %s  %s" % (label, r.hex(" "), "checksum ok" if checksum_ok(r) else "checksum MISMATCH"))

    try:
        try:
            show("firmware:", get(6))
        except usb.core.USBError as e:
            print("firmware:      request failed: %s" % e)
        r = get(5)
        show("profile id:", r)
        print("active keymap: %d (%s)" % (r[5], NAMES.get(r[5], "unknown")))
        if target is not None:
            data = block(3, target)
            print("sending SET_PROFILEID %s -> keymap %d (%s)" % (data.hex(" "), target, NAMES[target]))
            dev.ctrl_transfer(0x21, 0x09, 4, iface, data, timeout=1000)
            r = get(5)
            show("profile id:", r)
            print("active keymap now: %d (%s)" % (r[5], NAMES.get(r[5], "unknown")))
    finally:
        usb.util.dispose_resources(dev)
        for i in detached:
            try:
                dev.attach_kernel_driver(i)
            except usb.core.USBError as e:
                print("  (could not reattach kernel driver to interface %d: %s)" % (i, e))


if __name__ == "__main__":
    main(sys.argv[1:])
