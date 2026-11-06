"""THE BUILD MODULE: the second props bundle, reallife_props_site (README, "The second bundle"), for the building sites of
Scripts/Civic/SiteDresser.Plot.cs: new buildings going up on given ground. Same rules as props.py: one function each,
metres, X right, Y forward, Z up, one mesh on the second bundle's own atlas. Unity gets (x, z, y).

THE CODE IS THE SPECIFICATION (Scripts/Civic/CivicProps.cs, CivicStandIns, draws a stand-in of the same size and pivot
until the bundle has the id; Scripts/Civic/PlotRules.cs says where each stands):

    tower_crane_mast     the first bundle's tower crane below its slewing ring: pad, ballast, the 2 m lattice mast to 40 m
    tower_crane_jib      the rest, its pivot the ring's middle: SiteDresser hangs it 40 m up the mast, and the engine's
                         Animation turns it about its own Z (Unity's Y) on that pivot
    site_cabin           6.0 x 2.4 m on its skids, door and windows on +Y; PlotRules.YardSpots stands it against the yard's fence
    site_toilet          1.1 x 1.2 m, its door on +Y, beside the cabin
    site_stack_*         blocks on a pallet, sawn timber, steel bars, down the yard (PlotRules.StackSpots)
    site_earthworks      the dig: a 10 m square of broken earth with a low bank round it, stretched over the frame's footprint
    hammer               the tool of the clip hammer_nail (rake_asphalt holds the first bundle's leaf rake)
"""
import math

import bmesh
import numpy as np

import props_site
from life_props import flat
from prop_kit import Paint, Prop, box, grid, tube


def tower_crane_mast():
    p = Prop("tower_crane_mast", "lit", note="the tower crane's pad, ballast and lattice mast to its slewing ring 40 m up; tower_crane_jib turns on it")
    props_site.crane_mast(p)
    return p


def tower_crane_jib():
    p = Prop("tower_crane_jib", "lit", pivot="the slewing ring's middle",
             note="what turns on the ring, its pivot the ring's middle (40 m up the mast): the jib 30 m along +Y, the counter-jib 10 m, the cab at +X, the hook 13.8 m below")
    props_site.crane_slew(p)
    bmesh.ops.translate(p.bm, verts=p.bm.verts[:], vec=(0.0, 0.0, -40.0))
    return p


def site_cabin():
    p = Prop("site_cabin", "lit", note="a site welfare cabin 6.0 x 2.4 m and 2.6 m high on its skids; its door and two windows on +Y")
    for x in (-2.5, 2.5):
        p.add(box(0.22, 2.4, 0.16), flat("steel_dark"), at=(x, 0.0, 0.08))
    p.add(box(6.0, 2.4, 2.36, bevel=0.03), Paint("build_cladding"), at=(0.0, 0.0, 0.16 + 1.18))
    p.add(box(6.06, 2.46, 0.08, bevel=0.01), flat("steel_dark"), at=(0.0, 0.0, 2.56))
    for x in (-1.5, 0.0, 1.5):
        p.add(box(0.04, 2.42, 2.2), flat("build_cladding"), at=(x, 0.0, 1.32))            # the joints of its panels
    p.add(box(0.9, 0.04, 1.95), flat("paint_blue"), at=(-1.9, 1.21, 0.2 + 0.975))
    p.add(box(0.06, 0.05, 0.12), flat("aluminium"), at=(-1.55, 1.24, 1.1))
    for x in (0.4, 2.0):
        p.add(box(1.24, 0.03, 0.84), flat("steel_dark"), at=(x, 1.205, 1.62))
        p.add(box(1.16, 0.04, 0.76), flat("glass"), at=(x, 1.21, 1.62))
    p.add(box(1.0, 0.5, 0.18), flat("steel_dark"), at=(-1.9, 1.45, 0.09))                  # the step to the door
    return p


def site_toilet():
    p = Prop("site_toilet", "lit", note="a portable toilet 1.1 x 1.2 m and 2.3 m high; its door, with its vent and handle, on +Y")
    p.add(box(1.1, 1.2, 2.14, bevel=0.04), Paint("build_portable"), at=(0.0, 0.0, 1.07))
    p.add(box(1.18, 1.28, 0.14, bevel=0.03), flat("paint_white"), at=(0.0, 0.0, 2.21))
    p.add(box(0.82, 0.03, 1.9), flat("build_portable"), at=(0.0, 0.61, 1.03))
    p.add(box(0.36, 0.03, 0.10), flat("rubber_black"), at=(0.0, 0.625, 1.82))
    p.add(box(0.05, 0.05, 0.16), flat("aluminium"), at=(0.32, 0.64, 1.05))
    return p


def pallet(p, length=1.2, width=1.0):
    p.add(box(length, width, 0.03), Paint("wood"), at=(0.0, 0.0, 0.135))
    for x in (-length * 0.42, 0.0, length * 0.42):
        p.add(box(0.1, width, 0.12), Paint("wood"), at=(x, 0.0, 0.06))


def site_stack_blocks():
    p = Prop("site_stack_blocks", "lit", note="concrete blocks stacked four courses high on a pallet, 1.2 x 1.0 x 1.05 m")
    pallet(p)
    for course in range(4):
        z = 0.15 + course * 0.225
        for row, y in enumerate((-0.23, 0.23)):
            for k, x in enumerate((-0.37, 0.0, 0.37)):
                shift = 0.12 if (course + row) % 2 else 0.0
                p.add(box(0.36, 0.44, 0.215), Paint("build_block"), at=(max(-0.37, min(0.37, x + shift - 0.06)), y, z + 0.1075))
    return p


def site_stack_timber():
    p = Prop("site_stack_timber", "lit", note="sawn timber on two bearers, 1.0 x 3.6 m along Y and 0.6 m high")
    for y in (-1.2, 1.2):
        p.add(box(1.0, 0.1, 0.1), Paint("wood_dark"), at=(0.0, y, 0.05))
    for layer in range(3):
        for k in range(4):
            p.add(box(0.21, 3.6 - 0.08 * ((k + layer) % 3), 0.15), Paint("wood"), at=(-0.36 + k * 0.24, 0.0, 0.1 + 0.075 + layer * 0.16))
    return p


def site_stack_rebar():
    p = Prop("site_stack_rebar", "lit", note="steel bars bundled on bearers, 1.0 x 4.0 m along Y and 0.35 m high")
    for y in (-1.4, 1.4):
        p.add(box(1.0, 0.1, 0.1), Paint("wood_dark"), at=(0.0, y, 0.05))
    for layer in range(3):
        for k in range(7):
            p.add(box(0.032, 4.0, 0.032), flat("build_rust"), at=(-0.3 + k * 0.1 + 0.05 * (layer % 2), 0.0, 0.1 + 0.02 + layer * 0.07))
    for y in (-0.9, 0.9):
        p.add(box(0.76, 0.03, 0.24), flat("steel_dark"), at=(0.0, y, 0.2))
    return p


def site_earthworks():
    p = Prop("site_earthworks", "lit", note="the ground dug over: a 10 m square of broken earth, a bank of spoil 0.4 m high round it; stretched over a frame's footprint")
    lumps = np.random.default_rng(311).uniform(-1.0, 1.0, (13, 13))

    def ground(u, v):
        x, y = (u - 0.5) * 10.0, (v - 0.5) * 10.0
        edge = max(abs(x), abs(y))
        bank = 0.38 * max(0.0, 1.0 - abs(edge - 4.55) / 0.45)
        height = 0.03 + bank + 0.03 * lumps[round(v * 12), round(u * 12)] * (1.0 if edge < 4.0 else 0.4)
        return (x, y, max(0.005, height))
    p.add(grid(ground, 20, 20), Paint("build_earth"), sharp=45)
    return p


def hammer():
    p = Prop("hammer", "lit", pivot="the end of the handle; the handle is Z", note="a claw hammer 0.33 m long: held at 0.10 m, its face at +X")
    p.add(tube([(0.0, 0.0, 0.0), (0.0, 0.0, 0.30)], 0.015, 8), Paint("rubber_black", grade=False))
    p.add(box(0.035, 0.03, 0.05), Paint("steel_dark", grade=False), at=(0.0, 0.0, 0.31))
    p.add(tube([(0.0, 0.0, 0.315), (0.07, 0.0, 0.315)], 0.016, 8), Paint("chrome", grade=False))
    for side in (-1, 1):
        p.add(tube([(0.0, side * 0.006, 0.32), (-0.05, side * 0.008, 0.33), (-0.09, side * 0.010, 0.30)], 0.007, 5), Paint("steel_dark", grade=False))
    return p


# (maker, triangle budget). The second bundle's budget is 12 000 triangles (build_props.py).
PROPS = [
    (tower_crane_mast, 800), (tower_crane_jib, 900),
    (site_cabin, 300), (site_toilet, 140), (site_stack_blocks, 420), (site_stack_timber, 200), (site_stack_rebar, 380), (site_earthworks, 900),
    (hammer, 240),
]

SIDES = {
    "site_cabin": {"people": (((-1.9, 1.9), 180),)},
    "site_toilet": {"people": (((0.0, 1.3), 180),)},
}
