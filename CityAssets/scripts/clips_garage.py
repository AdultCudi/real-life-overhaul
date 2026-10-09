"""Clips for the Garage: riding the two bicycles and the two motorbikes of PropAssets/scripts/props_garage.py, made
to how Scripts/Garage/BikeMount.cs plays them.

BikeMount plays one of these by what the rider is doing (BikeClips in BikeMount.cs), whole body, at the clip's own
speed (ActivityAnimator.Play): pedalling, freewheeling or stopped on a bicycle (cycle_ride, cycle_coast, cycle_stand,
and the same three with _road on the road bicycle, whose drop bar is lower and further forward); riding or stopped
on a motorbike (moto_ride, moto_stand; moto_ride_sport, moto_stand_sport on the sport bike's clip-ons). It puts the
rider's origin at the vehicle's (0, 0, seat z + 0.04), on the ground, facing the way the bike faces. So IN EVERY CLIP HERE THE CHARACTER'S ORIGIN IS ON THE GROUND 0.04 m
AHEAD OF THE MIDDLE OF THE SEAT (the bike's own origin is 0.26 m ahead of it on bicycle_city, 0.16 m on
motorbike_standard, 0.18 m on motorbike_sport), and the root never moves. Afterwards, each frame, the code does
two things to the pose, and a third it does not do:

    hands   the game's own hand rig is aimed at the code's grips (BikeDef.Grip), which turn with the steering;
            the clips put the palms on those same grips.
    legs    on a BICYCLE the code puts both ankles 0.10 m above and 0.03 m behind the middle of the pedals
            (LegIk, knees forward), following its own crank, which turns only while the rider pedals; while a
            _stand clip plays it lets the legs go (the crank is brought round to the right pedal forward). The feet
            keep the turn the clip gave them. So cycle_ride's legs are never seen: they are drawn pedalling the
            way the code places them, the feet 8 degrees toes-down throughout. The rider sits 0.058 m over the
            saddle so that the code's ankle leaves the knee some 20 degrees of bend at the bottom of the stroke.
            On a MOTORBIKE the legs are the clip's: the feet are where the clip has them.
    helmet  it follows the Head bone, turn and all (it used to keep the player's rotation, which is why the
            motorbike clips look no more than 9 degrees to either side).

The numbers are BikeCatalogue.All (BikeBody.cs), here in the bike's own space (x right, y ahead, z up; Unity is
(x, z, y)); checks_garage.py measures the same ones on the meshes:

    bicycle_city        saddle top (0, -0.30, 0.93); bottom bracket (0, -0.08, 0.29); grips along X (+-0.26, 0.16, 1.14)
    bicycle_road        saddle top (0, -0.29, 0.95); bottom bracket (0, -0.07, 0.28); grips (+-0.13, 0.42, 1.00): the
                        tops of its drop bar
    both                cranks 0.17 m, a pedal's middle 0.13 m out; crank angle 0 = the right crank forward, and it
                        DECREASES as they pedal (the forward pedal goes down): BikeBody.PedalAt
    motorbike_standard  seat top (0, -0.20, 0.83); pegs (+-0.24, -0.10, 0.34); grips (+-0.31, 0.30, 1.075)
    motorbike_sport     seat top (0, -0.22, 0.84); pegs (+-0.24, -0.16, 0.38); grips (+-0.27, 0.42, 0.915): clip-ons

The Hips bone rides over the middle of the seat, 0.058 m above a saddle and 0.085 m above a motorbike's seat; the
middle of each palm is on the grip's axis; on a motorbike the soles are on the pegs (the arch on the standard
bike's, the ball of the foot on the sport bike's).
"""
import math

import numpy as np

from motion import TAU, Clip, c, s
from rig import J, PALM, Pose, basis, keys, turn, unit, wave  # noqa: F401

# ---------------------------------------------------------------------------------------------- the code's numbers

CRANK = 0.17                                          # BikeCatalogue.CrankLength
PEDAL_OUT = 0.13                                      # BikeCatalogue.PedalOut
ANKLE_UP, ANKLE_BACK = 0.10, 0.03                     # BikeMount.AnkleAbovePedal, and the 0.03 it takes off forwards
SEAT_AHEAD = 0.04                                     # BikeMount.Mounted: the rider's origin ahead of the seat's middle

CITY = {"prop": "bicycle_city", "seat": (-0.30, 0.93), "grip": (0.26, 0.16, 1.14), "crank": (-0.08, 0.29)}
ROAD = {"prop": "bicycle_road", "seat": (-0.29, 0.95), "grip": (0.13, 0.42, 1.00), "crank": (-0.07, 0.28)}
STANDARD = {"prop": "motorbike_standard", "seat": (-0.20, 0.83), "grip": (0.31, 0.30, 1.075), "peg": (0.24, -0.10, 0.34), "knee": (0.215, 0.13, 0.65)}
SPORT = {"prop": "motorbike_sport", "seat": (-0.22, 0.84), "grip": (0.27, 0.42, 0.915), "peg": (0.24, -0.16, 0.38), "knee": (0.205, 0.08, 0.62)}
PEG_RADIUS = 0.016

SADDLE_OVER = 0.058                                   # the Hips bone above a saddle's top
SEAT_OVER = 0.085                                     # and above a motorbike's seat
ANKLE = float(J["RightFoot"][2])                      # the ankle above the sole
BALL = 0.115                                          # the ball of the foot ahead of the ankle
ARCH = 0.055
FOOT_PITCH = 8.0                                      # the feet in cycle_ride: the code keeps this turn and moves the ankles

TURNS = 5                                             # crank turns in cycle_ride's 4 s: 75 a minute, the code's cadence at 20 km/h


def origin(bike):
    """The rider's origin in the bike's space."""
    return np.array([0.0, bike["seat"][0] + SEAT_AHEAD, 0.0])


def here(bike, point):
    """A point of the bike, in the clip's space."""
    return np.asarray(point, dtype=float) - origin(bike)


def seat(bike, over):
    return here(bike, (0.0, bike["seat"][0], bike["seat"][1] + over))


def mirror(point, sign):
    return np.array([sign * point[0], point[1], point[2]])


def palm_on(p, side, centre, fingers, palm, elbow=None, curl="handle"):
    """A hand placed by the middle of its palm: on a grip's axis, the fingers closing round it."""
    sign = 1.0 if side == "R" else -1.0
    hand = basis(fingers, palm) @ basis((sign, 0, 0), (0, 0, -1)).T
    p.hand(side, np.asarray(centre, dtype=float) - hand @ PALM[side], turned=hand, elbow=elbow, curl=curl)


def on_grip(p, bike, side, drop, elbow=None):
    """A hand on the bike's grip, which lies along X: the forearm comes down on to it at `drop` degrees, the wrist straight."""
    sign = 1.0 if side == "R" else -1.0
    a = math.radians(drop)
    palm_on(p, side, here(bike, mirror(bike["grip"], sign)), (sign * -0.06, math.cos(a), -math.sin(a)), (0.0, -math.sin(a), -math.cos(a)),
            elbow=elbow or (sign * 0.42, -0.55, -0.72), curl="handle")


def sole_on(p, side, point, pitch=0.0, ahead=BALL, yaw=0.0, knee=None, knee_at=None):
    """A foot whose sole, `ahead` of the ankle, is on a point (the top of a peg). knee_at: a point the knee bends
    towards (it ends up in the plane of the hip joint, the ankle and that point)."""
    turned = turn(yaw, pitch)
    if knee_at is not None:
        hips, pelvis = p._hips
        word = "Right" if side == "R" else "Left"
        knee = np.asarray(knee_at, dtype=float) - (hips + pelvis @ (J[word + "UpperLeg"] - J["Hips"]))
    p.foot(side, ankle=np.asarray(point, dtype=float) + turned @ np.array([0.0, -ahead, ANKLE]), yaw=yaw, pitch=pitch, knee=knee)


# ---------------------------------------------------------------------------------------------- bicycles

def pedal(bike, side, angle):
    """BikeBody.PedalAt: the middle of a pedal with the crank at an angle (radians; 0 = the right crank forward)."""
    sign = 1.0 if side == "R" else -1.0
    a = angle + (0.0 if side == "R" else math.pi)
    by, bz = bike["crank"]
    return here(bike, (sign * PEDAL_OUT, by + math.cos(a) * CRANK, bz + math.sin(a) * CRANK))


def cyclist(bike, angle, lean, chest, rock=1.0, look=(0.0, 9.0, 1.50), sit=(0.0, 0.0, 0.0), drop=34.0, twist=0.0, tip=0.0, elbow=None):
    """Seated on a bicycle with the cranks at an angle, the legs as BikeMount's LegIk leaves them."""
    p = Pose()
    push = math.sin(angle + 0.9)                      # +1 as the right leg pushes down, -1 the left
    p.hips(seat(bike, SADDLE_OVER) + np.asarray(sit) + (0.004 * rock * push, 0.0, 0.002 * rock * math.cos(2 * angle)), pitch=lean, roll=1.6 * rock * push + tip, yaw=-1.2 * rock * math.cos(angle + 0.9) + 0.25 * twist)
    p.chest(pitch=chest, roll=-1.2 * rock * push - 0.6 * tip, yaw=0.9 * rock * math.cos(angle + 0.9) + twist)        # the shoulders stay level over the bars
    for side, sign in (("L", -1.0), ("R", 1.0)):
        p.foot(side, ankle=pedal(bike, side, angle) + (0.0, -ANKLE_BACK, ANKLE_UP), pitch=FOOT_PITCH, knee=(sign * 0.10, 1.0, 0.25))
        on_grip(p, bike, side, drop, elbow=(sign * elbow[0], elbow[1], elbow[2]) if elbow else None)
    p.look_at(np.asarray(look, dtype=float))
    return p


def cycle_ride(t):
    # Upright town riding. Five turns of the cranks; over them a slow sway of the back and two short looks aside.
    glance = keys(t, (0.0, 0.0), (0.30, 0.0), (0.40, 1.0), (0.50, 0.0), (0.72, 0.0), (0.82, -0.8), (0.92, 0.0), (1.0, 0.0))
    return cyclist(CITY, -TAU * TURNS * t, 9.0 + 1.0 * s(t), 3.0, look=(3.5 * glance, 9.0, 1.50 - 0.10 * abs(glance)), sit=(0.004 * s(t), 0.0, 0.0), twist=4.0 * glance)


def cycle_coast(t):
    # Freewheeling: cranks level with the right foot forward, the weight a little back, looking about.
    glance = keys(t, (0.0, 0.0), (0.12, 0.0), (0.26, 1.0), (0.40, 1.0), (0.52, 0.0), (0.62, -0.7), (0.76, -0.7), (0.90, 0.0), (1.0, 0.0))
    return cyclist(CITY, 0.0, 8.0 + 2.0 * s(t), 3.0 + 1.5 * c(t, 2), rock=0.0, look=(7.0 * glance, 8.0, 1.48 - 0.25 * abs(glance)), sit=(0.014 * glance + 0.004 * s(t), -0.006 * abs(glance), 0.003 * c(t, 2)),
                   twist=9.0 * glance, tip=2.5 * glance)


# The road bicycle: the hands on the tops of the drop bar, 0.12 m lower and 0.24 m further forward than the town
# bar and only 0.26 m apart, so the back is well forward and the elbows hang narrow under the shoulders.
ROAD_LEAN, ROAD_CHEST = 30.0, 12.0
ROAD_ELBOW = (0.16, -0.30, -1.0)
ROAD_DROP = 22.0


def cycle_ride_road(t):
    # Road riding on the tops: the back 42 degrees forward and quiet, the hips rocking a little with the stroke,
    # the head up along the road with one look over each shoulder's side.
    glance = keys(t, (0.0, 0.0), (0.30, 0.0), (0.40, 1.0), (0.50, 0.0), (0.72, 0.0), (0.82, -0.8), (0.92, 0.0), (1.0, 0.0))
    return cyclist(ROAD, -TAU * TURNS * t, ROAD_LEAN + 1.0 * s(t), ROAD_CHEST, look=(3.0 * glance, 9.0, 1.42 - 0.08 * abs(glance)), sit=(0.004 * s(t), 0.0, 0.0), drop=ROAD_DROP, twist=3.0 * glance,
                   elbow=ROAD_ELBOW)


def cycle_coast_road(t):
    # Freewheeling on the road bicycle: cranks level, the right foot forward, the back a little higher, looking about.
    glance = keys(t, (0.0, 0.0), (0.12, 0.0), (0.26, 1.0), (0.40, 1.0), (0.52, 0.0), (0.62, -0.7), (0.76, -0.7), (0.90, 0.0), (1.0, 0.0))
    return cyclist(ROAD, 0.0, ROAD_LEAN - 2.0 + 1.5 * s(t), ROAD_CHEST - 1.0 + 1.5 * c(t, 2), rock=0.0, look=(6.0 * glance, 8.0, 1.42 - 0.20 * abs(glance)),
                   sit=(0.012 * glance + 0.004 * s(t), -0.006 * abs(glance), 0.003 * c(t, 2)), drop=ROAD_DROP, twist=7.0 * glance, tip=2.0 * glance, elbow=ROAD_ELBOW)


def astride(t, bike, hips, pitch, chest, drop, elbow, left=(-0.21, -0.06)):
    """Stopped astride a bicycle, off the saddle and standing just ahead of its nose: the left foot flat on the
    ground, the right still on its pedal (forward and level), both hands on the grips. The weight is on the left
    leg; they look up and down the road while they wait."""
    glance = keys(t, (0.0, 0.0), (0.10, 0.0), (0.22, -1.0), (0.40, -1.0), (0.52, 0.0), (0.62, 0.6), (0.74, 0.6), (0.86, 0.0), (1.0, 0.0))
    breath = s(t, 2)
    p = Pose()
    p.hips(here(bike, (hips[0] + 0.012 * s(t), hips[1] + 0.008 * c(t), hips[2] + 0.004 * breath)), pitch=pitch + 1.5 * s(t), roll=-4.5 + 1.0 * s(t), yaw=-3.0 - 3.0 * glance)
    p.chest(pitch=chest + 1.0 * breath, roll=-0.5, yaw=1.0 - 9.0 * glance)
    p.foot("L", here(bike, (left[0], left[1], 0.0)), yaw=-12.0, pitch=0.0, knee=(-0.25, 1.0, 0.1))
    sole_on(p, "R", pedal(bike, "R", 0.0) + (0.0, 0.0, 0.015), pitch=8.0, knee=(0.22, 1.0, 0.3))
    for side, sign in (("L", -1.0), ("R", 1.0)):
        on_grip(p, bike, side, drop, elbow=(sign * elbow[0], elbow[1], elbow[2]))
    p.look_at((-9.0 * glance, 8.0, 1.45))
    return p


def cycle_stand(t):
    # On the step-through town bicycle: upright, the elbows down and back along the ribs, not winged.
    return astride(t, CITY, (-0.065, -0.105, 0.872), -4.0, 2.0, 4.0, (0.12, -0.75, -1.0))


def cycle_stand_road(t):
    # On the road bicycle: over the sloping top tube (its top is 0.84 m up there), leaning on to the tops.
    return astride(t, ROAD, (-0.075, -0.075, 0.898), 12.0, 8.0, 30.0, (0.14, -0.35, -1.0), left=(-0.22, -0.04))


# ---------------------------------------------------------------------------------------------- motorbikes

def rider(t, bike, lean, chest, ahead, toe, drop, look_height, sway=1.0, elbow=(0.62, -0.45, -0.60)):
    """Seated on a motorbike: hands on the grips, feet on the pegs, knees in against the tank. A long loop of
    small things: the weight goes from one side of the seat to the other, the back gives and straightens, the
    eyes go from the road to one mirror and the other (the head hardly turns: the helmet does not turn with it)."""
    scan = keys(t, (0.0, 0.0), (0.08, 0.0), (0.20, 1.0), (0.32, 1.0), (0.44, 0.0), (0.56, -1.0), (0.68, -1.0), (0.80, 0.0), (1.0, 0.0))
    shift = s(t)
    give = wave(t * 2, 0.1)
    p = Pose()
    p.hips(seat(bike, SEAT_OVER) + (0.025 * shift * sway, -0.008 * give * sway, -0.004 * give), pitch=lean + 2.5 * give * sway, roll=2.0 * shift * sway, yaw=1.0 * scan)
    p.chest(pitch=chest + 1.5 * give, roll=-3.0 * shift * sway, yaw=4.0 * scan)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        sole_on(p, side, here(bike, mirror(bike["peg"], sign)) + (0.0, 0.0, PEG_RADIUS), pitch=toe, ahead=ahead, yaw=sign * 4.0,
                knee_at=here(bike, mirror(bike["knee"], sign)) + (0.010 * shift * sway, 0.0, 0.0))
        on_grip(p, bike, side, drop, elbow=(sign * elbow[0], elbow[1], elbow[2] - 0.25 * give))
    p.look_at((1.4 * scan, 9.0, look_height - 0.05 * abs(scan)))
    return p


def moto_ride(t):
    return rider(t, STANDARD, lean=9.0, chest=3.0, ahead=ARCH, toe=4.0, drop=30.0, look_height=1.40)


SPORT_ELBOW = (0.30, -0.40, -0.95)


def moto_ride_sport(t):
    # Down over the tank to the clip-ons, the balls of the feet on the higher pegs, the elbows tucked and low.
    return rider(t, SPORT, lean=33.0, chest=12.0, ahead=BALL, toe=14.0, drop=24.0, look_height=1.22, sway=0.8, elbow=SPORT_ELBOW)


def stopped(t, bike, lean, chest, ahead, toe, drop, elbow, foot):
    """Stopped on a motorbike, sitting to the left of the seat, the left foot flat on the ground ahead of its peg
    and out to the side; the right foot stays on its peg. Looks left, then right, for a gap (with the eyes more
    than the head)."""
    glance = keys(t, (0.0, 0.0), (0.10, 0.0), (0.22, -1.0), (0.42, -1.0), (0.54, 0.0), (0.64, 0.7), (0.76, 0.7), (0.88, 0.0), (1.0, 0.0))
    breath = s(t, 2)
    p = Pose()
    p.hips(seat(bike, SEAT_OVER) + (-0.060 + 0.010 * s(t), 0.0, -0.042 + 0.003 * breath), pitch=lean + 1.5 * s(t), roll=-5.0 + 0.8 * s(t), yaw=-2.0 - 1.5 * glance)
    p.chest(pitch=chest + 1.0 * breath, roll=0.5, yaw=-4.5 * glance)
    p.foot("L", here(bike, (foot[0], foot[1], 0.0)), yaw=-14.0, pitch=0.0, knee=(-0.30, 1.0, 0.2))
    sole_on(p, "R", here(bike, mirror(bike["peg"], 1.0)) + (0.0, 0.0, PEG_RADIUS), pitch=toe, ahead=ahead, yaw=4.0, knee_at=here(bike, mirror(bike["knee"], 1.0)) + (0.01, 0.0, 0.0))
    for side, sign in (("L", -1.0), ("R", 1.0)):
        on_grip(p, bike, side, drop, elbow=(sign * elbow[0], elbow[1], elbow[2]))
    p.look_at((-1.5 * glance, 9.0, 1.40))
    return p


def moto_stand(t):
    return stopped(t, STANDARD, 9.0, 3.0, ARCH, 4.0, 28.0, (0.45, -0.50, -0.85), (-0.30, -0.01))


def moto_stand_sport(t):
    # The same on the sport bike: sat up a little off the clip-ons (the arms nearly straight), the head up.
    return stopped(t, SPORT, 27.0, 10.0, BALL, 14.0, 20.0, SPORT_ELBOW, (-0.30, -0.05))


# ---------------------------------------------------------------------------------------------- the list

def under(bike):
    """The bike under the rider on the sheets, where BikeMount has it."""
    return ((bike["prop"], turn(), tuple(-origin(bike))),)


def where(bike):
    return ("the character's origin is on the ground %.2f m BEHIND the bike's own origin (BikeMount puts the rider at seat z + 0.04), facing the bike's front; the root does not move. "
            % -origin(bike)[1])


CLIPS = [
    Clip("cycle_ride", 4.0, cycle_ride, dressing=under(CITY),
         note="PLAYED BY BikeMount ON THE TOWN BICYCLE while pedalling (and on the road bicycle if cycle_ride_road is missing). " + where(CITY) + "Seated upright on bicycle_city: the Hips bone 0.058 m over the saddle (0.93 m up, 0.04 m behind the "
              "origin), palms on the grips (0.26 m either side, 1.14 m up, 0.42 m ahead of the origin), back 12 degrees forward, a slow sway and two short looks aside. "
              "The legs turn the cranks five times (75 a minute, the right crank forward at 0 s) with the ankles where BikeMount's LegIk puts them (0.10 m above and 0.03 m "
              "behind the pedals' middles): the code replaces them every frame and keeps only the feet's 8 degrees toes-down"),
    Clip("cycle_coast", 4.0, cycle_coast, dressing=under(CITY),
         note="PLAYED BY BikeMount on the town bicycle while it rolls without pedalling. " + where(CITY) + "Freewheeling on bicycle_city: cranks level, the right foot forward (cycle_ride's pose at 0 s), "
              "a look to the right and one to the left. The code's LegIk still holds the feet on the pedals where the crank stopped"),
    Clip("cycle_stand", 6.0, cycle_stand, dressing=under(CITY),
         note="PLAYED BY BikeMount on the town bicycle when stopped (LegIk let go, the crank brought to the right pedal forward). " + where(CITY) + "Stopped astride bicycle_city, off the saddle and standing just ahead of its nose (hips 0.16 m ahead of the origin): the left "
              "foot flat on the ground 0.21 m to the left and 0.20 m ahead of the origin, the right on its pedal (forward, level), hands on the grips; looks left and right. "
              "Step-through frame only"),
    Clip("cycle_ride_road", 4.0, cycle_ride_road, dressing=under(ROAD),
         note="PLAYED BY BikeMount ON THE ROAD BICYCLE while pedalling. " + where(ROAD) + "Seated on bicycle_road with the hands on the tops of its drop bar: the Hips bone 0.058 m over the saddle (0.95 m up), palms on the "
              "tops (0.13 m either side, 1.00 m up, 0.67 m ahead of the origin), back 42 degrees forward, elbows narrow under the shoulders; the legs as cycle_ride's (the code's LegIk)"),
    Clip("cycle_coast_road", 4.0, cycle_coast_road, dressing=under(ROAD),
         note="PLAYED BY BikeMount on the road bicycle while it rolls without pedalling. " + where(ROAD) + "cycle_ride_road with the cranks level, the right foot forward, the back 3 degrees higher, a look right and one left"),
    Clip("cycle_stand_road", 6.0, cycle_stand_road, dressing=under(ROAD),
         note="PLAYED BY BikeMount on the road bicycle when stopped. " + where(ROAD) + "Astride bicycle_road ahead of the saddle's nose, over its sloping top tube: the left foot flat on the ground 0.22 m to the left "
              "and 0.21 m ahead of the origin, the right on its pedal (forward, level), leaning 20 degrees on to the tops of the bar; looks left and right"),
    Clip("moto_ride", 4.0, moto_ride, dressing=under(STANDARD),
         note="PLAYED BY BikeMount ON THE ROADSTER while moving (and on the sport bike if moto_ride_sport is missing). " + where(STANDARD) + "Riding motorbike_standard: the Hips bone 0.085 m over the seat (0.83 m up, 0.04 m behind the origin), "
              "the arches on the pegs (0.24 m either side, 0.34 m up, 0.06 m ahead of the origin), palms on the grips (0.31 m either side, 1.075 m up, 0.46 m ahead), knees "
              "against the tank, back 12 degrees forward; shifts on the seat, looks 9 degrees right and left. The feet are the clip's own (the code leaves them)"),
    Clip("moto_ride_sport", 4.0, moto_ride_sport, dressing=under(SPORT),
         note="PLAYED BY BikeMount ON THE SPORT BIKE while moving. " + where(SPORT) + "On motorbike_sport: seat 0.84 m up, the balls of the feet on its pegs "
              "(0.24 m either side, 0.38 m up, 0.02 m ahead of the origin), palms on its clip-ons (0.27 m either side, 0.915 m up, 0.60 m ahead), back 45 degrees forward over the tank, elbows "
              "tucked and low"),
    Clip("moto_stand", 6.0, moto_stand, dressing=under(STANDARD),
         note="PLAYED BY BikeMount on the roadster when stopped. " + where(STANDARD) + "Stopped on motorbike_standard, sitting to the left of the seat: the left foot flat on the ground 0.30 m to the left "
              "and 0.15 m ahead of the origin (beside and ahead of its peg), the pelvis 0.06 m to the left and the trunk leaning 5 degrees over that foot, the right foot on its peg, hands on the grips; looks left and right"),
    Clip("moto_stand_sport", 6.0, moto_stand_sport, dressing=under(SPORT),
         note="PLAYED BY BikeMount on the sport bike when stopped. " + where(SPORT) + "moto_stand on motorbike_sport: the left foot flat on the ground 0.30 m to the left and 0.13 m ahead of the origin, "
              "the right on its peg, the hands on the clip-ons with the arms nearly straight, back 37 degrees forward"),
]
