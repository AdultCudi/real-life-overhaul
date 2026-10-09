"""Clips for everyday life: at home (Scripts/Home), with friends (Scripts/Social) and out for the evening or the
afternoon (Scripts/Leisure). Same rules as clips.py.

Where things are, for whoever places the character (x right, y ahead, z up, from the character's origin):
    cook_stir            the hob is 0.45 m ahead, 0.92 m up (the pan's base); the pan's handle points back and left
    pushup               the body lies ALONG the character's forward axis, CENTRED on the origin: toes 0.79 m behind
                         it, hands 0.50 m ahead, the crown 0.90 m ahead (PUSHUP_FEET moves all of it)
    situp                on the back, the hips 0.08 m ahead of the origin, feet 0.60 m ahead, the head 0.70 m BEHIND
    eat_sit, sit_chat    the origin is the middle of the seat (0.45 m up) at ground level, as chess_sit; the table
                         top is 0.75 m up with its near edge 0.20 m ahead, the plate's middle 0.35 m ahead
"""
import math

import numpy as np

from clips_jobs import CUP, at_side
from clips_life import BOARD, SEAT, TABLE  # noqa: F401
from motion import Clip, c, hang, idle, lag, s, shuffle, stand, weight  # noqa: F401
from rig import PALM, Grip, Pose, basis, between, blend_holds, ease, keys, turn, turn_about, unit, wave  # noqa: F401

TAU = 2 * math.pi
SIDES = (("L", -1.0), ("R", 1.0))


# ---------------------------------------------------------------------------------------------- shared

def mouth(p):
    """Where the lips are, once hips, chest and head are set: a little ahead of and just above the Head joint
    (0.065 m under the nose the sheets draw)."""
    solved = p.solve()
    return solved.at["Head"] + solved.turns["Head"] @ np.array([0.0, 0.105, 0.015])


def rim_at(point, rim, radius, tilt=0.0, yaw=-10.0):
    """The pose of a cup (rim `rim` above its pivot, `radius` across the half) whose near lip is at `point`,
    tipped back `tilt` degrees towards the drinker."""
    turned = turn(pitch=-tilt) @ turn(yaw=yaw)                     # tipped towards the drinker whichever way the cup is turned
    near = turned.T @ np.array([0.0, -1.0, 0.0])                   # the side of the cup that faces the drinker
    near = unit(np.array([near[0], near[1], 0.0])) * radius
    return turned, np.asarray(point, dtype=float) - turned @ (near + (0.0, 0.0, rim))


def reach(p, side, k, to, fingers, palm, elbow=None, curl="relaxed", arc=(0.0, 0.0, 0.0), rest=None):
    """A free hand part of the way (k) from hanging at the side to somewhere, bulging out by `arc` on the way."""
    sign = 1.0 if side == "R" else -1.0
    rest = at_side(p, side) if rest is None else np.asarray(rest, dtype=float)
    wrist = rest + (np.asarray(to, dtype=float) - rest) * k + np.asarray(arc, dtype=float) * math.sin(math.pi * k)
    p.hand(side, wrist, fingers=unit(unit(np.array([sign * 0.1, 0.3, -1.0])) * (1 - k) + unit(fingers) * k), palm=np.array([-sign, 0.0, 0.0]) * (1 - k) + unit(palm) * k,
           elbow=elbow if elbow is not None else (sign * 0.6, -0.6, -0.5), curl=blend_holds("relaxed", curl, k))
    return wrist


def sit(p, lean=0.0, yaw=0.0, roll=0.0, back=0.0, feet=(0.30, 0.26), spread=0.15):
    """Seated on something 0.45 m high whose middle is the character's origin (chess_sit's seat)."""
    p.hips((0.0, -0.02 - back, SEAT + 0.085), pitch=lean, yaw=yaw, roll=roll)
    p.foot("L", (-spread, feet[0], 0.0), yaw=-10, knee=(-0.25, 1.0, 0.6))
    p.foot("R", (spread, feet[1], 0.0), yaw=10, knee=(0.25, 1.0, 0.6))
    return p


def palm_at(p, side, centre, fingers, palm, elbow=None, curl="relaxed"):
    """A free hand placed by the middle of its palm (what touches somebody's shoulder, another palm) rather than by its wrist."""
    sign = 1.0 if side == "R" else -1.0
    hand = basis(fingers, palm) @ basis((sign, 0, 0), (0, 0, -1)).T
    p.hand(side, np.asarray(centre, dtype=float) - hand @ PALM[side], turned=hand, elbow=elbow, curl=curl)
    return p


def blend_hand(p, side, k, to, fingers, palm, elbow=None, curl="relaxed", arc=(0.0, 0.0, 0.0)):
    """As `reach`, but `to` is where the middle of the palm ends up."""
    sign = 1.0 if side == "R" else -1.0
    f = unit(unit(np.array([sign * 0.1, 0.3, -1.0])) * (1 - k) + unit(fingers) * k)
    n = np.array([-sign, 0.0, 0.0]) * (1 - k) + unit(palm) * k
    hand = basis(f, n) @ basis((sign, 0, 0), (0, 0, -1)).T
    rest = at_side(p, side)
    wrist = rest + (np.asarray(to, dtype=float) - hand @ PALM[side] - rest) * k + np.asarray(arc, dtype=float) * math.sin(math.pi * k)
    p.hand(side, wrist, turned=hand, elbow=elbow if elbow is not None else (sign * 0.6, -0.6, -0.5), curl=blend_holds("relaxed", curl, k))
    return wrist


_FLIP = np.diag([-1.0, 1.0, 1.0])


def mirrored(pose):
    """The same clip for the other side of the body: left and right exchanged. (Not for a clip with a tool.)"""
    def point(v):
        return None if v is None else np.asarray(v, dtype=float) * (-1.0, 1.0, 1.0)

    def turned(m):
        return None if m is None else _FLIP @ m @ _FLIP

    def other(t):
        p, q = pose(t), Pose()
        q._hips, q._chest, q._head = (point(p._hips[0]), turned(p._hips[1])), turned(p._chest), turned(p._head)
        for a, b in (("L", "R"), ("R", "L")):
            sole, foot, knee, toes = p._feet[a]
            q._feet[b] = (point(sole), turned(foot), point(knee), toes)
            if a in p._hands:
                wrist, hand, elbow = p._hands[a]
                q._hands[b] = (point(wrist), turned(hand), point(elbow))
            q._curl[b], q._shrug[b] = p._curl[a], p._shrug[a]
        if hasattr(p, "_look"):
            q._look = (point(p._look[0]),) + tuple(p._look[1:])
        if hasattr(p, "_spine"):
            q._spine = p._spine
        return q
    return other


# ---------------------------------------------------------------------------------------------- at home

HOB = np.array([0.02, 0.45, 0.92])                    # the middle of the pan's base on the hob
PAN_YAW = 120.0                                       # the handle points back and to the left, at the left hand
PAN = Grip("L", at=(0.23, 0.0, 0.045), thumb=(-1, 0, 0), back=(0, -1, 0.6), hold="handle")


def cook_stir(t):
    # At the hob, bent to it from the hips with the head down: the left hand keeps the pan by its handle, the
    # right stirs round it with a spoon, the whole forearm going round from the elbow (three turns a loop); once a
    # loop the pan is given a shake. The weight goes from foot to foot.
    stir = TAU * 3 * t
    late = stir - 0.5
    shake = math.sin(math.pi * keys(t, (0.62, 0.0), (0.86, 1.0))) * s(t, 9)
    p = Pose()
    sway = idle(p, t, width=0.16, lean=11, amount=0.8, shift=(0.0, 0.05))
    p.chest(pitch=5, yaw=3 * math.cos(late), roll=1.5 * sway)
    pan = HOB + (0.008 * shake, 0.022 * shake, 0.0)
    p.tool(turn(yaw=PAN_YAW + 2 * shake), pan, PAN, elbows={"L": (-0.8, -0.5, -0.2)})
    wrist = pan + (0.085 + 0.05 * math.cos(late), -0.125 + 0.05 * math.sin(late), 0.215 + 0.012 * math.sin(2 * late))
    p.hand("R", wrist, fingers=(-0.35, 0.85, -0.35), palm=(-0.85, -0.45, -0.2), elbow=(0.75, -0.5, -0.3), curl="handle")
    p.look_at(pan + (0.0, -0.05, 0.0), down=42)
    return p


PUSHUP_FEET = -0.70                                   # the ankles from the origin: the body is centred on where the player stood


def pushup(t):
    # Press-ups: one straight line from the heels through the knees and hips to the head, down slowly until the
    # chest is a hand's breadth off the floor with the head still up, a moment there, and up faster. Hands under
    # the shoulders, elbows going back at 45 degrees.
    down = keys(t, (0.0, 0.0), (0.46, 1.0), (0.56, 1.0), (1.0, 0.0))
    rise = 16.0 - 13.0 * down                         # the body's slope
    along = np.array([0.0, math.cos(math.radians(rise)), math.sin(math.radians(rise))])
    ankle = np.array([0.0, PUSHUP_FEET, 0.168])
    pelvis = turn(pitch=90 - rise)
    p = Pose()
    p.hips(ankle + 0.7935 * along - pelvis @ np.array([0.0, 0.027, -0.005]), pitch=90 - rise)      # the legs at full stretch: no sag at the knee
    p.chest(pitch=-2)
    for side, sign in SIDES:
        p.foot(side, ankle=(sign * 0.085, PUSHUP_FEET, 0.168), pitch=80, knee=(0.0, 0.2, -1.0), toes=-55)
        p.hand(side, (sign * 0.22, PUSHUP_FEET + 1.22, 0.035), fingers=(-sign * 0.12, 1.0, 0.0), palm=(0, 0, -1), elbow=(sign * 0.7, -0.7, 0.0), curl="flat")
    p.head(pitch=-30 - 6 * down)
    return p


SITUP_HIPS = 0.08                                     # the hips ahead of the origin: lying, the body is centred on it


def situp(t):
    # Sit-ups: on the back with the knees bent and the feet flat, fingertips at the temples and the elbows wide;
    # curls up until the chest is near the thighs, and down under control.
    up = keys(t, (0.0, 0.0), (0.42, 1.0), (0.50, 1.0), (1.0, 0.0))
    late = keys(lag(t, 0.04), (0.0, 0.0), (0.42, 1.0), (0.50, 1.0), (1.0, 0.0))
    p = Pose()
    p.hips((0.0, SITUP_HIPS - 0.02 * up, 0.105), pitch=-84 + 54 * up)
    for side, sign in SIDES:
        p.foot(side, (sign * 0.13, SITUP_HIPS + 0.52, 0.0), yaw=sign * 6, knee=(sign * 0.1, 0.1, 1.0))
    p.chest(pitch=4 + 14 * up)
    p.head(pitch=4 + 12 * late)
    solved = p.solve()
    head, turned, chest = solved.at["Head"], solved.turns["Head"], solved.turns["UpperChest"]
    for side, sign in SIDES:
        p.hand(side, head + turned @ np.array([sign * 0.165, -0.09, 0.085]), fingers=turned @ np.array([-sign * 0.45, -0.15, 0.88]), palm=turned @ np.array([-sign * 0.85, 0.35, -0.4]),
               elbow=chest @ np.array([sign * 1.0, 0.05 + 0.2 * up, 0.1]), curl="relaxed")
    return p


def stretch(t):
    # Standing stretches: both arms straight up overhead and a rise on the toes; a bend to the left and to the
    # right with the arms still up; the arms down along the sides; a forward fold with the back rounding and the
    # head and hands hanging; and up again.
    over = keys(t, (0.02, 0.0), (0.11, 1.0), (0.58, 1.0), (0.66, 0.0), (1.0, 0.0))
    tall = math.sin(math.pi * keys(t, (0.10, 0.0), (0.26, 1.0)))
    bend = keys(t, (0.26, 0.0), (0.33, -1.0), (0.40, -1.0), (0.47, 1.0), (0.54, 1.0), (0.60, 0.0))
    fold = keys(t, (0.66, 0.0), (0.76, 1.0), (0.88, 1.0), (0.97, 0.0))
    p = Pose()
    stand(p, width=0.19, lean=78 * fold, height=0.905 + 0.03 * tall + 0.165 * fold, sway=-0.9 * bend, roll=4 * bend, toe_out=10, lift=(tall, tall))
    p.chest(pitch=1 - 3 * tall + 30 * fold, roll=22 * bend)
    p.head(pitch=-12 * tall + 28 * fold, roll=8 * bend)
    solved = p.solve()
    chest = solved.turns["UpperChest"]
    for side, sign in SIDES:
        shoulder = solved.at[("Right" if side == "R" else "Left") + "UpperArm"]
        rest = at_side(p, side)
        high = shoulder + chest @ np.array([sign * 0.03, 0.02, 0.497])
        low = shoulder + np.array([-sign * 0.04, 0.04, -0.49])
        still = 1 - over - fold
        swing = np.array([sign * 0.40, 0.06, 0.0])                                             # straight arms out through the sides, both ways
        wrist = rest * still + high * over + low * fold + swing * math.sin(math.pi * over)
        p.hand(side, wrist, fingers=unit(np.array([sign * 0.1, 0.3, -1.0]) * still + chest @ np.array([0.0, 0.0, 1.0]) * over + np.array([0.0, 0.1, -1.0]) * fold),
               palm=np.array([-sign, 0.0, 0.0]) * still + chest @ np.array([-sign * 0.8, 0.6, 0.0]) * over + np.array([0.0, -1.0, 0.0]) * fold,
               elbow=(sign * 0.9, -0.3, 0.1), curl=blend_holds("relaxed", "flat", over))
        p.shrug(side, over)
    return p


PLATE = np.array([0.0, 0.35, 0.75])                   # the middle of the plate on the table, from the middle of the seat


def eat_sit(t):
    # At a table: the fork goes to the plate, loads, comes up to the mouth as the body leans in to meet it, and
    # goes back down while the mouthful is chewed. The left forearm lies on the table beside the plate.
    bite = keys(t, (0.0, 0.0), (0.22, 0.0), (0.42, 1.0), (0.56, 1.0), (0.76, 0.0), (1.0, 0.0))
    scoop = math.sin(math.pi * keys(t, (0.02, 0.0), (0.22, 1.0)))
    chew = keys(t, (0.56, 0.0), (0.64, 1.0), (0.90, 1.0), (1.0, 0.0))
    p = Pose()
    sit(p, lean=4 + 9 * bite - 2 * chew, roll=1.0 * s(t))
    p.chest(pitch=3 + 3 * bite)
    p.head(pitch=22 * (1 - bite) * (1 - chew) + 8 * bite + 3 * chew * (1 - bite) + 1.5 * chew * s(t, 8))
    lips = mouth(p)
    low = PLATE + (0.14, -0.10, 0.105 - 0.035 * scoop)
    high = lips + (0.125, 0.080, -0.065)              # the fist stays a fork's length and a little more from the face
    p.hand("R", low + (high - low) * bite + (0.0, 0.04 * math.sin(math.pi * bite), 0.0), fingers=unit(np.array([-0.55, 0.6, -0.55]) * (1 - bite) + np.array([-0.85, -0.33, 0.41]) * bite),
           palm=np.array([-0.3, -0.6, -0.75]) * (1 - bite) + np.array([-0.1, -0.8, 0.6]) * bite, elbow=(0.6, -0.2, -0.8), curl="pen")
    p.hand("L", (-0.20, 0.30 + 0.01 * s(t), TABLE + 0.045), fingers=(0.45, 0.9, 0.0), palm=(0, 0, -1), elbow=(-0.7, -0.2, -0.7), curl="relaxed")
    return p


MUG = Grip("R", at=(-0.040, 0.0, 0.050), thumb=(0, 0, 1), back=(-1, 0, 0), hold="cup")     # round the body, on the side away from the handle
MUG_YAW = 165.0                                       # so the handle (the mug's +X) points to the drinker's left and a little forward
MUG_LOW = np.array([0.21, 0.28, 1.04])                # a mug carried in front of the right hip
AT_MOUTH = 52.0                                       # degrees a cup is turned to the left on its way up: the hand goes round to its far side, the wrist out to the right, the elbow down


def drink_stand(t):
    # A mug carried in the right hand comes up to the mouth, turning in the hand's reach so that the wrist stays
    # out to the right of it and the elbow low; it is tipped as the head goes back, and lowered again.
    up = keys(t, (0.0, 0.0), (0.30, 1.0), (0.64, 1.0), (1.0, 0.0))
    tip = keys(t, (0.26, 0.0), (0.42, 1.0), (0.58, 1.0), (0.70, 0.0))
    p = Pose()
    stand(p, width=0.15, sway=0.25 * up, lean=-2 * tip)
    p.chest(pitch=1 - 3 * tip)
    p.head(pitch=4 - 4 * up - 14 * tip)
    turned, at = rim_at(mouth(p), 0.10, 0.04, tilt=(8 + 28 * tip) * up, yaw=MUG_YAW - AT_MOUTH * up)
    p.tool(turned, MUG_LOW + (at - MUG_LOW) * up + (0.0, 0.08 * math.sin(math.pi * up), 0.0), MUG, elbows={"R": (0.6, -0.1, -0.8)})
    hang(p, "L", 0.25 * up)
    return p


CLIPS = []

CLIPS += [
    Clip("cook_stir", 3.0, cook_stir, tool="pan", grips=(PAN,), tool_length=0.3, watch="L",
         note="at a hob 0.45 m ahead and 0.92 m up: the left hand on the pan's handle (which points back and left), the right stirring with a spoon that is not modelled"),
    Clip("pushup", 2.0, pushup, note="press-ups along the character's forward axis, centred on the origin: toes 0.79 m behind it, hands 0.50 m ahead and 0.50 m apart, the head 0.9 m ahead"),
    Clip("situp", 2.4, situp, note="sit-ups, knees bent, fingertips at the temples: the hips are 0.08 m ahead of the origin, the feet 0.60 m ahead, the head 0.70 m behind when lying"),
    Clip("stretch", 10.0, stretch, note="arms overhead on the toes, a bend to each side, a forward fold; needs 0.8 m clear either side"),
    Clip("eat_sit", 4.0, eat_sit, dressing=(("plate", turn(), tuple(PLATE)),),
         note="seated (seat 0.45 m up, its middle at the origin) at a table 0.75 m high whose near edge is 0.20 m ahead: fork to the mouth from a plate 0.35 m ahead; the fork is not modelled"),
    Clip("drink_stand", 3.0, drink_stand, loop=False, tool="mug", grips=(MUG,), tool_length=0.10,
         note="a mug carried in front of the right hip (0.28 m ahead, 1.04 m up) is lifted to the mouth, tipped and lowered"),
]


# ---------------------------------------------------------------------------------------------- with friends: greetings

# Two people who greet each other stand FACING, 0.80 m apart (the code accepts 0.65 to 1.0), and both play the
# same clip. A point (x, y, z) of one is (-x, 0.80 - y, z) of the other, so what both do must fit its own mirror
# image: right hands meet on the centre line; in a hug each one's LEFT arm goes high and RIGHT arm low and each
# head goes to its own left.
APART = 0.80
SHAKE = np.array([-0.011, APART / 2, 1.05])           # the middle of the right palm when the hands are clasped: both palms' skins meet on the centre line


def social_handshake(t):
    # The right hand goes out to the other's, closes on it (0.5 s), two good shakes with a lean in and a nod, lets
    # go (1.3 s) and comes back. The lean is in the chest: the code plays this on the upper body only.
    out = keys(t, (0.0, 0.0), (0.27, 1.0), (0.73, 1.0), (1.0, 0.0))
    body = keys(t, (0.0, 0.0), (0.24, 1.0), (0.76, 1.0), (1.0, 0.0))
    held = keys(t, (0.22, 0.0), (0.28, 1.0), (0.70, 1.0), (0.76, 0.0))
    k = (t - 0.30) / 0.40
    pump = math.sin(TAU * 2 * k) * math.sin(math.pi * k) ** 0.5 if 0.0 < k < 1.0 else 0.0
    p = Pose()
    stand(p, width=0.15)
    p.chest(pitch=1 + 6 * body, yaw=-7 * body)
    blend_hand(p, "R", out, SHAKE + (0.0, 0.0, 0.055 * pump), fingers=(-0.05, 1.0, -0.12 + 0.35 * pump), palm=(-1.0, 0.0, 0.05), elbow=(0.6, -0.6, -0.5),
               curl=blend_holds((0.1, 0.05, 0.06, 0.08, 0.10, 0.1, 0.2), "cup", held), arc=(0.04, 0.0, 0.03))
    hang(p, "L")
    p.head(pitch=1 - 3 * body + 11 * math.sin(math.pi * keys(t, (0.30, 0.0), (0.60, 1.0))), yaw=-2 * body)
    return p


def hug(whole, pat=True, step="L"):
    """A hug. `whole`: the whole body, with half a step in (the root stays where it is) so that two people 0.80 m
    apart finish 0.35 m apart, arms right round each other's backs. Otherwise the upper body alone, for people
    who stay where they are: the chest leans in and both arms, elbows low and well bent, close round a body half
    a metre ahead (two copies 0.80 m apart reach each other's chests and shoulders, no further).
    Either way the LEFT arm is the high one and the head goes to its own left, so that two copies interlock."""
    def pose(t):
        close = keys(t, (0.0, 0.0), (0.30, 1.0), (0.72, 1.0), (1.0, 0.0))
        arms = keys(t, (0.05, 0.0), (0.34, 1.0), (0.70, 1.0), (0.95, 0.0))
        k = (t - 0.38) / 0.30
        pats = 0.5 - 0.5 * math.cos(TAU * 2 * k) if pat and 0.0 < k < 1.0 else 0.0      # the hand off the back and on again, twice
        squeeze = math.sin(math.pi * keys(t, (0.30, 0.0), (0.72, 1.0)))
        p = Pose()
        if whole:
            stride = math.sin(math.pi * keys(t, (0.02, 0.0), (0.26, 1.0))) + math.sin(math.pi * keys(t, (0.74, 0.0), (0.98, 1.0)))
            ahead, lifted = (0.0, 0.30 * close), (stride, 0.6 * close)
            stand(p, width=0.15, lean=5 * close, height=0.905 - 0.035 * close, shift=(-0.035 * close, 0.251 * close), sway=(-0.4 if step == "L" else 0.4) * close,
                  left=ahead if step == "L" else (0.0, 0.0), right=ahead if step == "R" else (0.0, 0.0), lift=lifted if step == "L" else lifted[::-1])
            p.chest(pitch=2 + 5 * close, roll=-5 * close)
            p.head(yaw=-14 * close, roll=-10 * close, pitch=2 + 2 * close)
            # Over the other's right shoulder to the middle of their upper back; under their left arm to the middle of their back.
            high, high_fingers, high_palm = np.array([0.02, 0.72, 1.42]) + (0.0, 0.05 * pats, 0.0), (0.8, 0.35, -0.45), (0.25, -0.95, -0.1)
            low, low_fingers, low_palm = np.array([0.0, 0.715, 1.18]), (-1.0, 0.12, 0.0), (0.1, -1.0, 0.0)
            elbows = ((-0.8, -0.2, -0.6), (0.8, -0.3, -0.5))
        else:
            stand(p, width=0.15)
            p.chest(pitch=1 + (11 + 1.5 * squeeze) * close, roll=-4 * close)
            p.head(yaw=-16 * close, roll=-13 * close, pitch=1 - 4 * close)
            high, high_fingers, high_palm = np.array([-0.02, 0.46 + 0.055 * pats, 1.40]), (0.9, 0.3, -0.2), (0.1, -1.0, 0.0)
            low, low_fingers, low_palm = np.array([-0.02 - 0.02 * squeeze, 0.45, 1.15]), (-0.9, 0.3, 0.1), (0.0, -1.0, 0.0)
            elbows = ((-0.6, -0.2, -0.8), (0.6, -0.3, -0.75))
        blend_hand(p, "L", arms, high, high_fingers, high_palm, elbow=elbows[0], curl=(0.15, 0.2, 0.24, 0.28, 0.32, 0.2, 0.1), arc=(-0.15, 0.0, 0.06))
        blend_hand(p, "R", arms, low, low_fingers, low_palm, elbow=elbows[1], curl=(0.15, 0.2, 0.24, 0.28, 0.32, 0.2, 0.1), arc=(0.17, 0.0, 0.0))
        p.shrug("L", 0.5 * arms)
        return p
    return pose


def social_cheek_kiss(t):
    # Half a step in with the right foot and a lean from the hips and the chest, the right hand round the other's
    # upper arm, the head turned to offer the right cheek beside theirs (each head goes to its own left), and back.
    close = keys(t, (0.0, 0.0), (0.36, 1.0), (0.56, 1.0), (1.0, 0.0))
    arm = keys(t, (0.06, 0.0), (0.38, 1.0), (0.58, 1.0), (0.96, 0.0))
    stride = math.sin(math.pi * keys(t, (0.02, 0.0), (0.30, 1.0))) + math.sin(math.pi * keys(t, (0.62, 0.0), (0.94, 1.0)))
    p = Pose()
    stand(p, width=0.15, lean=11 * close, height=0.905 - 0.01 * close, shift=(-0.05 * close, 0.17 * close), sway=0.3 * close, right=(0.0, 0.22 * close), lift=(0.4 * close, stride))
    p.chest(pitch=2 + 10 * close, roll=-4 * close)
    p.head(yaw=-22 * close, roll=-7 * close, pitch=-9 * close)
    blend_hand(p, "R", arm, (0.29, 0.66, 1.26), fingers=(-0.35, 0.9, 0.1), palm=(-0.9, -0.35, 0.0), elbow=(0.45, -0.5, -0.8), curl="cup", arc=(0.10, 0.0, 0.0))
    hang(p, "L")
    return p


# ---------------------------------------------------------------------------------------------- with friends: on a bench

BENCH = 0.55                                          # two people on a bench sit this far apart, side by side


def sit_chat_a(t):
    # Sitting turned well round towards somebody on the LEFT and talking: the right hand comes up off the thigh
    # and beats time, the head goes with the words; then both hands rest.
    up = keys(t, (0.0, 0.0), (0.12, 1.0), (0.50, 1.0), (0.62, 0.0), (1.0, 0.0))
    beat = s(t, 5) * up
    late = s(lag(t, 0.012), 5) * up
    p = Pose()
    sit(p, lean=3 + 5 * up, yaw=-8, roll=1.2 * s(t), feet=(0.30 + 0.02 * s(t, 1, 0.2), 0.24))
    p.chest(pitch=3 + 2 * up, yaw=-23 - 3 * up, roll=1.0 * beat)
    p.head(yaw=-30 + 4 * s(t, 2), pitch=2 + 10 * max(0.0, s(t, 4)) * up, roll=-3 + 3 * s(t, 1, 0.1))
    rest, talk = np.array([0.15, 0.25, 0.635]), np.array([0.0, 0.38, 0.87 + 0.04 * beat])
    p.hand("R", rest + (talk - rest) * up, fingers=unit(np.array([-0.3, 0.95, -0.1]) * (1 - up) + np.array([-0.65, 0.65, 0.3 + 0.25 * late]) * up),
           palm=np.array([0.0, 0.0, -1.0]) * (1 - up) + np.array([-0.45, 0.2, 0.85]) * up, elbow=(0.7, -0.5, -0.5), curl=blend_holds("relaxed", "open", up))
    p.hand("L", (-0.16, 0.24, 0.635), fingers=(0.35, 0.93, -0.05), palm=(0, 0, -1), elbow=(-0.8, -0.4, -0.3), curl="relaxed")
    return p


def sit_chat_b(t):
    # Sitting turned well round towards somebody on the RIGHT and listening: deep nods, then sits back with open
    # hands (a laugh, a "well..."), a look away down the street, and forward again.
    nod = max(0.0, s(t, 6)) * keys(t, (0.0, 1.0), (0.32, 1.0), (0.40, 0.0), (0.84, 0.0), (0.92, 1.0), (1.0, 1.0))
    back = keys(t, (0.36, 0.0), (0.48, 1.0), (0.66, 1.0), (0.80, 0.0))
    away = keys(t, (0.62, 0.0), (0.70, 1.0), (0.80, 1.0), (0.90, 0.0))
    p = Pose()
    sit(p, lean=7 - 10 * back, yaw=8, roll=-1.2 * s(t, 1, 0.3), back=0.02 * back, feet=(0.26, 0.30 + 0.06 * back))
    p.chest(pitch=3 - 2 * back + 2 * nod, yaw=24)
    p.head(yaw=28 - 50 * away, pitch=1 + 13 * nod - 5 * back, roll=4 * back)
    for side, sign in SIDES:
        rest = np.array([sign * 0.05, 0.215 - 0.02 * sign, 0.655 + 0.01 * sign])      # hands together in the lap
        wide = np.array([sign * 0.20, 0.30, 0.74])
        p.hand(side, rest + (wide - rest) * back, fingers=unit(np.array([-sign * 0.75, 0.6, -0.2]) * (1 - back) + np.array([sign * 0.35, 0.9, 0.1]) * back),
               palm=np.array([-sign * 0.3, -0.5, -0.8]) * (1 - back) + np.array([-sign * 0.3, 0.2, 0.95]) * back, elbow=(sign * 0.8, -0.4, -0.4), curl=blend_holds("relaxed", "open", back))
        p.shrug(side, 0.6 * back)
    return p


# ---------------------------------------------------------------------------------------------- with friends: food and drink

WINE = Grip("R", at=(0.022, 0.0, 0.0), thumb=(0, 0, 1), back=(1, 0, 0), hold=(0.45, 0.62, 0.70, 0.78, 0.84, 0.75, 0.0))      # the stem between thumb and fingers
GLASS_LOW = np.array([0.20, 0.30, 1.05])              # the middle of the stem of a glass held in front of the right hip
CLINK = np.array([0.04, 0.515, 1.225])                # the same at the tap: tipped 12 degrees, its bowl is 0.53 m ahead, 1.30 m up and half a bowl to the right: two FACING 1.05 m apart touch bowls side by side


def cheers(t):
    # The glass comes up a hand's breadth from where it is carried, goes straight out at chest height with a lean
    # in, touches (0.8 s) and comes straight back: quick, never near the mouth; a nod, and down again.
    up = keys(t, (0.0, 0.0), (0.28, 1.0), (0.62, 1.0), (1.0, 0.0))
    out = keys(t, (0.30, 0.0), (0.444, 1.0), (0.58, 0.0))
    lean = keys(t, (0.10, 0.0), (0.44, 1.0), (0.52, 1.0), (0.80, 0.0))
    p = Pose()
    stand(p, width=0.15, lean=5 * lean, sway=0.2 * lean, shift=(0.0, 0.035 * lean), right=(0.0, 0.03 * lean))
    p.chest(pitch=1 + 3 * lean, yaw=-10 * out)
    raised = np.array([0.15, 0.31, 1.15])
    p.tool(turn(pitch=12 * out) @ turn(yaw=-10), GLASS_LOW + (raised - GLASS_LOW) * up + (CLINK - raised) * out, WINE, elbows={"R": (0.6, -0.4, -0.7)})
    hang(p, "L", 0.2 * up)
    p.head(pitch=1 - 2 * up + 8 * math.sin(math.pi * keys(t, (0.50, 0.0), (0.78, 1.0))))
    return p


TAKEAWAY = Grip("L", at=(0.0, -0.01, -0.012), thumb=(0.3, 1, 0), back=(0, 0, -1), hold="plate")
TAKEAWAY_AT = np.array([-0.13, 0.30, 1.06])           # the middle of the underside of a box on the left palm


def eat_standing(t):
    # A carton on the left palm: the right hand takes something out of it, the carton comes up under the chin as
    # the head dips to it, the bite goes in, and the hand goes back to the side, elbow in, while it is chewed.
    take = keys(t, (0.04, 0.0), (0.24, 1.0), (0.32, 1.0), (0.50, 0.0))
    bite = keys(t, (0.32, 0.0), (0.50, 1.0), (0.62, 1.0), (0.84, 0.0), (1.0, 0.0))
    has = keys(t, (0.22, 0.0), (0.28, 1.0), (0.58, 1.0), (0.64, 0.0))
    lift = keys(t, (0.30, 0.0), (0.48, 1.0), (0.64, 1.0), (0.86, 0.0))
    look = keys(t, (0.0, 0.0), (0.10, 1.0), (0.32, 1.0), (0.48, 0.25), (0.66, 0.25), (0.92, 0.0))
    p = Pose()
    stand(p, width=0.15, lean=8 * bite, sway=-0.3 * take - 0.2 * bite)
    p.chest(pitch=2 + 4 * bite)
    p.head(pitch=2 + 22 * look + 9 * bite + 1.5 * s(t, 7) * keys(t, (0.62, 0.0), (0.68, 1.0), (0.92, 1.0), (1.0, 0.0)), yaw=-8 * look * (1 - bite))
    box = TAKEAWAY_AT + (0.04 * lift, -0.02 * lift, 0.14 * lift)
    p.tool(turn(yaw=12, pitch=-6), box, TAKEAWAY, elbows={"L": (-0.6, -0.7, -0.5)})
    lips = mouth(p)
    rest = at_side(p, "R")
    still = 1 - take - bite
    wrist = rest * still + (box + (0.07, -0.02, 0.19)) * take + (lips + (0.085, 0.07, -0.07)) * bite + np.array([0.02, 0.07, 0.0]) * math.sin(math.pi * still)
    p.hand("R", wrist, fingers=unit(np.array([0.1, 0.3, -1.0]) * still + np.array([-0.45, 0.25, -0.85]) * take + np.array([-0.62, -0.55, 0.55]) * bite),
           palm=np.array([-1.0, 0.0, 0.0]) * still + np.array([-0.6, -0.6, 0.5]) * take + np.array([-0.3, -0.8, -0.5]) * bite, elbow=(0.45, -0.3, -0.85), curl=blend_holds("relaxed", "pinch", max(has, 0.6 * max(take, bite))))
    return p


CLIPS += [
    Clip("social_handshake", 1.8, social_handshake, loop=False, mask="upper", marks={"grip": 0.5, "release": 1.3},
         note="for two people FACING 0.80 m apart, both playing it: the right palms meet on the centre line 0.40 m ahead and 1.05 m up, two shakes; nothing below the chest moves"),
    Clip("social_hug", 2.6, hug(False), loop=False, mask="upper", watch="L",
         note="the upper body's hug, both playing it FACING: the chest leans in, the left hand high (1.40 m) and the right low (1.15 m) close round a body 0.45 m ahead, a pat, each head to its own left"),
    Clip("social_hug_a", 2.6, hug(True, pat=True, step="L"), loop=False, watch="L",
         note="the whole body's hug, one of a pair (with social_hug_b, or with itself): partner FACING 0.80 m ahead; half a step in with the left foot, finishing 0.35 m apart with the arms round their back (left over their shoulder, right under their arm), two pats, and back; the root stays put"),
    Clip("social_hug_b", 2.6, hug(True, pat=False, step="R"), loop=False, watch="L",
         note="the other of the pair: the same hug stepping in with the right foot and holding still while patted"),
    Clip("social_cheek_kiss", 1.6, social_cheek_kiss, loop=False,
         note="for two FACING 0.80 m apart, both playing it: half a step in with the right foot, a lean, right cheeks side by side 0.40 m ahead, the right hand on the other's arm; the root stays put"),
    Clip("sit_chat_a", 6.0, sit_chat_a, note="seated (seat 0.45 m up, its middle at the origin), turned to somebody 0.55 m to the LEFT on the same bench, talking with the right hand"),
    Clip("sit_chat_b", 6.0, sit_chat_b, note="seated the same way, turned to somebody 0.55 m to the RIGHT, listening: nods, sits back with open hands, a look away"),
    Clip("cheers", 1.8, cheers, loop=False, tool="wine_glass", grips=(WINE,), tool_length=0.2, marks={"clink": 0.8},
         note="a glass held by its stem in front of the right hip (1.05 m) goes out at chest height with a lean and straight back: the bowl is 0.53 m ahead, 1.30 m up and 0.04 m to the right at 0.8 s, so two FACING 1.05 m apart touch bowls side by side (at 0.80 m they pass without touching)"),
    Clip("eat_standing", 3.4, eat_standing, loop=False, tool="takeaway_box", grips=(TAKEAWAY,), tool_length=0.09, watch="R",
         note="a carton on the left palm 0.30 m ahead and 1.06 m up from start to end; the right hand takes a piece out and brings it to the mouth as the carton comes up under the chin"),
]


# ---------------------------------------------------------------------------------------------- with friends: out and about

def walk_talk(t):
    # Made for the upper body over the game's walk, for the one walking on the RIGHT: the head turned to the
    # companion on the left, the right hand coming up to make a point (twice a loop) and swinging between; the
    # left arm keeps the swing of walking (about one a second).
    up = keys(t, (0.0, 0.0), (0.10, 1.0), (0.40, 1.0), (0.50, 0.0), (0.60, 0.0), (0.68, 0.75), (0.84, 0.75), (0.94, 0.0), (1.0, 0.0))
    beat = s(t, 6) * up
    late = s(lag(t, 0.01), 6) * up
    swing = s(t, 5)
    p = Pose()
    stand(p, width=0.11, lean=3)
    p.chest(pitch=1, yaw=-7 + 3 * swing, roll=1.0 * s(t, 5, 0.1))
    p.head(yaw=-25 + 7 * keys(t, (0.44, 0.0), (0.52, 1.0), (0.60, 1.0), (0.68, 0.0)) - 3 * swing, pitch=1 + 4 * max(0.0, s(t, 3)) * up, roll=-2 + 2 * s(t, 2))
    hips = p._hips[0]
    rest = hips + np.array([0.228, 0.03 - 0.15 * swing, -0.05 + 0.02 * abs(swing)])
    talk = np.array([0.17, 0.31, 1.13 + 0.04 * beat])
    p.hand("R", rest + (talk - rest) * up, fingers=unit(np.array([0.06, 0.25 - 0.3 * swing, -1.0]) * (1 - up) + np.array([-0.4, 0.85, 0.3 + 0.25 * late]) * up),
           palm=np.array([-1.0, 0.1, 0.0]) * (1 - up) + np.array([-0.55, 0.1, 0.85]) * up, elbow=(0.6, -0.7, -0.3), curl=blend_holds("relaxed", "open", up))
    left = hips + np.array([-0.228, 0.03 + 0.15 * swing, -0.05 + 0.02 * abs(swing)])
    p.hand("L", left, fingers=(-0.06, 0.25 + 0.3 * swing, -1.0), palm=(1.0, 0.1, 0.0), elbow=(-0.4, -1.0, 0.0), curl="relaxed")
    return p


def point_show(t):
    # "This is my place": the right hand comes up open across the body to the left and sweeps round past straight
    # ahead and well out to the right, the chest and then the head going after it; a look back at the companion.
    out = keys(t, (0.0, 0.0), (0.24, 1.0), (0.74, 1.0), (1.0, 0.0))
    sweep = keys(t, (0.22, 0.0), (0.60, 1.0))
    late = keys(t, (0.26, 0.0), (0.66, 1.0))
    p = Pose()
    stand(p, width=0.15)
    p.chest(pitch=1 + 2 * out, yaw=(-12 + 27 * sweep) * out)
    palm = np.array([-0.12 + 0.57 * sweep, 0.40 + 0.08 * sweep, 1.17 + 0.10 * sweep])
    blend_hand(p, "R", out, palm, fingers=(-0.55 + 1.05 * sweep, 0.8, 0.12), palm=(-0.2 * (1 - sweep), -0.15, 1.0), elbow=(0.6, -0.6, -0.5), curl="open", arc=(0.0, 0.06, 0.0))
    hang(p, "L")
    p.head(yaw=(-22 + 47 * late) * out - 20 * math.sin(math.pi * keys(t, (0.66, 0.0), (0.94, 1.0))), pitch=1 - 3 * out)
    return p


PHONE_EAR = Grip("R", at=(0.0, -0.02, -0.012), thumb=(1, 0, 0), back=(0, 0, -1), hold=(0.3, 0.3, 0.34, 0.38, 0.42, 0.5, 0.1))      # the palm on the back of the phone, fingers up it towards its top


def frame(top, normal):
    """A turn for something flat (the phone): its +Y along `top`, its +Z as near `normal` as that allows."""
    y = unit(top)
    z = np.asarray(normal, dtype=float)
    z = unit(z - y * z.dot(y))
    return np.stack([np.cross(y, z), y, z], axis=1)


def phone_answer(t):
    # The right hand goes to the trouser pocket, comes out with the phone (0.45 s), a glance at the screen in
    # front of the chest, and up to the right ear; ends there (go on with the game's TalkingPhone).
    k = keys(t, (0.0, 0.0), (0.24, 0.0), (0.50, 0.45), (0.62, 0.45), (0.90, 1.0), (1.0, 1.0))      # pocket -> a look -> the ear
    dip = keys(t, (0.0, 0.0), (0.22, 1.0), (0.30, 1.0), (0.48, 0.0))
    look = math.sin(math.pi * keys(t, (0.30, 0.0), (0.82, 1.0)))
    p = Pose()
    stand(p, width=0.15, sway=0.2 * dip)
    p.chest(pitch=1 + 2 * look, yaw=2 * dip)
    p.head(pitch=1 + 18 * look, yaw=8 * look + 4 * dip, roll=7 * k)
    solved = p.solve()
    head, turned = solved.at["Head"], solved.turns["Head"]
    rest, pocket = at_side(p, "R"), p._hips[0] + np.array([0.215, 0.07, 0.03])
    hanging = basis((0.1, 0.3, -1.0), (-1.0, 0.0, 0.0)) @ basis((1, 0, 0), (0, 0, -1)).T
    ear = (frame(turned @ np.array([0.10, -0.55, 0.83]), turned @ np.array([-1.0, 0.22, 0.0])), head + turned @ np.array([0.142, 0.055, 0.02]))
    shown = (frame((-0.15, 0.55, 0.82), (-0.2, -0.75, 0.6)), np.array([0.13, 0.33, 1.22]))          # held up to be read
    wrists, hands = zip(*(PHONE_EAR.wrist(*pose) for pose in (shown, ear)))
    if k < 0.45:
        part = k / 0.45
        hand, wrist = between(hanging, hands[0], part), rest + (wrists[0] - rest) * part
    else:
        part = (k - 0.45) / 0.55
        hand, wrist = between(hands[0], hands[1], part), wrists[0] + (wrists[1] - wrists[0]) * part + np.array([0.05, 0.05, 0.0]) * math.sin(math.pi * part)
    wrist = wrist + (pocket - rest) * dip * (1 - k / 0.45 if k < 0.45 else 0.0)
    p.tool(hand @ PHONE_EAR.turn, wrist + hand @ PHONE_EAR.offset, PHONE_EAR, elbows={"R": (0.3, 0.2, -0.95)})
    hang(p, "L")
    return p


LONG_BOX = 1.20                                       # a box this long lies between the two who lift it, 0.42 m from each one's feet
BOX_END = np.array([0.0, 0.47, 0.0])                  # where the hands go under the near end (0.05 m in from its end face), on the ground


def box_lift_with(t):
    # One of two who lift a long box by its ends: a short step in with the left foot, down with the knees and a
    # straight back, the hands under the end (they have it at 0.8 s), a look at the other, and up together with
    # long arms (1.9 s); ends carrying, the box where it was over the ground.
    down = keys(t, (0.0, 0.0), (0.32, 1.0), (0.44, 1.0), (0.86, 0.0), (1.0, 0.0))
    arms = keys(t, (0.04, 0.0), (0.35, 1.0), (1.0, 1.0))
    up = 1.0 - down if t > 0.44 else 0.0
    step = math.sin(math.pi * keys(t, (0.02, 0.0), (0.20, 1.0)))
    p = Pose()
    stand(p, width=0.25, lean=50 * down + 14 * up, height=0.905 - 0.55 * down - 0.02 * up, shift=(0.0, 0.21 * down + 0.15 * up), left=(0.0, 0.14 * arms),
          lift=(max(0.9 * down, step), 0.9 * down), toe_out=20)
    p.chest(pitch=2 + 4 * down)
    solved = p.solve()
    for side, sign in SIDES:
        shoulder = solved.at[("Right" if side == "R" else "Left") + "UpperArm"]
        away = math.hypot(sign * 0.258 - shoulder[0], BOX_END[1] - 0.02 - shoulder[1])       # the wrist from the shoulder, seen from above
        height = max(BOX_END[2] + 0.045, shoulder[2] - math.sqrt(max(0.01, 0.49 ** 2 - away ** 2)) + 0.028)      # as low as a long arm takes the palm
        blend_hand(p, side, arms, (sign * 0.175, BOX_END[1], height), fingers=(-sign, 0.25, 0.0), palm=(0.0, 0.0, 1.0), elbow=(sign * 0.8, -0.5, -0.2), curl="plate", arc=(sign * 0.08, 0.10, 0.0))
    seen = keys(t, (0.34, 0.0), (0.50, 1.0))
    p.look_at((0.0, 0.47 + 1.5 * seen, 0.3 + 1.1 * seen), down=30)
    return p


def goodbye_wave_walk(t):
    # Walking away: the right arm comes up beside the head, palm to whoever is left behind, two flicks of the
    # hand, and down into the swing of walking. Made for the right arm alone, over the game's walk.
    up = keys(t, (0.0, 0.0), (0.30, 1.0), (0.72, 1.0), (1.0, 0.0))
    flick = math.sin(TAU * 2 * keys(t, (0.28, 0.0), (0.74, 1.0)))
    p = Pose()
    stand(p, width=0.11, lean=3)
    p.chest(pitch=1, yaw=5 * up)
    p.head(yaw=14 * up, pitch=-2 * up)
    blend_hand(p, "R", up, (0.36 + 0.07 * flick, -0.01, 1.66), fingers=(0.55 * flick + 0.1, -0.05, 1.0), palm=(0.0, -1.0, 0.05), elbow=(0.7, -0.2, -0.7), curl="open", arc=(0.10, 0.10, 0.0))
    p.shrug("R", 0.4 * up)
    hang(p, "L")
    return p


CLIPS += [
    Clip("walk_talk", 5.0, walk_talk, mask="upper", note="over the game's walk, for the one walking on the RIGHT: head turned 25 degrees to the left, the right hand making a point twice a loop, both arms otherwise swinging once a second"),
    Clip("walk_talk_l", 5.0, mirrored(walk_talk), mask="upper", watch="L", note="walk_talk mirrored, for the one walking on the LEFT: head to the right, the left hand talking"),
    Clip("point_show", 2.2, point_show, loop=False, mask="upper", note="an open right hand sweeps from the left (0.12 m left, 0.40 m ahead, 1.17 m up) round past straight ahead and out to the right (0.45 m right, 0.48 m ahead, 1.27 m up), chest and then head following"),
    Clip("phone_answer", 1.6, phone_answer, loop=False, mask="upper", tool="phone", grips=(PHONE_EAR,), tool_length=0.15, marks={"grip": 0.45},
         note="the right hand dips to the trouser pocket (show the phone from 0.45 s), a glance at the screen in front of the chest, then to the right ear; ENDS there: play it with ClipEnd.Hold and go on with the game's TalkingPhone"),
    Clip("box_lift_with", 2.2, box_lift_with, loop=False, marks={"grip": 0.8, "up": 1.9},
         note="one of two lifting a box 1.20 m long that lies between them: the partner stands FACING 2.04 m ahead, the box's near end is 0.42 m ahead; hands 0.35 m apart under the end (0.47 m ahead), on the ground at 0.8 s; the box (its underside 0.03 m under the wrists) is 0.85 m up at 1.9 s and has not moved over the ground; ENDS holding"),
    Clip("box_lift_with_b", 2.2, mirrored(box_lift_with), loop=False, marks={"grip": 0.8, "up": 1.9}, watch="L", note="box_lift_with mirrored, for the partner at the other end"),
    Clip("goodbye_wave_walk", 1.4, goodbye_wave_walk, loop=False, mask="right_arm", note="over the game's walk: the right hand up beside the head (0.36 m right, 1.66 m up), palm to the BACK, two flicks, and down"),
]


# ---------------------------------------------------------------------------------------------- leisure: fishing

# fishing_rod in this space: the butt at the origin, the rod along +Y for 2.10 m, the reel under it (-Z) 0.40 m up
# with its crank on the LEFT (-X). The RIGHT hand holds the handle 0.10 m from the butt in every clip (the hand
# the game hangs the rod on: Fishing attaches it to the right hand); the left holds 0.30 m further up, over the
# reel seat, or winds, or lets go. A right hand cannot get at a crank on the left of the rod: the left winds.
ROD_R = Grip("R", at=(0.0, 0.10, 0.0), thumb=(0, 1, 0), back=(1, 0, 0), hold="handle")
ROD_L = Grip("L", at=(0.0, 0.40, 0.0), thumb=(0, 1, 0), back=(-0.5, 0, 0.85), hold="handle")
ROD_HAND = np.array([0.20, 0.10, 0.98])               # the right hand's grip when waiting: beside the right hip
ROD_LIFT = 29.0                                       # degrees the rod points above level when waiting
ROD_YAW = -22.0                                       # and to the left of straight ahead: across the body
CRANK = np.array([-0.06, 0.40, -0.06])                # the crank's axle; the knob goes round it 0.04 m out
WAIT_LIFT = ROD_LIFT + 1.5 * math.sin(-0.2 * math.pi)  # fish_wait's first pose, where the one-offs start and end
WAIT_LOOK = np.array([-0.8, 6.0, 0.2])                # the float


def rod_pose(hand, lift, yaw):
    """The rod with the right hand's grip at `hand`, pointing `lift` degrees up and `yaw` degrees to the right."""
    turned = turn(yaw=yaw, pitch=-lift)
    return turned, np.asarray(hand, dtype=float) - turned @ np.array([0.0, 0.10, 0.0])


def loose(side, wrist, fingers, palm):
    """A free hand as (wrist, turn), to blend with a grip's."""
    sign = 1.0 if side == "R" else -1.0
    return np.asarray(wrist, dtype=float), basis(fingers, palm) @ basis((sign, 0, 0), (0, 0, -1)).T


def hand_between(p, side, a, b, k, elbow=None, curl=("handle", "relaxed")):
    p.hand(side, a[0] + (b[0] - a[0]) * k, turned=between(a[1], b[1], k), elbow=elbow, curl=blend_holds(curl[0], curl[1], k))


def fish_wait(t):
    # Waiting for a bite: the rod held out low across the body in both hands, the weight going from foot to foot,
    # the tip lifted once a loop to take up the slack, the eyes on the float.
    twitch = math.sin(math.pi * keys(t, (0.55, 0.0), (0.66, 1.0)))
    p = Pose()
    sway = idle(p, t, width=0.17, amount=0.6, lean=1, left=(0.0, 0.05))
    p.chest(pitch=2, yaw=6 + 1.5 * sway, roll=1.2 * sway)
    pose = rod_pose(ROD_HAND + (0.012 * sway, 0.0, 0.008 * s(t, 2) + 0.03 * twitch), ROD_LIFT + 1.5 * s(t, 2, 0.1) + 8 * twitch, ROD_YAW + 3 * s(t))
    p.tool(*pose, ROD_R, ROD_L, elbows={"R": (0.4, -0.9, -0.2), "L": (-0.7, -0.5, -0.4)})
    p.look_at((-0.8 + 0.5 * s(t), 6.0, 0.2), down=20)
    return p


def cast_rod(t):
    # From the waiting hold: the left hand lets go, the right takes the rod up and back over the shoulder as the
    # body turns and the weight goes on to the back (right) foot; the whip forward on to the front foot with the
    # arm thrown out 30 degrees above level (the LINE goes at 0.9 s: the rod never leaves the fist), the
    # follow-through down, and the left hand takes the rod again in exactly fish_wait's first pose.
    lift = keys(t, (0.0, WAIT_LIFT), (0.36, 114.0), (0.44, 114.0), (0.5625, 44.0), (0.72, 14.0), (1.0, WAIT_LIFT))
    yaw = keys(t, (0.0, ROD_YAW), (0.36, 4.0), (0.5625, -6.0), (0.72, -10.0), (1.0, ROD_YAW))
    hand = keys(t, (0.0, ROD_HAND), (0.36, (0.37, -0.12, 1.50)), (0.44, (0.37, -0.13, 1.51)), (0.5625, (0.22, 0.49, 1.58)), (0.72, (0.21, 0.47, 1.24)), (1.0, ROD_HAND))
    back = keys(t, (0.0, 0.0), (0.36, 1.0), (0.44, 1.0), (0.58, -1.0), (0.74, -0.8), (1.0, 0.0))
    off = keys(t, (0.0, 0.0), (0.14, 1.0), (0.78, 1.0), (1.0, 0.0))
    busy = keys(t, (0.0, 0.0), (0.14, 1.0), (0.84, 1.0), (1.0, 0.0))
    ahead = max(0.0, -back)
    p = Pose()
    stand(p, width=0.17, lean=1 - 4 * back, sway=0.7 * back, shift=(0.0, -0.05 * back + 0.02 * ahead), left=(0.0, 0.05), yaw=8 * max(0.0, back), lift=(0.0, 0.7 * ahead))
    p.chest(pitch=2 - 2 * back, yaw=6 + 24 * back, roll=2 * back)
    pose = rod_pose(hand, lift, yaw)
    p.tool(*pose, ROD_R, elbows={"R": np.array([0.4, -0.9, -0.2]) * (1 - busy) + np.array([0.6, -0.5, -0.6]) * busy})
    p.shrug("R", 0.6 * max(0.0, back))
    hand_between(p, "L", ROD_L.wrist(*pose), loose("L", (-0.27, 0.20 - 0.10 * back, 1.02 + 0.08 * ahead), (-0.2, 0.8, -0.5), (0.9, 0.2, -0.4)), off, elbow=(-0.7, -0.5, -0.4))
    p.look_at(WAIT_LOOK + np.array([0.5, 0.0, 0.6 + 1.2 * max(0.0, back)]) * busy, down=20, up=20)
    return p


def reel_in(t):
    # A fish on: leaning well back against it on a braced front leg, the rod up at 48 degrees with its butt held
    # in to the right hip, the left hand winding the reel's crank (two turns a loop) with the whole forearm, and
    # the rod pumped once a loop.
    pump = s(t)
    wind = TAU * 2 * t
    p = Pose()
    stand(p, width=0.18, lean=-9 - 2 * pump, sway=0.35 + 0.15 * pump, shift=(0.0, -0.10 - 0.015 * pump), left=(-0.02, 0.24), right=(0.02, -0.04), height=0.885)
    p.chest(pitch=-1, yaw=7, roll=1.0 * pump)
    turned, at = rod_pose((0.19, 0.07 - 0.012 * pump, 1.00 + 0.01 * pump), 48 + 4 * pump, -15.0)
    p.tool(turned, at, ROD_R, elbows={"R": (0.4, -0.9, -0.2)})
    knob = at + turned @ (CRANK + (0.0, 0.04 * math.cos(wind), 0.04 * math.sin(wind)))
    p.hand("L", knob + turned @ np.array([-0.105, -0.035, 0.03]), fingers=turned @ np.array([0.9, 0.3, -0.25]), palm=turned @ np.array([0.2, 0.35, -0.9]), elbow=(-0.7, -0.5, -0.5), curl="pinch")
    p.look_at((-0.6, 5.0, 0.2), down=20)
    return p


def fish_land(t):
    # The rod swung up high, out in front and to the right so that it clears the face, a step back with the right
    # foot, the left hand off the rod and out to the line as the catch swings in, the head down to it; then the
    # foot comes back and the rod down to exactly fish_wait's first pose.
    high = keys(t, (0.0, 0.0), (0.28, 1.0), (0.72, 1.0), (1.0, 0.0))
    back = keys(t, (0.08, 0.0), (0.34, 1.0), (0.74, 1.0), (0.98, 0.0))
    stride = math.sin(math.pi * keys(t, (0.08, 0.0), (0.32, 1.0))) + math.sin(math.pi * keys(t, (0.76, 0.0), (0.98, 1.0)))
    off = keys(t, (0.06, 0.0), (0.22, 1.0), (0.76, 1.0), (0.94, 0.0))
    p = Pose()
    stand(p, width=0.17, lean=1 - 5 * high, sway=0.4 * back, shift=(0.0, -0.11 * back), left=(0.0, 0.05), right=(0.02 * back, -0.27 * back), lift=(0.0, stride))
    p.chest(pitch=2 - 2 * high, yaw=6 + 6 * high)
    pose = rod_pose(ROD_HAND + np.array([0.13, 0.15, 0.24]) * high, WAIT_LIFT + (76 - WAIT_LIFT) * high - 5 * off, ROD_YAW + 16 * high)
    p.tool(*pose, ROD_R, elbows={"R": np.array([0.4, -0.9, -0.2]) * (1 - high) + np.array([0.8, -0.5, -0.3]) * high})
    hand_between(p, "L", ROD_L.wrist(*pose), loose("L", (-0.13, 0.36, 1.16 + 0.03 * s(t, 3)), (0.25, 0.8, 0.55), (0.65, -0.55, 0.5)), off, elbow=(-0.7, -0.5, -0.4), curl=("handle", "pinch"))
    seen = keys(t, (0.24, 0.0), (0.44, 1.0), (0.80, 1.0), (0.96, 0.0))
    p.look_at(WAIT_LOOK * (1 - seen) + np.array([-0.08, 0.55, 0.85]) * seen, down=20 + 14 * seen)
    return p


# ---------------------------------------------------------------------------------------------- leisure: a bar, a shop, a board

BAR = 1.05                                            # the top of the bar
BAR_GLASS = np.array([0.06, 0.35, BAR])               # where the glass stands on it (the middle of its foot)


def drink_bar(t):
    # Standing at a bar with the left hand on it: the right goes to the glass (closes on it at 0.5 s), lifts it,
    # a long drink, sets it down where it stood (lets go at 3.0 s) and comes away.
    go = keys(t, (0.0, 0.0), (0.135, 1.0), (0.84, 1.0), (1.0, 0.0))
    up = keys(t, (0.16, 0.0), (0.38, 1.0), (0.60, 1.0), (0.80, 0.0))
    tip = keys(t, (0.36, 0.0), (0.46, 1.0), (0.56, 1.0), (0.64, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=4 * go - 3 * up - 2 * tip, sway=-0.25 * go, shift=(0.0, 0.02 * go), left=(0.0, 0.04))
    p.chest(pitch=2 - 3 * tip, yaw=-3 * go * (1 - up))
    p.head(pitch=14 * go * (1 - up) - 15 * tip)
    turned, at = rim_at(mouth(p), 0.12, 0.0375, tilt=(8 + 30 * tip) * up, yaw=-10 - AT_MOUTH * up)
    glass = (turned, BAR_GLASS + (at - BAR_GLASS) * up + np.array([0.02, 0.07, 0.03]) * math.sin(math.pi * up))
    if 0.139 <= t <= 0.833:
        p.tool(*glass, CUP, elbows={"R": (0.6, -0.1, -0.8)})
    else:
        p.tool_free(turn(yaw=-10), BAR_GLASS)
        wrist, hand = CUP.wrist(turn(yaw=-10), BAR_GLASS)
        hanging = loose("R", at_side(p, "R"), (0.1, 0.3, -1.0), (-1.0, 0.0, 0.0))
        p.hand("R", hanging[0] + (wrist - hanging[0]) * go + np.array([0.05, -0.05, 0.05]) * math.sin(math.pi * go), turned=between(hanging[1], hand, go), elbow=(0.6, -0.6, -0.5),
               curl=blend_holds("relaxed", (0.15, 0.25, 0.28, 0.30, 0.34, 0.5, 0.3), go))
    p.hand("L", (-0.21, 0.29, BAR + 0.03), fingers=(0.4, 0.9, 0.0), palm=(0, 0, -1), elbow=(-0.8, -0.4, -0.4), curl="relaxed")
    return p


RAIL = np.array([0.0, 0.50, 1.55])                    # the middle of the rack's rail in front


def browse_rack(t):
    # Going through a rail of clothes: the right hand, hooked over the hangers from above well out to the right
    # with the elbow hanging under it, draws a handful a quarter of a metre along the rail to the left as the
    # shoulders turn with it, lifts off and goes back for the next (twice a loop); the left, lower, holds a garment
    # out by its side and gives with each draw. The head tips to look past the working arm.
    u = (t * 2) % 1.0
    push = keys(u, (0.0, 0.0), (0.56, 1.0), (0.66, 1.0), (1.0, 0.0))
    over = math.sin(math.pi * keys(u, (0.66, 0.0), (1.0, 1.0)))
    p = Pose()
    stand(p, width=0.15)
    p.chest(pitch=2, yaw=5 - 21 * push, roll=3 - 5 * push)
    hook = np.array([0.50 - 0.23 * push, RAIL[1] - 0.135 - 0.03 * over, RAIL[2] - 0.05 + 0.05 * over])
    p.hand("R", hook, fingers=(-0.15, 0.82, 0.55), palm=(-0.25, 0.45, -0.85), elbow=(0.2, -0.3, -0.95), curl=(0.3, 0.55, 0.6, 0.65, 0.7, 0.5, 0.0))
    p.hand("L", (-0.21 - 0.05 * push, RAIL[1] - 0.20 + 0.05 * push, RAIL[2] - 0.35 + 0.02 * push), fingers=(0.25, 0.9, 0.3), palm=(0.85, -0.35, -0.4), elbow=(-0.5, -0.5, -0.7), curl="pinch")
    garment = keys(u, (0.0, 0.0), (0.30, 0.0), (0.50, 1.0), (0.80, 1.0), (1.0, 0.0))
    p.look_at(np.array([-0.16, RAIL[1], RAIL[2] - 0.35]) * (1 - garment) + (hook + (-0.12, 0.10, -0.25)) * garment, down=24, up=20)
    p.head(roll=-7 + 5 * garment)
    return p


def try_on_turn(t):
    # In front of a mirror in something new, feet under the hips and the head well down: round to the left with
    # both arms a little out, looking down that side; round to the right with the left arm out and the right hand
    # up at the lapel; and back to the front. The hips lead and the shoulders follow; the feet step round one
    # after the other; the character itself does not turn.
    def way(at):
        return float(keys(at, (0.04, 0.0), (0.24, -1.0), (0.40, -1.0), (0.64, 1.0), (0.80, 1.0), (0.98, 0.0)))

    out = keys(t, (0.0, 0.0), (0.14, 1.0), (0.86, 1.0), (1.0, 0.0))
    lapel = keys(t, (0.44, 0.0), (0.62, 1.0), (0.80, 1.0), (0.94, 0.0))
    moves = ((0.04, 0.24, 0.0, -1.0), (0.40, 0.64, -1.0, 1.0), (0.80, 0.98, 1.0, 0.0))

    def foot(first):
        """How far round one foot is (-1..1) and how high it is lifted: the leading foot goes in the first 60 % of a move."""
        value, lifted = 0.0, 0.0
        for start, end, a, b in moves:
            lead = first if b < a else not first               # the left foot leads a turn to the left
            begin = start if lead else start + 0.4 * (end - start)
            k = (t - begin) / (0.6 * (end - start))
            if t >= start:
                value = a + (b - a) * ease(k)
            if 0.0 < k < 1.0:
                lifted = math.sin(math.pi * k)
        return value, lifted

    (left, left_up), (right, right_up) = foot(True), foot(False)
    half = 0.11
    spots = [turn(yaw=36 * a) @ np.array([sign * half, 0.0, 0.0]) - np.array([sign * half, 0.0, 0.0]) for a, sign in ((left, -1.0), (right, 1.0))]
    now, late = way(t), way(t - 0.07 * out)                    # the shoulders a moment behind the hips, and with them again at the end
    p = Pose()
    stand(p, width=half, yaw=44 * now, sway=0.3 * (right_up - left_up), left=tuple(spots[0][:2]), right=tuple(spots[1][:2]), lift=(0.7 * left_up, 0.7 * right_up),
          turn_feet=(36 * left, 36 * right))
    p.chest(pitch=3 * out, yaw=8 * late - 44 * (now - late))
    p.head(pitch=29 * out, yaw=(-20 * late + 6 * lapel) * out, roll=-5 * late * out)
    hips, pelvis = p._hips
    solved = p.solve()
    chest = solved.turns["UpperChest"]
    for side, sign in SIDES:
        rest = at_side(p, side)
        wide = hips + pelvis @ np.array([sign * (0.33 + 0.06 * sign * -late), 0.07, 0.0])
        wrist = rest + (wide - rest) * out
        fingers = unit(np.array([sign * 0.1, 0.3, -1.0]) * (1 - out) + pelvis @ np.array([sign * 0.55, 0.25, -0.8]) * out)
        palm = np.array([-sign, 0.0, 0.0]) * (1 - out) + pelvis @ np.array([-sign * 0.5, 0.75, -0.4]) * out
        elbow = pelvis @ np.array([sign * 0.8, -0.6, 0.0])
        if side == "R":                                          # up to the lapel for the turn to the right
            at_lapel = solved.at["UpperChest"] + chest @ np.array([0.075, 0.165, 0.10])
            wrist = wrist + (at_lapel - wrist) * lapel + chest @ np.array([0.06, 0.08, 0.0]) * math.sin(math.pi * lapel)
            fingers = unit(fingers * (1 - lapel) + chest @ np.array([-0.65, 0.15, 0.75]) * lapel)
            palm = palm * (1 - lapel) + chest @ np.array([-0.3, -0.9, -0.3]) * lapel
            elbow = elbow * (1 - lapel) + chest @ np.array([0.6, -0.2, -0.8]) * lapel
        p.hand(side, wrist, fingers=fingers, palm=palm, elbow=elbow, curl=blend_holds("relaxed", "pinch", lapel if side == "R" else 0.0))
    return p


def chess_move_player(t):
    # Seated as chess_sit begins (chin on the left fist), the feet either side of the table's base: the chin comes
    # off the hand, the right hand goes out to the board 0.45 m ahead, lifts a piece between thumb and two fingers
    # and sets it down two squares on, and the player settles back to think.
    think = keys(t, (0.0, 1.0), (0.08, 1.0), (0.26, 0.0), (0.72, 0.0), (0.94, 1.0), (1.0, 1.0))
    move = keys(t, (0.08, 0.0), (0.34, 1.0), (0.58, 1.0), (0.82, 0.0))
    carry = keys(t, (0.38, 0.0), (0.56, 1.0))
    p = Pose()
    p.hips((0.0, -0.02, SEAT + 0.085), pitch=16 * think + 22 * move)
    p.foot("L", (-0.30, 0.30, 0.0), yaw=-16, knee=(-0.5, 1.0, 0.6))
    p.foot("R", (0.30, 0.29, 0.0), yaw=16, knee=(0.5, 1.0, 0.6))
    p.chest(pitch=6 * think + 3 * move, yaw=-4 * move)
    chin = np.array([-0.07, 0.25, TABLE + 0.39])
    lying = np.array([-0.20, 0.36, TABLE + 0.035])
    p.hand("L", lying + (chin - lying) * think, fingers=unit(np.array([0.5, 0.85, 0.0]) * (1 - think) + np.array([0.55, -0.1, 0.8]) * think), palm=np.array([0, 0, -1]) * (1 - think) + np.array([0.6, -0.7, -0.3]) * think,
           elbow=(-0.6, 0.2, -0.8), curl=blend_holds("relaxed", "fist", 0.7 * think))
    resting = np.array([0.20, 0.36, TABLE + 0.035])
    piece = BOARD + (0.07, -0.16 + 0.10 * carry, 0.10 + 0.05 * math.sin(math.pi * carry))
    p.hand("R", resting + (piece - resting) * move + (0, 0, 0.05 * math.sin(math.pi * move)), fingers=unit(np.array([-0.4, 0.9, 0.0]) * (1 - move) + np.array([-0.3, 0.75, -0.6]) * move),
           palm=np.array([0, 0, -1]) * (1 - move) + np.array([-0.2, -0.35, -0.9]) * move, elbow=(0.7, -0.3, -0.6), curl=blend_holds("relaxed", (0.5, 0.55, 0.6, 0.92, 0.96, 0.85, 0.0), move))
    p.look_at(BOARD + (0.03 * move, 0.0, 0.0), down=30)
    return p


def throw_ball(t):
    # A free throw without the ball: it comes up from the chest to in front of and above the forehead as the
    # knees give, the legs and the right arm go straight together up on to the toes (it leaves the fingers at
    # 0.8 s), the wrist snaps over and is held there a moment, and down.
    load = math.sin(math.pi * keys(t, (0.06, 0.0), (0.30, 0.5), (0.52, 1.0)))
    up = keys(t, (0.0, 0.0), (0.14, 0.35), (0.36, 0.62), (0.56, 1.0), (0.80, 1.0), (1.0, 0.0))
    snap = keys(t, (0.52, 0.0), (0.60, 1.0), (0.84, 1.0), (0.96, 0.0))
    toes = math.sin(math.pi * keys(t, (0.44, 0.0), (0.58, 0.5), (0.78, 0.5), (0.92, 1.0)))
    p = Pose()
    stand(p, width=0.17, lean=7 * load - 2 * toes, height=0.905 - 0.10 * load + 0.045 * toes, right=(0.0, 0.07), lift=(1.3 * toes, 1.3 * toes))
    p.chest(pitch=1 - 3 * snap)
    set_point, fore, high = np.array([0.13, 0.24, 1.24]), np.array([0.15, 0.31, 1.63]), np.array([0.17, 0.24 + 0.05 * snap, 1.80 + 0.06 * toes])
    rest = at_side(p, "R")
    k = min(1.0, up / 0.62)
    over = max(0.0, (up - 0.62) / 0.38)
    wrist = rest + (set_point - rest) * k + (fore - set_point) * ease(max(0.0, (up - 0.35) / 0.27)) * (1 - over) + (high - set_point) * over
    if t > 0.80:                                      # down: straight to the side, not back the way it came
        k = over = keys(t, (0.80, 1.0), (1.0, 0.0))
        wrist = rest + (high - rest) * k + np.array([0.06, 0.20, 0.0]) * math.sin(math.pi * k)
    p.hand("R", wrist, fingers=unit(np.array([0.1, 0.3, -1.0]) * (1 - k) + np.array([-0.2, -0.35, 0.9]) * k * (1 - snap) + np.array([0.0, 0.9, -0.45]) * snap),
           palm=np.array([-1.0, 0.0, 0.0]) * (1 - k) + np.array([-0.1, 0.9, 0.4]) * k * (1 - snap) + np.array([0.0, -0.45, -0.9]) * snap, elbow=(0.35, 0.6, -0.7), curl=blend_holds("relaxed", "open", k))
    p.shrug("R", 0.7 * over)
    guide = keys(t, (0.0, 0.0), (0.14, 0.5), (0.36, 1.0), (0.54, 1.0), (0.74, 0.0), (1.0, 0.0))
    left = at_side(p, "L")
    beside = np.array([-0.07, 0.37, 1.30 + 0.35 * ease(max(0.0, (up - 0.35) / 0.3))])
    p.hand("L", left + (beside - left) * guide, fingers=unit(np.array([-0.1, 0.3, -1.0]) * (1 - guide) + np.array([0.15, 0.2, 1.0]) * guide), palm=np.array([1.0, 0.0, 0.0]) * (1 - guide) + np.array([1.0, 0.1, 0.0]) * guide,
           elbow=(-0.7, -0.2, -0.7), curl=blend_holds("relaxed", "open", guide))
    p.look_at((0.0, 4.0, 3.0 - 1.4 * (1 - min(1.0, up * 3))), up=24)
    return p


CLIPS += [
    Clip("cast_rod", 1.6, cast_rod, loop=False, tool="fishing_rod", grips=(ROD_R,), tool_length=2.1, marks={"release": 0.9},
         note="from and back to fish_wait's hold: the left hand lets go, the right takes the rod back over the right shoulder (the tip 24 degrees past upright behind) and whips it forward; the line goes at 0.9 s. Rod in the RIGHT hand 0.10 m from the butt"),
    Clip("fish_wait", 8.0, fish_wait, tool="fishing_rod", grips=(ROD_R, ROD_L), tool_length=2.1,
         note="the rod held out 29 degrees above level and 22 degrees to the left of straight ahead: right hand beside the right hip 0.10 m up the handle, left 0.40 m up over the reel seat; the tip is lifted once a loop"),
    Clip("reel_in", 1.2, reel_in, tool="fishing_rod", grips=(ROD_R,), tool_length=2.1, watch="L",
         note="the rod up at 48 degrees, its butt held to the right hip by the RIGHT hand; the LEFT hand winds the crank (0.40 m up the rod, under it, on its left side) two turns a loop; leaning back"),
    Clip("fish_land", 2.0, fish_land, loop=False, tool="fishing_rod", grips=(ROD_R,), tool_length=2.1,
         note="from and back to fish_wait's hold: the rod swung up to 76 degrees, a step back with the right foot (it comes back; the root stays), the left hand out to the line 0.36 m ahead, a look at the catch 0.6 m ahead"),
    Clip("drink_bar", 3.6, drink_bar, loop=False, tool="drink_glass", grips=(CUP,), tool_length=0.12, marks={"grip": 0.5, "release": 3.0},
         note="at a bar 1.05 m high, the left hand on it: the glass stands 0.35 m ahead and 0.06 m to the right; in the right hand from 0.5 s to 3.0 s (put it in the hand and back on the bar at those marks), to the mouth and down"),
    Clip("browse_rack", 4.0, browse_rack, mask="upper",
         note="at a rail 1.55 m up and 0.50 m ahead: the right hand, hooked over the hangers, draws them from 0.50 m right to 0.27 m right twice a loop; the left holds a garment out 0.21 to 0.26 m to the left, 1.20 m up"),
    Clip("try_on_turn", 3.0, try_on_turn, loop=False, note="arms a little out, looking down at oneself: about fifty degrees round to the left, then to the right, and back; the feet step round, the root does not turn"),
    Clip("chess_move_player", 2.4, chess_move_player, loop=False, dressing=(("chess_table", turn(), (0.0, 0.62, 0.0)),),
         note="seated as chess_sit (stool 0.45 m, table centre 0.62 m ahead), from and back to its first pose: the right hand moves a piece 0.46 m ahead two squares on"),
    Clip("throw_ball", 1.4, throw_ball, loop=False, marks={"release": 0.8}, note="a free throw: the ball (not modelled) leaves the right hand 0.33 m ahead and 1.95 m up at 0.8 s"),
]
