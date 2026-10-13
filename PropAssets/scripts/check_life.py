"""Measures, on the built meshes, what the clips of CityAssets rely on: where the handles of the life props are and
how thick, the heights people stand on and serve at. Run after build_props.py; exits 1 if anything is off.

    python check_life.py

Reads build/meshes/<id>.json (Unity space) and works in the kit's space (x, y forward, z up).
"""
import json
import os
import sys

import numpy as np

BUILD = os.environ.get("PROPS_BUILD") or os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "build")
failures = []
EXTRA = ["civic", "service", "events", "everyday", "garage", "family", "site", "tow"]


def kit(name, triangles=False):
    with open(os.path.join(BUILD, "meshes", name + ".json")) as f:
        data = json.load(f)
    vertices = np.array(data["vertices"]).reshape(-1, 3)[:, [0, 2, 1]]
    return (vertices, np.array(data["triangles"]).reshape(-1, 3)) if triangles else vertices


def report(ok, text):
    print(("  ok    " if ok else "  WRONG ") + text)
    if not ok:
        failures.append(text)


def grip(name, centre, axis, reach=0.03, thick=(0.025, 0.035), within=0.03):
    """A handle through `centre` along `axis`: the mesh is cut across the axis at the point and `reach` either side
    of it; what is cut within `within` of the axis must surround it and be a bar of the contracted thickness."""
    v, triangles = kit(name, True)
    v = v - np.array(centre)
    axis = np.array(axis, dtype=float)
    axis /= np.linalg.norm(axis)
    edges = np.concatenate([triangles[:, [0, 1]], triangles[:, [1, 2]], triangles[:, [2, 0]]])
    off, thickness = 0.0, []
    for cut in (-reach, 0.0001, reach):
        a, b = v[edges[:, 0]], v[edges[:, 1]]
        da, db = a @ axis - cut, b @ axis - cut
        crossing = (da * db < 0)
        t = (da[crossing] / (da[crossing] - db[crossing]))[:, None]
        points = a[crossing] + (b[crossing] - a[crossing]) * t
        radial = points - np.outer(points @ axis, axis)
        distance = np.linalg.norm(radial, axis=1)
        near = distance <= within
        if near.sum() < 6:
            report(False, "%s: nothing to hold at %s" % (name, centre))
            return
        off = max(off, float(np.linalg.norm((radial[near].max(axis=0) + radial[near].min(axis=0)) / 2)))
        thickness.append(2 * distance[near].max())
    thickness = max(thickness)
    report(off < 0.004 and thick[0] <= thickness <= thick[1] + 0.0005,
           "%-19s grip at (%.3f, %.3f, %.3f): %.1f mm thick, centred within %.1f mm" % (name, *centre, thickness * 1000, off * 1000))


def extent(name, text, got, want, tolerance=0.003):
    report(abs(got - want) <= tolerance, "%-19s %s: %.3f (wanted %.3f)" % (name, text, got, want))


def top_near(name, x, y, reach=0.03, across=None, below=9.0):
    v = kit(name)
    near = (np.abs(v[:, 0] - x) <= (across or reach)) & (np.abs(v[:, 1] - y) <= reach) & (v[:, 2] < below)
    return float(v[near, 2].max())


EXTRA.append("tech")       # the tech sector's buildings (checks_tech.py)


def main():
    print("Life props: grips and heights")
    grip("mop", (0, 0, 0.95), (0, 0, 1))
    grip("mop", (0, 0, 1.28), (0, 0, 1))
    extent("mop", "length", kit("mop")[:, 2].max(), 1.45)
    extent("mop", "head width", np.ptp(kit("mop")[:, 0]), 0.30)
    grip("rake", (0, 0, 0.95), (0, 0, 1))
    grip("rake", (0, 0, 1.30), (0, 0, 1))
    extent("rake", "length", kit("rake")[:, 2].max(), 1.55)
    extent("rake", "head width", np.ptp(kit("rake")[:, 0]), 0.45, 0.015)
    grip("paint_roller", (0, 0, 0.07), (0, 0, 1), reach=0.025)
    v = kit("paint_roller")
    roller = v[np.abs(v[:, 2] - 0.40) < 0.05]
    extent("paint_roller", "roller width", np.ptp(roller[np.abs(roller[:, 0]) < 0.12][:, 0]), 0.23)
    extent("paint_roller", "roller radius", np.abs(v[:, 1]).max(), 0.045)
    extent("paint_roller", "roller centre height", (roller[:, 2].max() + roller[:, 2].min()) / 2, 0.40)
    grip("watering_can", (0, -0.02, 0.34), (0, 1, 0), reach=0.055)
    v = kit("watering_can")
    body = v[(v[:, 2] < 0.30) & (np.abs(v[:, 1]) < 0.16) & (np.abs(v[:, 0]) > 0.03)]
    extent("watering_can", "clear space under the grip", 0.34 - 0.015 - body[:, 2].max(), 0.08, 0.04)
    v = kit("guitar")
    extent("guitar", "length", np.ptp(v[:, 2]), 1.00)
    extent("guitar", "body width", np.ptp(v[:, 0]), 0.38)
    neck = v[np.abs(v[:, 2] - 0.78) < 0.09]
    extent("guitar", "neck width where the fret hand is", np.ptp(neck[:, 0]), 0.045, 0.002)
    extent("guitar", "neck depth there", np.ptp(neck[:, 1]), 0.032, 0.004)
    v = kit("radio_handset")
    waist = v[np.abs(v[:, 2] - 0.06) < 0.06]
    extent("radio_handset", "body width", np.ptp(waist[np.abs(waist[:, 0]) < 0.0305][:, 0]), 0.06)
    extent("radio_handset", "height", v[:, 2].max(), 0.21)
    grip("umbrella", (0, 0, 0.06), (0, 0, 1), within=0.02)
    v = kit("umbrella")
    extent("umbrella", "canopy width", np.ptp(v[:, 0]), 1.02, 0.02)
    v = kit("newspaper")
    extent("newspaper", "width", np.ptp(v[:, 0]), 0.58)
    extent("newspaper", "height", np.ptp(v[:, 2]), 0.40)
    extent("map_folded", "width", np.ptp(kit("map_folded")[:, 0]), 0.44)
    back = np.array((0, -1, 1)) / np.sqrt(2)
    for side in (-1, 1):
        grip("hand_truck", (side * 0.17, -0.04, 1.14), back, reach=0.05)
        grip("pushchair", (side * 0.225, -0.44, 0.95), (1, 0, 0))
        for end in (-1, 1):
            grip("stretcher_push_bar", (side * 0.225, end * 1.11, 0.95), (1, 0, 0))
    extent("step_ladder", "tread 1", top_near("step_ladder", 0, -0.24, 0.05, 0.21, 0.30), 0.24, 0.0005)
    extent("step_ladder", "tread 2 (stood on)", top_near("step_ladder", 0, -0.12, 0.05, 0.21, 0.60), 0.48, 0.0005)
    extent("step_ladder", "platform", top_near("step_ladder", 0, 0.0, 0.15, 0.24), 0.72, 0.0005)
    extent("chess_table", "table top", top_near("chess_table", 0.33, 0.33), 0.74, 0.0005)
    for y in (-0.62, 0.62):
        extent("chess_table", "stool seat", top_near("chess_table", 0, y, 0.2), 0.45, 0.0005)
    for name in ("vendor_cart", "fruit_stand"):
        where = (-0.79, -0.33, 0.13) if name == "fruit_stand" else (-0.74, 0.39, 0.02)
        extent(name, "counter height", top_near(name, *where, below=1.0), 0.975, 0.0005)
    v = kit("vendor_cart")
    slab = v[np.abs(v[:, 2] - 0.96) < 0.016]
    extent("vendor_cart", "counter depth", np.ptp(slab[np.abs(slab[:, 0]) > 0.7][:, 1]), 0.80)
    v = kit("newspaper_kiosk")
    sill = v[(np.abs(v[:, 0]) < 0.5) & (v[:, 1] > 0.7) & (v[:, 2] < 1.0)]
    extent("newspaper_kiosk", "sill height", sill[:, 2].max(), 0.975, 0.0005)
    v = kit("flyer_stack")
    extent("flyer_stack", "top edge at the pivot", v[:, 2].max(), 0.0, 0.0005)
    extent("flyer_stack", "height", np.ptp(v[:, 2]), 0.21)
    v = kit("mail_satchel")
    extent("mail_satchel", "strap over the shoulder, above the pivot", v[:, 2].max(), 1.495 - 1.30, 0.006)
    bag = v[v[:, 2] < 1.02 - 1.30]
    extent("mail_satchel", "bag centre x", (bag[:, 0].max() + bag[:, 0].min()) / 2, -0.21, 0.012)
    for name, size in (("parcel_s", (0.22, 0.16, 0.10)), ("parcel_m", (0.38, 0.28, 0.24)), ("parcel_l", (0.50, 0.36, 0.32)), ("shelf_box", None)):
        v = kit(name)
        if size:
            for axis, want in enumerate(size):
                extent(name, "xyz"[axis], np.ptp(v[:, axis]), want, 0.004)
    # The later rounds: checks_<name>.py beside this file, each with run(c), c being this module (grip, extent,
    # top_near, kit, report). PROPS_DEV limits them as it does the props.
    here = os.path.dirname(os.path.abspath(__file__))
    sys.path.insert(0, here)
    only = os.environ.get("PROPS_DEV")
    for name in ([n.strip() for n in only.split(",") if n.strip()] if only else EXTRA):
        if os.path.exists(os.path.join(here, "checks_" + name + ".py")):
            __import__("checks_" + name).run(sys.modules[__name__])
    if failures:
        print("%d WRONG" % len(failures))
        sys.exit(1)
    print("all as contracted")


if __name__ == "__main__":
    main()
