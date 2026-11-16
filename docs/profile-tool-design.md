# Design: writing keymaps to the n52te from Windows 11

**Goal:** keep the n52te fully usable on Windows 11 by doing what Belkin's software did: build
the keypad's three keymaps on the PC, write them into the keypad's onboard memory, and let the
keypad's own firmware run them. Keys, mouse buttons, wheel, macros and keymap switching (with
the LED) then work on any PC without anything running in the background.

This replaces the remapper-first approach. Remapping keystrokes on the PC can't fix controls
the stored profile leaves silent, and can't change the keypad's keymap or LED (see
[original-driver-analysis.md](original-driver-analysis.md)).

## What is verified

| Piece | Status | Where |
|---|---|---|
| Read firmware version / active keymap | works on hardware | `research/n52te_keymap.py` |
| Select active keymap (LED follows, survives unplugging) | works on hardware | `research/n52te_keymap.py` |
| Read a keymap back from the keypad | works on hardware | `research/n52te_readback.py` |
| Keymap image layout, slot order, macro format | from the decompiled service + hardware read-back | [03](reverse-engineering/03-usb-protocol.md#keymap-image) |
| Chunk checksum | from the decompiled service | [03](reverse-engineering/03-usb-protocol.md#keymap-image) |
| Keymap numbers 0 = Blue, 1 = Red | hardware; 2 = Green inferred | [03](reverse-engineering/03-usb-protocol.md#verified-on-hardware-november-2026) |
| Upload (`SEND_SETTING` + `GET_RESULT`), unchanged keymap | works on hardware (round trip) | `research/n52te_upload.py --roundtrip` |
| Upload of a changed keymap (one key code) | works on hardware, keypad runs it with no PC software | `research/n52te_upload.py --set` |
| Upload of macros, mouse codes, keymap-switch codes | not yet tested | – |
| **Windows 11 transport** (no VM) | **not yet built** | – |

## Components

1. **Profile model and image builder.** Three keymaps × 27 inputs. Each input is a firmware
   code (key, mouse `D0`–`D8`, keymap switch `D9`–`DF`, media `E8`–`FF`), a macro marker plus
   macro steps, or disabled. The builder writes the 0x1C00-byte image the way `n52teHid.exe`
   does: header `1C 1B 00 01 KM`, code and option bytes (+1) per **slot**, macros converted
   like `0x4065c0` (delays folded into the previous step's flags). PC-side actions (joystick,
   launch app, window commands) aren't offered: only Belkin's service could run them.
2. **Backup first.** Before any write, read all three keymaps back and save the raw images.
   Restoring a backup is just uploading those images again (with the upload header).
3. **Upload.** Per keymap: 14 × `SET_REPORT wValue=1`, 0x200 bytes, `wIndex = KM << 8 | chunk`,
   each followed by `GET_REPORT wValue=2` and a checksum compare; retry the keymap up to 3 times
   like the original; then read it back and compare. Never send command 7 (`ENTER_ICP`, the
   firmware-update mode).
4. **Transport** (the only Windows-specific part). The keypad needs raw HID class requests that
   Windows' HID API can't send (it declares no feature reports; checked on hardware). Options:

   | Transport | Works with Memory Integrity on? | Trade-offs |
   |---|---|---|
   | Linux / VM (pyusb) | n/a | works today; needs a VM with USB passthrough |
   | UsbDk (signed, open source, last release 2020) | unknown, to test | borrows the keypad for a few seconds, no driver swap; third-party kernel driver |
   | WinUSB bound temporarily | yes (Microsoft's own driver) | needs a driver package signed by a certificate the user trusts locally; the keypad stops typing until the HID driver is restored |
   | New kernel filter like `JmtFltr` | only with Microsoft attestation signing | needs an EV code-signing certificate; not practical for this project |

   Plan: prototype on UsbDk (this PC has Memory Integrity off), test it on a machine with it
   on, and fall back to temporary WinUSB if needed. Keep the transport behind one small
   interface (`ControlIn`, `ControlOut`) so the VM path stays available for testing.
5. **Editor.** Edit Blue/Red/Green as the keypad's real keymaps, with "Read from keypad" and
   "Write to keypad". A keymap-switch action is written to all three keymaps, like the original
   editor. The remapper stays optional for things only a PC can do.

## Test plan

1. ~~Round trip: upload the Blue keymap exactly as read back (with the upload header), read it
   back, compare.~~ Done: body identical, header stored, keypad unchanged in use.
2. ~~Change one key in Blue, upload, check it on the keypad, restore the backup.~~ Done: wheel down
   set to B typed `b` on Windows; restored to the original afterwards.
3. Same two steps through the Windows transport.

## Open questions

* Meaning of option bytes p1–p3 (factory `01 01 00`; this keypad has `00 00 ff` and `ff ff ff`).
* Does command 8 ("driver mode") change anything? The original sent it on every plug-in.
* Verify keymap 2 = Green on hardware.
* Why the original editor's menu order (Blue/Green/Red) differs from the firmware numbers
  (0 = Blue, 1 = Red): find the label table in `n52teCfg.exe`.
