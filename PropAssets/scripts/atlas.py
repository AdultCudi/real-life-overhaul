"""Paints the texture atlas the props of one bundle share, and says where each region is.

    build/textures/props_base.png   4096 x 2048 (the first bundle), colour
    build/textures/props_mask.png   2048 x 1024, HDRP mask map: R metallic, G occlusion, B detail (unused), A smoothness
    build/atlas.json                region name -> [u0, v0, u1, v1]

The set being built (sets.json, PROPS_SET) gives the folder and the size. The first bundle's atlas has a fixed size; a
set whose atlas "grows" takes the smallest power-of-two size its regions fit, up to its limit, so it costs video
memory only for what it holds, and none while it holds nothing. Such a set paints the swatches its models name and
each model's own picture (models.py --scan lists them), and its procedural modules if it has any.

A region is either a SWATCH (one surface finish: a colour that gets grimier towards the bottom of the patch, so a
part mapped onto it by height picks up dirt and contact shadow where it meets the ground) or a DECAL (a painted
face: the lettering of the barrier, the tape, a badge). Everything is drawn here, with Pillow's built-in font
(Aileron, CC0); nothing is read from disk. Run with Blender's Python: python atlas.py
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.append(os.path.join(ROOT, "_tools", "pylib"))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
from PIL import Image, ImageDraw, ImageFilter, ImageFont  # noqa: E402

import sets  # noqa: E402

BUILD = sets.BUILD          # PROPS_BUILD: a working folder of one's own (README, "Working on one module")
# Powers of two, both: Unity leaves any other size uncompressed (RGBA32, four times the memory). A set that grows
# starts at the smallest and doubles to this limit.
WIDTH, HEIGHT = sets.SET["atlas"]["width"], sets.SET["atlas"]["height"]
MASK_DIVISOR = sets.SET["mask_divisor"]       # the mask map is this much smaller each way than the colour
SMALLEST = 256
PAD = 8                   # gutter round every region, filled with the region's own edge
SS = 2                    # regions are painted at twice their size and reduced: smooth edges without a rasteriser
GRIME = (0.23, 0.20, 0.17)

_rng = np.random.default_rng(7)


# ---------------------------------------------------------------------------------------------- painting

class Layer:
    """One region while it is being painted. Coordinates are in final pixels, y down."""

    def __init__(self, width, height, colour=(0.5, 0.5, 0.5), metal=0.0, smooth=0.3):
        self.width, self.height = width, height
        size = (width * SS, height * SS)
        self.base = Image.new("RGB", size, _rgb(colour))
        self.metal = Image.new("L", size, _byte(metal))
        self.smooth = Image.new("L", size, _byte(smooth))
        self.ao = Image.new("L", size, 255)
        self._draw = [ImageDraw.Draw(self.base), ImageDraw.Draw(self.metal), ImageDraw.Draw(self.smooth)]

    def _all(self, method, shape, colour, metal, smooth, **extra):
        values = (_rgb(colour), None if metal is None else _byte(metal), None if smooth is None else _byte(smooth))
        for draw, value in zip(self._draw, values):
            if value is not None:
                getattr(draw, method)(shape, fill=value, **extra)

    def rect(self, x0, y0, x1, y1, colour, metal=None, smooth=None, radius=0):
        shape = [x0 * SS, y0 * SS, x1 * SS - 1, y1 * SS - 1]
        if radius:
            self._all("rounded_rectangle", shape, colour, metal, smooth, radius=radius * SS)
        else:
            self._all("rectangle", shape, colour, metal, smooth)

    def poly(self, points, colour, metal=None, smooth=None):
        self._all("polygon", [(x * SS, y * SS) for x, y in points], colour, metal, smooth)

    def disc(self, x, y, radius, colour, metal=None, smooth=None):
        self._all("ellipse", [(x - radius) * SS, (y - radius) * SS, (x + radius) * SS, (y + radius) * SS], colour, metal, smooth)

    def line(self, points, colour, width=1.0, metal=None, smooth=None):
        self._all("line", [(x * SS, y * SS) for x, y in points], colour, metal, smooth, width=max(1, round(width * SS)))

    def text(self, text, box, colour, weight=0.06, metal=None, smooth=None, spacing=0.0):
        """Bold text as large as fits the box (x0, y0, x1, y1), centred in it."""
        x0, y0, x1, y1 = [v * SS for v in box]
        size = int(y1 - y0)
        while size > 4:
            font = ImageFont.load_default(size)
            stroke = max(1, round(size * weight))
            left, top, right, bottom = _text_box(font, text, stroke, spacing * size)
            if right - left <= x1 - x0 and bottom - top <= y1 - y0:
                break
            size -= 2
        ox = (x0 + x1) / 2 - (left + right) / 2
        oy = (y0 + y1) / 2 - (top + bottom) / 2
        values = (_rgb(colour), None if metal is None else _byte(metal), None if smooth is None else _byte(smooth))
        for draw, value in zip(self._draw, values):
            if value is None:
                continue
            x = ox
            for letter in text:      # letter by letter, for the spacing
                draw.text((x, oy), letter, font=font, fill=value, stroke_width=stroke, stroke_fill=value)
                x += font.getlength(letter) + spacing * size

    def finish(self, grime=0.0, grain=0.03, blotch=0.04, scratches=0, edge=0.0):
        """Surface life: fine grain, larger blotches, dirt and contact shadow rising from the bottom, worn edges."""
        w, h = self.base.size
        base = np.asarray(self.base, dtype=np.float64) / 255.0
        smooth = np.asarray(self.smooth, dtype=np.float64) / 255.0
        fine, coarse = _noise(w, h, 3 * SS), _noise(w, h, 28 * SS)
        base = base * (1.0 + grain * fine + blotch * coarse)[..., None]
        smooth = smooth * (1.0 + 0.07 * coarse + 0.04 * fine)
        occlusion = np.ones((h, w))
        if grime > 0:
            rise = (np.linspace(0.0, 1.0, h) ** 3.0)[:, None] * (1.0 + 0.35 * _noise(w, h, 14 * SS))
            rise = np.clip(rise * grime, 0.0, 1.0)
            base = base * (1.0 - 0.45 * rise)[..., None] + np.array(GRIME) * (0.22 * rise)[..., None]
            smooth = smooth * (1.0 - 0.5 * rise)
            occlusion = 1.0 - 0.45 * rise
        if edge > 0:
            yy, xx = np.mgrid[0:h, 0:w]
            border = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)) / (10.0 * SS)
            base = base * (1.0 - edge * np.clip(1.0 - border, 0.0, 1.0) ** 2)[..., None]
        self.base = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8), "RGB")
        self.smooth = Image.fromarray((np.clip(smooth, 0, 1) * 255).astype(np.uint8), "L")
        self.ao = Image.fromarray((np.clip(occlusion, 0, 1) * 255).astype(np.uint8), "L")
        if scratches:
            draw = ImageDraw.Draw(self.base)
            for _ in range(scratches):
                x, y = _rng.uniform(0, w), _rng.uniform(0, h)
                angle, length = _rng.uniform(-0.5, 0.5), _rng.uniform(6, 40) * SS
                pixel = self.base.getpixel((int(min(x, w - 1)), int(min(y, h - 1))))
                shade = tuple(min(255, int(c * 0.6 + 80)) for c in pixel)
                draw.line([(x, y), (x + math.cos(angle) * length, y + math.sin(angle) * length)], fill=shade, width=1)
        return self

    def images(self):
        size = (self.width, self.height)
        return [image.resize(size, Image.LANCZOS) for image in (self.base, self.metal, self.ao, self.smooth)]


def _rgb(colour):
    return tuple(int(round(min(1.0, max(0.0, c)) * 255)) for c in colour)


def _byte(value):
    return int(round(min(1.0, max(0.0, value)) * 255))


def _text_box(font, text, stroke, spacing):
    left, top, right, bottom = font.getbbox(text, stroke_width=stroke)
    return left, top, right + spacing * (len(text) - 1), bottom


def _noise(width, height, cell):
    """Smooth noise in -1..1 with features about `cell` pixels across."""
    small = _rng.uniform(0.0, 1.0, (max(2, height // cell + 2), max(2, width // cell + 2)))
    image = Image.fromarray((small * 255).astype(np.uint8), "L").resize((width, height), Image.BICUBIC)
    return np.asarray(image, dtype=np.float64) / 127.5 - 1.0


# ---------------------------------------------------------------------------------------------- swatches

# name: (colour, metallic, smoothness, grime towards the bottom)
SWATCHES = {
    "orange_plastic": ((0.92, 0.30, 0.05), 0.0, 0.42, 0.8),
    "reflective_white": ((0.93, 0.93, 0.90), 0.0, 0.55, 0.5),
    "rubber_black": ((0.045, 0.045, 0.05), 0.0, 0.22, 0.6),
    "paint_white": ((0.86, 0.87, 0.86), 0.0, 0.35, 0.9),
    "paint_blue": ((0.06, 0.18, 0.50), 0.0, 0.38, 0.7),
    "paint_yellow": ((0.95, 0.72, 0.05), 0.0, 0.45, 0.5),
    "paint_red": ((0.70, 0.07, 0.05), 0.0, 0.45, 0.5),
    "aluminium": ((0.72, 0.74, 0.76), 1.0, 0.58, 0.5),
    "steel_dark": ((0.22, 0.23, 0.25), 1.0, 0.48, 0.5),
    "chrome": ((0.86, 0.87, 0.88), 1.0, 0.86, 0.15),
    "gold": ((0.86, 0.64, 0.24), 1.0, 0.74, 0.2),
    "vinyl_navy": ((0.06, 0.08, 0.14), 0.0, 0.5, 0.4),
    "linen": ((0.90, 0.90, 0.88), 0.0, 0.12, 0.35),
    "wool_blue": ((0.28, 0.36, 0.48), 0.0, 0.08, 0.35),
    "nylon_red": ((0.72, 0.07, 0.06), 0.0, 0.2, 0.7),
    "nylon_black": ((0.05, 0.05, 0.06), 0.0, 0.2, 0.5),
    "canvas_olive": ((0.17, 0.18, 0.13), 0.0, 0.12, 0.7),
    "leather_black": ((0.035, 0.035, 0.04), 0.0, 0.55, 0.3),
    "gloss_black": ((0.02, 0.02, 0.025), 0.0, 0.85, 0.1),
    "cloth_navy": ((0.045, 0.06, 0.13), 0.0, 0.15, 0.25),
    "plastic_grey": ((0.10, 0.10, 0.11), 0.0, 0.35, 0.5),
    "plastic_green": ((0.05, 0.42, 0.19), 0.0, 0.45, 0.6),
    "glass": ((0.72, 0.88, 0.86), 0.0, 0.92, 0.0),
    "lens_red": ((0.72, 0.03, 0.03), 0.0, 0.9, 0.0),
    "lens_amber": ((0.95, 0.50, 0.03), 0.0, 0.9, 0.0),
    "car_silver": ((0.70, 0.71, 0.72), 0.6, 0.75, 0.0),
    "flame": ((1.0, 0.86, 0.72), 0.0, 0.3, 0.0),
    "tent_yellow": ((0.97, 0.80, 0.05), 0.0, 0.4, 0.3),
    # City life (city_props.py).
    "wood": ((0.60, 0.44, 0.26), 0.0, 0.25, 0.5),
    "bristle": ((0.36, 0.25, 0.12), 0.0, 0.08, 0.8),
    "paper": ((0.93, 0.92, 0.88), 0.0, 0.18, 0.1),
    "cardboard": ((0.62, 0.47, 0.30), 0.0, 0.12, 0.4),
}

def _srgb(linear):
    return tuple(round(12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055, 3) for c in linear)


# The plain finishes of the Birchwood School's models (models/birchwood_*), named swatch_<name> in them: only a later set that
# a model names them in paints them, never the first (closed) set's atlas. Colours as the model has them (linear in Blender,
# sRGB here); brick, roof and glass are the game's own materials (models.py, "game"), not painted.
MODEL_SWATCHES = {
    "stone": (_srgb((0.80, 0.76, 0.66)), 0.0, 0.2, 0.7),        # the limestone trim, bands and plinth
    "frame": (_srgb((0.93, 0.92, 0.88)), 0.0, 0.2, 0.3),        # window frames and mullions
    "door": (_srgb((0.20, 0.30, 0.22)), 0.0, 0.2, 0.3),
    "copper": (_srgb((0.30, 0.55, 0.45)), 0.0, 0.2, 0.2),       # the cupola's patina
    "letters": (_srgb((0.08, 0.10, 0.12)), 0.0, 0.5, 0.0),
    "flag_red": (_srgb((0.55, 0.05, 0.06)), 0.0, 0.5, 0.0),
    "flag_blue": (_srgb((0.05, 0.08, 0.25)), 0.0, 0.5, 0.0),
    "accent": (_srgb((0.05, 0.45, 0.50)), 0.0, 0.5, 0.3),        # the junior block's teal canopy
    "ground": (_srgb((0.35, 0.35, 0.34)), 0.0, 0.2, 0.5),        # the forecourt's slab
    "metal": (_srgb((0.12, 0.12, 0.13)), 0.0, 0.5, 0.2),         # rails, the fire escape, the tank's legs
    "wood": (_srgb((0.32, 0.20, 0.12)), 0.0, 0.5, 0.4),
    "lamp": (_srgb((0.95, 0.85, 0.55)), 0.0, 0.5, 0.0),
    "leaf": (_srgb((0.14, 0.32, 0.12)), 0.0, 0.5, 0.0),
    "accent2": (_srgb((0.85, 0.62, 0.12)), 0.0, 0.5, 0.2),       # the junior block's yellow window panels
    "sash": (_srgb((0.025, 0.04, 0.032)), 0.0, 0.35, 0.3),       # the window frames and sashes: the dark green black of the city's old windows
}
SWATCH_SIZE = (40, 80)     # a swatch is one finish and its grime, read 7 px in from its edges: smaller swatches leave the atlas room for more modules


def paint_swatch(name):
    colour, metal, smooth, grime = SWATCHES[name]
    return Layer(*SWATCH_SIZE, colour, metal, smooth).finish(grime=grime, grain=0.035, blotch=0.05)


# ---------------------------------------------------------------------------------------------- decals

TAPE_YELLOW = (0.98, 0.78, 0.03)
BOARD_BLUE = (0.05, 0.17, 0.52)
WHITE = (0.94, 0.94, 0.92)
BLACK = (0.03, 0.03, 0.035)


def paint_tape():
    # One repeat of the tape per metre; the row spans the atlas, so the strip tiles along U.
    # Painted 2048 wide as it always was and drawn out to the atlas's width when packed: the strip maps one repeat a metre.
    layer = Layer(2048, 152, TAPE_YELLOW, 0.0, 0.5)
    layer.text("POLICE LINE DO NOT CROSS", (150, 30, 2048 - 150, 122), BLACK, weight=0.07, smooth=0.4, spacing=0.03)
    return layer.finish(grain=0.02, blotch=0.03)


def paint_barrier_board():
    layer = Layer(1664, 140, BOARD_BLUE, 0.0, 0.38)
    layer.text("POLICE LINE  DO NOT CROSS", (90, 30, 1574, 110), WHITE, weight=0.065, smooth=0.3, spacing=0.05)
    return layer.finish(grime=0.25, grain=0.05, blotch=0.07, scratches=60, edge=0.25)


def paint_panel(text, ground, width=448, height=160):
    def paint():
        layer = Layer(width, height, ground, 0.0, 0.15)
        margin = height * 0.16
        layer.text(text, (margin * 1.4, margin, width - margin * 1.4, height - margin), WHITE, weight=0.06, smooth=0.5, spacing=0.04)
        return layer.finish(grain=0.05, blotch=0.03, edge=0.3)
    return paint


def star_of_life(layer, x, y, radius, blue=(0.04, 0.22, 0.62)):
    layer.disc(x, y, radius * 1.18, WHITE, smooth=0.45)
    half = radius * 0.27
    for k in range(3):
        a = math.radians(60 * k)
        ax, ay = math.sin(a), -math.cos(a)
        px, py = -ay, ax
        layer.poly([(x + ax * radius * s + px * half * t, y + ay * radius * s + py * half * t)
                    for s, t in ((1, 1), (1, -1), (-1, -1), (-1, 1))], blue, smooth=0.45)
    layer.line([(x, y - radius * 0.72), (x, y + radius * 0.72)], WHITE, width=radius * 0.09)
    snake = [(x + math.sin(t * 5.2) * radius * 0.2 * (1 - t * 0.5), y - radius * 0.55 + t * radius * 1.1) for t in np.linspace(0, 1, 24)]
    layer.line(snake, WHITE, width=radius * 0.07)


def paint_bag_front():
    red = SWATCHES["nylon_red"][0]
    seam = tuple(c * 0.55 for c in red)
    layer = Layer(384, 240, red, 0.0, 0.2)
    # Zip under the lid, pocket outline, and the reflective band with its yellow edges.
    layer.line([(8, 30), (376, 30)], BLACK, width=3)
    for x in range(10, 376, 6):
        layer.line([(x, 27.5), (x, 32.5)], (0.45, 0.45, 0.47), width=1.2, metal=1.0, smooth=0.6)
    layer.rect(40, 150, 344, 226, seam, radius=8)
    layer.rect(43, 153, 341, 223, tuple(c * 0.9 for c in red), radius=6)
    layer.rect(0, 108, 384, 140, (0.95, 0.80, 0.08), smooth=0.5)
    layer.rect(0, 114, 384, 134, (0.86, 0.87, 0.86), smooth=0.6)
    star_of_life(layer, 192, 70, 27)
    return layer.finish(grime=0.3, grain=0.06, blotch=0.04, edge=0.25)


def paint_duffel_top():
    canvas = SWATCHES["canvas_olive"][0]
    layer = Layer(448, 160, canvas, 0.0, 0.12)
    for x in (120, 328):          # the two handle straps crossing the top
        layer.rect(x - 16, 0, x + 16, 160, BLACK, smooth=0.25)
        layer.line([(x - 12, 0), (x - 12, 160)], (0.12, 0.12, 0.12), width=1)
        layer.line([(x + 12, 0), (x + 12, 160)], (0.12, 0.12, 0.12), width=1)
    layer.rect(24, 74, 424, 86, BLACK, smooth=0.3)
    for x in range(26, 424, 5):
        layer.line([(x, 77), (x, 83)], (0.5, 0.5, 0.52), width=1.2, metal=1.0, smooth=0.6)
    layer.rect(404, 72, 420, 88, (0.6, 0.6, 0.62), metal=1.0, smooth=0.6, radius=2)
    return layer.finish(grain=0.08, blotch=0.07, edge=0.2)


def paint_mattress_top():
    navy = SWATCHES["vinyl_navy"][0]
    layer = Layer(640, 192, navy, 0.0, 0.5)
    dark, light = tuple(c * 0.45 for c in navy), tuple(min(1, c * 1.9 + 0.02) for c in navy)
    layer.rect(8, 8, 632, 184, dark, radius=14)
    layer.rect(10, 10, 630, 182, navy, radius=12)
    for x in (115, 225, 340, 455, 548):       # welded seams; 455 is the hinge of the head section
        layer.line([(x, 10), (x, 182)], dark, width=2.5 if x == 455 else 1.5)
        layer.line([(x + 2.5, 10), (x + 2.5, 182)], light, width=1)
    return layer.finish(grain=0.03, blotch=0.08)


def paint_cloth(colour, stripes=None, fold=0.07):
    def paint():
        layer = Layer(192, 512, colour, 0.0, 0.1)
        for y, height, tint in stripes or ():
            layer.rect(0, y, 192, y + height, tint)
        layer.finish(grain=0.05, blotch=0.03)
        # Long soft folds running along the cloth.
        base = np.asarray(layer.base, dtype=np.float64) / 255.0
        w, h = layer.base.size
        folds = np.asarray(Image.fromarray((_rng.uniform(0, 1, (5, 9)) * 255).astype(np.uint8), "L").resize((w, h), Image.BICUBIC), dtype=np.float64) / 127.5 - 1.0
        base = base * (1.0 + fold * folds)[..., None]
        layer.base = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8), "RGB")
        return layer
    return paint


def paint_banded(width, height, bands):
    """A post or cone side: orange, with white reflective bands given as (from, to) fractions of the height from the top."""
    def paint():
        colour, _, smooth, _ = SWATCHES["orange_plastic"]
        layer = Layer(width, height, colour, 0.0, smooth)
        for top, bottom in bands:
            layer.rect(0, top * height, width, bottom * height, (0.95, 0.95, 0.93), smooth=0.6)
            layer.line([(0, top * height), (width, top * height)], (0.75, 0.72, 0.68), width=1)
            layer.line([(0, bottom * height), (width, bottom * height)], (0.75, 0.72, 0.68), width=1)
        return layer.finish(grime=0.75, grain=0.04, blotch=0.06, scratches=14)
    return paint


def paint_first_aid():
    green = SWATCHES["plastic_green"][0]
    layer = Layer(256, 192, green, 0.0, 0.45)
    layer.line([(0, 44), (256, 44)], tuple(c * 0.5 for c in green), width=2)
    for x in (56, 200):
        layer.rect(x - 14, 34, x + 14, 56, (0.75, 0.76, 0.78), metal=1.0, smooth=0.6, radius=3)
    layer.rect(113, 62, 143, 138, WHITE)
    layer.rect(90, 85, 166, 115, WHITE)
    layer.text("FIRST AID", (40, 148, 216, 180), WHITE, weight=0.05)
    return layer.finish(grime=0.3, grain=0.03, blotch=0.04, scratches=10, edge=0.2)


def paint_evidence(number):
    def paint():
        yellow = SWATCHES["tent_yellow"][0]
        layer = Layer(112, 152, yellow, 0.0, 0.4)
        layer.rect(0, 132, 112, 152, BLACK)
        layer.text(str(number), (20, 12, 92, 120), BLACK, weight=0.07)
        return layer.finish(grain=0.03, blotch=0.04, edge=0.15)
    return paint


def paint_badge():
    gold = SWATCHES["gold"][0]
    dark = tuple(c * 0.55 for c in gold)
    light = tuple(min(1.0, c * 1.2) for c in gold)
    layer = Layer(128, 160, gold, 1.0, 0.74)
    for k in range(16):          # sunburst behind the seal
        a = math.radians(k * 22.5)
        layer.line([(64 + math.cos(a) * 24, 78 + math.sin(a) * 24), (64 + math.cos(a) * 46, 78 + math.sin(a) * 46)], dark, width=2)
    layer.disc(64, 78, 27, dark)
    layer.disc(64, 78, 24, (0.05, 0.16, 0.50), metal=0.0, smooth=0.85)
    star = [(64 + math.sin(math.radians(36 * k)) * (17 if k % 2 == 0 else 7), 78 - math.cos(math.radians(36 * k)) * (17 if k % 2 == 0 else 7)) for k in range(10)]
    layer.poly(star, light, metal=1.0, smooth=0.8)
    layer.rect(26, 18, 102, 36, dark, radius=4)
    layer.rect(28, 20, 100, 34, light, radius=3)
    layer.rect(36, 122, 92, 142, dark, radius=3)
    layer.text("4172", (40, 124, 88, 140), light, weight=0.04)
    return layer.finish(grain=0.03, blotch=0.06)


def paint_radio():
    body = (0.07, 0.07, 0.08)
    layer = Layer(96, 152, body, 0.0, 0.35)
    layer.rect(6, 6, 90, 146, (0.10, 0.10, 0.11), radius=10)
    for y in range(26, 96, 9):
        layer.rect(20, y, 76, y + 4, (0.015, 0.015, 0.02), radius=2)
        layer.line([(21, y + 5), (75, y + 5)], (0.2, 0.2, 0.21), width=1)
    layer.disc(22, 15, 5, (0.9, 0.35, 0.05), smooth=0.5)
    layer.disc(74, 15, 2.5, (0.2, 0.9, 0.3), smooth=0.6)
    layer.rect(22, 108, 74, 134, (0.55, 0.56, 0.58), metal=1.0, smooth=0.5, radius=3)
    return layer.finish(grain=0.05, blotch=0.04)


def paint_wheel():
    layer = Layer(96, 96, SWATCHES["rubber_black"][0], 0.0, 0.22)
    layer.disc(48, 48, 33, (0.42, 0.43, 0.45), smooth=0.45)
    layer.disc(48, 48, 29, (0.60, 0.61, 0.63), smooth=0.45)
    for k in range(5):
        a = math.radians(72 * k)
        layer.line([(48, 48), (48 + math.cos(a) * 29, 48 + math.sin(a) * 29)], (0.36, 0.37, 0.39), width=3)
    layer.disc(48, 48, 9, (0.30, 0.31, 0.33), metal=1.0, smooth=0.6)
    layer.disc(48, 48, 4, (0.7, 0.7, 0.72), metal=1.0, smooth=0.7)
    return layer.finish(grain=0.04, blotch=0.05)


def paint_can():
    layer = Layer(160, 128, (0.07, 0.36, 0.70), 0.0, 0.6)
    layer.rect(0, 30, 160, 52, WHITE, smooth=0.6)
    layer.rect(0, 56, 160, 60, BLACK, smooth=0.6)
    layer.rect(0, 0, 160, 10, (0.75, 0.76, 0.78), metal=1.0, smooth=0.7)
    layer.rect(0, 120, 160, 128, (0.75, 0.76, 0.78), metal=1.0, smooth=0.7)
    layer.rect(0, 84, 160, 92, (0.95, 0.75, 0.08), smooth=0.6)
    return layer.finish(grime=0.25, grain=0.03, blotch=0.05, scratches=12)


def paint_strap():
    orange = (0.88, 0.36, 0.04)
    layer = Layer(224, 40, orange, 0.0, 0.25)
    for x in range(0, 224, 3):
        layer.line([(x, 0), (x, 40)], tuple(c * 0.86 for c in orange), width=1)
    for y in (7, 33):
        layer.line([(0, y), (224, y)], tuple(c * 0.55 for c in orange), width=1.5)
    return layer.finish(grain=0.05, blotch=0.04)


def paint_sign(text, ground, border=WHITE, letters=WHITE):
    def paint():
        layer = Layer(176, 176, border, 0.0, 0.55)
        corners = lambda r: [(88 + math.cos(math.radians(22.5 + 45 * k)) * r, 88 + math.sin(math.radians(22.5 + 45 * k)) * r) for k in range(8)]
        layer.poly(corners(95), border, smooth=0.55)
        layer.poly(corners(86), ground, smooth=0.5)
        layer.text(text, (22, 56, 154, 120), letters, weight=0.07, smooth=0.6)
        return layer.finish(grain=0.03, blotch=0.04, scratches=10)
    return paint


def paint_page(width, height, title, lines, tint=(0.93, 0.92, 0.88), band=None):
    """A printed sheet: an optional coloured band with a title, then lines of 'writing'."""
    def paint():
        layer = Layer(width, height, tint, 0.0, 0.18)
        top = 8
        if band:
            layer.rect(0, 0, width, height * 0.26, band)
            layer.text(title, (8, 5, width - 8, height * 0.26 - 5), WHITE, weight=0.06)
            top = height * 0.26 + 8
        elif title:
            layer.text(title, (8, 6, width - 8, 28), BLACK, weight=0.05)
            top = 36
        step = (height - top - 8) / max(1, lines)
        for k in range(lines):
            y = top + step * (k + 0.5)
            layer.line([(10, y), (width - 10 - (17 * k * k) % (width // 3), y)], (0.30, 0.32, 0.38), width=max(1.0, step * 0.16))
        return layer.finish(grain=0.02, blotch=0.03)
    return paint


def paint_parcel_label():
    layer = Layer(128, 88, (0.95, 0.95, 0.93), 0.0, 0.3)
    for k in range(4):
        layer.line([(8, 14 + k * 9), (70 - k * 9, 14 + k * 9)], BLACK, width=2)
    for x in range(10, 118, 5):
        layer.rect(x, 58, x + (2 if (x // 5) % 3 else 3), 80, BLACK)
    layer.rect(90, 8, 120, 38, (0.75, 0.10, 0.08))
    return layer.finish(grain=0.03, blotch=0.03)


def paint_cup():
    layer = Layer(128, 128, (0.95, 0.94, 0.91), 0.0, 0.3)
    layer.rect(0, 40, 128, 96, (0.55, 0.38, 0.22), smooth=0.1)          # the sleeve
    layer.disc(64, 68, 16, (0.25, 0.14, 0.07))
    layer.disc(64, 68, 11, (0.55, 0.38, 0.22))
    return layer.finish(grime=0.1, grain=0.03, blotch=0.03)


def paint_cart_sign():
    layer = Layer(448, 128, (0.05, 0.15, 0.40), 0.0, 0.4)
    layer.rect(6, 6, 442, 122, (0.95, 0.90, 0.78), radius=10)
    layer.rect(11, 11, 437, 117, (0.05, 0.15, 0.40), radius=8)
    layer.text("COFFEE  TEA  BAGELS", (30, 34, 418, 94), (0.95, 0.90, 0.78), weight=0.05, spacing=0.04)
    return layer.finish(grime=0.2, grain=0.04, blotch=0.05, scratches=20)


def paint_awning():
    layer = Layer(192, 96, (0.92, 0.91, 0.87), 0.0, 0.15)
    for x in range(0, 192, 48):
        layer.rect(x, 0, x + 24, 96, (0.62, 0.10, 0.08))
    return layer.finish(grime=0.15, grain=0.05, blotch=0.06)


def paint_board():
    layer = Layer(208, 296, (0.07, 0.08, 0.08), 0.0, 0.15)
    chalk = (0.92, 0.92, 0.88)
    layer.rect(8, 8, 200, 288, (0.10, 0.11, 0.11), radius=6)
    layer.text("OPEN", (30, 26, 178, 82), chalk, weight=0.05)
    layer.line([(40, 96), (168, 96)], chalk, width=2)
    layer.text("FRESH COFFEE", (24, 112, 184, 146), chalk, weight=0.04)
    layer.text("HOT BAGELS", (34, 158, 174, 190), chalk, weight=0.04)
    layer.text("COME IN", (44, 214, 164, 258), (0.95, 0.80, 0.25), weight=0.05)
    return layer.finish(grain=0.06, blotch=0.08, edge=0.2)


# name: painter. The tape is placed first, across the whole atlas.
DECALS = {
    "barrier_board": paint_barrier_board,
    "mattress_top": paint_mattress_top,
    "panel_police": paint_panel("POLICE", (0.035, 0.05, 0.13)),
    "panel_ems": paint_panel("PARAMEDIC", (0.02, 0.30, 0.17)),
    "chest_ems": paint_panel("EMS", (0.02, 0.30, 0.17), 208, 96),
    "bag_front": paint_bag_front,
    "duffel_top": paint_duffel_top,
    "sheet_cloth": paint_cloth(SWATCHES["linen"][0], fold=0.06),
    "blanket_cloth": paint_cloth(SWATCHES["wool_blue"][0], stripes=((40, 14, (0.62, 0.66, 0.70)), (62, 6, (0.62, 0.66, 0.70)),
                                                                     (444, 6, (0.62, 0.66, 0.70)), (458, 14, (0.62, 0.66, 0.70))), fold=0.09),
    "cone_side": paint_banded(128, 384, ((0.15, 0.36), (0.44, 0.56))),
    "post_side": paint_banded(64, 384, ((0.10, 0.20), (0.27, 0.37))),
    "first_aid_face": paint_first_aid,
    "evidence_1": paint_evidence(1),
    "evidence_2": paint_evidence(2),
    "evidence_3": paint_evidence(3),
    "evidence_4": paint_evidence(4),
    "badge_face": paint_badge,
    "radio_face": paint_radio,
    "wheel_side": paint_wheel,
    "can_label": paint_can,
    "strap": paint_strap,
    # City life (city_props.py).
    "stop_face": paint_sign("STOP", (0.72, 0.06, 0.05)),
    "slow_face": paint_sign("SLOW", (0.95, 0.45, 0.04), border=BLACK, letters=BLACK),
    "clipboard_page": paint_page(160, 224, "REPORT", 11),
    "leaflet_face": paint_page(112, 160, "SALE", 6, band=(0.80, 0.12, 0.10)),
    "parcel_label": paint_parcel_label,
    "cup_side": paint_cup,
    "cart_sign": paint_cart_sign,
    "awning": paint_awning,
    "board_face": paint_board,
}


# ---------------------------------------------------------------------------------------------- life props
# What life_props.py needs. Painted AFTER everything above (build() keeps that order), so the regions above come
# out pixel for pixel as they did before these existed: the shared random stream has already passed them.

LIFE_SWATCHES = {
    "wood_dark": ((0.30, 0.15, 0.07), 0.0, 0.5, 0.2),
    "tape_brown": ((0.50, 0.34, 0.16), 0.0, 0.6, 0.1),
    "wet_paint": ((0.22, 0.58, 0.80), 0.0, 0.7, 0.0),
    "stainless": ((0.74, 0.75, 0.76), 1.0, 0.70, 0.25),
    "fabric_teal": ((0.04, 0.36, 0.40), 0.0, 0.15, 0.3),
    "nylon_orange": ((0.95, 0.40, 0.04), 0.0, 0.25, 0.35),
    "canvas_mail": ((0.20, 0.30, 0.42), 0.0, 0.12, 0.4),
    "concrete": ((0.60, 0.59, 0.56), 0.0, 0.12, 0.6),
    "paint_green": ((0.05, 0.24, 0.14), 0.0, 0.4, 0.6),
    "plush_red": ((0.42, 0.03, 0.05), 0.0, 0.05, 0.1),
    "water_grey": ((0.34, 0.40, 0.39), 0.0, 0.9, 0.0),
    "sausage": ((0.62, 0.22, 0.12), 0.0, 0.5, 0.0),
}
SWATCHES.update(LIFE_SWATCHES)

INK = (0.13, 0.13, 0.14)
NEWSPRINT = (0.84, 0.83, 0.78)


def _columns(layer, rng, x0, x1, y0, y1, count, step=4.0, colour=(0.38, 0.38, 0.40)):
    """Columns of 'type': short strokes with paragraph breaks."""
    gap = 6.0
    width = (x1 - x0 - gap * (count - 1)) / count
    for c in range(count):
        left = x0 + c * (width + gap)
        y = y0
        while y < y1:
            if rng.uniform() < 0.10:
                y += step
                continue
            layer.line([(left, y), (left + width * (1.0 if rng.uniform() < 0.85 else rng.uniform(0.3, 0.8)), y)], colour, width=1.3)
            y += step


def _photo(layer, x0, y0, x1, y1, sky=(0.62, 0.66, 0.70)):
    layer.rect(x0, y0, x1, y1, sky)
    width = x1 - x0
    for k, (a, b, h) in enumerate(((0.05, 0.22, 0.55), (0.20, 0.36, 0.80), (0.40, 0.58, 0.45), (0.56, 0.74, 0.70), (0.76, 0.96, 0.5))):
        tone = 0.22 + 0.07 * (k % 3)
        layer.rect(x0 + a * width, y1 - (y1 - y0) * h, x0 + b * width, y1, (tone, tone, tone + 0.02))
    layer.rect(x0, y1 - 3, x1, y1, (0.15, 0.15, 0.16))


def paint_news(front):
    def paint():
        rng = np.random.default_rng(21 if front else 22)
        layer = Layer(232, 320, NEWSPRINT, 0.0, 0.12)
        if front:
            layer.text("THE DAILY POST", (14, 8, 218, 40), INK, weight=0.045)
            layer.line([(10, 45), (222, 45)], INK, width=2.5)
            layer.line([(10, 49), (222, 49)], INK, width=1)
            layer.text("CITY BOOMS", (10, 55, 222, 96), INK, weight=0.075)
            _photo(layer, 10, 104, 140, 196)
            _columns(layer, rng, 148, 222, 106, 196, 1)
            layer.line([(10, 204), (222, 204)], INK, width=1)
            layer.text("RENTS UP AGAIN", (10, 209, 130, 226), INK, weight=0.05)
            _columns(layer, rng, 10, 222, 232, 312, 4)
        else:
            layer.line([(10, 14), (222, 14)], INK, width=1.5)
            layer.text("BUSINESS", (10, 18, 90, 34), INK, weight=0.05)
            _columns(layer, rng, 10, 222, 42, 132, 4)
            _photo(layer, 64, 140, 222, 222, sky=(0.70, 0.70, 0.68))
            _columns(layer, rng, 10, 58, 140, 222, 1)
            layer.text("MARKETS STEADY", (10, 229, 150, 246), INK, weight=0.05)
            _columns(layer, rng, 10, 222, 252, 312, 4)
        return layer.finish(grain=0.035, blotch=0.06)
    return paint


def paint_map_print():
    land, street, casing = (0.93, 0.90, 0.79), (0.99, 0.99, 0.97), (0.72, 0.70, 0.64)
    layer = Layer(352, 240, land, 0.0, 0.2)
    rng = np.random.default_rng(31)
    for _ in range(40):                                  # city blocks in two tints
        x, y = rng.uniform(0, 352), rng.uniform(0, 240)
        layer.rect(x, y, x + rng.uniform(20, 60), y + rng.uniform(14, 40), (0.90, 0.86, 0.74))
    layer.rect(214, 28, 304, 96, (0.62, 0.80, 0.56), radius=8)                  # the park
    layer.disc(250, 62, 13, (0.52, 0.74, 0.86))
    layer.rect(40, 150, 92, 196, (0.66, 0.82, 0.60), radius=5)
    river = [(x, 205 - 70 * math.sin(x / 352 * 2.2) - x * 0.25) for x in range(0, 353, 16)]
    layer.line(river, (0.50, 0.72, 0.86), width=17)
    for tone, width in ((casing, 4.4), (street, 2.8)):     # the street grid, a little off square
        for k in range(-4, 16):
            x = k * 30 + 9
            layer.line([(x, 0), (x + 34, 240)], tone, width=width)
        for k in range(-2, 10):
            y = k * 28 + 12
            layer.line([(0, y + 16), (352, y)], tone, width=width)
    for points in ([(0, 78), (352, 62)], [(130, 0), (164, 240)], [(0, 20), (352, 236)]):         # avenues
        layer.line(points, (0.80, 0.56, 0.16), width=7.5)
        layer.line(points, (0.99, 0.82, 0.36), width=5)
    layer.disc(182, 118, 7, (0.80, 0.10, 0.08))
    layer.disc(182, 118, 3, WHITE)
    layer.rect(0, 0, 352, 5, WHITE)
    layer.rect(0, 235, 352, 240, WHITE)
    for x in (352 / 3, 704 / 3):                          # the creases, worn pale
        layer.line([(x, 0), (x, 240)], (0.98, 0.97, 0.93), width=2)
    layer.line([(0, 120), (352, 120)], (0.98, 0.97, 0.93), width=1.5)
    return layer.finish(grain=0.03, blotch=0.05)


def paint_map_cover():
    blue, cream = (0.06, 0.27, 0.52), (0.95, 0.93, 0.85)
    layer = Layer(112, 232, blue, 0.0, 0.3)
    layer.rect(8, 8, 104, 224, cream, radius=3)
    layer.rect(11, 11, 101, 221, blue, radius=2)
    layer.text("CITY", (18, 26, 94, 62), cream, weight=0.06)
    layer.text("MAP", (18, 66, 94, 104), (0.98, 0.80, 0.20), weight=0.07)
    layer.disc(56, 152, 30, cream)                                              # a compass rose
    layer.disc(56, 152, 27, blue)
    for k in range(4):
        a = math.radians(90 * k)
        tip = (56 + math.sin(a) * 25, 152 - math.cos(a) * 25)
        side = (math.cos(a) * 6, math.sin(a) * 6)
        layer.poly([tip, (56 + side[0], 152 + side[1]), (56 - side[0], 152 - side[1])], cream if k else (0.85, 0.15, 0.10))
    layer.text("STREETS  TRANSIT", (18, 196, 94, 210), cream, weight=0.03)
    return layer.finish(grain=0.03, blotch=0.05, edge=0.15)


def paint_notebook_page():
    layer = Layer(104, 152, (0.95, 0.94, 0.88), 0.0, 0.18)
    rng = np.random.default_rng(41)
    blue = (0.10, 0.14, 0.42)
    for y in range(20, 150, 8):
        layer.line([(0, y), (104, y)], (0.60, 0.72, 0.86), width=1)
    for row, y in enumerate(range(20, 116, 8)):          # handwriting: joined-up waves, word by word
        x = 8.0
        end = 96 - (0 if row % 4 != 3 else rng.uniform(20, 50))
        while x < end - 8:
            length = min(rng.uniform(8, 22), end - x)
            points = [(x + t, y - 3.2 + 2.2 * math.sin(t * 1.9 + row) * (0.6 + 0.4 * math.sin(t * 0.7))) for t in np.arange(0, length, 1.0)]
            if len(points) > 1:
                layer.line(points, blue, width=1.0)
            x += length + 4
    layer.line([(10, 126), (60, 126)], blue, width=1.4)
    layer.line([(10, 129.5), (48, 129.5)], blue, width=1.0)
    return layer.finish(grain=0.02, blotch=0.03)


def paint_flyer():
    yellow, blue = (0.98, 0.84, 0.16), (0.10, 0.20, 0.55)
    layer = Layer(152, 216, yellow, 0.0, 0.3)
    layer.rect(0, 0, 152, 86, blue)
    layer.text("GRAND", (10, 8, 142, 44), yellow, weight=0.07)
    layer.text("OPENING", (10, 46, 142, 78), WHITE, weight=0.07)
    burst = [(104 + math.cos(math.radians(15 * k)) * (30 if k % 2 == 0 else 22), 120 + math.sin(math.radians(15 * k)) * (30 if k % 2 == 0 else 22)) for k in range(24)]
    layer.poly(burst, (0.86, 0.10, 0.10))
    layer.text("50%", (84, 106, 124, 124), WHITE, weight=0.07)
    layer.text("OFF", (88, 124, 120, 136), WHITE, weight=0.06)
    for k, y in enumerate(range(100, 150, 8)):
        layer.line([(10, y), (64 - (k * 7) % 22, y)], (0.20, 0.18, 0.12), width=2)
    layer.rect(0, 160, 152, 216, (0.96, 0.95, 0.90))                            # the tear-off coupons
    for x in range(4, 152, 6):
        layer.line([(x, 160), (x + 3, 160)], (0.20, 0.18, 0.12), width=1.2)
    for x in range(19, 150, 19):
        layer.line([(x, 162), (x, 216)], (0.35, 0.35, 0.35), width=1)
    for x in range(0, 152, 19):
        layer.line([(x + 6, 170), (x + 6, 208)], blue, width=1.6)
        layer.line([(x + 11, 174), (x + 11, 204)], (0.30, 0.30, 0.30), width=1.0)
    return layer.finish(grain=0.025, blotch=0.03)


def paint_taxi():
    yellow = (0.99, 0.80, 0.04)
    layer = Layer(272, 96, yellow, 0.0, 0.6)
    layer.rect(5, 5, 267, 91, BLACK, radius=12)
    layer.rect(9, 9, 263, 87, yellow, radius=9)
    layer.text("TAXI", (40, 14, 232, 70), BLACK, weight=0.08, spacing=0.06)
    for k in range(20):
        if k % 2 == 0:
            layer.rect(16 + k * 12, 73, 28 + k * 12, 81, BLACK)
    return layer.finish(grain=0.02, blotch=0.03)


def paint_hotdog_sign():
    red, cream = (0.74, 0.07, 0.05), (0.99, 0.84, 0.22)
    layer = Layer(448, 128, red, 0.0, 0.45)
    layer.rect(6, 6, 442, 122, cream, radius=12)
    layer.rect(11, 11, 437, 117, red, radius=9)
    layer.text("HOT DOGS", (112, 28, 336, 100), cream, weight=0.075, spacing=0.04)
    for x in (62, 386):                                   # a hot dog either side
        layer.rect(x - 38, 50, x + 38, 84, (0.86, 0.62, 0.30), radius=15)
        layer.rect(x - 44, 56, x + 44, 74, SWATCHES["sausage"][0], radius=9)
        layer.line([(x - 36 + 6 * k, 65 + (3 if k % 2 else -3)) for k in range(13)], (0.98, 0.82, 0.10), width=2.4)
        layer.rect(x - 38, 76, x + 38, 86, (0.80, 0.55, 0.25), radius=5)
    return layer.finish(grime=0.2, grain=0.04, blotch=0.05, scratches=14)


def _notice(layer, rng, x, y, w, h, tint, title=None, strips=False, pin=(0.80, 0.10, 0.08)):
    layer.rect(x + 3, y + 4, x + w + 3, y + h + 4, (0.34, 0.24, 0.14))          # its shadow on the cork
    layer.rect(x, y, x + w, y + h, tint)
    top = y + 8
    if title:
        layer.text(title, (x + 5, y + 9, x + w - 5, y + 9 + min(24, h * 0.3)), INK, weight=0.06)
        top = y + 14 + min(24, h * 0.3)
    bottom = y + h - (h * 0.30 if strips else 6)
    yy = top
    while yy < bottom - 2:
        layer.line([(x + 6, yy), (x + w - 6 - rng.uniform(0, w * 0.3), yy)], (0.30, 0.30, 0.33), width=1.6)
        yy += 6
    if strips:
        layer.line([(x, bottom), (x + w, bottom)], (0.30, 0.30, 0.33), width=1)
        count = max(4, int(w // 11))
        for k in range(count):
            sx = x + w * k / count
            if rng.uniform() < 0.25:
                layer.rect(sx, bottom + 1, sx + w / count, y + h + 4, (0.60, 0.43, 0.26))   # torn off: cork shows
            else:
                layer.line([(sx, bottom), (sx, y + h)], (0.30, 0.30, 0.33), width=1)
                layer.line([(sx + w / count / 2, bottom + 4), (sx + w / count / 2, y + h - 4)], (0.20, 0.25, 0.50), width=1.4)
    layer.disc(x + w / 2, y + 5, 4.2, tuple(c * 0.6 for c in pin))
    layer.disc(x + w / 2 - 0.8, y + 4.2, 3.2, pin, smooth=0.7)


def paint_job_board():
    cork, blue = (0.63, 0.46, 0.28), (0.06, 0.19, 0.42)
    layer = Layer(448, 416, cork, 0.0, 0.08)
    rng = np.random.default_rng(51)
    for _ in range(1300):                                  # the cork's fleck
        x, y = rng.uniform(0, 448), rng.uniform(0, 416)
        tone = rng.uniform(0.75, 1.15)
        layer.disc(x, y, rng.uniform(0.6, 1.6), tuple(c * tone for c in cork))
    layer.rect(0, 0, 448, 76, blue, smooth=0.4)
    layer.rect(6, 6, 442, 70, WHITE, radius=5)
    layer.rect(9, 9, 439, 67, blue, radius=4)
    layer.text("HELP WANTED", (28, 16, 420, 62), WHITE, weight=0.07, spacing=0.05)
    paper, yellow, pink, green, sky = (0.95, 0.94, 0.90), (0.98, 0.90, 0.45), (0.96, 0.70, 0.74), (0.70, 0.88, 0.66), (0.66, 0.82, 0.94)
    _notice(layer, rng, 16, 92, 124, 150, paper, "JOBS", strips=True)
    _notice(layer, rng, 156, 88, 96, 74, yellow, "COOK", pin=(0.10, 0.25, 0.70))
    _notice(layer, rng, 268, 94, 160, 104, paper, "NOW HIRING", pin=(0.10, 0.50, 0.20))
    _notice(layer, rng, 160, 176, 92, 126, pink, "DRIVERS", strips=True, pin=(0.95, 0.75, 0.10))
    _notice(layer, rng, 20, 258, 118, 82, green, "CLEANER")
    _notice(layer, rng, 270, 214, 76, 96, sky, "SHOP", strips=True)
    _notice(layer, rng, 360, 208, 72, 60, yellow, None, pin=(0.10, 0.25, 0.70))
    _notice(layer, rng, 150, 318, 150, 84, paper, "APPLY WITHIN", pin=(0.80, 0.10, 0.08))
    _notice(layer, rng, 318, 326, 112, 76, pink, "WAITER", strips=True, pin=(0.10, 0.50, 0.20))
    _notice(layer, rng, 24, 352, 100, 52, sky, None)
    return layer.finish(grime=0.12, grain=0.04, blotch=0.05)


def paint_news_fascia():
    cream, green = (0.95, 0.88, 0.62), SWATCHES["paint_green"][0]
    layer = Layer(416, 72, cream, 0.0, 0.45)
    layer.rect(4, 4, 412, 68, green, radius=6)
    layer.rect(8, 8, 408, 64, cream, radius=4)
    layer.text("NEWS", (130, 12, 286, 60), green, weight=0.08, spacing=0.12)
    for x in (70, 346):
        star = [(x + math.sin(math.radians(36 * k)) * (17 if k % 2 == 0 else 7), 36 - math.cos(math.radians(36 * k)) * (17 if k % 2 == 0 else 7)) for k in range(10)]
        layer.poly(star, (0.72, 0.10, 0.08))
    return layer.finish(grime=0.15, grain=0.03, blotch=0.05, scratches=10)


MAG_TINTS = ((0.80, 0.12, 0.10), (0.10, 0.30, 0.66), (0.96, 0.78, 0.10), (0.10, 0.50, 0.32), (0.90, 0.42, 0.08), (0.50, 0.16, 0.50), (0.90, 0.90, 0.86), (0.08, 0.08, 0.10),
             (0.20, 0.62, 0.74), (0.86, 0.30, 0.50))


def paint_magazines(width, height, columns, rows):
    def paint():
        rng = np.random.default_rng(61 + columns)
        layer = Layer(width, height, (0.06, 0.06, 0.07), 0.0, 0.3)
        cw, rh = width / columns, height / rows
        for r in range(rows):
            for c in range(columns):
                x0, y0 = c * cw + 3, r * rh + 5
                x1, y1 = (c + 1) * cw - 3, (r + 1) * rh - 9
                tint = MAG_TINTS[int(rng.integers(len(MAG_TINTS)))]
                if rng.uniform() < 0.22:                 # a newspaper among them
                    layer.rect(x0, y0, x1, y1, NEWSPRINT, smooth=0.12)
                    layer.rect(x0 + 3, y0 + 3, x1 - 3, y0 + 10, INK)
                    for yy in np.arange(y0 + 15, y1 - 3, 4):
                        layer.line([(x0 + 3, yy), (x1 - 3, yy)], (0.40, 0.40, 0.42), width=1.2)
                    continue
                layer.rect(x0, y0, x1, y1, tint, smooth=0.6)
                light = sum(tint) > 1.6
                layer.rect(x0 + 3, y0 + 3, x1 - 3, y0 + 3 + (y1 - y0) * 0.17, BLACK if light else WHITE)
                other = MAG_TINTS[int(rng.integers(len(MAG_TINTS)))]
                cx, cy = (x0 + x1) / 2 + rng.uniform(-3, 3), y0 + (y1 - y0) * 0.60
                layer.disc(cx, cy, min(x1 - x0, y1 - y0) * 0.26, tuple(0.5 * a + 0.5 * b for a, b in zip(other, (0.9, 0.75, 0.6))), smooth=0.6)
                layer.rect(x0 + 3, y1 - 8, x0 + (x1 - x0) * 0.6, y1 - 5, BLACK if light else WHITE)
            layer.rect(0, (r + 1) * rh - 7, width, (r + 1) * rh - 1, (0.45, 0.46, 0.48), metal=1.0, smooth=0.6)       # the rack's lip
        return layer.finish(grime=0.1, grain=0.03, blotch=0.04)
    return paint


def paint_chess_board():
    light, dark = (0.84, 0.82, 0.76), (0.15, 0.15, 0.16)
    layer = Layer(160, 160, (0.30, 0.29, 0.28), 0.0, 0.45)
    for r in range(8):
        for c in range(8):
            layer.rect(8 + c * 18, 8 + r * 18, 26 + c * 18, 26 + r * 18, light if (r + c) % 2 == 0 else dark, smooth=0.5)
    return layer.finish(grain=0.05, blotch=0.08, scratches=12)


def paint_fruit(colour, kind="round", stem=None):
    """A crate's worth from above: each fruit a shaded ball, so the mound reads as a heap and not as a colour."""
    def paint():
        rng = np.random.default_rng(int(sum(colour) * 1000))
        deep = tuple(c * 0.28 for c in colour)
        layer = Layer(112, 112, deep, 0.0, 0.35)
        if kind == "banana":
            for k in range(22):
                cx, cy, turn = rng.uniform(-5, 117), rng.uniform(-5, 117), rng.uniform(0, 2 * math.pi)
                arc = [(cx + math.cos(turn + t) * 26 - math.cos(turn) * 26, cy + math.sin(turn + t) * 26 - math.sin(turn) * 26) for t in np.linspace(-0.55, 0.55, 9)]
                layer.line(arc, tuple(c * 0.62 for c in colour), width=10)
                layer.line(arc, colour, width=7.5)
                layer.line(arc[2:7], tuple(min(1.0, c * 1.15 + 0.06) for c in colour), width=2.5)
                layer.disc(*arc[0], 2.6, (0.30, 0.22, 0.08))
                layer.disc(*arc[-1], 2.2, (0.25, 0.30, 0.10))
            return layer.finish(grain=0.03, blotch=0.04)
        radius = 12.5
        row = 0
        y = -4.0
        while y < 122:
            x = -6.0 + (radius if row % 2 else 0.0)
            while x < 124:
                cx, cy = x + rng.uniform(-1.5, 1.5), y + rng.uniform(-1.5, 1.5)
                tone = rng.uniform(0.86, 1.08)
                body = tuple(min(1.0, c * tone) for c in colour)
                layer.disc(cx, cy, radius, tuple(c * 0.55 for c in body))
                layer.disc(cx - 0.8, cy - 0.8, radius - 1.6, tuple(c * 0.82 for c in body))
                layer.disc(cx - 2.2, cy - 2.4, radius - 4.6, body)
                layer.disc(cx - 4.2, cy - 4.6, radius - 8.6, tuple(min(1.0, c * 1.12 + 0.10) for c in body), smooth=0.6)
                if stem:
                    layer.disc(cx + 1.5, cy + 1.0, 1.7, stem)
                x += radius * 2 - 1.0
            y += radius * 1.72
            row += 1
        return layer.finish(grain=0.03, blotch=0.04)
    return paint


def paint_price_card():
    layer = Layer(56, 40, (0.97, 0.96, 0.90), 0.0, 0.2)
    layer.rect(0, 0, 56, 40, (0.80, 0.10, 0.08))
    layer.rect(3, 3, 53, 37, (0.97, 0.96, 0.90))
    layer.text("$2", (8, 6, 48, 34), INK, weight=0.08)
    return layer.finish(grain=0.02, blotch=0.03)


def paint_handset():
    layer = Layer(72, 208, (0.075, 0.075, 0.085), 0.0, 0.35)
    layer.rect(4, 4, 68, 204, (0.11, 0.11, 0.12), radius=8)
    layer.rect(12, 18, 60, 54, (0.02, 0.02, 0.025), radius=3)
    layer.rect(15, 21, 57, 51, (0.50, 0.66, 0.46), smooth=0.8, radius=2)        # the display
    layer.text("CH 05", (18, 26, 54, 46), (0.08, 0.14, 0.08), weight=0.06)
    for r in range(2):
        for c in range(3):
            layer.rect(13 + c * 16, 62 + r * 13, 26 + c * 16, 71 + r * 13, (0.24, 0.24, 0.26), radius=2)
    layer.rect(45, 62, 59, 71, (0.90, 0.38, 0.05), radius=2)
    for y in range(98, 186, 9):                            # the speaker grille
        layer.rect(14, y, 58, y + 4, (0.015, 0.015, 0.02), radius=2)
        layer.line([(15, y + 5), (57, y + 5)], (0.22, 0.22, 0.23), width=1)
    layer.disc(36, 10.5, 2.4, (0.90, 0.12, 0.08), smooth=0.7)
    return layer.finish(grain=0.05, blotch=0.04)


def paint_guitar_top():
    spruce = (0.83, 0.62, 0.33)
    layer = Layer(200, 248, spruce, 0.0, 0.62)
    for x in range(0, 200, 3):                             # the grain runs the length of the guitar
        layer.line([(x + 0.5, 0), (x + 0.5, 248)], tuple(c * (0.93 if (x // 3) % 3 else 0.88) for c in spruce), width=0.8)
    hole = (100.0, 93.0)
    for radius, tone in ((35.5, (0.20, 0.10, 0.05)), (33.5, (0.92, 0.86, 0.70)), (31.5, (0.20, 0.10, 0.05)), (29.5, spruce)):
        layer.disc(*hole, radius, tone)
    layer.disc(*hole, 25.0, (0.025, 0.02, 0.02), smooth=0.2)
    guard = [(hole[0] + 6, hole[1] - 30), (hole[0] + 44, hole[1] - 12), (hole[0] + 52, hole[1] + 26), (hole[0] + 30, hole[1] + 50), (hole[0] + 6, hole[1] + 30),
             (hole[0] + 26, hole[1] + 12), (hole[0] + 27, hole[1] - 10)]
    layer.poly(guard, (0.10, 0.05, 0.03), smooth=0.8)
    layer.rect(60, 174, 140, 190, (0.13, 0.07, 0.04), radius=3)                 # the bridge
    layer.rect(72, 178, 128, 181, (0.93, 0.91, 0.84))
    for k in range(6):
        t = k / 5.0
        layer.disc(86 + 28 * t, 186, 1.6, (0.93, 0.91, 0.84))
        layer.line([(86 + 28 * t, 179), (90 + 20 * t, 0)], (0.80, 0.76, 0.62), width=0.9 + 0.5 * (1 - t), metal=1.0, smooth=0.7)
    return layer.finish(grain=0.02, blotch=0.05)


def paint_fretboard():
    layer = Layer(48, 336, (0.12, 0.07, 0.045), 0.0, 0.4)
    scale, length = 0.73, 0.515                           # nut to saddle; nut to the end of the board
    frets = [scale * (1.0 - 2.0 ** (-n / 12.0)) / length * 336 for n in range(0, 22)]
    for n in (3, 5, 7, 9, 12, 15, 17):
        y = (frets[n - 1] + frets[n]) / 2
        if y < 334:
            for x in ((16, 32) if n == 12 else (24,)):
                layer.disc(x, y, 2.6, (0.90, 0.88, 0.80))
    for y in frets[1:]:
        if y < 334:
            layer.line([(0, y), (48, y)], (0.80, 0.80, 0.78), width=1.6, metal=1.0, smooth=0.8)
    for k in range(6):
        x = 5.5 + 7.4 * k
        layer.line([(x, 0), (x, 336)], (0.82, 0.78, 0.64), width=1.5 - 0.14 * k, metal=1.0, smooth=0.7)
    return layer.finish(grain=0.03, blotch=0.04)


def paint_mop_strings():
    cotton = (0.80, 0.79, 0.74)
    layer = Layer(160, 88, cotton, 0.0, 0.05)
    rng = np.random.default_rng(71)
    for k in range(64):
        x = 160 * (k + 0.5) / 64
        tone = rng.uniform(0.62, 1.10)
        sway, phase = rng.uniform(1.0, 3.0), rng.uniform(0, 6.28)
        points = [(x + sway * math.sin(phase + t * 0.09), t) for t in range(0, 89, 8)]
        layer.line(points, tuple(c * 0.55 for c in cotton), width=3.0)
        layer.line(points, tuple(min(1.0, c * tone) for c in cotton), width=1.9)
    return layer.finish(grain=0.05, blotch=0.10)


def paint_caution():
    yellow = SWATCHES["paint_yellow"][0]
    layer = Layer(160, 64, yellow, 0.0, 0.45)
    layer.poly([(28, 8), (52, 52), (4, 52)], BLACK)
    layer.poly([(28, 17), (44, 47), (12, 47)], yellow)
    layer.rect(26, 26, 30, 39, BLACK)
    layer.disc(28, 43, 2.2, BLACK)
    layer.text("CAUTION", (60, 8, 154, 32), BLACK, weight=0.07)
    layer.text("WET FLOOR", (60, 36, 154, 54), BLACK, weight=0.06)
    return layer.finish(grain=0.03, blotch=0.04, scratches=8)


def paint_food_logo():
    white, dark = (0.97, 0.96, 0.93), (0.10, 0.10, 0.11)
    layer = Layer(176, 152, white, 0.0, 0.4)
    layer.rect(0, 0, 176, 152, dark, radius=14)
    layer.rect(5, 5, 171, 147, white, radius=10)
    layer.disc(88, 60, 40, SWATCHES["nylon_orange"][0])
    layer.disc(88, 60, 25, white)                                               # a plate, a fork and a knife
    layer.disc(88, 60, 18, (0.90, 0.89, 0.86))
    layer.rect(52, 34, 55, 88, dark, radius=1)
    for dx in (-5, 0, 5):
        layer.rect(52 + dx, 30, 55 + dx, 48, dark, radius=1)
    layer.rect(121, 46, 125, 88, dark, radius=1)
    layer.poly([(121, 30), (128, 36), (128, 56), (121, 56)], dark)
    layer.text("QUICK BITE", (16, 108, 160, 138), dark, weight=0.07)
    return layer.finish(grime=0.15, grain=0.03, blotch=0.04)


def paint_banknote():
    green, pale = (0.50, 0.64, 0.46), (0.80, 0.86, 0.74)
    layer = Layer(80, 40, green, 0.0, 0.15)
    layer.rect(3, 3, 77, 37, pale)
    layer.rect(5, 5, 75, 35, green)
    layer.disc(40, 20, 11, pale)
    layer.disc(40, 20, 8, (0.36, 0.50, 0.34))
    for x in (13, 67):
        layer.text("5", (x - 6, 9, x + 6, 31), pale, weight=0.08)
    return layer.finish(grain=0.05, blotch=0.06)


def paint_fragile():
    red = (0.80, 0.10, 0.08)
    layer = Layer(112, 40, red, 0.0, 0.4)
    layer.rect(3, 3, 109, 37, WHITE)
    layer.rect(5, 5, 107, 35, red)
    layer.text("FRAGILE", (10, 8, 102, 32), WHITE, weight=0.07)
    return layer.finish(grain=0.03, blotch=0.03)


def paint_stripes(tint, width=192, height=96, pitch=48):
    def paint():
        layer = Layer(width, height, (0.92, 0.91, 0.87), 0.0, 0.15)
        for x in range(0, width, pitch):
            layer.rect(x, 0, x + pitch // 2, height, tint)
        return layer.finish(grime=0.15, grain=0.05, blotch=0.06)
    return paint


def paint_tearoff():
    layer = Layer(80, 112, (0.95, 0.94, 0.90), 0.0, 0.18)
    layer.text("ROOM", (8, 6, 72, 30), INK, weight=0.07)
    layer.text("TO LET", (8, 32, 72, 48), (0.80, 0.10, 0.08), weight=0.06)
    for k, y in enumerate(range(56, 76, 6)):
        layer.line([(8, y), (72 - (k * 9) % 20, y)], (0.30, 0.30, 0.33), width=1.6)
    layer.line([(0, 80), (80, 80)], (0.30, 0.30, 0.33), width=1)
    for x in range(10, 80, 10):
        layer.line([(x, 80), (x, 112)], (0.30, 0.30, 0.33), width=1)
    for x in range(0, 80, 10):
        layer.line([(x + 5, 85), (x + 5, 108)], (0.20, 0.25, 0.50), width=1.5)
    return layer.finish(grain=0.02, blotch=0.03)


LIFE_DECALS = {
    "news_front": paint_news(True),
    "news_inner": paint_news(False),
    "map_print": paint_map_print,
    "map_cover": paint_map_cover,
    "notebook_page": paint_notebook_page,
    "flyer_face": paint_flyer,
    "taxi_face": paint_taxi,
    "hotdog_sign": paint_hotdog_sign,
    "job_board_face": paint_job_board,
    "news_fascia": paint_news_fascia,
    "mag_wide": paint_magazines(384, 192, 8, 2),
    "mag_tall": paint_magazines(144, 304, 3, 4),
    "chess_board": paint_chess_board,
    "fruit_orange": paint_fruit((0.96, 0.52, 0.06), stem=(0.35, 0.30, 0.08)),
    "fruit_apple": paint_fruit((0.78, 0.09, 0.07), stem=(0.25, 0.15, 0.05)),
    "fruit_green": paint_fruit((0.45, 0.68, 0.14), stem=(0.25, 0.20, 0.05)),
    "fruit_lemon": paint_fruit((0.97, 0.86, 0.14)),
    "fruit_banana": paint_fruit((0.96, 0.80, 0.16), kind="banana"),
    "price_card": paint_price_card,
    "handset_face": paint_handset,
    "guitar_top": paint_guitar_top,
    "fretboard": paint_fretboard,
    "mop_strings": paint_mop_strings,
    "caution": paint_caution,
    "food_logo": paint_food_logo,
    "banknote": paint_banknote,
    "fragile": paint_fragile,
    "awning_green": paint_stripes((0.08, 0.36, 0.17)),
    "tearoff": paint_tearoff,
}


# ---------------------------------------------------------------------------------------------- packing

# ---------------------------------------------------------------------------------------------- the later rounds
# Each module of props added since has a painting module of its own beside it: paint_<name>.py with one function,
#
#     def register(a):            # a is this module: a.Layer, a.SWATCHES, a.WHITE, a.star_of_life, ...
#         return {swatch name: (colour, metallic, smoothness, grime)}, {decal name: painter returning a finished Layer}
#
# They are painted in this order after everything above, each from where the shared random stream then stands, so
# adding a module at the end (or a region at the end of the last) leaves every earlier region as it was.
EXTRA = ["civic", "service", "events", "everyday", "garage", "family", "site", "tow"]


def extra_modules():
    """The painting modules to use. PROPS_DEV=name[,name] limits them to those (working on one module without the
    others having to be whole); a module whose file is not there is skipped."""
    only = os.environ.get("PROPS_DEV")
    names = [n.strip() for n in only.split(",") if n.strip()] if only else EXTRA
    return [__import__("paint_" + name) for name in names if os.path.exists(os.path.join(HERE, "paint_" + name + ".py"))]


EXTRA.append("tech")       # the tech sector's buildings (paint_tech.py): a module of its own, painted after the others


def add_module(layers, module):
    """A painting module's regions (see EXTRA above), refused if it names one that exists."""
    swatches, decals = module.register(sys.modules[__name__])
    clash = [n for n in list(swatches) + list(decals) if n in layers or n in SWATCHES]
    if clash:
        raise SystemExit("%s names regions that exist already: %s" % (module.__name__, ", ".join(clash)))
    SWATCHES.update(swatches)
    layers.update({name: paint() for name, paint in decals.items()})
    layers.update({name: paint_swatch(name) for name in swatches})
    print("%s: %d regions, %d pixels with their gutters" % (module.__name__, len(swatches) + len(decals),
          sum((layers[n].width + 2 * PAD) * (layers[n].height + 2 * PAD) for n in list(swatches) + list(decals))))


class Picture:
    """A model's own painted picture (models.py --scan saved it) as a region: its colour, and its mask map's metallic,
    occlusion and smoothness from <id>_mask.png when the model has one, else its material's own two values."""

    def __init__(self, entry):
        self.colour = Image.open(entry["file"]).convert("RGB")
        self.width, self.height = self.colour.size
        self.mask = Image.open(entry["mask"]).convert("RGBA").resize(self.colour.size, Image.LANCZOS) if entry.get("mask") else None
        self.metal, self.smooth = entry.get("metallic", 0.0), entry.get("smoothness", 0.3)

    def images(self):
        if self.mask is not None:
            r, g, _, a = self.mask.split()
            return [self.colour, r, g, a]
        size = self.colour.size
        return [self.colour, Image.new("L", size, _byte(self.metal)), Image.new("L", size, 255), Image.new("L", size, _byte(self.smooth))]


def gather():
    """The regions of the set being built: name -> something with width, height and images()."""
    if sets.FIRST:
        layers = {"tape": paint_tape()}
        layers.update({name: paint() for name, paint in DECALS.items()})
        layers.update({name: paint_swatch(name) for name in SWATCHES if name not in LIFE_SWATCHES})
        layers.update({name: paint() for name, paint in LIFE_DECALS.items()})          # after the above: see LIFE_SWATCHES
        layers.update({name: paint_swatch(name) for name in LIFE_SWATCHES})
        for module in extra_modules():
            add_module(layers, module)
        return layers
    modules = [__import__("paint_" + name) for name in sets.SET.get("modules", [])]
    scanned = os.path.join(BUILD, "models.json")
    models = []
    if sets.SET.get("models"):
        if not os.path.exists(scanned):
            raise SystemExit("no %s: run models.py --scan first (build.ps1 does)" % scanned)
        with open(scanned, encoding="utf-8") as f:
            models = json.load(f)["models"]
    # The swatches its procedural modules may name (all of them), or only those its models name.
    wanted = set(SWATCHES) if modules else set()
    for model in models:
        unknown = [s for s in model["swatches"] if s not in SWATCHES and s not in MODEL_SWATCHES]
        if unknown:
            raise SystemExit("model %s names swatches the atlas does not have: %s (README, 'Adding a prop from Blender')" % (model["id"], ", ".join(unknown)))
        wanted.update(model["swatches"])
    SWATCHES.update({name: MODEL_SWATCHES[name] for name in sorted(wanted) if name in MODEL_SWATCHES and name not in SWATCHES})
    layers = {name: paint_swatch(name) for name in SWATCHES if name in wanted}
    for module in modules:
        add_module(layers, module)
    for model in models:
        for picture in model["pictures"]:
            layers[picture["region"]] = Picture(picture)
    return layers


def pack(layers, width, height):
    """Where each region goes in a width x height atlas, or (None, the first that does not fit). The tape is a row of
    its own with no gutter at its ends. The rest go, largest first, into the free rectangle each fits most tightly,
    which is then cut in two."""
    places = {"tape": (0, PAD)} if "tape" in layers else {}
    top = PAD + layers["tape"].height + PAD if "tape" in layers else 0
    free = [(0, top, width, height - top)]
    for name in sorted((n for n in layers if n != "tape"), key=lambda n: (-layers[n].height * layers[n].width, n)):
        w, h = layers[name].width + 2 * PAD, layers[name].height + 2 * PAD
        fits = [r for r in free if r[2] >= w and r[3] >= h]
        if not fits:
            return None, name
        fx, fy, fw, fh = min(fits, key=lambda r: min(r[2] - w, r[3] - h))
        free.remove((fx, fy, fw, fh))
        if fw - w > fh - h:
            free += [(fx + w, fy, fw - w, fh), (fx, fy + h, w, fh - h)]
        else:
            free += [(fx + w, fy, fw - w, h), (fx, fy + h, fw, fh - h)]
        places[name] = (fx + PAD, fy + PAD)
    return places, None


def sizes():
    """The sizes to try: the set's own, or for one that grows each power of two up to it, square then twice as wide."""
    if not sets.SET["atlas"].get("grows"):
        return [(WIDTH, HEIGHT)]
    out, w, h = [], SMALLEST, SMALLEST
    while w <= WIDTH and h <= HEIGHT:
        out.append((w, h))
        w, h = (w * 2, h) if w == h else (w, h * 2)
    return out


def write_empty():
    """A set with nothing in it ships no atlas at all: no video memory for nothing."""
    for name in ("props_base.png", "props_mask.png"):
        stale = os.path.join(BUILD, "textures", name)
        if os.path.exists(stale):
            os.remove(stale)
    os.makedirs(BUILD, exist_ok=True)
    with open(os.path.join(BUILD, "atlas.json"), "w") as f:
        json.dump({"width": 0, "height": 0, "used": 0, "swatches": [], "regions": {}}, f, indent=1)
    print("atlas: none, the set '%s' has no regions" % sets.NAME)


def build():
    global WIDTH, HEIGHT
    layers = gather()
    if not layers:
        write_empty()
        return
    missing = None
    for width, height in sizes():
        places, missing = pack(layers, width, height)
        if places is not None:
            WIDTH, HEIGHT = width, height
            break
    else:
        raise SystemExit("the atlas is full: no room for %s (the set '%s', %d x %d at most)" % (missing, sets.NAME, WIDTH, HEIGHT))

    base = np.full((HEIGHT, WIDTH, 3), 96, dtype=np.uint8)
    mask = np.zeros((HEIGHT, WIDTH, 4), dtype=np.uint8)
    mask[..., 1] = 255
    mask[..., 3] = 64
    rects = {}
    used = 0
    for name, layer in layers.items():
        px, py = places[name]
        images = layer.images()
        if name == "tape":
            images = [image.resize((WIDTH, layer.height), Image.LANCZOS) for image in images]
        colour, metal, ao, smooth = [np.asarray(image) for image in images]
        width = colour.shape[1]
        packed = np.dstack([metal, ao, np.zeros_like(metal), smooth])
        side = 0 if name == "tape" else PAD
        for target, source in ((base, colour), (mask, packed)):
            target[py - PAD:py + layer.height + PAD, px - side:px + width + side] = np.pad(source, ((PAD, PAD), (side, side), (0, 0)), mode="edge")
        rects[name] = [px / WIDTH, 1.0 - (py + layer.height) / HEIGHT, (px + width) / WIDTH, 1.0 - py / HEIGHT]
        used += (width + 2 * side) * (layer.height + 2 * PAD)

    os.makedirs(os.path.join(BUILD, "textures"), exist_ok=True)
    Image.fromarray(base, "RGB").save(os.path.join(BUILD, "textures", "props_base.png"))
    Image.fromarray(mask, "RGBA").resize((WIDTH // MASK_DIVISOR, HEIGHT // MASK_DIVISOR), Image.BOX).save(os.path.join(BUILD, "textures", "props_mask.png"))
    percent = round(100 * used / (WIDTH * HEIGHT))
    with open(os.path.join(BUILD, "atlas.json"), "w") as f:
        json.dump({"width": WIDTH, "height": HEIGHT, "used": percent, "swatches": sorted(SWATCHES), "regions": rects}, f, indent=1)
    print("atlas %d x %d, %d regions, %d %% used" % (WIDTH, HEIGHT, len(rects), percent))


if __name__ == "__main__":
    build()
