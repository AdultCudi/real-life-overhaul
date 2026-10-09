"""The painted faces of the event props (props_events.py): fires and street events. See atlas.py, "the later rounds".

Lettering that has to be read from across a street (the placard) is 0.12 m high on the prop and 60 px here.
"""
import math

import numpy as np


def register(a):
    WHITE, BLACK, INK = a.WHITE, a.BLACK, a.INK
    CARD = (0.95, 0.94, 0.90)
    MARKER = (0.07, 0.07, 0.09)
    RED = (0.78, 0.09, 0.07)
    LIME = (0.80, 0.90, 0.16)
    SILVER = (0.84, 0.85, 0.84)
    YELLOW = a.SWATCHES["paint_yellow"][0]

    def scrawl(layer, y, x0, x1, colour, width=5.0, wobble=2.0):
        """A marker line drawn by hand under a word."""
        points = [(x, y + wobble * math.sin(x * 0.045 + y)) for x in np.linspace(x0, x1, 16)]
        layer.line(points, colour, width=width)

    def placard(lines, accent, width, height):
        def paint():
            layer = a.Layer(width, height, CARD, 0.0, 0.2)
            layer.rect(0, 0, width, height, (0.80, 0.79, 0.74))                       # the edge of the card
            layer.rect(3, 3, width - 3, height - 3, CARD)
            band = (height - 28) / len(lines)
            tall = min(band - 8, height * 60 / 210)                                   # 0.12 m of a 0.42 m board
            for k, (text, shift) in enumerate(lines):
                y = 14 + band * k + (band - tall) / 2
                margin = width * 0.07
                layer.text(text, (margin + shift, y, width - margin + shift, y + tall), RED if k == accent else MARKER, weight=0.085, smooth=0.35)
            y = 14 + band * (accent + 1) - 3
            scrawl(layer, y, width * 0.20, width * 0.80, RED, width=4.0)
            for x in (width * 0.5 - 9, width * 0.5 + 9):                              # the staples that hold it to the lath
                for sy in (9, height - 9):
                    layer.rect(x - 1, sy - 4, x + 1, sy + 4, (0.55, 0.56, 0.58), metal=1.0, smooth=0.6)
            return layer.finish(grime=0.12, grain=0.03, blotch=0.05, edge=0.12)
        return paint

    def banner():
        blue, gold = (0.10, 0.27, 0.60), (0.93, 0.78, 0.16)
        layer = a.Layer(280, 192, blue, 0.0, 0.12)
        layer.rect(0, 0, 280, 14, gold)
        layer.rect(0, 178, 280, 192, gold)
        layer.rect(0, 20, 280, 24, gold)
        layer.rect(0, 168, 280, 172, gold)
        cx, cy = 140, 96                                                               # the same from both sides: the cloth shows it mirrored on the back
        layer.disc(cx, cy, 58, gold)
        layer.disc(cx, cy, 51, blue)
        star = [(cx + math.sin(math.radians(36 * k)) * (43 if k % 2 == 0 else 17), cy - math.cos(math.radians(36 * k)) * (43 if k % 2 == 0 else 17)) for k in range(10)]
        layer.poly(star, (0.95, 0.94, 0.90))
        for k in range(7):                                                             # seven beads under it
            t = math.radians(210 + 20 * k)
            layer.disc(cx + math.cos(t) * 78, cy - math.sin(t) * 66, 6.0, gold)
        return layer.finish(grime=0.08, grain=0.05, blotch=0.07)

    def bib():
        layer = a.Layer(176, 136, (0.96, 0.96, 0.94), 0.0, 0.25)
        layer.rect(0, 0, 176, 30, (0.06, 0.32, 0.62))
        layer.text("CITY FUN RUN", (14, 6, 162, 25), WHITE, weight=0.06, spacing=0.04)
        layer.text("247", (26, 38, 150, 102), MARKER, weight=0.055)
        layer.rect(0, 110, 176, 136, (0.90, 0.36, 0.06))
        layer.text("RIVERSIDE 10K", (16, 115, 160, 131), WHITE, weight=0.06, spacing=0.03)
        for x, y in ((9, 9), (167, 9), (9, 127), (167, 127)):                          # the safety pins
            layer.disc(x, y, 3.2, (0.70, 0.71, 0.73), metal=1.0, smooth=0.7)
            layer.disc(x, y, 1.4, (0.25, 0.25, 0.27))
        return layer.finish(grain=0.03, blotch=0.04, edge=0.10)

    def helmet_shield():
        leather = (0.05, 0.045, 0.045)
        layer = a.Layer(64, 80, leather, 0.0, 0.5)
        layer.rect(4, 4, 60, 76, (0.80, 0.62, 0.20), metal=1.0, smooth=0.7, radius=8)
        layer.rect(7, 7, 57, 73, leather, radius=6)
        layer.rect(10, 10, 54, 26, (0.70, 0.08, 0.06), radius=3)
        layer.text("ENGINE", (13, 13, 51, 23), WHITE, weight=0.05)
        layer.text("7", (16, 28, 48, 62), WHITE, weight=0.10)
        layer.text("R.F.D.", (16, 63, 48, 71), (0.80, 0.62, 0.20), weight=0.05)
        return layer.finish(grain=0.04, blotch=0.05)

    def helmet_side():
        """Runs up the crown: the reflective band round its foot, then the paint."""
        layer = a.Layer(96, 128, YELLOW, 0.0, 0.55)
        layer.rect(0, 92, 96, 122, LIME, smooth=0.6)
        for x in range(-24, 110, 24):                                                  # the band's slanted silver bars
            layer.poly([(x, 122), (x + 10, 122), (x + 22, 92), (x + 12, 92)], SILVER, smooth=0.7)
        layer.line([(0, 92), (96, 92)], (0.45, 0.40, 0.08), width=1.2)
        layer.rect(0, 122, 96, 128, (0.06, 0.06, 0.07))
        return layer.finish(grime=0.25, grain=0.035, blotch=0.06, scratches=10)

    def cylinder():
        """Runs up the air cylinder: valve end at the bottom, its label and bands."""
        layer = a.Layer(64, 192, YELLOW, 0.0, 0.5)
        layer.rect(0, 150, 64, 192, (0.16, 0.16, 0.17))                                # the black shoulder at the valve
        layer.rect(0, 100, 64, 124, LIME, smooth=0.6)
        layer.rect(0, 108, 64, 116, SILVER, smooth=0.7)
        layer.rect(0, 36, 22, 80, WHITE)                                               # the label, on the outer side
        layer.rect(0, 36, 22, 46, (0.70, 0.08, 0.06))
        for k in range(5):
            layer.line([(2, 52 + 5 * k), (19 - 4 * (k % 2), 52 + 5 * k)], INK, width=1.2)
        layer.rect(0, 12, 64, 18, (0.16, 0.16, 0.17))
        return layer.finish(grime=0.2, grain=0.03, blotch=0.06, scratches=14)

    def reflective():
        layer = a.Layer(128, 32, LIME, 0.0, 0.55)
        layer.rect(0, 10, 128, 22, SILVER, smooth=0.7)
        for x in range(0, 128, 8):
            layer.line([(x, 10), (x + 6, 22)], (0.70, 0.71, 0.70), width=1.0)
        return layer.finish(grain=0.04, blotch=0.04)

    def monitor():
        layer = a.Layer(96, 64, (0.03, 0.03, 0.035), 0.0, 0.6)
        layer.rect(5, 5, 91, 59, (0.36, 0.48, 0.62), smooth=0.85)                       # the shot: sky, street, two figures
        layer.rect(5, 36, 91, 59, (0.30, 0.29, 0.27), smooth=0.85)
        layer.rect(5, 22, 30, 36, (0.42, 0.36, 0.30), smooth=0.85)
        layer.rect(66, 16, 91, 36, (0.34, 0.33, 0.36), smooth=0.85)
        for x, tint in ((40, (0.62, 0.20, 0.16)), (55, (0.16, 0.24, 0.42))):
            layer.rect(x - 4, 26, x + 4, 50, tint, smooth=0.85, radius=2)
            layer.disc(x, 22, 3.4, (0.80, 0.62, 0.50), smooth=0.85)
        layer.disc(12, 11, 2.6, (0.95, 0.10, 0.08), smooth=0.85)
        layer.line([(18, 11), (34, 11)], WHITE, width=1.5)
        for x in (5, 91):                                                              # frame lines
            layer.line([(x, 5), (x, 59)], (0.75, 0.75, 0.75), width=0.8)
        return layer.finish(grain=0.02, blotch=0.02)

    def lamp_face():
        """The lens of the film light: a bright warm disc with the rings of a fresnel, in its dark rim."""
        layer = a.Layer(96, 96, (0.03, 0.03, 0.035), 0.0, 0.4)
        layer.disc(48, 48, 46, (0.30, 0.30, 0.32), metal=1.0, smooth=0.6)
        for k, radius in enumerate(range(43, 3, -5)):
            layer.disc(48, 48, radius, (1.0, 0.94, 0.76) if k % 2 == 0 else (0.98, 0.86, 0.62), metal=0.0, smooth=0.9)
        layer.disc(48, 48, 9, (1.0, 0.99, 0.93), smooth=0.9)
        return layer.finish(grain=0.01, blotch=0.02)

    def director():
        canvas = (0.045, 0.045, 0.05)
        layer = a.Layer(224, 80, canvas, 0.0, 0.12)
        for y in (5, 75):                                                              # the hems, stitched
            layer.line([(0, y), (224, y)], (0.16, 0.16, 0.17), width=1.2)
        layer.text("DIRECTOR", (26, 22, 198, 58), (0.93, 0.92, 0.88), weight=0.06, spacing=0.06)
        return layer.finish(grain=0.07, blotch=0.06)

    def rack_label():
        layer = a.Layer(160, 64, (0.93, 0.50, 0.05), 0.0, 0.3)
        layer.rect(3, 3, 157, 61, BLACK)
        layer.rect(6, 6, 154, 58, (0.93, 0.50, 0.05))
        layer.text("DANGER", (40, 9, 150, 33), BLACK, weight=0.09)
        layer.text("FIREWORKS  1.3G", (40, 37, 150, 53), BLACK, weight=0.06)
        layer.poly([(22, 10), (37, 34), (7, 34)], BLACK)                               # the explosive's diamond, roughly
        layer.poly([(22, 54), (37, 34), (7, 34)], BLACK)
        layer.disc(22, 30, 5, (0.93, 0.50, 0.05))
        for k in range(6):
            t = math.radians(60 * k)
            layer.line([(22, 30), (22 + math.cos(t) * 9, 30 + math.sin(t) * 9)], (0.93, 0.50, 0.05), width=1.4)
        return layer.finish(grime=0.15, grain=0.04, blotch=0.05, scratches=6)

    swatches = {
        "events_lens": ((0.05, 0.06, 0.15), 0.0, 0.95, 0.0),          # coated glass, seen dark
    }
    decals = {
        "events_placard_front": placard((("FAIR", -6), ("RENT", 5), ("NOW", -2)), 2, 280, 210),
        "events_placard_back": placard((("HOMES", 3), ("FOR", -8), ("ALL", 4)), 0, 240, 180),
        "events_banner": banner,
        "events_bib": bib,
        "events_helmet_shield": helmet_shield,
        "events_helmet_side": helmet_side,
        "events_cylinder": cylinder,
        "events_reflective": reflective,
        "events_monitor": monitor,
        "events_lamp_face": lamp_face,
        "events_director": director,
        "events_rack_label": rack_label,
    }
    return swatches, decals
