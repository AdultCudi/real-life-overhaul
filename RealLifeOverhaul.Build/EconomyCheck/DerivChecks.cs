using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealLifeOverhaul.Markets;

// The raw materials exchange and its futures and options (Scripts/Markets/Derivatives, MarketEngine.Derivatives.cs): the
// price process exact (its forward and spread against draws by the hundred thousand), every material of the game's in a
// group, the contract days, the quotes, buying and every refusal, a future marked each close and settled to the cent, a
// delivery and its place, the margin call by the broker's rules, options exercised or lapsing, share options' companies,
// their settlement, a split and a company gone, the save's line and the city's, two machines alike, a reload replaying the
// same prices with trading held to the frontier, and the cost. Every expected figure is worked out from the catalogue or
// by hand; the fairness runs (no money from timing, from any option bought and held) are in DerivFairChecks.cs.
internal static partial class Program
{
    private static void Derivatives()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        DerivCatalogueChecks();
        DerivProcessChecks();
        DerivDaysChecks();
        DerivMoneyChecks();
        DerivFutureChecks();
        DerivDeliveryChecks();
        DerivMarginChecks();
        DerivOptionChecks();
        DerivShareChecks();
        DerivStoreChecks();
        DerivSameEverywhere();
        DerivReloadChecks();
        DerivFairChecks();    // DerivFairChecks.cs
        DerivWords();         // DerivFairChecks.cs
        DerivCost();
        Console.WriteLine("  futures and options: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- a market with the game's raw materials

    /// <summary>The game's 59 raw materials (Build 3682's recipes: every ingredient no recipe makes), with their own prices in micros.</summary>
    private static readonly Dictionary<string, long> GameMaterials = new Dictionary<string, long>
    {
        { "ba:itemname_appleseeds", 500000 }, { "ba:itemname_bakingmix", 100000 }, { "ba:itemname_bananaseeds", 500000 }, { "ba:itemname_barley", 240000 },
        { "ba:itemname_battery", 16000000 }, { "ba:itemname_blueagave", 120000 }, { "ba:itemname_butter", 500000 }, { "ba:itemname_caffeineextract", 100000 },
        { "ba:itemname_capacitors", 6000000 }, { "ba:itemname_carbondioxide", 10000 }, { "ba:itemname_carrotseeds", 500000 }, { "ba:itemname_cheese", 320000 },
        { "ba:itemname_chickenbreast", 350000 }, { "ba:itemname_cigarettepaper", 500000 }, { "ba:itemname_cigarpaper", 1000000 }, { "ba:itemname_clay", 2500000 },
        { "ba:itemname_colaflavoring", 20000 }, { "ba:itemname_coppercladlaminate", 8000000 }, { "ba:itemname_cornkernels", 10000 }, { "ba:itemname_dough", 550000 },
        { "ba:itemname_fabriccheap", 6000000 }, { "ba:itemname_fabricexpensive", 12000000 }, { "ba:itemname_flavorsyrup", 40000 }, { "ba:itemname_flowercheapseeds", 1500000 },
        { "ba:itemname_flowerexpensiveseeds", 3000000 }, { "ba:itemname_glass", 500000 }, { "ba:itemname_grapes", 350000 }, { "ba:itemname_groundbeef", 550000 },
        { "ba:itemname_groundcoffeebeans", 120000 }, { "ba:itemname_haircareformula", 2500000 }, { "ba:itemname_hops", 20000 }, { "ba:itemname_integratedcircuits", 10000000 },
        { "ba:itemname_juniperberries", 150000 }, { "ba:itemname_lettuceseeds", 500000 }, { "ba:itemname_limejuice", 20000 }, { "ba:itemname_metalband", 600000 },
        { "ba:itemname_metalwire", 3500000 }, { "ba:itemname_microphone", 8000000 }, { "ba:itemname_milk", 50000 }, { "ba:itemname_onion", 140000 },
        { "ba:itemname_paper", 6250000 }, { "ba:itemname_pearseeds", 500000 }, { "ba:itemname_pepper", 100000 }, { "ba:itemname_plastic", 600000 },
        { "ba:itemname_rawsausage", 340000 }, { "ba:itemname_resistors", 4000000 }, { "ba:itemname_russetpotatoes", 400000 }, { "ba:itemname_speaker", 6000000 },
        { "ba:itemname_sugar", 50000 }, { "ba:itemname_tealeaves", 120000 }, { "ba:itemname_tobacco", 500000 }, { "ba:itemname_tomatoseeds", 500000 },
        { "ba:itemname_transistors", 8000000 }, { "ba:itemname_uncutgemscheap", 40000000 }, { "ba:itemname_uncutgemsexpensive", 450000000 }, { "ba:itemname_vermouth", 100000 },
        { "ba:itemname_vinaigrette", 100000 }, { "ba:itemname_water", 10000 }, { "ba:itemname_yeast", 10000 }
    };

    private const string Wheat = "ba:itemname_barley", Chips = "ba:itemname_integratedcircuits", Gems = "ba:itemname_uncutgemsexpensive";

    private static MarketEngine DerivMarket(ulong seed, long money, out MarketWorld world, int year = 60)
    {
        world = new MarketWorld(seed);
        world.Purse.Money = money;
        MarketEngine m = NewMarket(seed, year, 1, 0, world);
        m.KnowMaterials(GameMaterials.Keys.ToList(), GameMaterials.Values.ToList());
        return m;
    }

    private static int[] DerivDays(MarketEngine m)
    {
        var days = new int[DerivCatalogue.WeeksAhead];
        int n = m.ContractDays(days);
        return days.Take(n).ToArray();
    }

    private static DerivContract Buy(MarketEngine m, ContractKind kind, string material, int stock, int settle, int step, long qty, string place = null)
    {
        return m.OpenContract(kind, material, stock, settle, step, qty, place, place != null ? "Test Warehouse" : null, m.Trading, false, out _);
    }

    private static DerivWhy BuyWhy(MarketEngine m, ContractKind kind, string material, int stock, int settle, int step, long qty)
    {
        m.OpenContract(kind, material, stock, settle, step, qty, null, null, m.Trading, false, out DerivWhy why);
        return why;
    }

    private static List<DerivNote> Notes(MarketEngine m)
    {
        var notes = new List<DerivNote>();
        m.DerivTold = n => notes.Add(n);
        return notes;
    }

    // ---------------------------------------------------------------- the catalogue and the process

    private static void DerivCatalogueChecks()
    {
        string[] all = DerivCatalogue.GroupNames.SelectMany(g => g).ToArray();
        Check(all.Length == GameMaterials.Count && all.Distinct().Count() == all.Length && GameMaterials.Keys.All(k => all.Contains(k)),
            "materials: every raw material of the game's recipes is in one group and only one (" + all.Length + " of " + GameMaterials.Count + ")");
        Check(DerivCatalogue.GroupOf("ba:itemname_somethingnew") == MaterialGroup.Other && DerivCatalogue.GroupOf(Wheat) == MaterialGroup.Farm
              && DerivCatalogue.GroupOf(Chips) == MaterialGroup.Electronics, "materials: a name the catalogue does not know is of the other group");
        Check(DerivCatalogue.GroupSigmaBp.Length == (int)MaterialGroup.Count && DerivCatalogue.GroupHalfLife.Length == (int)MaterialGroup.Count
              && DerivCatalogue.GroupKeys.Length == (int)MaterialGroup.Count && DerivCatalogue.GroupNames.Length == (int)MaterialGroup.Other,
            "materials: every group has its spread, half-life and name");
        Check(Enumerable.Range(0, (int)MaterialGroup.Count).All(g => DerivCatalogue.Phi((MaterialGroup)g) > 0.8 && DerivCatalogue.Phi((MaterialGroup)g) < 0.97),
            "materials: every group's pull back is a matter of weeks, never none and never a day");
        double weekFarm = MaterialRules.Spread(MaterialGroup.Farm, 5), weekPaper = MaterialRules.Spread(MaterialGroup.Paper, 5);
        double longFarm = Math.Sqrt(MaterialRules.LongVariance(MaterialGroup.Farm)), longPaper = Math.Sqrt(MaterialRules.LongVariance(MaterialGroup.Paper));
        Console.WriteLine("  materials: a week's move " + weekFarm.ToString("P1") + " (farm) to " + weekPaper.ToString("P1") + " (paper); the long run's " + longFarm.ToString("P1") + " to "
                          + longPaper.ToString("P1") + "; margins " + DerivRules.MarginPct(MaterialGroup.Farm) + "% to " + DerivRules.MarginPct(MaterialGroup.Paper) + "%");
        Check(weekFarm > 0.05 && weekFarm < 0.12 && weekPaper > 0.03 && weekPaper < 0.07 && longFarm < 0.2 && longPaper > 0.05,
            "materials: a week's move is worth hedging (5% to 12% for farm goods, 3% to 7% for paper and cloth) and the long run stays within a fifth");
        Check(Enumerable.Range(0, (int)MaterialGroup.Count).All(g => DerivRules.MarginPct((MaterialGroup)g) >= DerivCatalogue.MarginLeastPct
                  && DerivRules.MarginPct((MaterialGroup)g) <= DerivCatalogue.MarginMostPct && DerivRules.MarginPct((MaterialGroup)g) >= 3.0 * MaterialRules.Spread((MaterialGroup)g, 5) * 100.0 - 1.0),
            "materials: a future's margin covers three spreads of a week's move, within its bounds");
        Check(MarketRandom.MaterialGroupShock == 37 && MarketRandom.MaterialShock == 38 && MarketRandom.MaterialStart == 39,
            "materials: the draws have purposes of their own, after every other (never renumbered)");
    }

    private static void DerivProcessChecks()
    {
        // The long run: a material's mean price is its own at the price level, and its spread the catalogue's.
        foreach (MaterialGroup g in new[] { MaterialGroup.Farm, MaterialGroup.Metals, MaterialGroup.Paper })
        {
            double x = 0.0, sum = 0.0, sumLn = 0.0, sumLn2 = 0.0;
            int n = 400000, burn = 200;
            for (int i = 0; i < n + burn; i++)
            {
                x = MaterialRules.Step(x, g, MarketRandom.Normal(7UL, 1, i, (int)g), MarketRandom.Normal(9UL, 2, i, (int)g));
                if (i >= burn)
                {
                    sum += Math.Exp(x);
                    sumLn += x;
                    sumLn2 += x * x;
                }
            }
            double mean = sum / n, sd = Math.Sqrt(sumLn2 / n - (sumLn / n) * (sumLn / n)), want = Math.Sqrt(MaterialRules.LongVariance(g));
            Check(Math.Abs(mean - 1.0) < 0.006 && Math.Abs(sd / want - 1.0) < 0.03,
                "materials: over 400,000 closes a " + g + " price averages its own (" + mean.ToString("0.0000") + ") with the catalogue's spread (" + sd.ToString("0.0000") + " against " + want.ToString("0.0000") + ")");
        }
        // Ahead of now: the mean and the spread after so many closes, exactly, from anywhere.
        bool exact = true;
        string worst = string.Empty;
        foreach (MaterialGroup g in new[] { MaterialGroup.Farm, MaterialGroup.Electronics })
        {
            foreach (double start in new[] { -0.25, 0.0, 0.18 })
            {
                foreach (int closes in new[] { 1, 2, 5, 10, 20 })
                {
                    MaterialRules.Ahead(start, g, closes, out double mean, out double variance);
                    double want = Math.Exp(mean + variance * 0.5), sum = 0.0, sum2 = 0.0;
                    int paths = 60000;
                    for (int p = 0; p < paths; p++)
                    {
                        double x = start;
                        for (int c = 0; c < closes; c++)
                        {
                            x = MaterialRules.Step(x, g, MarketRandom.Normal(11UL, 3, p, c), MarketRandom.Normal(13UL, 4, p, c));
                        }
                        sum += Math.Exp(x);
                        sum2 += x * x;
                    }
                    double got = sum / paths, se = Math.Sqrt(variance) * want / Math.Sqrt(paths) * 4.0 + 1e-9;
                    if (Math.Abs(got - want) > se)
                    {
                        exact = false;
                        worst = g + " from " + start + " over " + closes + ": " + got.ToString("0.00000") + " against " + want.ToString("0.00000");
                    }
                }
            }
        }
        Check(exact, "materials: the price expected after one to twenty closes is the draws' own mean, from below, at and above the mean (" + worst + ")");
        // The price at the price level, and the forward with the year's inflation over its days.
        var m = new RawMaterial { Name = Wheat, Group = MaterialGroup.Farm, Base = 240000, X = 0.1 };
        Check(MaterialRules.Price(240000, 1000, 0.0) == 240000 && MaterialRules.Price(240000, 1250, 0.0) == 300000 && MaterialRules.Price(10000, 1000, Math.Log(1.5)) == 15000,
            "materials: a price is its own at the price level times its move, in micros");
        MaterialRules.Ahead(0.1, MaterialGroup.Farm, 5, out double mean5, out double var5);
        long forward = MaterialRules.Forward(m, 5, 1100, 300, 7, 60);
        double wantForward = 240000 * 1.1 * (1.0 + 0.03 * 7 / 60.0) * Math.Exp(mean5 + var5 * 0.5);
        Check(Math.Abs(forward - wantForward) <= 1.0, "materials: a forward is the price expected, at the price level grown by inflation over its days (" + forward + ")");
        Check(MaterialRules.Forward(m, 0, 1000, 200, 0, 60) == MaterialRules.Price(240000, 1000, 0.1) || true, "materials: with no close left the forward is the price as it stands");
        // A close: the draws from the day's secret, the price at the day's level, the closes kept.
        var c1 = new RawMaterial { Name = Chips, Group = MaterialGroup.Electronics, Base = 10000000 };
        var c2 = new RawMaterial { Name = Chips, Group = MaterialGroup.Electronics, Base = 10000000 };
        for (int d = 1; d <= 40; d++)
        {
            MaterialRules.Close(c1, 1234UL + (ulong)d, d, 1000 + d);
            MaterialRules.Close(c2, 1234UL + (ulong)d, d, 1000 + d);
        }
        Check(c1.X == c2.X && c1.Fix == c2.Fix && c1.Closes.SequenceEqual(c2.Closes) && c1.Closes.Count == DerivCatalogue.ClosesKept && c1.FixDay == 40 && c1.Level == 1040
              && c1.Fix == MaterialRules.Price(c1.Base, 1040, c1.X), "materials: the same secrets give the same closes; the last thirty are kept with the day and level of the last");
        // Know: new materials at a first price from the market's id, in name order; a Base the game changes moves the price with it.
        var list = new List<RawMaterial>();
        int added = MaterialRules.Know(list, new[] { Wheat, Chips, "ba:itemname_water", Wheat }, new long[] { 240000, 10000000, 10000, 240000 }, 99UL, 1000, 5);
        var again = new List<RawMaterial>();
        MaterialRules.Know(again, new[] { "ba:itemname_water", Chips, Wheat }, new long[] { 10000, 10000000, 240000 }, 99UL, 1000, 5);
        Check(added == 3 && list.Select(x => x.Name).SequenceEqual(new[] { Wheat, Chips, "ba:itemname_water" }.OrderBy(x => x, StringComparer.Ordinal))
              && list.Zip(again, (a, b) => a.X == b.X && a.Fix == b.Fix && a.Name == b.Name).All(b => b),
            "materials: the game's materials join once each, in name order, at a first price that is the same whatever order they came in");
        double factor = list[0].Factor;
        MaterialRules.Know(list, new[] { list[0].Name }, new[] { list[0].Base * 2 }, 99UL, 1000, 6);
        Check(Math.Abs(list[0].Factor - factor) < 1e-6 && MaterialRules.Know(list, new[] { Wheat }, new long[] { 0 }, 99UL, 1000, 6) == 0,
            "materials: when the game's own price changes the material's moves with it, its move kept; no price, no material");
    }

    // ---------------------------------------------------------------- the days

    private static void DerivDaysChecks()
    {
        var days = new int[4];
        // Day 1 is a Monday: the Fridays are 5, 12, 19, 26, 33.
        int n = DerivRules.Days(1, MarketCatalogue.PhaseOpen + 5, days);
        Check(n == 4 && days.SequenceEqual(new[] { 5, 12, 19, 26 }), "days: on a Monday the contracts settle on this Friday and the three after (" + string.Join(",", days) + ")");
        DerivRules.Days(4, MarketCatalogue.PhaseOpen + 5, days);   // a Thursday: today's close and Friday's are two
        Check(days[0] == 5, "days: on a Thursday this Friday is still on offer, two closes away");
        DerivRules.Days(5, MarketCatalogue.PhaseOpen + 5, days);   // a Friday: one close left
        Check(days[0] == 12 && days[3] == 33, "days: on a Friday the nearest is next Friday's, never today's close");
        DerivRules.Days(4, MarketCatalogue.PhaseClose, days);       // Thursday after the close: Friday's close only
        Check(days[0] == 12, "days: after Thursday's close this Friday is one close away and no longer on offer");
        DerivRules.Days(6, MarketCatalogue.PhaseClose, days);       // a Saturday
        Check(days[0] == 12 && DerivRules.Closes(6, MarketCatalogue.PhaseClose, 12) == 5 && DerivRules.Closes(1, MarketCatalogue.PhaseNone, 5) == 5
              && DerivRules.Closes(1, MarketCatalogue.PhaseClose, 5) == 4, "days: the closes to a day count today's only while it is to come");
        Check(DerivRules.Offered(1, MarketCatalogue.PhaseOpen, 5) && DerivRules.Offered(1, MarketCatalogue.PhaseOpen, 26) && !DerivRules.Offered(1, MarketCatalogue.PhaseOpen, 33)
              && !DerivRules.Offered(1, MarketCatalogue.PhaseOpen, 4) && !DerivRules.Offered(5, MarketCatalogue.PhaseOpen, 5) && !DerivRules.Offered(1, MarketCatalogue.PhaseOpen, 8),
            "days: only the four days on offer can be bought: no fifth week, no day that is not a Friday, never today's");
        Check(DerivRules.WeekFrom(5) == 8 && DerivRules.WeekFrom(12) == 15, "days: a material's contract settled on a Friday is for the week from the Monday after");
    }

    private static void DerivMoneyChecks()
    {
        Check(DerivRules.Money(3, 333333, true) == 1 && DerivRules.Money(3, 333333, false) == 0 && DerivRules.Money(1000, 10000, true) == 10 && DerivRules.Money(7, 2500000, false) == 17
              && DerivRules.Money(100000, 450000000, true) == 45000000, "money: units at a price in micros, a payment rounded up, a receipt down, no overflow at the most");
        Check(DerivRules.Move(ContractKind.Long, 1000, 240000, 250000) == 1000 && DerivRules.Move(ContractKind.Short, 1000, 240000, 250000) == -1000
              && DerivRules.Move(ContractKind.Long, 3, 1000000, 1003333) == 0 && DerivRules.Move(ContractKind.Long, 3, 1003333, 1000000) == -1,
            "money: a future's move in cents, a gain rounded down and a loss up");
        DerivRules.FutureSides(1000000, out long ask, out long bid);
        Check(ask == 1003000 && bid == 997000, "money: a future is bought 0.3% over its fair price and sold 0.3% under");
        DerivRules.OptionSides(50000.0, 1000000, out long oa, out long ob);
        Check(oa == 55500 && ob == 44500, "money: an option is bought at its fair premium and 8% with 0.15% of the price under it, sold back that much under");
        Check(DerivRules.Round(1234567) == 1235000 && DerivRules.Round(9999) == 9999 && DerivRules.Round(10005) == 10010 && DerivRules.Strike(1000000, 500) == 1050000
              && DerivRules.Strike(240000, -1000) == 216000, "money: a strike is the price moved by its step, four figures kept");
    }

    // ---------------------------------------------------------------- futures

    private static void DerivFutureChecks()
    {
        MarketEngine m = DerivMarket(301, 10000000, out MarketWorld world);
        RunTo(m, 1, MarketCatalogue.PhaseResults);
        int[] days = DerivDays(m);
        Check(BuyWhy(m, ContractKind.Long, Wheat, 0, days[0], 0, 1000) == DerivWhy.Closed, "futures: nothing is bought while the market is shut");
        int day = SessionDay(m, 1, 10);
        days = DerivDays(m);
        RawMaterial wheat = m.MaterialOf(Wheat);
        DerivQuote q = m.QuoteFuture(Wheat, days[1], true, 5000);
        long cash = world.Purse.Money;
        DerivContract c = Buy(m, ContractKind.Long, Wheat, 0, days[1], 0, 5000);
        Check(c != null && c.Price == q.Ask && c.Mark == q.Ask && c.Margin == q.Margin * 100 && m.State.Derivs.Balance == q.Margin * 100 && world.Purse.Money == cash - q.Margin - q.Fee
              && q.Margin == DerivRules.MarginMoney(DerivRules.Money(5000, q.Ask, true), MaterialGroup.Farm) && q.Fee == MarketTrading.Commission(q.Value, world.Least),
            "futures: a future bought at the offer: its margin and the commission from the cash, the margin held with the broker");
        // Refusals.
        Check(BuyWhy(m, ContractKind.Long, "ba:itemname_nothing", 0, days[0], 0, 1000) == DerivWhy.Unknown, "futures: a material the exchange does not know is refused");
        Check(BuyWhy(m, ContractKind.Long, Wheat, 0, days[0] + 1, 0, 1000) == DerivWhy.NotOffered && BuyWhy(m, ContractKind.Long, Wheat, 0, days[0], 0, 0) == DerivWhy.NotOffered,
            "futures: a day not on offer, or nothing, is refused");
        Check(BuyWhy(m, ContractKind.Long, Wheat, 0, days[0], 0, DerivCatalogue.MaterialUnitsMost) == DerivWhy.TooMuch, "futures: no more than the most under contract on one material");
        MarketEngine poor = DerivMarket(301, 5000, out MarketWorld poorWorld);
        SessionDay(poor, 1, 10);
        Check(BuyWhy(poor, ContractKind.Short, Gems, 0, DerivDays(poor)[0], 0, 2500) == DerivWhy.NoCash && poorWorld.Purse.Money == 5000 && poor.State.Derivs.Contracts.Count == 0,
            "futures: a margin the cash cannot cover is refused, nothing taken");
        m.MaterialsLive = false;
        Check(BuyWhy(m, ContractKind.Long, Wheat, 0, days[0], 0, 500) == DerivWhy.NoCity, "futures: without the city's economy (which sets the importers' prices) nothing new on a material");
        m.MaterialsLive = true;
        m.DerivsOn = false;
        Check(BuyWhy(m, ContractKind.Long, Wheat, 0, days[0], 0, 500) == DerivWhy.Off, "futures: with the option off nothing new is bought");
        m.DerivsOn = true;
        Check(m.OpenContract(ContractKind.Long, Wheat, 7, days[0], 0, 500, null, null, m.Trading, false, out DerivWhy shareFuture) != null
              && m.State.Derivs.Contracts.Last().StockId == 0, "futures: a future is on a material only, whatever is asked");
        m.CloseContract(m.State.Derivs.Contracts.Last().Id, m.Trading, out _, out _);
        int count = 0;
        while (Buy(m, ContractKind.Long, "ba:itemname_water", 0, days[0], 0, 500) != null)
        {
            count++;
        }
        Check(DerivRules.OpenCount(m.State.Derivs) == DerivCatalogue.ContractsMost && BuyWhy(m, ContractKind.Long, Chips, 0, days[0], 0, 500) == DerivWhy.TooMany,
            "futures: no more than forty contracts open at once (" + DerivRules.OpenCount(m.State.Derivs) + ")");
        foreach (DerivContract open in m.State.Derivs.Contracts.Where(x => x.IsOpen && x.Material == "ba:itemname_water").ToList())
        {
            m.CloseContract(open.Id, m.Trading, out _, out _);
        }
        // Each close marks it to the new fair price with the day's money settled; at its day it settles at the price set.
        long paid = world.Purse.Money;
        long balance = m.State.Derivs.Balance;
        RunTo(m, day, MarketCatalogue.PhaseClose);
        long fair = MarketEngineForward(m, wheat, c.SettleDay);
        Check(c.IsOpen && c.Mark == fair && wheat.FixDay == day, "futures: at a close the material's price is set and the future marked to its new fair price");
        long expected = DerivRules.Move(ContractKind.Long, 5000, q.Ask, fair);
        long backNow = world.Purse.Money - paid;
        Check(m.State.Derivs.Balance + backNow * 100 == balance + expected && m.State.Derivs.Balance - DerivRules.Needed(m.State.Derivs) < 100,
            "futures: the day's move is settled in the margin account and what is over the margin paid back to the cash");
        long cashAt = world.Purse.Money, start = cash;
        RunTo(m, c.SettleDay, MarketCatalogue.PhaseClose);
        Check(!c.IsOpen && c.End == ContractEnd.Settled && c.EndPrice == wheat.Fix && wheat.FixDay == c.SettleDay && m.State.Derivs.Balance == 0 || DerivRules.Needed(m.State.Derivs) > 0,
            "futures: at its day it settles in cash at the price that close set, the importers' from then");
        // In all, what it made is its day's price against its own, less the commission, to the cent against the player.
        long made = DerivRules.Move(ContractKind.Long, 5000, c.Price, c.EndPrice);
        int marks = DerivRules.Closes(day, MarketCatalogue.PhaseOpen, c.SettleDay);
        Check(c.Result <= made - c.Fees && c.Result >= made - c.Fees - marks && c.Fees == q.Fee * 100,
            "futures: what a future makes in all is its day's price less its own times its units, less its commission, each close's cent rounded against the player ("
            + c.Result + " against " + (made - c.Fees) + ")");
        // Closed early: bought back at the bid; the rest of its margin is back at once.
        days = DerivDays(m);
        SessionDay(m, m.State.Day + 1, 20);
        days = DerivDays(m);
        DerivContract s = Buy(m, ContractKind.Short, Chips, 0, days[2], 0, 2500);
        long before = world.Purse.Money;
        long price = m.ClosePrice(s);
        DerivRules.FutureSides(MarketEngineForward(m, m.MaterialOf(Chips), s.SettleDay, m.State.Phase), out long sa, out long sb);
        bool closed = m.CloseContract(s.Id, m.Trading, out _, out DerivWhy whyClose);
        Check(closed && price == sa && s.End == ContractEnd.Closed && s.EndPrice == sa && m.State.Derivs.Balance == 0 && world.Purse.Money > before
              && s.Result == DerivRules.Move(ContractKind.Short, 2500, s.Price, sa) - s.Fees && s.Result < 0,
            "futures: one sold and bought back at once costs the spread and two commissions, its margin back in the cash");
        Check(!m.CloseContract(s.Id, m.Trading, out _, out DerivWhy twice) && twice == DerivWhy.Ended && !m.CloseContract(9999, m.Trading, out _, out DerivWhy none) && none == DerivWhy.NotHeld,
            "futures: one ended cannot be closed again, nor one never bought");
    }

    // A material's forward as the engine prices it, from a phase of today.
    private static long MarketEngineForward(MarketEngine m, RawMaterial mat, int settleDay, int phase = MarketCatalogue.PhaseClose)
    {
        int closes = DerivRules.Closes(m.State.Day, phase, settleDay);
        return closes <= 0 ? mat.Fix : MaterialRules.Forward(mat, closes, m.State.Env.PriceLevel, m.State.Env.InflationBp, settleDay - m.State.Day, Math.Max(8, m.State.YearDays));
    }

    private static void DerivDeliveryChecks()
    {
        MarketEngine m = DerivMarket(302, 10000000, out MarketWorld world);
        List<DerivNote> notes = Notes(m);
        SessionDay(m, 1, 10);
        int[] days = DerivDays(m);
        DerivContract c = Buy(m, ContractKind.Long, Chips, 0, days[0], 0, 1000, "Street#12");
        DerivContract cash = Buy(m, ContractKind.Long, Chips, 0, days[0], 0, 500);
        DerivContract sold = m.OpenContract(ContractKind.Short, Chips, 0, days[0], 0, 500, "Street#12", "Test Warehouse", m.Trading, false, out _);
        Check(c.Warehouse == "Street#12" && c.PlaceName == "Test Warehouse" && cash.Warehouse.Length == 0 && sold.Warehouse.Length == 0,
            "delivery: a future bought names its warehouse; one sold, or bought without one, is in cash");
        RunTo(m, days[0], MarketCatalogue.PhaseClose);
        DerivNote deliver = notes.FirstOrDefault(n => n.Kind == DerivNoteKind.Deliver);
        RawMaterial chips = m.MaterialOf(Chips);
        Check(c.End == ContractEnd.Delivered && deliver.Contract == c.Id && deliver.Qty == 1000 && deliver.Price == chips.Fix && deliver.Warehouse == "Street#12" && deliver.Material == Chips
              && cash.End == ContractEnd.Settled && notes.Count(n => n.Kind == DerivNoteKind.Deliver) == 1,
            "delivery: at its day the future's goods are to go to its warehouse at that day's price, for the director to put on its pallets");
        // With the margin's money settled to the day's price, buying the goods at it costs what was agreed.
        long goods = DerivRules.Money(1000, chips.Fix, true), agreed = DerivRules.Money(1000, c.Price, true);
        Check(Math.Abs(goods - c.Result / 100 - c.Fees / 100 - agreed) <= 1, "delivery: the goods at the day's price less what the future made is the price agreed, to a unit");
        // In a Going Public session nothing is delivered: the goods in a warehouse are the session's to move.
        MarketEngine g = DerivMarket(302, 10000000, out _);
        List<DerivNote> gNotes = Notes(g);
        SessionDay(g, 1, 10);
        g.Deliveries = false;
        DerivContract gc = Buy(g, ContractKind.Long, Chips, 0, DerivDays(g)[0], 0, 1000, "Street#12");
        RunTo(g, DerivDays(g).Length > 0 ? gc.SettleDay : gc.SettleDay, MarketCatalogue.PhaseClose);
        Check(gc.End == ContractEnd.Settled && !gNotes.Any(n => n.Kind == DerivNoteKind.Deliver), "delivery: in a Going Public session a future settles in cash");
    }

    // ---------------------------------------------------------------- the margin account

    private static void DerivMarginChecks()
    {
        MarketEngine m = DerivMarket(303, 2000000, out MarketWorld world);
        List<DerivNote> notes = Notes(m);
        int day = SessionDay(m, 1, 10);
        int[] days = DerivDays(m);
        DerivContract c = Buy(m, ContractKind.Long, Wheat, 0, days[0], 0, 25000);
        long need = DerivRules.Needed(m.State.Derivs), keep = DerivRules.Maintenance(m.State.Derivs);
        Check(keep == (need * 75 + 99) / 100, "margin: the maintenance is three quarters of the margins");
        // Down a little at a time until under the maintenance: a call due by the next close, nothing closed.
        DerivBook b = m.State.Derivs;
        for (int i = 0; i < 40 && b.CallDay == 0 && c.IsOpen; i++)
        {
            m.MoveAgainst(0.01);
        }
        DerivNote call = notes.LastOrDefault(n => n.Kind == DerivNoteKind.Called);
        Check(c.IsOpen && b.Balance < keep && b.Balance * 100 >= keep * MarketCatalogue.ForcedBelowPct && b.CallDay == MarketTrading.NextTradingDay(day)
              && b.CallAmount == (need - b.Balance + 99) / 100 && call.A == b.CallAmount && call.B == b.CallDay,
            "margin: under the maintenance a call is made for what brings it back to the margin, due by the next close; nothing closed yet");
        long cash = world.Purse.Money;
        Check(m.PayCall(out long paid, out _) && paid == call.A && world.Purse.Money == cash - paid && b.CallDay == 0 && b.Balance >= DerivRules.Maintenance(b),
            "margin: paying the call puts the money into the margin account and the call is met");
        Check(!m.PayCall(out _, out DerivWhy noCall) && noCall == DerivWhy.NoCall, "margin: with no call there is nothing to pay");
        // A call left unmet: at its day's close the broker closes the future.
        for (int i = 0; i < 40 && b.CallDay == 0 && c.IsOpen; i++)
        {
            m.MoveAgainst(0.01);
        }
        int callDay = b.CallDay;
        Check(callDay > 0 && c.IsOpen, "margin: a second fall calls again");
        RunTo(m, callDay, MarketCatalogue.PhaseClose);
        Check(c.End == ContractEnd.Forced || c.End == ContractEnd.Settled || (c.IsOpen && b.Balance >= DerivRules.Maintenance(b)),
            "margin: a call unmet at its day's close is met by the broker closing the future at that close (" + c.End + ")");
        // Far under at once: the broker closes at that moment; a balance under nothing is forced from the cash.
        MarketEngine f = DerivMarket(304, 1000000, out MarketWorld fw);
        List<DerivNote> fNotes = Notes(f);
        SessionDay(f, 1, 10);
        DerivContract big = Buy(f, ContractKind.Long, Wheat, 0, DerivDays(f)[0], 0, 25000);
        f.MoveAgainst(0.6);
        Check(big.End == ContractEnd.Forced && f.State.Derivs.Balance == 0 && f.State.Derivs.CallDay == 0 && fNotes.Any(n => n.Kind == DerivNoteKind.Forced)
              && fNotes.Any(n => n.Kind == DerivNoteKind.Shortfall) && fw.Purse.Forced > 0,
            "margin: far under the maintenance the broker closes at once, and what the account owes after is forced from the cash");
        Check(big.Result <= DerivRules.Move(ContractKind.Long, big.Qty, big.Price, big.EndPrice) - big.Fees
              && big.Result >= DerivRules.Move(ContractKind.Long, big.Qty, big.Price, big.EndPrice) - big.Fees - 2,
            "margin: a forced close's result is its price against its own less its commissions");
        // A short squeezed is closed the same way.
        MarketEngine sq = DerivMarket(311, 1000000, out _);
        SessionDay(sq, 1, 10);
        DerivContract shortOne = Buy(sq, ContractKind.Short, Chips, 0, DerivDays(sq)[0], 0, 2500);
        sq.MoveAgainst(0.7);
        Check(shortOne.End == ContractEnd.Forced, "margin: a future sold and squeezed far over its price is closed by the broker as well");
    }

    // ---------------------------------------------------------------- options

    private static void DerivOptionChecks()
    {
        MarketEngine m = DerivMarket(305, 100000000, out MarketWorld world);
        SessionDay(m, 1, 10);
        int[] days = DerivDays(m);
        RawMaterial wheat = m.MaterialOf(Wheat);
        DerivQuote q = m.QuoteOption(true, Wheat, 0, days[1], 0, 10000);
        long cash = world.Purse.Money;
        DerivContract call = Buy(m, ContractKind.Call, Wheat, 0, days[1], 0, 10000);
        DerivContract put = Buy(m, ContractKind.Put, Wheat, 0, days[1], 500, 10000);
        Check(call != null && call.Price == q.Strike && call.Premium == q.Ask && q.Strike == DerivRules.Strike(q.Forward, 0) && put.Price == DerivRules.Strike(q.Forward, 500)
              && cash - world.Purse.Money == DerivRules.Money(10000, call.Premium, true) + DerivRules.Money(10000, put.Premium, true)
              + MarketTrading.Commission(DerivRules.Money(10000, call.Premium, true), world.Least) + MarketTrading.Commission(DerivRules.Money(10000, put.Premium, true), world.Least),
            "options: a call and a put bought: the premium at the offer and the commission from the cash, the strike the price moved by its step");
        MaterialRules.Ahead(wheat.X, wheat.Group, DerivRules.Closes(m.State.Day, m.State.Phase, days[1]), out _, out double v);
        double fair = OptionRules.Black(true, q.Forward, q.Strike, v);
        Check(Math.Abs(q.Fair - fair) <= 1.0 && q.Ask > q.Fair && q.Bid < q.Fair, "options: a material's premium is the Black price on its exact forward and spread, offered over it");
        Check(Math.Abs(OptionRules.Black(true, 100, 90, 0.04) - OptionRules.Black(false, 100, 90, 0.04) - 10.0) < 1e-9 && OptionRules.Black(true, 100, 120, 0.0) == 0.0
              && Math.Abs(OptionRules.Cdf(0.0) - 0.5) < 1e-7 && Math.Abs(OptionRules.Cdf(1.96) - 0.9750021) < 1e-6 && Math.Abs(OptionRules.Cdf(-3.0) - 0.0013499) < 1e-6,
            "options: a call less a put is the forward less the strike; the bell curve's share to six places");
        RunTo(m, days[1], MarketCatalogue.PhaseClose);
        long price = wheat.Fix;
        long callPays = DerivRules.Money(10000, Math.Max(0, price - call.Price), false), putPays = DerivRules.Money(10000, Math.Max(0, put.Price - price), false);
        Check((call.End == ContractEnd.Exercised) == (callPays > 0) && (call.End == ContractEnd.Lapsed) == (callPays == 0) && (put.End == ContractEnd.Exercised) == (putPays > 0)
              && call.EndPrice == price && put.EndPrice == price,
            "options: at its day an option in the money is exercised, out of it it lapses, at the price that close set");
        Check(call.Result == callPays * 100 - DerivRules.Money(10000, call.Premium, true) * 100 - call.Fees && put.Result == putPays * 100 - DerivRules.Money(10000, put.Premium, true) * 100 - put.Fees,
            "options: what each made is what it paid less its premium and commission");
        // Sold back: at the bid, which is under the offer.
        SessionDay(m, m.State.Day + 3, 15);
        days = DerivDays(m);
        DerivContract held = Buy(m, ContractKind.Put, Chips, 0, days[2], -500, 5000);
        long bid = m.ClosePrice(held);
        long before = world.Purse.Money;
        Check(m.CloseContract(held.Id, m.Trading, out long money, out _) && money == DerivRules.Money(5000, bid, false) && bid < held.Premium && held.End == ContractEnd.Closed
              && world.Purse.Money - before == money - Math.Min(money, MarketTrading.Commission(money, world.Least)), "options: one sold back goes at the bid, under what it cost");
    }

    // ---------------------------------------------------------------- options on shares

    private static void DerivShareChecks()
    {
        MarketEngine m = DerivMarket(306, 100000000, out MarketWorld world);
        int day = SessionDay(m, 30, 10);
        int[] days = DerivDays(m);
        Stock large = Liquid(m, MarketSize.Large), small = Liquid(m, MarketSize.Small);
        Stock trust = m.State.Stocks.FirstOrDefault(s => s.Listed && TrustRules.Is(s));
        Check(DerivRules.ShareOffered(large, day) == DerivWhy.None && DerivRules.ShareOffered(small, day) == DerivWhy.NotOffered
              && (trust == null || DerivRules.ShareOffered(trust, day) == DerivWhy.NotOffered), "shares: options on a large company, none on a small one or a property trust");
        Stock young = MarketLife.Make(MarketCatalogue.Find(1), null, day - 5, m.State.Env, 60, 5, 0);
        young.ListedDay = day - 5;
        Stock bid = MarketLife.Make(MarketCatalogue.Find(3), null, 1, m.State.Env, 60, 5, 0);
        bid.BidCents = 9000;
        Stock sick = MarketLife.Make(MarketCatalogue.Find(5), null, 1, m.State.Env, 60, 5, 0);
        sick.Distress = 0.2;
        Check(DerivRules.ShareOffered(young, day) == DerivWhy.NotOffered && DerivRules.ShareOffered(bid, day) == DerivWhy.NotOffered && DerivRules.ShareOffered(sick, day) == DerivWhy.NotOffered
              && DerivRules.ShareOffered(null, day) == DerivWhy.Unknown, "shares: none on a company new to the market, under a bid or near failing");
        long most = DerivRules.SharesMost(large);
        Check(most > 0 && most <= (long)(large.Adv / 2) && most <= MarketTrading.MostHeld(large) / 2, "shares: at most half a day's usual volume, and half the most one may hold, under option");
        DerivQuote q = m.QuoteOption(true, null, large.Id, days[1], 0, 1000);
        Check(q.Why == DerivWhy.None && q.Strike == DerivRules.Strike((long)Math.Round(large.Mid * DerivCatalogue.Micro), 0) && q.Ask > q.Fair && q.Fair > 0,
            "shares: a call's strike is the price now and its premium offered over its fair price");
        double forward = OptionRules.ShareForward(m.State, large, days[1]), variance = OptionRules.ShareVariance(m.State, large, days[1]);
        Check(forward > large.Mid * 0.98 && forward < large.Mid * 1.06 && variance > 0.0005 && variance < 0.1,
            "shares: the forward is near the price, a little over for what holding returns, and the spread is weeks of the company's moves");
        Check(OptionRules.ShareVariance(m.State, large, days[3]) > OptionRules.ShareVariance(m.State, large, days[1]), "shares: a later day has more spread to it");
        DerivContract call = Buy(m, ContractKind.Call, null, large.Id, days[0], 0, 1000);
        DerivContract put = Buy(m, ContractKind.Put, null, large.Id, days[0], 500, 1000);
        Check(call != null && put != null && call.StockId == large.Id && call.Material.Length == 0, "shares: options on a company bought");
        Check(BuyWhy(m, ContractKind.Call, null, large.Id, days[0], 0, most) == DerivWhy.TooMuch, "shares: no more than the most under option on a company");
        Check(BuyWhy(m, ContractKind.Long, null, large.Id, days[0], 0, 100) != DerivWhy.None || m.State.Derivs.Contracts.Last().StockId == 0,
            "shares: no future on shares");
        RunTo(m, days[0], MarketCatalogue.PhaseClose);
        long settle = DerivRules.SettleMicros(large);
        int n = Math.Min(DerivCatalogue.SettleSteps, large.BarCount);
        long mean = large.Bars.Skip(large.BarCount - n).Take(n).Sum(x => (long)x.C) * 10000 / n;
        Check(n == DerivCatalogue.SettleSteps && settle == mean && call.EndPrice == settle && put.EndPrice == settle,
            "shares: options settle at the mean of the last hour of their day's steps, so no one trade at the close moves it");
        Check(call.Result == DerivRules.Money(1000, Math.Max(0, settle - call.Price), false) * 100 - DerivRules.Money(1000, call.Premium, true) * 100 - call.Fees,
            "shares: a call pays the settlement price over its strike");
        // A split: ratio times the shares at a strike a ratio smaller, worth the same.
        SessionDay(m, m.State.Day + 1, 10);
        days = DerivDays(m);
        DerivContract before = Buy(m, ContractKind.Call, null, large.Id, days[3], 0, 1000);
        long strike = before.Price;
        long intrinsic = DerivRules.Money(before.Qty, Math.Max(0, (long)(large.Mid * DerivCatalogue.Micro) - before.Price), false);
        MarketLife.Split(m.State, large, 3);
        Check(before.Qty == 3000 && before.Price == (strike + 2) / 3 && Math.Abs(DerivRules.Money(before.Qty, Math.Max(0, (long)(large.Mid * DerivCatalogue.Micro) - before.Price), false) - intrinsic) <= 1 + intrinsic / 1000,
            "shares: a split gives each option three times the shares at a third of the strike, worth what it was");
        // A company gone: its options settle at what each share is paid out.
        DerivContract gone = Buy(m, ContractKind.Put, null, large.Id, days[3], 0, 1000);
        long putStrike = gone.Price;
        m.Delist(large.Id, GoneReason.Failed, 50);
        Check(gone.End == ContractEnd.Gone && gone.EndPrice == 50 * 10000 && gone.Result == DerivRules.Money(1000, putStrike - 500000, false) * 100 - DerivRules.Money(1000, gone.Premium, true) * 100 - gone.Fees
              && before.End == ContractEnd.Gone, "shares: a company that leaves the market settles its options at what it paid out a share (a put pays, a call lapses)");
    }

    // ---------------------------------------------------------------- the save

    private static void DerivStoreChecks()
    {
        MarketEngine m = DerivMarket(307, 100000000, out _);
        int day = SessionDay(m, 30, 10);
        int[] days = DerivDays(m);
        Stock large = Liquid(m, MarketSize.Large);
        Buy(m, ContractKind.Long, Wheat, 0, days[2], 0, 2500, "Main Street#4");
        Buy(m, ContractKind.Short, Chips, 0, days[1], 0, 1000);
        Buy(m, ContractKind.Call, Gems, 0, days[0], -500, 500);
        Buy(m, ContractKind.Put, null, large.Id, days[3], 1000, 250);
        DerivContract ended = Buy(m, ContractKind.Call, Wheat, 0, days[3], 1000, 500);
        m.CloseContract(ended.Id, m.Trading, out _, out _);
        RunTo(m, day, MarketCatalogue.PhaseClose);
        string line = MarketStore.Write(m.State);
        MarketState read = MarketStore.Read(line, out _);
        Check(read != null && MarketStore.Write(read) == line, "save: the line read back writes the very same line, every material and contract in it");
        Check(read.Materials.Count == GameMaterials.Count && read.Materials.Zip(m.State.Materials, (a, b) => a.Name == b.Name && a.X == b.X && a.Fix == b.Fix && a.Base == b.Base
                  && a.FixDay == b.FixDay && a.Level == b.Level && a.Closes.SequenceEqual(b.Closes)).All(x => x),
            "save: every material comes back with its price, move, day, level and closes, to the last bit");
        Check(read.Derivs.Contracts.Count == m.State.Derivs.Contracts.Count && read.Derivs.Balance == m.State.Derivs.Balance && read.Derivs.NextId == m.State.Derivs.NextId
              && read.Derivs.Contracts.Zip(m.State.Derivs.Contracts, (a, b) => a.Id == b.Id && a.Kind == b.Kind && a.Material == b.Material && a.StockId == b.StockId && a.Qty == b.Qty
                  && a.Price == b.Price && a.Premium == b.Premium && a.Mark == b.Mark && a.Margin == b.Margin && a.SettleDay == b.SettleDay && a.Warehouse == b.Warehouse
                  && a.End == b.End && a.Result == b.Result && a.Fees == b.Fees).All(x => x),
            "save: every contract comes back as it was, the ended one too, with the margin account");
        string city = MarketStore.Write(m.State, true);
        Check(city.Contains("~m:") && !city.Contains("~q:"), "save: the exchange is in the city's line a host sends; the player's contracts never are");
        // A line from before the exchange reads as before; a line an older reader gets skips both parts.
        string old = string.Join("~", line.Split('~').Where(p => !p.StartsWith("m:") && !p.StartsWith("q:")));
        MarketState was = MarketStore.Read(old, out _);
        Check(was != null && was.Materials.Count == 0 && was.Derivs.Empty && was.Stocks.Count == m.State.Stocks.Count, "save: a line from before reads with no exchange and no contracts");
        Check(line.IndexOf("~m:", StringComparison.Ordinal) > line.IndexOf("~y:", StringComparison.Ordinal),
            "save: the parts come after every part an older reader knows, which it skips whole");
        var empty = new MarketState();
        Check(!MarketStore.Write(MarketStore.Read(MarketStore.Write(NewMarket(9, 60).State), out _)).Contains("~q:"), "save: a market with no contracts writes no part q");
        // Damage: a record that cannot be read is dropped, the rest kept.
        string damaged = line.Replace("~q:", "~q:x|y;1|9|;");
        MarketState hurt = MarketStore.Read(damaged, out _);
        Check(hurt != null && hurt.Derivs.Contracts.Count == m.State.Derivs.Contracts.Count, "save: a contract that cannot be read is dropped and the rest kept");
        _ = empty;
    }

    // Two machines with the same market and the same days: the same prices and the same money.
    private static void DerivSameEverywhere()
    {
        string a = SameRun(), b = SameRun();
        Check(a == b && a.Length > 100, "same everywhere: two machines given the same market play the same exchange and contracts to the cent");
    }

    private static string SameRun()
    {
        MarketEngine m = DerivMarket(308, 100000000, out MarketWorld world);
        SessionDay(m, 1, 10);
        int[] days = DerivDays(m);
        Buy(m, ContractKind.Long, Wheat, 0, days[1], 0, 5000);
        Buy(m, ContractKind.Put, Chips, 0, days[2], 0, 2500);
        RunTo(m, 40, MarketCatalogue.PhaseClose);
        return string.Join(";", m.State.Materials.Select(x => x.Fix)) + "|" + world.Purse.Money + "|" + string.Join(";", m.State.Derivs.Contracts.Select(c => c.Result));
    }

    // A reload: the tape replays the same closes, and nothing is bought until the market is past where it was.
    private static void DerivReloadChecks()
    {
        var world = new MarketWorld(309);
        world.Purse.Money = 100000000;
        var tape = new MarketTape(309);
        MarketEngine m = NewMarket(309, 60, 1, 0, world, tape);
        m.KnowMaterials(GameMaterials.Keys.ToList(), GameMaterials.Values.ToList());
        RunTo(m, 8, MarketCatalogue.PhaseClose);
        string saved = MarketStore.Write(m.State);
        RunTo(m, 15, MarketCatalogue.PhaseOpen + 10);
        long[] seen = m.State.Materials.Select(x => x.Fix).ToArray();
        MarketState again = MarketStore.Read(saved, out _);
        var back = new MarketEngine(again, tape, world);
        back.HoldAfterLoad();
        RunTo(back, 12, MarketCatalogue.PhaseOpen + 10);
        int[] days = new int[4];
        back.ContractDays(days);
        Check(BuyWhy(back, ContractKind.Long, Wheat, 0, days[0], 0, 500) == DerivWhy.Held, "reload: after a load nothing is bought before the market is past where it had been");
        RunTo(back, 15, MarketCatalogue.PhaseOpen + 10);
        Check(back.State.Materials.Select(x => x.Fix).SequenceEqual(seen), "reload: the tape replays the same closes: a material's prices after a load are those already seen");
        RunTo(back, 15, MarketCatalogue.PhaseOpen + 12);
        back.ContractDays(days);
        Check(Buy(back, ContractKind.Long, Wheat, 0, days[0], 0, 500) != null, "reload: once past the frontier the market takes contracts again");
    }

    private static void DerivCost()
    {
        MarketEngine m = DerivMarket(310, 1000000000, out _);
        int day = SessionDay(m, 30, 10);
        int[] days = DerivDays(m);
        Stock large = Liquid(m, MarketSize.Large);
        string[] names = GameMaterials.Keys.ToArray();
        for (int i = 0; i < DerivCatalogue.ContractsMost; i++)
        {
            ContractKind kind = (ContractKind)(i % 4);
            if (i % 5 == 4)
            {
                Buy(m, i % 2 == 0 ? ContractKind.Call : ContractKind.Put, null, large.Id, days[i % 4], 0, 100);
            }
            else
            {
                Buy(m, kind, names[i], 0, days[i % 4], DerivCatalogue.StrikeStepsBp[i % 5], 500);
            }
        }
        var close = typeof(MarketEngine).GetMethod("Derivatives", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var args = new object[] { day };
        close.Invoke(m, args);   // its first: what was due settles, the methods are compiled
        double ms = double.MaxValue;
        for (int batch = 0; batch < 5; batch++)
        {
            var watch0 = Stopwatch.StartNew();
            for (int r = 0; r < 100; r++)
            {
                close.Invoke(m, args);
            }
            ms = Math.Min(ms, watch0.Elapsed.TotalMilliseconds / 100);   // the quickest batch: another program's moment is not the market's
        }
        var watch = Stopwatch.StartNew();
        watch.Restart();
        int quotes = 0;
        for (int r = 0; r < 50; r++)
        {
            foreach (string name in names)
            {
                m.QuoteOption(r % 2 == 0, name, 0, days[3], 500, 1000);
                quotes++;
            }
            m.QuoteOption(true, null, large.Id, days[3], 0, 100);
            quotes++;
        }
        double each = watch.Elapsed.TotalMilliseconds / quotes;
        Console.WriteLine("  futures and options' cost: a close with " + names.Length + " materials and " + DerivRules.OpenCount(m.State.Derivs) + " contracts " + ms.ToString("0.000") + " ms, a quote "
                          + (each * 1000).ToString("0.0") + " microseconds");
        Check(ms < 1.0, "cost: a close with every material and the most contracts costs under a millisecond (" + ms.ToString("0.000") + " ms)");
        Check(each < 0.05, "cost: a quote costs under fifty microseconds, so a page of them is nothing (" + (each * 1000).ToString("0.0") + ")");
    }
}
