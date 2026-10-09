"""New hairstyles as a sculpted base plus strand cards laid along guide lines (run inside Blender: needs mathutils).

The idea, after two failed rounds of loose cards (README, "What did not work"):

  guides   Every style is first a SHEET of guide lines: each line leaves the parting (or the crown), runs over
           the smoothed skull to where the hair leaves the head and, if it is long enough, hangs from there.
           Lines are indexed by the azimuth they leave the head at, so neighbours stay neighbours: a flow.
  base     The sheet itself, as a closed, solid surface at the style's thickness above the skull: the sculpted
           silhouette. Strips of it are mapped onto the opaque SOLID strands of the atlas, strands along the
           guides, so it shades as hair and the style is never bald or see-through from play distance.
  cards    Ribbons along the same guides, lifted a few millimetres off the base in layers, each mapped onto one
           lock of the atlas. A card takes its shading normal from the BASE under it, not from its own plane:
           the mass lights as one volume (every card lighting as its own lump was the failure before), and
           its strands run along V, which is where the hair shader takes the strand direction from.
  scalp    hair_lib's cap on the skin with a painted, soft hairline, under everything.

Angles as in hair_lib: azimuth 0 = front, +90 = the character's left, 180 = back; elevation above the head centre.
"""
import math
import os

import numpy as np
from mathutils import Vector

import ba_common as ba
import hair_lib
import hairstyle_atlas as atlas
from hair_lib import smoothstep, unit

OUT = os.path.join(ba.ROOT, "build_hair")
DOWN = np.array([0.0, 0.0, -1.0])


def direction(az, e):
    az, e = np.radians(az), np.radians(e)
    return np.stack([np.sin(az) * np.cos(e), -np.cos(az) * np.cos(e), np.sin(e)], axis=-1)


def slerp(a, b, t):
    dot = float(np.clip(np.dot(a, b), -1, 1))
    angle = math.acos(dot)
    if angle < 1e-5:
        return a
    return (math.sin((1 - t) * angle) * a + math.sin(t * angle) * b) / math.sin(angle)


def _blur(grid, sigma):
    """Gaussian over the (azimuth, elevation) grid: wraps round in azimuth, clamps in elevation."""
    radius = int(math.ceil(sigma * 3))
    kernel = np.exp(-0.5 * (np.arange(-radius, radius + 1) / sigma) ** 2)
    kernel /= kernel.sum()
    out = np.zeros_like(grid)
    for k, w in zip(range(-radius, radius + 1), kernel):
        out += w * np.roll(grid, k, axis=0)
    padded = np.pad(out, ((0, 0), (radius, radius)), mode="edge")
    out2 = np.zeros_like(grid)
    for i, w in enumerate(kernel):
        out2 += w * padded[:, i:i + grid.shape[1]]
    return out2


class Skull:
    """The head as a smooth radius over direction: what hair lies on. Ears are bridged or smoothed away."""
    STEP = 3.0

    def __init__(self, head, cover_ears):
        self.head = head
        self.centre = head.centre
        self.az = np.arange(-180.0, 180.0, self.STEP)
        self.el = np.arange(-66.0, 90.1, self.STEP)
        raw = np.full((len(self.az), len(self.el)), np.nan)
        for i, az in enumerate(self.az):
            for j, e in enumerate(self.el):
                hit = head.bvh.ray_cast(Vector(self.centre), Vector(direction(az, e)), 0.4)
                if hit[0] is not None:
                    raw[i, j] = (np.array(hit[0]) - self.centre).dot(direction(az, e))
        for i in range(len(self.az)):                      # below the mesh: carry the last radius down
            last = np.nanmean(raw[i])
            for j in range(len(self.el) - 1, -1, -1):
                if np.isnan(raw[i, j]):
                    raw[i, j] = last
                else:
                    last = raw[i, j]
        wide = _blur(raw, 4.0)
        base = np.maximum(wide, raw) if cover_ears else np.minimum(raw, wide)
        self.r = _blur(base, 1.3)

    def radius(self, d):
        d = np.asarray(d, dtype=np.float64)
        d = d / np.linalg.norm(d, axis=-1, keepdims=True)
        az = np.degrees(np.arctan2(d[..., 0], -d[..., 1]))
        e = np.degrees(np.arcsin(np.clip(d[..., 2], -1, 1)))
        x = (az + 180.0) / self.STEP
        y = np.clip((e - self.el[0]) / self.STEP, 0, len(self.el) - 1.001)
        i0 = np.floor(x).astype(int)
        j0 = np.floor(y).astype(int)
        fx, fy = x - i0, y - j0
        n = len(self.az)
        r = self.r
        return ((1 - fx) * (1 - fy) * r[i0 % n, j0] + fx * (1 - fy) * r[(i0 + 1) % n, j0]
                + (1 - fx) * fy * r[i0 % n, j0 + 1] + fx * fy * r[(i0 + 1) % n, j0 + 1])

    def point(self, d, offset=0.0):
        d = unit(d)
        return self.centre + d * (float(self.radius(d)) + offset)


class Sheet:
    """The guide lines of a style, resampled evenly: G[column, row] and the outward normals N."""

    def __init__(self, style, head, skull):
        self.style, self.head, self.skull = style, head, skull
        self.thetas = np.asarray(style["thetas"], dtype=np.float64)
        self.closed = bool(style.get("closed", True))
        rows = style.get("rows", 14)
        lines, self.length = [], []
        for th in self.thetas:
            line = self._guide(float(th))
            seg = np.linalg.norm(np.diff(line, axis=0), axis=1)
            s = np.concatenate([[0.0], np.cumsum(seg)])
            t = np.linspace(0.0, s[-1], rows + 1)
            lines.append(np.stack([np.interp(t, s, line[:, k]) for k in range(3)], axis=1))
            self.length.append(s[-1])
        self.G = np.array(lines)
        self.length = np.array(self.length)
        self.C, self.R = self.G.shape[0], rows
        wave = style.get("wave")
        if wave:
            self._wave_base(wave)
        self.N = self._normals()

    def _guide(self, th):
        st, skull = self.style, self.skull
        d0, d1 = direction(*st["root"](th)), direction(th, st["exit"](th))
        length = st["length"](th)
        steps = st.get("scalp_steps", 12)
        points = []
        used = 0.0
        for i in range(steps + 1):
            t = i / steps
            p = skull.point(slerp(d0, d1, t), st["volume"](th, t))
            if points:
                step = np.linalg.norm(p - points[-1])
                if used + step > length:
                    points.append(points[-1] + (p - points[-1]) * (length - used) / step)
                    return np.array(points)
                used += step
            points.append(p)
        if not st.get("hangs", False):
            return np.array(points)
        # Off the head: straight down, drawn over a few centimetres onto the style's "cape" (where hair of this length
        # hangs: behind the shoulders, say), and kept clear of the body. Laid out, not simulated: no strand flies off.
        hang = length - used
        start = points[-1]
        tangent = unit(points[-1] - points[-2])
        tangent = np.array([tangent[0], tangent[1], 0.0])
        cape = st.get("cape")
        step = 0.012
        count = int(math.ceil(hang / step)) + 1
        fall = [start]
        for i in range(1, count + 1):
            depth = i * step
            free = start + tangent * min(depth, 0.03) * 0.6
            aim = cape(th, depth) if cape is not None else None
            if aim is None:
                h = free[:2] + np.array(st.get("tips_in", (0.0, 0.0))) * 0.0
            else:
                w = float(smoothstep(depth, 0.0, st.get("cape_within", 0.10))) * (aim[2] if len(aim) > 2 else 1.0)
                h = free[:2] * (1 - w) + np.array(aim[:2]) * w
            q = np.array([h[0], h[1], start[2] - depth])
            near, n, signed = self.head.nearest(q, True)
            need = st.get("clearance", 0.016)
            if signed < need:
                q = near + n * need
                q[2] = min(q[2], fall[-1][2] - step * 0.5)
            fall.append(q)
        fall = np.array(fall)
        for _ in range(2):
            fall[1:-1] = (fall[:-2] + 2 * fall[1:-1] + fall[2:]) / 4
        line = np.concatenate([np.array(points), fall[1:]])
        seg = np.linalg.norm(np.diff(line, axis=0), axis=1)
        s = np.concatenate([[0.0], np.cumsum(seg)])
        if s[-1] > length:
            keep = int(np.searchsorted(s, length))
            last = line[keep - 1] + (line[keep] - line[keep - 1]) * (length - s[keep - 1]) / max(s[keep] - s[keep - 1], 1e-9)
            line = np.concatenate([line[:keep], [last]])
        return line

    def _wave_base(self, wave):
        """The base swells and narrows with the waves, a little (the cards carry most of it)."""
        N = self._normals()
        for c in range(self.C):
            for r in range(self.R + 1):
                s = self.length[c] * r / self.R
                gain = smoothstep(s, wave["from"], wave["from"] + 0.08)
                self.G[c, r] += N[c, r] * wave["depth"] * 0.5 * gain * math.cos(2 * math.pi * s / wave["length"] + self.phase(c))

    def phase(self, c):
        wave = self.style.get("wave")
        return wave["twist"] * math.sin(math.radians(float(np.interp(c, np.arange(self.C), self.thetas))) * 2.0) if wave else 0.0

    def _normals(self):
        G = self.G
        if self.closed:
            across = np.roll(G, -1, axis=0) - np.roll(G, 1, axis=0)
        else:
            across = np.gradient(G, axis=0)
        along = np.gradient(G, axis=1)
        # Where the lines start from one point (the crown) there is no "across": take it from further down the line.
        N = np.cross(across, along)
        length = np.linalg.norm(N, axis=2, keepdims=True)
        N = N / np.maximum(length, 1e-12)
        axis = np.array([self.skull.centre[0], self.skull.centre[1], 0.0])
        for c in range(self.C):
            for r in range(self.R + 1):
                p = G[c, r]
                out = p - self.skull.centre if p[2] > self.skull.centre[2] - 0.03 else np.array([p[0] - axis[0], p[1] - axis[1], 0.0])
                out = unit(out)
                if length[c, r, 0] < 1e-9:
                    N[c, r] = out
                elif np.dot(N[c, r], out) < 0:
                    N[c, r] = -N[c, r]
                # Never facing inward by much: a fold in the sheet must not turn the shading over.
                if np.dot(N[c, r], out) < 0.25:
                    N[c, r] = unit(N[c, r] + out * (0.25 - np.dot(N[c, r], out)) * 1.5)
        for _ in range(2):                                  # one soft volume: smooth the normals over the sheet
            M = N.copy()
            M[:, 1:-1] += N[:, :-2] + N[:, 2:]
            if self.closed:
                M += np.roll(N, 1, axis=0) + np.roll(N, -1, axis=0)
            else:
                M[1:-1] += N[:-2] + N[2:]
            N = M / np.maximum(np.linalg.norm(M, axis=2, keepdims=True), 1e-12)
        return N

    def at(self, c, s):
        """Point, normal, the direction across the flow and the direction along it at column c (may be fractional), fraction s."""
        if self.closed:
            c = c % self.C
            c0 = int(math.floor(c)) % self.C
            c1 = (c0 + 1) % self.C
        else:
            c = min(max(c, 0.0), self.C - 1.0)
            c0 = min(int(math.floor(c)), self.C - 2)
            c1 = c0 + 1
        fc = c - math.floor(c) if self.closed else c - c0
        y = min(max(s, 0.0), 1.0) * self.R
        r0 = min(int(math.floor(y)), self.R - 1)
        fr = y - r0
        G, N = self.G, self.N
        a = G[c0, r0] * (1 - fr) + G[c0, r0 + 1] * fr
        b = G[c1, r0] * (1 - fr) + G[c1, r0 + 1] * fr
        p = a * (1 - fc) + b * fc
        n = unit((N[c0, r0] * (1 - fr) + N[c0, r0 + 1] * fr) * (1 - fc) + (N[c1, r0] * (1 - fr) + N[c1, r0 + 1] * fr) * fc)
        along = unit((G[c0, r0 + 1] - G[c0, r0]) * (1 - fc) + (G[c1, r0 + 1] - G[c1, r0]) * fc)
        if s > 1.0:                                          # past the end: straight on
            p = p + along * (s - 1.0) * (self.length[c0] * (1 - fc) + self.length[c1] * fc)
        across = np.cross(along, n)
        across = unit(across) if np.linalg.norm(across) > 1e-9 else unit(b - a)
        return p, n, across, along

    def metres(self, c):
        c0 = int(math.floor(c)) % self.C
        c1 = (c0 + 1) % self.C if self.closed else min(c0 + 1, self.C - 1)
        f = c - math.floor(c)
        return self.length[c0] * (1 - f) + self.length[c1] * f


class Mesh:
    def __init__(self):
        self.v, self.n, self.uv = [], [], []
        self.tri = {"scalp": [], "bulk": [], "wisps": []}
        self.count = 0

    def add(self, role, v, n, uv, tri):
        self.v.append(np.asarray(v, dtype=np.float64))
        self.n.append(np.asarray(n, dtype=np.float64))
        self.uv.append(np.asarray(uv, dtype=np.float64))
        self.tri[role].append(np.asarray(tri, dtype=np.int64) + self.count)
        self.count += len(v)

    def arrays(self):
        roles = [r for r in ("scalp", "bulk", "wisps") if self.tri[r]]
        return np.concatenate(self.v), np.concatenate(self.n), np.concatenate(self.uv), roles, [np.concatenate(self.tri[r]) for r in roles]


def add_base(mesh, sheet, rng, columns_per_strip=2, max_gap=0.07):
    """The sheet as a solid surface: strips of a few columns, each on one SOLID cell of the atlas."""
    cells = atlas.cells("SOLID")
    C, R = sheet.C, sheet.R
    last = C if sheet.closed else C - 1
    c = 0
    while c < last:
        width = min(columns_per_strip, last - c)
        u0, u1 = cells[int(rng.integers(len(cells)))]
        if rng.random() < 0.5:
            u0, u1 = u1, u0
        if not sheet.closed and sheet.style.get("soft_edges", True) and (c == 0 or c + width >= last):
            # The rim of an open sheet (beside the face): half a feathered lock, thin side out, so it ends in strands.
            lock = atlas.cells("DENSE")[int(rng.integers(len(atlas.cells("DENSE"))))]
            middle = 0.5 * (lock[0] + lock[1])
            u0, u1 = (lock[0], middle) if c == 0 else (middle, lock[1])
        v, n, uv, tri = [], [], [], []
        for k in range(width + 1):
            col = (c + k) % C
            for r in range(R + 1):
                v.append(sheet.G[col, r])
                n.append(sheet.N[col, r])
                uv.append((u0 + (u1 - u0) * k / width, 1.0 - r / R))
        for k in range(width):
            for r in range(R):
                a, b = k * (R + 1) + r, (k + 1) * (R + 1) + r
                if max(np.linalg.norm(np.array(v[a]) - v[b]), np.linalg.norm(np.array(v[a + 1]) - v[b + 1])) > max_gap:
                    continue                                  # the sheet parts here (over a shoulder)
                tri += [(a, b, b + 1), (a, b + 1, a + 1)]
        if tri:
            mesh.add("bulk", v, n, uv, _outward(np.array(v), np.array(n), np.array(tri)))
        c += width


def _outward(v, n, tri):
    """Triangles wound so that their face normal agrees with the shading normal."""
    a, b, c = v[tri[:, 0]], v[tri[:, 1]], v[tri[:, 2]]
    face = np.cross(b - a, c - a)
    flip = np.einsum("ij,ij->i", face, n[tri[:, 0]] + n[tri[:, 1]] + n[tri[:, 2]]) < 0
    tri = tri.copy()
    tri[flip] = tri[flip][:, ::-1]
    return tri


def add_card(mesh, sheet, rng, role, strip, c, s0, s1, width, lift, segments=6, drift=0.0, tilt=0.0, taper=0.0,
             bend=0.7, wave=None, flip_v=False, root_width=1.0):
    """One ribbon along the guides from fraction s0 to s1 of the line at column c.

    lift: (at the root, at the tip) metres above the base. drift: columns it wanders across the flow. tilt: one
    edge higher than the other, as a share of the half width. bend: how far the shading normal is the base's.
    """
    cells = atlas.cells(strip)
    u0, u1 = cells[int(rng.integers(len(cells)))]
    if rng.random() < 0.5:
        u0, u1 = u1, u0
    line = sheet.metres(c)
    v, n, uv = [], [], []
    phase = rng.uniform(-0.5, 0.5)
    for k in range(segments + 1):
        f = k / segments
        s = s0 + (s1 - s0) * f
        p, normal, across, along = sheet.at(c + drift * f, s)
        centre = p + normal * (lift[0] + (lift[1] - lift[0]) * f ** 1.4)
        if wave:
            metres = s * line
            gain = float(smoothstep(metres, wave["from"], wave["from"] + 0.08))
            angle = 2 * math.pi * metres / wave["length"] + sheet.phase(c) + phase * wave.get("loose", 1.0)
            centre = centre + across * wave["side"] * gain * math.sin(angle) + normal * wave["depth"] * gain * math.cos(angle)
        half = 0.5 * width * (root_width + (1 - root_width) * min(1.0, f * 3)) * (1 - taper * f)
        left = centre - across * half + normal * tilt * half
        right = centre + across * half - normal * tilt * half
        v += [left, right]
        uv += [(u0, f if flip_v else 1.0 - f), (u1, f if flip_v else 1.0 - f)]
        n += [normal, normal]
    v, n = np.array(v), np.array(n)
    if bend < 1.0:
        tri = np.array([(2 * k, 2 * k + 1, 2 * k + 3) for k in range(segments)] + [(2 * k, 2 * k + 3, 2 * k + 2) for k in range(segments)])
        own = hair_lib.vertex_normals(v, tri)
        own[np.einsum("ij,ij->i", own, n) < 0] *= -1
        n = n * bend + own * (1 - bend)
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    tri = []
    for k in range(segments):
        a = 2 * k
        tri += [(a, a + 1, a + 3), (a, a + 3, a + 2)]
    mesh.add(role, v, n, uv, _outward(v, n, np.array(tri)))


def add_layer(mesh, sheet, rng, layer):
    """Many cards: the layer says where (column range, start and length along the line) and how."""
    count = layer["count"]
    cards = []
    for i in range(count):
        # Even across the columns, jittered: no bald wedge, no pile.
        lo, hi = layer.get("columns", (0.0, sheet.C if sheet.closed else sheet.C - 1.0))
        c = lo + (hi - lo) * ((i + rng.uniform(0.15, 0.85)) / count)
        theta = float(np.interp(c % sheet.C if sheet.closed else c, np.arange(sheet.C), sheet.thetas))
        keep = layer.get("where")
        if keep is not None and rng.random() > keep(theta):
            continue
        line = sheet.metres(c)
        start = layer["start"](theta, rng) if callable(layer["start"]) else rng.uniform(*layer["start"])
        if layer.get("zone") is not None or layer.get("even"):
            # By place on the head (top, sides) and evenly over the surface, not over the columns (which crowd at the crown).
            here, _, _, _ = sheet.at(c, start)
            there, _, _, _ = sheet.at(c + 1.0, start)
            d = unit(here - sheet.skull.centre)
            chance = min(1.0, np.linalg.norm(there - here) / layer.get("spacing", 0.012)) if layer.get("even") else 1.0
            if layer.get("zone") is not None:
                chance *= layer["zone"](math.degrees(math.atan2(d[0], -d[1])), math.degrees(math.asin(max(-1.0, min(1.0, d[2])))))
            if rng.random() > chance:
                continue
        length = layer["length"](theta, rng) if callable(layer["length"]) else rng.uniform(*layer["length"])
        s0 = start if layer.get("start_is_fraction", True) else start / line
        s1 = min(s0 + length / line, layer.get("until", 1.0) + rng.uniform(0, layer.get("overhang", 0.0)))
        if s1 - s0 < 0.25 * length / line:
            continue
        cards.append((s0, c, s1, theta))
    cards.sort(key=lambda card: -card[0])          # those rooted furthest down first, the ones from the top over them
    for s0, c, s1, theta in cards:
        lift = layer["lift"]
        scale = rng.uniform(0.7, 1.3)
        width = layer["width"](theta) if callable(layer["width"]) else layer["width"]
        add_card(mesh, sheet, rng, layer.get("role", "bulk"), layer["strip"] if isinstance(layer["strip"], str) else layer["strip"][int(rng.integers(len(layer["strip"])))],
                 c, s0, s1, width * rng.uniform(0.8, 1.25), (lift[0] * scale, lift[1] * scale),
                 segments=max(2, int(round(layer.get("segments", 6) * min(1.0, (s1 - s0) * sheet.metres(c) / layer.get("full", 1e-3))))) if layer.get("full") else layer.get("segments", 6),
                 drift=rng.normal(0, layer.get("drift", 0.3)), tilt=rng.normal(0, layer.get("tilt", 0.15)), taper=layer.get("taper", 0.0),
                 bend=layer.get("bend", 0.7), wave=layer.get("wave", sheet.style.get("wave")) if layer.get("waves", True) else None,
                 root_width=layer.get("root_width", 1.0))


def add_hairline(mesh, sheet, rng, spec):
    """Hair that grows out of the hairline itself and is swept into the flow: each card is rooted on the skin just
    inside the hairline, rises, and joins the guide sheet a few centimetres on, then runs with it. This is what
    turns the front edge of the base from the rim of a cap into a hairline."""
    head, skull = sheet.head, sheet.skull
    flat = sheet.G.reshape(-1, 3)
    for side in (1.0, -1.0):
        for i in range(spec["count"]):
            lo, hi = spec["az"]
            az = lo + (hi - lo) * (i + rng.uniform(0.1, 0.9)) / spec["count"]
            e = float(head.hairline(az)) + rng.uniform(*spec.get("inside", (0.5, 4.0)))
            d0 = direction(side * az, e)
            az1 = az + spec["sweep"] * rng.uniform(0.7, 1.3)
            d1 = direction(side * az1, float(head.hairline(min(az1, 180.0))) + spec.get("rise", 6.0) * rng.uniform(0.6, 1.4))
            if spec.get("to") is not None:                      # a fixed place the hairline hair is combed to
                d1 = direction(*spec["to"](side * az, rng))
            index = int(np.argmin(np.linalg.norm(flat - skull.point(d1, 0.012), axis=1)))
            c, r = divmod(index, sheet.R + 1)
            join = flat[index]
            d1 = unit(join - skull.centre)
            off1 = float(np.linalg.norm(join - skull.centre) - skull.radius(d1)) + spec.get("lift", 0.004)
            points, normals = [], []
            for f in np.linspace(0.0, 1.0, 5):
                d = unit(slerp(d0, d1, float(f)))
                points.append(skull.point(d, 0.001 + (off1 - 0.001) * f + spec.get("arch", 0.004) * math.sin(math.pi * f)))
                normals.append(d)
            s0 = r / sheet.R
            for g in (0.34, 0.67, 1.0):
                q, n, _, _ = sheet.at(c, s0 + spec.get("on", 0.18) * g)
                points.append(q + n * (spec.get("lift", 0.004) + 0.002 * g))
                normals.append(n)
            strips = spec["strip"]
            _ribbon(mesh, spec.get("role", "wisps"), np.array(points), np.array(normals), spec["width"] * rng.uniform(0.75, 1.3),
                    strips if isinstance(strips, str) else strips[int(rng.integers(len(strips)))], rng, root_width=spec.get("root_width", 0.6))


def _ribbon(mesh, role, points, normals, width, strip, rng, root_width=1.0, taper=0.0):
    cells = atlas.cells(strip)
    u0, u1 = cells[int(rng.integers(len(cells)))]
    if rng.random() < 0.5:
        u0, u1 = u1, u0
    seg = np.linalg.norm(np.diff(points, axis=0), axis=1)
    along = np.concatenate([[0.0], np.cumsum(seg)]) / max(seg.sum(), 1e-9)
    v, n, uv = [], [], []
    count = len(points)
    for k in range(count):
        tangent = unit(points[min(k + 1, count - 1)] - points[max(k - 1, 0)])
        across = unit(np.cross(tangent, normals[k]))
        f = along[k]
        half = 0.5 * width * (root_width + (1 - root_width) * min(1.0, f * 3)) * (1 - taper * f)
        v += [points[k] - across * half, points[k] + across * half]
        n += [normals[k], normals[k]]
        uv += [(u0, 1.0 - f), (u1, 1.0 - f)]
    tri = []
    for k in range(count - 1):
        a = 2 * k
        tri += [(a, a + 1, a + 3), (a, a + 3, a + 2)]
    v, n = np.array(v), np.array(n)
    mesh.add(role, v, n, uv, _outward(v, n, np.array(tri)))


def add_scalp(mesh, head, style, name):
    """hair_lib's cap on the skin with a painted hairline: fine-grained, soft over a few degrees, thinned where the
    style says the hair is cropped (a fade). Grey, the material gives the colour."""
    from PIL import Image, ImageFilter
    v, n, uv, tri, _ = hair_lib.build_cap(head)
    mesh.add("scalp", v, n, uv, tri)
    rng = np.random.default_rng(style.get("seed", 1) + 500)
    size = 512
    options = style.get("scalp", {})
    ys, xs = np.mgrid[0:size, 0:size]
    u, w = (xs + 0.5) / size, 1.0 - (ys + 0.5) / size
    dx, dy = (u - 0.5) * 2, (0.5 - w) * 2
    r = np.sqrt(dx * dx + dy * dy)
    azimuth = np.degrees(np.arctan2(dx, dy))
    e = 90.0 - r * 150.0
    inside = e - head.hairline(np.abs(azimuth))

    def noise(sigma):
        img = Image.fromarray((rng.random((size, size)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(sigma))
        a = np.asarray(img, dtype=np.float64)
        return (a - a.mean()) / max(a.std(), 1e-6)
    soft = options.get("soft", 5.0)
    alpha = smoothstep(inside + noise(1.2) * soft * 0.35 + noise(6.0) * soft * 0.15, 0.0, soft)
    density = options.get("density")                 # lambda |azimuth|, elevation -> 0..1
    if density is not None:
        d = np.clip(density(np.abs(azimuth), e), 0.0, 1.0)
        alpha = alpha * np.clip(d + (1 - d) * (noise(0.8) * 0.5 + d * 1.2 - 0.3), 0.0, 1.0) * (0.35 + 0.65 * d)
    tone = np.clip(options.get("tone", 0.55) + noise(0.9) * 0.10, 0.1, 1.0)
    grey = (tone * 255).astype(np.uint8)
    out = np.dstack([grey, grey, grey, (np.clip(alpha * options.get("alpha", 1.0), 0, 1) * 255).astype(np.uint8)])
    Image.fromarray(out, "RGBA").save(os.path.join(OUT, "textures", name + ".png"))


def body_shapes(head_name, body_name, vertices, below):
    """Fat, Slim, Musc for hair that lies on neck and shoulders: the deltas of the nearest skin, eased in below the jaw."""
    from mathutils.kdtree import KDTree
    refs = [ba.load_reference(head_name), ba.load_reference(body_name)]
    points = np.concatenate([r["vertices"] for r in refs])
    tree = KDTree(len(points))
    for i, p in enumerate(points):
        tree.insert(Vector(p), i)
    tree.balance()
    nearest = np.array([tree.find(Vector(p))[1] for p in vertices])
    gain = 1.0 - smoothstep(vertices[:, 2], below - 0.05, below)
    shapes = []
    for name in ("Fat", "Slim", "Musc"):
        deltas = np.concatenate([r["shapes"].get(name, np.zeros_like(r["vertices"])) for r in refs])
        shapes.append((name, deltas[nearest] * gain[:, None]))
    return shapes


def build(style):
    rng = np.random.default_rng(style.get("seed", 1))
    head = hair_lib.Head(style["sex"])
    head.hairline_shift = style.get("hairline_shift", 0.0)
    head.hairline_scale = style.get("hairline_adjust")
    skull = Skull(head, style.get("cover_ears", False))
    sheet = Sheet(style, head, skull)
    os.makedirs(os.path.join(OUT, "debug"), exist_ok=True)
    np.save(os.path.join(OUT, "debug", style["id"] + "_guides.npy"), sheet.G)
    mesh = Mesh()
    scalp_name = "hairstyle_scalp_" + style["id"][len("hair_"):]
    add_scalp(mesh, head, style, scalp_name)
    add_base(mesh, sheet, rng, style.get("columns_per_strip", 2))
    for layer in style["layers"]:
        if layer.get("hairline"):
            add_hairline(mesh, sheet, rng, layer)
        else:
            add_layer(mesh, sheet, rng, layer)
    v, n, uv, roles, submeshes = mesh.arrays()
    long_hair = bool(style.get("hangs", False))
    indices, weights = hair_lib.skin_weights(v, long_hair)
    info = hair_lib.HEADS[style["sex"]]
    shapes = [("Hat", hair_lib.hat_shape(head, v, "Hat")), ("Half_Hat", hair_lib.hat_shape(head, v, "HalfHat" if style["sex"] == "male" else "HalfHatFemale"))]
    if long_hair:
        shapes += body_shapes(info["head"], info["body"], v, 1.53)
    ba.BUILD = OUT
    stats = ba.write_mesh(style["id"], v, n, uv, submeshes, indices, weights, shapes)
    for role, t in zip(roles, submeshes):
        print("   %-6s %5d triangles" % (role, len(t)))
    layers = [{"role": r, "texture": scalp_name if r == "scalp" else "hairstyle_atlas", "normal": None, "mask": None} for r in roles]
    item = {"id": style["id"], "kind": "hair", "sex": style["sex"], "mesh": style["id"], "layers": layers, "skinFrom": None, "stats": stats}
    textures = [{"name": scalp_name, "file": "textures/" + scalp_name + ".png", "type": "color", "maxSize": 256}]
    return item, textures
