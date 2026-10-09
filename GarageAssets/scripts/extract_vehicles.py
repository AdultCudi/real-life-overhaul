"""Reads how the vanilla game builds its player vehicles, so the Garage feature can be written against
facts: for every prefab that carries a CarController or ScooterController, its hierarchy (object names,
local position and scale), the mesh and mesh bounds of every renderer, the materials on it with their shader
and every saved float / colour / texture property name, and its colliders.

LOCAL REFERENCE ONLY: reads the installed game, writes one JSON; nothing from it is committed or shipped.

Run with Blender's Python (any Python 3 that can import the UnityPy in the pylib folder works):
    python -I extract_vehicles.py <Big Ambitions_Data folder> <out.json> <pylib folder with UnityPy>
"""
import json
import os
import sys

data_dir, out_path, pylib = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, pylib)
import UnityPy  # noqa: E402

aa = os.path.join(data_dir, "StreamingAssets", "aa", "StandaloneWindows64")
files = [os.path.join(aa, f) for f in os.listdir(aa) if "assets_prefabs_" in f or "vehicletypes" in f or "sharedshaders" in f]
env = UnityPy.load(*files)

WANTED = ("CarController", "ScooterController", "VehicleSpawnerController")


def v3(v):
    return [round(v.x, 3), round(v.y, 3), round(v.z, 3)]


def script_name(mono):
    try:
        return mono.m_Script.read().m_ClassName
    except Exception:
        return "?"


materials = {}


def material(pptr):
    try:
        m = pptr.read()
    except Exception:
        return None
    name = m.m_Name
    if name not in materials:
        try:
            shader = m.m_Shader.read()
            shader_name = shader.m_ParsedForm.m_Name
            props = [(p.m_Name, p.m_Description, int(p.m_Type)) for p in shader.m_ParsedForm.m_PropInfo.m_Props]
        except Exception:
            shader_name, props = "?", []
        saved = m.m_SavedProperties
        materials[name] = {
            "shader": shader_name,
            "shaderProps": props,
            "floats": {k: round(v, 4) for k, v in saved.m_Floats},
            "colors": {k: [round(v.r, 3), round(v.g, 3), round(v.b, 3), round(v.a, 3)] for k, v in saved.m_Colors},
            "textures": [k for k, v in saved.m_TexEnvs if v.m_Texture.m_PathID != 0],
            "keywords": list(getattr(m, "m_ValidKeywords", []) or []),
        }
    return name


def node(go, depth):
    out = {"name": go.m_Name, "active": bool(go.m_IsActive), "layer": go.m_Layer}
    transform = None
    comps = []
    for c in go.m_Component:
        ptr = c.component if hasattr(c, "component") else c[1]
        try:
            obj = ptr.read()
        except Exception:
            continue
        kind = ptr.type.name
        if kind == "Transform":
            transform = obj
            out["pos"] = v3(obj.m_LocalPosition)
            out["scale"] = v3(obj.m_LocalScale)
            q = obj.m_LocalRotation
            out["rot"] = [round(q.x, 3), round(q.y, 3), round(q.z, 3), round(q.w, 3)]
        elif kind == "MonoBehaviour":
            comps.append(script_name(obj))
        elif kind == "MeshFilter":
            try:
                mesh = obj.m_Mesh.read()
                out["mesh"] = mesh.m_Name
                out["meshCentre"] = v3(mesh.m_LocalAABB.m_Center)
                out["meshExtent"] = v3(mesh.m_LocalAABB.m_Extent)
                out["submeshes"] = len(mesh.m_SubMeshes)
            except Exception:
                out["mesh"] = "?"
        elif kind in ("MeshRenderer", "SkinnedMeshRenderer"):
            out["materials"] = [material(m) for m in obj.m_Materials]
            comps.append(kind)
        elif kind == "BoxCollider":
            out["box"] = {"centre": v3(obj.m_Center), "size": v3(obj.m_Size), "trigger": bool(obj.m_IsTrigger)}
        elif kind == "MeshCollider":
            try:
                mesh = obj.m_Mesh.read()
                out["meshCollider"] = {"mesh": mesh.m_Name, "centre": v3(mesh.m_LocalAABB.m_Center), "extent": v3(mesh.m_LocalAABB.m_Extent)}
            except Exception:
                out["meshCollider"] = "?"
        elif kind == "Rigidbody":
            out["mass"] = round(obj.m_Mass, 1)
        else:
            comps.append(kind)
    if comps:
        out["components"] = comps
    if transform is not None and depth < 7:
        kids = []
        for child in transform.m_Children:
            try:
                kids.append(node(child.read().m_GameObject.read(), depth + 1))
            except Exception:
                pass
        if kids:
            out["children"] = kids
    return out


result = {"vehicles": {}, "vehicleTypes": {}, "scripts": {}}
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        mono = obj.read()
        name = script_name(mono)
    except Exception:
        continue
    if name == "VehicleType":
        try:
            tree = obj.read_typetree()
            result["vehicleTypes"][tree.get("vehicleTypeName", "?")] = {k: v for k, v in tree.items() if isinstance(v, (int, float, str, bool))}
        except Exception as e:
            result["vehicleTypes"][str(obj.path_id)] = str(e)
        continue
    if name not in WANTED:
        continue
    try:
        go = mono.m_GameObject.read()
        entry = node(go, 0)
        try:
            tree = obj.read_typetree()
            entry["script"] = {k: v for k, v in tree.items() if isinstance(v, (int, float, str, bool))}
        except Exception:
            pass
        result["vehicles"][name + ":" + go.m_Name] = entry
    except Exception as e:
        result["vehicles"][name + ":" + str(obj.path_id)] = str(e)

result["materials"] = materials
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(result, f, indent=1)
print(len(result["vehicles"]), "vehicles,", len(materials), "materials,", len(result["vehicleTypes"]), "types")
