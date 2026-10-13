"""THE TECH MODULE: the tech sector's buildings (Scripts/Tech/Sites). Same rules as props.py: one function each, metres,
X right, Y forward, Z up, one mesh on the shared atlas. Unity gets (x, z, y).

THE CODE IS THE SPECIFICATION (Scripts/Tech/Sites/SiteProps.cs, which draws a box of each size until the bundle has
the model, and Scripts/Tech/Sites/SitePlan.cs, which lays them out):

    a server room     dc_rack, dc_rack_ai, dc_rack_network stand in rows, fronts on a cold aisle; dc_cooling at the ends of
                      the hot aisles; dc_ups against the side walls; dc_tray 2.45 m up over each row, scaled along its
                      length; dc_floor and dc_floor_vent laid flat under the hall, scaled to the panel; dc_ops_desk by the
                      door. dc_leds_a and dc_leds_b are the racks' status lights, laid on a rack's pivot (0.065 m forward
                      on dc_rack_ai, whose front is further out): the mod draws them in its own two materials and makes
                      them blink, the one set when the other is dark
    a lobby           hq_reception across the way in, visitors at +Y; hq_logo_wall behind it (the mod puts the company's
                      colour and name on its plate); hq_screen on a wall
    a tech office     tech_rack_small, tech_bench and tech_whiteboard against a wall
    a frontage        dc_sign beside the door (the mod letters the cloud's name on its glass), dc_booth, dc_barrier

Kit numbers that the code relies on (checks_tech.py measures them):

    dc_rack, dc_rack_network   footprint 0.60 x 1.07, the front (+Y) at y 0.535, 2.00 m tall (the network rack's cable
                               riser to 2.45, where the tray is)
    dc_rack_ai                 footprint 0.60 x 1.20, its front at y 0.600
    dc_leds_a, dc_leds_b       on the door's painted lights, 0.002 m proud of the front at y 0.535
    dc_tray                    2.00 long (along Y), 0.40 wide; the pivot is the middle of the tray's underside
    dc_floor, dc_floor_vent    2.40 x 2.40 and 0.60 x 2.40; tops at z 0.020 and 0.024 (the vents lie on the panels)
    hq_logo_wall               the plate for the name: 2.50 x 1.10, its face at y 0.080, centred 1.60 up
    hq_reception               the band across the front from 0.25 to 0.35 up, face at y 0.452: the company's colour goes on it
    hq_screen, tech_whiteboard the pivot is the middle of the back (y 0): they hang with their back on the wall
"""
import math

from life_props import flat, rod
from paint_tech import DOOR, led_spots
from prop_kit import Paint, Prop, box, grid, moved
from props_site import Face

FRONT = 0.535          # a rack's front, from its pivot
AI_FRONT = 0.600
DOOR_WIDTH, DOOR_HIGH, DOOR_MIDDLE = 0.52, 1.72, 1.00      # the painted door on the front


def decal_box(p, size, at, swatch, faces, bevel=0.0):
    """A box whose faces looking along each normal in `faces` show a region: faces = ((name, normal, right, (w, h)), ...)."""
    paint = Paint(swatch)
    for name, normal, right, extent in faces:
        centre = (normal[0] * size[0] / 2, normal[1] * size[1] / 2, normal[2] * size[2] / 2)      # (painted where it is made, then moved)
        paint.planar(name, normal, right, centre, extent)
    p.add(box(*size, bevel=bevel), paint, at=at)


# ---------------------------------------------------------------------------------------------- the server room

def cabinet(p, depth, front_decal, back=True, height=2.00):
    """A 42-unit rack's body: plinth, cabinet, top; its door painted on the front, its rear on the back."""
    half = depth / 2
    p.add(box(0.58, depth - 0.04, 0.04), flat("rubber_black"), at=(0, 0, 0.02))
    p.add(box(0.60, depth, height - 0.06, bevel=0.006), "tech_black", at=(0, 0, 0.04 + (height - 0.06) / 2))
    p.add(box(0.60, depth, 0.02), flat("steel_dark"), at=(0, 0, height - 0.01))
    Face(p).decal(front_decal, 0, DOOR_MIDDLE, DOOR_WIDTH, DOOR_HIGH, half + 0.0006)
    if back:
        Face(p, rot=(0, 0, 180)).decal("tech_rack_back", 0, 1.0, 0.52, 1.80, half + 0.0006)
    p.add(box(0.016, 0.022, 0.20, bevel=0.004), flat("aluminium"), at=(-0.262, half + 0.011, 1.02))      # the handle, at the reader's right


def dc_rack():
    p = Prop("dc_rack", "lit", note="a 42-unit rack of servers 0.60 x 1.07, 2.00 tall: front (+Y) a perforated door over eleven servers, their status "
                                   "lights painted dark (dc_leds_a and _b light them); the rear's fans and power strip on the back")
    cabinet(p, 2 * FRONT, "tech_servers")
    return p


def dc_rack_ai():
    p = Prop("dc_rack_ai", "lit", note="a rack of AI accelerators 0.60 x 1.20, 2.00 tall: the cabinet 1.12 deep with its front at y 0.60, the liquid "
                                      "cooling's two pipes (blue in, red out) and manifold behind it")
    half = 0.56
    p.add(box(0.58, 1.08, 0.04), flat("rubber_black"), at=(0, 0.04, 0.02))
    p.add(box(0.60, 1.12, 1.94, bevel=0.006), "tech_black", at=(0, 0.04, 1.01))
    p.add(box(0.60, 1.12, 0.02), flat("steel_dark"), at=(0, 0.04, 1.99))
    Face(p).decal("tech_gpus", 0, DOOR_MIDDLE, DOOR_WIDTH, DOOR_HIGH, AI_FRONT + 0.0006)
    Face(p, rot=(0, 0, 180)).decal("tech_rack_back", 0, 1.0, 0.52, 1.80, half - 0.04 + 0.0006)
    p.add(box(0.016, 0.022, 0.20, bevel=0.004), flat("aluminium"), at=(-0.262, AI_FRONT + 0.011, 1.02))
    for x, paint in ((-0.16, "paint_blue"), (0.16, "paint_red")):
        rod(p, (x, -0.555, 0.12), (x, -0.555, 1.92), 0.024, paint, sides=8)
    p.add(box(0.50, 0.05, 0.10, bevel=0.01), flat("stainless"), at=(0, -0.555, 1.93))
    return p


def dc_rack_network():
    p = Prop("dc_rack_network", "lit", note="the network core: a rack 0.60 x 1.07 of patch panels and switches with their cables, and a cable riser "
                                           "from its top to 2.45 m, where the tray over the row is")
    cabinet(p, 2 * FRONT, "tech_switches")
    p.add(box(0.16, 0.12, 0.45, bevel=0.01), flat("rubber_black"), at=(0, -0.20, 2.225))
    p.add(box(0.06, 0.08, 0.45), flat("paint_yellow"), at=(0.12, -0.20, 2.225))
    return p


def dc_leds(which):
    def maker():
        p = Prop("dc_leds_" + "ab"[which], "glow", note="the status lights of set %s of a rack's door: small squares 0.002 m proud of its front, on the painted "
                                                         "lights. Drawn by the mod in a material of its own that it makes blink" % "AB"[which])
        width, high = DOOR
        size = 0.0115
        for x, y, group in led_spots():
            if group != which:
                continue
            r = (x / width - 0.5) * DOOR_WIDTH
            z = DOOR_MIDDLE + (0.5 - y / high) * DOOR_HIGH
            p.add(grid(lambda u, v, r=r, z=z: (-r + (0.5 - u) * size, FRONT + 0.002, z + (v - 0.5) * size * 0.75), 1, 1), flat("paper"), sharp=180)
        return p
    return maker


def dc_cooling():
    p = Prop("dc_cooling", "lit", note="a computer room air conditioner 1.80 x 0.90, 1.96 tall, front (+Y): louvres, its control panel, the filter; "
                                      "the return grille on top")
    p.add(box(1.76, 0.86, 0.06), flat("rubber_black"), at=(0, 0, 0.03))
    p.add(box(1.80, 0.90, 1.89, bevel=0.01), "tech_white", at=(0, 0, 0.06 + 0.945))
    Face(p).decal("tech_crac", 0, 1.0, 1.70, 1.84, 0.4506)
    p.add(box(1.60, 0.70, 0.012), flat("steel_dark"), at=(0, 0, 1.956))
    return p


def dc_ups():
    p = Prop("dc_ups", "lit", note="a battery cabinet 0.80 x 0.85, 1.90 tall: display, status bar and grille on the front (+Y)")
    p.add(box(0.78, 0.83, 0.04), flat("rubber_black"), at=(0, 0, 0.02))
    p.add(box(0.80, 0.85, 1.86, bevel=0.008), "tech_grey", at=(0, 0, 0.04 + 0.93))
    Face(p).decal("tech_ups", 0, 1.0, 0.70, 1.70, 0.4256)
    return p


def dc_tray():
    p = Prop("dc_tray", "lit", pivot="the middle of the tray's underside", note="2.00 m of ladder cable tray 0.40 wide along Y, hung 2.45 m up over a row: "
                                                                               "its rails and rungs, a loom of cables and a yellow fibre duct on it, the hangers' rods 0.55 up")
    for x in (-0.19, 0.19):
        p.add(box(0.02, 2.0, 0.08), flat("aluminium"), at=(x, 0, 0.04))
    for k in range(7):
        p.add(box(0.36, 0.03, 0.012), flat("aluminium"), at=(0, -0.9 + k * 0.3, 0.006))
    p.add(box(0.26, 2.0, 0.05, bevel=0.012), flat("rubber_black"), at=(-0.04, 0, 0.037))
    p.add(box(0.10, 2.0, 0.06, bevel=0.01), flat("paint_yellow"), at=(0.12, 0, 0.042))
    for y in (-0.85, 0.85):
        p.add(box(0.46, 0.04, 0.03), flat("steel_dark"), at=(0, y, -0.015))
        for x in (-0.215, 0.215):
            rod(p, (x, y, -0.03), (x, y, 0.55), 0.006, "steel_dark", sides=4, caps=False)
    return p


def floor_panel(name, region, width, top):
    def maker():
        p = Prop(name, "lit", note="raised floor, %.2f x 2.40, its top %.3f up: laid flat under the hall and scaled to the panel" % (width, top))
        decal_box(p, (width, 2.40, top), (0, 0, top / 2), "tech_grey", ((region, (0, 0, 1), (1, 0, 0), (width, 2.40)),))
        return p
    return maker


def monitor(p, at, turn, region, size=(0.52, 0.31)):
    """A screen on its stand on a desk top at z `at[2]`, facing +Y turned by `turn` degrees."""
    x, y, z = at
    base = math.radians(turn)
    face = Face(p, at=(x, y, z), rot=(0, 0, turn))
    face.block((size[0], 0.025, size[1]), (0, 0, 0.30), flat("plastic_grey"), bevel=0.006)
    face.decal(region, 0, 0.30, size[0] - 0.03, size[1] - 0.03, 0.0131)
    face.block((0.05, 0.03, 0.17), (0, -0.03, 0.09), flat("plastic_grey"))
    face.block((0.20, 0.14, 0.012), (0, -0.02, 0.006), flat("plastic_grey"))
    return base


def desk(p, width, depth, top, swatch="tech_white", legs="tech_grey"):
    p.add(box(width, depth, 0.035, bevel=0.006), flat(swatch), at=(0, 0, top - 0.0175))
    for x in (-width / 2 + 0.015, width / 2 - 0.015):
        p.add(box(0.03, depth - 0.06, top - 0.035), flat(legs), at=(x, 0, (top - 0.035) / 2))
    p.add(box(width - 0.06, 0.02, 0.42), flat(legs), at=(0, -depth / 2 + 0.04, top - 0.035 - 0.25))


def dc_ops_desk():
    p = Prop("dc_ops_desk", "lit", note="an operations desk 1.60 x 0.80, top 0.75 up (1.21 with the screens): WHOEVER WORKS AT IT STANDS AT +Y. Three "
                                       "screens of dashboards facing +Y, the two outer ones turned in; a keyboard and a phone")
    desk(p, 1.60, 0.80, 0.75)
    monitor(p, (0.0, -0.20, 0.75), 0, "tech_dashboard")
    monitor(p, (-0.55, -0.15, 0.75), -18, "tech_dashboard")
    monitor(p, (0.55, -0.15, 0.75), 18, "tech_dashboard")
    p.add(box(0.44, 0.14, 0.015, bevel=0.004), flat("plastic_grey"), at=(0, 0.16, 0.7575))
    p.add(box(0.16, 0.18, 0.05, bevel=0.01), flat("gloss_black"), at=(0.55, 0.14, 0.775))
    return p


# ---------------------------------------------------------------------------------------------- the lobby

def hq_reception():
    p = Prop("hq_reception", "lit", note="a reception desk 2.60 x 0.90: VISITORS STAND AT +Y at a counter 1.10 up; the receptionist works behind at a "
                                        "surface 0.75 up with a screen. A dark band across the front from 0.25 to 0.35 up (face at y 0.452) for the company's colour")
    p.add(box(2.60, 0.06, 1.06, bevel=0.01), "tech_white", at=(0, 0.42, 0.53))
    p.add(box(2.64, 0.32, 0.04, bevel=0.008), flat("wood"), at=(0, 0.33, 1.08))
    p.add(box(2.50, 0.60, 0.035, bevel=0.006), flat("tech_white"), at=(0, -0.12, 0.7325))
    for x in (-1.275, 1.275):
        p.add(box(0.05, 0.86, 0.715), flat("tech_white"), at=(x, -0.02, 0.3575))
    p.add(box(2.50, 0.004, 0.10), flat("tech_black"), at=(0, 0.452, 0.30))
    monitor(p, (0.40, -0.25, 0.75), 180, "tech_dashboard", (0.48, 0.29))
    p.add(box(0.40, 0.13, 0.015, bevel=0.004), flat("plastic_grey"), at=(0.40, -0.02, 0.7575))
    return p


def hq_logo_wall():
    p = Prop("hq_logo_wall", "lit", note="a free-standing feature wall 3.40 x 0.30, 2.60 tall, wooden slats both sides on two steel feet; on the front (+Y) "
                                        "a dark plate 2.50 x 1.10 centred 1.60 up, its face at y 0.080, where the mod puts the company's colour and name")
    for x in (-1.40, 1.40):
        p.add(box(0.30, 0.30, 0.06, bevel=0.008), flat("steel_dark"), at=(x, 0, 0.03))
    decal_box(p, (3.40, 0.12, 2.52), (0, 0, 0.06 + 1.26), "wood",
              (("tech_slats", (0, 1, 0), (-1, 0, 0), (3.40, 2.52)), ("tech_slats", (0, -1, 0), (1, 0, 0), (3.40, 2.52))))
    p.add(box(3.42, 0.14, 0.03), flat("aluminium"), at=(0, 0, 2.595))
    p.add(box(2.50, 0.02, 1.10, bevel=0.004), flat("tech_screen"), at=(0, 0.07, 1.60))
    return p


def hq_screen():
    p = Prop("hq_screen", "lit", pivot="the middle of its back", note="a wall screen 1.60 x 0.95 of dashboards, 0.06 off the wall on its bracket, facing +Y")
    p.add(box(0.40, 0.012, 0.30), flat("steel_dark"), at=(0, 0.006, 0))
    p.add(box(1.60, 0.048, 0.95, bevel=0.008), flat("gloss_black"), at=(0, 0.036, 0))
    Face(p).decal("tech_dashboard", 0, 0, 1.56, 0.91, 0.0606)
    return p


# ---------------------------------------------------------------------------------------------- the office

def tech_rack_small():
    p = Prop("tech_rack_small", "lit", note="a server closet rack 0.60 x 0.80, 1.20 tall: a patch panel and six servers behind its door (+Y)")
    p.add(box(0.58, 0.76, 0.04), flat("rubber_black"), at=(0, 0, 0.02))
    p.add(box(0.60, 0.80, 1.14, bevel=0.006), "tech_black", at=(0, 0, 0.04 + 0.57))
    p.add(box(0.60, 0.80, 0.02), flat("steel_dark"), at=(0, 0, 1.19))
    Face(p).decal("tech_closet", 0, 0.62, 0.52, 1.00, 0.4006)
    p.add(box(0.016, 0.022, 0.18, bevel=0.004), flat("aluminium"), at=(-0.262, 0.411, 0.62))
    return p


def tech_bench():
    p = Prop("tech_bench", "lit", note="a developer's desk 1.40 x 0.70, top 0.75 up: WHOEVER WORKS AT IT STANDS AT +Y. Two screens of code side by side "
                                      "facing +Y, a keyboard, a tower under the right-hand end")
    desk(p, 1.40, 0.70, 0.75, "wood", "steel_dark")
    monitor(p, (-0.28, -0.16, 0.75), -6, "tech_code")
    monitor(p, (0.28, -0.16, 0.75), 6, "tech_code")
    p.add(box(0.44, 0.14, 0.015, bevel=0.004), flat("plastic_grey"), at=(0, 0.14, 0.7575))
    p.add(box(0.20, 0.44, 0.44, bevel=0.01), flat("tech_black"), at=(0.50, -0.02, 0.22))
    p.add(box(0.012, 0.004, 0.012), flat("lens_amber"), at=(0.45, 0.201, 0.40))
    return p


def tech_whiteboard():
    p = Prop("tech_whiteboard", "lit", pivot="the middle of its back", note="a whiteboard 1.80 x 1.20 on a wall, facing +Y: a sketch of a system and notes, "
                                                                            "its pen tray along the bottom")
    p.add(box(1.76, 0.015, 1.16), flat("tech_white"), at=(0, 0.0075, 0))
    Face(p).decal("tech_board", 0, 0, 1.70, 1.10, 0.0156)
    for x in (-0.89, 0.89):
        p.add(box(0.025, 0.03, 1.20), flat("aluminium"), at=(x, 0.015, 0))
    for z in (-0.59, 0.59):
        p.add(box(1.80, 0.03, 0.025), flat("aluminium"), at=(0, 0.015, z))
    p.add(box(0.60, 0.06, 0.02), flat("aluminium"), at=(0, 0.045, -0.585))
    return p


# ---------------------------------------------------------------------------------------------- the frontage

def dc_sign():
    p = Prop("dc_sign", "lit", note="a monument sign 2.40 x 0.40, 1.40 tall: a concrete base, a dark metal slab, a glass plate 2.00 x 0.60 centred "
                                   "0.95 up, its face at y 0.126 (+Y), where the mod letters the cloud's name")
    p.add(box(2.40, 0.40, 0.25, bevel=0.01), "concrete", at=(0, 0, 0.125))
    p.add(box(2.20, 0.24, 1.15, bevel=0.01), flat("tech_black"), at=(0, 0, 0.25 + 0.575))
    p.add(box(2.00, 0.012, 0.60), flat("tech_screen"), at=(0, 0.12, 0.95))
    p.add(box(2.24, 0.26, 0.02), flat("aluminium"), at=(0, 0, 1.41))
    return p


def dc_booth():
    p = Prop("dc_booth", "lit", note="a security booth 1.60 x 1.60, 2.42 tall: glazed on three sides above 1.00 m, its back (-Y) solid with the door in it, "
                                    "SECURITY on the band over the front (+Y)")
    p.add(box(1.60, 1.60, 1.00, bevel=0.01), "tech_white", at=(0, 0, 0.50))
    for x in (-0.76, 0.76):
        for y in (-0.76, 0.76):
            p.add(box(0.08, 0.08, 1.00), flat("steel_dark"), at=(x, y, 1.50))
    for normal in ((0, 1), (1, 0), (-1, 0)):
        nx, ny = normal
        size = (0.02, 1.44, 0.96) if nx else (1.44, 0.02, 0.96)
        p.add(box(*size), flat("glass"), at=(nx * 0.77, ny * 0.77, 1.50))
    p.add(box(1.60, 0.04, 1.00), flat("tech_white"), at=(0, -0.78, 1.50))
    p.add(box(0.80, 0.03, 1.96), flat("tech_grey"), at=(0.30, -0.80, 0.98))
    decal_box(p, (1.66, 1.66, 0.26), (0, 0, 2.13), "tech_white", (("tech_security", (0, 1, 0), (-1, 0, 0), (1.10, 0.275)),))
    p.add(box(1.84, 1.84, 0.16, bevel=0.01), flat("steel_dark"), at=(0, 0, 2.34))
    p.add(box(1.50, 0.40, 0.03), flat("wood"), at=(0, 0.55, 1.02))
    return p


def dc_barrier():
    p = Prop("dc_barrier", "lit", note="a vehicle barrier's post 0.36 x 0.36, 1.05 tall, its arm raised beside it to 4.45 m, red and white")
    p.add(box(0.40, 0.40, 0.04), flat("steel_dark"), at=(0, 0, 0.02))
    p.add(box(0.34, 0.34, 1.00, bevel=0.01), "paint_yellow", at=(0, 0, 0.54))
    p.add(box(0.36, 0.36, 0.02), flat("steel_dark"), at=(0, 0, 1.05))
    paint = Paint("paint_white", grade=False)
    for normal in ((0, 1, 0), (0, -1, 0)):
        paint.planar("tech_boom", normal, (0, 0, 1), (0, normal[1] * 0.03, 0), (3.40, 0.09))
    p.add(box(0.09, 0.06, 3.40), paint, at=(0.23, 0, 2.75))
    p.add(box(0.12, 0.10, 0.12), flat("steel_dark"), at=(0.20, 0, 0.98))
    return p


def dc_fence():
    p = Prop("dc_fence", "lit", note="2.50 m of security fence 2.20 tall along X: two posts, rails, bars, an outrigger at the top")
    for x in (-1.22, 1.22):
        p.add(box(0.06, 0.06, 2.20), flat("steel_dark"), at=(x, 0, 1.10))
    for z in (0.12, 1.95):
        p.add(box(2.44, 0.03, 0.04), flat("steel_dark"), at=(0, 0, z))
    for k in range(11):
        p.add(box(0.016, 0.016, 1.90), flat("steel_dark"), at=(-1.0 + k * 0.2, 0, 1.06))
    p.add(moved(box(2.44, 0.30, 0.012), rot=(40, 0, 0)), flat("steel_dark"), at=(0, 0.10, 2.10))
    return p


PROPS = [
    (dc_rack, 130), (dc_rack_ai, 240), (dc_rack_network, 190), (dc_leds(0), 40), (dc_leds(1), 40),
    (dc_cooling, 80), (dc_ups, 70), (dc_tray, 280), (floor_panel("dc_floor", "tech_tiles", 2.40, 0.020), 12),
    (floor_panel("dc_floor_vent", "tech_vent", 0.60, 0.024), 12), (dc_ops_desk, 420),
    (hq_reception, 310), (hq_logo_wall, 170), (hq_screen, 64),
    (tech_rack_small, 126), (tech_bench, 350), (tech_whiteboard, 82),
    (dc_sign, 124), (dc_booth, 242), (dc_barrier, 100), (dc_fence, 212),
]

SIDES = {
    "dc_rack": {"references": (((0.6, 1.07, 2.45), (0.7, 0.0, 1.22)),), "people": (((0.0, 1.2), 180),)},
    "hq_reception": {"people": (((0.0, 0.9), 180), ((0.0, -0.75), 0))},
    "hq_logo_wall": {"people": (((1.2, 1.5), 180),)},
    "dc_booth": {"people": (((1.4, 0.4), 0),)},
}


# ---------------------------------------------------------------------------------------------- whole scenes (scenes.py)

def _server_room():
    """Two pairs of rows as Tech/Sites/SitePlan.cs lays them at tier III: fronts on the cold aisles, backs on the hot."""
    things = []
    hot, cold = 1.0, 1.3
    rows = []
    for pair in range(2):
        v = pair * (2.4 + hot + cold)
        rows.append((v + 0.6, 180))
        rows.append((v + 1.8 + hot, 0))
    for r, (z, turn) in enumerate(rows):
        face = -1 if turn == 180 else 1
        for k in range(8):
            ai = r == 3
            name = "dc_rack_network" if r == 0 and k == 0 else "dc_rack_ai" if ai else "dc_rack"
            things.append((name, (0.3 + 0.6 * k, 0, z + face * (0.0 if ai else 0.065)), turn))
        for x in (0.9, 2.5, 3.8):
            things.append(("dc_tray", (x, 2.45, z), 90))
    for x in (-1.2, 1.2, 3.6, 6.0):
        for z in (-1.2, 1.2, 3.6, 6.0, 8.4):
            things.append(("dc_floor", (x, 0, z), 0))
    for z in (-0.65, 3.65, 8.45):
        for x in (1.2, 3.6):
            things.append(("dc_floor_vent", (x, 0, z), 90))
    for pair in range(2):
        things.append(("dc_cooling", (6.25, 0, pair * 4.7 + 1.7), 270))
    for x in (0.4, 1.3, 2.2):
        things.append(("dc_ups", (x, 0, -1.95), 0))
    things.append(("dc_ops_desk", (-1.6, 0, 3.0), 90))
    walls = [((0.2, 3.4, 12.4), (7.3, 1.7, 3.6)), ((8.6, 3.4, 0.2), (2.9, 1.7, -2.5))]
    people = [((2.6, 3.65), 90), ((-0.75, 3.0), 270)]
    return things, walls, people, ((2.6, 0.9, 2.8), 235, 24, 14.5, 42)


SCENES = {
    "server_room": _server_room(),
    "hq_lobby": (
        [("hq_reception", (0, 0, 0), 0), ("hq_logo_wall", (0, 0, -2.0), 0), ("hq_screen", (-4.38, 1.55, 0.5), 90), ("hq_screen", (4.38, 1.75, -1.0), 270)],
        [((9.0, 3.4, 0.2), (0, 1.7, -3.6)), ((0.2, 3.4, 7.2), (-4.5, 1.7, 0)), ((0.2, 3.4, 7.2), (4.5, 1.7, 0))],
        [((0, -0.85), 0), ((0.4, 1.0), 180), ((-2.6, 2.4), 150)],
        ((0, 1.2, -0.8), 18, 16, 11.0, 40)),
    "tech_office": (
        [("tech_bench", (0, 0, -0.35), 0), ("tech_rack_small", (-2.3, 0, -0.4), 0), ("tech_whiteboard", (2.2, 1.55, -0.70), 0)],
        [((7.0, 3.0, 0.2), (0, 1.5, -0.80))],
        [((0, 0.45), 180)],
        ((0, 1.0, -0.3), 15, 14, 7.0, 40)),
    "dc_frontage": (
        [("dc_sign", (2.6, 0, 1.4), 0), ("dc_booth", (-3.2, 0, 2.4), 0), ("dc_barrier", (-5.0, 0, 3.4), 0),
         ("dc_fence", (3.0, 0, 4.6), 0), ("dc_fence", (5.5, 0, 4.6), 0), ("dc_fence", (8.0, 0, 4.6), 0)],
        [((20.0, 6.0, 0.4), (0, 3.0, -0.4))],
        [((-3.2, 4.0), 180), ((1.4, 2.4), 0)],
        ((0.5, 1.6, 1.5), 20, 12, 19.0, 42)),
}
