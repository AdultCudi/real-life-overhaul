using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Uptown's band filled (the owner, 23 Oct 2026: "All 54 blocks", "Mostly built"), on the real plan of its 422 lots: the lots
// built from the first day and the open ones (the first block's old quarter kept, the landmarks' own blocks open, a share
// open on every other block, the lot a tree of the game's leans over kept empty, the same in any order and on every
// machine), the eight offices leased to the city's rival builders (office lots outside the first block, one a block, as
// many as the rivals), the landmarks' sites and the developers' lots on the filled plan, the lanes' squares and the boxes'
// reach for the made buildings' ground check against a whole pass, the other structures a building takes where no design
// of its own size fits its lot, the fill's load work timed, and the sources: the load's declarations, the generator for new
// buildings alone, the leases by the game's own start of a business, the map's row placed, the look check, the tests in
// the run, the wiring; and the texts.
internal static partial class Program
{
    private static void UptownFillChecks()
    {
        List<BuildingsLot> plan = UptownRealPlan();
        UptownFillRules(plan);
        UptownFillLeases(plan);
        UptownFillLeaseNames();
        UptownFillLandmarks(plan);
        UptownFillLanes();
        UptownFillBoxes();
        UptownFillOthers(plan);
        UptownFillLoad(plan);
        UptownFillSources();
        UptownFillTexts();
    }

    // The band's lots as LifeGame.Lots reads them from the ground's plan (UptownLots.All), their blocks with them.
    private static List<BuildingsLot> UptownRealPlan()
    {
        var plan = new List<BuildingsLot>(512);
        var all = new List<Lot>(UptownLots.All);
        all.AddRange(UptownLots.Places);   // as LifeGame.Lots reads the plan: the big places' ground after the lots
        foreach (Lot l in all)
        {
            plan.Add(new BuildingsLot
            {
                Id = l.Id, Street = l.Street, Number = l.Number, Block = l.Block, First = l.First, Avenue = BuildingsRules.FrontsAvenue(l.Facing), X = l.X, Z = l.Z,
                Facing = l.Facing, Width = l.Width, Depth = l.Depth, Class = (BuildingsClass)(int)l.Class, Sizes = l.Sizes
            });
        }
        return plan;
    }

    private static HashSet<string> UptownIds(List<BuildingsLot> plan, List<int> indices)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (int i in indices)
        {
            ids.Add(plan[i].Id);
        }
        return ids;
    }

    // ---------------------------------------------------------------- built from the first day, and open

    private static void UptownFillRules(List<BuildingsLot> plan)
    {
        Check(BuildingsRules.OpenLots == 30, "uptown fill: thirty lots stay open for the construction game (the owner, 25 Oct 2026), the rest built");
        var built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> builtIds = UptownIds(plan, built);
        HashSet<string> legacy = UptownIds(plan, BuildingsRules.DayOne(plan, 0f, 1f, new List<int>(), LandmarkRules.OwnBlock));
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        int first = 0, firstBuilt = 0;
        foreach (BuildingsLot lot in plan)
        {
            first += lot.First ? 1 : 0;
            firstBuilt += lot.First && builtIds.Contains(lot.Id) ? 1 : 0;
        }
        int notClear = 0;
        foreach (BuildingsLot lot in plan)
        {
            notClear += BuildingsRules.Clear(lot.Id) ? 0 : 1;
        }
        Check(plan.Count == 630 && built.Count == 567 && legacy.Count == 19 && first == 78 && firstBuilt == 44 && notClear == 3,
            "uptown fill: of the band's 628 lots and two big places' ground, 567 lots are built from the first day, 60 left open (30 for the construction game, 30 the landmarks' sites) and one kept empty; 44 of the first block's 78 (" + plan.Count + ", "
            + built.Count + ", " + legacy.Count + ", " + firstBuilt + " of " + first + ", " + notClear + " not clear)");
        bool treeLot = plan.Exists(l => l.Id == "uptown_1_14_004") && !BuildingsRules.Clear("uptown_1_14_004") && !builtIds.Contains("uptown_1_14_004")
                       && !leased.Contains("uptown_1_14_004") && BuildingsRules.Clear("uptown_1_14_005") && !BuildingsRules.Clear(null) && !BuildingsRules.Clear(string.Empty);
        Check(treeLot, "uptown fill: the lot a tree of the game's leans over (uptown_1_14_004, the ground's scan) is neither built nor leased, and its neighbours are clear");
        bool keepsOld = legacy.IsSubsetOf(builtIds) && leased.IsSubsetOf(builtIds);
        Check(keepsOld, "uptown fill: the first block's quarter built before the band was filled stays built (saves hold their records), and every leased office is built");

        var blocks = new SortedDictionary<int, List<BuildingsLot>>();
        foreach (BuildingsLot lot in plan)
        {
            if (!blocks.TryGetValue(lot.Block, out List<BuildingsLot> list))
            {
                list = new List<BuildingsLot>();
                blocks.Add(lot.Block, list);
            }
            list.Add(lot);
        }
        bool shares = true, landmarksOpen = true;
        int openBlocks = 0, blocksMay = 0;
        string wrong = null;
        foreach (KeyValuePair<int, List<BuildingsLot>> block in blocks)
        {
            int open = 0, fixedOnes = 0;
            bool firstBlock = false;
            foreach (BuildingsLot lot in block.Value)
            {
                open += builtIds.Contains(lot.Id) || !BuildingsRules.Clear(lot.Id) ? 0 : 1;
                fixedOnes += legacy.Contains(lot.Id) || leased.Contains(lot.Id) || !BuildingsRules.Clear(lot.Id) ? 1 : 0;
                firstBlock |= lot.First;
            }
            if (LandmarkRules.OwnBlock(block.Key))
            {
                bool all = open == block.Value.Count - fixedOnes;
                landmarksOpen &= all;
                wrong = all ? wrong : wrong ?? "block " + block.Key + " (a landmark's) has " + open + " open of " + block.Value.Count;
                continue;
            }
            shares &= open <= 1;
            openBlocks += open;
            blocksMay += block.Value.Count > fixedOnes ? 1 : 0;
            wrong = open <= 1 ? wrong : wrong ?? "block " + block.Key + " has " + open + " open";
        }
        Check(blocks.Count == 51 && landmarksOpen && shares && openBlocks == BuildingsRules.OpenLots && blocksMay > BuildingsRules.OpenLots,
            "uptown fill: each landmark's own block open but for the old quarter's lots; of the other " + blocksMay + " blocks with lots " + openBlocks + " keep one open, none more"
            + (wrong != null ? " (" + wrong + ")" : string.Empty));
        Check(LandmarkRules.OwnBlock(111) && LandmarkRules.OwnBlock(113) && LandmarkRules.OwnBlock(110) && !LandmarkRules.OwnBlock(106) && !LandmarkRules.OwnBlock(112)
              && !LandmarkRules.OwnBlock(201),
            "uptown fill: the blocks kept open are the tower's, the mall's and the stadium's own (111, 113, 110)");

        bool sorted = true;
        for (int i = 1; i < built.Count; i++)
        {
            sorted &= string.CompareOrdinal(plan[built[i - 1]].Id, plan[built[i]].Id) < 0;
        }
        bool same = true;
        var order = new int[plan.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        for (int turn = 0; turn < 12; turn++)
        {
            Shuffle(order, turn + 40);
            var shuffled = new List<BuildingsLot>(plan.Count);
            foreach (int i in order)
            {
                shuffled.Add(plan[i]);
            }
            same &= UptownIds(shuffled, BuildingsRules.Built(shuffled, LandmarkRules.OwnBlock, 0f, 1f, new List<int>())).SetEquals(builtIds);
            var leasedAgain = BuildingsRules.Leased(shuffled, new List<int>());
            var lease0 = BuildingsRules.Leased(plan, new List<int>());
            for (int k = 0; k < lease0.Count && k < leasedAgain.Count; k++)
            {
                same &= shuffled[leasedAgain[k]].Id == plan[lease0[k]].Id;
            }
        }
        Check(sorted && same, "uptown fill: the built lots in the order of their ids, and the same lots and leases from the plan in any order");

        var none = BuildingsRules.Built(plan, null, 0f, 1f, new List<int>());
        int noneFirst = 0;
        foreach (int i in none)
        {
            noneFirst += plan[i].First ? 1 : 0;
        }
        Check(none.Count > built.Count && noneFirst > firstBuilt && BuildingsRules.Built(new List<BuildingsLot>(), LandmarkRules.OwnBlock, 0f, 1f, new List<int>()).Count == 0
              && BuildingsRules.Built(null, null, 0f, 1f, new List<int>()).Count == 0,
            "uptown fill: with no block kept open the first block is built to its shares too; no plan builds nothing");

        int developers = 0, openFirst = 0;
        foreach (BuildingsLot lot in plan)
        {
            if (!builtIds.Contains(lot.Id))
            {
                developers += DevelopmentRules.Theirs(lot.Id) ? 1 : 0;
                openFirst += lot.First ? 1 : 0;
            }
        }
        Check(developers >= 25 && openFirst >= 20, "uptown fill: the city's developers have " + developers + " open lots to build on, " + openFirst
                                                  + " of the open lots in the first block, where they build first and the landmarks stand");

        var options = new List<BuildingsStructure>
        {
            new BuildingsStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_office" },
            new BuildingsStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_retail" },
            new BuildingsStructure { Size = "ba:buildingsize_j", Version = 1, Type = "ba:buildingtype_office" },
            new BuildingsStructure { Size = "ba:buildingsize_k", Version = 1, Type = "ba:buildingtype_office" }
        };
        options.Sort(BuildingsRules.CompareStructures);
        bool offices = true;
        var used = new HashSet<int>();
        foreach (BuildingsLot lot in plan)
        {
            int pick = BuildingsRules.PickOffice(lot.Id, options);
            offices &= pick >= 0 && options[pick].Type == BuildingsRules.OfficeType && pick == BuildingsRules.PickOffice(lot.Id, options);
            used.Add(pick);
        }
        var retail = new List<BuildingsStructure> { new BuildingsStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_retail" } };
        Check(offices && used.Count == 3 && BuildingsRules.PickOffice("x", retail) == -1 && BuildingsRules.PickOffice(null, options) == -1,
            "uptown fill: a leased lot is built with one of the game's office structures for its sizes, picked by its id, every one used; none where the game has none");
    }

    // ---------------------------------------------------------------- the offices leased to the city's builders

    private static void UptownFillLeases(List<BuildingsLot> plan)
    {
        var leased = BuildingsRules.Leased(plan, new List<int>());
        bool offices = true, outside = true, oneABlock = true;
        var blocks = new HashSet<int>();
        foreach (int i in leased)
        {
            offices &= plan[i].Class == BuildingsClass.Office;
            outside &= !plan[i].First;
            oneABlock &= blocks.Add(plan[i].Block);
        }
        Check(leased.Count == BuildingsRules.LeasedOffices && BuildingsRules.LeasedOffices == BuildCatalogue.RivalCount && offices && outside && oneABlock,
            "uptown fill: " + leased.Count + " office lots leased, one for each of the construction game's " + BuildCatalogue.RivalCount
            + " rival builders: office lots outside the first block (the landmarks' blocks), one a block");
        int rows = 0;
        var rowSeen = new HashSet<int>();
        foreach (int i in leased)
        {
            rows += rowSeen.Add(plan[i].Block / 100) ? 1 : 0;
        }
        Check(rows >= 2, "uptown fill: the builders' offices are spread over the band's rows (" + rows + " rows)");
        Check(BuildCatalogue.OfficeDayPerCrew > 0 && BuildCatalogue.OfficeAboutKey == "reallife_bld_office_about",
            "uptown fill: a builder's office is valued by its crews' days, and BizMan describes it in the texts");
        Check(BuildingsRules.Leased(null, new List<int>()).Count == 0 && BuildingsRules.Leased(new List<BuildingsLot>(), new List<int>()).Count == 0,
            "uptown fill: no plan leases nothing");
    }

    // ---------------------------------------------------------------- the landmarks on the filled plan

    // A lease's business is known by its type at its own lot, never by its name, which is text in the player's language: one
    // named in another language (or with another description) still stands, is held off the market and takes the lease's
    // name again; one of another type is still refused, and so are another player's, the player's and a building not an office.
    private static void UptownFillLeaseNames()
    {
        const string Type = "reallife:businesstype_construction", Studio = "ba:businesstype_graphicdesigner", Name = "Ostrander Building Co.";
        const string About = "reallife_bld_office_about";
        LeaseVerdict Of(string type, string name, string about, bool fresh, bool toLet, bool foreign, bool players, string building, out string why)
        {
            return BuildingsRules.LeaseOf(foreign, players, building, type, name, about, fresh, toLet, Type, Name, About, out why);
        }
        LeaseVerdict At(string type, string name, string about) => Of(type, name, about, false, false, false, false, BuildingsRules.OfficeType, out _);
        bool theirs = At(Type, Name, About) == LeaseVerdict.Theirs;
        bool otherLanguage = At(Type, "Ostrander Bauunternehmen", About) == LeaseVerdict.Renamed && At(Type, Name, "reallife_bld_office_about_old") == LeaseVerdict.Renamed
                             && At(Type, null, null) == LeaseVerdict.Renamed
                             && Of(Type, "Ostrander Bauunternehmen", About, false, true, false, false, BuildingsRules.OfficeType, out _) == LeaseVerdict.Renamed;
        bool otherType = Of(Studio, Name, About, false, false, false, false, BuildingsRules.OfficeType, out string why) == LeaseVerdict.Refused
                         && why == "a business of " + Studio + " is in it";
        bool refusals = Of(Type, Name, About, false, false, true, false, BuildingsRules.OfficeType, out string foreign) == LeaseVerdict.Refused
                        && foreign == "it is another player's in this session"
                        && Of(Type, Name, About, false, false, false, true, BuildingsRules.OfficeType, out string players) == LeaseVerdict.Refused
                        && players == "the player rents or owns it"
                        && Of(Type, Name, About, false, false, false, false, "ba:buildingtype_retail", out string retail) == LeaseVerdict.Refused
                        && retail == "it was built as ba:buildingtype_retail, not an office";
        bool moveIn = Of(null, null, null, true, false, false, false, BuildingsRules.OfficeType, out _) == LeaseVerdict.MoveIn
                      && Of(BuildingsRules.EmptyBusinessType, null, null, false, true, false, false, BuildingsRules.OfficeType, out _) == LeaseVerdict.MoveIn
                      && Of(Studio, null, null, false, true, false, false, BuildingsRules.OfficeType, out _) == LeaseVerdict.Refused
                      && Of(string.Empty, null, null, false, false, false, false, BuildingsRules.OfficeType, out _) == LeaseVerdict.Refused;
        bool leased = BuildingsRules.LeasedBy(Type, false, false, Type) && !BuildingsRules.LeasedBy(Studio, false, false, Type) && !BuildingsRules.LeasedBy(Type, true, false, Type)
                      && !BuildingsRules.LeasedBy(Type, false, true, Type) && !BuildingsRules.LeasedBy(Type, false, false, null);
        Check(theirs && otherLanguage && otherType && refusals && moveIn && leased,
            "uptown fill: a lease's business is known by its type at its own lot whatever its name: in another language it stands and takes the lease's name again, "
            + "and one of another type, another player's, the player's or on a building not an office is refused");

        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string game = File.ReadAllText(System.IO.Path.Combine(scripts, "Uptown", "LifeGame.Leases.cs")).Replace("\r\n", "\n");
        string director = File.ReadAllText(System.IO.Path.Combine(scripts, "Uptown", "LifeDirector.cs")).Replace("\r\n", "\n");
        string lease = MemberText(game, "public static string Lease(MadeBuilding made, DistrictLease lease, int index, GameInstance save, out bool movedIn, out bool renamed)");
        string rename = MemberText(game, "private static void Rename(BuildingRegistration reg, DistrictLease lease)");
        string changed = MemberText(game, "private static void Changed(BuildingRegistration reg)");
        int refused = lease.IndexOf("if (verdict == LeaseVerdict.Refused)", StringComparison.Ordinal), claim = lease.IndexOf("Claim(made, lease, index);", StringComparison.Ordinal);
        Check(refused > 0 && claim > refused && lease.Contains("BuildingsRules.LeaseOf(GoingPublic.Foreign(reg), reg.RentedByPlayer || reg.BuildingOwnedByPlayer, record.BuildingType,")
              && lease.Contains("else if (verdict == LeaseVerdict.Renamed)") && lease.Contains("Rename(reg, lease);") && !game.Contains("reg.BusinessName == lease.Name")
              && rename.Contains("reg.BusinessName = lease.Name;") && rename.Contains("reg.BusinessDescription = lease.DescriptionKey;") && rename.Contains("Changed(reg);")
              && changed.Contains("UpdateSign()") && changed.Contains("GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);")
              && MemberText(game, "public static bool Leased(MadeBuilding made, DistrictLease lease)")
                     .Contains("BuildingsRules.LeasedBy(reg.businessTypeName, reg.AvailableForRent, reg.RentedByPlayer, lease.BusinessType)")
              && director.Contains("int leased = Leases(save, out int renamed);") && director.Contains(" of them given their lessees' names again"),
            "uptown fill: the load holds every lease's business off the market but a refused one's, gives a renamed one the lease's name with its sign and the game's event, "
            + "knows a standing one by its type, and says how many were renamed");

        // The seed of a lease's start of a business: its type, its owner, its index and the save's seed, no text of a language.
        string started = MemberText(game, "private static void MoveIn(BuildingRegistration reg, DistrictLease lease, int index, AiBusinessDefault from, GameInstance save)");
        int seedAt = started.IndexOf("UnityEngine.Random.InitState(", StringComparison.Ordinal);
        string seed = seedAt >= 0 ? started.Substring(seedAt, started.IndexOf(";", seedAt, StringComparison.Ordinal) - seedAt) : string.Empty;
        Check(seed.Contains("lease.BusinessType") && seed.Contains("lease.Owner") && seed.Contains("index.ToString(System.Globalization.CultureInfo.InvariantCulture)")
              && seed.Contains("save.seed") && !Regex.IsMatch(seed, @"Name|Description|GetLocalization|\.T\(|Text\.")
              && Regex.Matches(game, @"Hash\([^;]*(?:Name|Description)").Count == 0,
            "uptown fill: a lease's business is started with the engine's chance seeded by its type, its owner, its index and the save, never by a name, "
            + "so every machine of a session starts it alike whatever its language (" + (seed.Length > 0 ? seed.Replace("\n", " ") : "no seed found") + ")");
    }

    private static void UptownFillLandmarks(List<BuildingsLot> plan)
    {
        HashSet<string> builtIds = UptownIds(plan, BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
        var lots = new List<SiteLot>();
        foreach (Lot l in LayoutRules.FirstLots)
        {
            lots.Add(new SiteLot
            {
                Id = l.Id, Block = l.Block, First = l.First, FirstDay = builtIds.Contains(l.Id), Avenue = l.Facing < 1f || l.Facing > 359f, X = l.X, Z = l.Z, Yaw = l.Yaw,
                Width = l.Width, Depth = l.Depth, Class = (int)l.Class
            });
        }
        var held = new List<int>();
        LandmarkSite tower = SiteOf(LandmarkKind.Tower, lots, null, held);
        held.Add(tower != null ? tower.Block : 0);
        LandmarkSite mall = SiteOf(LandmarkKind.Mall, lots, null, held);
        held.Add(mall != null ? mall.Block : 0);
        LandmarkSite stadium = SiteOf(LandmarkKind.Stadium, lots, null, held);
        bool own = tower != null && tower.Block == 111 && mall != null && mall.Block == 113 && stadium != null && stadium.Block == 110;
        bool noneBuilt = true;
        foreach (LandmarkSite site in new[] { tower, mall, stadium })
        {
            foreach (string id in site != null ? site.Lots : new List<string>())
            {
                noneBuilt &= !builtIds.Contains(id);
            }
        }
        Check(own && noneBuilt, "uptown fill: on the filled band each landmark still takes its own block, and none of its site's lots is built from the first day");
    }

    // ---------------------------------------------------------------- the lanes' squares

    private static void UptownFillLanes()
    {
        const int Points = 20176;
        var xs = new float[Points];
        var zs = new float[Points];
        var present = new bool[Points];
        uint seed = 2166136261u;
        for (int i = 0; i < Points; i++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            xs[i] = -3000f + (seed % 600000u) / 100f;
            seed = unchecked(seed * 1664525u + 1013904223u);
            zs[i] = -1500f + (seed % 250000u) / 100f;
            present[i] = i % 97 != 0;   // a few missing, as a lane point the game has dropped
        }
        var grid = new LaneGrid();
        grid.Fill(xs, zs, present, Points);
        bool same = true;
        int inside = 0, near = 0;
        for (int q = 0; q < 600; q++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            float cx = -3000f + (seed % 600000u) / 100f;
            seed = unchecked(seed * 1664525u + 1013904223u);
            float cz = -1500f + (seed % 250000u) / 100f;
            float facing = (seed >> 8) % 4u * 90f + (q % 3 == 0 ? 17f : 0f);
            float hw = 5f + q % 12, hd = 10f + q % 9;
            int brute = -1, bruteNear = -1;
            float best = float.MaxValue;
            for (int i = 0; i < Points; i++)
            {
                if (!present[i])
                {
                    continue;
                }
                if (brute < 0 && NewBuildingsRules.Inside(xs[i], zs[i], cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear))
                {
                    brute = i;
                }
                float dx = xs[i] - cx, dz = zs[i] - cz, sqr = dx * dx + dz * dz;
                if (sqr < best)
                {
                    best = sqr;
                    bruteNear = i;
                }
            }
            float reach = NewBuildingsRules.StreetReach + Math.Max(hw, hd);
            int gridInside = grid.Inside(cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear);
            int gridNear = grid.Nearest(cx, cz, reach, out float squared);
            same &= gridInside == brute && (best <= reach * reach ? gridNear == bruteNear && squared == best : gridNear == -1);
            inside += brute >= 0 ? 1 : 0;
            near += gridNear >= 0 ? 1 : 0;
        }
        Check(same && inside > 0 && inside < 600 && near > 0 && grid.Count == Points - (Points + 96) / 97,
            "uptown fill: the lanes' squares give a whole pass's answers, the lowest point inside a footprint and the nearest within reach, on 600 footprints over "
            + Points + " points (" + inside + " with a lane inside, " + near + " fronting one)");
        long began = Stopwatch.GetTimestamp();
        int hits = 0;
        for (int q = 0; q < 360; q++)
        {
            hits += grid.Inside(xs[q * 37] + 30f, zs[q * 37] + 30f, 90f, 6f, 12f, NewBuildingsRules.RoadClear) >= 0 ? 1 : 0;
            hits += grid.Nearest(xs[q * 37] + 30f, zs[q * 37] + 30f, 72f, out _) >= 0 ? 1 : 0;
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        Check(ms < 20.0 && hits > 0, "uptown fill: 360 buildings' lane checks in " + ms.ToString("F2") + " ms with the squares (wanted under 20 ms; a whole pass each is some 7 million point tests)");
    }

    // ---------------------------------------------------------------- the game's boxes by the rectangle's reach

    // The ground check's pass over the game's boxes (868 buildings and some 2,070 obstacles in the city's scenes, the game's
    // files): the turned test only for a box within the rectangle's reach finds the same first box as the turned test of
    // every box, for the band's 352 buildings, and in a fraction of the time.
    private static void UptownFillBoxes()
    {
        const int Boxes = 2934, Lots = 352;
        var box = new float[Boxes * 4];
        uint seed = 2166136261u;
        float Next(float from, float span)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            return from + (seed >> 8) % 1000000u / 1000000f * span;
        }
        for (int i = 0; i < Boxes; i++)
        {
            float x = Next(-4000f, 5600f), z = Next(-2500f, 3500f), w = Next(1f, 40f), d = Next(1f, 40f);
            box[i * 4] = x;
            box[i * 4 + 1] = z;
            box[i * 4 + 2] = i % 211 == 0 ? x - 1f : x + w;   // now and then a box with nothing in it, as an empty mesh gives
            box[i * 4 + 3] = z + d;
        }
        var cx = new float[Lots];
        var cz = new float[Lots];
        var facing = new float[Lots];
        var hw = new float[Lots];
        var hd = new float[Lots];
        for (int q = 0; q < Lots; q++)
        {
            cx[q] = Next(-800f, 1700f);
            cz[q] = Next(400f, 600f);
            facing[q] = (q % 4) * 90f + (q % 5 == 0 ? 23f : 0f);
            hw[q] = Next(4f, 12f);
            hd[q] = Next(10f, 9f);
        }
        int[] plain = new int[Lots], reached = new int[Lots];
        double plainMs = double.MaxValue, reachMs = double.MaxValue;
        for (int run = 0; run < 3; run++)
        {
            long a = Stopwatch.GetTimestamp();
            for (int q = 0; q < Lots; q++)
            {
                plain[q] = -1;
                for (int i = 0; i < Boxes; i++)
                {
                    int k = i * 4;
                    if (NewBuildingsRules.Overlaps(box[k], box[k + 1], box[k + 2], box[k + 3], cx[q], cz[q], facing[q], hw[q], hd[q], NewBuildingsRules.Gap))
                    {
                        plain[q] = i;
                        break;
                    }
                }
            }
            long b = Stopwatch.GetTimestamp();
            for (int q = 0; q < Lots; q++)
            {
                reached[q] = -1;
                NewBuildingsRules.Reach(facing[q], hw[q], hd[q], NewBuildingsRules.Gap, out float reachX, out float reachZ);
                for (int i = 0; i < Boxes; i++)
                {
                    int k = i * 4;
                    if (!NewBuildingsRules.Apart(box[k], box[k + 1], box[k + 2], box[k + 3], cx[q], cz[q], reachX, reachZ)
                        && NewBuildingsRules.Overlaps(box[k], box[k + 1], box[k + 2], box[k + 3], cx[q], cz[q], facing[q], hw[q], hd[q], NewBuildingsRules.Gap))
                    {
                        reached[q] = i;
                        break;
                    }
                }
            }
            long c = Stopwatch.GetTimestamp();
            plainMs = Math.Min(plainMs, (b - a) * 1000.0 / Stopwatch.Frequency);
            reachMs = Math.Min(reachMs, (c - b) * 1000.0 / Stopwatch.Frequency);
        }
        bool same = true;
        int met = 0;
        for (int q = 0; q < Lots; q++)
        {
            int first = -1;
            for (int i = 0; i < Boxes && first < 0; i++)
            {
                first = UptownMeets(box, i, cx[q], cz[q], facing[q], hw[q] + NewBuildingsRules.Gap, hd[q] + NewBuildingsRules.Gap) ? i : -1;
            }
            same &= plain[q] == reached[q] && reached[q] == first;
            met += first >= 0 ? 1 : 0;
        }
        Check(same && met > 0 && met < Lots && reachMs < plainMs && reachMs < 10.0,
            "uptown fill: the ground check's boxes tried by the rectangle's reach first find the same box as the turned test of every box and a test of the four axes "
            + "written apart, for " + Lots + " lots over " + Boxes + " boxes (" + met + " meeting one), in " + reachMs.ToString("F2") + " ms against "
            + plainMs.ToString("F2") + " ms (wanted under 10 ms)");
    }

    // An upright box and a turned rectangle (half sizes already grown) meet when no one of the four axes parts them: the
    // world's X and Z, and the rectangle's own across and outward.
    private static bool UptownMeets(float[] box, int i, float cx, float cz, float facing, float hw, float hd)
    {
        float minX = box[i * 4], minZ = box[i * 4 + 1], maxX = box[i * 4 + 2], maxZ = box[i * 4 + 3];
        if (maxX < minX || maxZ < minZ)
        {
            return false;
        }
        float f = facing * (float)(Math.PI / 180.0);
        float ux = (float)Math.Sin(f), uz = (float)Math.Cos(f);   // outward
        float ax = uz, az = -ux;                                  // across
        float spanX = Math.Abs(ax) * hw + Math.Abs(ux) * hd, spanZ = Math.Abs(az) * hw + Math.Abs(uz) * hd;
        if (cx + spanX < minX || cx - spanX > maxX || cz + spanZ < minZ || cz - spanZ > maxZ)
        {
            return false;
        }
        float mx = (minX + maxX) * 0.5f - cx, mz = (minZ + maxZ) * 0.5f - cz, ex = (maxX - minX) * 0.5f, ez = (maxZ - minZ) * 0.5f;
        return Math.Abs(mx * ax + mz * az) <= hw + ex * Math.Abs(ax) + ez * Math.Abs(az) && Math.Abs(mx * ux + mz * uz) <= hd + ex * Math.Abs(ux) + ez * Math.Abs(uz);
    }

    // ---------------------------------------------------------------- another structure where no design of its own fits

    // The game's structures for the band's sizes as its BuildingSizeData has them (the game's files, 23 Oct 2026; LifeGame.Structures
    // reads them at each load): size, version, type, never the special ones. No design of P or Q that can be copied fits a 29 by
    // 39 m warehouse lot (the smallest are 34.9 by 36.6 and 48.9 by 69.5 m), so a warehouse lot whose hash picks one of them
    // must take another of its sizes.
    private static readonly string[][] GameStructures =
    {
        new[] { "a", "1", "retail" }, new[] { "a", "2", "retail" }, new[] { "a", "3", "office" }, new[] { "b", "1", "residential" },
        new[] { "c", "1", "retail" }, new[] { "c", "1", "office" }, new[] { "c", "2", "retail" }, new[] { "f", "1", "residential" },
        new[] { "d", "2", "retail" }, new[] { "d", "2", "office" }, new[] { "j", "1", "office" }, new[] { "k", "1", "office" },
        new[] { "l", "1", "residential" }, new[] { "m", "1", "retail" }, new[] { "n", "1", "residential" },
        new[] { "h", "1", "warehouse" }, new[] { "h", "2", "warehouse" }, new[] { "h", "3", "warehouse" }, new[] { "i", "1", "warehouse" },
        new[] { "i", "2", "warehouse" }, new[] { "i", "3", "warehouse" }, new[] { "p", "1", "warehouse" }, new[] { "p", "2", "warehouse" },
        new[] { "p", "3", "warehouse" }, new[] { "q", "1", "warehouse" }, new[] { "q", "2", "warehouse" }, new[] { "q", "3", "warehouse" },
        new[] { "r", "1", "theater" }, new[] { "r", "2", "theater" }, new[] { "r", "3", "theater" },
        new[] { "s", "1", "cinema" }, new[] { "s", "2", "cinema" }, new[] { "s", "3", "cinema" }
    };

    // A lot's options as LifeGame.Structures lists them from the table above: its class's sizes, in the rules' order.
    private static List<BuildingsStructure> UptownOptions(BuildingsLot lot)
    {
        var options = new List<BuildingsStructure>();
        foreach (string[] s in GameStructures)
        {
            string size = "ba:buildingsize_" + s[0];
            for (int i = 0; lot.Sizes != null && i < lot.Sizes.Count; i++)
            {
                if (lot.Sizes[i] == size)
                {
                    options.Add(new BuildingsStructure { Size = size, Version = int.Parse(s[1]), Type = "ba:buildingtype_" + s[2] });
                }
            }
        }
        options.Sort(BuildingsRules.CompareStructures);
        return options;
    }

    // A list of structures as "Q1w H1w": the game's short name of each and its type's first letter.
    private static string UptownNames(List<BuildingsStructure> list)
    {
        var b = new StringBuilder();
        foreach (BuildingsStructure s in list)
        {
            b.Append(b.Length > 0 ? " " : string.Empty).Append(NewBuildingsRules.StructureName(s.Size, s.Version)).Append(s.Type.Substring(s.Type.LastIndexOf('_') + 1, 1));
        }
        return b.ToString();
    }

    private static BuildingsStructure UptownStructure(string size, int version, string type)
    {
        return new BuildingsStructure { Size = "ba:buildingsize_" + size, Version = version, Type = "ba:buildingtype_" + type };
    }

    // The fill's own work at a load that runs no game code, timed on the real plan as the load does it: which lots are built
    // and leased, each built lot's structures, its pick and its others; the lanes' points sorted into squares; and each
    // building's look at the lanes near it and its pass over the game's boxes. Behind the loading screen, never a frame.
    private static void UptownFillLoad(List<BuildingsLot> plan)
    {
        const int LanePoints = 20176, Boxes = 2934;
        var xs = new float[LanePoints];
        var zs = new float[LanePoints];
        var present = new bool[LanePoints];
        var box = new float[Boxes * 4];
        uint seed = 2166136261u;
        for (int i = 0; i < LanePoints; i++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            xs[i] = -3000f + (seed % 600000u) / 100f;
            seed = unchecked(seed * 1664525u + 1013904223u);
            zs[i] = -1500f + (seed % 250000u) / 100f;
            present[i] = true;
        }
        for (int i = 0; i < Boxes; i++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            float x = -4000f + seed % 560000u / 100f;
            seed = unchecked(seed * 1664525u + 1013904223u);
            float z = -2500f + seed % 350000u / 100f;
            box[i * 4] = x;
            box[i * 4 + 1] = z;
            box[i * 4 + 2] = x + 20f;
            box[i * 4 + 3] = z + 25f;
        }
        double rulesMs = double.MaxValue, lanesMs = double.MaxValue, groundMs = double.MaxValue;
        int built = 0, others = 0, hits = 0;
        for (int run = 0; run < 3; run++)
        {
            long a = Stopwatch.GetTimestamp();
            List<int> lots = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
            HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
            var into = new List<BuildingsStructure>();
            others = 0;
            foreach (int i in lots)
            {
                List<BuildingsStructure> options = UptownOptions(plan[i]);
                bool lease = leased.Contains(plan[i].Id);
                int pick = lease ? BuildingsRules.PickOffice(plan[i].Id, options) : BuildingsRules.PickStructure(plan[i].Id, options);
                others += BuildingsRules.Others(options, pick, lease, into).Count;
            }
            long b = Stopwatch.GetTimestamp();
            var grid = new LaneGrid();
            grid.Fill(xs, zs, present, LanePoints);
            long c = Stopwatch.GetTimestamp();
            hits = 0;
            foreach (int i in lots)
            {
                BuildingsLot lot = plan[i];
                float hw = lot.Width * 0.5f - 1f, hd = lot.Depth * 0.5f - 1f;
                hits += grid.Inside(lot.X, lot.Z, lot.Facing, hw, hd, NewBuildingsRules.RoadClear) >= 0 ? 1 : 0;
                hits += grid.Nearest(lot.X, lot.Z, NewBuildingsRules.StreetReach + Math.Max(hw, hd), out _) >= 0 ? 1 : 0;
                NewBuildingsRules.Reach(lot.Facing, hw, hd, NewBuildingsRules.Gap, out float reachX, out float reachZ);
                for (int k = 0; k < Boxes; k++)
                {
                    int q = k * 4;
                    if (!NewBuildingsRules.Apart(box[q], box[q + 1], box[q + 2], box[q + 3], lot.X, lot.Z, reachX, reachZ)
                        && NewBuildingsRules.Overlaps(box[q], box[q + 1], box[q + 2], box[q + 3], lot.X, lot.Z, lot.Facing, hw, hd, NewBuildingsRules.Gap))
                    {
                        hits++;
                        break;
                    }
                }
            }
            long d = Stopwatch.GetTimestamp();
            built = lots.Count;
            rulesMs = Math.Min(rulesMs, (b - a) * 1000.0 / Stopwatch.Frequency);
            lanesMs = Math.Min(lanesMs, (c - b) * 1000.0 / Stopwatch.Frequency);
            groundMs = Math.Min(groundMs, (d - c) * 1000.0 / Stopwatch.Frequency);
        }
        double all = rulesMs + lanesMs + groundMs;
        Check(built == 567 && others > 0 && hits > 0 && all < 30.0,
            "uptown fill: the fill's own load work without the game's code, on the real plan: the built and leased lots and each one's structures and others in "
            + rulesMs.ToString("F2") + " ms, " + LanePoints + " lane points into squares in " + lanesMs.ToString("F2") + " ms, " + built + " buildings' lane and box checks in "
            + groundMs.ToString("F2") + " ms; " + all.ToString("F2") + " ms in all (wanted under 30 ms)");
    }

    private static void UptownFillOthers(List<BuildingsLot> plan)
    {
        var warehouses = new List<BuildingsStructure>();
        foreach (string size in new[] { "h", "i", "p", "q" })
        {
            for (int v = 1; v <= 3; v++)
            {
                warehouses.Add(UptownStructure(size, v, "warehouse"));
            }
        }
        warehouses.Sort(BuildingsRules.CompareStructures);
        var into = new List<BuildingsStructure>();
        string fromP2 = UptownNames(BuildingsRules.Others(warehouses, 7, false, into));
        string fromQ3 = UptownNames(BuildingsRules.Others(warehouses, 11, false, into));
        string fromH1 = UptownNames(BuildingsRules.Others(warehouses, 0, false, into));
        Check(fromP2 == "Q1w H1w I1w" && fromQ3 == "H1w I1w P1w" && fromH1 == "I1w P1w Q1w",
            "uptown fill: a warehouse whose size has no design for its lot takes one of each other size, from its own on and round again (" + fromP2 + "; " + fromQ3 + "; " + fromH1 + ")");

        var small = new List<BuildingsStructure>
        {
            UptownStructure("a", 1, "retail"), UptownStructure("a", 2, "retail"), UptownStructure("a", 3, "office"), UptownStructure("b", 1, "residential"),
            UptownStructure("c", 1, "retail"), UptownStructure("c", 1, "office"), UptownStructure("c", 2, "retail"), UptownStructure("f", 1, "residential")
        };
        small.Sort(BuildingsRules.CompareStructures);
        int a1 = small.FindIndex(s => s.Size == "ba:buildingsize_a" && s.Version == 1), a3 = small.FindIndex(s => s.Size == "ba:buildingsize_a" && s.Version == 3);
        string fromA1 = UptownNames(BuildingsRules.Others(small, a1, false, into));
        string fromA3 = UptownNames(BuildingsRules.Others(small, a3, false, into));
        string officesA3 = UptownNames(BuildingsRules.Others(small, a3, true, into));
        Check(fromA1 == "B1r C1r F1r" && fromA3 == "B1r C1o F1r" && officesA3 == "C1o",
            "uptown fill: the other sizes keep the building's type where they take it, and a leased office takes offices alone (" + fromA1 + "; " + fromA3 + "; " + officesA3 + ")");
        Check(BuildingsRules.Others(null, 0, false, into).Count == 0 && BuildingsRules.Others(small, -1, false, into).Count == 0
              && BuildingsRules.Others(small, small.Count, false, into).Count == 0
              && BuildingsRules.Others(new List<BuildingsStructure> { UptownStructure("a", 1, "retail") }, 0, false, into).Count == 0,
            "uptown fill: no other structures where the pick is none of the options or its size is the only one");

        // On the real plan: every built lot's own structure and its others reach every size of its class (every office size
        // for a leased one), each size once; so a building stands wherever any size of its class has a design that fits.
        List<int> built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        int reached = 0, onPq = 0, leasedOk = 0;
        string wrong = null;
        foreach (int i in built)
        {
            BuildingsLot lot = plan[i];
            List<BuildingsStructure> options = UptownOptions(lot);
            bool lease = leased.Contains(lot.Id);
            int pick = lease ? BuildingsRules.PickOffice(lot.Id, options) : BuildingsRules.PickStructure(lot.Id, options);
            if (pick < 0)
            {
                wrong = wrong ?? lot.Id + " has no structure";
                continue;
            }
            BuildingsRules.Others(options, pick, lease, into);
            var sizes = new HashSet<string> { options[pick].Size };
            bool once = true, offices = !lease || options[pick].Type == BuildingsRules.OfficeType;
            foreach (BuildingsStructure o in into)
            {
                once &= sizes.Add(o.Size);
                offices &= !lease || o.Type == BuildingsRules.OfficeType;
            }
            bool all = true;
            foreach (BuildingsStructure o in options)
            {
                all &= sizes.Contains(o.Size) || (lease && o.Type != BuildingsRules.OfficeType);
            }
            if (once && offices && all)
            {
                reached++;
                leasedOk += lease ? 1 : 0;
            }
            else
            {
                wrong = wrong ?? lot.Id + " (" + UptownNames(into) + ")";
            }
            onPq += lot.Class == BuildingsClass.Warehouse && (options[pick].Size == "ba:buildingsize_p" || options[pick].Size == "ba:buildingsize_q") ? 1 : 0;
        }
        Check(reached == built.Count && leasedOk == BuildingsRules.LeasedOffices && onPq > 0,
            "uptown fill: each of the " + built.Count + " buildings may take every size of its lot's class (" + reached + "; the eight offices offices alone: " + leasedOk + "), "
            + onPq + " warehouse lots picking P or Q, which no design fits" + (wrong != null ? "; not so for " + wrong : string.Empty));

        var spec = new MadeSpec { Id = "x", Life = false };
        var life = new MadeSpec { Id = "y", Life = true };
        Func<string, int, string, bool> any = (size, version, type) => true;
        Func<string, int, string, bool> none = (size, version, type) => false;
        var home = new MadeStructure { Size = "ba:buildingsize_b", Version = 1, Type = NewBuildingsRules.HomeType };
        var office = new MadeStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_office" };
        var special = new MadeStructure { Size = "ba:buildingsize_d", Version = 3, Type = NewBuildingsRules.SpecialType };
        Check(NewBuildingsRules.OtherTakes(spec, office, any, false) && !NewBuildingsRules.OtherTakes(spec, office, none, true) && !NewBuildingsRules.OtherTakes(spec, special, any, true)
              && !NewBuildingsRules.OtherTakes(spec, home, any, false) && NewBuildingsRules.OtherTakes(spec, home, any, true) && NewBuildingsRules.OtherTakes(life, home, any, false)
              && !NewBuildingsRules.OtherTakes(null, office, any, true) && !NewBuildingsRules.OtherTakes(spec, new MadeStructure(), any, true)
              && !NewBuildingsRules.OtherTakes(spec, office, null, true),
            "new buildings: another structure is taken by the declaration's own limits: one the game has, never the special type, a home only where the deliveries leave it alone");
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownFillSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string director = Read("Uptown/LifeDirector.cs"), lots = Read("Uptown/LifeDirector.Lots.cs"), leases = Read("Uptown/LifeDirector.Leases.cs");
        string checks = Read("Uptown/LifeDirector.Checks.cs"), gameBuildings = Read("Uptown/LifeGame.Buildings.cs"), gameLeases = Read("Uptown/LifeGame.Leases.cs");
        string map = Read("Uptown/LifeGame.Map.cs"), guide = Read("Uptown/LifeGuide.cs"), districtBuildings = Read("Uptown/DistrictBuildings.cs");
        string landmarks = Read("Landmarks/LandmarksDirector.cs"), wiring = Read("Wiring/Wiring.ConstructionLinks.cs"), rivals = Read("Construction/ConstructionApp.Rivals.cs");
        string copyGame = Read("NewBuildings/CopyGame.cs"), ground = Read("NewBuildings/CopyGame.Ground.cs"), newDirector = Read("NewBuildings/NewBuildingsDirector.cs");

        string declare = MemberText(director, "private static void Declare()");
        Check(declare.Contains("BuildingsRules.Built(Plan, DistrictBuildings.KeptOpen") && declare.Contains("LifeGame.Kept(lot)") && declare.Contains("BuildingsRules.PickOffice(")
              && !declare.Contains("BuildingsRules.DayOne("),
            "uptown fill: the load declares the band's built lots, every lot whose records the save holds, and a leased lot as an office");
        string firstDay = MemberText(lots, "public static bool FirstDay(string lotId)");
        Check(firstDay.Contains("BuildingsRules.Built(Plan, kept") && firstDay.Contains("ReferenceEquals(kept, _firstDayFor)"),
            "uptown fill: the lots built from the first day the landmarks read are the same rule's, worked out again if the kept blocks change");
        Check(MemberText(lots, "public static bool StandsOn(string lotId)").Contains("StandingLots.Contains(lotId)") && director.Contains("MarkStanding();"),
            "uptown fill: whether a building stands on a lot is a set's look, made once the buildings stand (the construction game asks it of every lot on a page)");
        string firstLife = MemberText(director, "private static bool FirstLife()");
        Check(firstLife.Contains("LifeGame.PopulateOnly(save, NewOnes, Standing)") && firstLife.Contains("Leases(save, out int renamed)") && !firstLife.Contains("FinishOwner.City, 0, save"),
            "uptown fill: buildings new to a save the district lived in are set up by the game's own generator, not left empty to let, and the leases move in after");
        string only = MemberText(gameBuildings, "public static int PopulateOnly(GameInstance save, List<MadeBuilding> fresh, List<MadeBuilding> standing)");
        Check(only.Contains("all.Remove(record)") && only.Contains("finally") && only.Contains("all.AddRange(AllWas)") && only.Contains("UnityEngine.Random.state = was")
              && only.Contains("CityGenerator.InitializeCity(DistrictValues.Id)") && only.Contains("CityGenerator.DistributeBuildingsToRivals(DistrictValues.Id)"),
            "uptown fill: the generator sees the new buildings alone, and the game's list of all buildings and its chance are put back as they were, after a fault too");
        string lease = MemberText(gameLeases, "public static string Lease(MadeBuilding made, DistrictLease lease, int index, GameInstance save, out bool movedIn, out bool renamed)");
        string moveIn = MemberText(gameLeases, "private static void MoveIn(BuildingRegistration reg, DistrictLease lease, int index, AiBusinessDefault from, GameInstance save)");
        Check(lease.Contains("GoingPublic.Foreign(reg)") && lease.Contains("reg.RentedByPlayer || reg.BuildingOwnedByPlayer") && lease.Contains("made.NewRegistration")
              && moveIn.Contains("CompetitionHelper.StartNewCompetitorBusiness(lease.BusinessType, reg, impactMarket: false, from, string.Empty, setUpFirstDailyIncome: false)")
              && moveIn.Contains("UnityEngine.Random.InitState(") && moveIn.Contains("UnityEngine.Random.state = was") && moveIn.Contains("reg.dailyIncomes = new List<float>(IncomeDays)"),
            "uptown fill: a builder moves into its office by the game's own start of a business, seeded, never into the player's or another player's, with days of income to value it by");
        string borrow = MemberText(gameLeases, "private static string Borrow(string from, string to)");
        Check(borrow.Contains("BusinessLayoutSetHelper.GetAllBusinessLayoutSets()") && borrow.Contains("warnIfNotFound: false") && borrow.Contains("!= lent")
              && borrow.Contains("all.Remove(key)") && borrow.Contains("BusinessLayoutSetHelper.loadingLayouts"),
            "uptown fill: the studio's layouts are filed under the builders' type in the game's own cache, and taken out again if the game does not find them there");
        Check(Regex.Matches(gameLeases + gameBuildings, @"\bUpdate\(\)|LateUpdate\(|FixedUpdate\(").Count == 0 && Regex.Matches(leases + checks, @"void (Late|Fixed)?Update\(").Count == 0,
            "uptown fill: nothing of the fill, the leases or the checks runs a frame");
        Check(MemberText(map, "private static bool Row(float x1, float z1, float x2, float z2, out string why)").Contains("Place(_row, under);")
              && MemberText(map, "private static void Place(CityMapFilter row, CityMapFilterCategory under)").Contains("SetSiblingIndex("),
            "uptown fill: Uptown's row is stood among the game's district rows, where the game stands its own, not at the end of the list where its CreateFilter puts a late one");
        Check(landmarks.Contains("DistrictBuildings.KeptOpen = LandmarkRules.OwnBlock;") && MemberText(landmarks, "public static void Register(IModLogger logger)").Contains("KeptOpen"),
            "uptown fill: the landmarks keep their own blocks open at game start, whatever their option says");
        Check(wiring.Contains("DistrictBuildings.Lessee = RivalLease;") && MemberText(wiring, "internal static void Disconnect()").Contains("DistrictBuildings.Lessee = null;")
              && wiring.Contains("ConstructionBridges.RivalOffice = RivalOffice;") && rivals.Contains("ConstructionBridges.RivalOffice(r)") && rivals.Contains("reallife_bld_rival_office_go"),
            "uptown fill: the construction game leases its rivals' offices through the wiring, and its Rivals tab shows each office and makes it the destination");
        Check(districtBuildings.Contains("public static Func<int, bool> KeptOpen;") && districtBuildings.Contains("public static Func<int, DistrictLease> Lessee;"),
            "uptown fill: the district's bridges for kept blocks and leases are fields set once, never searched for");
        Check(guide.Contains(".Check(\"reallife_uptown_test_offices\", LifeDirector.TestOffices, \"Uptown check (builders' offices)\")")
              && guide.Contains(".Check(\"reallife_uptown_test_row\", LifeDirector.TestRow, \"Uptown check (map row)\")")
              && guide.Contains(".Check(\"reallife_uptown_test_cost\", LifeDirector.TestCost, \"Uptown check (frame cost)\", RunPlace.UptownLot)")
              && checks.Contains("\"Uptown check (builders' offices): ") && checks.Contains("\"Uptown check (map row): ") && checks.Contains("\"Uptown check (frame cost): "),
            "uptown fill: the offices, the map row and the frame cost are in the test run, each writing the line it is read by");
        string looks = MemberText(copyGame, "public static bool LooksAsGame(CityBuildingController c, out string why)");
        Check(looks.Contains("ShadowCastingMode.ShadowsOnly") && looks.Contains("IsInCameraBlockMode") && director.Length > 0
              && Read("Uptown/LifeDirector.Tests.cs").Contains("MadeBuildings.LooksAsGame(Standing[i], out string why)"),
            "uptown fill: the census judges every building's look against the game's (its box drawn only as a shadow while the map is shut)");
        Check(ground.Contains("Lanes.Inside(cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear)") && ground.Contains("Lanes.Nearest(cx, cz, reach, out _)")
              && ground.Contains("ReferenceEquals(points, _lanesOf)"),
            "uptown fill: the made buildings' ground check reads the lanes' squares, made again when the lanes change");
        string groundWhy = MemberText(ground, "public static string GroundWhy(DesignEntry d, MadeLot lot, out Vector3 root, out float yaw, out Vector3 door)");
        Check(groundWhy.Contains("NewBuildingsRules.Reach(facing, hw, hd, NewBuildingsRules.Gap, out float reachX, out float reachZ);")
              && Regex.Matches(groundWhy, @"!NewBuildingsRules\.Apart\(x0, z0, x1, z1, cx, cz, reachX, reachZ\) && NewBuildingsRules\.Overlaps\(x0, z0, x1, z1,").Count == 2,
            "uptown fill: the ground check tries the game's buildings and obstacles by the rectangle's reach before the turned test");
        Check(newDirector.Contains("Made buildings check (load time): ") && newDirector.Contains("NewBuildingsRules.MostMakeMs") && newDirector.Contains("if (!spec.Life || Meta.Dev.Verbose)"),
            "uptown fill: the load's line says what measuring and making cost, a district's buildings are one line, each with Developer diagnostics");
        string make = MemberText(newDirector, "private bool Make(MadeSpec spec)");
        int tries = make.IndexOf("for (int s = -1; lot < 0 && noDesign && s < (spec.Others != null ? spec.Others.Length : 0); s++)", StringComparison.Ordinal);
        int takes = make.IndexOf("spec.Size = other.Size;", StringComparison.Ordinal), record = make.IndexOf("MadeGame.MakeRecord(spec", StringComparison.Ordinal);
        Check(tries > 0 && make.Contains("noDesign &= pick < 0;") && make.Contains("NewBuildingsRules.OtherTakes(spec, spec.Others[s], MadeGame.StructureTakes, MadeGame.HomesLeftOut())")
              && takes > tries && record > takes && make.Contains("spec.Version = other.Version;") && make.Contains("spec.Type = other.Type;"),
            "new buildings: where no design of its size fits any lot, a building tries its other structures in order (never for a lot its ground refused), its record made with the one taken");
        Check(declare.IndexOf("if (!BuildingsRules.Clear(lot.Id) && !BigDown.Contains(lot.Id))", StringComparison.Ordinal) >= 0
              && declare.IndexOf("if (!BuildingsRules.Clear(lot.Id) && !BigDown.Contains(lot.Id))", StringComparison.Ordinal) < declare.IndexOf("FinishedBuilding f = _state.On(lot.Id);", StringComparison.Ordinal)
              && MemberText(lots, "public static bool Taken(string lotId)").Contains("!BuildingsRules.Clear(lotId) ||")
              && MemberText(Read("Uptown/LifeDirector.Tests.cs"), "private static bool Census(string when, bool asked = false)").Contains("BuildingsRules.Clear(Plan[i].Id)"),
            "uptown fill: a lot not clear is declared nothing at a load, taken for the construction game and the landmarks, and never counted open");
        Check(declare.Contains("BuildingsRules.Others(Options, pick, lease, Others);") && declare.Contains("LifeGame.Spec(GroundOf(lot, f), structure, Others,")
              && declare.IndexOf("Others.Clear();", StringComparison.Ordinal) >= 0
              && declare.IndexOf("Others.Clear();", StringComparison.Ordinal) < declare.IndexOf("if (f != null)", StringComparison.Ordinal)
              && MemberText(gameBuildings, "public static MadeSpec Spec(BuildingsLot lot, BuildingsStructure structure, IList<BuildingsStructure> others,").Contains("Others = instead"),
            "uptown fill: each day-one building is declared with its other structures (offices alone for a leased office), a finished one with none");
    }

    // ---------------------------------------------------------------- the texts

    private static void UptownFillTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys =
        {
            "reallife_uptown_test_offices", "reallife_uptown_test_offices_done", "reallife_uptown_test_offices_none", "reallife_uptown_test_row", "reallife_uptown_test_row_done",
            "reallife_uptown_test_row_wrong", "reallife_uptown_test_cost", "reallife_uptown_test_cost_start", "reallife_uptown_test_cost_done", "reallife_uptown_test_cost_none",
            "reallife_bld_row_office", "reallife_bld_rival_office_none", "reallife_bld_rival_office_go", "reallife_bld_rival_office_set", "reallife_bld_office_about"
        };
        bool all = en != null && pt != null && zh != null, short240 = true;
        string missing = null;
        foreach (string key in keys)
        {
            bool there = all && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && SameHoles(en[key], pt[key], zh[key]);
            missing = there ? missing : missing ?? key;
            all &= there;
            short240 &= en == null || !en.ContainsKey(key) || en[key].Length <= 240;
        }
        Check(all && short240, "uptown fill: every new text in en, pt and zh-cn with the same {holes}, every notice at most 240 characters (" + (missing ?? keys.Length + " texts") + ")");
        var langs = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        bool lines = true;
        string lineMissing = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            bool found = false;
            if (words != null)
            {
                foreach (KeyValuePair<string, string> kv in words)
                {
                    if (kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && kv.Value.Contains(UptownFillMark[i]))
                    {
                        found = true;
                        break;
                    }
                }
            }
            lines &= found;
            lineMissing = found ? lineMissing : lineMissing ?? langs[i];
        }
        Check(lines && UptownFillMark.Length == langs.Length, "uptown fill: What's new names the rival builders' offices in all nine languages"
                                                             + (lineMissing != null ? " (not in " + lineMissing + ")" : string.Empty));
        bool built = true;
        string builtMissing = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            bool found = false;
            if (words != null)
            {
                foreach (KeyValuePair<string, string> kv in words)
                {
                    found |= kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && kv.Value.Contains(UptownBuiltMark[i]);
                }
            }
            built &= found;
            builtMissing = found ? builtMissing : builtMissing ?? langs[i];
        }
        Check(built && UptownBuiltMark.Length == langs.Length, "uptown fill: What's new says Uptown is built up from the first day in all nine languages"
                                                              + (builtMissing != null ? " (not in " + builtMissing + ")" : string.Empty));
    }

    // A few words of the rival builders' offices' line in each language's What's new, in the order en, pt, zh-cn, de, fr, es, ru, nl, tr.
    private static readonly string[] UptownFillMark = { "offices in Uptown", "escritórios em Uptown", "在上城有了办公室", "Büros in Uptown", "bureaux à Uptown", "oficinas en Uptown",
                                                        "офисы в Uptown", "kantoren in Uptown", "Uptown'da ofisleri" };

    // A few words of Uptown's line saying it is built up from the first day, in the same order.
    private static readonly string[] UptownBuiltMark = { "built up like the city's own districts", "construído como os bairros da própria cidade", "像城市原有的街区一样建满了楼",
                                                         "bebaut wie die Viertel der Stadt", "bâti comme les quartiers de la ville", "construido como los barrios de la ciudad",
                                                         "застроен как районы самого города", "volgebouwd zoals de wijken van de stad", "şehrin kendi semtleri gibi yapılaşmış" };
}
