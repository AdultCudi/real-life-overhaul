using System;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;

// A rule bot's trades closed and its deepest fall from its peak: counted as it trades, kept in the save with the rule, and read as nothing
// from a save that had neither. The old Bots tab and the kit's rules table read the same two fields.
internal static partial class Program
{
    private static void StocksRuleFigureChecks()
    {
        // The counting, by hand: purchases are no closed trades, each sale is one.
        var rule = new RuleBot { Id = 1, Kind = RuleKind.Grid, StockId = 1, Budget = 10000, Cash = 10000, Status = RuleStatus.Running };
        RuleBots.Fill(rule, true, 10, 1000, 5);
        RuleBots.Fill(rule, true, 10, 1000, 5);
        Check(rule.Closed == 0, "stocks rule figures: a purchase is not a closed trade");
        RuleBots.Fill(rule, false, 5, 1200, 6, 0, 0);
        RuleBots.Fill(rule, false, 15, 900, 7, 0, 0);
        Check(rule.Closed == 2, "stocks rule figures: each sale is one trade closed (" + rule.Closed + ")");

        // The fall: the peak starts at the budget, a deeper fall is kept, a recovery does not undo it.
        var stock = new Stock { Id = 1, Last = 1000 };
        var held = new RuleBot { Id = 2, Budget = 10000, Cash = 0, Shares = 1000, Status = RuleStatus.Running };   // 1000 shares at 10.00 is all of its budget
        RuleBots.Mark(held, stock);
        Check(held.Peak == 10000 && held.DrawdownBp == 0, "stocks rule figures: the peak starts at the budget and the first mark falls from nothing");
        stock.Last = 900;
        RuleBots.Mark(held, stock);
        Check(held.DrawdownBp == 1000, "stocks rule figures: a tenth off the peak is 1000 basis points (" + held.DrawdownBp + ")");
        stock.Last = 950;
        RuleBots.Mark(held, stock);
        Check(held.DrawdownBp == 1000 && held.Peak == 10000, "stocks rule figures: a recovery keeps the deepest fall");
        stock.Last = 1100;
        RuleBots.Mark(held, stock);
        stock.Last = 990;
        RuleBots.Mark(held, stock);
        Check(held.Peak == 11000 && held.DrawdownBp == 1000, "stocks rule figures: a new peak moves the line the next fall is measured from (" + held.Peak + ", " + held.DrawdownBp + ")");
        stock.Last = 100;
        RuleBots.Mark(held, stock);
        Check(held.DrawdownBp > 1000 && held.DrawdownBp <= 10000, "stocks rule figures: a deeper fall replaces it, never past the whole");
        stock.Last = 0;
        int before = held.DrawdownBp;
        RuleBots.Mark(held, stock);
        Check(held.DrawdownBp == before, "stocks rule figures: a company with no price marks nothing");

        // On the real market: the same seed gives the same figures, some rules close trades, and every fall and peak is in range.
        string first = StocksRuleFigureRun(out int closedTotal, out int fallen, out bool sane);
        string again = StocksRuleFigureRun(out _, out _, out _);
        Check(first == again, "stocks rule figures: the same seed gives the same trades closed and falls (deterministic)");
        Check(closedTotal > 0 && sane, "stocks rule figures: over a year of the real market rules close trades, and each fall and peak is in range (" + closedTotal + " closed, " + fallen + " with a fall: " + first + ")");

        // The save: the fields come back, an untouched rule writes the line it always had, and an older line loads with both at nothing.
        var world = new MarketWorld(9033);
        world.Purse.Money = 80000000;
        MarketEngine m = NewMarket(9033, 60, 1, 0, world);
        MarketState s = m.State;
        RuleBots.SeedRivals(s);
        Stock k = s.Stocks.First(x => x.Listed && x.Own == null);
        RuleBot mine = RuleBots.NewRule(s, RuleKind.Dip, k.Id, 30, 800, OrderType.Market, 1500000, 40, 500, false, false, false, 1);
        string plain = MarketStore.Write(s);
        string plainRule = RuleLine(plain, mine.Id);
        Check(plainRule.Split('|').Length == 26, "stocks rule figures save: a rule that has not traded writes the same 26 fields as before (" + plainRule.Split('|').Length + ")");
        mine.Closed = 7;
        mine.Peak = 1612345;
        mine.DrawdownBp = 842;
        string line = MarketStore.Write(s);
        MarketState back = MarketStore.Read(line, out _);
        RuleBot r2 = back != null ? back.Rules.FirstOrDefault(r => r.Id == mine.Id) : null;
        Check(r2 != null && r2.Closed == 7 && r2.Peak == 1612345 && r2.DrawdownBp == 842, "stocks rule figures save: trades closed, the peak and the fall come back");
        Check(back != null && Part(MarketStore.Write(back), 'z') == Part(line, 'z'), "stocks rule figures save: the desk part writes again as it was read");
        string desk = Part(line, 'z');
        string older = line.Replace(desk, string.Join(";", desk.Split(';').Select(r => r.StartsWith("r|") ? string.Join("|", r.Split('|').Take(26)) : r)));
        MarketState old = MarketStore.Read(older, out _);
        RuleBot ro = old != null ? old.Rules.FirstOrDefault(r => r.Id == mine.Id) : null;
        Check(ro != null && ro.Closed == 0 && ro.Peak == 0 && ro.DrawdownBp == 0 && ro.Cash == mine.Cash && ro.Budget == mine.Budget && old.Rules.Count == s.Rules.Count,
            "stocks rule figures save: a save from before the two fields loads every rule as it was, with both at nothing");
        string odd = line.Replace(RuleLine(line, mine.Id), RuleLine(line, mine.Id).Replace("|7|1612345|842", "|-4|-9|99999"));
        MarketState clamped = MarketStore.Read(odd, out _);
        RuleBot rc = clamped != null ? clamped.Rules.FirstOrDefault(r => r.Id == mine.Id) : null;
        Check(rc != null && rc.Closed == 0 && rc.Peak == 0 && rc.DrawdownBp == 10000, "stocks rule figures save: a damaged figure is held in range");

        // Both interfaces read them.
        string tab = ModSource(@"Markets\MarketApp.Bots.cs"), pages = ModSource(@"Ui\Stocks\StocksBuild.Pages.cs");
        Check(tab.Contains("WriteRuleRows(s)") && tab.Contains("r.DrawdownBp") && tab.Contains("Count(r.Closed)"), "stocks rule figures: the old Bots tab lists the player's rules with their fall and trades");
        Check(pages.Contains("r.DrawdownBp") && pages.Contains("r.Closed") && pages.Contains("reallife_mkt_bot_col_fall") && pages.Contains("reallife_mkt_bot_col_trades"),
            "stocks rule figures: the kit's rules table has the fall and trades columns");
        Check(ModSource(@"Markets\MarketEngine.cs").Contains("RuleBots.Close(State)"), "stocks rule figures: every rule is marked at the close");
        StocksLooseEndChecks();
    }

    // The desk's loose ends: the watch button shows whether the company is watched, and the pages and fields nothing could reach are gone.
    private static void StocksLooseEndChecks()
    {
        string company = ModSource(@"Ui\Stocks\StocksBuild.Company.cs"), model = ModSource(@"Ui\Stocks\StocksModel.cs"), screen = ModSource(@"Ui\Stocks\StocksScreen.cs");
        Check(company.Contains("c.Watching = state.Watching(s.Id);"), "stocks watch: the eye on the company page is lit for a company on the watch list");
        Check(!Regex.IsMatch(model, @"Ticket,\s*Index") &&!model.Contains("UiStatementModel") && !model.Contains("UiDecisionModel") && !model.Contains("UiFormRow") && !screen.Contains("FoldBand"),
            "stocks desk: no page, band or form is kept that the screen cannot reach");
        Check(!model.Contains("CashAfter") && !model.Contains("MostRow") && !model.Contains("Instead"), "stocks ticket: the model holds only what the order panel draws");
    }

    private static string RuleLine(string line, int id)
    {
        foreach (string rec in Part(line, 'z').Split(';'))
        {
            string[] c = rec.Split('|');
            if (c[0] == "r" && c.Length > 1 && c[1] == id.ToString())
            {
                return rec;
            }
        }
        return string.Empty;
    }

    private static string StocksRuleFigureRun(out int closedTotal, out int fallen, out bool sane)
    {
        var world = new MarketWorld(9021);
        world.Purse.Money = 80000000;
        MarketEngine m = NewMarket(9021, 60, 1, 0, world);
        MarketState s = m.State;
        RuleBots.SeedRivals(s);
        var big = s.Stocks.Where(k => k.Listed && k.Own == null && k.Size != MarketSize.Small).OrderByDescending(k => k.Adv).Take(3).ToList();
        RuleKind[] kinds = { RuleKind.Dip, RuleKind.Rider, RuleKind.Dividend, RuleKind.Rebalance, RuleKind.Grid };
        for (int i = 0; i < kinds.Length; i++)
        {
            Stock k = big[i % big.Count];
            RuleBots.NewRule(s, kinds[i], k.Id, kinds[i] == RuleKind.Dip ? 100 : kinds[i] == RuleKind.Grid ? k.Last * 12 / 10 : k.Last, 200, OrderType.Market, 1500000, 40, 500, false, false, false, 1);
        }
        // Rivals that trade on any day the rule allows, so closed trades are read and not hoped for.
        Stock lead = big[0];
        s.Rules.Add(new RuleBot { Id = 90, Kind = RuleKind.Dip, Owner = 1, Who = 0, StockId = lead.Id, Level = 101, Budget = 1500000, Cash = 1500000, PerTrade = 10, MaxOpen = 1, Status = RuleStatus.Running });
        s.Rules.Add(new RuleBot { Id = 91, Kind = RuleKind.Rebalance, Owner = 1, Who = 1, StockId = lead.Id, GainBp = 1, Budget = 1500000, Cash = 1500000, PerTrade = 10, MaxOpen = 1, Status = RuleStatus.Running });
        var work = new MarketBots.Work();
        SessionDay(m, 2, 10);
        for (int day = 3; day <= 243; day++)
        {
            RunTo(m, day, MarketCatalogue.PhaseNone);
            RunTo(m, day, MarketCatalogue.PhaseOpen);
            if (MarketCatalogue.TradingDay(day))
            {
                RuleBots.Step(s, day, MarketCatalogue.PhaseOpen, null, work);   // the rules' own pass, as the engine runs it at the open
            }
            RunTo(m, day, MarketCatalogue.PhaseClose);
        }
        closedTotal = s.Rules.Sum(r => r.Closed);
        fallen = s.Rules.Count(r => r.DrawdownBp > 0);
        sane = s.Rules.All(r => r.Closed >= 0 && r.DrawdownBp >= 0 && r.DrawdownBp <= 10000 && r.Peak >= 0);
        return string.Join(",", s.Rules.Select(r => r.Id + ":" + r.Closed + ":" + r.Peak + ":" + r.DrawdownBp));
    }
}
