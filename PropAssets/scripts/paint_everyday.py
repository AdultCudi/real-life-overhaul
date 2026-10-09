"""The painting of the everyday props (props_everyday.py): what is cooked in, drunk from, carried home, fished
with, browsed, filled up at and nailed over a shop window. Pure Pillow; atlas.py calls register(a) with itself.
"""
import math

import numpy as np

EMPLOYERS = (                       # in the order of Jobs/CareerCatalogue.All: employer_sign_1 .. _6
    ("BRIGHTLINE", "PROMOTIONS", (0.62, 0.10, 0.42), (0.99, 0.82, 0.25)),
    ("BRIGHTSIDE", "CONTRACT CLEANING", (0.04, 0.36, 0.42), (0.80, 0.95, 0.95)),
    ("UNCLE LOU'S", "STREET EATS", (0.68, 0.08, 0.06), (0.99, 0.86, 0.35)),
    ("FAIRWAY", "MARKETS", (0.06, 0.34, 0.16), (0.96, 0.93, 0.80)),
    ("HUDSON", "PARCEL CO.", (0.30, 0.17, 0.08), (0.98, 0.78, 0.30)),
    ("FIVE BOROUGH", "CABS", (0.06, 0.06, 0.07), (0.99, 0.80, 0.06)),
)


def register(a):
    Layer, WHITE, BLACK, INK = a.Layer, a.WHITE, a.BLACK, a.INK
    CARD = a.SWATCHES["cardboard"][0]

    swatches = {
        "everyday_wine": ((0.34, 0.02, 0.07), 0.0, 0.92, 0.0),
        "everyday_glass_brown": ((0.25, 0.11, 0.03), 0.0, 0.88, 0.0),
        "everyday_pink": ((0.93, 0.42, 0.58), 0.0, 0.22, 0.0),
    }

    def pan_food():
        layer = Layer(80, 80, (0.05, 0.05, 0.055), 0.0, 0.45)
        layer.disc(40, 40, 38, (0.065, 0.065, 0.07), smooth=0.5)
        blob = [(31 + math.cos(t) * (17 + 3.5 * math.sin(3 * t + 1)), 44 + math.sin(t) * (15 + 3 * math.cos(2 * t))) for t in np.linspace(0, 2 * math.pi, 28, endpoint=False)]
        layer.poly(blob, (0.80, 0.72, 0.52), smooth=0.3)
        layer.poly([(31 + (x - 31) * 0.86, 44 + (y - 44) * 0.86) for x, y in blob], (0.96, 0.95, 0.90), smooth=0.5)
        layer.disc(29, 45, 7.5, (0.90, 0.55, 0.05), smooth=0.8)
        layer.disc(28, 44, 6, (0.98, 0.72, 0.08), smooth=0.85)
        layer.disc(26, 42, 1.8, (1.0, 0.93, 0.6), smooth=0.9)
        for x0, y0, x1, y1 in ((50, 16, 66, 50), (58, 26, 72, 58)):          # two rashers
            wave = [(x0 + (x1 - x0) * t + 2.5 * math.sin(t * 9), y0 + (y1 - y0) * t) for t in np.linspace(0, 1, 12)]
            layer.line(wave, (0.42, 0.10, 0.06), width=8.5)
            layer.line(wave, (0.66, 0.20, 0.12), width=6.5)
            layer.line([(x + 1.5, y) for x, y in wave], (0.90, 0.68, 0.52), width=1.6)
        return layer.finish(grain=0.04, blotch=0.05)

    def plate_meal():
        china = (0.93, 0.93, 0.90)
        layer = Layer(96, 96, china, 0.0, 0.75)
        layer.disc(48, 48, 45.5, (0.14, 0.26, 0.50), smooth=0.75)             # a blue line round the rim
        layer.disc(48, 48, 43.5, china, smooth=0.75)
        layer.disc(48, 48, 31, (0.80, 0.80, 0.77), smooth=0.7)
        layer.disc(48, 48, 30, (0.95, 0.95, 0.92), smooth=0.75)
        steak = [(40 + math.cos(t) * (15 + 2.5 * math.sin(2 * t)), 54 + math.sin(t) * (10 + 2 * math.cos(3 * t))) for t in np.linspace(0, 2 * math.pi, 24, endpoint=False)]
        layer.poly([(x + 1.5, y + 2) for x, y in steak], (0.50, 0.36, 0.22), smooth=0.5)           # the gravy
        layer.poly(steak, (0.36, 0.17, 0.08), smooth=0.35)
        for k in range(4):
            layer.line([(29 + 6 * k, 47), (35 + 6 * k, 61)], (0.20, 0.08, 0.04), width=1.6)
        rng = np.random.default_rng(81)
        for _ in range(11):                                                        # chips
            x, y, t = rng.uniform(50, 66), rng.uniform(30, 46), rng.uniform(0, math.pi)
            layer.line([(x - 6 * math.cos(t), y - 6 * math.sin(t)), (x + 6 * math.cos(t), y + 6 * math.sin(t))], (0.72, 0.50, 0.12), width=4)
            layer.line([(x - 5 * math.cos(t), y - 5 * math.sin(t)), (x + 5 * math.cos(t), y + 5 * math.sin(t))], (0.95, 0.76, 0.26), width=2.6)
        for _ in range(26):                                                        # peas
            x, y = rng.normal(34, 6), rng.normal(33, 4.5)
            layer.disc(x, y, 2.3, (0.20, 0.42, 0.10))
            layer.disc(x - 0.5, y - 0.5, 1.5, (0.42, 0.68, 0.20), smooth=0.6)
        return layer.finish(grain=0.02, blotch=0.03)

    def checkpoint_flag():
        blue = (0.10, 0.45, 0.92)
        layer = Layer(224, 64, blue, 0.0, 0.3)
        layer.rect(0, 0, 224, 6, WHITE)
        layer.rect(0, 58, 224, 64, WHITE)
        layer.text("CHECKPOINT", (30, 13, 194, 51), WHITE, weight=0.07, spacing=0.05)
        for x in (6, 200):
            for k in range(2):
                layer.poly([(x + k * 10, 16), (x + 9 + k * 10, 32), (x + k * 10, 48), (x + 5 + k * 10, 48), (x + 14 + k * 10, 32), (x + 5 + k * 10, 16)], (0.99, 0.86, 0.15))
        return layer.finish(grain=0.03, blotch=0.03)

    def beer_label():
        cream, red, gold = (0.95, 0.91, 0.76), (0.66, 0.08, 0.06), (0.80, 0.60, 0.20)
        layer = Layer(112, 56, cream, 0.0, 0.35)
        layer.rect(0, 0, 112, 5, gold, metal=1.0, smooth=0.7)
        layer.rect(0, 51, 112, 56, gold, metal=1.0, smooth=0.7)
        layer.rect(14, 9, 98, 47, red, radius=18)
        layer.rect(17, 12, 95, 44, cream, radius=15)
        layer.text("LAGER", (25, 16, 87, 34), red, weight=0.08)
        layer.text("CITY BREW", (30, 35, 82, 42), INK, weight=0.04)
        return layer.finish(grain=0.03, blotch=0.03)

    def popcorn_label():
        red, yellow = (0.74, 0.07, 0.05), (0.99, 0.84, 0.18)
        layer = Layer(112, 40, yellow, 0.0, 0.3)
        layer.rect(2, 2, 110, 38, red, radius=8)
        layer.text("POPCORN", (9, 8, 103, 32), yellow, weight=0.08)
        return layer.finish(grain=0.03, blotch=0.03)

    def popcorn_top():
        layer = Layer(64, 64, (0.50, 0.36, 0.12), 0.0, 0.1)
        rng = np.random.default_rng(83)
        for _ in range(150):
            x, y = rng.uniform(-3, 67), rng.uniform(-3, 67)
            tone = rng.uniform(0.86, 1.0)
            body = (0.99 * tone, 0.93 * tone, 0.70 * tone)
            layer.disc(x, y, 4.6, tuple(c * 0.62 for c in body))
            for _ in range(3):                                                     # each kernel a little cloud
                layer.disc(x + rng.uniform(-2.2, 2.2), y + rng.uniform(-2.2, 2.2), rng.uniform(1.8, 3.0), body)
            layer.disc(x + rng.uniform(-1, 1), y + rng.uniform(-1, 1), 0.9, (0.70, 0.42, 0.10))
        return layer.finish(grain=0.04, blotch=0.03)

    def takeaway_side():
        white, red = (0.96, 0.95, 0.92), (0.78, 0.09, 0.07)
        layer = Layer(80, 64, white, 0.0, 0.3)
        for k, (half, y) in enumerate(((11, 14), (15, 24), (19, 34))):            # a pagoda
            layer.poly([(40 - half - 4, y + 4), (40 - half + 3, y - 2), (40 + half - 3, y - 2), (40 + half + 4, y + 4)], red)
            layer.rect(40 - half + 5, y + 4, 40 + half - 5, y + 8, red)
        layer.rect(22, 42, 58, 45, red)
        layer.text("THANK YOU", (12, 48, 68, 58), red, weight=0.06)
        return layer.finish(grain=0.03, blotch=0.03)

    def noodles():
        layer = Layer(64, 64, (0.50, 0.33, 0.10), 0.0, 0.45)
        rng = np.random.default_rng(85)
        for _ in range(46):
            x, y, turn = rng.uniform(0, 64), rng.uniform(0, 64), rng.uniform(0, 6.28)
            bend = rng.uniform(-0.09, 0.09)
            path = []
            for _ in range(12):
                path.append((x, y))
                x, y, turn = x + 4 * math.cos(turn), y + 4 * math.sin(turn), turn + bend * 4
            layer.line(path, (0.60, 0.40, 0.12), width=4.2)
            layer.line(path, (0.93, 0.76, 0.36), width=2.8)
        for _ in range(9):
            x, y = rng.uniform(6, 58), rng.uniform(6, 58)
            layer.disc(x, y, 3.2, (0.18, 0.45, 0.12) if rng.uniform() < 0.5 else (0.90, 0.40, 0.08))
        for _ in range(4):
            x, y = rng.uniform(10, 54), rng.uniform(10, 54)
            layer.rect(x - 5, y - 3, x + 5, y + 3, (0.50, 0.24, 0.12), radius=2)
        return layer.finish(grain=0.04, blotch=0.04)

    def box_side():
        red = (0.72, 0.10, 0.08)
        layer = Layer(160, 112, CARD, 0.0, 0.12)
        for y in range(3, 112, 5):                                                 # the corrugation showing through
            layer.line([(0, y), (160, y)], tuple(c * 0.965 for c in CARD), width=1.2)
        layer.rect(0, 0, 160, 15, a.SWATCHES["tape_brown"][0], smooth=0.6)        # the tape over the top edge
        layer.line([(0, 15), (160, 15)], tuple(c * 0.7 for c in CARD), width=1)
        layer.text("FRAGILE", (10, 30, 104, 62), red, weight=0.08)
        layer.rect(10, 66, 104, 69, red)
        layer.text("HANDLE WITH CARE", (10, 73, 104, 83), red, weight=0.05)
        for x in (116, 138):                                                       # this way up
            layer.poly([(x, 46), (x + 8, 30), (x + 16, 46), (x + 11, 46), (x + 11, 66), (x + 5, 66), (x + 5, 46)], red)
        layer.rect(114, 70, 156, 73, red)
        layer.poly([(24, 90), (40, 90), (36, 98), (33, 99), (33, 105), (38, 107), (26, 107), (31, 105), (31, 99), (28, 98)], red)      # a glass
        for k in range(3):
            layer.line([(52, 93 + 5 * k), (104 - 12 * k, 93 + 5 * k)], tuple(c * 0.55 for c in CARD), width=1.6)
        return layer.finish(grime=0.15, grain=0.05, blotch=0.07)

    def ledger_front():
        rng = np.random.default_rng(87)
        layer = Layer(216, 304, a.NEWSPRINT, 0.0, 0.12)
        layer.line([(8, 7), (208, 7)], INK, width=1)
        layer.text("THE CITY LEDGER", (10, 10, 206, 40), INK, weight=0.05)
        layer.line([(8, 44), (208, 44)], INK, width=2.5)
        for x0, x1 in ((8, 52), (86, 130), (164, 208)):                            # the date line
            layer.line([(x0, 48.5), (x1, 48.5)], (0.38, 0.38, 0.40), width=1.3)
        layer.line([(8, 53), (208, 53)], INK, width=1)
        layer.text("MAYOR OPENS BRIDGE", (10, 58, 206, 82), INK, weight=0.075)
        a._photo(layer, 8, 90, 132, 180, sky=(0.66, 0.68, 0.70))
        a._columns(layer, rng, 138, 208, 91, 180, 1)
        layer.line([(8, 187), (208, 187)], INK, width=1)
        layer.text("SHOP RENTS RISE", (8, 191, 120, 206), INK, weight=0.05)
        a._columns(layer, rng, 8, 208, 212, 268, 4)
        layer.line([(8, 273), (208, 273)], INK, width=1)
        layer.rect(8, 277, 60, 298, (0.30, 0.30, 0.32))
        layer.text("WEATHER", (11, 281, 57, 294), a.NEWSPRINT, weight=0.05)
        a._columns(layer, rng, 66, 208, 278, 298, 3)
        return layer.finish(grain=0.035, blotch=0.06)

    def rack_sign():
        red = (0.80, 0.10, 0.08)
        layer = Layer(160, 56, red, 0.0, 0.5)
        layer.rect(3, 3, 157, 53, WHITE, radius=5)
        layer.rect(6, 6, 154, 50, red, radius=3)
        layer.text("SALE", (16, 9, 104, 47), WHITE, weight=0.085, spacing=0.05)
        layer.disc(128, 28, 17, (0.99, 0.84, 0.18))
        layer.text("-50%", (114, 20, 142, 36), red, weight=0.08)
        return layer.finish(grain=0.03, blotch=0.03)

    def bag_logo():
        cream, navy = (0.95, 0.93, 0.87), (0.07, 0.11, 0.25)
        layer = Layer(80, 96, cream, 0.0, 0.25)
        layer.rect(0, 70, 80, 96, navy)
        layer.disc(40, 36, 21, navy)
        layer.disc(40, 36, 18.5, cream)
        layer.disc(40, 36, 16.5, navy)
        layer.text("M", (28, 24, 52, 48), cream, weight=0.07)
        layer.text("MODA", (16, 74, 64, 86), cream, weight=0.06, spacing=0.12)
        layer.line([(20, 90), (60, 90)], (0.80, 0.60, 0.20), width=1.2)
        return layer.finish(grain=0.04, blotch=0.04)

    def fare_reader():
        layer = Layer(56, 56, (0.03, 0.03, 0.035), 0.0, 0.8)
        layer.disc(28, 28, 24, (0.98, 0.78, 0.05), smooth=0.6)
        layer.disc(28, 28, 20.5, (0.05, 0.05, 0.06), smooth=0.8)
        layer.rect(15, 24, 33, 36, WHITE, radius=2)                                # a card and the waves off it
        layer.rect(15, 27, 33, 29.5, (0.05, 0.05, 0.06))
        for r in (6, 10, 14):
            arc = [(33 + math.cos(t) * r, 22 - math.sin(t) * r) for t in np.linspace(-0.2, 1.3, 8)]
            layer.line(arc, (0.98, 0.78, 0.05), width=1.8)
        return layer.finish(grain=0.02, blotch=0.02)

    def fare_lamps():
        layer = Layer(112, 56, (0.03, 0.03, 0.035), 0.0, 0.8)
        green, red = (0.15, 0.90, 0.30), (0.95, 0.12, 0.08)
        layer.poly([(12, 22), (30, 22), (30, 12), (48, 28), (30, 44), (30, 34), (12, 34)], green)
        layer.line([(68, 14), (96, 42)], red, width=6.5)
        layer.line([(96, 14), (68, 42)], red, width=6.5)
        layer.line([(56, 4), (56, 52)], (0.25, 0.25, 0.27), width=1.5)
        return layer.finish(grain=0.02, blotch=0.02)

    def pump_face():
        layer = Layer(128, 208, (0.90, 0.90, 0.88), 0.0, 0.5)
        layer.rect(6, 6, 122, 118, (0.05, 0.05, 0.06), smooth=0.8, radius=5)
        lcd, digit = (0.62, 0.70, 0.58), (0.06, 0.09, 0.06)
        for y, height, number, label in ((13, 30, "42.50", 28), (50, 26, "31.07", 22), (83, 24, "1.369", 30)):
            layer.rect(13, y, 115, y + height, lcd, smooth=0.85, radius=2)
            layer.text(number, (44, y + 3, 111, y + height - 3), digit, weight=0.07, spacing=0.06)
            layer.line([(17, y + 7), (17 + label, y + 7)], digit, width=1.6)
            layer.line([(17, y + 12), (11 + label, y + 12)], digit, width=1.2)
        layer.text("$", (17, 22, 30, 40), digit, weight=0.07)
        layer.text("L", (17, 58, 30, 73), digit, weight=0.07)
        for k, (tint, name) in enumerate((((0.10, 0.50, 0.22), "REGULAR"), ((0.10, 0.25, 0.62), "PREMIUM"), ((0.12, 0.12, 0.13), "DIESEL"))):
            x = 8 + k * 39
            layer.rect(x, 126, x + 34, 160, (0.55, 0.56, 0.58), metal=1.0, smooth=0.6, radius=3)
            layer.rect(x + 2, 128, x + 32, 158, tint, radius=3)
            layer.text(name, (x + 4, 146, x + 30, 155), WHITE, weight=0.05)
            layer.text(("87", "91", "D")[k], (x + 7, 130, x + 27, 145), WHITE, weight=0.08)
        layer.rect(8, 168, 60, 200, (0.12, 0.12, 0.13), radius=3)                  # the card slot and keypad
        for r in range(3):
            for c in range(4):
                layer.rect(13 + c * 11, 172 + r * 9, 21 + c * 11, 178 + r * 9, (0.62, 0.63, 0.65), radius=1)
        layer.rect(68, 170, 120, 180, (0.12, 0.12, 0.13), radius=2)
        layer.rect(74, 174, 114, 176, (0.02, 0.02, 0.02))
        for k in range(3):
            layer.line([(68, 187 + 5 * k), (118 - 9 * k, 187 + 5 * k)], (0.35, 0.36, 0.38), width=1.4)
        return layer.finish(grime=0.12, grain=0.03, blotch=0.04, scratches=8)

    def pump_top():
        red = (0.78, 0.08, 0.06)
        layer = Layer(128, 48, WHITE, 0.0, 0.6)
        layer.rect(0, 0, 128, 7, red)
        layer.rect(0, 41, 128, 48, red)
        layer.disc(22, 24, 12, red)
        layer.poly([(22, 14), (28, 25), (22, 33), (16, 25)], (0.99, 0.84, 0.18))    # a drop
        layer.text("CITY FUEL", (40, 13, 122, 35), red, weight=0.08)
        return layer.finish(grain=0.02, blotch=0.03)

    def flammable():
        layer = Layer(40, 40, (0.72, 0.07, 0.05), 0.0, 0.45)
        layer.poly([(20, 2), (38, 20), (20, 38), (2, 20)], (0.98, 0.80, 0.06))
        layer.poly([(20, 6), (34, 20), (20, 34), (6, 20)], (0.99, 0.86, 0.12))
        layer.poly([(20, 10), (25, 19), (23, 18), (26, 25), (20, 31), (14, 25), (17, 19), (18, 22)], BLACK)       # a flame
        layer.poly([(20, 21), (23, 26), (20, 29), (17, 26)], (0.99, 0.86, 0.12))
        return layer.finish(grain=0.03, blotch=0.03)

    def wash_sign():
        blue = (0.04, 0.24, 0.66)
        layer = Layer(320, 64, blue, 0.0, 0.55)
        layer.rect(3, 3, 317, 61, WHITE, radius=7)
        layer.rect(7, 7, 313, 57, blue, radius=5)
        layer.text("CAR WASH", (62, 12, 258, 52), WHITE, weight=0.08, spacing=0.05)
        for cx, side in ((32, 1), (288, -1)):                                      # suds either side
            for dx, dy, r in ((-10, 6, 11), (6, -4, 9), (9, 11, 6.5), (-4, -11, 5)):
                layer.disc(cx + side * dx, 32 + dy, r, (0.62, 0.84, 0.97))
                layer.disc(cx + side * dx, 32 + dy, r - 1.6, blue)
                layer.disc(cx + side * dx - r * 0.35, 32 + dy - r * 0.35, r * 0.22, WHITE)
        return layer.finish(grime=0.1, grain=0.03, blotch=0.04, scratches=10)

    def wash_brush():
        blue = (0.10, 0.36, 0.80)
        layer = Layer(48, 64, tuple(c * 0.45 for c in blue), 0.0, 0.2)
        rng = np.random.default_rng(89)
        for k in range(40):                                                        # strips of cloth hanging round the drum
            x = rng.uniform(0, 48)
            tone = rng.uniform(0.55, 1.25)
            layer.line([(x, 0), (x + rng.uniform(-2, 2), 64)], tuple(min(1.0, c * tone) for c in blue), width=rng.uniform(1.2, 2.6))
        return layer.finish(grain=0.05, blotch=0.08)

    def employer(k):
        def paint():
            line1, line2, ground, letters = EMPLOYERS[k]
            layer = Layer(192, 96, ground, 0.0, 0.72)
            layer.rect(5, 5, 187, 91, letters, radius=5)
            layer.rect(7.5, 7.5, 184.5, 88.5, ground, radius=4)
            layer.text(line1, (16, 14, 176, 50), letters, weight=0.075, spacing=0.05)
            layer.line([(40, 54.5), (152, 54.5)], letters, width=1.4)
            layer.text(line2, (16, 60, 176, 82), letters, weight=0.06, spacing=0.06)
            return layer.finish(grime=0.08, grain=0.02, blotch=0.04, scratches=5)
        return paint

    def plywood():
        ply = (0.72, 0.56, 0.36)
        layer = Layer(112, 144, ply, 0.0, 0.12)
        rng = np.random.default_rng(91)
        knots = [(rng.uniform(10, 102), rng.uniform(10, 134), rng.uniform(4, 9)) for _ in range(5)]
        for k in range(40):                                                        # rotary-cut grain: wide wavy bands that part round the knots
            x0 = k * 3.0 - 2
            tone = 0.90 + 0.10 * math.sin(k * 1.7) + rng.uniform(-0.03, 0.03)
            path = []
            for y in range(0, 149, 6):
                x = x0 + 5.0 * math.sin(y * 0.021 + k * 0.13) + 2.0 * math.sin(y * 0.07 + k)
                for kx, ky, kr in knots:
                    d = math.hypot(x - kx, y - ky)
                    if d < kr * 3:
                        x += (x - kx) / max(d, 0.5) * kr * 1.2 * (1 - d / (kr * 3))
                path.append((x, y))
            layer.line(path, tuple(c * tone for c in ply), width=1.6)
        for kx, ky, kr in knots:
            layer.disc(kx, ky, kr * 0.55, (0.36, 0.22, 0.10))
            layer.disc(kx, ky, kr * 0.30, (0.22, 0.12, 0.05))
        spray = (0.05, 0.05, 0.06)                                                 # somebody's tag, and the glazier's note
        tag = [(22, 92), (34, 66), (44, 90), (54, 62), (66, 88), (80, 64), (90, 84)]
        layer.line(tag, spray, width=3.2)
        layer.line([(20, 100), (94, 96)], spray, width=2.4)
        layer.rect(60, 16, 100, 44, (0.94, 0.93, 0.88))
        layer.text("SORRY", (63, 19, 97, 29), (0.75, 0.10, 0.08), weight=0.07)
        layer.text("CLOSED", (63, 31, 97, 41), INK, weight=0.07)
        return layer.finish(grime=0.35, grain=0.05, blotch=0.08)

    def closed_notice():
        red = (0.74, 0.07, 0.05)
        layer = Layer(112, 160, (0.96, 0.95, 0.91), 0.0, 0.25)
        layer.rect(0, 0, 112, 160, red)
        layer.rect(6, 6, 106, 154, (0.96, 0.95, 0.91))
        layer.poly([(46, 12), (66, 12), (66, 24), (56, 33), (46, 24)], (0.10, 0.16, 0.36))           # the city's crest
        layer.poly([(49, 15), (63, 15), (63, 23), (56, 29.5), (49, 23)], (0.86, 0.70, 0.25))
        layer.line([(22, 22), (42, 22)], INK, width=1.2)
        layer.line([(70, 22), (90, 22)], INK, width=1.2)
        layer.text("CLOSED", (11, 38, 101, 66), red, weight=0.09)
        layer.rect(11, 68, 101, 84, red)
        layer.text("BY ORDER", (15, 70, 97, 82), WHITE, weight=0.08)
        for k, y in enumerate(range(92, 124, 5)):
            layer.line([(13, y), (99 - (k * 13) % 30, y)], (0.30, 0.30, 0.33), width=1.4)
        sign = [(14 + t, 138 - 5 * math.sin(t * 0.55) * (1 - t / 60)) for t in np.arange(0, 44, 1.5)]
        layer.line(sign, (0.08, 0.10, 0.36), width=1.3)
        layer.line([(13, 144), (62, 144)], (0.30, 0.30, 0.33), width=1)
        layer.disc(86, 136, 11, red)                                               # the seal
        layer.disc(86, 136, 8.5, (0.96, 0.95, 0.91))
        layer.disc(86, 136, 6.5, red)
        return layer.finish(grain=0.025, blotch=0.03)

    decals = {
        "everyday_pan_food": pan_food,
        "everyday_plate_meal": plate_meal,
        "everyday_checkpoint": checkpoint_flag,
        "everyday_beer_label": beer_label,
        "everyday_popcorn_label": popcorn_label,
        "everyday_popcorn_top": popcorn_top,
        "everyday_takeaway": takeaway_side,
        "everyday_noodles": noodles,
        "everyday_box_side": box_side,
        "everyday_ledger_front": ledger_front,
        "everyday_rack_sign": rack_sign,
        "everyday_bag_logo": bag_logo,
        "everyday_fare_reader": fare_reader,
        "everyday_fare_lamps": fare_lamps,
        "everyday_pump_face": pump_face,
        "everyday_pump_top": pump_top,
        "everyday_flammable": flammable,
        "everyday_wash_sign": wash_sign,
        "everyday_wash_brush": wash_brush,
        "everyday_plywood": plywood,
        "everyday_closed_notice": closed_notice,
    }
    for k in range(6):
        decals["everyday_employer_%d" % (k + 1)] = employer(k)
    return swatches, decals
