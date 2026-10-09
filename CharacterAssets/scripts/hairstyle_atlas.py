"""Paints the strand atlas the new hairstyles share: build_hair/textures/hairstyle_atlas.png (generated, nothing copied).

    python -I hairstyle_atlas.py [out folder]

The game's convention, measured on its own strand textures (which are only looked at): white-ish strands whose
colour comes from the material, coverage in alpha, strips running down the texture with the root at the top
(v = 1) and the tip at the bottom (v = 0), from nearly solid locks to single hairs.

    SOLID   opaque from edge to edge, only the tip is ragged      the sculpted base under everything
    DENSE   a full lock, solid core, feathered to points           the body of the hair
    MEDIUM  a thinner lock, gaps between groups of strands         the layer that is seen
    WISPY   separate strands                                       hairline, edges, over the top
    FLY     a handful of single hairs                              flyaways
Grey level: every strand its own tone and darker towards the root, where real hair lies in the shade of the hair
above it; the gaps of SOLID are the dark between strands.
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.append(os.path.join(ROOT, "_tools", "pylib"))
from PIL import Image, ImageDraw, ImageFilter  # noqa: E402

SIZE = 1024
SUPER = 3
# name: (first pixel, cell width in pixels, number of cells)
STRIPS = {
    "SOLID": (0, 96, 2),
    "DENSE": (192, 96, 3),
    "MEDIUM": (480, 96, 3),
    "WISPY": (768, 80, 2),
    "FLY": (928, 96, 1),
}
MARGIN = 5      # pixels kept clear round a lock, so neighbours do not bleed in at small mip levels


def cells(name):
    """(u0, u1) of every lock of a strip."""
    first, width, count = STRIPS[name]
    inset = 2 if name == "SOLID" else MARGIN
    return [((first + width * i + inset) / SIZE, (first + width * (i + 1) - inset) / SIZE) for i in range(count)]


def paint(path):
    rng = np.random.default_rng(11)
    big = SIZE * SUPER
    colour = Image.new("L", (big, big), 205)
    alpha = Image.new("L", (big, big), 0)
    dc, da = ImageDraw.Draw(colour), ImageDraw.Draw(alpha)

    def strand(x_root, x_tip, t_end, tone, width, wave, phase, t_start=0.0, root_dark=0.85):
        steps = 28
        points = []
        for i in range(steps + 1):
            t = t_start + (t_end - t_start) * i / steps
            x = x_root + (x_tip - x_root) * t ** 1.25 + wave * math.sin(phase + t * 7.0) * (0.3 + 0.7 * t)
            points.append((x * SUPER, t * big))
        for i in range(steps):
            t = t_start + (t_end - t_start) * i / steps
            w = max(1, int(round(width * SUPER * (1.0 - 0.45 * (i / steps) ** 2))))
            shade = tone * (root_dark + (1 - root_dark) * min(1.0, t / 0.3))
            dc.line([points[i], points[i + 1]], fill=int(np.clip(shade, 0, 1) * 255), width=w)
            da.line([points[i], points[i + 1]], fill=255, width=w)

    for name, (first, width, count) in STRIPS.items():
        for k in range(count):
            x0, x1 = first + width * k, first + width * (k + 1)
            if name == "SOLID":
                # The dark between strands, opaque down to where the tips start to part; locks of a centimetre or so,
                # each its own tone with a dark seam between, so the base already reads as locks from a distance.
                dc.rectangle([x0 * SUPER, 0, x1 * SUPER, big], fill=int(0.62 * 255))
                da.rectangle([x0 * SUPER, 0, x1 * SUPER, int(0.70 * big)], fill=255)
                edges = [x0]
                while edges[-1] < x1 - 8:
                    edges.append(edges[-1] + rng.uniform(8, 20))
                edges[-1] = x1
                for a, b in zip(edges[:-1], edges[1:]):
                    lock_tone = rng.uniform(0.80, 1.0)
                    lean = rng.normal(0, 3.0)
                    for _ in range(int((b - a) * 2.6)):
                        f = rng.uniform(0, 1)
                        x = a + (b - a) * f
                        round_ = 0.78 + 0.22 * math.sin(math.pi * f)          # darker towards the seams
                        strand(x, x + lean + rng.normal(0, 1.5), rng.uniform(0.86, 1.0) if rng.random() < 0.75 else rng.uniform(0.6, 0.86),
                               lock_tone * round_ * rng.uniform(0.92, 1.06), rng.uniform(1.2, 2.2), rng.uniform(0, 1.2), rng.uniform(0, 6.3), root_dark=0.9)
                continue
            lo, hi = x0 + MARGIN + 2, x1 - MARGIN - 2
            centre, half = (lo + hi) / 2, (hi - lo) / 2
            number = {"DENSE": 150, "MEDIUM": 60, "WISPY": 20, "FLY": 7}[name]
            # A lock gathers into a few points; thin strips stay spread.
            tips = [centre + half * rng.uniform(-0.55, 0.55) for _ in range({"DENSE": 2, "MEDIUM": 3, "WISPY": 4, "FLY": 5}[name])]
            order = []
            # Strands come in small bunches that share a tone and a path: locks within the lock.
            bunches = [(centre + half * rng.uniform(-0.9, 0.9), rng.uniform(0.84, 1.04), tips[int(rng.integers(len(tips)))]) for _ in range({"DENSE": 9, "MEDIUM": 6, "WISPY": 5, "FLY": 7}[name])]
            for _ in range(number):
                bx, btone, tip = bunches[int(rng.integers(len(bunches)))]
                x = float(np.clip(bx + rng.normal(0, half * (0.16 if name != "FLY" else 0.02)), lo, hi))
                pull = {"DENSE": 0.72, "MEDIUM": 0.5, "WISPY": 0.25, "FLY": 0.1}[name]
                x_tip = np.clip(x + (tip - x) * pull * rng.uniform(0.7, 1.1) + rng.normal(0, 1.5), lo, hi)
                end = rng.uniform(0.80, 0.995) if rng.random() < 0.7 else rng.uniform(0.5, 0.8)
                edge = 0.84 + 0.16 * math.cos(0.5 * math.pi * abs(x - centre) / half) if name in ("DENSE", "MEDIUM") else 1.0
                order.append((btone * rng.uniform(0.94, 1.0) * edge * (1.0, 0.95, 0.9)[k % 3], x, x_tip, end))
            order.sort()      # dark strands first, light ones over them
            for tone, x, x_tip, end in order:
                strand(x, x_tip, end, tone, {"DENSE": 1.7, "MEDIUM": 1.5, "WISPY": 1.3, "FLY": 1.2}[name] * rng.uniform(0.8, 1.2),
                       rng.uniform(0, {"DENSE": 1.0, "MEDIUM": 1.6, "WISPY": 2.4, "FLY": 3.0}[name]), rng.uniform(0, 6.3),
                       t_start=0.004 if name != "FLY" else rng.uniform(0.0, 0.2))
            if name == "DENSE":
                # A solid core down to two thirds: a lock that hides what is behind it.
                core = Image.new("L", (big, big), 0)
                d = ImageDraw.Draw(core)
                d.polygon([((centre - half * 0.80) * SUPER, 0), ((centre + half * 0.80) * SUPER, 0),
                           ((tips[0] + half * 0.25) * SUPER, 0.66 * big), ((tips[0] - half * 0.25) * SUPER, 0.66 * big)], fill=255)
                alpha.paste(255, mask=core)

    colour = colour.resize((SIZE, SIZE), Image.LANCZOS)
    alpha = alpha.resize((SIZE, SIZE), Image.LANCZOS)
    grey = np.asarray(colour, dtype=np.float64)
    a = np.asarray(alpha, dtype=np.float64) / 255.0
    # Under clear texels: the colour of the strands around (no dark or pale fringes when filtered).
    blurred = np.asarray(Image.fromarray((grey * a).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4)), dtype=np.float64)
    weight = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4)), dtype=np.float64) / 255.0
    fill = np.where(weight > 0.02, blurred / np.maximum(weight, 0.02), 205.0)
    grey = np.where(a > 0.6, grey, fill * (1 - a) + grey * a)
    g = np.clip(grey, 0, 255).astype(np.uint8)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.dstack([g, g, g, (a * 255).astype(np.uint8)]), "RGBA").save(path)
    for name in STRIPS:
        first, width, count = STRIPS[name]
        part = a[:, first:first + width * count]
        print("%-7s coverage %.2f, solid %.2f, tone %.2f" % (name, part.mean(), (part > 0.5).mean(), (grey[:, first:first + width * count] * part).sum() / max(part.sum(), 1) / 255))


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "build_hair")
    paint(os.path.join(out, "textures", "hairstyle_atlas.png"))
