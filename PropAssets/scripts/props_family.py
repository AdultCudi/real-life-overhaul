"""THE FAMILY MODULE: what a proposal, a wedding and a nursery need, and the driving school's markers. Same rules as
props.py: one function each, metres, X right, Y forward, Z up, one mesh on the shared atlas. Unity gets (x, z, y).

The code is the specification (Scripts/Family/FamilyCast.cs FamilyProps.StandIn, Scripts/Licences/LicenceProps.cs):
each prop here has the pivot, the facing and the size of the plain-shape stand-in it replaces (checks_family.py
measures them; the README table states them in Unity axes):

    wedding_arch        2.10 x 0.30 x 2.50, posts at x +-0.95; dressed on +Y (the couple and the guests are at +Y)
    flower_stand        0.40 across, pedestal top z 0.95, flowers to 1.20
    aisle_runner        1.10 x 1.00, lies from z 0 to 0.018, tiles along Y every 1.00
    cot                 0.72 x 1.26 x 0.90; side rails centred x +-0.33, their tops z 0.885 (rock_cot's hands); mattress top z 0.515
    high_chair          0.48 x 0.50 x 1.05 facing +Y; seat top z 0.645, tray rim z 0.815
    driving_school_sign a sandwich board 0.62 x 0.52 x 1.05, a face to +Y and one to -Y, each leaning 12 degrees
    test_checkpoint_beacon  a post on a base 0.22 across, 2.90 high, its flag flying to +X from z 2.60

Not asked for by code yet (their numbers are their own contract):

    ring_box            stands on z 0, lid up at the back (-Y); propose_kneel holds it under the middle of its base
    bouquet             the wrap is a round bar 0.035 thick from z -0.06 to 0.06 about the pivot (a fist closes there)
    baby_bundle         lies along Y, head at +Y, underside on z 0: hold_baby's left palm is under (0, -0.10, 0)
"""
import math

import numpy as np
from mathutils import Vector

from life_props import flat, pipe, rect, ribbon, rod, shifted, smooth, sticker, strut  # noqa: F401
from prop_kit import Paint, Prop, box, grid, lathe, loft, prism, rounded_rect, tube

COT_RAIL = (0.33, 0.885)                             # the middle of a side rail from the cot's centre line, and its top
MATTRESS = 0.515
WRAP = 0.0175                                        # radius of the bouquet's wrapped stems


def bar(p, a, b, radius, paint, sides=3):
    """A lattice member: an open-ended bar from a to b."""
    rod(p, a, b, radius, paint, sides, caps=False)


def fit(p, size, limit=0.06):
    """Brings the finished prop to exactly `size` (x, y, z; None leaves an axis alone) by scaling it a few per cent:
    x and y about their middles, which go to the pivot, z from the ground. More than `limit` is a mistake in the
    model, not something to scale away."""
    for axis, want in enumerate(size):
        if want is None:
            continue
        low, high = min(v.co[axis] for v in p.bm.verts), max(v.co[axis] for v in p.bm.verts)
        k = want / (high - low)
        if abs(k - 1.0) > limit:
            raise SystemExit("%s: %s is %.3f, wanted %.3f" % (p.name, "xyz"[axis], high - low, want))
        start = low if axis == 2 else (low + high) / 2
        for v in p.bm.verts:
            v.co[axis] = (v.co[axis] - start) * k
    return p


# ---------------------------------------------------------------------------------------------- the driving school

def learner_plate():
    p = Prop("learner_plate", "lit", pivot="centre of its back", note="L plate 0.18 x 0.18 x 0.004 m with rounded corners; the red L faces +Y")
    face = Paint("paint_white", grade=False).planar("family_l_plate", (0, 0, 1), (-1, 0, 0), (0, 0, 0), (0.18, 0.18))
    p.add(prism(rounded_rect(0.18, 0.18, 0.022, 2), 0.0, 0.004, bottom=False), face, rot=(-90, 0, 0))
    return p


def driving_school_sign():
    p = Prop("driving_school_sign", "lit",
             note="driving school's sandwich board 0.62 x 0.52 x 1.05 m: two framed boards leaning 12 degrees to a hinge at the top, one read "
                  "from +Y and one from -Y, each with a learner plate (white square, red L) 0.36 m across under DRIVING SCHOOL")
    lean = math.radians(12.0)
    foot, top = 0.238, 1.022                                                             # where a leg stands, and how high its top is
    for side in (-1, 1):
        def along(z, out=0.0, side=side):
            """A point of this board's plane at height z, `out` metres outside it."""
            return Vector((0, side * (foot - z * math.tan(lean) + out * math.cos(lean)), z + out * math.sin(lean)))
        for sx in (-1, 1):
            strut(p, tuple(along(0.012) + Vector((sx * 0.295, 0, 0))), tuple(along(top) + Vector((sx * 0.295, 0, 0))), 0.030, 0.032, "gloss_black")
            p.add(box(0.030, 0.044, 0.012, bevel=0.003), flat("rubber_black"), at=(sx * 0.295, side * foot, 0.006))
        normal, right = (0, side, 0), (-side, 0, 0)
        face = Paint("paint_white", grade=False).planar("family_school_board", normal, right, (0, 0, 0), (0.56, 0.896))
        middle = along(0.555, 0.004)
        p.add(box(0.56, 0.016, 0.896, bevel=0.004), face, at=tuple(middle), rot=(side * 12.0, 0, 0))
        for z in (0.085, top - 0.012):                                                    # the frame's rails under and over the board
            p.add(box(0.56, 0.026, 0.028), flat("gloss_black"), at=tuple(along(z)), rot=(side * 12.0, 0, 0))
    p.add(box(0.62, 0.090, 0.030, bevel=0.008), flat("gloss_black"), at=(0, 0, 1.035))                 # the hinge and carrying bar
    for sx in (-1, 1):                                                                    # the stays that stop it doing the splits
        p.add(box(0.004, 0.28, 0.016), flat("steel_dark"), at=(sx * 0.276, 0, 0.42))
    return fit(p, (0.62, None, 1.05), 0.02)


def test_checkpoint_beacon():
    p = Prop("test_checkpoint_beacon", "glow",
             note="driving-test checkpoint marker 2.90 m high: a teal post with two white bands on a rubber base 0.22 m across, a yellow flag "
                  "0.42 x 0.28 m flying to +X from z 2.60 to 2.88 (so x runs from -0.11 to 0.45); it is not turned by the code")
    p.add(lathe([(0.110, 0.0), (0.110, 0.030), (0.095, 0.055), (0.060, 0.075), (0.052, 0.110)], 12), flat("rubber_black"), sharp=40)
    p.add(lathe([(0.046, 0.10), (0.034, 2.56), (0.020, 2.60)], 8), flat("family_teal"), sharp=40)
    for z0, z1 in ((0.95, 1.13), (1.38, 1.56)):
        r0, r1 = 0.046 - 0.012 * (z0 - 0.10) / 2.46 + 0.003, 0.046 - 0.012 * (z1 - 0.10) / 2.46 + 0.003
        p.add(lathe([(r0 - 0.004, z0 - 0.006), (r0, z0), (r1, z1), (r1 - 0.004, z1 + 0.006)], 8), flat("reflective_white"), sharp=40)
    p.add(lathe([(0.012, 2.58), (0.012, 2.885), (0.0, 2.90)], 6), flat("steel_dark"), sharp=40)        # the staff the flag is on
    p.add(lathe([(0.0, 2.862), (0.020, 2.872), (0.020, 2.888), (0.0, 2.90)], 6), flat("family_teal"), sharp=50)

    def flag(u, v):
        wave = 0.022 * math.sin(u * 2.3 * math.pi) * u ** 0.6
        return (0.012 + 0.438 * u, wave, 2.60 + 0.28 * v - 0.030 * u * (1 - v) + 0.010 * math.sin(u * 2 * math.pi) * u)
    p.add(grid(flag, 6, 2, both_sides=True), flat("tent_yellow"), sharp=180)
    return p


BOX = (2.4, 5.5)                                     # a parking bay, outside of its lines
LINE = 0.10


def parking_box_marker():
    p = Prop("parking_box_marker", "lit",
             note="a parking bay painted on the ground, 2.4 x 5.5 m outside its 0.10 m lines (2.2 x 5.3 clear), lying 5 mm up; a 0.30 m tick "
                  "inwards at the middle of each side")
    w, d = BOX

    def strip(a, b):
        """A line LINE wide from a to b on the ground (its length runs up the paint)."""
        a, b = Vector((*a, 0.005)), Vector((*b, 0.005))
        side = (b - a).normalized().cross(Vector((0, 0, 1))) * LINE / 2
        p.add(grid(lambda u, v: tuple(a + (b - a) * v + side * (2 * u - 1)), 1, 1), flat("paint_white").param("family_line_paint"), sharp=180)
    for sx in (-1, 1):
        x = sx * (w - LINE) / 2
        strip((x, -d / 2), (x, d / 2))
        strip((x - sx * LINE / 2, 0), (x - sx * (LINE / 2 + 0.30), 0))
    for sy in (-1, 1):
        y = sy * (d - LINE) / 2
        strip((-w / 2 + LINE, y), (w / 2 - LINE, y))
        strip((0, y - sy * LINE / 2), (0, y - sy * (LINE / 2 + 0.30)))
    return p


def parking_box_posts():
    p = Prop("parking_box_posts", "glow",
             note="four red and white poles 1.20 m high on rubber bases at the corners of the parking bay: their centres are at (+-1.15, +-2.70), "
                  "the middle of the lines' corners; 2.26 x 5.36 m clear between them")
    w, d = BOX
    for sx in (-1, 1):
        for sy in (-1, 1):
            at = (sx * (w - LINE) / 2, sy * (d - LINE) / 2, 0)
            p.add(lathe([(0.11, 0.0), (0.11, 0.028), (0.055, 0.05), (0.024, 0.056)], 6), flat("rubber_black"), at=at, sharp=40)
            p.add(lathe([(0.020, 0.05), (0.020, 1.185), (0.012, 1.20)], 6, cap_top=True), Paint("reflective_white", grade=False).wrap("family_pole_bands", 0.05, 1.20), at=at, sharp=40)
    return p


# ---------------------------------------------------------------------------------------------- the proposal and the wedding

def ring_box():
    p = Prop("ring_box", "lit", pivot="centre of its base",
             note="open ring box 0.05 x 0.05 x 0.06 m: velvet base 0.018 m high, the lid standing open at the back (-Y), a gold ring with a "
                  "stone upright in the cushion, seen from +Y; held under its base")
    outline = rounded_rect(0.050, 0.050, 0.006, 1)
    p.add(loft([[(x * s, y * s, z) for x, y in outline] for z, s in ((0.0, 0.93), (0.003, 1.0), (0.018, 1.0))], cap_start=True, cap_end=True), flat("plush_red"), sharp=40)
    p.add(prism(rounded_rect(0.042, 0.042, 0.004, 1), 0.018, 0.0196, bottom=False), flat("linen"))
    p.add(box(0.020, 0.0045, 0.0008), flat("gloss_black"), at=(0, 0.002, 0.0196))
    centre, radius = Vector((0, 0.002, 0.0262)), 0.0085
    hoop = [tuple(centre + Vector((math.cos(a), 0, math.sin(a))) * radius) for a in np.linspace(0, 2 * math.pi, 10, endpoint=False)]
    p.add(tube(hoop, 0.0013, 4, closed=True), flat("gold"), sharp=100)
    p.add(lathe([(0.0016, -0.0012), (0.0030, 0.0010)], 6), flat("gold"), at=(0, 0.002, 0.0347), sharp=40)
    p.add(lathe([(0.0030, 0.0010), (0.0037, 0.0026), (0.0022, 0.0043), (0.0, 0.0043)], 6), flat("glass"), at=(0, 0.002, 0.0347), sharp=30)
    p.add(box(0.050, 0.010, 0.042, bevel=0.003), flat("plush_red"), at=(0, -0.020, 0.039))
    p.add(box(0.042, 0.0015, 0.034), flat("linen"), at=(0, -0.0145, 0.039))
    return p


def rose(p, at, tilt, heading, pink, full=True, scale=1.0, sides=6):
    """A rose head whose axis leans `tilt` degrees from upright towards `heading` (0 is +X, 90 is +Y)."""
    profile = [(0.020, 0.0), (0.036, 0.016), (0.031, 0.031), (0.0, 0.036)] if full else [(0.021, 0.0), (0.037, 0.022), (0.0, 0.036)]
    paint = Paint("family_pink" if pink else "linen", grade=False).planar("family_rose_pink" if pink else "family_rose_white", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.076, 0.076), facing=0.6)
    p.add(lathe(profile, sides), paint, at=at, rot=(0, tilt, heading), sharp=50, scale=(scale, scale, scale))


def leaf(p, root, tip, width, paint="plastic_green", sag=0.25):
    """A leaf from root to tip, folded a little along its rib; both its sides show."""
    root, tip = Vector(root), Vector(tip)
    along = tip - root
    across = along.cross(Vector((0, 0, 1)))
    if across.length < 1e-4:
        across = along.cross(Vector((1, 0, 0)))
    across = across.normalized() * width / 2
    lift = across.cross(along).normalized() * width * sag
    if lift.z < 0:
        lift = -lift

    def surface(u, v):
        wide = math.sin(math.pi * min(1.0, 0.10 + v * 0.90)) ** 0.7
        return tuple(root + along * v + across * (2 * u - 1) * wide + lift * (2 * u - 1) * wide - lift * 0.6 * math.sin(math.pi * v))
    p.add(grid(surface, 1, 2, both_sides=True), flat(paint), sharp=180)


def bouquet():
    p = Prop("bouquet", "lit", pivot="the middle of the wrapped stems, 0.10 m above their cut ends",
             note="round bridal posy 0.28 m across, 0.40 m long: thirteen white and pink roses in a dome over a collar of leaves; the stems are "
                  "wrapped in ribbon (a round bar 0.035 m thick from z -0.06 to 0.06, where the fist closes), a bow under the flowers at +Y")
    p.add(lathe([(0.011, -0.10), (0.0145, -0.06)], 8, cap_bottom=True), flat("plastic_green"), sharp=40)
    p.add(lathe([(0.0155, -0.066), (WRAP, -0.060), (WRAP, 0.060), (0.0155, 0.066)], 8), flat("linen"), sharp=40)
    p.add(lathe([(0.015, 0.060), (0.045, 0.105), (0.095, 0.150), (0.108, 0.185), (0.095, 0.225), (0.060, 0.250), (0.0, 0.260)], 8), "paint_green", sharp=60)
    for k in range(9):
        turn = 2 * math.pi * (k + 0.5) / 9
        out = Vector((math.cos(turn), math.sin(turn), 0))
        leaf(p, out * 0.070 + Vector((0, 0, 0.125)), out * 0.144 + Vector((0, 0, 0.140 + 0.012 * (k % 3))), 0.066)
    a, b, middle = 0.083, 0.076, 0.172
    rose(p, (0, 0, middle + b), 0, 0, False, scale=1.4, sides=8)
    for ring, (polar, count, start) in enumerate(((46, 6, 0), (82, 6, 30))):
        for k in range(count):
            heading = start + 360 * k / count
            lean, round_ = math.radians(polar), math.radians(heading)
            at = (a * math.sin(lean) * math.cos(round_), a * math.sin(lean) * math.sin(round_), middle + b * math.cos(lean))
            rose(p, at, polar, heading, (k + ring) % 2 == 0, full=ring == 0, scale=1.4, sides=8 if ring == 0 else 6)
    # The bow, tied under the flowers on the front.
    knot = Vector((0, 0.024, 0.082))
    for side in (-1, 1):
        loop = [knot, knot + Vector((side * 0.040, 0.012, 0.022)), knot + Vector((side * 0.066, 0.016, 0.004)), knot + Vector((side * 0.040, 0.012, -0.016)), knot + Vector((side * 0.004, 0.002, -0.003))]
        p.add(ribbon(loop, lambda q: (0, 1, 0.1), 0.024, 0.0025), flat("family_pink"), sharp=60)
        tail = [knot, knot + Vector((side * 0.018, 0.022, -0.050)), knot + Vector((side * 0.034, 0.028, -0.135))]
        p.add(ribbon(tail, lambda q: (0, 1, 0.2), 0.022, 0.0025), flat("family_pink"), sharp=60)
    p.add(box(0.020, 0.014, 0.018, bevel=0.004), flat("family_pink"), at=tuple(knot + Vector((0, 0.004, 0))))
    return p


ARCH = dict(post=0.95, depth=0.085, spring=1.86, rise=0.525, pole=0.027)
ARCH_SIZE = (2.10, 0.30, 2.50)


def arch_point(turn, y, grow=0.0):
    """A point of the arch's curve: turn 180 is the left springing, 90 the crown, 0 the right."""
    a = math.radians(turn)
    return (math.cos(a) * (ARCH["post"] + grow), y, ARCH["spring"] + math.sin(a) * (ARCH["rise"] + grow))


def wedding_arch():
    p = Prop("wedding_arch", "lit",
             note="wedding arch 2.10 x 0.30 x 2.50 m (the stand-in's): white poles in two arches 0.17 m apart, their posts at x +-0.95; a garland "
                  "with fifteen roses over the +Y crown, a swag and two tied drops on the +Y face (the couple and the guests are at +Y, the "
                  "officiant stands 0.75 m in front of it), a sprig with three roses behind; 1.61 m clear between the drops, 2.03 under the swag")
    post, depth, pole, spring, rise = ARCH["post"], ARCH["depth"], ARCH["pole"], ARCH["spring"], ARCH["rise"]
    for y in (-depth, depth):
        path = [(-post, y, 0.0), (-post, y, 1.0)] + [arch_point(180 - 22.5 * k, y) for k in range(9)] + [(post, y, 1.0), (post, y, 0.0)]
        p.add(tube(path, pole, 6), "paint_white", sharp=70)
    for side in (-1, 1):                                                                  # a diamond trellis in each side, and a foot plate
        p.add(box(0.11, 2 * depth + 0.10, 0.012, bevel=0.003), flat("paint_white"), at=(side * post, 0, 0.006))
        for z in (0.08, 1.82):
            rod(p, (side * post, -depth, z), (side * post, depth, z), 0.014, "paint_white", 5, caps=False)
        for k in range(10):
            z = 0.10 + 2 * depth * k
            bar(p, (side * post, -depth, z), (side * post, depth, z + 2 * depth), 0.009, "paint_white")
            bar(p, (side * post, depth, z), (side * post, -depth, z + 2 * depth), 0.009, "paint_white")
    for turn in (150, 130, 110, 90, 70, 50, 30):
        rod(p, arch_point(turn, -depth), arch_point(turn, depth), 0.014, "paint_white", 5, caps=False)
    # The garland lying on the front pole over the crown, leaves standing out of it, and a row of roses.
    rng = np.random.default_rng(911)
    front, limit, ceiling = depth - 0.012, ARCH_SIZE[1] / 2 - 0.002, ARCH_SIZE[2] - 0.004
    garland = [arch_point(t, front, 0.03) for t in np.linspace(170, 10, 13)]
    p.add(tube(garland, [0.034 + 0.011 * ((k * 7) % 3) for k in range(len(garland))], 5), flat("paint_green"), sharp=70)
    for k in range(22):
        t = 168 - 156 * (k + 0.5) / 22
        root = Vector(arch_point(t, front + 0.01, 0.03))
        out = Vector((math.cos(math.radians(t)), 0, math.sin(math.radians(t))))
        aim = (out * rng.uniform(-0.6, 1.0) + Vector((rng.uniform(-0.6, 0.6), rng.uniform(0.10, 0.35), rng.uniform(-0.5, 0.5)))).normalized()
        reach = min(0.17, (limit - root.y) / max(aim.y, 0.05), (ceiling - 0.03 - root.z) / max(aim.z, 0.05), (ARCH_SIZE[0] / 2 - 0.02 - abs(root.x)) / max(abs(aim.x), 0.05))
        leaf(p, root, root + aim * reach, 0.085, sag=0.2)
    for k in range(15):
        t = 160 - 140 * k / 14
        full = k % 3 != 1
        at = arch_point(t, front - 0.012, 0.015 + (0.0 if k % 2 else 0.022))
        rose(p, at, 50 + 30 * abs(math.cos(math.radians(t))), 90 + (t - 90) * 0.6, k % 2 == 1, full=full, scale=1.75 if full else 1.6, sides=7 if full else 6)
    # The drapes: a swag under the crown and a drop down each front pole, tied back half way.
    cloth = flat("linen").param("sheet_cloth")

    def swag(u, v):
        x = (u - 0.5) * 1.76
        top = spring + rise * math.sqrt(max(0.0, 1 - (x / post) ** 2)) - 0.035
        hang = 0.07 + 0.25 * (1 - (x / 0.88) ** 2)
        return (x, depth + 0.036 + 0.012 * math.sin(u * 9 * math.pi) * v, top - hang * v)
    p.add(grid(swag, 8, 2, both_sides=True), cloth, sharp=180)
    for side in (-1, 1):
        def drop(u, v, side=side):
            z = spring + 0.13 - (spring + 0.10) * v
            tie = abs(z - 1.12)
            wide = 0.10 + 0.14 * min(1.0, tie / 0.75) ** 0.8
            return (side * (1.045 - wide * u), depth + 0.040 + 0.012 * math.cos(u * 3 * math.pi) * min(1.0, tie * 3), z)
        p.add(grid(drop, 3, 6, both_sides=True), cloth, sharp=180)
        p.add(tube([(side * 0.935, depth + 0.02, 1.12), (side * 0.99, depth + 0.045, 1.12), (side * 1.036, depth + 0.02, 1.12)], 0.013, 5), flat("family_pink"), sharp=70)
        rose(p, (side * 0.985, depth + 0.012, 1.13), 76, 90 - side * 14, False, full=False, scale=1.3)
        rose(p, (side * 0.95, depth + 0.012, 1.215), 70, 90 + side * 12, True, full=False, scale=1.2)
    # Behind: a sprig at the crown, so the arch is not bare from the other side.
    behind = -front
    sprig = [arch_point(t, behind, 0.03) for t in np.linspace(132, 48, 6)]
    p.add(tube(sprig, [0.026, 0.038, 0.044, 0.044, 0.038, 0.026], 5), flat("paint_green"), sharp=70)
    for k in range(8):
        t = 130 - 80 * (k + 0.5) / 8
        root = Vector(arch_point(t, behind - 0.01, 0.03))
        aim = Vector((rng.uniform(-0.6, 0.6), -rng.uniform(0.10, 0.3), rng.uniform(-0.3, 0.8))).normalized()
        leaf(p, root, root + aim * min(0.15, (limit + root.y) / max(-aim.y, 0.05), (ceiling - 0.03 - root.z) / max(aim.z, 0.05)), 0.085, sag=0.2)
    for t, pink in ((90, True), (72, False), (108, False)):
        rose(p, arch_point(t, -depth + 0.004, 0.03), 52, -90 - (t - 90) * 0.6, pink, full=False, scale=1.5)
    return fit(p, ARCH_SIZE)


def flower_stand():
    p = Prop("flower_stand", "lit",
             note="a pedestal with an arrangement, 0.40 m across and 1.20 m high: an ivory column on a square plinth with its top 0.95 m up "
                  "(0.26 m square), a brass bowl, and a dome of thirteen white and pink roses over a collar of leaves; the same all round")
    stone = "paper"
    p.add(box(0.26, 0.26, 0.055, bevel=0.010), stone, at=(0, 0, 0.0275))
    p.add(lathe([(0.118, 0.055), (0.118, 0.075), (0.100, 0.095), (0.104, 0.110), (0.088, 0.135), (0.074, 0.80), (0.080, 0.815), (0.080, 0.835), (0.100, 0.870),
                 (0.118, 0.885), (0.118, 0.905)], 10), stone, sharp=35)
    p.add(box(0.26, 0.26, 0.045, bevel=0.008), flat(stone), at=(0, 0, 0.9275))
    p.add(lathe([(0.060, 0.950), (0.075, 0.956), (0.130, 0.990), (0.140, 1.010), (0.132, 1.014)], 10), flat("gold"), sharp=40)
    p.add(lathe([(0.132, 1.012), (0.150, 1.040), (0.150, 1.085), (0.120, 1.130), (0.060, 1.158), (0.0, 1.165)], 8), "paint_green", sharp=60)
    for k in range(12):
        turn = 2 * math.pi * k / 12
        out = Vector((math.cos(turn), math.sin(turn), 0))
        leaf(p, out * 0.105 + Vector((0, 0, 1.020)), out * 0.198 + Vector((0, 0, 1.000 + 0.022 * (k % 3))), 0.085)
    a, b, middle = 0.118, 0.092, 1.050
    rose(p, (0, 0, middle + b), 0, 0, False, scale=1.6, sides=8)
    for ring, (polar, count, start) in enumerate(((46, 6, 0), (84, 6, 30))):
        for k in range(count):
            heading = start + 360 * k / count
            lean, round_ = math.radians(polar), math.radians(heading)
            at = (a * math.sin(lean) * math.cos(round_), a * math.sin(lean) * math.sin(round_), middle + b * math.cos(lean))
            rose(p, at, polar, heading, (k + ring) % 2 == 0, full=ring == 0, scale=1.6, sides=7 if ring == 0 else 6)
    return fit(p, (0.40, 0.40, 1.20))


def aisle_runner():
    p = Prop("aisle_runner", "lit",
             note="one metre of an ivory aisle runner 1.10 m wide: lies from the ground to 0.018 m, hemmed along both sides with a gold line "
                  "inside each hem, a few petals on it; open at both ends (y +-0.50), where the next one carries on")
    across = ((-0.550, 0.0), (-0.547, 0.011), (-0.538, 0.0165), (-0.520, 0.013), (-0.26, 0.0125), (0.0, 0.0135), (0.26, 0.0125), (0.520, 0.013), (0.538, 0.0165), (0.547, 0.011), (0.550, 0.0))

    def cloth(u, v):
        k = min(len(across) - 2, int(u * (len(across) - 1) + 1e-9))
        f = u * (len(across) - 1) - k
        x = across[k][0] * (1 - f) + across[k + 1][0] * f
        z = across[k][1] * (1 - f) + across[k + 1][1] * f
        ripple = 0.0015 * math.sin(2 * math.pi * v) * math.sin(math.pi * u) * math.cos(3 * math.pi * u) if 0.0 < v < 1.0 else 0.0
        return (x, -0.5 + v, z + (ripple if z > 0.005 else 0.0))
    p.add(grid(cloth, len(across) - 1, 4), Paint("linen", grade=False).planar("family_runner", (0, 0, 1), (1, 0, 0), (0, 0, 0), (1.10, 1.00), facing=0.3), sharp=60)
    return fit(p, (1.10, 1.00, 0.018), 0.12)


# ---------------------------------------------------------------------------------------------- the nursery

def cot():
    rail_x, rail_top = COT_RAIL
    p = Prop("cot", "lit",
             note="baby's cot 0.72 x 1.26 x 0.90 m in beech, its length along Y: corner posts at (+-0.335, +-0.605); the side rails' tops are "
                  "0.885 m up, centred x +-0.33 (0.04 wide, 0.05 deep), thirteen bars a side; mattress top 0.515 m up, 0.60 x 1.12; a blanket "
                  "over the -Y half, a pillow at +Y; on four castors")
    wood = flat("wood")
    for sx in (-1, 1):
        for sy in (-1, 1):
            at = (sx * 0.335, sy * 0.605)
            p.add(box(0.05, 0.05, 0.845, bevel=0.007), "wood", at=(*at, 0.4775))
            p.add(lathe([(0.0, 0.0), (0.024, 0.010), (0.024, 0.040), (0.011, 0.060)], 6), flat("plastic_grey"), at=(*at, 0), sharp=50)
        p.add(box(0.04, 1.16, 0.05, bevel=0.010), wood, at=(sx * rail_x, 0, rail_top - 0.025))
        p.add(box(0.03, 1.16, 0.04), wood, at=(sx * rail_x, 0, 0.42))
        for k in range(13):
            y = -0.51 + 0.085 * k
            rod(p, (sx * rail_x, y, 0.44), (sx * rail_x, y, rail_top - 0.05), 0.009, wood, 5, caps=False)
    board = [(-0.31, 0.36), (0.31, 0.36), (0.31, 0.845), (0.16, 0.884), (0.0, 0.898), (-0.16, 0.884), (-0.31, 0.845)]
    for sy in (-1, 1):
        p.add(loft([[(x, sy * 0.605 + dy, z) for x, z in board] for dy in (-0.011, 0.011)], cap_start=True, cap_end=True), wood)
    p.add(box(0.64, 1.19, 0.03), wood, at=(0, 0, 0.41))
    p.add(box(0.60, 1.12, 0.09, bevel=0.016), flat("family_baby_blue"), at=(0, 0, MATTRESS - 0.045))

    def blanket(u, v):
        edge = math.sin(math.pi * u) ** 0.5
        fold = 0.012 * math.sin(v * 5.0 + u * 7.0) * edge + 0.020 * edge * math.sin(math.pi * v) ** 0.5
        return ((u - 0.5) * 0.585, -0.550 + 0.69 * v, MATTRESS + 0.004 + fold + (0.016 * edge if v > 0.99 else 0.0))
    p.add(grid(blanket, 6, 6), Paint("linen").param("blanket_cloth"), sharp=180)
    rings = [[(x * s, 0.40 + y * s, MATTRESS + h) for x, y in rounded_rect(0.34, 0.22, 0.05, 1)] for s, h in ((0.86, 0.002), (1.0, 0.022), (0.90, 0.050), (0.45, 0.062))]
    p.add(loft(rings, cap_end=True), flat("linen"), sharp=60)
    return fit(p, (0.72, 1.26, 0.90), 0.015)


def high_chair():
    p = Prop("high_chair", "lit",
             note="child's high chair 0.48 x 0.50 x 1.05 m facing +Y: splayed beech legs (feet at (+-0.22, +-0.23)), a padded seat 0.645 m up, a "
                  "padded back to 1.05 m, a white tray 0.815 m up reaching y 0.25, a foot rest 0.34 m up, a three-point harness")
    seat = 0.595                                                                         # the underside of the cushion's board
    cushion = flat("family_baby_blue")

    def leg(sx, sy, z):
        k = z / 0.60
        return Vector((sx * (0.2225 - 0.0725 * k), sy * (0.23 - 0.09 * k), z))
    for sx in (-1, 1):
        for sy in (-1, 1):
            strut(p, tuple(leg(sx, sy, 0.0)), tuple(leg(sx, sy, 0.60)), 0.035, 0.040, "wood")
        rod(p, tuple(leg(sx, -1, 0.22)), tuple(leg(sx, 1, 0.22)), 0.012, "wood", 6)
        p.add(box(0.030, 0.27, 0.030), flat("wood"), at=(sx * 0.17, -0.035, 0.845))
        p.add(box(0.030, 0.030, 0.215), flat("wood"), at=(sx * 0.17, 0.085, 0.7375))
    rod(p, tuple(leg(-1, -1, 0.22)), tuple(leg(1, -1, 0.22)), 0.012, "wood", 6)
    p.add(box(0.40, 0.10, 0.022), flat("wood"), at=(0, leg(1, 1, 0.34).y, 0.34))
    p.add(box(0.34, 0.30, 0.035, bevel=0.008), flat("wood"), at=(0, 0, seat + 0.0175))
    p.add(box(0.29, 0.26, 0.030, bevel=0.012), cushion, at=(0, 0.005, seat + 0.035))
    p.add(box(0.34, 0.028, 0.455, bevel=0.008), flat("wood"), at=(0, -0.172, 0.822), rot=(8, 0, 0))
    p.add(box(0.27, 0.024, 0.33, bevel=0.010), cushion, at=(0, -0.153, 0.805), rot=(8, 0, 0))
    shell, liner = __import__("life_props").cup(shifted(rounded_rect(0.44, 0.24, 0.055, 1), dy=0.13), shifted(rounded_rect(0.41, 0.21, 0.042, 1), dy=0.13), 0.787, 0.815, 0.797)
    p.add(shell, flat("paint_white"), sharp=40)
    p.add(liner, flat("paper"), sharp=40)
    p.add(box(0.030, 0.022, 0.20), flat("plastic_grey"), at=(0, 0.118, 0.69))                    # the post between the legs, under the tray
    for sx in (-1, 1):
        strap = smooth([(sx * 0.078, -0.180, 0.975), (sx * 0.072, -0.135, 0.80), (sx * 0.040, -0.055, 0.662), (0.0, 0.03, 0.652)], 2)
        p.add(ribbon(strap, lambda q: (0, 1, 0.6), 0.024, 0.004), flat("nylon_black"), sharp=60)
    p.add(ribbon([(0, 0.03, 0.652), (0, 0.108, 0.646)], lambda q: (0, 0, 1), 0.024, 0.004), flat("nylon_black"), sharp=60)
    p.add(box(0.040, 0.032, 0.012), flat("paint_red"), at=(0, 0.03, 0.657))
    return fit(p, (0.48, 0.50, 1.05), 0.03)


def birthday_cake():
    p = Prop("birthday_cake", "lit", pivot="centre of the base",
             note="iced cake 0.20 m across and 0.088 m high on a silver board 0.26 m across, piped round top and bottom, five candles alight "
                  "(flames to 0.16 m); HAPPY BIRTHDAY on top reads from +Y")
    p.add(lathe([(0.13, 0.0), (0.13, 0.006)], 12, cap_top=True), flat("stainless"), sharp=40)
    icing = Paint("paper", grade=False).wrap("family_cake_side", 0.006, 0.088).planar("family_cake_top", (0, 0, 1), (-1, 0, 0), (0, 0, 0), (0.196, 0.196))
    p.add(lathe([(0.098, 0.006), (0.100, 0.014), (0.100, 0.080), (0.096, 0.088)], 12, cap_top=True), icing, sharp=40)
    for radius, z, count, wobble in ((0.089, 0.091, 14, 0.004), (0.103, 0.013, 12, 0.0)):
        ring = [(math.cos(a) * (radius + wobble * (k % 2)), math.sin(a) * (radius + wobble * (k % 2)), z + wobble * (k % 2)) for k, a in enumerate(np.linspace(0, 2 * math.pi, count, endpoint=False))]
        p.add(tube(ring, 0.0075, 3, closed=True), flat("family_pink"), sharp=100)
    for k, paint in enumerate(("paint_blue", "paint_yellow", "paint_red", "plastic_green", "family_pink")):
        a = 2 * math.pi * (k + 0.25) / 5
        x, y = math.cos(a) * 0.058, math.sin(a) * 0.058
        p.add(tube([(x, y, 0.086), (x, y, 0.134)], 0.0036, 4), flat(paint), sharp=70)
        p.add(lathe([(0.0008, 0.134), (0.0052, 0.143), (0.0034, 0.151), (0.0, 0.160)], 4), flat("flame"), at=(x, y, 0), sharp=40)
    return p


def baby_bundle():
    p = Prop("baby_bundle", "lit", pivot="the middle of its underside",
             note="a swaddled baby 0.22 x 0.50 x 0.18 m lying along Y on its back, head (in a pink hat) at +Y: carried in the crook of the left "
                  "arm with the left palm under (0, -0.10, 0), fingers towards the feet")
    body = ((-0.250, 0.030, 0.045), (-0.225, 0.062, 0.092), (-0.130, 0.086, 0.132), (-0.010, 0.104, 0.172), (0.065, 0.110, 0.180), (0.115, 0.094, 0.150), (0.150, 0.060, 0.116))

    def skin(y, a, out=0.0):
        w = float(np.interp(y, [r[0] for r in body], [r[1] for r in body])) + out
        h = float(np.interp(y, [r[0] for r in body], [r[2] for r in body]))
        return (math.cos(a) * w, y, h / 2 + math.sin(a) * (h / 2 + out))
    rings = [[skin(y, a) for a in np.linspace(0, 2 * math.pi, 10, endpoint=False)] for y, _, _ in body]
    p.add(loft(rings, cap_start=True, cap_end=True), flat("linen"), sharp=60)
    fold = [skin(y, a, 0.004) for y, a in zip(np.linspace(0.125, -0.150, 7), np.radians(np.linspace(168, 8, 7)))]      # the blanket's edge, shoulder to hip
    p.add(ribbon(fold, lambda q: (q.x, 0.0, q.z - 0.075), 0.036, 0.005), flat("family_pink"), sharp=70)
    head, r = (0.0, 0.180, 0.092), 0.062
    collar = [(math.cos(a) * 0.066, 0.140, 0.074 + math.sin(a) * 0.058) for a in np.linspace(0, 2 * math.pi, 8, endpoint=False)]
    p.add(tube(collar, 0.015, 4, closed=True), flat("linen"), sharp=100)
    p.add(lathe([(0.0, -r), (0.70 * r, -0.72 * r), (r, 0.0), (0.72 * r, 0.70 * r), (0.0, r)], 8), flat("family_skin"), at=head, sharp=70)
    p.add(box(0.013, 0.014, 0.012, bevel=0.004), flat("family_skin"), at=(0, head[1] - 0.008, head[2] + r))
    for side in (-1, 1):                                                                 # eyes shut
        p.add(box(0.014, 0.003, 0.003), flat("wood_dark"), at=(side * 0.022, head[1] + 0.010, head[2] + r * 0.925), rot=(0, side * 18, 0))
    cap = 0.068
    p.add(lathe([(cap * 0.98, -0.010), (cap * 1.07, -0.002), (cap * 1.0, 0.008), (cap * 0.93, 0.030), (cap * 0.62, 0.056), (0.0, cap * 1.02)], 8), flat("family_pink"), at=head, rot=(-118, 0, 0), sharp=60)
    return p


PROPS = [
    (wedding_arch, 1950), (flower_stand, 900), (aisle_runner, 90), (cot, 950), (high_chair, 600),
    (driving_school_sign, 440), (test_checkpoint_beacon, 320),
    # Not asked for by code yet.
    (learner_plate, 36), (parking_box_marker, 16), (parking_box_posts, 260), (ring_box, 320), (bouquet, 800), (birthday_cake, 440), (baby_bundle, 450),
]

SIDES = {
    "wedding_arch": {"people": (((0.0, 0.75), 0), ((-0.55, 2.0), 90), ((0.55, 2.0), -90))},
    "flower_stand": {"people": (((0.65, 0.0), 0),)},
    "cot": {"people": (((0.68, 0.0), -90),)},
    "high_chair": {"people": (((0.0, 0.75), 180),)},
    "driving_school_sign": {"references": (((3.0, 0.2, 3.0), (0, -0.65, 1.5)),), "people": (((0.9, 0.3), 0),)},
    "test_checkpoint_beacon": {"references": (((1.8, 4.4, 1.45), (-2.4, 0.0, 0.725)),), "people": (((0.9, 0.0), 0),)},
}
