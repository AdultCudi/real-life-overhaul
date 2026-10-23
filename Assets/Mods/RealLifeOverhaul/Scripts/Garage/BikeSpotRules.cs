using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a two-wheeler place (<see cref="BikePlaces"/>) may stand, in plain numbers so that NavCheck checks them. The
    /// walkers' mesh runs on over crossings, so the whole footprint is sampled: all pavement, level, no crossing under
    /// it. A few multiplications a place tried, once a city load.
    /// </summary>
    public static class BikeSpotRules
    {
        public const float Margin = 0.35f;        // m beyond the footprint on every side that must be pavement too: clear of the kerb's edge
        public const float CrossingMargin = 0.6f; // m round the footprint in which no marked crossing may be
        public const float OnMesh = 0.12f;        // m: a sample is pavement if the walkable mesh is this near it, flat
        public const float LevelMost = 0.06f;     // m between the highest and lowest ground under it: a kerb is 0.12 m or more
        public const int Samples = 9;             // the middle, the four corners and the four sides' middles

        /// <summary>Why a place was refused, or None.</summary>
        public enum Refusal { None, OffPavement, OverKerb, Crossing }

        /// <summary>The i-th sample in the place's frame (metres from its middle): the middle, the corners, then the sides.</summary>
        public static void Sample(int i, float halfAcross, float halfAlong, out float across, out float along)
        {
            if (i < 0 || i >= Samples)
            {
                throw new ArgumentOutOfRangeException(nameof(i));
            }
            float x = halfAcross + Margin, z = halfAlong + Margin;
            switch (i)
            {
                case 0: across = 0f; along = 0f; break;
                case 1: across = x; along = z; break;
                case 2: across = -x; along = z; break;
                case 3: across = x; along = -z; break;
                case 4: across = -x; along = -z; break;
                case 5: across = x; along = 0f; break;
                case 6: across = -x; along = 0f; break;
                case 7: across = 0f; along = z; break;
                default: across = 0f; along = -z; break;
            }
        }

        /// <summary>The verdict from the samples: how many were off the pavement, the lowest and highest ground, any crossing under it.</summary>
        public static Refusal Judge(int offPavement, float lowest, float highest, bool crossing)
        {
            if (crossing)
            {
                return Refusal.Crossing;
            }
            if (offPavement > 0)
            {
                return Refusal.OffPavement;
            }
            return highest - lowest > LevelMost ? Refusal.OverKerb : Refusal.None;
        }

        /// <summary>Whether a sample is pavement: the walkable mesh within <see cref="OnMesh"/> of it on the flat (given squared).</summary>
        public static bool OnPavement(bool meshFound, float flatSquare)
        {
            return meshFound && flatSquare <= OnMesh * OnMesh;
        }
    }
}
