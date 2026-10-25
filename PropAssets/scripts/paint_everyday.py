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

    def flammable():
        layer = Layer(40, 40, (0.72, 0.07, 0.05), 0.0, 0.45)
        layer.poly([(20, 2), (38, 20), (20, 38), (2, 20)], (0.98, 0.80, 0.06))
        layer.poly([(20, 6), (34, 20), (20, 34), (6, 20)], (0.99, 0.86, 0.12))
        layer.poly([(20, 10), (25, 19), (23, 18), (26, 25), (20, 31), (14, 25), (17, 19), (18, 22)], BLACK)       # a flame
        layer.poly([(20, 21), (23, 26), (20, 29), (17, 26)], (0.99, 0.86, 0.12))
        return layer.finish(grain=0.03, blotch=0.03)

    decals = {
        "everyday_pan_food": pan_food,
        "everyday_plate_meal": plate_meal,
        "everyday_checkpoint": checkpoint_flag,
        "everyday_takeaway": takeaway_side,
        "everyday_noodles": noodles,
        "everyday_box_side": box_side,
        "everyday_ledger_front": ledger_front,
        "everyday_rack_sign": rack_sign,
        "everyday_fare_reader": fare_reader,
        "everyday_fare_lamps": fare_lamps,
        "everyday_flammable": flammable,
    }
    return swatches, decals
