"""THE PROPS: one function each, returning a Prop. Dimensions are metres; X right, Y forward, Z up.

To add one: write a function here (copy the nearest), add it to PROPS with a triangle budget, run build.ps1 and
look at previews/<id>.png. A new surface finish or painted face goes in atlas.py.

Sizes that the mod's code relies on are constants at the top, so they are changed in one place.
"""
import math
import os

import bmesh
import numpy as np

from city_props import CITY_PROPS
from life_props import LIFE_PROPS
from prop_kit import Paint, Prop, box, ellipse, fillet, grid, lathe, loft, moved, prism, rounded_rect, tube

# The stretcher's bed, as People/Stretcher.cs has it: 2 m long, the top of the mattress 0.7 m up, head end forward.
BED_TOP = 0.70
BED_HALF_WIDTH = 0.27
BED_HALF_LENGTH = 0.95
HINGE = 0.40                                   # the head section rises from here
RISE = math.tan(math.radians(10.0))

# Fitted to the game's character (PropAssets/_reference, see README "Fit to the character").
WAIST = (0.150, 0.118)                         # half width, half depth of the belt's inside
HEAD = (0.076, 0.097)                          # half width, half depth of the cap band's inside

STAND = (90, 0, 180)                           # turns a prism made lying down so that it stands and faces forward


def bed_top(y):
    return BED_TOP + max(0.0, min(y, BED_HALF_LENGTH) - HINGE) * RISE


# ---------------------------------------------------------------------------------------------- the cordon

def cone():
    p = Prop("cone", "glow")
    p.add(prism(rounded_rect(0.36, 0.36, 0.05), 0.0, 0.028, bevel=0.006), "rubber_black")
    body = lathe([(0.150, 0.026), (0.142, 0.040), (0.126, 0.052), (0.031, 0.735), (0.025, 0.750), (0.016, 0.750), (0.014, 0.700)], 16, cap_top=True)
    p.add(body, Paint("orange_plastic").wrap("cone_side", 0.03, 0.75), sharp=40)
    return p


def tape_post():
    p = Prop("tape_post", "glow", note="tape ties on at 0.95 m")
    p.add(prism(rounded_rect(0.38, 0.38, 0.11, 1), 0.0, 0.045, bevel=0.012), "rubber_black")
    p.add(lathe([(0.075, 0.043), (0.052, 0.075), (0.040, 1.00), (0.030, 1.02)], 10, cap_top=True),
          Paint("orange_plastic").wrap("post_side", 0.045, 1.02), sharp=40)
    grip = fillet([(-0.035, 0, 1.0), (-0.035, 0, 1.09), (0.035, 0, 1.09), (0.035, 0, 1.0)], 0.03, 2)
    p.add(tube(grip, 0.011, 5), Paint("orange_plastic", grade=False))
    return p


def tape(name, length, sag=0.0, segments=1):
    def make():
        p = Prop(name, "glow", pivot="centre of the strip, level with its ends", note="runs along forward; one repeat of the lettering per metre")
        strip = grid(lambda u, v: (0.0, (u - 0.5) * length, (v - 0.5) * 0.075 - sag * (1.0 - (2.0 * u - 1.0) ** 2)), segments, 1, both_sides=True)
        p.add(strip, Paint("paint_yellow").param("tape", repeat=length), sharp=180)
        return p
    return make


def barrier():
    p = Prop("barrier", "lit", note="the board runs left to right; lettering on both faces")
    board = Paint("paint_blue").planar("barrier_board", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (2.4, 0.2)).planar("barrier_board", (0, -1, 0), (1, 0, 0), (0, 0, 0), (2.4, 0.2))
    p.add(box(2.4, 0.036, 0.20, bevel=0.004), board, at=(0, 0, 0.95))
    lean = math.degrees(math.atan2(0.31, 0.86))
    for x in (-0.95, 0.95):
        for side in (-1, 1):
            p.add(box(0.07, 0.034, 0.95, bevel=0.004), "paint_white", at=(x, side * 0.175, 0.425), rot=(side * lean, 0, 0))
            p.add(box(0.09, 0.07, 0.022, bevel=0.004), "rubber_black", at=(x, side * 0.335, 0.011))
        p.add(box(0.05, 0.40, 0.026), "paint_white", at=(x, 0, 0.40))
        p.add(box(0.11, 0.10, 0.075, bevel=0.008), "steel_dark", at=(x, 0, 0.86))
    return p


# ---------------------------------------------------------------------------------------------- the ambulance crew

def castor(p, x, y):
    wheel = lathe([(0.064, -0.019), (0.075, -0.011), (0.075, 0.011), (0.064, 0.019)], 14, cap_top=True, cap_bottom=True)
    faces = Paint("rubber_black", grade=False).planar("wheel_side", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.15, 0.15)).planar("wheel_side", (0, 0, -1), (1, 0, 0), (0, 0, 0), (0.15, 0.15))
    p.add(wheel, faces, at=(x, y - 0.03, 0.075), rot=(0, 90, 0), sharp=35)
    p.add(box(0.058, 0.085, 0.010), "steel_dark", at=(x, y - 0.012, 0.160))
    for side in (-1, 1):
        p.add(box(0.005, 0.045, 0.095), "steel_dark", at=(x + side * 0.0265, y - 0.026, 0.112), rot=(-12, 0, 0))
    p.add(tube([(x, y, 0.16), (x, y, 0.20)], 0.013, 6), "aluminium")


def stretcher():
    p = Prop("stretcher", "lit", note="head end forward; the flat of the mattress is 0.70 m up")
    # Mattress: one rounded section swept from foot to head, rising over the last half metre.
    section = rounded_rect(BED_HALF_WIDTH * 2, 0.075, 0.024, 1)
    stations = ((-BED_HALF_LENGTH, 0.9), (-BED_HALF_LENGTH + 0.03, 1.0), (HINGE, 1.0), (BED_HALF_LENGTH - 0.03, 1.0), (BED_HALF_LENGTH, 0.9))
    rings = [[(x * s, y, bed_top(y) - 0.0375 + z * s) for x, z in section] for y, s in stations]
    p.add(loft(rings, cap_start=True, cap_end=True), Paint("vinyl_navy").planar("mattress_top", (0, 0, 1), (0, 1, 0), (0, 0, BED_TOP), (BED_HALF_LENGTH * 2, BED_HALF_WIDTH * 2)), sharp=60)
    p.add(box(0.36, 0.24, 0.065, bevel=0.022), "linen", at=(0, 0.76, bed_top(0.76) + 0.028), rot=(10, 0, 0))

    # The litter: deck, raised back rest, and the tube that runs right round with a handle loop at each end.
    p.add(box(0.50, 1.36, 0.012), "aluminium", at=(0, -0.27, 0.619))
    p.add(box(0.50, 0.56, 0.012), "aluminium", at=(0, 0.675, bed_top(0.675) - 0.083), rot=(10, 0, 0))
    p.add(tube([(x, y, 0.612) for x, y in rounded_rect(0.57, 2.0, 0.09)], 0.014, 6, closed=True), "aluminium")

    # Undercarriage: an X of legs on each side, pinned where they cross, on a base frame with four castors.
    for x in (-0.23, 0.23):
        p.add(tube([(x, -0.72, 0.19), (x, 0.62, 0.60)], 0.013, 6), "aluminium")
        p.add(tube([(x * 0.86, 0.72, 0.19), (x * 0.86, -0.62, 0.60)], 0.013, 6), "aluminium")
        for y in (-0.62, 0.62):
            p.add(box(0.075, 0.08, 0.04, bevel=0.007), "paint_yellow", at=(x * 0.93, y, 0.597))
    p.add(tube([(-0.25, 0.0, 0.395), (0.25, 0.0, 0.395)], 0.009, 5), "steel_dark")
    p.add(tube([(x, y, 0.19) for x, y in rounded_rect(0.50, 1.56, 0.06, 1)], 0.014, 6, closed=True), "aluminium")
    for x in (-0.25, 0.25):
        for y in (-0.72, 0.72):
            castor(p, x, y)

    # Side rails, and the three patient straps with their buckles.
    for x in (-0.292, 0.292):
        rail = fillet([(x, -0.45, 0.615), (x, -0.45, 0.86), (x, 0.35, 0.86), (x, 0.35, 0.615)], 0.08, 2)
        p.add(tube(rail, 0.011, 6), "aluminium")
        p.add(tube([(x, -0.45, 0.74), (x, 0.35, 0.74)], 0.008, 5, caps=False), "aluminium")
        p.add(tube([(x, -0.05, 0.615), (x, -0.05, 0.86)], 0.008, 5, caps=False), "aluminium")
    across = ((-0.276, 0.625), (-0.276, 0.690), (-0.255, 0.706), (0.255, 0.706), (0.276, 0.690), (0.276, 0.625))
    for y in (-0.62, -0.18, 0.26):
        strap = grid(lambda u, v, y=y: (across[round(u * 5)][0], y + (v - 0.5) * 0.05, across[round(u * 5)][1]), 5, 1)
        p.add(strap, Paint("orange_plastic").param("strap"), sharp=180)
        p.add(box(0.055, 0.04, 0.012, bevel=0.003), "steel_dark", at=(0.09, y, 0.712))
    return p


def drape(name, region, form, y0, y1, nu, nv, note):
    """A cloth lying over a body form on the stretcher's bed, tucked down over the mattress's edge inside the side
    rails, and over its ends. form: (y, half width, height above the mattress) along the body."""
    def make():
        edge = BED_HALF_WIDTH + 0.012
        xs = np.concatenate([[-edge], np.linspace(-BED_HALF_WIDTH + 0.01, BED_HALF_WIDTH - 0.01, nu - 1), [edge]])
        ys = np.linspace(y0, y1, nv + 1)
        along, half, high = [np.array(c) for c in zip(*form)]
        width = np.interp(ys, along, half)[:, None]
        body = np.interp(ys, along, high, left=0.0, right=0.0)[:, None] * np.clip(1.0 - (xs[None, :] / width) ** 2, 0.0, 1.0) ** 0.6
        cloth = body.copy()
        for _ in range(2):                      # the cloth spans the hollows instead of following them
            padded = np.pad(cloth, 1, mode="edge")
            cloth = np.maximum(body, sum(padded[1 + a:1 + a + nv + 1, 1 + b:1 + b + nu + 1] for a in (-1, 0, 1) for b in (-1, 0, 1)) / 9.0)
        z = np.array([bed_top(y) for y in ys])[:, None] + 0.006 + cloth
        yy, xx = np.meshgrid(ys, xs, indexing="ij")
        over = (np.abs(xx) > BED_HALF_WIDTH) * 1.0 + np.clip((np.abs(yy) - BED_HALF_LENGTH + 0.02) / 0.05, 0.0, 1.0)
        z = z - (0.05 + cloth) * over * (1.0 + 0.3 * np.sin(yy * 23.0 + xx * 9.0))
        p = Prop(name, "lit", pivot="the stretcher's own: put it on the stretcher with no offset", note=note)
        p.add(grid(lambda u, v: (xs[round(u * nu)], ys[round(v * nv)], z[round(v * nv), round(u * nu)]), nu, nv), Paint("linen").param(region), sharp=180)
        return p
    return make


SHEET_FORM = ((-0.94, 0.10, 0.0), (-0.88, 0.12, 0.25), (-0.78, 0.12, 0.13), (-0.42, 0.15, 0.14), (-0.05, 0.20, 0.19), (0.30, 0.22, 0.23),
              (0.46, 0.24, 0.20), (0.56, 0.10, 0.11), (0.68, 0.10, 0.22), (0.78, 0.09, 0.17), (0.86, 0.06, 0.0))
BLANKET_FORM = ((-0.94, 0.12, 0.0), (-0.88, 0.15, 0.27), (-0.76, 0.15, 0.17), (-0.42, 0.19, 0.19), (-0.05, 0.23, 0.25), (0.22, 0.25, 0.27), (0.34, 0.25, 0.27))


def medic_bag():
    p = Prop("medic_bag", "lit", note="front (emblem) forward")
    outline = rounded_rect(0.56, 0.26, 0.045)
    rings = [[(x * s, y * s, z) for x, y in outline] for z, s in ((0.0, 0.95), (0.025, 1.0), (0.295, 1.0), (0.325, 0.93))]
    faces = Paint("nylon_red").planar("bag_front", (0, 1, 0), (-1, 0, 0), (0, 0, 0.1625), (0.56, 0.325)).planar("bag_front", (0, -1, 0), (1, 0, 0), (0, 0, 0.1625), (0.56, 0.325))
    p.add(loft(rings, cap_start=True, cap_end=True), faces)
    for x in (-0.292, 0.292):
        p.add(box(0.05, 0.19, 0.20, bevel=0.016), "nylon_red", at=(x, 0, 0.125))
        p.add(box(0.054, 0.194, 0.03), "reflective_white", at=(x, 0, 0.18))
    p.add(box(0.44, 0.04, 0.006), "reflective_white", at=(0, 0, 0.326))
    for y in (-0.055, 0.055):
        handle = fillet([(-0.10, y, 0.315), (-0.10, y * 0.3, 0.405), (0.10, y * 0.3, 0.405), (0.10, y, 0.315)], 0.04, 2)
        p.add(tube(handle, 0.011, 5), Paint("nylon_black", grade=False))
    return p


def blanket_and_sheet():
    return [drape("stretcher_sheet", "sheet_cloth", SHEET_FORM, -0.98, 0.98, 12, 32, "a covered body, head to toe")(),
            drape("stretcher_blanket", "blanket_cloth", BLANKET_FORM, -0.98, 0.34, 12, 20, "feet to chest; the real body lies under it")()]


def first_aid_box():
    p = Prop("first_aid_box", "lit")
    faces = Paint("plastic_green").planar("first_aid_face", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.30, 0.22)).planar("first_aid_face", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.30, 0.22))
    p.add(box(0.30, 0.11, 0.22, bevel=0.012), faces, at=(0, 0, 0.11))
    p.add(tube(fillet([(-0.055, 0, 0.215), (-0.055, 0, 0.252), (0.055, 0, 0.252), (0.055, 0, 0.215)], 0.015, 2), 0.007, 5), Paint("reflective_white", grade=False))
    return p


# ---------------------------------------------------------------------------------------------- police equipment

def shield(scale=1.0, thickness=0.005):
    """A police shield lying on its back, top towards +Y: stand it up with STAND."""
    right = ((0.0, -0.036), (0.012, -0.029), (0.022, -0.018), (0.027, -0.002), (0.025, 0.012), (0.028, 0.026), (0.022, 0.034), (0.012, 0.031), (0.0, 0.036))
    outline = [(x * scale, y * scale) for x, y in right] + [(-x * scale, y * scale) for x, y in right[-2:0:-1]]
    return prism(outline, 0.0, thickness, bevel=0.0015 * scale)


def shield_paint(scale=1.0):
    return Paint("gold", grade=False).planar("badge_face", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.056 * scale, 0.072 * scale))


def badge():
    p = Prop("badge", "lit", pivot="centre of its back", note="faces forward")
    p.add(shield(), shield_paint(), rot=STAND, sharp=40)
    return p


def on_ring(half, turn, out=0.0):
    """A place on an ellipse (half width, half depth) at `turn` degrees (0 right, 90 front, 180 left, 270 back),
    pushed outwards by `out`, and the heading that faces a part's +Y outwards there."""
    t = math.radians(turn)
    nx, ny = math.cos(t) / half[0], math.sin(t) / half[1]
    length = math.hypot(nx, ny)
    nx, ny = nx / length, ny / length
    return (math.cos(t) * half[0] + nx * out, math.sin(t) * half[1] + ny * out), math.degrees(math.atan2(-nx, ny))


def duty_belt():
    p = Prop("duty_belt", "lit", pivot="centre of the ring, at half its height",
             note="inside of the ring: %.3f m half width, %.3f m half depth; holster on the right" % WAIST)
    thick, half_height, count = 0.006, 0.0275, 24
    outer = (WAIST[0] + thick, WAIST[1] + thick)
    rings = [ellipse(*WAIST, count, -half_height), ellipse(*outer, count, -half_height), ellipse(*outer, count, half_height), ellipse(*WAIST, count, half_height)]
    ring = loft(rings + [rings[0]])
    p.add(ring, Paint("leather_black", grade=False), sharp=60)

    def hang(part, paint, turn, out, z=0.0, lean=0.0):
        (x, y), heading = on_ring(outer, turn, out)
        p.add(moved(part, rot=(lean, 0, 0)), paint, at=(x, y, z), rot=(0, 0, heading))

    hang(box(0.055, 0.008, 0.045, bevel=0.003), Paint("chrome", grade=False), 90, 0.004)
    hang(box(0.062, 0.040, 0.150, bevel=0.012), "leather_black", 4, 0.020, -0.055, lean=-6)                    # holster
    hang(moved(box(0.034, 0.030, 0.075, bevel=0.008), rot=(0, -14, 0)), "plastic_grey", 4, 0.022, 0.050, lean=-6)   # the pistol's grip
    for turn in (117, 137):
        hang(box(0.036, 0.030, 0.088, bevel=0.007), "leather_black", turn, 0.015, -0.004)                      # magazines
    hang(box(0.042, 0.032, 0.072, bevel=0.009), "nylon_black", 60, 0.016, 0.0)                                 # spray
    hang(box(0.062, 0.036, 0.100, bevel=0.007), "nylon_black", 176, 0.018, -0.010)                             # radio pouch
    hang(box(0.052, 0.028, 0.060, bevel=0.006), "plastic_grey", 176, 0.018, 0.068)                             # radio
    hang(tube([(0.016, 0, 0.0), (0.016, 0, 0.075)], 0.005, 4), Paint("rubber_black", grade=False), 176, 0.018, 0.095)
    hang(box(0.088, 0.032, 0.085, bevel=0.012), "leather_black", 252, 0.016, 0.0)                              # cuff case
    hang(box(0.030, 0.012, 0.055), "leather_black", 207, 0.006, 0.0)                                           # baton ring and its loop
    hang(tube([(math.cos(a) * 0.021, 0.021 + math.sin(a) * 0.021, 0) for a in np.linspace(0, 2 * math.pi, 8, endpoint=False)], 0.004, 4, closed=True),
         Paint("chrome", grade=False), 207, 0.010, -0.024)
    for turn in (38, 152, 228, 312):
        hang(box(0.014, 0.004, 0.060), Paint("gloss_black", grade=False), turn, 0.002)
    return p


def shoulder_radio():
    p = Prop("shoulder_radio", "lit", pivot="centre of its back (the clip)", note="speaker faces forward, cord hangs down")
    p.add(prism(rounded_rect(0.056, 0.086, 0.012, 1), 0.0, 0.028, bevel=0.005),
          Paint("plastic_grey", grade=False).planar("radio_face", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.056, 0.086)), rot=STAND)
    p.add(box(0.006, 0.012, 0.034), Paint("rubber_black", grade=False), at=(0.030, 0.014, 0.008))
    p.add(tube([(0, 0.014, -0.040), (0, 0.016, -0.075), (0.004, 0.008, -0.115), (0.010, -0.004, -0.150)], 0.0045, 5), Paint("rubber_black", grade=False))
    return p


def police_cap():
    p = Prop("police_cap", "lit", pivot="centre of the band's lower edge", note="inside of the band: %.3f m half width, %.3f m half depth; peak forward" % HEAD)
    count = 24
    start = math.radians(90.0 - 180.0 / count)           # no vertex dead ahead: a flat panel for the badge

    def top(scale, z):
        ring = []
        for k in range(count):
            t = start + 2 * math.pi * k / count
            points = 1.0 + 0.05 * math.cos(8 * (t - math.radians(112.5)))       # the eight corners
            y = math.sin(t) * 0.128 * scale * points - 0.002
            ring.append((math.cos(t) * 0.120 * scale * points, y, z + 0.026 * (y + 0.13) / 0.26))      # the top slopes up to the front
        return ring
    band = [ellipse(HEAD[0] + 0.004, HEAD[1] + 0.004, count, 0.0, start=start), ellipse(HEAD[0] + 0.006, HEAD[1] + 0.006, count, 0.043, start=start)]
    p.add(loft(band), Paint("gloss_black", grade=False), sharp=60)
    crown = [band[1], top(1.0, 0.078), top(0.94, 0.090), top(0.5, 0.097), top(0.12, 0.098)]
    p.add(loft(crown, cap_start=True, cap_end=True), "cloth_navy", sharp=38)

    def visor(u, v):
        t = math.radians(158.0 - 136.0 * u)
        (x, y), _ = on_ring((HEAD[0] + 0.005, HEAD[1] + 0.005), math.degrees(t))
        reach = 0.078 * math.sin(math.pi * u) ** 0.55 * v
        return (x + math.cos(t) * reach * 0.35, y + math.sin(t) * reach, 0.006 - 0.36 * reach)
    p.add(grid(visor, 8, 2, both_sides=True), Paint("gloss_black", grade=False), sharp=180)
    cord = [(*on_ring((HEAD[0] + 0.008, HEAD[1] + 0.008), turn)[0], 0.013) for turn in np.linspace(28, 152, 8)]
    p.add(tube(cord, 0.0038, 4), Paint("gold", grade=False))
    p.add(moved(moved(shield(0.62, 0.004), rot=STAND), rot=(-22, 0, 0)), shield_paint(0.62), at=(0, HEAD[1] + 0.017, 0.070), sharp=40)
    return p


def handcuffs():
    p = Prop("handcuffs", "lit", note="lying flat, closed")
    steel = Paint("chrome", grade=False)
    for side in (-1, 1):
        ring = [(side * 0.058 + math.cos(a) * 0.036, math.sin(a) * 0.036, 0.006) for a in np.linspace(0, 2 * math.pi, 12, endpoint=False)]
        p.add(tube(ring, 0.0045, 5, closed=True), steel)
        p.add(box(0.026, 0.034, 0.012, bevel=0.0025), steel, at=(side * 0.027, 0, 0.006))
        link = [(side * 0.007 + math.cos(a) * 0.010, math.sin(a) * 0.006, 0.006 + side * 0.002) for a in np.linspace(0, 2 * math.pi, 6, endpoint=False)]
        p.add(tube(link, 0.0022, 4, closed=True), steel)
    return p


def panel(name, region_name, width, height, radius, back):
    def make():
        facing = "backwards (worn on the back)" if back else "forwards (worn on the chest)"
        p = Prop(name, "glow", pivot="centre of the patch, on the cloth", note="faces " + facing)
        side = 1.0 if back else -1.0

        def surface(u, v):
            a = (u - 0.5) * width / radius
            return (side * radius * math.sin(a), side * radius * (1.0 - math.cos(a)), (v - 0.5) * height)
        p.add(grid(surface, 6, 1), Paint("cloth_navy").param(region_name), sharp=180)
        return p
    return make


# ---------------------------------------------------------------------------------------------- accident and crime scene

def debris_glass():
    p = Prop("debris_glass", "lit", note="a scatter 1.2 m across; turn it at random")
    rng = np.random.default_rng(11)
    bm = bmesh.new()
    for _ in range(64):
        distance, bearing = abs(rng.normal(0.0, 0.26)), rng.uniform(0, 2 * math.pi)
        cx, cy = min(distance, 0.6) * math.cos(bearing), min(distance, 0.6) * math.sin(bearing)
        size, corners, spin = rng.uniform(0.012, 0.04), int(rng.integers(3, 5)), rng.uniform(0, 2 * math.pi)
        tilt = rng.uniform(-0.25, 0.25, 2)
        verts = []
        for k in range(corners):
            a = spin + 2 * math.pi * (k + rng.uniform(-0.25, 0.25)) / corners
            dx, dy = math.cos(a) * size * rng.uniform(0.6, 1.0), math.sin(a) * size
            verts.append(bm.verts.new((cx + dx, cy + dy, 0.006 + abs(dx * tilt[0] + dy * tilt[1]))))
        bm.faces.new(verts)
    p.add(bm, Paint("glass", grade=False), sharp=0)
    return p


def debris_plastic():
    p = Prop("debris_plastic", "lit", note="bumper and lamp fragments over 1.5 m; turn it at random")
    rng = np.random.default_rng(5)
    finishes = ["plastic_grey", "car_silver", "plastic_grey", "lens_red", "rubber_black", "lens_amber", "plastic_grey", "lens_red", "car_silver"]
    sizes = [0.30, 0.24, 0.16, 0.07, 0.14, 0.05, 0.11, 0.05, 0.09]
    for k, (finish, size) in enumerate(zip(finishes, sizes)):
        corners = int(rng.integers(5, 7))
        outline = [(math.cos(2 * math.pi * (c + rng.uniform(-0.3, 0.3)) / corners) * size * 0.5 * rng.uniform(0.7, 1.0),
                    math.sin(2 * math.pi * (c + rng.uniform(-0.3, 0.3)) / corners) * size * 0.28 * rng.uniform(0.6, 1.0)) for c in range(corners)]
        piece = prism(outline, 0.0, 0.004)
        for v in piece.verts:               # the curve of the panel it came from
            v.co.z += 0.9 * v.co.x ** 2 / max(size, 0.1)
        bearing, distance = 2 * math.pi * k / len(sizes) + rng.uniform(-0.3, 0.3), rng.uniform(0.15, 0.7)
        p.add(piece, Paint(finish, grade=False), at=(math.cos(bearing) * distance, math.sin(bearing) * distance, 0.002), rot=(rng.uniform(-6, 6), 0, rng.uniform(0, 360)), sharp=40)
    return p


def flare():
    p = Prop("flare", "glow", note="lying along forward, burning at the front end")
    p.add(tube([(0, -0.125, 0.016), (0, 0.105, 0.016)], 0.015, 10), Paint("paint_red", grade=False))
    p.add(tube([(0, -0.150, 0.016), (0, -0.122, 0.016)], 0.017, 10), Paint("reflective_white", grade=False))
    p.add(tube([(0, 0.105, 0.016), (0, 0.128, 0.016), (0, 0.136, 0.016)], [0.0135, 0.0125, 0.007], 8), Paint("flame", grade=False))
    return p


def evidence_marker(number):
    def make():
        p = Prop("evidence_marker_%d" % number, "lit", note="number on both faces")
        half, foot, apex = 0.045, 0.042, 0.125
        for side in (1.0, -1.0):
            face = grid(lambda u, v: (side * half * (1.0 - 2.0 * u), side * foot * (1.0 - v), apex * v), 1, 1, both_sides=True)
            p.add(face, Paint("tent_yellow").param("evidence_%d" % number), sharp=180)
        return p
    return make


def duffel_bag():
    p = Prop("duffel_bag", "lit", note="lies left to right")
    section = [(0.15 * math.copysign(abs(math.cos(a)) ** 0.6, math.cos(a)), 0.14 * math.copysign(abs(math.sin(a)) ** (0.45 if math.sin(a) < 0 else 0.7), math.sin(a)))
               for a in np.linspace(0, 2 * math.pi, 12, endpoint=False)]
    rings = [[(x, y * s, 0.14 + z * s) for y, z in section] for x, s in ((-0.35, 0.5), (-0.335, 0.82), (-0.29, 1.0), (0.29, 1.0), (0.335, 0.82), (0.35, 0.5))]
    p.add(loft(rings, cap_start=True, cap_end=True), Paint("canvas_olive").planar("duffel_top", (0, 0, 1), (1, 0, 0), (0, 0, 0.28), (0.70, 0.30), facing=0.45), sharp=60)
    for side in (-1, 1):
        handle = fillet([(-0.115, side * 0.085, 0.24), (-0.09, side * 0.05, 0.345), (0.09, side * 0.05, 0.345), (0.115, side * 0.085, 0.24)], 0.04, 2)
        p.add(tube(handle, 0.009, 4), Paint("nylon_black", grade=False))
    return p


def spray_can():
    p = Prop("spray_can", "lit", note="standing")
    p.add(lathe([(0.030, 0.0), (0.033, 0.004), (0.033, 0.150), (0.031, 0.156), (0.018, 0.170), (0.0175, 0.172)], 10, cap_bottom=True), Paint("aluminium").wrap("can_label", 0.0, 0.172), sharp=35)
    p.add(lathe([(0.0325, 0.152), (0.0325, 0.197), (0.0290, 0.203)], 10, cap_top=True), Paint("paint_blue", grade=False), sharp=35)
    return p


def crowbar():
    p = Prop("crowbar", "lit", note="lying along forward, hook at the front")
    radius = 0.009
    path = [(0.040, -0.385), (0.016, -0.345), (0.0, -0.29), (0.0, 0.29)]
    path += [(0.038 - 0.038 * math.cos(a), 0.29 + 0.038 * math.sin(a)) for a in np.radians((30, 70, 110, 150))]
    path += [(0.084, 0.275), (0.094, 0.245)]
    flat = [0.25, 0.6, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.6, 0.25]
    p.add(tube([(x, y, radius) for x, y in path], [radius * 1.5, radius * 1.2] + [radius] * 6 + [radius * 1.2, radius * 1.5], 6, flatten=flat), Paint("paint_red", grade=False), sharp=40)
    return p


# ---------------------------------------------------------------------------------------------- the list

# (maker, triangle budget). A maker returns one Prop or several.
PROPS = [
    (cone, 300), (tape_post, 250), (barrier, 600),
    (tape("tape", 1.0), 4), (tape("tape_4m", 4.0), 4), (tape("tape_4m_sag", 4.0, sag=0.07, segments=8), 32),
    (stretcher, 2500), (blanket_and_sheet, 800), (medic_bag, 400), (first_aid_box, 150),
    (badge, 100), (duty_belt, 800), (shoulder_radio, 120), (police_cap, 600), (handcuffs, 450),
    (panel("back_panel_police", "panel_police", 0.28, 0.10, 0.24, True), 12), (panel("back_panel_ems", "panel_ems", 0.28, 0.10, 0.24, True), 12),
    (panel("chest_panel_police", "panel_police", 0.11, 0.04, 0.24, False), 12), (panel("chest_panel_ems", "chest_ems", 0.09, 0.042, 0.24, False), 12),
    (debris_glass, 120), (debris_plastic, 220), (flare, 120),
    (evidence_marker(1), 8), (evidence_marker(2), 8), (evidence_marker(3), 8), (evidence_marker(4), 8),
    (duffel_bag, 300), (spray_can, 200), (crowbar, 150),
] + CITY_PROPS + LIFE_PROPS          # what the people of Scripts/CityLife work with: city_props.py, life_props.py


# The later rounds: props_<name>.py, each with a list PROPS of (maker, budget) and a painting module paint_<name>.py
# (see atlas.py). PROPS_DEV=name[,name] limits them to those, as it does the atlas; a module not there is skipped.
# A module may also say SIDES = {prop id: {"references": ((size, centre), ...), "people": (((x, y), turn), ...)}}: such a
# prop gets a second sheet, previews/<id>_sides.png, from six sides with what it goes over or beside (kit.turnaround).
# "parts": ((prop id, (x, y, z), turn), ...) puts other props on it where the code does (a vehicle's wheels).
SIDES = {}
EXTRA = ["civic", "service", "events", "everyday", "garage", "family", "site", "tow"]
_only = os.environ.get("PROPS_DEV")
EXTRA = EXTRA + ["tech"]   # the tech sector's buildings (props_tech.py)
# PROPS_SET=second: the second bundle (README, "The second bundle"): its own modules and nothing of the first's.
SECOND = ["build", "landmarks"]   # the building sites, then Uptown's stadium (props_landmarks.py)
if os.environ.get("PROPS_SET") == "second":
    PROPS = []
for _name in (SECOND if os.environ.get("PROPS_SET") == "second" else [n.strip() for n in _only.split(",") if n.strip()] if _only else EXTRA):
    if os.path.exists(os.path.join(os.path.dirname(os.path.abspath(__file__)), "props_" + _name + ".py")):
        _module = __import__("props_" + _name)
        PROPS = PROPS + _module.PROPS
        SIDES.update(getattr(_module, "SIDES", {}))


def build_all(only=None):
    """Every prop, as (Prop, budget)."""
    made = []
    for maker, budget in PROPS:
        result = maker()
        for prop in result if isinstance(result, list) else [result]:
            if only is None or prop.name in only:
                made.append((prop, budget))
    return made
