# n52te Editor for Windows 11 — Belkin / Razer Nostromo n52te

**Program the Belkin n52te (a.k.a. Razer Nostromo n52te / n52 Tournament Edition) gaming keypad on Windows 11 and Windows 10 (64-bit), the way Belkin's original software did:** edit its three keymaps, write them into the keypad, and the keypad runs them by itself — keys, mouse buttons, scroll wheel, media keys, macros and Blue / Red / Green keymap switching with the light — on any PC, with nothing running in the background.

> **Status: version 2 is in development on this branch.** The editor and `n52te-config` work and have been tested on a real keypad, but there is no v2 release yet. The previous version (a remapper based on the Interception driver) is still available as **[v1.3.0](../../releases/tag/v1.3.0)**; its source is at the [`v1.3.0` tag](../../tree/v1.3.0).

## Why

The official Belkin/Razer software (`n52te v2.1.2`, July 2008) was built for Windows XP/Vista. Its driver doesn't load on Windows 11, so its editor shows *"n52te not connected"* and the keypad can't be reprogrammed. The keypad itself still works: it keeps running whatever profile is stored in its memory. This project talks to that memory again, using the protocol worked out from Belkin's software and verified on hardware.

## What's here

- **`n52te Editor`**: an editor laid out like Belkin's n52te Editor v2.1.2 — Map 1 / Map 2 / Map 3 (Blue, Red, Green), a box for each of the 27 inputs (14 keys, round thumb button, wheel up / click / down, thumb pad, D-pad with diagonals) with Belkin's right-click menu, polling rate, profiles (New, Save, Save As, Import, Delete), **Reset Keymap** and **Apply Changes**.
  - It reads what the keypad holds when it starts, and **Apply Changes** writes only what changed, after backing up each keymap.
  - Profiles are Belkin's own v2.1.2 `.ini` format, so old Belkin profiles import as they are.
  - **Belkin's artwork**: point the editor at your own copy of Belkin's v2.1.2 software (right-click the window → *Artwork*) and it uses Belkin's original images. They are Belkin/Razer copyright and are not part of this project; without them the editor draws its own artwork in the same layout.
- **`n52te-config`**: the same functions from the command line (read, export, apply, macros, keymap switching, polling rate, self-test). See [docs/profile-tool-design.md](docs/profile-tool-design.md).

What the keypad can do by itself, tested on hardware: any key, mouse buttons, scroll wheel and pointer moves, all media and browser keys, macros (with delays, run N times, repeat while held, repeat until the next key) and keymap switching (toggle, while held, cycle) with the light following. Belkin's PC-side actions (launch an application, load a profile, joystick) and its "commands" (Cut, Copy, Paste…) needed Belkin's background service and don't work on Windows 11; the editor shows them greyed out. Details: [docs/reverse-engineering/04](docs/reverse-engineering/04-onboard-profile-and-codes.md#verified-on-hardware-17-nov-2026).

## Requirements

| | |
|---|---|
| **Device** | Belkin n52te · Razer Nostromo n52te · n52 Tournament Edition (`VID_050D&PID_0200`) |
| **OS** | Windows 11 or Windows 10, 64-bit; Memory Integrity can stay on |
| **UsbDk** | [UsbDk 1.0.22 x64](https://github.com/daynix/UsbDk/releases/tag/v1.0.22) (free, signed by Red Hat). It lends the keypad to the editor for the few seconds a read or write takes; the keypad doesn't type meanwhile. **Restart Windows after installing UsbDk.** |

## Installing (development builds)

Build it (below) and run `build\Install-n52te.exe` as administrator, or see **[INSTALL.md](INSTALL.md)**. The installer puts `n52te Editor.exe` and `n52te-config.exe` in `C:\Program Files\n52te`, adds shortcuts, and removes what version 1 installed: the remapper and its autostart, the AutoHotkey editor and, if you agree, the Interception driver (after creating a restore point).

## Building from source

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

It uses the C# compiler that ships with Windows (no Visual Studio, no SDK), runs the self-test, and writes `n52te-config.exe`, `n52te Editor.exe`, `Install-n52te.exe` and its `.sha256` to `build\`.

| Folder | Contents |
|---|---|
| [`src/core`](src/core) | Keypad protocol over UsbDk, keymap images, profiles, Belkin `.ini` files, code tables |
| [`src/onboardeditor`](src/onboardeditor) | The editor (WinForms) |
| [`src/configtool`](src/configtool) | `n52te-config` and its self-test |
| [`src/installer`](src/installer) | The installer |

Plan and progress: [docs/onboard-editor-plan.md](docs/onboard-editor-plan.md).

## How the original Belkin software worked

How Belkin's 2008 driver, service and editor worked, and the keypad's protocol and profile format: **[docs/original-driver-analysis.md](docs/original-driver-analysis.md)** and the reverse-engineering notes in [docs/reverse-engineering/](docs/reverse-engineering/README.md).

## Credits and licenses

- This repository started as a fork of INFOMALENA's n52te remapper for Windows 11 (version 1, MIT license, see [LICENSE](LICENSE)).
- **[UsbDk](https://github.com/daynix/UsbDk)** by Daynix / Red Hat (Apache 2.0), installed separately.
- Original driver, software and device: **Belkin International** / **Razer**.

## Disclaimer

**Unofficial** project, **not affiliated** with Belkin or Razer. "n52te", "Nostromo", "Belkin" and "Razer" are trademarks of their respective owners, used descriptively only. Software provided *as is*, with no warranty. Writing to the keypad's memory and installing drivers is at your own risk; every write keeps a backup of the keymap it replaces in `Documents\n52te keymaps`.
