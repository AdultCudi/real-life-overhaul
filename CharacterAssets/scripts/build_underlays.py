"""Builds the scalp underlays: a hair-coloured cap worn UNDER each of the game's own hairstyles.

    blender -b -P build_underlays.py -- [--no-preview]

The game's detailed hair is thin see-through strips over bare skin. Close up that is fine; from the
distance the game is played at the skin shows through and the head looks half bald. The underlay is a
smooth cap on the skull, coloured like the hair, so what shows between the strips is more hair.

One cap mesh per sex. One small texture per hairstyle and sex, whose alpha is that hairstyle's footprint:
solid where the game's hair mesh lies over the scalp, gone where it does not (fades, partings, bald
styles stay as the game made them). The footprint is measured from the game's mesh in the local
reference; nothing of that mesh is copied.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba
import hair_lib
from hair_lib import Head, build_cap, cap_uv, smoothstep
from mathutils import Vector
from mathutils.bvhtree import BVHTree

SIZE = 256
REACH = (0.006, 0.016)        # hair closer to the scalp than the first counts fully, beyond the second not at all
STRENGTH = 0.92               # the cap is never quite opaque: a trace of scalp keeps a parting a parting
DOWN = np.array([0.0, 0.0, -1.0])


def hair_renderers(sex):
    """(variant name, mesh name, indices of the submeshes that are strands) for every hairstyle of this sex."""
    found = []
    for renderer in ba.index()["renderers"]:
        parts = renderer["path"].split("/")
        if len(parts) == 4 and parts[1].lower() == sex and parts[2] == "Hair":
            strands = [i for i, m in enumerate(renderer["materials"]) if m and "scalp" not in m.lower()]
            found.append((parts[3], renderer["mesh"], strands))
    return found


def scalp_points(head):
    """For every texel of the cap's texture: the point of the skull it shows, or NaN off the head."""
    ys, xs = np.mgrid[0:SIZE, 0:SIZE]
    u, v = (xs + 0.5) / SIZE, 1.0 - (ys + 0.5) / SIZE
    dx, dy = (u - 0.5) * 2, (0.5 - v) * 2
    r = np.sqrt(dx * dx + dy * dy)
    azimuth = np.degrees(np.arctan2(dx, dy))
    e = 90.0 - r * 150.0
    points = np.full((SIZE, SIZE, 3), np.nan)
    for y in range(SIZE):
        for x in range(SIZE):
            if e[y, x] < -58:
                continue
            found = head.surface(head.direction(azimuth[y, x], e[y, x]))
            if found is not None:
                points[y, x] = found[0]
    return points, e, np.abs(azimuth)


def footprint(points, mesh_name, strands):
    """0..1 per texel: how much of the game's hair lies over that point of the scalp."""
    ref = ba.load_reference(mesh_name)
    wanted = [ref["submeshes"][i] for i in strands if i < len(ref["submeshes"])] or ref["submeshes"]
    tree = BVHTree.FromPolygons(ref["vertices"].tolist(), np.concatenate(wanted).tolist())
    cover = np.zeros(points.shape[:2])
    for y in range(SIZE):
        for x in range(SIZE):
            p = points[y, x]
            if np.isnan(p[0]):
                continue
            distance = tree.find_nearest(Vector(p))[3]
            cover[y, x] = 1.0 - float(smoothstep(distance, REACH[0], REACH[1]))
    return cover


def strokes(head, seed):
    """The cap's colour: fine streaks falling away from the crown, the way roots lie. Greyscale, tinted by the game."""
    from PIL import Image, ImageDraw, ImageFilter
    rng = np.random.default_rng(seed)
    sup = 4
    big = SIZE * sup
    image = Image.new("L", (big, big), int(255 * 0.5))
    draw = ImageDraw.Draw(image)
    for _ in range(9000):
        spot = head.spot(head.direction(rng.uniform(-180, 180), 90.0 - 150.0 * np.sqrt(rng.random()) * 0.95), rng)
        if spot is None:
            continue
        away = np.array([spot.p[0], spot.p[1] - 0.03, 0.0])
        t = spot.tangent(away * 4.0 + DOWN + rng.normal(0, 0.2, 3))
        a = cap_uv(spot.p - head.centre)
        b = cap_uv(spot.p + t * 0.012 * rng.uniform(0.5, 1.4) - head.centre)
        level = int(255 * np.clip(rng.normal(0.5, 0.11), 0.2, 0.85))
        draw.line([(a[0] * big, (1 - a[1]) * big), (b[0] * big, (1 - b[1]) * big)], fill=level, width=sup)
    return np.asarray(image.resize((SIZE, SIZE), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.5)), dtype=np.uint8)


def build(sex, previews):
    from PIL import Image, ImageFilter
    head = Head(sex)
    head.hairline_shift = -10.0                    # the cap reaches past any hairline; each footprint cuts it back
    vertices, normals, uv, triangles, _ = build_cap(head, margin=2.0, columns=48, rings=14)
    mesh = "cap_" + sex
    indices, weights = ba.bone_weights_single(len(vertices), "Skull")
    stats = ba.write_mesh(mesh, vertices, normals, uv, [triangles], indices, weights)
    points, e, phi = scalp_points(head)
    grey = strokes(head, 5 if sex == "male" else 6)
    # Only the scalp: long hair also lies close to the neck and the cheeks, which must stay skin.
    natural = np.interp(phi, *hair_lib.HAIRLINES[sex])
    scalp = smoothstep(e - natural, -7.0, -1.0)
    items, textures, done = [], [], {}
    for variant, mesh_name, strands in hair_renderers(sex):
        key = mesh_name
        if key in done:
            continue
        cover = footprint(points, mesh_name, strands)
        # Small holes in the footprint (the whorl at the crown, gaps between strips) are closed; a real bald patch is bigger and stays.
        closed = Image.fromarray((cover * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(11)).filter(ImageFilter.MinFilter(9))
        soft = np.asarray(closed.filter(ImageFilter.GaussianBlur(2.0)), dtype=np.float64) / 255.0
        alpha = np.clip(soft * STRENGTH, 0, 1) * scalp
        name = "under_%s_%s" % (sex[0], mesh_name.lower())
        path = os.path.join(ba.BUILD, "textures", name + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        Image.fromarray(np.dstack([grey, grey, grey, (alpha * 255).astype(np.uint8)]), "RGBA").save(path)
        done[key] = name
        covered = float((alpha > 0.5).mean())
        print("underlay %-7s %-22s %-22s covers %4.1f%% of the map" % (sex, variant, mesh_name, covered * 100))
        textures.append({"name": name, "file": "textures/" + name + ".png", "type": "color", "maxSize": SIZE})
        items.append({"id": name, "kind": "hair", "sex": sex, "mesh": mesh, "under": mesh_name, "skinFrom": None, "stats": stats,
                      "layers": [{"role": "scalp", "texture": name, "normal": None, "mask": None}]})
        previews.append((sex, variant, mesh_name, strands, name, vertices, normals, uv, triangles, head))
    return items, textures


def preview(entry):
    """Left pair: the game's hair alone. Right pair: with the underlay. From the front and from above-behind, far and near."""
    sex, variant, mesh_name, strands, name, vertices, normals, uv, triangles, head = entry
    sheets = []
    for with_cap in (False, True):
        scene = ba.reset_scene()
        camera = ba.setup_render(scene)
        info = hair_lib.HEADS[sex]
        ba.add_reference(info["head"], texture_name="T_FaceMale1_BC" if sex == "male" else None, only_submesh=0)
        ba.add_reference(info["body"], only_submesh=0)
        tint = (0.105, 0.062, 0.036)
        ref = ba.load_reference(mesh_name)
        # The game's strand textures are not part of the reference, so its strips are shown as plain half-clear ribbons:
        # enough to see where the scalp shows through them, which is what the underlay is for.
        ribbons = [ba.material("ribbon", tint=tint, roughness=0.6, specular=0.2)]
        ribbons[0].node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.45
        ba.make_object(mesh_name, ref["vertices"], [np.concatenate([ref["submeshes"][i] for i in strands if i < len(ref["submeshes"])])],
                       ref["uv"], ref["normals"], ribbons)
        if with_cap:
            texture = ba.image(os.path.join(ba.BUILD, "textures", name + ".png"))
            shade = tuple(c * 0.72 for c in tint)
            ba.make_object(name, vertices, [triangles], uv, normals, [ba.material("cap", tint=shade, texture=texture, alpha="clip", roughness=0.8,
                                                                                    two_sided=False, specular=0.05)])
        tag = "%s_%s" % (name, "with" if with_cap else "without")
        sheets.append(ba.render_views(scene, camera, head.centre, tag, [(0, 20), (150, 25)], distance=0.9, fov=25.0, size=360))
    from PIL import Image
    a, b = Image.open(sheets[0]), Image.open(sheets[1])
    both = Image.new("RGB", (a.width + b.width, a.height))
    both.paste(a, (0, 0))
    both.paste(b, (a.width, 0))
    out = os.path.join(ba.PREVIEWS, name + ".png")
    both.save(out)
    for s in sheets:
        os.remove(s)
    print("preview", out)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    items, textures, previews = [], [], []
    for sex in ("male", "female"):
        more_items, more_textures = build(sex, previews)
        items += more_items
        textures += more_textures
    with open(os.path.join(ba.BUILD, "catalogue_underlays.json"), "w") as f:
        json.dump({"items": items, "textures": textures}, f, indent=1)
    if "--no-preview" not in args:
        for entry in previews:
            preview(entry)


main()
