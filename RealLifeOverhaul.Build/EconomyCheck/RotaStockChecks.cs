using System;
using RealLifeOverhaul.Economy;

// The checks of the staff rota planner's goods (Scripts/Economy/Company/RotaStock.cs, and the goods in
// RotaLadder.Evaluate): a product sells only while some is on site; the Monday delivery, the morning top-up of a
// Logistics Manager and the room for them; paper bags; what would lift a product that runs out; and that a ladder with
// goods to run out never counts sales the shelves cannot supply, and ends where the goods, not the staff, hold it back.
internal static partial class Program
{
    private static void RotaStockChecks()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--rota-goods") >= 0)
        {
            RotaGoodsExample();
        }
        RotaStockRuns();
        RotaStockLifts();
        RotaStockBags();
        RotaStockSplit();
        RotaStockLadder();
    }

    // One product: a customer takes one unit, a unit leaves 5.00.
    private static RotaStock RotaGoods(int weekly, int target = 0, int shelf = 100, int box = 50, int boxes = 20, int onSite = 0)
    {
        var s = new RotaStock();
        s.Products = 1;
        s.PerCustomer[0] = 1000;
        s.UnitMargin[0] = 500;
        s.OnSite[0] = onSite;
        s.Weekly[0] = weekly;
        s.Target[0] = target;
        s.Shelf[0] = shelf;
        s.Box[0] = box;
        s.Boxes = boxes;
        return s;
    }

    // So many customers served at noon each day (a different number on a Saturday if given).
    private static int[] RotaNoon(int daily, int saturday = -1)
    {
        var served = new int[RotaCatalogue.Cells];
        for (int d = 0; d < RotaCatalogue.Days; d++)
        {
            served[d * 24 + 12] = d == 5 && saturday >= 0 ? saturday : daily;
        }
        return served;
    }

    // Two weeks of the goods, the second counted, as RotaLadder.Evaluate runs them; returns what the counted week's customers leave.
    private static long RotaGoodsWeek(RotaStock s, int[] served, RotaStockWeek into)
    {
        var run = new RotaStockRun();
        run.Start(s);
        into.Clear(s.Products);
        long value = 0;
        for (int lap = 0; lap < 2; lap++)
        {
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                RotaStockWeek counted = lap == 1 ? into : null;
                run.Arrive(cell, counted);
                long sold = run.Sell(cell, served[cell], 1000, counted);
                value += lap == 1 ? sold : 0;
            }
        }
        return value;
    }

    private static void RotaStockRuns()
    {
        // A delivery of 500 on a Monday, 100 customers a day: Monday to Friday sell, Saturday and Sunday find none.
        RotaStock s = RotaGoods(500);
        var w = new RotaStockWeek();
        long value = RotaGoodsWeek(s, RotaNoon(100), w);
        Check(w.Out[0] == 5 * 24 + 12, "rota goods: 500 delivered on a Monday at 100 a day run out at noon on the Saturday (hour " + w.Out[0] + ")");
        Check(w.Sold[0] == 500000 && w.Want[0] == 700000 && w.WantUnits(0) == 700, "rota goods: of the 700 a week wants, the 500 there are are sold");
        Check(value == 500L * 500 && w.Lost[0] == 200L * 500 && w.LostAll == w.Lost[0] && w.Short == 1, "rota goods: the week brings 2500.00, and the 200 missing would have left 1000.00 (" + value + ", " + w.Lost[0] + ")");
        Check(w.Delivered[0] == 500 && w.Refused[0] == 0, "rota goods: all of the delivery fits on the shelves and in storage");

        // Enough delivered: nothing runs out, and the week is what an unlimited one would be.
        s = RotaGoods(800);
        value = RotaGoodsWeek(s, RotaNoon(100), w);
        Check(w.Out[0] < 0 && w.Short == 0 && w.LostAll == 0 && value == 700L * 500, "rota goods: with 800 a week for a week that wants 700, nothing runs out");
        Check(RotaLift.Of(s, w, 0) == RotaLift.None, "rota goods: and nothing needs lifting");

        // What is there now carries into the first week; the second is the one counted, with only the delivery behind it.
        s = RotaGoods(500, onSite: 2000);
        value = RotaGoodsWeek(s, RotaNoon(100), w);
        Check(w.Out[0] < 0, "rota goods: 2000 there now and 500 a week still cover the week after next at 700 a week");
        s = RotaGoods(500, onSite: 300);
        value = RotaGoodsWeek(s, RotaNoon(100), w);
        Check(w.Out[0] == 6 * 24 + 12, "rota goods: 300 there now leave 100 for the week counted, so it runs out a day later, on the Sunday (hour " + w.Out[0] + ")");

        // Nothing brings it: not capped, sold to everybody, and how long the stock lasts is the page's to say.
        s = RotaGoods(0, onSite: 300);
        Check(!s.Capped(0) && !s.Any, "rota goods: a product no delivery and no Logistics Manager brings is not capped");
        value = RotaGoodsWeek(s, RotaNoon(100), w);
        Check(w.Short == 0 && value == 700L * 500 && w.DaysOf(0, 300) == 3, "rota goods: it is counted as sold to everybody, and its 300 last 3 days of 700 a week");

        // The same run gives the same week every time.
        var again = new RotaStockWeek();
        RotaGoodsWeek(RotaGoods(500), RotaNoon(100), w);
        RotaGoodsWeek(RotaGoods(500), RotaNoon(100), again);
        Check(w.Out[0] == again.Out[0] && w.Sold[0] == again.Sold[0] && w.Lost[0] == again.Lost[0], "rota goods: the same goods and customers give the same week every time");
    }

    private static void RotaStockLifts()
    {
        var w = new RotaStockWeek();

        // Too small a delivery, under the wholesaler's weekly limit: a larger amount, or an Urgent Order.
        RotaStock s = RotaGoods(500);
        s.Most[0] = 5000;
        RotaGoodsWeek(s, RotaNoon(100), w);
        Check(RotaLift.Of(s, w, 0) == RotaLift.Order, "rota goods: a delivery under the weekly limit that runs out: order more");

        // At the weekly limit of one wholesaler: a second wholesaler or a Logistics Manager, not an Urgent Order.
        s = RotaGoods(5000, shelf: 1000, box: 100, boxes: 100);
        s.Most[0] = 5000;
        RotaGoodsWeek(s, RotaNoon(1000), w);
        Check(w.Out[0] >= 0 && RotaLift.Of(s, w, 0) == RotaLift.Second, "rota goods: a delivery at the most one wholesaler sends a week that runs out: a second wholesaler or a delivery plan");

        // No room: what the shelves and the storage cannot hold goes back, and that is what to lift first.
        s = RotaGoods(2000);
        RotaGoodsWeek(s, RotaNoon(300), w);
        Check(w.Delivered[0] == 1100 && w.Refused[0] == 900, "rota goods: of 2000 delivered, 100 on the shelves and 20 boxes of 50 fit; 900 go back (" + w.Delivered[0] + ", " + w.Refused[0] + ")");
        Check(w.Out[0] >= 0 && RotaLift.Of(s, w, 0) == RotaLift.Room, "rota goods: and the goods then run out for want of room");

        // Boxes are whole: two products share the storage, a box each at the least.
        var two = new RotaStock { Products = 2, Boxes = 3 };
        for (int p = 0; p < 2; p++)
        {
            two.PerCustomer[p] = 1000;
            two.UnitMargin[p] = 100;
            two.Shelf[p] = 10;
            two.Box[p] = 50;
            two.Weekly[p] = 200;
        }
        var fresh = new RotaStockRun();
        fresh.Start(two);
        Check(fresh.Room(0) == 10 + 3 * 50 && fresh.Room(1) == 10 + 3 * 50, "rota goods: with nothing there each product has room for its shelves and all three box places");
        fresh.Arrive(RotaStock.DeliveryHour, null);
        Check(fresh.Left(0) == 160 && fresh.Left(1) == 10 && fresh.Room(1) == 0, "rota goods: the first delivered takes the three box places, and the second fits only on its shelves (" + fresh.Left(0) + " and " + fresh.Left(1) + ")");
        fresh.Sell(12, 1, 1000, null);
        Check(fresh.Left(0) == 159 && fresh.Room(0) == 1, "rota goods: a unit sold makes room for a unit in a part-filled box");

        // A Logistics Manager tops it up to 120 each morning: enough for 100 a day, not for a Saturday of 150.
        s = RotaGoods(0, target: 120);
        long value = RotaGoodsWeek(s, RotaNoon(100, 150), w);
        Check(s.Capped(0) && w.Out[0] == 5 * 24 + 12 && w.Sold[0] == (6 * 100 + 120) * 1000L, "rota goods: a top-up to 120 each morning sells 100 a day and 120 of a Saturday's 150 (" + w.Sold[0] / 1000 + ")");
        Check(w.DayUnits(0) == 150 && RotaLift.Of(s, w, 0) == RotaLift.Target, "rota goods: the busiest day wants 150, and a higher target is what would cover it");
        Check(w.Delivered[0] == 6 * 100 + 120, "rota goods: each morning brings what the day before took (" + w.Delivered[0] + ")");
    }

    private static void RotaStockBags()
    {
        // 300 bags a week for 100 customers a day: from Thursday nobody buys goods. Entrance fees still come in.
        var s = new RotaStock { Products = 2, Boxes = 50, Other = 100 };
        s.Bag[0] = true;
        s.PerCustomer[0] = 1000;
        s.UnitMargin[0] = 0;
        s.Shelf[0] = 50;
        s.Box[0] = 100;
        s.Weekly[0] = 300;
        s.PerCustomer[1] = 2000;     // two of the goods a customer
        s.UnitMargin[1] = 300;
        s.Shelf[1] = 100;
        s.Box[1] = 100;
        s.Weekly[1] = 5000;
        var w = new RotaStockWeek();
        long value = RotaGoodsWeek(s, RotaNoon(100), w);
        Check(w.Out[0] == 3 * 24 + 12 && w.Sold[0] == 300000, "rota goods: 300 bags a week for 100 customers a day run out on the Thursday");
        Check(w.Sold[1] == 600000 && w.Out[1] < 0, "rota goods: without a bag nobody buys goods, so only 300 customers buy their two (" + w.Sold[1] / 1000 + " sold)");
        Check(value == 700L * 100 + 600L * 300, "rota goods: the fees of all 700 customers come in, and the goods of 300 (" + value + ")");
        Check(w.Lost[0] == 400L * 2 * 300 && w.Short == 1, "rota goods: what the 400 without a bag would have bought is the bags' to answer for (" + w.Lost[0] + ")");
    }

    private static void RotaStockSplit()
    {
        var s = new RotaStock { Products = 1 };
        s.PerCustomer[0] = 1500;
        s.UnitMargin[0] = 1000;
        s.Split(2000, 1800);
        Check(s.Goods == 300 && s.Other == 200 && s.Margin == 2000, "rota goods: a customer's 20.00 is 15.00 of the product counted, 3.00 of other goods and 2.00 of fees");
        s.Split(2000, 1000);
        Check(s.Goods == 0 && s.Other == 500 && s.Margin == 2000, "rota goods: books that say less goods than the product counted give no other goods, and nothing is lost");
    }

    // The example shop with a product that runs out, printed: each plan with its week and its goods.
    private static void RotaGoodsExample()
    {
        foreach (int weekly in new[] { 9000, 200 })
        {
            RotaCase c = RotaSouvenirs();
            RotaStock s = c.Shop.Stock;
            s.Products = 1;
            s.PerCustomer[0] = 1000;
            s.UnitMargin[0] = 1800;
            s.Shelf[0] = 50;
            s.Box[0] = 50;
            s.Boxes = 40;
            s.Weekly[0] = weekly;
            s.Split(c.Shop.Margin, 1800);
            RotaLadderResult l = RotaClimb(c);
            Console.WriteLine("ROTA GOODS EXAMPLE (" + weekly + " a week): recommended rung " + l.Recommended + ", stop " + l.Stop + " (gain " + RotaMoneyText(l.StopGain) + ", needed " + RotaMoneyText(l.StopNeed) + ")");
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                RotaStockWeek w = r.Stock;
                Console.WriteLine("  " + RotaRungText(r));
                Console.WriteLine("    goods: wanted " + w.WantUnits(0) + ", sold " + (w.Sold[0] + 500) / 1000 + ", delivered " + w.Delivered[0] + ", refused " + w.Refused[0] + ", out at hour " + w.Out[0]
                                  + ", lost " + RotaMoneyText(r.Sum.StockLost) + ", lift " + RotaLift.Of(s, w, 0));
            }
        }
    }

    private static void RotaStockLadder()
    {
        // The planner's example shop, its customers leaving 20.86 each: as goods, a product sold one to a customer.
        RotaCase free = RotaSouvenirs();
        RotaLadderResult open = RotaClimb(free);

        RotaCase limited = RotaSouvenirs();
        RotaStock s = limited.Shop.Stock;
        s.Products = 1;
        s.PerCustomer[0] = 1000;
        s.UnitMargin[0] = 1800;
        s.Shelf[0] = 50;
        s.Box[0] = 50;
        s.Boxes = 40;
        s.Weekly[0] = 200;
        s.Split(limited.Shop.Margin, 1800);
        RotaLadderResult l = RotaClimb(limited);
        RotaRung now = l.Rungs[0], unlimited = open.Rungs[0];
        Check(now.Sum.StockShort == 1 && now.Stock.Out[0] >= 0, "rota goods: the example shop with 200 a week runs out as it is (" + now.Sum.Served + " served)");
        Check(now.Sum.Sales < unlimited.Sum.Sales, "rota goods: and its week counts fewer sales than the same week with the shelves full (" + now.Sum.Sales + " against " + unlimited.Sum.Sales + ")");
        long gap = unlimited.Sum.Sales - (now.Sum.Sales + now.Sum.StockLost);
        Check(Math.Abs(gap) <= now.Sum.Served, "rota goods: what is sold and what the goods could not supply add up to the week with the shelves full (" + gap + " apart)");
        bool never = true;
        for (int i = 0; i < l.Count; i++)
        {
            never &= (l.Rungs[i].Stock.Sold[0] + 500) / 1000 <= 2 * 200;   // (the counted week: what the first left, and its own delivery)
        }
        Check(never, "rota goods: no plan of the ladder sells more than its deliveries and what fits on site");
        Check(l.Count <= open.Count, "rota goods: a shop whose goods run out is advised no more hires than with full shelves (" + l.Count + " rungs against " + open.Count + ")");
        Check(open.Count <= 2 || l.Count < open.Count || l.Stop == RotaEnd.Stock || l.Stop == RotaEnd.Most, "rota goods: and where a hire paid with full shelves but no longer does, the ladder says the goods are why (stop " + l.Stop + ")");
        Check(l.Stop != RotaEnd.Stock || l.StopGain < l.StopNeed, "rota goods: it stops at a hire that adds less than a rung must");

        // The same goods give the same ladder every time, in one go and a step at a time.
        RotaCase twice = RotaSouvenirs();
        twice.Shop.Stock.Products = 1;
        twice.Shop.Stock.PerCustomer[0] = 1000;
        twice.Shop.Stock.UnitMargin[0] = 1800;
        twice.Shop.Stock.Shelf[0] = 50;
        twice.Shop.Stock.Box[0] = 50;
        twice.Shop.Stock.Boxes = 40;
        twice.Shop.Stock.Weekly[0] = 200;
        twice.Shop.Stock.Split(twice.Shop.Margin, 1800);
        RotaLadderResult stepped = RotaClimb(twice, RotaMode.Auto, true);
        Check(RotaLadderHash(stepped) == RotaLadderHash(l) && stepped.Rungs[0].Stock.Lost[0] == now.Stock.Lost[0], "rota goods: a ladder with goods made two steps at a time is the one made in one go");

        // Plenty delivered: the ladder is the one with full shelves.
        RotaCase plenty = RotaSouvenirs();
        plenty.Shop.Stock.Products = 1;
        plenty.Shop.Stock.PerCustomer[0] = 1000;
        plenty.Shop.Stock.UnitMargin[0] = 1800;
        plenty.Shop.Stock.Shelf[0] = 500;
        plenty.Shop.Stock.Box[0] = 500;
        plenty.Shop.Stock.Boxes = 40;
        plenty.Shop.Stock.Weekly[0] = 9000;
        plenty.Shop.Stock.Split(plenty.Shop.Margin, 1800);
        RotaLadderResult full = RotaClimb(plenty);
        bool same = full.Count == open.Count;
        for (int i = 0; same && i < full.Count; i++)
        {
            same &= full.Rungs[i].Sum.StockShort == 0 && Math.Abs(full.Rungs[i].Sum.Net - open.Rungs[i].Sum.Net) <= 2 * full.Rungs[i].Sum.Hours + 2;
        }
        Check(same, "rota goods: where the deliveries keep up, the plans are those of full shelves, give or take two cents an hour of rounding");
    }
}
