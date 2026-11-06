"""Builds every prop of one bundle into its build folder (meshes, catalogue) and renders previews/ to look at.

    blender -b -P build_props.py                     everything
    blender -b -P build_props.py -- --no-preview     meshes and catalogue only
    blender -b -P build_props.py -- cone barrier     only these (previews only; the catalogue is not rewritten)

Run atlas.py first: the props are mapped onto its regions. PROPS_SET picks the bundle (sets.json; the first by default):
its triangle limit, its textures, and the rules that its ids are new to every other bundle and that a closed set takes
no new prop.
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import prop_kit as kit  # noqa: E402
import props  # noqa: E402
import sets  # noqa: E402

VERSION = 1
TOTAL_BUDGET = sets.SET["triangles"]       # triangles, every mesh of the bundle together (README, "Budgets": why)
DECALS = [
    {"name": "decal_bus_stop", "file": "textures/decal_bus_stop.png", "type": "decal", "maxSize": 1024},   # decals.py: road paint
    {"name": "decal_dash", "file": "textures/decal_dash.png", "type": "decal", "maxSize": 256},
]


def textures():
    """The atlas atlas.py painted for this set (none for an empty set), and the road paint where the set has it."""
    with open(os.path.join(kit.BUILD, "atlas.json")) as f:
        atlas = json.load(f)
    side = max(atlas["width"], atlas["height"])
    out = [] if side == 0 else [
        {"name": "props_base", "file": "textures/props_base.png", "type": "color", "maxSize": side},
        {"name": "props_mask", "file": "textures/props_mask.png", "type": "mask", "maxSize": side // sets.SET["mask_divisor"]},
    ]
    return out + (DECALS if sets.SET.get("decals") else []), {"width": atlas["width"], "height": atlas["height"], "used": atlas.get("used", 0)}


def preview(prop):
    """Front and back three-quarter views close up, then the prop beside a 1.8 m bar."""
    vertices = prop.arrays()[0]
    lift = max(0.0, -float(vertices[:, 2].min()))          # a prop whose pivot is not on the ground is stood on it
    vertices = vertices + (0.0, 0.0, lift)
    low, high = vertices.min(axis=0), vertices.max(axis=0)
    centre, radius = (low + high) / 2, max(float(((high - low) ** 2).sum()) ** 0.5 / 2, 0.02)
    tiles = []
    for k, (azimuth, elevation) in enumerate(((35, 24), (215, 32))):
        scene, camera = kit.new_scene()
        kit.place(prop, (0, 0, lift))
        kit.aim(camera, centre, azimuth, elevation, radius / math.tan(math.radians(14)) * 1.15)
        tiles.append(os.path.join(kit.PREVIEWS, "_%s_%d.png" % (prop.name, k)))
        kit.render(scene, tiles[-1])
    scene, camera = kit.new_scene()
    kit.place(prop, (0, 0, lift))
    kit.height_bar((low[0] - 0.35, 0.0))
    reach = max(1.15, radius * 1.2)
    kit.aim(camera, ((low[0] + high[0]) / 2 - 0.15, 0.0, 0.85), 30, 14, reach / math.tan(math.radians(14)) * 1.1)
    tiles.append(os.path.join(kit.PREVIEWS, "_%s_2.png" % prop.name))
    kit.render(scene, tiles[-1])
    kit.sheet(tiles, os.path.join(kit.PREVIEWS, prop.name + ".png"))


def cordon(made):
    """An accident scene as the mod lays one out: posts and tape round it, barriers across the lane, the crew's kit inside."""
    by_name = {prop.name: prop for prop, _ in made}
    wanted = ("tape_post", "tape_4m_sag", "barrier", "cone", "stretcher", "stretcher_sheet", "medic_bag", "debris_glass", "debris_plastic",
              "flare", "evidence_marker_1", "evidence_marker_2")
    if any(name not in by_name for name in wanted):
        return
    scene, camera = kit.new_scene()
    corners = [(-4, -3), (0, -3), (4, -3), (4, 1), (4, 5), (0, 5), (-4, 5), (-4, 1)]
    for k, (x, y) in enumerate(corners):
        kit.place(by_name["tape_post"], (x, y, 0))
        nx, ny = corners[(k + 1) % len(corners)]
        kit.place(by_name["tape_4m_sag"], ((x + nx) / 2, (y + ny) / 2, 0.95), turn=math.degrees(math.atan2(-(nx - x), ny - y)))
    for x in (-2.0, 1.2):
        kit.place(by_name["barrier"], (x, 4.1, 0), turn=4 if x < 0 else -3)
    for x, y in ((-2.6, 0.2), (-1.4, -1.9), (2.4, -1.4)):
        kit.place(by_name["cone"], (x, y, 0))
    kit.place(by_name["stretcher"], (0.6, 0.6, 0), turn=-70)
    kit.place(by_name["stretcher_sheet"], (0.6, 0.6, 0), turn=-70)
    kit.place(by_name["medic_bag"], (-0.9, 0.9, 0), turn=25)
    kit.place(by_name["debris_glass"], (1.6, 2.4, 0))
    kit.place(by_name["debris_plastic"], (1.9, 2.2, 0), turn=40)
    kit.place(by_name["flare"], (-3.2, 3.9, 0), turn=60)
    kit.place(by_name["evidence_marker_1"], (1.1, 2.9, 0), turn=20)
    kit.place(by_name["evidence_marker_2"], (2.6, 1.6, 0), turn=-15)
    kit.height_bar((-1.9, 1.6))
    kit.aim(camera, (0, 1.0, 0.5), 28, 24, 15.5, fov=38)
    shots = [os.path.join(kit.PREVIEWS, "scene_cordon.png"), os.path.join(kit.PREVIEWS, "scene_cordon_street.png")]
    kit.render(scene, shots[0], 1280, 720)
    kit.aim(camera, (0.3, 0.8, 0.6), 200, 12, 7.5, fov=45)      # the height the player sees it from
    kit.render(scene, shots[1], 1280, 720)
    from PIL import Image
    for shot in shots:
        kit.compact(Image.open(shot), shot)


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = [a for a in arguments if not a.startswith("--")] or None
    made = props.build_all(only)
    if only is None:
        names = [prop.name for prop, _ in made] + list(props.ALIASES)
        twice = sorted({n for n in names if names.count(n) > 1})
        problems = (["listed twice: " + ", ".join(twice)] if twice else []) + sets.check_ids(names)
        if problems:
            raise SystemExit("PROP IDS: " + "; ".join(problems))
    rows, over = [], []
    for prop, budget in made:
        row = kit.write_mesh(prop)
        rows.append(row)
        if row["triangles"] > budget:
            over.append("%s has %d triangles, its budget is %d" % (prop.name, row["triangles"], budget))
    total = sum(r["triangles"] for r in rows)
    if only is None and total > TOTAL_BUDGET:
        over.append("%d triangles in all, the budget of the bundle %s is %d" % (total, sets.SET["bundle"], TOTAL_BUDGET))
    if over:
        raise SystemExit("OVER BUDGET: " + "; ".join(over))
    if only is None:
        by_id = {row["id"]: row for row in rows}
        for alias, kept in props.ALIASES.items():
            if kept not in by_id:
                raise SystemExit("PROP IDS: alias %s is drawn as %s, which is not built" % (alias, kept))
            rows.append(dict(by_id[kept], id=alias, alias_of=kept))      # the same mesh: stored once
        listed, atlas = textures()
        with open(os.path.join(kit.BUILD, "catalogue.json"), "w") as f:
            json.dump({"version": VERSION, "set": sets.NAME, "bundle": sets.SET["bundle"], "atlas": atlas, "textures": listed, "items": rows}, f, indent=1)
        print("catalogue: %s, %d props (%d aliases), %d triangles in all of %d" % (sets.SET["bundle"], len(rows), len(props.ALIASES), total, TOTAL_BUDGET))
    if "--no-preview" not in arguments:
        os.makedirs(kit.PREVIEWS, exist_ok=True)
        by_name = {prop.name: prop for prop, _ in made}
        for prop, _ in made:
            preview(prop)
            if prop.name in props.SIDES:
                sides = dict(props.SIDES[prop.name])
                # A SIDES entry may name the props the code puts on this one, (id, at, turn): those that were built with it.
                sides["parts"] = [(by_name[name], at, turn) for name, at, turn in sides.get("parts", ()) if name in by_name]
                kit.turnaround(prop, os.path.join(kit.PREVIEWS, prop.name + "_sides.png"), **sides)
        cordon(made)


try:
    main()
except SystemExit:
    raise
except Exception:
    import traceback
    traceback.print_exc()
    sys.exit(1)
