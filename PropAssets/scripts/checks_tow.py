"""What the code relies on in the tow truck (check_life.py runs this). Traffic/Tow/TowTruck puts the subframe and the bed on
the body at its origin and moves them about the pivot TowRules names; it puts the city bus's wheels on its axles, a lit
half of the light bar over each end of it, the cable from the winch, and stands the operator at the levers. TowRules
works out where the bed and a car on it are from the same figures as props_tow.py. Kit space: x right, y forward, z up."""
import math
import os
import re

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
CODE = os.path.join(HERE, "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Traffic", "Tow")


def constants(name):
    """The float constants of a C# file: name -> value (const float X = 1.5f; and static readonly Vector3 X = new Vector3(a, b, c))."""
    with open(os.path.join(CODE, name), encoding="utf-8-sig") as f:
        text = f.read()
    found = {m.group(1): float(m.group(2)) for m in re.finditer(r"const float (\w+) = (-?[0-9.]+)f;", text)}
    for m in re.finditer(r"Vector3 (\w+) = new Vector3\((-?[0-9.]+)f, (-?[0-9.]+)f, (-?[0-9.]+)f\)", text):
        found[m.group(1)] = (float(m.group(2)), float(m.group(3)), float(m.group(4)))
    return found


class Model:
    """props_tow.py's figures, read from its top-level assignments (it needs Blender to be imported; this runs without)."""

    def __init__(self):
        import ast
        with open(os.path.join(HERE, "props_tow.py"), encoding="utf-8") as f:
            tree = ast.parse(f.read())
        space = {"math": math, "round": round}
        for node in tree.body:
            if not isinstance(node, ast.Assign) or len(node.targets) != 1:
                continue
            target = node.targets[0]
            names = [target.id] if isinstance(target, ast.Name) else [e.id for e in target.elts if isinstance(e, ast.Name)] if isinstance(target, ast.Tuple) else []
            if not names or not all(n.isupper() for n in names):
                continue
            try:
                value = eval(compile(ast.Expression(node.value), "props_tow.py", "eval"), space)
            except Exception:
                continue        # a figure made by a function of the model's (SLIDE_MOST): worked out below instead
            if len(names) == 1 and isinstance(target, ast.Name):
                space[names[0]] = value
            else:
                space.update(zip(names, value))
        for name, value in space.items():
            if name.isupper():
                setattr(self, name, value)
        th = math.radians(self.TILT_MOST)
        a, b = self.BED_TOP - self.TILT_PIVOT[1], self.TILT_PIVOT[0] - self.BED_TAIL
        self.SLIDE_MOST = (self.TILT_PIVOT[1] + a * math.cos(th) - b * math.sin(th) - 0.04) / math.sin(th)     # props_tow._slide_most


def run(c):
    t = Model()
    rules, truck = constants("TowRules.cs"), constants("TowTruck.cs")

    # ---- the code and the model agree on every figure.
    for code, model in (("AxleFront", t.AXLES[0]), ("AxleRear", t.AXLES[1]), ("Nose", t.NOSE), ("HalfWidth", t.HALF), ("TiltMost", t.TILT_MOST),
                        ("PivotAhead", t.TILT_PIVOT[0]), ("PivotUp", t.TILT_PIVOT[1]), ("BedTop", t.BED_TOP), ("BedTail", t.BED_TAIL), ("HeadBoard", t.HEAD_BOARD[0])):
        c.extent("TowRules", code + " is the model's", rules[code], model, 0.0001)
    for code, model in (("PanelAhead", t.PANEL[0]), ("PanelOut", t.PANEL_OUT), ("WheelRadius", t.TYRE), ("TrackFront", t.TRACK[0]), ("TrackRear", t.TRACK[1])):
        c.extent("TowTruck", code + " is the model's", truck[code], model, 0.0001)
    for code, (along, up) in (("WinchAt", t.WINCH), ("BarAt", (t.BAR_AT, t.BAR_UP)), ("RamFoot", t.RAM_FOOT), ("RamHead", t.RAM_HEAD)):
        x, y, z = truck[code]
        c.report(abs(x) < 1e-6 and abs(y - up) < 1e-4 and abs(z - along) < 1e-4, "%-19s %s is the model's (%.3f along, %.3f up)" % ("TowTruck", code, along, up))
    # The slide down, worked out as TowRules.SlideFor works it out, is the one the previews pose the bed with.
    th = math.radians(rules["TiltMost"])
    slide = (rules["PivotUp"] + (rules["BedTail"] - rules["PivotAhead"]) * math.sin(th) + (rules["BedTop"] - rules["PivotUp"]) * math.cos(th) - rules["TailOverRoad"]) / math.sin(th)
    c.extent("TowRules", "SlideMost as props_tow.py has it", slide, t.SLIDE_MOST, 0.0005)

    # ---- the body: its length, its nose, the light bar on its roof, the levers, the lamps' places.
    v = c.kit("tow_truck")
    c.extent("tow_truck", "nose: nothing beyond the bumper but its tow eyes and plate", v[:, 1].max(), t.NOSE + 0.06, 0.012)
    bar = v[(np.abs(v[:, 0]) < 0.75) & (np.abs(v[:, 1] - t.BAR_AT) < 0.14) & (v[:, 2] > t.BAR_UP - 0.005)]
    c.extent("tow_truck", "the light bar's underside (TowTruck.BarAt)", bar[:, 2].min(), t.BAR_UP, 0.002)
    c.extent("tow_truck", "the light bar's middle along", (bar[:, 1].max() + bar[:, 1].min()) / 2, t.BAR_AT, 0.003)
    c.extent("tow_truck", "the light bar's length", np.ptp(bar[:, 0]), t.BAR[0], 0.01)
    knobs = v[(np.abs(v[:, 1] - t.PANEL[0]) < 0.22) & (np.abs(v[:, 2] - t.PANEL[1]) < 0.03)]
    c.extent("tow_truck", "the levers' knobs reach out (TowTruck.PanelOut)", np.abs(knobs[:, 0]).max(), t.PANEL_OUT + 0.02, 0.003)
    c.report(abs(knobs[:, 0].max() + knobs[:, 0].min()) < 0.003, "%-19s levers on both sides alike" % "tow_truck")
    # Nothing of the body where the bed and the subframe lie, behind the cab, above the chassis rails' top.
    behind = v[(v[:, 1] < t.CAB_BACK - 0.05) & (v[:, 1] > t.FRAME_BACK) & (np.abs(v[:, 0]) < t.HALF)]
    c.report(behind[:, 2].max() <= t.BED_UNDER - 0.02, "%-19s behind the cab nothing reaches the bed's underside: highest %.3f (the bed %.2f)" % ("tow_truck", behind[:, 2].max(), t.BED_UNDER))
    middle = behind[np.abs(behind[:, 0]) < t.TILT_RAILS[0] + 0.10]
    c.report(middle[:, 2].max() <= t.TILT_RAILS[1] + 0.001, "%-19s nothing under the subframe stands above its rails' foot: highest %.3f (%.2f)" % ("tow_truck", middle[:, 2].max(), t.TILT_RAILS[1]))
    # The front wheel turns inside its arch: the cab's sill over the axle is clear of the tyre with room to steer.
    over = v[(np.abs(v[:, 1] - t.AXLES[0]) < 0.10) & (np.abs(np.abs(v[:, 0]) - t.TRACK[0]) < 0.17) & (v[:, 2] > t.TYRE + 0.10)]
    c.report(over[:, 2].min() >= 2 * t.TYRE + 0.05, "%-19s over the front wheel: lowest %.3f, the tyre's top %.2f" % ("tow_truck", over[:, 2].min(), 2 * t.TYRE))

    # ---- the bed: its top, its tail, its width, the winch, nothing under it at rest that the wheels would meet.
    b = c.kit("tow_truck_bed")
    deck = b[(np.abs(np.abs(b[:, 0]) - t.HALF) < 0.0005) & (b[:, 2] < t.BED_TOP + 0.004)]     # the deck's own corners along its edges (under its rails and lamps)
    c.extent("tow_truck_bed", "top of the deck (TowRules.BedTop)", deck[:, 2].max(), t.BED_TOP, 0.001)
    c.extent("tow_truck_bed", "its tail (TowRules.BedTail)", b[:, 1].min(), t.BED_TAIL, 0.001)
    c.extent("tow_truck_bed", "its width over the edges", np.ptp(b[:, 0]), 2 * t.HALF, 0.03)
    c.extent("tow_truck_bed", "its underside at rest", b[:, 2].min(), t.BED_UNDER - 0.05, 0.002)
    tyre = b[(np.abs(b[:, 0]) > t.TRACK[1] - 0.28) & (np.abs(b[:, 0]) < t.TRACK[1] + 0.27)]          # whatever of the bed lies over the rear tyres' width
    c.report(tyre[:, 2].min() > 2 * t.TYRE + 0.02, "%-19s the rear tyres (top %.2f) pass under it: lowest over them %.3f" % ("tow_truck_bed", 2 * t.TYRE, tyre[:, 2].min()))
    roller = b[(np.abs(b[:, 1] - t.WINCH[0]) < 0.03) & (np.abs(b[:, 2] - (t.WINCH[1] - 0.03)) < 0.03)]
    c.report(len(roller) > 0 and np.ptp(roller[:, 0]) > 0.15, "%-19s the cable's roller where TowTruck.WinchAt says" % "tow_truck_bed")
    head = b[(b[:, 1] >= t.HEAD_BOARD[0] - 0.001)]
    c.extent("tow_truck_bed", "the headboard's top", head[:, 2].max(), t.HEAD_BOARD[2] + 0.07, 0.002)

    # ---- the subframe: its pivot where TowRules turns it about.
    s = c.kit("tow_truck_tilt")
    tube = s[(np.abs(s[:, 0]) > 0.46)]
    c.extent("tow_truck_tilt", "the pivot along", (tube[:, 1].max() + tube[:, 1].min()) / 2, t.TILT_PIVOT[0], 0.002)
    c.extent("tow_truck_tilt", "the pivot up", (tube[:, 2].max() + tube[:, 2].min()) / 2, t.TILT_PIVOT[1], 0.002)
    c.report(s[:, 2].max() <= t.BED_UNDER + 0.003, "%-19s its top (%.3f) under the bed's underside at rest (%.2f)" % ("tow_truck_tilt", s[:, 2].max(), t.BED_UNDER))

    # ---- the lamps lit: each group 1.5 mm proud of its lamps, at the ends the code says.
    head_lit = c.kit("tow_truck_light_head")
    c.extent("tow_truck_light_head", "in front of the headlamps", head_lit[:, 1].min(), t.CAB_FRONT + 0.024 + 0.0015, 0.0015)
    for name in ("tow_truck_light_tail", "tow_truck_light_brake"):
        lit = c.kit(name)
        c.extent(name, "behind the tail lamps", lit[:, 1].max(), t.LAMP_BACK - 0.022 - 0.0015, 0.0015)
    for name, sign in (("tow_truck_light_left", -1), ("tow_truck_light_right", 1)):
        lit = c.kit(name)
        c.report(np.all(lit[:, 0] * sign > 0), "%-19s on its own side only" % name)
    lens = c.kit("tow_bar_lens")
    c.report(lens[:, 0].min() > 0.0 and lens[:, 0].max() > t.BAR[0] / 2, "%-19s the +X half of the bar, out past its end" % "tow_bar_lens")
