using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// How tall a block is drawn, from how tall the building is and how wide its ground is. The game's real heights run from a shed to a
    /// tower of well over a hundred metres on a lot ten metres wide, and a block that stands that high on a thin lot is a needle that hides the
    /// streets round it. The approved mockups draw blocks about as tall as they are wide (21 to 36 m, a few to 50 m), so a height is kept as it
    /// is up to a knee that grows with the shorter side of the ground, and above it only 15 per cent of what is left over is drawn, up to a cap that
    /// also keeps a small square lot from being a chimney. A low building is exactly as tall as it is; a tower is still the tallest thing in
    /// its street. Game-free; EconomyCheck pins every number.
    /// </summary>
    public static class Map25Height
    {
        public const float KneeBase = 8f;         // metres: the knee of a lot with no width
        public const float KneePerSide = 1.4f;    // metres more of knee for each metre of the shorter side
        public const float Above = 0.15f;         // the share of the height above the knee that is drawn
        public const float CapBase = 6f;
        public const float CapPerSide = 3.4f;     // the tallest a block is drawn: this and the shorter side, metres
        public const float CapLongBase = 10f;
        public const float CapPerLong = 2.0f;     // and never more than this and the longer side (a 10 by 12 m lot is no 42 m chimney, a 10 by 30 m one is a slab)
        public const float Least = 2f;            // metres: no block is lower than a kerb and a door

        /// <summary>Full height up to here: 8 m and 1.4 m for each metre of the lot's shorter side (22 m at 10 m, 29 m at 15 m, 43 m at 25 m).</summary>
        public static float Knee(float shorter)
        {
            return KneeBase + KneePerSide * (shorter < 0f ? 0f : shorter);
        }

        /// <summary>
        /// The tallest a block is drawn: 6 m and 3.4 m for each metre of the shorter side (23 m at 5 m, 40 m at 10 m, 57 m at 15 m, 91 m at 25 m),
        /// and 10 m and 2 m for each of the longer.
        /// </summary>
        public static float Cap(float shorter, float longer)
        {
            float bySide = CapBase + CapPerSide * (shorter < 0f ? 0f : shorter);
            float byLong = CapLongBase + CapPerLong * (longer < shorter ? shorter : longer);
            return Math.Min(bySide, byLong);
        }

        /// <summary>The height a block is drawn at: <paramref name="height"/> metres on a lot of <paramref name="shorter"/> by <paramref name="longer"/> metres.</summary>
        public static float Drawn(float height, float shorter, float longer)
        {
            float h = height < Least ? Least : height;
            float knee = Knee(shorter);
            if (h > knee)
            {
                h = knee + (h - knee) * Above;
            }
            float cap = Cap(shorter, longer);
            return h > cap ? cap : h;
        }

        /// <summary>The sides of building <paramref name="i"/>'s outline, as <see cref="Map25Blocks.Extents"/> measures it (along and across its longest side).</summary>
        public static void Measure(Map25City city, int i, float[] scratchX, float[] scratchZ, out float shorter, out float longer)
        {
            int start = city.HullStart[i], n = city.HullStart[i + 1] - start;
            if (n < 3 || n > scratchX.Length)
            {
                shorter = longer = 10f;
                return;
            }
            for (int k = 0; k < n; k++)
            {
                scratchX[k] = city.HullX[start + k];
                scratchZ[k] = city.HullZ[start + k];
            }
            Map25Blocks.Extents(scratchX, scratchZ, n, out _, out _, out float u0, out float u1, out float v0, out float v1);
            shorter = Math.Min(u1 - u0, v1 - v0);
            longer = Math.Max(u1 - u0, v1 - v0);
        }
    }
}
