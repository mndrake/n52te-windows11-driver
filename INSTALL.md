# Installation guide — n52te Editor on Windows 11

Version 2 is in development: there is no release yet, so build the installer first (see the [README](README.md#building-from-source)). The previous version, a remapper based on the Interception driver, is release [v1.3.0](../../releases/tag/v1.3.0).

## 📋 Before you start

- **Windows 11 or Windows 10, 64-bit**, and **administrator rights** for the installer.
- The **n52te plugged in** over USB, directly or through a hub (not attached to a virtual machine).
- **UsbDk 1.0.22 x64**: download `UsbDk_1.0.22_x64.msi` from [its releases page](https://github.com/daynix/UsbDk/releases/tag/v1.0.22), install it, and **restart Windows**. Installing it restarts the USB stacks; on the test PC that left a Bluetooth keyboard not typing until the restart. The installer tells you if UsbDk is missing.

## 🚀 Installing

1. Run `build\Install-n52te.exe` and accept the administrator prompt. Windows SmartScreen may warn about an unsigned program: **More info → Run anyway**.
2. Click **Install**. The installer:
   1. closes the n52te programs that are running;
   2. removes what version 1 installed: the remapper and its autostart, the AutoHotkey editor and their shortcuts (your old remapper settings in `C:\ProgramData\n52te` are kept, but the new editor doesn't use them);
   3. removes leftovers of Belkin's 2008 driver;
   4. if the Interception driver from version 1 is still there, asks whether to remove it (recommended). It creates a restore point first, takes Interception out of the keyboard and mouse filter lists, then removes its services and moves its driver files to `C:\ProgramData\n52te\removed-drivers`;
   5. copies `n52te Editor.exe` and `n52te-config.exe` to `C:\Program Files\n52te` and adds the **n52te Editor** shortcut (Start menu and Desktop) and **n52te Keymaps (command prompt)**;
   6. checks for UsbDk.
3. If it asks you to, restart Windows.

## 🎮 First use

1. Open **n52te Editor**. It reads the keypad and shows its three keymaps: **Map 1** (Blue), **Map 2** (Red), **Map 3** (Green).
2. Click an input's box (or the key on the picture) and choose what it does: a single key, a macro, a keymap switch, a mouse button or the wheel, a media key, *Reset Key to Default* or *Disable Button*. Keymap switches are written to all three keymaps, as in Belkin's editor.
3. Click **Apply Changes**. The editor lists what will change, backs up each keymap it writes, writes it, checks it by reading it back, and switches the keypad to the keymap whose tab is showing. The keypad doesn't type for those few seconds.
4. **Save** keeps the profile in `Documents\n52te\Profiles` (Belkin's `.ini` format); **Import** loads a Belkin profile.

### Macros

Click the **MACRO EDITOR** tab on the window's right edge. Click **Record**, type the keys (the editor records them from any window, with the time between them unless *Ignore delays* is ticked), then **Stop**. Use **Advanced** for media keys and other keys, the mouse picture for mouse clicks, **Insert Delay** for pauses (choose the length in the box below it), and **Move Up / Move Down / Delete** to tidy up. Pick the **Macro Behavior**, then **Save** it under a name (`Documents\n52te\Macros`). To use it, right-click an input → **Macro** → the macro's name, then **Apply Changes**. A macro holds up to 128 steps, delays included.

Right-click the window background for more: *Read from Keypad*, profile files, *Artwork* (use Belkin's original images from your own copy of the v2.1.2 software), window size, and the backups folder.

## ⬆️ Upgrading from version 1

Run the new installer: it removes the remapper, its autostart and editor, and offers to remove Interception. Version 1's button assignments lived in the remapper, not in the keypad, so they can't be carried over: set them up again in the editor (keys, macros and keymap switches all run in the keypad now). Typed text and launching programs aren't possible from the keypad.

## 🔄 Uninstall

Delete `C:\Program Files\n52te` and the **n52te Editor** shortcuts. The keypad keeps the keymaps last written to it and works without any software. UsbDk can be removed in *Settings → Apps* (it's listed as *UsbDk Runtime Libraries*); never delete its files by hand, because it is registered as a filter for all USB devices.

## 🆘 Troubleshooting

| Symptom | Fix |
|---|---|
| The editor says *n52te not connected* | Check the keypad is plugged in, that UsbDk is installed, and that Windows was restarted after installing UsbDk. |
| *UsbDk couldn't take over the keypad* | Close other programs using it (another editor window, `n52te-config listen`), or unplug and replug the keypad. |
| A write failed | Nothing is lost: the editor names the backup it made (`Documents\n52te keymaps`). Restore it with `n52te-config write FILE KEYMAP --yes` from **n52te Keymaps (command prompt)**. |
| A button does nothing | It may be set to a Belkin PC-side action or command, which don't work on Windows 11 (shown greyed in the editor), or disabled. Give it a key or *Reset Key to Default*. |
| **No keyboard works, even at the sign-in screen** | A keyboard filter entry points to a driver that's gone. Sign in with the on-screen keyboard (Accessibility icon), open PowerShell as administrator and run the two commands below, then restart. |

```powershell
Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4D36E96B-E325-11CE-BFC1-08002BE10318}' UpperFilters -Value @('kbdclass') -Type MultiString
```
```powershell
Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4D36E96F-E325-11CE-BFC1-08002BE10318}' UpperFilters -Value @('mouclass') -Type MultiString
```

## ❓ Memory Integrity

The editor and `n52te-config` work with Memory Integrity (Core isolation) on. Windows won't turn it on while version 1's Interception driver is installed, which is one reason the installer offers to remove it.
