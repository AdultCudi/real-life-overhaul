"""The interface's typeface, from its pinned source to what the build and the mod read.

    python -I UiAssets/scripts/fonts.py            # build\fonts and Scripts/Ui/UiFontMetrics.Data.cs
    python -I UiAssets/scripts/fonts.py --check    # fails if the committed metrics differ from the fonts

The interface is set in Overpass, the open face Cities: Skylines II uses (the owner's choice of direction, 1 Nov 2026,
design/DIRECTION_CS2.md): Regular, SemiBold and Bold, as Red Hat publishes them (static fonts, SIL Open Font License 1.1,
OFL-Overpass.txt beside them). Each weight is cut here into UiAssets/build/fonts with its figures made tabular, since
TextMeshPro applies no OpenType features and a column of figures must line up, with the list of the characters its atlas
holds: the scripts of the mod's eleven languages that the face covers (Latin, Latin Extended and Cyrillic).

The same pass writes the faces' metrics (units per em, line metrics, every listed character's advance) into
Scripts/Ui/UiFontMetrics.Data.cs. The kit lays text out with those numbers in the game and in EconomyCheck alike, so
a label that the offline check says fits is measured by the very table the game uses. Kerning is left out on purpose:
it only ever narrows a line, so a width without it is the safe upper bound.

Needs Python 3 with fontTools (pip install fonttools). Nothing here touches the game.
"""
import hashlib
import io
import os
import sys

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
SOURCES = os.path.join(ROOT, "UiAssets", "fonts")
BUILD = os.path.join(ROOT, "UiAssets", "build", "fonts")
METRICS = os.path.join(ROOT, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "UiFontMetrics.Data.cs")

# Red Hat's Overpass repository at the commit the files were taken from, and their SHA-256: a changed file is a new font,
# made on purpose. "tabular" swaps the figures for their tabular cuts in the character map.
FAMILIES = [
    {"name": "Overpass", "licence": "OFL-Overpass", "tabular": True,
     "from": "RedHatOfficial/Overpass c580d28bfab7f39013568e684a65eeb23eff588d fonts/ttf",
     "files": {400: ("Overpass-Regular.ttf", "bf107d7ae99031c2193cd187acc9d4e5ebd45b6ea0e6845159ce68f87eb623aa"),
               600: ("Overpass-SemiBold.ttf", "d72444ce96344d20cb4b0798dde08c45a8fd22b152e7dec03fc8da5d58ef1e9f"),
               700: ("Overpass-Bold.ttf", "623f1f421fecc29095d76b3aa472c409e1b0b5db70ef2b3ba4b699546ce3d4af")},
     "weights": [(400, "Regular"), (600, "SemiBold"), (700, "Bold")]},
]

# The blocks an atlas takes what its face has of: the languages' letters, their punctuation, currency and the few
# signs the interface writes (x, minus, arrows where a face draws them). Vietnamese (1E00 to 1EFF) is not one of them.
RANGES = [
    (0x0020, 0x007E), (0x00A0, 0x00FF), (0x0100, 0x017F), (0x0180, 0x024F), (0x02C6, 0x02DD),
    (0x0370, 0x03FF), (0x0400, 0x04FF), (0x2000, 0x206F), (0x20A0, 0x20CF), (0x2100, 0x214F),
    (0x2190, 0x21FF), (0x2200, 0x22FF), (0x25A0, 0x25FF),
]


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def wanted(code):
    return any(lo <= code <= hi for lo, hi in RANGES)


def cut(family):
    """Each weight as a static font in build\\fonts, and the face's characters; returns [(weight, path, font)]."""
    made = []
    for weight, style in family["weights"]:
        path = os.path.join(BUILD, "%s-%s.ttf" % (family["name"], style))
        if "files" in family:
            name, pinned = family["files"][weight]
            source = os.path.join(SOURCES, name)
            if sha256(source) != pinned:
                raise SystemExit("FONTS: %s is not the pinned file" % name)
            font = TTFont(source)
            if family.get("tabular"):
                tabular(font)
            font.save(path)
        else:
            source = os.path.join(SOURCES, family["file"])
            got = sha256(source)
            if got != family["sha256"]:
                raise SystemExit("FONTS: %s is not the pinned file (%s, wanted %s)" % (family["file"], got, family["sha256"]))
            axes = dict(family.get("pin", {}))
            axes["wght"] = weight
            static = instancer.instantiateVariableFont(TTFont(source), axes, updateFontNames=True)
            static.save(path)
        made.append((weight, path, TTFont(path)))
    return made


def tabular(font):
    """Writes the face's tnum substitutions into its character map, so its default figures are the tabular ones."""
    swaps = {}
    gsub = font["GSUB"].table
    for record in gsub.FeatureList.FeatureRecord:
        if record.FeatureTag != "tnum":
            continue
        for index in record.Feature.LookupListIndex:
            for sub in gsub.LookupList.Lookup[index].SubTable:
                swaps.update(getattr(sub, "mapping", None) or {})
    if not swaps:
        raise SystemExit("FONTS: the face has no tabular figures to swap in")
    for table in font["cmap"].tables:
        for code, glyph in list(table.cmap.items()):
            if glyph in swaps:
                table.cmap[code] = swaps[glyph]


def characters(font):
    cmap = font.getBestCmap()
    return sorted(c for c in cmap if wanted(c) and c <= 0xFFFF)


def face(font, codes):
    hhea, os2, head = font["hhea"], font["OS/2"], font["head"]
    cmap, hmtx = font.getBestCmap(), font["hmtx"]
    advances = [hmtx[cmap[c]][0] for c in codes]
    return {
        "upem": head.unitsPerEm,
        "ascender": hhea.ascent,
        "descender": hhea.descent,
        "gap": hhea.lineGap,
        "cap": getattr(os2, "sCapHeight", 0),
        "x": getattr(os2, "sxHeight", 0),
        "advances": advances,
    }


def array(name, values, per_line=24):
    lines = []
    for i in range(0, len(values), per_line):
        lines.append("            " + ", ".join(str(v) for v in values[i:i + per_line]) + ",")
    return "        private static readonly ushort[] %s =\n        {\n%s\n        };\n" % (name, "\n".join(lines))


def write_metrics(families):
    out = io.StringIO()
    out.write("// Generated by UiAssets/scripts/fonts.py from the pinned fonts in UiAssets/fonts: do not edit by hand. Run the\n")
    out.write("// script again when a font or RANGES changes, and commit this file with it (fonts.py --check holds them together).\n")
    out.write("namespace RealLifeOverhaul.Ui\n{\n")
    out.write("    internal static partial class UiFontMetrics\n    {\n")
    for fam in families:
        out.write(array(fam["name"] + "Codes", fam["codes"]))
        for weight, data in fam["faces"]:
            out.write(array("%s%dAdvances" % (fam["name"], weight), data["advances"]))
    out.write("\n        // Each face: family, weight, units per em, ascender, descender (negative), line gap, cap height, x height.\n")
    out.write("        private static readonly UiFace[] Faces =\n        {\n")
    for fam in families:
        for weight, data in fam["faces"]:
            out.write("            new UiFace(UiFamily.%s, %d, %d, %d, %d, %d, %d, %d, %sCodes, %s%dAdvances),\n" % (
                fam["name"], weight, data["upem"], data["ascender"], data["descender"], data["gap"], data["cap"], data["x"],
                fam["name"], fam["name"], weight))
    out.write("        };\n    }\n}\n")
    return out.getvalue().replace("\n", "\r\n")


def main():
    check = "--check" in sys.argv
    os.makedirs(BUILD, exist_ok=True)
    families = []
    for family in FAMILIES:
        made = cut(family)
        codes = characters(made[0][2])
        for weight, path, font in made[1:]:
            if characters(font) != codes:
                raise SystemExit("FONTS: %s's weights do not cover the same characters" % family["name"])
        with open(os.path.join(BUILD, family["name"] + ".chars.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write(" ".join("%04X" % c for c in codes) + "\n")
        families.append({"name": family["name"], "codes": codes, "faces": [(w, face(font, codes)) for w, path, font in made]})
        print("font %s: %d characters, weights %s" % (family["name"], len(codes), ", ".join(str(w) for w, _, _ in made)))
    text = write_metrics(families)
    if check:
        with open(METRICS, "r", encoding="utf-8-sig", newline="") as f:
            if f.read() != text:
                raise SystemExit("FONTS: Scripts/Ui/UiFontMetrics.Data.cs is not what the fonts give: run fonts.py and commit it")
        print("FONTS: metrics as the fonts give them")
        return
    with open(METRICS, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print("metrics: " + os.path.relpath(METRICS, ROOT))


if __name__ == "__main__":
    main()
