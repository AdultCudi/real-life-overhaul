"""Writes what a built bundle holds to contents/<bundle>.json, committed beside the bundle: its SHA-256, its props with
their triangles, its atlas. StandardCheck (RLO-PERF-016) reads these instead of the bundle itself, and holds each to
the bundle's SHA-256, so a list can never describe another build. build_bundle.ps1 runs it once Unity has verified.

    python contents.py <build folder> <bundle file> [<out file>]
"""
import hashlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    build, bundle = sys.argv[1], sys.argv[2]
    with open(os.path.join(build, "catalogue.json"), encoding="utf-8") as f:
        catalogue = json.load(f)
    import sets
    entry = sets.BY_NAME[catalogue["set"]]
    name = os.path.basename(bundle)
    if name != entry["bundle"]:
        raise SystemExit("contents: %s was built from the set %s, whose bundle is %s" % (name, entry["name"], entry["bundle"]))
    meshes = {}
    for item in catalogue["items"]:
        meshes.setdefault(item["mesh"], item["triangles"])
    out = {
        "bundle": name,
        "set": entry["name"],
        "sha256": sha256(bundle),
        "bytes": os.path.getsize(bundle),
        "triangles": sum(meshes.values()),
        "triangle_limit": entry["triangles"],
        "atlas": catalogue.get("atlas", {"width": 0, "height": 0, "used": 0}),
        "textures": [t["name"] for t in catalogue["textures"]],
        "props": [dict({"id": i["id"], "mesh": i["mesh"], "triangles": i["triangles"], "material": i["material"], "size": i["size"]},
                       **({"alias_of": i["alias_of"]} if "alias_of" in i else {})) for i in catalogue["items"]],
    }
    target = sys.argv[3] if len(sys.argv) > 3 else sets.contents_path(entry)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, indent=1)
        f.write("\n")
    print("contents: %s, %d props, %d triangles of %d, atlas %d x %d (%d %% used), %d bytes -> %s"
          % (name, len(out["props"]), out["triangles"], entry["triangles"], out["atlas"]["width"], out["atlas"]["height"], out["atlas"]["used"], out["bytes"], target))


if __name__ == "__main__":
    main()
