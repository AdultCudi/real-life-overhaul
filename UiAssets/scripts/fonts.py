"""The interface's two typefaces, from their pinned sources to what the build and the mod read.

    python -I UiAssets/scripts/fonts.py            # build\\fonts and Scripts/Ui/UiFontMetrics.Data.cs
    python -I UiAssets/scripts/fonts.py --check    # fails if the committed metrics differ from the fonts
    python -I UiAssets/scripts/fonts.py --themes   # the theme study's faces (UiAssets/fonts/themes), for the previews alone

Sora and Manrope ship in UiAssets/fonts as Google Fonts publishes them (variable fonts, SIL Open Font License 1.1,
OFL-*.txt beside them). TextMeshPro rasterises a variable font at its default weight only, so each weight the type
scale names is cut here as a static font (fontTools' instancer) into UiAssets/build/fonts, with the list of the
characters its atlas holds: the scripts of the mod's eleven languages that the face covers (Latin and Latin Extended
in both; Cyrillic and Greek in Manrope alone, so Sora's Cyrillic falls back to Manrope of the same weight in the mod).

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

# google/fonts at the commits the files were taken from, and their SHA-256: a changed file is a new font, made on purpose.
FAMILIES = [
    {
        "name": "Sora",
        "file": "Sora[wght].ttf",
        "sha256": "84ff7096ae3ec6c8be47d906d1a0ba4de7f2ce78c615275c77301964a316e16c",
        "from": "google/fonts a926665019d3f7f25c8b1212cecbfa871e70de82 ofl/sora",
        "weights": [(600, "SemiBold"), (700, "Bold"), (800, "ExtraBold")],
    },
    {
        "name": "Manrope",
        "file": "Manrope[wght].ttf",
        "sha256": "3ae11c49db0455a3cc33e37d380f20fdb8c7f8b41dc07625c177e3d87a9d6ae6",
        "from": "google/fonts b31870aff700ab7a1d74fa0c6887d95beb9e0037 ofl/manrope",
        "weights": [(600, "SemiBold"), (700, "Bold"), (800, "ExtraBold")],
    },
]

# The theme study's faces (1 Nov 2026): three directions rendered for the owner to choose from, never shipped as they
# stand. Their cuts and lists go to build\fonts with the rest, their metrics to the preview's own UiThemeMetrics.cs, and
# themes.json tells the Unity builder which faces make the study's bundle. "pin" holds the axes a face is cut at besides
# its weight (Source Serif's optical size: the display cut for headlines, the text cut for reading); "files" names the
# static fonts of a family published without a variable font.
THEMES_FROM = "google/fonts 6c1dbe27dcf7d817d4b62cec033ba77fa8e2a5ec"
THEME_SOURCES = os.path.join(SOURCES, "themes")
THEME_METRICS = os.path.join(ROOT, "RealLifeOverhaul.Build", "UiPreview", "UiThemeMetrics.cs")
THEME_FAMILIES = [
    {"name": "SerifDisplay", "file": "SourceSerif4[opsz,wght].ttf", "licence": "OFL-SourceSerif4",
     "sha256": "97b2d4da6e3cb494b5a1e66ae176914d852ccabef49e0c02c0df25f3e39aca0b", "pin": {"opsz": 48},
     "weights": [(600, "SemiBold"), (700, "Bold")]},
    {"name": "Serif", "file": "SourceSerif4[opsz,wght].ttf", "licence": "OFL-SourceSerif4",
     "sha256": "97b2d4da6e3cb494b5a1e66ae176914d852ccabef49e0c02c0df25f3e39aca0b", "pin": {"opsz": 16},
     "weights": [(500, "Medium"), (700, "Bold")]},
    {"name": "Franklin", "file": "LibreFranklin[wght].ttf", "licence": "OFL-LibreFranklin",
     "sha256": "2329f394b10ed1c71107df20fddc11e2bb1b68c1ecbb385f975b36fd64a971f8",
     "weights": [(600, "SemiBold"), (700, "Bold"), (800, "ExtraBold")]},
    {"name": "PlexMono", "licence": "OFL-IBMPlexMono",
     "files": {500: ("IBMPlexMono-Medium.ttf", "a9b4c49bb299e05b5f6c481e7fb5e78943d2793249a0c8874ab574a2d1ea6755"),
               600: ("IBMPlexMono-SemiBold.ttf", "d3c38e55c78f5b0f28009fddba4834ec503278936a5986032424c9bd2d23aa46"),
               700: ("IBMPlexMono-Bold.ttf", "ac27abd6450a64dd94467580a02fe6235156d5b92f2926ebbc8e7489df64e0be")},
     "weights": [(500, "Medium"), (600, "SemiBold"), (700, "Bold")]},
    {"name": "PlexSans", "file": "IBMPlexSans[wdth,wght].ttf", "licence": "OFL-IBMPlexSans",
     "sha256": "3b031aa4216174205bd8471f88a49b91f093169e9e87bd5262242bc5967fe2e3", "pin": {"wdth": 100},
     "weights": [(500, "Medium"), (600, "SemiBold"), (700, "Bold")]},
    {"name": "Rubik", "file": "Rubik[wght].ttf", "licence": "OFL-Rubik",
     "sha256": "1b3a7437ba2af80e465e773ed60c5036d1ba6ace492d89046dbcf18fb31e4e88",
     "weights": [(500, "Medium"), (600, "SemiBold"), (700, "Bold"), (800, "ExtraBold")]},
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


def cut(family, sources=SOURCES):
    """Each weight as a static font in build\\fonts, and the face's characters; returns [(weight, path, font)]."""
    made = []
    for weight, style in family["weights"]:
        path = os.path.join(BUILD, "%s-%s.ttf" % (family["name"], style))
        if "files" in family:
            name, pinned = family["files"][weight]
            source = os.path.join(sources, name)
            if sha256(source) != pinned:
                raise SystemExit("FONTS: %s is not the pinned file" % name)
            TTFont(source).save(path)
        else:
            source = os.path.join(sources, family["file"])
            got = sha256(source)
            if got != family["sha256"]:
                raise SystemExit("FONTS: %s is not the pinned file (%s, wanted %s)" % (family["file"], got, family["sha256"]))
            axes = dict(family.get("pin", {}))
            axes["wght"] = weight
            static = instancer.instantiateVariableFont(TTFont(source), axes, updateFontNames=True)
            static.save(path)
        made.append((weight, path, TTFont(path)))
    return made


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


def themes():
    """The theme study's faces: cuts and lists in build\\fonts, the Unity builder's themes.json, the preview's metrics."""
    import json
    os.makedirs(BUILD, exist_ok=True)
    families, faces, licences = [], [], []
    for family in THEME_FAMILIES:
        made = cut(family, THEME_SOURCES)
        codes = characters(made[0][2])
        with open(os.path.join(BUILD, family["name"] + ".chars.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write(" ".join("%04X" % c for c in codes) + "\n")
        families.append({"name": family["name"], "codes": codes, "faces": [(w, face(font, codes)) for w, path, font in made]})
        for (weight, style) in family["weights"]:
            faces.append({"name": "%s-%s" % (family["name"], style), "family": family["name"], "weight": weight})
        if family["licence"] not in licences:
            licences.append(family["licence"])
        print("theme font %s: %d characters, weights %s" % (family["name"], len(codes), ", ".join(str(w) for w, _, _ in made)))
    spec = {"bundle": "reallife_ui_themes", "faces": faces, "licences": licences, "from": THEMES_FROM}
    with open(os.path.join(BUILD, "themes.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(spec, f, indent=2)
    text = write_metrics(families).replace("internal static partial class UiFontMetrics", "internal static class UiThemeMetrics")
    text = text.replace("private static readonly UiFace[] Faces", "public static readonly UiFace[] Faces")
    text = text.replace("namespace RealLifeOverhaul.Ui\r\n{", "using RealLifeOverhaul.Ui;\r\n\r\nnamespace RealLifeOverhaul.Preview\r\n{")
    with open(THEME_METRICS, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print("theme metrics: " + os.path.relpath(THEME_METRICS, ROOT))


def main():
    if "--themes" in sys.argv:
        themes()
        return
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
