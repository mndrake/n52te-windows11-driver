# 01 - Package contents

## Download

| File | SHA-256 |
|---|---|
| `n52te_win_driver_v212.zip` (Internet Archive item `n52te_win_driver_v212`, MD5 `8ebe1b9ec204d535d750442bb05aa83c`) | `952f3146406f9c3c20cfc3b58afbeb511c7e96d2c1ac4eecf52b13565699025d` |
| `n52te_win_driver_v2.1.2.exe` (inside the zip) | `1c8e7d4736bbdf922ed40a6f184c843b9ef8157a15ac4dc6cf6298f6995d7325` |

The `.exe` is a **Chilkat self-extractor** (its XML config names `n52te_win_driver_v2.1.2\n52te_win_driver_v2.1.2.exe`
as the setup program). Each entry is:

```
9B E5 27 6D | type (1) | name length (2, LE) | name | FILETIME (8) | attributes (4) | chunks…
chunk = u32 (size | 0x80000000) + data; a zero size ends the entry
```

In most entries every chunk is its own raw-deflate stream. `data1.cab`/`data2.cab` are stored
as-is. The result is a normal **InstallShield** package (`setup.ini`, `data1.hdr`, `data1.cab`,
`data2.cab`, `ISSetup.dll`, …) with 197 files.

## Files that matter

The SHA-256 prefixes are the first 16 hex digits.

| Path in payload | Size | SHA-256 prefix | Role |
|---|---|---|---|
| `WDM_XP64/JmtFltr.sys` | 46 464 | `53c25acd631a9757` | Filter driver, x64 (XP64/Vista64) |
| `WDM_NT_LOW/JmtFltr.sys` (= `WDM_9X/JmtFltr.sys`) | 48 896 | `011ecad13dfcb1a4` | Filter driver, x86, **with debug strings** |
| `INF_XP64/JmtFlter.inf` | 2 473 | `e811702cfa3edee1` | Installs the filter on x64 |
| `INF_XP_LOW/JmtFlter.inf` | 2 365 | `ce347873685b2835` | Installs the filter on x86 |
| `App_Executables/n52teHid.exe` | 159 744 | `02a8b9f985dd0f34` | Background service (internal name `razerhid`) |
| `App_Executables/n52teTra.exe` | 110 592 | `a3bc92bb998fb1f7` | Tray icon (`RazerJomanthaTrayIcon`) |
| `App_Executables/KBHook.dll` | 32 768 | `de473f0d1384b650` | Low-level keyboard hook for the macro recorder |
| `App_Ex_Kar/n52teCfg.exe` | 716 800 | `a8eef50949aad598` | The n52te Editor (MFC) |
| `App_Ex_Kar/OSD.exe` | 278 528 | `92d2e5e085869437` | On-screen display (window class `OSDMain`) |
| `JoyStickX64/vhidmini.sys`, `JoyStickX86/vhidmini.sys` | 13 952 / 12 672 | `7716d49d2a3580f8` / `c4dd41f16f92d939` | Root-enumerated virtual HID device (the "DirectInput" joystick) |
| `WDM_ICP/Usbicp.sys`, `WDM_ICP_XP64/UsbIcp.sys` | 14 592 / 19 200 | `316971041b049d4d` / `f305ed07efaf821a` | Firmware-update (ISP/ICP) driver |
| `INF_ICP/USBICP.INF` | 2 141 | `f4043ae30a3fc652` | Binds the ICP driver |
| `User_Profiles/Default.ini` | 42 196 | `516a734d2d8e4b5a` | Factory profile (see [04](04-onboard-profile-and-codes.md)) |
| `User_Profiles/config.ini` | 141 | | Editor settings (`Polling Rate`, `FWVersion`, `Onboard Profile`, …) |
| `Manual_en/manual.pdf` | 765 280 | | User manual |

The rest is installer support files, MFC skin bitmaps (`skin_English/`), the Flash 9 installer
used by the help screen, `DPInst.exe`/`devcon.exe` copies and the catalogue (`.cat`) files.

## USB IDs and the composite device

| ID | What |
|---|---|
| `USB\VID_050D&PID_0200&MI_00` | Interface 0: boot **keyboard**. Shows as "HID Keyboard Device" |
| `USB\VID_050D&PID_0200&MI_01` | Interface 1: **mouse** (wheel + buttons). Shows as "HID-compliant mouse" |
| `USB\VID_0425&PID_FF07` | Firmware-update mode, "Freescale USB JW32 ICP Device" (`USBICP.INF`) |
| `{D49F883C-6486-400a-8C22-1A9EF48577E4}\HID_DEVICE` | The virtual joystick installed by `vhidmini.inf` |

The manual's *Installation Verification* section matches. Device Manager shows two entries
under *Human Interface Devices* ("Nostromo n52te HID SpeedPad 2.0" and "… SpeedPad 2.0 Mouse
Wheel"), one *HID Keyboard Device* and one *HID-compliant mouse*.

`USBICP.INF` is a generic Motorola/Freescale ISP driver ("Motorola USB ISP Driver disk";
`Usbicp.sys` PDB path `E:\ISP_DRV_SRC\…\UsbIcp.pdb`). Its commented-out entries cover other
Freescale parts (LD64, JB8). The JW32 entry suggests the keypad uses a Freescale
**MC68HC908JW32** USB microcontroller (*inferred* from the driver name only).

## The filter INFs

Both INFs bind the same driver to **both** interfaces. In the x64 version
(`INF_XP64/JmtFlter.inf`, `DriverVer 01/11/2007,1.01`, catalogue `Jmtkbcat.cat`):

```ini
[Standard.NTamd64]
%HID.DeviceDesc01% = HID_Inst_03,USB\VID_050D&PID_0200&MI_00
%HID.DeviceDesc01% = HID_Inst_03,USB\VID_050D&PID_0200&MI_01

[HID_Inst_03.NTAMD64]
Include   = input.inf
CopyFiles = CopyFilesUSB, HID_Inst.Copyfiles.NT

[JmtFilter.AddReg.HW]
HKR,,"LowerFilters",0x00010000,"JmtFltr"
HKR,,"WaitWakeEnabled",0x00010001,0x1

[HID_Inst_03.NTAMD64.Services]
AddService = HidUsb,%SPSVCINST_ASSOCSERVICE%,HIDUSB_Service_Inst
AddService = JmtFltr,,JmtFltr_Service_Inst      ; demand start, error control "ignore"
```

So the stack on each interface is `hidusb` (function driver), with `JmtFltr` **below** it, and
`usbccgp` below that. `JmtFltr` therefore sees the raw URBs that `hidusb` sends, including the
interrupt-IN transfers carrying input reports. The x86 INF is the same thing built from an older
Razer template (`Flter2K.inf for Win2K, Copyright (c) 2003 Razer Inc.`).

Because the filter is registered under the device's hardware key with
`ErrorControl = 0 (ignore)`, a `JmtFltr` that can't load (for example blocked by Memory
Integrity on Windows 11) leaves that interface unable to start rather than crashing the system.

## The virtual joystick INF

`vhidmini.inf` is the Windows DDK `vhidmini` sample ("Virtual Hid Device", `DriverVer
09/21/2006,6.0.5736.1`), rebranded "Belkin". The INF still carries the sample's tiny
vendor-defined `MyReportDescriptor` registry value. The `.sys` (same in x86 and x64) has a
**joystick descriptor compiled in**:

```
Usage Page (Generic Desktop) / Usage (Joystick) / Collection (Application)
  Report ID 1, Input:  X, Y, Z, Rx, Ry, Rz      6 × 8 bit, 0–255
                       1 byte constant (padding)
                       Hat switch               4 bit, 0–7 (8 = centred) + 4 bit constant
                       Buttons 1–24             24 × 1 bit
  Report ID 2, Output: Byte Count (0x3B)        4 × 16 bit
                       Counted Buffer (0x3A)    512 × 8 bit
  Report ID 5, Output: vendor usages 0xFFBE0040 / 0xFFBE0041
```

`n52teHid.exe` finds this device by Usage Page 1 / Usage 4 and writes output report 2 to it.
The first bytes of that report mirror input report 1; see [05](05-n52tehid-service.md).
