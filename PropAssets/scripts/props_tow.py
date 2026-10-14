"""THE TOW TRUCK: the mod's own city tow truck (Traffic/Tow/TowTruck), a rollback: a cab-over truck with a bed that tilts
back and slides down to the road, and a winch at its front that pulls a wreck up it. Same rules as props.py: metres, X
right, Y forward, Z up, one mesh on the shared atlas; Unity gets (x, z, y). Painted by paint_tow.py, measured by
checks_tow.py.

    tow_truck             the cab and the chassis, on the ground under the middle between its axles (the truck's origin)
    tow_truck_tilt        the tilting subframe: its two rails and the pivot at their back, modelled where they lie at rest
    tow_truck_bed         the bed with its headboard and winch, modelled where it lies at rest
    tow_truck_light_*     its lamps lit, a mesh for each of TowTruck's groups (head, tail, brake, left, right), at the truck's origin
    tow_bar_lens          one end of the amber light bar on the cab roof, lit: shown in turn over each end while it flashes

The wheels are the city bus's (city_bus_wheel, city_bus_wheel_twin), drawn at TYRE / 0.48 of their size: TowTruck puts them
on, spins them and steers the front ones, as BusVehicle does.

THE BED. At rest it lies on the subframe, its top BED_TOP over the road, from BED_TAIL to BED_FRONT. TowTruck turns the
subframe nose up about TILT_PIVOT by up to TILT_MOST degrees and slides the bed back along it until its tail lies on the
road (Traffic/Tow/TowRules.DeckPose: the same numbers). Both are modelled in the truck's own space at rest, so that they
are put on at the truck's origin and moved from there.
"""
import math

from mathutils import Vector

from life_props import flat, pipe, rod, sticker
from prop_kit import Paint, Prop, box, lathe, loft, moved, tube
from props_service import (BUS_TAIL_LENSES, HEAD_LAMPS, HEAD_TURN, Hull, Shell, arch, arch_lip, arch_stations, corner, curve, lamp,
                           lamp_point, plate, uv_rect)

AXLES = (2.30, -2.30)          # TowTruck.AxleFront, AxleRear: the origin is midway between them
TYRE = 0.45                    # TowTruck.WheelRadius: the city bus's wheel drawn at 0.45 / 0.48
WHEEL_SCALE = TYRE / 0.48
TRACK = (0.98, 0.90)           # to the middle of a front wheel and of a rear pair (TowTruck.TrackFront, TrackRear)
ARCH = 0.56
CAB_W = 1.13                   # half the cab's width; the mirrors stand out to 1.33
HALF = 1.20                    # half the bed's width: TowTruck.HalfWidth
NOSE = 3.42                    # the front of the bumper (TowTruck.NoseAhead is this less the front axle)
CAB_FRONT, CAB_BACK = 3.30, 1.50
CAB_ROOF = 2.615               # the middle of the roof
BAND = (0.98, 1.08)            # the amber stripe round the cab
WAIST, CANT = 1.56, 2.32       # the windows' sill and head
CAB_WINDOW = (1.62, 2.98)      # the door's window, along
RAKE = round(CAB_FRONT - 0.30, 4)
BAR_AT, BAR_UP = 2.40, CAB_ROOF            # the light bar on the roof: the middle of its underside (TowTruck.BarAt)
BAR = (1.40, 0.26, 0.16)                   # its length, depth and height
RAILS = (0.40, 0.58, 0.84)                 # the chassis rails: their middles across, their foot and their top
FRAME_FRONT, FRAME_BACK = 3.20, -4.55
TILT_PIVOT = (-4.45, 0.89)                 # along and up: the subframe turns about this (TowTruck.TiltPivot)
TILT_RAILS = (0.40, 0.84, 0.94, -4.50, 0.90)   # the subframe's rails: across, foot, top, back end, front end
BED_TOP, BED_UNDER = 1.06, 0.94
BED_TAIL, BED_FRONT = -4.85, 1.30         # TowTruck.BedTail, BedFront
BED_TAPER = -4.30                          # the underside rises from here to the tail's lip
BED_LIP = 1.03
HEAD_BOARD = (1.17, 1.30, 2.06)            # the headboard: from, to, its top
WINCH = (0.84, 1.18)                       # where the cable leaves the winch: along and up (TowTruck.WinchAt)
PANEL = (0.75, 1.00)                       # the control levers on both sides: along, and the height of their knobs (TowTruck.PanelAhead)
PANEL_OUT = 1.24                           # ...how far out the knobs reach
TOOLS = (1.30, -1.62, 0.40, 0.90, 0.62, 1.15)   # the side lockers: from, to, foot, top, inner face, outer face
LAMP_BACK = -4.66                          # the faces of the tail lamps
TILT_MOST = 13.0                           # degrees (TowRules.TiltMost)


# ---------------------------------------------------------------------------------------------- shapes of its own

def extrude_x(profile, x0, x1):
    """A side profile of (along, up) points, anticlockwise seen from +X, made solid from x0 to x1."""
    from prop_kit import prism
    part = prism([(y, z) for y, z in profile], x0, x1)
    # prism made it in (x, y) with thickness along z: (along, up, across) -> (across, along, up).
    for v in part.verts:
        a, b, c = v.co
        v.co = Vector((c, a, b))
    import bmesh
    bmesh.ops.recalc_face_normals(part, faces=part.faces[:])
    return part


def amber_light_bar(p, at, length, depth, height):
    """A low light bar lying along X, its underside at `at`: two feet, a black base rail and amber lens modules all along,
    ribbed (the decal), with a black divider between each pair."""
    ox, oy, oz = at
    foot, rail = 0.028, 0.032
    for sx in (-1, 1):
        p.add(box(0.11, depth * 0.86, foot + 0.006, bevel=0.007), flat("rubber_black"), at=(ox + sx * length * 0.33, oy, oz + (foot + 0.006) / 2))
    from prop_kit import rounded_rect
    base = [[(ox + x * sx, oy + y, oz + z) for x, y in rounded_rect(length - 0.01, depth * w, 0.05, 2)] for z, w, sx in ((foot, 0.80, 0.98), (foot + 0.008, 0.90, 1.0), (foot + rail, 0.90, 1.0))]
    p.add(loft(base, cap_start=True, cap_end=True), flat("gloss_black"), sharp=40)
    z0, z1 = oz + foot + rail, oz + height - 0.003
    lens = (Paint("lens_amber", grade=False)
            .planar("tow_light_bar", (0, 1, 0), (-1, 0, 0), (ox, oy, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55)
            .planar("tow_light_bar", (0, -1, 0), (1, 0, 0), (ox, oy, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55))
    p.add(loft(bar_rings(ox, oy, z0, z1, length, depth, 0.0), cap_start=True, cap_end=True), lens, sharp=28)
    for sx in (-1, 0, 1):                                  # the dividers between the modules
        p.add(box(0.016, depth, (z1 - z0) + 0.004), flat("gloss_black"), at=(ox + sx * length * 0.25, oy, (z0 + z1) / 2))


def bar_rings(ox, oy, z0, z1, length, depth, grow, half_only=False):
    """The cross-sections of the light bar's lens from end to end (or from its middle to its +X end): a rounded box."""
    half, d = length / 2 + grow, depth / 2 + grow
    stations = [(0.55, 0.60, -half), (0.86, 0.90, -half + 0.035), (1.0, 1.0, -half + 0.10), (1.0, 1.0, half - 0.10), (0.86, 0.90, half - 0.035), (0.55, 0.60, half)]
    if half_only:
        stations = [(1.0, 1.0, 0.010 + grow)] + stations[3:]
    rings = []
    for wide, tall, x in stations:
        top = z0 + (z1 + grow - z0) * tall
        rings.append([(ox + x, oy + y * wide * 0.965, z) for y, z in ((-d, z0), (d, z0), (d * 0.94, z0 + (top - z0) * 0.62), (d * 0.62, top), (-d * 0.62, top), (-d * 0.94, z0 + (top - z0) * 0.62))])
    return rings


# ---------------------------------------------------------------------------------------------- the lamps

def tow_lamps():
    """Every lamp unit on the tow truck, as lamp() makes it: (decal, centre, normal, right, size, depth, mirrored, lit), where
    lit says what in it the code switches, (group, outline): the groups are TowTruck's lamps, tow_truck_light_<group>. The
    city bus's lamp units, made smaller where they had to be: the same decals, so the same outlines light up."""
    units = []
    for side in (1, -1):
        turn = "right" if side > 0 else "left"
        units.append(("svc_headlamp", (side * 0.74, CAB_FRONT, 0.86), (0, 1, 0), (-1, 0, 0), (0.36, 0.205), 0.024, side < 0,
                      (("head", HEAD_LAMPS[0]), ("head", HEAD_LAMPS[1]), (turn, HEAD_TURN))))
        units.append(("svc_bus_tail_lamp", (side * 0.90, LAMP_BACK, 0.64), (0, -1, 0), (1, 0, 0), (0.12, 0.48), 0.022, False,
                      ((turn, BUS_TAIL_LENSES[0]), ("brake", BUS_TAIL_LENSES[1]), ("tail", BUS_TAIL_LENSES[2]))))
        units.append(("svc_bus_repeater", (side * (CAB_W - 0.002), CAB_FRONT - 0.20, 0.88), (side, 0, 0), (0, side, 0), (0.12, 0.05), 0.004, False,
                      ((turn, uv_rect(2 / 32, 2 / 14, 30 / 32, 12 / 14)),)))
    return units


def tow_light(group, paint, note):
    """The lamps of one of TowTruck's groups, lit: each the lens of its lamp unit in the body again, 1.5 mm in front of it."""
    def make():
        p = Prop("tow_truck_light_" + group, "glow", pivot="the tow truck's own: the ground under the middle between its axles", note=note)
        for unit in tow_lamps():
            for part, outline in unit[7]:
                if part == group:
                    p.add(plate([tuple(lamp_point(unit, u, v, 0.0015)) for u, v in outline], unit[2]), flat(paint), sharp=10)
        return p
    return make


# ---------------------------------------------------------------------------------------------- the truck

def tow_truck():
    p = Prop("tow_truck", "lit", pivot="ground centre under the middle between its axles; nose to +Y",
             note="cab-over rollback tow truck without its bed and subframe: axles at y +-2.30 (tyres 0.45), cab from y 1.50 to 3.30, the amber light bar on the "
                  "roof (underside middle (0, 2.40, 2.615)), control levers on both sides at y 0.75 (knobs 1.00 m up, 1.24 out), tail lamps at y -4.66")
    s = Shell(low=0.40, high=1.60, length=8.3)
    s.rule("white", "swatch", "svc_coach_white").rule("blue", "swatch", "tow_blue").rule("amber", "flat", "tow_amber").rule("roof", "flat", "svc_coach_white")
    s.rule("black", "flat", "gloss_black").rule("rubber", "flat", "rubber_black").rule("under", "flat", "rubber_black")
    s.rule("glass", "own", "svc_glass").rule("screen", "own", "svc_screen")
    front = AXLES[0]
    cos30, sin30 = math.cos(math.radians(30)), math.sin(math.radians(30))

    # ---- the cab: forward control, a raked one-piece screen, the stripe round it.
    def cab(y):
        w = CAB_W - corner(y, CAB_FRONT, 0.14, 1) - corner(y, CAB_BACK, 0.06, -1)
        sill = arch(y, front, ARCH, TYRE, 0.66)
        r = 0.10
        cx, cz = w - 0.07 - r, 2.50
        return [(w - 0.03, sill), (w, max(BAND[0], sill + 0.004)), (w, max(BAND[1], sill + 0.008)), (w, WAIST), (w - 0.05, CANT), (w - 0.07, 2.50),
                (cx + r * cos30, cz + r * sin30), (cx + r * sin30, cz + r * cos30), (cx, 2.60), (0.5, 2.61), (0.0, CAB_ROOF)]

    def rake(point, y):
        if y <= RAKE:
            return point
        shift = curve(((WAIST, 0.0), (CANT, 0.17), (2.50, 0.21), (2.60, 0.28), (2.62, 0.30)), point.z)
        return Vector((point.x, point.y - shift * (y - RAKE) / (CAB_FRONT - RAKE), point.z))
    cab_ys = sorted(set([round(y, 4) for y in (CAB_BACK, CAB_BACK + 0.06, RAKE, CAB_FRONT - 0.07, CAB_FRONT)] + list(CAB_WINDOW) + arch_stations(front, ARCH)))
    hull = Hull(s, cab_ys, cab, rake)

    def cab_tag(y0, y1, k, side):
        if k == 3:
            return None if y0 >= CAB_WINDOW[0] - 0.001 and y1 <= CAB_WINDOW[1] + 0.001 else "white"
        return ("blue", "amber", "white")[k] if k < 3 else "roof"
    hull.skin(cab_tag, under="under")
    for side in (1, -1):
        hull.window(CAB_WINDOW[0], CAB_WINDOW[1], 3, side, ends=0.03)
    face = hull.wall(len(cab_ys) - 1, lambda k, m: (None if m == 1 else "white") if k == 3 else ("blue", "amber", "white")[k] if k < 3 else "roof",
                     (0, 1, 0), cuts={3: [CAB_W - 0.15]})
    a, b, c, d = face[(3, 1)]
    s.pane([a, b], [d, c], "rubber", "screen", (0, 1, 0.3), margin=0.03, depth=0.022)
    back = hull.wall(0, lambda k, m: (None if m == 1 else "white") if k == 3 else ("blue", "amber", "white")[k] if k < 3 else "roof", (0, -1, 0), cuts={3: [0.42]})
    a, b, c, d = back[(3, 1)]
    s.pane([a, b], [d, c], "rubber", "glass", (0, -1, 0), margin=0.03, depth=0.02)
    s.add_to(p, sharp=40)

    # ---- the cab's trim: livery, shut lines, handles, steps, mirrors, the front.
    for side in (1, -1):
        fore = (0, side, 0)
        sticker(p, "tow_door", (side * CAB_W, 2.30, 1.32), (side, 0, 0), fore, (1.08, 0.44), lift=0.002)
        for y in (CAB_BACK + 0.09, CAB_FRONT - 0.30):                  # the door's shut lines
            low = max(BAND[0] + 0.02, TYRE + math.sqrt(max(0.0, (ARCH + 0.03) ** 2 - (y - front) ** 2)) + 0.02)
            p.add(box(0.004, 0.010, CANT - low), flat("rubber_black"), at=(side * (CAB_W + 0.001), y, (CANT + low) / 2))
        p.add(box(0.016, 0.12, 0.032), flat("chrome"), at=(side * (CAB_W + 0.006), CAB_BACK + 0.24, 1.48))
        pipe(p, [(side * CAB_W, CAB_BACK + 0.03, 1.20), (side * (CAB_W + 0.04), CAB_BACK + 0.03, 1.20), (side * (CAB_W + 0.035), CAB_BACK + 0.03, 2.10),
                 (side * (CAB_W - 0.06), CAB_BACK + 0.03, 2.10)], 0.013, "chrome", 5, bend=0.03)
        for z in (0.50, 0.80):                                         # the steps up behind the front wheel
            p.add(box(0.20, 0.17, 0.04), flat("aluminium"), at=(side * (CAB_W - 0.08), CAB_BACK + 0.13, z))
        p.add(box(0.03, 0.17, 0.40), flat("gloss_black"), at=(side * (CAB_W - 0.18), CAB_BACK + 0.13, 0.64))
        ym = CAB_FRONT - 0.10
        for z in (1.80, 2.20):                                         # the mirror on two arms ahead of the door
            rod(p, (side * (CAB_W - 0.04), ym - 0.10, z), (side * (CAB_W + 0.17), ym, z), 0.011, "chrome", 5)
        p.add(box(0.05, 0.10, 0.46, bevel=0.015), flat("gloss_black"), at=(side * (CAB_W + 0.20), ym + 0.01, 2.00))
        p.add(box(0.012, 0.16, 0.06), flat("lens_amber"), at=(side * (CAB_W + 0.003), CAB_BACK + 0.40, 2.40))
        arch_lip(p, side * (CAB_W - 0.004), front, ARCH + 0.012, TYRE, side, paint="rubber_black", thick=0.024)
    sticker(p, "tow_grille", (0, CAB_FRONT, 1.30), (0, 1, 0), (-1, 0, 0), (1.20, 0.40), lift=0.006)
    p.add(box(1.24, 0.010, 0.44), flat("chrome"), at=(0, CAB_FRONT + 0.001, 1.30))
    for side in (1, -1):
        lamp(p, "svc_headlamp", (side * 0.74, CAB_FRONT, 0.86), (0, 1, 0), (-1, 0, 0), (0.36, 0.205), depth=0.024, mirrored=side < 0, bezel="chrome")
        lamp(p, "svc_bus_repeater", (side * (CAB_W - 0.002), CAB_FRONT - 0.20, 0.88), (side, 0, 0), (0, side, 0), (0.12, 0.05), depth=0.004)
    for x in (-0.36, 0.0, 0.36):                                       # marker lamps along the brow
        p.add(box(0.08, 0.03, 0.03), flat("lens_amber"), at=(x, CAB_FRONT - 0.30, 2.585))
    for x0, x1 in ((-0.86, -0.22), (0.10, 0.74)):                      # wipers
        rod(p, (x0, CAB_FRONT + 0.004, WAIST + 0.06), (x1, CAB_FRONT - 0.046, WAIST + 0.24), 0.010, "rubber_black", 4)
    # The bumper: a steel bar across the front with a tow eye each side, the plate in the middle.
    p.add(box(2 * CAB_W + 0.04, 0.13, 0.26, bevel=0.03), Paint("steel_dark"), at=(0, NOSE - 0.065, 0.55))
    p.add(box(2 * CAB_W - 0.10, 0.10, 0.06), flat("gloss_black"), at=(0, NOSE - 0.08, 0.40))
    for x in (-0.62, 0.62):
        p.add(lathe([(0.045, 0.0), (0.045, 0.02), (0.03, 0.03), (0.03, 0.06)], 8, cap_top=True), flat("chrome"), at=(x, NOSE, 0.48), rot=(-90, 0, 0), sharp=40)
    p.add(box(0.36, 0.008, 0.17), Paint("paper", grade=False).planar("tow_plate", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.36, 0.17)), at=(0.0, NOSE + 0.004, 0.57))
    # The light bar on the roof (the code shows tow_bar_lens over each end of it in turn), an aerial.
    amber_light_bar(p, (0.0, BAR_AT, BAR_UP), *BAR)
    rod(p, (0.82, CAB_BACK + 0.20, 2.58), (0.82, CAB_BACK + 0.10, 2.86), 0.006, "rubber_black", 4)

    # ---- under the cab, and the chassis.
    p.add(box(1.40, 1.30, 0.50), flat("rubber_black"), at=(0, front, 0.62))                       # nothing is seen through the front wheels
    rod(p, (-0.86, front, TYRE), (0.86, front, TYRE), 0.06, "steel_dark", 6, caps=False)
    for side in (1, -1):
        x = side * RAILS[0]
        p.add(box(0.08, FRAME_FRONT - FRAME_BACK, RAILS[2] - RAILS[1]), flat("gloss_black"), at=(x, (FRAME_FRONT + FRAME_BACK) / 2, (RAILS[1] + RAILS[2]) / 2))
    for y in (CAB_BACK - 0.20, -1.0, AXLES[1] - 0.80, FRAME_BACK + 0.04):                          # cross members
        p.add(box(2 * RAILS[0] - 0.08, 0.08, 0.16), flat("gloss_black"), at=(0, y, 0.70))
    rear = AXLES[1]
    rod(p, (-0.70, rear, TYRE), (0.70, rear, TYRE), 0.07, "steel_dark", 6, caps=False)
    p.add(lathe([(0.17, -0.12), (0.19, -0.04), (0.19, 0.06), (0.13, 0.14)], 8, cap_top=True, cap_bottom=True), flat("steel_dark"), at=(0, rear, TYRE), rot=(90, 0, 0), sharp=40)
    rod(p, (0, rear + 0.14, TYRE + 0.02), (0, CAB_BACK - 0.3, 0.62), 0.045, "steel_dark", 6)      # the shaft to the engine under the cab
    for side in (1, -1):                                               # springs over the rear axle
        p.add(box(0.08, 1.20, 0.07), flat("steel_dark"), at=(side * RAILS[0], rear, RAILS[1] - 0.05))
        p.add(box(0.55, 0.02, 0.44), flat("rubber_black"), at=(side * TRACK[1], rear - TYRE - 0.18, 0.47))      # mud flaps
    # The lockers between the cab and the rear wheels, both sides, and the control levers on their fronts.
    y0, y1, z0, z1, xi, xo = TOOLS
    for side in (1, -1):
        p.add(box(xo - xi, y0 - y1, z1 - z0, bevel=0.012), Paint("aluminium"), at=(side * (xi + xo) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
        for ya, yb in ((y0 - 0.04, PANEL[0] + 0.24), (PANEL[0] - 0.24, y1 + 0.04)):              # the doors, a handle on each
            p.add(box(0.006, ya - yb, z1 - z0 - 0.10), flat("aluminium"), at=(side * (xo + 0.002), (ya + yb) / 2, (z0 + z1) / 2))
            p.add(box(0.02, 0.10, 0.025), flat("chrome"), at=(side * (xo + 0.008), (ya + yb) / 2, z1 - 0.10))
        # The levers: a black panel with five, each with a knob, reaching out under the bed's edge.
        p.add(box(0.03, 0.44, 0.30), flat("gloss_black"), at=(side * (xo + 0.012), PANEL[0], 0.74))
        for k in range(5):
            y = PANEL[0] - 0.16 + 0.08 * k
            rod(p, (side * (xo + 0.02), y, 0.80), (side * (PANEL_OUT - 0.02), y, PANEL[1] - 0.01), 0.008, "chrome", 4)
            p.add(lathe([(0.0, -0.025), (0.018, -0.015), (0.020, 0.0), (0.018, 0.015), (0.0, 0.025)], 6), flat("paint_red" if k == 2 else "gloss_black"),
                  at=(side * PANEL_OUT, y, PANEL[1]), sharp=60)
    # The back: the rear cross member, the underride bar with its stripes, the lamps on brackets.
    p.add(box(2 * HALF - 0.10, 0.08, 0.14), Paint("steel_dark"), at=(0, FRAME_BACK - 0.08, 0.33))
    sticker(p, "tow_stripes", (0, FRAME_BACK - 0.12, 0.33), (0, -1, 0), (1, 0, 0), (2 * HALF - 0.14, 0.11), lift=0.001)
    for x in (-0.40, 0.40):
        p.add(box(0.06, 0.06, 0.40), flat("gloss_black"), at=(x, FRAME_BACK - 0.06, 0.54))
    p.add(box(2 * HALF - 0.30, 0.05, 0.06), flat("gloss_black"), at=(0, LAMP_BACK + 0.05, 0.64))
    for side in (1, -1):
        lamp(p, "svc_bus_tail_lamp", (side * 0.90, LAMP_BACK, 0.64), (0, -1, 0), (1, 0, 0), (0.12, 0.48), depth=0.022)
    p.add(box(0.36, 0.008, 0.17), Paint("paper", grade=False).planar("tow_plate", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.36, 0.17)), at=(0, LAMP_BACK + 0.02, 0.56))
    # The hydraulic ram's mount on the chassis (TowTruck puts the ram itself between it and the subframe).
    p.add(box(0.20, 0.10, 0.10), flat("steel_dark"), at=(0, RAM_FOOT[0], RAM_FOOT[1]))
    return p


RAM_FOOT = (-1.00, 0.60)                   # where the ram is mounted on the chassis: along, up (TowTruck.RamFoot)
RAM_HEAD = (-2.50, 0.84)                   # ...and under the subframe, at rest (TowTruck.RamHead)


def tow_truck_tilt():
    p = Prop("tow_truck_tilt", "lit", pivot="the tow truck's origin: it is modelled where it lies at rest; TowTruck turns it about (0, 0.89, -4.45)",
             note="the tilting subframe: two rails 0.10 m square at x +-0.40 from y -4.50 to 0.90 on the chassis rails, cross members, the pivot tube across at the back")
    across, foot, top, y0, y1 = TILT_RAILS
    for side in (1, -1):
        p.add(box(0.10, y1 - y0, top - foot, bevel=0.008), Paint("steel_dark"), at=(side * across, (y0 + y1) / 2, (foot + top) / 2))
    for y in (y0 + 0.12, RAM_HEAD[0], -0.40, y1 - 0.15):
        p.add(box(2 * across - 0.10, 0.08, 0.06), flat("steel_dark"), at=(0, y, foot + 0.035))
    p.add(box(0.16, 0.12, 0.06), flat("steel_dark"), at=(0, RAM_HEAD[0], RAM_HEAD[1] - 0.01))
    rod(p, (-0.55, TILT_PIVOT[0], TILT_PIVOT[1]), (0.55, TILT_PIVOT[0], TILT_PIVOT[1]), 0.045, "chrome", 8)
    return p


def tow_truck_bed():
    p = Prop("tow_truck_bed", "lit", pivot="the tow truck's origin: it is modelled where it lies at rest (its top 1.06 m up from y -4.85 to 1.30)",
             note="the rollback bed, 2.40 m wide, its top 1.06 m up from the tail at y -4.85 to the headboard; the tail tapers to a 3 cm lip; the winch's cable leaves at (0, 0.84, 1.18)")
    # The deck: one solid, its top the steel planks, its underside rising to the lip at the tail.
    profile = [(BED_TAIL, BED_LIP), (BED_TAPER, BED_UNDER), (BED_FRONT, BED_UNDER), (BED_FRONT, BED_TOP), (BED_TAIL, BED_TOP)]
    deck = (Paint("steel_dark", grade=False)
            .planar("tow_deck", (0, 0, 1), (0, 1, 0), (0, (BED_TAIL + HEAD_BOARD[0]) / 2, BED_TOP), (HEAD_BOARD[0] - BED_TAIL, 2 * HALF), facing=0.9))
    p.add(extrude_x(profile, -HALF, HALF), deck, sharp=30)
    for side in (1, -1):
        # The edge rails, and the red and white reflective strip along each side.
        p.add(box(0.05, HEAD_BOARD[0] - BED_TAIL - 0.10, 0.05, bevel=0.008), Paint("steel_dark"), at=(side * (HALF - 0.025), (HEAD_BOARD[0] + BED_TAIL + 0.10) / 2, BED_TOP + 0.025))
        sticker(p, "tow_reflect", (side * HALF, (BED_FRONT + BED_TAPER) / 2, (BED_TOP + BED_UNDER) / 2), (side, 0, 0), (0, side, 0), (BED_FRONT - BED_TAPER - 0.10, 0.08), lift=0.001)
        for y in (BED_TAIL + 0.70, -1.60, BED_FRONT - 0.40):           # marker lamps on the edge
            p.add(box(0.012, 0.07, 0.035), flat("lens_amber" if y > BED_TAIL + 1.0 else "lens_red"), at=(side * (HALF + 0.004), y, BED_TOP + 0.022))
        p.add(box(0.08, BED_FRONT - BED_TAPER - 0.20, 0.05), flat("gloss_black"), at=(side * 0.55, (BED_FRONT + BED_TAPER) / 2, BED_UNDER - 0.025))   # the runners underneath, inside the rear tyres
    # The headboard: two posts, a top rail, a panel lettered on its back (it faces the road behind), two work lamps.
    hy0, hy1, htop = HEAD_BOARD
    for side in (1, -1):
        p.add(box(0.10, hy1 - hy0, htop - BED_TOP, bevel=0.01), Paint("steel_dark"), at=(side * (HALF - 0.10), (hy0 + hy1) / 2, (BED_TOP + htop) / 2))
        p.add(box(0.12, 0.08, 0.07, bevel=0.01), flat("lens_amber"), at=(side * (HALF - 0.32), hy1 - 0.04, htop + 0.035))
    p.add(box(2 * HALF - 0.10, hy1 - hy0, 0.10, bevel=0.01), Paint("steel_dark"), at=(0, (hy0 + hy1) / 2, htop - 0.05))
    p.add(box(2 * HALF - 0.36, 0.03, htop - BED_TOP - 0.24), flat("steel_dark"), at=(0, hy0 + 0.04, (BED_TOP + htop) / 2 + 0.02))
    sticker(p, "tow_headboard", (0, hy0 + 0.025, (BED_TOP + htop) / 2 + 0.06), (0, -1, 0), (1, 0, 0), (2 * HALF - 0.44, 0.44), lift=0.001)
    # The winch: a drum of cable on a plate, its motor beside it, a roller where the cable leaves.
    wy, wz = 0.98, BED_TOP + 0.13
    p.add(box(0.86, 0.34, 0.04), flat("steel_dark"), at=(0, wy, BED_TOP + 0.02))
    drum = lathe([(0.13, -0.24), (0.13, -0.21), (0.085, -0.20), (0.085, 0.20), (0.13, 0.21), (0.13, 0.24)], 10, cap_top=True, cap_bottom=True)
    p.add(drum, flat("steel_dark"), at=(0, wy, wz), rot=(0, 90, 0), sharp=40)
    p.add(lathe([(0.091, -0.195), (0.091, 0.195)], 10), flat("nylon_black"), at=(0, wy, wz), rot=(0, 90, 0), sharp=40)          # the cable wound on it
    p.add(lathe([(0.075, 0.0), (0.075, 0.24), (0.05, 0.27)], 8, cap_top=True, cap_bottom=True), flat("tow_blue"), at=(0.26, wy, wz), rot=(0, 90, 0), sharp=40)
    for side in (1, -1):
        p.add(box(0.03, 0.26, 0.24), flat("steel_dark"), at=(side * 0.255, wy, BED_TOP + 0.14))
    rod(p, (-0.10, WINCH[0], WINCH[1] - 0.03), (0.10, WINCH[0], WINCH[1] - 0.03), 0.025, "chrome", 6)
    # The tail: a steel lip across it, and the hook of the cable resting on the deck when the bed is empty (TowTruck hides
    # it while the cable is out).
    p.add(box(2 * HALF - 0.02, 0.05, 0.025), Paint("steel_dark"), at=(0, BED_TAIL + 0.03, BED_LIP + 0.0125))
    return p


def tow_bar_lens():
    """The +X half of the cab's light bar again, a hair bigger and in the glow material: TowTruck shows one over each half
    in turn while the truck is at work (the truck itself is one mesh in the lit material). The other half is this one
    turned half round about Y."""
    p = Prop("tow_bar_lens", "glow", pivot="the middle of the underside of the tow truck's light bar (tow_truck's (0, 2.615, 2.40) in Unity); the lens is at +X",
             note="the +X half of the tow truck's 1.40 m amber light bar, 3 mm proud of it all round, for flashing; turn it 180 degrees about Y for the other half")
    length, depth, height = BAR
    grow = 0.003
    z0, z1 = 0.028 + 0.032 + 0.001, height - 0.003
    lens = (Paint("lens_amber", grade=False)
            .planar("tow_light_bar", (0, 1, 0), (-1, 0, 0), (0, 0, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55)
            .planar("tow_light_bar", (0, -1, 0), (1, 0, 0), (0, 0, (z0 + z1) / 2 - 0.004), (length, z1 - z0 - 0.012), facing=0.55))
    p.add(loft(bar_rings(0.0, 0.0, z0, z1, length, depth, grow, half_only=True), cap_start=True, cap_end=True), lens, sharp=28)
    return p


# ---------------------------------------------------------------------------------------------- the deck's motion, for the previews

def deck_pose(t):
    """TowRules.DeckPose again (the numbers must agree; checks_tow.py holds them to the C#): how far the subframe is turned
    (degrees, nose up) and the bed slid back along it (metres) at t, 0 at rest, 1 with the tail on the road."""
    tilt = TILT_MOST * smooth(t / 0.40)
    slide = SLIDE_MOST * smooth((t - 0.30) / 0.70)
    return tilt, slide


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def _slide_most():
    """How far the bed slides with the subframe at TILT_MOST for the top of its tail to come down to 4 cm over the road."""
    a, b = BED_TOP - TILT_PIVOT[1], TILT_PIVOT[0] - BED_TAIL
    th = math.radians(TILT_MOST)
    return (TILT_PIVOT[1] + a * math.cos(th) - b * math.sin(th) - 0.04) / math.sin(th)


SLIDE_MOST = _slide_most()


def posed(name, t, x=0.0, y=0.0):
    """Where to place the tilt or the bed in a scene at deck pose t (the truck's origin at (x, y), facing +Y): (at, pitch)."""
    tilt, slide = deck_pose(t)
    th = math.radians(tilt)
    py, pz = TILT_PIVOT
    # A point v of the part goes to P + R(v - P) - slide * R(forward) (the bed only); R turns +Y up towards +Z.
    def turn(vy, vz):
        return vy * math.cos(th) - vz * math.sin(th), vy * math.sin(th) + vz * math.cos(th)
    ry, rz = turn(-py, -pz)
    oy, oz = py + ry, pz + rz
    if name == "tow_truck_bed":
        fy, fz = turn(1.0, 0.0)
        oy, oz = oy - slide * fy, oz - slide * fz
    return (x, y + oy, oz), tilt


def truck_parts():
    """What TowTruck puts on the body, at rest (kit space, turn about z): the subframe, the bed, the wheels (the bus's, smaller),
    the left ones turned round, and the light bar's two lit halves."""
    parts = [("tow_truck_tilt", (0.0, 0.0, 0.0), 0.0), ("tow_truck_bed", (0.0, 0.0, 0.0), 0.0)]
    for side in (1, -1):
        parts.append(("city_bus_wheel", (side * TRACK[0], AXLES[0], TYRE), 0.0 if side > 0 else 180.0))
        parts.append(("city_bus_wheel_twin", (side * TRACK[1], AXLES[1], TYRE), 0.0 if side > 0 else 180.0))
    return parts


# ---------------------------------------------------------------------------------------------- the list

PROPS = [
    (tow_truck, 5200), (tow_truck_tilt, 300), (tow_truck_bed, 1000), (tow_bar_lens, 60),
    (tow_light("head", "svc_lens_clear", "the tow truck's headlamps lit, two round lamps in each unit"), 80),
    (tow_light("tail", "lens_red", "the tow truck's tail lamps lit, low in each back corner's unit"), 20),
    (tow_light("brake", "lens_red", "the tow truck's brake lamps lit, in each back corner's unit"), 20),
    (tow_light("left", "lens_amber", "the tow truck's left indicators lit: front, back and the repeater on the cab"), 30),
    (tow_light("right", "lens_amber", "the tow truck's right indicators lit: front, back and the repeater on the cab"), 30),
]

# The truck as TowTruck puts it together with its bed at rest, the operator at the levers on the right and somebody in front.
SIDES = {
    "tow_truck": {"parts": truck_parts(), "people": (((PANEL_OUT + 0.46, PANEL[0]), 90.0), ((0.3, 6.0), 180.0)),
                  "views": ((0, 8), (90, 8), (180, 8), (270, 8), (38, 18), (142, 22))},
}
