# The census of the game's own fonts, for the RLO interface's fallbacks and for the steps after a game update
# (docs/contributing/after-a-game-update.md, "The fonts"). Read only: it opens the game's own files with UnityPy and
# writes nothing into the game's folder.
#
# The interface's face (Overpass, the bundle reallife_ui) draws Latin and Cyrillic letters. Every other character is drawn
# by one of the game's own fonts: TextMeshPro's general fallbacks in the game's TMP Settings. This lists them as the
# mod's rules and checks stand on them: each font asset's line (its ascent and descent at its point size), whether it
# is static or dynamic, its fallback table, the characters its font file draws and how wide, TMP Settings' default
# font, general fallbacks and missing glyph character, and the characters of the game's own words in each language it
# offers (StreamingAssets/locale). The player's files carry no type trees, so each object is read
# by the field order of the game's own Unity.TextMeshPro.dll (TMP_Asset, TMP_FontAsset, TMP_Settings) and the engine's
# FaceInfo and Glyph; an object that does not end at its last byte is reported, never guessed at.
#
#   python -I game_fonts.py                        the installed game against the newest fonts-NNNN.json
#   python -I game_fonts.py --write fonts-NNNN.json   the same, written as the new baseline
#   python -I game_fonts.py --data                 writes Scripts/Ui/UiFallbackRules.Data.cs from the installed game
#   python -I game_fonts.py --data --check         fails if that file is not what the installed game gives
#   python -I game_fonts.py --extract <folder>     the fallbacks' font files, for the offline probe (local only: the
#                                                  game's files are never committed)
#
# Needs Python 3.10 or later, UnityPy and fontTools (pip install UnityPy fonttools). Exit code 0 when nothing changed.
import argparse
import io
import json
import os
import struct
import sys

import UnityPy
from fontTools.ttLib import TTCollection, TTFont

DEFAULT_GAME = r"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data"
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
DATA = os.path.join(ROOT, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "UiFallbackRules.Data.cs")
REGIONS = ("KR", "SC", "TC", "HK", "JP")   # a family whose name ends in one writes that region's script (Ui/UiFallbackRules.Region)
MODES = {0: "static", 1: "dynamic", 2: "dynamic OS"}
FACE = ["faceIndex:i", "familyName:s", "styleName:s", "pointSize:i", "scale:f", "unitsPerEM:i", "lineHeight:f", "ascentLine:f",
        "capLine:f", "meanLine:f", "baseline:f", "descentLine:f", "superscriptOffset:f", "superscriptSize:f",
        "subscriptOffset:f", "subscriptSize:f", "underlineOffset:f", "underlineThickness:f", "strikethroughOffset:f",
        "strikethroughThickness:f", "tabWidth:f"]


class Reader:
    """A serialized object's bytes read field by field, as the engine wrote them: each value on a four byte boundary."""

    def __init__(self, data):
        self.d = data
        self.p = 0

    def align(self):
        self.p = (self.p + 3) & ~3

    def i32(self):
        v = struct.unpack_from("<i", self.d, self.p)[0]
        self.p += 4
        return v

    def u32(self):
        v = struct.unpack_from("<I", self.d, self.p)[0]
        self.p += 4
        return v

    def f32(self):
        v = struct.unpack_from("<f", self.d, self.p)[0]
        self.p += 4
        return v

    def flag(self):
        v = self.d[self.p]
        self.p += 1
        self.align()
        return v

    def text(self):
        n = self.i32()
        if n < 0 or self.p + n > len(self.d):
            raise ValueError("a text of %d bytes at %d" % (n, self.p - 4))
        v = self.d[self.p:self.p + n].decode("utf-8", "replace")
        self.p += n
        self.align()
        return v

    def pptr(self):
        file_id = self.i32()
        path_id = struct.unpack_from("<q", self.d, self.p)[0]
        self.p += 8
        return (file_id, path_id)

    def many(self, each):
        n = self.i32()
        if n < 0 or n > 5000000:
            raise ValueError("a list of %d at %d" % (n, self.p - 4))
        out = [each() for _ in range(n)]
        self.align()
        return out


def fields(r, spec):
    out = {}
    for item in spec:
        name, kind = item.split(":")
        out[name] = r.i32() if kind == "i" else r.f32() if kind == "f" else r.text()
    return out


def font_asset(data, pair_bytes):
    """A TMP_FontAsset: what the census keeps of it, and where its reading ended."""
    r = Reader(data)
    r.pptr()
    r.flag()
    r.pptr()
    out = {"name": r.text(), "version": r.text(), "face": fields(r, FACE), "material": r.pptr()}
    r.text()                                   # m_SourceFontFileGUID
    r.text()                                   # m_CreationSettings: sourceFontFileName, sourceFontFileGUID
    r.text()
    for _ in range(9):                         # faceIndex to characterSetSelectionMode
        r.i32()
    r.text()                                   # characterSequence, referencedFontAssetGUID, referencedTextAssetGUID
    r.text()
    r.text()
    r.i32()                                    # fontStyle, fontStyleModifier, renderMode, includeFontFeatures
    r.f32()
    r.i32()
    r.flag()
    out["source"] = r.pptr()
    r.text()                                   # m_SourceFontFilePath
    out["mode"] = r.i32()
    r.flag()                                   # InternalDynamicOS
    glyphs = r.many(lambda: (r.u32(), [r.f32() for _ in range(5)], [r.i32() for _ in range(4)], r.f32(), r.i32(), r.i32()))
    out["glyphs"] = {g[0]: g[1][4] for g in glyphs}                                  # glyph index to its advance
    out["own"] = r.many(lambda: (r.i32(), r.u32(), r.u32(), r.f32()))               # element type, unicode, glyph index, scale
    r.many(r.pptr)                             # m_AtlasTextures
    r.i32()                                    # m_AtlasTextureIndex
    out["multi"] = r.flag()
    r.flag()                                   # m_GetFontFeatures, m_ClearDynamicDataOnBuild
    r.flag()
    out["atlas"] = [r.i32(), r.i32(), r.i32(), r.i32()]                              # width, height, padding, render mode
    r.many(lambda: [r.i32() for _ in range(4)])                                      # used and free glyph rects
    r.many(lambda: [r.i32() for _ in range(4)])
    r.many(lambda: (r.u32(), r.many(r.u32)))                                         # the font feature table
    r.many(lambda: (r.many(r.u32), r.u32()))

    def skip(n):
        r.p += n

    r.many(lambda: skip(pair_bytes))
    r.many(lambda: skip(24))
    r.many(lambda: skip(24))
    r.flag()                                   # m_ShouldReimportFontFeatures
    out["fallbacks"] = r.many(r.pptr)
    r.many(lambda: (r.pptr(), r.pptr()))       # m_FontWeightTable, fontWeights
    r.many(lambda: (r.pptr(), r.pptr()))
    for _ in range(4):                         # normalStyle to boldSpacing
        r.f32()
    r.flag()                                   # italicStyle, tabSize
    r.flag()
    r.text()                                   # m_fontInfo, the face info of TextMeshPro's first versions
    r.f32()
    r.f32()
    r.i32()
    for _ in range(17):
        r.f32()
    r.many(lambda: (r.i32(),) + tuple(r.f32() for _ in range(8)))                    # m_glyphInfoList

    def kerning():
        r.u32()
        skip(16)
        r.u32()
        skip(16)
        r.f32()
        r.flag()

    r.many(kerning)
    r.many(r.pptr)                             # fallbackFontAssets, atlas
    r.pptr()
    out["end"] = r.p
    return out


def settings(data):
    """TMP Settings: the default font, the general fallbacks, the missing glyph character, and where its reading ended."""
    r = Reader(data)
    r.pptr()
    r.flag()
    r.pptr()
    r.text()                                   # m_Name, assetVersion
    r.text()
    r.i32()                                    # m_TextWrappingMode
    r.flag()                                   # m_enableKerning
    r.many(r.u32)                              # m_ActiveFontFeatures
    for _ in range(5):                         # m_enableExtraPadding to m_GetFontFeaturesAtRuntime
        r.flag()
    out = {"missing_glyph": r.i32()}
    r.flag()                                   # m_ClearDynamicDataOnBuild
    out["warnings_disabled"] = bool(r.flag())
    out["default"] = r.pptr()
    r.text()                                   # m_defaultFontAssetPath
    for _ in range(7):                         # the default size, the auto size ratios and the two container sizes
        r.f32()
    r.flag()                                   # m_autoSizeTextContainer, m_IsTextObjectScaleStatic
    r.flag()
    out["fallbacks"] = r.many(r.pptr)
    out["match_material_preset"] = bool(r.flag())
    out["end"] = r.p
    return out


def codes_of(font_bytes):
    """Every character a font file draws and its advance in the font's own units, with its units per em."""
    faces = TTCollection(io.BytesIO(font_bytes)).fonts if font_bytes[:4] == b"ttcf" else [TTFont(io.BytesIO(font_bytes), lazy=True)]
    advances = {}
    upem = faces[0]["head"].unitsPerEm
    for face in faces:
        metrics = face["hmtx"]
        scale = upem / face["head"].unitsPerEm
        for code, glyph in face.getBestCmap().items():
            if code <= 0xFFFF and code not in advances:   # the interface's texts hold no character beyond the basic plane
                advances[code] = int(round(metrics[glyph][0] * scale))
    return advances, upem


def runs(codes):
    """Sorted characters as inclusive ranges: [first, last] in hex."""
    out, start, last = [], None, None
    for c in sorted(codes):
        if start is None:
            start = last = c
        elif c == last + 1:
            last = c
        else:
            out.append((start, last))
            start = last = c
    if start is not None:
        out.append((start, last))
    return out


def lines(parts, per_line=24):
    """Many short words as a few long lines, so the file stays readable and a change shows where it is."""
    return [" ".join(parts[i:i + per_line]) for i in range(0, len(parts), per_line)]


def advance_runs(advances):
    """A font's advances as runs of characters that share one: first-last:units. A run goes on over characters the font lacks."""
    out, start, last, value = [], None, None, None
    for c in sorted(advances):
        if start is not None and advances[c] == value:
            last = c
            continue
        if start is not None:
            out.append("%X-%X:%d" % (start, last, value))
        start, last, value = c, c, advances[c]
    if start is not None:
        out.append("%X-%X:%d" % (start, last, value))
    return out


def census(game):
    """The game's font assets and TMP Settings as the mod's rules need them, and each fallback's font file."""
    files = []
    for name in sorted(os.listdir(game)):
        full = os.path.join(game, name)
        if os.path.isfile(full) and (name.endswith(".assets") or (name.startswith("level") and "." not in name) or name == "globalgamemanagers"):
            files.append(full)
    if not files:
        sys.exit("The game's files are not at " + game + " (give --game)")
    env = UnityPy.load(*files)
    by_key, fonts, the_settings = {}, [], None
    for obj in env.objects:
        by_key[(obj.assets_file.name.lower(), obj.path_id)] = obj
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            head = obj.read(check_read=False)
            cls = head.m_Script.read().m_ClassName if head.m_Script else None
        except Exception:
            continue
        if cls == "TMP_FontAsset":
            fonts.append(obj)
        elif cls == "TMP_Settings":
            the_settings = obj

    def resolve(obj, ptr):
        file_id, path_id = ptr
        if path_id == 0:
            return None
        name = obj.assets_file.name.lower()
        if file_id != 0:
            ext = obj.assets_file.externals[file_id - 1]
            name = os.path.basename(getattr(ext, "path", None) or ext.name).lower()
        return by_key.get((name, path_id))

    def name_of(obj):
        return obj.read(check_read=False).m_Name if obj is not None else None

    unread, out, blobs = [], {}, {}
    for obj in fonts:
        raw, parsed = obj.get_raw_data(), None
        for pair_bytes in (44, 40):            # the engine's pair adjustment record, with and without its lookup flags
            try:
                tried = font_asset(raw, pair_bytes)
                if tried["end"] == len(raw):
                    parsed = tried
                    break
            except Exception:
                continue
        if parsed is None:
            unread.append(name_of(obj) or "?")
            continue
        face = parsed["face"]
        source = resolve(obj, parsed["source"])
        advances, upem, source_name = {}, face["unitsPerEM"], None
        if source is not None:
            font = source.read()
            source_name = font.m_Name
            blob = bytes(font.m_FontData) if getattr(font, "m_FontData", None) else b""
            if blob:
                advances, upem = codes_of(blob)
                blobs[parsed["name"]] = (source_name, blob)
        if not advances:                       # a static asset: what its own tables draw, at its own point size
            for _, code, glyph, _ in parsed["own"]:
                if code <= 0xFFFF and glyph in parsed["glyphs"]:
                    advances[code] = int(round(parsed["glyphs"][glyph]))
            upem = face["pointSize"]
        out[parsed["name"]] = {
            "file": obj.assets_file.name, "family": face["familyName"], "style": face["styleName"], "mode": MODES.get(parsed["mode"], str(parsed["mode"])),
            "point": face["pointSize"], "ascent": round(face["ascentLine"], 3), "descent": round(face["descentLine"], 3),
            "line": round(face["lineHeight"], 3), "cap": round(face["capLine"], 3), "scale": round(face["scale"], 3),
            "source": source_name, "fallbacks": [name_of(resolve(obj, f)) for f in parsed["fallbacks"]],
            "atlas": parsed["atlas"][:3], "multi_atlas": bool(parsed["multi"]),
            "characters": len(advances), "upem": upem,
            "covers": lines(["%X-%X" % r for r in runs(advances)]),
            "advances": lines(advance_runs(advances), 16),
        }
    result = {"fonts": dict(sorted(out.items())), "unread": sorted(unread), "settings": None, "words": words(game)}
    if the_settings is not None:
        try:
            s = settings(the_settings.get_raw_data())
            result["settings"] = {
                "default": name_of(resolve(the_settings, s["default"])),
                "fallbacks": [name_of(resolve(the_settings, f)) for f in s["fallbacks"]],
                "missing_glyph": s["missing_glyph"], "match_material_preset": s["match_material_preset"],
            }
        except Exception as e:
            result["unread"].append("TMP Settings (" + type(e).__name__ + ")")
    return result, blobs


def words(game):
    """The characters of the game's own words in each language it offers, as ranges: the interface draws them on its screens."""
    folder = os.path.join(game, "StreamingAssets", "locale")
    index = os.path.join(folder, "locale.json")
    if not os.path.exists(index):
        return {}
    out = {}
    with open(index, encoding="utf-8-sig") as f:
        listed = sorted(code.lower() for code in json.load(f))
    for code in listed:
        path = os.path.join(folder, code + ".json")
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8-sig") as f:
            texts = json.load(f)
        seen = set()
        for value in texts.values():
            if isinstance(value, str):
                seen.update(ord(ch) for ch in value if ord(ch) >= 32 and ord(ch) <= 0xFFFF and not 0xD800 <= ord(ch) <= 0xDFFF)
        out[code] = {"characters": len(seen), "covers": lines(["%X-%X" % r for r in runs(seen)])}
    return out


def chain(now):
    """The font assets a character can fall back to, in TMP's order: the general fallbacks, then the default font and its own."""
    s = now["settings"] or {}
    names = list(s.get("fallbacks") or [])
    default = s.get("default")
    if default:
        names.append(default)
        names.extend(now["fonts"].get(default, {}).get("fallbacks") or [])
    seen, out = set(), []
    for name in names:
        if name and name not in seen and name in now["fonts"]:
            seen.add(name)
            out.append(name)
    return out


def western(now):
    """The general fallback for Latin, Greek and Cyrillic letters: the first in the chain with a font file of its own and no region in its name."""
    for name in chain(now):
        font = now["fonts"][name]
        if font["source"] and font["family"].split(" ")[-1] not in REGIONS:
            return name
    return None


def unpack_advances(font):
    out = {}
    for line in font["advances"]:
        for part in line.split(" "):
            span, value = part.split(":")
            first, last = span.split("-")
            for c in range(int(first, 16), int(last, 16) + 1):
                out[c] = int(value)
    covered = set()
    for line in font["covers"]:
        for part in line.split(" "):
            first, last = part.split("-")
            covered.update(range(int(first, 16), int(last, 16) + 1))
    return {c: a for c, a in out.items() if c in covered}


def data_file(now):
    """Scripts/Ui/UiFallbackRules.Data.cs: the Western fallback's characters and advances, which the kit measures by."""
    name = western(now)
    if name is None:
        sys.exit("FONTS: the game has no general fallback for Latin, Greek and Cyrillic letters: the kit's measure of them needs another rule")
    font = now["fonts"][name]
    advances = unpack_advances(font)
    codes = sorted(advances)

    def array(label, values, per_line=24):
        rows = ["            " + ", ".join(str(v) for v in values[i:i + per_line]) + "," for i in range(0, len(values), per_line)]
        return "        private static readonly ushort[] %s =\n        {\n%s\n        };\n" % (label, "\n".join(rows))

    out = io.StringIO()
    out.write("// Generated by RealLifeOverhaul.Build/Census/game_fonts.py from the installed game's own font files: do not edit by\n")
    out.write("// hand. Run the script again after a game update and commit this file with the census (game_fonts.py --data --check\n")
    out.write("// holds them together).\n")
    out.write("namespace RealLifeOverhaul.Ui\n{\n")
    out.write("    public static partial class UiFallbackRules\n    {\n")
    out.write("        /// <summary>The family of the game's general fallback for Latin, Greek and Cyrillic letters (%s).</summary>\n" % name)
    out.write("        public const string WesternFamily = \"%s\";\n\n" % font["family"])
    out.write("        private const int WesternUnitsPerEm = %d;   // the font's own units in an em: its advances below are in them\n\n" % font["upem"])
    out.write(array("WesternCodes", codes))
    out.write("\n")
    out.write(array("WesternAdvances", [advances[c] for c in codes]))
    out.write("    }\n}\n")
    return out.getvalue().replace("\n", "\r\n")


def newest():
    """The census of the latest build kept beside this script: fonts-NNNN.json with the highest NNNN."""
    builds = []
    for name in os.listdir(HERE):
        if name.startswith("fonts-") and name.endswith(".json") and name[6:-5].isdigit():
            builds.append(int(name[6:-5]))
    return os.path.join(HERE, "fonts-%d.json" % max(builds)) if builds else os.path.join(HERE, "fonts.json")


def compare(old, new):
    """What changed between two censuses, a line each; nothing printed when they are the same."""
    changed = False
    for key in ("default", "fallbacks", "missing_glyph", "match_material_preset"):
        a, b = (old.get("settings") or {}).get(key), (new.get("settings") or {}).get(key)
        if a != b:
            print("  TMP Settings, %s: %s, was %s" % (key, b, a))
            changed = True
    for name in sorted(set(old["fonts"]) | set(new["fonts"])):
        a, b = old["fonts"].get(name), new["fonts"].get(name)
        if a is None or b is None:
            print("  font %s: %s" % (name, "new" if a is None else "gone"))
            changed = True
            continue
        for key in sorted(set(a) | set(b)):
            if a.get(key) != b.get(key):
                said = "%s, was %s" % (b.get(key), a.get(key)) if key not in ("covers", "advances") else "changed"
                print("  font %s, %s: %s" % (name, key, said))
                changed = True
    old_words, new_words = old.get("words") or {}, new.get("words") or {}
    for code in sorted(set(old_words) | set(new_words)):
        if old_words.get(code) != new_words.get(code):
            a, b = old_words.get(code), new_words.get(code)
            print("  the game's words in %s: %s" % (code, "new" if a is None else "gone" if b is None else "%d characters, were %d" % (b["characters"], a["characters"])))
            changed = True
    return changed


def main():
    parser = argparse.ArgumentParser(description="The census of the game's fonts: the fallbacks the RLO interface's text stands on.")
    parser.add_argument("--game", default=DEFAULT_GAME, help="the game's Big Ambitions_Data folder")
    parser.add_argument("--baseline", default=newest(), help="the census to compare with (default: the newest fonts-NNNN.json here)")
    parser.add_argument("--write", help="write this census to the file given, as the new baseline")
    parser.add_argument("--data", action="store_true", help="write Scripts/Ui/UiFallbackRules.Data.cs from the installed game")
    parser.add_argument("--check", action="store_true", help="with --data: fail if the file is not what the installed game gives")
    parser.add_argument("--extract", help="write the fallbacks' font files into this folder (local only, never committed)")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    now, blobs = census(args.game)
    order = chain(now)
    print("The game's fonts: %d font assets, %d unread; TMP's default font %s; general fallbacks: %s; missing glyph character %s."
          % (len(now["fonts"]), len(now["unread"]), (now["settings"] or {}).get("default"), ", ".join((now["settings"] or {}).get("fallbacks") or []) or "none",
             (now["settings"] or {}).get("missing_glyph")))
    for name in order:
        f = now["fonts"][name]
        print("  %-32s %-10s %-16s line %.3f em (ascent %.3f, descent %.3f), %5d characters%s"
              % (name, f["mode"], f["family"], (f["ascent"] - f["descent"]) / f["point"], f["ascent"] / f["point"], f["descent"] / f["point"], f["characters"],
                 "" if f["source"] or f["mode"] == "static" else "   (no font file of its own: the system's, where it is installed)"))
    for name in now["unread"]:
        print("  unread: " + name)
    print("  the game's own words: %d languages, %s" % (len(now["words"]), ", ".join("%s %d" % (code, w["characters"]) for code, w in now["words"].items())))
    if args.extract:
        os.makedirs(args.extract, exist_ok=True)
        for name in order:
            if name in blobs:
                source, blob = blobs[name]
                family = now["fonts"][name]["family"].replace(" ", "")
                target = os.path.join(args.extract, family + (".ttc" if blob[:4] == b"ttcf" else ".otf" if blob[:4] == b"OTTO" else ".ttf"))
                with open(target, "wb") as out:
                    out.write(blob)
                print("  extracted: %s (%s, %d bytes)" % (target, source, len(blob)))
    if args.data:
        text = data_file(now)
        if args.check:
            with open(DATA, "r", encoding="utf-8-sig", newline="") as f:
                if f.read() != text:
                    sys.exit("FONTS: Scripts/Ui/UiFallbackRules.Data.cs is not what the installed game gives: run game_fonts.py --data and commit it")
            print("FONTS: the kit's table of the Western fallback is as the installed game gives it.")
        else:
            with open(DATA, "w", encoding="utf-8", newline="") as f:
                f.write(text)
            print("Written: " + os.path.relpath(DATA, ROOT))
    if args.write:
        with open(args.write, "w", encoding="utf-8", newline="\n") as out:
            json.dump(now, out, indent=1, sort_keys=True)
            out.write("\n")
        print("Written as the baseline: " + args.write)
        return 0
    problems = bool(now["unread"]) or now["settings"] is None
    if os.path.exists(args.baseline):
        with open(args.baseline, encoding="utf-8") as f:
            problems |= compare(json.load(f), now)
    else:
        print("  no baseline at " + args.baseline + ": write one with --write")
        problems = True
    print("FONTS: as the mod knows them." if not problems else "FONTS: something changed. Read Ui/UiFallbackRules.cs, write the new census and run EconomyCheck --fonts.")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
