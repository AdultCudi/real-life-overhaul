"""Paints the strand atlas all hair and beard cards share: build/textures/hair_atlas.png (generated, CC0).

Greyscale strands with alpha; the game's hair shader multiplies them by the chosen hair colour.
Each strip runs root (top, v = 1) to tip (bottom, v = 0). Cards pick a strip by what they are for:

    DENSE   full locks, feathered to a point                the body of the hair
    MEDIUM  thinner locks                                   the visible top layer
    WISPY   separate strands                                edges, fringes, the hairline
    STRAY   a handful of single hairs                       flyaways
    FUZZ    dots and very short hairs, tileable             shells for cropped hair and stubble

Run with any Python that has Pillow and numpy:  python hair_atlas.py
"""
import math
import os
import random
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.join(os.path.dirname(HERE), "_tools", "pylib"))
from PIL import Image, ImageDraw, ImageFilter  # noqa: E402

SIZE = 1024
SUPER = 4            # painted at 4x and reduced, for anti-aliased hair-thin lines

# Each strip is a row of separate locks; a card shows exactly one lock, picked at random.
# name: (u0, u1, number of locks)
STRIPS = {
    "DENSE": (0.00, 0.30, 3),
    "MEDIUM": (0.30, 0.57, 3),
    "WISPY": (0.57, 0.78, 3),
    "STRAY": (0.78, 0.85, 1),
    "FUZZ": (0.85, 1.00, 1),
}
MARGIN = 0.008       # kept clear round each lock so neighbours never bleed in at low mip levels


def cells(name):
    u0, u1, count = STRIPS[name]
    width = (u1 - u0) / count
    return [(u0 + width * i + MARGIN, u0 + width * (i + 1) - MARGIN) for i in range(count)]


def strip_uv(name):
    """The whole strip (for the tileable fuzz)."""
    u0, u1, _ = STRIPS[name]
    return u0 + MARGIN, u1 - MARGIN, 0.0, 1.0


def _strand(draw_colour, draw_alpha, x0, x1, bend, v_root, v_tip, shade, width, wave, phase):
    """One hair as a polyline from root to tip in supersampled pixels."""
    big = SIZE * SUPER
    steps = 26
    points = []
    for i in range(steps + 1):
        t = i / steps
        v = v_root + (v_tip - v_root) * t
        x = x0 + (x1 - x0) * t ** 1.3 + bend * t * t + wave * math.sin(phase + t * 5.0) * t
        points.append((x * big, (1.0 - v) * big))
    for i in range(steps):
        t = i / steps
        # Thinner towards the tip; darker at the root, where hairs shade each other.
        w = max(1.0, width * SUPER * (1.0 - 0.5 * t ** 2))
        tone = shade * (0.66 + 0.30 * min(1.0, t * 2.2))
        level = int(max(0, min(255, tone * 255)))
        draw_colour.line([points[i], points[i + 1]], fill=level, width=int(round(w)))
        draw_alpha.line([points[i], points[i + 1]], fill=255, width=int(round(w)))


def _lock(draw_colour, draw_alpha, cell, rng, count, fray_from, tip_spread, wave, stray=False):
    """A lock of hair: spread across the cell at the root, drawn together into a feathered point at the tip."""
    u0, u1 = cell
    centre, half = (u0 + u1) / 2, (u1 - u0) / 2
    bend = rng.uniform(-0.25, 0.25) * half
    strands = []
    for _ in range(count):
        offset = rng.uniform(-1, 1)
        x0 = centre + half * 0.96 * offset
        x1 = centre + half * tip_spread * offset + rng.gauss(0, half * 0.05)
        # Hairs at the edge of a lock are the short ones, which is what feathers its outline.
        reach = 1.0 if rng.random() > 0.55 + 0.3 * abs(offset) else rng.uniform(fray_from, 1.0)
        # Roots start at slightly different heights, so the top edge of a card is feathered too.
        v_root = 1.0 - rng.uniform(0.0, 0.07) ** 1.0 if not stray else rng.uniform(0.8, 1.0)
        strands.append((rng.random(), x0, x1, v_root, max(0.0, 1.0 - reach * rng.uniform(0.94, 1.0))))
    strands.sort()
    for depth, x0, x1, v_root, v_tip in strands:
        shade = 0.34 + 0.52 * depth ** 1.4 + rng.uniform(-0.06, 0.08)
        if rng.random() < 0.06:
            shade = min(1.0, shade + 0.25)         # the odd hair catching the light
        _strand(draw_colour, draw_alpha, x0, x1, bend, v_root, v_tip, shade, rng.uniform(1.1, 1.9), wave * rng.uniform(0.3, 1.0) * half,
                rng.uniform(0, 6.28))


def _fuzz(draw_colour, draw_alpha, rng):
    """Short hairs seen nearly end-on, for shells. Tiles vertically."""
    u0, u1, _, _ = strip_uv("FUZZ")
    big = SIZE * SUPER
    for _ in range(5200):
        x = rng.uniform(u0, u1)
        y = rng.random()
        length = rng.uniform(0.004, 0.012)
        angle = rng.gauss(-math.pi / 2, 0.5)       # mostly along the strip, the way the hair lies
        dx, dy = math.cos(angle) * length, math.sin(angle) * length
        level = int(255 * rng.uniform(0.45, 1.0))
        width = int(round(rng.uniform(1.2, 2.0) * SUPER))
        for wrap in (-1, 0, 1):
            a = (x * big, (y + wrap) * big)
            b = (min(u1, max(u0, x + dx)) * big, (y + dy + wrap) * big)
            draw_colour.line([a, b], fill=level, width=width)
            draw_alpha.line([a, b], fill=255, width=width)


def build(path):
    rng = random.Random(7)
    big = SIZE * SUPER
    colour = Image.new("L", (big, big), 0)
    alpha = Image.new("L", (big, big), 0)
    dc, da = ImageDraw.Draw(colour), ImageDraw.Draw(alpha)
    for cell in cells("DENSE"):
        _lock(dc, da, cell, rng, count=520, fray_from=0.5, tip_spread=0.6, wave=0.10)
    for cell in cells("MEDIUM"):
        _lock(dc, da, cell, rng, count=210, fray_from=0.40, tip_spread=0.40, wave=0.16)
    for cell in cells("WISPY"):
        _lock(dc, da, cell, rng, count=42, fray_from=0.35, tip_spread=0.55, wave=0.30)
    for cell in cells("STRAY"):
        _lock(dc, da, cell, rng, count=7, fray_from=0.5, tip_spread=0.8, wave=0.5, stray=True)
    _fuzz(dc, da, rng)
    colour = colour.resize((SIZE, SIZE), Image.LANCZOS)
    alpha = alpha.resize((SIZE, SIZE), Image.LANCZOS)

    # Where there is no hair the colour must still be hair-coloured, or filtering drags dark fringes in:
    # spread the painted colour outwards into the empty texels.
    c = np.asarray(colour, dtype=np.float32) / 255.0
    a = np.asarray(alpha, dtype=np.float32) / 255.0
    premultiplied = Image.fromarray((c * a * 255).astype(np.uint8))
    filled_c, filled_a = c * a, a.copy()
    for radius in (2, 6, 16, 48):
        blur_c = np.asarray(premultiplied.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0
        blur_a = np.asarray(alpha.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0
        empty = filled_a < 0.02
        filled_c = np.where(empty, blur_c, filled_c)
        filled_a = np.where(empty, blur_a, filled_a)
    bled = np.where(filled_a > 0.004, filled_c / np.maximum(filled_a, 0.004), 0.6)
    bled = np.where(a > 0.5, c, np.clip(bled, 0.25, 1.0))
    rgb = (np.clip(bled, 0, 1) * 255).astype(np.uint8)
    out = np.dstack([rgb, rgb, rgb, (a * 255).astype(np.uint8)])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(out, "RGBA").save(path)
    return path


if __name__ == "__main__":
    target = os.path.join(os.path.dirname(HERE), "build", "textures", "hair_atlas.png")
    print(build(target))
