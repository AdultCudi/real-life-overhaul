"""The finishes and painted faces of the garage props (props_garage.py): bicycles, motorbikes, helmets, wheel faces,
car parts and the bike-share dock. See atlas.py, "the later rounds".
"""
import math


def register(a):
    WHITE, BLACK, INK = a.WHITE, a.BLACK, a.INK
    swatches = {
        # name: (colour, metallic, smoothness, grime towards the bottom)
        "garage_teal": ((0.07, 0.30, 0.44), 0.15, 0.78, 0.35),          # the town bicycle's frame
        "garage_red": ((0.62, 0.035, 0.04), 0.20, 0.86, 0.25),          # the road bicycle, the sport bike
        "garage_navy": ((0.035, 0.06, 0.17), 0.35, 0.88, 0.2),          # the roadster's tank
        "garage_white": ((0.90, 0.90, 0.88), 0.10, 0.86, 0.35),         # fairing and helmet white
        "garage_visor": ((0.025, 0.035, 0.06), 0.0, 0.96, 0.0),         # smoked glass: visors, the screen
        "garage_bay": ((0.10, 0.36, 0.20), 0.0, 0.5, 0.5),              # the bike-share green
    }

    def plate():
        layer = a.Layer(112, 80, (0.93, 0.80, 0.10), 0.0, 0.5)
        layer.rect(0, 0, 112, 80, BLACK, radius=6)
        layer.rect(3, 3, 109, 77, (0.93, 0.80, 0.10), radius=4)
        layer.text("RL0", (12, 8, 100, 38), BLACK, weight=0.07, spacing=0.06)
        layer.text("47K", (12, 42, 100, 72), BLACK, weight=0.07, spacing=0.06)
        return layer.finish(grime=0.25, grain=0.03, blotch=0.05)

    def pictogram(layer, cx, cy, size, colour, width=None):
        """A bicycle seen from its side, `size` wide."""
        r = size * 0.21
        width = width or size * 0.05
        rear, front = (cx - size * 0.29, cy + size * 0.12), (cx + size * 0.29, cy + size * 0.12)
        crank, seat, head = (cx - size * 0.04, cy + size * 0.12), (cx - size * 0.13, cy - size * 0.16), (cx + size * 0.18, cy - size * 0.16)
        for wheel in (rear, front):
            layer.disc(*wheel, r + width / 2, colour)
        for wheel in (rear, front):
            layer.disc(*wheel, r - width / 2, layer.ground)
        for start, end in ((rear, crank), (crank, seat), (seat, rear), (seat, head), (crank, head), (head, front), (head, (head[0] - size * 0.03, head[1] - size * 0.10)),
                           (seat, (seat[0] - size * 0.02, seat[1] - size * 0.07))):
            layer.line([start, end], colour, width=width)
        layer.line([(seat[0] - size * 0.09, seat[1] - size * 0.07), (seat[0] + size * 0.05, seat[1] - size * 0.07)], colour, width=width * 1.3)
        layer.line([(head[0] - size * 0.09, head[1] - size * 0.10), (head[0] + size * 0.03, head[1] - size * 0.10)], colour, width=width)

    def dock_panel():
        green = swatches["garage_bay"][0]
        layer = a.Layer(112, 144, WHITE, 0.0, 0.45)
        layer.ground = green
        layer.rect(0, 0, 112, 144, (0.55, 0.56, 0.58), radius=8)
        layer.rect(3, 3, 109, 141, green, radius=6)
        pictogram(layer, 56, 46, 84, WHITE)
        layer.rect(14, 86, 98, 110, WHITE, radius=4)
        layer.text("07", (22, 88, 90, 108), green, weight=0.07, spacing=0.05)
        for k, y in enumerate((120, 128)):
            layer.line([(16, y), (96 - 22 * k, y)], WHITE, width=2.0)
        return layer.finish(grime=0.2, grain=0.03, blotch=0.04, scratches=6)

    def kiosk_screen():
        dark, glow = (0.03, 0.05, 0.07), (0.55, 0.85, 0.80)
        layer = a.Layer(176, 144, (0.05, 0.05, 0.06), 0.0, 0.9)
        layer.rect(6, 6, 170, 138, dark, radius=4)
        layer.ground = dark
        layer.rect(6, 6, 170, 30, (0.08, 0.34, 0.20))
        layer.text("HIRE A BIKE", (16, 10, 160, 27), WHITE, weight=0.06, spacing=0.04)
        pictogram(layer, 52, 70, 64, glow)
        layer.text("15", (98, 44, 160, 84), glow, weight=0.07)
        layer.text("A DAY", (100, 84, 160, 98), glow, weight=0.05, spacing=0.05)
        layer.rect(16, 108, 84, 130, (0.10, 0.50, 0.26), radius=4)
        layer.text("START", (24, 112, 76, 126), WHITE, weight=0.06)
        layer.rect(92, 108, 160, 130, (0.20, 0.22, 0.25), radius=4)
        layer.text("RETURN", (98, 113, 154, 125), (0.80, 0.82, 0.84), weight=0.05)
        return layer.finish(grain=0.015, blotch=0.03)

    decals = {
        "garage_plate": plate,
        "garage_dock_panel": dock_panel,
        "garage_kiosk_screen": kiosk_screen,
    }
    return swatches, decals
