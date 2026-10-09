"""Renders the game's own head (and one of its hairstyles) for comparison. Local reference only."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ba_common as ba

VIEWS = [(0, 5), (45, 5), (90, 5), (180, 10), (0, 45)]

for sex, head, tex in (("male", "MaleFace1", "T_FaceMale1_BC"), ("female", "FemFace", None)):
    scene = ba.reset_scene()
    camera = ba.setup_render(scene)
    ba.add_reference(head, texture_name=tex)
    target = ba.bone_head("Skull") + (0, 0, 0.09)
    print(ba.render_views(scene, camera, target, "reference_head_" + sex, VIEWS, distance=1.3))
