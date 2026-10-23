using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>A place set out on a plot, in the land's own axes: metres from the land's middle, its front is -Z.</summary>
    public struct PlotSpot
    {
        public float X, Z;
        public float Yaw;     // degrees about Y the thing put there is turned by: its +Z looks that way
        public float ScaleX, ScaleZ;

        public PlotSpot(float x, float z, float yaw, float scaleX = 1f, float scaleZ = 1f)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            ScaleX = scaleX;
            ScaleZ = scaleZ;
        }
    }

    /// <summary>One straight run of the wire fence round a plot, in the land's axes.</summary>
    public struct FenceRun
    {
        public float X0, Z0, X1, Z1;
    }

    /// <summary>
    /// A new building going up on given ground, game-free. The land is given by whoever has it (the middle, which way its front
    /// looks, its size, how many storeys): it is kept in the site's own street field as text, so the save and the line a Going
    /// Public host sends keep their shape. The frame stands on three sides 3.5 m in from the land's edge, for the game's
    /// scaffold; the fourth, at +X, is the yard (the crane's pad, the stacks, the cabin). What stands in each stage, and the
    /// boxes of the frame storeys, the slab and the cladding, drawn as one mesh a stage in the game's own concrete and metal.
    /// </summary>
    public static class PlotRules
    {
        public const string Prefix = "plot:";
        public const float StoreyHeight = 3.6f;   // metres floor to floor: an office storey of the city's
        public const float SlabThick = 0.3f;      // metres
        public const float GroundSlab = 0.35f;    // metres: the slab cast on the ground; its top is the first floor
        public const float ColumnSide = 0.45f;    // metres
        public const float Span = 6.0f;           // metres between columns at most
        public const float Margin = 3.5f;         // metres of land kept round the frame on three sides: the game's scaffold is 3.04 m deep
        public const float Yard = 9.0f;           // metres of land on the +X side: the crane's 6 m pad, the stacks and the cabin
        public const int MinFrame = 8;            // metres a frame is at least each way
        public const int MinWidth = 21;           // metres of land along X at least: margin, frame and yard
        public const int MinDepth = 16;           // metres along Z at least: the yard's cabin, toilet and crane pad in a row
        public const int MostLand = 60;           // metres of land each way at most
        public const int MostStoreys = 10;
        public const float FencePanel = 2.5f;     // metres between the fence's posts
        public const float FenceHeight = 2.0f;    // metres
        public const float Hoarding = 2.4f;       // metres: one hoarding panel (SiteDresser.BayWidth)
        public const float GateWidth = 6.0f;      // metres: the gate in the front hoarding, at the yard
        public const float RailHeight = 1.0f;     // metres: the edge protection round the top slab
        public const float RailEvery = 2.4f;      // metres between its posts
        public const float CladThick = 0.08f;     // metres
        public const float CladLow = 1.0f;        // metres of cladding from a floor up to the window band
        public const float CladHigh = 0.7f;       // metres of cladding under the slab above the band
        public const float EarthSize = 10f;       // metres: the earthworks model is a 10 m square, stretched to the frame
        public const float CranePad = 6f;         // metres: the tower crane's pad
        public const float CraneRing = 40f;       // metres up: the crane's slewing ring, where the jib turns
        public const float StackGap = 3.5f;       // metres between material stacks in the yard

        /// <summary>The land as the site's street field: "plot:24x20:090:5" (width x depth in metres, yaw in degrees, storeys).</summary>
        public static string Encode(int width, int depth, int yaw, int storeys)
        {
            yaw = ((yaw % 360) + 360) % 360;
            return Prefix + width.ToString(CultureInfo.InvariantCulture) + "x" + depth.ToString(CultureInfo.InvariantCulture) + ":"
                   + yaw.ToString("000", CultureInfo.InvariantCulture) + ":" + storeys.ToString(CultureInfo.InvariantCulture);
        }

        public static bool IsPlot(string street) => street != null && street.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>The land back from the street field; false (and a small square) for anything that is not a plot's.</summary>
        public static bool TryRead(string street, out int width, out int depth, out int yaw, out int storeys)
        {
            width = MinWidth;
            depth = MinDepth;
            yaw = 0;
            storeys = 1;
            if (!IsPlot(street))
            {
                return false;
            }
            string[] parts = street.Substring(Prefix.Length).Split(':');
            if (parts.Length != 3)
            {
                return false;
            }
            string[] size = parts[0].Split('x');
            if (size.Length != 2 || !Int(size[0], out int w) || !Int(size[1], out int d) || !Int(parts[1], out int y) || !Int(parts[2], out int s))
            {
                return false;
            }
            width = Clamp(w, MinWidth, MostLand);
            depth = Clamp(d, MinDepth, MostLand);
            yaw = ((y % 360) + 360) % 360;
            storeys = Clamp(s, 1, MostStoreys);
            return w == width && d == depth && s == storeys;
        }

        private static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        /// <summary>Why land of a size cannot take a frame, or null if it can.</summary>
        public static string Unfit(int width, int depth, int storeys)
        {
            if (width < MinWidth || depth < MinDepth)
            {
                return "the land is " + width + " by " + depth + " m; a plot needs " + MinWidth + " by " + MinDepth + " m at least";
            }
            if (width > MostLand || depth > MostLand)
            {
                return "the land is over " + MostLand + " m one way";
            }
            return storeys < 1 || storeys > MostStoreys ? "a plot has 1 to " + MostStoreys + " storeys" : null;
        }

        /// <summary>The frame's footprint in the land's axes.</summary>
        public static void Frame(int width, int depth, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = -width * 0.5f + Margin;
            maxX = width * 0.5f - Yard;
            minZ = -depth * 0.5f + Margin;
            maxZ = depth * 0.5f - Margin;
        }

        /// <summary>Frame storeys standing in a stage: none before the frame stage, then one more for each share of it, all of them after.</summary>
        public static int Standing(int phase, float progress, int storeys)
        {
            if (phase < 3)
            {
                return 0;
            }
            if (phase > 3)
            {
                return storeys;
            }
            float into = (progress - 0.6f) / 0.2f;
            int standing = 1 + (int)Math.Floor(into * storeys);
            return standing < 1 ? 1 : standing > storeys ? storeys : standing;
        }

        /// <summary>A number that changes whenever what stands on a plot changes: the stage, and the storeys of the frame stage.</summary>
        public static int Step(int phase, float progress, int storeys) => phase * 100 + Standing(phase, progress, storeys);

        /// <summary>The progress (0 to 1) at which a plot has one more storey than now, or the next stage begins; for the test button.</summary>
        public static float NextStep(int phase, float progress, int storeys)
        {
            if (phase < 3)
            {
                return 0.6f + 0.1f / storeys;   // the first storey of the frame
            }
            if (phase == 3)
            {
                int standing = Standing(phase, progress, storeys);
                return standing >= storeys ? 0.81f : 0.6f + (standing + 0.5f) / storeys * 0.2f;
            }
            return 1f;
        }

        // Columns along one way of the frame: as few as keep every span within Span, at both ends.
        private static int Columns(float min, float max) => Math.Max(2, (int)Math.Ceiling((max - min - ColumnSide) / Span) + 1);

        private static float Column(float min, float max, int index, int count) => min + ColumnSide * 0.5f + (max - min - ColumnSide) * index / (count - 1);

        /// <summary>
        /// The frame as boxes in the land's axes: concrete columns and slabs for each storey standing, the edge protection round
        /// the top slab and, with <paramref name="clad"/>, panels on every storey under the top one, in metal.
        /// </summary>
        public static void FrameBoxes(int width, int depth, int standing, bool clad, List<ShellBox> concrete, List<ShellBox> metal)
        {
            Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
            int nx = Columns(minX, maxX), nz = Columns(minZ, maxZ);
            float half = ColumnSide * 0.5f;
            concrete.Add(new ShellBox(minX, maxX, 0f, GroundSlab, minZ, maxZ));
            for (int storey = 0; storey < standing; storey++)
            {
                float floor = GroundSlab + storey * StoreyHeight;
                float soffit = floor + StoreyHeight - SlabThick;
                for (int i = 0; i < nx; i++)
                {
                    for (int k = 0; k < nz; k++)
                    {
                        float x = Column(minX, maxX, i, nx), z = Column(minZ, maxZ, k, nz);
                        concrete.Add(new ShellBox(x - half, x + half, floor, soffit, z - half, z + half));
                    }
                }
                concrete.Add(new ShellBox(minX, maxX, soffit, floor + StoreyHeight, minZ, maxZ));
                if (clad && storey < standing - 1)
                {
                    Clad(minX, maxX, minZ, maxZ, floor, CladLow, metal);
                    Clad(minX, maxX, minZ, maxZ, soffit - CladHigh, CladHigh, metal);
                }
            }
            if (standing > 0)
            {
                Rails(minX, maxX, minZ, maxZ, GroundSlab + standing * StoreyHeight, metal);
            }
        }

        /// <summary>The ground slab alone, with the column starter bars standing out of it in metal (the slab stage).</summary>
        public static void SlabBoxes(int width, int depth, List<ShellBox> concrete, List<ShellBox> metal)
        {
            Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
            concrete.Add(new ShellBox(minX, maxX, 0f, GroundSlab, minZ, maxZ));
            int nx = Columns(minX, maxX), nz = Columns(minZ, maxZ);
            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    float x = Column(minX, maxX, i, nx), z = Column(minZ, maxZ, k, nz);
                    for (int bar = 0; bar < 4; bar++)
                    {
                        float bx = x + (bar % 2 == 0 ? -0.15f : 0.15f), bz = z + (bar < 2 ? -0.15f : 0.15f);
                        metal.Add(new ShellBox(bx - 0.02f, bx + 0.02f, GroundSlab, GroundSlab + 1.2f, bz - 0.02f, bz + 0.02f));
                    }
                }
            }
        }

        // A band of cladding round the frame's four faces, standing just proud of the slab edges.
        private static void Clad(float minX, float maxX, float minZ, float maxZ, float from, float height, List<ShellBox> into)
        {
            float to = from + height, t = CladThick;
            into.Add(new ShellBox(minX - t, maxX + t, from, to, minZ - t, minZ));
            into.Add(new ShellBox(minX - t, maxX + t, from, to, maxZ, maxZ + t));
            into.Add(new ShellBox(minX - t, minX, from, to, minZ, maxZ));
            into.Add(new ShellBox(maxX, maxX + t, from, to, minZ, maxZ));
        }

        // Posts every RailEvery round the top slab's edge, a top rail and a knee rail between them. The sides are walked
        // round the slab, each without its last post, so that every corner has one post.
        private static void Rails(float minX, float maxX, float minZ, float maxZ, float top, List<ShellBox> into)
        {
            const float post = 0.05f, rail = 0.04f, inset = 0.1f;
            float x0 = minX + inset, x1 = maxX - inset, z0 = minZ + inset, z1 = maxZ - inset;
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side % 2 == 0;
                float fixedAt = side == 0 ? z0 : side == 1 ? x1 : side == 2 ? z1 : x0;
                float from = side == 0 ? x0 : side == 1 ? z0 : side == 2 ? x1 : z1;
                float to = side == 0 ? x1 : side == 1 ? z1 : side == 2 ? x0 : z0;
                int posts = Math.Max(2, (int)Math.Ceiling(Math.Abs(to - from) / RailEvery) + 1);
                for (int p = 0; p < posts - 1; p++)
                {
                    float at = from + (to - from) * p / (posts - 1);
                    into.Add(alongX ? new ShellBox(at - post, at + post, top, top + RailHeight, fixedAt - post, fixedAt + post)
                        : new ShellBox(fixedAt - post, fixedAt + post, top, top + RailHeight, at - post, at + post));
                }
                float low = Math.Min(from, to), high = Math.Max(from, to);
                for (int r = 0; r < 2; r++)
                {
                    float y = top + (r == 0 ? RailHeight : RailHeight * 0.5f);
                    into.Add(alongX ? new ShellBox(low, high, y - rail, y, fixedAt - rail, fixedAt + rail)
                        : new ShellBox(fixedAt - rail, fixedAt + rail, y - rail, y, low, high));
                }
            }
        }

        /// <summary>The frame as one box for the wrap's layout (WrapRules): to its top rail.</summary>
        public static ShellBox Shell(int width, int depth, int standing)
        {
            Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
            return new ShellBox(minX, maxX, 0f, GroundSlab + Math.Max(1, standing) * StoreyHeight + RailHeight, minZ, maxZ);
        }

        /// <summary>The gate in the front hoarding, at the yard: its stretch along X.</summary>
        public static void Gate(int width, out float from, out float to)
        {
            from = width * 0.5f - Yard + 0.5f;
            to = from + GateWidth;
        }

        /// <summary>The hoarding panels along the land's front (-Z), every 2.4 m, the gate left open; each faces the street.</summary>
        public static void HoardingSpots(int width, int depth, List<PlotSpot> into)
        {
            Gate(width, out float gateFrom, out float gateTo);
            float z = -depth * 0.5f;
            for (float x = -width * 0.5f + Hoarding * 0.5f; x + Hoarding * 0.5f <= width * 0.5f + 0.01f; x += Hoarding)
            {
                if (x + Hoarding * 0.5f > gateFrom && x - Hoarding * 0.5f < gateTo)
                {
                    continue;
                }
                into.Add(new PlotSpot(x, z, 180f));
            }
        }

        /// <summary>The wire fence along the land's other three sides, as straight runs.</summary>
        public static void FenceRuns(int width, int depth, List<FenceRun> into)
        {
            float w = width * 0.5f, d = depth * 0.5f;
            into.Add(new FenceRun { X0 = -w, Z0 = -d, X1 = -w, Z1 = d });
            into.Add(new FenceRun { X0 = -w, Z0 = d, X1 = w, Z1 = d });
            into.Add(new FenceRun { X0 = w, Z0 = d, X1 = w, Z1 = -d });
        }

        /// <summary>The tower crane's pad: at the back of the yard, its jib reaching over the frame.</summary>
        public static PlotSpot CraneSpot(int width, int depth) => new PlotSpot(width * 0.5f - Yard * 0.5f, depth * 0.5f - CranePad * 0.5f - 1f, 270f);

        /// <summary>The site cabin against the yard's fence behind the hoarding, its door to the yard; the toilet beside it.</summary>
        public static void YardSpots(int width, int depth, out PlotSpot cabin, out PlotSpot toilet)
        {
            cabin = new PlotSpot(width * 0.5f - 1.4f, -depth * 0.5f + 3.6f, 270f);
            toilet = new PlotSpot(width * 0.5f - 0.8f, -depth * 0.5f + 7.4f, 270f);
        }

        /// <summary>The material stacks down the yard between the toilet and the crane's pad: as many as fit, at least one.</summary>
        public static void StackSpots(int width, int depth, List<PlotSpot> into)
        {
            float x = width * 0.5f - Yard + 2.2f;
            float from = -depth * 0.5f + 9.5f, to = depth * 0.5f - CranePad - 1.5f;
            int count = 0;
            for (float z = from; z <= to + 0.01f && count < 4; z += StackGap, count++)
            {
                into.Add(new PlotSpot(x, z, count % 2 == 0 ? 0f : 90f));
            }
            if (count == 0)
            {
                into.Add(new PlotSpot(x, (from + to) * 0.5f, 0f));
            }
        }

        /// <summary>The earthworks of the dig: the model's 10 m square stretched over the frame.</summary>
        public static PlotSpot EarthSpot(int width, int depth)
        {
            Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
            return new PlotSpot((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f, 0f, (maxX - minX) / EarthSize, (maxZ - minZ) / EarthSize);
        }

        /// <summary>Where the spoil from the dig is heaped: along the margin behind the frame and down its far side.</summary>
        public static void SpoilSpots(int width, int depth, List<PlotSpot> into)
        {
            Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
            float back = maxZ + Margin * 0.5f, left = minX - Margin * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                into.Add(new PlotSpot(minX + (maxX - minX) * (i + 0.5f) / 3f, back, i * 70f));
                into.Add(new PlotSpot(left, minZ + (maxZ - minZ) * (i + 0.5f) / 3f, 35f + i * 50f));
            }
        }

        /// <summary>A point of the land in the world: the land's middle and turn, both from the site's record.</summary>
        public static void ToWorld(float originX, float originZ, int yaw, float x, float z, out float worldX, out float worldZ)
        {
            double a = yaw * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            worldX = originX + cos * x + sin * z;
            worldZ = originZ - sin * x + cos * z;
        }
    }
}
