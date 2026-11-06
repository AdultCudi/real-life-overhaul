namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game's own repeatable numbers: an FNV-1a hash of what a draw is about, then xorshift. The same
    /// inputs give the same draws on every machine (the city's developers, the rivals' bids, a project's own luck).
    /// </summary>
    public struct BuildRandom
    {
        private uint _state;

        public BuildRandom(int seed)
        {
            _state = (uint)seed * 2654435761u ^ 0x5BD1E995u;
            if (_state == 0)
            {
                _state = 0x2545F491u;
            }
        }

        /// <summary>A draw seeded by several whole numbers.</summary>
        public static BuildRandom Of(int a, int b, int c)
        {
            return new BuildRandom(Mix(Mix(a, b), c));
        }

        public static int Mix(int a, int b)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)(a >> 16)) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)(b >> 16)) * 16777619u;
                return (int)h;
            }
        }

        /// <summary>FNV-1a of a text, the same on every machine and every run.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)h;
            }
        }

        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>A whole number from <paramref name="least"/> up to, not with, <paramref name="most"/>.</summary>
        public int Range(int least, int most)
        {
            return most <= least ? least : least + (int)(Next() % (uint)(most - least));
        }

        /// <summary>True with a chance in thousandths.</summary>
        public bool Chance(int permille)
        {
            return permille > 0 && (int)(Next() % 1000u) < permille;
        }

        /// <summary>A share from 0 up to 1.</summary>
        public double Value => (Next() & 0xFFFFFF) / 16777216.0;
    }
}
