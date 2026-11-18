# Reverse-engineering notes: Belkin n52te driver v2.1.2

Reference notes from decompiling Belkin's last official Windows software for the n52te
(`n52te_win_driver_v2.1.2.exe`, 11 July 2008, Windows XP/Vista 32/64-bit). They document how
the original driver, service and editor talk to the keypad, so the features that rely on them
can be rebuilt for Windows 10/11.

**Start with [`../original-driver-analysis.md`](../original-driver-analysis.md)**. It is the
short version: why the scroll wheel, mouse functions, keymap LEDs and D-pad left fail with
this project, and how to fix them. The pages here are the detailed reference behind it.

| Page | Contents |
|---|---|
| [01 - Package contents](01-package-contents.md) | What is in the installer, file hashes, INF files, USB IDs, the composite device layout |
| [02 - JmtFltr.sys filter driver](02-jmtfltr-filter-driver.md) | Dispatch table, device objects, every IOCTL, URB layout, interrupt-report hook, PnP/power behaviour |
| [03 - USB configuration protocol](03-usb-protocol.md) | The HID class SET/GET_REPORT commands, 8-byte command format, checksums, keymap upload image |
| [04 - Onboard profile and key codes](04-onboard-profile-and-codes.md) | The 27 inputs, profile record layout, the full firmware key-code table, factory profile dump |
| [05 - n52teHid.exe service](05-n52tehid-service.md) | Startup sequence, driver events, PC-side action types, the virtual joystick report |
| [06 - n52teCfg.exe editor](06-n52tecfg-editor.md) | Every assignable action and the code/bytes it writes, keymap rules, macro recorder hook |
| [07 - Editor: macros and options](07-n52tecfg-macros-and-options.md) | Macro option bytes p1–p3, recorder VK → code table, mouse and delay steps, size limits, polling rate, Reset Keymap, Import, profile file checksum |

## Status of the findings

* **Verified from code**: read directly from the decompiled or disassembled binaries, and
  where possible cross-checked against the x86 and x64 builds, the factory profile and the manual.
* **Inferred**: the host code only makes sense if the firmware behaves a certain way, but the
  firmware itself was not examined. These are marked *inferred*.
* **Verified on hardware** (November 2026): the configuration protocol, read-back and upload
  ([03](03-usb-protocol.md#verified-on-hardware-november-2026)), and the mouse, media, command
  and keymap-switch codes ([04](04-onboard-profile-and-codes.md#verified-on-hardware-17-nov-2026)).
  Macros and the macro options have not been tested on the keypad yet. Nothing was captured on
  the USB bus from the original software.

## How it was done

1. Download the package from the Internet Archive item
   [`n52te_win_driver_v212`](https://archive.org/details/n52te_win_driver_v212), a mirror of
   the file linked from Belkin support article 4639.
2. Unpack the Chilkat self-extractor with
   [`research/extract_belkin_package.py`](../../research/extract_belkin_package.py), then the
   InstallShield cabinets with `unshield -d payload x data1.cab`. Nothing is executed.
3. Decompile with Ghidra 11.1.2 in headless mode (`analyzeHeadless … -postScript` dumping every
   function to C). Use radare2 for routines Ghidra did not mark as functions, such as the x64
   interrupt completion routine.
4. Decode profiles with
   [`research/decode_n52te_profile.py`](../../research/decode_n52te_profile.py).

The x86 `JmtFltr.sys` in `WDM_NT_LOW/` is a build with `DbgPrint` strings left in (IOCTL names,
source file paths). That made it the easiest to read. The x64 build in `WDM_XP64/` has the
same logic and constants.

Addresses in these notes are virtual addresses in the original images (image base `0x10000`
for the drivers, `0x400000` for the executables).

## Not included

The Belkin/Razer binaries and the raw decompiler output are **not** in this repository. They
are Belkin/Razer copyright. These notes describe behaviour, data formats and constants only.
Use the steps above to regenerate the decompilation locally.
