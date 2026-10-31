using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Construction;

// Leftover plots (the owner, 28 Oct 2026: a building took part of a block and each single plot left over read "no design", with no way out;
// Scripts/Construction: PlotJoinRules, ConstructionDirector.JoinOffer, BuildMode.Join, GrowRules.Counts, Rest and Leave, ConstructionDirector.Grow):
// the design fit rule that leaves a single space with shops' designs alone, the join the Place tool offers (which side, what it says, the shape
// and the rows it is cut to, the use that fits as it is), the city's own growth leaving no stretch of one space that nothing fills on the real
// plan, then the sources (nothing a frame, the save untouched) and the texts.
internal static partial class Program
{
    private static void PlotJoinChecks()
    {
        PlotJoinFitChecks();
        PlotJoinOfferChecks();
        PlotJoinGrowChecks();
        PlotJoinSourceChecks();
        PlotJoinTextChecks();
    }

    private static void PlotJoinFitChecks()
    {
        // The rule behind the 0: a design is offered to a site of exactly its own spaces (or fewer when its frontage fills the site), so a leftover single space
        // has the one-space designs only, and joined with the site beside it the size of the joined shape.
        Check(!BuildCatalogue.Offered(1, 2, false) && !BuildCatalogue.Offered(1, 3, false) && BuildCatalogue.Offered(1, 1, false),
            "leftover plots: a single space is offered one-space designs only, none of the two- or three-space ones");
        PlotJoinRules.Shape(PlotJoinRules.After, 4, 1, 5, 2, out int first, out int columns);
        Check(first == 4 && columns == 3 && BuildCatalogue.Offered(columns, 3, false) && !BuildCatalogue.Offered(columns, 4, false),
            "leftover plots: a single space joined with the two beside it is one site of three spaces, and a three-space design fits it (" + first + ", " + columns + ")");
        PlotJoinRules.Shape(PlotJoinRules.Before, 4, 1, 2, 2, out first, out columns);
        Check(first == 2 && columns == 3, "leftover plots: joined with the site before it the shape starts at that site's first column (" + first + ", " + columns + ")");
        PlotJoinRules.Shape(PlotJoinRules.Neither, 4, 1, 0, 0, out first, out columns);
        Check(first == 4 && columns == 1, "leftover plots: no join leaves the shape as it is");
    }

    private static void PlotJoinOfferChecks()
    {
        const int After = PlotJoinRules.After, Before = PlotJoinRules.Before, Neither = PlotJoinRules.Neither;
        Check(PlotJoinRules.Side(true, 3, true, 5) == After && PlotJoinRules.Side(true, 0, true, 2) == Before && PlotJoinRules.Side(true, 0, true, 0) == After
              && PlotJoinRules.Side(false, 0, true, 0) == Before && PlotJoinRules.Side(true, 0, false, 0) == After && PlotJoinRules.Side(false, 9, false, 9) == Neither,
            "leftover plots: the plot to join is the one that gives designs (the one after first), else the one that can be joined, else none");
        Check(PlotJoinRules.Kind(4, After, 0) == PlotJoinRules.Fits && PlotJoinRules.Kind(0, After, 3) == PlotJoinRules.Offer && PlotJoinRules.Kind(0, Before, 0) == PlotJoinRules.Useless
              && PlotJoinRules.Kind(0, Neither, 7) == PlotJoinRules.Alone,
            "leftover plots: a plot with designs is offered nothing; with none, a join that gives designs is offered, one that gives none is said to, and none possible is Alone");
        Check(PlotJoinRules.Rows(0, 5, 5) == 5 && PlotJoinRules.Rows(3, 5, 4) == 3 && PlotJoinRules.Rows(5, 2, 4) == 2 && PlotJoinRules.Rows(-1, 4, 0) == 4 && PlotJoinRules.Rows(0, 0, 0) >= 1,
            "leftover plots: the joined shape is cut to the shallowest of the two sites and of the picked rows, never under one");
        int[] by = { 0, 2, 0, 5 };
        Check(PlotJoinRules.OtherUse(0, 0b1111, by) == 1 && PlotJoinRules.OtherUse(1, 0b1111, by) == 3 && PlotJoinRules.OtherUse(1, 0b0011, by) == -1 && PlotJoinRules.OtherUse(0, 0b1111, null) == -1,
            "leftover plots: the use named instead is a zoned one, other than the site's, that has designs on the squares as they are");
    }

    // The widths a city site is tried in, and what each leaves beside it, on the real plan's free runs: the rule is a pure walk over the run, so what is held
    // here is what the director's GrowCount does with a design test at each width.
    private static void PlotJoinGrowChecks()
    {
        var into = new int[BuildCatalogue.SiteSpacesMost * 2];
        int n = GrowRules.Counts(2, 5, false, into);
        Check(n == 2 && into[0] == 2 && into[1] == 1, "leftover plots: the widths tried from the one wanted down to one (" + string.Join(",", into.Take(n)) + ")");
        n = GrowRules.Counts(2, 5, true, into);
        Check(n == 5 && into[0] == 2 && into[1] == 1 && into[2] == 3 && into[3] == 4 && into[4] == 5,
            "leftover plots: then, roomy, the wider ones up to the whole run (" + string.Join(",", into.Take(n)) + ")");
        n = GrowRules.Counts(9, 3, true, into);
        Check(n == 3 && into[0] == 3 && into[2] == 1, "leftover plots: never wider than the run (" + string.Join(",", into.Take(n)) + ")");
        n = GrowRules.Counts(0, 1, true, into);
        Check(n == 1 && into[0] == 1, "leftover plots: a run of one is tried at one");
        n = GrowRules.Counts(3, 40, true, into);
        Check(n <= into.Length && into.Take(n).All(c => c >= 1 && c <= BuildCatalogue.SiteSpacesMost) && into.Take(n).Distinct().Count() == n,
            "leftover plots: no width beyond the widest site, none twice");

        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        GrowColumn[] cols = GrowLand(lots, grid);
        int runs = 0, clean = 0, restOk = 0, leftOut = 0;
        var visited = new HashSet<int>();
        var hist = new SortedDictionary<string, int>();
        for (int c = 0; c < grid.Columns.Count; c++)
        {
            int prev = grid.Prev(c);
            if (!cols[c].Free || cols[c].Use < 0 || (prev >= 0 && cols[prev].Free && cols[prev].Use == cols[c].Use))
            {
                continue;   // not the first of a free run of one use
            }
            int run = 0;
            for (int at = c; at >= 0 && cols[at].Free && cols[at].Use == cols[c].Use && run < BuildCatalogue.SiteSpacesMost; at = grid.Next(at))
            {
                run++;
            }
            runs++;
            int use = cols[c].Use;
            string hk = use + ":" + run; hist[hk] = hist.TryGetValue(hk, out int hv) ? hv + 1 : 1;
            // A stand-in for the design catalogue: shops have a design at every width, the other uses from two spaces.
            Func<int, bool> has = w => use == (int)BuildUse.Shops ? w >= 1 : w >= 2;
            int wanted = Math.Min(run, 2);
            int took = GrowRules.Counts(wanted, run, true, into);
            int firstOk = 0, chosen = 0;
            for (int k = 0; k < took; k++)
            {
                int count = into[k];
                int start = GrowRules.Take(grid, c, run, count);
                GrowRules.Rest(grid, c, run, start, count, out int leftAt, out int left, out int rightAt, out int right);
                restOk += left + right + count == run && (left > 0) == (leftAt >= 0) && (right > 0) == (rightAt >= 0) && left * right == 0 ? 1 : 0;
                if (!has(count))
                {
                    continue;
                }
                firstOk = firstOk == 0 ? count : firstOk;
                if ((left == 0 || has(left)) && (right == 0 || has(right)))
                {
                    chosen = count;
                    break;
                }
            }
            leftOut += chosen == 0 ? 1 : 0;
            clean += chosen > 0 || firstOk == 0 ? 1 : 0;
            visited.Add(c);
        }
        Check(runs >= 20 && restOk > 0, "leftover plots: the real plan has free runs to walk: " + runs + ", each width's leftover read from the run (" + restOk + " widths)");
        int singles = hist.Where(kv => kv.Key.EndsWith(":1") && !kv.Key.StartsWith("1:")).Sum(kv => kv.Value);
        Check(leftOut == singles && singles > 0,
            "leftover plots: on the real plan the runs of one space with no design (the uses other than shops) are the ones left out of the pass: " + leftOut + " of " + singles);
        // Every run length and every width wanted, with the same stand-in: the walk always ends on a width that leaves no unfillable space beside it where a wider
        // one avoids it, and the first width with a design alone would not have.
        int combos = 0, avoided = 0, plainStrands = 0;
        for (int run = 2; run <= BuildCatalogue.SiteSpacesMost; run++)
        {
            for (int want = 1; want <= run; want++)
            {
                Func<int, bool> has = w => w >= 2;
                int count = GrowRules.Counts(want, run, true, into), chosen = 0, firstOk = 0;
                for (int k = 0; k < count; k++)
                {
                    int w = into[k];
                    firstOk = firstOk == 0 && has(w) ? w : firstOk;
                    if (chosen == 0 && has(w) && (run - w == 0 || has(run - w)))
                    {
                        chosen = w;
                    }
                }
                combos++;
                avoided += chosen > 0 ? 1 : 0;
                plainStrands += firstOk > 0 && run - firstOk > 0 && !has(run - firstOk) ? 1 : 0;
            }
        }
        Check(avoided == combos && plainStrands > 0,
            "leftover plots: for every run of two to seven and every width wanted the walk finds a width that strands nothing (" + avoided + " of " + combos
            + "), where the first width with a design alone strands a space in " + plainStrands);
        Check(clean == runs, "leftover plots: every free run of the real plan ends in a width that has a design, or none has one (" + clean + " of " + runs + ")");

        // A run no design fills is left out of the day's pass, and the pass goes on to the next run.
        var copy = (GrowColumn[])cols.Clone();
        bool picked = GrowRules.Pick(grid, copy, Only(1), 4711, 40, out _, out int pickFirst, out int pickRun, out _);
        int left2 = picked ? GrowRules.Leave(grid, copy, pickFirst, pickRun) : -1;
        bool again = GrowRules.Pick(grid, copy, Only(1), 4711, 40, out _, out int nextFirst, out _, out _);
        Check(picked && left2 == pickRun && (!again || nextFirst != pickFirst), "leftover plots: a run left out is not picked again today (" + left2 + " column(s) left out)");
        Check(GrowRules.Leave(grid, copy, pickFirst, pickRun) == 0, "leftover plots: leaving a run out twice changes nothing");
        Check(GrowRules.RunsTried >= 2 && GrowRules.RunsTried <= 12, "leftover plots: the pass looks at a few runs, not all of them (" + GrowRules.RunsTried + ")");
    }

    private static void PlotJoinSourceChecks()
    {
        string join = ModSource(@"Construction\BuildMode.Join.cs"), window = ModSource(@"Construction\BuildMode.Window.cs"), offer = ModSource(@"Construction\ConstructionDirector.JoinOffer.cs");
        string grow = ModSource(@"Construction\ConstructionDirector.Grow.cs"), rules = ModSource(@"Construction\PlotJoinRules.cs"), build = ModSource(@"Construction\BuildMode.cs");
        Check(window.Contains("WriteNoDesign(s, parks)") && window.Contains("case RowJoinNext:") && join.Contains("_d.Join(_offer.First)") && build.Contains("OfferRead();"),
            "leftover plots: the Place panel writes the offer, its button joins through the Join tool's own join, the offer is read when squares are picked");
        Check(!join.Contains("void Update") && !join.Contains("LateUpdate") && !offer.Contains("Update(") && !rules.Contains("using UnityEngine"),
            "leftover plots: nothing a frame, and the rules are game-free");
        Check(offer.Contains("JoinBlock(own.Id") && offer.Contains("_state.OnLot(own.Id)") && join.Contains("PlotJoinRules.Offer"),
            "leftover plots: the join offered is the one the Join tool would make, a busy site is never offered one");
        Check(grow.Contains("GrowRules.Counts(") && grow.Contains("GrowRules.Rest(") && grow.Contains("GrowRules.Leave(") && grow.Contains("GrowRules.RunsTried"),
            "leftover plots: the city's growth walks the widths, reads what each leaves and leaves a run no design fills out of the day's pass");
        Check(!offer.Contains("_state.Lots.Add") && !offer.Contains("Changed()") && !join.Contains("BuildStore"),
            "leftover plots: the offer changes nothing and saves nothing: the save's line is as it was, older versions read it");
    }

    private static void PlotJoinTextChecks()
    {
        string[] keys =
        {
            PlotJoinRules.OfferKey, PlotJoinRules.UselessKey, PlotJoinRules.UseKey, PlotJoinRules.ButtonKey, PlotJoinRules.HintKey
        };
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("leftover plots texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string bad = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ")
                    || value.Contains("  "))
                {
                    bad = bad ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    bad = bad ?? key + " (its names in curly brackets)";
                }
            }
            Check(bad == null, "leftover plots texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (bad ?? "all") + ")");
        }
    }
}
