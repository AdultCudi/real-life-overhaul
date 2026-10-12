"""Paints the road markings the mod lays as decals (HDRP decal projectors, the game's own kind of road paint):

    build/textures/decal_bus_stop.png   512 x 1024, the words BUS STOP for a bus stop's zone, read by a driver coming up
                                        to it: BUS nearest (the bottom of the picture), STOP beyond, the letters drawn
                                        long, as road lettering is, so that they read at a low angle. 2.0 x 5.0 m on the road.
    build/textures/decal_dash.png       32 x 256, one dash and its gap of the broken line round the zone, tiled along it:
                                        1.0 m of paint and 0.5 m of road. Its width is the line's width, as with the
                                        lettering the picture's height runs along the road.

Yellow road paint, worn: the colour is the paint's, the alpha how much of it is left (the road shows through where it
is thin). Drawn with Pillow's own scalable font (Aileron, CC0) as the atlas is; nothing is read from disk.
Run with Blender's Python: python decals.py
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_tools", "pylib"))

import numpy as np  # noqa: E402
from PIL import Image, ImageDraw, ImageFilter, ImageFont  # noqa: E402

BUILD = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "build")
PAINT = (232, 186, 38)          # road yellow, a little dulled by traffic
SS = 4                          # drawn at four times the size, then reduced: smooth edges


def wear(alpha, seed, strength=0.22, scale=6):
    """Paint worn by tyres: fine speckle and broader thin patches taken off the alpha."""
    rng = np.random.default_rng(seed)
    h, w = alpha.shape
    fine = rng.random((h, w))
    coarse = np.asarray(Image.fromarray((rng.random((h // scale + 2, w // scale + 2)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float64) / 255.0
    keep = 1.0 - strength * (0.55 * coarse + 0.45 * fine)
    keep = np.where(fine < 0.035, keep * 0.35, keep)        # chips
    return np.clip(alpha * keep, 0.0, 1.0)


def rgba(alpha):
    out = np.zeros(alpha.shape + (4,), dtype=np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = PAINT
    out[..., 3] = (alpha * 255.0 + 0.5).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def word(draw, text, box, stretch, spacing=0.16, weight=0.02):
    """One word as large as fits the box (x0, y0, x1, y1, in drawn pixels), letter by letter with a clear gap between
    them (spacing, a share of the size), its letters stretched upwards `stretch` times."""
    x0, y0, x1, y1 = box
    width, height = x1 - x0, y1 - y0
    size = int(height / stretch)
    while size > 8:
        font = ImageFont.load_default(size)
        stroke = max(1, round(size * weight))
        boxes = [draw.textbbox((0, 0), letter, font=font, stroke_width=stroke) for letter in text]
        gap = spacing * size
        total = sum(b[2] - b[0] for b in boxes) + gap * (len(text) - 1)
        tall = max(b[3] for b in boxes) - min(b[1] for b in boxes)
        if total <= width:
            break
        size -= 4
    top = min(b[1] for b in boxes)
    layer = Image.new("L", (int(total) + 8, int(tall) + 8), 0)
    pen = ImageDraw.Draw(layer)
    x = 4.0
    for letter, b in zip(text, boxes):
        pen.text((x - b[0], 4 - top), letter, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
        x += (b[2] - b[0]) + gap
    layer = layer.resize((layer.width, int(layer.height * stretch)), Image.BICUBIC)
    return layer


def bus_stop():
    w, h = 512 * SS, 1024 * SS
    canvas = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(canvas)
    margin = 18 * SS
    # The two words, each in a band of the picture: BUS at the bottom (nearest the driver), STOP above, a gap between.
    for text, band in (("BUS", (h * 0.56, h - margin)), ("STOP", (margin, h * 0.44))):
        glyphs = word(draw, text, (margin, band[0], w - margin, band[1]), 2.6)
        if glyphs.height > band[1] - band[0]:
            glyphs = glyphs.resize((glyphs.width, int(band[1] - band[0])), Image.BICUBIC)
        canvas.paste(glyphs, (int((w - glyphs.width) / 2), int(band[0] + (band[1] - band[0] - glyphs.height) / 2)), glyphs)
    canvas = canvas.filter(ImageFilter.GaussianBlur(SS * 0.6)).resize((512, 1024), Image.LANCZOS)
    alpha = np.asarray(canvas, dtype=np.float64) / 255.0
    return rgba(wear(alpha, 11) * 0.92)


def dash():
    w, h = 32 * SS, 256 * SS
    canvas = Image.new("L", (w, h), 0)
    paint = int(h * 2 / 3)
    ImageDraw.Draw(canvas).rectangle([2 * SS, 0, w - 2 * SS, paint], fill=255)
    canvas = canvas.filter(ImageFilter.GaussianBlur(SS * 0.5)).resize((32, 256), Image.LANCZOS)
    alpha = np.asarray(canvas, dtype=np.float64) / 255.0
    return rgba(wear(alpha, 23, strength=0.18, scale=4) * 0.92)


def main():
    out = os.path.join(BUILD, "textures")
    os.makedirs(out, exist_ok=True)
    for name, image in (("decal_bus_stop", bus_stop()), ("decal_dash", dash())):
        image.save(os.path.join(out, name + ".png"))
        print("decal %s %dx%d" % (name, image.width, image.height))


if __name__ == "__main__":
    main()
