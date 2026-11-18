# research

## Overview

Standalone reverse-engineering tools for the Belkin n52te keypad (USB `050d:0200`).
They were used to unpack Belkin's v2.1.2 driver package, decode its profile format,
and test the keypad's USB control protocol on real hardware. The write-ups of what
they found are in [docs/reverse-engineering](../docs/reverse-engineering/README.md),
mainly `03-usb-protocol.md` and `04-onboard-profile-and-codes.md`.

These files are not part of the product. `build.ps1` compiles only `src\`, nothing
in `src\` imports them, and nothing here imports from `src\`. The three pyusb scripts
(`n52te_keymap.py`, `n52te_readback.py`, `n52te_upload.py`) are written for Linux
with root (they detach the kernel `usbhid` driver), for example inside a VM with
the keypad passed through. `N52teMonitor.cs` is the only Windows tool here.

## Interface

| File | Entry point / symbols | Purpose |
| --- | --- | --- |
| `decode_n52te_profile.py` | `main(path)`, `name(code)`; tables `CODES`, `INPUTS`, `MACRO_MARK`, `PC_MARK`, `PC_TYPES`, `KEYMAPS`, `REC = 0x104` | Decodes a Belkin editor profile `*.ini` (`[macro] Value=<hex>`) into 3 keymaps x 27 inputs, then prints any trailing bytes after the 3 x 27 records. Usage: `decode_n52te_profile.py [profile.ini]`; reads `Default.ini` when no argument is given. Exits with a message if the file has no `Value=` blob |
| `extract_belkin_package.py` | `main(src, out)`, `MAGIC = 9B E5 27 6D` | Unpacks the Chilkat self-extractor `n52te_win_driver_v2.1.2.exe` without running it. Usage: `extract_belkin_package.py <exe> <outdir>` |
| `n52te_keymap.py` | `main(argv)`, `block(sub, value)`, `checksum_ok(r)`, `NAMES` | Reads firmware version (`GET_REPORT` wValue 6) and active keymap (wValue 5). Selects a keymap with `SET_PROFILEID` (`SET_REPORT` wValue 4) when given `blue`/`green`/`red`/`0`/`1`/`2`. Action `read` (the default) only reads. A failed firmware-version request is printed and the script continues. Flag `--interface N` sets wIndex on every request (default 0) |
| `n52te_readback.py` | `main(argv)`, `read_image(dev, km)`, `decode(km, image)`, `name(code)`, `FIRMWARE_KEYMAPS`, `CHUNK = 0x200`, `CHUNKS = 14` | Reads a 0x1C00-byte keymap image with `GET_GROUP_SETTING` (`GET_REPORT` wValue 3, wIndex `km << 8 \| chunk`) and decodes it. Imports its tables from `decode_n52te_profile` and defines its own `name(code)`. Usage: `n52te_readback.py [0\|1\|2 ...]` (default 0). Keymap arguments are not range-checked. If a chunk fails, reading stops and the partial image is still saved and decoded |
| `n52te_upload.py` | `main(argv)`, `class Keypad` (`read_keymap`, `write_keymap`, `close`, `_xfer`), `checksum(data)`, `upload_header(image, km)`, `compare(before, after)`, `save(name, data)` | Writes a keymap image to the keypad (`SET_REPORT` wValue 1 per chunk, then `GET_REPORT` wValue 2 checksum check). Modes: `--roundtrip KM`, `--image FILE KM --yes`, `--set KM INPUT CODE --yes`. Reads the active keymap (`GET_REPORT` wValue 5) before and after. Sleeps 0.5 s between upload attempts and before the read-back. `main` returns 0 when the read-back matches, 1 when the upload failed, 2 when the read-back differs; argument and device errors exit through `sys.exit(message)`. `--set` imports `INPUTS`, `MACRO_MARK` and `name` from `decode_n52te_profile` only in that mode. `--image` exits unless FILE is exactly 0x1C00 bytes, but only after the "before" image has been read and saved |
| `N52teMonitor.cs` | `class Monitor : Form` (`Main`, `WndProc`, `OnInput`, `Write`, `Name`, `Short`) | Windows Raw Input logger for keyboard, mouse, consumer control and system control reports from the n52te. It also logs its own text box's KeyDown/KeyPress events and window focus changes. `Write` timestamps each line and writes it to the console and the log. Usage: `n52te-monitor [LOGFILE] [--all]`; the last non-`--all` argument is the LOGFILE |

There are no subdirectories.

## Invariants

- **Keymap numbering differs by source.** `decode_n52te_profile.py` labels the
  editor profile blob's keymaps 0 = Blue, 1 = Green, 2 = Red (`KEYMAPS`).
  `src/core/ProfileIni.cs` does not reorder them and treats blob keymap `k` as
  firmware keymap `k`, so the two disagree. The firmware
  numbers them 0 = Blue, 1 = Red, 2 = Green (`n52te_keymap.NAMES`,
  `n52te_readback.FIRMWARE_KEYMAPS`). The docstring of `n52te_keymap.py` says
  2 = Green is inferred, not verified.
- **Input order differs by source.** The profile blob stores 27 records of
  0x104 bytes in input order. The firmware image stores inputs in key-matrix
  order, with slot = `MACRO_MARK[i] - 0xA0` (`n52te_readback.decode`,
  `n52te_upload.main --set`). Code bytes are at `0x08 + slot`, option bytes at
  `0x23` / `0x3E` / `0x59` + slot (stored +1), and macros at `0x100 + slot * 0x100`.
- An input's code is a macro when it equals `MACRO_MARK[i]`, a PC-side action
  when it equals `PC_MARK[i]` (type in `seq[0]`, parameter in `seq[1]`, see
  `PC_TYPES`), and disabled when it is `0x00`. A macro step list ends at the
  first `(0, 0)` pair. Flags bit 0 means press, and in the firmware image
  `flags >> 1` is a delay in 50 ms units (`n52te_readback.decode`).
- 8-byte command blocks end in two check bytes: `b0^b2^b4`, `b1^b3^b5`
  (`n52te_keymap.block`, `checksum_ok`).
- Upload chunk checksum is the XOR of the even-offset bytes and the XOR of the
  odd-offset bytes (`n52te_upload.checksum`). A mismatch restarts the whole
  keymap, up to 3 attempts (`Keypad.write_keymap`).
- Uploads always rewrite the header to `1C 1B 00 01 KM 00 00 00`
  (`upload_header`), and `write_keymap` asserts the image is exactly `0x1C00`
  bytes and its first 5 bytes are `1C 1B 00 01 KM`. `compare` ignores the first 8 bytes, because the
  keypad reports a different header on read-back.
- `n52te_upload.py` accepts only keymaps 0, 1 or 2. `--image` and `--set`
  refuse to run without `--yes`. `--set` needs INPUT 1..27 and CODE 00..FF.
  The docstring says it never sends command 7.
- A failed interface-recipient request is retried as a device-recipient
  request, because Linux treats the wIndex low byte as an interface number.
  `n52te_readback.read_image` retries every failed chunk with `0xA0`, chunks 0
  and 1 included. `Keypad._xfer` (`n52te_upload.py`) retries with `0xA0` /
  `0x20` only when the wIndex low byte is not 0 or 1, and re-raises otherwise.
- `n52te_keymap.py` writes nothing unless a keymap is named. `n52te_readback.py`
  only sends `GET_REPORT` requests.
- `N52teMonitor.cs` assumes the x64 `RAWINPUTHEADER` size (`HeaderSize = 24`)
  and must be built with `/platform:x64`. It logs only devices whose name
  contains `VID_050D` unless `--all` is given, so without `--all` events with a
  null device handle (named `(injected)`) are dropped. Mouse events from other devices
  are never logged.

## Side Effects

- **Writes keypad memory:** `n52te_upload.py` in every mode, including
  `--roundtrip`, which uploads the keymap it just read. A failed upload can
  leave a keymap partly written. `n52te_keymap.py` changes the active keymap
  (and LED) when a keymap is named, and the keypad keeps that keymap after a
  replug.
- **USB driver binding:** the pyusb scripts detach the kernel driver from
  interfaces 0 and 1 and reattach it on exit.
- **Files written next to the scripts (in `research\`):** `keymap<N>.bin`
  (`n52te_readback.py`), `upload-before-km<N>.bin` and `upload-after-km<N>.bin`
  (`n52te_upload.py`).
- `extract_belkin_package.py` creates directories and files under the output
  directory it is given.
- `N52teMonitor.cs` registers Raw Input sinks (`RIDEV_INPUTSINK`) for usages
  keyboard, mouse, consumer control and system control. It appends to
  `n52te-monitor.log` in the current directory, or to the given LOGFILE, and
  shows a topmost window.
- `decode_n52te_profile.py` only reads its input file and prints to stdout.

## Verification

No test runner or manifest covers this folder. Nothing in `build.ps1` builds or
tests it. Do not run the USB scripts to verify changes, because some of them write
to the keypad. Safe checks:

- Run: `python -m py_compile research/decode_n52te_profile.py research/extract_belkin_package.py research/n52te_keymap.py research/n52te_readback.py research/n52te_upload.py` (syntax only)
- Run: `python research/decode_n52te_profile.py <profile.ini>` (offline; reads a Belkin profile file)
- Build the monitor: `csc /nologo /platform:x64 /target:exe /r:System.Windows.Forms.dll /out:n52te-monitor.exe research/N52teMonitor.cs`
