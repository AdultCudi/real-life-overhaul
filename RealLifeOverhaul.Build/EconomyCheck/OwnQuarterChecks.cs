using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

// A player's own company whose price was cut by two thirds in a day when its results came out (Scripts/Markets/Listing/ListingRules.cs: Results, GrowthLog). Its quarters read 280 to 300 million
// a quarter with two of 708 and 637 million in the middle, then 331, 296 and 280, and each results day moved the price: up while the two big quarters stood in the newer half of the eight the
// growth was read from, then down by 71% on the day they moved to the older half. The measure of the quarters themselves is the sum of the days' statements and is whole (the steady checks
// below hold it to the same quarter every time through weekends, year turns and long steps); what was wrong was the price taking a one-off quarter for growth and then for a collapse, all
// at once, because a share's worth moves by about eight times any change in the growth the market believes.
internal static partial class Program
{
    private static void OwnQuarterAll()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        OwnQuarterReadingChecks();
        OwnQuarterSteadyChecks();
        OwnQuarterBurstChecks();
        OwnQuarterPlayersChecks();
        OwnQuarterListingChecks();
        OwnQuarterSources();
        Console.WriteLine("  the own company's quarters and growth: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void OwnQuarterReadingChecks()
    {
        double[] steady = { 300, 300, 300, 300, 300, 300, 300, 300 };
        Check(ListingRules.GrowthLog(steady, 8, 4) == 0.0, "own quarters: steady takings read as no growth");
        double[] growing = { 330, 322, 315, 308, 300, 293, 286, 280 };   // about 9% a year
        double g = ListingRules.GrowthLog(growing, 8, 4);
        Check(Math.Abs(g - Math.Log((330 + 322 + 315 + 308) / (300 + 293 + 286 + 280.0))) < 1e-12 && g > 0.08 && g < 0.11, "own quarters: steady growth is read in full (" + g.ToString("F4") + ")");
        double[] twoBig = { 280, 296, 331, 637, 708, 292, 294, 302 };   // the player's own at their last results: the two big quarters are in the older half
        double[] twoBigNew = { 331, 296, 637, 708, 292, 294, 302, 280 };   // ...and at the results before, in the newer
        Check(Math.Abs(ListingRules.GrowthLog(twoBig, 8, 4)) < 0.03 && Math.Abs(ListingRules.GrowthLog(twoBigNew, 8, 4)) < 0.06,   // 331 against 292 is a real 13%
            "own quarters: two quarters of 2.4 times the rest are no growth in the newer half and no collapse in the older (" + ListingRules.GrowthLog(twoBigNew, 8, 4).ToString("F3") + ", " + ListingRules.GrowthLog(twoBig, 8, 4).ToString("F3") + ")");
        double[] partial = { 300, 300, 300, 300, 300, 300, 220, 300 };
        Check(Math.Abs(ListingRules.GrowthLog(partial, 8, 4)) < 0.001, "own quarters: a part quarter after a listing is not read as growth");
        Check(ListingRules.GrowthLog(steady, 6, 4) == 0.0 && ListingRules.GrowthLog(new double[8], 8, 4) == 0.0, "own quarters: too few quarters, or none with takings, give no reading");
        // the old reading of the player's next results, for the record: the four after against the four before
        double oldNext = Math.Log((300 + 280 + 296 + 331) / (637 + 708 + 292 + 294.0));
        Check(oldNext < -0.4 && Math.Abs(ListingRules.GrowthLog(new double[] { 300, 280, 296, 331, 637, 708, 292, 294 }, 8, 4)) < 0.03,
            "own quarters: the results after the player's last would have read " + oldNext.ToString("F2") + " (a growth cut to the floor and a second fall of most of the price), and now read nothing");
    }

    private sealed class QuarterRun
    {
        public readonly List<int> ResultDays = new List<int>();
        public readonly List<double> ResultMoves = new List<double>(), GrowthSteps = new List<double>();
        public double WorstDay;          // the largest one day move of the price on any market day
        public double WorstResults;      // ...and on a results day
        public double Low = double.MaxValue, High;   // the price's lowest and highest close, cents
        public long BiggestQuarter;      // the largest quarter any results table held during the run, thousands
        public long[] Quarters;          // each results' takings, thousands (the table at the end)
        public int[] QuarterNumbers;
    }

    // A company with takings and profit a day by the day, played for years; a run records what its results did to the price. Step: market days between the checks' looks.
    private static QuarterRun OwnQuarterPlay(ulong seed, int years, Func<int, double> revenue, Func<int, double> profit, int step = 1)
    {
        var world = new OwnWorld(seed) { Revenue = 20000000, Profit = 9000000, RevenueOn = revenue, ProfitOn = profit };
        MarketEngine m = LListed(seed, world, 4000, out Stock own);
        var run = new QuarterRun();
        if (own == null)
        {
            return run;
        }
        int start = m.State.Day, end = start + years * LYear, prev = own.Last, boost = own.ResultsBoostDay;
        double growth = own.Growth;
        for (int d = start + 1; d <= end; d += step)
        {
            int day = d;
            while (!MarketCatalogue.TradingDay(day))
            {
                day++;
            }
            RunTo(m, day);
            Stock s = m.MineListed();
            if (s == null)
            {
                break;
            }
            double move = prev > 0 ? (s.Last - prev) / (double)prev : 0.0;
            run.WorstDay = Math.Max(run.WorstDay, Math.Abs(move));
            run.Low = Math.Min(run.Low, s.Last);
            run.High = Math.Max(run.High, s.Last);
            if (s.ResultsBoostDay != boost)
            {
                boost = s.ResultsBoostDay;
                for (int i = 0; i < s.Results.Count; i++)
                {
                    run.BiggestQuarter = Math.Max(run.BiggestQuarter, s.Results[i].RevenueK);
                }
                if (step == 1)
                {
                    run.ResultDays.Add(day);
                    run.ResultMoves.Add(move);
                    run.WorstResults = Math.Max(run.WorstResults, Math.Abs(move));
                }
                run.GrowthSteps.Add(Math.Abs(s.Growth - growth));
            }
            growth = s.Growth;
            prev = s.Last;
        }
        Stock last = m.MineListed();
        if (last != null)
        {
            run.Quarters = new long[last.Results.Count];
            run.QuarterNumbers = new int[last.Results.Count];
            for (int i = 0; i < last.Results.Count; i++)
            {
                run.Quarters[i] = last.Results[i].RevenueK;
                run.QuarterNumbers[i] = last.Results[i].Quarter;
            }
        }
        return run;
    }

    // Steady real takings: every whole quarter is the same figure, never doubled, through weekends, the turns of the year and steps of many days between the market's looks.
    private static void OwnQuarterSteadyChecks()
    {
        bool whole = true, ordered = true, calm = true;
        double worstResults = 0.0, steps = 0.0;
        string said = string.Empty;
        foreach (int step in new[] { 1, 4, 11 })
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                QuarterRun run = OwnQuarterPlay(seed, 10, null, null, step);
                if (run.Quarters == null || run.Quarters.Length < MarketCatalogue.QuartersKept)
                {
                    whole = false;
                    said += " (too few quarters)";
                    continue;
                }
                for (int i = 1; i < run.Quarters.Length; i++)
                {
                    whole &= run.Quarters[i] == 300000;
                    ordered &= run.QuarterNumbers[i] == run.QuarterNumbers[i - 1] + 1;
                    if (run.Quarters[i] != 300000 && said.Length < 120)
                    {
                        said += " q" + run.QuarterNumbers[i] + "=" + run.Quarters[i] + "K";
                    }
                }
                worstResults = Math.Max(worstResults, run.WorstResults);
                foreach (double s in run.GrowthSteps)
                {
                    steps = Math.Max(steps, s);
                }
                calm &= run.High < run.Low * 12.0;
            }
        }
        Check(whole, "own quarters: with steady real takings of 20 million a day every quarter of ten years reads 300000K whatever the steps between the market's looks (a quarter that counted days twice would show 600000K)" + said);
        Check(ordered, "own quarters: the quarters follow one another, none counted twice and none left out");
        Check(steps <= ListingCatalogue.GrowthStepMost + 1e-9, "own quarters: with steady takings the growth the market believes moves by at most " + ListingCatalogue.GrowthStepMost + " at a results (" + steps.ToString("F4") + ")");
        Check(worstResults < 0.35, "own quarters: with steady takings no results day moves the price by a third (" + (worstResults * 100).ToString("F0") + "% at the most)");
        Check(calm, "own quarters: with steady takings the price stays within twelve times from its lowest close to its highest over ten years");
    }

    // The player's story made on purpose: steady takings, two quarters at 2.4 times, then steady again. Played from eight seeds.
    private static void OwnQuarterBurstChecks()
    {
        double worstResults = 0.0, worstSteps = 0.0, worstDay = 0.0, worstRange = 0.0;
        bool shown = true;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            int first = MarketCatalogue.QuarterOf(TradingOnOrAfter(200), LYear) + 9;
            Func<int, double> takings = d =>
            {
                int q = MarketCatalogue.QuarterOf(d, LYear);
                return q == first || q == first + 1 ? 48000000 : 20000000;
            };
            Func<int, double> profit = d => takings(d) > 30000000 ? 25000000 : 9000000;
            QuarterRun run = OwnQuarterPlay(seed, 9, takings, profit);
            shown &= run.BiggestQuarter > 600000;
            worstResults = Math.Max(worstResults, run.WorstResults);
            worstDay = Math.Max(worstDay, run.WorstDay);
            foreach (double s in run.GrowthSteps)
            {
                worstSteps = Math.Max(worstSteps, s);
            }
            worstRange = Math.Max(worstRange, run.High / Math.Max(1.0, run.Low));
        }
        Check(shown, "own quarters: the results tables did hold the big quarters during the run (the check would be empty otherwise)");
        Check(worstSteps <= ListingCatalogue.GrowthStepMost + 1e-9, "own quarters: two big quarters never move the growth the market believes by more than " + ListingCatalogue.GrowthStepMost + " at a results (" + worstSteps.ToString("F4") + ")");
        Check(worstResults < 0.35, "own quarters: no results day, with two quarters of 2.4 times the usual coming and going, moves the price by a third (" + (worstResults * 100).ToString("F0") + "% at the most; it was 124%, 66% and 73% before)");
        Check(worstDay < 0.5, "own quarters: no market day of those years moves the price by half (" + (worstDay * 100).ToString("F0") + "% at the most)");
        Check(worstRange < 12.0, "own quarters: two big quarters lift the price for a while but never twelvefold from its lowest close (" + worstRange.ToString("F1") + " times)");
        // played twice: the same days, the same prices
        Func<int, double> some = d => MarketCatalogue.QuarterOf(d, LYear) % 6 == 3 ? 48000000 : 20000000;
        QuarterRun a = OwnQuarterPlay(5, 4, some, null), b = OwnQuarterPlay(5, 4, some, null);
        bool same = a.ResultMoves.Count == b.ResultMoves.Count && a.High == b.High && a.Low == b.Low;
        for (int i = 0; same && i < a.ResultMoves.Count; i++)
        {
            same &= a.ResultMoves[i] == b.ResultMoves[i] && a.ResultDays[i] == b.ResultDays[i];
        }
        Check(same && a.ResultMoves.Count > 0, "own quarters: the same market played twice moves the same on every results day");
    }

    // The quarters of the player's table in order (takings in millions, from their Y11 Q3 on) and then steady, from eight quarters after the listing.
    private static void OwnQuarterPlayersChecks()
    {
        double[] table = { 280, 302, 294, 292, 708, 637, 331, 296, 280 };
        int firstQuarter = MarketCatalogue.QuarterOf(TradingOnOrAfter(200), LYear) + 8;
        Func<int, double> takings = d =>
        {
            int i = MarketCatalogue.QuarterOf(d, LYear) - firstQuarter;
            return (i >= 0 && i < table.Length ? table[i] : 290) * 1e6 / 15.0;
        };
        QuarterRun run = OwnQuarterPlay(21, 6, takings, d => takings(d) * 0.47);
        double steps = 0.0;
        foreach (double s in run.GrowthSteps)
        {
            steps = Math.Max(steps, s);
        }
        Check(run.ResultDays.Count >= 20 && run.WorstResults < 0.35 && steps <= ListingCatalogue.GrowthStepMost + 1e-9,
            "own quarters (the player's table): over " + run.ResultDays.Count + " results with the two big quarters in the middle no results day moves the price by a third (" + (run.WorstResults * 100).ToString("F0") + "%) and the growth moves by at most " + steps.ToString("F3"));
    }

    // The listing's own reading of eight quarters of books (ListingRules.Books) is held to the same rule: a one-off quarter before the market is not growth in its first price.
    private static void OwnQuarterListingChecks()
    {
        var f = new ListingFile();
        int day = 15 * 12 + 1;
        int now = MarketCatalogue.QuarterOf(day, LYear);
        for (int d = MarketCatalogue.QuarterStart(now - 8, LYear); d < day; d++)
        {
            int q = MarketCatalogue.QuarterOf(d, LYear);
            bool big = q == now - 3 || q == now - 2;
            ListingBook.AddDay(f, d, big ? 2400 : 1000, 100, LYear);
        }
        ListingRules.Books(f, day, LYear, out double rY, out double nY, out double rQ, out double nQ, out double growth);
        Check(Math.Abs(growth) < 0.03 && rY > 1000, "own quarters: two big quarters in the books before the listing give no growth (" + growth.ToString("F3") + ") though the year's takings are counted whole (" + rY.ToString("F0") + " a day)");
    }

    private static void OwnQuarterSources()
    {
        string rules = ModSource(@"Markets\Listing\ListingRules.cs"), catalogue = ModSource(@"Markets\Listing\ListingCatalogue.cs");
        Check(rules.Contains("GrowthLog(quarters, 8, 4)") && rules.Contains("ListingCatalogue.GrowthStepMost") && !rules.Contains("Math.Log(recent / (double)earlier)"),
            "own quarters: the results read growth through GrowthLog and move the belief a step at a time, never from the raw sums of the two halves");
        Check(rules.Contains("GrowthLog(Rates(rev, days, 8), 8, 4)") && catalogue.Contains("QuarterSeenMost") && catalogue.Contains("GrowthStepMost"), "own quarters: the listing's books read growth the same way");
        Check(!rules.Contains("System.Random") && !rules.Contains("DateTime"), "own quarters: the reading is a pure function of the quarters, the same on every machine");
        Dictionary<string, string> en = LocaleFile("en");
        Check(WhatsNewHas(en, "Fixed: the price of your own company no longer falls or rises by most of its value in a day when its results come out after a quarter far from the others"),
            "own quarters: What's new tells of the fix");
        string mood = en.TryGetValue("reallife_own_fall_why_mood", out string m) ? m : string.Empty, plain = en.TryGetValue("reallife_own_fall_why_mood_plain", out string p) ? p : string.Empty;
        Check(!mood.Contains("not your books") && !plain.Contains("not your books") && mood.Contains("growth") && plain.Contains("growth"), "own quarters: a fall put down to the mood no longer says it is not the books, as what the market expects of the growth is part of it");
    }
}
