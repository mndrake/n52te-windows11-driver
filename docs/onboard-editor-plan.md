# Plan: an editor that works like the original (onboard profiles)

**Goal:** replace the remapper-based editor with one that works like Belkin's n52te Editor
v2.1.2: you edit the keypad's three keymaps (Map 1 blue, Map 2 red, Map 3 green) and
**Apply Changes** writes them into the keypad, which then runs them itself, with the LED following
the keymap, the wheel and mouse buttons working, and nothing running in the background.

**Why:** the remapper approach (PC-side keymaps on top of whatever the keypad sends) can't
reach silent controls, can't move the LED, and needs a resident process and a kernel driver. The
onboard approach is what the hardware was built for, and its protocol is now verified
([03](reverse-engineering/03-usb-protocol.md), [profile-tool-design.md](profile-tool-design.md)).

## What the original editor had

From the v2.1.2 skin (`skin_English\*.ini`, `*.jpg`) and the editor analysis in
[06](reverse-engineering/06-n52tecfg-editor.md):

* **Main window:** keypad picture with **Map 1 / Map 2 / Map 3** tabs (blue / red / green; the
  picture is tinted with the keymap's colour). Around it, a label box for every one of the
  **27 inputs**: a 14-row column for keys 01–14, one for the round thumb button, three for the
  wheel (up / click / down), one for the thumb pad, eight for the D-pad (four directions and four
  diagonals).
* **Right-click menu per input:** Single Key, Macro (and saved macros), Keymap (Cycle; Blue,
  Green, Red toggle or momentary; written to all three keymaps), Mouse Button (left / middle /
  right; DirectInput button and axes; mouse X/Y axis and scroll wheel), Basic Commands (Cut …
  Print), Additional Commands, Media Functions, Load Profile, Load Application, Reset Key to
  Default, Disable Button.
* **Dialogs:** Single Key ("Please Enter a Single Key", captures a key press), Advanced Settings
  (Basic / Additional Commands, Media Functions, Macro drop-downs), Direct Input (name + button),
  profile picker.
* **Macro editor:** macro name with New / Open / Save / Save As / Delete; step list with Record,
  Stop, Advanced, Move Up / Down, Delete, Select All, Select All Delays, Insert Delay (spin
  value); "Ignore delays between events"; mouse-button picker (left / middle / right);
  **Macro Behavior**: Run Times *n*, Repeat while pressed, Repeat until next key is pressed,
  Repetition interval.
* **Profiles:** Current Profile drop-down with Linked Application and Auto-Switching; New, Save,
  Save As, Import, Delete; Auto-Switching Override; Show Changes On Screen (OSD).
* **Device:** Polling Rate 125 / 500 / 1000 Hz, Reset Keymap, **Apply Changes** (uploads all
  three keymaps; "This profile exceeds the maximum allowable size for storage in Synapse").

## Safety rules

* **No keyboard or mouse class filters.** Nothing is added to `UpperFilters` / `LowerFilters` of
  the keyboard (`{4D36E96B-…}`) or mouse (`{4D36E96F-…}`) class; they stay `kbdclass` /
  `mouclass`. On 18 Nov 2026 a stale Interception entry in those lists disabled every keyboard on
  the PC, including at the login screen.
* **UsbDk is a USB class filter.** It registers itself in `UpperFilters` of the USB class
  (`{36FC9E60-C465-11CF-8056-444553540000}`), the same mechanism. Install and remove it only with
  its own MSI, never by deleting `UsbDk.sys`; if the entry stays without the driver, USB devices
  (keyboards and mice too) will probably stop starting. Recovery is the same as for the keyboard
  class: remove `UsbDk` from that list (from Windows Recovery if needed) and restart.
* Create a restore point before any driver or registry change.
* Never send command 7 (`ENTER_ICP`, firmware update).

## Decisions

Confirmed on 18 Nov 2026. They replace the decisions on the cloud branch
`claude/n52te-driver-decompile-tejavx` (.NET 10, Companion app in the first release, HID or
WinUSB transport).

| Decision | Choice | Why |
|---|---|---|
| Technology | **C# WinForms on .NET Framework 4.8**, one `n52te Editor.exe`, sharing a core library with `n52te-config` | The keypad code (UsbDk, image builder, decoder) is already C#; AutoHotkey can't share it cleanly. Built with the `csc` that ships with Windows, like the rest: no SDK, no runtime download, small exes. That compiler only takes C# 5, so code ported from the cloud branch is rewritten without newer syntax. |
| Transport | **UsbDk** first, behind `IKeypadTransport`; temporary WinUSB only if UsbDk fails with Memory Integrity on | UsbDk works on hardware today and leaves the keypad on its HID driver. Standard HID is ruled out (no feature reports). WinUSB is untested: it would have to replace the driver of interface 0 (the keyboard), and it isn't known whether it passes the device-recipient requests with arbitrary `wIndex` the keypad needs. |
| Companion tray app | **Not in the first version** | Per-app keymap switching would borrow the keypad on every window change, and showing keypad-made switches would need constant reads; neither fits a borrow-the-device transport. If wanted later, PC actions can come back as a small optional add-on: the keypad plays an unused hotkey chord (e.g. Ctrl+Alt+Shift+F1, a firmware macro) and a tray app catches it with `RegisterHotKey`, with no driver. |
| Look | **Same layout and workflow, our own artwork** | Belkin's skin bitmaps and logos are Belkin's/Razer's; shipping them in a public repo isn't ours to do, and imitating their branding would misrepresent the project. Draw the keypad outline and controls ourselves. |
| PC-side actions (DirectInput joystick, Load Profile, Load Application, Open Explorer, Zoom, Minimize/Maximize, Shuffle) | **Leave out** (shown greyed out, with a tooltip) | The keypad reports them as keyboard usage `0xC4 + n`, which Windows' keyboard driver drops; running them needs a resident filter driver like `JmtFltr`. |
| Auto-switching by application, override, OSD | **Leave out of the first version** | Switching profiles means re-uploading three keymaps; the keypad stops typing for a few seconds each time. Revisit after v2.0 if wanted. |
| Remapper + Interception | **Retire** from the installer; offer to remove them | The onboard editor replaces them. |
| UsbDk | **Bundle the signed UsbDk 1.0.22 MSI** (Apache 2.0) and install it with consent, then ask for a restart | `n52te-config` and the editor need it; the restart avoids the Bluetooth-keyboard problem seen during testing. |
| Profile files | **Read and write the original v2.1.2 `.ini` format** | Old profiles import as-is; `research/decode_n52te_profile.py` already decodes it. |

## Reusing the cloud branch

The branch `claude/n52te-driver-decompile-tejavx` has a .NET 10 core (44 xUnit tests), an editor
and a Companion that build but have never run. Nothing of it is merged; port by hand into
`src/core` (C# 5) where it saves work, and check each piece against the hardware results:

| From `src/N52te.Core` | Use | Fix when porting |
|---|---|---|
| `ProfileCompiler.cs`, `Macro.cs` | input records, image builder, macro encoding | checksums and slot order against [03](reverse-engineering/03-usb-protocol.md#keymap-image) |
| `ProfileFiles.cs` | Belkin `.ini` read/write (byte-identical with the factory profile) | drop the JSON format and Companion fields |
| `FirmwareCodes.cs`, `KeypadLayout.cs`, `WindowsKeys.cs` | code tables, the 27 inputs, VK → firmware code | `Keymap` enum says Blue 0, **Green 1, Red 2**; hardware says Blue 0, **Red 1**, Green 2 |
| `ProfileValidator.cs` | checks before Apply Changes | — |
| `N52te.Core.Tests` | test cases for the `selftest` command | — |

Not carried over: the Companion (`Companion.cs`, `N52te.Companion`), `MappingDetector.cs`, the
self-update (`Updates.cs`), the .NET 10 CI workflow, and `research/n52te-probe.ps1`, whose
question (are there feature reports?) is already answered.

## Steps

### Phase 0 – Close the unknowns (before any UI work)

1. **UsbDk with Memory Integrity on.** Turn Memory Integrity on (you, in Windows Security),
   restart, run `n52te-config info` and `read all`. If UsbDk is blocked, prototype the fallback
   (temporarily binding WinUSB to the keypad) before going further; the rest of the plan sits on
   top of one `IKeypadTransport` interface either way.
2. **Hardware test matrix** with `n52te-config set` (each change backed up and restored):
   * select keymap 2, confirm the LED is green;
   * mouse codes `D0`–`D8` (left / right / middle, scroll down / up, axis moves);
   * keymap switches `D9`–`DF` written to all three keymaps (LED follows, momentary vs toggle);
   * commands `C0`–`CE` and media codes `E8`–`FF` (what Windows receives);
   * Disabled (`00`) and Reset to default.
   Record results in [04](reverse-engineering/04-onboard-profile-and-codes.md).
3. **Finish reverse-engineering `n52teCfg.exe`** (Ghidra, as for `n52teHid.exe`):
   * macro editor: how Run Times / Repeat while pressed / Repeat until next key / Repetition
     interval / Ignore delays map to option bytes p1–p3;
   * the size check behind "exceeds the maximum allowable size" (steps per macro, total);
   * how recorded keys (virtual-key codes) become firmware key codes, and how mouse-button
     steps are stored in a macro;
   * Reset Keymap, Import, and the polling-rate handler.
4. **Macro upload test:** add a `macro` command to `n52te-config` (from a small text file of
   steps), write a short macro with a delay, check it plays from the keypad, test each behaviour
   option, restore.
5. **Polling rate and command 8:** test `SET_POLLINGRATE` (cmd 9) and see whether command 8
   changes anything.

**Exit criterion:** every action the new editor offers has been written to and run on the
keypad at least once.

### Phase 1 – Core library

6. Split `src/configtool/N52teConfig.cs` into `src/core/`:
   * `Transport` (`IKeypadTransport`, UsbDk implementation, borrow/return the keypad);
   * `KeymapImage` (slot table, header, codes, option bytes, macro encode/decode, checksum);
   * `Profile` (name, start keymap, 3 × 27 inputs, macros) and `ProfileIni` (v2.1.2 `.ini`
     read/write);
   * `Codes` (firmware code table and names; VK → firmware key code).
7. **Self-tests** (a `selftest` command): the factory `Default.ini` and the backups from
   2026-11-16 must convert to images and back byte-identically.
8. `n52te-config` becomes a thin front end, with new commands: `apply PROFILE.ini` (all three
   keymaps + start keymap), `export PROFILE.ini` (read the keypad into a profile), `macro`,
   `polling`.

### Phase 2 – Editor main window

9. Main window laid out like the original: keypad drawing, Map 1/2/3 tabs (blue / red / green,
   firmware keymaps 0/1/2), the 27 labelled input boxes, active keymap shown from the keypad.
10. Right-click menu with the firmware-run items: Single Key, Macro, Keymap (all three keymaps),
    Mouse (buttons, wheel, axes), Basic / Additional Commands, Media, Reset Key to Default,
    Disable Button. PC-side items greyed out.
11. **Single Key** dialog that captures a key press (low-level keyboard hook, like `KBHook.dll`)
    and **Advanced Settings** drop-downs.
12. **Read from keypad** (new profile from the keypad's current contents) and **Apply Changes**
    (backup, upload with progress, verify, set start keymap). The keypad doesn't type during the
    few seconds this takes; say so in the UI.
13. **Reset Keymap**, Polling Rate (125 / 500 / 1000 Hz).

### Phase 3 – Macro editor

14. Step list with Record / Stop, Insert Delay (50 ms units, up to 6350 ms), Move Up / Down,
    Delete, Select All / Select All Delays, Ignore delays, mouse-button steps.
15. Macro Behavior options mapped to p1–p3 (from step 3); size check before Apply Changes.
16. Saved macros (New / Open / Save / Save As / Delete) that can be assigned to any input.

### Phase 4 – Profiles

17. Profile drop-down with New / Save / Save As / Import (v2.1.2 `.ini`) / Delete, stored in
    `Documents\n52te\Profiles`; the keypad holds one profile at a time.

### Phase 5 – Installer and migration

18. Installer: install the editor and `n52te-config`, bundle UsbDk (consent + restart), stop
    installing Interception and the remapper. On upgrade, stop and remove the remapper, its
    autostart entry and shortcuts, and offer to uninstall Interception (restart).
19. Existing remapper settings can't be carried over one-to-one (typed text, launching
    programs). Show them once in a read-only list so they can be re-created on the keypad.

### Phase 6 – Docs and release

20. Rewrite README / INSTALL around the onboard editor, new screenshots, update the
    reverse-engineering notes with the Phase 0 results, release as **v2.0.0**.

## Risks

* **UsbDk under Memory Integrity** (Phase 0 step 1) decides the transport.
* **UsbDk's USB class filter entry** (see Safety rules): a broken install or a hand-deleted
  driver can take all USB input down. The installer only ever runs UsbDk's own MSI.
* **Unverified codes or macro options** could behave differently from the editor's labels; Phase
  0 tests them before they are offered.
* **Bad writes:** every write keeps a backup and verifies by reading back; a keymap can always be
  rewritten. Never send command 7 (firmware-update mode).
