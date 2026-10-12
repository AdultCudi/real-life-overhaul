using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Markets;
using MarketSector = RealLifeOverhaul.Markets.MarketSector;
using MarketNews = RealLifeOverhaul.Markets.MarketNews;

// Candles folded exactly, the analysis tools against figures worked out by hand, the save's line (round trip, old and
// damaged lines), the tape (a load replays the same days and holds orders until past them; a lost tape re-rolls, a kept
// one never does), the old market brought over once and exactly, the margin account with every safeguard, the city moving
// the market, and what it all costs.
internal static partial class Program
{
    private static void MarketCandles()
    {
        var rnd = new Random(5);
        var daily = new List<Candle>();
        int price = 5000, day = 1;
        for (int i = 0; i < 260; i++)
        {
            while (!MarketCatalogue.TradingDay(day))
            {
                day++;
            }
            int o = Math.Max(1, price + rnd.Next(-60, 61)), c = Math.Max(1, o + rnd.Next(-120, 121));
            int h = Math.Max(o, c) + rnd.Next(0, 80), l = Math.Max(1, Math.Min(o, c) - rnd.Next(0, 80));
            daily.Add(new Candle(day, o, h, l, c, 1000 + rnd.Next(100000)));
            price = c;
            day++;
        }
        foreach (int year in new[] { 60, 365 })
        {
            // Folded as it goes: the last hundred days kept, the rest in months.
            var kept = new List<Candle>();
            var months = new List<Candle>();
            foreach (Candle c in daily)
            {
                MarketHistory.AddDay(kept, months, c, year);
            }
            Check(kept.Count == MarketCatalogue.DailyKept && kept[0].Day == daily[daily.Count - MarketCatalogue.DailyKept].Day, "market: the last hundred days are kept as days (" + year + ")");
            foreach (Timeframe frame in new[] { Timeframe.Month, Timeframe.Year })
            {
                var built = new List<Candle>();
                MarketHistory.Build(kept, months, default(Candle), frame, year, built);
                var direct = Aggregate(daily, frame, year);
                bool same = built.Count == direct.Count;
                for (int i = 0; same && i < built.Count; i++)
                {
                    same = built[i].Day == direct[i].Day && built[i].O == direct[i].O && built[i].H == direct[i].H && built[i].L == direct[i].L && built[i].C == direct[i].C && built[i].V == direct[i].V;
                }
                Check(same, "market: " + frame + " candles from months kept and days kept are exactly those of all the days (" + year + "-day year, " + built.Count + ")");
            }
            var weeks = new List<Candle>();
            MarketHistory.Build(kept, months, default(Candle), Timeframe.Week, year, weeks);
            var directWeeks = Aggregate(kept, Timeframe.Week, year);
            Check(weeks.Count == directWeeks.Count && weeks.Zip(directWeeks, (a, b) => a.O == b.O && a.H == b.H && a.L == b.L && a.C == b.C && a.V == b.V).All(x => x),
                "market: week candles are exactly the days of each week");
        }
        // Today's candle in progress comes last.
        var some = daily.Take(10).ToList();
        var withToday = new List<Candle>();
        MarketHistory.Build(some, new List<Candle>(), new Candle(999, 1, 2, 1, 2, 5), Timeframe.Day, 60, withToday);
        Check(withToday.Count == 11 && withToday[10].Day == 999, "market: today's candle, still being made, is the last one");
        // Steps folded into bars of two.
        var bars = new Candle[6];
        for (int i = 0; i < 6; i++)
        {
            bars[i] = new Candle(1, 100 + i, 110 + i, 90 + i, 101 + i, 10);
        }
        var two = new List<Candle>();
        MarketHistory.Bars(bars, 6, 2, two);
        Check(two.Count == 3 && two[0].O == 100 && two[0].C == 102 && two[0].H == 111 && two[0].L == 90 && two[0].V == 20, "market: two five-minute bars make one of ten minutes exactly");
        MarketHistory.Range(daily, 0, 10, out int low, out int high);
        Check(low == daily.Take(10).Min(c => c.L) && high == daily.Take(10).Max(c => c.H), "market: the range of a stretch of candles");
    }

    private static List<Candle> Aggregate(List<Candle> days, Timeframe frame, int year)
    {
        var result = new List<Candle>();
        int lastPeriod = int.MinValue;
        foreach (Candle c in days)
        {
            int period = MarketHistory.PeriodOf(c.Day, frame, year);
            if (period == lastPeriod)
            {
                Candle a = result[result.Count - 1];
                result[result.Count - 1] = new Candle(a.Day, a.O, Math.Max(a.H, c.H), Math.Min(a.L, c.L), c.C, a.V + c.V);
            }
            else
            {
                int start = frame == Timeframe.Week ? period * 7 + 1 : frame == Timeframe.Month ? MarketHistory.MonthStart(period, year) : MarketHistory.YearStart(period, year);
                result.Add(new Candle(start, c.O, c.H, c.L, c.C, c.V));
                lastPeriod = period;
            }
        }
        return result;
    }

    private static bool Near(double a, double b, double within = 1e-9)
    {
        return Math.Abs(a - b) <= within;
    }

    private static void MarketIndicatorChecks()
    {
        var into = new double[16];
        MarketIndicators.Sma(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10, 3, into);
        Check(double.IsNaN(into[1]) && Near(into[2], 2) && Near(into[9], 9), "market: the simple average of three: 2 at the third, 9 at the tenth");
        MarketIndicators.Ema(new double[] { 1, 2, 3, 4, 5, 6 }, 6, 3, into);
        Check(double.IsNaN(into[1]) && Near(into[2], 2) && Near(into[3], 3) && Near(into[5], 5), "market: the exponential average of three (weight a half) starts at the simple average and follows");
        MarketIndicators.Ema(new double[] { 10, 10, 10, 20 }, 4, 3, into);
        Check(Near(into[3], 15), "market: a jump to 20 after three tens is half taken in (15)");
        var mid = new double[8];
        var up = new double[8];
        var down = new double[8];
        MarketIndicators.Bollinger(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }, 8, 8, 2.0, mid, up, down);
        Check(Near(mid[7], 5) && Near(up[7], 9) && Near(down[7], 1), "market: Bollinger bands of eight with two spreads: 5 with 9 and 1 (the spread is 2)");
        MarketIndicators.Rsi(new double[] { 10, 11, 12, 11, 12, 13 }, 6, 3, into);
        Check(double.IsNaN(into[2]) && Near(into[3], 200.0 / 3.0, 1e-6) && Near(into[4], 700.0 / 9.0, 1e-6) && Near(into[5], 2300.0 / 27.0, 1e-6),
            "market: Wilder's strength index of three: 66.67, 77.78, 85.19 by hand");
        MarketIndicators.Rsi(new double[] { 1, 2, 3, 4, 5 }, 5, 2, into);
        Check(Near(into[4], 100.0), "market: a run of rises only is 100");
        var macd = new double[8];
        var signal = new double[8];
        var hist = new double[8];
        var scratch = new double[8];
        MarketIndicators.Macd(new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 8, 2, 3, 2, macd, signal, hist, scratch);
        Check(double.IsNaN(macd[1]) && Near(macd[2], 0.5) && Near(macd[7], 0.5) && double.IsNaN(signal[2]) && Near(signal[3], 0.5) && Near(hist[7], 0.0),
            "market: on a steady rise the MACD of two and three is a half, its signal the same and the difference nothing");
        var atr = new double[4];
        MarketIndicators.Atr(new double[] { 10, 11, 12, 13 }, new double[] { 8, 9, 10, 11 }, new double[] { 9, 10, 11, 12 }, 4, 3, atr);
        Check(Near(atr[2], 2) && Near(atr[3], 2), "market: the average true range of a steady two-wide range is two");
        MarketIndicators.Atr(new double[] { 10, 15 }, new double[] { 9, 14 }, new double[] { 9.5, 14.5 }, 2, 2, atr);
        Check(Near(atr[1], (1 + 5.5) / 2.0), "market: a gap counts in the true range (5.5 from the close before)");
        var k = new double[4];
        var d = new double[4];
        MarketIndicators.Stochastic(new double[] { 5, 6, 7, 8 }, new double[] { 1, 2, 3, 4 }, new double[] { 3, 5, 6, 7 }, 4, 3, 2, k, d);
        Check(Near(k[2], 500.0 / 6.0, 1e-9) && Near(k[3], 500.0 / 6.0, 1e-9) && Near(d[3], 500.0 / 6.0, 1e-9), "market: the stochastic %K of three is 83.33, and %D its average");
        var obv = new double[5];
        MarketIndicators.Obv(new double[] { 10, 11, 10, 10, 12 }, new double[] { 100, 200, 300, 400, 500 }, 5, obv);
        Check(Near(obv[1], 200) && Near(obv[2], -100) && Near(obv[3], -100) && Near(obv[4], 400), "market: on-balance volume adds on a rise and takes on a fall");
    }

    // A market with something of everything in it: positions, orders, the history, news, a margin account.
    private static MarketEngine Busy(ulong seed, int days, out MarketWorld world)
    {
        world = new MarketWorld(seed);
        world.Purse.Money = 1000000000;
        MarketEngine m = NewMarket(seed, 60, 1, 0, world);
        SessionDay(m, 2, 12);
        Stock a = Liquid(m, MarketSize.Large), b = Liquid(m, MarketSize.Mid);
        Trade(m, a, OrderSide.Buy, 400);
        Trade(m, b, OrderSide.Buy, 250);
        Trade(m, b, OrderSide.Sell, 50, OrderType.Limit, b.Last * 3, 0, OrderLife.UntilCancelled);
        Trade(m, a, OrderSide.Buy, 25, OrderType.Limit, Math.Max(1, a.Last / 2), 0, OrderLife.UntilCancelled);
        m.State.Watch.Add(a.Id);
        m.State.Watch.Add(Liquid(m, MarketSize.Small).Id);
        RunTo(m, days, MarketCatalogue.PhaseOpen + 30);
        return m;
    }

    private static void MarketStoreChecks()
    {
        MarketEngine m = Busy(121, 70, out _);
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out int version);
        Check(back != null && version == MarketStore.Version, "market: the save's line reads back");
        string again = back != null ? MarketStore.Write(back) : null;
        Check(again == line, "market: and writes again exactly the same (" + line.Length + " characters" + FirstDifference(line, again) + ")");
        bool same = back != null && back.Stocks.Count == m.State.Stocks.Count;
        for (int i = 0; same && i < m.State.Stocks.Count; i++)
        {
            Stock x = m.State.Stocks[i], y = back.Stocks[i];
            same = x.Id == y.Id && x.Last == y.Last && x.LogFair == y.LogFair && x.Mis == y.Mis && x.Shares == y.Shares && x.Daily.Count == y.Daily.Count && x.Rev == y.Rev && x.Margin == y.Margin
                   && x.DpsCents == y.DpsCents && x.Results.Count == y.Results.Count;
        }
        Check(same, "market: every company's price, business and history come back as they were");
        Check(back != null && back.Positions.Count == m.State.Positions.Count && back.Positions.All(p => m.State.Position(p.StockId).Shares == p.Shares && m.State.Position(p.StockId).Cost == p.Cost),
            "market: every holding comes back with its shares and cost");
        Check(back != null && back.Orders.Count == m.State.Orders.Count && back.Watch.SequenceEqual(m.State.Watch) && back.Trades.Count == m.State.Trades.Count && back.Now == m.State.Now
              && back.IndexLast == m.State.IndexLast && back.IndexDaily.Count == m.State.IndexDaily.Count, "market: orders, the watch list, the account's history, the clock and the index come back");
        // The market carries on after a load exactly as if there had been none (no player trade in between, and the same
        // fresh secrets from here on as the market that was saved would have drawn).
        var again2 = new MarketWorld(121) { Secrets = WorldOf(m).Secrets };
        again2.Purse.Money = WorldOf(m).Purse.Money;
        MarketEngine fresh = new MarketEngine(back, MarketTape.Read(m.Tape.Id, m.Tape.Whole()), again2);
        RunTo(m, 90);
        RunTo(fresh, 90);
        bool carriesOn = fresh.State.Stocks.All(s => m.State.Stock(s.Id) != null && m.State.Stock(s.Id).Last == s.Last);
        Check(carriesOn, "market: a market loaded from its line and its tape plays the same days as the one that was saved");
        // Old, newer and damaged lines.
        Check(MarketStore.Read("v=0~h:1", out _) == null && MarketStore.Read("nonsense", out _) == null && MarketStore.Read(string.Empty, out _) == null, "market: a line that is not one is not read");
        int survived = 0, tried = 0;
        foreach (int cut in new[] { 10, 100, 1000, line.Length / 3, line.Length / 2, line.Length - 50 })
        {
            tried++;
            var data = new Dictionary<string, string> { { MarketStore.Key, line.Substring(0, Math.Min(line.Length, cut)) } };
            MarketState damaged = MarketStore.Load(data, out _);   // never throws
            survived += damaged == null || damaged.Stocks.Count > 0 ? 1 : 0;
        }
        Check(survived == tried, "market: a line cut short anywhere is read as far as it goes or not at all, never with a fault");
        string later = line + "~z:something a later version wrote|1|2";
        MarketState newer = MarketStore.Read(later, out _);
        Check(newer != null && MarketStore.Write(newer) == line, "market: a part this version does not know is passed over");
        // A company's record lost from the line: it is made again so that the holding in it still points at a company.
        Position held = m.State.Positions[0];
        string withoutIt = RemoveCompany(line, held.StockId);
        MarketState repaired = MarketStore.Read(withoutIt, out _);
        Check(repaired != null && repaired.Stock(held.StockId) != null && repaired.Position(held.StockId) != null && repaired.Position(held.StockId).Shares == held.Shares,
            "market: a holding in a company missing from the line keeps its shares, and the company is made again");
        var garbled = line.Replace("|1|", "|x|");
        Check(MarketStore.Load(new Dictionary<string, string> { { MarketStore.Key, garbled } }, out _) != null, "market: a field that is not a number is read as nothing, not as a fault");
        // What a long game costs the save.
        MarketEngine longer = NewMarket(122, 60);
        RunTo(longer, 600, MarketCatalogue.PhaseOpen + 40);
        int size = MarketStore.Write(longer.State).Length;
        Console.WriteLine("  market save line after ten 60-day years: " + (size / 1024) + " KB (" + longer.State.Stocks.Count + " companies, " + MarketCatalogue.DailyKept + " daily candles each)");
        Check(size < 512 * 1024, "market: the save's line stays small (" + (size / 1024) + " KB)");
        Check(!line.Contains(m.Tape.Whole().Split('\n').Where(l => l.StartsWith("D ")).Select(l => l.Split(' ')[2]).FirstOrDefault() ?? "#"),
            "market: no secret of the market's chance is ever in the save");
    }

    // Where two lines first differ, for a check that fails.
    private static string FirstDifference(string a, string b)
    {
        if (a == null || b == null || a == b)
        {
            return string.Empty;
        }
        int i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }
        int from = Math.Max(0, i - 60);
        return "; they differ at " + i + ": ..." + a.Substring(from, Math.Min(120, a.Length - from)) + " against ..." + b.Substring(from, Math.Min(120, b.Length - from));
    }

    private static string RemoveCompany(string line, int id)
    {
        int k = line.IndexOf("~k:", StringComparison.Ordinal);
        int end = line.IndexOf('~', k + 3);
        string[] records = line.Substring(k + 3, end - k - 3).Split(';');
        string kept = string.Join(";", records.Where(r => !r.StartsWith(id + "|", StringComparison.Ordinal)));
        return line.Substring(0, k + 3) + kept + line.Substring(end);
    }

    private static void MarketTapeChecks()
    {
        // A day played twice, from the same save, with the tape: the same day (no re-roll of what was seen).
        var world = new MarketWorld(131);
        MarketEngine first = NewMarket(131, 60, 1, 0, world);
        RunTo(first, 20, MarketCatalogue.PhaseOpen + 5);
        string save = MarketStore.Write(first.State);
        // News of the city during the days that follow, which the replay must answer the same way.
        RunTo(first, 21, MarketCatalogue.PhaseOpen + 10);
        first.Event(MarketInputs.EventFire, 3, 0);
        first.Event(MarketInputs.EventStorm, 0, 0);
        RunTo(first, 34);
        var seen = first.State.Stocks.ToDictionary(s => s.Id, s => s.Last);
        var seenResults = first.State.Stocks.Where(s => s.Results.Count > 0).ToDictionary(s => s.Id, s => s.Results[s.Results.Count - 1].Eps);
        string tapeText = first.Tape.Whole();
        MarketTape tape = MarketTape.Read(first.Tape.Id, tapeText);
        Check(tape.Frontier == first.State.Now && tape.Days == first.Tape.Days, "market: the tape reads back with every day's secret and the frontier");
        Check(MarketTape.Read(first.Tape.Id + 1, tapeText).Days == 0, "market: a tape of another playthrough is not used");
        var otherWorld = new MarketWorld(9999);   // whatever chance this session would have
        var replay = new MarketEngine(MarketStore.Read(save, out _), tape, otherWorld);
        replay.HoldAfterLoad();
        replay.Event(MarketInputs.EventFire, 9, 0);   // news of this session's own, while the tape is followed: ignored
        RunTo(replay, 34);
        bool same = replay.State.Stocks.All(s => !seen.ContainsKey(s.Id) || seen[s.Id] == s.Last);
        bool sameResults = replay.State.Stocks.Where(s => seenResults.ContainsKey(s.Id) && s.Results.Count > 0).All(s => s.Results[s.Results.Count - 1].Eps == seenResults[s.Id]);
        Check(same && sameResults, "market: a save loaded again replays the same prices and the same results: a load cannot re-roll them");
        Check(otherWorld.Secrets == 0, "market: no fresh secret is drawn for a day the tape has");
        // Without its tape (deleted), the days are new ones: that is what the tape is for.
        var lost = new MarketEngine(MarketStore.Read(save, out _), new MarketTape(first.Tape.Id), new MarketWorld(4242));
        RunTo(lost, 34);
        Check(lost.State.Stocks.Count(s => seen.ContainsKey(s.Id) && seen[s.Id] != s.Last) > 50, "market: without the tape the days would be new ones (which is why it is kept)");
        // Past the frontier the replay goes on with fresh days, written to the tape.
        int daysBefore = tape.Days;
        RunTo(replay, 36);
        Check(otherWorld.Secrets > 0 && replay.Tape.Days > daysBefore && !replay.Held, "market: past the frontier the market goes on with fresh days, and takes orders again");
        // A damaged tape's lines are passed over: one day's line broken, rubbish added; every other day is still read.
        int cut = tapeText.IndexOf("\nD ", StringComparison.Ordinal);
        string broken = cut >= 0 ? tapeText.Substring(0, cut + 3) + "x" + tapeText.Substring(cut + 3) : tapeText;
        MarketTape damaged = MarketTape.Read(first.Tape.Id, broken + "\nrubbish\nE 1 2");
        int written = MarketTape.Read(first.Tape.Id, tapeText).Days;
        Check(cut >= 0 && damaged.Days == written - 1, "market: a damaged line of the tape is passed over and the rest is read (" + damaged.Days + " of " + written + " days)");
        // The city of each day is on the tape: a replay reads it, not this session's city.
        Check(tape.Env(TradingOnOrAfter(25), out MarketEnv env) && env.Supply != null, "market: the city of each day played is on the tape");
    }

    private static void MarketCarryOverChecks()
    {
        var file = new VenturesFile();
        Cast.Make(file, 4242, new List<string> { "Ada Holdings", "Bram Holdings" });
        Check(file.Companies.Count == 12 && file.Companies.Count(c => c.Rival) == 2, "market: an old Ventures file with twelve companies, two of them rivals");
        // Prices that have moved since, and holdings in five of them (one a test's, one a rival's).
        for (int i = 0; i < file.Companies.Count; i++)
        {
            file.Companies[i].PriceCents += 137 * i;
            file.Companies[i].Remember(file.Companies[i].PriceCents);
        }
        var held = new[] { 0, 3, 5, 8, 9 };
        foreach (int i in held)
        {
            file.Holdings.Add(new Holding { CompanyId = file.Companies[i].Id, Shares = 100 + i * 7, Cost = 5000 + i * 333, Dividends = i * 11, Test = i == 5 });
        }
        var expected = file.Holdings.Select(h => (h.CompanyId, h.Shares, h.Cost, h.Dividends, h.Test)).ToList();
        List<CarriedPrice> prices = MarketCarryOver.Prices(file);
        var world = new MarketWorld(4242);
        MarketState state = MarketSetup.Open(77, 30, 600, 60, world.Env(30), 77, new List<string> { "Ada Holdings", "Bram Holdings", "Cleo Holdings" }, new List<string> { "a", "b", "c" }, prices);
        bool pricesKept = true;
        foreach (Company c in file.Companies)
        {
            Stock s = state.Stock(MarketCarryOver.IdOf(c, state));
            pricesKept &= s != null && s.Last == c.PriceCents && s.Daily.Count > 0 && s.Daily[s.Daily.Count - 1].C == c.PriceCents;
        }
        Check(pricesKept, "market: each old company opens here at its old price, with its last closes");
        Check(MarketSetup.Rival(state, "Ada Holdings") != null && MarketSetup.Rival(state, "Bram Holdings") != null && MarketSetup.Rival(state, "Cleo Holdings") != null,
            "market: the rivals the old market listed are listed under the same names, and the game's others after them");
        long cash = 1000;
        world.Purse.Money = cash;
        MarketCarryOver.Result moved = MarketCarryOver.Move(file, state, world.Purse);
        bool exact = true;
        foreach ((int company, int shares, int cost, int dividends, bool test) in expected)
        {
            Company c = file.Company(company);
            Position p = state.Position(MarketCarryOver.IdOf(c, state));
            exact &= p != null && p.Shares == shares && p.Cost == cost && p.Dividends == dividends && p.Test == test;
        }
        Check(moved.Moved == expected.Count && exact, "market: every holding comes over with exactly its shares, what it cost, its dividends and its test mark");
        Check(world.Purse.Money == cash && moved.PaidOut == 0, "market: no money moves when the holdings come over");
        Check(file.Holdings.Count == 0 && file.MarketMoved && state.CarriedOver, "market: the old file keeps no holdings and is marked as moved");
        MarketCarryOver.Result second = MarketCarryOver.Move(file, state, world.Purse);
        Check(second.Moved == 0 && state.Positions.Count == expected.Count && state.Positions.Sum(p => p.Shares) == expected.Sum(e => (long)e.Shares), "market: a second look brings nothing: nothing is ever brought twice");
        // The flag survives the old file's own save line, so a load later brings nothing either.
        VenturesFile reread = VenturesStore.Read(VenturesStore.Write(file), out _);
        Check(reread != null && reread.MarketMoved && reread.Holdings.Count == 0 && !MarketCarryOver.Waiting(reread), "market: the old file's line keeps the mark");
        // A holding whose company has gone from the new market is paid out at its old price, so nothing is lost.
        var file2 = new VenturesFile();
        Cast.Make(file2, 77, null);
        Company gone = file2.Companies[0];
        file2.Holdings.Add(new Holding { CompanyId = gone.Id, Shares = 40, Cost = 999 });
        MarketState state2 = MarketSetup.Open(78, 30, 600, 60, world.Env(30), 78, null, null, MarketCarryOver.Prices(file2));
        state2.Stock(MarketCarryOver.IdOf(gone, state2)).Status = StockStatus.Gone;
        var purse = new MarketWallet();
        MarketCarryOver.Result paid = MarketCarryOver.Move(file2, state2, purse);
        Check(paid.PaidOut == 1 && purse.Money == 40L * gone.PriceCents / 100, "market: a holding with nowhere to go is paid out at its old price, with no commission");
    }

    private static void MarketMargin()
    {
        var world = new MarketWorld(141);
        world.Purse.Money = 100000000;
        MarketEngine m = NewMarket(141, 60, 1, 0, world);
        int day = SessionDay(m, 2, 10);
        var a = m.State.Account;
        a.Margin = true;
        a.Cash = 200000;   // deposited
        Stock s = Liquid(m, MarketSize.Large);
        long room = 200000L * 100 / MarketCatalogue.InitialMarginPct;   // half of every position in equity: twice the equity in all
        long qty = room * 100 / Math.Max(1, s.Last) * 100 / 105;         // shares worth a little under it
        Check(Why(m, s, OrderSide.Buy, qty) == OrderWhy.None, "market: a margin account may buy twice its equity");
        Check(Why(m, s, OrderSide.Buy, qty * 3) == OrderWhy.BelowMargin, "market: and no more");
        Trade(m, s, OrderSide.Buy, qty);
        Check(a.Cash < 0, "market: what is bought beyond the account's money is borrowed (" + a.Cash + ")");
        long owed = -a.Cash;
        RunTo(m, day);
        Check(a.Interest > 0 && a.Interest >= owed * (MarketValuation.BankRate(m.State.Env) + MarketCatalogue.MarginRateOverBp / 10000.0) / 60.0, "market: interest is charged on what is borrowed, at the bank rate and three points, a day of the year");
        // Shares sold short: only of a company that can be borrowed, and a fee to borrow them.
        Stock small = Liquid(m, MarketSize.Small);
        RunTo(m, TradingOnOrAfter(day + 1), MarketCatalogue.PhaseOpen + 5);
        Check(Why(m, small, OrderSide.Sell, 10) == OrderWhy.NoBorrow, "market: a small company's shares cannot be borrowed to sell short");
        Stock big = Liquid(m, MarketSize.Large, 1);
        Trade(m, big, OrderSide.Sell, 100);
        Check(m.State.Held(big.Id) == -100, "market: a company's shares sold short are owed");
        long fees = a.BorrowFees;
        RunTo(m, m.State.Day);
        Check(a.BorrowFees > fees, "market: borrowing shares costs a fee a day");
        // A margin call and the broker selling.
        var w2 = new MarketWorld(142);
        w2.Purse.Money = 100000000;
        MarketEngine e = NewMarket(142, 60, 1, 0, w2);
        int d2 = SessionDay(e, 2, 10);
        e.State.Account.Margin = true;
        e.State.Account.Cash = 100000;
        Stock t = Liquid(e, MarketSize.Large);
        Trade(e, t, OrderSide.Buy, 190000L * 100 / Math.Max(1, t.Last));
        t.Mis -= 0.48;   // the price falls hard: under the maintenance, not yet so far that the broker sells at once
        t.Last = t.MidCents;
        RunTo(e, d2);
        Check(w2.Calls == 1 && e.State.Account.CallDay > d2, "market: under its maintenance at the close the account gets a margin call");
        int callDay = e.State.Account.CallDay;
        RunTo(e, callDay, MarketCatalogue.PhaseOpen + 40);
        t.Mis -= 0.03;   // (and the price does not come back by the close of the day the call must be met by)
        RunTo(e, MarketTrading.NextTradingDay(callDay), MarketCatalogue.PhaseOpen + 2);
        long equity = MarketTrading.Equity(e.State, e.State.Phase), need = MarketTrading.Maintenance(e.State, e.State.Phase);
        Check(e.State.Trades.Any(x => x.Kind == TradeKind.Sold) && (e.State.Positions.Count == 0 || equity >= need),
            "market: unmet by its day, the broker sells until the account is sound (" + equity + " against " + need + ", call " + e.State.Account.CallDay + ")");
        // A gap through nothing: what is still owed when everything is sold is taken from the player's cash.
        var w3 = new MarketWorld(143);
        w3.Purse.Money = 50000000;
        MarketEngine g = NewMarket(143, 60, 1, 0, w3);
        SessionDay(g, 2, 10);
        g.State.Account.Margin = true;
        g.State.Account.Cash = 50000;
        Stock u = Liquid(g, MarketSize.Large);
        Trade(g, u, OrderSide.Buy, 99000L * 100 / Math.Max(1, u.Last));
        u.Mis -= 0.95;
        u.Last = u.MidCents;
        RunTo(g, g.State.Day, g.State.Phase + 1);
        Check(g.State.Positions.Count == 0 && g.State.Account.Cash == 0 && w3.Purse.Forced > 0, "market: far under its maintenance the broker sells at once, and a shortfall is taken from the player's cash (" + w3.Purse.Forced + ")");
        // A short owes the dividend; a short in a company that fails gains.
        Check(MarketTrading.Borrowable(m.State, big, m.State.Day) && !MarketTrading.Borrowable(m.State, small, m.State.Day), "market: whether shares can be borrowed is by size, age, distress and bids");
        long cashBefore = a.Cash;
        m.Delist(big.Id, GoneReason.Failed, 0);
        Check(m.State.Held(big.Id) == 0 && a.Cash == cashBefore && m.State.Trades.Any(x => x.Kind == TradeKind.Payout && x.StockId == big.Id), "market: shares sold short of a company that fails are owed no more");
    }

    // The city moves the market: two markets with the same chance, one city changed.
    private static void MarketCity()
    {
        // (from the close of a Wednesday: the next days are market days)
        double Index(Action<MarketWorld> change, int days, Func<MarketState, double> read)
        {
            var w = new MarketWorld(151);
            MarketEngine m = NewMarket(151, 60, 1, 0, w);
            RunTo(m, 3);
            change(w);
            RunTo(m, 3 + days);
            return read(m.State);
        }
        Func<MarketState, double> index = st => st.IndexLast;
        double plain = Index(w => { }, 10, index);
        Check(Index(w => w.City.RateBp = 500, 10, index) < plain * 0.97, "market: a bank rate two points higher lowers the market");
        Check(Index(w => w.City.Cycle = 3, 10, index) < plain * 0.95, "market: a recession lowers the market");
        Check(Index(w => w.City.Cycle = 0, 10, index) > plain, "market: a boom raises it");
        Func<MarketSector, Func<MarketState, double>> sector = sec => st => st.SectorLast[(int)sec];
        double cyclical = Index(w => w.City.Cycle = 3, 10, sector(MarketSector.Construction)) / Index(w => { }, 10, sector(MarketSector.Construction));
        double defensive = Index(w => w.City.Cycle = 3, 10, sector(MarketSector.Water)) / Index(w => { }, 10, sector(MarketSector.Water));
        Check(cyclical < defensive, "market: a recession hits building firms harder than water companies (" + cyclical.ToString("0.000") + " against " + defensive.ToString("0.000") + ")");
        double groceries = Index(w => w.City.Supply[1] = 1350, 3, sector(MarketSector.Restaurants)) / Index(w => { }, 3, sector(MarketSector.Restaurants));
        Check(groceries < 1.0, "market: a shortage of groceries and drink weighs on restaurants");
        double taxed = Index(w => w.City.Levers = MarketEnv.Packed(new[] { 0, 0, 1 }), 3, index);
        Check(taxed < plain, "market: a higher business tax lowers the market");
        double stormy = Index(w => w.City.Sky = 2, 1, sector(MarketSector.Insurance)) / Index(w => { }, 1, sector(MarketSector.Insurance));
        Check(stormy < 1.0, "market: a storm weighs on insurers");
        double inflation = Index(w => w.City.InflationBp = 800, 40, st => st.Stocks.Where(s => s.Listed).Sum(s => s.Rev)) / Index(w => { }, 40, st => st.Stocks.Where(s => s.Listed).Sum(s => s.Rev));
        Check(inflation > 1.0, "market: inflation lifts the companies' turnover (their prices follow it through their earnings)");
        int supermarket = Array.IndexOf(MarketInputs.Trades, "supermarket");
        Func<MarketState, double> grocers = st => st.Stocks.Where(s => s.Listed && s.Def.Trade == "supermarket").Sum(s => s.Rev);
        double competed = Index(w => { w.City.Trades = 1; w.City.TradeOf[0] = supermarket; w.City.TradeSales[0] = 20000000; }, 5, grocers) / Index(w => { }, 5, grocers);
        Check(competed < 0.999, "market: the player's own supermarkets take turnover from the listed supermarket chains (" + competed.ToString("0.0000") + ")");
        // News during the day: a fire at a business moves insurers at once.
        var fw = new MarketWorld(152);
        MarketEngine fire = NewMarket(152, 60, 1, 0, fw);
        MarketEngine calm = NewMarket(152, 60, 1, 0, new MarketWorld(152));
        SessionDay(fire, 2, 20);
        SessionDay(calm, 2, 20);
        fire.Event(MarketInputs.EventFire, -1, 0);
        RunTo(fire, fire.State.Day, fire.State.Phase + 1);
        RunTo(calm, calm.State.Day, calm.State.Phase + 1);
        Check(fire.State.SectorLast[(int)MarketSector.Insurance] < calm.State.SectorLast[(int)MarketSector.Insurance], "market: a fire reported during the day moves insurers' shares at once");
    }

    private static void MarketCost()
    {
        MarketEngine m = NewMarket(161, 60);
        RunTo(m, 3);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        int day = 3;
        for (int i = 0; i < 20; i++)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day);
        }
        double perDay = watch.Elapsed.TotalMilliseconds / 20.0;
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 20;
        Console.WriteLine("  market cost: a market day of " + m.State.Stocks.Count(s => s.Listed) + " companies, 78 steps, " + perDay.ToString("0.00") + " ms; " + bytes + " bytes allocated a day (news and the tape's lines)");
        Check(perDay < 12.0, "market: a whole market day costs a few milliseconds (" + perDay.ToString("0.00") + " ms), a step a few dozen microseconds");
        Check(bytes < 64 * 1024, "market: and allocates next to nothing (" + bytes + " bytes a day)");
    }
}
