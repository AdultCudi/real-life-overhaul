using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewGround;
using RealLifeOverhaul.Uptown;

// Uptown's ground (Uptown/LayoutRules.cs, LayoutPlans.cs, LayoutStreets.cs, UptownLots.cs and the folder's sources): the band's
// streets on the game's own grid, the lots round its blocks in Manhattan's mix with their classes' footprints, clear of
// the streets and of one another, their ids, streets and numbers fixed; the whole band laid at every load, three rows of
// eighteen blocks: its streets built as lanes and joined through 1st Avenue's own junctions and its corner at 9th Street
// (a model of each, from the game's baked lanes), never off the road grid, the same twice; the walking ground under every
// lot and its door, one surface for each mesh, joined to the game's own where they stop (measured from its data), off
// what stands on the old corners and the walls that stay, its margin kept only where it meets no other ground; the art,
// the walls, the colliders, the hiding, the zones and the termini where they belong. Over the sources: the game touched
// only through the folder's Game files, nothing saved or sent, no LINQ, nothing left to chance, the texts in three languages.
internal static partial class Program
{
    private static void Uptowns(bool print)
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        UptownStreetChecks();
        UptownLotChecks(print);
        UptownGroundChecks();
        UptownRoadChecks(print);
        UptownWalkChecks();
        UptownJoinChecks();
        UptownPlaceChecks();
        UptownCountChecks();
        UptownLifeChecks();
        UptownTimingChecks();
        UptownSourceChecks(scripts);
        UptownGameSourceChecks(scripts);
        UptownTextChecks(scripts);
        UptownFogChecks(scripts);   // the city map's fog cut away over the band (UptownFogCheck.cs)
    }

    // ---------------------------------------------------------------- the streets

    private static void UptownStreetChecks()
    {
        LayoutStreet[] all = LayoutStreets.All;
        Check(all.Length == 22, "uptown streets: nineteen streets and three avenues (" + all.Length + ")");
        Check(all.All(s => s.Id.StartsWith("reallife:street_uptown_", StringComparison.Ordinal) && s.Id.StartsWith("reallife:street_", StringComparison.Ordinal)),
            "uptown streets: every id under the mod's own street prefix, never one of the game's");
        Check(all.Select(s => s.Id).Distinct().Count() == all.Length && all.Select(s => s.Abbreviation).Distinct().Count() == all.Length
              && all.All(s => s.Abbreviation.Length == 3 && s.Abbreviation != "PRL"), "uptown streets: an id and a three-letter short name of their own each");
        Check(all.All(s => s.NameKey.StartsWith("reallife_newbuildings_street_", StringComparison.Ordinal)), "uptown streets: their names under the mod's street names' prefix");
        for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
        {
            LayoutStreet s = LayoutStreets.Street(k);
            Check(s != null && !s.Avenue && s.Index == k && LayoutStreets.Find(s.Id) == s, "uptown streets: street " + k + " found by its place and its id");
        }
        Check(LayoutStreets.Street(-6) == null && LayoutStreets.Street(14) == null && LayoutStreets.Avenue(0) == null && LayoutStreets.Avenue(4) == null
              && LayoutStreets.Find("ba:street_secondstreet") == null && LayoutStreets.Find(null) == null, "uptown streets: nothing outside the band, and none of the game's");
        string[] game = { "ba:street_ninthstreet", "ba:street_eighthstreet", "ba:street_seventhstreet", "ba:street_sixthstreet", "ba:street_fifthstreet",
            "ba:street_fourthstreet", "ba:street_thirdstreet", "ba:street_secondstreet", "ba:street_firststreet" };
        for (int k = 0; k <= 8; k++)
        {
            LayoutStreet s = LayoutStreets.Street(k);
            Check(s.Continues == game[k] && s.FirstNumber == 1001, "uptown streets: street " + k + " continues the game's " + game[k] + " with numbers from 1001");
        }
        Check(all.Where(s => s.Continues == null).All(s => s.FirstNumber == 1), "uptown streets: new streets and the avenues number from 1");
        Check(LayoutStreets.ContinuedFirst > 82 * 10, "uptown streets: a continued street's numbers never meet the game's (its highest is 82)");
        for (int r = 1; r <= 3; r++)
        {
            Check(LayoutStreets.Avenue(r) != null && LayoutStreets.Avenue(r).Avenue && LayoutStreets.Avenue(r).Index == r, "uptown streets: avenue " + r + " found by its row");
        }
        // The game's own grid: Manhattan's street modules' middles at its north edge, and 1st Avenue's.
        float[] modules = { -301.67f, -214.37f, -127.07f, -39.77f, 47.53f, 134.83f, 222.13f, 309.43f, 396.73f };
        for (int k = 0; k <= 8; k++)
        {
            Check(Near(LayoutRules.StreetX(k), modules[k], 0.05f), "uptown streets: street " + k + " stands on the game's own module at x " + modules[k]);
        }
        Check(Near(LayoutRules.AvenueZ(0), 464.58f, 0.01f) && Near(LayoutRules.AvenueZ(1), 611.78f, 0.01f), "uptown streets: 1st Avenue as the game has it, Uptown Avenue a street module and a crossing north");
        Check(Near(LayoutRules.StreetModule + 2f * LayoutRules.CrossingHalfZ, LayoutRules.AvenuePitch, 0.01f)
              && Near(LayoutRules.AvenueModule + 2f * LayoutRules.CrossingHalfX, LayoutRules.StreetPitch, 0.01f),
            "uptown streets: the game's modules tile the grid exactly, crossings and streets, crossings and avenues");
    }

    // ---------------------------------------------------------------- the lots

    private static void UptownLotChecks(bool print)
    {
        List<Lot> lots = LayoutRules.Lots;
        List<Lot> first = LayoutRules.FirstLots;
        int[] all = new int[6], firstMix = new int[6];
        foreach (Lot l in lots)
        {
            all[(int)l.Class]++;
        }
        int mixed = all[0] + all[1] + all[2] + all[3];
        // The venues: two theatres and three cinemas (the owner, 25 Oct 2026), lots of their own on the avenues, none in the first block.
        List<Lot> venues = lots.FindAll(l => l.Class == LotClass.Theatre || l.Class == LotClass.Cinema);
        Check(all[4] == 2 && all[5] == 3 && venues.TrueForAll(l => (l.Facing == 0f || l.Facing == 180f) && !l.First && l.Spaces == 5 && l.Width >= 35 && l.Width <= 40
                                                                    && l.Depth == (int)LayoutRules.AvenueDepth
                                                                    && l.Size == (l.Class == LotClass.Theatre ? "ba:buildingsize_r" : "ba:buildingsize_s") && SpacesRules.SpacesOf(l.Size, l.Version) == 5),
            "uptown lots: two theatres' lots (the game's size R) and three cinemas' (S), five spaces each as their size takes, on the avenues and out of the first block ("
            + string.Join(", ", venues.ConvertAll(l => l.Id + " " + l.Number + " " + l.Street.Substring(l.Street.LastIndexOf('_') + 1))) + ")");
        foreach (Lot l in first)
        {
            firstMix[(int)l.Class]++;
        }
        if (print)
        {
            Console.WriteLine("uptown lots: " + lots.Count + " in all (" + string.Join(", ", all) + "), the first block " + first.Count + " (" + string.Join(", ", firstMix) + ")");
        }
        int openSpaces = lots.Count(l => l.Open), built = lots.Count - openSpaces;
        Check(built >= 440 && built <= 600, "uptown lots: about 500 buildings' lots in the band, each the game's own size on its spaces (" + built + ")");
        Check(first.Count >= 55 && first.Count <= 90, "uptown lots: a first block of 55 to 90 (" + first.Count + ")");
        for (int c = 0; c < 4; c++)
        {
            int ofClass = lots.Count(l => !l.Open && (int)l.Class == c), plannedAll = lots.Count(l => !l.Open && (int)l.Class < 4);
            float share = 100f * ofClass / plannedAll, want = 100f * LayoutRules.Share((LotClass)c) / 98f;
            // Within 6 points: a run's last spaces take what still fits, most often a small size, so small buildings run a little over.
            Check(Math.Abs(share - want) <= 6f, "uptown lots: " + (LotClass)c + " " + share.ToString("0.0") + "% of the band's buildings, Manhattan's " + want.ToString("0.0") + "%");
        }
        Check(firstMix[0] < first.Count && firstMix[1] + firstMix[2] + firstMix[3] >= first.Count / 8, "uptown lots: the first block holds offices, big stores and a warehouse as well as small lots");
        var id = new Regex(@"^uptown_([1-3]_(0[0-9]|1[0-9])|4_(0[1-9]|1[0-8]))_\d\d\d\d$");   // the band's rows and columns, and the ring round it
        Check(lots.All(l => id.IsMatch(l.Id)) && lots.Select(l => l.Id).Distinct().Count() == lots.Count && lots.All(l => !UptownLots.OfEarlierPlan(l.Id)),
            "uptown lots: every id of the form uptown_<row>_<column>_<nnnn>, letters, digits and underscores, none twice, none an id of an earlier plan's");
        Check(UptownLots.OfEarlierPlan("uptown_1_14_04") && UptownLots.OfEarlierPlan("uptown_3_18_12") && UptownLots.OfEarlierPlan("uptown_1_14_004") && UptownLots.OfEarlierPlan("uptown_2_07_105")
              && !UptownLots.OfEarlierPlan(lots[0].Id) && !UptownLots.OfEarlierPlan("uptown_1_14_4") && !UptownLots.OfEarlierPlan("uptown_x_14_04") && !UptownLots.OfEarlierPlan(null)
              && !UptownLots.OfEarlierPlan("uptown_1_14_0a4") && !UptownLots.OfEarlierPlan("uptown_place_fuel"),
            "uptown lots: an id of an earlier plan (two or three digits in its block) is told apart from this plan's, so a save's land there is retired");
        Check(lots.All(l => UptownLots.Get(l.Id) == l) && UptownLots.Get("uptown_9_99_99") == null && UptownLots.Get(null) == null, "uptown lots: each found by its id, nothing else");
        Check(lots.SequenceEqual(lots.OrderBy(l => l.Row).ThenBy(l => l.Column)) && lots.All(l => l.Block == l.Row * 100 + l.Column), "uptown lots: in id order, row by row, the block its row and column");
        Check(first.All(l => l.First && l.Row == 1 && l.Column >= 6 && l.Column <= 13) && lots.Count(l => l.First) == first.Count, "uptown lots: the first block is row 1 between 9th and 1st Street");
        foreach (Lot l in lots)
        {
            // On whole spaces: as many as its size takes (one for an open run's), each the standard or a metre or two wider.
            SpaceSize size = SpacesRules.Of(l.Size, l.Version);
            bool ok = (l.Open ? l.Size == null && l.Spaces == 1 : size != null && size.Spaces == l.Spaces && l.Depth >= size.DepthClass && size.Class == l.Class)
                      && l.Width >= l.Spaces * SpacesRules.SpaceWidth && l.Width <= l.Spaces * (SpacesRules.SpaceWidth + 2) && l.Yaw % 90 == 0 && Near(l.Facing, (l.Yaw + 180) % 360);
            LayoutRules.Block(l.Row, l.Column, out float x0, out float z0, out float x1, out float z1);
            Corners(l, out float lx0, out float lz0, out float lx1, out float lz1);
            ok &= lx0 >= x0 - 0.01f && lx1 <= x1 + 0.01f && lz0 >= z0 - 0.01f && lz1 <= z1 + 0.01f;
            // Its front on its street's line, facing it.
            float front = l.Facing == 0f ? lz1 : l.Facing == 180f ? lz0 : l.Facing == 90f ? lx1 : lx0;
            float edge = l.Facing == 0f ? z1 : l.Facing == 180f ? z0 : l.Facing == 90f ? x1 : x0;
            ok &= Near(front, edge, 0.02f);
            LayoutStreet street = LayoutStreets.Find(l.Street);
            ok &= street != null && (l.Facing == 0f || l.Facing == 180f ? street.Avenue && street.Index == (l.Facing == 0f ? l.Row : l.Row - 1)
                : !street.Avenue && street.Index == LayoutRules.WestStreet(l.Column) + (l.Facing == 90f ? 1 : 0));
            Check(ok, "uptown lots: " + l.Id + " (" + l.Class + " " + (size != null ? size.Name : "open") + ", " + l.Spaces + " spaces, " + l.Width + " m) stands on its size's whole spaces, "
                      + "inside its block, its front on the street it names");
        }
        UptownSpaceChecks(lots, print);
        // Side by side, as Manhattan's: every laid block's edge is the lots' fronts and the corner lots' sides, but for what
        // a strip has over its lots' whole metres (under a metre each end) and the strips' ends.
        float worstEdge = 1f;
        string thinBlock = null;
        foreach (var block in lots.Where(l => !LayoutRules.Places.Any(p => p.Block == l.Block) && !LayoutRules.IsRing(l.Row, l.Column)).GroupBy(l => l.Block))
        {
            Lot one = block.First();
            LayoutRules.Block(one.Row, one.Column, out float bx0, out float bz0, out float bx1, out float bz1);
            float edge = (one.First ? 1f : 2f) * (bx1 - bx0) + 2f * (bz1 - bz0), covered = 0f;
            foreach (Lot l in block)
            {
                Corners(l, out float lx0, out float lz0, out float lx1, out float lz1);
                covered += Math.Abs(lz1 - bz1) < 0.55f ? lx1 - lx0 : 0f;
                covered += Math.Abs(lz0 - bz0) < 0.55f && !one.First ? lx1 - lx0 : 0f;
                covered += Math.Abs(lx0 - bx0) < 0.55f ? lz1 - lz0 : 0f;
                covered += Math.Abs(lx1 - bx1) < 0.55f ? lz1 - lz0 : 0f;
            }
            float share = covered / edge;
            if (share < worstEdge)
            {
                worstEdge = share;
                thinBlock = one.Block.ToString();
            }
        }
        Check(worstEdge >= 0.93f, "uptown lots: every block's edge is lots side by side, the corners its avenues', a place's two blocks apart (the least " + (100f * worstEdge).ToString("0") + "%, block " + thinBlock + ")");
        // Three parks kept, one in each part of the band and each in another row (25 Oct 2026), every other square built, never a landmark's block.
        int planned = 0, kept = 0;
        var keptRows = new HashSet<int>();
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                planned += LayoutRules.PlannedSquare(row, column) ? 1 : 0;
                if (LayoutRules.IsSquare(row, column))
                {
                    kept++;
                    keptRows.Add(row);
                }
                if (LayoutRules.IsSquare(row, column) == lots.Any(l => l.Row == row && l.Column == column))
                {
                    kept = -99;   // a kept square with lots, or a laid block with none
                }
            }
        }
        Check(planned == 17 && kept == 3 && keptRows.Count == LayoutRules.Rows
              && lots.Where(l => !LayoutRules.IsRing(l.Row, l.Column)).Select(l => l.Block).Distinct().Count() == LayoutRules.Rows * LayoutRules.Columns - kept
              && LayoutRules.IsSquare(2, 2) && LayoutRules.IsSquare(3, 9) && LayoutRules.IsSquare(1, 17),
            "uptown lots: of the plan's 17 squares 14 are built and " + kept + " stay parks and plazas, west, middle and east, one in each row, the rest of the 54 blocks laid with lots");
        // Clear of one another: two lots side by side, never one over another.
        var byBlock = lots.GroupBy(l => l.Block).ToList();
        int clashes = 0;
        foreach (var block in byBlock)
        {
            Lot[] b = block.ToArray();
            for (int i = 0; i < b.Length; i++)
            {
                for (int j = i + 1; j < b.Length; j++)
                {
                    clashes += Overlap(b[i], b[j]) ? 1 : 0;
                }
            }
        }
        Check(clashes == 0 && LayoutRules.LotGap == 0f, "uptown lots: the lots of a block stand side by side, none over another (" + clashes + ")");
        // Clear of the streets: inside a block, so off every street, avenue and crossing module.
        Check(lots.All(l => OffStreets(l)), "uptown lots: none on a street, an avenue or a crossing");
        // The big places' ground: as the plan's table gives it, its own id and address, never a lot, the lots round it clear.
        List<Lot> places = LayoutRules.Places;
        Lot store = places.FirstOrDefault(l => l.Id == "uptown_place_wholesale"), fuel = places.FirstOrDefault(l => l.Id == "uptown_place_fuel");
        bool placesOk = places.Count == 2 && store != null && fuel != null && places.All(l => UptownLots.Get(l.Id) == null && LayoutStreets.Find(l.Street) != null);
        if (placesOk)
        {
            Corners(store, out float sx0, out float sz0, out float sx1, out float sz1);
            Corners(fuel, out float fx0, out float fz0, out float fx1, out float fz1);
            LayoutRules.Block(1, 8, out float b8x0, out _, out float b8x1, out _);
            LayoutRules.Block(2, 6, out float b6x0, out _, out _, out _);
            placesOk &= sx0 >= b8x0 - 0.01f && sx0 <= b8x0 + 0.5f && sx1 <= b8x1 + 0.01f && sx1 >= b8x1 - 0.5f && sz0 <= 557.2f && Near(sz1, LayoutRules.AvenueZ(1) - LayoutRules.AvenueHalf, 0.1f)
                        && store.Facing == 0f && store.Index == 0
                        && fx0 >= b6x0 - 0.01f && fx0 <= b6x0 + 0.5f && fuel.Spaces == 4 && fuel.Index == 0 && Near(fz0, LayoutRules.AvenueZ(1) + LayoutRules.AvenueHalf, 0.02f) && Near(fz1 - fz0, 32f, 0.02f)
                        && fuel.Facing == 180f && store.Block == 108 && fuel.Block == 206;
            // The streets' lots end short of the store's back; the station's strip goes on beside it, side by side.
            placesOk &= lots.All(l => l.Block != store.Block || l.Strip == store.Strip || Gap(l, store) >= LayoutRules.PlaceClear - 0.02f);
            placesOk &= lots.All(l => l.Block != fuel.Block || !Overlap(l, fuel));
        }
        Check(placesOk, "uptown places: the wholesale store's ground over block 108's whole Uptown Avenue front (back to z 557) and the filling station's on four spaces of block 206's corner "
              + "of 9th Street and Uptown Avenue (32 m back), each with its own id and address, never a lot, the streets' lots " + LayoutRules.PlaceClear + " m short of the store's back");
        // Numbers: odd on one side, even on the other, from the street's first, along it, a place's among them; every address once.
        var numbered = lots.Concat(places).ToList();
        Check(numbered.Select(l => l.Street + "#" + l.Number).Distinct().Count() == numbered.Count, "uptown lots: every address once, the places' too");
        foreach (var side in numbered.GroupBy(l => l.Street + (l.Number % 2)))
        {
            Lot[] s = side.OrderBy(l => l.Order).ToArray();
            LayoutStreet street = LayoutStreets.Find(s[0].Street);
            bool ok = s.Select((l, i) => l.Order == i && l.Number == s[0].Number + 2 * i).All(x => x);
            ok &= s[0].Number == street.FirstNumber || s[0].Number == street.FirstNumber + 1;
            Check(ok, "uptown lots: the numbers along " + s[0].Street + " (" + (s[0].Number % 2 == 1 ? "odd" : "even") + " side) run from its first, two apart, in order");
        }
        Check(lots.Where(l => LayoutStreets.Find(l.Street).Continues != null).All(l => l.Number > 1000), "uptown lots: on a continued street every number is over 1000");
        Check(lots.All(l => UptownLots.Open(l) == UptownLots.Ready) && !UptownLots.Open(null), "uptown lots: every lot of the band open while its ground stands, and none while it does not");
        Check(UptownLots.SizesOf(LotClass.Small).SequenceEqual(new[] { "ba:buildingsize_a", "ba:buildingsize_b", "ba:buildingsize_c", "ba:buildingsize_f" })
              && UptownLots.SizesOf(LotClass.Office).SequenceEqual(new[] { "ba:buildingsize_d", "ba:buildingsize_j", "ba:buildingsize_k" })
              && UptownLots.SizesOf(LotClass.Big).SequenceEqual(new[] { "ba:buildingsize_m", "ba:buildingsize_l", "ba:buildingsize_n" })
              && UptownLots.SizesOf(LotClass.Warehouse).SequenceEqual(new[] { "ba:buildingsize_h", "ba:buildingsize_i", "ba:buildingsize_p", "ba:buildingsize_q" }),
            "uptown lots: each class holds the game's sizes places.md counts in it");
        if (print)
        {
            var rows = lots.Concat(LayoutRules.Places).Select(l => new { l.Id, l.Row, l.Column, l.First, l.Street, l.Number, l.X, l.Z, l.Yaw, l.Width, l.Depth, Class = l.Class.ToString(), Place = UptownLots.Get(l.Id) == null, l.Size, l.Version, l.Spaces, l.Strip, l.Index, l.Open });
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "uptown_lots.json"), JsonSerializer.Serialize(rows));
            Console.WriteLine("uptown lots written to " + Path.Combine(AppContext.BaseDirectory, "uptown_lots.json"));
        }
    }

    // ---------------------------------------------------------------- the spaces

    // Every strip of spaces taken end to end: its lots and places on consecutive spaces from its first, none twice; the
    // open runs of single spaces on their blocks' west streets, never a landmark's block, the frontage of the 30 open lots.
    private static void UptownSpaceChecks(List<Lot> lots, bool print)
    {
        var all = lots.Concat(LayoutRules.Places).ToList();
        bool whole = true;
        string broken = null;
        foreach (var strip in all.GroupBy(l => l.Strip))
        {
            Lot[] s = strip.OrderBy(l => l.Index).ToArray();
            bool ok = s[0].Index == 0 && s.All(l => l.Strip == (l.Block * 1000) + (int)l.Facing);
            for (int i = 1; i < s.Length; i++)
            {
                ok &= s[i - 1].Before(s[i]) && Near(Along(s[i - 1]) + s[i - 1].Width * 0.5f, Along(s[i]) - s[i].Width * 0.5f, 0.02f);
            }
            if (!ok && broken == null)
            {
                broken = strip.Key.ToString();
            }
            whole &= ok;
        }
        Check(whole, "uptown spaces: every strip's lots and places stand on its spaces end to end from its first, side by side, none twice (" + (broken ?? "all") + ")");
        var runs = lots.Where(l => l.Open).GroupBy(l => l.Block).ToList();
        bool runsOk = runs.Count == LayoutRules.OpenRuns && runs.All(r => r.Count() == LayoutRules.OpenRunSpaces);
        foreach (var r in runs)
        {
            Lot[] s = r.OrderBy(l => l.Index).ToArray();
            runsOk &= s.All(l => l.Facing == 270f && l.Spaces == 1 && l.Size == null && l.Class == LotClass.Small) && s[0].Index == 0;
            for (int i = 1; i < s.Length; i++)
            {
                runsOk &= s[i - 1].Before(s[i]);
            }
            runsOk &= !LayoutRules.KeptForLandmarks.Contains(r.Key) && LayoutRules.OpenRunBlock(s[0].Row, s[0].Column);
        }
        int openSpaces = lots.Count(l => l.Open);
        var rows = runs.Select(r => r.Key / 100).Distinct().Count();
        Check(runsOk && rows == LayoutRules.Rows && openSpaces >= 60 && openSpaces <= 70,
            "uptown spaces: " + runs.Count + " open runs of " + LayoutRules.OpenRunSpaces + " single spaces, each on its block's west street from its first space, in every row, never a landmark's block ("
            + openSpaces + " spaces: " + string.Join(", ", runs.Select(r => r.Key)) + ")");
        // A run's spaces join into sites of one to five: a site of two or three is two or three spaces side by side.
        Lot[] one = runs.Count > 0 ? runs[0].OrderBy(l => l.Index).ToArray() : new Lot[0];
        Check(one.Length >= 3 && one[0].Before(one[1]) && one[1].Before(one[2]) && !one[0].Before(one[2]) && !one[2].Before(one[0]),
            "uptown spaces: two spaces join only when one is straight after the other along one strip");
        if (print)
        {
            var bySize = lots.Where(l => !l.Open).GroupBy(l => SpacesRules.Of(l.Size, l.Version).Name).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count());
            Console.WriteLine("uptown spaces: " + lots.Sum(l => l.Spaces) + " spaces under lots, " + openSpaces + " open; buildings by size: " + string.Join(", ", bySize));
        }
    }

    // m along a lot's strip (x for an avenue's, z for a street's): its middle.
    private static float Along(Lot l)
    {
        return l.Facing == 0f || l.Facing == 180f ? l.X : l.Z;
    }

    // A lot's corners on the ground (its footprint is square to the grid).
    private static void Corners(Lot l, out float x0, out float z0, out float x1, out float z1)
    {
        bool along = l.Facing == 0f || l.Facing == 180f;
        float hx = (along ? l.Width : l.Depth) * 0.5f, hz = (along ? l.Depth : l.Width) * 0.5f;
        x0 = l.X - hx;
        x1 = l.X + hx;
        z0 = l.Z - hz;
        z1 = l.Z + hz;
    }

    // True when two lots share more than a centimetre of ground each way: side by side they share an edge alone.
    private static bool Overlap(Lot a, Lot b)
    {
        Corners(a, out float ax0, out float az0, out float ax1, out float az1);
        Corners(b, out float bx0, out float bz0, out float bx1, out float bz1);
        return Math.Min(ax1, bx1) - Math.Max(ax0, bx0) > 0.01f && Math.Min(az1, bz1) - Math.Max(az0, bz0) > 0.01f;
    }

    private static float Gap(Lot a, Lot b)
    {
        Corners(a, out float ax0, out float az0, out float ax1, out float az1);
        Corners(b, out float bx0, out float bz0, out float bx1, out float bz1);
        float dx = Math.Max(0f, Math.Max(bx0 - ax1, ax0 - bx1)), dz = Math.Max(0f, Math.Max(bz0 - az1, az0 - bz1));
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }

    private static bool OffStreets(Lot l)
    {
        Corners(l, out float x0, out float z0, out float x1, out float z1);
        for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
        {
            float c = LayoutRules.StreetX(k);
            if (x1 > c - LayoutRules.StreetHalf + 0.01f && x0 < c + LayoutRules.StreetHalf - 0.01f)
            {
                return false;
            }
        }
        for (int r = 0; r <= 3; r++)
        {
            float a = LayoutRules.AvenueZ(r);
            if (z1 > a - LayoutRules.AvenueHalf + 0.01f && z0 < a + LayoutRules.AvenueHalf - 0.01f)
            {
                return false;
            }
        }
        return true;
    }

    // ---------------------------------------------------------------- the band's own ground and the way out

    private static void UptownGroundChecks()
    {
        List<GroundRect> first = LayoutRules.FirstGround, band = LayoutRules.BandGround;
        Check(first.Count == 18 && band.Count == 47 && first.All(r => band.Contains(r)) && ReferenceEquals(UptownLots.Ground, band),
            "uptown ground: the first block's eight blocks, nine streets and its stretch of Uptown Avenue, then the band west, east and north of it, 1st Avenue's two continuations "
            + "and the ring's 24 built strips past its outer streets (" + band.Count + ")");
        // The ring round the band (the owner, 25 Oct 2026: "the edge lots of the district aren't filled"): every block of it built
        // along the side facing the band, its strip whole spaces end to end, as deep as a street's or an avenue's strip.
        int ringBlocks = 0;
        bool ringBuilt = true;
        for (int r = 1; r <= LayoutRules.Rows + 1; r++)
        {
            for (int c = 0; c <= LayoutRules.Columns + 1; c++)
            {
                if (!LayoutRules.IsRing(r, c))
                {
                    continue;
                }
                ringBlocks++;
                LayoutRules.RingStrip(r, c, out float rx0, out float rz0, out float rx1, out float rz1, out int facing);
                List<Lot> strip = LayoutRules.Lots.Where(l => l.Row == r && l.Column == c).ToList();
                float along = facing == 180 ? rx1 - rx0 : rz1 - rz0, covered = strip.Sum(l => (float)l.Width);
                ringBuilt &= strip.Count > 0 && strip.All(l => (int)l.Facing == facing && !l.Open && l.Size != null) && along - covered < 1f
                             && Near(LayoutRules.RingDepth(facing == 180), facing == 180 ? rz1 - rz0 : rx1 - rx0);
            }
        }
        Check(ringBlocks == 24 && ringBuilt, "uptown ring: the " + ringBlocks + " blocks past Willow and Lake Street and north of Crest Avenue built along the side facing the band, their strips' spaces end to end");
        Check(LayoutRules.FirstLots.All(l => LayoutRules.OnFirstGround(l.X, l.Z)), "uptown ground: every first lot's middle on the first block's ground");
        bool lotsOn = LayoutRules.Lots.All(l =>
        {
            Corners(l, out float x0, out float z0, out float x1, out float z1);
            return LayoutRules.OnGround(l.X, l.Z) && LayoutRules.OnGround(x0 + 0.5f, z0 + 0.5f) && LayoutRules.OnGround(x1 - 0.5f, z0 + 0.5f)
                   && LayoutRules.OnGround(x0 + 0.5f, z1 - 0.5f) && LayoutRules.OnGround(x1 - 0.5f, z1 - 0.5f);
        });
        Check(lotsOn, "uptown ground: every lot of the band on its ground, its middle and its corners");
        // The game's own buildings north of 1st Avenue and its doors there are never the district's ground (Build 3682).
        float[][] doors = { new[] { 373.1f, 478.9f }, new[] { 266.8f, 479.5f }, new[] { 92.0f, 479f }, new[] { 2f, 478f }, new[] { -94f, 478f }, new[] { -175f, 478f }, new[] { -272f, 478f } };
        Check(doors.All(d => !LayoutRules.OnGround(d[0], d[1])), "uptown ground: 1st Avenue's north side and its doors stay the game's");
        float west = LayoutPlans.RingWest, east = LayoutPlans.RingEast, north = LayoutPlans.RingNorth, south = LayoutPlans.South;
        Check(band.All(r => r.X1 >= west - 0.01f && r.X2 <= east + 0.01f && r.Z1 >= south - 0.01f && r.Z2 <= north + 0.01f && r.X2 > r.X1 && r.Z2 > r.Z1),
            "uptown ground: inside the band and its ring, the ring's backs west of Willow and east of Lake Street, 1st Avenue's south pavement to the ring's backs north of Crest Avenue");
        // 1st Avenue between its corners and Manhattan south of it are never the band's, nor north of it the backs of its
        // buildings up to the old wall line; past 9th and 1st Street the band begins at 1st Avenue's north pavement.
        bool own = true;
        float corner0 = LayoutRules.StreetX(0) - LayoutRules.CrossingHalfX, corner8 = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.CrossingHalfX;
        float edge0 = LayoutRules.StreetX(0) - LayoutRules.StreetHalf, edge8 = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf;
        for (float x = corner0 + 0.5f; x <= corner8 - 0.5f; x += 2.5f)
        {
            for (float z = south - 40f; z < LayoutRules.OldWallZ - 0.5f; z += 1f)
            {
                own &= (z >= LayoutPlans.RowOneFrom && (x < edge0 || x > edge8)) || !LayoutRules.OnGround(x, z);
            }
        }
        for (float x = west; x <= east; x += 5f)
        {
            own &= !LayoutRules.OnGround(x, south - 0.5f) && !LayoutRules.OnGround(x, north + 0.5f);
        }
        Check(own, "uptown ground: 1st Avenue between its corners, Manhattan south of it and the city past the ring north of Crest Avenue are never the band's");
        // Every street, avenue, crossing and block of the band on it, and 1st Avenue's continuations.
        bool whole = true;
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            float middle = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f;
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                whole &= LayoutRules.OnGround(LayoutRules.StreetX(k), middle) && LayoutRules.OnGround(LayoutRules.StreetX(k), LayoutRules.AvenueZ(row));
                whole &= k == LayoutStreets.EastMost || LayoutRules.OnGround(LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, LayoutRules.AvenueZ(row));
            }
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                whole &= LayoutRules.OnGround((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            }
        }
        for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
        {
            whole &= !LayoutPlans.OnFirstAvenue(k) || LayoutRules.OnGround(LayoutRules.StreetX(k), LayoutRules.AvenueZ(0));
        }
        Check(whole, "uptown ground: every street, avenue, crossing and block of the band on its ground, and 1st Avenue's continuations west and east");
        UptownExit exit = UptownLots.Exit;
        Check(!LayoutRules.OnGround(exit.X, exit.Z) && exit.Z > 472.1f && exit.Z < 474.5f && exit.X > 66.2f && exit.X < 116.2f && exit.Heading == 270f
              && exit.District == "ba:neighborhood_midtown", "uptown ground: the way out is a parking place on 1st Avenue's north side, heading west, in Midtown");
    }

    // ---------------------------------------------------------------- the streets as lanes, through 1st Avenue's junctions

    // A model of 1st Avenue's junction where street k meets it, from the game's baked lanes (Build 3682): its stop points
    // (the avenue's four in way 0, the street's northbound in way 1) and its exits, the corners as the game has them.
    private static void GameJunction(PlanThrough through, int k, int firstIndex)
    {
        float c = LayoutRules.StreetX(k);
        through.Stops.Clear();
        through.Exits.Clear();
        through.Existing = 20 + k;
        int index = firstIndex;
        GamePoint P(float x, float z, float dx, float dz, int way, string lane) => new GamePoint
        {
            Index = index++, X = x, Y = 0f, Z = z, Dx = dx, Dz = dz, Way = way, RoadId = 40 + k, Lane = lane, Speed = 50, Cars = true, Trucks = true
        };
        if (k == 0)
        {
            // The north-west corner: 1st Avenue's westbound turns south into 9th Street, 9th Street's northbound east.
            through.Stops.Add(P(c + 14.9f, 466.7f, -1f, 0f, 0, "Road_59-Lane_1"));
            through.Stops.Add(P(c + 14.9f, 470.7f, -1f, 0f, 0, "Road_59-Lane_3"));
            through.Stops.Add(P(c + 3.2f, 444.9f, 0f, 1f, 1, "Road_66-Lane_0"));
            through.Exits.Add(P(c - 0.8f, 446.4f, 0f, -1f, -1, "Road_66-Lane_1"));
            through.Exits.Add(P(c + 13.5f, 462.7f, 1f, 0f, -1, "Road_59-Lane_0"));
            through.Exits.Add(P(c + 13.5f, 458.7f, 1f, 0f, -1, "Road_59-Lane_2"));
            return;
        }
        if (k == 8)
        {
            // The north-east corner: 1st Street's northbound turns west into 1st Avenue; the eastbound turns south before it.
            through.Stops.Add(P(c + 3.1f, 445.1f, 0f, 1f, 0, "Road_42-Lane_1"));
            through.Exits.Add(P(c - 0.7f, 446.5f, 0f, -1f, -1, "Road_42-Lane_0"));
            through.Exits.Add(P(c - 12.9f, 466.3f, -1f, 0f, -1, "Road_23-Lane_0"));
            through.Exits.Add(P(c - 12.9f, 470.3f, -1f, 0f, -1, "Road_23-Lane_1"));
            return;
        }
        through.Stops.Add(P(c - 14.85f, 462.6f, 1f, 0f, 0, "Road_35-Lane_0"));
        through.Stops.Add(P(c - 14.85f, 458.6f, 1f, 0f, 0, "Road_35-Lane_2"));
        through.Stops.Add(P(c + 14.85f, 470.6f, -1f, 0f, 0, "Road_29-Lane_3"));
        through.Stops.Add(P(c + 14.85f, 466.6f, -1f, 0f, 0, "Road_29-Lane_1"));
        through.Stops.Add(P(c + 3.15f, 444.9f, 0f, 1f, 1, "Road_69-Lane_0"));
        through.Exits.Add(P(c - 0.85f, 446.4f, 0f, -1f, -1, "Road_69-Lane_1"));
        through.Exits.Add(P(c + 13.45f, 462.6f, 1f, 0f, -1, "Road_29-Lane_0"));
        through.Exits.Add(P(c + 13.45f, 458.6f, 1f, 0f, -1, "Road_29-Lane_2"));
        through.Exits.Add(P(c - 13.35f, 470.6f, -1f, 0f, -1, "Road_35-Lane_3"));
        through.Exits.Add(P(c - 13.35f, 466.6f, -1f, 0f, -1, "Road_35-Lane_1"));
    }

    private const int GamePoints = 18791;   // the game's lane points in Build 3682; the models' points stand for some of them

    private static RoadBuild UptownBuild(out RoadPlan plan)
    {
        plan = LayoutPlans.Streets();
        var points = new Dictionary<int, int>();
        int index = 100;
        foreach (PlanThrough through in plan.Throughs)
        {
            int k = (int)Math.Round((through.X - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
            // Two ends through one junction (9th Street's and 1st Avenue West's, at its corner) meet the same points of the game's.
            if (!points.TryGetValue(k, out int at))
            {
                at = index;
                points[k] = at;
                index += 20;
            }
            GameJunction(through, k, at);
        }
        return RoadRules.Build(plan, GameGrid, GamePoints, 300);
    }

    // The plan's road a turn from one of the game's stop points leads into: past its turn points to the lane they reach.
    private static int UptownRoadInto(RoadBuild build, int index)
    {
        LanePoint p = build.At(index);
        for (int guard = 0; p != null && p.Through && p.Next.Count > 0 && guard < 64; guard++)
        {
            p = build.At(p.Next[0]);
        }
        return p == null || p.Through ? -1 : p.RoadId - build.FirstRoadId;
    }

    // Which of the game's lanes at its junction turn into a new road, as the game lays its own turns (right kerb to kerb,
    // left from the lane nearest the middle into the lane nearest the middle, straight on lane by lane).
    private static bool UptownTurnsIn(int k, bool avenue, string lane)
    {
        if (avenue)
        {
            return true;   // 1st Avenue West out of 9th Street's corner: straight on from both westbound lanes, left from 9th Street's northbound
        }
        if (k == 0)
        {
            return lane != "Road_59-Lane_1";
        }
        return k == 8 || (lane != "Road_35-Lane_2" && lane != "Road_29-Lane_1");
    }

    // ... and which of its exits a new road coming in turns into.
    private static bool UptownTurnsOut(int k, bool avenue, string lane)
    {
        if (avenue)
        {
            return true;   // into 1st Avenue's eastbound lane by lane, and right into 9th Street from the kerb lane
        }
        if (k == 0)
        {
            return lane != "Road_59-Lane_2";
        }
        if (k == 8)
        {
            return lane != "Road_23-Lane_0";
        }
        return lane != "Road_29-Lane_2" && lane != "Road_35-Lane_1";
    }

    private static void UptownRoadChecks(bool print)
    {
        RoadBuild build = UptownBuild(out RoadPlan plan);
        Check(build.Problem == null, "uptown streets: the band's plan builds (" + build.Problem + ")");
        string check = RoadRules.Check(build, GameGrid, GamePoints, 299);
        Check(check == null, "uptown streets: the build is sound (" + check + ")");
        if (build.Problem != null)
        {
            return;
        }
        if (print)
        {
            Console.WriteLine("uptown streets: " + RoadRules.Words(build));
            UptownDump(build);
        }
        int streets = LayoutPlans.StreetCount * LayoutRules.Rows - 1, avenues = LayoutRules.Rows * (LayoutPlans.StreetCount - 1) + 9;
        Check(plan.Roads.Count == streets + avenues && plan.Roads.Count == 119 && plan.Junctions.Count == LayoutPlans.JunctionCount && LayoutPlans.JunctionCount == 67
              && plan.Throughs.Count == 9,
            "uptown streets: 56 lengths of street and 63 of avenue, 1st Avenue's continuations among them, 67 junctions and nine ends through the game's ("
            + plan.Roads.Count + ", " + plan.Junctions.Count + ", " + plan.Throughs.Count + ")");
        // A junction with lights at every crossing of the band and on 1st Avenue's continuations, each once.
        var seen = new HashSet<int>();
        bool placed = true;
        for (int row = 0; row <= LayoutRules.Rows; row++)
        {
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                int j = LayoutPlans.JunctionAt(k, row);
                if (j < 0)
                {
                    placed &= row == 0 && !LayoutPlans.OnFirstAvenue(k);
                    continue;
                }
                placed &= seen.Add(j) && j < plan.Junctions.Count && Near(plan.Junctions[j].X, LayoutRules.StreetX(k)) && Near(plan.Junctions[j].Z, LayoutRules.AvenueZ(row))
                          && plan.Junctions[j].Lights;
            }
        }
        Check(placed && seen.Count == plan.Junctions.Count && LayoutPlans.JunctionAt(LayoutStreets.WestMost - 1, 1) == -1 && LayoutPlans.JunctionAt(0, 4) == -1
              && LayoutPlans.JunctionAt(4, 0) == -1, "uptown streets: a junction with lights at every crossing of the band and of 1st Avenue's continuations, each once, none of 1st Avenue's own");
        bool lanes = true;
        for (int k = LayoutStreets.WestMost - 1; k <= LayoutStreets.EastMost + 1; k++)
        {
            for (int row = 0; row <= LayoutRules.Rows + 1; row++)
            {
                bool want = k >= LayoutStreets.WestMost && k <= LayoutStreets.EastMost && row >= 1 && row <= LayoutRules.Rows && !(k == 4 && row == 1);
                lanes &= LayoutPlans.HasLanes(k, row) == want;
            }
        }
        Check(lanes, "uptown streets: every street has lanes in every row but 5th Street's first, where 1st Avenue has no junction");
        // The avenues' arms: an avenue's length east of a junction and one west of it, as the plan's roads run.
        var eastArm = new HashSet<int>();
        var westArm = new HashSet<int>();
        foreach (PlanRoad r in plan.Roads.Where(r => r.Ahead == 2))
        {
            if (r.Start >= 0)
            {
                eastArm.Add(r.Start);
            }
            if (r.End >= 0)
            {
                westArm.Add(r.End);
            }
        }
        bool arms = true;
        for (int row = 0; row <= LayoutRules.Rows; row++)
        {
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                int j = LayoutPlans.JunctionAt(k, row);
                arms &= j < 0 || (LayoutPlans.AvenueArm(k, row, true) == eastArm.Contains(j) && LayoutPlans.AvenueArm(k, row, false) == westArm.Contains(j));
            }
        }
        Check(arms && !LayoutPlans.AvenueArm(LayoutRules.FirstColumns + 1, 0, false) && LayoutPlans.AvenueArm(-1, 0, true),
            "uptown streets: the avenues' arms where their roads run: none from Hill Street west to 1st Street, 1st Avenue West on into 9th Street's corner");
        // Through 1st Avenue's junctions: each street's lane north an exit of it, its lane south a stop point; 1st Avenue
        // West's two lanes out of 9th Street's corner exits of it, its two lanes into it stop points.
        Check(build.Through.Count == 20 && build.Through.Count(t => t.Way >= 0) == 10, "uptown streets: ten exits and ten stop points given to the game's junctions ("
            + build.Through.Count + ")");
        foreach (ThroughEdit t in build.Through)
        {
            LanePoint p = build.At(t.Point);
            bool atStreet = p != null && Near(p.Z, LayoutPlans.ThroughZ, 0.1f);
            bool atCorner = p != null && Near(p.X, LayoutRules.StreetX(0) - LayoutPlans.AvenueStop, 0.1f) && Math.Abs(p.Z - LayoutRules.AvenueZ(0)) < LayoutRules.AvenueKerb;
            Check(p != null && p.Name.EndsWith(t.Way >= 0 ? "-Out" : "-In") && (atStreet || atCorner),
                "uptown streets: " + (t.Way >= 0 ? "a stop point" : "an exit") + " given to junction " + t.Junction + " is its road's own end there");
        }
        // Its way is the one whose lanes it runs against: a street's southbound with the street's northbound, 1st Avenue
        // West's eastbound with 1st Avenue's westbound.
        foreach (PlanThrough through in plan.Throughs)
        {
            int k = through.Existing - 20;
            bool avenue = !through.AtStart;
            int[] ways = build.Through.Where(t => t.Way >= 0 && build.At(t.Point).RoadId - build.FirstRoadId == through.Road).Select(t => t.Way).Distinct().ToArray();
            int want = avenue || k == 8 ? 0 : 1;
            Check(ways.Length == 1 && ways[0] == want && build.Through.Where(t => build.At(t.Point).RoadId - build.FirstRoadId == through.Road).All(t => t.Junction == through.Existing),
                "uptown streets: road " + through.Road + " stops on the green of the lanes it runs against at junction " + through.Existing + " (way " + string.Join(", ", ways) + ")");
        }
        // The turns through each junction: from the game's stop points, named after the game's lanes and keeping their road ids.
        List<LanePoint> turns = build.Points.Where(p => p.Keeps >= 0).ToList();
        Check(turns.Count > 0 && turns.All(p => p.Through && p.RoadId == p.Keeps && Regex.IsMatch(p.Name, @"^Road_\d+-Lane_\d+-(CConnect|Connector)\d+$")),
            "uptown streets: turns from the game's stop points keep its lane's name and road id, as its own turns do");
        Check(build.Points.Select(p => p.Name).Distinct().Count() == build.Points.Count, "uptown streets: every new lane point named once, the turns from one lane of the game's into two roads too");
        var into = new Dictionary<int, HashSet<int>>();
        var from = new Dictionary<int, HashSet<int>>();
        foreach (LinkEdit e in build.Edits)
        {
            Dictionary<int, HashSet<int>> map = e.AsNext ? into : from;
            if (!map.TryGetValue(e.Existing, out HashSet<int> roads))
            {
                map[e.Existing] = roads = new HashSet<int>();
            }
            roads.Add(e.AsNext ? UptownRoadInto(build, e.Added) : build.At(e.Added).RoadId - build.FirstRoadId);
        }
        foreach (PlanThrough through in plan.Throughs)
        {
            int k = through.Existing - 20;
            bool avenue = !through.AtStart;
            foreach (GamePoint stop in through.Stops)
            {
                bool turnsIn = into.TryGetValue(stop.Index, out HashSet<int> roads) && roads.Contains(through.Road);
                bool expected = UptownTurnsIn(k, avenue, stop.Lane);
                Check(turnsIn == expected, "uptown streets: at junction " + through.Existing + " the game's " + stop.Lane + (expected ? " turns into" : " leaves alone") + " road " + through.Road);
            }
            foreach (GamePoint exit in through.Exits)
            {
                bool reached = from.TryGetValue(exit.Index, out HashSet<int> roads) && roads.Contains(through.Road);
                bool expected = UptownTurnsOut(k, avenue, exit.Lane);
                Check(reached == expected, "uptown streets: at junction " + through.Existing + " road " + through.Road + (expected ? " turns into" : " leaves alone") + " the game's " + exit.Lane);
            }
        }
        // The band's own junctions: the avenue's two ways on one green, the street's two arms on the other.
        Check(build.Junctions.Count == 67 && build.Junctions.All(j => j.Lights && j.Ways.Count == 2),
            "uptown streets: each of the band's junctions has lights and two greens, the avenue's and the street's");
        Check(build.Points.All(p => GameGrid.Inside(p.X, p.Z, RoadRules.EdgeMargin)), "uptown streets: never a lane point off the road grid");
        // The new lanes stay on the band's ground, or on 9th Street's corner, which 1st Avenue West's lanes cross into.
        bool onBand = build.Points.Where(p => !p.Through).All(p => LayoutRules.OnGround(p.X, p.Z)
            || (Math.Abs(p.X - LayoutRules.StreetX(0)) <= LayoutRules.CrossingHalfX && Math.Abs(p.Z - LayoutRules.AvenueZ(0)) <= LayoutRules.AvenueKerb));
        Check(onBand, "uptown streets: the new lanes stay on the band's ground, and 1st Avenue West's on to 9th Street's corner");
        float fifth = LayoutRules.StreetX(4), hill = LayoutRules.StreetX(LayoutRules.FirstColumns + 1), first = LayoutRules.StreetX(LayoutRules.FirstColumns);
        bool gaps = build.Points.All(p => !(Math.Abs(p.X - fifth) < LayoutRules.StreetHalf && p.Z > LayoutPlans.RowOneFrom && p.Z < LayoutRules.AvenueZ(1) - LayoutRules.CrossingHalfZ)
                                          && !(p.X > first + LayoutRules.CrossingHalfX && p.X < hill - LayoutRules.CrossingHalfX && Math.Abs(p.Z - LayoutRules.AvenueZ(0)) < LayoutRules.AvenueHalf));
        Check(gaps, "uptown streets: no lane on 5th Street's first row, nor on 1st Avenue from 1st Street's corner to Hill Street");
        // The game's lanes along 1st Avenue (its four, between its corners) never clash with the new streets but at the junctions.
        var gx = new List<float>();
        var gz = new List<float>();
        for (float x = -286f; x <= 382f; x += 4.8f)
        {
            foreach (float z in new[] { 458.6f, 462.6f, 466.6f, 470.6f })
            {
                gx.Add(x);
                gz.Add(z);
            }
        }
        Check(RoadRules.Clear(build, Enumerable.Range(0, gx.Count).ToArray(), gx.ToArray(), gz.ToArray()) == null,
            "uptown streets: 1st Avenue's own lanes are beside the new streets only where they go through its junctions");
        string clash = RoadRules.Clear(build, new[] { 9999 }, new[] { LayoutRules.StreetX(2) + 3.1f }, new[] { 540f });
        Check(clash != null && clash.Contains("9999"), "uptown streets: a road of the game's where a new street goes is found (a free update built there): " + clash);
        string far = RoadRules.Clear(build, new[] { 9998 }, new[] { LayoutRules.StreetX(11) + 43.65f }, new[] { LayoutRules.AvenueZ(3) + 2f });
        Check(far != null && far.Contains("9998"), "uptown streets: one where Crest Avenue goes is found too: " + far);
        // The same plan twice gives the same build.
        RoadBuild again = UptownBuild(out _);
        Check(again.Count == build.Count && again.Points.Select((p, i) => p.Name == build.Points[i].Name && p.X == build.Points[i].X && p.Z == build.Points[i].Z
                  && p.Next.SequenceEqual(build.Points[i].Next)).All(s => s) && again.Edits.Count == build.Edits.Count && again.Through.Count == build.Through.Count,
            "uptown streets: the same build every time, on every machine");
        // A junction of the game's not found at this load: the streets stand down, nothing built.
        RoadPlan missing = LayoutPlans.Streets();
        foreach (PlanThrough through in missing.Throughs)
        {
            through.Existing = -1;
        }
        Check((RoadRules.Build(missing, GameGrid, GamePoints, 300).Problem ?? string.Empty).Contains("found no junction"), "uptown streets: no junction of the game's found, the streets stand down");
        RoadPlan corner = LayoutPlans.Streets();
        int index = 100;
        foreach (PlanThrough through in corner.Throughs)
        {
            GameJunction(through, (int)Math.Round((through.X - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch), index);
            index += 20;
            if (!through.AtStart)
            {
                through.Existing = -1;   // 9th Street's corner found for the street but not for the avenue
            }
        }
        Check((RoadRules.Build(corner, GameGrid, GamePoints, 300).Problem ?? string.Empty).Contains("found no junction"),
            "uptown streets: 9th Street's corner not found for 1st Avenue West, the streets stand down");
    }

    // The band as NavCheck lays it, for the offline scans against the game's scene (not-in-repo navlab facts, band_clear.py).
    private static void UptownDump(RoadBuild build)
    {
        var modules = new List<ModulePlace>();
        LayoutPlans.Modules(modules);
        var walls = new List<PlanBox>();
        LayoutPlans.NewWalls(walls);
        var hides = new List<GroundRect>();
        LayoutPlans.Hides(hides);
        var zones = new List<PlanBox>();
        LayoutPlans.Zones(zones);
        var grounds = new List<PlanBox>();
        var roads = new List<PlanBox>();
        LayoutPlans.Colliders(grounds, roads);
        var wx = new List<float>();
        var wz = new List<float>();
        LayoutPlans.OldWalls(wx, wz);
        var termini = new List<PlanTerminus>();
        LayoutPlans.Termini(termini);
        float[] Rect(PlanBox w) => new[] { w.X - w.HalfX, w.Z - w.HalfZ, w.X + w.HalfX, w.Z + w.HalfZ };
        object Boxes(GroundPlan p) => p.Boxes.Select(b => new[] { b.X - b.HalfX, b.Z - b.HalfZ, b.X + b.HalfX, b.Z + b.HalfZ }).ToArray();
        var dump = new Dictionary<string, object>
        {
            { "Points", build.Points.Select(p => new[] { p.X, p.Z, p.Through ? 1f : 0f }).ToArray() },
            { "Player", Boxes(LayoutPlans.PlayerGround()) },
            { "Npc", Boxes(LayoutPlans.NpcGround()) },
            { "Walls", walls.Select(Rect).ToArray() },
            { "Hides", hides.Select(h => new[] { h.X1, h.Z1, h.X2, h.Z2 }).ToArray() },
            { "Zones", zones.Select(Rect).ToArray() },
            { "Grounds", grounds.Select(Rect).ToArray() },
            { "Roads", roads.Select(Rect).ToArray() },
            { "OldWalls", wx.Select((x, i) => new[] { x, wz[i] }).ToArray() },
            { "Modules", modules.Select(m => new[] { (float)m.Kind, m.X, m.Z, m.Scenery ? 1f : 0f }).ToArray() },
            { "Band", LayoutRules.BandGround.Select(h => new[] { h.X1, h.Z1, h.X2, h.Z2 }).ToArray() },
            { "Termini", termini.Select(t => new[] { t.X, t.Z }).ToArray() },
            {
                "Lots", LayoutRules.Lots.Select(l =>
                {
                    Corners(l, out float x0, out float z0, out float x1, out float z1);
                    return new object[] { x0, z0, x1, z1, l.Id };
                }).ToArray()
            }
        };
        string path = Path.Combine(AppContext.BaseDirectory, "uptown_plans.json");
        File.WriteAllText(path, JsonSerializer.Serialize(dump));
        Console.WriteLine("uptown plans written to " + path);
    }

    // ---------------------------------------------------------------- the walking ground

    private const float AgentRadius = 0.25f;   // m: both of the game's walking meshes' agents (Build 3682), the margin new ground keeps where it meets nothing

    private static bool UptownCovered(List<GroundBox> boxes, float x, float z)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i].Covers(x, z, 0.001f))
            {
                return true;
            }
        }
        return false;
    }

    // How many pieces a plan's ground is in: boxes that overlap or meet along a metre or more are one surface.
    private static int UptownPieces(List<GroundBox> boxes)
    {
        int n = boxes.Count;
        var group = Enumerable.Range(0, n).ToArray();
        int Find(int i)
        {
            while (group[i] != i)
            {
                group[i] = group[group[i]];
                i = group[i];
            }
            return i;
        }
        for (int a = 0; a < n; a++)
        {
            boxes[a].Extent(out float ax0, out float az0, out float ax1, out float az1);
            for (int b = a + 1; b < n; b++)
            {
                boxes[b].Extent(out float bx0, out float bz0, out float bx1, out float bz1);
                float wx = Math.Min(ax1, bx1) - Math.Max(ax0, bx0), wz = Math.Min(az1, bz1) - Math.Max(az0, bz0);
                if (wx >= -0.01f && wz >= -0.01f && Math.Max(wx, wz) >= 1f)
                {
                    group[Find(a)] = Find(b);
                }
            }
        }
        return Enumerable.Range(0, n).Select(Find).Distinct().Count();
    }

    private static void UptownWalkChecks()
    {
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        Check(player.Player && !player.Npc && !npc.Player && npc.Npc && player.ExpectColliders == 0 && npc.ExpectColliders == 0,
            "uptown walking: one plan for the player's mesh and one for the pedestrians', nothing of the game's on either");
        // The made buildings' ground check: the player's mesh under a lot's middle, its corners a metre in, and both at its door.
        int under = 0, doors = 0;
        foreach (Lot l in LayoutRules.Lots)
        {
            Corners(l, out float x0, out float z0, out float x1, out float z1);
            bool on = UptownCovered(player.Boxes, l.X, l.Z) && UptownCovered(player.Boxes, x0 + 1f, z0 + 1f) && UptownCovered(player.Boxes, x1 - 1f, z0 + 1f)
                      && UptownCovered(player.Boxes, x0 + 1f, z1 - 1f) && UptownCovered(player.Boxes, x1 - 1f, z1 - 1f);
            float dx = l.Facing == 90f ? 1f : l.Facing == 270f ? -1f : 0f, dz = l.Facing == 0f ? 1f : l.Facing == 180f ? -1f : 0f;
            float doorX = l.X + dx * (l.Depth * 0.5f + 1f), doorZ = l.Z + dz * (l.Depth * 0.5f + 1f);
            bool door = UptownCovered(player.Boxes, doorX, doorZ) && UptownCovered(npc.Boxes, doorX, doorZ);
            under += on ? 1 : 0;
            doors += door ? 1 : 0;
            if (!on || !door)
            {
                Check(false, "uptown walking: " + l.Id + " stands on the player's ground (" + on + "), and both meshes reach its door (" + door + ")");
            }
        }
        Check(under == LayoutRules.Lots.Count && doors == under, "uptown walking: all " + LayoutRules.Lots.Count + " lots stand on the player's ground, and both meshes reach every door ("
            + under + ", " + doors + ")");
        Check(UptownPieces(player.Boxes) == 1 && UptownPieces(npc.Boxes) == 1, "uptown walking: each plan's ground is one surface, no box apart from the rest ("
            + UptownPieces(player.Boxes) + ", " + UptownPieces(npc.Boxes) + ")");
        // The pedestrians keep to the pavements and the crosswalks: off every carriageway, block and avenue's middle.
        bool kept = true;
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            float middle = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f, a = LayoutRules.AvenueZ(row);
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                kept &= !UptownCovered(npc.Boxes, LayoutRules.StreetX(k), middle) && !UptownCovered(npc.Boxes, LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, a);
                kept &= k == LayoutStreets.WestMost || UptownCovered(npc.Boxes, LayoutRules.StreetX(k) - 7.8f, a);
                kept &= k == LayoutStreets.EastMost || UptownCovered(npc.Boxes, LayoutRules.StreetX(k) + 7.8f, a);
            }
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                kept &= !UptownCovered(npc.Boxes, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            }
        }
        Check(kept, "uptown walking: the pedestrians keep to the pavements, crossing each avenue at every junction on both sides, off the carriageways and the blocks");
        // Inside its edge: every box off the walls by the edge's clearance, north of 1st Avenue's kerbs.
        float west = LayoutPlans.RingWest + LayoutPlans.EdgeClear - 0.01f, east = LayoutPlans.RingEast - LayoutPlans.EdgeClear + 0.01f, north = LayoutPlans.RingNorth - LayoutPlans.EdgeClear + 0.01f;
        bool inside = player.Boxes.Concat(npc.Boxes).All(b =>
        {
            b.Extent(out float x0, out float z0, out float x1, out float z1);
            return x0 >= west && x1 <= east && z1 <= north && z0 >= LayoutRules.AvenueZ(0) - LayoutRules.AvenueKerb - 0.01f;
        });
        Check(inside, "uptown walking: every box inside the band and its ring, " + LayoutPlans.EdgeClear + " m off its walls, none on 1st Avenue's south pavement");
        Check(player.Boxes.Concat(npc.Boxes).All(b => Near(b.Top, LayoutPlans.GroundTop) && b.Yaw == 0f && b.HalfX >= 0.5f - 0.01f && b.HalfZ >= 0.5f - 0.01f),
            "uptown walking: every box flat at the ground's height, square to the grid, none narrower than a metre");
        // What stands on the old corners' pavements: no box on it, and only slivers beside it let go.
        var kept0 = new List<FlatRect>();
        LayoutPlans.KeptOff(kept0);
        bool off = kept0.All(h => player.Boxes.Concat(npc.Boxes).All(b =>
        {
            b.Extent(out float x0, out float z0, out float x1, out float z1);
            FlatRect g = h.Grown(AgentRadius);
            return !(x1 > g.MinX && x0 < g.MaxX && z1 > g.MinZ && z0 < g.MaxZ);
        }));
        Check(kept0.Count == 6 && off, "uptown walking: no box on the five scaffolds and building fronts standing on the first block's pavements nor on the game's wall at 9th Street's "
                                       + "corner, nor within a walker's margin of them");
        int lost = 0;
        for (int k = 0; k <= LayoutRules.FirstColumns; k++)
        {
            float c = LayoutRules.StreetX(k), to = LayoutRules.AvenueZ(1) - LayoutRules.AvenueHalf;
            foreach (float[] side in new[] { new[] { c - LayoutRules.StreetHalf, c - LayoutRules.StreetKerbWest }, new[] { c + LayoutRules.StreetKerbEast, c + LayoutRules.StreetHalf } })
            {
                for (float x = side[0] + 0.05f; x < side[1]; x += 0.2f)
                {
                    for (float z = LayoutPlans.JoinFrom + 0.05f; z < to; z += 0.5f)
                    {
                        bool hole = kept0.Take(5).Any(h => h.Grown(LayoutPlans.KeepClear + LayoutPlans.Least).Holds(x, z));
                        lost += !hole && !UptownCovered(player.Boxes, x, z) ? 1 : 0;
                        lost += !hole && !UptownCovered(npc.Boxes, x, z) ? 1 : 0;
                    }
                }
            }
        }
        Check(lost == 0, "uptown walking: every bit of the first block's pavements laid for both meshes but what stands there and a metre beside it (" + lost + " points missed)");
        // Seeds: on 1st Avenue's own ground between its corners, never under the new ground, so the city's own is what they join.
        bool seeds = player.Seeds.Count == 2 * LayoutRules.FirstColumns + 1 && npc.Seeds.Count == LayoutRules.FirstColumns;
        float corner0 = LayoutRules.StreetX(0), corner8 = LayoutRules.StreetX(LayoutRules.FirstColumns);
        seeds &= player.Seeds.All(s => !UptownCovered(player.Boxes, s.X, s.Z) && s.X >= corner0 - 0.01f && s.X <= corner8 + 0.01f
                                       && Math.Abs(s.Z - LayoutRules.AvenueZ(0)) < LayoutRules.AvenueKerb);
        seeds &= npc.Seeds.All(s => !UptownCovered(npc.Boxes, s.X, s.Z) && s.X > corner0 && s.X < corner8 && s.Z > LayoutRules.AvenueZ(0) + LayoutRules.AvenueKerb
                                    && s.Z < LayoutPlans.RowOneFrom);
        Check(seeds, "uptown walking: the seeds on 1st Avenue's own carriageway and north pavement, never under new ground");
        // The ground rebuilt whole: the band's own, every box on it.
        bool area = player.Area.Count == LayoutRules.BandGround.Count && npc.Area.Count == player.Area.Count;
        for (int i = 0; area && i < player.Area.Count; i++)
        {
            GroundRect r = LayoutRules.BandGround[i];
            area &= Near(player.Area[i].MinX, r.X1) && Near(player.Area[i].MinZ, r.Z1) && Near(player.Area[i].MaxX, r.X2) && Near(player.Area[i].MaxZ, r.Z2);
        }
        // Off the band's ground the new ground lies only where it joins the city's own: the first block's streets from their
        // old dead ends to the old wall line, 1st Avenue's continuations over its old corners.
        float avenue0 = LayoutRules.AvenueZ(0), x0First = LayoutRules.StreetX(0) - LayoutRules.StreetHalf, x8First = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf;
        bool Joins(float x, float z) => (z >= LayoutPlans.ArmFrom - 0.01f && z <= LayoutRules.OldWallZ + 0.01f && x >= x0First - 0.01f && x <= x8First + 0.01f)
                                        || (z >= avenue0 - LayoutRules.AvenueKerb - 0.01f && z <= LayoutPlans.RowOneFrom + 0.01f
                                            && ((x >= LayoutRules.StreetX(0) - LayoutRules.CrossingHalfX - 0.01f && x <= LayoutPlans.WestCornerJoin + 0.01f)
                                                || (x >= LayoutPlans.EastCornerJoin - 0.01f && x <= LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.CrossingHalfX + 0.01f)));
        bool InArea(List<FlatRect> rects, float x, float z) => rects.Any(r => r.Holds(x, z));
        int offBand = 0;
        foreach (GroundPlan plan in new[] { player, npc })
        {
            foreach (GroundBox b in plan.Boxes)
            {
                b.Extent(out float x0, out float z0, out float x1, out float z1);
                for (float x = x0 + 0.01f; x < x1; x += Math.Max(0.5f, (x1 - x0 - 0.02f) / 8f))
                {
                    for (float z = z0 + 0.01f; z < z1; z += Math.Max(0.5f, (z1 - z0 - 0.02f) / 8f))
                    {
                        offBand += InArea(plan.Area, x, z) || Joins(x, z) ? 0 : 1;
                    }
                }
                offBand += InArea(plan.Area, x1 - 0.01f, z1 - 0.01f) || Joins(x1 - 0.01f, z1 - 0.01f) ? 0 : 1;
            }
        }
        Check(area && offBand == 0, "uptown walking: both meshes' tiles over the band's ground rebuilt whole, every box on it but where it joins the city's own ("
                                    + offBand + " points off it)");
    }

    // ---------------------------------------------------------------- the joins to the game's own walking ground

    // The game's own walking meshes where the first block meets them (Build 3682, read from its data by the navlab's
    // band_joins.py): for each street, the least the player's mesh reaches north over its carriageway, then the most it
    // reaches along a line over each pavement for the player's and the pedestrians' (west, then east); and how far west
    // and east they reach over 1st Avenue's carriageway and north pavement at its two ends.
    private static readonly float[,] GameReach =
    {
        { 479.30f, 480.75f, 480.80f, 480.60f, 480.40f },
        { 479.35f, 480.80f, 480.80f, 480.30f, 480.25f },
        { 479.60f, 480.90f, 480.90f, 480.80f, 480.80f },
        { 479.30f, 480.75f, 480.70f, 480.65f, 480.40f },
        { 479.45f, 480.90f, 480.90f, 480.85f, 480.85f },
        { 479.45f, 480.70f, 480.70f, 480.85f, 480.85f },
        { 478.90f, 480.90f, 480.90f, 480.90f, 480.90f },
        { 478.90f, 480.90f, 480.90f, 480.85f, 480.90f },
        { 478.75f, 480.30f, 480.30f, 479.30f, 479.30f },
    };
    private const float GameWestRoad = -311.80f, GameWestPavement = -312.80f, GameEastRoad = 406.10f, GameEastPavement = 405.85f;

    // The first z from z0 north that the boxes cover along a line, or NaN.
    private static float UptownFrom(List<GroundBox> boxes, float x, float z0, float z1)
    {
        for (float z = z0; z <= z1; z += 0.01f)
        {
            if (UptownCovered(boxes, x, z))
            {
                return z;
            }
        }
        return float.NaN;
    }

    // The last x along a line, from x0 toward x1, the boxes cover without a gap, or NaN.
    private static float UptownTo(List<GroundBox> boxes, float z, float x0, float x1)
    {
        float step = x1 > x0 ? 0.01f : -0.01f, last = float.NaN;
        for (float x = x0; (x - x1) * step <= 0f; x += step)
        {
            if (UptownCovered(boxes, x, z))
            {
                last = x;
            }
            else if (!float.IsNaN(last))
            {
                break;
            }
        }
        return last;
    }

    private static void UptownJoinChecks()
    {
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        var shrunkPlayer = new List<GroundBox>();
        var shrunkNpc = new List<GroundBox>();
        GroundShapes.ShrinkOutward(player.Boxes, AgentRadius, shrunkPlayer);
        GroundShapes.ShrinkOutward(npc.Boxes, AgentRadius, shrunkNpc);
        float need = GroundRules.JoinOverlap;
        // Each street end: the new ground, its margin kept, begins far enough south that the game's mesh runs on over it.
        for (int k = 0; k <= LayoutRules.FirstColumns; k++)
        {
            float c = LayoutRules.StreetX(k);
            float road = float.MinValue;
            for (int i = 0; i < 5; i++)
            {
                float x = c - LayoutRules.StreetKerbWest + 0.3f + (LayoutRules.StreetKerbWest + LayoutRules.StreetKerbEast - 0.6f) * i / 4f;
                road = Math.Max(road, UptownFrom(shrunkPlayer, x, 470f, 500f));
            }
            float westX = c - (LayoutRules.StreetHalf + LayoutRules.StreetKerbWest) * 0.5f, eastX = c + (LayoutRules.StreetHalf + LayoutRules.StreetKerbEast) * 0.5f;
            if (k == 1)
            {
                eastX = c + 6.5f;   // 8th Street's east pavement is laid only as far east as the building front standing on it
            }
            float pw = UptownFrom(shrunkPlayer, westX, 470f, 500f), nw = UptownFrom(shrunkNpc, westX, 470f, 500f);
            float pe = UptownFrom(shrunkPlayer, eastX, 470f, 500f), ne = UptownFrom(shrunkNpc, eastX, 470f, 500f);
            Check(road + need <= GameReach[k, 0] && pw + need <= GameReach[k, 1] && nw + need <= GameReach[k, 2] && pe + need <= GameReach[k, 3] && ne + need <= GameReach[k, 4],
                "uptown joins: street " + k + "'s new ground, its margin kept, begins " + need + " m or more short of where the game's meshes stop over its carriageway and pavements"
                + " (from z " + road.ToString("0.00") + ", " + pw.ToString("0.00") + ", " + nw.ToString("0.00") + ", " + pe.ToString("0.00") + ", " + ne.ToString("0.00") + ")");
        }
        // 1st Avenue's two ends: its continuations reach over its old corners past where the game's meshes stop.
        float westRoad = UptownTo(shrunkPlayer, LayoutRules.AvenueZ(0), -330f, -290f), westWalk = UptownTo(shrunkNpc, 477f, -330f, -290f);
        float eastRoad = UptownTo(shrunkPlayer, LayoutRules.AvenueZ(0), 420f, 380f), eastWalk = UptownTo(shrunkNpc, 477f, 420f, 380f);
        Check(westRoad - need >= GameWestRoad && westWalk - need >= GameWestPavement && eastRoad + need <= GameEastRoad && eastWalk + need <= GameEastPavement,
            "uptown joins: 1st Avenue's continuations reach " + need + " m or more over the game's meshes at its two corners (to x " + westRoad.ToString("0.00") + ", "
            + westWalk.ToString("0.00") + " and from x " + eastRoad.ToString("0.00") + ", " + eastWalk.ToString("0.00") + ")");
        // The sidewalks' second cause, held for the band's own layout: the margin is kept only where the ground meets nothing,
        // so a kerb between a carriageway and its pavement, or a block and a street, is one surface with no gap.
        bool kerbs = true;
        for (int k = 0; k <= LayoutRules.FirstColumns; k++)
        {
            float c = LayoutRules.StreetX(k);
            foreach (float z in new[] { 490f, 520f, 560f, 590f })
            {
                foreach (float x in new[] { c - LayoutRules.StreetKerbWest, c + LayoutRules.StreetKerbEast })
                {
                    kerbs &= UptownCovered(shrunkPlayer, x - 0.05f, z) && UptownCovered(shrunkPlayer, x + 0.05f, z);
                }
            }
        }
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            float middle = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f;
            for (int k = LayoutStreets.WestMost + 1; k <= LayoutStreets.EastMost; k++)
            {
                float c = LayoutRules.StreetX(k);
                kerbs &= UptownCovered(shrunkPlayer, c - LayoutRules.StreetHalf - 0.05f, middle + 30f) && UptownCovered(shrunkPlayer, c - LayoutRules.StreetHalf + 0.05f, middle + 30f);
            }
        }
        Check(kerbs, "uptown joins: with the agent's margin kept, the player's ground has no gap at a kerb nor where a block meets its street");
        // ... and is kept at the band's edge, where the walls stand.
        float z1 = LayoutRules.AvenueZ(2), mid = (LayoutRules.AvenueZ(1) + LayoutRules.AvenueZ(2)) * 0.5f;   // the ring's west and east strips lie between the avenues
        float edge = LayoutPlans.RingWest + LayoutPlans.EdgeClear + AgentRadius;
        Check(!UptownCovered(shrunkPlayer, edge - 0.05f, mid) && UptownCovered(shrunkPlayer, edge + 0.05f, mid)
              && !UptownCovered(shrunkPlayer, LayoutPlans.RingEast - LayoutPlans.EdgeClear - AgentRadius + 0.05f, mid)
              && !UptownCovered(shrunkPlayer, LayoutRules.StreetX(5) + LayoutRules.StreetPitch * 0.5f, LayoutPlans.RingNorth - LayoutPlans.EdgeClear - AgentRadius + 0.05f)
              && !UptownCovered(shrunkNpc, LayoutRules.StreetX(5) + LayoutRules.StreetKerbEast + AgentRadius - 0.05f, z1 - 50f)
              && UptownCovered(shrunkNpc, LayoutRules.StreetX(5) + LayoutRules.StreetKerbEast + AgentRadius + 0.05f, z1 - 50f),
            "uptown joins: the agent's margin kept at the band's edge, and at the pedestrians' kerbs as the game keeps it");
        // Inside the band beyond the first block, away from its edge: one surface for the player, nowhere a hole but where
        // the game's wall at 9th Street's corner stands.
        var kept = new List<FlatRect>();
        LayoutPlans.KeptOff(kept);
        FlatRect wall = kept[kept.Count - 1].Grown(LayoutPlans.KeepClear + AgentRadius + 0.05f);
        int holes = 0;
        float x0 = LayoutRules.StreetX(0) - LayoutRules.StreetHalf, x8 = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf;
        float lift = LayoutPlans.EdgeClear + AgentRadius + 0.1f;
        for (float x = LayoutPlans.West + lift; x <= LayoutPlans.East - lift; x += 1.7f)
        {
            bool firstBlock = x > x0 && x < x8;
            float from = firstBlock ? LayoutRules.AvenueZ(1) - LayoutRules.AvenueHalf + 0.05f
                : x < LayoutPlans.WestCornerJoin - AgentRadius - 0.1f || x > LayoutPlans.EastCornerJoin + AgentRadius + 0.1f
                    ? LayoutRules.AvenueZ(0) - LayoutRules.AvenueKerb + AgentRadius + 0.1f : LayoutPlans.RowOneFrom + 0.05f;
            for (float z = from; z <= LayoutPlans.North - lift; z += 1.3f)
            {
                holes += UptownCovered(shrunkPlayer, x, z) || wall.Holds(x, z) ? 0 : 1;
            }
        }
        Check(holes == 0, "uptown joins: the player's ground over the band beyond the first block is one surface once its margin is kept (" + holes + " points missed)");
    }

    // ---------------------------------------------------------------- the art, the walls, the colliders, the hiding, the people and the buses

    private static void UptownPlaceChecks()
    {
        var modules = new List<ModulePlace>();
        LayoutPlans.Modules(modules);
        int Count(ModuleKind kind, bool scenery) => modules.Count(m => m.Kind == kind && m.Scenery == scenery);
        Check(Count(ModuleKind.Street, false) == 57 && Count(ModuleKind.Street, true) == 19 && Count(ModuleKind.Avenue, false) == 64 && Count(ModuleKind.Avenue, true) == 8
              && Count(ModuleKind.Crossing, false) == 67 && Count(ModuleKind.Crossing, true) == 0 && Count(ModuleKind.Ground, false) == 46 && Count(ModuleKind.Ground, true) == 28,
            "uptown art: 57 street lengths, 64 avenue lengths, 67 crossings and 46 blocks' grounds of the game's own pieces, and past the walls 19 streets, 8 avenues and 28 grounds as scenery");
        Check(modules.Where(m => m.Kind == ModuleKind.Street).All(m => Near(m.Z - LayoutRules.StreetModule * 0.5f, LayoutRules.AvenueZ(m.Row - 1) + LayoutRules.CrossingHalfZ, 0.01f)
                                                                       && Near(m.X, LayoutRules.StreetX(m.Street)) && m.Scenery == (m.Row > LayoutRules.Rows)),
            "uptown art: each street length fills its street from one avenue's crossing to the next, the row past Crest Avenue scenery");
        Check(modules.Where(m => m.Kind == ModuleKind.Avenue).All(m => Near(m.Z, LayoutRules.AvenueZ(m.Row)) && Near(m.X, LayoutRules.StreetX(m.Street) + LayoutRules.StreetPitch * 0.5f)
                                                                       && m.Scenery == (m.Street < LayoutStreets.WestMost || m.Street == LayoutStreets.EastMost)
                                                                       && !(m.Row == 0 && m.Street >= 0 && m.Street < LayoutRules.FirstColumns)),
            "uptown art: each avenue length between two crossings, those past Willow and Lake Street scenery, none over 1st Avenue's own");
        var crossings = modules.Where(m => m.Kind == ModuleKind.Crossing).ToList();
        Check(crossings.All(m => m.Junction == LayoutPlans.JunctionAt(m.Street, m.Row) && m.Junction >= 0 && Near(m.Z, LayoutRules.AvenueZ(m.Row)))
              && crossings.Select(m => m.Junction).Distinct().Count() == LayoutPlans.JunctionCount,
            "uptown art: every crossing one of the plan's junctions, each junction one crossing, its lights theirs");
        var grounds = modules.Where(m => m.Kind == ModuleKind.Ground).ToList();
        Check(grounds.All(m => !(m.Row == 1 && m.Street >= 0 && m.Street < LayoutRules.FirstColumns)) && grounds.Select(m => m.X + "," + m.Z).Distinct().Count() == grounds.Count
              && grounds.Where(m => !m.Scenery).All(m => LayoutRules.OnGround(m.X, m.Z)),
            "uptown art: a block's ground on every block of the band but the first block's, which keeps the backdrop's, and round it past the walls");
        var walls = new List<PlanBox>();
        LayoutPlans.NewWalls(walls);
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        Check(walls.Count == 9 && walls.All(w => Near(w.Top, LayoutPlans.WallHigh)),
            "uptown edge: nine new walls of the game's height: north, west and east behind the ring, across 1st Avenue's two continuations' ends and their corners with the ring, and two south of them");
        Check(walls.All(w => player.Boxes.Concat(npc.Boxes).All(b =>
        {
            b.Extent(out float x0, out float z0, out float x1, out float z1);
            return !(x1 > w.X - w.HalfX + 0.01f && x0 < w.X + w.HalfX - 0.01f && z1 > w.Z - w.HalfZ + 0.01f && z0 < w.Z + w.HalfZ - 0.01f);
        })), "uptown edge: the walls stand off the walking ground, round its edge");
        // Closed all round: a point just outside the band on each side is inside a wall.
        bool closed = true;
        for (float z = LayoutPlans.SouthWall + 1f; z < LayoutPlans.RowOneFrom; z += 3f)
        {
            closed &= walls.Any(w => w.Holds(LayoutPlans.West - 1f, z, 0f)) && walls.Any(w => w.Holds(LayoutPlans.East + 1f, z, 0f));
        }
        for (float z = LayoutPlans.RowOneFrom + 0.5f; z < LayoutPlans.RingNorth; z += 10f)
        {
            closed &= walls.Any(w => w.Holds(LayoutPlans.RingWest - 1f, z, 0f)) && walls.Any(w => w.Holds(LayoutPlans.RingEast + 1f, z, 0f));
        }
        // The corners where the ring's first strips meet 1st Avenue's continuations, west and east alike.
        for (float x = LayoutPlans.RingWest; x <= LayoutPlans.West - LayoutPlans.WallThick; x += 5f)
        {
            closed &= walls.Any(w => w.Holds(x, LayoutPlans.RowOneFrom - 1f, 0f)) && walls.Any(w => w.Holds(-x + LayoutPlans.West + LayoutPlans.East, LayoutPlans.RowOneFrom - 1f, 0f));
        }
        for (float x = LayoutPlans.RingWest; x <= LayoutPlans.RingEast; x += 10f)
        {
            closed &= walls.Any(w => w.Holds(x, LayoutPlans.RingNorth + 1f, 0f));
        }
        for (float x = LayoutPlans.West + 1f; x <= LayoutPlans.WestKeptWall; x += 10f)
        {
            closed &= walls.Any(w => w.Holds(x, LayoutPlans.SouthWall - 1f, 0f));
        }
        for (float x = LayoutPlans.EastBuilding + 1f; x <= LayoutPlans.East - 1f; x += 10f)
        {
            closed &= walls.Any(w => w.Holds(x, LayoutPlans.SouthWall - 1f, 0f));
        }
        Check(closed, "uptown edge: the band walled all round, the south of 1st Avenue's continuations from its west end to the game's kept corner wall and from the building at 2 1st Street east");
        var oldX = new List<float>();
        var oldZ = new List<float>();
        LayoutPlans.OldWalls(oldX, oldZ);
        // The game's walls (MapBorder's City Bounds Colliders, Build 3682): across the nine street ends, then across 1st
        // Avenue at its two corners, and the one across 9th Street's south-west corner, which stays.
        float[][] gameWalls =
        {
            new[] { -302.11f, 482.89f }, new[] { -215.01f, 482.41f }, new[] { -126.09f, 483.23f }, new[] { -39.18f, 483.35f }, new[] { 47.80f, 483.54f },
            new[] { 134.70f, 483.50f }, new[] { 222.20f, 483.26f }, new[] { 309.46f, 483.28f }, new[] { 396.13f, 482.26f },
            new[] { -315.74f, 468.03f }, new[] { 409.08f, 464.74f }
        };
        float[] kept = { -317.16f, 451.79f };
        float Distance(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
        bool opens = oldX.Count == 11 && oldZ.Count == 11;
        for (int i = 0; opens && i < oldX.Count; i++)
        {
            int nearest = Enumerable.Range(0, gameWalls.Length).OrderBy(w => Distance(oldX[i], oldZ[i], gameWalls[w][0], gameWalls[w][1])).First();
            opens &= nearest == i && Distance(oldX[i], oldZ[i], gameWalls[i][0], gameWalls[i][1]) <= LayoutPlans.OldWallReach
                     && Distance(oldX[i], oldZ[i], kept[0], kept[1]) > LayoutPlans.OldWallReach;
        }
        Check(opens, "uptown edge: the eleven walls opened are the game's across the street ends and 1st Avenue's two ends, the one across 9th Street's south-west corner out of reach");
        var groundsBoxes = new List<PlanBox>();
        var roads = new List<PlanBox>();
        LayoutPlans.Colliders(groundsBoxes, roads);
        // The city's own ground collider (GroundPlanes/GroundPlane, Build 3682) ends at z 606.05 between x -398.57 and 481.72.
        bool CityGround(float x, float z) => x >= -398.57f && x <= 481.72f && z <= 606.05f;
        bool grounded = player.Boxes.All(b =>
        {
            b.Extent(out float x0, out float z0, out float x1, out float z1);
            return new[] { b.X, x0 + 0.01f, x1 - 0.01f }.All(x => new[] { b.Z, z0 + 0.01f, z1 - 0.01f }.All(z => groundsBoxes.Any(g => g.Holds(x, z, 0f)) || CityGround(x, z)));
        });
        Check(groundsBoxes.Count == 3 && LayoutRules.Lots.All(l => groundsBoxes.Any(g => g.Holds(l.X, l.Z, 0f))) && groundsBoxes.All(g => Near(g.Top, UptownLots.GroundY)) && grounded,
            "uptown ground: three ground colliders at the city's height, under every lot, and the walking ground on them or on the city's own");
        bool carriageways = roads.Count == LayoutPlans.StreetCount * LayoutRules.Rows + LayoutRules.Rows + 2 && roads.All(r => Near(r.Top, UptownLots.GroundY));
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            float middle = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f;
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                carriageways &= roads.Any(r => r.Holds(LayoutRules.StreetX(k), middle, 0f));
            }
            carriageways &= roads.Any(r => r.Holds(LayoutRules.StreetX(3) + 40f, LayoutRules.AvenueZ(row), 0f));
        }
        carriageways &= roads.Any(r => r.Holds(LayoutRules.StreetX(-3), LayoutRules.AvenueZ(0), 0f)) && roads.Any(r => r.Holds(LayoutRules.StreetX(11), LayoutRules.AvenueZ(0), 0f));
        Check(carriageways, "uptown ground: a road collider under every carriageway of the band and of 1st Avenue's continuations");
        var hides = new List<GroundRect>();
        LayoutPlans.Hides(hides);
        bool hidesOk = hides.Count == 15 && hides.All(h => h.Z1 >= LayoutPlans.South - 0.01f);
        float[][] doors = { new[] { 373.1f, 478.9f }, new[] { 266.8f, 479.5f }, new[] { 92.0f, 479f }, new[] { 2f, 478f }, new[] { -94f, 478f }, new[] { -175f, 478f }, new[] { -272f, 478f } };
        hidesOk &= doors.All(d => !hides.Any(h => h.Holds(d[0], d[1])));
        for (float x = LayoutPlans.WestCornerJoin + 0.5f; x < LayoutPlans.EastCornerJoin; x += 5f)
        {
            hidesOk &= !hides.Any(h => h.Holds(x, LayoutRules.AvenueZ(0))) && !hides.Any(h => h.Holds(x, LayoutPlans.South - 0.5f));
        }
        // The backdrop over the band and its scenery goes: the middles of every block and street of it.
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                hidesOk &= LayoutRules.IsFirst(row, column) || hides.Any(h => h.Holds((x0 + x1) * 0.5f, (z0 + z1) * 0.5f));
            }
        }
        hidesOk &= modules.Where(m => (m.Scenery || m.Kind == ModuleKind.Ground) && m.Z > LayoutPlans.South).All(m => hides.Any(h => h.Holds(m.X, m.Z)));
        Check(hidesOk, "uptown hiding: the backdrop goes over the band and under its scenery north of 1st Avenue, never over 1st Avenue between its corners, its buildings or "
                       + "Manhattan south of it");
        var zones = new List<PlanBox>();
        LayoutPlans.Zones(zones);
        Check(zones.Count == 111 && zones.All(z => UptownCovered(npc.Boxes, z.X, z.Z) && UptownCovered(npc.Boxes, z.X - z.HalfX + 0.1f, z.Z - z.HalfZ + 0.1f)
                                                    && UptownCovered(npc.Boxes, z.X + z.HalfX - 0.1f, z.Z + z.HalfZ - 0.1f)),
            "uptown people: 111 zones, one along each street's east pavement and each avenue's south one, every one on the pedestrians' ground");
        var termini = new List<PlanTerminus>();
        LayoutPlans.Termini(termini);
        Check(termini.Count == 3 && termini.All(t => UptownCovered(npc.Boxes, t.X, t.Z)) && termini.Select(t => t.NameKey).Distinct().Count() == 3
              && termini[0].NameKey == GroundText.Terminus && termini[0].X > LayoutRules.StreetX(0) && termini[0].X < LayoutRules.StreetX(LayoutRules.FirstColumns)
              && termini.Select(t => (int)Math.Round((t.Z - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch)).SequenceEqual(new[] { 1, 2, 3 }),
            "uptown buses: three termini, one on each avenue's south pavement, Uptown Avenue's in the first block, each named");
        LayoutPlans.MapBounds(out float west, out float east, out float north);
        Check(LayoutRules.Lots.All(l => l.X - 40f >= west && l.X + 40f <= east && l.Z + 40f <= north), "uptown map: the city map's bounds reach 40 m past every lot, as a made building needs");
    }

    // ---------------------------------------------------------------- what the census counts, and the lots' life at a load

    private static void UptownCountChecks()
    {
        var modules = new List<ModulePlace>();
        LayoutPlans.Modules(modules);
        var oldX = new List<float>();
        var oldZ = new List<float>();
        LayoutPlans.OldWalls(oldX, oldZ);
        var zones = new List<PlanBox>();
        LayoutPlans.Zones(zones);
        bool kinds = true;
        foreach (ModuleKind kind in new[] { ModuleKind.Street, ModuleKind.Avenue, ModuleKind.Crossing, ModuleKind.Ground })
        {
            foreach (bool scenery in new[] { false, true })
            {
                kinds &= LayoutPlans.ModuleCount(kind, scenery) == modules.Count(m => m.Kind == kind && m.Scenery == scenery);
            }
        }
        Check(LayoutPlans.StreetCount == 19 && kinds && LayoutPlans.OldWallCount == oldX.Count && LayoutPlans.ZoneCount == zones.Count
              && LayoutPlans.JunctionCount == LayoutPlans.Streets().Junctions.Count,
            "uptown census: it expects the pieces, walls, zones and junctions the plans lay (" + modules.Count + ", " + LayoutPlans.OldWallCount + ", " + LayoutPlans.ZoneCount + ", "
            + LayoutPlans.JunctionCount + ")");
        int streets = LayoutPlans.ModuleCount(ModuleKind.Street, false), avenues = LayoutPlans.ModuleCount(ModuleKind.Avenue, false);
        Check(LayoutPlans.ParkingCount == streets * LayoutPlans.StreetParking + avenues * LayoutPlans.AvenueParking && LayoutPlans.ParkingCount == 185,
            "uptown census: a parking lane along each street length of the band and two along each avenue length, none in the scenery (" + LayoutPlans.ParkingCount + ")");
        bool nearest = true;
        int[] through = { 0, 1, 2, 3, 5, 6, 7, 8 };
        for (float x = -800f; x <= 900f; x += 2.5f)
        {
            int k = LayoutPlans.NearestThrough(x);
            float best = through.Min(s => Math.Abs(LayoutRules.StreetX(s) - x));
            nearest &= LayoutPlans.HasThrough(k) && Near(Math.Abs(LayoutRules.StreetX(k) - x), best, 0.001f);
        }
        Check(nearest && LayoutPlans.NearestThrough(LayoutRules.StreetX(4)) != 4 && through.All(LayoutPlans.HasThrough) && !LayoutPlans.HasThrough(4) && !LayoutPlans.HasThrough(9),
            "uptown census: a walk from Manhattan starts at the nearest street through 1st Avenue's junctions, never 5th Street");
    }

    private static void UptownLifeChecks()
    {
        int stamp = UptownLots.Stamp;
        Lot firstLot = LayoutRules.FirstLots[0], farLot = LayoutRules.Lots.Last(l => !l.First);
        UptownLots.Begin("the city is loading");
        Check(!UptownLots.Ready && !UptownLots.Closed && UptownLots.Why == "the city is loading" && !UptownLots.Open(firstLot) && !UptownLots.Open(farLot),
            "uptown lots: a load begins with nothing open and why");
        UptownLots.Laid(true, null);
        Check(UptownLots.Ready && UptownLots.Why == null && UptownLots.Stamp == stamp + 1 && UptownLots.All.All(UptownLots.Open) && !UptownLots.Open(null),
            "uptown lots: laid, every lot of the band opens and the stamp moves once");
        var heard = new List<string>();
        Action<string> earliest = why => heard.Add("first " + UptownLots.Ready);
        Action<string> faulty = why => throw new InvalidOperationException("a handler's fault");
        Action<string> latest = why => heard.Add("last " + why);
        UptownLots.Closing += earliest;
        UptownLots.Closing += faulty;
        UptownLots.Closing += latest;
        int faults = 0;
        UptownLots.Close("the test's clean-up", e => faults++);
        UptownLots.Close("again", e => faults++);
        UptownLots.Closing -= earliest;
        UptownLots.Closing -= faulty;
        UptownLots.Closing -= latest;
        Check(heard.Count == 2 && heard[0] == "first True" && heard[1] == "last the test's clean-up" && faults == 1,
            "uptown lots: closing tells every handler first, while the lots are still open, a faulty one apart, and only once");
        Check(UptownLots.Closed && !UptownLots.Ready && UptownLots.Why.Contains("the test's clean-up") && UptownLots.Stamp == stamp + 2 && !UptownLots.Open(firstLot) && !UptownLots.Open(farLot),
            "uptown lots: closed until the next load, with why, the stamp moved again");
        UptownLots.Begin("the city is loading");
        Check(!UptownLots.Closed && !UptownLots.Ready, "uptown lots: the next load opens the way again");
        UptownLots.Laid(false, null);
        Check(!UptownLots.Ready && UptownLots.Why != null && !UptownLots.Open(farLot), "uptown lots: a step that stood down without a reason still says something, and no lot opens");
    }

    // ---------------------------------------------------------------- the cost

    private static void UptownTimingChecks()
    {
        var watch = Stopwatch.StartNew();
        RoadPlan plan = LayoutPlans.Streets();
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        var modules = new List<ModulePlace>(512);
        LayoutPlans.Modules(modules);
        RoadBuild build = UptownBuild(out _);
        watch.Stop();
        var shrink = Stopwatch.StartNew();
        var into = new List<GroundBox>();
        GroundShapes.ShrinkOutward(player.Boxes, AgentRadius, into);
        GroundShapes.ShrinkOutward(npc.Boxes, AgentRadius, into);
        shrink.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 400.0 && plan.Roads.Count > 0 && player.Boxes.Count > 0 && npc.Boxes.Count > 0 && modules.Count > 0 && build.Problem == null,
            "uptown cost: the band's plans and its streets' build in under 400 ms (" + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms, " + build.Count + " lane points, "
            + player.Boxes.Count + " and " + npc.Boxes.Count + " boxes), once a load behind the loading screen");
        Check(shrink.Elapsed.TotalMilliseconds < 200.0, "uptown cost: both plans' margins in under 200 ms (" + shrink.Elapsed.TotalMilliseconds.ToString("0.0") + " ms)");
    }

    // ---------------------------------------------------------------- the sources

    // The folder holds the district's life too (Life*, DistrictValues, DistrictBuildings, BuildingsRules), which keeps rules
    // of its own and is checked by EconomyCheck's UptownLifeChecks: it seeds Unity's Random and gives it back, keeps the
    // district's own save line and touches the scene in its LifeGame files. These rules are the ground's.
    private static bool GroundsFile(string name)
    {
        return name.StartsWith("Layout", StringComparison.Ordinal) || name.StartsWith("Ground", StringComparison.Ordinal) || name == "UptownLots.cs";
    }

    private static void UptownSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string[] files = Directory.GetFiles(folder, "*.cs").Where(f => GroundsFile(Path.GetFileName(f))).ToArray();
        var code = files.ToDictionary(Path.GetFileName, f => Code(File.ReadAllText(f)));
        foreach (string name in new[] { "UptownLots.cs", "LayoutRules.cs", "LayoutPlans.cs", "LayoutStreets.cs" })
        {
            Check(code.ContainsKey(name), "uptown sources: " + name + " is there");
            if (code.TryGetValue(name, out string text))
            {
                Check(!Regex.IsMatch(text, @"\busing UnityEngine|\busing Gley|\bVector3\b|\bMathf\b|\bGameObject\b"), "uptown sources: " + name + " is game-free");
            }
        }
        foreach (var file in code)
        {
            Check(!Regex.IsMatch(file.Value, @"\bSystem\.Linq\b|using System\.Linq"), "uptown sources: no LINQ (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"new System\.Random\(\)|\bUnityEngine\.Random\b|\bRandom\.Range\b|DateTime\.Now"), "uptown sources: nothing left to chance or the clock (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"\bmodData\b|\bSaveHook\b|\bSend\("), "uptown sources: nothing saved or sent (" + file.Key + ")");
        }
    }

    // The game's side: the scene touched in the Game files alone, the load's order, what stands down gives back, the
    // session asked, the contract's line, the director's place in the city's load, the shared lines.
    private static void UptownGameSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        var code = Directory.GetFiles(folder, "*.cs").Where(f => GroundsFile(Path.GetFileName(f))).ToDictionary(Path.GetFileName, f => Code(File.ReadAllText(f)));
        string[] mine = { "GroundGame.cs", "GroundGame.Art.cs", "GroundDirector.cs", "GroundDirector.Tests.cs", "GroundSession.cs", "GroundGuide.cs", "GroundText.cs" };
        foreach (string name in mine)
        {
            Check(code.ContainsKey(name), "uptown sources: " + name + " is there");
        }
        if (!mine.All(code.ContainsKey))
        {
            return;
        }
        var scene = new Regex(@"\b(?:Object\.Instantiate|Instantiate\(|SetActive\(|AddComponent<|GetComponentsInChildren<|SceneManager\.|DestroyImmediate|\.enabled = |\.bounds = |redTrafficLight)");
        foreach (var file in code)
        {
            bool gameFile = file.Key.StartsWith("GroundGame", StringComparison.Ordinal);
            Check(gameFile || !scene.IsMatch(file.Value), "uptown sources: " + file.Key + (gameFile ? " is a Game file" : " leaves the scene to the Game files"));
            Check(!Regex.IsMatch(file.Value, @"\bGetField\(|\bGetProperty\(|\bGetMethod\(|BindingFlags|MP(?:Server|Client|Config)"),
                "uptown sources: " + file.Key + " reads nothing by name; the game through the contract, Going Public through GoingPublic");
            Check(!Regex.IsMatch(file.Value, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate)\s*\(") || file.Key == "GroundDirector.cs",
                "uptown sources: only the director has a frame method (" + file.Key + ")");
        }
        string director = code["GroundDirector.cs"], tests = code["GroundDirector.Tests.cs"], art = code["GroundGame.Art.cs"], game = code["GroundGame.cs"];
        string session = code["GroundSession.cs"];
        int Order(string text, string what) => text.IndexOf(what, StringComparison.Ordinal);
        string[] steps =
        {
            "GroundSession.Agrees(", "GroundGame.FindBackdrop()", "GroundGame.FindTemplates()", "GroundGame.HideBackdrop(", "GroundGame.HideStrays(", "GroundGame.OpenWalls(",
            "GroundWork.Streets(", "GroundGame.CopyArt(", "GroundWork.Ground(LayoutPlans.PlayerGround())", "GroundWork.TakeBack(", "GroundWork.Ground(LayoutPlans.NpcGround())",
            "GroundGame.Build(", "GroundGame.WidenMap(", "TransitMap.AddTerminus(", "ScenePrewarm.AddLate(", "UptownLots.Laid(_walkable"
        };
        bool ordered = true;
        for (int i = 0; i < steps.Length; i++)
        {
            ordered &= Order(director, steps[i]) >= 0 && (i == 0 || Order(director, steps[i]) > Order(director, steps[i - 1]));
        }
        Check(ordered, "uptown load: asked, found, the backdrop, its strays and the old walls put away (the ground's count finds what is left), the streets, the art (what of it "
                       + "stands on the ground is walked round), the player's ground (or the streets taken back), the pedestrians', the new walls and colliders, the map, the "
                       + "termini, the late parking and people, then the lots open; in that order");
        Check(Regex.Matches(director, @"StandDown\(""its streets: "" \+ why, true\)|StandDown\(""its ground for the player: "" \+ why, true\)").Count == 2
              && Regex.IsMatch(director, @"StandDown\(why, false\)") && Regex.IsMatch(director, @"if \(laid\)\s*\{\s*GroundGame\.GiveBack\(\);")
              && Regex.IsMatch(game, @"public static void GiveBack\(\)\s*\{\s*ShowBackdrop\(\);\s*CloseWalls\(\);\s*Forget\(\);"),
            "uptown load: every step that stands down after the backdrop was put away gives it all back the same load, the copied art with it, with one line");
        Check(Regex.IsMatch(director, @"private void Awake\(\)\s*\{[^}]*UptownLots\.Begin\([^}]*Lay\(\);") && director.Contains("UptownLots.Laid(false, stays ?"),
            "uptown lots: closed as a load begins, opened by the ground's step alone, closed again when the city goes");
        Check(Regex.IsMatch(session, @"if \(!Settings\.Uptown\)") && session.Contains("GoingPublic.RefusesOtherMods") && session.Contains("GoingPublic.ModsAsHost")
              && Order(session, "Settings.Uptown") < Order(session, "GoingPublic.IsHost"),
            "uptown session: the option first, then a session's host that refuses other mods and a guest whose mods are the host's");
        Check(code.Where(f => f.Key != "GroundSession.cs" && f.Key != "GroundGuide.cs").All(f => !f.Value.Contains("Settings.Uptown")),
            "uptown session: the option is read through the session's one question");
        Check(art.Contains("GameContract.RoadStreetName.Ok") && art.Contains("GameContract.RoadStreetName.Field.SetValue(") && code.Where(f => f.Key != "GroundGame.Art.cs").All(f => !f.Value.Contains("RoadStreetName")),
            "uptown sources: the street's label written through the contract's line, in the Game file");
        Check(Regex.IsMatch(art, @"_art = new GameObject\(ArtName\);\s*_art\.SetActive\(false\);") && Order(art, "_art.SetActive(true);") > Order(art, "Strip(copy);")
              && Order(art, "_art.SetActive(true);") > Order(art, "Wire(copy, build"),
            "uptown art: the copies made asleep and woken together once stripped and wired");
        Check(Regex.Matches(art, @"SceneFinds\.All<").Count == 2 && !Regex.IsMatch(game + art, @"FindObjects|GameObject\.Find\("), "uptown art: two searches of the scene, behind the loading screen, through SceneFinds");
        Check(Regex.IsMatch(art, @"ArtName = ""RealLifeOverhaul ") && Regex.IsMatch(game, @"GroundName = ""RealLifeOverhaul "),
            "uptown art: the copies and the walls under roots named for the mod, which the ground's count of the game's colliders leaves out");
        Check(Regex.IsMatch(game, @"StrayRoot = ""TheHamptons""") && Regex.IsMatch(game, @"SceneRoots\[i\]\.name != StrayRoot"),
            "uptown hiding: the strays put away are another scene's pieces on the band, of that one scene alone");
        Check(game.Contains("ShowBackdrop();") && game.Contains("CloseWalls();") && Regex.IsMatch(game, @"public static void Forget\(\)\s*\{\s*DropArt\(\);"),
            "uptown sources: the backdrop and the walls given back on a stand-down, the mod's own pieces when the city goes");
        Check(tests.Contains("\"Uptown check (path, \"") && tests.Contains("\"Uptown check (traffic): ") && tests.Contains("\"Uptown check (census): ")
              && Regex.Matches(tests, @"TestRefusals\.(?:Tell|TellKey)\(").Count >= 6,
            "uptown tests: each says why it did not start and writes one check line");
        // The owner's test of 23 Oct: CityMap keeps its camera only from the game's loaded event, after the load steps, so
        // asking it there stood the whole district down at every load.
        Check(!game.Contains("camera was not found") && Regex.IsMatch(game, @"_map = MapCamera\(\);\s*return null;")
              && game.Contains("game.citymapCamera.GetComponentInParent<CityMapCam>(true)") && director.Contains("if (!GroundGame.WidenMap("),
            "uptown load: the city map's camera found as the game's CityMap.Init finds it, and never a reason to stand the district down");
        string mod = Code(File.ReadAllText(Path.Combine(scripts, "Core", "RealLifeOverhaulMod.cs")));
        int uptown = Order(mod, "Guard.Add<Uptown.GroundDirector>(_root)"), buildings = Order(mod, "Guard.Add<NewBuildings.NewBuildingsDirector>(_root)"),
            prewarm = Order(mod, "ScenePrewarm.Begin()"), ground = Order(mod, "Guard.Add<NewGround.GroundDirector>(_root)");
        Check(uptown > ground && uptown < buildings && buildings < prewarm, "uptown load: the director added after new ground's, before the new buildings' and the scene's prewarm");
        string cities = Code(File.ReadAllText(Path.Combine(scripts, "Meta", "FeatureCities.cs")));
        string tree = Code(File.ReadAllText(Path.Combine(scripts, "Meta", "OptionsTree.cs")));
        string streets = Code(File.ReadAllText(Path.Combine(scripts, "NewBuildings", "NewBuildingsRules.cs")));
        string contract = Code(File.ReadAllText(Path.Combine(scripts, "Core", "GameContract.cs")));
        Check(cities.Contains("new Line(\"uptown\", CityScope.NewYork,") && tree.Contains("S(\"uptown\").For(\"uptown\").Main(\"uptown\")") && streets.Contains("Uptown.LayoutStreets.All")
              && Regex.IsMatch(contract, @"RoadStreetName = Optional\(Field\(UptownLabels, Game, ""Road"", ""streetName"", ""String""\)\)"),
            "uptown sources: New York's alone, its section on the options page, its streets among the mod's own, the label's field an optional line of the contract");
    }

    // The texts: every key the folder names in the three languages, short, without dashes or exclamation marks; the street
    // names fixed words in every language; and What's new's line in all nine, before the first fix.
    private static void UptownTextChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        var named = Directory.GetFiles(folder, "Ground*.cs").SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(reallife_[a-z_]+)\"").Select(m => m.Groups[1].Value))
                             .Concat(new[] { "reallife_opt_sec_uptown", "reallife_opt_tip_uptown" }).Concat(LayoutStreets.All.Select(s => s.NameKey)).Distinct().ToList();
        Check(named.Count >= 35, "uptown texts: its texts are found: " + named.Count);
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in named)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0, "uptown texts: " + key + " is in " + language + ".json");
                if (language == "en" && doc.RootElement.TryGetProperty(key, out JsonElement en) && key != "reallife_opt_tip_uptown")
                {
                    Check(en.GetString().Length <= 240, "uptown texts: " + key + " is at most 240 characters");
                    Check(!Regex.IsMatch(en.GetString(), "[–—!]| - "), "uptown texts: " + key + " has no dash and no exclamation mark");
                }
            }
            foreach (LayoutStreet s in LayoutStreets.All)
            {
                Check(doc.RootElement.TryGetProperty(s.NameKey, out JsonElement name) && Regex.IsMatch(name.GetString(), @"^(?:\d+(?:st|nd|rd|th)|[A-Z][a-z]+) (?:Street|Avenue)$"),
                    "uptown texts: " + s.Id + " is named as the game names its own streets, in " + language);
            }
        }
        var fixMark = new Dictionary<string, string>
        {
            { "en", "Fixed: " }, { "pt", "Corrigido: " }, { "zh-cn", "已修复：" }, { "de", "Behoben: " }, { "fr", "Corrigé : " }, { "es", "Corregido: " },
            { "ru", "Исправлено: " }, { "nl", "Opgelost: " }, { "tr", "Düzeltildi: " }
        };
        // Uptown's line opens as it did and now tells of the whole band.
        var piece = new Dictionary<string, string[]>
        {
            { "en", new[] { "Manhattan now goes on north past 1st Avenue into Uptown", "three rows of eighteen blocks" } },
            { "pt", new[] { "Manhattan agora segue para o norte depois da 1st Avenue", "três fileiras de dezoito quarteirões" } },
            { "zh-cn", new[] { "曼哈顿现在越过 1st Avenue 继续向北延伸", "三排共十八列街区" } },
            { "de", new[] { "Manhattan geht jetzt hinter der 1st Avenue nach Norden weiter", "drei Reihen mit je achtzehn Blöcken" } },
            { "fr", new[] { "Manhattan se prolonge désormais vers le nord après la 1st Avenue", "trois rangées de dix-huit pâtés de maisons" } },
            { "es", new[] { "Manhattan ahora sigue hacia el norte más allá de la 1st Avenue", "tres filas de dieciocho manzanas" } },
            { "ru", new[] { "Манхэттен теперь продолжается на север за 1st Avenue", "три ряда по восемнадцать кварталов" } },
            { "nl", new[] { "Manhattan loopt nu door naar het noorden, voorbij 1st Avenue", "drie rijen van achttien blokken" } },
            { "tr", new[] { "Manhattan artık 1st Avenue'nun ötesinde kuzeye", "on sekizer bloktan oluşan üç sıra" } }
        };
        foreach (var language in piece)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language.Key + ".json")));
            string next = doc.RootElement.TryGetProperty("reallife_changes_next", out JsonElement row) ? row.GetString() : string.Empty;
            int at = next.IndexOf(language.Value[0], StringComparison.Ordinal), band = next.IndexOf(language.Value[1], StringComparison.Ordinal);
            int fix = next.IndexOf(fixMark[language.Key], StringComparison.Ordinal);
            Check(at >= 0 && band > at && (fix < 0 || band < fix), "uptown texts: What's new's next row tells of Uptown's whole band in " + language.Key + ", before its fixes");
        }
    }
}
