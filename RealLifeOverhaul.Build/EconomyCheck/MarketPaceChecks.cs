using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Markets;

// The market's pace (Scripts/Markets/MarketEngine: RunFor, Midway, Finish; MarketValuation.Terms): the open and the close
// worked through a company at a time over frames give the very market the one-frame open and close gave, to the last
// bit, whether the time runs out after every piece or never, and whatever is asked of the market between two pieces;
// the catch-up keeps its frame's time; a company's worth taken apart once gives Worth's own bits for every expectation
// (against the sum written out whole, as it was before), and the search for an outlook the same outlook; the save's
// line and the tape written with their kept builders are the same text every time.
internal static partial class Program
{
    private static void MarketPace()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        PaceWorthSameBits();
        PaceOutlookSameBits();
        PaceCloseInPieces();
        PaceAskedMidway();
        PaceCatchUpBudget();
        PaceSettleCost();
        PaceWritersSameText();
        Console.WriteLine("  the market's pace: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the worth, written out whole (as it was before Terms)

    private static readonly double[] RefOwnFade = RefFade(MarketCatalogue.GrowthFadeYears);
    private static readonly double[] RefPatchFade = RefFade(MarketValuation.PatchFadeYears);
    private static readonly double[] RefPriceFade = RefFade(MarketValuation.InflationFadeYears);
    private static readonly double RefDebtYear = Math.Exp(-1.0 / MarketCatalogue.DebtRepriceYears), RefDebtHalfYear = Math.Exp(-0.5 / MarketCatalogue.DebtRepriceYears);
    private static readonly double[] RefOutlookShare = RefShares();

    private static double[] RefFade(double years)
    {
        var f = new double[MarketValuation.Horizon + 1];
        f[0] = 1.0;
        for (int t = 1; t <= MarketValuation.Horizon; t++)
        {
            f[t] = Math.Exp(-(t - 0.5) / years);
        }
        return f;
    }

    private static double[] RefShares()
    {
        var f = new double[MarketValuation.Horizon + 2];
        for (int t = 1; t <= MarketValuation.Horizon + 1; t++)
        {
            f[t] = 1.0 - Math.Exp(-(t - 0.5) / MarketCatalogue.OutlookYears);
        }
        return f;
    }

    private static double RefYearRate(double k, double distress, double least)
    {
        return Math.Max(1.0 + least, (1.0 + k) * Math.Exp(MarketValuation.Hazard(distress) * MarketValuation.LossOnFailure(distress)));
    }

    // MarketValuation.Worth as it was written up to 0.17.1: the whole sum for each expectation.
    private static double RefWorth(Stock s, in MarketEnv env, double outlook)
    {
        if (s.Shares <= 0)
        {
            return 0.01;
        }
        CompanyDef d = s.Def;
        double tax = MarketInputs.TaxRate(env);
        double usual = MarketValuation.UsualMargin(s, env);
        double patch = MarketValuation.CycleMargin(s, env) + (s.Margin - s.MarginTarget);
        double supply = MarketInputs.SupplyBp(env, s.Sector) / 10000.0;
        double cost = MarketValuation.InflationCost(s, env);
        double debtNow = s.Rev > 0.0 ? s.Debt / s.Rev : 0.0;
        double debtUsual = s.Def.DebtPct / 100.0;
        double rateNow = s.DebtRate;
        double rateUsual = MarketValuation.UsualDebtRate(s, env);
        double cycle = MarketValuation.CycleMargin(s, env);
        double k = MarketValuation.Rate(s, env);
        double gT = MarketValuation.TerminalGrowth(s);
        double least = gT + MarketValuation.SpreadLeast;
        double pass = d.PassPct / 100.0;
        double inflation = env.InflationBp / 10000.0;
        double city = MarketValuation.CityGrowth(s, env);
        double own = s.Growth - s.GrowthTarget;
        double payoutT = s.Own != null ? ListingCatalogue.OwnPayout : MarketValuation.PayoutOf(s, gT);
        double rev = s.Rev, discount = 1.0, sum = 0.0, debt = debtNow, rate = rateNow;
        for (int t = 1; t <= MarketValuation.Horizon; t++)
        {
            double real = MarketCatalogue.LongRealGrowth + own * RefOwnFade[t] + city * RefPatchFade[t];
            double prices = MarketValuation.TargetInflation + (inflation - MarketValuation.TargetInflation) * RefPriceFade[t];
            rev *= 1.0 + Math.Max(-0.3, real + prices * pass);
            double interest = (debtUsual + (debt - debtUsual) * RefDebtHalfYear) * (rateUsual + (rate - rateUsual) * RefDebtHalfYear);
            double margin = usual + patch * RefPatchFade[t] - cost * RefPriceFade[t] + (t == 1 ? supply * MarketValuation.SupplyYears : 0.0);
            double profit = rev * (margin - interest);
            double distress = MarketValuation.DistressAt(usual + cycle * RefPatchFade[t] - cost * RefPriceFade[t], interest);
            discount /= RefYearRate(k, distress, least);
            if (profit > 0.0)
            {
                sum += (s.Own != null ? ListingCatalogue.OwnPayout : MarketValuation.PayoutOf(s, real + prices * pass)) * profit * (1.0 - tax) * discount * Math.Exp(outlook * RefOutlookShare[t]);
            }
            debt = debtUsual + (debt - debtUsual) * RefDebtYear + (profit < 0.0 ? -profit / rev : 0.0);
            rate = rateUsual + (rate - rateUsual) * RefDebtYear;
        }
        double interestUsual = debtUsual * rateUsual;
        double last = rev * (usual - interestUsual);
        double distressUsual = MarketValuation.DistressAt(usual, interestUsual);
        double kT = RefYearRate(k, distressUsual, least) - 1.0;
        double expected = Math.Exp(outlook * RefOutlookShare[MarketValuation.Horizon + 1]);
        if (last > 0.0)
        {
            sum += payoutT * last * (1.0 - tax) * (1.0 + gT) / (kT - gT) * discount * expected;
        }
        if (TrustRules.Is(s))
        {
            sum *= TrustRules.PropertyFactor(env);
        }
        double value = sum / s.Shares;
        double floor = Math.Max(1e-6, 0.03 * s.Rev / s.Shares * expected);
        double x = Math.Max(-30.0, value / floor);
        return x > 30.0 ? value : floor * Math.Log(1.0 + Math.Exp(x));
    }

    // MarketValuation.OutlookFor as it was: the secant search over the whole sum.
    private static double RefOutlookFor(Stock s, in MarketEnv env, double worth, double from)
    {
        if (!(worth > 0.0))
        {
            return from;
        }
        double target = Math.Log(worth);
        double a = MarketValuation.Clamp(from, -8.0, 8.0), ga = Math.Log(RefWorth(s, env, a)) - target;
        if (Math.Abs(ga) < 1e-10)
        {
            return a;
        }
        double b = MarketValuation.Clamp(a - ga / 0.8, -8.0, 8.0), gb = Math.Log(RefWorth(s, env, b)) - target;
        for (int i = 0; i < 12 && Math.Abs(gb) > 1e-10; i++)
        {
            double slope = Math.Abs(b - a) > 1e-12 ? (gb - ga) / (b - a) : 0.8;
            if (!(slope > 1e-6))
            {
                slope = 0.8;
            }
            double c = MarketValuation.Clamp(b - gb / slope, -8.0, 8.0);
            if (c == b)
            {
                break;
            }
            a = b;
            ga = gb;
            b = c;
            gb = Math.Log(RefWorth(s, env, b)) - target;
        }
        return b;
    }

    private static bool SameBits(double a, double b)
    {
        return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }

    // Companies in every state the worth knows: as listed, after months of play (crises, bids, trusts, splits), and pushed
    // to the edges by hand (losses for years, debt far over its usual, no shares, a tiny turnover, a deep patch).
    private static List<(Stock s, MarketEnv env)> PaceCompanies()
    {
        var all = new List<(Stock, MarketEnv)>();
        foreach (ulong seed in new ulong[] { 401, 402, 403 })
        {
            MarketEngine m = NewMarket(seed, 60);
            RunTo(m, seed == 401 ? 2 : seed == 402 ? 90 : 240);
            foreach (Stock s in m.State.Stocks)
            {
                all.Add((s, m.State.Env));
            }
        }
        MarketEngine edge = NewMarket(409, 60);
        RunTo(edge, 30);
        var rng = new Random(409);
        int n = 0;
        foreach (Stock s in edge.State.Stocks.Where(k => k.Listed).Take(60))
        {
            switch (n++ % 6)
            {
                case 0:
                    s.Margin = -0.4;
                    s.MarginTarget = 0.1;
                    break;
                case 1:
                    s.Debt = s.Rev * (2.0 + rng.NextDouble() * 6.0);
                    s.DebtRate = 0.2;
                    break;
                case 2:
                    s.Shares = 0;
                    break;
                case 3:
                    s.Rev = 1e-3;
                    break;
                case 4:
                    s.MarginShift = -0.3;
                    s.Growth = -0.2;
                    break;
                default:
                    s.Growth = 0.5;
                    s.MarginShift = 0.1;
                    break;
            }
            all.Add((s, edge.State.Env));
        }
        MarketEnv hard = edge.State.Env;
        hard.Cycle = 3;
        hard.InflationBp = 1400;
        hard.RateBp = 1200;
        foreach (Stock s in edge.State.Stocks.Where(k => k.Listed).Skip(60).Take(30))
        {
            all.Add((s, hard));
        }
        return all;
    }

    private static readonly double[] PaceOutlooks = { -8.0, -5.25, -3.3, -1.0, -0.37, -0.01, 0.0, 1e-9, 0.12, 0.37, 1.0, 2.0, 5.5, 7.999, 8.0 };

    private static void PaceWorthSameBits()
    {
        List<(Stock s, MarketEnv env)> companies = PaceCompanies();
        var terms = new MarketValuation.Terms();
        var rng = new Random(7);
        int tried = 0, same = 0, loss = 0, none = 0, trusts = 0;
        foreach ((Stock s, MarketEnv env) in companies)
        {
            terms.Fill(s, env);
            loss += s.Margin < 0.0 ? 1 : 0;
            none += s.Shares <= 0 ? 1 : 0;
            trusts += TrustRules.Is(s) ? 1 : 0;
            for (int i = 0; i < PaceOutlooks.Length + 6; i++)
            {
                double o = i < PaceOutlooks.Length ? PaceOutlooks[i] : rng.NextDouble() * 16.0 - 8.0;
                double want = RefWorth(s, env, o);
                tried += 2;
                same += SameBits(MarketValuation.Worth(s, env, o), want) ? 1 : 0;
                same += SameBits(terms.Worth(o), want) ? 1 : 0;
            }
            tried++;
            same += SameBits(MarketValuation.Fair(s, env), RefWorth(s, env, s.Outlook)) ? 1 : 0;
        }
        Check(companies.Count > 400 && loss > 5 && none > 5 && trusts > 5,
            "pace: the worth is tried on " + companies.Count + " companies, among them losses for years (" + loss + "), none with shares (" + none + ") and trusts (" + trusts + ")");
        Check(same == tried, "pace: a company's worth taken apart once gives the whole sum's very bits at every expectation, filled once or asked whole (" + same + " of " + tried + ")");
    }

    private static void PaceOutlookSameBits()
    {
        List<(Stock s, MarketEnv env)> companies = PaceCompanies();
        var terms = new MarketValuation.Terms();
        var rng = new Random(11);
        int tried = 0, same = 0, moved = 0;
        foreach ((Stock s, MarketEnv env) in companies)
        {
            terms.Fill(s, env);
            for (int i = 0; i < 6; i++)
            {
                double worth = i == 0 ? 0.0 : i == 1 ? -3.0 : RefWorth(s, env, s.Outlook) * Math.Exp(rng.NextDouble() * 1.6 - 0.8);
                double from = i < 4 ? s.Outlook : rng.NextDouble() * 20.0 - 10.0;
                double want = RefOutlookFor(s, env, worth, from);
                tried += 2;
                same += SameBits(MarketValuation.OutlookFor(s, env, worth, from), want) ? 1 : 0;
                same += SameBits(MarketValuation.OutlookFor(terms, worth, from), want) ? 1 : 0;
                moved += want != from ? 1 : 0;
            }
        }
        Check(same == tried && moved > companies.Count, "pace: the outlook a price stands for comes out the same to the last bit through the company taken apart once (" + same + " of " + tried
                                                        + ", " + moved + " searches that moved it)");
    }

    // ---------------------------------------------------------------- the open and the close in pieces

    // A market with four bots, holdings, resting orders and stops, played for nine months in the way given; its whole
    // state, tape, news, payments, notes and money as one text.
    private static string PacePlayed(ulong seed, Func<MarketEngine, long, bool> go, out int calls)
    {
        MarketEngine m = BotMarket(seed, 5000000, out BotWorld w);
        calls = 0;
        PaceGo(m, MarketState.Time(TradingOnOrAfter(2), MarketCatalogue.PhaseOpen + 10), go, ref calls);
        for (int f = 1; f <= MarketBots.FirmsMost; f++)
        {
            m.Hire(f, 400000, 25, 10, 35, out _);
        }
        int k = 0;
        foreach (Stock s in m.State.Stocks.Where(x => x.Listed).Take(12))
        {
            Trade(m, s, OrderSide.Buy, 50 + k * 10);
            Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
            Trade(m, s, OrderSide.Sell, 20, OrderType.Stop, 0, (int)(q.Bid * 0.95), OrderLife.UntilCancelled);
            Trade(m, s, OrderSide.Buy, 20, OrderType.Limit, (int)(q.Ask * 0.97), 0, OrderLife.UntilCancelled);
            k++;
        }
        for (int d = 3; d < 270; d += 9)
        {
            PaceGo(m, MarketState.Time(d, MarketCatalogue.PhaseOpen + 3), go, ref calls);
            Trade(m, m.State.Stocks.Where(x => x.Listed).Skip(d % 30).First(), OrderSide.Buy, 30);
        }
        PaceGo(m, MarketState.Time(280, MarketCatalogue.PhaseClose), go, ref calls);
        return PaceText(m, w);
    }

    private static void PaceGo(MarketEngine m, long target, Func<MarketEngine, long, bool> go, ref int calls)
    {
        while (calls < 10000000)
        {
            calls++;
            if (go(m, target))
            {
                return;
            }
        }
    }

    private static string PaceText(MarketEngine m, BotWorld w)
    {
        var t = new System.Text.StringBuilder(1 << 20);
        t.Append(MarketStore.Write(m.State)).Append('#').Append(m.Tape.Whole()).Append('#');
        foreach (MarketNews n in w.Inner.Heard)
        {
            t.Append(n.Day).Append(',').Append((int)n.Kind).Append(',').Append(n.Stock).Append(',').Append(n.A).Append(',').Append(n.B).Append(',').Append(n.C).Append(';');
        }
        t.Append('#').Append(string.Join(",", w.Inner.Payments)).Append('#').Append(w.Inner.Fills).Append('#').Append(w.Inner.FilledQty).Append('#');
        foreach (BotNote note in w.Notes)
        {
            t.Append((int)note.Kind).Append(',').Append(note.Firm).Append(',').Append(note.Stock).Append(',').Append(note.Qty).Append(',').Append(note.A).Append(';');
        }
        t.Append('#').Append(w.Purse.Money);
        return t.ToString();
    }

    private static void PaceCloseInPieces()
    {
        int wholeCalls = 0, pieceCalls = 0, timeCalls = 0;
        bool same = true, lines = true;
        foreach (ulong seed in new ulong[] { 11, 12, 13 })
        {
            string whole = PacePlayed(seed, (m, target) =>
            {
                int work = int.MaxValue;
                return m.RunTo(target, ref work);
            }, out int c1);
            string pieces = PacePlayed(seed, (m, target) =>
            {
                int one = 1;
                return m.RunTo(target, ref one);
            }, out int c2);
            string timed = PacePlayed(seed, (m, target) => m.RunFor(target, 0), out int c3);
            same &= pieces == whole && timed == whole;
            lines &= whole.Length > 300000;
            wholeCalls += c1;
            pieceCalls += c2;
            timeCalls += c3;
        }
        Check(lines, "pace: nine months of three markets with four bots each, holdings, resting orders and stops are played to compare");
        Check(same, "pace: the open and the close worked through a piece at a time (and with no time at all a frame) give the very market of the one-go open and close: prices, "
                    + "histories, news, dividends, bot decisions, the tape, every payment and note");
        Check(pieceCalls > wholeCalls * 50 && timeCalls == pieceCalls,
            "pace: and they were played in pieces (" + pieceCalls + " calls against " + wholeCalls + "), one piece a call when the frame's time is gone at once");
    }

    // Plays one piece at a time until a close is half played; how many pieces it took.
    private static int PaceToMidClose(MarketEngine m, int day, int pieces)
    {
        long target = MarketState.Time(day, MarketCatalogue.PhaseClose);
        int done = 0, inClose = 0;
        while (done < 1000000)
        {
            int one = 1;
            m.RunTo(target, ref one);
            done++;
            if (m.Midway && m.State.Phase == MarketCatalogue.PhaseClose - 1 && ++inClose >= pieces)
            {
                return done;
            }
        }
        return done;
    }

    // What is asked of the market while a moment is half played is asked of it as it was once the moment was played in
    // one go: the moment is played out first.
    private static void PaceAskedMidway()
    {
        bool same = true, finished = true, halfway = true;
        string[] asks = { "order", "bot", "event", "test" };
        foreach (string ask in asks)
        {
            foreach (int pieces in new[] { 1, 60, 150, 290 })
            {
                MarketEngine a = BotMarket(500 + (ulong)pieces, 5000000, out BotWorld wa);
                MarketEngine b = BotMarket(500 + (ulong)pieces, 5000000, out BotWorld wb);
                RunTo(a, 20);
                RunTo(b, 20);
                a.Hire(2, 300000, 25, 10, 35, out _);
                b.Hire(2, 300000, 25, 10, 35, out _);
                int closeDay = TradingOnOrAfter(a.State.Day + 1);
                if (ask == "event")
                {
                    // (the tape's furthest moment at that very close, as after a load: a moment half played counts as played)
                    a.Tape.Reach(closeDay, MarketCatalogue.PhaseClose, true);
                    b.Tape.Reach(closeDay, MarketCatalogue.PhaseClose, true);
                    a.HoldAfterLoad();
                    b.HoldAfterLoad();
                }
                PaceToMidClose(a, closeDay, pieces);
                halfway &= a.Midway;
                // b: the same close played in one go, then the same ask.
                int work = int.MaxValue;
                b.RunTo(MarketState.Time(a.State.Day, MarketCatalogue.PhaseClose), ref work);
                Stock sa = a.State.Stocks.First(s => s.Listed && s.Id % 7 == 3);
                Stock sb = b.State.Stock(sa.Id);
                switch (ask)
                {
                    case "order":
                        a.Place(sa.Id, OrderSide.Buy, OrderType.Market, OrderLife.Day, 40, 0, 0, false, false, false, out _);
                        b.Place(sb.Id, OrderSide.Buy, OrderType.Market, OrderLife.Day, 40, 0, 0, false, false, false, out _);
                        break;
                    case "bot":
                        a.Hire(3, 300000, 25, 10, 35, out _);
                        b.Hire(3, 300000, 25, 10, 35, out _);
                        break;
                    case "event":
                        a.Event(MarketInputs.EventCompany, sa.Id, 25000);
                        b.Event(MarketInputs.EventCompany, sb.Id, 25000);
                        a.Finish();
                        break;
                    default:
                        a.NewsFor(sa.Id, true);
                        b.NewsFor(sb.Id, true);
                        break;
                }
                finished &= !a.Midway;
                int day = TradingOnOrAfter(a.State.Day + 3);
                RunTo(a, day);
                RunTo(b, day);
                same &= PaceText(a, wa) == PaceText(b, wb);
            }
        }
        Check(halfway, "pace: a close is caught half played (after 1, 60, 150 and 290 of its pieces)");
        Check(finished, "pace: an order, a bot hired, the city's news or a test asked for while a moment is half played plays it out first");
        Check(same, "pace: and the market goes on exactly as if the moment had been played in one go before the ask (orders, bots, events, tests)");
    }

    // ---------------------------------------------------------------- the catch-up's budget

    private static void PaceCatchUpBudget()
    {
        // With no time a frame, each call plays exactly one piece, whatever it is.
        MarketEngine one = NewMarket(611, 60);
        int day = 3;
        while (!MarketCatalogue.TradingDay(day) || !MarketCatalogue.TradingDay(day + 1))
        {
            day++;
        }
        RunTo(one, day);
        long target = MarketState.Time(day + 1, MarketCatalogue.PhaseClose);
        int calls = 1;
        while (!one.RunFor(target, 0) && calls < 100000)
        {
            calls++;
        }
        // A market day's pieces: the night, the results, the open (its start; each company's business and the call that
        // ends them; the night's draws; each company's open and the end of them; its end), 78 steps, the close (its start;
        // each company's candle and the end of them; each company settled and the end of them; the books; the bots' end;
        // its end).
        int n = one.State.Stocks.Count;
        int steps = MarketCatalogue.PhaseClose - 1 - MarketCatalogue.PhaseOpen;
        int expected = 2 + (1 + (n + 1) + 1 + (n + 1) + 1) + steps + (1 + (n + 1) + (n + 1) + 1 + 1 + 1);
        Check(calls == expected && steps == 78,
            "pace: with no time a frame each call plays exactly one piece: a market day of " + n + " companies in " + calls + " calls (" + expected + ")");

        // The slowest piece, and every call at a millisecond's budget within it of the millisecond (the fastest of three tries).
        double slowest = double.MaxValue, over = double.MaxValue;
        int frames = 0;
        bool same = true;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            MarketEngine a = BotMarket(612, 5000000, out _);
            MarketEngine b = BotMarket(612, 5000000, out _);
            RunTo(a, 30);
            RunTo(b, 30);
            for (int f = 1; f <= MarketBots.FirmsMost; f++)
            {
                a.Hire(f, 400000, 25, 10, 35, out _);
                b.Hire(f, 400000, 25, 10, 35, out _);
            }
            long to = MarketState.Time(TradingOnOrAfter(45), MarketCatalogue.PhaseClose);
            PaceTimes(a, to, 0, out double pieceMost, out _);
            PaceTimes(b, to, Stopwatch.Frequency / 1000, out double callMost, out int bFrames);
            slowest = Math.Min(slowest, pieceMost);
            over = Math.Min(over, callMost - 1.0 - pieceMost);
            frames = bFrames;
            same &= MarketStore.Write(a.State) == MarketStore.Write(b.State);
        }
        Console.WriteLine("  market pace: the slowest piece of fifteen market days " + slowest.ToString("0.000") + " ms; a frame at a 1 ms budget ran over by at most "
                          + Math.Max(0.0, over).ToString("0.000") + " ms beyond its last piece; " + frames + " frames for the fifteen days");
        Check(slowest < 1.0, "pace: no piece of the market's work takes a millisecond (the slowest " + slowest.ToString("0.000") + " ms)");
        Check(over < 0.25, "pace: a frame's catch-up at a millisecond's budget stops within its budget and the piece it was in (over by " + over.ToString("0.000") + " ms)");
        Check(same && frames > 15, "pace: the catch-up at a millisecond a frame comes to the very market of the catch-up one piece at a time (" + frames + " frames)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void PaceTimes(MarketEngine m, long target, long ticks, out double most, out int frames)
    {
        most = 0.0;
        frames = 0;
        while (frames < 1000000)
        {
            frames++;
            long began = Stopwatch.GetTimestamp();
            bool there = m.RunFor(target, ticks);
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            most = Math.Max(most, ms);
            if (there)
            {
                return;
            }
        }
    }

    // ---------------------------------------------------------------- what settling the close costs

    private static void PaceSettleCost()
    {
        MarketEngine m = NewMarket(613, 60);
        RunTo(m, 40);
        List<Stock> listed = m.State.Stocks.Where(s => s.Listed && s.BidCents <= 0).ToList();
        double before = double.MaxValue, after = double.MaxValue;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            before = Math.Min(before, PaceSettleOld(listed, m.State.Env));
            after = Math.Min(after, PaceSettleNew(listed, m.State.Env));
        }
        Console.WriteLine("  market pace: settling " + listed.Count + " companies at the close " + before.ToString("0.000") + " ms with the whole sum for each try, "
                          + after.ToString("0.000") + " ms taken apart once");
        Check(after < before * 0.6, "pace: settling the close with each company taken apart once costs well under two thirds of the whole sum for each try ("
                                    + after.ToString("0.000") + " against " + before.ToString("0.000") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PaceSettleOld(List<Stock> listed, MarketEnv env)
    {
        double sink = 0.0;
        long began = Stopwatch.GetTimestamp();
        for (int i = 0; i < listed.Count; i++)
        {
            Stock s = listed[i];
            double outlook = RefOutlookFor(s, env, RefWorth(s, env, s.Outlook) * 1.03, s.Outlook);
            sink += Math.Log(RefWorth(s, env, outlook));
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        return double.IsNaN(sink) ? ms + 1000.0 : ms;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PaceSettleNew(List<Stock> listed, MarketEnv env)
    {
        var terms = new MarketValuation.Terms();
        double sink = 0.0;
        long began = Stopwatch.GetTimestamp();
        for (int i = 0; i < listed.Count; i++)
        {
            Stock s = listed[i];
            terms.Fill(s, env);
            double outlook = MarketValuation.OutlookFor(terms, terms.Worth(s.Outlook) * 1.03, s.Outlook);
            sink += Math.Log(terms.Worth(outlook));
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        return double.IsNaN(sink) ? ms + 1000.0 : ms;
    }

    // ---------------------------------------------------------------- the writers with kept builders

    private static void PaceWritersSameText()
    {
        MarketEngine small = NewMarket(614, 60);
        RunTo(small, 5);
        MarketEngine big = NewMarket(615, 60);
        RunTo(big, 200);
        string first = MarketStore.Write(small.State);
        string bigLine = MarketStore.Write(big.State);
        string again = MarketStore.Write(small.State);
        string city = MarketStore.Write(small.State, true);
        string bigAgain = MarketStore.Write(big.State);
        Check(first == again && bigLine == bigAgain && first.Length < bigLine.Length && city != first,
            "pace: the save's line written with its kept builder is the same text every time, a short line after a long one included (" + first.Length + " and " + bigLine.Length + " characters)");
        MarketState back = MarketStore.Load(new Dictionary<string, string> { { MarketStore.Key, again } }, out int version);
        Check(back != null && MarketStore.Write(back) == first, "pace: and reads back to the same line");
        string tapeFirst = big.Tape.Whole();
        string tapeSmall = small.Tape.Whole();
        string tapeAgain = big.Tape.Whole();
        MarketTape read = MarketTape.Read(big.Tape.Id, tapeAgain);
        Check(tapeFirst == tapeAgain && tapeSmall.Length < tapeFirst.Length && read.Whole() == tapeFirst,
            "pace: the whole tape written with its kept builder is the same text every time and reads back to it");
    }
}
