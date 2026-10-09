"""Builds every clip: a contact sheet to look at (previews/<clip>.png), an FBX per clip for Unity (build/anim/) and
build/anim/clips.json, which says how long each is, whether it loops, which tool it is made for and how that tool
sits in the hand.

    blender -b -P build_clips.py                      everything
    blender -b -P build_clips.py -- sweep cpr         only these (sheets only; nothing in build/ is rewritten)
    blender -b -P build_clips.py -- --no-preview      no sheets

The sheets draw the solved pose itself (joints, bone turns, the tool at its pose) on a jointed dummy, eight moments
left to right, seen from the front quarter (top row) and from the side (bottom row). Right arm and leg are orange,
left blue. Tools are the real props when PropAssets/build has the atlas, else a plain bar.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REPO = os.path.dirname(ROOT)
BUILD = os.path.join(ROOT, "build", "anim")
PREVIEWS = os.path.join(ROOT, "previews")
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(REPO, "PropAssets", "scripts"))

import clips  # noqa: E402
import prop_kit as kit  # noqa: E402
import rig  # noqa: E402

FPS = 30
TILES = 8
SPACING = 1.3
VERSION = 1
SHEET_WIDTH = 1260 if "--small" in sys.argv else 1800

with open(os.path.join(HERE, "game_rig.json")) as _f:
    GAME = json.load(_f)


# ---------------------------------------------------------------------------------------------- to Unity

def unity_vector(v):
    return [round(float(v[0]), 5), round(float(v[2]), 5), round(float(v[1]), 5)]


def unity_quaternion(m):
    """A turn in this pipeline's space as a Unity quaternion (x, y, z, w)."""
    swap = np.array([[1.0, 0, 0], [0, 0, 1.0], [0, 1.0, 0]])
    q = Matrix((swap @ np.asarray(m) @ swap).tolist()).to_quaternion()
    return [round(q.x, 6), round(q.y, 6), round(q.z, 6), round(q.w, 6)]


def on_bone(side, grip_turn, grip_offset):
    """A tool's pose relative to the game's hand bone, from its pose relative to the wrist in the T-pose."""
    x, y, z, w = GAME["rightHandTurn" if side == "R" else "leftHandTurn"]
    bone = Quaternion((w, x, y, z)).inverted()
    tx, ty, tz, tw = unity_quaternion(grip_turn)
    local = bone @ Quaternion((tw, tx, ty, tz))
    position = bone @ Vector(unity_vector(grip_offset))
    return {"position": [round(c, 5) for c in position], "rotation": [round(local.x, 6), round(local.y, 6), round(local.z, 6), round(local.w, 6)]}


# ---------------------------------------------------------------------------------------------- the dummy

_shapes = {}
COLOURS = {"body": (0.55, 0.57, 0.62), "R": (0.90, 0.42, 0.10), "L": (0.16, 0.38, 0.80), "head": (0.82, 0.66, 0.55), "tool": (0.75, 0.70, 0.20)}


def shape(kind):
    if kind in _shapes and _shapes[kind].name in bpy.data.meshes:
        return _shapes[kind]
    bm = bmesh.new()
    if kind == "bar":           # along +z, from 0 to 1, radius 1
        bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=1.0, radius2=0.8, depth=1.0, matrix=Matrix.Translation((0, 0, 0.5)))
    elif kind == "ball":
        bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=10, radius=1.0)
    else:
        bmesh.ops.create_cube(bm, size=1.0)
    mesh = bpy.data.meshes.new("dummy_" + kind)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = kind != "box"
    _shapes[kind] = mesh
    return mesh


def part(kind, colour, matrix):
    obj = bpy.data.objects.new("dummy", shape(kind).copy())
    obj.data.materials.append(kit.plain_material("dummy_" + colour, COLOURS[colour], 0.7))
    obj.matrix_world = matrix
    bpy.context.scene.collection.objects.link(obj)
    kit._placed.append(obj)
    return obj


def frame_matrix(turn, position, scale=(1, 1, 1)):
    m = np.eye(4)
    m[:3, :3] = np.asarray(turn) @ np.diag(scale)
    m[:3, 3] = position
    return Matrix(m.tolist())


def limb(a, b, radius, colour):
    along = np.asarray(b) - np.asarray(a)
    length = float(np.linalg.norm(along))
    if length < 1e-5:
        return
    frame = rig.basis(along, (0.3, 0.2, 0.9))
    turn = np.stack([frame[:, 1], frame[:, 2], frame[:, 0]], axis=1)
    part("bar", colour, frame_matrix(turn, a, (radius, radius, length)))


def dummy(solved, shift):
    at = {name: p + shift for name, p in solved.at.items()}
    turns = solved.turns
    part("box", "body", frame_matrix(turns["Hips"], at["Hips"] + turns["Hips"] @ np.array([0, 0, 0.02]), (0.30, 0.19, 0.20)))
    middle = (at["Chest"] + at["Neck"]) / 2
    part("box", "body", frame_matrix(turns["UpperChest"], middle, (0.33, 0.20, 0.34)))
    limb(at["Hips"], at["Chest"], 0.11, "body")
    limb(at["Neck"], at["Head"], 0.05, "head")
    part("ball", "head", frame_matrix(turns["Head"], at["Head"] + turns["Head"] @ np.array([0, 0.02, 0.10]), (0.095, 0.11, 0.12)))
    part("box", "body", frame_matrix(turns["Head"], at["Head"] + turns["Head"] @ np.array([0, 0.12, 0.08]), (0.03, 0.04, 0.03)))      # the nose: which way they look
    for side, word in rig.SIDES.items():
        sign = 1.0 if side == "R" else -1.0
        limb(at[word + "UpperArm"], at[word + "LowerArm"], 0.048, side)
        limb(at[word + "LowerArm"], at[word + "Hand"], 0.040, side)
        hand = turns[word + "Hand"]
        hand_parts(at, turns, side, word, sign)
        limb(at[word + "UpperLeg"], at[word + "LowerLeg"], 0.068, side)
        limb(at[word + "LowerLeg"], at[word + "Foot"], 0.052, side)
        foot = turns[word + "Foot"]
        part("box", "body", frame_matrix(foot, at[word + "Foot"] + foot @ np.array([0, 0.06, -0.085]), (0.10, 0.26, 0.07)))


def hand_parts(at, turns, side, word, sign):
    """Palm, thumb and every finger joint: what closes on a tool."""
    hand = turns[word + "Hand"]
    part("box", "head", frame_matrix(hand, at[word + "Hand"] + hand @ np.array([sign * 0.042, 0.0, -0.004]), (0.084, 0.082, 0.026)))
    for finger in rig.FINGERS:
        names = [word + finger + segment for segment in rig.SEGMENTS] + [word + finger + "Tip"]
        for a, b in zip(names, names[1:]):
            limb(at[a], at[b], 0.0095 if finger != "Thumb" else 0.011, side if finger == "Thumb" else "head")


def closeup(clip, solved, shift, scale=4.0):
    """One hand with its forearm and the tool, drawn large: is the tool in the fist?"""
    side = clip.watch
    word, sign = rig.SIDES[side], (1.0 if side == "R" else -1.0)
    first = len(kit._placed)
    limb(solved.at[word + "LowerArm"], solved.at[word + "Hand"], 0.036, side)
    hand_parts(solved.at, solved.turns, side, word, sign)
    draw_tool(clip, solved, np.zeros(3))
    centre = solved.at[word + "Hand"] + solved.turns[word + "Hand"] @ np.array([sign * 0.08, 0.0, 0.0])
    move = Matrix.Translation(Vector((shift + (0, 0, 0.9)).tolist())) @ Matrix.Scale(scale, 4) @ Matrix.Translation(Vector((-centre).tolist()))
    for obj in kit._placed[first:]:
        obj.matrix_world = move @ obj.matrix_world


_props = {}


def tool_prop(name):
    if name not in _props:
        _props[name] = None
        try:
            import props
            made = props.build_all([name])
            if made:
                _props[name] = made[0][0]
        except Exception as e:      # no atlas yet, or no such prop: a plain bar stands in
            print("tool %s is drawn as a bar (%s)" % (name, str(e)[:80]))
    return _props[name]


def draw_tool(clip, solved, shift):
    if solved.tool is None or not clip.tool:
        return
    turn, position = solved.tool
    prop = tool_prop(clip.tool)
    if prop is not None:
        obj = kit.place(prop)
        obj.matrix_world = frame_matrix(turn, position + shift)
    else:
        part("bar", "tool", frame_matrix(turn, position + shift, (0.018, 0.018, clip.tool_length)))


def sheet(clip):
    rows = []
    for row, (azimuth, elevation) in enumerate(((28, 10), (90, 4), (28, 14))):
        scene, camera = kit.new_scene((0.30, 0.31, 0.33))
        for k in range(TILES):
            t = k / TILES if clip.loop else k / (TILES - 1)
            solved = clip.at(t)
            shift = np.array([(k - (TILES - 1) / 2) * SPACING * (1.0 if row != 1 else -1.0), 0.0, 0.0]) if row != 1 else np.array([0.0, (k - (TILES - 1) / 2) * -SPACING * 1.25, 0.0])
            if row == 2:
                closeup(clip, solved, shift)
                continue
            dummy(solved, shift)
            draw_tool(clip, solved, shift)
            for extra_name, extra_turn, extra_position in clip.dressing:
                prop = tool_prop(extra_name)
                if prop is not None:
                    kit.place(prop).matrix_world = frame_matrix(extra_turn, np.asarray(extra_position) + shift)
        kit.aim(camera, (0, 0, 0.9), azimuth, elevation, 26.0 if row == 1 else 21.0, fov=30)
        path = os.path.join(PREVIEWS, "_%s_%d.png" % (clip.name, row))
        kit.render(scene, path, SHEET_WIDTH, SHEET_WIDTH * 7 // 30)
        rows.append(path)
    from PIL import Image
    tiles = [Image.open(p).convert("RGB") for p in rows]
    joined = Image.new("RGB", (tiles[0].width, sum(t.height for t in tiles)))
    y = 0
    for tile in tiles:
        joined.paste(tile, (0, y))
        y += tile.height
    kit.compact(joined, os.path.join(PREVIEWS, clip.name + ".png"))
    for p in rows:
        os.remove(p)


# ---------------------------------------------------------------------------------------------- the armature and its clips

FLIP = np.diag([-1.0, -1.0, 1.0])       # this pipeline's space -> Blender's, with the character facing -Y as the FBX exporter expects


def blender_point(p):
    return Vector((FLIP @ np.asarray(p)).tolist())


def make_armature():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit._stage = None
    kit._placed.clear()
    kit._materials.clear()
    _shapes.clear()
    armature = bpy.data.armatures.new("Rig")
    obj = bpy.data.objects.new("Rig", armature)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in rig.bones():
        bone = armature.edit_bones.new(name)
        bone.head, bone.tail, bone.roll = blender_point(head), blender_point(tail), 0.0
        if parent:
            bone.parent = armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    rest = {bone.name: np.array(bone.matrix_local.to_3x3()) for bone in armature.bones}
    parents = {bone.name: bone.parent.name if bone.parent else None for bone in armature.bones}
    for bone in obj.pose.bones:
        bone.rotation_mode = "QUATERNION"
    return obj, rest, parents


EXPORT = dict(use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y")


def export_rig(obj):
    """The rig alone, standing in its T-pose: the model Unity makes the human Avatar from. Every clip's FBX then
    borrows that Avatar, so no clip's own first pose is ever mistaken for the T-pose."""
    for bone in obj.pose.bones:
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.location = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(BUILD, "rig.fbx"), bake_anim=False, **EXPORT)


def bake(obj, rest, parents, clip):
    frames = max(2, int(round(clip.length * FPS)))
    action = bpy.data.actions.new(clip.name)
    obj.animation_data_create()
    obj.animation_data.action = action
    last = {}
    stretch = 0.0
    samples = []
    hips_rest = FLIP @ rig.J["Hips"]
    for frame in range(frames + 1):
        t = frame / frames
        solved = clip.at(0.0 if (clip.loop and frame == frames) else t)
        stretch = max(stretch, solved.stretch)
        for bone in obj.pose.bones:
            name = bone.name
            mine = FLIP @ solved.turns[name] @ FLIP
            above = FLIP @ solved.turns[parents[name]] @ FLIP if parents[name] else np.eye(3)
            local = rest[name].T @ above.T @ mine @ rest[name]
            q = Matrix(local.tolist()).to_quaternion()
            if name in last and last[name].dot(q) < 0:
                q.negate()
            last[name] = q.copy()
            bone.rotation_quaternion = q
            bone.keyframe_insert("rotation_quaternion", frame=frame)
            if name == "Hips":
                bone.location = Vector((rest[name].T @ (FLIP @ solved.at["Hips"] - hips_rest)).tolist())
                bone.keyframe_insert("location", frame=frame)
        if frame % max(1, frames // 4) == 0 and frame < frames:
            samples.append({"time": round(frame / FPS, 4), "joints": {name: unity_vector(solved.at[name]) for name in
                                                                      ("Hips", "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftLowerArm", "RightLowerArm", "LeftLowerLeg", "RightLowerLeg")},
                            "leftHandTurn": unity_quaternion(solved.turns["LeftHand"]), "rightHandTurn": unity_quaternion(solved.turns["RightHand"]),
                            "tool": None if solved.tool is None or not solved.tool_held else {"position": unity_vector(solved.tool[1]), "rotation": unity_quaternion(solved.tool[0])}})
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 0, frames
    path = os.path.join(BUILD, clip.name + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, bake_anim_step=1.0, **EXPORT)
    obj.animation_data.action = None
    bpy.data.actions.remove(action)
    row = {"name": clip.name, "file": clip.name + ".fbx", "length": round(frames / FPS, 4), "loop": clip.loop, "note": clip.note, "stretch": round(stretch, 4),
           "speed": round(abs(clip.speed), 3), "mask": clip.mask, "watch": clip.watch, "marks": [{"name": name, "time": round(time, 3)} for name, time in clip.marks.items()],
           "dressing": [{"prop": name, "position": unity_vector(position), "rotation": unity_quaternion(turned)} for name, turned, position in clip.dressing],
           "samples": samples}
    if clip.tool:
        row["tool"] = {"prop": clip.tool, "hand": "Right" if clip.grips[0].side == "R" else "Left", **on_bone(clip.grips[0].side, *clip.grips[0].socket())}
        if len(clip.grips) > 1:
            other = clip.grips[1]
            turn, offset = other.socket()
            row["tool"]["otherHand"] = {"hand": "Right" if other.side == "R" else "Left", "wristOnTool": unity_vector(-(turn.T @ offset))}
    print("clip %-18s %5.2f s %s  stretch %.3f m%s" % (clip.name, frames / FPS, "loop" if clip.loop else "once", stretch, "  tool " + clip.tool if clip.tool else ""))
    return row


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = [a for a in arguments if not a.startswith("--")]
    wanted = [clip for clip in clips.CLIPS if not only or clip.name in only]
    missing = [name for name in only if name not in [clip.name for clip in clips.CLIPS]]
    if missing:
        raise SystemExit("no such clip: " + ", ".join(missing))
    rows = []
    if not only:
        os.makedirs(BUILD, exist_ok=True)
        obj, rest, parents = make_armature()
        export_rig(obj)
        rows = [bake(obj, rest, parents, clip) for clip in wanted]
        bad = [row["name"] for row in rows if row["stretch"] > 0.03 and "--allow-stretch" not in arguments]
        if bad:
            raise SystemExit("OUT OF REACH: a hand or foot cannot get where the clip puts it in " + ", ".join(bad))
        with open(os.path.join(BUILD, "clips.json"), "w") as f:
            json.dump({"version": VERSION, "fps": FPS, "rig": "rig.fbx", "clips": rows}, f, indent=1)
        print("clips.json: %d clips, %.1f s in all" % (len(rows), sum(r["length"] for r in rows)))
    if "--no-preview" not in arguments:
        os.makedirs(PREVIEWS, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        kit._stage = None
        kit._placed.clear()
        kit._materials.clear()
        _shapes.clear()
        for clip in wanted:
            sheet(clip)
            print("sheet " + clip.name)


try:
    main()
except SystemExit:
    raise
except Exception:
    import traceback
    traceback.print_exc()
    sys.exit(1)
