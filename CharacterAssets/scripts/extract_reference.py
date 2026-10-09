"""Reads the game's detailed character (rig, meshes, a few textures) into CharacterAssets/_reference.

LOCAL REFERENCE ONLY. Nothing written here is committed or shipped: the folder is git-ignored.
The Blender scripts fit new assets around these meshes and copy the skeleton's rest pose from them.

Run with Blender's Python (see build.ps1):
    python -I extract_reference.py <Big Ambitions_Data folder> <output folder> <pylib folder>
"""
import json
import os
import sys

data_dir, out_dir, pylib = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, pylib)

import numpy as np  # noqa: E402
import UnityPy  # noqa: E402
from UnityPy.helpers.MeshHelper import MeshHandler  # noqa: E402

SCENE = "level1"                                   # character creation; holds the detailed set
SHARED = "sharedassets1.assets"
ROOT = "PortraitGenerator/Setup/HumanDefinitionHigh"
TEXTURES = ("T_MaleSkinBase_BC", "T_FemSkin_BC", "T_FaceMale1_BC", "T_FemFace_BC", "T_MaleHair01_BC",
            "T_MaleSkinBase_N", "T_FaceMale1_N")

os.makedirs(os.path.join(out_dir, "meshes"), exist_ok=True)
os.makedirs(os.path.join(out_dir, "textures"), exist_ok=True)
env = UnityPy.load(os.path.join(data_dir, SCENE))


def transform_of(game_object):
    for component in game_object.m_Component:
        if component.component.type.name in ("Transform", "RectTransform"):
            return component.component.read()
    return None


def path_of(transform):
    names = []
    while transform is not None:
        names.append(transform.m_GameObject.read().m_Name)
        transform = transform.m_Father.read() if transform.m_Father else None
    return "/".join(reversed(names))


def trs(transform):
    p, r, s = transform.m_LocalPosition, transform.m_LocalRotation, transform.m_LocalScale
    return {"pos": [p.x, p.y, p.z], "rot": [r.x, r.y, r.z, r.w], "scale": [s.x, s.y, s.z]}


def matrix(m):
    return [[m.e00, m.e01, m.e02, m.e03], [m.e10, m.e11, m.e12, m.e13],
            [m.e20, m.e21, m.e22, m.e23], [m.e30, m.e31, m.e32, m.e33]]


index = {"renderers": [], "bones": {}}
meshes_done = {}

for obj in env.objects:
    if obj.type.name != "SkinnedMeshRenderer":
        continue
    renderer = obj.read()
    game_object = renderer.m_GameObject.read()
    path = path_of(transform_of(game_object))
    if not path.startswith(ROOT + "/") or not renderer.m_Mesh:
        continue
    mesh = renderer.m_Mesh.read()
    bones = []
    for bone in renderer.m_Bones:
        t = bone.read()
        name = t.m_GameObject.read().m_Name
        bones.append(name)
        if name not in index["bones"]:
            parent = t.m_Father.read() if t.m_Father else None
            index["bones"][name] = {"parent": parent.m_GameObject.read().m_Name if parent else None, **trs(t)}
    entry = {"path": path[len(ROOT) + 1:], "name": game_object.m_Name, "mesh": mesh.m_Name, "bones": bones,
             "materials": [m.read().m_Name if m else None for m in renderer.m_Materials]}
    index["renderers"].append(entry)

    if mesh.m_Name in meshes_done:
        continue
    handler = MeshHandler(mesh)
    handler.process()
    arrays = {
        "vertices": np.array(handler.m_Vertices, dtype=np.float32),
        "normals": np.array(handler.m_Normals, dtype=np.float32) if handler.m_Normals else np.zeros((0, 3), np.float32),
        "uv": np.array(handler.m_UV0, dtype=np.float32)[:, :2] if handler.m_UV0 else np.zeros((0, 2), np.float32),
        "bone_indices": np.array(handler.m_BoneIndices, dtype=np.int32) if handler.m_BoneIndices else np.zeros((0, 4), np.int32),
        "bone_weights": np.array(handler.m_BoneWeights, dtype=np.float32) if handler.m_BoneWeights else np.zeros((0, 4), np.float32),
        "bindposes": np.array([matrix(m) for m in mesh.m_BindPose], dtype=np.float32),
    }
    for i, triangles in enumerate(handler.get_triangles()):
        arrays["submesh_%d" % i] = np.array(triangles, dtype=np.int32).reshape(-1, 3)
    shapes = mesh.m_Shapes
    shape_names = []
    if shapes and shapes.channels:
        vertex_count = len(handler.m_Vertices)
        for channel in shapes.channels:
            shape = shapes.shapes[channel.frameIndex + channel.frameCount - 1]
            delta = np.zeros((vertex_count, 3), np.float32)
            for k in range(shape.firstVertex, shape.firstVertex + shape.vertexCount):
                v = shapes.vertices[k]
                delta[v.index] = (v.vertex.x, v.vertex.y, v.vertex.z)
            arrays["shape_%d" % len(shape_names)] = delta
            shape_names.append(channel.name)
    np.savez_compressed(os.path.join(out_dir, "meshes", mesh.m_Name + ".npz"), **arrays)
    meshes_done[mesh.m_Name] = {"shapes": shape_names, "vertices": len(handler.m_Vertices),
                                "submeshes": len(handler.get_triangles())}

index["meshes"] = meshes_done

# The rest of the skeleton above the bones (Armature and up to the character root).
for obj in env.objects:
    if obj.type.name == "Transform":
        t = obj.read()
        p = path_of(t)
        if p == ROOT + "/Model/Armature":
            index["armature"] = trs(t)
        elif p == ROOT + "/Model":
            index["model"] = trs(t)

wanted = set(TEXTURES)
shared = UnityPy.load(os.path.join(data_dir, SHARED))   # the scene's textures live in its shared-assets file
for obj in shared.objects:
    if obj.type.name == "Texture2D":
        texture = obj.read()
        if texture.m_Name in wanted:
            texture.image.save(os.path.join(out_dir, "textures", texture.m_Name + ".png"))
            wanted.discard(texture.m_Name)

with open(os.path.join(out_dir, "index.json"), "w") as f:
    json.dump(index, f, indent=1)
print("renderers", len(index["renderers"]), "meshes", len(meshes_done), "bones", len(index["bones"]),
      "textures missing", sorted(wanted))
