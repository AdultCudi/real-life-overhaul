"""The painted faces of the family module (props_family.py): the driving school's sandwich board and learner plate,
the parking bay, and the wedding and nursery things. See atlas.py, "the later rounds".

Lettering: the atlas's text() fits a line to a box, and the letters come out 0.85 of the font size high.

    family_school_board    0.56 x 0.896 m   DRIVING / SCHOOL 0.09 m capitals (a pavement board: read from across the
                                            street by its learner plate, 0.36 m across, not by its lettering)
"""
import math

import numpy as np


def register(a):
    BLACK = a.BLACK

    swatches = {
        "family_pink": ((0.93, 0.58, 0.67), 0.0, 0.30, 0.1),
        "family_skin": ((0.87, 0.66, 0.56), 0.0, 0.30, 0.0),
        "family_teal": ((0.10, 0.66, 0.57), 0.0, 0.55, 0.15),               # the checkpoint beacon (LicenceProps.Teal)
        "family_baby_blue": ((0.70, 0.80, 0.89), 0.0, 0.25, 0.05),          # the nursery's cushions and mattress (FamilyProps.Linen)
    }

    # ------------------------------------------------------------------------------------------ the driving school

    L_RED = (0.84, 0.06, 0.05)
    BOARD = (0.96, 0.96, 0.94)

    def school_board():
        # One face of the sandwich board, 160 x 256 px for 0.56 x 0.896 m (3.5 mm a pixel).
        layer = a.Layer(160, 256, BOARD, 0.0, 0.5)
        layer.rect(0, 0, 160, 256, L_RED)
        layer.rect(5, 5, 155, 251, BOARD, radius=6)
        layer.text("DRIVING", (12, 12, 148, 44), BLACK, weight=0.07, spacing=0.02)        # capitals 26 px: 0.09 m
        layer.text("SCHOOL", (12, 44, 148, 76), BLACK, weight=0.07, spacing=0.04)
        layer.rect(27, 86, 133, 192, (0.55, 0.55, 0.55), radius=9)                        # the learner plate: 0.36 m across
        layer.rect(29, 88, 131, 190, (1.0, 1.0, 0.99), radius=8, smooth=0.6)
        layer.rect(58, 101, 80, 177, L_RED, smooth=0.6)
        layer.rect(58, 156, 106, 177, L_RED, smooth=0.6)
        layer.rect(12, 204, 148, 242, L_RED, radius=5)
        layer.text("LESSONS", (20, 209, 140, 237), BOARD, weight=0.07, spacing=0.03)
        return layer.finish(grime=0.22, grain=0.03, blotch=0.05, scratches=10, edge=0.10)

    # ------------------------------------------------------------------------------------------ the wedding, the nursery

    def rose(colour, shade):
        def paint():
            layer = a.Layer(64, 64, shade, 0.0, 0.25)
            light = tuple(min(1.0, c * 1.06 + 0.03) for c in colour)
            for ring, (radius, count, size) in enumerate(((27, 7, 17), (19, 6, 13), (12, 5, 10), (6, 4, 7))):
                for k in range(count):
                    turn = 6.283 * (k / count) + ring * 0.55
                    x, y = 32 + math.cos(turn) * radius, 32 + math.sin(turn) * radius
                    layer.disc(x, y, size, shade)                                         # each petal: a dark rim, its face, a lit lip
                    layer.disc(x - math.cos(turn) * 1.6, y - math.sin(turn) * 1.6, size - 1.6, colour)
                    layer.disc(x - math.cos(turn) * 4.5, y - math.sin(turn) * 4.5, size - 5.0, light)
            layer.disc(32, 32, 4.5, shade)
            layer.disc(32, 32, 2.5, tuple(c * 0.8 for c in shade))
            return layer.finish(grain=0.03, blotch=0.04)
        return paint

    def runner():
        # One metre of the aisle runner, 128 x 112 px for 1.10 x 1.00 m: its top and bottom rows meet the next length's,
        # so nothing but the weave and the side bands touches them.
        rng = np.random.default_rng(853)
        ivory, shade, gilt = (0.88, 0.85, 0.76), (0.76, 0.72, 0.62), (0.72, 0.54, 0.20)
        layer = a.Layer(128, 112, ivory, 0.0, 0.2)
        for x in range(8, 124, 4):                                                        # the weave: faint ribs along its length
            layer.rect(x, 0, x + 1, 112, tuple(c * 0.975 for c in ivory))
        for x0, x1 in ((0, 5), (123, 128)):                                               # the hems, and a gold line inside each
            layer.rect(x0, 0, x1, 112, shade)
        for x in (9, 116.5):
            layer.rect(x, 0, x + 2.5, 112, gilt, metal=0.6, smooth=0.5)
        for x in (13.5, 113):
            layer.rect(x, 0, x + 1, 112, gilt, metal=0.6, smooth=0.5)
        for _ in range(16):                                                               # petals dropped on it
            x, y, turn = rng.uniform(20, 108), rng.uniform(10, 102), rng.uniform(0, 6.283)
            long, wide = rng.uniform(2.6, 3.6), rng.uniform(1.6, 2.2)
            tone = ((0.93, 0.58, 0.67), (0.88, 0.44, 0.56), (0.97, 0.80, 0.84))[int(rng.integers(3))]
            shape = [(x + math.cos(turn) * long * math.cos(b) - math.sin(turn) * wide * math.sin(b), y + math.sin(turn) * long * math.cos(b) + math.cos(turn) * wide * math.sin(b))
                     for b in np.linspace(0, 6.283, 8, endpoint=False)]
            layer.poly([(px + 0.7, py + 0.8) for px, py in shape], shade)
            layer.poly(shape, tone)
        return layer.finish(grain=0.03, blotch=0.05)

    decals = {
        "family_school_board": school_board,
        "family_rose_white": rose((0.95, 0.93, 0.86), (0.74, 0.69, 0.60)),
        "family_rose_pink": rose((0.93, 0.58, 0.67), (0.68, 0.30, 0.42)),
        "family_runner": runner,
    }
    return swatches, decals
