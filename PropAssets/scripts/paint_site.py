"""The painted faces of the site module (props_site.py): the campaign, the courthouse sign, the building site and the
road works, the station's evidence door. See atlas.py, "the later rounds".

The atlas is nearly full, so this module letters as little as it can: a word is a strip of its own that several props
share (VOTE is on the four posters, the four boards, the lawn sign and the polling sign), the colours round it are
swatches and the stars are geometry. A line fitted to a box by the atlas's text() comes out with capitals 0.81 of the
box's height when the width does not bind. Every line that has to be read across a street:

    region              pixels       on the prop (m)     capitals
    site_courthouse     400 x 100    1.21 x 0.3025       COURTHOUSE 46 px, 0.139 m
    site_vote           176 x 58     0.56 x 0.185        VOTE 45 px, 0.143 m (poster); 0.50 wide on the boards: 0.128 m
    site_here           160 x 58     0.44 x 0.16         HERE 45 px, 0.124 m
    site_sign           304 x 132    0.95 x 0.4125       DANGER 44 px, 0.138 m; KEEP OUT 45 px, 0.141 m
    site_opening        288 x 124    1.10 x 0.474        OPENING, SOON 45 px, 0.172 m
    site_notice         232 x 60     0.69 x 0.178        NOTICE 45 px, 0.134 m
    site_road_work      224 x 208    0.75 x 0.75         ROAD, WORK, AHEAD 45 px, 0.162 m
    site_skip_stencil   304 x 68     1.37 x 0.306        CITY SKIPS 45 px, 0.203 m
    site_evidence       272 x 62     0.70 x 0.16         EVIDENCE 41 px, 0.106 m (a door plate indoors, not a street sign)

The four campaign colours are CivicColours.Party(0..3) of Scripts/Civic/CivicProps.cs, to the digit.
"""
import math

import numpy as np

PARTIES = ((0.80, 0.42, 0.08), (0.10, 0.50, 0.52), (0.48, 0.22, 0.55), (0.42, 0.52, 0.14))      # amber, teal, plum, olive


def register(a):
    WHITE, BLACK, INK = a.WHITE, a.BLACK, a.INK
    YELLOW = a.SWATCHES["paint_yellow"][0]
    BLUE = a.SWATCHES["paint_blue"][0]
    NAVY = a.SWATCHES["vinyl_navy"][0]
    CARD = a.SWATCHES["paper"][0]
    RED = (0.80, 0.08, 0.06)

    swatches = {"site_party_%d" % k: (colour, 0.0, 0.35, 0.15) for k, colour in enumerate(PARTIES)}
    swatches["site_door"] = ((0.30, 0.33, 0.38), 0.0, 0.45, 0.35)

    # ------------------------------------------------------------------------------------------ the courthouse

    def courthouse():
        cream, brass = (0.93, 0.90, 0.80), (0.78, 0.62, 0.26)
        layer = a.Layer(400, 100, NAVY, 0.0, 0.5)
        layer.text("COURTHOUSE", (4, 2, 396, 60), cream, weight=0.06, smooth=0.55)
        layer.rect(40, 67, 360, 70, brass, metal=1.0, smooth=0.7)
        layer.text("EQUAL JUSTICE UNDER LAW", (70, 76, 330, 96), brass, weight=0.04, spacing=0.05, metal=1.0, smooth=0.7)
        return layer.finish(grain=0.035, blotch=0.05)

    def scales():
        brass = (0.80, 0.64, 0.28)
        gold = dict(metal=1.0, smooth=0.75)
        layer = a.Layer(64, 64, NAVY, 0.0, 0.5)
        cx = 32
        layer.rect(cx - 2, 10, cx + 2, 52, brass, **gold)
        layer.disc(cx, 9, 4, brass, **gold)
        layer.rect(cx - 11, 51, cx + 11, 55, brass, radius=1.5, **gold)
        layer.rect(cx - 16, 55, cx + 16, 60, brass, radius=1.5, **gold)
        layer.line([(cx - 20, 17), (cx, 13), (cx + 20, 17)], brass, width=2.6, **gold)
        for side in (-1, 1):
            x = cx + side * 20
            layer.disc(x, 17, 2.2, brass, **gold)
            for dx in (-8, 8):
                layer.line([(x, 17), (x + dx, 38)], brass, width=1.0, **gold)
            layer.poly([(x - 10, 38), (x + 10, 38), (x + 6, 45), (x - 6, 45)], brass, **gold)
        return layer.finish(grain=0.035, blotch=0.05)

    # ------------------------------------------------------------------------------------------ the campaign

    def vote():
        layer = a.Layer(176, 58, CARD, 0.0, 0.3)
        layer.text("VOTE", (6, 1, 170, 57), (0.09, 0.10, 0.16), weight=0.06, spacing=0.02)
        return layer.finish(grain=0.03, blotch=0.04)

    def here():
        layer = a.Layer(160, 58, CARD, 0.0, 0.3)
        layer.text("HERE", (6, 1, 154, 57), BLUE, weight=0.06, spacing=0.02)
        return layer.finish(grain=0.03, blotch=0.04)

    def polling():
        layer = a.Layer(208, 28, BLUE, 0.0, 0.38)
        layer.text("POLLING PLACE", (6, 2, 202, 26), WHITE, weight=0.05, spacing=0.03)
        return layer.finish(grain=0.03, blotch=0.04)

    def face(colour, hair):
        def paint():
            pale = tuple(0.72 + 0.28 * c for c in colour)
            deep = tuple(c * 0.50 for c in colour)
            layer = a.Layer(64, 80, pale, 0.0, 0.3)
            layer.disc(32, 40, 29, CARD)
            layer.rect(8, 50, 56, 96, deep, radius=14)                                   # shoulders
            layer.rect(27, 36, 37, 54, deep)
            layer.disc(32, 29, 12, deep)
            if hair == 1:                                                                # a bob
                layer.disc(32, 25, 13.5, deep)
                layer.rect(18.5, 25, 45.5, 45, deep, radius=5)
            elif hair == 2:                                                              # a flat top
                layer.rect(19, 15, 45, 24, deep, radius=3)
            elif hair == 3:                                                              # hair up
                layer.disc(32, 13, 6.5, deep)
                layer.disc(32, 26, 13, deep)
            layer.poly([(26, 51), (32, 59), (38, 51)], CARD)                             # collar
            if hair in (0, 2):
                layer.poly([(30, 54), (34, 54), (35.5, 66), (28.5, 66)], colour)         # a tie
            else:
                layer.disc(32, 61, 2.4, colour)                                          # a brooch
            for x0, y0, x1, y1 in ((0, 0, 64, 2), (0, 78, 64, 80), (0, 0, 2, 80), (62, 0, 64, 80)):
                layer.rect(x0, y0, x1, y1, deep)
            return layer.finish(grain=0.03, blotch=0.04)
        return paint

    # ------------------------------------------------------------------------------------------ the building site

    def site_sign():
        board = (0.95, 0.95, 0.92)
        layer = a.Layer(304, 132, BLACK, 0.0, 0.4)
        layer.rect(3, 3, 301, 129, board, radius=7)
        layer.rect(8, 8, 296, 68, RED, radius=7)
        layer.text("DANGER", (38, 10, 266, 66), board, weight=0.06, spacing=0.02)
        layer.text("KEEP OUT", (10, 72, 294, 128), RED, weight=0.06, spacing=0.02)
        return layer.finish(grime=0.25, grain=0.04, blotch=0.06, scratches=14, edge=0.10)

    def hard_hat():
        blue = (0.04, 0.27, 0.62)
        layer = a.Layer(80, 80, (0.95, 0.95, 0.92), 0.0, 0.45)
        layer.disc(40, 40, 37, blue)
        layer.disc(40, 44, 20, WHITE)                                                    # the shell
        layer.rect(14, 44, 66, 70, blue)
        layer.rect(15, 42, 65, 50, WHITE, radius=4)                                      # the brim
        layer.rect(37, 21, 43, 44, blue)
        layer.rect(38.5, 21, 41.5, 44, WHITE)
        layer.rect(27, 55, 53, 58, WHITE, radius=1.5)                                    # the strap under the chin
        return layer.finish(grime=0.25, grain=0.03, blotch=0.05, scratches=4)

    def opening():
        board = (0.95, 0.95, 0.92)
        layer = a.Layer(288, 124, board, 0.0, 0.4)
        layer.text("OPENING", (8, 3, 280, 59), BLUE, weight=0.06, spacing=0.01)
        layer.text("SOON", (60, 65, 228, 121), RED, weight=0.06, spacing=0.05)
        for x0 in (10, 238):                                                             # a sunburst either side of SOON
            cx, cy = x0 + 20, 93
            for k in range(8):
                turn = math.radians(45 * k + 22.5)
                layer.line([(cx + math.cos(turn) * 9, cy + math.sin(turn) * 9), (cx + math.cos(turn) * 19, cy + math.sin(turn) * 19)], (0.96, 0.72, 0.06), width=3.0)
            layer.disc(cx, cy, 7, (0.96, 0.72, 0.06))
        return layer.finish(grime=0.12, grain=0.03, blotch=0.05)

    def notice():
        layer = a.Layer(232, 60, BLUE, 0.0, 0.4)
        layer.text("NOTICE", (10, 2, 222, 58), WHITE, weight=0.06, spacing=0.04)
        return layer.finish(grain=0.03, blotch=0.05)

    def road_work():
        orange = (0.96, 0.45, 0.03)
        layer = a.Layer(224, 208, orange, 0.0, 0.5)
        layer.rect(5, 5, 219, 203, BLACK, radius=13)
        layer.rect(9, 9, 215, 199, orange, radius=10)
        layer.text("ROAD", (20, 16, 204, 72), BLACK, weight=0.06, spacing=0.03)
        layer.text("WORK", (20, 76, 204, 132), BLACK, weight=0.06, spacing=0.03)
        layer.text("AHEAD", (14, 136, 210, 192), BLACK, weight=0.06, spacing=0.01)
        return layer.finish(grime=0.22, grain=0.03, blotch=0.05, scratches=12)

    def planks():
        rng = np.random.default_rng(811)
        timber = (0.66, 0.52, 0.34)
        layer = a.Layer(192, 96, (0.10, 0.08, 0.06), 0.0, 0.12)
        for k in range(4):
            y0 = k * 24
            tone = rng.uniform(0.80, 1.10)
            body = tuple(min(1.0, c * tone) for c in timber)
            layer.rect(0, y0 + 1.5, 192, y0 + 22.5, body)
            for _ in range(7):                                                            # the grain runs along the board
                y = y0 + rng.uniform(3, 21)
                x = rng.uniform(-20, 150)
                layer.line([(x, y), (x + rng.uniform(40, 110), y + rng.uniform(-1.2, 1.2))], tuple(c * rng.uniform(0.80, 0.92) for c in body), width=1.0)
            joint = rng.uniform(40, 150)                                                  # where two boards butt, and their end bands
            layer.rect(joint - 1, y0 + 1.5, joint + 1, y0 + 22.5, (0.12, 0.10, 0.07))
            for x in (joint - 7, joint + 7):
                layer.rect(x - 2.5, y0 + 1.5, x + 2.5, y0 + 22.5, (0.55, 0.56, 0.58), metal=1.0, smooth=0.5)
            for _ in range(3):                                                            # mortar and boot marks
                layer.disc(rng.uniform(0, 192), y0 + rng.uniform(5, 19), rng.uniform(2, 5), (0.74, 0.72, 0.68))
        return layer.finish(grime=0.15, grain=0.06, blotch=0.10)

    def net():
        # Debris netting, one repeat: the props lay it mirrored tile by tile, so nothing has to match at its edges.
        green = (0.16, 0.40, 0.22)
        layer = a.Layer(64, 64, green, 0.0, 0.18)
        for k in range(0, 64, 4):
            layer.line([(k + 0.5, 0), (k + 0.5, 64)], tuple(c * 0.74 for c in green), width=1.0)
            layer.line([(0, k + 0.5), (64, k + 0.5)], tuple(c * 0.80 for c in green), width=1.0)
        for y in (0, 62):                                                                 # the woven bands the ties go through
            layer.rect(0, y, 64, y + 2, tuple(min(1.0, c * 1.45) for c in green))
        layer.rect(0, 30, 64, 33, tuple(min(1.0, c * 1.25) for c in green))
        for x in (8, 40):
            layer.disc(x, 31.5, 1.6, (0.05, 0.10, 0.06))
        return layer.finish(grain=0.06, blotch=0.12)

    def skip_stencil():
        layer = a.Layer(304, 68, YELLOW, 0.0, 0.45)
        layer.text("CITY SKIPS", (8, 6, 296, 62), BLACK, weight=0.05, spacing=0.02)
        for x in (38, 68, 98, 132, 160, 198, 228, 256):                                   # the bridges of a stencil
            layer.rect(x, 0, x + 2.5, 68, YELLOW)
        return layer.finish(grime=0.12, grain=0.05, blotch=0.08, scratches=18)

    def rubble():
        rng = np.random.default_rng(823)
        layer = a.Layer(96, 96, (0.30, 0.23, 0.16), 0.0, 0.08)
        tones = ((0.62, 0.61, 0.58), (0.50, 0.48, 0.45), (0.58, 0.24, 0.15), (0.30, 0.29, 0.27), (0.20, 0.15, 0.10), (0.74, 0.72, 0.66), (0.40, 0.30, 0.20),
                 (0.11, 0.11, 0.12), (0.36, 0.27, 0.18))
        for _ in range(150):
            x, y, r, turn = rng.uniform(0, 96), rng.uniform(0, 96), rng.uniform(2.0, 7.5), rng.uniform(0, 6.28)
            count = int(rng.integers(3, 6))
            tone = tones[int(rng.integers(len(tones)))]
            shape = [(x + math.cos(turn + 6.28 * k / count) * r * rng.uniform(0.6, 1.0), y + math.sin(turn + 6.28 * k / count) * r * rng.uniform(0.6, 1.0)) for k in range(count)]
            layer.poly([(px + 1.2, py + 1.5) for px, py in shape], (0.13, 0.10, 0.08))        # its shadow on the heap
            shade = rng.uniform(0.85, 1.1)
            layer.poly(shape, tuple(c * shade for c in tone))
        return layer.finish(grain=0.08, blotch=0.10)

    # ------------------------------------------------------------------------------------------ the road works

    def trench():
        # Looking down into 0.62 x 2.0 m of open trench: the cut sides (one lit, one in shadow), a dark bottom, a gas
        # main on its bed of sand half buried again, a cable. Nothing but stones changes along the length, so pieces
        # laid end to end join.
        rng = np.random.default_rng(853)
        floor = (0.070, 0.055, 0.042)
        layer = a.Layer(48, 128, floor, 0.0, 0.06)
        for k, tone in enumerate(((0.09, 0.09, 0.10), (0.38, 0.36, 0.33), (0.31, 0.23, 0.15), (0.21, 0.16, 0.10), (0.13, 0.10, 0.07))):
            layer.rect(48 - 1.9 * (k + 1), 0, 48 - 1.9 * k, 128, tone)                       # asphalt, road base, then soil going down
            layer.rect(1.9 * k, 0, 1.9 * (k + 1), 128, tuple(c * 0.42 for c in tone))
        layer.rect(9.5, 0, 17, 128, (0.035, 0.028, 0.022))                                # the shadow of the near side
        for _ in range(40):
            layer.disc(rng.uniform(11, 37), rng.uniform(0, 128), rng.uniform(0.7, 1.9), tuple(c * rng.uniform(0.6, 2.6) for c in floor))
        layer.rect(21.5, 0, 33.5, 128, (0.25, 0.20, 0.13))                                # the sand the main is laid on
        for y in range(3, 128, 5):
            layer.disc(27.5 + rng.choice((-6.0, 6.0)) + rng.uniform(-0.8, 0.8), y, rng.uniform(1.2, 2.4), (0.25, 0.20, 0.13))
        layer.rect(24, 0, 25.2, 128, (0.10, 0.08, 0.05))
        layer.rect(25, 0, 30, 128, (0.17, 0.19, 0.22), metal=0.6, smooth=0.45)            # the main: black iron, not a yellow line on a road
        layer.rect(27.6, 0, 28.8, 128, (0.40, 0.43, 0.48), metal=0.6, smooth=0.55)
        for y in (38, 102):
            layer.rect(24.2, y - 2.5, 30.8, y + 2.5, (0.50, 0.36, 0.06), smooth=0.4)          # its yellow joint wraps
        layer.rect(14.2, 0, 15.6, 128, (0.45, 0.14, 0.06))                                # a cable in its red duct
        for y in (22, 86):                                                                # where it has been covered again
            for _ in range(5):
                layer.disc(27 + rng.uniform(-4, 4), y + rng.uniform(-5, 5), rng.uniform(2.0, 3.6), tuple(c * rng.uniform(0.8, 1.2) for c in (0.19, 0.14, 0.09)))
        return layer.finish(grain=0.07, blotch=0.10)

    def asphalt():
        # Fresh surfacing, 1.2 x 2.0 m: black and fine, sealed along both edges, the roller's laps just visible.
        rng = np.random.default_rng(857)
        black = (0.050, 0.050, 0.056)
        layer = a.Layer(72, 112, black, 0.0, 0.30)
        for _ in range(520):
            tone = rng.uniform(0.6, 2.6)
            layer.disc(rng.uniform(0, 72), rng.uniform(0, 112), rng.uniform(0.3, 0.8), tuple(c * tone for c in black))
        for x in (24, 48):
            layer.line([(x, 0), (x, 112)], tuple(c * 0.7 for c in black), width=1.0, smooth=0.4)
        for x0, x1 in ((0, 4), (68, 72)):
            layer.rect(x0, 0, x1, 112, (0.018, 0.018, 0.022), smooth=0.62)                # the bitumen seal
        return layer.finish(grain=0.10, blotch=0.07)

    def manhole():
        iron, lit = (0.15, 0.15, 0.16), (0.27, 0.27, 0.28)
        cast = dict(metal=0.9, smooth=0.42)
        layer = a.Layer(96, 96, iron, 0.9, 0.35)
        layer.disc(48, 48, 47, lit, **cast)
        layer.disc(48, 48, 43.5, iron, **cast)
        for i in range(-5, 6):                                                           # the tread: squares, clear of the centre plate
            for j in range(-5, 6):
                x, y = 48 + i * 7.6, 48 + j * 7.6
                if 15.5 < math.hypot(x - 48, y - 48) < 39:
                    layer.rect(x - 2.4, y - 2.4, x + 2.4, y + 2.4, lit, radius=0.8, **cast)
        layer.disc(48, 48, 15, lit, **cast)
        layer.disc(48, 48, 13, iron, **cast)
        layer.text("CITY", (36, 39, 60, 48), lit, weight=0.05, **cast)
        layer.text("SEWER", (36, 48.5, 60, 57), lit, weight=0.05, **cast)
        for x in (10, 86):                                                               # the key holes
            layer.rect(x - 3.5, 46, x + 3.5, 50, (0.02, 0.02, 0.02), radius=1.5, metal=0.0, smooth=0.1)
        return layer.finish(grain=0.07, blotch=0.12)

    def shaft():
        # Down an open manhole: brick lining, step irons on one side, water catching the light at the bottom.
        layer = a.Layer(64, 64, (0.02, 0.02, 0.02), 0.0, 0.10)
        for radius, tone in ((33, (0.34, 0.31, 0.28)), (29, (0.24, 0.21, 0.18)), (25, (0.15, 0.13, 0.11)), (21, (0.08, 0.07, 0.06)), (17, (0.035, 0.03, 0.03))):
            layer.disc(32, 32, radius, tone)
        for k in range(16):
            turn = math.radians(22.5 * k)
            layer.line([(32 + math.cos(turn) * 22, 32 + math.sin(turn) * 22), (32 + math.cos(turn) * 33, 32 + math.sin(turn) * 33)], (0.10, 0.09, 0.08), width=0.8)
        for k, (y, half, tone) in enumerate(((7.5, 8.0, 0.60), (12.5, 6.4, 0.42), (16.5, 5.0, 0.28), (19.5, 3.8, 0.17))):
            layer.rect(32 - half, y, 32 + half, y + 1.8 - 0.25 * k, (tone, tone, tone * 1.03), metal=1.0, smooth=0.5)
        layer.disc(32, 36, 10, (0.05, 0.07, 0.08), smooth=0.9)
        layer.disc(35, 39, 3.5, (0.16, 0.21, 0.23), smooth=0.9)
        return layer.finish(grain=0.06, blotch=0.08)

    # ------------------------------------------------------------------------------------------ the station

    def evidence():
        blue = (0.06, 0.17, 0.45)                                                        # StationProps.PoliceBlue
        layer = a.Layer(272, 62, blue, 0.0, 0.5)
        layer.rect(3, 3, 269, 59, WHITE, radius=4)
        layer.rect(5.5, 5.5, 266.5, 56.5, blue, radius=3)
        layer.text("EVIDENCE", (12, 6, 260, 56), WHITE, weight=0.06, spacing=0.03)
        return layer.finish(grain=0.03, blotch=0.04, scratches=5)

    hair = (0, 1, 2, 3)
    decals = {
        "site_courthouse": courthouse,
        "site_scales": scales,
        "site_vote": vote,
        "site_face_0": face(PARTIES[0], hair[0]),
        "site_face_1": face(PARTIES[1], hair[1]),
        "site_face_2": face(PARTIES[2], hair[2]),
        "site_face_3": face(PARTIES[3], hair[3]),
        "site_here": here,
        "site_polling": polling,
        "site_sign": site_sign,
        "site_hard_hat": hard_hat,
        "site_opening": opening,
        "site_notice": notice,
        "site_road_work": road_work,
        "site_planks": planks,
        "site_net": net,
        "site_skip_stencil": skip_stencil,
        "site_rubble": rubble,
        "site_trench": trench,
        "site_asphalt": asphalt,
        "site_manhole": manhole,
        "site_shaft": shaft,
        "site_evidence": evidence,
    }
    return swatches, decals
