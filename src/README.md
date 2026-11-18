# n52te Editor and Companion (source)

Preview of the replacement for the Interception remapper. See
[`docs/onboard-editor-plan.md`](../docs/onboard-editor-plan.md) for the design and status.

| Project | What |
|---|---|
| `N52te.Core` | Profile model, firmware key codes, keymap image builder, Belkin `.ini` import/export, JSON profiles, text-to-macro, Companion hotkey allocation, keypad client with a safety guard. No Windows dependencies |
| `N52te.Core.Tests` | xUnit tests (run on any OS) |
| `N52te.Editor` | WinForms editor (`n52te-editor.exe`) |
| `N52te.Companion` | WinForms tray app (`n52te-companion.exe`) |

Neither app installs a driver or hooks the keyboard. The Companion reacts to hotkeys with
`RegisterHotKey` only.

## Build and test

Needs the .NET 10 SDK (long-term support, supported until November 2028). The Windows apps can also be built on Linux/macOS with Microsoft's SDK
(`EnableWindowsTargeting` is set). Distribution SDKs without the Windows Desktop targets can
only build `N52te.Core`.

```sh
dotnet test src/N52te.Core.Tests
# optional: also check byte-for-byte against Belkin's factory profile (not in this repository)
N52TE_BELKIN_DEFAULT_INI=/path/to/User_Profiles/Default.ini dotnet test src/N52te.Core.Tests

dotnet publish src/N52te.Editor    -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out
dotnet publish src/N52te.Companion -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out
```

For standalone builds that need no runtime installed (about 52 MB each, runtime included):

```sh
dotnet publish src/N52te.Editor -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o out
```

Standalone builds only get .NET security fixes when they are rebuilt. The CI workflow publishes
both variants as build artifacts.

## Status

* Done: everything that doesn't need the keypad. That covers the profile model, editing all 27
  inputs in 3 keymaps, macros with a recorder, text-to-macro, Companion actions and app rules,
  validation, and Belkin `.ini` import/export (byte-identical round trip of the factory profile).
* Not yet: **saving directly to the keypad** and **keymap switching from the PC**. Both wait for
  the probe results and the protocol capture (phases 0–1 of the plan). Until then, export a
  Belkin `.ini` and load it with the original software in a Windows XP/7 virtual machine.

Shared files live in `%APPDATA%\n52te\` (`Profiles\`, and `active-profile.json` for the Companion).
