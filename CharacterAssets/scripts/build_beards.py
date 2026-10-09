"""Builds the beards: a painted base that hugs the jaw, fuzz shells for stubble, strand cards for length.

    blender -b -P build_beards.py -- [beard id ...] [--no-preview]

A beard follows the face: every vertex takes its bone weights and its Fat / Slim / Musc / Mouth.* shape
offsets from the nearest point of the game's head mesh, so the body and mouth sliders move it with the skin.

Angles are those of hair_lib: phi from the front (0) to the ear (about 85), e the elevation seen from the
head centre (measured on the head: nose tip -20, lips -30 to -40, mouth corners at phi 21, chin -46).
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba
import hair_atlas
import hair_lib
from hair_lib import Builder, Head, Spot, card, grow, smoothstep, unit, vertex_normals
from mathutils import Vector

DOWN = np.array([0.0, 0.0, -1.0])
FORWARD = np.array([0.0, -1.0, 0.0])
LEFT = np.array([1.0, 0.0, 0.0])
TEXTURE_SIZE = 512
FOLLOWED_SHAPES = ["Fat", "Slim", "Musc", "Mouth.Height.Low", "Mouth.Height.High", "Mouth.Width.Low", "Mouth.Width.High",
                   "Mouth.Profile.Low", "Mouth.Profile.High", "Mouth.Corners.Low", "Mouth.Corners.High"]

# The outline of a full beard. Upper edge: moustache, then the cheek line up to the sideburn. Lower edge: the neck line.
UPPER = ([0, 12, 24, 40, 60, 76, 86], [-24.5, -25, -30, -30, -24, -11, -10])
LOWER = ([0, 30, 60, 78, 86], [-63, -61, -52, -38, -30])
EAR = 86.0


def inside_beard(phi, e, style=None):
    """Degrees inside the beard outline (negative outside), lips cut out. Works on arrays."""
    phi, e = np.asarray(phi, dtype=np.float64), np.asarray(e, dtype=np.float64)
    upper = np.interp(phi, *UPPER) + (style.get("cheek", 0.0) if style else 0.0)
    lower = np.interp(phi, *LOWER) + (style.get("neck", 0.0) if style else 0.0)
    m = np.minimum(np.minimum(upper - e, e - lower), (EAR - phi) * 0.6)
    # The lips: an ellipse round the mouth.
    lips = 1.0 - np.sqrt((phi / 17.0) ** 2 + ((e + 35.0) / 5.2) ** 2)
    m = np.minimum(m, -lips * 6.0)
    if style and style.get("goatee"):
        m = np.minimum(m, (style["goatee"] - phi) * 0.6)
    if style and style.get("no_moustache"):
        m = np.where((e > -30.5) & (phi < 26), -5.0, m)
    return m


class Face(Head):
    """The head as a beard sees it: the skin is found from outside, and 'inside' means inside the beard outline."""

    def __init__(self, style):
        super().__init__("male")
        self.style = style

    def surface(self, direction):
        d = unit(direction)
        hit = self.bvh.ray_cast(Vector(self.centre + d * 0.45), Vector(-d), 0.6)
        if hit[0] is None:
            return None
        n = np.array(hit[1])
        if np.dot(n, d) < 0:
            n = -n
        return np.array(hit[0]), n

    def spot(self, direction, rng=None):
        found = self.surface(direction)
        if found is None:
            return None
        s = Spot()
        s.p, s.n = found
        s.d = unit(direction)
        phi, e = self.angles(s.d)
        s.phi, s.e = float(phi), float(e)
        s.m = float(inside_beard(s.phi, s.e, self.style))
        s.side = 1.0 if s.d[0] >= 0 else -1.0
        s.rng = rng
        return s


def face_uv(direction):
    d = np.asarray(direction, dtype=np.float64)
    d = d / np.maximum(np.linalg.norm(d, axis=-1, keepdims=True), 1e-12)
    azimuth = np.degrees(np.arctan2(d[..., 0], -d[..., 1]))
    e = np.degrees(np.arcsin(np.clip(d[..., 2], -1, 1)))
    return np.stack([(azimuth + 95.0) / 190.0, (e + 76.0) / 72.0], axis=-1)


def build_shell(face, offset, columns=56, rows=18, fuzz_tile=None, shrink=0.0):
    """A grid over the jaw between the cheek line and the neck line. With fuzz_tile it is mapped onto the fuzz strip."""
    u0, u1, _, _ = hair_atlas.strip_uv("FUZZ")
    vertices, directions, uv, keep = [], [], [], []
    for j in range(rows + 1):
        for i in range(columns + 1):
            azimuth = -(EAR + 4) + 2 * (EAR + 4) * i / columns
            phi = abs(azimuth)
            upper = float(np.interp(phi, *UPPER)) + 5.0
            lower = float(np.interp(phi, *LOWER)) - 5.0
            e = upper + (lower - upper) * j / rows
            d = face.direction(azimuth, e)
            found = face.surface(d)
            if found is None:
                raise RuntimeError("no skin along azimuth %.0f elevation %.0f" % (azimuth, e))
            vertices.append(found[0] + found[1] * offset)
            directions.append(d)
            keep.append(float(inside_beard(phi, e, face.style)) > shrink - 4.0)
            uv.append((u0 if i % 2 == 0 else u1, j * fuzz_tile) if fuzz_tile else tuple(face_uv(d)))
    vertices = np.array(vertices)
    triangles = []
    for j in range(rows):
        for i in range(columns):
            a = j * (columns + 1) + i
            b, c, d = a + 1, a + columns + 1, a + columns + 2
            if keep[a] or keep[b] or keep[c] or keep[d]:
                triangles += [(a, b, d), (a, d, c)]
    triangles = np.array(triangles, dtype=np.int64)
    normals = vertex_normals(vertices, triangles)
    if np.sum(normals * (vertices - face.centre)) < 0:
        triangles = triangles[:, ::-1].copy()
        normals = -normals
    return vertices, normals, np.array(uv), triangles


def paint(face, style, path):
    """The base layer: short hairs painted along the growth direction, fading out at the beard's outline."""
    from PIL import Image, ImageDraw, ImageFilter
    rng = np.random.default_rng(style["seed"] + 500)
    size, sup = style.get("size", TEXTURE_SIZE), 2
    big = size * sup
    ys, xs = np.mgrid[0:size, 0:size]
    u, v = (xs + 0.5) / size, 1.0 - (ys + 0.5) / size
    azimuth, e = u * 190.0 - 95.0, v * 72.0 - 76.0
    inside = inside_beard(np.abs(azimuth), e, style)
    grain = rng.random((size // 3, size // 3))
    noise = np.asarray(Image.fromarray((grain * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC), dtype=np.float64) / 255.0
    soft = style.get("soft", 5.0)
    alpha = smoothstep(inside + (noise - 0.5) * soft, -0.5, soft) * style.get("alpha", 1.0)
    # Hairs: where they are drawn the base is solid, between them the skin shows by (1 - cover).
    strands = Image.new("L", (big, big), 0)
    colour = Image.new("L", (big, big), int(255 * 0.45))
    ds, dc = ImageDraw.Draw(strands), ImageDraw.Draw(colour)
    for _ in range(style.get("strokes", 22000)):
        az, el = rng.uniform(-90, 90), rng.uniform(-72, -8)
        if float(inside_beard(abs(az), el, style)) < -3:
            continue
        spot = face.spot(face.direction(az, el), rng)
        if spot is None:
            continue
        t = spot.tangent(style["flow"](spot) + rng.normal(0, 0.25, 3))
        a = face_uv(spot.p - face.centre)
        b = face_uv(spot.p + t * style.get("stroke", 0.004) * rng.uniform(0.5, 1.4) - face.centre)
        line = [(a[0] * big, (1 - a[1]) * big), (b[0] * big, (1 - b[1]) * big)]
        ds.line(line, fill=255, width=sup)
        dc.line(line, fill=int(255 * np.clip(rng.normal(0.45, 0.16), 0.12, 0.95)), width=sup)
    strands = np.asarray(strands.resize((size, size), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.5)), dtype=np.float64) / 255.0
    cover = style.get("cover", 1.0)                # 1: solid base; lower: only the painted hairs
    alpha = alpha * np.clip(cover + (1 - cover) * strands * 1.6, 0, 1)
    grey = np.asarray(colour.resize((size, size), Image.LANCZOS), dtype=np.uint8)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.dstack([grey, grey, grey, (np.clip(alpha, 0, 1) * 255).astype(np.uint8)]), "RGBA").save(path)


def sample(face, count, rng, min_inside=1.5):
    spots = []
    tries = 0
    while len(spots) < count * 3 and tries < count * 60:
        tries += 1
        az = rng.uniform(-EAR, EAR)
        el = math.degrees(math.asin(rng.uniform(math.sin(math.radians(-70)), math.sin(math.radians(-8)))))
        if float(inside_beard(abs(az), el, face.style)) < min_inside:
            continue
        spot = face.spot(face.direction(az, el), rng)
        if spot is not None:
            spots.append(spot)
    return spots


def follow_face(face, vertices):
    """Bone weights and slider shapes from the nearest head vertex."""
    from mathutils.kdtree import KDTree
    ref = face.ref
    tree = KDTree(len(ref["vertices"]))
    for i, v in enumerate(ref["vertices"]):
        tree.insert(Vector(v), i)
    tree.balance()
    nearest = np.array([tree.find(Vector(v))[1] for v in vertices])
    shapes = [(name, ref["shapes"][name][nearest]) for name in FOLLOWED_SHAPES if name in ref["shapes"]]
    return ref["bone_indices"][nearest], ref["bone_weights"][nearest], shapes


def beard_flow(s):
    # Down and a little forward on the cheeks and chin; sideways from the middle on the upper lip.
    moustache = float(smoothstep(s.e, -31.5, -29.0)) * float(1 - smoothstep(s.phi, 22, 32))
    return (1 - moustache) * (DOWN + 0.25 * FORWARD) + moustache * (s.side * LEFT + 0.6 * DOWN)


def moustache(s):
    return float(smoothstep(s.e, -31.5, -29.0)) * float(1 - smoothstep(s.phi, 22, 32))


def chin(s):
    """1 on the chin, 0 at the ears."""
    return float(1 - smoothstep(s.phi, 18, 60))


STUBBLE = {
    "id": "beard_stubble", "seed": 3, "flow": beard_flow, "alpha": 0.95, "cover": 0.16, "strokes": 60000, "stroke": 0.0022,
    "soft": 7.0, "size": 1024, "layers": [],
}
SHORT = {
    "id": "beard_short", "seed": 5, "flow": beard_flow, "alpha": 1.0, "cover": 0.72, "strokes": 30000, "stroke": 0.005,
    "soft": 5.0,
    "layers": [
        {"name": "coat", "count": 620, "strip": "MEDIUM", "role": "bulk", "length": lambda s: 0.008 + 0.004 * chin(s), "lift": lambda s: 22.0,
         "width": 0.008, "segments": 2, "gravity": 6.0, "hug": 0.3, "flow_jitter": 0.3, "clearance": (0.0008, 0.004)},
        {"name": "edge", "count": 200, "strip": "WISPY", "role": "wisps", "min_inside": -0.5, "where": lambda s: 1 - float(smoothstep(s.m, 1, 5)),
         "length": lambda s: 0.006, "lift": lambda s: 18.0, "width": 0.007, "segments": 2, "gravity": 4.0, "clearance": (0.0006, 0.002)},
    ],
}
FULL = {
    "id": "beard_full", "seed": 9, "flow": beard_flow, "alpha": 1.0, "cover": 0.95, "strokes": 30000, "stroke": 0.007,
    "soft": 5.0, "neck": 4.0,
    "layers": [
        {"name": "under", "count": 520, "strip": "DENSE", "role": "bulk", "length": lambda s: (0.016 + 0.020 * chin(s)) * (1 - 0.6 * moustache(s)),
         "lift": lambda s: 12.0, "width": 0.013, "segments": 3, "gravity": 10.0, "hug": 0.5, "flow_jitter": 0.2, "clearance": (0.001, 0.008)},
        {"name": "over", "count": 460, "strip": "MEDIUM", "role": "bulk", "length": lambda s: (0.020 + 0.026 * chin(s)) * (1 - 0.65 * moustache(s)),
         "lift": lambda s: 18.0, "width": 0.011, "segments": 4, "gravity": 12.0, "hug": 0.4, "flow_jitter": 0.25, "curl": 0.6, "clearance": (0.002, 0.014)},
        {"name": "edge", "count": 240, "strip": "WISPY", "role": "wisps", "min_inside": -0.5, "where": lambda s: 1 - float(smoothstep(s.m, 1, 6)),
         "length": lambda s: 0.009, "lift": lambda s: 20.0, "width": 0.008, "segments": 2, "gravity": 4.0, "clearance": (0.0006, 0.003)},
    ],
}
BEARDS = [STUBBLE, SHORT, FULL]


def build(style):
    rng = np.random.default_rng(style["seed"])
    face = Face(style)
    builder = Builder()
    builder.add("scalp", *build_shell(face, 0.0009))
    if style.get("shells"):
        count, spacing, tile = style["shells"]
        for k in range(count):
            builder.add("wisps", *build_shell(face, 0.0009 + spacing * (k + 1), fuzz_tile=tile, shrink=2.0 + 1.5 * k))
    for layer in style["layers"]:
        spots = sample(face, layer["count"], rng, layer.get("min_inside", 1.5))
        keep = layer.get("where")
        chosen = [s for s in spots if keep is None or rng.random() < keep(s)][:layer["count"]]
        chosen.sort(key=lambda s: s.p[2])            # lower hairs first, so those above lie over them
        for spot in chosen:
            path = grow(face, spot, style, layer, rng)
            if path is not None:
                builder.add(layer["role"], *card(path, layer, rng, face))
    vertices, normals, uv, roles, submeshes = builder.arrays()
    indices, weights, shapes = follow_face(face, vertices)
    stats = ba.write_mesh(style["id"], vertices, normals, uv, submeshes, indices, weights, shapes)
    texture = "base_" + style["id"]
    paint(face, style, os.path.join(ba.BUILD, "textures", texture + ".png"))
    layers = [{"role": r, "texture": texture if r == "scalp" else "hair_atlas", "normal": None, "mask": None} for r in roles]
    item = {"id": style["id"], "kind": "beard", "sex": "male", "mesh": style["id"], "layers": layers, "skinFrom": None, "stats": stats}
    textures = [{"name": texture, "file": "textures/" + texture + ".png", "type": "color", "maxSize": style.get("size", TEXTURE_SIZE)}]
    return item, textures, {"vertices": vertices, "normals": normals, "uv": uv, "roles": roles, "submeshes": submeshes, "texture": texture, "face": face}


def preview(style, data):
    scene = ba.reset_scene()
    camera = ba.setup_render(scene)
    ba.add_reference("MaleFace1", texture_name="T_FaceMale1_BC", only_submesh=0)
    ba.add_reference("MaleNakedTorso", only_submesh=0)
    atlas = ba.image(os.path.join(ba.BUILD, "textures", "hair_atlas.png"))
    base = ba.image(os.path.join(ba.BUILD, "textures", data["texture"] + ".png"))
    kinds = {"scalp": (base, "blend", False), "bulk": (atlas, "clip", True), "wisps": (atlas, "blend", True)}
    materials = [ba.material(role, tint=(0.105, 0.062, 0.036), texture=kinds[role][0], alpha=kinds[role][1], roughness=0.62,
                             two_sided=kinds[role][2], specular=0.2) for role in data["roles"]]
    ba.make_object(style["id"], data["vertices"], data["submeshes"], data["uv"], data["normals"], materials)
    target = data["face"].centre - (0, 0, 0.07)
    print("preview", ba.render_views(scene, camera, target, style["id"], [(0, 0), (40, 0), (90, 0), (20, -30)], distance=0.72, size=500))
    size = int(0.6 * 294.0)
    far = ba.render_views(scene, camera, target - (0, 0, 0.08), style["id"] + "_far", [(0, 20), (50, 20)], distance=3.0,
                          fov=math.degrees(2 * math.atan(0.3 / 3.0)), size=size)
    from PIL import Image
    img = Image.open(far)
    img.resize((img.width * 3, img.height * 3), Image.NEAREST).save(far)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wanted = [a for a in args if not a.startswith("--")]
    items, textures = [], []
    for style in BEARDS:
        if wanted and style["id"] not in wanted:
            continue
        item, extra, data = build(style)
        items.append(item)
        textures += extra
        if "--no-preview" not in args:
            preview(style, data)
    if not wanted:
        with open(os.path.join(ba.BUILD, "catalogue_beards.json"), "w") as f:
            json.dump({"items": items, "textures": textures}, f, indent=1)


main()
