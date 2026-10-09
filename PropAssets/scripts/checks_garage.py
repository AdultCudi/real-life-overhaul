"""What Scripts/Garage and the garage clips (CityAssets/scripts/clips_garage.py) rely on in the garage props: where a
rider's hands, feet and seat are, the axles, the sizes and pivots GarageCar scales and places the car parts by, where
the dock's hoops are. The numbers are BikeCatalogue.All (BikeBody.cs) and the constants of GarageCar.cs and
BikePlaces.cs, in the kit's axes (x right, y forward, z up). See check_life.py."""
import json
import os

import numpy as np

CRANK, PEDAL_OUT, PEDAL = 0.17, 0.13, (0.09, 0.075, 0.03)
BIKES = {
    "bicycle_city": {"axles": (-0.52, 0.58), "wheel": 0.34, "seat": (-0.30, 0.93), "grip": (0.26, 0.16, 1.14), "crank": (-0.08, 0.29), "long": 1.78, "head": (0.36, 1.00)},
    "bicycle_road": {"axles": (-0.49, 0.55), "wheel": 0.34, "seat": (-0.29, 0.95), "grip": (0.13, 0.42, 1.00), "crank": (-0.07, 0.28), "long": 1.72, "head": (0.36, 0.99)},
    "motorbike_standard": {"axles": (-0.651, 0.723), "wheel": 0.33, "seat": (-0.20, 0.83), "grip": (0.31, 0.30, 1.075), "peg": (0.24, -0.10, 0.34), "long": 2.05, "head": (0.40, 0.98)},
    "motorbike_sport": {"axles": (-0.651, 0.723), "wheel": 0.33, "seat": (-0.22, 0.84), "grip": (0.27, 0.42, 0.915), "peg": (0.24, -0.16, 0.38), "long": 2.05, "head": (0.41, 0.96)},
}
NEUTRAL = "garage_white"                              # the swatch the paint parts are on (BikeBody tints it)
BIKE_HALF = (0.40, 0.50, 1.00)                        # BikePlaces.BikeHalf: the room a bike is given (half width, half height, half length)
HEAD, HEAD_CLEAR, HEAD_BONE = (0.076, 0.097), 0.02, 0.113
DOCK_LINE, DOCK_HOOPS, DOCK_POST = 0.80, (-0.80, 0.0, 0.80), 1.38


def middle(values):
    return float(values.max() + values.min()) / 2


def wheels(c, name, axles, radius, tolerance=0.004):
    """The tyres' outsides: the lowest point under each axle is the ground, the wheels are where the code has them."""
    v = c.kit(name)
    c.extent(name, "stands on the ground", v[:, 2].min(), 0.0, 0.002)
    for y in axles:
        out = np.hypot(v[:, 1] - y, v[:, 2] - radius)
        tread = (np.abs(v[:, 0]) < 0.02) & (out > radius - 0.04) & (out < radius + 0.004)       # the mudguards are further out than that
        c.extent(name, "wheel at y %+.3f: radius about its axle" % y, out[tread].max(), radius, tolerance)
        c.report(tread.sum() >= 16, "%-19s wheel at y %+.3f: a tyre all the way round (%d points of its tread)" % (name, y, tread.sum()))


def bikes(c):
    for name, e in BIKES.items():
        v = c.kit(name)
        wheels(c, name, e["axles"], e["wheel"], 0.006 if "crank" in e else 0.004)
        c.extent(name, "length", np.ptp(v[:, 1]), e["long"], 0.03)
        c.report(np.abs(v[:, 0]).max() <= BIKE_HALF[0] and np.abs(v[:, 1]).max() <= BIKE_HALF[2] + 0.06,
                 "%-19s inside the room BikePlaces gives it, but for the front tyre's end (%.2f wide, from y %.2f to %.2f)" % (name, np.ptp(v[:, 0]), v[:, 1].min(), v[:, 1].max()))
        for side in (-1, 1):
            c.grip(name, (side * e["grip"][0], e["grip"][1], e["grip"][2]), (1, 0, 0), reach=0.04, thick=(0.028, 0.035), within=0.02)
        if "crank" in e:
            by, bz = e["crank"]
            c.extent(name, "saddle top at its middle", c.top_near(name, 0.0, e["seat"][0], 0.02, 0.09), e["seat"][1], 0.002)
            for side, forward in ((1.0, 1.0), (-1.0, -1.0)):         # as BikeBody.Pedals(def, 0): the right pedal forward, the left back, their middles on the crank circle
                y = by + forward * CRANK
                pedal = v[(np.abs(v[:, 0] - side * PEDAL_OUT) <= PEDAL[0] / 2 + 0.001) & (np.abs(v[:, 1] - y) <= PEDAL[1] / 2 + 0.001) & (np.abs(v[:, 2] - bz) < 0.04)]
                word = "right" if side > 0 else "left"
                c.extent(name, "%s pedal: middle, height" % word, middle(pedal[:, 2]), bz, 0.001)
                c.extent(name, "%s pedal: middle, fore and aft" % word, middle(pedal[:, 1]), y, 0.002)
                c.extent(name, "%s pedal: middle, out from the centre line" % word, middle(pedal[:, 0]), side * PEDAL_OUT, 0.002)
                c.extent(name, "%s pedal: thickness" % word, np.ptp(pedal[:, 2]), PEDAL[2], 0.002)
        else:
            c.extent(name, "seat top under the rider", c.top_near(name, 0.0, e["seat"][0], 0.13, 0.13, below=e["seat"][1] + 0.003), e["seat"][1], 0.002)
            for side in (-1, 1):
                c.grip(name, (side * e["peg"][0], e["peg"][1], e["peg"][2]), (1, 0, 0), reach=0.03, thick=(0.028, 0.034), within=0.02)


def has(c, name):
    return os.path.exists(os.path.join(c.BUILD, "meshes", name + ".json"))


def parts(c):
    """The same bikes as BikeBody.Build assembles them: every part about the point the code turns it about, the
    grips on the steering assembly, and the paint apart on the neutral swatch."""
    with open(os.path.join(c.BUILD, "atlas.json")) as f:
        u0, v0, u1, v1 = json.load(f)["regions"][NEUTRAL]
    for name, e in BIKES.items():
        hy, hz = e["head"]
        r = e["wheel"]
        placed = [(name + "_frame", (0.0, 0.0, 0.0)), (name + "_fork", (0.0, hy, hz))]
        placed += [(name + key, where) for key, where in (("_frame_paint", (0.0, 0.0, 0.0)), ("_fork_paint", (0.0, hy, hz))) if has(c, name + key)]
        wheels_ = ("_wheel", "_wheel") if "crank" in e else ("_wheel_rear", "_wheel_front")
        placed += [(name + wheels_[0], (0.0, e["axles"][0], r)), (name + wheels_[1], (0.0, e["axles"][1], r))]
        if "crank" in e:
            by, bz = e["crank"]
            placed += [(name + "_crank", (0.0, by, bz)), (name + "_pedal", (PEDAL_OUT, by + CRANK, bz)), (name + "_pedal", (-PEDAL_OUT, by - CRANK, bz))]
        together = np.concatenate([c.kit(part) + np.array(where) for part, where in placed])
        whole = c.kit(name)
        off = max(np.abs(together.min(axis=0) - whole.min(axis=0)).max(), np.abs(together.max(axis=0) - whole.max(axis=0)).max())
        c.report(off < 0.0005 and len(together) >= len(whole) * 0.95, "%-19s its parts, put where BikeBody.Build puts them, are the whole bike (bounds within %.1f mm, %d parts)" % (name, off * 1000, len(placed)))
        c.report(has(c, name + "_frame_paint"), "%-19s has a paint part on the frame" % name)
        for side in (-1, 1):
            c.grip(name + "_fork", (side * e["grip"][0], e["grip"][1] - hy, e["grip"][2] - hz), (1, 0, 0), reach=0.04, thick=(0.028, 0.035), within=0.02)
        for key in sorted(set(wheels_)):
            v = c.kit(name + key)
            c.extent(name + key, "radius about its own axle", np.hypot(v[:, 1], v[:, 2]).max(), r, 0.006 if "crank" in e else 0.004)
        if "crank" in e:
            v = c.kit(name + "_crank")
            c.extent(name + "_crank", "the right crank forward: reaches +Y", v[v[:, 0] > 0.06][:, 1].max(), CRANK, 0.012)
            c.extent(name + "_crank", "the left crank back: reaches -Y", v[v[:, 0] < -0.06][:, 1].min(), -CRANK, 0.012)
            v = c.kit(name + "_pedal")
            for axis, want in enumerate(PEDAL):
                c.extent(name + "_pedal", "xyz"[axis] + ", centred", np.ptp(v[:, axis]) + abs(middle(v[:, axis])), want, 0.002)
        for key in ("_frame_paint", "_fork_paint"):
            if not has(c, name + key):
                continue
            with open(os.path.join(c.BUILD, "meshes", name + key + ".json")) as f:
                uv = np.array(json.load(f)["uv"]).reshape(-1, 2)
            inside = (uv[:, 0] >= u0) & (uv[:, 0] <= u1) & (uv[:, 1] >= min(v0, v1)) & (uv[:, 1] <= max(v0, v1))
            c.report(bool(inside.all()), "%-19s every face is on the neutral swatch %s (%d of %d corners)" % (name + key, NEUTRAL, inside.sum(), len(uv)))


def dock(c):
    """Hoops in a line 0.80 m ahead of the pivot at x -0.8, 0, +0.8, the pay post at +1.38; a city bicycle standing
    at a hoop's x on the pivot's line (as BikePlaces.Display stands it) has its front wheel between the legs."""
    v = c.kit("bike_dock")
    c.extent("bike_dock", "stands on the ground", v[:, 2].min(), 0.0, 0.001)
    c.extent("bike_dock", "width", np.ptp(v[:, 0]), 2.60, 0.01)
    c.extent("bike_dock", "height", v[:, 2].max(), 1.25, 0.005)
    c.report(np.ptp(v[:, 1]) <= 0.20 and v[:, 1].min() > 0.70, "%-19s %.3f m deep, from y %.3f to %.3f" % ("bike_dock", np.ptp(v[:, 1]), v[:, 1].min(), v[:, 1].max()))
    post = v[v[:, 2] > 1.2]
    c.extent("bike_dock", "pay post x", middle(post[:, 0]), DOCK_POST, 0.005)
    c.extent("bike_dock", "pay post y", middle(post[:, 1]), DOCK_LINE, 0.005)
    bike, b = BIKES["bicycle_city"], c.kit("bicycle_city")
    through = b[(b[:, 1] > DOCK_LINE - 0.03) & (b[:, 1] < DOCK_LINE + 0.03)]           # what of the bicycle is in the hoops' plane
    low = through[through[:, 2] < 0.70]
    for x in DOCK_HOOPS:
        hoop = v[(np.abs(v[:, 0] - x) < 0.085) & (v[:, 2] > 0.08) & (v[:, 2] < 0.75)]
        legs = hoop[(hoop[:, 2] > 0.62) & (hoop[:, 2] < 0.66)]            # the legs where they begin to bend over (above the lock)
        c.extent("bike_dock", "hoop at x %+.1f: in the line" % x, middle(legs[:, 1]), DOCK_LINE, 0.003)
        c.extent("bike_dock", "hoop at x %+.1f: centred" % x, middle(legs[:, 0]), x, 0.003)
        c.extent("bike_dock", "hoop at x %+.1f: top" % x, hoop[:, 2].max(), 0.723, 0.006)
        gap = np.abs(legs[:, 0] - x).min()
        c.report(np.abs(low[:, 0]).max() < gap - 0.008, "%-19s hoop at x %+.1f: %.3f m between its legs, the bicycle is %.3f m wide there" % ("bike_dock", x, 2 * gap, 2 * np.abs(low[:, 0]).max()))
    inside = v[(np.abs(v[:, 0]) < 0.04) & (v[:, 2] > 0.5) & (v[:, 2] < 0.75)]
    c.report(low[:, 2].max() < inside[:, 2].min() - 0.01, "%-19s the docked wheel and mudguard (to %.3f) pass under the hoop's top (from %.3f)" % ("bike_dock", low[:, 2].max(), inside[:, 2].min()))
    above = through[through[:, 2] >= 0.70]
    c.report(len(above) == 0 or above[:, 2].min() > 0.745, "%-19s nothing else of the docked bicycle is in the hoop (basket and lamp are above or behind it)" % "bike_dock")
    rail = v[(v[:, 2] < 0.05) & (np.abs(v[:, 0]) < 0.3)]
    tyre = bike["wheel"] - np.sqrt(bike["wheel"] ** 2 - (rail[:, 1].min() - bike["axles"][1]) ** 2)
    c.report(rail[:, 2].max() < tyre, "%-19s the rail (top %.3f) is under the docked tyre (%.3f up where the rail begins)" % ("bike_dock", rail[:, 2].max(), tyre))


def run(c):
    print("Garage props: the rider's contact points, axles, sizes and pivots, as Scripts/Garage has them")
    bikes(c)
    parts(c)
    # The helmets: the pivot is the Head bone; nothing inside the head's space, the chin and crown where they are said to be.
    inside = (HEAD[0] + HEAD_CLEAR, HEAD[1] + HEAD_CLEAR)
    v = c.kit("helmet")
    low = v[v[:, 2] < HEAD_BONE + 0.001]
    reach = (low[:, 0] / inside[0]) ** 2 + (low[:, 1] / inside[1]) ** 2
    c.report(reach.min() > 0.999, "%-19s nothing within %.3f x %.3f m of the head's middle below the band (nearest at %.3f of it)" % ("helmet", *inside, reach.min() ** 0.5))
    c.extent("helmet", "chin, below the Head bone", v[:, 2].min(), -0.080, 0.004)
    c.extent("helmet", "crown, above the Head bone", v[:, 2].max(), 0.223, 0.006)
    c.extent("helmet", "width", np.ptp(v[:, 0]), 0.27, 0.02)
    c.extent("helmet", "length", np.ptp(v[:, 1]), 0.31, 0.02)
    c.report(v[:, 1].max() > -v[:, 1].min(), "%-19s the visor is towards +Y, Unity +Z (it reaches y %+.3f, the back %+.3f)" % ("helmet", v[:, 1].max(), v[:, 1].min()))
    v = c.kit("bicycle_helmet")
    low = v[v[:, 2] < HEAD_BONE + 0.021]
    reach = (low[:, 0] / (HEAD[0] + 0.008)) ** 2 + (low[:, 1] / (HEAD[1] + 0.008)) ** 2
    c.report(reach.min() ** 0.5 > 0.92, "%-19s its rim clears the head by 0.008 m (the straps and buckles lie 0.005 m off it: nearest at %.3f of the rim)" % ("bicycle_helmet", reach.min() ** 0.5))
    c.extent("bicycle_helmet", "crown, above the Head bone", v[:, 2].max(), HEAD_BONE + 0.118, 0.004)
    # Whole wheels: GarageCar.WheelPropRadius 0.34, WheelPropWidth 0.22, the axle along X, the outer face at -X.
    for name in ("wheel_5spoke", "wheel_mesh", "wheel_steel"):
        v = c.kit(name)
        c.extent(name, "radius (up)", np.ptp(v[:, 2]) / 2, 0.34, 0.001)
        c.extent(name, "radius (fore and aft)", np.ptp(v[:, 1]) / 2, 0.34, 0.006)
        c.extent(name, "width along the axle", np.ptp(v[:, 0]), 0.22, 0.001)
        c.extent(name, "centred on the axle", abs(middle(v[:, 2])) + abs(middle(v[:, 1])) + abs(middle(v[:, 0])), 0.0, 0.003)
        hub = v[np.hypot(v[:, 1], v[:, 2]) < 0.10]
        c.report(hub[:, 0].min() < -0.07 and hub[:, 0].max() < 0.105, "%-19s the face (hub cap, nuts) is at -X: the hub reaches x %+.3f; the back is closed at x %+.3f" % (name, hub[:, 0].min(), hub[:, 0].max()))
        tread = v[np.hypot(v[:, 1], v[:, 2]) > 0.335]
        c.report(len(tread) >= 40, "%-19s a tyre all the way round (%d points of its tread)" % (name, len(tread)))
    # The car parts: the sizes GarageCar.Fit divides by, and the pivots it places them at.
    v = c.kit("car_spoiler")
    c.extent("car_spoiler", "width (the code scales X by half / 0.70)", np.ptp(v[:, 0]), 1.40, 0.004)
    c.extent("car_spoiler", "top above the deck", v[:, 2].max(), 0.18, 0.003)
    c.extent("car_spoiler", "depth", np.ptp(v[:, 1]), 0.26, 0.004)
    c.extent("car_spoiler", "feet sunk below the pivot", v[:, 2].min(), -0.02, 0.002)
    feet = v[(v[:, 2] < 0.0) & (v[:, 0] > 0)]
    c.extent("car_spoiler", "feet either side of the middle", middle(feet[:, 0]), 0.42, 0.003)
    c.extent("car_spoiler", "centred fore and aft on its feet", middle(feet[:, 1]), 0.0, 0.004)
    v = c.kit("car_roof_bar")
    c.extent("car_roof_bar", "length (the code scales X by (half + 0.04) / 0.60)", np.ptp(v[:, 0]), 1.20, 0.004)
    c.extent("car_roof_bar", "top above the roof", v[:, 2].max(), 0.11, 0.002)
    c.extent("car_roof_bar", "depth", np.ptp(v[:, 1]), 0.06, 0.002)
    c.extent("car_roof_bar", "feet sunk below the pivot", v[:, 2].min(), -0.02, 0.002)
    feet = v[(v[:, 2] < 0.0) & (v[:, 0] > 0)]
    c.extent("car_roof_bar", "feet either side of the middle", middle(feet[:, 0]), 0.52, 0.003)
    bar = v[np.abs(v[:, 0]) > 0.585]                                 # its ends: the bar is one straight section
    c.report(bar[:, 2].min() > 0.065, "%-19s the bar clears a roof crowned 0.06 m over its feet (underside at %.3f)" % ("car_roof_bar", bar[:, 2].min()))
    v = c.kit("car_bull_bar")
    c.extent("car_bull_bar", "width (the code scales X by half / 0.50)", np.ptp(v[:, 0]), 1.00, 0.004)
    c.extent("car_bull_bar", "height (the code scales Y by height / 0.60)", v[:, 2].max(), 0.60, 0.004)
    c.extent("car_bull_bar", "bottom of the hoop at the pivot", v[:, 2].min(), 0.0, 0.004)
    c.extent("car_bull_bar", "front of the tubes ahead of the pivot", v[:, 1].max(), 0.02, 0.003)
    c.extent("car_bull_bar", "mounts behind the pivot", v[:, 1].min(), -0.12, 0.002)
    mounts = v[v[:, 1] < -0.05]
    c.extent("car_bull_bar", "mounts either side of the middle", np.abs(mounts[:, 0]).mean(), 0.275, 0.004)
    c.extent("car_bull_bar", "mounts: height", middle(mounts[:, 2]), 0.14, 0.004)
    for name, size in (("car_spoiler_lip", (1.40, 0.18, 0.06)), ("bike_dock_kiosk", (0.50, 0.30, 1.70))):
        v = c.kit(name)
        for axis, want in enumerate(size):
            c.extent(name, "xyz"[axis], np.ptp(v[:, axis]), want, 0.008)
        c.extent(name, "underside at the pivot", v[:, 2].min(), 0.0, 0.001)
    dock(c)
