"""Props made by hand in Blender and exported (README, "Adding a prop from Blender"), for a set that takes models.

    blender -b -P models.py -- --scan      reads every model of the set, saves each one's painted picture into the build
                                           folder and lists what atlas.py must paint for them (build/models.json)
    import models; models.PROPS()          inside build_props.py: a (maker, budget) for each model, as props.py lists

A model is a folder models/<id>/ holding <id>.glb (or .gltf or .fbx), <id>.json ({"budget": triangles, "material":
"lit" or "glow", "pivot": ..., "note": ..., "sharp": degrees}) and, if it wants them, <id>.png (its painted picture,
in place of the one its material carries) and <id>_mask.png (HDRP mask: R metallic, G occlusion, A smoothness).

Each material of the model is either a swatch of the atlas, named swatch_<name> (that finish, grimier towards the
bottom, as the procedural props are painted), or the one painted material, whose picture goes into the set's atlas
whole and whose UVs are drawn into its region, or, for a building (README, "Buildings"), one the game draws with a
material of its own: the model's .json lists those slots under "game", {"<slot>": {"kind": "brick", "tile": 1.65}}, and
they become submeshes after the atlas's, keeping the model's world-scale UVs (its second UV map, 1 unit a metre) divided by
`tile`, the metres one repeat of the game's picture covers. The model is read in Blender's own space, exactly as it was made: X
right, Y forward (the prop's front looks along +Y), Z up, metres, the origin at the pivot; prop_kit turns that into
Unity's on the way out, as for every prop.
"""
import json
import os
import re
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import sets  # noqa: E402
from prop_kit import Paint, Prop  # noqa: E402

FORMATS = (".glb", ".gltf", ".fbx")
ID = re.compile(r"^[a-z][a-z0-9_]*$")
SWATCH = re.compile(r"^swatch_([a-z0-9_]+?)(?:\.\d{3})?$")       # Blender adds .001 to a copied material's name
NOT_SHIPPED = re.compile(r"^(?:UCX|UBX|UCP|USP)_|_LOD[1-9]\d*$", re.I)
LARGEST_PICTURE = 1024      # pixels a side: a prop seen in the street needs 256 or 512
GAME_KINDS = ("brick", "roof", "glass", "fence")   # what the mod's CampusLookRules names a game material for (Core/GameLook); one submesh each
SIZE_RANGE = (0.01, 60.0)   # metres, the prop's largest side: outside it the export's scale is wrong (centimetres?)


def folders():
    """The set's model folders, by id."""
    if not sets.MODELS or not os.path.isdir(sets.MODELS):
        return []
    return sorted(name for name in os.listdir(sets.MODELS) if os.path.isdir(os.path.join(sets.MODELS, name)))


def source(model):
    """The model's export and its settings, refused with the reason when the folder does not follow the rules."""
    if not ID.match(model):
        raise SystemExit("models/%s: a prop id is lower case letters, digits and _ (it is the folder's name)" % model)
    folder = os.path.join(sets.MODELS, model)
    found = [f for f in os.listdir(folder) if os.path.splitext(f)[0] == model and os.path.splitext(f)[1].lower() in FORMATS]
    if len(found) != 1:
        raise SystemExit("models/%s: wants exactly one of %s, found %s" % (model, ", ".join(model + f for f in FORMATS), found or "none"))
    settings_file = os.path.join(folder, model + ".json")
    if not os.path.exists(settings_file):
        raise SystemExit("models/%s: no %s.json (its triangle budget and material)" % (model, model))
    with open(settings_file, encoding="utf-8") as f:
        settings = json.load(f)
    if not isinstance(settings.get("budget"), int) or settings["budget"] <= 0:
        raise SystemExit("models/%s: %s.json gives no triangle budget" % (model, model))
    if settings.get("material", "lit") not in ("lit", "glow"):
        raise SystemExit("models/%s: material is lit or glow" % model)
    return os.path.join(folder, found[0]), settings


def imported(path):
    """Imports the export into the open scene and returns its mesh objects; everything it brought is removed by tidy()."""
    before = set(bpy.data.objects)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    name = os.path.basename(path)
    for obj in new:
        if obj.type == "ARMATURE":
            tidy(new)
            raise SystemExit("%s: has a rig; a prop is one static mesh (README, 'Adding a prop from Blender')" % name)
        if obj.type == "MESH" and NOT_SHIPPED.search(obj.name):
            tidy(new)
            raise SystemExit("%s: object %s is a level of detail or a collision mesh; neither ships (README: one mesh, the "
                             "callers give it a box from PropLibrary.SizeOf if it must be hit)" % (name, obj.name))
    meshes = [o for o in new if o.type == "MESH"]
    if not meshes:
        tidy(new)
        raise SystemExit("%s: no mesh in it" % name)
    return new, meshes


def tidy(objects):
    for obj in objects:
        data = obj.data if obj.type == "MESH" else None
        bpy.data.objects.remove(obj, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def joined(meshes):
    """The model's meshes as one bmesh in world space, modifiers applied, and the material name of each face."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    names = []
    for obj in meshes:
        part = bmesh.new()
        part.from_object(obj, depsgraph)
        part.transform(obj.matrix_world)
        slots = [slot.material.name if slot.material else "" for slot in obj.material_slots] or [""]
        offset = len(names)
        names += slots
        for face in part.faces:
            face.material_index = offset + min(face.material_index, len(slots) - 1)
        mesh = bpy.data.meshes.new("_joined")
        part.to_mesh(mesh)
        part.free()
        bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
    return bm, names


def game_slots(settings, model):
    """The slots a building draws with the game's own materials: slot name -> (kind, metres one repeat covers)."""
    out = {}
    for slot, rule in (settings.get("game") or {}).items():
        kind, tile = rule.get("kind"), float(rule.get("tile", 0))
        if kind not in GAME_KINDS or tile <= 0:
            raise SystemExit("models/%s: game slot %s wants a kind of %s and a tile size in metres above 0" % (model, slot, ", ".join(GAME_KINDS)))
        out[slot] = (kind, tile)
    return out


def kinds(names, model, game=None):
    """Each material name -> ("swatch", name), ("game", kind) or ("picture", region); a model has one painted material at most."""
    out, painted = {}, []
    for name in sorted(set(names)):
        base = name.split(".")[0] if re.search(r"\.\d{3}$", name) else name
        if game and base in game:
            out[name] = ("game", game[base][0])
            continue
        m = SWATCH.match(name)
        if m:
            out[name] = ("swatch", m.group(1))
        else:
            painted.append(name)
            out[name] = ("picture", "model_" + model)
    if len(painted) > 1:
        raise SystemExit("models/%s: %d painted materials (%s); bake them into one picture, or name the plain ones swatch_<name>"
                         % (model, len(painted), ", ".join(n or "(none)" for n in painted)))
    return out


def base_image(material):
    """The picture behind a material's base colour, however many nodes lie between."""
    if material is None or material.node_tree is None:
        return None
    shader = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if shader is None:
        return None
    todo, seen = [link.from_node for link in shader.inputs["Base Color"].links], set()
    while todo:
        node = todo.pop()
        if node in seen:
            continue
        seen.add(node)
        if node.type == "TEX_IMAGE" and node.image is not None:
            return node.image
        for socket in node.inputs:
            todo += [link.from_node for link in socket.links]
    return None


def finish(material):
    """The material's own metallic and smoothness, for a model without a mask picture."""
    shader = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if material and material.node_tree else None
    if shader is None:
        return 0.0, 0.3
    return float(shader.inputs["Metallic"].default_value), 1.0 - float(shader.inputs["Roughness"].default_value)


def finish_colour(material):
    """A game material's stand-in when the game has none: the model's own colour (sRGB, 0..1), metallic and smoothness."""
    shader = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if material and material.node_tree else None
    if shader is None:
        return [0.5, 0.5, 0.5], 0.0, 0.3
    linear = list(shader.inputs["Base Color"].default_value)[:3]
    srgb = [round(12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055, 3) for c in linear]
    return srgb, float(shader.inputs["Metallic"].default_value), 1.0 - float(shader.inputs["Roughness"].default_value)


def scan():
    """Writes build/models.json and each model's picture: what atlas.py paints for the set."""
    out = []
    pictures = os.path.join(sets.BUILD, "models")
    os.makedirs(pictures, exist_ok=True)
    for model in folders():
        path, settings = source(model)
        new, meshes = imported(path)
        try:
            names = sorted({slot.material.name if slot.material else "" for obj in meshes for slot in obj.material_slots} or {""})
            used = kinds(names, model, game_slots(settings, model))
            entry = {"id": model, "file": path, "swatches": sorted({v[1] for v in used.values() if v[0] == "swatch"}), "pictures": []}
            for name, (kind, region) in used.items():
                if kind != "picture":
                    continue
                material = bpy.data.materials.get(name) if name else None
                own = os.path.join(sets.MODELS, model, model + ".png")
                target = os.path.join(pictures, model + ".png")
                if os.path.exists(own):
                    image = bpy.data.images.load(own)
                else:
                    image = base_image(material)
                    if image is None:
                        raise SystemExit("models/%s: material %s has no picture; bake one, give %s.png beside the model, or name "
                                         "the material swatch_<name>" % (model, name or "(none)", model))
                width, height = image.size
                if max(width, height) > LARGEST_PICTURE or min(width, height) < 4:
                    raise SystemExit("models/%s: its picture is %d x %d; %d px a side at most" % (model, width, height, LARGEST_PICTURE))
                image.save(filepath=target)
                metallic, smoothness = finish(material)
                mask = os.path.join(sets.MODELS, model, model + "_mask.png")
                entry["pictures"].append({"region": region, "material": name, "file": target, "mask": mask if os.path.exists(mask) else None,
                                          "metallic": round(metallic, 3), "smoothness": round(smoothness, 3)})
            out.append(entry)
            print("model %-22s %s, swatches %s, %d picture(s)" % (model, os.path.basename(path), ", ".join(entry["swatches"]) or "none", len(entry["pictures"])))
        finally:
            tidy(new)
    with open(os.path.join(sets.BUILD, "models.json"), "w", encoding="utf-8") as f:
        json.dump({"models": out}, f, indent=1)
    print("models: %d in the set '%s'" % (len(out), sets.NAME))


def maker(model):
    """A maker as props.py lists them: the model read again and painted onto the atlas atlas.py made."""
    def make():
        path, settings = source(model)
        new, meshes = imported(path)
        try:
            bm, names = joined(meshes)
        finally:
            tidy(new)
        game = game_slots(settings, model)
        origin = {}
        # A slot's faces above a height drawn with another game kind (a building's rooftop fence is the glass slot of the
        # model): {"slot": ..., "above": metres, "kind": ..., "tile": ...}. A virtual slot of its own, after the model's.
        for rule in settings.get("split") or []:
            if rule.get("kind") not in GAME_KINDS or float(rule.get("tile", 0)) <= 0 or rule["slot"] not in game:
                raise SystemExit("models/%s: a split of %s wants a kind of %s, a tile size above 0 and a slot that is a game slot" % (model, rule.get("slot"), ", ".join(GAME_KINDS)))
            sources = [i for i, n in enumerate(names) if n.split(".")[0] == rule["slot"]]
            names.append(rule["slot"] + "#" + rule["kind"])
            origin[names[-1]] = rule["slot"]
            game[names[-1]] = (rule["kind"], float(rule["tile"]))
            for face in bm.faces:
                if face.material_index in sources and face.calc_center_median().z > float(rule["above"]):
                    face.material_index = len(names) - 1
        used = kinds(names, model, game)
        prop = Prop(model, settings.get("material", "lit"), pivot=settings.get("pivot", "ground centre"), note=settings.get("note", ""))
        if game:
            layers = bm.loops.layers.uv
            if len(layers.keys()) < 2:
                raise SystemExit("models/%s: a game material wants the model's second UV map (world scale, 1 unit a metre); it has one" % model)
            world_source = layers.values()[1]
            world = layers.new("world")                       # a layer of a name of its own: the kit's UVMap is made on each part
            for face in bm.faces:
                for loop in face.loops:
                    loop[world].uv = loop[world_source].uv
        uv = bm.loops.layers.uv.verify() if bm.loops.layers.uv.active is None else bm.loops.layers.uv.active
        slots = [("atlas", None)]       # what each submesh is drawn with: the atlas's first, then each game kind once
        for index, name in enumerate(names):
            faces = [f for f in bm.faces if f.material_index == index]
            if not faces:
                continue
            part = bm.copy()
            bmesh.ops.delete(part, geom=[f for f in part.faces if f.material_index != index], context="FACES")
            for face in part.faces:
                if not face.smooth:              # shaded flat in Blender: every edge of it hard
                    for edge in face.edges:
                        edge.smooth = False
            kind, region = used[name]
            submesh = 0
            if kind == "game":
                tile = next(t for s, (k, t) in game.items() if k == region)
                if ("game", region) not in slots:
                    slots.append(("game", region))
                    prop.parts = prop.parts or [{"game": None}]
                    colour, metallic, smoothness = finish_colour(bpy.data.materials.get(origin.get(name, name)))
                    prop.parts.append({"game": region, "fallback": colour, "metallic": round(metallic, 3), "smoothness": round(smoothness, 3)})
                submesh = slots.index(("game", region))
                paint = Paint(None).world("world", 1.0 / tile)
            elif kind == "swatch":
                paint = Paint(region)
            else:
                if uv is None:
                    raise SystemExit("models/%s: the painted material has no UV map" % model)
                own = part.loops.layers.uv.get(uv.name)
                param = part.loops.layers.uv.new("param")
                for face in part.faces:
                    for loop in face.loops:
                        u, v = loop[own].uv
                        if not (-0.001 <= u <= 1.001 and -0.001 <= v <= 1.001):
                            raise SystemExit("models/%s: a UV of its painted material is outside 0..1 (%.3f, %.3f); the picture is "
                                             "drawn once, never tiled" % (model, u, v))
                        loop[param].uv = (min(1.0, max(0.0, u)), min(1.0, max(0.0, v)))
                paint = Paint(region).param(region)
            prop.add(part, paint, sharp=float(settings.get("sharp", 50.0)), marked=True, submesh=submesh)
        bm.free()
        low = [min(v.co[i] for v in prop.bm.verts) for i in range(3)]
        high = [max(v.co[i] for v in prop.bm.verts) for i in range(3)]
        largest = max(h - l for h, l in zip(high, low))
        if not SIZE_RANGE[0] <= largest <= SIZE_RANGE[1]:
            raise SystemExit("models/%s: its largest side is %.3f m; export in metres, scale 1 (README)" % (model, largest))
        return prop
    make.__name__ = model
    return make


def PROPS():
    """(maker, budget) for every model of the set."""
    return [(maker(model), source(model)[1]["budget"]) for model in folders()]


if __name__ == "__main__" and "--scan" in sys.argv:
    try:
        scan()
    except SystemExit:
        raise
    except Exception:
        import traceback
        traceback.print_exc()
        sys.exit(1)
