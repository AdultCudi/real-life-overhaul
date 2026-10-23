"""Clips for the building sites of Scripts/Civic/SiteDresser.Plot.cs (new buildings on given ground). Same rules as clips.py.

    hammer_nail    ASKED FOR BY CODE (the frame stage's carpenter, at the frame's foot): on one knee at a board 0.32 m up
                   and 0.44 m ahead, the left hand holding the nail, the right driving it with the hammer of the second
                   props bundle (PropAssets/scripts/props_build.py, held 0.10 m up its handle), two blows a loop
    rake_asphalt   ASKED FOR BY CODE (the slab stage's concrete gang): the first bundle's leaf rake (RAKE_LOW, RAKE_TOP)
                   pushed out and drawn back flat along the ground from 0.82 to 0.50 m ahead, levelling; the body
                   behind it

Where things are (x right, y ahead, z up, from the character's feet) is in each clip's note.
"""
import math

import numpy as np

from clips_life import RAKE_LOW, RAKE_TOP
from motion import Clip, kneel, s, shaft, stand
from rig import Grip, Pose, keys, wave

# The hammer is held 0.10 m up its handle, the thumb towards the head, the back of the hand to the side away from the
# swing: the face (the tool's +X) strikes down at the nail.
HAMMER_GRIP = Grip("R", at=(0, 0, 0.10), thumb=(0, 0, 1), back=(0, -1, 0))
HAMMER_HEAD = 0.315          # metres up the handle to the head's middle
HAMMER_FACE = 0.07           # metres from the handle to the striking face
NAIL = np.array([0.08, 0.44, 0.32])   # the nail's head where it is struck: a board at the frame's foot


def hammer_nail(t):
    # Two blows a loop: the hammer comes up 50 degrees from the wrist and elbow, a short wait, and down onto the nail; the left hand
    # keeps the nail upright beside it, the trunk leans in from the hip and the head watches the nail.
    blow = t * 2 % 1.0
    raised = keys(blow, (0.0, 0.0), (0.42, 1.0), (0.62, 1.0), (0.80, 0.0), (1.0, 0.0))
    angle = math.radians(-10.0 + 50.0 * raised)              # the handle's pitch: a little down at the blow, up when raised
    z = np.array([0.0, math.cos(angle), math.sin(angle)])
    down = np.array([0.0, math.sin(angle), -math.cos(angle)])   # the face's way, square to the handle in the swing's plane
    strike = NAIL - z * HAMMER_HEAD - down * HAMMER_FACE + z * 0.10   # the grip when the face is on the nail at the blow
    grip = strike + np.array([0.0, -0.01, 0.08]) * raised
    butt = grip - z * 0.10
    p = Pose()
    kneel(p, lean=34 - 2 * raised, forward=0.05, roll=1.0 * s(t))
    p.chest(pitch=6, yaw=-4 + 2 * s(t, 2))
    p.tool(*shaft(butt, butt + z, lean_to=down, level=False), HAMMER_GRIP, elbows={"R": (0.8, -0.4, -0.3)})
    p.hand("L", NAIL + (-0.07, -0.02, 0.06), fingers=(0.4, 0.6, -0.7), palm=(0.6, 0.1, -0.8), elbow=(-0.8, -0.3, -0.3), curl="pinch")
    p.look_at(NAIL + (0.0, 0.0, 0.05), down=22)
    return p


def rake_asphalt(t):
    # The rake pushed out flat and drawn back, the tines on the ground all the way: levelling, not gathering. The legs
    # and the lean do the work, the arms steer; a small step of the weight from foot to foot with each stroke.
    reach = wave(t)                                           # 0 near the feet .. 1 out, once a loop
    side = 0.04 * s(t, 0.5)
    tines = np.array([0.18 + side, 0.50 + 0.32 * reach, 0.0])
    top = np.array([-0.06, 0.26 + 0.28 * reach, 1.08 - 0.06 * reach])
    p = Pose()
    stand(p, height=0.90, width=0.20, yaw=6, lean=18 + 16 * reach, sway=-0.4 + 0.8 * reach, shift=(0.0, 0.03 + 0.05 * reach), left=(0.0, 0.16), right=(0.02, -0.10),
          toe_out=10)
    p.chest(yaw=6 - 4 * reach, pitch=4 + 3 * reach, roll=1)
    p.tool(*shaft(tines, top), RAKE_LOW, RAKE_TOP, elbows={"R": (0.8, -0.5, -0.3), "L": (-0.8, -0.4, -0.2)})
    p.look_at(tines + (0, 0.3, 0.05), down=18)
    return p


CLIPS = [
    Clip("hammer_nail", 1.6, hammer_nail, tool="hammer", grips=(HAMMER_GRIP,), tool_length=0.33,
         note="on one knee, two blows a loop at a nail 0.50 m ahead and 0.30 m up, the left hand holding it"),
    Clip("rake_asphalt", 2.6, rake_asphalt, tool="rake", grips=(RAKE_LOW, RAKE_TOP), tool_length=1.55,
         note="levelling: the rake pushed out to 0.82 m ahead and drawn back to 0.50 m, flat on the ground"),
]
