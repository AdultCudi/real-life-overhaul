"""Clips for the family module: a proposal and its answer, a wedding, a baby, a cheer. Same rules as clips.py.

THE CODE IS THE SPECIFICATION for the five the mod plays (Scripts/Family/Courtship.cs, Wedding.cs); where things are,
for whoever places the characters (x right, y ahead, z up, from the character's feet):

    propose_kneel        the PLAYER, whole body, a LOOP (JobBody.Clip: played until Rest, 5.6 s in all): kneeling on the
                         right knee, ring_box on the RIGHT palm 0.40 m ahead and 0.90 m up; the partner stands 1.25 m
                         ahead, facing
    propose_react_yes, propose_react_unsure
                         the PARTNER, whole body, once, 2.2 s (CityPerson.Work, ClipEnd.Return): the kneeler is 1.25 m
                         ahead, the box about 0.85 m ahead and 0.90 m up; both start and end in the standing idle
    ring_present         the PLAYER, whole body, once, 3.0 s: the partner stands 1.10 m ahead, facing; their left hand is
                         taken at (0.03, 0.61, 1.05) and the ring slid on from 1.26 to 1.86 s
    cheer                the PARTNER, whole body, once, 3.2 s: both arms up from 0.55 to 2.6 s (the photograph is at 2.2 s)

Not asked for by code yet:

    propose_kneel_down, _up   into the loop's first pose from standing (hold it, then play the loop), and out of it
    ring_receive         the other side of ring_present: the left hand held out to (-0.03, 0.49, 1.05)
    cheer_loop, cheer_once    a crowd's loop; one punch of the fist
    slow_dance_a, _b     the partners FACE, 0.45 m apart: put B 0.45 m ahead of A, turned to face A. The clasped hands
                         (A's left, B's right) are at A's (-0.45, 0.225, 1.35): 0.45 m to A's left of the line between
                         them, half way. A's right hand is on B's left side 1.25 m up, B's left on A's right shoulder
    hold_baby            baby_bundle lies in the crook of the left arm, head to the holder's left, across the chest
    rock_cot             facing the long side of cot: the near rail is 0.35 m ahead, 0.885 m up, so the cot's centre is
                         0.68 m ahead and it is turned a quarter turn (its length across the character)
"""
import math

import numpy as np

from motion import TAU, Clip, c, hang, idle, lag, s, stand, weight  # noqa: F401
from rig import J, PALM, Grip, Pose, basis, between, blend_holds, ease, keys, turn, unit, wave  # noqa: F401

SIDES = (("L", -1.0), ("R", 1.0))
ANKLE = float(J["RightFoot"][2])
BALL = float(J["RightToes"][1] - J["RightFoot"][1])                # the ball of the foot ahead of the ankle
TOE_DROP = float(J["RightFoot"][2] - J["RightToes"][2])            # and below it


# ---------------------------------------------------------------------------------------------- shared

def at_side(p, side, out=0.0):
    return p._hips[0] + np.array([(1.0 if side == "R" else -1.0) * (0.235 + out), 0.045, -0.045])


def mix(a, b, k):
    return np.asarray(a, dtype=float) * (1 - k) + np.asarray(b, dtype=float) * k


def hand_turn(side, fingers, palm):
    """How a hand is turned when its fingers and its palm point these ways."""
    return basis(fingers, palm) @ basis((1 if side == "R" else -1, 0, 0), (0, 0, -1)).T


def palm_at(p, side, centre, fingers, palm, elbow=None, curl="relaxed"):
    """A free hand placed by the middle of its palm (what touches a rail, a shoulder, another palm)."""
    turned = hand_turn(side, fingers, palm)
    p.hand(side, np.asarray(centre, dtype=float) - turned @ PALM[side], turned=turned, elbow=elbow, curl=curl)
    return p


def blend_hand(p, side, k, to, fingers, palm, elbow=None, curl="relaxed", arc=(0.0, 0.0, 0.0), rest=None):
    """A free hand part of the way (k) from hanging at the side to where the middle of its palm is at `to`."""
    sign = 1.0 if side == "R" else -1.0
    f = unit(unit(np.array([sign * 0.1, 0.3, -1.0])) * (1 - k) + unit(fingers) * k)
    n = np.array([-sign, 0.0, 0.0]) * (1 - k) + unit(palm) * k
    turned = hand_turn(side, f, n)
    rest = at_side(p, side) if rest is None else np.asarray(rest, dtype=float)
    wrist = rest + (np.asarray(to, dtype=float) - turned @ PALM[side] - rest) * k + np.asarray(arc, dtype=float) * math.sin(math.pi * k)
    p.hand(side, wrist, turned=turned, elbow=elbow if elbow is not None else (sign * 0.6, -0.6, -0.5), curl=blend_holds("relaxed", curl, k))
    return wrist


def tool_from_hand(grip, wrist, fingers, palm):
    """The pose of a tool when the hand that grips it is at `wrist`, turned so (for carrying it at the side)."""
    turned = hand_turn(grip.side, fingers, palm)
    return turned @ grip.turn, np.asarray(wrist, dtype=float) + turned @ grip.offset


# ---------------------------------------------------------------------------------------------- the proposal

# The box stands on the right palm, its open front (+y of the box) to whoever it is shown to; the fingers point
# forward and to the left under it, the thumb comes up its right side.
RING_BOX = Grip("R", at=(-0.027, 0.013, 0.0), thumb=(0.5, 1, 0), back=(0, 0, -1), hold=(0.35, 0.30, 0.34, 0.38, 0.42, 0.55, 0.1))
PARTNER = np.array([0.0, 1.25, 1.60])                 # the face of whoever is proposed to (Courtship.Kneel: 1.25 m in front)
KNEEL_BOX = np.array([0.05, 0.40, 0.90])
KNEELER = np.array([0.0, 1.05, 1.02])                 # the face of whoever kneels, from the partner's feet
OFFERED = np.array([0.0, 0.85, 0.92])                 # and the box they hold up


def box_carried(p):
    """The ring box in the right hand hanging at the side: closed in the fingers, clear of the thigh."""
    return tool_from_hand(RING_BOX, at_side(p, "R", 0.060), (0.08, 0.30, -1.0), (-0.9, 0.45, 0.0))


def box_shown(p, k, shown, tilt, through):
    """The box part of the way (k) from the side to where it is shown, by way of `through`."""
    rest_turn, rest = box_carried(p)
    return between(rest_turn, turn(pitch=-tilt, yaw=-6), ease(k)), keys(k, (0.0, rest), (0.45, through), (1.0, shown))


def proposal(step, back, down, box, left, t=None):
    """Down on the right knee with the ring box held up in the right hand, the left resting on the raised knee. Each
    argument goes 0..1: the left foot's step forward, the right foot's step back on to its toes, the knee going
    down, the box coming up, the left hand going to the knee. With t (the loop) the kneeler is alive: the box is
    offered up and forward once a loop, the chest lifts with it, the weight shifts on the knee, the head tilts."""
    step, back, down, box, left = float(step), float(back), float(down), float(box), float(left)
    offer = breath = sway = 0.0
    if t is not None:
        offer = float(keys(t, (0.10, 0.0), (0.30, 1.0), (0.52, 1.0), (0.76, 0.0)))
        breath, sway = s(t, 2), s(t, 1)
    p = Pose()
    going = math.sin(math.pi * step) * (1 - down)                       # the weight on the right leg while the left steps
    lean = 20 * math.sin(math.pi * down) ** 1.5 + 9.5 * down + 2.0 * offer            # towards the partner, from the hips
    p.hips((0.035 * going - 0.01 * down + 0.012 * sway, 0.05 * step * (1 - down) - 0.07 * down + 0.035 * offer, 0.905 - 0.43 * down - 0.012 * going + 0.004 * breath + 0.010 * offer),
           pitch=lean, roll=-2.5 * going + 1.5 * sway, yaw=-3 * down + 2.0 * sway)
    p.chest(pitch=3 - 2 * down - 4.0 * offer + 1.0 * breath, yaw=3 * down - 2.5 * sway, roll=-1.5 * sway)
    lift = math.sin(math.pi * step)
    p.foot("L", (-0.15 - 0.03 * step, 0.27 * step, 0.045 * lift), yaw=-8, pitch=-6 * lift, knee=mix((0.0, 1.0, 0.15), (-0.25, 0.3, 1.0), down))
    pitch = 34 * back + 46 * down                                       # up on the ball of the foot, then over on to the toes
    a = math.radians(pitch)
    toe = 0.148 - 0.568 * back                                          # where the toes are: 0.57 m back in the end
    ankle = (0.15 - 0.02 * back, toe - (BALL * math.cos(a) - TOE_DROP * math.sin(a)), BALL * math.sin(a) + TOE_DROP * math.cos(a) + 0.008 + 0.05 * math.sin(math.pi * back))
    p.foot("R", ankle=ankle, yaw=8 * (1 - back), pitch=pitch, toes=-55 * pitch / 80, knee=mix((0.0, 1.0, 0.15), (0.0, 0.8, -0.6), down))
    shown = KNEEL_BOX + np.array([-0.015, 0.060, 0.150]) * offer + (0.0, 0.0, 0.003 * breath) + np.array([0.0, -0.05, 0.31]) * (1 - down)      # with the body when it is held up standing
    p.tool(*box_shown(p, box, shown, 18 + 9 * offer, (0.22, 0.20, 0.76 + 0.27 * (1 - down))), RING_BOX, elbows={"R": mix((0.12, -0.60, -0.85), (0.45, -0.55, -0.7), box)})
    knee = p.solve().at["LeftLowerLeg"]                                 # the left hand lies on the raised thigh, just behind the knee
    blend_hand(p, "L", left, knee + (0.005, -0.075, 0.072), fingers=(0.12, 0.93, -0.33), palm=(0.05, 0.30, -0.95), elbow=(-0.65, -0.55, -0.5),
               curl=(0.2, 0.22 + 0.1 * offer, 0.26 + 0.1 * offer, 0.30 + 0.1 * offer, 0.34 + 0.1 * offer, 0.3, 0.2), arc=(-0.05, 0.03, 0.05))
    p.look_at(PARTNER, up=32)
    p.head(roll=3.5 * sway, pitch=-3.0 * offer + 1.5 * breath)
    return p


def propose_kneel(t):
    # The loop the player is kept in while the question is asked and answered: kneeling, the box held up; once a
    # loop it is offered a little higher and nearer, the chest lifting with it; the weight shifts, the head tilts.
    return proposal(1.0, 1.0, 1.0, 1.0, 1.0, t)


def propose_kneel_down(t):
    # A step forward with the left foot, the right goes back and its knee to the ground (1.7 s); the right hand
    # brings the open box up in front of the chest (2.5 s) and the left settles on the knee; the head stays on the
    # partner's face. Ends in the loop's first pose.
    return proposal(step=keys(t, (0.0, 0.0), (0.20, 1.0)), back=keys(t, (0.12, 0.0), (0.46, 1.0)), down=keys(t, (0.17, 0.0), (0.62, 1.0)),
                    box=keys(t, (0.34, 0.0), (0.89, 1.0)), left=keys(t, (0.38, 0.0), (0.84, 1.0)))


def propose_kneel_up(t):
    # From the kneel: up off the knee on to the left leg with the box still held up, the right foot comes forward,
    # the left steps back to where it started, and only then the box is lowered to the side.
    return proposal(step=keys(t, (0.56, 1.0), (0.88, 0.0)), back=keys(t, (0.22, 1.0), (0.60, 0.0)), down=keys(t, (0.04, 1.0), (0.52, 0.0)),
                    box=keys(t, (0.46, 1.0), (0.96, 0.0)), left=keys(t, (0.0, 1.0), (0.30, 0.0)))


def face_frame(p):
    """Where the head is and how it is turned, as the pose stands (after hips, chest and look)."""
    solved = p.solve()
    return solved.at["Head"], solved.turns["Head"]


def propose_react_yes(t):
    # The answer is yes: a start, half a step back with the right foot, both hands up in front of the mouth (the
    # elbows down by the ribs), the shoulders up; two quick nods over the fingertips; the hands come down and the
    # foot comes home. The eyes stay on whoever kneels 1.25 m ahead.
    back = float(keys(t, (0.05, 0.0), (0.23, 1.0), (0.74, 1.0), (0.94, 0.0)))
    up = float(keys(t, (0.07, 0.0), (0.28, 1.0), (0.70, 1.0), (0.97, 0.0)))
    weigh = float(keys(t, (0.0, 0.0), (0.09, -0.45), (0.30, 0.40), (0.70, 0.40), (0.82, -0.40), (1.0, 0.0)))
    k = (t - 0.32) / 0.36
    nods = (0.5 - 0.5 * math.cos(TAU * 2 * k)) if 0.0 < k < 1.0 else 0.0
    gasp = math.sin(math.pi * float(keys(t, (0.04, 0.0), (0.40, 1.0)))) ** 0.7
    p = Pose()
    stand(p, width=0.15, lean=-5.0 * back + 3.0 * nods, sway=weigh, shift=(0.0, -0.085 * back), right=(0.02 * back, -0.24 * back), lift=(0.0, 0.9 * math.sin(math.pi * back)),
          height=0.905 + 0.005 * gasp + 0.004 * nods - 0.014 * back)
    p.chest(pitch=-3.0 * gasp + 2.0 * nods, roll=-1.5 * weigh, yaw=-3.0 * back)
    p.look_at(KNEELER, down=26)
    p.head(pitch=10.0 * nods - 3.0 * gasp)
    head, turned = face_frame(p)
    for side, sign in SIDES:
        wait = 0.0
        late = float(keys(t, (0.07 + wait, 0.0), (0.29 + wait, 1.0), (0.70, 1.0), (0.97 - wait, 0.0)))
        to = head + turned @ np.array([sign * 0.050, 0.270 + 0.020 * nods, 0.035 - 0.045 * nods])      # they bob with each nod
        blend_hand(p, side, late, to, fingers=turned @ np.array([-sign * 0.42, -0.12, 0.90]), palm=turned @ np.array([-sign * 0.25, -0.96, 0.10]), elbow=(sign * 0.30, 0.15, -1.0),
                   curl=(0.12, 0.10, 0.14, 0.18, 0.22, 0.1, 0.25), arc=(sign * 0.07, 0.12, 0.0))
        p.shrug(side, 0.55 * up + 0.2 * gasp)
    return p


def propose_react_unsure(t):
    # Not sure: the weight sinks back on to the right hip and the left foot draws back after it, the right hand
    # comes flat to the middle of the breastbone (elbow down), the chest turns away to the left with the eyes; two
    # small shakes of the head, a breath out, a glance back at the kneeler; and back to standing.
    hold = float(keys(t, (0.05, 0.0), (0.30, 1.0), (0.70, 1.0), (0.96, 0.0)))
    away = float(keys(t, (0.18, 0.0), (0.38, 1.0), (0.52, 1.0), (0.64, 0.30), (0.74, 0.45), (0.90, 0.0)))
    sigh = math.sin(math.pi * float(keys(t, (0.40, 0.0), (0.72, 1.0))))
    k = (t - 0.34) / 0.30
    shake = math.sin(TAU * 2 * k) * math.sin(math.pi * k) if 0.0 < k < 1.0 else 0.0
    p = Pose()
    stand(p, width=0.15, lean=-2.5 * hold + 2.0 * sigh, sway=0.90 * hold, shift=(0.0, -0.050 * hold), left=(0.015 * hold, -0.13 * hold), lift=(0.75 * math.sin(math.pi * hold), 0.0),
          height=0.905 - 0.022 * hold - 0.008 * sigh)
    p.chest(pitch=1.5 * hold + 3.0 * sigh, roll=2.5 * hold, yaw=-13.0 * away)
    p.look_at(mix(KNEELER, (-0.85, 0.9, 0.50), 0.60 * away), down=30)
    p.head(yaw=13.0 * shake, pitch=3.0 * hold + 3.0 * sigh, roll=-5.0 * hold)
    solved = p.solve()
    chest, upper = solved.at["UpperChest"], solved.turns["UpperChest"]
    blend_hand(p, "R", hold, chest + upper @ np.array([0.030, 0.150, -0.005 - 0.020 * sigh]), fingers=upper @ np.array([-0.88, 0.05, 0.42]), palm=upper @ np.array([0.05, -0.99, 0.05]),
               elbow=(0.25, -0.25, -1.0), curl=(0.10, 0.04, 0.05, 0.07, 0.10, 0.1, 0.35), arc=(0.06, 0.12, 0.0))
    p.shrug("R", 0.35 * hold - 0.25 * sigh)
    p.shrug("L", 0.05 * hold)
    hang(p, "L", sway=0.6 * hold, swing=0.25 * hold + 0.15 * sigh, hold=blend_holds("relaxed", (0.3, 0.45, 0.5, 0.55, 0.6, 0.4, 0.0), hold))
    return p


# ---------------------------------------------------------------------------------------------- the rings

RING_APART = 1.10                                     # between the two at the front (Wedding.Lay: 0.55 m either side of the aisle)
MEET = np.array([0.03, 0.61, 1.05])          # where the partner's left hand is taken, from the giver's feet
THEIR_FACE = np.array([0.0, RING_APART, 1.60])


def step_in(p, t, lean, reach):
    """Half a step towards the other with the left foot and back again: the feet and hips of both ring clips."""
    step = float(keys(t, (0.02, 0.0), (0.19, 1.0), (0.82, 1.0), (0.98, 0.0)))
    weigh = float(keys(t, (0.0, 0.0), (0.07, 0.40), (0.24, -0.45), (0.80, -0.45), (0.90, 0.40), (1.0, 0.0)))
    stand(p, width=0.15, lean=lean * step, sway=weigh, shift=(0.0, reach * step), left=(0.0, 0.21 * step), lift=(0.8 * math.sin(math.pi * step), 0.0))
    return step, weigh


def ring_present(t):
    # Half a step in; the left hand comes up under the other's left hand and takes it (0.95 s), the right brings the
    # ring between finger and thumb and slides it on (1.26 to 1.86 s); both hands close round theirs for a moment;
    # the eyes go from the hands to their face; let go, and back.
    take = float(keys(t, (0.08, 0.0), (0.32, 1.0), (0.78, 1.0), (0.95, 0.0)))
    bring = float(keys(t, (0.14, 0.0), (0.42, 1.0), (0.77, 1.0), (0.94, 0.0)))
    slide = float(keys(t, (0.42, 0.0), (0.62, 1.0)))
    press = math.sin(math.pi * float(keys(t, (0.63, 0.0), (0.79, 1.0))))
    gaze = float(keys(t, (0.08, 0.0), (0.28, 1.0), (0.66, 1.0), (0.84, 0.0)))
    p = Pose()
    step, weigh = step_in(p, t, 5.0, 0.115)
    freed = math.sin(math.pi * float(keys(t, (0.77, 0.0), (0.97, 1.0))))             # the chest and the head lift as the hands let go
    p.chest(pitch=2.0 * step - 4.0 * freed, roll=-1.2 * weigh, yaw=2.0 * bring)
    blend_hand(p, "L", take, MEET + (0.015, -0.035, -0.032 + 0.006 * press), fingers=(0.28, 0.95, 0.06), palm=(0.0, -0.12, 1.0), elbow=(-0.5, -0.55, -0.7),
               curl=blend_holds("plate", "cup", 0.3 + 0.5 * press), arc=(-0.04, 0.05, 0.0))
    ring = MEET + (0.050 - 0.01 * press, -0.125 + 0.080 * slide, 0.068 - 0.016 * slide - 0.006 * press)       # 8 cm along their finger
    blend_hand(p, "R", bring, ring, fingers=(-0.38, 0.80, -0.47), palm=(-0.45, -0.25, -0.86), elbow=(0.45, -0.5, -0.75),
               curl=blend_holds("pinch", "cup", 0.8 * press), arc=(0.05, 0.06, 0.04))
    p.look_at(mix(THEIR_FACE, MEET, gaze), down=24)
    p.head(roll=-3.0 * gaze, pitch=-5.0 * freed)
    return p


def ring_receive(t):
    # The other side of ring_present: half a step in, the left hand held out palm down, fingers a little spread, to
    # where it is taken; it dips as the ring goes on; the right hand comes to the chest; the eyes on the ring, then up.
    out = float(keys(t, (0.06, 0.0), (0.30, 1.0), (0.85, 1.0), (0.98, 0.0)))
    slide = float(keys(t, (0.42, 0.0), (0.62, 1.0)))
    touched = float(keys(t, (0.30, 0.0), (0.50, 1.0), (0.80, 1.0), (0.95, 0.0)))
    gaze = float(keys(t, (0.08, 0.0), (0.28, 1.0), (0.66, 1.0), (0.84, 0.0)))
    p = Pose()
    step, weigh = step_in(p, t, 7.0, 0.11)
    p.chest(pitch=2.0 * step + 3.0 * out, roll=-1.2 * weigh, yaw=-3.0 * out)
    mine = np.array([-MEET[0], RING_APART - MEET[1], MEET[2]])
    blend_hand(p, "L", out, mine + (0.0, 0.0, -0.008 * math.sin(math.pi * slide)), fingers=(0.10, 0.99, -0.03), palm=(0.05, -0.03, -1.0), elbow=(-0.35, -0.5, -0.85),
               curl=(0.12, 0.03, 0.04, 0.05, 0.07, 0.1, 0.0), arc=(-0.04, 0.05, 0.03))
    solved = p.solve()
    chest, upper = solved.at["UpperChest"], solved.turns["UpperChest"]
    blend_hand(p, "R", touched, chest + upper @ np.array([0.03, 0.150, 0.045]), fingers=upper @ np.array([-0.80, 0.05, 0.60]), palm=upper @ np.array([0.10, -0.98, 0.10]),
               elbow=(0.45, -0.20, -0.9), curl=(0.12, 0.08, 0.10, 0.13, 0.17, 0.1, 0.4), arc=(0.06, 0.12, 0.0))
    p.look_at(mix(THEIR_FACE, mine, gaze), down=24)
    p.head(roll=4.0 * gaze)
    return p


# ---------------------------------------------------------------------------------------------- the dance

APART = 0.45                                          # between the partners' feet (the wish-list's)
CLASP = np.array([0.45, APART / 2, 1.35])             # the clasped hands: this far to the side of the line between them, half way
SWAYS = 2                                             # to one side and back, this many times a loop
REACH = 0.055                                         # how far the body goes over with the weight


def dance(lead):
    """One of a couple in a closed hold. The other is `APART` ahead, facing, and plays the other clip: both sway to
    the same side of the room, so each one's sway is the other's mirrored (half a sway later in its own terms)."""
    def pose(t):
        mine = t if lead else t + 0.5 / SWAYS
        w = weight(mine, SWAYS, settle=0.75)                           # -1 over my left foot .. +1 over my right
        phase = (mine * SWAYS) % 1.0
        left_up = math.sin(math.pi * min(1.0, max(0.0, (phase - 0.14) / 0.24))) ** 2      # the free foot is picked up and put down
        right_up = math.sin(math.pi * min(1.0, max(0.0, (phase - 0.64) / 0.24))) ** 2
        rise = 0.010 * c(t, 2 * SWAYS)                                 # the same for both: highest over a foot
        over = REACH * w                                               # how far I am to my right; the partner is as far to their left
        p = Pose()
        stand(p, width=0.12, lean=3.0, sway=w * REACH / 0.046, height=0.912 + rise, lift=(0.7 * left_up, 0.7 * right_up), toe_out=10)
        p.chest(pitch=1.5 + 0.8 * s(t, 3), roll=7.0 * w, yaw=3.0 * s(t, 1, 0.2) * (1.0 if lead else -1.0))     # the shoulders tilt 3.5 degrees against the hips
        if lead:
            # My left hand holds theirs out to my left; my right is round their left side, the fingers to their back.
            palm_at(p, "L", (-CLASP[0] + over, CLASP[1] - 0.008, CLASP[2] + rise), fingers=(-0.30, 0.25, 0.92), palm=(0.30, 0.95, -0.15),
                    elbow=(-0.45, -0.25, -0.85), curl="cup")
            palm_at(p, "R", (0.170 + over, APART + 0.050, 1.25 + rise), fingers=(-0.28, 0.95, 0.10), palm=(-1.0, -0.15, 0.0), elbow=(0.60, -0.30, -0.75),
                    curl=(0.15, 0.20, 0.24, 0.28, 0.32, 0.2, 0.2))
            p.look_at((0.03 + 0.5 * over, APART, 1.60), down=20)
            p.head(roll=3 * w, yaw=4 * s(t, 1, 0.1))
        else:
            # My right hand lies in their left; my left rests on their right shoulder.
            palm_at(p, "R", (CLASP[0] + over, CLASP[1] - 0.008, CLASP[2] + rise), fingers=(0.30, 0.25, 0.92), palm=(-0.30, 0.95, -0.15),
                    elbow=(0.45, -0.25, -0.85), curl="cup")
            palm_at(p, "L", (-0.195 + over, APART + 0.060, 1.452 + rise), fingers=(-0.10, 0.97, -0.22), palm=(0.15, -0.22, -0.96), elbow=(-0.20, -0.30, -0.95),
                    curl=(0.15, 0.18, 0.22, 0.26, 0.30, 0.2, 0.1))
            p.look_at((-0.03 + 0.5 * over, APART, 1.58), down=20)
            p.head(roll=-4 * w, yaw=-5 * s(t, 1, 0.35))
        return p
    return pose


# ---------------------------------------------------------------------------------------------- a cheer

STRAIGHT = 0.499                                      # shoulder to wrist with the elbow 20 degrees off straight


def shoulder_of(p, side):
    """Where the arm starts, as the pose stands (after hips, chest and shrug)."""
    return p.solve().at[("Right" if side == "R" else "Left") + "UpperArm"]


def arms_up(p, side, sign, k, pump, late):
    """An arm thrown up in a wide V (k: 0 at the side .. 1 up): nearly straight (20 degrees at the elbow) with the
    fist ahead of and outside the head; `pump` (0..1) pulls the fist 14 cm down towards the shoulder and lets it go."""
    p.shrug(side, 0.75 * k + 0.25 * (1 - pump) * k)
    rest = at_side(p, side)
    high = shoulder_of(p, side) + unit((sign * 0.34, 0.14, 1.0)) * (STRAIGHT - 0.14 * pump)
    wrist = keys(k, (0.0, rest), (0.45, (sign * 0.36, 0.26, 1.18)), (1.0, high))
    p.hand(side, wrist, fingers=unit(keys(k, (0.0, (sign * 0.1, 0.3, -1.0)), (0.45, (sign * 0.1, 0.7, 0.7)), (1.0, (sign * 0.28, 0.10 - 0.25 * (late - 0.5), 1.0)))),
           palm=keys(k, (0.0, (-sign, 0.0, 0.0)), (1.0, (-sign * 0.7, 0.7, 0.0))), elbow=(sign * 0.75, 0.35, -0.55), curl=blend_holds("relaxed", "fist", min(1.0, k * 2.5)))


def cheer(t):
    # Once: a deep dip (0.3 s), then both arms thrown up straight in a V and up off the heels (0.55 s), three
    # bounces with the fists pumping in turn, the shoulders tilting and the head back; down again by 3.2 s.
    up = float(keys(t, (0.05, 0.0), (0.17, 1.0), (0.82, 1.0), (0.98, 0.0)))
    dip = math.sin(math.pi * float(keys(t, (0.0, 0.0), (0.19, 1.0)))) + 0.5 * math.sin(math.pi * float(keys(t, (0.82, 0.0), (1.0, 1.0))))
    k = (t - 0.15) / 0.66
    bounce = (0.5 - 0.5 * math.cos(TAU * 3 * k)) if 0.0 < k < 1.0 else 0.0
    turn_to = math.sin(TAU * 1.5 * k) * math.sin(math.pi * k) if 0.0 < k < 1.0 else 0.0
    p = Pose()
    stand(p, width=0.17, lean=7.0 * dip - 2.5 * up, height=0.900 + 0.045 * bounce - 0.070 * dip, lift=(1.0 * bounce, 1.0 * bounce), sway=0.45 * turn_to, toe_out=12)
    p.chest(pitch=-3.5 * up + 3.0 * dip, roll=6.0 * turn_to, yaw=6.0 * turn_to)
    for side, sign in SIDES:
        wait = 0.0 if side == "R" else 0.015
        j = (t - 0.17 - (0.0 if side == "R" else 0.11)) / 0.66
        pump = (0.5 - 0.5 * math.cos(TAU * 3 * j)) if 0.0 < j < 1.0 else 0.0
        arms_up(p, side, sign, float(keys(t, (0.05 + wait, 0.0), (0.17 + wait, 1.0), (0.82, 1.0), (0.98, 0.0))), pump, pump)
    p.head(pitch=-10.0 * up + 4.0 * bounce * up + 4.0 * dip, yaw=7.0 * turn_to, roll=-3.0 * turn_to)
    return p


def cheer_loop(t):
    # A crowd's loop: both fists up in a straight-armed V pumping in turn, four bounces off the heels a loop with
    # 5.5 cm of rise, the shoulders tilting from side to side with the fist that is up.
    beat = wave(t * 4)
    tilt = s(t, 2)
    p = Pose()
    stand(p, width=0.17, lean=1.5 - 3.5 * beat, height=0.885 + 0.055 * beat, lift=(1.0 * beat, 1.0 * beat), sway=0.45 * tilt, toe_out=12)
    p.chest(pitch=-3 + 1.5 * beat, roll=7.0 * tilt, yaw=6 * s(t, 2, 0.1))
    for side, sign in SIDES:
        pump = wave(lag(t, 0.02) * 2 + (0.25 if side == "R" else 0.75))       # the right fist is up when the trunk tilts right
        late = wave(lag(t, 0.035) * 2 + (0.25 if side == "R" else 0.75))
        arms_up(p, side, sign, 1.0, pump, late)
    p.head(pitch=-9 + 4 * beat, yaw=8 * s(t, 1), roll=-3 * tilt)
    return p


def cheer_once(t):
    # "Yes!": a dip, then up on the toes with the right fist driven straight up beside the head (0.5 s), the trunk
    # leaning 5 degrees to the left under it, the left fist clenched at the chest; held 0.3 s, and back.
    up = float(keys(t, (0.0, 0.0), (0.26, 1.0), (0.46, 1.0), (0.90, 0.0)))
    arm = float(keys(t, (0.04, 0.0), (0.275, 1.0), (0.45, 1.0), (0.86, 0.0)))
    dip = math.sin(math.pi * float(keys(t, (0.0, 0.0), (0.20, 1.0))))
    toes = math.sin(math.pi * float(keys(t, (0.14, 0.0), (0.66, 1.0)))) ** 0.7
    p = Pose()
    stand(p, width=0.16, lean=4.0 * dip - 3.0 * up, height=0.900 + 0.045 * toes - 0.045 * dip, lift=(1.0 * toes, 1.0 * toes), sway=-0.35 * up)
    p.chest(pitch=-3 * up + 2.0 * dip, roll=-5.0 * up, yaw=-4 * up)
    p.shrug("R", 1.0 * arm)
    rest = at_side(p, "R")
    high = shoulder_of(p, "R") + unit((0.10, 0.06, 1.0)) * STRAIGHT
    wrist = keys(arm, (0.0, rest), (0.40, (0.31, 0.24, 1.22)), (1.0, high))
    p.hand("R", wrist, fingers=unit(keys(arm, (0.0, (0.1, 0.3, -1.0)), (0.40, (0.0, 0.6, 0.8)), (1.0, (0.06, 0.05, 1.0)))), palm=keys(arm, (0.0, (-1, 0, 0)), (1.0, (-0.8, 0.6, 0.0))),
           elbow=(0.45, 0.25, -0.85), curl=blend_holds("relaxed", "fist", min(1.0, arm * 2.5)))
    blend_hand(p, "L", up, (-0.20, 0.24, 1.17), fingers=(0.25, 0.45, 0.85), palm=(0.85, 0.2, -0.3), elbow=(-0.5, -0.6, -0.6), curl="fist", arc=(-0.04, 0.03, 0.0))
    p.head(pitch=-9 * up + 3.0 * dip, roll=3.0 * up)
    return p


# ---------------------------------------------------------------------------------------------- the baby

# baby_bundle lies along its own +y, head at +y, underside on its z 0. The left palm is under it 0.10 m towards the
# feet from its middle, fingers pointing to the feet, thumb to the bundle's +x (away from the holder's body).
BABY = Grip("L", at=(0.0, -0.10, 0.0), thumb=(1, 0, 0), back=(0, 0, -1), hold=(0.25, 0.30, 0.32, 0.36, 0.40, 0.45, 0.3))
BABY_ALONG = unit((-0.94, 0.06, 0.20))                 # from its feet to its head: to the holder's left and a little up
BABY_UP = (0.10, -0.42, 0.90)                         # the way it faces: up, and turned to the holder
BABY_AT = np.array([-0.035, 0.190, 0.035])           # the middle of its underside, from the base of the neck's height on the chest


def cradle(p, rock=0.0, bounce=0.0):
    """The bundle's pose against the chest, going with the trunk as it rocks."""
    hips, pelvis = p._hips
    chest = pelvis @ p._chest
    base = hips + pelvis @ np.array([0.0, 0.0, 0.09]) + chest @ np.array([0.0, 0.0, 0.18])       # between the shoulder blades' height
    turned = basis(BABY_ALONG, BABY_UP)                                # columns: along, facing, and their cross
    frame = np.stack([-turned[:, 2], turned[:, 0], turned[:, 1]], axis=1)                    # the bundle's x, y, z in the chest's space
    frame = turn(roll=5 * rock) @ frame
    return chest @ frame, base + chest @ (BABY_AT + (0.012 * rock, 0.0, 0.010 * bounce))


def hold_baby(t):
    # The baby in the crook of the left arm against the chest; a slow rock from side to side with the trunk, the
    # right hand on its legs patting now and then; the head bent to it, with a look up once a loop.
    rock = s(t, 2)
    late = s(lag(t, 0.02), 2)
    pats = keys(t, (0.0, 0.0), (0.50, 0.0), (0.56, 1.0), (0.80, 1.0), (0.86, 0.0), (1.0, 0.0)) * max(0.0, s(t, 12)) ** 1.5
    up = keys(t, (0.0, 0.0), (0.20, 0.0), (0.28, 1.0), (0.40, 1.0), (0.48, 0.0), (1.0, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.16, amount=0.6, lean=1.5)
    p.chest(pitch=3, yaw=5 * rock, roll=7.0 * rock + 1.0 * sway)
    turned, where = cradle(p, late, wave(t * 4))
    p.tool(turned, where, BABY, elbows={"L": (-0.55, -0.15, -0.85)})
    p.curl("L", (0.22, 0.10, 0.12, 0.14, 0.16, 0.30, 0.2))                               # flat under it: the fingers do not come up into the blanket
    p.shrug("L", 0.25)
    patted = where + turned @ np.array([-0.035, -0.135, 0.108 + 0.030 * pats])              # on the far side of its legs
    palm_at(p, "R", patted, fingers=unit(turned @ np.array([0.25, 0.75, -0.20]) + (0.0, 0.0, 0.25)), palm=turned @ np.array([0.35, 0.0, -1.0]), elbow=(0.12, -0.40, -1.0),
            curl=(0.15, 0.16, 0.20, 0.24, 0.28, 0.2, 0.2))
    face = where + turned @ np.array([0.0, 0.18, 0.16])
    p.look_at(mix(face, (0.5, 2.0, 1.55), up), down=38)
    p.head(roll=-4 * (1 - up) + 2 * rock)
    return p


RAIL = np.array([0.0, 0.35, 0.885])                    # the top of the cot's near rail
RAIL_HANDS = 0.21                                     # either side of the middle
ROCK = 0.028                                          # the rail goes this far to and fro


def rock_cot(t):
    # Bent over the cot from the hips with both hands on its rail, pushing it gently away and back twice a loop,
    # the weight going from foot to foot; the eyes on whoever is in it.
    rock = s(t, 2)
    late = s(lag(t, 0.025), 2)
    p = Pose()
    stand(p, width=0.17, height=0.925, lean=22 + 4.0 * rock, sway=0.7 * weight(t, 1), shift=(0.0, 0.035 + 0.015 * rock), left=(0.0, 0.04), right=(0.0, -0.03), toe_out=10)
    p.chest(pitch=6 + 1.0 * s(t, 2, 0.1), roll=2.5 * s(t, 1), yaw=4.0 * s(t, 1, 0.1))
    for side, sign in SIDES:
        palm_at(p, side, RAIL + (sign * RAIL_HANDS, ROCK * late, 0.006), fingers=(-sign * 0.20, 0.88, -0.42), palm=(0.0, 0.42, -0.90), elbow=(sign * 0.6, -0.6, -0.5), curl="bar")
    p.look_at((0.30 * s(t, 1, 0.1), 0.66, 0.52), down=40)
    p.head(roll=5 * s(t, 1, 0.15))
    return p


CLIPS = [
    # Asked for by the code.
    Clip("propose_kneel", 6.0, propose_kneel, tool="ring_box", grips=(RING_BOX,), tool_length=0.06,
         note="the kneeling LOOP of a proposal, whole body: on the right knee (left foot 0.27 m forward, the right knee on the ground 0.03 m ahead of the root, its "
              "foot 0.33 m behind), the open ring box on the right palm 0.40 m ahead and 0.90 m up, the left hand on the left knee, looking up at a partner 1.25 m "
              "ahead. Once a loop (0.6 to 4.6 s) the box is offered 15 cm higher and 6 cm nearer, the chest lifting and the weight settling forward. Faded into from standing (the code's 0.35 s), or "
              "after propose_kneel_down"),
    Clip("propose_react_yes", 2.2, propose_react_yes, loop=False, marks={"hands": 0.62, "down": 1.54},
         note="the partner's yes to a kneeler 1.25 m ahead: half a step back with the right foot (0.24 m; the root stays), both hands up in front of the mouth "
              "(0.10 m clear of it, elbows down), two nods, hands down and the foot home. Starts and ends in the standing idle"),
    Clip("propose_react_unsure", 2.2, propose_react_unsure, loop=False, marks={"hand": 0.66, "down": 1.54},
         note="the partner's doubt: the weight back on the right foot, the left drawn back 0.13 m, the right hand flat on the middle of the breastbone, the chest and the eyes "
              "turned 13 degrees off the kneeler to the left, two small shakes of the head, a breath out and a glance back; back to the standing idle"),
    Clip("ring_present", 3.0, ring_present, loop=False, marks={"take": 0.95, "ring": 1.86, "release": 2.5},
         note="the ring put on: half a step in with the left foot (the root stays), the left hand under the other's left hand at (0.03, 0.61, 1.05) (they "
              "stand 1.10 m ahead, facing; ring_receive holds that hand out), the right slides the ring on from 1.26 to 1.86 s, both hands close round "
              "theirs, a look up at their face; back to standing. Nothing is held: no prop"),
    Clip("cheer", 3.2, cheer, loop=False, marks={"up": 0.55, "down": 2.62},
         note="once: a dip, both arms thrown up nearly straight in a V (wrists 0.33 m either side, about 1.9 m up) and up on the toes, three bounces with the fists "
              "pumping in turn, the head back; arms up from 0.55 to 2.6 s. Needs 0.5 m clear either side"),
    # Not asked for by code yet.
    Clip("propose_kneel_down", 2.8, propose_kneel_down, loop=False, tool="ring_box", grips=(RING_BOX,), tool_length=0.06, marks={"kneel": 1.74, "present": 2.5},
         note="from standing into the first pose of propose_kneel (exactly): the left foot steps 0.27 m forward, the right goes back and its knee down, the "
              "box comes up in the right hand. Play with ClipEnd.Hold and then the loop. The box is in the hand from the start, low at the right side"),
    Clip("propose_kneel_up", 1.8, propose_kneel_up, loop=False, tool="ring_box", grips=(RING_BOX,), tool_length=0.06,
         note="from the first pose of propose_kneel back to standing where one stood, the box still held up until one is on one's feet, then lowered to the right side"),
    Clip("ring_receive", 3.0, ring_receive, loop=False, watch="L", marks={"taken": 0.95, "ring": 1.86},
         note="the other side of ring_present (the giver is 1.10 m ahead, facing): half a step in, the left hand held out palm down to (-0.03, 0.49, 1.05), elbow bent, "
              "the right hand to the chest, the eyes on the ring and then up; back to standing"),
    Clip("cheer_loop", 2.4, cheer_loop, note="a crowd's loop: both fists up in a V pumping in turn, four bounces on the toes a loop; needs 0.5 m clear either side"),
    Clip("cheer_once", 1.8, cheer_once, loop=False, marks={"top": 0.5}, note="one punch of the right fist into the air, up on the toes, and back to standing"),
    Clip("slow_dance_a", 8.0, dance(True), watch="L",
         note="the one who leads. The partner (slow_dance_b) is 0.45 m ahead, facing. Left hand holds theirs at (-0.45, 0.225, 1.35); right hand on "
              "their left side 1.25 m up, fingers round to the back. Two slow sways a loop, 5.5 cm each way with the shoulders tilting against the hips, the free foot lifted and set down"),
    Clip("slow_dance_b", 8.0, dance(False), watch="R",
         note="the one who follows. The partner (slow_dance_a) is 0.45 m ahead, facing. Right hand in theirs at (0.45, 0.225, 1.35); left hand on "
              "their right shoulder 1.45 m up. Start both clips at the same moment: they sway to the same side of the room"),
    Clip("hold_baby", 8.0, hold_baby, mask="upper", tool="baby_bundle", grips=(BABY,), tool_length=0.5,
         note="baby_bundle cradled in the crook of the left arm, its head to the holder's left 1.31 m up, its middle 0.19 m ahead and 1.19 m up, its length across the chest; the "
              "left palm is under it 0.10 m towards the feet from its middle; the right hand lies on its legs and pats; a slow rock from side to side"),
    Clip("rock_cot", 6.0, rock_cot, dressing=(("cot", turn(yaw=90), (0.0, 0.68, 0.0)),),
         note="bent over the long side of cot, both hands on its rail (0.885 m up, 0.35 m ahead, 0.21 m either side). The hands push the rail "
              "0.028 m away and back twice a loop: y = 0.35 + 0.028 sin(2 pi t / 3 s), for whoever wants the cot to follow"),
]
