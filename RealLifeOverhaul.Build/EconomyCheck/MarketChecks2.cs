using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Markets;

// The market's catalogue, calendar and chance; prices that stay sane; and its long run: thousands of days from many seeds
// at a short and a long year, measured as an economist would (the return and its spread, fat tails, calm and nervous
// spells, losing years, crashes), and the rules of thumb a player might try, none of which beats holding the market.
internal static partial class Program
{
    private static readonly string[] HouseRuleWords = { "gun", "guns", "arms", "armour", "weapon", "weapons", "rifle", "ammo", "munitions", "defence", "defense", "missile", "pet", "pets", "vet", "vets", "kennel", "kennels", "puppy", "animal" };

    private static void MarketCatalogueChecks()
    {
        var ids = new HashSet<int>();
        var tickers = new HashSet<string>();
        var names = new HashSet<string>();
        int listed = 0;
        var perSector = new int[(int)MarketSector.Count];
        foreach (CompanyDef d in MarketCatalogue.All)
        {
            Check(ids.Add(d.Id), "market: the company id " + d.Id + " is used once");
            Check(tickers.Add(d.Ticker), "market: the ticker " + d.Ticker + " is used once");
            Check(names.Add(d.Name), "market: the name " + d.Name + " is used once");
            Check(d.Id > 0 && d.Id < 900, "market: " + d.Name + " has an id below the rivals' (" + d.Id + ")");
            Check(d.RevenueM > 0 && d.PriceCents >= 100 && d.PriceCents <= 100000, "market: " + d.Name + " has a turnover and a starting price in bounds");
            Check(d.Trade == null || Array.IndexOf(MarketInputs.Trades, d.Trade) >= 0, "market: the trade of " + d.Name + " is one the market knows (" + d.Trade + ")");
            Check(d.MarginBp > 0 && d.MarginBp < 6000 && d.Beta >= 30 && d.Beta <= 160 && d.IdioBp >= 50 && d.IdioBp <= 300
                  && d.SpreadBp >= 2 && d.SpreadBp <= 120 && d.TurnoverBp >= 10 && d.TurnoverBp <= 120,
                "market: " + d.Name + "'s profile is in bounds (margin " + d.MarginBp + ", beta " + d.Beta + ", own move " + d.IdioBp + " bp, spread " + d.SpreadBp + " bp)");
            foreach (string word in d.Name.ToLowerInvariant().Split(' '))
            {
                Check(Array.IndexOf(HouseRuleWords, word) < 0, "market: " + d.Name + " keeps to the house rules (no weapons, no pets)");
            }
            Check(d.Ticker.Length >= 2 && d.Ticker.Length <= 4 && d.Ticker.All(char.IsUpper), "market: the ticker of " + d.Name + " is two to four capitals");
            if (!d.Pipeline)
            {
                listed++;
                perSector[(int)d.Sector]++;
            }
        }
        Check(listed >= 130, "market: well over a hundred companies are listed at the start (" + listed + ")");
        for (int s = 0; s < perSector.Length; s++)
        {
            Check(perSector[s] >= 4, "market: the sector " + (MarketSector)s + " has four companies at least (" + perSector[s] + ")");
        }
        Check(MarketCatalogue.All.Count(d => d.Pipeline) >= 8, "market: there are companies waiting to come to the market");
        Check(MarketCatalogue.SectorKeys.Length == (int)MarketSector.Count && MarketCatalogue.Sectors.Length == (int)MarketSector.Count, "market: every sector has a name and a profile");
        // The old market's companies all have a home here, in a sector that fits theirs.
        var fits = new[] { new[] { MarketSector.Transport, MarketSector.Logistics }, new[] { MarketSector.Property }, new[] { MarketSector.Stores }, new[] { MarketSector.Groceries, MarketSector.Restaurants },
            new[] { MarketSector.Services }, new[] { MarketSector.Power, MarketSector.Water }, new[] { MarketSector.Banks }, new[] { MarketSector.Media, MarketSector.Leisure } };
        var homes = new HashSet<int>();
        for (int sector = 0; sector < MarketCatalogue.VenturesMap.Length; sector++)
        {
            for (int n = 0; n < 2; n++)
            {
                CompanyDef d = MarketCatalogue.Find(MarketCatalogue.VenturesMap[sector][n]);
                Check(d != null && !d.Pipeline && fits[sector].Contains(d.Sector), "market: the old market's company " + sector + "/" + n + " has a listed home of a fitting sector");
                Check(d != null && homes.Add(d.Id), "market: no two of the old market's companies share a home");
            }
        }
        Check(MarketCatalogue.RivalTemplates.Length == 4 && MarketCatalogue.RivalTemplates.All(t => MarketCatalogue.IsRival(t.Id)), "market: four rival holdings, each with a rival's id");
        Check(MarketCatalogue.Find(1) != null && MarketCatalogue.Find(901) != null && MarketCatalogue.Find(899) == null, "market: Find finds a company and a rival template, and nothing for an unknown id");
    }

    private static void MarketCalendarChecks()
    {
        Check(MarketCatalogue.Steps == 78 && MarketCatalogue.PhaseClose == 81, "market: 78 steps of five minutes from 09:30 to 16:00");
        Check(MarketCatalogue.Weekday(1) == 0 && MarketCatalogue.Weekday(7) == 6 && MarketCatalogue.Weekday(8) == 0, "market: day 1 is a Monday");
        Check(MarketCatalogue.TradingDay(5) && !MarketCatalogue.TradingDay(6) && !MarketCatalogue.TradingDay(7), "market: Monday to Friday trade, the weekend does not");
        bool phases = true;
        for (int p = MarketCatalogue.PhaseOpen; p < MarketCatalogue.PhaseClose - 1; p++)
        {
            phases &= MarketCatalogue.PhaseAt(MarketCatalogue.MinuteOfPhase(p)) == p;
        }
        Check(phases && MarketCatalogue.PhaseAt(MarketCatalogue.CloseMinute) == MarketCatalogue.PhaseClose && MarketCatalogue.PhaseAt(MarketCatalogue.OpenMinute - 1) == MarketCatalogue.PhaseResults
              && MarketCatalogue.PhaseAt(0) == MarketCatalogue.PhaseNone, "market: each phase is reached at its minute, the close at 16:00");
        Check(MarketCatalogue.OpenAt(1, 9 * 60 + 30) && !MarketCatalogue.OpenAt(1, 16 * 60) && !MarketCatalogue.OpenAt(6, 12 * 60), "market: open from 09:30 to before 16:00 on a market day");
        foreach (int year in new[] { 60, 61, 100, 365 })
        {
            bool quarters = true, months = true;
            for (int d = 1; d <= year * 3; d++)
            {
                int q = MarketCatalogue.QuarterOf(d, year);
                quarters &= MarketCatalogue.QuarterStart(q, year) <= d && d < MarketCatalogue.QuarterStart(q + 1, year);
                int m = MarketCatalogue.MonthOf(d, year);
                months &= MarketHistory.MonthStart(m, year) <= d && d < MarketHistory.MonthStart(m + 1, year);
            }
            Check(quarters, "market: quarters tile a year of " + year + " days");
            Check(months, "market: months tile a year of " + year + " days");
            Check(MarketCatalogue.QuarterOf(year + 1, year) == 4 && MarketCatalogue.MonthOf(year + 1, year) == 12, "market: a new year begins its first quarter and month (" + year + " days)");
        }
        Check(Math.Abs(MarketCatalogue.VolScale(60) - 1.43) < 0.02 && MarketCatalogue.VolScale(365) == 1.0, "market: a short year's days move by its scale (1.43 at 60 days, 1 at 365)");
        Check(MarketTrading.MarketDays(5, 1) == 8 && MarketTrading.NextTradingDay(6) == 8, "market: the next market day after a Friday or a Saturday is the Monday");
    }

    private static void MarketRandomChecks()
    {
        Check(Math.Abs(MarketRandom.InverseNormal(0.975) - 1.959964) < 1e-5 && Math.Abs(MarketRandom.InverseNormal(0.5)) < 1e-9
              && Math.Abs(MarketRandom.InverseNormal(0.001) + 3.090232) < 1e-5 && Math.Abs(MarketRandom.InverseNormal(0.999) - 3.090232) < 1e-5,
            "market: the bell curve's inverse at 2.5%, 50% and 0.1% to five places");
        double sum = 0, sq = 0, four = 0, corr = 0;
        int n = 200000;
        double least = 1, most = 0;
        for (int i = 0; i < n; i++)
        {
            double z = MarketRandom.Fat(77, MarketRandom.OwnShock, MarketRandom.OwnTail, i, 3);
            double other = MarketRandom.Fat(77, MarketRandom.SectorShock, MarketRandom.SectorTail, i, 3);
            sum += z;
            sq += z * z;
            four += z * z * z * z;
            corr += z * other;
            double u = MarketRandom.Unit(5, 1, i);
            least = Math.Min(least, u);
            most = Math.Max(most, u);
        }
        double mean = sum / n, variance = sq / n - mean * mean, kurtosis = four / n / (variance * variance);
        Check(Math.Abs(mean) < 0.01 && Math.Abs(variance - 1.0) < 0.02, "market: the fat-tailed draw has mean 0 and spread 1 (" + mean.ToString("0.0000") + ", " + variance.ToString("0.000") + ")");
        Check(kurtosis > 3.6 && kurtosis < 8.0, "market: its tails are fatter than a bell curve's (kurtosis " + kurtosis.ToString("0.00") + ")");
        Check(Math.Abs(corr / n) < 0.01, "market: draws for different purposes are unrelated (" + (corr / n).ToString("0.0000") + ")");
        Check(least > 0.0 && most < 1.0, "market: a unit draw is never exactly 0 or 1");
        Check(MarketRandom.Key(9, 1, 2, 3) == MarketRandom.Key(9, 1, 2, 3) && MarketRandom.Key(9, 1, 2, 3) != MarketRandom.Key(10, 1, 2, 3)
              && MarketRandom.Key(9, 1, 2, 3) != MarketRandom.Key(9, 1, 3, 2), "market: a draw is the same for the same secret and purpose, and another for another");
    }

    // Prices stay sane: never under a cent or absurd, every candle in order, the market a sensible size.
    private static void MarketPriceChecks()
    {
        int badPrice = 0, badCandle = 0, badState = 0;
        int leastListed = int.MaxValue, mostListed = 0;
        double lowest = double.MaxValue, highest = 0;
        foreach (int year in new[] { 60, 365 })
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                MarketEngine m = NewMarket(seed * 101, year);
                int days = year == 60 ? 300 : 500;
                for (int d = 1; d <= days; d += 25)
                {
                    RunTo(m, d);
                    int listed = 0;
                    foreach (Stock s in m.State.Stocks)
                    {
                        if (!s.Listed)
                        {
                            continue;
                        }
                        listed++;
                        double mid = s.Mid;
                        lowest = Math.Min(lowest, mid);
                        highest = Math.Max(highest, mid);
                        badPrice += double.IsNaN(mid) || mid < 0.01 || mid > 50000.0 || s.Last < 1 ? 1 : 0;
                        badState += double.IsNaN(s.LogFair) || double.IsNaN(s.Mis) || double.IsNaN(s.Rev) || double.IsNaN(s.Margin) || s.Shares <= 0 || s.Adv <= 0 ? 1 : 0;
                        badCandle += s.Low > s.High || s.Last < s.Low || s.Last > s.High || s.Open < s.Low || s.Open > s.High ? 1 : 0;
                        foreach (Candle c in s.Daily)
                        {
                            badCandle += c.L > Math.Min(c.O, c.C) || c.H < Math.Max(c.O, c.C) || c.L < 1 || c.V < 0 ? 1 : 0;
                        }
                    }
                    leastListed = Math.Min(leastListed, listed);
                    mostListed = Math.Max(mostListed, listed);
                }
                Check(m.State.IndexLast > 10000 && m.State.IndexLast < 2000000, "market: the index stays sane (" + m.State.IndexLast + ", seed " + seed + ", year " + year + ")");
            }
        }
        Check(badPrice == 0, "market: no price is ever under a cent, absurd or not a number (" + badPrice + "); lowest " + lowest.ToString("0.00") + ", highest " + highest.ToString("0.00"));
        Check(badState == 0, "market: no company's figures are ever not a number (" + badState + ")");
        Check(badCandle == 0, "market: every candle has its low under and its high over its open and close (" + badCandle + ")");
        Check(leastListed >= 110 && mostListed <= MarketCatalogue.CompaniesMost, "market: the market keeps a sensible number of companies (" + leastListed + " to " + mostListed + ")");
    }

    // ---------------------------------------------------------------- the long run, and the rules of thumb

    /// <summary>A run's history for the rules of thumb: each company's closes and the dividends it paid, by market day.</summary>
    private sealed class MarketPast
    {
        public int Year;
        public readonly List<int> Days = new List<int>();
        public readonly Dictionary<int, List<double>> Close = new Dictionary<int, List<double>>();
        public readonly Dictionary<int, List<double>> Dividend = new Dictionary<int, List<double>>();
        public readonly Dictionary<int, List<double>> Pe = new Dictionary<int, List<double>>();
        public readonly Dictionary<int, List<double>> K = new Dictionary<int, List<double>>();     // its cost of equity that day: what holding it should return a year
        public readonly Dictionary<int, List<double>> Distress = new Dictionary<int, List<double>>();
        public readonly Dictionary<int, List<int>> Crises = new Dictionary<int, List<int>>();       // the days its own bad news came out (after the close)
        public readonly Dictionary<int, double> Cost = new Dictionary<int, double>();   // a round trip's cost, as a share
        public readonly Dictionary<int, List<int>> Results = new Dictionary<int, List<int>>();
        public readonly Dictionary<int, List<int>> Records = new Dictionary<int, List<int>>();
    }

    private static readonly List<MarketPast> Pasts = new List<MarketPast>();

    private static void MarketLongRun()
    {
        Pasts.Clear();
        LongRun(60, 16, 5);
        LongRun(365, 6, 3);
    }

    private static void LongRun(int year, int years, int seeds)
    {
        var annual = new List<double>();
        var daily = new List<double>();
        double worstDrop = 0.0, sumK = 0.0;
        int kCount = 0, crashes = 0, failures = 0, manias = 0, listings = 0, bids = 0;
        for (int seedNo = 1; seedNo <= seeds; seedNo++)
        {
            ulong seed = (ulong)(seedNo * 7919 + year);
            var world = new MarketWorld(seed);
            MarketEngine m = NewMarket(seed, year, 1, 0, world);
            foreach (Stock s in m.State.Stocks)
            {
                sumK += MarketValuation.CostOfEquity(s, m.State.Env);
                kCount++;
            }
            var past = new MarketPast { Year = year };
            double total = 1.0, peak = 1.0, yearStart = 1.0;
            int prevIndex = m.State.IndexLast;
            for (int d = 1; d <= year * years; d++)
            {
                RunTo(m, d);
                if (!MarketCatalogue.TradingDay(d))
                {
                    continue;
                }
                double cap = 0.0, paid = 0.0;
                past.Days.Add(d);
                int at = past.Days.Count - 1;
                foreach (Stock s in m.State.Stocks)
                {
                    if (!s.Listed && s.GoneDay != d)
                    {
                        continue;
                    }
                    double price = s.Listed ? s.Last / 100.0 : s.GoneCents / 100.0;
                    cap += s.Listed ? s.Shares * price : 0.0;
                    // (a dividend counts on the day it leaves the price: it is the holders' of the day before)
                    double dividend = s.Listed && s.ExDone && s.LastExDay == d && s.DeclaredCents > 0 ? s.DeclaredCents / 100.0 : 0.0;
                    paid += dividend * s.Shares;
                    Series(past.Close, s.Id, at).Add(price);
                    Series(past.Dividend, s.Id, at).Add(dividend);
                    double eps = 0.0;
                    for (int r = Math.Max(0, s.Results.Count - 4); r < s.Results.Count; r++)
                    {
                        eps += s.Results[r].Eps / 10000.0;
                    }
                    Series(past.Pe, s.Id, at).Add(eps > 0.0 ? price / eps : double.NaN);
                    Series(past.K, s.Id, at).Add(MarketValuation.CostOfEquity(s, m.State.Env));
                    Series(past.Distress, s.Id, at).Add(s.Distress);
                    if (!past.Cost.ContainsKey(s.Id))
                    {
                        past.Cost[s.Id] = 2.0 * (s.Def.SpreadBp / 2.0 + MarketCatalogue.CommissionBp) / 10000.0;
                    }
                    if (s.Listed && s.ResultsDay > d)
                    {
                        Mark(past.Results, s.Id, s.ResultsDay);
                    }
                    if (s.Listed && s.RecordDay >= d && s.DeclaredCents > 0)
                    {
                        Mark(past.Records, s.Id, s.RecordDay);
                    }
                }
                double move = m.State.IndexLast / (double)prevIndex - 1.0;
                daily.Add(Math.Log(m.State.IndexLast / (double)prevIndex));
                total *= 1.0 + move + (cap > 0.0 ? paid / cap : 0.0);
                prevIndex = m.State.IndexLast;
                peak = Math.Max(peak, total);
                worstDrop = Math.Max(worstDrop, 1.0 - total / peak);
                if (d % year == 0)
                {
                    annual.Add(total / yearStart - 1.0);
                    yearStart = total;
                }
            }
            foreach (MarketNews h in world.Heard.Where(h => h.Kind == MarketNewsKind.Crisis))
            {
                Mark(past.Crises, h.Stock, h.Day);
            }
            crashes += world.Heard.Count(h => h.Kind == MarketNewsKind.Crash);
            failures += world.Heard.Count(h => h.Kind == MarketNewsKind.Failure);
            manias += world.Heard.Count(h => h.Kind == MarketNewsKind.Mania && h.B == 1);
            listings += world.Heard.Count(h => h.Kind == MarketNewsKind.Listing && h.A > 0);
            bids += world.Heard.Count(h => h.Kind == MarketNewsKind.Bid);
            Pasts.Add(past);
        }
        double k = sumK / kCount;
        double geometric = Math.Exp(annual.Select(a => Math.Log(1.0 + a)).Average()) - 1.0;
        double am = annual.Average(), asd = Math.Sqrt(annual.Select(a => (a - am) * (a - am)).Average());
        double dm = daily.Average(), dsd = Math.Sqrt(daily.Select(x => (x - dm) * (x - dm)).Average());
        double kurtosis = daily.Select(x => Math.Pow((x - dm) / dsd, 4)).Average();
        var sq = daily.Select(x => (x - dm) * (x - dm)).ToList();
        double sqm = sq.Average(), sqv = sq.Select(x => (x - sqm) * (x - sqm)).Average(), acSq = 0.0, ac = 0.0;
        for (int i = 1; i < daily.Count; i++)
        {
            acSq += (sq[i] - sqm) * (sq[i - 1] - sqm);
            ac += (daily[i] - dm) * (daily[i - 1] - dm);
        }
        acSq /= (daily.Count - 1) * sqv;
        ac /= (daily.Count - 1) * dsd * dsd;
        double losing = annual.Count(a => a < 0.0) / (double)annual.Count;
        double seedYears = seeds * (double)years;
        Console.WriteLine("  market long run, " + year + "-day years, " + seeds + " x " + years + " years: return " + (geometric * 100).ToString("0.00") + "% a year (cost of equity "
                          + (k * 100).ToString("0.00") + "%), spread " + (asd * 100).ToString("0.0") + "%, losing years " + (losing * 100).ToString("0") + "%, worst fall "
                          + (worstDrop * 100).ToString("0") + "%; a day " + (dsd * 100).ToString("0.00") + "%, kurtosis " + kurtosis.ToString("0.0") + ", calm and nervous spells "
                          + acSq.ToString("0.00") + "; crashes " + (crashes / seedYears).ToString("0.00") + ", failures " + (failures / seedYears).ToString("0.00") + ", crazes "
                          + (manias / seedYears).ToString("0.00") + ", listings " + (listings / seedYears).ToString("0.0") + ", bids " + (bids / seedYears).ToString("0.0") + " a year");
        Check(geometric > k - 0.035 && geometric < k + 0.04, "market: holding the market returns about its cost of equity, the bank rate and a premium (" + (geometric * 100).ToString("0.00") + "% against " + (k * 100).ToString("0.00") + "%, year " + year + ")");
        Check(year == 60 ? dsd > 0.009 && dsd < 0.022 : dsd > 0.006 && dsd < 0.014, "market: a day's move of the market is realistic (" + (dsd * 100).ToString("0.00") + "%, year " + year + ")");
        Check(year == 60 ? asd > 0.04 && asd < 0.16 : asd > 0.08 && asd < 0.25, "market: a year's spread of returns is realistic (" + (asd * 100).ToString("0.0") + "%, year " + year + ")");
        Check(losing > 0.03 && losing < 0.45, "market: there are losing years, and not too many (" + (losing * 100).ToString("0") + "%, year " + year + ")");
        Check(worstDrop > 0.08 && worstDrop < 0.75, "market: real falls happen, and the market is not wiped out (" + (worstDrop * 100).ToString("0") + "%, year " + year + ")");
        Check(kurtosis > 4.0, "market: the market's days have fat tails (kurtosis " + kurtosis.ToString("0.0") + ", year " + year + ")");
        Check(acSq > 0.04, "market: calm and nervous spells: big days follow big days (" + acSq.ToString("0.00") + ", year " + year + ")");
        Check(Math.Abs(ac) < 0.08, "market: yesterday's move does not tell today's (" + ac.ToString("0.000") + ", year " + year + ")");
        Check(crashes / seedYears < 0.15 && manias / seedYears < 0.6 && failures / seedYears < 0.6, "market: crashes, crazes and failures are rare (" + crashes + ", " + manias + ", " + failures + " in " + seedYears + " years)");
        Check(listings / seedYears > 0.8 && listings / seedYears < 5.0 && bids / seedYears > 0.5 && bids / seedYears < 6.0, "market: companies come to the market and are bid for now and then");
    }

    private static List<double> Series(Dictionary<int, List<double>> into, int id, int at)
    {
        if (!into.TryGetValue(id, out List<double> list))
        {
            list = new List<double>();
            into[id] = list;
        }
        while (list.Count < at)
        {
            list.Add(double.NaN);   // (not yet listed)
        }
        return list;
    }

    private static void Mark(Dictionary<int, List<int>> into, int id, int day)
    {
        if (!into.TryGetValue(id, out List<int> list))
        {
            list = new List<int>();
            into[id] = list;
        }
        if (list.Count == 0 || list[list.Count - 1] != day)
        {
            list.Add(day);
        }
    }

    // The rules of thumb a player might try, against holding every company from the first day, after the costs of
    // their trades. A rule may pick riskier companies than the market's (smaller, more volatile ones): those should
    // return more, as they do in real markets, and that is no free lunch. So each rule is measured against what its
    // own picks should have returned (their costs of equity): a rule that beats that by more than holding does, by
    // more than a little, would be a way to make money from nothing.
    private static void MarketNoFreeLunch()
    {
        foreach (int year in new[] { 60, 365 })
        {
            var rules = new[] { "momentum", "losers", "cheap", "dear", "results", "dividends", "trend", "distressed", "fallen" };
            var excess = new double[rules.Length];
            var alpha = new double[rules.Length];
            int runs = 0;
            foreach (MarketPast past in Pasts.Where(p => p.Year == year))
            {
                runs++;
                double hold = Strategy(past, "hold", out double holdK);
                for (int r = 0; r < rules.Length; r++)
                {
                    double got = Strategy(past, rules[r], out double ruleK);
                    excess[r] += got - hold;
                    alpha[r] += (got - ruleK) - (hold - holdK);
                }
            }
            for (int r = 0; r < rules.Length; r++)
            {
                double e = excess[r] / Math.Max(1, runs), a = alpha[r] / Math.Max(1, runs);
                Console.WriteLine("  market rule of thumb (" + year + "-day years): " + rules[r] + " against holding " + (e >= 0 ? "+" : string.Empty) + (e * 100).ToString("0.00")
                                  + "% a year, against what its picks should return " + (a >= 0 ? "+" : string.Empty) + (a * 100).ToString("0.00") + "%");
                Check(a < 0.02, "market: no rule of thumb beats what its picks should return by 2% a year after costs (" + rules[r] + " " + (a * 100).ToString("0.00") + "%, year " + year + ")");
            }
        }
    }

    // A rule's return a year: an equal share in each company it picks, rebalanced every 5 market days, paying each
    // trade's cost; and what its picks should have returned a year (their cost of equity, as held).
    private static double Strategy(MarketPast past, string rule, out double expected)
    {
        int days = past.Days.Count;
        var held = new Dictionary<int, double>();   // share of the money in each company
        double total = 1.0, sumK = 0.0;
        int countK = 0;
        for (int t = 1; t < days; t++)
        {
            // The day's return of what is held.
            double r = 0.0, k = 0.0, w = 0.0;
            foreach (KeyValuePair<int, double> h in held)
            {
                List<double> c = past.Close[h.Key];
                if (t < c.Count && !double.IsNaN(c[t]) && !double.IsNaN(c[t - 1]) && c[t - 1] > 0)
                {
                    r += h.Value * ((c[t] + past.Dividend[h.Key][t]) / c[t - 1] - 1.0);
                }
                List<double> ks = past.K[h.Key];
                if (t - 1 < ks.Count && !double.IsNaN(ks[t - 1]))
                {
                    k += h.Value * ks[t - 1];
                    w += h.Value;
                }
            }
            total *= 1.0 + r;
            if (w > 0.0)
            {
                sumK += k / w;
                countK++;
            }
            bool rebalance = rule == "results" || rule == "dividends" || rule == "fallen" || t % 5 == 1;
            if (!rebalance && held.Count > 0)
            {
                continue;
            }
            var picks = new List<int>();
            foreach (KeyValuePair<int, List<double>> kv in past.Close)
            {
                List<double> c = kv.Value;
                if (t >= c.Count - 1 || double.IsNaN(c[t]) || t + 1 >= c.Count || double.IsNaN(c[t + 1]))
                {
                    continue;
                }
                switch (rule)
                {
                    case "hold": picks.Add(kv.Key); break;
                    case "momentum": if (t >= 20 && !double.IsNaN(c[t - 20]) && c[t] / c[t - 20] > 1.05) picks.Add(kv.Key); break;
                    case "losers": if (t >= 5 && !double.IsNaN(c[t - 5]) && c[t] / c[t - 5] < 0.95) picks.Add(kv.Key); break;
                    case "cheap": { double pe = past.Pe[kv.Key][t]; if (!double.IsNaN(pe) && pe > 0 && pe < 11) picks.Add(kv.Key); } break;
                    case "dear": { double pe = past.Pe[kv.Key][t]; if (!double.IsNaN(pe) && pe > 22) picks.Add(kv.Key); } break;
                    case "results": if (past.Results.TryGetValue(kv.Key, out List<int> rd) && rd.Contains(past.Days[t + 1])) picks.Add(kv.Key); break;
                    case "dividends": if (past.Records.TryGetValue(kv.Key, out List<int> rec) && rec.Contains(past.Days[t])) picks.Add(kv.Key); break;
                    // (the distress of the day before: what changed after today's close cannot be traded until tomorrow's open)
                    case "distressed": { List<double> ds = past.Distress[kv.Key]; if (t >= 1 && t - 1 < ds.Count && ds[t - 1] > MarketCatalogue.DistressFrom) picks.Add(kv.Key); } break;
                    case "fallen":
                        // Bought at the close of the day its price fell on its own bad news (the news came after the close before), held a month.
                        if (past.Crises.TryGetValue(kv.Key, out List<int> cr) && cr.Any(day => day < past.Days[t] && day >= past.Days[Math.Max(0, t - 20)]))
                        {
                            picks.Add(kv.Key);
                        }
                        break;
                    case "trend":
                        if (t >= 20)
                        {
                            double sum = 0.0;
                            bool ok = true;
                            for (int j = t - 19; j <= t; j++)
                            {
                                ok &= !double.IsNaN(c[j]);
                                sum += c[j];
                            }
                            if (ok && c[t] > sum / 20.0)
                            {
                                picks.Add(kv.Key);
                            }
                        }
                        break;
                }
            }
            var next = new Dictionary<int, double>();
            foreach (int id in picks)
            {
                next[id] = 1.0 / picks.Count;
            }
            // What changes hands pays its round trip's cost (half on the way in, half on the way out).
            double turnover = 0.0;
            foreach (int id in held.Keys.Union(next.Keys))
            {
                held.TryGetValue(id, out double was);
                next.TryGetValue(id, out double now);
                turnover += Math.Abs(now - was) * past.Cost[id] / 2.0;
            }
            total *= 1.0 - turnover;
            held = next;
        }
        double years = days / MarketCatalogue.TradingDaysPerYear(past.Year);
        expected = countK > 0 ? sumK / countK : 0.0;
        return Math.Pow(Math.Max(1e-6, total), 1.0 / years) - 1.0;
    }
}
