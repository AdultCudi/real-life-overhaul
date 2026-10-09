"""Builds hair from a style description: a scalp cap, layered strand cards, optional fuzz shells, hat shapes.

A style (see hair_styles.py) says where hair grows, which way it flows and how long it is; this module
turns that into geometry around the game's head mesh and paints the matching scalp texture.

Submeshes of a finished hairstyle, in draw order:
    scalp   a cap hugging the skull, hair-coloured, fading out at the hairline (plus fuzz shells)
    bulk    the strand cards that make the body of the hair (drawn solid: alpha test with depth)
    wisps   sparse cards for the hairline, fringes and flyaways (drawn soft)
Cards are ordered inside a submesh so that those lying on top come last.

Angles: phi is the azimuth from the front (0) round the side (90) to the back (180), the same on both
sides; e is the elevation above the level of the head centre, in degrees.
"""
import math
import os

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

import ba_common as ba
import hair_atlas

UP = np.array([0.0, 0.0, 1.0])
SCALP_SIZE = 512
SKIN_CLEARANCE = 0.0012        # the cap floats this far above the skin, metres

HEADS = {
    "male": {"head": "MaleFace1", "body": "MaleNakedTorso", "centre": (0.0, 0.015, 1.600)},
    "female": {"head": "FemFace", "body": "FemTorsoSwimsuit", "centre": (0.0, 0.015, 1.605)},
}

# Hairline: elevation of the edge of the hair at each azimuth.
HAIRLINES = {
    "male": ([0, 28, 48, 62, 70, 78, 84, 100, 112, 125, 145, 180],
             [36, 36, 27, 13, -5, -8, 7, 7, -6, -22, -36, -42]),
    "female": ([0, 30, 50, 64, 72, 79, 85, 100, 112, 125, 145, 180],
               [33, 33, 27, 15, 0, -4, 8, 8, -6, -22, -36, -42]),
}


def unit(v):
    v = np.asarray(v, dtype=np.float64)
    length = np.linalg.norm(v)
    return v / length if length > 1e-12 else v


def smoothstep(x, lo, hi):
    t = np.clip((np.asarray(x, dtype=np.float64) - lo) / (hi - lo), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def vertex_normals(vertices, triangles):
    normals = np.zeros_like(vertices)
    a, b, c = vertices[triangles[:, 0]], vertices[triangles[:, 1]], vertices[triangles[:, 2]]
    face = np.cross(b - a, c - a)
    for k in range(3):
        np.add.at(normals, triangles[:, k], face)
    return normals / np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)


class Spot:
    """A place on the head, as a style's functions see it."""
    __slots__ = ("p", "n", "d", "phi", "e", "m", "side", "rng")

    def tangent(self, v):
        """v laid flat on the head here."""
        v = np.asarray(v, dtype=np.float64)
        return unit(v - np.dot(v, self.n) * self.n)


class Head:
    def __init__(self, sex):
        self.sex = sex
        info = HEADS[sex]
        self.centre = np.array(info["centre"])
        self.ref = ba.load_reference(info["head"])
        skin = self.ref["submeshes"][0]
        self.bvh = BVHTree.FromPolygons(self.ref["vertices"].tolist(), skin.tolist())
        body = ba.load_reference(info["body"])
        vertices = np.concatenate([self.ref["vertices"], body["vertices"]])
        triangles = np.concatenate([skin, body["submeshes"][0] + len(self.ref["vertices"])])
        self.body_bvh = BVHTree.FromPolygons(vertices.tolist(), triangles.tolist())
        self.hairline_shift = 0.0
        self.hairline_scale = None

    # -- where hair grows
    def hairline(self, phi):
        xs, ys = HAIRLINES[self.sex]
        edge = np.interp(phi, xs, ys) + self.hairline_shift
        if self.hairline_scale is not None:
            edge = edge + self.hairline_scale(phi)
        return edge

    def angles(self, direction):
        direction = np.asarray(direction, dtype=np.float64)
        d = direction / np.maximum(np.linalg.norm(direction, axis=-1, keepdims=True), 1e-12)
        phi = np.degrees(np.arctan2(np.abs(d[..., 0]), -d[..., 1]))
        e = np.degrees(np.arcsin(np.clip(d[..., 2], -1, 1)))
        return phi, e

    @staticmethod
    def direction(azimuth, e):
        """azimuth: signed degrees, 0 front, +90 the character's left, 180 back."""
        a, el = math.radians(azimuth), math.radians(e)
        return np.array([math.sin(a) * math.cos(el), -math.cos(a) * math.cos(el), math.sin(el)])

    # -- the surface
    def surface(self, direction):
        """Skull surface along a direction from the head centre: (point, outward normal) or None."""
        d = unit(direction)
        hit = self.bvh.ray_cast(Vector(self.centre), Vector(d), 0.5)
        if hit[0] is None:
            return None
        n = np.array(hit[1])
        if np.dot(n, d) < 0:
            n = -n
        return np.array(hit[0]), n

    def nearest(self, p, body=False):
        """Closest skin point: (point, outward normal, signed distance: negative inside)."""
        tree = self.body_bvh if body else self.bvh
        location, normal, _, _ = tree.find_nearest(Vector(p))
        q, n = np.array(location), np.array(normal)
        offset = np.asarray(p) - q
        distance = float(np.linalg.norm(offset))
        signed = distance if np.dot(offset, n) >= 0 else -distance
        return q, n, signed

    def spot(self, direction, rng=None):
        found = self.surface(direction)
        if found is None:
            return None
        s = Spot()
        s.p, s.n = found
        s.d = unit(direction)
        phi, e = self.angles(s.d)
        s.phi, s.e = float(phi), float(e)
        s.m = s.e - float(self.hairline(s.phi))
        s.side = 1.0 if s.d[0] >= 0 else -1.0
        s.rng = rng
        return s

    def spot_at(self, p, rng=None):
        return self.spot(np.asarray(p) - self.centre, rng)


# ------------------------------------------------------------------------------------------------ the scalp cap

def cap_uv(direction):
    """Polar map of the head seen from above: the crown is the centre of the texture."""
    d = np.asarray(direction, dtype=np.float64)
    d = d / np.maximum(np.linalg.norm(d, axis=-1, keepdims=True), 1e-12)
    e = np.degrees(np.arcsin(np.clip(d[..., 2], -1, 1)))
    azimuth = np.arctan2(d[..., 0], -d[..., 1])
    r = (90.0 - e) / 150.0
    return np.stack([0.5 + 0.5 * r * np.sin(azimuth), 0.5 - 0.5 * r * np.cos(azimuth)], axis=-1)


def build_cap(head, offset=SKIN_CLEARANCE, margin=6.0, inset=0.0, columns=44, rings=11):
    """A grid from the crown down to `margin` degrees past the hairline (the texture fades it out before that)."""
    vertices, directions = [], []
    for j in range(rings + 1):
        for i in range(columns):
            azimuth = -180.0 + 360.0 * i / columns
            edge = float(head.hairline(abs(azimuth))) - margin + inset
            e = 90.0 - (90.0 - edge) * (j / rings) ** 0.85
            d = head.direction(azimuth, e)
            found = head.surface(d)
            if found is None:
                raise RuntimeError("no skull along azimuth %.0f elevation %.0f" % (azimuth, e))
            vertices.append(found[0] + found[1] * offset)
            directions.append(d)
    vertices, directions = np.array(vertices), np.array(directions)
    triangles = []
    for j in range(rings):
        for i in range(columns):
            a, b = j * columns + i, j * columns + (i + 1) % columns
            c, d = a + columns, b + columns
            if j == 0:
                triangles.append((a, d, c))          # the first ring is the crown point repeated
            else:
                triangles += [(a, b, d), (a, d, c)]
    triangles = np.array(triangles, dtype=np.int64)
    normals = vertex_normals(vertices, triangles)
    # Seen from outside the triangles must be counter-clockwise; fix the whole grid at once if not.
    if np.sum(normals * (vertices - head.centre)) < 0:
        triangles = triangles[:, ::-1].copy()
        normals = -normals
    return vertices, normals, cap_uv(directions), triangles, directions


def build_shells(head, count, spacing, tile, inset_step=2.5, columns=44, rings=11):
    """Cap copies stacked outwards, mapped onto the tileable fuzz strip: cropped hair and stubble."""
    u0, u1, _, _ = hair_atlas.strip_uv("FUZZ")
    parts = []
    for k in range(count):
        vertices, normals, _, triangles, directions = build_cap(
            head, offset=SKIN_CLEARANCE + spacing * (k + 1), margin=-1.0, inset=inset_step * k, columns=columns, rings=rings)
        uv = np.zeros((len(vertices), 2))
        for j in range(rings + 1):
            for i in range(columns):
                uv[j * columns + i] = (u0 if i % 2 == 0 else u1, j * tile + 0.37 * k)
        parts.append((vertices, normals, uv, triangles))
    return parts


def paint_scalp(head, style, path):
    """The cap's texture: hair-coloured streaks along the flow, alpha fading out across the hairline."""
    import sys
    sys.path.append(os.path.join(ba.ROOT, "_tools", "pylib"))
    from PIL import Image, ImageDraw, ImageFilter
    rng = np.random.default_rng(style.get("seed", 1) + 1000)
    size, sup = SCALP_SIZE, 2
    big = size * sup
    scalp = style.get("scalp", {})

    # Alpha from the hairline, per texel.
    ys, xs = np.mgrid[0:size, 0:size]
    u, v = (xs + 0.5) / size, 1.0 - (ys + 0.5) / size
    dx, dy = (u - 0.5) * 2, (0.5 - v) * 2
    r = np.sqrt(dx * dx + dy * dy)
    azimuth = np.arctan2(dx, dy)
    e = 90.0 - r * 150.0
    phi = np.degrees(np.abs(azimuth))
    inside = e - head.hairline(phi)
    grain = rng.random((size // 4, size // 4))
    noise = np.asarray(Image.fromarray((grain * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC), dtype=np.float64) / 255.0
    soft = scalp.get("soft", 7.0)                       # degrees over which the hairline fades
    alpha = smoothstep(inside + (noise - 0.5) * soft * 0.9, -0.5, soft)
    alpha *= scalp.get("alpha", 1.0)
    thin = scalp.get("thin")                           # optional: lambda phi, e, azimuth_deg -> 0..1 keep
    if thin is not None:
        alpha *= thin(phi, e, np.degrees(azimuth))

    colour = Image.new("L", (big, big), int(255 * scalp.get("tone", 0.50)))
    draw = ImageDraw.Draw(colour)
    flow = style["flow"]
    stroke = scalp.get("stroke", 0.016)
    for _ in range(scalp.get("strokes", 7000)):
        az = rng.uniform(-180, 180)
        el = 90.0 - 150.0 * math.sqrt(rng.random()) * 0.92
        spot = head.spot(head.direction(az, el), rng)
        if spot is None or spot.m < -8:
            continue
        t = spot.tangent(flow(spot))
        a = cap_uv(spot.p - head.centre)
        b = cap_uv(spot.p + t * stroke * rng.uniform(0.5, 1.3) - head.centre)
        level = int(255 * np.clip(scalp.get("tone", 0.50) + rng.normal(0, 0.16), 0.12, 0.95))
        draw.line([(a[0] * big, (1 - a[1]) * big), (b[0] * big, (1 - b[1]) * big)], fill=level, width=sup)
    colour = colour.resize((size, size), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.4))
    grey = np.asarray(colour, dtype=np.uint8)
    out = np.dstack([grey, grey, grey, (np.clip(alpha, 0, 1) * 255).astype(np.uint8)])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(out, "RGBA").save(path)


# ------------------------------------------------------------------------------------------------ strand cards

def sample_roots(head, count, rng, min_inside=1.0, top_only=None):
    """Evenly spread spots on the hair-bearing part of the skull."""
    spots = []
    total = int(count * 3.2) + 50
    golden = math.pi * (3 - math.sqrt(5))
    for k in range(total):
        z = 1 - 1.75 * (k + 0.5) / total            # from the crown down to about -49 degrees
        radius = math.sqrt(max(0.0, 1 - z * z))
        theta = golden * k
        jitter = rng.normal(0, 0.02, 3)
        spot = head.spot(np.array([math.cos(theta) * radius, math.sin(theta) * radius, z]) + jitter, rng)
        if spot is not None and spot.m >= min_inside:
            spots.append(spot)
    rng.shuffle(spots)
    return spots


def grow(head, spot, style, layer, rng):
    """One strand as a list of (point, direction, outward normal), from the root out."""
    flow = layer.get("flow", style["flow"])
    length = layer["length"](spot) * rng.uniform(1 - layer.get("length_jitter", 0.2), 1 + layer.get("length_jitter", 0.2))
    if length <= 0.002:
        return None
    segments = max(2, min(layer.get("segments", 6), int(math.ceil(length / layer.get("min_segment", 0.008)))))
    step = length / segments
    lift = math.radians(layer["lift"](spot) + rng.normal(0, layer.get("lift_jitter", 6.0)))
    gravity = layer.get("gravity", 2.0)
    hug = layer.get("hug", 0.0)
    body = layer.get("body", False)
    clear0, clear1 = layer.get("clearance", (0.002, 0.006))
    clear1 = clear1 * rng.uniform(0.6, 1.4)
    t = spot.tangent(flow(spot) + rng.normal(0, layer.get("flow_jitter", 0.15), 3))
    direction = unit(t * math.cos(lift) + spot.n * math.sin(lift))
    swirl = rng.normal(0, layer.get("curl", 0.0))
    point = spot.p + spot.n * clear0
    path = [(point, direction, spot.n)]
    for k in range(1, segments + 1):
        f = k / segments
        here = head.spot_at(point, rng) if hug > 0 else None
        pull = np.zeros(3)
        if here is not None:
            pull = here.tangent(flow(here)) * hug
        side = unit(np.cross(direction, path[-1][2]))
        direction = unit(direction + pull + np.array([0, 0, -1.0]) * gravity * step + side * swirl * step * 10)
        # Never straight up off the head: a strand that tips past 50 degrees is laid back down.
        rise = float(np.dot(direction, path[-1][2]))
        if rise > 0.77:
            direction = unit(direction - path[-1][2] * (rise - 0.77))
        point = point + direction * step
        q, n, signed = head.nearest(point, body)
        need = clear0 + (clear1 - clear0) * f
        if signed < need:
            point = q + n * need
            direction = unit(point - path[-1][0])
        outward = unit(0.6 * n + 0.4 * unit(point - head.centre)) if signed < 0.05 else unit(point - head.centre)
        path.append((point, direction, outward))
    return path


def card(path, layer, rng, head):
    """A ribbon along a strand: vertices, normals, uv, triangles (local indices)."""
    strip = layer["strip"]
    options = hair_atlas.cells(strip)
    u0, u1 = options[int(rng.integers(len(options)))]
    ua, ub = (u0, u1) if rng.random() < 0.5 else (u1, u0)
    width = layer["width"] * rng.uniform(0.8, 1.25)
    taper = layer.get("taper", 0.15)
    tilt = math.radians(rng.normal(0, layer.get("tilt", 14.0)))
    count = len(path)
    vertices, normals, uv = [], [], []
    for k, (point, direction, outward) in enumerate(path):
        f = k / (count - 1)
        side = unit(np.cross(direction, outward))
        side = unit(side * math.cos(tilt) + outward * math.sin(tilt))
        half = 0.5 * width * (1 - taper * f)
        vertices += [point - side * half, point + side * half]
        normals += [outward, outward]
        uv += [(ua, 1.0 - f), (ub, 1.0 - f)]
    triangles = []
    for k in range(count - 1):
        a = 2 * k
        triangles += [(a, a + 1, a + 3), (a, a + 3, a + 2)]
    return np.array(vertices), np.array(normals), np.array(uv), np.array(triangles, dtype=np.int64)


class Builder:
    """Collects geometry per submesh and writes the finished mesh."""

    def __init__(self):
        self.vertices, self.normals, self.uv = [], [], []
        self.submeshes = {"scalp": [], "bulk": [], "wisps": []}
        self.count = 0

    def add(self, role, vertices, normals, uv, triangles):
        self.vertices.append(vertices)
        self.normals.append(normals)
        self.uv.append(uv)
        self.submeshes[role].append(triangles + self.count)
        self.count += len(vertices)

    def arrays(self):
        vertices, normals, uv = np.concatenate(self.vertices), np.concatenate(self.normals), np.concatenate(self.uv)
        roles = [r for r in ("scalp", "bulk", "wisps") if self.submeshes[r]]
        return vertices, normals, uv, roles, [np.concatenate(self.submeshes[r]) for r in roles]


def build_cards(head, style, builder, rng):
    for layer in style["layers"]:
        spots = sample_roots(head, layer["count"], rng, layer.get("min_inside", 3.0))
        keep = layer.get("where")
        chosen = []
        for spot in spots:
            if keep is None or rng.random() < keep(spot):
                chosen.append(spot)
            if len(chosen) >= layer["count"]:
                break
        order = layer.get("order", style.get("order"))
        chosen.sort(key=(lambda s: order(s)) if order else (lambda s: s.p[2]))
        for spot in chosen:
            path = grow(head, spot, style, layer, rng)
            if path is not None:
                builder.add(layer.get("role", "bulk"), *card(path, layer, rng, head))


def build_tails(head, style, builder, rng):
    """Free-hanging bunches (a ponytail): strands leave one point and fall, resting on the neck and back."""
    for tail in style.get("tails", []):
        origin, main = np.array(tail["origin"]), unit(tail["direction"])
        layer = {"strip": tail.get("strip", "DENSE"), "width": tail["width"], "taper": 0.3, "tilt": 40.0}
        for _ in range(tail["count"]):
            length = tail["length"] * rng.uniform(0.8, 1.05)
            segments = tail.get("segments", 8)
            step = length / segments
            direction = unit(main + rng.normal(0, tail.get("spread", 0.2), 3))
            point = origin + rng.normal(0, tail.get("radius", 0.008), 3)
            path = [(point, direction, unit(direction - main * np.dot(direction, main) + BACKWARD * 0.3))]
            for k in range(segments):
                direction = unit(direction + np.array([0, 0, -1.0]) * tail.get("gravity", 6.0) * step)
                point = point + direction * step
                q, n, signed = head.nearest(point, True)
                if signed < 0.008:
                    point = q + n * 0.008
                    direction = unit(point - path[-1][0])
                axis = origin + main * np.dot(point - origin, main)
                radial = point - axis
                outward = unit(radial) if np.linalg.norm(radial) > 1e-4 else BACKWARD
                path.append((point, direction, unit(outward + BACKWARD * 0.5)))
            builder.add(tail.get("role", "bulk"), *card(path, layer, rng, head))


BACKWARD = np.array([0.0, 1.0, 0.0])


def skin_weights(vertices, long_hair):
    """Rigid on the skull; hair hanging below the jaw eases onto the neck and chest so it stays on the shoulders."""
    names = ba.bone_names()
    indices = np.zeros((len(vertices), 4), dtype=np.int64)
    weights = np.zeros((len(vertices), 4))
    indices[:, 0], indices[:, 1], indices[:, 2] = names.index("Skull"), names.index("Neck"), names.index("UpperChest")
    if not long_hair:
        weights[:, 0] = 1.0
        return indices, weights
    z = vertices[:, 2]
    skull = smoothstep(z, 1.47, 1.555)
    neck_share = smoothstep(z, 1.36, 1.46)
    weights[:, 0] = skull
    weights[:, 1] = (1 - skull) * neck_share
    weights[:, 2] = (1 - skull) * (1 - neck_share)
    return indices, weights


def hat_shape(head, vertices, hat_mesh):
    """Deltas that tuck the hair under a hat: everything the hat covers is pressed onto the scalp."""
    hat = ba.load_reference(hat_mesh)["vertices"]
    near = hat[np.linalg.norm(hat[:, :2] - head.centre[:2], axis=1) < 0.13]
    brim = float(np.percentile(near[:, 2], 4))
    deltas = np.zeros_like(vertices)
    for i, v in enumerate(vertices):
        cover = float(smoothstep(v[2], brim - 0.012, brim + 0.01))
        if cover <= 0:
            continue
        spot = head.spot_at(v)
        if spot is None:
            continue
        deltas[i] = (spot.p + spot.n * 0.0015 - v) * cover
    return deltas


def build_style(style):
    """Geometry, textures and catalogue entry for one hairstyle. Returns (catalogue item, texture entries, stats)."""
    rng = np.random.default_rng(style.get("seed", 1))
    head = Head(style["sex"])
    head.hairline_shift = style.get("hairline_shift", 0.0)
    head.hairline_scale = style.get("hairline_adjust")
    builder = Builder()
    cap = build_cap(head)
    builder.add("scalp", cap[0], cap[1], cap[2], cap[3])
    shells = style.get("shells")
    if shells:
        for vertices, normals, uv, triangles in build_shells(head, shells["count"], shells["spacing"], shells["tile"]):
            builder.add("wisps", vertices, normals, uv, triangles)
    build_cards(head, style, builder, rng)
    build_tails(head, style, builder, rng)
    vertices, normals, uv, roles, submeshes = builder.arrays()
    indices, weights = skin_weights(vertices, style.get("long", False))
    shapes = [("Hat", hat_shape(head, vertices, "Hat")), ("Half_Hat", hat_shape(head, vertices, "HalfHat"))]
    stats = ba.write_mesh(style["id"], vertices, normals, uv, submeshes, indices, weights, shapes)
    scalp_name = "scalp_" + style["id"][len("hair_"):]
    paint_scalp(head, style, os.path.join(ba.BUILD, "textures", scalp_name + ".png"))
    layers = [{"role": r, "texture": scalp_name if r == "scalp" else "hair_atlas", "normal": None, "mask": None} for r in roles]
    item = {"id": style["id"], "kind": "hair", "sex": style["sex"], "mesh": style["id"], "layers": layers, "skinFrom": None}
    textures = [{"name": scalp_name, "file": "textures/" + scalp_name + ".png", "type": "color", "maxSize": SCALP_SIZE}]
    preview = {"vertices": vertices, "normals": normals, "uv": uv, "roles": roles, "submeshes": submeshes, "scalp": scalp_name, "head": head}
    return item, textures, stats, preview
