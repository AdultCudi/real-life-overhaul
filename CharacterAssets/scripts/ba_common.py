"""Shared helpers for the Blender scripts: the game's reference meshes, coordinate systems, export, preview renders.

Spaces
------
Unity space: left-handed, Y up, the character faces +Z. "Canonical" = the bind space of the game's male head
mesh (MaleFace1). Every mod mesh is exported in canonical Unity space; at runtime the mod gives it that
mesh's bind poses, so it lines up with the game's skeleton by construction.
Blender space: right-handed, Z up, the character faces -Y. unity (x, y, z) <-> blender (-x, -z, y).
The change of handedness reverses triangle winding, so triangles are flipped on the way in and out.
"""
import json
import math
import os

import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REFERENCE = os.path.join(ROOT, "_reference")
BUILD = os.path.join(ROOT, "build")
PREVIEWS = os.path.join(ROOT, "previews")
CANONICAL_MESH = "MaleFace1"
sys.path.append(os.path.join(ROOT, "_tools", "pylib"))   # Pillow, for contact sheets and texture painting

_index = None
_canonical = None


def index():
    global _index
    if _index is None:
        with open(os.path.join(REFERENCE, "index.json")) as f:
            _index = json.load(f)
    return _index


def bone_names():
    return index()["renderers"][0]["bones"]


def u2b(v):
    v = np.asarray(v, dtype=np.float64)
    return np.stack([-v[..., 0], -v[..., 2], v[..., 1]], axis=-1)


def b2u(v):
    v = np.asarray(v, dtype=np.float64)
    return np.stack([-v[..., 0], v[..., 2], -v[..., 1]], axis=-1)


def _raw(mesh_name):
    return np.load(os.path.join(REFERENCE, "meshes", mesh_name + ".npz"))


def canonical_bone_matrices():
    """World matrix of every bone in the canonical bind pose (Unity space), in the game's bone order."""
    global _canonical
    if _canonical is None:
        _canonical = np.linalg.inv(_raw(CANONICAL_MESH)["bindposes"].astype(np.float64))
    return _canonical


def bone_head(name):
    """Canonical position of a bone's origin, Blender space."""
    return u2b(canonical_bone_matrices()[bone_names().index(name)][:3, 3])


def load_reference(mesh_name):
    """A game mesh brought into canonical space. Returns Blender-space arrays.

    Game meshes do not share bind poses (some are stored at another scale), so each is skinned with its
    own bind poses onto the canonical bone positions.
    """
    raw = _raw(mesh_name)
    vertices = raw["vertices"].astype(np.float64)
    indices, weights = raw["bone_indices"], raw["bone_weights"].astype(np.float64)
    if len(weights) != len(vertices):
        # Stored without weights (hats): rigid on the skull.
        indices = np.full((len(vertices), 4), bone_names().index("Skull"), dtype=np.int64)
        weights = np.tile(np.array([1.0, 0.0, 0.0, 0.0]), (len(vertices), 1))
    weights = weights / np.maximum(weights.sum(axis=1, keepdims=True), 1e-9)
    per_bone = canonical_bone_matrices() @ raw["bindposes"].astype(np.float64)      # (bones, 4, 4)
    per_vertex = np.einsum("vk,vkij->vij", weights, per_bone[indices])              # (verts, 4, 4)
    homogeneous = np.concatenate([vertices, np.ones((len(vertices), 1))], axis=1)
    position = np.einsum("vij,vj->vi", per_vertex, homogeneous)[:, :3]
    linear = per_vertex[:, :3, :3]
    normals = raw["normals"].astype(np.float64)
    if len(normals):
        normals = np.einsum("vij,vj->vi", linear, normals)
        normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
    submeshes = []
    i = 0
    while "submesh_%d" % i in raw.files:
        submeshes.append(raw["submesh_%d" % i][:, ::-1].copy())     # winding flips with handedness
        i += 1
    names = index()["meshes"][mesh_name]["shapes"]
    shapes = {}
    for k, shape in enumerate(names):
        shapes[shape] = u2b(np.einsum("vij,vj->vi", linear, raw["shape_%d" % k].astype(np.float64)))
    return {"name": mesh_name, "vertices": u2b(position), "normals": u2b(normals) if len(normals) else normals,
            "uv": raw["uv"].astype(np.float64), "submeshes": submeshes, "bone_indices": indices,
            "bone_weights": weights, "shapes": shapes, "shape_order": names}


def write_mesh(name, vertices, normals, uv, submeshes, bone_indices, bone_weights, shapes=None):
    """Writes build/meshes/<name>.json in canonical Unity space. Inputs are Blender-space arrays.

    submeshes: list of (n, 3) int arrays, counter-clockwise as seen from outside (Blender convention).
    shapes: list of (shape name, (verts, 3) deltas), in the order the game expects them.
    """
    os.makedirs(os.path.join(BUILD, "meshes"), exist_ok=True)
    weights = np.asarray(bone_weights, dtype=np.float64)
    weights = weights / np.maximum(weights.sum(axis=1, keepdims=True), 1e-9)
    data = {
        "name": name,
        "vertices": np.round(b2u(vertices), 5).ravel().tolist(),
        "normals": np.round(b2u(normals), 4).ravel().tolist(),
        "uv": np.round(np.asarray(uv), 5).ravel().tolist(),
        "boneIndices": np.asarray(bone_indices, dtype=np.int64).ravel().tolist(),
        "boneWeights": np.round(weights, 4).ravel().tolist(),
        "submeshes": [{"triangles": np.asarray(t, dtype=np.int64)[:, ::-1].ravel().tolist()} for t in submeshes],
        "shapes": [{"name": n, "deltas": np.round(b2u(d), 5).ravel().tolist()} for n, d in (shapes or [])],
    }
    path = os.path.join(BUILD, "meshes", name + ".json")
    with open(path, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    triangles = sum(len(t) for t in submeshes)
    print("mesh %-22s %6d vertices %6d triangles %d submeshes %d shapes" % (name, len(vertices), triangles, len(submeshes), len(shapes or [])))
    return {"vertices": int(len(vertices)), "triangles": int(triangles)}


def bone_weights_single(count, bone):
    """Everything rigid on one bone."""
    indices = np.zeros((count, 4), dtype=np.int64)
    indices[:, 0] = bone_names().index(bone)
    weights = np.zeros((count, 4))
    weights[:, 0] = 1.0
    return indices, weights


# ---------------------------------------------------------------------------------------------- Blender side

def reset_scene():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def make_object(name, vertices, submeshes, uv=None, normals=None, materials=None, smooth=True):
    """A Blender mesh object for previews. One material slot per submesh."""
    import bpy
    faces = np.concatenate(submeshes) if len(submeshes) else np.zeros((0, 3), dtype=np.int64)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(np.asarray(vertices).tolist(), [], faces.tolist())
    if uv is not None and len(uv):
        layer = mesh.uv_layers.new(name="UVMap")
        loops = np.zeros(len(mesh.loops), dtype=np.int64)
        mesh.loops.foreach_get("vertex_index", loops)
        layer.data.foreach_set("uv", np.asarray(uv)[loops].ravel())
    if materials:
        for material in materials:
            mesh.materials.append(material)
        slots = np.concatenate([np.full(len(t), i, dtype=np.int64) for i, t in enumerate(submeshes)])
        mesh.polygons.foreach_set("material_index", slots)
    if smooth:
        mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    if normals is not None and len(normals):
        mesh.normals_split_custom_set_from_vertices(np.asarray(normals).tolist())
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def image(path, colour=True):
    import bpy
    img = bpy.data.images.load(path, check_existing=True)
    if not colour:
        img.colorspace_settings.name = "Non-Color"
    return img


def material(name, tint=(1, 1, 1), texture=None, alpha=None, roughness=0.6, two_sided=True, specular=0.5, normal=None):
    """Principled preview material. alpha: None (opaque), 'clip' (dithered, like alpha test) or 'blend'."""
    import bpy
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes["Principled BSDF"]
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Specular IOR Level"].default_value = specular
    shader.inputs["Base Color"].default_value = (*tint, 1)
    if texture is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = texture
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        links.new(tex.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = (*tint, 1)
        links.new(mix.outputs[2], shader.inputs["Base Color"])
        if alpha:
            links.new(tex.outputs["Alpha"], shader.inputs["Alpha"])
    if normal is not None:
        ntex = nodes.new("ShaderNodeTexImage")
        ntex.image = normal
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], shader.inputs["Normal"])
    mat.surface_render_method = "BLENDED" if alpha == "blend" else "DITHERED"
    mat.use_backface_culling = not two_sided
    return mat


def add_reference(mesh_name, tint=(0.9, 0.74, 0.58), texture_name=None, only_submesh=None):
    """A game mesh in the preview scene, skin-coloured."""
    ref = load_reference(mesh_name)
    texture = None
    if texture_name:
        path = os.path.join(REFERENCE, "textures", texture_name + ".png")
        if os.path.exists(path):
            texture = image(path)
    submeshes = ref["submeshes"] if only_submesh is None else [ref["submeshes"][only_submesh]]
    mats = [material(mesh_name + "_%d" % i, tint=tint, texture=texture, roughness=0.55, two_sided=False) for i in range(len(submeshes))]
    return make_object(mesh_name, ref["vertices"], submeshes, ref["uv"], ref["normals"], mats), ref


def setup_render(scene, width=900, height=900, samples=48):
    import bpy
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.film_transparent = False
    scene.eevee.taa_render_samples = samples
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.58, 0.62, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    scene.world = world

    def sun(name, energy, rotation):
        light = bpy.data.lights.new(name, "SUN")
        light.energy = energy
        light.angle = math.radians(8)
        obj = bpy.data.objects.new(name, light)
        obj.rotation_euler = [math.radians(a) for a in rotation]
        scene.collection.objects.link(obj)

    sun("Key", 3.2, (50, 0, 35))
    sun("Fill", 1.0, (65, 0, -70))
    sun("Rim", 1.6, (-55, 0, 160))
    camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def aim(camera, target, azimuth, elevation, distance, fov=25.0):
    """azimuth 0 = from the front, 90 = from the character's left, 180 = from behind."""
    from mathutils import Vector
    az, el = math.radians(azimuth), math.radians(elevation)
    offset = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * distance
    camera.location = Vector(target) + offset
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.sensor_fit = "VERTICAL"
    camera.data.angle_y = math.radians(fov)
    camera.data.clip_start, camera.data.clip_end = 0.05, 100


def render_views(scene, camera, target, name, views, distance=1.1, fov=25.0, size=640):
    """Renders several angles and joins them into one contact sheet previews/<name>.png."""
    import bpy
    os.makedirs(PREVIEWS, exist_ok=True)
    scene.render.resolution_x = scene.render.resolution_y = size
    tiles = []
    for i, (azimuth, elevation) in enumerate(views):
        aim(camera, target, azimuth, elevation, distance, fov)
        path = os.path.join(PREVIEWS, "_tile_%s_%d.png" % (name, i))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        tiles.append(path)
    from PIL import Image
    sheet = Image.new("RGB", (size * len(tiles), size))
    for i, path in enumerate(tiles):
        sheet.paste(Image.open(path).convert("RGB"), (size * i, 0))
        os.remove(path)
    out = os.path.join(PREVIEWS, name + ".png")
    sheet.save(out)
    return out
