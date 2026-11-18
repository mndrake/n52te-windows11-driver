# 06 - `n52teCfg.exe` editor

The n52te Editor (MFC, title `n52te Editor`, version string `V2_1_2`). It edits profiles in
memory and saves them as `.ini` ([04](04-onboard-profile-and-codes.md)). For "Apply Changes" /
"Load to Synapse" it asks the service to upload them. It has no `DeviceIoControl` calls of its
own.

## Assignable actions

Right-clicking an input opens a popup menu (built in `0x443e2b`). The command handler around
`0x443fc4` writes the record of the selected input in the **current** keymap, except for keymap
switches, which go to all three:

| Menu | Item | Command ID | `code` written | `seq` |
|---|---|---|---|---|
| | Single Key | `0x2711` | chosen key code (`0x10`–`0x9F`, or a media code) | cleared |
| | Macro | `0x2712` | opens the macro editor | |
| | *(saved macro n)* | `50000 + n` | input's **macro marker** | macro steps; p1–p3 = macro options |
| Keymap | Cycle Keymaps | `0x4E21` | `D9` (all keymaps) | cleared |
| | Blue Keymap Toggle / Momentary | `0x4E22` / `0x4E23` | `DA` / `DB` (all keymaps) | cleared |
| | Green Keymap Toggle / Momentary | `0x4E24` / `0x4E25` | `DC` / `DD` (all keymaps) | cleared |
| | Red Keymap Toggle / Momentary | `0x4E26` / `0x4E27` | `DE` / `DF` (all keymaps) | cleared |
| Mouse Button | Left / Middle / Right Mouse Button | `0x7531` / `0x7532` / `0x7533` | `D0` / `D2` / `D1` | cleared |
| | Di Button | `0x7534` | input's **PC-action marker** | `[1, button]` |
| | Di Axis Up | `0x7535` | axes X…Rz: PC-action marker. "Mouse X Axis": `D5`. "Mouse Y Axis": `D7`. "Mouse Scroll Wheel": `D4` (scroll up) | axes: `[2, axis]` |
| | Di Axis Down | `0x7536` | axes X…Rz: PC-action marker. Mouse X: `D6`. Mouse Y: `D8`. Mouse Scroll Wheel: `D3` (scroll down) | axes: `[3, axis]` |
| Basic Commands | Cut, Copy, Paste, Undo, All, Find, New, Save, Print | `0x7537`–`0x753F` | `C0`–`C8` | cleared |
| Additional Commands | Swap Windows | `0x7540` | `C9` | cleared |
| | Close Windows | `0x7541` | `CA` | cleared |
| | Open Explorer | `0x7542` | PC-action marker | `[0x12]` |
| | Run | `0x7543` | `CB` | cleared |
| | Show Desktop | `0x7544` | `CC` | cleared |
| | Zoom In / Zoom Out | `0x7545` / `0x7546` | PC-action marker | `[0x14]` / `[0x13]` |
| | Maximize / Minimize Window | `0x7547` / `0x7548` | PC-action marker | `[0x11]` / `[0x10]` |
| Media Functions | Play/Pause, Stop, Previous Track, Next Track | `0x7549`–`0x754C` | `EC`, `EA`, `E9`, `E8` | cleared |
| | Shuffle | `0x754D` | PC-action marker | `[0x15]` |
| | Volume Up / Volume Down / Mute | `0x754E`–`0x7550` | `EE` / `EF` / `ED` | cleared |
| | Load Profile | `0x7551` | PC-action marker | `[4, profile]` |
| | Load Application | `0x7552` | PC-action marker | `[5, …]` (path kept in the profile) |
| | Reset Key to Default | `0x7553` | factory code from the table at `0x4a4860` (identical to `Default.ini`). Diagonals 24–27 get their factory macro | cleared / factory macro |
| | Disable Button | `0x7554` | `00` | cleared |

So the editor's "Mouse" options, including **the wheel as an assignable action**, and all
keymap/LED functions write **firmware codes**. The keypad executes them without the PC.
Only the "Di …", Open Explorer, Zoom, Minimize/Maximize, Shuffle, Load Profile and Load
Application items depend on `n52teHid.exe`.

## Showing an assignment (`0x443fc4` area)

The label shown for an input is computed in this order:

1. code = the input's PC-action marker → name from the PC-side type (`Button %d`, axis names, …);
2. code in `0xD0`–`0xDF` → `Left Button` … `Red Keymap Momentary`;
3. code in `0xC0`–`0xCE` → command names;
4. code in `0xE8`–`0xFF` → media/WWW names;
5. otherwise → key name from the key-code table (`0x4356e0`). For the D-pad inputs, `5F`/`67`/`75`/`77` are labelled "DPad East/South/North/West".

## Macro recorder

`KBHook.dll` exports `installhook(hwnd)` and `removehook()`. It installs a `WH_KEYBOARD_LL`
hook that posts `0x40A (vkCode, flags & LLKHF_UP)` to the editor window, so the editor can
record keystrokes. Macros support delays up to 6350 ms (`0xFF` delay steps, merged into the
flags byte on upload; see [03](03-usb-protocol.md)) and the playback options in p1–p3. The
editor's "Mouse Button" panel inserts `D0`–`D2` press/release steps, the "Left Button Press /
Release" labels.

The macro options (p1–p3), the recorder's VK → code table, the real size limits, the polling
rate, Reset Keymap, Import and the profile file checksum are in
[07](07-n52tecfg-macros-and-options.md).

## Files the editor uses

* `skin_English\*.jpg|bmp|ini`: the whole UI is a bitmap skin. `main_normal1/2/3.jpg` are the
  three keymap views.
* `User_Profiles\*.ini` and `config.ini` ([04](04-onboard-profile-and-codes.md)).
* `HELP_English\Help.swf` (needs the bundled Flash 9 installer).
