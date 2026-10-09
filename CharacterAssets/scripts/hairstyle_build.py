"""The new hairstyles: blender -b -P hairstyle_build.py -- [style id ...]   (all without an id)

Writes build_hair/meshes/<id>.json, build_hair/textures/hairstyle_scalp_<id>.png and build_hair/catalogue_hairstyles.json.
A style is a description for hairstyle_lib: where the guide lines start (parting, crown), where they leave the
head, how long and how thick, and the layers of cards laid along them. Metres and degrees.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hairstyle_lib as lib  # noqa: E402
from hair_lib import smoothstep, unit  # noqa: E402


def wrap(th):
    return (th + 180.0) % 360.0 - 180.0


def angles(v):
    v = unit(v)
    return math.degrees(math.atan2(v[0], -v[1])), math.degrees(math.asin(max(-1.0, min(1.0, v[2]))))


def sagittal(phi, side=0.0):
    """A point of the arc over the top of the head: phi degrees forward of straight up (negative: behind)."""
    return np.array([side, -math.sin(math.radians(phi)), math.cos(math.radians(phi))])


def ring(lo, hi, count):
    """Exit azimuths from lo round the back to hi (unwrapped, so they interpolate)."""
    return np.linspace(lo, hi, count)


# ------------------------------------------------------------------------------------------------ women

def back_cape(th, depth, front=62.0, full=80.0):
    """Where long hair hangs once it has left the head: behind the shoulders, on an oval round neck and back that
    widens downwards. The strands beside the face hang straight; the ones behind them are drawn back."""
    a, side = abs(wrap(th)), 1.0 if wrap(th) >= 0 else -1.0
    if a < front or depth < 0.02:
        return None
    psi = math.radians(30.0 + 60.0 * (a - front) / (180.0 - front))
    grow = 1.0 + 0.5 * max(0.0, depth - 0.05)
    return (side * 0.128 * grow * math.cos(psi), 0.03 + (0.098 + 0.12 * max(0.0, depth - 0.08)) * math.sin(psi), float(smoothstep(a, front, full)))


def uneven(th, amount=0.05):
    return 1.0 + amount * math.sin(math.radians(th) * 7.3) + 0.8 * amount * math.sin(math.radians(th) * 3.1 + 1.0)


def f_long_straight():
    def root(th):
        a, side = abs(wrap(th)), 1.0 if wrap(th) >= 0 else -1.0
        u = float(np.clip((a - 42.0) / 83.0, 0.0, 1.0))
        return angles(sagittal(66.0 - 94.0 * u, 0.008 * side))

    def exit_(th):
        return float(np.interp(abs(wrap(th)), [42, 50, 60, 80, 100, 140, 180], [15, 13, 10, 4, 0, -8, -12]))

    def length(th):
        return float(np.interp(abs(wrap(th)), [42, 55, 64, 80, 95, 180], [0.34, 0.36, 0.35, 0.36, 0.42, 0.45])) * uneven(th, 0.035)

    def volume(th, t):
        front = 1.0 - float(smoothstep(abs(wrap(th)), 40.0, 90.0))
        return 0.004 + 0.007 * front + (0.006 + 0.004 * front) * float(smoothstep(t, 0.0, 0.18)) + 0.008 * float(smoothstep(t, 0.15, 0.7))

    full = (1.0, 1.0)
    C = 64
    return {
        "id": "hair_f_long_straight", "sex": "female", "seed": 3, "cover_ears": True, "hangs": True, "closed": False,
        "thetas": ring(42.0, 318.0, C), "rows": 18, "root": root, "exit": exit_, "length": length, "volume": volume,
        "flare": lambda th, f: 0.0, "cape": back_cape, "clearance": 0.016, "scalp": {"soft": 10.0},
        "wave": {"from": 0.12, "length": 0.30, "side": 0.003, "depth": 0.003, "twist": 1.0, "loose": 0.4},
        "layers": [
            {"strip": "DENSE", "count": 80, "start": (0.25, 0.45), "length": full, "lift": (0.002, 0.004), "width": 0.036, "segments": 9, "drift": 0.15,
             "overhang": 0.04},
            {"strip": ("DENSE", "MEDIUM"), "count": 170, "start": (0.0, 0.04), "length": full, "lift": (0.004, 0.008), "width": 0.030, "segments": 11,
             "drift": 0.2, "overhang": 0.06},
            {"strip": "MEDIUM", "count": 90, "start": (0.0, 0.3), "length": (0.2, 0.36), "lift": (0.007, 0.012), "width": 0.026, "segments": 8, "drift": 0.25},
            {"strip": ("MEDIUM", "WISPY"), "role": "wisps", "count": 16, "columns": (0.0, 2.5), "start": (0.0, 0.15), "length": (0.10, 0.30),
             "lift": (0.002, 0.007), "width": 0.024, "segments": 8, "drift": 0.6, "tilt": 0.3},
            {"strip": ("MEDIUM", "WISPY"), "role": "wisps", "count": 16, "columns": (61.5, 63.0), "start": (0.0, 0.15), "length": (0.10, 0.30),
             "lift": (0.002, 0.007), "width": 0.024, "segments": 8, "drift": 0.6, "tilt": 0.3},
            {"hairline": True, "role": "wisps", "strip": ("MEDIUM", "WISPY", "WISPY"), "count": 26, "az": (30.0, 78.0), "sweep": 14.0, "rise": 5.0,
             "width": 0.016, "on": 0.14, "lift": 0.006, "inside": (-0.5, 3.0)},
            {"strip": ("WISPY", "WISPY", "FLY"), "role": "wisps", "count": 110, "start": (0.0, 0.6), "length": (0.12, 0.35), "lift": (0.009, 0.02),
             "width": 0.028, "segments": 7, "drift": 0.5, "overhang": 0.08},
        ],
    }


def f_waves():
    """Shoulder-length waves: the parting and fall of the long style, cut at the shoulders, with body and a wave from the ears down."""
    style = f_long_straight()
    length = style["length"]
    volume = style["volume"]
    style.update({
        "id": "hair_f_waves", "seed": 5, "rows": 20,
        "length": lambda th: min(length(th), float(np.interp(abs(wrap(th)), [42, 64, 95, 180], [0.27, 0.28, 0.31, 0.33])) * uneven(th, 0.03)),
        "volume": lambda th, t: volume(th, t) + 0.005 * float(smoothstep(t, 0.2, 0.9)),
        "wave": {"from": 0.07, "length": 0.11, "side": 0.006, "depth": 0.006, "twist": 2.0, "loose": 0.3},
    })
    for layer in style["layers"]:
        if "segments" in layer:
            layer["segments"] = max(layer["segments"], 12)
    return style


# ------------------------------------------------------------------------------------------------ men

def top_of_head(az, e, lo=36.0, hi=52.0):
    """1 on the top of the head (which reaches down to the hairline at the front), 0 on sides and back."""
    return float(smoothstep(e + 22.0 * max(0.0, math.cos(math.radians(az))), lo, hi))


def on_path(root, exit_, th, t):
    az0, e0 = root(th)
    d = lib.slerp(lib.direction(az0, e0), lib.direction(th, exit_(th)), t)
    return angles(d)


def m_crop():
    """Short textured crop: hair of three to five centimetres brushed forward from the crown, short back and sides."""
    head_line = lib.hair_lib.Head("male").hairline
    crown = lib.direction(180.0, 62.0)

    def root(th):
        return angles(crown + 0.10 * np.array([math.sin(math.radians(th)), -math.cos(math.radians(th)), 0.0]))

    def exit_(th):
        # Over the forehead the hair reaches past the hairline; at the sides and back it stops short of it, and the
        # painted scalp carries the fade down to the skin.
        return float(head_line(abs(wrap(th)))) - 5.0 + 15.0 * float(smoothstep(abs(wrap(th)), 45.0, 70.0))

    def volume(th, t):
        az, e = on_path(root, exit_, th, t)
        top = top_of_head(az, e)
        return (0.0035 + 0.0125 * top) * (1.0 - 0.75 * float(smoothstep(t, 0.9, 1.0))) + 0.0015

    return {
        "id": "hair_m_crop", "sex": "male", "seed": 7, "cover_ears": False, "hangs": False, "closed": True,
        "thetas": np.linspace(-180.0, 180.0, 56, endpoint=False), "rows": 14, "scalp_steps": 16, "root": root, "exit": exit_, "length": lambda th: 1.0,
        "volume": volume, "columns_per_strip": 2,
        "scalp": {"soft": 6.0, "tone": 0.8, "density": lambda phi, e: np.clip((e - head_line(phi)) / 13.0 + 0.9 * np.clip(np.cos(np.radians(phi)), 0, 1), 0, 1)},
        "layers": [
            {"strip": ("DENSE", "MEDIUM"), "count": 1500, "even": True, "zone": lambda az, e: 1.0 - top_of_head(az, e), "start": (0.05, 0.97), "length": (0.012, 0.022),
             "lift": (0.0005, 0.003), "width": 0.012, "segments": 2, "drift": 0.4, "tilt": 0.2, "waves": False},
            {"strip": ("DENSE", "DENSE", "MEDIUM"), "count": 1700, "even": True, "zone": top_of_head, "start": (0.0, 0.93), "length": (0.028, 0.045),
             "lift": (0.001, 0.008), "width": 0.014, "segments": 3, "drift": 0.9, "tilt": 0.35, "bend": 0.6, "waves": False, "overhang": 0.03},
            {"strip": ("MEDIUM", "WISPY"), "role": "wisps", "count": 700, "even": True, "zone": top_of_head, "start": (0.0, 0.96), "length": (0.025, 0.04),
             "lift": (0.004, 0.014), "width": 0.012, "segments": 3, "drift": 1.2, "tilt": 0.4, "bend": 0.6, "waves": False, "overhang": 0.05},
            {"strip": "WISPY", "role": "wisps", "count": 500, "even": True, "zone": lambda az, e: 1.0 - top_of_head(az, e), "start": (0.75, 1.0), "length": (0.008, 0.016),
             "lift": (0.0005, 0.002), "width": 0.010, "segments": 2, "drift": 0.3, "waves": False, "overhang": 0.03},
        ],
    }


def m_sidepart():
    """Side part, medium: parted on the left, the top combed across to the right and a little back, sides and back combed down."""
    head_line = lib.hair_lib.Head("male").hairline
    front, back = lib.direction(34.0, 41.0), lib.direction(150.0, 58.0)

    def root(th):
        w = wrap(th)
        if w <= 34.0:
            u = float(np.clip((34.0 - w) / 184.0, 0.0, 1.0)) ** 0.8
            side = -1.0
        else:
            u = float(np.clip((w - 34.0) / 116.0, 0.0, 1.0))
            side = 1.0
        d = lib.slerp(front, back, u)
        return angles(d - 0.012 * side * unit(np.cross(d, back - front)) * (1.0 if u < 0.98 else 0.0))

    def exit_(th):
        return float(head_line(abs(wrap(th)))) - 4.0 + 11.0 * float(smoothstep(abs(wrap(th)), 45.0, 70.0))

    def across(az, e):
        return top_of_head(az, e, 34.0, 48.0)

    def volume(th, t):
        az, e = on_path(root, exit_, th, t)
        top = across(az, e) * (1.0 if wrap(th) <= 34.0 else 0.35)
        quiff = max(0.0, math.cos(math.radians(az))) ** 2
        rise = float(smoothstep(t, 0.0, 0.12))
        return (0.004 + (0.012 + 0.008 * quiff) * top * (0.35 + 0.65 * rise)) * (1.0 - 0.75 * float(smoothstep(t, 0.9, 1.0))) + 0.0015

    return {
        "id": "hair_m_sidepart", "sex": "male", "seed": 9, "cover_ears": False, "hangs": False, "closed": True,
        "thetas": np.linspace(-180.0, 180.0, 60, endpoint=False), "rows": 16, "scalp_steps": 18, "root": root, "exit": exit_, "length": lambda th: 1.0,
        "volume": volume,
        "scalp": {"soft": 6.0, "tone": 0.8, "density": lambda phi, e: np.clip((e - head_line(phi)) / 11.0 + 0.9 * np.clip(np.cos(np.radians(phi)), 0, 1), 0, 1)},
        "layers": [
            {"strip": ("DENSE", "MEDIUM"), "count": 1500, "even": True, "zone": lambda az, e: 1.0 - across(az, e), "start": (0.03, 0.95), "length": (0.02, 0.035),
             "lift": (0.0005, 0.003), "width": 0.013, "segments": 2, "drift": 0.2, "tilt": 0.15, "waves": False, "overhang": 0.02},
            {"strip": ("DENSE", "DENSE", "MEDIUM"), "count": 900, "even": True, "zone": across, "start": (0.0, 0.8), "length": (0.05, 0.09),
             "lift": (0.0015, 0.006), "width": 0.019, "segments": 5, "drift": 0.25, "tilt": 0.2, "waves": False, "overhang": 0.02},
            {"strip": ("MEDIUM", "WISPY"), "role": "wisps", "count": 450, "even": True, "zone": across, "start": (0.0, 0.85), "length": (0.04, 0.08),
             "lift": (0.005, 0.011), "width": 0.016, "segments": 4, "drift": 0.4, "tilt": 0.25, "waves": False, "overhang": 0.04},
            {"strip": "WISPY", "role": "wisps", "count": 500, "even": True, "zone": lambda az, e: 1.0 - across(az, e), "start": (0.75, 1.0), "length": (0.01, 0.02),
             "lift": (0.0005, 0.002), "width": 0.010, "segments": 2, "drift": 0.3, "waves": False, "overhang": 0.03},
        ],
    }


STYLES = {"hair_f_long_straight": f_long_straight, "hair_f_waves": f_waves, "hair_m_crop": m_crop, "hair_m_sidepart": m_sidepart}


def main():
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    path = os.path.join(lib.OUT, "catalogue_hairstyles.json")
    catalogue = {"items": [], "textures": []}
    if os.path.exists(path):
        with open(path) as f:
            catalogue = json.load(f)
    atlas_entry = {"name": "hairstyle_atlas", "file": "textures/hairstyle_atlas.png", "type": "color", "maxSize": 1024}
    for style_id in wanted or list(STYLES):
        item, textures = lib.build(STYLES[style_id]())
        catalogue["items"] = [i for i in catalogue["items"] if i["id"] != item["id"]] + [item]
        names = {t["name"] for t in textures} | {"hairstyle_atlas"}
        catalogue["textures"] = [t for t in catalogue["textures"] if t["name"] not in names] + textures + [atlas_entry]
    with open(path, "w") as f:
        json.dump(catalogue, f, indent=1)


main()
