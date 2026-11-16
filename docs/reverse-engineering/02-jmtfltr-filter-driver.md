# 02 - `JmtFltr.sys` filter driver

Razer "Jamantha" USB filter (`e:\keyboard_ap\razer\jamantha\driver\usb-tara\` with
`usbfilter.c`, `pnp.c`, `usb.c`). WDM, built with the Vista WDK (`c:\winddk\6000`). Version
resource `1.0.0.0 built by: WinDDK`. The x86 build has `DbgPrint` strings prefixed `USB_VA: `.

## Device objects

* **Control device** `\Device\JmtFilter`, symbolic link `\DosDevices\JmtFilter` (opened from user
  mode as `\\.\JmtFilter`). Created in `DriverEntry` (x86 `0x1b110`) with a 0x10-byte extension.
  All private IOCTLs are handled here only.
* **One filter device per interface**, created in `AddDevice` (x86 `0x19f60`) with a 0x58-byte
  extension and attached with `IoAttachDeviceToDeviceStack` on top of the interface PDO, which
  puts it *under* `hidusb`. Extension fields (x86): `+0x04` started flag, `+0x05` removed flag,
  `+0x08` own device object, `+0x0C` PDO, `+0x10` next-lower device, `+0x14` `0x10000004`,
  `+0x20` / `+0x30` events, `+0x40` outstanding-IO counter, `+0x44` cached USB device descriptor.

## Dispatch table (x86 addresses)

| Major function | Handler | Behaviour |
|---|---|---|
| all (default) | `0x104b0` | Pass through to the lower device; complete with `STATUS_INVALID_DEVICE_REQUEST` (`0xC00000BB`) if sent to the control device, or `STATUS_DELETE_PENDING` once removed |
| `IRP_MJ_CREATE` / `IRP_MJ_CLOSE` | `0x19e10` | Succeed |
| `IRP_MJ_DEVICE_CONTROL` | `0x10c60` | Private IOCTLs on the control device; pass-through on filter devices |
| `IRP_MJ_INTERNAL_DEVICE_CONTROL` | `0x10850` | Watches `IOCTL_INTERNAL_USB_SUBMIT_URB` from `hidusb` and hooks interrupt transfers |
| `IRP_MJ_POWER` | `0x1a1d0` | Re-sends "driver mode" on resume (below) |
| `IRP_MJ_PNP` | `0x1a4e0` | Start / remove / stop handling (below) |
| `DriverUnload` | `0x1a0e0` | Deletes the symbolic link and control device |

The x64 build has the same layout. Its internal-IOCTL handler is at `0x107c0` and its interrupt
completion routine at `0x10a80`.

## Private IOCTLs

All use `FILE_DEVICE_UNKNOWN` (0x22), `METHOD_BUFFERED`, `FILE_ANY_ACCESS`. Names are the
driver's and service's own debug strings. "USB" means the IOCTL produces one control transfer;
see [03](03-usb-protocol.md) for what goes on the wire.

| IOCTL | Function | Name | Input → output | Action |
|---|---|---|---|---|
| `0x222400` | `0x900` | `MJ HOOK` | 4-byte event handle → – | `ObReferenceObjectByHandle` on the handle, enable the input hook |
| `0x222404` | `0x901` | (unhook) | – | Dereference the event, disable the hook |
| `0x222408` | `0x902` | `IOCTL_WT_READ` | – → 4 bytes | Return the last event code and clear it |
| `0x22240C` | `0x903` | `IOCTL_GET_RESULT` | – → n | USB: GET_REPORT `wValue=2` |
| `0x222414` | `0x905` | `IOCTL_GET_GROUP_SETTING` | – → n | USB: GET_REPORT `wValue=3`, `wIndex=keymap<<8 \| chunk++` |
| `0x222418` | `0x906` | `IOCTL_SET_PROFILEID` | 8 bytes → – | USB: SET_REPORT `wValue=4`; remembers byte 5 |
| `0x22241C` | `0x907` | `IOCTL_GET_PROFILEID` | – → 8 | USB: GET_REPORT `wValue=5`; remembers byte 5 |
| `0x222420` | `0x908` | `IOCTL_CLEAR_BUFFER_INDEX` | – | `chunk = 0` |
| `0x222440` | `0x910` | `IOCTL_GET_FWVERSION` (driver prints "GET_RESULT") | – → 8 | USB: GET_REPORT `wValue=6` |
| `0x222444` | `0x911` | `IOCTL_SET_CURRENTPROFILE` | 2 bytes → – | Store; byte 1 becomes the `wIndex` high byte (keymap) |
| `0x222448` | `0x912` | `IOCTL_SET_PROFILE_KEYINFO` | 0x121 bytes → – | `[0]` = profile 6–0x65, then two 0x90-byte blocks copied into a driver table. The table is never read in the shipped code |
| `0x22244C` | `0x913` | (no name) | 4 bytes → – | Stores a global that nothing reads |
| `0x222450` | `0x914` | `IOCTL_ENTER_ICP` | 1 byte → – | USB: SET_REPORT `wValue=7`, 1 byte |
| `0x222454` | `0x915` | `IOCTL_GET_ICP_RESULT` | – → n | USB: GET_REPORT `wValue=8` |
| `0x222458` | `0x916` | `IOCTL_SET_DRIVER_MODE` | – | USB: SET_REPORT `wValue=8`, 1 byte `00`; sets a "driver mode" flag |
| `0x22245C` | `0x917` | `IOCTL_GET_DRIVER_MODE_RESULT` | – → n | USB: GET_REPORT `wValue=8` |
| `0x222460` | `0x918` | `IOCTL_SET_POLLINGRATE` | 8 bytes → – | USB: SET_REPORT `wValue=9` |
| `0x222464` | `0x919` | `IOCTL_GET_POLLINGRATE` | – → n | USB: GET_REPORT `wValue=10` |
| `0x22201C` | `0x807` | `IOCTL_SEND_SETTING` | ≤ 0x400 bytes → – | USB: SET_REPORT `wValue=1`, `wIndex=keymap<<8 \| chunk++` |

USB IOCTLs are only sent once a filter instance has started (global "started" flag set on
`IRP_MN_START_DEVICE`). Otherwise they succeed without touching the device.

## How a control transfer is sent (x86 `0x122d0`)

1. Allocate a 0x50-byte `_URB_CONTROL_VENDOR_OR_CLASS_REQUEST` from non-paged pool, tag `Valu`:

   | Field | Value |
   |---|---|
   | `Hdr.Length` / `Hdr.Function` | `0x50` / `0x1B` = `URB_FUNCTION_CLASS_INTERFACE` |
   | `TransferFlags` | `0` (OUT) or `1` = `USBD_TRANSFER_DIRECTION_IN` |
   | `TransferBufferLength` / `TransferBuffer` | caller's length / a 0x400-byte stack buffer |
   | `RequestTypeReservedBits` | `0` |
   | `Request` | `0x09` SET_REPORT (OUT) or `0x01` GET_REPORT (IN) |
   | `Value` | command 1–10 |
   | `Index` | `keymap<<8 \| chunk` for commands 1 and 3, else `0` |

2. `IoBuildDeviceIoControlRequest(IOCTL_INTERNAL_USB_SUBMIT_URB, …)` to the lower device of the
   **filter instance that most recently forwarded an internal IOCTL** (a global set in the
   internal-IOCTL handler). This can be MI_00 or MI_01 depending on timing.
3. Wait up to **3 s** (`-30 000 000` × 100 ns). On timeout, `IoCancelIrp` and wait again.

The pool URB is never freed: neither build imports `ExFreePool`, so every request leaks 0x50
bytes. The completion status is not returned to the caller either; user mode checks results with
the GET commands instead. The SET handlers `memcpy` the caller's input into the 0x400-byte stack
buffer **without a length check**, so a longer input overflows the kernel stack (the `/GS`
cookie catches it as a bug check `0xF7`). The service always sends 0x200 bytes.

## Input-report hook

The internal-IOCTL handler (x86 `0x10850`, x64 `0x107c0`) looks for
`IOCTL_INTERNAL_USB_SUBMIT_URB` with `URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER` (`9`). It copies
the stack location and sets a completion routine (x86 `0x10a50`, x64 `0x10a80`). When the
transfer completes, and only while user mode has registered an event (`MJ HOOK`):

```c
if (len == 8 && buf != NULL) {                // 8-byte boot keyboard report: mod, res, k1..k6
    if (buf[2] >= 0xC5 && buf[2] <= 0xDF) {   // first key slot
        buf[2] = 0;                           // hide it from hidusb/kbdhid
        event = buf[2_original] - 0xC4;       // 1..27 = input number
        KeSetEvent(user_event);
    } else if (buf[0] == 0 && buf[2] == 0 && buf[3] == 0) {
        event = 0xFF99;                       // all keys released
        KeSetEvent(user_event);
    }
}
```

Only the **first** key slot is checked. The x64 build compiles the same comparisons
(`cmp 0xc5`, `cmp 0xdf`, `sub 0xc4` at `0x10b75`–`0x10bb0`). The mouse interface's reports are
not 8 bytes long, so they pass through untouched.

What the event codes mean to the service is covered in [05](05-n52tehid-service.md). The
`0xC5`–`0xDF` window overlaps the firmware's mouse/keymap codes (`0xD0`–`0xDF`, see
[04](04-onboard-profile-and-codes.md)). That only works because the firmware executes those
codes itself and never reports them as keystrokes (*inferred*).

## PnP

| Minor | Behaviour |
|---|---|
| `IRP_MN_START_DEVICE` (0) | Raise event `0xBD` ("device inserted") if hooked, set the global started flag, forward synchronously. On success, read the 18-byte USB device descriptor with `URB_FUNCTION_GET_DESCRIPTOR_FROM_DEVICE` into the extension |
| `IRP_MN_REMOVE_DEVICE` (2) | Clear started flag, raise event `0xBE` ("device removed"), signal a second optional event with `0xD000`, mark removed, wait for outstanding IO, detach and delete |
| `IRP_MN_STOP_DEVICE` (4) | Clear the started-descriptor flag and forward |
| others | Forward |

## Power

On `IRP_MN_SET_POWER` for `DevicePowerState` `PowerDeviceD0` (resume), the driver sends the
`SET_DRIVER_MODE` request (`wValue=8`, data `00`) by itself before forwarding. So after
sleep/resume the keypad is put back into "driver mode" without the service's help.
`IRP_MN_WAIT_WAKE` is forwarded with a completion routine that calls `PoStartNextPowerIrp`.

## Why it can't be used on Windows 11

* It is a 2007 WDM driver signed only with an old SHA-1 cross-signed catalogue. Windows 11 with
  Memory Integrity (HVCI) refuses drivers like this, and there is no x64 Windows 10/11-signed
  build.
* It does nothing unusual that a modern KMDF lower filter couldn't do. A replacement needs a
  `WdfRequestSend` of class-interface control transfers, plus an interrupt-IN completion hook
  for the `0xC5`–`0xDF` codes. The obstacle is signing (Microsoft attestation signing for
  Windows 11), not functionality.
