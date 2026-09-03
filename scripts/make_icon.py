#!/usr/bin/env python3
"""Generate app.ico and PNG/SVG-adjacent bitmaps without third-party deps."""

from __future__ import annotations

import struct
import zlib
from pathlib import Path


def clamp(v: int) -> int:
    return 0 if v < 0 else 255 if v > 255 else v


def mix(a: tuple[int, int, int, int], b: tuple[int, int, int, int], t: float) -> tuple[int, int, int, int]:
    inv = 1.0 - t
    return (
        clamp(int(a[0] * inv + b[0] * t)),
        clamp(int(a[1] * inv + b[1] * t)),
        clamp(int(a[2] * inv + b[2] * t)),
        clamp(int(a[3] * inv + b[3] * t)),
    )


def rounded_rect_coverage(x: float, y: float, left: float, top: float, w: float, h: float, r: float) -> float:
    px = x + 0.5
    py = y + 0.5
    cx = min(max(px, left + r), left + w - r)
    cy = min(max(py, top + r), top + h - r)
    if left + r <= px <= left + w - r or top + r <= py <= top + h - r:
        inside_x = left <= px <= left + w
        inside_y = top <= py <= top + h
        return 1.0 if inside_x and inside_y else 0.0
    dx = px - cx
    dy = py - cy
    dist = (dx * dx + dy * dy) ** 0.5
    edge = r - dist
    if edge >= 1:
        return 1.0
    if edge <= 0:
        return 0.0
    return edge


def blit_round_rect(
    pixels: list[list[tuple[int, int, int, int]]],
    left: float,
    top: float,
    w: float,
    h: float,
    r: float,
    color: tuple[int, int, int, int],
) -> None:
    height = len(pixels)
    width = len(pixels[0])
    y0 = max(0, int(top) - 1)
    y1 = min(height, int(top + h) + 2)
    x0 = max(0, int(left) - 1)
    x1 = min(width, int(left + w) + 2)
    for y in range(y0, y1):
        for x in range(x0, x1):
            cov = rounded_rect_coverage(x, y, left, top, w, h, r)
            if cov <= 0:
                continue
            src = (color[0], color[1], color[2], int(color[3] * cov))
            dst = pixels[y][x]
            if src[3] >= 250:
                pixels[y][x] = src
                continue
            a = src[3] / 255.0
            pixels[y][x] = mix(dst, src, a)


def draw_icon(size: int) -> list[list[tuple[int, int, int, int]]]:
    pixels = [[(0, 0, 0, 0) for _ in range(size)] for _ in range(size)]
    s = float(size)
    pad = s * 0.06
    body = s - pad * 2
    radius = body * 0.22
    navy = (27, 54, 93, 255)
    ink = (18, 32, 56, 255)
    key = (245, 246, 248, 255)
    amber = (217, 119, 6, 255)

    blit_round_rect(pixels, pad + s * 0.02, pad + s * 0.04, body, body, radius, ink)
    blit_round_rect(pixels, pad, pad, body, body, radius, navy)

    key_l = pad + body * 0.18
    key_t = pad + body * 0.30
    key_w = body * 0.64
    key_h = body * 0.38
    blit_round_rect(pixels, key_l, key_t, key_w, key_h, key_h * 0.22, key)

    # Small mapped-app mark on the key
    mark = key_h * 0.28
    blit_round_rect(
        pixels,
        key_l + key_w - mark * 1.7,
        key_t + key_h * 0.36,
        mark,
        mark,
        mark * 0.28,
        amber,
    )
    return pixels


def flatten(pixels: list[list[tuple[int, int, int, int]]]) -> bytes:
    out = bytearray()
    for row in pixels:
        for r, g, b, a in row:
            out.extend((r, g, b, a))
    return bytes(out)


def write_png(path: Path, pixels: list[list[tuple[int, int, int, int]]]) -> None:
    h = len(pixels)
    w = len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw.extend((r, g, b, a))

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    path.write_bytes(png)


def png_bytes(pixels: list[list[tuple[int, int, int, int]]]) -> bytes:
    h = len(pixels)
    w = len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw.extend((r, g, b, a))

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    return png


def write_ico(path: Path, sizes: list[int]) -> None:
    images = [png_bytes(draw_icon(size)) for size in sizes]
    count = len(sizes)
    offset = 6 + 16 * count
    out = bytearray()
    out += struct.pack("<HHH", 0, 1, count)
    entries = bytearray()
    for size, data in zip(sizes, images):
        w = 0 if size >= 256 else size
        h = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", w, h, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    out += entries
    for data in images:
        out += data
    path.write_bytes(out)


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    assets = root / "src" / "BetterCopilotButton" / "Assets"
    docs = root / "docs" / "images"
    assets.mkdir(parents=True, exist_ok=True)
    docs.mkdir(parents=True, exist_ok=True)
    write_ico(assets / "app.ico", [16, 32, 48, 256])
    write_png(assets / "app.png", draw_icon(256))
    write_png(docs / "icon.png", draw_icon(256))
    print("Wrote icons")


if __name__ == "__main__":
    main()
