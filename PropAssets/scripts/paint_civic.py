"""What props_civic.py needs painted: the precinct's sign and furniture, the bus stop and shelter, the hotel
frontage. Pure Pillow; atlas.py calls register(a) with itself (see "the later rounds" there).

The police blue is the barrier board's (BOARD_BLUE), which sits beside the `paint_blue` swatch without a seam: the
POLICE word is painted on it with no border, so it can be laid on anything painted `paint_blue`.
"""
import math

import numpy as np

IRON = (0.05, 0.055, 0.065)            # the black of cast street furniture and of the hotel sign's board
CREAM = (0.97, 0.96, 0.92)
BRASS = (0.86, 0.64, 0.24)
BUS_BLUE = (0.08, 0.27, 0.58)          # the flag of a bus stop, as Transport/BusStopDressing has it
LAMP_BLUE = (0.10, 0.30, 1.0)

# The big wall shield: the badge's outline (props.py, shield()), drawn finer. Right half, bottom to top.
_SHIELD_HALF = ((0.0, -0.036), (0.012, -0.029), (0.022, -0.018), (0.027, -0.002), (0.025, 0.012), (0.028, 0.026), (0.022, 0.034), (0.012, 0.031), (0.0, 0.036))


def shield_outline(steps=2):
    """The shield's outline, anticlockwise, as (x, y) with its height 1 (y from -0.5 to 0.5) and x centred."""
    pts = list(_SHIELD_HALF) + [(-x, y) for x, y in _SHIELD_HALF[-2:0:-1]]
    count, out = len(pts), []
    for i in range(count):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % count], pts[(i + 2) % count]
        for k in range(steps):
            t = k / steps
            out.append(tuple(0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t ** 3)
                             for a, b, c, d in zip(p0, p1, p2, p3)))
    low, high = min(y for _, y in out), max(y for _, y in out)
    return [(x / (high - low), (y - (high + low) / 2) / (high - low)) for x, y in out]


SHIELD = shield_outline()
SHIELD_WIDTH = 2 * max(x for x, _ in SHIELD)          # of a shield 1 high
SHIELD_PX = (int(round(392 * SHIELD_WIDTH)), 392)


def star(x, y, radius, inner=0.42, points=5):
    return [(x + math.sin(math.pi * k / points) * radius * (1.0 if k % 2 == 0 else inner), y - math.cos(math.pi * k / points) * radius * (1.0 if k % 2 == 0 else inner))
            for k in range(points * 2)]


def register(a):
    Layer, WHITE, BLACK, INK, BLUE = a.Layer, a.WHITE, a.BLACK, a.INK, a.BOARD_BLUE

    def shade(colour, by):
        return tuple(min(1.0, c * by) for c in colour)

    # ------------------------------------------------------------------------------------------ the precinct

    def precinct_board():
        # 1.65 x 0.80 m, the board from 2.05 to 2.85 m up. POLICE centred 2.56 m up; a rule at 2.27 m; below it the
        # strip (1.50 x 0.17 at 2.15 m) the code letters the precinct's name on: plain.
        w, h = 704, 340
        row = lambda up: (2.85 - up) / 0.80 * h
        layer = Layer(w, h, BLUE, 0.0, 0.38)
        layer.rect(4, 4, w - 4, h - 4, WHITE, smooth=0.5, radius=7)
        layer.rect(7.5, 7.5, w - 7.5, h - 7.5, BLUE, smooth=0.38, radius=5)
        layer.text("POLICE", (70, row(2.56) - 80, w - 70, row(2.56) + 80), WHITE, weight=0.075, smooth=0.5, spacing=0.07)
        layer.rect(32, row(2.27) - 5, w - 32, row(2.27) + 5, WHITE, smooth=0.5)
        return layer.finish(grime=0.12, grain=0.04, blotch=0.06, scratches=24, edge=0.12)

    def lamp_glass():
        # Wrapped round the lamp, mirrored: the glass is brightest in front of the bulb.
        w, h = 64, 128
        layer = Layer(w, h, LAMP_BLUE, 0.0, 0.92)
        layer.finish(grain=0.015, blotch=0.02)
        base = np.asarray(layer.base, dtype=np.float64) / 255.0
        yy, xx = np.mgrid[0:base.shape[0], 0:base.shape[1]]
        v = yy / (base.shape[0] - 1.0)
        bulb = np.exp(-((v - 0.52) / 0.30) ** 2)
        base = base * (0.62 + 0.38 * bulb)[..., None] + np.array((0.35, 0.50, 0.75)) * (0.55 * bulb ** 3)[..., None]
        layer.base = a.Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8), "RGB")
        return layer

    def police_word():
        layer = Layer(512, 128, BLUE, 0.0, 0.38)
        layer.text("POLICE", (20, 16, 492, 112), WHITE, weight=0.075, smooth=0.5, spacing=0.07)
        return layer.finish(grain=0.035, blotch=0.04)

    def shield_face():
        w, h = SHIELD_PX
        gold = a.SWATCHES["gold"][0]
        dark, light = shade(gold, 0.50), shade(gold, 1.18)
        enamel = (0.04, 0.13, 0.42)
        layer = Layer(w, h, gold, 1.0, 0.74)
        ring = lambda s: [(w / 2 + x * h * s, h / 2 - y * h * s) for x, y in SHIELD]
        layer.poly(ring(0.925), dark, metal=1.0, smooth=0.6)
        layer.poly(ring(0.905), light, metal=1.0, smooth=0.8)
        layer.poly(ring(0.875), dark, metal=1.0, smooth=0.6)
        layer.poly(ring(0.86), enamel, metal=0.0, smooth=0.86)
        cx, cy = w / 2, h * 0.545
        for k in range(24):          # sunburst behind the seal
            t = math.radians(k * 15.0)
            layer.line([(cx + math.cos(t) * 60, cy + math.sin(t) * 60), (cx + math.cos(t) * (96 if k % 2 == 0 else 84), cy + math.sin(t) * (96 if k % 2 == 0 else 84))],
                       gold, width=4, metal=1.0, smooth=0.74)
        layer.disc(cx, cy, 70, dark, metal=1.0, smooth=0.6)
        layer.disc(cx, cy, 66, light, metal=1.0, smooth=0.8)
        layer.disc(cx, cy, 57, dark, metal=1.0, smooth=0.6)
        layer.disc(cx, cy, 54, BLUE, metal=0.0, smooth=0.86)
        layer.poly(star(cx, cy + 2, 43), light, metal=1.0, smooth=0.8)
        # The banner across the top with POLICE, a smaller scroll below.
        top = h * 0.215
        layer.rect(w * 0.075, top - 31, w * 0.925, top + 31, dark, metal=1.0, smooth=0.6, radius=6)
        layer.rect(w * 0.075 + 4, top - 27, w * 0.925 - 4, top + 27, CREAM, metal=0.0, smooth=0.5, radius=4)
        layer.text("POLICE", (w * 0.075 + 14, top - 20, w * 0.925 - 14, top + 20), enamel, weight=0.075, metal=0.0, smooth=0.5, spacing=0.06)
        low = h * 0.835
        layer.rect(w * 0.27, low - 15, w * 0.73, low + 15, dark, metal=1.0, smooth=0.6, radius=4)
        layer.rect(w * 0.27 + 3, low - 12, w * 0.73 - 3, low + 12, light, metal=1.0, smooth=0.8, radius=3)
        layer.text("PRECINCT", (w * 0.27 + 8, low - 8, w * 0.73 - 8, low + 8), enamel, weight=0.06, metal=0.0, smooth=0.5)
        return layer.finish(grain=0.025, blotch=0.05)

    def notices():
        # 1.10 x 1.50 m of cork under a blue header. Sheets: a WANTED poster with a face, a map, community notices.
        w, h = 288, 392
        cork = (0.63, 0.46, 0.28)
        layer = Layer(w, h, cork, 0.0, 0.08)
        rng = np.random.default_rng(77)
        for _ in range(1000):
            x, y = rng.uniform(0, w), rng.uniform(0, h)
            layer.disc(x, y, rng.uniform(0.6, 1.5), shade(cork, rng.uniform(0.75, 1.15)))
        layer.rect(0, 0, w, 46, BLUE, smooth=0.4)
        layer.rect(4, 4, w - 4, 42, WHITE, radius=4)
        layer.rect(6.5, 6.5, w - 6.5, 39.5, BLUE, radius=3)
        layer.text("POLICE NOTICES", (16, 11, w - 16, 35), WHITE, weight=0.07, spacing=0.05)
        paper, yellow, pink, green, sky = (0.95, 0.94, 0.90), (0.98, 0.90, 0.45), (0.96, 0.70, 0.74), (0.70, 0.88, 0.66), (0.66, 0.82, 0.94)
        # WANTED: the sheet, the heading, a head and shoulders in a frame, lines, a reward strip.
        x, y, sw, sh = 14, 58, 118, 168
        a._notice(layer, rng, x, y, sw, sh, (0.93, 0.89, 0.76), None)
        layer.rect(x + 1, y + 10, x + sw - 1, y + sh - 1, (0.93, 0.89, 0.76))
        layer.text("WANTED", (x + 8, y + 12, x + sw - 8, y + 38), (0.62, 0.07, 0.05), weight=0.08, spacing=0.04)
        layer.rect(x + 22, y + 44, x + sw - 22, y + 120, INK)
        layer.rect(x + 24, y + 46, x + sw - 24, y + 118, (0.70, 0.71, 0.70))
        for k in range(5):          # the height lines of a mug shot
            layer.line([(x + 24, y + 54 + k * 14), (x + sw - 24, y + 54 + k * 14)], (0.58, 0.59, 0.58), width=1)
        mx = x + sw / 2
        layer.poly([(mx - 30, y + 118), (mx - 27, y + 100), (mx - 12, y + 92), (mx + 12, y + 92), (mx + 27, y + 100), (mx + 30, y + 118)], (0.16, 0.16, 0.18))
        layer.rect(mx - 6, y + 82, mx + 6, y + 96, (0.16, 0.16, 0.18))
        layer.disc(mx, y + 72, 14.5, (0.16, 0.16, 0.18))
        for k in range(3):
            layer.line([(x + 12, y + 128 + k * 6), (x + sw - 12 - 9 * k, y + 128 + k * 6)], (0.30, 0.30, 0.33), width=1.6)
        layer.rect(x + 14, y + 147, x + sw - 14, y + 163, (0.62, 0.07, 0.05))
        layer.text("REWARD", (x + 20, y + 149, x + sw - 20, y + 161), (0.95, 0.92, 0.82), weight=0.07)
        # The district map.
        x, y, sw, sh = 148, 60, 126, 104
        a._notice(layer, rng, x, y, sw, sh, (0.90, 0.90, 0.84), None, pin=(0.10, 0.25, 0.70))
        layer.rect(x + 4, y + 12, x + sw - 4, y + sh - 4, (0.84, 0.86, 0.78))
        layer.poly([(x + 4, y + 70), (x + 40, y + 62), (x + 76, y + 80), (x + sw - 4, y + 74), (x + sw - 4, y + 90), (x + 74, y + 94), (x + 38, y + 76), (x + 4, y + 84)], (0.56, 0.72, 0.84))
        layer.rect(x + 78, y + 20, x + 112, y + 46, (0.62, 0.78, 0.56))
        for k in range(1, 6):
            layer.line([(x + 4 + k * 20, y + 12), (x + 4 + k * 20 - 8, y + sh - 4)], (0.97, 0.97, 0.94), width=2.2)
        for k in range(1, 4):
            layer.line([(x + 4, y + 12 + k * 22), (x + sw - 4, y + 6 + k * 22)], (0.97, 0.97, 0.94), width=2.2)
        layer.line([(x + 10, y + 24), (x + 60, y + 50), (x + sw - 10, y + 40)], (0.90, 0.60, 0.10), width=2.6)
        layer.disc(x + 58, y + 49, 5.5, WHITE)
        layer.disc(x + 58, y + 49, 4.0, BLUE)
        a._notice(layer, rng, 152, 176, 122, 78, yellow, "NEIGHBOURHOOD WATCH", pin=(0.10, 0.50, 0.20))
        a._notice(layer, rng, 14, 238, 86, 104, paper, "LOST DOG", strips=True)
        layer.rect(14 + 22, 238 + 38, 14 + 64, 238 + 68, (0.72, 0.73, 0.72))
        layer.disc(14 + 43, 238 + 56, 9, (0.45, 0.32, 0.20))
        layer.disc(14 + 43, 238 + 47, 6.5, (0.45, 0.32, 0.20))
        layer.poly([(14 + 36, 238 + 44), (14 + 34, 238 + 53), (14 + 39, 238 + 49)], (0.30, 0.20, 0.12))
        layer.poly([(14 + 50, 238 + 44), (14 + 52, 238 + 53), (14 + 47, 238 + 49)], (0.30, 0.20, 0.12))
        a._notice(layer, rng, 110, 266, 80, 62, sky, "ROAD CLOSED", pin=(0.95, 0.75, 0.10))
        a._notice(layer, rng, 200, 264, 74, 112, pink, "MISSING", strips=True, pin=(0.10, 0.25, 0.70))
        a._notice(layer, rng, 112, 338, 78, 44, green, "CRIME LINE")
        a._notice(layer, rng, 20, 352, 76, 32, paper, None, pin=(0.10, 0.50, 0.20))
        return layer.finish(grime=0.10, grain=0.04, blotch=0.05)

    def flag():
        # A city's flag, not a country's: blue, a white band through the middle carrying a gold-ringed seal.
        w, h = 240, 144
        field = (0.06, 0.20, 0.52)
        gold = (0.90, 0.68, 0.16)
        layer = Layer(w, h, field, 0.0, 0.12)
        layer.rect(0, 50, w, 94, (0.93, 0.93, 0.90))
        layer.rect(0, 46, w, 50, gold)
        layer.rect(0, 94, w, 98, gold)
        cx, cy = 92, 72
        layer.disc(cx, cy, 39, gold)
        layer.disc(cx, cy, 34, field)
        layer.disc(cx, cy, 30, (0.93, 0.93, 0.90))
        layer.disc(cx, cy, 27, field)
        # A skyline over water inside the seal.
        for bx, bw, bh in ((-20, 9, 16), (-10, 8, 26), (-1, 10, 34), (10, 8, 22), (18, 6, 14)):
            layer.rect(cx + bx, cy + 9 - bh, cx + bx + bw, cy + 9, (0.93, 0.93, 0.90))
        layer.rect(cx - 23, cy + 12, cx + 23, cy + 15, gold)
        layer.rect(cx - 16, cy + 18, cx + 16, cy + 20.5, gold)
        layer.finish(grain=0.05, blotch=0.04)
        base = np.asarray(layer.base, dtype=np.float64) / 255.0          # soft folds running down the cloth
        fw, fh = layer.base.size
        folds = np.sin(np.linspace(0, 5.5 * math.pi, fw) + 0.6)[None, :] * np.linspace(0.5, 1.0, fw)[None, :]
        layer.base = a.Image.fromarray((np.clip(base * (1.0 + 0.10 * folds)[..., None], 0, 1) * 255).astype(np.uint8), "RGB")
        return layer

    def keyboard():
        layer = Layer(128, 48, (0.09, 0.09, 0.10), 0.0, 0.35)
        for r in range(4):
            for k in range(14):
                layer.rect(5 + k * 8.5 + (r % 2) * 2, 5 + r * 8, 11.5 + k * 8.5 + (r % 2) * 2, 11 + r * 8, (0.20, 0.20, 0.22), radius=1)
        layer.rect(36, 37, 92, 43, (0.20, 0.20, 0.22), radius=1)
        for k in (0, 1, 2, 11, 12, 13):
            layer.rect(5 + k * 8.5, 37, 11.5 + k * 8.5, 43, (0.20, 0.20, 0.22), radius=1)
        return layer.finish(grain=0.04, blotch=0.04)

    # ------------------------------------------------------------------------------------------ the bus stop

    def bus(layer, x, y, w, h, colour, ground):
        """A bus from the side, w x h, its top left at (x, y)."""
        layer.rect(x, y, x + w, y + h * 0.80, colour, radius=h * 0.14)
        layer.rect(x, y + h * 0.40, x + w, y + h * 0.80, colour)
        for k in range(4):
            wx = x + w * (0.07 + 0.215 * k)
            layer.rect(wx, y + h * 0.14, wx + w * 0.165, y + h * 0.46, ground, radius=1.5)
        for wx in (x + w * 0.24, x + w * 0.76):
            layer.disc(wx, y + h * 0.80, h * 0.20, ground)
            layer.disc(wx, y + h * 0.80, h * 0.135, colour)

    def bus_flag():
        # 0.50 x 0.62 m, its middle 2.42 m up. The code letters the route numbers at 2.50 m in white (a box 0.46 x
        # 0.40): the blue field is plain down to 2.33 m; the band under it says what this is.
        w, h = 208, 256
        white = (0.95, 0.95, 0.93)
        band = (2.73 - 2.33) / 0.62 * h
        layer = Layer(w, h, BUS_BLUE, 0.0, 0.5)
        layer.rect(0, 0, w, h, white, smooth=0.55, radius=12)
        layer.rect(5, 5, w - 5, h - 5, BUS_BLUE, smooth=0.5, radius=8)
        layer.rect(5, band, w - 5, h - 5, white, smooth=0.55, radius=8)
        layer.rect(5, band, w - 5, band + 12, white, smooth=0.55)
        bus(layer, 16, band + 20, 78, 52, BUS_BLUE, white)
        layer.text("BUS", (104, band + 12, w - 14, band + 44), BUS_BLUE, weight=0.08, smooth=0.5)
        layer.text("STOP", (104, band + 47, w - 14, band + 77), BUS_BLUE, weight=0.08, smooth=0.5)
        return layer.finish(grain=0.03, blotch=0.04, scratches=6)

    def timetable():
        w, h = 96, 136
        layer = Layer(w, h, (0.95, 0.95, 0.92), 0.0, 0.6)
        layer.rect(0, 0, w, 24, BUS_BLUE)
        layer.text("BUS TIMES", (6, 5, w - 6, 19), WHITE, weight=0.07)
        rng = np.random.default_rng(12)
        for r in range(13):
            y = 31 + r * 7.8
            if r % 2 == 0:
                layer.rect(4, y - 3, w - 4, y + 3.6, (0.86, 0.89, 0.93))
            layer.rect(7, y - 1.6, 17, y + 1.8, BUS_BLUE)
            for k in range(6):
                if rng.uniform() < 0.85:
                    layer.line([(24 + k * 11.5, y), (31 + k * 11.5, y)], (0.25, 0.26, 0.30), width=1.7)
        return layer.finish(grain=0.02, blotch=0.03)

    def route_map():
        # The shelter's end panel, 1.04 x 1.70 m: the network above, a poster below.
        w, h = 224, 368
        layer = Layer(w, h, (0.94, 0.94, 0.91), 0.0, 0.6)
        layer.rect(0, 0, w, 40, BUS_BLUE)
        layer.text("BUS ROUTES", (12, 8, w - 12, 32), WHITE, weight=0.07, spacing=0.04)
        layer.rect(8, 48, w - 8, 222, (0.90, 0.91, 0.87))
        layer.poly([(8, 150), (60, 138), (120, 160), (w - 8, 148), (w - 8, 176), (120, 186), (60, 164), (8, 176)], (0.66, 0.80, 0.90))
        layer.rect(140, 62, 196, 100, (0.72, 0.84, 0.66), radius=6)
        lines = (((0.80, 0.12, 0.10), [(18, 70), (70, 70), (110, 110), (110, 200), (60, 212)]),
                 (BUS_BLUE, [(20, 205), (56, 120), (130, 120), (170, 80), (206, 80)]),
                 ((0.10, 0.52, 0.26), [(24, 96), (90, 96), (150, 132), (150, 205), (204, 205)]),
                 ((0.92, 0.58, 0.06), [(204, 112), (176, 140), (176, 176), (88, 176), (40, 140)]))
        for colour, path in lines:
            layer.line(path, colour, width=4.5)
            for px, py in path:
                layer.disc(px, py, 4.6, colour)
                layer.disc(px, py, 2.6, WHITE)
        layer.disc(110, 120, 8.5, INK)          # you are here
        layer.disc(110, 120, 5.5, (0.95, 0.80, 0.10))
        for k, (colour, _) in enumerate(lines):
            layer.rect(12 + k * 52, 229, 26 + k * 52, 240, colour, radius=2)
            layer.line([(30 + k * 52, 234.5), (56 + k * 52, 234.5)], (0.30, 0.30, 0.33), width=2)
        # The poster: a day pass.
        layer.rect(8, 250, w - 8, h - 8, (0.96, 0.78, 0.12))
        layer.rect(8, 250, w - 8, 300, (0.07, 0.10, 0.22))
        layer.text("DAY PASS", (18, 258, w - 18, 292), (0.96, 0.78, 0.12), weight=0.08, spacing=0.03)
        bus(layer, 28, 308, 70, 44, (0.07, 0.10, 0.22), (0.96, 0.78, 0.12))
        layer.text("RIDE", (112, 306, w - 18, 328), (0.07, 0.10, 0.22), weight=0.08)
        layer.text("ALL DAY", (112, 332, w - 18, 352), (0.07, 0.10, 0.22), weight=0.07)
        return layer.finish(grime=0.08, grain=0.025, blotch=0.03)

    def bus_roundel():
        # On the shelter's fascia: 0.80 x 0.20 m.
        w, h = 224, 56
        layer = Layer(w, h, BUS_BLUE, 0.0, 0.5)
        layer.rect(0, 0, w, h, (0.95, 0.95, 0.93), radius=10)
        layer.rect(3, 3, w - 3, h - 3, BUS_BLUE, radius=8)
        layer.disc(34, 28, 21, (0.95, 0.95, 0.93))
        bus(layer, 17, 16, 34, 24, BUS_BLUE, (0.95, 0.95, 0.93))
        layer.text("BUS STOP", (70, 10, w - 12, 46), (0.95, 0.95, 0.93), weight=0.075, spacing=0.03)
        return layer.finish(grain=0.03, blotch=0.04)

    def glass_pane():
        # One pane of the shelter: there is no transparency, so the glass is a pale tint with the sky's streaks in
        # it and the row of dots that real panes carry so that nobody walks into them.
        w, h = 96, 160
        tint = (0.60, 0.72, 0.72)
        layer = Layer(w, h, tint, 0.0, 0.93)
        for x0, wide, by in ((-40, 22, 1.16), (4, 9, 1.10), (52, 30, 1.13)):
            layer.poly([(x0, h), (x0 + wide, h), (x0 + wide + 70, 0), (x0 + 70, 0)], shade(tint, by), smooth=0.95)
        for k in range(9):
            layer.rect(5 + k * 10.2, 84, 10 + k * 10.2, 89, (0.90, 0.93, 0.92), smooth=0.4)
            layer.rect(5 + k * 10.2, 93, 10 + k * 10.2, 98, (0.90, 0.93, 0.92), smooth=0.4)
        layer.finish(grain=0.012, blotch=0.03)
        base = np.asarray(layer.base, dtype=np.float64) / 255.0          # darker towards the ground, lighter to the sky
        rise = np.linspace(1.10, 0.84, base.shape[0])[:, None, None]
        layer.base = a.Image.fromarray((np.clip(base * rise, 0, 1) * 255).astype(np.uint8), "RGB")
        return layer

    # ------------------------------------------------------------------------------------------ the hotel

    def hotel_word():
        # 0.56 x 0.28 m on the sign's board, under the plate the code letters the name on.
        w, h = 224, 112
        layer = Layer(w, h, IRON, 0.0, 0.45)
        layer.text("HOTEL", (14, 8, w - 14, 56), CREAM, weight=0.06, smooth=0.5, spacing=0.14)
        layer.rect(20, 64, w - 20, 66.5, BRASS, metal=1.0, smooth=0.74)
        for k in range(3):
            layer.poly(star(w / 2 + (k - 1) * 44, 89, 15), BRASS, metal=1.0, smooth=0.74)
        return layer.finish(grain=0.03, blotch=0.04)

    def canvas():
        cream, tone = (0.88, 0.84, 0.72), (0.80, 0.75, 0.62)
        layer = Layer(192, 96, cream, 0.0, 0.12)
        for x in range(0, 192, 48):
            layer.rect(x + 2, 0, x + 22, 96, tone)
            layer.rect(x + 30, 0, x + 33, 96, tone)
            layer.rect(x + 39, 0, x + 42, 96, tone)
        return layer.finish(grime=0.12, grain=0.05, blotch=0.06)

    swatches = {
        "civic_iron": (IRON, 0.0, 0.45, 0.5),
        "civic_grey": ((0.40, 0.43, 0.46), 0.0, 0.42, 0.5),
        "civic_lamp_blue": (LAMP_BLUE, 0.0, 0.92, 0.0),
        "civic_leather": ((0.46, 0.27, 0.12), 0.0, 0.5, 0.25),
    }
    decals = {
        "civic_precinct_board": precinct_board,
        "civic_lamp_glass": lamp_glass,
        "civic_police_word": police_word,
        "civic_shield": shield_face,
        "civic_notices": notices,
        "civic_flag": flag,
        "civic_keyboard": keyboard,
        "civic_bus_flag": bus_flag,
        "civic_timetable": timetable,
        "civic_route_map": route_map,
        "civic_bus_roundel": bus_roundel,
        "civic_glass_pane": glass_pane,
        "civic_hotel_word": hotel_word,
        "civic_canvas": canvas,
    }
    return swatches, decals
