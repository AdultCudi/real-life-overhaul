using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a two-wheeler place (a bike-share dock, a bicycle rack, a motorbike stand: <see cref="BikePlaces"/>) may stand,
    /// in plain numbers, so that NavCheck (BikeSpotCheck.cs) checks the very rules it is put down by.
    ///
    /// The pedestrians' navigation mesh, which says where pavement is, runs on over the road wherever people cross it: a
    /// marked crossing is as walkable as the pavement. So the nearest walkable point to a place along a shopfront can be a
    /// crossing at the corner, and one sampled point says nothing of the rest of the rack: half of it can hang over the
    /// kerb into the road, standing at the walkable mesh's height above the asphalt (a player's screenshot, 20 Oct 2026:
    /// a rack and its two bicycles on a crossing, partly in the air). Now the whole footprint, with a margin, is sampled:
    /// every point must be pavement exactly there, the ground under them level to within a kerb's fraction (nothing of it
    /// over a kerb), and no marked crossing under it; the place then stands on the ground at its middle.
    ///
    /// A switch and a few multiplications, asked once a city load for each place tried. Nothing is allocated.
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

        /// <summary>
        /// The i-th point sampled, in the place's own frame (metres across and along from its middle), for a footprint of
        /// these half sizes with the margin round it: the middle first, then the corners, then the sides. The same every time.
        /// </summary>
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

        /// <summary>
        /// The verdict on a place from what was found at its samples: how many were not pavement exactly there, the lowest and
        /// the highest ground under them, and whether a marked crossing lies under the footprint or its margin.
        /// </summary>
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

        /// <summary>Whether a sample is pavement: the walkable mesh found within <see cref="OnMesh"/> of it on the flat (the square of the flat distance given).</summary>
        public static bool OnPavement(bool meshFound, float flatSquare)
        {
            return meshFound && flatSquare <= OnMesh * OnMesh;
        }
    }
}
