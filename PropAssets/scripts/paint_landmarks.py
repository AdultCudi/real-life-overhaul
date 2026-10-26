"""The finishes and the one painted face of the landmarks module (props_landmarks.py), the second bundle's: Crest Field's
stands, roof, floodlights and pitch. See atlas.py, "the later rounds", and README, "The second bundle". Nothing here
carries a name: the stadium's name is the mod's text, never painted.

    region              pixels       on the prop (m)       what
    stadium_pitch       256 x 236    52 x 48               the field: grass mown in stripes, the white lines of a pitch
                                                           46 x 30, the centre circle and the boxes, a running track round it
"""
import math

GRASS_A = (0.17, 0.42, 0.16)
GRASS_B = (0.21, 0.49, 0.19)
TRACK = (0.55, 0.22, 0.14)        # an all-weather track's red
LINE = (0.93, 0.94, 0.92)
FIELD = (52.0, 48.0)              # m: the prop's own size, the stands stretch it to the field they leave
SIZE = (256, 236)                 # px: about 4.9 px a metre each way


def register(a):
    swatches = {
        "stadium_concrete": ((0.64, 0.64, 0.62), 0.0, 0.22, 0.7),   # the rake, the walls, the stair towers, dirtier at the foot
        "stadium_seat": ((0.07, 0.40, 0.44), 0.0, 0.50, 0.1),        # Uptown's green blue moulded seats
        "stadium_seat_light": ((0.80, 0.82, 0.80), 0.0, 0.50, 0.1),  # the pale seats that band the rows
        "stadium_roof": ((0.80, 0.81, 0.82), 0.4, 0.45, 0.2),        # the canopy's sheet, its underside pale to light the stand
        "stadium_steel": ((0.30, 0.31, 0.33), 0.9, 0.45, 0.4),       # columns, the masts, the lamp frames
        "stadium_lamp": ((1.00, 0.97, 0.88), 0.0, 0.85, 0.0),        # a floodlight's lens (the glow material)
    }

    def metres(x, y):
        return x * SIZE[0] / FIELD[0], y * SIZE[1] / FIELD[1]

    def pitch():
        w, h = SIZE
        layer = a.Layer(w, h, TRACK, 0.0, 0.25)
        tx, ty = metres(3.0, 3.0)                      # the track's width round the grass
        layer.rect(tx, ty, w - tx, h - ty, GRASS_A, smooth=0.18)
        stripes = 10
        for k in range(stripes):
            if k % 2:
                x0 = tx + (w - 2 * tx) * k / stripes
                layer.rect(x0, ty, tx + (w - 2 * tx) * (k + 1) / stripes, h - ty, GRASS_B, smooth=0.18)
        # The pitch's lines: 46 x 30 m in the middle, the halfway line, the centre circle and spot, the boxes and goals.
        px, py = metres((FIELD[0] - 46.0) / 2.0, (FIELD[1] - 30.0) / 2.0)
        x0, y0, x1, y1 = px, py, w - px, h - py
        lw = 1.2
        layer.line([(x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0)], LINE, lw)
        mx, my = w / 2.0, h / 2.0
        layer.line([(mx, y0), (mx, y1)], LINE, lw)
        cr = metres(4.6, 0)[0]
        layer.line([(mx + cr * math.cos(t * 0.1), my + cr * math.sin(t * 0.1)) for t in range(64)], LINE, lw)
        layer.disc(mx, my, 1.4, LINE)
        bx, by = metres(8.0, 16.0)
        sx, sy = metres(3.0, 7.0)
        gy = metres(0, 4.0)[1]
        for side in (0, 1):
            edge = x0 if side == 0 else x1
            out = 1 if side == 0 else -1
            layer.line([(edge, my - by / 2), (edge + out * bx, my - by / 2), (edge + out * bx, my + by / 2), (edge, my + by / 2)], LINE, lw)
            layer.line([(edge, my - sy / 2), (edge + out * sx, my - sy / 2), (edge + out * sx, my + sy / 2), (edge, my + sy / 2)], LINE, lw)
            layer.rect(min(edge, edge - out * 4), my - gy / 2, max(edge, edge - out * 4), my + gy / 2, LINE)
            layer.disc(edge + out * metres(6.0, 0)[0], my, 1.2, LINE)
        # The track's lanes, faint, round the grass.
        for k in (1, 2):
            inset = tx * k / 3.0
            layer.line([(inset, inset), (w - inset, inset), (w - inset, h - inset), (inset, h - inset), (inset, inset)], (0.78, 0.55, 0.48), 0.8)
        return layer.finish(grain=0.05, blotch=0.06)

    return swatches, {"stadium_pitch": pitch}
