# Plan: n52te onboard profile editor (no remapper, no Interception)

## Goal

Replace the Interception-based remapper with an editor that **writes profiles into the keypad's
onboard memory**, the way Belkin's "Load to Synapse" did. The keypad's firmware then runs the
profile by itself, on any PC, at the login screen, with nothing running in the background.

Design rules, learnt the hard way:

* **No class filter drivers.** Nothing gets added to the keyboard or mouse class `UpperFilters`
  / `LowerFilters`. A broken Interception registration in that list disabled every keyboard
  on the PC, including at the login screen.
* **No kernel driver of our own** unless every other route fails (§3, option D).
* **Never send the firmware-update command** (`wValue=7`, `IOCTL_ENTER_ICP`). It is the only
  command that could leave the keypad unusable.

The protocol, profile format and code tables this plan relies on are in
[`reverse-engineering/`](reverse-engineering/README.md).

## 1. Features and where they run

| Feature | Runs in | How | Phase |
|---|---|---|---|
| Remap any of the 27 inputs (14 keys, thumb button, thumb pad, wheel up/click/down, D-pad and diagonals) to a key or media key | firmware | key code in the input's record | 4 |
| Mouse left/right/middle button, wheel up/down, pointer X/Y on any input | firmware | codes `D0`–`D8` | 4 |
| Macros: up to 128 steps per input, with delays and playback options | firmware | macro marker + 256-byte sequence | 4 |
| Short typed text (about 60 characters, fewer with Shift) | firmware | generated macro | 4 |
| 3 keymaps (Blue/Green/Red) with toggle / momentary / cycle switch buttons; **LED shows the active keymap** | firmware | codes `D9`–`DF`, always written to all three keymaps | 4 |
| Start keymap, select keymap from the PC | firmware + PC | `SET_PROFILEID` (cmd 4) | 3–4 |
| Polling rate 125 / 500 / 1000 Hz | firmware | `SET_POLLINGRATE` (cmd 9) | 4 |
| Disable an input, reset to factory profile | firmware | code `00` / upload factory profile | 4 |
| Import old Belkin v2.1.2 profiles (`.ini`) | editor | existing decoder | 2 |
| **Launch programs / websites, window commands (minimise, maximise, close, show desktop), zoom, long text** | **Companion app** | the input sends a unique hotkey chord, the Companion reacts | 5 |
| **Automatic profile switching per game/app** | **Companion app** | foreground-window watcher → select keymap or upload profile | 5 |
| **On-screen notice of the active keymap** | **Companion app** | shown on switches it makes; for buttons on the keypad only if the transport can read the keymap | 5 |

The Companion is optional. Without it, everything in the "firmware" rows still works.

## 2. Architecture

```
Editor (Windows app)
 ├─ Profile model     Profile → 3 keymaps → 27 inputs → action
 ├─ Serializer        → 0x1C00-byte keymap image, checksums (03-usb-protocol.md)
 ├─ Importers         Belkin .ini (v2.1.2), editor's own JSON format
 └─ Transport         one interface, swappable back ends (§3)
        upload(keymap image) · selectKeymap(n) · readKeymap() · readFirmwareVersion()

Companion (optional, per-user tray app, no admin, no driver)
 ├─ Hotkey actions    RegisterHotKey on the chords the profile assigned
 ├─ App watcher       foreground process → keymap or profile
 └─ OSD               small overlay showing Blue / Green / Red
```

Profiles are saved as readable JSON next to the old `.ini` import, so they can be diffed,
shared and backed up.

## 3. Transport: how the editor talks to the keypad

The original used its own kernel filter to send HID class requests with non-standard fields
(`wValue = command`, `wIndex = keymap<<8 | chunk`). Which route works on Windows 11 is decided by
the probe (phase 0) and the capture (phase 1):

| Option | How | Pros | Cons |
|---|---|---|---|
| **A. Standard HID** | `HidD_SetFeature` / `HidD_GetFeature` | No drivers at all; keypad keeps typing; Companion can read the active keymap | Only works if the keypad declares feature reports and the firmware ignores the `wValue` report-type byte. It also has to cope with the interface number in `wIndex` (or chunk order alone has to be enough). Decided by the probe and a test |
| **B. Temporary WinUSB swap** | While saving: point the keypad's interface at Microsoft's `winusb.sys` (signed, in-box), send raw control transfers, then restore `hidusb` | No third-party kernel code; full control of `wValue`/`wIndex` | Needs admin; keypad stops typing for a few seconds during a save; must restore reliably even if the app crashes (do it with a watchdog / at next start) |
| **C. Program elsewhere** | Export the image, upload with a small libusb tool from Linux or a VM | Nothing installed on Windows at all | Not one-click |
| **D. New signed KMDF filter** | Like `JmtFltr.sys`, but on the n52te's own device node only | Everything the original could do, including reading events | Microsoft attestation signing; kernel risk; last resort |

**Plan:** implement A if the probe allows it, otherwise B, and always ship C as the fallback.
D is out of scope unless A–C all fail.

## 4. Phases

### Phase 0: probe (ready)

Run [`research/n52te-probe.ps1`](../research/n52te-probe.ps1) with the keypad plugged in, from an
admin PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\research\n52te-probe.ps1           # descriptors only
powershell -ExecutionPolicy Bypass -File .\research\n52te-probe.ps1 -TryGet   # + read-only GET_FEATURE
```

It changes nothing. It reports each HID interface (usages, report IDs, feature reports), the
device and configuration descriptors and the decoded HID report descriptors. The output goes
to the Desktop and the clipboard.

**Done when:** we know the interfaces, endpoints and report descriptors, and whether any
feature report exists (route A candidate or not).

### Phase 1: confirm the protocol with a USB capture

Run the original v2.1.2 software in a Windows XP or 7 virtual machine with the keypad passed
through. Capture with USBPcap/Wireshark inside the VM, or `usbmon` on a Linux host. Record:

1. device plug-in (startup sequence: `GET_PROFILEID`, `GET_FWVERSION`, `SET_DRIVER_MODE`),
2. "Load to Synapse" of the factory profile, then of a profile with one change per action type,
3. keymap switch from the editor, polling-rate change,
4. pressing a keymap-switch button and a PC-action button on the keypad.

Confirm or correct the open points: which interface receives the requests, `wIndex` on the wire,
keymap colour order, the meaning of option bytes p1–p3 (vary the macro playback mode), the delay
unit (inferred 50 ms), what "driver mode" changes, and whether `GET_GROUP_SETTING` (cmd 3) reads
a keymap back.

**Done when:** the serializer's output for the factory profile is byte-identical to the captured
upload, and the open points in `03-usb-protocol.md` are resolved.

### Phase 2: profile core library

* Model + validation: 27 inputs × 3 keymaps, 128-step macro limit, keymap-switch codes written to
  all keymaps, at least one way back to keymap 0 when a keymap button is used.
* Serializer: 0x1C00-byte image, 14 × 0x200 chunks, XOR checksums, 8-byte command blocks.
* Importer for Belkin `.ini` (from `research/decode_n52te_profile.py`), JSON load/save.
* Text-to-macro helper using the firmware key-code table (with Shift handling and a character budget).
* Unit tests: factory `.ini` → image matches the phase 1 capture; round-trip `.ini` → JSON → image.

**Done when:** all tests pass without hardware.

### Phase 3: transport

* Implement the chosen back end (A or B) plus the export used by C.
* Safety: refuse command 7; cap every transfer at 0x200 bytes; checksum after every chunk and
  resend the keymap up to 3 times like the original; read back if cmd 3 works; time out after
  3 s per request.
* Keep the last successfully uploaded profile as a backup, and offer "restore factory profile"
  at all times.
* For B: restore `hidusb` in a `finally` block, and also on the next start if the app was killed
  mid-save.

**Done when:** the factory profile can be uploaded and the keypad still types its factory
layout. Then one changed key, one macro, the wheel as keys, and the Blue→Green switch with the
LED changing.

### Phase 4: editor UI

* Visual keypad with all 27 inputs (wheel up/click/down and the four diagonals shown
  explicitly), keymap tabs Blue / Green / Red, start-keymap and polling-rate settings.
* Action picker: key, media key, mouse button, wheel, pointer movement, macro, typed text, keymap
  switch (toggle / momentary / cycle), disabled, PC action (needs the Companion).
* Macro editor: record (keyboard hook only while the editor is focused), edit, delays, step
  counter against the 128 limit.
* "Save to keypad" with progress, verification and a clear result. "Read from keypad" if the
  transport supports it.
* Import old Belkin profiles. English and Spanish UI.

**Done when:** every firmware row of §1 can be set from the UI and verified on the keypad.

### Phase 5: optional Companion app

* **PC actions:** the editor assigns each PC action a macro that presses a chord not used
  elsewhere, e.g. Ctrl+Alt+Shift+F1…F12, then Ctrl+Alt+Shift+numpad keys. The Companion registers
  those chords with `RegisterHotKey`, so it gets them without hooking or intercepting the keyboard.
  Actions: launch program / URL, minimise / maximise / close window, show desktop, zoom in/out,
  type long text, media-player control. If the Companion isn't running, the chord just reaches the
  focused app, so pick chords nothing else uses, and let the user change them.
* **Auto-switching:** preferred mode is *keymap per app*: on a foreground change, send
  `SET_PROFILEID` (one 8-byte command, no flash write, instant). The alternative mode uploads a
  whole profile when the app changes. It writes the keypad's flash every time, so only do it
  when the profile actually differs, and rate-limit it (flash endurance is unknown).
* **OSD:** show the keymap colour when the Companion switches it. Showing switches made with the
  keypad's own buttons needs `GET_PROFILEID` polling, so it's only possible with transport A.
* Per-user tray app, starts with Windows via the normal Run key, no admin rights, no drivers.

**Done when:** each action type works from a keypad button, and auto-switching follows the
foreground app without noticeable delay.

### Phase 6: packaging and migration

* Installer with **no drivers** (for transport B it only relies on the in-box `winusb.sys`).
* Migration from the Interception version: create a restore point, reset the keyboard/mouse
  class filter lists to `kbdclass` / `mouclass`, then run Interception's uninstaller and remove
  the remapper's Run entry. Keep a recovery section in `INSTALL.md`.
* Uninstaller leaves the keypad with its current onboard profile, which keeps working.

## 5. Risks

| Risk | Mitigation |
|---|---|
| No driver-free route to the keypad (A fails) | B (in-box WinUSB), C (export + external upload) |
| Protocol details wrong (unverified) | Phase 1 capture before any write; factory-profile byte-for-byte test |
| Keypad bricked | Never send cmd 7; only send the documented 10 commands; factory profile always available |
| Flash wear from frequent uploads | Prefer keymap switching (no write) for auto-switching; skip identical uploads |
| Interrupted upload | Per-chunk checksum, whole-keymap resend, backup of last good profile |
| Chord collisions for PC actions | User-configurable chords; detect `RegisterHotKey` failures and report them |

## 6. Decisions

Decided on 18 Nov 2026:

1. **UI technology:** WinForms on .NET 10 (LTS, supported until 14 Nov 2028; .NET 8 reached end of support on 10 Nov 2026), published as single-file `.exe`s. It
   replaces the AutoHotkey editor.
2. **Companion:** part of the first release (phase 5 work runs alongside phase 4).
3. **Language:** English is the default UI language. Spanish follows as a translation.

## 7. Code layout

| Path | What |
|---|---|
| `src/N52te.Core` | Profile model, key-code tables, keymap image builder, Belkin `.ini` import/export, JSON, text-to-macro, Companion chord allocation, device client over a transport interface (with the safety guard) |
| `src/N52te.Core.Tests` | Unit tests, runnable on any OS. Set `N52TE_BELKIN_DEFAULT_INI` to Belkin's `Default.ini` to also run the byte-for-byte round-trip test |
| `src/N52te.Editor` | WinForms editor |
| `src/N52te.Companion` | WinForms tray app |
