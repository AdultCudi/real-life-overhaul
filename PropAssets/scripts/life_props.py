"""THE LIFE PROPS: what people hold, carry, push, wear and stand at in the second round of city life. Same rules as
props.py (which lists these in PROPS): one function each, metres, X right, Y forward, Z up, one mesh on the shared
atlas. Unity gets (x, z, y).

THE GRIP NUMBERS ARE A CONTRACT with the clips of CityAssets/scripts/clips.py: where a note says "held at", the
clip puts the middle of a palm at exactly that point, and a handle there is 0.025 to 0.035 m thick so the fingers
close round it. Change one and the other must change with it. scripts/check_life.py measures them on the built
meshes.
"""
import math

import numpy as np
from mathutils import Vector

from prop_kit import Paint, Prop, box, fillet, grid, lathe, loft, moved, prism, rounded_rect, tube

LEAN = 30.0                                    # degrees from upright at which a mop or rake lies flat on the ground
PIVOT_CHEST = 1.30                             # the satchel's pivot: the upper-chest bone of a standing figure


# ---------------------------------------------------------------------------------------------- helpers

def flat(name):
    return Paint(name, grade=False)


def rod(p, a, b, radius, paint, sides=6, caps=True):
    """A straight round bar from a to b, shaded round."""
    p.add(tube([a, b], radius, sides, caps=caps), flat(paint) if isinstance(paint, str) else paint, sharp=70)


def pipe(p, points, radius, paint, sides=6, bend=0.0, closed=False, caps=True):
    """A round bar along a path, its corners bent to `bend`."""
    path = fillet(points, bend, 2) if bend > 0 else points
    p.add(tube(path, radius, sides, closed=closed, caps=caps), flat(paint) if isinstance(paint, str) else paint, sharp=70)


def sticker(p, name, centre, normal, right, size, lift=0.0015, nu=1, nv=1, both=False):
    """A printed face laid on a surface: the whole region, `size` metres, looking along `normal`; `right` is where
    the print's left-to-right runs, seen from outside."""
    n, r = Vector(normal).normalized(), Vector(right).normalized()
    up = n.cross(r)
    c = Vector(centre) + n * lift
    p.add(grid(lambda u, v: tuple(c + r * ((u - 0.5) * size[0]) + up * ((v - 0.5) * size[1])), nu, nv, both_sides=both), flat("paper").param(name), sharp=180)


def rect(width, depth, centre=(0.0, 0.0)):
    """A rectangle's corners, anticlockwise from above."""
    return [(centre[0] + sx * width / 2, centre[1] + sy * depth / 2) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]


def shifted(outline, dx=0.0, dy=0.0):
    return [(x + dx, y + dy) for x, y in outline]


def inset(outline, distance):
    """An anticlockwise outline drawn in by `distance` all round."""
    count, out = len(outline), []
    for k in range(count):
        before, here, after = Vector(outline[k - 1]), Vector(outline[k]), Vector(outline[(k + 1) % count])
        a, b = (here - before).normalized(), (after - here).normalized()
        na, nb = Vector((-a.y, a.x)), Vector((-b.y, b.x))
        mid = (na + nb).normalized()
        out.append(tuple(here + mid * (distance / max(0.4, mid.dot(na)))))
    return out


def cup(outer, inner, z0, z1, floor, top=None):
    """An open box or tray from two anticlockwise outlines (and optionally a different outer outline at the rim):
    the shell (outside, underside and rim) and the liner (inside walls and floor), so that they can be painted
    apart."""
    top = top or outer
    shell = loft([[(x, y, z0) for x, y in outer], [(x, y, z1) for x, y in top], [(x, y, z1) for x, y in inner]], cap_start=True)
    liner = loft([[(x, y, z1) for x, y in inner], [(x, y, floor) for x, y in inner]], cap_end=True)
    return shell, liner


def smooth(points, steps=4):
    """A curve through the points (Catmull-Rom), `steps` pieces to each span."""
    pts = [Vector(p) for p in points]
    pts = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for k in range(steps):
            t = k / steps
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-2])
    return out


def ribbon(points, away, width, thickness):
    """A flat strap along a path. away(point) is the direction its outer face looks in there."""
    pts = [Vector(p) for p in points]
    rings = []
    for i, point in enumerate(pts):
        tangent = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        out = Vector(away(point))
        out = (out - tangent * out.dot(tangent)).normalized()
        across = tangent.cross(out)
        rings.append([tuple(point + across * (sa * width / 2) + out * (so * thickness / 2)) for sa, so in ((1, 1), (-1, 1), (-1, -1), (1, -1))])
    return loft(rings, cap_start=True, cap_end=True)


def wheel(p, at, radius, width, segments=12):
    """A tyre and its painted disc, turning about X."""
    part = lathe([(radius * 0.86, -width / 2), (radius, -width * 0.28), (radius, width * 0.28), (radius * 0.86, width / 2)], segments, cap_top=True, cap_bottom=True)
    size = (radius * 2, radius * 2)
    faces = flat("rubber_black").planar("wheel_side", (0, 0, 1), (1, 0, 0), (0, 0, 0), size).planar("wheel_side", (0, 0, -1), (1, 0, 0), (0, 0, 0), size)
    p.add(part, faces, at=at, rot=(0, 90, 0), sharp=35)


def spoked_wheel(p, at, radius, spokes=8):
    """A cart wheel one can see through: tyre, rim, spokes and hub, turning about Y."""
    x, y, z = at
    ring = [(x + math.cos(a) * (radius - 0.022), y, z + math.sin(a) * (radius - 0.022)) for a in np.linspace(0, 2 * math.pi, 16, endpoint=False)]
    p.add(tube(ring, 0.022, 6, closed=True), flat("rubber_black"), sharp=70)
    ring = [(x + math.cos(a) * (radius - 0.046), y, z + math.sin(a) * (radius - 0.046)) for a in np.linspace(0, 2 * math.pi, 16, endpoint=False)]
    p.add(tube(ring, 0.011, 4, closed=True), flat("chrome"), sharp=100)
    for k in range(spokes):
        a = 2 * math.pi * k / spokes
        rod(p, (x + math.cos(a) * 0.03, y, z + math.sin(a) * 0.03), (x + math.cos(a) * (radius - 0.05), y, z + math.sin(a) * (radius - 0.05)), 0.006, "chrome", 4, caps=False)
    p.add(lathe([(0.0, -0.035), (0.03, -0.03), (0.045, 0.0), (0.03, 0.03), (0.0, 0.035)], 8), flat("steel_dark"), at=at, rot=(90, 0, 0), sharp=70)


def strut(p, a, b, width, depth, paint):
    """A square-section leg between two points, its ends cut level (so a foot sits flat on the ground). width is
    along X, depth along Y."""
    rings = [[(c[0] + sx * width / 2, c[1] + sy * depth / 2, c[2]) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for c in (a, b)]
    p.add(loft(rings, cap_start=True, cap_end=True), paint)


def canopy(u, v, radius, tip, rim, ribs=8, dome=1.8, slack=0.4, scallop=0.05):
    """Cloth over ribs: straight from rib to rib, hanging slacker and pulled in between them, so that the panels
    are hollow and the edge is scalloped. u goes round, v from the tip to the rim."""
    k = (u * ribs) % 1.0
    between = 1.0 - abs(2.0 * k - 1.0)
    chord = math.cos(math.pi / ribs) / math.cos((k - 0.5) * 2 * math.pi / ribs)
    r = radius * v * chord * (1.0 - scallop * between * v * v)
    drop = (1.0 - slack * between) * v ** dome + slack * between * v
    return (math.cos(2 * math.pi * u) * r, math.sin(2 * math.pi * u) * r, tip - (tip - rim) * drop)


# ---------------------------------------------------------------------------------------------- hand tools

def mop():
    p = Prop("mop", "lit", pivot="middle of the mop head on the ground; the shaft is Z",
             note="1.45 m; held at 0.95 m (right) and 1.28 m (left); the head, 0.30 m wide, lies flat when the shaft leans %d degrees back (-Y)" % LEAN)

    def lying(part, paint, at=(0, 0, 0), **more):       # made lying on the ground, then turned to the shaft
        p.add(moved(part, at=at), paint, rot=(-LEAN, 0, 0), **more)
    rings = []
    for x, d, h in ((-0.150, 0.050, 0.010), (-0.142, 0.074, 0.022), (-0.075, 0.086, 0.032), (0.0, 0.078, 0.036), (0.075, 0.090, 0.032), (0.142, 0.070, 0.022), (0.150, 0.048, 0.010)):
        rings.append([(x, y, z) for y, z in ((-d, 0.004), (-d * 0.6, 0.0), (d * 0.6, 0.0), (d, 0.004), (d * 1.02, h * 0.55), (d * 0.5, h), (-d * 0.5, h), (-d * 1.02, h * 0.55))])
    strings = Paint("linen").planar("mop_strings", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.30, 0.19), facing=0.25)
    lying(loft(rings, cap_start=True, cap_end=True), strings, sharp=70)
    lying(box(0.20, 0.046, 0.020, bevel=0.005), flat("paint_blue"), at=(0, -0.023, 0.040))
    p.add(lathe([(0.021, 0.036), (0.019, 0.085), (0.0155, 0.10), (0.0155, 0.125)], 8), flat("paint_blue"), sharp=40)
    p.add(tube([(0, 0, 0.11), (0, 0, 1.40)], 0.014, 8, caps=False), flat("aluminium"))
    p.add(lathe([(0.0162, 1.24), (0.0162, 1.435), (0.012, 1.45)], 8, cap_top=True, cap_bottom=True), flat("rubber_black"), sharp=40)
    return p


def rake():
    p = Prop("rake", "lit", pivot="middle of the tines' edge on the ground; the shaft is Z",
             note="leaf rake, 1.55 m; held at 0.95 m (right) and 1.30 m (left); the tips of the tines lie on the ground when the shaft leans %d degrees back (-Y)" % LEAN)
    count = 13
    for k in range(count):
        f = 2.0 * k / (count - 1) - 1.0
        tine = [(f * 0.030, 0.008, 0.430), (f * 0.130, 0.020, 0.215), (f * 0.218, 0.030, 0.040), (f * 0.225, 0.022, 0.012), (f * 0.225, 0.0, 0.0)]
        p.add(ribbon(tine, lambda q: (0, 1, 0.25 if q.z > 0.03 else 2.0), 0.011, 0.004), Paint("plastic_green"), sharp=100)
    brace = [(f * 0.150, 0.027, 0.185 + 0.014 * f * f) for f in np.linspace(-1, 1, 7)]
    p.add(ribbon(brace, lambda q: (0, 1, 0), 0.016, 0.006), flat("paint_yellow"), sharp=100)
    p.add(loft([[(sx * w, 0.008 + sy * 0.009, z) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for w, z in ((0.046, 0.395), (0.050, 0.41), (0.026, 0.50))],
               cap_start=True, cap_end=True), flat("plastic_green"))
    p.add(lathe([(0.020, 0.46), (0.018, 0.56), (0.0145, 0.575)], 8), flat("plastic_green"), sharp=40)
    p.add(tube([(0, 0, 0.47), (0, 0, 1.54)], 0.0135, 8), flat("wood"))
    p.add(lathe([(0.0135, 1.535), (0.011, 1.547), (0.0, 1.55)], 8), flat("wood"), sharp=60)
    return p


def paint_roller():
    p = Prop("paint_roller", "lit", pivot="end of the handle; the handle is Z",
             note="held at 0.07 m (handle 0.14 m long, 0.032 m thick); the roller, 0.23 m wide along X, is centred 0.40 m up; it works on its +Y side")
    p.add(lathe([(0.011, 0.0), (0.0155, 0.006), (0.016, 0.03), (0.016, 0.10), (0.014, 0.124), (0.0085, 0.14)], 8, cap_bottom=True, cap_top=True), flat("paint_red"), sharp=40)
    pipe(p, [(0, 0, 0.135), (0, 0, 0.245), (0.150, 0, 0.305), (0.150, 0, 0.40), (0.118, 0, 0.40)], 0.0042, "chrome", 6, bend=0.035)
    for x0, x1, paint, ends in ((-0.115, -0.080, "linen", (True, False)), (-0.080, 0.080, "wet_paint", (False, False)), (0.080, 0.115, "linen", (False, True))):
        profile = [(0.045, x0), (0.045, x1)]
        if ends[0]:
            profile = [(0.041, x0), (0.045, x0 + 0.005), (0.045, x1)]
        if ends[1]:
            profile = [(0.045, x0), (0.045, x1 - 0.005), (0.041, x1)]
        p.add(lathe(profile, 12), flat(paint), at=(0, 0, 0.40), rot=(0, 90, 0), sharp=40)
    for x, cap in ((-0.115, dict(cap_bottom=True)), (0.115, dict(cap_top=True))):
        p.add(lathe([(0.041, x), (0.041, x + 0.0001)], 12, **cap), flat("plastic_grey"), at=(0, 0, 0.40), rot=(0, 90, 0), sharp=40)
    return p


def watering_can():
    p = Prop("watering_can", "lit", note="spout and rose towards +Y; carried by the bar over the top: it runs along Y, centred (0, -0.02, 0.34), 0.03 m thick, 0.11 m of it straight")
    count = 14
    rings = [[(math.cos(2 * math.pi * k / count) * a, math.sin(2 * math.pi * k / count) * b, z) for k in range(count)]
             for a, b, z in ((0.088, 0.136, 0.0), (0.096, 0.146, 0.008), (0.100, 0.150, 0.19), (0.097, 0.146, 0.232), (0.092, 0.140, 0.24), (0.084, 0.132, 0.24), (0.084, 0.132, 0.205))]
    p.add(loft(rings, cap_start=True), "plastic_green", sharp=40)
    p.add(loft([rings[-1]], cap_end=True), flat("glass"))
    cover = [(math.cos(a) * 0.092, 0.012 + math.sin(a) * 0.130) for a in np.linspace(0, math.pi, 8)]
    p.add(prism(cover, 0.236, 0.244), flat("plastic_green"), sharp=40)
    # The spout, from low on the front up to the rose.
    aim = Vector((0, 0.30, 0.25)).normalized()
    tip = Vector((0, 0.42, 0.30)) - aim * 0.040
    foot = tip - aim * 0.335
    p.add(tube([tuple(foot), tuple(foot + aim * 0.17), tuple(tip)], [0.024, 0.017, 0.012], 8, caps=False), flat("plastic_green"))
    tilt = -math.degrees(math.atan2(aim.y, aim.z))
    p.add(lathe([(0.012, 0.0), (0.040, 0.034), (0.040, 0.040)], 10, cap_bottom=True), flat("plastic_green"), at=tuple(tip), rot=(tilt, 0, 0), sharp=40)
    p.add(lathe([(0.039, 0.040), (0.0, 0.043)], 10), flat("gold"), at=tuple(tip), rot=(tilt, 0, 0), sharp=180)
    rod(p, (0, 0.118, 0.236), tuple(foot + aim * 0.20), 0.006, "plastic_green", 5)
    # The handle: up from the front of the top, back over it (the grip), and down the back.
    handle = fillet([(0, 0.115, 0.235), (0, 0.055, 0.34), (0, -0.095, 0.34), (0, -0.185, 0.245), (0, -0.142, 0.10)], 0.020, 2)
    p.add(tube(handle, 0.015, 8), flat("plastic_green"), sharp=70)
    return p


# ---------------------------------------------------------------------------------------------- things held

GUITAR_BODY = ((0.0, 0.0), (0.070, 0.004), (0.130, 0.024), (0.172, 0.070), (0.190, 0.130), (0.184, 0.190), (0.156, 0.245), (0.133, 0.285), (0.134, 0.330), (0.145, 0.375),
               (0.140, 0.420), (0.115, 0.458), (0.070, 0.476), (0.0, 0.480))


def guitar():
    p = Prop("guitar", "lit", pivot="bottom end of the body; it stands on its end, the neck is Z",
             note="1.00 m; sound board faces +Y, sound hole at (0, 0.05, 0.30); the fret hand wraps the neck (0.045 x 0.032 m) at 0.78 m, the nut is at 0.86 m")
    outline = [(x, z) for x, z in GUITAR_BODY] + [(-x, z) for x, z in GUITAR_BODY[-2:0:-1]]
    rings = [[(x * s, y, 0.24 + (z - 0.24) * s) for x, z in outline] for y, s in ((-0.050, 0.975), (-0.043, 1.0), (0.044, 1.0), (0.050, 0.985))]
    top = Paint("wood_dark").planar("guitar_top", (0, 1, 0), (-1, 0, 0), (0, 0, 0.24), (0.38, 0.48))
    p.add(loft(rings, cap_start=True, cap_end=True), top, sharp=40)
    p.add(box(0.15, 0.006, 0.030, bevel=0.002), flat("wood_dark"), at=(0, 0.053, 0.130))
    p.add(box(0.105, 0.003, 0.004), flat("paper"), at=(0, 0.0575, 0.136))
    # Neck and heel behind the fret board; the head with its six pegs.
    section = ((0.0225, 0.050), (0.0225, 0.040), (0.015, 0.030), (0.0, 0.026), (-0.015, 0.030), (-0.0225, 0.040), (-0.0225, 0.050))
    p.add(loft([[(x, y, z) for x, y in section] for z in (0.47, 0.86)], cap_start=True, cap_end=True), flat("wood_dark"), sharp=60)
    p.add(loft([[(sx * 0.0225, y, z) for sx, y in ((1, 0.045), (-1, 0.045), (-1, back), (1, back))] for z, back in ((0.478, -0.046), (0.50, -0.046), (0.56, 0.026))],
               cap_start=True, cap_end=True), flat("wood_dark"))
    board = flat("wood_dark").planar("fretboard", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.045, 0.515))
    p.add(box(0.045, 0.008, 0.515), board, at=(0, 0.054, 0.6025))
    p.add(box(0.046, 0.010, 0.006), flat("paper"), at=(0, 0.055, 0.863))
    head = [(-0.0225, 0.866), (0.0225, 0.866), (0.034, 0.888), (0.035, 0.975), (0.022, 1.0), (-0.022, 1.0), (-0.035, 0.975), (-0.034, 0.888)]
    p.add(loft([[(x, y, z) for x, z in head] for y in (0.030, 0.046)], cap_start=True, cap_end=True), flat("wood_dark"))
    for side in (-1, 1):
        for z in (0.900, 0.935, 0.970):
            p.add(box(0.013, 0.007, 0.016, bevel=0.002), flat("chrome"), at=(side * 0.0415, 0.036, z))
            p.add(lathe([(0.004, 0.046), (0.004, 0.053)], 5, cap_top=True), flat("chrome"), at=(side * 0.022, 0, z), rot=(-90, 0, 0), sharp=70)
    return p


def notebook():
    p = Prop("notebook", "lit", pivot="centre, lying flat", note="open reporter's pad 0.10 x 0.15 m, spiral at +Y, the written page up; held flat on the left palm")
    p.add(box(0.100, 0.150, 0.002), flat("cardboard"), at=(0, 0, 0.001))
    p.add(box(0.098, 0.140, 0.007), flat("paper"), at=(0, -0.004, 0.0055))
    sticker(p, "notebook_page", (0, -0.004, 0.009), (0, 0, 1), (1, 0, 0), (0.098, 0.140), lift=0.0004)
    turns = 10
    spiral = [(-0.045 + 0.090 * i / (turns * 4), 0.0675 + 0.0052 * math.cos(i * math.pi / 2), 0.0062 + 0.0052 * math.sin(i * math.pi / 2)) for i in range(turns * 4 + 1)]
    p.add(tube(spiral, 0.0011, 3), flat("chrome"), sharp=180)
    return p


def newspaper():
    p = Prop("newspaper", "lit", pivot="bottom of the centre fold; it stands in the XZ plane",
             note="open broadsheet 0.58 x 0.40 m held up to read from -Y; hands at (-0.25, 0, 0.13) and (0.25, 0, 0.13); front and back page face +Y")

    def page(u, v):
        s = abs(2.0 * u - 1.0)                              # 0 at the fold, 1 at an outer edge
        curl = v ** 2.2 * s ** 1.6
        y = -0.030 * s + 0.011 * math.sin(math.pi * s) * (0.35 + 0.65 * v) + 0.050 * curl
        return ((u - 0.5) * 0.58 * (1.0 - 0.012 * curl), y, 0.40 * v - 0.022 * curl)
    for half in (0, 1):                                     # 0 is the reader's left
        p.add(grid(lambda a, b, h=half: page((h + a) / 2, b), 4, 4), flat("paper").param("news_inner"), sharp=180)
        p.add(grid(lambda a, b, h=half: page((h + 1 - a) / 2, b), 4, 4), flat("paper").param("news_front" if half == 0 else "news_inner"), sharp=180)
    return p


def map_folded():
    p = Prop("map_folded", "lit", pivot="bottom centre; it stands in the XZ plane",
             note="half-unfolded street map 0.44 x 0.30 m read from -Y; hands at (-0.19, 0, 0.10) and (0.19, 0, 0.10); the cover panel is on +Y")
    creases, depth = (-0.22, -0.0733, 0.0733, 0.22), (-0.010, 0.013, -0.013, 0.010)

    def sheet(u, v):
        x = (u - 0.5) * 0.44
        y = float(np.interp(x, creases, depth)) + 0.006 * (1.0 - abs(2.0 * v - 1.0)) + 0.018 * v * v * abs(2.0 * u - 1.0) ** 2
        return (x, y, 0.30 * v - 0.004 * v * v * abs(2.0 * u - 1.0) ** 2)
    p.add(grid(sheet, 6, 2), flat("paper").param("map_print"), sharp=180)
    for panel in range(3):                                  # the back, seen from +Y: left to right is +X to -X
        paint = flat("paper").param("map_cover") if panel == 2 else flat("paper")
        p.add(grid(lambda a, b, k=panel: sheet(1.0 - (k + a) / 3, b), 2, 2), paint, sharp=180)
    return p


def umbrella():
    p = Prop("umbrella", "lit", pivot="bottom of the handle; the shaft is Z",
             note="open, 1.02 m across, rim 0.62 m up, tip 0.86 m; held by the straight handle (0.13 m long, 0.03 m thick) at 0.06 m")
    p.add(lathe([(0.010, 0.0), (0.015, 0.006), (0.015, 0.118), (0.011, 0.13)], 8, cap_bottom=True, cap_top=True), flat("wood_dark"), sharp=40)
    p.add(tube([(0, 0, 0.125), (0, 0, 0.862)], 0.0055, 6, caps=False), flat("chrome"), sharp=70)
    p.add(lathe([(0.013, 0.855), (0.009, 0.872), (0.004, 0.878), (0.003, 0.915), (0.0, 0.918)], 6), flat("gloss_black"), sharp=40)
    p.add(lathe([(0.012, 0.50), (0.012, 0.535)], 6, cap_top=True, cap_bottom=True), flat("gloss_black"), sharp=40)
    shape = dict(radius=0.51, tip=0.86, rim=0.62)
    p.add(grid(lambda u, v: canopy(u, v, **shape), 16, 5, both_sides=True), Paint("cloth_navy"), sharp=180)
    for k in range(8):
        u = k / 8.0
        rib = [Vector(canopy(u, v, **shape)) - Vector((0, 0, 0.006)) for v in (0.04, 0.35, 0.70, 1.0)]
        p.add(tube(rib, 0.0028, 3), flat("steel_dark"), sharp=180)
        a = 2 * math.pi * u
        rod(p, (math.cos(a) * 0.012, math.sin(a) * 0.012, 0.52), tuple(Vector(canopy(u, 0.52, **shape)) - Vector((0, 0, 0.008))), 0.0024, "steel_dark", 3, caps=False)
        p.add(lathe([(0.004, -0.004), (0.004, 0.006)], 4, cap_top=True, cap_bottom=True), flat("gloss_black"), at=tuple(Vector(canopy(u, 1.0, **shape)) + Vector((math.cos(a) * 0.004, math.sin(a) * 0.004, -0.004))))
    return p


# ---------------------------------------------------------------------------------------------- things carried

def carton(name, sx, sy, sz, fragile):
    def make():
        p = Prop(name, "lit", note="%.2f x %.2f x %.2f m cardboard box, taped; carried with a hand under each end along X" % (sx, sy, sz))
        p.add(box(sx, sy, sz, bevel=0.006), "cardboard", at=(0, 0, sz / 2))
        tape, drop = min(0.05, sy * 0.30), min(0.075, sz * 0.5)
        p.add(box(sx + 0.002, tape, 0.001), flat("tape_brown"), at=(0, 0, sz + 0.0005))
        for side in (-1, 1):
            p.add(box(0.001, tape, drop), flat("tape_brown"), at=(side * (sx / 2 + 0.0005), 0, sz - drop / 2))
        wide = min(0.13, sx * 0.40)
        sticker(p, "parcel_label", (sx * 0.20, -(tape / 2 + (sy - tape) / 4), sz), (0, 0, 1), (1, 0, 0), (wide, wide * 88 / 128))
        if fragile:
            sticker(p, "fragile", (-sx * 0.18, sy / 2, sz * 0.62), (0, 1, 0), (-1, 0, 0), (0.14, 0.05))
            sticker(p, "fragile", (sx * 0.18, -sy / 2, sz * 0.62), (0, -1, 0), (1, 0, 0), (0.14, 0.05))
        return p
    return make


def shelf_box():
    p = Prop("shelf_box", "lit", note="open stock carton 0.40 x 0.30 x 0.25 m, flaps folded down, tins inside 0.05 m below the rim; carried in both arms, hands under the ends along X")
    shell, liner = cup(rect(0.40, 0.30), rect(0.388, 0.288), 0.0, 0.25, 0.10)
    p.add(shell, "cardboard")
    p.add(liner, flat("cardboard"))
    for side in (-1, 1):
        p.add(box(0.396, 0.004, 0.14), flat("cardboard"), at=(0, side * 0.187, 0.191), rot=(side * 30, 0, 0))
        p.add(box(0.004, 0.296, 0.14), flat("cardboard"), at=(side * 0.2075, 0, 0.181), rot=(0, side * 4, 0))
    for i in range(4):
        for j in range(3):
            tin = lathe([(0.044, 0.10), (0.044, 0.194), (0.040, 0.20)], 8)
            p.add(tin, Paint("aluminium", grade=False).wrap("can_label", 0.10, 0.20), at=((i - 1.5) * 0.094, (j - 1) * 0.092, 0), sharp=40)
            p.add(lathe([(0.040, 0.20), (0.0, 0.2005)], 8), flat("chrome"), at=((i - 1.5) * 0.094, (j - 1) * 0.092, 0), sharp=180)
    sticker(p, "parcel_label", (0.09, -0.15, 0.065), (0, -1, 0), (1, 0, 0), (0.12, 0.0825))
    return p


def hand_truck():
    p = Prop("hand_truck", "lit", pivot="the ground under the middle of the axle",
             note="sack truck; wheels 0.11 m radius at x +-0.21; toe plate reaches +Y 0.26 m; grips 0.12 m long, 0.032 m thick, centred (-0.17, -0.04, 1.14) and (0.17, -0.04, 1.14), pointing back and up")
    back = Vector((0, -1, 1)).normalized()
    for side in (-1, 1):
        x = side * 0.17
        centre = Vector((x, -0.04, 1.14))
        low, high = centre - back * 0.06, centre + back * 0.06
        pipe(p, [(x, 0.04, 0.008), (x, 0.04, 1.035), tuple(low), tuple(low + back * 0.02)], 0.013, "paint_red", 8, bend=0.05)
        p.add(tube([tuple(low), tuple(high)], 0.016, 8), flat("rubber_black"), sharp=70)
        p.add(tube([tuple(high - back * 0.006), tuple(high + back * 0.004)], 0.019, 8), flat("rubber_black"), sharp=70)
        p.add(box(0.010, 0.085, 0.16, bevel=0.003), flat("paint_red"), at=(x - side * 0.004, 0.012, 0.125))
        wheel(p, (side * 0.21, 0, 0.11), 0.11, 0.05)
    for z, bow in ((0.30, 0.0), (0.62, 0.0), (0.94, 0.0)):
        rod(p, (-0.17, 0.04, z), (0.17, 0.04, z), 0.011, "paint_red", 6, caps=False)
    p.add(box(0.035, 0.006, 0.64), flat("paint_red"), at=(0, 0.045, 0.62))
    rod(p, (-0.236, 0, 0.11), (0.236, 0, 0.11), 0.009, "steel_dark", 6)
    p.add(box(0.36, 0.235, 0.008, bevel=0.002), flat("aluminium"), at=(0, 0.1425, 0.004))
    p.add(box(0.36, 0.008, 0.055), flat("aluminium"), at=(0, 0.029, 0.031))
    return p


def pushchair():
    p = Prop("pushchair", "lit", pivot="ground centre of the wheels; it travels towards +Y",
             note="hood up, a blanket in the seat; pushed by the bar along X at y -0.44, z 0.95 (0.03 m thick), hands on the foam grips at x +-0.225")
    for side in (-1, 1):
        top, foot = (side * 0.150, -0.44, 0.95), (side * 0.190, 0.30, 0.09)
        rod(p, foot, top, 0.012, "aluminium", 6)
        along = lambda t: tuple(Vector(foot).lerp(Vector(top), t))
        rod(p, along(0.62), (side * 0.200, -0.30, 0.10), 0.011, "aluminium", 6)
        rod(p, (side * 0.200, -0.30, 0.10), (side * 0.192, 0.27, 0.105), 0.009, "aluminium", 5)
        wheel(p, (side * 0.230, 0.30, 0.09), 0.09, 0.035)
        wheel(p, (side * 0.230, -0.30, 0.10), 0.10, 0.035)
        p.add(tube([(side * 0.175, -0.44, 0.95), (side * 0.262, -0.44, 0.95)], 0.0172, 8), flat("rubber_black"), sharp=70)
    p.add(tube([(-0.26, -0.44, 0.95), (0.26, -0.44, 0.95)], 0.015, 8), flat("aluminium"), sharp=70)
    rod(p, (-0.23, 0.30, 0.09), (0.23, 0.30, 0.09), 0.008, "steel_dark", 5)
    rod(p, (-0.23, -0.30, 0.10), (0.23, -0.30, 0.10), 0.008, "steel_dark", 5)
    # Seat, back, calf rest and foot plate; the basket underneath; the bumper bar.
    p.add(box(0.34, 0.31, 0.035, bevel=0.012), "fabric_teal", at=(0, -0.05, 0.40))
    p.add(box(0.34, 0.035, 0.47, bevel=0.012), "fabric_teal", at=(0, -0.268, 0.615), rot=(16.5, 0, 0))
    p.add(box(0.30, 0.025, 0.21, bevel=0.008), "fabric_teal", at=(0, 0.142, 0.31), rot=(-24, 0, 0))
    p.add(box(0.30, 0.10, 0.018, bevel=0.005), flat("plastic_grey"), at=(0, 0.215, 0.205))
    shell, liner = cup(rounded_rect(0.33, 0.40, 0.04, 1), rounded_rect(0.31, 0.38, 0.03, 1), 0.14, 0.25, 0.155)
    p.add(shell, flat("nylon_black"), at=(0, -0.03, 0))
    p.add(liner, flat("nylon_black"), at=(0, -0.03, 0))
    pipe(p, [(-0.172, -0.13, 0.50), (-0.172, 0.115, 0.585), (0.172, 0.115, 0.585), (0.172, -0.13, 0.50)], 0.013, "rubber_black", 6, bend=0.05)

    def blanket(u, v):
        bump = math.sin(math.pi * u) ** 0.7 * math.sin(math.pi * min(1.0, v * 1.15)) ** 0.6
        y = -0.215 + 0.33 * v
        return ((u - 0.5) * 0.31, y - 0.05 * bump * (1 - v), 0.418 + (0.20 - 0.09 * v) * bump + 0.14 * (1 - v) ** 3 * math.sin(math.pi * u) ** 0.5)
    p.add(grid(blanket, 6, 6), Paint("linen").param("blanket_cloth"), sharp=180)
    # The hood: bows hinged on the frame either side, cloth over them.
    hinge = Vector((0, -0.24, 0.718))

    def hood(u, v):
        s, turn = math.pi * u, math.radians(115.0 - 110.0 * v)
        reach = 0.30 * math.sin(s) ** 0.45
        return (0.185 * math.cos(s), hinge.y + math.cos(turn) * reach, hinge.z + math.sin(turn) * reach)
    p.add(grid(hood, 8, 5, both_sides=True), Paint("fabric_teal"), sharp=180)
    for side in (-1, 1):
        p.add(lathe([(0.02, -0.008), (0.02, 0.008)], 8, cap_top=True, cap_bottom=True), flat("plastic_grey"), at=(side * 0.186, hinge.y, hinge.z), rot=(0, 90, 0), sharp=40)
    return p


def stretcher_push_bar():
    p = Prop("stretcher_push_bar", "lit", pivot="the stretcher's own: a child of the stretcher at zero",
             note="a push bar at each end of the stretcher: cross bar 0.03 m thick 0.95 m up, 1.11 m from the middle, rubber grips at x +-0.225")
    for end in (-1, 1):
        y = end * 1.11
        bar = [(-0.30, end * 0.885, 0.612), (-0.30, y, 0.95), (0.30, y, 0.95), (0.30, end * 0.885, 0.612)]
        pipe(p, bar, 0.015, "aluminium", 8, bend=0.045)
        for side in (-1, 1):
            p.add(tube([(side * 0.172, y, 0.95), (side * 0.262, y, 0.95)], 0.0172, 8), flat("rubber_black"), sharp=70)
            p.add(box(0.040, 0.075, 0.046, bevel=0.007), flat("paint_yellow"), at=(side * 0.296, end * 0.885, 0.612))
            rod(p, (side * 0.30, end * 0.985, 0.612), (side * 0.30, end * 1.03, 0.82), 0.008, "aluminium", 5, caps=False)
    return p


# ---------------------------------------------------------------------------------------------- for jobs

def taxi_sign():
    p = Prop("taxi_sign", "glow", note="roof light: 0.36 x 0.13 x 0.12 m lamp on a low black magnetic base; TAXI on its +Y and -Y faces")
    lamp = [(-0.18, 0.025), (0.18, 0.025), (0.18, 0.105), (0.172, 0.132), (0.150, 0.150), (0.120, 0.155), (-0.120, 0.155), (-0.150, 0.150), (-0.172, 0.132), (-0.18, 0.105)]
    rings = [[(x * s, y, 0.09 + (z - 0.09) * s) for x, z in lamp] for y, s in ((-0.060, 0.93), (-0.050, 1.0), (0.050, 1.0), (0.060, 0.93))]
    faces = Paint("paint_yellow", grade=False).planar("taxi_face", (0, 1, 0), (-1, 0, 0), (0, 0, 0.09), (0.335, 0.118)).planar("taxi_face", (0, -1, 0), (1, 0, 0), (0, 0, 0.09), (0.335, 0.118))
    p.add(loft(rings, cap_start=True, cap_end=True), faces, sharp=40)
    p.add(box(0.31, 0.105, 0.020, bevel=0.005), flat("gloss_black"), at=(0, 0, 0.018))
    p.add(box(0.25, 0.085, 0.008), flat("rubber_black"), at=(0, 0, 0.004))
    return p


def flyer():
    p = Prop("flyer", "lit", pivot="centre, lying flat", note="one A5 flyer 0.148 x 0.21 m, printed side up, its head towards +Y; held by its near edge")

    def sheet(u, v):
        return ((u - 0.5) * 0.148, (v - 0.5) * 0.21, 0.004 * (2 * u - 1) ** 2 + 0.003 * v * v)
    p.add(grid(sheet, 4, 4, both_sides=True), flat("paper").param("flyer_face"), sharp=180)
    return p


def flyer_stack():
    p = Prop("flyer_stack", "lit", pivot="middle of the top edge: the stack hangs below it", note="a hand's worth of flyers 0.15 x 0.21 x 0.02 m, upright; the printed face is +Y")
    for k, (dx, dz, skew) in enumerate(((0.0008, -0.0012, 0.25), (-0.0009, -0.0004, -0.2), (0.0004, -0.0016, 0.1), (-0.0006, -0.0008, -0.3), (0.0, 0.0, 0.0))):
        paint = flat("paper")
        if k == 4:
            paint = paint.planar("flyer_face", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.148, 0.21))
        p.add(box(0.148, 0.004, 0.21), paint, at=(dx, -0.008 + 0.004 * k, -0.105 + dz), rot=(0, skew, 0))
    return p


def job_board():
    p = Prop("job_board", "lit", note="help-wanted notice board 1.2 m wide, 1.85 m high, the board from 0.75 to 1.75 m; it is read from +Y; the back is plain")
    for side in (-1, 1):
        p.add(box(0.05, 0.05, 1.79, bevel=0.006), "paint_green", at=(side * 0.565, 0, 0.895))
        p.add(box(0.15, 0.54, 0.06, bevel=0.014), "concrete", at=(side * 0.565, 0, 0.03))
        p.add(box(0.07, 0.07, 0.05, bevel=0.008), flat("steel_dark"), at=(side * 0.565, 0, 0.085))
    p.add(box(1.08, 0.030, 1.00), "wood", at=(0, 0, 1.25))
    sticker(p, "job_board_face", (0, 0.015, 1.25), (0, 1, 0), (-1, 0, 0), (1.04, 0.96), lift=0.001)
    for z in (0.76, 1.74):
        p.add(box(1.08, 0.044, 0.024, bevel=0.004), flat("aluminium"), at=(0, 0, z))
    for x in (-0.53, 0.53):
        p.add(box(0.024, 0.044, 0.98, bevel=0.004), flat("aluminium"), at=(x, 0, 1.25))
    cap = [[(x, y, z) for y, z in ((-0.085, 1.785), (0.085, 1.785), (0.085, 1.80), (0.0, 1.85), (-0.085, 1.80))] for x in (-0.63, 0.63)]
    p.add(loft(cap, cap_start=True, cap_end=True), "paint_green")

    def pinned(name, x, z, width, height):
        def sheet(u, v):
            return (x - (u - 0.5) * width, 0.0175 + 0.018 * (1 - v) ** 2 + 0.006 * (1 - v) * (2 * u - 1) ** 2, z + (v - 0.5) * height)
        p.add(grid(sheet, 2, 3, both_sides=True), flat("paper").param(name), sharp=180)
        p.add(lathe([(0.006, 0.0), (0.006, 0.006), (0.0, 0.008)], 6), flat("paint_red"), at=(x, 0.016, z + height / 2 - 0.012), rot=(-90, 0, 0), sharp=60)
    pinned("tearoff", -0.40, 1.165, 0.115, 0.16)
    pinned("flyer_face", 0.385, 0.885, 0.125, 0.177)
    return p


def mop_bucket():
    p = Prop("mop_bucket", "lit", note="janitor's bucket 0.36 x 0.50 m on four castors, tub 0.37 m high, the wringer and its lever (to 0.75 m) at the -Y end; water in it")
    outer = [[(x, y, z) for x, y in rounded_rect(w, d, r, 1)] for w, d, r, z in ((0.28, 0.42, 0.05, 0.065), (0.30, 0.44, 0.05, 0.075), (0.36, 0.50, 0.06, 0.36), (0.372, 0.512, 0.06, 0.362),
                                                                               (0.372, 0.512, 0.06, 0.372), (0.336, 0.476, 0.05, 0.372), (0.316, 0.456, 0.05, 0.25))]
    p.add(loft(outer, cap_start=True), "paint_yellow", sharp=40)
    p.add(loft([outer[-1]], cap_end=True), flat("water_grey"))
    for side in (-1, 1):
        sticker(p, "caution", (side * 0.1665, 0.07, 0.22), (side * 0.995, 0, -0.0995), (0, side, 0), (0.20, 0.08), lift=0.002)
        for y in (-0.17, 0.17):
            x = side * 0.115
            p.add(lathe([(0.027, -0.011), (0.032, -0.006), (0.032, 0.006), (0.027, 0.011)], 8, cap_top=True, cap_bottom=True), flat("rubber_black"), at=(x, y - 0.015, 0.032), rot=(0, 90, 0), sharp=40)
            p.add(box(0.034, 0.04, 0.012), flat("steel_dark"), at=(x, y - 0.006, 0.064))
            for cheek in (-1, 1):
                p.add(box(0.004, 0.026, 0.04), flat("steel_dark"), at=(x + cheek * 0.015, y - 0.012, 0.045), rot=(-15, 0, 0))
    # The wringer: a hopper hooked over the end of the tub, its press plate, and the lever that works it.
    shell, liner = cup(shifted(rounded_rect(0.31, 0.20, 0.03, 1), dy=-0.165), shifted(rounded_rect(0.285, 0.175, 0.02, 1), dy=-0.165), 0.30, 0.55, 0.38,
                       top=shifted(rounded_rect(0.33, 0.22, 0.03, 1), dy=-0.165))
    p.add(shell, "paint_yellow")
    p.add(liner, flat("plastic_grey"))
    p.add(box(0.27, 0.014, 0.13, bevel=0.003), flat("paint_yellow"), at=(0, -0.130, 0.47), rot=(-12, 0, 0))
    p.add(box(0.026, 0.07, 0.11, bevel=0.006), flat("plastic_grey"), at=(0.172, -0.165, 0.47))
    lever = [(0.190, -0.150, 0.47), (0.190, -0.205, 0.60), (0.190, -0.245, 0.70)]
    p.add(tube(lever, 0.010, 6), flat("steel_dark"), sharp=70)
    aim = (Vector(lever[2]) - Vector(lever[1])).normalized()
    p.add(tube([tuple(Vector(lever[2]) - aim * 0.05), tuple(Vector(lever[2]) + aim * 0.055)], 0.016, 8), flat("paint_red"), sharp=70)
    return p


CASE = ((-0.540, 0.0), (-0.532, 0.100), (-0.500, 0.170), (-0.430, 0.205), (-0.330, 0.210), (-0.220, 0.195), (-0.120, 0.165), (-0.040, 0.158), (0.040, 0.148), (0.100, 0.120),
        (0.160, 0.092), (0.300, 0.082), (0.500, 0.076), (0.532, 0.060), (0.540, 0.0))


def guitar_case():
    p = Prop("guitar_case", "lit", pivot="ground centre of the lower shell", note="open hard case 1.08 m long (X), the lid standing up along -Y; plush inside with coins and a note; a busker's tips")
    outline = [(x, -y) for x, y in CASE] + [(x, y) for x, y in CASE[-2:0:-1]]          # anticlockwise from above
    shell, liner = cup(outline, inset(outline, 0.014), 0.0, 0.11, 0.022)
    p.add(shell, "leather_black")
    p.add(liner, "plush_red")
    lid_outline = [(x, -(y + 0.21)) for x, y in outline][::-1]                          # as it would lie fully open, hinge along y = 0
    shell, liner = cup(lid_outline, inset(lid_outline, 0.014), -0.05, 0.0, -0.038)
    p.add(shell, flat("leather_black"), at=(0, -0.21, 0.11), rot=(-80, 0, 0))
    p.add(liner, flat("plush_red"), at=(0, -0.21, 0.11), rot=(-80, 0, 0))
    for x in (-0.40, -0.26):
        p.add(tube([(x - 0.03, -0.214, 0.11), (x + 0.03, -0.214, 0.11)], 0.006, 5), flat("chrome"), sharp=70)
    for x in (-0.36, -0.10, 0.36):
        y = 0.21 if x == -0.36 else 0.168 if x == -0.10 else 0.083
        p.add(box(0.035, 0.008, 0.03, bevel=0.002), flat("chrome"), at=(x, y + 0.002, 0.085))
    pipe(p, [(-0.30, 0.205, 0.06), (-0.30, 0.245, 0.06), (-0.18, 0.232, 0.06), (-0.18, 0.192, 0.06)], 0.009, "leather_black", 5, bend=0.02)
    # The neck rest with its little compartment, and what has been thrown in.
    p.add(box(0.11, 0.13, 0.060, bevel=0.008), "plush_red", at=(0.20, 0, 0.050))
    p.add(box(0.03, 0.12, 0.045, bevel=0.008), "plush_red", at=(0.42, 0, 0.044))
    rng = np.random.default_rng(3)
    for k in range(9):
        x, y = rng.uniform(-0.46, -0.10), rng.uniform(-0.12, 0.12)
        p.add(lathe([(0.0125, 0.0), (0.0125, 0.002), (0.0, 0.0022)], 7), flat("gold" if k % 3 else "chrome"), at=(x, y, 0.0222 + 0.0005 * k), sharp=60)

    def note(u, v):
        return ((u - 0.5) * 0.13, (v - 0.5) * 0.062, 0.006 * math.sin(math.pi * u) + 0.004 * (2 * u - 1) * (2 * v - 1))
    p.add(grid(note, 3, 1, both_sides=True), flat("paper").param("banknote"), at=(-0.21, -0.04, 0.0235), rot=(0, 0, 28), sharp=180)
    return p


def vendor_cart():
    p = Prop("vendor_cart", "lit", note="hot-dog cart; customers at +Y, the vendor at -Y; counter 0.975 m up, 0.80 m deep (y -0.40 to 0.40), 1.50 m wide; handle at -X, wheels at +X")
    p.add(box(1.30, 0.70, 0.58, bevel=0.015), "stainless", at=(0, 0, 0.655))
    p.add(box(1.50, 0.80, 0.030, bevel=0.007), flat("stainless"), at=(0, 0, 0.960))
    sticker(p, "hotdog_sign", (0, 0.35, 0.66), (0, 1, 0), (-1, 0, 0), (1.10, 0.314), lift=0.002)
    for x in (-0.33, 0.33):                                # the vendor's doors
        p.add(box(0.56, 0.008, 0.44, bevel=0.003), flat("aluminium"), at=(x, -0.352, 0.66))
        p.add(box(0.012, 0.014, 0.10, bevel=0.003), flat("chrome"), at=(x - math.copysign(0.22, x), -0.362, 0.70))
    for side in (-1, 1):
        spoked_wheel(p, (0.40, side * 0.385, 0.30), 0.30)
        rod(p, (-0.56, side * 0.27, 0.37), (-0.56, side * 0.27, 0.02), 0.018, "steel_dark", 6, caps=False)
        p.add(lathe([(0.030, 0.0), (0.030, 0.022), (0.020, 0.03)], 8, cap_bottom=True), flat("rubber_black"), at=(-0.56, side * 0.27, 0), sharp=40)
    rod(p, (0.40, -0.40, 0.30), (0.40, 0.40, 0.30), 0.012, "steel_dark", 6)
    rod(p, (-0.56, -0.27, 0.12), (-0.56, 0.27, 0.12), 0.010, "steel_dark", 5, caps=False)
    pipe(p, [(-0.64, -0.28, 0.87), (-0.90, -0.28, 0.93), (-0.90, 0.28, 0.93), (-0.64, 0.28, 0.87)], 0.016, "chrome", 6, bend=0.05)
    p.add(tube([(-0.90, -0.17, 0.93), (-0.90, 0.17, 0.93)], 0.020, 8), flat("rubber_black"), sharp=70)
    # On the counter: the steamer under its roll-top lid, a flat pan lid, the condiment rail, napkins.
    p.add(box(0.50, 0.44, 0.07, bevel=0.008), flat("stainless"), at=(-0.40, -0.05, 1.01))
    lid = [[(x, -0.05 + 0.205 * math.cos(t), 1.045 + 0.15 * math.sin(t)) for t in np.linspace(0, math.pi, 8)] for x in (-0.635, -0.165)]
    p.add(loft(lid, cap_start=True, cap_end=True), flat("chrome"), sharp=40)
    pipe(p, [(-0.48, 0.10, 1.145), (-0.48, 0.135, 1.17), (-0.32, 0.135, 1.17), (-0.32, 0.10, 1.145)], 0.008, "gloss_black", 5, bend=0.015)
    p.add(box(0.30, 0.34, 0.022, bevel=0.006), flat("chrome"), at=(0.08, -0.08, 0.986))
    p.add(lathe([(0.016, 0.997), (0.022, 1.012), (0.016, 1.026), (0.0, 1.028)], 8), flat("gloss_black"), at=(0.08, -0.08, 0), sharp=40)
    shell, liner = cup(shifted(rect(0.40, 0.10), 0.46, 0.33), shifted(rect(0.388, 0.088), 0.46, 0.33), 0.975, 1.035, 0.985)
    p.add(shell, flat("stainless"))
    p.add(liner, flat("stainless"))
    for k, paint in enumerate(("paint_red", "paint_yellow", "paint_red", "paint_yellow")):
        bottle = lathe([(0.022, 0.985), (0.024, 0.995), (0.024, 1.085), (0.013, 1.108), (0.011, 1.118), (0.005, 1.150), (0.0, 1.152)], 8)
        p.add(bottle, flat(paint), at=(0.325 + 0.09 * k, 0.33, 0), sharp=40)
    p.add(box(0.13, 0.11, 0.11, bevel=0.01), flat("gloss_black"), at=(0.50, -0.20, 1.03))
    p.add(box(0.09, 0.06, 0.03), flat("paper"), at=(0.50, -0.20, 1.09))
    p.add(lathe([(0.030, 0.975), (0.030, 1.04)], 8, cap_top=True), flat("steel_dark"), at=(0.66, -0.31, 0), sharp=40)       # where the parasol stands
    return p


def vendor_umbrella():
    p = Prop("vendor_umbrella", "lit", pivot="ground centre, at the foot of the pole", note="market parasol 2.1 m across, red and white; pole to 2.35 m, rim 2.0 m up, the valance hanging to 1.9 m")
    p.add(lathe([(0.26, 0.0), (0.28, 0.012), (0.28, 0.055), (0.25, 0.075), (0.06, 0.085), (0.05, 0.20), (0.032, 0.21)], 14, cap_bottom=True), "concrete", sharp=40)
    p.add(tube([(0, 0, 0.20), (0, 0, 2.31)], 0.022, 8, caps=False), Paint("aluminium"), sharp=70)
    p.add(lathe([(0.035, 2.27), (0.030, 2.305), (0.012, 2.32), (0.016, 2.335), (0.0, 2.35)], 8), flat("paint_red"), sharp=40)
    p.add(lathe([(0.040, 1.74), (0.040, 1.80)], 8, cap_top=True, cap_bottom=True), flat("steel_dark"), sharp=40)
    shape = dict(radius=1.05, tip=2.30, rim=2.02, dome=1.5, slack=0.3, scallop=0.02)
    for k in range(8):
        paint = Paint("nylon_red" if k % 2 == 0 else "linen", grade=False)
        p.add(grid(lambda a, b, k=k: canopy((k + a) / 8, b, **shape), 2, 4, both_sides=True), paint, sharp=180)

        def valance(a, b, k=k):
            x, y, z = canopy((k + a) / 8, 1.0, **shape)
            return (x, y, z - 0.12 * b * (1.0 - 0.25 * abs(2 * a - 1)))
        p.add(grid(valance, 2, 1, both_sides=True), paint, sharp=180)
        u = k / 8.0
        a = 2 * math.pi * u
        rib = [Vector(canopy(u, v, **shape)) - Vector((0, 0, 0.012)) for v in (0.03, 0.5, 1.0)]
        p.add(tube(rib, 0.008, 4), flat("wood"), sharp=100)
        rod(p, (math.cos(a) * 0.04, math.sin(a) * 0.04, 1.77), tuple(Vector(canopy(u, 0.55, **shape)) - Vector((0, 0, 0.02))), 0.007, "wood", 4, caps=False)
    return p


# ---------------------------------------------------------------------------------------------- worn

def mail_satchel():
    p = Prop("mail_satchel", "lit", pivot="the upper-chest bone: kit point (0, 0, %.2f) of a figure standing at the origin facing +Y" % PIVOT_CHEST,
             note="postal shoulder bag as worn: the bag (0.36 x 0.28 x 0.11 m) at the left hip, its strap up across chest and back over the right shoulder; parent to UpperChest at zero")
    cx, cz = -0.20, 0.88
    rings = [[(cx + x * s, y * s, cz + z) for x, y in rounded_rect(0.11, 0.36, 0.03, 1)] for z, s in ((-0.14, 0.86), (-0.125, 0.97), (-0.03, 1.03), (0.11, 0.98), (0.14, 0.95))]
    p.add(loft(rings, cap_start=True, cap_end=True), "canvas_mail", sharp=40)
    # The flap over the outer face, its pale stripe and two buckled straps; letters standing in the top.
    flap = [[(cx - 0.058 - out, y, cz + z) for y in (0.172, -0.172)] + [(cx - 0.040, y, cz + z) for y in (-0.172, 0.172)] for z, out in ((-0.045, 0.012), (0.0, 0.016), (0.118, 0.008), (0.146, -0.012))]
    p.add(loft(flap, cap_start=True, cap_end=True), "canvas_mail", sharp=40)
    p.add(box(0.004, 0.346, 0.022), flat("reflective_white"), at=(cx - 0.0735, 0, cz - 0.018), rot=(0, -3, 0))
    for y in (-0.09, 0.09):
        p.add(box(0.006, 0.030, 0.13), flat("leather_black"), at=(cx - 0.074, y, cz + 0.005), rot=(0, -3, 0))
        p.add(box(0.008, 0.040, 0.026, bevel=0.002), flat("chrome"), at=(cx - 0.078, y, cz - 0.052))
    for k, (y, lean, high) in enumerate(((-0.10, 5, 0.05), (-0.02, -4, 0.062), (0.05, 7, 0.045), (0.11, -3, 0.056))):
        p.add(box(0.004, 0.115, 0.09), flat("paper" if k != 2 else "cardboard"), at=(cx + 0.012 - 0.012 * (k % 2), y, cz + 0.14 + high - 0.045), rot=(lean, 0, 0))
    # The strap: off the front end of the bag, across the chest, over the right shoulder, down the back.
    front = [(-0.200, 0.172, 0.985), (-0.130, 0.150, 1.12), (-0.050, 0.128, 1.26), (0.040, 0.114, 1.385), (0.088, 0.078, 1.452), (0.105, 0.0, 1.482)]
    path = front + [(x, -y, z) for x, y, z in front[-2::-1]]
    spine = (Vector((0, 0, 0.95)), Vector((0, 0, 1.38)))

    def away(q):
        t = max(0.0, min(1.0, (q.z - spine[0].z) / (spine[1].z - spine[0].z)))
        return q - spine[0].lerp(spine[1], t)
    p.add(ribbon(smooth(path, 3), away, 0.042, 0.005), flat("canvas_mail"), sharp=60)
    for side in (-1, 1):
        p.add(box(0.012, 0.012, 0.05, bevel=0.002), flat("chrome"), at=(-0.200, side * 0.176, 0.985))
    for v in p.bm.verts:
        v.co.z -= PIVOT_CHEST
    return p


# ---------------------------------------------------------------------------------------------- street furniture

def step_ladder():
    p = Prop("step_ladder", "lit", pivot="the ground under the middle of the top platform",
             note="A-frame 0.46 m wide; climbed from -Y: tread tops at (0, -0.24, 0.24) and (0, -0.12, 0.48), platform top at 0.72 m (0.28 m deep); one stands on the 0.48 m tread facing +Y")
    for side in (-1, 1):
        x = side * 0.215
        strut(p, (x, -0.36, 0.0), (x, -0.012, 0.696), 0.030, 0.070, Paint("aluminium"))
        strut(p, (x * 0.95, 0.32, 0.0), (x * 0.95, 0.105, 0.69), 0.026, 0.040, Paint("aluminium"))
        p.add(box(0.040, 0.085, 0.022, bevel=0.004), flat("rubber_black"), at=(x, -0.362, 0.011))
        p.add(box(0.034, 0.055, 0.022, bevel=0.004), flat("rubber_black"), at=(x * 0.95, 0.322, 0.011))
        p.add(box(0.005, 0.385, 0.022), flat("steel_dark"), at=(x - side * 0.019, 0.022, 0.36))          # the spreader
    for y, z in ((-0.24, 0.24), (-0.12, 0.48)):
        p.add(box(0.400, 0.090, 0.028, bevel=0.004), flat("aluminium"), at=(0, y, z - 0.015))
        p.add(box(0.380, 0.070, 0.002), flat("steel_dark"), at=(0, y, z - 0.001))
    for z in (0.22, 0.47):
        y = 0.32 - (0.32 - 0.105) * z / 0.69
        p.add(box(0.41, 0.012, 0.035), flat("aluminium"), at=(0, y, z))
    p.add(box(0.46, 0.28, 0.045, bevel=0.010), flat("paint_yellow"), at=(0, 0, 0.6965))
    p.add(box(0.36, 0.18, 0.002), flat("rubber_black"), at=(0, 0, 0.719))
    return p


PIECES = {
    "pawn": ((0.014, 0.0), (0.014, 0.004), (0.007, 0.012), (0.005, 0.028), (0.009, 0.036), (0.006, 0.044), (0.0, 0.046)),
    "rook": ((0.016, 0.0), (0.016, 0.005), (0.009, 0.014), (0.008, 0.040), (0.012, 0.044), (0.012, 0.056), (0.0, 0.056)),
    "bishop": ((0.015, 0.0), (0.015, 0.005), (0.008, 0.014), (0.006, 0.042), (0.010, 0.050), (0.006, 0.062), (0.0, 0.068)),
    "king": ((0.017, 0.0), (0.017, 0.005), (0.009, 0.016), (0.007, 0.050), (0.012, 0.058), (0.008, 0.068), (0.004, 0.072), (0.004, 0.084), (0.0, 0.084)),
}
GAME = (("paper", (("king", 4, 0), ("rook", 0, 0), ("bishop", 2, 2), ("pawn", 0, 1), ("pawn", 1, 2), ("pawn", 5, 1), ("pawn", 3, 3))),
        ("wood_dark", (("king", 6, 7), ("rook", 0, 7), ("bishop", 5, 4), ("pawn", 0, 6), ("pawn", 1, 5), ("pawn", 6, 5), ("pawn", 4, 4))))


def chess_table():
    p = Prop("chess_table", "lit", pivot="ground centre of the table", note="stone table 0.70 m square, top 0.74 m up, a game in progress; stools 0.34 m across, seats 0.45 m up, centred at y -0.62 and +0.62")
    p.add(box(0.70, 0.70, 0.07, bevel=0.012), "concrete", at=(0, 0, 0.705))
    board = flat("concrete").planar("chess_board", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.444, 0.444))
    p.add(box(0.444, 0.444, 0.004), board, at=(0, 0, 0.7395))
    p.add(box(0.36, 0.36, 0.05, bevel=0.012), "concrete", at=(0, 0, 0.645))
    p.add(box(0.22, 0.22, 0.58, bevel=0.02), "concrete", at=(0, 0, 0.34))
    p.add(box(0.46, 0.46, 0.07, bevel=0.015), "concrete", at=(0, 0, 0.035))
    for y in (-0.62, 0.62):
        stool = lathe([(0.125, 0.0), (0.135, 0.012), (0.095, 0.06), (0.085, 0.34), (0.155, 0.395), (0.170, 0.405), (0.170, 0.438), (0.158, 0.45), (0.0, 0.45)], 12, cap_bottom=True)
        p.add(stool, "concrete", at=(0, y, 0), sharp=35)
    for paint, men in GAME:
        for kind, column, row in men:
            p.add(lathe(PIECES[kind], 6), flat(paint), at=((column - 3.5) * 0.05, (row - 3.5) * 0.05, 0.7415), sharp=40)
    return p


def fruit_stand():
    p = Prop("fruit_stand", "lit", note="street fruit stall 1.7 m wide, 1.2 m deep, the awning's valance hanging to 1.92 m; customers at +Y, the vendor at -Y behind a flat counter strip 0.975 m up and 0.25 m deep")
    for sx in (-1, 1):
        p.add(box(0.045, 0.045, 2.19, bevel=0.005), "wood", at=(sx * 0.775, -0.425, 1.095))
        p.add(box(0.045, 0.045, 2.07, bevel=0.005), "wood", at=(sx * 0.775, 0.425, 1.035))
        strut(p, (sx * 0.775, -0.40, 0.12), (sx * 0.775, 0.40, 0.62), 0.022, 0.05, flat("wood"))
        strut(p, (sx * 0.775, 0.40, 0.12), (sx * 0.775, -0.40, 0.86), 0.022, 0.05, flat("wood"))
    p.add(box(1.60, 0.25, 0.035, bevel=0.005), flat("wood"), at=(0, -0.325, 0.9575))
    p.add(box(1.52, 0.03, 0.09), flat("wood"), at=(0, -0.425, 0.89))
    p.add(box(1.52, 0.03, 0.09), flat("wood"), at=(0, 0.425, 0.63))
    slope = math.degrees(math.atan2(0.25, 0.65))
    deck = dict(at=(0, 0.125, 0.805), rot=(-slope, 0, 0))

    def on_deck(part, paint, at=(0, 0, 0), rot=(0, 0, 0), **more):       # made lying on the sloping deck, then tipped with it
        p.add(moved(part, at=at, rot=rot), paint, **deck, **more)
    on_deck(box(1.52, 0.70, 0.022), flat("wood"))
    on_deck(box(1.52, 0.022, 0.05), flat("wood"), at=(0, 0.345, 0.03))
    fruit = (("fruit_orange", "fruit_banana", "fruit_apple"), ("fruit_green", "fruit_apple", "fruit_lemon"))
    for row, y in enumerate((-0.165, 0.17)):
        for column, x in enumerate((-0.50, 0.0, 0.50)):
            shell, liner = cup(rect(0.47, 0.31, (x, y)), rect(0.446, 0.286, (x, y)), 0.011, 0.125, 0.03)
            on_deck(shell, flat("wood"))
            on_deck(liner, flat("wood"))
            for z in (0.052, 0.094):
                on_deck(box(0.474, 0.314, 0.008), flat("wood_dark"), at=(x, y, z))

            def heap(u, v, x=x, y=y, k=row * 3 + column):
                dome = (math.sin(math.pi * u) * math.sin(math.pi * v)) ** 0.5
                lumps = 0.011 * math.sin(u * 19.0 + k) * math.cos(v * 13.0 + 2 * k)
                return (x + (u - 0.5) * 0.444, y + (v - 0.5) * 0.284, 0.085 + 0.075 * dome + lumps * dome)
            on_deck(grid(heap, 8, 6), flat("paper").param(fruit[row][column]), sharp=180)
            card = flat("paper").planar("price_card", (0, 1, 0), (-1, 0, 0), (x + 0.15, y - 0.150, 0.20), (0.084, 0.06))
            on_deck(box(0.084, 0.004, 0.06), card, at=(x + 0.15, y - 0.150, 0.20), rot=(slope - 12, 0, 0))
            on_deck(box(0.006, 0.004, 0.09), flat("wood"), at=(x + 0.15, y - 0.150, 0.14), rot=(slope - 12, 0, 0))
    # The skirt on the customers' side, and the awning with its valance.
    p.add(grid(lambda u, v: (0.75 - 1.5 * u, 0.442 + 0.008 * math.sin(u * 40.0) * (1 - v), 0.12 + 0.50 * v), 12, 1, both_sides=True), Paint("linen").param("awning_green"), sharp=180)

    def awning(u, v):
        sag = 0.035 * math.sin(math.pi * u) * math.sin(math.pi * v)
        return ((u - 0.5) * 1.72, -0.50 + 1.18 * v, 2.215 - 0.165 * v - sag)
    p.add(grid(awning, 6, 4, both_sides=True), Paint("linen").param("awning_green"), sharp=180)
    front = Paint("linen").planar("awning_green", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (1.72, 0.13)).planar("awning_green", (0, -1, 0), (1, 0, 0), (0, 0, 0), (1.72, 0.13))
    p.add(box(1.72, 0.006, 0.13), front, at=(0, 0.68, 1.988))
    for y, z in ((-0.425, 2.195), (0.425, 2.075)):
        p.add(box(1.60, 0.03, 0.03), flat("wood"), at=(0, y, z))
    return p


def newspaper_kiosk():
    p = Prop("newspaper_kiosk", "lit", note="news kiosk 1.8 m wide with its shutters open, 1.5 m deep at the roof, 2.4 m high; it serves at +Y over a sill 0.975 m up; nobody is seen inside")
    p.add(box(1.40, 1.20, 0.975, bevel=0.012), "paint_green", at=(0, 0, 0.4875))
    p.add(box(1.46, 1.26, 0.10, bevel=0.012), flat("steel_dark"), at=(0, 0, 0.05))
    p.add(box(1.40, 0.90, 1.19), flat("paint_green"), at=(0, -0.15, 1.56))
    for side in (-1, 1):
        p.add(box(0.25, 0.31, 1.19), flat("paint_green"), at=(side * 0.575, 0.445, 1.56))
        for y in (-0.60, 0.60):                             # corner trims
            p.add(box(0.05, 0.05, 2.14, bevel=0.008), flat("steel_dark"), at=(side * 0.70, y, 1.09))
    p.add(box(0.92, 0.31, 0.22), flat("paint_green"), at=(0, 0.445, 2.045))
    p.add(box(0.90, 0.006, 0.975), flat("gloss_black"), at=(0, 0.303, 1.4625))
    p.add(box(1.00, 0.24, 0.04, bevel=0.006), flat("aluminium"), at=(0, 0.67, 0.955))
    sticker(p, "mag_wide", (0, 0.612, 0.50), (0, 1, 0), (-1, 0, 0), (1.20, 0.60), lift=0.002)
    for z in (0.212, 0.512):
        p.add(box(1.22, 0.03, 0.014), flat("aluminium"), at=(0, 0.628, z))
    p.add(box(1.24, 0.024, 0.64), flat("steel_dark"), at=(0, 0.600, 0.50))
    sign = Paint("paint_green").planar("news_fascia", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (1.36, 0.235))
    p.add(box(1.36, 0.05, 0.235), sign, at=(0, 0.625, 2.045))
    for side in (-1, 1):                                    # the shutters, swung right open, a rack on each
        rack = Paint("paint_green").planar("mag_tall", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.43, 0.94))
        p.add(box(0.45, 0.03, 0.975), flat("paint_green"), at=(side * 0.672, 0.656, 1.4625), rot=(0, 0, side * 10))
        p.add(box(0.43, 0.012, 0.94), rack, at=(side * 0.668, 0.676, 1.4625), rot=(0, 0, side * 10))
        for k in range(4):
            p.add(box(0.43, 0.028, 0.012), flat("aluminium"), at=(side * 0.666, 0.690, 0.998 + 0.235 * k), rot=(0, 0, side * 10))
    for x in (-0.25, 0.20):                                 # today's papers on the sill
        stack = flat("paper").planar("news_front", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.16, 0.22))
        p.add(box(0.16, 0.22, 0.05 if x < 0 else 0.03), stack, at=(x, 0.67, 1.0 if x < 0 else 0.99), rot=(0, 0, 4 if x < 0 else -3))
    p.add(box(0.62, 0.012, 1.86, bevel=0.004), "paint_green", at=(0.25, -0.604, 0.97))            # the door at the back
    p.add(box(0.02, 0.03, 0.12, bevel=0.004), flat("chrome"), at=(0.02, -0.618, 1.02))
    p.add(box(1.72, 1.52, 0.06, bevel=0.012), flat("steel_dark"), at=(0, 0.06, 2.185))
    roof = [[(x * w, 0.06 + y * d, z) for x, y in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for w, d, z in ((0.82, 0.72, 2.212), (0.40, 0.22, 2.40))]
    p.add(loft(roof, cap_start=True, cap_end=True), flat("paint_green"), sharp=20)
    return p


# ---------------------------------------------------------------------------------------------- the list

# (maker, triangle budget), as in props.py.
LIFE_PROPS = [
    (mop, 290), (rake, 680), (paint_roller, 450), (watering_can, 550), (guitar, 760), (notebook, 300), (newspaper, 140), (map_folded, 60),
    (umbrella, 800), (carton("parcel_s", 0.22, 0.16, 0.10, False), 100), (carton("parcel_m", 0.38, 0.28, 0.24, True), 100), (carton("parcel_l", 0.50, 0.36, 0.32, True), 100),
    (shelf_box, 620), (hand_truck, 830), (pushchair, 1300), (stretcher_push_bar, 640),
    (taxi_sign, 150), (flyer, 64), (flyer_stack, 60), (job_board, 610), (mop_bucket, 780), (guitar_case, 1070), (vendor_cart, 2250), (vendor_umbrella, 870),
    (mail_satchel, 670), (step_ladder, 490), (chess_table, 1700), (fruit_stand, 1700), (newspaper_kiosk, 800),
]
