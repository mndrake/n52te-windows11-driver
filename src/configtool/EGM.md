# configtool (n52te-config.exe)

## Overview

`n52te-config` is the command-line tool that reads, switches and writes the Belkin n52te's
onboard keymaps (blue 0, red 1, green 2). It contains no protocol logic of its own: it parses
arguments, prints results, takes backups, and calls the shared core library in `src/core`
(see [core contract](../core/EGM.md)) for the keypad protocol over UsbDk, keymap images,
profiles, Belkin `.ini` files, macros and code tables.

`build.ps1` compiles it from `src\core\*.cs` plus `src\configtool\*.cs` (C# 5, in-box .NET
Framework `csc`, x64, `/warnaserror+`), then runs `n52te-config selftest` as the build gate.
The installer (`src\installer\Installer.cs`) embeds the resulting exe as resource `RES_CONFIG`.

## Interface

Files:

- `N52teConfig.cs`: `N52te.Program` (internal static), the entry point. Private members:
  `Main`, `Run`, `Show`, `Apply`, `SetMacro`, `Write`, `Backup`, `HasYes`, `Option`, the
  `Usage` text and `DataDir`.
- `SelfTest.cs`: `N52te.SelfTest` (internal static) with
  `public static int Run(string defaultIni, string imagesDir, TextWriter output)`. Its check
  groups are `Layout`, `CommandBlocks`, `Macros`, `Factory`, `Images` and `Protocol`. It also
  has a private nested `SimulatedKeypad : IKeypadTransport` that stands in for the keypad.

Help is handled in `Program.Main` before `Run`, with a case-sensitive comparison: no args or
exactly `help`, `--help` or `/?`. Every other first argument goes to `Program.Run`, which
dispatches on `args[0].ToLowerInvariant()`. `selftest` and `show` are handled first; every other
command (including unknown ones) opens the keypad, prints the info line and then dispatches.

KM arguments (`read`, `select`, `roundtrip`, `set`, `write`, `macro`) go through
`KeypadLayout.ParseKeymap`: `0|1|2` or `blue|red|green`, in any case.

| Command | Needs keypad | Writes keypad | Notes |
| --- | --- | --- | --- |
| no args, `help`, `--help`, `/?` (exact case) | no | no | handled in `Main`; prints `Usage`, exit 1 |
| unknown command (including `HELP`, `Help`) | yes | no | opens the keypad and prints the info line, then prints `Usage`, exit 1 |
| `selftest [DEFAULT.INI]` | no | no | defaults to env `N52TE_BELKIN_DEFAULT_INI`; images dir = `DataDir` |
| `show FILE` | no | no | `.ini` (any case) goes to `ProfileIni.Read` and prints `Profile.Check()` warnings; anything else is decoded as a keymap image, keymap number guessed from the image header, falling back to 0 |
| `info` | yes | no | firmware reply + active keymap (printed before every keypad command) |
| `read [KM\|all] [DIR]` | yes | no | default `all` (the literal `all` is case-sensitive); saves `keymapN.bin` to DIR (default `DataDir`) |
| `export FILE.ini [NAME]` | yes | no | `ProfileIni.FromImages` + `ProfileIni.Write`; start keymap = the keypad's current `ActiveKeymap()`; NAME defaults to the file name without its extension |
| `descriptor` | yes | no | walks the configuration descriptor, prints interfaces and endpoint poll intervals |
| `listen [SECONDS]` | yes | no | default 20 s; casts `pad.Transport` to `UsbDkTransport` |
| `polling [125\|500\|1000]` | yes | with arg | maps to interval 8/2/1 ms via `SetPollingInterval` (any other value throws), then reads back `PollingRate()` |
| `select KM` | yes | yes | `SelectKeymap`, then verifies with `ActiveKeymap` |
| `apply FILE.ini --yes\|--dry-run` | yes | unless dry run | prints `Profile.Check()` warnings; writes only the keymaps that differ, then selects the profile's start keymap |
| `macro KM INPUT "STEPS" [--mode once\|held\|untilnext] [--times N] [--interval MS] --yes` | yes | yes | `Macro.Parse`; sets `Code = KeypadLayout.MacroMarker[input-1]` and clears `FirmwareSequence` |
| `set KM INPUT CODE --yes` | yes | yes | INPUT checked with `KeypadLayout.CheckInput`; CODE is hex 00-FF (checked), written at `KeymapImage.CodeOffset + KeypadLayout.Slot(input)` |
| `write FILE KM --yes` | yes | yes | FILE must be exactly `KeymapImage.Size` bytes |
| `roundtrip KM` | yes | yes | writes the read-back image unchanged and verifies it |

Exit codes: `0` success; `1` usage (help) or unknown command; `2` any exception (printed to stderr as
`error: <message>`); `3` the active keymap does not match the requested one after
`select`/`apply`; `4` a write failed verification; `6` one or more selftest checks failed.

Core types used (defined in `src/core`, not here): `KeypadClient`, `UsbDkTransport`,
`IKeypadTransport`, `KeypadLayout`, `KeymapImage`, `InputRecord`, `Profile`, `ProfileIni`,
`Macro`, `FirmwareCodes`, `Hex`.

## Invariants

- `apply`, `macro`, `set` and `write` require `--yes` (`apply` also accepts `--dry-run`).
  Without it they throw `ArgumentException` (`HasYes` checks in `Run`, `Apply`, `SetMacro`).
  The check runs after the keypad has been opened and the info line printed, but before any
  keymap is read or written.
- `roundtrip`, `select` and `polling` (with a rate argument) change keypad state without
  needing `--yes`.
- Every keymap write goes through `Program.Write`: it calls `Backup` first, then
  `KeypadClient.WriteAndVerify`. On failure it prints a ready-to-run
  `n52te-config write "<backup>" KM --yes` restore command and returns false (exit 4). Do not
  add a write path that skips `Backup`.
- `set`, `write` and `roundtrip` upload `KeymapImage.WithUploadHeader(source, km)`. `apply`
  and `macro` upload `KeymapImage.Build(records, km)`.
- `apply` skips any keymap where `KeymapImage.FirstDifference(before, image) < 0`. `--dry-run`
  writes nothing and does not change the active keymap. If a keymap write fails, `apply`
  returns 4 at once: later keymaps are not written and the start keymap is not selected.
- `macro` limits: `--times` 1-10 (stored as `times-1`, default 1). `--interval` is 50, 100,
  150, 200 or 250 ms (stored as `interval/50-1`, default 50). `--mode` (case-insensitive,
  default `once`) once/times=0, held=1, untilnext=2. Inputs are checked with
  `KeypadLayout.CheckInput`.
- `selftest`, `show` and the help forms handled in `Main` return before
  `KeypadClient.OpenUsbDk()`, so they never need UsbDk or the keypad. Every other command,
  including an unknown one, opens the keypad once in a `using` block and releases it on exit.
- `SelfTest` keeps its state in static fields (`failures`, `o`). `Run` resets them, so calls
  must not overlap.
- `SelfTest` holds expected byte sequences that were verified on hardware. Examples: select
  keymap 1 = `00 08 00 03 00 01 00 0A`, `"A 1000ms B"` uploads as `14 01 14 28 2F 01 2F 00`,
  wheel up is slot 26 and D-pad left is slot 7, and the Default.ini checksum byte is `0x9A`.
  If one of these fails, the core library has regressed. Do not edit the expectation to make
  it pass.
- `SelfTest.Protocol` requires that command 7 (firmware update) is refused by
  `KeypadClient.Control` with `InvalidOperationException` before it reaches the transport
  (`SimulatedKeypad.SawCommand7` stays false).

## Side Effects

- USB: every command except `selftest`, `show` and the help forms handled in `Main` (unknown
  commands included) takes the keypad through UsbDk
  (`KeypadClient.OpenUsbDk`). The keypad stops typing until the process exits.
- Keypad state: `select`, `apply`, `macro`, `set`, `write` and `roundtrip` rewrite the onboard
  keymaps and/or the active keymap, which persists after unplugging. `polling` with an argument
  changes the polling rate, which applies after a replug.
- Disk writes: `DataDir` = `%USERPROFILE%\Documents\n52te keymaps` (`SpecialFolder.MyDocuments`).
  It is created on demand and gets `keymap{KM}-before-{yyyyMMdd-HHmmss}.bin` backups before
  every write and `keymapN.bin` files from `read` (unless DIR is given). `export` writes the
  given `.ini` file.
- Disk reads: `show`, `apply` and `write` read user-supplied files. `selftest` reads every
  `*.bin` under `DataDir` recursively, plus the Default.ini from its argument or
  `N52TE_BELKIN_DEFAULT_INI`, so the build-time selftest depends on the building user's
  Documents folder. It reports missing inputs as `skip` and does not fail on them.
- Console: results go to stdout; exceptions go to stderr.

## Verification

- Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1` (from the repo root). This
  compiles `n52te-config.exe` and fails the build if `n52te-config selftest` exits non-zero.
  Set `N52TE_BELKIN_DEFAULT_INI` to Belkin's `User_Profiles\Default.ini` to include the
  factory-profile checks.
- No keypad needed: `build\n52te-config.exe selftest` and `build\n52te-config.exe show FILE`.
