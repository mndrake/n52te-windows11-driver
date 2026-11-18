# core (namespace `N52te`)

## Overview

Shared library for talking to the Belkin n52te keypad (USB `050D:0200`) and for its data
formats. It has no entry point of its own. `build.ps1` compiles every `src\core\*.cs` file
together with `src\configtool` (`n52te-config.exe`) and again with `src\onboardeditor`
(`n52te Editor.exe`), so a change here affects both programs. Every type is `internal`.

Layers, bottom to top:

1. **Transport** (`Transport.cs`): raw USB control transfers on endpoint 0 through UsbDk.
2. **Protocol** (`KeypadClient.cs`): HID `SET_REPORT` / `GET_REPORT` commands (command
   number in `wValue`), keymap chunk upload and read-back.
3. **Data model and codecs**: `KeypadLayout`, `FirmwareCodes`, `KeymapImage` (the
   0x1C00-byte firmware keymap image), `Profile` / `InputRecord` / `Macro`, `ProfileIni`
   (Belkin `User_Profiles\*.ini`), `SavedMacro` (Belkin `User Macros\*.ini`).
4. **Session** (`KeypadSession.cs`): whole-profile read, diff, and apply.

Protocol and format background:
`/docs/reverse-engineering/03-usb-protocol.md`,
`/docs/reverse-engineering/04-onboard-profile-and-codes.md`,
`/docs/reverse-engineering/07-n52tecfg-macros-and-options.md`.

## Interface

### Transport (`Transport.cs`)

- `IKeypadTransport : IDisposable`: `byte[] Control(byte requestType, byte request, ushort value, ushort index, byte[] dataOut, int lengthIn)`. IN requests (bit 7 of `requestType`) return up to `lengthIn` bytes. OUT requests send `dataOut` and return `null`.
- `UsbDk` (static P/Invoke wrapper around `UsbDkHelper.dll`): `IsInstalled`, `Load()`, `UsbDk_GetDevicesList`, `UsbDk_ReleaseDevicesList`, `UsbDk_StartRedirect`, `UsbDk_StopRedirect`, `UsbDk_WritePipe`, `UsbDk_ReadPipe`, `UsbDk_AbortPipe`, `UsbDk_GetRedirectorSystemHandle`, plus kernel32 `CreateEvent`, `WaitForSingleObject`, `WaitForMultipleObjects`, `CancelIoEx`, `CloseHandle`. Struct-layout constants `DeviceInfoSize` (848), `DescriptorOffset` (824), `RequestSize` (72).
- `UsbDkTransport : IKeypadTransport`: `Vid`/`Pid` (`0x050D`/`0x0200`), `static bool IsPresent()` (checks for the keypad without taking it over; returns `false` if UsbDk is not installed, otherwise loads the DLL), constructor (finds the keypad and starts the redirect), `Control(...)`, `Dispose()`, `Listen(int seconds, TextWriter o)` (diagnostic dump of interrupt endpoints 0x81 and 0x82).
- Errors: the constructor throws a plain `Exception` when UsbDk cannot list devices, the keypad is not found, or the redirect fails. `Control` throws `IOException` when the UsbDk call fails, when a pending transfer does not complete within 5 s, and when the request reports a non-zero USBD status.

### Protocol (`KeypadClient.cs`)

- `KeypadClient : IDisposable`: `KeypadClient(IKeypadTransport)`, `static OpenUsbDk()`, `Transport`, `Dispose()`.
- Commands: `Firmware()` (cmd 6), `ProfileId()` (cmd 5), `ActiveKeymap()`, `SelectKeymap(int km)` (cmd 4), `SetPollingInterval(byte ms)` (cmd 9), `PollingRate()` (cmd 10), `ConfigurationDescriptor()` (standard `GET_DESCRIPTOR`), `ReadKeymap(int km)` (cmd 3), `WriteKeymap(int km, byte[] image, TextWriter log)` (cmd 1 plus `GET_RESULT` cmd 2), `WriteAndVerify(int km, byte[] image, TextWriter log)`.
- `internal Control(...)`: the guarded path every class request goes through. It is `internal` so the self-test can check the guard.
- Helpers: `static Block(byte sub, byte value)` (8-byte command block with XOR checksum), `static ChecksumOk(byte[] r)`.

### Layout and codes

- `KeypadLayout` (`KeypadLayout.cs`): `InputCount` (27), `KeymapCount` (3), `KeymapNames` (`Blue`, `Red`, `Green` = firmware 0, 1, 2), `InputNames`, `MacroMarker[]`, `PcActionMarker[]`, `FactoryCode[]`, `Slot(int input)`, `CheckInput(int input)` (throws `ArgumentOutOfRangeException` outside 1-27), `ParseKeymap(string)` (accepts `blue`/`red`/`green` or `0`/`1`/`2`, case-insensitive; otherwise throws `ArgumentException`).
- `FirmwareCodes` (`FirmwareCodes.cs`): constants `Disabled` (0x00), `DelayStep` (0xFF), `LeftShift`, `LeftCtrl`, `LeftAlt`; `Name(int code)`, `TryParse(string token, out byte code)`, `FromVirtualKey(int vk)`, range tests `IsMouse`, `IsKeymapSwitch`, `IsCommand`, `IsMedia`.

### Keymap image (`KeymapImage.cs`)

- `KeymapImage`: `Size` (0x1C00), `ChunkSize` (0x200), `Chunks` (14), offsets `CodeOffset`, `P1Offset`, `P2Offset`, `P3Offset`, `MacroOffset`.
- `SetUploadHeader`, `IsUploadImage`, `WithUploadHeader`, `Build(InputRecord[] records, int km)`, `Parse(byte[] image)`, `FirstDifference(a, b)`, `Same(...)`, `ChunkChecksum(...)`, `Describe(...)` (two overloads), `DescribeRecord(int input, InputRecord r)`, `Warnings(InputRecord[])`.
- Guards: `Build` throws `ArgumentException` unless given exactly 27 records. `Parse` and `WithUploadHeader` throw `ArgumentException` unless the image is exactly 0x1C00 bytes.

### Profile model (`Profile.cs`)

- `InputRecord`: `Size` (0x104), `SequenceSize` (0x100), fields `Code`, `Sequence` (profile-form macro), `Options` (3 bytes, editor form; default `{1, 1, 0}`), `FirmwareSequence` (raw firmware macro bytes, or `null`); `Clone()`, `CopyTo(blob, offset)`, `static FromBlob(blob, offset)`, `static DescribeOptions(byte[])`.
- `Profile`: `Name`, `Exe`, `SwitchFlag`, `StartKeymap`, `Keymaps` (`InputRecord[3][27]`); `static Factory()` (Belkin's `Default.ini` layout), `Check()` (warnings to show before writing: invalid macro mode byte, PC-action markers, command codes, inconsistent keymap switches).
- `Macro`: `MaxPairs` (128), `MaxDelayUnits` (127), `DelayUnitMs` (50); `ToFirmware(byte[] profile)`, `FromFirmware(byte[] image, int offset)`, `Parse(string text)`, `ToText(byte[] profile)`, `Describe(byte[] profile)`.
- `Hex`: `Of(byte[])`, `Of(byte[], int offset, int count)` (lower-case, space-separated).

### Files

- `ProfileIni` (`ProfileIni.cs`): `RecordsSize` (0x5244), `BlobSize`; `Read(string path, List<string> warnings)`, `Write(Profile p, string path)`, `ToText(Profile)`, `FromBlob(byte[])`, `ToBlob(Profile)`, `Checksum(byte[])`, `FromImages(byte[][] images, int startKeymap, string name, List<string> warnings)`.
- `ProfileIni.Read` throws `InvalidDataException` when the `[macro] Value=` entry is missing, is not valid hex, or has the wrong size. It ignores a `Key map` value outside 0-2 without a warning.
- `SavedMacro` (`MacroFile.cs`): fields `Name`, `Sequence`, `Options` (a new instance defaults to `{0, 0, 0}`), `IgnoreDelay`; `static Folder`, `static PathFor(name)`, `static List()` (names without `.ini`, sorted case-insensitively; empty if `Folder` does not exist), `static Read(path)`, `Write(path)`, `static PressesMoreThanReleases(byte[] seq)`.
- `SavedMacro.Read` throws `InvalidDataException` when `Macro=` is missing or shorter than 0x100 bytes of hex. It does not verify the checksum. Missing `Behaviour`, `RepeatTimes`, `RepeatInterval` and `IgnoreDelay` keys default to 0, 1, 1 and 0.

### Session (`KeypadSession.cs`)

- `KeypadSession`: `BackupDir`, `Read(KeypadClient pad, string name, List<string> warnings)`, `Differences(Profile before, Profile after)`, `Apply(KeypadClient pad, Profile p, Action<string> log)` (returns `null` on success, otherwise an error message; a keymap write failure includes an `n52te-config write` restore command for that keymap's backup, a failed start-keymap check does not).

## Invariants

- **Never enter firmware-update mode.** `KeypadClient.Control` throws `InvalidOperationException` for any class request (`(requestType & 0x60) == 0x20`) with `value == 7` (`ENTER_ICP`). All class requests (`Get`, `Set`) must go through this method. Do not call `transport.Control` directly for class requests. The only direct call is the standard `ConfigurationDescriptor()` request.
- **C# 5 only.** The code is built by the .NET Framework 4.x `csc` (the `FirmwareCodes.cs` header states the C# 5 rule) with `/warnaserror+` (`build.ps1`). Do not use newer syntax such as `?.`, string interpolation, or expression-bodied members.
- **Recipient selection.** `Get` and `Set` use the interface recipient (`0xA1`/`0x21`) when the low byte of `wIndex` is 0 or 1, and the device recipient (`0xA0`/`0x20`) otherwise.
- **Keymap numbering.** Firmware keymap 0 is Blue, 1 is Red, 2 is Green (`KeypadLayout.KeymapNames`). Inputs are numbered 1 to 27 (`CheckInput`). An input's slot in the image is `MacroMarker[input-1] - 0xA0` (`Slot`).
- **Image shape.** An image is exactly `KeymapImage.Size` (0x1C00) bytes, sent and read in 14 chunks of 0x200 bytes with `wIndex = (km << 8) | chunk`. `WriteKeymap` throws `ArgumentException` for any image that fails `IsUploadImage`, which checks the length, byte 0 = 0x1C, byte 1 = 0x1B (27) and byte 4 = `km` (not bytes 2-3; `SetUploadHeader` writes `1C 1B 00 01 KM`). `ReadKeymap` throws `IOException` if a chunk comes back short. Read-back headers differ, so `FirstDifference` compares only from `CodeOffset` (0x08) onward.
- **Option bytes.** `InputRecord.Options` holds the editor form. `KeymapImage.Build` adds 1 to each byte and `Parse` subtracts 1.
- **Lossless macros.** `KeymapImage.Parse` keeps raw bytes in `FirmwareSequence` when `Macro.FromFirmware` returns `null` or the round trip changes the bytes. `Build` writes `FirmwareSequence` in place of `Sequence` when it is set. `ProfileIni.ToBlob` and `InputRecord.CopyTo` write only `Sequence`, so raw macros are lost in an `.ini`. `FromImages` adds a warning (from `KeymapImage.Warnings`) only for inputs whose code is their `MacroMarker`. Raw bytes on other inputs, such as PC-action markers, are dropped without a warning.
- **Macro forms.** The profile form uses `(code, 1/0)` press/release pairs with `(0xFF, units)` delay steps of 50 ms. The firmware form stores each delay in bits 1-7 of the previous step's flags. `Macro.ToFirmware` adds the matching release when a macro ends on a press, but only if fewer than 128 steps are used. `Macro.Parse` rejects a leading delay, delays of 0 ms or less or not multiples of 50 ms, more than 127 delay units after one step (6350 ms), unknown key names and tokens that resolve to 0x00 or 0xFF, an empty macro, and more than 128 steps. `Macro.FromFirmware` returns `null` when non-zero bytes follow the end marker or the profile form would overflow 0x100 bytes.
- **Checksums.** Command blocks and replies use `b6 = b0^b2^b4`, `b7 = b1^b3^b5` (`Block`, `ChecksumOk`). Chunk `GET_RESULT` is the XOR of the even-offset bytes and the XOR of the odd-offset bytes (`ChunkChecksum`). The profile `.ini` and macro `.ini` both end their hex with the 8-bit sum of the data bytes (`ProfileIni.Checksum`, `SavedMacro.Write`). Belkin's editor rejects a file whose checksum does not match. `ProfileIni.Read` only warns on a mismatch.
- **Value ranges.** `SetPollingInterval` accepts only 1, 2, or 8 ms. `ActiveKeymap` throws `IOException` if the reply checksum is bad or the keymap is greater than 2.
- **Profile checks.** `Profile.Check` reports an input that has a keymap-switch code (0xD9-0xDF) in one keymap but a different code in another keymap. It also reports a non-empty macro whose mode byte (`Options[0]`) is greater than 2, any PC-action marker, and any command code (0xC0-0xCE).
- **Apply order.** `KeypadSession.Apply` handles each keymap in turn: read the current image, skip it if it already matches, write a backup `.bin`, then `WriteAndVerify`. It returns at the first failed keymap: later keymaps are not written and `SelectKeymap` is not called. Only after all three keymaps succeed does it call `SelectKeymap(p.StartKeymap)` and confirm the result with `ActiveKeymap()`.

## Side Effects

- **USB device takeover.** The `UsbDkTransport` constructor calls `UsbDk_StartRedirect`. From then until `Dispose()` (`UsbDk_StopRedirect`) or process exit, the keypad sends no input to Windows. `Dispose()` also releases the device list and closes the transfer event. `IsPresent()` only lists devices and does not take over the keypad.
- **Native DLL load.** `UsbDk.Load()` calls `LoadLibrary` on the fixed path `C:\Program Files\UsbDk Runtime Library\UsbDkHelper.dll`. It throws if UsbDk is not installed.
- **Unmanaged memory and handles.** `Control` allocates `HGlobal` buffers and frees them in `finally`, and waits up to 5 s on an event. `Listen` aborts pending pipe reads with `UsbDk_AbortPipe` and waits up to 2 s for them; if they have not all completed, it calls `CancelIoEx` on the redirector system handle and waits up to 2 s more. This keeps the redirect from being left in place. `Listen` frees none of its native allocations: its read buffers, request blocks, `OVERLAPPED` blocks and both events leak.
- **Keypad state changes.** `WriteKeymap` and `WriteAndVerify` overwrite onboard keymap storage, making up to 3 attempts. Each attempt stops at the first chunk whose `GET_RESULT` does not match, and the next attempt resends the whole keymap. A failed attempt is followed by a 500 ms `Thread.Sleep`, including the third. `SelectKeymap` changes the active keymap. `SetPollingInterval` changes a setting that persists across unplugging and takes effect on reconnect.
- **Filesystem writes.**
  - `KeypadSession.Apply` creates `Documents\n52te keymaps\` and writes `keymap{km}-before-{yyyyMMdd-HHmmss}.bin` there.
  - `SavedMacro.Write` creates its parent directory and writes the file. The default folder is `Documents\n52te\Macros\` (`SavedMacro.Folder`).
  - `ProfileIni.Write` overwrites the target path.
  - All `.ini` input and output uses `Encoding.Default` (the ANSI code page) with CRLF line endings.
- **Filesystem reads.** `ProfileIni.Read`, `SavedMacro.Read`, `SavedMacro.List` (reads the `Folder` directory), and `UsbDk.IsInstalled` (`File.Exists`).
- **Console and log output.** `Listen`, `KeymapImage.Describe`, and `WriteKeymap` write to the `TextWriter` passed in. `Apply` reports progress through its `log` callback.

## Verification

- Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1` from the repository root. It compiles `src\core` into both programs with warnings as errors and runs `n52te-config selftest` (`src\configtool\SelfTest.cs`), which exercises this library against a simulated keypad without hardware. Set `N52TE_BELKIN_DEFAULT_INI` to Belkin's `Default.ini` to include it in the test.
