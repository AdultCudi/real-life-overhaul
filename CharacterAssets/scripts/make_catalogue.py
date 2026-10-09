"""Joins the per-kind catalogues the build scripts wrote into build/catalogue.json, which goes into the bundle.

    python make_catalogue.py
"""
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba

# Built by the scripts but not accepted (see README): kept out of the bundle.
REJECTED = ("feet_f_flat", "feet_f_heel", "feet_flat_", "feet_heel_")

items, textures, seen = [], [], set()
for path in sorted(glob.glob(os.path.join(ba.BUILD, "catalogue_*.json"))):
    with open(path) as f:
        part = json.load(f)
    items += [item for item in part["items"] if not item["mesh"].startswith(REJECTED)]
    for texture in part["textures"]:
        if texture["name"] not in seen and not texture["name"].startswith(REJECTED):
            seen.add(texture["name"])
            textures.append(texture)

catalogue = {"version": 2, "bones": ba.bone_names(), "textures": textures, "items": items}
with open(os.path.join(ba.BUILD, "catalogue.json"), "w") as f:
    json.dump(catalogue, f, indent=1)

print("%-18s %-7s %-7s %9s %10s  layers" % ("id", "kind", "sex", "vertices", "triangles"))
for item in items:
    stats = item.get("stats", {})
    print("%-18s %-7s %-7s %9d %10d  %s" % (item["id"], item["kind"], item["sex"], stats.get("vertices", 0), stats.get("triangles", 0),
                                          ", ".join(layer["role"] for layer in item["layers"])))
print(len(items), "items,", len(textures), "textures")
