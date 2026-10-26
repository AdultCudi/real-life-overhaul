using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.Uptown;

// The band north of Manhattan as two districts (plans/uptown-split.md; the owner, 27 Oct 2026: "Can we split the Uptown district
// into 2? ... Every single thing must be separated properly"), game-free: the boundary rule (every block, with all the lots round
// it, wholly in one district; the cut runs along a street of the grid), the districts' figures against the game's own range,
// the rebalance of the large stores (the quotas, nothing else moving, the pins that keep a held store), the census against the
// old districts (a split that is not balanced, and the band as one district, are told apart), the save from before the split
// (what is held keeps its store, what is not is rebuilt, the line's new parts, the record of the second district's market),
// and the sources read as text (nothing of the band is still one district's alone).
internal static partial class Program
{
    private static void UptownSplitChecks()
    {
        UptownSplitBoundary();
        UptownSplitFigures();
        UptownSplitRebalance();
        UptownSplitCensus();
        UptownSplitMigration();
        UptownSplitSources();
        UptownSplitTexts();
    }

    // ---------------------------------------------------------------- the boundary

    private static void UptownSplitBoundary()
    {
        Check(Math.Abs(DistrictSplit.BoundaryX - LayoutRules.StreetX(DistrictSplit.BoundaryStreet)) < 0.01f && DistrictSplit.BoundaryStreet == 4
              && LayoutRules.WestStreet(DistrictSplit.LastWestColumn + 1) == DistrictSplit.BoundaryStreet && LayoutRules.WestStreet(DistrictSplit.LastWestColumn) + 1 == DistrictSplit.BoundaryStreet,
            "uptown split: the cut runs along 5th Street (index 4), between the blocks of columns 9 and 10");
        Check(DistrictSplit.Count == 2 && DistrictSplit.Ids[0] == DistrictValues.Id && DistrictSplit.Ids[1] == "reallife_neighborhood_cedarheights" && DistrictSplit.Ids.Distinct().Count() == 2
              && DistrictSplit.Ids.All(id => id.StartsWith("reallife_", StringComparison.Ordinal)) && DistrictSplit.Index(DistrictSplit.Ids[1]) == 1 && DistrictSplit.Index("ba:neighborhood_midtown") == -1
              && DistrictSplit.Is(DistrictValues.Id) && !DistrictSplit.Is(null),
            "uptown split: two districts with ids of the mod's own, the first the id the whole band had before");

        IReadOnlyList<Lot> lots = LayoutRules.Lots;
        int straddle = 0, wrong = 0, columnWrong = 0;
        foreach (Lot l in lots)
        {
            bool avenue = l.Facing == 0f || l.Facing == 180f;
            float half = (avenue ? l.Width : l.Depth) * 0.5f;
            bool west = l.X + half < DistrictSplit.BoundaryX, east = l.X - half > DistrictSplit.BoundaryX;
            straddle += west || east ? 0 : 1;
            wrong += DistrictSplit.OfX(l.X) != l.District || (west ? DistrictSplit.CedarHeights : DistrictSplit.Uptown) != l.District ? 1 : 0;
            columnWrong += l.District != DistrictSplit.OfColumn(l.Column) || l.District != DistrictSplit.OfBlock(l.Block) || DistrictSplit.Of(l) != l.District ? 1 : 0;
        }
        Check(straddle == 0 && wrong == 0 && columnWrong == 0, "uptown split: no lot straddles the cut, and every lot's district is its block's, its column's and its position's alike (" + lots.Count + " lots)");

        var blocks = new Dictionary<int, HashSet<int>>();
        foreach (Lot l in lots)
        {
            if (!blocks.TryGetValue(l.Block, out HashSet<int> set))
            {
                set = blocks[l.Block] = new HashSet<int>();
            }
            set.Add(l.District);
        }
        bool oneEach = blocks.Values.All(s => s.Count == 1);
        int blockCuts = 0;
        for (int row = 1; row <= LayoutRules.Rows + 1; row++)
        {
            for (int column = 0; column <= LayoutRules.Columns + 1; column++)
            {
                if (row > LayoutRules.Rows && (column < 1 || column > LayoutRules.Columns))
                {
                    continue;
                }
                LayoutRules.Block(row, column, out float x0, out _, out float x1, out _);
                bool whole = x1 < DistrictSplit.BoundaryX || x0 > DistrictSplit.BoundaryX;
                blockCuts += whole && DistrictSplit.OfX((x0 + x1) * 0.5f) == DistrictSplit.OfColumn(column) ? 0 : 1;
            }
        }
        Check(oneEach && blocks.Count > 50 && blockCuts == 0, "uptown split: every block (" + blocks.Count + " with lots, the squares and the ring among the grid's) is wholly in one district, the lots round it all in the same");

        int cedarLots = lots.Count(l => l.District == DistrictSplit.CedarHeights), uptownLots = lots.Count - cedarLots;
        Check(cedarLots > 250 && uptownLots > 250 && Math.Abs(cedarLots - uptownLots) <= 40, "uptown split: both districts hold lots, within 40 of each other (Uptown " + uptownLots + ", Cedar Heights " + cedarLots + ")");

        bool okUptown = DistrictSplit.Bounds(DistrictSplit.Uptown, LayoutRules.BandGround, out float ux1, out float uz1, out float ux2, out float uz2);
        bool okCedar = DistrictSplit.Bounds(DistrictSplit.CedarHeights, LayoutRules.BandGround, out float cx1, out float cz1, out float cx2, out float cz2);
        bool halves = okUptown && okCedar;
        float west0 = LayoutRules.BandGround.Min(r => Math.Min(r.X1, r.X2)), east0 = LayoutRules.BandGround.Max(r => Math.Max(r.X1, r.X2));
        Check(halves && Math.Abs(ux1 - DistrictSplit.BoundaryX) < 0.01f && Math.Abs(cx2 - DistrictSplit.BoundaryX) < 0.01f && Math.Abs(ux2 - east0) < 0.01f && Math.Abs(cx1 - west0) < 0.01f,
            "uptown split: the two zones on the city map are halves of the band's ground that meet at the cut");

        // A street is in its district; the avenues and 5th Street, which the cut runs along, are in both.
        bool streets = true;
        foreach (LayoutStreet street in LayoutStreets.All)
        {
            HashSet<int> on = new HashSet<int>(lots.Where(l => l.Street == street.Id).Select(l => l.District));
            bool both = street.Avenue || street.Index == DistrictSplit.BoundaryStreet;
            streets &= both ? on.Count == 2 : on.Count == 1;
        }
        Check(streets, "uptown split: each street is in its district, the avenues and 5th Street, which the cut runs along, in both");

        // Where the band's places stand.
        var specialsBy = SpecialsRules.All.GroupBy(p => DistrictSplit.OfBlock(Block(p.Lot))).ToDictionary(g => g.Key, g => g.Count());
        Check(specialsBy.GetValueOrDefault(DistrictSplit.CedarHeights) == 6 && specialsBy.GetValueOrDefault(DistrictSplit.Uptown) == 5,
            "uptown split: the special places are shared out, six in Cedar Heights (the bank's branch, the clinic, the agencies, the furniture store) and five in Uptown (the supermarket, the dealership, the importer, the shops)");
        Check(DistrictSplit.OfBlock(LayoutRules.CampusBlock) == DistrictSplit.CedarHeights && DistrictSplit.OfBlock(309) == DistrictSplit.CedarHeights && DistrictSplit.OfBlock(117) == DistrictSplit.Uptown
              && LayoutRules.Places.Count == 2 && LayoutRules.Places.All(p => p.District == DistrictSplit.CedarHeights),
            "uptown split: Birchwood's campus, the west park and the wholesale store and the petrol station are in Cedar Heights, the east park in Uptown");
        Check(LandmarkRules.BlocksOf(LandmarkKind.Tower)[0] == 111 && LandmarkRules.BlocksOf(LandmarkKind.Mall)[0] == 113 && LandmarkRules.BlocksOf(LandmarkKind.Stadium)[0] == 110
              && new[] { 110, 111, 113 }.All(b => DistrictSplit.OfBlock(b) == DistrictSplit.Uptown) && LayoutRules.KeptForLandmarks.All(b => DistrictSplit.OfBlock(b) == DistrictSplit.Uptown),
            "uptown split: the tower, the mall and the stadium, on the first blocks their rules take, are in Uptown");
    }

    private static int Block(string lotId)
    {
        Lot lot = UptownLots.Get(lotId);
        return lot != null ? lot.Block : 0;
    }

    // ---------------------------------------------------------------- the districts' figures

    private static void UptownSplitFigures()
    {
        DistrictFigures u = DistrictSplit.Figures(DistrictSplit.Uptown), c = DistrictSplit.Figures(DistrictSplit.CedarHeights);
        Check(DistrictValues.Sound(u) && DistrictValues.Sound(c) && !DistrictValues.Same(u, c), "uptown split: both districts' figures are ones the game can take, and they differ");
        Check(u.Working == 5 && u.Middle == 76 && u.Upper == 19 && c.Working == 40 && c.Middle == 50 && c.Upper == 10, "uptown split: Uptown's people are 5, 76 and 19 % working, middle and upper class, Cedar Heights' 40, 50 and 10");
        // Inside the range the game's own seven districts span (Build 3682), a figure at a time.
        DistrictFigures[] old = { DistrictValues.Midtown3682, DistrictValues.HellsKitchen3682 };
        var oldWeights = DistrictCensus.Old.Select(o => o.Demand).ToArray();
        var oldRent = new[] { 0.65f, 0.5f, 0.5f, 1.3f, 2.2f, 0.55f, 0.9f };
        var oldParking = new[] { 0f, 0f, 0f, 7.5f, 15f, 5f, 5f };
        var oldCars = new[] { 80, 35, 70, 90, 100, 80, 90 };
        var oldInterior = new[] { 0, 0, 0, 0, 50, 0, 0 };
        var oldMarketing = new[] { 0.9f, 1f, 1f, 0.7f, 0.5f, 1f, 0.8f };
        var oldShut = new[] { 600f, 600f, 600f, 700f, 850f, 600f, 700f };
        bool inRange = true;
        foreach (DistrictFigures f in new[] { u, c })
        {
            inRange &= f.Demand >= oldWeights.Min() && f.Demand <= oldWeights.Max() && f.RealEstate >= oldRent.Min() && f.RealEstate <= oldRent.Max() && f.Parking >= oldParking.Min() && f.Parking <= oldParking.Max()
                       && f.CarTraffic >= oldCars.Min() && f.CarTraffic <= oldCars.Max() && f.InteriorScore >= oldInterior.Min() && f.InteriorScore <= oldInterior.Max()
                       && f.Marketing >= oldMarketing.Min() && f.Marketing <= oldMarketing.Max() && f.ShutDownBelow >= oldShut.Min() && f.ShutDownBelow <= oldShut.Max();
        }
        Check(inRange && old.Length == 2, "uptown split: each figure of each district (customers, land, parking, cars, interior score, marketing, closing income) is inside the range the game's seven districts span");
        Check(u.Demand > c.Demand && u.RealEstate > c.RealEstate && u.Parking > c.Parking && u.Upper > c.Upper && u.Working < c.Working && u.CarTraffic >= c.CarTraffic
              && u.RealEstate < DistrictValues.Midtown3682.RealEstate && u.Demand < DistrictValues.Midtown3682.Demand && c.RealEstate < DistrictValues.HellsKitchen3682.RealEstate,
            "uptown split: Uptown is the upscale one (dearer land, more customers, more upper class), under Midtown's land and customers; Cedar Heights the working and middle class one, under Hell's Kitchen's land");
        Check(u.EmptyOffices == MixRules.ToLetShare && u.EmptyShops == MixRules.ToLetShare && c.EmptyOffices == MixRules.ToLetShare && c.EmptyShops == MixRules.ToLetShare,
            "uptown split: both keep the owner's third of shops, offices and homes free to rent");
        float pu = DistrictValues.PriceIndex(u.Working, u.Middle, u.Upper), pc = DistrictValues.PriceIndex(c.Working, c.Middle, c.Upper);
        Check(pu > pc && pu < DistrictValues.PriceIndex(0, 6, 94) && pc > DistrictValues.PriceIndex(80, 18, 2),
            "uptown split: their people's price index (" + pu.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " and " + pc.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ") sits between the game's poorest and richest");
        // Colours: every district's, the game's seven and the band's two, all clearly apart (the owner: "make sure all districts are different in color").
        var names = new List<string>();
        var colours = new List<float[]>();
        foreach (KeyValuePair<string, float[]> g in DistrictValues.GameColours)
        {
            names.Add(g.Key.Replace("ba:neighborhood_", string.Empty));
            colours.Add(g.Value);
        }
        names.Add("uptown");
        colours.Add(DistrictValues.ColourOf(DistrictSplit.Uptown));
        names.Add("cedarheights");
        colours.Add(DistrictValues.ColourOf(DistrictSplit.CedarHeights));
        string colourWhy = DistrictValues.ColourWhy(names, colours, DistrictValues.ColourLeast, out string colourLine);
        double nearestGame = double.MaxValue;
        for (int d = 0; d < 2; d++)
        {
            foreach (KeyValuePair<string, float[]> g in DistrictValues.GameColours)
            {
                nearestGame = Math.Min(nearestGame, DistrictValues.ColourDistance(DistrictValues.ColourOf(d), g.Value));
            }
        }
        double between = DistrictValues.ColourDistance(DistrictValues.ColourOf(0), DistrictValues.ColourOf(1));
        Check(colourWhy == null && nearestGame >= 40.0 && between >= 40.0 && names.Count == 9, "uptown split colours: all nine districts' colours are at least " + DistrictValues.ColourLeast + " apart, the band's two at least 40 from the game's seven and from each other (" + colourLine + ")");
        // Made to fail: a band district in Midtown's colour, and one a hair from Hell's Kitchen's.
        var same = new List<float[]>(colours);
        same[7] = DistrictValues.GameColours[4].Value;
        var near = new List<float[]>(colours);
        near[8] = new[] { 0.32f, 0.42f, 0.85f };
        Check(DistrictValues.ColourWhy(names, same, DistrictValues.ColourLeast, out string _) != null && DistrictValues.ColourWhy(names, near, DistrictValues.ColourLeast, out string _) != null
              && DistrictValues.ColourDistance(colours[0], colours[0]) == 0.0, "uptown split colours: a district in another's colour, or a hair from it, is told");
        string mapSource = SplitSource(@"Uptown\LifeGame.Map.cs") + SplitSource(@"Uptown\LifeGame.cs");
        Check(mapSource.Contains("d.cityMapIcon = Icon(district);") && !mapSource.Contains("midtown.cityMapIcon") && mapSource.Contains("CodeAssets.DropSprite(Icons[d]);")
              && SplitSource(@"Uptown\LifeDirector.cs").Contains("Uptown check (district colours): "), "uptown split colours: each district's toggle disc is drawn in its own colour (never Midtown's icon), let go with the city, and the colours listed once a load");
        bool shift = DistrictSplit.TrafficShift(DistrictSplit.Uptown) > 0 && DistrictSplit.TrafficShift(DistrictSplit.CedarHeights) < 0;
        BuildingsLot sample = new BuildingsLot { Id = "x", Avenue = true, Z = 500f, Block = 110 };
        BuildingsLot there = sample;
        there.Block = 105;
        Check(shift && BuildingsRules.Traffic(sample, 500f, 0f, 1f) - BuildingsRules.Traffic(there, 500f, 0f, 1f) == DistrictSplit.TrafficShift(0) - DistrictSplit.TrafficShift(1),
            "uptown split: a lot's traffic is the rules' own with its district's shift, Uptown's lots a little busier and Cedar Heights' a little quieter");
    }

    // ---------------------------------------------------------------- the rebalance

    private static List<Lot> FirstPlan()
    {
        LayoutRules.Pin(null);
        var swaps = LayoutRules.Swaps.ToList();
        LayoutRules.Pin(swaps);
        List<Lot> first = LayoutRules.Lots.ToList();
        LayoutRules.Pin(null);
        return first;
    }

    private static void UptownSplitRebalance()
    {
        LayoutRules.Pin(null);
        List<Lot> plan = LayoutRules.Lots.ToList();
        List<string> swaps = LayoutRules.Swaps.ToList();
        List<Lot> first = FirstPlan();
        LayoutRules.Pin(null);
        int largeFirst = first.Count(l => !l.Open && l.Size == LayoutRules.LargeSize);
        Check(largeFirst == 28 && first.Count == plan.Count && first.All(l => !l.Rebalanced) && swaps.Count == 19, "uptown split: the first plan had 28 M1 stores in the band, of which the rebalance rebuilds 19 (" + swaps.Count + ")");
        int largeUptown = plan.Count(l => !l.Open && l.Size == LayoutRules.LargeSize && l.District == DistrictSplit.Uptown), largeCedar = plan.Count(l => !l.Open && l.Size == LayoutRules.LargeSize && l.District == DistrictSplit.CedarHeights);
        Check(largeUptown == DistrictSplit.LargeStoresUptown && largeCedar == DistrictSplit.LargeStoresCedar && largeUptown < 12 && largeCedar < 12,
            "uptown split: the plan keeps " + largeUptown + " M1 stores in Uptown and " + largeCedar + " in Cedar Heights, fewer than the 12 of Midtown, the game's most");
        bool nothingMoves = plan.Count == first.Count;
        int changed = 0;
        for (int i = 0; nothingMoves && i < plan.Count; i++)
        {
            Lot a = plan[i], b = first[i];
            nothingMoves &= a.Id == b.Id && a.Street == b.Street && a.Number == b.Number && a.X == b.X && a.Z == b.Z && a.Yaw == b.Yaw && a.Width == b.Width && a.Depth == b.Depth
                            && a.Spaces == b.Spaces && a.Strip == b.Strip && a.Index == b.Index && a.Class == b.Class && a.Open == b.Open && a.Block == b.Block;
            bool differs = a.Size != b.Size || a.Version != b.Version;
            changed += differs ? 1 : 0;
            nothingMoves &= differs == a.Rebalanced && (!differs || (b.Size == LayoutRules.LargeSize && a.Size == LayoutRules.SwapSize && a.Version == 1 && swaps.Contains(a.Id)));
        }
        Check(nothingMoves && changed == swaps.Count, "uptown split: of the plan only the " + changed + " rebuilt lots differ from the first plan's, in their size (M1 to J1 on the same five spaces); no id, address, place or neighbour moves");
        SpaceSize m = SpacesRules.Of(LayoutRules.LargeSize, 1), j = SpacesRules.Of(LayoutRules.SwapSize, 1);
        Check(m != null && j != null && m.Spaces == j.Spaces && j.DepthClass <= m.DepthClass + 5f && swaps.SequenceEqual(swaps.OrderBy(x => x, StringComparer.Ordinal)),
            "uptown split: a J1 office takes the five spaces an M1 store did, and fits every strip an M1 store stood on");
        // The special places' own M1 lots stay stores; the kept ones past them are on avenues while avenues last.
        var kept = plan.Where(l => !l.Open && l.Size == LayoutRules.LargeSize).ToList();
        Check(SpecialsRules.All.Where(p => p.Size == LayoutRules.LargeSize).All(p => kept.Any(l => l.Id == p.Lot)), "uptown split: the lots of the special places that are M1 stores (the furniture store, the dealership) stay stores");
        int keptOnAvenue = kept.Count(l => l.Facing == 0f || l.Facing == 180f), keptOther = kept.Count - keptOnAvenue;
        int avenuesFirst = first.Count(l => !l.Open && l.Size == LayoutRules.LargeSize && (l.Facing == 0f || l.Facing == 180f));
        Check(kept.Count == 9 && (keptOther == 0 || avenuesFirst < kept.Count), "uptown split: the stores kept are the special places' and then those on the avenues (" + keptOnAvenue + " of " + kept.Count + "), as a big store belongs on a main road");

        // Pins: a store someone holds stays a store, whatever else changes.
        string held = swaps[3];
        LayoutRules.Pin(new[] { held });
        Lot pinned = UptownLots.Get(held);
        bool pins = pinned != null && pinned.Size == LayoutRules.LargeSize && !pinned.Rebalanced && LayoutRules.Swaps.Count == swaps.Count && !LayoutRules.IsSwapped(held)
                    && swaps.Where(s => s != held).All(LayoutRules.IsSwapped) && LayoutRules.Lots.Count(l => !l.Open && l.Size == LayoutRules.LargeSize) == kept.Count + 1;
        LayoutRules.Pin(swaps);
        bool all = LayoutRules.Lots.Select(l => l.Size + "|" + l.Version).SequenceEqual(first.Select(l => l.Size + "|" + l.Version));
        LayoutRules.Pin(null);
        bool again = LayoutRules.Lots.Select(l => l.Id + "|" + l.Size + "|" + l.Version).SequenceEqual(plan.Select(l => l.Id + "|" + l.Size + "|" + l.Version));
        Check(pins && all && again, "uptown split: a pinned lot stays an M1 store, every lot pinned is the first plan again, and no pin is the rebalanced plan again, the same every time");

        // A rebalanced lot is a class Big lot with an office's size, and counts nowhere as a lease's.
        Check(plan.Where(l => l.Rebalanced).All(l => l.Class == LotClass.Big && l.Sizes.Count == 1 && l.Sizes[0] == LayoutRules.SwapSize), "uptown split: a rebuilt lot keeps its class and takes its one size, so no lease and no class count moves");
    }

    // ---------------------------------------------------------------- the census

    private static readonly Dictionary<string, string[]> SplitTypes = new Dictionary<string, string[]>
    {
        ["a1"] = new[] { RetailT }, ["a2"] = new[] { RetailT }, ["a3"] = new[] { OfficeT }, ["b1"] = new[] { HomeT }, ["c1"] = new[] { OfficeT, RetailT }, ["c2"] = new[] { RetailT },
        ["d2"] = new[] { OfficeT, RetailT }, ["f1"] = new[] { HomeT }, ["j1"] = new[] { OfficeT }, ["k1"] = new[] { OfficeT }, ["l1"] = new[] { HomeT }, ["m1"] = new[] { RetailT }, ["n1"] = new[] { HomeT }
    };

    private const string RetailT = "ba:buildingtype_retail", OfficeT = "ba:buildingtype_office", HomeT = "ba:buildingtype_residential", WarehouseT = "ba:buildingtype_warehouse";

    // The customers a shop of a size has room for (BuildingSizeData of Build 3682).
    private static int ShopRoom(string size)
    {
        switch (size.Substring(size.Length - 1))
        {
            case "a": return 15;
            case "c": return 30;
            case "d": return 40;
            case "m": return 75;
            default: return 0;
        }
    }

    // A lot's building as the district would stand it: the structure its id's hash picks among those its size and version take.
    private static CensusUnit SplitUnit(Lot l)
    {
        SpecialPlace place = SpecialsRules.At(l.Id);
        if (place != null)
        {
            return new CensusUnit { Type = place.Type == "ba:buildingtype_special" ? place.PlainType : place.Type, Capacity = place.Type == RetailT ? ShopRoom(place.Size) : 0 };
        }
        string letter = l.Size.Substring(l.Size.Length - 1);
        if (!SplitTypes.TryGetValue(letter + l.Version, out string[] types))
        {
            types = new[] { l.Class == LotClass.Warehouse ? WarehouseT : "venue" };
        }
        string type = types[(int)(RealLifeOverhaul.NewBuildings.NewBuildingsRules.Hash(l.Id + "|structure") % (uint)types.Length)];
        return new CensusUnit { Type = type, Capacity = type == RetailT ? ShopRoom(l.Size) : 0 };
    }

    private static CensusRow SplitRow(int district, IEnumerable<Lot> lots)
    {
        DistrictSplit.Bounds(district, LayoutRules.BandGround, out float x1, out float z1, out float x2, out float z2);
        return DistrictCensus.Count(DistrictSplit.IdOf(district), (x2 - x1) * (double)(z2 - z1), DistrictSplit.Figures(district), lots.Where(l => !l.Open && l.District == district).Select(SplitUnit));
    }

    private static void UptownSplitCensus()
    {
        LayoutRules.Pin(null);
        IReadOnlyList<Lot> lots = LayoutRules.Lots;
        CensusRow uptown = SplitRow(DistrictSplit.Uptown, lots), cedar = SplitRow(DistrictSplit.CedarHeights, lots);
        double volumeMost = DistrictCensus.OldVolumeMost(DistrictCensus.Old);
        int largeMost = DistrictCensus.OldLargeMost(DistrictCensus.Old);
        Check(Math.Abs(volumeMost - 1336.5) < 0.01 && largeMost == 12 && DistrictCensus.OldShopsMost(DistrictCensus.Old) == 81 && Math.Abs(DistrictCensus.OldUpperHomesMost(DistrictCensus.Old) - 22.4) < 0.01,
            "uptown split census: the game's old districts at most have a customer volume of 1,337 (Midtown), 12 large stores (Midtown), 81 shops (Industry City) and 22.4 upper-class homes (the Hamptons)");
        string uWhy = DistrictCensus.Verdict(uptown, volumeMost, largeMost), cWhy = DistrictCensus.Verdict(cedar, volumeMost, largeMost);
        Check(uWhy == null && cWhy == null, "uptown split census: neither district is over the old districts' most customer volume or large stores. " + uptown.Describe() + ". " + cedar.Describe());
        Check(uptown.Volume < volumeMost * 0.9 && cedar.Volume < uptown.Volume && uptown.Large == 6 && cedar.Large == 3 && uptown.Shops > 0 && cedar.Shops > 0,
            "uptown split census: Uptown's volume " + uptown.Volume.ToString("0") + " is under nine tenths of Midtown's, Cedar Heights' " + cedar.Volume.ToString("0") + " under Uptown's");
        double wealth = DistrictCensus.OldUpperHomesMost(DistrictCensus.Old);
        Check(uptown.UpperHomes <= wealth + 0.05 && cedar.UpperHomes < uptown.UpperHomes, "uptown split census: the upper-class homes are Uptown " + uptown.UpperHomes.ToString("0.0") + " and Cedar Heights " + cedar.UpperHomes.ToString("0.0")
            + ", none over the game's most (" + wealth.ToString("0.0") + ")");

        // Made to fail: the band as it was (one district with the neighbours' blend, the first plan's stores) is over, and a split that keeps every store is over in both halves.
        List<Lot> first = FirstPlan();
        LayoutRules.Pin(null);
        var band = DistrictCensus.Count("band", 0.0, DistrictValues.Written, first.Where(l => !l.Open).Select(SplitUnit));
        string bandWhy = DistrictCensus.Verdict(band, volumeMost, largeMost);
        Check(bandWhy != null && band.Large == 28 && band.Volume > volumeMost * 3, "uptown split census: the band as one district fails the census (28 large stores, volume " + band.Volume.ToString("0") + "): " + bandWhy);
        CensusRow halfA = DistrictCensus.Count("a", 0.0, DistrictSplit.Figures(0), first.Where(l => !l.Open && l.District == 0).Select(SplitUnit));
        CensusRow halfB = DistrictCensus.Count("b", 0.0, DistrictSplit.Figures(1), first.Where(l => !l.Open && l.District == 1).Select(SplitUnit));
        Check(DistrictCensus.Verdict(halfA, volumeMost, largeMost) != null && DistrictCensus.Verdict(halfB, volumeMost, largeMost) != null && halfA.Large == 14 && halfB.Large == 14,
            "uptown split census: the halves with every first-plan store (14 large stores each) fail it, so the rebalance is what passes it");
        CensusRow heavy = DistrictCensus.Count("heavy", 0.0, DistrictValues.Written, lots.Where(l => !l.Open && l.District == 0).Select(SplitUnit));
        Check(DistrictCensus.Verdict(heavy, volumeMost, largeMost) != null && heavy.Volume > uptown.Volume, "uptown split census: Uptown's buildings with the old band's customer weight (0.8) fail it on volume (" + heavy.Volume.ToString("0") + ")");
        Check(DistrictCensus.Verdict(null, 1.0, 1) != null && DistrictCensus.Verdict(new CensusRow { Demand = 1f, ShopCapacity = 10 }, 10.0, 0) == null && DistrictCensus.Verdict(new CensusRow { Demand = 1f, ShopCapacity = 11 }, 10.0, 0) != null
              && DistrictCensus.Verdict(new CensusRow { Large = 1 }, 10.0, 0) != null, "uptown split census: a district on the limit passes, one over it by a customer or a large store does not");
    }

    // ---------------------------------------------------------------- a save from before the split

    private static void UptownSplitMigration()
    {
        LayoutRules.Pin(null);
        List<string> swaps = LayoutRules.Swaps.ToList();
        var pinned = new List<string>();
        var rebuilt = new List<string>();
        var retired = new List<string>();
        var kept = new HashSet<string>(swaps.Where((s, i) => i % 2 == 0));
        var held = new HashSet<string>(new[] { swaps[0], swaps[1], swaps[4] });
        SplitRules.Decide(swaps, kept.Contains, held.Contains, pinned, rebuilt, retired);
        Check(pinned.SequenceEqual(new[] { swaps[0], swaps[1], swaps[4] }) && rebuilt.Count == swaps.Count - 3 && pinned.All(held.Contains) && rebuilt.All(r => !held.Contains(r))
              && retired.All(r => kept.Contains(r) && rebuilt.Contains(r)) && retired.Count == rebuilt.Count(kept.Contains) && !retired.Contains(swaps[0]),
            "uptown split save: a store someone holds stays a store; the rest are rebuilt, and only those the save kept records for have them let go (" + pinned.Count + " kept, " + rebuilt.Count + " rebuilt, " + retired.Count + " let go)");
        SplitRules.Decide(swaps, null, null, pinned, rebuilt, retired);
        Check(pinned.Count == 0 && rebuilt.Count == swaps.Count && retired.Count == 0, "uptown split save: a save that never had the band holds none, rebuilds every lot and lets go nothing");
        SplitRules.Decide(swaps, s => true, s => true, pinned, rebuilt, retired);
        Check(pinned.Count == swaps.Count && rebuilt.Count == 0 && retired.Count == 0, "uptown split save: a save that holds every store keeps every store, and loses nothing");
        SplitRules.KeepAll(swaps, pinned);
        Check(pinned.SequenceEqual(swaps) && SplitRules.Undecided(0) && !SplitRules.Undecided(LayoutRules.RebalanceGeneration) && !SplitRules.Undecided(LayoutRules.RebalanceGeneration + 1),
            "uptown split save: a line that cannot be read keeps every lot as it was; the rebalance is decided once for a save and never again");
        Check(SplitRules.StartLevel(0, 0) == 1000 && SplitRules.StartLevel(0, 950) == 950 && SplitRules.StartLevel(1010, 950) == 1010 && SplitRules.StartLevel(5000, 0) == DemandRules.LevelMost
              && SplitRules.StartLevel(1, 0) == DemandRules.LevelLeast, "uptown split save: the second district's market starts from its own level, else the band's, else even, always inside the market's caps");
        Check(SplitRules.KindOf("stats") == SplitKind.Both && SplitRules.KindOf("demand") == SplitKind.Both && SplitRules.KindOf("filter") == SplitKind.Both && SplitRules.KindOf("level") == SplitKind.Both
              && SplitRules.KindOf("event") == SplitKind.First && SplitRules.KindOf("plan") == SplitKind.First && SplitRules.KindOf("building") == SplitKind.Position && SplitRules.KindOf("deed") == SplitKind.Position
              && SplitRules.KindOf("cell") == SplitKind.Position && SplitRules.KindOf("project") == SplitKind.Position,
            "uptown split save: the stats, demand rows, map row and market level are copied for the second district, a market event or a pricing plan stays with Uptown, and a thing with a place follows its place");

        // The district's line: the new parts round trip, an older line reads as before the split, and what a later version adds is skipped.
        var state = new LifeState { Plan = LayoutRules.PlanGeneration, MarketLevel = 1010, MarketDay = 33, CedarLevel = 960, CedarDay = 34, Split = LifeStore.SplitGeneration, Rebalance = LayoutRules.RebalanceGeneration };
        state.Pinned.Add(swaps[2]);
        state.Pinned.Add(swaps[7]);
        string line = LifeStore.Write(state);
        LifeState back = LifeStore.Read(line, out int version);
        Check(back != null && version == LifeStore.Version && back.MarketLevel == 1010 && back.MarketDay == 33 && back.CedarLevel == 960 && back.CedarDay == 34 && back.Split == 1 && back.Rebalance == 1
              && back.Pinned.SequenceEqual(new[] { swaps[2], swaps[7] }) && back.LevelOf(0) == 1010 && back.LevelOf(1) == 960 && back.DayOf(1) == 34 && LifeStore.Write(back) == line,
            "uptown split save: the line carries the second district's market level, the split's mark, the rebalance's generation and the stores it leaves, and writes back exactly");
        LifeState old = LifeStore.Read("v=1^f:^g:4^o:1^m:1010,33^x:1", out int _);
        Check(old != null && old.Split == 0 && old.Rebalance == 0 && old.Pinned.Count == 0 && old.CedarLevel == 0 && old.MarketLevel == 1010 && old.Mixed && old.Opened && old.Plan == 4,
            "uptown split save: a line from before the split reads as before it: no mark, nothing decided, the band's market level for Uptown");
        LifeState later = LifeStore.Read(line + "^z:later^k:" + string.Join(",", Enumerable.Repeat("uptown_1_01_0001", 5)), out int _);
        Check(later != null && later.Pinned.Count == 3 && LifeStore.PinnedMost >= 64, "uptown split save: a part a later version adds is skipped, a lot named twice is kept once, the list is bounded");
        LifeState hostile = LifeStore.Read("v=1^q:abc,-5^s:9999^r:x^k:a b,|;,uptown_ok", out int _);
        Check(hostile != null && hostile.CedarLevel == 0 && hostile.Split == 99 && hostile.Rebalance == 0 && hostile.Pinned.SequenceEqual(new[] { "uptown_ok" }), "uptown split save: a damaged part is bounded, never thrown on");

        // What the district save lets go for an address.
        var records = new List<StoredRecord>();
        var items = new List<object>();
        string street = LayoutStreets.All[0].Id;
        for (int i = 0; i < 3; i++)
        {
            var r = new StoredRecord();
            r.Addresses.Add(new PlaceKey(street, 1 + i));
            records.Add(r);
            items.Add(i);
        }
        int gone = DistrictSaveStore.RetireAddress(records, items, street, 2);
        Check(gone == 1 && records.Count == 2 && items.Count == 2 && !items.Contains(1), "uptown split save: the district save lets go the records of one address, the others stay in their order");
    }

    // ---------------------------------------------------------------- the sources

    private static string SplitSource(string relative)
    {
        return ModSource(relative).Replace("\r\n", "\n");
    }

    private static void UptownSplitSources()
    {
        string director = SplitSource(@"Uptown\LifeDirector.cs"), split = SplitSource(@"Uptown\LifeDirector.Split.cs"), market = SplitSource(@"Uptown\LifeDirector.Market.cs");
        string early = MemberText(director, "private static void Early()"), awake = MemberText(director, "private void Awake()"), read = MemberText(director, "private static void ReadState()");
        Check(early.IndexOf("RetireEarlierPlan(", StringComparison.Ordinal) >= 0 && early.IndexOf("DecidePlan(", StringComparison.Ordinal) > early.IndexOf("RetireEarlierPlan(", StringComparison.Ordinal)
              && early.IndexOf("DecidePlan(", StringComparison.Ordinal) < early.IndexOf("Know(", StringComparison.Ordinal) && awake.Contains("DecidePlan(SaveGameManager.Current);")
              && awake.IndexOf("DecidePlan(", StringComparison.Ordinal) < awake.IndexOf("ReadState();", StringComparison.Ordinal) && read.IndexOf("ApplyDecision();", StringComparison.Ordinal) < read.IndexOf("WriteState();", StringComparison.Ordinal),
            "uptown split sources: the rebalance is decided before anything reads the plan (at the district save's early step, else at the city load), and the decision is written into the line");
        string decide = MemberText(split, "private static void DecidePlan(GameInstance save)");
        Check(decide.Contains("LayoutRules.Pin(null);") && decide.Contains("SplitRules.Decide(") && decide.Contains("DistrictSaveDirector.RetireAddresses(save,") && decide.Contains("SplitRules.KeepAll(") && decide.Contains("GroundSession.Agrees(")
              && decide.Contains("read.Plan < LayoutRules.PlanGeneration") && decide.Contains("SplitRules.Undecided(read.Rebalance)"),
            "uptown split sources: only where the band may live; a held store is pinned, the shops' records of the others let go; an unreadable line keeps all; a save of an earlier plan holds nothing");
        string held = MemberText(split, "private static bool HeldLot(string lotId, LifeState state)");
        Check(held.Contains("LifeGame.Someones(b)") && held.Contains("state.Covering(lotId) != null") && held.Contains("state.IsCleared(lotId)"),
            "uptown split sources: a store is held by the player's rent or deed, another player's, a building the construction game finished over it or knocked down there");
        string move = MemberText(split, "private static string MoveBand(GameInstance save)");
        string first = MemberText(director, "private static bool FirstLife()");
        Check(move.Contains("LifeGame.MoveRows(save,") && move.Contains("_splitWas < LifeStore.SplitGeneration") && move.Contains("!LifeGame.HasStats(save, DistrictSplit.CedarHeights)") && move.Contains("_splitDone = true;")
              && first.IndexOf("MoveBand(save)", StringComparison.Ordinal) < first.IndexOf("LifeGame.Stats(save)", StringComparison.Ordinal) && first.IndexOf("MoveBand(save)", StringComparison.Ordinal) > 0
              && first.Contains("Logger?.Info(moved);"), "uptown split sources: the first load after the split moves what the save holds before the second district's stats are made, once, with a log line");
        Check(market.Contains("for (int d = 0; d < DistrictSplit.Count; d++)") && market.Contains("LifeGame.ApplyLevel(district, Level[district]);") && market.Contains("_state.SetMarket(district, Level[district], day);")
              && market.Contains("kept = _state.LevelOf(DistrictSplit.Uptown);"), "uptown split sources: each district has its own market level, its own demand, its own day in the line, the second starting from the band's");
        string stats = MemberText(SplitSource(@"Uptown\LifeGame.Buildings.cs"), "public static int Stats(GameInstance save)");
        Check(stats.Contains("first.nextNewBusinessDay") && stats.Contains("d != DistrictSplit.Uptown && first != null"), "uptown split sources: a second district's stats start as the band's were");

        // Nothing of the band is one district's alone any more.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var users = new List<string>();
        if (Directory.Exists(scripts))
        {
            foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                if (text.Contains("DistrictValues.Id") || text.Contains("\"reallife_neighborhood_uptown\""))
                {
                    users.Add(System.IO.Path.GetFileName(path));
                }
            }
        }
        string[] allowed = { "BuildCatalogue.cs", "ConstructionDirector.cs", "DistrictBuildings.cs", "DistrictSplit.cs", "DistrictValues.cs", "LifeDirector.Tests.cs", "LifeText.cs", "ConstructionLinks.cs", "Wiring.ConstructionLinks.cs" };
        users.RemoveAll(u => allowed.Contains(u));
        Check(users.Count == 0, "uptown split sources: no file reads the first district's id as the whole band's but those that name a district for a reason" + (users.Count > 0 ? ": " + string.Join(", ", users) : string.Empty));
        string lifeGame = SplitSource(@"Uptown\LifeGame.cs") + SplitSource(@"Uptown\LifeGame.Map.cs") + SplitSource(@"Uptown\LifeGame.Buildings.cs") + SplitSource(@"Uptown\LifeGame.Mix.cs") + SplitSource(@"Uptown\LifeGame.Market.cs");
        Check(!lifeGame.Contains("_record") && !lifeGame.Contains("_zone") && !lifeGame.Contains("_row ") && lifeGame.Contains("Records[d]") && lifeGame.Contains("Zones[district]"),
            "uptown split sources: the game's side keeps a record, a zone and a row for each district, none for the band alone");
        string construction = SplitSource(@"Construction\ConstructionDirector.Grow.cs") + SplitSource(@"Construction\ConstructionDirector.Zone.cs");
        Check(construction.Contains("GrowRules.MayStart(GrowingIn(district), day, _state.NextGrowDay)") && construction.Contains("DistrictOfColumn(c) == district") && construction.Contains("CouncilSitting(day, d)")
              && construction.Contains("BuildRandom.Of(_state.Seed, day, 0x7A0E + district)"),
            "uptown split sources: the growth is capped under each district's own sites, by its own demand and free land, and the council sits for each district with a draw of its own");
        Check(SplitSource(@"Construction\LandRules.cs").Contains("public int District;") && SplitSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("District = l.District, DistrictKey = DistrictSplit.IdOf(l.District),")
              && SplitSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("ConstructionBridges.LandPrice = lot => DistrictBuildings.LandPricePerSqm(lot);")
              && SplitSource(@"Wiring\Wiring.BuildModeLinks.cs").Contains("BuildModeBridges.DistrictAt = (x, z) => DistrictSplit.OfX(x);"),
            "uptown split sources: the construction game reads each lot's district, its land price and its demand from the district, and build mode's bars follow the map's middle");
    }

    // ---------------------------------------------------------------- the texts

    private static void UptownSplitTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys = { DistrictSplit.CedarId, "reallife_uptown_ground_terminus_cedar", "reallife_uptown_test_split", "reallife_uptown_test_split_done" };
        bool all = en != null && pt != null && zh != null;
        foreach (string key in keys)
        {
            all &= en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && en[key].Length <= 240 && SameHoles(en[key], pt[key], zh[key]);
        }
        Check(all, "uptown split texts: the second district's name, its terminus, the census test and its notice are in en, pt and zh-cn with the same {holes}, at most 240 characters");
        // What's new: the feature's line is in some row of every language (never only the next row's, which the version step moves).
        var tokens = new Dictionary<string, string> { ["en"] = "Cedar Heights", ["pt"] = "Cedar Heights", ["zh-cn"] = "雪松高地", ["de"] = "Cedar Heights", ["fr"] = "Cedar Heights", ["es"] = "Cedar Heights", ["ru"] = "Седар-Хайтс", ["nl"] = "Cedar Heights", ["tr"] = "Cedar Heights" };
        bool news = true;
        string newsMissing = null;
        foreach (KeyValuePair<string, string> token in tokens)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, token.Key + ".json"));
            bool has = words != null && words.Any(w => w.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && w.Key != "reallife_changes_title" && w.Value.Contains(token.Value) && !w.Value.Contains("  "));
            news &= has;
            newsMissing = has ? newsMissing : newsMissing ?? token.Key;
        }
        Check(news, "uptown split texts: What's new has the split's line in a row of all nine languages, without a double space" + (newsMissing != null ? " (not in " + newsMissing + ")" : string.Empty));
        Check(all && en[DistrictSplit.CedarId] == "Cedar Heights" && zh[DistrictSplit.CedarId] == "雪松高地" && en["reallife_uptown_ground_terminus_cedar"] == "Cedar Heights" && en[DistrictValues.Id] == "Uptown",
            "uptown split texts: the districts are Uptown and Cedar Heights (雪松高地), the terminus named for the second");
        Check(all && !en["reallife_meta_title_uptown"].Equals("Uptown") && en["reallife_meta_title_uptown"].Contains("Uptown") && en["reallife_meta_title_uptown"].Contains("Cedar Heights"),
            "uptown split texts: the feature's title names both districts");
        Check(all && en["reallife_schools_enabled"].Contains("Cedar Heights") && !en["reallife_schools_enabled"].Contains("Uptown") && pt["reallife_schools_enabled"].Contains("Cedar Heights") && zh["reallife_schools_enabled"].Contains("雪松高地"),
            "uptown split texts: Birchwood School is in Cedar Heights in every language");
    }
}
