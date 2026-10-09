"""The authoring rig: a humanoid with the proportions of the game's street character, posed by saying where things
are (hips, chest, head, hands, feet, a tool) instead of by turning bones. Pure numpy: no Blender in here.

Space: metres, x to the character's right, y forward, z up (the prop kit's space; Unity gets (x, z, y)).
Angles are degrees. yaw > 0 turns to the right, pitch > 0 leans forward (nose down), roll > 0 leans to the right.

    p = Pose()
    p.hips((0, 0, 0.86), yaw=10, pitch=8)          # where the hip joint is, and how the pelvis is turned
    p.chest(pitch=25, yaw=-15)                       # relative to the pelvis
    p.head(pitch=20)                                 # relative to the chest
    p.foot("L", (-0.16, 0.10, 0), yaw=-10)           # the point of the sole under the ankle
    p.tool(turn, position)                           # a tool held in the hands (see Grip): the hands follow it
    p.hand("R", (0.3, 0.3, 1.0), fingers=(0, 1, 0), palm=(0, 0, -1))      # or a free hand
    solved = p.solve()                               # joint positions and bone turns; solved.stretch says what could not be reached

A bone's turn is a rotation in the character's space applied to the bone as it is in the T-pose. That is all a
humanoid clip needs: Unity turns it into muscle values, and they play on any humanoid.
"""
import json
import math
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))

with open(os.path.join(HERE, "game_rig.json")) as _f:
    _RIG = json.load(_f)

# The joints of the T-pose, in this module's space.
J = {name: np.array([p[0], p[2], p[1]]) for name, p in _RIG["joints"].items()}
J["HeadTop"] = J["Head"] + (0.0, 0.0, 0.22)
SIDES = {"L": "Left", "R": "Right"}
PALM = {"L": np.array([-0.085, 0.0, -0.028]), "R": np.array([0.085, 0.0, -0.028])}   # middle of the palm from the wrist, T-pose
FINGERS = ("Thumb", "Index", "Middle", "Ring", "Little")
SEGMENTS = ("Proximal", "Intermediate", "Distal")
SOLE = 0.0                                                                           # the ground


def unit(v):
    v = np.asarray(v, dtype=float)
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


def turn_about(axis, degrees):
    axis = unit(axis)
    a = math.radians(degrees)
    k = np.array([[0, -axis[2], axis[1]], [axis[2], 0, -axis[0]], [-axis[1], axis[0], 0]])
    return np.eye(3) + math.sin(a) * k + (1 - math.cos(a)) * (k @ k)


def turn(yaw=0.0, pitch=0.0, roll=0.0):
    """yaw to the right, then pitch forward, then roll to the right."""
    return turn_about((0, 0, 1), -yaw) @ turn_about((1, 0, 0), -pitch) @ turn_about((0, 1, 0), roll)


def basis(primary, secondary):
    """An orthonormal frame whose first axis is `primary` and whose second leans towards `secondary`."""
    a = unit(primary)
    b = np.asarray(secondary, dtype=float)
    b = b - a * b.dot(a)
    if np.linalg.norm(b) < 1e-6:
        b = np.cross(a, (0.0, 0.0, 1.0) if abs(a[2]) < 0.9 else (1.0, 0.0, 0.0))
    b = unit(b)
    return np.stack([a, b, np.cross(a, b)], axis=1)


def between(a, b, t):
    """Part of the way from one turn to another."""
    delta = a.T @ b
    angle = math.acos(max(-1.0, min(1.0, (np.trace(delta) - 1) / 2)))
    if angle < 1e-6:
        return a.copy()
    axis = np.array([delta[2, 1] - delta[1, 2], delta[0, 2] - delta[2, 0], delta[1, 0] - delta[0, 1]])
    return a @ turn_about(axis, math.degrees(angle) * t)


def ease(t):
    """0..1 -> 0..1, slow at both ends."""
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def wave(t, shift=0.0):
    """A smooth 0..1..0 over one cycle of t, starting at 0."""
    return 0.5 - 0.5 * math.cos(2 * math.pi * (t - shift))


def keys(t, *points):
    """Eased interpolation through (time, value) points; values are numbers or vectors."""
    if t <= points[0][0]:
        return np.asarray(points[0][1], dtype=float)
    for (t0, v0), (t1, v1) in zip(points, points[1:]):
        if t <= t1:
            k = ease((t - t0) / (t1 - t0)) if t1 > t0 else 1.0
            return np.asarray(v0, dtype=float) * (1 - k) + np.asarray(v1, dtype=float) * k
    return np.asarray(points[-1][1], dtype=float)


# How the fingers close: (thumb, index, middle, ring, little, how far the thumb comes round under the palm, spread).
# 0 is straight, 1 a tight fist. The game's character has a thumb, an index finger and one "ring" chain for the
# other three, so what reads there is thumb, index and the rest.
HOLDS = {
    "flat":    (0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.0),
    "relaxed": (0.18, 0.16, 0.24, 0.32, 0.40, 0.20, 0.0),      # a hand hanging or resting
    "open":    (0.05, 0.04, 0.06, 0.08, 0.10, 0.00, 1.0),      # a spread hand: a wave, a stop
    "handle":  (0.55, 0.80, 0.84, 0.88, 0.92, 0.85, 0.0),      # round a shaft about 3 cm thick
    "bar":     (0.45, 0.68, 0.72, 0.76, 0.80, 0.80, 0.0),      # round a rail or grip 4 to 5 cm thick
    "cup":     (0.25, 0.42, 0.45, 0.48, 0.52, 0.70, 0.3),      # round a cup or a can
    "pinch":   (0.50, 0.52, 0.70, 0.80, 0.86, 0.75, 0.0),      # a sheet or a cigarette between thumb and fingers
    "pen":     (0.55, 0.60, 0.72, 0.88, 0.92, 0.80, 0.0),
    "plate":   (0.30, 0.22, 0.24, 0.26, 0.30, 0.55, 0.2),      # under something flat, thumb over its edge
    "point":   (0.60, 0.00, 0.88, 0.92, 0.95, 0.70, 0.0),
    "fist":    (0.70, 1.00, 1.00, 1.00, 1.00, 0.90, 0.0),
    "knock":   (0.55, 0.92, 0.95, 0.95, 0.95, 0.60, 0.0),
}


def hold_of(value):
    """A hold by name, a single curl (the old way: every finger alike), or the seven numbers themselves."""
    if isinstance(value, str):
        return HOLDS[value]
    if isinstance(value, (int, float)):
        v = float(value)
        return (v * 0.6, v, v, v, v, 0.5 * v, 0.0)
    return tuple(value)


def blend_holds(a, b, k):
    a, b = hold_of(a), hold_of(b)
    return tuple(x * (1 - k) + y * k for x, y in zip(a, b))


class Grip:
    """How a hand holds a tool: which point of the tool sits in the palm and how the tool is turned in the hand.

    Grip("R", at=(0, 0, 1.1), thumb=(0, 0, 1), back=(1, 0, 0))
        the tool's point `at` is in the palm; the tool's direction `thumb` runs out of the fist on the thumb's side
        (a broom's shaft, upwards); the tool's direction `back` points out of the back of the hand.
    """

    def __init__(self, side, at, thumb=(0, 0, 1), back=(1, 0, 0), lift=0.0, hold="handle"):
        self.side = side
        self.hold = hold                                    # how the fingers close on it (see HOLDS)
        hand = basis((0, 1, 0), (0, 0, 1))                  # T-pose: thumb forward, back of the hand up
        tool = basis(thumb, back)
        self.turn = hand @ tool.T                           # tool directions -> hand directions, in the T-pose
        self.offset = PALM[side] + (0.0, 0.0, -lift) - self.turn @ np.asarray(at, dtype=float)

    def wrist(self, tool_turn, tool_position):
        """Where the wrist is and how the hand is turned when the tool is at this pose."""
        hand = tool_turn @ self.turn.T
        return np.asarray(tool_position, dtype=float) - hand @ self.offset, hand

    def socket(self):
        """The tool's pose relative to the wrist in the T-pose: what the game needs to hang the tool on the hand bone."""
        return self.turn, self.offset


class Solved:
    def __init__(self):
        self.at = {}        # joint -> position
        self.turns = {}     # bone -> 3x3 turn from the T-pose
        self.stretch = 0.0  # the furthest any hand or foot was from where it was asked to be
        self.stretched = ""
        self.tool = None    # (turn, position) or None
        self.tool_held = False


class Pose:
    def __init__(self):
        self._hips = (J["Hips"].copy(), np.eye(3))
        self._chest = np.eye(3)
        self._head = np.eye(3)
        self._hands = {}
        self._feet = {"L": (np.array([J["LeftFoot"][0], J["LeftFoot"][1], SOLE]), np.eye(3), None, 0.0),
                      "R": (np.array([J["RightFoot"][0], J["RightFoot"][1], SOLE]), np.eye(3), None, 0.0)}
        self._curl = {"L": "relaxed", "R": "relaxed"}
        self._tool = None
        self._shrug = {"L": 0.0, "R": 0.0}

    def hips(self, position, yaw=0.0, pitch=0.0, roll=0.0):
        self._hips = (np.asarray(position, dtype=float), turn(yaw, pitch, roll))
        return self

    def chest(self, yaw=0.0, pitch=0.0, roll=0.0):
        self._chest = turn(yaw, pitch, roll)
        return self

    def head(self, yaw=0.0, pitch=0.0, roll=0.0):
        self._head = turn(yaw, pitch, roll)
        return self

    def look_at(self, point, limit=60.0, down=34.0, up=40.0):
        """Turns the head towards a point (after hips and chest are set)."""
        self._look = (np.asarray(point, dtype=float), limit, down, up)
        return self

    def foot(self, side, sole=None, yaw=0.0, pitch=0.0, roll=0.0, knee=None, toes=0.0, ankle=None):
        """sole: the point under the ankle (or say where the ankle itself is). pitch > 0 points the toes down (heel
        up). knee: the direction the knee points in. toes: degrees the toes bend back up (a foot up on its toes)."""
        turned = turn(yaw, pitch, roll)
        if ankle is not None:
            sole = np.asarray(ankle, dtype=float) - turned @ np.array([0.0, 0.0, J[SIDES[side] + "Foot"][2] - SOLE])
        self._feet[side] = (np.asarray(sole, dtype=float), turned, knee, toes)
        return self

    def hand(self, side, wrist, fingers=None, palm=None, elbow=None, curl=None, turned=None):
        """A free hand: where the wrist is; which way the fingers and the palm point; where the elbow leans."""
        hand = turned
        if fingers is not None:
            rest = basis((1 if side == "R" else -1, 0, 0), (0, 0, -1))
            hand = basis(fingers, palm if palm is not None else (0, 0, -1)) @ rest.T
        self._hands[side] = (np.asarray(wrist, dtype=float), hand, elbow)
        if curl is not None:
            self._curl[side] = curl
        return self

    def tool(self, tool_turn, position, *grips, elbows=None):
        """A tool at a pose, held by one or two Grips: the hands go where the tool needs them."""
        self._tool = (np.asarray(tool_turn, dtype=float), np.asarray(position, dtype=float))
        for grip in grips:
            wrist, hand = grip.wrist(*self._tool)
            self._hands[grip.side] = (wrist, hand, (elbows or {}).get(grip.side))
            self._curl[grip.side] = grip.hold
        return self

    def tool_free(self, tool_turn, position):
        """The tool where it lies or stands when nobody has hold of it (before it is picked up, after it is put
        down): drawn there on the sheets, and not measured against the hand."""
        self._tool = (np.asarray(tool_turn, dtype=float), np.asarray(position, dtype=float))
        self._tool_held = False
        return self

    def curl(self, side, amount):
        """How the fingers are closed: a name from HOLDS, one number for every finger, or seven (see HOLDS)."""
        self._curl[side] = amount
        return self

    def spine(self, lumbar=0.2, thoracic=0.55):
        """How the turn between pelvis and chest is shared out up the back (the rest is the upper chest's)."""
        self._spine = (lumbar, thoracic)
        return self

    def shrug(self, side, amount):
        self._shrug[side] = amount
        return self

    # ------------------------------------------------------------------------------------------ solving

    def solve(self):
        out = Solved()
        at, turns = out.at, out.turns
        position, pelvis = self._hips
        chest = pelvis @ self._chest
        at["Hips"], turns["Hips"] = position, pelvis
        low, mid = getattr(self, "_spine", (0.2, 0.55))    # the lumbar spine stays near neutral: people bend at the hips
        chain = (("Spine", between(pelvis, chest, low)), ("Chest", between(pelvis, chest, mid)), ("UpperChest", chest))
        parent = "Hips"
        for name, turned in chain:
            at[name] = at[parent] + turns[parent] @ (J[name] - J[parent])
            turns[name] = turned
            parent = name
        at["Neck"] = at["UpperChest"] + chest @ (J["Neck"] - J["UpperChest"])
        head = chest @ self._head
        look = getattr(self, "_look", None)
        if look is not None:
            eye = at["Neck"] + chest @ (J["Head"] - J["Neck"]) + (0.0, 0.0, 0.08)
            want = unit(look[0] - eye)
            local = chest.T @ want
            yaw = max(-look[1], min(look[1], math.degrees(math.atan2(local[0], local[1]))))
            pitch = max(-look[3], min(look[2], -math.degrees(math.asin(max(-1.0, min(1.0, local[2]))))))
            head = chest @ turn(yaw, pitch) @ self._head
        turns["Neck"] = between(chest, head, 0.45)
        at["Head"] = at["Neck"] + turns["Neck"] @ (J["Head"] - J["Neck"])
        turns["Head"] = head
        at["HeadTop"] = at["Head"] + head @ (J["HeadTop"] - J["Head"])

        for side, word in SIDES.items():
            sign = 1.0 if side == "R" else -1.0
            # Arm.
            shoulder, upper, lower, hand_name = word + "Shoulder", word + "UpperArm", word + "LowerArm", word + "Hand"
            at[shoulder] = at["UpperChest"] + chest @ (J[shoulder] - J["UpperChest"])
            turns[shoulder] = chest @ turn_about((0, 1, 0), -sign * 25.0 * self._shrug[side])
            at[upper] = at[shoulder] + turns[shoulder] @ (J[upper] - J[shoulder])
            wrist, hand, elbow = self._hands.get(side, (None, None, None))
            if wrist is None:       # hanging at the side, a little forward and out
                wrist = at[upper] + chest @ np.array([sign * 0.10, 0.06, -0.50])
                elbow = chest @ np.array([sign * 0.3, -1.0, -0.2])
            if elbow is None:
                elbow = chest @ np.array([sign * 0.55, -0.55, -0.65])
            rest_hinge = np.array([0.0, 0.0, sign])
            joint, reached, hinge = _two_bone(at[upper], wrist, np.linalg.norm(J[lower] - J[upper]), np.linalg.norm(J[hand_name] - J[lower]), elbow, out)
            at[lower], at[hand_name] = joint, reached
            turns[upper] = basis(joint - at[upper], hinge) @ basis(J[lower] - J[upper], rest_hinge).T
            turns[lower] = basis(reached - joint, hinge) @ basis(J[hand_name] - J[lower], rest_hinge).T
            turns[hand_name] = hand if hand is not None else turns[lower]
            thumb, index, middle, ring, little, oppose, spread = hold_of(self._curl[side])
            hand_turn = turns[hand_name]
            for k, (finger, curl) in enumerate(zip(FINGERS, (thumb, index, middle, ring, little))):
                total = 0.0
                start, step = _finger_rest(sign, k)
                joint = at[hand_name] + hand_turn @ start
                if finger == "Thumb":
                    # Round under the palm first (opposition), then closing towards the fingers.
                    base = hand_turn @ turn_about((1, 0, 0), -62.0 * oppose) @ turn_about((0, 0, 1), sign * 18.0 * spread)
                    for n, segment in enumerate(SEGMENTS):
                        total += curl * (26.0, 30.0, 40.0)[n]
                        turns[word + finger + segment] = base @ turn_about((0.3 * sign, 0, 1), -sign * total)
                        at[word + finger + segment] = joint
                        joint = joint + turns[word + finger + segment] @ step
                    at[word + finger + "Tip"] = joint
                else:
                    fan = hand_turn @ turn_about((0, 0, 1), sign * spread * (9.0, 0.0, -8.0, -16.0)[k - 1])
                    for n, segment in enumerate(SEGMENTS):
                        total += curl * (72.0, 85.0, 55.0)[n]
                        turns[word + finger + segment] = fan @ turn_about((0, 1, 0), sign * total)
                        at[word + finger + segment] = joint
                        joint = joint + turns[word + finger + segment] @ step
                    at[word + finger + "Tip"] = joint
            # Leg.
            upper, lower, foot, toes = word + "UpperLeg", word + "LowerLeg", word + "Foot", word + "Toes"
            at[upper] = at["Hips"] + pelvis @ (J[upper] - J["Hips"])
            sole, foot_turn, knee, toe_bend = self._feet[side]
            ankle = sole + foot_turn @ (J[foot] - np.array([J[foot][0], J[foot][1], SOLE]))
            if knee is None:
                knee = unit(foot_turn @ np.array([0.0, 1.0, 0.0]) + pelvis @ np.array([0.0, 1.0, 0.0])) + (0.0, 0.0, 0.15)
            joint, reached, hinge = _two_bone(at[upper], ankle, np.linalg.norm(J[lower] - J[upper]), np.linalg.norm(J[foot] - J[lower]), knee, out)
            rest_hinge = np.array([-1.0, 0.0, 0.0])
            at[lower], at[foot] = joint, reached
            turns[upper] = basis(joint - at[upper], hinge) @ basis(J[lower] - J[upper], rest_hinge).T
            turns[lower] = basis(reached - joint, hinge) @ basis(J[foot] - J[lower], rest_hinge).T
            turns[foot] = foot_turn
            at[toes] = reached + foot_turn @ (J[toes] - J[foot])
            turns[toes] = foot_turn @ turn_about((1, 0, 0), toe_bend)
        out.tool = self._tool
        out.tool_held = self._tool is not None and getattr(self, "_tool_held", True)
        return out


def _finger_rest(sign, k):
    """Where a finger starts from the wrist and the step from one of its joints to the next, in the T-pose."""
    if k == 0:
        return np.array([sign * 0.025, 0.03, -0.01]), np.array([sign * 0.022, 0.022, 0.0])
    return np.array([sign * 0.078, 0.036 - 0.024 * (k - 1), 0.0]), np.array([sign * 0.03, 0.0, 0.0])


def _two_bone(root, target, a, b, pole, out):
    """The middle joint of a two-bone limb reaching for a target, bent towards `pole`. Returns (middle, end, hinge)."""
    to = target - root
    distance = np.linalg.norm(to)
    reach = min(max(distance, abs(a - b) + 1e-4), (a + b) * 0.9995)
    if abs(distance - reach) > out.stretch:
        out.stretch, out.stretched = abs(distance - reach), ("arm" if a < 0.35 else "leg") + (" too near" if distance < reach else " too far")
    along = unit(to) if distance > 1e-6 else np.array([0.0, 0.0, -1.0])
    side = np.asarray(pole, dtype=float)
    side = side - along * side.dot(along)
    if np.linalg.norm(side) < 1e-6:
        side = np.cross(along, (1.0, 0.0, 0.0))
    side = unit(side)
    cos_a = (a * a + reach * reach - b * b) / (2 * a * reach)
    sin_a = math.sqrt(max(0.0, 1 - cos_a * cos_a))
    middle = root + along * (a * cos_a) + side * (a * sin_a)
    end = root + along * reach
    hinge = np.cross(middle - root, end - middle)
    if np.linalg.norm(hinge) < 1e-6:
        hinge = np.cross(side, along)
    return middle, end, unit(hinge)


def bones():
    """Every bone of the rig as (name, parent, head joint, tail point) in the T-pose."""
    rows = [("Hips", None, J["Hips"], J["Spine"]), ("Spine", "Hips", J["Spine"], J["Chest"]), ("Chest", "Spine", J["Chest"], J["UpperChest"]),
            ("UpperChest", "Chest", J["UpperChest"], J["Neck"]), ("Neck", "UpperChest", J["Neck"], J["Head"]), ("Head", "Neck", J["Head"], J["HeadTop"])]
    for side, word in SIDES.items():
        sign = 1.0 if side == "R" else -1.0
        tip = J[word + "Hand"] + (sign * 0.075, 0.0, 0.0)
        rows += [(word + "Shoulder", "UpperChest", J[word + "Shoulder"], J[word + "UpperArm"]), (word + "UpperArm", word + "Shoulder", J[word + "UpperArm"], J[word + "LowerArm"]),
                 (word + "LowerArm", word + "UpperArm", J[word + "LowerArm"], J[word + "Hand"]), (word + "Hand", word + "LowerArm", J[word + "Hand"], tip),
                 (word + "UpperLeg", "Hips", J[word + "UpperLeg"], J[word + "LowerLeg"]), (word + "LowerLeg", word + "UpperLeg", J[word + "LowerLeg"], J[word + "Foot"]),
                 (word + "Foot", word + "LowerLeg", J[word + "Foot"], J[word + "Toes"]), (word + "Toes", word + "Foot", J[word + "Toes"], J[word + "Toes"] + (0.0, 0.07, 0.0))]
        for k, finger in enumerate(FINGERS):
            start, step = _finger_rest(sign, k)
            start = J[word + "Hand"] + start
            parent = word + "Hand"
            for segment in SEGMENTS:
                rows.append((word + finger + segment, parent, start.copy(), start + step))
                parent, start = word + finger + segment, start + step
    return rows
