# installer

## Overview

`Install-n52te.exe` is a single-file WinForms setup program. It installs the n52te Editor and
`n52te-config` into `C:\Program Files\n52te` and cleans up what earlier versions left behind: the
Interception remapper and its AutoHotkey editor, the remapper's autostart and shortcuts, old Belkin
driver packages and filter entries, and, if the user agrees, the Interception driver itself.

[`build.ps1`](../../build.ps1) compiles `Installer.cs` on its own with the in-box .NET Framework C#
compiler (C# 5, `/target:winexe`). It uses `/win32manifest:src\installer\app.manifest`, references
`System.Management` and `Microsoft.CSharp`, and embeds the already-built `n52te Editor.exe` and
`n52te-config.exe` as the managed resources `RES_EDITOR` and `RES_CONFIG`. The installer does not
reference `src/core` when it compiles. It also does not bundle UsbDk; it only checks for UsbDk.

Files:

- `Installer.cs`: the whole program (`internal static class Installer`).
- `app.manifest`: sets `requestedExecutionLevel level="requireAdministrator"` and declares Windows 10/11
  support (`supportedOS {8e0f7a12-...}`).

## Interface

- **Entry point**: `Installer.Main()` (`[STAThread]`) builds the fixed-size dark window: a log
  `TextBox` and the buttons **Install**, **Restart now** (disabled until a restart is needed) and
  **Close**. **Install** disables Install and Close and starts `Run()` on a background thread.
- **`Run()`** runs six logged stages in order:
  1. `StopPrograms()` kills the processes `n52te Editor`, `Editor n52te` and `n52te_interception`,
     plus `AutoHotkey64.exe`, but only the copy in `InstallDir` (`StopPath` matches the full
     `MainModule.FileName`).
  2. `RemoveOldVersion()` deletes the HKCU `Run\n52te` value, but only if it contains
     `n52te_interception`. It deletes four shortcuts in the common Start menu (`Programs\n52te`:
     `n52te Remapper.lnk`, `Editor n52te.lnk`, `Remapeador n52te.lnk`, `n52te Editor.lnk`) and two on
     the common Desktop (`n52te Editor.lnk`, `Editor n52te.lnk`). The current `n52te Editor.lnk`
     shortcuts are recreated in stage 5. It also deletes the `OldFiles` in `InstallDir`. It keeps
     `%ProgramData%\n52te\n52te_config.ini`.
  3. `CleanBelkin()` and `CleanBelkinFilter()` remove Belkin driver leftovers (see Side Effects).
  4. `RemoveInterception()` removes the Interception driver, after the user agrees.
  5. The install stage creates `InstallDir`, calls `Extract()` for each `Payload` entry, deletes
     `OldFiles` again and calls `MakeShortcuts()`.
  6. `CheckUsbDk()` checks for UsbDk.

  Only after all six stages succeed does `Run()` enable **Restart now**, and only if `needRestart`
  is set.
- **Constants**: `InstallDir` (`C:\Program Files\n52te`), `EditorExe` (`n52te Editor.exe`),
  `ConfigExe` (`n52te-config.exe`), `UsbDkHelper`
  (`C:\Program Files\UsbDk Runtime Library\UsbDkHelper.dll`), `UsbDkPage` (UsbDk v1.0.22 release page),
  `KeyboardClass` / `MouseClass` (the HKLM class keys `{4D36E96B-...}` / `{4D36E96F-...}`).
- **`Payload`**: maps `RES_EDITOR` to `n52te Editor.exe` and `RES_CONFIG` to `n52te-config.exe`. These
  resource names must match the `/resource:` arguments in `build.ps1`.
- **`OldFiles`**: files of earlier versions in `InstallDir`, including `AutoHotkey64.exe` and an
  `InstallDir` copy of `n52te_config.ini` (that copy is deleted; the `%ProgramData%` one is kept).
- **Shortcuts made by `MakeShortcuts()`**: it first creates the Start menu folder `Programs\n52te`,
  then makes Start menu `n52te\n52te Editor.lnk`, Desktop `n52te Editor.lnk`, and Start menu
  `n52te\n52te Keymaps (command prompt).lnk`. The last one targets `cmd.exe /k` with `InstallDir`
  added to `PATH`, then runs `n52te-config info` and `n52te-config`. Every shortcut is made by
  `Shortcut()` through the `WScript.Shell` COM object (called with `dynamic`), with
  `WorkingDirectory = InstallDir`.
- **Helpers**:
  - Process: `RunProcess(exe, args)` returns the exit code. `RunCapture(exe, args)` returns stdout.
    `Stop(processName)` kills every process with that name. `StopPath(fullPath)` kills only processes
    whose main module is that exact file. Both wait up to 3000 ms per process.
  - Registry: `Filters(classKey)` reads `UpperFilters`. `SetFilters(classKey, current, remove, required)`
    rewrites it. `ServiceExists(name)` checks for a service key. `Names(list, name)` is a
    case-insensitive membership test.
  - Restore points: `CreateRestorePoint(description)` and `CountRestorePoints()` use WMI
    `root\default:SystemRestore`. `CountRestorePoints()` returns -1 if WMI cannot be queried.
  - Files: `DeleteFile(path)` deletes one file, returns whether it did, and logs any failure.
    `MoveFileEx(existing, newName, flags)` is a P/Invoke to `kernel32`; both call sites pass flag 4
    (`MOVEFILE_DELAY_UNTIL_REBOOT`) with a null new name.
  - UI: `Ask(question)` shows a Yes/No `MessageBox` on the UI thread. `Log(s)` appends a line to the log.
    `Ui(a)` marshals a call to the UI thread. `MakeButton(text, at, width)` builds a flat button.
- **Assembly metadata**: `AssemblyTitle("n52te Editor Setup")`, `AssemblyVersion("2.0.0.0")`.

## Invariants

- **When Interception is removed, the keyboard and mouse class filter lists keep `kbdclass` and
  `mouclass`.** On that path `SetFilters` removes the Interception entry (`keyboard` / `mouse`), drops
  empty entries and puts the required class driver back at index 0 if it is missing. It writes nothing
  if nothing changed. `SetFilters` runs only on the Interception removal path: if Interception is not
  present, or the user declines, the installer does not check or repair a missing `kbdclass` /
  `mouclass`.
- **Order when removing Interception**: (1) take the entries out of the filter lists, (2) delete the
  services with `sc delete keyboard|mouse`, (3) move the files `keyboard.sys` / `mouse.sys`. A filter
  list that names a filter whose driver is gone stops every keyboard (or mouse) from starting. See
  [docs/onboard-editor-plan.md](../../docs/onboard-editor-plan.md), "Safety rules".
- Interception is only removed after the user says yes in `Ask`. The installer then tries to create a
  restore point. It counts as made only if WMI returns 0 *and* the number of restore points went up.
  If it was not made, the installer asks a second time; if the user says yes, Interception is removed
  **without** a restore point.
- Stages 2 and 3 (old version and Belkin cleanup) run with no prompt and before any restore point.
  This does not follow the plan's rule "Create a restore point before any driver or registry change".
- Interception counts as present if any of these is true: `keyboard` is in the keyboard filter list,
  `mouse` is in the mouse filter list, a service named `keyboard` or `mouse` exists, or
  `%SystemRoot%\System32\drivers\keyboard.sys` or `mouse.sys` exists.
- The backup of `keyboard.sys` / `mouse.sys` is best effort. If creating the backup folder, copying or
  deleting fails, the file is scheduled for deletion on reboot instead, so no backup may exist. A
  successful copy overwrites any earlier backup of the same name.
- The installer only stops the `AutoHotkey64.exe` inside `InstallDir`, never one the user installed.
  It only deletes the autostart value if that value points at `n52te_interception`.
- `CleanBelkin` only deletes `oem*.inf` packages from `pnputil /enum-drivers` whose listing mentions
  `jmtflter`, `jmtfltr`, `usbicp` or `vhidmini`. `CleanBelkinFilter` only removes device instances under
  `USB\VID_050D&PID_0200&MI_00` / `MI_01` whose `LowerFilters` contain `JmtFltr`. It deletes the
  `jmtfltr` service whenever that service key exists, whatever happened to the device entries.
  Exit codes from `pnputil` and `sc` are logged, never checked.
- Any exception in `Run()` stops the remaining stages and is logged as `[ERROR]`. After that the
  installer changes nothing else, and Close is enabled again (`finally`). **Restart now** stays
  disabled, even if Interception was already removed in stage 4.
- Some failures are logged or ignored and do not stop `Run()`: `DeleteFile` and `Shortcut()` log and
  carry on; `Stop` / `StopPath` ignore kill errors; the **Restart now** button and opening `UsbDkPage`
  ignore errors.
- `Extract` throws if a resource is missing. If writing the target fails with an `IOException`, it
  stops the process running that file and tries again, up to 3 times (600 ms apart), then rethrows.
  Other exceptions are not retried.
- C# 5 only (in-box `csc.exe`): no string interpolation, `nameof` or other newer syntax.
- The program must run elevated (`app.manifest` `requireAdministrator`), because it writes HKLM and
  files in Program Files and System32.

## Side Effects

- **Processes**: kills the n52te and remapper processes listed above. Runs `sc delete`,
  `pnputil /enum-drivers`, `/delete-driver <oem>.inf /uninstall /force`, `/remove-device` and, only if
  a device entry was removed, `/scan-devices`. It creates a shortcut that starts `cmd.exe`, but never
  starts `cmd.exe` itself. **Restart now** runs `shutdown /r /t 5`. `CheckUsbDk` can open `UsbDkPage`
  in the browser.
- **Registry**:
  - Deletes the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `n52te`.
  - Rewrites `UpperFilters` (REG_MULTI_SZ) under the HKLM keyboard and mouse class keys.
  - Deletes the services `keyboard`, `mouse` and `jmtfltr` (through `sc delete`).
  - Belkin USB device instances under `HKLM\SYSTEM\CurrentControlSet\Enum\USB` are removed with
    `pnputil /remove-device`, not by editing the registry directly.
- **Files**:
  - Writes `C:\Program Files\n52te\n52te Editor.exe` and `n52te-config.exe`, and deletes the
    `OldFiles` there.
  - Creates the Start menu folder `Programs\n52te`, and creates and deletes `.lnk` files there and on
    the common Desktop.
  - Copies `keyboard.sys` / `mouse.sys` to `%ProgramData%\n52te\removed-drivers` and then deletes them
    from `System32\drivers`. If that fails, the file is deleted on reboot (`MoveFileEx`) without a
    backup.
  - Deletes `System32\drivers\JmtFltr.sys` without a backup, only if the `jmtfltr` service key exists.
    If the delete fails, the file is deleted on reboot (`MoveFileEx`).
- **System Restore**: tries to create a restore point (`RestorePointType` 12, `EventType` 100, i.e.
  BEGIN_SYSTEM_CHANGE) before it removes Interception. No matching END_SYSTEM_CHANGE (101) call is made.
- **Reboot**: sets `needRestart` after removing Interception. **Restart now** is enabled only if
  `Run()` then finishes without an exception. The installer never restarts Windows by itself.

## Verification

- Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1` (from the repository root). This
  compiles the installer and writes `Install-n52te.exe` and its `.sha256`. There is no automated test
  for the installer. Running it changes the system (drivers, registry, Program Files), so only run it
  by hand on a machine you are willing to change.
