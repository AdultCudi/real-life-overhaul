"""Clips for the accident and crime scenes (People/, Traffic/, Crime/): wheeling the stretcher, lifting a casualty,
carrying the bag, hands up, the radio, the notebook, sending people on. Same rules as clips.py.

Where things are, for whoever places the character (x right, y ahead, z up, from the character's feet):
    push_cart / pull_cart    the rail is 0.44 m ahead, 0.95 m up, the hands 0.45 m apart (stretcher_push_bar, pushchair)
    lift_patient             the casualty lies across in front, 0.42 m ahead; the hands end 0.30 m ahead and 0.88 m up (the bed is 0.70)
"""
import math

import numpy as np

from motion import Clip, Walk, c, hang, idle, kneel, lag, s, stand, weight  # noqa: F401
from rig import Grip, Pose, blend_holds, ease, keys, turn, unit, wave  # noqa: F401

RAIL = np.array([0.0, 0.44, 0.95])                    # the middle of a push rail from the pusher's feet
RAIL_HALF = 0.225                                     # half the distance between the hands


def on_rail(p, lift=0.0, sway=0.0):
    """Both hands closed over a horizontal rail in front, palms down, elbows a little out."""
    for side, sign in (("L", -1.0), ("R", 1.0)):
        palm = RAIL + (sign * RAIL_HALF + sway, 0.0, lift)
        wrist = palm + (sign * 0.012, -0.072, 0.040)                # the wrist is behind and above the bar the fingers go over
        p.hand(side, wrist, fingers=(-sign * 0.10, 0.93, -0.34), palm=(0.0, 0.34, -0.94), elbow=(sign * 0.75, -0.5, -0.45), curl="bar")


PUSH = Walk(1.2, 1.1)


def push_cart(t):
    # Walking behind something on wheels: both hands on its rail, the weight a little forward into it. The hands
    # stay where the rail is while the body bobs and turns behind them.
    p = Pose()
    swing = PUSH.body(p, t, lean=8, yaw=3, roll=1.5, sway=0.008, bob=0.018)
    p.chest(pitch=3, yaw=-4 * swing, roll=-1.0 * s(t, shift=0.02))
    on_rail(p)
    p.head(pitch=-6, yaw=2 * swing)
    return p


PULL = Walk(-0.9, 1.1, duty=0.66, lift=0.06)


def pull_cart(t):
    # Walking backwards, drawing it after: both hands on the rail in front, the weight back, short careful steps,
    # the head turned to see where the feet are going.
    p = Pose()
    swing = PULL.body(p, t, lean=-3, yaw=3, roll=1.5, sway=0.008, bob=0.014, height=0.885)
    p.chest(pitch=0, yaw=-8 + 3 * swing)
    on_rail(p)
    p.head(yaw=-38, pitch=10)
    return p


def lift_patient(t):
    # One of two people lifting together: down on the right knee with the hands under the casualty, the back flat,
    # then up with the legs to standing with the load at the height of a stretcher's bed. Ends holding (ClipEnd.Hold).
    under = keys(t, (0.0, 0.0), (0.22, 1.0), (1.0, 1.0))              # the hands go under and take the weight
    up = keys(t, (0.26, 0.0), (0.86, 1.0), (1.0, 1.0))                # the lift itself
    settle = math.sin(math.pi * keys(t, (0.80, 0.0), (1.0, 1.0)))     # a small give at the top
    p = Pose()
    hips = np.array([0.0, -0.05 + 0.02 * under + 0.10 * up, 0.475 - 0.02 * under * (1 - up) + (0.895 - 0.475) * up - 0.012 * settle])
    p.hips(hips, pitch=20 + 16 * under - 28 * up, roll=-1.5 * up)
    # The right leg comes from kneeling (shin on the ground, toes tucked) to standing a little behind.
    ankle = keys(up, (0.0, (0.13, -0.33, 0.168)), (0.5, (0.15, -0.26, 0.21)), (1.0, (0.16, -0.12, 0.126)))
    p.foot("R", ankle=ankle, pitch=80 * (1 - ease(min(1.0, up * 1.6))), toes=-55 * (1 - ease(min(1.0, up * 1.6))), yaw=8 * up,
           knee=unit(np.array([0.0, 0.8, -0.6]) * (1 - up) + np.array([0.1, 1.0, 0.2]) * up))
    p.foot("L", (-0.18, 0.22, 0.0), yaw=-8, knee=(-0.2, 0.6, 0.8 - 0.6 * up))
    p.chest(pitch=6 - 4 * up)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        low = np.array([sign * 0.20, 0.32 + 0.12 * under, 0.44 - 0.06 * under])
        high = np.array([sign * 0.20, 0.30, 0.88 - 0.012 * settle])
        p.hand(side, low + (high - low) * min(1.0, up * 1.3), fingers=(-sign * 0.25, 0.95, 0.05), palm=(0.0, -0.1, 1.0), elbow=(sign * 0.7, -0.6, -0.4), curl=blend_holds("relaxed", "plate", under))
    p.look_at((0.0, 0.55, 0.30 + 0.60 * up), down=22)
    return p


BAG = Grip("R", at=(0.0, 0.0, 0.40), thumb=(-1, 0, 0), back=(0, 1, 0), hold="handle")
BAG_WALK = Walk(1.3, 1.08)


def carry_bag_walk(t):
    # A brisk walk with a heavy bag in the right hand: that arm hangs straight and hardly swings, the body leans a
    # little away from the load, the other arm swings wide to balance.
    p = Pose()
    swing = BAG_WALK.body(p, t, lean=5, yaw=5, roll=2.0, bob=0.022)
    p.chest(pitch=2, yaw=-7 * swing, roll=-2.5)
    late = c(lag(t, 0.06))
    hips = p._hips[0]
    bag = (turn(yaw=90 + 4 * late, roll=0), hips + np.array([0.275, 0.03 - 0.05 * late, -0.905 + 0.375 + 0.01 * wave(t * 2)]))
    p.tool(*bag, BAG, elbows={"R": (0.5, -1.0, 0.0)})
    p.shrug("R", -0.5)
    BAG_WALK.arm(p, "L", t, amount=1.25)
    p.head(pitch=2, yaw=2 * swing)
    return p


def raised(p, up, tremble=0.0, sway=0.0):
    """Both hands up beside the head, palms forward, fingers spread."""
    hips = p._hips[0]
    for side, sign in (("L", -1.0), ("R", 1.0)):
        rest = hips + np.array([sign * 0.235, 0.045, -0.045])
        high = np.array([sign * (0.44 + 0.01 * tremble) + 0.02 * sway, 0.12, 1.60 + 0.012 * tremble * sign])
        lifted = keys(up, (0.0, rest), (0.5, rest + (high - rest) * 0.5 + (sign * 0.10, 0.14, 0.0)), (1.0, high))
        p.hand(side, lifted, fingers=unit(keys(up, (0.0, (sign * 0.1, 0.3, -1)), (0.5, (sign * 0.3, 0.8, 0.5)), (1.0, (sign * 0.12, 0.05, 1)))),
               palm=keys(up, (0.0, (-sign, 0, 0)), (1.0, (0, 1, 0.05))), elbow=(sign * 0.9, -0.25, -0.5), curl=blend_holds("relaxed", "open", up))
        p.shrug(side, 0.5 * up)


def hands_up(t):
    # Caught: a start (the body goes back onto the heels), and both hands go up, the right a moment after the left.
    # Ends with them up; go on with hands_up_hold.
    back = keys(t, (0.0, 0.0), (0.25, 1.0), (0.7, 0.6), (1.0, 0.5))
    up = keys(t, (0.05, 0.0), (0.75, 1.0), (1.0, 1.0))
    p = Pose()
    stand(p, width=0.16, lean=-3 * back, shift=(0.0, -0.02 * back), height=0.905 - 0.015 * back)
    p.chest(pitch=-2 * back)
    raised(p, up)
    p.head(pitch=-4 * back)
    return p


def hands_up_hold(t):
    # Kept up: the weight goes from foot to foot, the arms tire and are put back up, the eyes go from one officer to the other.
    p = Pose()
    sway = idle(p, t, width=0.16, lean=-1.5, amount=0.6)
    p.chest(pitch=-1, roll=1.5 * sway)
    raised(p, 1.0 - 0.06 * wave(t, 0.3), tremble=s(t, 5), sway=sway)
    p.head(pitch=-2, yaw=keys(t, (0.0, -14), (0.4, -14), (0.5, 16), (0.9, 16), (1.0, -14)))
    return p


def radio_talk(t):
    # The left hand at the radio on the left collar bone, the head turned down to it; speaks, listens (the head
    # comes up and looks round), speaks again.
    speak = keys(t, (0.0, 1.0), (0.42, 1.0), (0.52, 0.0), (0.82, 0.0), (0.92, 1.0), (1.0, 1.0))
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.7)
    p.chest(pitch=1 + speak, yaw=-3 - 2 * sway, roll=1.5 * sway)
    mic = np.array([-0.095 + 0.03 * sway, 0.095, 1.385])
    p.hand("L", mic + (-0.085, 0.045, -0.055 - 0.02 * (1 - speak)), fingers=(0.75, -0.25, 0.6), palm=(0.25, -0.9, -0.3), elbow=(-0.8, -0.2, -0.6), curl="cup")
    p.shrug("L", 0.35)
    hang(p, "R", sway, 0.1 * s(t, 2))
    p.head(yaw=-30 * speak + 22 * (1 - speak) * s(t, 2, 0.1), pitch=16 * speak + 2 * max(0.0, s(t, 9)) * speak, roll=-6 * speak)
    return p


NOTEBOOK = Grip("L", at=(-0.012, -0.03, -0.006), thumb=(0.3, 1, 0), back=(0, 0, -1), hold="plate")


def take_notes(t):
    # A notebook in the left hand at the chest, the right hand writing short lines; a look up at whoever is
    # talking, a nod, and down again.
    up = keys(t, (0.0, 0.0), (0.50, 0.0), (0.60, 1.0), (0.78, 1.0), (0.88, 0.0), (1.0, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.7)
    pad = (turn(yaw=10, pitch=-38, roll=-4), np.array([-0.05 + 0.02 * sway, 0.31, 1.16]))
    p.chest(pitch=3 - 2 * up, yaw=-2, roll=1.2 * sway)
    p.tool(*pad, NOTEBOOK, elbows={"L": (-0.6, -0.7, -0.5)})
    line = (t * 5) % 1.0                              # five short lines a loop
    pen = pad[1] + pad[0] @ np.array([-0.025 + 0.05 * line + 0.004 * s(t, 26), 0.03 - 0.045 * ((t * 5) // 1) / 5 + 0.003 * s(t, 17), 0.07 + 0.035 * up])
    p.hand("R", pen, fingers=(-0.62, 0.55, -0.55), palm=(-0.45, 0.25, -0.86), elbow=(0.75, -0.55, -0.4), curl="pen")
    p.head(yaw=-3 + 8 * up, pitch=26 - 26 * up + 6 * max(0.0, s(t, 8)) * up)
    return p


def point_direct(t):
    # "That way, please": looks at them, the right arm goes out to the right with a pointing finger and stays, and
    # the left hand sweeps them along towards it, twice; then both come down.
    out = keys(t, (0.0, 0.0), (0.22, 1.0), (0.80, 1.0), (1.0, 0.0))
    along = keys(t, (0.12, 0.0), (0.30, 1.0), (0.82, 1.0), (1.0, 0.0))
    sweep = wave(keys(t, (0.26, 0.0), (0.80, 2.0))) * along
    p = Pose()
    stand(p, width=0.17, yaw=10 * out, sway=0.5 * out, toe_out=10, turn_feet=(0, 12 * out))
    p.chest(yaw=12 * out, pitch=1, roll=2 * out)
    hips = p._hips[0]
    rest = hips + np.array([0.235, 0.045, -0.045])
    far = np.array([0.66, 0.16, 1.34])
    p.hand("R", rest + (far - rest) * out + (0, 0.10 * math.sin(math.pi * out), 0), fingers=unit(keys(out, (0.0, (0.1, 0.3, -1)), (1.0, (1.0, 0.25, -0.02)))),
           palm=keys(out, (0.0, (-1, 0, 0)), (1.0, (0.1, 0.5, -0.85))), elbow=(0.6, -0.6, -0.5), curl=blend_holds("relaxed", "point", out))
    rest = hips + np.array([-0.235, 0.045, -0.045])
    start, end = np.array([-0.30, 0.34, 1.14]), np.array([0.14, 0.36, 1.22])
    p.hand("L", rest + (start + (end - start) * sweep - rest) * along, fingers=unit(keys(along, (0.0, (-0.1, 0.3, -1)), (1.0, (0.55 + 0.4 * sweep, 0.75, 0.1)))),
           palm=keys(along, (0.0, (1, 0, 0)), (1.0, (0.85, 0.2, 0.45))), elbow=(-0.6, -0.6, -0.5), curl=blend_holds("relaxed", "open", along))
    p.head(yaw=keys(t, (0.0, 0), (0.18, -6), (0.36, 34), (0.56, 0), (0.74, 30), (1.0, 0)), pitch=1)
    return p


CLIPS = [
    Clip("push_cart", 1.1, push_cart, speed=1.2, dressing=(("stretcher", turn(), (0.0, 1.55, 0.0)), ("stretcher_push_bar", turn(), (0.0, 1.55, 0.0))),
         note="walking behind a rail 0.44 m ahead and 0.95 m up, hands 0.45 m apart (the stretcher's bar, a pushchair): made for 1.2 m/s"),
    Clip("pull_cart", 1.1, pull_cart, speed=0.9, dressing=(("stretcher", turn(), (0.0, 1.55, 0.0)), ("stretcher_push_bar", turn(), (0.0, 1.55, 0.0))),
         note="walking BACKWARDS at 0.9 m/s drawing the same rail after; the character still faces the rail"),
    Clip("lift_patient", 1.6, lift_patient, loop=False, marks={"lift": 0.42, "up": 1.38},
         note="one of two lifting together: from the right knee, hands under someone lying 0.42 m ahead, to standing with the hands 0.30 m ahead and 0.88 m up; hold the end"),
    Clip("carry_bag_walk", 1.08, carry_bag_walk, speed=1.3, tool="medic_bag", grips=(BAG,), tool_length=0.4, note="walking at 1.3 m/s with the medic bag in the right hand"),
    Clip("hands_up", 1.0, hands_up, loop=False, note="both hands go up; hold the end or go on with hands_up_hold"),
    Clip("hands_up_hold", 5.0, hands_up_hold, note="hands kept up beside the head; follows hands_up"),
    Clip("radio_talk", 5.0, radio_talk, watch="L", note="left hand at the shoulder radio on the left collar bone, speaking and listening"),
    Clip("take_notes", 5.0, take_notes, tool="notebook", grips=(NOTEBOOK,), tool_length=0.15, note="notebook in the left hand, writing with the right, a look up and a nod"),
    Clip("point_direct", 2.6, point_direct, loop=False, note="points to the character's right with the right arm and waves someone in front along that way"),
]
