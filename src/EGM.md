# src

## Overview

All product source. `build.ps1` (repository root) compiles these folders with the in-box
.NET Framework 4.x `csc` (C# 5, x64, `/warnaserror+`); there is no `.sln` or `.csproj`.
Build graph:

| Output | Compiled from | Notes |
| --- | --- | --- |
| `n52te-config.exe` | `core` + `configtool` | Console app; `n52te-config selftest` runs as the build gate |
| `n52te Editor.exe` | `core` + `onboardeditor` | WinForms app |
| `Install-n52te.exe` | `installer` only | Embeds the two programs above as resources `RES_CONFIG` and `RES_EDITOR` |

`core` is compiled into both programs, so a change there affects both. `configtool` and
`onboardeditor` don't reference each other, and `installer` doesn't reference `core`.
All top-level types are `internal`, in namespace `N52te` (installer: the `Installer` class). The
nested `public` types `Assignment.Item` and `ProfileList.Entry` are internal in effect.

## Subdirectories & Boundaries

- core: [./core/EGM.md](./core/EGM.md) - Shared keypad library. It covers the UsbDk transport
  (`IKeypadTransport`, `UsbDkTransport`), the HID protocol client (`KeypadClient`, which never
  sends command 7), the 0x1C00-byte keymap image (`KeymapImage`), the profile model
  (`Profile`, `InputRecord`, `Macro`), Belkin `.ini` profiles and macros (`ProfileIni`,
  `SavedMacro`), code tables (`FirmwareCodes`, `KeypadLayout`) and whole-profile
  read/diff/apply with backups (`KeypadSession`).
- configtool: [./configtool/EGM.md](./configtool/EGM.md) - `n52te-config.exe` command-line
  tool (`N52te.Program`). Subcommands `info`, `read`, `export`, `apply`, `macro`, `set`, `write`,
  `select`, `polling`, `roundtrip` and others. `apply`, `macro`, `set` and `write` need `--yes`.
  `roundtrip`, `select` and `polling` don't. Each keymap write is backed up first. The `selftest` subcommand (`N52te.SelfTest`) checks `core` against
  byte sequences verified on hardware and a `SimulatedKeypad`.
- onboardeditor: [./onboardeditor/EGM.md](./onboardeditor/EGM.md) - `n52te Editor.exe`, a
  WinForms editor in Belkin's v2.1.2 layout (`MainForm`, `MacroEditor`, `Skin`, `SkinDialog`).
  Work that takes the keypad goes through `MainForm.OnKeypad` on a worker thread. The presence
  check (`UsbDkTransport.IsPresent`) runs on the UI thread. Profiles are stored in
  `Documents\n52te\Profiles` and settings in `%APPDATA%\n52te\editor.ini`.
- installer: [./installer/EGM.md](./installer/EGM.md) - `Install-n52te.exe` (`Installer.Run`)
  runs elevated (`app.manifest` `requireAdministrator`). It installs both programs to
  `C:\Program Files\n52te` and removes v1 and Belkin leftovers. Belkin cleanup runs without a
  prompt. It removes the Interception driver only after the user agrees, preferably after a
  restore point; if none can be created, it asks again. When it removes Interception it keeps
  `kbdclass`/`mouclass` in the class filter lists. It checks for UsbDk but does not bundle it.
