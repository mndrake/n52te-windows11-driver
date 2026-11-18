# n52te Editor for Windows 11 Blueprint

## System Overview

Software to program the onboard memory of the Belkin / Razer Nostromo n52te keypad
(`VID_050D&PID_0200`) on Windows 10/11 x64. The keypad stores three keymaps (firmware 0 = Blue,
1 = Red, 2 = Green) and runs them by itself. This project reads and writes them over USB control
transfers through UsbDk, using the protocol reverse-engineered from Belkin's v2.1.2 software.
Nothing runs in the background. User-facing documentation is in [README.md](./README.md) and
[INSTALL.md](./INSTALL.md); this file is the architectural contract.

```text
n52te Editor.exe (src/onboardeditor) ─┐
                                      ├─> src/core: KeypadSession -> KeypadClient -> UsbDkTransport -> UsbDkHelper.dll -> keypad
n52te-config.exe (src/configtool)  ───┘
Install-n52te.exe (src/installer): embeds both exes; no runtime link to core
research/: standalone RE scripts (pyusb on Linux, Raw Input logger), not built
```

Every keypad call is synchronous and blocking. The editor runs each call that takes the keypad
(`KeypadClient.OpenUsbDk`) on a worker thread (`Task.Run` in `MainForm.OnKeypad`). Its presence check,
`UsbDkTransport.IsPresent()`, runs on the UI thread: at Shown, every 3000 ms, and after a read
fails. That check lists devices without taking the keypad.

## Local Interface & Invariants

- `build.ps1` (PowerShell; params `-OutDir` (default `build\`) and `-InstallerName` (default
  `Install-n52te.exe`)) is the only build script. It runs four steps, in order:
  1. Compile `core` + `configtool` to `build\obj\n52te-config.exe` and run `n52te-config selftest`.
     A non-zero exit fails the build.
  2. Compile `core` + `onboardeditor` to `build\obj\n52te Editor.exe`.
  3. Compile `installer\Installer.cs` with `/win32manifest:src\installer\app.manifest` and
     `/resource:` the two exes as `RES_EDITOR` / `RES_CONFIG`.
  4. Copy the exes to `build\` and write `<installer>.sha256`.
- `build.ps1` requires `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and compiles with
  `/platform:x64 /optimize+ /warnaserror+`. All code must stay C# 5.
- `build.ps1` side effects: it writes only under `-OutDir`. If a running exe locks its target in
  `build\`, the copy only warns, and the new build stays in `build\obj`. It installs nothing.
- `build.ps1` depends on per-user input. The selftest gate also round-trips every `*.bin` under the
  building user's `Documents\n52te keymaps` (`SelfTest.Images`), so a malformed backup there fails
  the build. If `N52TE_BELKIN_DEFAULT_INI` is set, the gate reads that file too.

## Subdirectories & System Boundaries

- src: [./src/EGM.md](./src/EGM.md) - All product source and its build graph.
  - [./src/core/EGM.md](./src/core/EGM.md) - Shared library: UsbDk transport, HID protocol
    (`KeypadClient`), keymap image, profile/macro model, Belkin `.ini` codecs, `KeypadSession`
    apply with backups.
  - [./src/configtool/EGM.md](./src/configtool/EGM.md) - `n52te-config.exe` CLI and its
    `selftest` build gate.
  - [./src/onboardeditor/EGM.md](./src/onboardeditor/EGM.md) - `n52te Editor.exe` WinForms
    editor in Belkin's layout.
  - [./src/installer/EGM.md](./src/installer/EGM.md) - `Install-n52te.exe`: installs both
    programs and removes v1 / Interception / Belkin driver leftovers.
- research: [./research/EGM.md](./research/EGM.md) - Standalone reverse-engineering tools:
  Belkin package extractor, profile decoder, pyusb keymap read/select/upload scripts, and a Raw
  Input monitor. Not built, and not imported by `src`. Some scripts write to the keypad.
- docs (no code, no contract): design and reverse-engineering notes, for example
  [docs/reverse-engineering/README.md](./docs/reverse-engineering/README.md) and
  [docs/onboard-editor-plan.md](./docs/onboard-editor-plan.md).

## Global Invariants

- **Never send command 7 (`ENTER_ICP`, firmware update).** `KeypadClient.Control` throws for it,
  and `SelfTest.Protocol` checks the guard. Class requests must not bypass `KeypadClient.Control`.
- **Keymap numbering:** firmware and all `src` code use 0 = Blue, 1 = Red, 2 = Green
  (`KeypadLayout.KeymapNames`). `ProfileIni` maps `.ini` blob keymap `k` to `Keymaps[k]` without
  reordering. Note: `research/decode_n52te_profile.KEYMAPS` labels the blob keymaps
  `Blue, Green, Red`, which disagrees with `src/core`. Treat `src/core` as the reference.
- **Image format:** a keymap image is 0x1C00 bytes, sent in 14 chunks of 0x200. An upload
  carries the header `1C 1B 00 01 KM`, and comparisons skip the first 8 bytes
  (`KeymapImage`, `research/n52te_upload.py`).
- **Back up before every write:** each keymap write is preceded by a `.bin` backup in
  `Documents\n52te keymaps` (`KeypadSession.Apply`, `configtool Program.Write`). The CLI requires
  `--yes` for `apply`, `macro`, `set` and `write`. `roundtrip` (which rewrites a keymap unchanged),
  `select` and `polling` change keypad state without it.
- **Hardware-verified expectations:** `SelfTest` holds byte sequences verified on hardware. When a
  check fails, fix `core`; do not edit the expectation to make it pass.
- **C# 5 with the in-box compiler, x64, warnings as errors** (`build.ps1`).
- **The installer never leaves the keyboard or mouse class filter lists without
  `kbdclass`/`mouclass`** when it removes Interception (`Installer.SetFilters`). It doesn't repair
  these lists in any other case. It removes Interception only after the user agrees
  (`RemoveInterception`). If no restore point can be created, it asks again and goes ahead
  without one if the user agrees. The Belkin-leftover cleanup runs before that, with no prompt
  and no restore point.

## Global Side Effects

- **USB:** while a `UsbDkTransport` is open (`UsbDk_StartRedirect`), the keypad is taken from
  Windows and sends no input. Writes change onboard memory, the active keymap and the polling
  rate, which persist across replugs.
- **UsbDk dependency:** both programs load
  `C:\Program Files\UsbDk Runtime Library\UsbDkHelper.dll` at run time. UsbDk is installed
  separately.
- **User files:**
  - `Documents\n52te keymaps\` holds backups and read images.
  - `Documents\n52te\Profiles\` holds the editor's profiles (`MainForm.ProfileDir`).
    `Documents\n52te\Macros\` holds saved macros; its path is defined in core (`SavedMacro.Folder`).
  - `%APPDATA%\n52te\editor.ini` holds the editor settings.
- **System changes (installer only, elevated):**
  - Kills `n52te Editor`, `Editor n52te`, `n52te_interception`, and the `AutoHotkey64.exe` in the
    install folder.
  - Deletes the v1 autostart (the HKCU `...\Run` value `n52te`), v1 files, and old Start menu and
    Desktop shortcuts.
  - Writes `C:\Program Files\n52te`, and creates the Start menu shortcuts `n52te Editor` and
    `n52te Keymaps (command prompt)` and a Desktop shortcut.
  - Rewrites HKLM class `UpperFilters`.
  - Deletes the services `keyboard`, `mouse` and `jmtfltr`. Runs `pnputil` to delete driver
    packages, remove devices and, after a device was removed, `/scan-devices`.
  - Backs up `System32\drivers\keyboard.sys` and `mouse.sys` to
    `%ProgramData%\n52te\removed-drivers`, then deletes them. Deletes `JmtFltr.sys` without a
    backup. A locked file is deleted on reboot (`MoveFileEx`).
  - Creates a System Restore point before removing Interception, when Windows allows one.
  - Has a **Restart now** button that runs `shutdown /r /t 5`.
- **Global hook:** while recording a macro, the editor installs a `WH_KEYBOARD_LL` hook.

## System Verification

- Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1` (builds all three exes; fails on any
  warning or a `n52te-config selftest` failure; needs no keypad, but reads the saved `.bin` files
  in `Documents\n52te keymaps`).
- Optional: set `N52TE_BELKIN_DEFAULT_INI` to Belkin's `Default.ini` to add the factory-profile
  checks.
- No automated tests cover the editor UI, the installer or `research/`.
