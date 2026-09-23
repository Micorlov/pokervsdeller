#!/usr/bin/env python3
"""Convert a PNG between 24-bit RGB and 32-bit RGBA, losslessly.

Play wants the feature graphic and screenshots as "JPEG or 24-bit PNG (no
alpha)" but the store icon as "32-bit PNG (with alpha)". Chrome writes whichever
it feels like — it drops the alpha channel whenever the page renders fully
opaque — and re-encoding as JPEG would put ringing around the UI text, so the
channel is added or removed here instead. Pure stdlib: no Pillow in this
toolchain.

    png-alpha.py --strip a.png b.png     # -> 24-bit RGB
    png-alpha.py --add   icon.png        # -> 32-bit RGBA, alpha 255
"""
import struct
import sys
import zlib

PAETH_PREDICTORS = 4


def _chunks(data: bytes):
    pos = 8
    while pos < len(data):
        (length,) = struct.unpack(">I", data[pos : pos + 4])
        kind = data[pos + 4 : pos + 8]
        yield kind, data[pos + 8 : pos + 8 + length]
        pos += 12 + length


def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def _unfilter(raw: bytes, width: int, height: int, bpp: int) -> list[bytearray]:
    stride = width * bpp
    rows, prev, pos = [], bytearray(stride), 0
    for _ in range(height):
        ftype = raw[pos]
        line = bytearray(raw[pos + 1 : pos + 1 + stride])
        pos += 1 + stride
        for i in range(stride):
            left = line[i - bpp] if i >= bpp else 0
            up = prev[i]
            upleft = prev[i - bpp] if i >= bpp else 0
            if ftype == 1:
                line[i] = (line[i] + left) & 0xFF
            elif ftype == 2:
                line[i] = (line[i] + up) & 0xFF
            elif ftype == 3:
                line[i] = (line[i] + (left + up) // 2) & 0xFF
            elif ftype == PAETH_PREDICTORS:
                line[i] = (line[i] + _paeth(left, up, upleft)) & 0xFF
        rows.append(line)
        prev = line
    return rows


def _chunk(kind: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + kind
        + payload
        + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
    )


def convert(src: str, dst: str, *, want_alpha: bool) -> str:
    data = open(src, "rb").read()
    header, idat = None, b""
    for kind, payload in _chunks(data):
        if kind == b"IHDR":
            header = payload
        elif kind == b"IDAT":
            idat += payload
    if header is None:
        raise ValueError(f"{src}: no IHDR")
    width, height, depth, colour = struct.unpack(">IIBB", header[:10])
    if depth != 8 or colour not in (2, 6):
        raise ValueError(f"{src}: expected 8-bit RGB or RGBA, got colour={colour} depth={depth}")
    want = 6 if want_alpha else 2
    if colour == want:
        return f"already {'32-bit RGBA' if want_alpha else '24-bit RGB'}"

    src_bpp = 4 if colour == 6 else 3
    rows = _unfilter(zlib.decompress(idat), width, height, src_bpp)
    out = bytearray()
    for line in rows:
        out.append(0)  # filter: None
        for x in range(width):
            pixel = line[x * src_bpp : x * src_bpp + 3]
            out += pixel + b"\xff" if want_alpha else pixel

    new_header = struct.pack(">IIBBBBB", width, height, 8, want, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + _chunk(b"IHDR", new_header)
    png += _chunk(b"IDAT", zlib.compress(bytes(out), 9)) + _chunk(b"IEND", b"")
    open(dst, "wb").write(png)
    return "32-bit RGBA" if want_alpha else "24-bit RGB"


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args or args[0] not in ("--strip", "--add"):
        sys.exit(__doc__)
    want_alpha = args[0] == "--add"
    for path in args[1:]:
        print(f"  {convert(path, path, want_alpha=want_alpha)}: {path}")
