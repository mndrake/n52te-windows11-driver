# Installation guide — n52te on Windows 11

This guide explains how to install the **Belkin / Razer n52te** software with the `Install-n52te.exe` installer.

## 📋 Before you start

- **Windows 11 or Windows 10 (64-bit).**
- You'll need **administrator rights** (the installer asks for them on its own).
- Have the **n52te plugged in** over USB (not required to install, but you'll need it to test at the end).

## 🚀 Step-by-step installation

### 1. Download the installer
From the repository's **[Releases](../../releases/latest)** tab, download **`Install-n52te.exe`**.

### 2. Run it
Double-click `Install-n52te.exe`.

### 3. SmartScreen warning (normal)
Because this is free software without a paid code-signing certificate, Windows may show **"Windows protected your PC"**:

> Click **More info** → **Run anyway**.

### 4. Accept User Account Control (UAC)
Windows will ask for administrator permission (needed to install the driver). Click **Yes**.

### 5. Click "Install"
In the installer window, click **Install**. The installer:
1. Removes leftovers of old Belkin drivers.
2. Installs the **Interception** driver.
3. Copies the editor and the remapper to `C:\Program Files\n52te`.
4. Creates shortcuts on the Desktop and in the Start menu.
5. Leaves the n52te ready to use.

### 6. Restart if asked
The **first time** the driver is installed, Windows needs a **restart** to activate it. If the installer says so, click **Restart now** (or restart whenever you like). If the driver was already active, no restart is needed.

## 🎮 First use

1. Open **n52te Editor** (shortcut on the Desktop).
2. Click a button on the keypad drawing (or click **Identify** and press a physical button on the n52te).
3. Choose what the button does (a key, a combination, text, or opening a program) and click **Assign to this button**.
4. **To test it, click OUTSIDE the editor** (while the editor is in front, the n52te types raw keys) and press the button on the n52te.
5. Check **"Start the remapper with Windows"** if you want it to always work without opening anything.

### Blue, Red and Green keymaps

Each button can do something different in each of three keymaps, like the original Belkin software:

1. Pick a button to switch keymaps (for example the round thumb button), choose **Switch keymap (Blue / Red / Green)**, then pick **Cycle keymaps**, a color (stays until you switch again) or a color *only while held*. A switch button works the same in all three keymaps.
2. Click the **Blue**, **Red** or **Green** tab above the keypad drawing and assign the other buttons for that keymap. Buttons with no action in Red or Green send their normal key.
3. The keypad's light doesn't follow these keymaps: it shows the keymap stored in the keypad's own memory, which only Belkin's old driver can change. The editor shows the **Remapper keymap** at the top left, and the remapper window's title shows it too.

> 💡 Your normal keyboard is **never affected**: only the n52te's keys are remapped.

## ⬆️ Upgrading from an earlier (Spanish) version

Just run the new installer. It keeps your existing button configuration, replaces the old `Editor n52te.exe` with `n52te Editor.exe`, and removes the old *Editor n52te* and *Remapeador n52te* shortcuts.

## 🔄 Uninstall

1. **Turn off autostart**: open the editor and uncheck *"Start the remapper with Windows"*.
2. **Delete the files**: delete the `C:\Program Files\n52te` folder and the shortcuts.
3. **(Optional) Remove the Interception driver**: open PowerShell **as administrator** and run:
   ```powershell
   & "C:\Program Files\n52te\install-interception.exe" /uninstall
   ```
   Then restart. (If you already deleted the folder, download Interception from its [official repository](https://github.com/oblitum/Interception) to uninstall it.)

## 🆘 Troubleshooting

| Symptom | Fix |
|---|---|
| The editor says *"the remapper isn't running"* | Open the **n52te Remapper** shortcut, or check *"Start the remapper with Windows"* and restart. |
| I assigned a key and nothing changes | Remember to click **outside** the editor before testing the button. If the text under the drawing says the remapper **does not recognize** your n52te, or your keypad sends other keys (a stored red/green/blue profile), click **Learn keys** in the editor. |
| *"Access is denied"* when saving | Versions up to 1.1.0 created the config folder as admin-only. Run the current installer again: it gives normal users write access to `C:\ProgramData\n52te`. |
| The n52te doesn't show up | Check that it's plugged in and that the driver loaded: restart after installing. |
| The keyboard acted strangely during install | Restart the computer; everything starts clean. |

## ❓ Do I need to turn off Memory Integrity?

**No.** The Interception driver this project uses is compatible with Windows 11 Memory Integrity (HVCI), unlike the original 2008 Belkin driver. You don't have to turn off any protection.

---

<sub>Made by <b>INFOMALENA</b> · Software Development</sub>
