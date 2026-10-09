"""What every garment of build_garments.py is made with.

  Surface   a game mesh (or several) as something to MEASURE and to SAMPLE: horizontal and vertical cross-sections
            (ray-cast, so a radius at any angle and height), and skin weights and Fat/Slim/Musc deltas at the closest
            point of its surface (barycentric, so a 1 300 triangle game garment gives smooth weights). Extends
            build_wardrobe.Form; nothing of a Surface is ever written to the output.
  Sheet     a garment panel as a rectangular grid over (up, around) with a mask of the cells that exist. It is built
            fine (for the cloth simulation and the bake) and exported coarse: the game mesh is a choice of its rows and
            columns, so both share one UV layout and the folds bake by plain resampling, without a ray-cast bake.
  drape     Blender's cloth solver on the welded fine sheets, over collision bodies, pinned where the garment is held.
  bake      the fine surface's normals in the coarse surface's tangent frame, into the sheet's rectangle of the normal
            map, with the fabric's own relief added; a cavity map of the folds for the colour.
  fabrics   twill, jersey, rib: height fields over metric cloth coordinates.

Blender space throughout (Z up, the character faces -Y, +X is its left): see ba_common.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba  # noqa: E402
from build_wardrobe import BONES, Form, dense_weights, top_four, vertex_normals  # noqa: E402

BODY_SHAPES = ("Fat", "Slim", "Musc")       # the game sets these three by position on every clothing mesh


# ---------------------------------------------------------------- measuring and sampling the game's meshes


class Surface(Form):
    """Game meshes welded into one surface with weights and the three body shapes (taken by position, as the game does)."""

    def __init__(self, parts):
        """parts: (game mesh, list of submeshes or None for all)."""
        vertices, triangles, indices, weights, shapes, offset = [], [], [], [], [[], [], []], 0
        for mesh_name, chosen in parts:
            part = ba.load_reference(mesh_name)
            chosen = range(len(part["submeshes"])) if chosen is None else chosen
            triangles.append(np.concatenate([part["submeshes"][i] for i in chosen]) + offset)
            offset += len(part["vertices"])
            vertices.append(part["vertices"])
            indices.append(part["bone_indices"])
            weights.append(part["bone_weights"])
            for k in range(3):
                order = part["shape_order"]
                shapes[k].append(part["shapes"][order[k]] if len(order) > k else np.zeros_like(part["vertices"]))
        vertices, triangles = np.concatenate(vertices), np.concatenate(triangles)
        keys = np.round(vertices * 20000).astype(np.int64)
        _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
        triangles = inverse.reshape(-1)[triangles]
        triangles = triangles[(triangles[:, 0] != triangles[:, 1]) & (triangles[:, 1] != triangles[:, 2]) & (triangles[:, 0] != triangles[:, 2])]
        used = np.unique(triangles)
        remap = -np.ones(len(first), dtype=np.int64)
        remap[used] = np.arange(len(used))
        self.vertices, self.triangles = vertices[first][used], remap[triangles]
        self.weights = dense_weights(np.concatenate(indices), np.concatenate(weights))[first][used]
        self.shape_order = list(BODY_SHAPES)
        self.shapes = {n: np.concatenate(shapes[k])[first][used] for k, n in enumerate(BODY_SHAPES)}
        self.normals = vertex_normals(self.vertices, self.triangles)
        self._bvh = None

    def bvh(self):
        if self._bvh is None:
            from mathutils.bvhtree import BVHTree
            self._bvh = BVHTree.FromPolygons(self.vertices.tolist(), self.triangles.tolist())
        return self._bvh

    def closest(self, points):
        """Per point: the triangle of the surface nearest to it, the barycentric coordinates there, the distance."""
        tree = self.bvh()
        tri = np.zeros(len(points), dtype=np.int64)
        bary = np.zeros((len(points), 3))
        distance = np.zeros(len(points))
        for i, p in enumerate(points):
            location, _, index, d = tree.find_nearest(p.tolist())
            a, b, c = self.vertices[self.triangles[index]]
            v0, v1, v2 = b - a, c - a, np.array(location) - a
            d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
            den = max(d00 * d11 - d01 * d01, 1e-18)
            v = (d11 * d20 - d01 * d21) / den
            w = (d00 * d21 - d01 * d20) / den
            tri[i], bary[i], distance[i] = index, np.clip([1 - v - w, v, w], 0, 1), d
        return tri, bary / bary.sum(axis=1, keepdims=True), distance

    def sample(self, points, count=4):
        """Weights (points, bones) and shape deltas at the closest surface points."""
        tri, bary, _ = self.closest(points)
        corners = self.triangles[tri]
        weights = np.einsum("pk,pkb->pb", bary, self.weights[corners])
        shapes = {n: np.einsum("pk,pkx->px", bary, self.shapes[n][corners]) for n in BODY_SHAPES}
        return weights, shapes

    def push_out(self, points, ease, rounds=2):
        """Points moved, where they are closer than `ease` to the surface or under it, straight out to that distance."""
        tree = self.bvh()
        out = np.array(points, dtype=float).reshape(-1, 3)
        for _ in range(rounds):
            for i, p in enumerate(out):
                location, normal, _, _ = tree.find_nearest(p.tolist())
                location, normal = np.array(location), np.array(normal)
                signed = (p - location) @ normal
                if signed < ease:
                    out[i] = p + normal * (ease - signed)
        return out.reshape(np.shape(points))

    def cut(self, origin, normal):
        """Where a plane cuts the surface: (n, 2, 3) line segments."""
        v, t = self.vertices, self.triangles
        height = (v - np.asarray(origin)) @ np.asarray(normal)
        side = height > 0
        s = side[t]
        out = []
        for tri in t[(s.any(axis=1)) & (~s.all(axis=1))]:
            pts = []
            for a, b in ((0, 1), (1, 2), (2, 0)):
                if side[tri[a]] != side[tri[b]]:
                    pts.append(v[tri[a]] + (v[tri[b]] - v[tri[a]]) * height[tri[a]] / (height[tri[a]] - height[tri[b]]))
            if len(pts) == 2:
                out.append(pts)
        return np.array(out).reshape(-1, 2, 3)

    def segments(self, axis, value):
        """Where the plane (coordinate axis = value) cuts the surface: (n, 2, 3) line segments."""
        v, t = self.vertices, self.triangles
        side = v[:, axis] > value
        s = side[t]
        crossing = t[(s.any(axis=1)) & (~s.all(axis=1))]
        out = []
        for tri in crossing:
            pts = []
            for a, b in ((0, 1), (1, 2), (2, 0)):
                p, q = v[tri[a]], v[tri[b]]
                if side[tri[a]] != side[tri[b]]:
                    pts.append(p + (q - p) * (value - p[axis]) / (q[axis] - p[axis]))
            if len(pts) == 2:
                out.append(pts)
        return np.array(out).reshape(-1, 2, 3)


def ray_radius(segments2, centre, directions, far=True, limit=None):
    """Distance from centre along each direction (n, 2) to the cut segments2 (m, 2, 2): the farthest hit (the outer
    outline) or the nearest. NaN where a ray hits nothing."""
    p, q = segments2[:, 0] - centre, segments2[:, 1] - centre
    e = q - p
    out = np.full(len(directions), np.nan)
    for i, d in enumerate(directions):
        den = d[0] * e[:, 1] - d[1] * e[:, 0]
        ok = np.abs(den) > 1e-12
        t = np.where(ok, (p[:, 0] * e[:, 1] - p[:, 1] * e[:, 0]) / np.where(ok, den, 1), -1)
        s = np.where(ok, (p[:, 0] * d[1] - p[:, 1] * d[0]) / np.where(ok, den, 1), -1)
        hit = t[(t > 0) & (s >= -1e-6) & (s <= 1 + 1e-6)]
        if limit is not None:
            hit = hit[hit < limit]
        if len(hit):
            out[i] = hit.max() if far else hit.min()
    return out


def coons(bottom, top, left, right, reach=4.0):
    """A patch (rows, columns, 3) through four boundary curves: bottom and top (columns, 3), left and right (rows, 3).
    Ruled between bottom and top; the side curves' own shape fades out towards the middle (reach: how fast), so an
    armhole's curve does not push the whole chest out, as a plain Coons patch would."""
    rows, columns = len(left), len(bottom)
    u = np.linspace(0, 1, columns)[None, :, None]
    v = np.linspace(0, 1, rows)[:, None, None]
    ruled = (1 - v) * bottom[None] + v * top[None]
    return ruled + (1 - u) ** reach * (left[:, None] - ruled[:, :1]) + u ** reach * (right[:, None] - ruled[:, -1:])


def fill_nan(values, wrap=False):
    """NaNs replaced by interpolation along the array (round the ring if wrap)."""
    values = np.array(values, dtype=float)
    good = ~np.isnan(values)
    if not good.any():
        return values
    x = np.arange(len(values))
    if wrap:
        xs = np.concatenate([x[good] - len(values), x[good], x[good] + len(values)])
        return np.interp(x, xs, np.tile(values[good], 3))
    return np.interp(x, x[good], values[good])


def smooth1(values, rounds=1, wrap=False):
    values = np.array(values, dtype=float)
    for _ in range(rounds):
        left = np.roll(values, 1, axis=0)
        right = np.roll(values, -1, axis=0)
        if not wrap:
            left[0], right[-1] = values[0], values[-1]
        values = 0.25 * left + 0.5 * values + 0.25 * right
    return values


def curve(points):
    xs, ys = np.array([p[0] for p in points]), np.array([p[1] for p in points])
    return lambda x: np.interp(x, xs, ys)


def resample(polyline, count):
    """A polyline (n, k) at count points evenly spaced along its length."""
    polyline = np.asarray(polyline, dtype=float)
    lengths = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(polyline, axis=0), axis=1))])
    at = np.linspace(0, lengths[-1], count)
    return np.stack([np.interp(at, lengths, polyline[:, k]) for k in range(polyline.shape[1])], axis=1)


# ---------------------------------------------------------------- sheets


class Sheet:
    """A panel: points[row, column] (fine), which cells exist, and where it lies in the texture.

    rows, columns: the fine indices kept in the game mesh (always including the panel's ends).
    rect: (u0, v0, u1, v1) of the texture; u runs along the columns, v along the rows.
    """

    def __init__(self, name, points, rect, rows, columns, cells=None, flip=False):
        self.name, self.points, self.rect, self.flip = name, np.array(points, dtype=float), rect, flip
        self.rows, self.columns = np.array(sorted(set(rows))), np.array(sorted(set(columns)))
        shape = self.points.shape[:2]
        self.cells = np.ones((shape[0] - 1, shape[1] - 1), dtype=bool) if cells is None else cells
        self.pattern = self.points.copy()       # as cut, before draping: metric cloth coordinates come from this
        self.ids = None                         # welded vertex per grid node (fine), -1 where unused

    def used(self):
        used = np.zeros(self.points.shape[:2], dtype=bool)
        c = self.cells
        used[:-1, :-1] |= c
        used[1:, :-1] |= c
        used[:-1, 1:] |= c
        used[1:, 1:] |= c
        return used

    def uv(self):
        """UV of every fine node: even in index, so a kept row or column lands on a fixed texel line."""
        rows, columns = self.points.shape[:2]
        u0, v0, u1, v1 = self.rect
        u = u0 + (u1 - u0) * np.arange(columns) / (columns - 1)
        v = v0 + (v1 - v0) * np.arange(rows) / (rows - 1)
        return np.stack(np.broadcast_arrays(u[None, :], v[:, None]), axis=-1)

    def metric(self):
        """Cloth coordinates in metres of every fine node, measured on the pattern: (across, up)."""
        p, used = self.pattern, self.used()
        step_across = np.linalg.norm(np.diff(p, axis=1), axis=2) * (used[:, 1:] & used[:, :-1])
        step_up = np.linalg.norm(np.diff(p, axis=0), axis=2) * (used[1:] & used[:-1])
        across = np.concatenate([np.zeros((p.shape[0], 1)), np.cumsum(step_across, axis=1)], axis=1)
        up = np.concatenate([np.zeros((1, p.shape[1])), np.cumsum(step_up, axis=0)], axis=0)
        return across, up

    def coarse_cells(self):
        """Cells of the game mesh: (row index pairs, column index pairs) that are wholly inside the panel."""
        out = []
        for a, b in zip(self.rows[:-1], self.rows[1:]):
            for c, d in zip(self.columns[:-1], self.columns[1:]):
                if self.cells[a:b, c:d].all():
                    out.append((a, b, c, d))
        return out


def weld(sheets, tolerance=2e-4):
    """Gives the fine nodes of all sheets welded vertex ids (nodes at one place are one vertex: seams).
    Returns positions (n, 3) and quads (m, 4), counter-clockwise from outside."""
    points, owners = [], []
    for sheet in sheets:
        used = sheet.used()
        sheet._used = used
        points.append(sheet.points[used])
        owners.append(used.sum())
    points = np.concatenate(points)
    keys = np.round(points / tolerance).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    inverse = inverse.reshape(-1)
    positions = points[first]
    quads, start = [], 0
    for sheet, count in zip(sheets, owners):
        ids = -np.ones(sheet.points.shape[:2], dtype=np.int64)
        ids[sheet._used] = inverse[start:start + count]
        start += count
        sheet.ids = ids
        r, c = np.nonzero(sheet.cells)
        q = np.stack([ids[r, c], ids[r, c + 1], ids[r + 1, c + 1], ids[r + 1, c]], axis=1)
        quads.append(q[:, ::-1] if sheet.flip else q)
    quads = np.concatenate(quads)
    good = np.array([len(set(q)) == 4 for q in quads.tolist()])
    return positions, quads[good]


def pull(sheets, positions):
    """Writes welded positions back into the sheets."""
    for sheet in sheets:
        sheet.points[sheet._used] = positions[sheet.ids[sheet._used]]


def quad_normals(positions, quads):
    tris = np.concatenate([quads[:, [0, 1, 2]], quads[:, [0, 2, 3]]])
    return vertex_normals(positions, tris)


# ---------------------------------------------------------------- the cloth simulation


def drape(positions, quads, pin, bodies, frames=60, settings=None, gravity=-9.81, keep=None, grow=None):
    """Blender's cloth solver on the welded fine mesh. pin: per vertex 0..1 (1 = held where it is; less = a spring to
    where it was cut, which keeps the silhouette). grow: per vertex 0..1, how much of settings["grow"] (a share of its
    size) the cloth there is larger than cut: the surplus is what buckles into folds.
    bodies: (vertices, triangles) collision meshes. Returns the settled positions."""
    import bpy
    scene = ba.reset_scene()
    scene.gravity = (0, 0, gravity)
    scene.frame_start, scene.frame_end = 1, frames + 2
    for k, (vertices, triangles) in enumerate(bodies):
        mesh = bpy.data.meshes.new("body%d" % k)
        mesh.from_pydata(np.asarray(vertices).tolist(), [], np.asarray(triangles).tolist())
        mesh.update()
        body = bpy.data.objects.new("body%d" % k, mesh)
        scene.collection.objects.link(body)
        collision = body.modifiers.new("Collision", "COLLISION")
        body.collision.thickness_outer = (settings or {}).get("body_distance", 0.004)
        body.collision.thickness_inner = 0.02
        body.collision.cloth_friction = (settings or {}).get("friction", 12.0)
        body.collision.damping = 1.0
    mesh = bpy.data.meshes.new("cloth")
    mesh.from_pydata(positions.tolist(), [], quads.tolist())
    mesh.update()
    cloth = bpy.data.objects.new("cloth", mesh)
    scene.collection.objects.link(cloth)
    group = cloth.vertex_groups.new(name="pin")
    for i in np.nonzero(pin > 0.001)[0]:
        group.add([int(i)], float(pin[i]), "REPLACE")
    modifier = cloth.modifiers.new("Cloth", "CLOTH")
    s = modifier.settings
    values = dict(quality=10, mass=0.4, tension_stiffness=25.0, compression_stiffness=25.0, shear_stiffness=15.0, bending_stiffness=2.0,
                  tension_damping=8.0, compression_damping=8.0, shear_damping=8.0, bending_damping=0.5, air_damping=2.0, pin_stiffness=1.0, shrink_min=0.0)
    values.update({k: v for k, v in (settings or {}).items() if k in values})
    for key, value in values.items():
        setattr(s, key, value)
    s.vertex_group_mass = "pin"
    if grow is not None:
        surplus = cloth.vertex_groups.new(name="grow")
        for i in np.nonzero(grow > 0.001)[0]:
            surplus.add([int(i)], float(grow[i]), "REPLACE")
        s.vertex_group_shrink = "grow"
        s.shrink_min = 0.0
        s.shrink_max = -float((settings or {}).get("grow", 0.04))
    c = modifier.collision_settings
    c.use_collision = True
    c.distance_min = (settings or {}).get("cloth_distance", 0.003)
    c.collision_quality = 4
    c.use_self_collision = bool((settings or {}).get("self_collision", False))
    c.self_distance_min = 0.004
    modifier.point_cache.frame_start, modifier.point_cache.frame_end = 1, frames + 2
    bpy.context.view_layer.objects.active = cloth
    for frame in range(1, frames + 2):
        scene.frame_set(frame)
    evaluated = cloth.evaluated_get(bpy.context.evaluated_depsgraph_get())
    out = np.zeros(len(positions) * 3)
    evaluated.data.vertices.foreach_get("co", out)
    out = out.reshape(-1, 3)
    if keep is not None:
        bpy.ops.wm.save_as_mainfile(filepath=keep)
    moved = np.linalg.norm(out - positions, axis=1)
    print("drape: %d vertices, %d frames, moved mean %.1f mm, max %.1f mm" % (len(positions), frames, moved.mean() * 1000, moved.max() * 1000))
    if not np.isfinite(out).all() or moved.max() > 0.6:
        raise RuntimeError("the cloth simulation blew up")
    return out


def cavity(positions, quads, normals, rounds=10):
    """How far each vertex of the draped cloth lies under (-) or over (+) the cloth's own smoothed surface, in metres:
    the hollows of the folds, whatever the game mesh's facets are."""
    depth = np.sum((positions - relax(positions, quads, rounds)) * normals, axis=1)
    return depth - np.median(depth)


def relax(positions, quads, rounds, fixed=None, rate=0.5):
    """A light Laplacian pass over the draped cloth: takes the solver's jitter out and leaves the folds."""
    edges = np.concatenate([quads[:, [0, 1]], quads[:, [1, 2]], quads[:, [2, 3]], quads[:, [3, 0]]])
    edges = np.unique(np.sort(edges, axis=1), axis=0)
    degree = np.zeros(len(positions))
    np.add.at(degree, edges[:, 0], 1)
    np.add.at(degree, edges[:, 1], 1)
    out = positions.copy()
    for _ in range(rounds):
        total = np.zeros_like(out)
        np.add.at(total, edges[:, 0], out[edges[:, 1]])
        np.add.at(total, edges[:, 1], out[edges[:, 0]])
        moved = out + rate * (total / np.maximum(degree, 1)[:, None] - out)
        if fixed is not None:
            moved[fixed] = out[fixed]
        out = moved
    return out


# ---------------------------------------------------------------- baking


def _resize(array, width, height):
    from PIL import Image
    return np.asarray(Image.fromarray(np.ascontiguousarray(array, dtype=np.float32), mode="F").resize((width, height), Image.BICUBIC), dtype=np.float64)


def _bilinear_rows(coarse, index, count):
    """Values given at the fine indices `index` along axis 0, interpolated to all `count` fine indices."""
    at = np.arange(count)
    k = np.clip(np.searchsorted(index, at, side="right") - 1, 0, len(index) - 2)
    t = ((at - index[k]) / (index[k + 1] - index[k]))
    t = t.reshape((-1,) + (1,) * (coarse.ndim - 1))
    return coarse[k] * (1 - t) + coarse[k + 1] * t


def coarse_surface(sheet, normals_fine):
    """The game mesh's surface under every fine node: its position and its tangent frame (as Unity derives it:
    tangent along u, bitangent on the side of v)."""
    rows, columns = sheet.rows, sheet.columns
    p = sheet.points[np.ix_(rows, columns)]
    n = normals_fine[np.ix_(rows, columns)]
    full_rows, full_columns = sheet.points.shape[:2]

    def fine(values):
        values = _bilinear_rows(values, rows, full_rows)
        return np.swapaxes(_bilinear_rows(np.swapaxes(values, 0, 1), columns, full_columns), 0, 1)
    position, normal = fine(p), fine(n)
    normal /= np.maximum(np.linalg.norm(normal, axis=2, keepdims=True), 1e-9)
    du = np.gradient(position, axis=1)
    dv = np.gradient(position, axis=0)
    tangent = du - normal * np.sum(du * normal, axis=2, keepdims=True)
    tangent /= np.maximum(np.linalg.norm(tangent, axis=2, keepdims=True), 1e-9)
    bitangent = np.cross(normal, tangent)
    bitangent *= np.sign(np.sum(bitangent * dv, axis=2, keepdims=True))
    return position, normal, tangent, bitangent


class Atlas:
    """The garment's texture space: a tangent normal map, a height field for painted relief, a cavity map of the
    folds, and metric cloth coordinates per texel, all filled sheet by sheet."""

    def __init__(self, size):
        self.size = size
        self.normal = np.zeros((size, size, 3))
        self.normal[..., 2] = 1.0
        self.cavity = np.zeros((size, size))
        self.across = np.zeros((size, size))
        self.up = np.zeros((size, size))
        self.flat_across = np.zeros((size, size))     # the same, but one value per column and per row of a sheet: for prints
        self.flat_up = np.zeros((size, size))
        self.level = np.zeros((size, size))           # the height (Z, metres) the cloth was cut at: for stripes that run level
        self.sheet = -np.ones((size, size), dtype=np.int64)
        self.names = []

    def box(self, rect):
        u0, v0, u1, v1 = rect
        s = self.size
        x0, x1 = int(round(u0 * s)), int(round(u1 * s))
        y0, y1 = int(round((1 - v1) * s)), int(round((1 - v0) * s))
        return x0, y0, x1, y1

    def add(self, sheet, fine_normals, depth):
        """fine_normals: (rows, columns, 3) of the draped fine surface on this sheet; depth: its cavity (rows, columns)."""
        x0, y0, x1, y1 = self.box(sheet.rect)
        w, h = x1 - x0, y1 - y0
        position, normal, tangent, bitangent = coarse_surface(sheet, fine_normals)
        local = np.stack([np.sum(fine_normals * tangent, axis=2), np.sum(fine_normals * bitangent, axis=2), np.sum(fine_normals * normal, axis=2)], axis=2)
        across, up = sheet.metric()
        for k in range(3):
            self.normal[y0:y1, x0:x1, k] = _resize(local[::-1, :, k], w, h)
        self.cavity[y0:y1, x0:x1] = _resize(depth[::-1], w, h)
        self.across[y0:y1, x0:x1] = _resize(across[::-1], w, h)
        self.up[y0:y1, x0:x1] = _resize(up[::-1], w, h)
        # A print wants coordinates without shear: the across of the sheet's widest row for every row, the up of its
        # longest column for every column. (Per-row and per-column lengths drift against each other on a shaped panel,
        # and a print laid on them comes out as streaks.)
        wide, long_ = int(np.argmax(across[:, -1])), int(np.argmax(up[-1]))
        flat_a = np.broadcast_to(across[wide][None, :], across.shape)
        flat_u = np.broadcast_to(up[:, long_][:, None], up.shape)
        self.flat_across[y0:y1, x0:x1] = _resize(np.ascontiguousarray(flat_a[::-1]), w, h)
        self.flat_up[y0:y1, x0:x1] = _resize(np.ascontiguousarray(flat_u[::-1]), w, h)
        self.level[y0:y1, x0:x1] = _resize(np.ascontiguousarray(sheet.pattern[::-1, :, 2]), w, h)
        self.sheet[y0:y1, x0:x1] = len(self.names)
        self.names.append(sheet.name)

    def inside(self, name):
        return self.sheet == self.names.index(name)

    def local(self, name):
        """Per texel of a sheet's rectangle: (u, v) from 0 to 1 across and up the sheet; NaN outside."""
        index = self.names.index(name)
        ys, xs = np.nonzero(self.sheet == index)
        u = np.full((self.size, self.size), np.nan)
        v = np.full((self.size, self.size), np.nan)
        u[ys, xs] = (xs - xs.min() + 0.5) / (xs.max() - xs.min() + 1)
        v[ys, xs] = 1 - (ys - ys.min() + 0.5) / (ys.max() - ys.min() + 1)
        return u, v

    def finish_normal(self, height, relief, fold_gain=1.0):
        """The folds' normals with the painted height field (texels) added as fine relief."""
        dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * relief
        dy = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * relief
        n = self.normal.copy()
        n[..., :2] *= fold_gain
        n[..., 2] = np.maximum(n[..., 2], 0.05)
        n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-9)
        out = np.dstack([n[..., 0] / n[..., 2] - dx, n[..., 1] / n[..., 2] - dy, np.ones_like(dx)])
        out /= np.linalg.norm(out, axis=2, keepdims=True)
        return out

    def shade(self, strength=1.0, reach=0.004):
        """A multiplier for the colour from the folds: hollows darker, crests a little lighter."""
        return np.clip(1.0 + strength * np.clip(self.cavity / reach, -1, 0.2) * 0.35, 0.4, 1.2)


def save_image(name, array, size=None):
    """array: (h, w, 3 or 4) floats 0..1. Returns the path."""
    from PIL import Image
    folder = os.path.join(ba.BUILD, "textures")
    os.makedirs(folder, exist_ok=True)
    image = Image.fromarray((np.clip(array, 0, 1) * 255 + 0.5).astype(np.uint8))
    if size and size != image.width:
        image = image.resize((size, size), Image.LANCZOS)
    path = os.path.join(folder, name + ".png")
    image.save(path)
    return path


class Tinted:
    """A garment coloured the way the game colours its own clothes: one pale base map with the shading in it, and a
    colour mask whose red, green and blue areas each take one colour of the colourway (green: the cloth; blue: the trim;
    red: a third: a print, a zip). A colourway is then three colours in the catalogue and no texture of its own.

        tinted = Tinted(spec)
        tinted.add("navy", key, make, red, green, blue)     # make() -> base (h, w), mask (h, w, 3) or None (all cloth)
    Colourways with the same key share base and mask; another key (another print, a heathered yarn) gets its own.

        tinted.put("breton", ("", shade), ("stripes", mask, 512), navy, white, white)
    is the same with base and mask named apart, so a garment with several prints keeps ONE base map: a base is
    (tag, make[, size]) -> <id>_bc[_tag], a mask None (all cloth) or (tag, make[, size]) -> <id>_cm[_tag]; each tag is
    made once. make() -> (h, w) or (h, w, 3) for a base, (h, w, 3) for a mask (its green is filled in)."""

    def __init__(self, spec, covered=None):
        self.spec, self.covered, self.made, self.textures, self.layers = spec, covered, {}, [], {}
        self.bases, self.masks, self.means = {}, {}, {}

    def put(self, colour, base, mask, red, green, blue):
        name = self.spec["id"]
        tag, make = base[0], base[1]
        if tag not in self.bases:
            size = base[2] if len(base) > 2 else self.spec["colour_size"]
            shade = np.clip(make(), 0, 1)
            if shade.ndim == 2:
                shade = np.dstack([shade, shade, shade])
            inside = self.covered if self.covered is not None else np.ones(shade.shape[:2], dtype=bool)
            self.means[tag] = shade[inside].mean(axis=0)
            if self.covered is not None:
                shade = bleed(shade, self.covered, 8)
            base_name = "%s_bc%s" % (name, "_" + tag if tag else "")
            save_image(base_name, np.dstack([shade, np.ones(shade.shape[:2])]), size)
            self.textures.append({"name": base_name, "file": "textures/%s.png" % base_name, "type": "color", "maxSize": size})
            self.bases[tag] = base_name
        if mask is None:
            if None not in self.masks:
                save_image("tint_green", np.dstack([np.zeros((8, 8)), np.ones((8, 8)), np.zeros((8, 8)), np.ones((8, 8))]))
                self.textures.append({"name": "tint_green", "file": "textures/tint_green.png", "type": "tint", "maxSize": 32})
                self.masks[None] = "tint_green"
        elif mask[0] not in self.masks:
            size = mask[2] if len(mask) > 2 else self.spec.get("mask_size", self.spec["colour_size"])
            channels = np.clip(mask[1](), 0, 1)
            channels[..., 1] = np.clip(1 - channels[..., 0] - channels[..., 2], 0, 1)        # every texel is somebody's
            if self.covered is not None:
                channels = np.where(self.covered[..., None], channels, [0.0, 1.0, 0.0])
            mask_name = "%s_cm%s" % (name, "_" + mask[0] if mask[0] else "")
            save_image(mask_name, np.dstack([channels, np.ones(channels.shape[:2])]), size)
            self.textures.append({"name": mask_name, "file": "textures/%s.png" % mask_name, "type": "tint", "maxSize": size})
            self.masks[mask[0]] = mask_name
        self.layers[colour] = {"role": "cloth", "texture": self.bases[tag], "normal": name + "_n", "tintMask": self.masks[mask[0] if mask else None],
                               "tints": [[round(float(v), 4) for v in c] for c in (red, green, blue)]}

    def mean(self, base):
        """The mean value (per channel) of a base map over the cloth, making it if need be: a colourway that wants the
        cloth to come out at a given colour divides by this."""
        if base[0] not in self.bases:
            self.put("", base, None, (1, 1, 1), (1, 1, 1), (1, 1, 1))
            del self.layers[""]
        return self.means[base[0]]

    def add(self, colour, key, make, red, green, blue):
        name = self.spec["id"]
        if key not in self.made:
            suffix = "" if not self.made else str(len(self.made) + 1)
            base, mask = make()
            base = np.dstack([base, base, base])
            if self.covered is not None:
                base = bleed(base, self.covered, 8)
            base_name = "%s_bc%s" % (name, suffix)
            size = self.spec["colour_size"]
            save_image(base_name, np.dstack([base, np.ones(base.shape[:2])]), size)
            self.textures.append({"name": base_name, "file": "textures/%s.png" % base_name, "type": "color", "maxSize": size})
            if mask is None:
                mask_name = "tint_green"
                save_image(mask_name, np.dstack([np.zeros((8, 8)), np.ones((8, 8)), np.zeros((8, 8)), np.ones((8, 8))]))
                self.textures.append({"name": mask_name, "file": "textures/%s.png" % mask_name, "type": "tint", "maxSize": 32})
            else:
                mask = np.clip(mask, 0, 1)
                mask[..., 1] = np.clip(1 - mask[..., 0] - mask[..., 2], 0, 1)        # every texel is somebody's
                if self.covered is not None:
                    mask = np.where(self.covered[..., None], mask, [0.0, 1.0, 0.0])
                mask_name = "%s_cm%s" % (name, suffix)
                size = self.spec.get("mask_size", self.spec["colour_size"])
                save_image(mask_name, np.dstack([mask, np.ones(mask.shape[:2])]), size)
                self.textures.append({"name": mask_name, "file": "textures/%s.png" % mask_name, "type": "tint", "maxSize": size})
            self.made[key] = (base_name, mask_name)
        base_name, mask_name = self.made[key]
        self.layers[colour] = {"role": "cloth", "texture": base_name, "normal": name + "_n", "tintMask": mask_name,
                               "tints": [[round(float(v), 4) for v in c] for c in (red, green, blue)]}


def bleed(colour, covered, rounds=6):
    """Grows painted texels into the unpainted ones next to them, so mip maps do not pull in the background."""
    colour, covered = colour.copy(), covered.copy()
    for _ in range(rounds):
        total = np.zeros_like(colour)
        count = np.zeros(covered.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            total += np.roll(colour * covered[..., None], (dy, dx), (0, 1))
            count += np.roll(covered, (dy, dx), (0, 1))
        grow = (~covered) & (count > 0)
        colour[grow] = total[grow] / count[grow][:, None]
        covered = covered | grow
    return colour


# ---------------------------------------------------------------- fabrics (height 0..1 over metres of cloth)


def noise(size, seed, octaves=(1, 2, 4)):
    rng = np.random.default_rng(seed)
    out = np.zeros((size, size))
    for k in octaves:
        field = rng.random((size, size))
        for _ in range(k):
            field = (field + np.roll(field, 1, 0) + np.roll(field, 1, 1) + np.roll(field, (1, 1), (0, 1))) / 4
        out += (field - field.mean()) / max(field.std(), 1e-9)
    return out / len(octaves)


def blotches(size, seed, scale):
    """Soft large-scale variation (wash, wear): 0..1."""
    from PIL import Image
    rng = np.random.default_rng(seed)
    small = rng.random((max(2, size // scale), max(2, size // scale))).astype(np.float32)
    big = np.asarray(Image.fromarray(small, mode="F").resize((size, size), Image.BICUBIC), dtype=np.float64)
    return np.clip((big - big.min()) / max(big.max() - big.min(), 1e-9), 0, 1)


def twill(across, up, pitch=0.0022):
    """Denim's diagonal rib (drawn coarser than life, so a 1 mm texel can carry it)."""
    return 0.5 + 0.5 * np.sin((across + up) * 2 * np.pi / pitch)


def jersey(across, up, pitch=0.0024):
    """Fine knit: columns of stitches."""
    return (0.5 + 0.5 * np.sin(across * 2 * np.pi / pitch)) * (0.75 + 0.25 * np.sin(up * 2 * np.pi / (pitch * 0.8)))


def rib(across, pitch=0.006):
    """Rib knit: raised wales."""
    return np.clip(0.5 + 0.9 * np.sin(across * 2 * np.pi / pitch), 0, 1)


def stitch_line(distance, along, width=0.0007, dash=0.0032):
    """A row of stitches: distance (m) from the line, along (m) down it. Returns coverage 0..1."""
    return np.exp(-(distance / width) ** 2) * (np.sin(along * 2 * np.pi / dash) > -0.35)


# ---------------------------------------------------------------- the game mesh


class Mesh:
    """Collects the game mesh: vertices with UVs, normals, the fine vertex they came from; triangles."""

    def __init__(self):
        self.vertices, self.normals, self.uv, self.triangles = [], [], [], []

    def add(self, vertices, normals, uv, triangles):
        base = len(self.vertices)
        self.vertices += np.asarray(vertices).tolist()
        self.normals += np.asarray(normals).tolist()
        self.uv += np.asarray(uv).tolist()
        self.triangles += (np.asarray(triangles, dtype=np.int64) + base).tolist()
        return base

    def add_sheet(self, sheet, fine_normals):
        """The kept rows and columns of a sheet. Returns index[row k, column k] into the mesh (-1 unused)."""
        rows, columns = sheet.rows, sheet.columns
        uv = sheet.uv()
        index = -np.ones((len(rows), len(columns)), dtype=np.int64)
        triangles, row_of, column_of = [], {r: i for i, r in enumerate(rows)}, {c: i for i, c in enumerate(columns)}
        for a, b, c, d in sheet.coarse_cells():
            ra, rb, ca, cd = row_of[a], row_of[b], column_of[c], column_of[d]
            for r, k in ((ra, ca), (ra, cd), (rb, cd), (rb, ca)):
                if index[r, k] < 0:
                    index[r, k] = len(self.vertices)
                    self.vertices.append(sheet.points[rows[r], columns[k]].tolist())
                    self.normals.append(fine_normals[rows[r], columns[k]].tolist())
                    self.uv.append(uv[rows[r], columns[k]].tolist())
            q = [index[ra, ca], index[ra, cd], index[rb, cd], index[rb, ca]]
            if sheet.flip:
                q = q[::-1]
            # The diagonal that bends less.
            p = [np.array(self.vertices[i]) for i in q]
            if np.linalg.norm(p[0] - p[2]) <= np.linalg.norm(p[1] - p[3]):
                triangles += [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
            else:
                triangles += [[q[0], q[1], q[3]], [q[1], q[2], q[3]]]
        self.triangles += triangles
        return index

    def strip(self, rows_points, rows_uv, flip=False, closed=False, outward=None):
        """Quads between consecutive rows of points (r, n, 3); smooth normals of its own. outward: (n, 3) directions
        the first row faces, which then decide the winding instead of flip."""
        if outward is not None:
            a = np.asarray(rows_points, dtype=float)
            face = np.cross(a[0, 1:] - a[0, :-1], a[1, :-1] - a[0, :-1])
            flip = bool(np.sum(face * np.asarray(outward)[:-1]) < 0)
        rows_points, rows_uv = np.asarray(rows_points, dtype=float), np.asarray(rows_uv, dtype=float)
        r, n = rows_points.shape[:2]
        triangles = []
        for i in range(r - 1):
            for j in range(n - 1 if not closed else n):
                a, b = i * n + j, i * n + (j + 1) % n
                d, e = a + n, b + n
                triangles += [[a, d, b], [b, d, e]] if flip else [[a, b, d], [b, e, d]]
        triangles = np.array(triangles, dtype=np.int64)
        flat = rows_points.reshape(-1, 3)
        keys = np.round(flat * 50000).astype(np.int64)
        _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
        welded = inverse.reshape(-1)[triangles]
        welded = welded[(welded[:, 0] != welded[:, 1]) & (welded[:, 1] != welded[:, 2]) & (welded[:, 0] != welded[:, 2])]
        normals = vertex_normals(flat[first], welded)[inverse.reshape(-1)]
        return self.add(flat, normals, rows_uv.reshape(-1, 2), triangles)

    def arrays(self):
        return np.array(self.vertices), np.array(self.normals), np.array(self.uv), np.array(self.triangles, dtype=np.int64)


def skin(vertices, triangles, source, rounds=3, sides=None):
    """Weights and body shapes for the game mesh from a Surface, evened out over the garment's own surface (welded by
    position, so seams and added parts do not tear). sides: optional per-vertex +1/-1 to drop weights of the limbs of
    the other side (a trouser leg must not follow the other leg)."""
    weights, shapes = source.sample(vertices)
    if sides is not None:
        left = np.array([n.endswith(".L") or ".L." in n for n in BONES])
        right = np.array([n.endswith(".R") or ".R." in n for n in BONES])
        weights[np.ix_(sides > 0, right)] = 0
        weights[np.ix_(sides < 0, left)] = 0
        weights /= np.maximum(weights.sum(axis=1, keepdims=True), 1e-9)
    keys = np.round(vertices * 5000).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    inverse = inverse.reshape(-1)
    t = inverse[triangles]
    edges = np.unique(np.sort(np.concatenate([t[:, [0, 1]], t[:, [1, 2]], t[:, [2, 0]]]), axis=1), axis=0)
    edges = edges[edges[:, 0] != edges[:, 1]]
    degree = np.zeros(len(first))
    np.add.at(degree, edges[:, 0], 1)
    np.add.at(degree, edges[:, 1], 1)

    def even(values, times):
        values = values[first]
        for _ in range(times):
            total = np.zeros_like(values)
            np.add.at(total, edges[:, 0], values[edges[:, 1]])
            np.add.at(total, edges[:, 1], values[edges[:, 0]])
            values = 0.5 * values + 0.5 * total / np.maximum(degree, 1)[:, None]
        return values[inverse]
    weights = even(weights, rounds)
    weights /= np.maximum(weights.sum(axis=1, keepdims=True), 1e-9)
    shapes = {n: even(shapes[n], rounds + 2) for n in BODY_SHAPES}
    return weights, shapes


def export(mesh_id, mesh, weights, shapes, extra=()):
    """extra: further (name, deltas) shapes after the three body shapes; the mod sets those by the game piece's value
    for the shape of the same name."""
    vertices, normals, uv, triangles = mesh.arrays()
    normals = normals / np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
    indices, four = top_four(weights)
    return ba.write_mesh(mesh_id, vertices, normals, uv, [triangles], indices, four, [(n, shapes[n]) for n in BODY_SHAPES] + list(extra))


def follow_shape(vertices, game_mesh, shape, source, reach=0.008):
    """A shape the game's own piece has for a kind of shoe ("Boots": the hem drawn in to go into the shaft), for our
    mesh: where the game's piece moves, ours goes to where the game's surface ends up (so it is inside the boot too,
    although ours hangs wider), and stays where the game's stays. source: the Surface of that game mesh."""
    part = ba.load_reference(game_mesh)
    if shape not in part["shapes"]:
        return None
    from mathutils.kdtree import KDTree
    tree = KDTree(len(part["vertices"]))
    for i, v in enumerate(part["vertices"]):
        tree.insert(v.tolist(), i)
    tree.balance()
    deltas = part["shapes"][shape]
    tri, bary, _ = source.closest(vertices)
    corners = source.triangles[tri]
    on_game = np.einsum("pk,pkx->px", bary, source.vertices[corners])
    out = np.zeros_like(vertices)
    for i, q in enumerate(on_game):
        total, weight = np.zeros(3), 0.0
        for _, index, distance in tree.find_n(q.tolist(), 3):
            w = 1.0 / (distance + 1e-4)
            total += deltas[index] * w
            weight += w
        delta = total / weight
        share = min(np.linalg.norm(delta) / reach, 1.0)
        out[i] = delta - share * 0.85 * (vertices[i] - q)
    return out





def skin_hide(vertices, triangles, game_mesh, submesh, margin=0.03, below=None, reach=0.05, rim=None, above=None):
    """Which triangles of the bare skin shown with a garment (a submesh of one of the game's pieces) lie under its
    cloth and well inside its openings: the mod does not draw those, so there is no body under the cloth to come
    through it when the character moves (a coarse garment over fine skin does not bend exactly as the skin does).
    Returns {"triangles": how many the submesh has (the mod checks), "hidden": their indices}."""
    from mathutils.bvhtree import BVHTree
    from mathutils.kdtree import KDTree
    part = ba.load_reference(game_mesh)
    skin = part["submeshes"][submesh]
    points = part["vertices"]
    normals = vertex_normals(points, skin)
    cloth = BVHTree.FromPolygons(np.asarray(vertices).tolist(), np.asarray(triangles).tolist())
    # the garment's open edges: edges with one triangle, on the mesh welded by position
    keys = np.round(np.asarray(vertices) * 5000).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    t = inverse.reshape(-1)[np.asarray(triangles)]
    edges = np.sort(np.concatenate([t[:, [0, 1]], t[:, [1, 2]], t[:, [2, 0]]]), axis=1)
    edges = edges[edges[:, 0] != edges[:, 1]]
    unique, counts = np.unique(edges, axis=0, return_counts=True)
    given = rim
    rim = np.unique(unique[counts == 1])
    rim_points = np.asarray(vertices)[first][rim] if given is None else np.asarray(given)     # given: the visible openings
    tree = KDTree(len(rim_points))
    for i, p in enumerate(rim_points):
        tree.insert(p.tolist(), i)
    tree.balance()
    used = np.unique(skin)
    covered = np.zeros(len(points), dtype=bool)
    for i in used:
        p, n = points[i], normals[i]
        if (below is not None and p[2] < below) or (above is not None and p[2] > above):
            covered[i] = True
            continue
        hit = cloth.ray_cast((p + n * 0.0005).tolist(), n.tolist(), reach)
        if hit[0] is None:
            # the cloth may lie a little under the skin where the body is hollow: that is under the garment too
            hit = cloth.ray_cast((p + n * 0.0005).tolist(), (-n).tolist(), 0.015)
        if hit[0] is None:
            continue
        covered[i] = tree.find(p.tolist())[2] > margin
    hidden = np.nonzero(covered[skin].all(axis=1))[0]
    print("skin hide: %d of %d triangles of %s[%d] are under the cloth" % (len(hidden), len(skin), game_mesh, submesh))
    return {"triangles": int(len(skin)), "hidden": hidden.tolist()}


def hulls(vertices, triangles=None, slabs=5, axis=1, pair=True):
    """Closed shells to drape over, where the thing itself is open or many-layered (a shoe with its lining, tongue and
    laces; the game's trousers, open at the waist): the convex hulls of overlapping slabs along an axis, per foot if
    pair. Open and inner faces catch the cloth and drag it the wrong way; closed shells do not."""
    import bmesh
    out_vertices, out_triangles, offset = [], [], 0
    y0, y1 = vertices[:, axis].min(), vertices[:, axis].max()
    step = (y1 - y0) / slabs
    sides = []
    for left in ((vertices[:, 0] > 0, vertices[:, 0] <= 0) if pair else (np.ones(len(vertices), dtype=bool),)):
        for k in range(slabs):
            sides.append(left & (vertices[:, axis] >= y0 + step * (k - 0.35)) & (vertices[:, axis] <= y0 + step * (k + 1.35)))
    for side in sides:
        bm = bmesh.new()
        for v in vertices[side]:
            bm.verts.new(v.tolist())
        result = bmesh.ops.convex_hull(bm, input=bm.verts[:])
        bmesh.ops.delete(bm, geom=list(set(bm.verts) - set(g for g in result["geom"] if isinstance(g, bmesh.types.BMVert))), context="VERTS")
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.verts.index_update()
        out_vertices.append(np.array([v.co[:] for v in bm.verts]))
        out_triangles.append(np.array([[v.index for v in f.verts] for f in bm.faces], dtype=np.int64) + offset)
        offset += len(bm.verts)
        bm.free()
    return np.concatenate(out_vertices), np.concatenate(out_triangles)


def read_built(mesh_id):
    """A mesh this pipeline built earlier (a shoe, to drape a hem over), back in Blender space: vertices, triangles."""
    import json
    with open(os.path.join(ba.BUILD, "meshes", mesh_id + ".json")) as f:
        data = json.load(f)
    vertices = ba.u2b(np.array(data["vertices"]).reshape(-1, 3))
    triangles = np.concatenate([np.array(s["triangles"], dtype=np.int64).reshape(-1, 3)[:, ::-1] for s in data["submeshes"]])
    return vertices, triangles
