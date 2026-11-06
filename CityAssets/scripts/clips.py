"""THE CLIPS: one function each, taking the moment t (0..1 through the clip) and returning a Pose (see rig.py).

This file holds the street workers, the people standing about, the counter and the handcuffs; the families added
since are in clips_scenes.py (accident and crime scenes), clips_jobs.py (the player's jobs) and clips_life.py (city
life), and CLIPS at the bottom is all of them. To add one: write a function (copy the nearest), add a Clip to its
file's list, then
    python -I check_clips.py <name>                 is everything within reach?
    blender -b -P build_clips.py -- <name>          look at previews/<name>.png  (--small for a smaller picture)
and when it reads right, build.ps1. Looping clips must come back to where they started: build them from wave(t),
sin and cos of whole turns of t, never from t itself.

How a figure is kept from looking like a robot is in motion.py (read its head): bend at the hips with a straight
back, weight from foot to foot, eased moves, the hand a moment behind the body.

Tools: a clip made for a tool names the prop, where it is at every moment (Pose.tool) and how the hands hold it
(Grip, with the way the fingers close: rig.HOLDS). The first Grip is the hand the game hangs the tool on; a second
Grip is the other hand, which the game keeps on the tool with its own two-bone solver. All props stand on the
ground with their length along z.
"""
import math
import os

import numpy as np

from motion import TAU, Clip, Walk, c, hang, idle, kneel, kneel_both, lag, s, shaft, shuffle, stance, stand, weight  # noqa: F401
from rig import Grip, Pose, basis, between, blend_holds, ease, keys, turn, turn_about, unit, wave  # noqa: F401

# ---------------------------------------------------------------------------------------------- street workers

BROOM_LOW = Grip("R", at=(0, 0, 1.04), thumb=(0, 0, 1), back=(1, 0, 0))
BROOM_TOP = Grip("L", at=(0, 0, 1.33), thumb=(0, 0, 1), back=(-1, 0, 0))


def sweep(t):
    # The broom slants across the front of the body: top hand by the chest, bristles on the ground ahead and to the
    # right. Each stroke draws them from the right across to the left; they come back just off the ground. The body
    # leads (weight from the right foot to the left, the hips and shoulders turning), the broom follows it.
    stroke = wave(t)                                  # 0 at the right, 1 at the left
    body = wave(t + 0.06)                             # the body is a moment ahead of the broom
    lift = max(0.0, s(t, shift=0.5)) ** 2 * 0.06
    head = np.array([0.36 - 0.40 * stroke, 0.78 + 0.10 * math.sin(math.pi * stroke), lift])
    top = np.array([-0.05 + 0.12 * stroke, 0.33 + 0.03 * s(t), 1.14 - 0.02 * math.sin(math.pi * stroke)])
    p = Pose()
    stand(p, height=0.90, width=0.17, yaw=8 - 14 * body, lean=22 + 3 * math.sin(math.pi * body), sway=0.55 - 1.1 * body, shift=(0.0, 0.02),
          left=(0.0, 0.10), right=(0.02, -0.09), lift=(0.0, 0.5 * body ** 2), toe_out=10)
    p.chest(yaw=5 - 15 * body, pitch=4, roll=2 - 4 * body)
    p.tool(*shaft(head, top), BROOM_LOW, BROOM_TOP, elbows={"R": (0.8, -0.5, -0.3), "L": (-0.8, -0.4, -0.2)})
    p.look_at(head + (0, 0.5, 0.1), down=16)
    return p


SHOVEL_TOP = Grip("R", at=(0, 0, 1.17), thumb=(0, 0, 1), back=(1, 0, 0), hold="bar")
SHOVEL_LOW = Grip("L", at=(0, 0, 0.84), thumb=(0, 0, 1), back=(-1, 0, 0))


def shovel_dig(t):
    # Drive the blade in ahead with the weight over the front foot, lever the handle down, stand up with the load
    # (legs, not back), turn and throw it off to the left, and come round again. Hinged at the hips throughout.
    drive = keys(t, (0.0, 0.0), (0.18, 1.0), (0.40, 1.0), (0.56, 0.0), (1.0, 0.0))
    lever = keys(t, (0.20, 0.0), (0.36, 1.0), (0.48, 0.6), (0.72, 0.6), (1.0, 0.0))
    carry = keys(t, (0.40, 0.0), (0.60, 1.0), (0.74, 1.0), (1.0, 0.0))
    throw = keys(t, (0.58, 0.0), (0.69, 1.0), (0.82, 0.0))
    tip = np.array([0.14 - 0.40 * carry - 0.16 * throw, 0.84 - 0.12 * carry + 0.08 * throw, 0.10 - 0.14 * drive + 0.34 * carry + 0.16 * throw])
    rise = math.radians(52 - 6 * lever - 8 * carry + 8 * throw)
    swing = math.radians(-4 + 24 * carry)                                # the handle end stays by the right hip
    top = tip + np.array([math.sin(swing) * math.cos(rise), -math.cos(swing) * math.cos(rise), math.sin(rise)])
    p = Pose()
    stand(p, height=0.90 - 0.03 * drive - 0.04 * lever + 0.03 * carry, width=0.19, yaw=8 - 16 * carry, lean=36 + 8 * drive + 4 * lever - 18 * carry,
          sway=-0.5 * drive - 0.4 * throw + 0.3 * lever, shift=(0.0, 0.10 + 0.05 * drive - 0.03 * lever), left=(-0.02, 0.20), right=(0.03, -0.14),
          lift=(0.0, 0.5 * drive * (1 - lever)), toe_out=12)
    p.chest(yaw=12 - 26 * carry - 8 * throw, pitch=5 + 3 * drive - 3 * carry, roll=3 - 5 * carry)
    p.tool(*shaft(tip, top), SHOVEL_TOP, SHOVEL_LOW, elbows={"R": (0.6, -0.6, -0.6), "L": (-0.8, -0.2, -0.4)})
    p.look_at(tip + (0, 0.3, 0.2), down=14)
    return p


HAMMER_R = Grip("R", at=(0.21, 0, 1.02), thumb=(-1, 0, 0), back=(0, 0, 1), hold="bar")
HAMMER_L = Grip("L", at=(-0.21, 0, 1.02), thumb=(1, 0, 0), back=(0, 0, 1), hold="bar")


def jackhammer(t):
    # Feet wide, knees soft, the trunk leaning on the machine from the hips: it kicks, and arms, shoulders and head
    # take the kick up one after the other.
    shake = 0.012 * s(t, 6) + 0.004 * s(t, 13)
    late = 0.012 * s(t, 6, 0.18)                      # what gets to the shoulders and head, a moment later
    sink = 0.02 * wave(t)
    foot = np.array([0.0, 0.47, shake * 0.6 - sink * 0.3])
    top = foot + unit(np.array([0.012 * s(t, 2), -0.11, 1.0]))
    p = Pose()
    stand(p, height=0.865 - sink * 0.5 + shake * 0.2, width=0.22, lean=17 + 2 * wave(t), sway=0.25 * s(t), shift=(0.0, 0.07), toe_out=16, left=(0, 0.04), right=(0, -0.05))
    p.chest(pitch=4 + late * 55, yaw=2 * s(t), roll=1.2 * s(t))
    p.tool(*shaft(foot, top, lean_to=(1.0, 0.0, 0.0)), HAMMER_R, HAMMER_L)
    p.shrug("L", 0.25 + late * 10).shrug("R", 0.25 - late * 10)
    p.look_at(foot + (0, 0.35, 0.1), down=18)
    p.head(pitch=late * 90)
    return p


PADDLE = Grip("R", at=(0, 0, 1.10), thumb=(0, 0, 1), back=(1, 0, 0))


def paddle_hold(t):
    # The paddle stands on the ground at the right; the left arm slows the traffic down: palm down, patting the air.
    pat = wave(t * 2)                                 # two pats a loop
    foot = np.array([0.31, 0.20, 0.0])
    p = Pose()
    sway = idle(p, t, width=0.17, yaw=-4, shift=(0.02, 0.0), amount=0.7)
    top = foot + unit(np.array([-0.02 + 0.012 * sway, -0.03, 1.0]))
    p.chest(yaw=-8 + 3 * sway, pitch=2, roll=1.5 * sway)
    p.tool(*shaft(foot, top, lean_to=(1.0, 0.0, 0.0)), PADDLE, elbows={"R": (0.7, -0.7, -0.2)})
    late = wave(lag(t, 0.03) * 2)
    p.hand("L", (-0.38, 0.30, 1.14 - 0.16 * pat), fingers=(-0.55, 0.8, 0.12 - 0.25 * (pat - late)), palm=(0, 0.15, -1), elbow=(-0.6, -0.5, -0.6), curl="open")
    p.head(yaw=-26 + 8 * s(t, shift=0.1), pitch=2 + 3 * pat)
    return p


def direct_traffic(t):
    # Left hand holds one stream (palm out, arm high), the right arm sweeps the other through, twice a loop; the
    # body turns from one stream to the other and the head goes first.
    beckon = wave(t * 2)
    turn_ = s(t)
    p = Pose()
    stand(p, width=0.20, yaw=7 * turn_, sway=0.5 * weight(t, shift=0.05), toe_out=14)
    p.chest(yaw=9 * turn_, pitch=1, roll=1.5 * turn_)
    p.hand("L", (-0.45, 0.36 + 0.015 * turn_, 1.40 + 0.02 * s(t, 2)), fingers=(-0.15, 0.1, 1), palm=(-0.45, 0.9, 0), elbow=(-0.7, -0.3, -0.6), curl="open")
    late = wave(lag(t, 0.04) * 2)
    wrist = keys(beckon, (0.0, (0.62, 0.20, 1.28)), (1.0, (0.22, 0.24, 1.52)))
    p.hand("R", wrist, fingers=unit(keys(late, (0.0, (1, 0.25, 0.0)), (1.0, (-0.8, 0.1, 0.6)))), palm=keys(late, (0.0, (0, 1, 0.2)), (1.0, (-0.3, -0.5, -0.8))),
           elbow=(0.9, -0.2, -0.3), curl=blend_holds("open", "relaxed", late))
    p.head(yaw=keys(t, (0.0, 34), (0.10, 34), (0.42, -34), (0.60, -34), (0.92, 34), (1.0, 34)), pitch=2)
    return p


SQUEEGEE = Grip("R", at=(0, 0, 0.07), thumb=(0, 0, 1), back=(0, -1, 0))
GLASS = 0.50                                         # metres in front of the feet
SQUEEGEE_LENGTH = 0.42                               # grip end to the rubber


def squeegee_wipe(t):
    # Two pulls down the glass side by side; between them the blade comes off the glass and goes back up. The knees
    # take the body down with the pull; the back stays straight and the eyes stay on the rubber.
    half, u = (0, t * 2) if t < 0.5 else (1, t * 2 - 1)
    x = (0.26 if half == 0 else 0.02) + 0.015 * s(u, 0.5)
    down = keys(u, (0.0, 0.0), (0.60, 1.0), (0.72, 1.0), (1.0, 0.0))
    off = keys(u, (0.60, 0.0), (0.78, 1.0), (0.92, 1.0), (1.0, 0.0))                 # off the glass on the way back up
    blade = np.array([x, GLASS - 0.06 * off, 1.62 - 0.52 * down])
    handle = blade + unit(np.array([0.06, -0.62, -0.78 + 0.30 * down])) * SQUEEGEE_LENGTH
    over = keys(t, (0.0, 0.5), (0.42, 0.5), (0.56, -0.3), (0.90, -0.3), (1.0, 0.5))  # the weight follows the blade across
    p = Pose()
    stand(p, height=0.905 - 0.11 * down, width=0.17, shift=(0.03 + x * 0.25, 0.04), yaw=6, lean=4 + 6 * down, sway=over, left=(0, 0.03), right=(0, -0.03))
    p.chest(pitch=1 + 3 * down, yaw=8 - x * 30, roll=2)
    p.tool(*shaft(handle, blade, lean_to=(1.0, 0.0, 0.0)), SQUEEGEE, elbows={"R": (0.8, -0.5, -0.4)})
    hang(p, "L", swing=0.3 * down)
    p.look_at(blade, down=20, up=30)
    return p


def kneel_work(t):
    # On one knee, the trunk straight and leaning in from the hip, both hands at something low in front: the right
    # turns and pulls, the left steadies; now and then the weight comes back and the head lifts.
    turn_ = TAU * t * 2
    back = keys(t, (0.0, 0.0), (0.62, 0.0), (0.74, 1.0), (0.88, 1.0), (1.0, 0.0))
    p = Pose()
    kneel(p, lean=30 - 9 * back + 1.5 * s(t, 2), forward=0.02 - 0.03 * back, roll=1.5 * s(t))
    p.chest(pitch=6 - 3 * back, yaw=5 + 3 * s(t, shift=0.2))
    away = np.array([0.0, -0.10, 0.10]) * back
    p.hand("R", away + (0.13 + 0.035 * math.cos(turn_) * (1 - back), 0.47 + 0.015 * math.sin(turn_), 0.46 + 0.035 * math.sin(turn_) * (1 - back)), fingers=(-0.35, 0.8, -0.5), palm=(-0.6, 0.0, -0.8),
           elbow=(0.8, -0.4, -0.2), curl="handle")
    p.hand("L", away * 0.5 + (-0.10, 0.46 + 0.006 * s(t, 2), 0.44), fingers=(0.35, 0.85, -0.4), palm=(0.5, 0.1, -0.85), elbow=(-0.8, -0.3, -0.3), curl="cup")
    p.look_at((0.03, 0.62, 0.36 + 0.5 * back), down=18)
    p.head(yaw=6 * s(t, shift=0.1))
    return p


def cpr(t):
    # On both knees beside someone lying across in front: shoulders over the hands, arms locked, the back flat, and
    # the whole trunk rocking down from the hips onto the heels of the hands, two compressions a loop.
    press = wave(t * 2) ** 0.8
    p = Pose()
    kneel_both(p, forward=-0.06 + 0.02 * press, height=0.0 - 0.012 * press, lean=46 + 6 * press, spread=0.16)
    p.chest(pitch=4)
    depth = 0.30 - 0.055 * press
    p.hand("L", (-0.015, 0.40, depth), fingers=(0.55, 0.8, 0.1), palm=(0, 0.05, -1), elbow=(-0.9, -0.2, 0.2), curl=0.2)
    p.hand("R", (0.03, 0.385, depth + 0.035), fingers=(-0.55, 0.8, 0.1), palm=(0, 0.05, -1), elbow=(0.9, -0.2, 0.2), curl="cup")
    p.shrug("L", 0.2 + 0.35 * press).shrug("R", 0.2 + 0.35 * press)
    p.look_at((0.0, 0.75, 0.25), down=8)
    return p


CLIPBOARD = Grip("L", at=(-0.075, -0.07, 0.004), thumb=(0.25, 1, 0), back=(0, 0, -1), hold="plate")


def clipboard_write(t):
    # Board on the left forearm, the right hand writing a line and moving down; a look up near the end.
    up = keys(t, (0.0, 0.0), (0.68, 0.0), (0.78, 1.0), (0.90, 1.0), (1.0, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.14, amount=0.6)
    board = (turn(yaw=8, pitch=-32, roll=-6), np.array([-0.03 + 0.02 * sway, 0.335, 1.07]))
    p.chest(pitch=3 - 2 * up, yaw=-3, roll=1.2 * sway)
    p.tool(*board, CLIPBOARD, elbows={"L": (-0.6, -0.7, -0.5)})
    line = (t * 3) % 1.0                              # three lines a loop
    pen = board[1] + board[0] @ np.array([-0.05 + 0.11 * line + 0.006 * s(t, 21), 0.07 - 0.05 * t + 0.004 * s(t, 14), 0.075 + 0.05 * up])
    p.hand("R", pen, fingers=(-0.62, 0.55, -0.55), palm=(-0.45, 0.25, -0.86), elbow=(0.75, -0.55, -0.4), curl="pen")
    p.head(yaw=-4 + 26 * up, pitch=24 - 24 * up)
    return p


# ---------------------------------------------------------------------------------------------- taking a breather (same tools, same grips)

def broom_rest(t):
    # Upright, both hands on the broom stood in front and some weight on it, looking up and down the street.
    p = Pose()
    sway = idle(p, t, width=0.16, lean=3, amount=0.9)
    foot = np.array([0.10 + 0.02 * sway, 0.40, 0.0])
    top = foot + unit(np.array([-0.06 + 0.02 * sway, -0.07, 1.0]))
    p.chest(pitch=2, yaw=-3 * sway, roll=2 * sway)
    p.tool(*shaft(foot, top), BROOM_LOW, BROOM_TOP, elbows={"R": (0.7, -0.6, -0.4), "L": (-0.7, -0.5, -0.5)})
    p.head(yaw=keys(t, (0.0, -32), (0.25, -32), (0.45, 30), (0.75, 30), (1.0, -32)), pitch=2)
    return p


def shovel_rest(t):
    # The shovel stood on its point in the right hand; the left forearm across the brow, then on the hip.
    wipe = keys(t, (0.0, 0.0), (0.12, 0.0), (0.26, 1.0), (0.50, 1.0), (0.64, 0.0), (1.0, 0.0))
    tip = np.array([0.27, 0.34, 0.0])
    p = Pose()
    sway = idle(p, t, width=0.16, amount=0.8)
    top = tip + unit(np.array([-0.03 + 0.012 * sway, -0.10, 1.0]))
    p.chest(pitch=2 - 4 * wipe, yaw=-2 * sway, roll=2 * sway)
    p.tool(*shaft(tip, top), SHOVEL_TOP, elbows={"R": (0.8, -0.5, -0.3)})
    hips = p._hips[0]
    hip, brow = hips + np.array([-0.235, 0.05, 0.075]), np.array([-0.04 + 0.07 * s(t, 3) * wipe, 0.165, 1.60])
    p.hand("L", hip + (brow - hip) * wipe + (0, 0.12 * math.sin(math.pi * wipe), 0), fingers=unit(keys(wipe, (0.0, (-0.2, 0.5, -0.85)), (1.0, (0.95, 0.1, 0.25)))),
           palm=keys(wipe, (0.0, (1, 0, 0)), (1.0, (0, 1, 0.1))), elbow=keys(wipe, (0.0, (-0.7, -0.7, 0.0)), (1.0, (-0.9, 0.2, -0.3))), curl="relaxed")
    p.head(yaw=10 * s(t) - 8 * wipe, pitch=2 - 9 * wipe)
    return p


def jackhammer_idle(t):
    # Stopped, hands still on the handles, straightening up off the machine and looking round.
    up = keys(t, (0.0, 0.0), (0.2, 0.0), (0.36, 1.0), (0.86, 1.0), (1.0, 0.0))
    foot = np.array([0.0, 0.47, 0.0])
    top = foot + unit(np.array([0.0, -0.11, 1.0]))
    p = Pose()
    stand(p, height=0.875 + 0.012 * up, width=0.22, lean=13 - 5 * up, sway=0.5 * weight(t), shift=(0.0, 0.07), toe_out=16, left=(0, 0.04), right=(0, -0.05))
    p.chest(pitch=3 - 2 * up, yaw=3 * s(t))
    p.tool(*shaft(foot, top, lean_to=(1.0, 0.0, 0.0)), HAMMER_R, HAMMER_L)
    p.head(yaw=keys(t, (0.0, 0), (0.2, 0), (0.36, 36), (0.6, 36), (0.76, -20), (0.9, -20), (1.0, 0)), pitch=keys(t, (0.0, 14), (0.2, 14), (0.36, -6), (0.9, -6), (1.0, 14)))
    return p


def squeegee_rest(t):
    # The squeegee lowered at the side, a hand on the hip, looking the glass over for streaks.
    p = Pose()
    sway = idle(p, t, width=0.16, yaw=6, shift=(0.03, 0.04), amount=0.9)
    hips = p._hips[0]
    grip = hips + np.array([0.28, 0.11, 0.05])
    blade = grip + unit(np.array([0.22, 0.50, -0.70])) * SQUEEGEE_LENGTH
    p.chest(pitch=1, yaw=4, roll=1.5 * sway)
    p.tool(*shaft(grip, blade, lean_to=(1.0, 0.0, 0.0), level=False), SQUEEGEE, elbows={"R": (0.8, -0.5, -0.2)})
    p.hand("L", hips + np.array([-0.235, 0.05, 0.075]), fingers=(-0.2, 0.5, -0.85), palm=(1, 0, 0), elbow=(-0.7, -0.7, 0.0), curl="relaxed")
    p.head(yaw=keys(t, (0.0, 14), (0.3, 14), (0.5, -16), (0.8, -16), (1.0, 14)), pitch=keys(t, (0.0, -14), (0.3, -14), (0.5, 4), (0.8, 4), (1.0, -14)))
    return p


COUNTER = np.array([0.04, 0.62, 0.975])              # where a cup stands on a counter in front: the top is 0.975 m up


def counter_wipe(t):
    # Behind a counter: the right hand wipes it down in slow ovals with the body going with it, the left rests on its edge.
    turn_ = TAU * t * 2
    p = Pose()
    stand(p, height=0.90, width=0.17, lean=14 + 2 * math.sin(turn_), sway=0.5 * math.cos(turn_ / 2), shift=(0.0, 0.13), left=(0, 0.05), right=(0, -0.05))
    p.chest(pitch=4, yaw=5 * math.cos(turn_ / 2), roll=1.5 * math.cos(turn_ / 2))
    late = turn_ - 0.25
    hand = np.array([0.10 + 0.14 * math.cos(late), 0.53 + 0.045 * math.sin(late), COUNTER[2] + 0.02])
    p.hand("R", hand, fingers=(-0.2, 0.95, 0.1), palm=(0, 0.1, -1), elbow=(0.8, -0.5, 0.1), curl=0.1)
    p.hand("L", (-0.23, 0.46, COUNTER[2] + 0.025), fingers=(0.3, 0.9, 0.1), palm=(0, 0.1, -1), elbow=(-0.8, -0.5, 0.0), curl="relaxed")
    p.look_at(hand + (0, 0.2, 0), down=16)
    return p


def serve(t):
    # Something handed over a counter: a lean from the hips, the right hand out palm up, a nod, and back.
    out = keys(t, (0.0, 0.0), (0.30, 1.0), (0.66, 1.0), (1.0, 0.0))
    arm = keys(t, (0.04, 0.0), (0.36, 1.0), (0.70, 1.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=11 * out, sway=-0.3 * out, shift=(0.0, 0.06 * out), left=(0, 0.04 * out))
    p.chest(pitch=2 + 2 * out, yaw=-5 * out)
    rest, over = p._hips[0] + np.array([0.235, 0.045, -0.045]), np.array([0.08, 0.60, 1.10])
    p.hand("R", rest + (over - rest) * arm + (0, 0, 0.08 * math.sin(math.pi * arm)), fingers=unit(keys(arm, (0.0, (0.1, 0.3, -1)), (1.0, (-0.15, 0.95, 0.15)))),
           palm=keys(arm, (0.0, (-1, 0, 0)), (1.0, (-0.2, 0, 1))), elbow=(0.7, -0.6, -0.4), curl=blend_holds("relaxed", "plate", arm))
    hang(p, "L")
    p.head(pitch=1 + 8 * max(0.0, s(t, 2, 0.15)) * out, yaw=-3 * out)
    return p


# ---------------------------------------------------------------------------------------------- handing things out

LEAFLET = Grip("R", at=(0, -0.045, -0.018), thumb=(-1, 0, 0), back=(0, 0, 1), hold="pinch")
STACK = Grip("L", at=(0.0, -0.01, -0.012), thumb=(0.3, 1, 0), back=(0, 0, -1), hold="plate")
STACK_AT = (turn(yaw=12, pitch=-18), np.array([-0.13, 0.30, 1.03]))


def leaflet_idle(t):
    # A handful of leaflets on the left palm, one ready in the right hand; looking up and down the pavement.
    p = Pose()
    sway = idle(p, t, width=0.15)
    p.chest(pitch=2, yaw=-2 * sway, roll=1.5 * sway)
    stack = (STACK_AT[0], STACK_AT[1] + (0.02 * sway, 0, 0))
    p.tool(*stack, STACK, elbows={"L": (-0.6, -0.7, -0.5)})
    sheet = (turn(yaw=-20 + 4 * sway, pitch=-12), np.array([0.14 + 0.02 * sway, 0.33 + 0.01 * s(t, 2), 1.06 + 0.01 * s(t, 2, 0.2)]))
    wrist, hand = LEAFLET.wrist(*sheet)
    p.hand("R", wrist, turned=hand, elbow=(0.7, -0.6, -0.5), curl="pinch")
    p.head(yaw=keys(t, (0.0, -30), (0.30, -30), (0.50, 28), (0.80, 28), (1.0, -30)), pitch=1)
    return p


def leaflet_offer(t):
    # A half step towards someone passing and the leaflet held out to them, then back.
    out = keys(t, (0.0, 0.0), (0.26, 1.0), (0.72, 1.0), (1.0, 0.0))
    arm = keys(t, (0.05, 0.0), (0.33, 1.0), (0.76, 1.0), (1.0, 0.0))
    step = math.sin(math.pi * keys(t, (0.0, 0.0), (0.22, 1.0), (0.74, 1.0), (1.0, 2.0)))
    p = Pose()
    stand(p, width=0.15, yaw=10 * out, lean=7 * out, sway=0.5 * out, shift=(0.0, 0.07 * out), right=(0.02 * out, 0.16 * out), lift=(0.0, abs(step)), turn_feet=(0, 8 * out))
    p.chest(pitch=2 + out, yaw=5 * out)
    wrist, hand = STACK.wrist(*STACK_AT)
    p.hand("L", wrist, turned=hand, elbow=(-0.6, -0.7, -0.5), curl="plate")
    sheet = (turn(yaw=-20 + 34 * arm, pitch=-12 + 4 * arm), np.array([0.14 + 0.16 * arm, 0.33 + 0.34 * arm, 1.06 + 0.10 * arm + 0.012 * arm * s(t, 3)]))
    p.tool(*sheet, LEAFLET, elbows={"R": (0.7, -0.6, -0.5)})
    p.head(yaw=14 * out, pitch=-2 - 3 * out)
    return p


# ---------------------------------------------------------------------------------------------- people together

def talk_a(t):
    # Explaining: the right hand comes up and beats time, the left joins for the point, then both rest.
    up = keys(t, (0.0, 0.0), (0.12, 1.0), (0.52, 1.0), (0.64, 0.0), (1.0, 0.0))
    both = keys(t, (0.26, 0.0), (0.36, 1.0), (0.50, 1.0), (0.62, 0.0))
    beat = s(t, 5) * up
    late = s(lag(t, 0.012), 5) * up
    p = Pose()
    sway = idle(p, t, width=0.15, lean=2 * up)
    p.chest(pitch=1 + 2 * up, yaw=-3 * sway, roll=1.5 * sway + 1.0 * beat)
    hips = p._hips[0]
    rest = hips + np.array([0.235, 0.045, -0.045])
    p.hand("R", rest + up * np.array([0.02, 0.27, 0.30 + 0.04 * beat]), fingers=unit(keys(up, (0.0, (0.1, 0.3, -1)), (1.0, (-0.25, 0.9, 0.35 + 0.25 * late)))),
           palm=keys(up, (0.0, (-1, 0, 0)), (1.0, (-0.5, 0.1, 0.85))), elbow=(0.6, -0.7, -0.4), curl=blend_holds("relaxed", "open", up))
    rest = hips + np.array([-0.235, 0.045, -0.045])
    p.hand("L", rest + both * np.array([-0.02, 0.25, 0.27 - 0.03 * beat]), fingers=unit(keys(both, (0.0, (-0.1, 0.3, -1)), (1.0, (0.25, 0.9, 0.35 - 0.2 * late)))),
           palm=keys(both, (0.0, (1, 0, 0)), (1.0, (0.5, 0.1, 0.85))), elbow=(-0.6, -0.7, -0.4), curl=blend_holds("relaxed", "open", both))
    p.head(yaw=4 * s(t, 2), pitch=2 + 5 * max(0.0, s(t, 4)) * up, roll=3 * s(t, 1, 0.1))
    return p


def talk_b(t):
    # Listening, mostly: nods, a shrug with open hands, a look away and back.
    shrug = keys(t, (0.40, 0.0), (0.50, 1.0), (0.62, 1.0), (0.74, 0.0))
    nod = max(0.0, s(t, 6)) * keys(t, (0.0, 1.0), (0.34, 1.0), (0.42, 0.0), (0.80, 0.0), (0.90, 1.0), (1.0, 1.0))
    p = Pose()
    sway = idle(p, t, width=0.16, phase=0.3)
    p.chest(pitch=1, yaw=3 * sway, roll=1.5 * sway)
    hips = p._hips[0]
    for side, sign in (("L", -1.0), ("R", 1.0)):
        rest = hips + np.array([sign * 0.235, 0.045, -0.045])
        p.hand(side, rest + shrug * np.array([sign * 0.10, 0.20, 0.16]), fingers=unit(keys(shrug, (0.0, (sign * 0.1, 0.3, -1)), (1.0, (sign * 0.45, 0.85, 0.1)))),
               palm=keys(shrug, (0.0, (-sign, 0, 0)), (1.0, (-sign * 0.3, 0.2, 0.95))), elbow=(sign * 0.6, -0.7, -0.4), curl=blend_holds("relaxed", "open", shrug))
        p.shrug(side, 0.8 * shrug)
    p.head(yaw=keys(t, (0.0, 0), (0.70, 0), (0.78, 24), (0.88, 24), (1.0, 0)), pitch=1 + 7 * nod, roll=4 * shrug)
    return p


def wave_hello(t):
    up = keys(t, (0.0, 0.0), (0.22, 1.0), (0.78, 1.0), (1.0, 0.0))
    swing = s(t, 3) * up
    late = s(lag(t, 0.03, loop=False), 3) * up
    p = Pose()
    stand(p, width=0.15, sway=-0.5 * up)
    p.chest(roll=-2 * up, pitch=1, yaw=4 * up)
    rest = p._hips[0] + np.array([0.235, 0.045, -0.045])
    high = np.array([0.42 + 0.11 * swing, 0.16, 1.60])
    p.hand("R", rest + (high - rest) * up, fingers=unit(keys(up, (0.0, (0.1, 0.3, -1)), (1.0, (0.32 * late, 0.05, 1)))), palm=keys(up, (0.0, (-1, 0, 0)), (1.0, (0, 1, 0))),
           elbow=(0.9, -0.2, -0.5), curl=blend_holds("relaxed", "open", up))
    hang(p, "L", sway=-0.5 * up)
    p.head(pitch=-3 * up, roll=3 * up)
    return p


def clap(t):
    # Four claps a loop, the hands a little to the left of centre, the body keeping time.
    together = wave(t * 4) ** 0.6
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.7)
    p.chest(pitch=2, roll=1.5 * sway)
    gap = 0.19 * (1 - together)
    p.hand("R", (0.035 + gap, 0.30, 1.20 + 0.01 * together), fingers=(-0.35, 0.75, 0.55), palm=(-1, 0.1, 0), elbow=(0.7, -0.5, -0.6), curl=0.05)
    p.hand("L", (-0.035 - gap, 0.30, 1.19), fingers=(0.35, 0.75, 0.55), palm=(1, 0.1, 0), elbow=(-0.7, -0.5, -0.6), curl=0.05)
    p.head(pitch=1 + 2 * together, yaw=3 * sway)
    return p


def watch_nod(t):
    # Standing listening to music: weight from foot to foot, the head keeping time, a glance aside.
    beat = wave(t * 8)
    p = Pose()
    sway = idle(p, t, width=0.15, height=0.905 - 0.006 * beat)
    p.chest(yaw=-3 * sway, roll=2.5 * sway, pitch=1)
    hang(p, "L", sway, 0.15 * s(t, 2))
    hang(p, "R", sway, -0.15 * s(t, 2))
    p.head(pitch=1 + 4 * beat, yaw=keys(t, (0.0, 0), (0.55, 0), (0.64, -22), (0.78, -22), (0.88, 0), (1.0, 0)), roll=2 * sway)
    return p


SMOKE = Grip("R", at=(0, -0.070, -0.022), thumb=(-1, 0, 0), back=(0, 0, 1), hold="pinch")


def smoke(t):
    # The cigarette down at the side, up to the mouth for a drag, down again, and the smoke blown up and away.
    up = keys(t, (0.0, 0.0), (0.30, 0.0), (0.42, 1.0), (0.56, 1.0), (0.68, 0.0), (1.0, 0.0))
    blow = keys(t, (0.62, 0.0), (0.72, 1.0), (0.84, 1.0), (0.94, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.15)
    p.chest(yaw=-3 * sway, pitch=1 - 2 * blow, roll=1.5 * sway)
    low, high = p._hips[0] + np.array([0.26, 0.09, -0.01]), np.array([0.105, 0.150, 1.475])
    p.hand("R", low + (high - low) * up + (0, 0.10 * math.sin(math.pi * up), 0), fingers=unit(keys(up, (0.0, (0.15, 0.55, -0.8)), (1.0, (-0.72, 0.05, 0.68)))),
           palm=keys(up, (0.0, (-0.9, 0.1, -0.4)), (1.0, (-0.45, -0.75, -0.5))), elbow=(0.45, -0.3, -0.9), curl="pinch")
    hang(p, "L", sway)
    p.head(yaw=6 * sway + 12 * blow, pitch=1 - 3 * up - 12 * blow)
    return p


# The phone upright (portrait), as people hold it to film or take a picture of themselves: the palm on its back, the
# fingers up it and a little across, the thumb along its side. Two hands for filming, the lower corners each; the
# right one alone for a selfie. Held from below like this the wrists are under the phone, so with the screen at eye
# level the elbows still hang down in front of the chest: the game's character has short arms (0.50 m from shoulder to
# wrist), and a phone held by its ends at eye level puts both elbows up level with the shoulders and out to the sides.
PHONE_HOLD = (0.28, 0.10, 0.12, 0.15, 0.20, 0.45, 0.15)   # fingers nearly straight along the back (it is 7 cm across), the thumb on the edge
PHONE_R = Grip("R", at=(0.030, -0.045, -0.014), thumb=(1.0, 0.12, 0), back=(0, 0, -1), hold=PHONE_HOLD)
PHONE_L = Grip("L", at=(-0.030, -0.045, -0.014), thumb=(-1.0, 0.12, 0), back=(0, 0, -1), hold=PHONE_HOLD)
SELFIE = Grip("R", at=(0.014, -0.030, -0.014), thumb=(0.82, 0.57, 0), back=(0, 0, -1), hold=PHONE_HOLD)
EYES = np.array([0.0, 0.10, 1.555])                  # where the eyes are, standing (the head joint 0.07 forward and 0.08 up)


def facing(towards, top=(0.0, 0.0, 1.0)):
    """A turn for the phone with its screen (+Z) towards `towards` and its top (+Y) as near `top` as that allows."""
    z = unit(towards)
    y = np.asarray(top, dtype=float)
    y = unit(y - z * z.dot(y))
    return np.stack([np.cross(y, z), y, z], axis=1)


def take_photo(t):
    # Filming, or a picture of something ahead: the phone comes up in both hands to eye level, a hand's length short
    # of the face (0.30 m) with the screen to the eyes, the elbows down in front of the chest; it is framed (a small
    # turn, a step of the head), held still for the shot, and lowered back to the chest to look at what was taken.
    up = keys(t, (0.0, 0.0), (0.20, 1.0), (0.82, 1.0), (1.0, 0.0))
    frame = keys(t, (0.22, 0.0), (0.36, 1.0), (0.50, -0.4), (0.60, 0.0))
    p = Pose()
    stand(p, height=0.905 - 0.010 * up, width=0.16, yaw=5 * frame, shift=(0.0, 0.01 * up), sway=0.4 * frame)
    p.chest(pitch=2 - 3 * up, yaw=3 * frame)
    low, high = np.array([0.0, 0.31, 1.17]), np.array([0.0, 0.40, 1.545])
    at = low + (high - low) * up + np.array([0.0, 0.03, 0.0]) * math.sin(math.pi * up)      # out a little on the way up
    aim = turn(yaw=6 * frame) @ (EYES - high)
    screen = between(facing(EYES - low, top=(0.0, 1.0, 0.35)), facing(aim), up)
    p.tool(screen, at, PHONE_R, PHONE_L, elbows={"R": (0.25, -0.25, -1.0), "L": (-0.25, -0.25, -1.0)})
    p.look_at(at + (0.0, 0.0, 0.02), down=40)
    return p


def selfie(t):
    # A picture of oneself with whatever is behind: the right arm takes the phone up and out to its length (0.42 m
    # from the eyes, 40 degrees round to the right and 15 up), the screen turned to the face; the head turns up into
    # it and tips to one side, the right shoulder rises with the arm and the weight goes on to the left foot. Held for
    # the picture (a second one with the phone moved a little and the head tipped the other way), then lowered to the
    # chest for a look at it. The left arm hangs. Played with the back to what is to be in the picture.
    out = keys(t, (0.0, 0.0), (0.06, 0.0), (0.24, 1.0), (0.80, 1.0), (0.94, 0.0))
    tip = keys(t, (0.26, 0.0), (0.34, 1.0), (0.52, 1.0), (0.60, -0.6), (0.76, -0.6), (0.84, 0.0))
    shift = keys(t, (0.44, 0.0), (0.54, 1.0), (0.80, 1.0), (0.90, 0.0))          # the phone moved a little for the second picture
    p = Pose()
    stand(p, height=0.905 - 0.006 * out, width=0.16, yaw=-4 * out, sway=-0.45 * out, toe_out=10)
    p.chest(yaw=-3 * out, pitch=1 - 4 * out, roll=-2 * out)
    p.shrug("R", 0.30 * out)
    low = np.array([0.10, 0.30, 1.20])
    high = EYES + turn(yaw=40 - 6 * shift, pitch=-(15 + 3 * shift)) @ np.array([0.0, 0.42, 0.0])
    at = low + (high - low) * out + np.array([0.04, 0.04, 0.03]) * math.sin(math.pi * out)
    screen = between(facing(EYES - low, top=(-0.15, 1.0, 0.35)), facing(EYES - high, top=(-0.06, 0.0, 1.0)), out)
    p.tool(screen, at, SELFIE, elbows={"R": (0.7, -0.15, -1.0)})
    hang(p, "L", -0.45 * out)
    p.look_at(at, down=40)
    p.head(roll=7 * tip * out)
    return p


# ---------------------------------------------------------------------------------------------- at a counter, with a cup

CUP = Grip("R", at=(0.037, 0, 0.060), thumb=(0, 0, 1), back=(1, 0, 0), hold="cup")


def cup_at(position, tilt=0.0):
    return (turn(pitch=-tilt, yaw=-10), np.asarray(position, dtype=float))


def counter_reach(t):
    # Leans in from the hips and puts the right hand round a cup standing on the counter ahead, and comes back with it (or without).
    out = keys(t, (0.0, 0.0), (0.36, 1.0), (0.56, 1.0), (1.0, 0.0))
    arm = keys(t, (0.05, 0.0), (0.40, 1.0), (0.58, 1.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.15, lean=20 * out, sway=0.3 * out, shift=(0.0, 0.10 * out), right=(0, 0.05 * out))
    p.chest(pitch=2 + 3 * out, yaw=-8 * out)
    held = np.array([0.20, 0.30, 1.10])
    p.tool(*cup_at(held + (COUNTER - held) * arm + (0, 0, 0.06 * math.sin(math.pi * arm))), CUP, elbows={"R": (0.7, -0.6, -0.4)})
    hang(p, "L")
    p.look_at(COUNTER + (0, 0.1, 0.05), down=18)
    return p


def cup_hold(t):
    # A cup held at the chest: weight from foot to foot, a look at it and a look about.
    p = Pose()
    sway = idle(p, t, width=0.15)
    p.chest(pitch=1, yaw=-2 * sway, roll=1.5 * sway)
    p.tool(*cup_at((0.20 + 0.03 * sway, 0.30, 1.10 + 0.008 * s(t, 2))), CUP, elbows={"R": (0.7, -0.6, -0.4)})
    hang(p, "L", sway)
    p.head(yaw=keys(t, (0.0, 0), (0.30, 0), (0.42, -26), (0.62, -26), (0.74, 18), (0.90, 18), (1.0, 0)), pitch=keys(t, (0.0, 10), (0.25, 10), (0.36, 0), (0.92, 0), (1.0, 10)))
    return p


def sip(t):
    up = keys(t, (0.0, 0.0), (0.30, 1.0), (0.62, 1.0), (1.0, 0.0))
    tip = keys(t, (0.26, 0.0), (0.42, 1.0), (0.58, 1.0), (0.70, 0.0))
    p = Pose()
    stand(p, width=0.15)
    p.chest(pitch=1 - 3 * tip)
    low, high = np.array([0.20, 0.30, 1.10]), np.array([0.02, 0.200, 1.405])
    p.tool(*cup_at(low + (high - low) * up + (0, 0.08 * math.sin(math.pi * up), 0.02 * tip), tilt=30 * tip), CUP, elbows={"R": (0.5, -0.3, -0.9)})
    hang(p, "L")
    p.head(pitch=4 - 4 * up - 14 * tip)
    return p


# ---------------------------------------------------------------------------------------------- in handcuffs

def cuffed(p, t, sway):
    for side, sign in (("L", -1.0), ("R", 1.0)):
        at = p._hips[0] + p._hips[1] @ np.array([sign * 0.045, -0.118, 0.03 + 0.004 * sway * sign])
        p.hand(side, at, fingers=p._hips[1] @ np.array([-sign * 0.55, -0.25, -0.8]), palm=p._hips[1] @ np.array([0, -1, 0.1]), elbow=p._hips[1] @ np.array([sign * 0.8, -0.6, 0.1]), curl=0.5)
        p.shrug(side, -0.2)


def cuffed_stand(t):
    p = Pose()
    sway = idle(p, t, width=0.14, lean=3, amount=0.8)
    p.chest(pitch=4, yaw=-2 * sway, roll=1.5 * sway)
    cuffed(p, t, sway)
    p.head(pitch=24 + 3 * s(t, 2), yaw=keys(t, (0.0, 0), (0.6, 0), (0.7, 16), (0.86, 16), (1.0, 0)))
    return p


CUFFED_WALK = Walk(1.2, 1.1)                          # the game's walking pace


def cuffed_walk(t):
    # Walking on the spot with the hands held behind the back: whoever moves the character sets the pace.
    p = Pose()
    swing = CUFFED_WALK.body(p, t, lean=5, yaw=4)
    p.chest(pitch=3, yaw=-6 * swing, roll=-1.5 * s(t, shift=0.02))
    cuffed(p, t, s(t))
    p.head(pitch=20, yaw=3 * swing)
    return p


# ---------------------------------------------------------------------------------------------- the list

BASE_CLIPS = [
    Clip("sweep", 2.4, sweep, tool="broom", grips=(BROOM_LOW, BROOM_TOP), tool_length=1.45, note="street broom, both hands; sweeps across the front, 0.84 m ahead"),
    Clip("shovel_dig", 3.6, shovel_dig, tool="shovel", grips=(SHOVEL_TOP, SHOVEL_LOW), tool_length=1.25, note="digs 0.8 m ahead, throws the load off to the left"),
    Clip("jackhammer", 1.2, jackhammer, tool="jackhammer", grips=(HAMMER_R, HAMMER_L), tool_length=1.05, note="the bit is 0.47 m ahead of the feet"),
    Clip("paddle_hold", 4.0, paddle_hold, tool="stop_paddle", grips=(PADDLE,), tool_length=1.9, note="paddle stood on the ground at the right; left hand slows traffic"),
    Clip("direct_traffic", 3.2, direct_traffic, note="left hand holds one stream, right arm waves the other on"),
    Clip("squeegee_wipe", 4.0, squeegee_wipe, tool="squeegee", grips=(SQUEEGEE,), tool_length=SQUEEGEE_LENGTH, note="the glass is %.2f m ahead of the feet" % GLASS),
    Clip("kneel_work", 3.0, kneel_work, note="right knee down, hands working 0.45 m ahead at knee height"),
    Clip("cpr", 1.1, cpr, note="both knees down; the chest being pressed is 0.40 m ahead, 0.25 m up"),
    Clip("clipboard_write", 4.5, clipboard_write, tool="clipboard", grips=(CLIPBOARD,), tool_length=0.3, note="board in the left hand, writing with the right"),
    Clip("leaflet_idle", 6.0, leaflet_idle, tool="leaflet_stack", grips=(STACK,), tool_length=0.1, note="leaflets on the left palm; looks up and down the street"),
    Clip("leaflet_offer", 2.6, leaflet_offer, loop=False, tool="leaflet", grips=(LEAFLET,), tool_length=0.15, note="holds one out 0.65 m ahead; the stack stays in the left hand"),
    Clip("talk_a", 6.0, talk_a, note="talking, with the hands; face the other person"),
    Clip("talk_b", 6.4, talk_b, note="listening: nods, a shrug, a look away"),
    Clip("wave_hello", 2.2, wave_hello, loop=False, note="right arm up, three waves"),
    Clip("clap", 2.0, clap, note="four claps a loop"),
    Clip("watch_nod", 6.4, watch_nod, note="listening to music, arms down"),
    Clip("smoke", 9.0, smoke, tool="cigarette", grips=(SMOKE,), tool_length=0.084, note="a drag every nine seconds"),
    Clip("take_photo", 5.0, take_photo, loop=False, tool="phone", grips=(PHONE_R, PHONE_L), tool_length=0.15, note="phone upright in both hands, up to eye level 0.30 m ahead, held, lowered"),
    Clip("selfie", 5.0, selfie, loop=False, tool="phone", grips=(SELFIE,), tool_length=0.15, note="phone up and out at arm's length in the right hand, above the eyes, screen to the face; two pictures, lowered"),
    Clip("counter_reach", 2.4, counter_reach, loop=False, tool="coffee_cup", grips=(CUP,), tool_length=0.12, marks={"grip": 0.9},
         note="the right hand goes to a cup on a counter 0.62 m ahead and 0.975 m up, and back: put the cup in or take it out of the hand half way"),
    Clip("cup_hold", 7.0, cup_hold, tool="coffee_cup", grips=(CUP,), tool_length=0.12, note="a cup held at the chest"),
    Clip("sip", 3.4, sip, loop=False, tool="coffee_cup", grips=(CUP,), tool_length=0.12, note="a drink from the cup, from and back to cup_hold"),
    Clip("broom_rest", 7.0, broom_rest, tool="broom", grips=(BROOM_LOW, BROOM_TOP), tool_length=1.45, note="leaning on the broom; alternate with sweep"),
    Clip("shovel_rest", 7.0, shovel_rest, tool="shovel", grips=(SHOVEL_TOP,), tool_length=1.25, note="shovel stood in the right hand, the left wipes the brow; alternate with shovel_dig"),
    Clip("jackhammer_idle", 6.0, jackhammer_idle, tool="jackhammer", grips=(HAMMER_R, HAMMER_L), tool_length=1.05, note="stopped, looking round; alternate with jackhammer"),
    Clip("squeegee_rest", 6.0, squeegee_rest, tool="squeegee", grips=(SQUEEGEE,), tool_length=SQUEEGEE_LENGTH, note="squeegee down at the side, looking the glass over"),
    Clip("counter_wipe", 3.6, counter_wipe, note="wiping a counter 0.55 m ahead and 0.975 m up"),
    Clip("serve", 2.2, serve, loop=False, note="hands something over a counter"),
    Clip("cuffed_stand", 5.0, cuffed_stand, note="hands behind the back, head down"),
    Clip("cuffed_walk", 1.1, cuffed_walk, speed=1.2, note="walking on the spot, hands behind the back: made for 1.2 m/s, the game's walking pace"),
]

CLIPS = list(BASE_CLIPS)
# The later rounds are clips_<name>.py, each with its own list CLIPS. CLIPS_DEV=name[,name] loads only those of them
# (working on one module without the others having to be whole).
EXTRA = ["events", "duty", "everyday", "garage", "family", "site", "build"]
_only = os.environ.get("CLIPS_DEV")
_extra = [n.strip() for n in _only.split(",") if n.strip()] if _only else EXTRA
for _module in ("clips_scenes", "clips_jobs", "clips_life") + tuple("clips_" + n for n in _extra):
    try:
        CLIPS += __import__(_module).CLIPS
    except ModuleNotFoundError as _missing:
        if _missing.name != _module:
            raise
_names = [clip.name for clip in CLIPS]
assert len(_names) == len(set(_names)), "a clip is listed twice: " + ", ".join(sorted(n for n in set(_names) if _names.count(n) > 1))
