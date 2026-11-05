using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The city's buildings as the map keeps them: each a convex outline seen from above (anticlockwise, x east, z north, in
    /// metres), the height of its foot above the scene's ground plane, the height it is drawn at (<see cref="Height"/>, from the game's
    /// own by <see cref="Map25Height"/>), and the address it is known by (0 for none, which only the mod's own buildings and the fixture have).
    /// The tint is the game's highlight on it, written by the director while the map is open. Game-free; the game's measure of it is Navigation/NavBase.
    /// </summary>
    public sealed class Map25City
    {
        public int Count;
        public int[] HullStart = new int[1];
        public float[] HullX = new float[0], HullZ = new float[0];
        public float[] BaseY = new float[0], Height = new float[0], Real = new float[0];
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
            Real = new float[count];
            Key = new long[count];
            Tint = new Map25Colour[count];
            TintShare = new float[count];
        }

        /// <summary>
        /// The city from the arrays the minimap keeps (every outline anticlockwise from <paramref name="start"/>, the street's height beside
        /// each and its own): the scene's ground plane is the median street, so a block's foot is its street's difference from it.
        /// The heights are drawn by <see cref="Map25Height"/>, whatever the game's are.
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
            city.Compress();
            city.FindBounds();
            return city;
        }

        /// <summary>Keeps each building's height as the game measured it in <see cref="Real"/> and sets <see cref="Height"/> to the height it is drawn at.</summary>
        public void Compress()
        {
            var scratchX = new float[32];
            var scratchZ = new float[32];
            Array.Copy(Height, Real, Count);
            for (int i = 0; i < Count; i++)
            {
                Map25Height.Measure(this, i, scratchX, scratchZ, out float shorter, out float longer);
                Height[i] = Map25Height.Drawn(Real[i], shorter, longer);
            }
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
        public float Band;   // metres of height one window band stands for (the minimap's, which has no windows)
        public bool Fine;    // windows, awnings, lit windows, cornices

        public static Map25Detail Full => new Map25Detail { Band = 7.2f, Fine = true };
        public static Map25Detail Small => new Map25Detail { Band = 21.6f, Fine = false };
    }

    /// <summary>
    /// The blocks, every detail of the approved mockups (make_map25d.py) as colour in the vertices and a few triangles: the
    /// walls shaded by the way they face with a darker foot (in metres), a heavier podium and a lighter crown, a cornice, a parapet rim on
    /// the roof, a setback on a wide tower, a water tank on legs, a row of air conditioners and a stair hut on some roofs; and, in the full
    /// detail, a translucent pane a window, awnings on the north and south walls of shops, a few lit windows on towers. A building's
    /// draws are fixed by its address alone: the tint and the detail change colours and counts of nothing else, so a tint is a write of
    /// colours into the same vertices. One instance a thread (it keeps its scratch arrays).
    /// </summary>
    public sealed partial class Map25Blocks
    {
        private const float RoofShade = 0.66f, RimShade = 1.0f;
        private const int Most = 32;                 // corners of an outline we take
        private const float Floor = 3.0f;            // metres of the shop's ground floor band
        private const float WindowShare = 0.16f;     // how far a minimap band goes to the window colour
        private const float TierFrom = 21f;          // metres: a setback only on a block at least this tall
        private const float TierWide = 26f;          // and on a lot at least this wide: below it the tier would not read
        private const float TierStep = 0.22f;        // of the shorter side, the setback's inset
        private const float TierHeight = 0.42f;      // of the block's height, at the most 0.45 of the shorter side

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
        private float _paneScale = 1f;

        /// <summary>
        /// Draws building <paramref name="i"/> of the city into the builders, solid and shaded. With <paramref name="panes"/> false the panes are
        /// left out (a tint rewrites colours of the blocks, crowns and fronts and the panes do not change with it).
        /// </summary>
        public void Generate(Map25Builders t, Map25City city, int i, Map25Detail detail, bool panes)
        {
            int start = city.HullStart[i];
            int n = city.HullStart[i + 1] - start;
            if (n < 3 || n > Most)
            {
                return;
            }
            float y0 = city.BaseY[i];
            float h = Math.Max(Map25Height.Least, city.Height[i]);
            long key = city.Key.Length > i ? city.Key[i] : 0L;
            uint seed = key != 0L ? (uint)(key ^ (key >> 32)) : (uint)i * 2654435761u + 97u;
            Decide(seed, h, n);
            for (int k = 0; k < n; k++)
            {
                _x[k] = city.HullX[start + k];
                _z[k] = city.HullZ[start + k];
            }
            Map25Colour tone = Map25Palette.Tone(_tone).Scale(_vary);
            Map25Colour paneTone = tone;   // a pane is translucent over whatever wall is under it, tinted or not
            // The game's highlight is the full map's: the minimap has no pointer and shows none, so its blocks stay as they are whatever the city holds (the set is built once, from a city
            // that may have tints in it, and is never written again).
            float share = detail.Fine && city.TintShare.Length > i ? city.TintShare[i] : 0f;
            bool tinted = share > 0f;
            Map25Colour tint = tinted ? city.Tint[i] : default;
            if (tinted)
            {
                tone = Map25Palette.Tinted(tone, tint, share);
            }
            bool drawPanes = panes && detail.Fine;
            _paneScale = drawPanes ? PaneScale(n, h) : 1f;
            var awningRng = new Map25Rng(seed ^ 0xA5A5A5A5u);
            for (int e = 0; e < n; e++)
            {
                int f = (e + 1) % n;
                Wall(t, _x[e], _z[e], _x[f], _z[f], y0, h, tone, paneTone, detail, e, tint, share, ref awningRng, drawPanes);
            }
            Extents(_x, _z, n, out float ux, out float uz, out float u0, out float u1, out float v0, out float v1);
            float width = Math.Min(u1 - u0, v1 - v0);
            float scale = Math.Max(0.45f, Math.Min(1.1f, width / 24f));
            float rim = Math.Min(0.9f, width * 0.18f);
            float top = y0 + h;
            Map25Colour roof = tone.Scale(RoofShade);   // the approved shots' roofs are the darkest part of a block, with a lit parapet round them
            Roof(t.Blocks, _x, _z, n, top, roof, tone.Scale(RimShade), roof.Scale(0.92f), rim, width);
            float[] rx = _x, rz = _z;
            if (h >= TierFrom && width >= TierWide)
            {
                float step = width * TierStep;
                Inset(_x, _z, n, step, _qx, _qz);
                float h2 = Math.Min(h * TierHeight, width * 0.45f);
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
                    t.Crowns.Strip(_qx[e], _qz[e], _qx[f], _qz[f], top, top + h2, c.Scale(0.70f), c);
                }
                top += h2;
                Map25Colour roof2 = tier.Scale(RoofShade);
                Roof(t.Crowns, _qx, _qz, n, top, roof2, tier.Scale(RimShade), roof2.Scale(0.92f), Math.Min(0.6f, rim), width * 0.5f);
                rx = _qx;
                rz = _qz;
                Extents(rx, rz, n, out ux, out uz, out u0, out u1, out v0, out v1);
                scale *= 0.8f;
            }
            if (_props)
            {
                Props(t.Blocks, rx, rz, n, top, tone, ux, uz, u0, u1, v0, v1, scale);
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

        private void Wall(Map25Builders t, float ax, float az, float bx, float bz, float y0, float h, Map25Colour tone, Map25Colour paneTone, Map25Detail detail,
                          int edge, Map25Colour tint, float share, ref Map25Rng awningRng, bool panes)
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
            Map25Builder b = t.Blocks;
            float body = h >= 22f ? h - 1.6f : h;
            float ground = _shop ? Math.Min(Floor, h * 0.5f) : 0f;
            if (ground > 0f)
            {
                Piece(b, ax, az, bx, bz, y0, h, 0f, ground, wall.Scale(0.74f));
            }
            if (detail.Fine)
            {
                Body(b, ax, az, bx, bz, y0, h, ground, body, wall);
            }
            else
            {
                Bands(b, ax, az, bx, bz, y0, h, ground, body, wall, wall.Mix(Map25Palette.Window, WindowShare), detail.Band);
            }
            if (h >= 22f && detail.Fine)
            {
                Piece(b, ax, az, bx, bz, y0, h, body, body + 0.5f, wall.Scale(0.84f));
                Piece(b, ax, az, bx, bz, y0, h, body + 0.5f, h, wall.Scale(1.16f));
            }
            else if (h >= 22f)
            {
                Piece(b, ax, az, bx, bz, y0, h, body, h, wall.Scale(1.10f));
            }
            if (panes)
            {
                Panes(t.Panes, ax, az, ux, uz, nx, nz, length, y0, h, paneTone.Scale(factor), edge);
            }
            if (_shop && detail.Fine && (nz > 0.75f || nz < -0.75f) && length >= 9f)
            {
                Awnings(t.Fronts, ax, az, ux, uz, nx, nz, length, y0, tint, share, paneTone, factor, ref awningRng);
            }
        }

        // The body of a tall wall between its shop base and its cornice: a heavier podium to a third of the way up, a lighter crown over the top quarter, a hard step between.
        private static void Body(Map25Builder b, float ax, float az, float bx, float bz, float y0, float h, float from, float to, Map25Colour wall)
        {
            float lo = from;
            if (h >= 20f)
            {
                float podium = h * 0.30f, crown = h * 0.74f;
                if (podium > lo && podium < to)
                {
                    Piece(b, ax, az, bx, bz, y0, h, lo, podium, wall);
                    lo = podium;
                }
                if (crown > lo && crown < to)
                {
                    Piece(b, ax, az, bx, bz, y0, h, lo, crown, wall);
                    lo = crown;
                }
            }
            Piece(b, ax, az, bx, bz, y0, h, lo, to, wall);
        }

        // The minimap's walls: a band of glass colour every floor group, so a wall is not one slab at a pixel a metre.
        private static void Bands(Map25Builder b, float ax, float az, float bx, float bz, float y0, float h, float from, float to, Map25Colour wall, Map25Colour glass, float floor)
        {
            float window = floor * 0.30f;
            float cursor = from;
            float at = from;
            while (at + floor * 0.5f < to)
            {
                float ws = at + (floor - window) * 0.5f, we = ws + window;
                if (we > to)
                {
                    break;
                }
                Piece(b, ax, az, bx, bz, y0, h, cursor, ws, wall);
                Piece(b, ax, az, bx, bz, y0, h, ws, we, glass);
                cursor = we;
                at += floor;
            }
            Piece(b, ax, az, bx, bz, y0, h, cursor, to, wall);
        }

        // One band of a wall between two heights, as dark at its foot as the wall is there: shaded in metres from the ground, so a tall wall's foot is no taller than a low one's.
        private static void Piece(Map25Builder b, float ax, float az, float bx, float bz, float y0, float h, float za, float zb, Map25Colour colour)
        {
            if (zb - za < 0.01f)
            {
                return;
            }
            colour = Podium(colour, (za + zb) * 0.5f, h);
            float lo = za;
            while (lo < zb - 0.001f)
            {
                float hi = zb;
                if (lo < Map25Palette.FootKnee && hi > Map25Palette.FootKnee)
                {
                    hi = Map25Palette.FootKnee;
                }
                else if (lo < Map25Palette.FootEnd && hi > Map25Palette.FootEnd)
                {
                    hi = Map25Palette.FootEnd;
                }
                b.Strip(ax, az, bx, bz, y0 + lo, y0 + hi, colour.Scale(1f - Map25Palette.Foot(lo)), colour.Scale(1f - Map25Palette.Foot(hi)));
                lo = hi;
            }
        }

        // A taller building is heavier at its foot and a little lighter at its crown (a band of the wall, by where its middle is).
        private static Map25Colour Podium(Map25Colour colour, float middle, float h)
        {
            if (h < 20f)
            {
                return colour;
            }
            if (middle < h * 0.30f)
            {
                return colour.Scale(0.90f);
            }
            return middle > h * 0.74f ? colour.Mix(new Map25Colour(1f, 1f, 1f), 0.035f) : colour;
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
    }
}
