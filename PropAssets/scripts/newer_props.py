"""Ships props that the asset bundle does not have yet: their meshes as the pipeline writes them for Unity
(build/meshes/<id>.json, already in Unity's space) and their rows of the catalogue, beside the bundle in
Assets/Mods/RealLifeOverhaul/Bundles/props_newer/. The mod reads them when it has read the bundle and uses them in place
of the bundle's own of the same name (Core/PropLibrary); they draw with the bundle's atlas, so they may only use
paints the atlas already has.

    python newer_props.py city_bus city_bus_roof      after build_props.py, when Unity cannot build the bundle now
    python newer_props.py --clear                     build_bundle.ps1 runs this once the bundle has them all

Everything listed goes in one catalogue; the mod takes all of them or none.
"""
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BUILD = os.path.join(os.path.dirname(HERE), "build")
OUT = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Assets", "Mods", "RealLifeOverhaul", "Bundles", "props_newer")
VERSION = 1


def main(ids):
    if ids == ["--clear"]:
        if os.path.isdir(OUT):
            shutil.rmtree(OUT)
            print("props_newer: removed (the bundle has them)")
        return 0
    if not ids:
        print(__doc__)
        return 2
    with open(os.path.join(BUILD, "catalogue.json"), encoding="utf-8") as f:
        catalogue = json.load(f)
    rows = {item["id"]: item for item in catalogue["items"]}
    missing = [i for i in ids if i not in rows]
    if missing:
        print("props_newer: not in build/catalogue.json (run build_props.py first): " + ", ".join(missing))
        return 1
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)
    os.makedirs(OUT)
    items = []
    for i in ids:
        row = rows[i]
        with open(os.path.join(BUILD, "meshes", row["mesh"] + ".json"), encoding="utf-8") as f:
            mesh = json.load(f)
        with open(os.path.join(OUT, row["mesh"] + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump({k: mesh[k] for k in ("name", "vertices", "normals", "uv", "triangles")}, f, separators=(",", ":"))
        items.append({k: row[k] for k in ("id", "mesh", "material", "size", "triangles", "vertices")})
        print("props_newer: %-16s %5d vertices %5d triangles" % (i, row["vertices"], row["triangles"]))
    with open(os.path.join(OUT, "catalogue.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"version": VERSION, "items": items}, f, indent=1)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
