"""What the code relies on in the tech module's props (props_tech.py), measured on the built meshes by check_life.py.
Kit space: x right, y forward, z up (Unity's x, z, y). The numbers are those of Scripts/Tech/Sites/SiteProps.cs (the sizes
the code stands each model by and the boxes it draws until the bundle has it) and Scripts/Tech/Sites/SitePlan.cs
(RackWidth 0.6, RackDepth 1.07, AiDepth 1.2, TrayHeight 2.45, Panel 2.4, VentWidth 0.6)."""
import numpy as np


def run(c):
    print("Tech props: footprints, fronts and pivots")

    def size(name, want, tolerance=0.006):
        v = c.kit(name)
        for axis, value in zip("xyz", want):
            if value is not None:
                c.extent(name, "size " + axis, float(np.ptp(v[:, "xyz".index(axis)])), value, tolerance)
        return v

    def grounded(name, v):
        c.extent(name, "stands on the ground", float(v[:, 2].min()), 0.0, 0.0005)

    for name, depth, front in (("dc_rack", 1.07, 0.535), ("dc_rack_network", 1.07, 0.535), ("dc_rack_ai", 1.20, 0.600)):
        v = size(name, (0.60, depth, None), 0.035 if name == "dc_rack_ai" else 0.03)
        grounded(name, v)
        c.extent(name, "the front (SitePlan: flush with the cold aisle)", float(v[:, 1].max()), front + 0.02 if name != "dc_rack_ai" else front + 0.02, 0.025)
        body = v[(v[:, 1] > front - 0.01) & ~((v[:, 0] < -0.25) & (np.abs(v[:, 2] - 1.02) < 0.11))]   # (all but the handle)
        c.extent(name, "the door's face", float(body[:, 1].max()), front + 0.0006, 0.0015)
        c.extent(name, "within its footprint at the back", float(-v[:, 1].min()), depth - front, 0.025)
    c.extent("dc_rack", "height", float(c.kit("dc_rack")[:, 2].max()), 2.00, 0.002)
    c.extent("dc_rack_network", "the cable riser meets the tray (TrayHeight)", float(c.kit("dc_rack_network")[:, 2].max()), 2.45, 0.002)

    for name in ("dc_leds_a", "dc_leds_b"):
        v = c.kit(name)
        c.report(len(v) >= 40, "%-19s lights in the set: %d corners" % (name, len(v)))
        c.extent(name, "proud of the door", float(v[:, 1].min()), 0.537, 0.0005)
        c.report(float(np.abs(v[:, 0]).max()) < 0.26 and float(v[:, 2].min()) > 0.14 and float(v[:, 2].max()) < 1.86,
                 "%-19s every light on the door (x within 0.26, 0.14 to 1.86 up)" % name)

    v = size("dc_tray", (0.46, 2.00, None), 0.002)
    rails = v[(v[:, 2] > 0.005) & (v[:, 2] < 0.09)]
    c.extent("dc_tray", "between the rails' outsides", float(np.ptp(rails[:, 0])), 0.40, 0.002)
    c.extent("dc_tray", "the underside at the pivot", float(v[(np.abs(v[:, 0]) < 0.17)][:, 2].min()), 0.0, 0.035)
    c.extent("dc_tray", "the hangers' tops", float(v[:, 2].max()), 0.55, 0.002)
    for name, width, top in (("dc_floor", 2.40, 0.020), ("dc_floor_vent", 0.60, 0.024)):
        v = size(name, (width, 2.40, top), 0.001)
        grounded(name, v)
    for name, want in (("dc_cooling", (1.80, 0.90, 1.962)), ("dc_ups", (0.80, 0.85, 1.90)), ("tech_rack_small", (0.60, 0.80, 1.20))):
        v = size(name, want, 0.03)
        grounded(name, v)
    for name, width, depth in (("dc_ops_desk", 1.60, 0.80), ("tech_bench", 1.40, 0.70)):
        v = c.kit(name)
        grounded(name, v)
        top = v[(np.abs(v[:, 0]) > width / 2 - 0.05) & (v[:, 2] > 0.6) & (v[:, 2] < 0.8)]
        c.extent(name, "desk top", float(top[:, 2].max()), 0.75, 0.0015)
        c.extent(name, "width", float(np.ptp(v[:, 0])), width, 0.03)
        c.extent(name, "depth", float(np.ptp(v[v[:, 2] < 0.76][:, 1])), depth, 0.02)

    v = size("hq_reception", (2.64, None, None), 0.01)
    grounded("hq_reception", v)
    c.extent("hq_reception", "the visitors' counter", float(v[v[:, 1] > 0.2][:, 2].max()), 1.10, 0.002)
    band = v[(np.abs(v[:, 2] - 0.30) < 0.051) & (np.abs(v[:, 0]) < 1.26) & (v[:, 1] > 0.44)]
    c.extent("hq_reception", "the colour band's face", float(band[:, 1].max()), 0.452, 0.0025)
    c.extent("hq_reception", "the footprint's depth", float(np.ptp(v[v[:, 2] < 0.9][:, 1])), 0.90, 0.03)
    v = size("hq_logo_wall", (3.42, 0.30, 2.61), 0.012)
    grounded("hq_logo_wall", v)
    plate = v[(np.abs(v[:, 2] - 1.60) < 0.56) & (np.abs(v[:, 0]) < 1.26) & (v[:, 1] > 0.065)]
    c.extent("hq_logo_wall", "the name's plate (face)", float(plate[:, 1].max()), 0.080, 0.0015)
    c.extent("hq_logo_wall", "the name's plate (width)", float(np.ptp(plate[:, 0])), 2.50, 0.01)
    for name, want in (("hq_screen", (1.60, None, 0.95)), ("tech_whiteboard", (1.80, None, 1.20))):
        v = size(name, want, 0.012)
        c.extent(name, "its back on the wall at the pivot", float(v[:, 1].min()), 0.0, 0.0005)
        c.extent(name, "centred on its pivot", float(v[:, 2].max() + v[:, 2].min()), 0.0, 0.03)
    v = size("dc_sign", (2.40, 0.40, 1.42), 0.012)
    grounded("dc_sign", v)
    v = size("dc_booth", (1.84, 1.84, 2.42), 0.012)
    grounded("dc_booth", v)
    v = c.kit("dc_barrier")
    grounded("dc_barrier", v)
    c.extent("dc_barrier", "the raised arm's top", float(v[:, 2].max()), 4.45, 0.002)
    v = size("dc_fence", (2.50, None, None), 0.02)
    grounded("dc_fence", v)
