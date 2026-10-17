using RealLifeOverhaul.Economy;

// The online shops fixed, each reproduced here:
//   1. the catalogue showed and stepped prices without the city's price level, while the site charged with it;
//   2. "sold this week" and the day-by-day table covered eight days, the week's books seven;
//   3. a product taken off the site could never be pruned once a restocked return took its unbooked cost under nothing;
//   4. a packed order no van took was refunded in full, but its goods never went back on the shelves;
//   5. a book closed in the middle of a day (the option off, a site closed, the first test) overwrote that day's history;
//   6. the Guide gave set-up and hosting at the start's prices;
//   7. a closed or lost site's last sales never reached the game's statement;
//   8. after a fault, the takings of orders already picked were never paid in.
internal static partial class Program
{
    private static void OnlineFixes()
    {
        OnlineFixShopPrice();
        OnlineFixWeek();
        OnlineFixMidDay();
        OnlineFixUnbooked();
        OnlineFixRestock();
        OnlineFixSources();
    }

    private static void OnlineFixShopPrice()
    {
        Check(OnlineRules.ShopCents(10f, 1200) == 1200 && OnlineRules.ShopCents(9.99f, 1000) == 999 && OnlineRules.ShopCents(4.5f, 1100) == 495,
            "online fix: the shop's price is at today's level, as the site charges it");
        string game = EconomySource(@"Online\OnlineGame.cs");
        Check(MemberText(game, "public static int ShopPrice(").Contains("OnlineRules.ShopCents(") && MemberText(game, "public static void ReadSite(").Contains("OnlineRules.ShopCents("),
            "online fix: the catalogue page and the site take the shop's price from one rule");
    }

    private static void OnlineFixWeek()
    {
        var shop = new WebShop();
        shop.Lines.Add(new WebLine { Item = "x" });
        var closed = new DayBook();
        var week = new DayBook();
        for (int day = 1; day <= 10; day++)
        {
            OnlineRules.CloseDay(shop, day, closed);
            shop.Lines[0].Units = 1;
            shop.Lines[0].Missed = 1;
            shop.Today.Orders = 1;
        }
        shop.WeekInto(week, 10);
        Check(shop.Lines[0].SoldWeek == 7 && shop.Lines[0].MissedWeek == 7 && week.Orders == 7,
            "online fix: this week's units and the week's books both cover today and the six days before it (" + shop.Lines[0].SoldWeek + " and " + week.Orders + ")");
        Check(MemberText(EconomySource(@"Online\OnlineApp.Shops.cs"), "private void WriteDays(").Contains("for (int back = 6; back >= 0; back--)"),
            "online fix: the day-by-day table lists the same seven days");
    }

    private static void OnlineFixMidDay()
    {
        var w = new OnlineWorld();
        var shop = new WebShop();
        shop.Lines.Add(new WebLine { Item = "x" });
        shop.Today.Clear(12);
        shop.Today.Orders = 3;
        shop.Today.Takings = 1000;
        shop.Today.Delivery = 100;
        shop.Lines[0].Units = 3;
        shop.StarLog[12 % 7] = 3000;   // the rating at the close a week before
        shop.Stars = 2500;
        DayBook part = w.Settle(shop, 12);
        Check(part.Takings == 1000 && part.Delivery == 100 && w.Cash == 900 && shop.Today.Day == 12 && shop.Today.Orders == 3 && shop.Today.Takings == 1000 && shop.Lines[0].Units == 3
              && shop.StarLog[12 % 7] == 3000 && shop.StarsBefore(12) == 3000,
            "online fix: a stop in the middle of a day pays its money in and keeps the day's figures and last week's rating");
        var state = new OnlineState { Seed = 1, FirstDay = 1 };
        state.Shops.Add(shop);
        shop.Place = "A~1";
        OnlineState read = OnlineStore.Read(OnlineStore.Write(state), out _);
        Check(read != null && read.Shops.Count == 1 && read.Shops[0].Today.PaidTakings == 1000 && read.Shops[0].Today.PaidDelivery == 100,
            "online fix: what a stop paid in is saved with the day");
        shop.Today.Orders = 4;
        shop.Today.Takings = 1500;
        shop.Lines[0].Units = 4;
        DayBook night = w.Settle(shop, 13);
        Check(night.Takings == 500 && night.Delivery == 0 && w.Cash == 1400 && shop.Week[12 % 7].Day == 12 && shop.Week[12 % 7].Orders == 4 && shop.Week[12 % 7].Takings == 1500
              && shop.Lines[0].WeekUnits[12 % 7] == 4 && shop.StarLog[12 % 7] == 2500,
            "online fix: at midnight only the rest is paid, and the whole day goes into the week's history");
        DayBook twice = w.Settle(shop, 13);
        Check(twice.Takings == 0 && w.Cash == 1400, "online fix: a second stop the same day pays nothing twice");
    }

    private static void OnlineFixUnbooked()
    {
        var shop = new WebShop();
        var line = new WebLine { Item = "x", On = false, BookCost = -500 };
        shop.Lines.Add(line);
        Check(OnlineRules.Unbooked(shop, out long takings, out long cost) && takings == 0 && cost == -500 && OnlineRules.Prune(shop) == 0,
            "online fix: goods back on the shelves leave a cost under nothing, entered in the statement like any other");
        line.BookCost = 0;   // as the statement takes it
        Check(!OnlineRules.Unbooked(shop, out _, out _) && OnlineRules.Prune(shop) == 1 && shop.Lines.Count == 0, "online fix: once entered, a product taken off is pruned");
        string book = MemberText(EconomySource(@"Online\OnlineGame.cs"), "public static bool Book(");
        Check(book.Contains("OnlineRules.Unbooked(shop, out long takings, out long cost)") && book.Contains("if (line.BookCost != 0)") && !book.Contains("Math.Max(0, shop.Lines[l].BookCost)"),
            "online fix: the statement takes a cost under nothing and clears it whatever its sign");
    }

    private static void OnlineFixRestock()
    {
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "R~1", 1, OnlineCatalogue.OwnVans, 1, 1, 2000, 2000, 60, 2000, out site, 500);
        var count = new HourCount();
        OnlineOrder(w, shop, 1, 50, 60, 0, 3, 2000);
        OnlineRules.Pick(shop, site, 51, 1, w.Take, ref count);
        WebLine line = shop.Lines[0];
        int goods = shop.Today.Goods, stock = w.Stock[line.Item], bookCost = line.BookCost, refunds = shop.Today.Refunds;
        OnlineRules.Deliver(w.State, shop, site, 60 + OnlineCatalogue.MissingHours + 1, ref count, w.Put);
        Check(shop.Today.Missing == 1 && shop.Today.Refunds == refunds + 6000 && w.Stock[line.Item] == stock + 3 && shop.Today.Goods == goods - 3 * 900 && line.BookCost == bookCost - 3 * 900,
            "online fix: a packed order no van took is refunded and its goods go back on the shelves, off the day's goods");
        Check(EconomySource(@"Online\OnlineRules.cs").Contains("Deliver(s, shop, site, hour, ref count, put);"), "online fix: every hour hands the shelves to the deliveries");
        var full = new OnlineWorld { NoRoom = true };
        WebShop crowded = OnlineShop(full, "R~2", 1, OnlineCatalogue.OwnVans, 1, 1, 2000, 2000, 60, 2000, out Site s2, 500);
        OnlineOrder(full, crowded, 1, 50, 60, 0, 2, 2000);
        OnlineRules.Pick(crowded, s2, 51, 1, full.Take, ref count);
        int before = crowded.Today.Goods;
        OnlineRules.Deliver(full.State, crowded, s2, 60 + OnlineCatalogue.MissingHours + 1, ref count, full.Put);
        Check(crowded.Today.Goods == before && crowded.Today.Missing == 1, "online fix: with no room on the shelves the goods stay written off");
    }

    private static void OnlineFixSources()
    {
        string guide = EconomySource(@"Online\OnlineGuide.cs");
        Check(guide.Contains("Guide.Money(OnlineRules.SetUpCost(q, false, Prices.Level))") && guide.Contains("Guide.Money(OnlineRules.HostingCost(q, Prices.Level))"),
            "online fix: the Guide gives set-up and hosting at today's prices");
        string close = MemberText(EconomySource(@"Online\OnlineDirector.Actions.cs"), "public void CloseShop("), places = MemberText(EconomySource(@"Online\OnlineDirector.cs"), "private int ReadPlaces(");
        int booked = close.IndexOf("OnlineGame.Book(place, Save.Day - 1, out _, out _, true)", System.StringComparison.Ordinal);
        Check(booked > 0 && booked < close.IndexOf("_state.Shops.Remove(place.Shop)", System.StringComparison.Ordinal)
              && places.IndexOf("OnlineGame.Book(place, Save.Day - 1, out _, out _, true)", System.StringComparison.Ordinal) > 0
              && places.IndexOf("OnlineGame.Book(place, Save.Day - 1, out _, out _, true)", System.StringComparison.Ordinal) < places.IndexOf("_state.Shops.Remove(place.Shop)", System.StringComparison.Ordinal),
            "online fix: a site closed or lost enters its last sales in the latest statement before it goes");
        string director = EconomySource(@"Online\OnlineDirector.cs");
        string fault = MemberText(director, "private void Fault("), pay = MemberText(director, "private void PayInAfterFault(");
        Check(fault.IndexOf("PayInAfterFault();", System.StringComparison.Ordinal) > 0 && fault.IndexOf("PayInAfterFault();", System.StringComparison.Ordinal) < fault.IndexOf("StandDown(", System.StringComparison.Ordinal)
              && pay.Contains("CloseBook(_places[i], Save.Day)") && pay.Contains("catch (Exception e)"),
            "online fix: after a fault the takings of orders already picked are paid in, each site on its own");
    }
}
