using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's design window's rules ("Choose a building"): its filters (the designs that fit the squares picked, the corner
    /// buildings, the city's own designs), what each shows, and the cards' order, cheapest first. Game-free (EconomyCheck's
    /// BuildModeChecks), the same on every machine.
    /// COST: nothing per frame; a pass over the designs (32 at most) when the window is written.
    /// </summary>
    public static class BuildModeCatalogue
    {
        public const int Fits = 0;      // every design that can be built on the squares picked now
        public const int Corners = 1;   // the corner buildings (a corner site's)
        public const int City = 2;      // the city's own designs
        public const int FilterCount = 3;

        /// <summary>The filters' names, each with its {count}, by filter.</summary>
        public static readonly string[] FilterKeys = { "reallife_bld_cat_fits", "reallife_bld_cat_corners", "reallife_bld_cat_city" };

        /// <summary>True when a design is shown under a filter: Fits shows those that can be built; the others show their kind, greyed where it cannot be built.</summary>
        public static bool Shows(int filter, bool corner, bool fits)
        {
            switch (filter)
            {
                case Corners: return corner;
                case City: return !corner;
                default: return fits;
            }
        }

        /// <summary>How many designs a filter shows.</summary>
        public static int Count(int filter, bool[] corner, bool[] fits, int n)
        {
            int c = 0;
            for (int i = 0; i < n && i < corner.Length && i < fits.Length; i++)
            {
                if (Shows(filter, corner[i], fits[i]))
                {
                    c++;
                }
            }
            return c;
        }

        /// <summary>The filter the window opens on: Fits here when anything fits, else the first that shows anything.</summary>
        public static int First(bool[] corner, bool[] fits, int n)
        {
            for (int f = 0; f < FilterCount; f++)
            {
                if (Count(f, corner, fits, n) > 0)
                {
                    return f;
                }
            }
            return Fits;
        }

        /// <summary>The cards' order into <paramref name="order"/>: those that can be built first, then by estimate, cheapest first, then as found (a stable order).</summary>
        public static void Order(long[] costs, bool[] fits, int n, int[] order)
        {
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            for (int i = 1; i < n; i++)   // an insertion sort: 32 designs at most, nothing made
            {
                int at = order[i];
                int k = i - 1;
                while (k >= 0 && Before(at, order[k], costs, fits))
                {
                    order[k + 1] = order[k];
                    k--;
                }
                order[k + 1] = at;
            }
        }

        private static bool Before(int a, int b, long[] costs, bool[] fits)
        {
            if (fits[a] != fits[b])
            {
                return fits[a];
            }
            return costs[a] < costs[b];
        }

        /// <summary>The first warning that stops a design (its key), or null when it can be built.</summary>
        public static string Why(int warnings)
        {
            for (int b = 0; b < BuildModeRules.WarnCount; b++)
            {
                int bit = 1 << b;
                if ((warnings & bit) != 0 && (BuildModeRules.Stops & bit) != 0)
                {
                    return BuildModeRules.WarnKeys[b];
                }
            }
            return null;
        }

        /// <summary>The game's picture for a use's card (none known: the building).</summary>
        public static string Picture(int use)
        {
            switch (use)
            {
                case 0: return "Icon-Residential";
                case 1: return "Icon-Retail";
                case 2: return "Icon-Office";
                case 3: return "Icon-Cargo";
                default: return "Icon-Building";
            }
        }

        /// <summary>The number of designs a list holds at most (the city's asked for and the corner buildings).</summary>
        public const int Most = 40;

        /// <summary>Guards an index into the designs.</summary>
        public static bool Valid(int i, int n) => i >= 0 && i < Math.Min(n, Most);
    }
}
