# Handover: n52te onboard editor (cloud session → local session)

Written 18 Nov 2026 at the end of a cloud session. Branch: **`claude/n52te-driver-decompile-tejavx`**
(last commit before this file: `18cf29b`). Everything below is committed and pushed.

## 1. Safety rules (read first)

On 18 Nov every keyboard on this PC stopped working, including at the login screen. The cause:
the keyboard and mouse class filter lists still named Interception's drivers (`keyboard`,
`mouse`), but those services had an empty `ImagePath`. It was fixed by resetting the lists to
the Windows defaults. A local session may have changed them. So:

1. **Never** add, remove or edit `UpperFilters`/`LowerFilters` under
   `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E96B-E325-11CE-BFC1-08002BE10318}` (keyboard) or
   `{4D36E96F-E325-11CE-BFC1-08002BE10318}` (mouse). They must stay `kbdclass` / `mouclass`.
2. **Never** run `install-interception.exe`, `pnputil /add-driver`, `sc create/config` for input
   drivers, `bcdedit` or test-signing changes without the user's explicit OK for that exact command.
3. Before any change to drivers or the registry, create a restore point:
   `Checkpoint-Computer -Description "before n52te change" -RestorePointType MODIFY_SETTINGS` (admin).
4. **Never** send USB command 7 (`IOCTL_ENTER_ICP`, firmware update) to the keypad.
   `GuardedTransport` in the core refuses it; keep it that way.
5. Recovery if keyboards die again: log in with the on-screen keyboard (Accessibility icon on
   the login screen). Then in an admin PowerShell:
   ```powershell
   Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4D36E96B-E325-11CE-BFC1-08002BE10318}' UpperFilters -Value @('kbdclass') -Type MultiString
   Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4D36E96F-E325-11CE-BFC1-08002BE10318}' UpperFilters -Value @('mouclass') -Type MultiString
   ```
   and restart. Without any mouse: Windows Recovery → Command Prompt, then
   `reg load HKLM\OFFSYS C:\Windows\System32\config\SYSTEM` and the same values under
   `HKLM\OFFSYS\ControlSet001\Control\Class\…`, then `reg unload HKLM\OFFSYS`.

## 2. What this project is now

The old approach (Interception driver + per-key remapper, the `Instalar-n52te.exe` releases
v1.2.0/v1.3.0) is being replaced by an **editor that writes profiles into the keypad's onboard
memory**. The firmware then runs them by itself, with no driver and no remapper. An optional
**Companion** tray app covers what the firmware can't do (launch programs, window commands, long
text, per-app keymap switching, on-screen keymap notice).

Read in this order:
1. [`docs/original-driver-analysis.md`](original-driver-analysis.md): why the wheel, mouse functions,
   keymap LEDs and D-pad left failed with the Interception approach.
2. [`docs/onboard-editor-plan.md`](onboard-editor-plan.md): the plan, phases, transport options, decisions.
3. [`docs/reverse-engineering/`](reverse-engineering/README.md): protocol, profile format, key codes,
   from decompiling Belkin's v2.1.2 driver.
4. [`src/README.md`](../src/README.md): code layout and build commands.

Decisions already made by the user: **WinForms**, **.NET 10** (LTS; .NET 8 is end of life),
**Companion in the first release**, **English UI by default**, **standalone builds with the
runtime included**.

## 3. State of the code (`src/`)

| Part | State |
|---|---|
| `N52te.Core` | Done and tested (44 xUnit tests): profile model (3 keymaps × 27 inputs), firmware key codes, keymap image builder + checksums, 8-byte commands, Belkin `.ini` import/export (byte-identical with Belkin's factory profile), JSON profiles, text-to-macro, Companion hotkey chords, `KeypadClient` behind `GuardedTransport`, `MappingDetector` + `WindowsKeys.FromRawKey`, `Updates` (GitHub release check) |
| `N52te.Editor` | Builds. Never run on Windows by anyone yet. Edits all inputs, macros with recorder, typed text, Companion actions, app rules, checks panel, Belkin import/export, **Keypad → Read mapping by pressing buttons** (Raw Input, listens only to devices whose ID contains `VID_050D`) |
| `N52te.Companion` | Builds, never run. Tray app: `RegisterHotKey` for the profile's Ctrl+Alt+Shift chords, actions, app-rule watcher, keymap banner. `KeypadConnection.TryOpen()` returns null (no transport yet) |
| CI | `.github/workflows/build.yml` runs tests and publishes both apps (runtime-required and standalone) as artifacts on `windows-latest`. Three runs passed |
| Transport to the keypad | **Not started.** Waits for the probe (phase 0) and a USB capture (phase 1) |

## 4. Open items, in order

1. **User feedback in progress:**
   - *"The editor pane is not big enough to fit everything."* Partly fixed in `18cf29b` (forms
     now use `AutoScaleMode.Dpi` and fit the screen). Still to do: in `ActionEditor.cs`, replace
     the fixed-size `Info()` labels and the `note` label with `WrapLabel`, and replace `Stack()`
     (a `FlowLayoutPanel`) with a one-column `TableLayoutPanel` so labels wrap to the panel width.
     Also wrap the action editor in an `AutoScroll` panel with
     `AutoScrollMinSize = LogicalToDeviceUnits(new Size(470, 620))`. Docked children don't
     trigger AutoScroll by themselves. Then **ask the user for a screenshot** at their display
     scaling to confirm.
   - *"The editor does not have what the keypad is currently mapped to."* Answered by the new
     "Read mapping by pressing buttons". Needs testing on the real keypad. Reading the onboard
     memory directly needs the transport (item 4).
2. **One-time install + self-update** (user asked: "install the apps so I do not have to keep
   downloading"). Not built yet. Design agreed in the session:
   - Editor offers "Install on this PC": copy both exes to `%LOCALAPPDATA%\Programs\n52te`, Start
     menu shortcuts (`IShellLinkW` COM), an entry under
     `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\n52te-editor` with
     `UninstallString` = `"…\n52te-editor.exe" --uninstall`, optional Companion autostart
     (`HKCU\…\Run`, value `n52te Companion`, which is what `CompanionContext` already uses). No admin.
   - Help → Check for updates (plus a quiet check at start-up when installed): `Updates.CheckAsync`.
     Download `n52te-standalone.zip` + `SHA256SUMS.txt` and **verify the SHA-256** of both exes.
     Rename the running exes to `.old` and move the new ones in; restart Companion and Editor; delete
     `.old` on the next start. Uninstall keeps `%APPDATA%\n52te` (profiles).
   - CI: on push to this branch, build with `-p:Version=0.1.${{ github.run_number }}`, zip the
     standalone exes as `n52te-standalone.zip` with `SHA256SUMS.txt`, and create a **pre-release**
     tagged `editor-v0.1.<run>` (`gh release create … --prerelease`, job needs
     `permissions: contents: write`). Pre-releases don't become "latest", so the existing v1.3.0
     release and the README download link stay as they are. `Updates.Owner/Repository` point at
     `mndrake/n52te-windows11-driver`.
3. **Phase 0, probe:** run `research\n52te-probe.ps1 -TryGet` (admin PowerShell, keypad plugged in
   directly). It is read-only. Its output decides whether the editor can use standard HID
   (`HidD_SetFeature`) or needs the temporary WinUSB route (plan §3).
4. **Phase 1, capture:** USBPcap/Wireshark of Belkin's v2.1.2 software in a Windows XP/7 VM with the
   keypad passed through. Confirm `wIndex`, the interface, keymap colour order, option bytes
   p1–p3, the delay unit (inferred 50 ms) and what command 8 "driver mode" does
   (`docs/reverse-engineering/03-usb-protocol.md`, open questions).
5. **Phase 3, transport:** implement `IKeypadTransport` (HID or WinUSB), then enable
   Keypad → Save to keypad and `KeypadConnection.TryOpen()` in the Companion.
6. Later: import of the old Interception remapper's `%ProgramData%\n52te\n52te_config.ini`
   (offered to the user, not requested yet); Spanish translation.

## 5. Building and testing on Windows

```powershell
winget install Microsoft.DotNet.SDK.10          # or https://dotnet.microsoft.com/download/dotnet/10.0
dotnet test src\N52te.Core.Tests
dotnet build src\N52te.sln -c Release           # Release also runs the WinForms analyzers (WFO1000)
dotnet run --project src\N52te.Editor
dotnet run --project src\N52te.Companion

# standalone single-file builds (about 52 MB each)
dotnet publish src\N52te.Editor -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o out
```

The byte-exact Belkin test runs only if `N52TE_BELKIN_DEFAULT_INI` points at Belkin's
`User_Profiles\Default.ini`. Get it from the Internet Archive item `n52te_win_driver_v212`, unpacked
with `research\extract_belkin_package.py` and then `unshield` (Linux/WSL). Don't commit Belkin files.

Conventions: warnings are errors (`src/Directory.Build.props`). Keep the core free of Windows
APIs so it stays testable everywhere. Commit messages end with the `Co-Authored-By` line the
session uses.

## 6. Prompt for the local session

Paste this into Claude Code running in your local clone of the repository:

> I'm continuing work started in a cloud session. Check out branch
> `claude/n52te-driver-decompile-tejavx` and read `docs/HANDOVER.md` first, especially section 1
> (safety rules). Never change keyboard/mouse class filters or install input drivers without asking
> me for that exact command, and create a restore point before any driver or registry change.
> Then:
> 1. Build and run the tests (`dotnet test src\N52te.Core.Tests`, `dotnet build src\N52te.sln -c Release`).
> 2. Finish the editor layout fix from section 4 item 1, run the editor, and ask me for a screenshot.
> 3. Build the one-time install + self-update and the CI pre-release step from section 4 item 2.
> 4. Help me run `research\n52te-probe.ps1 -TryGet` with the keypad plugged in, and use the result to
>    choose the keypad transport (plan §3).
> Tell me before each step that changes anything outside the repository.
