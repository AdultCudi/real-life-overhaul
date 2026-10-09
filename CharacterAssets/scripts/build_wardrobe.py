"""Wardrobe: garments built AROUND the game's body, with the mod's own topology.

    blender -b -P build_wardrobe.py

How a top is made (make_top):
  1. The game's bare torso is read from the local reference as a measuring form: welded, its two open ends closed,
     softened (hollows filled, never cut into) and grown by the garment's ease.
  2. That form is voxel-remeshed. The result is a new, even surface of the mod's own vertices and triangles that
     lies a constant distance off the body: nothing of the game's mesh (vertices, triangles, UVs) is in it.
  3. It is cut open where the garment ends (collar, cuffs, hem), by the body's own landmarks (bone influence, height).
  4. Skin weights and the body's Fat/Slim/Musc shapes are sampled from the nearest points of the body, so the garment
     moves and changes build exactly as the body under it does. The body under a garment is not drawn (the mod shows
     only the skin parts of the game's piece named in skinFrom), so cloth cannot be poked through by it.
  5. Ribbed bands (collar, cuffs, hem) are raised and given an inner lip; UVs are laid around the trunk and each
     sleeve in real units, so a knit or weave tile comes out at its true size.

Writes build/meshes/<id>.json, build/textures/*.png and build/pilot_catalogue_wardrobe.json.

STATUS: a pilot. One garment (a men's knit sweater) goes through the whole route and is worn in the HDRP preview
(trial.ps1 wardrobe_trial), but it does not beat the game's own sweater yet (see README, "Wardrobe pilot"), so its
catalogue is named pilot_*: make_catalogue.py does not pick it up and nothing of it is in the bundle.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import kdtree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba  # noqa: E402

BONES = ba.bone_names()


def bone_mask(predicate):
    return np.array([predicate(name) for name in BONES], dtype=bool)


HAND = bone_mask(lambda n: n.startswith("Hand.") or "Finger" in n)
NECK = bone_mask(lambda n: n in ("Neck", "Skull", "Jaw", "Eye.L", "Eye.R"))
ARM = bone_mask(lambda n: n.startswith("UpperArm.") or n.startswith("LowerArm.") or n.startswith("Hand.") or "Finger" in n)
LEFT = bone_mask(lambda n: n.endswith(".L") or ".L." in n)
LOWER_ARM = bone_mask(lambda n: n.startswith("LowerArm.") or n.startswith("Hand.") or "Finger" in n)


def dense_weights(indices, weights):
    dense = np.zeros((len(indices), len(BONES)))
    for k in range(indices.shape[1]):
        np.add.at(dense, (np.arange(len(indices)), indices[:, k]), weights[:, k])
    return dense / np.maximum(dense.sum(axis=1, keepdims=True), 1e-9)


def top_four(dense):
    order = np.argsort(-dense, axis=1)[:, :4]
    weights = np.take_along_axis(dense, order, axis=1)
    return order, weights / np.maximum(weights.sum(axis=1, keepdims=True), 1e-9)


def vertex_normals(vertices, triangles):
    face = np.cross(vertices[triangles[:, 1]] - vertices[triangles[:, 0]], vertices[triangles[:, 2]] - vertices[triangles[:, 0]])
    normals = np.zeros_like(vertices)
    for k in range(3):
        np.add.at(normals, triangles[:, k], face)
    return normals / np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)


def neighbours(count, triangles):
    edges = np.concatenate([triangles[:, [0, 1]], triangles[:, [1, 2]], triangles[:, [2, 0]]])
    edges = np.unique(np.sort(edges, axis=1), axis=0)
    return edges, count


def smooth(values, edges, count, rounds, pinned=None, rate=0.5):
    values = values.copy()
    degree = np.zeros(count)
    np.add.at(degree, edges[:, 0], 1)
    np.add.at(degree, edges[:, 1], 1)
    degree = np.maximum(degree, 1)[:, None]
    for _ in range(rounds):
        total = np.zeros_like(values)
        np.add.at(total, edges[:, 0], values[edges[:, 1]])
        np.add.at(total, edges[:, 1], values[edges[:, 0]])
        moved = values + rate * (total / degree - values)
        if pinned is not None:
            moved[pinned] = values[pinned]
        values = moved
    return values


class Form:
    """The body as a measuring form: welded positions with weights and shapes, and a soft, grown version of it."""

    def __init__(self, parts):
        """parts: (game mesh, submesh or None for all) pairs; together they are the body the garment goes round."""
        body = {"vertices": [], "bone_indices": [], "bone_weights": [], "shapes": {}}
        triangles, offset, names = [], 0, None
        for mesh_name, submesh in parts:
            part = ba.load_reference(mesh_name)
            chosen = [submesh] if submesh is not None else range(len(part["submeshes"]))
            triangles.append(np.concatenate([part["submeshes"][i] for i in chosen]) + offset)
            offset += len(part["vertices"])
            for key in ("vertices", "bone_indices", "bone_weights"):
                body[key].append(part[key])
            # The three builds, under the names this sex's meshes use (the women's are in lower case).
            own = {n.lower(): n for n in part["shape_order"]}
            names = names if parts[0][0] != mesh_name else [own[n] for n in ("fat", "slim", "musc")]
            for name in names:
                body["shapes"].setdefault(name, []).append(part["shapes"][own[name.lower()]])
        triangles = np.concatenate(triangles)
        body = {"vertices": np.concatenate(body["vertices"]), "bone_indices": np.concatenate(body["bone_indices"]),
                "bone_weights": np.concatenate(body["bone_weights"]), "shape_order": names,
                "shapes": {n: np.concatenate(v) for n, v in body["shapes"].items()}}
        keys = np.round(body["vertices"] * 20000).astype(np.int64)
        _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
        inverse = inverse.reshape(-1)
        self.vertices = body["vertices"][first]
        self.triangles = inverse[triangles]
        self.triangles = self.triangles[(self.triangles[:, 0] != self.triangles[:, 1]) & (self.triangles[:, 1] != self.triangles[:, 2])
                                        & (self.triangles[:, 0] != self.triangles[:, 2])]
        used = np.unique(self.triangles)
        remap = -np.ones(len(self.vertices), dtype=np.int64)
        remap[used] = np.arange(len(used))
        self.vertices, self.triangles = self.vertices[used], remap[self.triangles]
        self.weights = dense_weights(body["bone_indices"], body["bone_weights"])[first][used]
        self.shape_order = [n for n in body["shape_order"]]
        self.shapes = {n: body["shapes"][n][first][used] for n in self.shape_order}
        self.normals = vertex_normals(self.vertices, self.triangles)
        self.tree = kdtree.KDTree(len(self.vertices))
        for i, v in enumerate(self.vertices):
            self.tree.insert(v, i)
        self.tree.balance()

    def grown(self, ease, soften):
        """Positions pushed out by the ease, with the hollows (between muscles, the small of the back) filled first."""
        edges, count = neighbours(len(self.vertices), self.triangles)
        soft = smooth(self.vertices, edges, count, soften)
        fill = np.maximum(0.0, np.einsum("ij,ij->i", soft - self.vertices, self.normals))
        normals = smooth(self.normals, edges, count, 4)
        normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)
        return self.vertices + normals * (ease + fill)[:, None]

    def sample(self, points, count=4):
        """Weights and shape deltas of the body at points near it: the nearest few body vertices, by distance."""
        weights = np.zeros((len(points), len(BONES)))
        shapes = {n: np.zeros((len(points), 3)) for n in self.shape_order}
        for i, p in enumerate(points):
            found = self.tree.find_n(p, count)
            share = np.array([1.0 / max(d, 1e-5) ** 2 for _, _, d in found])
            share /= share.sum()
            ids = [k for _, k, _ in found]
            weights[i] = share @ self.weights[ids]
            for n in self.shape_order:
                shapes[n][i] = share @ self.shapes[n][ids]
        return weights, shapes


def remeshed(vertices, triangles, voxel):
    """Closes the open ends of a surface and voxel-remeshes it: a new, even mesh of our own."""
    mesh = bpy.data.meshes.new("form")
    mesh.from_pydata(vertices.tolist(), [], triangles.tolist())
    mesh.update()
    obj = bpy.data.objects.new("form", mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()
    mesh.remesh_voxel_size = voxel
    mesh.remesh_voxel_adaptivity = 0.0
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.voxel_remesh()
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    out_vertices = np.array([v.co[:] for v in bm.verts])
    out_triangles = np.array([[v.index for v in f.verts] for f in bm.faces], dtype=np.int64)
    bm.free()
    bpy.data.objects.remove(obj)
    return out_vertices, out_triangles


def keep_faces(vertices, triangles, keep_vertex):
    triangles = triangles[keep_vertex[triangles].all(axis=1)]
    # The largest connected piece only: cutting can leave crumbs.
    parent = np.arange(len(vertices))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for a, b, c in triangles:
        ra, rb, rc = find(a), find(b), find(c)
        parent[rb] = ra
        parent[rc] = ra
    roots = np.array([find(i) for i in triangles[:, 0]])
    values, counts = np.unique(roots, return_counts=True)
    triangles = triangles[roots == values[np.argmax(counts)]]
    used = np.unique(triangles)
    remap = -np.ones(len(vertices), dtype=np.int64)
    remap[used] = np.arange(len(used))
    return used, remap[triangles]


def boundary_loops(count, triangles):
    """Boundary vertices grouped by the opening they belong to."""
    edges = np.concatenate([triangles[:, [0, 1]], triangles[:, [1, 2]], triangles[:, [2, 0]]])
    keys = np.sort(edges, axis=1)
    _, inverse, counts = np.unique(keys, axis=0, return_inverse=True, return_counts=True)
    rim = edges[counts[inverse.reshape(-1)] == 1]
    parent = {}

    def find(i):
        parent.setdefault(i, i)
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for a, b in rim:
        parent[find(int(a))] = find(int(b))
    groups = {}
    for v in parent:
        groups.setdefault(find(v), []).append(v)
    return [np.array(g) for g in groups.values()], rim


def make_top(spec):
    form = Form(spec["form"])
    grown = form.grown(spec["ease"], spec["soften"])
    vertices, triangles = remeshed(grown, form.triangles, spec["voxel"])
    weights, _ = form.sample(vertices)

    # Where the garment ends.
    hips = ba.bone_head("Hips")[2]
    neck = weights[:, NECK].sum(axis=1)
    hand = weights[:, HAND].sum(axis=1)
    lower_arm = weights[:, LOWER_ARM].sum(axis=1)
    keep = (neck < spec["collar"]) & (vertices[:, 2] > hips + spec["hem"])
    keep &= (hand < 0.35) if spec["sleeve"] == "long" else (lower_arm < 0.02) & (np.abs(vertices[:, 0]) < spec["sleeve_reach"])
    used, triangles = keep_faces(vertices, triangles, keep)
    vertices = vertices[used]
    edges, count = neighbours(len(vertices), triangles)
    loops, rim = boundary_loops(len(vertices), triangles)
    rim_vertices = np.unique(rim)
    pinned = np.zeros(len(vertices), dtype=bool)
    pinned[rim_vertices] = True
    # Tidy the cut edges (they follow voxel steps), then relax the surface a little.
    # A cut follows the voxel steps. Every opening is laid flat on the plane that fits it best (an edge of a real
    # garment is cut straight), then evened out along itself, and the cloth next to it relaxed.
    for loop in loops:
        centre = vertices[loop].mean(axis=0)
        plane = np.linalg.svd(vertices[loop] - centre)[2][2]
        vertices[loop] -= np.outer((vertices[loop] - centre) @ plane, plane)
    for _ in range(8):
        ring = vertices.copy()
        total = np.zeros_like(vertices)
        degree = np.zeros(len(vertices))
        np.add.at(total, rim[:, 0], ring[rim[:, 1]])
        np.add.at(total, rim[:, 1], ring[rim[:, 0]])
        np.add.at(degree, rim[:, 0], 1)
        np.add.at(degree, rim[:, 1], 1)
        vertices[rim_vertices] = 0.5 * ring[rim_vertices] + 0.5 * total[rim_vertices] / degree[rim_vertices, None]
    vertices = smooth(vertices, edges, count, 6, pinned, 0.5)

    # Bands: raised ribbing along every opening, wider at hem and cuffs than at the collar.
    normals = vertex_normals(vertices, triangles)
    band = np.zeros(len(vertices))
    for loop in loops:
        centre = vertices[loop].mean(axis=0)
        collar = centre[2] > vertices[:, 2].max() - 0.12 and abs(centre[0]) < 0.05
        width = spec["collar_band"] if collar else spec["band"]
        tree = kdtree.KDTree(len(loop))
        for i, v in enumerate(loop):
            tree.insert(vertices[v], i)
        tree.balance()
        for i in range(len(vertices)):
            distance = tree.find(vertices[i])[2]
            if distance < width:
                band[i] = max(band[i], 1.0)
            elif distance < width + 0.006:
                band[i] = max(band[i], 1.0 - (distance - width) / 0.006)
    vertices = vertices + normals * (band * spec["band_rise"])[:, None]

    # An inner lip on every opening, so the edge has the thickness of the cloth.
    lip = len(vertices) + np.arange(len(rim_vertices))
    lip_of = -np.ones(len(vertices), dtype=np.int64)
    lip_of[rim_vertices] = lip
    inward = vertices[rim_vertices] - normals[rim_vertices] * (spec["ease"] * 0.7 + spec["band_rise"])
    vertices = np.concatenate([vertices, inward])
    band = np.concatenate([band, np.ones(len(rim_vertices))])
    lip_triangles = []
    for a, b in rim:
        lip_triangles += [[b, a, lip_of[a]], [b, lip_of[a], lip_of[b]]]
    triangles = np.concatenate([triangles, np.array(lip_triangles, dtype=np.int64)])

    weights, shapes = form.sample(vertices)
    normals = vertex_normals(vertices, triangles)

    # UVs in metres: around the trunk, and around each arm along its own axis. A corner takes the layout of its
    # triangle's region, so the armhole is a seam, as on a real garment.
    arm = weights[:, ARM].sum(axis=1) + weights[:, bone_mask(lambda n: n.startswith("Shoulder."))].sum(axis=1) * 0.35
    left = weights[:, LEFT].sum(axis=1) > 0.5
    region = np.where(arm > 0.55, np.where(left, 1, 2), 0)
    axes = {}
    for side, tag in ((1, ".L"), (2, ".R")):
        start, end = np.array(ba.bone_head("UpperArm" + tag)), np.array(ba.bone_head("Hand" + tag))
        axis = (end - start) / np.linalg.norm(end - start)
        up = np.array([0.0, 0.0, 1.0])
        side_vector = np.cross(axis, up)
        side_vector /= np.linalg.norm(side_vector)
        axes[side] = (start, axis, side_vector, np.cross(side_vector, axis))

    def layout(v, where):
        p = vertices[v]
        if where == 0:
            angle = math.atan2(p[0], -p[1])                 # 0 at the front, the seam down the back
            return angle / (2 * math.pi), p[2], 0.16 * 2 * math.pi
        start, axis, a, b = axes[where]
        d = p - start
        angle = math.atan2(d @ a, d @ b)                    # the seam under the arm
        return angle / (2 * math.pi), d @ axis, 0.05 * 2 * math.pi

    out_vertices, out_uv, corner_index, out_triangles = [], [], {}, []
    for tri in triangles:
        counts = np.bincount(region[tri], minlength=3)
        where = int(np.argmax(counts))
        turns = [layout(v, where) for v in tri]
        wrap = max(t[0] for t in turns) - min(t[0] for t in turns) > 0.5
        corners = []
        for v, (turn, along, radius) in zip(tri, turns):
            if wrap and turn < 0:
                turn += 1.0
            key = (int(v), where, wrap and turn > 0.5)
            if key not in corner_index:
                corner_index[key] = len(out_vertices)
                out_vertices.append(int(v))
                stretch = 1.0 + band[v] * (spec["rib_stretch"] - 1.0)
                out_uv.append((turn * radius * stretch / spec["tile"], along / spec["tile"]))
            corners.append(corner_index[key])
        out_triangles.append(corners)
    order = np.array(out_vertices)
    indices, four = top_four(weights[order])
    stats = ba.write_mesh(spec["id"], vertices[order], normals[order], np.array(out_uv), [np.array(out_triangles)], indices, four,
                          [(n, shapes[n][order]) for n in form.shape_order])
    return {"id": spec["id"], "kind": "top", "sex": spec["sex"], "mesh": spec["id"], "skinFrom": spec["skin_from"],
            "layers": [{"role": "cloth", "texture": spec["texture"], "normal": spec["texture"] + "_n"}], "stats": stats}


# ---------------------------------------------------------------- fabrics


def knit(size=512, stitches=16):
    """A stockinette tile: columns of V-shaped stitches. Returns colour (shading only: the game tints it) and height."""
    y, x = np.mgrid[0:size, 0:size] / size
    column = (x * stitches) % 1.0
    row = (y * stitches * 1.25) % 1.0
    leg = np.abs(column - 0.5) * 2.0                              # 0 on the stitch's centre line, 1 between columns
    slant = (row + (1.0 - leg) * 0.55) % 1.0                      # the two legs of the V lean in to the centre
    strand = 0.5 - 0.5 * np.cos(slant * 2 * np.pi)                # a yarn loop every row
    height = strand * (0.35 + 0.65 * np.sin(np.clip(leg, 0, 1) * np.pi) ** 0.6) * (1.0 - 0.75 * leg ** 6)
    height = height / height.max()
    return height


def save_fabric(name, height, depth, contrast):
    from PIL import Image
    folder = os.path.join(ba.BUILD, "textures")
    os.makedirs(folder, exist_ok=True)
    size = height.shape[0]
    shade = 1.0 - contrast * (1.0 - height)
    colour = np.dstack([shade, shade, shade, np.ones_like(shade)])
    Image.fromarray((np.clip(colour, 0, 1) * 255).astype(np.uint8)).save(os.path.join(folder, name + ".png"))
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * depth * size / 512
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * depth * size / 512
    normal = np.dstack([-dx, dy, np.ones_like(dx)])
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    Image.fromarray(((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(folder, name + "_n.png"))
    return [{"name": name, "file": "textures/" + name + ".png", "type": "color", "maxSize": size},
            {"name": name + "_n", "file": "textures/" + name + "_n.png", "type": "normal", "maxSize": size}]


TOPS = [
    {"id": "top_m_knit", "sex": "male", "form": [("MaleNakedTorso", 0), ("MaleSwimsuit", None)], "skin_from": "Sweater",
     "texture": "fabric_knit", "ease": 0.014, "soften": 10, "voxel": 0.016, "sleeve": "long", "collar": 0.55, "hem": -0.09,
     "band": 0.05, "collar_band": 0.022, "band_rise": 0.003, "rib_stretch": 0.6, "tile": 0.07},
]

if __name__ == "__main__":
    sys.path.insert(0, os.path.join(ba.ROOT, "_tools", "pylib"))
    textures = save_fabric("fabric_knit", knit(), depth=9.0, contrast=0.3)
    items = [make_top(spec) for spec in TOPS]
    with open(os.path.join(ba.BUILD, "pilot_catalogue_wardrobe.json"), "w") as f:
        json.dump({"items": items, "textures": textures}, f, indent=1)
