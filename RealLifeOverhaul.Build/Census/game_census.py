# The census of the game's own content, for the steps after a game update (docs/contributing/after-a-game-update.md,
# "New content and DLCs"). Read only: it opens the game's Addressables bundles with UnityPy and writes nothing into
# the game's folder.
#
# It lists what the mod's city check, DLC rule and tables by district stand on: every Building definition (its address,
# district, type and requiredDLC), the game's districts (NeighborhoodData), its streets (StreetData), and the bundles of
# Addressables groups other than the game's default one (where a DLC's content would come). With a baseline it prints
# only what changed: buildings gated behind a DLC, districts and streets new or gone, buildings new or gone.
#
#   python -I game_census.py                          the census of the installed game, against census-3682.json
#   python -I game_census.py --write census-NNNN.json  the same, written as the new baseline
#   python -I game_census.py --game "<Big Ambitions_Data>" --baseline other.json
#
# Needs Python 3.10 or later and UnityPy (pip install UnityPy). Exit code 0 when nothing the mod relies on changed.
import argparse
import json
import os
import sys

import UnityPy

DEFAULT_GAME = r"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data"
HERE = os.path.dirname(os.path.abspath(__file__))
KNOWN_DISTRICTS = [   # Core/CityDistricts.NewYork
    "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity", "ba:neighborhood_lowermanhattan",
    "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons",
]
GLOBAL = "ba:neighborhood_global"
WANTED = {"Building", "NeighborhoodData", "StreetData"}


def bundles(folder):
    """Every bundle of the game's Addressables, by its path under the folder."""
    for root, _, names in os.walk(folder):
        for name in names:
            if name.endswith(".bundle"):
                yield os.path.join(root, name)


def interesting(path):
    """The bundles that hold the definitions the census reads: buildings, districts and streets, in any group."""
    name = os.path.basename(path).lower()
    return any(word in name for word in ("buildings_", "neighborhoods_", "streetdata_"))


def read(path, found, errors):
    env = UnityPy.load(path)
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            script = obj.read(check_read=False).m_Script.read()
            cls = script.m_ClassName
            if cls not in WANTED:
                continue
            found.setdefault(cls, []).append(obj.read_typetree())
        except Exception as e:   # a definition UnityPy cannot read is counted, not guessed at
            errors.append(os.path.basename(path) + ": " + type(e).__name__)


def census(game, everything):
    folder = os.path.join(game, "StreamingAssets", "aa", "StandaloneWindows64")
    if not os.path.isdir(folder):
        sys.exit("The game's Addressables are not at " + folder + " (give --game)")
    found, errors, groups = {}, [], set()
    for path in bundles(folder):
        rel = os.path.relpath(path, folder)
        group = rel.split(os.sep)[0].split("_assets_")[0]
        groups.add(group)
        if everything or interesting(path):
            read(path, found, errors)
    buildings = []
    for b in found.get("Building", []):
        buildings.append({
            "address": "%s|%d" % (b.get("StreetName", ""), b.get("StreetNumber", 0)),
            "district": b.get("Neighbourhood", "") or GLOBAL,
            "type": b.get("BuildingType", ""),
            "sqm": b.get("totalSqm", 0),
            "dlc": b.get("requiredDLC", 0),
        })
    buildings.sort(key=lambda x: x["address"])
    by_district = {}
    for b in buildings:
        by_district[b["district"]] = by_district.get(b["district"], 0) + 1
    return {
        "buildings": len(buildings),
        "gated": [b["address"] + " (" + b["district"] + ", DLC " + str(b["dlc"]) + ")" for b in buildings if b["dlc"]],
        "addresses": [b["address"] for b in buildings],
        "by_district": dict(sorted(by_district.items())),
        "districts": sorted(d.get("neighbourhood", "") for d in found.get("NeighborhoodData", [])),
        "streets": sorted(s.get("streetName", s.get("m_Name", "")) for s in found.get("StreetData", [])),
        "groups": sorted(groups),
        "unread": len(errors),
    }


def changed(label, old, new):
    gone = sorted(set(old) - set(new))
    added = sorted(set(new) - set(old))
    if gone:
        print("  %s gone (%d): %s" % (label, len(gone), ", ".join(gone[:20]) + (" and more" if len(gone) > 20 else "")))
    if added:
        print("  %s new (%d): %s" % (label, len(added), ", ".join(added[:20]) + (" and more" if len(added) > 20 else "")))
    return bool(gone or added)


def main():
    parser = argparse.ArgumentParser(description="The census of the game's buildings, districts, streets and DLC-gated content.")
    parser.add_argument("--game", default=DEFAULT_GAME, help="the game's Big Ambitions_Data folder")
    parser.add_argument("--baseline", default=os.path.join(HERE, "census-3682.json"), help="the census to compare with")
    parser.add_argument("--write", help="write this census to the file given, as the new baseline")
    parser.add_argument("--all", action="store_true", help="read every bundle, not only those of buildings, districts and streets (slow)")
    args = parser.parse_args()

    now = census(args.game, args.all)
    print("The game's census: %d buildings, %d gated behind a DLC; %d districts; %d streets; %d Addressables groups; %d definitions unread."
          % (now["buildings"], len(now["gated"]), len(now["districts"]), len(now["streets"]), len(now["groups"]), now["unread"]))
    for district, count in now["by_district"].items():
        print("  %-36s %4d buildings%s" % (district, count, "" if district in KNOWN_DISTRICTS or district == GLOBAL else "   (not a district the mod knows)"))
    for gated in now["gated"]:
        print("  gated: " + gated)
    if args.write:
        with open(args.write, "w", encoding="utf-8", newline="\n") as out:
            json.dump(now, out, indent=1, sort_keys=True)
            out.write("\n")
        print("Written as the baseline: " + args.write)
        return 0

    problems = bool(now["gated"]) or now["unread"] > 0
    problems |= any(d not in KNOWN_DISTRICTS and d != GLOBAL for d in now["districts"])
    if os.path.exists(args.baseline):
        with open(args.baseline, encoding="utf-8") as f:
            was = json.load(f)
        print("Against " + os.path.basename(args.baseline) + ":")
        moved = changed("districts", was["districts"], now["districts"])
        moved |= changed("streets", was["streets"], now["streets"])
        moved |= changed("buildings", was["addresses"], now["addresses"])
        moved |= changed("Addressables groups", was["groups"], now["groups"])
        moved |= changed("gated buildings", was["gated"], now["gated"])
        if not moved:
            print("  nothing changed.")
        problems |= moved
    else:
        print("No baseline at " + args.baseline + ": run with --write to make one.")
    print("CENSUS: " + ("something the mod relies on changed; follow after-a-game-update.md, New content and DLCs." if problems else "as the mod knows it."))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
