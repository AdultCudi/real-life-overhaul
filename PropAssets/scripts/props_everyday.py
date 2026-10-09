"""THE EVERYDAY PROPS: what is cooked in and eaten off at home, drunk from and carried on an evening out, fished
with, browsed in a shop, filled up at on the road, and stood outside an office or a shop that has been shut.
Same rules as props.py (which lists these through PROPS at the end): one function each, metres, X right,
Y forward, Z up, one mesh on the shared atlas. Unity gets (x, z, y).

THE HAND NUMBERS ARE A CONTRACT with the clips of CityAssets (clips written from the same numbers): where a note
says "held at", a clip puts a palm at exactly that point. scripts/checks_everyday.py measures them on the built
meshes. Notes give points in the kit's axes; the README table gives the same in Unity's.
"""
import math

import bmesh
import numpy as np
from mathutils import Vector

from life_props import flat, pipe, rect, ribbon, rod, smooth, sticker
from prop_kit import Paint, Prop, box, fillet, grid, lathe, loft, moved, prism, rounded_rect, tube


# ---------------------------------------------------------------------------------------------- helpers

def solid(part):
    """A closed surface, its faces turned outwards."""
    part.normal_update()
    if part.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(part, faces=part.faces[:])
    return part


def facing_away(part, point, inside=False):
    """An open skin, its faces turned away from `point` (or towards it: the inside of a tub)."""
    part.normal_update()
    point = Vector(point)
    total = sum(f.normal.dot(f.calc_center_median() - point) * f.calc_area() for f in part.faces)
    if (total < 0) != inside:
        bmesh.ops.reverse_faces(part, faces=part.faces[:])
    return part


def patch(p, paint, centre, normal, right, size, lift=0.001):
    """A flat rectangle of one finish laid on a surface (a slot, a strip of tape)."""
    n, r = Vector(normal).normalized(), Vector(right).normalized()
    up = n.cross(r)
    c = Vector(centre) + n * lift
    p.add(grid(lambda u, v: tuple(c + r * ((u - 0.5) * size[0]) + up * ((v - 0.5) * size[1])), 1, 1), flat(paint) if isinstance(paint, str) else paint, sharp=180)


def band(p, name, r0, r1, z0, z1, centre=90.0, arc=120.0, n=4, lift=0.0008):
    """A printed label round a turned part, the middle of it at `centre` degrees (90 is +Y); it reads the right way
    round from outside. Give it the arc and count that put its edges on the part's own."""
    def surface(u, v):
        t = math.radians(centre - arc / 2 + u * arc)
        r = r0 + (r1 - r0) * v + lift
        return (math.cos(t) * r, math.sin(t) * r, z0 + (z1 - z0) * v)
    p.add(grid(surface, n, 1), flat("paper").param(name), sharp=180)


def disc(radius, z, segments):
    """A flat round face looking up (the surface of a drink)."""
    return lathe([(radius, z)], segments, cap_top=True)


TO_Y = (-90, 0, 0)          # turns a part made along Z to lie along +Y


# ---------------------------------------------------------------------------------------------- home

def pan():
    p = Prop("pan", "lit", pivot="centre of the base; the handle is +X",
             note="frying pan 0.26 m across, 0.05 m deep, an egg and bacon in it; the handle is a straight round bar 0.028 m thick from x 0.14 to 0.31 at z 0.045: held at (0.23, 0, 0.045)")
    p.add(lathe([(0.098, 0.0), (0.106, 0.005), (0.127, 0.045), (0.130, 0.0485), (0.128, 0.050)], 12, cap_bottom=True), flat("paint_red"), sharp=40)
    p.add(lathe([(0.128, 0.050), (0.1245, 0.0475), (0.103, 0.009)], 12), flat("rubber_black"), sharp=40)
    floor = flat("rubber_black").planar("everyday_pan_food", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.206, 0.206))
    p.add(disc(0.103, 0.009, 12), floor)
    p.add(box(0.040, 0.034, 0.016, bevel=0.004), flat("stainless"), at=(0.136, 0, 0.040))
    for y in (-0.009, 0.009):
        p.add(lathe([(0.004, 0.0), (0.003, 0.002), (0.0, 0.0025)], 5), flat("chrome"), at=(0.134, y, 0.048), sharp=60)
    p.add(tube([(0.140, 0, 0.045), (0.300, 0, 0.045), (0.310, 0, 0.045)], [0.014, 0.014, 0.009], 8), flat("gloss_black"), sharp=40)
    return p


def plate():
    p = Prop("plate", "lit", pivot="centre of the base", note="dinner plate 0.26 m across on a foot, 0.024 m high; steak, chips and peas on it (painted)")
    meal = Paint("paint_white", grade=False).planar("everyday_plate_meal", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.26, 0.26), facing=0.6)
    p.add(lathe([(0.048, 0.0), (0.052, 0.005), (0.126, 0.019), (0.130, 0.0215), (0.127, 0.0235), (0.085, 0.0125), (0.0, 0.012)], 12, cap_bottom=True), meal, sharp=35)
    return p


def mug():
    p = Prop("mug", "lit", pivot="centre of the base; the handle is +X",
             note="0.08 m across, 0.10 m high, coffee 0.012 m below the rim; held round the body at 0.05 m (a plain cylinder there), the C handle at +X reaches x 0.074")
    p.add(lathe([(0.036, 0.0), (0.040, 0.004), (0.040, 0.097), (0.0385, 0.100)], 12, cap_bottom=True), flat("paint_blue"), sharp=40)
    p.add(lathe([(0.0385, 0.100), (0.0365, 0.0985), (0.0365, 0.088)], 12), flat("paint_white"), sharp=40)
    p.add(disc(0.0365, 0.088, 12), flat("wood_dark"))
    handle = fillet([(0.038, 0, 0.080), (0.068, 0, 0.080), (0.068, 0, 0.024), (0.038, 0, 0.024)], 0.016, 2)
    p.add(tube(handle, 0.006, 5, flatten=[0.75] * len(handle)), flat("paint_blue"), sharp=70)
    return p


def run_checkpoint():
    p = Prop("run_checkpoint", "glow", note="the morning run's marker: a blue ring 1.6 m across on the ground and a feather flag 1.4 m high either side of it (x +-0.74), CHECKPOINT up each; it reads from +Y and -Y and is run through between the flags")
    p.add(lathe([(0.80, 0.0), (0.79, 0.02), (0.67, 0.02), (0.66, 0.0)], 20), flat("wet_paint"), sharp=40)
    for k in range(10):
        a = math.radians(36 * k + 18)
        p.add(box(0.085, 0.16, 0.004), flat("reflective_white"), at=(math.cos(a) * 0.73, math.sin(a) * 0.73, 0.0205), rot=(0, 0, 36 * k + 18))
    for side in (-1, 1):
        x0 = side * 0.732

        def pole(z, s=side, x0=x0):
            return x0 - s * 0.225 * max(0.0, (z - 1.0) / 0.385) ** 2
        p.add(lathe([(0.062, 0.02), (0.066, 0.03), (0.066, 0.045), (0.022, 0.06), (0.014, 0.10)], 8), flat("rubber_black"), at=(x0, 0, 0), sharp=40)
        heights = (0.09, 0.60, 1.00, 1.14, 1.26, 1.34, 1.395)
        p.add(tube([(pole(z), 0, z) for z in heights], [0.009, 0.008, 0.007, 0.0065, 0.006, 0.0055, 0.005], 5), flat("gloss_black"), sharp=70)

        def flag(u, v, s=side, x0=x0, pole=pole):
            # v runs towards +X, u up the cloth (cut on the slant at its foot): the lettering reads up it from +Y, down it from -Y
            free = x0 - s * 0.275
            lean = (1.0 - v) if s > 0 else v                                  # 0 at the pole, 1 at the free edge
            low = 0.30 + 0.16 * lean
            z = low + (1.392 - low) * u
            at_pole = pole(z)
            x = at_pole + (free - at_pole) * lean
            return (x, 0.012 * math.sin(u * 5.0 + lean * 2.0) * lean, z)
        p.add(grid(flag, 6, 2, both_sides=True), flat("wet_paint").param("everyday_checkpoint"), sharp=180)
    return p


# ---------------------------------------------------------------------------------------------- an evening out

def wine_glass():
    p = Prop("wine_glass", "lit", pivot="the middle of the stem",
             note="0.07 m across, 0.20 m high: foot at z -0.085, rim at +0.115, red wine in the bowl; held by the stem (0.009 m thick, straight from z -0.066 to +0.028) at the origin")
    p.add(lathe([(0.032, -0.085), (0.032, -0.083), (0.006, -0.076), (0.0045, -0.066), (0.0045, 0.028), (0.014, 0.040)], 10, cap_bottom=True), flat("glass"), sharp=40)
    p.add(lathe([(0.014, 0.040), (0.029, 0.055), (0.035, 0.078)], 10), flat("everyday_wine"), sharp=40)          # the wine, seen through the bowl
    p.add(lathe([(0.035, 0.078), (0.0345, 0.100), (0.031, 0.115), (0.0295, 0.1145), (0.033, 0.079)], 10), flat("glass"), sharp=40)
    p.add(disc(0.033, 0.079, 10), flat("everyday_wine"))
    return p


def beer_bottle():
    p = Prop("beer_bottle", "lit", pivot="0.08 m up its axis",
             note="brown bottle 0.066 m across, 0.24 m high (z -0.08 to +0.16), crown cap, label facing +Y; the hand closes round the body at the origin (a plain cylinder from z -0.075 to +0.055)")
    p.add(lathe([(0.029, -0.080), (0.033, -0.075), (0.033, 0.055), (0.027, 0.078), (0.017, 0.098), (0.0135, 0.115), (0.0125, 0.147)], 10, cap_bottom=True), flat("everyday_glass_brown"), sharp=40)
    p.add(lathe([(0.0125, 0.147), (0.0152, 0.148), (0.0155, 0.154), (0.0130, 0.1595), (0.0, 0.160)], 10), flat("gold"), sharp=40)
    band(p, "everyday_beer_label", 0.033, 0.033, -0.046, 0.006, centre=90.0, arc=180.0, n=5)
    return p


def takeaway_box():
    p = Prop("takeaway_box", "lit", pivot="the middle of the underside",
             note="open noodle carton 0.14 x 0.11 x 0.09 m, its four flaps up and out, noodles and a fork in it; rests on the left palm")
    base, top, lip = rect(0.085, 0.066), rect(0.118, 0.092), rect(0.112, 0.086)
    shell = loft([[(x, y, 0.0) for x, y in base], [(x, y, 0.068) for x, y in top], [(x, y, 0.068) for x, y in lip]], cap_start=True)
    p.add(facing_away(shell, (0, 0, 0.04)), flat("paper"))
    liner = loft([[(x, y, 0.068) for x, y in lip], [(x * 0.94, y * 0.94, 0.048) for x, y in lip]])
    p.add(facing_away(liner, (0, 0, 0.06), inside=True), flat("paper"))

    def heap(u, v):
        dome = (math.sin(math.pi * u) * math.sin(math.pi * v)) ** 0.6
        return ((u - 0.5) * 0.106, (v - 0.5) * 0.081, 0.050 + 0.026 * dome + 0.003 * math.sin(u * 17.0) * math.cos(v * 13.0) * dome)
    p.add(grid(heap, 4, 3), flat("paper").param("everyday_noodles"), sharp=180)
    for sx, sy, w in ((0, 1, 0.116), (0, -1, 0.116), (1, 0, 0.090), (-1, 0, 0.090)):       # the flaps, standing up and out
        centre = (sx * 0.0640, sy * 0.0495, 0.0785)
        p.add(box(w if sy else 0.0015, 0.0015 if sy else w, 0.024), flat("paper"), at=centre, rot=(-sy * 26, sx * 26, 0))
    for sy in (-1, 1):
        normal = Vector((0, sy * 0.068, -0.013)).normalized()
        sticker(p, "everyday_takeaway", (0, sy * 0.0395, 0.034), tuple(normal), (-sy, 0, 0), (0.060, 0.048), lift=0.0008)
    fork = [(-0.022, -0.008, 0.066), (0.012, 0.004, 0.076), (0.060, 0.022, 0.0875)]
    p.add(ribbon(fork, lambda q: (0, 0, 1), 0.009, 0.002), flat("paint_white"), sharp=100)
    return p


def popcorn_tub():
    p = Prop("popcorn_tub", "lit", pivot="the middle of the underside",
             note="striped tub 0.16 m across at the rim (0.11 at the foot), 0.165 m high, heaped to 0.20 m; POPCORN on its +Y and -Y sides")
    r = lambda z: 0.055 + 0.024 * (z - 0.003) / 0.157
    p.add(lathe([(0.052, 0.0), (0.055, 0.003), (0.079, 0.160), (0.080, 0.165), (0.077, 0.165), (0.0765, 0.150)], 12, cap_bottom=True),
          Paint("paper", grade=False).wrap("awning", 0.0, 0.165), sharp=40)
    heap = lathe([(0.0765, 0.150), (0.071, 0.171), (0.052, 0.189), (0.026, 0.198), (0.0, 0.200)], 12)
    rng = np.random.default_rng(5)
    for v in heap.verts:                                                           # a heap, not a dome
        if 0.16 < v.co.z < 0.1985 and v.co.xy.length > 0.01:
            v.co.z += rng.uniform(-0.004, 0.0035)
            v.co.xy *= 1.0 + rng.uniform(-0.05, 0.04)
    top = flat("paper").planar("everyday_popcorn_top", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.16, 0.16), facing=-1.0)
    p.add(heap, top, sharp=180)
    for centre in (90.0, 270.0):
        band(p, "everyday_popcorn_label", r(0.062), r(0.112), 0.062, 0.112, centre=centre, arc=120.0, n=4)
    return p


def gift_box():
    p = Prop("gift_box", "lit", note="wrapped present 0.20 x 0.20 m, 0.10 m high under its lid, a gold ribbon round it both ways and a bow on top to 0.14 m")
    p.add(box(0.188, 0.188, 0.074), "paint_red", at=(0, 0, 0.037))
    p.add(box(0.196, 0.196, 0.030, bevel=0.003), flat("paint_red"), at=(0, 0, 0.085))
    p.add(box(0.200, 0.026, 0.102), flat("gold"), at=(0, 0, 0.051))
    p.add(box(0.026, 0.200, 0.1025), flat("gold"), at=(0, 0, 0.05125))
    for side in (-1, 1):                                                           # the bow: two loops, two tails, a knot
        loop = smooth([(0, 0, 0.106), (side * 0.040, 0, 0.1392), (side * 0.074, 0, 0.130), (side * 0.070, 0, 0.110), (side * 0.030, 0, 0.104), (0, 0, 0.108)], 1)
        centre = Vector((side * 0.042, 0, 0.118))
        p.add(ribbon(loop, lambda q, c=centre: q - c, 0.026, 0.0016), flat("gold"), sharp=60, rot=(0, 0, side * 12))
        tail = [(0, side * 0.004, 0.108), (side * 0.030, side * 0.040, 0.104), (side * 0.046, side * 0.072, 0.1025)]
        p.add(ribbon(tail, lambda q: (0, 0, 1), 0.022, 0.0016), flat("gold"), sharp=60)
    p.add(lathe([(0.012, 0.100), (0.015, 0.110), (0.010, 0.120), (0.0, 0.122)], 6), flat("gold"), sharp=50)
    return p


def flowers_bunch():
    p = Prop("flowers_bunch", "lit", pivot="the wrapped stems, 0.10 m up from their cut ends",
             note="a bunch in brown paper 0.42 m long: stems to z -0.10, blooms to +0.32, 0.22 m across; the hand closes round the wrap at the origin (0.039 m thick there, 0.035 at z -0.03, 0.042 at +0.03)")
    p.add(lathe([(0.010, -0.100), (0.012, -0.084)], 6, cap_bottom=True), flat("plastic_green"), sharp=60)
    cone = lathe([(0.0145, -0.086), (0.0172, -0.036), (0.0215, 0.036), (0.034, 0.080), (0.062, 0.145), (0.086, 0.195)], 10)
    for v in cone.verts:                                                           # the paper stands higher behind the flowers
        if v.co.z > 0.14:
            v.co.z += 0.045 * max(0.0, -v.co.y / 0.086) * (v.co.z - 0.14) / 0.055
    p.add(cone, Paint("cardboard", grade=False), sharp=40)
    p.add(lathe([(0.084, 0.190), (0.066, 0.214), (0.030, 0.232), (0.0, 0.236)], 8), flat("paint_green"), sharp=180)
    bloom = [(0.007, 0.0), (0.024, 0.014), (0.023, 0.030), (0.0, 0.037)]
    tints = ("paint_red", "paint_yellow", "everyday_pink")
    heads = [((0.0, 0.0, 0.281), 0.0, 0.0)]
    heads += [((math.cos(a) * 0.048, math.sin(a) * 0.048, 0.262), 30.0, math.degrees(a)) for a in (math.radians(20 + 72 * k) for k in range(5))]
    heads += [((math.cos(a) * 0.079, math.sin(a) * 0.079 + 0.004, 0.226), 62.0, math.degrees(a)) for a in (math.radians(56 + 72 * k) for k in (0, 1, 3, 4, 2))][:4]
    for k, (at, tilt, turn) in enumerate(heads):
        p.add(lathe(bloom, 5), flat(tints[k % 3]), at=at, rot=(0, tilt, turn), sharp=75)
        p.add(lathe([(0.008, 0.031), (0.0, 0.039)], 5), flat("lens_amber" if k % 3 != 1 else "wood_dark"), at=at, rot=(0, tilt, turn), sharp=180)
    for k in range(5):                                                             # leaves between the outer blooms
        turn = 92 + 72 * k

        def leaf(u, v):
            wide = math.sin(math.pi * u) ** 0.8
            return (0.050 + 0.058 * u, (v - 0.5) * 0.034 * wide, 0.200 + 0.035 * u - 0.012 * u * u + 0.006 * abs(2 * v - 1) * wide)
        p.add(grid(leaf, 2, 2, both_sides=True), flat("plastic_green"), rot=(0, 0, turn), sharp=180)
    return p


def gym_bag():
    p = Prop("gym_bag", "lit", pivot="the middle of the handles",
             note="barrel holdall 0.50 x 0.24 m hanging from its handles: the wrap that joins them is a round bar along X at the origin, 0.03 m thick, 0.11 m long; the bag's top is 0.07 m below, its underside 0.245 m below")
    cz, a, b = -0.1565, 0.120, 0.0875
    section = [(a * math.copysign(abs(math.cos(t)) ** 0.7, math.cos(t)), b * math.copysign(abs(math.sin(t)) ** (0.5 if math.sin(t) < 0 else 0.75), math.sin(t)))
               for t in np.linspace(0, 2 * math.pi, 12, endpoint=False)]
    rings = [[(x, y * s, cz + z * s) for y, z in section] for x, s in ((-0.238, 0.55), (-0.226, 0.86), (-0.190, 1.0), (0.190, 1.0), (0.226, 0.86), (0.238, 0.55))]
    p.add(loft(rings, cap_start=True, cap_end=True), "fabric_teal", sharp=60)
    for x in (-0.135, 0.135):                                                      # a white stripe round it either side of the handles
        hoop = [[(x + dx, y * 1.012, cz + z * 1.012) for y, z in section] for dx in (-0.016, 0.016)]
        p.add(loft(hoop), flat("reflective_white"), sharp=60)
    for side in (-1, 1):
        p.add(box(0.024, 0.150, 0.100, bevel=0.007), flat("nylon_black"), at=(side * 0.2355, 0, cz - 0.008))
        p.add(box(0.004, 0.110, 0.006), flat("steel_dark"), at=(side * 0.2480, 0, cz + 0.026))
    p.add(box(0.400, 0.014, 0.005), flat("steel_dark"), at=(0, 0, cz + b + 0.001))
    p.add(box(0.014, 0.020, 0.008, bevel=0.002), flat("chrome"), at=(0.185, 0, cz + b + 0.004))
    p.add(tube([(-0.055, 0, 0), (0.055, 0, 0)], 0.015, 8), flat("leather_black"), sharp=70)
    for sy in (-1, 1):
        for sx in (-1, 1):
            strap = [(sx * 0.064, sy * 0.004, -0.002), (sx * 0.078, sy * 0.022, -0.026), (sx * 0.100, sy * 0.058, -0.074), (sx * 0.104, sy * 0.096, -0.118), (sx * 0.104, sy * 0.1165, -0.170)]
            p.add(ribbon(strap, lambda q, sy=sy: (0, sy, 0.8), 0.026, 0.004), flat("nylon_black"), sharp=60)
    return p


def moving_box():
    p = Prop("moving_box", "lit", note="removals box 0.45 x 0.35 x 0.35 m (0.35 high), taped, FRAGILE on its +Y and -Y sides, a hand hole in each end; carried like parcel_m, a hand under each end along X")
    sides = Paint("cardboard").planar("everyday_box_side", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.45, 0.35)).planar("everyday_box_side", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.45, 0.35))
    p.add(box(0.45, 0.35, 0.35, bevel=0.005), sides, at=(0, 0, 0.175))
    patch(p, "tape_brown", (0, 0, 0.35), (0, 0, 1), (1, 0, 0), (0.44, 0.055), lift=0.0008)
    for side in (-1, 1):
        patch(p, "tape_brown", (side * 0.225, 0, 0.305), (side, 0, 0), (0, side, 0), (0.055, 0.08), lift=0.0008)
        hole = prism(rounded_rect(0.095, 0.032, 0.014, 1), 0.0, 0.0006, bottom=False)
        p.add(hole, flat("rubber_black"), at=(side * 0.2250, 0, 0.215), rot=(90, 0, side * 90))
    return p


def newspaper_ledger():
    p = Prop("newspaper_ledger", "lit", pivot="bottom of the centre fold; it stands in the XZ plane",
             note="the City Ledger: the `newspaper` prop sheet for sheet (0.58 x 0.40 m, read from -Y, hands at (-0.25, 0, 0.13) and (0.25, 0, 0.13)) with the Ledger's front page facing +Y")

    def page(u, v):                                         # as life_props.newspaper
        s = abs(2.0 * u - 1.0)
        curl = v ** 2.2 * s ** 1.6
        y = -0.030 * s + 0.011 * math.sin(math.pi * s) * (0.35 + 0.65 * v) + 0.050 * curl
        return ((u - 0.5) * 0.58 * (1.0 - 0.012 * curl), y, 0.40 * v - 0.022 * curl)
    for half in (0, 1):
        p.add(grid(lambda a, b, h=half: page((h + a) / 2, b), 4, 4), flat("paper").param("news_inner"), sharp=180)
        p.add(grid(lambda a, b, h=half: page((h + 1 - a) / 2, b), 4, 4), flat("paper").param("everyday_ledger_front" if half == 0 else "news_inner"), sharp=180)
    return p


# ---------------------------------------------------------------------------------------------- leisure

def fishing_rod():
    p = Prop("fishing_rod", "lit", pivot="the butt of the handle; the rod runs along +Y",
             note="spinning rod 2.10 m: cork handle 0.028 m thick on the Y axis from 0 to 0.56 (reel seat 0.34 to 0.46, 0.032 m at its hoods), hands at y 0.10, 0.40 and 0.52; the reel hangs below at y 0.40 with its crank knob at (-0.058, 0.402, -0.056); blank to 0.003 m at the tip, five guides and a tip ring underneath")
    line = lambda *ys: [(0, y, 0) for y in ys]
    p.add(tube(line(0.0, 0.004, 0.020), [0.0115, 0.0155, 0.0155], 8), flat("rubber_black"), sharp=40)
    p.add(tube(line(0.018, 0.340), 0.014, 8, caps=False), flat("wood"), sharp=70)
    p.add(tube(line(0.340, 0.460), 0.0142, 8, caps=False), flat("gloss_black"), sharp=70)
    for y0, y1 in ((0.340, 0.362), (0.438, 0.460)):                                # the hoods that clamp the reel's foot
        p.add(tube(line(y0, y0 + 0.004, y1) if y0 < 0.4 else line(y0, y1 - 0.004, y1), [0.0143, 0.016, 0.016] if y0 < 0.4 else [0.016, 0.016, 0.0143], 8, caps=False), flat("chrome"), sharp=40)
    p.add(tube(line(0.460, 0.550, 0.560), [0.014, 0.014, 0.0085], 8), flat("wood"), sharp=40)
    p.add(tube(line(0.558, 0.95, 1.35, 1.75, 2.097), [0.0060, 0.0046, 0.0034, 0.0024, 0.0015], 6), flat("gloss_black"), sharp=70)
    p.add(tube(line(0.560, 0.585), 0.0068, 6, caps=False), flat("paint_red"), sharp=70)
    for y, r in ((0.82, 0.017), (1.12, 0.013), (1.42, 0.010), (1.68, 0.008), (1.90, 0.0065)):      # the guides: a ring under the blank on a foot
        drop = r + 0.008
        ring = [(math.cos(t) * r, y, -drop + math.sin(t) * r) for t in np.linspace(0, 2 * math.pi, 5, endpoint=False)]
        p.add(tube(ring, 0.0014, 3, closed=True), flat("chrome"), sharp=180)
        rod(p, (0, y - 0.022, -0.003), (0, y, -drop + r), 0.0013, "chrome", 3, caps=False)
        p.add(tube(line(y - 0.030, y - 0.016), 0.0062 - (y - 0.56) * 0.0029, 6, caps=False), flat("paint_red"), sharp=70)
    ring = [(math.cos(t) * 0.0045, 2.0988, -0.004 + math.sin(t) * 0.0045) for t in np.linspace(0, 2 * math.pi, 5, endpoint=False)]
    p.add(tube(ring, 0.0012, 3, closed=True), flat("chrome"), sharp=180)
    # The reel: its foot under the seat, the stem, the gear housing, the rotor and spool pointing up the rod, the bail, the crank on the left.
    p.add(box(0.012, 0.070, 0.005), flat("steel_dark"), at=(0, 0.400, -0.0165))
    p.add(box(0.008, 0.020, 0.030), flat("steel_dark"), at=(0, 0.396, -0.033))
    p.add(box(0.024, 0.044, 0.034, bevel=0.007), flat("steel_dark"), at=(0, 0.390, -0.062))
    spool = lathe([(0.012, 0.0), (0.022, 0.004), (0.024, 0.022), (0.0255, 0.027), (0.016, 0.029), (0.016, 0.050), (0.0255, 0.052), (0.0235, 0.060), (0.0, 0.063)], 8)
    p.add(spool, flat("stainless"), at=(0, 0.410, -0.060), rot=TO_Y, sharp=50)
    p.add(lathe([(0.0195, 0.0295), (0.0195, 0.0515)], 8), flat("linen"), at=(0, 0.410, -0.060), rot=TO_Y, sharp=50)        # the line on the spool
    bail = [(math.cos(t) * 0.027, 0.434 + 0.020 * math.sin(t), -0.060 - math.sin(t) * 0.026) for t in np.linspace(0, math.pi, 6)]
    p.add(tube(bail, 0.0013, 3), flat("chrome"), sharp=180)
    rod(p, (-0.012, 0.388, -0.064), (-0.041, 0.388, -0.064), 0.0045, "chrome", 5)
    rod(p, (-0.041, 0.383, -0.069), (-0.043, 0.404, -0.054), 0.0045, "chrome", 4)
    p.add(lathe([(0.0045, 0.0), (0.008, 0.005), (0.008, 0.019), (0.005, 0.024), (0.0, 0.0245)], 6), flat("rubber_black"), at=(-0.0455, 0.402, -0.056), rot=(0, -90, 0), sharp=50)
    return p


def fishing_float():
    p = Prop("fishing_float", "lit", pivot="the waterline", note="0.045 m across: red from the waterline to 0.043 m, an orange antenna to 0.07 m, white below to -0.04 m")
    p.add(lathe([(0.0, -0.040), (0.010, -0.030), (0.021, -0.010), (0.0225, 0.0)], 8), flat("reflective_white"), sharp=40)
    p.add(lathe([(0.0225, 0.0), (0.019, 0.016), (0.008, 0.036), (0.003, 0.043)], 8), flat("lens_red"), sharp=40)
    p.add(lathe([(0.0022, 0.043), (0.0022, 0.060), (0.004, 0.064), (0.0, 0.070)], 5), flat("lens_amber"), sharp=40)
    return p


def fishing_line():
    p = Prop("fishing_line", "lit", pivot="one end; it runs along +Y", note="a pale thread one metre long and 0.004 m across, to be scaled along its length")
    p.add(tube([(0, 0, 0), (0, 1, 0)], 0.002, 4, caps=False), flat("reflective_white"), sharp=180)
    return p


def drink_glass():
    p = Prop("drink_glass", "lit", note="tumbler 0.075 m across the rim, 0.12 m high, a drink and ice in it to 0.086 m; held as coffee_cup, round the glass 0.06 m up (0.068 m across there)")
    p.add(lathe([(0.029, 0.0), (0.0312, 0.003), (0.0318, 0.012)], 12, cap_bottom=True), flat("glass"), sharp=40)
    p.add(lathe([(0.0318, 0.012), (0.0355, 0.086)], 12), flat("lens_amber"), sharp=40)              # the drink, seen through the glass
    p.add(lathe([(0.0355, 0.086), (0.0375, 0.120), (0.0357, 0.1195), (0.0342, 0.087)], 12), flat("glass"), sharp=40)
    p.add(disc(0.0342, 0.087, 12), flat("lens_amber"))
    for at, turn in (((0.010, 0.006, 0.086), (14, 22, 30)), ((-0.012, 0.004, 0.084), (-20, 10, 65)), ((0.0, -0.014, 0.085), (8, -18, 10))):
        p.add(box(0.019, 0.019, 0.019), flat("glass"), at=at, rot=turn)
    return p


def cocktail_glass():
    p = Prop("cocktail_glass", "lit", note="martini glass 0.11 m across, 0.16 m high, on a stem 0.008 m thick; a pink drink to 0.145 m and an olive on a stick across the rim")
    p.add(lathe([(0.034, 0.0), (0.034, 0.002), (0.006, 0.009), (0.004, 0.020), (0.004, 0.082)], 10, cap_bottom=True), flat("glass"), sharp=40)
    p.add(lathe([(0.004, 0.082), (0.0455, 0.142)], 10), flat("everyday_pink"), sharp=40)
    p.add(lathe([(0.0455, 0.142), (0.055, 0.1585), (0.0535, 0.160), (0.0445, 0.1445)], 10), flat("glass"), sharp=40)
    p.add(disc(0.0445, 0.1445, 10), flat("everyday_pink"))
    rod(p, (-0.036, 0.012, 0.1455), (0.0535, -0.010, 0.1595), 0.0011, "wood", 3)
    p.add(lathe([(0.0, -0.009), (0.0075, -0.005), (0.0085, 0.001), (0.006, 0.007), (0.0, 0.009)], 6), flat("plastic_green"), at=(0.004, 0.002, 0.1515), rot=(0, 80, -14), sharp=180)
    p.add(lathe([(0.003, 0.0085), (0.0, 0.0098)], 5), flat("paint_red"), at=(0.004, 0.002, 0.1515), rot=(0, 80, -14), sharp=180)
    return p


def _garment(p, x, outline, thick, paint, top):
    """A garment on its hanger, seen edge on from the front of the rack: `outline` is (y, z) points anticlockwise
    with z measured down from the hanger's shoulder line, which is `top` metres up."""
    middle = sum(z for _, z in outline) / len(outline)
    rings = [[(x + dx, y * s, top + middle + (z - middle) * s) for y, z in outline] for dx, s in ((-thick / 2, 0.93), (-thick * 0.28, 1.0), (thick * 0.28, 1.0), (thick / 2, 0.93))]
    p.add(loft(rings, cap_start=True, cap_end=True), paint, sharp=40)


def _mirrored(half):
    """A whole outline from its -Y half, given from the top centre down to the hem's middle."""
    return half + [(-y, z) for y, z in half[::-1] if abs(y) > 1e-6]


TEE = _mirrored([(0.0, -0.035), (-0.050, 0.0), (-0.205, -0.045), (-0.240, -0.185), (-0.190, -0.205), (-0.168, -0.110), (-0.172, -0.600), (0.0, -0.615)])
SHIRT = _mirrored([(0.0, -0.050), (-0.045, 0.0), (-0.200, -0.040), (-0.248, -0.560), (-0.200, -0.570), (-0.186, -0.130), (-0.176, -0.690), (0.0, -0.705)])
JACKET = _mirrored([(0.0, -0.090), (-0.050, 0.0), (-0.215, -0.045), (-0.262, -0.610), (-0.208, -0.620), (-0.198, -0.140), (-0.192, -0.760), (0.0, -0.770)])
DRESS = _mirrored([(0.0, -0.085), (-0.050, -0.010), (-0.075, 0.0), (-0.150, -0.030), (-0.132, -0.170), (-0.112, -0.350), (-0.150, -0.640), (-0.205, -0.960), (0.0, -0.985)])
TROUSERS = _mirrored([(0.0, -0.090), (-0.148, -0.090), (-0.158, -0.110), (-0.150, -0.640), (0.0, -0.650)])


def clothes_rack():
    p = Prop("clothes_rack", "lit", note="shop garment rail 1.44 m wide on castors, the rail along X 1.55 m up, seven garments on hangers, a SALE sign on top to 1.76 m; browsed from +Y, where the rail is clear to a hand between the hangers' hooks")
    for side in (-1, 1):
        x = side * 0.700
        p.add(tube([(x, 0, 0.085), (x, 0, 1.55)], 0.0175, 8, caps=False), Paint("chrome"), sharp=70)
        p.add(box(0.040, 0.560, 0.034, bevel=0.008), flat("chrome"), at=(x, 0, 0.087))
        for y in (-0.245, 0.245):
            p.add(lathe([(0.026, -0.011), (0.034, -0.007), (0.034, 0.007), (0.026, 0.011)], 6, cap_top=True, cap_bottom=True), flat("rubber_black"), at=(x, y, 0.034), rot=(0, 90, 0), sharp=40)
            p.add(box(0.030, 0.034, 0.010), flat("steel_dark"), at=(x, y, 0.068))
        p.add(lathe([(0.0195, 1.533), (0.0200, 1.567)], 8, cap_top=True), flat("chrome"), at=(x, 0, 0), sharp=40)
    p.add(tube([(-0.720, 0, 1.55), (0.720, 0, 1.55)], 0.0175, 8), flat("chrome"), sharp=70)
    rod(p, (-0.700, 0, 0.30), (0.700, 0, 0.30), 0.011, "chrome", 6, caps=False)
    # The sign: a card in a frame on two stalks off the rail.
    faces = flat("paint_red").planar("everyday_rack_sign", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.46, 0.161)).planar("everyday_rack_sign", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.46, 0.161))
    p.add(box(0.46, 0.010, 0.161), faces, at=(0, 0, 1.675))
    p.add(box(0.48, 0.016, 0.010), flat("chrome"), at=(0, 0, 1.755))
    p.add(box(0.48, 0.016, 0.010), flat("chrome"), at=(0, 0, 1.595))
    for x in (-0.20, 0.20):
        rod(p, (x, 0, 1.565), (x, 0, 1.595), 0.006, "chrome", 5, caps=False)
    # Seven garments. Each hangs from a wire hook over the rail; its shoulder line is 0.08 m below the rail's middle.
    stripes = lambda swatch, name, drop: Paint(swatch, grade=False).planar(name, (1, 0, 0), (0, 1, 0), (0, 0, 1.47 - drop / 2), (0.52, drop + 0.04), turns=1).planar(
        name, (-1, 0, 0), (0, -1, 0), (0, 0, 1.47 - drop / 2), (0.52, drop + 0.04), turns=1)
    rail = ((TEE, 0.036, stripes("linen", "awning", 0.615), "wood"), (SHIRT, 0.040, flat("wool_blue"), "wood"), (DRESS, 0.034, flat("plush_red"), "gloss_black"),
            (JACKET, 0.062, flat("cloth_navy"), "wood_dark"), (TROUSERS, 0.046, flat("canvas_olive"), "chrome"), (TEE, 0.036, flat("tent_yellow"), "wood"),
            (SHIRT, 0.040, flat("linen"), "wood"))
    for k, (outline, thick, paint, hanger) in enumerate(rail):
        x = -0.54 + 0.18 * k
        top = 1.47
        _garment(p, x, outline, thick, paint, top)
        hook = [(x, 0.0, top - 0.012), (x, 0.0, 1.518), (x, 0.021, 1.538), (x, 0.021, 1.562), (x, 0.0, 1.574), (x, -0.021, 1.562), (x, -0.023, 1.548)]
        p.add(tube(hook, 0.0028, 3), flat("chrome"), sharp=180)
        if outline is TROUSERS:                             # a wire hanger: the trousers are folded over its bar
            wire = [(x, -0.185, top - 0.088), (x, 0.0, top - 0.008), (x, 0.185, top - 0.088)]
            p.add(tube(wire, 0.0028, 3), flat("chrome"), sharp=180)
            rod(p, (x, -0.185, top - 0.088), (x, 0.185, top - 0.088), 0.0045, "chrome", 4)
        else:                                               # the hanger's shoulders showing in the neck
            p.add(box(0.012, 0.110, 0.016, bevel=0.003), flat(hanger), at=(x, 0, top - 0.010))
        if outline is SHIRT or outline is JACKET:           # a collar, and a row of buttons down the front edge
            for sy in (-1, 1):
                collar = [(x, sy * 0.048, top + 0.004), (x, sy * 0.075, top - 0.062), (x, sy * 0.004, top - 0.060 if outline is SHIRT else top - 0.105)]
                tone = "paint_white" if k == 1 else ("paper" if outline is SHIRT else "nylon_black")
                p.add(solid(loft([[(q[0] - thick / 2 - 0.003, q[1], q[2]) for q in collar], [(q[0] + thick / 2 + 0.003, q[1], q[2]) for q in collar]], cap_start=True, cap_end=True)), flat(tone))
    return p


def shopping_bag():
    p = Prop("shopping_bag", "lit", pivot="the top of the handles, in the middle: the bag hangs below",
             note="paper carrier 0.32 x 0.12 m, 0.29 m high, its twisted-paper handles rising 0.09 m to the pivot; the shop's panel on its +Y and -Y faces; tissue paper in the top")
    top = -0.090
    outer, inner = rect(0.320, 0.120), rect(0.314, 0.114)
    shell = loft([[(x * 0.97, y * 0.92, -0.380) for x, y in outer], [(x, y, top) for x, y in outer], [(x, y, top) for x, y in inner]], cap_start=True)
    faces = Paint("paper").planar("everyday_bag_logo", (0, 1, 0), (-1, 0, 0), (0, 0, -0.235), (0.235, 0.282)).planar("everyday_bag_logo", (0, -1, 0), (1, 0, 0), (0, 0, -0.235), (0.235, 0.282))
    p.add(facing_away(shell, (0, 0, -0.2)), faces)
    p.add(facing_away(loft([[(x, y, top) for x, y in inner], [(x, y, top - 0.045) for x, y in inner]]), (0, 0, top), inside=True), flat("paper"))

    def tissue(u, v):
        return ((u - 0.5) * 0.314, (v - 0.5) * 0.114, top - 0.040 + 0.026 * math.sin(u * 9.0 + 1.0) * math.sin(math.pi * v) * math.sin(math.pi * u) + 0.012 * math.sin(math.pi * v))
    p.add(grid(tissue, 6, 2), flat("everyday_pink"), sharp=180)
    for sy in (-1, 1):
        y = sy * 0.055
        handle = [(-0.062, y, top - 0.030), (-0.060, y, top + 0.006), (-0.052, sy * 0.038, -0.050), (-0.038, sy * 0.017, -0.016), (-0.018, sy * 0.008, -0.0036), (0.018, sy * 0.008, -0.0036),
                  (0.038, sy * 0.017, -0.016), (0.052, sy * 0.038, -0.050), (0.060, y, top + 0.006), (0.062, y, top - 0.030)]
        p.add(tube(handle, 0.0036, 4), flat("cardboard"), sharp=100)
    return p


# ---------------------------------------------------------------------------------------------- transport

def turnstile():
    p = Prop("turnstile", "lit", note="subway fare gate: a stainless cabinet 0.30 x 1.20 m, its top 0.975 m up with the card reader on it at the +Y (entry) end, a display to 1.00 m behind the reader; the tripod's hub on the +X side 0.80 m up, one arm barring the way out to x 0.60")
    p.add(box(0.300, 1.200, 0.060, bevel=0.008), flat("steel_dark"), at=(0, 0, 0.030))
    p.add(box(0.280, 1.170, 0.890, bevel=0.022), "stainless", at=(0, 0, 0.505))
    p.add(box(0.300, 1.200, 0.025, bevel=0.008), flat("stainless"), at=(0, 0, 0.9625))
    sticker(p, "everyday_fare_reader", (0, 0.455, 0.975), (0, 0, 1), (1, 0, 0), (0.15, 0.15), lift=0.0012)
    wedge = [[(x, y, z) for y, z in ((0.355, 0.975), (0.215, 0.975), (0.215, 1.0), (0.275, 1.0))] for x in (-0.11, 0.11)]
    p.add(solid(loft(wedge, cap_start=True, cap_end=True)), flat("gloss_black"))
    slope = Vector((0, 0.025, 0.08)).normalized()
    sticker(p, "everyday_fare_lamps", (0, 0.315, 0.9875), tuple(slope), (-1, 0, 0), (0.17, 0.072), lift=0.0012)
    for sy, right in ((1, (-1, 0, 0)), (-1, (1, 0, 0))):                           # go / no entry, on both ends
        p.add(box(0.200, 0.006, 0.110, bevel=0.002), flat("gloss_black"), at=(0, sy * 0.586, 0.800))
        sticker(p, "everyday_fare_lamps", (0, sy * 0.589, 0.800), (0, sy, 0), right, (0.18, 0.09), lift=0.0012)
    p.add(box(0.006, 0.520, 0.560, bevel=0.002), flat("stainless"), at=(-0.141, -0.24, 0.42))        # the service door
    p.add(lathe([(0.010, 0.0), (0.010, 0.004), (0.0, 0.005)], 6), flat("steel_dark"), at=(-0.144, -0.44, 0.58), rot=(0, -90, 0), sharp=50)
    p.add(box(0.012, 1.10, 0.035, bevel=0.004), flat("rubber_black"), at=(0.142, 0, 0.30))
    # The tripod: a hub tilted 45 degrees out and down, three arms on it, one of them level across the way.
    hub, axis = Vector((0.118, 0.0, 0.820)), Vector((1, 0, -1)).normalized()
    p.add(box(0.090, 0.300, 0.300, bevel=0.030), flat("stainless"), at=(0.125, 0, 0.800))
    p.add(lathe([(0.105, 0.0), (0.105, 0.040), (0.075, 0.075), (0.045, 0.090), (0.0, 0.092)], 10), flat("steel_dark"), at=tuple(hub), rot=(0, 135, 0), sharp=40)
    root = hub + axis * 0.070
    for k in range(3):
        t = math.radians(120 * k)
        v = Vector((1, 0, 0))
        aim = v * math.cos(t) + axis.cross(v) * math.sin(t) + axis * axis.dot(v) * (1 - math.cos(t))
        tip = root + aim * 0.435
        p.add(tube([tuple(root + aim * 0.03), tuple(tip - aim * 0.012), tuple(tip)], [0.019, 0.019, 0.012], 8), flat("chrome"), sharp=50)
    return p


def fuel_pump():
    p = Prop("fuel_pump", "lit", note="filling-station pump 0.50 x 0.40 m, 1.60 m high: price and litres on its +Y and -Y faces from 0.88 to 1.36 m, a nozzle in its boot and a hose looping down on each side (x +-0.25), the brand on top, a kick plate below")
    p.add(box(0.420, 0.400, 0.100, bevel=0.012), "steel_dark", at=(0, 0, 0.050))
    p.add(box(0.380, 0.360, 0.750, bevel=0.016), "paint_red", at=(0, 0, 0.475))
    p.add(box(0.384, 0.364, 0.070), flat("paint_white"), at=(0, 0, 0.700))
    p.add(box(0.350, 0.330, 0.024), flat("steel_dark"), at=(0, 0, 0.860))
    faces = flat("paint_white").planar("everyday_pump_face", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.300, 0.4875)).planar("everyday_pump_face", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.300, 0.4875))
    p.add(box(0.380, 0.360, 0.500, bevel=0.016), flat("paint_white"), at=(0, 0, 1.120))
    for sy, right in ((1, (-1, 0, 0)), (-1, (1, 0, 0))):
        p.add(box(0.312, 0.008, 0.498), faces, at=(0, sy * 0.181, 1.120))
        sticker(p, "everyday_flammable", (sy * -0.115, sy * 0.180, 0.520), (0, sy, 0), right, (0.085, 0.085), lift=0.0015)
        p.add(box(0.300, 0.004, 0.110), flat("stainless"), at=(0, sy * 0.181, 0.230))
    brand = flat("paint_red").planar("everyday_pump_top", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.42, 0.158)).planar("everyday_pump_top", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.42, 0.158))
    p.add(box(0.440, 0.110, 0.170, bevel=0.012), brand, at=(0, 0, 1.515))
    for x in (-0.13, 0.13):
        p.add(box(0.035, 0.060, 0.070), flat("steel_dark"), at=(x, 0, 1.400))
    for side in (-1, 1):                                                           # a boot, the nozzle hung in it, and its hose
        x = side * 0.190
        p.add(box(0.060, 0.090, 0.150, bevel=0.010), flat("gloss_black"), at=(x + side * 0.030, 0.020, 1.080))
        nozzle = [(x + side * 0.034, 0.020, 1.110), (x + side * 0.038, 0.022, 0.990), (x + side * 0.040, 0.050, 0.900), (x + side * 0.040, 0.060, 0.860)]
        p.add(tube(nozzle, [0.011, 0.020, 0.020, 0.015], 6), flat("plastic_green"), sharp=50)
        guard = [(x + side * 0.040, 0.030, 0.985), (x + side * 0.040, -0.012, 0.965), (x + side * 0.040, 0.012, 0.900), (x + side * 0.040, 0.040, 0.890)]
        p.add(tube(guard, 0.004, 3), flat("steel_dark"), sharp=180)
        hose = smooth([(x + side * 0.040, 0.060, 0.865), (x + side * 0.044, 0.080, 0.640), (x + side * 0.045, 0.030, 0.420), (x + side * 0.045, -0.060, 0.430), (x + side * 0.044, -0.105, 0.700),
                       (x + side * 0.042, -0.105, 1.180)], 2) + [Vector((x + side * 0.036, -0.105, 1.270)), Vector((x + side * 0.012, -0.105, 1.300))]
        p.add(tube(hose, 0.0125, 5), flat("rubber_black"), sharp=70)
        p.add(box(0.030, 0.050, 0.050), flat("steel_dark"), at=(x + side * 0.010, -0.105, 1.300))
    return p


def jerry_can():
    p = Prop("jerry_can", "lit", note="20 l red can 0.35 x 0.18 m, 0.37 m high at its top, the spout's cap at the +X corner to 0.45 m; carried by the bar over the top: along X, centred (0, 0, 0.415), 0.03 m thick, 0.11 m of it straight, 0.03 m clear above the can")
    rings = [[(x, y, z) for x, y in rounded_rect(w, d, r, 2)] for w, d, r, z in ((0.326, 0.150, 0.030, 0.0), (0.350, 0.172, 0.040, 0.014), (0.350, 0.172, 0.040, 0.325), (0.330, 0.154, 0.034, 0.362), (0.300, 0.126, 0.028, 0.370))]
    p.add(solid(loft(rings, cap_start=True, cap_end=True)), "paint_red", sharp=35)
    for sy, right in ((1, (-1, 0, 0)), (-1, (1, 0, 0))):                           # the pressed X on each side
        for k, turn in enumerate((38, -38)):
            p.add(box(0.300, 0.008 - 0.001 * k, 0.026), flat("paint_red"), at=(0, sy * 0.086, 0.170), rot=(0, turn, 0))
        p.add(box(0.250, 0.008, 0.022), flat("paint_red"), at=(0, sy * 0.086, 0.035))
        p.add(box(0.250, 0.008, 0.022), flat("paint_red"), at=(0, sy * 0.086, 0.300))
    sticker(p, "everyday_flammable", (-0.118, 0.086, 0.235), (0, 1, 0), (-1, 0, 0), (0.066, 0.066), lift=0.0008)
    handle = fillet([(-0.078, 0, 0.360), (-0.078, 0, 0.415), (0.078, 0, 0.415), (0.078, 0, 0.360)], 0.020, 2)
    p.add(tube(handle, 0.015, 8, caps=False), flat("paint_red"), sharp=70)
    p.add(lathe([(0.026, 0.366), (0.026, 0.400), (0.031, 0.404), (0.031, 0.442), (0.026, 0.450)], 8, cap_top=True), flat("gloss_black"), at=(0.126, 0, 0), sharp=40)
    p.add(lathe([(0.011, 0.368), (0.011, 0.385)], 6, cap_top=True), flat("paint_yellow"), at=(-0.122, 0, 0), sharp=50)
    return p


def warning_triangle():
    p = Prop("warning_triangle", "glow", note="reflective red triangle 0.45 m wide, its point 0.42 m up, on a weighted foot 0.05 m deep; it faces +Y and -Y alike")
    def outline(inset, y):
        corners = [Vector((-0.2355, 0.034)), Vector((0.2355, 0.034)), Vector((0.0, 0.4375))]       # before their tips are cut: 0.45 wide, 0.42 high after
        centre = sum(corners, Vector((0, 0))) / 3
        points = []
        for k, c in enumerate(corners):
            before, after = corners[k - 1], corners[(k + 1) % 3]
            pull = (centre - c).normalized() * (inset * 2.0)                       # an inset of the sides moves a corner twice as far
            for other in (before, after):
                q = c + pull + (other - c).normalized() * (0.021 if inset == 0 else 0.008)
                points.append((q.x, y, q.y))
        return points
    for band_, t, paint in (((0.0, 0.052), 0.007, "lens_red"), ((0.052, 0.072), 0.0045, "tent_yellow")):
        rings = [outline(band_[0], t), outline(band_[1], t), outline(band_[1], -t), outline(band_[0], -t), outline(band_[0], t)]
        p.add(solid(loft(rings)), flat(paint), sharp=40)
    p.add(box(0.340, 0.050, 0.030, bevel=0.007), flat("rubber_black"), at=(0, 0, 0.015))
    p.add(box(0.060, 0.024, 0.022), flat("steel_dark"), at=(0, 0, 0.036))
    return p


def car_wash_arch():
    p = Prop("car_wash_arch", "lit", note="roll-over car wash 3.6 m wide, 1.0 m deep, 3.2 m high; driven through along Y: 2.60 m clear between the side brushes, 2.42 m under the top brush; CAR WASH on top, both ways")
    for side in (-1, 1):
        x = side * 1.670
        p.add(box(0.340, 1.000, 0.160, bevel=0.020), "steel_dark", at=(side * 1.630, 0, 0.080))
        p.add(box(0.260, 0.600, 2.700, bevel=0.025), "paint_blue", at=(x, 0, 1.510))
        p.add(box(0.262, 0.500, 1.500), flat("paint_white"), at=(side * 1.668, 0, 1.450))
        hazard = Paint("paint_yellow", grade=False).planar("awning", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.50, 0.25)).planar("awning", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.50, 0.25))
        p.add(box(0.262, 0.606, 0.200), hazard, at=(side * 1.668, 0, 0.360))
        for y in (-0.36, 0.36):                                                    # the carriage's wheels on their rail
            p.add(lathe([(0.070, -0.03), (0.070, 0.03)], 8, cap_top=True, cap_bottom=True), flat("steel_dark"), at=(side * 1.600, y, 0.09), rot=(0, 90, 0), sharp=40)
        p.add(box(0.050, 1.000, 0.030), flat("aluminium"), at=(side * 1.600, 0, 0.015))
        # A side brush: a drum of cloth strips on a shaft hung from an arm off the tower.
        bx = side * 1.420
        brush = lathe([(0.050, 0.26), (0.118, 0.34), (0.108, 0.80), (0.120, 1.25), (0.108, 1.70), (0.118, 2.12), (0.050, 2.20)], 8, cap_top=True, cap_bottom=True)
        p.add(brush, Paint("wet_paint", grade=False).wrap("everyday_wash_brush", 0.26, 2.20), at=(bx, 0, 0), sharp=30)
        rod(p, (bx, 0, 0.20), (bx, 0, 2.34), 0.022, "steel_dark", 6)
        p.add(box(0.260, 0.110, 0.080), flat("steel_dark"), at=(side * 1.500, 0, 2.340))
        p.add(box(0.150, 0.160, 0.140), flat("paint_yellow"), at=(bx, 0, 2.450))
        # The traffic light on the entry face.
        p.add(box(0.130, 0.050, 0.300, bevel=0.012), flat("gloss_black"), at=(x, 0.320, 2.050))
        for z, lens in ((2.120, "lens_red"), (1.980, "plastic_green")):
            p.add(lathe([(0.045, 0.0), (0.040, 0.010), (0.0, 0.014)], 8), flat(lens), at=(x, 0.345, z), rot=(-90, 0, 0), sharp=50)
        p.add(box(0.160, 0.040, 0.260), flat("steel_dark"), at=(x, -0.320, 1.250))           # the control box behind
    p.add(box(3.600, 0.640, 0.150, bevel=0.025), flat("paint_blue"), at=(0, 0, 2.785))
    p.add(box(3.300, 0.560, 0.012), flat("paint_white"), at=(0, 0, 2.704))
    # The top brush, lying across between two hangers.
    top = lathe([(0.050, -1.20), (0.132, -1.12), (0.120, -0.56), (0.134, 0.0), (0.120, 0.56), (0.132, 1.12), (0.050, 1.20)], 8, cap_top=True, cap_bottom=True)
    p.add(top, Paint("wet_paint", grade=False).wrap("everyday_wash_brush", -1.20, 1.20), at=(0, -0.12, 2.560), rot=(0, 90, 0), sharp=30)
    rod(p, (-1.27, -0.12, 2.560), (1.27, -0.12, 2.560), 0.022, "steel_dark", 6)
    for side in (-1, 1):
        p.add(box(0.050, 0.110, 0.200), flat("steel_dark"), at=(side * 1.255, -0.12, 2.630))
    # Spray pipes: an arch of pipe on the entry side with nozzles pointing in, a bar of them on the way out.
    arch = fillet([(-1.340, 0.440, 0.16), (-1.340, 0.440, 2.465), (1.340, 0.440, 2.465), (1.340, 0.440, 0.16)], 0.12, 2)
    p.add(tube(arch, 0.020, 6), flat("aluminium"), sharp=70)
    for z in (0.55, 0.95, 1.35, 1.75, 2.15):
        for side in (-1, 1):
            p.add(lathe([(0.016, 0.0), (0.009, 0.030)], 5, cap_top=True), flat("paint_yellow"), at=(side * 1.340, 0.440, z), rot=(0, -side * 90, 0), sharp=50)
    for x in (-0.9, -0.45, 0.0, 0.45, 0.9):
        p.add(lathe([(0.016, 0.0), (0.009, 0.030)], 5, cap_top=True), flat("paint_yellow"), at=(x, 0.440, 2.465), rot=(180, 0, 0), sharp=50)
    for side in (-1, 1):
        rod(p, (side * 1.340, 0.440, 1.30), (side * 1.545, 0.300, 1.30), 0.012, "aluminium", 4, caps=False)
        rod(p, (side * 1.340, 0.440, 2.30), (side * 1.545, 0.300, 2.30), 0.012, "aluminium", 4, caps=False)
    rod(p, (-1.54, -0.440, 2.50), (1.54, -0.440, 2.50), 0.018, "aluminium", 6)
    for side in (-1, 1):
        rod(p, (side * 1.50, -0.440, 2.50), (side * 1.56, -0.300, 2.50), 0.012, "aluminium", 4, caps=False)
    # The sign on top, lettered both ways, on a frame.
    sign = Paint("paint_blue", grade=False).planar("everyday_wash_sign", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (1.70, 0.34)).planar("everyday_wash_sign", (0, -1, 0), (1, 0, 0), (0, 0, 0), (1.70, 0.34))
    p.add(box(1.70, 0.070, 0.340, bevel=0.010), sign, at=(0, 0, 3.030))
    for x in (-0.875, 0.875):
        p.add(box(0.050, 0.090, 0.340), flat("steel_dark"), at=(x, 0, 3.030))
    for side in (-1, 1):                                                           # a beacon either end of the beam
        p.add(lathe([(0.050, 2.86), (0.042, 2.88), (0.040, 2.935), (0.0, 2.955)], 6), flat("lens_amber"), at=(side * 1.55, 0, 0), sharp=40)
    return p


# ---------------------------------------------------------------------------------------------- careers and business

def dispatch_desk():
    p = Prop("dispatch_desk", "lit", note="dispatcher's standing desk 1.2 x 0.5 m: the sloping top is 1.03 m up at the back and 0.97 m at its front edge (+Y, where the dispatcher stands), the city map on the board behind it rises to 1.40 m and faces +Y")
    slope = math.degrees(math.atan2(0.06, 0.45))
    for side in (-1, 1):
        ends = [[(side * 0.585 + dx, y, z) for y, z in ((-0.215, 0.0), (0.235, 0.0), (0.235, 0.950), (-0.215, 1.010))] for dx in (-0.015, 0.015)]
        p.add(solid(loft(ends, cap_start=True, cap_end=True)), "wood_dark")
    p.add(box(1.140, 0.022, 0.900), "wood_dark", at=(0, -0.204, 0.450))
    p.add(box(1.140, 0.400, 0.022), flat("wood_dark"), at=(0, 0.0, 0.520))
    p.add(box(1.140, 0.030, 0.090), flat("wood_dark"), at=(0, 0.200, 0.060))
    desk = dict(at=(0, 0.014, 1.000), rot=(-slope, 0, 0))

    def on_desk(part, paint, at=(0, 0, 0), rot=(0, 0, 0), **more):                 # made lying on the desk's top (z 0 is its surface), then tipped with it
        p.add(moved(part, at=(at[0], at[1], at[2] + 0.0175), rot=rot), paint, **desk, **more)
    p.add(box(1.200, 0.468, 0.035, bevel=0.006), flat("wood"), **desk)
    on_desk(box(1.200, 0.016, 0.018), flat("wood_dark"), at=(0, 0.226, 0.009))              # the lip that stops the papers
    for x, turn, name in ((-0.30, 6, "clipboard_page"), (-0.06, -4, "clipboard_page")):
        on_desk(grid(lambda u, v: ((u - 0.5) * 0.21, (0.5 - v) * 0.297 * -1, 0.0012), 1, 1), flat("paper").param(name), at=(x, 0.03, 0), rot=(0, 0, turn), sharp=180)
    turned = (math.cos(math.radians(-12)), math.sin(math.radians(-12)), 0)       # on_desk has moved the part before it is painted
    on_desk(box(0.062, 0.135, 0.034, bevel=0.007), flat("plastic_grey").planar("handset_face", (0, 0, 1), turned, (0.34, 0.02, 0), (0.048, 0.132)), at=(0.34, 0.02, 0.017), rot=(0, 0, -12))
    on_desk(tube([(0, 0, 0), (0, 0.075, 0)], [0.006, 0.004], 5), flat("rubber_black"), at=(0.345, 0.085, 0.017), rot=(0, 0, -12), sharp=60)
    on_desk(box(0.120, 0.090, 0.050, bevel=0.008), flat("plastic_grey"), at=(0.50, -0.12, 0.025))        # the base set, its lamp and its mike on a stalk
    on_desk(lathe([(0.007, 0.050), (0.005, 0.056), (0.0, 0.057)], 6), flat("lens_red"), at=(0.535, -0.10, 0), sharp=60)
    stalk = [(0.46, -0.12, 0.050), (0.45, -0.10, 0.130), (0.43, -0.04, 0.170)]
    on_desk(tube(stalk, [0.004, 0.004, 0.009], 4), flat("gloss_black"), sharp=70)
    on_desk(lathe([(0.030, 0.0), (0.034, 0.006), (0.034, 0.080), (0.031, 0.084)], 8, cap_bottom=True), flat("paint_white"), at=(-0.50, -0.10, 0), sharp=40)          # a mug of pens
    for k, tint in enumerate(("paint_red", "paint_blue", "gloss_black")):
        on_desk(tube([(0, 0, 0.02), (0.012 * (k - 1), 0.008 * k, 0.135)], 0.004, 4), flat(tint), at=(-0.50 + 0.008 * (k - 1), -0.10, 0), sharp=70)
    # The wall map on its board, pins in it and a route between four of them.
    p.add(box(1.200, 0.030, 0.800), flat("wood_dark"), at=(0, -0.235, 1.000))
    sticker(p, "map_print", (0, -0.220, 1.005), (0, 1, 0), (-1, 0, 0), (1.10, 0.72), lift=0.001)
    for z in (0.633, 1.377):
        p.add(box(1.150, 0.012, 0.024), flat("aluminium"), at=(0, -0.2155, z))
    for x in (-0.563, 0.563):
        p.add(box(0.024, 0.012, 0.720), flat("aluminium"), at=(x, -0.2155, 1.005))
    pins = ((-0.40, 1.22, "paint_red"), (-0.22, 0.98, "paint_red"), (0.06, 1.10, "paint_red"), (0.31, 0.86, "paint_red"), (0.42, 1.25, "paint_yellow"), (-0.36, 0.80, "paint_blue"),
            (0.14, 0.78, "paint_yellow"), (-0.05, 1.28, "paint_blue"))
    for x, z, tint in pins:
        p.add(lathe([(0.003, 0.0), (0.012, 0.016), (0.0, 0.026)], 5), flat(tint), at=(x, -0.219, z), rot=(-90, 0, 0), sharp=60)
    route = [(x, -0.2165, z) for x, z, _ in pins[:4]]
    p.add(tube(route, 0.0035, 3), flat("paint_red"), sharp=180)
    return p


def employer_sign(name, face):
    def make():
        p = Prop(name, "lit", pivot="the middle of its back, on the wall; it faces +Y",
                 note="office door plaque 0.60 x 0.30 m, 0.03 m proud of the wall: a brass frame, four screws, " + ("a plain blue enamel plate for lettering by code" if face is None else "the employer's name in enamel"))
        outline = lambda w, h, y: [(-w / 2, y, h / 2), (w / 2, y, h / 2), (w / 2, y, -h / 2), (-w / 2, y, -h / 2)]
        frame = loft([outline(0.600, 0.300, 0.0), outline(0.600, 0.300, 0.018), outline(0.580, 0.280, 0.026)])
        p.add(facing_away(frame, (0, 0.01, 0)), flat("gold"), sharp=30)
        plate = flat("paint_blue") if face is None else flat("paint_blue").param(face)
        p.add(grid(lambda u, v: (0.290 - 0.580 * u, 0.026, -0.140 + 0.280 * v), 1, 1), plate, sharp=180)
        for sx in (-1, 1):
            for sz in (-1, 1):
                p.add(lathe([(0.0085, 0.0), (0.0060, 0.004)], 4, cap_top=True), flat("gold"), at=(sx * 0.268, 0.026, sz * 0.118), rot=(-90, 0, 0), sharp=50)
        return p
    return make


def window_boarded():
    p = Prop("window_boarded", "lit", pivot="ground centre of its back face; it faces +Y",
             note="plywood sheet 1.2 x 1.5 m, 0.018 m thick, to stand against a shopfront: two battens screwed across its front (to 0.05 m), a sprayed tag and a note on it")
    front = Paint("wood").planar("everyday_plywood", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (1.20, 1.50))
    p.add(box(1.200, 0.018, 1.500), front, at=(0, 0.009, 0.750))
    for z in (0.320, 0.975):                                                       # clear of the tag and of the note
        p.add(box(1.160, 0.030, 0.090), Paint("wood"), at=(0, 0.033, z))
        for x in (-0.50, 0.0, 0.50):
            p.add(lathe([(0.009, 0.0), (0.006, 0.002)], 4, cap_top=True), flat("steel_dark"), at=(x, 0.048, z), rot=(-90, 0, 0), sharp=50)
    return p


def closed_notice():
    p = Prop("closed_notice", "lit", note="an official A3 notice (0.30 x 0.42 m, CLOSED BY ORDER) in a snap frame on a weighted stand 1.30 m high, leaning 8 degrees back; it faces +Y")
    p.add(lathe([(0.170, 0.0), (0.170, 0.020), (0.060, 0.034), (0.022, 0.040), (0.022, 0.070)], 10), "steel_dark", sharp=40)
    p.add(tube([(0, 0, 0.060), (0, 0, 0.900)], 0.013, 8, caps=False), flat("chrome"), sharp=70)
    tilt = 8.0
    c, s = math.cos(math.radians(tilt)), math.sin(math.radians(tilt))
    centre = Vector((0, -0.012, 1.075))
    turn = lambda q: centre + Vector((q[0], q[1] * c - q[2] * s, q[1] * s + q[2] * c))
    p.add(box(0.336, 0.022, 0.456, bevel=0.006), flat("aluminium"), at=tuple(centre), rot=(tilt, 0, 0))
    p.add(box(0.060, 0.030, 0.080, bevel=0.006), flat("steel_dark"), at=tuple(turn((0, -0.022, -0.150))), rot=(tilt, 0, 0))
    sticker(p, "everyday_closed_notice", tuple(turn((0, 0.011, 0))), (0, c, s), (-1, 0, 0), (0.300, 0.420), lift=0.0012)
    return p


# ---------------------------------------------------------------------------------------------- the list

PROPS = [
    (pan, 290), (plate, 160), (mug, 250), (run_checkpoint, 620),
    (wine_glass, 260), (beer_bottle, 210), (takeaway_box, 220), (popcorn_tub, 260), (gift_box, 290), (flowers_bunch, 580), (gym_bag, 540), (moving_box, 110), (newspaper_ledger, 140),
    (fishing_rod, 860), (fishing_float, 120), (fishing_line, 8), (drink_glass, 240), (cocktail_glass, 230), (clothes_rack, 1900), (shopping_bag, 240),
    (turnstile, 600), (fuel_pump, 860), (jerry_can, 480), (warning_triangle, 160), (car_wash_arch, 1800),
    (dispatch_desk, 700), (employer_sign("employer_sign", None), 64),

] + [(employer_sign("employer_sign_%d" % k, "everyday_employer_%d" % k), 64) for k in range(1, 7)] + [(window_boarded, 96), (closed_notice, 200)]

# Judged beside who uses them (previews/<id>_sides.png).
SIDES = {
    "clothes_rack": {"people": (((0.10, 0.75), 180),)},
    "car_wash_arch": {"references": (((1.85, 4.5, 0.75), (0, 0, 0.55)), ((1.6, 2.2, 0.55), (0, -0.2, 1.2))), "people": (((2.5, 0.9), 180),)},
    "fuel_pump": {"people": (((0.70, 0.30), 90),)},
    "turnstile": {"people": (((0.50, 0.75), 180),)},
    "dispatch_desk": {"people": (((0.0, 0.62), 180),)},
}
