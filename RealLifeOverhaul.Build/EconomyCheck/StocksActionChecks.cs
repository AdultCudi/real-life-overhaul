using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Ui;

// The Stocks desk's kit (Scripts/Ui/Stocks) does everything the old Stocks app does (the owner, 4 Nov: "bonds, futures, bots all
// look empty"). The kit asks the director, which asks the engine; the checks here ask the engine the same questions the sheets ask
// and read the money and the state it moves, and read the rules the sheets write. Every old action has a sheet or a button, the
// search filters, and a rival runs a rule by the rules the player's runs by.
internal static partial class Program
{
    private static void StocksActionChecks()
    {
        StocksActionTable();
        StocksSearchChecks();
        StocksSheetStepChecks();
        StocksBondMoney();
        StocksDerivMoney();
        StocksBotMoney();
        StocksRuleChecks();
    }

    // Every action of the old app is a line with the sheet that does it; the sheets and the pages' buttons are what the kit has.
    private static void StocksActionTable()
    {
        var kinds = new HashSet<string>(typeof(StocksSheets).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(byte)).Select(f => f.Name), StringComparer.Ordinal);
        kinds.Add("Page");
        var used = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool known = true, once = true;
        foreach (string line in StocksSheets.Actions)
        {
            int at = line.LastIndexOf(':');
            string sheet = at > 0 ? line.Substring(at + 1) : string.Empty;
            known &= kinds.Contains(sheet);
            once &= seen.Add(line);
            used.Add(sheet);
        }
        Check(known && once && StocksSheets.Actions.Length >= 48, "stocks actions: every action of the old app is a line with a sheet or a page button of the kit (" + StocksSheets.Actions.Length + ")");
        var sheets = kinds.Where(k => k != "None" && k != "Page").ToList();
        Check(sheets.All(used.Contains), "stocks actions: every sheet the kit has is for an action of the old app (" + string.Join(",", sheets.Where(s => !used.Contains(s))) + ")");
        // The old app's own actions, named by what it asks the director (read from its source), each has its line.
        string root = FindRoot();
        string[] old = { "BuyBond", "BuyLadder", "SellBond", "SetBondReinvest", "SetBondRollover", "PayCall", "CloseContract", "HireBot", "DismissBot", "RestartBot", "SellUpBot",
            "SetBot", "SetBotScope", "PickForBot", "SetBotPayout", "AddBotCapital", "TakeBotCapital", "Cancel", "Deposit", "Withdraw", "Watch", "SetReinvest", "PlaceWorked", "Engage", "OrderAudit", "Publish" };
        string kit = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Stocks"), "StocksScreen*.cs").Select(System.IO.File.ReadAllText));
        var missing = old.Where(a => kit.IndexOf("_market." + a + "(", StringComparison.Ordinal) < 0).ToList();
        Check(missing.Count == 0, "stocks actions: the kit's screen calls the director for every old app action (" + string.Join(",", missing) + " not)");
        Check(kit.IndexOf("_market.Buy(", StringComparison.Ordinal) >= 0 && kit.IndexOf("_market.Ask(", StringComparison.Ordinal) >= 0 && kit.IndexOf("_market.Place(", StringComparison.Ordinal) >= 0,
            "stocks actions: contracts, the founder's deeds and orders go through the director too");
    }

    private static string FindRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul")))
        {
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return dir ?? ".";
    }

    private static void StocksSearchChecks()
    {
        var gmf = new Stock { Id = 1, Name = "Greenmarket Foods", Ticker = "GMF" };
        var stc = new Stock { Id = 2, Name = "Steamline Coffee", Ticker = "STC" };
        Check(StocksMath.Matches(gmf, "green") && StocksMath.Matches(gmf, "GMF") && StocksMath.Matches(gmf, "  gmf ") && !StocksMath.Matches(gmf, "stc") && StocksMath.Matches(stc, "COFFEE")
              && StocksMath.Matches(gmf, null) && StocksMath.Matches(gmf, "   ") && !StocksMath.Matches(null, "a"), "stocks search: it filters by name or ticker in any case, and nothing typed shows all");
        var all = new List<Stock> { gmf, stc, new Stock { Id = 3, Name = "Greenfield Hotels", Ticker = "GFH" } };
        Check(all.Count(s => StocksMath.Matches(s, "green")) == 2 && all.Count(s => StocksMath.Matches(s, "greenm")) == 1 && all.Count(s => StocksMath.Matches(s, "zzz")) == 0,
            "stocks search: typing more narrows the list, and a word nothing has shows none");
    }

    private static void StocksSheetStepChecks()
    {
        var f = new StocksFieldModel { Kind = StocksFieldKind.Stepper, Min = 500, Max = 2000, Step = 500 };
        Check(StocksSheetState.Stepped(500, f, -1) == 500 && StocksSheetState.Stepped(500, f, 1) == 1000 && StocksSheetState.Stepped(2000, f, 1) == 2000, "stocks sheet: a stepper moves by its step and stops at its ends");
        var c = new StocksFieldModel { Kind = StocksFieldKind.Choice, Count = 3 };
        Check(StocksSheetState.Cycled(2, c, 1) == 0 && StocksSheetState.Cycled(0, c, -1) == 2, "stocks sheet: a choice goes round");
        Check(StocksSheetState.Typed("1,5x00", 0, 10000) == 1500 && StocksSheetState.Typed("99999", 0, 10000) == 10000 && StocksSheetState.Typed(null, 5, 100) == 5, "stocks sheet: typed digits are kept to the stepper's ends");
        var st = new StocksSheetState { Kind = StocksSheets.BondBuy, Key = 3 };
        st.V[0] = 77;
        st.Close();
        Check(!st.Open && st.V[0] == 0 && st.Key == 0, "stocks sheet: closing it leaves nothing of what was set");
    }

    // A bond bought through the engine, as the Buy sheet does: the cash leaves by the amount and the holding stands; sold, the cash comes back.
    private static void StocksBondMoney()
    {
        MarketEngine m = BondMarket(4401, 60, 1000000, out MarketWorld world);
        BondOffer o = OfferOf(m, 2);
        long before = world.Purse.Money;
        long least = BondRules.Least(m.State.Env.PriceLevel);
        Check(m.CanBuyBond(o, least - 1, m.Trading, true, 0) == BondWhy.TooLittle && world.Purse.Money == before, "stocks bonds: under the least is refused, nothing taken");
        BondHolding h = m.BuyBond(o, 100000, 50, m.Trading, true, out BondWhy why, 0, true);
        Check(h != null && why == BondWhy.None && before - world.Purse.Money == 100000 && m.State.Bonds.Holdings.Count == 1 && h.Reinvest == 50 && h.Rollover && h.EndDay > m.State.Day,
            "stocks bonds: a bond bought takes its amount from the cash and stands with its reinvest, rollover and end day");
        Check(m.SetBondReinvest(h.Id, 25, out _) && h.Reinvest == 25 && m.SetBondRollover(h.Id, false, out _) && !h.Rollover, "stocks bonds: the reinvest and the rollover of a bond held change on the holding");
        long cash = world.Purse.Money;
        bool sold = m.SellBond(h.Id, m.Trading, out long money, out BondWhy sellWhy);
        Check(sold && money > 0 && world.Purse.Money - cash == money && m.State.Bonds.Holding(h.Id) == null && sellWhy == BondWhy.None, "stocks bonds: sold early it pays its price into the cash and goes");
        long at = world.Purse.Money;
        int bought = m.BuyLadder(400000, 0, false, m.Trading, true, out BondWhy lwhy);
        Check(bought > 1 && lwhy == BondWhy.None && at - world.Purse.Money == 400000 && m.State.Bonds.Holdings.Count == bought, "stocks bonds: a ladder takes its sum and holds a bond for each Treasury term (" + bought + ")");
        var offers = new List<BondOffer>();
        m.BondOffers(new List<string> { "First Bank", "Second Bank" }, offers);
        var ladder = new List<BondOffer>();
        StocksMath.LadderOffers(offers, ladder);
        Check(ladder.Count == bought && ladder.Zip(ladder.Skip(1), (a, b) => a.Def.TermQuarters <= b.Def.TermQuarters).All(x => x) && offers.Any(x => x.Def.Kind == BondKind.Deposit) && offers.Any(x => x.Def.Kind == BondKind.City),
            "stocks bonds: the offers hold the Treasury, the city and the bank deposits, and the ladder runs shortest first");
    }

    // A future and an option opened and closed through the engine, as the sheets do.
    private static void StocksDerivMoney()
    {
        MarketEngine m = DerivMarket(4402, 10000000, out MarketWorld world);
        SessionDay(m, 1, 10);
        int[] days = DerivDays(m);
        DerivQuote q = m.QuoteFuture(Wheat, days[0], true, 2500);
        long cash = world.Purse.Money;
        DerivContract c = Buy(m, ContractKind.Long, Wheat, 0, days[0], 0, 2500);
        Check(c != null && c.IsOpen && cash - world.Purse.Money == q.Margin + q.Fee && m.State.Derivs.Balance == q.Margin * 100, "stocks futures: a future opened holds its margin and takes its commission from the cash");
        long open = world.Purse.Money;
        bool closed = m.CloseContract(c.Id, m.Trading, out _, out DerivWhy why);
        Check(closed && why == DerivWhy.None && c.End == ContractEnd.Closed && m.State.Derivs.Balance == 0 && world.Purse.Money > open - q.Fee, "stocks futures: closed, the margin comes back and the contract ends");
        Stock share = m.State.Stocks.First(s => DerivRules.ShareOffered(s, m.State.Day) == DerivWhy.None);
        DerivQuote oq = m.QuoteOption(true, null, share.Id, days[0], 0, 100);
        long before = world.Purse.Money;
        DerivContract call = Buy(m, ContractKind.Call, null, share.Id, days[0], 0, 100);
        Check(call != null && call.OnShares && before - world.Purse.Money == oq.Value + oq.Fee, "stocks futures: an option on a share costs its premium and commission");
        Check(m.CloseContract(call.Id, m.Trading, out long back, out _) && back > 0 && call.End == ContractEnd.Closed, "stocks futures: an option sold back pays its bid and ends");
    }

    // A firm hired and sent away, as the Hire sheet and its Dismiss button do.
    private static void StocksBotMoney()
    {
        var world = new MarketWorld(4403);
        world.Purse.Money = 5000000;
        MarketEngine m = NewMarket(4403, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        long before = world.Purse.Money;
        long capital = Math.Max(m.BotCapitalLeast, 900000);
        Bot b = m.Hire(MarketBots.All[0].Id, capital, 20, 5, 35, out BotWhy why);
        Check(b != null && why == BotWhy.None && world.Purse.Money == before && MarketBots.Committed(m.State) == capital && MarketBots.Hired(m.State, MarketBots.All[0].Id) != null && b.Size == 20 && b.Stop == 5 && b.Loss == 35,
            "stocks bots: a firm hired is given its capital, spent as it trades, and trades with the settings written for it (" + (before - world.Purse.Money) + " of " + capital + ", " + why + ", " + (b == null ? "none" : b.Size + "/" + b.Stop + "/" + b.Loss) + ")");
        Check(m.Hire(MarketBots.All[0].Id, capital, 20, 5, 35, out BotWhy twice) == null && twice == BotWhy.Hired, "stocks bots: a firm already hired is not hired twice");
        Check(m.SetBot(MarketBots.All[0].Id, 25, 10, 50, out _) && b.Size == 25 && b.Stop == 10 && b.Loss == 50, "stocks bots: the settings of a hired firm change at once");
        Check(m.Dismiss(MarketBots.All[0].Id, out _) && MarketBots.Hired(m.State, MarketBots.All[0].Id) == null, "stocks bots: a firm dismissed is gone from the book");
    }

    // The rules the player writes and a rival runs: the same intent, the same order, a budget that caps, a stop that guards.
    private static void StocksRuleChecks()
    {
        var state = new MarketState();
        var stock = new Stock { Id = 5, Status = StockStatus.Listed, Last = 3000, Open = 3000, Name = "Rule Co", Ticker = "RLC" };
        for (int i = 0; i < 30; i++)
        {
            stock.Daily.Add(new Candle(i, 4000 - i * 30, 4000, 3000, 4000 - i * 30, 100));
        }
        state.Stocks.Add(stock);
        state.Rules.Add(new RuleBot { Id = 1, Kind = RuleKind.Dip, Owner = 1, Who = 0, StockId = 5, Level = 30, Budget = 1000000, Cash = 1000000, PerTrade = 10, MaxOpen = 1, Status = RuleStatus.Running });
        RuleBot mine = RuleBots.NewRule(state, RuleKind.Dip, 5, 30, 800, OrderType.Market, 1000000, 10, 500, true, false, true, 1);
        Check(mine != null && mine.Owner == 0 && mine.Id == 2 && mine.Cash == mine.Budget && RuleBots.PlayerCount(state) == 1, "stocks rules: a rule is written with its budget as its cash, its id one past the largest");
        long a = RuleBots.Intent(mine, stock, new MarketBots.Work());
        long b = RuleBots.Intent(state.Rules[0], stock, new MarketBots.Work());
        Check(a == b && a != 0, "stocks rules: a rival's rule and the player's read the same intent from the same candles (" + a + ", " + b + ")");
        var rival = new List<long>();
        var player = new List<long>();
        RuleBots.Step(state, 40, MarketCatalogue.PhaseOpen, (r, id, buy, qty, limit, type) => { player.Add(buy ? qty : -qty); return true; }, new MarketBots.Work());
        TradePrint print = state.Names.Newest(0);
        Check(player.Count == 1 && state.Names.Count == 1 && print.Who == TradeWho.Rival && print.Size == Math.Abs(player[0]) && state.Rules[0].LastDay == 40, "stocks rules: a rival's order is the same size and goes on the tape, not through the player's cash");
        mine.Cash = 1;   // $1: under a share
        Check(RuleBots.Intent(mine, stock, new MarketBots.Work()) == 0, "stocks rules: a budget under a share buys nothing");
        mine.Cash = 60;  // $60: two shares at $30
        mine.OpenTrades = 0;
        Check(RuleBots.Intent(mine, stock, new MarketBots.Work()) == 2, "stocks rules: a budget caps what a rule buys");
        for (int i = 0; i < RuleBots.PlayerMost + 2; i++)
        {
            RuleBots.NewRule(state, RuleKind.Rider, 5, 0, 800, OrderType.Market, 1000, 1, 500, true, false, false, 1);
        }
        Check(RuleBots.PlayerCount(state) == RuleBots.PlayerMost && state.Rules.Count <= RuleBots.Most, "stocks rules: the player runs at most " + RuleBots.PlayerMost + ", whatever the rivals run");
        int rivals = state.Rules.Count(r => r.Owner == 1);
        Check(!RuleBots.Remove(state, 0) && RuleBots.Remove(state, 1) && state.Rules.Count(r => r.Owner == 1) == rivals && RuleBots.NewRule(state, RuleKind.Grid, 99, 1, 1, OrderType.Market, 1, 1, 1, true, false, false, 1) == null,
            "stocks rules: a rival's rule is never removed, and a rule on a company that is not listed is refused");
        int maxId = state.Rules.Max(r => r.Id);
        RuleBot again = RuleBots.NewRule(state, RuleKind.Gap, 5, 200, 800, OrderType.Market, 1000, 1, 500, true, false, false, 1);
        Check(again != null && again.Id == maxId + 1, "stocks rules: a removed rule's id is not reused");

        // A stop guard sells what is held through its stop, though it never bought: the whole holding.
        var s2 = new MarketState();
        var st2 = new Stock { Id = 6, Status = StockStatus.Listed, Last = 3000, Open = 3000 };
        for (int i = 0; i < 5; i++)
        {
            st2.Daily.Add(new Candle(i, 4000, 4000, 4000, 4000, 100));
        }
        s2.Stocks.Add(st2);
        s2.Positions.Add(new Position { StockId = 6, Shares = 40, Cost = 160000 });
        s2.Rules.Add(new RuleBot { Id = 1, Kind = RuleKind.StopGuard, Owner = 0, StockId = 6, Level = 4000, StopBp = 500, Budget = 1000, Cash = 1000, PerTrade = 1, MaxOpen = 1, Status = RuleStatus.Running });
        long sold = 0;
        RuleBots.Step(s2, 9, MarketCatalogue.PhaseOpen, (r, id, buy, qty, limit, type) => { sold = buy ? 0 : qty; return true; }, new MarketBots.Work());
        Check(sold == 40, "stocks rules: a stop guard sells the whole holding that has fallen through its stop (" + sold + ")");
    }
}
