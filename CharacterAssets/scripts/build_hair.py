"""Builds every hairstyle: meshes, scalp textures, catalogue entries and preview sheets.

    blender -b -P build_hair.py -- [style id ...] [--no-preview]
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba
import hair_atlas
import hair_lib
import hair_styles

CLOSE = [(0, 5), (40, 8), (90, 5), (180, 8)]
WIDE = [(0, 5), (140, 10)]            # long hair: down to the chest
FAR = [(0, 20), (140, 20), (180, 20)]        # as the game shows the player: 3 m away, from above
TINTS = {"brown": (0.105, 0.062, 0.036)}
PIXELS_PER_METRE_INGAME = 294.0              # 1080p, 63 degree vertical field of view, 3 m


def preview(style, data):
    head = data["head"]
    atlas = ba.image(os.path.join(ba.BUILD, "textures", "hair_atlas.png"))
    for tint_name, tint in TINTS.items():
        scene = ba.reset_scene()
        camera = ba.setup_render(scene)
        info = hair_lib.HEADS[style["sex"]]
        ba.add_reference(info["head"], texture_name="T_FaceMale1_BC" if style["sex"] == "male" else None, only_submesh=0)
        ba.add_reference(info["body"], only_submesh=0)
        atlas = ba.image(os.path.join(ba.BUILD, "textures", "hair_atlas.png"))
        scalp = ba.image(os.path.join(ba.BUILD, "textures", data["scalp"] + ".png"))
        kinds = {"scalp": (scalp, "clip", False), "bulk": (atlas, "clip", True), "wisps": (atlas, "blend", True)}
        materials = [ba.material(role, tint=tint, texture=kinds[role][0], alpha=kinds[role][1], roughness=0.62,
                                 two_sided=kinds[role][2], specular=0.2) for role in data["roles"]]
        ba.make_object(style["id"], data["vertices"], data["submeshes"], data["uv"], data["normals"], materials)
        target = head.centre + (0, 0, 0.0)
        close = ba.render_views(scene, camera, target, style["id"] + "_" + tint_name, CLOSE, distance=0.72, fov=25.0, size=500)
        if style.get("long"):
            ba.render_views(scene, camera, target - (0, 0, 0.14), style["id"] + "_" + tint_name + "_wide", WIDE, distance=1.5, fov=25.0, size=500)
        size = int(0.6 * PIXELS_PER_METRE_INGAME)
        far = ba.render_views(scene, camera, target - (0, 0, 0.12), style["id"] + "_" + tint_name + "_far", FAR, distance=3.0,
                              fov=math.degrees(2 * math.atan(0.3 / 3.0)), size=size)
        from PIL import Image
        img = Image.open(far)
        img.resize((img.width * 3, img.height * 3), Image.NEAREST).save(far)
        print("preview", close, far)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wanted = [a for a in args if not a.startswith("--")]
    hair_atlas.build(os.path.join(ba.BUILD, "textures", "hair_atlas.png"))
    items, textures = [], [{"name": "hair_atlas", "file": "textures/hair_atlas.png", "type": "color", "maxSize": 1024}]
    for style in hair_styles.STYLES:
        if wanted and style["id"] not in wanted:
            continue
        item, extra, stats, data = hair_lib.build_style(style)
        item["stats"] = stats
        items.append(item)
        textures += extra
        if "--no-preview" not in args:
            preview(style, data)
    if not wanted:
        with open(os.path.join(ba.BUILD, "catalogue_hair.json"), "w") as f:
            json.dump({"items": items, "textures": textures}, f, indent=1)


main()
