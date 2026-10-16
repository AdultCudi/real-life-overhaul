"""Clips for the player's jobs (Scripts/Jobs: the wish-list in docs/features/jobs.md, "Job clips") and
for the jobs to come (mopping, stocking shelves, a counter, a till, carrying a box, a guitar). Same rules as clips.py.

The short gestures start and end in the standing idle and are made for the upper body (mask "upper"): play them
with ActivityAnimator.PlayOver and the legs stay the game's own, or with Play and they stand as authored.

Where things are, for whoever places the character (x right, y ahead, z up, from the character's feet):
    job_hand_over, job_receive      the hand-over point is 0.50 m ahead, 1.15 m up (the other person stands 0.8 to 0.9 m away)
    job_receive_box                 the same point; the box is 0.38 m wide and comes into both hands
    job_knock                       the door is 0.50 m ahead; the knuckles land 1.50 m up
    job_car_enter / _exit           see CAR below
    job_load_boot                   the boot floor is 0.52 m ahead and 0.70 m up
    stock_shelf                     the box is at the left hip, 0.95 m up; the shelf is 0.55 m ahead, 1.30 m up
    serve_counter, till_tap         the counter top is 0.975 m up (the carts' counter), its near edge 0.25 m ahead
"""
import math

import numpy as np

from motion import Clip, Walk, c, hang, idle, lag, s, shaft, stand, weight  # noqa: F401
from rig import Grip, Pose, between, blend_holds, ease, keys, turn, turn_about, unit, wave  # noqa: F401

GIVE = np.array([0.10, 0.50, 1.15])                   # where a thing changes hands


def at_side(p, side):
    return p._hips[0] + np.array([(1.0 if side == "R" else -1.0) * 0.235, 0.045, -0.045])


def mirrored(x, sign):
    return np.array([x[0] * sign, x[1], x[2]])


# ---------------------------------------------------------------------------------------------- the gestures of the wish-list

def job_hail(t):
    # Flagging a car down: up on the toes and leaning out, the right arm up and out towards the road, the open
    # hand waving twice a loop. Loops for as long as the car takes.
    beat = wave(t * 2)
    late = wave(lag(t, 0.05) * 2)
    p = Pose()
    stand(p, width=0.16, lean=5 + 2 * beat, sway=0.35 + 0.15 * s(t), shift=(0.0, 0.05), height=0.92 + 0.03 * beat, lift=(0.3 + 0.7 * beat, 0.3 + 0.7 * beat))
    p.chest(yaw=6, pitch=1, roll=3)
    p.hand("R", (0.43 + 0.12 * (beat - 0.5), 0.36, 1.64 + 0.05 * beat), fingers=unit((0.25 + 0.7 * (late - 0.5), 0.35, 1.0)), palm=(-0.15, 1.0, -0.3), elbow=(0.9, -0.3, -0.5), curl="open")
    p.shrug("R", 0.6)
    hang(p, "L", swing=0.2 * (beat - 0.5))
    p.head(yaw=8, pitch=-6)
    return p


def hand_over(side):
    sign = 1.0 if side == "R" else -1.0
    other = "L" if side == "R" else "R"

    def pose(t):
        # Holds something out at chest height to someone in front, lets go (0.55 s), and the arm comes back.
        out = keys(t, (0.0, 0.0), (0.38, 1.0), (0.56, 1.0), (1.0, 0.0))
        body = keys(t, (0.0, 0.0), (0.30, 1.0), (0.58, 1.0), (1.0, 0.0))
        held = keys(t, (0.42, 1.0), (0.50, 0.0))                       # the fingers open at the release
        p = Pose()
        stand(p, width=0.15, lean=6 * body, sway=0.3 * sign * body, shift=(0.0, 0.03 * body))
        p.chest(pitch=1 + 2 * body, yaw=-5 * sign * body)
        rest, give = at_side(p, side), mirrored(GIVE, sign)
        chest = np.array([sign * 0.17, 0.26, 1.10])                    # where it is carried before it is offered
        wrist = keys(out, (0.0, rest), (0.35, chest), (1.0, give)) if t < 0.5 else keys(out, (0.0, rest), (1.0, give))
        p.hand(side, wrist + (0, 0, 0.02 * math.sin(math.pi * out)), fingers=unit(keys(out, (0.0, (sign * 0.1, 0.3, -1)), (1.0, (-sign * 0.2, 0.95, 0.12)))),
               palm=keys(out, (0.0, (-sign, 0, 0)), (1.0, (-sign * 0.75, 0.0, 0.65))), elbow=(sign * 0.6, -0.6, -0.5),
               curl=blend_holds(blend_holds("relaxed", "open", out), "pinch", held * out))
        hang(p, other)
        p.head(pitch=1 + 5 * max(0.0, s(t, 1.5, 0.2)) * body, yaw=-3 * sign * body)
        return p
    return pose


def job_receive(t):
    # Reaches forward, closes the hand on what is offered (0.5 s), draws it in to the body, a small nod.
    out = keys(t, (0.0, 0.0), (0.36, 1.0), (0.42, 1.0), (0.78, 0.0), (1.0, 0.0))
    took = keys(t, (0.34, 0.0), (0.42, 1.0))
    keep = keys(t, (0.42, 0.0), (0.80, 1.0), (1.0, 1.0))
    body = keys(t, (0.0, 0.0), (0.32, 1.0), (0.46, 1.0), (0.85, 0.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.15, lean=6 * body, sway=0.3 * body, shift=(0.0, 0.03 * body))
    p.chest(pitch=1 + 2 * body, yaw=-5 * body)
    rest, belly = at_side(p, "R"), np.array([0.16, 0.24, 1.06])
    home = rest + (belly - rest) * keep
    p.hand("R", home + (GIVE - home) * out + (0, 0, 0.02 * math.sin(math.pi * out)), fingers=unit(keys(max(out, keep), (0.0, (0.1, 0.3, -1)), (1.0, (-0.2, 0.95, 0.12)))),
           palm=keys(max(out, keep), (0.0, (-1, 0, 0)), (1.0, (-0.75, 0.0, 0.65))), elbow=(0.6, -0.6, -0.5), curl=blend_holds(blend_holds("relaxed", "open", out), "pinch", took))
    hang(p, "L")
    p.head(pitch=1 + 9 * math.sin(math.pi * keys(t, (0.55, 0.0), (0.85, 1.0))), yaw=-3 * body)
    return p


BOX_R = Grip("R", at=(0.15, 0.0, 0.0), thumb=(0, 1, 0), back=(0, 0, -1), hold="plate")
BOX_L = Grip("L", at=(-0.15, 0.0, 0.0), thumb=(0, 1, 0), back=(0, 0, -1), hold="plate")
BOX_CARRY = np.array([0.0, 0.29, 0.98])               # the bottom middle of a box carried in both arms


def job_receive_box(t):
    # Both hands go out under a box that is being held out (they have it at 0.5 s), it is drawn in against the
    # body, and a nod. Ends carrying it: go on with carry_box_hold or carry_box_walk.
    out = keys(t, (0.0, 0.0), (0.36, 1.0), (0.42, 1.0), (0.82, 0.0), (1.0, 0.0))
    up = keys(t, (0.0, 0.0), (0.30, 1.0), (1.0, 1.0))
    body = keys(t, (0.0, 0.0), (0.32, 1.0), (0.46, 1.0), (0.85, 0.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=9 * body, shift=(0.0, 0.04 * body), left=(0, 0.04 * body))
    p.chest(pitch=1 + 2 * body)
    low = np.array([0.0, 0.20, 0.93])
    box = low + (BOX_CARRY - low) * up + (np.array([0.0, 0.49, 1.03]) - BOX_CARRY) * out
    p.tool(turn(pitch=-6 * out), box, BOX_R, BOX_L, elbows={"R": (0.7, -0.6, -0.5), "L": (-0.7, -0.6, -0.5)})
    if t < 0.36 * 1.0:                                 # empty hands on the way out: not yet closed
        p.curl("R", blend_holds("relaxed", "plate", up)).curl("L", blend_holds("relaxed", "plate", up))
    p.head(pitch=4 + 8 * math.sin(math.pi * keys(t, (0.58, 0.0), (0.88, 1.0))) + 6 * out)
    return p


def job_refuse(t):
    # "No, thanks": the right palm comes up and out at shoulder height, the head turns away, half a step back.
    no = keys(t, (0.0, 0.0), (0.28, 1.0), (0.66, 1.0), (1.0, 0.0))
    arm = keys(t, (0.04, 0.0), (0.32, 1.0), (0.70, 1.0), (1.0, 0.0))
    step = math.sin(math.pi * keys(t, (0.08, 0.0), (0.34, 1.0)))
    p = Pose()
    stand(p, width=0.15, lean=-3 * no, sway=-0.5 * no, shift=(0.0, -0.05 * no), left=(-0.02 * no, -0.14 * no), lift=(step, 0.0), yaw=-5 * no)
    p.chest(pitch=-1 * no, yaw=-8 * no)
    rest = at_side(p, "R")
    stop = np.array([0.22, 0.42 + 0.02 * s(t, 3) * no, 1.36])
    p.hand("R", rest + (stop - rest) * arm, fingers=unit(keys(arm, (0.0, (0.1, 0.3, -1)), (1.0, (0.08, 0.2, 1)))), palm=keys(arm, (0.0, (-1, 0, 0)), (1.0, (0, 1, -0.1))),
           elbow=(0.7, -0.5, -0.6), curl=blend_holds("relaxed", "open", arm))
    hang(p, "L")
    p.head(yaw=-30 * no + 6 * s(t, 3) * no, pitch=3 * no)
    return p


def job_thank(t):
    # A nod and a short open-hand wave at head height.
    up = keys(t, (0.0, 0.0), (0.30, 1.0), (0.70, 1.0), (1.0, 0.0))
    swing = s(t, 2.5, 0.1) * up
    p = Pose()
    stand(p, width=0.15, lean=3 * up, sway=0.3 * up)
    p.chest(pitch=1 + 2 * up, roll=2 * up)
    rest = at_side(p, "R")
    high = np.array([0.42 + 0.10 * swing, 0.30, 1.52])
    p.hand("R", rest + (high - rest) * up, fingers=unit(keys(up, (0.0, (0.1, 0.3, -1)), (1.0, (0.3 * swing, 0.1, 1)))), palm=keys(up, (0.0, (-1, 0, 0)), (1.0, (0, 1, 0))),
           elbow=(0.8, -0.3, -0.6), curl=blend_holds("relaxed", "open", up))
    hang(p, "L")
    p.head(pitch=1 + 11 * math.sin(math.pi * keys(t, (0.12, 0.0), (0.50, 1.0))), roll=2 * up)
    return p


def job_knock(t):
    # Three raps on a door with the knuckles of the right hand at head height, then a step back to wait.
    up = keys(t, (0.0, 0.0), (0.20, 1.0), (0.62, 1.0), (0.82, 0.0), (1.0, 0.0))
    rap = max(0.0, s(keys(t, (0.22, 0.0), (0.62, 3.0)))) ** 0.7 * (1.0 if 0.22 < t < 0.62 else 0.0)
    back = keys(t, (0.62, 0.0), (0.92, 1.0), (1.0, 1.0))
    step = math.sin(math.pi * keys(t, (0.64, 0.0), (0.90, 1.0)))
    p = Pose()
    stand(p, width=0.15, lean=4 * up, sway=0.4 * up - 0.5 * back, shift=(0.0, 0.03 * up - 0.06 * back), left=(0.0, -0.16 * back), lift=(step, 0.0))
    p.chest(pitch=1 + up, yaw=-6 * up)
    rest = at_side(p, "R")
    door = np.array([0.13, 0.385 + 0.045 * rap, 1.50])
    p.hand("R", rest + (door - rest) * up + (0, -0.06 * math.sin(math.pi * up) * (1 - up), 0), fingers=unit(keys(up, (0.0, (0.1, 0.3, -1)), (1.0, (-0.25, 0.3, 0.9)))),
           palm=keys(up, (0.0, (-1, 0, 0)), (1.0, (-0.9, -0.35, -0.2))), elbow=(0.7, -0.4, -0.7), curl=blend_holds("relaxed", "knock", up))
    hang(p, "L")
    p.head(pitch=-2 * up + 3 * back, yaw=4 * up)
    return p


def job_annoyed(t):
    # Both hands thrown up, the head shaken, and a turn away.
    up = keys(t, (0.0, 0.0), (0.22, 1.0), (0.46, 1.0), (0.74, 0.0), (1.0, 0.0))
    away = keys(t, (0.36, 0.0), (0.70, 1.0), (0.86, 1.0), (1.0, 0.0))
    shake = s(t, 3, 0.05) * keys(t, (0.10, 0.0), (0.24, 1.0), (0.56, 1.0), (0.72, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=-2 * up, sway=-0.5 * away, yaw=-10 * away, turn_feet=(-10 * away, 0), height=0.905 + 0.008 * up)
    p.chest(pitch=-2 * up, yaw=-14 * away)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        rest = at_side(p, side)
        high = np.array([sign * 0.40, 0.26, 1.34 + 0.04 * sign * shake])
        p.hand(side, rest + (high - rest) * up + (0, 0.08 * math.sin(math.pi * up), 0), fingers=unit(keys(up, (0.0, (sign * 0.1, 0.3, -1)), (1.0, (sign * 0.45, 0.6, 0.65)))),
               palm=keys(up, (0.0, (-sign, 0, 0)), (1.0, (-sign * 0.5, 0.2, 0.85))), elbow=(sign * 0.7, -0.6, -0.4), curl=blend_holds("relaxed", "open", up))
        p.shrug(side, 0.7 * up)
    p.head(yaw=14 * shake - 26 * away, pitch=-3 * up + 4 * away, roll=3 * shake)
    return p


# ---------------------------------------------------------------------------------------------- into and out of a car

# The character does not move: it stands where it stood, FACING THE DOOR, and the clip carries the body into the
# seat. Put the character's feet on the ground CAR_OUT metres out from the middle of the seat, on the line through
# the seat that is square to the car, facing the car. The seat then is straight ahead and the car's nose to the
# character's right ("job_car_enter", a door on the car's right-hand side) or left ("job_car_enter_l"). The clip
# ends sitting, facing the car's nose; its last pose can be held, but a passenger should be handed to the game's
# own seated state then. job_car_exit is the same backwards, from sitting to standing at that point.
CAR_OUT = 0.75                                        # from the feet to the middle of the seat
CAR_SEAT = 0.52                                       # the top of the seat cushion above the road
CAR_FLOOR = 0.30                                      # the car's floor above the road
CAR_SILL = 0.32                                       # the door opening's sill is this far ahead of the feet


def car_enter(side):
    sign = 1.0 if side == "R" else -1.0               # the car's nose is to the right (+) or left of the character

    def pose(t):
        reach = keys(t, (0.0, 0.0), (0.16, 1.0), (0.30, 1.0), (0.46, 0.0), (1.0, 0.0))     # the hand to the handle, and the pull
        pull = keys(t, (0.16, 0.0), (0.32, 1.0), (1.0, 1.0))
        step = keys(t, (0.26, 0.0), (0.52, 1.0), (1.0, 1.0))                               # the foot nearer the nose goes in
        sit = keys(t, (0.40, 0.0), (0.80, 1.0), (1.0, 1.0))                                # the body follows: duck, turn, down
        tuck = keys(t, (0.52, 0.0), (0.90, 1.0), (1.0, 1.0))                               # the second foot comes in after
        duck = math.sin(math.pi * keys(t, (0.34, 0.0), (0.84, 1.0)))
        p = Pose()
        seat = np.array([0.0, CAR_OUT - 0.04, CAR_SEAT + 0.085])                           # the hip joints when sitting
        stood = np.array([-sign * 0.04 * pull, -0.03 * pull, 0.905])
        hips = stood + (seat - stood) * sit + (0, 0, -0.05 * duck * (1 - sit))
        p.hips(hips, yaw=sign * 90 * ease(sit), pitch=10 * reach + 30 * duck - 6 * sit, roll=-sign * 6 * duck)
        inner, outer = ("R", "L") if side == "R" else ("L", "R")
        ground = np.array([sign * 0.15, 0.0, 0.0])
        inside = np.array([sign * 0.42, CAR_OUT - 0.10, CAR_FLOOR])
        lift = math.sin(math.pi * step) if step < 1.0 else 0.0
        p.foot(inner, ground + (inside - ground) * step + (0, 0, 0.16 * lift), yaw=sign * (8 + 80 * step), pitch=12 * lift,
               knee=unit(np.array([sign * 0.2, 1.0, 0.2]) * (1 - sit) + np.array([sign * 1.0, 0.0, 0.5]) * sit))
        ground = np.array([-sign * 0.15, 0.0, 0.0])
        inside = np.array([sign * 0.40, CAR_OUT + 0.10, CAR_FLOOR])
        lift = math.sin(math.pi * tuck) if tuck < 1.0 else 0.0
        over = ground + (inside - ground) * tuck + (0, -0.10 * lift, 0.26 * lift)
        p.foot(outer, over, yaw=-sign * 8 + sign * 98 * tuck, pitch=20 * lift + 14 * sit * (1 - tuck),
               knee=unit(np.array([-sign * 0.2, 1.0, 0.2]) * (1 - tuck) + np.array([sign * 1.0, 0.1, 0.5]) * tuck))
        p.chest(pitch=4 * duck + 2 * sit, yaw=-sign * 16 * duck)
        # The hand nearer the tail opens the door; afterwards both come to rest on the thighs.
        handle = np.array([-sign * 0.28 - sign * 0.10 * pull, 0.36 - 0.14 * pull, 0.98])
        pelvis = p._hips[1]
        for hand_side, hand_sign in ((outer, -sign), (inner, sign)):
            rest = hips + pelvis @ np.array([hand_sign * 0.235, 0.045, -0.045])
            lap = hips + pelvis @ np.array([hand_sign * 0.13, 0.30, 0.07])
            at = rest + (lap - rest) * ease(sit)
            fingers = unit(pelvis @ (np.array([hand_sign * 0.1, 0.3, -1]) * (1 - sit) + np.array([-hand_sign * 0.1, 1.0, -0.25]) * sit))
            palm = pelvis @ (np.array([-hand_sign, 0, 0]) * (1 - sit) + np.array([0, 0, -1.0]) * sit)
            hold = blend_holds("relaxed", "plate", sit)
            if hand_side == outer:
                at = at + (handle - at) * reach
                fingers = unit(fingers * (1 - reach) + np.array([sign * 0.3, 0.9, -0.1]) * reach)
                palm = palm * (1 - reach) + np.array([sign * 0.9, 0.0, -0.3]) * reach
                hold = blend_holds(hold, "handle", reach)
            p.hand(hand_side, at, fingers=fingers, palm=palm, elbow=pelvis @ np.array([hand_sign * 0.6, -0.7, -0.3]), curl=hold)
        p.head(pitch=12 * duck - 4 * sit, yaw=-sign * 20 * duck * (1 - sit) + sign * 25 * reach)
        return p
    return pose


def reversed_(pose):
    return lambda t: pose(1.0 - t)


# ---------------------------------------------------------------------------------------------- a box

BOOT = np.array([0.0, 0.52, 0.70])                    # the boot floor: where the bottom of the box is set down


def job_load_boot(t):
    # Standing at an open boot with a box in both arms: a step in, a bend at the hips and knees, the box lowered on
    # to the boot floor (let go at 1.0 s), the hands slid out from under it and the body straightened up.
    down = keys(t, (0.0, 0.0), (0.50, 1.0), (0.62, 1.0), (1.0, 0.0))
    arms = keys(t, (0.06, 0.0), (0.54, 1.0), (0.60, 1.0), (1.0, 0.0))
    free = keys(t, (0.56, 0.0), (0.74, 1.0), (1.0, 1.0))
    p = Pose()
    stand(p, width=0.18, lean=42 * down, height=0.905 - 0.02 * down, shift=(0.0, 0.16 * down), left=(0.0, 0.10 * down), sway=-0.2 * down, toe_out=10)
    p.chest(pitch=2 + 4 * down)
    box = BOX_CARRY + (BOOT - BOX_CARRY) * arms
    if t < 0.56:
        p.tool(turn(), box, BOX_R, BOX_L, elbows={"R": (0.7, -0.6, -0.5), "L": (-0.7, -0.6, -0.5)})
    else:
        p.tool_free(turn(), BOOT)                                   # it stays in the boot; the hands come away empty
        for side, sign, grip in (("L", -1.0, BOX_L), ("R", 1.0, BOX_R)):
            wrist, hand = grip.wrist(turn(), box + (sign * 0.07 * free, -0.10 * free, 0.03 * free))
            rest = at_side(p, side)
            k = keys(t, (0.70, 0.0), (1.0, 1.0))
            p.hand(side, wrist, turned=hand, elbow=(sign * 0.7, -0.6, -0.5), curl=blend_holds("plate", "relaxed", free))
            if k > 0:
                p.hand(side, wrist + (rest - wrist) * k, fingers=unit(hand @ np.array([sign, 0, 0]) * (1 - k) + np.array([sign * 0.1, 0.3, -1]) * k),
                       palm=(hand @ np.array([0, 0, -1])) * (1 - k) + np.array([-sign, 0, 0]) * k, elbow=(sign * 0.7, -0.6, -0.5), curl=blend_holds("plate", "relaxed", free))
    p.look_at(BOOT + (0, 0.1, 0.1), down=20)
    return p


def carry_box_hold(t):
    # A box held in both arms against the belly. Made for the arms only (mask "arms"): over the game's own walk or
    # idle it is what carrying a parcel looks like, with no feet to slide.
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.6)
    p.chest(pitch=-1, roll=1.2 * sway)
    p.tool(turn(roll=1.5 * sway), BOX_CARRY + (0.02 * sway, 0.0, 0.006 * s(t, 2)), BOX_R, BOX_L, elbows={"R": (0.7, -0.6, -0.5), "L": (-0.7, -0.6, -0.5)})
    p.head(pitch=2)
    return p


BOX_WALK = Walk(1.2, 1.1)


def carry_box_walk(t):
    # Walking with a box in both arms: shorter in the arms than a free walk, the load steady in front, the trunk a
    # little back against its weight.
    p = Pose()
    swing = BOX_WALK.body(p, t, lean=1, yaw=4, roll=1.8, bob=0.02)
    p.chest(pitch=-2, yaw=-3 * swing, roll=-1.2 * s(t, shift=0.02))
    hips = p._hips[0]
    box = (turn(yaw=2 * swing, roll=1.5 * s(t, shift=0.05)), BOX_CARRY + (hips[0] * 0.7, 0.0, hips[2] - 0.895 + 0.004 * s(t, 2, 0.1)))
    p.tool(*box, BOX_R, BOX_L, elbows={"R": (0.7, -0.6, -0.5), "L": (-0.7, -0.6, -0.5)})
    p.head(pitch=0, yaw=2 * swing)
    return p


# ---------------------------------------------------------------------------------------------- flyers

FLYERS = Grip("L", at=(-0.02, 0.012, -0.125), thumb=(0, 0, 1), back=(0, 1, 0), hold=(0.25, 0.18, 0.2, 0.22, 0.25, 0.3, 0.2))


def job_flyer_hold(t):
    # A stack of flyers held flat against the chest with the left hand. Made for the left arm only (mask
    # "left_arm"): the rest of the body is whatever the game is doing, walking included.
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.5)
    p.chest(pitch=1, roll=1.0 * sway)
    stack = (turn(yaw=-8, pitch=-10, roll=4), np.array([-0.075 + 0.02 * sway, 0.185, 1.325 + 0.004 * s(t, 2)]))
    p.tool(*stack, FLYERS, elbows={"L": (-0.7, -0.5, -0.6)})
    hang(p, "R", sway)
    p.head(pitch=1)
    return p


# ---------------------------------------------------------------------------------------------- the jobs to come

MOP_LOW = Grip("R", at=(0, 0, 0.95), thumb=(0, 0, 1), back=(1, 0, 0))
MOP_TOP = Grip("L", at=(0, 0, 1.28), thumb=(0, 0, 1), back=(-1, 0, 0))


def mop_floor(t):
    # The mop drawn across the floor in a lazy figure of eight in front, the body rocking from foot to foot with it
    # and the top hand steering.
    side = s(t)                                       # -1 at the left, +1 at the right
    body = s(t + 0.05)
    head = np.array([0.25 * side, 0.72 + 0.10 * s(t, 2), 0.0])
    top = np.array([0.02 + 0.10 * side, 0.30 + 0.03 * c(t, 2), 1.20])
    p = Pose()
    stand(p, height=0.90, width=0.18, yaw=7 * body, lean=20 + 3 * s(t, 2), sway=0.6 * body, shift=(0.0, 0.03), left=(0.0, 0.08), right=(0.02, -0.08), toe_out=10,
          lift=(0.35 * max(0.0, body), 0.35 * max(0.0, -body)))
    p.chest(yaw=8 * body, pitch=4, roll=2.5 * body)
    p.tool(*shaft(head, top), MOP_LOW, MOP_TOP, elbows={"R": (0.8, -0.5, -0.3), "L": (-0.8, -0.4, -0.2)})
    p.look_at(head + (0, 0.5, 0.1), down=16)
    return p


BOX_AT = np.array([-0.20, 0.30, 0.97])                # an open box at the left hip
SHELF = np.array([0.10, 0.55, 1.30])


def stock_shelf(t):
    # Takes something out of a box at the left hip with the right hand (0.6 s), lifts it across and sets it on a
    # shelf at chest height (1.5 s), squares it up, and the hand comes back.
    dip = keys(t, (0.0, 0.0), (0.22, 1.0), (0.30, 1.0), (0.50, 0.0), (1.0, 0.0))
    place = keys(t, (0.30, 0.0), (0.56, 1.0), (0.72, 1.0), (1.0, 0.0))
    body = keys(t, (0.26, 0.0), (0.52, 1.0), (0.74, 1.0), (1.0, 0.0))
    has = keys(t, (0.20, 0.0), (0.26, 1.0), (0.56, 1.0), (0.64, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=6 * dip + 8 * body, sway=-0.5 * dip + 0.5 * body, shift=(0.0, 0.05 * body), right=(0.0, 0.07 * body), yaw=-8 * dip + 4 * body)
    p.chest(pitch=2 + 2 * dip, yaw=-14 * dip + 6 * body, roll=-2 * dip)
    rest = at_side(p, "R")
    wrist = rest + (BOX_AT + (0.06, -0.02, 0.12) - rest) * dip + (SHELF + (0.04, -0.09 + 0.012 * s(t, 6) * place, 0.06) - rest) * place
    p.hand("R", wrist + (0, 0.06 * math.sin(math.pi * place), 0.05 * math.sin(math.pi * place)), fingers=unit(np.array([0.1, 0.3, -1]) * (1 - place) * (1 - dip) + np.array([-0.5, 0.5, -0.7]) * dip + np.array([-0.15, 0.95, -0.15]) * place),
           palm=np.array([-1, 0, 0]) * (1 - place) * (1 - dip) + np.array([-0.3, 0.2, -0.95]) * dip + np.array([-0.85, 0.1, -0.4]) * place, elbow=(0.7, -0.6, -0.4),
           curl=blend_holds("relaxed", "cup", has))
    # The left arm steadies the box.
    p.hand("L", BOX_AT + (-0.14, -0.02, -0.03), fingers=(0.35, 0.9, 0.1), palm=(0.9, 0.0, 0.3), elbow=(-0.7, -0.7, -0.2), curl="plate")
    p.look_at(BOX_AT * dip + SHELF * (1 - dip) + (0, 0.2, 0.0), down=24)
    return p


CUP = Grip("R", at=(0.037, 0, 0.060), thumb=(0, 0, 1), back=(1, 0, 0), hold="cup")     # as in clips.py
COUNTER_TOP = 0.975


def serve_counter(t):
    # Behind a counter: picks a cup up from the worktop in front, leans over and sets it down on the customer's
    # side (let go at 1.1 s), a nod, and back upright.
    out = keys(t, (0.0, 0.0), (0.16, 0.0), (0.50, 1.0), (0.62, 1.0), (1.0, 0.0))
    take = keys(t, (0.0, 0.0), (0.14, 1.0), (0.54, 1.0), (0.62, 0.0))
    body = keys(t, (0.10, 0.0), (0.46, 1.0), (0.64, 1.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=4 + 20 * body, sway=0.3 * body, shift=(0.0, 0.03 + 0.10 * body), right=(0.0, 0.07 * body))
    p.chest(pitch=2 + 2 * body, yaw=-6 * body)
    near, far = np.array([0.16, 0.33, COUNTER_TOP]), np.array([0.06, 0.78, COUNTER_TOP])
    cup = near + (far - near) * out + (0, 0, 0.07 * math.sin(math.pi * out) + 0.0)
    if t < 0.58:
        rest = at_side(p, "R")
        wrist, hand = CUP.wrist(turn(yaw=-10), cup)
        k = keys(t, (0.0, 0.0), (0.14, 1.0))
        p.tool(turn(yaw=-10), cup, CUP, elbows={"R": (0.7, -0.6, -0.4)})
        if k < 1.0:                                   # the hand comes to the cup first
            p.hand("R", rest + (wrist - rest) * k, fingers=unit(np.array([0.1, 0.3, -1]) * (1 - k) + hand @ np.array([1, 0, 0]) * k), palm=np.array([-1, 0, 0]) * (1 - k) + hand @ np.array([0, 0, -1]) * k,
                   elbow=(0.7, -0.6, -0.4), curl=blend_holds("relaxed", "cup", k * 0.7))
            p.tool_free(turn(yaw=-10), near)
    else:
        p.tool_free(turn(yaw=-10), far)               # the cup stays on the counter
        k = keys(t, (0.60, 0.0), (1.0, 1.0))
        wrist, hand = CUP.wrist(turn(yaw=-10), far)
        rest = at_side(p, "R")
        p.hand("R", wrist + (rest - wrist) * k + (0, -0.05 * math.sin(math.pi * k), 0.05 * math.sin(math.pi * k)), fingers=unit(hand @ np.array([1, 0, 0]) * (1 - k) + np.array([0.1, 0.3, -1]) * k),
               palm=(hand @ np.array([0, 0, -1])) * (1 - k) + np.array([-1, 0, 0]) * k, elbow=(0.7, -0.6, -0.4), curl=blend_holds("cup", "relaxed", k))
    p.hand("L", (-0.22, 0.30, COUNTER_TOP + 0.03), fingers=(0.3, 0.9, 0.05), palm=(0, 0.1, -1), elbow=(-0.8, -0.5, -0.1), curl="relaxed")
    p.head(pitch=6 * (1 - body) + 2 + 8 * math.sin(math.pi * keys(t, (0.56, 0.0), (0.80, 1.0))), yaw=-3 * body)
    return p


def till_tap(t):
    # A register or a card machine on the counter: looks down, three taps with the right forefinger, looks up.
    over = keys(t, (0.0, 0.0), (0.22, 1.0), (0.74, 1.0), (1.0, 0.0))
    taps = keys(t, (0.24, 0.0), (0.72, 3.0))
    tap = max(0.0, math.sin(2 * math.pi * taps)) ** 0.6 * (1.0 if 0.24 < t < 0.72 else 0.0)
    key = int(min(2, taps))
    p = Pose()
    stand(p, width=0.16, lean=1 + 3 * over, sway=0.3 * over, shift=(0.0, 0.0))
    p.chest(pitch=2 + 2 * over, yaw=-4 * over)
    rest = at_side(p, "R")
    till = np.array([0.10 + 0.045 * key, 0.34 + 0.02 * (key % 2), COUNTER_TOP + 0.20 - 0.035 * tap])
    p.hand("R", rest + (till - rest) * over + (0, 0.05 * math.sin(math.pi * over), 0), fingers=unit(keys(over, (0.0, (0.1, 0.3, -1)), (1.0, (-0.2, 0.75, -0.62)))),
           palm=keys(over, (0.0, (-1, 0, 0)), (1.0, (-0.2, -0.55, -0.8))), elbow=(0.7, -0.6, -0.4), curl=blend_holds("relaxed", "point", over))
    p.hand("L", (-0.22, 0.30, COUNTER_TOP + 0.03), fingers=(0.3, 0.9, 0.05), palm=(0, 0.1, -1), elbow=(-0.8, -0.5, -0.1), curl="relaxed")
    p.head(pitch=4 + 20 * over * (1 - keys(t, (0.72, 0.0), (0.88, 1.0))), yaw=5 * over)
    return p


GUITAR_NECK = Grip("L", at=(0.0, -0.012, 0.68), thumb=(0, 0, 1), back=(-1, 0, 0), hold=(0.35, 0.72, 0.74, 0.76, 0.78, 0.55, 0.0))


def guitar_strum(t):
    # Standing with a guitar across the body: the left hand on the neck (it changes chord once a loop), the right
    # forearm on the upper bout and the hand going down and up across the strings, the body and a foot keeping time.
    beat = s(t, 4)                                    # four down-and-ups a loop
    late = s(lag(t, 0.012), 4)
    bar = wave(t * 2)
    p = Pose()
    sway = 0.5 * weight(t, 1.0)
    stand(p, width=0.16, sway=sway, lean=2, height=0.905 - 0.006 * bar, lift=(0.0, 0.25 * max(0.0, s(t, 4, 0.1))), right=(0.02, 0.05), toe_out=12)
    p.chest(pitch=3, yaw=-6, roll=1.5 * sway + 1.0 * s(t, 2))
    neck = unit(np.array([-0.84, 0.10, 0.50]))
    face = unit(np.array([0.05, 1.0, 0.24]))
    x = np.cross(face, neck)
    turned = np.stack([unit(x), np.cross(neck, unit(x)), neck], axis=1)
    centre = np.array([0.13 + 0.03 * sway, 0.235, 1.03 + 0.004 * s(t, 2)])
    origin = centre - turned @ np.array([0.0, 0.0, 0.24])
    change = math.sin(math.pi * keys(t, (0.42, 0.0), (0.52, 1.0))) + math.sin(math.pi * keys(t, (0.92, 0.0), (1.0, 0.5))) + math.sin(math.pi * keys(t, (0.0, 0.5), (0.04, 1.0)))
    p.tool(turned, origin, GUITAR_NECK, elbows={"L": (-0.6, -0.4, -0.7)})
    p.curl("L", blend_holds(GUITAR_NECK.hold, (0.3, 0.35, 0.4, 0.5, 0.55, 0.5, 0.3), min(1.0, change)))      # the fingers lift for the change of chord
    hole = origin + turned @ np.array([0.0, 0.085, 0.30])
    strum = hole + turned @ np.array([0.055 * beat, 0.0, 0.03]) + (0.0, 0.01, 0.0)
    p.hand("R", strum + turned @ np.array([0.10, 0.02, -0.02]), fingers=unit(turned @ np.array([-1.0, -0.25, 0.25 + 0.3 * late])), palm=turned @ np.array([0.1, -1.0, 0.0]),
           elbow=(0.6, -0.8, 0.2), curl=(0.3, 0.45, 0.5, 0.55, 0.6, 0.6, 0.0))
    p.head(pitch=8 - 6 * bar, yaw=-10 + 14 * keys(t, (0.0, 0.0), (0.5, 0.0), (0.62, 1.0), (0.9, 1.0), (1.0, 0.0)), roll=2 * s(t, 2))
    return p


CLIPS = [
    Clip("job_hail", 1.6, job_hail, mask="upper", note="right arm up and out, two waves a loop, up on the toes: hailing a car in front"),
    Clip("job_hand_over", 1.2, hand_over("R"), loop=False, mask="upper", marks={"release": 0.55}, note="holds something out 0.50 m ahead at 1.15 m with the right hand and lets go"),
    Clip("job_hand_over_l", 1.2, hand_over("L"), watch="L", loop=False, mask="upper", marks={"release": 0.55}, note="the same with the left hand (the flyer hand)"),
    Clip("job_receive", 1.3, job_receive, loop=False, mask="upper", marks={"grip": 0.5}, note="reaches 0.50 m ahead at 1.15 m, takes what is offered, draws it in, nods"),
    Clip("job_receive_box", 1.3, job_receive_box, loop=False, mask="upper", marks={"grip": 0.5}, tool="parcel_m", grips=(BOX_R, BOX_L), tool_length=0.24,
         note="both hands take a box 0.38 m wide held out in front and draw it in; ends carrying it (carry_box_hold)"),
    Clip("job_refuse", 1.2, job_refuse, loop=False, mask="upper", note="right palm up and out at shoulder height, head turned away, half a step back"),
    Clip("job_thank", 1.2, job_thank, loop=False, mask="upper", note="a nod and a short open-hand wave at head height"),
    Clip("job_knock", 1.6, job_knock, loop=False, mask="upper", marks={"rap": 0.42}, note="three raps at 1.50 m on a door 0.50 m ahead, then a step back"),
    Clip("job_annoyed", 1.4, job_annoyed, loop=False, mask="upper", note="both hands thrown up, a shake of the head, a turn away"),
    Clip("job_car_enter", 1.5, car_enter("R"), loop=False, marks={"door": 0.35, "seated": 1.25},
         note="from standing facing a door on the car's RIGHT side, feet %.2f m out from the middle of the seat, into the seat (%.2f m up); the root does not move" % (CAR_OUT, CAR_SEAT)),
    Clip("job_car_enter_l", 1.5, car_enter("L"), loop=False, marks={"door": 0.35, "seated": 1.25}, note="the same for a door on the car's LEFT side"),
    Clip("job_car_exit", 1.5, reversed_(car_enter("R")), loop=False, marks={"standing": 1.2}, note="job_car_enter backwards: out of the seat to standing at the same point, facing the car"),
    Clip("job_car_exit_l", 1.5, reversed_(car_enter("L")), loop=False, marks={"standing": 1.2}, note="job_car_enter_l backwards"),
    Clip("job_load_boot", 1.8, job_load_boot, loop=False, marks={"release": 1.0}, tool="parcel_m", grips=(BOX_R, BOX_L), tool_length=0.24,
         note="lowers a box carried in both arms on to a boot floor 0.52 m ahead and 0.70 m up, lets go and straightens"),
    Clip("job_flyer_hold", 5.0, job_flyer_hold, mask="left_arm", tool="flyer_stack", grips=(FLYERS,), tool_length=0.21,
         note="a stack of flyers held to the chest by the left hand; made to be layered over walking (left arm only)"),
    Clip("carry_box_hold", 5.0, carry_box_hold, mask="arms", tool="parcel_m", grips=(BOX_R, BOX_L), tool_length=0.24,
         note="a box in both arms; made to be layered over the game's walk or idle (arms only): the way to carry a parcel"),
    Clip("carry_box_walk", 1.1, carry_box_walk, speed=1.2, tool="parcel_m", grips=(BOX_R, BOX_L), tool_length=0.24, note="the whole body walking at 1.2 m/s with a box in both arms"),
    Clip("mop_floor", 3.2, mop_floor, tool="mop", grips=(MOP_LOW, MOP_TOP), tool_length=1.45, note="mopping in a figure of eight 0.7 m ahead"),
    Clip("stock_shelf", 2.6, stock_shelf, loop=False, marks={"grip": 0.6, "release": 1.5}, dressing=(("shelf_box", turn(), tuple(BOX_AT + (0.0, 0.0, -0.20))),),
         note="right hand takes from a box at the left hip (0.95 m) and sets it on a shelf 0.55 m ahead at 1.30 m"),
    Clip("serve_counter", 2.0, serve_counter, loop=False, marks={"grip": 0.28, "release": 1.1}, tool="coffee_cup", grips=(CUP,), tool_length=0.12,
         note="behind a counter 0.975 m high: lifts a cup from 0.33 m ahead and sets it down 0.78 m ahead for the customer"),
    Clip("till_tap", 1.6, till_tap, loop=False, mask="upper", marks={"tap": 0.5}, note="three taps with the right forefinger on a till 0.34 m ahead, 1.15 m up"),
    Clip("guitar_strum", 3.2, guitar_strum, tool="guitar", grips=(GUITAR_NECK,), tool_length=1.0, note="standing, strumming a guitar held across the body; left hand on the neck"),
]
