"""Boils _reference/game_skeleton.json (see extract_game_skeleton.py) down to the few measurements the pipeline keeps:
where the joints of the game's street character are in its T-pose, and how its two hand bones are turned there.
Numbers only; this file is committed (scripts/game_rig.json) so that the clips can be rebuilt without the game.

    python -I make_game_rig.py
"""
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
NAMES = {"Hips": "Hips", "Spine": "Spine", "Chest": "Chest", "UpperChest": "UpperChest", "Neck": "Neck", "Skull": "Head",
         "Shoulder.L": "LeftShoulder", "UpperArm.L": "LeftUpperArm", "LowerArm.L": "LeftLowerArm", "Hand.L": "LeftHand",
         "Shoulder.R": "RightShoulder", "UpperArm.R": "RightUpperArm", "LowerArm.R": "RightLowerArm", "Hand.R": "RightHand",
         "UpperLeg.L": "LeftUpperLeg", "LowerLeg.L": "LeftLowerLeg", "Foot.L": "LeftFoot", "ToesFoot.L": "LeftToes",
         "UpperLeg.R": "RightUpperLeg", "LowerLeg.R": "RightLowerLeg", "Foot.R": "RightFoot", "ToesFoot.R": "RightToes",
         "IndexFinger.L.001": "LeftIndexProximal", "IndexFinger.R.001": "RightIndexProximal"}


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return np.array([aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz])


def qrot(q, v):
    u, w = np.array(q[:3]), q[3]
    return v + 2 * np.cross(u, np.cross(u, v) + w * v)


with open(os.path.join(HERE, "..", "_reference", "game_skeleton.json")) as f:
    data = json.load(f)
world, joints, turns, bones = {}, {}, {}, []
for i, node in enumerate(data["humanSkeleton"]):
    t, q = np.array(node["t"]), np.array(node["q"])
    if node["parent"] >= 0:
        pt, pq = world[node["parent"]]
        t, q = pt + qrot(pq, t), qmul(pq, q)
    world[i] = (t, q)
    bones.append({"name": node["name"], "parent": data["humanSkeleton"][node["parent"]]["name"] if node["parent"] >= 0 else None,
                  "t": [round(x, 5) for x in node["t"]], "q": [round(x, 6) for x in node["q"]]})
    if node["name"] in NAMES:
        joints[NAMES[node["name"]]] = [round(float(x), 4) for x in t]
        turns[NAMES[node["name"]]] = [round(float(x), 6) for x in q]
out = {"note": "Unity space (x right, y up, z forward), metres, the character at the origin facing +z, in the T-pose of its Avatar (ArmatureLODAvatar).",
       "joints": joints, "leftHandTurn": turns["LeftHand"], "rightHandTurn": turns["RightHand"], "tposeBones": bones}
with open(os.path.join(HERE, "game_rig.json"), "w") as f:
    json.dump(out, f, indent=1)
print(json.dumps({k: out[k] for k in ("leftHandTurn", "rightHandTurn")}))
