"""Two made-up models for build.ps1 -Test, which proves the path of a Blender export into a bundle without shipping
anything: test_bag_glb (a .glb whose painted picture is the one its material carries) and test_box_fbx (an .fbx with
its picture and mask beside it as test_box_fbx.png and test_box_fbx_mask.png). Each has one painted material and one
swatch (swatch_paper on its top), UVs laid by hand, the front looking along +Y, the pivot at the ground centre.

    blender -b -P test_models.py -- <models folder>
"""
import json
import os
import sys

import bmesh
import bpy

OUT = sys.argv[sys.argv.index("--") + 1]
CELLS = 4      # the picture is cut into 4 x 2 cells, one a face


def picture(name, size, path):
    """A picture with a coloured cell for each face and a frame round each, saved as a PNG."""
    image = bpy.data.images.new(name, size, size // 2)
    pixels = []
    colours = [(0.85, 0.20, 0.10), (0.10, 0.45, 0.80), (0.95, 0.75, 0.10), (0.20, 0.60, 0.25), (0.60, 0.30, 0.70), (0.90, 0.90, 0.85)]
    cell = size // CELLS
    for y in range(size // 2):
        for x in range(size):
            k = (y // cell) * CELLS + x // cell
            edge = x % cell < 3 or y % cell < 3
            r, g, b = (0.05, 0.05, 0.05) if edge else colours[k % len(colours)]
            pixels += [r, g, b, 1.0]
    image.pixels = pixels
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


def material(name, image=None):
    made = bpy.data.materials.new(name)      # with its node tree: Blender 5 always gives one
    if image is not None:
        nodes = made.node_tree.nodes
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        made.node_tree.links.new(texture.outputs["Color"], nodes["Principled BSDF"].inputs["Base Color"])
    return made


def box_object(name, size, painted):
    """A box standing on the ground, its top in swatch_paper, its other faces drawn from cells of the picture."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x, v.co.y, v.co.z = v.co.x * size[0], v.co.y * size[1], (v.co.z + 0.5) * size[2]
    uv = bm.loops.layers.uv.new("UVMap")
    bm.faces.ensure_lookup_table()
    for k, face in enumerate(bm.faces):
        face.material_index = 1 if face.normal.z > 0.9 else 0
        cx, cy = k % CELLS, k // CELLS % 2
        corners = [(0.02, 0.02), (0.98, 0.02), (0.98, 0.98), (0.02, 0.98)]
        for loop, (s, t) in zip(face.loops, corners):
            loop[uv].uv = ((cx + s) / CELLS, (cy + t) / 2)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(painted)
    mesh.materials.append(material("swatch_paper"))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def export(obj, path):
    for other in bpy.context.scene.objects:
        other.select_set(other is obj)
    bpy.context.view_layer.objects.active = obj
    if path.endswith(".glb"):
        bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True)
    else:
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True)


def settings(folder, name, budget, note):
    with open(os.path.join(folder, name + ".json"), "w", encoding="utf-8") as f:
        json.dump({"budget": budget, "material": "lit", "pivot": "ground centre", "note": note}, f, indent=1)


bpy.ops.wm.read_factory_settings(use_empty=True)
for name, ext, size, own in (("test_bag_glb", ".glb", (0.30, 0.18, 0.34), False), ("test_box_fbx", ".fbx", (0.22, 0.22, 0.08), True)):
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    image = picture(name + "_picture", 256, os.path.join(folder, name + ".png") if own else os.path.join(OUT, name + "_source.png"))
    obj = box_object(name, size, material(name + "_painted", image))
    export(obj, os.path.join(folder, name + ext))
    if own:
        mask = bpy.data.images.new(name + "_mask", 128, 64, alpha=True)
        mask.pixels = [0.0, 1.0, 0.0, 0.6] * (128 * 64)
        mask.filepath_raw = os.path.join(folder, name + "_mask.png")
        mask.file_format = "PNG"
        mask.save()
    settings(folder, name, 40, "a made-up model of build.ps1 -Test")
    print("test %s: %s" % (name, os.path.join(folder, name + ext)))
