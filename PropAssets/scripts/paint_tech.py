"""The painted faces of the tech module (props_tech.py): the server room's racks, cooling and battery cabinets and its
raised floor; a headquarters' lobby (the logo wall's slats, the screens); a tech office (the whiteboard, code on a
screen); the data centre's frontage (the security booth's band, the barrier's arm). See atlas.py, "the later rounds".

Nothing here carries a name: the company's name on the logo wall and the cloud's on the sign are lettered by the mod
(Scripts/Tech/Sites/SiteProps.cs) in the game's font. The status lights of the racks are painted dark: the mod lays
its own small lights over them (dc_leds_a, dc_leds_b) and makes those blink, so the spots below are shared with
props_tech.py, which puts the lights exactly on them.

    region              pixels       on the prop (m)       what
    tech_servers        96 x 320     0.52 x 1.72           a rack's door: eleven servers of one to four units
    tech_gpus           96 x 320     0.52 x 1.72           the same spots, accelerator sleds with their heat sinks
    tech_switches       96 x 320     0.52 x 1.72           patch panels, switches, cables, servers under them
    tech_rack_back      64 x 224     0.52 x 1.80           the rear: fans, power strips, cable looms
    tech_crac           192 x 208    1.70 x 1.84           a cooling unit's front: louvres, its panel, the filter
    tech_ups            96 x 224     0.70 x 1.70           a battery cabinet: display, status bar, grille
    tech_tiles          128 x 128    2.40 x 2.40           four by four floor tiles
    tech_vent           64 x 256     0.60 x 2.40           four perforated tiles
    tech_dashboard      192 x 112    screens               charts on a dark screen
    tech_code           160 x 96     screens               a code editor
    tech_board          240 x 160    1.70 x 1.10           a whiteboard with a diagram and notes
    tech_closet         96 x 192     0.52 x 1.00           a small rack's front
    tech_slats          256 x 192    3.40 x 2.60           vertical wooden slats
    tech_security       192 x 48     1.40 x 0.35           SECURITY on the booth's band (capitals 0.21 m)
    tech_boom           256 x 24     3.40 x 0.09           the barrier arm's red and white
"""
import numpy as np

# The servers of a rack's door, top to bottom, as heights in pixels of the 320 (one unit is 14): shared by tech_servers,
# tech_gpus and tech_switches, and by the lights props_tech.py lays over them.
UNITS = (14, 14, 28, 28, 28, 42, 28, 14, 56, 28, 28)
DOOR = (96, 320)
TOP = 6


def led_spots():
    """The status lights of a rack's door: (x, y) in pixels of the door's 96 x 320, and the set it blinks with (0 or 1)."""
    spots = []
    y = TOP
    for k, high in enumerate(UNITS):
        spots.append((9.0, y + 5.0, k % 2))
        spots.append((15.0, y + 5.0, (k + 1) % 2))
        if high >= 28:
            spots.append((9.0, y + 12.0, (k + 1) % 2))
        y += high
    return spots


def register(a):
    BLACK = (0.035, 0.037, 0.042)
    BEZEL = (0.13, 0.135, 0.145)
    STEEL = (0.30, 0.31, 0.33)
    OFF = (0.05, 0.09, 0.06)          # a status light that is not lit
    WHITE = (0.90, 0.91, 0.92)
    rng = np.random.default_rng(23)

    swatches = {
        "tech_black": (BLACK, 0.35, 0.45, 0.25),
        "tech_white": (WHITE, 0.0, 0.45, 0.30),
        "tech_grey": ((0.55, 0.57, 0.60), 0.0, 0.38, 0.35),
        "tech_screen": ((0.02, 0.025, 0.03), 0.0, 0.88, 0.0),
    }

    def units(layer, paint):
        """The door's grid of servers, each painted by paint(layer, x0, y0, x1, y1, unit index), then its lights."""
        y = TOP
        for k, high in enumerate(UNITS):
            layer.rect(4, y + 1, 92, y + high - 1, BEZEL, metal=0.4, smooth=0.5)
            paint(layer, 4, y + 1, 92, y + high - 1, k)
            y += high
        for x, y, _ in led_spots():
            layer.disc(x, y, 1.7, OFF, smooth=0.9)

    def frame(layer):
        layer.rect(0, 0, 96, 320, BLACK)
        layer.rect(1, 1, 3, 319, STEEL, metal=0.8, smooth=0.5)
        layer.rect(93, 1, 95, 319, STEEL, metal=0.8, smooth=0.5)

    def servers():
        layer = a.Layer(*DOOR, BLACK, 0.3, 0.45)
        frame(layer)

        def server(layer, x0, y0, x1, y1, k):
            layer.rect(x0 + 18, y0 + 2, x1 - 3, y1 - 2, (0.08, 0.085, 0.095), smooth=0.4)
            bays = 8 if (y1 - y0) < 20 else 6
            for b in range(bays):
                bx = x0 + 20 + b * (x1 - x0 - 26) / bays
                layer.rect(bx, y0 + 3, bx + (x1 - x0 - 26) / bays - 1.5, y1 - 3, (0.17, 0.175, 0.19), metal=0.5, smooth=0.5)
                layer.rect(bx + 1, y1 - 6, bx + 3, y1 - 4, (0.32, 0.33, 0.35), metal=0.8)
            if k % 3 == 0:
                layer.rect(x1 - 14, y0 + 3, x1 - 4, y0 + 7, (0.55, 0.58, 0.62), metal=0.6)
        units(layer, server)
        return layer.finish(grain=0.03, blotch=0.03)

    def gpus():
        layer = a.Layer(*DOOR, BLACK, 0.3, 0.45)
        frame(layer)
        green = (0.36, 0.62, 0.18)

        def sled(layer, x0, y0, x1, y1, k):
            layer.rect(x0 + 18, y0 + 2, x1 - 3, y1 - 2, (0.06, 0.065, 0.07))
            for fx in np.arange(x0 + 20, x1 - 5, 2.5):
                layer.line([(fx, y0 + 3), (fx, y1 - 3)], (0.24, 0.25, 0.27), width=0.9, metal=0.9, smooth=0.6)
            layer.rect(x0 + 18, y1 - 4, x1 - 3, y1 - 2.5, green, smooth=0.6)
            layer.rect(x1 - 12, y0 + 3, x1 - 5, y0 + 6, (0.70, 0.58, 0.20), metal=1.0, smooth=0.7)
        units(layer, sled)
        return layer.finish(grain=0.03, blotch=0.03)

    def switches():
        layer = a.Layer(*DOOR, BLACK, 0.3, 0.45)
        frame(layer)
        cables = ((0.10, 0.36, 0.78), (0.86, 0.72, 0.10), (0.15, 0.55, 0.30), (0.80, 0.20, 0.18))

        def kit(layer, x0, y0, x1, y1, k):
            if k < 6:          # patch panels and switches: rows of ports, and the cables out of them
                rows = 1 if (y1 - y0) < 20 else 2
                for row in range(rows):
                    py = y0 + 3 + row * (y1 - y0 - 6) / rows
                    for p in range(12):
                        px = x0 + 21 + p * 5.6
                        layer.rect(px, py, px + 4, py + 4, (0.02, 0.02, 0.025))
                        colour = cables[int(rng.integers(0, len(cables)))]
                        layer.line([(px + 2, py + 4), (px + 2 + rng.uniform(-2, 2), y1 - 1)], colour, width=1.2, smooth=0.5)
            else:
                layer.rect(x0 + 18, y0 + 2, x1 - 3, y1 - 2, (0.08, 0.085, 0.095))
                for b in range(6):
                    bx = x0 + 20 + b * 11
                    layer.rect(bx, y0 + 3, bx + 9.5, y1 - 3, (0.17, 0.175, 0.19), metal=0.5)
        units(layer, kit)
        return layer.finish(grain=0.03, blotch=0.03)

    def rack_back():
        layer = a.Layer(64, 224, BLACK, 0.3, 0.45)
        layer.rect(2, 2, 62, 222, (0.06, 0.06, 0.07))
        for row in range(9):
            y = 10 + row * 23
            for x in (18, 44):
                layer.disc(x, y + 8, 8, (0.10, 0.10, 0.11))
                layer.disc(x, y + 8, 6.5, (0.02, 0.02, 0.025))
                layer.line([(x - 6, y + 8), (x + 6, y + 8)], (0.12, 0.12, 0.13), width=1.2)
                layer.line([(x, y + 2), (x, y + 14)], (0.12, 0.12, 0.13), width=1.2)
        layer.rect(3, 4, 7, 220, (0.10, 0.10, 0.11), metal=0.5)                         # the power strip
        for y in range(10, 220, 12):
            layer.rect(4, y, 6, y + 4, (0.75, 0.10, 0.08))
        for k, colour in enumerate(((0.10, 0.36, 0.78), (0.86, 0.72, 0.10), (0.15, 0.55, 0.30))):
            x = 54 + k * 2.6
            layer.line([(x, 6), (x - 1.5, 110), (x + 0.5, 218)], colour, width=2.0, smooth=0.5)
        return layer.finish(grain=0.04, blotch=0.04)

    def crac():
        layer = a.Layer(192, 208, WHITE, 0.0, 0.45)
        layer.rect(6, 6, 186, 112, (0.80, 0.81, 0.82))
        for y in range(10, 110, 5):                                                     # louvres
            layer.rect(9, y, 183, y + 2.4, (0.62, 0.63, 0.65), metal=0.3)
        layer.rect(136, 120, 184, 152, (0.20, 0.22, 0.25), smooth=0.6)                  # the panel
        layer.rect(140, 124, 180, 140, (0.06, 0.20, 0.32), smooth=0.9)
        layer.text("22.0", (142, 125, 178, 139), (0.55, 0.85, 1.0), weight=0.03)
        for k in range(3):
            layer.disc(146 + k * 9, 146, 2.2, (0.30, 0.70, 0.30) if k == 0 else (0.25, 0.27, 0.30))
        layer.rect(6, 160, 186, 202, (0.74, 0.75, 0.77))                                # the filter
        for x in range(10, 184, 4):
            layer.line([(x, 162), (x, 200)], (0.58, 0.59, 0.61), width=1.2)
        layer.rect(6, 118, 128, 154, (0.86, 0.87, 0.88))
        layer.rect(10, 128, 40, 132, (0.30, 0.31, 0.33), metal=0.6)                     # handles
        layer.rect(94, 128, 124, 132, (0.30, 0.31, 0.33), metal=0.6)
        return layer.finish(grime=0.25, grain=0.03, blotch=0.04)

    def ups():
        layer = a.Layer(96, 224, (0.50, 0.52, 0.55), 0.0, 0.4)
        layer.rect(14, 12, 82, 44, (0.10, 0.11, 0.12), smooth=0.6)
        layer.rect(18, 16, 78, 34, (0.10, 0.30, 0.12), smooth=0.9)                      # the display
        for k in range(6):
            layer.rect(22 + k * 9, 30 - (k + 2) * 1.6, 28 + k * 9, 31, (0.45, 0.90, 0.40))
        layer.disc(24, 39, 2.0, (0.30, 0.75, 0.30))
        layer.disc(32, 39, 2.0, (0.25, 0.27, 0.30))
        layer.text("UPS", (54, 35, 78, 43), (0.85, 0.86, 0.88), weight=0.04)
        for y in range(56, 212, 6):                                                     # the grille
            layer.rect(12, y, 84, y + 3, (0.36, 0.38, 0.40))
        layer.rect(42, 120, 54, 150, (0.22, 0.23, 0.25), metal=0.6, radius=2)          # the handle
        return layer.finish(grime=0.3, grain=0.03, blotch=0.04)

    def tiles():                # (a flat grey with its seams: 128 px for 2.40 m is enough, 19 mm a pixel)
        layer = a.Layer(128, 128, (0.58, 0.59, 0.60), 0.0, 0.32)
        for k in range(16):
            x, y = (k % 4) * 32, (k // 4) * 32
            shade = 0.56 + rng.uniform(-0.02, 0.02)
            layer.rect(x + 0.5, y + 0.5, x + 31.5, y + 31.5, (shade, shade + 0.01, shade + 0.02))
            for _ in range(15):
                sx, sy = x + rng.uniform(2, 30), y + rng.uniform(2, 30)
                layer.disc(sx, sy, 0.4, (shade - 0.08, shade - 0.07, shade - 0.06))
        for k in range(5):
            layer.rect(k * 32 - 0.6, 0, k * 32 + 0.6, 128, (0.30, 0.31, 0.32), metal=0.6)
            layer.rect(0, k * 32 - 0.6, 128, k * 32 + 0.6, (0.30, 0.31, 0.32), metal=0.6)
        return layer.finish(grain=0.025, blotch=0.03)

    def vent():
        layer = a.Layer(64, 256, (0.56, 0.57, 0.58), 0.3, 0.4)
        for t in range(4):
            y0 = t * 64
            layer.rect(1, y0 + 1, 63, y0 + 63, (0.52, 0.53, 0.55), metal=0.5)
            for row in range(9):
                for col in range(9):
                    layer.disc(8 + col * 6, y0 + 8 + row * 6, 1.6, (0.06, 0.06, 0.07))
            layer.rect(0, y0 - 1, 64, y0 + 1, (0.30, 0.31, 0.32), metal=0.6)
        return layer.finish(grain=0.025, blotch=0.03)

    def dashboard():
        layer = a.Layer(192, 112, (0.03, 0.04, 0.06), 0.0, 0.85)
        layer.rect(4, 4, 188, 14, (0.08, 0.11, 0.16))
        layer.text("UPTIME 99.98", (8, 5, 90, 13), (0.60, 0.85, 1.0), weight=0.02)
        for panel, (x0, y0, x1, y1) in enumerate(((6, 20, 94, 64), (100, 20, 186, 64), (6, 70, 94, 106), (100, 70, 186, 106))):
            layer.rect(x0, y0, x1, y1, (0.06, 0.08, 0.12))
            colour = ((0.30, 0.85, 0.55), (0.35, 0.65, 1.0), (1.0, 0.70, 0.25), (0.85, 0.40, 0.75))[panel]
            if panel % 2 == 0:
                points = [(x0 + 2 + k * (x1 - x0 - 4) / 14, y1 - 4 - (y1 - y0 - 10) * (0.5 + 0.45 * np.sin(k * 0.9 + panel))) for k in range(15)]
                layer.line(points, colour, width=1.4)
            else:
                for k in range(9):
                    high = (y1 - y0 - 8) * rng.uniform(0.25, 0.95)
                    layer.rect(x0 + 4 + k * 9, y1 - 3 - high, x0 + 10 + k * 9, y1 - 3, colour)
        return layer.finish(grain=0.01, blotch=0.01)

    def code():
        layer = a.Layer(160, 96, (0.06, 0.07, 0.09), 0.0, 0.85)
        layer.rect(0, 0, 22, 96, (0.09, 0.10, 0.13))
        colours = ((0.55, 0.75, 1.0), (0.95, 0.65, 0.40), (0.60, 0.85, 0.55), (0.80, 0.60, 0.90), (0.75, 0.77, 0.80))
        y = 5
        indent = 0
        while y < 92:
            x = 27 + indent * 7
            for _ in range(int(rng.integers(1, 4))):
                w = rng.uniform(8, 30)
                layer.rect(x, y, min(156, x + w), y + 2.6, colours[int(rng.integers(0, len(colours)))])
                x += w + 4
            indent = max(0, min(4, indent + int(rng.integers(-1, 2))))
            y += 6
        return layer.finish(grain=0.01, blotch=0.01)

    def board():
        layer = a.Layer(240, 160, (0.95, 0.96, 0.96), 0.0, 0.75)
        blue, red, black = (0.12, 0.25, 0.70), (0.75, 0.15, 0.12), (0.10, 0.10, 0.12)
        for (x0, y0, x1, y1), word in (((20, 24, 80, 52), "API"), ((110, 24, 170, 52), "DB"), ((20, 92, 80, 120), "WEB"), ((110, 92, 186, 120), "CACHE")):
            layer.line([(x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0)], blue, width=1.6)
            layer.text(word, (x0 + 6, y0 + 6, x1 - 6, y1 - 6), blue, weight=0.04)
        for (x0, y0), (x1, y1) in (((80, 38), (110, 38)), ((50, 52), (50, 92)), ((140, 52), (148, 92)), ((80, 106), (110, 106))):
            layer.line([(x0, y0), (x1, y1)], black, width=1.4)
        layer.line([(196, 30), (226, 30)], red, width=1.4)
        layer.line([(196, 40), (220, 40)], red, width=1.4)
        layer.line([(196, 50), (228, 50)], red, width=1.4)
        for k, colour in enumerate(((0.98, 0.88, 0.30), (0.98, 0.70, 0.75), (0.65, 0.88, 0.98))):
            x, y = 192 + (k % 2) * 22, 74 + k * 24
            layer.rect(x, y, x + 20, y + 20, colour, smooth=0.3)
            layer.line([(x + 3, y + 7), (x + 16, y + 7)], (0.3, 0.3, 0.3), width=1.0)
            layer.line([(x + 3, y + 12), (x + 13, y + 12)], (0.3, 0.3, 0.3), width=1.0)
        return layer.finish(grain=0.01, blotch=0.03)

    def closet():
        layer = a.Layer(96, 192, BLACK, 0.3, 0.45)
        layer.rect(1, 1, 3, 191, STEEL, metal=0.8)
        layer.rect(93, 1, 95, 191, STEEL, metal=0.8)
        y = 8
        for k, high in enumerate((14, 28, 14, 42, 28, 28, 28)):
            layer.rect(4, y + 1, 92, y + high - 1, BEZEL, metal=0.4)
            layer.rect(22, y + 3, 89, y + high - 3, (0.10, 0.105, 0.115))
            layer.disc(9, y + 5, 1.7, (0.25, 0.70, 0.30) if k % 2 == 0 else (0.20, 0.50, 0.85), smooth=0.9)
            if k == 0:
                for p in range(12):
                    layer.rect(24 + p * 5.4, y + 4, 28 + p * 5.4, y + 9, (0.02, 0.02, 0.025))
            y += high
        return layer.finish(grain=0.03, blotch=0.03)

    def slats():
        layer = a.Layer(256, 192, (0.08, 0.06, 0.05), 0.0, 0.3)
        x = 1.0
        while x < 255:
            shade = rng.uniform(-0.04, 0.04)
            layer.rect(x, 0, x + 6.5, 192, (0.56 + shade, 0.40 + shade, 0.25 + shade * 0.5), smooth=0.35)
            for _ in range(5):
                gy = rng.uniform(0, 192)
                layer.line([(x + 1, gy), (x + 5.5, gy + rng.uniform(-6, 6))], (0.48 + shade, 0.33 + shade, 0.20), width=0.8)
            x += 8.0
        return layer.finish(grain=0.05, blotch=0.06)

    def security():
        layer = a.Layer(192, 48, (0.06, 0.12, 0.30), 0.0, 0.5)
        layer.text("SECURITY", (8, 6, 184, 42), (0.96, 0.97, 0.98), weight=0.06, spacing=0.06)
        return layer.finish(grain=0.02, blotch=0.03)

    def boom():
        layer = a.Layer(256, 24, (0.95, 0.95, 0.94), 0.0, 0.5)
        for k in range(-1, 12):
            x = k * 24
            layer.poly([(x, 24), (x + 12, 24), (x + 24, 0), (x + 12, 0)], (0.80, 0.08, 0.06))
        return layer.finish(grain=0.02, blotch=0.03)

    decals = {
        "tech_servers": servers,
        "tech_gpus": gpus,
        "tech_switches": switches,
        "tech_rack_back": rack_back,
        "tech_crac": crac,
        "tech_ups": ups,
        "tech_tiles": tiles,
        "tech_vent": vent,
        "tech_dashboard": dashboard,
        "tech_code": code,
        "tech_board": board,
        "tech_closet": closet,
        "tech_slats": slats,
        "tech_security": security,
        "tech_boom": boom,
    }
    return swatches, decals
