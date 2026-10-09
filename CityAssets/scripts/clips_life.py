"""Clips for the living city (Scripts/CityLife): the courier and the mail, the queue at a cart, the painter on a
ladder, the gardener, the park, tourists, people waiting by a door or a car, umbrellas. Same rules as clips.py.

Where things are, for whoever places the character (x right, y ahead, z up, from the character's feet):
    hand_truck_push      the truck's axle is 1.09 m ahead; it leans back 38 degrees on to the hands
    box_set_down         the box goes on the ground 0.40 m ahead (box_pick_up is the same backwards)
    mail_deliver         the letter box is 0.52 m ahead, 1.05 m up; the satchel hangs at the left hip
    paint_roll           the feet are ON THE FIRST TREAD of step_ladder (0.24 m up): put the character at the ladder's
                         pivot moved 0.24 m back, facing the wall; the wall is 0.80 m ahead of the character
    water_plants         the water lands 0.65 m ahead, about 0.5 m up (a planter)
    chess_sit            sitting on one of chess_table's stools (0.45 m): put the character at the stool's centre
                         facing the table, whose centre is then 0.62 m ahead
    lean_wait            leaning back on something 0.14 m behind the feet at hip height (a car's side)
    pay_tap              the card reader is 0.50 m ahead on a counter 0.975 m up
"""
import math

import numpy as np

from clips_jobs import BOX_CARRY, BOX_L, BOX_R, at_side
from motion import Clip, Walk, c, hang, idle, lag, s, shaft, shuffle, stand, weight  # noqa: F401
from rig import Grip, Pose, between, blend_holds, ease, keys, turn, turn_about, unit, wave  # noqa: F401

# ---------------------------------------------------------------------------------------------- deliveries

TRUCK_TILT = 38.0
TRUCK_AXLE = np.array([0.0, 1.09, 0.11])
TRUCK_R = Grip("R", at=(0.17, -0.04, 1.14), thumb=(0, 0.6, -0.8), back=(1, 0, 0), hold="bar")
TRUCK_L = Grip("L", at=(-0.17, -0.04, 1.14), thumb=(0, 0.6, -0.8), back=(-1, 0, 0), hold="bar")
TRUCK_WALK = Walk(1.2, 1.1)


def truck_pose(tilt=TRUCK_TILT, bump=0.0):
    turned = turn(pitch=-tilt)
    return turned, TRUCK_AXLE + (0.0, 0.0, bump) - turned @ np.array([0.0, 0.0, 0.11])


def hand_truck_push(t):
    # Walking behind a loaded sack truck leant back on to the hands: arms long, the weight forward into it.
    p = Pose()
    swing = TRUCK_WALK.body(p, t, lean=9, yaw=3, roll=1.5, sway=0.008, bob=0.018)
    p.chest(pitch=3, yaw=-4 * swing)
    p.tool(*truck_pose(TRUCK_TILT + 0.6 * s(t, 2), 0.003 * s(t, 4)), TRUCK_R, TRUCK_L, elbows={"R": (0.7, -0.6, -0.4), "L": (-0.7, -0.6, -0.4)})
    p.head(pitch=-4, yaw=2 * swing)
    return p


GROUND_BOX = np.array([0.0, 0.40, 0.0])


def box_set_down(t):
    # A box carried in both arms put down on the ground in front: down with the knees, the back straight, the
    # hands out from under it (let go at 1.2 s), and up again.
    down = keys(t, (0.0, 0.0), (0.50, 1.0), (0.60, 1.0), (1.0, 0.0))
    arms = keys(t, (0.06, 0.0), (0.52, 1.0), (0.58, 1.0), (1.0, 0.0))
    free = keys(t, (0.55, 0.0), (0.72, 1.0), (1.0, 1.0))
    p = Pose()
    stand(p, width=0.25, lean=52 * down, height=0.905 - 0.55 * down, shift=(0.0, 0.21 * down), left=(0.0, 0.05 * down), lift=(0.9 * down, 0.9 * down), toe_out=26)
    p.chest(pitch=2 + 4 * down)
    box = BOX_CARRY + (GROUND_BOX + (0.0, 0.0, 0.035) - BOX_CARRY) * arms          # the fingers are under it until it is let go
    if t < 0.55:
        p.tool(turn(), box, BOX_R, BOX_L, elbows={"R": (0.8, -0.5, -0.3), "L": (-0.8, -0.5, -0.3)})
    else:
        p.tool_free(turn(), GROUND_BOX)
        k = keys(t, (0.66, 0.0), (1.0, 1.0))
        for side, sign, grip in (("L", -1.0, BOX_L), ("R", 1.0, BOX_R)):
            wrist, hand = grip.wrist(turn(), box + (sign * 0.07 * free, -0.08 * free, 0.03 * free))
            rest = at_side(p, side)
            p.hand(side, wrist + (rest - wrist) * k, fingers=unit(hand @ np.array([sign, 0, 0]) * (1 - k) + np.array([sign * 0.1, 0.3, -1]) * k),
                   palm=(hand @ np.array([0, 0, -1])) * (1 - k) + np.array([-sign, 0, 0]) * k, elbow=(sign * 0.8, -0.5, -0.3), curl=blend_holds("plate", "relaxed", free))
    p.look_at(GROUND_BOX + (0, 0.9, 0.3), down=4)
    return p


def box_pick_up(t):
    return box_set_down(1.0 - t)


SLOT = np.array([0.12, 0.52, 1.05])
SATCHEL = np.array([-0.22, 0.08, 0.98])               # the mouth of the bag at the left hip


def mail_deliver(t):
    # The left hand lifts the satchel's flap and steadies it, the right goes in, comes out with a letter (0.7 s),
    # posts it through a slot in front (1.5 s) and comes back.
    dip = keys(t, (0.0, 0.0), (0.20, 1.0), (0.30, 1.0), (0.48, 0.0), (1.0, 0.0))
    post = keys(t, (0.30, 0.0), (0.56, 1.0), (0.70, 1.0), (1.0, 0.0))
    push = math.sin(math.pi * keys(t, (0.54, 0.0), (0.72, 1.0)))
    has = keys(t, (0.22, 0.0), (0.28, 1.0), (0.62, 1.0), (0.68, 0.0))
    p = Pose()
    stand(p, width=0.16, lean=4 * dip + 7 * post, sway=-0.5 * dip + 0.5 * post, shift=(0.0, 0.05 * post), right=(0.0, 0.08 * post), yaw=-8 * dip + 4 * post)
    p.chest(pitch=2 + 3 * dip, yaw=-16 * dip + 5 * post, roll=-3 * dip)
    rest = at_side(p, "R")
    wrist = rest + (SATCHEL + (0.10, 0.02, 0.06) - rest) * dip + (SLOT + (0.03, -0.11 + 0.05 * push, 0.02) - rest) * post
    p.hand("R", wrist + (0, 0.05 * math.sin(math.pi * post), 0.04 * math.sin(math.pi * post)),
           fingers=unit(np.array([0.1, 0.3, -1]) * (1 - post) * (1 - dip) + np.array([-0.6, 0.2, -0.75]) * dip + np.array([-0.1, 1.0, 0.0]) * post),
           palm=np.array([-1, 0, 0]) * (1 - post) * (1 - dip) + np.array([-0.4, -0.6, -0.5]) * dip + np.array([-0.95, 0.0, -0.3]) * post, elbow=(0.7, -0.6, -0.4),
           curl=blend_holds("relaxed", "pinch", has))
    hold = keys(t, (0.0, 0.0), (0.14, 1.0), (0.40, 1.0), (0.56, 0.0), (1.0, 0.0))
    left = at_side(p, "L")
    p.hand("L", left + (SATCHEL + (-0.06, 0.10, 0.03) - left) * hold, fingers=unit(np.array([-0.1, 0.3, -1]) * (1 - hold) + np.array([0.3, 0.6, -0.75]) * hold),
           palm=np.array([1, 0, 0]) * (1 - hold) + np.array([0.6, -0.3, -0.75]) * hold, elbow=(-0.7, -0.7, 0.0), curl=blend_holds("relaxed", "pinch", hold))
    p.look_at(SATCHEL * dip + SLOT * (1 - dip) + (0, 0.2, 0.0), down=26)
    return p


# ---------------------------------------------------------------------------------------------- weather

UMBRELLA = Grip("R", at=(0, 0, 0.06), thumb=(0, 0, 1), back=(1, 0, 0), hold="handle")


def umbrella_hold(t):
    # An open umbrella held up in the right hand. Made for the right arm only (mask "right_arm"): over the game's
    # walk it is someone walking in the rain.
    p = Pose()
    sway = idle(p, t, width=0.15, amount=0.5)
    p.chest(pitch=1, roll=1.0 * sway)
    foot = np.array([0.20 + 0.01 * sway, 0.24, 1.34 + 0.006 * s(t, 2)])
    top = foot + unit(np.array([-0.13 + 0.015 * s(t), -0.14, 1.0]))
    p.tool(*shaft(foot, top, lean_to=(1.0, 0.0, 0.0), level=False), UMBRELLA, elbows={"R": (0.5, -0.5, -0.8)})
    hang(p, "L", sway)
    p.head(pitch=1)
    return p


# ---------------------------------------------------------------------------------------------- painter and gardener

TREAD = 0.24                                          # the first tread of step_ladder
WALL = 0.80                                           # ahead of the character standing on it
ROLLER = Grip("R", at=(0, 0, 0.07), thumb=(0, 0, 1), back=(0, -1, 0), hold="handle")
ROLLER_LENGTH = 0.40


def paint_roll(t):
    # On the second step of a ladder, rolling paint on to the wall in long strokes up and down, moving across a
    # roller's width each time; the free hand on the hip, the body rises and reaches with the stroke.
    up = wave(t * 2)                                  # two strokes a loop
    across = keys(t, (0.0, 0.0), (0.42, 0.0), (0.56, 1.0), (0.92, 1.0), (1.0, 0.0))
    late = wave(lag(t, 0.03) * 2)
    roller = np.array([0.30 - 0.20 * across, WALL - 0.05, TREAD + 1.36 + 0.44 * up])
    handle = roller - unit(np.array([0.05, 0.80, 0.60 - 0.25 * late])) * ROLLER_LENGTH
    p = Pose()
    stand(p, width=0.13, lean=6 + 4 * up, sway=0.3 - 0.5 * across, height=0.905 + 0.012 * up, floor=TREAD, toe_out=4, lift=(0.0, 0.0))
    p.chest(pitch=-1 - 3 * up, yaw=6 - 8 * across, roll=-3 * up)
    p.tool(*shaft(handle, roller, lean_to=(1.0, 0.0, 0.0)), ROLLER, elbows={"R": (0.8, -0.5, -0.3)})
    p.shrug("R", 0.5 * up)
    p.hand("L", p._hips[0] + np.array([-0.235, 0.05, 0.075]), fingers=(-0.2, 0.5, -0.85), palm=(1, 0, 0), elbow=(-0.7, -0.7, 0.0), curl="relaxed")      # on the hip
    p.look_at(roller, down=20, up=38)
    return p


CAN = Grip("R", at=(0.0, -0.02, 0.34), thumb=(0, 1, 0), back=(1, 0, 0), hold="handle")


def water_plants(t):
    # A watering can in the right hand: carried level, then tipped out over a planter in front, moved along it,
    # and brought level again.
    pour = keys(t, (0.0, 0.0), (0.16, 1.0), (0.70, 1.0), (0.84, 0.0), (1.0, 0.0))
    along = keys(t, (0.16, -1.0), (0.44, 1.0), (0.70, -0.4))
    p = Pose()
    stand(p, width=0.17, lean=15 * pour, sway=0.4 * along * pour, shift=(0.0, 0.04 * pour), left=(0.0, 0.06), right=(0.0, -0.04), yaw=5 * along * pour)
    p.chest(pitch=2 + 2 * pour, yaw=6 * along * pour, roll=2 * pour)
    hips = p._hips[0]
    carried = hips + np.array([0.27, 0.06, -0.905 + 0.44])
    tipped = np.array([0.20 + 0.08 * along, 0.19, 0.62])
    can = carried + (tipped - carried) * pour
    p.tool(turn(yaw=8 * along * pour, pitch=34 * pour + 3 * s(t, 3) * pour), can, CAN, elbows={"R": (0.6, -0.8, -0.1)})
    hang(p, "L", swing=0.2 * pour)
    p.look_at((0.2 + 0.2 * along, 0.75, 0.5), down=24)
    return p


RAKE_LOW = Grip("R", at=(0, 0, 0.95), thumb=(0, 0, 1), back=(1, 0, 0))
RAKE_TOP = Grip("L", at=(0, 0, 1.30), thumb=(0, 0, 1), back=(-1, 0, 0))


def rake_leaves(t):
    # The rake reached out ahead, set down and drawn back towards the feet; lifted and reached out again a little
    # to one side. The pull comes from the legs and the turn of the body, the arms only steer.
    pull = keys(t, (0.0, 0.0), (0.12, 0.0), (0.56, 1.0), (0.66, 1.0), (1.0, 0.0))
    lift = math.sin(math.pi * keys(t, (0.62, 0.0), (1.0, 1.0))) * 0.10
    side = s(t, 0.5)
    tines = np.array([0.22 - 0.05 * pull, 0.74 - 0.28 * pull, lift])
    top = np.array([-0.08 + 0.05 * pull, 0.27 - 0.24 * pull, 1.08 + 0.14 * pull])
    p = Pose()
    stand(p, height=0.90, width=0.18, yaw=6 - 8 * pull, lean=28 - 15 * pull, sway=-0.5 + 1.0 * pull, shift=(0.0, 0.05 - 0.05 * pull), left=(0.0, 0.14), right=(0.02, -0.10),
          lift=(0.0, 0.4 * (1 - pull)), toe_out=10)
    p.chest(yaw=8 - 14 * pull, pitch=4, roll=2 - 3 * pull)
    p.tool(*shaft(tines, top), RAKE_LOW, RAKE_TOP, elbows={"R": (0.8, -0.5, -0.3), "L": (-0.8, -0.4, -0.2)})
    p.look_at(tines + (0, 0.4, 0.1), down=16)
    return p


# ---------------------------------------------------------------------------------------------- the park

def tai_chi(t):
    # "Wave hands like clouds": a wide, sunk stance; the weight flows from one leg to the other and back, the waist
    # turns with it, and the hands circle past each other, one rising across the face as the other sinks to the hip.
    flow = s(t, 2)                                    # -1 over the left leg .. +1 over the right, twice a loop
    late = s(lag(t, 0.02), 2)
    later = s(lag(t, 0.04), 2)
    breath = wave(t * 4)
    p = Pose()
    stand(p, height=0.83 + 0.012 * breath, width=0.27, sway=1.6 * flow, yaw=12 * flow, toe_out=16, lean=2)
    p.chest(yaw=14 * late, pitch=1)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        high = 0.5 + 0.5 * later * sign              # this hand is the upper one when the weight is on its own side
        wrist = np.array([sign * (0.10 + 0.17 * high) + 0.11 * later, 0.31 + 0.05 * high, 0.98 + 0.42 * high])
        p.hand(side, wrist, fingers=unit((-sign * (0.75 - 0.5 * high), 0.35, 0.25 + 0.75 * high)), palm=(-sign * 0.3, -0.75 + 1.2 * (1 - high), -0.2 - 0.6 * (1 - high)),
               elbow=(sign * 0.8, -0.3, -0.5), curl=(0.1, 0.12, 0.14, 0.16, 0.18, 0.1, 0.3))
        p.shrug(side, 0.0)
    p.look_at((0.9 * later, 1.2, 1.35), limit=40)
    return p


MAP_L = Grip("L", at=(-0.19, 0.004, 0.10), thumb=(0, 0, 1), back=(0, 1, 0), hold="pinch")
MAP_R = Grip("R", at=(0.19, 0.004, 0.10), thumb=(0, 0, 1), back=(0, 1, 0), hold="pinch")


def map_read(t):
    # A map held open in both hands: studied, then the head comes up and looks down one street and the other, the
    # body turning with the map as if to match it to the place.
    up = keys(t, (0.0, 0.0), (0.34, 0.0), (0.44, 1.0), (0.84, 1.0), (0.94, 0.0), (1.0, 0.0))
    look = keys(t, (0.40, 0.0), (0.52, -1.0), (0.66, 1.0), (0.84, 1.0), (0.96, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.16, yaw=8 * look * up, amount=0.8)
    p.chest(pitch=3 - 2 * up, yaw=8 * look * up, roll=1.5 * sway)
    page = (turn(yaw=14 * look * up, pitch=38 - 10 * up), np.array([0.0 + 0.02 * sway, 0.33, 1.06 + 0.02 * up]))
    p.tool(*page, MAP_R, MAP_L, elbows={"R": (0.7, -0.6, -0.5), "L": (-0.7, -0.6, -0.5)})
    p.head(pitch=26 - 28 * up, yaw=34 * look * up)
    return p


PAPER_L = Grip("L", at=(-0.25, 0.006, 0.13), thumb=(0, 0, 1), back=(0, 1, 0), hold="pinch")
PAPER_R = Grip("R", at=(0.25, 0.006, 0.13), thumb=(0, 0, 1), back=(0, 1, 0), hold="pinch")


def read_paper(t):
    # A newspaper held open in both hands: read down one page and across to the other, the paper given a shake to
    # straighten it, a glance over the top of it at the street.
    shake = keys(t, (0.46, 0.0), (0.50, 1.0), (0.56, 0.0)) * s(t, 26)
    over = keys(t, (0.0, 0.0), (0.70, 0.0), (0.78, 1.0), (0.90, 1.0), (1.0, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.16, amount=0.6)
    p.chest(pitch=2, roll=1.2 * sway)
    page = (turn(pitch=26 + 2 * shake - 6 * over), np.array([0.02 * sway, 0.36, 1.10 + 0.012 * shake - 0.05 * over]))
    p.tool(*page, PAPER_R, PAPER_L, elbows={"R": (0.8, -0.5, -0.5), "L": (-0.8, -0.5, -0.5)})
    scan = keys(t, (0.0, -1.0), (0.42, -1.0), (0.56, 1.0), (0.70, 1.0), (1.0, -1.0))
    p.head(pitch=22 - 6 * wave(t * 3) * (1 - over) - 20 * over, yaw=9 * scan * (1 - over) - 22 * over)
    return p


# ---------------------------------------------------------------------------------------------- waiting

def folded(p, sway=0.0, breath=0.0):
    """Arms crossed on the chest: the right forearm over the left, each hand tucked at the other elbow."""
    hips, pelvis = p._hips
    chest = pelvis @ p._chest
    base = hips + pelvis @ np.array([0.0, 0.0, 0.09]) + chest @ np.array([0.0, 0.0, 0.12])
    p.hand("R", base + chest @ np.array([-0.125, 0.20 + 0.006 * breath, 0.06]), fingers=chest @ np.array([-0.9, -0.3, 0.3]), palm=chest @ np.array([0.2, -0.9, -0.2]),
           elbow=chest @ np.array([0.8, -0.2, -0.6]), curl="cup")
    p.hand("L", base + chest @ np.array([0.11, 0.155 + 0.006 * breath, 0.02]), fingers=chest @ np.array([0.9, 0.1, 0.4]), palm=chest @ np.array([-0.2, -0.9, 0.1]),
           elbow=chest @ np.array([-0.8, -0.2, -0.6]), curl="cup")
    p.shrug("L", 0.15).shrug("R", 0.15)


def arms_crossed(t):
    # Standing square with the arms folded: a doorman or someone waiting who is not going anywhere; looks slowly
    # one way and the other, rocks on the heels.
    p = Pose()
    sway = idle(p, t, width=0.19, amount=0.6, lean=-1, toe_out=14)
    p.chest(pitch=-2, roll=1.2 * sway)
    folded(p, sway, s(t, 3))
    p.head(yaw=keys(t, (0.0, -24), (0.30, -24), (0.44, 20), (0.80, 20), (1.0, -24)), pitch=2)
    return p


def lean_wait(t):
    # Leaning back against a car's side with the arms folded, one foot crossed over the other; watches the street,
    # shifts, looks at the other end of it.
    shift = keys(t, (0.0, 0.0), (0.46, 0.0), (0.54, 1.0), (0.92, 1.0), (1.0, 0.0))
    p = Pose()
    p.hips((0.02 - 0.04 * shift, -0.14, 0.845), pitch=5, roll=2 - 4 * shift, yaw=4 - 8 * shift)      # the seat against the car, the trunk upright over it
    p.foot("L", (-0.12, 0.12, 0.0), yaw=-10)
    p.foot("R", (0.01, 0.22, 0.0), yaw=14, pitch=0)                # crossed in front of the left
    p.chest(pitch=3, roll=-1 + 2 * shift)
    folded(p, 0.0, s(t, 3))
    p.head(yaw=keys(t, (0.0, 26), (0.40, 26), (0.52, -30), (0.90, -30), (1.0, 26)), pitch=0)
    return p


def guard_stand(t):
    # Feet apart, hands clasped in front at the belt, upright: someone on a door. The head goes steadily along
    # the street and back, a rise on the toes now and then.
    rise = math.sin(math.pi * keys(t, (0.60, 0.0), (0.74, 1.0)))
    p = Pose()
    sway = idle(p, t, width=0.20, amount=0.45, toe_out=14, height=0.905 + 0.012 * rise)
    p.chest(pitch=-1, roll=1.0 * sway)
    hips = p._hips[0]
    p.hand("R", hips + np.array([0.035, 0.150, 0.015]), fingers=(-0.85, 0.2, -0.5), palm=(0.2, -0.95, 0.2), elbow=(0.8, -0.5, -0.1), curl="cup")
    p.hand("L", hips + np.array([-0.045, 0.125, 0.0]), fingers=(0.85, 0.2, -0.5), palm=(-0.2, -0.95, 0.2), elbow=(-0.8, -0.5, -0.1), curl="cup")
    p.head(yaw=keys(t, (0.0, -30), (0.22, -30), (0.46, 30), (0.70, 30), (1.0, -30)), pitch=1)
    return p


def queue_wait(t):
    # In a queue: weight from foot to foot, a lean out to see how far the front is, a look at the watch on the
    # left wrist, a sigh.
    peer = keys(t, (0.0, 0.0), (0.12, 0.0), (0.22, 1.0), (0.36, 1.0), (0.46, 0.0), (1.0, 0.0))
    watch = keys(t, (0.52, 0.0), (0.62, 1.0), (0.76, 1.0), (0.86, 0.0))
    p = Pose()
    sway = idle(p, t, width=0.15, phase=0.1)
    stand(p, width=0.15, sway=sway * (1 - peer) + 1.3 * peer, lean=2 * peer, left=(0.0, 0.0), lift=(0.3 * peer, 0.0))
    p.chest(pitch=1 + 2 * watch, roll=5 * peer + 1.2 * sway, yaw=4 * peer)
    left = at_side(p, "L")
    wrist = np.array([-0.04, 0.27, 1.18])
    p.hand("L", left + (wrist - left) * watch, fingers=unit(np.array([-0.1, 0.3, -1]) * (1 - watch) + np.array([0.95, 0.3, 0.05]) * watch), palm=np.array([1, 0, 0]) * (1 - watch) + np.array([0.0, 0.25, -1.0]) * watch,
           elbow=(-0.8, -0.4, -0.4), curl=blend_holds("relaxed", "fist", 0.5 * watch))
    hang(p, "R", sway)
    p.head(pitch=1 + 24 * watch - 2 * peer, yaw=-8 * watch + 10 * peer, roll=6 * peer)
    return p


def pay_tap(t):
    # A card or a phone held to the reader on the counter with the right hand, kept there a moment, and put away.
    out = keys(t, (0.0, 0.0), (0.34, 1.0), (0.62, 1.0), (1.0, 0.0))
    body = keys(t, (0.0, 0.0), (0.30, 1.0), (0.64, 1.0), (1.0, 0.0))
    p = Pose()
    stand(p, width=0.15, lean=7 * body, sway=0.3 * body, shift=(0.0, 0.03 * body))
    p.chest(pitch=1 + 2 * body, yaw=-5 * body)
    rest, pocket = at_side(p, "R"), np.array([0.20, 0.10, 0.96])
    reader = np.array([0.10, 0.42, 1.07 - 0.02 * math.sin(math.pi * keys(t, (0.34, 0.0), (0.50, 1.0)))])
    wrist = keys(out, (0.0, rest), (0.25, pocket), (1.0, reader))
    p.hand("R", wrist, fingers=unit(keys(out, (0.0, (0.1, 0.3, -1)), (1.0, (-0.2, 0.95, -0.2)))), palm=keys(out, (0.0, (-1, 0, 0)), (1.0, (-0.3, 0.2, -0.95))), elbow=(0.7, -0.6, -0.4),
           curl=blend_holds("relaxed", "pinch", min(1.0, out * 3)))
    hang(p, "L")
    p.head(pitch=2 + 16 * body)
    return p


# ---------------------------------------------------------------------------------------------- chess

SEAT = 0.45                                           # the top of a stool
TABLE = 0.74
BOARD = np.array([0.0, 0.62, TABLE])                  # the middle of the board from the stool's centre


def chess_sit(t):
    # Sitting at the chess table: elbows on it, chin on the left hand, thinking; then the right hand goes out,
    # lifts a piece, sets it down two squares on, and the player sits back to wait.
    think = keys(t, (0.0, 1.0), (0.34, 1.0), (0.44, 0.0), (0.74, 0.0), (0.86, 1.0), (1.0, 1.0))
    move = keys(t, (0.36, 0.0), (0.50, 1.0), (0.62, 1.0), (0.76, 0.0))
    carry = keys(t, (0.50, 0.0), (0.60, 1.0))
    back = keys(t, (0.62, 0.0), (0.76, 1.0), (0.90, 1.0), (1.0, 0.0))
    p = Pose()
    p.hips((0.0, -0.02 - 0.03 * back, SEAT + 0.085), pitch=16 * think + 22 * move - 4 * back, roll=1.5 * s(t))
    p.foot("L", (-0.15, 0.30 + 0.03 * s(t, 1, 0.2), 0.0), yaw=-10, knee=(-0.25, 1.0, 0.6))
    p.foot("R", (0.15, 0.26 - 0.10 * back, 0.0), yaw=10, knee=(0.25, 1.0, 0.6))
    p.chest(pitch=6 * think + 3 * move, yaw=-4 * move)
    # The left elbow on the table edge and the chin on the fist, while thinking; else the forearm lies on the table.
    chin = np.array([-0.07, 0.25, TABLE + 0.39])
    lying = np.array([-0.20, 0.36, TABLE + 0.035])
    p.hand("L", lying + (chin - lying) * think, fingers=unit(np.array([0.5, 0.85, 0.0]) * (1 - think) + np.array([0.55, -0.1, 0.8]) * think), palm=np.array([0, 0, -1]) * (1 - think) + np.array([0.6, -0.7, -0.3]) * think,
           elbow=(-0.6, 0.2, -0.8), curl=blend_holds("relaxed", "fist", 0.7 * think))
    resting = np.array([0.20, 0.36, TABLE + 0.035])
    piece = BOARD + (0.07, -0.16 + 0.10 * carry, 0.10 + 0.05 * math.sin(math.pi * carry))
    p.hand("R", resting + (piece - resting) * move + (0, 0, 0.05 * math.sin(math.pi * move)), fingers=unit(np.array([-0.4, 0.9, 0.0]) * (1 - move) + np.array([-0.3, 0.75, -0.6]) * move),
           palm=np.array([0, 0, -1]) * (1 - move) + np.array([-0.2, -0.35, -0.9]) * move, elbow=(0.7, -0.3, -0.6), curl=blend_holds("relaxed", "pinch", move))
    p.look_at(BOARD + (0.05 * s(t, 3), 0.0, 0.0 + 0.5 * back), down=30)
    return p


CLIPS = [
    Clip("hand_truck_push", 1.1, hand_truck_push, speed=1.2, tool="hand_truck", grips=(TRUCK_R, TRUCK_L), tool_length=1.2,
         note="walking at 1.2 m/s behind the sack truck leant back 38 degrees; its axle is 1.09 m ahead of the feet"),
    Clip("box_set_down", 2.2, box_set_down, loop=False, marks={"release": 1.2}, tool="parcel_m", grips=(BOX_R, BOX_L), tool_length=0.24,
         note="a box carried in both arms is put on the ground 0.40 m ahead, and up again"),
    Clip("box_pick_up", 2.2, box_pick_up, loop=False, marks={"grip": 1.0}, tool="parcel_m", grips=(BOX_R, BOX_L), tool_length=0.24,
         note="box_set_down backwards: down to a box on the ground 0.40 m ahead and up with it; ends carrying (carry_box_hold)"),
    Clip("mail_deliver", 2.6, mail_deliver, loop=False, marks={"grip": 0.7, "release": 1.6}, dressing=(("mail_satchel", turn(), (0.0, 0.0, 1.30)),),
         note="takes a letter from the satchel at the left hip and posts it through a slot 0.52 m ahead, 1.05 m up"),
    Clip("umbrella_hold", 6.0, umbrella_hold, mask="right_arm", tool="umbrella", grips=(UMBRELLA,), tool_length=0.86,
         note="an open umbrella held up in the right hand; made to be layered over the game's walk (right arm only)"),
    Clip("paint_roll", 4.4, paint_roll, tool="paint_roller", grips=(ROLLER,), tool_length=ROLLER_LENGTH, dressing=(("step_ladder", turn(), (0.0, 0.24, 0.0)),),
         note="standing on the first tread of step_ladder (0.24 m up), rolling a wall 0.80 m ahead between 1.6 and 2.0 m"),
    Clip("water_plants", 6.0, water_plants, tool="watering_can", grips=(CAN,), tool_length=0.3, note="watering a planter 0.65 m ahead from a can in the right hand"),
    Clip("rake_leaves", 2.8, rake_leaves, tool="rake", grips=(RAKE_LOW, RAKE_TOP), tool_length=1.55, note="raking towards the feet from 0.8 m ahead"),
    Clip("tai_chi", 12.0, tai_chi, note="slow 'cloud hands' in a wide stance; needs 0.8 m clear either side"),
    Clip("map_read", 8.0, map_read, tool="map_folded", grips=(MAP_R, MAP_L), tool_length=0.3, note="a map open in both hands: studied, then a look up and down the street"),
    Clip("read_paper", 9.0, read_paper, tool="newspaper", grips=(PAPER_R, PAPER_L), tool_length=0.4, note="standing reading a newspaper held open in both hands"),
    Clip("arms_crossed", 8.0, arms_crossed, note="standing square with folded arms, looking up and down the street"),
    Clip("lean_wait", 9.0, lean_wait, note="leaning back on a car's side 0.14 m behind the feet, arms folded, ankles crossed"),
    Clip("guard_stand", 8.0, guard_stand, note="feet apart, hands clasped in front: on a door"),
    Clip("queue_wait", 9.0, queue_wait, watch="L", note="waiting in a queue: a lean out to see the front, a look at the watch"),
    Clip("pay_tap", 1.8, pay_tap, loop=False, mask="upper", marks={"tap": 0.7}, note="a card held to a reader 0.50 m ahead on a counter 0.975 m up"),
    Clip("chess_sit", 10.0, chess_sit, dressing=(("chess_table", turn(), (0.0, 0.62, 0.0)),),
         note="sitting on a stool of chess_table (0.45 m), thinking and making a move; the table's centre is 0.62 m ahead"),
]
