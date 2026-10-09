"""What the event clips (CityAssets/scripts/clips_events.py) and Scripts/Events rely on in the event props: see check_life.py."""
import numpy as np


class Contract:
    """The same numbers as the head of props_events.py (which needs Blender, and this does not)."""
    CAMERA_EYE = (0.032, -0.59, 1.50)
    LENS_HEIGHT = 1.47
    PAN_AIM = np.array((0.16, -0.45, -0.17)) / np.linalg.norm((0.16, -0.45, -0.17))
    PAN_GRIP = np.array((0.090, -0.005, 1.270)) + PAN_AIM * (0.50 - 0.065)
    FOCUS_KNOB = (-0.135, -0.295, 1.41)
    HELMET_INSIDE = (0.082, 0.103)
    LAMP_HEIGHT = 2.32


def run(c):
    print("Event props: grips and heights")
    # The nozzle: FireScene puts the right wrist 0.06 m and the left 0.30 m along it; hose_hold's palms are there.
    c.grip("hose_nozzle", (0, 0.06, 0), (0, 1, 0))
    c.grip("hose_nozzle", (0, 0.30, 0), (0, 1, 0))
    v = c.kit("hose_nozzle")
    c.extent("hose_nozzle", "the coupling behind the pivot", v[:, 1].min(), -0.05)
    c.extent("hose_nozzle", "the tip (where the jet starts)", v[:, 1].max(), 0.42)
    # Staffs: held as the stop paddle is, 1.10 m above the pivot, and carried clear of the ground.
    for name, top, width in (("placard", 2.19, 0.56), ("banner_pole", 2.20, 0.72)):
        c.grip(name, (0, 0, 1.10), (0, 0, 1))
        v = c.kit(name)
        c.extent(name, "foot of the staff", v[:, 2].min(), 0.70)
        c.extent(name, "top", v[:, 2].max(), top)
        c.extent(name, "width", np.ptp(v[:, 0]), width, 0.005)
    v = c.kit("placard")
    board = v[v[:, 2] > 1.76]
    c.extent("placard", "board height", np.ptp(board[np.abs(board[:, 0]) > 0.1][:, 2]), 0.42)
    v = c.kit("banner_pole")
    cloth = v[v[:, 0] > 0.03]
    c.extent("banner_pole", "cloth to +X", cloth[:, 0].max(), 0.705)
    c.extent("banner_pole", "cloth height", np.ptp(cloth[:, 2]), 0.48, 0.01)
    # The camera: camera_operate stands 0.75 m behind the tripod's centre with the right eye 0.16 m ahead, 1.50 m up.
    e = Contract
    v = c.kit("film_camera")
    cup = v[(np.abs(v[:, 0] - e.CAMERA_EYE[0]) < 0.035) & (np.abs(v[:, 2] - e.CAMERA_EYE[2]) < 0.035) & (v[:, 1] < -0.5)]
    c.extent("film_camera", "eyecup just ahead of the operator's eye", cup[:, 1].min(), e.CAMERA_EYE[1] + 0.004, 0.002)
    c.extent("film_camera", "eyecup height", (cup[:, 2].max() + cup[:, 2].min()) / 2, e.CAMERA_EYE[2], 0.002)
    lens = v[(v[:, 1] > 0.0) & (v[:, 1] < 0.12) & (np.abs(v[:, 0]) < 0.051) & (np.abs(v[:, 2] - e.LENS_HEIGHT) < 0.051)]
    c.extent("film_camera", "lens axis height", (lens[:, 2].max() + lens[:, 2].min()) / 2, e.LENS_HEIGHT, 0.002)
    c.grip("film_camera", tuple(e.PAN_GRIP), tuple(e.PAN_AIM), thick=(0.028, 0.034))
    c.grip("film_camera", tuple(e.FOCUS_KNOB), (0, 1, 0), thick=(0.034, 0.042))
    c.extent("film_camera", "height", v[:, 2].max(), 1.70, 0.005)
    # The helmet's band clears the head (police_cap's HEAD and 0.006 m: it sits 0.028 m lower).
    v = c.kit("fire_helmet")
    band = v[v[:, 2] < 0.06]
    inside = (band[:, 0] / e.HELMET_INSIDE[0]) ** 2 + (band[:, 1] / e.HELMET_INSIDE[1]) ** 2
    c.report(inside.min() > 0.999, "%-19s nothing inside the band's %.3f x %.3f m (nearest at %.3f of it)" % ("fire_helmet", *e.HELMET_INSIDE, inside.min() ** 0.5))
    c.extent("fire_helmet", "lowest point at the pivot", v[:, 2].min(), 0.0, 0.001)
    c.extent("fire_helmet", "peak at +Y", v[:, 1].max(), 0.165)
    c.extent("fire_helmet", "neck brim", v[:, 1].min(), -0.235)
    # Worn on the upper-chest bone.
    v = c.kit("fire_air_pack")
    c.extent("fire_air_pack", "straps over the shoulders, above the pivot", v[:, 2].max(), 0.246, 0.006)
    c.extent("fire_air_pack", "cylinder's back", v[:, 1].min(), -0.270, 0.004)
    c.report(not np.any((np.abs(v[:, 0]) < 0.09) & (v[:, 1] > -0.110) & (v[:, 1] < 0.115) & (np.abs(v[:, 2]) < 0.15)), "%-19s nothing inside the chest" % "fire_air_pack")
    v = c.kit("runner_bib")
    c.extent("runner_bib", "ahead of the bone", v[:, 1].max(), 0.134, 0.001)
    c.extent("runner_bib", "below the bone", (v[:, 2].max() + v[:, 2].min()) / 2, -0.07, 0.001)
    # The set.
    v = c.kit("light_stand")
    lamp = v[(v[:, 1] > 0.08) & (v[:, 1] < 0.09) & (np.abs(v[:, 0]) < 0.14)]
    c.extent("light_stand", "lamp height", (lamp[:, 2].max() + lamp[:, 2].min()) / 2, e.LAMP_HEIGHT, 0.002)
    c.extent("light_stand", "height", v[:, 2].max(), 2.50, 0.01)
    c.extent("directors_chair", "seat rails (the canvas hangs 0.04 m between them)", c.top_near("directors_chair", 0.235, 0.0, 0.25, 0.03, below=0.70), 0.62, 0.003)
    c.extent("directors_chair", "height", c.kit("directors_chair")[:, 2].max(), 1.10)
    v = c.kit("firework_rack")
    c.extent("firework_rack", "length", np.ptp(v[:, 0]), 1.20)
    c.extent("firework_rack", "height", v[:, 2].max(), 0.55, 0.01)
