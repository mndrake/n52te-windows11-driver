# 04 - Onboard profile and key codes

The keypad stores **3 keymaps × 27 inputs** in its onboard ("Synapse") memory and executes them
itself. The editor keeps the same data in each profile `.ini`, and
[`research/decode_n52te_profile.py`](../../research/decode_n52te_profile.py) decodes it.

## The 27 inputs

The order is the record order in the profile `.ini`, and the `n` in the driver's `0xC4 + n`
events. It was established from the factory profile, the manual's default layout and the
editor's per-input marker tables, whose values follow the physical key matrix.

**The upload image (and the keypad's memory) uses a different order:** each input sits at
slot = its macro marker − `0xA0` (input 2 → slot 4, input 23 → slot 7, …). The full table and
how it was verified are in [03](03-usb-protocol.md#keymap-image).

| n | Physical input | Factory action | Macro marker | PC-action marker |
|---|---|---|---|---|
| 1–5 | Keys 01–05 (top row) | Tab, Q, W, E, R | `A0 A4 A8 AC B0` | `7C 7E 80 81 82` |
| 6–10 | Keys 06–10 | Caps Lock, A, S, D, F | `A1 A5 A9 AD B1` | `84 85 87 89 8A` |
| 11–14 | Keys 11–14 | L Shift, Z, X, C | `A2 A6 AA AE` | `8B 8C 8D 8F` |
| 15 | Round thumb button | L Alt | `A3` | `90` |
| 16 | Wheel up | **firmware Scroll up (`D4`)** | `BA` | `91` |
| 17 | Wheel click | **firmware Middle Button (`D2`)** | `B8` | `92` |
| 18 | Wheel down | **firmware Scroll down (`D3`)** | `B9` | `93` |
| 19 | Thumb pad | Space | `B2` | `94` |
| 20 | D-pad up | Up | `B3` | `95` |
| 21 | D-pad right | Right | `AB` | `96` |
| 22 | D-pad down | Down | `AF` | `97` |
| 23 | D-pad left | Left | `A7` | `99` |
| 24 | D-pad up+right | macro `+Up +Right -Up -Right` | `B4` | `9B` |
| 25 | D-pad down+right | macro `+Down +Right -Down -Right` | `B5` | `9D` |
| 26 | D-pad down+left | macro `+Down +Left -Down -Left` | `B6` | `9E` |
| 27 | D-pad up+left | macro `+Up +Left -Up -Left` | `B7` | `9F` |

The diagonals are separate firmware inputs. Their factory macros press and release both
directions at once, so holding a diagonal does **not** hold the arrow keys (unless the option
bytes, below, select "repeat while held").

## Record layout

Each input is a 0x104-byte record. Keymap *k* input *n* (1-based) starts at
`(k × 27 + n − 1) × 0x104` in the profile blob:

| Offset | Size | Meaning |
|---|---|---|
| `+0x000` | 1 | **code**: what the input does (below) |
| `+0x001` | 256 | **seq**: 128 × `(code, flags)` steps. `flags` bit 0: 1 = press, 0 = release. A step with code `0xFF` is a delay, in the editor copy only |
| `+0x101` | 3 | option bytes p1, p2, p3 (factory `01 01 00`): macro playback mode, run count and repeat interval ([07](07-n52tecfg-macros-and-options.md#1-macro-behaviour-options--p1-p2-p3)); uploaded +1, behaviour tested on the keypad ([below](#macros-18-nov-2026)) |

How the `code` byte is interpreted:

| `code` | Meaning | Who executes it |
|---|---|---|
| `0x00` | Disabled ("Disable Button") | – |
| key code `0x10`–`0x9F` | Single key | firmware → keyboard interface MI_00 |
| = **macro marker** of this input (`0xA0`–`0xBA`) | Play `seq` as a macro | firmware |
| = **PC-action marker** of this input (`0x7C`–`0x9F`) | PC-side action: `seq[0]` = type, `seq[1]` = parameter | **PC** (`n52teHid.exe`); the firmware reports keyboard usage `0xC4 + n` (*inferred*) |
| `0xC0`–`0xCE` | Command (Cut, Copy, …, Show Desktop) | **nobody on Windows 11**: the keypad sends nothing for the ones tested (hardware); presumably Belkin's service ran them |
| `0xD0`–`0xD8` | Mouse button / wheel / pointer movement | **firmware → mouse interface MI_01** (hardware) |
| `0xD9`–`0xDF` | Keymap switch (cycle, or Blue/Green/Red toggle/momentary) | **firmware; the LED follows the keymap** (hardware) |
| `0xE8`–`0xFF` | Media / WWW / application keys | firmware, consumer keys (hardware for the ones tested) |

The PC-action markers overlap the key-code range (`0x8F` is also "Mute", `0x9B`–`0x9F` the
editor's "DPad NE…NW" labels). The editor and firmware disambiguate by checking the marker
*for that input first*. The PC-side action types are listed in [05](05-n52tehid-service.md).

## Profile `.ini` files

```ini
[Setting]
Name=Default
Exe=                  ; application linked for auto-switching
Switch Flag=0
Key map=0             ; start keymap
[macro]
Value=<hex>           ; 3 × 27 × 0x104 bytes + 1 trailing byte (0x5245 bytes)
```

The factory profile (`User_Profiles/Default.ini`) has three **identical** keymaps containing the
layout above, and trailing byte `9A`. It contains **no keymap-switch input**, so a keypad on
factory settings can never leave keymap 0, and its LED never changes colour.

`User_Profiles/config.ini` holds editor settings: `Totle` (profile count), `Current`,
`Auto Switch`, `Show Screen` (OSD), `Polling Rate` (0/1/2), `FWVersion`, `Plug`,
`Onboard Profile`, and a `[Profile]` list.

## Firmware key codes

Extracted from the editor's code→name tables (`n52teCfg.exe` `0x4356e0` and the action-label
function around `0x443fc4`). The key numbering is Razer's own, not HID usages. Codes not listed
had no name in the editor. Labels are the editor's spelling, e.g. `(8)` = numpad 8, `(Dos/v)` =
Japanese keyboard.

### Keys (`0x10`–`0x9F`)

| Code | Name | Code | Name | Code | Name | Code | Name |
|---|---|---|---|---|---|---|---|
| `10` | ~ | `11` | 1 | `12` | Q | `13` | TAB |
| `14` | A | `15` | ESC | `16` | Z | `17` | (MuHenkan) |
| `18` | F1 | `19` | 2 | `1A` | W | `1B` | Caps Lock |
| `1C` | S | `1E` | X | `20` | F2 | `21` | 3 |
| `22` | E | `23` | F3 | `24` | D | `25` | F4 |
| `26` | C | `27` | Hanguel(Korean) | `28` | 5 | `29` | 4 |
| `2A` | R | `2B` | T | `2C` | F | `2D` | G |
| `2E` | V | `2F` | B | `30` | 6 | `31` | 7 |
| `32` | U | `33` | Y | `34` | J | `35` | H |
| `36` | M | `37` | N | `38` | = | `39` | 8 |
| `3A` | I | `3B` | ] | `3C` | K | `3D` | F6 |
| `3E` | , | `3F` | backtick (Dos/v) | `40` | F8 | `41` | 9 |
| `42` | O | `43` | F7 | `44` | L | `45` | Henkan(Dos/v) |
| `46` | . | `47` | Katakana(Dos/v) | `48` | - | `49` | 0 |
| `4A` | P | `4B` | [ | `4C` | ; | `4D` | ' |
| `4E` | `$`/`}` (102-key) | `4F` | / | `50` | F9 | `51` | F10 |
| `52` | V-(Dos/v) | `53` | Backspace | `54` | `\` | `55` | F5 |
| `56` | Enter | `57` | Space Bar | `58` | Insert | `59` | F12 |
| `5A` | (8) | `5B` | (5) | `5C` | (2) | `5D` | (0) |
| `5E` | (/) | `5F` | Right | `60` | Delete | `61` | F11 |
| `62` | (7) | `63` | (4) | `64` | (1) | `65` | NA |
| `66` | Num Lock | `67` | Down | `68` | Pageup | `69` | Pagedown |
| `6A` | (9) | `6B` | (6) | `6C` | (3) | `6D` | (.) |
| `6E` | (*) | `6F` | (-) | `70` | Home | `71` | End |
| `72` | (+) | `73` | NA | `74` | (Enter) | `75` | Up |
| `76` | Pause/Break | `77` | Left | `78` | NA | `79` | Scroll Lock |
| `7A` | PrtScr | `7B` | NA | `7C` | NA | `7D` | L Alt |
| `7F` | R Alt | `83` | L Shift | `86` | R Shift | `87` | Profile |
| `88` | L Ctrl | `8E` | R Ctrl | `8F` | Mute | `9A` | App |
| `9B` | DPad NE | `9C` | Win | `9D` | DPad SE | `9E` | DPad SW |
| `9F` | DPad NW |  |  |  |  |  |  |

### Special codes (`0xC0`–`0xFF`)

| Code | Name | Handled by |
|---|---|---|
| `C0` | Cut | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C1` | Copy | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C2` | Paste | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C3` | Undo | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C4` | All | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C5` | Find | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C6` | New | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C7` | Save | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C8` | Print | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `C9` | Swap Windows | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `CA` | Close Windows | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `CB` | Run | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `CC` | Show Desktop | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `CD` | Maximize Windows | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `CE` | Minimize Windows | command: nothing reaches Windows (C0–C3, C5, C9, CB tested; [below](#verified-on-hardware-17-nov-2026)) |
| `D0` | Left Button | **mouse (firmware → MI_01)** |
| `D1` | Right Button | **mouse (firmware → MI_01)** |
| `D2` | Middle Button | **mouse (firmware → MI_01)** |
| `D3` | Scroll down | **mouse (firmware → MI_01)** |
| `D4` | Scroll up | **mouse (firmware → MI_01)** |
| `D5` | X-axis left | **mouse (firmware → MI_01)** |
| `D6` | X-axis right | **mouse (firmware → MI_01)** |
| `D7` | Y-axis left | **mouse (firmware → MI_01)** |
| `D8` | Y-axis right | **mouse (firmware → MI_01)** |
| `D9` | Cycle Keymaps | **keymap switch (firmware, sets LED)** |
| `DA` | Blue Keymap Toggle | **keymap switch (firmware, sets LED)** |
| `DB` | Blue Keymap Momentary | **keymap switch (firmware, sets LED)** |
| `DC` | Green Keymap Toggle | **keymap switch (firmware, sets LED)** |
| `DD` | Green Keymap Momentary | **keymap switch (firmware, sets LED)** |
| `DE` | Red Keymap Toggle | **keymap switch (firmware, sets LED)** |
| `DF` | Red Keymap Momentary | **keymap switch (firmware, sets LED)** |
| `E8` | Next Track | media / WWW |
| `E9` | Pre Track | media / WWW |
| `EA` | Stop | media / WWW |
| `EB` | Eject | media / WWW |
| `EC` | Play/Pause | media / WWW |
| `ED` | Mute | media / WWW |
| `EE` | Volume Up | media / WWW |
| `EF` | Volume Down | media / WWW |
| `F0` | Media | media / WWW |
| `F1` | Mail | media / WWW |
| `F2` | Calculator | media / WWW |
| `F3` | Open Explorer | media / WWW |
| `F4` | Zoom In | media / WWW |
| `F5` | Zoom out | media / WWW |
| `F6` | Zoom 100% | media / WWW |
| `F7` | Rotate Picture | media / WWW |
| `F8` | Search | media / WWW |
| `F9` | Shuffle | media / WWW |
| `FA` | WWW Home | media / WWW |
| `FB` | WWW Back | media / WWW |
| `FC` | WWW Forward | media / WWW |
| `FD` | WWW Stop | media / WWW |
| `FE` | WWW Refresh | media / WWW |
| `FF` | WWW Favorite | media / WWW |

The keymap-switch codes are always written to **all three keymaps** of the profile at once
(editor `0x4e21`–`0x4e27` handlers loop over the keymaps). That matches the manual: "Keys that
are selected for one of these Keymap switching options will be dedicated to this function
across all three Keymaps".

## Verified on hardware (17 Nov 2026)

Test layouts were written with `n52te-config` (UsbDk, Memory Integrity off) to keymap 1 (Red)
and keymap 2 (Green); keymap 0 (Blue) kept the owner's layout. What Windows received was logged
with `research/N52teMonitor.cs` (Raw Input), and the LED colours were reported by hand. Afterwards
Red and Green were restored from the backups taken before the test; a read-back on 18 Nov matched
them apart from the header bytes the upload writes.

All codes below had the factory option bytes `01 01 00`.

| Code | Name in the editor | On the keypad |
|---|---|---|
| `D0` / `D1` / `D2` | Left / Right / Middle Button | real mouse clicks, held for as long as the input is held |
| `D3` / `D4` | Scroll down / up | scroll, but repeats very fast while held: one short press gave 14 wheel steps |
| `D5` / `D6` | X-axis left / right | moves the pointer **right** / **left** (the editor's names are reversed) |
| `D7` / `D8` | Y-axis left / right | moves the pointer **down** / **up** |
| `C0` `C1` `C2` `C3` `C5` | Cut, Copy, Paste, Undo, Find | **nothing** reaches Windows |
| `C9` / `CB` | Swap Windows / Run | **nothing** reaches Windows |
| `EE` / `EF` / `ED` | Volume Up / Down / Mute | real consumer keys (volume up, volume down, mute) |
| `F2` | Calculator | consumer key; opens Calculator |
| `FB` | WWW Back | consumer key; browser Back |
| `00` | Disable Button | nothing |
| `D9` | Cycle Keymaps | Red → Green (so keymap 2 is Green) |
| `DA` / `DC` / `DE` | Blue / Green / Red Keymap Toggle | switch to that keymap and stay; the LED follows. Pressing the switch for the keymap already active keeps it |
| `DB` / `DD` / `DF` | Blue / Green / Red Keymap Momentary | that keymap (and LED colour) while held, back to the previous one on release |

The LED follows switches made with the keypad's own buttons, not only `SET_PROFILEID` from the PC.

**Why switches go into all three keymaps.** In the test, the round thumb button was "Blue
Keymap Momentary" (`DB`) in Red only; in Blue it is the owner's middle button. Holding it
switched to Blue, where the same button is a middle click, and the keypad then flipped between
Red and Blue at about the polling rate, sending hundreds of middle clicks. A switch input must
have the same switch code in every keymap, as the original editor writes it.

**Not tested yet:** the other commands (`C4` and `C6`–`CE` apart from `C9`/`CB`), which are
expected to send nothing like the rest. The remaining media codes and the polling rate were
tested on 18 Nov, below. (A first macro attempt on 17 Nov was void: nothing from the keypad reached Windows at the time, a PC-side fault,
see [profile-tool-design.md](../profile-tool-design.md). Macros were tested again on 18 Nov,
below.)

### Macros (18 Nov 2026)

Written with `n52te-config write` to keymap 1 (Red), keys 01–06, with Memory Integrity **on**;
Red was restored from its backup afterwards. The macro was `35 01 35 00 3A 01 3A 00` ("hi": H
down, H up, I down, I up) in the input's slot, with the input's code set to its macro marker.
Option bytes are the raw values in the image (offsets `0x23`/`0x3E`/`0x59`), which are the
editor's p1–p3 **+ 1** ([07](07-n52tecfg-macros-and-options.md#1-macro-behaviour-options--p1-p2-p3)).

| Raw option bytes | Editor meaning | On the keypad |
|---|---|---|
| `01 01 00`, `01 01 01` | Run Times, 1 time | plays once per press; holding doesn't repeat |
| `01 03 01` | Run Times, 3 times | `hihihi` per press |
| `01 0A 05` | Run Times, 10 times, interval 250 ms | `hi` × 10, all at once: **the interval doesn't apply to Run Times** |
| `02 02 01` | Repeat while pressed, 50 ms | one `hi` on a tap; held about 2 s: `hi` × 21 |
| `02 01 01` | Repeat while pressed, 50 ms | held about 2 s: `hi` × 36 |
| `02 01 05` | Repeat while pressed, 250 ms | held about 2 s: `hi` × 4, so the interval slows the repeat (exact timing not measured) |
| `03 01 01` | Repeat until next key is pressed | repeats until another keypad key is pressed (`hi` × 84, then that key's `h`) |
| `00 00 00` | – | **nothing**: a raw p1 of 0 disables the macro |

Also verified: a delay inside a macro. `14 01 14 28 2F 01 2F 00` typed `a`, paused about one
second, then typed `b`: the release of A carries flags `0x28` = delay 20 × 50 ms (bits 1–7) after
the step, as described in [03](03-usb-protocol.md). The keypad stored and returned every byte of
these macros unchanged.

So, for a new tool: write option bytes as editor value + 1 (mode 1–3, count 1–10, interval
1–5), and never leave p1 at 0 on a macro input. The `01 01 00` stored on every input of the test
keypad is "Run Times, once" for macros and is ignored by plain keys.

Mouse-button steps inside a macro work too: `D0 01 D0 00` left-clicked, `D1 01 D1 00`
right-clicked, and `35 01 35 00 D2 01 D2 00` typed `h` and then middle-clicked (raw options
`01 01 01`).

### Media codes (18 Nov 2026)

Every remaining code in `E8`–`FF` was put on Red and pressed once, with
`research/N52teMonitor.cs` logging the raw HID report. All of them arrive on interface 1
(collection 2, report ID `02`) as a **consumer-page usage**, so the firmware sends them without
any PC software. The "Windows key" column is what Windows turned the usage into, if anything:

| Code | Editor name | Consumer usage | Windows key |
|---|---|---|---|
| `E8` | Next Track | `0B5` Scan Next Track | MediaNextTrack |
| `E9` | Pre Track | `0B6` Scan Previous Track | MediaPreviousTrack |
| `EA` | Stop | `0B7` Stop | MediaStop |
| `EB` | Eject | `0B8` Eject | none |
| `EC` | Play/Pause | `0CD` Play/Pause | MediaPlayPause |
| `F0` | Media | `183` AL Consumer Control Configuration | SelectMedia (opens the media player) |
| `F1` | Mail | `18A` AL Email Reader | LaunchMail (opens the mail app) |
| `F3` | Open Explorer | `194` AL Local Machine Browser | LaunchApplication1 (opens File Explorer) |
| `F4` | Zoom In | `22D` AC Zoom In | none |
| `F5` | Zoom out | `22E` AC Zoom Out | none |
| `F6` | Zoom 100% | `231` AC Normal View | none |
| `F7` | Rotate Picture | `1C8` | none |
| `F8` | Search | `221` AC Search | BrowserSearch |
| `F9` | Shuffle | `1C9` | none |
| `FA` | WWW Home | `223` AC Home | BrowserHome |
| `FC` | WWW Forward | `225` AC Forward | BrowserForward |
| `FD` | WWW Stop | `226` AC Stop | BrowserStop |
| `FE` | WWW Refresh | `227` AC Refresh | BrowserRefresh |
| `FF` | WWW Favorite | `22A` AC Bookmarks | BrowserFavorites |

With `ED`/`EE`/`EF`/`F2`/`FB` from 17 Nov, every media code has now run on the keypad. "none"
means Windows has no virtual key for the usage; an application that reads consumer reports itself
could still use it. Open Explorer, Zoom and Shuffle, which the original editor stored as PC-side
actions, therefore also exist as firmware codes.

### Polling rate (18 Nov 2026)

`SET_POLLINGRATE` ([03](03-usb-protocol.md#8-byte-command-format)) is kept across unplugging and
takes effect when the keypad next connects. It changes only the **keyboard** endpoint:

| Setting | `bInterval` of endpoint `81` (keyboard, interface 0) | `bInterval` of endpoint `82` (mouse + consumer, interface 1) |
|---|---|---|
| 125 Hz (interval 8, the test keypad's setting) | 8 ms | 8 ms |
| 1000 Hz (interval 1), after replugging | 1 ms | 8 ms |

So mouse and media reports stay at 125 Hz whatever the setting: a pointer-move input sent exactly
one report every 8 ms at both settings.
