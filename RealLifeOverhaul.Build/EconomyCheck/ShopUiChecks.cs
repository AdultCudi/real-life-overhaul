using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// The Web shop beyond the old app (run with --ventures-ui): the delivery fee and free-over amount, the area served, packing times and the badge, night orders held until the
// morning, prices that follow the lowest rival, reviews counted, thirty days of sales, finished orders kept, their save, and a year of days through the rules the director calls.
internal static partial class Program
{
    private static void ShopExtraChecks()
    {
        var s = new ShopExtra { Place = "88 Broadway", FeeCents = 450, FreeOverCents = 5000 };
        Check(OnlineExtraRules.FeeFor(s, 2000) == 450 && OnlineExtraRules.FeeFor(s, 5000) == 0 && OnlineExtraRules.FeeFor(new ShopExtra(), 100) == 0 && OnlineExtraRules.FeeFor(new ShopExtra { FeeCents = 300 }, 99999) == 300,
            "web shop: the customer pays the delivery fee under the free-over amount and nothing at or over it");
        s.DistrictMask = 0b101;
        Check(s.Serves(0) && !s.Serves(1) && s.Serves(2) && new ShopExtra().Serves(7), "web shop: the area is the districts set, all of them when none is set");
        Check(!OnlineExtraRules.Takes(s, 1, 12) && OnlineExtraRules.Takes(s, 0, 12), "web shop: an order from a district not served is turned away");
        s.DistrictMask = 0;
        Check(!OnlineExtraRules.Takes(s, 0, 23) && OnlineExtraRules.Takes(s, 0, 9), "web shop: a night order is not taken unless night orders are on");
        s.Night = true;
        int placed = 24 * 10 + 23;
        Check(OnlineExtraRules.Takes(s, 0, 23) && OnlineExtraRules.PickFrom(s, placed) == 24 * 11 + 6 && OnlineExtraRules.PickFrom(s, 24 * 10 + 3) == 24 * 10 + 6 && OnlineExtraRules.PickFrom(s, 24 * 10 + 12) == 24 * 10 + 12,
            "web shop: a night order is held until 06:00 of the morning");
        Check(OnlineExtraRules.PackingMinutes(0) == 30 && OnlineExtraRules.PackingMinutes(1) == 60 && OnlineExtraRules.PackingMinutes(2) == 1440, "web shop: packing is told as 30 minutes, an hour or the day");
        s.Packing = ShopExtra.PackingDay;
        bool slow = OnlineExtraRules.Express(s, 4);
        s.Packing = ShopExtra.Packing60;
        Check(!slow && OnlineExtraRules.Express(s, 4) && !OnlineExtraRules.Express(new ShopExtra { Packing = 0, DistrictMask = 0 }, 0), "web shop: the Express badge needs a packing time under a day and an area served");

        // Rival prices: the same for the same save, a few either side of the going price, the lowest of them followed.
        int a = OnlineExtraRules.RivalPrice(9, 0, "Sourdough loaf", 620), b = OnlineExtraRules.RivalPrice(9, 0, "Sourdough loaf", 620);
        int low = OnlineExtraRules.LowestRival(9, "Sourdough loaf", 620);
        Check(a == b && a >= 620 * 85 / 100 && a <= 620 * 115 / 100 + 1 && low <= a && low >= 620 * 85 / 100, "web shop: a rival shop prices a product within 15 per cent of the going price, the same every time (" + low + ")");
        var match = new ShopExtra { Match = true };
        Check(OnlineExtraRules.Price(match, 9, "Sourdough loaf", 700, 650, 300) == 700 && OnlineExtraRules.Price(match, 9, "Sourdough loaf", 0, 650, 300) == Math.Max(300, OnlineExtraRules.LowestRival(9, "Sourdough loaf", 650))
              && OnlineExtraRules.Price(new ShopExtra(), 9, "Sourdough loaf", 0, 650, 300) == 650, "web shop: a price of its own stands, matching follows the lowest rival only where there is none, and never under cost");

        // Delivered orders: kept in a bounded ring, some reviewed, the fee asked of the customer.
        var d = new ShopExtra { FeeCents = 450, FreeOverCents = 5000 };
        int fees = 0;
        for (int i = 1; i <= 200; i++)
        {
            fees += OnlineExtraRules.Delivered(d, 77, i, 380 + i / 10, i % 4, 2, i % 2 == 0 ? 6000 : 1500, i % 7 == 0, "Sourdough loaf", i % 11 == 0, 1 + i);
        }
        Check(d.Done.Count == ShopExtra.DoneMost && d.Done[d.Done.Count - 1].Day == 380 + 20 && fees == 450 * 100, "web shop: finished orders are kept, the last " + ShopExtra.DoneMost + ", and the fee is asked of each under the free-over amount");
        Check(d.Reviews > 40 && d.Reviews < 110, "web shop: about a third of delivered orders are rated and counted as reviews (" + d.Reviews + " of 200)");

        // Reviews: kept per product, late orders rated low, good ones high, the breakdown and the average read from them, an answer kept.
        var counts = new int[5];
        int rated = OnlineExtraRules.Breakdown(d, "Sourdough loaf", counts);
        Check(rated == d.Notes.Count && rated > 0 && counts.Sum() == rated && OnlineExtraRules.Halves(counts) >= 6 && OnlineExtraRules.Breakdown(d, "Croissant", counts) == 0 && OnlineExtraRules.Halves(counts) == -1,
            "web shop: reviews are kept for each product, the stars counted one to five and averaged in halves (" + rated + " kept)");
        bool lateLow = d.Notes.Where(n => n.Day >= 0).All(n => n.Stars >= 1 && n.Stars <= 5);
        Check(lateLow && d.Notes.Count <= ShopExtra.NotesMost, "web shop: a review has one to five stars and the ring keeps the last " + ShopExtra.NotesMost);
        var rr = new int[5];
        int rh;
        int rc = OnlineExtraRules.RivalReviews(9, 1, "Sourdough loaf", rr, out rh);
        var rr2 = new int[5];
        int rh2;
        OnlineExtraRules.RivalReviews(9, 1, "Sourdough loaf", rr2, out rh2);
        Check(rr.Sum() == rc && rc >= 20 && rh >= 7 && rh <= 10 && rr.SequenceEqual(rr2) && rh == rh2, "web shop: a rival reviews are a count and an average of its own by the same rule, the same every time (" + rc + ")");

        // Was-price, only a few left, the promise as a day and an hour, night orders held.
        var priced = new ShopExtra();
        OnlineExtraRules.PriceChanged(priced, "Croissant", 400, 360);
        int was = priced.LineOf("Croissant", false).WasCents;
        OnlineExtraRules.PriceChanged(priced, "Croissant", 360, 340);
        int still = priced.LineOf("Croissant", false).WasCents;
        OnlineExtraRules.PriceChanged(priced, "Croissant", 340, 420);
        Check(was == 400 && still == 400 && priced.LineOf("Croissant", false).WasCents == 0, "web shop: a lowered price keeps the price it was and a raise above it clears it");
        Check(OnlineExtraRules.OnlyLeft(5) && !OnlineExtraRules.OnlyLeft(6) && !OnlineExtraRules.OnlyLeft(0), "web shop: only a few left shows at five or fewer in stock");
        int days, hr;
        OnlineExtraRules.ArrivesBy(24 * 11 + 10, 24 * 10 + 15, out days, out hr);
        Check(days == 1 && hr == 10, "web shop: the promise is read as tomorrow by the hour");
        var night = new ShopExtra { Night = true };
        Check(OnlineExtraRules.HoldHours(night, 23) == 7 && OnlineExtraRules.HoldHours(night, 2) == 4 && OnlineExtraRules.HoldHours(night, 12) == 0 && OnlineExtraRules.HoldHours(new ShopExtra(), 23) == 0,
            "web shop: a night order is held until 06:00 and the picking skips it until then");

        // The owner refunds an order now or sends the courier.
        var shopState = new WebShop { Place = "p" };
        var o1 = new WebOrder { Id = 5, Stage = OrderStage.Picked, Total = 900, Count = 1 };
        var o2 = new WebOrder { Id = 6, Stage = OrderStage.Waiting, Count = 1 };
        shopState.Orders.Add(o1);
        shopState.Orders.Add(o2);
        Check(OnlineRules.Refund(shopState, 5) && shopState.Today.Refunds == 900 && shopState.Orders.Count == 1 && !OnlineRules.Refund(shopState, 5), "web shop: refunding an order now gives back what was paid, ends the order and cannot be done twice");
        Check(!OnlineRules.SendNow(shopState, new Site(), 100, 6), "web shop: the courier is not sent for an order that is not packed");

        // Thirty days of sales: a ring that keeps the last thirty days.
        var ring = new ShopExtra();
        for (int day = 1; day <= 100; day++)
        {
            OnlineExtraRules.Sold(ring, day, day * 100);
        }
        var cells = new int[ShopExtra.SalesDays];
        OnlineExtraRules.Ring(ring, 100, cells);
        Check(cells[29] == 10000 && cells[0] == 71 * 100 && cells.All(c => c > 0), "web shop: sales are kept for thirty days, oldest first");
        var early = new ShopExtra();
        OnlineExtraRules.Sold(early, 3, 500);
        OnlineExtraRules.Ring(early, 3, cells);
        Check(cells[29] == 500 && cells.Take(27).All(c => c == 0), "web shop: days before the shop had sales are zero and the page leaves them out");

        // Save.
        var x = new OnlineExtra();
        ShopExtra one = x.Of("88 Broadway", true);
        one.FeeCents = 450;
        one.FreeOverCents = 5000;
        one.Packing = 1;
        one.Night = true;
        one.Match = true;
        one.DistrictMask = 5;
        one.Reviews = 312;
        for (int day = 1; day <= 40; day++)
        {
            OnlineExtraRules.Sold(one, day, 1000 + day);
            OnlineExtraRules.Delivered(one, 5, day, day, 1, 2, 900, day % 5 == 0);
        }
        string line = OnlineExtraStore.Write(x);
        OnlineExtra back = OnlineExtraStore.Read(line);
        Check(back != null && OnlineExtraStore.Write(back) == line && back.Of("88 Broadway", false).Done.Count == 40 && back.Of("88 Broadway", false).Sales[40 % 30] == 1040,
            "web shop save: the extras write and read back to the same line");
        Check(OnlineExtraStore.Load(new Dictionary<string, string>()).Shops.Count == 0 && OnlineExtraStore.Load(new Dictionary<string, string> { { OnlineExtraStore.Key, "junk" } }).Shops.Count == 0,
            "web shop save: a save from before the extras loads empty, and a broken line gives an empty set");

        // A hundred and twenty days through the hooks the director sets: the same save gives the same, and a save on day 30 reads back.
        OnlineExtra run = new OnlineExtra(), again = new OnlineExtra();
        string day30 = null;
        for (int day = 1; day <= 120; day++)
        {
            foreach (OnlineExtra e in new[] { run, again })
            {
                ShopExtra sx = e.Of("p", true);
                sx.FeeCents = 300;
                OnlineExtraRules.Sold(sx, day, 800 + day % 9 * 10);
                OnlineExtraRules.Delivered(sx, 31, day, day, day % 3, 1, 800, false);
            }
            if (day == 30)
            {
                day30 = OnlineExtraStore.Write(run);
            }
        }
        OnlineExtra restored = OnlineExtraStore.Read(day30);
        Check(OnlineExtraStore.Write(run) == OnlineExtraStore.Write(again) && restored.Of("p", false).Reviews <= run.Of("p", false).Reviews
              && restored.Of("p", false).Done.Count == 30, "web shop run: a hundred and twenty days give the same extras twice, and a save on day 30 reads back");
    }

    private static void ShopEntryPoints()
    {
        string root = RepoRoot();
        string dir = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Economy", "Online");
        string director = File.ReadAllText(System.IO.Path.Combine(dir, "OnlineDirector.cs"));
        string rules = File.ReadAllText(System.IO.Path.Combine(dir, "OnlineRules.cs")) + File.ReadAllText(System.IO.Path.Combine(dir, "OnlineRules.Delivery.cs"));
        Check(director.Contains("RealLifeOverhaul.Ui.ShopLive.Want(tab, pick)") && director.Contains("Kit = () => new RealLifeOverhaul.Ui.VentScreen(") && director.Contains("UiShell.Face(_app, def)"),
            "web shop entry points: the phone tile opens the kit page, and the tab and site of a notice reach it through Want");
        Check(rules.Contains("gate(shop, o.District, hour)") && rules.Contains("price(shop, o.Line[c], o.Unit[c])") && rules.Contains("ArrivedHook(shop, o, late)"),
            "web shop entry points: the area, the night rule, the rival price and the delivery fee are asked as orders come and arrive");
        Check(UiScreens.All.Any(sc => sc.Id == "webshop" && sc.Section == UiScreenSwitches.Ventures), "web shop entry points: the app has its own switch in Mod settings");
        string scripts = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts");
        string calendar = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "Wiring.CalendarLinks.cs")) + File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "Wiring.CalendarMoney.cs"));
        string ventLinks = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "VenturesLinks.cs"));
        Check(calendar.Contains("VenturesLinks.CalendarApp(") && ventLinks.Contains("\"offer:\"") && ventLinks.Contains("\"deal:\"") && ventLinks.Contains("\"pledge:\"")
              && calendar.Contains("OnlineDirector.TabShops + \":shop:\""), "calendar links: an investors entry opens the offer, promise or deal it is about, and a hosting entry the site it is about");
        string ventDir = File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "Ventures", "VenturesDirector.cs")) + File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "Brands", "BrandsDirector.cs")) + director;
        Check(CountOfText(ventDir, "PhoneApp.Wants[AppId]") == 3, "calendar links: Ventures, Brands and the Web shop each take a tab and row from a calendar entry (PhoneApp.Wants)");
        string day = File.ReadAllText(System.IO.Path.Combine(dir, "OnlineDirector.Day.cs"));
        Check(day.Contains("TabOrders, \"shop:\" + place.Shop.Place") && rules.Contains("hour < o.PickAt") && rules.Contains("HoldHook(shop, hour)"), "web shop entry points: the notice of waiting orders opens the Orders of that site, and a night order is skipped by picking until its hour");
        string live = File.ReadAllText(System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Ventures", "ShopLive.cs"));
        Check(!live.Contains("Application.Quit") && !live.Contains("AppShell"), "web shop entry points: nothing on the kit pages quits the game or opens the old window");
    }

    private static int CountOfText(string text, string what)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(what, at, StringComparison.Ordinal)) >= 0)
        {
            n++;
            at += what.Length;
        }
        return n;
    }
}
