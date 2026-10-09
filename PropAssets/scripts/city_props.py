"""THE CITY-LIFE PROPS: what the people of Scripts/CityLife work with. Same rules as props.py (which lists these in
PROPS): one function each, metres, X right, Y forward, Z up, one mesh on the shared atlas.

Hand tools stand on the ground at their working end with their length along Z, because that is how the clips of
CityAssets/scripts/clips.py hold them: a clip's Grip names a point on the tool's Z axis, and that number and the
model here must agree. They are noted beside each tool.
"""
import math

import numpy as np

from prop_kit import Paint, Prop, box, ellipse, fillet, grid, lathe, loft, moved, prism, rounded_rect, tube

BROOM_LEAN = 30.0                              # degrees from upright at which the bristles lie flat on the ground


# ---------------------------------------------------------------------------------------------- hand tools

def broom():
    p = Prop("broom", "lit", pivot="middle of the bristles' edge on the ground; the shaft is Z",
             note="held at 1.04 and 1.33 m up the shaft; bristles flat on the ground when the shaft leans %d degrees back (-Y)" % BROOM_LEAN)
    cut = math.tan(math.radians(BROOM_LEAN))
    rings = []
    for z, half_w, half_d in ((0.0, 0.215, 0.050), (0.07, 0.205, 0.042), (0.150, 0.195, 0.034)):
        ring = []
        for x, y in ((-half_w, -half_d), (half_w, -half_d), (half_w, half_d), (-half_w, half_d)):
            ring.append((x, y, z - (y * cut if z == 0.0 else 0.0)))          # the working edge is cut to the ground
        rings.append(ring)
    p.add(loft(rings, cap_start=True, cap_end=True), "bristle", sharp=30)
    p.add(box(0.40, 0.075, 0.045, bevel=0.008), "paint_red", at=(0, 0, 0.170))
    p.add(lathe([(0.024, 0.19), (0.020, 0.25), (0.0145, 0.26)], 8), Paint("steel_dark", grade=False), sharp=40)
    p.add(tube([(0, 0, 0.19), (0, 0, 1.44)], 0.0135, 8), Paint("wood", grade=False))
    p.add(lathe([(0.0135, 1.43), (0.012, 1.45), (0.006, 1.456)], 8, cap_top=True), Paint("wood", grade=False), sharp=40)
    return p


def shovel():
    p = Prop("shovel", "lit", pivot="point of the blade; the shaft is Z", note="held at 0.84 m and in the D-handle at 1.17 m; the blade's hollow faces +Y")

    def blade(u, v):
        x = (u - 0.5) * 0.23 * (0.25 + 0.75 * min(1.0, v / 0.35))
        return (x, 0.018 - 0.07 * (2 * u - 1) ** 2 * (0.4 + 0.6 * v) - 0.02 * (1 - v), v * 0.29)
    p.add(grid(blade, 6, 5, both_sides=True), Paint("steel_dark", grade=False), sharp=40)
    p.add(tube([(0, -0.012, 0.20), (0, -0.004, 0.30), (0, 0, 0.40)], [0.024, 0.021, 0.0165], 8, caps=False), Paint("steel_dark", grade=False))
    p.add(tube([(0, 0, 0.38), (0, 0, 1.085)], 0.0155, 8), Paint("wood", grade=False))
    grip = [(0, 0, 1.08), (-0.058, 0, 1.135), (-0.058, 0, 1.19), (0.058, 0, 1.19), (0.058, 0, 1.135)]
    p.add(tube(fillet(grip[1:] + grip[1:2], 0.012, 1)[:-1], 0.011, 6, closed=True), Paint("paint_yellow", grade=False))
    p.add(tube([(0, 0, 1.07), (0, 0, 1.135)], 0.017, 6), Paint("paint_yellow", grade=False))
    return p


def jackhammer():
    p = Prop("jackhammer", "lit", pivot="point of the bit; upright", note="held by the two handles at 1.02 m, 0.21 m either side")
    p.add(lathe([(0.004, 0.0), (0.016, 0.07), (0.016, 0.33), (0.026, 0.34), (0.026, 0.37)], 8, cap_bottom=True), Paint("chrome", grade=False), sharp=40)
    p.add(lathe([(0.040, 0.36), (0.050, 0.38), (0.050, 0.46), (0.062, 0.48), (0.066, 0.78), (0.058, 0.80), (0.058, 0.86), (0.070, 0.88), (0.070, 0.96), (0.050, 0.99)], 12, cap_top=True, cap_bottom=True),
          "paint_yellow", sharp=40)
    for z in (0.52, 0.74):
        p.add(lathe([(0.069, z - 0.012), (0.071, z - 0.008), (0.071, z + 0.008), (0.069, z + 0.012)], 12), Paint("steel_dark", grade=False), sharp=40)
    p.add(box(0.10, 0.07, 0.06, bevel=0.012), "steel_dark", at=(0, 0, 1.01))
    p.add(tube([(-0.29, 0, 1.02), (0.29, 0, 1.02)], 0.014, 8), Paint("steel_dark", grade=False))
    for side in (-1, 1):
        p.add(tube([(side * 0.13, 0, 1.02), (side * 0.29, 0, 1.02)], 0.019, 8), Paint("rubber_black", grade=False))
    p.add(tube([(0, -0.03, 0.93), (0, -0.10, 0.96), (0, -0.16, 0.88), (0, -0.19, 0.70)], 0.012, 6), Paint("rubber_black", grade=False))
    return p


def stop_paddle():
    p = Prop("stop_paddle", "glow", pivot="foot of the staff; upright", note="held at 1.10 m; STOP faces +Y, SLOW faces -Y")
    p.add(tube([(0, 0, 0.0), (0, 0, 1.47)], 0.014, 8), Paint("aluminium"))
    p.add(lathe([(0.018, 0.0), (0.018, 0.035)], 8, cap_bottom=True), Paint("rubber_black", grade=False), sharp=40)
    outline = [(math.cos(math.radians(22.5 + 45 * k)) * 0.243, math.sin(math.radians(22.5 + 45 * k)) * 0.243) for k in range(8)]
    faces = Paint("paint_white", grade=False).planar("stop_face", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.45, 0.45)).planar("slow_face", (0, 0, -1), (-1, 0, 0), (0, 0, 0), (0.45, 0.45))
    p.add(prism(outline, -0.004, 0.004), faces, at=(0, 0, 1.68), rot=(90, 0, 180), sharp=40)
    return p


def squeegee():
    p = Prop("squeegee", "lit", pivot="end of the handle; the handle is Z", note="held at 0.07 m; the rubber edge is 0.42 m up, 0.36 m wide along X")
    p.add(tube([(0, 0, 0.0), (0, 0, 0.16)], 0.016, 8), Paint("rubber_black", grade=False))
    p.add(tube([(0, 0, 0.16), (0, 0, 0.345)], [0.013, 0.011], 8), Paint("plastic_grey", grade=False))
    p.add(box(0.11, 0.022, 0.05, bevel=0.006), Paint("plastic_grey", grade=False), at=(0, 0, 0.365))
    p.add(box(0.36, 0.016, 0.022, bevel=0.003), Paint("aluminium", grade=False), at=(0, 0, 0.395))
    p.add(box(0.36, 0.004, 0.018), Paint("rubber_black", grade=False), at=(0, 0, 0.411))
    return p


def bucket():
    p = Prop("bucket", "lit", note="0.30 m across, 0.27 m high, water in it")
    p.add(lathe([(0.105, 0.0), (0.112, 0.006), (0.146, 0.262), (0.152, 0.265), (0.152, 0.272), (0.142, 0.272), (0.106, 0.012)], 14, cap_bottom=True), "paint_blue", sharp=40)
    p.add(lathe([(0.0, 0.215), (0.136, 0.215)], 14), Paint("glass", grade=False), sharp=180)
    handle = [(math.cos(a) * 0.150, math.sin(a) * 0.125, 0.264 - math.sin(a) * 0.034) for a in np.linspace(0, math.pi, 7)]      # down, resting on the rim
    p.add(tube(handle, 0.005, 4), Paint("steel_dark", grade=False))
    return p


def clipboard():
    p = Prop("clipboard", "lit", pivot="ground centre, lying flat", note="0.235 x 0.33 m; the clip is at +Y; held under its near left corner")
    p.add(prism(rounded_rect(0.235, 0.33, 0.012, 1), 0.0, 0.005), Paint("plastic_grey", grade=False))
    page = Paint("paper", grade=False).planar("clipboard_page", (0, 0, 1), (1, 0, 0), (0, -0.008, 0), (0.21, 0.292))
    p.add(box(0.21, 0.292, 0.003), page, at=(0, -0.008, 0.0065))
    p.add(box(0.10, 0.030, 0.012, bevel=0.003), Paint("chrome", grade=False), at=(0, 0.142, 0.012))
    return p


def toolbox():
    p = Prop("toolbox", "lit", note="0.46 x 0.22 x 0.24 m, lies left to right")
    p.add(box(0.46, 0.21, 0.15, bevel=0.012), "paint_red", at=(0, 0, 0.075))
    p.add(box(0.47, 0.22, 0.045, bevel=0.012), "paint_red", at=(0, 0, 0.170))
    for x in (-0.13, 0.13):
        p.add(box(0.035, 0.012, 0.05, bevel=0.003), Paint("chrome", grade=False), at=(x, 0.110, 0.145))
    p.add(tube(fillet([(-0.11, 0, 0.19), (-0.11, 0, 0.235), (0.11, 0, 0.235), (0.11, 0, 0.19)], 0.02, 2), 0.009, 5), Paint("steel_dark", grade=False))
    return p


# ---------------------------------------------------------------------------------------------- things carried and handed over

def parcel():
    p = Prop("parcel", "lit", note="0.38 x 0.28 x 0.24 m cardboard box; carried with a hand under each end")
    faces = Paint("cardboard").planar("parcel_label", (0, 0, 1), (1, 0, 0), (0.07, -0.03, 0.24), (0.15, 0.10))
    p.add(box(0.38, 0.28, 0.24, bevel=0.004), faces, at=(0, 0, 0.12))
    p.add(box(0.385, 0.052, 0.002), Paint("paper", grade=False), at=(0, 0, 0.2405))
    for side in (-1, 1):
        p.add(box(0.002, 0.052, 0.09), Paint("paper", grade=False), at=(side * 0.1905, 0, 0.197))
    return p


def leaflet(name, thickness, note):
    def make():
        p = Prop(name, "lit", pivot="centre, lying flat", note=note)
        faces = Paint("paper", grade=False).planar("leaflet_face", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.105, 0.148)).planar("leaflet_face", (0, 0, -1), (-1, 0, 0), (0, 0, 0), (0.105, 0.148))
        p.add(box(0.105, 0.148, thickness), faces)
        return p
    return make


def coffee_cup():
    p = Prop("coffee_cup", "lit", pivot="ground centre", note="0.085 m across, 0.125 m high; held round the sleeve at 0.06 m")
    p.add(lathe([(0.030, 0.0), (0.031, 0.002), (0.041, 0.108), (0.0425, 0.110)], 10, cap_bottom=True), Paint("paper").wrap("cup_side", 0.0, 0.11), sharp=40)
    p.add(lathe([(0.0440, 0.106), (0.0440, 0.113), (0.036, 0.124), (0.0, 0.124)], 10), Paint("gloss_black", grade=False), sharp=40)
    return p


def phone():
    p = Prop("phone", "lit", pivot="centre, lying flat, screen up", note="0.072 x 0.150 m; top towards +Y")
    p.add(prism(rounded_rect(0.072, 0.150, 0.010, 1), -0.004, 0.004), Paint("gloss_black", grade=False))
    return p


def cigarette():
    p = Prop("cigarette", "glow", pivot="the filter end; lies along +Y", note="84 mm; the lit end is at +Y")
    p.add(tube([(0, 0.0, 0), (0, 0.026, 0)], 0.004, 5), Paint("cardboard", grade=False))
    p.add(tube([(0, 0.026, 0), (0, 0.080, 0)], 0.004, 5, caps=False), Paint("paper", grade=False))
    p.add(tube([(0, 0.080, 0), (0, 0.084, 0)], [0.004, 0.003], 5), Paint("flame", grade=False))
    return p


# ---------------------------------------------------------------------------------------------- street furniture

def sandwich_board():
    p = Prop("sandwich_board", "lit", note="A-frame 0.60 m wide, 0.95 m high; the faces look along +Y and -Y")
    lean = math.degrees(math.atan2(0.21, 0.93))
    for side in (-1, 1):
        face = Paint("wood").planar("board_face", (0, side, 0), (-side, 0, 0), (0, 0, 0), (0.52, 0.74))
        p.add(box(0.60, 0.022, 0.95, bevel=0.004), face, at=(0, side * 0.115, 0.465), rot=(side * lean, 0, 0))
    p.add(box(0.58, 0.03, 0.02), Paint("steel_dark", grade=False), at=(0, 0, 0.925))
    return p


def coffee_cart():
    p = Prop("coffee_cart", "lit", note="1.50 m wide, 0.80 m deep, 2.05 m high; customers stand at +Y, the vendor at -Y; counter 0.95 m up")
    body = Paint("paint_blue").planar("cart_sign", (0, 1, 0), (-1, 0, 0), (0, 0, 0.62), (1.10, 0.32))
    p.add(box(1.40, 0.72, 0.62, bevel=0.012), body, at=(0, 0, 0.62))
    p.add(box(1.50, 0.80, 0.04, bevel=0.008), Paint("aluminium", grade=False), at=(0, 0, 0.95))
    for x in (-0.52, 0.52):
        wheel = lathe([(0.20, -0.025), (0.23, -0.014), (0.23, 0.014), (0.20, 0.025)], 14, cap_top=True, cap_bottom=True)
        faces = Paint("rubber_black", grade=False).planar("wheel_side", (0, 0, 1), (1, 0, 0), (0, 0, 0), (0.46, 0.46)).planar("wheel_side", (0, 0, -1), (1, 0, 0), (0, 0, 0), (0.46, 0.46))
        p.add(wheel, faces, at=(x * 1.38, 0.0, 0.23), rot=(0, 90, 0), sharp=35)
    for x in (-0.60, 0.60):
        p.add(box(0.05, 0.05, 0.31), "steel_dark", at=(x, -0.30, 0.155))
    p.add(tube(fillet([(-0.70, -0.30, 0.80), (-0.92, -0.30, 0.98), (-0.92, 0.30, 0.98), (-0.70, 0.30, 0.80)], 0.05, 2), 0.016, 6), Paint("chrome", grade=False))
    # The machine, the cups and the till on the counter.
    p.add(box(0.42, 0.34, 0.36, bevel=0.02), Paint("chrome", grade=False), at=(-0.36, -0.10, 1.15))
    p.add(box(0.30, 0.05, 0.10, bevel=0.01), Paint("gloss_black", grade=False), at=(-0.36, 0.085, 1.06))
    for k in range(3):
        p.add(lathe([(0.030, 0.0), (0.041, 0.11), (0.041, 0.20)], 8, cap_top=True), Paint("paper", grade=False), at=(0.16 + k * 0.10, 0.20, 0.97), sharp=40)
    p.add(box(0.26, 0.24, 0.10, bevel=0.01), Paint("plastic_grey", grade=False), at=(0.48, -0.14, 1.02))
    # Four posts and the awning.
    for x in (-0.70, 0.70):
        for y in (-0.36, 0.36):
            p.add(tube([(x, y, 0.95), (x, y, 1.88)], 0.014, 6, caps=False), Paint("chrome", grade=False))

    def awning(u, v):
        x, y = (u - 0.5) * 1.72, (v - 0.5) * 1.06
        return (x, y, 2.05 - 0.17 * abs(2 * v - 1) ** 1.6)
    p.add(grid(awning, 8, 6, both_sides=True), Paint("paint_white").param("awning"), sharp=180)
    for y in (-0.53, 0.53):
        p.add(box(1.72, 0.008, 0.11), Paint("paint_white").planar("awning", (0, 1, 0), (1, 0, 0), (0, 0, 0), (1.72, 0.11)).planar("awning", (0, -1, 0), (1, 0, 0), (0, 0, 0), (1.72, 0.11)),
              at=(0, y, 1.825))
    return p


CITY_PROPS = [
    (broom, 260), (shovel, 420), (jackhammer, 650), (stop_paddle, 160), (squeegee, 180), (bucket, 260), (clipboard, 120), (toolbox, 270),
    (parcel, 80), (leaflet("leaflet", 0.0012, "one A6 leaflet; held by its near edge"), 12),
    (leaflet("leaflet_stack", 0.018, "a hand's worth of leaflets"), 12), (coffee_cup, 120), (phone, 60), (cigarette, 60),
    (sandwich_board, 110), (coffee_cart, 1400),
]
