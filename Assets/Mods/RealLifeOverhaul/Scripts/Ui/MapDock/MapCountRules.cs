using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of the game's map filters shows, by the data the game gives it (CityMapFilterData): the predicate its work puts each building through.</summary>
    public enum MapLineKind : byte
    {
        None = 0,        // a line with no places of its own to count: Hide closed, the subway stations, the jobs
        BuildingType,    // the units of a building type that are to let
        BusinessType,    // the buildings that hold a kind of business
        Neighbourhood,   // a district: its zone, counted by the buildings in it
        Address,         // one place by its address (the activities)
        Rival,           // the places a rival runs
        Rented,          // what the player rents or owns
        ForSale          // what stands for sale and is not the player's
    }

    /// <summary>One line of the game's filters as the counts see it: its kind and the key of the kind (a type, a district, an address, a rival).</summary>
    public struct MapLineSpec
    {
        public MapLineKind Kind;
        public string Key;
    }

    /// <summary>One building of the city as the counts see it: the facts the game's own filter work reads.</summary>
    public struct MapBuildingFact
    {
        public string BuildingType, BusinessType, Neighbourhood, Address, RivalId;
        public bool ForRent, Rented, Owned, ForSale;
    }

    /// <summary>
    /// How many places each line of the map's filters shows, by the same tests the game's own filter work puts each building through
    /// (CityMapFilters.ApplyFilters(controller)): a type of building to let, a kind of business, a district, an address, a rival's places, what the player rents or
    /// owns, what is for sale. Rivals' and the city's own businesses are buildings like the player's, so they count the same. One pass over the buildings, once when the
    /// map opens and when a registration changes, never a frame. Game-free.
    /// </summary>
    public static class MapCountRules
    {
        /// <summary>The count of every line, in step with <paramref name="lines"/>; -1 for a line that has no places to count.</summary>
        public static void Count(IList<MapLineSpec> lines, IList<MapBuildingFact> buildings, int[] into)
        {
            var byType = new Dictionary<string, int>(StringComparer.Ordinal);
            var byBusiness = new Dictionary<string, int>(StringComparer.Ordinal);
            var byDistrict = new Dictionary<string, int>(StringComparer.Ordinal);
            var byAddress = new Dictionary<string, int>(StringComparer.Ordinal);
            var byRival = new Dictionary<string, int>(StringComparer.Ordinal);
            int rented = 0, forSale = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                MapBuildingFact b = buildings[i];
                if (b.ForRent)
                {
                    Add(byType, b.BuildingType);
                }
                Add(byBusiness, b.BusinessType);
                Add(byDistrict, b.Neighbourhood);
                Add(byAddress, b.Address);
                Add(byRival, b.RivalId);
                rented += b.Rented || b.Owned ? 1 : 0;
                forSale += b.ForSale && !b.Owned ? 1 : 0;
            }
            for (int i = 0; i < lines.Count && i < into.Length; i++)
            {
                MapLineSpec line = lines[i];
                switch (line.Kind)
                {
                    case MapLineKind.BuildingType: into[i] = Get(byType, line.Key); break;
                    case MapLineKind.BusinessType: into[i] = Get(byBusiness, line.Key); break;
                    case MapLineKind.Neighbourhood: into[i] = Get(byDistrict, line.Key); break;
                    case MapLineKind.Address: into[i] = Get(byAddress, line.Key); break;
                    case MapLineKind.Rival: into[i] = Get(byRival, line.Key); break;
                    case MapLineKind.Rented: into[i] = rented; break;
                    case MapLineKind.ForSale: into[i] = forSale; break;
                    default: into[i] = -1; break;
                }
            }
        }

        private static void Add(Dictionary<string, int> into, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            into.TryGetValue(key, out int n);
            into[key] = n + 1;
        }

        private static int Get(Dictionary<string, int> from, string key) => key != null && from.TryGetValue(key, out int n) ? n : 0;

        /// <summary>The lines on and the lines there are, for "11 of 31 on": only the lines the map can show now are counted.</summary>
        public static void Tally(IList<bool> on, IList<bool> available, out int lit, out int total)
        {
            lit = total = 0;
            for (int i = 0; i < on.Count && i < available.Count; i++)
            {
                if (available[i])
                {
                    total++;
                    lit += on[i] ? 1 : 0;
                }
            }
        }
    }

    /// <summary>The quick chips under the map's search.</summary>
    public enum MapChipKind : byte
    {
        Food = 0,
        Groceries,
        Fuel,
        Jobs,
        ForRent,
        Yours,
        Count
    }

    /// <summary>
    /// Which of the map's lines each quick chip switches, by each line's own key and its group's: a chip is offered only when some line is its (the owner: "only
    /// chips whose lines exist"), is lit while every one of its lines is on, and a press turns all of them on, or all of them off when they all were. Food, Groceries
    /// and Fuel are kinds of business, named by the game's own keys (ba:businesstype_coffeeshop), so a business kind a game update adds joins by its name.
    /// Game-free.
    /// </summary>
    public static class MapChipRules
    {
        private static readonly string[][] Words =
        {
            new[] { "restaurant", "coffee", "bakery", "pizza", "cafe", "diner", "burger", "icecream", "sandwich", "foodstand", "hotdog" },   // Food
            new[] { "supermarket", "grocery", "fruitandvegetable", "liquor", "butcher", "convenience", "deli" },                               // Groceries
            new[] { "gasstation", "fuelstation", "petrol" },                                                                                   // Fuel
        };

        public const string JobsGroup = "citymap_category_jobs";
        public const string RentGroup = "citymap_category_rentable";
        public const string YoursLine = "buildingresume_rented_by_you";

        /// <summary>True when a line is one a chip names.</summary>
        public static bool Matches(MapChipKind chip, string lineKey, string groupKey)
        {
            if (string.IsNullOrEmpty(lineKey))
            {
                return false;
            }
            switch (chip)
            {
                case MapChipKind.Jobs: return groupKey == JobsGroup;
                case MapChipKind.ForRent: return groupKey == RentGroup;
                case MapChipKind.Yours: return lineKey == YoursLine;
            }
            string[] words = Words[(int)chip];
            string key = lineKey.ToLowerInvariant();
            for (int i = 0; i < words.Length; i++)
            {
                if (key.IndexOf(words[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The indices of the lines a chip names, among lines given by their keys, groups and whether the map can show them now. Empty when it names none, so the chip is
        /// not offered.
        /// </summary>
        public static int Lines(MapChipKind chip, IList<string> keys, IList<string> groups, IList<bool> available, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < keys.Count && i < groups.Count && i < available.Count; i++)
            {
                if (available[i] && Matches(chip, keys[i], groups[i]))
                {
                    into.Add(i);
                }
            }
            return into.Count;
        }

        /// <summary>A chip is lit while every line it names is on.</summary>
        public static bool Lit(IList<int> lines, IList<bool> on)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!on[lines[i]])
                {
                    return false;
                }
            }
            return lines.Count > 0;
        }

        /// <summary>What a press sets every one of a chip's lines to: on, or off when they were all on.</summary>
        public static bool Target(IList<int> lines, IList<bool> on) => !Lit(lines, on);
    }

    /// <summary>What the place card shows that is a sum on data the game and the mod hold: the price tier, the rating's words, the busy hours, a review's line.</summary>
    public static class MapPlaceRules
    {
        public const int ReviewLines = 2;         // lines for each stars bucket and reason: one is picked by the resident and the day, nothing is saved
        private const string Mod = "reallife";    // the start of every key; a review's key is made of parts put after it, so the text check reads only whole keys
        public const int TierLow = 900, TierHigh = 1100;   // thousandths of the city's typical price: under the first is "$", over the second "$$$"

        /// <summary>The price tier from a business's prices against the district's standard, in thousandths (1000 is the standard): 1, 2 or 3; 0 when there is none.</summary>
        public static int Tier(int price)
        {
            if (price <= 0)
            {
                return 0;
            }
            return price < TierLow ? 1 : price > TierHigh ? 3 : 2;
        }

        /// <summary>A rating in tenths of a star as its figure: 43 is "4.3"; none for 0 (not rated yet).</summary>
        public static string Rating(int tenths) => tenths <= 0 ? null
            : (tenths / 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A rating in tenths as halves of a star for the kit's stars: 43 is 9 (four and a half), rounded to the nearest half.</summary>
        public static int Halves(int tenths) => tenths <= 0 ? 0 : Math.Max(1, Math.Min(10, (tenths + 2) / 5));

        /// <summary>
        /// The typical shape of a business's day, 0 to 100 an hour from its first open hour: the game's own hourly multipliers (the same the customers' entries are
        /// drawn by) over the hours it is open, the day's busiest hour 100. <paramref name="multiplier"/> is the game's factor for an hour of the day.
        /// </summary>
        public static int[] Busy(Func<int, float> multiplier, int open, int close, out int from)
        {
            from = Math.Max(0, Math.Min(23, open));
            int to = Math.Max(from + 1, Math.Min(24, close <= open ? 24 : close));
            int n = Math.Min(24, to - from);
            var values = new int[n];
            float most = 0f;
            for (int i = 0; i < n; i++)
            {
                most = Math.Max(most, multiplier(from + i));
            }
            for (int i = 0; i < n; i++)
            {
                values[i] = most > 0f ? (int)Math.Round(multiplier(from + i) / most * 100f) : 0;
            }
            return values;
        }

        /// <summary>The index of the hour now in the drawn hours, or -1 when it is not one of them.</summary>
        public static int Now(int hour, int from, int count) => hour >= from && hour < from + count ? hour - from : -1;

        /// <summary>Busier than usual: today's arrivals past the usual by an eighth and more, once there is a usual to go by.</summary>
        public static bool Busier(int today, int usual) => usual >= 4 && today * 8 >= usual * 9;

        /// <summary>
        /// A review's text key: its stars' bucket (1 and 2 stars low, 3 middle, 4 and 5 high), the reason the simulation has for the business now (the weakest of price,
        /// quality, wait and cleanliness, or mixed) and one of <see cref="ReviewLines"/> lines picked by the resident and the day, so the same review always reads the
        /// same and nothing is saved. Reasons are those of FlareReviewWhy: 0 mixed, 1 price, 2 quality, 3 wait, 4 clean.
        /// </summary>
        public static string ReviewKey(int stars, int why, int resident, int day)
        {
            string bucket = stars <= 2 ? "low" : stars == 3 ? "mid" : "high";
            string reason = why == 1 ? "price" : why == 2 ? "quality" : why == 3 ? "wait" : why == 4 ? "clean" : "mixed";
            uint h = 2166136261u;
            h = (h ^ (uint)resident) * 16777619u;
            h = (h ^ (uint)day) * 16777619u;
            return Mod + "_map_review_" + bucket + "_" + reason + "_" + (int)(h % ReviewLines);
        }

        /// <summary>The ages of a review as the card writes them: today, yesterday, or so many days.</summary>
        public static int DaysAgo(int today, int day) => Math.Max(0, today - day);
    }

    /// <summary>One way to the picked place with its minutes and its cost, for the route strip.</summary>
    public enum MapRouteMode : byte
    {
        Walk = 0,
        Taxi,
        Subway,
        Car
    }

    /// <summary>
    /// The route strip's figures in plain numbers: the minutes each way takes from the road metres of the walking and driving graphs, the cab's quote and the subway's.
    /// The graphs give metres; a way takes the time its pace needs, and the minutes are of the game's clock as the minimap's are. The taxi is the City Cabs call (its
    /// call-out and a taxi's pace, with the wait for the cab to come), the subway is the walk to a station, the ride, and the walk from the one nearest the place. Game-free.
    /// </summary>
    public static class MapRouteRules
    {
        public const float WalkPace = 1.4f;        // metres a second on foot, the game's own walk
        public const float CarPace = 8f;           // metres a second over a street with its junctions and lights (a taxi's pace, TaxiRules.Pace)
        public const float SubwayPace = 12f;       // metres a second in the tunnel, between stations, in the straight line
        public const float SubwayWait = 120f;      // seconds on the platform
        public const float CabWait = 240f;         // seconds for the cab to come, in the middle of its start distances (TaxiRules.StartLeast and StartMost)
        public const float DetourWalk = 1.0f;      // the walking graph's metres are the road's
        public const float StraightDetour = 1.3f;  // a straight line to a road distance, where no graph is built

        /// <summary>Minutes of the game's clock for so many metres at a pace in metres a second.</summary>
        public static int Minutes(float metres, float pace, float extraSeconds, float minutesPerSecond)
        {
            if (!(metres >= 0f) || !(pace > 0f))
            {
                return 0;
            }
            return Math.Max(1, (int)Math.Ceiling((metres / pace + Math.Max(0f, extraSeconds)) * Math.Max(0.0001f, minutesPerSecond) - 0.001));
        }

        /// <summary>The subway's seconds: the walks at both ends, the wait and the ride in a straight line between the two stations.</summary>
        public static float SubwaySeconds(float walkTo, float ride, float walkFrom) => walkTo / WalkPace + SubwayWait + ride / SubwayPace + walkFrom / WalkPace;

        /// <summary>Whether the subway is worth offering: the two walks to and from its stations come to under six tenths of the road between the places, else the street is the better way.</summary>
        public static bool SubwayOffered(float walkTo, float walkFrom, float road) => walkTo + walkFrom < road * 0.6f;
    }
}
