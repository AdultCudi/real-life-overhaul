using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Districts;
using RealLifeOverhaul.Ui;

// The rules behind the city map's panels, checked game-free (Scripts/Ui/Map/MapRules.cs, MapSaved.cs, MapPresses.cs, Districts/DistrictMonths.cs): the counts of places
// on each line, the quick chips, the place card's price tier, rating, busy hours and review lines, the route strip's minutes, the saved places and the district view's
// monthly snapshots, each with its save line written and read back and an old save loaded, and every press kind reaching the call it was drawn for.
internal static partial class Program
{
    private static void Map5RuleChecks()
    {
        Map5CountChecks();
        Map5ChipChecks();
        Map5PlaceChecks();
        Map5RouteChecks();
        Map5SavedChecks();
        Map5MonthChecks();
        Map5BusyChecks();
        Map5TagChecks();
        Map5PressChecks();
    }

    // ------------------------------------------------------------------------------------------------- counts

    private static MapBuildingFact MapFact(string type, string business, string district, string address, bool rent = false, string rival = null, bool rented = false, bool owned = false, bool sale = false) =>
        new MapBuildingFact { BuildingType = type, BusinessType = business, Neighbourhood = district, Address = address, ForRent = rent, RivalId = rival, Rented = rented, Owned = owned, ForSale = sale };

    private static void Map5CountChecks()
    {
        var lines = new[]
        {
            new MapLineSpec { Kind = MapLineKind.BuildingType, Key = "retail" }, new MapLineSpec { Kind = MapLineKind.BusinessType, Key = "ba:businesstype_coffeeshop" },
            new MapLineSpec { Kind = MapLineKind.Neighbourhood, Key = "midtown" }, new MapLineSpec { Kind = MapLineKind.Address, Key = "1 tur" },
            new MapLineSpec { Kind = MapLineKind.Rival, Key = "onyx" }, new MapLineSpec { Kind = MapLineKind.Rented }, new MapLineSpec { Kind = MapLineKind.ForSale },
            new MapLineSpec { Kind = MapLineKind.None }, new MapLineSpec { Kind = MapLineKind.BuildingType, Key = "office" }, new MapLineSpec { Kind = MapLineKind.BusinessType, Key = null },
        };
        var city = new List<MapBuildingFact>
        {
            MapFact("retail", "ba:businesstype_empty", "midtown", "1 a", rent: true),                              // a unit to let
            MapFact("retail", "ba:businesstype_coffeeshop", "midtown", "2 a", rival: "onyx"),                      // a rival's café: not to let
            MapFact("retail", "ba:businesstype_coffeeshop", "uptown", "3 a", rented: true),                        // the player's café
            MapFact("retail", "ba:businesstype_empty", "uptown", "4 a", rent: true, owned: true),                  // the player's own, to let
            MapFact("office", "ba:businesstype_empty", "midtown", "5 a", rent: true, sale: true),                  // an office for sale and to let
            MapFact("office", "ba:businesstype_empty", "midtown", "6 a", sale: true, owned: true),                 // for sale but the player's own: no longer a sale
            MapFact("retail", "ba:businesstype_coffeeshop", "midtown", "1 tur", rival: "onyx", rent: false),
        };
        var count = new int[lines.Length];
        MapCountRules.Count(lines, city, count);
        Check(count[0] == 2 && count[8] == 1, "map filters: a building type's line counts the units of that type that are to let, and no others (retail 2, office 1)");
        Check(count[1] == 3 && count[9] == 0, "map filters: a kind of business counts every building that holds it, rivals' and the player's alike (3 cafés), and a line with no key none");
        Check(count[2] == 5 && count[3] == 1 && count[4] == 2, "map filters: a district counts the buildings in it, an address its one place, a rival the places it runs (5, 1, 2)");
        Check(count[5] == 3 && count[6] == 1 && count[7] == -1, "map filters: Rented by you counts what the player rents or owns (3), For sale what is for sale and not the player's (1), a line with no places is -1");
        MapCountRules.Count(lines, new List<MapBuildingFact>(), count);
        Check(count[0] == 0 && count[5] == 0 && count[7] == -1, "map filters: an empty city counts zero on every line that has places, and still -1 on the rest");
        var big = new List<MapBuildingFact>();
        for (int i = 0; i < 883; i++)
        {
            big.Add(MapFact(i % 3 == 0 ? "retail" : "office", i % 5 == 0 ? "ba:businesstype_coffeeshop" : "ba:businesstype_empty", "d" + (i % 7), i + " st", rent: i % 4 == 0, rival: i % 11 == 0 ? "onyx" : null));
        }
        var manyLines = new MapLineSpec[120];
        for (int i = 0; i < manyLines.Length; i++)
        {
            manyLines[i] = new MapLineSpec { Kind = (MapLineKind)(1 + i % 5), Key = i % 2 == 0 ? "retail" : "d" + (i % 7) };
        }
        var many = new int[manyLines.Length];
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < 20; r++)
        {
            MapCountRules.Count(manyLines, big, many);
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds / 20.0 < 2.0, "map filters: counting 120 lines over 883 buildings takes " + (watch.Elapsed.TotalMilliseconds / 20.0).ToString("F2", CultureInfo.InvariantCulture) + " ms (under 2), once when the map opens");
        MapCountRules.Tally(new[] { true, false, true, true }, new[] { true, true, false, true }, out int lit, out int total);
        Check(lit == 2 && total == 3, "map filters: '2 of 3 on' counts only lines the map can show now (a line it cannot show is not a line)");
    }

    // ------------------------------------------------------------------------------------------------- chips

    private static void Map5ChipChecks()
    {
        Check(MapChipRules.Matches(MapChipKind.Food, "ba:businesstype_coffeeshop", "x") && MapChipRules.Matches(MapChipKind.Food, "ba:businesstype_fastfoodrestaurant", "x")
              && !MapChipRules.Matches(MapChipKind.Food, "ba:businesstype_supermarket", "x") && !MapChipRules.Matches(MapChipKind.Food, "ba:businesstype_barbershop", "x"),
            "quick chips: Food takes the café and the fast food line, not the supermarket, and not a barber's shop that has 'bar' in its name");
        Check(MapChipRules.Matches(MapChipKind.Groceries, "ba:businesstype_supermarket", "x") && MapChipRules.Matches(MapChipKind.Groceries, "ba:businesstype_fruitandvegetablestore", "x")
              && MapChipRules.Matches(MapChipKind.Fuel, "ba:businesstype_gasstation", "x") && !MapChipRules.Matches(MapChipKind.Fuel, "ba:businesstype_cardealership", "x"),
            "quick chips: Groceries takes the supermarket and the greengrocer, Fuel the gas station and not the car dealer");
        Check(MapChipRules.Matches(MapChipKind.Jobs, "ba:skill_customerservice", MapChipRules.JobsGroup) && !MapChipRules.Matches(MapChipKind.Jobs, "ba:skill_customerservice", "other")
              && MapChipRules.Matches(MapChipKind.ForRent, "retail", MapChipRules.RentGroup) && MapChipRules.Matches(MapChipKind.Yours, MapChipRules.YoursLine, "bizman_status")
              && !MapChipRules.Matches(MapChipKind.Yours, "citymap_filter_hide_closed", "bizman_status") && !MapChipRules.Matches(MapChipKind.Food, null, "x"),
            "quick chips: Jobs and For rent are the game's own groups, Your places is the 'rented by you' line, and a line with no key is no chip's");
        var keys = new List<string> { "ba:businesstype_coffeeshop", "ba:businesstype_bakery", "retail", "buildingresume_rented_by_you", "ba:businesstype_gasstation" };
        var groups = new List<string> { "a", "a", MapChipRules.RentGroup, "bizman_status", "b" };
        var available = new List<bool> { true, true, true, true, false };
        var into = new List<int>();
        Check(MapChipRules.Lines(MapChipKind.Food, keys, groups, available, into) == 2 && into[0] == 0 && into[1] == 1, "quick chips: Food names the two food lines of this list");
        Check(MapChipRules.Lines(MapChipKind.Fuel, keys, groups, available, into) == 0 && into.Count == 0, "quick chips: a chip whose only line cannot be shown now names none, so it is not offered (the gas station is not available)");
        var on = new List<bool> { true, false, false, false, false };
        MapChipRules.Lines(MapChipKind.Food, keys, groups, available, into);
        Check(!MapChipRules.Lit(into, on) && MapChipRules.Target(into, on), "quick chips: a chip with one of its two lines on is not lit, and a press turns both on");
        on[1] = true;
        Check(MapChipRules.Lit(into, on) && !MapChipRules.Target(into, on), "quick chips: a chip with every line on is lit, and a press turns them all off");
        Check(!MapChipRules.Lit(new List<int>(), on), "quick chips: a chip with no line is never lit");
    }

    // ------------------------------------------------------------------------------------------------- the place card

    private static void Map5PlaceChecks()
    {
        Check(MapPlaceRules.Tier(0) == 0 && MapPlaceRules.Tier(899) == 1 && MapPlaceRules.Tier(900) == 2 && MapPlaceRules.Tier(1100) == 2 && MapPlaceRules.Tier(1101) == 3,
            "place card: the price tier is $ under nine tenths of the district's standard, $$ to eleven tenths, $$$ over, and none without a price");
        Check(MapPlaceRules.Rating(43) == "4.3" && MapPlaceRules.Rating(50) == "5.0" && MapPlaceRules.Rating(10) == "1.0" && MapPlaceRules.Rating(0) == null,
            "place card: a rating in tenths reads as its figure and none as nothing (not rated yet)");
        Check(MapPlaceRules.Halves(43) == 9 && MapPlaceRules.Halves(50) == 10 && MapPlaceRules.Halves(10) == 2 && MapPlaceRules.Halves(22) == 4 && MapPlaceRules.Halves(0) == 0 && MapPlaceRules.Halves(9) == 2,
            "place card: the stars are the rating's nearest half (4.3 is four and a half)");
        // The busy hours: the game's own hourly multipliers over the hours it is open, the busiest hour at 100.
        float[] shape = { 0, 0, 0, 0, 0, 0, 0, 0.4f, 0.6f, 0.5f, 0.5f, 1f, 1.5f, 1.2f, 0.8f, 0.8f, 1f, 1.8f, 2f, 1.5f, 0.9f, 0.4f, 0, 0 };
        int[] busy = MapPlaceRules.Busy(h => shape[h], 7, 22, out int from);
        int top = 0;
        for (int i = 0; i < busy.Length; i++)
        {
            top = Math.Max(top, busy[i]);
        }
        Check(from == 7 && busy.Length == 15 && top == 100 && busy[11] == 100 && busy[0] == 20 && busy[5] == 75,
            "place card: the busy hours run from the first open hour (07) for 15 hours, the busiest (18:00, the game's 2.0) is 100 and the rest are in proportion");
        int[] night = MapPlaceRules.Busy(h => 1f, 20, 4, out int nightFrom);
        Check(nightFrom == 20 && night.Length == 4 && night[0] == 100, "place card: a business open past midnight is drawn to the end of the day, from its first open hour");
        int[] shut = MapPlaceRules.Busy(h => 0f, 9, 12, out int _);
        Check(shut.Length == 3 && shut[0] == 0 && shut[1] == 0, "place card: hours with no customers at all draw as empty bars, never a division by nothing");
        Check(MapPlaceRules.Now(15, 7, 15) == 8 && MapPlaceRules.Now(6, 7, 15) == -1 && MapPlaceRules.Now(22, 7, 15) == -1, "place card: the hour now is marked only when it is one of the hours drawn");
        Check(!MapPlaceRules.Busier(5, 3) && !MapPlaceRules.Busier(10, 9) && MapPlaceRules.Busier(10, 8) && MapPlaceRules.Busier(18, 16) && !MapPlaceRules.Busier(30, 0),
            "place card: busier than usual is today an eighth over a usual of at least four, and there is no 'usual' to go by before that");
        // Review lines: by stars bucket and the business's reason, one of two lines by the resident and the day: the same every time, nothing saved.
        string a = MapPlaceRules.ReviewKey(5, 1, 4711, 120), b = MapPlaceRules.ReviewKey(5, 1, 4711, 120);
        var seen = new HashSet<string>();
        for (int resident = 1; resident <= 60; resident++)
        {
            seen.Add(MapPlaceRules.ReviewKey(2, 3, resident, 77));
        }
        Check(a == b && seen.Count == MapPlaceRules.ReviewLines && seen.Contains("reallife_map_review_low_wait_0") && seen.Contains("reallife_map_review_low_wait_1"),
            "place card: a review reads the same every time for its resident and day, and sixty residents use both lines of its bucket and reason");
        Check(MapPlaceRules.ReviewKey(1, 0, 1, 1).StartsWith("reallife_map_review_low_mixed_", StringComparison.Ordinal) && MapPlaceRules.ReviewKey(3, 2, 1, 1).StartsWith("reallife_map_review_mid_quality_", StringComparison.Ordinal)
              && MapPlaceRules.ReviewKey(4, 4, 1, 1).StartsWith("reallife_map_review_high_clean_", StringComparison.Ordinal) && MapPlaceRules.ReviewKey(5, 9, 1, 1).StartsWith("reallife_map_review_high_mixed_", StringComparison.Ordinal),
            "place card: one and two stars are low, three middle, four and five high, and the reasons are the simulation's (price, quality, wait, clean, else mixed)");
        Check(MapPlaceRules.DaysAgo(10, 7) == 3 && MapPlaceRules.DaysAgo(10, 10) == 0 && MapPlaceRules.DaysAgo(10, 12) == 0, "place card: a review's age is days, never below nought");
    }

    // ------------------------------------------------------------------------------------------------- the route strip

    private static void Map5RouteChecks()
    {
        Check(MapRouteRules.Minutes(1200f, MapRouteRules.WalkPace, 0f, 1f) == 858 && MapRouteRules.Minutes(1200f, MapRouteRules.CarPace, MapRouteRules.CabWait, 1f) == 390
              && MapRouteRules.Minutes(0f, MapRouteRules.WalkPace, 0f, 1f) == 1 && MapRouteRules.Minutes(-3f, 1.4f, 0f, 1f) == 0 && MapRouteRules.Minutes(100f, 0f, 0f, 1f) == 0,
            "route strip: a way's minutes are its metres at the pace plus the wait, in the game's clock, at least one; no way or no pace is none");
        float subway = MapRouteRules.SubwaySeconds(280f, 2400f, 190f);
        Check(Math.Abs(subway - (280f / 1.4f + 120f + 2400f / 12f + 190f / 1.4f)) < 0.01f, "route strip: the subway is the walk to a station, the wait, the ride and the walk from the other station");
        Check(MapRouteRules.SubwayOffered(300f, 200f, 2000f) && !MapRouteRules.SubwayOffered(900f, 900f, 2000f), "route strip: the subway is offered only when the two walks are short of the way a cab would take");
    }

    // ------------------------------------------------------------------------------------------------- saved places

    private static void Map5SavedChecks()
    {
        var saved = new MapSaved();
        Check(saved.Add("5th Avenue", 214, "Speedy Bites") && saved.Add("Broadway", 7, "Bloom & Co") && !saved.Add("5th Avenue", 214, "again") && !saved.Add("", 3, "x") && saved.Count == 2 && saved[0].Street == "Broadway",
            "saved places: a place is saved once, the newest first, and one with no street is not");
        Check(saved.Contains("5th Avenue", 214) && !saved.Contains("5th Avenue", 215) && saved.IndexOf("Broadway", 7) == 0, "saved places: a place is found by its street and its number");
        Check(!saved.Toggle("5th Avenue", 214, "x") && !saved.Contains("5th Avenue", 214) && saved.Toggle("5th Avenue", 214, "Speedy Bites") && saved.Contains("5th Avenue", 214),
            "saved places: Save saves an unsaved place and takes a saved one out");
        Check(saved.Add("A;B|C", 1, "Name;with|marks") && saved[0].Street == "A B C" && saved[0].Name == "Name with marks", "saved places: the line's separators never ride in a street or a name");
        string line = saved.Write();
        MapSaved back = MapSaved.Read(line);
        Check(back.Count == saved.Count && back.Write() == line && back.Contains("5th Avenue", 214) && back[0].Name == saved[0].Name, "saved places: the line written and read back is the same list in the same order");
        var many = new MapSaved();
        for (int i = 0; i < MapSaved.Most + 9; i++)
        {
            many.Add("Street " + i, i, "n" + i);
        }
        Check(many.Count == MapSaved.Most && many.Contains("Street " + (MapSaved.Most + 8), MapSaved.Most + 8) && !many.Contains("Street 0", 0), "saved places: " + MapSaved.Most + " at most, the oldest go first");
        Check(MapSaved.Read(null).Count == 0 && MapSaved.Read("").Count == 0 && new MapSaved().Write() == null, "saved places: an old save with no line loads with none saved, and none saved writes no line");
        Check(!MapSaved.Readable("2;a|1|b") && MapSaved.Read("2;a|1|b").Count == 0 && MapSaved.Readable("1;a|1|b") && MapSaved.Read("1;a|x|b;c|2|d").Count == 1,
            "saved places: a line of another version gives none (and is left as read), a place that cannot be read is left out");
    }

    // ------------------------------------------------------------------------------------------------- the district view's months

    private static void Map5MonthChecks()
    {
        var names = new List<string> { "midtown", "uptown", "bad;name" };
        long[] rent = { 21200, 18800, 5 };
        var months = new DistrictMonths();
        List<long[]> Figs(long scale)
        {
            var list = new List<long[]>();
            for (int i = 0; i < names.Count; i++)
            {
                var row = new long[DistrictMonths.Layers];
                row[1] = rent[i] * scale / 100;
                row[4] = 3 + i;
                list.Add(row);
            }
            return list;
        }
        List<bool[]> Has()
        {
            var list = new List<bool[]>();
            for (int i = 0; i < names.Count; i++)
            {
                var h = new bool[DistrictMonths.Layers];
                h[1] = true;
                h[4] = i != 1;
                list.Add(h);
            }
            return list;
        }
        Check(months.Take(31, 360, names, Figs(100), Has()) && months.Count == 1 && !months.Take(45, 360, names, Figs(110), Has()) && months.Count == 1,
            "district months: a month is kept once, at its first read, and a second read in it keeps nothing");
        Check(!months.Change(31, 360, "midtown", 1, 21200, true, out long s0, out int t0), "district months: on the day a month was kept there is no change yet");
        Check(months.Change(45, 360, "midtown", 1, 22260, true, out long s1, out int t1) && s1 == 21200 && t1 == 50, "district months: a rent of 212.00 become 222.60 is 5.0 per cent up this month (50 tenths)");
        Check(months.Change(45, 360, "midtown", 1, 19080, true, out long s2, out int t2) && t2 == -100, "district months: a fall reads negative (-10.0 per cent)");
        Check(!months.Change(45, 360, "uptown", 4, 9, true, out long _, out int _) && !months.Change(45, 360, "uptown", 1, 0, false, out long _, out int _) && !months.Change(45, 360, "nowhere", 1, 5, true, out long _, out int _)
              && !months.Change(45, 360, "midtown", 12, 5, true, out long _, out int _) && !months.Change(45, 360, "midtown", -1, 5, true, out long _, out int _),
            "district months: no change where the district had no figure then or has none now, is unknown, or the layer is not one of the ten");
        Check(!months.Change(75, 360, "midtown", 1, 22260, true, out long _, out int _), "district months: a month with nothing kept for it says no change yet, however much the figure moved");
        Check(months.Take(75, 360, names, Figs(150), Has()) && months.Count == 2 && months.Change(80, 360, "midtown", 1, 33000, true, out long s3, out int t3) && s3 == 31800 && t3 == 38,
            "district months: the next month starts from its own first read (318.00 to 330.00 is 3.8 per cent)");
        var sumNames = new List<string> { "midtown", "uptown" };
        var once = new DistrictMonths();
        once.Take(1, 360, sumNames, new List<long[]> { MapMonthRow(1, 1000), MapMonthRow(1, 3000) }, new List<bool[]> { MapMonthHas(), MapMonthHas() });
        Check(once.CityChange(10, 360, sumNames, 1, new long[] { 1100, 3000 }, new[] { true, true }, out int cityTenths) && cityTenths == 25 && !once.CityChange(10, 360, sumNames, 1, new long[] { 0, 0 }, new[] { false, false }, out int _),
            "district months: the city's change is the districts' figures summed (4000 to 4100 is 2.5 per cent), and none where no district has a figure");
        string line = months.Write();
        DistrictMonths back = DistrictMonths.Read(line);
        Check(back.Count == 2 && back.Write() == line && back.Change(45, 360, "midtown", 1, 22260, true, out long _, out int bt) && bt == 50, "district months: the line written and read back keeps every month, the same line and the same change");
        Check(DistrictMonths.Read(null).Count == 0 && DistrictMonths.Read("").Count == 0 && new DistrictMonths().Write() == null, "district months: an old save with no line loads with none kept, and none kept writes no line");
        Check(!DistrictMonths.Readable("9;1=2:a=1,1,1,1,1,1,1,1,1,1") && DistrictMonths.Read("9;1=2:a=1,1,1,1,1,1,1,1,1,1").Count == 0 && DistrictMonths.Read("1;x=y:a=1").Count == 0 && DistrictMonths.Read("1;3=40:a=1,2").Count == 1,
            "district months: a line of another version gives none (left as read), a month or a district that cannot be read is left out");
        var long12 = new DistrictMonths();
        for (int m = 0; m < DistrictMonths.Keep + 5; m++)
        {
            long12.Take(m * 30 + 1, 360, sumNames, new List<long[]> { MapMonthRow(1, 5), MapMonthRow(1, 6) }, new List<bool[]> { MapMonthHas(), MapMonthHas() });
        }
        Check(long12.Count == DistrictMonths.Keep && DistrictMonths.Read(long12.Write()).Count == DistrictMonths.Keep, "district months: " + DistrictMonths.Keep + " months at the most, the oldest dropped");
        Check(DistrictMonths.MonthOf(1, 360) == 0 && DistrictMonths.MonthOf(30, 360) == 0 && DistrictMonths.MonthOf(31, 360) == 1 && DistrictMonths.MonthOf(0, 360) == 0 && DistrictMonths.MonthOf(361, 360) == 12 && DistrictMonths.MonthOf(31, 365) == 0 && DistrictMonths.MonthOf(32, 365) == 1,
            "district months: a month is the calendar's (a twelfth of the year, a day before the first is the first)");
    }

    private static long[] MapMonthRow(int layer, long value)
    {
        var row = new long[DistrictMonths.Layers];
        row[layer] = value;
        return row;
    }

    private static bool[] MapMonthHas()
    {
        var has = new bool[DistrictMonths.Layers];
        has[1] = true;
        return has;
    }

    // ------------------------------------------------------------------------------------------------- every press reaches the call it was drawn for

    // A recording source: it names each call it gets and the argument, so a command that arrives as another kind or another number is found (the wave's rule 13).
    private sealed class MapRecorder : IMapActions
    {
        public string Name;
        public int Arg = -1, Calls;

        private bool R(string name, int arg = -1)
        {
            Name = name;
            Arg = arg;
            Calls++;
            return true;
        }

        public bool Rail(int index) => R("Rail", index);
        public bool ShowAll() => R("ShowAll");
        public bool Search() => R("Search");
        public bool Result(int index) => R("Result", index);
        public bool Chip(int index) => R("Chip", index);
        public bool Group(int row) => R("Group", row);
        public bool GroupAll(int row) => R("GroupAll", row);
        public bool Line(int row) => R("Line", row);
        public bool Locate(int row) => R("Locate", row);
        public bool Station(int row) => R("Station", row);
        public bool Choice(int row) => R("Choice", row);
        public bool Footer() => R("Footer");
        public bool PickedLocate() => R("PickedLocate");
        public bool PickedDestination() => R("PickedDestination");
        public bool PickedClear() => R("PickedClear");
        public bool PickedMore() => R("PickedMore");
        public bool PickedBizMan() => R("PickedBizMan");
        public bool PlaceTab(int index) => R("PlaceTab", index);
        public bool Directions() => R("Directions");
        public bool Save() => R("Save");
        public bool PlaceDestination() => R("PlaceDestination");
        public bool PlaceRent() => R("PlaceRent");
        public bool PlaceAllReviews() => R("PlaceAllReviews");
        public bool PlaceReview(int index) => R("PlaceReview", index);
        public bool RouteOption(int index) => R("RouteOption", index);
        public bool Go() => R("Go");
        public bool North() => R("North");
        public bool Centre() => R("Centre");
        public bool ZoomIn() => R("ZoomIn");
        public bool ZoomOut() => R("ZoomOut");
        public bool Tilt() => R("Tilt");
        public bool Layer(int index) => R("Layer", index);
        public bool HideAll() => R("HideAll");
        public bool DistrictLayer(int layer) => R("DistrictLayer", layer);
        public bool DistrictRow(int row) => R("DistrictRow", row);
        public bool DistrictShow() => R("DistrictShow");
        public bool DistrictFilters() => R("DistrictFilters");
    }

    // The call each command of the 5000 block is drawn for, and whether it carries its argument: written out here, apart from MapPresses, so a swap there is caught.
    private static readonly (int Command, string Call, bool Arg)[] MapDrawn =
    {
        (UiMapFilters.RailCommand, "Rail", true), (UiMapFilters.ShowAllCommand, "ShowAll", false), (UiMapFilters.SearchCommand, "Search", false), (UiMapFilters.ResultCommand, "Result", true),
        (UiMapFilters.ChipCommand, "Chip", true), (UiMapFilters.GroupCommand, "Group", true), (UiMapFilters.GroupAllCommand, "GroupAll", true), (UiMapFilters.LineCommand, "Line", true),
        (UiMapFilters.LocateCommand, "Locate", true), (UiMapFilters.StationCommand, "Station", true), (UiMapFilters.ChoiceCommand, "Choice", true), (UiMapFilters.FooterCommand, "Footer", false),
        (UiMapFilters.PickedLocateCommand, "PickedLocate", false), (UiMapFilters.PickedDestinationCommand, "PickedDestination", false), (UiMapFilters.PickedClearCommand, "PickedClear", false),
        (UiMapFilters.PickedMoreCommand, "PickedMore", false), (UiMapFilters.PickedBizManCommand, "PickedBizMan", false), (UiMapPlace.BizManCommand, "PickedBizMan", false),
        (UiMapPlace.TabCommand, "PlaceTab", true), (UiMapPlace.DirectionsCommand, "Directions", false), (UiMapPlace.SaveCommand, "Save", false),
        (UiMapPlace.DestinationCommand, "PlaceDestination", false), (UiMapPlace.RentCommand, "PlaceRent", false), (UiMapPlace.AllCommand, "PlaceAllReviews", false), (UiMapPlace.ReviewCommand, "PlaceReview", true),
        (UiMapChrome.OptionCommand, "RouteOption", true), (UiMapChrome.GoCommand, "Go", false), (UiMapChrome.NorthCommand, "North", false), (UiMapChrome.CentreCommand, "Centre", false),
        (UiMapChrome.ZoomInCommand, "ZoomIn", false), (UiMapChrome.ZoomOutCommand, "ZoomOut", false), (UiMapChrome.TiltCommand, "Tilt", false), (UiMapChrome.LayerCommand, "Layer", true),
        (UiMapChrome.HideAllCommand, "HideAll", false), (UiMapDistrict.LayerCommand, "DistrictLayer", true), (UiMapDistrict.RowCommand, "DistrictRow", true), (UiMapDistrict.ShowCommand, "DistrictShow", false),
        (UiMapDistrict.FiltersCommand, "DistrictFilters", false),
    };

    // ------------------------------------------------------------------------------------------------- the pieces over the map

    // The small pieces the HUD's host keeps over the map (a district's tag, the label on the route) are laid out once into the size they say: in every language each one holds its
    // words whole inside its box, on the grid, with words that stand out, the cyan tag of the district the page is about as well as the slate ones.
    private static void Map5TagChecks()
    {
        var s = new UiScene();
        var said = new HashSet<string>();
        Action<string> say = line =>
        {
            if (said.Add(line))
            {
                Console.WriteLine("        " + line);
            }
        };
        var districts = new[] { ("Murray Hill", "$152"), ("Lower Manhattan", "112"), ("Industry City, the old docks and the warehouses beyond", "$1,240,500"), ("Hell's Kitchen", null) };
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            string minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "14");
            string hours = t("reallife_nav_hours", "{hours} h {minutes} min").Replace("{hours}", "1").Replace("{minutes}", "40");
            bool fits = true;
            foreach (string words in new[] { minutes, hours })
            {
                foreach (bool onFoot in new[] { true, false })
                {
                    foreach (string distance in new[] { "1.2 km", "850 m", null })
                    {
                        UiMapChrome.RouteTagSize(words, distance, out float w, out float h);
                        s.Begin("route tag", w + 40f, h + 40f, true);
                        UiMapChrome.RouteTag(s, UiAt.Screen, 20f, 20f, w, h, onFoot, words, distance);
                        fits &= UiCheckRules.Spills(s, say) == 0 && UiCheckRules.OffGrid(s, say) == 0 && UiCheckRules.Unfit(s, say) == 0 && UiCheckRules.TooSmall(s, say) == 0
                                && UiCheckRules.LowContrast(s, say) == 0 && UiCheckRules.Overlaps(s, say) == 0 && w >= 60f && h >= 32f;
                    }
                }
            }
            Check(fits, "map tags: the label on the route holds its minutes (\"" + minutes + "\", \"" + hours + "\") and its way to go whole, on the grid, in " + lang);
        }
        bool tagsFit = true;
        foreach (var d in districts)
        {
            foreach (bool chosen in new[] { false, true })
            {
                UiMapDistrict.TagSize(d.Item1, d.Item2 ?? "", out float w, out float h);
                s.Begin("district tag", w + 40f, h + 40f, true);
                UiMapDistrict.Tag(s, UiAt.Screen, 20f, 20f, w, h, d.Item1, d.Item2 ?? "", UiRole.Ramp3, chosen);
                tagsFit &= UiCheckRules.Spills(s, say) == 0 && UiCheckRules.OffGrid(s, say) == 0 && UiCheckRules.TooSmall(s, say) == 0 && UiCheckRules.LowContrast(s, say) == 0
                           && UiCheckRules.Overlaps(s, say) == 0 && w <= 240f;
            }
        }
        Check(tagsFit, "map tags: a district's tag, slate or cyan, stays inside its box on the grid with words that stand out, and never wider than 240 px for a long name");
    }

    private static void Map5PressChecks()
    {
        var rec = new MapRecorder();
        string wrong = null;
        var seen = new HashSet<int>();
        foreach ((int command, string call, bool carriesArg) in MapDrawn)
        {
            rec.Calls = 0;
            bool done = MapPresses.Do(command, 37, rec);
            if (!seen.Add(command))
            {
                wrong = "the command " + command + " is drawn for two kinds";
            }
            else if (!done || rec.Calls != 1 || rec.Name != call || rec.Arg != (carriesArg ? 37 : -1))
            {
                wrong = "command " + command + " reached " + rec.Name + "(" + rec.Arg + ") in " + rec.Calls + " call(s), not " + call + (carriesArg ? "(37)" : "()");
            }
        }
        Check(wrong == null, "map panels: every command of the 5000 block reaches the one call it is drawn for, with its argument or none, once (" + (wrong ?? MapDrawn.Length + " commands") + ")");
        Check(MapDrawn.Length == MapPresses.Commands.Length, "map panels: the table of commands the checks hold is every command the panels name (" + MapDrawn.Length + " of " + MapPresses.Commands.Length + ")");
        rec.Calls = 0;
        int taken = 0;
        foreach (int other in new[] { 0, 1, 2, 3, 4, 5, 7, 20, 40, 60, 200, 230, 260, 300, 400, 650, 4999, 5100, 9000, 9010 })
        {
            taken += MapPresses.Do(other, 1, rec) ? 1 : 0;
        }
        Check(taken == 0 && rec.Calls == 0, "map panels: a command of another part of the kit (the app window's 1, the decision card's 1, the HUD's 260, the status bar's 9000) is nobody's here and does nothing");
        var allDifferent = new HashSet<int>(MapPresses.Commands);
        Check(allDifferent.Count == MapPresses.Commands.Length && allDifferent.Contains(5000) && allDifferent.Contains(5063), "map panels: every command of the block is its own number");
    }

    // ------------------------------------------------------------------------------------------------- busier than usual

    private static void Map5BusyChecks()
    {
        var busy = new MapBusy();
        busy.Watch("main~12", 40, 10);
        Check(busy.Count == 1 && busy.Usual("main~12", out int u0) && u0 == 40, "map busy: a place first looked at is kept at what it draws that day, so it is never busier on the first look");
        Check(!busy.Usual("elsewhere~1", out int _), "map busy: a place not looked at has no usual");
        Check(!busy.Move("main~12", 80, 10), "map busy: the same game day moves nothing");
        Check(busy.Move("main~12", 80, 11) && busy.Usual("main~12", out int u1) && u1 == 50, "map busy: a new day moves the average a quarter of the way (40 and a day of 80 make 50)");
        Check(!busy.Move("elsewhere~1", 5, 12), "map busy: a place not kept is not moved");
        busy.Watch("main~12", 70, 12);
        Check(busy.Count == 1 && busy.Usual("main~12", out int u2) && u2 == 50, "map busy: looking again at a place that is kept changes nothing but the day it was looked at");
        busy.Watch("bad;place", 5, 1);
        busy.Watch("bad|place", 5, 1);
        busy.Watch(string.Empty, 5, 1);
        busy.Watch(null, 5, 1);
        Check(busy.Count == 1, "map busy: a place with a separator in its name, or none, is not kept");
        var many = new MapBusy();
        for (int i = 0; i < MapBusy.Most + 10; i++)
        {
            many.Watch("s~" + i, i, i);
        }
        Check(many.Count == MapBusy.Most && !many.Usual("s~0", out int _) && many.Usual("s~" + (MapBusy.Most + 9), out int _),
            "map busy: at most " + MapBusy.Most + " places are kept, the one looked at longest ago going first");
        string line = busy.Write();
        MapBusy back = MapBusy.Read(line);
        Check(back.Count == 1 && back.Write() == line && back.Usual("main~12", out int u3) && u3 == 50, "map busy: the line written and read back keeps every place, the same line and the same average");
        Check(MapBusy.Read(null).Count == 0 && new MapBusy().Write() == null && !MapBusy.Readable("9;a|1|1") && MapBusy.Read("9;a|1|1").Count == 0 && MapBusy.Read("1;a|x|1;b|2|3").Count == 1,
            "map busy: an old save has none, none kept writes no line, a line of another version is not read, and a place that cannot be read is left out");
        Check(MapPlaceRules.Busier(45, 40) && !MapPlaceRules.Busier(44, 40) && !MapPlaceRules.Busier(30, 3), "map busy: busier than usual is an eighth over the average, and only once there is an average to go by");
    }
}
