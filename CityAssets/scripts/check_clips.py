"""Solves every clip at 60 moments and says how far out of reach anything is. No Blender needed:  python -I check_clips.py [clip ...]"""
import os
import sys

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
sys.exit(1 if bad else 0)
