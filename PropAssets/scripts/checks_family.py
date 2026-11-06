"""What the family code (Scripts/Family/FamilyCast.cs, Scripts/Licences/LicenceProps.cs), the family clips
(CityAssets/scripts/clips_family.py) and the features to come rely on in the family props: see check_life.py. The
numbers are those at the head of props_family.py (which needs Blender, and this does not)."""
import numpy as np

SIZES = {                                            # kit space: x, y (forward), z (up)
    "wedding_arch": (2.10, 0.30, 2.50), "flower_stand": (0.40, 0.40, 1.20), "aisle_runner": (1.10, 1.00, 0.018), "cot": (0.72, 1.26, 0.90),
    "high_chair": (0.48, 0.50, 1.05), "driving_school_sign": (0.62, 0.52, 1.05), "test_checkpoint_beacon": (None, 0.22, 2.90),
    "ring_box": (0.05, 0.05, 0.06),
}
CENTRED = ("wedding_arch", "flower_stand", "aisle_runner", "cot", "high_chair", "driving_school_sign")      # ground-centre pivots the code places by


def run(c):
    print("Family props: sizes, pivots, grips and heights")
    for name, size in SIZES.items():
        v = c.kit(name)
        for axis, want in enumerate(size):
            if want is not None:
                c.extent(name, "xyz"[axis], np.ptp(v[:, axis]), want, 0.006 if name == "driving_school_sign" and axis == 1 else 0.004)
    for name in CENTRED:
        v = c.kit(name)
        c.extent(name, "stands on the ground", v[:, 2].min(), 0.0, 0.0005)
        for axis in (0, 1):
            c.extent(name, "pivot in the middle (%s)" % "xy"[axis], (v[:, axis].max() + v[:, axis].min()) / 2, 0.0, 0.004)
    # The wedding: the arch's posts are where the stand-in's are, the pedestal's top is the stand-in's, the runner tiles.
    v = c.kit("wedding_arch")
    low = v[(v[:, 2] > 0.3) & (v[:, 2] < 0.9) & (v[:, 0] > 0) & (v[:, 1] < 0)]             # a post where nothing is hung on it
    c.extent("wedding_arch", "post (x)", (low[:, 0].max() + low[:, 0].min()) / 2, 0.95, 0.012)
    v = c.kit("flower_stand")
    c.extent("flower_stand", "pedestal top", v[(np.abs(v[:, 0]) > 0.115) & (np.abs(v[:, 1]) > 0.115) & (v[:, 2] < 0.97)][:, 2].max(), 0.95, 0.004)
    v = c.kit("aisle_runner")
    for end in (-0.5, 0.5):
        edge = v[np.abs(v[:, 1] - end) < 0.001]
        c.extent("aisle_runner", "end at y %+.1f: the plain section" % end, edge[:, 2].max(), 0.0175, 0.0008)
    # The driving school: a board to each side, the beacon's post on the pivot and its flag above head height.
    v = c.kit("driving_school_sign")
    c.extent("driving_school_sign", "feet apart (y)", np.ptp(v[v[:, 2] < 0.02][:, 1]), 0.52, 0.006)
    v = c.kit("test_checkpoint_beacon")
    base = v[v[:, 2] < 0.04]
    c.extent("test_checkpoint_beacon", "base across", np.ptp(base[:, 0]), 0.22, 0.001)
    c.extent("test_checkpoint_beacon", "base on the pivot", (base[:, 0].max() + base[:, 0].min()) / 2, 0.0, 0.001)
    flag = v[v[:, 0] > 0.12]
    c.extent("test_checkpoint_beacon", "flag from", flag[:, 2].min(), 2.60, 0.035)
    c.extent("test_checkpoint_beacon", "flag out to (x)", v[:, 0].max(), 0.45, 0.004)
    # Held things: the ring box on a palm, the baby on a forearm.
    c.extent("ring_box", "stands on its base", c.kit("ring_box")[:, 2].min(), 0.0, 0.0005)
    v = c.kit("baby_bundle")
    c.extent("baby_bundle", "underside", v[:, 2].min(), 0.0, 0.001)
    c.extent("baby_bundle", "length", np.ptp(v[:, 1]), 0.50, 0.004)
    c.extent("baby_bundle", "width", np.ptp(v[:, 0]), 0.22, 0.004)
    c.extent("baby_bundle", "height", v[:, 2].max(), 0.18, 0.004)
    c.extent("baby_bundle", "under the left palm (0, -0.10): the underside", v[(np.abs(v[:, 1] + 0.10) < 0.04)][:, 2].min(), 0.0, 0.004)
    # The cot: rock_cot's hands are on a side rail, the mattress is where a baby is laid.
    for side in (-1, 1):
        c.extent("cot", "side rail top", c.top_near("cot", side * 0.33, 0.565, 0.012, across=0.02), 0.885, 0.004)
    v = c.kit("cot")
    rail = v[(np.abs(v[:, 1]) < 0.575) & (v[:, 2] > 0.86) & (v[:, 0] > 0)]
    c.extent("cot", "side rail middle (x)", (rail[:, 0].max() + rail[:, 0].min()) / 2, 0.33, 0.003)
    c.extent("cot", "mattress top", c.top_near("cot", 0.284, 0.544, 0.012, below=0.56), 0.515, 0.003)
    c.extent("high_chair", "seat", c.top_near("high_chair", 0.13, 0.12, 0.01, below=0.66), 0.645, 0.004)
    c.extent("high_chair", "tray rim", float(c.kit("high_chair")[c.kit("high_chair")[:, 1] > 0.20][:, 2].max()), 0.815, 0.004)
