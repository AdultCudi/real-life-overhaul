namespace RealLifeOverhaul.Tech
{
    /// <summary>The tech sector's dice: each roll a hash of the save's seed and what it is about, so a reload gives the same answer. Game-free.</summary>
    public static class TechRandom
    {
        public static uint Hash(int seed, int a, int b = 0, int c = 0)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)a * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)b * 0xC2B2AE3Du;
                h = (h << 11) | (h >> 21);
                h ^= (uint)c * 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>0 to 999.</summary>
        public static int Mille(int seed, int a, int b = 0, int c = 0)
        {
            return (int)(Hash(seed, a, b, c) % 1000u);
        }

        /// <summary>least to most, both included.</summary>
        public static int Range(int seed, int a, int b, int c, int least, int most)
        {
            if (most <= least)
            {
                return least;
            }
            return least + (int)(Hash(seed, a, b, c) % (uint)(most - least + 1));
        }

        /// <summary>A number of a string (an id), the same every time.</summary>
        public static int Of(string text)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = h * 31 + text[i];
                }
                return h;
            }
        }
    }
}
