"""Shoes, built the way a shoe is: a sole unit, an upper lasted onto it, a padded collar, a tongue and laces.

    blender -b -P build_shoes.py

Every style is described in one place (STYLES) and comes out of the same construction:

  last      the plan of the sole (width along the foot) and the height of the upper along the foot, taken a few
            millimetres outside the game's foot, which is only measured (scripts/ba_common.load_reference);
  upper     a surface over (around the foot, up from the sole): out of the welt, up the side, in over the foot to
            a narrow flat top in front of the throat, and behind it to the collar, an oval round the ankle;
  lining    the collar rolled in and down; roof: the flat top of the vamp, where the laces lie;
  sole      welt, a stepped midsole wall with a groove, the ground edge, the tread plan;
  tongue    a padded slab that follows the front of the collar; laces: flat bars across the instep.

The same (around, up) coordinates are the texture's layout, so panels, seams, stitching, perforations and mesh
panels are painted exactly where the geometry is: paint. Colour is in the texture (the mod's cloth materials take
it from there), so a style comes in several colourways that share its meshes and its normal map.
Skin weights and the Fat/Slim/Musc shapes are sampled from the foot.
Writes build/meshes/feet_<m|f>_<style>.json, build/textures/feet_<style>_*.png and build/catalogue_shoes.json.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba  # noqa: E402
from build_wardrobe import Form, top_four, vertex_normals  # noqa: E402

CX = 0.15            # the left foot's centre line (Blender +X is the character's left); the right shoe is its mirror
AROUND, UP = 44, 9   # resolution of the upper
SIZE = 1024          # the normal map; the colour maps are half that (their detail is flat panels)


def curve(points):
    xs, ys = np.array([p[0] for p in points]), np.array([p[1] for p in points])
    return lambda x: np.interp(x, xs, ys)


class Part:
    """A piece of mesh with its own smooth normals (parts are not welded to each other: their joins are creases)."""

    def __init__(self):
        self.vertices, self.uv, self.triangles = [], [], []

    def grid(self, points, uvs, flip=False):
        """points: (rows, columns, 3). Adds the quads between neighbouring rows and columns."""
        rows, columns = points.shape[:2]
        base = len(self.vertices)
        self.vertices += points.reshape(-1, 3).tolist()
        self.uv += uvs.reshape(-1, 2).tolist()
        for r in range(rows - 1):
            for c in range(columns - 1):
                a, b = base + r * columns + c, base + r * columns + c + 1
                d, e = a + columns, b + columns
                self.triangles += [[a, d, b], [b, d, e]] if flip else [[a, b, d], [b, e, d]]

    def arrays(self):
        return np.array(self.vertices), np.array(self.uv), np.array(self.triangles, dtype=np.int64)


def closed_ring(rows):
    return np.concatenate([rows, rows[:, :1]], axis=1)


def build(spec):
    width = curve(spec["width"])              # half width of the last along the foot (y: heel positive, toe negative)
    ridge = curve(spec["ridge"])              # height of the upper on the centre line
    sole_top = curve(spec["sole_top"])        # where the upper meets the sole
    toe, heel, throat = spec["toe"], spec["heel"], spec["throat"]

    phi = np.linspace(0, 2 * np.pi, AROUND, endpoint=False)          # 0 = back of the heel, pi = tip of the toe
    middle, half = (heel + toe) / 2, (heel - toe) / 2
    y = middle + half * np.cos(phi)
    x = width(y) * np.sign(np.sin(phi)) * np.abs(np.sin(phi)) ** 0.75
    q = 1 - np.abs(phi / np.pi - 1)                                  # 0 at the heel, 1 at the toe, on both sides
    turn = phi / (2 * np.pi)

    # ---- upper. Where a column ends: in front of the throat on the edge of the vamp's flat top (a strip along the
    # centre line that widens into the toe box), behind it on the collar, an oval round the ankle whose front point
    # is the throat. One continuous curve, so the top line is clean all the way round.
    q_throat = math.acos((throat - middle) / half) / math.pi
    ankle_y, ankle_x, ankle_back = spec.get("ankle_y", 0.014), spec.get("ankle_x", 0.046), spec.get("ankle_back", 0.047)
    alpha = np.pi * np.clip(q / q_throat, 0, 1)                             # 0 at the back of the collar, pi at the throat
    closed = (q >= q_throat).astype(float)
    collar = spec["collar_side"] + (spec["collar_back"] - spec["collar_side"]) * np.clip(1 - q / 0.2, 0, 1) ** 2
    collar = collar + (ridge(throat) - collar) * np.clip((q / q_throat - 0.6) / 0.4, 0, 1) ** 2
    # Half the width of the flat top: a finger wide at the throat, the toe box's width at the toe (a blunt, boxed toe).
    # Half the width of the vamp's flat top: a finger wide at the throat, the toe box's width at the toe (a boxed toe).
    plateau = (0.005 + 0.017 * np.clip((q - q_throat) / (1 - q_throat), 0, 1) ** 1.5) * closed
    target = np.zeros((AROUND, 3))
    target[:, 0] = np.where(closed > 0, CX + np.sign(np.sin(phi)) * np.minimum(np.abs(x) * 0.9, plateau), CX + np.sign(np.sin(phi)) * ankle_x * np.sin(alpha))
    target[:, 1] = np.where(closed > 0, y, ankle_y + np.where(np.cos(alpha) > 0, ankle_back, ankle_y - throat) * np.cos(alpha))
    target[:, 2] = np.where(closed > 0, ridge(y), collar)
    # The toe comes back along the foot as it comes in, so it is a rounded box and not a wedge.
    target[:, 1] += closed * np.clip((q - 0.78) / 0.22, 0, 1) ** 2 * 0.55 * (spec["toe_top"] - y)
    base_z = sole_top(y)
    foot = np.stack([CX + x, y, base_z], axis=1)
    h3 = np.linspace(0, 1, UP)[:, None, None]
    h = h3[..., 0]
    bulge = spec["bulge"] * np.sin(np.pi * np.minimum(h * 1.6, 1.0)) * (0.4 + 0.6 * np.sin(np.pi * np.clip(q, 0.02, 0.98)))[None, :]
    upper = foot[None] + (target - foot)[None] * h3 ** spec["roundness"]
    # The sides swell a little; behind the throat the quarters stand off the ankle bones instead of running straight to the collar.
    upper[..., 0] += np.sign(x)[None, :] * (bulge + 0.010 * (1 - closed)[None, :] * np.sin(np.pi * h ** 1.5) ** 0.7)
    upper[..., 2] = base_z[None, :] + (target[:, 2] - base_z)[None, :] * (1 - (1 - h) ** 1.7)

    shoe = Part()
    uv = np.dstack([np.tile(turn[None, :], (UP, 1)), 0.45 + 0.53 * np.tile(h, (1, AROUND))])
    wrap_uv = np.concatenate([uv, np.dstack([np.ones((UP, 1)), uv[:, :1, 1]])], axis=1)      # the texture wraps at the heel
    shoe.grid(closed_ring(upper), wrap_uv)

    # ---- the vamp's flat top: joins the two sides in front of the throat
    rim = upper[-1]
    front = np.where(q >= q_throat - 1e-6)[0]
    left = front[front <= AROUND // 2]                      # from the throat forward to the toe, on one side
    right = (AROUND - left) % AROUND                        # the same stations on the other side
    roof = Part()
    crown = (rim[left] + rim[right]) / 2
    crown[:, 2] += 0.0015
    roof_uv = np.array([uv[-1, left], uv[-1, left], uv[-1, right]])
    roof_uv[:, :, 1] = 0.45 + 0.53 * 0.985       # the leather of the top edge, on all of it
    roof.grid(np.array([rim[left], crown, rim[right]]), roof_uv)

    # ---- collar lining: the top line rolled in and down round the ankle
    lining = Part()
    centre = np.stack([np.full(AROUND, CX), np.full(AROUND, ankle_y), rim[:, 2]], axis=1)
    inwards = (centre - rim) / np.maximum(np.linalg.norm(centre - rim, axis=1, keepdims=True), 1e-6)
    rows = []
    for inset, drop in ((0.0, 0.0), (0.003, 0.004), (0.007, 0.003), (0.009, -0.006), (0.009, -0.04)):
        row = rim + inwards * inset
        row[:, 2] += drop
        rows.append(row)
    rows = np.array(rows)
    lining_uv = np.dstack([0.02 + 0.3 * np.tile(turn[None, :], (5, 1)), 0.34 + 0.08 * np.tile(np.linspace(0, 1, 5)[:, None], (1, AROUND))])
    back = np.where(q <= q_throat + 1e-6)[0]
    order = np.concatenate([back[back > AROUND // 2], back[back <= AROUND // 2]])      # one run through the back of the heel
    lining.grid(rows[:, order], lining_uv[:, order])

    # ---- sole: welt, a midsole wall in two steps with a groove between them, the ground edge, the tread plan
    sole = Part()
    welt = spec["welt"]
    spring = spec["spring"] * np.clip((q - 0.7) / 0.3, 0, 1) ** 2
    # A heeled sole: the ground line lifts under the waist of the foot, leaving a heel block behind it.
    waist = np.clip((spec.get("heel_breast", 0.0) - y) / 0.012, 0, 1) * np.clip((y - spec.get("ball", -0.13)) / 0.03, 0, 1)
    spring = spring + spec.get("arch", 0.0) * waist
    out = np.stack([x / np.maximum(np.abs(x), 0.012), -np.cos(phi)], axis=1)       # outward, in plan
    out /= np.maximum(np.linalg.norm(out, axis=1, keepdims=True), 1e-6)

    def ring(push, level):
        return np.stack([CX + x + out[:, 0] * push, y + out[:, 1] * push, spring + (base_z - spring) * level], axis=1)
    step = spec.get("step", 0.0025)
    wall = np.array([ring(-0.004, 1.0) + [0, 0, 0.001], ring(welt, 1.0), ring(welt, 0.62), ring(welt - step, 0.56), ring(welt - step, 0.50),
                     ring(welt + step * 0.6, 0.44), ring(welt + step * 0.6, 0.10), ring(welt - 0.002, 0.0)])
    levels = np.array([0.335, 0.32, 0.29, 0.283, 0.277, 0.27, 0.23, 0.215])
    wall_uv = np.dstack([np.tile(turn[None, :], (len(wall), 1)), np.tile(levels[:, None], (1, AROUND))])
    sole.grid(closed_ring(wall), np.concatenate([wall_uv, np.dstack([np.ones((len(wall), 1)), wall_uv[:, :1, 1]])], axis=1), flip=True)
    ground = wall[-1]
    tread = ground.copy()
    tread[:, 0] = CX + (ground[:, 0] - CX) * 0.55
    tread[:, 1] = middle + (ground[:, 1] - middle) * 0.9
    spine = np.stack([np.full(AROUND, CX), tread[:, 1], spring], axis=1)
    under = closed_ring(np.array([ground, tread, spine]))
    plan = np.stack([0.02 + (under[..., 0] - CX + 0.07) / 0.14 * 0.28, 0.01 + (heel + 0.01 - under[..., 1]) / (heel - toe + 0.02) * 0.19], axis=-1)
    sole.grid(under, plan, flip=True)
    parts = [shoe, roof, lining, sole]

    # ---- tongue: a padded slab following the front of the collar's oval, standing a little proud of the top line
    if spec.get("tongue"):
        tongue = Part()
        sweep = np.linspace(-1.0, 1.0, 9) * spec.get("tongue_sweep", 0.75)
        section = [(0.0, -0.018), (0.0, 0.3), (0.001, 0.8), (0.004, 1.0), (0.008, 0.8), (0.009, 0.3), (0.009, -0.018)]   # (inward, up) round the edge
        grid = np.zeros((len(section), 9, 3))
        for r, (inset, rise) in enumerate(section):
            radius_x, radius_y = ankle_x - 0.003 - inset, ankle_y - throat - 0.002 - inset
            grid[r, :, 0] = CX + radius_x * np.sin(sweep)
            grid[r, :, 1] = ankle_y - radius_y * np.cos(sweep) - 0.006 * max(rise, 0)
            grid[r, :, 2] = ridge(throat) + spec["tongue_rise"] * rise - 0.012 * (sweep / sweep.max()) ** 2
        tongue_uv = np.dstack(np.meshgrid(0.36 + 0.14 * (sweep / sweep.max() + 1) / 2, 0.22 + 0.2 * np.linspace(0, 1, len(section))))
        tongue.grid(grid, tongue_uv, flip=True)
        parts.append(tongue)

    # ---- laces: flat bars across the vamp's top, from one eyelet row to the other
    laces = Part()
    for k, lace_y in enumerate(spec["laces"]):
        column = int(np.argmin(np.abs(y[:AROUND // 2] - lace_y)))
        a, b = upper[int(UP * 0.78), column], upper[int(UP * 0.78), (AROUND - column) % AROUND]
        span = np.linspace(0, 1, 6)
        path = a[None, :] * (1 - span[:, None]) + b[None, :] * span[:, None]
        path[:, 2] = np.maximum(path[:, 2], ridge(lace_y) + 0.0015 - 0.006 * np.abs(2 * span - 1) ** 3) + 0.003
        path[:, 1] += (0.004 if k % 2 else -0.004) * (2 * span - 1)
        profile = np.array([[0, -0.0035, 0.0], [0, -0.002, 0.002], [0, 0.002, 0.002], [0, 0.0035, 0.0], [0, 0.0, -0.002]])
        bar = closed_ring(path[:, None, :] + profile[None, :, :])
        bar_uv = np.dstack(np.meshgrid(0.55 + 0.03 * np.arange(6) / 5 + 0.04 * (k % 4), 0.22 + 0.2 * span))
        laces.grid(bar, bar_uv)
    parts.append(laces)
    return parts


def finish(mesh_id, parts, form):
    """Both shoes, welded part by part for smooth normals, with the foot's weights and shapes."""
    vertices, uvs, triangles, normals = [], [], [], []
    offset = 0
    for mirror in (False, True):
        for part in parts:
            v, uv, t = part.arrays()
            if mirror:
                v = v * np.array([-1.0, 1.0, 1.0])
            else:
                t = t[:, ::-1]      # the grids are laid out clockwise; mirroring turns them round by itself
            keys = np.round(v * 50000).astype(np.int64)
            _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
            welded = inverse.reshape(-1)[t]
            welded = welded[(welded[:, 0] != welded[:, 1]) & (welded[:, 1] != welded[:, 2]) & (welded[:, 0] != welded[:, 2])]
            vertices.append(v)
            uvs.append(uv)
            normals.append(vertex_normals(v[first], welded)[inverse.reshape(-1)])
            triangles.append(t + offset)
            offset += len(v)
    vertices, uvs, normals, triangles = np.concatenate(vertices), np.concatenate(uvs), np.concatenate(normals), np.concatenate(triangles)
    weights, shapes = form.sample(vertices)
    indices, four = top_four(weights)
    return ba.write_mesh(mesh_id, vertices, normals, uvs, [triangles], indices, four, [(n, shapes[n]) for n in form.shape_order])


# ---------------------------------------------------------------- texture


def paint(style, spec, colourways):
    """One normal map for the style and one colour map per colourway, in the layout the parts were given:
    upper on top, sole wall and tread plan, lining, tongue, laces."""
    from PIL import Image
    rng = np.random.default_rng(3)
    v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    v = 1 - v
    grain = rng.random((SIZE, SIZE))
    for _ in range(2):
        grain = (grain + np.roll(grain, 1, 0) + np.roll(grain, 1, 1) + np.roll(grain, (1, 1), (0, 1))) / 4

    # ---- upper: u = around (0 back of heel, 0.5 toe), up = from the welt to the top line
    up = (v - 0.45) / 0.53
    upper = (up >= 0) & (up <= 1)
    q = 1 - np.abs(u * 2 - 1)
    toe_cap = q - (0.80 - 0.07 * up)
    heel_counter = (0.17 + 0.05 * np.sin(np.clip(up, 0, 1) * np.pi)) - q
    eyestay = np.minimum(np.minimum(q - 0.42, 0.80 - q), up - (0.50 + 0.25 * np.abs(q - 0.62)))
    foxing = (0.18 + 0.10 * np.clip((q - 0.55) / 0.3, 0, 1)) - up
    panel = np.zeros((SIZE, SIZE))
    stitch = np.zeros((SIZE, SIZE))
    step_shade = np.zeros((SIZE, SIZE))
    for field, raised in ((toe_cap, spec["toe_cap"]), (heel_counter, True), (eyestay, True), (foxing, spec["foxing"])):
        if not raised:
            continue
        panel = np.maximum(panel, np.clip(field / 0.004, 0, 1))
        # The shadow of a panel's edge, fading out before the top line (carried to the rim it reads as a nick in it).
        step_shade = np.maximum(step_shade, np.exp(-(np.minimum(field, 0) / 0.006) ** 2) * (field < 0) * (1 - np.clip((up - 0.8) / 0.15, 0, 1)))
        for inset in (0.012, 0.022):
            line = np.exp(-((field - inset) / 0.0016) ** 2)
            stitch = np.maximum(stitch, line * (np.sin((u * 2 + v) * 2 * np.pi * 110) > -0.3))
    holes = np.zeros((SIZE, SIZE))
    if spec["perforations"]:
        for row in range(3):
            for k in range(7):
                centre_q = 0.50 + 0.035 * k + 0.012 * row
                holes = np.maximum(holes, np.exp(-(((q - centre_q) / 0.0035) ** 2 + ((up - 0.30 - 0.1 * row + (centre_q - 0.5) * 0.9) / 0.012) ** 2)))
    # Knitted mesh where no leather lies (a runner): a fine honeycomb.
    mesh = np.zeros((SIZE, SIZE))
    accent_area = np.clip(heel_counter / 0.004, 0, 1)
    if spec.get("mesh"):
        mesh = (1 - panel) * (0.5 + 0.5 * np.sin(u * 2 * np.pi * 150) * np.sin(v * 2 * np.pi * 150))
        accent_area = np.maximum(accent_area, np.clip(eyestay / 0.004, 0, 1))
    height = np.zeros((SIZE, SIZE))
    height[upper] = (panel * 0.5 + stitch * 0.12 - holes * 0.6 + (grain - 0.5) * spec["grain"] - mesh * 0.35)[upper]

    # ---- sole wall (v 0.215 to 0.335): the groove is geometry; teeth along the lower step if the style has them
    wall = (v >= 0.21) & (v < 0.34)
    band = (v - 0.215) / 0.12
    teeth = (np.sin(u * 2 * np.pi * 90) > 0.2) * (band < 0.45) * (1.0 if spec.get("teeth") else 0.0)
    height[wall] = (-teeth * 0.7 + (grain - 0.5) * 0.1)[wall]
    plan = (v < 0.21) & (u < 0.32)
    tread = (np.sin((u * 3 + v * 1.4) * 2 * np.pi * 26) > 0) & (np.sin((u * 3 - v * 1.4) * 2 * np.pi * 26) > -0.4)
    height[plan] = tread[plan] * 1.0
    tongue = (u >= 0.35) & (u < 0.51) & (v >= 0.21) & (v < 0.43)
    height[tongue] = ((grain - 0.5) * spec["grain"])[tongue]
    lace = (u >= 0.54) & (u < 0.75) & (v >= 0.21) & (v < 0.43)
    weave = 0.5 + 0.5 * np.sin((u * 9 + v) * 2 * np.pi * 60) * np.sin((u * 9 - v) * 2 * np.pi * 60)
    height[lace] = weave[lace] * 0.5
    lining = (v >= 0.335) & (v < 0.45) & (u < 0.34)

    folder = os.path.join(ba.BUILD, "textures")
    os.makedirs(folder, exist_ok=True)
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * spec["relief"]
    dy = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * spec["relief"]
    normal = np.dstack([-dx, -dy, np.ones_like(dx)])
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    name = "feet_" + style
    Image.fromarray(((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(folder, name + "_n.png"))
    textures = [{"name": name + "_n", "file": "textures/" + name + "_n.png", "type": "normal", "maxSize": SIZE}]

    for colour_name, c in colourways.items():
        colour = np.ones((SIZE, SIZE, 3)) * np.array(c["lining"])
        tint = np.ones((SIZE, SIZE, 3)) * np.array(c["upper"])
        tint = tint * (1 - accent_area[..., None]) + np.array(c["accent"]) * accent_area[..., None]
        thread = np.array(c.get("thread", c["accent"]))
        shade = 1 - 0.5 * holes - 0.18 * step_shade - 0.25 * np.exp(-(up / 0.05) ** 2) - 0.22 * mesh
        tint = tint * np.clip(shade, 0, 1.2)[..., None]
        tint = tint * (1 - stitch[..., None] * 0.8) + thread * stitch[..., None] * 0.8
        colour[upper] = tint[upper]
        colour[wall] = (np.array(c["sole"]) * (0.86 + 0.14 * np.clip(band * 3, 0, 1) - 0.25 * teeth)[..., None])[wall]
        colour[plan] = (np.array(c["outsole"]) * (0.75 + 0.25 * tread)[..., None])[plan]
        colour[tongue] = (np.array(c["upper"]) * (0.9 + 0.1 * grain)[..., None])[tongue]
        colour[lace] = (np.array(c["lace"]) * (0.8 + 0.2 * weave)[..., None])[lace]
        colour[lining] = (np.array(c["lining"]) * (0.85 + 0.15 * grain)[..., None])[lining]
        rgba = np.dstack([np.clip(colour, 0, 1), np.ones((SIZE, SIZE))])
        image = Image.fromarray((rgba * 255).astype(np.uint8)).resize((SIZE // 2, SIZE // 2), Image.LANCZOS)
        image.save(os.path.join(folder, "%s_%s.png" % (name, colour_name)))
        textures.append({"name": "%s_%s" % (name, colour_name), "file": "textures/%s_%s.png" % (name, colour_name), "type": "color", "maxSize": SIZE // 2})
    return textures


WHITE, OFF, NAVY, GUM = (0.93, 0.93, 0.91), (0.88, 0.87, 0.83), (0.08, 0.11, 0.22), (0.62, 0.45, 0.27)
BLACK, GREY, GREEN, RED = (0.05, 0.05, 0.055), (0.45, 0.46, 0.48), (0.10, 0.30, 0.18), (0.62, 0.10, 0.09)
BROWN, DARK, TAN, OXBLOOD = (0.33, 0.18, 0.10), (0.16, 0.10, 0.07), (0.55, 0.38, 0.22), (0.30, 0.07, 0.07)


def way(upper, accent, sole, outsole, lace, lining=(0.82, 0.8, 0.76), thread=None):
    entry = {"upper": upper, "accent": accent, "sole": sole, "outsole": outsole, "lace": lace, "lining": lining}
    if thread is not None:
        entry["thread"] = thread
    return entry


# The last: a few millimetres outside the game's foot (the same for both sexes: their feet differ by a millimetre).
LAST = {
    "toe": -0.252, "heel": 0.070, "toe_top": -0.19,
    "width": [(-0.252, 0.040), (-0.215, 0.054), (-0.14, 0.058), (-0.06, 0.052), (0.0, 0.050), (0.07, 0.043)],
    "ridge": [(-0.252, 0.044), (-0.215, 0.058), (-0.15, 0.070), (-0.10, 0.094), (-0.06, 0.122), (0.0, 0.134)],
    "bulge": 0.004, "roundness": 2.6, "tongue": True,
}

STYLES = {
    # A low court sneaker: plain leather upper, perforated quarters, padded collar, cup sole.
    "court": (dict(LAST, throat=-0.045, sole_top=[(-0.252, 0.026), (-0.2, 0.022), (-0.1, 0.024), (0.07, 0.032)],
                   collar_side=0.090, collar_back=0.112, welt=0.004, spring=0.008, tongue_rise=0.011,
                   laces=(-0.135, -0.118, -0.101, -0.084, -0.067),
                   toe_cap=False, foxing=False, perforations=True, grain=0.25, relief=2.2),
              {"white": way(WHITE, NAVY, OFF, GUM, WHITE, thread=(0.7, 0.7, 0.68)),
               "green": way(WHITE, GREEN, OFF, OFF, WHITE, thread=(0.7, 0.7, 0.68)),
               "black": way(BLACK, (0.12, 0.12, 0.13), OFF, (0.8, 0.8, 0.78), BLACK, (0.2, 0.2, 0.2), (0.2, 0.2, 0.2)),
               "grey": way(GREY, (0.3, 0.31, 0.33), OFF, GUM, OFF, thread=(0.36, 0.37, 0.38))}),
    # A runner: knitted mesh under leather overlays, a thick stepped midsole with a toothed edge.
    "runner": (dict(LAST, throat=-0.05, sole_top=[(-0.252, 0.030), (-0.2, 0.028), (-0.1, 0.034), (0.07, 0.046)],
                    ridge=[(-0.252, 0.050), (-0.215, 0.064), (-0.15, 0.078), (-0.10, 0.100), (-0.06, 0.128), (0.0, 0.142)],
                    collar_side=0.102, collar_back=0.126, welt=0.007, spring=0.014, step=0.0035, tongue_rise=0.013,
                    laces=(-0.14, -0.123, -0.106, -0.089, -0.072),
                    toe_cap=True, foxing=True, perforations=False, mesh=True, teeth=True, grain=0.2, relief=2.4),
               {"grey": way((0.62, 0.64, 0.66), (0.10, 0.25, 0.55), WHITE, (0.12, 0.12, 0.13), WHITE, thread=(0.5, 0.5, 0.52)),
                "black": way((0.10, 0.10, 0.11), RED, WHITE, (0.1, 0.1, 0.1), BLACK, (0.2, 0.2, 0.2), (0.15, 0.15, 0.15)),
                "navy": way((0.12, 0.17, 0.32), (0.85, 0.42, 0.10), WHITE, GUM, WHITE, thread=(0.1, 0.12, 0.2)),
                "white": way(WHITE, GREY, WHITE, GREY, WHITE, thread=(0.7, 0.7, 0.68))}),
    # A cap-toe derby: smooth leather, a thin welted leather sole with a stacked heel.
    "derby": (dict(LAST, throat=-0.05, sole_top=[(-0.252, 0.014), (-0.2, 0.012), (-0.1, 0.014), (-0.02, 0.026), (0.07, 0.030)],
                   ridge=[(-0.252, 0.034), (-0.215, 0.048), (-0.15, 0.062), (-0.10, 0.090), (-0.06, 0.120), (0.0, 0.132)],
                   collar_side=0.086, collar_back=0.104, welt=0.005, spring=0.006, arch=0.016, heel_breast=-0.012, ball=-0.14, step=0.0012,
                   tongue_rise=0.008, laces=(-0.128, -0.112, -0.096, -0.080),
                   toe_cap=True, foxing=False, perforations=False, grain=0.12, relief=2.0),
              {"brown": way(BROWN, (0.26, 0.14, 0.08), DARK, (0.2, 0.14, 0.1), DARK, (0.6, 0.48, 0.36), (0.2, 0.11, 0.06)),
               "black": way(BLACK, (0.04, 0.04, 0.045), (0.03, 0.03, 0.03), (0.08, 0.07, 0.07), BLACK, (0.3, 0.26, 0.22), (0.02, 0.02, 0.02)),
               "tan": way(TAN, (0.47, 0.31, 0.17), DARK, (0.2, 0.14, 0.1), (0.35, 0.22, 0.12), (0.7, 0.58, 0.44), (0.36, 0.24, 0.13)),
               "oxblood": way(OXBLOOD, (0.24, 0.05, 0.05), (0.07, 0.04, 0.04), (0.1, 0.07, 0.06), (0.12, 0.04, 0.04), (0.55, 0.42, 0.34), (0.16, 0.03, 0.03))}),
}

FEET = {"male": {"form": [("MaleNakedFeet", 0)], "tag": "m"}, "female": {"form": [("FemNakedFeet", 0)], "tag": "f"}}
SKIN_FROM = {"court": "Sneakers03", "runner": "Sneakers03", "derby": "Shoes"}


# ================================================================ the second round: boot, loafer, flats, heels
#
# These are built from the same pieces (Part, the lasted upper, the sole unit) plus what the first three did not
# need: sheets that find their own facing, tubes (laces, piping, bindings), a painter with named regions, and for
# the women's flats and heels an upper that follows the measured foot instead of a last.


def unit(v):
    v = np.asarray(v, dtype=float)
    return v / max(np.linalg.norm(v), 1e-12)


def sheet(part, points, uvs, inside):
    """A grid facing away from `inside` (a point the surface encloses or turns its back to)."""
    points = np.asarray(points, dtype=float)
    across = points[:-1, 1:] - points[:-1, :-1]
    along = points[1:, :-1] - points[:-1, :-1]
    raw = np.cross(across, along)                    # the facing of Part.grid's unflipped triangles is against this
    centre = (points[:-1, :-1] + points[1:, 1:]) / 2 - np.asarray(inside)
    part.grid(points, np.asarray(uvs, dtype=float), flip=float((raw * centre).sum()) > 0)


def rect_uv(rows, columns, rect):
    """UVs of a rows x columns grid over rect = (u0, v0, u1, v1): columns along u, rows along v."""
    u = np.linspace(rect[0], rect[2], columns)
    v = np.linspace(rect[1], rect[3], rows)
    return np.dstack(np.meshgrid(u, v))


def tube(part, path, radius, rect, sides=4, squash=1.0, hint=(0.0, 0.0, 1.0), cap=False):
    """A tube along a path (n, 3): `sides` round, flattened by `squash` across the hint direction. rect: its texture."""
    path = np.asarray(path, dtype=float)
    radius = np.broadcast_to(np.asarray(radius, dtype=float), (len(path),))
    tangent = np.gradient(path, axis=0)
    tangent /= np.maximum(np.linalg.norm(tangent, axis=1, keepdims=True), 1e-12)
    normal = np.asarray(hint, dtype=float)
    rings = []
    for i in range(len(path)):
        normal = normal - tangent[i] * (normal @ tangent[i])
        if np.linalg.norm(normal) < 1e-6:
            normal = np.cross(tangent[i], [1.0, 0.0, 0.0])
        normal = unit(normal)
        binormal = np.cross(normal, tangent[i])
        angle = np.linspace(0, 2 * np.pi, sides, endpoint=False) + np.pi / sides
        rings.append(path[i] + radius[i] * (np.cos(angle)[:, None] * normal * squash + np.sin(angle)[:, None] * binormal))
    rings = closed_ring(np.array(rings))
    part.grid(rings, rect_uv(len(path), sides + 1, rect))
    if cap:
        for end, flip in ((0, True), (-1, False)):
            fan = np.array([np.repeat(path[end][None], sides + 1, axis=0), rings[end]])
            part.grid(fan, rect_uv(2, sides + 1, rect), flip=flip)


def disc(part, centre, normal, radius, rect, sides=6, rise=0.0008, across=(1.0, 0.0, 0.0)):
    """A small raised button (an eyelet, a stud): its texture is a square, the rim on the square's inscribed circle."""
    normal = unit(normal)
    e1 = unit(np.asarray(across, dtype=float) - normal * (np.asarray(across, dtype=float) @ normal))
    e2 = np.cross(normal, e1)
    angle = np.linspace(0, 2 * np.pi, sides + 1)
    rim = np.asarray(centre) + radius * (np.cos(angle)[:, None] * e1 + np.sin(angle)[:, None] * e2)
    top = np.repeat((np.asarray(centre) + normal * rise)[None], sides + 1, axis=0)
    mid_u, mid_v, half_u, half_v = (rect[0] + rect[2]) / 2, (rect[1] + rect[3]) / 2, (rect[2] - rect[0]) / 2, (rect[3] - rect[1]) / 2
    uv = np.array([np.tile([mid_u, mid_v], (sides + 1, 1)), np.stack([mid_u + half_u * np.cos(angle), mid_v + half_v * np.sin(angle)], axis=1)])
    part.grid(np.array([top, rim]), uv)


def bezier(a, control, b, count):
    t = np.linspace(0, 1, count)[:, None]
    return (1 - t) ** 2 * np.asarray(a) + 2 * t * (1 - t) * np.asarray(control) + t ** 2 * np.asarray(b)


def outline(spec):
    """The stations round the last: angle, where along the foot, half width, 0..1 from heel to toe, the side."""
    toe, heel = spec["toe"], spec["heel"]
    phi = np.linspace(0, 2 * np.pi, AROUND, endpoint=False)
    middle, half = (heel + toe) / 2, (heel - toe) / 2
    y = middle + half * np.cos(phi)
    side = np.sign(np.sin(phi))
    x = curve(spec["width"])(y) * side * np.abs(np.sin(phi)) ** spec.get("outline_power", 0.75)
    q = 1 - np.abs(phi / np.pi - 1)
    return phi, y, x, q, side, middle, half


def sole_unit(spec, phi, y, x, q, base_z, middle, wall_v=(0.215, 0.335), levels=None):
    """Welt, a midsole wall in two steps with a groove between them, the ground edge and the tread plan (as build())."""
    toe, heel = spec["toe"], spec["heel"]
    turn = phi / (2 * np.pi)
    sole = Part()
    welt = spec["welt"]
    spring = spec["spring"] * np.clip((q - 0.7) / 0.3, 0, 1) ** 2
    waist = np.clip((spec.get("heel_breast", 0.0) - y) / 0.012, 0, 1) * np.clip((y - spec.get("ball", -0.13)) / 0.03, 0, 1)
    spring = spring + spec.get("arch", 0.0) * waist
    out = np.stack([x / np.maximum(np.abs(x), 0.012), -np.cos(phi)], axis=1)
    out /= np.maximum(np.linalg.norm(out, axis=1, keepdims=True), 1e-6)

    def ring(push, level):
        return np.stack([CX + x + out[:, 0] * push, y + out[:, 1] * push, spring + (base_z - spring) * level], axis=1)
    step = spec.get("step", 0.0025)
    wall = np.array([ring(-0.004, 1.0) + [0, 0, 0.001], ring(welt, 1.0), ring(welt, 0.62), ring(welt - step, 0.56), ring(welt - step, 0.50),
                     ring(welt + step * 0.6, 0.44), ring(welt + step * 0.6, 0.10), ring(welt - 0.002, 0.0)])
    span = wall_v[1] - wall_v[0]
    levels = wall_v[0] + span * (np.array([0.335, 0.32, 0.29, 0.283, 0.277, 0.27, 0.23, 0.215]) - 0.215) / 0.12
    wall_uv = np.dstack([np.tile(turn[None, :], (len(wall), 1)), np.tile(levels[:, None], (1, AROUND))])
    sole.grid(closed_ring(wall), np.concatenate([wall_uv, np.dstack([np.ones((len(wall), 1)), wall_uv[:, :1, 1]])], axis=1), flip=True)
    ground = wall[-1]
    tread = ground.copy()
    tread[:, 0] = CX + (ground[:, 0] - CX) * 0.55
    tread[:, 1] = middle + (ground[:, 1] - middle) * 0.9
    spine = np.stack([np.full(AROUND, CX), tread[:, 1], spring], axis=1)
    under = closed_ring(np.array([ground, tread, spine]))
    plan = np.stack([0.02 + (under[..., 0] - CX + 0.07) / 0.14 * 0.28, 0.01 + (heel + 0.01 - under[..., 1]) / (heel - toe + 0.02) * 0.19], axis=-1)
    sole.grid(under, plan, flip=True)
    return sole


# Where things lie in a boot's texture (u0, v0, u1, v1).
BOOT_UV = {"upper": (0.0, 0.60, 1.0, 0.98), "shaft": (0.02, 0.40, 0.98, 0.58), "collar": (0.02, 0.345, 0.98, 0.385),
           "tongue": (0.35, 0.02, 0.51, 0.20), "lace": (0.54, 0.02, 0.75, 0.20), "eyelet": (0.78, 0.02, 0.86, 0.10),
           "lining": (0.88, 0.02, 0.98, 0.20)}


def build_boot(spec):
    """A laced boot: the lasted upper ends in a keyhole (the lacing slot up the instep and a ring round the ankle),
    the shaft rises from the ring with the lacing gap carried up its front, a padded collar rolls in over the top,
    a tongue lies behind the gap and the laces cross it from eyelet to eyelet."""
    phi, y, x, q, side, middle, half = outline(spec)
    ridge, sole_top = curve(spec["ridge"]), curve(spec["sole_top"])
    station = np.minimum(np.arange(AROUND), AROUND - np.arange(AROUND))       # 0 at the back of the heel, 22 at the toe
    kt, ks, g = spec["throat_column"], spec["slot_column"], spec["gap"] / 2
    q_throat = kt / (AROUND / 2)
    closed = station >= kt
    in_ring = station <= ks
    lean = spec["leg_shift"] * np.clip((kt - station) / (kt - ks), 0, 1)       # the leg stands a little inside the foot's centre line
    ring_z = float(ridge(y[ks]))
    shaft_rows = spec["shaft_rows"]

    def ring_frame(t):
        rx = spec["ring_x"][0] + (spec["ring_x"][1] - spec["ring_x"][0]) * t
        back = spec["ring_back"][0] + (spec["ring_back"][1] - spec["ring_back"][0]) * t
        cy = spec["ring_y"][0] + (spec["ring_y"][1] - spec["ring_y"][0]) * t
        front_y = y[ks] + (spec["top_front"] - y[ks]) * t
        theta1 = np.pi - math.asin(g / rx)
        return rx, back, cy, (cy - front_y) / -math.cos(theta1), theta1

    def top_line(theta):
        return spec["top"] + spec["top_rise"] * np.maximum(-np.cos(theta), 0) ** 2

    def ring_point(t, theta, sign, lateral=None):
        """A point of the shaft: t up it (0 the ankle ring, 1 the foot of the collar), theta round it from the back."""
        rx, back, cy, front, theta1 = ring_frame(t)
        if lateral is not None:
            theta = np.pi - math.asin(lateral / rx)
        theta = np.asarray(theta, dtype=float)
        z = ring_z + (top_line(theta) - spec["collar_height"] - ring_z) * t
        return np.stack([CX + spec["leg_shift"] + sign * rx * np.sin(theta), cy + np.where(np.cos(theta) > 0, back, front) * np.cos(theta), z + 0 * theta], axis=-1)

    # ---- the lasted upper, to the keyhole
    plateau = g + spec.get("plateau", 0.014) * np.clip((q - q_throat) / (1 - q_throat), 0, 1) ** 1.5
    target = np.zeros((AROUND, 3))
    target[:, 0] = np.where(closed, CX + side * np.minimum(np.abs(x) * 0.9, plateau), CX + lean + side * g)
    target[:, 1] = y
    target[:, 2] = ridge(y)
    theta1 = ring_frame(0)[4]
    for k in np.where(in_ring)[0]:
        target[k] = ring_point(0.0, station[k] / ks * theta1, side[k] if side[k] != 0 else 1.0)
    # The toe box: its top comes back along the foot, evenly, so the top line of the toe is round in plan.
    target[:, 1] += closed * spec["toe_back"] * np.clip((spec["toe_top"] - y) / (spec["toe_top"] - spec["toe"]), 0, 1) * (spec["toe_top"] - y)
    base_z = sole_top(y)
    foot = np.stack([CX + x, y, base_z], axis=1)
    h3 = np.linspace(0, 1, UP)[:, None, None]
    h = h3[..., 0]
    # The vamp is a rounded box; the quarters lean in to the ankle from further down.
    # It stands up from the welt, leans in over the height of the ankle and is upright again where the shaft begins.
    quarter = np.clip((kt - station) / (kt - ks), 0, 1)[None, :]
    rise = (1 - quarter) * (1 - (1 - h) ** 1.7) + quarter * (1 - (1 - h) ** 1.25)
    lean_in = rise ** spec["quarter_roundness"]
    lean_in = lean_in * lean_in * (3 - 2 * lean_in) * 0.6 + lean_in * 0.4
    inward = (1 - quarter) * h ** spec["roundness"] + quarter * lean_in
    bulge = spec["bulge"] * np.sin(np.pi * np.minimum(h * 1.6, 1.0)) * (0.4 + 0.6 * np.sin(np.pi * np.clip(q, 0.02, 0.98)))[None, :]
    upper = foot[None] + (target - foot)[None] * inward[..., None]
    upper[..., 0] += np.sign(x)[None, :] * bulge * (1 - quarter * rise)
    upper[..., 2] = base_z[None, :] + (target[:, 2] - base_z)[None, :] * rise

    box = BOOT_UV["upper"]
    turn = phi / (2 * np.pi)
    shell = Part()
    uv = np.dstack([np.tile(turn[None, :], (UP, 1)), box[1] + (box[3] - box[1]) * np.tile(h, (1, AROUND))])
    shell.grid(closed_ring(upper), np.concatenate([uv, np.dstack([np.ones((UP, 1)), uv[:, :1, 1]])], axis=1))

    rim = upper[-1]
    left = np.arange(kt, AROUND // 2 + 1)
    right = (AROUND - left) % AROUND
    crown = (rim[left] + rim[right]) / 2
    crown[:, 2] += np.minimum(spec.get("crown", 0.0015), np.abs(rim[left, 0] - rim[right, 0]) * 0.12)
    # In the same part as the sides, so the top of the vamp is round and not a strip; each half in its own side's texture.
    inset = np.array([0.0, (box[3] - box[1]) * 0.03])
    shell.grid(np.array([rim[left], crown]), np.array([uv[-1, left] - inset, uv[-1, left] - inset * 0.5]))
    shell.grid(np.array([crown, rim[right]]), np.array([uv[-1, right] - inset * 0.5, uv[-1, right] - inset]))

    # ---- the shaft: the same stations round the ankle ring, carried up; in the same part, so it runs on smoothly
    order = np.concatenate([np.arange(AROUND - ks, AROUND), np.arange(0, ks + 1)])       # from the right facing round the back to the left
    beta = np.where(order > AROUND // 2, -(AROUND - order), order) / ks                  # -1 .. 1
    rows = []
    for r in range(shaft_rows + 1):
        t = r / shaft_rows
        theta = np.abs(beta) * ring_frame(t)[4]
        points = ring_point(t, theta, np.where(beta < 0, -1.0, 1.0))
        rows.append(points)
    shaft = np.array(rows)
    shaft[0] = rim[order]
    box = BOOT_UV["shaft"]
    shaft_uv = rect_uv(shaft_rows + 1, len(order), box)
    shell.grid(shaft, shaft_uv)
    axis = np.array([CX + spec["leg_shift"], spec["ring_y"][1], 0.0])

    # The facing edges turned in, so the lacing gap has an edge with thickness: down the slot and up the shaft.
    for sign in (-1, 1):
        slot_columns = [k if sign > 0 else (AROUND - k) % AROUND for k in range(kt, ks, -1)]
        edge = np.concatenate([rim[slot_columns], shaft[:, -1 if sign > 0 else 0]])
        inward = np.concatenate([np.tile([0.0, 0.0, -1.0], (len(slot_columns), 1)), unit_rows(axis - shaft[:, -1 if sign > 0 else 0], flat=True)])
        turned = edge + inward * 0.005 + np.array([-sign * 0.001, 0, 0])
        strip_uv = np.dstack([np.tile(np.array([box[0], box[0] - 0.012])[:, None], (1, len(edge))), np.tile(np.linspace(box[1], box[3], len(edge))[None, :], (2, 1))])
        sheet(shell, np.array([edge, turned]), strip_uv, edge.mean(axis=0) + np.array([sign * 0.02, 0.01, -0.01]))

    # ---- the padded collar: a roll standing proud of the shaft, in over its top and down inside
    collar = Part()
    base = shaft[-1]
    outward = unit_rows(base - axis, flat=True)
    profile = spec["collar_profile"]
    roll = np.array([base + outward * out + np.array([0, 0, up * spec["collar_height"]]) for out, up in profile])
    sheet(collar, roll, rect_uv(len(profile), len(order), BOOT_UV["collar"]), base.mean(axis=0) + np.array([0, 0, spec["collar_height"] * 0.4]))
    for end in (0, -1):
        ring_end = roll[:, end]
        hub = np.repeat(ring_end.mean(axis=0)[None], len(profile), axis=0)
        for flip in (False, True):
            collar.grid(np.array([hub, ring_end]), rect_uv(2, len(profile), BOOT_UV["collar"]), flip=flip)

    # ---- the line the lacing follows: up the slot, then up the front of the shaft
    centre = [np.array([CX + lean[k], y[k], ridge(y[k])]) for k in range(kt, ks - 1, -1)]
    where = [("slot", float(k)) for k in range(kt, ks - 1, -1)]
    for r in range(1, shaft_rows + 1):
        t = r / shaft_rows
        point = (shaft[r, 0] + shaft[r, -1]) / 2
        centre.append(point)
        where.append(("shaft", t))
    centre = np.array(centre)
    run = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(centre, axis=0), axis=1))])

    def facing_normal(i):
        tangent = unit(centre[min(i + 1, len(centre) - 1)] - centre[max(i - 1, 0)])
        return unit([0.0, -tangent[2], tangent[1]])                      # square to the line, up and forward

    # ---- the tongue: behind the gap, from under the vamp to above the collar, curved round the instep
    tongue = Part()
    nodes, normals = [], []
    first = facing_normal(0)
    nodes.append(centre[0] + np.array([0, -0.014, -0.004]) - first * 0.004)
    normals.append(first)
    for i in range(len(centre)):
        n = facing_normal(i)
        nodes.append(centre[i] - n * 0.004)
        normals.append(n)
    last = normals[-1]
    crest = centre[-1] + np.array([0, 0, spec["collar_height"] + spec["tongue_above"]]) - last * 0.001
    nodes += [centre[-1] + np.array([0, 0, spec["collar_height"] * 0.6]) - last * 0.004, crest, crest - last * 0.006 + np.array([0, 0, 0.002]),
              crest - last * 0.011 + np.array([0, 0, -0.006]), crest - last * 0.011 + np.array([0, 0, -0.03])]
    normals += [last] * 5
    across = np.linspace(-1, 1, 5)
    grid = np.zeros((len(nodes), len(across), 3))
    for i, (node, n) in enumerate(zip(nodes, normals)):
        widen = spec["tongue_width"] * (0.8 + 0.2 * min(i / 3.0, 1.0))
        grid[i] = node[None, :] + np.outer(across * widen, [1.0, 0, 0]) - np.outer(spec["tongue_curve"] * across ** 2, n)
    sheet(tongue, grid[:-3], rect_uv(len(nodes) - 3, len(across), BOOT_UV["tongue"]), axis + np.array([0, 0.02, 0.1]))
    sheet(tongue, grid[-4:], rect_uv(4, len(across), BOOT_UV["tongue"]), crest - last * 0.005 + np.array([0, 0, -0.02]))

    # ---- eyelets and laces
    def on_surface(i_float, sign, lateral):
        """A point of the upper `lateral` from the lacing line, at a place given as a (fractional) node of that line."""
        low = int(min(math.floor(i_float), len(where) - 2))
        f = i_float - low
        found = []
        for node in (low, low + 1):
            kind, value = where[node]
            if kind == "shaft":
                found.append(ring_point(value, None, sign, lateral=lateral))
                continue
            k = int(round(value))
            if k == ks:
                found.append(ring_point(0.0, None, sign, lateral=lateral))
                continue
            column = upper[:, k if sign > 0 else (AROUND - k) % AROUND]
            off = np.abs(column[:, 0] - (CX + lean[k]))
            row = int(np.argmax(off < lateral))                               # the first row inside the wanted offset
            a, b = column[row - 1], column[row]
            found.append(a + (b - a) * (off[row - 1] - lateral) / max(off[row - 1] - off[row], 1e-9))
        return found[0] * (1 - f) + found[1] * f

    limit = run[-1] - 0.004
    count = spec["eyelets"]
    places = np.interp(np.linspace(spec["first_eyelet"], limit, count), run, np.arange(len(run)))
    eyes = {}
    hardware, laces = Part(), Part()
    for sign in (-1, 1):
        for i, place in enumerate(places):
            p = on_surface(place, sign, g + spec["eyelet_inset"])
            along = on_surface(min(place + 0.2, len(where) - 1), sign, g + spec["eyelet_inset"]) - on_surface(max(place - 0.2, 0), sign, g + spec["eyelet_inset"])
            outwards = on_surface(place, sign, g + spec["eyelet_inset"] + 0.004) - on_surface(place, sign, g + spec["eyelet_inset"] - 0.004)
            n = unit(np.cross(along, outwards))
            if n @ (p - (axis + np.array([0, 0, p[2] - 0.04]))) < 0:
                n = -n
            eyes[(sign, i)] = (p, n)
            disc(hardware, p + n * 0.0004, n, spec["eyelet_radius"], BOOT_UV["eyelet"], sides=6, rise=0.0012, across=along)
    lace_box = BOOT_UV["lace"]

    def strand(a, na, b, nb, k, lift):
        mid = (a + b) / 2 + unit(na + nb) * lift
        path = np.concatenate([[a - na * 0.001], bezier(a + na * 0.0022, mid, b + nb * 0.0022, 4), [b - nb * 0.001]])
        rect = (lace_box[0] + 0.05 * (k % 4), lace_box[1], lace_box[0] + 0.05 * (k % 4) + 0.04, lace_box[3])
        tube(laces, path, spec["lace_radius"], rect, sides=4, hint=unit(na + nb))
    strand(*eyes[(-1, 0)], *eyes[(1, 0)], 0, spec["lace_lift"] * 0.6)
    for i in range(count - 1):
        strand(*eyes[(-1, i)], *eyes[(1, i + 1)], 2 * i + 1, spec["lace_lift"])
        strand(*eyes[(1, i)], *eyes[(-1, i + 1)], 2 * i + 2, spec["lace_lift"] * 1.35)       # the one on top
    # The bow: a knot between the top eyelets, two loops and two ends hanging down the front.
    (pl, nl), (pr, nr) = eyes[(-1, count - 1)], eyes[(1, count - 1)]
    front = unit(nl + nr)
    knot = (pl + pr) / 2 + front * (spec["lace_lift"] * 0.55 + 0.002)
    strand(pl, nl, pr, nr, 3, spec["lace_lift"] * 0.55)
    tube(laces, [knot + np.array([-0.004, 0, 0]), knot + np.array([0.004, 0, 0])], spec["lace_radius"] * 1.9, (lace_box[0], lace_box[1], lace_box[0] + 0.04, lace_box[3]), sides=5, hint=front, cap=True)
    for sign in (-1, 1):
        t = np.linspace(0, 2 * np.pi, 7)[:, None]
        loop = knot + (np.array([sign * 0.019, 0, -0.006]) * (1 - np.cos(t)) / 2 + np.array([0, 0, 0.007]) * np.sin(t) + front * 0.004 * np.sin(t / 2))
        tube(laces, loop, spec["lace_radius"], (lace_box[0] + 0.1, lace_box[1], lace_box[0] + 0.14, lace_box[3]), sides=4, hint=front)
        end = knot + np.array([[sign * 0.002, 0, 0], [sign * 0.008, 0, -0.010], [sign * 0.011, 0, -0.024], [sign * 0.012, 0, -0.036]]) + front * np.array([0.0, 0.002, 0.0015, 0.0])[:, None]
        tube(laces, end, spec["lace_radius"], (lace_box[0] + 0.15, lace_box[1], lace_box[0] + 0.19, lace_box[3]), sides=4, hint=front, cap=True)

    sole = sole_unit(spec, phi, y, x, q, base_z, middle)
    return [shell, collar, tongue, hardware, laces, sole]


def unit_rows(vectors, flat=False):
    vectors = np.array(vectors, dtype=float)
    if flat:
        vectors[:, 2] = 0
    return vectors / np.maximum(np.linalg.norm(vectors, axis=1, keepdims=True), 1e-12)


# ---------------------------------------------------------------- painting with named regions


class Canvas:
    """The style's texture while it is painted: a height field (the normal map), and for the colour a material per
    pixel (a key of the colourway), a shade, and how much thread shows. Regions are the rectangles the parts' UVs use."""

    def __init__(self, seed=3):
        v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
        self.u, self.v = u, 1 - v
        rng = np.random.default_rng(seed)
        grain = rng.random((SIZE, SIZE))
        for _ in range(2):
            grain = (grain + np.roll(grain, 1, 0) + np.roll(grain, 1, 1) + np.roll(grain, (1, 1), (0, 1))) / 4
        self.grain = (grain - grain.mean()) / grain.std() * 0.12        # about -0.3 .. 0.3
        blotch = rng.random((SIZE // 32, SIZE // 32))
        from PIL import Image
        self.blotch = np.asarray(Image.fromarray((blotch * 255).astype(np.uint8)).resize((SIZE, SIZE), Image.BICUBIC), dtype=float) / 255 - 0.5
        self.height = np.zeros((SIZE, SIZE))
        self.keys = ["lining"]
        self.key = np.zeros((SIZE, SIZE), dtype=np.int64)
        self.shade = np.ones((SIZE, SIZE))
        self.thread = np.zeros((SIZE, SIZE))
        self.gloss = np.zeros((SIZE, SIZE))          # a light catch (metal, patent): added white

    def region(self, rect, pad=0.006):
        """(mask, across 0..1, up 0..1) of a rectangle; the mask reaches a little past it, so its edge does not bleed."""
        s = (self.u - rect[0]) / (rect[2] - rect[0])
        t = (self.v - rect[1]) / (rect[3] - rect[1])
        return (self.u >= rect[0] - pad) & (self.u <= rect[2] + pad) & (self.v >= rect[1] - pad) & (self.v <= rect[3] + pad), s, t

    def fill(self, mask, key, shade=None, height=None):
        if key not in self.keys:
            self.keys.append(key)
        self.key[mask] = self.keys.index(key)
        self.shade[mask] = 1.0 if shade is None else (shade[mask] if np.ndim(shade) else shade)
        self.height[mask] = 0.0 if height is None else (height[mask] if np.ndim(height) else height)
        self.thread[mask] = 0.0
        self.gloss[mask] = 0.0

    def panel(self, mask, field, key=None, raise_by=0.5, stitches=(0.012, 0.022), stitch_width=0.0016, shadow=0.006, dash=110.0):
        """A leather panel lying on what is there: where field > 0 (in the region's own units). Its edge is a step in
        the normal map with a shadow under it, and rows of stitching run inside the edge."""
        inside = np.clip(field / 0.004, 0, 1) * mask
        self.height += inside * raise_by
        self.shade *= 1 - 0.2 * np.exp(-(np.minimum(field, 0) / shadow) ** 2) * (field < 0) * mask
        if key is not None:
            if key not in self.keys:
                self.keys.append(key)
            self.key[(field > 0) & mask] = self.keys.index(key)
        for inset in stitches:
            self.stitch(mask, field - inset, stitch_width, dash)

    def stitch(self, mask, field, width=0.0016, dash=110.0):
        line = np.exp(-(field / width) ** 2) * (np.sin((self.u * 2 + self.v) * 2 * np.pi * dash) > -0.3) * mask
        self.thread = np.maximum(self.thread, line)
        self.height += line * 0.12 - np.exp(-(field / (width * 2.2)) ** 2) * mask * 0.10       # the thread sits in a pulled-in furrow

    def save(self, name, relief, colourways, extra=None):
        from PIL import Image
        folder = os.path.join(ba.BUILD, "textures")
        os.makedirs(folder, exist_ok=True)
        dx = (np.roll(self.height, -1, 1) - np.roll(self.height, 1, 1)) * relief
        dy = (np.roll(self.height, 1, 0) - np.roll(self.height, -1, 0)) * relief
        normal = np.dstack([-dx, -dy, np.ones_like(dx)])
        normal /= np.linalg.norm(normal, axis=2, keepdims=True)
        Image.fromarray(((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(folder, name + "_n.png"))
        textures = [{"name": name + "_n", "file": "textures/" + name + "_n.png", "type": "normal", "maxSize": SIZE}]
        for colour_name, c in colourways.items():
            table = np.array([c.get(k, c["upper"]) for k in self.keys], dtype=float)
            colour = table[self.key] * np.clip(self.shade, 0, 1.4)[..., None]
            thread = np.array(c.get("thread", c["upper"]), dtype=float)
            colour = colour * (1 - self.thread[..., None] * 0.85) + thread * self.thread[..., None] * 0.85
            colour = colour + self.gloss[..., None] * (1 - colour)
            if extra is not None:
                colour = extra(colour, c)
            rgba = np.dstack([np.clip(colour, 0, 1), np.ones((SIZE, SIZE))])
            image = Image.fromarray((rgba * 255).astype(np.uint8)).resize((SIZE // 2, SIZE // 2), Image.LANCZOS)
            image.save(os.path.join(folder, "%s_%s.png" % (name, colour_name)))
            textures.append({"name": "%s_%s" % (name, colour_name), "file": "textures/%s_%s.png" % (name, colour_name), "type": "color", "maxSize": SIZE // 2})
        return textures


def paint_sole(canvas, spec, wall_v=(0.215, 0.335)):
    """The sole's wall (welt on top, midsole, the outsole's edge with its lugs) and the tread plan."""
    u, v = canvas.u, canvas.v
    wall = (v >= wall_v[0] - 0.005) & (v < wall_v[1] + 0.005)
    band = (v - wall_v[0]) / (wall_v[1] - wall_v[0])
    teeth = (np.sin(u * 2 * np.pi * spec.get("lugs", 90)) > 0.2) * (band < 0.45) * (1.0 if spec.get("teeth") else 0.0)
    canvas.fill(wall, "sole", 0.86 + 0.14 * np.clip(band * 3, 0, 1) - 0.25 * teeth, -teeth * 0.7 + canvas.grain * 0.3)
    if spec.get("outsole_edge"):
        canvas.fill(wall & (band < 0.47), "outsole", 0.9 - 0.3 * teeth + 0 * band, -teeth * 0.7 + canvas.grain * 0.3)
    if spec.get("welt_stitch"):
        top = wall & (band > 0.85)
        canvas.fill(top, "welt", 1.0 + 0 * band, canvas.grain * 0.3)
        canvas.stitch(top, band - 0.93, 0.03, spec.get("welt_dash", 170.0))
    plan = (v < wall_v[0] - 0.005) & (u < 0.32)
    tread = (np.sin((u * 3 + v * 1.4) * 2 * np.pi * 26) > 0) & (np.sin((u * 3 - v * 1.4) * 2 * np.pi * 26) > -0.4)
    canvas.fill(plan, "outsole", 0.75 + 0.25 * tread, tread * 1.0)


def leather(canvas, mask, key, grain, wear=0.0):
    canvas.fill(mask, key, 1.0 + canvas.blotch * wear + canvas.grain * 0.15, canvas.grain * grain * 2.0)


def paint_boot(style, spec, colourways):
    c = Canvas()
    u, v = c.u, c.v
    half = AROUND / 2
    q_throat, q_slot = spec["throat_column"] / half, spec["slot_column"] / half

    # ---- the lasted upper: vamp with a toe cap; the quarters lie over it and carry the eyelets; counter and backstay
    m, s, up = c.region(BOOT_UV["upper"])
    q = 1 - np.abs(s * 2 - 1)
    leather(c, m, "upper", spec["grain"], spec.get("wear", 0.0))
    creases = sum(np.exp(-((q - centre - 0.006 * np.sin(up * 9 + centre * 60)) / 0.004) ** 2) for centre in (0.64, 0.685)) * np.clip((up - 0.45) / 0.3, 0, 1)
    c.height -= creases * m * 0.18
    c.shade *= 1 - 0.05 * creases * m
    front = q_throat + 0.035
    line = np.where(up < 0.84, q_slot - 0.02 + (front - q_slot + 0.02) * np.clip(up / 0.84, 0, 1) ** 1.6, front)
    c.panel(m, line - q, stitches=(0.010, 0.018))
    if spec.get("toe_cap"):
        c.panel(m, q - (0.80 - 0.05 * up), stitches=(0.010, 0.018))
    c.panel(m, np.minimum((0.15 + 0.05 * np.sin(np.clip(up, 0, 1) * np.pi)) - q, 0.62 - up) , key="accent" if spec.get("accent_counter") else None, stitches=(0.010,))
    c.panel(m, 0.022 - q, raise_by=0.4, stitches=(0.006,))
    # Burnished toward the toe, the heel and the welt, as a boot that has been polished and worn.
    c.shade *= 1 - m * spec.get("burnish", 0.0) * np.clip(np.maximum(np.maximum((q - 0.84) / 0.12 * np.clip(1.5 - 1.6 * up, 0, 1), (0.08 - q) / 0.08), 1 - up / 0.07), 0, 1)
    c.shade *= 1 - m * 0.25 * np.exp(-(up / 0.035) ** 2)

    # ---- the shaft: quarters all round; the eyelet rows are stitched down, a backstay runs up the back
    m, s, t = c.region((BOOT_UV["shaft"][0] - 0.015, BOOT_UV["shaft"][1], BOOT_UV["shaft"][2] + 0.015, BOOT_UV["shaft"][3]))
    leather(c, m, "upper", spec["grain"], spec.get("wear", 0.0))
    m, s, t = c.region(BOOT_UV["shaft"])
    edge = np.minimum(s, 1 - s)
    c.stitch(m, edge - 0.062, 0.0022, 60.0)
    c.stitch(m, edge - 0.075, 0.0022, 60.0)
    c.panel(m, 0.035 - np.abs(s - 0.5), raise_by=0.4, stitches=(0.007,), stitch_width=0.0022, dash=60.0)
    c.stitch(m, t - 0.93, 0.02, 60.0)

    # ---- the collar: padded, in two rolls
    m, s, t = c.region(BOOT_UV["collar"])
    rolls = np.abs(np.sin(t * np.pi * 1.0 / 0.62)) * (t < 0.62)
    c.fill(m, "accent", 0.85 + 0.2 * rolls + canvas_grain(c, 0.1), rolls * 1.4 + c.grain * spec["grain"])

    # ---- tongue, laces, eyelets, lining
    m, s, t = c.region(BOOT_UV["tongue"])
    leather(c, m, "tongue", spec["grain"])
    c.stitch(m, np.minimum(s, 1 - s) - 0.12, 0.02, 60.0)
    m, s, t = c.region(BOOT_UV["lace"])
    weave = 0.5 + 0.5 * np.sin((u * 9 + v) * 2 * np.pi * 60) * np.sin((u * 9 - v) * 2 * np.pi * 60)
    c.fill(m, "lace", 0.8 + 0.2 * weave, weave * 0.5)
    m, s, t = c.region(BOOT_UV["eyelet"])
    r = np.hypot(s - 0.5, t - 0.5) * 2
    c.fill(m, "metal", np.where(r < 0.42, 0.12, 0.75 + 0.5 * (s - t)), np.where(r < 0.42, -1.0, 0.6 * np.clip((1 - r) / 0.3, 0, 1)))
    c.gloss[m & (r > 0.5) & (np.abs(s - t + 0.25) < 0.12)] = 0.35
    m, s, t = c.region(BOOT_UV["lining"])
    c.fill(m, "lining", 0.9 + canvas_grain(c, 0.1), c.grain * 0.3)
    paint_sole(c, spec)
    return c.save("feet_" + style, spec["relief"], colourways)


def canvas_grain(canvas, amount):
    return canvas.grain * amount / 0.3


class ShoeForm:
    """What a shoe takes its skin weights and Fat/Slim/Musc from: the closest point of the game's own shoe (or foot),
    under the names that sex's meshes give the three builds."""

    def __init__(self, parts):
        from garment_lib import Surface
        self.surface = Surface(parts)
        own = ba.load_reference(parts[0][0])["shape_order"]
        lower = {n.lower(): n for n in own}
        self.names = [(given, lower.get(given.lower(), given)) for given in self.surface.shape_order]
        self.shape_order = [mine for _, mine in self.names]

    def sample(self, points):
        weights, shapes = self.surface.sample(np.asarray(points, dtype=float))
        return weights, {mine: shapes[given] for given, mine in self.names}


def way2(upper, sole, outsole, lace=None, accent=None, lining=(0.62, 0.5, 0.38), thread=None, welt=None, metal=(0.62, 0.55, 0.38), **more):
    entry = {"upper": upper, "accent": accent or tuple(0.55 * np.array(upper)), "sole": sole, "outsole": outsole, "lace": lace or upper,
             "lining": lining, "thread": thread or tuple(0.7 * np.array(upper)), "welt": welt or sole, "metal": metal, "tongue": tuple(0.92 * np.array(upper))}
    entry.update(more)
    return entry


# ---------------------------------------------------------------- loafer

LOAFER_UV = {"upper": (0.0, 0.45, 1.0, 0.98), "lining": (0.02, 0.345, 0.32, 0.44), "apron": (0.35, 0.02, 0.55, 0.20),
             "strap": (0.58, 0.02, 0.98, 0.09), "seam": (0.58, 0.11, 0.98, 0.14), "binding": (0.58, 0.16, 0.98, 0.19)}


def build_loafer(spec):
    """A penny loafer: the lasted upper ends in front on the edge of an apron (the moccasin's plug), sewn in with a
    raised seam and running on behind the throat as a short tongue; a saddle strap lies across the vamp; behind the
    throat the top line is a low D round the ankle with the lining rolled in; a thin welted sole with a stacked heel."""
    phi, y, x, q, side, middle, half = outline(spec)
    ridge, sole_top, apron = curve(spec["ridge"]), curve(spec["sole_top"]), curve(spec["apron"])
    station = np.minimum(np.arange(AROUND), AROUND - np.arange(AROUND))
    kt = spec["throat_column"]
    throat, half_stations = float(y[kt]), AROUND // 2
    closed = station >= kt
    ankle_y, ankle_x, ankle_back = spec["ankle_y"], spec["ankle_x"], spec["ankle_back"]
    beta = np.clip(station / kt, 0, 1)
    alpha = np.pi * beta
    drop = spec["apron_drop"]
    edge_throat = float(ridge(throat)) - drop
    collar = spec["collar_side"] + (spec["collar_back"] - spec["collar_side"]) * np.clip(1 - beta / 0.42, 0, 1) ** 2
    rise = np.clip((beta - 0.55) / 0.45, 0, 1)
    collar = collar + (edge_throat - collar) * rise * rise * (3 - 2 * rise)
    plateau = np.minimum(np.abs(x) * 0.9, apron(y))
    plateau = np.where(plateau > 0.004, plateau, 0.0)          # in front of the apron's end the two sides meet on the centre line
    wide = np.sin(alpha) ** 0.8
    open_x = np.where(beta <= 0.5, ankle_x * wide, plateau[kt] + (ankle_x - plateau[kt]) * wide)
    target = np.zeros((AROUND, 3))
    target[:, 0] = CX + side * np.where(closed, plateau, open_x)
    target[:, 1] = np.where(closed, y, ankle_y + np.where(np.cos(alpha) > 0, ankle_back, ankle_y - throat) * np.cos(alpha))
    target[:, 2] = np.where(closed, ridge(y) - drop * np.clip(plateau / 0.012, 0, 1), collar)
    pull = closed * spec["toe_back"] * np.clip((spec["toe_top"] - y) / (spec["toe_top"] - spec["toe"]), 0, 1) * (spec["toe_top"] - y)
    target[:, 1] += pull
    base_z = sole_top(y)
    foot = np.stack([CX + x, y, base_z], axis=1)
    h3 = np.linspace(0, 1, UP)[:, None, None]
    h = h3[..., 0]
    openf = (~closed).astype(float)
    bulge = spec["bulge"] * np.sin(np.pi * np.minimum(h * 1.6, 1.0)) * (0.4 + 0.6 * np.sin(np.pi * np.clip(q, 0.02, 0.98)))[None, :]
    upper = foot[None] + (target - foot)[None] * h3 ** spec["roundness"]
    upper[..., 0] += np.sign(x)[None, :] * (bulge + spec["standoff"] * openf[None, :] * np.sin(np.pi * h ** 1.5) ** 0.7)
    upper[..., 2] = base_z[None, :] + (target[:, 2] - base_z)[None, :] * (1 - (1 - h) ** 1.7)

    box = LOAFER_UV["upper"]
    turn = phi / (2 * np.pi)
    shell = Part()
    uv = np.dstack([np.tile(turn[None, :], (UP, 1)), box[1] + (box[3] - box[1]) * np.tile(h, (1, AROUND))])
    shell.grid(closed_ring(upper), np.concatenate([uv, np.dstack([np.ones((UP, 1)), uv[:, :1, 1]])], axis=1))
    rim = upper[-1]

    # ---- the apron and, behind the throat, the tongue it runs on into
    front = [k for k in range(kt, half_stations + 1) if plateau[k] > 0.004]
    plug = Part()
    across = np.array([-1.0, -1.0, -0.6, 0.0, 0.6, 1.0, 1.0])
    rows = []
    for k in front:
        a, b = rim[(AROUND - k) % AROUND], rim[k]
        row = a[None, :] + (b - a)[None, :] * ((across + 1) / 2)[:, None]
        row[:, 2] += (drop * np.clip(plateau[k] / 0.012, 0, 1) + 0.0006) * (1 - across ** 2)
        rows.append(row)
    nose = rows[-1].copy()                                    # the apron's round end
    nose[:] = rim[front[-1] + 1]
    slope = (ridge(throat + 0.01) - ridge(throat)) / 0.01
    tail = []
    for d, widen, sink in ((0.005, 1.0, 0.0), (0.010, 0.96, 0.0), (0.0135, 0.86, 0.0), (0.0150, 0.72, 0.0012), (0.014, 0.70, 0.0045), (0.008, 0.8, 0.0055)):
        row = rows[0].copy()
        row[:, 0] = CX + (row[:, 0] - CX) * widen
        row[:, 1] += d
        row[:, 2] += slope * d * spec["tongue_slope"] + spec["tongue_lift"] * min(d / 0.01, 1.0) - sink
        row[[0, -1], 2] -= 0.006 * min(d / 0.005, 1.0)        # its edges have thickness
        tail.append(row)
    grid = np.array(tail[::-1] + rows + [nose])
    rect = LOAFER_UV["apron"]
    plug_uv = np.stack([rect[0] + (rect[2] - rect[0]) * (0.5 + (grid[..., 0] - CX) / 0.07),
                        rect[1] + (rect[3] - rect[1]) * np.clip((throat + 0.03 - grid[..., 1]) / (throat + 0.03 - spec["toe"]), 0, 1)], axis=-1)
    sheet(plug, grid[2:], plug_uv[2:], np.array([CX, middle, 0.02]))
    sheet(plug, grid[:3], plug_uv[:3], np.array([CX, throat + 0.012, float(ridge(throat)) + 0.03]))     # the tongue's underside

    # ---- the seam that holds the apron: a raised, hand-sewn ridge round its edge
    seam = Part()
    path = np.concatenate([rim[[(AROUND - k) % AROUND for k in front]], rim[front[::-1]]]) + np.array([0, 0, 0.0008])
    smooth_path = path.copy()
    smooth_path[1:-1] = (path[:-2] + 2 * path[1:-1] + path[2:]) / 4
    tube(seam, smooth_path, spec["seam_radius"], LOAFER_UV["seam"], sides=4, cap=True)

    # ---- the saddle strap: a band over the vamp from welt to welt, proud of the upper
    strap = Part()
    apron_rows = {k: rows[i] for i, k in enumerate(front)}

    def over_the_vamp(kf):
        low = int(math.floor(kf))
        f = kf - low
        line = []
        for k in (low, low + 1):
            left_side = upper[1:, (AROUND - k) % AROUND]
            right_side = upper[1:, k]
            line.append(np.concatenate([left_side, apron_rows[k][2:-2], right_side[::-1]]))
        return line[0] * (1 - f) + line[1] * f
    a, b = spec["strap"]
    lanes = [(a + 0.1, 0.0002), (a, 0.0032), ((a + b) / 2, 0.0038), (b, 0.0032), (b - 0.1, 0.0002)]
    bands = []
    for kf, lift in lanes:
        line = over_the_vamp(kf)
        out = unit_rows((line - np.array([CX, 0.0, 0.03])) * np.array([1.0, 0.0, 1.0]))
        swell = 1.0 + spec["strap_swell"] * np.exp(-((np.abs(line[:, 0] - CX) - 0.034) / 0.012) ** 2)      # the roll where it is sewn to the apron's seam
        bands.append(line + out * (lift * swell)[:, None])
    bands = np.array(bands)                                   # (lanes, stations, 3)
    rect = LOAFER_UV["strap"]
    strap_uv = np.dstack(np.meshgrid(rect[1] + (rect[3] - rect[1]) * np.array([0.0, 0.1, 0.5, 0.9, 1.0]), np.linspace(rect[0], rect[2], bands.shape[1])))[..., ::-1]
    sheet(strap, np.swapaxes(bands, 0, 1), strap_uv,
          np.array([CX, (y[int(a)] + y[int(b)]) / 2, 0.03]))

    # ---- the collar's lining, rolled in and down round the ankle, and a bound top line
    lining = Part()
    centre = np.stack([np.full(AROUND, CX), np.full(AROUND, ankle_y), rim[:, 2]], axis=1)
    inwards = (centre - rim) / np.maximum(np.linalg.norm(centre - rim, axis=1, keepdims=True), 1e-6)
    order = np.concatenate([np.arange(AROUND - kt, AROUND), np.arange(0, kt + 1)])
    rolled = []
    for inset, down in ((0.0, 0.0), (0.0025, 0.0025), (0.0055, 0.0015), (0.007, -0.006), (0.007, -0.04)):
        row = rim + inwards * inset
        row[:, 2] += down
        rolled.append(row[order])
    lining.grid(np.array(rolled), rect_uv(5, len(order), LOAFER_UV["lining"]))

    sole = sole_unit(spec, phi, y, x, q, base_z, middle)
    return [shell, plug, seam, strap, lining, sole]


def paint_loafer(style, spec, colourways):
    c = Canvas(seed=5)
    u, v = c.u, c.v
    m, s, up = c.region(LOAFER_UV["upper"])
    q = 1 - np.abs(s * 2 - 1)
    leather(c, m, "upper", spec["grain"], spec.get("wear", 0.0))
    c.panel(m, np.minimum((0.15 + 0.04 * np.sin(np.clip(up, 0, 1) * np.pi)) - q, 0.80 - up), raise_by=0.35, stitches=(0.010,))
    c.panel(m, 0.018 - q, raise_by=0.3, stitches=(0.006,))
    q_throat = spec["throat_column"] / (AROUND / 2)
    c.stitch(m & (q < q_throat + 0.01), up - 0.93, 0.008, 120.0)                       # the top line is stitched down
    c.shade *= 1 - m * spec.get("burnish", 0.0) * np.clip(np.maximum(np.maximum((q - 0.84) / 0.12 * np.clip(1.5 - 1.6 * up, 0, 1), (0.08 - q) / 0.08), 1 - up / 0.07), 0, 1)
    c.shade *= 1 - m * 0.25 * np.exp(-(up / 0.035) ** 2)
    creases = sum(np.exp(-((q - centre - 0.005 * np.sin(up * 9 + centre * 60)) / 0.004) ** 2) for centre in (0.66, 0.70)) * np.clip((up - 0.3) / 0.3, 0, 1)
    c.height -= creases * m * 0.15

    m, s, t = c.region(LOAFER_UV["apron"])
    leather(c, m, "upper", spec["grain"], spec.get("wear", 0.0))
    c.shade *= 1 - m * 0.06 * np.exp(-((t - 0.62) / 0.02) ** 2)                      # a crease across the apron where the foot bends

    m, s, t = c.region(LOAFER_UV["strap"])
    leather(c, m, "strap", spec["grain"])
    edge = np.minimum(t, 1 - t)
    c.shade *= 1 - m * (0.07 + 0.45 * np.clip((0.12 - edge) / 0.06, 0, 1))          # its cut edges, darker
    c.stitch(m & (np.abs(s - 0.5) > 0.13), edge - 0.24, 0.035, 90.0)
    # The penny slot: a lens cut out of the strap, with the apron showing dark behind it.
    lens = 1 - ((s - 0.5) / 0.07) ** 2 - ((t - 0.5) / 0.13) ** 2 * (1 + 3 * ((s - 0.5) / 0.07) ** 2)
    slot = np.clip(lens / 0.25, 0, 1) * m
    c.height -= slot * 1.2
    c.shade *= 1 - 0.72 * slot
    c.height += np.exp(-(lens / 0.2) ** 2) * (lens < 0) * m * 0.3

    m, s, t = c.region(LOAFER_UV["seam"])
    sewn = 0.5 + 0.5 * np.sin(t * 2 * np.pi * 38)
    c.fill(m, "upper", 0.8 + 0.25 * sewn, sewn * 0.9)
    c.thread[m] = np.clip((sewn[m] - 0.72) / 0.2, 0, 1) * 0.5
    m, s, t = c.region(LOAFER_UV["lining"])
    c.fill(m, "lining", 0.88 + canvas_grain(c, 0.1), c.grain * 0.3)
    c.shade *= 1 - m * 0.35 * np.clip((0.55 - t) / 0.4, 0, 1)                        # darker down inside the shoe
    paint_sole(c, spec)
    return c.save("feet_" + style, spec["relief"], colourways)


LOAFER = dict(
    toe=-0.252, heel=0.070, toe_top=-0.19, toe_back=0.42,
    width=[(-0.252, 0.040), (-0.215, 0.054), (-0.14, 0.058), (-0.06, 0.052), (0.0, 0.050), (0.07, 0.043)],
    ridge=[(-0.252, 0.036), (-0.215, 0.050), (-0.15, 0.062), (-0.10, 0.075), (-0.08, 0.086), (-0.06, 0.097), (-0.045, 0.1055), (-0.03, 0.1175)],
    sole_top=[(-0.252, 0.014), (-0.2, 0.012), (-0.1, 0.014), (-0.02, 0.026), (0.07, 0.030)],
    apron=[(-0.252, 0.0), (-0.2335, 0.0), (-0.2248, 0.013), (-0.2148, 0.0195), (-0.20, 0.0235), (-0.15, 0.027), (-0.06, 0.026)],
    apron_drop=0.004, bulge=0.004, roundness=2.4, standoff=0.006,
    # The throat is where the bare skin of the game's low shoes begins: in front of it there is no foot to show.
    throat_column=9, ankle_y=0.014, ankle_x=0.037, ankle_back=0.041, collar_side=0.068, collar_back=0.094, outline_power=0.62,
    tongue_slope=1.0, tongue_lift=0.0, seam_radius=0.0021, strap=(11.7, 10.5), strap_swell=0.6,
    welt=0.005, spring=0.006, arch=0.014, heel_breast=-0.012, ball=-0.14, step=0.0012,
    welt_stitch=True, grain=0.07, relief=2.0, burnish=0.3, wear=0.12,
)


# ---------------------------------------------------------------- women's flats and heels: uppers that follow the foot
#
# A flat and a pump leave the top of the foot bare, so they cannot be built on a last a few millimetres outside the
# foot: their top line has to lie ON the skin that shows. The skin is the game's (its bare foot for the flat; for
# the heel the skin part of its own pumps, which is the foot already pitched for a high heel) and is only measured:
# where its surface is, and for the pumps where it ends, which is where a shoe has to begin.

COURT_UV = {"upper": (0.0, 0.50, 1.0, 0.98), "binding": (0.0, 0.44, 1.0, 0.48), "heel": (0.36, 0.02, 0.70, 0.20),
            "bow": (0.74, 0.02, 0.98, 0.10), "lining": (0.74, 0.12, 0.98, 0.20)}
ROWS = 7


class Foot:
    """The skin a shoe sits on, as something to measure."""

    def __init__(self, parts):
        from garment_lib import Surface
        self.surface = Surface(parts)
        self.tree = self.surface.bvh()
        self._slices = {}

    def top(self, x, y, start=0.12):
        """Height of the skin under a point, from above; None where there is none."""
        location = self.tree.ray_cast((float(x), float(y), start), (0.0, 0.0, -1.0))[0]
        return None if location is None else float(location[2])

    def side(self, origin, direction, z):
        """How far from origin (x, y), along direction, the outline of the skin lies at height z; None if nowhere."""
        from garment_lib import ray_radius
        key = round(float(z), 5)
        if key not in self._slices:
            self._slices[key] = self.surface.segments(2, float(z))[:, :, :2]
        segments = self._slices[key]
        if not len(segments):
            return None
        r = ray_radius(segments, np.asarray(origin, dtype=float), np.asarray([direction], dtype=float), far=True, limit=0.16)[0]
        return None if np.isnan(r) else float(r)

    def nearest(self, point):
        location, normal, _, _ = self.tree.find_nearest([float(c) for c in point])
        return np.array(location), np.array(normal)

    def opening(self):
        """Where the skin of the left foot ends below: the points of that edge in order, and at each the direction
        along the skin away from the edge and the skin's outward normal."""
        v, triangles = self.surface.vertices, self.surface.triangles
        edges = {}
        for tri in triangles:
            for a, b, c in ((0, 1, 2), (1, 2, 0), (2, 0, 1)):
                edges.setdefault((min(tri[a], tri[b]), max(tri[a], tri[b])), []).append(tri[c])
        border = {k: o[0] for k, o in edges.items() if len(o) == 1}
        link = {}
        inward = {}
        for (a, b), o in border.items():
            link.setdefault(a, []).append(b)
            link.setdefault(b, []).append(a)
            along = unit(v[b] - v[a])
            w = v[o] - (v[a] + v[b]) / 2
            w = unit(w - along * (w @ along))
            inward[a] = inward.get(a, 0) + w
            inward[b] = inward.get(b, 0) + w
        loops, seen = [], set()
        for start in link:
            if start in seen:
                continue
            loop, previous, current = [start], None, start
            seen.add(start)
            while True:
                options = [n for n in link[current] if n != previous and n not in seen]
                if not options:
                    break
                previous, current = current, options[0]
                loop.append(current)
                seen.add(current)
            loops.append(loop)
        loops = [l for l in loops if len(l) > 8 and v[l][:, 0].mean() > 0]
        loop = min(loops, key=lambda l: v[l][:, 2].mean())
        return v[loop], unit_rows([inward[i] for i in loop]), self.surface.normals[loop]


def thin_sole(spec, edge, ground, phi, cx, y, wall_v=(0.215, 0.335)):
    """A sole that is an edge and an underside: from the line the upper is lasted to (edge, one point a station)
    down to `ground` (a height per station)."""
    sole = Part()
    turn = phi / (2 * np.pi)
    centre = np.stack([cx, y], axis=1)
    out = unit_rows(np.concatenate([edge[:, :2] - centre, np.zeros((AROUND, 1))], axis=1) + np.stack([0 * y, -np.cos(phi) * 0.02, 0 * y], axis=1), flat=True)
    welt = spec["welt"]

    def ring(push, z):
        return np.stack([edge[:, 0] + out[:, 0] * push, edge[:, 1] + out[:, 1] * push, z], axis=1)
    top, bottom = edge[:, 2], np.minimum(ground, edge[:, 2] - 0.002)
    rand = bottom + (top - bottom) * spec.get("rand", 0.6)
    wall = np.array([ring(-0.003, top + 0.0008), ring(welt, top), ring(welt, rand), ring(welt * 0.4, rand - 0.0006), ring(welt * 0.4, bottom + 0.001), ring(-0.001, bottom)])
    levels = wall_v[0] + (wall_v[1] - wall_v[0]) * np.array([1.0, 0.9, 0.62, 0.55, 0.1, 0.0])
    wall_uv = np.dstack([np.tile(turn[None, :], (len(wall), 1)), np.tile(levels[:, None], (1, AROUND))])
    sole.grid(closed_ring(wall), np.concatenate([wall_uv, np.dstack([np.ones((len(wall), 1)), wall_uv[:, :1, 1]])], axis=1), flip=True)
    under_edge = wall[-1]
    tread = under_edge.copy()
    tread[:, 0] = cx + (under_edge[:, 0] - cx) * 0.5
    spine = np.stack([cx, tread[:, 1], bottom], axis=1)
    under = closed_ring(np.array([under_edge, tread, spine]))
    toe, heel = spec["toe"], spec["heel"]
    plan = np.stack([0.02 + (under[..., 0] - CX + 0.07) / 0.14 * 0.28, 0.01 + (heel + 0.01 - under[..., 1]) / (heel - toe + 0.02) * 0.19], axis=-1)
    sole.grid(under, plan, flip=True)
    return sole


def court_upper(spec, edge, top, out, normals, foot=None):
    """The upper of a shoe that is one piece from the sole's edge to a top line lying on the skin, and the binding
    that turns over that top line. edge, top: a point per station; out: outward in plan; normals: the skin's at the top."""
    h = np.linspace(0, 1, ROWS)[:, None]
    lean, rise = h ** spec["lean"], 1 - (1 - h) ** spec["rise"]
    upper = np.zeros((ROWS, AROUND, 3))
    upper[..., :2] = edge[None, :, :2] + (top - edge)[None, :, :2] * lean[..., None]
    upper[..., 2] = edge[None, :, 2] + (top - edge)[None, :, 2] * rise
    upper[..., :2] += out[None, :, :2] * (spec["bulge"] * np.sin(np.pi * h) ** 0.8)[..., None]
    if foot is not None:
        first = spec.get("clear_from", 1)                          # rows from here up are kept clear of the skin
        upper[first:] = foot.surface.push_out(upper[first:], spec["ease"])
        # The skin is finer than the shoe: where it bulges between two of the shoe's vertices (an ankle bone), the
        # middle of that edge is taken clear of it too.
        for _ in range(3):
            for r in range(first, ROWS):
                for k in range(AROUND):
                    for r2, k2 in ((r, (k + 1) % AROUND), (r + 1, k), (r + 1, (k + 1) % AROUND)):
                        if r2 >= ROWS:
                            continue
                        middle = (upper[r, k] + upper[r2, k2]) / 2
                        location, normal = foot.nearest(middle)
                        signed = (middle - location) @ normal
                        if signed < spec["ease"] * 0.5 and np.linalg.norm(middle - location) < 0.006:
                            upper[r, k] += normal * (spec["ease"] * 0.5 - signed)
                            upper[r2, k2] += normal * (spec["ease"] * 0.5 - signed)
    shell = Part()
    box = COURT_UV["upper"]
    turn = np.arange(AROUND) / AROUND
    uv = np.dstack([np.tile(turn[None, :], (ROWS, 1)), box[1] + (box[3] - box[1]) * np.tile(h, (1, AROUND))])
    shell.grid(closed_ring(upper), np.concatenate([uv, np.dstack([np.ones((ROWS, 1)), uv[:, :1, 1]])], axis=1))
    rim = upper[-1]
    along = unit_rows(rim - upper[-2])
    binding = Part()
    w = spec["binding"]
    rows = np.array([rim - along * w + normals * 0.0010, rim + normals * 0.0013, rim + along * 0.0012 + normals * 0.0002, rim + along * 0.0006 - normals * 0.0030])
    box = COURT_UV["binding"]
    b_uv = np.dstack([np.tile(turn[None, :], (4, 1)), np.tile(np.linspace(box[1], box[3], 4)[:, None], (1, AROUND))])
    binding.grid(closed_ring(rows), np.concatenate([b_uv, np.dstack([np.ones((4, 1)), b_uv[:, :1, 1]])], axis=1))
    return shell, binding, upper


def smooth_ring(values, rounds=1):
    values = np.array(values, dtype=float)
    for _ in range(rounds):
        values = (np.roll(values, 1, axis=0) + 2 * values + np.roll(values, -1, axis=0)) / 4
    return values


def build_flat(spec):
    """A ballet flat on the bare foot: a thin sole under it, an upper that covers the toes and runs low round the sides
    to a heel cup, its top line bound and lying on the skin, a small bow at the throat."""
    phi, y, x, q, side, middle, half = outline(spec)
    cx = curve(spec["centre"])(y)
    foot = Foot([("FemNakedFeet", [0])])
    edge = np.stack([cx + x, y, np.full(AROUND, spec["edge_z"])], axis=1)
    u0, reach = spec["throat_start"], spec["throat_reach"]
    spine_y = np.clip(y, u0, spec["spine_back"])
    origin = np.stack([curve(spec["centre"])(spine_y), spine_y], axis=1)
    direction = unit_rows(np.concatenate([edge[:, :2] - origin, np.zeros((AROUND, 1))], axis=1))[:, :2]
    # The sole's edge is the foot's own outline (the widest of a few heights) and a margin: toes and heel are not an oval.
    radius = np.zeros(AROUND)
    for k in range(AROUND):
        found = [foot.side(origin[k], direction[k], z) for z in (0.003, 0.009, 0.016, 0.024)]
        found = [r for r in found if r is not None]
        radius[k] = max(found) if found else np.linalg.norm(edge[k, :2] - origin[k])
    radius = np.maximum(np.maximum(radius, np.roll(radius, 1)), np.roll(radius, -1))        # over the gaps between the toes
    radius = np.maximum(radius, smooth_ring(radius, 6)) + spec["last"]
    edge[:, :2] = origin + direction * radius[:, None]
    height = spec["side"] + (spec["back"] - spec["side"]) * np.clip((y - spec["cup_from"]) / (spec["heel"] - 0.01 - spec["cup_from"]), 0, 1) ** 1.5
    top = np.zeros((AROUND, 3))
    sides = y >= u0
    for k in np.where(sides)[0]:
        r = foot.side(origin[k], direction[k], height[k])
        if r is None:
            r = np.linalg.norm(edge[k, :2] - origin[k]) - 0.006
        top[k] = [origin[k, 0] + direction[k, 0] * (r + spec["ease"]), origin[k, 1] + direction[k, 1] * (r + spec["ease"]), height[k]]
    # In front the top line crosses the foot behind the toes: a U from one side to the other.
    first = {}
    for s in (-1.0, 1.0):
        mine = [k for k in np.where(sides)[0] if side[k] == s]
        first[s] = mine[int(np.argmin(y[mine]))]
    centre_front = float(curve(spec["centre"])(u0))
    for k in np.where(~sides)[0]:
        s = side[k] if side[k] != 0 else 1.0
        a = np.clip((u0 - y[k]) / (u0 - spec["toe"]), 0, 1) ** spec["throat_power"] * np.pi / 2
        wide = abs(top[first[s], 0] - centre_front)
        px, py = centre_front + s * wide * math.cos(a), u0 - reach * math.sin(a)
        z = foot.top(px, py)
        z = (spec["side"] if z is None else z + spec["ease"])
        blend = np.clip(a / 0.5, 0, 1)
        top[k] = [px, py, spec["side"] * (1 - blend) + z * blend]
    top[:, 2] = smooth_ring(top[:, 2], 2)
    top[:, :2] = smooth_ring(top[:, :2], 1)
    normals = np.array([foot.nearest(p)[1] for p in top])
    points = np.array([foot.nearest(p)[0] for p in top])
    inside = np.stack([origin[:, 0], origin[:, 1], np.full(AROUND, 0.012)], axis=1)
    normals = np.where((np.einsum("ij,ij->i", normals, points - inside) < 0)[:, None], -normals, normals)
    top = points + normals * spec["ease"]
    out = np.concatenate([direction, np.zeros((AROUND, 1))], axis=1)
    shell, binding, upper = court_upper(spec, edge, top, out, normals, foot)

    # ---- the bow: a knot and two flat loops of ribbon lying on the vamp in front of the throat
    bow = Part()
    front = AROUND // 2
    lie = unit(upper[-1, front] - upper[-3, front])                    # up the vamp towards the throat
    across = np.array([1.0, 0.0, 0.0])
    normal = unit(np.cross(across, lie))
    normal = normal if normal[2] > 0 else -normal
    knot = upper[-1, front] - lie * 0.007 + normal * 0.0035
    rect = COURT_UV["bow"]
    tube(bow, [knot - across * 0.0035, knot + across * 0.0035], 0.0036, (rect[0], rect[1], rect[0] + 0.05, rect[3]), sides=5, hint=normal, cap=True)
    for s in (-1, 1):
        t = np.linspace(0, 2 * np.pi, 8)[:, None]
        loop = knot + across * s * 0.016 * (1 - np.cos(t)) / 2 + lie * 0.0055 * np.sin(t) + normal * (0.0015 * np.sin(t / 2))
        tube(bow, loop, 0.0036, (rect[0] + 0.06, rect[1], rect[2], rect[3]), sides=4, squash=0.3, hint=normal)
        tail = knot + np.array([[0, 0, 0]]) + (across * s * np.array([0.001, 0.007, 0.012])[:, None] - lie * np.array([0.0, 0.008, 0.016])[:, None] - normal * np.array([0.0, 0.0012, 0.002])[:, None])
        tube(bow, tail, 0.0034, (rect[0] + 0.06, rect[1], rect[2], rect[3]), sides=4, squash=0.3, hint=normal, cap=True)

    ground = np.where(y > spec["heel_breast"], spec["ground"] - spec["heel_lift"], spec["ground"]) + spec["spring"] * np.clip((q - 0.75) / 0.25, 0, 1) ** 2
    sole = thin_sole(spec, edge, ground, phi, cx, y)
    return [shell, binding, bow, sole]


def build_heel(spec):
    """A court shoe with a high heel. The game poses the foot flat; its own pumps carry the pitch in the mesh (the skin
    part they show is a foot already standing on a heel), so a shoe built round that skin stands as high as theirs.
    The top line follows the edge of that skin a little way in; the sole is a platform under the ball, a shank up
    the arch and a seat for the heel, which is a part of its own."""
    phi, y, x, q, side, middle, half = outline(spec)
    cx = curve(spec["centre"])(y)
    foot = Foot([("FemSuitShoes", [0])])
    sole_top = curve(spec["sole_top"])
    edge = np.stack([cx + x, y, sole_top(y)], axis=1)
    border, inward, normals = foot.opening()
    pivot = np.array(spec["pivot"])
    inside = np.stack([np.full(len(border), pivot[0]), border[:, 1], border[:, 2] - 0.03], axis=1)
    flip = np.einsum("ij,ij->i", normals, border - inside) < 0
    normals = np.where(flip[:, None], -normals, normals)
    line = border + inward * spec["margin"] + normals * spec["ease"]
    angle = np.arctan2(line[:, 0] - pivot[0], line[:, 1] - pivot[1])
    order = np.argsort(angle)
    wanted = np.arctan2(edge[:, 0] - pivot[0], edge[:, 1] - pivot[1])
    top = np.stack([np.interp(wanted, angle[order], line[order, c], period=2 * np.pi) for c in range(3)], axis=1)
    top_normals = unit_rows(np.stack([np.interp(wanted, angle[order], normals[order, c], period=2 * np.pi) for c in range(3)], axis=1))
    top = smooth_ring(top, spec["smooth"])
    found = [foot.nearest(point) for point in top]
    top_normals = np.array([n for _, n in found])
    inside = np.stack([np.full(AROUND, pivot[0]), top[:, 1], top[:, 2] - 0.03], axis=1)
    top_normals = np.where((np.einsum("ij,ij->i", top_normals, top - inside) < 0)[:, None], -top_normals, top_normals)
    top = np.array([point for point, _ in found]) + top_normals * spec["ease"]
    top[:, 2] += spec["cut"] * np.clip((y - spec["cut_from"]) / 0.03, 0, 1)             # the heel cup stands a little above the skin's edge
    out = unit_rows(np.concatenate([edge[:, :2] - np.stack([cx, y], axis=1), np.zeros((AROUND, 1))], axis=1) + np.stack([0 * y, -np.cos(phi) * 0.02, 0 * y], axis=1), flat=True)
    shell, binding, upper = court_upper(spec, edge, top, out, top_normals, foot)

    shank = edge[:, 2] - spec["shank"]
    ground = np.where(y < spec["ball"], spec["spring"] * np.clip((q - 0.75) / 0.25, 0, 1) ** 2, shank)
    ground = np.minimum(ground, shank)
    sole = thin_sole(spec, edge, ground, phi, cx, y)

    # ---- the heel: from under the seat down to the ground, a block that narrows, flat in front
    heel = Part()
    back = [k for k in list(range(AROUND - spec["heel_stations"], AROUND)) + list(range(0, spec["heel_stations"] + 1))]
    seat = np.stack([edge[back, 0], edge[back, 1], shank[back] + 0.0005], axis=1)
    seat_centre = np.array([seat[:, 0].mean(), spec["heel_y"], 0.0])
    levels = np.linspace(0, 1, 6)
    rows = []
    for level in levels:
        scale = 1 - (1 - spec["heel_taper"]) * level ** spec["heel_curve"]
        ring = seat_centre + (seat - seat_centre) * np.array([scale, scale, 0.0])
        ring[:, 2] = seat[:, 2] * (1 - level)
        rows.append(np.concatenate([ring, ring[:1]]))                # closed across the breast
    rows = np.array(rows)
    sheet(heel, rows, rect_uv(len(levels), rows.shape[1], COURT_UV["heel"]), np.array([seat_centre[0], seat_centre[1], seat[:, 2].mean() / 2]))
    base = rows[-1]
    hub = np.repeat(base.mean(axis=0)[None], len(base), axis=0)
    sheet(heel, np.array([hub, base]), rect_uv(2, len(base), (COURT_UV["heel"][0], COURT_UV["heel"][1], COURT_UV["heel"][2], COURT_UV["heel"][1] + 0.01)),
          np.array([seat_centre[0], seat_centre[1], 0.05]))
    return [shell, binding, heel, sole]


def paint_court(style, spec, colourways):
    c = Canvas(seed=7)
    m, s, up = c.region(COURT_UV["upper"])
    q = 1 - np.abs(s * 2 - 1)
    leather(c, m, "upper", spec["grain"], spec.get("wear", 0.0))
    c.panel(m, 0.012 - q, raise_by=0.25, stitches=(0.005,), shadow=0.004)                 # the back seam
    if spec.get("side_seam"):
        c.stitch(m, np.abs(q - spec["side_seam"]) - 0.0, 0.0014, 140.0)
    c.shade *= 1 - m * 0.22 * np.exp(-(up / 0.05) ** 2)
    c.shade *= 1 - m * spec.get("burnish", 0.0) * np.clip((q - 0.86) / 0.14 * np.clip(1.5 - 1.6 * up, 0, 1), 0, 1)
    c.stitch(m, up - (1 - spec["top_stitch"]), 0.006, 150.0)
    m, s, t = c.region(COURT_UV["binding"])
    ribs = 0.5 + 0.5 * np.sin(s * 2 * np.pi * spec.get("ribs", 260))
    c.fill(m, "binding", 0.86 + 0.16 * ribs * spec.get("ribbed", 1.0), ribs * 0.5 * spec.get("ribbed", 1.0))
    m, s, t = c.region(COURT_UV["bow"])
    ribs = 0.5 + 0.5 * np.sin(t * 2 * np.pi * 14)
    c.fill(m, "binding", 0.86 + 0.16 * ribs, ribs * 0.4)
    m, s, t = c.region(COURT_UV["heel"])
    c.fill(m, "heel", 1.0 + canvas_grain(c, 0.06), c.grain * spec["grain"])
    lift = m & (t > 0.955)
    c.fill(lift, "outsole", 0.9, 0.0)
    c.height -= m * 0.8 * np.exp(-((t - 0.955) / 0.006) ** 2)
    m, s, t = c.region(COURT_UV["lining"])
    c.fill(m, "lining", 0.9, c.grain * 0.3)
    paint_sole(c, dict(spec, teeth=False, welt_stitch=False))
    return c.save("feet_" + style, spec["relief"], colourways)


FLAT = dict(
    toe=-0.243, heel=0.057, outline_power=0.62,
    width=[(-0.243, 0.028), (-0.225, 0.045), (-0.20, 0.054), (-0.16, 0.0565), (-0.10, 0.0535), (-0.06, 0.0495), (0.0, 0.0455), (0.057, 0.0375)],
    centre=[(-0.243, 0.142), (-0.20, 0.149), (-0.06, 0.153), (0.0, 0.148), (0.057, 0.1495)],
    edge_z=0.003, ground=-0.006, heel_lift=0.0015, heel_breast=-0.01, spring=0.005, welt=0.0022, rand=0.5,
    side=0.031, back=0.053, cup_from=-0.03, throat_start=-0.118, throat_reach=0.052, throat_power=0.8, spine_back=0.02,
    ease=0.0032, last=0.0042, lean=1.5, rise=2.3, bulge=0.0015, binding=0.005, top_stitch=0.13,
    grain=0.06, relief=1.8, burnish=0.12, wear=0.08, ribbed=1.0,
)

HEEL = dict(
    toe=-0.207, heel=0.060, outline_power=0.54,
    width=[(-0.207, 0.027), (-0.19, 0.038), (-0.15, 0.0435), (-0.11, 0.0435), (-0.06, 0.038), (-0.01, 0.034), (0.03, 0.0315), (0.060, 0.026)],
    centre=[(-0.207, 0.1555), (-0.12, 0.155), (-0.02, 0.1515), (0.060, 0.1485)],
    sole_top=[(-0.207, 0.009), (-0.14, 0.009), (-0.115, 0.013), (-0.08, 0.035), (-0.04, 0.062), (0.0, 0.083), (0.03, 0.090), (0.060, 0.091)],
    pivot=(0.154, -0.05), margin=0.009, ease=0.0036, smooth=4, clear_from=1, cut=0.0, cut_from=0.0,
    ball=-0.12, shank=0.005, spring=0.003, welt=0.0018, rand=0.55,
    heel_stations=5, heel_y=0.047, heel_taper=0.38, heel_curve=0.6,
    lean=1.5, rise=2.4, bulge=0.002, binding=0.004, top_stitch=0.10, ribbed=0.0,
    grain=0.05, relief=1.8, burnish=0.1, wear=0.06,
)


WHEAT, RUBBER, HONEY = (0.62, 0.44, 0.22), (0.07, 0.065, 0.06), (0.50, 0.36, 0.20)

BOOT = dict(
    toe=-0.252, heel=0.070, toe_top=-0.19,
    width=[(-0.252, 0.041), (-0.215, 0.055), (-0.14, 0.059), (-0.06, 0.053), (0.0, 0.051), (0.07, 0.044)],
    ridge=[(-0.252, 0.050), (-0.215, 0.066), (-0.15, 0.080), (-0.10, 0.102), (-0.06, 0.126), (0.0, 0.140)],
    sole_top=[(-0.252, 0.028), (-0.2, 0.024), (-0.1, 0.027), (-0.02, 0.038), (0.07, 0.042)],
    bulge=0.004, roundness=2.6, quarter_roundness=1.3, crown=0.003, plateau=-0.004, toe_back=0.45,
    # The shaft is as wide as the game's own boot: its trousers draw their hems in ("Boots") to go inside that.
    throat_column=12, slot_column=10, gap=0.015, leg_shift=-0.0065,
    ring_x=(0.045, 0.048), ring_back=(0.052, 0.056), ring_y=(0.008, 0.010), top_front=-0.053, top=0.198, top_rise=0.018, shaft_rows=4,
    collar_height=0.024,
    collar_profile=[(0.0003, 0.0), (0.0042, 0.16), (0.0055, 0.45), (0.0045, 0.78), (0.0005, 0.98), (-0.0050, 0.93), (-0.0100, 0.55), (-0.0115, -0.8)],
    tongue_width=0.019, tongue_curve=0.005, tongue_above=0.012,
    eyelets=6, first_eyelet=0.012, eyelet_inset=0.011, eyelet_radius=0.0036, lace_radius=0.0017, lace_lift=0.0075,
    welt=0.006, spring=0.010, arch=0.012, heel_breast=-0.012, ball=-0.14, step=0.002,
    toe_cap=True, teeth=True, lugs=60, outsole_edge=True, welt_stitch=True, grain=0.16, relief=2.2, burnish=0.3, wear=0.25,
)

# style -> how it is built and painted, its colourways, whose skin shows above it, and what it is skinned from
STYLES2 = {
    # A laced leather boot to above the ankle: derby quarters over a capped vamp, seven pairs of eyelets, padded collar.
    "boot": dict(build=build_boot, paint=paint_boot, spec=BOOT, skin_from="Boots",
                 forms={"male": [("MaleBoots", [0, 1])], "female": [("FemBoots", [1, 3])]},
                 colourways={
                     "brown": way2(BROWN, (0.20, 0.13, 0.09), RUBBER, lace=(0.30, 0.19, 0.10), welt=(0.42, 0.30, 0.18), thread=(0.52, 0.38, 0.22)),
                     "black": way2((0.055, 0.055, 0.06), (0.04, 0.04, 0.04), RUBBER, lace=(0.07, 0.07, 0.075), accent=(0.035, 0.035, 0.04), welt=(0.06, 0.055, 0.05),
                                   thread=(0.13, 0.13, 0.14), metal=(0.5, 0.5, 0.52)),
                     "tan": way2(WHEAT, (0.50, 0.38, 0.24), (0.28, 0.20, 0.12), lace=(0.66, 0.50, 0.22), accent=(0.22, 0.14, 0.08), welt=(0.55, 0.43, 0.28),
                                 thread=(0.75, 0.62, 0.40)),
                     "oxblood": way2(OXBLOOD, (0.10, 0.05, 0.04), RUBBER, lace=(0.16, 0.05, 0.05), welt=(0.30, 0.20, 0.14), thread=(0.42, 0.14, 0.12)),
                 }),
    # A penny loafer: apron sewn in with a raised seam, saddle strap with the slot, low stacked heel.
    "loafer": dict(build=build_loafer, paint=paint_loafer, spec=LOAFER, skin_from="Shoes",
                   forms={"male": [("MaleNakedFeet", [0])], "female": [("FemNakedFeet", [0])]},
                   colourways={
                       "brown": way2((0.30, 0.16, 0.085), DARK, (0.2, 0.14, 0.1), lining=(0.60, 0.47, 0.34), welt=(0.20, 0.12, 0.07), thread=(0.42, 0.27, 0.15)),
                       "black": way2((0.05, 0.05, 0.055), (0.03, 0.03, 0.03), (0.08, 0.07, 0.07), lining=(0.32, 0.27, 0.22), welt=(0.04, 0.04, 0.04), thread=(0.12, 0.12, 0.13)),
                       "tan": way2((0.52, 0.34, 0.18), DARK, (0.2, 0.14, 0.1), lining=(0.70, 0.58, 0.44), welt=(0.30, 0.19, 0.10), thread=(0.66, 0.50, 0.30)),
                       "oxblood": way2((0.31, 0.065, 0.065), (0.07, 0.04, 0.04), (0.1, 0.07, 0.06), lining=(0.56, 0.43, 0.34), welt=(0.12, 0.05, 0.05), thread=(0.45, 0.16, 0.14)),
                   }),
    # A ballet flat (women): thin sole, low bound top line on the bare foot, a bow at the throat.
    "flat": dict(build=build_flat, paint=paint_court, spec=FLAT, skin_from="NakedFeet", forms={"female": [("FemNakedFeet", [0])]},
                 colourways={
                     "black": way2((0.05, 0.05, 0.055), (0.035, 0.035, 0.035), (0.42, 0.31, 0.21), binding=(0.03, 0.03, 0.035), thread=(0.12, 0.12, 0.13)),
                     "nude": way2((0.76, 0.60, 0.48), (0.50, 0.38, 0.28), (0.42, 0.31, 0.21), binding=(0.66, 0.50, 0.39), thread=(0.82, 0.70, 0.58)),
                     "red": way2((0.60, 0.06, 0.07), (0.10, 0.04, 0.04), (0.42, 0.31, 0.21), binding=(0.42, 0.04, 0.05), thread=(0.75, 0.2, 0.2)),
                     "navy": way2((0.07, 0.10, 0.22), (0.04, 0.045, 0.07), (0.42, 0.31, 0.21), binding=(0.045, 0.065, 0.15), thread=(0.2, 0.25, 0.42)),
                 }),
    # A court shoe on a high block heel (women), round the pitched foot of the game's own pumps.
    "heel": dict(build=build_heel, paint=paint_court, spec=HEEL, skin_from="SuitShoes", forms={"female": [("FemSuitShoes", [0, 1])]},
                 colourways={
                     "black": way2((0.05, 0.05, 0.055), (0.03, 0.03, 0.03), (0.40, 0.29, 0.20), binding=(0.045, 0.045, 0.05), heel=(0.045, 0.045, 0.05), thread=(0.12, 0.12, 0.13)),
                     "nude": way2((0.76, 0.60, 0.48), (0.48, 0.36, 0.27), (0.40, 0.29, 0.20), binding=(0.72, 0.56, 0.44), heel=(0.72, 0.56, 0.44), thread=(0.82, 0.70, 0.58)),
                     "red": way2((0.60, 0.06, 0.07), (0.06, 0.03, 0.03), (0.40, 0.29, 0.20), binding=(0.54, 0.05, 0.06), heel=(0.54, 0.05, 0.06), thread=(0.75, 0.2, 0.2)),
                     "navy": way2((0.07, 0.10, 0.22), (0.03, 0.035, 0.05), (0.40, 0.29, 0.20), binding=(0.06, 0.085, 0.19), heel=(0.06, 0.085, 0.19), thread=(0.2, 0.25, 0.42)),
                 }),
}


def build_all(only=None):
    sys.path.insert(0, os.path.join(ba.ROOT, "_tools", "pylib"))
    path = os.path.join(ba.BUILD, "catalogue_shoes.json")
    items, textures = [], []
    kept = {"items": [], "textures": []}
    if only and os.path.exists(path):
        with open(path) as f:
            kept = json.load(f)

    def style_of(name):                      # feet_<m|f>_<style>_<colour> or feet_<style>_<n|colour>
        bits = name.split("_")
        return bits[2] if bits[1] in ("m", "f") else bits[1]

    def keep(style):
        items.extend(i for i in kept["items"] if style_of(i["id"]) == style)
        textures.extend(t for t in kept["textures"] if style_of(t["name"]) == style)

    for style, (spec, colourways) in STYLES.items():
        if only and style not in only:
            keep(style)
            continue
        textures += paint(style, spec, colourways)
        parts = build(spec)
        for sex, who in FEET.items():
            mesh_id = "feet_%s_%s" % (who["tag"], style)
            stats = finish(mesh_id, parts, Form(who["form"]))
            for colour_name in colourways:
                items.append({"id": "%s_%s" % (mesh_id, colour_name), "kind": "feet", "sex": sex, "mesh": mesh_id, "skinFrom": SKIN_FROM[style],
                              "layers": [{"role": "cloth", "texture": "feet_%s_%s" % (style, colour_name), "normal": "feet_%s_n" % style}],
                              "stats": stats})
    for style, entry in STYLES2.items():
        if only and style not in only:
            keep(style)
            continue
        textures += entry["paint"](style, entry["spec"], entry["colourways"])
        for sex, parts_of_form in entry["forms"].items():
            tag = FEET[sex]["tag"]
            mesh_id = "feet_%s_%s" % (tag, style)
            parts = [p for p in entry["build"](dict(entry["spec"], sex=sex)) if len(p.vertices)]
            stats = finish(mesh_id, parts, ShoeForm(parts_of_form))
            for colour_name in entry["colourways"]:
                items.append({"id": "%s_%s" % (mesh_id, colour_name), "kind": "feet", "sex": sex, "mesh": mesh_id, "skinFrom": entry["skin_from"],
                              "layers": [{"role": "cloth", "texture": "feet_%s_%s" % (style, colour_name), "normal": "feet_%s_n" % style}],
                              "stats": stats})
    with open(path, "w") as f:
        json.dump({"items": items, "textures": textures}, f, indent=1)


if __name__ == "__main__":
    # blender -b -P build_shoes.py            every style
    # blender -b -P build_shoes.py -- boot    only these (the others' catalogue entries are kept as they are)
    build_all(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None)
