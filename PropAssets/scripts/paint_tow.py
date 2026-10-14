"""What the tow truck of props_tow.py is painted with: the livery (white with a navy skirt and an amber stripe round the cab,
the fictional firm Bridgeway Towing lettered on its doors and its headboard), its amber light bar, its grille, its plate,
the bed's steel planks and its reflective strips. One function, register(a), as atlas.py asks of a later round. The
lamps, the glass and the wheels are the city bus's (paint_service.py).

Lettering sizes: a decal is drawn for the size it has on the truck (given beside each painter), capital letters at least
44 px high where they are to be read from the pavement.
"""
import numpy as np

COACH_WHITE = (0.90, 0.90, 0.87)       # paint_service's svc_coach_white: the cab's white
NAVY = (0.06, 0.15, 0.34)
AMBER = (0.97, 0.60, 0.06)
LETTERS = (0.06, 0.15, 0.34)


def register(a):
    Layer = a.Layer

    swatches = {
        "tow_blue": (NAVY, 0.25, 0.75, 0.55),
        "tow_amber": (AMBER, 0.0, 0.70, 0.35),
    }

    def paint_light_bar():            # the bar seen from the front, 1.40 m: four amber modules
        amber = (0.98, 0.56, 0.04)
        layer = Layer(256, 36, (0.03, 0.03, 0.035), 0.0, 0.5)
        for x0, x1 in ((4, 62), (66, 126), (130, 190), (194, 252)):
            layer.rect(x0, 3, x1, 33, tuple(v * 0.45 for v in amber), smooth=0.9, radius=4)
            layer.rect(x0 + 1.5, 4.5, x1 - 1.5, 31.5, amber, smooth=0.95, radius=3)
            for x in np.arange(x0 + 5, x1 - 3, 4.0):       # the lens's ribs
                layer.line([(x, 6), (x, 30)], tuple(v * 0.80 for v in amber), width=1)
            for k in range(3):                             # the lamps behind it
                cx = x0 + (x1 - x0) * (k + 0.5) / 3
                layer.disc(cx, 18, 5.0, tuple(min(1.0, v * 1.1 + 0.10) for v in amber), smooth=0.95)
                layer.disc(cx, 18, 2.4, (1.0, 0.95, 0.80), smooth=0.95)
        return layer.finish(grain=0.012, blotch=0.02)

    def paint_door():                 # 1.08 x 0.44 m on each cab door, over the stripe: 400 x 164 px
        layer = Layer(400, 164, COACH_WHITE, 0.15, 0.72)
        layer.text("BRIDGEWAY", (10, 6, 390, 82), LETTERS, weight=0.08, spacing=0.06, smooth=0.75)
        layer.rect(14, 88, 386, 93, AMBER, smooth=0.7)
        layer.text("TOWING & RECOVERY", (14, 100, 386, 128), LETTERS, weight=0.06, spacing=0.05, smooth=0.7)
        layer.text("24 HOURS  555 0142", (60, 134, 340, 158), LETTERS, weight=0.05, spacing=0.05, smooth=0.7)
        return layer.finish(grain=0.008, blotch=0.0)

    def paint_headboard():            # 1.96 x 0.44 m on the back of the headboard: 512 x 116 px
        layer = Layer(512, 116, NAVY, 0.25, 0.75)
        layer.rect(4, 4, 508, 112, AMBER, smooth=0.7, radius=6)
        layer.rect(9, 9, 503, 107, NAVY, smooth=0.75, radius=4)
        layer.text("BRIDGEWAY TOWING", (24, 16, 488, 72), (0.96, 0.96, 0.94), weight=0.08, spacing=0.06, smooth=0.75)
        layer.text("555 0142", (150, 78, 362, 102), AMBER, weight=0.06, spacing=0.08, smooth=0.7)
        return layer.finish(grime=0.15, grain=0.02, blotch=0.03)

    def paint_grille():               # 1.20 x 0.40 m on the cab front: black slats in a white surround, the badge
        layer = Layer(240, 80, COACH_WHITE, 0.15, 0.72)
        layer.rect(4, 4, 236, 76, (0.55, 0.56, 0.58), metal=1.0, smooth=0.8, radius=8)
        layer.rect(8, 8, 232, 72, (0.02, 0.02, 0.025), metal=0.0, smooth=0.3, radius=5)
        for y in np.arange(12.0, 70.0, 6.0):
            layer.rect(10, y, 230, y + 2.6, (0.16, 0.16, 0.17), smooth=0.45)
            layer.line([(10, y + 0.4), (230, y + 0.4)], (0.30, 0.30, 0.32), width=1)
        layer.rect(96, 30, 144, 50, NAVY, smooth=0.8, radius=4)
        layer.text("BW", (100, 32, 140, 48), AMBER, weight=0.07)
        return layer.finish(grime=0.15, grain=0.03, blotch=0.04)

    def paint_plate():                # 0.36 x 0.17 m
        layer = Layer(96, 48, (0.93, 0.92, 0.84), 0.0, 0.5)
        layer.rect(0, 0, 96, 48, (0.05, 0.10, 0.30), radius=5)
        layer.rect(2.5, 2.5, 93.5, 45.5, (0.93, 0.92, 0.84), radius=4)
        layer.text("TW 0142", (8, 11, 88, 40), (0.05, 0.10, 0.30), weight=0.06, spacing=0.03)
        return layer.finish(grime=0.25, grain=0.03, blotch=0.04)

    def paint_deck():                 # the bed's top, 6.02 x 2.40 m: steel planks along it, hazard stripes over the last 0.40 m at the tail (left)
        layer = Layer(384, 160, (0.30, 0.31, 0.33), 1.0, 0.45)
        for y in np.arange(0.0, 160.0, 10.0):              # the planks' joints along the bed
            layer.rect(0, y, 384, y + 1.6, (0.12, 0.12, 0.13), metal=1.0, smooth=0.3)
            layer.line([(0, y + 2.2), (384, y + 2.2)], (0.44, 0.45, 0.47), width=1)
        for x in np.arange(40.0, 384.0, 64.0):             # the cross bearers seen as rows of welds
            layer.rect(x, 0, x + 1.5, 160, (0.22, 0.22, 0.24), metal=1.0, smooth=0.35)
        tail = 26                                          # px: the last 0.40 m of the bed
        layer.rect(0, 0, tail, 160, (0.96, 0.78, 0.06), metal=0.0, smooth=0.5)
        for y in np.arange(-24.0, 160.0, 16.0):           # black bars slanting across the yellow
            layer.poly([(0, y), (tail, y + 14), (tail, y + 22), (0, y + 8)], (0.03, 0.03, 0.035), metal=0.0, smooth=0.4)
        layer.rect(tail, 0, tail + 2, 160, (0.12, 0.12, 0.13), metal=1.0, smooth=0.3)
        return layer.finish(grime=0.35, grain=0.04, blotch=0.07, scratches=30)

    def paint_reflect():              # the strip along each side of the bed, 5.50 x 0.08 m: red and white blocks
        layer = Layer(512, 12, (0.95, 0.95, 0.93), 0.0, 0.8)
        for x in range(0, 512, 32):
            layer.rect(x, 0, x + 16, 12, (0.80, 0.05, 0.04), smooth=0.8)
        return layer.finish(grime=0.2, grain=0.01, blotch=0.02)

    def paint_stripes():              # the underride bar's face, 2.26 x 0.11 m: red and white, slanting
        layer = Layer(256, 16, (0.95, 0.95, 0.93), 0.0, 0.7)
        for k in range(-2, 22):
            x = k * 12.0
            layer.poly([(x, 16), (x + 6, 16), (x + 12, 0), (x + 6, 0)], (0.80, 0.05, 0.04), smooth=0.7)
        return layer.finish(grime=0.3, grain=0.02, blotch=0.03)

    decals = {
        "tow_light_bar": paint_light_bar,
        "tow_door": paint_door,
        "tow_headboard": paint_headboard,
        "tow_grille": paint_grille,
        "tow_plate": paint_plate,
        "tow_deck": paint_deck,
        "tow_reflect": paint_reflect,
        "tow_stripes": paint_stripes,
    }
    return swatches, decals
