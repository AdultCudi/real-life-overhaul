using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The city's buildings as the map keeps them: each a convex outline seen from above (anticlockwise, x east, z north, in
    /// metres), the height of its foot above the scene's ground plane, its height, and the address it is known by (0 for none,
    /// which only the mod's own buildings and the fixture have). The tint is the game's highlight on it, written by the director
    /// while the map is open. Game-free; the game's measure of it is Navigation/NavBase.
    /// </summary>
    public sealed class Map25City
    {
        public int Count;
        public int[] HullStart = new int[1];
        public float[] HullX = new float[0], HullZ = new float[0];
        public float[] BaseY = new float[0], Height = new float[0];
        public long[] Key = new long[0];
        public Map25Colour[] Tint = new Map25Colour[0];
        public float[] TintShare = new float[0];
        public float MinX, MinZ, MaxX, MaxZ;

        /// <summary>Sizes the per building arrays for <paramref name="count"/> buildings and <paramref name="corners"/> outline corners.</summary>
        public void Reserve(int count, int corners)
        {
            Count = count;
            HullStart = new int[count + 1];
            HullX = new float[corners];
            HullZ = new float[corners];
            BaseY = new float[count];
            Height = new float[count];
            Key = new long[count];
            Tint = new Map25Colour[count];
            TintShare = new float[count];
        }

        /// <summary>
        /// The city from the arrays the minimap keeps (every outline anticlockwise from <paramref name="start"/>, the street's height beside
        /// each and its own): the scene's ground plane is the median street, so a block's foot is its street's difference from it.
        /// </summary>
        public static Map25City FromOutlines(int hulls, int[] start, float[] x, float[] z, long[] key, float[] foot, float[] height, out float y0)
        {
            var city = new Map25City();
            city.Reserve(hulls, start[hulls]);
            Array.Copy(x, city.HullX, start[hulls]);
            Array.Copy(z, city.HullZ, start[hulls]);
            Array.Copy(start, city.HullStart, hulls + 1);
            var sorted = new float[hulls];
            Array.Copy(foot, sorted, Math.Min(hulls, foot.Length));
            Array.Sort(sorted);
            y0 = hulls > 0 ? sorted[hulls / 2] : 0f;
            for (int i = 0; i < hulls; i++)
            {
                city.BaseY[i] = (i < foot.Length ? foot[i] : y0) - y0;
                city.Height[i] = i < height.Length && height[i] > 0f ? height[i] : 12f;
                city.Key[i] = i < key.Length ? key[i] : 0L;
            }
            city.FindBounds();
            return city;
        }

        public void FindBounds()
        {
            MinX = MinZ = float.MaxValue;
            MaxX = MaxZ = float.MinValue;
            for (int i = 0; i < HullStart[Count]; i++)
            {
                MinX = Math.Min(MinX, HullX[i]);
                MaxX = Math.Max(MaxX, HullX[i]);
                MinZ = Math.Min(MinZ, HullZ[i]);
                MaxZ = Math.Max(MaxZ, HullZ[i]);
            }
        }
    }

    /// <summary>How much of the blocks' detail a set carries: the full map wants every one, the minimap, a pixel of which is a metre, wants the shapes and the bands.</summary>
    public struct Map25Detail
    {
        public float Band;   // metres of height one window band stands for
        public bool Fine;    // awnings, lit windows, cornices

        public static Map25Detail Full => new Map25Detail { Band = 7.2f, Fine = true };
        public static Map25Detail Small => new Map25Detail { Band = 21.6f, Fine = false };
    }

    /// <summary>
    /// The blocks, every detail of the approved mockups (make_map25d.py) as colour in the vertices and a few triangles: the
    /// walls shaded by the way they face with a darker foot, windows as bands a floor pair high, a heavier podium and a lighter
    /// crown, a cornice, a parapet rim on the roof, a setback on a tall building, a water tank, a row of air conditioners and
    /// a stair hut on some roofs, awnings on the north and south walls of shops, a few lit windows on towers. A building's
    /// draws are fixed by its address alone: the tint and the detail change colours and counts of nothing else, so a tint is
    /// a write of colours into the same vertices. One instance a thread (it keeps its scratch arrays).
    /// </summary>
    public sealed class Map25Blocks
    {
        private const int Most = 32;                 // corners of an outline we take
        private const float Floor = 3.0f;            // metres of the shop's ground floor band
        private const float Awning = 0.25f;          // metres an awning stands out: little, so that from above a shop front has no bar beside it
        private const float WindowShare = 0.12f;     // how far a window band goes to the window colour

        private readonly float[] _x = new float[Most], _z = new float[Most];
        private readonly float[] _qx = new float[Most], _qz = new float[Most];
        private readonly float[] _hx = new float[Most * 2 + 2], _hz = new float[Most * 2 + 2];
        private readonly float[] _px = new float[Most * 2], _pz = new float[Most * 2];

        // the building's fixed decisions, drawn first and always in the same order
        private int _tone;
        private float _vary;
        private bool _shop, _props;
        private readonly bool[] _slot = new bool[2];
        private readonly float[] _slotKind = new float[2], _slotFx = new float[2], _slotFy = new float[2];
        private readonly int[] _slotCount = new int[2];
        private readonly int[] _lit = new int[4];
        private readonly float[] _litAt = new float[4];
        private readonly int[] _litFloor = new int[4];

        /// <summary>Draws building <paramref name="i"/> of the city into the builder, solid and shaded.</summary>
        public void Generate(Map25Builder b, Map25City city, int i, Map25Detail detail)
        {
            int start = city.HullStart[i];
            int n = city.HullStart[i + 1] - start;
            if (n < 3 || n > Most)
            {
                return;
            }
            float y0 = city.BaseY[i];
            float h = Math.Max(2f, city.Height[i]);
            long key = city.Key.Length > i ? city.Key[i] : 0L;
            uint seed = key != 0L ? (uint)(key ^ (key >> 32)) : (uint)i * 2654435761u + 97u;
            Decide(seed, h, n);
            for (int k = 0; k < n; k++)
            {
                _x[k] = city.HullX[start + k];
                _z[k] = city.HullZ[start + k];
            }
            Map25Colour tone = Map25Palette.Tone(_tone).Scale(_vary);
            bool tinted = city.TintShare.Length > i && city.TintShare[i] > 0f;
            Map25Colour tint = tinted ? city.Tint[i] : default;
            if (tinted)
            {
                tone = Map25Palette.Tinted(tone, tint, city.TintShare[i]);
            }
            var awningRng = new Map25Rng(seed ^ 0xA5A5A5A5u);
            for (int e = 0; e < n; e++)
            {
                int f = (e + 1) % n;
                Wall(b, _x[e], _z[e], _x[f], _z[f], y0, h, tone, detail, e, tinted, tint, ref awningRng);
            }
            Extents(_x, _z, n, out float ux, out float uz, out float u0, out float u1, out float v0, out float v1);
            float width = Math.Min(u1 - u0, v1 - v0);
            float scale = Math.Max(0.45f, Math.Min(1.1f, width / 24f));
            float rim = Math.Min(0.9f, width * 0.18f);
            float top = y0 + h;
            Map25Colour roof = tone.Scale(1.14f);
            Roof(b, _x, _z, n, top, roof, tone.Scale(1.32f), roof.Scale(0.92f), rim, width);
            float[] rx = _x, rz = _z;
            int rn = n;
            if (h > 36f && width > 16f)
            {
                float step = width * 0.22f;
                Inset(_x, _z, n, step, _qx, _qz);
                float h2 = h * 0.42f;
                Map25Colour tier = tone.Scale(1.04f);
                for (int e = 0; e < n; e++)
                {
                    int f = (e + 1) % n;
                    float dx = _qx[f] - _qx[e], dz = _qz[f] - _qz[e];
                    float l = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (l < 0.3f)
                    {
                        continue;
                    }
                    Map25Colour c = tier.Scale(Map25Palette.WallFactor(dz / l, -dx / l));
                    b.Strip(_qx[e], _qz[e], _qx[f], _qz[f], top, top + h2, c.Scale(0.70f), c);
                }
                top += h2;
                Map25Colour roof2 = tier.Scale(1.14f);
                Roof(b, _qx, _qz, n, top, roof2, tier.Scale(1.32f), roof2.Scale(0.92f), Math.Min(0.6f, rim), width * 0.5f);
                rx = _qx;
                rz = _qz;
                Extents(rx, rz, rn, out ux, out uz, out u0, out u1, out v0, out v1);
                scale *= 0.8f;
            }
            if (_props)
            {
                Props(b, rx, rz, rn, top, tone, ux, uz, u0, u1, v0, v1, scale);
            }
        }

        // Every choice of a building, drawn whether or not it is used.
        private void Decide(uint seed, float height, int corners)
        {
            var rng = new Map25Rng(seed);
            _tone = rng.Int(5);
            _vary = rng.Range(0.94f, 1.07f);
            _shop = rng.Float() < (height < 30f ? 0.85f : 0.6f);
            _props = rng.Float() < 0.55f;
            for (int s = 0; s < 2; s++)
            {
                _slot[s] = rng.Float() < 0.55f;
                _slotKind[s] = rng.Float();
                _slotFx[s] = s == 0 ? rng.Range(0.28f, 0.42f) : rng.Range(0.58f, 0.72f);
                _slotFy[s] = rng.Range(0.3f, 0.7f);
                _slotCount[s] = 1 + rng.Int(3);
            }
            for (int k = 0; k < 4; k++)
            {
                _lit[k] = rng.Int(corners);
                _litAt[k] = rng.Range(0.2f, 0.8f);
                _litFloor[k] = rng.Int(8);
            }
        }

        private void Wall(Map25Builder b, float ax, float az, float bx, float bz, float y0, float h, Map25Colour tone, Map25Detail detail,
                          int edge, bool tinted, Map25Colour tint, ref Map25Rng awningRng)
        {
            float dx = bx - ax, dz = bz - az;
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < 0.3f)
            {
                return;
            }
            float ux = dx / length, uz = dz / length;
            float nx = uz, nz = -ux;
            float factor = Map25Palette.WallFactor(nx, nz);
            Map25Colour wall = tone.Scale(factor);
            Map25Colour glass = wall.Mix(Map25Palette.Window, WindowShare);
            float body = h >= 22f ? h - 1.6f : h;
            float ground = _shop ? Math.Min(Floor, h * 0.5f) : 0f;
            float z = 0f;
            if (ground > 0f)
            {
                Piece(b, ax, az, bx, bz, y0, h, 0f, ground, wall.Scale(0.74f));
                z = ground;
            }
            float floor = detail.Band;
            float window = floor * 0.30f;
            float cursor = z;
            float at = z;
            int floorNo = 0;
            while (at + floor * 0.5f < body)
            {
                float ws = at + (floor - window) * 0.5f, we = ws + window;
                if (we > body)
                {
                    break;
                }
                Piece(b, ax, az, bx, bz, y0, h, cursor, ws, wall);
                Piece(b, ax, az, bx, bz, y0, h, ws, we, glass);
                if (detail.Fine && h >= 30f && length > 8f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        if (_lit[k] == edge && _litFloor[k] == floorNo % 8)
                        {
                            float t = 1.5f + _litAt[k] * (length - 3.6f);
                            float wx = ax + ux * t, wz = az + uz * t;
                            Map25Colour lit = wall.Mix(Map25Palette.Lit, 0.75f);
                            b.Flat(wx + nx * 0.04f, y0 + ws, wz + nz * 0.04f, wx + ux * 1.4f + nx * 0.04f, y0 + ws, wz + uz * 1.4f + nz * 0.04f,
                                   wx + ux * 1.4f + nx * 0.04f, y0 + we, wz + uz * 1.4f + nz * 0.04f, wx + nx * 0.04f, y0 + we, wz + nz * 0.04f, lit, 1f);
                        }
                    }
                }
                cursor = we;
                at += floor;
                floorNo++;
            }
            Piece(b, ax, az, bx, bz, y0, h, cursor, body, wall);
            if (h >= 22f && detail.Fine)
            {
                Piece(b, ax, az, bx, bz, y0, h, body, body + 0.5f, wall.Scale(0.84f));
                Piece(b, ax, az, bx, bz, y0, h, body + 0.5f, h, wall.Scale(1.16f));
            }
            else if (h >= 22f)
            {
                Piece(b, ax, az, bx, bz, y0, h, body, h, wall.Scale(1.10f));
            }
            if (_shop && detail.Fine && (nz > 0.75f || nz < -0.75f) && length >= 9f)
            {
                int segments = Math.Max(1, (int)(length / 12f));
                float seg = length / segments;
                for (int s = 0; s < segments; s++)
                {
                    int pick = awningRng.Int(Map25Palette.Awnings.Length);
                    Map25Colour a = tinted ? tint : Map25Palette.Awnings[pick].Mix(tone, 0.30f);
                    a = a.Scale(0.80f + 0.35f * factor);
                    float s0 = s * seg + 1.2f, s1 = (s + 1) * seg - 1.2f;
                    float x0 = ax + ux * s0, z0 = az + uz * s0, x1 = ax + ux * s1, z1 = az + uz * s1;
                    b.Flat(x0, y0 + 3.2f, z0, x1, y0 + 3.2f, z1, x1 + nx * Awning, y0 + 2.5f, z1 + nz * Awning, x0 + nx * Awning, y0 + 2.5f, z0 + nz * Awning, a, 1f);
                }
            }
        }

        // One band of a wall between two heights, its two edges as dark at the foot as the wall is there.
        private static void Piece(Map25Builder b, float ax, float az, float bx, float bz, float y0, float h, float za, float zb, Map25Colour colour)
        {
            if (zb - za < 0.01f)
            {
                return;
            }
            float ta = za / h, tb = zb / h;
            Map25Colour low = Podium(colour, ta, h).Scale(1f - Map25Palette.Foot(ta));
            Map25Colour high = Podium(colour, tb, h).Scale(1f - Map25Palette.Foot(tb));
            b.Strip(ax, az, bx, bz, y0 + za, y0 + zb, low, high);
        }

        // A taller building is heavier at its foot and a little lighter at its crown.
        private static Map25Colour Podium(Map25Colour colour, float t, float h)
        {
            if (h < 20f)
            {
                return colour;
            }
            if (t < 0.30f)
            {
                return colour.Scale(0.90f);
            }
            return t > 0.74f ? colour.Mix(new Map25Colour(1f, 1f, 1f), 0.035f) : colour;
        }

        private static void Roof(Map25Builder b, float[] x, float[] z, int n, float y, Map25Colour top, Map25Colour rim, Map25Colour inner, float inset, float width)
        {
            if (width < 3.5f || inset < 0.2f)
            {
                int first = b.Vertex(x[0], y, z[0], top);
                int prev = b.Vertex(x[1], y, z[1], top);
                for (int k = 2; k < n; k++)
                {
                    int next = b.Vertex(x[k], y, z[k], top);
                    b.Triangle(first, prev, next);
                    prev = next;
                }
                return;
            }
            var qx = new float[n];
            var qz = new float[n];
            Inset(x, z, n, inset, qx, qz);
            for (int e = 0; e < n; e++)
            {
                int f = (e + 1) % n;
                b.Flat(x[e], y, z[e], x[f], y, z[f], qx[f], y, qz[f], qx[e], y, qz[e], rim, 1f);
            }
            int a = b.Vertex(qx[0], y, qz[0], inner);
            int p = b.Vertex(qx[1], y, qz[1], inner);
            for (int k = 2; k < n; k++)
            {
                int c = b.Vertex(qx[k], y, qz[k], inner);
                b.Triangle(a, p, c);
                p = c;
            }
        }

        /// <summary>An anticlockwise outline moved in by <paramref name="distance"/> (out when negative), corner by corner along the bisector.</summary>
        public static void Inset(float[] x, float[] z, int n, float distance, float[] outX, float[] outZ)
        {
            for (int k = 0; k < n; k++)
            {
                int prev = (k + n - 1) % n, next = (k + 1) % n;
                float d0x = x[k] - x[prev], d0z = z[k] - z[prev];
                float d1x = x[next] - x[k], d1z = z[next] - z[k];
                float l0 = (float)Math.Sqrt(d0x * d0x + d0z * d0z), l1 = (float)Math.Sqrt(d1x * d1x + d1z * d1z);
                if (l0 < 1e-5f || l1 < 1e-5f)
                {
                    outX[k] = x[k];
                    outZ[k] = z[k];
                    continue;
                }
                float n0x = -d0z / l0, n0z = d0x / l0, n1x = -d1z / l1, n1z = d1x / l1;   // inward normals of the two edges
                float denominator = 1f + n0x * n1x + n0z * n1z;
                if (denominator < 0.2f)
                {
                    outX[k] = x[k] + n1x * distance;
                    outZ[k] = z[k] + n1z * distance;
                }
                else
                {
                    outX[k] = x[k] + (n0x + n1x) * distance / denominator;
                    outZ[k] = z[k] + (n0z + n1z) * distance / denominator;
                }
            }
        }

        /// <summary>The longest side's direction and the outline's extents along it and across it, from the first corner.</summary>
        public static void Extents(float[] x, float[] z, int n, out float ux, out float uz, out float u0, out float u1, out float v0, out float v1)
        {
            float best = -1f;
            ux = 1f;
            uz = 0f;
            for (int k = 0; k < n; k++)
            {
                int f = (k + 1) % n;
                float dx = x[f] - x[k], dz = z[f] - z[k];
                float l = dx * dx + dz * dz;
                if (l > best)
                {
                    best = l;
                    float root = (float)Math.Sqrt(l);
                    ux = root > 0f ? dx / root : 1f;
                    uz = root > 0f ? dz / root : 0f;
                }
            }
            u0 = v0 = float.MaxValue;
            u1 = v1 = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                float dx = x[k] - x[0], dz = z[k] - z[0];
                float u = dx * ux + dz * uz, v = -dx * uz + dz * ux;
                u0 = Math.Min(u0, u);
                u1 = Math.Max(u1, u);
                v0 = Math.Min(v0, v);
                v1 = Math.Max(v1, v);
            }
        }

        private void Props(Map25Builder b, float[] x, float[] z, int n, float y, Map25Colour tone, float ux, float uz, float u0, float u1, float v0, float v1, float scale)
        {
            for (int s = 0; s < 2; s++)
            {
                if (!_slot[s])
                {
                    continue;
                }
                float u = u0 + _slotFx[s] * (u1 - u0), v = v0 + _slotFy[s] * (v1 - v0);
                float cx = x[0] + ux * u - uz * v, cz = z[0] + uz * u + ux * v;
                float kind = _slotKind[s];
                float room = (kind < 0.28f ? 2.0f : kind < 0.58f ? 1.8f : 1.2f + 1.7f * (_slotCount[s] - 1)) * scale;
                if (!Inside(x, z, n, cx, cz, room))
                {
                    continue;
                }
                if (kind < 0.28f)
                {
                    Tank(b, cx, cz, y, 1.7f * scale);
                }
                else if (kind < 0.58f)
                {
                    Box(b, cx, cz, y, 1.7f * scale, 1.4f * scale, 2.0f * scale, tone.Scale(1.02f), ux, uz);
                }
                else
                {
                    for (int q = 0; q < _slotCount[s]; q++)
                    {
                        float shift = (q - (_slotCount[s] - 1) * 0.5f) * 3.2f * scale;
                        Box(b, cx + ux * shift, cz + uz * shift, y, 1.1f * scale, 0.75f * scale, 1.0f * scale, Map25Palette.Rooftop, ux, uz);
                    }
                }
            }
        }

        // True if the point lies inside the convex outline by at least the margin.
        private static bool Inside(float[] x, float[] z, int n, float px, float pz, float margin)
        {
            for (int k = 0; k < n; k++)
            {
                int f = (k + 1) % n;
                float dx = x[f] - x[k], dz = z[f] - z[k];
                float l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-5f)
                {
                    continue;
                }
                if ((dx * (pz - z[k]) - dz * (px - x[k])) / l < margin)
                {
                    return false;
                }
            }
            return true;
        }

        // A box with half sizes along (ux, uz) and across it, its four walls shaded by the way they face, and its top.
        private static void Box(Map25Builder b, float cx, float cz, float y, float halfU, float halfV, float height, Map25Colour colour, float ux, float uz)
        {
            float vx = -uz, vz = ux;
            float ax = cx - ux * halfU - vx * halfV, az = cz - uz * halfU - vz * halfV;
            float bx = cx + ux * halfU - vx * halfV, bz = cz + uz * halfU - vz * halfV;
            float dx = cx + ux * halfU + vx * halfV, dz = cz + uz * halfU + vz * halfV;
            float ex = cx - ux * halfU + vx * halfV, ez = cz - uz * halfU + vz * halfV;
            Face(b, ax, az, bx, bz, y, height, colour, -vx, -vz);
            Face(b, bx, bz, dx, dz, y, height, colour, ux, uz);
            Face(b, dx, dz, ex, ez, y, height, colour, vx, vz);
            Face(b, ex, ez, ax, az, y, height, colour, -ux, -uz);
            Map25Colour top = colour.Scale(1.14f);
            b.Flat(ax, y + height, az, bx, y + height, bz, dx, y + height, dz, ex, y + height, ez, top, 1f);
        }

        private static void Face(Map25Builder b, float ax, float az, float bx, float bz, float y, float height, Map25Colour colour, float nx, float nz)
        {
            Map25Colour c = colour.Scale(Map25Palette.WallFactor(nx, nz));
            b.Strip(ax, az, bx, bz, y, y + height, c.Scale(0.80f), c);
        }

        // A water tank: four legs as one darker post, eight sided body, a flat top and a cone over it.
        private static void Tank(Map25Builder b, float cx, float cz, float y, float r)
        {
            const int sides = 8;
            float legs = r * 0.95f, body = r * 1.3f, cone = r * 0.7f;
            Map25Colour post = Map25Palette.Leg;
            float p = r * 0.45f;
            Face(b, cx - p, cz - p, cx + p, cz - p, y, legs, post, 0f, -1f);
            Face(b, cx + p, cz - p, cx + p, cz + p, y, legs, post, 1f, 0f);
            Face(b, cx + p, cz + p, cx - p, cz + p, y, legs, post, 0f, 1f);
            Face(b, cx - p, cz + p, cx - p, cz - p, y, legs, post, -1f, 0f);
            for (int s = 0; s < sides; s++)
            {
                double a0 = s * 2.0 * Math.PI / sides, a1 = (s + 1) * 2.0 * Math.PI / sides, am = (a0 + a1) * 0.5;
                float ax = cx + (float)Math.Cos(a0) * r, az = cz + (float)Math.Sin(a0) * r;
                float bx = cx + (float)Math.Cos(a1) * r, bz = cz + (float)Math.Sin(a1) * r;
                Face(b, ax, az, bx, bz, y + legs, body, Map25Palette.Tank, (float)Math.Cos(am), (float)Math.Sin(am));
            }
            float yt = y + legs + body;
            int apex = b.Vertex(cx, yt + cone, cz, Map25Palette.Tank.Scale(1.12f));
            for (int s = 0; s < sides; s++)
            {
                double a0 = s * 2.0 * Math.PI / sides, a1 = (s + 1) * 2.0 * Math.PI / sides;
                int i0 = b.Vertex(cx + (float)Math.Cos(a0) * r, yt, cz + (float)Math.Sin(a0) * r, Map25Palette.TankTop);
                int i1 = b.Vertex(cx + (float)Math.Cos(a1) * r, yt, cz + (float)Math.Sin(a1) * r, Map25Palette.TankTop);
                b.Triangle(i0, apex, i1);
            }
        }

        /// <summary>
        /// The contact shadow of building <paramref name="i"/> on the ground: its foot grown a little and swept to the east and south,
        /// in two layers (a wide faint one and a tight stronger one) that read as a soft edge. Written as translucent flat polygons
        /// at <paramref name="groundY"/>.
        /// </summary>
        public void Shadow(Map25Builder b, Map25City city, int i, float groundY)
        {
            int start = city.HullStart[i];
            int n = city.HullStart[i + 1] - start;
            if (n < 3 || n > Most)
            {
                return;
            }
            float h = Math.Max(2f, city.Height[i]);
            float reach = 0.55f * Math.Min(h, 40f);
            for (int k = 0; k < n; k++)
            {
                _x[k] = city.HullX[start + k];
                _z[k] = city.HullZ[start + k];
            }
            Layer(b, n, 1.6f, reach * 1.0f, groundY, 0.14f);
            Layer(b, n, 0.4f, reach * 0.8f, groundY, 0.20f);
        }

        private void Layer(Map25Builder b, int n, float grow, float reach, float y, float alpha)
        {
            Inset(_x, _z, n, -grow, _qx, _qz);
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                _px[m] = _qx[k];
                _pz[m] = _qz[k];
                m++;
                _px[m] = _qx[k] + 0.78f * reach;
                _pz[m] = _qz[k] - 0.62f * reach;
                m++;
            }
            int hull = Hull(_px, _pz, m);
            if (hull < 3)
            {
                return;
            }
            Map25Colour ink = Map25Palette.Ink;
            int first = b.Vertex(_hx[0], y, _hz[0], ink, alpha);
            int prev = b.Vertex(_hx[1], y, _hz[1], ink, alpha);
            for (int k = 2; k < hull; k++)
            {
                int next = b.Vertex(_hx[k], y, _hz[k], ink, alpha);
                b.Triangle(first, prev, next);
                prev = next;
            }
        }

        // The convex hull of the points, anticlockwise, into _hx and _hz; its corner count.
        private int Hull(float[] px, float[] pz, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float kx = px[i], kz = pz[i];
                int j = i - 1;
                while (j >= 0 && (px[j] > kx || (px[j] == kx && pz[j] > kz)))
                {
                    px[j + 1] = px[j];
                    pz[j + 1] = pz[j];
                    j--;
                }
                px[j + 1] = kx;
                pz[j + 1] = kz;
            }
            int m = 0;
            for (int i = 0; i < count; i++)
            {
                while (m >= 2 && Cross(_hx[m - 2], _hz[m - 2], _hx[m - 1], _hz[m - 1], px[i], pz[i]) <= 0f)
                {
                    m--;
                }
                _hx[m] = px[i];
                _hz[m] = pz[i];
                m++;
            }
            int lower = m + 1;
            for (int i = count - 2; i >= 0; i--)
            {
                while (m >= lower && Cross(_hx[m - 2], _hz[m - 2], _hx[m - 1], _hz[m - 1], px[i], pz[i]) <= 0f)
                {
                    m--;
                }
                _hx[m] = px[i];
                _hz[m] = pz[i];
                m++;
            }
            return m - 1;
        }

        private static float Cross(float ax, float az, float bx, float bz, float cx, float cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }
    }
}
