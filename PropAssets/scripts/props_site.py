"""THE SITE MODULE: what civic life, the courts and the police station stand in the street and the room. Same rules as
props.py: one function each, metres, X right, Y forward, Z up, one mesh on the shared atlas. Unity gets (x, z, y).

THE CODE IS THE SPECIFICATION. Every id below is asked for by the mod, which draws a stand-in of boxes until the
bundle has it; size, pivot and facing are the stand-in's (Scripts/Civic/CivicProps.cs, CivicStandIns; Scripts/Justice/
Courthouse.cs and CommunityService.cs; Scripts/Law/StationProps.cs), and Scripts/Civic/SiteDresser.cs says how the
pieces go together:

    scaffold_bay         every 2.40 m along a wall (BayWidth), its pivot 0.12 m off the wall (WallGap); deck top 2.40 m up
    scaffold_bay_upper   at the same place 2.40 m up (DeckHeight), and again every 2.00 m (LiftHeight); its own deck top 2.00 m up
    scaffold_net         at an upper bay's pivot: the outer face of that lift
    scaffold_fan         at a first bay's pivot: it springs from the first deck's outer edge
    site_hoarding(_notice)   every 2.40 m along a wall, 0.12 m off it
    trench_cover, asphalt_patch, road_marking    four in a row every 2.00 m along the lane: their ends join
    manhole_open         where the cover was; manhole_lid 1.25 m further along; works_tent over the hole, open to the pavement

Kit numbers that other things rely on (checks_site.py measures them):

    soapbox              the top is z 0.30 (CampaignWorld.BoxTop): the speaker stands on it
    scaffold_bay         standards at (+-1.175, 0.06) and (+-1.175, 1.74); the deck is z 2.37 to 2.40, y 0.09 to 1.71
                         (workers are stood on it 0.80 m from the pivot)
    scaffold_bay_upper   the same plan; deck z 1.97 to 2.00; guard rails 1.00 and 0.50 m up, toe board to 0.15 m
    campaign_poster_n    the printed side is +Y; the mount reaches 0.010 m behind the pivot (the code hangs the pivot 0.012 m
                         off the wall, which would otherwise show as a gap)
"""
import math

import bmesh
import numpy as np
from mathutils import Vector

from life_props import cup, flat, pipe, rect, rod, shifted, sticker, strut
from prop_kit import Paint, Prop, box, grid, lathe, loft, moved, prism, rounded_rect, tube

TUBE = 0.024                                         # a scaffold tube: 48 mm
BAY = 2.40                                           # SiteDresser.BayWidth
STANDARD_X = 1.175                                   # where a bay's standards are, from its middle: side by side with the next bay's
STANDARD_Y = (0.06, 1.74)                            # and from its pivot (the wall side, the street side)
DECK_Y = (0.09, 1.71)                                # the boards lie between the two rows of standards
BOARD_LEAN = math.degrees(math.atan2(0.21, 0.93))    # the sandwich board's faces (city_props.sandwich_board)


# ---------------------------------------------------------------------------------------------- helpers

def bar(p, a, b, radius, paint, sides=3):
    """A lattice member: an open-ended bar from a to b."""
    rod(p, a, b, radius, paint, sides, caps=False)


def pane(p, swatch, centre, normal, right, size, lift=0.002):
    """A flat piece of one finish laid on a surface (a window, a plate)."""
    n, r = Vector(normal).normalized(), Vector(right).normalized()
    up = n.cross(r)
    c = Vector(centre) + n * lift
    p.add(grid(lambda u, v: tuple(c + r * ((u - 0.5) * size[0]) + up * ((v - 0.5) * size[1])), 1, 1), flat(swatch), sharp=180)


def scaffold_tube(p, a, b, paint="aluminium"):
    p.add(tube([a, b], TUBE, 6), flat(paint), sharp=70)


def coupler(p, at):
    p.add(box(0.075, 0.075, 0.075), flat("steel_dark"), at=at)


def tiled(surface, nu, nv, both_sides=False, cells=1):
    """Like kit.grid, but every `cells` cells each way show the whole of the region it is painted with (param), mirrored
    from one repeat to the next so that they join: for a net, a weave, a heap."""
    fold = lambda k: 1.0 - abs((k / cells) % 2.0 - 1.0)
    bm = bmesh.new()
    param = bm.loops.layers.uv.new("param")
    verts = [[bm.verts.new(surface(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    back = [[bm.verts.new(v.co) for v in row] for row in verts] if both_sides else None
    for j in range(nv):
        for i in range(nu):
            corners = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
            for skin, order in ((verts, corners), (back, corners[::-1])):
                if skin is None:
                    continue
                face = bm.faces.new([skin[b][a] for a, b in order])
                for loop, (a, b) in zip(face.loops, order):
                    loop[param].uv = (fold(a), fold(b))
    bm.normal_update()
    return bm


def skin(points, faces, both_sides=False):
    """A few flat faces between given points (cloth, a sheet): faces are lists of indices, anticlockwise from outside."""
    bm = bmesh.new()
    verts = [bm.verts.new(q) for q in points]
    for face in faces:
        bm.faces.new([verts[k] for k in face])
    if both_sides:
        inner = [bm.verts.new(q) for q in points]
        for face in faces:
            bm.faces.new([inner[k] for k in face[::-1]])
    bm.normal_update()
    return bm


class Face:
    """Flat artwork laid out as it is read: r to the reader's right, z up, y out of the surface towards the reader.
    Made facing +Y about a local origin, then turned and stood where the surface is."""

    def __init__(self, p, at=(0.0, 0.0, 0.0), rot=(0.0, 0.0, 0.0)):
        self.p, self.at, self.rot = p, at, rot

    def add(self, part, paint, sharp=50.0):
        self.p.add(part, paint, at=self.at, rot=self.rot, sharp=sharp)

    def block(self, size, centre, paint, bevel=0.0):
        """A box: size (across, out, up), centre (r, y, z)."""
        self.add(moved(box(*size, bevel=bevel), at=(-centre[0], centre[1], centre[2])), paint)

    def decal(self, name, r, z, width, height, y):
        self.add(grid(lambda u, v: (-r + (0.5 - u) * width, y, z + (v - 0.5) * height), 1, 1), flat("paper").param(name), sharp=180)

    def poly(self, points, y, paint):
        """A flat shape from its outline (r, z), anticlockwise as read; it must be seen whole from its middle."""
        bm = bmesh.new()
        middle = bm.verts.new((-sum(q[0] for q in points) / len(points), y, sum(q[1] for q in points) / len(points)))
        ring = [bm.verts.new((-r, y, z)) for r, z in points]
        for k in range(len(ring)):
            bm.faces.new((middle, ring[k], ring[(k + 1) % len(ring)]))
        bm.normal_update()
        self.add(bm, flat(paint) if isinstance(paint, str) else paint, sharp=180)

    def panel(self, r0, z0, r1, z1, y, paint):
        self.poly([(r0, z0), (r1, z0), (r1, z1), (r0, z1)], y, paint)

    def star(self, r, z, radius, y, paint="paper"):
        self.poly([(r + math.cos(math.radians(90 + 36 * k)) * radius * (1.0 if k % 2 == 0 else 0.42),
                    z + math.sin(math.radians(90 + 36 * k)) * radius * (1.0 if k % 2 == 0 else 0.42)) for k in range(10)], y, paint)

    def disc(self, r, z, radius, y, paint, count=14):
        self.poly([(r + math.cos(2 * math.pi * k / count) * radius, z + math.sin(2 * math.pi * k / count) * radius) for k in range(count)], y, paint)


def outward(part, towards=(0.0, 0.0, 1.0)):
    """Turns an open skin's faces to look towards a direction (for a loft whose winding is not worth working out)."""
    part.normal_update()
    aim = Vector(towards)
    if sum(face.normal.dot(aim) * face.calc_area() for face in part.faces) < 0:
        bmesh.ops.reverse_faces(part, faces=part.faces[:])
        part.normal_update()
    return part


# ---------------------------------------------------------------------------------------------- the courthouse

def courthouse_sign():
    p = Prop("courthouse_sign", "lit",
             note="courthouse sign 1.42 x 0.42 x 1.95 m read from +Y: two black posts at x +-0.60 on stone feet (0.22 x 0.42), brass ball finials to 1.95; "
                  "a navy board 1.30 x 0.62 centred 1.52 m up, its face at y 0.025, lettered COURTHOUSE (capitals 0.14 m) under a pair of scales; "
                  "a brass cap 1.36 wide over it. The code letters nothing on the model")
    for side in (-1, 1):
        x = side * 0.60
        p.add(box(0.22, 0.42, 0.07, bevel=0.012), "concrete", at=(x, 0, 0.035))
        p.add(box(0.16, 0.24, 0.05, bevel=0.012), "concrete", at=(x, 0, 0.093))
        p.add(box(0.09, 0.09, 1.76, bevel=0.010), "plastic_grey", at=(x, 0, 0.99))
        p.add(lathe([(0.062, 1.87), (0.066, 1.878), (0.050, 1.886), (0.026, 1.892), (0.026, 1.900), (0.040, 1.906), (0.045, 1.925), (0.036, 1.943), (0.0, 1.950)], 10), flat("gold"),
              at=(x, 0, 0), sharp=40)
        p.add(box(0.11, 0.11, 0.028, bevel=0.006), flat("gold"), at=(x, 0, 1.196))                 # collars under and over the board
    f = Face(p)
    p.add(box(1.30, 0.05, 0.62, bevel=0.006), flat("vinyl_navy"), at=(0, 0, 1.52))
    for z in (1.235, 1.805):
        f.block((1.22, 0.006, 0.012), (0, 0.027, z), flat("gold"))
    for r in (-0.61, 0.61):
        f.block((0.012, 0.006, 0.582), (r, 0.027, 1.52), flat("gold"))
    f.decal("site_courthouse", 0, 1.455, 1.21, 0.3025, 0.0265)
    f.decal("site_scales", 0, 1.700, 0.17, 0.17, 0.0265)
    for r in (-0.36, 0.36):
        f.star(r, 1.700, 0.042, 0.0265, "gold")
    p.add(box(1.36, 0.085, 0.040, bevel=0.010), flat("gold"), at=(0, 0, 1.850))
    p.add(box(1.24, 0.060, 0.016, bevel=0.004), flat("gold"), at=(0, 0, 1.202))
    return p


# ---------------------------------------------------------------------------------------------- the campaign

def party(number):
    return "site_party_%d" % number


def poster_art(f, number, y, lift=0.0015):
    """The printed side of a campaign poster 0.60 x 0.85 about the face's origin: a white band of stars top and bottom
    in the party's colour (the ground the art is laid on), the candidate, VOTE."""
    f.panel(-0.28, -0.305, 0.28, 0.325, y + lift, "paper")
    for r in (-0.17, 0.0, 0.17):
        f.star(r, 0.375, 0.036, y + lift)
    for k in range(5):
        f.star(-0.22 + 0.11 * k, -0.365, 0.028, y + lift)
    f.decal("site_face_%d" % number, 0, 0.135, 0.28, 0.35, y + 2 * lift)
    for r in (-0.205, 0.205):
        for z in (0.215, 0.055):
            f.star(r, z, 0.040, y + 2 * lift, party(number))
    f.decal("site_vote", 0, -0.150, 0.56, 0.1845, y + 2 * lift)
    f.panel(-0.23, -0.283, 0.23, -0.262, y + 2 * lift, party(number))


def campaign_poster(number):
    def make():
        p = Prop("campaign_poster_%d" % number, "lit", pivot="centre of its back face as the code hangs it (0.012 m off the wall)",
                 note="election poster 0.60 x 0.85 m on a mount 0.014 thick, printed side +Y: stars on the party's colour, the candidate, VOTE (capitals 0.14 m); "
                      "the mount reaches 0.010 m BEHIND the pivot so that no gap shows at the wall")
        p.add(box(0.60, 0.014, 0.85), flat(party(number)), at=(0, -0.003, 0))
        poster_art(Face(p), number, 0.004)
        return p
    return make


def a_frame(p, paint):
    """The sandwich board's two leaning faces (0.60 x 0.93 x 0.46 over all); returns a Face for each, whose origin is
    the middle of the panel and whose surface is y 0.011."""
    faces = []
    for side, turn in ((1, 0), (-1, 180)):
        p.add(box(0.60, 0.022, 0.95, bevel=0.004), paint, at=(0, side * 0.115, 0.465), rot=(side * BOARD_LEAN, 0, 0))
        faces.append(Face(p, at=(0, side * 0.115, 0.465), rot=(BOARD_LEAN, 0, turn)))
    p.add(box(0.58, 0.03, 0.02), flat("steel_dark"), at=(0, 0, 0.925))
    for x in (-0.288, 0.288):
        rod(p, (x, -0.150, 0.30), (x, 0.150, 0.30), 0.004, "steel_dark", 4, caps=False)
    return faces


def campaign_board(number):
    def make():
        p = Prop("campaign_board_%d" % number, "lit",
                 note="campaign A-board, the sandwich board's size (0.60 x 0.46 x 0.93 m), in the party's colour; the same on both faces (+Y and -Y): "
                      "the candidate, VOTE (capitals 0.13 m), stars")
        for f in a_frame(p, Paint(party(number))):
            f.panel(-0.26, -0.11, 0.26, 0.435, 0.0125, "paper")
            f.decal("site_face_%d" % number, 0, 0.265, 0.24, 0.30, 0.0140)
            for r in (-0.19, 0.19):
                for z in (0.335, 0.195):
                    f.star(r, z, 0.038, 0.0140, party(number))
            f.decal("site_vote", 0, 0.005, 0.50, 0.165, 0.0140)
            for r in (-0.17, 0.0, 0.17):
                f.star(r, -0.245, 0.050, 0.0125)
        return p
    return make


def polling_sign():
    p = Prop("polling_sign", "lit",
             note="polling-place A-board, the sandwich board's size (0.60 x 0.46 x 0.93 m), white; both faces alike: POLLING PLACE on a blue band, VOTE HERE "
                  "(capitals 0.125 m), a ballot going into its box. No arrow: the code stands it beside the door facing along the wall, read from both sides")
    for f in a_frame(p, Paint("paint_white")):
        f.panel(-0.28, 0.345, 0.28, 0.455, 0.0125, "paint_blue")
        f.decal("site_polling", 0, 0.400, 0.50, 0.0673, 0.0140)
        f.panel(-0.27, -0.03, 0.27, 0.325, 0.0125, "paper")
        f.decal("site_vote", 0, 0.235, 0.50, 0.165, 0.0140)
        f.decal("site_here", 0, 0.065, 0.44, 0.1595, 0.0140)
        f.disc(0, -0.215, 0.150, 0.0125, "paint_blue")
        f.panel(-0.085, -0.305, 0.085, -0.215, 0.0140, "paper")                       # the box
        f.panel(-0.050, -0.224, 0.050, -0.212, 0.0155, "gloss_black")                 # its slot
        f.poly([(-0.012, -0.215), (0.075, -0.190), (0.047, -0.095), (-0.040, -0.120)], 0.0132, "paper")    # the ballot
        f.poly([(-0.004, -0.150), (0.010, -0.168), (0.046, -0.128), (0.038, -0.122), (0.011, -0.152), (0.002, -0.142)], 0.0146, "paint_blue")   # a tick on it
        f.panel(-0.28, -0.445, 0.28, -0.405, 0.0125, "paint_blue")
    return p


def soapbox():
    p = Prop("soapbox", "lit",
             note="a wooden crate upside down to stand on, 0.60 x 0.45 x 0.30 m: THE TOP IS z 0.30 and flat all over (the code stands the speaker 0.30 up); "
                  "slatted long sides, panelled ends with a hand hole each")
    p.add(box(0.56, 0.40, 0.27), flat("wood_dark"), at=(0, 0, 0.145))                      # what shows through the gaps
    for y in (-0.151, 0.0, 0.151):                                                         # three boards, 4 mm between them
        p.add(box(0.60, 0.147, 0.014, bevel=0.002), flat("wood"), at=(0, y, 0.293))
    for side in (-1, 1):
        for z in (0.041, 0.139, 0.237):
            p.add(box(0.592, 0.014, 0.082, bevel=0.003), "wood", at=(0, side * 0.212, z))
        for x in (-0.262, 0.0, 0.262):
            p.add(box(0.058 if x else 0.050, 0.008, 0.286), flat("wood"), at=(x, side * 0.221, 0.143))
        p.add(box(0.016, 0.44, 0.286), "wood", at=(side * 0.290, 0, 0.143))
        p.add(box(0.004, 0.115, 0.032), flat("gloss_black"), at=(side * 0.2975, 0, 0.215))   # the hand hole
        for y in (-0.185, 0.185):
            p.add(box(0.004, 0.05, 0.286), flat("wood"), at=(side * 0.298, y, 0.143))
    return p


# ---------------------------------------------------------------------------------------------- the scaffold

def scaffold_frame(p, height, brace=True):
    """The tubes of one lift: four standards on base plates or spigots, a ledger along each row, four transoms over
    them, a brace across the wall side. The boards go on the transoms, their top at `height`."""
    ledger, transom = height - 0.102, height - 0.054
    for x in (-STANDARD_X, STANDARD_X):
        for y in STANDARD_Y:
            scaffold_tube(p, (x, y, 0.0), (x, y, height))
    for y, inside in ((STANDARD_Y[0] + 2 * TUBE, 1), (STANDARD_Y[1] - 2 * TUBE, -1)):
        scaffold_tube(p, (-BAY / 2, y, ledger), (BAY / 2, y, ledger))
        for x in (-STANDARD_X, STANDARD_X):
            coupler(p, (x, y - inside * TUBE, ledger))
    for x in (-1.13, -0.40, 0.40, 1.13):
        scaffold_tube(p, (x, 0.0, transom), (x, 1.80, transom))
    if brace:
        y = STANDARD_Y[0] - 2 * TUBE
        scaffold_tube(p, (-1.15, y, 0.32), (1.15, y, height - 0.36))
        for x, z in ((-STANDARD_X, 0.32), (STANDARD_X, height - 0.36)):
            coupler(p, (x, y + TUBE, z + (0.012 if x < 0 else -0.012)))
    width = (DECK_Y[1] - DECK_Y[0]) / 2
    for k, right in enumerate(((1, 0, 0), (-1, 0, 0))):                                    # two widths of four boards
        y = DECK_Y[0] + width * (k + 0.5)
        boards = Paint("wood").planar("site_planks", (0, 0, 1), right, (0, y, 0), (BAY, width)).planar("site_planks", (0, 0, -1), right, (0, y, 0), (BAY, width))
        p.add(moved(box(BAY, width, 0.03), at=(0, y, height - 0.015)), boards)


def scaffold_bay():
    p = Prop("scaffold_bay", "lit", pivot="the ground at the middle of its wall side; it stands out towards +Y",
             note="the first lift of a tube scaffold, 2.40 (x) x 1.80 (y 0 to 1.80) x 2.40 m: standards at (+-1.175, 0.06) and (+-1.175, 1.74) on base plates, "
                  "ledgers 2.30 up, boarded deck from z 2.37 to 2.40 (y 0.09 to 1.71); a brace across the wall side only, so that people walk through "
                  "along x; yellow sleeves on the street-side standards. Tiles every 2.40 m; scaffold_bay_upper stands on it")
    scaffold_frame(p, 2.40)
    for x in (-STANDARD_X, STANDARD_X):
        for y in STANDARD_Y:
            p.add(box(0.12, 0.15, 0.012), flat("steel_dark"), at=(x - math.copysign(0.036, x), y, 0.006))      # inside the bay's width: the next bay's is beside it
            p.add(lathe([(0.034, 0.012), (0.034, 0.10), (0.050, 0.10), (0.050, 0.125), (0.026, 0.125)], 6), flat("steel_dark"), at=(x, y, 0), sharp=40)
        p.add(tube([(x, STANDARD_Y[1], 0.30), (x, STANDARD_Y[1], 1.85)], 0.040, 8, caps=True), Paint("paint_yellow"), sharp=70)
    return p


def scaffold_bay_upper():
    p = Prop("scaffold_bay_upper", "lit", pivot="as scaffold_bay's, on the deck below: the code puts it 2.40 m up, then every 2.00 m",
             note="a lift above the first, 2.40 x 1.80 x 2.00 m, the same plan: its own boarded deck from z 1.97 to 2.00; on the street side (y 1.69) guard rails "
                  "1.00 and 0.50 m up and a toe board to 0.15 m, which guard the deck it stands on; spigot collars at the foot of the standards")
    scaffold_frame(p, 2.00)
    for x in (-STANDARD_X, STANDARD_X):
        for y in STANDARD_Y:
            p.add(tube([(x, y, 0.0), (x, y, 0.11)], 0.031, 6), flat("steel_dark"), sharp=70)
    y = STANDARD_Y[1] - 2 * TUBE
    for z in (1.00, 0.50):
        scaffold_tube(p, (-BAY / 2, y, z), (BAY / 2, y, z))
        for x in (-STANDARD_X, STANDARD_X):
            coupler(p, (x, y + TUBE, z))
    p.add(box(BAY - 0.11, 0.03, 0.148, bevel=0.003), "wood", at=(0, y - 0.005, 0.076))
    return p


def scaffold_net():
    p = Prop("scaffold_net", "lit", pivot="as scaffold_bay_upper's: the net is 1.80 m out from it",
             note="debris netting for one upper bay: green, 2.38 x 1.96 m at y 1.80 (z 0.02 to 1.98), the same from both sides, a little slack; it is not see-through")

    def cloth(u, v):
        slack = math.sin(math.pi * u) * math.sin(math.pi * v)
        return (1.19 - 2.38 * u, 1.800 + 0.007 * slack * math.sin(u * 9.0 + v * 5.0) + 0.004 * slack, 0.02 + 1.96 * v)
    p.add(tiled(cloth, 8, 6, both_sides=True), flat("plastic_green").param("site_net"), sharp=180)
    return p


def scaffold_fan():
    p = Prop("scaffold_fan", "lit", pivot="as scaffold_bay's (it hangs 2.1 to 2.9 m above the pivot)",
             note="the protection fan over the walkway: boards 2.40 wide sloping up 28 degrees from the first deck's outer edge (y 1.80, z 2.41) to (y 2.60, z 2.83), "
                  "on two raking tubes from the street-side standards (y 1.74, z 2.06)")
    at, rot = (0, 2.20, 2.62), (28, 0, 0)
    p.add(box(BAY, 0.90, 0.036), "wood", at=at, rot=rot)
    for side in (1, -1):
        part = grid(lambda u, v, s=side: (s * (u - 0.5) * BAY, (v - 0.5) * 0.90, s * 0.0195), 1, 1)
        p.add(part, flat("wood").param("site_planks"), at=at, rot=rot, sharp=180)
    slope = Vector((0.0, math.cos(math.radians(28)), math.sin(math.radians(28))))
    for x in (-1.10, 1.10):
        tip = Vector((x, 2.20, 2.62)) + slope * 0.30 - Vector((0, -slope.z, slope.y)) * (0.018 + TUBE)
        scaffold_tube(p, (x, STANDARD_Y[1] + 0.01, 2.06), tuple(tip))
        coupler(p, (x, STANDARD_Y[1] + 0.02, 2.075))
        foot = Vector((x, 2.20, 2.62)) - slope * 0.40 - Vector((0, -slope.z, slope.y)) * (0.018 + TUBE)
        scaffold_tube(p, (x, STANDARD_Y[1] + 0.01, 2.30), tuple(foot))
    return p


# ---------------------------------------------------------------------------------------------- the building site

def hoarding_panel(p):
    """One painted timber hoarding panel 2.40 x 2.40, public face +Y at y 0.032; the frame and its block feet behind,
    nothing further back than y -0.06. Returns the Face of the painted side."""
    p.add(box(BAY, 0.014, 2.40), "wood", at=(0, 0.019, 1.20))
    p.add(box(BAY, 0.006, 2.40), "paint_blue", at=(0, 0.029, 1.20))
    for x in (-0.6, 0.6):
        p.add(box(0.07, 0.072, 2.23), "wood", at=(x, -0.024, 1.265))
        p.add(box(0.44, 0.072, 0.15, bevel=0.012), "concrete", at=(x, -0.024, 0.075))
    for z in (0.50, 1.25, 2.00):
        p.add(box(BAY, 0.040, 0.07), flat("wood"), at=(0, -0.008, z))
    f = Face(p)
    f.block((BAY, 0.006, 0.14), (0, 0.035, 2.30), flat("paint_white"))                      # the white band along the top
    f.block((BAY, 0.008, 0.10), (0, 0.036, 0.05), "plastic_grey")                           # the kick board
    return f


def site_hoarding():
    p = Prop("site_hoarding", "lit",
             note="one hoarding panel 2.40 x 2.40 m, 0.10 deep (y -0.06 to 0.04), painted side +Y: blue, a white band from z 2.23 to 2.37, a DANGER KEEP OUT sign "
                  "0.95 x 0.41 (capitals 0.14 m) centred 1.70 m up and a hard-hat roundel either side; timber frame and block feet behind. Tiles every 2.40 m")
    f = hoarding_panel(p)
    f.block((0.98, 0.006, 0.44), (0, 0.035, 1.70), flat("aluminium"))
    f.decal("site_sign", 0, 1.70, 0.95, 0.4125, 0.0395)
    for r in (-0.86, 0.86):
        f.decal("site_hard_hat", r, 1.70, 0.32, 0.32, 0.0335)
    return p


def site_hoarding_notice():
    p = Prop("site_hoarding_notice", "lit",
             note="site_hoarding carrying the 'opening soon' board instead of the warning sign: white, 1.20 x 0.80 m centred 1.50 m up, OPENING SOON "
                  "(capitals 0.17 m), its face at y 0.040; hard-hat roundels either side")
    f = hoarding_panel(p)
    f.block((1.20, 0.008, 0.80), (0, 0.036, 1.50), flat("paint_white"), bevel=0.002)
    for z in (1.885, 1.115):
        f.block((1.20, 0.010, 0.03), (0, 0.035, z), flat("aluminium"))
    f.decal("site_opening", 0, 1.595, 1.10, 0.474, 0.0415)
    f.panel(-0.50, 1.285, 0.50, 1.315, 0.0415, "paint_red")
    f.panel(-0.36, 1.215, 0.36, 1.250, 0.0415, "plastic_grey")
    f.panel(-0.24, 1.160, 0.24, 1.190, 0.0415, "plastic_grey")
    for r in (-0.90, 0.90):
        f.decal("site_hard_hat", r, 1.70, 0.32, 0.32, 0.0335)
    return p


def site_notice_board():
    p = Prop("site_notice_board", "lit",
             note="works notice 0.90 x 0.45 x 1.80 m read from +Y: two galvanised posts at x +-0.38 on weighted feet 0.45 long; the board 0.90 x 0.65 centred "
                  "1.45 m up, its face at y 0.040: NOTICE (capitals 0.13 m) on a blue band, a site plan and a sheet of small print under it")
    for side in (-1, 1):
        x = side * 0.38
        p.add(box(0.05, 0.05, 1.77, bevel=0.005), "aluminium", at=(x, 0, 0.915))
        p.add(box(0.062, 0.062, 0.02, bevel=0.004), flat("plastic_grey"), at=(x, 0, 1.79))
        p.add(box(0.10, 0.45, 0.06, bevel=0.014), "rubber_black", at=(x, 0, 0.03))
        p.add(box(0.075, 0.075, 0.05, bevel=0.008), flat("steel_dark"), at=(x, 0, 0.082))
    p.add(box(0.90, 0.020, 0.65, bevel=0.003), flat("paint_white"), at=(0, 0.030, 1.45))
    for z in (1.20, 1.70):
        p.add(box(0.82, 0.016, 0.04), flat("aluminium"), at=(0, 0.012, z))
    f = Face(p)
    f.panel(-0.43, 1.575, 0.43, 1.755, 0.0415, "paint_blue")
    f.decal("site_notice", 0, 1.665, 0.69, 0.1784, 0.0430)
    f.decal("map_print", -0.205, 1.350, 0.40, 0.2727, 0.0415)
    f.decal("clipboard_page", 0.275, 1.350, 0.24, 0.336, 0.0415)
    for r, z in ((-0.385, 1.468), (-0.025, 1.468), (0.275, 1.500)):
        p.add(lathe([(0.008, 0.0), (0.008, 0.005), (0.0, 0.007)], 6), flat("paint_red"), at=(-r, 0.0415, z), rot=(-90, 0, 0), sharp=60)
    return p


def skip_bin():
    p = Prop("skip_bin", "lit",
             note="builder's skip 3.40 (x) x 1.70 x 1.25 m: sloped ends, rim rail, four lifting lugs, CITY SKIPS on both long sides (capitals 0.20 m), "
                  "rubble and timber to within 0.15 m of the rim")
    half, low, wide = 1.66, 1.02, 0.78
    shell = [[(x, y, 0.07) for x, y in rect(2 * low, 2 * wide)], [(x, y, 1.17) for x, y in rect(2 * half, 2 * wide)], [(x, y, 1.17) for x, y in rect(3.40, 1.64)],
             [(x, y, 1.25) for x, y in rect(3.40, 1.64)], [(x, y, 1.25) for x, y in rect(3.28, 1.50)]]
    p.add(loft(shell, cap_start=True), "paint_yellow")
    fill = 0.88                                                                           # where the rubble meets the sides
    inner = low + (half - low) * (fill - 0.07) / 1.10 - 0.02
    p.add(loft([[(x, y, 1.25) for x, y in rect(3.28, 1.50)], [(x, y, 1.15) for x, y in rect(3.22, 1.50)], [(x, y, fill) for x, y in rect(2 * inner, 1.50)]]), flat("wood_dark"))
    rng = np.random.default_rng(907)
    bumps = rng.uniform(-1.0, 1.0, (7, 10))

    def heap(u, v):
        inside = math.sin(math.pi * u) ** 0.6 * math.sin(math.pi * v) ** 0.6
        lump = bumps[min(6, int(v * 6.999)), min(9, int(u * 9.999))] if 0.0 < u < 1.0 and 0.0 < v < 1.0 else 0.0
        return ((u - 0.5) * 2 * inner, (v - 0.5) * 1.50, fill + 0.16 * inside + 0.05 * lump * inside)
    p.add(tiled(heap, 9, 6), flat("concrete").param("site_rubble"), sharp=60)
    for at, turn, size, paint in (((0.35, 0.10, 1.07), (7, -9, 24), (1.7, 0.20, 0.04), "wood"), ((-0.55, -0.18, 1.05), (-10, 6, -38), (1.2, 0.18, 0.04), "wood"),
                                  ((-0.2, 0.35, 1.06), (12, 4, 80), (0.215, 0.10, 0.065), "sausage"), ((0.8, -0.3, 1.02), (-8, 14, 15), (0.215, 0.10, 0.065), "cardboard"),
                                  ((0.15, -0.42, 1.00), (20, -6, 130), (0.215, 0.10, 0.065), "sausage"), ((-0.95, 0.25, 0.99), (5, 22, 50), (0.215, 0.10, 0.065), "cardboard"),
                                  ((1.05, 0.30, 0.97), (-14, -18, 100), (0.44, 0.215, 0.10), "concrete")):
        p.add(box(*size), paint, at=at, rot=turn)
    for sy in (-1, 1):
        for x in (-0.72, 0.72):
            p.add(box(0.07, 0.035, 1.08, bevel=0.008), "paint_yellow", at=(x, sy * 0.7975, 0.63))
        for x in (-1.15, 1.15):                                                           # a lifting lug: plate, pin and keeper
            p.add(box(0.20, 0.022, 0.26), flat("paint_yellow"), at=(x, sy * 0.791, 0.90))
            p.add(tube([(x, sy * 0.78, 0.92), (x, sy * 0.832, 0.92), (x, sy * 0.833, 0.92), (x, sy * 0.85, 0.92)], [0.028, 0.028, 0.046, 0.046], 6), flat("steel_dark"), sharp=50)
        sticker(p, "site_skip_stencil", (0, sy * 0.78, 0.74), (0, sy, 0), (-sy, 0, 0), (1.37, 0.306), lift=0.002)
        p.add(box(1.90, 0.10, 0.07), "steel_dark", at=(0, sy * 0.45, 0.035))
        for sx in (-1, 1):
            strut(p, (sx * (low + 0.02), sy * 0.36, 0.08), (sx * (half + 0.012), sy * 0.36, 1.17), 0.045, 0.07, "paint_yellow")
    slope = Vector((1.10, 0.0, -(half - low))).normalized()
    for sx in (-1, 1):                                                                    # the striped marking low on each end
        sticker(p, "awning", (sx * (low + (half - low) * 0.30 / 1.10), 0, 0.37), (sx * slope.x, 0, slope.z), (0, sx, 0), (1.30, 0.26), lift=0.003)
    return p


# ---------------------------------------------------------------------------------------------- road works

def road_sign_works():
    p = Prop("road_sign_works", "lit",
             note="ROAD WORK AHEAD (capitals 0.16 m) on an orange board 0.75 m square with rounded corners, its face +Y at y 0.026, bottom edge 0.775 m up; "
                  "on a steel stand 0.60 deep with a sandbag over its back rail; 0.75 x 0.60 x 1.55 m over all")
    face = Paint("aluminium", grade=False).planar("site_road_work", (0, 1, 0), (-1, 0, 0), (0, 0, 1.15), (0.75, 0.75))
    p.add(moved(prism(rounded_rect(0.75, 0.75, 0.055, 3), 0.014, 0.026), rot=(-90, 0, 0), at=(0, 0, 1.15)), face, sharp=40)
    for x in (-0.30, 0.30):
        p.add(box(0.04, 0.028, 1.50, bevel=0.004), "steel_dark", at=(x, 0.0, 0.79))
        p.add(box(0.05, 0.036, 0.022, bevel=0.004), flat("rubber_black"), at=(x, 0.0, 1.539))
        p.add(box(0.05, 0.60, 0.04, bevel=0.006), "steel_dark", at=(x, 0, 0.02))
        strut(p, (x, -0.275, 0.04), (x, -0.012, 1.02), 0.036, 0.026, "steel_dark")
        strut(p, (x, 0.275, 0.04), (x, 0.0, 0.52), 0.036, 0.026, "steel_dark")
    for z in (0.86, 1.44):
        p.add(box(0.64, 0.012, 0.045), flat("steel_dark"), at=(0, 0.008, z))
    p.add(box(0.60, 0.035, 0.035), flat("steel_dark"), at=(0, -0.20, 0.058))
    bag = lathe([(0.0, -0.21), (0.050, -0.19), (0.085, -0.10), (0.092, 0.0), (0.085, 0.10), (0.050, 0.19), (0.0, 0.21)], 8)
    p.add(bag, flat("nylon_orange"), at=(0.02, -0.20, 0.118), rot=(0, 90, 6), scale=(0.62, 1.0, 1.0), sharp=70)
    return p


def periodic(y, seed, waves=3):
    """Noise along a 2 m piece that comes back to where it started, so that pieces laid end to end join."""
    return sum(math.sin(math.pi * (k + 1) * y + seed * (1.7 + k)) / (k + 1.5) for k in range(waves)) / 1.2


def trench_cover():
    p = Prop("trench_cover", "lit",
             note="2.0 m of open trench, 1.00 x 2.00 m lying along Y and 0.02 high: a painted trench 0.56 wide (cut sides, an iron main and a red duct in the bottom) "
                  "between two banks of broken surface and spoil with lifted slabs. Its ends join the next piece's: laid every 2.00 m")
    p.add(grid(lambda u, v: ((u - 0.5) * 0.62, (v - 0.5) * 2.0, 0.003), 1, 1), flat("paper").param("site_trench"), sharp=180)
    for side in (-1, 1):
        for k in range(4):
            y0 = -1.0 + 0.5 * k

            def bank(u, v, side=side, y0=y0):
                y = y0 + 0.5 * v
                outer = 0.462 + 0.038 * max(-1.0, min(1.0, periodic(y, 3.0 + side)))
                inner = 0.285 + 0.018 * periodic(y, 7.0 - side, 4)
                rise = (0.0, 0.0125, 0.0165, 0.006)[round(u * 3)] * (1.0 + 0.2 * periodic(y, 11.0 + side, 4))
                return (side * (outer + (inner - outer) * (0.0, 0.30, 0.72, 1.0)[round(u * 3)]), y, min(0.020, rise))
            part = outward(grid(bank, 3, 3))
            cx = side * 0.385
            p.add(part, Paint("bristle", grade=False).planar("site_rubble", (0, 0, 1), (1, 0, 0), (cx, y0 + 0.25, 0), (0.50, 0.50), facing=0.2), sharp=180)
    rng = np.random.default_rng(859)
    for k in range(7):                                                                    # slabs of the old surface, levered up
        side = 1 if k % 2 else -1
        x, y = side * rng.uniform(0.34, 0.40), -0.84 + 0.28 * k + rng.uniform(-0.04, 0.04)
        slab = prism([(-0.07, -0.05), (0.06, -0.06), (0.08, 0.03), (0.01, 0.07), (-0.06, 0.04)], 0.0, 0.010, bottom=False)
        p.add(slab, flat("plastic_grey"), at=(x, y, 0.0075), rot=(rng.uniform(-1.5, 1.5), side * rng.uniform(0.5, 2), rng.uniform(0, 360)), scale=(rng.uniform(0.8, 1.2),) * 2 + (1.0,))
    return p


def spoil_heap():
    p = Prop("spoil_heap", "lit",
             note="what came out of the trench: a heap of earth and broken surface 0.90 m across and 0.35 m high, its top off centre, slabs of asphalt in it; "
                  "round enough for any heading")
    rng = np.random.default_rng(863)
    lumps = rng.uniform(-1.0, 1.0, (9, 9))

    def heap(u, v):
        a, b = 2 * u - 1, 2 * v - 1
        x, y = a * math.sqrt(1 - b * b / 2), b * math.sqrt(1 - a * a / 2)          # the square drawn in to a circle
        r = min(1.0, math.hypot(x, y))
        lump = lumps[round(v * 8), round(u * 8)] if r < 0.93 else 0.0
        top = (1.0 - r * r)
        turn = math.atan2(y, x)
        skirt = 1.0 - 0.07 * (1.0 + math.sin(3 * turn + 1.0)) * (abs(math.sin(2 * turn)) ** 0.5)      # the foot is not a circle; it still reaches 0.45 on the axes
        height = min(0.350, 0.325 * (1.0 - r ** 1.25) * (1.0 + 0.34 * lump)) - 0.005
        return (0.450 * x * skirt * (1.0 + 0.08 * lump * top) + 0.10 * top, 0.450 * y * skirt * (1.0 + 0.08 * lump * top) - 0.06 * top, height)
    p.add(tiled(heap, 8, 8, cells=2), Paint("bristle").param("site_rubble"), sharp=40)
    for at, turn, size in (((0.27, -0.14, 0.09), (24, 30, 20), (0.20, 0.15, 0.035)), ((-0.20, 0.10, 0.11), (-20, -36, 70), (0.18, 0.13, 0.035)),
                           ((0.05, 0.27, 0.07), (-40, 8, 130), (0.16, 0.12, 0.03)), ((-0.16, -0.20, 0.10), (30, -24, 40), (0.15, 0.11, 0.03))):
        slab = prism([(-0.5, -0.4), (0.4, -0.5), (0.5, 0.2), (0.1, 0.5), (-0.45, 0.35)], -0.5, 0.5)
        p.add(slab, flat("plastic_grey"), at=at, rot=turn, scale=size)
    return p


def road_marking():
    p = Prop("road_marking", "lit", pivot="the road under its middle; the paint is 0.004 m up",
             note="a sprayed setting-out line, yellow, 0.10 x 2.00 m along Y, flat (one-sided, 0.004 m above the road): two strokes with a break, a cross tick "
                  "and an arrow head. Its ends join the next piece's")
    f = lambda points: p.add(skin([(x, y, 0.004) for x, y in points], [list(range(len(points)))]), flat("paint_yellow"), sharp=180)
    for y0, y1 in ((-1.0, -0.10), (0.06, 1.0)):
        count = 6
        left = [(-0.016 - 0.007 * math.sin(7.0 * y0 + 5.0 * k), y0 + (y1 - y0) * k / count) for k in range(count + 1)]
        right = [(0.017 + 0.006 * math.sin(4.0 * y1 + 3.0 * k), y0 + (y1 - y0) * k / count) for k in range(count + 1)]
        left[0], right[0] = (-0.018, y0), (0.018, y0)
        left[-1], right[-1] = (-0.018, y1), (0.018, y1)
        for k in range(count):
            f([left[k], right[k], right[k + 1], left[k + 1]])
    f([(-0.050, -0.535), (0.050, -0.545), (0.050, -0.510), (-0.050, -0.500)])
    f([(0.0, 0.76), (-0.050, 0.66), (-0.036, 0.645), (0.0, 0.715), (0.036, 0.645), (0.050, 0.66)])
    return p


def asphalt_patch():
    p = Prop("asphalt_patch", "lit",
             note="2.0 m of fresh surfacing, 1.20 x 2.00 m lying along Y, 0.012 thick with rolled edges: black, sealed along both sides. Its ends join the next "
                  "piece's: laid every 2.00 m")
    section = ((0.600, 0.0), (0.588, 0.0085), (0.570, 0.012), (-0.570, 0.012), (-0.588, 0.0085), (-0.600, 0.0))
    top = Paint("rubber_black", grade=False).planar("site_asphalt", (0, 0, 1), (1, 0, 0), (0, 0, 0), (1.20, 2.00), facing=0.5)
    p.add(outward(loft([[(x, y, z) for x, z in section] for y in (-1.0, 1.0)], closed=False)), top, sharp=30)
    for y, look in ((-1.0, -1), (1.0, 1)):
        p.add(outward(skin([(x, y, z) for x, z in section], [list(range(len(section)))]), (0, look, 0)), flat("rubber_black"))
    return p


def works_tent():
    p = Prop("works_tent", "lit",
             note="a frame tent over an open cover, 1.50 x 1.50 x 1.90 m, OPEN AT +Y: four legs at (+-0.72, +-0.72), yellow walls on -Y and both x sides from "
                  "z 0.04 to 1.52 with a red and white reflective band 1.0 m up, a pyramid top to 1.90 with a valance, the door rolled up under the front "
                  "eave. No floor: it stands over manhole_open")
    eave, wall, top = 0.75, 0.736, 1.90
    for x in (-0.72, 0.72):
        for y in (-0.72, 0.72):
            p.add(tube([(x, y, 0.0), (x, y, 1.52)], 0.016, 6), Paint("aluminium"), sharp=70)
            p.add(lathe([(0.030, 0.0), (0.030, 0.008), (0.020, 0.012)], 6, cap_bottom=True), flat("plastic_grey"), at=(x, y, 0), sharp=40)
            p.add(tube([(x, y, 1.51), (0.03 * x, 0.03 * y, top - 0.03)], 0.012, 4, caps=False), flat("aluminium"), sharp=70)       # the rafters, seen from inside
    for a, b in (((-0.72, -0.72), (0.72, -0.72)), ((-0.72, 0.72), (0.72, 0.72)), ((-0.72, -0.72), (-0.72, 0.72)), ((0.72, -0.72), (0.72, 0.72))):
        p.add(tube([(*a, 1.505), (*b, 1.505)], 0.014, 5, caps=False), flat("aluminium"), sharp=70)
    for x in (-0.72, 0.72):
        p.add(tube([(x, -0.72, 0.55), (x, 0.72, 0.55)], 0.012, 5, caps=False), flat("aluminium"), sharp=70)

    def cloth(a, b, out):
        def at(u, v):
            slack = math.sin(math.pi * u) * math.sin(math.pi * v)
            q = Vector(a).lerp(Vector(b), u) + out * (0.010 * slack)
            return (q.x, q.y, 0.04 + 1.48 * v)
        return at
    for a, b in (((wall, wall), (wall, -wall)), ((wall, -wall), (-wall, -wall)), ((-wall, -wall), (-wall, wall))):
        along = (Vector(b) - Vector(a)).normalized()
        out = Vector((-along.y, along.x))
        p.add(grid(cloth(a, b, out), 3, 2, both_sides=True), Paint("tent_yellow"), sharp=180)
        mid = (Vector(a) + Vector(b)) / 2
        pane(p, "reflective_white", (mid.x + out.x * 0.0115, mid.y + out.y * 0.0115, 1.00), (out.x, out.y, 0), (-along.x, -along.y, 0), (2 * wall, 0.10), lift=0.0)
        pane(p, "paint_red", (mid.x + out.x * 0.0130, mid.y + out.y * 0.0130, 1.00), (out.x, out.y, 0), (-along.x, -along.y, 0), (2 * wall, 0.034), lift=0.0)
    corners = [(-eave, -eave), (eave, -eave), (eave, eave), (-eave, eave)]
    roof = [(x, y, 1.53) for x, y in corners] + [(0.0, 0.0, top)]
    p.add(skin(roof, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)], both_sides=True), Paint("tent_yellow"), sharp=20)
    hem = [(x, y, 1.53) for x, y in corners] + [(x * 0.992, y * 0.992, 1.41) for x, y in corners]
    p.add(skin(hem, [(1, 0, 4, 5), (2, 1, 5, 6), (3, 2, 6, 7), (0, 3, 7, 4)], both_sides=True), flat("nylon_orange"), sharp=20)       # the valance
    p.add(tube([(-0.70, 0.690, 1.36), (0.70, 0.690, 1.36)], 0.042, 7), flat("tent_yellow"), sharp=70)                                  # the door, rolled up
    for x in (-0.45, 0.45):
        p.add(tube([(x + 0.02 * k, 0.690 + 0.046 * math.sin(a), 1.36 + 0.046 * math.cos(a)) for k, a in enumerate(np.linspace(0, 2 * math.pi, 7)[:-1])], 0.006, 3, closed=True),
              flat("nylon_black"), sharp=100)
    p.add(lathe([(0.075, top - 0.035), (0.05, top - 0.010), (0.0, top)], 6), flat("nylon_orange"), sharp=40)
    return p


def manhole_lid():
    p = Prop("manhole_lid", "lit",
             note="a cast-iron cover 0.66 m across and 0.05 thick, lying flat: tread pattern, CITY SEWER, two key holes. The cover itself when it is in place, "
                  "and the one lifted out and laid beside an open hole")
    top = Paint("steel_dark", grade=False).planar("site_manhole", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.66, 0.66))
    p.add(lathe([(0.318, 0.0), (0.330, 0.008), (0.330, 0.043), (0.323, 0.050)], 16, cap_top=True), top, sharp=40)
    return p


def manhole_open():
    p = Prop("manhole_open", "lit",
             note="an open manhole, 0.90 x 0.90 x 0.06 m: a square of broken-out bedding 0.02 high, the iron frame standing 0.06 up (0.80 across outside, 0.62 "
                  "inside), and the shaft painted inside it (step irons on the -Y side). The lid is manhole_lid, which the code lays 1.25 m along")
    count = 16
    ring, square = [], []
    for k in range(count):
        a = 2 * math.pi * (k + 0.5) / count
        cx, cy = math.cos(a), math.sin(a)
        ring.append((cx * 0.385, cy * 0.385))
        reach = 0.45 / max(abs(cx), abs(cy))
        square.append((cx * reach * (1.0 - 0.035 * (k % 3 == 1)), cy * reach * (1.0 - 0.035 * (k % 3 == 1))))
    rings = [[(x, y, 0.0) for x, y in square], [(x * 0.985, y * 0.985, 0.019) for x, y in square], [(x, y, 0.020) for x, y in ring]]
    p.add(outward(loft(rings)), "concrete", sharp=40)
    frame = [(0.385, 0.012), (0.400, 0.024), (0.400, 0.052), (0.392, 0.060), (0.335, 0.060), (0.330, 0.054), (0.330, 0.036), (0.312, 0.036), (0.312, 0.008)]
    p.add(lathe(frame, 16), flat("steel_dark"), sharp=40)
    hole = [(math.cos(2 * math.pi * k / 16) * 0.312, math.sin(2 * math.pi * k / 16) * 0.312, 0.008) for k in range(16)]
    p.add(skin(hole, [list(range(16))]), Paint("gloss_black", grade=False).planar("site_shaft", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.624, 0.624)), sharp=180)
    return p


# ---------------------------------------------------------------------------------------------- the police station

def office_desk():
    p = Prop("office_desk", "lit",
             note="a detective's desk 1.40 x 0.70 m, top 0.75 m up (1.17 with the screen): WHOEVER WORKS AT IT STANDS AT +Y, its back (-Y) goes to a wall. Wood top "
                  "on a grey steel frame, three drawers on the right (x +0.24 to +0.66) facing +Y, a modesty panel at the back; on it a screen (facing +Y) "
                  "showing a street map, keyboard, mouse, a case file, a tray, a mug. The footprint is exactly the top's: the code centres it by its bounds")
    p.add(box(1.40, 0.70, 0.04, bevel=0.007), flat("wood"), at=(0, 0, 0.73))
    p.add(box(0.035, 0.62, 0.69, bevel=0.004), "steel_dark", at=(-0.665, -0.01, 0.345))
    p.add(box(0.42, 0.62, 0.59, bevel=0.006), "steel_dark", at=(0.45, -0.01, 0.415))
    p.add(box(0.38, 0.56, 0.12), flat("plastic_grey"), at=(0.45, -0.02, 0.06))
    p.add(box(0.87, 0.018, 0.42), "steel_dark", at=(-0.212, -0.30, 0.47))
    for k, (z, high) in enumerate(((0.615, 0.13), (0.475, 0.13), (0.275, 0.25))):
        p.add(box(0.385, 0.012, high - 0.012, bevel=0.003), flat("aluminium"), at=(0.45, 0.304, z))
        p.add(box(0.14, 0.016, 0.014, bevel=0.004), flat("gloss_black"), at=(0.45, 0.316, z + high / 2 - 0.035))
    for x in (-0.665, 0.275, 0.625):
        p.add(box(0.05, 0.05, 0.012), flat("rubber_black"), at=(x, -0.27, 0.006))
        p.add(box(0.05, 0.05, 0.012), flat("rubber_black"), at=(x, 0.25, 0.006))
    # The screen on its stand, leaning back, turned to whoever works here.
    p.add(lathe([(0.11, 0.750), (0.11, 0.758), (0.035, 0.766), (0.022, 0.770)], 10), flat("plastic_grey"), at=(-0.30, -0.17, 0), scale=(1.0, 0.7, 1.0), sharp=40)
    p.add(box(0.05, 0.022, 0.20), flat("plastic_grey"), at=(-0.30, -0.185, 0.86))
    screen = Face(p, at=(-0.30, -0.165, 1.00), rot=(8, 0, 0))
    screen.block((0.52, 0.030, 0.33), (0, 0, 0), flat("plastic_grey"), bevel=0.008)
    screen.block((0.20, 0.03, 0.14), (0, -0.022, -0.01), flat("plastic_grey"), bevel=0.01)
    screen.decal("map_print", 0, 0.008, 0.475, 0.283, 0.0162)
    p.add(box(0.44, 0.15, 0.018, bevel=0.005), flat("plastic_grey"), at=(-0.30, 0.13, 0.759), rot=(4, 0, 0))
    pane(p, "rubber_black", (-0.30, 0.13, 0.7690), (0, -0.07, 1), (1, 0, 0), (0.41, 0.115), lift=0.001)
    p.add(lathe([(0.030, 0.0), (0.032, 0.010), (0.022, 0.026), (0.0, 0.030)], 8), flat("gloss_black"), at=(0.04, 0.15, 0.750), scale=(1.0, 1.6, 1.0), sharp=50)
    # The case file, the tray and the mug.
    p.add(box(0.24, 0.32, 0.010, bevel=0.002), flat("cardboard"), at=(0.36, 0.08, 0.755), rot=(0, 0, -9))
    p.add(box(0.21, 0.297, 0.006), flat("paper"), at=(0.345, 0.085, 0.763), rot=(0, 0, -5))
    turn = math.radians(-5)
    sticker(p, "clipboard_page", (0.345, 0.085, 0.766), (0, 0, 1), (math.cos(turn), math.sin(turn), 0), (0.21, 0.297), lift=0.0012)
    shell, liner = cup(shifted(rect(0.26, 0.34), 0.50, -0.16), shifted(rect(0.244, 0.324), 0.50, -0.16), 0.750, 0.805, 0.756)
    p.add(shell, flat("plastic_grey"))
    p.add(liner, flat("plastic_grey"))
    p.add(box(0.215, 0.30, 0.022), flat("paper"), at=(0.50, -0.16, 0.768), rot=(0, 0, 3))
    p.add(lathe([(0.034, 0.750), (0.040, 0.754), (0.040, 0.842), (0.035, 0.842), (0.035, 0.830)], 10, cap_top=True), flat("paint_blue"), at=(0.02, -0.12, 0), sharp=40)
    p.add(tube([(0.058, -0.12, 0.825), (0.082, -0.12, 0.815), (0.082, -0.12, 0.780), (0.058, -0.12, 0.770)], 0.006, 4), flat("paint_blue"), sharp=70)
    return p


def evidence_door():
    p = Prop("evidence_door", "lit", pivot="the floor under the middle of its back face (the code hangs it by its bounds)",
             note="a closed steel door in its frame against a wall, 1.04 x 2.12 m, 0.065 proud (y 0 to 0.065), read from +Y: grey leaf 0.90 x 2.04, a blue plate "
                  "lettered EVIDENCE 0.70 x 0.16 centred 1.62 m up (capitals 0.105 m), lever handle and lock at x -0.34 as read from the front (Unity x +0.34), "
                  "1.02 m up, a keypad over it, kick plate, three hinges, a closer on the head. The code letters nothing on the model")
    for x in (-0.485, 0.485):
        p.add(box(0.07, 0.040, 2.12, bevel=0.006), "steel_dark", at=(x, 0.020, 1.06))
    p.add(box(1.04, 0.040, 0.07, bevel=0.006), flat("steel_dark"), at=(0, 0.020, 2.085))
    p.add(box(0.90, 0.020, 2.045), "site_door", at=(0, 0.021, 1.0275))
    f = Face(p)
    for z, high in ((0.62, 0.72), (1.56, 0.84)):                                          # the pressed panels of the leaf
        f.block((0.74, 0.005, high), (0, 0.0325, z), "site_door", bevel=0.002)
    f.block((0.86, 0.004, 0.22), (0, 0.033, 0.13), flat("stainless"))                     # kick plate
    f.block((0.72, 0.006, 0.18), (0, 0.037, 1.62), flat("aluminium"), bevel=0.002)
    f.decal("site_evidence", 0, 1.62, 0.70, 0.1596, 0.0412)
    # Lock side: escutcheon, lever, cylinder, keypad. Unity x +0.34 is r -0.34 for a reader standing at +Z.
    r = -0.34
    f.block((0.055, 0.008, 0.26), (r, 0.039, 1.02), flat("stainless"), bevel=0.003)
    p.add(tube([(-r, 0.040, 1.05), (-r, 0.058, 1.05), (-r - 0.012, 0.058, 1.05), (-r - 0.125, 0.056, 1.05)], 0.0085, 6), flat("stainless"), sharp=70)
    p.add(lathe([(0.014, 0.0), (0.014, 0.006), (0.0, 0.007)], 8), flat("gold"), at=(-r, 0.043, 0.96), rot=(-90, 0, 0), sharp=50)
    f.block((0.085, 0.022, 0.13), (r, 0.046, 1.25), flat("plastic_grey"), bevel=0.005)
    for row in range(4):
        f.block((0.060, 0.004, 0.014), (r, 0.058, 1.205 + 0.024 * row), flat("aluminium"))
    f.block((0.012, 0.004, 0.008), (r + 0.022, 0.058, 1.303), flat("lens_red"))
    for z in (0.25, 1.06, 1.87):                                                          # hinges on the other side
        p.add(tube([(-0.455, 0.046, z - 0.055), (-0.455, 0.046, z + 0.055)], 0.011, 6), flat("stainless"), at=(0.91 if r > 0 else 0.0, 0, 0), sharp=70)
    f.block((0.24, 0.030, 0.05), (r + 0.10, 0.046, 1.985), flat("aluminium"), bevel=0.005)  # the closer and its arm
    p.add(box(0.30, 0.008, 0.014), flat("aluminium"), at=(-r - 0.20, 0.050, 2.035), rot=(0, 8, 0))
    return p


# ---------------------------------------------------------------------------------------------- community service

def litter_pile():
    p = Prop("litter_pile", "lit",
             note="street litter within 0.40 m of the pivot, 0.10 high: a crushed drinks can, a ball of paper, a flattened carton, a paper cup on its side with "
                  "its lid off, a sheet of newspaper, a crisp packet. Round: the code gives it any heading")
    rng = np.random.default_rng(877)
    can = lathe([(0.0, 0.0), (0.026, 0.0), (0.033, 0.008), (0.033, 0.110), (0.027, 0.120), (0.0, 0.120)], 10)
    for v in can.verts:                                                                    # trodden on
        if 0.03 < v.co.z < 0.09:
            v.co.x *= 0.80
            v.co.y *= 0.62
    p.add(can, Paint("aluminium", grade=False).wrap("can_label", 0.0, 0.12), at=(0.125, 0.040, 0.033), rot=(90, 0, 40), sharp=45)
    ball = bmesh.new()
    bmesh.ops.create_icosphere(ball, subdivisions=2, radius=0.052)
    for v in ball.verts:
        v.co = v.co * rng.uniform(0.74, 1.10)
        v.co.z *= 0.86
    p.add(ball, flat("paper"), at=(0.0, -0.015, 0.049), sharp=12)
    p.add(box(0.19, 0.13, 0.014, bevel=0.003), "cardboard", at=(-0.085, 0.065, 0.009), rot=(2, -3, 62))
    p.add(box(0.19, 0.055, 0.004), flat("cardboard"), at=(-0.140, 0.042, 0.029), rot=(0, 16, 62))
    turn = math.radians(62)
    sticker(p, "parcel_label", (-0.075, 0.072, 0.0165), (0.05, 0.0, 1), (math.cos(turn), math.sin(turn), 0), (0.075, 0.052), lift=0.0012)
    p.add(lathe([(0.030, 0.0), (0.031, 0.002), (0.041, 0.108), (0.0425, 0.110)], 10, cap_bottom=True), Paint("paper", grade=False).wrap("cup_side", 0.0, 0.11),
          at=(-0.065, -0.095, 0.036), rot=(84, 0, -70), sharp=40)
    p.add(lathe([(0.0, 0.0), (0.044, 0.0), (0.045, 0.010), (0.036, 0.014), (0.0, 0.015)], 10), flat("plastic_grey"), at=(0.050, -0.140, 0.0), sharp=40)

    def sheet(u, v):
        fold = 0.012 * abs(math.sin(math.pi * u * 2.0)) * (0.4 + 0.6 * v) + 0.010 * (1 - v) * abs(2 * u - 1)
        return ((u - 0.5) * 0.22, (v - 0.5) * 0.15, 0.004 + fold + 0.018 * v * v)
    p.add(grid(sheet, 4, 2, both_sides=True), flat("paper").param("news_front"), at=(0.060, 0.105, 0.0), rot=(0, 0, -24), sharp=180)

    def packet(u, v):
        return ((u - 0.5) * 0.12, (v - 0.5) * 0.085, 0.004 + 0.016 * math.sin(math.pi * v) * (0.4 + 0.6 * math.sin(math.pi * u)) + 0.006 * math.sin(9.0 * u))
    p.add(grid(packet, 4, 2, both_sides=True), flat("lens_red"), at=(0.130, -0.070, 0.0), rot=(0, 0, 75), sharp=30)
    for x, y, a in ((-0.07, 0.175, 30), (-0.02, -0.180, -50), (-0.180, -0.03, 80)):          # cigarette ends
        p.add(tube([(0, -0.012, 0.004), (0, 0.012, 0.004)], 0.004, 4), flat("cardboard"), at=(x, y, 0.0), rot=(0, 0, a), sharp=70)
    return p


# ---------------------------------------------------------------------------------------------- not asked for by code


# (maker, triangle budget). The module's budget is 14 000 triangles; these add up to 13 474 and the props use 12 633.
PROPS = [
    (courthouse_sign, 900), (litter_pile, 500),
    (campaign_poster(0), 160), (campaign_poster(1), 160), (campaign_poster(2), 160), (campaign_poster(3), 160),
    (campaign_board(0), 300), (campaign_board(1), 300), (campaign_board(2), 300), (campaign_board(3), 300), (polling_sign, 240), (soapbox, 600),
    (scaffold_bay, 660), (scaffold_bay_upper, 560), (scaffold_net, 200), (scaffold_fan, 140),
    (site_hoarding, 240), (site_hoarding_notice, 300), (site_notice_board, 500), (skip_bin, 760),
    (road_sign_works, 500), (trench_cover, 260), (spoil_heap, 220), (road_marking, 40), (asphalt_patch, 24), (works_tent, 540), (manhole_open, 360), (manhole_lid, 130),
    (office_desk, 1000), (evidence_door, 660),
]

SIDES = {
    "courthouse_sign": {"references": (((3.0, 0.2, 3.0), (0, -0.40, 1.5)),), "people": (((0.0, 0.95), 180),)},
    "campaign_board_0": {"people": (((0.75, 0.3), 0),)},
    "polling_sign": {"people": (((0.75, 0.3), 0),)},
    "soapbox": {"people": (((0.0, 0.0), 0), ((0.0, 2.3), 180))},
    "scaffold_bay": {"references": (((3.2, 0.2, 5.0), (0, -0.22, 2.5)),), "people": (((0.4, 0.92), 90),)},
    "scaffold_bay_upper": {"people": (((1.8, 0.9), 0),)},
    "site_hoarding": {"people": (((1.55, 0.5), 0),)},
    "site_hoarding_notice": {"people": (((1.55, 0.5), 0),)},
    "site_notice_board": {"people": (((0.0, 0.9), 180),)},
    "skip_bin": {"people": (((2.3, 0.4), 0),)},
    "road_sign_works": {"people": (((0.85, 0.2), 0),)},
    "works_tent": {"people": (((0.0, 1.35), 180),)},
    "office_desk": {"references": (((2.4, 0.1, 2.4), (0, -0.40, 1.2)),), "people": (((-0.3, 0.75), 180),)},
    "evidence_door": {"references": (((2.4, 0.1, 2.4), (0, -0.05, 1.2)),), "people": (((0.95, 0.5), 180),)},
}
