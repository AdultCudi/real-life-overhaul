"""Solves every clip at 60 moments and says how far out of reach anything is, then checks the poses that have to read a
certain way (the phone clips). No Blender needed:  python -I check_clips.py [clip ...]"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clips  # noqa: E402

bad = 0
for clip in clips.CLIPS:
    if sys.argv[1:] and clip.name not in sys.argv[1:]:
        continue
    worst, low, where = 0.0, 9.0, ""
    for k in range(60):
        solved = clip.at(k / 60)
        if solved.stretch > worst:
            worst, where = solved.stretch, "%s at t=%.2f" % (solved.stretched, k / 60)
        low = min(low, min(p[2] for p in solved.at.values()))
    flag = "  OUT OF REACH (%s)" % where if worst > 0.03 else ""
    bad += worst > 0.03
    print("%-18s %5.2f s %-4s stretch %.3f  lowest joint %.3f%s" % (clip.name, clip.length, "loop" if clip.loop else "once", worst, low, flag))


# ---------------------------------------------------------------------------------------------- how the phone is held
# Seen from the game's camera a pose has to say what it is at once. These are the measures of that, at moments through
# the part of the clip where the phone is up: where the phone is from the eyes, which way its screen faces, where the
# elbows are, where the head looks.

def _eyes(solved):
    return solved.at["Head"] + solved.turns["Head"] @ np.array([0.0, 0.07, 0.08])


def _angle(a, b):
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    return math.degrees(math.acos(max(-1.0, min(1.0, a.dot(b) / (np.linalg.norm(a) * np.linalg.norm(b))))))


def _arm(solved, word):
    shoulder, elbow, wrist = solved.at[word + "UpperArm"], solved.at[word + "LowerArm"], solved.at[word + "Hand"]
    return shoulder, elbow, wrist, np.linalg.norm(wrist - shoulder) / (np.linalg.norm(elbow - shoulder) + np.linalg.norm(wrist - elbow))


def _phone(solved):
    turn, position = solved.tool
    eyes = _eyes(solved)
    to_eyes = eyes - position
    distance = float(np.linalg.norm(to_eyes))
    above = math.degrees(math.asin(-to_eyes[2] / distance))                    # the phone above the eyes, degrees
    right = math.degrees(math.atan2(-to_eyes[0], -to_eyes[1]))                 # round to the right of straight ahead, degrees
    looking = _angle(solved.turns["Head"] @ np.array([0.0, 1.0, 0.0]), position - eyes)
    return distance, above, right, _angle(turn[:, 2], to_eyes), looking


def check(name, moments, rules):
    global bad
    if sys.argv[1:] and name not in sys.argv[1:]:
        return
    clip = next(c for c in clips.CLIPS if c.name == name)
    failed = []
    for t in moments:
        for say, ok in rules(clip.at(t)):
            if not ok and not any(f.startswith(say) for f in failed):
                failed.append("%s (t=%.2f)" % (say, t))
    bad += bool(failed)
    print("%-18s pose %s" % (name, "PASS" if not failed else "FAILED: " + "; ".join(failed)))


def take_photo(solved):
    distance, above, right, screen, looking = _phone(solved)
    yield "the phone 0.25 to 0.40 m from the eyes", 0.25 <= distance <= 0.40
    yield "the phone at eye level (within 8 degrees)", abs(above) <= 8.0
    yield "the screen faces the eyes (within 20 degrees)", screen <= 20.0
    yield "the phone upright (its top within 25 degrees of up)", _angle(solved.tool[0][:, 1], (0, 0, 1)) <= 25.0
    for word in ("Right", "Left"):
        shoulder, elbow, wrist, _ = _arm(solved, word)
        yield word + " elbow at least 6 cm below the shoulder", shoulder[2] - elbow[2] >= 0.06
        yield word + " elbow no wider than the shoulder", abs(elbow[0]) <= abs(shoulder[0]) + 0.01
        yield word + " forearm forward and up to the phone", wrist[1] - elbow[1] >= 0.08 and wrist[2] > elbow[2]
    yield "the head looks at the screen (within 15 degrees)", looking <= 15.0


def selfie(solved):
    distance, above, right, screen, looking = _phone(solved)
    _, _, _, reach = _arm(solved, "Right")
    yield "the phone 0.35 to 0.50 m from the eyes", 0.35 <= distance <= 0.50
    yield "the phone a little above the eyes (8 to 25 degrees)", 8.0 <= above <= 25.0
    yield "the phone ahead and to the right (15 to 50 degrees round)", 15.0 <= right <= 50.0
    yield "the screen faces the eyes (within 15 degrees)", screen <= 15.0
    yield "the right arm out at its length (at least 0.93 of it)", reach >= 0.93
    yield "the right hand on the phone (within 1 cm)", solved.stretch <= 0.01
    yield "the head looks into the phone (within 15 degrees)", looking <= 15.0
    yield "the left hand hangs by the hip", solved.at["LeftHand"][2] < solved.at["Hips"][2] + 0.05


check("take_photo", [0.24 + 0.56 * k / 10 for k in range(11)], take_photo)       # held from 0.20 to 0.82
check("selfie", [0.26 + 0.52 * k / 10 for k in range(11)], selfie)               # held from 0.24 to 0.80
sys.exit(1 if bad else 0)
