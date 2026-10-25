using System;
using System.Linq;
using RealLifeOverhaul.Markets;

// The most of a company the player may hold, an option since the 10% rule became one: at each choice the figure, an order
// to it and past it, a worked order and a bot's room, a rival's holding stopped at its free float, a player's listed
// company kept at a tenth, a holding past a lowered choice kept and sold, and one place in the sources that reads it.
internal static partial class Program
{
    private static void HoldingCapChecks()
    {
        try
        {
            for (int choice = 0; choice < MarketCatalogue.HoldingCapBp.Length; choice++)
            {
                HoldingCapAt(choice);
            }
            HoldingCapLowered();
            HoldingCapOthers();
        }
        finally
        {
            MarketTrading.HoldTo(0);   // every other check is written for the tenth
        }
        HoldingCapSources();
        HoldingCapTexts();
    }

    private static void HoldingCapAt(int choice)
    {
        MarketTrading.HoldTo(choice);
        int bp = MarketCatalogue.HoldingCapBp[choice];
        string at = " (" + bp / 100 + "%)";
        var world = new MarketWorld(931);
        world.Purse.Money = 1000000000000;
        MarketEngine m = NewMarket(931, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        Stock big = Liquid(m, MarketSize.Large);
        long most = MarketTrading.MostHeld(big), per = MarketTrading.MostPerOrder(big);
        Check(MarketTrading.HoldingBp == bp && most == big.Shares * bp / 10000L && MarketTrading.MostHeldBp(big) == bp,
            "holding option: the most of a city company is the choice's share of its shares" + at);

        var p = new Position { StockId = big.Id, Shares = most - 5, Cost = 1, OpenedDay = m.State.Day };
        m.State.Positions.Add(p);
        Check(Why(m, big, OrderSide.Buy, 10) == OrderWhy.HoldingLimit, "holding option: an order past the most is refused" + at);
        Order fill = Trade(m, big, OrderSide.Buy, 5);
        Check(fill != null && fill.State == OrderState.Filled && m.State.Held(big.Id) == most, "holding option: an order up to the most is filled, and the holding is the most" + at);
        Worked(m, big, OrderSide.Buy, per * 3, out OrderWhy worked);
        Check(worked == OrderWhy.HoldingLimit, "holding option: a worked purchase at the most is refused" + at);
        Check(MarketBots.Room(m.State, big) == 0, "holding option: a bot has no room left at the most" + at);
        p.Shares = most - per / 2;
        Check(MarketBots.Room(m.State, big) == Math.Min(per, per / 2), "holding option: a bot's room is what is left under the most, at most one order" + at);
        m.State.Positions.Remove(p);
    }

    // Lowered from half to the tenth: what is held stays, a purchase is refused, a sale goes through.
    private static void HoldingCapLowered()
    {
        MarketTrading.HoldTo(2);
        var world = new MarketWorld(932);
        world.Purse.Money = 1000000000000;
        MarketEngine m = NewMarket(932, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        Stock big = Liquid(m, MarketSize.Large);
        long half = MarketTrading.MostHeld(big);
        m.State.Positions.Add(new Position { StockId = big.Id, Shares = half, Cost = 1, OpenedDay = m.State.Day });
        MarketTrading.HoldTo(0);
        long tenth = MarketTrading.MostHeld(big);
        Check(tenth < half && m.State.Held(big.Id) == half && Why(m, big, OrderSide.Buy, 1) == OrderWhy.HoldingLimit,
            "holding option: lowered under a holding, the holding is kept and a purchase is refused");
        Order sold = Trade(m, big, OrderSide.Sell, 10);
        Check(sold != null && sold.State == OrderState.Filled && m.State.Held(big.Id) == half - 10, "holding option: lowered under a holding, its shares can still be sold");
        MarketTrading.HoldTo(-4);
        Check(MarketTrading.HoldingBp == MarketCatalogue.HoldingShareBp, "holding option: a choice under the first is read as the tenth");
        MarketTrading.HoldTo(17);
        Check(MarketTrading.HoldingBp == 10000, "holding option: a choice past the last is read as all of it");
    }

    // A rival's holding has only its free float for outside holders; a player's listed company keeps the tenth at every choice.
    private static void HoldingCapOthers()
    {
        CompanyDef holdingDef = MarketCatalogue.All.First(d => d.Holding);
        CompanyDef cityDef = MarketCatalogue.All.First(d => !d.Holding);
        var rival = new Stock { Id = holdingDef.Id, Def = holdingDef, Shares = 4000000 };
        var own = new Stock { Id = cityDef.Id, Def = cityDef, Shares = 4000000, Own = new OwnListing { Founder = 2400000 } };
        bool rivalOk = true, ownOk = true;
        for (int choice = 0; choice < MarketCatalogue.HoldingCapBp.Length; choice++)
        {
            MarketTrading.HoldTo(choice);
            int bp = MarketCatalogue.HoldingCapBp[choice];
            rivalOk &= MarketTrading.MostHeld(rival) == rival.Shares * Math.Min(bp, MarketCatalogue.HoldingFloatBp) / 10000L && MarketTrading.MostHeld(rival) <= MarketIndex.Float(rival);
            ownOk &= MarketTrading.MostHeld(own) == own.Shares / 10 && MarketTrading.MostHeld(own) <= own.Own.Others(own);
        }
        Check(rivalOk && MarketCatalogue.HoldingFloatBp == 2500, "holding option: a rival's holding company stops at its free float, a quarter, at every choice past it");
        Check(ownOk, "holding option: a player's listed company keeps the tenth at every choice, within what its founder does not hold");
        MarketTrading.HoldTo(0);
        Check(MarketTrading.MostHeld(rival) == rival.Shares / 10, "holding option: at the tenth, a rival's holding is a tenth as before");
    }

    // Every sizing of a holding reads the one figure: nothing else names the old constant, and the director follows the option.
    private static void HoldingCapSources()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string[] readers = System.IO.Directory.Exists(root)
            ? System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(f => CodeAt(System.IO.File.ReadAllText(f), "HoldingShareBp") >= 0)
                .Select(f => System.IO.Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : new string[0];
        Check(string.Join(",", readers) == "MarketCatalogue.cs,MarketText.cs,MarketTrading.cs",
            "holding option: only the catalogue, the trading rules and the refusal's words name the tenth (" + string.Join(", ", readers) + ")");
        string trading = ModSource(@"Markets\MarketTrading.cs"), invest = ModSource(@"Markets\MarketDirector.Invest.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(CodeAt(trading, "if (-after > MostHeld(s))") > 0 && CodeAt(trading, "if (buying && after > MostHeld(s))") > 0,
            "holding option: a purchase and a short sale are both held to the same most");
        Check(CodeAt(invest, "MarketTrading.HoldTo(Settings.MarketsHolding);") > 0, "holding option: the market follows the option with the others, once a second");
        Check(CodeAt(mod, ".AddDropdown(Settings.MarketsHoldingId, \"reallife_mkt_opt_holding\",") > 0
              && CodeAt(mod, "\"reallife_mkt_opt_holding_tenth\", \"reallife_mkt_opt_holding_quarter\", \"reallife_mkt_opt_holding_half\", \"reallife_mkt_opt_holding_all\"") > 0,
            "holding option: the option's row has its four choices, in the catalogue's order");
    }

    private static void HoldingCapTexts()
    {
        string[] keys = { "reallife_mkt_opt_holding", "reallife_mkt_opt_holding_tenth", "reallife_mkt_opt_holding_quarter", "reallife_mkt_opt_holding_half",
            "reallife_mkt_opt_holding_all", "reallife_opt_tip_markets_holding", "reallife_opt_sec_investor", "reallife_mkt_why_holding_most", "reallife_mkt_test_holding",
            "reallife_mkt_test_holding_pass", "reallife_mkt_test_holding_fail" };
        bool all = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            var texts = LocaleFile(code);
            all &= keys.All(k => texts.TryGetValue(k, out string v) && v.Length > 0);
            all &= texts.TryGetValue("reallife_mkt_why_holding_most", out string why) && why.Contains("{pct}");
            all &= texts.TryGetValue("reallife_mkt_test_holding_pass", out string pass) && pass.Contains("{count}") && pass.Contains("{name}") && pass.Contains("{pct}");
        }
        var en = LocaleFile("en");
        Check(all && en["reallife_opt_tip_markets_holding"].Contains("only new buying is refused") && en["reallife_guide_markets"].Contains("The most of a company you may hold"),
            "holding option: its texts in en, pt and zh-cn, the refusal with the figure, the tip and the Guide saying a holding past a lowered choice is kept");
    }
}
