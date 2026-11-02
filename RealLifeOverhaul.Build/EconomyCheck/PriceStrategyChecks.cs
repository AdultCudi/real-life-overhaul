using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

internal static partial class Program
{
    // The pricing strategy a pricing plan keeps on BizMan's pricing page and a place's own manager follows (Economy/Company/
    // PriceStrategy.cs): its rule in cents, its number in the managers' save line, and the plans' own save line.
    private static void PriceStrategies()
    {
        var none = default(PriceStrategy);
        Check(none.IsDefault && !none.Prices && PriceStrategyRules.Price(none, 499, 450, 300, 600, 480) == 499,
            "price strategy: the default leaves the game's own suggestion as it is");
        var match = new PriceStrategy { Stance = PriceStance.Match };
        var under = new PriceStrategy { Stance = PriceStance.Undercut };
        var over = new PriceStrategy { Stance = PriceStance.Premium };
        Check(PriceStrategyRules.Price(match, 499, 450, 0, 0, 0) == 450 && PriceStrategyRules.Price(under, 499, 400, 0, 0, 0) == 380
              && PriceStrategyRules.Price(over, 499, 400, 0, 0, 0) == 440 && PriceStrategyRules.Price(match, 499, 0, 0, 0, 0) == 499,
            "price strategy: level with the rivals' lowest, five per cent under it or ten over it; with no rival, the suggestion");
        var margin = new PriceStrategy { Margin = 20 };
        Check(PriceStrategyRules.MarginFloor(300, 20) == 375 && PriceStrategyRules.MarginFloor(0, 20) == 1 && PriceStrategyRules.MarginFloor(300, 0) == 1
              && PriceStrategyRules.Price(margin, 340, 0, 300, 0, 0) == 375 && PriceStrategyRules.Price(margin, 340, 0, 300, 360, 0) == 375
              && PriceStrategyRules.Price(over, 499, 600, 0, 620, 0) == 620,
            "price strategy: the least margin raises a price to it, over the neighbourhood's ceiling too; the ceiling holds a price above the rivals");
        var step = new PriceStrategy { Stance = PriceStance.Match, Step = 10 };
        var floor = new PriceStrategy { Stance = PriceStance.Match, OverCost = true, Step = 5 };
        Check(PriceStrategyRules.Price(step, 499, 300, 0, 0, 450) == 405 && PriceStrategyRules.Price(step, 499, 300, 0, 0, 0) == 300
              && PriceStrategyRules.Price(floor, 499, 200, 300, 0, 290) == 315,
            "price strategy: a price moves at most the day's step from yesterday's, and never under cost holds over that step");
        var ninety = new PriceStrategy { Ninety = true };
        var both = new PriceStrategy { Ninety = true, Margin = 20 };
        Check(PriceStrategyRules.Price(ninety, 512, 0, 0, 0, 0) == 490 && PriceStrategyRules.Price(both, 360, 0, 300, 0, 0) == 390
              && PriceStrategyRules.Ninety(405, 400, 420) == 405 && PriceStrategyRules.Ninety(490, 1, 500) == 490 && PriceStrategyRules.Ninety(540, 1, 10000) == 490,
            "price strategy: .90 takes the nearest such price inside the other rules, the lower on a tie, and none where none fits");
        Check(PriceStrategyRules.MarginOf(500, 300) == 40 && PriceStrategyRules.MarginOf(280, 300) == -7 && PriceStrategyRules.MarginOf(0, 300) == int.MinValue
              && PriceStrategyRules.AgainstRivals(525, 500) == 50 && PriceStrategyRules.AgainstRivals(475, 500) == -50 && PriceStrategyRules.AgainstRivals(500, 0) == int.MinValue,
            "price strategy: a margin is a whole per cent of the price, the rivals' gap tenths of one, neither without its figure");
        Check(Rising(PriceStrategyRules.Margins) && Rising(PriceStrategyRules.Steps) && PriceStrategyRules.StanceKeys.Length == (int)PriceStance.Premium + 1,
            "price strategy: the margins and the day's steps are choices from none up, each once; each stance has its words");
        // Every strategy reads back from its number; the default is 0, so a managers' line from before the strategy reads as it.
        int tried = 0, wrong = 0;
        for (int st = 0; st <= (int)PriceStance.Premium; st++)
        {
            foreach (int m in PriceStrategyRules.Margins)
            {
                foreach (int sp in PriceStrategyRules.Steps)
                {
                    for (int bits = 0; bits < 8; bits++)
                    {
                        var s = new PriceStrategy { Stance = (PriceStance)st, Margin = m, Step = sp, Ninety = (bits & 1) != 0, OverCost = (bits & 2) != 0, Hold = (bits & 4) != 0 };
                        tried++;
                        wrong += SameStrategy(PriceStrategyRules.Unpack(PriceStrategyRules.Pack(s)), s) ? 0 : 1;
                    }
                }
            }
        }
        Check(wrong == 0 && PriceStrategyRules.Pack(none) == 0 && PriceStrategyRules.Unpack(0).IsDefault && PriceStrategyRules.Unpack(-5).IsDefault
              && PriceStrategyRules.Unpack(int.MaxValue).Margin == PriceStrategyRules.Margins[PriceStrategyRules.Margins.Length - 1],
            "price strategy: every strategy reads back from its number (" + tried + " tried), the default is 0 and a number past the lists is their last choice");
        // The managers' save line keeps a place's strategy in its spare slot.
        var state = new ManagerState { MonthFrom = 3, Today = 1 };
        var placed = new ManagerFile { Place = "Main%20St~3", Id = "m1", Role = ManagerCatalogue.RolePlace, From = 2, Tasks = 0x5b };
        placed.Strategy = new PriceStrategy { Stance = PriceStance.Undercut, Margin = 30, Ninety = true, Step = 10 };
        state.Managers.Add(placed);
        state.Managers.Add(new ManagerFile { Place = "Side%20St~1", Id = "m2", Role = ManagerCatalogue.RolePlace, From = 2 });
        string line = ManagerStore.Write(state);
        ManagerState back = ManagerStore.Read(line, out int version);
        Check(back != null && version == ManagerStore.Version && back.Managers.Count == 2 && SameStrategy(back.Managers[0].Strategy, placed.Strategy)
              && back.Managers[1].Strategy.IsDefault && ManagerStore.Write(back) == line,
            "price strategy: a place's strategy is kept in the managers' save line, and one without a strategy reads as the default");
        PricePlanSave();
    }

    // The pricing plans' own line: every plan with a strategy or prices kept reads back to the same line, its marks escaped;
    // another version or a broken line gives none, so the game's own reprice runs; an empty one is a save from before.
    private static void PricePlanSave()
    {
        var plans = new Dictionary<string, PricePlanFile>();
        var a = new PricePlanFile { Strategy = new PriceStrategy { Stance = PriceStance.Premium, Margin = 40, OverCost = true, Step = 5, Hold = true }, Day = 214, Hour = 6 };
        a.Yesterday["ba:item_cola"] = 225;
        a.Yesterday["odd|name^with;marks=and%"] = 1999;
        a.Today["ba:item_cola"] = 230;
        plans["plan|1^x"] = a;
        var b = new PricePlanFile { Day = -1, Hour = -1 };
        b.Today["ba:item_water"] = 175;
        plans["plan2"] = b;
        plans["empty"] = new PricePlanFile();   // nothing kept: not written
        string line = PricePlanStore.Write(plans);
        var back = new Dictionary<string, PricePlanFile>();
        bool read = PricePlanStore.Read(line, back);
        PricePlanFile ra = back.TryGetValue("plan|1^x", out PricePlanFile x) ? x : null;
        Check(read && back.Count == 2 && !back.ContainsKey("empty") && ra != null && SameStrategy(ra.Strategy, a.Strategy) && ra.Day == 214 && ra.Hour == 6
              && ra.Yesterday.Count == 2 && ra.Yesterday["odd|name^with;marks=and%"] == 1999 && ra.Today["ba:item_cola"] == 230 && back["plan2"].Today["ba:item_water"] == 175
              && PricePlanStore.Write(back) == line,
            "pricing plans: every plan with a strategy or prices kept reads back to the same line, its marks escaped, one with nothing left out");
        var none = new Dictionary<string, PricePlanFile> { ["left"] = new PricePlanFile() };
        Check(!PricePlanStore.Read("v=" + (PricePlanStore.Version + 1) + "^p|0|0|0|0|0|0|1|2||", none) && none.Count == 0
              && !PricePlanStore.Read("v=" + PricePlanStore.Version + "^p|9|0|0|0|0|0|1|2||", none) && none.Count == 0
              && !PricePlanStore.Read("v=" + PricePlanStore.Version + "^p|0|15|0|0|0|0|1|2||", none) && none.Count == 0
              && PricePlanStore.Read(null, none) && none.Count == 0 && PricePlanStore.Read(string.Empty, none),
            "pricing plans: another version or a broken line reads as none, so every plan reprices as the game's own; no line is a save from before");
        var many = new PricePlanFile();
        for (int i = 0; i < PricePlanStore.MostProducts + 50; i++)
        {
            many.Today["ba:item_" + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)] = 100 + i;
        }
        var big = new Dictionary<string, PricePlanFile> { ["big"] = many };
        var bigBack = new Dictionary<string, PricePlanFile>();
        Check(PricePlanStore.Read(PricePlanStore.Write(big), bigBack) && bigBack["big"].Today.Count == PricePlanStore.MostProducts,
            "pricing plans: a plan keeps at most " + PricePlanStore.MostProducts + " products' prices, so its line cannot grow without end");
    }

    // A list of choices as the pricing page offers them: none first, then each larger than the one before.
    private static bool Rising(int[] shares)
    {
        bool rising = shares.Length > 1 && shares[0] == 0;
        for (int i = 1; rising && i < shares.Length; i++)
        {
            rising = shares[i] > shares[i - 1];
        }
        return rising;
    }

    private static bool SameStrategy(PriceStrategy a, PriceStrategy b) =>
        a.Stance == b.Stance && a.Margin == b.Margin && a.Ninety == b.Ninety && a.OverCost == b.OverCost && a.Step == b.Step && a.Hold == b.Hold;
}
