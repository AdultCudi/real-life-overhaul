"""Garments: cut as panels round the measured body, draped by cloth simulation, baked and painted in their own layout.

    blender -b -P build_garments.py                    every garment
    blender -b -P build_garments.py -- legs_m_jeans    only the named ones (the catalogue keeps the others)
    blender -b -P build_garments.py -- --no-drape ...  skip the simulation (fast: for checking a cut)

Per garment (see garment_lib.py for the tools and README.md, "Garments", for the method and how to add one):
  cut       fine quad panels (Sheet) from the body's cross-sections, with designed openings and ease per region;
  drape     Blender's cloth solver over the body (and shoes), pinned where the garment is held; then frozen;
  mesh      the game mesh is every n-th row and column of the draped panels, plus construction as geometry
            (waistband, hems turned in, belt loops, button, collar, ribs);
  bake      the fine folds into a tangent normal map by resampling, with the fabric's relief; the folds' hollows
            into the colour; seams, stitching, pockets and wear painted in the panel's own metric coordinates;
  skin      weights and Fat/Slim/Musc from the game's own nearest garment or body, by closest surface point.
Writes build/meshes/<id>.json, build/textures/<id>_*.png and build/catalogue_garments.json.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba  # noqa: E402
import garment_lib as gl  # noqa: E402
from garment_lib import Atlas, Mesh, Sheet, Surface, curve  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
DRAPE = "--no-drape" not in ARGS
ATLAS = 2048             # textures are painted at this size and shipped smaller
FINE = 3                 # fine rows and columns per row and column of the game mesh
PARTIAL = os.path.join(ba.BUILD, "garments")      # one catalogue fragment per garment, joined at the end


def ring_directions(count):
    """Round a left leg: 0 = the inseam (towards the other leg), then the front, the outside, the back."""
    a = 2 * np.pi * np.arange(count) / count
    return np.stack([-np.cos(a), -np.sin(a)], axis=1)


# ================================================================ the palette
#
# ONE list of colours for every garment: an item "<mesh>_<colour id>" exists for every garment and every entry, so the
# mod's two dropdowns (garment, colour) always meet. A plain entry is a cloth colour; trim, thread, buttons and zips
# follow from it by rule (each paint function has its own). A patterned entry names its print (`pattern`), a second
# colour and the plain entry a garment falls back to when it cannot carry that print (a garment lists what it can in
# `patterns`). A print costs a mask texture per garment, a colour costs nothing: see garment_lib.Tinted.
# The mod's side of this list is CharacterCatalogue.Colours (same ids, same order) with the labels in Locales/en.json;
# an id that is no longer here (or was a wash of the baked jeans) is an alias there, resolved when a save is read.

WHITE, NAVY, BLACK = (0.90, 0.90, 0.88), (0.065, 0.09, 0.19), (0.04, 0.04, 0.045)
PALETTE = {
    # neutrals
    "white": dict(label="White", cloth=WHITE),
    "cream": dict(label="Cream", cloth=(0.84, 0.77, 0.63)),
    "stone": dict(label="Stone", cloth=(0.70, 0.66, 0.58)),
    "grey": dict(label="Grey", cloth=(0.38, 0.38, 0.39)),
    "charcoal": dict(label="Charcoal", cloth=(0.14, 0.14, 0.15)),
    "black": dict(label="Black", cloth=BLACK),
    # blues
    "navy": dict(label="Navy", cloth=NAVY),
    "indigo": dict(label="Indigo blue", cloth=(0.17, 0.22, 0.34)),
    "cobalt": dict(label="Cobalt blue", cloth=(0.07, 0.20, 0.58)),
    "sky": dict(label="Sky blue", cloth=(0.50, 0.66, 0.82)),
    "teal": dict(label="Teal", cloth=(0.08, 0.36, 0.38)),
    # greens
    "emerald": dict(label="Emerald", cloth=(0.03, 0.30, 0.20)),
    "forest": dict(label="Forest green", cloth=(0.07, 0.17, 0.10)),
    "olive": dict(label="Olive", cloth=(0.27, 0.29, 0.16)),
    "sage": dict(label="Sage green", cloth=(0.48, 0.58, 0.46)),
    # yellows and browns
    "mustard": dict(label="Mustard", cloth=(0.74, 0.54, 0.13)),
    "tan": dict(label="Tan", cloth=(0.58, 0.47, 0.33)),
    "chocolate": dict(label="Chocolate brown", cloth=(0.23, 0.14, 0.09)),
    "rust": dict(label="Rust", cloth=(0.50, 0.22, 0.12)),
    # reds
    "red": dict(label="Red", cloth=(0.62, 0.07, 0.09)),
    "burgundy": dict(label="Burgundy", cloth=(0.30, 0.045, 0.085)),
    "blush": dict(label="Blush pink", cloth=(0.80, 0.56, 0.56)),
    # prints and two-tone: `cloth` is the ground, `second` the print or the other colour
    "heather": dict(label="Heather grey", pattern="heather", cloth=(0.56, 0.57, 0.58), plain="grey"),
    "dots": dict(label="Polka dots, navy and white", pattern="dots", cloth=(0.07, 0.10, 0.22), second=WHITE, plain="navy"),
    "dotsred": dict(label="Polka dots, red and cream", pattern="dots", cloth=(0.62, 0.08, 0.10), second=(0.92, 0.90, 0.86), plain="red"),
    "finedots": dict(label="Fine dots, yellow and white", pattern="finedots", cloth=(0.86, 0.68, 0.20), second=(0.95, 0.94, 0.90), plain="mustard"),
    "breton": dict(label="Breton stripes, white and navy", pattern="stripes", cloth=WHITE, second=NAVY, plain="navy"),
    "pinstripe": dict(label="Pinstripe, charcoal", pattern="pinstripe", cloth=(0.13, 0.13, 0.145), second=(0.42, 0.42, 0.43), plain="charcoal"),
    "gingham": dict(label="Gingham, blue and white", pattern="gingham", cloth=WHITE, second=(0.13, 0.30, 0.62), plain="sky"),
    "block": dict(label="Colour block, white and navy", pattern="block", cloth=WHITE, second=NAVY, plain="navy"),
    "trim": dict(label="Black with white trim", pattern="trim", cloth=BLACK, second=WHITE, plain="black"),
    "ringer": dict(label="White with navy trim", pattern="trim", cloth=WHITE, second=NAVY, plain="white"),
}
TIERS = ("everyday", "smart", "premium")


def colourways(spec):
    """(colour id, entry) for every entry of the palette, as this garment wears it: a print it cannot carry
    (not in its `patterns`) becomes the entry's plain colour (`id` then says which, for a garment with rules by colour)."""
    can = spec.get("patterns", ())
    return [(name, entry if entry.get("pattern") is None or entry["pattern"] in can else dict(PALETTE[entry["plain"]], id=entry["plain"]))
            for name, entry in PALETTE.items()]


def luminance(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def blend(a, b, t):
    return tuple(np.array(a) * (1 - t) + np.array(b) * t)


def bands(d, pitch, width, soft=0.0006):
    """Stripes across the coordinate d (metres): coverage 0..1 of a band `width` wide every `pitch`."""
    off = np.abs((d / pitch) % 1.0 - 0.5) * pitch
    return np.clip((width / 2 - off) / soft + 0.5, 0, 1)


def check(a, b, square=0.009):
    """Gingham over shear-free cloth coordinates: (where both stripes cross, where one runs alone)."""
    h, v = bands(b, 2 * square, square), bands(a, 2 * square, square)
    return h * v, h * (1 - v) + v * (1 - h)


def dots(across, up, size=0.0045, pitch=0.022, shift=0.0):
    """A dotted print in cloth coordinates (the same size on every panel of a garment): coverage 0..1. shift 0.5 is
    the same lattice half a pitch on, in the gaps of the first: two dot sizes that never touch share one mask."""
    a, b = across / pitch, up / pitch
    b2 = b + 0.5 * (np.floor(a) % 2) + shift
    d = np.hypot((a % 1.0) - 0.5, (b2 % 1.0) - 0.5) * pitch
    return np.clip((size - d) / 0.0006, 0, 1)


def print_masks(spec, pattern_coords, level, parts, blank=None):
    """The masks of the prints a knit top or a skirt can carry, by pattern: (tag, make, size).
    pattern_coords: shear-free (across, up) for dots and checks; level: the coordinate level stripes run across;
    parts: (red, blue) for the two-tone entries: what takes the second colour of a colour block, and the trim;
    blank: where no print goes (ribbing)."""
    keep = 1.0 if blank is None else 1 - blank
    zero = np.zeros(level.shape)
    a, b = pattern_coords

    def spots():
        return np.dstack([dots(a, b) * keep, zero, dots(a, b, 0.0028, shift=0.5) * keep])

    def lines():
        return np.dstack([level * keep, zero, zero])

    def squares():
        both, one = check(a, b)
        return np.dstack([both * keep, zero, one * keep])

    def pieces():
        return np.dstack([parts[0] * (1 - parts[1]), zero, parts[1]])
    big = spec.get("mask_size", spec["colour_size"])
    return {"dots": ("dots", spots, big), "stripes": ("stripes", lines, spec.get("stripe_size", 512)),
            "gingham": ("check", squares, big), "parts": ("parts", pieces, spec.get("parts_size", 512))}


def print_tints(entry, masks, block=None):
    """(mask, red, green, blue) for a palette entry on a knit top or a skirt. block: what a colour block is here:
    None = a mask's red part takes the second colour, or a plain colour for the whole piece (a dress: bodice and skirt)."""
    cloth, second, pattern = entry["cloth"], entry.get("second"), entry.get("pattern")
    if pattern == "dots":
        return masks["dots"], second, cloth, cloth
    if pattern == "finedots":
        return masks["dots"], cloth, cloth, second
    if pattern == "stripes":
        return masks["stripes"], second, cloth, cloth
    if pattern == "gingham":
        return masks["gingham"], second, cloth, blend(cloth, second, 0.5)
    if pattern == "trim":
        return masks["parts"], cloth, cloth, second
    if pattern == "block":
        if block is not None:
            return None, block, block, block
        return masks["parts"], second, cloth, second
    return None, cloth, cloth, cloth


# ================================================================ trousers


def cut_trousers(spec):
    """The two halves of a pair of trousers (left, and its mirror) as fine sheets.

    Each half is one rectangle: columns run round the leg from the inseam, rows from the hem up. Above the crotch
    only the outer three quarters of the columns go on, round the hip from the centre front to the centre back;
    the top edge of the inner quarter is the crotch seam, on the centre plane.
    """
    ref = Surface([(spec["fit"], None)])                         # the game's own trousers: where hips and seat are
    body = Surface(spec["body"])                                 # bare legs and feet
    n, f = spec["columns"], FINE
    nf = n * f
    cf, cb = nf // 8, nf - nf // 8                               # centre front and centre back columns
    directions = ring_directions(nf)

    # ---- the centre plane: the crotch curve
    centre_cut = ref.segments(0, 0.004)[:, :, 1:]
    crotch = float(centre_cut[..., 1].min())
    hip, thigh, waist, hem = crotch + spec["rise_low"], crotch - 0.025, spec["waist"], spec["hem"]
    middle_y = float(centre_cut[..., 0].mean())

    # ---- the leg below the crotch: centre line and radius by (height, angle)
    heights = np.arange(hem - 0.03, thigh + 0.011, 0.01)
    centres, radius = [], []
    for z in heights:
        cut = ref.segments(2, max(z, 0.07))[:, :, :2]
        cut = cut[(cut[..., 0] > 0.001).all(axis=1)]
        centres.append(cut.reshape(-1, 2).mean(axis=0))
    centres = gl.smooth1(np.array(centres), 6)
    knee = ba.bone_head("LowerLeg.L")[2]
    for z, centre in zip(heights, centres):
        cut = ref.segments(2, max(z, 0.07))[:, :, :2]
        cut = cut[(cut[..., 0] > 0.001).all(axis=1)]
        r = gl.fill_nan(gl.ray_radius(cut, centre, directions), True) - spec["fit_inset"]
        skin = body.segments(2, z)[:, :, :2]
        skin = skin[(skin[..., 0] > 0.001).all(axis=1)] if len(skin) else skin
        if len(skin) > 8 and z > 0.16:
            r = np.fmax(r, gl.fill_nan(gl.ray_radius(skin, centre, directions), True))
        ease = spec["ease"](z)
        r = np.maximum(r + ease, spec["leg"](z))
        radius.append(r)
    radius = gl.smooth1(np.array(radius), 3)
    radius = gl.smooth1(radius.T, 4, wrap=True).T

    # The hem is cut on a slant, higher over the instep than at the heel, so it clears the shoe by its cut and
    # not by being pushed there; and it sits a little back, round the heel.
    angle = 2 * np.pi * np.arange(nf) / nf
    hem_line = hem + spec["hem_rise"] * (0.5 + 0.5 * np.sin(angle)) ** 1.5

    def leg_ring(share):
        """share: 0 on the hem line, 1 at the top of the thigh."""
        z = hem_line + (thigh - hem_line) * share
        k = np.clip((z - heights[0]) / 0.01, 0, len(heights) - 1.001)
        i, t = k.astype(int), (k - k.astype(int))[:, None]
        centre = centres[i] * (1 - t) + centres[i + 1] * t
        centre[:, 1] += spec["hem_back"] * np.clip(1 - (z - hem) / 0.35, 0, 1) ** 2
        r = radius[i, np.arange(nf)] * (1 - t[:, 0]) + radius[i + 1, np.arange(nf)] * t[:, 0]
        ring = np.concatenate([centre + directions * r[:, None], z[:, None]], axis=1)
        ring[:, 0] = np.maximum(ring[:, 0], spec["inseam_gap"])
        return ring

    # ---- the hip: half outlines from the centre front round the side to the centre back. The waist is not level:
    # it follows the top edge of the game's trousers (every top of the game is cut to clear that line).
    phi = np.linspace(-np.pi / 2, np.pi / 2, 73)
    rays = np.stack([np.cos(phi), np.sin(phi)], axis=1)
    hip_centre = np.array([0.0, middle_y])
    levels = np.arange(hip - 0.01, float(ref.vertices[:, 2].max()) + 0.004, 0.004)
    table = np.array([gl.ray_radius(ref.segments(2, z)[:, :, :2], hip_centre, rays) for z in levels])
    reached = ~np.isnan(table)
    top_level = np.array([levels[np.nonzero(reached[:, k])[0].max()] for k in range(len(phi))])
    top_level = np.minimum(gl.smooth1(top_level, 4) - 0.003, waist)
    for k in range(len(phi)):
        column = table[:, k]
        good = np.nonzero(~np.isnan(column))[0]
        table[:, k] = np.interp(np.arange(len(levels)), good, column[good])
    table = gl.smooth1(gl.smooth1(table, 2).T, 2).T

    def hip_arc(share):
        """share: 0 at the hip line, 1 at the waist."""
        z = hip + (top_level - hip) * share
        k = np.clip((z - levels[0]) / 0.004, 0, len(levels) - 1.001)
        i, t = k.astype(int), k - k.astype(int)
        r = table[i, np.arange(len(phi))] * (1 - t) + table[i + 1, np.arange(len(phi))] * t + spec["hip_ease"]
        line = np.concatenate([hip_centre + rays * r[:, None], z[:, None]], axis=1)
        line[[0, -1], 0] = 0.0
        return gl.resample(line, cb - cf + 1)

    # ---- the top ring of the leg: the hip arc at the hip line, and the crotch curve under it on the centre plane
    top = np.zeros((nf + 1, 3))
    arc = hip_arc(0.0)
    top[cf:cb + 1] = arc
    psi = np.linspace(np.pi, 2 * np.pi, 91)
    d = np.stack([np.cos(psi), np.sin(psi)], axis=1)
    centre = np.array([middle_y, hip])
    r = gl.fill_nan(gl.ray_radius(centre_cut, centre, d)) + spec["crotch_ease"]
    under = centre + d * r[:, None]                              # (y, z) from the front, under, to the back
    under[0], under[-1] = arc[0, 1:], arc[-1, 1:]
    low = int(np.argmin(under[:, 1]))
    front = gl.resample(under[low::-1], cf + 1)                  # from the lowest point forward and up
    back = gl.resample(under[low:], nf - cb + 1)                 # from the lowest point back and up
    top[0:cf + 1, 1:] = front
    top[cb:nf + 1, 1:] = back[::-1]
    top[0:cf + 1, 0] = 0.0
    top[cb:, 0] = 0.0

    # ---- rows
    leg_cells, blend_cells, hip_cells = spec["leg_rows"] * f, spec["blend_rows"] * f, spec["hip_rows"] * f
    rows = leg_cells + blend_cells + hip_cells + 1
    points = np.zeros((rows, nf + 1, 3))
    for i in range(leg_cells + 1):
        ring = leg_ring(i / leg_cells)
        points[i, :nf], points[i, nf] = ring, ring[0]
    base = points[leg_cells].copy()
    for i in range(1, blend_cells + 1):
        t = i / blend_cells
        t = t * t * (3 - 2 * t) * 0.5 + t * 0.5
        points[leg_cells + i] = base * (1 - t) + top * t
    for i in range(1, hip_cells + 1):
        points[leg_cells + blend_cells + i, cf:cb + 1] = hip_arc(i / hip_cells)
    cells = np.ones((rows - 1, nf), dtype=bool)
    cells[leg_cells + blend_cells:, :cf] = False
    cells[leg_cells + blend_cells:, cb:] = False

    band = int(round(spec["waistband"] / ((waist - hip) / hip_cells)))          # fine rows of the waistband
    kept_rows = list(range(0, rows, f)) + [rows - 1, rows - 1 - band, rows - 2 - band, 1]
    kept_columns = list(range(0, nf + 1, f))
    left = Sheet("left", points, (0.0, 0.0, 0.5, 1.0), kept_rows, kept_columns, cells)
    mirrored = points * np.array([-1.0, 1.0, 1.0])
    right = Sheet("right", mirrored, (0.5, 0.0, 1.0, 1.0), kept_rows, kept_columns, cells, flip=True)
    marks = {"cf": cf, "cb": cb, "nf": nf, "leg": leg_cells, "blend": blend_cells, "hip": hip_cells, "band": band, "rows": rows,
             "crotch": crotch, "hip_z": hip, "thigh": thigh, "knee": knee}
    return [left, right], marks, ref, body


def trousers(spec):
    sheets, m, ref, body = cut_trousers(spec)
    positions, quads = gl.weld(sheets)
    # The grid's winding has to face outwards: check on the left thigh.
    normals = gl.quad_normals(positions, quads)
    probe = sheets[0].ids[m["leg"] // 2, m["nf"] // 2]
    if normals[probe][0] < 0:
        for sheet in sheets:
            sheet.flip = not sheet.flip
        positions, quads = gl.weld(sheets)

    if DRAPE:
        # Held at the waistband and guided over the seat; below that a weak spring to the cut keeps the silhouette,
        # and the cloth is a few percent larger than cut where trousers fold: on the shoe, at the knee, at the crotch.
        pin = np.zeros(len(positions))
        grow = np.zeros(len(positions))
        rows = m["rows"]
        heights = sheets[0].points[:, m["nf"] // 2, 2]
        hold = np.full(rows, spec["hold"])
        start = m["leg"] - m["leg"] // 8
        hold[start:] = np.maximum(hold[start:], np.linspace(0, 1, rows - start) ** 1.5 * 0.9)
        hold[rows - 1 - m["band"]:] = 1.0
        low = heights - heights[0]
        hold = np.maximum(hold, spec.get("hem_hold", 0.0) * np.clip(1 - low / 0.07, 0, 1))
        surplus = np.zeros(rows)
        for z, reach, amount in spec["folds"]:
            z = z if z >= 0 else m["knee"] if z == -1 else m["crotch"]
            surplus = np.maximum(surplus, amount * np.exp(-((heights - z) / reach) ** 2))
        surplus[rows - 1 - m["band"] - 3:] = 0
        for sheet in sheets:
            ids = sheet.ids
            for i in range(rows):
                row = ids[i][ids[i] >= 0]
                pin[row] = np.maximum(pin[row], hold[i])
                grow[row] = np.maximum(grow[row], surplus[i])
        bodies = [(body.vertices, body.triangles)] + [gl.hulls(*gl.read_built(name)) for name in spec.get("over", [])]
        settled = gl.drape(positions, quads, pin, bodies, spec.get("frames", 70), spec.get("cloth"), grow=grow)
        hard = pin >= 0.999
        positions = gl.relax(settled, quads, spec.get("relax", 2), fixed=hard)
    gl.pull(sheets, positions)
    normals = gl.quad_normals(positions, quads)

    # ---- the waistband stands 2 mm proud of the cloth under it (geometry: it shows in the outline)
    lift = np.zeros(len(positions))
    for sheet in sheets:
        ids = sheet.ids[m["rows"] - 1 - m["band"]:]
        lift[ids[ids >= 0]] = 0.002
    positions = positions + normals * lift[:, None]
    gl.pull(sheets, positions)
    normals = gl.quad_normals(positions, quads)

    mesh = Mesh()
    atlas = Atlas(ATLAS)
    grids = []
    hollows = gl.cavity(positions, quads, normals)
    for sheet in sheets:
        fine = normals[np.maximum(sheet.ids, 0)]
        grids.append(mesh.add_sheet(sheet, fine))
        atlas.add(sheet, fine, hollows[np.maximum(sheet.ids, 0)])

    # ---- hems and the waist turned in: the cloth has a thickness wherever it ends
    cf, cb = m["cf"], m["cb"]
    for sheet in sheets:
        uv = sheet.uv()
        pts, ids = sheet.points, sheet.ids
        nrm = normals[np.maximum(ids, 0)]
        cols = sheet.columns
        bottom = pts[0, cols]
        inward = -nrm[0, cols]
        hem_rows = [bottom, bottom + inward * 0.0035 + [0, 0, -0.001], bottom + inward * 0.004 + [0, 0, 0.02]]
        hem_uv = [uv[0, cols], uv[2, cols], uv[8, cols]]
        mesh.strip(hem_rows, hem_uv, flip=not sheet.flip)
        cols = cols[(cols >= cf) & (cols <= cb)]
        topline = pts[-1, cols]
        inward = -nrm[-1, cols]
        inward[:, 2] = 0
        waist_rows = [topline, topline + inward * 0.004 + [0, 0, 0.001], topline + inward * 0.0045 + [0, 0, -0.03]]
        waist_uv = [uv[-1, cols], uv[-3, cols], uv[-10, cols]]
        mesh.strip(waist_rows, waist_uv, flip=sheet.flip)

    # ---- belt loops and the waist button, on the draped waistband
    loop_rect = spec["loop_rect"]

    def patch(u, v):
        return [loop_rect[0] + (loop_rect[2] - loop_rect[0]) * u, loop_rect[1] + (loop_rect[3] - loop_rect[1]) * v]
    band_low, band_high = m["rows"] - 1 - m["band"], m["rows"] - 1
    for sheet in sheets:
        nrm = normals[np.maximum(sheet.ids, 0)]
        for share in spec["loops"]:
            column = int(round(cf + (cb - cf) * share))
            if sheet.name == "right" and share >= 0.999:
                continue                                         # the centre back loop is one loop
            along = np.cross(nrm[band_high, column], [0, 0, 1.0])
            along /= np.linalg.norm(along)
            section = [(-0.0065, 0.0), (-0.005, 0.0035), (0.005, 0.0035), (0.0065, 0.0)]
            rows_p, rows_uv = [], []
            for k, (row, out) in enumerate(((band_low - 2, -0.001), (band_low + 1, 0.003), (band_high - 1, 0.003), (band_high + 1, -0.001))):
                row = min(row, band_high)
                p, nn = sheet.points[row, column], nrm[row, column]
                p = p + [0, 0, 0.004] if k == 3 else p
                rows_p.append([p + along * a + nn * (b + out) for a, b in section])
                rows_uv.append([patch(0.1 + 0.25 * j / 3, 0.05 + 0.9 * k / 3) for j in range(4)])
            mesh.strip(rows_p, rows_uv, flip=along @ np.cross([0, 0, 1.0], nrm[band_high, column]) > 0)
    # The button: a low dome on the centre front of the waistband.
    left = sheets[0]
    nrm = normals[np.maximum(left.ids, 0)]
    row = (band_low + band_high) // 2
    centre, out = left.points[row, cf] + [0.012, 0, 0], nrm[row, cf + 2]
    side = np.cross(out, [0, 0, 1.0])
    side /= np.linalg.norm(side)
    up = np.cross(side, out)
    rings = []
    ring_uv = []
    for radius, rise in ((0.0085, 0.0), (0.0085, 0.0025), (0.006, 0.0038), (0.0, 0.0042)):
        angle = np.linspace(0, 2 * np.pi, 11)
        rings.append(centre[None, :] + out * (rise + 0.002) + radius * (np.cos(angle)[:, None] * side + np.sin(angle)[:, None] * up))
        ring_uv.append([patch(0.7 + 0.2 * radius / 0.0085 * math.cos(a), 0.5 + 0.4 * radius / 0.0085 * math.sin(a)) for a in angle])
    mesh.strip(rings, ring_uv, flip=False)

    vertices, _, _, triangles = mesh.arrays()
    sides = np.where(vertices[:, 0] > 0.004, 1, np.where(vertices[:, 0] < -0.004, -1, 0))
    weights, shapes = gl.skin(vertices, triangles, ref, rounds=2, sides=sides)
    # The game's trousers have a shape per kind of shoe; "Boots" draws the hem in so it goes into the shaft.
    extra = []
    for shape in spec.get("shoe_shapes", ("Boots",)):
        deltas = gl.follow_shape(vertices, spec["fit"], shape, ref)
        if deltas is not None:
            extra.append((shape, deltas))
    stats = gl.export(spec["id"], mesh, weights, shapes, extra)
    textures, layers = spec.get("paint", paint_jeans)(spec, atlas, m)
    items = [{"id": "%s_%s" % (spec["id"], colour), "kind": "legs", "sex": spec["sex"], "mesh": spec["id"], "tier": spec["tier"],
              "layers": [layers[colour]], "stats": stats}
             for colour in layers]
    return items, textures


def column_distance(atlas, name, u):
    """Metres of cloth from the column line u (0..1 across the sheet), signed towards larger u; NaN outside."""
    inside = atlas.inside(name)
    ys, xs = np.nonzero(inside)
    x0, x1 = xs.min(), xs.max()
    x = int(round(x0 + u * (x1 - x0)))
    out = np.full(inside.shape, np.nan)
    out[inside] = (atlas.across - atlas.across[:, x:x + 1])[inside]
    return out


def paint_jeans(spec, atlas, m):
    """Denim: twill, seams and stitching where a pair of jeans has them, wear, the folds' shadows.
    Tinted: the base map carries wear, fade and twill (worn is light), one per wash (JEANS_WASHES); the cloth's colour
    is the tint, the stitching takes the red colour and rivets and button the blue."""
    size = atlas.size
    px = 1.0 / size
    across, up = atlas.across.copy(), atlas.up.copy()
    height = np.zeros((size, size))
    stitch = np.zeros((size, size))
    seam = np.zeros((size, size))
    wear = np.zeros((size, size))
    rivet = np.zeros((size, size))
    bandmask = np.zeros((size, size))
    cfu, cbu = m["cf"] / m["nf"], m["cb"] / m["nf"]
    covered = atlas.sheet >= 0
    for name in ("left", "right"):
        inside = atlas.inside(name)
        u, v = atlas.local(name)
        ys, xs = np.nonzero(inside)
        top = np.nanmax(np.where(inside, up, np.nan), axis=0, keepdims=True)       # the waist's height, per column
        down = top - up
        d_out = column_distance(atlas, name, 0.5)
        d_front = column_distance(atlas, name, cfu)
        d_back = column_distance(atlas, name, cbu)
        d_in = np.fmin(np.abs(column_distance(atlas, name, 0.0)), np.abs(column_distance(atlas, name, 1.0)))
        above = v > (m["leg"] + m["blend"]) / (m["rows"] - 1)                      # the hip part of the sheet
        with np.errstate(invalid="ignore"):
            # waistband: its two rows of stitching, and the step below it
            band = down < spec["waistband"]
            bandmask = np.where(inside & band, 1.0, bandmask)
            for at in (0.004, spec["waistband"] - 0.004):
                stitch = np.fmax(stitch, np.where(inside, gl.stitch_line(down - at, across), 0))
            seam = np.fmax(seam, np.where(inside, np.exp(-((down - spec["waistband"]) / 0.0012) ** 2), 0))
            # hem: one row of stitching, a centimetre up
            stitch = np.fmax(stitch, np.where(inside, gl.stitch_line(up - 0.012, across), 0))
            # outseam (pressed open, plain) and inseam (felled: a ridge with two rows of stitching)
            seam = np.fmax(seam, np.where(inside & ~band, np.exp(-(d_out / 0.0011) ** 2), 0))
            seam = np.fmax(seam, np.where(inside & ~above, np.exp(-(d_in / 0.0012) ** 2), 0))
            for at in (0.003, 0.009):
                stitch = np.fmax(stitch, np.where(inside & ~above, gl.stitch_line(d_in - at, up), 0))
            # centre front and centre back seams, top-stitched on the wearer's left
            for d_c, sign in ((d_front, 1.0), (d_back, -1.0)):
                seam = np.fmax(seam, np.where(inside & ~band & above, np.exp(-(d_c / 0.0012) ** 2), 0))
                if name == "left":
                    for at in (0.003, 0.009):
                        stitch = np.fmax(stitch, np.where(inside & ~band & above, gl.stitch_line(d_c * sign - at, up), 0))
            # the fly: a J of two rows of stitching beside the centre front
            if name == "left":
                fly_end = spec["waistband"] + 0.105
                for width in (0.034, 0.040):
                    j = np.where(down < fly_end, np.abs(d_front - width), np.abs(np.hypot(d_front, down - fly_end) - width))
                    j = np.where((d_front > 0) & (down < fly_end + width) & ~band, j, np.nan)
                    stitch = np.fmax(stitch, np.where(inside, np.nan_to_num(gl.stitch_line(j, up + d_front), nan=0.0), 0))
            # front pocket: a scooped opening from the waistband to the outseam, two rows of stitching, a rivet at each end
            open_w, open_h = spec["pocket_in"], spec["pocket_down"]
            ex, ey = (-d_out) / open_w, (down - spec["waistband"]) / open_h
            scoop = (np.hypot(ex, ey) - 1.0) * min(open_w, open_h)
            front_panel = (u > cfu) & (u < 0.5) & ~band & (ex > 0) & (ey > 0)
            for at in (0.0, 0.006):
                stitch = np.fmax(stitch, np.where(inside & front_panel, np.nan_to_num(gl.stitch_line(scoop + at, d_out + down), nan=0.0), 0))
            seam = np.fmax(seam, np.where(inside & front_panel, np.nan_to_num(np.exp(-((scoop - 0.002) / 0.0012) ** 2), nan=0.0), 0))
            for rx, ry in ((open_w, 0.004), (0.004, open_h)):
                rivet = np.fmax(rivet, np.where(inside & (u < 0.5), np.nan_to_num(np.exp(-(np.hypot(-d_out - rx, down - spec["waistband"] - ry) / 0.0028) ** 4), nan=0.0), 0))
            # back yoke: a seam slanting down to the centre back, two rows of stitching
            back_panel = (u > 0.5) & (u < cbu) & ~band
            reach = np.nanmax(np.where(inside & above, -d_back, np.nan))
            slope = np.clip(-d_back / max(reach, 1e-6), 0, 1)
            yoke = down - (spec["yoke_back"] + (spec["yoke_side"] - spec["yoke_back"]) * slope)
            seam = np.fmax(seam, np.where(inside & back_panel, np.nan_to_num(np.exp(-(yoke / 0.0012) ** 2), nan=0.0), 0))
            for at in (0.003, 0.009):
                stitch = np.fmax(stitch, np.where(inside & back_panel, np.nan_to_num(gl.stitch_line(yoke - at, d_back), nan=0.0), 0))
            # back pocket: a patch with a pointed bottom, stitched all round, an arc of stitching across it
            px_c, py_top, pw, ph = spec["back_pocket"]
            bx = (-d_back - px_c) / (pw / 2)                                       # -1..1 across the pocket
            by = (down - py_top) / ph                                              # 0 at its top, 1 at the point
            edge = np.fmax(np.fmax(np.abs(bx) * (1 + 0.08 * by) - 1, -by), by - (1 - 0.14 * np.abs(bx))) * (pw / 2)
            on_back = inside & back_panel
            for at in (-0.003, -0.009):
                stitch = np.fmax(stitch, np.where(on_back & (by > 0.02), np.nan_to_num(gl.stitch_line(edge - at, d_back + down), nan=0.0), 0))
            patch_h = np.where(on_back, np.nan_to_num(np.clip(-edge / 0.0012, 0, 1), nan=0.0), 0)
            arc = by - (0.42 + 0.16 * np.cos(bx * np.pi))
            stitch = np.fmax(stitch, np.where(on_back & (edge < -0.012), np.nan_to_num(gl.stitch_line(arc * ph, d_back), nan=0.0), 0))
            height += patch_h * 0.5
            # wear: the thigh fronts, the knees and the seat fade first
            rel = np.where(inside, v, 0)
            front_fade = np.exp(-((u - 0.27) / 0.13) ** 2) * (np.exp(-((rel - 0.62) / 0.16) ** 2) + 0.7 * np.exp(-((rel - 0.44) / 0.07) ** 2))
            seat_fade = np.exp(-((u - 0.74) / 0.11) ** 2) * np.exp(-((rel - 0.80) / 0.09) ** 2)
            wear = np.fmax(wear, np.where(inside, np.nan_to_num(front_fade + 0.8 * seat_fade, nan=0.0), 0))
    # the patch for loops and button: plain cloth, laid in metres
    x0, y0, x1, y1 = atlas.box(spec["loop_rect"])
    yy, xx = np.mgrid[y0:y1, x0:x1]
    across[y0:y1, x0:x1], up[y0:y1, x0:x1] = xx * px * 1.0, yy * px * 1.0
    loops = np.zeros((size, size), dtype=bool)
    loops[y0:y1, x0:x1] = True
    button = np.zeros((size, size))
    bu, bv = (xx - x0) / (x1 - x0), 1 - (yy - y0) / (y1 - y0)
    button[y0:y1, x0:x1] = np.clip(1.2 - np.hypot((bu - 0.7) / 0.21, (bv - 0.5) / 0.42), 0, 1) > 0.15
    loop_edge = np.zeros((size, size))
    loop_edge[y0:y1, x0:x1] = np.fmax(np.exp(-((bu - 0.13) / 0.012) ** 2), np.exp(-((bu - 0.32) / 0.012) ** 2)) * (bu < 0.45)
    dashes = np.zeros((size, size))
    dashes[y0:y1, x0:x1] = np.sin(yy * 0.9) > -0.3
    stitch = np.fmax(stitch, loop_edge * dashes)
    covered = covered | loops

    weave = gl.twill(across, up, spec["twill"])
    grain = gl.noise(size, 11, (1, 3))
    slub = gl.noise(size, 5, (0,))                                  # single threads catching the light
    wash = gl.blotches(size, 7, 96)
    seam = np.nan_to_num(seam)
    stitch = np.nan_to_num(stitch)
    height += weave * 0.30 + grain * 0.05 + seam * 0.9 - stitch * 0.5 + rivet * 1.0 + button * 1.5
    normal = atlas.finish_normal(height, spec["relief"] * size / 1024, spec.get("fold_gain", 1.0))      # painted at twice the size it ships at
    name = spec["id"]
    gl.save_image(name + "_n", normal * 0.5 + 0.5, spec["normal_size"])
    textures = [{"name": name + "_n", "file": "textures/%s_n.png" % name, "type": "normal", "maxSize": spec["normal_size"]}]

    folds = atlas.shade(spec.get("fold_shade", 1.0))
    # Pucker along the seams: denim fades in short bars across a seam.
    pucker = seam * (0.5 + 0.5 * np.sin((across + up) * 2 * np.pi / 0.009))
    metal = np.fmax(rivet, button)
    tinted = gl.Tinted(spec, covered)

    def wash_base(w):
        def make():
            fade = np.clip(w["fade"] * (wear * 0.75 + wash * 0.25 + pucker * 0.6 + bandmask * 0.1), 0, 1)
            show = np.clip(0.05 + 0.11 * weave + 0.06 * (slub > 1.4) + fade * 0.26, 0, 1)      # how much of the pale weft shows
            light = (1 + w["contrast"] * show) / (1 + w["contrast"] * 0.42)
            light = light * (1.0 + 0.05 * grain) * folds * (1 - 0.30 * np.clip(seam - pucker, 0, 1) * (1 - fade))
            light = np.clip(light, 0, 1)
            # Indigo fades towards grey, not towards a brighter blue: the unfaded cloth keeps more of its blue.
            shade = np.dstack([light ** e for e in w.get("hue", (1, 1, 1))])
            shade = shade * (1 - stitch[..., None] * 0.85) + 0.96 * stitch[..., None] * 0.85
            return shade * (1 - metal[..., None]) + 0.78 * metal[..., None]
        return make

    def mask():
        return np.dstack([stitch * 0.85 * (1 - metal), np.zeros((size, size)), metal])
    bases = {key: (key, wash_base(w), w.get("size", spec["colour_size"])) for key, w in JEANS_WASHES.items()}
    for colour_name, entry in colourways(spec):
        d = JEANS_COLOURS.get(entry.get("id", colour_name), {})
        cloth = np.array(d.get("cloth", entry["cloth"]))
        dark, blue = luminance(cloth) < 0.10, cloth[2] > 1.25 * cloth[0] and cloth[2] > 1.1 * cloth[1]
        key = d.get("wash", "raw" if dark else "washed" if blue and luminance(cloth) < 0.35 else "pale")
        tint = np.clip(cloth / tinted.mean(bases[key]), 0, 0.98)
        thread = d.get("thread", (0.78, 0.50, 0.18) if blue else tuple(np.clip(tint * 1.9 + 0.05, 0, 0.9)) if dark else tuple(tint * 0.72))
        steel = d.get("metal", (0.62, 0.42, 0.24) if blue else (0.25, 0.25, 0.26) if dark else (0.48, 0.48, 0.50))
        tinted.put(colour_name, bases[key], ("", mask, spec.get("mask_size", spec["colour_size"])), thread, tuple(tint), steel)
    return textures + tinted.textures, tinted.layers


def paint_chinos(spec, atlas, m):
    """Cotton or wool trousers: a fine twill, slanted front pockets, buttoned welt pockets behind, a fly, plain seams,
    and (suit trousers) a pressed crease down each leg. One cloth colour: a tint mask garment."""
    size = atlas.size
    px = 1.0 / size
    across, up = atlas.across.copy(), atlas.up.copy()
    height = np.zeros((size, size))
    stitch = np.zeros((size, size))
    seam = np.zeros((size, size))
    welt = np.zeros((size, size))
    button = np.zeros((size, size))
    crease = np.zeros((size, size))
    cfu, cbu = m["cf"] / m["nf"], m["cb"] / m["nf"]
    covered = atlas.sheet >= 0
    band_w = spec["waistband"]
    for name in ("left", "right"):
        inside = atlas.inside(name)
        u, v = atlas.local(name)
        top = np.nanmax(np.where(inside, up, np.nan), axis=0, keepdims=True)
        down = top - up
        d_out = column_distance(atlas, name, 0.5)
        d_front = column_distance(atlas, name, cfu)
        d_back = column_distance(atlas, name, cbu)
        d_in = np.fmin(np.abs(column_distance(atlas, name, 0.0)), np.abs(column_distance(atlas, name, 1.0)))
        above = v > (m["leg"] + m["blend"]) / (m["rows"] - 1)
        with np.errstate(invalid="ignore"):
            band = down < band_w
            if spec.get("band_stitch", True):
                for at in (0.003, band_w - 0.003):
                    stitch = np.fmax(stitch, np.where(inside, gl.stitch_line(down - at, across, 0.0006, 0.0028), 0))
            seam = np.fmax(seam, np.where(inside, np.exp(-((down - band_w) / 0.0012) ** 2), 0))
            if spec.get("hem_stitch", True):
                stitch = np.fmax(stitch, np.where(inside, gl.stitch_line(up - 0.022, across, 0.0006, 0.0028), 0))
            seam = np.fmax(seam, np.where(inside & ~band, np.exp(-(d_out / 0.0011) ** 2), 0) * 0.8)
            seam = np.fmax(seam, np.where(inside & ~above, np.exp(-(d_in / 0.0011) ** 2), 0) * 0.8)
            for d_c in (d_front, d_back):
                seam = np.fmax(seam, np.where(inside & ~band & above, np.exp(-(d_c / 0.0012) ** 2), 0))
            if name == "left":
                fly_end, width = band_w + 0.11, 0.036
                j = np.where(down < fly_end, np.abs(d_front - width), np.abs(np.hypot(d_front, down - fly_end) - width))
                j = np.where((d_front > 0) & (down < fly_end + width) & ~band, j, np.nan)
                stitch = np.fmax(stitch, np.where(inside, np.nan_to_num(gl.stitch_line(j, up + d_front, 0.0006, 0.0028), nan=0.0), 0))
            # front pocket: a slanted opening from the waistband to the outseam, its edge turned and stitched
            p_in, p_down = spec["pocket_in"], spec["pocket_down"]
            x, y = -d_out, down - band_w
            slant = (x / p_in + y / p_down - 1.0) * (p_in * p_down / math.hypot(p_in, p_down))
            front_panel = inside & (u > cfu) & (u < 0.5) & ~band & (x > 0) & (y > 0) & (y < p_down + 0.004)
            seam = np.fmax(seam, np.where(front_panel, np.nan_to_num(np.exp(-(slant / 0.0013) ** 2), nan=0.0), 0))
            stitch = np.fmax(stitch, np.where(front_panel, np.nan_to_num(gl.stitch_line(slant - 0.006, x + y, 0.0006, 0.0028), nan=0.0), 0))
            # back pocket: a welt slot with a button under its middle
            px_c, py, pw = spec["back_pocket"]
            bx, by = -d_back - px_c, down - py
            box = np.fmax(np.abs(bx) - pw / 2, np.abs(by) - 0.0045)
            on_back = inside & (u > 0.5) & (u < cbu) & ~band
            welt = np.fmax(welt, np.where(on_back, np.nan_to_num(np.clip(-box / 0.0012, 0, 1), nan=0.0), 0))
            stitch = np.fmax(stitch, np.where(on_back, np.nan_to_num(gl.stitch_line(box - 0.003, bx + by, 0.0006, 0.0028), nan=0.0), 0))
            button = np.fmax(button, np.where(on_back, np.nan_to_num(np.clip((0.0065 - np.hypot(bx, by - 0.016)) / 0.001, 0, 1), nan=0.0), 0))
            if spec.get("crease"):
                for at in (0.25, 0.75):
                    d = column_distance(atlas, name, at)
                    low = np.clip((v - 0.02) / 0.05, 0, 1) * np.clip(((m["leg"] + m["blend"] * 0.5) / (m["rows"] - 1) - v) / 0.08, 0, 1)
                    crease = np.fmax(crease, np.where(inside, np.nan_to_num(np.exp(-(d / 0.0022) ** 2) * low, nan=0.0), 0))
    x0, y0, x1, y1 = atlas.box(spec["loop_rect"])
    yy, xx = np.mgrid[y0:y1, x0:x1]
    across[y0:y1, x0:x1], up[y0:y1, x0:x1] = xx * px, yy * px
    bu, bv = (xx - x0) / (x1 - x0), 1 - (yy - y0) / (y1 - y0)
    button[y0:y1, x0:x1] = np.clip(1.2 - np.hypot((bu - 0.7) / 0.21, (bv - 0.5) / 0.42), 0, 1) > 0.15
    loop_edge = np.zeros((size, size))
    loop_edge[y0:y1, x0:x1] = np.fmax(np.exp(-((bu - 0.13) / 0.012) ** 2), np.exp(-((bu - 0.32) / 0.012) ** 2)) * (bu < 0.45)
    stitch = np.fmax(stitch, loop_edge * 0.8)
    covered = covered.copy()
    covered[y0:y1, x0:x1] = True

    weave = gl.twill(across, up, spec["twill"])
    grain = gl.noise(size, 51, (1, 3))
    wash = gl.blotches(size, 53, 128)
    seam, stitch = np.nan_to_num(seam), np.nan_to_num(stitch)
    holes = np.clip(button - 0.0, 0, 1) * (np.hypot(np.sin(across * 2 * np.pi / 0.004), np.sin(up * 2 * np.pi / 0.004)) < 0.5)
    height += weave * spec.get("weave_relief", 0.10) + grain * 0.04 + seam * 0.8 - stitch * 0.4 - welt * 0.7 + button * 1.3 - holes * 0.5 + crease * 0.9
    normal = atlas.finish_normal(height, spec["relief"] * size / 1024, spec.get("fold_gain", 1.0))
    name = spec["id"]
    gl.save_image(name + "_n", normal * 0.5 + 0.5, spec["normal_size"])
    textures = [{"name": name + "_n", "file": "textures/%s_n.png" % name, "type": "normal", "maxSize": spec["normal_size"]}]
    folds = atlas.shade(spec.get("fold_shade", 1.0))
    tinted = gl.Tinted(spec, covered)

    def make():
        shade = (0.955 + 0.03 * weave + 0.02 * grain + 0.03 * (wash - 0.5) * spec.get("wash", 1.0)) * folds
        shade = shade * (1 - 0.16 * seam) * (1 - 0.14 * stitch) * (1 - 0.35 * welt) * (1 + 0.05 * crease) * (1 - 0.35 * holes)
        return np.clip(shade, 0, 1)

    def buttons():
        return np.dstack([button, np.zeros((size, size)), np.zeros((size, size))])

    def pinstripe():
        # A chalk line every 14 mm, parallel to the crease of each half leg (so the lines meet at an angle in the seams,
        # as on cut cloth), and not on the waistband or the loops.
        line = np.zeros((size, size))
        for name in ("left", "right"):
            inside = atlas.inside(name)
            u, _ = atlas.local(name)
            top = np.nanmax(np.where(inside, atlas.up, np.nan), axis=0, keepdims=True)
            with np.errstate(invalid="ignore"):
                for at, half in ((0.25, u < 0.5), (0.75, u >= 0.5)):
                    d = np.nan_to_num(column_distance(atlas, name, at), nan=0.0)
                    line = np.fmax(line, np.where(inside & half & (top - atlas.up > band_w), bands(d, PINSTRIPE[0], PINSTRIPE[1], 0.0008), 0))
        return np.dstack([line * (1 - button), np.zeros((size, size)), button])
    base = ("", make)
    for colour_name, entry in colourways(spec):
        cloth = np.array(entry["cloth"]) * spec.get("tone", 1.0)
        knob = tuple(np.clip(cloth * 0.55 + 0.05, 0, 1))
        if entry.get("pattern") == "pinstripe":
            tinted.put(colour_name, base, ("pin", pinstripe, spec.get("stripe_size", 1024)), entry["second"], tuple(cloth), knob)
        else:
            tinted.put(colour_name, base, ("", buttons), knob, tuple(cloth), tuple(cloth))
    return textures + tinted.textures, tinted.layers


# ================================================================ tops


def unit(v):
    return v / np.linalg.norm(v)


def face_out(sheet, inside):
    """Sets the sheet's winding so that it faces away from the point `inside` (seen from the sheet's middle)."""
    i, j = sheet.points.shape[0] // 2, sheet.points.shape[1] // 2
    p = sheet.points
    normal = np.cross(p[i, j + 1] - p[i, j], p[i + 1, j] - p[i, j])
    sheet.flip = bool(normal @ (p[i, j] - inside) < 0)


def turn_in(mesh, sheet, normals, edge, columns=None, depth=0.02, thickness=0.0035):
    """The cloth turned in along a sheet's first or last row (edge 0 or -1): an opening with the cloth's thickness."""
    cols = sheet.columns if columns is None else columns
    uv, pts = sheet.uv(), sheet.points
    nrm = normals[np.maximum(sheet.ids, 0)]
    step = 1 if edge == 0 else -1
    rim = pts[edge, cols]
    away = rim - pts[edge + 3 * step, cols]
    away /= np.maximum(np.linalg.norm(away, axis=1, keepdims=True), 1e-9)
    inward = -nrm[edge, cols]
    rows = [rim, rim + inward * thickness + away * 0.001, rim + inward * (thickness + 0.0005) - away * depth]
    rows_uv = [uv[edge, cols], uv[edge + 2 * step, cols], uv[edge + 8 * step, cols]]
    mesh.strip(rows, rows_uv, flip=(edge == 0) != sheet.flip)


def cut_top(spec):
    """Front and back panels and two set-in sleeves, as fine sheets.

    A panel is one rectangle from the hem to the shoulder: straight rows up to the armpit; above it a patch whose
    edges are designed curves: the armhole (half of the arm's outline where the sleeve is set in) at each side, and
    along the top the shoulder seam, the neckline, the other shoulder seam. A sleeve is a tube whose first ring is
    that armhole and whose other rings follow the arm.
    """
    body = Surface(spec["body"])
    under = Surface(spec["under"])
    f = FINE
    C, n_low, n_up, n_sleeve = spec["columns"] * f, spec["low_rows"] * f, spec["up_rows"] * f, spec["sleeve_rows"] * f
    M = 2 * n_up
    shoulder, elbow = np.array(ba.bone_head("UpperArm.L")), np.array(ba.bone_head("LowerArm.L"))
    wrist = np.array(ba.bone_head("Hand.L"))
    axis = unit(elbow - shoulder)
    upper = np.linalg.norm(elbow - shoulder)
    mirror = np.array([-1.0, 1.0, 1.0])

    def arm(along):
        """A point on the arm's line and its direction there, `along` metres from the shoulder, round the elbow."""
        fore = unit(wrist - elbow)
        bend = np.clip((along - upper + 0.04) / 0.08, 0, 1)
        point = shoulder + axis * along if along <= upper else elbow + fore * (along - upper)
        return point, unit(axis * (1 - bend) + fore * bend)

    def outline(origin, normal, ease):
        """The arm's outline in a plane, from its underside (0) by the front to the top and round: M points, centre."""
        down = unit(np.array([0, 0, -1.0]) - normal * -normal[2])
        front = unit(np.cross(down, normal))
        if front[1] > 0:
            front = -front
        cut = body.cut(origin, normal)
        cut = cut[(np.linalg.norm(cut - origin, axis=2) < 0.14).all(axis=1)]
        centre = cut.reshape(-1, 3).mean(axis=0)
        centre = centre * 0.5 + (origin + axis * ((centre - origin) @ axis)) * 0.5
        flat = np.stack([(cut - centre) @ down, (cut - centre) @ front], axis=2)
        psi = 2 * np.pi * np.arange(M) / M
        rays = np.stack([np.cos(psi), np.sin(psi)], axis=1)
        r = gl.smooth1(gl.fill_nan(gl.ray_radius(flat, np.zeros(2), rays, far=False), True), 2, wrap=True) + ease(psi)
        return centre + r[:, None] * (rays[:, :1] * down + rays[:, 1:] * front), centre

    # ---- the armhole: the arm's outline in a plane leaning in under the arm, eased, and deepened at the armpit
    lean = spec["scye_lean"]
    scye_normal = unit(np.array([math.cos(lean), 0.0, -math.sin(lean)]))
    loop, _ = outline(shoulder + np.array(spec["scye_at"]), scye_normal,
                      lambda psi: spec["scye_ease"] + spec["scye_drop"] * np.clip(np.cos(psi), 0, 1) ** 2)
    armpit = loop[0]

    # ---- the trunk below the armpit: rings that fall from the chest instead of following the waist in
    hem = spec["hem"]
    levels = np.arange(hem - 0.01, armpit[2] + 0.011, 0.01)
    phi = 2 * np.pi * np.arange(96) / 96
    rays = np.stack([np.cos(phi), np.sin(phi)], axis=1)
    centre = np.array([0.0, spec["centre_y"]])
    table = []
    for z in levels:
        r = gl.fill_nan(gl.ray_radius(body.segments(2, z)[:, :, :2], centre, rays, limit=0.21), True)
        low = under.segments(2, z)[:, :, :2]
        if len(low) > 8:
            r = np.fmax(r, gl.fill_nan(gl.ray_radius(low, centre, rays, limit=0.3), True) + spec["over_ease"])
        table.append(r + spec["ease"](z))
    table = np.array(table)
    for i in range(len(levels) - 2, -1, -1):
        table[i] = np.maximum(table[i], table[i + 1] - spec["fall"] * 0.01)
    for i in range(1, len(levels)):
        table[i] = np.maximum(table[i], table[i - 1] - spec["flare"] * 0.01)
    table = gl.smooth1(gl.smooth1(table, 2).T, 3, wrap=True).T

    def half_ring(z, sign):
        """From the left side seam (+X) across the front (sign -1) or the back (+1) to the right side seam."""
        k = np.clip((z - levels[0]) / 0.01, 0, len(levels) - 1.001)
        r = table[int(k)] * (1 - (k - int(k))) + table[int(k) + 1] * (k - int(k))
        angle = np.linspace(0, np.pi, 97)
        radius = np.interp(sign * angle, phi, r, period=2 * np.pi)
        line = centre + radius[:, None] * np.stack([np.cos(angle), sign * np.sin(angle)], axis=1)
        return np.concatenate([gl.resample(line, C + 1), np.full((C + 1, 1), z)], axis=1)

    # ---- the neck: side neck point on the shoulder ridge, the front and back points on the centre line
    ridge = body.segments(0, spec["neck_half"])[:, :, 1:].reshape(-1, 2)
    ridge = ridge[(ridge[:, 1] < shoulder[2] + 0.09) & (np.abs(ridge[:, 0] - shoulder[1]) < 0.09)]
    top = ridge[np.argmax(ridge[:, 1])]
    neck_side = np.array([spec["neck_half"], top[0], top[1]])
    profile = body.segments(0, 0.003)[:, :, 1:].reshape(-1, 2)

    def centre_point(drop, sign):
        z = neck_side[2] - drop
        near = profile[np.abs(profile[:, 1] - z) < 0.015]
        return np.array([0.0, near[:, 0].min() if sign < 0 else near[:, 0].max(), z])

    def top_edge(sign, drop):
        mid = centre_point(drop, sign)
        cs = int(round(C / 2 * spec["shoulder_share"]))
        t = np.linspace(0, 1, cs + 1)[:, None]
        seam = loop[M // 2] * (1 - t) + neck_side * t
        a = np.linspace(0, np.pi / 2, C // 2 - cs + 1)[1:, None]
        neck = np.concatenate([neck_side[0] * np.cos(a), neck_side[1] + (mid[1] - neck_side[1]) * np.sin(a),
                               neck_side[2] + (mid[2] - neck_side[2]) * np.sin(a) ** spec.get("neck_round", 1.0)], axis=1)
        half = np.concatenate([seam, neck])
        half[cs:] = body.push_out(half[cs:], spec["neck_ease"])
        half[1:cs] = body.push_out(half[1:cs], spec["shoulder_ease"])
        half[-1, 0] = 0.0
        return np.concatenate([half, (half * mirror)[-2::-1]]), cs

    sheets = []
    for name, sign, drop, rect in (("front", -1, spec["neck_front"], spec["rects"]["front"]), ("back", 1, spec["neck_back"], spec["rects"]["back"])):
        points = np.zeros((n_low + n_up + 1, C + 1, 3))
        for i in range(n_low + 1):
            points[i] = half_ring(hem + (armpit[2] - hem) * i / n_low, sign)
        # the side seams run into the armpits
        shift = armpit - points[n_low, 0]
        w_col = np.clip(1 - np.arange(C + 1) / (C * 0.22), 0, 1) ** 2
        for i in range(n_low + 1):
            w_row = np.clip((i / n_low - 0.45) / 0.55, 0, 1) ** 2
            points[i] += w_row * (w_col[:, None] * shift + w_col[::-1, None] * shift * mirror)
        edge, cs = top_edge(sign, drop)
        side = loop[np.arange(n_up + 1)] if sign < 0 else loop[(M - np.arange(n_up + 1)) % M]
        patch = gl.coons(points[n_low], edge, side, side * mirror)
        patch[1:-1, 1:-1] = body.push_out(patch[1:-1, 1:-1], spec["upper_ease"])
        points[n_low:] = patch
        rows = list(range(0, n_low + n_up + 1, f)) + [1]
        sheet = Sheet(name, points, rect, rows, range(0, C + 1, f))
        face_out(sheet, np.array([0.0, spec["centre_y"], points[n_low // 2, 0, 2]]))
        sheets.append(sheet)

    marks = {"C": C, "low": n_low, "up": n_up, "M": M, "cs": cs, "sleeve": n_sleeve, "armpit": armpit[2]}
    if spec.get("sleeveless"):
        return sheets, marks, body, under

    # ---- the sleeves
    length = spec["sleeve_length"]
    points = np.zeros((n_sleeve + 1, M + 1, 3))
    points[0, :M] = loop
    for i in range(1, n_sleeve + 1):
        along = spec["sleeve_start"] + (length - spec["sleeve_start"]) * (i - 1) / (n_sleeve - 1)
        cuff = np.clip((along - (length - spec.get("cuff", 0.0))) / 0.012, 0, 1) if spec.get("cuff") else 0.0
        ring, mid = outline(*arm(along), lambda psi: spec["sleeve_ease"](along) + 0 * psi)
        spoke = ring - mid
        reach = np.linalg.norm(spoke, axis=1, keepdims=True)
        wide = np.maximum(reach, spec["sleeve_width"](along))
        points[i, :M] = mid + spoke / reach * (wide * (1 - cuff) + (reach - spec["sleeve_ease"](along) + spec.get("cuff_ease", 0.0)) * cuff)
    points[:, M] = points[:, 0]
    rows = list(range(0, n_sleeve + 1, f)) + [n_sleeve - 1]
    for name, flipx in (("sleeve_l", False), ("sleeve_r", True)):
        sheet = Sheet(name, points * mirror if flipx else points, spec["rects"][name], rows, range(0, M + 1, f))
        face_out(sheet, arm(length * 0.5)[0] * (mirror if flipx else 1.0))
        sheets.append(sheet)
    marks = {"C": C, "low": n_low, "up": n_up, "M": M, "cs": cs, "sleeve": n_sleeve, "armpit": armpit[2]}
    return sheets, marks, body, under


def top(spec):
    sheets, m, body, under = cut_top(spec)
    hem_z = spec["hem"]
    positions, quads = gl.weld(sheets)
    C, cs = m["C"], m["cs"]
    if DRAPE:
        # Hung from the neckline and the shoulder seams; a weak spring to the cut keeps the silhouette.
        pin = np.zeros(len(positions))
        grow = np.zeros(len(positions))
        for sheet in sheets:
            hold = np.full(sheet.points.shape[:2], spec["hold"])
            surplus = np.full(sheet.points.shape[:2], spec.get("surplus", 0.5))
            if sheet.name in ("front", "back"):
                hold[-1] = 1.0
                hold[-2, cs:C - cs + 1] = 1.0
                height = (np.arange(hold.shape[0]) / (hold.shape[0] - 1))[:, None]
                hold = np.maximum(hold, spec.get("shoulder_hold", 0.5) * np.clip((height - 0.8) / 0.2, 0, 1) ** 2)
                surplus[-m["up"] // 2:] = 0.0
                if spec.get("waist_seam"):
                    hold[0] = 1.0
                if spec.get("sleeveless"):                       # the armholes keep their drawn curve
                    hold[m["low"]:, [0, -1]] = 1.0
                    hold[m["low"]:, [1, -2]] = np.maximum(hold[m["low"]:, [1, -2]], 0.6)
                if spec.get("band"):
                    rows = int(round(spec["band"] / (m["armpit"] - hem_z) * m["low"]))
                    hold[:rows + 1] = 0.9
                    surplus[:rows + 1] = 0.0
            else:
                hold = np.maximum(hold, spec.get("sleeve_hold", 0.3))
                hold[0] = np.maximum(hold[0], 0.6)
                if spec.get("cuff"):
                    rows = int(round(spec["cuff"] / (spec["sleeve_length"] - spec["sleeve_start"]) * (m["sleeve"] - 1)))
                    hold[-rows - 1:] = 0.9
                    surplus[-rows - 1:] = 0.0
            pin[sheet.ids] = np.maximum(pin[sheet.ids], hold)
            grow[sheet.ids] = np.maximum(grow[sheet.ids], surplus)
        hips = under.vertices[under.vertices[:, 2] > hem_z - 0.12]
        bodies = [(body.vertices, body.triangles), gl.hulls(hips, slabs=4, axis=2, pair=False)]
        settled = gl.drape(positions, quads, pin, bodies, spec.get("frames", 60), spec.get("cloth"), grow=grow)
        positions = gl.relax(settled, quads, spec.get("relax", 3), fixed=pin >= 0.999)
        # Clear of the body with room for the game mesh's coarser facets: nothing of the skin left under it may show.
        cleared = body.push_out(positions, spec.get("clear", 0.006), rounds=1)
        if spec.get("skin_surface"):
            # Where the bare skin shown with this garment is a layer of its own (the swimsuit's torso lies over the
            # head mesh's bib), the nearest surface of the body is not always the outermost: clear that skin by name.
            cleared = Surface(spec["skin_surface"]).push_out(cleared, spec.get("clear", 0.006), rounds=2)
        kept = np.zeros(len(positions), dtype=bool)
        if spec.get("waist_seam"):
            for sheet in sheets[:2]:
                kept[sheet.ids[0]] = True                            # the seam a skirt hangs from stays as cut
            if not spec.get("sleeveless"):
                kept |= pin >= 0.999
        positions = np.where(kept[:, None], positions, cleared)
    if spec.get("yoke"):
        # Over the shoulders no further in than the game's own top there: under it lies the bib of the head's mesh,
        # which moves with the shoulders its own way, and the game's top is what is known to cover it in every pose.
        neck = np.zeros(len(positions), dtype=bool)
        for sheet in sheets[:2]:
            neck[sheet.ids[-4:, cs - 2:C - cs + 3]] = True       # the neckline keeps its drawn curve
        high = (positions[:, 2] > spec["yoke_from"]) & ~neck
        positions[high] = Surface(spec["yoke"]).push_out(positions[high], spec.get("yoke_ease", 0.0), rounds=1)
        positions = gl.relax(positions, quads, spec.get("yoke_relax", 1), fixed=neck | (positions[:, 2] < hem_z + 0.004))
    gl.pull(sheets, positions)
    normals = gl.quad_normals(positions, quads)

    mesh = Mesh()
    atlas = Atlas(ATLAS)
    hollows = gl.cavity(positions, quads, normals)
    for sheet in sheets:
        fine = normals[sheet.ids]
        mesh.add_sheet(sheet, fine)
        atlas.add(sheet, fine, hollows[sheet.ids])
    front, back = sheets[0], sheets[1]
    for sheet in sheets:
        if sheet in (front, back) and spec.get("waist_seam"):
            continue
        turn_in(mesh, sheet, normals, 0 if sheet in (front, back) else -1, depth=0.022)
    seam_line = np.concatenate([front.points[0], back.points[0][-2:0:-1]])
    rims = [seam_line]
    if spec.get("sleeveless"):
        # The armholes: the cloth turned in round each, a binding's width.
        n_low = m["low"]
        kept = [r for r in front.rows if r >= n_low]
        for column in (0, C):
            edge_ids = np.concatenate([front.ids[kept, column], back.ids[kept, column][-2::-1]])
            line = positions[edge_ids]
            rims.append(line)
            nrm = normals[edge_ids] / np.linalg.norm(normals[edge_ids], axis=1, keepdims=True)
            centre = line.mean(axis=0)
            hole = centre - line
            hole -= nrm * np.sum(hole * nrm, axis=1, keepdims=True)
            hole /= np.maximum(np.linalg.norm(hole, axis=1, keepdims=True), 1e-9)
            uv_f, uv_b = front.uv(), back.uv()
            step = 1 if column == 0 else -1
            uv_edge = np.concatenate([uv_f[kept, column], uv_b[kept, column][-2::-1]])
            uv_in = np.concatenate([uv_f[kept, column + 4 * step], uv_b[kept, column + 4 * step][-2::-1]])
            rows_p = [line, line - nrm * 0.003 + hole * 0.001, line - nrm * 0.0035 - hole * 0.014]
            rows_uv = [uv_edge, uv_edge * 0.6 + uv_in * 0.4, uv_in]
            rows_p = [np.concatenate([r, r[:1]]) for r in rows_p]
            rows_uv = [np.concatenate([r, r[:1]]) for r in rows_uv]
            mesh.strip(rows_p, rows_uv, outward=np.concatenate([nrm, nrm[:1]]))

    # ---- the collar: a band standing round the neckline, with an inner face (a rib or a binding by its paint)
    cols = front.columns[(front.columns >= cs) & (front.columns <= C - cs)]
    line = np.concatenate([front.points[-1, cols], back.points[-1, cols][-2::-1]])
    nrm = np.concatenate([normals[front.ids[-1, cols]], normals[back.ids[-1, cols][-2::-1]]])
    nrm /= np.linalg.norm(nrm, axis=1, keepdims=True)
    along = np.roll(line, -1, axis=0) - np.roll(line, 1, axis=0)
    rims.append(line.copy())
    rise = np.cross(along, nrm)
    rise /= np.linalg.norm(rise, axis=1, keepdims=True)
    rise *= np.sign(rise[:, 2:3] + 1e-9)
    rise = unit(np.array([0, 0, 1.0]))[None, :] * spec["collar_stand"] + rise * (1 - spec["collar_stand"])
    width = spec["collar"]
    rings = [line - rise * 0.004 + nrm * 0.0025, line + rise * width * 0.5 + nrm * 0.0035, line + rise * width + nrm * 0.002,
             line + rise * width - nrm * 0.003, line + rise * width * 0.2 - nrm * 0.004]
    for k in (1, 2, 3, 4):
        rings[k] = body.push_out(rings[k], 0.0025)
        if spec.get("skin_surface"):
            rings[k] = Surface(spec["skin_surface"]).push_out(rings[k], 0.0025)
    u0, v0, u1, v1 = spec["rects"]["collar"]
    count = len(line)
    ring_uv = [[[u0 + (u1 - u0) * j / count, v0 + (v1 - v0) * share] for j in range(count + 1)] for share in (0.0, 0.3, 0.6, 0.75, 1.0)]
    rings = [np.concatenate([r, r[:1]]) for r in rings]
    away = (line - line.mean(axis=0)) * np.array([1.0, 1.0, 0.0])      # the cloth's own normals lie too flat at a neck's rim to tell
    mesh.strip(rings, ring_uv, outward=np.concatenate([away, away[:1]]))

    if spec.get("zip_pull"):
        # The zip's slider and its pull tab, hanging from the top of the zip under the collar: two small boxes.
        row, c = front.points.shape[0] - 3, C // 2
        p, n = front.points[row, c], unit(normals[front.ids[row, c]])
        down = unit(front.points[row - 6, c] - p)
        side = unit(np.cross(down, n))
        n = unit(np.cross(side, down))
        u0, v0, u1, v1 = spec["zip_pull"]
        for a, b, half_w, half_t, lift in ((0.002, 0.014, 0.0048, 0.0022, 0.0035), (0.012, 0.036, 0.0032, 0.0010, 0.0050)):
            rows_p, rows_uv = [], []
            for k, (along, scale) in enumerate(((a, 0.0), (a, 1.0), (b, 1.0), (b, 0.0))):
                q = p + down * along + n * lift
                rows_p.append([q + n * half_t * t * scale + side * half_w * w * scale for w, t in ((-1, -1), (1, -1), (1, 1), (-1, 1), (-1, -1))])
                rows_uv.append([[u0 + (u1 - u0) * (0.1 + 0.8 * j / 4), v0 + (v1 - v0) * (0.1 + 0.8 * k / 3)] for j in range(5)])
            mesh.strip(rows_p, rows_uv, flip=True)

    vertices, _, _, triangles = mesh.arrays()
    weights, shapes = gl.skin(vertices, triangles, Surface(spec["weights"]), rounds=spec.get("skin_rounds", 2))
    if spec.get("waist_seam"):
        # What hangs from this seam (a skirt) is built to it: the line, and the weights and body shapes along it.
        on_seam = np.array([int(np.argmin(np.linalg.norm(vertices - p, axis=1))) for p in seam_line])
        np.savez(os.path.join(PARTIAL, spec["id"] + "_waist.npz"), line=seam_line, weights=weights[on_seam],
                 **{n: shapes[n][on_seam] for n in gl.BODY_SHAPES})
    stats = gl.export(spec["id"], mesh, weights, shapes)
    textures, layers = spec["paint"](spec, atlas, m)
    items = [{"id": "%s_%s" % (spec["id"], colour), "kind": "top", "sex": spec["sex"], "mesh": spec["id"], "tier": spec["tier"],
              "skinFrom": spec["skin_from"], "layers": [layers[colour]], "stats": stats}
             for colour in layers]
    if spec.get("skin_hide"):
        hide = gl.skin_hide(vertices, triangles, *spec["skin_hide"], margin=spec.get("hide_margin", 0.008), rim=np.concatenate(rims[1:]),
                            below=(hem_z + 0.004) if spec.get("waist_seam") else None)
        for item in items:
            item["skinHide"] = hide
    return items, textures


def edge_fields(atlas, name):
    """Per texel of a sheet: metres of cloth from its bottom, top, left and right edges (NaN outside)."""
    inside = atlas.inside(name)
    across = np.where(inside, atlas.across, np.nan)
    up = np.where(inside, atlas.up, np.nan)
    with np.errstate(all="ignore"):
        return inside, up, np.nanmax(up, axis=0, keepdims=True) - up, across, np.nanmax(across, axis=1, keepdims=True) - across


def paint_knit_top(spec, atlas, m):
    """A jersey top: fine knit, seams at the sides, shoulders and armholes, twin-needle hems, a ribbed collar."""
    size = atlas.size
    across, up = atlas.across.copy(), atlas.up.copy()
    seam = np.zeros((size, size))
    stitch = np.zeros((size, size))
    ribbed = np.zeros((size, size))
    sleeves = np.zeros((size, size))                      # for the two-tone entries: the sleeves, and the bands at their hems
    cuffs = np.zeros((size, size))
    level = np.zeros((size, size))                        # for level stripes
    print_a, print_b = atlas.flat_across.copy(), atlas.flat_up.copy()      # for dots and checks
    covered = atlas.sheet >= 0
    share_up = m["low"] / (m["low"] + m["up"])
    cs_share = m["cs"] / m["C"]
    for name in atlas.names:
        inside, bottom, top, left, right = edge_fields(atlas, name)
        u, v = atlas.local(name)
        with np.errstate(invalid="ignore"):
            hem = 0.022
            for at in (hem, hem - 0.005):
                if "sleeve" in name or not spec.get("waist_seam"):
                    stitch = np.fmax(stitch, np.nan_to_num(gl.stitch_line((bottom if "sleeve" not in name else top) - at, across, 0.0006, 0.0026)))
            if "sleeve" in name:
                seam = np.fmax(seam, np.nan_to_num(np.exp(-(bottom / 0.0014) ** 2)))                    # set into the armhole
                seam = np.fmax(seam, np.nan_to_num(np.exp(-(np.fmin(left, right) / 0.0012) ** 2)))       # under the arm
                sleeves = np.where(inside, 1.0, sleeves)
                # A print on a sleeve is laid from its hem: round by the hem's own girth (the armhole, its widest row, is
                # wider and would squeeze the print), up by each column's own length (the cap's columns differ).
                rows_in = np.nonzero(inside.any(axis=1))[0]
                print_a = np.where(inside, atlas.across[rows_in.min() + 2][None, :], print_a)
                print_b = np.where(inside, np.nan_to_num(top), print_b)
                cuffs = np.fmax(cuffs, np.nan_to_num(np.clip((hem - top) / 0.0008 + 0.5, 0, 1)))
                # Stripes go round a sleeve, counted from its hem (the sheet's rows, so they are rings); the cap, where the
                # sleeve is set into the armhole, stays plain like the yoke.
                from_hem = np.max(np.where(inside, atlas.flat_up, 0)) - atlas.flat_up
                reach = np.max(np.where(inside & (v > 2.0 / m["sleeve"]), from_hem, 0)) + 0.004
                whole = from_hem + 0.004 < math.floor(reach / STRIPE[0]) * STRIPE[0]          # no stripe cut off by the cap
                level = np.where(inside & whole, bands(from_hem + 0.004, STRIPE[0], STRIPE[1]), level)
            else:
                # On the body stripes run level: across the height the cloth was cut at, so front and back meet in the
                # side seams and a skirt hung from this bodice carries them on. The yoke stays plain, as on a Breton
                # shirt (and because cloth lying over the shoulders would draw them wide).
                low = atlas.level < math.floor((m["armpit"] + spec.get("stripe_yoke", 0.085)) / STRIPE[0]) * STRIPE[0]      # whole stripes only
                level = np.where(inside & low, bands(atlas.level, STRIPE[0], STRIPE[1]), level)
                side = np.fmin(left, right)
                seam = np.fmax(seam, np.nan_to_num(np.exp(-(side / 0.0013) ** 2)))                       # side seam and armhole
                shoulder = (np.abs(u - 0.5) > 0.5 - cs_share / 2)
                seam = np.fmax(seam, np.nan_to_num(np.where(shoulder, np.exp(-(top / 0.0013) ** 2), 0)))
                stitch = np.fmax(stitch, np.nan_to_num(np.where((v > share_up) | shoulder, gl.stitch_line(np.fmin(side, np.where(shoulder, top, 1)) - 0.004, up + across, 0.0006, 0.0026), 0)) * 0.6)
    x0, y0, x1, y1 = atlas.box(spec["rects"]["collar"])
    yy, xx = np.mgrid[y0:y1, x0:x1]
    across[y0:y1, x0:x1], up[y0:y1, x0:x1] = xx / size * spec["collar_length"], (y1 - yy) / (y1 - y0) * spec["collar"] * 1.6
    ribbed[y0:y1, x0:x1] = 1.0
    covered[y0:y1, x0:x1] = True

    knit = gl.jersey(across, up, spec["knit"])
    ribs = gl.rib(across, spec.get("rib", 0.0028))
    grain = gl.noise(size, 21, (1, 3))
    fleck = gl.noise(size, 23, (0, 1))
    height = (knit * 0.10 * (1 - ribbed) + ribs * 0.8 * ribbed + grain * 0.04 + seam * 0.7 - stitch * 0.35)
    normal = atlas.finish_normal(height, spec["relief"] * size / 1024, spec.get("fold_gain", 1.0))
    name = spec["id"]
    gl.save_image(name + "_n", normal * 0.5 + 0.5, spec["normal_size"])
    textures = [{"name": name + "_n", "file": "textures/%s_n.png" % name, "type": "normal", "maxSize": spec["normal_size"]}]
    folds = atlas.shade(spec.get("fold_shade", 1.0))
    tinted = gl.Tinted(spec, covered)

    def shade(heather=0.0):
        def make():
            light = (0.97 + 0.03 * knit) * (1 - ribbed) + (0.80 + 0.20 * ribs) * ribbed
            light = light * (1.0 + 0.03 * grain) * folds * (1 - 0.10 * seam) * (1 - 0.12 * stitch)
            return light * (1.0 + heather * np.clip(fleck, -2, 2)) if heather else light
        return make
    masks = print_masks(spec, (print_a, print_b), level, (sleeves, np.fmax(ribbed, cuffs)), blank=ribbed)
    for colour_name, entry in colourways(spec):
        # A dress in two colours is its bodice in one and its skirt in the other: no mask.
        mask, red, green, blue = print_tints(entry, masks, entry["cloth"] if spec.get("waist_seam") else None)
        base = ("heather", shade(0.10)) if entry.get("pattern") == "heather" else ("", shade())
        tinted.put(colour_name, base, mask, red, green, blue)
    return textures + tinted.textures, tinted.layers


def paint_bomber(spec, atlas, m):
    """A flight jacket: a smooth close weave, ribbed collar, cuffs and hem band, a front zip under two rows of
    stitching, slanted welt pockets, seams at the shoulders, armholes, sides and under the sleeves."""
    size = atlas.size
    across, up = atlas.across.copy(), atlas.up.copy()
    seam = np.zeros((size, size))
    stitch = np.zeros((size, size))
    ribbed = np.zeros((size, size))
    teeth = np.zeros((size, size))
    tape = np.zeros((size, size))
    welt = np.zeros((size, size))
    covered = atlas.sheet >= 0
    cs_share = m["cs"] / m["C"]
    for name in atlas.names:
        inside, bottom, top, left, right = edge_fields(atlas, name)
        u, v = atlas.local(name)
        with np.errstate(invalid="ignore"):
            if "sleeve" in name:
                ribbed = np.fmax(ribbed, np.nan_to_num(np.clip((spec["cuff"] - top) / 0.002, 0, 1)))
                seam = np.fmax(seam, np.nan_to_num(np.exp(-((top - spec["cuff"]) / 0.0014) ** 2)))
                seam = np.fmax(seam, np.nan_to_num(np.exp(-(bottom / 0.0016) ** 2)))
                seam = np.fmax(seam, np.nan_to_num(np.exp(-(np.fmin(left, right) / 0.0013) ** 2)))
                stitch = np.fmax(stitch, np.nan_to_num(gl.stitch_line(bottom - 0.006, across, 0.0007, 0.003)))
            else:
                band = spec["band"]
                ribbed = np.fmax(ribbed, np.nan_to_num(np.clip((band - bottom) / 0.002, 0, 1)))
                seam = np.fmax(seam, np.nan_to_num(np.exp(-((bottom - band) / 0.0014) ** 2)))
                side = np.fmin(left, right)
                seam = np.fmax(seam, np.nan_to_num(np.exp(-(side / 0.0014) ** 2)))
                shoulder = np.abs(u - 0.5) > 0.5 - cs_share / 2
                seam = np.fmax(seam, np.nan_to_num(np.where(shoulder, np.exp(-(top / 0.0014) ** 2), 0)))
                stitch = np.fmax(stitch, np.nan_to_num(np.where(bottom > band, gl.stitch_line(side - 0.006, up, 0.0007, 0.003), 0)))
                if name == "front":
                    d = column_distance(atlas, name, 0.5)
                    teeth = np.fmax(teeth, np.nan_to_num(np.clip(1 - np.abs(d) / 0.0035, 0, 1) * (np.sin(up * 2 * np.pi / 0.0026) > 0)))
                    tape = np.fmax(tape, np.nan_to_num(np.clip(1.4 - np.abs(d) / 0.007, 0, 1)))
                    for at in (0.011, 0.016):
                        stitch = np.fmax(stitch, np.nan_to_num(gl.stitch_line(np.abs(d) - at, up, 0.0007, 0.003)))
                    seam = np.fmax(seam, np.nan_to_num(np.exp(-((np.abs(d) - 0.0085) / 0.0012) ** 2)))
                    # welt pockets: a slanted slot on each side, stitched round
                    px, py, length, slant = spec["pocket"]
                    lx, ly = np.abs(d) - px, bottom - py
                    along_p = lx * math.sin(slant) + ly * math.cos(slant)
                    cross_p = lx * math.cos(slant) - ly * math.sin(slant)
                    box = np.fmax(np.abs(along_p) - length / 2, np.abs(cross_p) - 0.009)
                    welt = np.fmax(welt, np.nan_to_num(np.clip(-box / 0.0015, 0, 1)))
                    stitch = np.fmax(stitch, np.nan_to_num(gl.stitch_line(box - 0.003, along_p + cross_p, 0.0007, 0.003)))
    x0, y0, x1, y1 = atlas.box(spec["rects"]["collar"])
    yy, xx = np.mgrid[y0:y1, x0:x1]
    across[y0:y1, x0:x1], up[y0:y1, x0:x1] = xx / size * spec["collar_length"], (y1 - yy) / (y1 - y0) * spec["collar"] * 1.6
    ribbed[y0:y1, x0:x1] = 1.0
    covered[y0:y1, x0:x1] = True

    ribs = gl.rib(across, spec.get("rib", 0.0042))
    weave = 0.5 + 0.25 * np.sin(across * 2 * np.pi / 0.0016) + 0.25 * np.sin(up * 2 * np.pi / 0.0016)
    grain = gl.noise(size, 31, (1, 3))
    crinkle = gl.noise(size, 33, (4, 6))                        # nylon's fine creasing
    plain = 1 - ribbed
    height = (weave * 0.04 + crinkle * 0.025) * plain + ribs * 0.9 * ribbed + seam * 0.8 - stitch * 0.4 + teeth * 1.6 - welt * 0.6
    normal = atlas.finish_normal(height, spec["relief"] * size / 1024, spec.get("fold_gain", 1.0))
    name = spec["id"]
    gl.save_image(name + "_n", normal * 0.5 + 0.5, spec["normal_size"])
    textures = [{"name": name + "_n", "file": "textures/%s_n.png" % name, "type": "normal", "maxSize": spec["normal_size"]}]
    folds = atlas.shade(spec.get("fold_shade", 1.0))
    tinted = gl.Tinted(spec, covered)
    # The zip: a metal strip up the middle of its tape; the teeth are its relief (normal map) and the gaps between them.
    chain = np.clip(tape * 2.2 - 1.2, 0, 1)
    pull = np.zeros((size, size))
    if spec.get("zip_pull"):
        x0, y0, x1, y1 = atlas.box(spec["zip_pull"])
        pull[y0:y1, x0:x1] = 1.0
        covered[y0:y1, x0:x1] = True
        chain = np.fmax(chain, pull)
        teeth = np.fmax(teeth, pull)

    def make():
        shade = (1.0 + 0.04 * grain + 0.03 * crinkle) * folds * (1 - 0.18 * seam) * (1 - 0.25 * welt) * (1 - 0.2 * stitch)
        shade = np.where(ribbed > 0.5, (0.72 + 0.28 * ribs) * folds, shade)
        shade = shade * (1 - 0.45 * tape * (1 - chain)) * (1 - chain * 0.55 * (1 - teeth))
        return np.clip(shade, 0, 1)
    ribs_on = np.where(ribbed > 0.5, 1.0, 0.0)
    sleeves = np.zeros((size, size))
    for name in atlas.names:
        if "sleeve" in name:
            sleeves = np.where(atlas.inside(name), 1.0, sleeves)

    def zip_and_ribs():
        return np.dstack([chain, np.zeros((size, size)), ribs_on * (1 - chain)])

    def sleeves_and_ribs():                                  # a jacket in two colours: the sleeves; ribbing and zip go together
        trim = np.fmax(ribs_on, chain)
        return np.dstack([sleeves * (1 - trim), np.zeros((size, size)), trim])
    base, plain = ("", make), ("", zip_and_ribs, spec.get("mask_size", spec["colour_size"]))
    for colour_name, entry in colourways(spec):
        cloth = np.array(entry["cloth"])
        metal = (0.35, 0.35, 0.36) if luminance(cloth) < 0.02 else (0.60, 0.50, 0.32) if cloth[0] > 1.3 * cloth[2] and luminance(cloth) > 0.2 else (0.55, 0.55, 0.56)
        if entry.get("pattern") == "block":                  # the body in the darker colour, the sleeves in the lighter: a varsity jacket
            body, arm = sorted((entry["cloth"], entry["second"]), key=luminance)
            tinted.put(colour_name, base, ("parts", sleeves_and_ribs, spec.get("mask_size", spec["colour_size"])), arm, body, tuple(np.array(body) * 0.85))
        elif entry.get("pattern") == "trim":
            tinted.put(colour_name, base, plain, metal, tuple(cloth), entry["second"])
        else:
            tinted.put(colour_name, base, plain, metal, tuple(cloth), tuple(cloth * 0.85))
    return textures + tinted.textures, tinted.layers


STRIPE = (0.022, 0.009)        # level stripes: one every 22 mm, 9 mm wide
PINSTRIPE = (0.018, 0.0012)    # a chalk line every 18 mm


# ================================================================ skirts


def skirt(spec):
    """A flared skirt hung from a bodice's waist seam: one sheet all the way round, from the hem up to the seam.
    Its top ring IS the bodice's hem as built (read from the bodice's build), so dress and skirt meet without a gap."""
    seam = np.load(os.path.join(PARTIAL, spec["bodice"] + "_waist.npz"))
    waist = seam["line"]
    body = Surface(spec["body"])
    f = FINE
    count = len(waist)
    rows = spec["rows"] * f
    centre = np.array([0.0, spec["centre_y"]])
    spoke = waist[:, :2] - centre
    reach = np.linalg.norm(spoke, axis=1)
    spoke /= reach[:, None]
    hem = spec["hem"]
    points = np.zeros((rows + 1, count + 1, 3))
    for i in range(rows + 1):
        share = i / rows                                           # 0 at the hem, 1 at the waist seam
        z = hem + (waist[:, 2] - hem) * share
        drop = waist[:, 2] - z
        flare = reach + spec["flare"](drop)
        cut = body.segments(2, float(z.mean()))[:, :, :2]
        if len(cut) > 8:
            flare = np.maximum(flare, gl.fill_nan(gl.ray_radius(cut, centre, spoke, limit=0.3), True) + spec["ease"])
        points[i, :count] = np.concatenate([centre + spoke * flare[:, None], z[:, None]], axis=1)
    points[-1, :count] = waist
    points[:, count] = points[:, 0]
    sheet = Sheet("skirt", points, (0.0, 0.0, 1.0, 1.0), list(range(0, rows + 1, f)) + [1], range(0, count + 1, f))
    face_out(sheet, np.array([0.0, spec["centre_y"], points[rows // 2, 0, 2]]))
    sheets = [sheet]
    positions, quads = gl.weld(sheets)
    if DRAPE:
        # Hung from the seam; it falls into its own flutes, a weak spring to the cut keeping the bell.
        hold = np.full(points.shape[:2], spec["hold"])
        hold[-1] = 1.0
        hold[-2] = 0.6
        surplus = np.broadcast_to(np.linspace(1.0, 0.0, rows + 1)[:, None] ** 0.5, hold.shape)
        pin, grow = np.zeros(len(positions)), np.zeros(len(positions))
        pin[sheet.ids] = np.maximum(pin[sheet.ids], hold)
        grow[sheet.ids] = np.maximum(grow[sheet.ids], surplus)
        settled = gl.drape(positions, quads, pin, [(body.vertices, body.triangles)], spec.get("frames", 70), spec.get("cloth"), grow=grow)
        positions = gl.relax(settled, quads, spec.get("relax", 3), fixed=pin >= 0.999)
    gl.pull(sheets, positions)
    normals = gl.quad_normals(positions, quads)
    mesh, atlas = Mesh(), Atlas(ATLAS)
    hollows = gl.cavity(positions, quads, normals)
    mesh.add_sheet(sheet, normals[sheet.ids])
    atlas.add(sheet, normals[sheet.ids], hollows[sheet.ids])
    turn_in(mesh, sheet, normals, 0, depth=0.03)
    # A skirt is seen from inside when it swings: an inner face, a cloth's thickness in.
    inner = Sheet("lining", sheet.points - normals[sheet.ids] * 0.0025, sheet.rect, sheet.rows, sheet.columns, flip=not sheet.flip)
    inner.ids = sheet.ids
    mesh.add_sheet(inner, -normals[sheet.ids])
    vertices, _, _, triangles = mesh.arrays()
    weights, shapes = gl.skin(vertices, triangles, Surface(spec["weights"]), rounds=spec.get("skin_rounds", 4))
    # At the seam exactly as the bodice is skinned, so the two never part; its own way a hand's width below.
    gap = np.linalg.norm(vertices[:, None, :] - waist[None, :, :], axis=2)
    nearest = np.argmin(gap, axis=1)
    own = np.clip((waist[nearest, 2] - vertices[:, 2] - 0.004) / 0.09, 0, 1)[:, None]
    weights = weights * own + seam["weights"][nearest] * (1 - own)
    shapes = {n: shapes[n] * own + seam[n][nearest] * (1 - own) for n in gl.BODY_SHAPES}
    stats = gl.export(spec["id"], mesh, weights, shapes)

    size = atlas.size
    inside, bottom, top, left, right = edge_fields(atlas, "skirt")
    with np.errstate(invalid="ignore"):
        stitch = np.nan_to_num(gl.stitch_line(bottom - 0.025, atlas.across, 0.0006, 0.0026))
        u, _ = atlas.local("skirt")
        seam = np.nan_to_num(np.exp(-(top / 0.0016) ** 2))
        for at in (0.25, 0.75):                                    # side seams
            seam = np.fmax(seam, np.nan_to_num(np.exp(-(column_distance(atlas, "skirt", at) / 0.0012) ** 2)))
    weave = 0.5 + 0.25 * np.sin(atlas.across * 2 * np.pi / 0.0018) + 0.25 * np.sin(atlas.up * 2 * np.pi / 0.0018)
    # A flared skirt's panels are sectors of a ring: the print is laid on them flat (round the cone's apex), one
    # panel in front and one behind, so its dots stay round all the way down.
    across_f, up_f = sheet.metric()
    girth = across_f[-1, -1]
    slant = up_f[-1].mean()
    apex = girth * slant / max(across_f[0, -1] - girth, 1e-6)
    radius = apex + (up_f[-1:] - up_f)
    x0, y0, x1, y1 = atlas.box(sheet.rect)
    # Per texel, not resampled from the panel's grid: the two panels meet in a sharp line at the side seams.
    round_ = gl._resize(np.ascontiguousarray(np.broadcast_to(across_f[-1][None, :] / girth, radius.shape)[::-1]), x1 - x0, y1 - y0)
    radius = gl._resize(np.ascontiguousarray(radius[::-1]), x1 - x0, y1 - y0)
    along = (round_ + 0.25) % 0.5 - 0.25
    print_x, print_y = radius * np.sin(along * girth / apex), radius * np.cos(along * girth / apex)
    grain = gl.noise(size, 41, (1, 3))
    normal = atlas.finish_normal(weave * 0.05 + grain * 0.04 + seam * 0.6 - stitch * 0.3, spec["relief"] * size / 1024, spec.get("fold_gain", 1.0))
    name = spec["id"]
    gl.save_image(name + "_n", normal * 0.5 + 0.5, spec["normal_size"])
    textures = [{"name": name + "_n", "file": "textures/%s_n.png" % name, "type": "normal", "maxSize": spec["normal_size"]}]
    folds = atlas.shade(spec.get("fold_shade", 1.0))
    tinted = gl.Tinted(spec)
    items = []

    def shade():
        return (0.97 + 0.03 * weave) * (1.0 + 0.03 * grain) * folds * (1 - 0.10 * seam) * (1 - 0.12 * stitch)
    # Stripes run level, across the height the cloth was cut at: the bodice's go on down the skirt. The trim of a
    # two-tone dress is a band at the hem and a piping in the waist seam (the texture repeats, so its top row is next
    # to its bottom row: both ends are trim).
    level = bands(atlas.level, STRIPE[0], STRIPE[1])
    hem_band = np.nan_to_num(np.clip((spec.get("hem_band", 0.04) - bottom) / 0.0008 + 0.5, 0, 1))
    hem_band = np.fmax(hem_band, np.nan_to_num(np.clip((0.005 - top) / 0.0008 + 0.5, 0, 1)))
    masks = print_masks(spec, (print_x, print_y), level, (np.zeros((size, size)), hem_band))
    for colour_name, entry in colourways(spec):
        # A dress in two colours: the bodice has the first, the skirt the second.
        mask, red, green, blue = print_tints(entry, masks, entry.get("second"))
        tinted.put(colour_name, ("", shade), mask, red, green, blue)
        items.append({"id": "%s_%s" % (name, colour_name), "kind": "legs", "sex": spec["sex"], "mesh": name, "tier": spec["tier"],
                      "skinFrom": spec["skin_from"], "layers": [tinted.layers[colour_name]], "stats": stats})
    if spec.get("skin_hide"):
        # A long skirt: the legs well above its hem are not drawn (a knee would come through it at a run).
        hide = gl.skin_hide(vertices, triangles, *spec["skin_hide"], margin=spec.get("hide_margin", 0.12), reach=0.4,
                            above=float(vertices[:, 2].min()) + spec.get("hide_margin", 0.12) + 0.03)
        for item in items:
            item["skinHide"] = hide
    return items, textures + tinted.textures


def tube_skirt(spec):
    """A straight skirt on a waistband of its own (a pencil skirt): one sheet round the hips from the hem up, cut to
    the game's own skirt where that has cloth (hips, seat, the line of its waist) and tapering below it. Column 0 is the
    centre back: the zip above and the vent below are there."""
    ref = Surface([(spec["fit"], [0])])
    body = Surface(spec["body"])
    f = FINE
    count = spec["columns"] * f
    rows = spec["rows"] * f
    centre = np.array([0.0, spec["centre_y"]])
    phi = np.pi / 2 + 2 * np.pi * np.arange(count) / count           # from the centre back (+Y) round by the left side
    rays = np.stack([np.cos(phi), np.sin(phi)], axis=1)
    levels = np.arange(float(ref.vertices[:, 2].min()) + 0.004, float(ref.vertices[:, 2].max()) + 0.004, 0.004)
    table = np.array([gl.ray_radius(ref.segments(2, z)[:, :, :2], centre, rays) for z in levels])
    reached = ~np.isnan(table)
    top_level = np.array([levels[np.nonzero(reached[:, k])[0].max()] for k in range(count)])
    top_level = np.minimum(gl.smooth1(top_level, 6, wrap=True) - 0.003, spec["waist"])
    for k in range(count):
        good = np.nonzero(reached[:, k])[0]
        table[:, k] = np.interp(np.arange(len(levels)), good, table[good, k])
    table = gl.smooth1(gl.smooth1(table, 2).T, 3, wrap=True).T
    hem = spec["hem"]
    points = np.zeros((rows + 1, count + 1, 3))
    for i in range(rows + 1):
        z = hem + (top_level - hem) * i / rows
        k = np.clip((z - levels[0]) / 0.004, 0, len(levels) - 1.001)
        j, t = k.astype(int), k - k.astype(int)
        r = table[j, np.arange(count)] * (1 - t) + table[j + 1, np.arange(count)] * t + spec["ease"]
        below = np.clip(levels[0] - z, 0, None)                    # under the game skirt's hem: straight down, then in
        r = r - spec["taper"] * below
        cut = body.segments(2, float(z.mean()))[:, :, :2]
        if len(cut) > 8:
            r = np.fmax(r, gl.fill_nan(gl.ray_radius(cut, centre, rays, limit=0.3), True) + spec["body_ease"])
        points[i, :count] = np.concatenate([centre + rays * r[:, None], z[:, None]], axis=1)
    for i in range(rows + 1):                                       # round the ring once more: no kinks where the legs part
        points[i, :count, :2] = centre + gl.smooth1(points[i, :count, :2] - centre, 3, wrap=True)
    points[:, count] = points[:, 0]
    band = max(2, int(round(spec["waistband"] / ((spec["waist"] - hem) / rows))))
    sheet = Sheet("skirt", points, (0.0, 0.0, 1.0, 1.0), list(range(0, rows + 1, f)) + [1, rows - band, rows - band - 1], range(0, count + 1, f))
    face_out(sheet, np.array([0.0, spec["centre_y"], points[rows // 2, 0, 2]]))
    sheets = [sheet]
    positions, quads = gl.weld(sheets)
    if DRAPE:
        hold = np.full(points.shape[:2], spec["hold"])
        hold[rows - band:] = 1.0
        hold[:2] = np.maximum(hold[:2], 0.8)
        surplus = np.zeros(points.shape[:2])
        heights = (np.arange(rows + 1) / rows)[:, None]
        surplus[:] = np.exp(-((heights - 0.62) / 0.2) ** 2)          # across the lap, where a straight skirt creases
        pin, grow = np.zeros(len(positions)), np.zeros(len(positions))
        pin[sheet.ids] = np.maximum(pin[sheet.ids], hold)
        grow[sheet.ids] = np.maximum(grow[sheet.ids], surplus)
        settled = gl.drape(positions, quads, pin, [(body.vertices, body.triangles)], spec.get("frames", 60), spec.get("cloth"), grow=grow)
        positions = gl.relax(settled, quads, spec.get("relax", 3), fixed=pin >= 0.999)
        positions = body.push_out(positions, 0.005, rounds=1)
    gl.pull(sheets, positions)
    normals = gl.quad_normals(positions, quads)
    lift = np.zeros(len(positions))
    lift[sheet.ids[rows - band:]] = 0.002
    positions = positions + normals * lift[:, None]
    gl.pull(sheets, positions)
    normals = gl.quad_normals(positions, quads)
    mesh, atlas = Mesh(), Atlas(ATLAS)
    hollows = gl.cavity(positions, quads, normals)
    mesh.add_sheet(sheet, normals[sheet.ids])
    atlas.add(sheet, normals[sheet.ids], hollows[sheet.ids])
    turn_in(mesh, sheet, normals, 0, depth=0.012)
    turn_in(mesh, sheet, normals, -1, depth=0.03)
    vertices, _, _, triangles = mesh.arrays()
    weights, shapes = gl.skin(vertices, triangles, Surface(spec["weights"]), rounds=3)
    stats = gl.export(spec["id"], mesh, weights, shapes)

    size = atlas.size
    inside, bottom, top, left, right = edge_fields(atlas, "skirt")
    u, v = atlas.local("skirt")
    band_w = spec["waistband"]
    with np.errstate(invalid="ignore"):
        seam = np.nan_to_num(np.exp(-((top - band_w) / 0.0013) ** 2))
        stitch = np.nan_to_num(gl.stitch_line(top - band_w + 0.003, atlas.across, 0.0006, 0.0028)) * 0.7
        for at in (0.25, 0.75):
            seam = np.fmax(seam, np.nan_to_num(np.exp(-(column_distance(atlas, "skirt", at) / 0.0012) ** 2)) * 0.8)
        back = np.fmin(np.abs(np.nan_to_num(column_distance(atlas, "skirt", 0.0), nan=9.0)), np.abs(np.nan_to_num(column_distance(atlas, "skirt", 1.0), nan=9.0)))
        seam = np.fmax(seam, np.exp(-(back / 0.0012) ** 2))
        vent = np.nan_to_num(np.exp(-(back / 0.0022) ** 2) * (bottom < spec["vent"]))                 # the back vent: an open slit
        stitch = np.fmax(stitch, np.nan_to_num(gl.stitch_line(np.hypot(back, np.clip(bottom - spec["vent"], 0, None)) - 0.012, atlas.up + back, 0.0006, 0.0028)
                                               * (bottom < spec["vent"] + 0.014)) * 0.7)
        darts = np.zeros((size, size))
        for at, length in ((0.12, 0.11), (0.88, 0.11), (0.37, 0.085), (0.63, 0.085)):
            d = np.nan_to_num(column_distance(atlas, "skirt", at), nan=9.0)
            fade = np.nan_to_num(np.clip(1 - (top - band_w) / length, 0, 1) * (top > band_w))
            darts = np.fmax(darts, np.exp(-(d / 0.0011) ** 2) * fade)
        seam = np.fmax(seam, darts * 0.8)
    weave = 0.5 + 0.25 * np.sin(atlas.across * 2 * np.pi / 0.0016) + 0.25 * np.sin(atlas.up * 2 * np.pi / 0.0016)
    grain = gl.noise(size, 61, (1, 3))
    normal = atlas.finish_normal(weave * 0.04 + grain * 0.03 + seam * 0.7 - stitch * 0.3 - vent * 1.2, spec["relief"] * size / 1024, spec.get("fold_gain", 1.0))
    name = spec["id"]
    gl.save_image(name + "_n", normal * 0.5 + 0.5, spec["normal_size"])
    textures = [{"name": name + "_n", "file": "textures/%s_n.png" % name, "type": "normal", "maxSize": spec["normal_size"]}]
    folds = atlas.shade(spec.get("fold_shade", 1.0))
    tinted = gl.Tinted(spec)

    def make():
        shade = (0.965 + 0.03 * weave) * (1.0 + 0.025 * grain) * folds * (1 - 0.14 * seam) * (1 - 0.10 * stitch) * (1 - 0.6 * vent)
        return np.clip(shade, 0, 1)

    def pinstripe():
        # Chalk lines parallel to the centre front, below the waistband: they meet at an angle in the centre back seam.
        d = np.nan_to_num(column_distance(atlas, "skirt", 0.5), nan=0.0)
        line = bands(d, PINSTRIPE[0], PINSTRIPE[1], 0.0008) * np.nan_to_num(top > band_w)
        return np.dstack([line, np.zeros((size, size)), np.zeros((size, size))])
    items = []
    for colour_name, entry in colourways(spec):
        cloth = tuple(np.array(entry["cloth"]) * spec.get("tone", 1.0))
        if entry.get("pattern") == "pinstripe":
            tinted.put(colour_name, ("", make), ("pin", pinstripe, spec.get("stripe_size", 1024)), entry["second"], cloth, cloth)
        else:
            tinted.put(colour_name, ("", make), None, cloth, cloth, cloth)
        items.append({"id": "%s_%s" % (name, colour_name), "kind": "legs", "sex": spec["sex"], "mesh": name, "tier": spec["tier"],
                      "skinFrom": spec["skin_from"], "layers": [tinted.layers[colour_name]], "stats": stats})
    return items, textures + tinted.textures


# Denim washes: base maps. contrast = how much lighter the pale weft is than the dyed warp, fade = how far wear has
# brought it out, hue = per channel exponents (blue denim fades towards grey). A colour picks its wash by rule in
# paint_jeans (dark = raw, mid blue = washed, the rest = pale) unless it is named here; `cloth` is what the jeans
# come out at on average (the tint is that over the base map's mean).
JEANS_WASHES = {
    "washed": dict(fade=0.85, contrast=5.0, hue=(1.22, 1.08, 0.88)),
    "raw": dict(fade=0.35, contrast=4.0, size=512),
    "pale": dict(fade=1.0, contrast=1.3, size=512),
}
JEANS_COLOURS = {
    "indigo": dict(wash="washed", cloth=(0.17, 0.21, 0.31)),
    "navy": dict(wash="raw", cloth=(0.075, 0.095, 0.16)),                               # dark rinse
    "sky": dict(wash="pale", cloth=(0.42, 0.52, 0.66), thread=(0.82, 0.70, 0.45)),      # light wash
    "black": dict(wash="raw", cloth=(0.06, 0.06, 0.065), thread=(0.10, 0.10, 0.11)),
    "grey": dict(wash="pale", cloth=(0.37, 0.37, 0.38), thread=(0.50, 0.50, 0.52), metal=(0.45, 0.45, 0.46)),
}

GARMENTS = {
    # Men's straight-leg jeans: mid rise, room in the thigh, a straight 40 cm hem that breaks once on the shoe.
    "legs_m_jeans": dict(
        make=trousers, id="legs_m_jeans", sex="male", fit="MaleJeans", body=[("MaleSwimsuit", [0]), ("MaleNakedFeet", [0])],
        over=["feet_m_court"], columns=32, leg_rows=30, blend_rows=3, hip_rows=9, waist=1.046, hem=0.085, rise_low=0.065,
        fit_inset=0.006, hip_ease=0.0015, crotch_ease=0.004, inseam_gap=0.004, waistband=0.038,
        ease=curve([(0.0, 0.010), (0.45, 0.012), (0.60, 0.016), (0.80, 0.014)]),
        leg=curve([(0.08, 0.0635), (0.50, 0.0665), (0.62, 0.070), (0.80, 0.074)]),
        loops=(0.10, 0.50, 1.0), loop_rect=(0.005, 0.90, 0.058, 0.995),
        pocket_in=0.105, pocket_down=0.075, yoke_back=0.105, yoke_side=0.062, back_pocket=(0.095, 0.125, 0.135, 0.145),
        twill=0.0036, relief=1.6, normal_size=1024, colour_size=1024, tier="everyday",
        # folds: (height or -1 knee / -2 crotch, reach, share of the surplus)
        hold=0.05, hem_hold=0.8, hem_rise=0.040, hem_back=0.008, folds=((0.22, 0.07, 1.0), (-1, 0.08, 0.7), (-2, 0.08, 0.8)),
        cloth=dict(mass=0.02, tension_stiffness=30.0, compression_stiffness=30.0, shear_stiffness=20.0, bending_stiffness=3.0, body_distance=0.004,
                   grow=0.08, pin_stiffness=0.6),
        frames=70, relax=4),
    # Men's crew-neck T-shirt: a regular fit that falls from the chest, set-in sleeves to above the elbow, a ribbed
    # collar, twin-needle hems. Worn over the trousers.
    "top_m_tee": dict(
        make=top, paint=paint_knit_top, id="top_m_tee", sex="male", skin_from="Shirt", weights=[("MaleShirt", [0])], yoke=[("MaleShirt", [0])], yoke_from=1.30,
        body=[("MaleNakedTorso", None), ("MaleFace1", [0]), ("MaleSwimsuit", [0])], under=[("MaleJeans", None)],
        columns=26, low_rows=12, up_rows=8, sleeve_rows=5, hem=0.905, centre_y=0.008,
        ease=curve([(0.9, 0.012), (1.1, 0.014), (1.3, 0.012)]), over_ease=0.010, fall=0.10, flare=0.25, upper_ease=0.008,
        scye_at=(0.006, 0.0, -0.01), scye_lean=0.26, scye_ease=0.012, scye_drop=0.022,
        neck_half=0.068, neck_front=0.058, neck_back=0.016, neck_ease=0.004, shoulder_ease=0.007, shoulder_share=0.46,
        sleeve_start=0.07, sleeve_length=0.235, sleeve_ease=curve([(0.0, 0.016), (0.25, 0.022)]), sleeve_width=curve([(0.0, 0.058), (0.25, 0.064)]),
        collar=0.032, collar_stand=0.3, fold_shade=0.4, collar_length=0.48,
        rects={"front": (0.0, 0.33, 0.5, 1.0), "back": (0.5, 0.33, 1.0, 1.0), "sleeve_l": (0.0, 0.0, 0.5, 0.27), "sleeve_r": (0.5, 0.0, 1.0, 0.27),
               "collar": (0.0, 0.285, 1.0, 0.32)},
        knit=0.0036, relief=1.0, normal_size=1024, colour_size=512, tier="everyday", patterns=("heather", "stripes", "block", "trim"),
        hold=0.03, surplus=0.6, cloth=dict(mass=0.012, tension_stiffness=15.0, compression_stiffness=15.0, shear_stiffness=8.0, bending_stiffness=0.6,
                                         body_distance=0.004, grow=0.035, pin_stiffness=0.5),
        frames=60, relax=3),
    # Men's bomber jacket: a blouson body on a ribbed hem band, ribbed cuffs and a ribbed stand collar, a front zip
    # closed to the collar (so nothing worn under it has to show), slanted welt pockets.
    "top_m_bomber": dict(
        make=top, paint=paint_bomber, id="top_m_bomber", sex="male", skin_from="Sweater", weights=[("MaleSweater", [0])],
        body=[("MaleNakedTorso", None), ("MaleFace1", [0]), ("MaleSwimsuit", [0])], under=[("MaleJeans", None)],
        columns=28, low_rows=10, up_rows=8, sleeve_rows=13, hem=0.925, centre_y=0.008, band=0.055, cuff=0.055, cuff_ease=0.010,
        ease=curve([(0.92, 0.010), (0.975, 0.010), (0.995, 0.040), (1.15, 0.034), (1.3, 0.026)]), over_ease=0.004, fall=3.0, flare=3.0, upper_ease=0.016,
        scye_at=(0.010, 0.0, -0.014), scye_lean=0.26, scye_ease=0.024, scye_drop=0.034,
        neck_half=0.072, neck_front=0.050, neck_back=0.012, neck_ease=0.009, shoulder_ease=0.014, shoulder_share=0.46,
        sleeve_start=0.08, sleeve_length=0.522, sleeve_ease=curve([(0.0, 0.024), (0.45, 0.024), (0.53, 0.018)]),
        sleeve_width=curve([(0.0, 0.066), (0.28, 0.062), (0.44, 0.058), (0.53, 0.05)]),
        collar=0.040, collar_stand=0.85, collar_length=0.50, pocket=(0.105, 0.15, 0.15, 0.30), zip_pull=(0.004, 0.438, 0.03, 0.448),
        rects={"front": (0.0, 0.45, 0.5, 1.0), "back": (0.5, 0.45, 1.0, 1.0), "sleeve_l": (0.0, 0.0, 0.5, 0.36), "sleeve_r": (0.5, 0.0, 1.0, 0.36),
               "collar": (0.0, 0.375, 1.0, 0.435)},
        relief=1.3, normal_size=1024, colour_size=1024, mask_size=512, tier="everyday", patterns=("block", "trim"), fold_shade=0.7, clear=0.008,
        hold=0.03, surplus=1.0, sleeve_hold=0.06, cloth=dict(mass=0.014, tension_stiffness=20.0, compression_stiffness=20.0, shear_stiffness=10.0,
                                                           bending_stiffness=1.2, body_distance=0.006, grow=0.07, pin_stiffness=0.5),
        frames=60, relax=3),
}

# Chinos: a slimmer, tapered leg in cotton twill; slanted front pockets, buttoned welt pockets, one colour (tint mask).
GARMENTS["legs_m_chinos"] = dict(
    GARMENTS["legs_m_jeans"], id="legs_m_chinos", paint=paint_chinos, colour_size=512, mask_size=256,
    ease=curve([(0.0, 0.007), (0.45, 0.009), (0.60, 0.014), (0.80, 0.013)]),
    leg=curve([(0.08, 0.057), (0.30, 0.058), (0.50, 0.063), (0.62, 0.068), (0.80, 0.073)]),
    waistband=0.036, pocket_in=0.045, pocket_down=0.165, back_pocket=(0.095, 0.085, 0.13),
    twill=0.0020, weave_relief=0.08, relief=1.2, fold_shade=0.8, hem_hold=1.0,
    hem=0.088, hem_rise=0.034, folds=((0.20, 0.06, 0.8), (-1, 0.08, 0.6), (-2, 0.08, 0.8)),
    cloth=dict(mass=0.016, tension_stiffness=24.0, compression_stiffness=24.0, shear_stiffness=14.0, bending_stiffness=1.6, body_distance=0.004,
               grow=0.07, pin_stiffness=0.6))
# Suit trousers: the chinos' cut in a smooth wool, pressed creases, no visible stitching at waistband and hem.
GARMENTS["legs_m_suit"] = dict(
    GARMENTS["legs_m_chinos"], id="legs_m_suit", tier="smart", patterns=("pinstripe",), crease=True, band_stitch=False, hem_stitch=False,
    twill=0.0016, weave_relief=0.04, wash=0.0, fold_shade=0.9,
    leg=curve([(0.08, 0.060), (0.30, 0.061), (0.50, 0.065), (0.62, 0.069), (0.80, 0.074)]),
    folds=((0.20, 0.06, 0.6), (-1, 0.08, 0.4), (-2, 0.08, 0.7)))

# The women's versions: the same cuts round the woman's body, with their own fit.
GARMENTS["legs_f_jeans"] = dict(
    GARMENTS["legs_m_jeans"], id="legs_f_jeans", sex="female", fit="FemJeans", body=[("FemLegsSwimsuit", [0]), ("FemNakedFeet", [0])],
    over=["feet_f_court"], waist=1.056, hem=0.085, hem_rise=0.036,
    ease=curve([(0.0, 0.008), (0.45, 0.009), (0.60, 0.011), (0.80, 0.010)]),
    leg=curve([(0.08, 0.058), (0.50, 0.060), (0.62, 0.064), (0.80, 0.068)]),
    pocket_in=0.095, pocket_down=0.065, back_pocket=(0.085, 0.115, 0.120, 0.130))
GARMENTS["top_f_tee"] = dict(
    GARMENTS["top_m_tee"], id="top_f_tee", sex="female", weights=[("FemShirt", [0])], yoke=[("FemShirt", [0])],
    body=[("FemTorsoSwimsuit", None), ("FemFace", [0]), ("FemLegsSwimsuit", [0])], under=[("FemJeans", None), ("FemSkirt", [0])],
    hem=0.93, ease=curve([(0.9, 0.009), (1.1, 0.008), (1.3, 0.009)]), over_ease=0.008, fall=0.45, flare=0.5, upper_ease=0.007,
    neck_half=0.080, neck_front=0.078, neck_back=0.020, scye_ease=0.010, scye_drop=0.016,
    sleeve_ease=curve([(0.0, 0.012), (0.25, 0.014)]), sleeve_width=curve([(0.0, 0.05), (0.25, 0.052)]), collar=0.018, patterns=("heather", "stripes", "block", "trim"),      # dots were tried: ovals where the tee draws in
    yoke_relax=3)

GARMENTS["legs_f_chinos"] = dict(
    GARMENTS["legs_f_jeans"], id="legs_f_chinos", paint=paint_chinos, colour_size=512, mask_size=256,
    ease=curve([(0.0, 0.006), (0.45, 0.007), (0.60, 0.010), (0.80, 0.009)]),
    leg=curve([(0.08, 0.052), (0.30, 0.053), (0.50, 0.057), (0.62, 0.062), (0.80, 0.067)]),
    waistband=0.034, pocket_in=0.04, pocket_down=0.15, back_pocket=(0.085, 0.08, 0.115),
    twill=0.0020, weave_relief=0.08, relief=1.2, fold_shade=0.8, hem=0.105, hem_rise=0.020, hem_hold=1.0,
    folds=((0.20, 0.05, 0.5), (-1, 0.08, 0.6), (-2, 0.08, 0.8)),
    cloth=dict(mass=0.016, tension_stiffness=24.0, compression_stiffness=24.0, shear_stiffness=14.0, bending_stiffness=1.6, body_distance=0.004,
               grow=0.07, pin_stiffness=0.6))
GARMENTS["legs_f_suit"] = dict(
    GARMENTS["legs_f_chinos"], id="legs_f_suit", tier="smart", patterns=("pinstripe",), crease=True, band_stitch=False, hem_stitch=False,
    twill=0.0016, weave_relief=0.04, wash=0.0, fold_shade=0.9, hem=0.085, hem_rise=0.034,
    leg=curve([(0.08, 0.058), (0.30, 0.058), (0.50, 0.060), (0.62, 0.064), (0.80, 0.068)]),
    folds=((0.20, 0.06, 0.6), (-1, 0.08, 0.4), (-2, 0.08, 0.7)))

# A summer dress is two pieces that meet in its waist seam: a fitted bodice with a scoop neck and short sleeves (a top)
# and a flared knee-length skirt (worn in the legs' place). The bodice is built first: the skirt hangs from its hem.
GARMENTS["dress_f_summer"] = dict(
    GARMENTS["top_f_tee"], id="dress_f_summer", waist_seam=True, hem=1.085, low_rows=6, knit=0.0018,
    under=[("FemJeans", None)],      # as it was built and accepted (the tee has since been cut to clear the pencil skirt too)
    ease=curve([(1.0, 0.007), (1.3, 0.008)]), fall=3.0, flare=3.0, neck_half=0.085, neck_front=0.085, neck_back=0.03,
    sleeve_length=0.20, collar=0.008, patterns=("dots", "finedots", "stripes", "gingham", "block", "trim"), yoke_relax=3, colour_size=512, mask_size=1024,
    rects={"front": (0.0, 0.40, 0.5, 1.0), "back": (0.5, 0.40, 1.0, 1.0), "sleeve_l": (0.0, 0.0, 0.5, 0.33), "sleeve_r": (0.5, 0.0, 1.0, 0.33),
           "collar": (0.0, 0.35, 1.0, 0.385)})
GARMENTS["skirt_f_summer"] = dict(
    make=skirt, id="skirt_f_summer", sex="female", bodice="dress_f_summer", skin_from="FormalSkirt", weights=[("FemSkirt", [0])],
    body=[("FemLegsSwimsuit", None)], rows=20, hem=0.50, centre_y=0.008, ease=0.012,
    flare=curve([(0.0, 0.0), (0.05, 0.012), (0.20, 0.07), (0.60, 0.20)]),
    relief=1.0, normal_size=1024, colour_size=512, mask_size=1024, tier="everyday", patterns=("dots", "finedots", "stripes", "gingham", "block", "trim"), fold_shade=0.6,
    hold=0.012, cloth=dict(mass=0.01, tension_stiffness=15.0, compression_stiffness=15.0, shear_stiffness=6.0, bending_stiffness=0.35,
                           body_distance=0.006, grow=0.05, pin_stiffness=0.4), frames=70, relax=3)

# A pencil skirt: straight from the hip to just above the knee, a waistband, darts, a centre back seam with a vent.
GARMENTS["legs_f_pencil"] = dict(
    make=tube_skirt, id="legs_f_pencil", sex="female", fit="FemSkirt", skin_from="FormalSkirt", weights=[("FemSkirt", [0])],
    body=[("FemLegsSwimsuit", None)], columns=40, rows=18, hem=0.52, waist=1.07, centre_y=0.008, waistband=0.034,
    ease=0.002, body_ease=0.010, taper=0.16, vent=0.14, relief=1.0, normal_size=1024, colour_size=512, tier="smart", patterns=("pinstripe",), fold_shade=0.8,
    hold=0.08, cloth=dict(mass=0.012, tension_stiffness=25.0, compression_stiffness=25.0, shear_stiffness=12.0, bending_stiffness=1.5,
                          body_distance=0.005, grow=0.03, pin_stiffness=0.6), frames=50, relax=3)

# An evening dress: a fitted bodice with a soft V neck and cap sleeves in a fine, smooth cloth, and a long, slim A-line
# skirt to the calf from the waist seam. (The legs well above the hem are not drawn: skin_hide.)
GARMENTS["dress_f_evening"] = dict(
    GARMENTS["dress_f_summer"], id="dress_f_evening", under=[("FemJeans", None), ("FemSkirt", [0])], neck_half=0.082, neck_front=0.105, neck_back=0.03, neck_round=1.6,
    sleeve_length=0.15, collar=0.006, knit=0.0014, tier="premium", patterns=(), colour_size=512, relief=0.6, fold_shade=0.8)
# The same dress without sleeves was tried and is NOT shipped (README, "Sleeveless tops"): its description is kept for
# whoever takes it up. Bare arms and shoulders come from the game's swimsuit top; the skin under the cloth is hidden.
SLEEVELESS_TRIAL = dict(
    GARMENTS["dress_f_summer"], id="dress_f_sleeveless", sleeveless=True, skin_from="Swimsuit", weights=[("FemTorsoSwimsuit", None)],
    yoke=[("FemFace", [0])], yoke_from=1.30, yoke_ease=0.004, yoke_relax=1,
    neck_half=0.078, neck_front=0.125, neck_back=0.05, neck_round=1.7, shoulder_share=0.40, scye_ease=0.005, scye_drop=0.010,
    scye_at=(-0.012, 0.0, -0.01), clear=0.006, upper_ease=0.008, shoulder_ease=0.005, relax=5, skin_rounds=0,
    skin_surface=[("FemTorsoSwimsuit", [0])], skin_hide=("FemTorsoSwimsuit", 0),
    collar=0.006, knit=0.0014, tier="premium", patterns=(), colour_size=512, relief=0.6, fold_shade=0.8,
    rects={"front": (0.0, 0.08, 0.5, 1.0), "back": (0.5, 0.08, 1.0, 1.0), "collar": (0.0, 0.01, 1.0, 0.06)})
GARMENTS["skirt_f_evening"] = dict(
    GARMENTS["skirt_f_summer"], id="skirt_f_evening", bodice="dress_f_evening", rows=30, hem=0.30,
    weights=[("FemSkirt", [0]), ("FemLegsSwimsuit", [0])], skin_rounds=24, ease=0.022, skin_hide=("FemSkirt", 1),
    flare=curve([(0.0, 0.0), (0.05, 0.010), (0.20, 0.035), (0.50, 0.075), (0.80, 0.12)]),
    tier="premium", patterns=(), colour_size=512, fold_shade=0.8,
    cloth=dict(mass=0.012, tension_stiffness=18.0, compression_stiffness=18.0, shear_stiffness=7.0, bending_stiffness=0.5,
               body_distance=0.006, grow=0.04, pin_stiffness=0.4))


# Further families of garments live in files of their own (garments_<family>.py beside this one): each has a
# register(module) that adds its descriptions to GARMENTS (and may use everything defined above through `module`).
def register_families():
    import glob
    import importlib
    wanted = [a for a in ARGS if not a.startswith("--")]
    for path in sorted(glob.glob(os.path.join(os.path.dirname(os.path.abspath(__file__)), "garments_*.py"))):
        name = os.path.splitext(os.path.basename(path))[0]
        try:
            importlib.import_module(name).register(sys.modules[__name__])
        except Exception as error:      # a family that is being worked on must not stop the others from building
            print("family %s not loaded: %r" % (name, error))
            if not wanted:
                raise                   # a build of everything is a build of everything


if __name__ == "__main__":
    sys.path.insert(0, os.path.join(ba.ROOT, "_tools", "pylib"))
    os.makedirs(PARTIAL, exist_ok=True)
    register_families()
    wanted = [a for a in ARGS if not a.startswith("--")] or list(GARMENTS)
    for name in wanted:
        spec = GARMENTS[name]
        items, textures = spec["make"](spec)
        with open(os.path.join(PARTIAL, name + ".json"), "w") as f:
            json.dump({"items": items, "textures": textures}, f)
    items, textures = [], []
    for name in GARMENTS:
        path = os.path.join(PARTIAL, name + ".json")
        if os.path.exists(path):
            with open(path) as f:
                part = json.load(f)
            items += part["items"]
            textures += part["textures"]
    with open(os.path.join(ba.BUILD, "catalogue_garments.json"), "w") as f:
        json.dump({"items": items, "textures": textures}, f, indent=1)
    print("garments:", len(items), "items,", len(textures), "textures")
