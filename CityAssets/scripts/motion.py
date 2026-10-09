"""What every clip file shares: the Clip row, and the ways of standing, bending and walking that keep a figure from
looking like a robot. No Blender in here.

Rules the helpers exist to enforce (they are what was wrong with the first set of clips):

  * People bend at the HIPS. `stand(lean=...)` pitches the pelvis and takes the hips back over the heels; the chest
    is then set a few degrees from it at most (`p.chest(pitch=...)` is relative to the pelvis). A big chest pitch is
    a rounded back: do not go beyond about 10 degrees. `kneel` and `kneel_both` do the same on the ground.
  * Nobody stands still on two planted feet. `weight(t)` is where the weight is (-1 left .. +1 right) with the
    settling people do; `stand(sway=...)` moves the pelvis over that foot and drops the other hip; `shuffle` lifts a
    foot and puts it down somewhere else for a while.
  * Things start and stop gently (`keys` eases every segment), and the parts of the body do not all move at once:
    drive the hand from the moment `lag(t, seconds)` earlier than the body.
  * A walking loop's planted foot goes back under the body at exactly the speed the clip is made for (`Walk`), so
    that it does not slide when whoever moves the character keeps that speed; the catalogue carries the speed.
"""
import math

import numpy as np

from rig import J, Pose, ease, keys, turn, unit, wave  # noqa: F401

TAU = 2 * math.pi

MASKS = ("full", "upper", "arms", "left_arm", "right_arm")


class Clip:
    """One row of the catalogue.

    speed   metres a second the clip walks at when it is a walking loop (0: it stands). Play it at
            ground speed / speed and the feet do not slide.
    mask    the part of the body the clip is made for. "full" replaces the whole pose. Anything else is meant to be
            layered over the game's own walk or idle (ActivityAnimator.PlayOver): only that part is taken from it.
    marks   named moments in seconds (when a hand closes on something, when it lets go).
    dressing  props drawn with the figure on the contact sheets: (prop id, turn, position).
    """

    def __init__(self, name, length, pose, loop=True, tool=None, grips=(), tool_length=1.4, dressing=(), note="", speed=0.0, mask="full", marks=None, watch=None):
        assert mask in MASKS, mask
        self.name, self.length, self.pose, self.loop = name, length, pose, loop
        self.tool, self.grips, self.tool_length, self.dressing, self.note = tool, tuple(grips), tool_length, tuple(dressing), note
        self.speed, self.mask, self.marks = speed, mask, dict(marks or {})
        self.watch = watch or (self.grips[0].side if self.grips else "R")      # the hand the sheets show close up

    def at(self, t):
        return self.pose(t).solve()


def s(t, turns=1.0, shift=0.0):
    return math.sin(TAU * (t * turns - shift))


def c(t, turns=1.0, shift=0.0):
    return math.cos(TAU * (t * turns - shift))


def lag(t, part, loop=True):
    """The moment a part of the clip earlier (part: a fraction of the clip). For a hand that follows the body."""
    return (t - part) % 1.0 if loop else max(0.0, t - part)


def weight(t, turns=1.0, shift=0.0, settle=0.6):
    """Where the weight is, -1 (left foot) .. +1 (right): it goes over quickly and then stays, as people stand."""
    x = math.sin(TAU * (t * turns - shift))
    return math.copysign(abs(x) ** settle, x)


def shuffle(t, out, back, span=0.07):
    """A foot taken somewhere else at moment `out` and brought home at `back` (fractions of a loop). Returns how
    far it is there (0..1) and how high it is lifted (0..1) on the way."""
    def passage(start):
        k = (t - start) / span
        return k if 0.0 <= k <= 1.0 else None
    k = passage(out)
    if k is not None:
        return ease(k), math.sin(math.pi * k)
    k = passage(back)
    if k is not None:
        return 1.0 - ease(k), math.sin(math.pi * k)
    return (1.0 if out + span < t < back else 0.0), 0.0


def shaft(foot, towards, lean_to=(1.0, 0.0, 0.0), level=True):
    """The pose of a long tool whose foot is at one point and whose shaft (its Z) runs towards another. Its X points
    as nearly along `lean_to` as it can; with `level` it is kept horizontal as well, so that a broom's or a rake's
    working edge lies flat on the ground however the shaft leans."""
    z = unit(np.asarray(towards, dtype=float) - np.asarray(foot, dtype=float))
    across = np.cross((0.0, 0.0, 1.0), z)
    if level and np.linalg.norm(across) > 0.05:
        x = unit(across)
        if x.dot(lean_to) < 0:
            x = -x
    else:
        from rig import basis
        x = basis(z, lean_to)[:, 1]
    return np.stack([x, np.cross(z, x), z], axis=1), np.asarray(foot, dtype=float)


# ---------------------------------------------------------------------------------------------- standing

def stance(p, height=0.90, width=0.15, yaw=0.0, pitch=0.0, roll=0.0, shift=(0.0, 0.0), toe_out=8.0, left=(0.0, 0.0), right=(0.0, 0.0)):
    """Standing, plainly: both feet flat, hips over them. (Kept for clips that need exactly this; see `stand`.)"""
    p.hips((shift[0], shift[1], height), yaw=yaw, pitch=pitch, roll=roll)
    p.foot("L", (-width + left[0], left[1], 0.0), yaw=-toe_out)
    p.foot("R", (width + right[0], right[1], 0.0), yaw=toe_out)
    return p


def stand(p, height=0.905, width=0.15, yaw=0.0, lean=0.0, roll=0.0, shift=(0.0, 0.0), toe_out=8.0, left=(0.0, 0.0), right=(0.0, 0.0), sway=0.0,
          lift=(0.0, 0.0), turn_feet=(0.0, 0.0), floor=0.0):
    """Standing as people do.

    lean    degrees bent forward AT THE HIPS: the pelvis pitches, the hips go back over the heels and the knees give.
    sway    where the weight is, -1 (left) .. +1 (right): the pelvis goes over that foot and the other hip drops.
    lift    how far each foot (left, right) is off the ground, 0..1 (see `shuffle`).
    floor   what is stood on, above the ground (a step of a ladder).
    """
    bend = math.radians(lean)
    back = -0.30 * math.sin(bend)                                   # hips back as the trunk goes forward
    drop = 0.045 * (1 - math.cos(bend)) * 6 + 0.012 * abs(sway)     # the knees give a little
    p.hips((shift[0] + 0.046 * sway, shift[1] + back, floor + height - drop), yaw=yaw, pitch=lean, roll=roll - 3.0 * sway)
    p.foot("L", (-width + left[0], left[1], floor + 0.035 * lift[0]), yaw=-toe_out + turn_feet[0], pitch=14 * lift[0])
    p.foot("R", (width + right[0], right[1], floor + 0.035 * lift[1]), yaw=toe_out + turn_feet[1], pitch=14 * lift[1])
    return p


def idle(p, t, turns=1.0, phase=0.0, amount=1.0, **how):
    """A standing loop's legs and pelvis: the weight goes from foot to foot and, once a loop, the free foot is moved
    a few centimetres and later brought back. Returns the sway (-1..1) for the chest and head to answer."""
    sway = weight(t, turns, phase) * amount
    there, up = shuffle((t - phase / max(turns, 1e-6)) % 1.0, 0.30, 0.80)
    left = how.pop("left", (0.0, 0.0))
    left = (left[0] - 0.05 * there * amount, left[1] + 0.09 * there * amount)
    stand(p, sway=sway, left=left, lift=(up * amount, 0.0), **how)
    return sway


def kneel(p, side="R", forward=0.0, height=0.0, lean=14.0, yaw=0.0, roll=0.0):
    """Down on one knee (the given side's), the other foot planted in front. `lean` is at the hips: the back stays straight."""
    sign = 1.0 if side == "R" else -1.0
    other = "L" if side == "R" else "R"
    p.hips((0.0, -0.07 + forward, 0.475 + height), pitch=lean, yaw=yaw, roll=roll)
    p.foot(side, ankle=(sign * 0.13, -0.33 + forward, 0.168), pitch=80, knee=(0.0, 0.8, -0.6), toes=-55)
    p.foot(other, (-sign * 0.18, 0.27 + forward, 0.0), yaw=-sign * 8, knee=(-sign * 0.25, 0.3, 1.0))
    return p


def kneel_both(p, forward=0.0, height=0.0, lean=30.0, spread=0.15):
    """On both knees, sitting up off the heels."""
    p.hips((0.0, -0.02 + forward, 0.475 + height), pitch=lean)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        p.foot(side, ankle=(sign * spread, -0.36 + forward * 0.5, 0.168), pitch=80, knee=(sign * 0.12, 0.9, -0.5), toes=-55)
    return p


def hang(p, side, sway=0.0, swing=0.0, hold="relaxed"):
    """An arm left hanging at the side, alive: it swings a little with the body. For the hand a clip has no use for."""
    sign = 1.0 if side == "R" else -1.0
    hips, pelvis = p._hips
    wrist = hips + pelvis @ np.array([sign * 0.235 - 0.01 * sway, 0.045 + 0.05 * swing, -0.045 + 0.012 * abs(swing)])
    p.hand(side, wrist, fingers=pelvis @ np.array([sign * 0.08, 0.22 + 0.2 * swing, -1.0]), palm=pelvis @ np.array([-sign, 0.1, 0.0]), elbow=pelvis @ np.array([sign * 0.5, -1.0, -0.1]), curl=hold)
    return p


# ---------------------------------------------------------------------------------------------- walking

ANKLE = float(J["RightFoot"][2])                                   # the ankle above the sole
BALL = float(J["RightToes"][1] - J["RightFoot"][1])                # the ball of the foot ahead of the ankle
HEEL = -0.055                                                      # the heel behind it


class Walk:
    """A walking loop on the spot: two steps a loop, the planted foot going back at exactly `speed`.

    Walk(speed, length)          forwards
    Walk(-speed, length)         backwards (feet reach back, toes down first)
    duty                         the part of a loop each foot is on the ground (0.6 walking; more is a shuffle)
    """

    def __init__(self, speed, length, duty=0.62, width=0.095, height=0.895, lift=0.065, toe_out=6.0):
        self.speed, self.length, self.duty, self.width, self.height, self.lift, self.toe_out = speed, length, duty, width, height, lift, toe_out
        self.travel = abs(speed) * length * duty                   # how far a planted foot goes back under the body
        self.backwards = speed < 0

    def foot(self, p, side, t, lead=0.0):
        """Puts one foot where it is at moment t. lead: metres the whole footfall pattern sits ahead of the hips."""
        sign = 1.0 if side == "R" else -1.0
        u = (t + (0.5 if side == "R" else 0.0)) % 1.0
        front, direction = self.travel * 0.40 + lead, -1.0
        if self.backwards:
            front, direction = -self.travel * 0.52 + lead, 1.0
        x = sign * self.width
        if u < self.duty:                                          # planted
            k = u / self.duty
            y = front + direction * self.travel * k
            if self.backwards:                                     # toes down first, then the heel; peels off the heel
                pitch = 16 * max(0.0, 1 - k / 0.2) - 10 * max(0.0, (k - 0.8) / 0.2)
                self._place(p, side, x, y, pitch, 0.0, sign)
            else:
                strike = max(0.0, 1 - k / 0.20)                    # heel first
                off = ease(max(0.0, (k - 0.58) / 0.42))            # then up onto the ball and off it
                self._place(p, side, x, y, -24 * strike + 48 * off, -38 * off, sign)
        else:                                                      # carried through
            k = (u - self.duty) / (1 - self.duty)
            y = front + direction * self.travel * (1 - ease(k))
            high = self.lift * math.sin(math.pi * k) ** 0.8
            if self.backwards:
                pitch = -10 * (1 - k) + 28 * math.sin(math.pi * k) + 16 * k
            else:
                pitch = 48 * (1 - k) ** 2.2 - 24 * k ** 1.5
            self._place(p, side, x, y, pitch, 0.0, sign, high)
        return p

    def _place(self, p, side, x, y, pitch, toes, sign, high=0.0):
        """The foot turned about whichever of heel and ball is on the ground, so that neither goes through it."""
        pivot = BALL if pitch > 0 else HEEL
        rotated = turn(pitch=pitch) @ np.array([0.0, -pivot, ANKLE])       # from the pivot to the ankle
        p.foot(side, ankle=(x, y + pivot + rotated[1], high + rotated[2]), yaw=sign * self.toe_out, pitch=pitch, toes=toes)

    def body(self, p, t, lean=4.0, yaw=5.0, roll=2.0, lead=0.0, bob=0.02, sway=0.012, height=None):
        """Hips for moment t (with the bob, the roll and the turn of walking) and both feet. Returns the swing
        (-1..1, positive when the LEFT foot is forward) for the arms and the chest to answer."""
        swing = c(t)                                               # left foot lands at t = 0
        down = wave(t * 2, 0.12)                                   # lowest with both feet down, highest over one foot
        p.hips((-sway * s(t, shift=0.05), 0.0, (height or self.height) - bob * (1 - down)), yaw=yaw * swing * (-1.0 if self.backwards else 1.0),
               roll=roll * s(t, shift=0.02), pitch=lean)
        self.foot(p, "L", t, lead)
        self.foot(p, "R", t, lead)
        return swing

    def arm(self, p, side, t, amount=1.0, hold="relaxed"):
        """A free arm swinging against its leg."""
        sign = 1.0 if side == "R" else -1.0
        swing = c(lag(t, 0.04)) * sign * amount                    # right arm forward with the left foot
        hips, pelvis = p._hips
        wrist = hips + pelvis @ np.array([sign * 0.225, 0.02 + 0.17 * swing, -0.06 + 0.035 * abs(swing) + 0.02 * max(0.0, swing)])
        p.hand(side, wrist, fingers=pelvis @ np.array([sign * 0.05, 0.25 + 0.5 * swing, -1.0]), palm=pelvis @ np.array([-sign, 0.1, 0.0]),
               elbow=pelvis @ np.array([sign * 0.4, -1.0, 0.0]), curl=hold)
        return p
