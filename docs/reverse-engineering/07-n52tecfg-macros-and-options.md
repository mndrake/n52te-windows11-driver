# 07 - `n52teCfg.exe` editor: macro options, recorder, limits, polling, reset and import

Follows on from [06](06-n52tecfg-editor.md). Decompiled on 17 Nov 2026 with Ghidra 11.1.2 in
headless mode (`n52teCfg.exe`, plus `n52teHid.exe` for the service side of the editor's
messages), using small Ghidra scripts for string, constant and address cross-references. The
decompiler output is not in the repository ([README](README.md#not-included)).

Legend: **[V]** verified in code (address given), **[I]** inferred. Nothing on this page was
tested on the keypad except where [04](04-onboard-profile-and-codes.md#verified-on-hardware-17-nov-2026)
says so; in particular, what the firmware does with the macro options is still open.

Struct notation below:
* macro-editor dialog (`CMacroDlg`, ctor `0x41c651`): `+0x64` 128×(code,flags) steps (0x100 bytes),
  `+0x164` step count, `+0x174` Behaviour, `+0x178` RepeatTimes, `+0x17c` RepeatInterval,
  `+0x180` IgnoreDelay, `+0x184` "delay time" for Insert Delay (50 ms units).
* profile record (0x104 bytes): `+0` code, `+1` seq[256], `+0x101` p1, `+0x102` p2, `+0x103` p3.

The macro-editor skin controls are created in `0x4200bc` from `Macro_expand.ini [BUTTONPOS]` by key
number **[V]** (key strings at `0x4a34d4…0x4a3664`):

| INI key | name | member | handler in `0x41d113` (OnCommand) |
|---|---|---|---|
| 1 | BUTTON_EXPAND | +0x284 | close/collapse |
| 4/5/6/7/31 | NEW/OPEN/SAVE/SaveAs/DELETE | +0x518/+0x5f4/+0x6d0/+0x7ac/+0x1b70 | macro file ops |
| 8/9 | Record/STOP | +0x888/+0x964 | `installhook`/`removehook` (KBHook.dll) |
| 10 | Advance | +0xa40 | inserts steps from the "Advanced" dialog |
| 11–15 | UP/DOWN/DEL/Sel/SelDelays | +0xb1c…+0xe8c | list edit |
| 16 | Insert_Delay | +0xf68 | inserts/extends a `0xFF` step |
| 17 | DelayTimes | +0x1044 | popup → `+0x184` |
| 18 | BUTTON_Ignore | +0x12d8 | `+0x180 = checked` |
| 19/20/21 | REPEAT1/2/3 | +0x13b4/+0x1490/+0x156c | `+0x174 = 0/1/2` |
| 23 | MACRONAME (combo) | +0x188 | |
| 24 | TIMES | +0x1724 | popup → `+0x178` |
| 25 | INTERVAL | +0x1800 | popup → `+0x17c` |
| 26/27/28 | MOUSELEFT/MID/RIGHT | +0x18dc/+0x19b8/+0x1a94 | insert D0/D2/D1 press+release |

Keys **2, 3, 22 (BUTTON_REPEAT4), 29, 30 (SPIN_DELAYTIME…)** are never read **[V]**: member +0x1648
exists (ctor/dtor only, xrefs `0x41c86a`, `0x425b64`) but is never created or handled. So
"Repetition interval:" is only a label printed in the JPG, not a 4th mode, and the little up/down
spin next to the delay box does nothing (the delay time is chosen from the DelayTimes popup).

---

## 1. Macro behaviour options → p1, p2, p3

### Where p1..p3 come from **[V]**
Macro files (`User Macros\<name>.ini`, `[Setting]`) hold `Behaviour`, `RepeatTimes`,
`RepeatInterval`, `IgnoreDelay` and `Macro` (=0x100-byte step array, WritePrivateProfileStruct).
Load `0x424771`, save `0x424a6b`.

When a saved macro is assigned to an input (menu ID 50000+n, `0x43c5f1` → `0x43bca9`):
```
rec.code = macroMarkerTable_0x4a487c[input]          // A0 A4 A8 AC B0 A1 … B4 B5 B6 B7
rec.seq  = file[Setting]Macro (0x100 bytes)
rec.p1   = GetPrivateProfileInt("Setting","Behaviour",      0)
rec.p2   = GetPrivateProfileInt("Setting","RepeatTimes",    1)
rec.p3   = GetPrivateProfileInt("Setting","RepeatInterval", 1)
```
`IgnoreDelay` is **not** copied into the profile. Profile save `0x431d4d` re-reads Behaviour /
RepeatTimes / RepeatInterval / Macro from the macro file of every macro-assigned input (codes
`A0`–`BA`, except the factory diagonal macros `+Up+Right…`), so the profile always mirrors the
macro file.

### What the controls store **[V]**
| Control | Stored (editor/file/profile) | Displayed | Uploaded (service adds +1, doc 03) |
|---|---|---|---|
| REPEAT1 "Run Times" (`+0x13b4`) | p1 = **0** | | 1 |
| REPEAT2 "Repeat while pressed" (`+0x1490`) | p1 = **1** | | 2 |
| REPEAT3 "Repeat until next key is pressed" (`+0x156c`) | p1 = **2** | | 3 |
| TIMES popup (`0x44ae01`: 10 items "1".."10") | p2 = **index 0..9** (`0x423b4e`) | `p2+1` (`0x423fab`) | count 1..10 |
| INTERVAL popup (`0x41761e`) | p3 = **index 0..4** (`0x423c81`) | `(p3+1)*50 ms` (`0x424000`) | p3+1 = interval in **50 ms units** (1..5 = 50..250 ms) |

Details:
* Radio handlers at `0x41e897/0x41e8cf/0x41e907` write 0/1/2 to `+0x174`, then `0x42462c(mode)`
  enables/disables controls: TIMES (`+0x1724`) is enabled **only in mode 0**; the selected radio is
  disabled (that is how the bitmap skin shows "selected"). `0x42462c` also has a case 3 (no radio
  selected) that no handler can reach. INTERVAL is never disabled, so the interval applies in every
  mode **[I: firmware meaning]**.
* INTERVAL popup bug: the loop adds "50 ms","100 ms","150 ms","200 ms" and then adds the last
  formatted string again, so the 5th entry reads "200 ms" but selects index 4 = 250 ms
  (`0x4176a9`–`0x417705`).
* New macro (`0x4249c2`) sets Behaviour=RepeatTimes=RepeatInterval=IgnoreDelay=0, i.e. "Run 1
  time, 50 ms". `GetPrivateProfileInt` defaults when the key is missing: Behaviour 0, RepeatTimes 1,
  RepeatInterval 1 (`0x43bca9`).
* Factory / "New profile" (`0x442fad`) sets p1=1, p2=1, p3=0 for every record (that is the `01 01 00`
  in Default.ini): Behaviour "Repeat while pressed", 2 times, 50 ms. For the factory diagonal
  macros this means "repeat Up+Right press/release while held, 50 ms apart" **[I]**.

### "Ignore delays between events" (`+0x180`) **[V]**
Used only while recording/inserting mouse buttons: when 0, the recorder (`0x424c7a`) and the mouse
button handlers (`0x41e96f`, `0x41ebfd`, `0x41ee8b`) insert a `0xFF` delay step before each new
event (see §4); when 1, no delay steps are inserted (and the 127-entry cap becomes 128, because no
room for a trailing delay is reserved). It does **not** strip existing delay steps, is saved only
in the macro file (`IgnoreDelay`) and never reaches the profile/keypad.

## 2. "This profile exceeds the maximum allowable size for storage in Synapse"

**[V] Not implemented in v2.1.2.** `Set_Profile_Synapse_{normal,over,down}.jpg` (the dialog text is
baked into the JPG) is referenced by **no** skin INI and by **no** binary in the payload: an ASCII
and UTF-16 search of every `.exe/.dll/.ocx/.sys` for `Synapse` finds only `n52teCfg.exe`, whose only
hits are `Load to Synapse` and `skins\Synapse_close.ini|Synapse_expand.ini|Synapse_1..3.jpg`.
No "Set_Profile" string exists anywhere. It is a leftover asset.

Limits that do exist:
* Per macro: **128 entries incl. delay steps** (`+0x164 < 0x80`; recorder stops at 127 when delays
  are on, `0x424ced`; Insert Delay/mouse buttons refuse at `>0x7f`). The service folds delay steps
  into the previous step (doc 03), so the uploaded macro has ≤128 key steps.
* The keymap image is fixed size (0x100 header + 27×0x100 macro slots = 0x1C00), so there is
  nothing to overflow: every input has its own 256-byte slot **[V via doc 03 layout; I that this is
  why the check was dropped]**.
* Max 100 profiles (`0x442fad`: `count < 100`; import `0x4416ef`: `99 < count` → refuse).

## 3. Recorded keys: VK → firmware code

### Macro recorder **[V]**
`KBHook.dll` (`installhook`/`removehook`, loaded in `0x41d04c`) posts `0x40A(vkCode, LLKHF_UP)`.
Handler `0x424c7a(vk, up)`:
1. ignored unless recording (`+0x264`), and **ignored if (vk, up) equals the previous event**
   (auto-repeat suppression, globals `0x4a8c40/44`);
2. `code = 0x4343cb(vk)`: linear scan of the 0x91-entry table at **`0x48cbb0`**
   (`struct {int code; int vk;}`), **last match wins**, returns 0 if not found;
3. delay step (see §4), then step `(code, up ? 0 : 1)`.

An unmapped VK gives code `00`, and the step list treats `00` as end-of-macro (`0x423f35` loop stops
at code 0) **[V]**: e.g. VK_MEDIA_NEXT_TRACK (B0) is not in the table.
Low-level hooks deliver VK_LSHIFT/RSHIFT/LCONTROL/… directly, which is why the table uses A0–A5.
Both Enter keys arrive as VK_RETURN → `56`; numpad Enter `74` is only reachable via VK_SEPARATOR
(6C) **[I]**.

Table quirks: `9C` (Win) appears twice (VK 5B and 5C); `93` and `94` both list VK B1
(MEDIA_PREV_TRACK) so B1 → `94` (last match); `9F` ← VK FB (VK_ZOOM). 31 codes have VK 0
(not recordable): `17 1D 1F 27 3F 45 47 4E 52 65 73 78 7B 7C 7E 80 81 82 84 85 87 89 8A 8B 8C 8D 95 98 9B 9D 9E`.
Reverse lookup (code → VK) is `0x434378` (first-wins on the other column).

### Single Key dialog **[V]**
Dialog class ctor `0x4513e0` (title "n52TE SingleKey", skin `Single_Key.ini`), opened for command
`0x2711` in `0x43c5f1`. Key handler `0x4526ed(isDown, vk, flags)`:
```
if vk==VK_CONTROL: vk = (flags & 0x100) ? A3 : A2      // extended bit → right
if vk==VK_SHIFT:   vk = scan==0x2A ? A0 : scan==0x36 ? A1 : vk
if vk==VK_MENU:    vk = (flags & 0x100) ? A5 : A4
code = 0x4343cb(vk)                                     // same table
if (isDown && code) || vk==VK_SNAPSHOT: dlg+0x5c = code; show name (0x45279e → 0x4356e0)
```
On OK: `rec.code = dlg+0x5c`, `rec.seq` zeroed, p1..p3 untouched (`0x43c5f1` lines for 0x2711).
(`flags` = WM_KEYDOWN lParam bits 16–24, i.e. scan code + extended bit **[I]**.)
A second recorder with the same normalisation plus `VK_RETURN+extended → 0x6C` (so numpad Enter →
`74`) is `0x434a96` (128 steps at `+0x74`); which dialog owns it was not traced.

### VK → firmware code table (`0x48cbb0`, sorted by VK) **[V]**
| VK | name | code |
|---|---|---|
| `08` | BACK | `53` |
| `09` | TAB | `13` |
| `0D` | RETURN | `56` |
| `13` | PAUSE | `76` |
| `14` | CAPITAL | `1B` |
| `1B` | ESCAPE | `15` |
| `20` | SPACE | `57` |
| `21` | PRIOR | `68` |
| `22` | NEXT | `69` |
| `23` | END | `71` |
| `24` | HOME | `70` |
| `25` | LEFT | `77` |
| `26` | UP | `75` |
| `27` | RIGHT | `5F` |
| `28` | DOWN | `67` |
| `2C` | SNAPSHOT | `7A` |
| `2D` | INSERT | `58` |
| `2E` | DELETE | `60` |
| `30` | 0 | `49` |
| `31` | 1 | `11` |
| `32` | 2 | `19` |
| `33` | 3 | `21` |
| `34` | 4 | `29` |
| `35` | 5 | `28` |
| `36` | 6 | `30` |
| `37` | 7 | `31` |
| `38` | 8 | `39` |
| `39` | 9 | `41` |
| `41` | A | `14` |
| `42` | B | `2F` |
| `43` | C | `26` |
| `44` | D | `24` |
| `45` | E | `22` |
| `46` | F | `2C` |
| `47` | G | `2D` |
| `48` | H | `35` |
| `49` | I | `3A` |
| `4A` | J | `34` |
| `4B` | K | `3C` |
| `4C` | L | `44` |
| `4D` | M | `36` |
| `4E` | N | `37` |
| `4F` | O | `42` |
| `50` | P | `4A` |
| `51` | Q | `12` |
| `52` | R | `2A` |
| `53` | S | `1C` |
| `54` | T | `2B` |
| `55` | U | `32` |
| `56` | V | `2E` |
| `57` | W | `1A` |
| `58` | X | `1E` |
| `59` | Y | `33` |
| `5A` | Z | `16` |
| `5B` | LWIN | `9C` |
| `5C` | RWIN | `9C` |
| `5D` | APPS | `9A` |
| `60` | NUMPAD0 | `5D` |
| `61` | NUMPAD1 | `64` |
| `62` | NUMPAD2 | `5C` |
| `63` | NUMPAD3 | `6C` |
| `64` | NUMPAD4 | `63` |
| `65` | NUMPAD5 | `5B` |
| `66` | NUMPAD6 | `6B` |
| `67` | NUMPAD7 | `62` |
| `68` | NUMPAD8 | `5A` |
| `69` | NUMPAD9 | `6A` |
| `6A` | MULTIPLY | `6E` |
| `6B` | ADD | `72` |
| `6C` | SEPARATOR | `74` |
| `6D` | SUBTRACT | `6F` |
| `6E` | DECIMAL | `6D` |
| `6F` | DIVIDE | `5E` |
| `70` | F1 | `18` |
| `71` | F2 | `20` |
| `72` | F3 | `23` |
| `73` | F4 | `25` |
| `74` | F5 | `55` |
| `75` | F6 | `3D` |
| `76` | F7 | `43` |
| `77` | F8 | `40` |
| `78` | F9 | `50` |
| `79` | F10 | `51` |
| `7A` | F11 | `61` |
| `7B` | F12 | `59` |
| `90` | NUMLOCK | `66` |
| `91` | SCROLL | `79` |
| `A0` | LSHIFT | `83` |
| `A1` | RSHIFT | `86` |
| `A2` | LCONTROL | `88` |
| `A3` | RCONTROL | `8E` |
| `A4` | LMENU | `7D` |
| `A5` | RMENU | `7F` |
| `AC` | BROWSER_HOME | `99` |
| `AD` | VOLUME_MUTE | `8F` |
| `AE` | VOLUME_DOWN | `97` |
| `AF` | VOLUME_UP | `96` |
| `B1` | MEDIA_PREV_TRACK | `94` |
| `B1` | MEDIA_PREV_TRACK | `93` |
| `B2` | MEDIA_STOP | `92` |
| `B3` | MEDIA_PLAY_PAUSE | `91` |
| `B5` | LAUNCH_MEDIA_SELECT | `90` |
| `BA` | OEM_1 ; | `4C` |
| `BB` | OEM_PLUS = | `38` |
| `BC` | OEM_COMMA | `3E` |
| `BD` | OEM_MINUS | `48` |
| `BE` | OEM_PERIOD | `46` |
| `BF` | OEM_2 / | `4F` |
| `C0` | OEM_3 ` | `10` |
| `DB` | OEM_4 [ | `4B` |
| `DC` | OEM_5 \ | `54` |
| `DD` | OEM_6 ] | `3B` |
| `DE` | OEM_7 ' | `4D` |
| `FB` | ZOOM | `9F` |

## 4. Macro steps for mouse buttons and delays **[V]**

Step = `(code, flags)`, editor flags: 1 = press, 0 = release; delay step = `(0xFF, n)` with
**n = delay in 50 ms units, 1..127** (displayed `n*50 ms`, `0x423f35`).

| Action | Steps written |
|---|---|
| Mouse Button panel, left (`+0x18dc`, `0x41e943…`) | `[FF n]` (if delays on and ≥50 ms since last event) `D0 01`, `D0 00` ("Left Button Press/Release") |
| middle (`+0x19b8`) | … `D2 01`, `D2 00` |
| right (`+0x1a94`) | … `D1 01`, `D1 00` |
| Recorded key (`0x424c7a`) | … `code 01` (down) / `code 00` (up) |
| Recorder / mouse-button delay | `n = (GetTickCount() − lastEventTick) / 50`, only if ≥1, capped at 0x7F; skipped when IgnoreDelay or for the first step |
| DelayTimes popup (items `0x409021`; msg 0x3417 in `0x42382e`) | `+0x184` = 1,2,3,4,5,10,20 for 50,100,150,200,250,500,1000 ms (default 1) |
| Insert Delay (`+0xf68`, `0x41fa18…`) | one item selected: if the next step is a delay → add `+0x184` to it; else if the selected step is a delay → add to it; else insert `FF +0x184` after it. Nothing selected: same against the last step (extend or append). Sum capped at 127 |

The 50/100/150/200 ms `DELAY*` buttons belong to `macro.ini`, which no code references by name
(only `Macro_expand.ini`/`macro_close.ini` are loaded) **[I: legacy skin]**.
Uploaded form (service, doc 03): the `FF n` step is folded into the previous step's flags as
`n<<1 | press`, so max delay after a step = 127×50 = 6350 ms.

Other step codes the macro list can show (`0x423f35`): `C0`–`CE` commands and media items
`E8 E9 EA EC ED EE EF F3 F9` (inserted via the Advanced dialog) **[V display only]**.

Save warning: Save/SaveAs (`+0x6d0`/`+0x7ac`) call `0x4256c6`, which counts presses minus releases
over non-delay steps; if presses exceed releases the MacroWarning dialog is shown and "Cancel"
aborts the save **[V]**.

## 5. Polling rate **[V]**

Main window buttons (`0x43fcd9`, OnCommand): 125 Hz `+0x411cac` → 0, 500 Hz `+0x411d88` → 1,
1000 Hz `+0x411e64` → 2. Each:
1. `this+0x207700 = v`; update the radio images (`0x445e02`);
2. `PostMessage(FindWindow(NULL,"razerJomanthahidUSB"), 0x50B, v, 0)`;
3. write `config.ini [Setting] Polling Rate = v`;
4. start timer 2 (10 s) and set `+0x6c = 1` (timer purpose not traced).

Service (`n52teHid.exe`, MFC message map at `0x41a298`, found by scanning the AFX_MSGMAP entries):

| msg | handler | action |
|---|---|---|
| `0x4FA` | `0x4054a0` | re-read `config.ini` (`0x403230`/`0x4032a0`, incl. `Polling Rate` → `+0x164`) |
| `0x506` | `0x406d40` | `0x406780(km)` (SET_PROFILEID) and store km as the active profile's start keymap |
| `0x508` | `0x405460` | not traced |
| `0x509` | `0x406b20` | upload profile `wParam`: `lParam == -1` → all 3 keymaps with progress bar, else only keymap `lParam`; up to 3 tries each; then SET_PROFILEID(start keymap) **and SET_POLLINGRATE(config Polling Rate)** |
| `0x50B` | `0x406d20` | `0x406d90(wParam)` |
| `0x50D` | `0x406d80` | no-op |

`0x406d90` sends IOCTL `0x222460` with the 8-byte command `00 08 00 04 00 iv x0 x1`,
`iv = {0:8, 1:2, 2:1}` ms (125/500/1000 Hz), `x0/x1` XOR checksums (doc 03). So the rate reaches
the keypad immediately on click and again after every upload. (The manual's "needs a reboot" is not
reflected in code **[I]**.)

## 6. Reset Keymap and Import

### Reset Keymap (`+0x41238c`, in `0x43fcd9`) **[V]**
Confirmation dialog `0x44df30`; on IDYES, for the profile being edited and **the keymap currently
shown** (`km = +0x20c9b8`):
* inputs 1–23: `rec.code = factory[n]` (table `0x4a4860` =
  `13 12 1A 22 2A 1B 14 1C 24 2C 83 16 1E 26 7D D4 D2 D3 57 75 5F 67 77 B4 B5 B6 B7`).
  **seq and p1–p3 are not cleared** (a dead switch would set seq[0]=6..9 for codes 9B/9D/9E/9F).
* inputs 24–27 (diagonals): in **all three keymaps**, `code = B4..B7`, seq cleared and set to the
  factory macro (`75 01 5F 01 75 00 5F 00` etc.). p1–p3 untouched.
* marks the profile modified (`+0x207770 = 1`), redraws. Nothing is uploaded until Apply Changes.

### Import (`+0x4121d4` → `0x4416ef`) **[V]**
Open-file dialog in `User Profiles\` (`*.ini`). Reads `[Setting] Name`, `Exe`, `Switch Flag` and
`[macro] Value` (GetPrivateProfileStruct 0x5244 bytes, so the checksum byte must match) into a new
profile slot (refused at 100 profiles), copies the file to `User Profiles\<Name>.ini` and updates
`config.ini [Profile]`. If the name already exists it asks (dialog `0x42ff70`) and then offers a
Save dialog for a new name. `Key map` (start keymap) is not read by Import.

Other main buttons for reference: `+0x411f40` New (`0x442fad`), `+0x41201c` Save (`0x445fab`),
`+0x4120f8` Save As (`0x442955`, save dialog), `+0x412468` Delete (`0x442673`),
`+0x4122b0` Apply Changes (posts `0x509(profile, -1)`), `+0x411784/860/93c` keymap tabs.

## 7. Other things that change the uploaded image / device state

* **Start keymap (`Key map`)** **[V]**: clicking the Map 1/2/3 tab (`0x43fcd9`, `+0x411784…`) sets the
  profile's keymap to 0/1/2 (`+0x20c9b8` and `+0x7b50`), **saves the profile file** (`0x431d4d`,
  writes `Key map`) and, if this profile is the one on the keypad (`+0x2076f4 == +0x207704`), posts
  `0x506(km)` → service SET_PROFILEID. So the start keymap is simply the tab that was last viewed.
  After an upload the service sends SET_PROFILEID(start keymap).
* **Trailing byte `9A`** **[V]**: the profile `Value` is written/read with
  `WritePrivateProfileStructA`/`GetPrivateProfileStructA` (`0x416bf4`/`0x416c83`), size 0x5244;
  Windows appends a checksum byte = 8-bit sum of the data bytes. Checked on Default.ini: sum of the
  0x5244 bytes & 0xFF = `9A`. It is not profile data and is not uploaded; a profile written by
  another tool must append the correct sum or the editor (and service) reject the value. Same for
  the macro files' `Macro` key (0x100 bytes + checksum).
* p1–p3 of non-macro inputs are never changed after "New profile" (01 01 00); Single Key /
  commands / mouse / keymap switches only rewrite `code` and zero `seq`.
* Keymap-switch codes D9–DF are written to all three keymaps (again visible in `0x43c5f1`).
* `Switch Flag`/`Exe` (auto-switch) and `Auto Switch`/`Show Screen` (`+0x411af4`/`+0x411bd0`, post
  `0x4FA 0x20`) are PC-side only.

## Not resolved
* Firmware semantics of p1 = 1/2/3 after the +1 (names come from the UI; behaviour on the keypad
  not tested). Whether "Run Times" also waits `p3` between runs.
* Purpose of the 10-s timer 2 after a polling change; service message `0x508`.
* Owner dialog of the second recorder `0x434a96`.
