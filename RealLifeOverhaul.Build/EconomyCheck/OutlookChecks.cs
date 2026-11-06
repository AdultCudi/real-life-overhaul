using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Outlook;

// The Economy app (Scripts/Outlook): the daily history it keeps and its line in the save (bounded, an old save empty, a later
// version's line read as far as it can be, a damaged one refused), a figure's change over days and how it reads, the one-word
// verdict, the districts' ranking, each piece of advice by its rule from the readings and its sentence written from the figures,
// the charts' scaling and smoothing for any series at all (flat, a single point, negative, huge, none), and the colours' contrast.
internal static partial class Program
{
    private static void OutlookApp()
    {
        OutlookBookChecks();
        OutlookStoreChecks();
        OutlookRuleChecks();
        OutlookInsightChecks();
        OutlookScaleChecks();
        OutlookThemeChecks();
        OutlookPlaceChecks();
        OutlookTexts();
        OutlookSources();
    }

    // ---------------------------------------------------------------- the book

    private static void OutlookBookChecks()
    {
        var book = new OutlookBook();
        Check(book.LastDay == 0 && book.Lines == 0 && !book.Get("prices", 1, out _), "outlook: an empty book has no days and no figures");
        Check(!book.Set(null, 5, 1) && !book.Set("", 5, 1) && !book.Set("prices", 0, 1) && !book.Set("prices", -3, 1) && !book.Set("prices", 5, OutlookCatalogue.NoValue) && book.Lines == 0,
            "outlook: no name, a day under 1 and the no-value mark are refused, and make no line");

        // Bounded: 300 days of one figure keep the last 120.
        for (int day = 1; day <= 300; day++)
        {
            book.Set("prices", day, 100000 + day);
        }
        bool window = true;
        for (int day = 181; day <= 300; day++)
        {
            window &= book.Get("prices", day, out int v) && v == 100000 + day;
        }
        Check(window && book.LastDay == 300, "outlook: the last 120 days are kept, each as written (300 days written)");
        Check(!book.Get("prices", 180, out _) && !book.Get("prices", 1, out _) && !book.Get("prices", 301, out _), "outlook: a day older than the window is gone, a day not yet come is none");
        Check(!book.Set("prices", 180, 5) && !book.Set("prices", 100, 5), "outlook: a day the window has passed cannot be written");
        Check(book.Set("prices", 250, 7) && book.Get("prices", 250, out int again) && again == 7, "outlook: a day in the window can be written over");

        // The line count is bounded.
        var full = new OutlookBook();
        for (int k = 0; k < OutlookCatalogue.LinesMost + 8; k++)
        {
            full.Set("figure" + k, 10, k);
        }
        Check(full.Lines == OutlookCatalogue.LinesMost && full.Refused == 8 && !full.Has("figure" + OutlookCatalogue.LinesMost) && full.Get("figure0", 10, out _),
            "outlook: the book keeps " + OutlookCatalogue.LinesMost + " figures at most, and counts the ones it refused");

        // A window with gaps, and the newest reading.
        var gaps = new OutlookBook();
        gaps.Set("income", 10, 1000);
        gaps.Set("income", 12, 1010);
        gaps.Set("income", 15, 990);
        var into = new int[8];
        int found = gaps.Window("income", 15, 8, into);
        Check(found == 3 && into[7] == 990 && into[5] == OutlookCatalogue.NoValue && into[4] == 1010 && into[2] == 1000 && into[1] == OutlookCatalogue.NoValue && into[0] == OutlookCatalogue.NoValue,
            "outlook: a window of 8 days ending on day 15 has the three readings in place and the no-value mark for the days between");
        Check(gaps.Newest("income", out int newestDay, out int newest) && newestDay == 15 && newest == 990 && !gaps.Newest("nothing", out _, out _), "outlook: the newest reading of a figure, none for a figure not kept");
        Check(gaps.Days("income", 15, 8) == 3 && gaps.Window("income", 15, 8, null) == 0, "outlook: days with a reading counted; a missing array is none");

        // A day older than the window by a long way does not wipe the book (it is refused), and a far day moves the window.
        var jump = new OutlookBook();
        jump.Set("a", 100, 1);
        jump.Set("a", 5000, 2);
        Check(jump.LastDay == 5000 && !jump.Get("a", 100, out _) && jump.Get("a", 5000, out int far) && far == 2, "outlook: a far later day moves the window and the old reading is gone");

        // The extremes keep their value.
        var extreme = new OutlookBook();
        extreme.Set("x", 3, int.MaxValue);
        extreme.Set("x", 4, int.MinValue + 1);
        Check(extreme.Get("x", 3, out int hi) && hi == int.MaxValue && extreme.Get("x", 4, out int lo) && lo == int.MinValue + 1, "outlook: the largest and smallest numbers are kept whole");
    }

    // ---------------------------------------------------------------- the line in the save

    private static OutlookBook OutlookSample(int seed, int days, int figures)
    {
        var random = new Random(seed);
        var book = new OutlookBook();
        var value = new int[figures];
        for (int f = 0; f < figures; f++)
        {
            value[f] = random.Next(-5000, 200000);
        }
        for (int day = 1; day <= days; day++)
        {
            for (int f = 0; f < figures; f++)
            {
                if (random.Next(7) == 0 && day % 5 != 0)
                {
                    continue;   // a figure that changes in steps, and a day without a reading now and then
                }
                if (day % 5 == 0)
                {
                    value[f] += random.Next(-300, 400);
                }
                if (random.Next(400) == 0)
                {
                    value[f] = random.Next(2) == 0 ? int.MaxValue - random.Next(5) : int.MinValue + 1 + random.Next(5);   // an extreme now and then
                }
                book.Set(f == 0 ? "prices" : "district:" + f, day, value[f]);
            }
        }
        return book;
    }

    private static bool OutlookSame(OutlookBook a, OutlookBook b)
    {
        if (a.LastDay != b.LastDay || a.Lines != b.Lines)
        {
            return false;
        }
        foreach (string name in a.Names())
        {
            for (int day = Math.Max(1, a.LastDay - OutlookCatalogue.HistoryDays + 1); day <= a.LastDay; day++)
            {
                bool x = a.Get(name, day, out int va), y = b.Get(name, day, out int vb);
                if (x != y || (x && va != vb))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void OutlookStoreChecks()
    {
        bool trips = true;
        string first = null;
        for (int seed = 1; seed <= 60; seed++)
        {
            OutlookBook book = OutlookSample(seed, 40 + seed * 7, 1 + seed % 14);
            string line = OutlookStore.Write(book);
            OutlookBook back = OutlookStore.Read(line, out int version);
            bool ok = back != null && version == OutlookCatalogue.Version && OutlookSame(book, back) && OutlookStore.Write(back) == line;
            trips &= ok;
            first = ok || first != null ? first : "seed " + seed;
        }
        Check(trips, "outlook: the line reads back to the same book, whole, for 60 books (steps, gaps, extremes), and writes the same line again" + (first != null ? " (first: " + first + ")" : string.Empty));

        // A book of the size the game keeps: nine districts' incomes, their market, the national figures, the whole window, from the real model.
        OutlookBook sim = OutlookFromModel(7, 240);
        string simLine = OutlookStore.Write(sim);
        OutlookBook simBack = OutlookStore.Read(simLine, out _);
        Check(simBack != null && OutlookSame(sim, simBack) && simLine.Length < 14000, "outlook: a year of the city's model with nine districts is a line of " + simLine.Length + " characters (under 14,000) and reads back whole");

        // An old save has no line: nothing to read, and nothing damaged either.
        var data = new Dictionary<string, string>();
        Check(OutlookStore.Load(data, out int none) == null && none == 0 && !SaveLines.IsKept(data, OutlookCatalogue.Key), "outlook: an old save with no line starts an empty book and keeps nothing");
        data[OutlookCatalogue.Key] = OutlookStore.Write(OutlookSample(3, 50, 4));
        OutlookBook loaded = OutlookStore.Load(data, out int loadedVersion);
        Check(loaded != null && loadedVersion == 1 && loaded.LastDay == 50, "outlook: a line of this version is loaded");

        // A line of a later version: read as far as it goes, and what it adds is skipped.
        OutlookBook later = OutlookStore.Read("v=2~l:12~s:prices|5|=100,1,,n,=7;future|1|=1,2~x:whatever~z", out int laterVersion);
        Check(later != null && laterVersion == 2 && later.Get("prices", 5, out int p5) && p5 == 100 && later.Get("prices", 6, out int p6) && p6 == 101 && later.Get("prices", 7, out int p7) && p7 == 101
              && !later.Get("prices", 8, out _) && later.Get("prices", 9, out int p9) && p9 == 7 && later.Get("future", 2, out int f2) && f2 == 3,
            "outlook: a later version's line is read as far as this one knows it (changes, repeats, gaps, a figure it adds, a part it does not know)");

        // Damaged lines are refused, never half read.
        string[] damaged =
        {
            null, "", "hello", "v=", "v=0~l:5~s:", "v=x~l:5", "v=1", "v=1~l:abc~s:prices|1|=1", "v=1~l:5~s:prices|9|1", "v=1~l:5~s:prices|b|=1", "v=1~l:5~s:prices|0|=1",
            "v=1~l:5~s:prices|1|=x", "v=1~l:5~s:prices|1|=-2147483648", "v=1~l:5~s:prices|1|=1,2147483647", "v=1~l:99999999~s:prices|1|=1", "v=1~l:5~s:|1|=1", "v=1~l:5~s:prices|1",
            "v=1~l:5~s:prices|1|=1,n,3", "v=1~l:5~s:prices|1|" + new string(',', 200)
        };
        bool refused = true;
        string leaked = null;
        foreach (string text in damaged)
        {
            bool nothing = OutlookStore.Read(text, out _) == null;
            refused &= nothing;
            leaked = nothing || leaked != null ? leaked : "'" + text + "'";
        }
        Check(refused, "outlook: a damaged line (no header, a change with nothing before it, a day or number that is not one, too many days) is refused" + (leaked != null ? ", not " + leaked : string.Empty));

        // A line the store cannot read is kept as it was in the save, and a later one is only said.
        var keep = new Dictionary<string, string> { { OutlookCatalogue.Key, "v=1~l:abc~s:prices|1|=1" } };
        Check(OutlookStore.Load(keep, out _) == null && SaveLines.IsKept(keep, OutlookCatalogue.Key), "outlook: a line that cannot be read is kept as it was");
        SaveLines.Forget();

        // The names are cleaned so a district's id never breaks the line apart.
        var odd = new OutlookBook();
        odd.Set(OutlookCatalogue.OfDistrictIncome("a|b;c~d,e=f"), 3, 5);
        odd.Set(OutlookCatalogue.OfDistrictDemand("ba:neighborhood_midtown", 2), 3, 9);
        OutlookBook oddBack = OutlookStore.Read(OutlookStore.Write(odd), out _);
        Check(oddBack != null && OutlookSame(odd, oddBack) && oddBack.Get(OutlookCatalogue.OfDistrictIncome("a|b;c~d,e=f"), 3, out int fiveBack) && fiveBack == 5,
            "outlook: a district's id with the line's dividers in it is cleaned before it is a name, and still finds its figure");

        // A figure with nothing in the window is not written.
        var stale = new OutlookBook();
        stale.Set("old", 5, 1);
        stale.Set("now", 500, 2);
        string staleLine = OutlookStore.Write(stale);
        Check(!staleLine.Contains("old|") && staleLine.Contains("now|"), "outlook: a figure whose last reading has left the window is not written");
    }

    // The city's own model run for some days from a seed, nine districts, recorded into a book as the game's day does.
    private static OutlookBook OutlookFromModel(int seed, int days)
    {
        CityState state = CityModel.Open(seed, 1);
        string[] names = { "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity", "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown",
            "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons", "reallife_neighborhood_uptown", "reallife_neighborhood_cedarheights" };
        for (int i = 0; i < names.Length; i++)
        {
            CityModel.Know(state, names[i], CityModel.StartIncome(30 + i * 4, 50, 20 - i));
        }
        var book = new OutlookBook();
        for (int day = 2; day <= days; day++)
        {
            CityModel.Day(state, day, new CityDay(), MacroCatalogue.Standard, 5);
            book.Set(OutlookCatalogue.Prices, day, state.Index);
            book.Set(OutlookCatalogue.Inflation, day, state.Inflation);
            book.Set(OutlookCatalogue.Rate, day, state.Base);
            book.Set(OutlookCatalogue.Cycle, day, (int)state.Cycle);
            book.Set(OutlookCatalogue.Spend, day, CityModel.Spend(state.Cycle, MacroCatalogue.Standard));
            book.Set(OutlookCatalogue.Footfall, day, CityModel.CycleFootfall(state.Cycle, MacroCatalogue.Standard));
            book.Set(OutlookCatalogue.Unemployment, day, 35 + (int)state.Cycle * 20);
            long sum = 0;
            for (int d = 0; d < state.Districts.Count; d++)
            {
                book.Set(OutlookCatalogue.OfDistrictIncome(state.Districts[d].Name), day, state.Districts[d].Income);
                sum += state.Districts[d].Income;
            }
            book.Set(OutlookCatalogue.Income, day, (int)(sum / state.Districts.Count));
            book.Set(OutlookCatalogue.OfDistrictLevel(names[7]), day, 950 + day % 40);
            for (int use = 0; use < 4; use++)
            {
                book.Set(OutlookCatalogue.OfDistrictDemand(names[7], use), day, 40 + (day + use * 9) % 50);
            }
            book.Set(OutlookCatalogue.Reserve, day, 40000 + day * 13);
        }
        return book;
    }

    // ---------------------------------------------------------------- changes, the verdict, the ranking

    private static void OutlookRuleChecks()
    {
        var window = new int[OutlookCatalogue.HistoryDays];
        for (int i = 0; i < window.Length; i++)
        {
            window[i] = 100000 + i * 100;   // a rise of 100 a day
        }
        int now = window[window.Length - 1];
        OutlookDelta week = OutlookRules.Over(window, window.Length, 7, now, OutlookChange.Percent);
        OutlookDelta month = OutlookRules.Over(window, window.Length, 30, now, OutlookChange.Points);
        Check(week.Known && week.Value == (int)((long)(now - window[window.Length - 8]) * 10000L / window[window.Length - 8]) && month.Known && month.Value == 3000,
            "outlook: a change over 7 days is a per cent in basis points, over 30 days in the figure's own points (" + week.Value + " and " + month.Value + ")");
        window[window.Length - 8] = OutlookCatalogue.NoValue;
        OutlookDelta neighbour = OutlookRules.Over(window, window.Length, 7, now, OutlookChange.Points);
        Check(neighbour.Known && neighbour.Value == now - window[window.Length - 9], "outlook: a day with no reading is made up by the day before it (" + neighbour.Value + ")");
        window[window.Length - 9] = OutlookCatalogue.NoValue;
        window[window.Length - 7] = OutlookCatalogue.NoValue;
        Check(!OutlookRules.Over(window, window.Length, 7, now, OutlookChange.Points).Known, "outlook: with no reading within a day of it, a change is not known");
        Check(!OutlookRules.Over(null, 5, 7, 1, OutlookChange.Points).Known && !OutlookRules.Over(new int[3], 3, 7, 1, OutlookChange.Points).Known && !OutlookRules.Over(new int[10], 10, 0, 1, OutlookChange.Points).Known
              && !OutlookRules.Over(new int[10], 1, 3, 1, OutlookChange.Points).Known && !OutlookRules.Over(new int[10], 10, 3, OutlookCatalogue.NoValue, OutlookChange.Points).Known,
            "outlook: too few days, no days, no window and no reading now are no change");
        Check(!OutlookRules.Between(0, 5, OutlookChange.Percent).Known && OutlookRules.Between(0, 5, OutlookChange.Points).Value == 5 && OutlookRules.Between(-100, -50, OutlookChange.Percent).Value == 5000
              && OutlookRules.Between(int.MinValue + 1, int.MaxValue, OutlookChange.Points).Known && OutlookRules.Between(1, int.MaxValue, OutlookChange.Percent).Value == 1000000,
            "outlook: a per cent of nothing is unknown; a reserve below zero that shrinks reads as a rise; the extremes are held, never wrapped");

        Check(OutlookRules.OutcomeOf(OutlookDelta.None, 1) == OutlookTone.Flat && OutlookRules.OutcomeOf(OutlookDelta.Of(0), -1) == OutlookTone.Flat && OutlookRules.OutcomeOf(OutlookDelta.Of(5), 1) == OutlookTone.Good
              && OutlookRules.OutcomeOf(OutlookDelta.Of(-5), 1) == OutlookTone.Bad && OutlookRules.OutcomeOf(OutlookDelta.Of(5), -1) == OutlookTone.Bad && OutlookRules.OutcomeOf(OutlookDelta.Of(-5), -1) == OutlookTone.Good
              && OutlookRules.OutcomeOf(OutlookDelta.Of(5), 0) == OutlookTone.Neutral,
            "outlook: a rise is good where a rise is good and bad where it is bad, nothing is flat, and a figure that is neither is neutral");

        // The verdict: every word is reachable, and each boundary is where the catalogue says.
        OutlookDelta none = OutlookDelta.None;
        int target = MacroCatalogue.InflationTarget;
        Check(OutlookRules.Judge(3, target, none, none) == OutlookVerdict.Recession && OutlookRules.Judge(3, 900, OutlookDelta.Of(100), OutlookDelta.Of(9000)) == OutlookVerdict.Recession
              && OutlookRules.Judge(2, target, none, none) == OutlookVerdict.Slowing && OutlookRules.Judge(2, 900, none, none) == OutlookVerdict.Slowing,
            "outlook: a recession and a slowdown are the verdict whatever else is read");
        Check(OutlookRules.Judge(0, target, none, none) == OutlookVerdict.Growing && OutlookRules.Judge(0, target + OutlookCatalogue.HotGap, none, none) == OutlookVerdict.Overheating
              && OutlookRules.Judge(0, target + OutlookCatalogue.HotGap - 1, none, none) == OutlookVerdict.Growing && OutlookRules.Judge(1, target + OutlookCatalogue.HotGap, none, none) == OutlookVerdict.Overheating
              && OutlookRules.Judge(1, target + OutlookCatalogue.HotGap - 1, none, none) == OutlookVerdict.Steady,
            "outlook: a boom is growing and prices 1.5 points over the target make a boom or a steady economy overheating, 1.49 do not");
        Check(OutlookRules.Judge(1, target, OutlookDelta.Of(OutlookCatalogue.IncomeFalls), none) == OutlookVerdict.Slowing && OutlookRules.Judge(1, target, OutlookDelta.Of(OutlookCatalogue.IncomeFalls + 1), none) == OutlookVerdict.Steady
              && OutlookRules.Judge(1, target, none, OutlookDelta.Of(OutlookCatalogue.StocksFall)) == OutlookVerdict.Slowing && OutlookRules.Judge(1, target, OutlookDelta.Of(OutlookCatalogue.IncomeRises), none) == OutlookVerdict.Growing
              && OutlookRules.Judge(1, target, none, OutlookDelta.Of(OutlookCatalogue.StocksRise)) == OutlookVerdict.Growing && OutlookRules.Judge(1, target, OutlookDelta.Of(OutlookCatalogue.IncomeFalls), OutlookDelta.Of(OutlookCatalogue.StocksRise)) == OutlookVerdict.Steady,
            "outlook: a steady economy is slowing when incomes or shares fell over the long window, growing when they rose, steady when they disagree or are not known");
        var seen = new HashSet<OutlookVerdict>();
        for (int c = 0; c < 4; c++)
        {
            foreach (int inflation in new[] { 100, 200, 500 })
            {
                seen.Add(OutlookRules.Judge(c, inflation, none, none));
            }
        }
        Check(seen.Count == 5, "outlook: all five words of the verdict come out of some reading (" + seen.Count + ")");
        Check(OutlookRules.OutcomeOf(OutlookVerdict.Growing) == OutlookTone.Good && OutlookRules.OutcomeOf(OutlookVerdict.Steady) == OutlookTone.Neutral && OutlookRules.OutcomeOf(OutlookVerdict.Recession) == OutlookTone.Bad && OutlookRules.OutcomeOf(OutlookVerdict.Overheating) == OutlookTone.Bad
              && OutlookRules.VerdictKeys.Length == 5 && OutlookRules.VerdictLineKeys.Length == 5, "outlook: each verdict has a tone, a word and a line");

        // The property formula the app asks of days gone by is the city's own.
        var rng = new Random(11);
        bool worth = true;
        for (int n = 0; n < 4000; n++)
        {
            var s = new CityState { Cycle = (CycleState)rng.Next(4), Index = rng.Next(MacroCatalogue.IndexLeast, MacroCatalogue.IndexMost) };
            var d = new CityDistrict { Name = "d", Start = rng.Next(600, 1401), Income = rng.Next(600, 1401), Before = 1000 };
            int level = rng.Next(4), marketLevel = rng.Next(3) == 0 ? 0 : rng.Next(880, 1121);
            int own = CityModel.Property(s, d, level);
            int mine = OutlookRules.Worth((int)s.Cycle, s.Index, d.Income, d.Start, level, marketLevel);
            worth &= mine == (marketLevel > 0 ? (int)((long)own * marketLevel / 1000L) : own);
            worth &= own == CityModel.Property(s.Cycle, s.Index, d.Income, d.Start, level);
            worth &= CityModel.Property(s, null, level) == (int)((1000L + CityModel.Swung(MacroCatalogue.StateProperty[(int)s.Cycle] - 1000, level)) * s.Index / MacroCatalogue.IndexStart);
        }
        Check(worth, "outlook: what a property is worth for a state given as numbers is what the city works out for its district, with the market level on it");

        // The ranking: pull on customers, 100 the mean; the same order whatever the order of the list.
        var readings = new OutlookReadings();
        string[] ids = { "b", "a", "c", "d" };
        int[] weights = { 1000, 1000, 450, 1300 };
        int[] incomes = { 1000, 1100, 1000, 900 };
        readings.Begin();
        for (int i = 0; i < ids.Length; i++)
        {
            OutlookDistrict d = readings.NextDistrict();
            d.Id = ids[i];
            d.Weight = weights[i];
            d.Income = incomes[i];
            d.Footfall = (incomes[i] - 1000) / 100;
        }
        readings.Done();
        readings.Footfall = 0;
        OutlookRules.Rank(readings);
        var order = new int[8];
        int count = OutlookRules.Order(readings, order);
        string ranked = string.Empty;
        for (int i = 0; i < count; i++)
        {
            ranked += readings.Districts[order[i]].Id;
        }
        long mean = 0;
        foreach (OutlookDistrict d in readings.Districts)
        {
            mean += d.Pull;
        }
        Check(count == 4 && ranked == "dabc", "outlook: the districts are ranked by the game's weight of their customers and what their income adds (" + ranked + ", not dabc)");
        Check(Math.Abs(mean / 4.0 - 100.0) < 2.0, "outlook: the pull of the districts averages 100 (" + mean / 4.0 + ")");
        readings.Districts.Reverse();
        OutlookRules.Rank(readings);
        string again = string.Empty;
        count = OutlookRules.Order(readings, order);
        for (int i = 0; i < count; i++)
        {
            again += readings.Districts[order[i]].Id;
        }
        Check(again == ranked, "outlook: the ranking does not depend on the order of the list (" + ranked + " and " + again + ")");
        var tied = new OutlookReadings();
        tied.Begin();
        foreach (string id in new[] { "z", "m", "a" })
        {
            OutlookDistrict d = tied.NextDistrict();
            d.Id = id;
        }
        tied.Done();
        OutlookRules.Rank(tied);
        Check(tied.Districts[2].Rank == 1 && tied.Districts[1].Rank == 2 && tied.Districts[0].Rank == 3, "outlook: districts alike in every way stand in the order of their ids");
        var empty = new OutlookReadings();
        OutlookRules.Rank(empty);
        Check(OutlookRules.Order(empty, order) == 0 && OutlookRules.MeanIncome(empty) == MacroCatalogue.IncomeMiddle && OutlookRules.MeanWorth(empty) == 1000, "outlook: no districts rank nothing and average the middle");

        // The trends from a book: a steady rise, with the model's own memory for the first month.
        OutlookBook book = new OutlookBook();
        for (int day = 1; day <= 100; day++)
        {
            book.Set(OutlookCatalogue.Prices, day, 100000 + day * 100);
            book.Set(OutlookCatalogue.Inflation, day, 200);
            book.Set(OutlookCatalogue.OfDistrictIncome("x"), day, 1000 + day);
        }
        var r = new OutlookReadings();
        r.Begin();
        r.CityOn = true;
        r.Day = 100;
        r.Prices = 100000 + 100 * 100;
        r.Inflation = 200;
        OutlookDistrict dx = r.NextDistrict();
        dx.Id = "x";
        dx.Income = 1100;
        dx.Before = 1000;
        r.Done();
        var scratch = new int[OutlookCatalogue.HistoryDays];
        OutlookRules.Trends(book, r, scratch);
        Check(r.Short[OutlookCatalogue.TilePrices].Known && r.Short[OutlookCatalogue.TilePrices].Value == 10000 * 700 / (100000 + 93 * 100) && r.Long[OutlookCatalogue.TilePrices].Known
              && r.Short[OutlookCatalogue.TileInflation].Known && r.Short[OutlookCatalogue.TileInflation].Value == 0 && !r.Short[OutlookCatalogue.TileRate].Known,
            "outlook: tiles take their changes from the book (prices up " + r.Short[OutlookCatalogue.TilePrices].Value + " basis points in 7 days), none for a figure the book lacks");
        Check(dx.IncomeLong.Known && dx.IncomeLong.Value == 30, "outlook: a district's income change over 30 days is from the book (" + dx.IncomeLong.Value + ")");
        var fresh = new OutlookBook();
        r.Begin();
        r.CityOn = true;
        r.Day = 10;
        r.Prices = 103000;
        r.MonthChange = 120;
        OutlookDistrict dy = r.NextDistrict();
        dy.Id = "y";
        dy.Income = 1043;
        dy.Before = 1000;
        r.Done();
        OutlookRules.Trends(fresh, r, scratch);
        Check(r.Long[OutlookCatalogue.TilePrices].Known && r.Long[OutlookCatalogue.TilePrices].Value == 120 && dy.IncomeLong.Known && dy.IncomeLong.Value == 43 && !r.Short[OutlookCatalogue.TilePrices].Known,
            "outlook: on a new book the month's change the model remembers stands in for the long change of prices and of a district's income, and the short ones wait");
    }

    // ---------------------------------------------------------------- the advice

    private static OutlookReadings OutlookQuiet()
    {
        var r = new OutlookReadings();
        r.Begin();
        r.Day = 50;
        r.CityOn = true;
        r.JobsOn = true;
        r.Cycle = (int)CycleState.Steady;
        r.Level = MacroCatalogue.Standard;
        r.Prices = 100000;
        r.Inflation = MacroCatalogue.InflationTarget;
        r.RateBp = 300;
        r.Unemployment = 50;
        r.Vacancies = 14;
        r.Spend = 1000;
        r.Footfall = 0;
        r.Costs = 1000;
        r.Takings = 1000;
        r.Property = 1000;
        for (int i = 0; i < 3; i++)
        {
            OutlookDistrict d = r.NextDistrict();
            d.Id = "d" + i;
            d.Income = d.Start = 1000;
            d.Property = 1000;
            d.Weight = 1000;
        }
        r.Done();
        return r;
    }

    private static List<OutlookInsight> OutlookAdvice(OutlookReadings r)
    {
        var list = new List<OutlookInsight>();
        OutlookInsights.Build(r, list);
        return list;
    }

    private static bool OutlookHas(List<OutlookInsight> list, int rule)
    {
        foreach (OutlookInsight i in list)
        {
            if (i.Rule == rule)
            {
                return true;
            }
        }
        return false;
    }

    private static OutlookInsight OutlookGet(List<OutlookInsight> list, int rule)
    {
        foreach (OutlookInsight i in list)
        {
            if (i.Rule == rule)
            {
                return i;
            }
        }
        return default(OutlookInsight);
    }

    // The English words, standing in for the game's locale in play.
    private static OutlookWords OutlookEnglish(Dictionary<string, string> en)
    {
        return new OutlookWords { T = key => en.TryGetValue(key, out string t) ? t : "[" + key + "]", Money = m => "$" + m.ToString("N0", CultureInfo.InvariantCulture), District = id => id == "up" ? "Uptown" : id };
    }

    private static void OutlookInsightChecks()
    {
        OutlookReadings quiet = OutlookQuiet();
        Check(OutlookAdvice(quiet).Count == 0, "outlook advice: a quiet economy (steady, on target, jobs usual, nothing moving) has nothing to say");
        var off = OutlookQuiet();
        off.CityOn = false;
        off.Cycle = 0;
        off.Inflation = 900;
        Check(OutlookAdvice(off).Count == 0 && OutlookInsights.Build(null, new List<OutlookInsight>()) == 0, "outlook advice: with the city's economy off, nothing is said, and no readings say nothing");

        // Each rule, made to fire by the figure it reads and by nothing else.
        var cases = new List<(string Name, int Rule, Action<OutlookReadings> Set, Action<OutlookReadings> Unset)>
        {
            ("the bank raised its rate", OutlookInsights.RateUp, r => { r.LastMove = 2; r.RateBp = 350; }, r => r.LastMove = 0),
            ("the bank cut its rate", OutlookInsights.RateDown, r => { r.LastMove = -1; r.RateBp = 275; }, r => r.LastMove = 0),
            ("a rise is due", OutlookInsights.RateAheadUp, r => { r.Pending = true; r.PendingMove = 1; r.DaysToDecision = 2; }, r => r.DaysToDecision = 4),
            ("a cut is due", OutlookInsights.RateAheadDown, r => { r.Pending = true; r.PendingMove = -2; r.DaysToDecision = 3; }, r => r.Pending = false),
            ("prices high", OutlookInsights.PricesHigh, r => r.Inflation = OutlookCatalogue.InflationHigh, r => r.Inflation = OutlookCatalogue.InflationHigh - 1),
            ("prices low", OutlookInsights.PricesLow, r => r.Inflation = OutlookCatalogue.InflationLow, r => r.Inflation = OutlookCatalogue.InflationLow + 1),
            ("jobs tight", OutlookInsights.JobsTight, r => r.Unemployment = OutlookCatalogue.JobsTight, r => r.Unemployment = OutlookCatalogue.JobsTight + 1),
            ("jobs slack", OutlookInsights.JobsSlack, r => r.Unemployment = OutlookCatalogue.JobsSlack, r => r.Unemployment = OutlookCatalogue.JobsSlack - 1),
            ("a boom", OutlookInsights.Boom, r => { r.Cycle = 0; r.Footfall = 6; r.Spend = 1040; }, r => r.Cycle = 1),
            ("a slowdown", OutlookInsights.Slump, r => { r.Cycle = 2; r.Footfall = -5; r.Spend = 970; }, r => r.Cycle = 1),
            ("property up", OutlookInsights.WorthUp, r => r.Districts[1].WorthLong = OutlookDelta.Of(OutlookCatalogue.WorthMoves), r => r.Districts[1].WorthLong = OutlookDelta.Of(OutlookCatalogue.WorthMoves - 1)),
            ("property down", OutlookInsights.WorthDown, r => r.Districts[2].WorthLong = OutlookDelta.Of(-OutlookCatalogue.WorthMoves), r => r.Districts[2].WorthLong = OutlookDelta.Of(-OutlookCatalogue.WorthMoves + 1)),
            ("income up", OutlookInsights.IncomeUp, r => { r.Districts[0].IncomeLong = OutlookDelta.Of(OutlookCatalogue.IncomeMoves); r.Districts[0].Income = 1020; }, r => r.Districts[0].IncomeLong = OutlookDelta.Of(OutlookCatalogue.IncomeMoves - 1)),
            ("income down", OutlookInsights.IncomeDown, r => { r.Districts[0].IncomeLong = OutlookDelta.Of(-OutlookCatalogue.IncomeMoves); r.Districts[0].Income = 980; }, r => r.Districts[0].IncomeLong = OutlookDelta.None),
            ("a shortage", OutlookInsights.Shortage, r => { r.Supply[1].Shock = 1; r.Supply[1].Percent = 30; r.Supply[1].Days = 5; }, r => r.Supply[1].Percent = OutlookCatalogue.ShockPercent - 1),
            ("a glut", OutlookInsights.Glut, r => { r.Supply[2].Shock = 2; r.Supply[2].Percent = -18; r.Supply[2].Days = 4; r.Supply[2].Announced = true; }, r => r.Supply[2].Shock = 0),
            ("stocks up", OutlookInsights.StocksUp, r => { r.MarketOn = true; r.Long[OutlookCatalogue.TileStocks] = OutlookDelta.Of(OutlookCatalogue.StocksMove); }, r => r.MarketOn = false),
            ("stocks down", OutlookInsights.StocksDown, r => { r.MarketOn = true; r.Long[OutlookCatalogue.TileStocks] = OutlookDelta.Of(-OutlookCatalogue.StocksMove); }, r => r.Long[OutlookCatalogue.TileStocks] = OutlookDelta.Of(-OutlookCatalogue.StocksMove + 1)),
            ("demand high", OutlookInsights.DemandHigh, r => r.Districts[0].Demand[1] = OutlookCatalogue.DemandHigh, r => r.Districts[0].Demand[1] = OutlookCatalogue.DemandHigh - 1),
            ("demand low", OutlookInsights.DemandLow, r => r.Districts[1].Demand[3] = OutlookCatalogue.DemandLow, r => r.Districts[1].Demand[3] = OutlookCatalogue.DemandLow + 1),
            ("buildings going up", OutlookInsights.Building, r => r.Districts[2].Growing = OutlookCatalogue.BuildingsMany, r => r.Districts[2].Growing = OutlookCatalogue.BuildingsMany - 1),
            ("a deficit", OutlookInsights.Deficit, r => { r.CouncilOn = true; r.Revenue = 1000; r.Spending = 1100; r.Reserve = 5000; }, r => r.Spending = 900),
            ("costs up", OutlookInsights.CostsUp, r => { r.Long[OutlookCatalogue.TilePrices] = OutlookDelta.Of(300); r.WagesTomorrow = 500; }, r => r.Long[OutlookCatalogue.TilePrices] = OutlookDelta.Of(299)),
        };
        var covered = new HashSet<int>();
        var firing = new List<string>();
        var silent = new List<string>();
        foreach (var c in cases)
        {
            OutlookReadings r = OutlookQuiet();
            c.Set(r);
            List<OutlookInsight> on = OutlookAdvice(r);
            covered.Add(c.Rule);
            bool fires = OutlookHas(on, c.Rule) && on.Count <= 3;
            if (!fires)
            {
                firing.Add(c.Name);
            }
            c.Unset(r);
            List<OutlookInsight> off2 = OutlookAdvice(r);
            if (OutlookHas(off2, c.Rule))
            {
                silent.Add(c.Name);
            }
        }
        Check(covered.Count == OutlookInsights.Count && cases.Count == OutlookInsights.Count, "outlook advice: a case for each of the " + OutlookInsights.Count + " rules");
        Check(firing.Count == 0, "outlook advice: every rule speaks when its figure is where the catalogue says (not: " + string.Join(", ", firing) + ")");
        Check(silent.Count == 0, "outlook advice: and falls silent one step short of it (still talking: " + string.Join(", ", silent) + ")");

        // The sentences are written from the same figures.
        var en = new Dictionary<string, string>();
        for (int rule = 0; rule < OutlookInsights.Count; rule++)
        {
            en[OutlookInsights.HeadKeys[rule]] = OutlookEnglishHead(rule);
            en[OutlookInsights.MeanKeys[rule]] = "mean " + rule;
            if (OutlookInsights.HasTwo(rule))
            {
                en[OutlookInsights.MeanTwoKeys[rule]] = "mean two " + rule;
            }
        }
        en[OutlookInsights.HeadKeys[OutlookInsights.Shortage] + "_soon"] = "Short of {kind} soon: {percent} for {days} days.";
        en[OutlookInsights.HeadKeys[OutlookInsights.Glut] + "_soon"] = "Too much {kind} soon: {percent} for {days} days.";
        for (int k = 0; k < OutlookWords.KindKeys.Length; k++)
        {
            en[OutlookWords.KindKeys[k]] = "kind" + k;
        }
        for (int u = 0; u < 4; u++)
        {
            en[OutlookWords.UseKeys[u]] = "use" + u;
        }
        OutlookWords words = OutlookEnglish(en);
        OutlookReadings loud = OutlookQuiet();
        loud.LastMove = 3;
        loud.RateBp = 375;
        loud.Inflation = 412;
        loud.Unemployment = 35;
        loud.Cycle = 0;
        loud.Footfall = 6;
        loud.Spend = 1040;
        loud.Districts[1].Id = "up";
        loud.Districts[1].WorthLong = OutlookDelta.Of(842);
        loud.Districts[0].IncomeLong = OutlookDelta.Of(31);
        loud.Districts[0].Income = 1031;
        loud.Supply[3].Shock = 1;
        loud.Supply[3].Percent = 28;
        loud.Supply[3].Days = 6;
        loud.Districts[2].Demand[2] = 83;
        loud.Districts[2].Growing = 4;
        List<OutlookInsight> all = OutlookAdvice(loud);
        string said = string.Empty;
        foreach (OutlookInsight i in all)
        {
            said += words.Head(i) + "\n";
        }
        Check(said.Contains("0.75%") && said.Contains("3.75%") && said.Contains("4.12%") && said.Contains("3.5%") && said.Contains("6%") && said.Contains("4%") && said.Contains("Uptown") && said.Contains("8.4%")
              && said.Contains("3.1") && said.Contains("kind3") && said.Contains("28%") && said.Contains("83") && said.Contains("use2") && said.Contains("4 buildings"),
            "outlook advice: each sentence carries the figures its rule read (0.75% and 3.75% for the bank, 4.12% prices, 3.5% jobs, 6% and 4% for shops, Uptown 8.4%, income 3.1, kind3 28%, demand 83 for use2, 4 buildings)");
        bool bare = true;
        foreach (OutlookInsight i in all)
        {
            string head = words.Head(i);
            bare &= head.IndexOf('{') < 0 && head.IndexOf('[') < 0 && head.IndexOf("NaN", StringComparison.Ordinal) < 0 && head.Length > 10 && words.Mean(i).Length > 4;
        }
        Check(bare, "outlook advice: no sentence is left with a name in curly brackets or a missing text");

        // The order: what touches the player first, then the size of the move; the same whatever the order of the districts.
        OutlookReadings mine = OutlookQuiet();
        mine.LastMove = 1;
        mine.Debt = 0;
        int plain = OutlookGet(OutlookAdvice(mine), OutlookInsights.RateUp).Score;
        mine.Debt = 5000;
        OutlookInsight debtor = OutlookGet(OutlookAdvice(mine), OutlookInsights.RateUp);
        Check(debtor.Score > plain && debtor.Kind == OutlookInsightKind.Warning && debtor.Variant == 0 && OutlookGet(OutlookAdvice(OutlookQuiet().With(r => r.LastMove = 1)), OutlookInsights.RateUp).Variant == 1,
            "outlook advice: a rise in the rate matters more to a player in debt, as a warning, with the meaning that tells them to pay down; to one with none it is news with another meaning");
        OutlookReadings crowd = OutlookQuiet();
        crowd.LastMove = 4;
        crowd.Inflation = 600;
        crowd.Cycle = 3;
        crowd.Unemployment = 100;
        crowd.Districts[0].IncomeLong = OutlookDelta.Of(-60);
        crowd.Districts[1].WorthLong = OutlookDelta.Of(-900);
        List<OutlookInsight> ranked = OutlookAdvice(crowd);
        bool sorted = true;
        for (int i = 1; i < ranked.Count; i++)
        {
            sorted &= ranked[i - 1].Score >= ranked[i].Score;
        }
        crowd.Districts.Reverse();
        List<OutlookInsight> reversed = OutlookAdvice(crowd);
        bool same = ranked.Count == reversed.Count;
        for (int i = 0; same && i < ranked.Count; i++)
        {
            same &= ranked[i].Rule == reversed[i].Rule && ranked[i].Score == reversed[i].Score && ranked[i].Name == reversed[i].Name;
        }
        Check(sorted && same && ranked.Count >= 6 && ranked.Count <= OutlookCatalogue.InsightsMost, "outlook advice: the advice is in order of its score, and the same for the same readings in any order of districts (" + ranked.Count + " pieces)");
        bool kinds = true;
        foreach (OutlookInsight i in ranked)
        {
            kinds &= i.Tab >= 0 && i.Tab < OutlookTab.Count && OutlookInsights.HeadKeys[i.Rule].Length > 0;
        }
        Check(kinds && OutlookInsights.Names.Length == OutlookInsights.Count && OutlookInsights.HeadKeys.Length == OutlookInsights.Count, "outlook advice: each piece points at a tab and has its texts named");

        // The district rules pick the district that moved most, the same one every time.
        OutlookReadings tie = OutlookQuiet();
        tie.Districts[0].IncomeLong = OutlookDelta.Of(40);
        tie.Districts[1].IncomeLong = OutlookDelta.Of(55);
        tie.Districts[2].IncomeLong = OutlookDelta.Of(55);
        Check(OutlookGet(OutlookAdvice(tie), OutlookInsights.IncomeUp).Name == "d1", "outlook advice: of two districts that moved the same, the one first by id is named");
        tie.Districts[1].Mine = false;
        tie.Districts[0].Mine = true;
        tie.Debt = 0;
        OutlookInsight others = OutlookGet(OutlookAdvice(tie), OutlookInsights.IncomeUp);
        tie.Districts[1].Mine = true;
        OutlookInsight theirs = OutlookGet(OutlookAdvice(tie), OutlookInsights.IncomeUp);
        Check(others.Kind == OutlookInsightKind.Info && theirs.Kind == OutlookInsightKind.Opportunity && theirs.Score > others.Score,
            "outlook advice: a district the player has a business in is a chance, ranked higher; one they have nothing in is only news");
    }

    // Test wording of each rule's head, the placeholders of the real texts.
    private static string OutlookEnglishHead(int rule)
    {
        switch (rule)
        {
            case OutlookInsights.RateUp: return "Raised by {points} to {rate}.";
            case OutlookInsights.RateDown: return "Cut by {points} to {rate}.";
            case OutlookInsights.RateAheadUp: return "Due: up {points} to {rate} in {days} days.";
            case OutlookInsights.RateAheadDown: return "Due: down {points} to {rate} in {days} days.";
            case OutlookInsights.PricesHigh: return "Prices rise {inflation} a year, target {target}.";
            case OutlookInsights.PricesLow: return "Prices rise only {inflation} a year, target {target}.";
            case OutlookInsights.CostsUp: return "Prices rose {change} in {days} days.";
            case OutlookInsights.JobsTight: return "Unemployment is low at {rate}.";
            case OutlookInsights.JobsSlack: return "Unemployment is high at {rate}.";
            case OutlookInsights.Boom: return "Shops are {footfall} busier and customers spend {spend} more.";
            case OutlookInsights.Slump: return "Shops are {footfall} quieter and customers spend {spend} less.";
            case OutlookInsights.WorthUp: return "Property in {district} gained {change} in {days} days.";
            case OutlookInsights.WorthDown: return "Property in {district} lost {change} in {days} days.";
            case OutlookInsights.IncomeUp: return "People in {district} have more to spend: up {change} points to {income}.";
            case OutlookInsights.IncomeDown: return "People in {district} have less to spend: down {change} points to {income}.";
            case OutlookInsights.Shortage: return "Short of {kind}: {percent} for {days} days.";
            case OutlookInsights.Glut: return "Too much {kind}: {percent} for {days} days.";
            case OutlookInsights.StocksUp: return "The index rose {change} in {days} days.";
            case OutlookInsights.StocksDown: return "The index fell {change} in {days} days.";
            case OutlookInsights.DemandHigh: return "Demand for {use} in {district} is {demand} out of 100.";
            case OutlookInsights.DemandLow: return "Demand for {use} in {district} is only {demand} out of 100.";
            case OutlookInsights.Building: return "{count} buildings are going up in {district}.";
            default: return "The city is short by {amount} a week, reserve {reserve}.";
        }
    }

    private static OutlookReadings With(this OutlookReadings r, Action<OutlookReadings> change)
    {
        change(r);
        return r;
    }

    // ---------------------------------------------------------------- the charts

    private static void OutlookScaleChecks()
    {
        // The axis for any range at all.
        double[][] ranges =
        {
            new[] { 5.0, 5.0 }, new[] { 0.0, 0.0 }, new[] { -50.0, -10.0 }, new[] { 1e12, 2e12 }, new[] { 1e-9, 2e-9 }, new[] { 0.0, 1.0 }, new[] { -3.0, 7.0 }, new[] { 9.0, 3.0 },
            new[] { double.NaN, 4.0 }, new[] { 2.0, double.PositiveInfinity }, new[] { double.NegativeInfinity, double.NaN }, new[] { -1e300, 1e300 }, new[] { 100000.0, 100000.4 },
            new[] { 1234.5, 1234.5 }, new[] { -7.0, -7.0 }
        };
        bool axes = true;
        string bad = null;
        foreach (double[] range in ranges)
        {
            foreach (bool zero in new[] { false, true })
            {
                foreach (int ticks in new[] { 0, 2, 5, 40 })
                {
                    OutlookAxis a = OutlookScale.Nice(range[0], range[1], ticks, zero);
                    double lo = OutlookScale.Finite(range[0]) ? range[0] : 0.0, hi = OutlookScale.Finite(range[1]) ? range[1] : 0.0;
                    double min = Math.Min(Math.Max(Math.Min(lo, hi), -OutlookScale.Largest), OutlookScale.Largest), max = Math.Max(Math.Min(Math.Max(lo, hi), OutlookScale.Largest), -OutlookScale.Largest);
                    double tolerance = 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(min), Math.Abs(max)));
                    bool ok = OutlookScale.Finite(a.Lo) && OutlookScale.Finite(a.Hi) && OutlookScale.Finite(a.Step) && a.Step > 0.0 && a.Hi > a.Lo && a.Lo <= min + tolerance
                              && a.Hi >= max - tolerance && a.Ticks >= 2 && a.Ticks <= OutlookScale.TicksMost + 2 && (!zero || (a.Lo <= 0.0 && a.Hi >= 0.0));
                    double f1 = a.Fraction(lo), f2 = a.Fraction(double.NaN), f3 = a.Fraction(1e300), f4 = a.Fraction(-1e300);
                    ok &= f1 >= 0.0 && f1 <= 1.0 && f2 == 0.5 && f3 <= 1.0 && f4 >= 0.0;
                    axes &= ok;
                    bad = ok || bad != null ? bad : range[0] + ".." + range[1] + " zero " + zero + " ticks " + ticks + " gave " + a.Lo + ".." + a.Hi + " step " + a.Step;
                }
            }
        }
        Check(axes, "outlook charts: an axis for a flat range, zero, a negative range, huge and tiny values, NaN, infinity and a reversed range has a step, covers the data and is finite" + (bad != null ? " (" + bad + ")" : string.Empty));
        OutlookAxis flat = OutlookScale.Nice(5.0, 5.0, 5, false);
        OutlookAxis zeros = OutlookScale.Nice(0.0, 0.0, 5, false);
        Check(flat.Lo < 5.0 && flat.Hi > 5.0 && flat.Fraction(5.0) > 0.2 && flat.Fraction(5.0) < 0.8 && zeros.Lo < 0.0 && zeros.Hi > 0.0, "outlook charts: a flat series gets room either side, so its line stands in the middle of the chart");
        OutlookAxis tidy = OutlookScale.Nice(97.3, 104.6, 5, false);
        Check(tidy.Lo == 96.0 && tidy.Hi == 106.0 && tidy.Step == 2.0 && tidy.Tick(2) == 100.0, "outlook charts: the ends and the step are round numbers (96 to 106 in twos for 97.3 to 104.6: " + tidy.Lo + " to " + tidy.Hi + " step " + tidy.Step + ")");
        Check(OutlookScale.NiceStep(0) == 1.0 && OutlookScale.NiceStep(-3) == 1.0 && OutlookScale.NiceStep(double.NaN) == 1.0 && OutlookScale.NiceStep(0.07) == 0.1 && OutlookScale.NiceStep(230) == 500.0 && OutlookScale.NiceStep(1) == 1.0,
            "outlook charts: the step is 1, 2, 5 or 10 times a power of ten, and 1 for anything that is not a positive number");

        // Laying a window out as points.
        var window = new int[120];
        for (int i = 0; i < window.Length; i++)
        {
            window[i] = i % 17 == 3 ? OutlookCatalogue.NoValue : 100000 + (i * 37 % 900);
        }
        OutlookScale.Range(window, window.Length, 1000.0, out double lo2, out double hi2, out int found);
        OutlookAxis axis = OutlookScale.Nice(lo2, hi2, 5, false);
        var xs = new float[120];
        var ys = new float[120];
        int n = OutlookScale.Points(window, window.Length, 1000.0, axis, 800f, 200f, xs, ys);
        bool inside = true, rising = true;
        for (int i = 0; i < n; i++)
        {
            inside &= xs[i] >= 0f && xs[i] <= 800f && ys[i] >= 0f && ys[i] <= 200f && !float.IsNaN(xs[i]) && !float.IsNaN(ys[i]);
            rising &= i == 0 || xs[i] > xs[i - 1];
        }
        Check(n == found && n == 120 - 7 && inside && rising, "outlook charts: a window with gaps gives a point for each reading, left to right, inside the box (" + n + " points, " + found + " readings)");
        var one = new int[] { OutlookCatalogue.NoValue, 5000, OutlookCatalogue.NoValue };
        OutlookScale.Range(one, 3, 100.0, out double lo3, out double hi3, out int found3);
        int n3 = OutlookScale.Points(one, 3, 100.0, OutlookScale.Nice(lo3, hi3, 4, false), 300f, 100f, xs, ys);
        Check(found3 == 1 && n3 == 1 && ys[0] > 10f && ys[0] < 90f, "outlook charts: a single reading is a single point in the middle of the chart");
        var nothing = new int[] { OutlookCatalogue.NoValue, OutlookCatalogue.NoValue };
        Check(!OutlookScale.Range(nothing, 2, 1.0, out _, out _, out int found0) && found0 == 0 && OutlookScale.Points(nothing, 2, 1.0, flat, 10f, 10f, xs, ys) == 0
              && OutlookScale.Points(null, 2, 1.0, flat, 10f, 10f, xs, ys) == 0 && OutlookScale.Points(one, 3, 0.0, flat, 10f, 10f, xs, ys) == 0 && OutlookScale.Points(one, 0, 1.0, flat, 10f, 10f, xs, ys) == 0,
            "outlook charts: no readings, no window and no unit are no points");
        var big = new int[] { int.MaxValue, int.MinValue + 1, 0, int.MaxValue };
        OutlookScale.Range(big, 4, 1.0, out double lo4, out double hi4, out _);
        int n4 = OutlookScale.Points(big, 4, 1.0, OutlookScale.Nice(lo4, hi4, 5, false), 100f, 100f, xs, ys);
        bool finite = n4 == 4;
        for (int i = 0; i < n4; i++)
        {
            finite &= !float.IsNaN(ys[i]) && !float.IsInfinity(ys[i]) && ys[i] >= 0f && ys[i] <= 100f;
        }
        Check(finite, "outlook charts: the largest and smallest numbers lay out without overflow");

        // The smooth line.
        float[] px = { 0f, 10f, 20f, 30f, 40f }, py = { 50f, 50f, 20f, 20f, 80f };
        var ox = new float[(px.Length - 1) * 6 + 1];
        var oy = new float[ox.Length];
        int m = OutlookScale.Smooth(px, py, px.Length, 6, ox, oy);
        bool through = m == ox.Length, bounded = true, mono = true;
        for (int k = 0; k < px.Length; k++)
        {
            through &= ox[k * 6] == px[k] && oy[k * 6] == py[k];
        }
        for (int i = 0; i < m; i++)
        {
            bounded &= oy[i] >= 20f - 1e-3f && oy[i] <= 80f + 1e-3f && !float.IsNaN(oy[i]);
            mono &= i == 0 || ox[i] >= ox[i - 1];
        }
        Check(through && bounded && mono, "outlook charts: the smooth line passes through every point, stays between the highest and lowest and runs left to right (" + m + " points)");
        bool flatLines = true;
        for (int i = 12; i <= 18; i++)
        {
            flatLines &= Math.Abs(oy[i] - 20f) < 1e-3f;
        }
        Check(flatLines, "outlook charts: between two points of the same height the smooth line is flat, it does not bow");
        bool noOvershoot = true;
        var random = new Random(5);
        for (int trial = 0; trial < 300; trial++)
        {
            int points = random.Next(1, 40);
            var tx = new float[points];
            var ty = new float[points];
            float at = 0f;
            for (int i = 0; i < points; i++)
            {
                at += random.Next(5) == 0 ? 0f : random.Next(1, 30);   // equal x now and then
                tx[i] = at;
                ty[i] = random.Next(0, 3) == 0 ? 0f : random.Next(-1000, 1000);
            }
            int subs = random.Next(1, 8);
            var qx = new float[Math.Max(1, (points - 1) * subs + 1)];
            var qy = new float[qx.Length];
            int made = OutlookScale.Smooth(tx, ty, points, subs, qx, qy);
            noOvershoot &= made == qx.Length;
            for (int k = 0; k + 1 < points; k++)
            {
                float lo5 = Math.Min(ty[k], ty[k + 1]) - 1e-2f * (1f + Math.Abs(ty[k]) + Math.Abs(ty[k + 1])), hi5 = Math.Max(ty[k], ty[k + 1]) + 1e-2f * (1f + Math.Abs(ty[k]) + Math.Abs(ty[k + 1]));
                for (int s = 0; s < subs; s++)
                {
                    float y = qy[k * subs + s];
                    noOvershoot &= !float.IsNaN(y) && !float.IsInfinity(y) && y >= lo5 && y <= hi5;
                }
            }
        }
        Check(noOvershoot, "outlook charts: 300 random lines (equal x, flat runs, sharp turns) are smoothed without an overshoot, a NaN or a missing point");
        Check(OutlookScale.Smooth(px, py, 1, 4, ox, oy) == 1 && OutlookScale.Smooth(px, py, 0, 4, ox, oy) == 0 && OutlookScale.Smooth(null, py, 3, 4, ox, oy) == 0 && OutlookScale.Smooth(px, py, 5, 6, new float[3], new float[3]) == 0
              && OutlookScale.Smooth(px, py, 2, 0, ox, oy) == 2, "outlook charts: one point is itself, none is none, too small a buffer is refused and no cutting is one piece");

        // The speed of it: a whole chart's scaling and smoothing.
        var wide = new int[OutlookCatalogue.HistoryDays];
        for (int i = 0; i < wide.Length; i++)
        {
            wide[i] = 100000 + i * 11 % 700;
        }
        var wx = new float[OutlookCatalogue.HistoryDays];
        var wy = new float[OutlookCatalogue.HistoryDays];
        var sx = new float[OutlookCatalogue.HistoryDays * 4];
        var sy = new float[sx.Length];
        var watch = Stopwatch.StartNew();
        int made2 = 0;
        for (int t = 0; t < 2000; t++)
        {
            OutlookScale.Range(wide, wide.Length, 1000.0, out double a1, out double b1, out _);
            OutlookAxis ax = OutlookScale.Nice(a1, b1, 5, false);
            int np = OutlookScale.Points(wide, wide.Length, 1000.0, ax, 1800f, 300f, wx, wy);
            made2 += OutlookScale.Smooth(wx, wy, np, 4, sx, sy);
        }
        watch.Stop();
        Check(made2 > 0 && watch.Elapsed.TotalMilliseconds < 400, "outlook charts: scaling and smoothing a chart of 120 days takes " + (watch.Elapsed.TotalMilliseconds / 2000).ToString("0.000", CultureInfo.InvariantCulture) + " ms");
    }

    // ---------------------------------------------------------------- the colours

    private static void OutlookThemeChecks()
    {
        Check(Math.Abs(OutlookTheme.Contrast(0x000000, 0xFFFFFF) - 21.0) < 1e-6 && Math.Abs(OutlookTheme.Contrast(0x123456, 0x123456) - 1.0) < 1e-9
              && Math.Abs(OutlookTheme.Contrast(0x777777, 0x000000) - OutlookTheme.Contrast(0x000000, 0x777777)) < 1e-12,
            "outlook colours: the contrast ratio is 21 for black on white, 1 for a colour on itself, and the same either way round");
        Check(OutlookTheme.Blend(0xFF0000, 0x000000, 0.5) == 0x800000 && OutlookTheme.Blend(0x123456, 0xABCDEF, 1.0) == 0x123456 && OutlookTheme.Blend(0x123456, 0xABCDEF, 0.0) == 0xABCDEF
              && OutlookTheme.Blend(0x123456, 0xABCDEF, 5.0) == 0x123456, "outlook colours: a colour laid over another at an opacity, held between none and all");
        foreach (OutlookPalette p in new[] { OutlookTheme.Dark, OutlookTheme.Light })
        {
            string weakest;
            double worst = OutlookTheme.Weakest(p, out weakest);
            Check(worst >= OutlookTheme.Least, "outlook colours: in the " + p.Name + " look every lettering on its surface holds WCAG AA, 4.5 to 1 (the weakest, " + weakest + ", is " + worst.ToString("0.00", CultureInfo.InvariantCulture) + ")");
            int pairs = OutlookTheme.Pairs(p).Length;
            Check(pairs >= 18, "outlook colours: the " + p.Name + " look lists " + pairs + " letterings on their surfaces");
        }
        Check(OutlookTheme.Of(0) == OutlookTheme.Dark && OutlookTheme.Of(1) == OutlookTheme.Light && OutlookTheme.Of(7) == OutlookTheme.Dark && OutlookTheme.Of(-1) == OutlookTheme.Dark && OutlookTheme.Dark.Dark && !OutlookTheme.Light.Dark,
            "outlook colours: dark is the look to begin with and for anything the option does not name");
        Check(OutlookTheme.Dark.Back == 0x0F1420 && OutlookTheme.Dark.Card == 0x171E2E && OutlookTheme.Dark.Muted == 0x8A94A7 && OutlookTheme.Dark.Good == 0x2ECC71 && OutlookTheme.Dark.Bad == 0xFF5C5C,
            "outlook colours: the dark look is the owner's navy, card, muted grey and green and red");

        // The map's colours made visible on a card, in either look.
        var rng = new Random(9);
        bool seen = true;
        for (int i = 0; i < 2000; i++)
        {
            int colour = rng.Next(0, 0x1000000);
            seen &= OutlookTheme.Contrast(OutlookTheme.Visible(colour, OutlookTheme.Dark.Card, true), OutlookTheme.Dark.Card) >= OutlookTheme.LeastShape
                    && OutlookTheme.Contrast(OutlookTheme.Visible(colour, OutlookTheme.Light.Card, false), OutlookTheme.Light.Card) >= OutlookTheme.LeastShape;
        }
        Check(seen, "outlook colours: any colour of the city map is made visible against a card (3 to 1) in both looks");
        Check(OutlookTheme.Visible(0x2DD4BF, OutlookTheme.Dark.Card, true) == 0x2DD4BF, "outlook colours: a colour that is visible already is left as it is");
    }
}
