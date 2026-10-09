"""The hairstyles. One dict each: add a style here, add its id to the mod's catalogue (CharacterCatalogue.cs)
and its label to Locales/en.json, run build.ps1.

A style's functions get a Spot (hair_lib.Spot): p position, n outward normal, phi azimuth from the front
(0..180), e elevation (degrees), m degrees inside the hairline, side (+1 the character's left, -1 right).
Lengths and widths are metres, lift is degrees off the scalp.
"""
import numpy as np

from hair_lib import smoothstep

DOWN = np.array([0.0, 0.0, -1.0])
FORWARD = np.array([0.0, -1.0, 0.0])
BACK = np.array([0.0, 1.0, 0.0])
LEFT = np.array([1.0, 0.0, 0.0])


def top(s, lo=28.0, hi=50.0):
    """0 on the sides and back, 1 on top of the head."""
    return float(smoothstep(s.e, lo, hi))


def crop_flow(s):
    # Brushed forward on top, lying down on the sides and back.
    return top(s) * (FORWARD + 0.25 * s.side * LEFT) + (1 - top(s)) * DOWN


CROP = {
    "id": "hair_m_crop", "sex": "male", "seed": 11, "flow": crop_flow,
    "scalp": {"tone": 0.46, "soft": 6.0},
    "layers": [
        {"name": "sides", "count": 380, "strip": "DENSE", "role": "bulk",
         "where": lambda s: 1 - top(s, 34, 52), "length": lambda s: 0.016 + 0.012 * top(s, 5, 40),
         "lift": lambda s: 8.0, "width": 0.014, "segments": 3, "gravity": 4.0, "hug": 0.6, "clearance": (0.0015, 0.004)},
        {"name": "top under", "count": 320, "strip": "DENSE", "role": "bulk",
         "where": lambda s: top(s, 30, 46), "length": lambda s: 0.036, "lift": lambda s: 14.0,
         "width": 0.018, "segments": 4, "gravity": 3.0, "hug": 0.5, "clearance": (0.002, 0.008)},
        {"name": "top texture", "count": 260, "strip": "MEDIUM", "role": "bulk",
         "where": lambda s: top(s, 36, 52), "length": lambda s: 0.040, "lift": lambda s: 24.0, "lift_jitter": 10.0,
         "flow_jitter": 0.35, "width": 0.014, "segments": 4, "gravity": 4.0, "hug": 0.3, "curl": 1.5, "clearance": (0.002, 0.014)},
        {"name": "fringe", "count": 150, "strip": "MEDIUM", "role": "bulk", "min_inside": 0.0,
         "where": lambda s: (1 - float(smoothstep(s.m, 3, 10))) * (s.phi < 60), "length": lambda s: 0.022, "lift": lambda s: 18.0,
         "width": 0.010, "segments": 3, "gravity": 5.0, "hug": 0.3, "flow_jitter": 0.3, "clearance": (0.001, 0.006)},
        {"name": "hairline", "count": 160, "strip": "WISPY", "role": "wisps", "min_inside": -1.0,
         "where": lambda s: 1 - float(smoothstep(s.m, 2, 7)), "length": lambda s: 0.014, "lift": lambda s: 12.0,
         "width": 0.010, "segments": 2, "gravity": 2.0, "hug": 0.5, "clearance": (0.001, 0.003)},
    ],
}


def edge(s, lo=2.0, hi=7.0):
    """1 at the hairline, 0 further in."""
    return 1 - float(smoothstep(s.m, lo, hi))


def hairline_layer(length=0.014, count=130, lift=12.0, strip="WISPY"):
    """Short soft hairs along the edge, so the hairline is not a drawn line."""
    return {"name": "hairline", "count": count, "strip": strip, "role": "wisps", "min_inside": -1.0, "where": edge,
            "length": lambda s: length, "lift": lambda s: lift, "width": 0.010, "segments": 2, "gravity": 2.0, "hug": 0.5,
            "clearance": (0.001, 0.003)}


# ------------------------------------------------------------------------------------------------ men

PART_X = 0.034      # the parting sits on the character's left


def sidepart_flow(s):
    across = LEFT * float(np.tanh((s.p[0] - PART_X) / 0.010))   # combed away from the parting on both sides of it
    return top(s, 32, 50) * (across + 0.45 * BACK) + (1 - top(s, 32, 50)) * (DOWN + 0.5 * BACK)


def sidepart_thin(phi, e, azimuth):
    # A little scalp shows along the parting.
    x = np.sin(np.radians(azimuth)) * np.cos(np.radians(e))
    line = np.exp(-((x * 0.09 - PART_X) / 0.004) ** 2) * (e > 38) * (phi < 80)
    return 1 - 0.5 * line


SIDEPART = {
    "id": "hair_m_sidepart", "sex": "male", "seed": 23, "flow": sidepart_flow, "hairline_shift": -2.0,
    "scalp": {"tone": 0.46, "soft": 6.0, "thin": sidepart_thin},
    "layers": [
        {"name": "sides", "count": 360, "strip": "DENSE", "role": "bulk",
         "where": lambda s: 1 - top(s, 36, 52), "length": lambda s: 0.022 + 0.014 * top(s, 5, 40),
         "lift": lambda s: 6.0, "width": 0.015, "segments": 3, "gravity": 3.0, "hug": 0.7, "clearance": (0.0015, 0.004)},
        {"name": "top under", "count": 300, "strip": "DENSE", "role": "bulk",
         "where": lambda s: top(s, 32, 46), "length": lambda s: 0.060, "lift": lambda s: 10.0,
         "width": 0.020, "segments": 6, "gravity": 2.0, "hug": 0.8, "clearance": (0.002, 0.007)},
        {"name": "top over", "count": 240, "strip": "MEDIUM", "role": "bulk",
         "where": lambda s: top(s, 36, 50), "length": lambda s: 0.068, "lift": lambda s: 14.0 + 10.0 * edge(s, 0, 14) * (s.phi < 50),
         "width": 0.016, "segments": 6, "gravity": 2.5, "hug": 0.7, "flow_jitter": 0.2, "clearance": (0.003, 0.012)},
        hairline_layer(),
    ],
}

BUZZ = {
    "id": "hair_m_buzz", "sex": "male", "seed": 31, "flow": lambda s: top(s) * FORWARD + (1 - top(s)) * DOWN,
    "scalp": {"tone": 0.42, "soft": 5.0, "alpha": 0.86, "strokes": 16000, "stroke": 0.005},
    "shells": {"count": 3, "spacing": 0.0011, "tile": 2.6},
    "layers": [],
}


def swept_flow(s):
    behind = float(smoothstep(s.phi, 110, 150))          # at the back of the head it simply hangs
    return top(s, 25, 50) * (BACK + 0.12 * s.side * LEFT) + (1 - top(s, 25, 50)) * ((1 - behind) * BACK + (0.8 + behind) * DOWN)


SWEPT = {
    "id": "hair_m_swept", "sex": "male", "seed": 41, "flow": swept_flow, "hairline_shift": -2.0,
    "scalp": {"tone": 0.46, "soft": 6.0},
    "layers": [
        {"name": "sides", "count": 380, "strip": "DENSE", "role": "bulk",
         "where": lambda s: 1 - top(s, 36, 52), "length": lambda s: 0.045 - 0.02 * float(smoothstep(-s.e, 5, 25)), "lift": lambda s: 6.0,
         "width": 0.018, "segments": 5, "gravity": 2.5, "hug": 0.8, "clearance": (0.0015, 0.006)},
        {"name": "top under", "count": 300, "strip": "DENSE", "role": "bulk",
         "where": lambda s: top(s, 32, 46), "length": lambda s: 0.085, "lift": lambda s: 12.0 + 22.0 * (s.phi < 45),
         "width": 0.022, "segments": 7, "gravity": 1.5, "hug": 0.8, "clearance": (0.002, 0.010)},
        {"name": "top over", "count": 240, "strip": "MEDIUM", "role": "bulk",
         "where": lambda s: top(s, 36, 50), "length": lambda s: 0.095, "lift": lambda s: 18.0 + 26.0 * (s.phi < 45),
         "width": 0.018, "segments": 7, "gravity": 1.5, "hug": 0.7, "flow_jitter": 0.2, "clearance": (0.004, 0.018)},
        hairline_layer(),
    ],
}

# ------------------------------------------------------------------------------------------------ women

CROWN_Y = 0.03


def parted_flow(s):
    # Away from a centre parting in front of the crown, fanning out behind it, then down.
    # Hair that starts in front of the ears is taken back past them first, so it frames the face instead of covering it.
    away = np.array([s.p[0], max(0.0, s.p[1] - CROWN_Y), 0.0])
    norm = np.linalg.norm(away)
    away = away / norm if norm > 1e-4 else s.side * LEFT
    front = float(smoothstep(-s.p[1], -0.035, 0.03))
    crown = top(s, 30, 62)
    return crown * (away + 0.9 * front * BACK) + (1 - 0.7 * crown) * ((1 - 0.8 * front) * DOWN + 0.9 * front * BACK)


def hang_to(z_tip, extra=0.03):
    """Length that brings a strand from its root down to a given height."""
    return lambda s: max(0.03, (s.p[2] - z_tip) * 1.06 + extra + 0.07 * top(s, 30, 70))


def centre_thin(phi, e, azimuth):
    x = np.sin(np.radians(azimuth)) * np.cos(np.radians(e))
    line = np.exp(-((x * 0.09) / 0.0035) ** 2) * (e > 34) * (phi < 90)
    return 1 - 0.5 * line


def hanging(style_id, seed, z_tip, counts=(300, 260, 70), segments=11):
    return {
        "id": style_id, "sex": "female", "seed": seed, "flow": parted_flow, "long": True, "hairline_shift": -5.0,
        "scalp": {"tone": 0.46, "soft": 6.0, "thin": centre_thin},
        "layers": [
            {"name": "under", "count": counts[0], "strip": "DENSE", "role": "bulk", "body": True,
             "length": hang_to(z_tip), "length_jitter": 0.06, "lift": lambda s: 4.0, "width": 0.034, "segments": segments,
             "gravity": 14.0, "hug": 0.9, "clearance": (0.002, 0.006), "tilt": 8.0},
            {"name": "over", "count": counts[1], "strip": "MEDIUM", "role": "bulk", "body": True,
             "where": lambda s: 0.35 + 0.65 * top(s, 10, 45),
             "length": hang_to(z_tip, 0.04), "length_jitter": 0.08, "lift": lambda s: 7.0, "width": 0.026, "segments": segments,
             "gravity": 14.0, "hug": 0.9, "clearance": (0.004, 0.013), "flow_jitter": 0.1, "tilt": 12.0},
            {"name": "loose", "count": counts[2], "strip": "WISPY", "role": "wisps", "body": True,
             "where": lambda s: top(s, 10, 45),
             "length": hang_to(z_tip, 0.05), "length_jitter": 0.12, "lift": lambda s: 9.0, "width": 0.022, "segments": segments,
             "gravity": 13.0, "hug": 0.85, "clearance": (0.006, 0.018), "flow_jitter": 0.14, "tilt": 20.0},
            hairline_layer(length=0.018, count=110),
        ],
    }


LONG = hanging("hair_f_long", 53, z_tip=1.30)
BOB = hanging("hair_f_bob", 59, z_tip=1.475, counts=(280, 240, 60), segments=8)

TIE = np.array([0.0, 0.112, 1.640])      # where the ponytail is gathered, at the back of the head


def tied_flow(s):
    return TIE - s.p


PONYTAIL = {
    "id": "hair_f_ponytail", "sex": "female", "seed": 61, "flow": tied_flow, "long": True, "hairline_shift": -5.0,
    "scalp": {"tone": 0.44, "soft": 6.0, "stroke": 0.03},
    "layers": [
        {"name": "pulled back", "count": 420, "strip": "DENSE", "role": "bulk",
         "length": lambda s: min(0.20, float(np.linalg.norm(TIE - s.p)) * 1.12 + 0.01), "length_jitter": 0.03,
         "lift": lambda s: 3.0, "width": 0.022, "segments": 7, "gravity": 0.0, "hug": 1.2, "clearance": (0.002, 0.005),
         "tilt": 6.0, "order": lambda s: -float(np.linalg.norm(TIE - s.p))},
        hairline_layer(length=0.016, count=110, lift=4.0),
    ],
    "tails": [
        {"origin": tuple(TIE + np.array([0, 0.012, 0.0])), "direction": (0, 0.75, -0.55), "count": 54, "length": 0.27, "width": 0.022,
         "spread": 0.22, "radius": 0.007, "gravity": 7.0, "segments": 9, "strip": "DENSE"},
        {"origin": tuple(TIE + np.array([0, 0.012, 0.0])), "direction": (0, 0.75, -0.55), "count": 26, "length": 0.29, "width": 0.018,
         "spread": 0.15, "radius": 0.009, "gravity": 7.0, "segments": 9, "strip": "WISPY", "role": "wisps"},
    ],
}


def pixie_flow(s):
    # A long fringe swept forward and across; short and close at the sides and back.
    return top(s, 26, 48) * (FORWARD + 0.75 * LEFT) + (1 - top(s, 26, 48)) * (DOWN + 0.25 * FORWARD)


PIXIE = {
    "id": "hair_f_pixie", "sex": "female", "seed": 67, "flow": pixie_flow, "hairline_shift": -4.0,
    "scalp": {"tone": 0.46, "soft": 6.0},
    "layers": [
        {"name": "sides", "count": 380, "strip": "DENSE", "role": "bulk",
         "where": lambda s: 1 - top(s, 34, 52), "length": lambda s: 0.026 + 0.016 * top(s, 5, 40),
         "lift": lambda s: 7.0, "width": 0.015, "segments": 3, "gravity": 4.0, "hug": 0.6, "clearance": (0.0015, 0.004)},
        {"name": "top under", "count": 320, "strip": "DENSE", "role": "bulk",
         "where": lambda s: top(s, 30, 46), "length": lambda s: 0.058, "lift": lambda s: 12.0,
         "width": 0.020, "segments": 6, "gravity": 3.5, "hug": 0.6, "clearance": (0.002, 0.008)},
        {"name": "top over", "count": 260, "strip": "MEDIUM", "role": "bulk",
         "where": lambda s: top(s, 36, 52), "length": lambda s: 0.066, "lift": lambda s: 20.0, "lift_jitter": 8.0,
         "flow_jitter": 0.3, "width": 0.016, "segments": 6, "gravity": 4.0, "hug": 0.5, "curl": 1.0, "clearance": (0.003, 0.014)},
        hairline_layer(length=0.016),
    ],
}

STYLES = [CROP, SIDEPART, BUZZ, SWEPT, LONG, BOB, PONYTAIL, PIXIE]
