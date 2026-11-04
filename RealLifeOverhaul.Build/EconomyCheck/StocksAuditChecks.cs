using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Ui;

// The Stocks desk's logic audit (4 Nov): the Bots page's Today and This week were lifetime sums of cash flow and never reset; a sell's
// "Position after" doubled the average cost; the Portfolio had lost the Pays and Account views; the alerts were fixed at 5 per cent and
// the support alert had no creator; the Market had no five-day spark and no open-orders box; and four of the old confirms asked
// nothing. Each is read back here off the real market, the real day turn and the same game-free handlers the screen calls.
internal static partial class Program
{
    private static void StocksAuditChecks()
    {
        StocksPositionAfterChecks();
        StocksBotFigureRun();
        StocksBotFigureStore();
        StocksPaysAccountChecks();
        StocksAlertChecks();
        StocksCommandChecks();
        StocksScreenChecks();
    }

    // ------------------------------------------------------------------------------------------------ Position after

    // The ticket's "Position after" must be the position the engine leaves: what is kept keeps its average cost.
    private static void StocksPositionAfterChecks()
    {
        // The audit's example: 100 shares at $10, sell 50 at $10: the average stays $10, not $20.
        StocksFigures.PositionAfter(100, 1000, false, 50, 500, out long shares, out long average);
        Check(shares == 50 && average == 1000, "stocks position after: 100 shares at $10, a sale of 50, leaves 50 at an average of $10 (" + shares + " at " + average + " cents)");
        var cases = new[]
        {
            new long[] { 0, 0, 100, 3700 },          // a first purchase
            new long[] { 100, 1000, 40, 480 },       // more of what is held: the cost adds, fee included
            new long[] { 100, 1000, -50, 500 },      // part sold at no gain
            new long[] { 100, 1000, -50, 650 },      // part sold at a gain: the sale's money is not the kept cost
            new long[] { 100, 1000, -100, 800 },     // all sold: nothing is left, no average
            new long[] { 100, 1000, -130, 1560 },    // through nothing: 30 shares sold short at what is left of the order
            new long[] { -40, 400, 25, 275 },        // part of a short bought back
            new long[] { -40, 400, 40, 380 },        // a short covered whole
            new long[] { -40, 400, 70, 770 },        // covered and 30 more bought
        };
        int wrong = 0;
        var seen = new StringBuilder();
        foreach (long[] c in cases)
        {
            long delta = c[2];
            var p = new Position { StockId = 1, Shares = c[0], Cost = c[1] };
            MarketTrading.Book(p, delta, c[3], new RealLifeOverhaul.Markets.Account());
            long want = p.Shares != 0 ? p.Cost * 100L / Math.Abs(p.Shares) : 0L;
            StocksFigures.PositionAfter(c[0], c[1], delta > 0, Math.Abs(delta), c[3], out long got, out long avg);
            if (got != p.Shares || avg != want)
            {
                wrong++;
                seen.Append(" [").Append(string.Join(",", c)).Append(": ").Append(got).Append(" at ").Append(avg).Append(" against ").Append(p.Shares).Append(" at ").Append(want).Append("]");
            }
        }
        Check(wrong == 0, "stocks position after: for a buy, a part sale, a whole sale, a sale through nothing and the cover of a short it is the position MarketTrading.Book leaves" + seen);
    }

    // ------------------------------------------------------------------------------------------------ the bots' day and week

    // A seeded city, a hundred and twenty days one at a time through the engine's own day turn: what a rule and a hired firm gained today is
    // read at each turn and at each close, and is the realised gain of that day (not cash flow, not a lifetime sum).
    private static void StocksBotFigureRun()
    {
        var world = new MarketWorld(9021);
        world.Purse.Money = 80000000;
        MarketEngine m = NewMarket(9021, 60, 1, 0, world);
        MarketState s = m.State;
        RuleBots.SeedRivals(s);
        List<Stock> big = s.Stocks.Where(k => k.Listed && k.Own == null && k.Size != MarketSize.Small).OrderByDescending(k => k.Adv).Take(3).ToList();
        RuleKind[] kinds = { RuleKind.Dip, RuleKind.Rider, RuleKind.Dividend, RuleKind.Rebalance, RuleKind.Grid };
        for (int i = 0; i < kinds.Length; i++)
        {
            Stock k = big[i % big.Count];
            int level = kinds[i] == RuleKind.Dip ? 100 : kinds[i] == RuleKind.Grid ? k.Last * 12 / 10 : kinds[i] == RuleKind.Gap ? 200 : k.Last;
            RuleBots.NewRule(s, kinds[i], k.Id, level, 200, OrderType.Market, 1500000, 40, 500, false, false, false, 1);
        }
        SessionDay(m, 2, 10);
        Bot firm = m.Hire(MarketBots.All[0].Id, 9000000, 25, 5, 35, out BotWhy hired);
        Bot firm2 = m.Hire(MarketBots.All[1].Id, 9000000, 25, 5, 35, out _);
        int rules = s.Rules.Count;
        int turnsNonZero = 0, closesChecked = 0, sellDays = 0, wrongDay = 0, wrongWeek = 0, wrongSum = 0, lifetimeNotToday = 0, weekStarts = 0, weekKept = 0, bots = 0;
        var cash = new long[rules];
        var cost = new long[rules];
        var weekSum = new long[rules];
        long weekSumFirm = 0;
        long realised0 = firm != null ? firm.Realised : 0;
        long firmBase = 0, firmWeekBase = 0;
        for (int day = 3; day <= 243; day++)
        {
            RunTo(m, day, MarketCatalogue.PhaseNone);   // the day turn: nothing happens yet in this day
            bool monday = MarketCatalogue.WeekOf(day) != MarketCatalogue.WeekOf(day - 1);
            for (int i = 0; i < rules; i++)
            {
                RuleBot r = s.Rules[i];
                turnsNonZero += r.DayGain != 0 || RuleBots.DayOf(r, s.Day) != 0 ? 1 : 0;
                if (monday)
                {
                    weekStarts++;
                    wrongWeek += r.WeekGain != 0 ? 1 : 0;
                    weekSum[i] = 0;
                }
                cash[i] = r.Cash;
                cost[i] = r.Cost;
            }
            if (firm != null)
            {
                turnsNonZero += firm.DayGain != 0 ? 1 : 0;
                wrongWeek += monday && firm.WeekGain != 0 ? 1 : 0;
                firmBase = firm.Realised;
                if (monday)
                {
                    firmWeekBase = firm.Realised;
                }
            }
            RunTo(m, day, MarketCatalogue.PhaseClose);
            if (!MarketCatalogue.TradingDay(day))
            {
                continue;
            }
            closesChecked++;
            for (int i = 0; i < rules; i++)
            {
                RuleBot r = s.Rules[i];
                // What it gained today is what its cash and its holding say: cash in less cost out, plus the cost of what stays.
                long want = (r.Cash - cash[i]) + (r.Cost - cost[i]);
                long got = RuleBots.DayOf(r, day);
                if (got != want && !(r.Owner == 0 && r.Cash == 0))
                {
                    wrongSum++;
                }
                weekSum[i] += got;
                wrongWeek += RuleBots.WeekOf(r, day) != weekSum[i] && r.Owner == 0 && r.Cash > 0 ? 1 : 0;
                sellDays += got != 0 ? 1 : 0;
            }
            if (firm != null)
            {
                bots++;
                wrongDay += MarketBots.DayOf(firm, day) != firm.Realised - firmBase ? 1 : 0;
                wrongWeek += MarketBots.WeekOf(firm, day) != firm.Realised - firmWeekBase ? 1 : 0;
                lifetimeNotToday += firm.Realised != 0 && MarketBots.DayOf(firm, day) == 0 ? 1 : 0;
                sellDays += firm.Realised - firmBase != 0 ? 1 : 0;
            }
        }
        Check(hired == BotWhy.None && closesChecked >= 150 && rules >= 8, "stocks bots run: the seeded city runs " + closesChecked + " market days with " + rules + " rules and a hired firm");
        Check(turnsNonZero == 0, "stocks bots run: at every day turn what a rule and a firm gained today is nothing again (" + turnsNonZero + " not)");
        Check(wrongWeek == 0 && weekStarts >= rules * 10, "stocks bots run: this week's figure is nothing on a Monday and the sum of its days after (" + wrongWeek + " wrong in " + weekStarts + " Mondays)");
        Check(wrongSum == 0 && wrongDay == 0, "stocks bots run: each day's figure is the gain realised that day: a rule's is its cash and holding's change, a firm's its realised change (" + wrongSum + " and " + wrongDay + " wrong)");
        Check(sellDays >= 10, "stocks bots run: gains are realised on many of the days, so the check reads real sales (" + sellDays + ")");
        long all = s.Rules.Where(r => r.Owner == 0).Sum(r => RuleBots.DayOf(r, s.Day));
        MarketBots.Figures(s, s.Day, out long today, out long week);
        long firms = s.Bots.Sum(b => MarketBots.DayOf(b, s.Day));
        Check(today == all + firms, "stocks bots run: the page's figure for today is the player's rules and firms together, and no rival's");
        MarketBots.Figures(s, s.Day + 1, out long tomorrow, out long weekTomorrow);
        Check(tomorrow == 0 && (MarketCatalogue.WeekOf(s.Day + 1) != MarketCatalogue.WeekOf(s.Day) ? weekTomorrow == 0 : true),
            "stocks bots run: a figure counted for yesterday reads nothing today, before any turn has come");
        _stocksRun = m;
    }

    private static MarketEngine _stocksRun;
}
