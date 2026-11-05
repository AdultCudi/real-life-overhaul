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

        // ------------------------------------------------------------------ the kit's tabs, search, favourites and the wanted badge

        public const int WantedLeast = 55;   // a use is WANTED where its demand is the district's highest and at least this (the demand bars' "wants more" starts at DemandHigh)
        public const int TabAll = -1;        // the tab that shows every design of the use in hand

        /// <summary>The use the district wants most (0 to 3), or -1 where none stands out: demand known, highest, and at least <see cref="WantedLeast"/>; a tie goes to the first.</summary>
        public static int Wanted(int[] demand, int n)
        {
            int best = -1, value = WantedLeast - 1;
            for (int u = 0; u < n && u < demand.Length; u++)
            {
                if (demand[u] > value)
                {
                    value = demand[u];
                    best = u;
                }
            }
            return best;
        }

        /// <summary>Weeks a build takes, from its days: whole weeks, at least one.</summary>
        public static int Weeks(int days) => Math.Max(1, (Math.Max(0, days) + 6) / 7);

        /// <summary>True when a design's name holds every word of the search (case does not count); an empty search holds all.</summary>
        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            string[] words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (name.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A design's id for the favourites: its use and its name, or the corner building's model.</summary>
        public static string FavouriteId(int use, string look, string name) => use.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (string.IsNullOrEmpty(look) ? name : look);

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

        // ------------------------------------------------------------------ the window's size and the cards' grid

        public const float CardGap = 36f;       // between two cards, in the window's units (the help screen's 2400 by 1600)
        public const float CardMin = 420f;      // the narrowest a card is drawn: its name, kind, figures and price each on a line of its own
        public const int ColumnsMost = 3;       // cards side by side at most
        public const float WideShare = 0.9f;    // of the canvas's width the window may take
        public const float HighShare = 0.745f;  // of the canvas's height, where the toolbar is not measured: room for the game's top bar and the toolbar
        public const float ScaleLeast = 0.4f;   // the smallest the window is drawn at

        /// <summary>How many cards stand side by side on a page of this width: three where each is at least <see cref="CardMin"/> wide, else fewer, one at least.</summary>
        public static int Columns(float pageWide)
        {
            int n = (int)Math.Floor((pageWide - 8f + CardGap) / (CardMin + CardGap));   // the 8 is the slack CardWide leaves
            return Math.Max(1, Math.Min(ColumnsMost, n));
        }

        /// <summary>A card's width on a page of this width with so many to a row (a little under the page's, so the scrollbar never touches one): the same for every card.</summary>
        public static float CardWide(float pageWide, int columns)
        {
            int n = Math.Max(1, columns);
            return (float)Math.Floor((pageWide - CardGap * (n - 1) - 8f) / n);
        }

        /// <summary>
        /// What the window is scaled by so it fits the screen (the owner, 27 Oct 2026: it ran over the footer and the toolbar). The canvas
        /// is 2160 units high at every resolution and shrinks to 2160 divided by the game's UI scale, so at a larger UI scale the window,
        /// 2400 by 1600 units, no longer fits: it is drawn smaller, by the canvas's width and by the height free between the game's top
        /// bar and the toolbar (<paramref name="freeHigh"/>), never above 1 and never below <see cref="ScaleLeast"/>.
        /// </summary>
        public static float Scale(float canvasWide, float freeHigh, float wide, float high)
        {
            if (wide <= 0f || high <= 0f || canvasWide <= 0f || freeHigh <= 0f)
            {
                return 1f;
            }
            float s = Math.Min(1f, Math.Min(canvasWide * WideShare / wide, freeHigh / high));
            return Math.Max(ScaleLeast, s);
        }

        /// <summary>The number of designs a list holds at most (the city's asked for and the corner buildings).</summary>
        public const int Most = 40;

        /// <summary>Guards an index into the designs.</summary>
        public static bool Valid(int i, int n) => i >= 0 && i < Math.Min(n, Most);
    }
}
