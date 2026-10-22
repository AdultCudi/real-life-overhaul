using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Nav
{
    // The minimap's and the GPS's game-free parts, compiled by NavCheck as they are and checked on made-up roads; the
    // route itself is in NavRoute.cs.

    /// <summary>What a route asks of whoever follows it at one place.</summary>
    public enum NavTurnKind { None, KeepLeft, KeepRight, Left, Right, UTurn, Arrive }

    /// <summary>One turn of a found route.</summary>
    public struct NavTurn
    {
        public float At;            // metres along the route where the junction begins
        public float Angle;         // degrees, left positive
        public float InX, InZ;      // the heading before it
        public NavTurnKind Kind;
        public int Road;            // the road turned onto (the graph's own number), -1 unknown
    }

    /// <summary>What <see cref="NavTracker.Tick"/> decided.</summary>
    public enum NavEvent { None, Reroute, Arrived }

    /// <summary>The turns' angles and kinds, the distances as said, and the game's lane point names.</summary>
    public static class NavMath
    {
        public const float StraightBelow = 28f;   // degrees: a change of heading under this is "carry on", nothing is said
        public const float TurnFrom = 55f;        // degrees: from here it is a turn; under it, "keep left/right"
        public const float UTurnFrom = 150f;      // degrees

        /// <summary>Degrees from one flat direction to another, left (anticlockwise seen from above) positive, -180..180.</summary>
        public static float SignedAngle(float ax, float az, float bx, float bz)
        {
            float cross = ax * bz - az * bx;
            float dot = ax * bx + az * bz;
            if (cross == 0f && dot == 0f)
            {
                return 0f;
            }
            return (float)(Math.Atan2(cross, dot) * 180.0 / Math.PI);
        }

        public static NavTurnKind Classify(float degrees)
        {
            float size = Math.Abs(degrees);
            if (size < StraightBelow)
            {
                return NavTurnKind.None;
            }
            if (size >= UTurnFrom)
            {
                return NavTurnKind.UTurn;
            }
            if (size < TurnFrom)
            {
                return degrees > 0f ? NavTurnKind.KeepLeft : NavTurnKind.KeepRight;
            }
            return degrees > 0f ? NavTurnKind.Left : NavTurnKind.Right;
        }

        /// <summary>A distance as a satnav says it: to 10 m under 200 m, to 50 m under a kilometre, to 100 m above; never 0.</summary>
        public static int RoundMetres(float metres)
        {
            int step = metres < 200f ? 10 : metres < 1000f ? 50 : 100;
            int rounded = (int)Math.Round(metres / step) * step;
            return rounded < 10 ? 10 : rounded;
        }

        /// <summary>The road number in a lane point's name ("Road_25-Lane_3-Waypoint_7" is 25); -1 if the name is not of that form.</summary>
        public static int RoadOf(string name)
        {
            if (name == null || name.Length < 6 || name[0] != 'R' || name[1] != 'o' || name[2] != 'a' || name[3] != 'd' || name[4] != '_')
            {
                return -1;
            }
            int number = 0;
            int i = 5;
            for (; i < name.Length && name[i] >= '0' && name[i] <= '9' && i < 14; i++)
            {
                number = number * 10 + (name[i] - '0');
            }
            return i > 5 ? number : -1;
        }

        /// <summary>True for a lane point that joins two roads at a junction (its name says "Connect").</summary>
        public static bool IsConnector(string name)
        {
            return name != null && name.IndexOf("Connect", StringComparison.Ordinal) >= 0;
        }
    }

    /// <summary>
    /// The lane points in a grid of square cells, for "which are near this place": built once by a counting sort into two
    /// arrays; a question looks at the few cells its circle touches.
    /// </summary>
    public sealed class NavIndex
    {
        private readonly float[] _x;
        private readonly float[] _z;
        private readonly float _cell;
        private readonly float _minX;
        private readonly float _minZ;
        private readonly int _columns;
        private readonly int _rows;
        private readonly int[] _cellStart;
        private readonly int[] _points;

        public float MinX => _minX;
        public float MinZ => _minZ;
        public float MaxX { get; }
        public float MaxZ { get; }
        public int Indexed => _points.Length;

        public NavIndex(float[] x, float[] z, bool[] usable, int count, float cell = 32f)
        {
            _x = x;
            _z = z;
            _cell = cell;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                if (!usable[i])
                {
                    continue;
                }
                used++;
                minX = Math.Min(minX, x[i]);
                maxX = Math.Max(maxX, x[i]);
                minZ = Math.Min(minZ, z[i]);
                maxZ = Math.Max(maxZ, z[i]);
            }
            if (used == 0)
            {
                minX = minZ = maxX = maxZ = 0f;
            }
            _minX = minX;
            _minZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            _columns = Math.Max(1, (int)((maxX - minX) / cell) + 1);
            _rows = Math.Max(1, (int)((maxZ - minZ) / cell) + 1);
            _cellStart = new int[_columns * _rows + 1];
            _points = new int[used];
            for (int i = 0; i < count; i++)
            {
                if (usable[i])
                {
                    _cellStart[CellOf(x[i], z[i]) + 1]++;
                }
            }
            for (int c = 0; c < _columns * _rows; c++)
            {
                _cellStart[c + 1] += _cellStart[c];
            }
            var next = new int[_columns * _rows];
            for (int i = 0; i < count; i++)
            {
                if (usable[i])
                {
                    int c = CellOf(x[i], z[i]);
                    _points[_cellStart[c] + next[c]++] = i;
                }
            }
        }

        private int CellOf(float x, float z)
        {
            int column = Clamp((int)((x - _minX) / _cell), 0, _columns - 1);
            int row = Clamp((int)((z - _minZ) / _cell), 0, _rows - 1);
            return row * _columns + column;
        }

        private static int Clamp(int value, int low, int high)
        {
            return value < low ? low : value > high ? high : value;
        }

        /// <summary>The point nearest a place (flat distance) within a radius, or -1.</summary>
        public int Nearest(float x, float z, float within)
        {
            int best = -1;
            float bestSqr = within * within;
            int c0 = Clamp((int)((x - within - _minX) / _cell), 0, _columns - 1);
            int c1 = Clamp((int)((x + within - _minX) / _cell), 0, _columns - 1);
            int r0 = Clamp((int)((z - within - _minZ) / _cell), 0, _rows - 1);
            int r1 = Clamp((int)((z + within - _minZ) / _cell), 0, _rows - 1);
            for (int row = r0; row <= r1; row++)
            {
                for (int column = c0; column <= c1; column++)
                {
                    int cell = row * _columns + column;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int point = _points[k];
                        float dx = _x[point] - x;
                        float dz = _z[point] - z;
                        float sqr = dx * dx + dz * dz;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            best = point;
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>Adds every point within a radius of a place (flat distance) to a list.</summary>
        public void Within(float x, float z, float radius, List<int> into)
        {
            float limit = radius * radius;
            int c0 = Clamp((int)((x - radius - _minX) / _cell), 0, _columns - 1);
            int c1 = Clamp((int)((x + radius - _minX) / _cell), 0, _columns - 1);
            int r0 = Clamp((int)((z - radius - _minZ) / _cell), 0, _rows - 1);
            int r1 = Clamp((int)((z + radius - _minZ) / _cell), 0, _rows - 1);
            for (int row = r0; row <= r1; row++)
            {
                for (int column = c0; column <= c1; column++)
                {
                    int cell = row * _columns + column;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int point = _points[k];
                        float dx = _x[point] - x;
                        float dz = _z[point] - z;
                        if (dx * dx + dz * dz <= limit)
                        {
                            into.Add(point);
                        }
                    }
                }
            }
        }
    }

    /// <summary>The road graph's edges made over for walking.</summary>
    public static class NavEdges
    {
        /// <summary>The same roads on foot: every edge both ways, with no charge for changing lane.</summary>
        public static void TwoWay(int count, int[] start, int[] to, out int[] start2, out int[] to2, out float[] extra2)
        {
            start2 = new int[count + 1];
            for (int i = 0; i < count; i++)
            {
                for (int e = start[i]; e < start[i + 1]; e++)
                {
                    start2[i + 1]++;
                    start2[to[e] + 1]++;
                }
            }
            for (int i = 0; i < count; i++)
            {
                start2[i + 1] += start2[i];
            }
            to2 = new int[start2[count]];
            extra2 = new float[start2[count]];
            var next = new int[count];
            for (int i = 0; i < count; i++)
            {
                for (int e = start[i]; e < start[i + 1]; e++)
                {
                    int other = to[e];
                    to2[start2[i] + next[i]++] = other;
                    to2[start2[other] + next[other]++] = i;
                }
            }
        }
    }

    /// <summary>The minimap's view: how much of the city it shows, and which part of the map picture that is.</summary>
    public static class NavView
    {
        public const float BaseMetres = 260f;     // metres across the view at 100% zoom, standing or walking
        public const float WidenFrom = 8f;        // metres a second: faster than this the view begins to widen
        public const float WidenOver = 20f;       // metres a second more than WidenFrom at which it is widest
        public const float WidestBy = 0.9f;       // share more it shows at its widest

        /// <summary>Metres across the view for an option value (per cent: more is closer) and a speed.</summary>
        public static float MetresAcross(int zoomPercent, float speed)
        {
            float zoom = zoomPercent < 25 ? 25f : zoomPercent > 400 ? 400f : zoomPercent;
            float fast = (speed - WidenFrom) / WidenOver;
            fast = fast < 0f ? 0f : fast > 1f ? 1f : fast;
            return BaseMetres * 100f / zoom * (1f + WidestBy * fast * fast * (3f - 2f * fast));
        }

        /// <summary>Moves a value towards another by a share of the gap per second; lands on it when within half a per cent.</summary>
        public static float Ease(float current, float target, float dt, float rate)
        {
            float gap = target - current;
            if (Math.Abs(gap) <= Math.Abs(target) * 0.005f)
            {
                return target;
            }
            float share = 1f - (float)Math.Exp(-rate * dt);
            return current + gap * share;
        }

        /// <summary>
        /// The part of the map picture, as texture coordinates (corner and size), that a square of <paramref name="metres"/>
        /// a side around a place is.
        /// </summary>
        public static void Window(float x, float z, float originX, float originZ, float worldWidth, float worldHeight, float metres,
                                  out float u, out float v, out float width, out float height)
        {
            width = metres / worldWidth;
            height = metres / worldHeight;
            u = (x - originX) / worldWidth - width * 0.5f;
            v = (z - originZ) / worldHeight - height * 0.5f;
        }

        /// <summary>
        /// Where a place is in the view, in its units from the middle, right and up positive, for a view centred on (px, pz)
        /// and turned by <paramref name="turn"/> degrees clockwise from north.
        /// </summary>
        public static void ToView(float wx, float wz, float px, float pz, float turn, float unitsPerMetre, out float vx, out float vy)
        {
            float dx = (wx - px) * unitsPerMetre;
            float dz = (wz - pz) * unitsPerMetre;
            double radians = turn * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            vx = dx * cos - dz * sin;
            vy = dx * sin + dz * cos;
        }
    }

    /// <summary>
    /// The road picture as numbers: every lane drawn as a soft-edged band, a byte of road cover a pixel. It touches only the
    /// arrays it is given, so it runs off the main thread.
    /// </summary>
    public static class NavRaster
    {
        /// <summary>
        /// The picture's size for a piece of world, and its pixels a metre: as many as fit in <paramref name="longest"/>
        /// pixels, at most <paramref name="mostPerMetre"/>.
        /// </summary>
        public static void Size(float worldWidth, float worldHeight, int longest, float mostPerMetre, out int width, out int height, out float perMetre)
        {
            float span = Math.Max(1f, Math.Max(worldWidth, worldHeight));
            perMetre = Math.Min(mostPerMetre, longest / span);
            width = Math.Max(4, (int)Math.Ceiling(worldWidth * perMetre));
            height = Math.Max(4, (int)Math.Ceiling(worldHeight * perMetre));
        }

        /// <summary>Draws every edge that costs nothing extra (a lane, not a change of lane) between two usable points.</summary>
        public static int Draw(byte[] pixels, int width, int height, float originX, float originZ, float perMetre, float halfWidthMetres,
                               float[] x, float[] z, int[] edgeStart, int[] edgeTo, float[] edgeExtra, bool[] usable, int count)
        {
            float radius = Math.Max(0.75f, halfWidthMetres * perMetre);
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (!usable[i])
                {
                    continue;
                }
                float ax = (x[i] - originX) * perMetre;
                float az = (z[i] - originZ) * perMetre;
                for (int e = edgeStart[i]; e < edgeStart[i + 1]; e++)
                {
                    int other = edgeTo[e];
                    if (edgeExtra[e] > 0f || !usable[other])
                    {
                        continue;
                    }
                    Band(pixels, width, height, ax, az, (x[other] - originX) * perMetre, (z[other] - originZ) * perMetre, radius);
                    drawn++;
                }
            }
            return drawn;
        }

        // One band: every pixel within the radius of the segment is road; the last pixel of the edge fades out.
        private static void Band(byte[] pixels, int width, int height, float ax, float az, float bx, float bz, float radius)
        {
            float reach = radius + 1f;
            int x0 = (int)Math.Floor(Math.Min(ax, bx) - reach);
            int x1 = (int)Math.Ceiling(Math.Max(ax, bx) + reach);
            int y0 = (int)Math.Floor(Math.Min(az, bz) - reach);
            int y1 = (int)Math.Ceiling(Math.Max(az, bz) + reach);
            // The outermost pixels stay empty, so that looking past the edge shows nothing.
            x0 = x0 < 1 ? 1 : x0;
            y0 = y0 < 1 ? 1 : y0;
            x1 = x1 > width - 2 ? width - 2 : x1;
            y1 = y1 > height - 2 ? height - 2 : y1;
            float dx = bx - ax;
            float dz = bz - az;
            float lengthSqr = dx * dx + dz * dz;
            for (int py = y0; py <= y1; py++)
            {
                int row = py * width;
                float cz = py + 0.5f - az;
                for (int px = x0; px <= x1; px++)
                {
                    float cx = px + 0.5f - ax;
                    float t = lengthSqr > 0.0001f ? (cx * dx + cz * dz) / lengthSqr : 0f;
                    t = t < 0f ? 0f : t > 1f ? 1f : t;
                    float ox = cx - dx * t;
                    float oz = cz - dz * t;
                    float cover = radius + 0.5f - (float)Math.Sqrt(ox * ox + oz * oz);
                    if (cover <= 0f)
                    {
                        continue;
                    }
                    byte value = cover >= 1f ? (byte)255 : (byte)(cover * 255f);
                    if (value > pixels[row + px])
                    {
                        pixels[row + px] = value;
                    }
                }
            }
        }
    }
}
