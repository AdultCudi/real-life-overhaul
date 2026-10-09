"""Clips for the site module: civic life and the courts. Same rules as clips.py.

    litter_pick      ASKED FOR BY CODE (Scripts/Justice/CommunityService.cs): played whole-body on the player, once, for
                     1.8 s; the litter is switched off 1.1 s in. The code turns the player to face the litter and
                     attaches NO tool, so this is a bare hand. It has no say in how far away the litter is (anything
                     up to 1.0 m): the hand goes to the ground 0.55 m ahead
    speech_gesture   on Civic's wish-list (the code plays talk_a on a rally's speaker today, whole-body, looping). The
                     speaker stands on soapbox (0.30 m up, 0.60 x 0.45: the feet stay inside 0.36 x 0.26) and the
                     listeners 2.3 m ahead, 1.15 m apart, on the ground
    oath_hand, podium_speak    not asked for by code: made for an earlier wish-list, moved here from the family module
                     (Justice's wish-list has a court_swear for a courtroom that does not exist: oath_hand is that hand)

Where things are (x right, y ahead, z up, from the character's feet) is in each clip's note.
"""
import math

import numpy as np

from motion import TAU, Clip, c, hang, idle, lag, s, stand, weight  # noqa: F401
from rig import J, PALM, Pose, basis, between, blend_holds, ease, keys, turn, unit, wave  # noqa: F401

SIDES = (("L", -1.0), ("R", 1.0))
ANKLE = float(J["RightFoot"][2])
BALL = float(J["RightToes"][1] - J["RightFoot"][1])                # the ball of the foot ahead of the ankle


# ---------------------------------------------------------------------------------------------- shared

def at_side(p, side, out=0.0):
    return p._hips[0] + np.array([(1.0 if side == "R" else -1.0) * (0.235 + out), 0.045, -0.045])


def mix(a, b, k):
    return np.asarray(a, dtype=float) * (1 - k) + np.asarray(b, dtype=float) * k


def hand_turn(side, fingers, palm):
    """How a hand is turned when its fingers and its palm point these ways."""
    return basis(fingers, palm) @ basis((1 if side == "R" else -1, 0, 0), (0, 0, -1)).T


def palm_at(p, side, centre, fingers, palm, elbow=None, curl="relaxed"):
    """A free hand placed by the middle of its palm (what touches a rail, a knee, another palm)."""
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


def on_ball(p, side, x, heel, yaw=0.0, knee=None):
    """A foot kept where it stands with its heel coming up `heel` degrees: it turns about the ball, the toes stay flat."""
    rotated = turn(pitch=heel) @ np.array([0.0, -BALL, ANKLE])
    p.foot(side, ankle=(x, BALL + rotated[1], rotated[2]), yaw=yaw, pitch=heel, toes=-heel, knee=knee)


def bump(t, start, end):
    """0 outside start..end, a smooth rise and fall between: a foot lifted for a step."""
    k = (t - start) / (end - start)
    return math.sin(math.pi * k) if 0.0 < k < 1.0 else 0.0


# ---------------------------------------------------------------------------------------------- community service

LITTER = np.array([0.05, 0.55, 0.0])                  # what is picked up
PICK = np.array([0.055, 0.535, 0.105])                # the middle of the right palm when the fingers close on it
LEAN, SINK, AHEAD = 36.0, 0.545, 0.375                # at the bottom: degrees forward at the hips, how far they come down, and forward


def litter_pick(t):
    # A step forward with the left foot and down into a squat between the knees, the right heel off the ground, the
    # chest kept up (36 degrees forward at the hips, no more) and the head with it; the left forearm lies along the
    # left thigh, hand on the knee; the right shoulder dips and the right hand goes to the ground ahead, closes
    # (1.1 s), and all of it comes back up the way it went.
    down = keys(t, (0.0, 0.0), (0.54, 1.0), (0.67, 1.0), (0.98, 0.0))
    arm = keys(t, (0.09, 0.0), (0.585, 1.0), (0.635, 1.0), (0.90, 0.0))
    step = keys(t, (0.0, 0.0), (0.16, 1.0), (0.80, 1.0), (0.96, 0.0))
    lift = bump(t, 0.0, 0.16) + bump(t, 0.80, 0.96)
    closed = keys(t, (0.575, 0.0), (0.611, 1.0), (1.0, 1.0))
    p = Pose()
    stand(p, width=0.15, lean=LEAN * down, height=0.905 - SINK * down, shift=(0.015 * down, AHEAD * down), yaw=-9 * down, sway=0.35 * bump(t, -0.04, 0.20) + 0.3 * bump(t, 0.78, 1.02),
          left=(-0.05 * step, 0.27 * step), lift=(lift, 0.0))
    p.foot("L", (-0.15 - 0.05 * step, 0.27 * step, 0.035 * lift), yaw=-8 - 12 * step, pitch=14 * lift, knee=(-0.60 * down, 1.0, 0.15 + 0.35 * down))      # the knees go apart: the trunk
    on_ball(p, "R", 0.15 + 0.03 * down, 56 * down, yaw=8 + 14 * down, knee=(0.70 * down, 1.0, 0.15 - 0.30 * down))                                 # comes down between them
    p.chest(pitch=7 * down, yaw=-12 * down, roll=7 * down)        # the right shoulder goes down and forward to the hand
    rest = at_side(p, "R")
    turned = hand_turn("R", unit(mix((0.1, 0.3, -1.0), (0.0, 0.32, -0.95), arm)), mix((-1.0, 0.0, 0.0), (-0.25, -0.92, -0.30), arm))
    wrist = rest + (PICK - turned @ PALM["R"] - rest) * arm + np.array([0.02, 0.10, 0.05]) * math.sin(math.pi * arm) * (1.0 if t < 0.6 else 0.4)
    p.hand("R", wrist, turned=turned, elbow=(0.8, -0.4, -0.3), curl=blend_holds(blend_holds("relaxed", "open", arm * (1 - closed)), "pinch", closed * min(1.0, 2.5 * (1.0 - t))))
    knee = p.solve().at["LeftLowerLeg"]
    blend_hand(p, "L", down, knee + (0.0, -0.035, 0.065), fingers=(0.25, 0.90, -0.30), palm=(0.05, -0.30, -0.95), elbow=(-0.35, -0.75, -0.75), curl="cup")
    look = keys(t, (0.0, 0.0), (0.22, 1.0), (0.70, 1.0), (1.0, 0.0))
    p.look_at(mix((0.0, 3.0, 1.55), LITTER + (0.0, 0.25, 0.0), look), down=24)
    return p


# ---------------------------------------------------------------------------------------------- a rally

CROWD = np.array([0.0, 2.3, 1.30])                    # the middle listener's face, seen from 0.30 m up on the box
FLAT = (0.10, 0.03, 0.04, 0.05, 0.06, 0.0, 0.0)       # an open hand with the fingers TOGETHER: the game's hand has three, and spread they read as a V


def speech_gesture(t):
    # A speaker to a crowd: (1) the right forefinger up beside the head, jabbed forward on the words; (2) both arms
    # opened wide to them, palms up; (3) the edge of the right hand brought down on the left palm, three times; then
    # the hands drop for a breath. The weight goes from foot to foot, the trunk leans in on each point, the head goes
    # along the listeners.
    point = keys(t, (0.02, 0.0), (0.09, 1.0), (0.27, 1.0), (0.36, 0.0))                 # each beat begins as the one before lets go: the hands
    wide = keys(t, (0.29, 0.0), (0.38, 1.0), (0.54, 1.0), (0.63, 0.0))                  # never drop between them (the three never add up to more than 1)
    chop = keys(t, (0.56, 0.0), (0.65, 1.0), (0.84, 1.0), (0.93, 0.0))
    jab = max(0.0, s(lag(t, 0.006), 12, 0.02)) ** 0.7 * point
    blow = (0.5 - 0.5 * c(t, 16, 0.02)) ** 2 * chop                   # down on the palm three times in the hold, and soft landings
    p = Pose()
    sway = idle(p, t, turns=2.0, width=0.13, amount=0.8, lean=2.5 + 3.0 * point + 2.5 * jab + 2.0 * chop + 1.5 * wide)
    p.chest(pitch=1.5 + 3.0 * jab - 3.0 * wide + 1.5 * blow, yaw=-6 * point + 3 * s(t, 1, 0.1), roll=1.2 * sway)
    # The right hand.
    rest = at_side(p, "R")
    up = np.array([0.40, 0.27 + 0.19 * jab, 1.51 - 0.04 * jab])       # 0.3 m clear of the head, and 0.2 m of travel with the trunk's
    out = np.array([0.53, 0.21, 1.26 + 0.02 * s(t, 8)])
    edge = np.array([0.095, 0.36, 1.275 - 0.095 * blow])
    wrist = rest + (up - rest) * point + (out - rest) * wide + (edge - rest) * chop + np.array([0.06, 0.08, 0.0]) * math.sin(math.pi * max(point, chop))
    fingers = unit(np.array([0.1, 0.3, -1.0]) * (1 - point - wide - chop) + np.array([-0.04, 0.30 + 0.55 * jab, 0.94]) * point + np.array([0.82, 0.52, 0.22]) * wide + np.array([-0.42, 0.88, -0.20]) * chop)
    palm = np.array([-1.0, 0.0, 0.0]) * (1 - point - wide - chop) + np.array([-0.95, 0.30, 0.0]) * point + np.array([-0.25, 0.10, 0.96]) * wide + np.array([-0.85, -0.45, -0.25]) * chop
    hold = blend_holds(blend_holds(blend_holds("relaxed", (0.95, 0.0, 0.95, 0.97, 0.98, 1.0, 0.0), point), FLAT, wide), FLAT, chop)      # one finger, the thumb shut on the rest
    p.hand("R", wrist, fingers=fingers, palm=palm, elbow=(0.25 + 0.3 * wide, -0.25, -1.0), curl=hold)
    p.shrug("R", 0.30 * point + 0.15 * wide)
    # The left hand: quiet for the first point, then with the right.
    rest = at_side(p, "L")
    held = np.array([-0.26, 0.20, 1.02])                               # a loose fist at the belt while the right hand points
    out = np.array([-0.53, 0.21, 1.25 - 0.02 * s(t, 8)])
    under = np.array([-0.075, 0.33, 1.105 - 0.012 * blow])
    wrist = rest + (held - rest) * point + (out - rest) * wide + (under - rest) * chop + np.array([-0.05, 0.07, 0.0]) * math.sin(math.pi * max(point, chop))
    fingers = unit(np.array([-0.1, 0.3, -1.0]) * (1 - point - wide - chop) + np.array([0.55, 0.70, -0.45]) * point + np.array([-0.82, 0.52, 0.22]) * wide + np.array([0.50, 0.84, 0.10]) * chop)
    palm = np.array([1.0, 0.0, 0.0]) * (1 - point - wide - chop) + np.array([0.55, -0.60, -0.55]) * point + np.array([0.25, 0.10, 0.96]) * wide + np.array([0.10, -0.10, 0.99]) * chop
    hold = blend_holds(blend_holds(blend_holds("relaxed", (0.45, 0.62, 0.66, 0.70, 0.74, 0.6, 0.0), point), FLAT, wide), FLAT, chop)
    p.hand("L", wrist, fingers=fingers, palm=palm, elbow=(-0.30 - 0.25 * wide, -0.30, -1.0), curl=hold)
    p.shrug("L", 0.15 * wide)
    along = keys(t, (0.0, -1.0), (0.12, -1.0), (0.30, 0.7), (0.44, 1.0), (0.58, -0.6), (0.70, 0.0), (0.86, 0.2), (1.0, -1.0))      # along the listeners, 1.15 m either side
    p.look_at(CROWD + (1.15 * along, 0.0, 0.0), down=30)
    p.head(pitch=3.0 * jab + 4.0 * blow - 3.0 * wide, roll=2.0 * sway)
    return p


# ---------------------------------------------------------------------------------------------- office (not asked for by code)

def oath_hand(t):
    # The right hand raised beside the shoulder, palm forward, fingers together (0.6 s), kept there 1.6 s, lowered.
    # The trunk straightens, the eyes are level.
    up = keys(t, (0.02, 0.0), (0.20, 1.0), (0.735, 1.0), (0.97, 0.0))
    body = keys(t, (0.0, 0.0), (0.18, 1.0), (0.75, 1.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.15, lean=-1.0 * body, sway=0.15 * body)
    p.chest(pitch=-1.5 * body)
    rest = at_side(p, "R")
    high = np.array([0.405, 0.215, 1.455 + 0.004 * s(t, 5) * body])                  # over the elbow, which hangs at the ribs: nowhere near the ear
    wrist = keys(up, (0.0, rest), (0.5, (0.36, 0.27, 1.10)), (1.0, high))
    p.hand("R", wrist, fingers=unit(keys(up, (0.0, (0.1, 0.3, -1.0)), (0.5, (0.1, 0.9, 0.3)), (1.0, (0.0, 0.0, 1.0)))), palm=keys(up, (0.0, (-1, 0, 0)), (1.0, (0.0, 1.0, 0.0))),
           elbow=(0.45, 0.10, -1.0), curl=blend_holds("relaxed", (0.10, 0.02, 0.02, 0.02, 0.03, 0.0, 0.0), up))
    hang(p, "L")
    p.head(pitch=-2 * body)
    return p


LECTERN = np.array([0.0, 0.40, 1.10])                 # the middle of the lectern's top
LECTERN_HANDS = 0.19


def podium_speak(t):
    # Speaking from a lectern, leaning on it a little: both hands flat on its top with the fingers over the far edge,
    # the elbows hanging below the shoulders, the weight going from foot to foot, the head going round the hall;
    # twice a loop a hand comes up to make a point (the right, an open palm turned up; then the left, an open hand
    # on edge, beating time).
    make = keys(t, (0.0, 0.0), (0.10, 0.0), (0.17, 1.0), (0.34, 1.0), (0.42, 0.0), (1.0, 0.0))
    count = keys(t, (0.0, 0.0), (0.58, 0.0), (0.65, 1.0), (0.78, 1.0), (0.86, 0.0), (1.0, 0.0))
    beat = s(lag(t, 0.01), 12)
    p = Pose()
    sway = idle(p, t, turns=2.0, width=0.16, amount=0.8, lean=9, shift=(0.0, 0.03))
    p.chest(pitch=3 + 1.5 * make + 1.0 * count, yaw=keys(t, (0.0, -6), (0.25, 7), (0.55, -3), (0.80, 6), (1.0, -6)), roll=1.2 * sway)
    for side, sign, lifted, fingers, palm, drop in (("R", 1.0, make, (-0.10, 0.88, 0.45), (-0.45, 0.20, 0.87), 0.85), ("L", -1.0, count, (0.10, 0.90, 0.40), (1.0, 0.0, 0.0), 0.75)):
        on = LECTERN + (sign * LECTERN_HANDS, -0.02, 0.006)
        resting = hand_turn(side, (-sign * 0.12, 0.96, -0.24), (0.0, 0.24, -0.97))
        raised = hand_turn(side, fingers, palm)
        turned = between(resting, raised, lifted)
        up = np.array([sign * 0.24, 0.42, 1.30 + 0.04 * beat * lifted])
        centre = on + (up - on) * lifted + (0.0, -0.05, 0.03) * np.array(math.sin(math.pi * lifted))
        p.hand(side, centre - turned @ PALM[side], turned=turned, elbow=(sign * 0.22, -0.45, -drop), curl=blend_holds((0.12, 0.10, 0.14, 0.18, 0.22, 0.05, 0.0), FLAT, lifted))
    p.head(yaw=keys(t, (0.0, -24), (0.20, -24), (0.34, 20), (0.52, 20), (0.66, -6), (0.84, 26), (1.0, -24)), pitch=keys(t, (0.0, -2), (0.45, -2), (0.52, 12), (0.58, -2), (1.0, -2)))
    return p


CLIPS = [
    Clip("litter_pick", 1.8, litter_pick, loop=False, marks={"grip": 1.1}, dressing=(("litter_pile", turn(), (0.05, 0.55, 0.0)),),
         note="picking litter off the ground with the bare right hand: a step forward with the left foot (0.27 m) and down, the trunk forward from the hips, the "
              "chest kept up, the left forearm along the thigh; the fingers close on something ON THE GROUND 0.55 m AHEAD (0.05 m right of the middle) at 1.1 s (the code switches "
              "the litter off then); back up to standing where one stood. Nothing is held: the code attaches no tool"),
    Clip("speech_gesture", 8.0, speech_gesture,
         note="a speaker to a crowd, standing (on soapbox: the feet stay within 0.36 x 0.26 m), the listeners 2.3 m ahead and 0.30 m lower, 1.15 m apart: "
              "the right forefinger up beside the head and jabbed forward (0.7 to 2.2 s), both arms opened wide palms up (3.3 to 4.4 s), the edge of the "
              "right hand down on the left palm three times (5.4 to 6.7 s), hands down; the head goes along the listeners. Needs 0.6 m clear either side"),
    Clip("oath_hand", 3.0, oath_hand, loop=False, mask="upper", marks={"raised": 0.6, "lower": 2.2},
         note="not asked for by code. The right hand raised beside the shoulder, forearm upright over the elbow (palm 1.54 m up, 0.40 m to the right, 0.22 m ahead), palm square to the front, fingers together, held from 0.6 to 2.2 s, lowered"),
    Clip("podium_speak", 8.0, podium_speak, mask="upper",
         note="not asked for by code. Leaning a little on a lectern 1.10 m up whose middle is 0.40 m ahead: the hands flat on its top 0.19 m either side, fingers over the far "
              "edge; the right hand comes up open to make a point at 1.4 s, the left at 5.2 s; the head goes round the audience"),
]
