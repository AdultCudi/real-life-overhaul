"""THE SERVICE VEHICLES: bodies that the mod stands over one of the game's own traffic vehicles, whose renderers are
hidden meanwhile (so each body has its own wheels), and the light bar. Same rules as props.py: metres, X right,
Y forward, Z up, one mesh on the shared atlas; Unity gets (x, z, y).

    minibus          over the panel van (Transport/TransitBus.Dress): its axles on the van's, on the ground
    fire_engine      over the delivery truck (Events/EventRoad.EngineLivery): its axles on the truck's
    fire_light_bar   on the roof of a truck that has no modelled body (EventPropsBar)
    fire_bar_lens    one end of the fire engine's own light bar, lit: shown in turn over each end while it flashes

What the code relies on, and what was measured on the game's vehicles (their prefabs; README "Service vehicles"):

    Mersaidi Dash      mesh 2.33 x 2.27 x 6.66 m (0.29 to 2.56 up), colliders 2.26 x 6.81, origin on the ground under
                       its middle; axles at z +2.40 and -1.62, track 1.70, tyres 0.377 in radius
    Delivery Truck     mesh 2.72 x 3.01 x 7.43 m, ORIGIN UNDER ITS REAR AXLE (the body runs from z -2.353 to +5.077);
                       axles at z 0 and +4.118, tyres 0.42 in radius, twins at the back
    Freight Truck T1   mesh 2.89 x 4.00 x 10.98 m, origin under its rear axle too (z -2.98 to +7.99); axles at z 0 and
                       +6.876, tyres 0.542 in radius: too long for a pumper, so the engine is not put on it

A body is not assembled from boxes. It is a HULL: cross-sections (a list of levels from the sill up over the
cantrail to the middle of the roof) at stations along its length, skinned, with the cells of the skin tagged for
what they are (paint, a stripe, a window to be let in, nothing because a door goes there). The tags are painted by
a Skin, which stands in for prop_kit.Paint.
"""
import math

import bmesh
import numpy as np
from mathutils import Vector

from life_props import flat, pipe, ribbon, rod, sticker
from prop_kit import Paint, Prop, box, lathe, loft, region, rounded_rect, tube


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def curve(points, y):
    """Piecewise linear through (y, value) points given in rising y."""
    return float(np.interp(y, [p[0] for p in points], [p[1] for p in points]))


def corner(y, end, radius, sign):
    """How far a side is drawn in near an end of the body, so that the corner is round in plan. end: the y of the
    end wall; sign +1 for a front end, -1 for a rear one."""
    d = radius - (end - y) * sign
    return radius - math.sqrt(max(0.0, radius * radius - d * d)) if d > 0 else 0.0


def arch(y, centre, radius, axle, sill):
    d = abs(y - centre)
    return max(sill, axle + math.sqrt(radius * radius - d * d)) if d < radius else sill


def arch_stations(centre, radius, steps=6):
    return [round(centre + radius * math.cos(math.pi * k / steps), 4) for k in range(steps, -1, -1)]


def newell(points):
    n = Vector((0.0, 0.0, 0.0))
    for a, b in zip(points, points[1:] + points[:1]):
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n


# ---------------------------------------------------------------------------------------------- the skin

class Skin:
    """Paints a Shell: every face carries a tag, every tag a rule.

    ("swatch", name)          a finish, dirtier towards the road (by height on the whole vehicle, not on the part)
    ("flat", name)            the clean part of a finish
    ("own", name)             the region laid over the face's own corners (glass panes)
    ("decal", name, centre, right, up, (width, height))     the region projected on to the faces"""

    def __init__(self, rules, low, high, length):
        self.rules, self.low, self.high, self.length = rules, low, high, length

    def apply(self, bm, uv):
        tag = bm.faces.layers.int["tag"]
        own = bm.loops.layers.uv["param"]
        for face in bm.faces:
            rule = self.rules[face[tag]]
            kind = rule[0]
            r = region(rule[1], 7.0 if kind in ("swatch", "flat") else 1.0)
            for loop in face.loops:
                p = loop.vert.co
                if kind == "own":
                    s, t = loop[own].uv
                elif kind == "decal":
                    _, _, centre, right, up, size = rule
                    s = min(1.0, max(0.0, 0.5 + (p - centre).dot(right) / size[0]))
                    t = min(1.0, max(0.0, 0.5 + (p - centre).dot(up) / size[1]))
                else:
                    s = min(1.0, max(0.0, 0.5 + p.y / self.length))
                    t = 0.04 + 0.92 * min(1.0, max(0.0, (p.z - self.low) / (self.high - self.low))) if kind == "swatch" else 0.86
                loop[uv].uv = (r[0] + (r[2] - r[0]) * s, r[1] + (r[3] - r[1]) * t)


class Shell:
    """A mesh built face by face from shared points, each face tagged."""

    def __init__(self, low=0.15, high=1.30, length=6.5):
        self.bm = bmesh.new()
        self.tag = self.bm.faces.layers.int.new("tag")
        self.par = self.bm.loops.layers.uv.new("param")
        self.cache, self.rules, self.index = {}, [], {}
        self.grade = (low, high, length)

    def rule(self, name, kind, region_name, *more):
        self.index[name] = len(self.rules)
        self.rules.append((kind, region_name) + tuple(Vector(m) if i < 3 else m for i, m in enumerate(more)))
        return self

    def vert(self, p):
        key = (round(p[0], 4), round(p[1], 4), round(p[2], 4))
        if key not in self.cache:
            self.cache[key] = self.bm.verts.new(p)
        return self.cache[key]

    def face(self, points, tag, inside=None, out=None, uvs=None):
        """A face through the points, turned to look away from `inside` (a point) or along `out` (a direction)."""
        keep, kept = [], []
        for j, p in enumerate(points):
            p = Vector(p)
            if keep and (p - keep[-1]).length < 2e-4:
                continue
            keep.append(p)
            kept.append(uvs[j] if uvs else None)
        if len(keep) > 1 and (keep[0] - keep[-1]).length < 2e-4:
            keep.pop()
            kept.pop()
        if len(keep) < 3:
            return None
        n = newell(keep)
        if n.length < 1e-10:
            return None
        middle = sum(keep, Vector()) / len(keep)
        if (inside is not None and n.dot(middle - Vector(inside)) < 0) or (inside is None and out is not None and n.dot(Vector(out)) < 0):
            keep.reverse()
            kept.reverse()
        verts = [self.vert(p) for p in keep]
        if len(set(verts)) < len(verts):
            return None
        try:
            made = self.bm.faces.new(verts)
        except ValueError:
            return None
        made[self.tag] = self.index[tag]
        if uvs:
            for loop, st in zip(made.loops, kept):
                loop[self.par].uv = st
        return made

    def pane(self, bottom, top, frame, glass, out, margin=0.026, depth=0.024, ends=None):
        """A window let into the skin: `bottom` and `top` are its lower and upper edges (as many points each, running
        the same way). A chamfered surround of `margin` goes in by `depth` to the glass."""
        b, t = [Vector(p) for p in bottom], [Vector(p) for p in top]
        n = len(b)
        normal = newell(b + t[::-1])
        if normal.dot(Vector(out)) < 0:
            normal = -normal
        normal.normalize()
        ends = margin if ends is None else ends

        def inner(row, other, j):
            up = other[j] - row[j]
            height = up.length
            up = up / height if height > 1e-6 else Vector()
            along = row[1] - row[0] if j == 0 else row[n - 2] - row[n - 1] if j == n - 1 else Vector()
            if along.length > 1e-6:
                along.normalize()
            return row[j] + up * min(margin, height * 0.45) + along * ends - normal * depth
        bi, ti = [inner(b, t, j) for j in range(n)], [inner(t, b, j) for j in range(n)]
        centre = (sum(b, Vector()) + sum(t, Vector())) / (2 * n)

        def reveal(points):
            middle = sum((Vector(q) for q in points), Vector()) / len(points)
            self.face(points, frame, out=(centre - middle).normalized() * 0.5 + normal)
        length = [0.0]
        for j in range(n - 1):
            length.append(length[-1] + (bi[j + 1] - bi[j]).length)
        for j in range(n - 1):
            reveal([b[j], b[j + 1], bi[j + 1], bi[j]])
            reveal([t[j], t[j + 1], ti[j + 1], ti[j]])
            u0, u1 = length[j] / length[-1], length[j + 1] / length[-1]
            self.face([bi[j], bi[j + 1], ti[j + 1], ti[j]], glass, out=normal, uvs=[(u0, 0.0), (u1, 0.0), (u1, 1.0), (u0, 1.0)])
        reveal([b[0], bi[0], ti[0], t[0]])
        reveal([b[-1], bi[-1], ti[-1], t[-1]])

    def panel(self, origin, across, up, width, height, tag, out, window=None, frame="rubber", glass="glass", **pane):
        """A flat rectangle from `origin` (its lower corner), optionally with a window (u0, v0, u1, v1 in metres)."""
        o, ex, ez = Vector(origin), Vector(across).normalized(), Vector(up).normalized()

        def at(u, v):
            return o + ex * u + ez * v
        outer = [at(0, 0), at(width, 0), at(width, height), at(0, height)]
        if not window:
            self.face(outer, tag, out=out)
            return
        u0, v0, u1, v1 = window
        hole = [at(u0, v0), at(u1, v0), at(u1, v1), at(u0, v1)]
        for k in range(4):
            self.face([outer[k], outer[(k + 1) % 4], hole[(k + 1) % 4], hole[k]], tag, out=out)
        self.pane(hole[:2], [hole[3], hole[2]], frame, glass, out, **pane)

    def add_to(self, prop, sharp=40.0):
        prop.add(self.bm, Skin(self.rules, *self.grade), sharp=sharp)


class Hull:
    """Cross-sections at stations along Y. section(y) -> the right-hand half as (x, z) levels from the sill up to
    the middle of the roof (x = 0); the left is its mirror. warp(point, y) may then move a point (a raked front)."""

    def __init__(self, shell, stations, section, warp=None):
        self.shell, self.ys, self.warp = shell, list(stations), warp
        self.rings = [section(y) for y in self.ys]
        self.levels = len(self.rings[0])

    def at(self, y):
        return min(range(len(self.ys)), key=lambda i: abs(self.ys[i] - y))

    def P(self, i, k, side=1):
        x, z = self.rings[i][k]
        p = Vector((side * x, self.ys[i], z))
        return self.warp(p, self.ys[i]) if self.warp else p

    def skin(self, tag, under=None):
        """tag(y0, y1, k, side) -> the tag of the cell between two stations and levels k and k + 1, or None for a gap."""
        for i in range(len(self.ys) - 1):
            zs = [z for _, z in self.rings[i] + self.rings[i + 1]]
            inside = (0.0, (self.ys[i] + self.ys[i + 1]) / 2, (min(zs) + max(zs)) / 2)
            for side in (1, -1):
                for k in range(self.levels - 1):
                    name = tag(self.ys[i], self.ys[i + 1], k, side)
                    if name:
                        self.shell.face([self.P(i, k, side), self.P(i, k + 1, side), self.P(i + 1, k + 1, side), self.P(i + 1, k, side)], name, inside=inside,
                                        uvs=[(0.0, 0.0), (0.0, 1.0), (1.0, 1.0), (1.0, 0.0)])
            if under:
                self.shell.face([self.P(i, 0, 1), self.P(i, 0, -1), self.P(i + 1, 0, -1), self.P(i + 1, 0, 1)], under, out=(0, 0, -1))

    def wall(self, i, tag, out, cuts=None, first=0, last=None):
        """Closes the hull at station i with strips across it. tag(k, m) -> tag of piece m of strip k; cuts: {k: [x, ...]}
        cuts strip k at those distances either side of the middle (a strip uncut is one piece, m = 0). Pieces whose
        tag is None are left open and returned as {(k, m): (lower left, lower right, upper right, upper left)}."""
        left_open = {}
        for k in range(first, self.levels - 1 if last is None else last):
            l0, r0, l1, r1 = self.P(i, k, -1), self.P(i, k, 1), self.P(i, k + 1, -1), self.P(i, k + 1, 1)
            xs = sorted(cuts.get(k, [])) if cuts else []
            edges = [None] + [-x for x in reversed(xs)] + xs + [None]

            def on(a, b, x):
                return a if x is None and a is l0 or x is None and a is l1 else b if x is None else a + (b - a) * ((x - a.x) / (b.x - a.x)) if abs(b.x - a.x) > 1e-6 else a
            for m in range(len(edges) - 1):
                a = l0 if edges[m] is None else on(l0, r0, edges[m])
                b = r0 if edges[m + 1] is None else on(l0, r0, edges[m + 1])
                c = r1 if edges[m + 1] is None else on(l1, r1, edges[m + 1])
                d = l1 if edges[m] is None else on(l1, r1, edges[m])
                name = tag(k, m)
                if name:
                    self.shell.face([a, b, c, d], name, out=out)
                else:
                    left_open[(k, m)] = (a, b, c, d)
        return left_open

    def window(self, y0, y1, k, side, frame="rubber", glass="glass", **pane):
        """Lets a window into the cells of level k between two stations (the cells must have been left open)."""
        i0, i1 = self.at(y0), self.at(y1)
        bottom = [self.P(i, k, side) for i in range(i0, i1 + 1)]
        top = [self.P(i, k + 1, side) for i in range(i0, i1 + 1)]
        self.shell.pane(bottom, top, frame, glass, (side, 0, 0), **pane)


# ---------------------------------------------------------------------------------------------- parts both vehicles have

def road_wheel(p, x, y, radius, width, face, side, twin=False, dish=0.06, segments=14):
    """A wheel standing on the ground, its outer face at x (the side it looks to: +1 right, -1 left)."""
    w, r = width / 2, radius
    profile = [(r * 0.50, -w), (r * 0.90, -w), (r, -w + 0.045)]
    if twin:
        profile += [(r, -0.035), (r * 0.88, -0.012), (r * 0.88, 0.012), (r, 0.035)]
    profile += [(r, w - 0.045), (r * 0.90, w), (r * 0.64, w - 0.006), (r * 0.60, w - 0.006 - dish * 0.55), (r * 0.27, w - 0.006 - dish), (r * 0.13, w - 0.006 - dish * 0.2)]
    paint = Paint("rubber_black", grade=False).planar(face, (0, 0, 1), (1, 0, 0), (0, 0, 0), (2 * r, 2 * r), facing=0.45)
    p.add(lathe(profile, segments, cap_top=True, cap_bottom=True), paint, at=(x - side * w, y, radius), rot=(0, 90 * side, 0), sharp=38)


def arch_lip(p, x, y, radius, axle, side, paint="plastic_grey", thick=0.022, steps=8):
    """The lip round a wheel arch."""
    path = [(x, y + radius * math.cos(math.pi * k / steps), axle + radius * math.sin(math.pi * k / steps)) for k in range(steps + 1)]
    path = [(x, path[0][1], axle - 0.03)] + path + [(x, path[-1][1], axle - 0.03)]
    p.add(tube(path, thick, 5, caps=True), flat(paint), sharp=75)


def lamp(p, name, centre, normal, right, size, depth=0.03, bezel="gloss_black", mirrored=False):
    """A lamp unit standing proud of the body, its lens a decal."""
    n, r = Vector(normal).normalized(), Vector(right).normalized()
    up = n.cross(r)
    c = Vector(centre)
    half = [r * (size[0] / 2), up * (size[1] / 2)]
    ring = lambda grow, lift: [tuple(c + half[0] * (sa * grow) + half[1] * (sb * grow) + n * lift) for sa, sb in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
    part = loft([ring(1.0, -0.01), ring(1.0, depth * 0.6), ring(0.94, depth)], cap_end=True)
    part.normal_update()
    if sum(f.normal.dot(n) for f in part.faces if f.normal.dot(n) > 0.9 or f.normal.dot(n) < -0.9) < 0:
        bmesh.ops.reverse_faces(part, faces=part.faces[:])
    paint = Paint(bezel, grade=False).planar(name, tuple(n), tuple(-r if mirrored else r), tuple(c), (size[0] * 0.94, size[1] * 0.94), facing=0.9)
    p.add(part, paint, sharp=30)


def light_bar(p, at=(0.0, 0.0, 0.0), length=1.30, depth=0.28, height=0.17, feet=True):
    """A modern low light bar lying along X, its underside at `at`: feet, a dark base rail, lens modules red at the
    ends and clear in the middle, their faces ribbed (the decal), a spine along the top."""
    ox, oy, oz = at
    k = length / 1.30
    foot, rail = 0.030, 0.034
    if feet:
        for sx in (-1, 1):
            p.add(box(0.11 * k, depth * 0.86, foot + 0.006, bevel=0.007), flat("rubber_black"), at=(ox + sx * length * 0.33, oy, oz + (foot + 0.006) / 2))
    base = [[(ox + x * sx, oy + y, oz + z) for x, y in rounded_rect(length - 0.01, depth * w, 0.05 * k, 2)] for z, w, sx in ((foot, 0.80, 0.98), (foot + 0.008, 0.90, 1.0), (foot + rail, 0.90, 1.0))]
    p.add(loft(base, cap_start=True, cap_end=True), flat("gloss_black"), sharp=40)
    z0, z1 = oz + foot + rail, oz + height - 0.003
    face = lambda sign: (0, sign, 0)
    lens = (Paint("lens_red", grade=False)
            .planar("svc_light_bar", face(1), (-1, 0, 0), (ox, oy, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55)
            .planar("svc_light_bar", face(-1), (1, 0, 0), (ox, oy, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55))
    half = length / 2
    stations = [(-half, 0.55, 0.60), (-half + 0.035 * k, 0.86, 0.90), (-half + 0.10 * k, 1.0, 1.0), (half - 0.10 * k, 1.0, 1.0), (half - 0.035 * k, 0.86, 0.90), (half, 0.55, 0.60)]
    d = depth / 2
    rings = []
    for x, wide, tall in stations:
        top = z0 + (z1 - z0) * tall
        rings.append([(ox + x, oy + y * wide * 0.965, z) for y, z in ((-d, z0), (d, z0), (d * 0.94, z0 + (top - z0) * 0.62), (d * 0.62, top), (-d * 0.62, top), (-d * 0.94, z0 + (top - z0) * 0.62))])
    p.add(loft(rings, cap_start=True, cap_end=True), lens, sharp=28)
    p.add(box(length * 0.20, depth * 0.66, 0.012, bevel=0.004), flat("svc_lens_clear"), at=(ox, oy, z1 - 0.003))
    for sx in (-1, 1):                                   # the dividers between the modules, and the end caps
        p.add(box(0.016, depth, (z1 - z0) + 0.004), flat("gloss_black"), at=(ox + sx * length * 0.095, oy, (z0 + z1) / 2))
        p.add(box(0.014, depth, (z1 - z0) * 1.0), flat("gloss_black"), at=(ox + sx * length * 0.285, oy, (z0 + z1) / 2 - 0.002))


# ---------------------------------------------------------------------------------------------- the shuttle bus

BUS_W = 1.035                 # half the saloon's width at the waist: trim stands out to 1.05
CAB_W = 0.955
BUS_AXLES = (2.40, -1.62)     # the Mersaidi Dash's own axles (wheelbase 4.02): the wheels turn about where the van's do
BUS_REAR, BUS_NOSE = -3.27, 3.28          # the back wall and the nose; the bumpers reach -3.33 and +3.33 (the van is 6.66 long)
BUS_TRACK = 1.70
BUS_TYRE = 0.375
BUS_ARCH = 0.45
BUS_DOOR = (0.22, 1.30)       # the passenger door, kerb side, behind the cab: its middle is 0.76 m ahead of the body's (TransitBus.BodyDoorAhead)
BUS_ROOF, BUS_TOP = 2.62, 2.75
BUS_BULKHEAD = 1.37           # where the saloon ends and the cab begins
BUS_WINDOWS = ((-3.09, -2.07), (-2.01, -1.23), (-1.17, -0.545), (-0.485, 0.14))          # both sides; then the door, or one more window
CAB_SHIFT = 0.35              # the cab stands this much further forward than it was first drawn (the front axle went from 2.05 to 2.40)
SCREEN_BASE = 2.50 + CAB_SHIFT


def cab_y(y):
    """A station of the cab as first drawn (front axle at 2.05, nose at 3.045) -> where it is now: everything back to the
    foot of the screen moves forward with the axle, and the bonnet ahead of it is shortened so that the nose ends at BUS_NOSE."""
    return y + CAB_SHIFT if y <= 2.50 else SCREEN_BASE + (y - 2.50) * (BUS_NOSE - SCREEN_BASE) / (3.045 - 2.50)


def minibus():
    p = Prop("minibus", "lit", pivot="ground centre under the middle of the body; nose to +Y",
             note="cutaway shuttle bus body with its own wheels (axles at y 2.40 and -1.62, the Mersaidi Dash's; track 1.70); passenger door on +X centred y 0.76; "
                  "the roof is flat at 2.75 m from y 1.50 to 2.45 (the route board) and on the air-conditioning pod from y -3.18 to -2.23 (the number)")
    s = Shell(low=0.20, high=1.45, length=6.4)
    s.rule("white", "swatch", "svc_coach_white").rule("blue", "swatch", "svc_coach_blue").rule("sky", "flat", "svc_coach_sky")
    s.rule("roof", "flat", "svc_coach_white").rule("black", "flat", "gloss_black").rule("rubber", "flat", "rubber_black")
    s.rule("under", "flat", "plastic_grey").rule("glass", "own", "svc_glass").rule("screen", "own", "svc_screen")
    s.rule("door", "flat", "car_silver").rule("tread", "flat", "aluminium")
    rear_axle, front_axle = BUS_AXLES[1], BUS_AXLES[0]

    # ---- the saloon: sill, skirt, stripe, waist, window band, cantrail, round roof edge, roof.
    def saloon(y, cap=False):
        d = corner(y, BUS_REAR, 0.14, -1)
        rise = smooth01((y - 1.15 - CAB_SHIFT) / 0.40)
        top = BUS_ROOF + (BUS_TOP - BUS_ROOF) * rise
        floor = 0.0
        if cap:                                            # over the cab: the cap's underside, its brow drawn in and down
            d = curve(((2.10, 0.0), (2.28, 0.035), (2.40, 0.13), (2.46, 0.30)), y - CAB_SHIFT)
            top = curve(((2.10, BUS_TOP), (2.28, 2.722), (2.40, 2.65), (2.46, 2.54)), y - CAB_SHIFT)
            floor = curve(((2.10, 2.15), (2.28, 2.17), (2.40, 2.23), (2.46, 2.33)), y - CAB_SHIFT)
        w = BUS_W - d
        r = min(0.20, (top - floor) * 0.48) if cap else 0.20
        cx, cz = w - 0.045 - r, top - r
        crown = 0.0 if cap else 0.012 * (1.0 - rise)
        ring = [(w - 0.03, arch(y, rear_axle, BUS_ARCH, BUS_TYRE, 0.36)), (w, 0.84), (w, 0.94), (w, 1.22), (w - 0.03, 2.12), (w - 0.045, cz),
                (cx + r * math.cos(math.radians(30)), cz + r * math.sin(math.radians(30))), (cx + r * math.cos(math.radians(60)), cz + r * math.sin(math.radians(60))),
                (cx, top), (0.45, top + crown), (0.0, top + crown * 1.3)]
        if cap:
            ring = [(w - 0.045, max(z, floor)) if k < 5 else (x, max(z, floor)) for k, (x, z) in enumerate(ring)]
        return ring
    body_ys = sorted(set([BUS_REAR, BUS_REAR + 0.02, BUS_REAR + 0.07, BUS_REAR + 0.14, BUS_DOOR[0], round((BUS_DOOR[0] + BUS_DOOR[1]) / 2, 4), BUS_DOOR[1], BUS_BULKHEAD]
                         + [y for w in BUS_WINDOWS for y in w] + arch_stations(rear_axle, BUS_ARCH)))
    body = Hull(s, body_ys, saloon)

    def body_tag(y0, y1, k, side):
        if side > 0 and y0 >= BUS_DOOR[0] - 0.001 and y1 <= BUS_DOOR[1] + 0.001 and k < 4:
            return None
        if k == 3:
            if any(y0 >= a - 0.001 and y1 <= b + 0.001 for a, b in BUS_WINDOWS) or (side < 0 and y0 >= BUS_DOOR[0] - 0.001 and y1 <= BUS_DOOR[1] + 0.001):
                return None
            return "white" if y1 <= BUS_WINDOWS[0][0] + 0.001 else "black"
        return ("blue", "sky", "white", None, "white")[k] if k < 5 else "roof"
    body.skin(body_tag, under="under")
    for a, b in BUS_WINDOWS:
        for side in (1, -1):
            body.window(a, b, 3, side)
    body.window(BUS_DOOR[0], BUS_DOOR[1], 3, -1)
    # The back: a wide window, the stripes carried round.
    back = body.wall(0, lambda k, m: None if k == 3 and m == 1 else ("blue", "sky", "white", "white", "white")[k] if k < 5 else "roof", (0, -1, 0), cuts={3: [0.74]})
    a, b, c, d = back[(3, 1)]
    s.pane([a, b], [d, c], "rubber", "glass", (0, -1, 0))
    # The front of the saloon behind the cab, and the cap over the cab.
    body.wall(len(body_ys) - 1, lambda k, m: "white", (0, 1, 0), last=4)
    cap_ys = [BUS_BULKHEAD] + [round(y + CAB_SHIFT, 4) for y in (1.15, 1.28, 1.42, 1.55, 2.10, 2.28, 2.40, 2.46)]
    cap = Hull(s, cap_ys, lambda y: saloon(y, cap=True) if y > BUS_BULKHEAD else [(x, max(z, 2.15)) if k >= 5 else (BUS_W - 0.045, max(z, 2.15)) for k, (x, z) in enumerate(saloon(y))])
    cap.skin(lambda y0, y1, k, side: "roof" if k >= 4 else None, under="roof")
    cap.wall(len(cap_ys) - 1, lambda k, m: "roof", (0, 1, 0))
    # Joins the saloon's roof to the cap's first ring: they are the same above the cab's roof line.

    # ---- the cab: a van's nose, bonnet and raked screen, narrower than the coachwork behind it.
    profile = tuple((cab_y(y), z) for y, z in ((0.9, 2.14), (2.05, 2.14), (2.50, 1.28), (2.60, 1.255), (2.885, 1.15), (2.97, 1.09), (3.02, 1.02), (3.045, 0.95)))

    def cab(y):
        top = curve(profile, y)
        d = corner(y, BUS_NOSE, 0.16, 1)
        w = CAB_W - d
        z3, z4 = min(1.22, top - 0.09), min(2.04, top - 0.07)
        x4 = w - 0.09 * (z4 - z3) / 0.82
        sill = arch(y, front_axle, BUS_ARCH, BUS_TYRE, 0.36 if y < cab_y(2.6) else 0.40)
        return [(w - 0.03, sill), (w, min(0.84, top - 0.24)), (w, min(0.94, top - 0.16)), (w, z3), (x4, z4), (x4 - 0.05, top - 0.035), ((x4 - 0.05) * 0.60, top - 0.008), (0.0, top)]
    cab_ys = sorted(set([round(cab_y(y), 4) for y in (1.00, 1.08, 2.60, 2.885, 2.97, 3.02, 3.045)] + arch_stations(front_axle, BUS_ARCH)))
    cab_side = round(cab_y(1.08), 4)                       # where the cab door's window begins
    nose = Hull(s, cab_ys, cab)
    screen = (round(cab_y(2.05), 4), round(cab_y(2.50), 4))

    def cab_tag(y0, y1, k, side):
        if k >= 5 and y0 >= screen[0] - 0.001 and y1 <= screen[1] + 0.001:
            return None
        if k == 3 and y0 >= cab_side - 0.001 and y1 <= screen[1] + 0.001:
            return None
        if k < 2 and y1 <= front_axle - BUS_ARCH + 0.001:
            return ("blue", "sky")[k]
        return "white"
    nose.skin(cab_tag, under="under")
    for side in (1, -1):
        nose.window(cab_side, screen[0], 3, side, ends=0.035)
        nose.window(screen[0], screen[1], 3, side, margin=0.018, ends=0.03)
    i0, i1 = nose.at(screen[1]), nose.at(screen[0])
    across = [(5, -1), (6, -1), (7, 1), (6, 1), (5, 1)]
    s.pane([nose.P(i0, k, side) for k, side in across], [nose.P(i1, k, side) for k, side in across], "rubber", "screen", (0, 1, 0.6), margin=0.03, depth=0.02)
    nose.wall(len(cab_ys) - 1, lambda k, m: "white", (0, 1, 0))

    # ---- the passenger door: two glazed leaves set back in a portal, a tread at the sill.
    i0, i1 = body.at(BUS_DOOR[0]), body.at(BUS_DOOR[1])
    xd = BUS_W - 0.055
    for i, out in ((i0, (0, 1, 0)), (i1, (0, -1, 0))):
        for k in range(4):
            a, b = body.P(i, k), body.P(i, k + 1)
            s.face([a, b, (xd, b.y, b.z), (xd, a.y, a.z)], "black", out=out)
    for i in range(i0, i1):
        a, b = body.P(i, 4), body.P(i + 1, 4)
        s.face([a, b, (xd, b.y, b.z), (xd, a.y, a.z)], "black", out=(0, 0, -1))
        a, b = body.P(i, 0), body.P(i + 1, 0)
        s.face([a, b, (xd, b.y, 0.40), (xd, a.y, 0.40)], "tread", out=(0.4, 0, 1))
    mid = (BUS_DOOR[0] + BUS_DOOR[1]) / 2
    for y0, y1 in ((BUS_DOOR[0], mid - 0.012), (mid + 0.012, BUS_DOOR[1])):
        s.panel((xd, y0, 0.40), (0, 1, 0), (0, 0, 1), y1 - y0, 1.72, "door", (1, 0, 0), window=(0.07, 0.24, y1 - y0 - 0.07, 1.62), margin=0.02, depth=0.016)
    s.panel((xd + 0.012, mid - 0.016, 0.40), (0, 1, 0), (0, 0, 1), 0.032, 1.72, "rubber", (1, 0, 0))
    s.add_to(p, sharp=40)

    # ---- what stands on the coachwork.
    for side in (1, -1):
        # Wheels in their arches; the saloon's arches have a lip, the body builder's flare.
        road_wheel(p, side * (BUS_TRACK / 2 + 0.115), front_axle, BUS_TYRE, 0.23, "svc_wheel", side)
        road_wheel(p, side * (BUS_TRACK / 2 + 0.115), rear_axle, BUS_TYRE, 0.23, "svc_wheel", side)
        arch_lip(p, side * (BUS_W - 0.003), rear_axle, BUS_ARCH + 0.012, BUS_TYRE, side, thick=0.018)
        arch_lip(p, side * (CAB_W + 0.004), front_axle, BUS_ARCH + 0.012, BUS_TYRE, side, thick=0.018)
        # Lettering, the same way round on both sides.
        fore = (0, side, 0)
        sticker(p, "svc_bus_title", (side * (BUS_W - 0.0375), -1.30, 2.27), (side, 0, 0.05), fore, (1.96, 0.2205), lift=0.002)
        sticker(p, "svc_bus_shuttle", (side * BUS_W, -2.58, 1.085), (side, 0, 0), fore, (0.72, 0.1575), lift=0.002)
        sticker(p, "svc_bus_number", (side * CAB_W, 1.42 + CAB_SHIFT, 1.04), (side, 0, 0), fore, (0.26, 0.13), lift=0.002)
        # Cab door: its shut lines and handle; the mirror on its arm.
        p.add(box(0.004, 0.008, 0.46), flat("rubber_black"), at=(side * (CAB_W + 0.001), 2.33 + CAB_SHIFT, 0.985))
        p.add(box(0.012, 0.10, 0.028, bevel=0.004), flat("gloss_black"), at=(side * (CAB_W + 0.004), 1.17 + CAB_SHIFT, 1.17))
        pipe(p, [(side * (CAB_W - 0.03), 2.40 + CAB_SHIFT, 1.33), (side * (CAB_W + 0.06), 2.47 + CAB_SHIFT, 1.36), (side * (CAB_W + 0.06), 2.47 + CAB_SHIFT, 1.52)], 0.012, "gloss_black", 5, bend=0.03)
        p.add(box(0.06, 0.045, 0.25, bevel=0.012), flat("gloss_black"), at=(side * (CAB_W + 0.062), 2.475 + CAB_SHIFT, 1.60))
        p.add(box(0.05, 0.004, 0.22), flat("svc_lens_clear"), at=(side * (CAB_W + 0.062), 2.451 + CAB_SHIFT, 1.60))
        # Side repeater and marker lamps.
        p.add(box(0.012, 0.07, 0.035, bevel=0.004), flat("lens_amber"), at=(side * (CAB_W + 0.003), cab_y(2.70) + 0.03, 0.93))
        p.add(box(0.010, 0.09, 0.03, bevel=0.003), flat("lens_amber"), at=(side * (BUS_W + 0.003), BUS_REAR + 0.24, 0.89))
        p.add(box(0.010, 0.09, 0.03, bevel=0.003), flat("lens_red"), at=(side * (BUS_W + 0.001), BUS_REAR + 0.16, 2.30))
        # Head and tail lamps.
        lamp(p, "svc_headlamp", (side * 0.60, BUS_NOSE, 0.80), (0, 1, 0), (-1, 0, 0), (0.32, 0.18), mirrored=side < 0)
        lamp(p, "svc_tail_lamp", (side * 0.775, BUS_REAR, 1.03), (0, -1, 0), (1, 0, 0), (0.13, 0.36), depth=0.025)
    # The nose: grille and badge, bumper, plate, wipers.
    sticker(p, "svc_bus_grille", (0, BUS_NOSE, 0.80), (0, 1, 0), (-1, 0, 0), (0.80, 0.225), lift=0.004)
    bumper = [(x, cab_y(y)) for x, y in ((-0.985, 2.62), (-0.985, 2.90), (-0.93, 3.015), (-0.80, 3.059), (0.80, 3.059), (0.93, 3.015), (0.985, 2.90), (0.985, 2.62))]
    away = lambda q: (q.x * (1.0 if abs(q.x) > 0.9 else 0.25), max(0.0, q.y - cab_y(2.80)) * 4.0 + (1.0 if abs(q.x) < 0.9 else 0.0), 0.0)
    p.add(ribbon([(x, y, 0.47) for x, y in bumper], away, 0.25, 0.07), Paint("plastic_grey"), sharp=50)
    p.add(box(0.36, 0.006, 0.17), Paint("paper", grade=False).planar("svc_plate_bus", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.36, 0.17)), at=(0, BUS_NOSE + 0.052, 0.47))
    for x in (-0.62, 0.62):                                # fog lamps in the bumper
        p.add(lathe([(0.046, 0.0), (0.046, 0.004), (0.034, 0.008)], 10, cap_top=True), flat("svc_lens_clear"), at=(x, BUS_NOSE + 0.047, 0.44), rot=(-90, 0, 0), sharp=40)
    glass_at = lambda x, y: Vector((x, y + CAB_SHIFT + 0.016, 1.28 + (2.50 - y) * (0.86 / 0.45) + 0.012))      # a point just off the screen
    for x0, x1 in ((-0.12, -0.66), (0.50, -0.04)):         # wipers, parked
        rod(p, tuple(glass_at(x0, 2.485)), tuple(glass_at(x1, 2.40)), 0.009, "rubber_black", 4)
        rod(p, tuple(glass_at(x0 + (x1 - x0) * 0.45, 2.445)), tuple(glass_at(x0 + (x1 - x0) * 0.45, 2.39) + Vector((0, 0.004, 0))), 0.006, "rubber_black", 4)
    # The back: bumper, plate, the emergency door's outline and handle, high lamps, number.
    p.add(box(1.84, 0.075, 0.20, bevel=0.03), Paint("plastic_grey"), at=(0, BUS_REAR - 0.0225, 0.47))
    p.add(box(0.36, 0.008, 0.17), Paint("paper", grade=False).planar("svc_plate_bus", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.36, 0.17)), at=(0, BUS_REAR - 0.005, 0.72))
    for x in (-0.47, 0.47):
        p.add(box(0.012, 0.006, 1.52), flat("rubber_black"), at=(x, BUS_REAR - 0.002, 1.38))
    p.add(box(0.952, 0.006, 0.012), flat("rubber_black"), at=(0, BUS_REAR - 0.002, 0.62))
    p.add(box(0.05, 0.03, 0.16, bevel=0.01), flat("paint_red"), at=(0.36, BUS_REAR - 0.01, 1.08))
    for x in (-0.20, 0.0, 0.20):
        p.add(box(0.07, 0.012, 0.03, bevel=0.004), flat("lens_red"), at=(x, BUS_REAR - 0.003, 2.34))
    sticker(p, "svc_bus_number", (-0.70, BUS_REAR, 2.30), (0, -1, 0), (1, 0, 0), (0.24, 0.12), lift=0.002)
    # Under the floor: something where the axles are, so that one does not see through the arches.
    for y in BUS_AXLES:
        p.add(box(1.36, 0.60, 0.42), flat("rubber_black"), at=(0, y, 0.44))
        rod(p, (-0.70, y, BUS_TYRE), (0.70, y, BUS_TYRE), 0.05, "steel_dark", 6, caps=False)
    # The roof: marker lamps on the brow, a hatch, the air-conditioning pod with two fans, an aerial.
    for x in (-0.50, -0.25, 0.0, 0.25, 0.50):
        p.add(box(0.06, 0.03, 0.022, bevel=0.006), flat("lens_amber"), at=(x, 2.415 + CAB_SHIFT, 2.615), rot=(40, 0, 0))
    p.add(box(0.66, 0.66, 0.03, bevel=0.012), flat("svc_coach_white"), at=(0, -0.30, BUS_ROOF + 0.022))
    p.add(box(0.50, 0.50, 0.035, bevel=0.012), flat("plastic_grey"), at=(0, -0.30, BUS_ROOF + 0.036))
    POD = BUS_REAR + 0.68                                  # the middle of the pod (TransitBus.BodyNumberBack is 0.36 m behind it)
    pod_top = BUS_TOP - 0.004                              # the fans on it come to 2.75 and no higher: the code measures the roof by the bounds
    pod = [[(x * a, POD + y * b, z) for x, y in rounded_rect(1.46, 1.34, 0.16, 2)] for a, b, z in ((1.0, 1.0, BUS_ROOF - 0.01), (1.0, 1.0, BUS_TOP - 0.035), (0.955, 0.95, pod_top))]
    vents = Paint("svc_coach_white", grade=False)
    for normal, right, centre in (((1, 0, 0), (0, 1, 0), (0.73, POD, 2.685)), ((-1, 0, 0), (0, -1, 0), (-0.73, POD, 2.685)), ((0, -1, 0), (1, 0, 0), (0, POD - 0.67, 2.685))):
        vents.planar("svc_vent", normal, right, centre, (0.96, 0.085), facing=0.9)
    p.add(loft(pod, cap_end=True), vents, sharp=35)
    for x in (-0.36, 0.36):                                # the fans: flush, so that the pod's top stays the highest thing
        p.add(lathe([(0.235, -0.01), (0.235, 0.0015)], 12, cap_top=True), flat("plastic_grey"), at=(x, POD + 0.24, pod_top), sharp=30)
        p.add(lathe([(0.065, 0.0), (0.065, 0.0025)], 8, cap_top=True), flat("aluminium"), at=(x, POD + 0.24, pod_top), sharp=30)
        for turn in range(3):
            p.add(box(0.012, 0.40, 0.002), flat("gloss_black"), at=(x, POD + 0.24, pod_top + 0.0012), rot=(0, 0, 60 * turn))
    rod(p, (0.55, 0.55, BUS_ROOF), (0.55, 0.50, BUS_TOP - 0.012), 0.006, "rubber_black", 4)
    return p


# ---------------------------------------------------------------------------------------------- the fire engine

# Made to the game's Delivery Truck (README, "Service vehicles"): 2.72 x 3.01 x 7.43 m, its axles 4.118 m apart, 2.353 m of it
# behind the rear axle and 0.96 m ahead of the front one. The pivot is under the middle of that length.
ENG_HALF = 3.715              # bumper and back step reach +-3.715
ENG_W = 1.25                  # half the body's width; mirrors stand out to 1.36
ENG_AXLES = (2.756, -1.362)   # the truck's own (wheelbase 4.118): EngineLivery puts these on the truck's axles
ENG_TYRE, ENG_ARCH = 0.51, 0.60
ENG_FRONT, ENG_BACK = ENG_HALF - 0.18, -ENG_HALF + 0.15          # the cab's front wall and the body's back wall
ENG_CAB = ENG_AXLES[0] - 1.35 # the back of the cab
ENG_PUMP = (ENG_CAB - 1.30, ENG_CAB)      # the pump house, between the cab and the body: its panels are centred y 0.756 on both sides
ENG_PUMP_MID = (ENG_PUMP[0] + ENG_PUMP[1]) / 2
ENG_BODY_MID = (ENG_BACK + ENG_PUMP[0]) / 2
ENG_BAND = (1.12, 1.28)       # the reflective band
ENG_LOCKERS = ((round(ENG_AXLES[1] + 0.65, 4), round(ENG_PUMP[0] - 0.06, 4), True), (round(ENG_AXLES[1] - 0.52, 4), round(ENG_AXLES[1] + 0.52, 4), False),
               (round(ENG_BACK + 0.08, 4), round(ENG_AXLES[1] - 0.68, 4), True))       # (from, to, full height)
ENG_OUTLET = round((ENG_LOCKERS[0][0] + ENG_LOCKERS[0][1]) / 2, 4)       # the discharge outlets, low on both sides: where the code lays the hose from
# The cab is short: the crew door behind the wheel, the front door over it (a cab-over), a hand's breadth of nose ahead of the arch.
CAB_WINDOWS = ((round(ENG_CAB + 0.10, 4), round(ENG_AXLES[0] - 0.68, 4)), (round(ENG_AXLES[0] - 0.60, 4), round(ENG_FRONT - 0.16, 4)))
ENG_RAKE = round(ENG_FRONT - 0.77, 4)      # the screen leans back from here up


ENG_BAR, ENG_BAR_UP = round(ENG_FRONT - 1.00, 4), 3.006       # the light bar on the cab roof: the middle of its underside


def tread(p, x0, x1, y0, y1, z, tile=0.30):
    """Tread plate lying flat: tiles of the decal, so that the diamonds stay their size."""
    nx, ny = max(1, round((x1 - x0) / tile)), max(1, round((y1 - y0) / tile))
    w, d = (x1 - x0) / nx, (y1 - y0) / ny
    for i in range(nx):
        for j in range(ny):
            sticker(p, "svc_tread", (x0 + w * (i + 0.5), y0 + d * (j + 0.5), z), (0, 0, 1), (1, 0, 0), (w, d), lift=0.0)


def fire_engine():
    p = Prop("fire_engine", "lit", pivot="ground centre under the middle of the body; nose to +Y",
             note="pumper body with its own wheels (axles at y 2.756 and -1.362, the Delivery Truck's) and its own light bar on the cab roof; pump panels on both sides centred y 0.756, "
                  "0.66 to 2.14 m up; discharge outlets low on both sides at y -0.333; crew doors centred y 1.78, front doors over the wheel, y 2.80")
    s = Shell(low=0.30, high=1.70, length=7.6)
    s.rule("red", "swatch", "svc_engine_red").rule("band", "flat", "reflective_white").rule("roof", "flat", "svc_coach_white")
    s.rule("alu", "flat", "aluminium").rule("black", "flat", "gloss_black").rule("rubber", "flat", "rubber_black").rule("under", "flat", "rubber_black")
    s.rule("glass", "own", "svc_glass").rule("screen", "own", "svc_screen")
    s.rule("chev", "decal", "svc_chevrons", (0, ENG_BACK, 1.36), (1, 0, 0), (0, 0, 1), (2 * ENG_W, 1.48))
    s.rule("hose", "decal", "svc_hose_bed", (0, ENG_BODY_MID, 2.60), (0, 1, 0), (-1, 0, 0), (ENG_PUMP[0] - ENG_BACK + 0.05, 1.24))
    for side, name in ((1, "R"), (-1, "L")):
        s.rule("panel" + name, "decal", "svc_pump_panel", (side * (ENG_W - 0.03), ENG_PUMP_MID, 1.40), (0, side, 0), (0, 0, 1), (1.30, 1.48))
        for n, (a, b, full) in enumerate(ENG_LOCKERS):
            s.rule("shut%d%s" % (n, name), "decal", "svc_shutter", (side * ENG_W, (a + b) / 2, 1.36 if full else ENG_BAND[1] + 0.74), (0, side, 0), (0, 0, 1), (b - a, 1.48))
    front, rear = ENG_AXLES
    cos30, sin30 = math.cos(math.radians(30)), math.sin(math.radians(30))

    # ---- the cab: forward control, a raked two-piece screen, a white roof.
    def cab(y):
        w = ENG_W - corner(y, ENG_FRONT, 0.16, 1)
        sill = arch(y, front, ENG_ARCH, ENG_TYRE, 0.52)
        r = 0.20
        cx, cz = w - 0.075 - r, 2.80
        return [(w - 0.03, sill), (w, max(ENG_BAND[0], sill + 0.004)), (w, ENG_BAND[1]), (w, 1.72), (w - 0.06, 2.52), (w - 0.075, 2.80),
                (cx + r * cos30, cz + r * sin30), (cx + r * sin30, cz + r * cos30), (cx, 3.00), (0.5, 3.012), (0.0, 3.018)]

    def rake(point, y):
        if y <= ENG_RAKE:
            return point
        shift = curve(((1.72, 0.0), (2.52, 0.26), (2.80, 0.31), (2.93, 0.37), (3.00, 0.50), (3.02, 0.56)), point.z)
        return Vector((point.x, point.y - shift * (y - ENG_RAKE) / (ENG_FRONT - ENG_RAKE), point.z))
    cab_ys = sorted(set([round(y, 4) for y in (ENG_CAB, ENG_RAKE, ENG_FRONT - 0.08, ENG_FRONT - 0.03, ENG_FRONT)] + [y for w in CAB_WINDOWS for y in w] + arch_stations(front, ENG_ARCH)))
    hull = Hull(s, cab_ys, cab, rake)

    def cab_tag(y0, y1, k, side):
        if k == 3:
            if any(y0 >= a - 0.001 and y1 <= b + 0.001 for a, b in CAB_WINDOWS):
                return None
            return "black" if CAB_WINDOWS[0][1] - 0.001 <= y0 and y1 <= CAB_WINDOWS[-1][0] + 0.001 else "roof"
        return ("red", "band", "red")[k] if k < 3 else "roof"
    hull.skin(cab_tag, under="under")
    for a, b in CAB_WINDOWS:
        for side in (1, -1):
            hull.window(a, b, 3, side, ends=0.03)
    face = hull.wall(len(cab_ys) - 1, lambda k, m: (None if m in (1, 3) else "black" if m == 2 else "roof") if k == 3 else ("red", "band", "red")[k] if k < 3 else "roof",
                     (0, 1, 0), cuts={3: [0.035, ENG_W - 0.21]})
    for m in (1, 3):
        a, b, c, d = face[(3, m)]
        s.pane([a, b], [d, c], "rubber", "screen", (0, 1, 0.3), margin=0.03, depth=0.022)
    hull.wall(0, lambda k, m: "red" if k < 3 else "roof", (0, -1, 0))

    # ---- the pump house: a stainless panel either side between running board and coaming.
    pw = ENG_W - 0.03
    pump = Hull(s, list(ENG_PUMP), lambda y: [(pw - 0.02, 0.52), (pw, 0.66), (pw, 2.14), (pw, 2.54), (pw - 0.05, 2.62), (0.55, 2.62), (0.0, 2.62)])
    pump.skin(lambda y0, y1, k, side: ("alu", "panelR" if side > 0 else "panelL", "red", "alu", "alu", "alu")[k], under="under")

    # ---- the body: lockers behind roller shutters, the band across them, a coaming, the hose bed between the side boxes.
    def body(y):
        w = ENG_W - corner(y, ENG_BACK, 0.05, -1)
        sill = arch(y, rear, ENG_ARCH, ENG_TYRE, 0.52)
        return [(w - 0.02, sill), (w, max(0.62, sill + 0.004)), (w, ENG_BAND[0]), (w, ENG_BAND[1]), (w, 2.10), (w, 2.66), (w - 0.03, 2.72), (0.66, 2.72), (0.62, 2.60), (0.0, 2.60)]
    body_ys = sorted(set([round(y, 4) for y in (ENG_BACK, ENG_BACK + 0.05, ENG_PUMP[0])] + [y for a, b, _ in ENG_LOCKERS for y in (a, b)] + arch_stations(rear, ENG_ARCH)))
    box_body = Hull(s, body_ys, body)

    def body_tag(y0, y1, k, side):
        if k in (1, 3):
            for n, (a, b, full) in enumerate(ENG_LOCKERS):
                if y0 >= a - 0.001 and y1 <= b + 0.001 and (full or k == 3):
                    return "shut%d%s" % (n, "R" if side > 0 else "L")
        return ("alu", "red", "band", "red", "red", "alu", "red", "black", "hose")[k]
    box_body.skin(body_tag, under="under")
    box_body.wall(0, lambda k, m: ("alu", "chev", "chev", "chev", "red", "alu", "red", "red", "red")[k], (0, -1, 0))
    box_body.wall(len(body_ys) - 1, lambda k, m: "red", (0, 1, 0))
    s.add_to(p, sharp=40)

    # ---- wheels, arches, and something behind them.
    for side in (1, -1):
        road_wheel(p, side * (ENG_W - 0.02), front, ENG_TYRE, 0.30, "svc_wheel_alu", side, dish=0.035)
        road_wheel(p, side * (ENG_W - 0.02), rear, ENG_TYRE, 0.60, "svc_wheel_alu", side, twin=True, dish=0.15)
        arch_lip(p, side * (ENG_W - 0.004), front, ENG_ARCH + 0.015, ENG_TYRE, side, paint="rubber_black", thick=0.026)
        arch_lip(p, side * (ENG_W - 0.004), rear, ENG_ARCH + 0.015, ENG_TYRE, side, paint="rubber_black", thick=0.026)
    for y in ENG_AXLES:
        p.add(box(1.36, 0.90, 0.62), flat("rubber_black"), at=(0, y, 0.63))
        rod(p, (-0.90, y, ENG_TYRE), (0.90, y, ENG_TYRE), 0.07, "steel_dark", 6, caps=False)
    p.add(box(0.90, front - rear + 0.9, 0.26), flat("rubber_black"), at=(0, (front + rear) / 2, 0.52))                  # the chassis between the axles

    # ---- the cab's trim.
    for side in (1, -1):
        fore = (0, side, 0)
        sticker(p, "svc_fire_door", (side * ENG_W, front + 0.04, 1.502), (side, 0, 0), fore, (0.60, 0.42), lift=0.002)
        for y in (ENG_CAB + 0.06, front - 0.66, front - 0.62, ENG_FRONT - 0.08):                 # shut lines of the two doors
            low = max(0.56, ENG_TYRE + math.sqrt(max(0.0, (ENG_ARCH + 0.03) ** 2 - (y - front) ** 2)) + 0.02)
            p.add(box(0.004, 0.010, 1.72 - low), flat("rubber_black"), at=(side * (ENG_W + 0.001), y, (1.72 + low) / 2))
        for y in (ENG_CAB + 0.16, ENG_FRONT - 0.18):          # door handles, and a grab rail beside each door
            p.add(box(0.016, 0.13, 0.035), flat("chrome"), at=(side * (ENG_W + 0.006), y, 1.62))
        for y in (ENG_CAB + 0.03,):
            pipe(p, [(side * ENG_W, y, 1.80), (side * (ENG_W + 0.045), y, 1.80), (side * (ENG_W + 0.035), y, 2.50), (side * (ENG_W - 0.07), y, 2.50)], 0.014, "chrome", 5, bend=0.03)
        for y0, y1 in ((ENG_CAB + 0.04, front - ENG_ARCH - 0.04),):        # the step under the crew door (the front door is over the wheel)
            p.add(box(0.20, y1 - y0, 0.05), flat("aluminium"), at=(side * (ENG_W - 0.09), (y0 + y1) / 2, 0.50))
        # The mirror: a tall housing on two arms ahead of the door.
        ym = ENG_FRONT - 0.12
        for z in (1.92, 2.34):
            rod(p, (side * (ENG_W - 0.04), ym - 0.12, z), (side * (ENG_W + 0.075), ym, z), 0.012, "chrome", 5)
        p.add(box(0.05, 0.11, 0.50, bevel=0.016), flat("gloss_black"), at=(side * (ENG_W + 0.082), ym + 0.012, 2.13))
        # Lamps on the front: twin head lamps under the band, a warning lamp over it; markers on the sides.
        lamp(p, "svc_headlamp", (side * 0.82, ENG_FRONT, 0.99), (0, 1, 0), (-1, 0, 0), (0.40, 0.21), mirrored=side < 0, bezel="chrome")
        p.add(box(0.22, 0.03, 0.12, bevel=0.012), flat("lens_red"), at=(side * 0.84, ENG_FRONT + 0.012, 1.50))
        p.add(box(0.25, 0.012, 0.15), flat("chrome"), at=(side * 0.84, ENG_FRONT + 0.004, 1.50))
        p.add(box(0.012, 0.16, 0.07), flat("lens_amber"), at=(side * (ENG_W + 0.004), ENG_FRONT - 0.24, 1.20))
        p.add(box(0.012, 0.20, 0.09), flat("lens_red"), at=(side * (ENG_W - 0.07), ENG_CAB + 0.42, 2.66))
        # An air horn on the roof either side of the light bar.
        horn = lathe([(0.022, 0.0), (0.022, 0.30), (0.036, 0.42), (0.070, 0.50), (0.074, 0.505)], 8, cap_bottom=True)
        p.add(horn, flat("chrome"), at=(side * 0.97, ENG_BAR - 0.20, 3.075), rot=(-90, 0, 0), sharp=40)
        p.add(box(0.03, 0.05, 0.06), flat("chrome"), at=(side * 0.97, ENG_BAR - 0.06, 3.035))
    sticker(p, "svc_engine_grille", (0, ENG_FRONT, 1.215), (0, 1, 0), (-1, 0, 0), (0.94, 0.744), lift=0.008)
    p.add(box(0.98, 0.012, 0.78), flat("chrome"), at=(0, ENG_FRONT, 1.215))
    for x in (-0.70, -0.35, 0.0, 0.35, 0.70):              # marker lamps along the brow
        p.add(box(0.08, 0.03, 0.03), flat("lens_amber"), at=(x, ENG_FRONT - 0.305, 2.72))
    for x0, x1 in ((-0.92, -0.30), (0.12, 0.74)):          # wipers
        rod(p, (x0, ENG_FRONT + 0.004, 1.76), (x1, ENG_FRONT - 0.044, 1.92), 0.011, "rubber_black", 4)
    # The bumper: a chrome bar standing forward of the cab, tread plate on top, a siren, the plate.
    p.add(box(2 * ENG_W + 0.02, 0.19, 0.34, bevel=0.04), Paint("chrome"), at=(0, ENG_FRONT + 0.085, 0.69))
    p.add(box(2 * ENG_W - 0.08, 0.15, 0.10), flat("gloss_black"), at=(0, ENG_FRONT + 0.03, 0.47))
    tread(p, -(ENG_W - 0.05), ENG_W - 0.05, ENG_FRONT + 0.005, ENG_FRONT + 0.15, 0.8605, tile=0.20)
    p.add(lathe([(0.075, 0.0), (0.105, 0.03), (0.105, 0.16), (0.085, 0.20), (0.05, 0.215)], 10, cap_top=True, cap_bottom=True), flat("chrome"), at=(0.0, ENG_FRONT - 0.02, 0.965), rot=(-90, 0, 0), sharp=40, scale=(0.85, 0.85, 0.85))
    p.add(box(0.10, 0.12, 0.04), flat("chrome"), at=(0.0, ENG_FRONT + 0.07, 0.88))
    p.add(box(0.36, 0.008, 0.17), Paint("paper", grade=False).planar("svc_plate_bus", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.36, 0.17)), at=(0.40, ENG_FRONT + 0.1765, 0.69))
    # The roof: the light bar (the code adds none when the body is modelled), a hatch, the aerial.
    light_bar(p, at=(0.0, ENG_BAR, ENG_BAR_UP), length=1.50, depth=0.30, height=0.194)
    p.add(box(0.90, 0.66, 0.07, bevel=0.02), flat("svc_coach_white"), at=(0, ENG_CAB + 0.42, 3.04))
    p.add(box(0.66, 0.42, 0.02), flat("plastic_grey"), at=(0, ENG_CAB + 0.42, 3.078))
    rod(p, (-0.86, ENG_CAB + 0.14, 3.0), (-0.86, ENG_CAB + 0.04, 3.18), 0.006, "rubber_black", 4)

    # ---- the pump house's trim: running boards, a rail, crosslays, the deck gun, the low discharge outlets.
    for side in (1, -1):
        p.add(box(0.07, 1.26, 0.045), flat("aluminium"), at=(side * (ENG_W - 0.035), ENG_PUMP_MID, 0.50))
        tread(p, min(side * (ENG_W - 0.07), side * ENG_W), max(side * (ENG_W - 0.07), side * ENG_W), ENG_PUMP[0] + 0.02, ENG_PUMP[1] - 0.02, 0.5235, tile=0.21)
        pipe(p, [(side * pw, ENG_PUMP[1] - 0.025, 0.80), (side * (pw + 0.05), ENG_PUMP[1] - 0.025, 0.80), (side * (pw + 0.05), ENG_PUMP[1] - 0.025, 2.02), (side * pw, ENG_PUMP[1] - 0.025, 2.02)], 0.014, "chrome", 5, bend=0.03)
        # A capped outlet low on the body where the code lays the hose from.
        p.add(lathe([(0.075, 0.0), (0.075, 0.03), (0.062, 0.035), (0.062, 0.07), (0.07, 0.075), (0.07, 0.10), (0.045, 0.11)], 10, cap_top=True), flat("chrome"),
              at=(side * (ENG_W - 0.02), ENG_OUTLET, 0.76), rot=(0, 90 * side, 0), sharp=40)
        p.add(box(0.012, 0.26, 0.22), flat("aluminium"), at=(side * (ENG_W + 0.002), ENG_OUTLET, 0.76))
    for y in (ENG_PUMP[0] + 0.30, ENG_PUMP[0] + 0.82):     # two crosslays of flaked hose
        hose = Paint("nylon_black", grade=False).planar("svc_hose_bed", (0, 0, 1), (1, 0, 0), (0, y, 0), (2 * ENG_W - 0.28, 0.40))
        p.add(box(2 * ENG_W - 0.28, 0.40, 0.11), hose, at=(0, y, 2.665))
        p.add(box(2 * ENG_W - 0.22, 0.035, 0.15), flat("aluminium"), at=(0, y - 0.225, 2.69))
    p.add(box(2 * ENG_W - 0.22, 0.035, 0.15), flat("aluminium"), at=(0, ENG_PUMP[0] + 1.045, 2.69))
    p.add(lathe([(0.13, 0.0), (0.13, 0.03), (0.075, 0.06), (0.075, 0.16), (0.095, 0.17), (0.095, 0.21), (0.06, 0.22)], 10, cap_top=True), flat("svc_engine_red"), at=(0, ENG_PUMP[0] + 0.56, 2.72), sharp=40)
    gun = [(0, ENG_PUMP[0] + 0.56, 2.93), (0, ENG_PUMP[0] + 0.56, 3.02), (0, ENG_PUMP[0] + 0.64, 3.085), (0, ENG_PUMP[0] + 0.92, 3.115)]
    p.add(tube(gun, [0.05, 0.05, 0.048, 0.04], 8), flat("chrome"), sharp=60)
    aim = (Vector(gun[3]) - Vector(gun[2])).normalized()
    p.add(tube([tuple(Vector(gun[3]) - aim * 0.02), tuple(Vector(gun[3]) + aim * 0.20), tuple(Vector(gun[3]) + aim * 0.27)], [0.055, 0.048, 0.036], 8), flat("svc_engine_red"), sharp=60)
    rod(p, (-0.16, ENG_PUMP[0] + 0.58, 3.03), (0.16, ENG_PUMP[0] + 0.58, 3.03), 0.012, "gloss_black", 5)

    # ---- the body's trim.
    for side in (1, -1):
        fore = (0, side, 0)
        sticker(p, "svc_fire_title", (side * ENG_W, ENG_BODY_MID, 2.385), (side, 0, 0), fore, (2.56, 0.32), lift=0.002)
        for z in (2.10, 2.655):                            # the rails over the shutters and under the coaming
            p.add(box(0.014, ENG_PUMP[0] - ENG_BACK + 0.01, 0.03), flat("aluminium"), at=(side * (ENG_W + 0.004), ENG_BODY_MID, z))
        p.add(box(0.012, 0.12, 0.05), flat("lens_red"), at=(side * (ENG_W + 0.004), ENG_BACK + 0.25, 2.52))
        p.add(box(0.012, 0.12, 0.05), flat("lens_amber"), at=(side * (ENG_W + 0.004), ENG_PUMP[0] - 0.30, 2.52))
        lamp(p, "svc_tail_lamp", (side * (ENG_W - 0.17), ENG_BACK, 1.04), (0, -1, 0), (1, 0, 0), (0.15, 0.42), depth=0.03)
        p.add(box(0.20, 0.03, 0.11, bevel=0.012), flat("lens_red"), at=(side * (ENG_W - 0.27), ENG_BACK - 0.012, 2.38))
        pipe(p, [(side * (ENG_W - 0.11), ENG_BACK, 1.50), (side * (ENG_W - 0.11), ENG_BACK - 0.07, 1.50), (side * (ENG_W - 0.11), ENG_BACK - 0.07, 2.30), (side * (ENG_W - 0.11), ENG_BACK, 2.30)], 0.016, "chrome", 5, bend=0.03)
    p.add(box(0.70, 0.03, 0.11, bevel=0.012), flat("lens_amber"), at=(0, ENG_BACK - 0.012, 2.38))
    # The step across the back, and the plate.
    p.add(box(2 * ENG_W - 0.02, 0.17, 0.07, bevel=0.012), flat("aluminium"), at=(0, ENG_BACK - 0.065, 0.495))
    tread(p, -(ENG_W - 0.02), ENG_W - 0.02, ENG_BACK - 0.145, ENG_BACK - 0.005, 0.5305, tile=0.20)
    p.add(box(0.36, 0.008, 0.17), Paint("paper", grade=False).planar("svc_plate_bus", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.36, 0.17)), at=(0, ENG_BACK - 0.005, 0.77))
    # On the side boxes: a ladder on brackets (right), two suction hoses (left).
    lx, ly0, ly1, lz = ENG_W - 0.28, ENG_BACK + 0.25, ENG_PUMP[0] - 0.30, 2.80
    for x in (lx - 0.19, lx + 0.19):
        p.add(box(0.028, ly1 - ly0, 0.07), flat("aluminium"), at=(x, (ly0 + ly1) / 2, lz))
    for k in range(10):
        y = ly0 + 0.15 + k * (ly1 - ly0 - 0.30) / 9
        rod(p, (lx - 0.19, y, lz), (lx + 0.19, y, lz), 0.014, "aluminium", 5, caps=False)
    for y in (ly0 + 0.40, (ly0 + ly1) / 2, ly1 - 0.40):
        p.add(box(0.46, 0.05, 0.045), flat("steel_dark"), at=(lx, y, 2.7425))
    for x in (-(ENG_W - 0.39), -(ENG_W - 0.18)):
        p.add(tube([(x, ly0 + 0.15, 2.80), (x, ly1 - 0.15, 2.80)], 0.075, 8), flat("rubber_black"), sharp=60)
        for y in (ly0 + 0.15, ly1 - 0.15):
            p.add(tube([(x, y - 0.07, 2.80), (x, y + 0.07, 2.80)], 0.085, 8), flat("chrome"), sharp=60)
    for y in (ly0 + 0.70, ly1 - 0.70):
        p.add(box(0.44, 0.04, 0.012), flat("steel_dark"), at=(-(ENG_W - 0.285), y, 2.88))
    return p


# ---------------------------------------------------------------------------------------------- the light bar

def fire_light_bar():
    p = Prop("fire_light_bar", "glow", pivot="centre of its underside; it lies along X",
             note="1.30 x 0.28 m light bar, 0.17 m high: two feet, a dark base rail, red lens modules either end and a clear one in the middle")
    light_bar(p)
    return p


def fire_bar_lens():
    """One red end of the fire engine's light bar again, a hair bigger and in the glow material: EngineLivery shows one
    over each end of the modelled bar in turn (the engine itself is one mesh in the lit material, so nothing of it can
    light up by itself). The other end is this one turned half round about Y."""
    p = Prop("fire_bar_lens", "glow", pivot="the middle of the underside of the fire engine's light bar (fire_engine's (0, 3.006, 2.535) in Unity); the lens is at +X",
             note="the +X red lens module of the fire engine's 1.50 m light bar, 3 mm proud of it all round, for flashing; turn it 180 degrees about Y for the other end")
    length, depth, height, grow = 1.50, 0.30, 0.194, 0.003
    k = length / 1.30
    z0, z1 = 0.030 + 0.034 + 0.001, height - 0.003 + grow
    half, d = length / 2, depth / 2 + grow
    lens = (Paint("lens_red", grade=False)
            .planar("svc_light_bar", (0, 1, 0), (-1, 0, 0), (0, 0, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55)
            .planar("svc_light_bar", (0, -1, 0), (1, 0, 0), (0, 0, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55))
    rings = []
    for x, wide, tall in ((length * 0.095 + 0.010, 1.0, 1.0), (half - 0.10 * k, 1.0, 1.0), (half - 0.035 * k + grow, 0.86, 0.90), (half + grow, 0.55, 0.60)):
        top = z0 + (z1 - z0) * tall
        rings.append([(x, y * wide * 0.965, z) for y, z in ((-d, z0), (d, z0), (d * 0.94, z0 + (top - z0) * 0.62), (d * 0.62, top), (-d * 0.62, top), (-d * 0.94, z0 + (top - z0) * 0.62))])
    p.add(loft(rings, cap_start=True, cap_end=True), lens, sharp=28)
    return p


# ---------------------------------------------------------------------------------------------- the list

PROPS = [(minibus, 5500), (fire_engine, 7000), (fire_light_bar, 350), (fire_bar_lens, 60)]

# Each beside the vehicle it stands over (to the left, as measured on the game's prefab), a person at the door and one in front.
SIDES = {
    "minibus": {"references": (((2.33, 6.66, 2.27), (-3.4, 0.0, 1.425)),), "people": (((1.35, 0.76), 90.0), ((0.3, 4.3), 180.0)),
                "views": ((0, 8), (90, 8), (180, 8), (90, 52), (38, 20), (142, 28))},
    "fire_engine": {"references": (((2.72, 7.43, 3.01), (-4.0, 0.0, 1.505)),), "people": (((1.72, 0.756), 90.0), ((0.4, 5.0), 180.0)),
                    "views": ((0, 8), (90, 8), (180, 8), (90, 52), (38, 20), (142, 28))},
}
