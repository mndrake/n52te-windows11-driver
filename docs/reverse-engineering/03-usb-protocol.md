# 03 - USB configuration protocol

Everything the original software tells the keypad goes over **HID class requests on an
interface**, issued by `JmtFltr.sys` on behalf of `n52teHid.exe`. There are no vendor requests,
no output reports on the interrupt pipe and no separate configuration interface.

```
bmRequestType  0x21 (host→device, class, interface)  for SET_REPORT
               0xA1 (device→host, class, interface)  for GET_REPORT
bRequest       0x09 SET_REPORT   /   0x01 GET_REPORT
wValue         command number 1–10 (high byte 0, NOT the usual report type)
wIndex         keymap<<8 | chunk#   for commands 1 and 3, otherwise 0
wLength        length of the data stage
```

The non-standard `wValue`/`wIndex` is why the Win32 HID API can't send these. `HidD_SetFeature`,
`HidD_SetOutputReport` and `HidD_GetFeature` always encode `wValue = (report type 2/3) << 8 |
report ID` and `wIndex = interface number`. A new tool needs raw control transfers (libusb /
WinUSB / a kernel filter).

## Commands

| `wValue` | Dir | Driver IOCTL | Called by `n52teHid.exe` | Data |
|---|---|---|---|---|
| 1 | SET | `IOCTL_SEND_SETTING` | keymap upload `0x406250` | one 0x200-byte chunk of a keymap image |
| 2 | GET | `IOCTL_GET_RESULT` | `0x406620` (after each chunk) | 2 bytes: checksum of the last chunk received |
| 3 | GET | `IOCTL_GET_GROUP_SETTING` | not used | reads a chunk back (same `wIndex` scheme as 1) |
| 4 | SET | `IOCTL_SET_PROFILEID` | `0x406780` | 8-byte command, sub-command 3: **select active keymap** |
| 5 | GET | `IOCTL_GET_PROFILEID` | `0x405cc0` (on device arrival) | 8-byte reply, byte 5 = active keymap (0–2) |
| 6 | GET | `IOCTL_GET_FWVERSION` | `0x405fb0` (on device arrival) | 8-byte reply, firmware version |
| 7 | SET | `IOCTL_ENTER_ICP` | not used | 1 byte; presumably enters the ISP bootloader (`VID_0425&PID_FF07`) |
| 8 | SET | `IOCTL_SET_DRIVER_MODE` | `0x404d30` on device arrival; the driver also sends it on every resume to D0 | 1 byte `00` |
| 8 | GET | `IOCTL_GET_ICP_RESULT` / `…_DRIVER_MODE_RESULT` | not used | status |
| 9 | SET | `IOCTL_SET_POLLINGRATE` | `0x406d90` | 8-byte command, sub-command 4: polling interval |
| 10 | GET | `IOCTL_GET_POLLINGRATE` | not used | 8-byte reply |

The editor `n52teCfg.exe` never opens `\\.\JmtFilter`. Every device access goes through the
service.

What "driver mode" (command 8) changes in the firmware is **unknown**. The service sends it once
the keypad appears, before loading a profile, and the driver repeats it after every resume.

## 8-byte command format

Commands 4, 5, 6, 9 and 10 use the same 8-byte block:

```
offset  0    1    2    3     4    5      6         7
        00   08   00   SUB   00   VALUE  b0^b2^b4  b1^b3^b5
```

| SUB | Command | VALUE |
|---|---|---|
| 3 | SET/GET_PROFILEID (4/5) | keymap 0, 1, 2. **On the hardware: 0 = Blue, 1 = Red (both verified), 2 = Green (inferred)**, not the editor's menu order. Verified working on interface 0 and kept across unplugging; see [the hardware test](../original-driver-analysis.md#verified-on-hardware-switching-the-onboard-keymap) |
| 4 | SET_POLLINGRATE (9) | interval in ms: `08` = 125 Hz, `02` = 500 Hz, `01` = 1000 Hz. **Verified on hardware** (18 Nov 2026): kept across unplugging, takes effect when the keypad reconnects (the manual says "needs a reboot"), and sets the poll interval of the keyboard endpoint only ([04](04-onboard-profile-and-codes.md#polling-rate-18-nov-2026)) |

Examples (all derived from the service code, not captured):

```
select keymap 0      00 08 00 03 00 00 00 0B
select keymap 1      00 08 00 03 00 01 00 0A
select keymap 2      00 08 00 03 00 02 00 09
polling 1000 Hz      00 08 00 04 00 01 00 0D
```

The service checks replies the same way: `GET_PROFILEID` is accepted only if bytes 6/7 match.
Otherwise it logs `Checksum Error`.

`GET_POLLINGRATE` (10), never used by Belkin's software, works on hardware: it returns
`00 08 01 06 03 VALUE x0 x1` with valid checksums, where VALUE is the current interval in ms
(`08` on a keypad set to 125 Hz, `01` after setting 1000 Hz).

## Uploading a keymap ("Load to Synapse")

Done for each of the three keymaps by `n52teHid.exe` (`0x406250`), called from the profile
loader (`0x404420`):

1. `IOCTL_CLEAR_BUFFER_INDEX` sets the driver's chunk counter to 0.
2. `IOCTL_SET_CURRENTPROFILE` with `00 KM` puts the keymap number in the `wIndex` high byte.
3. Build the 0x1C00-byte image below and send it as **14 × SET_REPORT `wValue=1`**, 0x200 bytes
   each (`wIndex = KM<<8 | 0…13`). After each chunk, `GET_REPORT wValue=2` returns two bytes
   that must equal `(XOR of even-offset bytes, XOR of odd-offset bytes)` of that chunk. On a
   mismatch the whole keymap is abandoned and retried, up to 3 attempts.
4. After all three keymaps: `SET_PROFILEID` with the profile's start keymap.

### Keymap image

| Offset | Size | Content |
|---|---|---|
| `0x000` | 1 | `0x1C` |
| `0x001` | 1 | `0x1B` (27 inputs) |
| `0x002` | 1 | `0x00` |
| `0x003` | 1 | `0x01` |
| `0x004` | 1 | keymap number |
| `0x008` | 27 | firmware **code** of each **slot** |
| `0x023` | 27 | option byte p1 of each slot, **+1** |
| `0x03E` | 27 | option byte p2 of each slot, **+1** |
| `0x059` | 27 | option byte p3 of each slot, **+1** |
| `0x100 + slot×0x100` | 256 | **macro of that slot**: 128 × `[code][flags]` |

**Slots are in key-matrix order, not input order** (verified twice: in `n52teHid.exe` and on
hardware). The upload routine builds a 27-entry table with immediate `MOV`s at
`0x4062c1`–`0x406380` and writes input *n*'s record to `slot[n]`:

| Input | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 24 | 25 | 26 | 27 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Slot | 0 | 4 | 8 | 12 | 16 | 1 | 5 | 9 | 13 | 17 | 2 | 6 | 10 | 14 | 3 | 26 | 24 | 25 | 18 | 19 | 11 | 15 | 7 | 20 | 21 | 22 | 23 |

This is each input's macro marker minus `0xA0` ([04](04-onboard-profile-and-codes.md)). Decoding a
keymap read back from a real keypad with this table reproduces the keys every control sends.

**Read-back differs from the upload header:** `GET_GROUP_SETTING` returned `1C 00 00 01 KM+1`
(byte 1 `00` instead of `1B`, byte 4 = keymap + 1) for keymaps 0/1/2. A re-upload should use the
service's header, `1C 1B 00 01 KM`.

**Chunk checksum** (`0x406720`, verified in code): over the 0x200-byte chunk, `x0` = XOR of the
even-offset bytes, `x1` = XOR of the odd-offset bytes; `GET_RESULT` must return `x0 x1`.
`SET_CURRENTPROFILE` (`0x405ee0`) and `CLEAR_BUFFER_INDEX` (`0x4061a0`) only set state in the
driver (the keymap number for the `wIndex` high byte and the chunk counter for its low byte),
so a tool that builds `wIndex = KM << 8 | chunk` itself does not need them.

The macro conversion (`0x4065c0`) turns the editor's list of `(code, flags)` steps into this form.
A step whose code is `0xFF` is a delay. Its value is shifted left by one and ORed into the
previous step's flags byte, so in the uploaded form `flags` bit 0 = press (1) / release (0)
and bits 1–7 = delay after the step. The README's maximum delay of 6350 ms = 127 × 50 ms
suggests a 50 ms unit (*inferred*). If a macro ends on a press, the last press is followed
by its release.

Codes, inputs and option bytes are described in [04](04-onboard-profile-and-codes.md).

## Verified on hardware (November 2026)

From Linux (pyusb, `usbhid` detached) on a real n52te, firmware reply `00 08 01 04 65 00 64 0c`:

* `GET_FWVERSION` (6), `GET_PROFILEID` (5), `SET_PROFILEID` (4) and `GET_GROUP_SETTING` (3) all
  work. Replies carry `01` in byte 2 and valid checksums.
* Keymap numbers: **0 = Blue, 1 = Red** (verified with the LED), 2 = Green (inferred). The
  selected keymap survives unplugging.
* Interface 0 answers the requests. For `GET_GROUP_SETTING` the firmware takes the chunk number
  from the `wIndex` low byte whatever the recipient: Linux refuses interface-recipient requests
  whose `wIndex` low byte isn't a claimed interface (chunks ≥ 2), and a device-recipient request
  (`0xA0`) with the same `wIndex` returns the right data.
* Tools: [`research/n52te_keymap.py`](../../research/n52te_keymap.py) (read/select keymap) and
  [`research/n52te_readback.py`](../../research/n52te_readback.py) (read back and decode a keymap).

**Upload verified with a round trip** ([`research/n52te_upload.py`](../../research/n52te_upload.py)
`--roundtrip 0`): keymap 0 was read back, sent again as 14 × `SET_REPORT wValue=1` (chunk 0 with
interface recipient `0x21`, chunks ≥ 2 with device recipient `0x20`, `wIndex = KM << 8 | chunk`),
and every `GET_REPORT wValue=2` (`wIndex 0`) returned the expected checksum on the first try
(`c7 cf` for chunk 0; `00 00` for the mostly empty macro chunks). Reading the keymap again gave an
identical body, and the stored header changed from `1C 00 00 01 01` to the uploaded
`1C 1B 00 01 00`, so the write was stored. The keypad kept working with the same layout and LED.

**Changed keymap verified** (`--set 0 18 2F`): wheel down (input 18, slot 25) in keymap 0 changed
from `B9` (its empty macro marker) to `2F` (B). Chunk 0's checksum became `c7 59` and matched,
read-back showed only that byte changed, and on Windows, with no software running, wheel down
typed `b` while the rest of the layout was unchanged. Setting it back to `B9` restored a body
identical to the original backup.

## Open questions

* Does `usbccgp` pass the `wIndex` low byte (chunk number) through unchanged for
  `URB_FUNCTION_CLASS_INTERFACE` on Windows? (Only matters for a Windows kernel-filter
  transport; a raw-device transport sets it directly.)
* What does command 8 ("driver mode") switch, and does the firmware report PC-side actions as
  `0xC4 + input` only in that mode?
* Meaning of option bytes p1–p3 (factory value `01 01 00`). They are most likely the macro
  playback mode (once / while held / toggle), which the manual describes but the code doesn't
  name.
