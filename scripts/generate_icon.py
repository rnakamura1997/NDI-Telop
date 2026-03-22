#!/usr/bin/env python3
from __future__ import annotations

import os
import struct
import sys
import zlib
from pathlib import Path

SIZES = (16, 32, 48, 64, 128, 256)
DEFAULT_OUTPUT = Path("src/NdiTelop/Assets/icon.ico")


def png_chunk(tag: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + tag
        + data
        + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    )


def render_png(size: int) -> bytes:
    width = height = size
    bg = (0x00, 0xC1, 0xB2, 255)
    fg = (255, 255, 255, 255)
    cut = (0x00, 0x8C, 0x84, 255)
    rows: list[bytes] = []

    for y in range(height):
        row = bytearray([0])
        for x in range(width):
            pixel = bg

            cx = (x + 0.5 - width / 2) / (width / 2)
            cy = (y + 0.5 - height / 2) / (height / 2)
            if cx * cx + cy * cy > 0.98:
                pixel = (0, 0, 0, 0)

            left = 0.24 * width + (y * 0.26)
            right = 0.76 * width - (y * 0.26)
            top = 0.14 * height

            if y > top and left <= x <= left + 0.12 * width:
                pixel = fg
            if y > top and right - 0.12 * width <= x <= right:
                pixel = fg
            if 0.49 * height <= y <= 0.61 * height and 0.33 * width <= x <= 0.67 * width:
                pixel = fg
            if 0.57 * height <= y <= 0.72 * height and 0.43 * width <= x <= 0.57 * width:
                pixel = cut

            row.extend(pixel)
        rows.append(bytes(row))

    compressed = zlib.compress(b"".join(rows), 9)
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", compressed)
        + png_chunk(b"IEND", b"")
    )


def build_ico() -> bytes:
    images = [render_png(size) for size in SIZES]
    header = struct.pack("<HHH", 0, 1, len(images))
    directory = bytearray()
    offset = 6 + 16 * len(images)

    for size, image in zip(SIZES, images, strict=True):
        encoded_size = size if size < 256 else 0
        directory.extend(
            struct.pack(
                "<BBBBHHII",
                encoded_size,
                encoded_size,
                0,
                0,
                1,
                32,
                len(image),
                offset,
            )
        )
        offset += len(image)

    return header + bytes(directory) + b"".join(images)


def main() -> int:
    output = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_OUTPUT
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(build_ico())
    print(f"generated {output} ({os.path.getsize(output)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
