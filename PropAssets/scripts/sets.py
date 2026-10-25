"""The props bundles (sets.json): which one this run builds, where it writes, its limits, and what the others hold.

PROPS_SET=<name> picks the set (the first by default); PROPS_BUILD=<folder> sends its files to a folder of one's own.
Every script of the pipeline takes the set from here, so a bundle's rules are written once, in sets.json. Plain Python:
atlas.py runs it outside Blender.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
CONTENTS = os.path.join(ROOT, "contents")       # what each shipped bundle holds, written after Unity has built it (contents.py)

with open(os.path.join(ROOT, "sets.json"), encoding="utf-8") as _f:
    ALL = json.load(_f)["sets"]
BY_NAME = {s["name"]: s for s in ALL}

NAME = os.environ.get("PROPS_SET") or ALL[0]["name"]
if NAME not in BY_NAME:
    raise SystemExit("PROPS_SET=%s: sets.json has no such set (%s)" % (NAME, ", ".join(BY_NAME)))
SET = BY_NAME[NAME]
FIRST = SET is ALL[0]
BUILD = os.environ.get("PROPS_BUILD") or os.path.join(ROOT, SET["build"])
# The set's Blender exports (README, "Adding a prop from Blender"); PROPS_MODELS points the build at other ones, as
# build.ps1 -Test does with the made-up models it proves the path with.
MODELS = (os.environ.get("PROPS_MODELS") or os.path.join(ROOT, SET["models"])) if SET.get("models") else None


def contents_path(entry):
    return os.path.join(CONTENTS, entry["bundle"] + ".json")


def contents(entry):
    """What the committed bundle of a set holds, or None before it was first built."""
    path = contents_path(entry)
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def held_elsewhere():
    """Every prop id the other sets' bundles hold: id -> bundle. A prop is in one bundle only."""
    held = {}
    for entry in ALL:
        if entry is SET:
            continue
        for item in (contents(entry) or {}).get("props", []):
            held[item["id"]] = entry["bundle"]
    return held


def check_ids(ids):
    """The rules on a set's ids, before anything is built: unique across the bundles, and a closed set takes no new one."""
    problems = []
    elsewhere = held_elsewhere()
    clash = sorted(i for i in ids if i in elsewhere)
    if clash:
        problems.append("in another bundle already (a prop id is unique across the bundles): " + ", ".join("%s (%s)" % (i, elsewhere[i]) for i in clash))
    if not SET.get("open"):
        mine = contents(SET)
        if mine is not None:
            known = {item["id"] for item in mine.get("props", [])}
            new = sorted(i for i in ids if i not in known)
            if new:
                last_open = [s["name"] for s in ALL if s.get("open")]
                problems.append("new in the closed set '%s' (new props go in the set '%s', README 'The bundles'): %s"
                                % (NAME, last_open[-1] if last_open else "?", ", ".join(new)))
    return problems
