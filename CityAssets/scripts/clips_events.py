"""Clips for fires and street events (Scripts/Events: the wish-list in RealLifeOverhaul.Build/README.md, "City events").
Same rules as clips.py.

Where things are, for whoever places the character (x right, y ahead, z up, from the character's feet):
    hose_hold        the nozzle's pivot (the back of its barrel) is 0.46 m ahead, 0.17 m right, 1.02 m up, pointing ahead and
                     12 degrees up: exactly where FireScene.StartWater stands it. The right palm is 0.06 m along it, the left
                     under the barrel 0.30 m along. The body is carried 0.15 m forward of the feet's point into a braced stance.
    pump_panel       the panel is 0.50 m ahead: the valve wheel 0.16 m right and 1.10 m up, the gauge on the centre line 1.40 m up
    keep_back        nothing: both palms 0.45 m ahead of the shoulders at 1.36 m; the look is over the RIGHT shoulder (the fire is behind)
    crowd_look_up    nothing: looks 50 degrees up, straight ahead; the left arm points up there once
    placard_hold     the staff's pivot is on the ground 0.27 m right, 0.22 m ahead (placard and banner_pole: their staff
                     starts 0.70 m up); the fist is 1.10 m up, pumped to 1.25 m twice a loop
    camera_operate   film_camera's pivot is 0.75 m AHEAD (StreetEventScene.FilmSet stands the operator 0.75 m behind it), both
                     facing the same way: the right eye at the eyecup (0.032 right, 0.16 ahead, 1.50 up), the right hand on
                     the pan bar, the left on the handle of the focus whip
"""
import math

import numpy as np

from motion import TAU, Clip, c, hang, idle, lag, s, shaft, shuffle, stand, weight  # noqa: F401
from rig import Grip, Pose, basis, blend_holds, ease, keys, turn, turn_about, unit, wave  # noqa: F401


def at_side(p, side):
    return p._hips[0] + p._hips[1] @ np.array([(1.0 if side == "R" else -1.0) * 0.235, 0.045, -0.045])


# ---------------------------------------------------------------------------------------------- the fire crew

NOZZLE_AT = np.array([0.17, 0.46, 1.02])              # FireScene.StartWater: forward 0.46, right 0.17, up 1.02
NOZZLE_RISE = 12.0                                    # degrees above level (Aim adds 0.22 to the slope)
NOZZLE_R = Grip("R", at=(0, 0.06, 0), thumb=(0, 1, 0), back=(1, 0, 0), hold="handle")       # on the black sleeve, from the right
HOSE_HIPS = 0.15                                      # how far the hips are carried forward of the feet's point
NOZZLE_L = Grip("L", at=(0, 0.30, 0), thumb=(0, 1, 0), back=(0, 0, -1), hold="handle")      # under the barrel, palm up


def hose_hold(t):
    # Braced against the reaction of the jet: left foot well forward, the right leg long behind, hips low and turned
    # with the left side leading, the trunk leaning in from the hips. The nozzle stays where the game stands it; twice
    # a loop the jet shoves the body back 4 cm on to the rear leg, whose knee gives, and it drives forward again; the
    # chest and the arms take it up a moment later.
    back = wave(t * 2, 0.05) ** 1.4
    late = wave(lag(t, 0.035) * 2, 0.05) ** 1.4
    shiver = 0.3 * s(t, 7, 0.1)
    p = Pose()
    p.hips((0.015 * s(t), HOSE_HIPS - 0.040 * back, 0.812 - 0.030 * back), yaw=26 + 2.0 * s(t), pitch=15 - 3.0 * back + 0.5 * shiver, roll=-1.5 + 1.0 * s(t))
    p.foot("L", (-0.14, 0.50, 0.0), yaw=-4)
    p.foot("R", (0.20, -0.17, 0.0), yaw=40)
    p.chest(yaw=10 - 3.0 * late, pitch=5 - 3.0 * late, roll=1.5)
    nozzle = (turn(yaw=0.8 * s(t), pitch=-NOZZLE_RISE - 1.5 * late - 0.4 * shiver), NOZZLE_AT + (0.0, -0.008 * late, 0.005 * late))
    p.tool(*nozzle, NOZZLE_R, NOZZLE_L, elbows={"R": (0.30, -0.90, -0.30), "L": (-0.55, -0.15, -0.8)})
    p.shrug("R", 0.15 + 0.2 * late).shrug("L", 0.2 + 0.25 * late)
    p.look_at((0.3 + 0.3 * s(t), 5.0, 1.5), limit=50)
    return p


VALVE = np.array([0.16, 0.50, 1.10])                  # the middle of a hand wheel on the pump panel (the panel is 0.50 m ahead)
GAUGE = np.array([0.00, 0.50, 1.40])


def pump_panel(t):
    # At the pump panel, leaning in from the hips: the right hand takes a hand wheel on the panel round once, the left
    # resting on the ledge under the panel; then the head tilts up to the gauge and the left forefinger reaches up and forward to
    # tap its glass twice; and round again.
    round_ = keys(t, (0.0, 0.0), (0.08, 0.0), (0.46, 1.0), (1.0, 1.0))
    read = keys(t, (0.0, 0.0), (0.42, 0.0), (0.54, 1.0), (0.86, 1.0), (0.96, 0.0), (1.0, 0.0))
    reach = keys(t, (0.0, 0.0), (0.48, 0.0), (0.62, 1.0), (0.80, 1.0), (0.94, 0.0), (1.0, 0.0))
    tap = max(0.0, s(keys(t, (0.62, 0.0), (0.80, 2.0)))) * (1.0 if 0.62 < t < 0.80 else 0.0)
    a = TAU * round_
    p = Pose()
    sway = idle(p, t, width=0.18, lean=9 - 3 * read, shift=(0.02, 0.10), amount=0.5, left=(0.0, 0.05))
    p.chest(pitch=4 - 3 * read, yaw=6 * (1 - read) - 4 * reach, roll=1.2 * sway)
    rim = VALVE + (0.075 * math.sin(a), 0.0, 0.075 * math.cos(a))
    p.hand("R", rim + (0.0, -0.075, 0.0), fingers=unit((-0.35 - 0.25 * math.cos(a), 0.9, 0.25 * math.sin(a))), palm=unit((-0.5 * math.sin(a), 0.25, -0.85 * math.cos(a) - 0.2)),
           elbow=(0.6, -0.5, -0.6), curl="bar")
    low = np.array([-0.21, 0.395, 0.99])                                           # the palm down on the ledge of the panel
    glass = GAUGE + (-0.055, -0.150 + 0.035 * tap, 0.035)
    p.hand("L", keys(reach, (0.0, low), (0.5, (-0.19, 0.37, 1.24)), (1.0, glass)), fingers=unit(keys(reach, (0.0, (0.15, 1.0, 0.05)), (1.0, (0.30, 0.95, 0.15)))),
           palm=keys(reach, (0.0, (0.0, 0.1, -1.0)), (1.0, (0.6, 0.0, -0.8))), elbow=(-0.5, -0.3, -0.85), curl=blend_holds("flat", "point", reach))
    p.look_at(VALVE * (1 - read) + (GAUGE + (0.0, 0.3, 0.38)) * read, down=30, up=20)
    return p


def keep_back(t):
    # "Stay back": a step towards whoever is coming too close, both arms out in front with the palms forward, two
    # thrusts at the air with the chest behind them, then a look right round over the right shoulder at the fire behind
    # (hips, chest and head all turn; the arms stay out), and the foot comes back.
    step = keys(t, (0.0, 0.0), (0.04, 0.0), (0.20, 1.0), (0.78, 1.0), (0.94, 0.0), (1.0, 0.0))
    up = math.sin(math.pi * keys(t, (0.04, 0.0), (0.20, 1.0))) + math.sin(math.pi * keys(t, (0.78, 0.0), (0.94, 1.0)))
    beat = keys(t, (0.18, 0.0), (0.48, 2.0))
    push = max(0.0, s(beat)) ** 1.5 * (1.0 if 0.18 < t < 0.48 else 0.0)
    late_beat = keys(lag(t, 0.03), (0.18, 0.0), (0.48, 2.0))
    late = max(0.0, s(late_beat)) ** 1.5 * (1.0 if 0.18 < lag(t, 0.03) < 0.48 else 0.0)
    look = keys(t, (0.0, 0.0), (0.50, 0.0), (0.62, 1.0), (0.76, 1.0), (0.90, 0.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.17, lean=3 + 3 * step + 5 * push, sway=0.45 - 0.85 * step, shift=(0.0, 0.14 * step + 0.02 * push), left=(-0.02, 0.34 * step), lift=(up, 0.0), yaw=12 * look, toe_out=10)
    p.chest(pitch=2 + 4 * late, yaw=24 * look, roll=-2 * look)
    p.head(yaw=66 * look - 4 * push, pitch=2 + 3 * push - 3 * look)
    seen = p.solve()
    for side, sign in (("L", -1.0), ("R", 1.0)):
        shoulder = seen.at[("Left" if side == "L" else "Right") + "UpperArm"]
        sink = 0.10 * look * (1.0 if side == "R" else 0.2)                           # the arm on the side they look round to sinks a little
        wrist = np.array([sign * 0.20, shoulder[1] + 0.410 + 0.085 * push, 1.36 - sink + 0.02 * push])
        p.hand(side, wrist, fingers=unit((sign * 0.10, 0.05 + 0.15 * (push - late), 1.0)), palm=(0.0, 1.0, -0.08), elbow=(sign * 0.35, -0.3, -0.9), curl="open")
        p.shrug(side, 0.3 * push)
    return p


def crowd_look_up(t):
    # Watching the sky: the weight on one leg, a little back lean, the head back looking 50 degrees up. The right hand
    # comes up along the body and is held as a peak 4 cm off the brow, edge to the forehead, elbow out and down; later
    # the weight goes over to the other leg and the left arm points up at one.
    shade = keys(t, (0.0, 0.0), (0.03, 0.0), (0.20, 1.0), (0.36, 1.0), (0.53, 0.0), (1.0, 0.0))
    point = keys(t, (0.0, 0.0), (0.53, 0.0), (0.68, 1.0), (0.80, 1.0), (0.95, 0.0), (1.0, 0.0))
    over = keys(t, (0.0, 0.85), (0.46, 0.85), (0.60, -0.85), (0.86, -0.85), (1.0, 0.85))      # where the weight is
    gaze = 9 * s(t, 1, 0.1) + 4 * s(t, 3)
    p = Pose()
    stand(p, width=0.16, lean=-6, sway=over + 0.08 * s(t, 3), shift=(0.0, 0.02), left=(-0.02, 0.05), toe_out=12)
    p.chest(pitch=-7, yaw=-2 * over + 0.3 * gaze - 5 * point, roll=1.5 * over)
    p.head(pitch=-38 - 2 * wave(t * 2), yaw=0.7 * gaze - 6 * point, roll=2 * over)
    seen = p.solve()                                                               # where that leaves the brow
    head, where = seen.turns["Head"], seen.at["Head"]
    brow = where + head @ np.array([0.030, 0.125, 0.100])
    rest = at_side(p, "R")
    peak = brow + head @ np.array([0.085, 0.050, 0.0]) + (0.0, 0.015, 0.0)          # the wrist out beside the temple; the hand reaches across in front of the brow
    flat = unit(head @ np.array([0.0, 0.0, -1.0]) * 0.45 + np.array([0.0, 0.0, -1.0]) * 0.55)      # nearer level than the tilted head: it shades
    p.hand("R", keys(shade, (0.0, rest), (0.45, (0.27, 0.19, 1.26)), (1.0, peak)), fingers=unit(keys(shade, (0.0, (0.1, 0.3, -1.0)), (0.45, (-0.25, 0.5, 0.8)), (1.0, head @ np.array([-1.0, 0.10, 0.0])))),
           palm=keys(shade, (0.0, (-1.0, 0.0, 0.0)), (0.45, (-0.8, -0.3, -0.5)), (1.0, flat)), elbow=(0.8, -0.25, -0.55), curl=blend_holds("relaxed", "flat", shade))
    p.shrug("R", 0.4 * shade)
    rest = at_side(p, "L")
    aim = unit((-0.22, 0.62, 0.76))                                                 # up at what the head looks at
    shoulder = seen.at["LeftUpperArm"]
    p.hand("L", keys(point, (0.0, rest), (0.45, (-0.27, 0.20, 1.20)), (1.0, shoulder + aim * 0.49)),
           fingers=unit(keys(point, (0.0, (-0.1, 0.3, -1.0)), (0.45, (-0.2, 0.7, 0.6)), (1.0, aim))), palm=keys(point, (0.0, (1.0, 0.0, 0.0)), (0.45, (0.9, 0.0, -0.4)), (1.0, (0.9, 0.2, -0.4))),
           elbow=(-0.7, -0.3, -0.6), curl=blend_holds("relaxed", "point", point))
    p.shrug("L", 0.6 * point)
    return p


# ---------------------------------------------------------------------------------------------- a march

STAFF = Grip("R", at=(0, 0, 1.10), thumb=(0, 0, 1), back=(1, 0, 0))            # the stop paddle's (clips.PADDLE): the same socket in the hand
STAFF_FOOT = np.array([0.27, 0.22, 0.0])                                       # the staff's pivot when the fist is 1.10 m up


def staff_up(p, t, sway=0.0):
    """The right arm of placard_hold: the staff upright in the fist, pumped up 0.15 m twice a loop; the elbow kept
    down by the ribs."""
    pump = wave(t * 2) ** 1.6
    late = wave(lag(t, 0.025) * 2) ** 1.6
    foot = STAFF_FOOT + (0.012 * sway, 0.02 * pump, 0.15 * pump)
    top = foot + unit(np.array([-0.035 + 0.01 * sway, 0.045 + 0.05 * (pump - late), 1.0]))      # the board lags the fist a touch
    p.tool(*shaft(foot, top, lean_to=(1.0, 0.0, 0.0), level=False), STAFF, elbows={"R": (0.18, -0.75, -0.65)})
    p.shrug("R", 0.35 * pump)
    return pump


def placard_hold(t):
    # A protester standing: the placard pumped up on the beat with a dip and a spring in the knees and the chest
    # lifting, the weight going over to the left leg as the left fist is punched up, the head up and calling out.
    p = Pose()
    beat = wave(t * 2) ** 1.6
    dip = wave(t * 2, 0.5) ** 2                                                    # down between the pumps
    punch = keys(t, (0.0, 0.0), (0.38, 0.0), (0.50, 1.0), (0.58, 1.0), (0.72, 0.0), (1.0, 0.0))
    sway = 0.5 * weight(t, shift=0.1) * (1 - punch) - 0.8 * punch
    stand(p, width=0.17, height=0.905 - 0.035 * dip + 0.012 * beat, lean=1 + 2 * dip, sway=sway, lift=(0.0, 0.5 * punch), toe_out=10)
    p.chest(pitch=1 + 2 * dip - 5 * beat, yaw=-3 * sway, roll=1.2 * sway)
    staff_up(p, t, sway)
    rest = np.array([-0.24, 0.20, 1.12])                                           # the left fist at the chest between times
    high = np.array([-0.32, 0.16, 1.78])
    p.hand("L", rest + (high - rest) * punch + np.array([-0.04, 0.08, 0.0]) * math.sin(math.pi * punch), fingers=unit(np.array([0.55, 0.45, 0.7]) * (1 - punch) + np.array([0.2, 0.0, 1.0]) * punch),
           palm=np.array([0.7, -0.5, -0.5]) * (1 - punch) + np.array([0.9, 0.4, 0.0]) * punch, elbow=(-0.8, -0.3, -0.5), curl="fist")
    p.shrug("L", 0.6 * punch)
    p.head(pitch=-5 - 6 * beat, yaw=keys(t, (0.0, -14), (0.30, -14), (0.48, 12), (0.80, 12), (1.0, -14)))
    return p


def placard_hold_arm(t):
    # The same right arm over a quiet body: for layering on the game's walk (mask "right_arm").
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.5)
    p.chest(pitch=1, roll=1.0 * sway)
    staff_up(p, t, sway)
    hang(p, "L", sway)
    p.head(pitch=-2)
    return p


# ---------------------------------------------------------------------------------------------- the film shoot

CAMERA = np.array([0.0, 0.75, 0.0])                                             # film_camera's pivot from the operator's feet
EYECUP = np.array([0.032, 0.16, 1.50])                                          # = the prop's (0.032, -0.59, 1.50)
EYE = np.array([0.032, 0.105, 0.082])                                           # the right eye from the head joint
PAN_AIM = unit((0.16, -0.45, -0.17))
PAN_GRIP = np.array([0.090, -0.005, 1.270]) + PAN_AIM * 0.435                   # as props_events.PAN_GRIP
FOCUS_KNOB = np.array([-0.135, -0.295, 1.41])
PAN = Grip("R", at=PAN_GRIP, thumb=-PAN_AIM, back=(0.85, 0.0, 0.5), hold="handle")
FOCUS = Grip("L", at=FOCUS_KNOB, thumb=(0, 1, 0), back=(-0.9, 0.0, 0.45), hold="bar")


def camera_operate(t):
    # At the camera: bent well forward at the hips with the legs nearly straight, feet in a short stride clear of the
    # tripod, the right eye at the eyecup and kept there while the weight goes from foot to foot behind it; the right
    # hand on the pan bar, the left working the focus handle a few degrees each way. Once a loop the chest comes up and
    # the head 0.11 m off the eyepiece to look at the scene over the camera, the left hand dropping to the thigh.
    off = keys(t, (0.0, 0.0), (0.58, 0.0), (0.68, 1.0), (0.82, 1.0), (0.92, 0.0), (1.0, 0.0))
    p = Pose()
    sway = 0.45 * weight(t, shift=0.05)
    stand(p, height=0.89, width=0.21, lean=22 - 5 * off, sway=sway, shift=(0.0, 0.0), left=(-0.03, -0.02), right=(0.02, -0.20), toe_out=14, yaw=2 * sway)
    p.chest(pitch=6 - 5 * off, yaw=-2 * sway - 4 * off, roll=1.5 * sway)
    p.head(pitch=-28 + 8 * off, yaw=-6 * off, roll=-1.5 * sway + 3 * off)
    seen = p.solve()                                                               # then the whole body is moved so that the eye is where it must be
    eye = seen.at["Head"] + seen.turns["Head"] @ EYE
    want = EYECUP + np.array([-0.020, -0.100, 0.045]) * off
    p._hips = (p._hips[0] + (want - eye), p._hips[1])
    wrist, hand = PAN.wrist(turn(), CAMERA)
    p.hand("R", wrist, turned=hand, elbow=(0.7, -0.6, -0.4), curl="handle")
    twist = turn_about((0, 1, 0), 16 * s(t, 2) * (1 - off))
    wrist, hand = FOCUS.wrist(twist, CAMERA + FOCUS_KNOB - twist @ FOCUS_KNOB)
    thigh = p._hips[0] + np.array([-0.21, 0.20, -0.02])                             # off the handle while looking up
    p.hand("L", wrist + (thigh - wrist) * off, fingers=unit(hand @ np.array([-1.0, 0.0, 0.0]) * (1 - off) + np.array([-0.1, 0.6, -0.8]) * off),
           palm=unit(hand @ np.array([0.0, 0.0, -1.0]) * (1 - off) + np.array([0.6, 0.0, -0.8]) * off), elbow=(-0.75, -0.5, -0.45), curl=blend_holds("bar", "relaxed", off))
    return p


CLIPS = [
    Clip("hose_hold", 4.0, hose_hold, tool="hose_nozzle", grips=(NOZZLE_R, NOZZLE_L), tool_length=0.42,
         note="braced behind a hose nozzle whose pivot is 0.46 m ahead, 0.17 m right and 1.02 m up, pointing ahead and 12 degrees up (where FireScene stands it): "
              "left foot forward, right palm 0.06 m along the nozzle, left under the barrel 0.30 m along; the jet's kick is taken up by the body"),
    Clip("pump_panel", 4.0, pump_panel, note="at a pump panel 0.50 m ahead: the right hand turns a hand wheel 0.16 m right, 1.10 m up, the left on a ledge 0.99 m up, then a look up at a gauge and two taps on its glass about 1.45 m up"),
    Clip("keep_back", 3.0, keep_back, note="both arms out, palms forward 0.45 m ahead of the shoulders at 1.36 m, a step forward with the left foot, two thrusts, a turn to look over the right shoulder (the fire is behind)"),
    Clip("crowd_look_up", 6.0, crowd_look_up, note="watching the sky 50 degrees up: the right hand shades the eyes from 1.2 to 2.2 s, the left arm points up from 4.1 to 4.8 s"),
    Clip("placard_hold", 4.0, placard_hold, tool="placard", grips=(STAFF,), tool_length=2.19,
         note="a protester standing: the staff (placard or banner_pole; the stop paddle's grip) upright in the right fist 1.10 m up, 0.27 m right and 0.22 m ahead, pumped up 0.15 m "
              "twice a loop; the left fist punches the air once a loop. The right arm alone (PlayOver, RightArm) is the marcher's"),
    Clip("placard_hold_arm", 4.0, placard_hold_arm, mask="right_arm", tool="placard", grips=(STAFF,), tool_length=2.19,
         note="placard_hold's right arm over a quiet body, listed for the right arm: the same grip and the same pumps"),
    Clip("camera_operate", 6.0, camera_operate, dressing=(("film_camera", turn(), tuple(CAMERA)),), watch="L",
         note="at film_camera, whose pivot is 0.75 m ahead facing the same way: right eye at the eyecup (0.16 m ahead, 1.50 m up), right hand on the pan bar, "
              "left on the focus whip's handle; the head comes off the eyepiece once a loop (3.5 to 5.5 s)"),
]
