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
| Look | **Belkin's window, as close as possible** (decided 18 Nov 2026): same 468 × 509 layout, positions, tabs, buttons and right-click menu, taken from `skin_English\main.ini`. **Belkin's own artwork is used when the user points the editor at their own copy** of the v2.1.2 software (right-click → Artwork); otherwise the editor draws its own artwork in the same layout | Belkin's skin bitmaps and logos are Belkin's/Razer's: loading them from the user's copy is fine, shipping them in a public repo isn't ours to do. No UI from the earlier remapper editor is reused. |
| PC-side actions (DirectInput joystick, Load Profile, Load Application, Open Explorer, Zoom, Minimize/Maximize, Shuffle) and the **Basic / Additional Commands** (`C0`–`CE`: Cut, Copy, Paste, Undo, Find, Swap Windows, Run, …) | **Leave out** (shown greyed out, with a tooltip) | The keypad reports the PC-side actions as keyboard usage `0xC4 + n`, which Windows' keyboard driver drops; running them needs a resident filter driver like `JmtFltr`. The commands send nothing at all on hardware (17 Nov 2026). Cut/Copy/Paste etc. can still be offered as ordinary key-combination macros (Ctrl+X …) once macros are verified. |
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

1. ~~**UsbDk with Memory Integrity on.**~~ Done on 18 Nov 2026: `n52te-config info` and
   `read all` work with Memory Integrity on, without admin rights
   ([profile-tool-design.md](profile-tool-design.md#test-plan)), and so do `write` and `select`
   (the macro tests in step 4). UsbDk stays the transport.
2. ~~**Hardware test matrix.**~~ Done on 17–18 Nov 2026
   ([04](reverse-engineering/04-onboard-profile-and-codes.md#verified-on-hardware-17-nov-2026)):
   keymap 2 is Green; mouse codes `D0`–`D8` and keymap switches `D9`–`DF` work from the firmware
   and the LED follows; **every** media code `E8`–`FF` arrives as a consumer key (some, like
   Zoom, Rotate, Shuffle and Eject, have no Windows key); the commands tried (`C0`–`C3`, `C5`,
   `C9`, `CB`) send **nothing**. Reset to default needs no test of its own: it writes ordinary
   codes and the factory diagonal macros ([07](reverse-engineering/07-n52tecfg-macros-and-options.md#6-reset-keymap-and-import)).
3. ~~**Finish reverse-engineering `n52teCfg.exe`.**~~ Done on 17 Nov 2026
   ([07](reverse-engineering/07-n52tecfg-macros-and-options.md)): p1 = mode (Run Times / Repeat
   while pressed / Repeat until next key), p2 = run count, p3 = interval; "Ignore delays" never
   reaches the keypad; the size warning was never implemented (the real limit is 128 steps per
   macro); the recorder's VK → code table; Reset Keymap, Import, polling rate, and the profile
   file's trailing checksum byte.
4. ~~**Macro upload test.**~~ Done on 18 Nov 2026 with hand-built images and `n52te-config
   write` ([04](reverse-engineering/04-onboard-profile-and-codes.md#macros-18-nov-2026)): macros
   with several keys and a delay play from the keypad; Run Times (with its count), Repeat while
   pressed (with its interval) and Repeat until next key all work; option bytes are the editor's
   values + 1, and a raw p1 of 0 disables the macro; mouse-button steps inside a macro work. A
   `macro` command for `n52te-config` comes with the core library (Phase 1).
5. **Polling rate** ~~(cmd 9)~~: done on 18 Nov 2026 with the new `n52te-config polling` and
   `descriptor` commands
   ([04](reverse-engineering/04-onboard-profile-and-codes.md#polling-rate-18-nov-2026)): kept across
   unplugging, effective after reconnecting, and it only changes the keyboard endpoint (mouse and
   media stay at 125 Hz). The editor should say "takes effect after replugging the keypad".
   **Command 8** ("driver mode") is still untested; it isn't needed for anything the editor
   offers, so it stays out unless something turns out to depend on it.

**Exit criterion:** every action the new editor offers has been written to and run on the
keypad at least once.

### Phase 1 – Core library

Done on 18 Nov 2026 (steps 6–8). The library is plain C# 5 so the in-box `csc` builds it; the
editor will compile the same files.

6. ~~Split `src/configtool/N52teConfig.cs` into `src/core/`.~~ Done:
   * `Transport.cs`: `IKeypadTransport`, the UsbDk implementation (borrow/return the keypad,
     `listen`);
   * `KeypadClient.cs`: the configuration commands (read/write/verify a keymap, select keymap,
     polling rate, descriptor), with the guard that refuses command 7;
   * `KeymapImage.cs`: slot table, upload header, option bytes +1, macro fold/unfold, checksum,
     decoding for display;
   * `Profile.cs`: input records, profile (name, start keymap, 3 × 27 inputs), Belkin's factory
     profile, checks (switches not in all three keymaps, invalid macro modes, PC-side actions,
     commands), macro steps as text (`"A 1000ms B"`, `"+LShift H -LShift"`);
   * `ProfileIni.cs`: v2.1.2 `.ini` read/write, including the trailing checksum byte;
   * `FirmwareCodes.cs`: code names (pointer directions as the keypad really moves), name ↔
     code, VK → firmware code.
   Ported from the cloud branch with its bug fixed: its image builder wrote inputs in input order
   instead of slot order.
7. ~~**Self-tests**~~: `n52te-config selftest [Default.ini]`, also run by `build.ps1`. Passes:
   the built-in factory profile is byte-identical with Belkin's `Default.ini` (checksum `9A`),
   and the file written back is identical to it; all 28 saved keypad images (backups and test
   images from 16–18 Nov) convert to records and back byte-identically, and through a `.ini`;
   the macro "A 1000ms B" encodes to the bytes verified on hardware; a simulated keypad checks
   the write/verify path and that command 7 never leaves the program.
8. ~~`n52te-config` becomes a thin front end.~~ New commands: `export FILE.ini` (the keypad's
   three keymaps and active keymap as a Belkin profile), `apply FILE.ini --yes` (writes only
   the keymaps that differ, then selects the start keymap; `--dry-run` lists the changes),
   `macro KM INPUT "STEPS" [--mode …] --yes`, `show FILE` (decode a `.bin` or `.ini` offline),
   `selftest`. Checked on the keypad read-only: `read all` matches earlier reads byte for byte,
   `export` + `apply --dry-run` finds all three keymaps unchanged, and `apply Default.ini
   --dry-run` lists exactly the factory layout's differences. Writes, with Memory Integrity on:
   `macro red 1 "A 1000ms B"` typed `a`, a pause, `b` from the keypad; `apply` of the exported
   profile then wrote only Red back (Blue and Green skipped as unchanged), selected Blue, and
   left Red byte-identical with its original.

### Phase 2 – Editor main window

First version built on 18 Nov 2026 in `src/onboardeditor` (`n52te Editor.exe`, compiled by
`build.ps1`, not in the installer yet); not yet tried by a user. It covers steps 9–13 plus the
profile row and buttons (New, Save, Save As, Import, Delete into `Documents\n52te\Profiles`),
reads the keypad when it starts, and shows "n52te not connected" like the original. The Single
Key dialog captures keys in the dialog itself (no global hook needed); the macro dialog takes
the steps as text until phase 3. Auto-Switching Override and Show Changes On Screen are drawn but
not available.

9. Main window laid out like the original: keypad drawing, Map 1/2/3 tabs (blue / red / green,
   firmware keymaps 0/1/2), the 27 labelled input boxes, active keymap shown from the keypad.
10. Right-click menu with the firmware-run items: Single Key, Macro, Keymap (all three keymaps),
    Mouse (buttons, wheel, axes; with the pointer directions the keypad really produces, not
    Belkin's reversed labels), Media, Reset Key to Default, Disable Button. PC-side items and the
    Basic / Additional Commands greyed out.
11. **Single Key** dialog that captures a key press (low-level keyboard hook, like `KBHook.dll`)
    and **Advanced Settings** drop-downs.
12. **Read from keypad** (new profile from the keypad's current contents) and **Apply Changes**
    (backup, upload with progress, verify, set start keymap). The keypad doesn't type during the
    few seconds this takes; say so in the UI.
13. **Reset Keymap**, Polling Rate (125 / 500 / 1000 Hz).

### Phase 3 – Macro editor

First version built on 18 Nov 2026 (`src/onboardeditor/MacroEditor.cs`, `src/core/MacroFile.cs`),
not yet tried by a user: Belkin's 266 × 456 macro panel from `Macro_expand.ini`, sliding out of
the main window's right edge with its collapsed "MACRO EDITOR" tab, with Belkin's artwork or the
built-in one. It covers steps 14–16: recording through a system-wide low-level keyboard hook
(like `KBHook.dll`; auto-repeat skipped, time between events as delay steps unless *Ignore
delays*, stops at 128 steps), Advanced (keys, mouse, media), mouse-button steps from the mouse
picture, Insert Delay with Belkin's 50–1000 ms choices, Move Up / Down, Delete, Select All /
Select All Delays, Macro Behavior (Run Times 1–10, Repeat while pressed, Repeat until next key,
interval 50–250 ms), and saved macros in Belkin's macro file format (`Documents\n52te\Macros`)
with New / Open / Save / Save As / Delete and the "presses more than it releases" warning. An
input's right-click **Macro** menu lists the saved macros, the macro open in the editor, *Edit
This Input's Macro* and *Type the Steps…* (the text dialog). The README shows a screenshot
(built-in artwork only: Belkin's images stay out of the repository).

14. Step list with Record / Stop, Insert Delay (50 ms units, up to 6350 ms), Move Up / Down,
    Delete, Select All / Select All Delays, Ignore delays, mouse-button steps.
15. Macro Behavior options mapped to p1–p3 (from step 3); size check before Apply Changes.
16. Saved macros (New / Open / Save / Save As / Delete) that can be assigned to any input.

### Phase 4 – Profiles

17. ~~Profile drop-down with New / Save / Save As / Import (v2.1.2 `.ini`) / Delete, stored in
    `Documents\n52te\Profiles`; the keypad holds one profile at a time.~~ Built on 18 Nov 2026,
    not yet tried by a user:
    * the **Current Profile** drop-down lists the saved profiles in Belkin's three columns
      (Linked Application `-`, Auto-Switching `Off`); a green dot marks the profile last
      written to the keypad;
    * Belkin's prompts, drawn in its style (`SkinDialog.cs`): **SAVE CHANGES** "Do you want to
      save your changes?" Yes / No / Cancel when leaving a changed profile, **Profile Save
      Warning!** when an imported profile's name is taken (then Save as), the macro editor's
      "Current Macro has unsaved changes. Continue?", and Apply Changes, Reset Keymap, Delete
      and error messages in the same style;
    * like Belkin: Import copies the file into the profiles folder under its profile name,
      Delete opens the next profile in the list (or the factory profile), at most 100 profiles;
    * after reading the keypad the editor looks for the saved profile whose three keymaps the
      keypad holds and opens it (with the keypad's active keymap as its start keymap) instead
      of an unnamed "Keypad" profile; without a keypad it opens the profile used last.

### Phase 5 – Installer and migration

On 18 Nov 2026 the user asked to scrap all UI from the forked repository. Removed from `main`:
the AutoHotkey editor (`src/editor`), the Interception remapper (`src/remapper`), the installer's
Interception files and INFOMALENA branding (`src/installer/resources`), the v1 screenshots and
logo, and the committed v1.3 `Install-n52te.exe` (still in release v1.3.0; source at the
`v1.3.0` tag). The installer was rewritten (own UI) and README / INSTALL describe version 2.

18. ~~Installer: install the editor and `n52te-config`, stop installing Interception and the
    remapper; on upgrade remove the remapper, its autostart and shortcuts, and offer to remove
    Interception.~~ Done in `src/installer/Installer.cs` (18 Nov 2026), not yet run on a PC.
    Interception is removed in the safe order: restore point, then its entries out of the
    keyboard/mouse filter lists (keeping `kbdclass` / `mouclass`), then its services, then its
    files (kept in `C:\ProgramData\n52te\removed-drivers`). Still to do: bundle UsbDk (now the
    installer links to its download page), an uninstall entry in *Settings → Apps*.
19. Existing remapper settings can't be carried over one-to-one (typed text, launching
    programs). Show them once in a read-only list so they can be re-created on the keypad.

### Phase 6 – Docs and release

20. Rewrite README / INSTALL around the onboard editor, new screenshots, update the
    reverse-engineering notes with the Phase 0 results, release as **v2.0.0**.

## Risks

* ~~**UsbDk under Memory Integrity**~~: works (Phase 0 step 1). Users who want Memory Integrity
  on must remove Interception first; the installer's migration step (Phase 5) does that.
* **UsbDk's USB class filter entry** (see Safety rules): a broken install or a hand-deleted
  driver can take all USB input down. The installer only ever runs UsbDk's own MSI.
* **Unverified codes or macro options** could behave differently from the editor's labels; Phase
  0 tests them before they are offered.
* **Bad writes:** every write keeps a backup and verifies by reading back; a keymap can always be
  rewritten. Never send command 7 (firmware-update mode).
