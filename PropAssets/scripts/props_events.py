"""THE EVENT PROPS: what a fire crew wears and holds, and what a street event stands on a closed road (Scripts/Events:
EventProps builds a plain stand-in for each id until the bundle has it). Same rules as props.py: one function each,
metres, X right, Y forward, Z up, one mesh on the shared atlas. Unity gets (x, z, y).

Contracts with the code and with CityAssets/scripts/clips_events.py (checks_events.py measures them):

    hose_nozzle     lies along +Y from its pivot (the back of the barrel, where the coupling ends 0.05 m behind);
                    gripped round the black sleeve 0.06 m along and round the barrel 0.30 m along; the jet leaves 0.42 m along
    placard,        held as stop_paddle is: the fist 1.10 m above the pivot; the staff starts 0.70 m up
    banner_pole
    film_camera     the operator stands 0.75 m BEHIND the tripod's centre on the lens's line (StreetEventScene.FilmSet),
                    so the eyecup is at (0.032, -0.59, 1.50), the pan bar's grip at PAN_GRIP, the focus whip at FOCUS_KNOB
    fire_helmet     the inside of the band is HELMET_INSIDE (half width, half depth)
"""
import math
import os

import numpy as np
from mathutils import Vector

from life_props import flat, pipe, ribbon, rod, smooth, sticker, strut
from prop_kit import Paint, Prop, box, ellipse, grid, lathe, loft, prism, rounded_rect, tube

HELMET_INSIDE = (0.082, 0.103)                       # the cap's HEAD (0.076, 0.097), a size up: it sits 0.028 m lower on the head
HAND = 1.10                                          # the fist on a staff, above the pivot (the stop paddle's)
STAFF_FOOT = 0.70

OPERATOR = -0.75                                     # where the camera operator stands, from the tripod's centre
CAMERA_EYE = (0.032, OPERATOR + 0.16, 1.50)          # camera_operate's right eye
LENS_HEIGHT = 1.47
PAN_FROM = Vector((0.090, -0.005, 1.270))
PAN_AIM = Vector((0.16, -0.45, -0.17)).normalized()  # the pan bar: back, down and out to the operator's right hand
PAN_LENGTH = 0.50
PAN_GRIP = PAN_FROM + PAN_AIM * (PAN_LENGTH - 0.065)
FOCUS_KNOB = Vector((-0.135, -0.295, 1.41))          # the handle of the focus whip, along Y

LAMP_HEIGHT = 2.32


def turned(p, profile, paint, segments=8, at=(0, 0, 0), sharp=40, **caps):
    """A turned part lying along +Y: profile is (radius, y) from back to front."""
    p.add(lathe(profile, segments, **caps), flat(paint) if isinstance(paint, str) else paint, at=at, rot=(-90, 0, 0), sharp=sharp)


def across(p, profile, paint, segments=8, at=(0, 0, 0), sharp=40):
    """A turned part lying along X (a drum, a knob), closed at both ends: profile is (radius, x)."""
    p.add(lathe(profile, segments, cap_top=True, cap_bottom=True), flat(paint) if isinstance(paint, str) else paint, at=at, rot=(0, 90, 0), sharp=sharp)


def frame(width, height, y, z):
    """A rectangle in the XZ plane wound as a lathe's rings are once laid along +Y."""
    return [(width, y, z - height), (-width, y, z - height), (-width, y, z + height), (width, y, z + height)]


# ---------------------------------------------------------------------------------------------- the fire crew

def hose_nozzle():
    p = Prop("hose_nozzle", "lit", pivot="the back of the barrel, where the coupling ends; it points along +Y",
             note="0.47 m: coupling from -0.05 to 0, black sleeve gripped at 0.06 m (0.031 m thick), shut-off with its bale pulled back, "
                  "barrel gripped at 0.30 m (0.030 m thick), bumper tip to 0.42 m")
    turned(p, [(0.0225, -0.050), (0.0285, -0.047), (0.0285, -0.010), (0.0215, -0.006), (0.0215, 0.005)], "gold", 8, cap_bottom=True)
    for side in (-1, 1):                                                            # the coupling's lugs
        p.add(box(0.010, 0.016, 0.012, bevel=0.002), flat("gold"), at=(side * 0.030, -0.030, 0))
    turned(p, [(0.0185, 0.003), (0.0155, 0.010), (0.0155, 0.112), (0.0190, 0.119)], "rubber_black", 8)
    turned(p, [(0.0200, 0.116), (0.0320, 0.126), (0.0340, 0.140), (0.0340, 0.192), (0.0320, 0.206), (0.0200, 0.215)], "paint_red", 10)
    turned(p, [(0.0165, 0.212), (0.0146, 0.346)], "aluminium", 8)
    turned(p, [(0.0162, 0.222), (0.0215, 0.226), (0.0215, 0.246), (0.0158, 0.250)], "gold", 8)
    turned(p, [(0.0170, 0.343), (0.0270, 0.350), (0.0310, 0.358), (0.0310, 0.410), (0.0270, 0.420), (0.0200, 0.4195), (0.0185, 0.398)], "rubber_black", 10, cap_top=True)
    # The bale: a loop hinged either side of the shut-off, pulled back over the sleeve (water on).
    bale = [(-0.0295, 0.172, 0.006), (-0.0295, 0.146, 0.036), (-0.020, 0.130, 0.0495), (0.020, 0.130, 0.0495), (0.0295, 0.146, 0.036), (0.0295, 0.172, 0.006)]
    p.add(tube(bale, 0.0045, 5), flat("chrome"), sharp=70)
    return p


def fire_helmet():
    p = Prop("fire_helmet", "lit", pivot="centre of the rim's lower edge (the tip of the neck brim is the lowest point); peak at +Y",
             note="traditional helmet: ribbed crown with a comb, short front peak, long neck brim, leather front with the company's number; "
                  "inside of the band %.3f m half width, %.3f m half depth" % HELMET_INSIDE)
    count = 24

    def crown(z, a, b, rib=0.055):
        return [(math.cos(2 * math.pi * k / count) * a * (1 + rib * (k % 3 == 0)), math.sin(2 * math.pi * k / count) * b * (1 + rib * (k % 3 == 0)), z) for k in range(count)]
    rings = [crown(0.030, 0.094, 0.115), crown(0.072, 0.093, 0.114), crown(0.112, 0.083, 0.102), crown(0.146, 0.060, 0.076), crown(0.164, 0.030, 0.040)]
    p.add(loft(rings, cap_end=True), Paint("paint_yellow").wrap("events_helmet_side", 0.030, 0.170), sharp=38)

    def edge(k, drop):
        t = 2 * math.pi * k / count
        s, c = math.sin(t), math.cos(t)
        back = max(0.0, -s)
        top = 0.026 - 0.008 * max(0.0, s) - 0.020 * back ** 1.3
        return (c * 0.150 * (1.0 - 0.08 * back * back), s * (0.165 if s >= 0 else 0.235), top - drop)
    inside = HELMET_INSIDE
    brim = [ellipse(*inside, count, 0.010), [edge(k, 0.006) for k in range(count)], [edge(k, 0.0) for k in range(count)], ellipse(0.093, 0.114, count, 0.031)]
    p.add(loft(brim), flat("paint_yellow"), sharp=50)
    p.add(loft([ellipse(*inside, count, 0.058), ellipse(*inside, count, 0.010)]), flat("leather_black"), sharp=60)      # the band, seen from below
    # The comb over the crown from front to back.
    comb = [(0.0, y, z) for y, z in ((0.110, 0.094), (0.094, 0.128), (0.056, 0.160), (0.0, 0.174), (-0.056, 0.160), (-0.094, 0.128), (-0.112, 0.090))]
    p.add(ribbon(comb, lambda q: (0.0, q.y, q.z - 0.02), 0.020, 0.016), flat("paint_yellow"), sharp=40)
    # The leather front, leaning back on the crown, and the brass holder at its top.
    outline = [(-0.034, -0.050), (0.034, -0.050), (0.043, -0.012), (0.040, 0.026), (0.024, 0.048), (0.0, 0.055), (-0.024, 0.048), (-0.040, 0.026), (-0.043, -0.012)]
    front = Paint("leather_black", grade=False).planar("events_helmet_shield", (0, 0, 1), (1, 0, 0), (0, 0.0025, 0), (0.086, 0.105))
    p.add(prism(outline, 0.0, 0.005, bevel=0.0015), front, at=(0, 0.124, 0.089), rot=(72, 0, 180), sharp=40)
    p.add(box(0.026, 0.018, 0.022, bevel=0.004), flat("gold"), at=(0, 0.098, 0.146), rot=(-30, 0, 0))
    return p


def fire_air_pack():
    # Everything here is measured from the upper-chest bone of the game's character (CityAssets/scripts/game_rig.json,
    # and the offsets the game's own chest and back panels sit at): shoulder joints 0.19 m above it and 0.17 m out, the top
    # of the shoulder beside the neck 0.235 m above, the chest's cloth 0.12 to 0.134 m ahead, the back 0.115 m behind, the
    # waist (where duty_belt sits) 0.19 m below, its middle 0.03 m ahead of the bone.
    p = Prop("fire_air_pack", "lit", pivot="the upper-chest bone of the wearer, who faces +Y",
             note="breathing apparatus as worn: the cylinder (valve down) on a back plate, shoulder straps over to the chest, chest strap and waist belt, "
                  "reflective bands on the cylinder, the lumbar pad, the straps and across the chest; parent to UpperChest at zero. "
                  "The straps cross the shoulders 0.24 m above the pivot, the belt is 0.19 m below it, the cylinder's back 0.27 m behind")
    cy = -0.204
    cylinder = lathe([(0.016, -0.450), (0.016, -0.412), (0.034, -0.400), (0.060, -0.378), (0.066, -0.350), (0.066, 0.125), (0.058, 0.162), (0.037, 0.186), (0.0, 0.196)], 10, cap_bottom=True)
    p.add(cylinder, Paint("paint_yellow").wrap("events_cylinder", -0.450, 0.196), at=(0, cy, 0), rot=(0, 0, -90), sharp=40)
    across(p, [(0.026, -0.010), (0.030, -0.004), (0.030, 0.004), (0.026, 0.010)], "gloss_black", 8, at=(0.052, cy, -0.436))      # the hand wheel
    rod(p, (0.0, cy, -0.436), (0.045, cy, -0.436), 0.008, "chrome", 5, caps=False)
    p.add(box(0.046, 0.050, 0.044), flat("chrome"), at=(-0.030, cy + 0.004, -0.444))                                             # the reducer
    pipe(p, [(-0.085, -0.130, -0.330), (-0.085, -0.172, -0.495), (0.085, -0.172, -0.495), (0.085, -0.130, -0.330)], 0.010, "gloss_black", 5, bend=0.03)
    p.add(box(0.25, 0.022, 0.47, bevel=0.010), flat("plastic_grey"), at=(0, -0.127, -0.075))                                     # the back plate
    p.add(box(0.32, 0.036, 0.11, bevel=0.012), "nylon_black", at=(0, -0.112, -0.187))                                           # the lumbar pad
    for side in (-1, 1):
        sticker(p, "events_reflective", (side * 0.118, -0.130, -0.187), (0, -1, 0), (1, 0, 0), (0.076, 0.030), lift=0.001)
    p.add(lathe([(0.0680, -0.020), (0.0695, -0.016), (0.0695, 0.024), (0.0680, 0.028)], 10), flat("nylon_black"), at=(0, cy, 0), sharp=40)
    p.add(box(0.010, 0.040, 0.032), flat("chrome"), at=(0.070, cy - 0.010, 0.004))

    def out(q):                                                                    # away from the trunk
        return q - Vector((max(-0.09, min(0.09, q.x)), 0.0, max(-0.19, min(0.16, q.z))))
    for side in (-1, 1):
        path = [(side * 0.095, -0.123, 0.130), (side * 0.100, -0.086, 0.214), (side * 0.105, 0.005, 0.240), (side * 0.108, 0.086, 0.196), (side * 0.110, 0.123, 0.110),
                (side * 0.112, 0.131, 0.000), (side * 0.120, 0.136, -0.090), (side * 0.150, 0.105, -0.160), (side * 0.165, 0.032, -0.186)]
        p.add(ribbon(smooth(path, 2), out, 0.045, 0.006), flat("nylon_black"), sharp=60)
        sticker(p, "events_reflective", (side * 0.111, 0.1310, 0.050), (0, 1, 0.08), (0, 0, 1), (0.090, 0.030), lift=0.001)
        p.add(box(0.050, 0.010, 0.030), flat("chrome"), at=(side * 0.116, 0.137, -0.055))                                       # the adjuster

    def chest(u, v):                                                               # the chest strap: a reflective band between the two
        x = 0.116 - 0.232 * u
        return (x, 0.140 - 0.35 * x * x, 0.020 + (v - 0.5) * 0.040)
    p.add(grid(chest, 4, 1), flat("nylon_black").param("events_reflective"), sharp=180)
    p.add(box(0.040, 0.008, 0.046), flat("gloss_black"), at=(0, 0.1405, 0.020))
    # The waist belt, and the gauge on its hose over the left shoulder.
    middle = (0.0, 0.029)
    waist = [ellipse(0.156, 0.120, 16, -0.211, middle), ellipse(0.162, 0.126, 16, -0.211, middle), ellipse(0.162, 0.126, 16, -0.163, middle), ellipse(0.156, 0.120, 16, -0.163, middle)]
    p.add(loft(waist), flat("nylon_black"), sharp=60)
    p.add(box(0.055, 0.012, 0.042), flat("gloss_black"), at=(0, 0.159, -0.187))
    hose = [(-0.045, -0.176, -0.400), (-0.100, -0.150, -0.200), (-0.128, -0.130, 0.130), (-0.131, -0.020, 0.246), (-0.134, 0.090, 0.200), (-0.136, 0.128, 0.110), (-0.134, 0.137, 0.010)]
    p.add(tube(smooth(hose, 2), 0.006, 3), flat("rubber_black"), sharp=100)
    turned(p, [(0.022, 0.131), (0.022, 0.143), (0.018, 0.1445)], "gloss_black", 8, at=(-0.134, 0, -0.018))
    turned(p, [(0.018, 0.1446), (0.0, 0.1446)], "paper", 8, at=(-0.134, 0, -0.018), sharp=180)
    return p


# ---------------------------------------------------------------------------------------------- carried in a march

def placard():
    p = Prop("placard", "lit", pivot="as stop_paddle: the fist is %.2f m above it; the staff starts %.2f m up" % (HAND, STAFF_FOOT),
             note="protest placard: two cards 0.56 x 0.42 m stapled either side of a lath, FAIR RENT NOW on +Y, HOMES FOR ALL on -Y; the lath runs from 0.70 to 2.15 m")
    p.add(box(0.028, 0.020, 1.45), "wood", at=(0, 0, STAFF_FOOT + 0.725))
    front = flat("paper").planar("events_placard_front", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.56, 0.42))
    p.add(box(0.56, 0.005, 0.42), front, at=(0, 0.0125, 1.98))
    back = flat("paper").planar("events_placard_back", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.56, 0.42))
    p.add(box(0.56, 0.005, 0.42), back, at=(0, -0.0125, 1.98))
    return p


def banner_pole():
    p = Prop("banner_pole", "lit", pivot="as stop_paddle: the fist is %.2f m above it; the pole starts %.2f m up" % (HAND, STAFF_FOOT),
             note="a march banner: a cloth 0.70 x 0.48 m to +X of a pole 0.03 m thick with a brass finial at 2.20 m; the same emblem on both sides")
    p.add(tube([(0, 0, STAFF_FOOT), (0, 0, 2.165)], 0.015, 8), "wood", sharp=70)
    p.add(lathe([(0.0150, 2.160), (0.0150, 2.172), (0.0085, 2.180), (0.0125, 2.190), (0.0, 2.200)], 8), flat("gold"), sharp=40)
    p.add(lathe([(0.0178, 1.690), (0.0178, 2.150)], 6), flat("linen"), sharp=70)                                              # the sleeve the pole goes through

    def cloth(u, v):
        wave = 0.011 * math.sin(2 * math.pi * (1.25 * u - 0.08)) * u ** 0.6 * (0.55 + 0.45 * (1 - v))
        return (0.016 + 0.689 * u, wave, 1.700 + 0.460 * v - 0.020 * u * u - 0.006 * math.sin(math.pi * u) * (1 - v))
    p.add(grid(cloth, 8, 3, both_sides=True), flat("linen").param("events_banner"), sharp=180)
    return p


def runner_bib():
    p = Prop("runner_bib", "lit", pivot="the upper-chest bone; the number is 0.134 m ahead of it and 0.07 m below",
             note="a race number 0.22 x 0.17 m, bowed to the chest (0.30 m radius: its edges are 0.02 m behind its middle); faces +Y")
    radius = 0.30

    def surface(u, v):
        a = (u - 0.5) * 0.22 / radius
        return (-radius * math.sin(a), 0.134 - radius * (1.0 - math.cos(a)), -0.07 + (v - 0.5) * 0.17)
    p.add(grid(surface, 6, 1), flat("paper").param("events_bib"), sharp=180)
    return p


# ---------------------------------------------------------------------------------------------- the film shoot

def tripod_leg(p, heading, top_at, top_out, foot_out, twin=True):
    a = math.radians(heading)
    aim, side = Vector((math.cos(a), math.sin(a), 0.0)), Vector((-math.sin(a), math.cos(a), 0.0))
    top, foot = aim * top_out + Vector((0, 0, top_at)), aim * foot_out + Vector((0, 0, 0.028))
    return aim, side, top, foot


def film_camera():
    p = Prop("film_camera", "lit", note="cinema camera on a heavy tripod; the lens looks along +Y, %.2f m up; the operator stands %.2f m behind the tripod's centre: "
             "eyecup at (%.3f, %.2f, %.2f), pan bar grip at (%.3f, %.3f, %.3f), focus whip handle at (%.3f, %.3f, %.2f)"
             % (LENS_HEIGHT, -OPERATOR, *CAMERA_EYE, *PAN_GRIP, *FOCUS_KNOB))
    for heading in (90, 210, 330):
        aim, side, top, foot = tripod_leg(p, heading, 1.150, 0.075, 0.45)
        middle = top.lerp(foot, 0.52)
        for s in (-1, 1):
            rod(p, tuple(top + side * s * 0.045), tuple(middle + side * s * 0.020), 0.012, "steel_dark", 5, caps=False)
        rod(p, tuple(top.lerp(foot, 0.42)), tuple(foot), 0.0105, "aluminium", 5, caps=False)
        p.add(box(0.080, 0.038, 0.052), flat("gloss_black"), at=tuple(middle), rot=(0, 0, heading - 90))
        p.add(lathe([(0.034, 0.0), (0.026, 0.028), (0.0, 0.036)], 6), flat("rubber_black"), at=(foot.x, foot.y, 0), sharp=40)
        rod(p, (0, 0, 0.47), tuple(top.lerp(foot, 1 - (0.47 - 0.028) / (1.150 - 0.028))), 0.007, "steel_dark", 4, caps=False)
    p.add(lathe([(0.030, 0.445), (0.030, 0.495)], 6, cap_top=True, cap_bottom=True), flat("gloss_black"), sharp=40)
    p.add(lathe([(0.060, 1.115), (0.086, 1.145), (0.086, 1.185), (0.058, 1.205)], 10, cap_bottom=True), flat("steel_dark"), sharp=40)
    # The fluid head: body, tilt drums, the sliding plate, and the pan bar out to the operator's right hand.
    p.add(box(0.110, 0.140, 0.118, bevel=0.012), flat("gloss_black"), at=(0, 0, 1.262))
    for side in (-1, 1):
        across(p, [(0.050, -0.014), (0.050, 0.014)], "steel_dark", 8, at=(side * 0.068, 0, 1.266))
    p.add(box(0.085, 0.300, 0.028, bevel=0.005), flat("steel_dark"), at=(0, -0.100, 1.334))
    p.add(box(0.100, 0.260, 0.024), flat("gloss_black"), at=(0, -0.130, 1.359))
    end = PAN_FROM + PAN_AIM * PAN_LENGTH
    p.add(tube([tuple(PAN_FROM), tuple(PAN_FROM + PAN_AIM * 0.375)], 0.009, 6), flat("steel_dark"), sharp=70)
    p.add(tube([tuple(PAN_FROM + PAN_AIM * 0.365), tuple(end - PAN_AIM * 0.006), tuple(end)], [0.016, 0.016, 0.012], 8), flat("rubber_black"), sharp=40)
    # The camera: body, battery, the film magazine on top, lens and matte box on their rods.
    p.add(box(0.150, 0.300, 0.190, bevel=0.014), "plastic_grey", at=(0, -0.200, 1.465))
    p.add(box(0.004, 0.170, 0.110), flat("steel_dark"), at=(0.0765, -0.200, 1.465))                                             # the door
    p.add(box(0.004, 0.110, 0.060), flat("aluminium"), at=(-0.0765, -0.250, 1.500))                                             # the control panel
    turned(p, [(0.007, 0.0), (0.007, 0.006), (0.0, 0.0065)], "paint_red", 6, at=(-0.0785, -0.160, 1.500), sharp=60)
    p.add(box(0.100, 0.050, 0.130, bevel=0.008), flat("gloss_black"), at=(0, -0.373, 1.455))
    for y in (-0.275, -0.125):
        across(p, [(0.064, -0.037), (0.070, -0.031), (0.070, 0.031), (0.064, 0.037)], "gloss_black", 10, at=(0, y, 1.630))
    p.add(box(0.066, 0.170, 0.075), flat("gloss_black"), at=(0, -0.200, 1.590))
    turned(p, [(0.050, -0.052), (0.050, -0.034), (0.043, -0.028), (0.043, 0.024), (0.048, 0.028), (0.048, 0.066), (0.043, 0.070), (0.050, 0.120), (0.050, 0.166)],
           "gloss_black", 10, at=(0, 0, LENS_HEIGHT))
    turned(p, [(0.0440, 0.002), (0.0440, 0.012)], "chrome", 10, at=(0, 0, LENS_HEIGHT))
    turned(p, [(0.050, 0.166), (0.044, 0.160), (0.0, 0.168)], "events_lens", 10, at=(0, 0, LENS_HEIGHT), sharp=60)
    hood = [frame(0.070, 0.062, 0.150, LENS_HEIGHT), frame(0.108, 0.078, 0.280, LENS_HEIGHT), frame(0.102, 0.072, 0.280, LENS_HEIGHT), frame(0.064, 0.056, 0.160, LENS_HEIGHT)]
    p.add(loft(hood, cap_start=True, cap_end=True), flat("gloss_black"), sharp=40)
    p.add(box(0.214, 0.100, 0.004), flat("gloss_black"), at=(0, 0.280 + 0.046, LENS_HEIGHT + 0.080 + 0.017), rot=(20, 0, 0))    # the flag
    for side in (-1, 1):
        rod(p, (side * 0.030, -0.060, 1.384), (side * 0.030, 0.265, 1.384), 0.0075, "chrome", 5)
    p.add(box(0.090, 0.022, 0.034), flat("gloss_black"), at=(0, 0.215, 1.392))
    # The viewfinder on its bracket behind the camera, its eyecup where the operator's right eye is.
    ex, ey, ez = CAMERA_EYE
    turned(p, [(0.0, ey + 0.018), (0.0195, ey + 0.018)], "events_lens", 6, at=(ex, 0, ez), sharp=180)
    turned(p, [(0.0190, ey + 0.0175), (0.0250, ey + 0.004), (0.0310, ey + 0.007), (0.0270, ey + 0.030), (0.0210, ey + 0.040), (0.0210, ey + 0.122)], "rubber_black", 6, at=(ex, 0, ez))
    p.add(box(0.054, 0.056, 0.052, bevel=0.007), flat("gloss_black"), at=(ex, ey + 0.146, ez))
    pipe(p, [(ex + 0.027, ey + 0.146, ez), (0.088, ey + 0.165, ez), (0.088, -0.330, ez)], 0.007, "steel_dark", 4, bend=0.012)
    p.add(box(0.022, 0.034, 0.034), flat("gloss_black"), at=(0.084, -0.320, ez))
    # A small monitor turned to the operator, on an arm off the camera's left.
    screen = flat("gloss_black").planar("events_monitor", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.144, 0.096))
    p.add(box(0.150, 0.022, 0.100, bevel=0.005), screen, at=(-0.165, -0.215, 1.610), rot=(-8, 0, 22))
    pipe(p, [(-0.060, -0.200, 1.555), (-0.100, -0.200, 1.580), (-0.150, -0.205, 1.585)], 0.006, "steel_dark", 4)
    # The follow focus on the lens, and its whip back to the operator's left hand.
    across(p, [(0.019, -0.006), (0.019, 0.006)], "aluminium", 8, at=(-0.058, 0.045, LENS_HEIGHT - 0.033))
    p.add(box(0.030, 0.036, 0.062), flat("gloss_black"), at=(-0.052, 0.045, 1.408))
    whip = [(-0.070, 0.045, 1.437), (-0.105, 0.020, 1.432), (-0.132, -0.100, 1.418), (FOCUS_KNOB.x, FOCUS_KNOB.y + 0.045, FOCUS_KNOB.z)]
    p.add(tube(smooth(whip, 2), 0.005, 4), flat("steel_dark"), sharp=100)
    turned(p, [(0.014, -0.047), (0.020, -0.042), (0.020, 0.042), (0.014, 0.047)], "rubber_black", 8, at=tuple(FOCUS_KNOB), cap_top=True, cap_bottom=True)
    return p


def light_stand():
    p = Prop("light_stand", "glow", note="film light on a tripod stand: the lamp, %.2f m up, shines along +Y; barn doors, a cable down the mast, a sandbag on the rear leg. "
             "The whole prop is the glow material: the lens is bright, the rest dark" % LAMP_HEIGHT)
    black, dark = "gloss_black", "steel_dark"
    for heading in (30, 150, 270):
        aim, side, top, foot = tripod_leg(p, heading, 0.52, 0.030, 0.46)
        rod(p, tuple(top), tuple(foot), 0.011, dark, 6, caps=False)
        rod(p, tuple(aim * 0.030 + Vector((0, 0, 0.245))), tuple(top.lerp(foot, 0.52)), 0.007, dark, 4, caps=False)
        p.add(lathe([(0.030, 0.0), (0.022, 0.030), (0.0, 0.036)], 6), flat("rubber_black"), at=(foot.x, foot.y, 0), sharp=40)
    for z0, z1, radius in ((0.20, 1.14, 0.022), (1.10, 1.74, 0.0175), (1.70, 2.135, 0.0135)):
        p.add(tube([(0, 0, z0), (0, 0, z1)], radius, 8, caps=z0 < 0.3), flat(dark), sharp=70)
    for z, radius in ((0.245, 0.034), (0.52, 0.036), (1.125, 0.030), (1.725, 0.026)):
        p.add(lathe([(radius - 0.008, z - 0.030), (radius, z - 0.022), (radius, z + 0.022), (radius - 0.008, z + 0.030)], 6), flat(black), sharp=40)
        if z > 1.0:
            rod(p, (0.0, 0.0, z), (0.075, 0.0, z), 0.006, black, 4, caps=False)
            across(p, [(0.014, -0.008), (0.014, 0.008)], black, 6, at=(0.078, 0, z))
    # The yoke and the head hung in it.
    pipe(p, [(-0.158, -0.030, LAMP_HEIGHT), (-0.158, -0.030, 2.140), (0.158, -0.030, 2.140), (0.158, -0.030, LAMP_HEIGHT)], 0.010, dark, 5, bend=0.04)
    for side in (-1, 1):
        across(p, [(0.026, -0.010), (0.030, -0.004), (0.030, 0.004), (0.026, 0.010)], black, 8, at=(side * 0.176, -0.030, LAMP_HEIGHT))
        rod(p, (side * 0.120, -0.030, LAMP_HEIGHT), (side * 0.170, -0.030, LAMP_HEIGHT), 0.008, dark, 4, caps=False)
    turned(p, [(0.075, -0.170), (0.108, -0.150), (0.125, -0.100), (0.125, 0.060), (0.135, 0.066), (0.135, 0.086), (0.116, 0.086)],
           black, 12, at=(0, 0, LAMP_HEIGHT), cap_bottom=True)
    face = flat("gloss_black").planar("events_lamp_face", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.242, 0.242))
    p.add(lathe([(0.116, 0.0845), (0.0, 0.0855)], 12), face, at=(0, 0, LAMP_HEIGHT), rot=(-90, 0, 0), sharp=180)
    pipe(p, [(-0.050, -0.165, LAMP_HEIGHT + 0.03), (-0.050, -0.215, LAMP_HEIGHT + 0.03), (0.050, -0.215, LAMP_HEIGHT + 0.03), (0.050, -0.165, LAMP_HEIGHT + 0.03)], 0.007, dark, 4, bend=0.015)
    # Barn doors: four leaves hinged on the front ring, opened out.
    hinge, reach = 0.132, 0.100
    for turn_, half, wide, open_ in ((0, 0.100, 0.125, 62), (180, 0.100, 0.125, 62), (90, 0.095, 0.075, 48), (270, 0.095, 0.075, 48)):
        a, o = math.radians(turn_), math.radians(open_)
        up, right = Vector((-math.sin(a), 0, math.cos(a))), Vector((math.cos(a), 0, math.sin(a)))
        root = Vector((0, 0.088, LAMP_HEIGHT)) + up * hinge
        tip = root + up * (reach * math.cos(o)) + Vector((0, reach * math.sin(o), 0))
        p.add(grid(lambda u, v: tuple((root + right * ((u - 0.5) * 2 * half)) * (1 - v) + (tip + right * ((u - 0.5) * 2 * wide)) * v), 1, 1, both_sides=True), flat(black), sharp=180)
    # The cable: off the back of the head, down the mast, away across the ground.
    cable = [(0.0, -0.168, LAMP_HEIGHT - 0.05), (0.0, -0.250, 2.10), (0.022, -0.050, 1.84), (0.030, -0.022, 1.30), (0.028, -0.028, 0.62), (0.090, -0.160, 0.050), (0.230, -0.300, 0.010), (0.130, -0.440, 0.010)]
    p.add(tube(smooth(cable, 2), 0.006, 3), flat("rubber_black"), sharp=100)
    # The sandbag slung over the rear leg.
    rings = []
    for x, hang, size in ((-0.165, 0.118, 0.30), (-0.145, 0.105, 0.85), (-0.075, 0.055, 1.0), (-0.020, 0.004, 0.55), (0.020, 0.004, 0.55), (0.075, 0.055, 1.0), (0.145, 0.105, 0.85), (0.165, 0.118, 0.30)):
        rings.append([(x, -0.245 + math.cos(t) * 0.085 * size, 0.300 - hang + math.sin(t) * 0.040 * size) for t in np.linspace(0, 2 * math.pi, 6, endpoint=False)])
    p.add(loft(rings, cap_start=True, cap_end=True), "canvas_olive", sharp=50)
    return p


def directors_chair():
    p = Prop("directors_chair", "lit", note="tall folding director's chair, facing +Y: crossed legs front and back, canvas seat 0.62 m up (as the stand-in has it), "
             "arms 0.86 m up, the back sling lettered DIRECTOR to 1.10 m; a script lies on the seat")
    for side in (-1, 1):
        p.add(box(0.040, 0.440, 0.035, bevel=0.007), "wood", at=(side * 0.250, 0, 0.0175))
        p.add(box(0.035, 0.400, 0.040, bevel=0.007), flat("wood"), at=(side * 0.235, 0, 0.600))
        p.add(box(0.032, 0.032, 0.250), flat("wood"), at=(side * 0.240, 0.170, 0.725))
        strut(p, (side * 0.240, -0.170, 0.600), (side * 0.240, -0.208, 1.100), 0.032, 0.034, flat("wood"))
        p.add(prism(rounded_rect(0.055, 0.425, 0.020, 1), 0.840, 0.862, bevel=0.006), flat("wood"), at=(side * 0.2475, -0.0075, 0))
        for y in (0.186, -0.186):                                                   # a crossed pair front and back: this side's lath outermost on the right, a lath's thickness in on the left
            y = y if side > 0 else y - math.copysign(0.023, y)
            strut(p, (side * 0.250, y, 0.030), (-side * 0.235, y, 0.585), 0.046, 0.021, Paint("wood"))
    for y in (0.199, -0.199):
        p.add(lathe([(0.011, 0.0), (0.011, 0.004), (0.0, 0.006)], 6), flat("chrome"), at=(0, y, 0.3075), rot=(-90 if y > 0 else 90, 0, 0), sharp=60)

    def seat(u, v):
        return (0.2525 - 0.505 * u, -0.195 + 0.39 * v, 0.622 - 0.042 * math.sin(math.pi * u) ** 0.8)
    p.add(grid(seat, 6, 1, both_sides=True), flat("nylon_black"), sharp=180)

    def sling(u, v):
        z = 0.895 + 0.185 * v
        return (0.257 - 0.514 * u, -0.170 - 0.038 * (z - 0.60) / 0.50 + 0.019 - 0.034 * math.sin(math.pi * u) ** 0.7, z)
    p.add(grid(sling, 6, 1, both_sides=True), flat("nylon_black").param("events_director"), sharp=180)
    script = flat("paper").planar("clipboard_page", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.21, 0.28))
    p.add(box(0.210, 0.280, 0.012), script, at=(0.040, 0.010, 0.590), rot=(0, 0, 14))
    return p


def firework_rack():
    p = Prop("firework_rack", "lit", note="a timber rack of twelve mortar tubes in two rows, lying along X, loaded and fused; the tubes point straight up, their mouths 0.53 m up")
    for side in (-1, 1):
        p.add(box(0.090, 0.500, 0.040, bevel=0.006), "wood", at=(side * 0.555, 0, 0.020))
        p.add(box(0.040, 0.386, 0.470), "wood", at=(side * 0.560, 0, 0.275))
    p.add(box(1.080, 0.386, 0.030), flat("wood"), at=(0, 0, 0.055))
    for z in (0.170, 0.400):
        for y in (-0.173, 0.0, 0.173):
            p.add(box(1.080, 0.040, 0.070), Paint("wood"), at=(0, y, z))
    covers = ("paint_red", "tent_yellow", "paint_red", "paint_blue", "paint_red", "tent_yellow")
    for row, y in enumerate((-0.105, 0.105)):
        for k in range(6):
            x = -0.45 + 0.18 * k
            p.add(lathe([(0.048, 0.070), (0.048, 0.530), (0.0415, 0.526)], 8), Paint("rubber_black"), at=(x, y, 0), sharp=40)
            empty = row == 1 and k == 4
            if empty:
                p.add(lathe([(0.0415, 0.526), (0.0415, 0.400), (0.0, 0.400)], 8), flat("gloss_black"), at=(x, y, 0), sharp=40)
            else:
                p.add(lathe([(0.0415, 0.526), (0.0, 0.5265)], 8), flat(covers[(k + 2 * row) % 6]), at=(x, y, 0), sharp=180)
        # Quick match from tube to tube along the row, and its tail down the end of the rack.
        match = [(-0.583, y * 0.6, 0.30), (-0.583, y, 0.505), (-0.50, y, 0.538)] + [(-0.45 + 0.18 * k + 0.0, y + (0.014 if k % 2 else -0.014), 0.538) for k in range(6)]
        if row == 1:
            match = match[2:-2] + [(0.27, y, 0.538)]
        p.add(tube(match, 0.005, 3), flat("cardboard"), sharp=100)
    for face in (1, -1):
        sticker(p, "events_rack_label", (face * -0.25, face * 0.193, 0.400), (0, face, 0), (-face, 0, 0), (0.160, 0.064), lift=0.0015)
    p.add(box(0.100, 0.022, 0.060, bevel=0.004), flat("plastic_grey"), at=(-0.400, 0.204, 0.400))                              # the firing module, on the front rail
    return p


# ---------------------------------------------------------------------------------------------- the list

PROPS = [
    (hose_nozzle, 580), (placard, 40), (banner_pole, 200), (film_camera, 1500),
    (fire_helmet, 570), (fire_air_pack, 1040), (runner_bib, 12), (light_stand, 1030), (directors_chair, 470), (firework_rack, 840),          # 6 282 of the module's 8 000
]

# What each is judged beside: the operator behind the camera, somebody beside the stand and the chair, the marcher whose fist it is in.
SIDES = {
    "film_camera": {"people": (((0.0, OPERATOR), 0.0),)},
    "light_stand": {"people": (((0.9, 0.0), 0.0),)},
    "directors_chair": {"people": (((0.75, 0.0), 0.0),)},
    "placard": {"people": (((-0.26, -0.24), 0.0),)},
    "banner_pole": {"people": (((-0.26, -0.24), 0.0),)},
}


# ---------------------------------------------------------------------------------------------- how the worn things fit
# EVENTS_FIT=1 adds two props that are NOT for the bundle: the worn things on a dummy with the proportions of the
# game's skeleton (events_fit_crew: helmet and air pack; events_fit_runner: the number), to look at in previews/.

def absorb(p, other, at=(0.0, 0.0, 0.0)):
    """Copies a finished prop into another, moved."""
    layer = other.bm.loops.layers.uv.active
    made = {v: p.bm.verts.new(v.co + Vector(at)) for v in other.bm.verts}
    for face in other.bm.faces:
        copy = p.bm.faces.new([made[v] for v in face.verts])
        copy.smooth = True
        for source, target in zip(face.loops, copy.loops):
            target[p.uv].uv = source[layer].uv
        for source, target in zip(face.edges, copy.edges):
            target.smooth = source.smooth


def dummy(p):
    """The game's street character from the upper-chest bone (the origin), as boxes: bone heights from game_rig.json."""
    skin = "concrete"
    p.add(box(0.32, 0.236, 0.20, bevel=0.03), flat(skin), at=(0, 0.029, -0.262))                 # pelvis (the duty belt's waist: 0.150 x 0.118)
    p.add(box(0.30, 0.236, 0.20, bevel=0.03), flat(skin), at=(0, 0.014, -0.090))                 # belly: its front where the runner's number goes
    p.add(box(0.33, 0.237, 0.22, bevel=0.04), flat(skin), at=(0, 0.002, 0.090))                  # chest: 0.12 ahead of the bone, 0.115 behind
    p.add(box(0.40, 0.17, 0.085, bevel=0.03), flat(skin), at=(0, 0.015, 0.180))                  # shoulders
    p.add(box(0.11, 0.11, 0.14, bevel=0.02), flat(skin), at=(0, 0.025, 0.270))
    p.add(lathe([(0.0, -1.0), (0.55, -0.83), (0.90, -0.45), (1.0, 0.0), (0.90, 0.45), (0.55, 0.83), (0.0, 1.0)], 12), flat(skin), at=(0, 0.032, 0.415), scale=(0.075, 0.092, 0.125), sharp=60)
    for side in (-1, 1):
        p.add(box(0.085, 0.10, 0.60, bevel=0.02), flat(skin), at=(side * 0.235, 0.03, -0.10))
        p.add(box(0.13, 0.15, 0.93, bevel=0.02), flat(skin), at=(side * 0.095, 0.03, -0.722))


def fit_crew():
    p = Prop("events_fit_crew", "lit", pivot="the upper-chest bone", note="not for the bundle: fire_helmet and fire_air_pack on a dummy of the game's skeleton")
    dummy(p)
    absorb(p, fire_helmet(), (0.0, 0.032 + 0.012, 0.315 + 0.085))                                # the head bone, and FireScene's Wear(Helmet, Head, 0.085, 0.012)
    absorb(p, fire_air_pack())
    return p


def fit_runner():
    p = Prop("events_fit_runner", "lit", pivot="the upper-chest bone", note="not for the bundle: runner_bib on a dummy of the game's skeleton")
    dummy(p)
    absorb(p, runner_bib())
    return p


if os.environ.get("EVENTS_FIT"):
    PROPS = PROPS + [(fit_crew, 9000), (fit_runner, 9000)]


# ---------------------------------------------------------------------------------------------- added by the lead

def hose_coil():
    """EventProps.HoseCoil: a rolled length of delivery hose lying flat, 0.52 across and 0.09 high, ground centre."""
    p = Prop("hose_coil", "lit", note="a rolled fire hose lying flat; the coupling at the outer end points along forward")
    turns, r0, r1, high = 7, 0.085, 0.250, 0.086
    profile = [(r0 - 0.004, 0.002), (r0 - 0.004, high - 0.006)]
    for k in range(turns):                                  # each lap of the flat hose: a rounded top edge with a groove between laps
        a, b = r0 + (r1 - r0) * k / turns, r0 + (r1 - r0) * (k + 1) / turns
        profile += [((a + b) / 2, high + 0.004), (b, high - 0.008)]
    profile += [(r1 + 0.008, high - 0.004), (r1 + 0.010, 0.006), (r1 + 0.004, 0.0)]
    p.add(lathe(profile[::-1], 16), Paint("tent_yellow"), sharp=28)
    p.add(lathe([(r0 - 0.004, 0.004), (0.0, 0.004)], 16), Paint("rubber_black", grade=False))      # the ground seen through the eye
    # The tail of the hose leaving the roll, flattened, and its brass coupling with two lugs.
    p.add(tube([(r1 - 0.030, -0.06, 0.045), (r1 - 0.030, 0.03, 0.045), (r1 - 0.036, 0.11, 0.043)], 0.040, 6, flatten=[0.18, 0.2, 0.6]), Paint("tent_yellow", grade=False), sharp=40)
    p.add(tube([(r1 - 0.036, 0.105, 0.043), (r1 - 0.038, 0.165, 0.043)], [0.040, 0.043], 10), Paint("gold", grade=False), sharp=40)
    p.add(tube([(r1 - 0.038, 0.142, 0.043), (r1 - 0.038, 0.158, 0.043)], 0.050, 10), Paint("gold", grade=False), sharp=40)
    return p


PROPS = PROPS + [(hose_coil, 900)]
