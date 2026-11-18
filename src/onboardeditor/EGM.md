# n52te Editor (onboardeditor)

## Overview

This folder holds the WinForms UI for `n52te Editor.exe`, the onboard keymap
editor. Its layout copies Belkin's n52te v2.1.2 editor. All types are `internal`
and live in namespace `N52te`. `build.ps1` builds the exe from this folder plus
`src/core`, with `/target:winexe` and references to `System`, `System.Core`,
`System.Drawing` and `System.Windows.Forms`. There is no `.csproj`.

The protocol, profile and macro code lives in `src/core`, not here. See the
[core contract](../core/EGM.md). From core this UI uses `KeypadClient`,
`KeypadSession`, `KeypadLayout`, `KeymapImage`, `FirmwareCodes`, `InputRecord`,
`Profile`, `Macro`, `ProfileIni`, `SavedMacro` (MacroFile.cs), `UsbDk` and
`UsbDkTransport` (Transport.cs).

## Interface

| File | Types | Role |
| --- | --- | --- |
| `Program.cs` | `EditorProgram` | Entry point `[STAThread] Main(string[] args)`. Every path that returns gives 0; there is no top-level exception handler. |
| `MainForm.cs` | `MainForm`, `Settings` | The main borderless window: keymap boxes, profiles and keypad actions. Also loads and saves the per-user settings. |
| `Assignment.cs` | `Assignment`, `Assignment.Item` | Label text for an input, and the menu tables of firmware command codes. |
| `Dialogs.cs` | `DarkStyle`, `SingleKeyDialog`, `MacroDialog`, `NameDialog` | Small modal dialogs and their dark styling. |
| `ProfileList.cs` | `ProfileList`, `ProfileList.Entry` | The borderless profile drop-down. |
| `MacroEditor.cs` | `MacroLayout`, `MacroSkin`, `MacroEditor`, `MacroTab` | The macro editor panel that slides out of the main window, and its collapsed tab. |
| `Skin.cs` | `SkinLayout`, `Skin` | Main-window layout and artwork: Belkin's JPGs, or artwork the program draws itself. |
| `SkinDialog.cs` | `SkinDialog` | Prompt dialog in Belkin's style. |

### Command line (`Program.cs:EditorProgram.Main`)

- No arguments: runs `Application.Run(new MainForm(true))`, the live editor.
- `--screenshot <out.png> [--skin <dir>] [--hover <input>] [--macro]`: builds
  `new MainForm(false)`, calls `SaveScreenshot(path, skin, hover, macro)` and
  exits. The flag needs a value after it to take effect. `--hover` is parsed
  with `int.Parse`, so a non-numeric value throws.
- `--screenshot-dialogs <folder>`: works only as the sole flag
  (`args.Length == 2`). Writes `dialog-save.png`, `dialog-import.png` and
  `dialog-apply.png` with `SkinDialog.SaveSample` at scale 1.5.

### Key members

- `MainForm(bool live)`: `Settings.Load()` runs in both modes (the scale comes
  from it). `live=false` means offline or screenshot mode, with no keypad, the
  built-in skin and no presence timer. `live=true` loads the Belkin
  skin from settings, creates the profile folder, starts the presence timer and,
  once shown, reads the keypad (or the last profile if the keypad is absent).
- `MainForm.SetStatusText(string)`: public wrapper that `MacroEditor` uses to
  change the status line. `MainForm.SaveScreenshot(path, belkinFolder,
  highlight, withMacroEditor)` renders the window offline at scale 1.5 as a PNG.
- Private `MainForm` features: the right-click input menu (`ShowInputMenu`), the
  window menu (`ShowWindowMenu`), the profile buttons (New, Save, SaveAs,
  Import, Delete) and keypad actions (`ReadFromKeypad`, `ApplyChanges`,
  `SetPolling`, `ResetKeymap`).
- `Settings`: fields `BelkinSkin`, `Scale`, `LastProfile`, `OnKeypad`. Methods
  `static Load()` and `Save()`.
- `Assignment.Short(int, InputRecord)` and `Assignment.IsInactive(int,
  InputRecord)`. Menu tables `Keymap` (0xD9-0xDF), `Mouse` (0xD0-0xD8),
  `BasicCommands`, `AdditionalCommands`, `Media` and `MoreMedia`, all of type
  `Item[]`. `Item.Works` is false when an item has the `NothingOnWindows` note.
- `SingleKeyDialog(inputName).Code` returns a byte.
  `MacroDialog(inputName, InputRecord, isMacro)` returns `.Sequence` and
  `.Options`. Options has 3 bytes: mode, times-1 and interval index.
  `NameDialog(title, prompt, initial).Value` returns the name, and
  `NameDialog.IsValidName(string)` checks one.
- `ProfileList(List<Entry>, float scale).Show(owner, at, width)`. A `null`
  entry is drawn as a separator.
- `MacroEditor(MainForm owner)` members: `SetLook(belkinFolder, scale)`,
  `ShowState()`, `Toggle()`, `Expanded`, `MacroName`, `HasSteps`, `Current()`
  (returns a `SavedMacro`), `LoadMacro(SavedMacro, path)`, `SaveMacro(asNew)`,
  `StopRecording()`, and the screenshot helpers `Render()` and `Demo()`.
- `MacroSkin.Load(belkinFolder)` returns an `IDisposable` holding the
  `Normal`, `Over`, `Down` and `Tab` bitmaps.
- `Skin.IsBelkinSkin(folder)`, `Skin.LoadBelkin(folder)`, `Skin.BuiltIn()`,
  `Box(n)`, `Normal(km, isConnected)`, `Over(km)`, `Down(km)`, `Source`,
  `BoxFill`, `Text`. `Box(n)` returns `SkinLayout.Boxes[n-1]` for a Belkin skin;
  the built-in skin moves inputs 15-19 to x=266, width 95.
  `SkinLayout.IconFor(input)` maps an input to its group icon.
- `SkinDialog.Ask(owner, scale, title, message, params buttons)` returns the
  index of the button chosen, or -1. `AskWithDetail(...)` adds detail lines.
  `Tell(...)` shows a single "Ok" button. `SaveSample(path, scale, ...)` writes a
  PNG. Enter returns 0 and Esc returns -1. Detail text is cut to 18 lines plus
  an "... and N more" line, and the dialog is at least 143 logical pixels high.
- `ProfileList` rows are 20 logical pixels high and at most 14 show at once; the
  mouse wheel scrolls 3 rows. The list closes on deactivate or Esc.

This folder has no subdirectories.

## Invariants

- **Fixed logical layouts.** The main window is 468x509 (`SkinLayout`) with 27
  boxes and 27 picture rectangles, indexed by input-1. Painting and hit-testing
  of the label boxes go through `Skin.Box(n)`, not `SkinLayout.Boxes` directly.
  The macro panel is 266x456 with list rows 15 px high, and its tab is 22x143
  (`MacroLayout`). `SkinDialog` is 460 wide. Geometry is in logical pixels:
  most painting uses `ScaleTransform(scale)`, and hit-testing divides mouse
  coordinates by `scale`. Exceptions: `SkinDialog.OnPaint` resets the transform
  to draw the message with a scaled font, `MacroTab.OnPaint` stretches its
  bitmap to `ClientSize`, and `ProfileList` draws its border before the
  transform.
- **Scale** (`MainForm` constructor). The window uses `Settings.Scale` when it
  is greater than 0. Otherwise the scale is the DPI factor times 1.25, rounded to
  a quarter, and at least 1. The Size menu offers only 1, 1.25, 1.5, 1.75, 2
  and 2.5. `Program.Main` calls `SetProcessDPIAware` first.
- **Keypad access.** Every `KeypadClient` operation (read, apply, polling) goes
  through `MainForm.OnKeypad`. It sets `busy`, then runs `Task.Run` with
  `using (KeypadClient.OpenUsbDk())`, so each operation opens and disposes a
  new client on a worker thread. `busy` is cleared when each `OnKeypad` call
  ends, so in `ApplyChanges` it is clear while the confirmation dialog is open,
  between the read and the write. While `busy` is set, the input menu, Read,
  Apply and polling changes are blocked, and closing the form is cancelled.
  The busy guard does not block New, Save, Save As, Import, Delete, Reset
  Keymap or the map tabs.
- **Presence.** `UsbDkTransport.IsPresent()` runs on the UI thread, not through
  `OnKeypad`: from the `Shown` handler, from a 3000 ms timer (`CheckPresence`,
  skipped while `busy`) and from the `ReadFromKeypad` failure handler. An
  exception counts as not present.
- **Read.** After a read, `FindSavedProfile` compares the keypad's keymap images
  with each saved profile, trying `Settings.OnKeypad` first. A match opens that
  profile and records it in `Settings.OnKeypad`; otherwise the read profile
  opens unsaved. A failed silent read at startup falls back to
  `LoadLastProfile`.
- **Apply order** (`ApplyChanges`):
  1. Run `profile.Check()`.
  2. Read the keypad with `KeypadSession.Read`.
  3. Compute `KeypadSession.Differences`. With no differences, it stops there.
  4. Confirm with `SkinDialog.AskWithDetail`.
  5. Call `KeypadSession.Apply`.
  6. On success, record `Settings.OnKeypad`.
- **Keymap switches.** Codes for which `FirmwareCodes.IsKeymapSwitch` is true
  are written to all three keymaps (`Replace`). When the replaced code was a
  switch, `Replace` also overwrites the other keymaps where that input held the
  same switch code. `ResetKeymap` asks for confirmation, resets inputs 24 and up
  (the D-pad diagonals) in all keymaps, and sends inputs below 24 that hold a
  switch through `Replace` before resetting them.
- **Polling.** Intervals are {8, 2, 1} ms for 125, 500 and 1000 Hz
  (`SetPolling`, `ReadFromKeypad`).
- **Profiles.** At most 100 `*.ini` files are allowed in the profile folder
  (`MaxProfiles`), but overwriting an existing file is always allowed. Names are
  1-60 characters after trimming, with no invalid file-name characters
  (`NameDialog.IsValidName`). `MainForm.ConfirmDiscard` asks before New,
  Import, Open Profile File, picking a profile or the factory profile from the
  list, Read from Keypad (menu) and closing. `DeleteProfile` does not call it:
  it asks only to confirm the delete, and unsaved edits are dropped.
- **Macros** (`MacroEditor`):
  - At most `Macro.MaxPairs` (128) steps.
  - A delay step holds at most `Macro.MaxDelayUnits` (127) units of 50 ms.
  - Leading delays are only partly prevented: `InsertDelay` refuses to add a
    delay to an empty list, `DeleteSelected` removes delays left at the start,
    and recording adds no delay before the first event. `MoveSelected` can
    still move a delay to the start, and `LoadMacro` keeps a leading delay read
    from a file.
  - Options are mode 0-2, times 0-9 and interval 0-4, but out-of-range values
    are handled differently. `LoadMacro` resets each to 0. `MacroDialog` resets
    to mode 0, times 1 (shown value), interval 0. Clicking Times or Interval
    wraps modulo 10 or 5. Only the mouse wheel clamps. `MainForm.AssignMacro`
    resets only an out-of-range mode and passes times and interval through
    unchecked.
  - Saving refuses an empty macro, and asks for confirmation when presses
    outnumber releases (`SavedMacro.PressesMoreThanReleases`).
- **Recording.** `MacroEditor.OnHook` skips auto-repeat: an event with the same
  code and the same up/down state as the last one. It does not record keys that
  the keypad lacks (`FirmwareCodes.FromVirtualKey` returns 0) and shows a "key
  not on the keypad" note instead. Numpad Enter is recorded as 0x74, as in
  `SingleKeyDialog`. It always calls `CallNextHookEx`, so keys still reach
  other programs. The hook delegate is kept in a field so it is not
  garbage-collected.
  - `RecordDelay` adds no delay before the first event, when "Ignore delays"
    is set, for gaps under one unit, or when only one step is left.
  - Clicking the panel's mouse buttons while recording records a delay, a press
    and a release.
  - `RecordEvent` stops recording when the list already holds 128 steps.
  - `StopRecording` runs from `LoadMacro`, `SaveMacro`, `RecordEvent` (list
    full), `MacroEditor.OnFormClosing` and `MainForm.OnFormClosing`.
- **Skins.**
  - A folder counts as a Belkin skin only if it has both `main.ini` and
    `main_normal1_2.jpg` (`Skin.IsBelkinSkin`).
  - `Skin.LoadBelkin` has no per-file fallback. If it throws,
    `MainForm.LoadSkin` switches to `Skin.BuiltIn()`.
  - `MacroSkin.Load` falls back to built-in artwork if any of its JPGs fails to
    load.
  - The old skin is disposed after a new one is loaded.
- **Single instance.** No mutex enforces a single instance.

## Side Effects

- **Settings file.** `%APPDATA%\n52te\editor.ini` holds `key=value` lines:
  `BelkinSkin`, `Scale`, `LastProfile`, `OnKeypad` (`Settings`). `Load` and
  `Save` swallow only `IOException`.
- **Profiles.** Stored in `Documents\n52te\Profiles\*.ini`
  (`MainForm.ProfileDir`). Profiles are created, listed, read and written with
  `ProfileIni`, and deleted with `File.Delete`. Import reads the chosen file
  with `ProfileIni.Read` and writes a new file into this folder with
  `ProfileIni.Write`, asking for a new name if the name is taken. Export and
  Open work with any path; after "Open Profile File...", Save writes back to
  that outside path. `DeleteProfile` clears `Settings.OnKeypad` if it named the
  deleted file, then loads the next profile or the factory profile.
- **Macros.** Saved macros live in `SavedMacro.Folder` and are written and
  deleted there (`MacroEditor.SaveMacro`, `DeleteMacro`). "Open a Belkin Macro
  File..." (`OpenMacro`) reads any path; a file outside the folder opens with
  no path, so saving it creates a new macro in the folder.
- **USB.** Keypad commands go through `KeypadClient.OpenUsbDk()` on a worker
  thread (`OnKeypad`). The editor calls `KeypadSession.Read`, `KeypadSession.Apply` (which
  backs up each changed keymap first), `PollingRate` and `SetPollingInterval`.
  On the UI thread it reads `UsbDk.IsInstalled` and calls
  `UsbDkTransport.IsPresent()`.
- **Global keyboard hook.** While recording, a low-level keyboard hook is
  installed with `user32 SetWindowsHookEx(13 /* WH_KEYBOARD_LL */)`, using
  `kernel32 GetModuleHandle(null)`. Events are passed on with `CallNextHookEx`,
  and the hook is removed with `UnhookWindowsHookEx` (`MacroEditor`).
- **Other P/Invoke.** `user32 SetProcessDPIAware` (Program). `user32
  ReleaseCapture` and `SendMessage(WM_NCLBUTTONDOWN, HTCAPTION)` let the user
  drag the borderless windows (`MainForm`, `SkinDialog`).
- **Explorer.** `Process.Start` opens the profiles folder, the backups folder
  (`KeypadSession.BackupDir`) and the macros folder.
- **Image reads.** From a user-chosen Belkin folder the editor reads
  `main_normal{1..3}_{1,2}.jpg`, `main_over{1..3}_2.jpg`,
  `main_down{1..3}_2.jpg`, `Macro_Editor_{normal,over,down}.JPG` and
  `macro_close.JPG`. None of this artwork is bundled with the repository.
- **PNG writes.** Only the screenshot command-line modes write PNGs
  (`MainForm.SaveScreenshot`, `SkinDialog.SaveSample`).
- **Not used.** The editor makes no registry or clipboard calls.

## Verification

- Build: `powershell -ExecutionPolicy Bypass -File .\build.ps1` (run from the
  repository root). This compiles `n52te Editor.exe`.
- There are no automated UI tests. `n52te-config selftest` covers only
  `src/core`. To check the UI by eye, render it with
  `"n52te Editor.exe" --screenshot out.png [--macro]` or
  `"n52te Editor.exe" --screenshot-dialogs <folder>`.
