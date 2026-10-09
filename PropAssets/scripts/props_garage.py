"""THE GARAGE PROPS, made to what Scripts/Garage asks for by id: two bicycles and two motorbikes (BikeBody.cs), the
helmet, whole wheels and bolt-on parts for the game's cars (GarageCar.cs), and the bike-share dock (BikePlaces.cs).
Same rules as props.py: one function each, metres, X right, Y forward, Z up, one mesh on the shared atlas. Unity
gets (x, z, y).

THE CODE IS THE SPECIFICATION. The numbers below are BikeCatalogue.All (BikeBody.cs) turned into the kit's axes;
checks_garage.py measures them on the built meshes and CityAssets/scripts/clips_garage.py says them again.

                      rear / front axle (y)  wheel   seat top (y, z)   grip (x, y, z)        crank or peg            steering axis: top, bottom (y, z)
    bicycle_city      -0.52  / 0.58          0.34    (-0.30, 0.93)     (0.26, 0.16, 1.14)    crank (-0.08, 0.29)     (0.36, 1.00), (0.44, 0.70)
    bicycle_road      -0.49  / 0.55          0.34    (-0.29, 0.95)     (0.13, 0.42, 1.00)    crank (-0.07, 0.28)     (0.36, 0.99), (0.43, 0.72)
    motorbike_standard -0.651 / 0.723        0.33    (-0.20, 0.83)     (0.31, 0.30, 1.075)   peg (0.24, -0.10, 0.34) (0.40, 0.98), (0.47, 0.80)
    motorbike_sport   -0.651 / 0.723         0.33    (-0.22, 0.84)     (0.27, 0.42, 0.915)   peg (0.24, -0.16, 0.38) (0.41, 0.96), (0.48, 0.78)

    cranks 0.17 m, a pedal's MIDDLE on the crank circle 0.13 m from the centre line (BikeCatalogue.CrankLength,
    PedalOut); modelled as BikeBody.Pedals(def, 0) leaves them: the right crank forward, the left back, pedals level.
    Grips lie along X (the game's hand rig holds a scooter's bar): on the road bicycle they are the TOPS of its drop
    bar either side of the stem, on the sport bike its clip-ons under the top yoke. The pivot is the vehicle's origin: on the ground
    under the middle, which is NOT midway between the axles.

Each bike is built here from the parts BikeBody moves (frame, fork, wheel, crank, pedal), each about the point
BikeBody turns it about, and comes out TWICE: as those parts (ids <bike>_frame, _fork, _wheel or _wheel_front /
_wheel_rear, _crank, _pedal), which is what BikeBody.Build assembles for anything ridden or coloured, and put
together as ONE mesh under the bike's own id (for a display bike in the model's own colour, which is one draw
call, and for the sheets of CityAssets and scenes.py).

PAINT. What is painted in the bike's colour (frame tubes, mudguards, tank, tail, fairing) is a part of its own in
the parts build: <bike>_frame_paint and <bike>_fork_paint, at the frame's and the fork's pivots, mapped onto the
neutral swatch garage_white. BikeBody draws those two with PropLibrary.Tinted(colour): the atlas material with its
base colour multiplied by BikeDef.Colour or the owner's choice, one shared material a colour. In the whole bike the
same faces are on the colour's own swatch (garage_teal, garage_red, garage_navy: BikeDef.Colour is those to the digit).
"""
import math
import os

import numpy as np
from mathutils import Vector

from life_props import cup, flat, pipe, ribbon, rod, smooth, sticker
from prop_kit import Paint, Prop, box, ellipse, fillet, grid, lathe, loft, moved, prism, region, rounded_rect, tube

TAU = 2 * math.pi
NEUTRAL = "garage_white"                              # what the paint parts are mapped on: the tint is multiplied into it

# ---------------------------------------------------------------------------------------------- the contract

CRANK = 0.17                                          # BikeCatalogue.CrankLength
PEDAL_OUT = 0.13                                      # BikeCatalogue.PedalOut
PEDAL = (0.09, 0.075, 0.03)                           # the stand-in's is 0.09 x 0.075 x 0.022

CITY = {"axles": (-0.52, 0.58), "wheel": 0.34, "seat": (-0.30, 0.93), "grip": (0.26, 0.16, 1.14), "crank": (-0.08, 0.29), "head": ((0.36, 1.00), (0.44, 0.70))}
ROAD = {"axles": (-0.49, 0.55), "wheel": 0.34, "seat": (-0.29, 0.95), "grip": (0.13, 0.42, 1.00), "crank": (-0.07, 0.28), "head": ((0.36, 0.99), (0.43, 0.72))}
STANDARD = {"axles": (-0.651, 0.723), "wheel": 0.33, "seat": (-0.20, 0.83), "grip": (0.31, 0.30, 1.075), "peg": (0.24, -0.10, 0.34), "head": ((0.40, 0.98), (0.47, 0.80))}
SPORT = {"axles": (-0.651, 0.723), "wheel": 0.33, "seat": (-0.22, 0.84), "grip": (0.27, 0.42, 0.915), "peg": (0.24, -0.16, 0.38), "head": ((0.41, 0.96), (0.48, 0.78))}
PEG_RADIUS = 0.016

HEAD = (0.076, 0.097)                                 # props.py: half width and half depth of the head at a cap's band
HEAD_CLEAR = 0.02
HEAD_BONE = 0.113                                     # the cap's band above the Head bone, which is the helmets' pivot

WHEEL_RADIUS, WHEEL_WIDTH = 0.34, 0.22                # GarageCar.WheelPropRadius, WheelPropWidth


# ---------------------------------------------------------------------------------------------- helpers

def about_x(p, profile, paint, segments, at, sharp=40, inward=False, **caps):
    """A turned part whose axis is X (a wheel, a drum): profile is (radius, x) from -x to +x. inward: the faces
    look towards the axis and out of the sides (the well of a rim) instead of away from it."""
    if inward:
        profile = profile[::-1]
    p.add(lathe(profile, segments, **caps), flat(paint) if isinstance(paint, str) else paint, at=at, rot=(0, 90, 0), sharp=sharp)


def about_y(p, profile, paint, segments, at, sharp=40, tilt=0.0, **caps):
    """A turned part whose axis is Y (a lamp): profile is (radius, y) from back to front. tilt: degrees nose up."""
    p.add(lathe(profile, segments, **caps), flat(paint) if isinstance(paint, str) else paint, at=at, rot=(-90 + tilt, 0, 0), sharp=sharp)


def hoop(p, centre, radius, thick, paint, segments=18, sides=5):
    """A ring of round section about X (a tyre)."""
    points = [(centre[0], centre[1] + math.cos(a) * radius, centre[2] + math.sin(a) * radius) for a in np.linspace(0, TAU, segments, endpoint=False)]
    p.add(tube(points, thick, sides, closed=True), flat(paint) if isinstance(paint, str) else paint, sharp=70)


def washer(p, r0, r1, x, paint, segments, at, thick=0.004):
    """A flat ring seen from both sides (a brake disc, a sprocket), about X."""
    about_x(p, [(r0, x - thick / 2), (r1, x - thick / 2), (r1, x + thick / 2), (r0, x + thick / 2)], paint, segments, at, sharp=30)


def beam(p, a, b, width, height, paint, end=None):
    """A bar of rectangular section between two points, `width` along X and `height` up (end: at b, if it tapers)."""
    end = end or (width, height)
    rings = [[(c[0] + sx * w / 2, c[1], c[2] + sz * h / 2) for sx, sz in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for c, (w, h) in ((a, (width, height)), (b, end))]
    p.add(loft(rings, cap_start=True, cap_end=True), flat(paint) if isinstance(paint, str) else paint)


def slab(p, outline, x0, x1, paint, sharp=40, shrink=None):
    """A plate standing fore and aft: an outline of (y, z) points given thickness from x0 to x1. shrink: the outer
    face (x1) is drawn in to this fraction of the outline about its middle, for a chamfered edge."""
    rings = [[(x0, y, z) for y, z in outline]]
    if shrink:
        cy, cz = sum(y for y, _ in outline) / len(outline), sum(z for _, z in outline) / len(outline)
        rings.append([(x0 + (x1 - x0) * 0.6, y, z) for y, z in outline])
        rings.append([(x1, cy + (y - cy) * shrink, cz + (z - cz) * shrink) for y, z in outline])
    else:
        rings.append([(x1, y, z) for y, z in outline])
    p.add(loft(rings, cap_start=True, cap_end=True), flat(paint) if isinstance(paint, str) else paint, sharp=sharp)


def arc(centre, radius, start, end, count):
    """Points round an axle (y, z): angles in degrees from straight ahead (+Y), going up and over."""
    return [(0.0, centre[0] + math.cos(math.radians(a)) * radius, centre[1] + math.sin(math.radians(a)) * radius) for a in np.linspace(start, end, count)]


def guard(p, centre, radius, start, end, width, paint, count=8, crown=0.012, both=True):
    """A mudguard: a strip round a wheel, arched across its width."""
    cy, cz = centre

    def surface(u, v):
        a = math.radians(start + (end - start) * u)
        r = radius - crown * (2 * v - 1) ** 2
        return ((v - 0.5) * width, cy + math.cos(a) * r, cz + math.sin(a) * r)
    p.add(grid(surface, count, 2, both_sides=both), flat(paint) if isinstance(paint, str) else paint, sharp=180)


def shell_ring(y, half, z0, z1, waist=1.0, count=5, squash=0.75):
    """A cross-section of a body that runs fore and aft (a tank, a seat, a fairing): a rounded outline in the XZ
    plane at station y, `half` wide either side, from z0 up to z1. waist < 1 draws the lower half in (the knee
    cut-outs of a tank). count points a quarter turn."""
    points = []
    total = count * 4
    for k in range(total):
        a = TAU * k / total
        cx, sz = math.cos(a), math.sin(a)
        x = math.copysign(abs(cx) ** squash, cx) * half
        z = math.copysign(abs(sz) ** squash, sz)
        if sz < 0:
            x *= 1.0 - (1.0 - waist) * min(1.0, -sz * 1.4)
        points.append((x, y, (z0 + z1) / 2 + z * (z1 - z0) / 2))
    return points


# ---------------------------------------------------------------------------------------------- a bike in parts

def absorb(p, other, at=(0.0, 0.0, 0.0)):
    """Copies a finished prop into another, moved."""
    layer = other.bm.loops.layers.uv.active
    shift = Vector(at)
    made = {v: p.bm.verts.new(v.co + shift) for v in other.bm.verts}
    for face in other.bm.faces:
        copy = p.bm.faces.new([made[v] for v in face.verts])
        copy.smooth = True
        for source, target in zip(face.loops, copy.loops):
            target[p.uv].uv = source[layer].uv
        for source, target in zip(face.edges, copy.edges):
            target.smooth = source.smooth
    return p


class Shift:
    """A prop that everything is added to so much further forward and up: bodywork drawn for one seat, put at another."""

    def __init__(self, prop, dy=0.0, dz=0.0):
        self.prop, self.dy, self.dz = prop, dy, dz

    def add(self, part, paint, at=(0, 0, 0), **more):
        self.prop.add(part, paint, at=(at[0], at[1] + self.dy, at[2] + self.dz), **more)
        return self


class Painted:
    """A part and its paint: whatever is added in the bike's colour goes to the paint part, the rest to the part."""

    def __init__(self, plain, paint, colour):
        self.plain, self.paint, self.colour = plain, paint, colour

    def add(self, part, paint, **more):
        (self.paint if getattr(paint, "swatch", paint) == self.colour else self.plain).add(part, paint, **more)
        return self


def repaint(prop, old, new):
    """Moves every face mapped on one swatch on to another, keeping its place within it."""
    a, b = region(old, 0.0), region(new, 0.0)
    for face in prop.bm.faces:
        for loop in face.loops:
            u, v = loop[prop.uv].uv
            if a[0] - 1e-6 <= u <= a[2] + 1e-6 and a[1] - 1e-6 <= v <= a[3] + 1e-6:
                loop[prop.uv].uv = (b[0] + (u - a[0]) / (a[2] - a[0]) * (b[2] - b[0]), b[1] + (v - a[1]) / (a[3] - a[1]) * (b[3] - b[1]))


class Bike:
    """A two-wheeler made of the parts BikeBody moves: the frame (with everything fixed to it), the fork (the whole
    steering assembly), the wheels, and on a bicycle the crank set and a pedal; and of frame and fork, what is
    painted in the bike's colour apart (frame_paint, fork_paint). Frame and fork are drawn in the bike's own space,
    the others about their own axis."""

    def __init__(self, name, spec, note, colour):
        self.name, self.spec, self.note, self.colour = name, spec, note, colour
        self.made = {}

    def part(self, key):
        if key not in self.made:
            self.made[key] = Prop("%s_%s" % (self.name, key), "lit")
        if key in ("frame", "fork"):
            self.part(key + "_paint")
            return Painted(self.made[key], self.made[key + "_paint"], self.colour)
        return self.made[key]

    def places(self):
        """Where BikeBody.Build puts each part."""
        (rear, front), r = self.spec["axles"], self.spec["wheel"]
        out = [(key, (0.0, 0.0, 0.0)) for key in ("frame", "frame_paint", "fork", "fork_paint") if len(self.made[key].bm.faces)]
        out += [("wheel", (0.0, rear, r)), ("wheel", (0.0, front, r))] if "wheel" in self.made else [("wheel_rear", (0.0, rear, r)), ("wheel_front", (0.0, front, r))]
        if "crank" in self.made:
            by, bz = self.spec["crank"]
            out += [("crank", (0.0, by, bz)), ("pedal", (PEDAL_OUT, by + CRANK, bz)), ("pedal", (-PEDAL_OUT, by - CRANK, bz))]
        return out

    def whole(self):
        p = Prop(self.name, "lit", pivot="the vehicle's origin: the ground under its middle (not midway between the axles); front wheel to +Y", note=self.note)
        for key, where in self.places():
            absorb(p, self.made[key], where)
        return p

    def split(self):
        top, bottom = self.spec["head"]
        for key in ("fork", "fork_paint"):
            for v in self.made[key].bm.verts:
                v.co -= Vector((0.0, top[0], top[1]))
        for key in ("frame_paint", "fork_paint"):
            repaint(self.made[key], self.colour, NEUTRAL)
        fork = "the top of the steering axis, which is (0, %.2f, %.2f) on the frame; the axis runs from there towards (0, %+.2f, %+.2f)" % (*top, bottom[0] - top[0], bottom[1] - top[1])
        tint = "; on the neutral swatch %s, drawn with PropLibrary.Tinted(colour)" % NEUTRAL
        words = {
            "frame": ("the vehicle's origin (the ground under its middle); front to +Y", "what is fixed to the frame and not in the bike's colour"),
            "frame_paint": ("the vehicle's origin, as the frame", "what of the frame is painted in the bike's colour" + tint),
            "fork": (fork, "the steering assembly: fork, bars and what is fixed to them; grips along X"),
            "fork_paint": (fork, "what of the steering assembly is painted in the bike's colour" + tint),
            "wheel": ("the middle of the axle, which lies along X", "one wheel, used at both axles (%.2f m radius)" % self.spec["wheel"]),
            "wheel_front": ("the middle of the axle, which lies along X", "the front wheel (%.2f m radius); a child of the fork" % self.spec["wheel"]),
            "wheel_rear": ("the middle of the axle, which lies along X", "the rear wheel (%.2f m radius), its sprocket on the left" % self.spec["wheel"]),
            "crank": ("the bottom bracket's middle, its axle along X", "both cranks and the chainring: the right crank points forward (+Y), the left back; %.2f m cranks" % CRANK),
            "pedal": ("its middle", "one pedal, %.2f x %.3f x %.2f m; it goes %.2f m either side of the centre line at the crank's end, kept level" % (*PEDAL, PEDAL_OUT)),
        }
        parts = []
        for key, prop in self.made.items():
            prop.pivot, prop.note = words[key]
            if len(prop.bm.faces):
                parts.append(prop)
        return parts

    def result(self):
        """The whole bike (one mesh, its paint on the colour's own swatch), then its parts (the paint apart, neutral)."""
        whole = self.whole()
        return [whole] + self.split()


def at(point, x=0.0):
    return (x, point[0], point[1])


def axis_of(spec):
    """The steering axis: (y, z) so far from its bottom end to its top end (0 to 1, and beyond either way)."""
    top, bottom = spec["head"]
    return lambda t: (bottom[0] + (top[0] - bottom[0]) * t, bottom[1] + (top[1] - bottom[1]) * t)


def handlebar(p, clamp, grip, paint, radius, mid, bend=0.04, sides=6):
    """One bar from end to end: across the clamp at (y, z), by way of the points `mid` to the grips, which lie along X."""
    gx, gy, gz = grip
    half = fillet([(0.0, clamp[0], clamp[1])] + list(mid) + [(gx - 0.075, gy, gz), (gx + 0.062, gy, gz)], bend, 2)
    path = [Vector((-q.x, q.y, q.z)) for q in half[:0:-1]] + list(half)
    p.add(tube(path, radius, sides), flat(paint) if isinstance(paint, str) else paint, sharp=70)


# ---------------------------------------------------------------------------------------------- bicycles

def bicycle_wheel(p, tyre, rim="aluminium", spokes=12, r=0.34):
    """A wire wheel about its axle: tyre, rim, hub and thin spokes laced from both flanges."""
    origin = (0.0, 0.0, 0.0)
    hoop(p, origin, r - tyre, tyre, "rubber_black", 24, 5)
    well = r - 2 * tyre + 0.004
    about_x(p, [(well, -tyre * 0.75), (well - 0.016, 0.0), (well, tyre * 0.75)], rim, 24, origin, sharp=25, inward=True)
    about_x(p, [(0.008, -0.052), (0.021, -0.032), (0.021, 0.032), (0.008, 0.052)], "stainless", 6, origin, sharp=50)
    for k in range(spokes):
        side = 1.0 if k % 2 else -1.0
        a = TAU * k / spokes
        b = a + side * 0.9                                                   # laced at a tangent to the flange
        rod(p, (side * 0.032, math.cos(b) * 0.019, math.sin(b) * 0.019), (0.0, math.cos(a) * (well - 0.012), math.sin(a) * (well - 0.012)), 0.0022, "stainless", 3, caps=False)


def bicycle_crank(p, ring, paint="stainless"):
    """The crank set about the bottom bracket: axle, chainring, the right crank forward and the left back, the pedals' spindles."""
    rod(p, (-0.080, 0, 0), (0.080, 0, 0), 0.009, "stainless", 4, caps=False)
    about_x(p, [(ring, 0.042), (ring, 0.046)], "stainless", 10, (0, 0, 0), sharp=30, cap_top=True, cap_bottom=True)
    for side, forward in ((1.0, 1.0), (-1.0, -1.0)):
        p.add(tube([(side * 0.073, 0, 0), (side * 0.073, forward * CRANK, 0)], [0.013, 0.010], 5), flat(paint), sharp=70)
        rod(p, (side * 0.073, forward * CRANK, 0), (side * (PEDAL_OUT - PEDAL[0] / 2 + 0.004), forward * CRANK, 0), 0.006, "steel_dark", 3, caps=False)


def bicycle_drive(frame, spec, ring, sprocket):
    """What of the drive stays on the frame: the bottom bracket's shell, the sprocket and the chain."""
    (by, bz), rear, r = spec["crank"], spec["axles"][0], spec["wheel"]
    rod(frame, (-0.036, by, bz), (0.036, by, bz), 0.023, "steel_dark", 6)
    about_x(frame, [(sprocket, 0.040), (sprocket, 0.046)], "steel_dark", 6, (0, rear, r), sharp=30, cap_top=True, cap_bottom=True)
    chain = [(rear, r + sprocket), (rear - sprocket, r), (rear, r - sprocket), (by, bz - ring), (by + ring * 0.72, bz - ring * 0.70), (by + ring, bz), (by + ring * 0.72, bz + ring * 0.70), (by, bz + ring)]
    frame.add(tube([(0.044, y, z) for y, z in chain], 0.006, 3, closed=True), flat("steel_dark"), sharp=180)


def bicycle_saddle(p, seat, half, nose, paint, rear=-0.145):
    """A saddle whose top's middle is the code's seat point: wide and flat where one sits, drawn out to a nose."""
    sy, sz = seat
    rings = []
    for y, w, top, deep in ((rear, 0.55, -0.004, 0.030), (rear + 0.02, 0.92, 0.0, 0.042), (-0.08, 1.0, 0.0, 0.046), (-0.01, 0.86, 0.0, 0.044), (0.055, 0.46, 0.0, 0.036),
                            (nose - 0.03, 0.26, -0.001, 0.030), (nose, 0.17, -0.006, 0.022)):
        w *= half
        t = sz + top
        rings.append([(w, sy + y, t - 0.013), (w * 0.72, sy + y, t), (-w * 0.72, sy + y, t), (-w, sy + y, t - 0.013), (-w * 0.55, sy + y, t - deep), (w * 0.55, sy + y, t - deep)])
    p.add(loft(rings, cap_start=True, cap_end=True), paint, sharp=50)


def brake_lever(p, side, grip):
    gx, gy, gz = grip
    p.add(tube([(side * (gx - 0.075), gy + 0.012, gz - 0.004), (side * (gx - 0.060), gy + 0.036, gz - 0.012), (side * (gx + 0.030), gy + 0.050, gz - 0.018)], 0.0045, 3), flat("stainless"), sharp=70)


def bicycle_city():
    spec = CITY
    b = Bike("bicycle_city", spec,
             "town bicycle, teal step-through frame, 1.79 m long: axles 0.52 m behind and 0.58 m ahead of the pivot, 0.34 m up; bottom bracket (0, -0.08, 0.29), 0.17 m cranks, "
             "the right pedal forward with its middle at (0.13, 0.09, 0.29); saddle top (0, -0.30, 0.93); a swept-back town bar whose rubber grips lie along X centred "
             "(+-0.26, 0.16, 1.14); steering axis from (0, 0.36, 1.00) down to (0, 0.44, 0.70); mudguards, chain guard, carrier, wicker basket, lamp, bell; the kickstand is folded up",
             "garage_teal")
    frame, fork = b.part("frame"), b.part("fork")
    paint = b.colour
    r = spec["wheel"]
    rear, front = (spec["axles"][0], r), (spec["axles"][1], r)
    bb, seat, grip = spec["crank"], spec["seat"], spec["grip"]
    axis = axis_of(spec)
    seat_tube = lambda s: (bb[0] - 0.328 * s, bb[1] + 0.945 * s)             # 71 degrees, to under the saddle
    bicycle_wheel(b.part("wheel"), 0.019)
    bicycle_crank(b.part("crank"), 0.092)
    b.part("pedal").add(box(*PEDAL, bevel=0.005), flat("rubber_black"))
    # The frame: head tube, down tube, the low second tube of a step-through, seat tube, stays.
    rod(frame, at(axis(-0.03)), at(axis(0.86)), 0.021, paint, 8)
    rod(frame, at(axis(0.10)), at(bb), 0.019, paint, 7, caps=False)
    rod(frame, at(axis(0.62)), at(seat_tube(0.23)), 0.016, paint, 7, caps=False)
    rod(frame, at(bb), at(seat_tube(0.47)), 0.017, paint, 7, caps=False)
    rod(frame, at(seat_tube(0.42)), at(seat_tube(0.61)), 0.012, "stainless", 6, caps=False)
    for side in (-1, 1):
        rod(frame, at(bb, side * 0.030), at(rear, side * 0.062), 0.010, paint, 5, caps=False)
        rod(frame, at(seat_tube(0.43), side * 0.016), at(rear, side * 0.062), 0.008, paint, 5, caps=False)
        frame.add(box(0.006, 0.040, 0.030), flat(paint), at=at(rear, side * 0.064))
    # Rear mudguard on its stays, the chain guard, the carrier.
    guard(frame, rear, 0.366, 18.0, 165.0, 0.050, paint, 8)
    for side in (-1, 1):
        tip = (rear[0] + math.cos(math.radians(160.0)) * 0.360, rear[1] + math.sin(math.radians(160.0)) * 0.360)
        rod(frame, at(rear, side * 0.056), at(tip, side * 0.024), 0.0028, "stainless", 3, caps=False)
    slab(frame, [(0.032, 0.310), (0.016, 0.378), (-0.08, 0.402), (-0.28, 0.396), (-0.535, 0.394), (-0.565, 0.374), (-0.535, 0.352), (-0.28, 0.344), (-0.14, 0.328), (-0.08, 0.272)],
         0.050, 0.058, paint)
    deck = 0.728
    frame.add(tube([(-0.062, -0.385, deck), (-0.062, -0.725, deck), (-0.040, -0.750, deck), (0.040, -0.750, deck), (0.062, -0.725, deck), (0.062, -0.385, deck)], 0.0055, 4, closed=True), flat("gloss_black"), sharp=70)
    for y in (-0.50, -0.62):
        rod(frame, (-0.062, y, deck), (0.062, y, deck), 0.004, "gloss_black", 3, caps=False)
    for side in (-1, 1):
        rod(frame, (side * 0.062, -0.70, deck), at(rear, side * 0.070), 0.005, "gloss_black", 3, caps=False)
        rod(frame, (side * 0.062, -0.50, deck), at(rear, side * 0.070), 0.005, "gloss_black", 3, caps=False)
        rod(frame, (side * 0.062, -0.39, deck), at(seat_tube(0.45), side * 0.015), 0.004, "gloss_black", 3, caps=False)
    bicycle_drive(frame, spec, 0.092, 0.036)
    bicycle_saddle(frame, seat, 0.105, 0.145, Paint("wood_dark", grade=False))
    frame.add(box(0.040, 0.050, 0.022), flat("steel_dark"), at=at(seat_tube(0.622)))
    for side in (-1, 1):                                                     # the saddle's two springs
        rod(frame, (side * 0.055, seat[0] - 0.11, seat[1] - 0.045), (side * 0.040, seat[0] - 0.075, seat[1] - 0.10), 0.011, "steel_dark", 4)
    # The kickstand, folded up along the left chain stay.
    rod(frame, (-0.036, bb[0] - 0.085, bb[1] - 0.004), (-0.070, bb[0] - 0.345, bb[1] + 0.012), 0.008, "stainless", 5)
    # The steering assembly. The fork: down the steering axis, then swept forward to the axle.
    crown = axis(-0.085)
    for side in (-1, 1):
        blade = fillet([at(crown, side * 0.048), at(axis(-0.84), side * 0.050), at(front, side * 0.050)], 0.10, 2)
        fork.add(tube(blade, [0.012] * (len(blade) - 2) + [0.009, 0.008], 5), flat(paint), sharp=70)
    rod(fork, at(crown, -0.058), at(crown, 0.058), 0.015, paint, 6)
    rod(fork, at(crown), at(axis(-0.03)), 0.017, "steel_dark", 6, caps=False)
    guard(fork, front, 0.366, 25.0, 200.0, 0.050, paint, 8)
    for a in (32.0, 190.0):
        for side in (-1, 1):
            tip = (front[0] + math.cos(math.radians(a)) * 0.360, front[1] + math.sin(math.radians(a)) * 0.360)
            rod(fork, at(front, side * 0.056), at(tip, side * 0.024), 0.0028, "stainless", 3, caps=False)
    # Quill stem, the bar rising and sweeping back to the grips, levers, bell.
    gx, gy, gz = grip
    top = axis(1.27)
    rod(fork, at(axis(0.86)), at(top), 0.013, "stainless", 6)
    clamp = (0.385, gz - 0.055)
    rod(fork, at((top[0], top[1] - 0.012)), at(clamp), 0.013, "stainless", 6)
    handlebar(fork, clamp, grip, "stainless", 0.011, [(0.100, clamp[0], clamp[1] + 0.004), (0.165, gy + 0.070, gz - 0.004)])
    for side in (-1, 1):
        fork.add(tube([(side * (gx - 0.055), gy, gz), (side * (gx + 0.055), gy, gz)], 0.015, 8), flat("rubber_black"), sharp=60)
        brake_lever(fork, side, grip)
    fork.add(lathe([(0.026, 0.0), (0.022, 0.014), (0.0, 0.024)], 6), flat("chrome"), at=(-0.150, gy + 0.105, gz + 0.004), sharp=40)
    # The basket over the front wheel on its stays, and the lamp under its front.
    middle, floor = 0.600, 0.84
    shell, liner = cup(rounded_rect(0.30, 0.22, 0.04, 1), rounded_rect(0.28, 0.20, 0.03, 1), floor, floor + 0.19, floor + 0.012, top=rounded_rect(0.36, 0.26, 0.05, 1))
    fork.add(shell, Paint("wood"), at=(0, middle, 0), sharp=40)
    fork.add(liner, Paint("wood_dark"), at=(0, middle, 0), sharp=40)
    for side in (-1, 1):
        rod(fork, (side * 0.10, middle + 0.02, floor + 0.004), at(front, side * 0.054), 0.004, "gloss_black", 3, caps=False)
        rod(fork, (side * 0.10, middle - 0.105, floor + 0.10), (side * 0.075, clamp[0], clamp[1]), 0.005, "gloss_black", 3, caps=False)
    lamp = (0.0, middle + 0.075, floor - 0.062)
    about_y(fork, [(0.012, 0.0), (0.034, 0.030), (0.038, 0.062), (0.034, 0.066)], "chrome", 7, lamp, sharp=50)
    about_y(fork, [(0.034, 0.066), (0.0, 0.074)], "glass", 7, lamp, sharp=180)
    rod(fork, (0.0, lamp[1] + 0.03, floor + 0.004), (0.0, lamp[1] + 0.03, lamp[2] + 0.03), 0.005, "gloss_black", 3, caps=False)
    return b.result()


def bicycle_road():
    spec = ROAD
    b = Bike("bicycle_road", spec,
             "road bicycle, red diamond frame with a sloping top tube, black deep rims, derailleur, bottle, 1.72 m long: axles 0.49 m behind and 0.55 m ahead of the pivot, "
             "0.34 m up; bottom bracket (0, -0.07, 0.28), 0.17 m cranks, the right pedal forward with its middle at (0.13, 0.10, 0.28); saddle top (0, -0.29, 0.95); "
             "A DROP BAR: taped tops along X 1.00 m up and 0.42 m ahead of the pivot, where the code has the hands (the grips are the tops, centred (+-0.13, 0.42, 1.00), "
             "0.030 m thick), bending forward to the brake hoods at (+-0.205, 0.515, 1.02) and down into the drops, which end at (+-0.205, 0.43, 0.875); steering axis "
             "from (0, 0.36, 0.99) down to (0, 0.43, 0.72); no stand",
             "garage_red")
    frame, fork = b.part("frame"), b.part("fork")
    paint = b.colour
    r = spec["wheel"]
    rear, front = (spec["axles"][0], r), (spec["axles"][1], r)
    bb, seat, grip = spec["crank"], spec["seat"], spec["grip"]
    axis = axis_of(spec)
    seat_tube = lambda s: (bb[0] - 0.312 * s, bb[1] + 0.950 * s)             # 72 degrees
    bicycle_wheel(b.part("wheel"), 0.013, rim="gloss_black", spokes=12)
    bicycle_crank(b.part("crank"), 0.100, paint="gloss_black")
    b.part("pedal").add(box(*PEDAL, bevel=0.005), flat("rubber_black"))
    rod(frame, at(axis(-0.03)), at(axis(0.74)), 0.022, paint, 8)
    rod(frame, at(axis(0.10)), at(bb), 0.021, paint, 7, caps=False)
    rod(frame, at(axis(0.62)), at(seat_tube(0.55)), 0.017, paint, 7, caps=False)
    rod(frame, at(bb), at(seat_tube(0.60)), 0.017, paint, 7)
    rod(frame, at(seat_tube(0.55)), at(seat_tube(0.665)), 0.012, "gloss_black", 6, caps=False)
    for side in (-1, 1):
        rod(frame, at(bb, side * 0.030), at(rear, side * 0.060), 0.010, paint, 5, caps=False)
        rod(frame, at(seat_tube(0.54), side * 0.014), at(rear, side * 0.060), 0.0075, paint, 5, caps=False)
        frame.add(box(0.006, 0.036, 0.028), flat("stainless"), at=at(rear, side * 0.062))
    bicycle_drive(frame, spec, 0.100, 0.046)
    # Derailleurs: the cage with its two jockey wheels under the sprockets, the front mech over the chainring.
    cage = [(rear[0] + 0.012, rear[1] - 0.045), (rear[0] + 0.030, rear[1] - 0.070), (rear[0] + 0.058, rear[1] - 0.158), (rear[0] + 0.036, rear[1] - 0.168), (rear[0] - 0.002, rear[1] - 0.075)]
    slab(frame, cage, 0.052, 0.060, "gloss_black")
    for y, z in ((rear[0] + 0.022, rear[1] - 0.085), (rear[0] + 0.046, rear[1] - 0.150)):
        about_x(frame, [(0.021, 0.040), (0.021, 0.047)], "stainless", 6, (0, y, z), sharp=30, cap_top=True, cap_bottom=True)
    frame.add(box(0.030, 0.045, 0.030), flat("stainless"), at=(0.056, rear[0] + 0.012, rear[1] - 0.040))
    frame.add(box(0.016, 0.070, 0.034), flat("stainless"), at=(0.046, bb[0] - 0.015, bb[1] + 0.116), rot=(18, 0, 0))
    bicycle_saddle(frame, seat, 0.070, 0.150, Paint("leather_black", grade=False), rear=-0.125)
    frame.add(box(0.034, 0.046, 0.020), flat("steel_dark"), at=at(seat_tube(0.672)))
    # The bottle in its cage on the down tube.
    low, high = Vector(at(bb)), Vector(at(axis(0.10)))
    along = (high - low).normalized()
    out = Vector((0.0, -along.z, along.y))                                   # square off the tube, upwards and back
    foot = low + along * 0.17 + out * 0.058
    frame.add(tube([tuple(foot), tuple(foot + along * 0.150), tuple(foot + along * 0.172), tuple(foot + along * 0.215)], [0.035, 0.035, 0.018, 0.016], 8), Paint("paint_white", grade=False), sharp=40)
    frame.add(tube([tuple(foot + along * 0.212), tuple(foot + along * 0.240)], 0.012, 6), flat("paint_blue"), sharp=40)
    for side in (-1, 1):
        wire = [low + along * 0.15 + out * 0.020, low + along * 0.155 + out * 0.058 + Vector((side * 0.037, 0, 0)), low + along * 0.30 + out * 0.060 + Vector((side * 0.037, 0, 0)), low + along * 0.33 + out * 0.020]
        frame.add(tube([tuple(q) for q in wire], 0.003, 3, caps=False), flat("gloss_black"), sharp=180)
    # The steering assembly: a black fork, a short stem reaching forward, and a drop bar. The code's hands are on
    # its tops (the one part of a drop bar that lies along X); hoods, levers and drops are ahead of and below them.
    crown = axis(-0.075)
    for side in (-1, 1):
        blade = fillet([at(crown, side * 0.042), at(axis(-0.88), side * 0.046), at(front, side * 0.048)], 0.07, 2)
        fork.add(tube(blade, [0.012] * (len(blade) - 2) + [0.009, 0.007], 5), flat("gloss_black"), sharp=70)
    rod(fork, at(crown, -0.050), at(crown, 0.050), 0.014, "gloss_black", 6)
    rod(fork, at(crown), at(axis(-0.03)), 0.017, "gloss_black", 6, caps=False)
    gx, gy, gz = grip
    top = axis(1.01)
    rod(fork, at(axis(0.74)), at(top), 0.016, "gloss_black", 6)
    clamp = (gy, gz)
    rod(fork, at((top[0] - 0.004, top[1] - 0.012)), at((clamp[0] - 0.012, clamp[1] - 0.002)), 0.014, "gloss_black", 6)
    fork.add(box(0.044, 0.034, 0.040, bevel=0.008), flat("gloss_black"), at=at((clamp[0] - 0.004, clamp[1])))
    hood_x = 0.205
    half = fillet([(0.0, gy, gz), (hood_x, gy, gz), (hood_x, gy + 0.085, gz - 0.004), (hood_x, gy + 0.118, gz - 0.060), (hood_x, gy + 0.075, gz - 0.122), (hood_x, gy + 0.010, gz - 0.125)], 0.030, 3)
    bare = [q for q in half if q.x <= 0.050] + [Vector((0.050, gy, gz))]
    fork.add(tube([Vector((-q.x, q.y, q.z)) for q in bare[:0:-1]] + bare, 0.0125, 6), flat("aluminium"), sharp=70)
    for side in (-1, 1):
        tape = [Vector((0.050, gy, gz))] + [q for q in half if q.x > 0.050]
        fork.add(tube([(side * q.x, q.y, q.z) for q in tape], 0.015, 6), flat("rubber_black"), sharp=70)
        # The brake hood on the forward bend, its lever hanging in front of the drop.
        fork.add(tube([(side * hood_x, gy + 0.070, gz + 0.004), (side * hood_x, gy + 0.105, gz + 0.022), (side * hood_x, gy + 0.128, gz + 0.050)], [0.019, 0.018, 0.013], 6), flat("rubber_black"), sharp=60)
        fork.add(tube([(side * hood_x, gy + 0.122, gz + 0.018), (side * (hood_x + 0.004), gy + 0.136, gz - 0.050), (side * (hood_x + 0.006), gy + 0.118, gz - 0.118)], [0.007, 0.006, 0.005], 4), flat("stainless"), sharp=70)
    return b.result()


# ---------------------------------------------------------------------------------------------- motorbikes

def moto_wheel(p, r, width, spokes=5, rim="gloss_black", discs=(1,), disc_at=0.075, disc=0.150, sprocket=None):
    """A cast wheel on a fat tyre, about its axle: tyre, rim well, hub, flat spokes, brake discs (the sides given) and a sprocket."""
    h = width / 2
    centre = (0.0, 0.0, 0.0)
    about_x(p, [(r - 0.088, -h * 0.80), (r - 0.042, -h), (r - 0.010, -h * 0.64), (r, -h * 0.22), (r, h * 0.22), (r - 0.010, h * 0.64), (r - 0.042, h), (r - 0.088, h * 0.80)],
            "rubber_black", 18, centre, sharp=40)
    lip = r - 0.086
    about_x(p, [(lip, -h * 0.80), (lip - 0.020, -h * 0.50), (lip - 0.020, h * 0.50), (lip, h * 0.80)], rim, 18, centre, sharp=30, inward=True)
    about_x(p, [(0.024, -h - 0.012), (0.052, -0.040), (0.052, 0.040), (0.024, h + 0.012)], rim, 8, centre, sharp=40)
    for k in range(spokes):
        a = TAU * k / spokes + 0.35
        ca, sa = math.cos(a), math.sin(a)
        rings = [[(sx * t, ca * q - sa * sw * w, sa * q + ca * sw * w) for sx, sw in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
                 for q, w, t in ((0.040, 0.026, 0.024), (lip - 0.012, 0.015, 0.016))]
        p.add(loft(rings, cap_start=True, cap_end=True), flat(rim), sharp=60)
    for side in discs:
        face(p, disc - 0.055, disc, side * disc_at, side, "stainless", 14, centre)
    if sprocket:
        face(p, 0.045, sprocket, -disc_at - 0.004, -1, "steel_dark", 12, centre, thick=0.006)


def face(p, r0, r1, x, side, paint, segments, at, thick=0.005):
    """A flat ring seen from one side and its edge (a brake disc, a sprocket), about X."""
    profile = [(r1, x - thick), (r1, x), (r0, x)] if side > 0 else [(r0, x), (r1, x), (r1, x + thick)]
    about_x(p, profile, paint, segments, at, sharp=30)


def fork_line(spec):
    """The fork legs' line, parallel to the code's steering axis and through the front axle: (y, z) so far up it;
    and the rake in degrees."""
    (ty, tz), (by, bz) = spec["head"]
    rake = math.atan2(by - ty, tz - bz)
    axle = (spec["axles"][1], spec["wheel"])
    return (lambda s: (axle[0] - math.sin(rake) * s, axle[1] + math.cos(rake) * s)), math.degrees(rake)


def moto_fork(p, spec, half=0.095, inverted=False):
    """Two fork legs from the front axle up to the top yoke, with both yokes reaching back to the steering axis.
    Returns the fork's line, how far up it the legs end, and the rake."""
    line, rake = fork_line(spec)
    top = (spec["head"][0][1] + 0.012 - spec["wheel"]) / math.cos(math.radians(rake))
    for side in (-1, 1):
        if inverted:                                                         # thick gold tubes above, bright sliders below
            rod(p, at(line(-0.035), side * half), at(line(0.30), side * half), 0.019, "chrome", 6)
            rod(p, at(line(0.27), side * half), at(line(top), side * half), 0.027, "gold", 8)
        else:
            rod(p, at(line(-0.04), side * half), at(line(0.31), side * half), 0.027, "aluminium", 8)
            rod(p, at(line(0.29), side * half), at(line(top), side * half), 0.019, "chrome", 6)
        p.add(box(0.030, 0.050, 0.075), flat("steel_dark"), at=at(line(0.185), side * (half - 0.022)), rot=(rake, 0, 0))      # the brake caliper
    for s, deep in ((top - 0.02, 0.028), (top - 0.23, 0.034)):
        y, z = line(s)
        p.add(box(half * 2 + 0.07, 0.120, deep, bevel=0.008), flat("gloss_black"), at=(0.0, y - 0.024, z + 0.009), rot=(rake, 0, 0))
    return line, top, rake


def moto_controls(p, grip):
    """Handlebar grips where the code has the hands, levers and their reservoirs."""
    gx, gy, gz = grip
    for side in (-1, 1):
        p.add(tube([(side * (gx - 0.060), gy, gz), (side * (gx + 0.060), gy, gz)], 0.017, 8), flat("rubber_black"), sharp=60)
        p.add(tube([(side * (gx - 0.085), gy + 0.022, gz), (side * (gx - 0.060), gy + 0.052, gz - 0.004), (side * (gx + 0.050), gy + 0.062, gz - 0.012)], 0.0055, 4), flat("stainless"), sharp=70)
        p.add(box(0.045, 0.034, 0.036), flat("gloss_black"), at=(side * (gx - 0.090), gy + 0.022, gz + 0.012))


def mirror(p, foot, head, paint="gloss_black"):
    rod(p, foot, head, 0.005, paint, 4, caps=False)
    p.add(lathe([(0.040, -0.004), (0.046, 0.004), (0.028, 0.020)], 6, cap_top=True, cap_bottom=True), flat(paint), at=head, rot=(-90, 0, 0), scale=(1.3, 0.85, 1.0), sharp=50)
    p.add(lathe([(0.0, -0.0052), (0.037, -0.0052)], 6), flat("chrome"), at=head, rot=(-90, 0, 0), scale=(1.3, 0.85, 1.0), sharp=180)


def seat_ring(y, half, top, deep):
    return [(half, y, top - 0.022), (half * 0.78, y, top), (-half * 0.78, y, top), (-half, y, top - 0.022), (-half * 0.9, y, top - deep), (half * 0.9, y, top - deep)]


def number_plate(p, where, tilt=-15.0):
    face_ = Paint("gloss_black", grade=False).planar("garage_plate", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.172, 0.123))
    p.add(box(0.18, 0.010, 0.13), face_, at=where, rot=(tilt, 0, 0))


def foot_peg(p, peg, root, paint="stainless"):
    """A round peg along X where the code has the foot, on a hanger from the frame."""
    px, py, pz = peg
    for side in (-1, 1):
        p.add(tube([(side * (px - 0.040), py, pz), (side * (px + 0.040), py, pz)], PEG_RADIUS, 8), flat("rubber_black"), sharp=60)
        p.add(tube([(side * root[0], root[1], root[2]), (side * (px - 0.070), py + 0.004, pz + 0.006), (side * (px - 0.036), py, pz)], 0.010, 5), flat(paint), sharp=70)


def side_stand(p, hinge):
    """Folded up along the left side: the code stands a parked bike upright, and it is ridden like this."""
    foot = (hinge[0] - 0.030, hinge[1] - 0.300, hinge[2] + 0.030)
    rod(p, hinge, foot, 0.011, "gloss_black", 5)
    p.add(box(0.040, 0.070, 0.010), flat("gloss_black"), at=(foot[0] - 0.010, foot[1] - 0.020, foot[2] - 0.006), rot=(-8, 0, 0))


def drive_chain(p, rear, front, big, small, x):
    points = [(rear[0], rear[1] + big), (rear[0] - big, rear[1]), (rear[0], rear[1] - big), (front[0], front[1] - small), (front[0] + small, front[1]), (front[0], front[1] + small)]
    p.add(tube([(x, y, z) for y, z in points], 0.009, 3, closed=True), flat("steel_dark"), sharp=180)


def motorbike_standard():
    spec = STANDARD
    sy, sz = spec["seat"]
    b = Bike("motorbike_standard", spec,
             "naked roadster, navy tank, 2.05 m long: axles 0.651 m behind and 0.723 m ahead of the pivot (the game's scooter's), 0.33 m up; seat top (0, -0.20, 0.83); round "
             "pegs along X centred (+-0.24, -0.10, 0.34); a pulled-back bar whose grips lie along X centred (+-0.31, 0.30, 1.075), 0.034 m thick; steering axis from "
             "(0, 0.40, 0.98) down to (0, 0.47, 0.80); round headlamp, finned twin, exhaust and silencer on the right, twin shocks, mirrors, plate; upright, the side stand folded",
             "garage_navy")
    frame, fork = b.part("frame"), b.part("fork")
    upper, tank_at, lower = Shift(frame, 0.02, 0.04), Shift(frame, 0.0, 0.04), Shift(frame, 0.0, 0.02)       # drawn for a seat at (-0.22, 0.79) and 0.31 m wheels
    paint = b.colour
    r = spec["wheel"]
    rear, front = (spec["axles"][0], r), (spec["axles"][1], r)
    moto_wheel(b.part("wheel_front"), r, 0.115, discs=(-1,), disc_at=0.068)
    moto_wheel(b.part("wheel_rear"), r, 0.150, discs=(1,), disc_at=0.086, disc=0.120, sprocket=0.105)
    # The steering assembly: forks and yokes, mudguard, the lamp between the legs, clocks, indicators, bar, mirrors.
    line, top, rake = moto_fork(fork, spec)
    guard(fork, front, 0.358, 28.0, 152.0, 0.135, paint, 6, crown=0.028)
    for side in (-1, 1):
        rod(fork, (side * 0.064, front[0] - 0.06, front[1] + 0.335), at(line(0.28), side * 0.095), 0.006, "gloss_black", 3, caps=False)
    lamp = (0.0, line(0.585)[0] + 0.066, 0.875)
    about_y(fork, [(0.030, -0.105), (0.078, -0.055), (0.092, -0.005), (0.092, 0.022)], "gloss_black", 10, lamp, sharp=40, cap_bottom=True)
    about_y(fork, [(0.092, 0.022), (0.097, 0.030), (0.087, 0.037)], "chrome", 10, lamp, sharp=40)
    about_y(fork, [(0.087, 0.037), (0.050, 0.049), (0.0, 0.053)], "glass", 10, lamp, sharp=180)
    gx, gy, gz = spec["grip"]
    yoke = line(top - 0.02)
    clamp = (yoke[0] - 0.040, gz - 0.030)
    for side in (-1, 1):
        beam(fork, at(line(0.600), side * 0.095), (side * 0.088, lamp[1] - 0.01, lamp[2]), 0.008, 0.045, "gloss_black")
        clock = (side * 0.046, yoke[0] + 0.030, yoke[1] + 0.050)
        fork.add(lathe([(0.034, 0.0), (0.037, 0.034), (0.030, 0.040)], 7, cap_bottom=True), flat("gloss_black"), at=clock, rot=(38, 0, 0), sharp=50)
        fork.add(lathe([(0.0, 0.0405), (0.030, 0.0405)], 7), flat("paper"), at=clock, rot=(38, 0, 0), sharp=180)
        rod(fork, (side * 0.080, lamp[1] - 0.035, lamp[2] - 0.085), (side * 0.150, lamp[1] - 0.035, lamp[2] - 0.085), 0.006, "gloss_black", 3, caps=False)
        fork.add(box(0.034, 0.050, 0.030), flat("lens_amber"), at=(side * 0.150, lamp[1] - 0.035, lamp[2] - 0.085))
        rod(fork, (side * 0.045, yoke[0] - 0.030, yoke[1]), (side * 0.045, clamp[0], clamp[1]), 0.013, "gloss_black", 5)
        mirror(fork, (side * (gx - 0.090), gy + 0.022, gz + 0.03), (side * 0.305, gy + 0.005, gz + 0.150), "chrome")
    handlebar(fork, clamp, spec["grip"], "chrome", 0.0115, [(0.105, clamp[0], clamp[1]), (0.190, gy + 0.075, gz - 0.006)], bend=0.035)
    moto_controls(fork, spec["grip"])
    # Tank, seat, tail and the covers under the seat.
    tank = [shell_ring(y, half, z0, z1, waist, count=3) for y, half, z0, z1, waist in (
        (-0.075, 0.068, 0.775, 0.835, 1.0), (-0.015, 0.104, 0.735, 0.885, 0.80), (0.08, 0.150, 0.705, 0.945, 0.70), (0.19, 0.172, 0.700, 0.985, 0.74),
        (0.29, 0.168, 0.715, 0.990, 0.86), (0.36, 0.125, 0.745, 0.970, 1.0), (0.398, 0.068, 0.790, 0.940, 1.0))]
    tank_at.add(loft(tank, cap_start=True, cap_end=True), Paint(paint, grade=False), sharp=45)
    tank_at.add(lathe([(0.034, 0.0), (0.034, 0.008), (0.026, 0.014), (0.0, 0.015)], 8), flat("chrome"), at=(0, 0.235, 0.985), sharp=40)
    seat = [seat_ring(sy + y, half, sz + top_, deep) for y, half, top_, deep in (
        (0.185, 0.064, 0.026, 0.050), (0.125, 0.088, -0.006, 0.066), (0.085, 0.122, 0.0, 0.075), (-0.125, 0.150, 0.0, 0.075), (-0.165, 0.145, 0.026, 0.088), (-0.36, 0.125, 0.036, 0.078),
        (-0.44, 0.085, 0.040, 0.055))]
    frame.add(loft(seat, cap_start=True, cap_end=True), Paint("leather_black", grade=False), sharp=40)
    tail = [shell_ring(y, half, z0, z1, 1.0, count=2) for y, half, z0, z1 in ((-0.36, 0.132, 0.655, 0.745), (-0.60, 0.118, 0.700, 0.765), (-0.745, 0.078, 0.735, 0.800), (-0.790, 0.045, 0.752, 0.796))]
    upper.add(loft(tail, cap_start=True, cap_end=True), Paint(paint, grade=False), sharp=45)
    upper.add(box(0.095, 0.022, 0.038), flat("lens_red"), at=(0, -0.792, 0.775))
    for side in (-1, 1):
        slab(upper, [(-0.03, 0.705), (-0.34, 0.700), (-0.31, 0.535), (-0.13, 0.505)], side * 0.095, side * 0.140, paint, shrink=0.82)
        rod(upper, (side * 0.050, -0.80, 0.735), (side * 0.120, -0.80, 0.735), 0.006, "gloss_black", 3, caps=False)
        upper.add(box(0.034, 0.050, 0.030), flat("lens_amber"), at=(side * 0.120, -0.80, 0.735))
    guard(frame, rear, 0.368, 78.0, 150.0, 0.165, "gloss_black", 5, crown=0.03)
    rod(frame, (0.0, -0.780, 0.775), (0.0, rear[0] - 0.335, 0.580), 0.013, "gloss_black", 4)
    number_plate(frame, (0.0, rear[0] - 0.345, 0.550))
    # The frame: the steering head, a double cradle from it under the engine and up to the seat rails.
    (ty, tz), (hy, hz) = spec["head"]
    rod(frame, (0.0, hy + 0.008, hz - 0.016), (0.0, ty + 0.006, tz - 0.016), 0.031, "gloss_black", 8)
    pivot = (-0.195, 0.430)                                                  # where the swing arm hangs
    for side in (-1, 1):
        cradle = [(side * 0.045, hy - 0.035, hz + 0.090), (side * 0.085, 0.335, 0.440), (side * 0.085, 0.245, 0.282), (side * 0.085, -0.160, 0.282), (side * 0.100, pivot[0] - 0.020, pivot[1] + 0.010),
                  (side * 0.105, -0.180, 0.745)]
        frame.add(tube(cradle, 0.016, 5, caps=False), flat("gloss_black"), sharp=70)
        rod(upper, (side * 0.105, -0.150, 0.712), (side * 0.100, -0.665, 0.772), 0.014, "gloss_black", 5)
        rod(frame, (side * 0.100, pivot[0] - 0.020, pivot[1] + 0.020), (side * 0.100, -0.540, 0.792), 0.012, "gloss_black", 5, caps=False)
        # Swing arm and shock.
        beam(frame, (side * 0.100, pivot[0], pivot[1] - 0.010), (side * 0.108, rear[0] - 0.035, rear[1]), 0.028, 0.066, "aluminium", end=(0.028, 0.046))
        low, high = Vector((side * 0.140, rear[0] + 0.075, rear[1] + 0.020)), Vector((side * 0.128, -0.435, 0.745))
        rod(frame, tuple(low), tuple(high), 0.011, "chrome", 5)
        rod(frame, tuple(low + (high - low) * 0.24), tuple(low + (high - low) * 0.88), 0.027, "paint_red", 6)
    # The engine: cases with their side covers, a finned twin leaning forward, the head, the air box behind it.
    cases = [(-0.172, 0.310), (-0.130, 0.275), (0.220, 0.275), (0.285, 0.310), (0.295, 0.420), (0.250, 0.500), (-0.120, 0.500), (-0.178, 0.430)]
    for side in (-1, 1):
        slab(lower, cases, 0.0, side * 0.148, "aluminium", shrink=0.86)
    about_x(lower, [(0.098, 0.138), (0.092, 0.168), (0.055, 0.184), (0.0, 0.186)], "stainless", 8, (0, -0.030, 0.385), sharp=40)
    about_x(lower, [(0.0, -0.176), (0.050, -0.174), (0.078, -0.160), (0.084, -0.138)], "stainless", 8, (0, 0.130, 0.385), sharp=40)
    fins = [[(x, y, 0.024 * k) for x, y in rounded_rect(*((0.250, 0.165, 0.030) if k % 2 == 0 else (0.212, 0.132, 0.022)), 1)] for k in range(8)]
    lower.add(loft(fins, cap_end=True), Paint("aluminium", grade=False), at=(0, 0.185, 0.490), rot=(-12, 0, 0), sharp=22)
    lower.add(box(0.240, 0.170, 0.052, bevel=0.012), flat("stainless"), at=(0, 0.226, 0.680), rot=(-12, 0, 0))
    lower.add(box(0.215, 0.110, 0.135), flat("gloss_black"), at=(0, 0.020, 0.595))
    # Exhaust: two headers down the front (clear of the front tyre), under the cases, into one silencer on the right.
    lower.add(tube(smooth([(0.038, 0.280, 0.630), (0.042, 0.338, 0.530), (0.050, 0.345, 0.360), (0.085, 0.280, 0.240), (0.155, 0.050, 0.232), (0.192, -0.270, 0.252)], 2), 0.021, 5, caps=False), flat("chrome"), sharp=70)
    lower.add(tube(smooth([(-0.038, 0.280, 0.630), (-0.042, 0.338, 0.530), (-0.040, 0.345, 0.360), (-0.010, 0.280, 0.240), (0.100, 0.130, 0.232)], 2), 0.021, 5, caps=False), flat("chrome"), sharp=70)
    lower.add(tube([(0.192, -0.265, 0.252), (0.200, -0.345, 0.272), (0.226, -0.860, 0.440), (0.229, -0.905, 0.455)], [0.022, 0.056, 0.056, 0.040], 8), flat("chrome"), sharp=50)
    lower.add(tube([(0.229, -0.905, 0.455), (0.2295, -0.907, 0.4557)], 0.030, 8), flat("rubber_black"), sharp=50)
    drive_chain(frame, rear, (-0.150, 0.420), 0.105, 0.032, -0.090)
    foot_peg(frame, spec["peg"], (0.100, pivot[0] - 0.020, pivot[1] + 0.010))
    px, py, pz = spec["peg"]
    for side in (-1, 1):                                                     # the brake pedal and the gear lever, under the toes
        rod(frame, (side * (px - 0.055), py, pz - 0.004), (side * (px - 0.020), py + 0.150, pz + 0.004), 0.006, "stainless", 3, caps=False)
        rod(frame, (side * (px - 0.045), py + 0.150, pz + 0.004), (side * (px - 0.005), py + 0.150, pz + 0.004), 0.008, "stainless", 4)
    side_stand(frame, (-0.105, -0.030, 0.290))
    return b.result()


def motorbike_sport():
    spec = SPORT
    sy, sz = spec["seat"]
    b = Bike("motorbike_sport", spec,
             "sports bike in red and white, full fairing with a smoked screen and twin headlamps, 2.05 m long: axles as motorbike_standard; seat top (0, -0.22, 0.84); pegs along X "
             "centred (+-0.24, -0.16, 0.38); CLIP-ONS: a bar clamped to each fork leg under the top yoke, running back and out to a grip along X centred (+-0.27, 0.42, 0.915), "
             "0.034 m thick; steering axis from (0, 0.41, 0.96) down to (0, 0.48, 0.78); the fairing is in the bike's colour over a white belly pan (two shells, no decal: the "
             "colour is the code's); tail cowl, upside-down forks, twin front discs; upright, the side stand folded",
             "garage_red")
    frame, fork = b.part("frame"), b.part("fork")
    upper, tail_at, lower = Shift(frame, 0.02, 0.03), Shift(frame, 0.04, 0.03), Shift(frame, -0.03, 0.02)    # drawn for a seat at (-0.26, 0.81) and 0.31 m wheels
    red = b.colour
    r = spec["wheel"]
    rear, front = (spec["axles"][0], r), (spec["axles"][1], r)
    moto_wheel(b.part("wheel_front"), r, 0.120, discs=(-1, 1), disc_at=0.070)
    moto_wheel(b.part("wheel_rear"), r, 0.170, discs=(1,), disc_at=0.096, disc=0.115, sprocket=0.100)
    line, top, rake = moto_fork(fork, spec, inverted=True)
    guard(fork, front, 0.358, 20.0, 150.0, 0.135, red, 6, crown=0.030)
    for side in (-1, 1):
        rod(fork, (side * 0.064, front[0] - 0.06, front[1] + 0.335), at(line(0.28), side * 0.095), 0.006, "gloss_black", 3, caps=False)

    # The fairing (fixed to the frame): the nose with its lamps and screen, and the sides running down into the belly
    # pan. All of it is the bike's colour but the belly pan, which is a white shell of its own lapped 4 mm over the
    # sides' lower edge (so that the colour can be the code's: a decal across both could not be tinted).
    nose = [shell_ring(y, half, z0, z1, waist, count=3) for y, half, z0, z1, waist in (
        (0.835, 0.050, 0.780, 0.850, 1.0), (0.785, 0.150, 0.715, 0.915, 0.9), (0.690, 0.212, 0.660, 0.975, 0.85), (0.580, 0.236, 0.610, 1.000, 0.85), (0.500, 0.234, 0.560, 0.985, 0.9),
        (0.440, 0.222, 0.480, 0.800, 1.0), (0.405, 0.170, 0.440, 0.740, 1.0))]
    upper.add(loft(nose, cap_start=True, cap_end=True), Paint(red, grade=False), sharp=45)
    for side in (-1, 1):
        upper.add(box(0.112, 0.030, 0.050, bevel=0.012), flat("glass"), at=(side * 0.082, 0.792, 0.772), rot=(-38, 0, -side * 32))

    def screen(u, v):
        w = 0.185 - 0.075 * v
        bow = 1.0 - (2 * u - 1) ** 2
        return ((2 * u - 1) * w, 0.640 - 0.190 * v + 0.065 * bow * (1.0 - 0.3 * v), 0.955 + 0.135 * v - 0.040 * (1.0 - bow) * (1.0 - v))
    upper.add(grid(screen, 4, 3, both_sides=True), Paint("garage_visor", grade=False), sharp=180)
    belly = [shell_ring(y, half, z0, z1, 1.0, count=3, squash=0.6) for y, half, z0, z1 in (
        (0.400, 0.066, 0.365, 0.560), (0.325, 0.166, 0.225, 0.735), (0.160, 0.204, 0.168, 0.705), (0.020, 0.198, 0.160, 0.535), (-0.105, 0.172, 0.166, 0.490), (-0.200, 0.118, 0.190, 0.400),
        (-0.236, 0.058, 0.222, 0.340))]
    lower.add(loft(belly, cap_start=True, cap_end=True), Paint(red, grade=False), sharp=45)
    pan = [shell_ring(y, half + 0.004, z0 - 0.004, top_, 1.0, count=3, squash=0.6) for y, half, z0, top_ in (
        (0.404, 0.066, 0.365, 0.452), (0.325, 0.166, 0.225, 0.446), (0.160, 0.204, 0.168, 0.420), (0.020, 0.198, 0.160, 0.385), (-0.105, 0.172, 0.166, 0.352), (-0.200, 0.118, 0.190, 0.322),
        (-0.240, 0.058, 0.222, 0.300))]
    lower.add(loft(pan, cap_start=True, cap_end=True), Paint("garage_white", grade=False), sharp=45)
    for side in (-1, 1):                                                     # the vent behind the front wheel: three dark slats let into the side
        for k in range(3):
            lower.add(box(0.010, 0.016, 0.105), flat("gloss_black"), at=(side * 0.197, 0.235 - 0.042 * k, 0.560), rot=(-24, 0, side * 6))
    # Twin-spar frame, the engine seen between it and the fairing, the tank over them.
    lower.add(box(0.230, 0.400, 0.240, bevel=0.030), flat("steel_dark"), at=(0, 0.100, 0.615))
    (ty, tz), (hy, hz) = spec["head"]
    pivot = (-0.215, 0.490)
    for side in (-1, 1):
        beam(frame, (side * 0.075, hy - 0.040, hz + 0.090), (side * 0.135, pivot[0], pivot[1]), 0.034, 0.095, "aluminium", end=(0.034, 0.120))
        beam(frame, (side * 0.118, -0.180, 0.650), (side * 0.105, -0.600, 0.875), 0.022, 0.040, "gloss_black")
        beam(frame, (side * 0.125, pivot[0], pivot[1] - 0.020), (side * 0.112, rear[0] - 0.035, rear[1]), 0.032, 0.085, "aluminium", end=(0.030, 0.050))
    tank = [shell_ring(y, half, z0, z1, waist, count=3) for y, half, z0, z1, waist in (
        (-0.095, 0.070, 0.780, 0.860, 1.0), (-0.025, 0.110, 0.742, 0.925, 0.76), (0.080, 0.156, 0.725, 0.992, 0.70), (0.190, 0.170, 0.722, 1.012, 0.76),
        (0.300, 0.150, 0.740, 0.990, 0.90), (0.380, 0.088, 0.780, 0.952, 1.0))]
    upper.add(loft(tank, cap_start=True, cap_end=True), Paint(red, grade=False), sharp=45)
    upper.add(lathe([(0.034, 0.0), (0.034, 0.006), (0.0, 0.010)], 8), flat("gloss_black"), at=(0, 0.215, 1.010), sharp=40)
    seat = [seat_ring(sy + y, half, sz + top_, deep) for y, half, top_, deep in (
        (0.190, 0.066, 0.026, 0.045), (0.120, 0.084, -0.004, 0.058), (0.060, 0.108, 0.0, 0.065), (-0.100, 0.135, 0.0, 0.065), (-0.150, 0.120, 0.018, 0.050))]
    frame.add(loft(seat, cap_start=True, cap_end=True), Paint("leather_black", grade=False), sharp=40)
    cowl = [shell_ring(y, half, z0, z1, 1.0, count=3, squash=0.65) for y, half, z0, z1 in ((-0.385, 0.128, 0.715, 0.850), (-0.550, 0.122, 0.765, 0.930), (-0.730, 0.086, 0.825, 0.968), (-0.875, 0.034, 0.895, 0.958))]
    tail_at.add(loft(cowl, cap_start=True, cap_end=True), Paint(red, grade=False), sharp=45)
    tail_at.add(box(0.150, 0.150, 0.026, bevel=0.010), flat("leather_black"), at=(0, -0.520, 0.927), rot=(-25, 0, 0))
    tail_at.add(box(0.070, 0.020, 0.032), flat("lens_red"), at=(0, -0.872, 0.922))
    for side in (-1, 1):
        slab(tail_at, [(-0.100, 0.760), (-0.400, 0.775), (-0.385, 0.640), (-0.180, 0.540)], side * 0.085, side * 0.128, red, shrink=0.84)
    guard(frame, rear, 0.368, 55.0, 122.0, 0.185, "gloss_black", 4, crown=0.03)
    rod(frame, (0.0, -0.770, 0.890), (0.0, rear[0] - 0.335, 0.580), 0.013, "gloss_black", 4)
    number_plate(frame, (0.0, rear[0] - 0.345, 0.550))
    # Clip-ons: a bar clamped to each fork leg under the top yoke, back and out to the grip (where the code has the
    # hands); mirrors on the fairing.
    gx, gy, gz = spec["grip"]
    leg = line(top - 0.075)
    for side in (-1, 1):
        fork.add(tube([at(leg, side * 0.095), (side * (gx - 0.095), gy + 0.012, gz), (side * (gx - 0.060), gy, gz), (side * (gx + 0.062), gy, gz)], 0.012, 6), flat("gloss_black"), sharp=70)
        fork.add(lathe([(0.034, -0.016), (0.034, 0.016)], 8, cap_top=True, cap_bottom=True), flat("aluminium"), at=at(leg, side * 0.095), rot=(rake, 0, 0), sharp=40)
        mirror(upper, (side * 0.205, 0.620, 0.985), (side * 0.318, 0.585, 1.075))
    moto_controls(fork, spec["grip"])
    # Exhaust: a short fat silencer low on the right.
    low = Shift(frame, 0.0, 0.02)
    low.add(tube([(0.130, -0.130, 0.215), (0.195, -0.300, 0.238), (0.212, -0.360, 0.262), (0.240, -0.700, 0.400), (0.243, -0.745, 0.418)], [0.030, 0.030, 0.062, 0.062, 0.046], 8), flat("stainless"), sharp=50)
    low.add(tube([(0.243, -0.745, 0.418), (0.2432, -0.747, 0.4188)], 0.036, 8), flat("rubber_black"), sharp=50)
    drive_chain(frame, rear, (-0.170, 0.460), 0.100, 0.032, -0.100)
    px, py, pz = spec["peg"]
    for side in (-1, 1):                                                     # the peg plates and their levers
        slab(frame, [(py + 0.130, pz + 0.075), (py - 0.035, pz + 0.020), (py - 0.030, pz - 0.022), (py + 0.030, pz - 0.022), (py + 0.150, pz + 0.035)], side * 0.150, side * 0.162, "aluminium")
        rod(frame, (side * (px - 0.070), py + 0.010, pz), (side * (px - 0.045), py + 0.135, pz - 0.012), 0.006, "stainless", 3, caps=False)
        rod(frame, (side * (px - 0.055), py + 0.135, pz - 0.012), (side * (px + 0.005), py + 0.135, pz - 0.012), 0.008, "stainless", 4)
    foot_peg(frame, spec["peg"], (0.156, py, pz), paint="aluminium")
    side_stand(frame, (-0.135, -0.060, 0.262))
    return b.result()


# ---------------------------------------------------------------------------------------------- helmets
# BikeBody.Helmet puts "helmet" at the Head bone's position (the base of the skull) with the PLAYER's rotation: the
# pivot is the Head bone, +Y the face. The head's middle at a cap's band is HEAD_BONE above that.

HELMET_SIDES = 16


def helmet_ring(a, b, z, y=0.0, count=HELMET_SIDES):
    return [(math.cos(TAU * k / count) * a, y + math.sin(TAU * k / count) * b, z) for k in range(count)]


def lifted(p, dz=HEAD_BONE):
    for v in p.bm.verts:
        v.co.z += dz
    return p


# The full-face shell, bottom to top: half width, half depth, height from the cap's band, how far forward its middle is.
FULL_FACE = ((0.104, 0.129, -0.190, 0.008), (0.116, 0.143, -0.150, 0.008), (0.128, 0.154, -0.080, 0.005), (0.131, 0.158, 0.000, 0.003), (0.126, 0.152, 0.040, 0.000),
             (0.110, 0.133, 0.075, -0.003), (0.080, 0.099, 0.097, -0.005), (0.040, 0.052, 0.108, -0.006))


def helmet():
    inside = (HEAD[0] + HEAD_CLEAR, HEAD[1] + HEAD_CLEAR)
    p = Prop("helmet", "lit", pivot="the Head bone (the base of the skull); visor to +Y",
             note="full-face motorcycle helmet, white with a red stripe and a smoked visor: from 0.080 m below the pivot (chin) to 0.226 m above it; the head's middle is 0.113 m "
                  "above the pivot and the helmet clears a head of %.3f x %.3f m half sizes by %.2f m all round (the opening is %.3f x %.3f m), hollow to 0.208 m above the pivot" % (*HEAD, HEAD_CLEAR, *inside))
    p.add(loft([helmet_ring(a, b, z, y) for a, b, z, y in FULL_FACE], cap_end=True), Paint("garage_white", grade=False), sharp=40)
    low = FULL_FACE[0]
    liner = [helmet_ring(low[0], low[1], low[2], low[3]), helmet_ring(inside[0] + 0.003, inside[1] + 0.003, low[2] - 0.003), helmet_ring(*inside, 0.0), helmet_ring(0.070, 0.090, 0.075), helmet_ring(0.030, 0.040, 0.095)]
    p.add(loft(liner, flip=True), Paint("leather_black", grade=False), sharp=50)
    p.add(loft([liner[-1]], cap_end=True, flip=True), flat("leather_black"))

    def shell(turn_, z, out=1.0):
        """A point on the shell: turn_ in degrees round from the right ear (90 is straight ahead)."""
        a, b, y = [float(np.interp(z, [r[2] for r in FULL_FACE], [r[i] for r in FULL_FACE])) for i in (0, 1, 3)]
        t = math.radians(turn_)
        return (math.cos(t) * a * out, y + math.sin(t) * b * out, z)
    # The visor: a band round the front on the shell's own facets, its lower corners cut away.
    step = 360.0 / HELMET_SIDES
    visor = [[shell(step * k, z if 0 < k < 8 else corner, 1.022) for k in range(9)] for z, corner in ((-0.076, -0.040), (0.0, 0.0), (0.034, 0.030))]
    p.add(loft(visor, closed=False), Paint("garage_visor", grade=False), sharp=60)
    for side in (-1, 1):
        about_x(p, [(0.0, 0.0), (0.021, 0.0), (0.016, 0.009), (0.0, 0.009)] if side > 0 else [(0.0, -0.009), (0.016, -0.009), (0.021, 0.0), (0.0, 0.0)], "gloss_black", 6, (side * 0.133, 0.006, -0.012), sharp=50)
        p.add(box(0.040, 0.008, 0.013), flat("gloss_black"), at=shell(90.0 - side * 24.0, -0.128, 1.014), rot=(0, 0, -side * 24.0))
    # The stripe over the crown from the brow to the nape.
    line = [shell(90.0, z) for z in (0.044, 0.075, 0.097)] + [(0.0, 0.030, 0.108), (0.0, -0.040, 0.108)] + [shell(270.0, z) for z in (0.097, 0.075, 0.040, 0.0, -0.080, -0.150, -0.188)]
    line = [Vector((0.0, y, z)) + Vector((0.0, y, z + 0.03)).normalized() * 0.005 for _, y, z in line]

    def stripe(u, v):
        q = line[int(round(v * (len(line) - 1)))]
        return ((0.5 - u) * 0.046, q.y, q.z)
    p.add(grid(stripe, 1, len(line) - 1), Paint("garage_red", grade=False), sharp=180)
    return lifted(p)


CYCLE = (0.106, 0.140, 0.140, -0.022, -0.004)        # half width, half depth, height of the dome, the rim's height, its middle's shift forward


def bicycle_helmet():
    a, b, c, z0, y0 = CYCLE
    inside = (HEAD[0] + 0.008, HEAD[1] + 0.008)
    p = Prop("bicycle_helmet", "lit", pivot="as helmet: the Head bone (the base of the skull); front to +Y",
             note="NOT ASKED FOR BY CODE. Vented cycling helmet sitting on top of the head: from 0.089 m above the pivot to 0.231 m above it, a short peak, ten vents, straps to "
                  "buckles under the ears; the inside of its rim is %.3f x %.3f m half sizes and it is hollow to 0.208 m above the pivot" % inside)
    count = 14
    rings = [helmet_ring(a * math.cos(math.radians(f)), b * math.cos(math.radians(f)), z0 + c * math.sin(math.radians(f)), y0, count) for f in (0.0, 22.0, 44.0, 63.0, 79.0)]
    p.add(loft(rings, cap_end=True), Paint("garage_teal", grade=False), sharp=40)
    liner = [rings[0], helmet_ring(*inside, z0 - 0.002, 0.0, count), helmet_ring(*inside, 0.020, 0.0, count), helmet_ring(0.066, 0.084, 0.070, 0.0, count), helmet_ring(0.030, 0.040, 0.095, 0.0, count)]
    p.add(loft(liner, flip=True), Paint("gloss_black", grade=False), sharp=50)
    p.add(loft([liner[-1]], cap_end=True, flip=True), flat("gloss_black"))

    def dome(x, y, lift=0.0):
        return (x, y, z0 + lift + c * math.sqrt(max(0.0, 1.0 - (x / a) ** 2 - ((y - y0) / b) ** 2)))
    for x in (-0.060, -0.030, 0.0, 0.030, 0.060):                            # the vents: dark slots fore and aft
        reach = b * math.sqrt(1.0 - (x / a) ** 2) * 0.86
        for start, end in ((0.030, y0 + reach), (y0 - reach, -0.040)):
            p.add(grid(lambda u, v, x=x, s=start, e=end: dome(x + (u - 0.5) * 0.017, s + (e - s) * v, 0.003), 1, 3), Paint("gloss_black", grade=False), sharp=180)

    def peak(u, v):
        t = math.radians(30.0 + 120.0 * u)
        out = 1.0 + 0.30 * v * math.sin(math.radians(180.0 * u)) ** 0.7
        return (math.cos(t) * a * out, y0 + math.sin(t) * b * out, z0 + 0.012 - 0.010 * v)
    p.add(grid(peak, 4, 1, both_sides=True), Paint("gloss_black", grade=False), sharp=180)
    for side in (-1, 1):                                                     # the straps: a V from the rim to a buckle under the ear
        buckle = (side * (HEAD[0] + 0.005), 0.004, -0.095)
        for y in (0.052, -0.060):
            p.add(ribbon([(side * (inside[0] + 0.002), y, z0), buckle], lambda q, s=side: (s, 0, 0), 0.014, 0.002), flat("nylon_black"), sharp=60)
        p.add(box(0.005, 0.022, 0.018), flat("gloss_black"), at=buckle)
    return lifted(p)


# ---------------------------------------------------------------------------------------------- wheel faces
# One unit across (the code scales them, uniformly, to a wheel's diameter): the hub's centre is the origin, the face
# looks along +X, the lip is the full diameter, and a barrel runs back to x -0.25 to fill the inside of the game's tyre.

RIM_SEGMENTS = 24
RIM_LIP = 0.05
RIM_BACK = -0.25


def rim_barrel(p, lip_paint, well_paint, well=0.435):
    """The lip, the well behind it down to the brake's back plate, and the brake itself, seen through the spokes."""
    origin = (0.0, 0.0, 0.0)
    about_x(p, [(well, 0.004), (well + 0.030, 0.034), (0.5, RIM_LIP)], lip_paint, RIM_SEGMENTS, origin, sharp=35, inward=True)
    about_x(p, [(well, -0.200), (well, 0.004)], well_paint, RIM_SEGMENTS, origin, sharp=30, inward=True)
    about_x(p, [(0.0, -0.200), (well, -0.200)], Paint("rubber_black", grade=False), RIM_SEGMENTS, origin, sharp=30, inward=True)
    about_x(p, [(0.12, -0.110), (0.345, -0.110), (0.345, -0.135)], Paint("stainless", grade=False), 14, origin, sharp=30, inward=True)       # the brake disc
    p.add(box(0.050, 0.110, 0.200), flat("paint_red"), at=(-0.095, 0.275, 0.0))                                                                     # and its caliper


def lug_nuts(p, radius, x, paint="chrome", count=5, start=0.0):
    for k in range(count):
        a = TAU * k / count + start
        about_x(p, [(0.024, 0.0), (0.022, 0.020), (0.0, 0.027)], paint, 6, (x, math.sin(a) * radius, math.cos(a) * radius), sharp=50)


def spoke(p, turn_, stations, paint, twist=0.0):
    """A spoke from hub to rim: stations are (radius, half width, x of its back, x of its face), the face crowned."""
    rings = []
    for k, (radius, half, back, face) in enumerate(stations):
        a = turn_ + twist * k / max(1, len(stations) - 1)
        out = Vector((0.0, math.sin(a), math.cos(a)))
        across = Vector((0.0, math.cos(a), -math.sin(a)))
        rings.append([tuple(out * radius + across * (s * half) + Vector((x, 0, 0))) for s, x in ((-1.0, back), (-0.8, face - 0.006), (0.0, face), (0.8, face - 0.006), (1.0, back))])
    p.add(loft(rings, cap_start=True, cap_end=True), flat(paint) if isinstance(paint, str) else paint, sharp=40)


RIM_NOTE = "ONE UNIT ACROSS: scale it uniformly by the wheel's diameter. Hub centre at the pivot, the face looks along +X; the lip is at the full diameter, 0.05 ahead of the pivot, and a barrel " \
           "runs 0.25 behind it to fill the tyre (0.30 deep in all); no tyre. "


def rim_5spoke():
    p = Prop("rim_5spoke", "lit", pivot="the hub's centre; the face looks along +X", note=RIM_NOTE + "Five thick silver alloy spokes with a brake disc and red caliper behind them, hub cap, five nuts")
    rim_barrel(p, Paint("aluminium", grade=False), Paint("steel_dark", grade=False))
    for k in range(5):
        spoke(p, TAU * k / 5, ((0.085, 0.085, -0.060, 0.020), (0.250, 0.058, -0.050, 0.032), (0.445, 0.082, -0.040, 0.016)), Paint("aluminium", grade=False))
    about_x(p, [(0.150, -0.060), (0.150, 0.010), (0.128, 0.022)], Paint("aluminium", grade=False), 10, (0, 0, 0), sharp=40)
    about_x(p, [(0.128, 0.022), (0.070, 0.022), (0.066, 0.036), (0.040, 0.046), (0.0, 0.048)], "chrome", 10, (0, 0, 0), sharp=40)
    lug_nuts(p, 0.100, 0.020, start=TAU / 10)
    return p


def rim_mesh():
    p = Prop("rim_mesh", "lit", pivot="the hub's centre; the face looks along +X", note=RIM_NOTE + "Cross-laced gold mesh centre (ten pairs of thin spokes) in a polished lip, hub cap, five nuts")
    rim_barrel(p, Paint("chrome", grade=False), Paint("steel_dark", grade=False), well=0.415)
    gold = Paint("gold", grade=False)
    for k in range(10):
        for lean in (-1, 1):
            a = TAU * k / 10
            b = a + lean * TAU / 10 * 1.5
            inner = Vector((0.008, math.sin(a) * 0.125, math.cos(a) * 0.125))
            outer = Vector((0.006 + 0.008 * lean, math.sin(b) * 0.418, math.cos(b) * 0.418))
            p.add(tube([tuple(inner), tuple(outer)], 0.017, 4, caps=False), Paint("gold", grade=False), sharp=80)
    about_x(p, [(0.400, -0.020), (0.422, 0.004), (0.400, 0.022)], gold, RIM_SEGMENTS, (0, 0, 0), sharp=40)
    about_x(p, [(0.150, -0.040), (0.150, 0.012), (0.135, 0.024), (0.070, 0.027), (0.060, 0.042), (0.0, 0.047)], gold, 10, (0, 0, 0), sharp=40)
    lug_nuts(p, 0.103, 0.022)
    return p


def rim_steel():
    p = Prop("rim_steel", "lit", pivot="the hub's centre; the face looks along +X", note=RIM_NOTE + "Pressed steel wheel painted white, eight round ventilation holes (dark, not cut), a chrome hub cap, five nuts")
    steel = Paint("paint_white", grade=False)
    origin = (0.0, 0.0, 0.0)
    dish = [(0.150, 0.0), (0.215, 0.0), (0.300, -0.020), (0.385, -0.060), (0.430, -0.060), (0.445, 0.004), (0.470, 0.034), (0.5, RIM_LIP)]
    about_x(p, dish, steel, RIM_SEGMENTS, origin, sharp=35, inward=True)
    slope = math.degrees(math.atan2(0.040, 0.085))
    for k in range(8):
        a = TAU * (k + 0.5) / 8
        hole = lathe([(0.034, 0.0), (0.0, 0.0)], 8)
        p.add(moved(hole, rot=(0, 90 - slope, 0)), Paint("rubber_black", grade=False), at=(-0.037, math.sin(a) * 0.342, math.cos(a) * 0.342), rot=(-math.degrees(a), 0, 0), sharp=180)
    about_x(p, [(0.185, 0.0), (0.180, 0.016), (0.140, 0.034), (0.070, 0.046), (0.0, 0.049)], "chrome", 10, origin, sharp=40)
    lug_nuts(p, 0.240, -0.008, start=TAU / 10)
    return p


# ---------------------------------------------------------------------------------------------- whole wheels for cars
# GarageCar.Wheels hides the car's own wheel and puts one of these in its place, scaled (width / 0.22, radius / 0.34,
# radius / 0.34): a whole wheel, tyre included, 0.34 m in radius and 0.22 m wide, the axle along X, the outer face
# looking along -X (on the car's right side the code turns it half round about Y).

RIM_RADIUS = 0.250                                    # the lip of the rim inside the 0.34 m tyre
RIM_DEEP = 0.55                                       # the unit rim's depth, to metres
RIM_FACE = -0.103                                     # where the lip is: just inside the outer sidewall


def car_wheel(name, rim, words):
    def make():
        p = Prop(name, "lit", pivot="the middle of the wheel; the axle lies along X and the OUTER face looks along -X",
                 note="a whole car wheel, tyre included: 0.34 m radius, 0.22 m wide (GarageCar scales it to each car's wheel, X by the width, Y and Z by the radius). " + words)
        face_ = rim()
        for v in face_.bm.verts:                      # drawn one unit across with its face to +X: turned half round about the upright, and sized
            v.co = Vector((RIM_FACE + (RIM_LIP - v.co.x) * RIM_DEEP, -v.co.y * 2 * RIM_RADIUS, v.co.z * 2 * RIM_RADIUS))
        absorb(p, face_)
        h = WHEEL_WIDTH / 2
        tyre = [(RIM_RADIUS + 0.001, -h + 0.010), (0.272, -h), (0.304, -h), (0.326, -h + 0.011), (WHEEL_RADIUS, -h + 0.038), (WHEEL_RADIUS, h - 0.038), (0.326, h - 0.011), (0.304, h), (0.272, h),
                (0.215, h - 0.008), (0.0, h - 0.008)]
        about_x(p, tyre, Paint("rubber_black", grade=False), RIM_SEGMENTS, (0.0, 0.0, 0.0), sharp=40)
        return p
    make.__name__ = name
    return make


wheel_5spoke = car_wheel("wheel_5spoke", rim_5spoke, "Five thick silver alloy spokes, a brake disc and red caliper seen between them, chrome cap, five nuts")
wheel_mesh = car_wheel("wheel_mesh", rim_mesh, "Cross-laced gold mesh centre (ten pairs of thin spokes) in a polished lip, the same brake behind")
wheel_steel = car_wheel("wheel_steel", rim_steel, "Pressed steel wheel painted white, eight round ventilation holes (dark, not cut through), a chrome hub cap, five nuts")


# ---------------------------------------------------------------------------------------------- bolt-on parts for cars
# Where GarageCar.Fit puts them, and how it scales them, is in each one's pivot and note.

def car_spoiler():
    p = Prop("car_spoiler", "lit", pivot="on the boot deck under the middle of its two feet; leading edge to +Y",
             note="raised rear wing, satin black: 1.40 m wide (GarageCar scales X by the deck's half width / 0.70), 0.18 m high above the pivot, 0.26 m deep: an aerofoil 0.12 to "
                  "0.17 m up between two end plates, on two uprights 0.42 m either side of the middle whose feet reach 0.02 m BELOW the pivot (into the deck, so that no gap "
                  "shows on a sloping one)")
    black = Paint("gloss_black", grade=False)
    section = ((0.105, 0.128), (0.080, 0.142), (0.020, 0.154), (-0.070, 0.160), (-0.118, 0.170), (-0.122, 0.165), (-0.070, 0.142), (0.020, 0.130), (0.085, 0.122))
    p.add(loft([[(x, y, z) for y, z in section] for x in (-0.692, 0.692)], cap_start=True, cap_end=True), black, sharp=35)
    for side in (-1, 1):
        slab(p, [(0.130, 0.105), (0.130, 0.158), (0.050, 0.180), (-0.130, 0.180), (-0.130, 0.110), (-0.050, 0.085), (0.070, 0.085)], side * 0.688, side * 0.700, black, shrink=0.94)
        slab(p, [(0.078, -0.020), (0.068, 0.060), (0.080, 0.132), (-0.055, 0.150), (-0.040, 0.060), (-0.084, -0.020)], side * 0.410, side * 0.430, black)
        p.add(box(0.056, 0.190, 0.032, bevel=0.004), black, at=(side * 0.420, 0.0, -0.004))
        for y in (-0.070, 0.070):
            p.add(lathe([(0.008, 0.0), (0.008, 0.004), (0.0, 0.006)], 6), flat("stainless"), at=(side * 0.420, y, 0.012), sharp=50)
    return p


def car_spoiler_lip():
    p = Prop("car_spoiler_lip", "lit", pivot="the centre of its underside (it sits on a boot lid); front edge to +Y",
             note="NOT ASKED FOR BY CODE (it has one spoiler, the wing). Boot-lid lip spoiler 1.40 m wide, 0.18 m deep, rising from nothing at the front to a 0.06 m "
                  "kicked-up edge at the back; gloss black")
    rings = []
    for x, scale, back in ((-0.700, 0.45, 0.030), (-0.675, 0.80, 0.012), (-0.560, 1.0, 0.0), (0.0, 1.0, -0.004), (0.560, 1.0, 0.0), (0.675, 0.80, 0.012), (0.700, 0.45, 0.030)):
        section = ((0.090, 0.0), (0.090, 0.006), (0.030, 0.016), (-0.050, 0.038), (-0.086, 0.060), (-0.090, 0.060), (-0.090, 0.0))
        rings.append([(x, y - (back if y > 0 else 0.0), z * scale) for y, z in section])
    p.add(loft(rings, cap_start=True, cap_end=True), Paint("gloss_black", grade=False), sharp=35)
    return p


def car_roof_bar():
    p = Prop("car_roof_bar", "lit", pivot="on the roof under the middle of the bar, at the height its feet stand on; the bar lies along X",
             note="ONE roof bar (GarageCar fits two): 1.20 m long (X scaled by (the roof's half width + 0.04) / 0.60), 0.11 m high above the pivot, 0.06 m deep: an aluminium "
                  "aero bar 0.080 to 0.110 m up with rubber end caps, on two black clamp feet 0.52 m either side of the middle that reach 0.02 m BELOW the pivot (into the roof)")
    bar = [(0.030, 0.094), (0.017, 0.110), (-0.017, 0.110), (-0.030, 0.094), (-0.017, 0.080), (0.017, 0.080)]
    p.add(loft([[(x, y, z) for y, z in bar] for x in (-0.594, 0.594)], cap_start=True, cap_end=True), flat("aluminium"), sharp=40)
    p.add(box(1.120, 0.020, 0.003), flat("rubber_black"), at=(0, 0, 0.1090))                                # the rubber strip along its top
    for side in (-1, 1):
        foot = [[(side * 0.520 + sx * w, sy * d, z) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for w, d, z in ((0.046, 0.030, -0.020), (0.046, 0.030, 0.014), (0.032, 0.026, 0.060), (0.028, 0.024, 0.086))]
        p.add(loft(foot, cap_start=True, cap_end=True), flat("gloss_black"), sharp=40)
        p.add(box(0.008, 0.050, 0.026, bevel=0.003), flat("rubber_black"), at=(side * 0.596, 0.0, 0.095))
        p.add(lathe([(0.007, 0.0), (0.007, 0.004), (0.0, 0.006)], 6), flat("stainless"), at=(side * 0.520, -0.0245, 0.036), rot=(90, 0, 0), sharp=50)
    return p


def car_bull_bar():
    p = Prop("car_bull_bar", "lit", pivot="the bottom centre of the hoop, 0.02 m behind the front of its tubes; it faces +Y",
             note="chrome tubular bull bar: 1.00 m wide and 0.60 m high as modelled (GarageCar scales X by the nose's half width / 0.50 and Y by the height of the nose / 0.60), "
                  "0.14 m deep: an outer hoop with its lower corners cut in, two uprights 0.275 m either side of the middle, a rail 0.14 m up, a dark skid plate under it, "
                  "and two mounts at the uprights reaching 0.12 m BEHIND the pivot at rail height (into the nose)")
    chrome = "chrome"
    r, y = 0.0225, -0.0025
    low, high, half = r, 0.603 - r, 0.503 - r
    outer = fillet([(0.0, y, low), (0.300, y, low), (half, y, 0.170), (half, y, high), (-half, y, high), (-half, y, 0.170), (-0.300, y, low), (0.0, y, low)], 0.10, 2)
    p.add(tube(outer, r, 6, caps=False), flat(chrome), sharp=70)
    for side in (-1, 1):
        rod(p, (side * 0.275, y, low), (side * 0.275, y, high), 0.020, chrome, 6, caps=False)
        beam(p, (side * 0.275, -0.116, 0.140), (side * 0.275, y, 0.140), 0.030, 0.052, "steel_dark")
        p.add(box(0.090, 0.008, 0.120, bevel=0.003), flat("steel_dark"), at=(side * 0.275, -0.116, 0.140))
        for z in (0.105, 0.175):
            p.add(lathe([(0.008, 0.0), (0.008, 0.004), (0.0, 0.006)], 6), flat("stainless"), at=(side * 0.275, -0.112, z), rot=(-90, 0, 0), sharp=50)
    rod(p, (-0.440, y, 0.140), (0.440, y, 0.140), 0.020, chrome, 6, caps=False)
    rod(p, (-half, y, 0.370), (half, y, 0.370), 0.016, chrome, 6, caps=False)
    plate = [(-0.270, 0.030), (0.270, 0.030), (0.270, 0.128), (-0.270, 0.128)]
    p.add(loft([[(x, y - 0.006, z) for x, z in plate], [(x, y + 0.004, z) for x, z in plate]], cap_start=True, cap_end=True), flat("steel_dark"), sharp=40)
    for x in (-0.18, -0.06, 0.06, 0.18):                                     # the slots pressed into the plate
        p.add(box(0.070, 0.004, 0.050, bevel=0.0015), flat("rubber_black"), at=(x, y + 0.005, 0.079))
    return p


# ---------------------------------------------------------------------------------------------- bike share
# BikePlaces.Dress puts "bike_dock" at the place's origin and stands two city bicycles at x -0.8 and 0, facing +Y,
# their pivots at y 0: their front wheels (axle at y 0.58) are then in the hoops, which are in a line at y 0.80.

DOCK_LINE = 0.80
DOCK_HOOPS = (-0.80, 0.0, 0.80)
DOCK_POST = 1.38


def bike_dock():
    p = Prop("bike_dock", "glow", pivot="ground centre, 0.80 m BEHIND the line of the hoops (the bicycles stand at the pivot's line, facing +Y, front wheels in the hoops)",
             note="bike-share dock for three bicycles: a rail on the ground from x -1.14 to +1.46 at y 0.75 to 0.90, three wheel hoops 0.72 m high at x -0.80, 0 and +0.80 "
                  "(0.087 m clear between their legs, each with a lock and a status light), and a green pay post 1.25 m high at x +1.38 whose screen and numbered panel face "
                  "-Y, towards whoever stands by the bicycles. 2.60 m wide, 1.25 m high, 0.20 m deep in all. A city bicycle at (x of a hoop, 0, 0) facing +Y has its front tyre "
                  "between that hoop's legs, clear of the rail")
    yl = DOCK_LINE
    rail = [(x + 0.16, y + yl + 0.025) for x, y in rounded_rect(2.600, 0.150, 0.030, 1)]
    p.add(prism(rail, 0.0, 0.038, bevel=0.008), "steel_dark")
    for x in DOCK_HOOPS:
        hoop_ = fillet([(x - 0.0625, yl, 0.030), (x - 0.0625, yl, 0.704), (x + 0.0625, yl, 0.704), (x + 0.0625, yl, 0.030)], 0.055, 3)
        p.add(tube(hoop_, 0.019, 6), flat("aluminium"), sharp=70)
        for side in (-1, 1):
            p.add(lathe([(0.030, 0.0), (0.030, 0.008), (0.019, 0.016)], 8, cap_top=True), flat("steel_dark"), at=(x + side * 0.0625, yl, 0.036), sharp=50)
        # The lock on the right leg, with its light towards the rider.
        p.add(box(0.050, 0.070, 0.110, bevel=0.010), Paint("garage_bay", grade=False), at=(x + 0.094, yl, 0.560))
        about_y(p, [(0.014, 0.0), (0.014, 0.004), (0.0, 0.008)], Paint("plastic_green", grade=False), 6, (x + 0.094, yl - 0.035, 0.580), sharp=50, tilt=180.0)
    post = [[(DOCK_POST + x, yl + y, z) for x, y in rounded_rect(w, d, 0.028, 1)] for w, d, z in ((0.160, 0.120, 0.030), (0.160, 0.120, 1.170), (0.140, 0.100, 1.225))]
    p.add(loft(post, cap_end=True), Paint("garage_bay"), sharp=40)
    top = [[(DOCK_POST + x, yl + y, z) for x, y in rounded_rect(w, d, 0.026, 1)] for w, d, z in ((0.140, 0.100, 1.225), (0.100, 0.070, 1.250))]
    p.add(loft(top, cap_end=True), flat("steel_dark"), sharp=40)
    sticker(p, "garage_kiosk_screen", (DOCK_POST, yl - 0.060, 1.060), (0, -1, 0), (1, 0, 0), (0.128, 0.105), lift=0.0065)
    p.add(box(0.140, 0.006, 0.117), flat("gloss_black"), at=(DOCK_POST, yl - 0.0615, 1.060))
    sticker(p, "garage_dock_panel", (DOCK_POST, yl - 0.060, 0.800), (0, -1, 0), (1, 0, 0), (0.128, 0.165), lift=0.002)
    p.add(box(0.060, 0.016, 0.090, bevel=0.005), flat("gloss_black"), at=(DOCK_POST, yl - 0.066, 0.940))          # the card reader
    p.add(box(0.040, 0.004, 0.006), flat("plastic_green"), at=(DOCK_POST, yl - 0.075, 0.962))
    return p


def bike_dock_kiosk():
    p = Prop("bike_dock_kiosk", "glow", pivot="ground centre; it faces +Y",
             note="bike-share payment terminal 0.50 m wide, 1.70 m high, 0.30 m deep: a lit screen 1.20 to 1.42 m up, a card reader beside it, a map of the docks below, "
                  "'BIKES' across the top in 0.12 m letters on both faces")
    p.add(prism(rounded_rect(0.500, 0.300, 0.04, 1), 0.0, 0.050, bevel=0.010), "steel_dark")
    body = [[(x, y, z) for x, y in rounded_rect(w, d, 0.035, 1)] for w, d, z in ((0.420, 0.200, 0.048), (0.420, 0.200, 1.500), (0.400, 0.180, 1.535))]
    p.add(loft(body, cap_end=True), Paint("garage_bay"), sharp=40)
    header = Paint("garage_bay", grade=False).planar("garage_kiosk_header", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.480, 0.160)).planar("garage_kiosk_header", (0, -1, 0), (1, 0, 0), (0, 0, 0), (0.480, 0.160))
    p.add(box(0.500, 0.110, 0.168, bevel=0.006), header, at=(0, 0, 1.616))
    # The sloping console with the screen, and the card reader.
    p.add(box(0.360, 0.060, 0.300, bevel=0.012), flat("steel_dark"), at=(0, 0.090, 1.300), rot=(10, 0, 0))
    screen = Paint("gloss_black", grade=False).planar("garage_kiosk_screen", (0, 1, 0), (-1, 0, 0), (0, 0, 0), (0.250, 0.205))
    p.add(box(0.250, 0.010, 0.205), screen, at=(-0.040, 0.124, 1.318), rot=(10, 0, 0))
    p.add(box(0.070, 0.030, 0.110, bevel=0.008), flat("gloss_black"), at=(0.135, 0.120, 1.290), rot=(10, 0, 0))
    p.add(box(0.050, 0.006, 0.008), flat("plastic_green"), at=(0.135, 0.137, 1.318), rot=(10, 0, 0))
    sticker(p, "garage_kiosk_map", (0.0, 0.100, 0.800), (0, 1, 0), (-1, 0, 0), (0.330, 0.450), lift=0.002)
    for z in (0.560, 1.040):
        p.add(box(0.360, 0.010, 0.016), flat("aluminium"), at=(0, 0.103, z))
    for x in (-0.176, 0.176):
        p.add(box(0.016, 0.010, 0.496), flat("aluminium"), at=(x, 0.103, 0.800))
    return p


# ---------------------------------------------------------------------------------------------- the list

# A bike's maker gives the whole bike and then its parts (each within the whole bike's budget).
PROPS = [
    (bicycle_city, 2450), (bicycle_road, 2450), (motorbike_standard, 3800), (motorbike_sport, 3500),
    (helmet, 540), (bicycle_helmet, 420),
    (wheel_5spoke, 1200), (wheel_mesh, 1300), (wheel_steel, 1200),
    (car_spoiler, 330), (car_spoiler_lip, 140), (car_roof_bar, 220), (car_bull_bar, 700),
    (bike_dock, 980), (bike_dock_kiosk, 320),
]

RIDER = {"people": (((0.7, -1.25), 0),)}             # a standing person beside the back wheel (beside the middle they would hide it)
SIDES = {
    "bicycle_city": RIDER, "bicycle_road": RIDER, "motorbike_standard": {"people": (((0.8, -1.45), 0),)}, "motorbike_sport": {"people": (((0.8, -1.45), 0),)},
    "bike_dock": {"people": (((-1.6, 0.3), 0),)}, "bike_dock_kiosk": {"people": (((0.7, 0.4), 180),)},
    # a car's roof 1.2 m wide, a nose (its front 0.07 m behind the hoop), a boot deck
    "car_roof_bar": {"references": (((1.12, 1.60, 0.02), (0, 0, -0.010)),)},
    "car_bull_bar": {"references": (((1.90, 0.40, 0.75), (0, -0.270, 0.275)),)},
    "car_spoiler": {"references": (((1.56, 0.60, 0.02), (0, 0.04, -0.010)),)},
}
