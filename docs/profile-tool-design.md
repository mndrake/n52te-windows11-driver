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
| Upload of macros, mouse codes, keymap-switch codes | works on hardware (17–18 Nov 2026) | [04](reverse-engineering/04-onboard-profile-and-codes.md#verified-on-hardware-17-nov-2026) |
| Windows 11 transport (UsbDk, no VM): info, read, round-trip write, select keymap | works on hardware (Memory Integrity off) | `src/configtool/N52teConfig.cs` (`n52te-config.exe`) |
| UsbDk with Memory Integrity **on** | works on hardware (18 Nov 2026): `info`, `read all`, `write` and `select` as a normal user, no admin; Windows Security doesn't list `UsbDk.sys` as incompatible | `n52te-config.exe` |

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
   | UsbDk (signed, open source, last release 2020) | yes (tested: read-only commands) | borrows the keypad for a few seconds, no driver swap; third-party kernel driver, registered as a filter on the whole USB class |
   | WinUSB bound temporarily | yes (Microsoft's own driver) | needs a driver package signed by a certificate the user trusts locally; the keypad stops typing until the HID driver is restored |
   | New kernel filter like `JmtFltr` | only with Microsoft attestation signing | needs an EV code-signing certificate; not practical for this project |

   Plan: UsbDk, which now works with Memory Integrity both off and on; temporary WinUSB stays
   the fallback. Keep the transport behind one small
   interface (`ControlIn`, `ControlOut`) so the VM path stays available for testing.
5. **Editor.** Edit Blue/Red/Green as the keypad's real keymaps, with "Read from keypad" and
   "Write to keypad". A keymap-switch action is written to all three keymaps, like the original
   editor. (The remapper was removed from the project on 18 Nov 2026.)

## Using `n52te-config` on Windows

Requires [UsbDk 1.0.22 x64](https://github.com/daynix/UsbDk/releases) (signed by Red Hat). The
installer puts `n52te-config.exe` in `C:\Program Files\n52te` and adds a Start-menu shortcut,
**n52te Keymaps (command prompt)**, that opens a prompt with the tool on its `PATH`. It doesn't
bundle UsbDk; it logs whether UsbDk is installed. `build.ps1` compiles the tool; on its own:

```
csc /platform:x64 /r:System.dll /r:System.Core.dll /out:n52te-config.exe src\core\*.cs src\configtool\*.cs
n52te-config selftest [path\to\Belkin\User_Profiles\Default.ini]
```

The keypad code lives in `src\core` (transport, protocol, keymap image, profile, `.ini` files,
code tables), shared with the editor to come; `src\configtool` is the command-line front end and
the self-test.

| Command | What it does |
|---|---|
| `n52te-config info` | firmware version and active keymap (read-only) |
| `n52te-config select blue\|red\|green` | make that keymap active; the LED follows and it stays after unplugging |
| `n52te-config read [0\|1\|2\|all] [DIR]` | read keymaps back, decode them, save `keymapN.bin` (read-only) |
| `n52te-config roundtrip 0\|1\|2` | write a keymap back unchanged and verify it |
| `n52te-config set KM INPUT CODE --yes` | change one input to a firmware code (hex), verify |
| `n52te-config write FILE KM --yes` | write a saved image (e.g. a backup) as keymap KM, verify |
| `n52te-config polling [125\|500\|1000]` | set the keyboard side's USB polling rate (kept after unplugging, effective after replugging); without a rate, only shows the current one |
| `n52te-config descriptor` | show the USB poll interval of each endpoint (read-only) |
| `n52te-config listen [SECONDS]` | print the keypad's raw key and mouse reports (diagnostic; the keypad types nothing meanwhile) |
| `n52te-config export FILE.ini [NAME]` | save the three keymaps and the active keymap as a Belkin v2.1.2 profile (read-only) |
| `n52te-config apply FILE.ini --yes` | write a Belkin profile: only the keymaps that differ, then select its start keymap; `--dry-run` lists the changes and writes nothing |
| `n52te-config macro KM INPUT "STEPS" [--mode once\|held\|untilnext] [--times N] [--interval MS] --yes` | put a macro on one input; steps like `"H I"`, `"+LShift A -LShift"`, `"A 1000ms B"`, `"Mouse_Left"` |
| `n52te-config show FILE` | decode a keymap image (`.bin`) or a profile (`.ini`) without the keypad |
| `n52te-config selftest [DEFAULT.INI]` | check the tool against known data and a simulated keypad, without the keypad |

The tool refuses command 7 (`ENTER_ICP`, firmware update) whatever it is asked to do.

Every write first saves `keymapN-before-<time>.bin` in `Documents\n52te keymaps` (also where
`read` saves images unless given a folder), checks each chunk's
checksum, and reads the keymap back to verify. The keypad doesn't type while the tool runs;
UsbDk hands it back to Windows when the tool exits.

**Restart Windows after installing UsbDk.** Installing it attaches a filter to every USB device and
restarts the USB stacks. On the test PC that also restarted the (USB) Bluetooth adapter twice,
and the Bluetooth keyboard stopped reaching apps until a reboot. Using the tool afterwards
(borrowing and returning the keypad) did not affect the Bluetooth keyboard.

## Test plan

1. ~~Round trip: upload the Blue keymap exactly as read back (with the upload header), read it
   back, compare.~~ Done: body identical, header stored, keypad unchanged in use.
2. ~~Change one key in Blue, upload, check it on the keypad, restore the backup.~~ Done: wheel down
   set to B typed `b` on Windows; restored to the original afterwards.
3. ~~Same steps through the Windows transport.~~ Done with `n52te-config` over UsbDk: `info`,
   `read all` (identical to the VM read-back), `roundtrip blue` (verified) and `select red` /
   `select blue` (LED followed).
4. ~~UsbDk with Memory Integrity on.~~ Done on 18 Nov 2026: `info` and `read all` work as a
   normal user; Blue read back identical to the 16 Nov image. To turn Memory Integrity on, the
   test PC first needed Interception's `keyboard.sys` / `mouse.sys` (each has a writable and
   executable `INIT` section) and some unrelated old Razer, Epson and Logitech drivers removed;
   the incompatible-driver list only refreshed after a restart.
5. ~~Mouse, media, command and keymap-switch codes.~~ Done on 17 Nov 2026 on Red and Green, then
   restored from backups; results in
   [04](reverse-engineering/04-onboard-profile-and-codes.md#verified-on-hardware-17-nov-2026).
   Mouse, media and keymap-switch codes work from the firmware; the commands (`C0`–`CE`) send
   nothing.
6. ~~Macros, their option bytes, and writing with Memory Integrity on.~~ Done on 18 Nov 2026:
   three verified writes to Red with Memory Integrity on (two macro tests and the restore);
   results in [04](reverse-engineering/04-onboard-profile-and-codes.md#macros-18-nov-2026).
7. ~~The polling rate, mouse steps inside a macro, the remaining media codes.~~ Done on 18 Nov
   2026 ([04](reverse-engineering/04-onboard-profile-and-codes.md#media-codes-18-nov-2026)). Phase 0
   is complete except command 8, which nothing needs. The first macro attempt (17 Nov) was void: from about 17:30 nothing from the keypad reached
   Windows, not even a plain key in the untouched Blue keymap, although a raw capture over UsbDk
   showed the keypad sending the Blue layout correctly. The cause wasn't found. One more fault
   happened on the way: an early version of `n52te-config listen` left the keypad redirected to
   UsbDk until it was replugged (fixed). The next day a stale Interception entry in the keyboard
   class filters stopped every keyboard on the PC, which may be the same fault.

## Open questions

* Meaning of option bytes p1–p3 (factory `01 01 00`; this keypad has `00 00 ff` and `ff ff ff`).
* Does command 8 ("driver mode") change anything? The original sent it on every plug-in.
* Verify keymap 2 = Green on hardware.
* Why the original editor's menu order (Blue/Green/Red) differs from the firmware numbers
  (0 = Blue, 1 = Red): find the label table in `n52teCfg.exe`.
