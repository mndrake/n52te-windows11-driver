# 05 - `n52teHid.exe` background service

An MFC application (version resource: `razerhid MFC Application`, Razer USA, 2004) that runs in
the background. It owns the connection to `\\.\JmtFilter`. It uploads profiles to the keypad
and performs every action the firmware can't do itself. The editor never talks to the driver
directly. It finds the service window and posts messages to it, and the service posts back to
the window titled `n52te Editor`.

## Startup and the event loop

1. `0x405260`: `CreateFileA("\\.\JmtFilter")`, then `IOCTL 0x222400` (`MJ HOOK`) with a
   4-byte event handle. This enables the driver's input hook.
2. A worker thread (`0x4050c0`) loops: wait on the event → `IOCTL_WT_READ` (`0x222408`) →
   dispatch the 4-byte code to `0x404d30`.
3. On exit, `0x4052e0`: `IOCTL 0x222404` (unhook) and close the handle.

## Event codes (dispatcher `0x404d30`)

| Code | Source | Handling |
|---|---|---|
| `0xBD` | driver: `IRP_MN_START_DEVICE` | "Insert USB Keyboard": `Sleep(8000)`, `GET_PROFILEID` (`0x405cc0`, adopts the keypad's active keymap if the checksum is valid and the value < 3) and `GET_FWVERSION` (`0x405fb0`), then **`SET_DRIVER_MODE`**, start the tray icon `n52teTra.exe` unless disabled in `HKCU\Control Panel\…\CHTrayEnabled`, 2 s timer, post `0x50C (1)` to the editor |
| `0xBE` | driver: `IRP_MN_REMOVE_DEVICE` | "Remove USB Keboard": close the tray window `RazerJomanthaTrayIcon`, post `0x50C (0)` to the editor |
| `1`–`27` | driver: keyboard usage `0xC4 + n` | Run the PC-side action of input *n* in the active keymap (`0x404970`, table below), remember *n* |
| `0xFF99` | driver: all keys released | If the remembered input was a joystick action (types 1–3, 6–9), send "all centred / released" to the virtual joystick (`0x404870`) |
| `0xA1`–`0xAE` | not produced by the shipped driver | `0x403fa0`: an older per-key action table (load profile, launch, Ctrl+X/C/V/Z/A/F, Alt+Tab, Alt+F4, Win+E/R/D). Its source in this version is unknown |
| `0` | – | Release a pending Alt (after Alt+Tab) |

## PC-side action types (`0x404970`)

A record whose code is the input's PC-action marker ([04](04-onboard-profile-and-codes.md))
carries `type = seq[0]`, `param = seq[1]`:

| Type | Editor menu item | What the service does |
|---|---|---|
| 1 | Di Button | Virtual joystick button `param + 1` pressed (code `0x101 + param`) |
| 2 | Di Axis Up (axes X…Rz) | Joystick axis `param` (0 = X … 5 = Rz) to minimum |
| 3 | Di Axis Down (axes X…Rz) | Joystick axis `param` to maximum |
| 4 | Load Profile | Load and upload profile `param` (`0x404420`): writes `Current`/`Onboard Profile` to `config.ini`, posts `0x4C8` to the editor, shows the OSD and a progress window, uploads all three keymaps (3 tries each), then `SET_PROFILEID(start keymap)` |
| 5 | Load Application | `WinExec` the program path stored with the profile, if the file exists |
| 6 / 7 / 8 / 9 | none in v2.1.2 (the editor's reset code has unreachable cases for them) | Joystick up+right / down+right / down+left / up+left |
| `0x10` | Minimize Window | `WM_SYSCOMMAND SC_MINIMIZE` to the foreground window, if it has a minimise box |
| `0x11` | Maximize Window | `WM_SYSCOMMAND SC_MAXIMIZE`, if it has a maximise box |
| `0x12` | Open Explorer | Win+E |
| `0x13` | Zoom Out | Ctrl + numpad `-` or Ctrl + wheel −120. Which one depends on the foreground window class; special cases include Visio, FrontPage, Internet Explorer and MFC MDI apps |
| `0x14` | Zoom In | Ctrl + numpad `+` or Ctrl + wheel +120 |
| `0x15` | Shuffle | Windows Media Player `WM_COMMAND 0x499A`; the same routine also drives iTunes, Winamp and an Alt+P,H shortcut |

Joystick actions are released on `0xFF99`. Everything else is a one-shot action on press.

None of these can work on Windows 11 without a replacement for `JmtFltr.sys`. The keyboard
usages `0xC5`–`0xDF` that signal them are dropped by `kbdhid.sys`, before any user-mode hook or
Interception sees them.

## Virtual joystick reports (`0x401060` / `0x4013d0`)

The service enumerates HID devices (`SetupDiGetClassDevs` on the HID GUID), opens each one, and
writes to the first one whose top-level collection is Usage Page 1 / Usage 4 (the `vhidmini`
joystick, see [01](01-package-contents.md)). The report is `OutputReportByteLength` bytes long:

| Byte | Value |
|---|---|
| 0 | Report ID `0x02` |
| 1–6 | X, Y, Z, Rx, Ry, Rz (centre `0x80`, min `0x00`, max `0xFF`) |
| 7 | `0x00` |
| 8 | Hat, `0x08` = centred |
| 9–12 | Buttons, bit 0 of byte 9 = button 1 |
| rest | `0x00` |

The internal action codes passed to `0x401060`:

| Code | Effect |
|---|---|
| `0x01`–`0x06` | Axis 1–6 to `0x00` |
| `0x11`–`0x16` | Axis 1–6 to `0xFF` |
| `0x101`–`0x120` | Button 1–32 |
| `0x201`…`0x208` | Up, up+right, right, down+right, down, down+left, left, up+left (via X/Y) |
| `0xFFFF` | Everything centred/released |

Each call sends one complete state, so only one joystick input is active at a time.

## On-screen display

`0x4046f0` sends `WM_COPYDATA` to the window titled `OSDMain` (starting `OSD.exe` up to five
times if it isn't running). The payload is the text (e.g. the profile name), 2000 ms duration,
colour `0xFF00` and font "System", size 25.

## Registry and files

* Profiles and settings: `%ALLUSERSPROFILE%\Application Data\n52te\User_Profiles\` (from v2.1.1;
  see the release notes in the package README).
* `HKCU\Control Panel\…` value `CHTrayEnabled` controls the tray icon.
