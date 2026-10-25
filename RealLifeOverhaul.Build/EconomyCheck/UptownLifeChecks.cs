using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Uptown;
using RealLifeOverhaul.Utilities;
using NyDistricts = RealLifeOverhaul.CityDistricts;

// Uptown as a district of the game (Scripts/Uptown; docs/features/uptown.md, "The district and its buildings"), game-free:
// its values from Midtown and Hell's Kitchen (Build 3682's figures written down, the half and half, what the game makes of
// them once a run), its quad on the city map, its lots' first buildings, their structures and traffic on a plan laid as the
// ground's (the game's grid continued north of 1st Avenue), what refuses a building the construction game finishes, its line
// in the save, the district save's view of it, the city check with it, every table of the mod's by district with it and
// without it, and the sources read as text: its game files alone write the game's tables of districts, the load's order, the
// stand-down, nothing left to chance, nothing per frame; and its texts.
internal static partial class Program
{
    private const string UptownId = DistrictValues.Id;

    private static void UptownLifeChecks()
    {
        UptownValues();
        UptownZone();
        UptownLotRules();
        UptownFinishing();
        UptownStore();
        UptownSavePlaces();
        UptownCityCheck();
        UptownTables();
        UptownSources();
        UptownTexts();
        UptownFillChecks();
        UptownPlaceChecks();
        UptownDenseChecks();
        UptownFrontChecks();
        UptownWorkChecks();
    }

    // ---------------------------------------------------------------- its values

    private static void UptownValues()
    {
        // The game's own NeighborhoodData and BuildingTypeData of Build 3682 (its addressables): what the figures written down must be.
        DistrictFigures m = DistrictValues.Midtown3682, k = DistrictValues.HellsKitchen3682;
        Check(m.Working == 0 && m.Middle == 6 && m.Upper == 94 && m.RealEstate == 2.2f && m.Parking == 15f && m.Demand == 0.9f && m.CarTraffic == 100 && m.InteriorScore == 50
              && m.BasePrice == 20554f && m.RentPerPrice == 0.00056f && m.Marketing == 0.5f && m.ShutDownBelow == 850f && m.Offices && m.EmptyOffices == 15 && m.EmptyShops == 6,
            "uptown: Midtown's figures as Build 3682 has them");
        Check(k.Working == 6 && k.Middle == 82 && k.Upper == 12 && k.RealEstate == 1.3f && k.Parking == 7.5f && k.Demand == 0.7f && k.CarTraffic == 90 && k.InteriorScore == 0
              && k.BasePrice == 20554f && k.RentPerPrice == 0.00056f && k.Marketing == 0.7f && k.ShutDownBelow == 700f && k.Offices && k.EmptyOffices == 20 && k.EmptyShops == 10,
            "uptown: Hell's Kitchen's figures as Build 3682 has them");

        DistrictFigures u = DistrictValues.Written;
        Check(u.Working == 3 && u.Middle == 44 && u.Upper == 53, "uptown: its people are 3, 44 and 53 % working, middle and upper class, 100 in all");
        Check(u.RealEstate == 1.75f && u.Parking == 11.25f && u.Demand == 0.8f && u.CarTraffic == 95 && u.InteriorScore == 25 && u.BasePrice == 20554f && u.RentPerPrice == 0.00056f
              && u.Marketing == 0.6f && u.ShutDownBelow == 775f && u.Offices, "uptown: each other figure is its neighbours' mean, one they share kept exactly (the rent's multiplier)");
        Check(u.EmptyOffices == 18 && u.EmptyShops == 8, "uptown: 18 % of its offices and 8 % of its shops kept empty to let (17.5 and 8, halves up)");
        Check(DistrictValues.Sound(u) && DistrictValues.LandPerSqm(u) == 20554f * 1.75f, "uptown: figures the game can take, its land 35,969.5 a square metre");

        Check(DistrictValues.Same(DistrictValues.Blend(k, m), u), "uptown: the blend is the same whichever neighbour comes first");
        Check(DistrictValues.Same(DistrictValues.Blend(m, m), m), "uptown: a district blended with itself is itself");
        DistrictFigures odd = m;
        odd.Working = 90;
        odd.Upper = 60;
        DistrictFigures over = DistrictValues.Blend(odd, odd);
        Check(over.Working == 90 && over.Middle == 0 && over.Upper == 10, "uptown: classes that would pass 100 are kept to 100, the middle class going first, then the upper");
        DistrictFigures changed = m;
        changed.Parking = 16f;
        Check(!DistrictValues.Same(changed, m) && !DistrictValues.Sound(new DistrictFigures()), "uptown: a figure the game changed is told, and no figures at all are no district");

        // The game's own arithmetic of a district's price index (CitizenHelper.Init), in single precision.
        float index = (1.1999999f * 3 + 1.4f * 44 + 1.6999999f * 53) / 100;
        Check(DistrictValues.PriceIndex(3, 44, 53) == index && Math.Abs(index - 1.553f) < 0.001f && DistrictValues.PriceIndex(0, 0, 0) == 1.4f,
            "uptown: its people's price index is the game's own sum (1.553), and a district of no people the middle class's");
        var classes = new List<int>();
        var shares = new List<float>();
        DistrictValues.ClassOrder(3, 44, 53, classes, shares);
        Check(classes.Count == 3 && classes[0] == 2 && classes[1] == 1 && classes[2] == 0 && shares[0] == 53f && shares[2] == 3f, "uptown: its classes by share, largest first");
        DistrictValues.ClassOrder(10, 10, 80, classes, shares);
        bool tie = classes[0] == 2 && classes[1] == 0 && classes[2] == 1;
        DistrictValues.ClassOrder(50, 50, 0, classes, shares);
        Check(tie && classes[0] == 0 && classes[1] == 1 && classes[2] == 2, "uptown: classes of equal share keep the game's order (working, middle, upper), as OrderByDescending does");
        string line = DistrictValues.Describe(u);
        Check(line.Contains("3/44/53") && line.Contains("x1.75") && line.IndexOf(" - ", StringComparison.Ordinal) < 0, "uptown: its figures in one line for the log: " + line);
    }

    // ---------------------------------------------------------------- its quad on the city map

    private static void UptownZone()
    {
        // The game's own zones (MainScene, CityManager/NeighborhoodZones): a quad mesh 0 to 2 by -2 to 0, Midtown's at (-344, 516.5)
        // scaled (240, 270) over -344..136 by -23.5..516.5, Hell's Kitchen's at (136, 516.5) scaled (155, 270), Garment's south of it.
        bool midtown = DistrictValues.ZoneOver(-344f, -23.5f, 136f, 516.5f, 0f, 2f, -2f, 0f, out float x, out float z, out float sx, out float sz);
        Check(midtown && x == -344f && z == 516.5f && sx == 240f && sz == 270f, "uptown: a quad over a rectangle stands as the game's own Midtown does (" + x + ", " + z + ", " + sx + ", " + sz + ")");
        bool kitchen = DistrictValues.ZoneOver(446f, 516.5f, 136f, -23.5f, 0f, 2f, -2f, 0f, out x, out z, out sx, out sz);
        Check(kitchen && x == 136f && z == 516.5f && sx == 155f && sz == 270f, "uptown: corners in any order, as Hell's Kitchen's");
        Check(!DistrictValues.ZoneOver(0f, 0f, 10f, 10f, 1f, 1f, -2f, 0f, out x, out z, out sx, out sz) && !DistrictValues.ZoneOver(5f, 0f, 5f, 10f, 0f, 2f, -2f, 0f, out x, out z, out sx, out sz),
            "uptown: a template with no extent, or a rectangle with none, makes no quad");

        // The ground's first block runs from the old wall (480.5) to past Uptown Avenue; Manhattan's zones reach 516.5.
        float south = DistrictValues.SouthClear(-320.32f, 480.5f, 415.38f, 635.38f, -344f, 136f, -23.5f, 516.5f);
        south = DistrictValues.SouthClear(-320.32f, south, 415.38f, 635.38f, 136f, 446f, -23.5f, 516.5f);
        Check(south == 516.5f, "uptown: its quad starts where Midtown's and Hell's Kitchen's end, so no stretch of the map is tinted twice (" + south + ")");
        Check(DistrictValues.SouthClear(-320.32f, 480.5f, 415.38f, 635.38f, 136f, 446f, -563.5f, -23.5f) == 480.5f
              && DistrictValues.SouthClear(-320.32f, 480.5f, 415.38f, 635.38f, -900f, -400f, -23.5f, 516.5f) == 480.5f
              && DistrictValues.SouthClear(-320.32f, 480.5f, 415.38f, 635.38f, -344f, 136f, -100f, 700f) == 480.5f
              && DistrictValues.SouthClear(-320.32f, 480.5f, 415.38f, 635.38f, -344f, 136f, 500f, 600f) == 480.5f,
            "uptown: a zone that does not reach it, lies beside it, covers it whole or starts inside it leaves its south edge where it is");
        bool cleared = DistrictValues.ZoneOver(-320.32f, south, 415.38f, 635.38f, 0f, 2f, -2f, 0f, out x, out z, out sx, out sz);
        Check(cleared && Math.Abs(x + 320.32f) < 0.01f && Math.Abs(z - 635.38f) < 0.01f && Math.Abs(sz * 2f - 118.88f) < 0.01f, "uptown: its quad over the ground cleared of Manhattan's zones");
        Check(DistrictValues.ZoneAlpha == 100f / 255f && DistrictValues.ZoneGreen > DistrictValues.ZoneBlue && DistrictValues.ZoneBlue > DistrictValues.ZoneRed,
            "uptown: its zone a green-blue no district of the game's has, at the game's own alpha");
    }

    // ---------------------------------------------------------------- the lots, on a plan laid as the ground's

    private static readonly string[] UptownSmall = { "ba:buildingsize_a", "ba:buildingsize_b", "ba:buildingsize_c", "ba:buildingsize_f" };
    private static readonly string[] UptownOffice = { "ba:buildingsize_d", "ba:buildingsize_j", "ba:buildingsize_k" };
    private static readonly string[] UptownBig = { "ba:buildingsize_m", "ba:buildingsize_l", "ba:buildingsize_n" };
    private static readonly string[] UptownWarehouse = { "ba:buildingsize_h", "ba:buildingsize_i", "ba:buildingsize_p", "ba:buildingsize_q" };
    private static readonly BuildingsClass[] UptownMix = { BuildingsClass.Small, BuildingsClass.Office, BuildingsClass.Small, BuildingsClass.Big, BuildingsClass.Small, BuildingsClass.Warehouse };
    private static readonly string[] UptownStreetNames = { "ninth", "eighth", "seventh", "sixth", "fifth", "fourth", "third", "second", "first" };
    private static readonly string[] UptownAvenueNames = { "uptownavenue", "summitavenue" };

    private static IReadOnlyList<string> UptownSizes(BuildingsClass kind)
    {
        return kind == BuildingsClass.Office ? UptownOffice : kind == BuildingsClass.Big ? UptownBig : kind == BuildingsClass.Warehouse ? UptownWarehouse : UptownSmall;
    }

    // The ground's grid (LayoutRules on its branch): Manhattan's streets every 87.3 m from 9th Street's middle at -301.67, its
    // avenues every 147.2 m north of 1st Avenue's at 464.58; two rows of the eight blocks between 9th and 1st Street, each
    // block's lots along its avenue (north of them, in Manhattan's mix) and small ones along its two streets. Row 1 is the first block.
    private static List<BuildingsLot> UptownPlan()
    {
        var lots = new List<BuildingsLot>(256);
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        int mix = 0;
        for (int row = 1; row <= 2; row++)
        {
            float avenue = 464.58f + 147.2f * row;
            float south = row == 1 ? 510f : 464.58f + 147.2f * (row - 1) + 13.65f;
            for (int column = 6; column < 14; column++)
            {
                float x0 = -301.67f + 87.3f * (column - 6) + 8.65f, x1 = x0 + 87.3f - 17.3f;
                int n = 0;
                for (float along = x0 + 1f; ; )
                {
                    BuildingsClass kind = UptownMix[mix % UptownMix.Length];
                    int width = kind == BuildingsClass.Small ? 11 : kind == BuildingsClass.Warehouse ? 29 : 23;
                    int depth = kind == BuildingsClass.Small ? 24 : kind == BuildingsClass.Warehouse ? 39 : 32;
                    if (along + width > x1 - 1f)
                    {
                        break;
                    }
                    mix++;
                    UptownLot(lots, numbers, row, column, ++n, UptownAvenueNames[row - 1], false, along + width * 0.5f, avenue - 13.65f - depth * 0.5f, 0f, width, depth, kind);
                    along += width + 2f;
                }
                for (int side = 0; side < 2; side++)
                {
                    for (float along = south + 1f; along + 11f < avenue - 13.65f - 34f; along += 13f)
                    {
                        float x = side == 0 ? x0 + 12f : x1 - 12f;
                        UptownLot(lots, numbers, row, column, ++n, UptownStreetNames[column - 6 + side], true, x, along + 5.5f, side == 0 ? 270f : 90f, 11, 24, BuildingsClass.Small);
                    }
                }
            }
        }
        return lots;
    }

    private static void UptownLot(List<BuildingsLot> lots, Dictionary<string, int> numbers, int row, int column, int n, string name, bool continued, float x, float z, float facing,
                                  int width, int depth, BuildingsClass kind)
    {
        string street = "reallife:street_uptown_" + name;
        bool odd = facing == 90f || facing == 0f;   // a street's west side and an avenue's south side, as the ground numbers them
        string side = street + (odd ? "|odd" : "|even");
        if (!numbers.TryGetValue(side, out int number))
        {
            number = (continued ? 1001 : 1) + (odd ? 0 : 1);
        }
        numbers[side] = number + 2;
        lots.Add(new BuildingsLot
        {
            Id = "uptown_" + row.ToString(CultureInfo.InvariantCulture) + "_" + column.ToString("00", CultureInfo.InvariantCulture) + "_" + n.ToString("00", CultureInfo.InvariantCulture),
            Street = street, Number = number, First = row == 1, Avenue = BuildingsRules.FrontsAvenue(facing), X = x, Z = z, Facing = facing, Width = width, Depth = depth,
            Class = kind, Sizes = UptownSizes(kind)
        });
    }

    private static void UptownLotRules()
    {
        Check(BuildingsRules.DayOneCount(0) == 0 && BuildingsRules.DayOneCount(1) == 1 && BuildingsRules.DayOneCount(3) == 1 && BuildingsRules.DayOneCount(6) == 2
              && BuildingsRules.DayOneCount(83) == 21 && BuildingsRules.DayOneCount(100) == 25 && BuildingsRules.DayOneCount(-3) == 0,
            "uptown: a quarter of the first block's lots stand from the first day, halves up, at least one where there are any (21 of the ground's 83)");
        Check(BuildingsRules.FrontsAvenue(0f) && BuildingsRules.FrontsAvenue(180f) && BuildingsRules.FrontsAvenue(360f) && BuildingsRules.FrontsAvenue(-180f) && BuildingsRules.FrontsAvenue(359.5f)
              && !BuildingsRules.FrontsAvenue(90f) && !BuildingsRules.FrontsAvenue(270f) && !BuildingsRules.FrontsAvenue(-90f),
            "uptown: a lot whose street lies north or south of it fronts an avenue (they run west to east), one whose street lies east or west a street");
        var onAvenue = new BuildingsLot { Id = "x", Avenue = true, X = 5f, Z = 600f };
        var onStreet = new BuildingsLot { Id = "y", Avenue = false, X = 5f, Z = 600f };
        Check(BuildingsRules.Reach(onAvenue, 0f, 1f) == 600f - BuildingsRules.AvenuePull && BuildingsRules.Reach(onStreet, 0f, 1f) == 600f && BuildingsRules.Reach(onStreet, 1f, 0f) == 5f,
            "uptown: how far a lot is from Manhattan along the way the district runs, an avenue's " + BuildingsRules.AvenuePull + " m nearer");

        List<BuildingsLot> plan = UptownPlan();
        int first = 0, avenues = 0;
        foreach (BuildingsLot lot in plan)
        {
            first += lot.First ? 1 : 0;
            avenues += lot.Avenue ? 1 : 0;
        }
        var chosen = BuildingsRules.DayOne(plan, 0f, 1f, new List<int>());
        Check(first >= 80 && first <= 110 && avenues > 0 && chosen.Count == BuildingsRules.DayOneCount(first),
            "uptown: on a plan of " + plan.Count + " lots, " + chosen.Count + " of the first block's " + first + " stand from the first day");
        var picked = new HashSet<string>(StringComparer.Ordinal);
        float farthest = float.MinValue;
        bool allFirst = true;
        foreach (int i in chosen)
        {
            picked.Add(plan[i].Id);
            allFirst &= plan[i].First;
            farthest = Math.Max(farthest, BuildingsRules.Reach(plan[i], 0f, 1f));
        }
        bool nearest = true;
        foreach (BuildingsLot lot in plan)
        {
            if (lot.First && !picked.Contains(lot.Id))
            {
                nearest &= BuildingsRules.Reach(lot, 0f, 1f) >= farthest;
            }
        }
        Check(allFirst && nearest, "uptown: the first day's buildings are the first block's nearest Manhattan, so the streets out of it are built up first");
        bool sorted = true;
        for (int i = 1; i < chosen.Count; i++)
        {
            sorted &= string.CompareOrdinal(plan[chosen[i - 1]].Id, plan[chosen[i]].Id) < 0;
        }
        Check(sorted, "uptown: the first day's lots in the order of their ids");

        // The same lots in any order: the same buildings and the same traffic, on every machine.
        float near = BuildingsRules.Nearest(plan, 0f, 1f);
        var traffic = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (BuildingsLot lot in plan)
        {
            traffic[lot.Id] = BuildingsRules.Traffic(lot, near, 0f, 1f);
        }
        bool same = true;
        var order = new int[plan.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        for (int turn = 0; turn < 12; turn++)
        {
            Shuffle(order, turn);
            var shuffled = new List<BuildingsLot>(plan.Count);
            foreach (int i in order)
            {
                shuffled.Add(plan[i]);
            }
            var again = new HashSet<string>(StringComparer.Ordinal);
            foreach (int i in BuildingsRules.DayOne(shuffled, 0f, 1f, new List<int>()))
            {
                again.Add(shuffled[i].Id);
            }
            float nearAgain = BuildingsRules.Nearest(shuffled, 0f, 1f);
            same &= again.SetEquals(picked) && nearAgain == near;
            foreach (BuildingsLot lot in shuffled)
            {
                same &= BuildingsRules.Traffic(lot, nearAgain, 0f, 1f) == traffic[lot.Id];
            }
        }
        Check(same, "uptown: the plan's lots in any order give the same first day's buildings and the same traffic");

        bool bounded = true, close = true;
        foreach (BuildingsLot lot in plan)
        {
            int t = traffic[lot.Id];
            bounded &= t >= BuildingsRules.TrafficLeast && t <= BuildingsRules.TrafficMost;
            int steps = Math.Max(0, Math.Min(BuildingsRules.TrafficStepsMost, (int)Math.Floor((lot.Z - near) / BuildingsRules.TrafficStepMetres)));
            int plain = (lot.Avenue ? BuildingsRules.AvenueTraffic : BuildingsRules.StreetTraffic) - BuildingsRules.TrafficStep * steps;
            close &= Math.Abs(t - plain) <= BuildingsRules.TrafficJitter;
        }
        Check(bounded && close, "uptown: every lot's traffic index is 10 to 60, its road's less 2 a step of 25 m from Manhattan, within 3 of that by its id");
        var nearLot = new BuildingsLot { Id = "a", Avenue = true, Z = 500f };
        var farLot = new BuildingsLot { Id = "a", Avenue = true, Z = 1500f };
        var sideLot = new BuildingsLot { Id = "a", Avenue = false, Z = 500f };
        Check(BuildingsRules.Traffic(nearLot, 500f, 0f, 1f) - BuildingsRules.Traffic(farLot, 500f, 0f, 1f) == BuildingsRules.TrafficStep * BuildingsRules.TrafficStepsMost
              && BuildingsRules.Traffic(nearLot, 500f, 0f, 1f) - BuildingsRules.Traffic(sideLot, 500f, 0f, 1f) == BuildingsRules.AvenueTraffic - BuildingsRules.StreetTraffic,
            "uptown: a lot far out loses ten at most, and one on a street has sixteen less than one on an avenue");

        var options = new List<BuildingsStructure>
        {
            new BuildingsStructure { Size = "ba:buildingsize_c", Version = 2, Type = "ba:buildingtype_retail" },
            new BuildingsStructure { Size = "ba:buildingsize_a", Version = 1, Type = "ba:buildingtype_retail" },
            new BuildingsStructure { Size = "ba:buildingsize_b", Version = 1, Type = "ba:buildingtype_residential" },
            new BuildingsStructure { Size = "ba:buildingsize_c", Version = 1, Type = "ba:buildingtype_retail" }
        };
        options.Sort(BuildingsRules.CompareStructures);
        Check(options[0].Size == "ba:buildingsize_a" && options[1].Size == "ba:buildingsize_b" && options[2].Version == 1 && options[3].Version == 2,
            "uptown: the game's structures in one order, size, type, then version");
        bool spread = true;
        var seen = new HashSet<int>();
        foreach (BuildingsLot lot in plan)
        {
            int pick = BuildingsRules.PickStructure(lot.Id, options);
            spread &= pick >= 0 && pick < options.Count && pick == BuildingsRules.PickStructure(lot.Id, options);
            seen.Add(pick);
        }
        Check(spread && seen.Count == options.Count && BuildingsRules.PickStructure("a", new List<BuildingsStructure>()) == -1 && BuildingsRules.PickStructure(null, options) == -1,
            "uptown: a lot's structure picked by its id among the game's, every one of them used across a plan, none from none");
        Check(BuildingsRules.TakesSize(plan[0], plan[0].Sizes[0]) && !BuildingsRules.TakesSize(plan[0], "ba:buildingsize_t") && !BuildingsRules.TakesSize(new BuildingsLot(), "ba:buildingsize_a"),
            "uptown: a lot takes the sizes of its class alone");
    }

    // ---------------------------------------------------------------- the construction game's finished buildings

    private static void UptownFinishing()
    {
        var lot = new BuildingsLot { Id = "uptown_1_08_03", Sizes = UptownSmall, Class = BuildingsClass.Small };
        Check(BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 4, FinishOwner.Player, 650000, true, true, false) == null,
            "uptown: a building finished on a free lot, of a size its class takes, is raised");
        Check(BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 0, FinishOwner.City, 0, true, true, true) == null,
            "uptown: in a Going Public session a building for the city is raised, on every machine alike");
        string[] refusals =
        {
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 1, FinishOwner.City, 0, true, false, false),
            BuildingsRules.FinishRefusal(lot, false, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 1, FinishOwner.City, 0, true, true, false),
            BuildingsRules.FinishRefusal(lot, true, true, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 1, FinishOwner.City, 0, true, true, false),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, BuildingsRules.SpecialType, 1, FinishOwner.City, 0, true, true, false),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_k", 1, "ba:buildingtype_office", 1, FinishOwner.City, 0, true, true, false),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 9, "ba:buildingtype_retail", 1, FinishOwner.City, 0, false, true, false),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 13, FinishOwner.City, 0, true, true, false),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", -1, FinishOwner.City, 0, true, true, false),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 1, FinishOwner.Player, 1, true, true, true),
            BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_c", 1, "ba:buildingtype_retail", 1, FinishOwner.Player, double.NaN, true, true, false)
        };
        var whys = new HashSet<string>(StringComparer.Ordinal);
        bool each = true;
        foreach (string why in refusals)
        {
            each &= !string.IsNullOrEmpty(why) && why.IndexOf(" - ", StringComparison.Ordinal) < 0;
            whys.Add(why ?? string.Empty);
        }
        Check(each && whys.Count == refusals.Length, "uptown: Uptown not living, a lot unknown or taken, the special type, a size its class does not take, a structure the game lacks, "
                                                     + "storeys out of 0 to 12, the player's deed in a session and a price that is no money are each refused, each with its own why");
        Check(BuildingsRules.StoreysMost == 12 && BuildingsRules.NewDeedOccupancy == 0f, "uptown: twelve storeys at most, and a new deed's building has nobody in it yet");
    }

    // ---------------------------------------------------------------- its line in the save

    private static void UptownStore()
    {
        var state = new LifeState();
        state.Finished.Add(new FinishedBuilding
        {
            Lot = "uptown_1_08_03", Size = "ba:buildingsize_c", Version = 2, Type = "ba:buildingtype_retail", Design = "SM_Hell's KitchenBuilding11", Storeys = 4,
            Owner = FinishOwner.Player, Price = 653210.75, Day = 140, Settled = true
        });
        state.Finished.Add(new FinishedBuilding { Lot = "uptown_2_11_01", Size = "ba:buildingsize_k", Version = 1, Type = "ba:buildingtype_office", Design = string.Empty, Owner = FinishOwner.City, Day = 9 });
        string line = LifeStore.Write(state);
        LifeState back = LifeStore.Read(line, out int version);
        bool same = back != null && version == LifeStore.Version && back.Finished.Count == 2;
        if (same)
        {
            FinishedBuilding a = back.Finished[0], b = back.Finished[1];
            same = a.Lot == "uptown_1_08_03" && a.Size == "ba:buildingsize_c" && a.Version == 2 && a.Type == "ba:buildingtype_retail" && a.Design == "SM_Hell's KitchenBuilding11"
                   && a.Storeys == 4 && a.Owner == FinishOwner.Player && a.Price == 653210.75 && a.Day == 140 && a.Settled
                   && b.Lot == "uptown_2_11_01" && b.Design == string.Empty && b.Storeys == 0 && b.Owner == FinishOwner.City && b.Price == 0 && b.Day == 9 && !b.Settled;
        }
        Check(same && line.StartsWith("v=1^f:", StringComparison.Ordinal), "uptown: the district's line reads back exactly what it wrote (" + line + ")");
        Check(LifeStore.Key == "reallife:uptown" && DistrictSaveRules.IsModId(LifeStore.Key), "uptown: its line is under a key of the mod's own");
        LifeState empty = LifeStore.Read(LifeStore.Write(new LifeState()), out _);
        Check(empty != null && empty.Finished.Count == 0 && LifeStore.Load(null, out _).Finished.Count == 0 && LifeStore.Load(new Dictionary<string, string>(), out _).Finished.Count == 0,
            "uptown: a save with no line, or an empty one, has no finished building");
        LifeState later = LifeStore.Read("v=2^x:something new^f:uptown_1_06_01|ba:buildingsize_a|1|ba:buildingtype_retail||0|0|0|3|1|extra|fields", out int laterVersion);
        Check(later != null && laterVersion == 2 && later.Finished.Count == 1 && later.Finished[0].Day == 3 && later.Finished[0].Settled,
            "uptown: a later version's line is read as far as this one knows it (a part and fields it adds are passed over)");
        Check(LifeStore.Read("garbage", out _) == null && LifeStore.Read("v=x^f:", out _) == null && LifeStore.Read(null, out _) == null,
            "uptown: a line that is not the district's is not read (it is kept as it was)");
        LifeState bounded = LifeStore.Read("v=1^f:a|s|999|t|d|99|7|-5|-2|1;a|s|1|t||0|0|0|0|0;b|||t||0|0|0|0|0", out _);
        Check(bounded.Finished.Count == 1 && bounded.Finished[0].Version == 99 && bounded.Finished[0].Storeys == BuildingsRules.StoreysMost && bounded.Finished[0].Owner == FinishOwner.City
              && bounded.Finished[0].Price == 0 && bounded.Finished[0].Day == 0,
            "uptown: figures out of bounds are kept within them, an owner mark not the player's goes to the city, a lot twice is read once, a record with no size left out");
        var many = new LifeState();
        for (int i = 0; i < LifeStore.FinishedMost + 20; i++)
        {
            many.Finished.Add(new FinishedBuilding { Lot = "lot" + i, Size = "s", Type = "t" });
        }
        Check(LifeStore.Read(LifeStore.Write(many), out _).Finished.Count == LifeStore.FinishedMost, "uptown: the line holds " + LifeStore.FinishedMost + " finished buildings at most");
        var cleaned = new LifeState();
        cleaned.Finished.Add(new FinishedBuilding { Lot = "lot", Size = "s", Type = "t", Design = "a|b;c^d" });
        Check(LifeStore.Read(LifeStore.Write(cleaned), out _).Finished[0].Design == "a b c d", "uptown: a design's name never breaks the line");
    }

    // ---------------------------------------------------------------- the district save's view of it

    private static void UptownSavePlaces()
    {
        List<BuildingsLot> plan = UptownPlan();
        Check(DistrictSaveRules.IsModId(UptownId) && !DistrictSaveRules.IsModId("ba:neighborhood_midtown"), "uptown: its id is the mod's own, never one of the game's");
        bool streets = true;
        foreach (BuildingsLot lot in plan)
        {
            streets &= DistrictSaveRules.IsModId(lot.Street);
        }
        Check(streets, "uptown: every street of its plan is the mod's own, so whatever names one leaves every save");

        // The district save's fill as Uptown fills it once its buildings stand.
        DistrictPlaces.Reset();
        DistrictPlaces.Add("uptown", set =>
        {
            set.District(UptownId);
            foreach (BuildingsLot lot in plan)
            {
                set.Street(lot.Street);
            }
            set.Ground(-320.32f, 480.5f, 415.38f, 635.38f);
            set.Exit(91.18f, 0f, 473.33f, 270f, "ba:neighborhood_midtown");
            set.Door(plan[0].Street, plan[0].Number, plan[0].X, 0f, plan[0].Z + 13f, 0f, "ba:neighborhood_midtown");
        });
        PlaceSet all = DistrictPlaces.Build();
        PlaceSet own = DistrictPlaces.Of("uptown");
        bool named = all.NamesDistrict(UptownId) && own != null && own.NamesDistrict(UptownId) && own.IsScoped;
        foreach (BuildingsLot lot in plan)
        {
            named &= all.Names(lot.Street, lot.Number) && own.Names(lot.Street, lot.Number) && all.Has(lot.Street, lot.Number);
        }
        Check(named, "uptown: its district, every lot's address and its whole streets are named to the district save, and are its own places alone when scoped");
        Check(all.OnGround(0f, 600f) && !all.OnGround(0f, 470f) && all.NearestExit(0f, 600f, out PlaceExit exit) && exit.District == "ba:neighborhood_midtown"
              && all.DoorOf(plan[0].Street, plan[0].Number, out PlaceExit door) && !DistrictSaveRules.IsModId(door.District),
            "uptown: the player and a car on its ground are written at the way out on 1st Avenue in Midtown, a car inside one of its buildings parked in a game district");
        Check(own != null && !own.Names("reallife:street_provinglane", 1) && !own.NamesDistrict("reallife:district_elsewhere"), "uptown: its own places never sweep in another feature's");
        DistrictPlaces.Reset();

        // Every kind of record that can name it: a kind of its own each, and the way each goes.
        Check(DistrictSaveRules.Code(SaveKind.Registration) == "reg" && DistrictSaveRules.Code(SaveKind.DistrictStats) == "stat" && DistrictSaveRules.Code(SaveKind.MarketEvent) == "mkt"
              && DistrictSaveRules.Code(SaveKind.Demand) == "dem" && DistrictSaveRules.Code(SaveKind.MapFilter) == "filter" && DistrictSaveRules.Code(SaveKind.Vehicle) == "veh"
              && DistrictSaveRules.Way(SaveKind.Registration) == SaveWay.Leaves && DistrictSaveRules.Way(SaveKind.DistrictStats) == SaveWay.Leaves
              && DistrictSaveRules.Way(SaveKind.Demand) == SaveWay.LeavesFromGame && DistrictSaveRules.Way(SaveKind.MapFilter) == SaveWay.Leaves
              && DistrictSaveRules.Way(SaveKind.Vehicle) == SaveWay.StandsIn,
            "uptown: its registrations, its stats, its market events and its ticked row of the city map's filters leave the save, its products' demand leaves the game's rows, "
            + "and a car parked there stands in at the way out");
    }

    // ---------------------------------------------------------------- the city check with Uptown

    private static void UptownCityCheck()
    {
        var census = new CityCensus();
        foreach (string d in NyDistricts.NewYork)
        {
            for (int i = 0; i < 20; i++)
            {
                census.Add(d);
            }
            census.GameDistricts.Add(d);
        }
        for (int i = 0; i < 21; i++)
        {
            census.Add(UptownId);
        }
        census.GameDistricts.Add(UptownId);
        census.Scenes.Add("LowerManhattan");
        var verdict = CityRules.Decide(census, new CityVerdict());
        Check(verdict.Kind == CityKind.NewYork && verdict.NewDistricts.Count == 0 && verdict.ModDistricts.Count == 1 && verdict.ModDistricts[0] == UptownId && verdict.NewYorkFeatures,
            "uptown: New York with Uptown is the New York the mod knows, its district the mod's own and not a free update's (" + verdict.Kind + ")");
        Check(CityRules.Line(verdict, census).Contains("the mod's own: " + UptownId + " (21)"), "uptown: the city check's line names it as the mod's own");
        var bare = new CityCensus();
        foreach (string d in NyDistricts.NewYork)
        {
            bare.Add(d);
            bare.GameDistricts.Add(d);
        }
        bare.GameDistricts.Add(UptownId);
        CityRules.Decide(bare, verdict);
        Check(verdict.Kind == CityKind.NewYork && !verdict.Elsewhere.Contains(UptownId) && bare.Here("ba:neighborhood_midtown") && !bare.Here(UptownId),
            "uptown: among the game's districts with no building standing, it is no other city's");
        Check(NyDistricts.Mine(UptownId) && !NyDistricts.Known(UptownId) && !NyDistricts.Mine("ba:neighborhood_midtown") && !NyDistricts.Mine(null) && !NyDistricts.Mine("ba:neighborhood_jfkairport"),
            "uptown: the mod's own district is told by its id, and is none of the seven");
    }

    // ---------------------------------------------------------------- every table of the mod's by district, with Uptown and without

    private static void UptownTables()
    {
        var game = new List<string>(NyDistricts.NewYork) { NyDistricts.Global };
        var withUptown = new List<string>(game) { UptownId };

        // Precincts (left out): six stations, New York's districts first; Uptown sorts after them and never takes one.
        var districts = new SortedDictionary<string, int>(NyDistricts.KnownFirst) { { UptownId, 21 } };
        foreach (string d in NyDistricts.NewYork)
        {
            districts[d] = 20;
        }
        var stations = new List<string>();
        foreach (string d in districts.Keys)
        {
            if (stations.Count < 6 && d != "ba:neighborhood_thehamptons")
            {
                stations.Add(d);
            }
        }
        Check(stations.Count == 6 && !stations.Contains(UptownId) && NyDistricts.KnownFirst.Compare(UptownId, "ba:neighborhood_thehamptons") > 0,
            "uptown, precincts: left out, the six stations stay in New York's districts and its streets are served by the nearest");

        // Hotels (left out): every slot in one of New York's districts.
        bool hotels = true;
        foreach (HotelSlot slot in TravelCatalogue.Hotels)
        {
            hotels &= NyDistricts.Known(slot.Neighbourhood);
        }
        Check(hotels, "uptown, hotels: left out, no hotel slot is in it");

        // Broadband (left out): the player's own network is New York's seven; Uptown has no place in it and costs nothing to ask about.
        Check(BroadbandRules.District(UptownId) == -1 && BroadbandRules.BuildCost(BroadbandRules.District(UptownId), 100) == 0, "uptown, broadband: left out of the player's own network");

        // Internet (takes part): reached by the providers that reach every district, wireless and copper.
        int reach = 0;
        for (int p = 0; p < InternetCatalogue.Providers.Length; p++)
        {
            reach += InternetRules.Reaches(p, UptownId) ? 1 : 0;
        }
        Check(reach == 2 && InternetRules.Reaches(0, "ba:neighborhood_midtown"), "uptown, internet: takes part, wireless and copper reach its businesses, New York's districts as before");

        // Civic wards (takes part): an eighth ward after New York's seven, within the count's room, no saved index of the seven moved.
        var wards = NyDistricts.Ordered(withUptown, null, new List<string>());
        var before = NyDistricts.Ordered(game, null, new List<string>());
        bool kept = wards.Count == before.Count + 1 && wards[wards.Count - 1] == UptownId;
        for (int i = 0; i < before.Count; i++)
        {
            kept &= wards[i] == before[i];
        }
        Check(kept && wards.Count <= CivicCatalogue.MaxDistricts, "uptown, civic wards: takes part, an eighth ward, New York's seven where they were");

        // The city economy (takes part): a district's income from its own class shares, between its neighbours'; theirs as before.
        int upIncome = CityModel.StartIncome(3, 44, 53), mid = CityModel.StartIncome(0, 6, 94), kitchen = CityModel.StartIncome(6, 82, 12);
        CityState city = CityModel.Open(7, 1);
        CityModel.Know(city, "ba:neighborhood_midtown", mid);
        CityModel.Know(city, UptownId, upIncome);
        Check(upIncome == 1176 && upIncome > kitchen && upIncome < mid && city.District(UptownId) != null && city.District("ba:neighborhood_midtown").Start == mid,
            "uptown, city economy: takes part, its income starts at " + upIncome + ", between Hell's Kitchen's " + kitchen + " and Midtown's " + mid);

        // Brands (takes part): by ordinal order it comes after every district of the game's, whose picks keep their places.
        var brandOrder = new List<string>(withUptown);
        brandOrder.Sort(string.CompareOrdinal);
        Check(brandOrder[brandOrder.Count - 1] == UptownId, "uptown, brands: takes part, after the game's districts in the brands' order");

        // Flare (takes part in New York): a home district of the city's people after the seven; another city's Flare keeps New York's seven.
        var flareAway = NyDistricts.Ordered(withUptown, NyDistricts.Known, new List<string>());
        Check(wards.Contains(UptownId) && !flareAway.Contains(UptownId) && flareAway.Count == NyDistricts.NewYork.Length, "uptown, Flare: takes part in New York, after the seven");

        // The district view (takes part): its crimes logged by its own id like any district's.
        var log = new RealLifeOverhaul.Districts.DistrictLog();
        log.Add(10, UptownId);
        log.Add(11, "ba:neighborhood_midtown");
        Check(log.Count(UptownId, 11, 7) == 1 && log.Count("ba:neighborhood_midtown", 11, 7) == 1, "uptown, district view: takes part, its crimes counted by its own id");

        // Online (takes part): after the districts known, whose orders keep their index.
        var online = new OnlineState();
        online.Districts.AddRange(game);
        int midIndex = online.District("ba:neighborhood_midtown");
        online.Districts.Add(UptownId);
        Check(online.District(UptownId) == game.Count && online.District("ba:neighborhood_midtown") == midIndex, "uptown, Online: takes part, after the known districts, their orders' indices kept");

        // Fame (takes part): its people's opinion of the player kept by its own id.
        var fame = new RealLifeOverhaul.Fame.FameFile();
        Check(fame.DistrictOrNew(UptownId) != null && fame.District(UptownId) != null && fame.District("ba:neighborhood_midtown") == null, "uptown, Fame: takes part, its opinion kept by its own id");

        // Social (takes part): a person whose home was dealt before Uptown keeps its name, as Uptown comes after the game's districts.
        var people = new SocialPerson[24];
        SocialCatalogue.MakeCast(people, 5, 1, 112, game.Count);
        bool homes = true;
        foreach (SocialPerson p in people)
        {
            homes &= p == null || (p.District >= 0 && p.District < game.Count && withUptown[p.District % withUptown.Count] == game[p.District % game.Count]);
        }
        Check(homes, "uptown, Social: takes part, a person's home district keeps its name with Uptown after the game's");

        // Faults, Hiring and Estate take part by the game's own id on its buildings and registrations: nothing of theirs is by a list of districts.
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        var all = new StringBuilder(1 << 17);
        bool noLinq = true, noChance = true, byContract = true, noHarmony = true, oneFrame = true, noSend = true;
        foreach (string path in Directory.GetFiles(System.IO.Path.Combine(scripts, "Uptown"), "*.cs"))
        {
            string name = System.IO.Path.GetFileName(path);
            if (!(name.StartsWith("Life", StringComparison.Ordinal) || name.StartsWith("District", StringComparison.Ordinal)
                  || name.StartsWith("Buildings", StringComparison.Ordinal)))
            {
                continue;   // the ground's files, held to its own rules by NavCheck's UptownCheck
            }
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            all.Append(text).Append('\n');
            noLinq &= !text.Contains("using System.Linq");
            noChance &= !Regex.IsMatch(text, @"new System\.Random|new Random\(|Random\.Range|Random\.value");
            byContract &= !Regex.IsMatch(text, @"GetField\(|GetMethod\(|GetProperty\(|BindingFlags");
            noHarmony &= !text.Contains("Harmony");
            // The ground's checks hold the ground's files to these; the district's life holds its own to them here.
            oneFrame &= !Regex.IsMatch(text, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate)\s*\(") || name == "LifeDirector.cs";
            noSend &= !Regex.IsMatch(text, @"\bSend\(|MP(?:Server|Client|Config)");
        }
        string code = all.ToString();
        Check(code.Length > 0 && noLinq && noHarmony, "uptown: no LINQ and no Harmony in the district's files");
        Check(oneFrame && noSend, "uptown: of the district's files only its director has a frame method, and none sends anything over Going Public");
        Check(noChance && Occurs(code, "UnityEngine.Random.InitState(") == 4 && Occurs(code, "UnityEngine.Random.state = was;") == 4
              && code.Contains("NewBuildingsRules.Hash(DistrictValues.Id + \"|first|\"") && code.Contains("NewBuildingsRules.Hash(DistrictValues.Id + \"|more|\"")
              && code.Contains("NewBuildingsRules.Hash(lease.BusinessType + \"|\" + lease.Owner + \"|\" + index.ToString(")
              && code.Contains("NewBuildingsRules.Hash(DistrictValues.Id + \"|stats|\" + save.seed)")
              && code.Contains("NewBuildingsRules.Hash(made.Spec.Id + \"|opened|\" + seed)"),
            "uptown: nothing of its is chance: the game's own generator (for the district, or its buildings new to a save alone), its start of a leased business and the opening pass's run with "
            + "the engine's chance seeded from the save, put back after; its stats' days from the save's seed");
        Check(byContract, "uptown: every member of the game's it reads by name is a line of Core/GameContract");
        Check(!Regex.IsMatch(code, @"GetBuildingRegistration\("), "uptown: it never asks the game for its buildings' registrations (the game would make an empty one and the kept one be let go)");

        // Its game files write the district's own list alone of the game's lists by district, and nothing else of the mod writes the game's tables of districts.
        string lifeGame = Read(@"Uptown\LifeGame.cs") + Read(@"Uptown\LifeGame.Buildings.cs") + Read(@"Uptown\LifeGame.Map.cs") + Read(@"Uptown\LifeGame.Tests.cs") + Read(@"Uptown\LifeGame.Player.cs");
        bool ownList = lifeGame.Length > 0;
        foreach (string line in lifeGame.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (line.Contains("AllNeighbourhoodBuildings") && !trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                ownList &= line.Contains("DistrictValues.Id");
            }
        }
        Check(ownList, "uptown: of the game's lists of buildings by district it writes its own alone (never one a held unit is in)");
        var others = new List<string>();
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = System.IO.Path.GetFileName(path);
            if (name.StartsWith("LifeGame", StringComparison.Ordinal) || name == "GameContract.cs")
            {
                continue;
            }
            string text = File.ReadAllText(path);
            if (text.Contains("GameContract.DistrictLookup") || text.Contains("GameContract.DistrictArray") || text.Contains("GameContract.ClassShares")
                || text.Contains("GameContract.WholesalersByDistrict") || text.Contains("GameContract.DistrictZones") || text.Contains("idealAvailableBuildingsInNeighborhood ="))
            {
                others.Add(name);
            }
        }
        Check(others.Count == 0, "uptown: only its game files write the game's lookups of districts, its run-long tables and its targets of units to let" + (others.Count > 0 ? ": " + string.Join(", ", others) : string.Empty));
        string leave = MemberText(lifeGame, "public static void LeaveLists()");
        Check(leave.Contains("BuildingHelper.allBuildings.Remove(Entered[i]);") && leave.Contains("district.Clear();") && Occurs(leave, ".Remove(") == 1,
            "uptown: standing down, its buildings leave the game's lists and stay in its lookup by address, for anything still holding one");

        // The district among the game's at once with a list of its buildings (its stats never meet a missing one), its map row once its zone is in.
        string know = MemberText(lifeGame, "public static bool Know(out string why)");
        Check(know.Contains("BuildingHelper.AllNeighbourhoodBuildings[DistrictValues.Id] = new List<Building>();") && know.Contains("lookup[DistrictValues.Id] = _record;")
              && lifeGame.Contains("d.hasCityMapFilter = false;") && lifeGame.Contains("_record.hasCityMapFilter = true;"),
            "uptown: among the game's districts with a list of its buildings at once, its map row only once its zone is in");
        string rows = MemberText(lifeGame, "private static void PeopleRows(DistrictFigures f)");
        Check(rows.Contains("shares.Count > 0 && !shares.ContainsKey(DistrictValues.Id)") && rows.Contains("prices.Count > 0 && !prices.ContainsKey(DistrictValues.Id)"),
            "uptown: its rows go into the game's run-long class tables only once the game has made them (its own start throws on a row put there before it)");
        string unload = MemberText(lifeGame, "public static void Unload()");
        Check(unload.Contains("Unlist();") && unload.Contains("UnityEngine.Object.Destroy(_record);") && unload.IndexOf("Unlist();", StringComparison.Ordinal) < unload.IndexOf("Destroy(_record)", StringComparison.Ordinal),
            "uptown: when its city goes, nothing of it stays in the game's lists, and its record goes last");
        string map = MemberText(lifeGame, "public static bool Map(float x1, float z1, float x2, float z2, out string why)");
        Check(map.Contains("DistrictValues.SouthClear(") && map.IndexOf("SouthClear(", StringComparison.Ordinal) < map.IndexOf("DistrictValues.ZoneOver(", StringComparison.Ordinal),
            "uptown: its quad is cleared of the game's zones before it is laid");
        string plan = MemberText(lifeGame, "private static void Add(IReadOnlyList<Lot> all, List<BuildingsLot> into)");
        Check(plan.Contains("Avenue = BuildingsRules.FrontsAvenue(lot.Facing)") && !plan.Contains("MadeStreets"),
            "uptown: a lot's avenue told from its own facing, never from streets another step may not have made yet");

        // Its place among the district save's features, and its own line kept by the save tools.
        string director = Read(@"Uptown\LifeDirector.cs");
        Check(director.Contains("DistrictPlaces.Add(PlacesOwner, Fill, Early);") && director.Contains("DistrictSaveDirector.KeepsAlso(LifeStore.Key);")
              && director.Contains("public const string PlacesOwner = \"uptown\";"),
            "uptown: it names its places to the district save with an early step, and the save tools keep its line with the district's records");
        string fill = MemberText(director, "private static void Fill(PlaceSet set)");
        Check(fill.Contains("set.District(DistrictValues.Id);") && fill.Contains("set.Street(") && fill.Contains("set.Ground(") && fill.Contains("set.Exit(") && fill.Contains("set.Door(")
              && fill.Contains("exit.District);") && fill.IndexOf("if (!_known)", StringComparison.Ordinal) < fill.IndexOf("set.District", StringComparison.Ordinal),
            "uptown: its fill names its district once it is among the game's, then its streets, ground, way out and every standing door, a car inside one parked in a game district");
        string knowWhen = MemberText(director, "private static void Know(string when)");
        Check(knowWhen.Contains("GroundSession.Agrees(out string why) || !LifeGame.NewYorkSave(SaveGameManager.Current, out why)"),
            "uptown: among the game's districts only where the ground's verdict lets it (the option, Going Public) and the save is New York's");
        string begin = MemberText(director, "private static void Begin()");
        Check(begin.IndexOf("LifeGame.Unload();", StringComparison.Ordinal) >= 0
              && begin.IndexOf("LifeGame.Unload();", StringComparison.Ordinal) < begin.IndexOf("_known = _lives = _down = false;", StringComparison.Ordinal),
            "uptown: a city whose director never started gives back its record and rows at the next load");

        // The load's order (Core/RealLifeOverhaulMod): registered at game start, its director before the made buildings', settled before the district save's step.
        string entry = Read(@"Core\RealLifeOverhaulMod.cs");
        int register = entry.IndexOf("Uptown.LifeDirector.Register(context.Logger)", StringComparison.Ordinal), city = entry.IndexOf("public class RealLifeOverhaulCity", StringComparison.Ordinal);
        int added = entry.IndexOf("Guard.Add<Uptown.LifeDirector>(_root)", StringComparison.Ordinal), made = entry.IndexOf("Guard.Add<NewBuildings.NewBuildingsDirector>(_root)", StringComparison.Ordinal);
        int prewarm = entry.IndexOf("ScenePrewarm.Begin()", StringComparison.Ordinal), settle = entry.IndexOf("Uptown.LifeDirector.Settle()", StringComparison.Ordinal);
        int district = entry.IndexOf("DistrictSave.DistrictSaveDirector.City(_root)", StringComparison.Ordinal);
        Check(register > 0 && register < city && added > city && added < made && prewarm > made && settle > prewarm && district > settle,
            "uptown: registered with the district save at game start; its buildings declared before the made buildings' engine makes them; in the game's lists before the district save's step");

        // It lives at a load with its buildings or not at all: Settle, a step of its own, takes it out where none stands or its director never started.
        string settleText = MemberText(director, "public static void Settle()");
        int away = settleText.IndexOf("if (Instance == null || FrameGuard.Off)", StringComparison.Ordinal), loop = settleText.IndexOf("for (int i = 0; i < Declared.Count; i++)", StringComparison.Ordinal);
        int none = settleText.IndexOf("if (!_lives)", StringComparison.Ordinal);
        Check(away > 0 && loop > away && none > loop && Occurs(settleText, "Close(") == 2,
            "uptown: a load where its director never started (its feature away in another city), or where no building of it stands, takes it out of the game's districts at the city's step");

        // The stand-down: the player out first, then its records kept, its buildings out of the lists and hidden (never destroyed), the district unlisted.
        string close = MemberText(director, "private static void Close(string why, bool sessionChanged)");
        int keepOut = close.IndexOf("DistrictSaveDirector.KeepOut(PlacesOwner, why)", StringComparison.Ordinal), lists = close.IndexOf("LifeGame.LeaveLists();", StringComparison.Ordinal);
        int hide = close.IndexOf("CopyGame.Show(Standing[i].Controller, false);", StringComparison.Ordinal), unlist = close.IndexOf("LifeGame.Unlist();", StringComparison.Ordinal);
        Check(keepOut >= 0 && lists > keepOut && hide > lists && unlist > hide && !code.Contains("Destroy(made") && close.Contains("if (sessionChanged && GoingPublic.InSession)"),
            "uptown: standing down keeps its records first, then leaves the game's lists, hides its buildings and unlists the district; only a session's turn is told in a notice");
        string down = MemberText(director, "private IEnumerator Down(string why, bool sessionChanged)");
        Check(down.IndexOf("LifeGame.WalkOut();", StringComparison.Ordinal) >= 0 && down.IndexOf("Close(why, sessionChanged);", StringComparison.Ordinal) > down.IndexOf("LifeGame.WalkOut();", StringComparison.Ordinal),
            "uptown: the player is taken out of a building of it by the game's own way out before it stands down");
        Check(director.Contains("UptownLots.Closing += OnClosing;") && director.Contains("private static void OnClosing(string why)") && !director.Contains("Guard.Wrap<string>(FrameGuard, OnClosing)"),
            "uptown: the ground's clean-up reaches it even though the feature's guards are off by then");

        // Nothing per frame: one time comparison a frame, once a second a look at the session and the ground, the hot spots' clock open.
        string update = MemberText(director, "private void OnUpdate()");
        Check(director.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && update.IndexOf("if (now < _nextSlow)", StringComparison.Ordinal) >= 0
              && update.IndexOf("return;", StringComparison.Ordinal) > update.IndexOf("if (now < _nextSlow)", StringComparison.Ordinal),
            "uptown: idle, one time comparison a frame; once a second a look at the session and the ground");
    }

    // ---------------------------------------------------------------- the texts

    private static void UptownTexts()
    {
        string folder = LocalesFolder();
        string keysFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, "..", "Scripts", "Uptown", "LifeText.cs"));
        string keysText = File.Exists(keysFile) ? File.ReadAllText(keysFile) : string.Empty;
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        var keys = new List<string> { UptownId };
        foreach (Match m in Regex.Matches(keysText, @"public const string \w+ = ""(reallife_[a-z_]+)"";"))
        {
            keys.Add(m.Groups[1].Value);
        }
        bool there = en != null && pt != null && zh != null && keys.Count == 52 && LifeText.District == UptownId;
        string missing = null;
        bool short240 = true;
        foreach (string key in keys)
        {
            bool all3 = en != null && pt != null && zh != null && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && SameHoles(en[key], pt[key], zh[key]);
            if (!all3)
            {
                there = false;
                missing = missing ?? key;
            }
            short240 &= en == null || !en.ContainsKey(key) || en[key].Length <= 240;
        }
        Check(there, "uptown: every text is in en, pt and zh-cn with the same {holes} (" + (missing ?? keys.Count + " texts") + ")");
        Check(en != null && en.TryGetValue(UptownId, out string name) && name == "Uptown" && zh != null && zh.TryGetValue(UptownId, out string zhName) && zhName == "上城",
            "uptown: the district's name is its id's text, as the game names its own (Uptown, 上城)");
        Check(short240, "uptown: every notice is at most 240 characters in English");
        // The feature's title is Uptown's ground's key (GroundText.Title), shared by the district's features: this side's texts
        // never define it, and its words are the district's name.
        Check(en != null && en.TryGetValue("reallife_meta_title_uptown", out string title) && en.TryGetValue(UptownId, out string district) && title == district
              && !keysText.Contains("reallife_meta_title_uptown"),
            "uptown: the feature's title is the ground's key, not this side's, in the district's name");
    }
}
