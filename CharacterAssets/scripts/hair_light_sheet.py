"""Lays out and measures the shots of hair against a strong light (plans hair_light and hair_light_measure):

    python -I hair_light_sheet.py <pylib> <pictures folder> <measure folder> <out folder>

Pictures (the warm lamp, tone-mapped) become one sheet per sex and skin: per hairstyle three rows (the game's own
hair, the finish as it was before 11 October, the finish as it is), per row four hair colours, each with the lamp
behind the head as the camera sees it (150 degrees round), beside it (90) and at the camera's side (20). Tiles are
true game pixels of the third-person view, enlarged twice.

The measure shots (a white lamp, no tone mapping: a pixel is the light sent back) become measured.txt: see ABOUT.
"""
import os
import re
import sys

pylib, pictures, measures, out = sys.argv[1:5]
sys.path.insert(0, pylib)

import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

FINISHES = (("0game", "game"), ("1before", "before"), ("2after", "after"))
COLOURS = ("black", "brown", "blond", "grey")
ANGLES = ("150", "090", "020")
BOX = (68, 60, 132, 124)        # the head in a 200 px shot
SCALE = 2
NAME = re.compile(r"([mf])_(\w+?)_(\w+)-(\w+)_a(\d+)_(\w+)\.png$")

ABOUT = """Hair against a lamp, measured in the HDRP preview (hair_light.ps1): a white lamp of 900 candela 3 m from the head,
no tone mapping, so a pixel is the light sent back. Every number is a share of LIT SKIN: the middle of the bald head
lit from the camera's side.

"Hair" is where the head with hair differs from the bald head (taken from the black, lit-from-the-camera shot of
each style, so thin hair with skin showing through it counts as hair).
  mean  how bright the hair is on average. Lit from the camera's side this is the hair's own colour; black hair that
        covers the skull reads about 0.05 to 0.15, and more where skin shows through it.
  glow  the brightest hundredth of the hair, counting only the light it adds to what the bald head shows at the same
        spot under the same lamp (so skin seen through thin hair is not counted). With the lamp behind or beside the
        head this is the hair lighting up against the lamp: the fault.
"""

os.makedirs(out, exist_ok=True)


def shots(folder):
    found = {}
    for name in sorted(os.listdir(folder)):
        match = NAME.match(name)
        if match:
            found[match.groups()] = os.path.join(folder, name)
    return found


# ---------------------------------------------------------------- the sheets

def sheets():
    found = shots(pictures)
    tile = (BOX[2] - BOX[0]) * SCALE
    for sex in ("m", "f"):
        for skin in sorted({k[3] for k in found}):
            styles = sorted({k[1] for k in found if k[0] == sex and k[3] == skin and k[1] != "none"})
            if not styles:
                continue
            label = 74
            sheet = Image.new("RGB", (label + tile * len(COLOURS) * len(ANGLES), 14 + len(styles) * len(FINISHES) * tile), (24, 24, 24))
            draw = ImageDraw.Draw(sheet)
            for c, colour in enumerate(COLOURS):
                for a, angle in enumerate(ANGLES):
                    where = {"150": "behind", "090": "beside", "020": "camera"}[angle]
                    draw.text((label + (c * len(ANGLES) + a) * tile + 2, 1), "%s, lamp %s" % (colour, where), fill=(230, 230, 230))
            for s, style in enumerate(styles):
                for f, (finish, word) in enumerate(FINISHES):
                    y = 14 + (s * len(FINISHES) + f) * tile
                    draw.text((2, y + 2), style, fill=(230, 230, 230))
                    draw.text((2, y + 14), word, fill=(150, 200, 255) if word == "after" else (200, 200, 200))
                    for c, colour in enumerate(COLOURS):
                        for a, angle in enumerate(ANGLES):
                            path = found.get((sex, style, colour, skin, angle, finish))
                            if path:
                                image = Image.open(path).convert("RGB").crop(BOX).resize((tile, tile), Image.NEAREST)
                                sheet.paste(image, (label + (c * len(ANGLES) + a) * tile, y))
            sheet.save(os.path.join(out, "%s_%s_skin.jpg" % ("men" if sex == "m" else "women", skin)), quality=90)


# ---------------------------------------------------------------- the numbers

def linear(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(np.float64) / 255.0
    return np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)


def luma(l):
    return 0.2126 * l[..., 0] + 0.7152 * l[..., 1] + 0.0722 * l[..., 2]


def measure():
    found = shots(measures)
    yy, xx = np.mgrid[0:200, 0:200]
    middle = ((xx - 100) / 11.0) ** 2 + ((yy - 90) / 11.0) ** 2 <= 1.0
    lines = [ABOUT]
    worst = {}
    for sex in ("m", "f"):
        bald = found.get((sex, "none", "black", "light", "020", "0game"))
        if not bald:
            continue
        bald_l = linear(bald)
        skin = luma(bald_l)[middle].mean()
        bare = {angle: luma(linear(found[(sex, "none", "black", "light", angle, "0game")])) for angle in ANGLES}
        lines.append("%s: lit skin = %.3f (pixel value of the bald head's middle, lamp at the camera's side)" % ("MEN" if sex == "m" else "WOMEN", skin))
        lines.append("%-22s %-7s %-7s | lamp behind: mean glow | beside: mean glow | camera side: mean glow" % ("style", "hair", "finish"))
        for style in sorted({k[1] for k in found if k[0] == sex and k[1] != "none"}):
            mask_shot = found.get((sex, style, "black", "light", "020", "2after"))
            if not mask_shot:
                continue
            hair = np.abs(linear(mask_shot) - bald_l).sum(axis=2) > 0.02
            if hair.sum() < 30:
                continue
            for colour in COLOURS:
                for finish, word in FINISHES:
                    cells = []
                    for angle in ANGLES:
                        path = found.get((sex, style, colour, "light", angle, finish))
                        if not path:
                            cells.append("    -     -")
                            continue
                        value = luma(linear(path))[hair]
                        mean, top = value.mean() / skin, np.percentile(np.maximum(0.0, value - bare[angle][hair]), 99) / skin
                        cells.append("%5.2f %5.2f" % (mean, top))
                        if angle != "020":
                            key = (word, colour)
                            worst[key] = max(worst.get(key, (0, 0, "")), (top, mean, "%s %s" % (style, "behind" if angle == "150" else "beside")))
                    lines.append("%-22s %-7s %-7s |        %s |    %s |         %s" % (style, colour, word, cells[0], cells[1], cells[2]))
            lines.append("")
    lines.append("The glow with the lamp behind or beside the head, worst style per colour (share of lit skin):")
    for colour in COLOURS:
        row = "  %-6s" % colour
        for _, word in FINISHES:
            top, mean, where = worst.get((word, colour), (0, 0, "-"))
            row += "  %s %.2f (%s)" % (word, top, where)
        lines.append(row)
    with open(os.path.join(out, "measured.txt"), "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines[-6:]))


if os.path.isdir(pictures):
    sheets()
if os.path.isdir(measures):
    measure()
