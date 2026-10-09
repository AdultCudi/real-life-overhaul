"""Clips for people on duty and people getting about: the arrest (both sides of it), the officer on a precinct door,
a prisoner into the back of a car; boarding a bus, hanging on a strap, the bench of a shelter, a can of fuel, an open
bonnet, a suitcase; the dispatcher's desk, somebody stuck, the collector at a counter, the inspector. Same rules as
clips.py.

Where things are, for whoever places the character (x right, y ahead, z up, from the character's feet):
    arrest_cuffed        nothing: starts in hands_up_hold's first pose, ends in cuffed_stand's first pose (exactly)
    officer_cuff         whoever is being cuffed stands 0.90 m AHEAD, back to the officer (their wrists end 0.77 m
                         ahead, 0.94 m up; their left shoulder 0.17 m to the left, 0.86 m ahead, 1.40 m up). The officer steps in.
    cuffed_car_enter     as job_car_enter: feet 0.75 m out from the middle of the seat, facing the door
    board_step           the step's edge is 0.35 m ahead and 0.30 m up; an upright rail 0.40 m right and 0.80 m ahead, held from 1.42 m up
    strap_hang           the strap's loop is 1.82 m up, 0.16 m ahead, 0.28 m to the right
    sit_bench/_up, sit_wait   the seat is 0.46 m up, its middle 0.30 m BEHIND the feet; the feet never move
    refuel_hold          the filler is 0.40 m ahead, 0.90 m up; the can slants back from it to the left; the left foot is 0.15 m ahead
    bonnet_check         the engine bay is 0.90 m up: the left hand leans on its edge 0.40 m left and 0.44 m ahead, the work is 0.70 m ahead
    desk_plan            the desk top is 1.00 m up, the board on it 0.45 m ahead; the wall map beyond and above
    collector_lean       the counter top is 0.975 m up; the left hand lies on it 0.45 m ahead
"""
import math

import numpy as np

from clips_jobs import CAR_FLOOR, CAR_OUT, CAR_SEAT, at_side
from clips_scenes import BAG_WALK, carry_bag_walk, hands_up_hold
from motion import TAU, BALL, ANKLE, Clip, c, hang, idle, lag, s, shuffle, stand, weight  # noqa: F401
from rig import PALM, Grip, Pose, basis, between, blend_holds, ease, keys, turn, unit, wave  # noqa: F401


def span(t, a, b):
    """0 before a, 1 after b, straight between."""
    return min(1.0, max(0.0, (t - a) / (b - a)))


def ramp(t, a, b):
    """The same, eased."""
    return ease(span(t, a, b))


def bump(t, a, b, power=2.0):
    """0 at a, 1 half way, 0 at b, leaving and arriving gently; exactly 0 outside."""
    x = span(t, a, b)
    return 0.0 if x <= 0.0 or x >= 1.0 else math.sin(math.pi * x) ** power


def mix(a, b, k):
    """From a to b; exactly a at 0 and exactly b at 1."""
    return np.asarray(a, dtype=float) * (1 - k) + np.asarray(b, dtype=float) * k


def through(k, points):
    """A smooth path through points (k 0..1, the points evenly spaced along it): no stop at each as `keys` makes."""
    pts = [np.asarray(q, dtype=float) for q in points]
    n = len(pts) - 1
    if k <= 0.0:
        return pts[0]
    if k >= 1.0:
        return pts[-1]
    x = k * n
    i = min(int(x), n - 1)
    u = x - i
    p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, n)]
    return 0.5 * (2 * p1 + (p2 - p0) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (3 * p1 - p0 - 3 * p2 + p3) * u ** 3)


def hand_turn(side, fingers, palm):
    """How a hand is turned when its fingers and its palm point these ways (what Pose.hand works out for itself)."""
    rest = basis((1 if side == "R" else -1, 0, 0), (0, 0, -1))
    return basis(fingers, palm) @ rest.T


def turned_between(k, *turns):
    """Through several turns of a hand, evenly spaced over k 0..1; exactly the first at 0 and the last at 1."""
    n = len(turns) - 1
    if k <= 0.0:
        return turns[0]
    if k >= 1.0:
        return turns[-1]
    i = min(int(k * n), n - 1)
    return between(turns[i], turns[i + 1], k * n - i)


def palm_on(p, side, point, fingers, palm, elbow=None, curl=None):
    """A free hand whose PALM (not its wrist) is at a point: on a rail, a counter, a shoulder."""
    turned = hand_turn(side, fingers, palm)
    p.hand(side, np.asarray(point, dtype=float) - turned @ PALM[side], turned=turned, elbow=elbow, curl=curl)
    return p


def on_hip(p, side, off=0.0, towards=None, fingers=None, palm=None, curl="relaxed"):
    """A hand on the hip, elbow out and back; `off` 0..1 takes it away to `towards` (a wrist) for a gesture."""
    sign = 1.0 if side == "R" else -1.0
    rest = p._hips[0] + p._hips[1] @ np.array([sign * 0.205, 0.035, 0.135])
    wrist = rest if towards is None else rest + (np.asarray(towards, dtype=float) - rest) * off + (0.0, 0.07 * math.sin(math.pi * off), 0.0)
    resting, facing = np.array([sign * 0.05, 0.85, -0.5]), np.array([-sign, 0.0, -0.25])
    if fingers is not None:
        resting, facing = unit(mix(resting, fingers, off)), mix(facing, palm, off)
    p.hand(side, wrist, fingers=resting, palm=facing, elbow=(sign * 1.0, -0.45, 0.0), curl=curl)
    return p


_ENDS = {}


def _ends():
    """The two poses an arrest goes between: hands_up_hold's first and cuffed_stand's first. (clips.py is asked for
    late: it is the file that loads this one.)"""
    if not _ENDS:
        import clips
        _ENDS["up"], _ENDS["cuffed"], _ENDS["hands"] = hands_up_hold(0.0), clips.cuffed_stand(0.0), clips.cuffed
    return _ENDS["up"], _ENDS["cuffed"]


# ---------------------------------------------------------------------------------------------- the arrest

CLICK = 1.5 / 2.4                                     # the moment the cuffs close, in both clips


def arrest_cuffed(t):
    # From hands up (hands_up_hold) to handcuffed (cuffed_stand): told to, the hands come down out to the sides,
    # palms down, and go behind the back, the right a moment before the left; the cuffs close on them (1.5 s), the
    # shoulders are pulled back by it, and the head sinks. The feet come in a little, one after the other.
    up, held = _ends()
    body = ramp(t, 0.06, 0.80)
    down = {"R": ramp(t, 0.08, 0.58), "L": ramp(t, 0.15, 0.61)}                 # there just before the click
    step = {"L": span(t, 0.20, 0.34), "R": span(t, 0.50, 0.64)}
    over = 0.5 * bump(t, 0.12, 0.42) - 0.5 * bump(t, 0.42, 0.72)             # the weight goes off the foot that moves
    jolt = bump(t, CLICK - 0.035, CLICK + 0.13)                                 # the cuffs close: the shoulders are jerked back, the head drops
    p = Pose()
    p.hips(mix(up._hips[0], held._hips[0], body) + (0.046 * over, -0.010 * jolt, -0.012 * abs(over)), pitch=mix(-1.5, 3.0, body) - 2.0 * jolt, roll=-3.0 * over)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        k = step[side]
        lift = math.sin(math.pi * k) if 0.0 < k < 1.0 else 0.0
        p.foot(side, mix(up._feet[side][0], held._feet[side][0], ease(k)) + (0.0, 0.0, 0.035 * 0.4 * lift), yaw=sign * 8.0, pitch=14 * 0.4 * lift)
    p.chest(pitch=mix(-1.0, 4.0, body) - 4.0 * jolt, roll=1.5 * over)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        k = down[side]
        (wrist_a, turn_a, elbow_a), (wrist_b, turn_b, elbow_b) = up._hands[side], held._hands[side]
        wrist = through(k, (wrist_a, (sign * 0.48, 0.08, 1.25), (sign * 0.41, -0.09, 1.00), (sign * 0.25, -0.200, 0.95), wrist_b))
        lowering = hand_turn(side, (sign * 0.5, 0.8, 0.25), (0.0, 0.3, -0.95))          # palms down, on the way
        beside = hand_turn(side, (sign * 0.3, 0.1, -0.95), (-sign * 0.1, -1.0, 0.0))    # hanging, palms back
        p.hand(side, wrist, turned=turned_between(k, turn_a, lowering, beside, beside, turn_b), elbow=mix(elbow_a, elbow_b, k), curl=blend_holds(up._curl[side], held._curl[side], k))
        p.shrug(side, mix(up._shrug[side], held._shrug[side], k) + 0.4 * jolt)
    head = 0.45 * ramp(t, 0.20, 0.60) + 0.55 * ramp(t, 0.66, 0.98)
    p.head(yaw=mix(-14.0, 0.0, ramp(t, 0.0, 0.30)) + 10 * bump(t, 0.20, 0.56), pitch=mix(-2.0, 24.0, head) + 10 * jolt)
    return p


BEHIND = 0.90                                         # the officer stands this far behind whoever is cuffed
# Where Crime/CuffPose lays the handcuffs on somebody in cuffed_stand's first pose, seen from the officer: between
# the wrists and 0.02 m behind them, the rings left and right, the flat of them towards the officer.
CUFFS_ON = (np.stack([(1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)], axis=1), np.array([0.0, BEHIND - 0.152, 0.941]))
HANDCUFFS = Grip("R", at=(0.094, 0.0, 0.006), thumb=(0.0, 0.94, -0.34), back=(1, 0, 0), hold=(0.62, 0.90, 0.93, 0.95, 0.96, 0.88, 0.0))        # a fist round the outside of the right-hand ring
LEFT_WRIST = np.array([-0.105, BEHIND - 0.165, 1.005])                                    # their left forearm, a hand above the wrist
LEFT_SHOULDER = np.array([-0.17, BEHIND - 0.04, 1.400])                                   # the top of their left shoulder, from behind


def officer_cuff(t):
    # The officer's half of arrest_cuffed, a pace behind: a step in with the left foot, the handcuffs brought up
    # in the right hand while the prisoner's arms come down, the left hand takes the left forearm and holds it,
    # the cuffs go on (1.5 s) with a push, and the left hand goes up on to the shoulder while the right keeps
    # hold of the cuffs. Ends there (hold it): stepped in, a hand on the shoulder.
    inn = ramp(t, 0.03, 0.30)
    step = span(t, 0.05, 0.25)
    lift = math.sin(math.pi * step) if 0.0 < step < 1.0 else 0.0
    bend = ramp(t, 0.12, 0.48)
    rise = ramp(t, 0.66, 0.90)
    push = bump(t, CLICK - 0.05, CLICK + 0.07)
    p = Pose()
    stand(p, height=0.905 - 0.020 * inn + 0.010 * rise, width=0.15, lean=23 * bend - 9 * rise + 1.5 * push, shift=(-0.02 * inn, 0.215 * inn + 0.01 * push), sway=-0.5 * inn,
          left=(-0.02 * inn, 0.29 * ease(step)), lift=(0.9 * lift, 0.0), turn_feet=(3 * inn, 6 * inn))
    p.chest(pitch=3 * bend - 1 * rise, yaw=4 * bend - 8 * rise, roll=-1.5 * rise)
    # The right hand and the handcuffs in it: from hanging at the side, up to the ready, on to the wrists.
    to = ramp(t, 0.08, 0.60)
    wrist_on, turn_on = HANDCUFFS.wrist(*CUFFS_ON)
    hanging = hand_turn("R", (0.08, 0.22, -1.0), (-1.0, 0.1, 0.0))
    start = at_side(p, "R")
    wrist = through(to, (start, start + (0.03, 0.14, 0.08), (0.20, 0.40, 1.02), wrist_on + (0.03, -0.10, 0.05), wrist_on)) + (0.0, 0.014 * push, 0.0)
    turned = between(hanging, turn_on, ramp(t, 0.10, 0.50))
    p.hand("R", wrist, turned=turned, elbow=(0.3, -0.8, -0.9), curl=HANDCUFFS.hold)
    p._tool = (turned @ HANDCUFFS.turn, wrist + turned @ HANDCUFFS.offset)          # the tool is where the hand has it
    # The left hand: to their left forearm, and after the click up on to the shoulder.
    grab, top = ramp(t, 0.18, 0.60), ramp(t, 0.68, 0.90)
    free = hand_turn("L", (-0.08, 0.22, -1.0), (1.0, 0.1, 0.0))
    holds = hand_turn("L", (0.72, 0.50, -0.48), (0.25, 0.80, 0.55))
    rests = hand_turn("L", (0.06, 1.0, 0.02), (0.0, 0.12, -1.0))
    rest = at_side(p, "L")
    on_arm, on_top = LEFT_WRIST - holds @ PALM["L"], LEFT_SHOULDER - rests @ PALM["L"]
    low = through(grab, (rest, rest + (-0.04, 0.16, 0.08), on_arm + (-0.05, -0.12, 0.05), on_arm))
    p.hand("L", low + (on_top - low) * top + (-0.05 * math.sin(math.pi * top), -0.07 * math.sin(math.pi * top), 0.0), turned=between(between(free, holds, grab), rests, top),
           elbow=(-0.7, -0.6, -0.4), curl=blend_holds(blend_holds("relaxed", "bar", grab), "plate", top))
    p.shrug("L", 0.3 * top)
    watch = mix(mix((0.1, 0.9, 1.2), CUFFS_ON[1], ramp(t, 0.06, 0.34)), (0.0, BEHIND, 1.58), ramp(t, 0.70, 0.92))
    p.look_at(watch, down=30)
    return p


THUMBS_IN = (0.75, 0.22, 0.26, 0.30, 0.36, 0.95, 0.15)                                    # thumb hooked behind a belt, the fingers lying on it


def officer_guard_idle(t):
    # Standing easy by a door, thumbs hooked in the duty belt either side of the buckle, elbows out; the weight
    # goes from foot to foot, the head goes slowly along the street and back, a rock up on the toes.
    rock = bump(t, 0.56, 0.72)
    breath = s(t, 2)
    p = Pose()
    look = keys(t, (0.0, -1.0), (0.16, -1.0), (0.42, 1.0), (0.62, 1.0), (0.90, -1.0), (1.0, -1.0))
    sway = idle(p, t, width=0.19, amount=1.1, toe_out=13, lean=-1, height=0.905 + 0.012 * rock)
    p.chest(pitch=-1, yaw=10 * look - 2 * sway, roll=1.6 * sway)
    hips, pelvis = p._hips
    for side, sign in (("L", -1.0), ("R", 1.0)):
        wrist = hips + pelvis @ np.array([sign * 0.150, 0.176, 0.084 + 0.004 * breath])
        p.hand(side, wrist, fingers=pelvis @ np.array([-sign * 0.42, 0.16, -0.89]), palm=pelvis @ np.array([sign * 0.15, -0.97, -0.2]), elbow=pelvis @ np.array([sign * 0.9, -0.40, -0.05]),
               curl=THUMBS_IN)
        p.shrug(side, 0.10 + 0.08 * breath)
    p.head(yaw=35 * look, pitch=1 + 2 * rock)                                   # 45 degrees each way with the ten of the chest
    return p


def cuffed_car_enter(side):
    sign = 1.0 if side == "R" else -1.0               # the car's nose is to the right (+) or left of the character

    def pose(t):
        # job_car_enter's way in for somebody whose hands are behind their back: from cuffed_stand's first pose,
        # the foot nearer the nose goes in, the head and trunk duck low under the roof, the body turns and sits,
        # the other foot follows. Slower than a free person, and sits leaning forward off the hands.
        up, held = _ends()
        step = keys(t, (0.14, 0.0), (0.50, 1.0), (1.0, 1.0))
        sit = keys(t, (0.18, 0.0), (0.84, 1.0), (1.0, 1.0))
        tuck = keys(t, (0.38, 0.0), (0.80, 1.0), (1.0, 1.0))
        duck = math.sin(math.pi * keys(t, (0.12, 0.0), (0.90, 1.0)))
        p = Pose()
        seat = np.array([0.0, CAR_OUT - 0.04, CAR_SEAT + 0.085])                       # the hip joints when sitting
        hips = mix(held._hips[0], seat, sit) + (-sign * 0.03 * step * (1 - sit), 0.0, -0.06 * duck * (1 - sit))
        p.hips(hips, yaw=sign * 90 * ease(sit), pitch=3 * (1 - sit) + 24 * duck + 9 * sit, roll=-sign * 5 * duck)
        inner, outer = ("R", "L") if side == "R" else ("L", "R")
        plain = np.array([sign * 0.07, 1.0, 0.125])                                   # the way a standing knee points
        ground = held._feet[inner][0]
        inside = np.array([sign * 0.42, CAR_OUT - 0.10, CAR_FLOOR])
        lift = math.sin(math.pi * step) if 0.0 < step < 1.0 else 0.0
        p.foot(inner, ground + (inside - ground) * step + (0, -0.10 * lift, 0.07 * lift), yaw=sign * (8 + 80 * step), pitch=10 * lift, knee=unit(plain * (1 - sit) + np.array([sign * 1.0, 0.0, 0.5]) * sit))
        ground = held._feet[outer][0]
        inside = np.array([sign * 0.40, CAR_OUT + 0.10, CAR_FLOOR])
        lift = math.sin(math.pi * tuck) if 0.0 < tuck < 1.0 else 0.0
        plain = np.array([-sign * 0.07, 1.0, 0.125])
        p.foot(outer, ground + (inside - ground) * tuck + (0, 0.06 * lift, 0.14 * lift), yaw=-sign * 8 + sign * 98 * tuck, pitch=10 * lift + 10 * sit * (1 - tuck),
               knee=unit(plain * (1 - tuck) + np.array([sign * 1.0, 0.1, 0.5]) * tuck))
        p.chest(pitch=4 + 5 * duck + 5 * sit, yaw=-sign * 14 * duck)
        _ENDS["hands"](p, t, 0.0)
        p.head(pitch=24 * (1 - sit) + 4 * duck + 8 * sit, yaw=-sign * 18 * duck * (1 - sit))
        return p
    return pose


# ---------------------------------------------------------------------------------------------- the bus

STEP_AHEAD, STEP_UP = 0.35, 0.30                      # the edge of a bus's step from the feet
BUS_RAIL = np.array([0.40, 0.80, 1.42])               # where the hand takes an upright rail inside the door, at the right


def on_ball(p, side, sole, pitch, yaw):
    """A foot with its heel raised, turned about the ball so that the toes stay where they are (as Walk does)."""
    rotated = turn(pitch=pitch) @ np.array([0.0, -BALL, ANKLE])
    p.foot(side, ankle=(sole[0], sole[1] + BALL + rotated[1], sole[2] + rotated[2]), yaw=yaw, pitch=pitch, toes=-0.8 * pitch)


def board_step(t):
    # Up through a bus door: the right hand goes to the rail inside, the left foot up on to the step, the body is
    # drawn forward and up over it by the leg and the arm, the right foot pushes off and follows. Ends standing on
    # the step, leaning in under the door's head (whoever plays it takes the person off the street then).
    reach = ramp(t, 0.02, 0.56)
    lead = span(t, 0.08, 0.36)
    trail = span(t, 0.47, 0.90)
    forward, up = ramp(t, 0.18, 0.76), ramp(t, 0.36, 0.86)
    dip = bump(t, 0.10, 0.50)
    over = 0.7 * bump(t, 0.02, 0.40) - 0.8 * bump(t, 0.36, 0.96)                     # on the right foot, then on the left
    p = Pose()
    hips = np.array([0.035 * over, 0.44 * forward, 0.905 + STEP_UP * up - 0.035 * dip])
    p.hips(hips, pitch=7 * reach * (1 - 0.6 * up) + 9 * bump(t, 0.30, 0.86) + 2 * up, roll=-2.5 * over, yaw=-4 * bump(t, 0.10, 0.50) + 4 * bump(t, 0.50, 0.92))
    high = ease(min(1.0, lead / 0.6))                                                 # up first, then forward: the toe clears the edge
    along = ease(max(0.0, (lead - 0.15) / 0.85))
    p.foot("L", (-0.15 + 0.03 * along, 0.47 * along, (STEP_UP + 0.07) * high - 0.07 * ease(max(0.0, (lead - 0.6) / 0.4))), yaw=-8 + 3 * along, pitch=14 * bump(lead, 0.0, 0.6) - 8 * bump(lead, 0.6, 1.0))
    peel = ramp(t, 0.34, 0.47)                                                        # the heel comes up before the foot leaves
    high = ease(min(1.0, trail / 0.55))
    along = ease(max(0.0, (trail - 0.2) / 0.8))
    sole = np.array([0.15 - 0.02 * along, 0.45 * along, STEP_UP * high + 0.06 * bump(trail, 0.0, 1.0)])
    on_ball(p, "R", sole, 32 * peel * (1 - trail) ** 2, 8 - 2 * along)
    p.chest(pitch=2 + 3 * reach, yaw=5 * reach - 3 * up, roll=1.5 * over)
    rail = hand_turn("R", (-0.05, 1.0, 0.1), (-1.0, 0.0, 0.0))
    hanging = hand_turn("R", (0.08, 0.22, -1.0), (-1.0, 0.1, 0.0))
    rest, grip = at_side(p, "R"), BUS_RAIL + (0.0, 0.0, 0.20 * ramp(t, 0.56, 0.90)) - rail @ PALM["R"]          # it slides up the rail as they rise
    p.hand("R", rest + (grip - rest) * reach + (0.06 * math.sin(math.pi * reach), 0.0, 0.05 * math.sin(math.pi * reach)), turned=between(hanging, rail, reach), elbow=(0.45, -0.6, -0.7),
           curl=blend_holds("relaxed", "bar", ramp(t, 0.42, 0.56)))
    p.shrug("R", 0.3 * reach * (1 - up))
    hang(p, "L", swing=0.5 * bump(t, 0.30, 0.90))
    p.head(pitch=10 * bump(t, 0.04, 0.50) + 9 * up, yaw=6 * reach * (1 - up))
    return p


STRAP = np.array([0.28, 0.16, 1.82])                  # the loop of a strap overhead


STRAP_SWAY = (1.3, 0.09)                              # how far the body goes under the strap: side to side (x 0.046 m), fore and aft (m)


def strap_hang(t):
    # Standing in a moving bus with the right hand up in a strap: feet apart, the body going with the bus (side to
    # side, and forward and back as it brakes and pulls away) under a hand that hardly moves; a look out of the
    # window and back.
    side_ = 0.75 * weight(t, 1.0, 0.0, settle=0.8) + 0.25 * s(t, 3)
    late = 0.75 * weight(lag(t, 0.04), 1.0, 0.0, settle=0.8) + 0.25 * s(lag(t, 0.04), 3)
    surge = s(t, 2, 0.15)
    p = Pose()
    window = keys(t, (0.0, 0.0), (0.14, 0.0), (0.28, 1.0), (0.56, 1.0), (0.70, 0.0), (1.0, 0.0))
    # The body hangs away from the strap (its weight on that arm), and swings under it as the bus goes.
    stand(p, width=0.21, sway=STRAP_SWAY[0] * side_ - 0.5, shift=(-0.045 + 0.015 * s(t, 3), STRAP_SWAY[1] * surge), lean=1 - 4.0 * surge, roll=-3.0 - 1.5 * side_, toe_out=12, yaw=2 * s(t, 1, 0.1) - 5 * window,
          height=0.895 - 0.025 * abs(surge), left=(-0.02, 0.0))
    p.chest(pitch=1 + 3.0 * surge, yaw=3 + 2 * late - 13 * window, roll=-1.5 + 2.0 * late)
    loop = STRAP + (0.030 * late, 0.030 * s(lag(t, 0.03), 2, 0.15), 0.0)             # the strap swings a little with them
    palm_on(p, "R", loop, fingers=(-0.10 - 0.10 * late, 0.15, 1.0), palm=(0.0, 1.0, -0.1), elbow=(0.9, 0.35, -0.4), curl="bar")
    p.shrug("R", 0.65)
    hang(p, "L", late, -1.8 * s(lag(t, 0.06), 2, 0.15) - 0.8 * late)                # the free arm swings against the body, a moment behind
    p.head(yaw=-42 * window, pitch=2 + 3 * window, roll=-2 * late)
    return p


# ---------------------------------------------------------------------------------------------- the bench of a shelter

BENCH_UP, BENCH_BACK = 0.46, 0.30                     # the seat's top above the ground; its middle behind the feet
STOOD, SAT = np.array([0.0, 0.0, 0.905]), np.array([0.0, -BENCH_BACK - 0.01, BENCH_UP + 0.085])
BENCH = (("precinct_bench", turn(), (0.42, -BENCH_BACK - 0.03, 0.0)),)     # on the sheets: a bench whose seat is that high (its middle is 0.03 m ahead of its pivot)


def on_bench(p, down, fold=0.0, hands=None, roll=0.0, slide=0.0, twist=0.0, nod=0.0, look=0.0, right=(0.0, 0.0, 0.0), reach=0.0, breath=0.0):
    """Between standing (down 0) and sitting on the bench (down 1), the feet where they stood. `fold` is the lean
    forward at the hips on top of how one sits; `hands` 0 hanging .. 1 on the thighs (as `down` if not said)."""
    hands = down if hands is None else hands
    hips = mix(STOOD, SAT, down) + (slide, 0.0, 0.004 * breath)
    p.hips(hips, pitch=3.0 * down + fold, roll=roll)
    p.foot("L", (-0.15, 0.0, 0.0), yaw=-8.0)
    p.foot("R", (0.15 + right[0], right[1], right[2]), yaw=8.0 + 40 * right[0], pitch=14 * right[2] / 0.035)
    p.chest(pitch=2.0 * down + 0.15 * fold, yaw=twist, roll=-0.5 * roll)
    pelvis = p._hips[1]
    for side, sign in (("L", -1.0), ("R", 1.0)):
        hanging = hips + pelvis @ np.array([sign * 0.235, 0.045, -0.045])
        thigh = hips + np.array([sign * 0.125, 0.205 + reach, 0.088])
        free = hand_turn(side, pelvis @ np.array([sign * 0.08, 0.22, -1.0]), pelvis @ np.array([-sign, 0.1, 0.0]))
        lying = hand_turn(side, (-sign * 0.16, 1.0, -0.10), (0.0, -0.1, -1.0))
        p.hand(side, mix(hanging, thigh, hands) + (0.0, 0.10 * bump(hands, 0.0, 1.0), 0.0), turned=between(free, lying, hands),
               elbow=mix(pelvis @ np.array([sign * 0.5, -1.0, -0.1]), (sign * 0.7, -0.7, -0.2), hands), curl="relaxed")
    p.head(yaw=look, pitch=4.0 * down + nod)
    return p


def sit_bench(t):
    # Sitting down on a bench behind: a glance back at it, the hips go back and down while the trunk folds forward
    # over the feet, the hands come on to the thighs, and the trunk comes up as the seat takes the weight.
    p = Pose()
    return on_bench(p, ramp(t, 0.10, 0.80), fold=30 * bump(t, 0.04, 0.96), hands=ramp(t, 0.30, 0.92), look=-26 * bump(t, 0.0, 0.42), nod=10 * bump(t, 0.0, 0.50) - 6 * bump(t, 0.50, 1.0),
                    twist=-6 * bump(t, 0.0, 0.42))


def sit_bench_up(t):
    # Getting up: the trunk folds forward over the feet, the hands press on the thighs, the hips come up and
    # forward, and the body straightens to standing.
    p = Pose()
    return on_bench(p, 1.0 - ramp(t, 0.22, 0.90), fold=32 * bump(t, 0.0, 0.92, 1.4), hands=1.0 - ramp(t, 0.42, 0.96), nod=-8 * bump(t, 0.0, 0.5), reach=0.04 * bump(t, 0.0, 0.5))


def sit_wait(t):
    # Sitting at a bus stop, hands on the thighs: leans forward and looks down the street for the bus, sits back,
    # shifts on the seat, stretches a foot out and draws it in, a shorter look the other way.
    peer = keys(t, (0.0, 0.0), (0.16, 0.0), (0.28, 1.0), (0.48, 1.0), (0.60, 0.0), (1.0, 0.0))
    other = keys(t, (0.0, 0.0), (0.72, 0.0), (0.79, 1.0), (0.88, 1.0), (0.95, 0.0), (1.0, 0.0))
    there, lifted = shuffle(t, 0.56, 0.80, span=0.06)                              # (home again well before the loop comes round)
    p = Pose()
    return on_bench(p, 1.0, fold=13 * peer - 3 * other, roll=1.6 * s(t) - 8 * peer, slide=0.010 * s(t) - 0.045 * peer, twist=-12 * peer + 7 * other, look=-46 * peer + 30 * other,
                    nod=-5 * peer + 3 * bump(t, 0.60, 0.72), right=(0.02 * there, 0.13 * there, 0.035 * 0.5 * lifted), reach=0.05 * peer, breath=s(t, 2))


# ---------------------------------------------------------------------------------------------- at a car

FILLER = np.array([0.0, 0.40, 0.90])                  # the mouth of a car's filler from the feet
# jerry_can (Unity: 0.35 wide X, 0.45 high, 0.18 deep; here x, z, y): a carry handle along x at the top, the spout at
# the top corner on the +x end (props_everyday.py: a cap 0.06 m across at x 0.126, its top 0.45 m up).
CAN_SPOUT = np.array([0.126, 0.0, 0.45])
CAN_HANDLE = Grip("R", at=(0.0, 0.0, 0.415), thumb=(-1, 0, 0), back=(0, 0, 1), hold="handle")       # over the bar; the thumb to the can's -x end
CAN_BASE = Grip("L", at=(0.175, -0.01, 0.10), thumb=(0, 0, -1), back=(1, 0, 0), hold="plate")       # under the side that is down when it pours, by the base


def can_pouring(tip, heading=0.0):
    """The can laid over to pour with its cap on the filler: its top towards the filler and `tip` degrees below
    level (above, if negative), its +x side (the spout's) underneath, its base away to the left (heading turns that
    towards the body)."""
    a, h = math.radians(tip), math.radians(heading)
    z = np.array([math.cos(h) * math.cos(a), math.sin(h) * math.cos(a), -math.sin(a)])
    x = unit(np.array([0.0, 0.0, -1.0]) + z * z[2])
    turned = np.stack([x, np.cross(z, x), z], axis=1)
    return turned, FILLER - turned @ CAN_SPOUT


REFUEL_SHIFT, REFUEL_TIP, REFUEL_HEADING, REFUEL_YAW = -0.10, (-13.0, 0.0), 26.0, -6.0      # how far forward the hips are; the can's top above level, from and to; its base towards the body; the turn to it


def refuel_hold(t):
    # Bent at the hips over a car's filler, emptying a jerry can into it: the can lies across in front, the right
    # hand on its handle by the filler, the left under its base, which is lifted as the can empties and let down
    # again; the weight shifts, the eyes stay on the spout but for one look up the road.
    lift = wave(t * 2)                                                               # the base is heaved up to pour and let down, twice a loop
    glance = keys(t, (0.0, 0.0), (0.56, 0.0), (0.66, 1.0), (0.80, 1.0), (0.90, 0.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.19, height=0.880 + 0.012 * lift, lean=31 - 3 * lift - 3 * glance, sway=-0.45 + 0.35 * weight(t, 1.0, 0.1) + 0.2 * lift, shift=(-0.06, REFUEL_SHIFT + 0.015 * lift),
          left=(-0.02, 0.15), right=(0.02, -0.03), toe_out=11, yaw=REFUEL_YAW, turn_feet=(-10.0, 0.0))
    p.chest(pitch=4 - 2 * glance, yaw=REFUEL_YAW - 4, roll=-6.0 + 3.5 * lift)
    p.tool(*can_pouring(REFUEL_TIP[0] + (REFUEL_TIP[1] - REFUEL_TIP[0]) * lift + 1.0 * s(t, 7) * lift, heading=REFUEL_HEADING), CAN_HANDLE, CAN_BASE, elbows={"R": (0.6, -0.6, -0.5), "L": (-0.8, -0.5, -0.3)})
    p.look_at(mix(FILLER + (0.0, 0.0, 0.02), (1.2, 1.6, 1.5), glance), down=18)
    return p


BONNET_EDGE = np.array([-0.40, 0.44, 0.90])           # where a hand leans on the edge of an open engine bay (the top of the wing)
ENGINE = np.array([0.10, 0.70, 0.86])                 # what is being worked on in it


def bonnet_check(t):
    # Leaning in over an open bonnet from the hips: the left hand braced on the edge of the bay, the right down in
    # it turning something, the head following; then up, both hands wiped on each other, a look over the engine,
    # and in again.
    up = keys(t, (0.0, 0.0), (0.46, 0.0), (0.58, 1.0), (0.72, 1.0), (0.94, 0.0), (1.0, 0.0))
    arm = keys(t, (0.0, 0.0), (0.48, 0.0), (0.60, 1.0), (0.72, 1.0), (0.92, 0.0), (1.0, 0.0))
    turning = TAU * t * 4
    across = keys(t, (0.0, 0.0), (0.14, 0.0), (0.24, 1.0), (0.40, 1.0), (0.50, 0.0), (1.0, 0.0))      # then something further over
    peer = bump(t, 0.20, 0.44)                                                       # a closer look, deeper in
    reach = wave(span(t, 0.02, 0.46) * 2) * (1 - arm)                                # in for it, twist, and pull back: twice
    p = Pose()
    stand(p, height=0.90, width=0.19, lean=31 + 3 * peer + 6 * reach - 18 * up + 1.5 * s(t, 2), sway=-0.4 + 0.6 * up + 0.12 * s(t), shift=(0.0, 0.19 + 0.02 * reach - 0.12 * up), left=(0.0, 0.11),
          right=(0.0, -0.04), toe_out=11)
    p.chest(pitch=6 + 2 * reach - 3 * up, yaw=(7 - 12 * across) * (1 - up) + 3 * peer + 5 * reach, roll=-2.5 * (1 - up) + 3 * reach)
    wipe = np.array([0.0, 0.33, 1.07])                                               # where the hands meet to be wiped
    rub = s(t, 9) * arm
    work = (ENGINE + (-0.16 * across, 0.03 * peer - 0.05 + 0.115 * reach, -0.03 * peer + 0.05 * across + 0.02 - 0.05 * reach)
            + np.array([0.035 * math.cos(turning * 2), 0.0, 0.035 * math.sin(turning * 2)]) * reach)
    at_work = hand_turn("R", (-0.30, 0.75, -0.60), (-0.5, -0.1, -0.85))
    wiping = hand_turn("R", (-0.85, 0.45, 0.05), (-0.25, -0.2, -0.95))
    p.hand("R", mix(work + (0.035, -0.075, 0.06), wipe + (0.075 + 0.030 * rub, 0.0, 0.035), arm) + (0.0, -0.06 * bump(arm, 0.0, 1.0), 0.0), turned=between(at_work, wiping, arm), elbow=(0.8, -0.5, 0.3 * math.sin(turning * 2) * reach),
           curl=blend_holds("handle", "relaxed", arm))
    braced = hand_turn("L", (0.30, 0.95, 0.0), (0.0, 0.0, -1.0))
    under = hand_turn("L", (0.85, 0.45, 0.05), (0.2, 0.2, 0.95))
    p.hand("L", mix(BONNET_EDGE + (0.0, 0.0, 0.008) - braced @ PALM["L"], wipe + (-0.075 - 0.030 * rub, 0.0, -0.005), arm) + (0.0, -0.05 * bump(arm, 0.0, 1.0), 0.03 * bump(arm, 0.0, 1.0)),
           turned=between(braced, under, arm), elbow=(-0.7, -0.6, -0.3), curl=blend_holds(0.05, "relaxed", arm))
    p.shrug("L", 0.4 * (1 - arm))
    p.shrug("R", -0.4 * reach)
    p.look_at(mix(work + (0.0, 0.25, 0.0), mix(wipe, (0.2, 0.95, 0.85), ramp(t, 0.66, 0.76)), up), down=18)
    return p


# suitcase (Unity: 0.45 wide X, 0.65 high, 0.25 deep): a carry handle along x on top. The medic bag's grip, higher up.
SUITCASE = Grip("R", at=(0.0, 0.0, 0.630), thumb=(-1, 0, 0), back=(0, 1, 0), hold="handle")
CASE_OUT = 0.335                                      # the handle to the right of the hips: the case's inner face clears the thigh


def carry_suitcase_walk(t):
    # carry_bag_walk with a suitcase: the same walk and body, the case hanging from a straight arm held a little
    # out from the leg, its broad sides along the way they go, swinging a few degrees with the stride.
    p = carry_bag_walk(t)
    late = c(lag(t, 0.06))
    hips = p._hips[0]
    turned = turn(yaw=90 + 3 * late) @ turn(roll=3 * late)
    handle = hips + np.array([CASE_OUT, 0.03 - 0.045 * late, -0.905 + 0.815 + 0.008 * wave(t * 2)])
    p.chest(pitch=2, yaw=-7 * c(t), roll=-5.5)                                      # leaning away from the case
    p.tool(turned, handle - turned @ np.array([0.0, 0.0, 0.630]), SUITCASE, elbows={"R": (0.6, -1.0, 0.0)})
    p.shrug("R", -0.5)
    reach = -c(lag(t, 0.04))                                                        # the free arm swings wide to balance it: 0.27 m each way
    pelvis = p._hips[1]
    p.hand("L", hips + pelvis @ np.array([-0.235, 0.02 + 0.27 * reach, -0.075 + 0.135 * abs(reach) ** 1.5]), fingers=pelvis @ np.array([-0.05, 0.25 + 0.6 * reach, -1.0]), palm=pelvis @ np.array([1.0, 0.1, 0.0]),
           elbow=pelvis @ np.array([-0.4, -1.0, 0.0]), curl="relaxed")
    return p


# ---------------------------------------------------------------------------------------------- work, and trouble at it

DESK = 1.00                                           # the top of a planning desk
MARKERS = (np.array([0.25, 0.40, DESK]), np.array([-0.02, 0.49, DESK]), np.array([0.07, 0.51, DESK]), np.array([0.28, 0.43, DESK]))
WALL_MAP = np.array([0.15, 1.40, 1.80])


def desk_plan(t):
    # At a planning desk: the right hand picks a marker up off the board and puts it down somewhere else, the eyes
    # go up to the map on the wall beyond and a finger follows a road on it, then a second marker is moved the
    # other way; the left hand lies on the desk's edge. Made for the upper body: the legs only stand.
    a, b, c_, d = MARKERS
    over = np.array([0.0, 0.0, 0.07])
    point = np.array([0.20, 0.64, 1.40])                                             # the fingertip held out at the map
    tip = keys(t, (0.0, a + over), (0.07, a), (0.11, a), (0.25, b), (0.29, b), (0.37, point), (0.58, point), (0.67, c_), (0.71, c_), (0.85, d), (0.89, d), (1.0, a + over))
    tip = tip + (0.0, 0.0, 0.05 * bump(t, 0.11, 0.25) + 0.05 * bump(t, 0.71, 0.85))                       # carried over the others
    has = keys(t, (0.07, 0.0), (0.10, 1.0), (0.26, 1.0), (0.30, 0.0), (0.67, 0.0), (0.70, 1.0), (0.86, 1.0), (0.90, 0.0))
    up = keys(t, (0.0, 0.0), (0.30, 0.0), (0.38, 1.0), (0.56, 1.0), (0.64, 0.0), (1.0, 0.0))
    trace = s(t, 4) * up
    p = Pose()
    sway = idle(p, t, width=0.16, lean=4 + 2 * up, amount=0.6)
    p.chest(pitch=5 + 3 * up, yaw=-14 * (tip[0] - 0.12) * (1 - up) + 3 * up, roll=1.2 * sway)
    picking, showing = hand_turn("R", (-0.15, 0.78, -0.60), (-0.30, -0.45, -0.84)), hand_turn("R", (0.0, 0.92, 0.38), (-0.35, 0.30, -0.88))
    fingers = picking @ np.array([1.0, 0.0, 0.0])
    p.hand("R", tip - fingers * 0.115 * (1 - up) - np.array([0.0, 0.16, 0.0]) * up + (0.03 * trace, 0.0, 0.035 * (1 - up) + 0.02 * s(t, 4, 0.25) * up), turned=between(picking, showing, up),
           elbow=(0.25, -0.7, -0.9), curl=blend_holds(blend_holds("cup", "pinch", has), "point", up))
    palm_on(p, "L", (-0.22 + 0.02 * sway, 0.36, DESK + 0.008), fingers=(0.25, 0.95, 0.0), palm=(0.0, 0.0, -1.0), elbow=(-0.6, -0.6, -0.4), curl=0.08)
    p.look_at(mix(tip, WALL_MAP + (0.25 * trace, 0.0, 0.0), up), down=32, up=34)
    return p


def staff_struggle(t):
    # Stuck: hands on the hips, looking down at whatever it is on the floor in front and shaking the head; a look
    # away and a long breath out, then the right hand comes off the hip, open, at the thing ("and what am I meant
    # to do with that?") and goes back.
    down = keys(t, (0.0, 1.0), (0.34, 1.0), (0.43, 0.0), (0.58, 0.0), (0.68, 1.0), (1.0, 1.0))
    shake = s(t, 6) * bump(t, 0.04, 0.37, 1.0)
    breath = bump(t, 0.36, 0.52)                                                     # in ...
    sigh = bump(t, 0.44, 0.70)                                                       # ... and out: the chest and the shoulders fall
    ask = keys(t, (0.0, 0.0), (0.70, 0.0), (0.79, 1.0), (0.88, 1.0), (0.97, 0.0), (1.0, 0.0))
    peer = bump(t, 0.02, 0.30)                                                       # the head goes forward and down at it
    huff = bump(t, 0.14, 0.34)                                                       # a first, short breath out while looking
    p = Pose()
    # The weight on the left leg, then (2 s) over on to the right, and back for the open hand.
    sway = keys(t, (0.0, -0.85), (0.27, -0.85), (0.37, 0.85), (0.62, 0.85), (0.72, -0.60), (0.90, -0.60), (1.0, -0.85)) + 0.08 * s(t, 3)
    stand(p, width=0.17, lean=5 * down + 3 * peer - 2 * breath + 2 * sigh, sway=sway, height=0.905 + 0.008 * breath - 0.030 * sigh - 0.012 * huff, left=(-0.02, 0.03), right=(0.03, 0.07), toe_out=11,
          turn_feet=(-4.0, 8.0))
    p.chest(pitch=3 * down + 2 * peer - 3 * breath + 5 * sigh + 3 * huff + 2 * ask, yaw=-3 * sway + 4 * ask, roll=1.5 * sway)
    on_hip(p, "L")
    on_hip(p, "R", ask, towards=(0.27, 0.36, 1.03), fingers=(0.10, 0.99, -0.05), palm=(-0.15, 0.0, 1.0), curl=blend_holds("relaxed", "open", ask))
    for side in "LR":
        p.shrug(side, 0.25 + 0.6 * breath - 0.9 * sigh - 0.6 * huff + (0.3 * ask if side == "R" else 0.0))      # (1.0 is about 6 cm at the shoulder)
    p.head(pitch=4 + 27 * down + 10 * peer - 8 * breath + 6 * sigh + 3 * abs(shake), yaw=24 * shake - 26 * (1 - down) + 5 * ask, roll=4 * shake + 4 * ask)
    return p


COUNTER_TOP = 0.975
LEAN_ON = np.array([-0.21, 0.45, COUNTER_TOP + 0.006])               # the left palm on the counter


def collector_lean(t):
    # Leaning in over a counter on the left hand, a foot forward, the head pushed out at whoever is behind it; the
    # right hand low over the counter between them: two slow beats with the open palm up ("you know how it is"),
    # then the forefinger put down on the counter twice ("every week"), and open again.
    beat = wave(t * 5) * keys(t, (0.0, 1.0), (0.38, 1.0), (0.46, 0.0), (0.88, 0.0), (0.96, 1.0), (1.0, 1.0))
    late = wave(lag(t, 0.02) * 5)
    finger = keys(t, (0.0, 0.0), (0.42, 0.0), (0.52, 1.0), (0.80, 1.0), (0.90, 0.0), (1.0, 0.0))
    tap = wave(span(t, 0.54, 0.82) * 2)                                             # down on the counter and 9 cm up again, twice
    press = s(t) + 0.35 * s(t, 3)                                                    # on to the counter hand, and off it
    p = Pose()
    stand(p, width=0.17, height=0.925 - 0.008 * press, lean=20 + 4.5 * press + 1.5 * tap, sway=-0.45 - 0.35 * press, shift=(-0.03, 0.15 + 0.02 * press), left=(-0.01, 0.17), right=(0.02, 0.07), toe_out=12,
          yaw=3 - 3 * press)
    p.chest(pitch=4 + 1.5 * press, yaw=-7 - 4 * press, roll=-3.5 - 2.5 * press)
    palm_on(p, "L", LEAN_ON, fingers=(0.38, 0.92, 0.0), palm=(0.0, 0.0, -1.0), elbow=(-0.85, -0.45, -0.05), curl=0.04)
    p.shrug("L", 0.6 + 0.3 * press)
    open_ = np.array([0.15 + 0.02 * beat, 0.34 + 0.04 * beat, 1.17 - 0.06 * beat])
    down_ = np.array([0.02, 0.385, COUNTER_TOP + 0.235 - 0.090 * tap])                # the fingertip stops 3 cm over the counter
    asking, pointing = hand_turn("R", (-0.25, 0.92, 0.10 - 0.25 * (beat - late)), (-0.35, -0.10, 0.93)), hand_turn("R", (-0.30, 0.62, -0.72), (-0.35, -0.72, -0.60))
    p.hand("R", mix(open_, down_, finger) + (0.0, 0.0, 0.04 * bump(finger, 0.0, 1.0)), turned=between(asking, pointing, finger), elbow=(0.7, -0.6, -0.4), curl=blend_holds("open", "point", finger))
    p.look_at((0.0, 1.15, 1.56), up=40)
    p.head(roll=6 - 2 * press, pitch=3 * tap, yaw=3 * press)
    return p


CLIPBOARD = Grip("L", at=(-0.075, -0.07, 0.004), thumb=(0.25, 1, 0), back=(0, 0, -1), hold="plate")     # as in clips.py (clipboard_write)


def inspect_look(t):
    # An inspector with a clipboard in the left hand and a pen in the right: looks up and along a ceiling, down
    # and along a floor, then brings the board up, puts a tick on it and lets the pen hand drop.
    up = keys(t, (0.0, 0.0), (0.05, 0.0), (0.15, 1.0), (0.35, 1.0), (0.44, 0.0), (1.0, 0.0))
    down = keys(t, (0.0, 0.0), (0.41, 0.0), (0.51, 1.0), (0.63, 1.0), (0.71, 0.0), (1.0, 0.0))
    sweep = keys(t, (0.0, 0.0), (0.06, 0.0), (0.15, -1.0), (0.35, 1.0), (0.50, 0.7), (0.63, -0.7), (0.72, 0.0), (1.0, 0.0))
    tick = keys(t, (0.0, 0.0), (0.68, 0.0), (0.77, 1.0), (0.89, 1.0), (0.97, 0.0), (1.0, 0.0))
    stroke = span(t, 0.79, 0.86)
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.6, lean=-1.5 * up + 2 * down, yaw=4 * sweep)
    board = (turn(yaw=8, pitch=-32 + 12 * up - 4 * tick, roll=-6), np.array([-0.03 + 0.02 * sway, 0.335 - 0.075 * up, 1.05 - 0.01 * up + 0.03 * tick]))
    p.chest(pitch=3 - 4 * up + 2 * down, yaw=-3 + 8 * sweep, roll=1.2 * sway)
    p.tool(*board, CLIPBOARD, elbows={"L": (-0.6, -0.7, -0.5)})
    mark = np.array([0.075 + 0.05 * stroke, -0.03 - 0.02 * min(1.0, stroke / 0.3) + 0.055 * max(0.0, (stroke - 0.3) / 0.7), 0.122])
    pen = board[1] + board[0] @ mark
    rest = p._hips[0] + np.array([0.20, 0.20 - 0.03 * up, 0.10])                    # the pen hand waits by the board, the elbow bent
    p.hand("R", rest + (pen - rest) * tick + (0.05 * math.sin(math.pi * tick), 0.06 * math.sin(math.pi * tick), 0.0), fingers=unit(mix((-0.25, 0.9, -0.35), (-0.62, 0.55, -0.55), tick)),
           palm=mix((-0.9, -0.2, -0.4), (-0.45, 0.25, -0.86), tick), elbow=(0.75, -0.55, -0.4), curl="pen")
    look = 1.0 - max(up, down)
    p.head(yaw=34 * sweep * max(up, down) - 4 * look, pitch=22 * look - 36 * up + 34 * down)
    return p


CLIPS = [
    Clip("arrest_cuffed", 2.4, arrest_cuffed, loop=False, marks={"click": 1.5},
         note="from hands_up_hold (its first pose exactly): the hands come down out to the sides and behind the back, the cuffs close at 1.5 s; ends in cuffed_stand's first pose exactly"),
    Clip("officer_cuff", 2.4, officer_cuff, loop=False, marks={"click": 1.5}, tool="handcuffs", grips=(HANDCUFFS,), tool_length=0.08,
         note="the officer's half of arrest_cuffed: whoever is cuffed stands %.2f m ahead, back turned. A short step in with the left foot and a lean, the cuffs put on wrists %.2f m ahead and 0.94 m up "
              "(click 1.5 s), then the left hand on their left shoulder (0.17 m left, 0.86 m ahead, 1.40 m up); ends so, stepped in: hold the end. The cuffs in the right hand end exactly where "
              "CuffPose lays its pair: hide one of the two at the click" % (BEHIND, BEHIND - 0.132)),
    Clip("officer_guard_idle", 8.0, officer_guard_idle, note="standing easy on a door: thumbs hooked in the duty belt either side of the buckle, weight from foot to foot, a slow look left and right"),
    Clip("cuffed_car_enter", 2.0, cuffed_car_enter("R"), loop=False, marks={"door": 0.25, "seated": 1.7},
         note="job_car_enter with the hands cuffed behind the back, from cuffed_stand's first pose: a door on the car's RIGHT side, feet %.2f m out from the middle of the seat (%.2f m up); "
              "ducks low, turns, sits leaning forward; the door must be open by 0.25 s; the root does not move" % (CAR_OUT, CAR_SEAT)),
    Clip("cuffed_car_enter_l", 2.0, cuffed_car_enter("L"), loop=False, marks={"door": 0.25, "seated": 1.7}, note="the same for a door on the car's LEFT side"),
    Clip("board_step", 1.4, board_step, loop=False, marks={"step": 0.5, "aboard": 1.25},
         note="up through a bus door: the step's edge %.2f m ahead and %.2f m up, the right hand takes an upright rail 0.40 m right and 0.80 m ahead at 1.42 m and slides up it to 1.62 m; the root does not move; "
              "ENDS STANDING ON THE STEP leaning in (0.44 m ahead, 0.30 m up): take the person off the street then" % (STEP_AHEAD, STEP_UP)),
    Clip("strap_hang", 6.0, strap_hang, note="standing in a moving bus, the right hand in a strap 1.82 m up, 0.16 m ahead and 0.28 m right; hangs off it and sways with the bus, a look out of the window"),
    Clip("sit_bench", 1.6, sit_bench, loop=False, marks={"seated": 1.3}, dressing=BENCH,
         note="sits down on a seat %.2f m up whose middle is %.2f m BEHIND the feet; the root and the feet stay; ends in sit_wait's first pose exactly" % (BENCH_UP, BENCH_BACK)),
    Clip("sit_bench_up", 1.6, sit_bench_up, loop=False, marks={"standing": 1.4}, dressing=BENCH, note="gets up off that seat, from sit_wait's first pose exactly, to standing where the feet are"),
    Clip("sit_wait", 8.0, sit_wait, dressing=BENCH, note="sitting on that seat (%.2f m up, %.2f m behind the feet), hands on the thighs: leans out to look down the street, shifts, stretches a foot" % (BENCH_UP, BENCH_BACK)),
    Clip("refuel_hold", 5.0, refuel_hold, tool="jerry_can", grips=(CAN_HANDLE, CAN_BASE), tool_length=0.45,
         note="bent at the hips emptying a jerry can into a filler 0.40 m ahead and 0.90 m up: the can hangs from the filler back towards the left hip, below the chest: right hand on its handle, left under it by the base, which is lifted to pour"),
    Clip("bonnet_check", 6.0, bonnet_check, note="leaning over an open bonnet 0.90 m up: left hand braced on the bay's edge 0.40 m left and 0.44 m ahead, the right working 0.70 m ahead, 0.86 m up; then up, wiping the hands"),
    Clip("carry_suitcase_walk", 1.08, carry_suitcase_walk, speed=1.3, tool="suitcase", grips=(SUITCASE,), tool_length=0.65,
         note="carry_bag_walk with a suitcase in the right hand, held %.2f m out from the middle so that it clears the leg, its bottom 0.18 m off the ground: 1.3 m/s" % CASE_OUT),
    Clip("desk_plan", 6.0, desk_plan, mask="upper", note="at a desk %.2f m high: the right hand moves markers on a board 0.45 m ahead, a look up at a wall map beyond with a finger on it; left hand on the desk" % DESK),
    Clip("staff_struggle", 6.0, staff_struggle, note="stuck: hands on hips, looking down at something on the floor 0.6 m ahead, a shake of the head, a sigh, an open hand at it"),
    Clip("collector_lean", 5.0, collector_lean, watch="R", dressing=(("vendor_cart", turn(yaw=180), (0.0, 0.72, 0.0)),),      # on the sheets: a counter that high, he on the customers' side
         note="leaning in on the left hand flat on a counter %.3f m up and 0.45 m ahead; the right gestures low over it (open palm, then a forefinger jabbed down at it twice, 3 cm clear); rocks on to that hand and off it; looks at whoever is 1.1 m ahead" % COUNTER_TOP),
    Clip("inspect_look", 6.0, inspect_look, tool="clipboard", grips=(CLIPBOARD,), tool_length=0.3, watch="R",
         note="clipboard in the left hand (clipboard_write's grip): a look up and along a ceiling, down and along a floor, then a tick on the board with the right hand"),
]
