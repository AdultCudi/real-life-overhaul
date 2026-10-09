"""THE CIVIC PROPS: what makes a designated building a police station (sign, lamp, awning, emblem, the furniture
of its front office), a kerb a bus stop, and a door a hotel's. Same rules as props.py, which lists these through
its EXTRA: one function each, metres, X right, Y forward, Z up, one mesh on the shared atlas. Unity gets (x, z, y):
"faces +Z" in a README is +Y here.

Sizes, pivots and the plain areas that the mod letters itself (the precinct's name, a stop's route numbers, a
hotel's name) are what Law/Precincts.cs, Transport/BusStopDressing.cs and Travel/Hotels.cs assume: change one and
the other must change with it. scripts/checks_civic.py measures them on the built meshes.
"""
import math

import numpy as np
from mathutils import Vector

from life_props import flat, pipe, rect, ribbon, rod, sticker, strut
from paint_civic import SHIELD_WIDTH, shield_outline
from prop_kit import Paint, Prop, box, fillet, grid, lathe, loft, moved, prism, rounded_rect, tube

STAND = (90, 0, 180)                           # turns a part made lying on its back so that it stands and faces +Y
BLUE = "paint_blue"                            # the police blue: the barrier board's


# ---------------------------------------------------------------------------------------------- helpers

def shield(height, thickness, steps=2, bevel=0.0):
    """A police shield lying on its back, top towards +Y, its middle at the origin: stand it up with STAND."""
    return prism([(x * height, y * height) for x, y in shield_outline(steps)], 0.0, thickness, bevel=bevel, bottom=False)


def shield_paint(height, facing=0.8):
    return Paint("gold", grade=False).planar("civic_shield", (0, 0, 1), (1, 0, 0), (0, 0, 0), (SHIELD_WIDTH * height, height), facing=facing)


def stud(p, at, radius=0.009, height=0.006, paint="steel_dark", rot=(0, 0, 0)):
    """A bolt head."""
    p.add(lathe([(radius, 0.0), (radius * 0.8, height)], 5, cap_top=True), flat(paint), at=at, rot=rot, sharp=40)


def ball(p, at, radius, paint, segments=8, stem=0.0):
    """A finial ball standing on `at` (with a short stem under it)."""
    profile = [(radius * 0.45, 0.0), (radius * 0.45, stem)] if stem > 0 else []
    profile += [(radius * math.sin(t), stem + radius * (1.0 - math.cos(t))) for t in np.radians((35, 65, 90, 120, 150))] + [(0.0, stem + radius * 2.0)]
    p.add(lathe(profile, segments), flat(paint), at=at, sharp=40)


def castor(p, x, y, radius, top, paint="gold"):
    """A swivel castor under a deck whose underside is at `top`: the wheel turns about Y."""
    tyre = lathe([(radius * 0.84, -0.014), (radius, -0.008), (radius, 0.008), (radius * 0.84, 0.014)], 8, cap_top=True, cap_bottom=True)
    p.add(tyre, flat("rubber_black"), at=(x, y, radius), rot=(90, 0, 0), sharp=40)
    p.add(box(0.050, 0.060, 0.010), flat(paint), at=(x, y, top - 0.005))
    for side in (-1, 1):
        p.add(box(0.030, 0.004, top - radius), flat(paint), at=(x, y + side * 0.019, (top + radius) / 2))


# ---------------------------------------------------------------------------------------------- the police station

def precinct_sign():
    p = Prop("precinct_sign", "lit", note="read from +Y; two posts 0.07 square at x +-0.86, flat tops 2.90 m up (the right one carries blue_lamp); the board 1.65 x 0.80 m from 2.05 to 2.85 m, "
             "its face at y +0.025; the code letters the precinct's name on the plain strip 1.50 x 0.17 m centred 2.15 m up; nothing behind y -0.04")
    for side in (-1, 1):
        x = side * 0.86
        p.add(box(0.07, 0.07, 2.90, bevel=0.005), "civic_iron", at=(x, 0, 1.45))
        p.add(box(0.11, 0.08, 0.014), flat("steel_dark"), at=(side * 0.845, 0, 0.007))
        p.add(box(0.080, 0.078, 0.05, bevel=0.006), "civic_iron", at=(side * 0.859, 0, 0.039))
        stud(p, (side * 0.805, 0.022, 0.014), 0.008, 0.008)
        stud(p, (side * 0.805, -0.022, 0.014), 0.008, 0.008)
        for z in (2.17, 2.73):          # the clamps that carry the board's rails
            p.add(box(0.080, 0.078, 0.045, bevel=0.005), flat("steel_dark"), at=(x, 0, z))
    board = Paint(BLUE, grade=False).planar("civic_precinct_board", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (1.65, 0.80))
    p.add(box(1.65, 0.045, 0.80), board, at=(0, 0.0025, 2.45))
    for z in (2.17, 2.73):
        p.add(box(1.66, 0.014, 0.040), flat("steel_dark"), at=(0, -0.028, z))
    for x in (-0.795, 0.795):
        for z in (2.085, 2.815):
            stud(p, (x, 0.025, z), 0.007, 0.004, "chrome", rot=(-90, 0, 0))
    return p


def blue_lamp():
    p = Prop("blue_lamp", "glow", pivot="centre of its base", note="the blue lamp of a police station: glass 0.17 m across under a dark cap, 0.27 m high; it sits on a post top")
    p.add(lathe([(0.034, 0.0), (0.034, 0.006), (0.090, 0.013), (0.090, 0.020)], 12, cap_bottom=True), flat("civic_iron"), sharp=40)
    p.add(lathe([(0.085, 0.020), (0.085, 0.238)], 12), Paint("civic_lamp_blue", grade=False).wrap("civic_lamp_glass", 0.020, 0.238), sharp=40)
    p.add(lathe([(0.086, 0.236), (0.100, 0.240), (0.100, 0.249), (0.058, 0.261), (0.017, 0.264), (0.012, 0.270)], 12, cap_top=True), flat("civic_iron"), sharp=40)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        p.add(box(0.009, 0.009, 0.218), flat("civic_iron"), at=(math.cos(a) * 0.0865, math.sin(a) * 0.0865, 0.129), rot=(0, 0, 45 + 90 * k))
    return p


def precinct_desk():
    p = Prop("precinct_desk", "lit", note="station front counter 2.4 m wide, 0.8 m deep; the public stand at +Y under a shelf 1.10 m up; the officer's writing surface is 0.78 m up on the -Y side")
    p.add(box(2.34, 0.70, 0.08), flat("civic_iron"), at=(0, -0.03, 0.04))
    p.add(box(2.40, 0.06, 0.975), "wood_dark", at=(0, 0.30, 0.5675))
    p.add(box(2.40, 0.30, 0.045, bevel=0.008), flat("wood"), at=(0, 0.25, 1.0775))
    gable = [(-0.40, 0.08), (0.27, 0.08), (0.27, 1.055), (0.12, 1.055), (0.12, 0.78), (-0.40, 0.78)]
    for x0, x1 in ((-1.20, -1.15), (1.15, 1.20)):
        p.add(loft([[(x, y, z) for y, z in gable] for x in (x0, x1)], cap_start=True, cap_end=True), "wood_dark")
    p.add(box(2.30, 0.56, 0.040, bevel=0.006), flat("wood"), at=(0, -0.12, 0.76))
    # The fascia the public see: police blue, POLICE between two shields, a kick rail under it.
    p.add(box(2.24, 0.020, 0.64, bevel=0.004), flat(BLUE), at=(0, 0.340, 0.60))
    sticker(p, "civic_police_word", (0, 0.350, 0.60), (0, 1, 0), (-1, 0, 0), (1.60, 0.40), lift=0.0012)
    for x in (-0.90, 0.90):
        p.add(shield(0.38, 0.018, 1, 0.006), shield_paint(0.38), at=(x, 0.350, 0.60), rot=STAND, sharp=40)
    p.add(box(2.30, 0.016, 0.10), flat("aluminium"), at=(0, 0.338, 0.15))
    # The officer's side: a drawer pedestal, the monitor (its back to the public), keyboard, papers, a phone.
    p.add(box(0.46, 0.50, 0.62, bevel=0.006), "civic_grey", at=(0.86, -0.12, 0.43))
    for z in (0.24, 0.44, 0.63):
        p.add(box(0.42, 0.006, 0.165), flat("civic_grey"), at=(0.86, -0.372, z))
        p.add(box(0.14, 0.014, 0.014), flat("chrome"), at=(0.86, -0.382, z + 0.045))
    p.add(box(0.22, 0.16, 0.014), flat("plastic_grey"), at=(-0.40, -0.02, 0.787))
    p.add(box(0.05, 0.03, 0.12), flat("plastic_grey"), at=(-0.40, 0.0, 0.85))
    p.add(box(0.50, 0.035, 0.30, bevel=0.006), flat("plastic_grey"), at=(-0.40, -0.02, 0.945))
    p.add(box(0.46, 0.004, 0.26), flat("gloss_black"), at=(-0.40, -0.0395, 0.948))
    keys = Paint("plastic_grey", grade=False).planar("civic_keyboard", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.40, 0.14))
    p.add(box(0.40, 0.14, 0.016), keys, at=(-0.40, -0.24, 0.788))
    p.add(box(0.21, 0.297, 0.022), flat("paper"), at=(0.22, -0.16, 0.791), rot=(0, 0, -8))
    sticker(p, "clipboard_page", (0.22, -0.16, 0.802), (0, 0, 1), (math.cos(math.radians(-8)), math.sin(math.radians(-8)), 0), (0.21, 0.297), lift=0.0006)
    p.add(box(0.21, 0.297, 0.004), flat("paper"), at=(0.02, -0.215, 0.782), rot=(0, 0, 11))
    p.add(box(0.16, 0.20, 0.035, bevel=0.008), flat("gloss_black"), at=(-0.90, -0.14, 0.7975), rot=(0, 0, 10))
    p.add(box(0.05, 0.19, 0.035), flat("gloss_black"), at=(-0.945, -0.14, 0.83), rot=(0, 0, 10))
    p.add(lathe([(0.034, 0.78), (0.038, 0.87), (0.034, 0.87), (0.030, 0.79)], 8), flat("steel_dark"), at=(0.50, 0.04, 0), sharp=40)
    for k, lean in enumerate((8, -10, 3)):
        p.add(box(0.007, 0.007, 0.15), flat(("paint_blue", "paint_red", "paint_yellow")[k]), at=(0.50 + 0.012 * (k - 1), 0.04 + 0.01 * (k % 2), 0.87), rot=(lean, lean * 0.7, 0))
    return p


def precinct_bench():
    p = Prop("precinct_bench", "lit", note="waiting-room bench 1.8 m long; the sitter faces +Y; slatted seat 0.46 m up, 0.44 m deep; steel frame bolted down, a cuff rail along the back")
    for x in (-0.84, 0.0, 0.84):
        p.add(box(0.05, 0.036, 0.43), "steel_dark", at=(x, 0.215, 0.215))
        strut(p, (x, -0.190, 0.0), (x, -0.262, 0.850), 0.05, 0.036, Paint("steel_dark"))
        p.add(box(0.05, 0.42, 0.030), flat("steel_dark"), at=(x, 0.015, 0.4215))
        p.add(box(0.09, 0.53, 0.008), flat("steel_dark"), at=(x, 0.006, 0.004))
        for y in (0.245, -0.235):
            stud(p, (x + 0.030 * (1 if x <= 0 else -1), y, 0.008), 0.009, 0.008, "chrome")
    for y in (0.230, 0.130, 0.030, -0.070, -0.170):          # the seat: top 0.46 m up
        p.add(box(1.76, 0.086, 0.024, bevel=0.006), flat("wood"), at=(0, y, 0.448))
    lean = math.degrees(math.atan2(0.072, 0.85))
    for z in (0.660, 0.785):
        y = -0.190 - 0.072 * z / 0.85 + 0.030
        p.add(box(1.76, 0.022, 0.110, bevel=0.006), flat("wood"), at=(0, y, z), rot=(lean, 0, 0))
    rod(p, (-0.84, -0.210, 0.545), (0.84, -0.210, 0.545), 0.011, "chrome", 6, caps=False)
    for side in (-1, 1):          # the arms
        x = side * 0.886
        pipe(p, [(x, 0.215, 0.40), (x, 0.215, 0.655), (x, -0.235, 0.655), (x, -0.235, 0.56)], 0.014, "steel_dark", 5, bend=0.05)
        for y, z in ((0.215, 0.42), (-0.235, 0.58)):
            p.add(box(0.05, 0.03, 0.03), flat("steel_dark"), at=(side * 0.862, y, z))
    return p


def holding_cell_bars():
    p = Prop("holding_cell_bars", "lit", note="a barred wall 3.0 m long (X), 2.4 m high: round bars 0.11 m apart between a frame, three flat rails, and a door 0.9 m wide at the +X end, "
             "shut, with its lock box (x 0.54 to 0.69) and three hinges")
    grey = "civic_grey"
    p.add(box(3.0, 0.056, 0.06), flat(grey), at=(0, 0, 2.37))
    p.add(box(3.0, 0.056, 0.05), grey, at=(0, 0, 0.025))
    for x, plate in ((-1.47, -1.44), (0.50, 0.50), (1.47, 1.44)):
        p.add(box(0.06, 0.06, 2.40, bevel=0.006), grey, at=(x, 0, 1.20))
        p.add(box(0.12, 0.10, 0.008), flat("steel_dark"), at=(plate, 0, 0.004))
        for side in (-1, 1):
            stud(p, (plate + (0.042 if x <= 0.5 else -0.042) * (1 if x != 0.50 else side), side * 0.039, 0.008), 0.007, 0.006, "chrome")
    for k in range(1, 17):
        x = -1.44 + 1.91 * k / 17
        p.add(tube([(x, 0, 0.05), (x, 0, 2.34)], 0.011, 6, caps=False), Paint(grey), sharp=70)
    for z in (0.45, 1.20, 1.95):
        p.add(box(1.91, 0.040, 0.012), flat(grey), at=(-0.485, 0, z))
    # The door: a transom over it, the leaf in its own frame, bars in both.
    p.add(box(0.91, 0.056, 0.05), flat(grey), at=(0.985, 0, 2.08))
    for x in (0.5575, 1.4125):
        p.add(box(0.045, 0.045, 2.02, bevel=0.005), grey, at=(x, 0, 1.04))
    for z in (0.0525, 2.0275):
        p.add(box(0.81, 0.040, 0.045), flat(grey), at=(0.985, 0, z))
    for z in (0.45, 1.65):
        p.add(box(0.81, 0.040, 0.012), flat(grey), at=(0.985, 0, z))
    p.add(box(0.81, 0.040, 0.085), flat(grey), at=(0.985, 0, 1.05))
    for k in range(1, 7):
        x = 0.58 + 0.81 * k / 7
        p.add(tube([(x, 0, 0.075), (x, 0, 2.005)], 0.011, 6, caps=False), Paint(grey), sharp=70)
        p.add(tube([(x, 0, 2.105), (x, 0, 2.34)], 0.011, 6, caps=False), flat(grey), sharp=70)
    for z in (0.35, 1.05, 1.75):
        p.add(lathe([(0.014, -0.055), (0.014, 0.055)], 6, cap_top=True, cap_bottom=True), flat("steel_dark"), at=(1.4375, 0.036, z), sharp=40)
        p.add(box(0.055, 0.008, 0.085), flat("steel_dark"), at=(1.415, 0.0265, z))
    p.add(box(0.15, 0.090, 0.24, bevel=0.008), "steel_dark", at=(0.615, 0, 1.05))
    p.add(box(0.014, 0.072, 0.15), flat("steel_dark"), at=(0.533, 0, 1.05))
    for side in (-1, 1):
        p.add(box(0.040, 0.004, 0.085, bevel=0.0015), flat("chrome"), at=(0.605, side * 0.0465, 1.07))
        p.add(box(0.009, 0.002, 0.026), flat("gloss_black"), at=(0.605, side * 0.049, 1.062))
        p.add(lathe([(0.008, 0.0), (0.008, 0.0015)], 6, cap_top=True), flat("gloss_black"), at=(0.605, side * 0.0485, 1.078), rot=(-90 * side, 0, 0), sharp=40)
    return p


def notice_board():
    p = Prop("notice_board", "lit", pivot="bottom centre of its back (it hangs on a wall)", note="wall notice board 1.2 x 1.6 m, 0.06 m deep, read from +Y: cork under POLICE NOTICES, a wanted poster, a map, community notices")
    for z in (0.0275, 1.5725):
        p.add(box(1.20, 0.060, 0.055, bevel=0.007), flat("aluminium"), at=(0, 0.030, z))
    for x in (-0.5725, 0.5725):
        p.add(box(0.055, 0.058, 1.49, bevel=0.007), "aluminium", at=(x, 0.029, 0.80))
    p.add(box(1.10, 0.034, 1.50), flat("wood"), at=(0, 0.017, 0.80))
    sticker(p, "civic_notices", (0, 0.034, 0.80), (0, 1, 0), (-1, 0, 0), (1.092, 1.492), lift=0.001)

    def pinned(name, x, z, width, height, curl=0.016):
        def sheet(u, v):
            return (x - (u - 0.5) * width, 0.0365 + curl * (1 - v) ** 2 + 0.005 * (1 - v) * (2 * u - 1) ** 2, z + (v - 0.5) * height)
        p.add(grid(sheet, 2, 3, both_sides=True), flat("paper").param(name), sharp=180)
        p.add(lathe([(0.006, 0.0), (0.006, 0.005), (0.0, 0.007)], 6), flat("paint_red"), at=(x, 0.036, z + height / 2 - 0.012), rot=(-90, 0, 0), sharp=60)
    pinned("tearoff", 0.326, 0.150, 0.115, 0.16)
    return p


def flag_pole():
    p = Prop("flag_pole", "lit", pivot="ground centre of the pole", note="6.0 m: bolted base, tapered pole, gold ball; the city's flag (1.5 x 0.9 m, blue with a white band and a seal) flies to +X from 4.87 to 5.78 m up")
    p.add(box(0.34, 0.34, 0.022, bevel=0.005), "steel_dark", at=(0, 0, 0.011))
    for sx in (-1, 1):
        for sy in (-1, 1):
            stud(p, (sx * 0.125, sy * 0.125, 0.022), 0.014, 0.014, "chrome")
    p.add(lathe([(0.095, 0.022), (0.095, 0.050), (0.068, 0.120), (0.060, 0.150)], 10), "steel_dark", sharp=40)
    p.add(lathe([(0.056, 0.14), (0.052, 2.0), (0.030, 5.86)], 10), Paint("paint_white"), sharp=40)
    p.add(lathe([(0.030, 5.855), (0.046, 5.865), (0.046, 5.882), (0.020, 5.892)], 10), flat("steel_dark"), sharp=40)
    ball(p, (0, 0, 5.888), 0.056, "gold", 10)
    # The halyard down to its cleat, and the flag clipped to it.
    for y in (-0.014, 0.014):
        p.add(tube([(0.048, y, 5.872), (0.066, y * 0.6, 1.33)], 0.0035, 3, caps=False), flat("linen"), sharp=180)
    p.add(box(0.030, 0.022, 0.050, bevel=0.004), flat("steel_dark"), at=(0.060, 0, 1.30))
    p.add(box(0.016, 0.018, 0.150), flat("steel_dark"), at=(0.076, 0, 1.30))

    def cloth(u, v):
        wave = math.sin(u * 2.0 * math.pi * 1.35 + 0.4 + 0.5 * v)
        swing = 0.085 * wave * u ** 0.75 + 0.03 * u * u
        droop = 0.13 * u ** 1.4 * (1.0 - 0.25 * v)
        return (0.075 + 1.46 * u - 0.03 * u * (1 - v), swing, 4.88 + 0.90 * v - droop)
    p.add(grid(cloth, 10, 3, both_sides=True), flat("paint_blue").param("civic_flag"), sharp=180)
    for z in (5.765, 4.895):
        p.add(box(0.036, 0.008, 0.020), flat("gold"), at=(0.062, 0, z))
    return p


def police_shield_emblem():
    p = Prop("police_shield_emblem", "lit", pivot="centre of its back (it hangs on a wall)",
             note="wall plaque 0.8 m high, faces +Y: a gold police shield, blue enamel, POLICE on its banner; 0.04 m thick with a bevelled rim, the seal and the banner raised")
    paint = lambda: shield_paint(0.80, facing=0.5)
    p.add(shield(0.80, 0.040, 2, 0.014), shield_paint(0.80), rot=STAND, sharp=40)
    seal = moved(lathe([(0.146, 0.0395), (0.140, 0.048), (0.0, 0.048)], 16), at=(0, (0.5 - 0.545) * 0.80, 0))
    p.add(seal, paint(), rot=STAND, sharp=40)
    wide, high = 0.85 * SHIELD_WIDTH * 0.80, 62.0 / 392.0 * 0.80
    banner = moved(box(wide, high, 0.010, bevel=0.003), at=(0, (0.5 - 0.215) * 0.80, 0.043))
    p.add(banner, paint(), rot=STAND, sharp=40)
    return p


def station_awning():
    p = Prop("station_awning", "lit", pivot="the middle of its wall edge, at its underside", note="police-blue door canopy 2.6 m wide reaching 1.4 m out along +Y, 0.5 m high at the wall and 0.3 m at its front; POLICE on the front valance; a steel frame under it")

    def top(u, v):
        x = 1.30 - 2.60 * u
        bay = ((x + 1.275) / 0.85) % 1.0          # the cloth hangs a little between the four rafters
        return (x, 1.40 * v, 0.50 - 0.20 * v - 0.024 * math.sin(math.pi * v) * math.sin(math.pi * bay) ** 0.8)
    p.add(grid(top, 12, 3, both_sides=True), flat(BLUE), sharp=180)
    rod(p, (-1.30, 1.391, 0.302), (1.30, 1.391, 0.302), 0.008, "reflective_white", 4, caps=False)
    p.add(box(2.60, 0.022, 0.045), flat("aluminium"), at=(0, 0.011, 0.4775))
    p.add(box(2.60, 0.008, 0.30), flat(BLUE), at=(0, 1.394, 0.15))
    sticker(p, "civic_police_word", (0, 1.398, 0.160), (0, 1, 0), (-1, 0, 0), (1.08, 0.27), lift=0.0012)
    p.add(box(2.60, 0.012, 0.022), flat("reflective_white"), at=(0, 1.394, 0.011))
    for side in (-1, 1):
        x = side * 1.30
        wing = [(x, 0.0, 0.0), (x, 1.40, 0.0), (x, 1.40, 0.30), (x, 0.0, 0.50)]
        for order in (wing, wing[::-1]):
            part = loft([order], cap_end=True, closed=True)
            p.add(part, flat(BLUE), sharp=180)
        p.add(box(0.012, 1.386, 0.022), flat("reflective_white"), at=(side * 1.294, 0.694, 0.011))
        rod(p, (side * 1.292, 0.022, 0.488), (side * 1.292, 1.391, 0.302), 0.008, "reflective_white", 4, caps=False)
    # The frame: a rail on the wall, one behind the valance, rafters between, a tie along each side and a stay.
    steel = "aluminium"
    rod(p, (-1.28, 0.022, 0.468), (1.28, 0.022, 0.468), 0.014, steel, 5)
    rod(p, (-1.28, 1.372, 0.278), (1.28, 1.372, 0.278), 0.014, steel, 5)
    rod(p, (-1.28, 1.372, 0.030), (1.28, 1.372, 0.030), 0.012, steel, 5)
    rod(p, (-1.28, 0.022, 0.030), (1.28, 0.022, 0.030), 0.012, steel, 5)
    for x in (-1.275, -0.425, 0.425, 1.275):
        rod(p, (x, 0.022, 0.468), (x, 1.372, 0.278), 0.011, steel, 5, caps=False)
        p.add(box(0.07, 0.008, 0.10), flat("steel_dark"), at=(x * 0.985, 0.004, 0.41))
    for x in (-1.272, 1.272):
        rod(p, (x, 0.022, 0.030), (x, 1.372, 0.030), 0.012, steel, 5, caps=False)
        rod(p, (x, 0.022, 0.030), (x, 0.70, 0.352), 0.009, steel, 4, caps=False)
        p.add(box(0.07, 0.008, 0.08), flat("steel_dark"), at=(x * 0.985, 0.004, 0.04))
    return p


def bollard():
    p = Prop("bollard", "lit", note="cast street bollard 0.90 m high, 0.17 m across its foot: flared base, neck rings, ball top, a reflective band")
    profile = [(0.085, 0.0), (0.085, 0.030), (0.070, 0.060), (0.059, 0.120), (0.056, 0.60), (0.066, 0.618), (0.066, 0.640), (0.050, 0.656), (0.050, 0.725),
               (0.062, 0.772), (0.071, 0.826), (0.062, 0.870), (0.036, 0.895), (0.0, 0.900)]
    p.add(lathe(profile, 12), "civic_iron", sharp=35)
    p.add(lathe([(0.0592, 0.44), (0.0588, 0.52)], 12), flat("reflective_white"), sharp=40)
    return p


# ---------------------------------------------------------------------------------------------- bus stops

def bus_stop_sign():
    p = Prop("bus_stop_sign", "glow", pivot="ground centre of the pole", note="pole 0.07 m thick, 2.85 m; the flag (0.50 x 0.62 x 0.03 m, its middle 2.42 m up) hangs to +X and is read from +Y and -Y: "
             "the code letters the route numbers in white on its blue field at (0.30, 2.50), 0.019 m off the middle; a timetable case 1.45 m up")
    p.add(lathe([(0.060, 0.0), (0.060, 0.012), (0.040, 0.030)], 8), flat("steel_dark"), sharp=40)
    p.add(lathe([(0.035, 0.0), (0.035, 2.835), (0.027, 2.850)], 8, cap_top=True), Paint("aluminium"), sharp=40)
    faces = lambda name, size: (Paint("aluminium", grade=False).planar(name, (0, 1, 0), (-1, 0, 0), (0, 0, 0), size).planar(name, (0, -1, 0), (1, 0, 0), (0, 0, 0), size))
    p.add(box(0.50, 0.030, 0.62, bevel=0.003), faces("civic_bus_flag", (0.50, 0.62)), at=(0.30, 0, 2.42))
    for z in (2.63, 2.21):
        p.add(box(0.090, 0.080, 0.034, bevel=0.004), flat("steel_dark"), at=(0.010, 0, z))
    p.add(box(0.26, 0.030, 0.36, bevel=0.004), faces("civic_timetable", (0.244, 0.344)), at=(0.20, 0, 1.45))
    for z in (1.57, 1.33):
        p.add(box(0.075, 0.060, 0.026, bevel=0.003), flat("steel_dark"), at=(0.0325, 0, z))
    return p


def bus_shelter():
    p = Prop("bus_shelter", "lit", pivot="ground centre of its floor (1.4 m deep along X, 3.6 m long along Y)",
             note="open to -X (the road), glazed back at +X, the end panel (route map and poster, both faces) at +Y; bench seat 0.46 m up along the back from y -1.30 to +0.60; "
                  "barrel roof 1.70 x 3.76 m reaching x -0.93, crown 2.50 m; six posts")
    back = (-1.76, -0.59, 0.59, 1.76)
    posts = [(0.66, y) for y in back] + [(-0.66, -1.76), (-0.66, 1.76)]
    for x, y in posts:
        p.add(box(0.07, 0.07, 2.33, bevel=0.005), "steel_dark", at=(x, y, 1.165))
        p.add(box(0.14, 0.14, 0.012), flat("steel_dark"), at=(x, y, 0.006))
        for sx, sy in ((1, 1), (-1, -1)):
            stud(p, (x + sx * 0.05, y + sy * 0.05, 0.012), 0.008, 0.007, "chrome")
    # The ring of beams the roof sits on, the ribs across, the roof and its fascia over the kerb.
    for x in (-0.66, 0.66):
        p.add(box(0.05, 3.59, 0.08), flat("steel_dark"), at=(x, 0, 2.33))
    for y in (-1.76, -0.59, 0.59, 1.76):
        p.add(box(1.64, 0.05, 0.07), flat("steel_dark"), at=(-0.11, y, 2.335))
    arc = [(-0.93 + 1.70 * k / 8, 2.40 + 0.10 * math.sin(math.pi * k / 8)) for k in range(9)]
    ring = lambda y: [(x, y, z) for x, z in arc] + [(x, y, z - 0.035) for x, z in arc[::-1]]
    p.add(loft([ring(-1.88), ring(1.88)], cap_start=True, cap_end=True), flat("civic_grey"), sharp=30)
    p.add(box(0.030, 3.76, 0.15), flat("steel_dark"), at=(-0.913, 0, 2.345))
    sticker(p, "civic_bus_roundel", (-0.928, 0, 2.345), (-1, 0, 0), (0, -1, 0), (0.56, 0.14), lift=0.002)
    p.add(box(0.030, 3.76, 0.07), flat("steel_dark"), at=(0.755, 0, 2.375))
    p.add(box(0.10, 2.20, 0.020), flat("paper"), at=(-0.20, 0, 2.292))          # the light under the roof
    # The back: three bays of glass over a kick panel, a mullion in each.
    pane = lambda w, h: (Paint("glass", grade=False).planar("civic_glass_pane", (1, 0, 0), (0, 1, 0), (0, 0, 0), (w, h)).planar("civic_glass_pane", (-1, 0, 0), (0, -1, 0), (0, 0, 0), (w, h)))
    for y0, y1 in zip(back, back[1:]):
        mid, wide = (y0 + y1) / 2, y1 - y0 - 0.07
        p.add(box(0.014, wide, 1.87), pane(wide, 1.87), at=(0.66, mid, 1.355))
        p.add(box(0.024, wide, 0.30), "civic_grey", at=(0.66, mid, 0.27))
        p.add(box(0.045, wide, 0.040), flat("steel_dark"), at=(0.66, mid, 0.105))
        p.add(box(0.040, wide, 0.035), flat("steel_dark"), at=(0.66, mid, 0.4275))
        p.add(box(0.030, 0.025, 1.85), flat("steel_dark"), at=(0.66, mid, 1.365))
    # The upstream end: a narrow screen by the back corner, so that the wind is kept off the bench.
    p.add(box(0.48, 0.014, 1.87), Paint("glass", grade=False).planar("civic_glass_pane", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.48, 1.87)).planar("civic_glass_pane", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.48, 1.87)),
          at=(0.385, -1.76, 1.355))
    p.add(box(0.035, 0.030, 2.20), "steel_dark", at=(0.135, -1.76, 1.19))
    p.add(box(0.49, 0.035, 0.035), flat("steel_dark"), at=(0.385, -1.76, 0.4275))
    p.add(box(0.48, 0.024, 0.30), "civic_grey", at=(0.385, -1.76, 0.27))
    p.add(box(0.49, 0.040, 0.040), flat("steel_dark"), at=(0.385, -1.76, 0.105))
    # The downstream end: a framed panel, the route map on both faces.
    p.add(box(1.25, 0.050, 1.96), flat("steel_dark"), at=(0, 1.76, 1.30))
    for side in (-1, 1):
        sticker(p, "civic_route_map", (0, 1.76 + side * 0.025, 1.32), (0, side, 0), (-side, 0, 0), (1.04, 1.70), lift=0.002)
        for z in (0.455, 2.185):
            p.add(box(1.10, 0.012, 0.030), flat("aluminium"), at=(0, 1.76 + side * 0.031, z))
        for x in (-0.535, 0.535):
            p.add(box(0.030, 0.012, 1.70), flat("aluminium"), at=(x, 1.76 + side * 0.031, 1.32))
    # The bench along the back: three slats, top 0.46 m up.
    for x in (0.265, 0.400, 0.535):
        p.add(box(0.122, 1.90, 0.030, bevel=0.006), flat("wood"), at=(x, -0.35, 0.445))
    for y in (-1.15, -0.35, 0.45):
        p.add(box(0.045, 0.05, 0.43), "steel_dark", at=(0.25, y, 0.215))
        p.add(box(0.045, 0.05, 0.43), "steel_dark", at=(0.56, y, 0.215))
        p.add(box(0.38, 0.05, 0.030), flat("steel_dark"), at=(0.405, y, 0.4145))
        p.add(box(0.44, 0.07, 0.008), flat("steel_dark"), at=(0.405, y, 0.004))
    rod(p, (0.625, -1.30, 0.80), (0.625, 0.60, 0.80), 0.020, "aluminium", 6)          # a rail to lean back on
    for y in (-1.15, -0.35, 0.45):
        rod(p, (0.56, y, 0.43), (0.625, y, 0.80), 0.012, "steel_dark", 4, caps=False)
    return p


# ---------------------------------------------------------------------------------------------- hotels

def hotel_sign():
    p = Prop("hotel_sign", "lit", note="pavement sign 0.70 m wide, 1.90 m high, 0.16 m deep, its back to a wall and its face to +Y: brass frame and finial on a weighted base; "
             "the code letters the hotel's name in cream on the dark plate 0.60 x 0.34 m centred 1.50 m up, whose face is at y +0.080")
    p.add(box(0.70, 0.16, 0.060, bevel=0.012), "civic_iron", at=(0, 0, 0.030))
    p.add(box(0.58, 0.12, 0.030, bevel=0.008), flat("civic_iron"), at=(0, 0, 0.073))
    x, spring, rise = 0.326, 1.70, 0.130
    arch = [(-x * math.cos(t), 0.0, spring + rise * math.sin(t)) for t in np.linspace(0, math.pi, 9)]
    p.add(tube([(-x, 0, 0.085)] + arch + [(x, 0, 0.085)], 0.017, 6, caps=False), flat("gold"), sharp=70)
    for z in (0.30, 0.60):
        rod(p, (-x, 0, z), (x, 0, z), 0.011, "gold", 5, caps=False)
    for side in (-1, 1):
        p.add(lathe([(0.024, 0.085), (0.024, 0.10), (0.018, 0.115)], 6), flat("gold"), at=(side * x, 0, 0), sharp=40)
    ball(p, (0, 0, spring + rise + 0.012), 0.024, "gold", 6, stem=0.010)
    # The board in the frame, the name plate standing off it on its brass backing, HOTEL and the stars under it.
    p.add(box(0.616, 0.081, 1.08, bevel=0.004), "civic_iron", at=(0, 0.0255, 1.16))
    p.add(box(0.612, 0.006, 0.352), flat("gold"), at=(0, 0.069, 1.50))
    p.add(box(0.600, 0.014, 0.340, bevel=0.002), flat("civic_iron"), at=(0, 0.073, 1.50))
    sticker(p, "civic_hotel_word", (0, 0.066, 1.13), (0, 1, 0), (-1, 0, 0), (0.56, 0.28), lift=0.0012)
    p.add(box(0.50, 0.005, 0.008), flat("gold"), at=(0, 0.0685, 0.90))
    p.add(box(0.50, 0.005, 0.008), flat("gold"), at=(0, 0.0685, 0.70))
    diamond = [(0.0, -0.07), (0.05, 0.0), (0.0, 0.07), (-0.05, 0.0)]
    p.add(prism(diamond, 0.0, 0.006, bottom=False), flat("gold"), at=(0, 0.066, 0.80), rot=STAND)
    return p


def hotel_awning():
    p = Prop("hotel_awning", "lit", pivot="the middle of its wall edge, at its underside", note="entrance canopy 2.40 m wide reaching 1.40 m out along +Y, 0.45 m high at the wall: cream canvas curving down "
             "to a scalloped valance, on a brass frame; hung 2.75 m up")
    drop, low, lobe = 0.150, 0.0, 0.062
    curve = lambda t: (1.40 * math.sin(t * math.pi / 2), drop + (0.45 - drop) * math.cos(t * math.pi / 2))
    cloth = lambda: Paint("linen", grade=False).param("civic_canvas")

    def top(u, v):
        y, z = curve(v)
        return (1.20 - 2.40 * u, y, z)
    p.add(grid(top, 1, 6, both_sides=True), cloth(), sharp=30)

    def hem(s, count):
        return low + lobe * (1.0 - min(1.0, abs(math.sin(math.pi * count * s)) / 0.866))

    def front(u, v):
        bottom = hem(u, 8)
        return (1.20 - 2.40 * u, 1.40, bottom + (drop - bottom) * v)
    p.add(grid(front, 24, 1, both_sides=True), cloth(), sharp=180)
    for side in (-1, 1):
        def cheek(u, v, side=side):
            y, z = curve(u)
            return (side * 1.20, y, drop + (z - drop) * v)

        def skirt(u, v, side=side):
            bottom = hem(u, 4)
            return (side * 1.20, 1.40 * u, bottom + (drop - bottom) * v)
        p.add(grid(cheek, 6, 1, both_sides=True), cloth(), sharp=180)
        p.add(grid(skirt, 12, 1, both_sides=True), cloth(), sharp=180)
    # The brass frame: wall rail, the hoop the valance hangs from, three bows under the cloth.
    brass = "gold"
    rod(p, (-1.185, 0.020, 0.425), (1.185, 0.020, 0.425), 0.013, brass, 5)
    pipe(p, [(-1.185, 0.020, drop + 0.004), (-1.185, 1.385, drop + 0.004), (1.185, 1.385, drop + 0.004), (1.185, 0.020, drop + 0.004)], 0.013, brass, 5, bend=0.03)
    for x in (-1.185, 0.0, 1.185):
        bow = [(x, 0.020 + 1.365 * math.sin(t), drop + 0.004 + (0.421 - drop) * math.cos(t)) for t in np.linspace(0, math.pi / 2, 6)]
        p.add(tube(bow, 0.011, 4, caps=False), flat(brass), sharp=100)
    for x in (-1.185, 1.185):
        p.add(box(0.06, 0.008, 0.09), flat(brass), at=(x * 0.975, 0.004, 0.40))
        p.add(box(0.06, 0.008, 0.07), flat(brass), at=(x * 0.975, 0.004, drop + 0.02))
    return p


def trunk(p, at, size, paint, strap, turn=0.0, handle=1):
    """A suitcase lying on a deck: size (x, y, z), two straps round it, a handle and two catches on its +Y or -Y side."""
    sx, sy, sz = size
    spin = lambda part: moved(part, rot=(0, 0, turn))
    p.add(spin(box(sx, sy, sz, bevel=min(0.022, sz * 0.12))), paint, at=at)
    for k in (-1, 1):
        p.add(spin(moved(box(0.034, sy + 0.006, sz + 0.006), at=(k * sx * 0.27, 0, 0))), flat(strap), at=at)
        p.add(spin(moved(box(0.036, 0.008, 0.040), at=(k * sx * 0.27, handle * (sy / 2 + 0.004), sz * 0.12))), flat("gold"), at=at)
    grip = [(-0.07, handle * sy / 2, 0.0), (-0.06, handle * (sy / 2 + 0.035), 0.0), (0.06, handle * (sy / 2 + 0.035), 0.0), (0.07, handle * sy / 2, 0.0)]
    p.add(spin(tube(fillet(grip, 0.012, 2), 0.009, 5)), flat(strap), at=at, sharp=70)


def luggage_cart():
    p = Prop("luggage_cart", "lit", note="a hotel's brass birdcage trolley, 1.10 m long (X), 0.60 m wide, 1.75 m high: carpeted deck 0.22 m up on four castors, two hoops and a hanging rail; "
             "two cases, a holdall and a hat box on it")
    deck = 0.215
    p.add(box(1.06, 0.56, 0.050, bevel=0.012), flat("gold"), at=(0, 0, deck - 0.025))
    p.add(box(1.00, 0.50, 0.008), flat("plush_red"), at=(0, 0, deck + 0.002))
    for sx in (-1, 1):
        for sy in (-1, 1):
            castor(p, sx * 0.43, sy * 0.20, 0.060, deck - 0.050)
            p.add(lathe([(0.034, -0.014), (0.034, 0.014)], 6, cap_top=True, cap_bottom=True), flat("rubber_black"), at=(sx * 0.516, sy * 0.2706, deck - 0.025), sharp=40)
    # Two hoops that lean together at the top, the rail between them, a ball on each.
    for side in (-1, 1):
        rise = [(0.25, 0.0), (0.243, 0.16), (0.205, 0.31), (0.135, 0.405), (0.06, 0.44), (0.0, 0.45)]
        half = [(side * (0.50 - 0.10 * (h / 0.45) ** 1.5), -y, 1.22 + h) for y, h in rise]
        path = [(side * 0.50, -0.25, deck)] + half + [(x, -y, z) for x, y, z in half[-2::-1]] + [(side * 0.50, 0.25, deck)]
        p.add(tube(path, 0.016, 6, caps=False), flat("gold"), sharp=70)
        for y in (-0.25, 0.25):
            p.add(lathe([(0.027, deck), (0.027, deck + 0.012), (0.019, deck + 0.03)], 6), flat("gold"), at=(side * 0.50, y, 0), sharp=40)
        ball(p, (side * 0.40, 0, 1.682), 0.029, "gold", 6, stem=0.010)
    rod(p, (-0.40, 0, 1.655), (0.40, 0, 1.655), 0.013, "gold", 6, caps=False)
    for x in (0.12,):          # a hanger left on the rail
        pipe(p, [(x, 0, 1.642), (x, 0, 1.60), (x, 0.17, 1.545), (x, -0.17, 1.545), (x, 0, 1.60)], 0.0045, "chrome", 3, bend=0.012, caps=False)
    # The luggage.
    top = deck + 0.006
    trunk(p, (-0.19, -0.01, top + 0.11), (0.62, 0.42, 0.22), "vinyl_navy", "civic_leather", handle=1)
    trunk(p, (0.33, 0.0, top + 0.15), (0.30, 0.44, 0.30), "civic_leather", "leather_black", turn=0.0, handle=1)
    rings = []
    for x, s in ((-0.25, 0.55), (-0.225, 0.90), (-0.12, 1.0), (0.12, 1.0), (0.225, 0.90), (0.25, 0.55)):
        rings.append([(x, math.cos(t) * 0.135 * s, 0.105 + math.sin(t) * 0.105 * s - (0.012 if math.sin(t) < -0.5 else 0.0)) for t in np.linspace(0, 2 * math.pi, 10, endpoint=False)])
    bag = loft(rings, cap_start=True, cap_end=True)
    p.add(moved(bag, rot=(0, 0, 9)), Paint("canvas_olive").planar("duffel_top", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.50, 0.19), facing=0.75), at=(-0.21, -0.02, top + 0.222), sharp=50)
    for k in (-1, 1):
        loop = [(k * 0.065, -0.04, 0.20), (k * 0.065, -0.02, 0.265), (k * 0.065, 0.02, 0.265), (k * 0.065, 0.04, 0.20)]
        p.add(moved(tube(fillet(loop, 0.02, 2), 0.008, 4, caps=False), rot=(0, 0, 9)), flat("nylon_black"), at=(-0.21, -0.02, top + 0.222), sharp=100)
    hat = lathe([(0.125, 0.0), (0.125, 0.105), (0.131, 0.105), (0.131, 0.150), (0.0, 0.150)], 12, cap_bottom=False)
    p.add(hat, Paint("linen", grade=False), at=(0.33, 0.03, top + 0.30), sharp=40)
    p.add(lathe([(0.1265, 0.040), (0.1265, 0.070)], 12), flat("paint_red"), at=(0.33, 0.03, top + 0.30), sharp=40)
    return p


def suitcase():
    p = Prop("suitcase", "lit", note="hard-shell case 0.45 x 0.25 m standing 0.65 m high on four small wheels; carried by the bar over its top: along X, centred (0, 0, 0.630), 0.028 m thick, "
             "0.12 m of it straight, 0.031 m clear above the shell (whose top is 0.585 m up)")
    shell = "wet_paint"
    stations = ((0.045, 0.90), (0.062, 1.0), (0.568, 1.0), (0.585, 0.90))
    rings = [[(x * s, y * s, z) for x, y in rounded_rect(0.45, 0.238, 0.045, 2)] for z, s in stations]
    p.add(loft(rings, cap_start=True, cap_end=True), Paint(shell), sharp=40)
    p.add(loft([[(sx * (0.2265 * s), sy * 0.007, z) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for z, s in ((0.0442, 0.895), (0.062, 0.993), (0.568, 0.993), (0.5858, 0.895))],
               cap_start=True, cap_end=True), flat("plastic_grey"))
    for side in (-1, 1):          # the ribs pressed into the two broad faces
        for x in (-0.135, -0.045, 0.045, 0.135):
            p.add(box(0.034, 0.012, 0.42), flat(shell), at=(x, side * 0.119, 0.315))
            p.add(box(0.026, 0.012, 0.44), flat(shell), at=(x, side * 0.1175, 0.315))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(lathe([(0.024, -0.012), (0.024, 0.012)], 6, cap_top=True, cap_bottom=True), flat("rubber_black"), at=(sx * 0.170, sy * 0.075, 0.024), rot=(0, 90, 0), sharp=40)
            p.add(box(0.040, 0.044, 0.022), flat("plastic_grey"), at=(sx * 0.170, sy * 0.075, 0.044))
    # The carry handle: a straight bar on two posts. And the pull handle's plate behind it, the side grip.
    bar = fillet([(-0.078, 0, 0.580), (-0.078, 0, 0.630), (0.078, 0, 0.630), (0.078, 0, 0.580)], 0.014, 2)
    p.add(tube(bar, 0.014, 6), flat("rubber_black"), sharp=70)
    for side in (-1, 1):
        p.add(lathe([(0.020, -0.006), (0.020, 0.006)], 6, cap_top=True, cap_bottom=True), flat("plastic_grey"), at=(side * 0.078, 0, 0.630), rot=(30, 90, 0), sharp=40)
        p.add(box(0.040, 0.040, 0.010), flat("plastic_grey"), at=(side * 0.078, 0, 0.588))
    p.add(box(0.19, 0.030, 0.008), flat("plastic_grey"), at=(0, -0.080, 0.587))
    return p


# ---------------------------------------------------------------------------------------------- the street

def street_bin():
    p = Prop("street_bin", "lit", note="city litter bin 0.54 m across its rim, 0.90 m high: dark green with a gold band, open at the top (Events sets fire to it: the flames stand 0.85 m up in the middle)")
    body = [(0.200, 0.0), (0.200, 0.030), (0.232, 0.042), (0.250, 0.585), (0.258, 0.592), (0.258, 0.668), (0.250, 0.675), (0.252, 0.840), (0.270, 0.850), (0.270, 0.888), (0.258, 0.900), (0.236, 0.900), (0.228, 0.880), (0.224, 0.640)]
    p.add(lathe(body[:4], 12, cap_bottom=False), Paint("paint_green"), sharp=40)
    p.add(lathe(body[3:7], 12), flat("gold"), sharp=40)
    p.add(lathe(body[6:8], 12), flat("paint_green"), sharp=40)
    p.add(lathe(body[7:], 12), flat("civic_iron"), sharp=40)

    def litter(u, v):
        r, a = 0.224 * v, 2 * math.pi * u
        return (math.cos(a) * r, math.sin(a) * r, 0.640 + 0.05 * (1 - v) + 0.018 * math.sin(a * 3 + 1.0) * v * (1 - v) * 4)
    p.add(grid(litter, 12, 2), flat("nylon_black"), sharp=60)
    p.add(box(0.15, 0.10, 0.004), flat("paper"), at=(0.05, 0.03, 0.688), rot=(14, -9, 30))
    p.add(lathe([(0.030, 0.0), (0.036, 0.09), (0.0, 0.09)], 6), flat("paper"), at=(-0.08, -0.05, 0.672), rot=(60, 20, 0), sharp=40)
    for k in range(8):          # ribs up the side, leaning with it
        a = math.radians(22.5 + 45 * k)
        p.add(moved(box(0.014, 0.026, 0.47), rot=(0, 1.9, 0)), "paint_green", at=(math.cos(a) * 0.2425, math.sin(a) * 0.2425, 0.325), rot=(0, 0, 22.5 + 45 * k))
    p.add(lathe([(0.2535, 0.775), (0.2565, 0.782), (0.2565, 0.800), (0.2535, 0.807)], 12), flat("gold"), sharp=40)
    return p


# ---------------------------------------------------------------------------------------------- the list

# (maker, triangle budget), as in props.py.
PROPS = [
    (precinct_sign, 530), (blue_lamp, 290), (precinct_desk, 750), (precinct_bench, 750), (holding_cell_bars, 1110), (notice_board, 240), (flag_pole, 565),
    (police_shield_emblem, 250), (station_awning, 455), (bollard, 330),
    (bus_stop_sign, 340), (bus_shelter, 1350),
    (hotel_sign, 495), (hotel_awning, 525), (luggage_cart, 1815), (suitcase, 615),
    (street_bin, 550),
]

# Judged beside what they stand at (previews/<id>_sides.png): the wall the sign stands 0.12 m off, people for scale.
SIDES = {
    "precinct_sign": {"references": (((3.4, 0.20, 3.6), (0.0, -0.26, 1.8)),), "people": (((1.55, 0.35), 0.0),)},
    "bus_shelter": {"references": (), "people": (((0.05, 0.6), 90.0), ((-1.55, -1.0), -60.0))},
    "luggage_cart": {"references": (), "people": (((1.05, 0.05), 0.0),)},
}
