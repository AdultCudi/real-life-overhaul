"""What the code relies on in the service vehicles (check_life.py runs this): Transport/TransitBus.Dress puts the
minibus's axles on those of the van under it (BodyAxleFront, BodyAxleRear), stands its route board on the roof at the
height of the minibus's bounds 2.22 m ahead of the middle (BodyBoardAhead) and the line number 2.95 m behind it
(BodyNumberBack); the people board 0.76 m ahead of the middle on +X (BodyDoorAhead). Events/EventRoad.EngineLivery puts
the fire engine's axles on the delivery truck's (BodyAxleFront, BodyAxleRear) and a lit lens over each end of its light
bar (BarAt); FireScene.PlanCrew stands the pump operator 0.50 m off the panel and lays the hose from the outlet.
Kit space: x right, y forward, z up."""
import numpy as np


def surface(c, name, x, y):
    """The height of the highest surface of a prop over a point (the vertices are too few on a flat roof to ask them)."""
    v, triangles = c.kit(name, True)
    a, b, d = v[triangles[:, 0]], v[triangles[:, 1]], v[triangles[:, 2]]
    den = (b[:, 1] - d[:, 1]) * (a[:, 0] - d[:, 0]) + (d[:, 0] - b[:, 0]) * (a[:, 1] - d[:, 1])
    ok = np.abs(den) > 1e-9
    den = np.where(ok, den, 1.0)
    wa = ((b[:, 1] - d[:, 1]) * (x - d[:, 0]) + (d[:, 0] - b[:, 0]) * (y - d[:, 1])) / den
    wb = ((d[:, 1] - a[:, 1]) * (x - d[:, 0]) + (a[:, 0] - d[:, 0]) * (y - d[:, 1])) / den
    wd = 1.0 - wa - wb
    inside = ok & (wa >= -1e-6) & (wb >= -1e-6) & (wd >= -1e-6)
    return float((wa * a[:, 2] + wb * b[:, 2] + wd * d[:, 2])[inside].max()) if inside.any() else -1.0


def run(c):
    v = c.kit("minibus")
    c.extent("minibus", "height (the roof the board stands on)", v[:, 2].max(), 2.75, 0.0015)
    c.extent("minibus", "width", np.ptp(v[:, 0]), 2.10, 0.005)
    c.extent("minibus", "length (the Mersaidi Dash is 6.66)", np.ptp(v[:, 1]), 6.66, 0.01)
    c.extent("minibus", "middle of its length at the pivot", (v[:, 1].max() + v[:, 1].min()) / 2, 0.0, 0.01)
    for x in (-0.75, 0.0, 0.75):
        for y in (1.60, 2.22, 2.40):
            c.extent("minibus", "roof under the route board at (%.2f, %.2f)" % (x, y), surface(c, "minibus", x, y), 2.75, 0.0015)
    for x in (-0.25, 0.25):
        for y in (-3.13, -2.95, -2.73):
            c.extent("minibus", "roof under the line number at (%.2f, %.2f)" % (x, y), surface(c, "minibus", x, y), 2.75, 0.0045)
    door = v[(np.abs(v[:, 0] - 0.98) < 0.003) & (v[:, 2] > 0.45) & (v[:, 2] < 2.0) & (v[:, 1] > 0.0) & (v[:, 1] < 1.5)]          # the leaves, set back
    c.extent("minibus", "middle of the passenger door (y)", (door[:, 1].max() + door[:, 1].min()) / 2, 0.76, 0.02)
    for name, radius, axles in (("minibus", 0.375, (2.40, -1.62)), ("fire_engine", 0.51, (2.756, -1.362))):          # the Mersaidi Dash's axles, the Delivery Truck's
        w = c.kit(name)
        c.extent(name, "stands on the ground", w[:, 2].min(), 0.0, 0.001)
        for y in axles:
            tyre = w[(np.abs(w[:, 1] - y) < radius + 0.02) & (w[:, 2] < 0.02)]
            c.extent(name, "a tyre on the ground at y %.2f" % y, (tyre[:, 1].max() + tyre[:, 1].min()) / 2 if len(tyre) else 9.0, y, 0.02)
    e = c.kit("fire_engine")
    c.extent("fire_engine", "height (under 3.5 m: TrafficEffects.RoofHeight)", e[:, 2].max(), 3.20, 0.03)
    c.extent("fire_engine", "width (the Delivery Truck is 2.72)", np.ptp(e[:, 0]), 2.72, 0.02)
    c.extent("fire_engine", "length (the Delivery Truck is 7.43)", np.ptp(e[:, 1]), 7.43, 0.01)
    c.extent("fire_engine", "middle of its length at the pivot", (e[:, 1].max() + e[:, 1].min()) / 2, 0.0, 0.01)
    for side in (-1, 1):
        panel = e[(np.abs(e[:, 0] - side * 1.22) < 0.002) & (e[:, 2] > 0.6) & (e[:, 2] < 2.2) & (e[:, 1] > 0.0) & (e[:, 1] < 1.5)]
        c.extent("fire_engine", "middle of the pump panel on %s (y)" % ("+X" if side > 0 else "-X"), (panel[:, 1].max() + panel[:, 1].min()) / 2 if len(panel) else 9.0, 0.756, 0.01)
        outlet = e[(e[:, 0] * side > 1.285) & (np.abs(e[:, 2] - 0.76) < 0.09) & (e[:, 1] > -1.0) & (e[:, 1] < 0.3)]
        c.extent("fire_engine", "outlet on %s (y)" % ("+X" if side > 0 else "-X"), (outlet[:, 1].max() + outlet[:, 1].min()) / 2 if len(outlet) else 9.0, -0.333, 0.01)
    bar = e[(e[:, 2] > 3.03) & (np.abs(e[:, 0]) < 0.76) & (e[:, 1] > 2.3) & (e[:, 1] < 2.8)]
    c.extent("fire_engine", "middle of the light bar (y)", (bar[:, 1].max() + bar[:, 1].min()) / 2 if len(bar) else 9.0, 2.535, 0.01)
    c.extent("fire_engine", "top of the light bar", bar[:, 2].max() if len(bar) else 9.0, 3.20, 0.005)
    lens = c.kit("fire_bar_lens")
    c.extent("fire_bar_lens", "reaches the end of the bar (x)", lens[:, 0].max(), 0.753, 0.002)
    c.extent("fire_bar_lens", "its top, over the bar's (3.20 - 3.006)", lens[:, 2].max(), 0.194, 0.002)
    c.extent("fire_bar_lens", "depth", np.ptp(lens[:, 1]), 0.295, 0.003)
    b = c.kit("fire_light_bar")
    for axis, want in enumerate((1.30, 0.28, 0.17)):
        c.extent("fire_light_bar", "xyz"[axis], np.ptp(b[:, axis]), want, 0.002)
    c.extent("fire_light_bar", "underside at the pivot", b[:, 2].min(), 0.0, 0.0005)
