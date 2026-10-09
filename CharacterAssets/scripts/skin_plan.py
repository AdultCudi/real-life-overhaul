"""Plans and sheets for the skin finish (Scripts/Character/SkinFinish.cs), judged in the HDRP preview.

    python -I skin_plan.py plan <plan name> <out.json>
    python -I skin_plan.py sheets <pylib> <shots folder> <out prefix>         one sheet per who and view: tones down, variants across
    python -I skin_plan.py differ <pylib> <shots folder>                      how far each variant is from the game's look, in numbers

A shot is named <who>_<tone>_<stage>_<view><yaw>_<variant>. The preview's skin is a stand-in (HDRP/Lit on the
game's textures: the same mask and normal maps read the same way, but not the game's own shader), so these sheets
say what a change of the maps does, before against after; the game has the last word.

What was tried on the way (rounds one to five, October 2026; the options that did nothing are gone from the code):
  smoothness x1.6: wet plastic.  x0.6: chalk, and the lips go dead.  x0.9 light to x0.75 dark: kept.
  occlusion to the power 1.8 and 2.5: under 0.3 grey levels of change on average: the map only reaches ambient light.
  creases five texels apart at strength 0.5: reptile scales.  At strength 0.12 to 0.2: a net one can read as lines.
  creases 2.5 texels apart at 0.12 (0.06 on the body): a grain; with the smoothness lowered in them (0.3): kept.
  a broader unevenness of the normal map (bumps a dozen texels across) at 0.3 and 0.5: a skin disease.
"""
import glob
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from preview_plan import CROPS, FEMALE, MALE, shot  # noqa: E402

CROPS.update({"torso": [900, 900]})
MAN = dict(MALE, parts=["Eyebrows/Eyebrows14", "Torso/Shirt", "Legs/ShortJeans", "Feet/Sneakers03"], cloth={"Torso": "B8B4AA", "Legs": "3A4A66"})
WOMAN = dict(FEMALE, parts=["Eyebrows/Eyebrows04", "Torso/Shirt", "Legs/ShortJeans", "Feet/Sneakers"], cloth={"Torso": "B8B4AA", "Legs": "3A4A66"})
BARE = dict(MALE, parts=["Eyebrows/Eyebrows14", "Torso/NakedTorso", "Legs/ShortJeans", "Feet/Sneakers03"], cloth={"Legs": "3A4A66"})

GAME = ("0game", [])
SHIPPED = ("1finished", ["skin"])          # SkinFinish.Settings.Default

# What a sheet shows of each view: the part of the shot (x, y, w, h) and how much it is enlarged.
CUTS = {"creation_close0": ((300, 250, 460, 500), 1.3), "creation_close60": ((230, 230, 460, 500), 1.3), "creation_torso0": ((150, 0, 600, 760), 1.0),
        "city_head0": ((40, 40, 120, 120), 4.0), "city_game0": ((60, 60, 180, 300), 2.0)}


def variants(prefix, who, hair, entries, tones=("light", "dark"), views=(("creation", "close", 0),), hair_colour="brown"):
    shots = []
    for tone in tones:
        for stage, view, yaw in views:
            for label, finish in entries:
                shots.append(shot("%s_%s_%s_%s%d_%s" % (prefix, tone, stage, view, yaw, label), who, hair, hair_colour, tone, stage, view, yaw, finish))
    return shots


def skin_same():
    """The copies themselves: with (next to) nothing changed they must draw as the game's maps do, compressed or not."""
    entries = (GAME, ("1plain", ["skin:smooth=1,dark=1,grain=0,pores=0.0001,body=0.0001,compress=0"]),
               ("2packed", ["skin:smooth=1,dark=1,grain=0,pores=0.0001,body=0.0001"]),
               ("3mask", ["skin:smooth=0.999,dark=0.999,grain=0,pores=0,body=0"]))
    return variants("m", MAN, "MaleHair04", entries, tones=("light",))


def skin_tried():
    """The rejected ends of each handle, for the record."""
    entries = (GAME, ("1gloss", ["skin:smooth=1.6,dark=1.6,pores=0,body=0"]), ("2chalk", ["skin:smooth=0.6,dark=0.6,pores=0,body=0"]),
               ("3scales", ["skin:smooth=1,dark=1,pores=0.5,body=0.3,scale=1,grain=0"]), ("4net", ["skin:smooth=1,dark=1,pores=0.2,body=0.1,scale=1,grain=0.6"]),
               SHIPPED)
    return variants("m", MAN, "MaleHair04", entries)


def skin_report():
    """The sheets kept in evidence: the game's skin against the finished one, four tones, both sexes, close up from
    two sides, at play distance, and bare-chested (where the head's skin meets the body's)."""
    entries = (GAME, SHIPPED)
    tones = ("pale", "light", "mid", "dark")
    return (variants("m", MAN, "MaleHair04", entries, tones, (("creation", "close", 0), ("creation", "close", 60), ("city", "head", 0), ("city", "game", 0)))
            + variants("f", WOMAN, "FemHair02", entries, tones, (("creation", "close", 0), ("city", "head", 0)))
            + variants("bare", BARE, "MaleHair04", entries, ("light", "dark"), (("creation", "torso", 0),)))


PLANS = {"skin_same": skin_same, "skin_tried": skin_tried, "skin_report": skin_report}


def groups_of(folder):
    """{(who, view): {tone: [(variant, path), ...]}} in name order."""
    groups = {}
    for path in sorted(glob.glob(os.path.join(folder, "*.png"))):
        who, tone, stage, view, label = os.path.basename(path)[:-4].split("_", 4)
        groups.setdefault((who, stage + "_" + view), {}).setdefault(tone, []).append((label, path))
    return groups


def sheets(pylib, folder, prefix):
    sys.path.insert(0, pylib)
    from PIL import Image, ImageDraw
    for (who, view), tones in groups_of(folder).items():
        (x, y, w, h), scale = CUTS.get(view, ((0, 0, 100000, 100000), 1.0))
        rows = []
        for tone, items in tones.items():
            cells = []
            for label, path in items:
                image = Image.open(path).convert("RGB")
                image = image.crop((x, y, min(image.width, x + w), min(image.height, y + h)))
                image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS if scale < 1 else Image.BICUBIC)
                cells.append(("%s %s  %s" % (who, tone, label), image))
            rows.append(cells)
        # Few variants: the tones side by side, each a before and after pair. Many: one row per tone.
        if all(len(cells) == 2 for cells in rows) and len(rows) > 2:
            rows = [rows[i][:] + rows[i + 1][:] for i in range(0, len(rows) - 1, 2)] + ([rows[-1]] if len(rows) % 2 else [])
        width, height = rows[0][0][1].width, rows[0][0][1].height + 16
        columns = max(len(cells) for cells in rows)
        sheet = Image.new("RGB", (columns * width + (columns - 1) * 4, len(rows) * height + (len(rows) - 1) * 4), (24, 24, 24))
        draw = ImageDraw.Draw(sheet)
        for r, cells in enumerate(rows):
            for c, (label, image) in enumerate(cells):
                sheet.paste(image, (c * (width + 4), r * (height + 4) + 16))
                draw.text((c * (width + 4) + 3, r * (height + 4) + 2), label, fill=(235, 235, 235))
        out = "%s_%s_%s.jpg" % (prefix, who, view)
        sheet.save(out, quality=93)
        print("sheet", out, sheet.size)


def differ(pylib, folder):
    """Numbers beside the pictures: a change the eye is unsure of is measured here (grey levels of 255)."""
    sys.path.insert(0, pylib)
    from PIL import Image, ImageChops, ImageStat
    for (who, view), tones in groups_of(folder).items():
        for tone, items in tones.items():
            base = Image.open(items[0][1]).convert("RGB")
            for label, path in items[1:]:
                difference = ImageChops.difference(base, Image.open(path).convert("RGB")).convert("L")
                stat = ImageStat.Stat(difference)
                changed = sum(difference.histogram()[7:]) * 100.0 / (difference.width * difference.height)
                print("%-5s %-17s %-6s %-10s mean %.2f  largest %3d  pixels changed by more than 6: %.1f%%"
                      % (who, view, tone, label, stat.mean[0], stat.extrema[0][1], changed))


if __name__ == "__main__":
    if sys.argv[1] == "plan":
        import json
        shots = PLANS[sys.argv[2]]()
        with open(sys.argv[3], "w") as f:
            json.dump({"width": 1920, "height": 1080, "shots": shots}, f, indent=1)
        print("plan", sys.argv[2], len(shots), "shots")
    elif sys.argv[1] == "sheets":
        sheets(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        differ(sys.argv[2], sys.argv[3])
