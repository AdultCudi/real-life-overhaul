"""THE SERVICE VEHICLES: the mod's own city bus and its parts, the fire engine's body that the mod stands over one of
the game's own traffic vehicles, whose renderers are hidden meanwhile (so the body has its own wheels), and the light
bar. Same rules as props.py: metres, X right, Y forward, Z up, one mesh on the shared atlas; Unity gets (x, z, y).

    city_bus             the body of the mod's own bus (Transport/BusVehicle), on the ground under its middle
    city_bus_wheel       a front wheel on its own middle, and the rear pair (city_bus_wheel_twin): BusVehicle spins and steers them
    city_bus_door        one leaf of its doors: BusVehicle puts two in each door and slides them open
    city_bus_light_*     its lamps lit, a mesh for each of BusVehicle's groups (head, tail, brake, left, right), at the bus's origin
    fire_engine          over the delivery truck (Events/EventRoad.EngineLivery): its axles on the truck's
    fire_light_bar       on the roof of a truck that has no modelled body (EventPropsBar)
    fire_bar_lens        one end of the fire engine's own light bar, lit: shown in turn over each end while it flashes

What the code relies on, and what was measured on the game's vehicles (their prefabs; README "Service vehicles"):

    Delivery Truck    mesh 2.72 x 3.01 x 7.43 m, ORIGIN UNDER ITS REAR AXLE (the body runs from z -2.353 to +5.077);
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
from prop_kit import Paint, Prop, box, lathe, loft, moved, region, rounded_rect, tube


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


# ---------------------------------------------------------------------------------------------- the city bus

# The mod's own bus (Transport/BusVehicle), which stands over no vehicle of the game's: its measurements are BusVehicle's
# and BusBay's, and BusVehicle puts the wheels, the door leaves and the lamps on the body where they are below. A
# low-floor midi bus, 10.90 m long over its sign boxes and 2.40 m wide (only the mirrors stand out beyond that), 3.05 m
# to the top of the air-conditioning pod, its axles 2.90 m either side of the middle, its two doors on the kerb side (+x).
BUS_HALF = 1.20               # BusVehicle.HalfWidth
BUS_END = 5.45                # half of BusVehicle.Length: the sign boxes reach it, the bumpers 5 mm less
BUS_NOSE, BUS_TAIL = 5.42, -5.42          # the front and back walls
BUS_CORNERS = (0.25, 0.20)    # the corners' radius in plan, at the front and at the back
BUS_AXLES = (2.90, -2.90)     # BusBay.BusAxleFront, and BusWheelbase behind it
BUS_TYRE, BUS_ARCH = 0.48, 0.58           # BusVehicle.WheelRadius; the wheel arches' radius, over the axle
BUS_TRACK = (1.02, 0.92)      # to the middle of a front wheel and of a rear pair (BusVehicle.TrackFront, TrackRear)
BUS_TREAD = (0.30, 0.56)      # the front tyre's width and the rear pair's (TyreFront, TyreRear)
BUS_FLOOR = 0.33              # BusVehicle.Floor: the threshold of both doors
BUS_DOORS = (4.45, -0.55)     # the middles of the front door and of the middle door (FrontDoorAhead, MiddleDoorAhead)
BUS_LEAF = (0.60, 2.30, 0.04)             # each door is two leaves of this (DoorLeaf, DoorTall, and the plain leaf's thickness), 0.03 off the side when shut
# Up the side: the sill, the top of the grey skirt, the waist (the windows' foot), the window band's top, the cantrail,
# and the middle of the roof.
BUS_SILL, BUS_SKIRT, BUS_WAIST, BUS_BAND, BUS_RAIL, BUS_ROOF = 0.22, 0.60, 1.12, 2.70, 2.76, 2.865
BUS_POD = (-4.90, -2.70, 3.05)            # the air-conditioning pod: from, to, and its top (BusVehicle.Height)
# The windows, front to back. On the right the first has the side sign over it; on the left the last is the engine's grille.
BUS_RIGHT = ((2.62, 3.82), (1.38, 2.56), (0.10, 1.32), (-2.42, -1.20), (-3.70, -2.48), (-5.00, -3.76))
BUS_LEFT = ((3.88, 5.11), (2.62, 3.82), (1.38, 2.56), (0.10, 1.32), (-1.14, 0.04), (-2.42, -1.20), (-3.70, -2.48), (-5.00, -3.76))


def uv_disc(u, v, ru, rv, count=10):
    return [(u + ru * math.cos(2 * math.pi * k / count), v + rv * math.sin(2 * math.pi * k / count)) for k in range(count)]


def uv_rect(u0, v0, u1, v1):
    return [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]


# What lights up in each lamp, in its decal's own u, v (0..1, v up), as paint_service.py paints them: the two round lamps
# and the indicator of svc_headlamp (112 x 64 px), the indicator, brake and tail lenses of svc_bus_tail_lamp (40 x 160)
# from the top, the lens of svc_bus_repeater and of svc_bus_brake_high.
HEAD_LAMPS = (uv_disc(58 / 112, 0.5, 19 / 112, 19 / 64), uv_disc(90 / 112, 0.5, 14 / 112, 14 / 64))
HEAD_TURN = uv_rect(8 / 112, 8 / 64, 34 / 112, 56 / 64)
BUS_TAIL_LENSES = (uv_rect(5 / 40, 1 - 37 / 160, 35 / 40, 1 - 5 / 160), uv_rect(5 / 40, 1 - 81 / 160, 35 / 40, 1 - 41 / 160), uv_rect(5 / 40, 1 - 125 / 160, 35 / 40, 1 - 85 / 160))


def bus_lamps():
    """Every lamp unit on the city bus, as lamp() makes it: (decal, centre, normal, right, size, depth, mirrored, lit),
    where lit says what in it the code switches, (group, outline): the groups are BusVehicle's lamps, city_bus_light_<group>."""
    units = []
    for side in (1, -1):
        turn = "right" if side > 0 else "left"
        # Low at the front corners, the indicator outside the headlamp; at each back corner indicator, brake, tail and
        # reversing lamp; a repeater on each side between the front wheel and the front door.
        units.append(("svc_headlamp", (side * 0.66, BUS_NOSE, 0.74), (0, 1, 0), (-1, 0, 0), (0.385, 0.22), 0.024, side < 0,
                      (("head", HEAD_LAMPS[0]), ("head", HEAD_LAMPS[1]), (turn, HEAD_TURN))))
        units.append(("svc_bus_tail_lamp", (side * 0.90, BUS_TAIL, 1.09), (0, -1, 0), (1, 0, 0), (0.16, 0.64), 0.024, False,
                      ((turn, BUS_TAIL_LENSES[0]), ("brake", BUS_TAIL_LENSES[1]), ("tail", BUS_TAIL_LENSES[2]))))
        units.append(("svc_bus_repeater", (side * (BUS_HALF - 0.002), 3.70, 0.80), (side, 0, 0), (0, side, 0), (0.12, 0.05), 0.004, False,
                      ((turn, uv_rect(2 / 32, 2 / 14, 30 / 32, 12 / 14)),)))
    # The high brake lamp: over the number at the back (its lettering is 2.52 to 2.72 m up), under the roof's edge.
    units.append(("svc_bus_brake_high", (0.0, BUS_TAIL, 2.81), (0, -1, 0), (1, 0, 0), (0.40, 0.05), 0.012, False, (("brake", uv_rect(2 / 96, 2 / 12, 94 / 96, 10 / 12)),)))
    return units


def lamp_point(unit, u, v, lift=0.0):
    """A point of a lamp unit's lens (u, v as on its decal), `lift` in front of it: the decal's own mapping (Paint.planar),
    which a mirrored unit turns half round."""
    _, centre, normal, right, size, depth, mirrored, _ = unit
    n, r = Vector(normal).normalized(), Vector(right).normalized()
    r = -r if mirrored else r
    up = n.cross(r)
    return Vector(centre) + n * (depth + lift) + r * ((u - 0.5) * size[0] * 0.94) + up * ((v - 0.5) * size[1] * 0.94)


def plate(points, normal):
    """One flat face through the points, looking along `normal`."""
    bm = bmesh.new()
    face = bm.faces.new([bm.verts.new(p) for p in points])
    bm.normal_update()
    if face.normal.dot(Vector(normal)) < 0:
        bmesh.ops.reverse_faces(bm, faces=[face])
    return bm


def bus_bumper(p, end, radius, z0, z1):
    """Across an end wall and a third of the way round each corner, 0.05 thick on the body's own outline: its face is
    5 mm short of the end (BUS_END), and its ends stay inside the bus's width."""
    sign = 1 if end > 0 else -1
    cx, cy = BUS_HALF - radius, end - sign * radius
    path = []
    for x_sign in (-1, 1):
        arc = [(x_sign * (cx + radius * math.cos(math.radians(a))), cy + sign * radius * math.sin(math.radians(a)), (z0 + z1) / 2) for a in (30, 50, 70, 90)]
        path += arc if x_sign < 0 else arc[::-1]

    def away(q):
        if abs(q.x) <= cx + 1e-6:
            return (0.0, sign, 0.0)
        return (q.x - math.copysign(cx, q.x), q.y - cy, 0.0)
    p.add(ribbon(path, away, z1 - z0, 0.05), Paint("svc_bus_grey"), sharp=50)


def city_bus():
    p = Prop("city_bus", "lit", pivot="ground centre under the middle of the bus; nose to +Y",
             note="the mod's own city bus (Transport/BusVehicle): axles at y +-2.90, doors on +X centred y 4.45 and -0.55 with their threshold 0.33 m up; "
                  "the wheels, the door leaves and the lamps are props of their own (city_bus_wheel, city_bus_wheel_twin, city_bus_door, city_bus_light_*)")
    s = Shell(low=0.20, high=1.70, length=11.0)
    # The red is the clean part of its swatch all over, as the lettering's ground is: only the skirt gets the road's dirt.
    s.rule("red", "flat", "svc_bus_red").rule("roof", "flat", "svc_bus_red").rule("skirt", "swatch", "svc_bus_grey")
    s.rule("black", "flat", "gloss_black").rule("rubber", "flat", "rubber_black").rule("under", "flat", "rubber_black")
    s.rule("glass", "own", "svc_glass").rule("screen", "own", "svc_screen").rule("grille", "own", "svc_bus_grille")
    s.rule("inside", "flat", "svc_bus_inside").rule("floor", "flat", "plastic_grey").rule("step", "flat", "paint_yellow")
    front_axle, rear_axle = BUS_AXLES
    nose, tail = BUS_CORNERS
    doors = [(round(m - BUS_LEAF[0] + 0.01, 4), round(m + BUS_LEAF[0] - 0.01, 4)) for m in BUS_DOORS]      # 1 cm inside the leaves all round
    lintel = BUS_FLOOR + BUS_LEAF[1]      # the doors' upper edge, 2.63

    # ---- the body: sill, grey skirt, red panels to the waist, the window band, cantrail, round roof edge, roof.
    def section(y):
        front = smooth01((y - (BUS_NOSE - nose)) / nose)          # 1 at the front wall, 0 behind its corners
        back = smooth01(((BUS_TAIL + tail) - y) / tail)
        w = BUS_HALF - corner(y, BUS_NOSE, nose, 1) - corner(y, BUS_TAIL, tail, -1)
        sill = max(arch(y, front_axle, BUS_ARCH, BUS_TYRE, BUS_SILL), arch(y, rear_axle, BUS_ARCH, BUS_TYRE, BUS_SILL))
        waist = BUS_WAIST - 0.16 * front + 0.36 * back      # the windscreen goes lower than the side windows, the back window not as low
        band = BUS_BAND - 0.10 * front - 0.32 * back
        brow = 0.09 * smooth01((y - 4.60) / 0.80)             # the roof rises over the front to take the sign box
        return [(w - 0.035, sill), (w - 0.004, max(BUS_FLOOR, sill + 0.004)), (w, max(BUS_SKIRT, sill + 0.008)), (w, max(waist, sill + 0.012)),
                (w, band), (w, BUS_RAIL + brow), (w - 0.035, BUS_RAIL + 0.075 + brow), (w - 0.10, BUS_RAIL + 0.098 + brow), (0.0, BUS_ROOF + brow)]
    ys = [BUS_NOSE - nose * (1 - math.sin(math.radians(a))) for a in (0, 22.5, 45, 67.5, 90)]
    ys += [BUS_TAIL + tail * (1 - math.sin(math.radians(a))) for a in (0, 22.5, 45, 67.5, 90)]
    ys += arch_stations(front_axle, BUS_ARCH, 8) + arch_stations(rear_axle, BUS_ARCH, 8) + [y for d in doors for y in d] + [y for w in BUS_RIGHT + BUS_LEFT for y in w] + [4.60, 4.80]
    stations = sorted(set(round(y, 4) for y in ys))
    body = Hull(s, stations, section)

    def within(y0, y1, span):
        return y0 >= span[0] - 0.001 and y1 <= span[1] + 0.001

    def body_tag(y0, y1, k, side):
        if side > 0 and 1 <= k <= 3 and any(within(y0, y1, d) for d in doors):
            return None
        if k == 3:
            if any(within(y0, y1, w) for w in (BUS_RIGHT if side > 0 else BUS_LEFT)) or y0 >= BUS_NOSE - nose - 0.001:
                return None                                # windows, the grille, the windscreen's corners
            return "red" if y1 <= BUS_LEFT[-1][0] + 0.001 else "black"          # the back corners; the pillars
        return ("skirt", "skirt", "red", None, "red", "red", "red", "roof")[k]
    body.skin(body_tag, under="under")
    for a, b in BUS_RIGHT[1:]:
        body.window(a, b, 3, 1)
    for a, b in BUS_LEFT[:-1]:
        body.window(a, b, 3, -1)
    for side in (1, -1):
        body.window(BUS_NOSE - nose, BUS_NOSE, 3, side, ends=0.03)          # the windscreen round the corners
    # The first window on the right: the side sign's panel over it, flush with the side. BusVehicle.Signs letters there
    # 12 mm out, in a box 1.10 x 0.20 centred y 3.25, 2.55 m up.
    i0, i1 = body.at(BUS_RIGHT[0][0]), body.at(BUS_RIGHT[0][1])
    head = [body.P(i, 4) for i in range(i0, i1 + 1)]
    split = [Vector((q.x, q.y, 2.40)) for q in head]
    s.pane([body.P(i, 3) for i in range(i0, i1 + 1)], split, "rubber", "glass", (1, 0, 0))
    for j in range(len(head) - 1):
        s.face([split[j], split[j + 1], head[j + 1], head[j]], "black", out=(1, 0, 0))
    # The engine's grille at the left back corner, where its radiator is.
    i0, i1 = body.at(BUS_LEFT[-1][0]), body.at(BUS_LEFT[-1][1])
    s.pane([body.P(i, 3, -1) for i in range(i0, i1 + 1)], [body.P(i, 4, -1) for i in range(i0, i1 + 1)], "black", "grille", (-1, 0, 0), margin=0.03, depth=0.03)
    # The front: the windscreen, the sign box over it (its wall black), the panel under it. The code letters the sign
    # 15 mm in front of the box's face, in a box 1.62 x 0.24 centred 2.78 m up; at the back 0.45 x 0.20, 2.62 m up.
    sign_half = 0.86
    face = body.wall(len(stations) - 1, lambda k, m: None if k == 3 else ("skirt", "skirt", "red", None, "black" if m == 1 else "red", "black" if m == 1 else "red", "red", "roof")[k],
                     (0, 1, 0), cuts={4: [sign_half], 5: [sign_half]})
    a, b, c, d = face[(3, 0)]
    s.pane([a, b], [d, c], "rubber", "screen", (0, 1, 0), margin=0.035, depth=0.02)
    p.add(box(2 * sign_half, BUS_END - BUS_NOSE, 0.30), flat("gloss_black"), at=(0, (BUS_END + BUS_NOSE) / 2, 2.77))
    # The back: the engine's grille low down with red round it, the back window over it, the number's box over that.
    face = body.wall(0, lambda k, m: None if k in (2, 3) and m == 1 else ("skirt", "skirt", "red", "red", "red", "red", "red", "roof")[k], (0, -1, 0), cuts={2: [0.72], 3: [0.70]})
    up = lambda q, r, z: q + (r - q) * ((z - q.z) / (r.z - q.z))
    for k, (low, high), glass, frame in ((2, (0.70, 1.30), "grille", "black"), (3, (1.58, 2.30), "glass", "rubber")):
        a, b, c, d = face[(k, 1)]                          # red below and above it, inside the piece the wall left open
        a1, b1, a2, b2 = up(a, d, low), up(b, c, low), up(a, d, high), up(b, c, high)
        s.face([a, b, b1, a1], "red", out=(0, -1, 0))
        s.face([a2, b2, c, d], "red", out=(0, -1, 0))
        s.pane([a1, b1], [a2, b2], frame, glass, (0, -1, 0), margin=0.03, depth=0.03 if glass == "grille" else 0.02)
    p.add(box(0.54, BUS_END - BUS_NOSE, 0.30), flat("gloss_black"), at=(0, -(BUS_END + BUS_NOSE) / 2, 2.62))

    # ---- the doors: open in the side down to the floor, a dark inside behind them; the code's leaves close them.
    for (y0, y1), middle in zip(doors, BUS_DOORS):
        i0, i1 = body.at(y0), body.at(y1)
        back = -0.40 if middle > 0 else -(BUS_HALF - 0.06)          # the platform beside the driver; across the bus at the middle door
        edge = [body.P(i, 1) for i in range(i0, i1 + 1)]          # the threshold, at the floor
        top = [Vector((body.P(i, 4).x, body.ys[i], lintel)) for i in range(i0, i1 + 1)]
        for j in range(i1 - i0):
            s.face([top[j], top[j + 1], body.P(i0 + j + 1, 4), body.P(i0 + j, 4)], "black", out=(1, 0, 0))
        s.face(edge + [(1.12, y1, BUS_FLOOR), (1.12, y0, BUS_FLOOR)], "step", out=(0, 0, 1))
        s.face([(1.12, y0, BUS_FLOOR), (1.12, y1, BUS_FLOOR), (back, y1, BUS_FLOOR), (back, y0, BUS_FLOOR)], "floor", out=(0, 0, 1))
        s.face(top + [(back, y1, lintel), (back, y0, lintel)], "inside", out=(0, 0, -1))
        for i, y, out in ((i0, y0, (0, 1, 0)), (i1, y1, (0, -1, 0))):
            s.face([body.P(i, 1), body.P(i, 2), body.P(i, 3), Vector((body.P(i, 4).x, y, lintel)), (back, y, lintel), (back, y, BUS_FLOOR)], "inside", out=out)
        s.face([(back, y0, BUS_FLOOR), (back, y1, BUS_FLOOR), (back, y1, lintel), (back, y0, lintel)], "inside", out=(1, 0, 0))
    s.add_to(p, sharp=40)
    for (y0, y1), middle in zip(doors, BUS_DOORS):
        for y in (y0 + 0.08, y1 - 0.08):                   # the yellow poles either side of the way in, a rail across under the ceiling
            rod(p, (1.08, y, BUS_FLOOR), (1.08, y, lintel), 0.018, "paint_yellow", 6, caps=False)
        rod(p, (0.55, y0, lintel - 0.12), (0.55, y1, lintel - 0.12), 0.016, "paint_yellow", 6, caps=False)
    p.add(box(0.12, 0.10, 0.30, bevel=0.012), flat("steel_dark"), at=(0.42, doors[0][1] - 0.10, 1.25))          # the ticket validator by the driver
    p.add(box(0.004, 0.08, 0.10), flat("svc_lens_clear"), at=(0.482, doors[0][1] - 0.10, 1.31))
    rod(p, (0.42, doors[0][1] - 0.10, BUS_FLOOR), (0.42, doors[0][1] - 0.10, 1.10), 0.02, "paint_yellow", 6, caps=False)
    for x in (-0.92, -0.48):                               # across from the middle door, two seats facing forward, hung on the wall
        p.add(box(0.42, 0.42, 0.10, bevel=0.02), flat("wool_blue"), at=(x, BUS_DOORS[1] + 0.06, 0.50))
        p.add(box(0.42, 0.08, 0.58, bevel=0.02), flat("wool_blue"), at=(x, BUS_DOORS[1] - 0.17, 0.84), rot=(-8, 0, 0))

    # ---- under the floor: the wheel boxes, so that one does not see through the arches (the front wheels steer 38 degrees);
    # a rubber lip round each arch, inside the bus's width.
    for y, half in ((front_axle, 0.55), (rear_axle, 0.60)):
        p.add(box(2 * half, 2 * BUS_ARCH + 0.06, 0.93), flat("rubber_black"), at=(0, y, 0.575))
        for side in (1, -1):
            arch_lip(p, side * (BUS_HALF - 0.016), y, BUS_ARCH + 0.01, BUS_TYRE, side, paint="rubber_black", thick=0.016)

    # ---- the ends: bumpers, plates, the lamp units, wipers, mirrors.
    bus_bumper(p, BUS_NOSE, nose, 0.22, 0.48)
    bus_bumper(p, BUS_TAIL, tail, 0.22, 0.46)
    for y, normal, right in ((BUS_END - 0.0045, (0, 1, 0), (-1, 0, 0)), (-(BUS_END - 0.0045), (0, -1, 0), (1, 0, 0))):
        p.add(box(0.36, 0.006, 0.17), Paint("paper", grade=False).planar("svc_plate_bus", normal, right, (0, 0, 0), (0.36, 0.17)), at=(0, y, 0.35))
    for unit in bus_lamps():
        decal, centre, normal, right, size, depth, mirrored, _ = unit
        lamp(p, decal, centre, normal, right, size, depth=depth, mirrored=mirrored)
    p.add(box(1.70, 0.008, 0.17, bevel=0.003), flat("gloss_black"), at=(0, BUS_NOSE, 0.74))         # the black band the headlamps stand in
    glass_y = BUS_NOSE - 0.02 + 0.012
    for x0, x1 in ((-0.10, -0.84), (0.80, 0.06)):          # the wipers, parked along the foot of the windscreen
        rod(p, (x0, glass_y, 1.035), (x1, glass_y, 1.075), 0.010, "rubber_black", 4)
        rod(p, (x0 + (x1 - x0) * 0.08, glass_y + 0.004, 1.02), (x0 + (x1 - x0) * 0.55, glass_y + 0.004, 1.05), 0.012, "gloss_black", 4)
    for side in (1, -1):
        # On an arm from the top of the front corner, out over the front door's leaves (open, they stand 1.32 m out) and down.
        pipe(p, [(side * 1.08, 5.31, 2.87), (side * 1.365, 5.36, 2.845), (side * 1.38, 5.37, 2.50)], 0.016, "gloss_black", 6, bend=0.06)
        p.add(box(0.07, 0.15, 0.40, bevel=0.02), flat("gloss_black"), at=(side * 1.375, 5.37, 2.33))
        p.add(box(0.05, 0.004, 0.35), flat("chrome"), at=(side * 1.375, 5.37 - 0.077, 2.33))

    # ---- lettering: the city's name, the fleet number on both sides, the front and the back; the wheelchair's door.
    for side in (1, -1):
        fore = (0, side, 0)
        sticker(p, "svc_bus_title", (side * BUS_HALF, 1.32 if side > 0 else 0.0, 0.86), (side, 0, 0), fore, (1.70, 0.19), lift=0.002)
        sticker(p, "svc_bus_number", (side * BUS_HALF, -4.40, 0.86), (side, 0, 0), fore, (0.26, 0.13), lift=0.002)
    sticker(p, "svc_bus_number", (-0.30, BUS_NOSE, 0.89), (0, 1, 0), (-1, 0, 0), (0.20, 0.10), lift=0.002)
    sticker(p, "svc_bus_number", (-0.62, BUS_TAIL, 2.62), (0, -1, 0), (1, 0, 0), (0.26, 0.13), lift=0.002)
    sticker(p, "svc_bus_wheelchair", (BUS_HALF, 0.24, 0.86), (1, 0, 0), (0, 1, 0), (0.16, 0.16), lift=0.002)

    # ---- the roof: two escape hatches, the air-conditioning pod at the back with its fans flush on top, an aerial.
    for y in (1.10, -1.30):
        p.add(box(0.76, 0.70, 0.035, bevel=0.012), flat("aluminium"), at=(0, y, BUS_ROOF + 0.01))
    a, b, height = BUS_POD
    mid, long = (a + b) / 2, b - a
    pod_top = height - 0.004                               # the fans come to the top and no higher: BusVehicle.Height
    pod = [[(x * k, mid + y * k2, z) for x, y in rounded_rect(1.64, long, 0.20, 2)] for k, k2, z in ((1.0, 1.0, BUS_ROOF - 0.01), (1.0, 1.0, pod_top - 0.035), (0.965, 0.985, pod_top))]
    vents = Paint("svc_coach_white", grade=False)
    for normal, right, centre, wide in (((1, 0, 0), (0, 1, 0), (0.82, mid, 2.955), long * 0.80), ((-1, 0, 0), (0, -1, 0), (-0.82, mid, 2.955), long * 0.80),
                                        ((0, -1, 0), (1, 0, 0), (0, a, 2.955), 1.20), ((0, 1, 0), (-1, 0, 0), (0, b, 2.955), 1.20)):
        vents.planar("svc_vent", normal, right, centre, (wide, 0.11), facing=0.9)
    p.add(loft(pod, cap_end=True), vents, sharp=35)
    for y in (mid - 0.52, mid + 0.52):
        p.add(lathe([(0.25, -0.01), (0.25, 0.0015)], 12, cap_top=True), flat("plastic_grey"), at=(0, y, pod_top), sharp=30)
        p.add(lathe([(0.07, 0.0), (0.07, height - pod_top)], 8, cap_top=True), flat("aluminium"), at=(0, y, pod_top), sharp=30)
        for turn in range(3):
            p.add(box(0.012, 0.44, 0.002), flat("gloss_black"), at=(0, y, pod_top + 0.0012), rot=(0, 0, 60 * turn))
    rod(p, (-0.40, 3.60, BUS_ROOF), (-0.40, 3.54, BUS_ROOF + 0.16), 0.006, "rubber_black", 4)
    return p


def bus_wheel(name, wide, dish, note):
    """A wheel of the city bus standing on its own middle, its axle along X, its outer face (the hub, the nuts) to +X:
    BusVehicle spins it about X and steers the front ones about Y. The tyre and the steel wheel are one lathe, painted
    with the wheel's face; ten nuts stand on the hub so that it is seen to turn. `dish`: how far in the hub is."""
    def make():
        p = Prop(name, "lit", pivot="the middle of the wheel; its axle along X, the hub to +X", note=note)
        r, w = BUS_TYRE, wide / 2
        profile = [(0.29, -w + 0.007), (0.43, -w), (r, -w + 0.05)]
        if wide > 0.4:                                     # a pair: the groove between the two tyres
            profile += [(r, -0.03), (r * 0.88, 0.0), (r, 0.03)]
        hub = w - dish
        profile += [(r, w - 0.05), (0.43, w), (0.29, w - 0.007), (0.275, w - 0.03), (0.21, hub), (0.11, hub), (0.075, hub + 0.04)]
        paint = Paint("rubber_black", grade=False).planar("svc_wheel", (0, 0, 1), (1, 0, 0), (0, 0, 0), (2 * r, 2 * r), facing=0.45)
        p.add(lathe(profile, 16, cap_top=True, cap_bottom=True), paint, rot=(0, 90, 0), sharp=38)
        for k in range(10):                                # on the nuts svc_wheel paints
            t = 2 * math.pi * k / 10
            nut = moved(lathe([(0.017, 0.0), (0.0, 0.016)], 3), at=(0.168 * math.cos(t), 0.168 * math.sin(t), hub - 0.002), rot=(0, 0, math.degrees(t)))
            p.add(nut, flat("chrome"), rot=(0, 90, 0), sharp=30)
        return p
    return make


def city_bus_door():
    p = Prop("city_bus_door", "lit", pivot="the middle of its lower edge; it stands along Y, its outside to +X",
             note="one leaf of the city bus's doors, 0.60 m wide and 2.30 m high: glass in a thin red frame with a rail across; BusVehicle puts two in each door, 0.03 off the side")
    wide, high, thick = BUS_LEAF
    s = Shell(low=0.0, high=high, length=wide)
    s.rule("frame", "flat", "svc_bus_red").rule("rubber", "flat", "rubber_black").rule("glass", "own", "svc_glass")
    x, stile, rails = thick / 2, 0.055, (0.0, 0.10, 0.98, 1.06, 2.22, high)

    def at(u, v, out=x):
        return (out, -wide / 2 + u, v)
    # The stiles, each with the rails' ends along its inner edge (so that no edge ends in the middle of another).
    s.face([at(0.0, 0.0), at(stile, 0.0)] + [at(stile, v) for v in rails[1:-1]] + [at(stile, high), at(0.0, high)], "frame", out=(1, 0, 0))
    s.face([at(wide - stile, 0.0), at(wide, 0.0), at(wide, high), at(wide - stile, high)] + [at(wide - stile, v) for v in rails[-2:0:-1]], "frame", out=(1, 0, 0))
    for v0, v1 in ((rails[0], rails[1]), (rails[2], rails[3]), (rails[4], rails[5])):
        s.face([at(stile, v0), at(wide - stile, v0), at(wide - stile, v1), at(stile, v1)], "frame", out=(1, 0, 0))
    for v0, v1 in ((rails[1], rails[2]), (rails[3], rails[4])):
        s.pane([at(stile, v0), at(wide - stile, v0)], [at(stile, v1), at(wide - stile, v1)], "rubber", "glass", (1, 0, 0), margin=0.012, depth=0.012)
    back = [at(0.0, 0.0, -x), at(wide, 0.0, -x), at(wide, high, -x), at(0.0, high, -x)]
    s.face(back, "frame", out=(-1, 0, 0))
    front = [at(0.0, 0.0), at(wide, 0.0), at(wide, high), at(0.0, high)]
    for k in range(4):                                     # the edges: the rubber that seals them
        s.face([front[k], front[(k + 1) % 4], back[(k + 1) % 4], back[k]], "rubber", inside=(0, 0, high / 2))
    s.add_to(p, sharp=40)
    return p


def bus_light(group, paint, note):
    """The lamps of one of BusVehicle's groups, lit: each the lens of its lamp unit in the body again, 1.5 mm in front of
    it, in the glow material. The body is one mesh in the lit material, so nothing of it can light up by itself."""
    def make():
        p = Prop("city_bus_light_" + group, "glow", pivot="the city bus's own: the ground under its middle", note=note)
        for unit in bus_lamps():
            for part, outline in unit[7]:
                if part == group:
                    p.add(plate([tuple(lamp_point(unit, u, v, 0.0015)) for u, v in outline], unit[2]), flat(paint), sharp=10)
        return p
    return make


def bus_parts(open_doors=False):
    """Where BusVehicle puts the city bus's parts on it (kit space, turn about z): the wheels, the left ones turned round,
    and the door leaves, shut or wholly open (out 0.07 m, along 0.56 m away from the door's middle)."""
    parts = []
    for side in (1, -1):
        parts.append(("city_bus_wheel", (side * BUS_TRACK[0], BUS_AXLES[0], BUS_TYRE), 0.0 if side > 0 else 180.0))
        parts.append(("city_bus_wheel_twin", (side * BUS_TRACK[1], BUS_AXLES[1], BUS_TYRE), 0.0 if side > 0 else 180.0))
    for middle in BUS_DOORS:
        for along in (1, -1):
            parts.append(("city_bus_door", (BUS_HALF + 0.03 + (0.07 if open_doors else 0.0), middle + along * (BUS_LEAF[0] / 2 + (0.56 if open_doors else 0.0)), BUS_FLOOR), 0.0))
    return parts


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

PROPS = [
    (city_bus, 6500),
    (bus_wheel("city_bus_wheel", BUS_TREAD[0], 0.075, "front wheel of the city bus: tyre 0.48 in radius and 0.30 wide, a steel wheel with ten nuts"), 350),
    (bus_wheel("city_bus_wheel_twin", BUS_TREAD[1], 0.13, "rear pair of the city bus as one wheel: 0.48 in radius, 0.56 wide over both tyres, the hub set deep"), 600),
    (city_bus_door, 120),
    (bus_light("head", "svc_lens_clear", "the city bus's headlamps lit, two round lamps in each unit"), 120),
    (bus_light("tail", "lens_red", "the city bus's tail lamps lit, low in each back corner's unit"), 80),
    (bus_light("brake", "lens_red", "the city bus's brake lamps lit: in each back corner's unit and the high one over the number"), 90),
    (bus_light("left", "lens_amber", "the city bus's left indicators lit: front, back and the repeater on the side"), 80),
    (bus_light("right", "lens_amber", "the city bus's right indicators lit: front, back and the repeater on the side"), 80),          # the lamps 450 in all
    (fire_engine, 7000), (fire_light_bar, 350), (fire_bar_lens, 60),
]

# The fire engine beside the vehicle it stands over (to the left, as measured on the game's prefab), a person at the panel and
# one in front; the bus as BusVehicle puts it together, doors shut, somebody at its front door and one in front of it.
SIDES = {
    "city_bus": {"parts": bus_parts(), "people": (((1.50, BUS_DOORS[0]), 90.0), ((0.3, 7.0), 180.0)),
                 "views": ((0, 8), (90, 8), (180, 8), (270, 8), (38, 18), (142, 22))},
    "fire_engine":{"references": (((2.72, 7.43, 3.01), (-4.0, 0.0, 1.505)),), "people": (((1.72, 0.756), 90.0), ((0.4, 5.0), 180.0)),
                    "views": ((0, 8), (90, 8), (180, 8), (90, 52), (38, 20), (142, 28))},
}
