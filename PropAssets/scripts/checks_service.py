"""What the code relies on in the service vehicles (check_life.py runs this). Transport/BusVehicle builds the city bus out
of city_bus and its parts: the wheels on its axles 2.90 m either side of its middle, 0.48 m in radius, the front ones
steering up to 38 degrees; two leaves in each door, 0.03 m off the side, that slide out 0.07 m and along 0.56 m; its
lettering over the windscreen, over the front door and at the back; each group of lamps lit over its lamps.
Events/EventRoad.EngineLivery puts the fire engine's axles on the delivery truck's (BodyAxleFront, BodyAxleRear) and a lit
lens over each end of its light bar (BarAt); FireScene.PlanCrew stands the pump operator 0.50 m off the panel and lays
the hose from the outlet. Kit space: x right, y forward, z up."""
import json
import math
import os
import re

import numpy as np

BUS = "city_bus"
ROOF = "city_bus_roof"
INSIDE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Transport", "BusInside.cs")
DOORS = (4.45, -0.55)          # BusVehicle.FrontDoorAhead, MiddleDoorAhead: two leaves 0.60 wide, 2.30 high, from the floor at 0.33
SIGNS = (("over the windscreen", (0.0, 5.465, 2.78), (0, 1, 0), (1.62, 0.24)), ("over the front door", (1.212, 3.25, 2.55), (1, 0, 0), (1.10, 0.20)),
         ("at the back", (0.0, -5.465, 2.62), (0, -1, 0), (0.45, 0.20)))          # BusVehicle.Signs: middle, facing, box
LAMPS = {"head": ("svc_headlamp",), "tail": ("svc_bus_tail_lamp",), "brake": ("svc_bus_tail_lamp", "svc_bus_brake_high"),
         "left": ("svc_headlamp", "svc_bus_tail_lamp", "svc_bus_repeater"), "right": ("svc_headlamp", "svc_bus_tail_lamp", "svc_bus_repeater")}


class Mesh:
    """A built prop in kit space, with what the measurements below ask of it."""

    def __init__(self, c, name):
        self.v, self.triangles = c.kit(name, True)
        with open(os.path.join(c.BUILD, "meshes", name + ".json")) as f:
            self.uv = np.array(json.load(f)["uv"]).reshape(-1, 2)
        with open(os.path.join(c.BUILD, "atlas.json")) as f:
            self.regions = json.load(f)["regions"]

    def hits(self, origins, direction):
        """How far each ray from `origins` along `direction` goes before it meets a face (inf where it meets none), and
        the face it meets (-1 for none). Only faces whose box spans the rays across the direction are tried."""
        origins, direction = np.asarray(origins, dtype=float), np.asarray(direction, dtype=float) / np.linalg.norm(direction)
        v, t = self.v, self.triangles
        a, b, d = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
        keep = np.ones(len(t), dtype=bool)
        for axis in range(3):
            if abs(direction[axis]) < 1e-9:               # the rays do not move along this axis: a face must span them on it
                low, high = np.minimum(np.minimum(a[:, axis], b[:, axis]), d[:, axis]), np.maximum(np.maximum(a[:, axis], b[:, axis]), d[:, axis])
                keep &= (high >= origins[:, axis].min() - 1e-6) & (low <= origins[:, axis].max() + 1e-6)
        index = np.nonzero(keep)[0]
        if not len(index):
            return np.full(len(origins), np.inf), np.full(len(origins), -1)
        a, e1, e2 = a[index], b[index] - a[index], d[index] - a[index]
        p = np.cross(direction, e2)
        det = (e1 * p).sum(axis=1)
        ok = np.abs(det) > 1e-12
        inv = np.where(ok, 1.0 / np.where(ok, det, 1.0), 0.0)
        s = origins[:, None, :] - a[None]
        u = (s * p[None]).sum(axis=2) * inv[None]
        q = np.cross(s, e1[None])
        w = (q * direction).sum(axis=2) * inv[None]
        far = (q * e2[None]).sum(axis=2) * inv[None]
        far = np.where(ok[None] & (u >= -1e-9) & (w >= -1e-9) & (u + w <= 1.0 + 1e-9) & (far > 1e-7), far, np.inf)
        first = far.argmin(axis=1)
        distance = far[np.arange(len(origins)), first]
        return distance, np.where(np.isfinite(distance), index[first], -1)

    def normals(self, faces):
        v, t = self.v, self.triangles[faces]
        n = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
        return n / np.linalg.norm(n, axis=1)[:, None]

    def on(self, faces, regions):
        """Whether each face's middle is mapped inside one of the atlas regions."""
        middle = self.uv[self.triangles[faces]].mean(axis=1)
        return np.any([(middle[:, 0] >= r[0]) & (middle[:, 0] <= r[2]) & (middle[:, 1] >= r[1]) & (middle[:, 1] <= r[3]) for r in (self.regions[n] for n in regions)], axis=0)


def run(c):
    # ---- the city bus: BusVehicle's measurements.
    bus = Mesh(c, BUS)
    b = bus.v
    c.extent(BUS, "length over the sign boxes (BusVehicle.Length)", np.ptp(b[:, 1]), 10.90, 0.003)
    c.extent(BUS, "middle of its length at the pivot", (b[:, 1].max() + b[:, 1].min()) / 2, 0.0, 0.002)
    body = b[b[:, 2] < 2.0]                                # under the mirrors
    c.extent(BUS, "width under the mirrors (BusVehicle.HalfWidth)", np.ptp(body[:, 0]), 2.40, 0.008)
    c.extent(BUS, "middle of its width at the pivot", (body[:, 0].max() + body[:, 0].min()) / 2, 0.0, 0.001)
    roof = Mesh(c, ROOF)
    c.extent(BUS, "height over the pod, on its roof (BusVehicle.Height)", max(b[:, 2].max(), roof.v[:, 2].max()), 3.05, 0.002)
    c.report(1.25 < np.abs(b[:, 0]).max() <= 1.42, "%-19s the mirrors stand out a little: to %.3f either side" % (BUS, np.abs(b[:, 0]).max()))
    c.report(b[:, 2].min() > 0.08, "%-19s clear of the road: its lowest point %.3f up" % (BUS, b[:, 2].min()))
    tyre = 2 * math.sqrt(0.48 ** 2 - 0.27 ** 2)            # the tyre's length 0.75 m up
    for axle in (2.90, -2.90):                             # BusBay.BusAxleFront and BusWheelbase
        for side in (1, -1):
            ys = np.arange(axle - 0.80, axle + 0.80, 0.005)
            far, _ = bus.hits([(side * 1.6, y, 0.75) for y in ys], (-side, 0, 0))
            open_ = ys[1.6 - far < 1.0]                    # met nothing of the side: the box between the wheels, or nothing
            where = "arch over the axle at y %+.2f on %s" % (axle, "+X" if side > 0 else "-X")
            c.extent(BUS, where + ": its middle", (open_.max() + open_.min()) / 2 if len(open_) else 9.0, axle, 0.006)
            c.report(len(open_) > 0 and np.ptp(open_) > tyre + 0.10, "%-19s %s: %.2f open 0.75 m up, the tyre %.2f" % (BUS, where, np.ptp(open_) if len(open_) else 0.0, tyre))

    # ---- the doors: open where the leaves are, down to the floor and no wider; clear of the leaves, shut and sliding open.
    for middle in DOORS:
        where = "door at y %+.2f" % middle
        rays = [(1.6, middle + du, z) for du in np.linspace(-0.57, 0.57, 9) for z in np.linspace(0.37, 2.59, 9)]
        far, _ = bus.hits(rays, (-1, 0, 0))
        # The middle door's own validator stands in it on its rear pole, by design (BusInside's Furniture, x 0.90..1.07,
        # along -1.05..-0.95, 1.10 to 1.40 up): what a ray meets of it is left out, and nothing else.
        y, z = np.array(rays)[:, 1], np.array(rays)[:, 2]
        meet = 1.6 - far
        reader = (meet > 0.89) & (meet < 1.075) & (y > -1.055) & (y < -0.94) & (z > 1.09) & (z < 1.41)
        meet = np.where(reader, 0.0, meet)
        c.report(np.all(meet < 0.5), "%-19s %s open behind its leaves: nothing of the body nearer than %.2f in from the side%s"
                 % (BUS, where, 1.20 - meet.max(), " (but its validator)" if reader.any() else ""))
        far, _ = bus.hits([(1.6, middle + side * 0.61, z) for side in (1, -1) for z in (0.5, 1.5, 2.4)] + [(1.6, middle, 2.67), (1.6, middle, 0.30)], (-1, 0, 0))
        c.report(np.all(np.abs(1.6 - far - 1.20) < 0.04), "%-19s %s no wider or higher than its leaves: the side is there 1 cm outside them" % (BUS, where))
        far, _ = bus.hits([(1.0, middle, 1.5)], (0, 0, -1))
        c.extent(BUS, where + ": the floor in it (BusVehicle.Floor)", 1.5 - far[0], 0.33, 0.002)
        shut = b[(b[:, 0] > 1.205) & (np.abs(b[:, 1] - middle) < 0.60) & (b[:, 2] > 0.33) & (b[:, 2] < 2.63)]
        c.report(len(shut) == 0, "%-19s %s: nothing where its leaves stand shut (%d points)" % (BUS, where, len(shut)))
        sweep = b[(b[:, 0] > 1.27) & (b[:, 0] < 1.335) & (np.abs(b[:, 1] - middle) < 1.17) & (b[:, 2] > 0.30) & (b[:, 2] < 2.66)]
        c.report(len(sweep) == 0, "%-19s %s: nothing in the way of its leaves sliding open, the mirror outside them (%d points)" % (BUS, where, len(sweep)))

    inside(c, bus, roof)

    # ---- the lettering: a flat black panel under each, 8 to 20 mm behind it, nothing in front.
    for where, centre, facing, (wide, high) in SIGNS:
        n = np.array(facing, dtype=float)
        across = np.cross(n, (0.0, 0.0, 1.0))
        points = [np.array(centre) + across * du + np.array((0.0, 0.0, 1.0)) * dv for du in np.linspace(-wide / 2, wide / 2, 7) for dv in np.linspace(-high / 2, high / 2, 3)]
        far, faces = bus.hits([q + n * 0.5 for q in points], -n)
        gap = far - 0.5
        met = faces >= 0
        flat = met.all() and np.all(np.abs(bus.normals(faces) @ n) > 0.9999) and np.ptp(gap) < 0.0005
        c.report(flat and np.all((gap > 0.008) & (gap < 0.020)) and bus.on(faces, ("gloss_black",)).all(),
                 "%-19s sign %s: a flat black panel %.1f to %.1f mm behind the lettering" % (BUS, where, gap.min() * 1000, gap.max() * 1000))

    # ---- the lamps: each lit group over the lenses of its own lamps, 0.5 to 3 mm in front of them, on its own side.
    for group, decals in LAMPS.items():
        name = "city_bus_light_" + group
        lit = Mesh(c, name)
        faces = np.arange(len(lit.triangles))
        n = lit.normals(faces)
        middles = lit.v[lit.triangles].mean(axis=1)
        n *= np.sign(((middles - (0.0, 0.0, 1.4)) * n).sum(axis=1))[:, None]      # out of the bus
        gaps, lenses = [], []
        for middle, normal in zip(middles, n):
            far, face = bus.hits([middle + normal * 0.02], -normal)
            gaps.append(far[0] - 0.02)
            lenses.append(face[0] >= 0 and bool(bus.on(face, decals)[0]))
        gaps = np.array(gaps)
        c.report(np.all((gaps > 0.0005) & (gaps < 0.003)) and all(lenses),
                 "%-19s %d faces, each over its lamp's lens: %.1f to %.1f mm in front of it" % (name, len(faces), gaps.min() * 1000, gaps.max() * 1000))
        if group in ("left", "right"):
            c.report(np.all(lit.v[:, 0] * (1 if group == "right" else -1) > 0.3), "%-19s all on the %s (%s)" % (name, group, "+X" if group == "right" else "-X"))

    # ---- the wheels and the leaf: their own middles, BusVehicle's sizes; the wheels in their arches, steered or not.
    for name, wide in (("city_bus_wheel", 0.30), ("city_bus_wheel_twin", 0.56)):
        w = c.kit(name)
        c.extent(name, "radius (BusVehicle.WheelRadius)", np.sqrt(w[:, 1] ** 2 + w[:, 2] ** 2).max(), 0.48, 0.002)
        c.extent(name, "width (TyreFront, TyreRear)", np.ptp(w[:, 0]), wide, 0.002)
        for axis in range(3):
            c.extent(name, "middle in %s at its pivot" % "xyz"[axis], (w[:, axis].max() + w[:, axis].min()) / 2, 0.0, 0.002)
        hub = w[np.sqrt(w[:, 1] ** 2 + w[:, 2] ** 2) < 0.08]
        c.report(hub[:, 0].mean() > 0.0, "%-19s the hub to +X (its cap out to x %.3f)" % (name, hub[:, 0].max()))
    w = c.kit("city_bus_wheel")
    for steer in (-38.0, 38.0):                            # BusVehicle: the front wheels steer up to 38 degrees
        t = math.radians(steer)
        placed = np.stack([1.02 + w[:, 0] * math.cos(t) - w[:, 1] * math.sin(t), 2.90 + w[:, 0] * math.sin(t) + w[:, 1] * math.cos(t), 0.48 + w[:, 2]], axis=1)
        room = np.sqrt(np.maximum(0.0, 0.58 ** 2 - np.maximum(0.0, placed[:, 2] - 0.48) ** 2))          # the arch's half length at each height
        c.report(placed[:, 0].min() > 0.555 and np.all(np.abs(placed[:, 1] - 2.90) < room - 0.01),
                 "%-19s steered %+.0f degrees: inside its arch, %.3f clear of the housing over it" % ("city_bus_wheel", steer, placed[:, 0].min() - 0.55))
    t = c.kit("city_bus_wheel_twin")
    c.report(0.92 + t[:, 0].max() <= 1.2005 and 0.92 + t[:, 0].min() > 0.605,
             "%-19s on its track (0.92): from x %.3f to %.3f, inside the side and clear of the housing" % ("city_bus_wheel_twin", 0.92 + t[:, 0].min(), 0.92 + t[:, 0].max()))
    leaf = c.kit("city_bus_door")
    for axis, want in enumerate((0.04, 0.60, 2.30)):
        c.extent("city_bus_door", "xyz"[axis] + " (DoorLeaf, DoorTall)", np.ptp(leaf[:, axis]), want, 0.002)
    c.extent("city_bus_door", "lower edge at the pivot", leaf[:, 2].min(), 0.0, 0.0005)
    c.extent("city_bus_door", "middle of its width at the pivot", (leaf[:, 1].max() + leaf[:, 1].min()) / 2, 0.0, 0.001)

    # ---- the fire engine and its light bar.
    e = c.kit("fire_engine")
    c.extent("fire_engine", "stands on the ground", e[:, 2].min(), 0.0, 0.001)
    for y in (2.756, -1.362):                              # the Delivery Truck's axles
        tyre = e[(np.abs(e[:, 1] - y) < 0.51 + 0.02) & (e[:, 2] < 0.02)]
        c.extent("fire_engine", "a tyre on the ground at y %.2f" % y, (tyre[:, 1].max() + tyre[:, 1].min()) / 2 if len(tyre) else 9.0, y, 0.02)
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


class _Numbers:
    """The constants of props_service.py, read without Blender (it imports bmesh): every plain assignment at the top of
    the module whose value can be worked out from numbers and the names before it."""

    def __init__(self):
        import ast
        path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "props_service.py")
        with open(path, encoding="utf-8") as f:
            tree = ast.parse(f.read())
        names = {"math": math}
        for node in tree.body:
            if isinstance(node, ast.Assign) and all(isinstance(t, (ast.Name, ast.Tuple)) for t in node.targets):
                try:
                    value = eval(compile(ast.Expression(node.value), path, "eval"), {"__builtins__": {}}, names)
                except Exception:
                    continue
                for target in node.targets:
                    if isinstance(target, ast.Name):
                        names[target.id] = value
                    else:
                        for element, part in zip(target.elts, value):
                            names[element.id] = part
        self.__dict__.update(names)


ps = _Numbers()


def inside_numbers():
    """What Transport/BusInside.cs says of the inside: its constants, its named places and its lists of places, (x, z) there
    and (x, y) here."""
    with open(INSIDE, encoding="utf-8") as f:
        text = f.read()
    number = r"(-?[0-9.]+)f"
    consts = {m.group(1): float(m.group(2)) for m in re.finditer(r"public const float (\w+) = " + number + ";", text)}
    spots = {m.group(1): (float(m.group(2)), float(m.group(3))) for m in re.finditer(r"Spot (\w+) = new Spot\(" + number + ", " + number + r"\)", text)}
    lists = {m.group(1): [(float(a), float(b)) for a, b in re.findall(r"new Spot\(" + number + ", " + number + r"\)", m.group(2))]
             for m in re.finditer(r"Spot\[\] (\w+) = \{([^}]*)\}", text)}
    return consts, spots, lists


def inside(c, bus, roof):
    """The inside as BusInside walks it: its numbers are the model's, the floor is under every place of every path with
    nothing over it, the seats, the validator and the strap are where the clips put a rider's body and hands."""
    consts, spots, lists = inside_numbers()
    floor = ps.BUS_FLOOR
    agree = [("Floor", floor), ("SeatUp", ps.SEAT_UP), ("SeatFeet", ps.SEAT_FEET), ("Wall", ps.IN_WALL), ("FrontDoor", ps.BUS_DOORS[0]),
             ("MiddleDoor", ps.BUS_DOORS[1]), ("StrapX", ps.STRAP_X), ("RiderStrap", ps.RIDER_STRAP), ("StrapUp", ps.STRAP_LOOP - floor)]
    wrong = [name for name, want in agree if abs(consts.get(name, 99.0) - want) > 1e-4]
    c.report(not wrong and lists.get("Seats") == list(ps.RIDER_SEATS),
             "%-19s BusInside.cs says what the model is: %s" % (BUS, "floor, seats, doors, strap" if not wrong else "NOT " + ", ".join(wrong)))
    c.report(abs(spots["BoardFrom"][0] - (ps.BUS_HALF + 0.30)) < 1e-4 and abs(spots["BoardFrom"][0] - spots["Aboard"][0] - consts["StepIn"]) < 1e-4
             and abs(spots["BoardFrom"][1] - ps.BUS_DOORS[0]) < 1e-4 and abs(spots["ExitTo"][0] - (ps.BUS_HALF + 0.30)) < 1e-4 and abs(spots["ExitTo"][1] - ps.BUS_DOORS[1]) < 1e-4,
             "%-19s board_step from %.2f m out at the front door to %.2f in (BusVehicle.DoorOut, its step %.2f); off at the middle door" % (BUS, spots["BoardFrom"][0], spots["Aboard"][0], consts["StepIn"]))

    def down(points, start=2.05):
        # What is seen looking down: a face turned away (the underside where it rises over the arches) is not drawn from above.
        origins = np.array([(x, y, start) for x, y in points], dtype=float)
        depth = np.zeros(len(origins))
        faces = np.full(len(origins), -1)
        todo = np.arange(len(origins))
        for _ in range(6):
            if not len(todo):
                break
            far, met = bus.hits(origins[todo], (0, 0, -1))
            hit = met >= 0
            up = np.zeros(len(todo), dtype=bool)
            up[hit] = bus.normals(met[hit])[:, 2] < 0.0      # (Unity's winding, read in the kit's axes: a face turned up comes out with z under 0)
            done = ~hit | up
            depth[todo[done]] += far[done]
            faces[todo[done]] = met[done]
            again = todo[~done]
            depth[again] += far[~done] + 0.001
            origins[again, 2] -= far[~done] + 0.001
            todo = again
        return start - depth, faces

    # The seats: each cushion's top SEAT_UP over the floor at its middle, the floor where the feet go ahead of it.
    for x, y in ps.RIDER_SEATS:
        top, faces = down([(x, y)])
        feet, _ = down([(x, y + ps.SEAT_FEET), (x - 0.08, y + ps.SEAT_FEET), (x + 0.08, y + ps.SEAT_FEET)])
        c.report(abs(top[0] - floor - ps.SEAT_UP) < 0.004 and bool(bus.on(faces, ("wool_blue",))[0]) and np.all(np.abs(feet - floor) < 0.003),
                 "%-19s the rider's seat at (%.2f, %.2f): its top %.3f over the floor (sit_bench %.2f), the floor where the feet go %.2f ahead" % (BUS, x, y, top[0] - floor, ps.SEAT_UP, ps.SEAT_FEET))

    # Every path of BusInside: the floor at 0.33 under it every 5 cm and nothing over it up to the straps' grips; away from
    # the seats a body's width of it too.
    named = dict(spots)
    for i, (x, y) in enumerate(lists["Seats"]):
        named["Feet%d" % i] = (x, y + consts["SeatFeet"])
    for i, q in enumerate(lists["Aisle"]):
        named["Aisle%d" % i] = q
    for i, q in enumerate(lists["AisleUp"]):
        named["AisleUp%d" % i] = q
    paths = [("Aboard", "Reader"), ("Reader", "Aisle0", "Aisle1", "Aisle2", "SeatTurn", "Feet0"), ("SeatTurn", "WindowOut", "Feet1"), ("Aisle2", "Strap"),
             ("Feet0", "OffSeat", "Exit"), ("Feet1", "WindowOut", "OffSeat", "Exit"), ("Strap", "Exit"), ("Aisle2", "BackToAisle", "Exit"),
             ("Reader", "FrontExit"), ("Aboard", "FrontExit"),
             ("MiddleAboard", "MiddleReader"), ("MiddleReader", "DoorIn", "Strap"), ("MiddleReader", "DoorIn", "BackToAisle", "SeatTurn", "Feet0"),
             ("MiddleAboard", "DoorIn", "AisleUp0", "AisleUp1", "AisleUp2", "AisleUp3", "Reader")]
    worst, blocked = 0.0, []
    for path in paths:
        for a, b in zip(path, path[1:]):
            p0, p1 = np.array(named[a]), np.array(named[b])
            leg = np.linalg.norm(p1 - p0)
            along = (p1 - p0) / max(leg, 1e-6)
            aside = np.array((-along[1], along[0]))
            seat_end = a.startswith("Feet") or b.startswith("Feet") or b == "WindowOut"
            points = []
            for k in np.linspace(0.0, 1.0, max(2, int(leg / 0.05) + 1)):
                q = p0 + (p1 - p0) * k
                points.append(q)
                if not seat_end:
                    points += [q + aside * 0.13, q - aside * 0.13]
            got, _ = down(points)
            off = np.abs(got - floor)
            worst = max(worst, off.max())
            if off.max() > 0.003:
                blocked.append("%s to %s" % (a, b))
    c.report(not blocked, "%-19s BusInside's %d paths: the floor under them all, nothing over it below 2.05 m, nor 0.13 m either side away from the seats (worst %.3f)%s"
             % (BUS, len(paths), worst, "" if not blocked else ": NOT " + ", ".join(blocked)))

    # The validator: pay_tap holds the card to a reader ReaderAhead away and ReaderUp over the feet.
    lens = np.array((consts["ReaderX"], consts["ReaderZ"]))
    far, faces = bus.hits([(lens[0] + 0.30, lens[1], consts["ReaderY"])], (-1, 0, 0))
    met = lens[0] + 0.30 - far[0]
    reach = np.linalg.norm(np.array(spots["Reader"]) - lens)
    c.report(abs(met - lens[0]) < 0.003 and bool(bus.on(faces, ("svc_lens_clear",))[0]) and abs(reach - consts["ReaderAhead"]) < 0.02
             and abs(consts["ReaderY"] - floor - consts["ReaderUp"]) < 0.01,
             "%-19s the validator's lens at x %.3f, %.3f over the floor; the rider before it %.2f from it (pay_tap %.2f and %.3f)"
             % (BUS, met, consts["ReaderY"] - floor, reach, consts["ReaderAhead"], consts["ReaderUp"]))

    # The middle door's validator: its lens facing forward (+y) at MiddleReaderZ, the rider before it ReaderAhead away.
    lens = np.array((consts["MiddleReaderX"], consts["MiddleReaderZ"]))
    far, faces = bus.hits([(lens[0], lens[1] + 0.30, consts["ReaderY"])], (0, -1, 0))
    met = lens[1] + 0.30 - far[0]
    reach = np.linalg.norm(np.array(spots["MiddleReader"]) - lens)
    c.report(abs(met - lens[1]) < 0.003 and bool(bus.on(faces, ("svc_lens_clear",))[0]) and abs(reach - consts["ReaderAhead"]) < 0.02
             and abs(consts["ReaderY"] - floor - consts["ReaderUp"]) < 0.01,
             "%-19s the middle door's validator's lens at y %.3f, %.3f over the floor; the rider before it %.2f from it (pay_tap %.2f and %.3f)"
             % (BUS, met, consts["ReaderY"] - floor, reach, consts["ReaderAhead"], consts["ReaderUp"]))

    # The strap: strap_hang holds it StrapUp over the feet, StrapRight to the right and StrapAhead ahead of them.
    far, faces = bus.hits([(consts["StrapX"], consts["RiderStrap"], 1.60)], (0, 0, 1))
    grip = 1.60 + far[0] + 0.011
    stand = (consts["StrapX"] - consts["StrapRight"], consts["RiderStrap"] - consts["StrapAhead"])
    c.report(abs(grip - floor - consts["StrapUp"]) < 0.006 and np.allclose(stand, spots["Strap"], atol=1e-4) and bool(bus.on(faces, ("rubber_black",))[0]),
             "%-19s the rider's strap: its grip %.3f over the floor (strap_hang %.2f), the rider under it at (%.2f, %.2f)" % (BUS, grip - floor, consts["StrapUp"], *stand))

    # The roof apart: the ceiling at the doors' upper edge, within the sides, and nothing of the body left over the inside.
    lintel = floor + ps.BUS_LEAF[1]
    r = roof.v
    c.extent(ROOF, "the ceiling under it at the doors' upper edge", r[:, 2].min(), lintel, 0.002)
    c.report(np.abs(r[:, 0]).max() <= ps.BUS_HALF + 0.016, "%-19s within the bus's sides (to %.3f)" % (ROOF, np.abs(r[:, 0]).max()))
    up, _ = down([(x, y) for x in (-0.9, 0.0, 0.9) for y in (-4.0, -1.0, 2.0)], start=4.0)
    c.report(np.all(up < ps.ROOF_FROM), "%-19s nothing of it over the inside: the view from above looks in when the roof is not drawn" % BUS)
