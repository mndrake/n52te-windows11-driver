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
| `+0x101` | 3 | option bytes p1, p2, p3 (factory `01 01 00`; meaning not named in the code, probably macro playback mode) |

How the `code` byte is interpreted:

| `code` | Meaning | Who executes it |
|---|---|---|
| `0x00` | Disabled ("Disable Button") | – |
| key code `0x10`–`0x9F` | Single key | firmware → keyboard interface MI_00 |
| = **macro marker** of this input (`0xA0`–`0xBA`) | Play `seq` as a macro | firmware |
| = **PC-action marker** of this input (`0x7C`–`0x9F`) | PC-side action: `seq[0]` = type, `seq[1]` = parameter | **PC** (`n52teHid.exe`); the firmware reports keyboard usage `0xC4 + n` (*inferred*) |
| `0xC0`–`0xCE` | Command (Cut, Copy, …, Show Desktop) | firmware (*inferred*) |
| `0xD0`–`0xD8` | Mouse button / wheel / pointer movement | **firmware → mouse interface MI_01** |
| `0xD9`–`0xDF` | Keymap switch (cycle, or Blue/Green/Red toggle/momentary) | **firmware; the LED follows the keymap** |
| `0xE8`–`0xFF` | Media / WWW / application keys | firmware (consumer keys, *inferred*) |

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
| `C0` | Cut | command (firmware, *inferred*) |
| `C1` | Copy | command (firmware, *inferred*) |
| `C2` | Paste | command (firmware, *inferred*) |
| `C3` | Undo | command (firmware, *inferred*) |
| `C4` | All | command (firmware, *inferred*) |
| `C5` | Find | command (firmware, *inferred*) |
| `C6` | New | command (firmware, *inferred*) |
| `C7` | Save | command (firmware, *inferred*) |
| `C8` | Print | command (firmware, *inferred*) |
| `C9` | Swap Windows | command (firmware, *inferred*) |
| `CA` | Close Windows | command (firmware, *inferred*) |
| `CB` | Run | command (firmware, *inferred*) |
| `CC` | Show Desktop | command (firmware, *inferred*) |
| `CD` | Maximize Windows | command (firmware, *inferred*) |
| `CE` | Minimize Windows | command (firmware, *inferred*) |
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
