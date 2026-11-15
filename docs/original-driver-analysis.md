# Original Belkin n52te driver (v2.1.2): how it works and why the wheel, mouse buttons and keymap LEDs fail on Windows 11

This is a reverse-engineering write-up of Belkin's last Windows package for the n52te
(`n52te_win_driver_v2.1.2.exe`, July 2008, XP/Vista 32/64-bit). It was done to explain three
symptoms seen with this project on Windows 11:

* the **scroll wheel** (and its click) can't be used/remapped,
* **mouse-button functions** ("Left button") don't behave like they did with the Belkin software,
* the **keymap LEDs** (Blue / Green / Red) never change.

Everything below comes from static analysis only (Ghidra 11.1.2 / radare2 on the binaries, plus
decompiling this project's own `Instalar-n52te.exe` payload). No hardware was attached, so the
items marked *unverified* need a USB capture or a test on a real keypad.

## TL;DR

The n52te is not "a keyboard with a driver". It is a **composite USB device with its own
firmware and onboard memory**. The wheel, the mouse functions and keymap switching (with its
LED colour) are **executed by the keypad's firmware**, using the profile stored in its onboard
("Synapse") memory. The PC never sends an "LED on" command.

| Symptom | Root cause |
|---|---|
| Scroll wheel / wheel click | They are inputs 16–18 of the firmware and come out of the **second USB interface (MI_01), a HID mouse**. This project's remapper only opens **keyboards** in Interception (`interception_set_filter(ctx, interception_is_keyboard, …)`), and the editor/INI only know 20 keyboard inputs. The wheel is never seen, so it can't be remapped. |
| Mouse buttons ("Left button") | In the original software "Left/Right/Middle Mouse Button", "Scroll up/down" and mouse-axis moves are **firmware key codes `0xD0`–`0xD8`** stored in the onboard profile, and the keypad sends them as real mouse reports on MI_01. Nothing in the new stack can write the onboard profile. It can only fake a click with `SendInput` from a *keyboard* event (see note below). |
| Keymap LEDs | LED colour = the firmware's **active keymap**. It changes only when (a) an input whose onboard code is `0xD9`–`0xDF` (Cycle / Blue / Green / Red, toggle or momentary) is pressed, or (b) the host sends HID class request SET_REPORT `wValue=4` with the keymap number. The factory profile has **no keymap key**, and the new stack has **no channel to the device**, so the LED stays on the default keymap (Blue). |

Also: if an old onboard profile (programmed years ago with the Belkin editor) assigns an input
to a **PC-side action** (DirectInput joystick button/axis, Load Profile, Launch App, minimise /
maximise, Win+E, media-player control), the firmware reports that input as keyboard usage
**`0xC4 + input#` (`0xC5`–`0xDF`)**. Only the old `JmtFltr.sys` + `n52teHid.exe` understood
those. Windows' `kbdhid` has no mapping for them, so those inputs look **dead** on Windows 11
and Interception never even receives a keystroke for them (so "Learn keys" can't catch them
either).

## 1. What is in the package

Get it from the Internet Archive mirror `n52te_win_driver_v212` (zip MD5
`8ebe1b9ec204d535d750442bb05aa83c`). The archive.org snapshot of Belkin's support page
(article 4639) links to the same file. Unpack without running anything:

```sh
python3 research/extract_belkin_package.py n52te_win_driver_v2.1.2.exe out/
unshield -d payload x out/n52te_win_driver_v2.1.2/data1.cab
```

Relevant files in the InstallShield payload:

| File | What it is |
|---|---|
| `INF_XP64/JmtFlter.inf`, `WDM_XP64/JmtFltr.sys` (x64), `WDM_NT_LOW/JmtFltr.sys` (x86, built with debug prints) | "Jamantha" USB **lower filter** installed under `hidusb` on **both** `USB\VID_050D&PID_0200&MI_00` and `…&MI_01` (`HKR,,"LowerFilters",…,"JmtFltr"`). Exposes `\\.\JmtFilter`. Source paths in the binary: `e:\keyboard_ap\razer\jamantha\driver\usb-tara\{usb,pnp,usbfilter}.c`. |
| `App_Executables/n52teHid.exe` | Background service (originally `razerhid.exe`). Talks to `\\.\JmtFilter`, uploads profiles to the keypad, performs PC-side actions. |
| `App_Ex_Kar/n52teCfg.exe` | The editor (MFC). |
| `App_Ex_Kar/OSD.exe`, `n52teTra.exe`, `KBHook.dll` | On-screen display, tray icon, keyboard hook helper. |
| `JoyStickX86|X64/vhidmini.sys` | Root-enumerated **virtual HID joystick** used for "Di Button / Di Axis" actions. |
| `INF_ICP/USBICP.INF`, `WDM_ICP/Usbicp.sys` | Firmware-update (ICP) driver for the keypad's bootloader, `USB\VID_0425&PID_FF07` ("Freescale USB JW32 ICP Device"). |
| `User_Profiles/Default.ini` | Factory profile (decoded in §4). |

The manual's "Installation Verification" confirms the composite layout. After installing,
Device Manager shows *"Nostromo n52te HID SpeedPad 2.0"* **and** *"… SpeedPad 2.0 Mouse Wheel"*,
plus a *HID Keyboard Device* **and** a *HID-compliant mouse*.

## 2. Architecture of the original stack

```
 n52teCfg.exe (editor) ──writes profile──▶ n52teHid.exe ──DeviceIoControl──▶ \\.\JmtFilter
                                             ▲    │                              │
            WT_READ events (button n, plug)  │    │ virtual joystick reports     │ URB: HID class
                                             │    ▼                              ▼ SET/GET_REPORT
                                          vhidmini.sys                hidusb ─ JmtFltr ─ usbccgp ─ n52te
                                                                       (MI_00 keyboard, MI_01 mouse)
```

`JmtFltr.sys` does two jobs:

1. **Configuration channel.** It builds `URB_FUNCTION_CLASS_INTERFACE` control transfers
   (`bRequest` 9 = SET_REPORT or 1 = GET_REPORT) and sends them down with
   `IOCTL_INTERNAL_USB_SUBMIT_URB`. `wValue` is not a normal report type/ID but a **command
   number 1–10** (high byte 0).
2. **Input hook.** It sets a completion routine on interrupt-IN URBs. On every 8-byte boot-keyboard
   report, if the first key byte is in **`0xC5`–`0xDF`**, it zeroes that byte (so Windows never
   sees it) and signals user mode with event `byte - 0xC4` (1–27 = input number). An all-zero
   report raises event `0xFF99` (release). Device start/stop raise `0xBD` / `0xBE`.
   *Verified identical in the x86 and x64 builds (`cmp 0xc5`, `cmp 0xdf`, `sub 0xc4`).*

`n52teHid.exe` reacts to those events:

* `0xBD` (keypad plugged): waits 8 s, sends `IOCTL_SET_DRIVER_MODE`, starts the tray icon.
* `1..27` (input n pressed): looks up the action type of input n in the active keymap and runs it:
  type 1/2/3 = virtual-joystick button / axis min / axis max; 4 = load profile (uploads all three
  keymaps, shows OSD); 5 = launch program; 6–9 = joystick diagonals; `0x10`/`0x11` =
  minimise/maximise the foreground window; `0x12` = Win+E; `0x13`–`0x15` = further commands (`0x15` drives
  media players: WMP / iTunes / Winamp). `0xFF99` releases the joystick.
* It also writes joystick reports to the HID device with Usage Page 1 / Usage 4 (the
  `vhidmini` joystick) with `WriteFile`.

Everything else (single keys, macros, **mouse buttons, wheel, mouse movement, keymap
switching**) never reaches the PC as an "action". The firmware runs it.

## 3. Device protocol (HID class requests on the interface)

All requests are `bmRequestType = 0x21` (OUT) / `0xA1` (IN), `bRequest = 0x09` (SET_REPORT) /
`0x01` (GET_REPORT), `wValue = command`:

| IOCTL (`\\.\JmtFilter`) | Name in debug strings | Request | `wValue` | `wIndex` | Data |
|---|---|---|---|---|---|
| `0x22201C` | `IOCTL_SEND_SETTING` | SET | 1 | `(keymap << 8) \| chunk#` | 0x200-byte chunk of a keymap (below) |
| `0x22240C` | `IOCTL_GET_RESULT` | GET | 2 | 0 | 2 bytes: XOR of even bytes, XOR of odd bytes of the last chunk |
| `0x222414` | `IOCTL_GET_GROUP_SETTING` | GET | 3 | `(keymap << 8) \| chunk#` | read back a chunk |
| `0x222418` | `IOCTL_SET_PROFILEID` | SET | 4 | 0 | 8 bytes `00 08 00 03 00 KM X0 X1` (selects active keymap → LED) |
| `0x22241C` | `IOCTL_GET_PROFILEID` | GET | 5 | 0 | 8 bytes, byte 5 = active keymap |
| `0x222440` | `IOCTL_GET_RESULT` (2nd) | GET | 6 | 0 | status |
| `0x222450` | `IOCTL_ENTER_ICP` | SET | 7 | 0 | 1 byte; presumably switches to the firmware-update bootloader handled by `Usbicp.sys` |
| `0x222458` | `IOCTL_SET_DRIVER_MODE` | SET | 8 | 0 | 1 byte `00`. Also sent automatically on every resume to D0 |
| `0x222454`/`0x22245C` | `…GET_ICP_RESULT` / `…GET_DRIVER_MODE_RESULT` | GET | 8 | 0 | status |
| `0x222460` | `IOCTL_SET_POLLINGRATE` | SET | 9 | 0 | polling-rate setting (125/500/1000 Hz) |
| `0x222464` | `IOCTL_GET_POLLINGRATE` | GET | 10 | 0 | |
| `0x222444` | `IOCTL_SET_CURRENTPROFILE` | (driver only) | | | 2 bytes; byte 1 becomes the `wIndex` high byte |
| `0x222420` | `IOCTL_CLEAR_BUFFER_INDEX` | (driver only) | | | resets `chunk#` to 0 |
| `0x222400` / `0x222404` / `0x222408` | `MJ HOOK` / unhook / `IOCTL_WT_READ` | (driver only) | | | event handle / read last event |

Checksums in the `SET_PROFILEID` payload: `X0 = b0^b2^b4`, `X1 = b1^b3^b5`, i.e. for keymap
`KM`: `00 08 00 03 00 KM 00 (0B^KM)`.

**Uploading one keymap** (what "Load to Synapse" / "Apply Changes" does, `n52teHid` @ `0x406250`):
`CLEAR_BUFFER_INDEX`, `SET_CURRENTPROFILE(KM)`, then 14 × `SEND_SETTING` of 0x200 bytes, each
followed by `GET_RESULT` and a checksum compare. On a mismatch the whole keymap is resent, up to
3 tries. This runs for all three keymaps in turn, then `SET_PROFILEID(start keymap)`. The
0x1C00-byte image is:

| Offset | Size | Content |
|---|---|---|
| `0x00` | 5 | `1C 1B 00 01 KM` |
| `0x08` | 27 | firmware code of each input |
| `0x23` / `0x3E` / `0x59` | 27 each | per-input option bytes p1/p2/p3, each `+1` |
| `0x100 + n*0x100` | 256 | macro of input n: up to 128 × `[code][flags]`, `flags` bit0 = press/release, bits 1–7 = delay merged in from `0xFF` delay entries |

*Unverified on the wire:* whether `usbccgp` leaves the `wIndex` low byte (chunk#) alone, and
which interface (MI_00/MI_01) the firmware expects. The filter simply uses whichever of its two
instances last forwarded an internal IOCTL. A USBPcap/usbmon capture of the original software
in a VM would settle both.

**No LED command exists.** None of the ten commands sets an LED. The colour follows the active
keymap (commands 4/5, or the firmware's own keymap codes).

## 4. The onboard profile: 3 keymaps × 27 inputs

The editor's `.ini` stores the same data as `[macro] Value=<hex>`. That is 3 × 27 records of
0x104 bytes (`code`, 256-byte macro, `p1 p2 p3`) plus one trailing byte.
`research/decode_n52te_profile.py` decodes it. The factory profile decodes to exactly the
manual's default layout:

| # | Physical input | Factory code |
|---|---|---|
| 1–14 | Keys 01–14 | Tab, Q, W, E, R, Caps Lock, A, S, D, F, L Shift, Z, X, C |
| 15 | Round thumb button | L Alt |
| **16** | **Wheel up** | **`D4` firmware Scroll up** |
| **17** | **Wheel click** | **`D2` firmware Middle Button** |
| **18** | **Wheel down** | **`D3` firmware Scroll down** |
| 19 | Thumb pad | Space |
| 20–23 | D-pad up / right / down / left | arrows |
| 24–27 | D-pad diagonals | firmware macros (e.g. `+Up +Right -Up -Right`) |

Firmware code space (from the editor's code→name tables): `0x10`–`0x9F` are keys in Razer's
own numbering (not HID usages, e.g. `0x13`=Tab, `0x57`=Space, `0x75`=Up), and then the special codes:

* `0xC0`–`0xCC` basic/additional commands (Cut, Copy, Paste, Swap/Close windows, …),
* **`0xD0` Left Button, `0xD1` Right Button, `0xD2` Middle Button, `0xD3` Scroll down,
  `0xD4` Scroll up, `0xD5`–`0xD8` mouse X/Y movement**,
* **`0xD9` Cycle Keymaps, `0xDA`/`0xDB` Blue toggle/momentary, `0xDC`/`0xDD` Green,
  `0xDE`/`0xDF` Red**,
* `0xE8`–`0xFF` media / WWW keys.

Keymap order 0/1/2 = Blue/Green/Red follows the editor's menus (*unverified on hardware*).

## 5. What this project does today (decompiled from `Instalar-n52te.exe`)

* **Remapper** `n52te_interception.exe` (.NET): `interception_set_filter(ctx, interception_is_keyboard, 0xFFFF)`.
  Mice are never filtered. Keystrokes whose hardware ID contains `VID_050D` are matched against 20
  sources (`B01`–`B14`, `BESP`, `BALT`, `BUP/BDOWN/BLEFT/BRIGHT`) and replaced via `SendInput`.
  `LButton/RButton/MButton/XButton1/2` targets are sent as `MOUSEEVENTF_*DOWN/UP`. The `INPUT`
  struct layout is correct for x64.
* **Editor** `Editor n52te.exe` (AutoHotkey v2): the same 20 tiles. No wheel, no keymaps, no
  device communication.
* **Installer**: `pnputil /delete-driver oemXX.inf /uninstall /force` for packages mentioning
  `jmtflter`/`jmtfltr`/`usbicp`/`vhidmini`, then `install-interception.exe /install`. It never
  talks to the keypad.

So the new stack only rewrites **keystrokes coming out of the keyboard interface**. Anything the
firmware does itself, or sends on the mouse interface, or sends as `0xC5`–`0xDF`, is out of its reach.

### D-pad left

D-pad left is firmware input **23**. In the factory profile it is a plain Left Arrow (code `0x77`).
The two diagonals beside it are separate inputs, 26 (down+left) and 27 (up+left). Each is a
one-shot firmware macro (`+Down +Left -Down -Left`, `+Up +Left -Up -Left`) that releases both
keys immediately, even while you keep holding.

Nothing in this project treats Left differently from Up/Down/Right. The remapper turns the
Interception stroke `E0 4B` into `0xE04B`, and `MapVirtualKey(0xE04B, MAPVK_VSC_TO_VK_EX)` gives
`VK_LEFT` (37), which `SRC` maps to `BLEFT`. That is exactly the path the other three directions
take. A learned scancode is stored and looked up as `E04B` the same way. So if only Left is dead,
the cause is below the remapper:

1. **Onboard profile.** If input 23 was given a PC-side action (or "Disable Button") with the old
   Belkin editor, the keypad now sends keyboard usage `0xC4 + 23 = 0xDB`, or nothing at all.
   Windows' `kbdhid` has no translation for `0xDB` and drops it *before* Interception, which
   sits on `kbdclass`. So the remapper, "Identify" and "Learn keys" never see the press, and
   no software above `kbdhid` can recover it.
2. **Diagonal zone.** If your thumb rolls into the down-left/up-left zone, you get inputs 26/27
   instead. Their macros tap Down/Up + Left and release at once, so a held Left "stops working"
   in games.
3. **Hardware.** A worn contact on that side of the 8-way pad.

How to tell them apart: start the remapper, open the editor, click **Identificar**, and press
D-pad left. The remapper console logs `[APRENDER] scancode …`.
* `E04B` and the *Izq.* tile is selected: the input works, so look at your mapping/game.
* `E050` (Down) or `E048` (Up): you're hitting a diagonal.
* Nothing logged at all: case 1 or 3. To check for case 1, use Linux (`usbhid-dump` or
  `evtest`, or a live USB stick): a press that shows usage `0xDB` in the raw report means the
  onboard profile is the culprit. Nothing in the raw report either means hardware.

Fix for case 1: reset input 23 to its default ("Reset Key to Default") in the original Belkin
software, e.g. in a Windows XP/7 VM with USB passthrough, then "Load to Synapse". Do the same for
26/27 if you want held diagonals.

### Note on "Left button" as a mouse click

If "left button" means assigning **LButton** to a key in this project's editor: that path
(`Execute → SendMouse(MOUSEEVENTF_LEFTDOWN/UP)`) looks correct in the decompiled code. Two things
make it silently fail. (1) While the window titled *"Editor n52te"* is in the foreground, the
remapper deliberately passes keys through raw. (2) Windows blocks `SendInput` from a
non-elevated remapper into an **elevated** (run-as-admin) game/app (UIPI). Run the remapper
elevated in that case.
If it means a physical input that types nothing at all, check whether the keypad's onboard
profile assigns it a PC-side action (see TL;DR). The decoder in §4 shows this if you export the
profile from the old editor.

## 6. Ways to fix it

1. **Program the onboard memory once with the original software, then use Windows 11 without it.**
   Run the Belkin v2.1.2 software in a Windows XP/7 VM with USB passthrough (or on an old PC).
   Assign the wheel, mouse buttons and Blue/Green/Red keymap keys there, then "Load to Synapse".
   These all run in the keypad's firmware, so they keep working on any PC with no driver,
   LEDs included. Avoid PC-side actions (joystick, launch app, load profile, window/media
   commands): they need `n52teHid` and will be dead on Windows 11.
2. **Wheel in the remapper.** Add a second Interception filter for mice, limited to wheel and
   middle-button states so normal mouse movement isn't routed through user space:
   `interception_set_filter(ctx, interception_is_mouse, INTERCEPTION_FILTER_MOUSE_WHEEL | INTERCEPTION_FILTER_MOUSE_MIDDLE_BUTTON_DOWN | INTERCEPTION_FILTER_MOUSE_MIDDLE_BUTTON_UP)`.
   For mouse devices (11–20) whose hardware ID is `…VID_050D&PID_0200&MI_01`, map `rolling > 0` /
   `< 0` / middle button to new `WUP` / `WDOWN` / `WCLICK` entries. Forward every other mouse
   stroke unchanged with `interception_send`. Add the three tiles to the editor.
3. **Keymaps/LEDs from Windows 11 directly.** This needs raw control transfers with the
   protocol in §3. The Win32 HID API can't do it: `HidD_SetFeature`/`HidD_SetOutputReport` always
   put report type 2/3 in the `wValue` high byte and the interface number in `wIndex`, while the
   n52te uses high byte 0 and `wIndex = keymap<<8 | chunk#`. Options:
   * a small libusb/pyusb tool (e.g. `ctrl_transfer(0x21, 9, 4, 0, payload)` to switch keymap, or
     the full upload) run from Linux or with a temporary libusbK/WinUSB driver on the interface,
     restoring the HID driver afterwards;
   * a new signed KMDF lower filter equivalent to `JmtFltr` (needs Microsoft attestation signing
     to load on Windows 11 with Memory Integrity on).

   First confirm the open questions in §3 with a USB capture of the original software.
4. **If the wheel doesn't even scroll natively,** check Device Manager → the n52te's
   `HID\VID_050D&PID_0200&MI_01` node (*HID-compliant mouse*) for an error code, and under
   *Details → Lower filters* for a leftover `JmtFltr`. The old filter can't load on Windows 11
   with Memory Integrity on, and a stale `LowerFilters` entry stops that interface from starting.

## Reproducing the analysis

* `research/extract_belkin_package.py`: unpacks the Chilkat SFX (raw-deflate chunks) without running it.
* `research/decode_n52te_profile.py`: decodes a v2.1.2 profile `.ini` (3 keymaps × 27 inputs).
* Decompilation: Ghidra 11.1.2 headless (`analyzeHeadless … -postScript` dumping every function),
  radare2 for the x64 completion routine at `0x10a80`, ILSpy for the .NET remapper/installer.
  Key addresses: `JmtFltr.sys` x86 IOCTL dispatch `0x10c60`, interrupt completion `0x10a50`,
  URB send `0x122d0`; `n52teHid.exe` event dispatch `0x404d30`, action handler `0x404970`,
  keymap upload `0x406250`, keymap select `0x406780`; `n52teCfg.exe` code tables at `0x4356e0`.

The Belkin binaries and decompiled output are not included in this repository. They are
Belkin/Razer copyright.
