#!/usr/bin/env python3
"""Unpack n52te_win_driver_v2.1.2.exe (Belkin's July 2008 package) without running it.

The .exe is a Chilkat self-extractor. Each entry is:
    9B E5 27 6D | type(1) | name_len(2) | name | FILETIME(8) | attrs(4) | chunks
where chunks are [u32 size|0x80000000][data] terminated by a zero size. In most
entries every chunk is its own raw-deflate stream; the InstallShield cabinets
are stored as-is.

After this, extract the InstallShield payload with unshield (Linux):
    unshield -d payload x out/n52te_win_driver_v2.1.2/data1.cab

Mirror of the package: https://archive.org/details/n52te_win_driver_v212
(zip MD5 8ebe1b9ec204d535d750442bb05aa83c).
Usage: extract_belkin_package.py n52te_win_driver_v2.1.2.exe out/
"""
import os
import struct
import sys
import zlib

MAGIC = b"\x9b\xe5\x27\x6d"


def main(src, out):
    data = open(src, "rb").read()
    pos = data.find(MAGIC)
    while 0 <= pos < len(data) - 8:
        kind = data[pos + 4]
        nlen = struct.unpack_from("<H", data, pos + 5)[0]
        name = data[pos + 7:pos + 7 + nlen].decode("latin-1").rstrip("\0")
        p = pos + 7 + nlen + 12
        if kind == 2:  # file entry
            chunks = []
            while True:
                size = struct.unpack_from("<I", data, p)[0]
                p += 4
                if size == 0:
                    break
                size &= 0x7FFFFFFF
                chunks.append(data[p:p + size])
                p += size
            buf = b"".join(chunks)
            try:  # each chunk is an independent raw-deflate stream...
                parts = []
                for c in chunks:
                    d = zlib.decompressobj(-15)
                    parts.append(d.decompress(c) + d.flush())
                    if not d.eof:
                        raise zlib.error("truncated")
                buf = b"".join(parts)
            except zlib.error:
                pass  # ...or the entry is stored (the .cab files)
            dest = os.path.join(out, *name.split("/"))
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, "wb") as fh:
                fh.write(buf)
            print(f"{len(buf):>10}  {name}")
        pos = data.find(MAGIC, p)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
