namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's chance, the same on every machine: every draw is a hash of the city's seed, what it is for, who it is of and
    /// the day (so a Going Public guest that generates from the host's seed has the host's people, and a day lived twice comes out the
    /// same). Game-free. COST: a few multiplications a draw, nothing allocated.
    /// </summary>
    public static class PopDraw
    {
        // What a draw is for: each its own number, so no two draws of one person on one day are alike.
        public const int Vacant = 1, Class = 2, Size = 3, Kind = 4, Age = 5, Sex = 6, Education = 7, Status = 8, Field = 9, Skill = 10,
            Savings = 11, Debt = 12, Car = 13, Taste = 14, Owner = 15, Mortgage = 16, Part = 17, Partner = 18, Child = 19, Death = 20,
            Couple = 21, Split = 22, Leave = 23, Quit = 24, Find = 25, Move = 26, Migrate = 27, Birth = 28, Flat = 29, Link = 30,
            University = 31, Retire = 32, Commuter = 33, Unemployed = 34, Work = 35, Choice = 36, Games = 37, Priced = 38,
            Older = 39, Mover = 40;

        /// <summary>A whole number that is always the same for these four.</summary>
        public static uint Hash(int seed, int purpose, int who, int when)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)purpose * 2246822519u ^ (uint)who * 3266489917u ^ (uint)when * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>0 to just under <paramref name="range"/>.</summary>
        public static int Range(int seed, int purpose, int who, int when, int range)
        {
            return range <= 1 ? 0 : (int)(Hash(seed, purpose, who, when) % (uint)range);
        }

        /// <summary>A whole number between two, both included.</summary>
        public static int Between(int seed, int purpose, int who, int when, int least, int most)
        {
            return most <= least ? least : least + Range(seed, purpose, who, when, most - least + 1);
        }

        /// <summary>True with a chance of so many in a thousand.</summary>
        public static bool Mille(int seed, int purpose, int who, int when, int perMille)
        {
            return perMille > 0 && (perMille >= 1000 || Range(seed, purpose, who, when, 1000) < perMille);
        }

        /// <summary>True on a day with the chance a rate of so many in a thousand a year gives one day of a year of so many days.</summary>
        public static bool Daily(int seed, int purpose, int who, int day, int perMilleYear, int yearDays)
        {
            if (perMilleYear <= 0)
            {
                return false;
            }
            long perMillion = (long)perMilleYear * 1000 / (yearDays < 1 ? 1 : yearDays);
            return perMillion >= 1000000 || Range(seed, purpose, who, day, 1000000) < perMillion;
        }

        /// <summary>The place in a table of weights a draw falls in (the weights need not add to anything); -1 when they are all nothing.</summary>
        public static int Pick(int seed, int purpose, int who, int when, int[] weights)
        {
            long sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i] > 0 ? weights[i] : 0;
            }
            if (sum <= 0)
            {
                return -1;
            }
            long roll = (long)(Hash(seed, purpose, who, when) % (ulong)sum);
            for (int i = 0; i < weights.Length; i++)
            {
                long w = weights[i] > 0 ? weights[i] : 0;
                if (roll < w)
                {
                    return i;
                }
                roll -= w;
            }
            return weights.Length - 1;
        }
    }
}
