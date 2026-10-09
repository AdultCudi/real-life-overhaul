"""What the service vehicles of props_service.py are painted with: the shuttle bus body (`minibus`), the fire engine
body (`fire_engine`) and the light bar (`fire_light_bar`). One function, register(a), as atlas.py asks of a later
round: coachwork finishes as swatches, and the faces that have to be crisp (glass with a reflection in it, wheels,
lamps, grilles, the pump panel, roller shutters, lettering, chevrons) as decals.

Lettering sizes: a decal is drawn for the size it has on the vehicle (given beside each painter), capital letters
at least 44 px high where they are to be read from the pavement.
"""
import math

import numpy as np
from PIL import Image

COACH_WHITE = (0.90, 0.90, 0.87)
COACH_BLUE = (0.05, 0.20, 0.52)
COACH_SKY = (0.25, 0.62, 0.86)
ENGINE_RED = (0.72, 0.045, 0.035)
GOLD = (0.93, 0.74, 0.26)
STEEL = (0.74, 0.75, 0.76)


def register(a):
    Layer, WHITE, BLACK, SS = a.Layer, a.WHITE, a.BLACK, a.SS

    swatches = {
        "svc_coach_white": (COACH_WHITE, 0.15, 0.72, 0.55),
        "svc_coach_blue": (COACH_BLUE, 0.25, 0.72, 0.60),
        "svc_coach_sky": (COACH_SKY, 0.20, 0.72, 0.20),
        "svc_engine_red": (ENGINE_RED, 0.25, 0.78, 0.50),
        "svc_tint": ((0.035, 0.045, 0.055), 0.0, 0.92, 0.0),
        "svc_lens_clear": ((0.93, 0.95, 0.97), 0.0, 0.90, 0.0),
    }

    def shade(layer, colour):
        """Paints the whole layer from colour(u, v) -> (r, g, b) arrays; u to the right, v up, both 0..1."""
        w, h = layer.base.size
        u, v = np.meshgrid(np.linspace(0.0, 1.0, w), np.linspace(1.0, 0.0, h))
        rgb = np.clip(np.stack(colour(u, v), axis=-1), 0.0, 1.0)
        layer.base = Image.fromarray((rgb * 255).astype(np.uint8), "RGB")
        from PIL import ImageDraw
        layer._draw[0] = ImageDraw.Draw(layer.base)

    def glass(width, height, deep=(0.022, 0.030, 0.040), sky=(0.24, 0.33, 0.42), band=0.0):
        """Opaque tinted glass: dark at the foot, the sky in its upper half, two soft streaks of reflection."""
        def paint():
            layer = Layer(width, height, deep, 0.0, 0.93)

            def colour(u, v):
                lift = np.clip((v - 0.30) / 0.70, 0.0, 1.0) ** 1.6
                streak = 0.10 * np.exp(-((u - 0.30 - 0.35 * v) / 0.07) ** 2) + 0.06 * np.exp(-((u - 0.52 - 0.35 * v) / 0.03) ** 2)
                top = np.where(v > 1.0 - band, 0.35, 1.0) if band else 1.0
                return [(deep[c] + (sky[c] - deep[c]) * lift + streak * (0.6 + 0.4 * v)) * top for c in range(3)]
            shade(layer, colour)
            return layer.finish(grain=0.012, blotch=0.03)
        return paint

    def wheel(rim, dish, holes, nuts, cap, tyre_edge=0.60):
        """A road wheel seen square on, 128 px across the whole tyre: the rim's edge is at `tyre_edge` of the radius."""
        def paint():
            layer = Layer(128, 128, (0.05, 0.05, 0.055), 0.0, 0.22)
            c, r = 64.0, 64.0
            layer.disc(c, c, r * 0.97, (0.075, 0.075, 0.08), smooth=0.25)
            layer.disc(c, c, r * 0.90, (0.045, 0.045, 0.05), smooth=0.2)
            for k in range(40):                           # the lettering moulded into the side wall, as ticks
                t = 2 * math.pi * k / 40
                if k % 10 < 6:
                    layer.line([(c + math.cos(t) * r * 0.74, c + math.sin(t) * r * 0.74), (c + math.cos(t) * r * 0.80, c + math.sin(t) * r * 0.80)], (0.10, 0.10, 0.105), width=1.2)
            layer.disc(c, c, r * (tyre_edge + 0.03), (0.02, 0.02, 0.02))
            layer.disc(c, c, r * tyre_edge, tuple(min(1.0, v * 1.12) for v in rim), metal=1.0, smooth=0.75)
            layer.disc(c, c, r * (tyre_edge - 0.045), tuple(v * 0.55 for v in rim), metal=1.0, smooth=0.5)
            layer.disc(c, c, r * (tyre_edge - 0.07), dish, metal=1.0, smooth=0.62)
            for k in range(holes):
                t = 2 * math.pi * (k + 0.5) / holes
                layer.disc(c + math.cos(t) * r * 0.40, c + math.sin(t) * r * 0.40, r * 0.062, (0.015, 0.015, 0.018), metal=0.0, smooth=0.2)
                layer.disc(c + math.cos(t) * r * 0.40 - 0.8, c + math.sin(t) * r * 0.40 - 0.8, r * 0.045, (0.035, 0.035, 0.04), metal=0.0, smooth=0.2)
            layer.disc(c, c, r * 0.27, tuple(v * 0.62 for v in dish), metal=1.0, smooth=0.5)
            layer.disc(c, c, r * 0.25, dish, metal=1.0, smooth=0.6)
            for k in range(nuts):
                t = 2 * math.pi * k / nuts
                layer.disc(c + math.cos(t) * r * 0.20, c + math.sin(t) * r * 0.20, 2.4, (0.20, 0.20, 0.21), metal=1.0, smooth=0.5)
                layer.disc(c + math.cos(t) * r * 0.20 - 0.4, c + math.sin(t) * r * 0.20 - 0.4, 1.6, (0.80, 0.80, 0.82), metal=1.0, smooth=0.8)
            layer.disc(c, c, r * 0.135, tuple(v * 0.6 for v in cap), metal=1.0, smooth=0.6)
            layer.disc(c, c, r * 0.12, cap, metal=1.0, smooth=0.85)
            layer.disc(c - 1.5, c - 1.5, r * 0.05, tuple(min(1.0, v * 1.15) for v in cap), metal=1.0, smooth=0.9)
            return layer.finish(grime=0.25, grain=0.03, blotch=0.05)
        return paint

    def lettering(text, width, height, ground, ink, weight=0.07, spacing=0.05, margin=0.12, rule=None, metal=None, finish=(0.15, 0.72)):
        """Letters on the coachwork's own colour: the ground must be the swatch's, so that the sticker's edge is not seen."""
        def paint():
            layer = Layer(width, height, ground, *finish)        # the swatch's own metallic and smoothness
            m = height * margin
            layer.text(text, (m, m, width - m, height - m - (6 if rule else 0)), ink, weight=weight, spacing=spacing, metal=metal, smooth=0.6)
            if rule:
                layer.rect(m, height - m - 3, width - m, height - m, rule, smooth=0.6)
            return layer.finish(grain=0.008, blotch=0.0)
        return paint

    def paint_bus_grille():           # 0.80 x 0.22 m, on the nose
        layer = Layer(256, 72, COACH_WHITE, 0.15, 0.72)
        layer.rect(4, 4, 252, 68, (0.60, 0.61, 0.63), metal=1.0, smooth=0.8, radius=14)
        layer.rect(8, 8, 248, 64, (0.02, 0.02, 0.025), metal=0.0, smooth=0.3, radius=11)
        for y in (18, 30, 42, 54):
            layer.rect(12, y - 3, 244, y + 2, (0.13, 0.13, 0.14), smooth=0.45, radius=2)
            layer.line([(14, y - 3), (242, y - 3)], (0.30, 0.30, 0.32), width=1)
        layer.disc(128, 36, 19, (0.02, 0.02, 0.025))
        layer.disc(128, 36, 16, (0.78, 0.79, 0.81), metal=1.0, smooth=0.85)
        layer.disc(128, 36, 12.5, COACH_BLUE, metal=0.2, smooth=0.8)
        layer.poly([(128, 26), (137, 42), (119, 42)], (0.90, 0.91, 0.92), metal=1.0, smooth=0.85)
        return layer.finish(grime=0.2, grain=0.03, blotch=0.04)

    def paint_headlamp():             # 0.30 x 0.17 m; the indicator is at the left of the decal
        layer = Layer(112, 64, (0.03, 0.03, 0.035), 0.0, 0.5)
        layer.rect(2, 2, 110, 62, (0.55, 0.56, 0.58), metal=1.0, smooth=0.8, radius=12)
        layer.rect(5, 5, 107, 59, (0.10, 0.10, 0.11), metal=0.4, smooth=0.9, radius=10)
        layer.rect(8, 8, 34, 56, (0.98, 0.52, 0.04), metal=0.0, smooth=0.92, radius=7)          # indicator
        for y in range(12, 54, 5):
            layer.line([(10, y), (32, y)], (0.80, 0.36, 0.02), width=1)
        for x, radius in ((58, 19), (90, 14)):
            layer.disc(x, 32, radius + 2, (0.45, 0.46, 0.48), metal=1.0, smooth=0.85)
            layer.disc(x, 32, radius, (0.86, 0.90, 0.94), metal=0.3, smooth=0.95)
            layer.disc(x, 32, radius * 0.55, (0.98, 0.99, 1.0), metal=0.0, smooth=0.95)
            layer.disc(x - radius * 0.3, 32 - radius * 0.3, radius * 0.22, (1.0, 1.0, 1.0), smooth=0.95)
        return layer.finish(grain=0.015, blotch=0.03)

    def paint_tail_lamp():            # 0.15 x 0.42 m, upright: stop, turn, reverse, in a black surround
        layer = Layer(48, 136, (0.03, 0.03, 0.035), 0.0, 0.5)
        for y0, y1, tint in ((5, 52, (0.80, 0.03, 0.03)), (56, 90, (0.98, 0.50, 0.04)), (94, 131, (0.80, 0.03, 0.03))):
            layer.rect(5, y0, 43, y1, tuple(v * 0.5 for v in tint), smooth=0.9, radius=6)
            layer.rect(7, y0 + 2, 41, y1 - 2, tint, smooth=0.94, radius=5)
            for y in range(int(y0) + 6, int(y1) - 4, 4):
                layer.line([(9, y), (39, y)], tuple(v * 0.78 for v in tint), width=1)
            layer.disc(24, (y0 + y1) / 2, 6, tuple(min(1.0, v * 1.25 + 0.08) for v in tint), smooth=0.95)
        layer.rect(14, 104, 34, 122, (0.90, 0.91, 0.92), smooth=0.94, radius=3)                   # the reversing lamp
        return layer.finish(grain=0.015, blotch=0.03)

    def paint_plate(text):
        def paint():                  # 0.36 x 0.17 m
            layer = Layer(96, 48, (0.93, 0.92, 0.84), 0.0, 0.5)
            layer.rect(0, 0, 96, 48, (0.05, 0.10, 0.30), radius=5)
            layer.rect(2.5, 2.5, 93.5, 45.5, (0.93, 0.92, 0.84), radius=4)
            layer.text(text, (8, 11, 88, 40), (0.05, 0.10, 0.30), weight=0.06, spacing=0.03)
            return layer.finish(grime=0.25, grain=0.03, blotch=0.04)
        return paint

    def paint_shutter():              # a locker's roller shutter: 96 px across whatever its width, 1.46 m high
        layer = Layer(96, 384, (0.70, 0.72, 0.74), 1.0, 0.55)
        pitch = 384.0 / 41
        for k in range(41):
            y = k * pitch
            layer.rect(0, y, 96, y + pitch * 0.45, (0.80, 0.82, 0.84), metal=1.0, smooth=0.62)
            layer.rect(0, y + pitch * 0.45, 96, y + pitch * 0.80, (0.62, 0.64, 0.66), metal=1.0, smooth=0.5)
            layer.rect(0, y + pitch * 0.80, 96, y + pitch, (0.22, 0.23, 0.25), metal=1.0, smooth=0.3)
        for x in (0, 90):                                  # the guides either side
            layer.rect(x, 0, x + 6, 384, (0.30, 0.31, 0.33), metal=1.0, smooth=0.5)
            layer.line([(x + 3, 0), (x + 3, 384)], (0.52, 0.53, 0.55), width=1)
        layer.rect(0, 0, 96, 9, (0.26, 0.27, 0.29), metal=1.0, smooth=0.5)
        layer.rect(6, 352, 90, 384, (0.56, 0.58, 0.60), metal=1.0, smooth=0.6)                    # the lift bar
        layer.rect(6, 352, 90, 355, (0.86, 0.87, 0.89), metal=1.0, smooth=0.7)
        layer.rect(22, 362, 74, 373, (0.10, 0.10, 0.11), metal=0.0, smooth=0.4, radius=5)
        layer.rect(25, 364, 71, 368, (0.30, 0.30, 0.32), metal=0.0, smooth=0.5, radius=2)
        layer.disc(82, 368, 3.2, (0.85, 0.70, 0.25), metal=1.0, smooth=0.8)
        return layer.finish(grime=0.30, grain=0.02, blotch=0.04, scratches=30)

    def gauge(layer, x, y, radius, needle, face=(0.96, 0.96, 0.93), zone=None):
        layer.disc(x, y, radius + 3.0, (0.20, 0.20, 0.22), metal=1.0, smooth=0.5)
        layer.disc(x, y, radius + 2.0, (0.86, 0.87, 0.89), metal=1.0, smooth=0.9)
        layer.disc(x, y, radius, face, metal=0.0, smooth=0.9)
        if zone:
            for k in range(12):
                t = math.radians(zone[0] + (zone[1] - zone[0]) * k / 11)
                layer.disc(x + math.cos(t) * radius * 0.80, y + math.sin(t) * radius * 0.80, radius * 0.09, zone[2], metal=0.0)
        for k in range(13):
            t = math.radians(135 + 270 * k / 12)
            inner = 0.68 if k % 3 == 0 else 0.80
            layer.line([(x + math.cos(t) * radius * inner, y + math.sin(t) * radius * inner), (x + math.cos(t) * radius * 0.93, y + math.sin(t) * radius * 0.93)],
                       (0.05, 0.05, 0.06), width=max(1.0, radius * (0.08 if k % 3 == 0 else 0.045)), metal=0.0)
        t = math.radians(needle)
        layer.line([(x - math.cos(t) * radius * 0.18, y - math.sin(t) * radius * 0.18), (x + math.cos(t) * radius * 0.82, y + math.sin(t) * radius * 0.82)],
                   (0.85, 0.08, 0.05), width=max(1.2, radius * 0.09), metal=0.0)
        layer.disc(x, y, radius * 0.13, (0.05, 0.05, 0.06), metal=0.0)

    def paint_pump_panel():           # 1.30 x 1.48 m: 352 x 400 px
        layer = Layer(352, 400, STEEL, 1.0, 0.62)
        for y in range(0, 400, 2):                         # brushed
            tone = 0.97 + 0.05 * math.sin(y * 12.9898) * math.sin(y * 0.37)
            layer.line([(0, y), (352, y)], tuple(v * tone for v in STEEL), width=1)
        dark, black = (0.25, 0.26, 0.28), (0.04, 0.04, 0.045)
        layer.rect(0, 0, 352, 400, dark, radius=0)
        layer.rect(4, 4, 348, 396, STEEL, radius=6)
        for x, y in ((12, 12), (340, 12), (12, 388), (340, 388), (176, 10), (176, 390), (10, 200), (342, 200)):
            layer.disc(x, y, 3.2, (0.40, 0.41, 0.43), smooth=0.5)
            layer.disc(x - 0.5, y - 0.5, 2.0, (0.86, 0.87, 0.88), smooth=0.8)
        # The lamp strip and the name plate.
        layer.rect(22, 14, 330, 28, black, metal=0.0, smooth=0.5, radius=5)
        layer.rect(26, 17, 326, 25, (0.95, 0.94, 0.86), metal=0.0, smooth=0.9, radius=3)
        layer.rect(96, 36, 256, 60, black, metal=0.0, smooth=0.5, radius=3)
        layer.text("PUMP  1500", (104, 39, 248, 57), (0.93, 0.80, 0.30), weight=0.05, metal=0.0)
        # Two master gauges, and a row of line gauges each over its valve.
        gauge(layer, 112, 108, 36, 205, zone=(300, 400, (0.80, 0.10, 0.08)))
        gauge(layer, 240, 108, 36, 285, zone=(135, 215, (0.10, 0.55, 0.20)))
        layer.rect(160, 150, 192, 160, black, metal=0.0, radius=2)
        layer.rect(28, 76, 60, 140, black, metal=0.0, smooth=0.5, radius=4)                          # switches
        for k, tint in enumerate(((0.15, 0.80, 0.25), (0.95, 0.70, 0.08), (0.90, 0.12, 0.08), (0.15, 0.80, 0.25))):
            layer.disc(44, 86 + 14.5 * k, 3.8, tint, metal=0.0, smooth=0.9)
            layer.disc(43.2, 85.2 + 14.5 * k, 1.4, (1.0, 1.0, 0.95), metal=0.0)
        layer.rect(292, 76, 326, 140, black, metal=0.0, smooth=0.5, radius=4)                         # the governor
        layer.rect(296, 80, 322, 100, (0.30, 0.55, 0.36), metal=0.0, smooth=0.9, radius=2)
        layer.text("85", (299, 82, 319, 98), (0.06, 0.14, 0.06), weight=0.07, metal=0.0)
        for k in range(4):
            layer.rect(297 + (k % 2) * 13.5, 106 + (k // 2) * 15, 308 + (k % 2) * 13.5, 117 + (k // 2) * 15, (0.30, 0.30, 0.33), metal=0.0, radius=2)
        tints = ((0.85, 0.10, 0.08), (0.95, 0.72, 0.06), (0.10, 0.35, 0.75), (0.10, 0.55, 0.20), (0.92, 0.45, 0.05), (0.92, 0.92, 0.90))
        for k, tint in enumerate(tints):
            x = 38 + 55.2 * k
            gauge(layer, x, 190, 15, 150 + 37 * ((k * 5) % 7))
            layer.rect(x - 18, 212, x + 18, 221, tint, metal=0.0, smooth=0.6, radius=2)                # the colour-coded label
            layer.line([(x - 12, 216.5), (x + 12, 216.5)], (0.03, 0.03, 0.03) if sum(tint) > 1.5 else (0.95, 0.95, 0.95), width=1.6, metal=0.0)
            layer.rect(x - 3.5, 228, x + 3.5, 262, (0.50, 0.51, 0.53), metal=1.0, smooth=0.8, radius=2)   # the valve's rod
            layer.rect(x - 5, 228, x + 5, 234, (0.30, 0.31, 0.33), metal=1.0, smooth=0.5, radius=2)
            layer.rect(x - 19, 256, x + 19, 271, tuple(v * 0.45 for v in tint), metal=0.0, smooth=0.5, radius=7)
            layer.rect(x - 18, 255, x + 18, 268, tint, metal=0.0, smooth=0.6, radius=6)                # its T handle
            layer.line([(x - 12, 258.5), (x + 12, 258.5)], tuple(min(1.0, v * 1.3 + 0.12) for v in tint), width=1.5, metal=0.0)
        # The intake (large, capped black), two discharges with red caps, and the throttle.
        for x, y, radius, cap in ((70, 334, 44, (0.07, 0.07, 0.08)), (176, 326, 30, (0.75, 0.09, 0.07)), (258, 326, 30, (0.75, 0.09, 0.07))):
            layer.disc(x + 2, y + 3, radius + 5, (0.30, 0.31, 0.33), smooth=0.4)
            layer.disc(x, y, radius + 4, (0.90, 0.91, 0.92), metal=1.0, smooth=0.9)
            layer.disc(x, y, radius, (0.50, 0.51, 0.53), metal=1.0, smooth=0.7)
            layer.disc(x, y, radius - 5, tuple(v * 0.6 for v in cap), metal=0.0, smooth=0.5)
            layer.disc(x, y, radius - 7, cap, metal=0.0, smooth=0.6)
            for k in range(3 if radius < 40 else 4):                                               # the lugs
                t = math.radians(30 + 360.0 * k / (3 if radius < 40 else 4))
                layer.disc(x + math.cos(t) * (radius - 1), y + math.sin(t) * (radius - 1), radius * 0.16, (0.86, 0.87, 0.88), metal=1.0, smooth=0.9)
            layer.disc(x - radius * 0.25, y - radius * 0.25, radius * 0.22, tuple(min(1.0, v * 1.5 + 0.08) for v in cap), metal=0.0, smooth=0.7)
        layer.disc(318, 310, 17, (0.10, 0.10, 0.11), metal=0.0, smooth=0.5)
        layer.disc(318, 310, 13, (0.80, 0.09, 0.07), metal=0.0, smooth=0.7)
        layer.disc(315, 307, 4, (0.98, 0.45, 0.40), metal=0.0)
        layer.rect(296, 344, 340, 380, (0.96, 0.80, 0.08), metal=0.0, smooth=0.5, radius=2)           # a warning plate
        layer.poly([(318, 348), (329, 364), (307, 364)], black, metal=0.0)
        for y in (368, 373):
            layer.line([(301, y), (335, y)], black, width=1.5, metal=0.0)
        layer.rect(128, 366, 224, 384, black, metal=0.0, smooth=0.5, radius=2)
        layer.text("DISCHARGE", (133, 369, 219, 381), (0.92, 0.92, 0.90), weight=0.05, metal=0.0)
        return layer.finish(grime=0.22, grain=0.02, blotch=0.04, scratches=24)

    def paint_fire_door():            # 0.86 x 0.60 m on a cab door: 320 x 224 px
        layer = Layer(320, 224, ENGINE_RED, 0.25, 0.78)
        layer.text("FIRE DEPT", (10, 8, 310, 76), GOLD, weight=0.075, spacing=0.05, metal=0.6, smooth=0.8)
        layer.rect(14, 80, 306, 84, GOLD, metal=0.6, smooth=0.8)
        # The badge: a cross pattee with a roundel, between "ENGINE" and its number.
        cx, cy, r = 160.0, 152.0, 54.0
        for k in range(4):
            t = math.radians(90 * k)
            ax, ay = math.cos(t), math.sin(t)
            px, py = -ay, ax
            arm = [(cx + ax * r * 0.18 + px * r * 0.16, cy + ay * r * 0.18 + py * r * 0.16), (cx + ax * r + px * r * 0.50, cy + ay * r + py * r * 0.50),
                   (cx + ax * r * 1.08, cy + ay * r * 1.08), (cx + ax * r - px * r * 0.50, cy + ay * r - py * r * 0.50), (cx + ax * r * 0.18 - px * r * 0.16, cy + ay * r * 0.18 - py * r * 0.16)]
            layer.poly(arm, GOLD, metal=0.6, smooth=0.8)
        layer.disc(cx, cy, r * 0.50, GOLD, metal=0.6, smooth=0.8)
        layer.disc(cx, cy, r * 0.43, (0.06, 0.07, 0.10), metal=0.0, smooth=0.7)
        layer.disc(cx, cy, r * 0.36, (0.93, 0.93, 0.90), metal=0.0, smooth=0.7)
        layer.text("7", (cx - 12, cy - 16, cx + 12, cy + 16), ENGINE_RED, weight=0.09, metal=0.0)
        layer.text("ENGINE", (6, 132, 98, 172), (0.95, 0.95, 0.92), weight=0.06, metal=0.0, smooth=0.6)
        layer.text("CO. 7", (224, 132, 314, 172), (0.95, 0.95, 0.92), weight=0.06, metal=0.0, smooth=0.6)
        return layer.finish(grain=0.03, blotch=0.04)

    def paint_chevrons():             # the back of the engine, 2.30 x 1.30 m: yellow and red, pointing up to the middle
        red, yellow = (0.80, 0.06, 0.04), (0.98, 0.84, 0.08)
        layer = Layer(288, 160, yellow, 0.0, 0.6)

        def colour(u, v):
            band = np.floor((np.abs(u - 0.5) * 288.0 + v * 160.0) / 34.0) % 2 == 0
            return [np.where(band, red[c], yellow[c]) for c in range(3)]
        shade(layer, colour)
        layer.rect(142.5, 0, 145.5, 160, (0.18, 0.18, 0.19), smooth=0.4)
        return layer.finish(grime=0.25, grain=0.02, blotch=0.04, scratches=12)

    def paint_engine_grille():        # 1.00 x 0.78 m on the cab front
        layer = Layer(192, 152, ENGINE_RED, 0.25, 0.78)
        layer.rect(3, 3, 189, 149, (0.88, 0.89, 0.90), metal=1.0, smooth=0.9, radius=10)
        layer.rect(10, 10, 182, 142, (0.015, 0.015, 0.02), metal=0.0, smooth=0.3, radius=6)
        for y in range(14, 140, 6):
            layer.line([(12, y), (180, y)], (0.16, 0.16, 0.17), width=1.4)
        for x in range(16, 180, 6):
            layer.line([(x, 12), (x, 140)], (0.11, 0.11, 0.12), width=1.0)
        for x in (52, 96, 140):
            layer.rect(x - 3.5, 8, x + 3.5, 144, (0.86, 0.87, 0.88), metal=1.0, smooth=0.9)
            layer.line([(x - 1.5, 8), (x - 1.5, 144)], (1.0, 1.0, 1.0), width=1, metal=1.0)
        layer.rect(60, 62, 132, 90, (0.86, 0.87, 0.88), metal=1.0, smooth=0.9, radius=4)
        layer.rect(63, 65, 129, 87, ENGINE_RED, metal=0.2, smooth=0.8, radius=3)
        layer.text("ENGINE 7", (67, 68, 125, 84), GOLD, weight=0.06, metal=0.5)
        return layer.finish(grime=0.2, grain=0.02, blotch=0.04)

    def paint_hose_bed():             # flaked hose seen from above: the folds run across the region
        layer = Layer(128, 128, (0.20, 0.17, 0.10), 0.0, 0.2)
        rng = np.random.default_rng(404)
        for k in range(26):
            x = k * 128.0 / 26
            tint = (0.93, 0.78, 0.22) if (k // 9) % 2 == 0 else (0.90, 0.88, 0.80)
            tone = rng.uniform(0.82, 1.05)
            layer.rect(x + 0.4, 0, x + 128.0 / 26 - 0.4, 128, tuple(v * tone * 0.62 for v in tint), smooth=0.2)
            layer.rect(x + 1.0, 0, x + 128.0 / 26 - 1.4, 128, tuple(v * tone for v in tint), smooth=0.25)
            layer.line([(x + 2.0, 0), (x + 2.0, 128)], tuple(min(1.0, v * tone * 1.12) for v in tint), width=1)
        for y in (4, 122):
            layer.rect(0, y - 4, 128, y + 2, (0.30, 0.26, 0.14), smooth=0.2)
        for x in (60, 100):                                # two couplings lying on top
            layer.rect(x - 9, 84, x + 9, 104, (0.42, 0.34, 0.14), metal=1.0, smooth=0.6, radius=4)
            layer.rect(x - 7, 86, x + 7, 100, (0.86, 0.70, 0.30), metal=1.0, smooth=0.8, radius=3)
        return layer.finish(grime=0.15, grain=0.05, blotch=0.07)

    def paint_tread():                # aluminium tread plate: raised diamonds
        base = (0.66, 0.68, 0.70)
        layer = Layer(96, 96, base, 1.0, 0.55)
        for row in range(9):
            for column in range(9):
                x, y = column * 12.0 + (0 if row % 2 else 6.0), row * 12.0 + 3.0
                tilt = 1 if (row + column) % 2 else -1
                layer.line([(x - 4, y - 2.2 * tilt), (x + 4, y + 2.2 * tilt)], (0.36, 0.37, 0.39), width=3.2, metal=1.0, smooth=0.4)
                layer.line([(x - 3.6, y - 2.0 * tilt - 0.7), (x + 3.6, y + 2.0 * tilt - 0.7)], (0.90, 0.91, 0.92), width=1.7, metal=1.0, smooth=0.8)
        return layer.finish(grime=0.35, grain=0.03, blotch=0.06, scratches=20)

    def paint_light_bar():            # the bar seen from the front, 1.30 m: red modules, a clear middle, red modules
        red, clear = (0.86, 0.03, 0.03), (0.93, 0.95, 0.97)
        layer = Layer(256, 40, (0.03, 0.03, 0.035), 0.0, 0.5)
        cells = [(4, 46, red), (50, 92, red), (96, 108, (0.03, 0.03, 0.035)), (112, 144, clear), (148, 160, (0.03, 0.03, 0.035)), (164, 206, red), (210, 252, red)]
        for x0, x1, tint in cells:
            if sum(tint) < 0.3:
                continue
            layer.rect(x0, 3, x1, 37, tuple(v * 0.45 for v in tint), smooth=0.9, radius=4)
            layer.rect(x0 + 1.5, 4.5, x1 - 1.5, 35.5, tint, smooth=0.95, radius=3)
            for x in np.arange(x0 + 5, x1 - 3, 4.0):       # the lens's ribs
                layer.line([(x, 6), (x, 34)], tuple(v * 0.80 for v in tint), width=1)
            count = 3 if x1 - x0 > 36 else 2
            for k in range(count):                         # the lamps behind it
                cx = x0 + (x1 - x0) * (k + 0.5) / count
                layer.disc(cx, 20, 5.2, tuple(min(1.0, v * 1.1 + 0.10) for v in tint), smooth=0.95)
                layer.disc(cx, 20, 2.6, (1.0, 0.97, 0.92), smooth=0.95)
        return layer.finish(grain=0.012, blotch=0.02)

    def paint_vent():                 # louvres on the air-conditioning pod
        layer = Layer(128, 32, (0.06, 0.06, 0.065), 0.0, 0.35)
        for y in range(4, 30, 5):
            layer.rect(4, y, 124, y + 2.4, (0.20, 0.20, 0.21), smooth=0.45, radius=1)
            layer.line([(4, y + 3.2), (124, y + 3.2)], (0.015, 0.015, 0.02), width=1)
        return layer.finish(grain=0.03, blotch=0.04)

    decals = {
        "svc_glass": glass(96, 160),
        "svc_screen": glass(224, 112, deep=(0.030, 0.040, 0.052), sky=(0.27, 0.36, 0.46), band=0.10),
        "svc_wheel": wheel((0.86, 0.87, 0.88), (0.80, 0.81, 0.82), 8, 6, (0.30, 0.31, 0.33)),
        "svc_wheel_alu": wheel((0.90, 0.91, 0.92), (0.72, 0.73, 0.75), 10, 10, (0.90, 0.91, 0.92), tyre_edge=0.62),
        "svc_bus_title": lettering("CITY TRANSIT", 640, 72, COACH_WHITE, COACH_BLUE, weight=0.07, spacing=0.07),
        "svc_bus_shuttle": lettering("SHUTTLE", 256, 56, COACH_WHITE, COACH_SKY, weight=0.07, spacing=0.10, rule=COACH_BLUE),
        "svc_bus_number": lettering("2417", 112, 56, COACH_WHITE, (0.06, 0.07, 0.09), weight=0.06, spacing=0.05),
        "svc_bus_grille": paint_bus_grille,
        "svc_headlamp": paint_headlamp,
        "svc_tail_lamp": paint_tail_lamp,
        "svc_plate_bus": paint_plate("CT 2417"),
        "svc_shutter": paint_shutter,
        "svc_pump_panel": paint_pump_panel,
        "svc_fire_door": paint_fire_door,
        "svc_fire_title": lettering("FIRE & RESCUE", 640, 80, ENGINE_RED, GOLD, weight=0.075, spacing=0.07, metal=0.6, finish=(0.25, 0.78)),
        "svc_chevrons": paint_chevrons,
        "svc_engine_grille": paint_engine_grille,
        "svc_hose_bed": paint_hose_bed,
        "svc_tread": paint_tread,
        "svc_light_bar": paint_light_bar,
        "svc_vent": paint_vent,
    }
    return swatches, decals
