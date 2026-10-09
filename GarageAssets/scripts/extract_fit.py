"""Measures every vanilla car body so the Garage's fitted parts (spoiler, roof rack, bull bar) can be placed
on the real shape and not on a bounding box: for each prefab with a CarController, the vertices of the meshes
under CarHolder/Body in the prefab's own space, reduced to a height profile along the centre line and the
few numbers the parts need. Prints one C# row per car for Scripts/Garage/GarageFit.cs.

LOCAL REFERENCE ONLY: reads the installed game; nothing from it is committed or shipped but the numbers.

    python -I extract_fit.py <Big Ambitions_Data folder> <pylib folder with UnityPy>
"""
import math

import numpy as np
import os
import sys

data_dir, pylib = sys.argv[1], sys.argv[2]
sys.path.insert(0, pylib)
import UnityPy  # noqa: E402

aa = os.path.join(data_dir, "StreamingAssets", "aa", "StandaloneWindows64")
env = UnityPy.load(*[os.path.join(aa, f) for f in os.listdir(aa) if "assets_prefabs_" in f])


def qrot(q, v):
    x, y, z, w = q.x, q.y, q.z, q.w
    vx, vy, vz = v
    tx, ty, tz = 2 * (y * vz - z * vy), 2 * (z * vx - x * vz), 2 * (x * vy - y * vx)
    return (vx + w * tx + (y * tz - z * ty), vy + w * ty + (z * tx - x * tz), vz + w * tz + (x * ty - y * tx))


def collect(go, chain, out, under_body):
    transform = None
    mesh = None
    for c in go.m_Component:
        ptr = c.component if hasattr(c, "component") else c[1]
        kind = ptr.type.name
        if kind == "Transform":
            transform = ptr.read()
        elif kind == "MeshFilter":
            try:
                mesh = ptr.read().m_Mesh.read()
            except Exception:
                mesh = None
    if transform is None or not go.m_IsActive:
        return
    chain = chain + [transform]
    name = go.m_Name
    if under_body and mesh is not None and "Shadow" not in name:
        verts, tris = [], []
        for line in mesh.export().splitlines():
            if line.startswith("v "):
                _, x, y, z = line.split()[:4]
                v = (-float(x), float(y), float(z))
                for t in reversed(chain[1:]):   # up to, not including, the prefab root
                    s = t.m_LocalScale
                    v = qrot(t.m_LocalRotation, (v[0] * s.x, v[1] * s.y, v[2] * s.z))
                    p = t.m_LocalPosition
                    v = (v[0] + p.x, v[1] + p.y, v[2] + p.z)
                verts.append(v)
            elif line.startswith("f "):
                tris.append([int(part.split("/")[0]) - 1 for part in line.split()[1:4]])
        out.append((name, bool(getattr(mesh, "m_IsReadable", False)), np.array(verts), np.array(tris)))
    for child in transform.m_Children:
        try:
            kid = child.read().m_GameObject.read()
        except Exception:
            continue
        collect(kid, chain, out, under_body or kid.m_Name == "Body")


class Body:
    """Every triangle of a car's body; height(x, z) is the top of the body above that point (0 if there is none)."""

    def __init__(self, meshes):
        a, b, c = [], [], []
        for _, _, verts, tris in meshes:
            if len(tris):
                a.append(verts[tris[:, 0]]); b.append(verts[tris[:, 1]]); c.append(verts[tris[:, 2]])
        self.a, self.b, self.c = np.concatenate(a), np.concatenate(b), np.concatenate(c)
        self.all = np.concatenate([self.a, self.b, self.c])

    def heights(self, x, z):
        a, b, c = self.a, self.b, self.c
        d = (b[:, 2] - c[:, 2]) * (a[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (a[:, 2] - c[:, 2])
        ok = np.abs(d) > 1e-9
        d = np.where(ok, d, 1.0)
        u = ((b[:, 2] - c[:, 2]) * (x - c[:, 0]) + (c[:, 0] - b[:, 0]) * (z - c[:, 2])) / d
        v = ((c[:, 2] - a[:, 2]) * (x - c[:, 0]) + (a[:, 0] - c[:, 0]) * (z - c[:, 2])) / d
        w = 1 - u - v
        inside = ok & (u >= -1e-6) & (v >= -1e-6) & (w >= -1e-6)
        return (u * a[:, 1] + v * b[:, 1] + w * c[:, 1])[inside]

    def height(self, x, z):
        h = self.heights(x, z)
        return float(h.max()) if len(h) else 0.0

    def low(self, x, z):
        h = self.heights(x, z)
        return float(h.min()) if len(h) else 0.0


def edge(body, z, level, limit):
    """How far out from the centre line the body is still above a level, at one z."""
    x = 0.0
    while x < limit and body.height(x + 0.02, z) >= level:
        x += 0.02
    return x


rows = []
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        mono = obj.read()
        if mono.m_Script.read().m_ClassName != "CarController":
            continue
        go = mono.m_GameObject.read()
    except Exception:
        continue
    meshes = []
    collect(go, [], meshes, False)
    if not meshes:
        rows.append("// %s: no body mesh found" % go.m_Name)
        continue
    body = Body(meshes)
    readable = all(m[1] for m in meshes)
    z0, z1 = float(body.all[:, 2].min()), float(body.all[:, 2].max())
    half = float(np.abs(body.all[:, 0]).max())
    # the centre line from tail to nose, every 4 cm
    zs = np.arange(z0 + 0.02, z1, 0.04)
    prof = [body.height(0.0, z) for z in zs]
    top = max(prof)
    # the roof: the longest stretch within 6 cm of the top
    best, i = (0, 0), 0
    while i < len(zs):
        if prof[i] > top - 0.06:
            j = i
            while j < len(zs) and prof[j] > top - 0.06:
                j += 1
            if j - i > best[1] - best[0]:
                best = (i, j)
            i = j
        else:
            i += 1
    roof0, roof1 = float(zs[best[0]]), float(zs[best[1] - 1])
    length = roof1 - roof0
    bar0, bar1 = roof0 + 0.22 * length, roof1 - 0.22 * length
    roof_y = max(body.height(0.0, bar0), body.height(0.0, bar1))
    roof_half = min(edge(body, bar0, roof_y - 0.07, half), edge(body, bar1, roof_y - 0.07, half))
    foot_y = min(body.height(s * roof_half * 0.92, z) for s in (-1, 1) for z in (bar0, bar1))
    # the boot deck, 0.10 to 0.46 m in from the tail
    d0, d1, dm = body.height(0.0, z0 + 0.10), body.height(0.0, z0 + 0.46), body.height(0.0, z0 + 0.26)
    slope = math.degrees(math.atan2(abs(d1 - d0), 0.36))
    deck_half = edge(body, z0 + 0.26, dm - 0.06, half)
    post_y = min(body.height(s * deck_half * 0.55, z0 + 0.26) for s in (-1, 1))
    # the nose
    nose_hi = body.height(0.0, z1 - 0.06)
    nose_lo = body.low(0.0, z1 - 0.06)
    nose_half = edge(body, z1 - 0.10, nose_lo + 0.02, half)
    bonnet = body.height(0.0, z1 - 0.40)
    rows.append('{ "%s", new CarFit(%.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.1ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff, %.2ff) },   // %s%s'
                % (go.m_Name.lower(), z0, z1, half, top, roof0, roof1, roof_y, roof_half, foot_y, dm, slope, deck_half, post_y, nose_lo, nose_hi, nose_half, bonnet,
                   ", ".join(m[0] for m in meshes), "" if readable else " (not readable at run time)"))
print("// rear z, front z, half width, top | roof from z, to z, height at the bars, half width, height at the feet | deck height, slope (deg), half width, height at the posts | nose low, nose high, nose half width, bonnet height")
for r in sorted(rows):
    print(r)
