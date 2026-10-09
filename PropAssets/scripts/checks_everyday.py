"""What clips and code rely on in the everyday props (props_everyday.py), measured on the built meshes: check_life.py
calls run(c) with itself. Kit space: x right, y forward, z up (Unity's x, z, y)."""
import numpy as np


def run(c):
    print("Everyday props: grips, pivots and sizes")
    size = lambda v, axis: float(np.ptp(v[:, axis]))

    # Home.
    c.grip("pan", (0.23, 0, 0.045), (1, 0, 0), thick=(0.027, 0.029))
    v = c.kit("pan")
    c.extent("pan", "across the rim", size(v, 1), 0.26)
    c.extent("pan", "depth of the pan itself", v[v[:, 0] < 0.11][:, 2].max(), 0.05)
    c.grip("pan", (0.19, 0, 0.045), (1, 0, 0), thick=(0.027, 0.029))
    c.grip("pan", (0.27, 0, 0.045), (1, 0, 0), thick=(0.027, 0.029))
    c.extent("pan", "the handle ends at x", v[:, 0].max(), 0.31)
    c.extent("pan", "base on the pivot", v[:, 2].min(), 0.0, 0.0005)
    v = c.kit("plate")
    c.extent("plate", "across", size(v, 0), 0.26)
    c.extent("plate", "base on the pivot", v[:, 2].min(), 0.0, 0.0005)
    v = c.kit("mug")
    body = v[(v[:, 0] < 0.03) & (v[:, 2] > 0.003) & (v[:, 2] < 0.098) & (np.hypot(v[:, 0], v[:, 1]) > 0.0375)]
    c.extent("mug", "body across where it is held (0.05 up)", size(body, 1), 0.08, 0.001)
    walls = v[(v[:, 2] > 0.004) & (v[:, 2] < 0.097) & (np.hypot(v[:, 0], v[:, 1]) > 0.0375) & (v[:, 0] < 0.03)]
    c.report(len(walls) == 0 or float(np.ptp(np.hypot(walls[:, 0], walls[:, 1]))) < 0.0005, "%-19s a plain cylinder between 0.004 and 0.097" % "mug")
    c.extent("mug", "height", v[:, 2].max(), 0.10)
    inside = v[(np.hypot(v[:, 0], v[:, 1]) < 0.037) & (v[:, 2] > 0.01)]
    c.extent("mug", "coffee below the rim", 0.10 - inside[:, 2].min(), 0.012, 0.001)
    c.report(v[:, 0].max() > 0.065 and v[:, 0].min() > -0.0405, "%-19s handle at +X (to %.3f)" % ("mug", v[:, 0].max()))
    v = c.kit("run_checkpoint")
    c.extent("run_checkpoint", "across", size(v, 0), 1.6, 0.005)
    c.extent("run_checkpoint", "height", v[:, 2].max(), 1.4, 0.005)

    # An evening out.
    c.grip("wine_glass", (0, 0, 0), (0, 0, 1), reach=0.02, thick=(0.007, 0.011), within=0.02)
    v = c.kit("wine_glass")
    c.extent("wine_glass", "foot below the pivot", v[:, 2].min(), -0.085, 0.001)
    c.extent("wine_glass", "rim above the pivot", v[:, 2].max(), 0.115, 0.001)
    c.extent("wine_glass", "across", size(v, 0), 0.07, 0.001)
    c.grip("beer_bottle", (0, 0, 0), (0, 0, 1), reach=0.05, thick=(0.062, 0.070), within=0.04)
    c.grip("beer_bottle", (0, 0, -0.01), (0, 0, 1), reach=0.05, thick=(0.062, 0.070), within=0.04)       # plain from -0.06 to +0.05
    v = c.kit("beer_bottle")
    c.extent("beer_bottle", "base below the pivot", v[:, 2].min(), -0.08, 0.001)
    c.extent("beer_bottle", "height", size(v, 2), 0.24, 0.001)
    for name, want in (("takeaway_box", (0.14, 0.11, 0.09)), ("popcorn_tub", (0.16, 0.16, 0.20)), ("gift_box", (0.20, 0.20, 0.14)), ("moving_box", (0.45, 0.35, 0.35)),
                       ("flowers_bunch", (0.22, 0.22, 0.42)), ("gym_bag", (0.50, 0.24, 0.26)), ("drink_glass", (0.075, 0.075, 0.12)), ("cocktail_glass", (0.11, 0.11, 0.16)),
                       ("shopping_bag", (0.32, 0.12, 0.38)), ("fishing_float", (0.045, 0.045, 0.11)), ("clothes_rack", (1.44, 0.56, 1.76)), ("fuel_pump", (0.5, 0.4, 1.6)),
                       ("jerry_can", (0.35, 0.18, 0.45)), ("warning_triangle", (0.45, 0.05, 0.42)), ("car_wash_arch", (3.6, 1.0, 3.2)), ("dispatch_desk", (1.2, 0.5, 1.4)),
                       ("window_boarded", (1.2, 0.05, 1.5)), ("employer_sign", (0.6, 0.03, 0.3)), ("employer_sign_6", (0.6, 0.03, 0.3))):
        v = c.kit(name)
        for axis, length in enumerate(want):
            loose = 0.006 if (axis == 1 and name in ("cocktail_glass", "drink_glass", "popcorn_tub")) else 0.0      # ten or twelve sides: narrower across the flats
            c.extent(name, "xyz"[axis], size(v, axis), length, loose + (max(0.005, length * 0.012) if name not in ("takeaway_box", "moving_box") else 0.004))
    for name in ("takeaway_box", "popcorn_tub", "gift_box", "moving_box", "drink_glass", "cocktail_glass", "clothes_rack", "fuel_pump", "jerry_can", "warning_triangle", "car_wash_arch",
                 "dispatch_desk", "turnstile", "window_boarded", "closed_notice", "run_checkpoint"):
        c.extent(name, "stands on its pivot", c.kit(name)[:, 2].min(), 0.0, 0.0005)
    c.grip("flowers_bunch", (0, 0, 0), (0, 0, 1), thick=(0.035, 0.045))
    v = c.kit("flowers_bunch")
    c.extent("flowers_bunch", "stems below the pivot", v[:, 2].min(), -0.10, 0.001)
    c.grip("gym_bag", (0, 0, 0), (1, 0, 0), reach=0.045, thick=(0.029, 0.031))
    v = c.kit("gym_bag")
    c.extent("gym_bag", "top of the bag under the pivot", v[(np.abs(v[:, 0]) > 0.125) & (np.abs(v[:, 1]) < 0.03)][:, 2].max(), -0.07, 0.01)
    c.extent("gym_bag", "underside below the pivot", v[:, 2].min(), -0.245, 0.002)
    a, b = c.kit("newspaper"), c.kit("newspaper_ledger")
    c.report(a.shape == b.shape and float(np.abs(a - b).max()) < 1e-6, "%-19s sheet for sheet the newspaper" % "newspaper_ledger")

    # Leisure.
    c.grip("fishing_rod", (0, 0.10, 0), (0, 1, 0), thick=(0.027, 0.029))
    c.grip("fishing_rod", (0, 0.52, 0), (0, 1, 0), thick=(0.027, 0.029))
    v = c.kit("fishing_rod")
    c.extent("fishing_rod", "butt at the pivot", v[:, 1].min(), 0.0, 0.0005)
    c.extent("fishing_rod", "length", v[:, 1].max(), 2.10, 0.002)
    seat = v[(v[:, 1] > 0.34) & (v[:, 1] < 0.46) & (v[:, 2] > -0.010)]
    c.extent("fishing_rod", "reel seat, thickness over the top", 2 * float(np.hypot(seat[:, 0], seat[:, 2]).max()), 0.032, 0.0005)
    handle = v[(v[:, 1] < 0.555) & (v[:, 2] > -0.010)]
    c.report(float(np.hypot(handle[:, 0], handle[:, 2]).max()) <= 0.0161, "%-19s handle on the axis from 0 to 0.56, nothing proud of it on top" % "fishing_rod")
    knob = v[v[:, 0] < -0.047]
    centre = (knob.max(axis=0) + knob.min(axis=0)) / 2
    c.report(float(np.abs(centre - np.array((-0.06, 0.40, -0.06))).max()) < 0.008, "%-19s crank knob at (%.3f, %.3f, %.3f), wanted about (-0.06, 0.40, -0.06)" % ("fishing_rod", *centre))
    reel = v[(v[:, 2] < -0.02) & (v[:, 1] < 0.6)]
    c.extent("fishing_rod", "reel under the rod at y", (reel[:, 1].max() + reel[:, 1].min()) / 2, 0.40, 0.03)
    tip = v[v[:, 1] > 2.0]
    c.report(float(np.abs(tip[:, 0]).max()) < 0.008, "%-19s tip on the axis" % "fishing_rod")
    v = c.kit("fishing_float")
    c.extent("fishing_float", "above the waterline", v[:, 2].max(), 0.07, 0.001)
    v = c.kit("fishing_line")
    c.extent("fishing_line", "from the pivot", v[:, 1].min(), 0.0, 0.0001)
    c.extent("fishing_line", "unit length", v[:, 1].max(), 1.0, 0.0001)
    c.extent("fishing_line", "thread across", size(v, 0), 0.004, 0.0002)
    v = c.kit("drink_glass")
    held = v[np.abs(v[:, 2] - 0.06) < 0.05]
    c.report(0.062 <= size(held, 1) <= 0.074, "%-19s %.3f across where the cup grip holds it" % ("drink_glass", size(held, 1)))
    c.grip("clothes_rack", (0.09, 0, 1.55), (1, 0, 0), thick=(0.033, 0.037))
    c.grip("clothes_rack", (0.63, 0, 1.55), (1, 0, 0), thick=(0.033, 0.037))
    v = c.kit("clothes_rack")
    hooks = v[(np.abs(v[:, 2] - 1.55) < 0.03) & (v[:, 1] > 0.0185) & (np.abs(v[:, 0]) < 0.69)]
    c.report(len(hooks) > 0 and float(np.diff(np.unique(np.round(hooks[:, 0], 2))).min()) >= 0.16, "%-19s 0.16 m or more of clear rail between hooks on the browsing side" % "clothes_rack")
    v = c.kit("shopping_bag")
    c.extent("shopping_bag", "top of the handles at the pivot", v[:, 2].max(), 0.0, 0.0005)

    # Transport.
    c.extent("turnstile", "reader height", c.top_near("turnstile", 0, 0.455, 0.08), 0.975, 0.002)
    v = c.kit("turnstile")
    cabinet = v[v[:, 2] < 0.5]
    c.extent("turnstile", "cabinet width", size(cabinet[cabinet[:, 0] < 0.151], 0), 0.30, 0.003)
    c.extent("turnstile", "length", size(v, 1), 1.20, 0.003)
    c.extent("turnstile", "height", v[:, 2].max(), 1.00, 0.003)
    c.grip("jerry_can", (0, 0, 0.415), (1, 0, 0), reach=0.05, thick=(0.029, 0.031))
    v = c.kit("jerry_can")
    under = v[(np.abs(v[:, 1]) > 0.04) & (v[:, 2] < 0.395)]              # the can's flat top (its corners: the handle and the caps are nearer the middle line)
    c.extent("jerry_can", "clear under the handle", 0.415 - 0.015 - under[:, 2].max(), 0.03, 0.002)
    v = c.kit("car_wash_arch")
    low = v[v[:, 2] < 2.40]
    c.report(float(np.abs(low[:, 0]).min()) >= 1.2995, "%-19s %.3f m clear between the sides (2.60 wanted)" % ("car_wash_arch", 2 * float(np.abs(low[:, 0]).min())))
    c.report(float(v[np.abs(v[:, 0]) < 1.29][:, 2].min()) >= 2.40, "%-19s %.3f m clear under the top brush (2.40 wanted)" % ("car_wash_arch", float(v[np.abs(v[:, 0]) < 1.29][:, 2].min())))

    # Careers and business.
    for name in ["employer_sign"] + ["employer_sign_%d" % k for k in range(1, 7)] + ["window_boarded"]:
        c.extent(name, "back face on the pivot", c.kit(name)[:, 1].min(), 0.0, 0.0005)
    v = c.kit("employer_sign_3")
    c.extent("employer_sign_3", "centred on the pivot", (v[:, 2].max() + v[:, 2].min()) / 2, 0.0, 0.0005)
    v = c.kit("closed_notice")
    c.extent("closed_notice", "height", v[:, 2].max(), 1.30, 0.01)
    v = c.kit("dispatch_desk")
    c.extent("dispatch_desk", "front edge of the desk (lip included)", v[v[:, 1] > 0.20][:, 2].max(), 0.99, 0.02)
    c.extent("dispatch_desk", "top of the map board", v[v[:, 1] < -0.20][:, 2].max(), 1.40, 0.003)
