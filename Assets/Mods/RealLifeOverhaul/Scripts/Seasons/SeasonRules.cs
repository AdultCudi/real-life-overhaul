using System;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>One day's snow, from the save's number and the day alone, so every machine of a session has the same.</summary>
    public struct SnowDay
    {
        public bool Falls;
        public int From;     // the hour it starts
        public int Hours;    // how long it falls
        public bool Heavy;   // a heavy fall whitens everything; a light one leaves the ground patchy

        public int To => From + Hours;
    }

    /// <summary>
    /// The seasons, game-free (checked by EconomyCheck): the season of a day as the calendar has it (Core/CalendarRules,
    /// the year beginning in midwinter), what customers buy more or less of in each, and the snow of a winter day: when it
    /// falls, how hard, and how much lies on the ground hour by hour. Nothing is random but <see cref="Roll"/>, which is
    /// the save's number and the day; nothing is kept, so nothing is saved.
    /// </summary>
    public static class SeasonRules
    {
        public const int Thousand = 1000;
        public const int Seasons = 4;                 // winter, spring, summer, autumn, as CalendarRules counts them
        public const int DemandLeast = 600;           // thousandths: no season takes more than this off a good's sales
        public const int DemandMost = 1500;           // thousandths: nor puts more than this on
        public const int YearLeast = 970;             // thousandths: a good's four seasons average out near its own sales,
        public const int YearMost = 1060;             //   so a year of seasons costs or gives a shop next to nothing

        public const int SnowChance = 300;            // thousandths of a winter day
        public const int HeavyChance = 350;           // thousandths of a snow day
        public const int LatestStart = 20;            // the last hour a fall may begin
        public const int LeastHours = 2;
        public const int MostHours = 9;
        public const float RiseHours = 2.5f;          // hours a fall takes to lay all it will
        public const float LightCover = 0.55f;        // the most a light fall lays
        public const float MeltHours = 16f;           // hours from midnight that snow lying from the day before takes to go
        public const float RampHours = 0.4f;          // hours a fall takes to get going and to die away
        public const float PatchyFrom = 0.2f;         // cover from which the roads count as snowy
        public const float WhiteFrom = 0.6f;          // cover from which they count as covered
        public const int ForecastDays = 7;            // days ahead the calendar knows the snow of

        public const int PurposeSnow = 41, PurposeFrom = 43, PurposeHours = 47, PurposeHeavy = 53;

        /// <summary>The goods whose sales follow the season, by the game's item names.</summary>
        public static readonly string[] Goods =
        {
            "ba:itemname_cupofcoffee", "ba:itemname_cupoftea", "ba:itemname_icecream", "ba:itemname_slushi", "ba:itemname_sodacan",
            "ba:itemname_energydrink", "ba:itemname_beer", "ba:itemname_bottleofwine", "ba:itemname_whisky", "ba:itemname_martini",
            "ba:itemname_margarita", "ba:itemname_cheapgift", "ba:itemname_expensivegift", "ba:itemname_cheapjewelry", "ba:itemname_expensivejewelry",
            "ba:itemname_cheapflower", "ba:itemname_expensiveflower", "ba:itemname_salad", "ba:itemname_apple", "ba:itemname_pear",
            "ba:itemname_banana", "ba:itemname_lettuce", "ba:itemname_tomato", "ba:itemname_carrot", "ba:itemname_hotdog",
            "ba:itemname_kabob", "ba:itemname_pizza", "ba:itemname_frozenfood", "ba:itemname_popcorn", "ba:itemname_cottoncandy",
            "ba:itemname_umbrella", "ba:itemname_classiccheapmaleclothing", "ba:itemname_classiccheapfemaleclothing", "ba:itemname_classicexpensivemaleclothing",
            "ba:itemname_classicexpensivefemaleclothing", "ba:itemname_moderncheapmaleclothing", "ba:itemname_moderncheapfemaleclothing",
            "ba:itemname_modernexpensivemaleclothing", "ba:itemname_modernexpensivefemaleclothing", "ba:itemname_smartphone1", "ba:itemname_smartphone2",
            "ba:itemname_smartwatch1", "ba:itemname_smartwatch2", "ba:itemname_earbuds01", "ba:itemname_headphones01", "ba:itemname_novel",
            "ba:itemname_youngnovel", "ba:itemname_picturebook", "ba:itemname_limitededitionbook"
        };

        // Thousandths of a good's own sales in winter, spring, summer and autumn, in the order of Goods.
        private static readonly int[][] Demand =
        {
            new[] { 1150, 1000, 900, 1050 },   // coffee: hot drinks in the cold
            new[] { 1200, 1000, 850, 1100 },   // tea
            new[] { 700, 1050, 1350, 900 },    // ice cream
            new[] { 700, 1050, 1300, 950 },    // slushies
            new[] { 900, 1000, 1150, 950 },    // cans of soda
            new[] { 950, 1000, 1100, 1000 },   // energy drinks
            new[] { 950, 1000, 1150, 1000 },   // beer: the summer's drink
            new[] { 1150, 1000, 900, 1050 },   // wine: the winter's
            new[] { 1200, 950, 850, 1050 },    // whisky
            new[] { 1100, 1000, 950, 1000 },   // martinis: parties at the year's end
            new[] { 900, 1000, 1200, 950 },    // margaritas
            new[] { 1200, 950, 900, 1050 },    // gifts: the holidays
            new[] { 1250, 950, 900, 1050 },
            new[] { 1150, 1000, 950, 1000 },   // jewellery
            new[] { 1200, 1000, 950, 1000 },
            new[] { 1150, 1200, 950, 850 },    // flowers: the winter's celebrations and the spring
            new[] { 1200, 1150, 950, 850 },
            new[] { 850, 1050, 1200, 950 },    // salads
            new[] { 900, 950, 1050, 1150 },    // apples and pears: the autumn's harvest
            new[] { 900, 950, 1050, 1150 },
            new[] { 950, 1000, 1100, 1000 },   // bananas
            new[] { 900, 1050, 1150, 950 },    // lettuce
            new[] { 850, 1000, 1200, 1000 },   // tomatoes
            new[] { 1050, 1000, 900, 1050 },   // carrots: soups
            new[] { 950, 1000, 1150, 1000 },   // hot dogs: the summer's street food
            new[] { 900, 1000, 1200, 950 },    // kebabs: barbecues
            new[] { 1100, 1000, 950, 1000 },   // pizza: nights in
            new[] { 1050, 1000, 1050, 950 },   // frozen food: nights in, and the freezer in the heat
            new[] { 1100, 1000, 900, 1050 },   // popcorn: the cinema when it is cold
            new[] { 900, 1000, 1200, 950 },    // cotton candy: summer fairs
            new[] { 950, 1150, 800, 1200 },    // umbrellas: the wet seasons
            new[] { 1100, 1000, 850, 1100 },   // clothes: coats in the cold, little in the heat
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1100, 1000, 850, 1100 },
            new[] { 1150, 950, 950, 1000 },    // phones, watches and headphones: the holidays' presents
            new[] { 1150, 950, 950, 1000 },
            new[] { 1150, 950, 950, 1000 },
            new[] { 1150, 950, 950, 1000 },
            new[] { 1150, 950, 950, 1000 },
            new[] { 1150, 950, 950, 1000 },
            new[] { 1100, 950, 1050, 950 },    // books: long evenings, and the beach
            new[] { 1100, 950, 1050, 950 },
            new[] { 1150, 950, 1000, 950 },
            new[] { 1150, 950, 950, 1000 }
        };

        public static int GoodCount => Goods.Length;

        /// <summary>True when these seasons stand down for Alcware - Seasons: it is switched on and the player has not asked for both.</summary>
        public static bool StandsDown(bool alcwareLoaded, bool keptBeside)
        {
            return alcwareLoaded && !keptBeside;
        }

        /// <summary>The season of a day: CalendarRules.Winter, Spring, Summer or Autumn.</summary>
        public static int SeasonOf(int day, int year)
        {
            return CalendarRules.Season(day, year);
        }

        /// <summary>
        /// The day whose customers a moment belongs to. The game draws a day's customers at midnight with the figures it has
        /// then, so from the last hour of a day on the next day's season is what counts.
        /// </summary>
        public static int DrawDay(int day, int hour)
        {
            return hour >= 23 ? day + 1 : day;
        }

        /// <summary>The first day of the season a day is in (back to day 1 at most).</summary>
        public static int SeasonStart(int day, int year)
        {
            int season = SeasonOf(day, year);
            int d = Math.Max(1, day);
            int least = Math.Max(1, d - Math.Max(1, year));
            while (d > least && SeasonOf(d - 1, year) == season)
            {
                d--;
            }
            return d;
        }

        /// <summary>True on the first day of a season, day 1 being one.</summary>
        public static bool BeginsOn(int day, int year)
        {
            return day <= 1 || SeasonOf(day, year) != SeasonOf(day - 1, year);
        }

        /// <summary>The place of an item in Goods, or -1 for one whose sales do not follow the season.</summary>
        public static int IndexOf(string item)
        {
            if (string.IsNullOrEmpty(item))
            {
                return -1;
            }
            for (int i = 0; i < Goods.Length; i++)
            {
                if (string.Equals(Goods[i], item, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Thousandths of a good's own sales in a season; 1000 for a good or a season out of range.</summary>
        public static int DemandOf(int good, int season)
        {
            if (good < 0 || good >= Demand.Length || season < 0 || season >= Seasons)
            {
                return Thousand;
            }
            return Math.Max(DemandLeast, Math.Min(DemandMost, Demand[good][season]));
        }

        /// <summary>The game's sales ratio for a good this season: its own (0 meaning 1, as the game reads it) times the season's.</summary>
        public static float Ratio(float own, int good, int season)
        {
            return (own == 0f ? 1f : own) * DemandOf(good, season) / (float)Thousand;
        }

        /// <summary>
        /// The good in place <paramref name="rank"/> (from 0) of those that sell more in a season (or less), the largest
        /// change first and ties in the table's order; -1 past the last that moves that way. Chosen without a list.
        /// </summary>
        public static int Ranked(int season, int rank, bool rises)
        {
            for (int i = 0; i < Goods.Length; i++)
            {
                int mine = Change(i, season, rises);
                if (mine <= 0)
                {
                    continue;
                }
                int above = 0;
                for (int k = 0; k < Goods.Length; k++)
                {
                    int theirs = Change(k, season, rises);
                    if (theirs > mine || (theirs == mine && k < i))
                    {
                        above++;
                    }
                }
                if (above == rank)
                {
                    return i;
                }
            }
            return -1;
        }

        // How far a good moves in a season the way asked, in thousandths; 0 or less if it does not move that way.
        private static int Change(int good, int season, bool rises)
        {
            int d = DemandOf(good, season) - Thousand;
            return rises ? d : -d;
        }

        /// <summary>A figure in thousandths as a change in whole per cent: "+15%", "-30%", "0%".</summary>
        public static string Change(int thousandths)
        {
            int percent = (thousandths - Thousand + (thousandths >= Thousand ? 5 : -5)) / 10;
            return (percent > 0 ? "+" : string.Empty) + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>A number from 0 to range less one that is always the same for this save's number, a day and a purpose.</summary>
        public static int Roll(int salt, int day, int purpose, int range)
        {
            unchecked
            {
                uint hash = (uint)(day * 73856093) ^ (uint)(purpose * 19349663) ^ (uint)(salt * 83492791);
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return range <= 1 ? 0 : (int)(hash % (uint)range);
            }
        }

        /// <summary>The snow of a day: none outside winter, else by the rolls.</summary>
        public static SnowDay SnowOn(int day, int year, int salt)
        {
            var snow = new SnowDay();
            if (day < 1 || SeasonOf(day, year) != CalendarRules.Winter || Roll(salt, day, PurposeSnow, Thousand) >= SnowChance)
            {
                return snow;
            }
            snow.Falls = true;
            snow.From = Roll(salt, day, PurposeFrom, LatestStart + 1);
            snow.Hours = LeastHours + Roll(salt, day, PurposeHours, Math.Min(MostHours, 24 - snow.From) - LeastHours + 1);   // over by midnight
            snow.Heavy = Roll(salt, day, PurposeHeavy, Thousand) < HeavyChance;
            return snow;
        }

        /// <summary>How hard it snows at an hour of the day (0 to 24, with the minutes as a fraction): 0, up to 0.5 light, up to 1 heavy.</summary>
        public static float Falling(SnowDay snow, float hour)
        {
            if (!snow.Falls || hour <= snow.From || hour >= snow.To)
            {
                return 0f;
            }
            float ramp = Math.Min(1f, Math.Min(hour - snow.From, snow.To - hour) / RampHours);
            return (snow.Heavy ? 1f : 0.5f) * ramp;
        }

        /// <summary>The most a day's fall lays.</summary>
        public static float Peak(SnowDay snow)
        {
            return !snow.Falls ? 0f : snow.Heavy ? 1f : LightCover;
        }

        /// <summary>What a day's own fall has laid by an hour: a fall shorter than RiseHours lays less than its most.</summary>
        public static float Laid(SnowDay snow, float hour)
        {
            if (!snow.Falls || hour <= snow.From)
            {
                return 0f;
            }
            float fallen = Math.Min(hour, snow.To) - snow.From;
            return Peak(snow) * Math.Min(1f, fallen / RiseHours);
        }

        /// <summary>
        /// How much snow lies at an hour of a day, 0 to 1: what the day's own fall has laid, on top of what lay at the end of
        /// the day before melting away from midnight over <see cref="MeltHours"/>. Snow lies until midnight once it has
        /// fallen, so only the day before matters.
        /// </summary>
        public static float Cover(int day, float hour, int year, int salt)
        {
            hour = Math.Max(0f, Math.Min(24f, hour));
            SnowDay yesterday = SnowOn(day - 1, year, salt);
            float carried = Laid(yesterday, 24f) * Math.Max(0f, 1f - hour / MeltHours);
            return Math.Min(1f, carried + Laid(SnowOn(day, year, salt), hour));
        }

        /// <summary>The roads as driving feels them: 0 clear, 1 snowy in patches, 2 covered.</summary>
        public static int Step(float cover)
        {
            return cover >= WhiteFrom ? 2 : cover >= PatchyFrom ? 1 : 0;
        }

        /// <summary>The first snow day from <paramref name="from"/> to <paramref name="to"/>, or 0 for none.</summary>
        public static int NextSnow(int from, int to, int year, int salt)
        {
            for (int day = Math.Max(1, from); day <= to; day++)
            {
                if (SnowOn(day, year, salt).Falls)
                {
                    return day;
                }
            }
            return 0;
        }

        // HDRP 14 keeps a renderer's decal layers in bits 8 to 15 of its rendering layer mask; bit 8 is the default
        // layer, which the game's own decals and every car and person (0x101) are on.
        public const int DecalBitsFrom = 8;
        public const int DecalBitsTo = 15;

        /// <summary>
        /// The rendering layer bit the lying snow takes: of bits 9 to 15, the one the fewest renderers that are not ground
        /// carry (<paramref name="holders"/>, by bit), the highest on a tie; never the default decal layer.
        /// </summary>
        public static int SnowBit(int[] holders)
        {
            int best = DecalBitsTo;
            for (int b = DecalBitsTo - 1; b > DecalBitsFrom; b--)
            {
                if (Held(holders, b) < Held(holders, best))
                {
                    best = b;
                }
            }
            return best;
        }

        private static int Held(int[] holders, int bit)
        {
            return holders != null && bit < holders.Length ? holders[bit] : 0;
        }
    }
}
