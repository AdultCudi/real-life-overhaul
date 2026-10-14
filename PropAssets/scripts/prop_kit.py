"""The modelling kit the props are built with: a handful of shape makers, the atlas mapping, export and previews.

Space: metres, X to the right, Y forward, Z up (what you see in Blender with the prop facing away from the
front view). Unity gets (x, z, y): right, up, forward stay right, up, forward; the change of handedness reverses
the triangle winding on the way out.

A prop is a Prop that parts are added to. A part is a bmesh made by one of the shape makers (box, lathe, tube,
prism, loft, grid) plus a Paint that says which atlas region each of its faces shows. Shading is smooth with hard
edges where faces meet at more than the part's `sharp` angle, and normals are weighted by face area, so a
chamfered box keeps flat faces and gets a soft highlight on its edges without extra geometry.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
BUILD = os.environ.get("PROPS_BUILD") or os.path.join(ROOT, "build")      # PROPS_BUILD: a working folder of one's own
PREVIEWS = os.path.join(ROOT, "previews")
sys.path.append(os.path.join(ROOT, "_tools", "pylib"))

_atlas = None


def atlas():
    global _atlas
    if _atlas is None:
        with open(os.path.join(BUILD, "atlas.json")) as f:
            _atlas = json.load(f)
    return _atlas


def region(name, inset=1.0):
    """A region's rectangle in UV, drawn in by `inset` pixels."""
    data = atlas()
    u0, v0, u1, v1 = data["regions"][name]
    du, dv = inset / data["width"], inset / data["height"]
    return u0 + du, v0 + dv, u1 - du, v1 - dv


# ---------------------------------------------------------------------------------------------- paint

class Paint:
    """Which atlas region each face of a part shows.

    Paint("aluminium")                      one finish; height on the part runs up the swatch (dirt at the bottom)
    Paint("aluminium", grade=False)         the clean upper part of the swatch throughout
    Paint("orange_plastic").wrap("cone_side", z0, z1)        a turned part: the region runs up it, mirrored round it
    Paint("paint_blue").planar("barrier_board", normal, right, centre, (width, height))
                                            faces looking along `normal` show the region; `right` is the direction
                                            the region's left-to-right runs in, seen from outside
    """

    def __init__(self, swatch, grade=True):
        self.swatch, self.grade = swatch, grade
        self.decals = []
        self.wrapped = None
        self.mapped = None

    def planar(self, name, normal, right, centre, size, facing=0.8, turns=0):
        normal, right = Vector(normal).normalized(), Vector(right).normalized()
        self.decals.append((name, normal, right, normal.cross(right), Vector(centre), size, facing, turns))
        return self

    def param(self, name, repeat=1.0):
        """For a grid: the region is laid over the surface's own u, v. repeat > 1 only for the tape, whose row tiles."""
        self.mapped = (name, repeat)
        return self

    def wrap(self, name, z0, z1):
        self.wrapped = (name, z0, z1)
        return self

    def apply(self, bm, uv):
        low = Vector((min(v.co[i] for v in bm.verts) for i in range(3)))
        span = Vector((max(max(v.co[i] for v in bm.verts) - low[i], 1e-6) for i in range(3)))
        across = 0 if span.x >= span.y else 1
        sw = region(self.swatch, 7.0)
        own = bm.loops.layers.uv.get("param") if self.mapped else None
        for face in bm.faces:
            decal = next((d for d in self.decals if face.normal.dot(d[1]) > d[6]), None)
            for loop in face.loops:
                p = loop.vert.co
                if own:
                    name, repeat = self.mapped
                    r = region(name)
                    if repeat != 1.0:
                        r = (0.0, r[1], 1.0, r[3])
                    s, t = loop[own].uv
                    loop[uv].uv = (r[0] + (r[2] - r[0]) * s * repeat, r[1] + (r[3] - r[1]) * t)
                elif decal:
                    name, _, right, up, centre, size, _, turns = decal
                    s = 0.5 + (p - centre).dot(right) / size[0]
                    t = 0.5 + (p - centre).dot(up) / size[1]
                    for _ in range(turns):
                        s, t = t, 1.0 - s
                    r = region(name)
                    loop[uv].uv = (r[0] + (r[2] - r[0]) * min(1.0, max(0.0, s)), r[1] + (r[3] - r[1]) * min(1.0, max(0.0, t)))
                elif self.wrapped:
                    name, z0, z1 = self.wrapped
                    r = region(name, 3.0)
                    turn = math.atan2(p.y, p.x) / math.pi          # -1..1: mirrored, so the seam does not show
                    loop[uv].uv = (r[0] + (r[2] - r[0]) * abs(turn), r[1] + (r[3] - r[1]) * min(1.0, max(0.0, (p.z - z0) / (z1 - z0))))
                else:
                    s = (p[across] - low[across]) / span[across]
                    t = (p.z - low.z) / span.z if self.grade else 0.85
                    loop[uv].uv = (sw[0] + (sw[2] - sw[0]) * s, sw[1] + (sw[3] - sw[1]) * (0.04 + 0.92 * t))


# ---------------------------------------------------------------------------------------------- shapes

def box(sx, sy, sz, bevel=0.0):
    """A box centred on the origin, optionally with chamfered edges."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=1, affect="EDGES", profile=0.5)
    return bm


def lathe(profile, segments=12, cap_top=False, cap_bottom=False):
    """Turns a profile of (radius, z) points, bottom to top, round the Z axis."""
    bm = bmesh.new()
    rings = []
    for radius, z in profile:
        rings.append([bm.verts.new((math.cos(2 * math.pi * k / segments) * radius, math.sin(2 * math.pi * k / segments) * radius, z)) for k in range(segments)])
    for lower, upper in zip(rings, rings[1:]):
        for k in range(segments):
            n = (k + 1) % segments
            bm.faces.new((lower[k], lower[n], upper[n], upper[k]))
    if cap_top:
        bm.faces.new(rings[-1])
    if cap_bottom:
        bm.faces.new(rings[0][::-1])
    return bm


def tube(points, radius, sides=6, closed=False, caps=True, flatten=None):
    """A round bar along a path. radius: one number or one per point. flatten: per point, how far the section is
    squashed along its first cross axis (1 = round), for forged ends."""
    points = [Vector(p) for p in points]
    count = len(points)
    radii = radius if isinstance(radius, (list, tuple)) else [radius] * count
    bm = bmesh.new()
    tangents = []
    for i in range(count):
        before = points[i - 1] if (i > 0 or closed) else points[i]
        after = points[(i + 1) % count] if (i < count - 1 or closed) else points[i]
        tangents.append((after - before).normalized())
    side = tangents[0].cross(Vector((0, 0, 1)))
    if side.length < 0.05:
        side = tangents[0].cross(Vector((1, 0, 0)))
    side.normalize()
    rings = []
    for i in range(count):
        # Carried along the path: turned by as much as the path turns, never spun about it.
        side = (side - tangents[i] * side.dot(tangents[i])).normalized()
        up = tangents[i].cross(side)
        squash = flatten[i] if flatten else 1.0
        rings.append([bm.verts.new(points[i] + (side * math.cos(2 * math.pi * k / sides) * squash + up * math.sin(2 * math.pi * k / sides)) * radii[i]) for k in range(sides)])
    for i in range(count if closed else count - 1):
        lower, upper = rings[i], rings[(i + 1) % count]
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((lower[k], lower[n], upper[n], upper[k]))
    if caps and not closed:
        bm.faces.new(rings[0][::-1])
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def prism(outline, z0, z1, bevel=0.0, bottom=True):
    """A flat outline of (x, y) points, anticlockwise from above, given thickness from z0 to z1."""
    bm = bmesh.new()
    lower = [bm.verts.new((x, y, z0)) for x, y in outline]
    upper = [bm.verts.new((x, y, z1)) for x, y in outline]
    count = len(outline)
    for k in range(count):
        n = (k + 1) % count
        bm.faces.new((lower[k], lower[n], upper[n], upper[k]))
    top = bm.faces.new(upper)
    if bottom:
        bm.faces.new(lower[::-1])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(top.edges), offset=bevel, segments=1, affect="EDGES", profile=0.5)
    return bm


def loft(rings, cap_start=False, cap_end=False, closed=True, flip=False):
    """Skin over rings of points (all the same count, all wound the same way). With both ends capped the faces are
    turned outwards whichever way the rings run; an open skin faces outwards if the rings run anticlockwise seen
    from the last ring looking back (as a lathe's do), else pass flip."""
    bm = bmesh.new()
    made = [[bm.verts.new(p) for p in ring] for ring in rings]
    count = len(rings[0])
    for lower, upper in zip(made, made[1:]):
        for k in range(count if closed else count - 1):
            n = (k + 1) % count
            bm.faces.new((lower[k], lower[n], upper[n], upper[k]))
    if cap_start:
        bm.faces.new(made[0][::-1])
    if cap_end:
        bm.faces.new(made[-1])
    bm.normal_update()
    if (cap_start and cap_end and bm.calc_volume(signed=True) < 0) or flip:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    return bm


def grid(surface, nu, nv, both_sides=False):
    """An open surface: surface(u, v) -> point for u, v in 0..1. Faces look along dP/du x dP/dv."""
    bm = bmesh.new()
    param = bm.loops.layers.uv.new("param")
    verts = [[bm.verts.new(surface(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    back = [[bm.verts.new(v.co) for v in row] for row in verts] if both_sides else None
    for j in range(nv):
        for i in range(nu):
            corners = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
            face = bm.faces.new([verts[b][a] for a, b in corners])
            for loop, (a, b) in zip(face.loops, corners):
                loop[param].uv = (a / nu, b / nv)
            if both_sides:      # the other side reads the right way round too
                face = bm.faces.new([back[b][a] for a, b in corners[::-1]])
                for loop, (a, b) in zip(face.loops, corners[::-1]):
                    loop[param].uv = (1.0 - a / nu, b / nv)
    bm.normal_update()
    return bm


def moved(part, at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    """Turns (degrees, XYZ) and moves a part before it is painted or combined."""
    part.transform(Matrix.Translation(Vector(at)) @ Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1.0)))
    return part


def ellipse(a, b, count, z=0.0, centre=(0.0, 0.0), start=0.0):
    return [(centre[0] + math.cos(start + 2 * math.pi * k / count) * a, centre[1] + math.sin(start + 2 * math.pi * k / count) * b, z) for k in range(count)]


def rounded_rect(width, depth, radius, steps=2):
    """Outline of a rectangle with rounded corners, anticlockwise, (x, y)."""
    points = []
    for cx, cy, start in ((width / 2 - radius, depth / 2 - radius, 0), (-width / 2 + radius, depth / 2 - radius, 90),
                          (-width / 2 + radius, -depth / 2 + radius, 180), (width / 2 - radius, -depth / 2 + radius, 270)):
        for k in range(steps + 1):
            a = math.radians(start + 90 * k / steps)
            points.append((cx + math.cos(a) * radius, cy + math.sin(a) * radius))
    return points


def fillet(points, radius, steps=3):
    """A path with its corners rounded."""
    points = [Vector(p) for p in points]
    out = [points[0]]
    for before, corner, after in zip(points, points[1:], points[2:]):
        a, b = (before - corner).normalized(), (after - corner).normalized()
        reach = min(radius, (before - corner).length * 0.45, (after - corner).length * 0.45)
        start, end = corner + a * reach, corner + b * reach
        for k in range(steps + 1):
            t = k / steps
            out.append((1 - t) ** 2 * start + 2 * t * (1 - t) * corner + t ** 2 * end)
    out.append(points[-1])
    return out


# ---------------------------------------------------------------------------------------------- props

class Prop:
    def __init__(self, name, material="lit", pivot="ground centre", note=""):
        self.name, self.material, self.pivot, self.note = name, material, pivot, note
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def add(self, part, paint, at=(0, 0, 0), rot=(0, 0, 0), sharp=50.0, scale=(1, 1, 1)):
        """Adds a part: painted where it was made, then turned (degrees, XYZ) and moved into place."""
        if isinstance(paint, str):
            paint = Paint(paint)
        part.normal_update()
        part.verts.index_update()
        uv = part.loops.layers.uv.new("UVMap")
        paint.apply(part, uv)
        limit = math.radians(sharp)
        for edge in part.edges:
            edge.smooth = len(edge.link_faces) == 2 and edge.calc_face_angle(0.0) < limit
        matrix = Matrix.Translation(Vector(at)) @ Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1.0))
        made = [self.bm.verts.new(matrix @ v.co) for v in part.verts]
        for face in part.faces:
            copy = self.bm.faces.new([made[v.index] for v in face.verts])
            copy.smooth = True
            for source, target in zip(face.loops, copy.loops):
                target[self.uv].uv = source[uv].uv
            for source, target in zip(face.edges, copy.edges):
                target.smooth = source.smooth
        part.free()
        return self

    def arrays(self):
        """Welded vertices, normals, UVs and triangles, in the kit's space."""
        bm = self.bm.copy()
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bmesh.ops.delete(bm, geom=[face for face in bm.faces if face.calc_area() < 1e-10], context="FACES")     # a surface pinched to a point
        bm.verts.index_update()
        bm.faces.index_update()
        bm.normal_update()
        uv = bm.loops.layers.uv.active
        vertices, normals, uvs, triangles, seen = [], [], [], [], {}
        corner = {}
        for vert in bm.verts:
            # Faces round the vertex that are joined by smooth edges share one normal, weighted towards the large ones.
            group = {face.index: face.index for face in vert.link_faces}

            def find(i):
                while group[i] != i:
                    i = group[i]
                return i
            for edge in vert.link_edges:
                if edge.smooth and len(edge.link_faces) == 2:
                    group[find(edge.link_faces[0].index)] = find(edge.link_faces[1].index)
            sums = {}
            for loop in vert.link_loops:
                face = loop.face
                root = find(face.index)
                sums[root] = sums.get(root, Vector()) + face.normal * (face.calc_area() ** 1.5 * loop.calc_angle() + 1e-12)
            for loop in vert.link_loops:
                corner[(loop.face.index, vert.index)] = sums[find(loop.face.index)].normalized()
        for face in bm.faces:
            triangle = []
            for loop in face.loops:
                p, n, t = loop.vert.co, corner[(face.index, loop.vert.index)], loop[uv].uv
                key = (round(p.x, 5), round(p.y, 5), round(p.z, 5), round(n.x, 3), round(n.y, 3), round(n.z, 3), round(t.x, 5), round(t.y, 5))
                if key not in seen:
                    seen[key] = len(vertices)
                    vertices.append(tuple(p))
                    normals.append(tuple(n))
                    uvs.append(tuple(t))
                triangle.append(seen[key])
            triangles.append(triangle)
        bm.free()
        return np.array(vertices), np.array(normals), np.array(uvs), np.array(triangles, dtype=np.int64)


def to_unity(v):
    v = np.asarray(v, dtype=np.float64)
    return np.stack([v[..., 0], v[..., 2], v[..., 1]], axis=-1)


def write_mesh(prop):
    """Writes build/meshes/<name>.json in Unity space and returns the prop's catalogue row."""
    vertices, normals, uvs, triangles = prop.arrays()
    os.makedirs(os.path.join(BUILD, "meshes"), exist_ok=True)
    unity = to_unity(vertices)
    data = {
        "name": prop.name,
        "vertices": np.round(unity, 5).ravel().tolist(),
        "normals": np.round(to_unity(normals), 4).ravel().tolist(),
        "uv": np.round(uvs, 5).ravel().tolist(),
        "triangles": triangles[:, ::-1].ravel().tolist(),
    }
    with open(os.path.join(BUILD, "meshes", prop.name + ".json"), "w") as f:
        json.dump(data, f, separators=(",", ":"))
    low, high = unity.min(axis=0), unity.max(axis=0)
    print("mesh %-22s %5d vertices %5d triangles  %.3f x %.3f x %.3f m" % (prop.name, len(vertices), len(triangles), *(high - low)))
    return {"id": prop.name, "mesh": prop.name, "material": prop.material, "triangles": int(len(triangles)), "vertices": int(len(vertices)),
            "size": np.round(high - low, 4).tolist(), "centre": np.round((high + low) / 2, 4).tolist(), "pivot": prop.pivot, "note": prop.note}


# ---------------------------------------------------------------------------------------------- previews

_materials = {}


def preview_material(kind):
    """What the mod's runtime material does with the atlas, as nearly as Blender can show it: colour, metallic from
    the mask's red, roughness from its alpha, occlusion multiplied in; the 'glow' kind also lights itself a little."""
    if kind in _materials:
        return _materials[kind]
    material = bpy.data.materials.new("props_" + kind)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes["Principled BSDF"]
    base = nodes.new("ShaderNodeTexImage")
    base.image = bpy.data.images.load(os.path.join(BUILD, "textures", "props_base.png"), check_existing=True)
    mask = nodes.new("ShaderNodeTexImage")
    mask.image = bpy.data.images.load(os.path.join(BUILD, "textures", "props_mask.png"), check_existing=True)
    mask.image.colorspace_settings.name = "Non-Color"
    mask.image.alpha_mode = "CHANNEL_PACKED"
    split = nodes.new("ShaderNodeSeparateColor")
    links.new(mask.outputs["Color"], split.inputs["Color"])
    shade = nodes.new("ShaderNodeMix")
    shade.data_type = "RGBA"
    shade.blend_type = "MULTIPLY"
    shade.inputs[0].default_value = 1.0
    links.new(base.outputs["Color"], shade.inputs[6])
    links.new(split.outputs["Green"], shade.inputs[7])
    links.new(shade.outputs[2], shader.inputs["Base Color"])
    links.new(split.outputs["Red"], shader.inputs["Metallic"])
    rough = nodes.new("ShaderNodeMath")
    rough.operation = "SUBTRACT"
    rough.inputs[0].default_value = 1.0
    links.new(mask.outputs["Alpha"], rough.inputs[1])
    links.new(rough.outputs[0], shader.inputs["Roughness"])
    if kind == "glow":
        links.new(base.outputs["Color"], shader.inputs["Emission Color"])
        shader.inputs["Emission Strength"].default_value = 0.25
    material.use_backface_culling = True
    _materials[kind] = material
    return material


def plain_material(name, colour, roughness=0.9):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*colour, 1)
    shader.inputs["Roughness"].default_value = roughness
    return material


def place(prop, at=(0, 0, 0), turn=0.0, scale=(1, 1, 1)):
    """Puts the prop in the preview scene exactly as it is exported: same vertices, normals and UVs."""
    vertices, normals, uvs, triangles = prop.arrays()
    mesh = bpy.data.meshes.new(prop.name)
    mesh.from_pydata(vertices.tolist(), [], triangles.tolist())
    layer = mesh.uv_layers.new(name="UVMap")
    loops = np.zeros(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get("vertex_index", loops)
    layer.data.foreach_set("uv", uvs[loops].ravel())
    mesh.materials.append(preview_material(prop.material))
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    mesh.normals_split_custom_set_from_vertices(normals.tolist())
    mesh.update()
    obj = bpy.data.objects.new(prop.name, mesh)
    obj.location = at
    obj.rotation_euler = (0, 0, math.radians(turn))
    obj.scale = scale
    bpy.context.scene.collection.objects.link(obj)
    _placed.append(obj)
    return obj


_stage = None
_placed = []


def use_gpu():
    """Renders on the NVIDIA card, never the integrated one: Cycles set to OptiX (CUDA if OptiX will not start) on the NVIDIA
    devices only, and the previews' EEVEE, which draws on whichever card Blender's own GPU context is on, says which that is.
    Printed once a run, so a run on the wrong card shows."""
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = kind
                prefs.get_devices()
            except Exception:
                continue
            if any(d.type == kind and "NVIDIA" in d.name for d in prefs.devices):
                break
        for d in prefs.devices:
            d.use = d.type == prefs.compute_device_type and "NVIDIA" in d.name
        for scene in bpy.data.scenes:
            scene.cycles.device = "GPU"
        cycles = ", ".join(d.name for d in prefs.devices if d.use) or "no NVIDIA device"
    except Exception as e:
        cycles = "not set (%s)" % e
    global _cycles_said
    _cycles_said = cycles


_cycles_said = None
_gpu_said = False


def _say_gpu():
    """After the first render, when EEVEE's drawing context is up: the card it drew on."""
    global _gpu_said
    if _gpu_said:
        return
    _gpu_said = True
    try:
        import gpu
        drawing = "%s %s" % (gpu.platform.vendor_get(), gpu.platform.renderer_get())
    except Exception as e:
        drawing = "unknown (%s)" % e
    print("render device: EEVEE drew on %s; Cycles is set to %s" % (drawing, _cycles_said))
    if "NVIDIA" not in drawing.upper():
        print("render device: WARNING, not the NVIDIA card (start Blender with --gpu-backend vulkan, or set Blender to the NVIDIA card in Windows' graphics settings)")


def new_scene(ground=(0.20, 0.20, 0.21)):
    """The preview stage, emptied of props: a neutral ground, daylight and a camera. Made once per run."""
    global _stage
    for obj in _placed:
        mesh = obj.data
        bpy.data.objects.remove(obj)
        bpy.data.meshes.remove(mesh)
    _placed.clear()
    if _stage:
        return _stage
    bpy.ops.wm.read_factory_settings(use_empty=True)
    use_gpu()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.60, 0.66, 0.74, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world
    for name, energy, rotation in (("Key", 3.4, (48, 0, 150)), ("Fill", 0.9, (62, 0, -80))):
        light = bpy.data.lights.new(name, "SUN")
        light.energy = energy
        light.angle = math.radians(6)
        obj = bpy.data.objects.new(name, light)
        obj.rotation_euler = [math.radians(a) for a in rotation]
        scene.collection.objects.link(obj)
    bpy.ops.mesh.primitive_plane_add(size=80)
    bpy.context.active_object.data.materials.append(plain_material("ground", ground))
    camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    _stage = (scene, camera)
    return _stage


def height_bar(at):
    """A 1.8 m bar banded every 0.3 m: the height of a person, for scale."""
    for k in range(6):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(at[0], at[1], 0.15 + 0.3 * k))
        obj = bpy.context.active_object
        obj.scale = (0.05, 0.05, 0.3)
        obj.data.materials.append(plain_material("bar_red", (0.75, 0.08, 0.06)) if k % 2 == 0 else plain_material("bar_white", (0.9, 0.9, 0.9)))
        _placed.append(obj)


def reference_box(size, centre, colour=(0.42, 0.47, 0.52)):
    """A plain grey box in the preview scene: the vehicle a body goes over, a wall, a person's bulk. Kit space."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    obj = bpy.context.active_object
    obj.scale = size
    obj.data.materials.append(plain_material("reference_%.2f_%.2f_%.2f" % tuple(colour), colour))
    _placed.append(obj)
    return obj


def figure(at=(0.0, 0.0), turn=0.0, up=0.0):
    """A standing person 1.78 m tall made of boxes, for scale beside something big, standing `up` over the ground (on a
    vehicle's floor). Faces +Y before `turn` (degrees)."""
    parts = (((0.13, 0.15, 0.86), (-0.10, 0, 0.43)), ((0.13, 0.15, 0.86), (0.10, 0, 0.43)), ((0.40, 0.22, 0.60), (0, 0, 1.16)),
             ((0.10, 0.12, 0.62), (-0.26, 0, 1.13)), ((0.10, 0.12, 0.62), (0.26, 0, 1.13)), ((0.17, 0.20, 0.24), (0, 0.01, 1.66)))
    a = math.radians(turn)
    for size, (x, y, z) in parts:
        obj = reference_box(size, (at[0] + x * math.cos(a) - y * math.sin(a), at[1] + x * math.sin(a) + y * math.cos(a), z + up), (0.55, 0.42, 0.34))
        obj.rotation_euler = (0, 0, a)


def turnaround(prop, out, references=(), people=(), parts=(), views=((0, 8), (90, 8), (180, 8), (270, 8), (38, 20), (218, 28)), tile=(560, 420)):
    """Six views of a prop on one sheet (front, right, back, left, two three-quarters) with whatever it is made to go
    over or beside: references is ((size, centre), ...) boxes in kit space, people ((x, y), turn) standing figures,
    parts ((Prop, at, turn), ...) the props the code puts on it (a vehicle's wheels). Azimuth 0 looks at the prop's
    front (+Y in the kit, +Z in Unity)."""
    vertices = prop.arrays()[0]
    low, high = vertices.min(axis=0), vertices.max(axis=0)
    centre, radius = (low + high) / 2, max(float(((high - low) ** 2).sum()) ** 0.5 / 2, 0.3)
    tiles = []
    for k, (azimuth, elevation) in enumerate(views):
        scene, camera = new_scene()
        place(prop)
        for part, at, turn in parts:
            place(part, at, turn)
        for size, where in references:
            reference_box(size, where)
        for where, turn in people:
            figure(where, turn)
        aim(camera, centre, azimuth, elevation, radius / math.tan(math.radians(14)) * 1.12)
        tiles.append(os.path.join(PREVIEWS, "_%s_side%d.png" % (prop.name, k)))
        render(scene, tiles[-1], *tile)
    from PIL import Image
    images = [Image.open(p).convert("RGB") for p in tiles]
    rows = (len(images) + 2) // 3
    joined = Image.new("RGB", (tile[0] * 3, tile[1] * rows))
    for k, image in enumerate(images):
        joined.paste(image, ((k % 3) * tile[0], (k // 3) * tile[1]))
    compact(joined, out)
    for p in tiles:
        os.remove(p)


def aim(camera, target, azimuth, elevation, distance, fov=28.0):
    """azimuth 0 looks at the prop's front (from +Y), 90 from its right."""
    az, el = math.radians(azimuth), math.radians(elevation)
    offset = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el))) * distance
    camera.location = Vector(target) + offset
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.angle = math.radians(fov)
    camera.data.clip_start, camera.data.clip_end = 0.01, 200


def render(scene, path, width=480, height=480):
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    _say_gpu()


def compact(image, path):
    """Saves a preview with a 256-colour palette: a third of the size, so the renders can live in the repository."""
    from PIL import Image
    image.convert("RGB").quantize(256, method=Image.Quantize.MEDIANCUT).save(path, optimize=True)


def sheet(paths, out):
    from PIL import Image
    tiles = [Image.open(p).convert("RGB") for p in paths]
    joined = Image.new("RGB", (sum(t.width for t in tiles), max(t.height for t in tiles)))
    x = 0
    for tile in tiles:
        joined.paste(tile, (x, 0))
        x += tile.width
    compact(joined, out)
    for p in paths:
        os.remove(p)
