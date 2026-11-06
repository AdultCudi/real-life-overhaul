"""THE LANDMARKS MODULE: Crest Field, Uptown's stadium (Scripts/Landmarks), in the second bundle beside the building sites'
props (README, "The second bundle"). Same rules as props.py: one function each, metres, X right, Y forward, Z up, one mesh
on the second bundle's atlas. Unity gets (x, z, y).

THE CODE IS THE SPECIFICATION (Scripts/Landmarks/LandmarkRules.cs lays the pieces out, LandmarksGame.cs draws plain
shapes of the same size until the bundle has them and gives each solid piece its box):

    stadium_stand        a 10 m section of a main stand, 12 m deep: a front wall, nine rows of seats rising away from
                         the pitch, a concourse and the back wall to the street, and a roof held by two masts and their
                         ties; its seats face +Y (the pitch). Sections stand side by side, stretched along X to the length
    stadium_end          a 10 m section of an end stand, 9 m deep: seven rows, no roof; its seats face +Y
    stadium_corner       a stair tower 9 m along X and 12 m along Y filling a corner of the bowl: the same from every side,
                         so it stands at all four corners unturned
    stadium_floodlight   a mast 34 m high with its lamp frame at the top, tilted down towards +Y
    stadium_lamps        the frame's twelve lamps, the glow material, stood where the mast stands
    stadium_pitch        the field the stands leave: 52 x 48 m of grass mown in stripes with its lines and a running
                         track round it, 4 cm off the ground, stretched to the field

Sizes the code relies on (LandmarkRules: Section 10, StandDepth 12, EndDepth 9, StandHeight 15, EndHeight 8, CornerHeight
10, FloodlightHeight 34, PitchWidth 52, PitchDepth 48), every pivot on the ground in the middle of the footprint.
"""
import math

from life_props import flat
from prop_kit import Paint, Prop, box, grid

SECTION = 10.0
TREAD = 1.0           # m of each row, back to front
RISE = 0.85           # m each row stands over the one in front
FRONT_WALL = 1.2      # m: the wall between the front row and the pitch
RUNS = 5              # runs of seats along a section
TILT = -20.0          # degrees the lamp frame looks down at the pitch


def seats(p, rows, front, z0):
    """Rows of seats on their treads: runs of four, a pale band every fourth row; the front row's front at y = front."""
    run = SECTION / RUNS
    for k in range(rows):
        top = z0 + k * RISE
        y = front - (k + 0.5) * TREAD
        p.add(box(SECTION, TREAD, top), flat("stadium_concrete"), at=(0.0, y, top * 0.5))
        paint = "stadium_seat_light" if k % 4 == 3 else "stadium_seat"
        for r in range(RUNS):
            x = -SECTION / 2 + (r + 0.5) * run
            p.add(box(run - 0.12, 0.44, 0.10), flat(paint), at=(x, y + 0.14, top + 0.42))
            p.add(box(run - 0.12, 0.07, 0.42), flat(paint), at=(x, y - 0.12, top + 0.64))
    return front - rows * TREAD, z0 + (rows - 1) * RISE


def stadium_stand():
    p = Prop("stadium_stand", "lit", note="a 10 m section of a main stand, 12 m deep and 15 m to its masts: nine rows facing +Y, the back wall to the street, a cable-held roof")
    p.add(box(SECTION, 0.3, FRONT_WALL), Paint("stadium_concrete"), at=(0.0, 5.85, FRONT_WALL * 0.5))
    back, top = seats(p, 9, 5.7, FRONT_WALL)
    p.add(box(SECTION, back + 5.6, 0.3), flat("stadium_concrete"), at=(0.0, (back - 5.6) * 0.5, top + 0.15))   # the concourse behind the top row
    p.add(box(SECTION, 0.4, 11.0), Paint("stadium_concrete"), at=(0.0, -5.8, 5.5))
    p.add(box(SECTION, 0.06, 1.2), flat("stadium_seat"), at=(0.0, -6.02, 9.4))                                 # the district's colour along the street
    for x in (-3.8, 3.8):
        p.add(box(1.6, 0.06, 2.6), flat("steel_dark"), at=(x, -6.02, 1.3))                                     # the gates under the stand
    p.add(box(SECTION, 10.6, 0.22), Paint("stadium_roof", grade=False), at=(0.0, 0.6, 12.6), rot=(3.0, 0.0, 0.0))
    p.add(box(SECTION, 0.16, 0.7), flat("stadium_steel"), at=(0.0, 5.85, 13.2))
    for x in (-4.0, 4.0):
        p.add(box(0.36, 0.36, 4.2), flat("stadium_steel"), at=(x, -5.5, 12.9))                                 # a mast over the back wall
        tie = math.hypot(11.3, 1.9)
        p.add(box(0.08, tie, 0.08), flat("stadium_steel"), at=(x, 0.15, 14.05), rot=(math.degrees(math.atan2(-1.9, 11.3)), 0.0, 0.0))
    return p


def stadium_end():
    p = Prop("stadium_end", "lit", note="a 10 m section of an end stand, 9 m deep and 7.6 m high: seven rows facing +Y, open to the sky")
    p.add(box(SECTION, 0.3, FRONT_WALL), Paint("stadium_concrete"), at=(0.0, 4.35, FRONT_WALL * 0.5))
    back, top = seats(p, 7, 4.2, FRONT_WALL)
    p.add(box(SECTION, back + 4.1, 0.3), flat("stadium_concrete"), at=(0.0, (back - 4.1) * 0.5, top + 0.15))
    p.add(box(SECTION, 0.4, 7.6), Paint("stadium_concrete"), at=(0.0, -4.3, 3.8))
    p.add(box(SECTION, 0.06, 0.9), flat("stadium_seat"), at=(0.0, -4.52, 6.6))
    p.add(box(1.6, 0.06, 2.6), flat("steel_dark"), at=(0.0, -4.52, 1.3))
    return p


def stadium_corner():
    p = Prop("stadium_corner", "lit", note="a stair tower 9 x 12 m and 9.6 m high filling a corner of the bowl, alike from every side")
    p.add(box(8.6, 11.6, 9.2, bevel=0.08), Paint("stadium_concrete"), at=(0.0, 0.0, 4.6))
    p.add(box(8.8, 11.8, 0.3), flat("stadium_steel"), at=(0.0, 0.0, 9.35))
    for z in (3.4, 6.6):
        p.add(box(8.7, 11.7, 0.14), flat("stadium_concrete"), at=(0.0, 0.0, z))                                # the landings' lines
    p.add(box(8.66, 11.66, 0.9), flat("stadium_seat"), at=(0.0, 0.0, 8.2))
    for side in (-1, 1):
        p.add(box(1.8, 0.06, 2.6), flat("steel_dark"), at=(0.0, side * 5.83, 1.3))
        p.add(box(0.06, 1.8, 2.6), flat("steel_dark"), at=(side * 4.33, 0.0, 1.3))
        for z in (4.6, 7.6):
            p.add(box(2.6, 0.06, 0.9), flat("glass"), at=(0.0, side * 5.83, z))
            p.add(box(0.06, 2.6, 0.9), flat("glass"), at=(side * 4.33, 0.0, z))
    return p


def frame_point(x, y, z):
    """A point of the lamp frame, made level about its own middle, tilted to look down at the pitch."""
    a = math.radians(TILT)
    return (x, 0.45 + y * math.cos(a) - z * math.sin(a), 32.3 + y * math.sin(a) + z * math.cos(a))


def stadium_floodlight():
    p = Prop("stadium_floodlight", "lit", note="a floodlight mast 34 m high, its lamp frame at the top tilted down towards +Y; stadium_lamps hangs on it")
    p.add(box(1.6, 1.6, 0.6, bevel=0.04), Paint("stadium_concrete"), at=(0.0, 0.0, 0.3))
    for z0, z1, side in ((0.6, 12.0, 1.0), (12.0, 24.0, 0.8), (24.0, 32.0, 0.6)):
        p.add(box(side, side, z1 - z0), Paint("stadium_steel"), at=(0.0, 0.0, (z0 + z1) * 0.5))
    p.add(box(5.6, 0.3, 3.4), flat("stadium_steel"), at=frame_point(0.0, 0.0, 0.0), rot=(TILT, 0.0, 0.0))
    p.add(box(0.5, 0.6, 1.2), flat("stadium_steel"), at=(0.0, 0.25, 31.5))
    return p


def stadium_lamps():
    p = Prop("stadium_lamps", "glow", note="the twelve lamps of a floodlight's frame, 34 m up, looking down towards +Y: stood where stadium_floodlight stands")
    for row in range(3):
        for col in range(4):
            x, z = -2.1 + col * 1.4, -1.05 + row * 1.05
            p.add(box(1.2, 0.18, 0.86), flat("stadium_lamp"), at=frame_point(x, 0.22, z), rot=(TILT, 0.0, 0.0))
    return p


def stadium_pitch():
    p = Prop("stadium_pitch", "lit", note="the field, 52 x 48 m and 4 cm off the ground: grass in stripes, a pitch's lines, a running track round it")
    p.add(grid(lambda u, v: ((u - 0.5) * 52.0, (v - 0.5) * 48.0, 0.04), 1, 1), flat("stadium_concrete").param("stadium_pitch"), sharp=180)
    return p


# (maker, triangle budget). The second bundle's budget is 12 000 triangles (build_props.py).
PROPS = [
    (stadium_stand, 1600), (stadium_end, 1200), (stadium_corner, 500), (stadium_floodlight, 300), (stadium_lamps, 200), (stadium_pitch, 20),
]
