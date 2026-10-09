"""Reads the game's detailed character COMPLETELY (hierarchy, meshes, materials, textures) into
CharacterAssets/_reference/preview, for the in-engine preview (PreviewProject).

LOCAL REFERENCE ONLY. Nothing written here is committed or shipped: the folder is git-ignored.
The preview project rebuilds the character from it so that a new look is judged under HDRP, next to the
game's own, before it goes anywhere near the bundle.

Run with Blender's Python (see preview.ps1):
    python -I extract_preview.py <Big Ambitions_Data folder> <output folder> <pylib folder>

Output:
    character.json      transforms, renderers, materials (shader, keywords, floats, colours, textures)
    meshes/<name>.bam   one binary mesh each (layout: see write_mesh)
    textures/<name>.png decoded as stored (normal maps keep their packed channels)
"""
import json
import os
import struct
import sys

data_dir, out_dir, pylib = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, pylib)

import numpy as np  # noqa: E402
import UnityPy  # noqa: E402
from UnityPy.helpers.MeshHelper import MeshHandler  # noqa: E402

SCENE = "level1"                                   # character creation; holds the detailed set
ROOT = "PortraitGenerator/Setup/HumanDefinitionHigh"

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


def matrix(m):
    return [m.e00, m.e01, m.e02, m.e03, m.e10, m.e11, m.e12, m.e13,
            m.e20, m.e21, m.e22, m.e23, m.e30, m.e31, m.e32, m.e33]


def write_mesh(path, mesh):
    """BAM1: counts, then vertex streams (only those flagged), bind poses, submesh index lists, blend shapes."""
    handler = MeshHandler(mesh)
    handler.process()
    count = len(handler.m_Vertices)

    def stream(values, width):
        if not values:
            return None
        array = np.array(values, dtype=np.float32)
        return array[:, :width] if array.shape[1] >= width else None

    streams = [np.array(handler.m_Vertices, dtype=np.float32)[:, :3],
               stream(handler.m_Normals, 3), stream(handler.m_Tangents, 4),
               stream(handler.m_UV0, 2), stream(handler.m_UV1, 2)]
    weights = None
    if handler.m_BoneIndices:
        # One, two or four influences per vertex depending on the mesh; a single one has no weight stream at all.
        indices = np.zeros((count, 4), np.int32)
        amounts = np.zeros((count, 4), np.float32)
        given = np.array(handler.m_BoneIndices, dtype=np.int32).reshape(count, -1)[:, :4]
        indices[:, :given.shape[1]] = given
        if handler.m_BoneWeights:
            given = np.array(handler.m_BoneWeights, dtype=np.float32).reshape(count, -1)[:, :4]
            amounts[:, :given.shape[1]] = given
        else:
            amounts[:, 0] = 1.0
        weights = (indices, amounts)
    flags = sum(1 << i for i, s in enumerate(streams) if s is not None) | (32 if weights else 0)
    triangles = handler.get_triangles()
    shapes = mesh.m_Shapes
    channels = shapes.channels if shapes and shapes.channels else []
    with open(path, "wb") as f:
        f.write(b"BAM1")
        f.write(struct.pack("<5i", count, len(triangles), len(mesh.m_BindPose), len(channels), flags))
        for s in streams:
            if s is not None:
                f.write(np.ascontiguousarray(s, dtype="<f4").tobytes())
        if weights:
            f.write(np.ascontiguousarray(weights[0], dtype="<i4").tobytes())
            f.write(np.ascontiguousarray(weights[1], dtype="<f4").tobytes())
        for pose in mesh.m_BindPose:
            f.write(struct.pack("<16f", *matrix(pose)))
        for submesh in triangles:
            indices = np.array(submesh, dtype="<i4").reshape(-1)
            f.write(struct.pack("<i", len(indices)))
            f.write(indices.tobytes())
        for channel in channels:
            shape = shapes.shapes[channel.frameIndex + channel.frameCount - 1]
            delta = np.zeros((count, 3), "<f4")
            normal = np.zeros((count, 3), "<f4")
            for k in range(shape.firstVertex, shape.firstVertex + shape.vertexCount):
                v = shapes.vertices[k]
                delta[v.index] = (v.vertex.x, v.vertex.y, v.vertex.z)
                normal[v.index] = (v.normal.x, v.normal.y, v.normal.z)
            name = channel.name.encode("utf-8")
            f.write(struct.pack("<i", len(name)))
            f.write(name)
            f.write(delta.tobytes())
            f.write(normal.tobytes())
    return {"vertices": count, "submeshes": len(triangles), "shapes": [c.name for c in channels]}


textures = {}


def texture_of(pointer):
    if not pointer or pointer.path_id == 0:
        return None
    try:
        texture = pointer.read()
    except Exception:
        return None
    if texture.object_reader.type.name != "Texture2D":
        return None
    name = texture.m_Name
    if name not in textures:
        try:
            texture.image.save(os.path.join(out_dir, "textures", name + ".png"))
        except Exception as error:  # an undecodable format: the preview falls back to a flat colour
            print("texture", name, "not decoded:", error)
            textures[name] = None
            return None
        textures[name] = {"width": texture.m_Width, "height": texture.m_Height,
                          "format": int(texture.m_TextureFormat), "srgb": int(getattr(texture, "m_ColorSpace", 1)),
                          "mips": int(getattr(texture, "m_MipCount", 1))}
    return name if textures[name] else None


materials = {}


def material_of(pointer):
    if not pointer or pointer.path_id == 0:
        return None
    material = pointer.read()
    name = material.m_Name
    if name in materials:
        return name
    shader = material.m_Shader.read() if material.m_Shader else None
    saved = material.m_SavedProperties
    entry = {"shader": shader.m_ParsedForm.m_Name if shader else None,
             "queue": int(material.m_CustomRenderQueue),
             "keywords": list(getattr(material, "m_ValidKeywords", None) or []),
             "disabledPasses": list(getattr(material, "disabledShaderPasses", None) or []),
             "floats": {str(k): float(v) for k, v in saved.m_Floats},
             "ints": {str(k): int(v) for k, v in (getattr(saved, "m_Ints", None) or [])},
             "colors": {str(k): [v.r, v.g, v.b, v.a] for k, v in saved.m_Colors},
             "textures": {}}
    for key, tex in saved.m_TexEnvs:
        texture = texture_of(tex.m_Texture)
        if texture:
            entry["textures"][str(key)] = {"name": texture, "scale": [tex.m_Scale.x, tex.m_Scale.y],
                                           "offset": [tex.m_Offset.x, tex.m_Offset.y]}
    materials[name] = entry
    return name


# Every transform under the character, parents before children.
transforms, index_of = [], {}
pending = []
for obj in env.objects:
    if obj.type.name == "Transform":
        t = obj.read()
        if path_of(t) == ROOT:
            pending.append((t, -1))
            break
while pending:
    t, parent = pending.pop(0)
    game_object = t.m_GameObject.read()
    p, r, s = t.m_LocalPosition, t.m_LocalRotation, t.m_LocalScale
    index_of[t.object_reader.path_id] = len(transforms)
    transforms.append({"name": game_object.m_Name, "parent": parent, "active": bool(game_object.m_IsActive),
                       "pos": [p.x, p.y, p.z], "rot": [r.x, r.y, r.z, r.w], "scale": [s.x, s.y, s.z]})
    me = len(transforms) - 1
    for child in t.m_Children:
        pending.append((child.read(), me))

renderers, meshes = [], {}
for obj in env.objects:
    if obj.type.name != "SkinnedMeshRenderer":
        continue
    renderer = obj.read()
    game_object = renderer.m_GameObject.read()
    own = transform_of(game_object)
    if own.object_reader.path_id not in index_of or not renderer.m_Mesh:
        continue
    mesh = renderer.m_Mesh.read()
    if mesh.m_Name not in meshes:
        meshes[mesh.m_Name] = write_mesh(os.path.join(out_dir, "meshes", mesh.m_Name + ".bam"), mesh)
    renderers.append({
        "transform": index_of[own.object_reader.path_id],
        "path": path_of(own)[len(ROOT) + 1:],
        "mesh": mesh.m_Name,
        "enabled": bool(renderer.m_Enabled),
        "bones": [index_of.get(b.path_id, -1) for b in renderer.m_Bones],
        "rootBone": index_of.get(renderer.m_RootBone.path_id, -1) if renderer.m_RootBone else -1,
        "weights": [float(w) for w in (renderer.m_BlendShapeWeights or [])],
        "materials": [material_of(m) for m in renderer.m_Materials],
    })

with open(os.path.join(out_dir, "character.json"), "w") as f:
    json.dump({"transforms": transforms, "renderers": renderers, "meshes": meshes, "materials": materials,
               "textures": {k: v for k, v in textures.items() if v}}, f, indent=1)
print("transforms", len(transforms), "renderers", len(renderers), "meshes", len(meshes),
      "materials", len(materials), "textures", len([t for t in textures.values() if t]))
shaders = {}
for m in materials.values():
    shaders[m["shader"]] = shaders.get(m["shader"], 0) + 1
print("shaders", shaders)
