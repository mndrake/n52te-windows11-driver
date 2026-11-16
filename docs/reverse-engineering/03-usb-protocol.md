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
| 3 | SET/GET_PROFILEID (4/5) | keymap 0, 1, 2 (editor order Blue, Green, Red, *unverified*) |
| 4 | SET_POLLINGRATE (9) | interval in ms: `08` = 125 Hz, `02` = 500 Hz, `01` = 1000 Hz (manual: needs a reboot) |

Examples (all derived from the service code, not captured):

```
select keymap 0      00 08 00 03 00 00 00 0B
select keymap 1      00 08 00 03 00 01 00 0A
select keymap 2      00 08 00 03 00 02 00 09
polling 1000 Hz      00 08 00 04 00 01 00 0D
```

The service checks replies the same way: `GET_PROFILEID` is accepted only if bytes 6/7 match.
Otherwise it logs `Checksum Error`.

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
| `0x008` | 27 | firmware **code** of each input |
| `0x023` | 27 | option byte p1 of each input, **+1** |
| `0x03E` | 27 | option byte p2, **+1** |
| `0x059` | 27 | option byte p3, **+1** |
| `0x100 + n×0x100` | 256 | **macro of input n**: 128 × `[code][flags]` |

The macro conversion (`0x4065c0`) turns the editor's list of `(code, flags)` steps into this form.
A step whose code is `0xFF` is a delay. Its value is shifted left by one and ORed into the
previous step's flags byte, so in the uploaded form `flags` bit 0 = press (1) / release (0)
and bits 1–7 = delay after the step. The README's maximum delay of 6350 ms = 127 × 50 ms
suggests a 50 ms unit (*inferred*). If a macro ends on a press, the last press is followed
by its release.

Codes, inputs and option bytes are described in [04](04-onboard-profile-and-codes.md).

## Open questions (need a USB capture)

* Does `usbccgp` pass the `wIndex` low byte (chunk number) through unchanged for
  `URB_FUNCTION_CLASS_INTERFACE`, or rewrite it to the interface number?
* Which interface does the firmware expect the requests on? The driver uses whichever of its two
  instances (MI_00/MI_01) forwarded an internal IOCTL most recently.
* What does command 8 ("driver mode") switch, and does the firmware report PC-side actions as
  `0xC4 + input` only in that mode?
* Meaning of option bytes p1–p3 (factory value `01 01 00`). They are most likely the macro
  playback mode (once / while held / toggle), which the manual describes but the code doesn't
  name.
