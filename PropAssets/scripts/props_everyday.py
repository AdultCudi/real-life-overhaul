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

from life_props import flat, pipe, rect, ribbon, rod, sticker
from prop_kit import Paint, Prop, box, fillet, grid, lathe, loft, prism, rounded_rect, tube


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


# ---------------------------------------------------------------------------------------------- careers and business


# ---------------------------------------------------------------------------------------------- the list

PROPS = [
    (pan, 290), (plate, 160), (mug, 250), (run_checkpoint, 620),
    (wine_glass, 260), (takeaway_box, 220), (moving_box, 110), (newspaper_ledger, 140),
    (fishing_rod, 860), (fishing_float, 120), (fishing_line, 8), (drink_glass, 240), (clothes_rack, 1900),
    (turnstile, 600), (jerry_can, 480),
]

# Judged beside who uses them (previews/<id>_sides.png).
SIDES = {
    "clothes_rack": {"people": (((0.10, 0.75), 180),)},
    "turnstile": {"people": (((0.50, 0.75), 180),)},
}
