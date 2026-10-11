using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// The online shops' checks (Scripts/Economy/Online): demand and what moves it, the orders of an hour and that the same
// seed gives the same ones, picking from stock that can never go under nothing, the staff's capacity, the rounds of the
// vans and the courier, lateness, refunds and the rating with its bounds, returns, the day's book against what is paid
// in, what a site adds and the bound on it, the save's line, and the time forty sites take over a day of hours. Every
// figure expected here is worked out by hand from the catalogue.
internal static partial class Program
{
    private const int OnlineDistricts = 6;

    /// <summary>A building's stock and a wallet, standing in for the game.</summary>
    private sealed class OnlineWorld
    {
        public readonly OnlineState State = new OnlineState { Seed = 20260411, FirstDay = 1 };
        public readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
        public readonly Dictionary<string, int> UnitCost = new Dictionary<string, int>();
        public long Cash, TakenUnits, TakenCost, PutUnits;
        public bool WentUnder, NoRoom;
        public StockTake Take;
        public StockBack Put;

        public OnlineWorld()
        {
            Take = TakeIt;
            Put = PutIt;
        }

        private int TakeIt(string item, int wanted, out int cost)
        {
            int have;
            Stock.TryGetValue(item, out have);
            int got = Math.Min(Math.Max(0, wanted), Math.Max(0, have));
            Stock[item] = have - got;
            WentUnder |= Stock[item] < 0;
            int each;
            UnitCost.TryGetValue(item, out each);
            cost = got * each;
            TakenUnits += got;
            TakenCost += cost;
            return got;
        }

        private int PutIt(string item, int units, int unitCost)
        {
            if (NoRoom)
            {
                return 0;
            }
            int have;
            Stock.TryGetValue(item, out have);
            Stock[item] = have + units;
            PutUnits += units;
            return units;
        }

        /// <summary>Closes a site's day and pays its book in, as the director does.</summary>
        public DayBook Settle(WebShop shop, int day)
        {
            var closed = new DayBook();
            OnlineRules.CloseDay(shop, day, closed);
            Cash += closed.Takings - closed.Refunds - closed.Delivery;
            return closed;
        }
    }

    // A site with a number of products at a price against a going price, every district wanting them the same, each district as far.
    private static WebShop OnlineShop(OnlineWorld w, string place, int quality, int way, int promise, int lines, int ask, int going, int demand, int metres, out Site site, int stock = 1000000)
    {
        WebShop shop = OnlineRules.Open(w.State, place, place + " online", quality, way, promise, false, 1, 30);
        site = new Site();
        site.Size(lines, OnlineDistricts);
        for (int l = 0; l < lines; l++)
        {
            string item = place + ":item" + l;
            OnlineRules.Offer(shop, item);
            w.Stock[item] = stock;
            w.UnitCost[item] = going * 45 / 100;
            site.Ask[l] = ask;
            site.Going[l] = going;
            for (int d = 0; d < OnlineDistricts; d++)
            {
                site.Demand[l * OnlineDistricts + d] = demand;
            }
        }
        for (int d = 0; d < OnlineDistricts; d++)
        {
            site.Metres[d] = metres;
        }
        while (w.State.Districts.Count < OnlineDistricts)
        {
            w.State.Districts.Add("district" + w.State.Districts.Count);
        }
        OnlineRules.Plan(shop, site, 1, 0, 1, 1, w.State.Shops.Count);
        return shop;
    }

    // Hours from..to (counted from day 0) at one site with so many pickers, the day's book closed and paid in at each midnight.
    private static void OnlineRun(OnlineWorld w, WebShop shop, Site site, int from, int to, int pickers, DayBook total = null)
    {
        for (int hour = from; hour <= to; hour++)
        {
            if (hour % 24 == 0 && shop.Today.Day < hour / 24)
            {
                DayBook closed = w.Settle(shop, hour / 24);
                if (total != null)
                {
                    total.Add(closed);
                }
                OnlineRules.Plan(shop, site, hour / 24, 0, site.Cycle, site.Season, w.State.Shops.Count);
            }
            OnlineRules.Hour(w.State, shop, site, hour, pickers, w.Take, w.Put);
        }
    }

    private static WebOrder OnlineOrder(OnlineWorld w, WebShop shop, int district, int placed, int due, int line, int qty, int unit)
    {
        WebOrder o = OnlineRules.NewOrder();
        o.Id = w.State.NextOrder++;
        o.District = district;
        o.Placed = placed;
        o.Due = due;
        o.Count = 1;
        o.Line[0] = line;
        o.Qty[0] = qty;
        o.Unit[0] = unit;
        o.Tilt = 1000;
        shop.Orders.Add(o);
        shop.Today.Orders++;
        return o;
    }

    private static void Online()
    {
        OnlineCatalogueChecks();
        OnlineDemand();
        OnlinePlan();
        OnlineOrders();
        OnlinePicking();
        OnlineRounds();
        OnlineLateAndRating();
        OnlineReturns();
        OnlineDayAndMoney();
        OnlineLinesAndCosts();
        OnlineSaveAndLoad();
        OnlineBalance();
        OnlineTiming();
    }

    private static void OnlineCatalogueChecks()
    {
        int sum = 0;
        for (int h = 0; h < OnlineCatalogue.HourShare.Length; h++)
        {
            sum += OnlineCatalogue.HourShare[h];
        }
        Check(OnlineCatalogue.HourShare.Length == 24 && sum == 1000, "online: the hours' shares of a day's orders are twenty-four and add up to 1000");
        Check(OnlineCatalogue.SetUp.Length == 3 && OnlineCatalogue.Hosting.Length == 3 && OnlineCatalogue.OrdersADay.Length == 3 && OnlineCatalogue.OrdersMost.Length == 3, "online: three qualities of site, each with every figure");
        bool better = true, bounded = true;
        for (int q = 0; q < 3; q++)
        {
            better &= q == 0 || (OnlineCatalogue.SetUp[q] > OnlineCatalogue.SetUp[q - 1] && OnlineCatalogue.Hosting[q] > OnlineCatalogue.Hosting[q - 1] && OnlineCatalogue.OrdersADay[q] > OnlineCatalogue.OrdersADay[q - 1]
                                && OnlineCatalogue.OrdersMost[q] > OnlineCatalogue.OrdersMost[q - 1]);
            bounded &= OnlineCatalogue.OrdersMost[q] * 2 == OnlineCatalogue.OrdersADay[q] * 5;
        }
        Check(better, "online: a dearer site costs more to host, takes more orders and has a higher bound");
        Check(bounded, "online: the bound on a day's orders is two and a half times an ordinary day's, at every quality");
        Check(OnlineCatalogue.ReturnShare.Length == 6 && OnlineCatalogue.ResaleShare.Length == 6 && OnlineCatalogue.ResaleShare[0] == 0 && OnlineCatalogue.ResaleShare[1] == 0, "online: six kinds of goods, and returned food is never sold again");
        Check(OnlineCatalogue.PromiseFactor[0] > OnlineCatalogue.PromiseFactor[1] && OnlineCatalogue.PromiseFactor[1] == 1000, "online: a same-day promise brings more orders than a next-day one");
        Check(OnlineCatalogue.CycleFactor[0] > OnlineCatalogue.CycleFactor[1] && OnlineCatalogue.CycleFactor[1] == 1000 && OnlineCatalogue.CycleFactor[2] > OnlineCatalogue.CycleFactor[3], "online: a boom brings more orders than steady times, a recession fewest");
        Check(OnlineCatalogue.DriverMinutes == 342, "online: a driver's day is the 5.7 hours the game pays a delivery driver for");
        Check(OnlineCatalogue.ScoreGood > OnlineCatalogue.ScoreShort && OnlineCatalogue.ScoreShort > OnlineCatalogue.ScoreLate && OnlineCatalogue.ScoreLate > OnlineCatalogue.ScoreLost, "online: on time marks above short, short above late, late above lost");
        Check(OnlineCatalogue.Quality(-3) == 0 && OnlineCatalogue.Quality(9) == 2 && OnlineCatalogue.Quality(1) == 1, "online: a quality out of range is the nearest there is");
    }

    private static int OnlineRef(int quality = 1, bool inHouse = false, int lines = 8, int need = 60, int price = 1000, int promise = 1, int stars = 4000, int ads = 0, int campaign = 0, int cycle = 1, int season = 1, int shops = 1)
    {
        return OnlineRules.PerDay(quality, inHouse, lines, need, price, promise, stars, ads, campaign, cycle, season, shops);
    }

    private static void OnlineDemand()
    {
        Check(OnlineRules.PriceFactor(1000) == 1000, "online demand: at the going price the price factor is 1000");
        Check(OnlineRules.PriceFactor(1100) == 826 && OnlineRules.PriceFactor(900) == 1234 && OnlineRules.PriceFactor(800) == 1562, "online demand: ten per cent dearer sells 82.6%, ten per cent cheaper 123.4%, twenty per cent cheaper 156.2% (the inverse square)");
        bool falls = true;
        for (int r = 1; r <= 2000; r++)
        {
            falls &= OnlineRules.PriceFactor(r) <= OnlineRules.PriceFactor(r - 1) || r == 1;
        }
        Check(falls, "online demand: a higher price never sells more, at any ratio from nothing to twice the going price");
        Check(OnlineRules.PriceFactor(1599) > 0 && OnlineRules.PriceFactor(1600) == 0 && OnlineRules.PriceFactor(5000) == 0, "online demand: from sixty per cent over the going price nobody orders");
        Check(OnlineRules.PriceFactor(700) == 2000 && OnlineRules.PriceFactor(100) == 2000 && OnlineRules.PriceFactor(0) == 0, "online demand: however cheap, a product sells no more than double; a price of nothing is no price");
        Check(OnlineRef() == 16000 && OnlineRef(0) == 8000 && OnlineRef(2) == 24000, "online demand: at the reference conditions a site takes its quality's 8, 16 or 24 orders a day");
        Check(OnlineRef(0) < OnlineRef(1) && OnlineRef(1) < OnlineRef(2), "online demand: a better site never takes fewer orders");
        bool wider = true, wants = true, cheaper = true, rated = true, pushed = true;
        for (int l = 1; l <= 12; l++)
        {
            wider &= OnlineRef(lines: l) >= OnlineRef(lines: l - 1);
        }
        Check(wider && OnlineRef(lines: 0) == 0 && OnlineRef(lines: 1) == 6400 && OnlineRef(lines: 8) == OnlineRef(lines: 12), "online demand: a wider catalogue never takes fewer orders; one product takes four tenths, and from eight it is at its full");
        for (int n = 1; n <= 100; n++)
        {
            wants &= OnlineRef(need: n) >= OnlineRef(need: n - 1);
        }
        Check(wants && OnlineRef(need: 0) == 8000 && OnlineRef(need: 100) == 24000, "online demand: more of the game's own demand never brings fewer orders; it moves them between half and one and a half");
        for (int r = 2; r <= 2000; r++)
        {
            cheaper &= OnlineRef(price: OnlineRules.PriceFactor(r)) <= OnlineRef(price: OnlineRules.PriceFactor(r - 1));
        }
        Check(cheaper, "online demand: orders never rise with the price");
        for (int s = 1001; s <= 5000; s += 7)
        {
            rated &= OnlineRef(stars: s) >= OnlineRef(stars: s - 7);
        }
        Check(rated && OnlineRules.StarsFactor(1000) == 400 && OnlineRules.StarsFactor(4000) == 1000 && OnlineRules.StarsFactor(5000) == 1200, "online demand: a better rating never brings fewer orders: 40% at one star, all at four, 120% at five");
        for (int a = 1; a <= 40; a++)
        {
            pushed &= OnlineRef(ads: a) >= OnlineRef(ads: a - 1) && OnlineRef(campaign: a) >= OnlineRef(campaign: a - 1);
        }
        Check(pushed && OnlineRules.MarketingFactor(20, 0) == 1200 && OnlineRules.MarketingFactor(0, 12) == 1060 && OnlineRules.MarketingFactor(99, 99) == 1360,
            "online demand: more marketing never brings fewer orders: 20% of advertising is 20% more, a 12% campaign at the business 6% more, and both stop at their most");
        Check(OnlineRef(promise: 0) == 20000 && OnlineRef(inHouse: true) == 17600, "online demand: a same-day promise brings a quarter more orders, a site built in-house a tenth more");
        Check(OnlineRef(cycle: 0) > OnlineRef(cycle: 1) && OnlineRef(cycle: 2) < OnlineRef(cycle: 1) && OnlineRef(cycle: 3) < OnlineRef(cycle: 2) && OnlineRef(season: 0) > OnlineRef(season: 3)
              && OnlineRef(season: 3) > OnlineRef(season: 1) && OnlineRef(season: 2) < OnlineRef(season: 1), "online demand: the economy and the season move orders as the tables say (most in a boom and in winter)");
        bool shared = true, grows = true;
        for (int n = 2; n <= OnlineCatalogue.ShopsMost; n++)
        {
            shared &= OnlineRules.Crowd(n) < OnlineRules.Crowd(n - 1);
            grows &= (long)n * OnlineRules.Crowd(n) > (long)(n - 1) * OnlineRules.Crowd(n - 1);
        }
        Check(shared && grows && OnlineRules.Crowd(1) == 1000 && OnlineRules.Crowd(4) == 571 && OnlineCatalogue.ShopsMost * OnlineRules.Crowd(OnlineCatalogue.ShopsMost) < 3300,
            "online demand: each further site leaves every site fewer orders, all of them together still take more, four take 2.3 times what one does and twelve no more than 3.3 times");
        bool capped = true;
        for (int q = 0; q < 3; q++)
        {
            capped &= OnlineRules.PerDay(q, true, 12, 100, 2000, 0, 5000, 30, 12, 0, 0, 1) == OnlineCatalogue.OrdersMost[q] * 1000;
        }
        Check(capped, "online demand: with everything at its best together a site takes exactly its bound and no more");
    }

    private static void OnlinePlan()
    {
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", 1, OnlineCatalogue.Courier, 1, 8, 2500, 2500, 60, 2000, out site);
        Check(site.PerDay == 16000 && site.Need == 60 && site.Price == 1000, "online plan: eight products at the going price where the game's demand is 60 make the reference site of 16 orders a day");
        shop.Offline = true;
        OnlineRules.Plan(shop, site, 1, 0, 1, 1, 1);
        Check(site.PerDay == 0 && OnlineRules.Arrive(w.State, shop, site, 30) == 0, "online plan: a site whose hosting is unpaid takes no orders");
        shop.Offline = false;
        shop.Lines[7].On = false;
        OnlineRules.Plan(shop, site, 1, 0, 1, 1, 1);
        Check(site.Factor[7] == 0 && site.PerDay == OnlineRef(lines: 7), "online plan: a product taken off the site sells nothing and no longer widens the catalogue");
        shop.Lines[7].On = true;
        site.Ask[0] = 4000;   // sixty per cent over the going price
        for (int l = 0; l < 8; l++)
        {
            site.Demand[l * OnlineDistricts + 2] = 100;   // one district wants twice... (set below)
            site.Demand[l * OnlineDistricts + 3] = 50;
        }
        OnlineRules.Plan(shop, site, 1, 0, 1, 1, 1);
        Check(site.Factor[0] == 0 && site.Weight[2] == 2 * site.Weight[3] && site.Weight[2] == 700, "online plan: a district's share of the orders is what it wants of the products that sell: twice the demand, twice the share");
        bool never = true;
        for (int hour = 24; hour < 24 * 40; hour++)
        {
            for (int k = 0; k < 3; k++)
            {
                WebOrder o = OnlineRules.Draw(w.State, shop, site, hour, OnlineRules.Key(shop.Place), k);
                for (int j = 0; o != null && j < o.Count; j++)
                {
                    never &= o.Line[j] != 0;
                }
                OnlineRules.Recycle(o);
            }
        }
        Check(never, "online plan: a product priced out of the market is in no order, in forty days of them");
        for (int l = 0; l < 8; l++)
        {
            shop.Lines[l].On = false;
        }
        OnlineRules.Plan(shop, site, 1, 0, 1, 1, 1);
        Check(site.PerDay == 0 && OnlineRules.Draw(w.State, shop, site, 30, 1, 0) == null, "online plan: with nothing on the site there are no orders and none can be drawn");
    }

    private static void OnlineOrders()
    {
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", 1, OnlineCatalogue.Courier, 1, 8, 2500, 2500, 60, 2000, out site);
        int key = OnlineRules.Key(shop.Place);
        long came = 0;
        const int Days = 400;
        for (int hour = 24; hour < 24 * (Days + 1); hour++)
        {
            came += OnlineRules.Expected(w.State.Seed, key, site.PerDay, hour);
        }
        Check(Math.Abs(came - 16L * Days) < 16L * Days * 3 / 100, "online orders: over 400 days the hours' draws bring 16 orders a day to within three per cent (" + (came / (double)Days).ToString("0.00") + ")");
        int evening = 0, night = 0;
        for (int day = 1; day <= Days; day++)
        {
            evening += OnlineRules.Expected(w.State.Seed, key, site.PerDay, day * 24 + 20);
            night += OnlineRules.Expected(w.State.Seed, key, site.PerDay, day * 24 + 3);
        }
        Check(evening > night * 20, "online orders: the evening brings many times the orders of the small hours (" + evening + " at 20:00 against " + night + " at 03:00 in 400 days)");

        // The same seed gives the same history; another seed another.
        string a = OnlineHistory(77, 10), b = OnlineHistory(77, 10), c = OnlineHistory(78, 10);
        Check(a == b, "online orders: the same seed and the same days give the same orders, books and rating, to the last figure");
        Check(a != c, "online orders: another seed gives other orders");

        bool shaped = true, cheapMany = false, dearOne = true;
        int one = 0, two = 0, three = 0, all = 0;
        site.Ask[0] = 500;      // a cheap product: up to three of it
        site.Going[0] = 500;
        site.Ask[1] = 9000;     // a dear one: one of it
        site.Going[1] = 9000;
        OnlineRules.Plan(shop, site, 1, 0, 1, 1, 1);
        for (int hour = 24; hour < 24 * 200; hour++)
        {
            WebOrder o = OnlineRules.Draw(w.State, shop, site, hour, key, 0);
            all++;
            one += o.Count == 1 ? 1 : 0;
            two += o.Count == 2 ? 1 : 0;
            three += o.Count == 3 ? 1 : 0;
            shaped &= o.Count >= 1 && o.Count <= 3 && o.District >= 0 && o.District < OnlineDistricts && o.Stage == OrderStage.Waiting && o.Placed == hour && o.Due == OnlineRules.Due(1, hour);
            for (int j = 0; j < o.Count; j++)
            {
                shaped &= o.Qty[j] >= 1 && o.Qty[j] <= 3 && o.Unit[j] == site.Ask[o.Line[j]];
                for (int i = 0; i < j; i++)
                {
                    shaped &= o.Line[i] != o.Line[j];
                }
                cheapMany |= o.Line[j] == 0 && o.Qty[j] == 3;
                dearOne &= o.Line[j] != 1 || o.Qty[j] == 1;
            }
            OnlineRules.Recycle(o);
        }
        Check(shaped, "online orders: every order is one to three different products, one to three of each, at the price asked, for a district of the city, promised by the site's promise");
        Check(cheapMany && dearOne, "online orders: a customer takes up to three of a cheap product and one of a dear one");
        Check(Math.Abs(one * 100 / all - 55) <= 4 && Math.Abs(two * 100 / all - 30) <= 4 && Math.Abs(three * 100 / all - 15) <= 4, "online orders: about 55% have one product, 30% two, 15% three (" + one * 100 / all + ", " + two * 100 / all + ", " + three * 100 / all + ")");

        Check(OnlineRules.Due(1, 5 * 24 + 9) == 6 * 24 + 20 && OnlineRules.Due(1, 5 * 24 + 23) == 6 * 24 + 20, "online orders: a next-day order is promised for 20:00 the next day, whenever it was placed");
        Check(OnlineRules.Due(0, 5 * 24 + 9) == 5 * 24 + 15 && OnlineRules.Due(0, 5 * 24 + 14) == 5 * 24 + 20, "online orders: a same-day order before the cut-off is promised six hours on");
        Check(OnlineRules.Due(0, 5 * 24 + 15) == 6 * 24 + 12 && OnlineRules.Due(0, 5 * 24 + 23) == 6 * 24 + 12, "online orders: a same-day order after the cut-off is promised for noon the next day");

        // The bound: whatever the demand, no more than the quality's most in a day; and no more open than the site holds.
        var w2 = new OnlineWorld();
        Site s2;
        WebShop shop2 = OnlineShop(w2, "B~2", 0, OnlineCatalogue.Courier, 1, 8, 2500, 2500, 60, 2000, out s2);
        s2.PerDay = 900000;   // (far beyond anything the rule gives: the count itself must stop it)
        int most = 0;
        for (int day = 1; day <= 5; day++)
        {
            for (int h = 0; h < 24; h++)
            {
                OnlineRules.Arrive(w2.State, shop2, s2, day * 24 + h);
            }
            most = Math.Max(most, shop2.Today.Orders);
            var closed = new DayBook();
            OnlineRules.CloseDay(shop2, day + 1, closed);
        }
        Check(most == OnlineCatalogue.OrdersMost[0], "online orders: with demand far beyond the rule a Basic site still takes exactly its 20 orders in a day and no more");
        Check(shop2.Orders.Count <= OnlineCatalogue.OpenMost && shop2.Orders.Count == OnlineCatalogue.OpenMost, "online orders: a site that nobody picks for holds 80 open orders and takes no more");
        s2.PerDay = 900000;
        int turned = shop2.Today.Turned;
        OnlineRules.Arrive(w2.State, shop2, s2, 6 * 24 + 20);
        Check(shop2.Today.Turned > turned && shop2.Orders.Count == OnlineCatalogue.OpenMost, "online orders: orders that find the site full are turned away and counted");
    }

    // Ten or more days of two sites from a seed, as the save's line: one with a courier, one with vans, stock that runs out, staff by day only.
    private static string OnlineHistory(int seed, int days)
    {
        var w = new OnlineWorld();
        w.State.Seed = seed;
        Site sa, sb;
        WebShop a = OnlineShop(w, "A~1", 1, OnlineCatalogue.Courier, 0, 6, 2400, 2500, 70, 2500, out sa, 60);
        WebShop b = OnlineShop(w, "B~2", 2, OnlineCatalogue.OwnVans, 1, 9, 1800, 2000, 50, 4000, out sb, 90);
        sa.Kind = 2;
        sb.Kind = 3;
        w.State.Vans.Add(new Van { Driver = "d1", Capacity = 8, Ready = true });
        for (int hour = 24; hour < 24 * (days + 1); hour++)
        {
            int h = hour % 24;
            if (h == 0)
            {
                w.Settle(a, hour / 24);
                w.Settle(b, hour / 24);
                OnlineRules.Plan(a, sa, hour / 24, 0, 1, 1, 2);
                OnlineRules.Plan(b, sb, hour / 24, 0, 1, 1, 2);
            }
            int pickers = h >= 9 && h < 18 ? 1 : 0;
            OnlineRules.Hour(w.State, a, sa, hour, pickers, w.Take, w.Put);
            OnlineRules.Hour(w.State, b, sb, hour, pickers, w.Take, w.Put);
        }
        return OnlineStore.Write(w.State) + "#" + w.Cash + "#" + w.TakenUnits;
    }

    private static void OnlinePicking()
    {
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", 1, OnlineCatalogue.Courier, 1, 3, 2000, 2000, 60, 3000, out site, 5);
        var count = new HourCount();
        string item0 = shop.Lines[0].Item, item1 = shop.Lines[1].Item;
        OnlineOrder(w, shop, 1, 30, 60, 0, 3, 2000);
        OnlineRules.Pick(shop, site, 31, 1, w.Take, ref count);
        WebOrder first = shop.Orders[0];
        Check(first.Stage == OrderStage.Out && first.Got[0] == 3 && first.Total == 6000 && !first.Short && w.Stock[item0] == 2 && shop.Today.Takings == 6000 && shop.Today.Goods == 3 * 900 && shop.Today.Picked == 1
              && shop.Today.Units == 3, "online picking: three of five in stock are picked whole: three leave the stock, the order is charged 60.00 and the goods cost what they were bought for");
        Check(first.Arrive == 31 + OnlineRules.CourierTime(3000) && shop.Today.Delivery == OnlineRules.CourierFee(3000) && count.Sent == 1, "online picking: with a courier a picked order is collected at once, due at the door two hours on, and the fee is in the day's book");
        OnlineOrder(w, shop, 1, 31, 60, 0, 3, 2000);
        OnlineRules.Pick(shop, site, 32, 1, w.Take, ref count);
        WebOrder second = shop.Orders[1];
        Check(second.Short && second.Got[0] == 2 && second.Total == 4000 && w.Stock[item0] == 0 && shop.Today.Short == 1 && shop.Lines[0].Missed == 1, "online picking: two left of three asked for: the order goes out short, charged for two, and the one missing is counted against the product");
        int stars = shop.Stars;
        OnlineOrder(w, shop, 1, 32, 60, 0, 1, 2000);
        OnlineRules.Pick(shop, site, 33, 1, w.Take, ref count);
        Check(shop.Orders.Count == 2 && shop.Today.Cancelled == 1 && shop.Today.Takings == 10000 && shop.Stars == stars + (1000 - stars) / 20 && w.Stock[item0] == 0 && !w.WentUnder,
            "online picking: nothing left: the order is cancelled, nothing is charged, the rating takes a one-star mark, and stock never goes under nothing");

        // Capacity: so many pickers pick six orders each in an hour, the oldest first.
        for (int i = 0; i < 20; i++)
        {
            OnlineOrder(w, shop, 2, 34, 80, 1, 1, 2000).Id = 1000 + i;
        }
        w.Stock[item1] = 100;
        count = new HourCount();
        OnlineRules.Pick(shop, site, 35, 2, w.Take, ref count);
        bool oldest = true;
        for (int i = 0; i < shop.Orders.Count; i++)
        {
            WebOrder o = shop.Orders[i];
            oldest &= o.Id < 1000 || (o.Id < 1012) == (o.Stage == OrderStage.Out);
        }
        Check(count.Picked == 12 && shop.Count(OrderStage.Waiting) == 8 && oldest, "online picking: two members of staff pick twelve orders in an hour, the twelve that came first");
        Check(shop.Idle == 0, "online picking: orders left over while somebody picks are no cause for alarm");
        count = new HourCount();
        OnlineRules.Pick(shop, site, 36, 0, w.Take, ref count);
        OnlineRules.Pick(shop, site, 37, 0, w.Take, ref count);
        OnlineRules.Pick(shop, site, 38, 0, w.Take, ref count);
        Check(count.Picked == 0 && shop.Idle == 3 && shop.Count(OrderStage.Waiting) == 8, "online picking: with nobody on shift orders wait, and the hours they wait are counted");
        OnlineRules.Pick(shop, site, 39, 1, w.Take, ref count);
        Check(shop.Idle == 0 && count.Picked == 6, "online picking: when somebody comes on shift six are picked and the count of idle hours starts again");
        int before = shop.Today.Cancelled;
        stars = shop.Stars;
        count = new HourCount();
        OnlineRules.Pick(shop, site, 80 + OnlineCatalogue.WaitMost, 0, w.Take, ref count);
        Check(shop.Count(OrderStage.Waiting) == 2 && count.Cancelled == 0, "online picking: an order is still waited for a day after its promise, to the hour");
        OnlineRules.Pick(shop, site, 80 + OnlineCatalogue.WaitMost + 1, 0, w.Take, ref count);
        Check(shop.Count(OrderStage.Waiting) == 0 && shop.Today.Cancelled == before + 2 && shop.Stars < stars, "online picking: an hour later the customers have given up: the orders are cancelled and the rating falls");

        // With vans a picked order waits for a round; a product no longer in the catalogue is simply not there.
        var w2 = new OnlineWorld();
        Site s2;
        WebShop shop2 = OnlineShop(w2, "B~2", 1, OnlineCatalogue.OwnVans, 1, 2, 2000, 2000, 60, 3000, out s2, 50);
        WebOrder o2 = OnlineOrder(w2, shop2, 0, 30, 60, 0, 2, 2000);
        o2.Count = 2;
        o2.Line[1] = -1;
        o2.Qty[1] = 1;
        o2.Unit[1] = 2000;
        count = new HourCount();
        OnlineRules.Pick(shop2, s2, 31, 1, w2.Take, ref count);
        Check(o2.Stage == OrderStage.Picked && shop2.Today.Delivery == 0 && count.Sent == 0, "online picking: with the player's own vans a picked order is packed and waits for a round");
        Check(o2.Short && o2.Got[1] == 0 && o2.Total == 4000, "online picking: a product that has left the catalogue is left out of the order");
        Check(w.TakenUnits == shop.Today.Units && w2.TakenUnits == shop2.Today.Units, "online picking: the units in the day's book are exactly the units that left the stock");
    }

    private static void OnlineRounds()
    {
        Check(OnlineRules.Travel(2200) == 6 && OnlineRules.Travel(11000) == 30 && OnlineRules.Travel(0) == 1, "online rounds: a van does 22 km in an hour: 2.2 km in six minutes, 11 km in thirty");
        Check(OnlineRules.VanCost(5000) == 600 && OnlineRules.VanCost(0) == 0, "online rounds: a round to 5 km and back costs 6.00 in fuel and wear");
        Check(OnlineRules.CourierFee(0) == 350 && OnlineRules.CourierFee(2000) == 550 && OnlineRules.CourierFee(7500) == 1100, "online rounds: a courier charges 3.50 a parcel and 1.00 a kilometre");
        Check(OnlineRules.CourierTime(0) == 2 && OnlineRules.CourierTime(7999) == 2 && OnlineRules.CourierTime(8000) == 3 && OnlineRules.CourierTime(24000) == 5, "online rounds: a courier is at the door two hours on, and an hour more for each 8 km");

        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", 1, OnlineCatalogue.OwnVans, 1, 2, 2000, 2000, 60, 2200, out site, 500);
        site.Metres[3] = 11000;
        var van = new Van { Driver = "d1", Capacity = 3, Ready = true };
        w.State.Vans.Add(van);
        var count = new HourCount();
        int day = 3, nine = day * 24 + 9;
        // Five packed orders for district 1 and two for district 3; the one promised soonest is for district 3.
        for (int i = 0; i < 5; i++)
        {
            OnlineOrder(w, shop, 1, nine - 5, nine + 40 + i, 0, 1, 2000);
        }
        WebOrder soon = OnlineOrder(w, shop, 3, nine - 4, nine + 10, 0, 1, 2000), other = OnlineOrder(w, shop, 3, nine - 4, nine + 50, 0, 1, 2000);
        OnlineRules.Pick(shop, site, nine - 3, 2, w.Take, ref count);
        Check(shop.Count(OrderStage.Picked) == 7, "online rounds: seven orders packed and waiting");
        count = new HourCount();
        OnlineRules.Rounds(w.State, shop, site, day * 24 + 7, ref count);
        Check(count.Sent == 0 && shop.Count(OrderStage.Out) == 0, "online rounds: no round leaves before eight in the morning");
        OnlineRules.Rounds(w.State, shop, site, nine, ref count);
        Check(count.Sent == 2 && soon.Stage == OrderStage.Out && other.Stage == OrderStage.Out && shop.Count(OrderStage.Picked) == 5, "online rounds: the van takes the order promised soonest and the other one for the same district, and nothing for another district");
        int minutes = 10 + 2 * 30 + 2 * 5;
        Check(van.FreeAt == nine * 60 + minutes && van.Used == minutes, "online rounds: the round takes loading, the way there and back and five minutes a door: 80 minutes");
        Check(soon.Arrive == (nine * 60 + 10 + 30 + 5 + 59) / 60 && other.Arrive == (nine * 60 + 10 + 30 + 10 + 59) / 60, "online rounds: each door is reached after the way there and its turn, to the next full hour");
        Check(shop.Today.Delivery == OnlineRules.VanCost(11000), "online rounds: one round's running cost is in the day's book, once");
        count = new HourCount();
        OnlineRules.Rounds(w.State, shop, site, nine + 1, ref count);
        Check(count.Sent == 0, "online rounds: a van that is out takes nothing until it is back");
        OnlineRules.Rounds(w.State, shop, site, nine + 2, ref count);
        Check(count.Sent == 3 && shop.Count(OrderStage.Picked) == 2, "online rounds: back again, it takes as many for the next district as it holds: three of five");
        bool firstCame = shop.Orders[0].Stage == OrderStage.Out && shop.Orders[1].Stage == OrderStage.Out && shop.Orders[2].Stage == OrderStage.Out;
        Check(firstCame, "online rounds: of equal districts the orders that came first go first");
        var second = new Van { Driver = "d2", Capacity = 8, Ready = true };
        w.State.Vans.Add(second);
        count = new HourCount();
        OnlineRules.Rounds(w.State, shop, site, nine + 2, ref count);
        Check(count.Sent == 2 && second.Used == 10 + 2 * 6 + 2 * 5, "online rounds: a second van takes what the first could not, in the same hour");
        var idle = new Van { Driver = "d3", Capacity = 8, Ready = false };
        w.State.Vans.Add(idle);
        OnlineOrder(w, shop, 1, nine, nine + 60, 0, 1, 2000);
        OnlineRules.Pick(shop, site, nine + 3, 1, w.Take, ref count);
        van.FreeAt = second.FreeAt = (nine + 30) * 60;
        count = new HourCount();
        OnlineRules.Rounds(w.State, shop, site, nine + 3, ref count);
        Check(count.Sent == 0 && idle.Used == 0, "online rounds: a van without its driver or its vehicle this hour takes nothing");
        OnlineRules.Rounds(w.State, shop, site, day * 24 + 20, ref count);
        Check(count.Sent == 0, "online rounds: no round leaves from eight in the evening");

        // The driver's day: never more than 342 minutes, however much is packed.
        var w2 = new OnlineWorld();
        Site s2;
        WebShop shop2 = OnlineShop(w2, "B~2", 2, OnlineCatalogue.OwnVans, 1, 2, 2000, 2000, 60, 4400, out s2, 5000);
        var v2 = new Van { Driver = "d1", Capacity = 8, Ready = true };
        w2.State.Vans.Add(v2);
        int sentInDay = 0, mostUsed = 0;
        for (int hour = 5 * 24; hour < 6 * 24; hour++)
        {
            for (int i = 0; i < 10; i++)
            {
                OnlineOrder(w2, shop2, i % OnlineDistricts, hour, hour + 200, 0, 1, 2000);
            }
            var c2 = new HourCount();
            OnlineRules.Pick(shop2, s2, hour, 4, w2.Take, ref c2);
            OnlineRules.Rounds(w2.State, shop2, s2, hour, ref c2);
            sentInDay += c2.Sent;
            mostUsed = Math.Max(mostUsed, v2.Used);
        }
        Check(mostUsed <= OnlineCatalogue.DriverMinutes && mostUsed > OnlineCatalogue.DriverMinutes - 60, "online rounds: a driver's rounds fill the day and never pass 342 minutes (" + mostUsed + ")");
        Check(sentInDay > 20 && sentInDay <= 45, "online rounds: one van of eight parcels delivers a few dozen parcels in its day to districts 4.4 km off and no more (" + sentInDay + ")");
        s2.Metres[0] = 70000;   // further than a driver's whole day reaches
        v2.Used = 0;
        v2.FreeAt = 0;
        for (int i = shop2.Orders.Count - 1; i >= 0; i--)
        {
            if (shop2.Orders[i].District != 0)
            {
                shop2.Orders.RemoveAt(i);
            }
        }
        var c3 = new HourCount();
        OnlineRules.Rounds(w2.State, shop2, s2, 6 * 24 + 9, ref c3);
        Check(c3.Sent == 0 && v2.Used == 0, "online rounds: a district no driver's day stretches to gets no round, and the van is not sent");
        OnlineRules.Rounds(w2.State, shop2, s2, 7 * 24 + 9, ref c3);
        Check(v2.Day == 7 && v2.Used == 0, "online rounds: a new day starts the driver's minutes again");
    }

    private static void OnlineLateAndRating()
    {
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", 1, OnlineCatalogue.Courier, 1, 2, 2000, 2000, 60, 2000, out site, 500);
        site.Kind = 0;
        var count = new HourCount();
        WebOrder good = OnlineOrder(w, shop, 1, 30, 60, 0, 2, 2000);
        OnlineRules.Pick(shop, site, 31, 1, w.Take, ref count);
        int stars = shop.Stars;
        OnlineRules.Deliver(w.State, shop, site, 32, ref count);
        Check(shop.Orders.Count == 1 && count.Arrived == 0, "online delivery: an order is not at the door before its hour");
        OnlineRules.Deliver(w.State, shop, site, 33, ref count);
        Check(shop.Orders.Count == 0 && shop.Today.OnTime == 1 && shop.Today.Refunds == 0 && shop.Stars == stars + (5000 - stars) / 20, "online delivery: on time and whole: nothing refunded, and the rating moves a twentieth of the way to five stars");
        WebOrder late = OnlineOrder(w, shop, 1, 40, 42, 0, 3, 2000);
        OnlineRules.Pick(shop, site, 41, 1, w.Take, ref count);
        stars = shop.Stars;
        OnlineRules.Deliver(w.State, shop, site, 43, ref count);
        Check(shop.Today.Late == 1 && shop.Today.Refunds == 1500 && shop.BookRefunds == 1500 && shop.Stars == stars + (2000 - stars) / 20, "online delivery: an hour late: a quarter of the 60.00 is refunded and the rating takes a two-star mark");

        shop.Way = OnlineCatalogue.OwnVans;
        WebOrder lost = OnlineOrder(w, shop, 1, 50, 60, 0, 1, 2000);
        OnlineRules.Pick(shop, site, 51, 1, w.Take, ref count);
        stars = shop.Stars;
        int refunds = shop.Today.Refunds;
        OnlineRules.Deliver(w.State, shop, site, 60 + OnlineCatalogue.MissingHours, ref count);
        Check(shop.Orders.Count == 1 && shop.Today.Missing == 0, "online delivery: a packed order no van has taken is still waited for a day after its promise");
        OnlineRules.Deliver(w.State, shop, site, 60 + OnlineCatalogue.MissingHours + 1, ref count);
        Check(shop.Orders.Count == 0 && shop.Today.Missing == 1 && shop.Today.Refunds == refunds + 2000 && shop.Stars == stars + (1000 - stars) / 20, "online delivery: then it is refunded in full and the rating takes a one-star mark");
        Check(shop.Today.Refunds <= shop.Today.Takings, "online delivery: what is refunded is never more than what was taken");

        // The rating's bounds.
        var r = new WebShop();
        int biggest = 0;
        for (int i = 0; i < 2000; i++)
        {
            int was = r.Stars;
            OnlineRules.Rate(r, OnlineCatalogue.ScoreLost, 1300);
            biggest = Math.Max(biggest, Math.Abs(r.Stars - was));
        }
        Check(r.Stars >= OnlineCatalogue.StarsLeast && r.Stars <= OnlineCatalogue.StarsLeast + 20, "online rating: two thousand lost orders at dear prices bring the rating to one star and not under it (" + r.Stars + ")");
        for (int i = 0; i < 2000; i++)
        {
            int was = r.Stars;
            OnlineRules.Rate(r, OnlineCatalogue.ScoreGood, 900);
            biggest = Math.Max(biggest, Math.Abs(r.Stars - was));
        }
        Check(r.Stars <= OnlineCatalogue.StarsMost && r.Stars >= OnlineCatalogue.StarsMost - 20, "online rating: two thousand perfect orders at cheap prices bring it to five stars and not over it (" + r.Stars + ")");
        Check(biggest <= 200, "online rating: no single order moves the rating more than 0.2 of a star (" + biggest + " thousandths at most)");
        var dear = new WebShop();
        var fair = new WebShop();
        var cheap = new WebShop { Stars = 3000 };
        var plain = new WebShop { Stars = 3000 };
        OnlineRules.Rate(dear, OnlineCatalogue.ScoreGood, 1101);
        OnlineRules.Rate(fair, OnlineCatalogue.ScoreGood, 1100);
        OnlineRules.Rate(cheap, OnlineCatalogue.ScoreShort, 940);
        OnlineRules.Rate(plain, OnlineCatalogue.ScoreShort, 1000);
        Check(dear.Stars == 4000 + (4500 - 4000) / 20 && fair.Stars == 4000 + (5000 - 4000) / 20, "online rating: an order more than a tenth over the going price is marked half a star down");
        Check(cheap.Stars == 3000 + (3250 - 3000) / 20 && plain.Stars == 3000, "online rating: an order more than a twentieth under it is marked a quarter of a star up");
        Check(OnlineRef(stars: dear.Stars) < OnlineRef(stars: fair.Stars), "online rating: the rating feeds the orders: the lower-rated site takes fewer");

        // A week of late deliveries is seen as a falling rating.
        var f = new WebShop();
        f.Today.Clear(10);
        var closed = new DayBook();
        OnlineRules.CloseDay(f, 11, closed);
        for (int i = 0; i < 12; i++)
        {
            OnlineRules.Rate(f, OnlineCatalogue.ScoreLate, 1000);
        }
        Check(f.StarsBefore(11) == 4000 && f.StarsBefore(11) - f.Stars >= OnlineCatalogue.FallingBy, "online rating: twelve late orders in a week take more than 0.3 of a star off the rating of a week ago, which is what the Planner calls falling");
    }

    private static void OnlineReturns()
    {
        // The share that comes back, by the kind of goods, over many orders.
        int[] back = new int[6];
        const int Orders = 6000;
        for (int kind = 0; kind < 6; kind++)
        {
            var w = new OnlineWorld();
            Site site;
            WebShop shop = OnlineShop(w, "A~1", 1, OnlineCatalogue.Courier, 1, 2, 2000, 2000, 60, 2000, out site);
            site.Kind = kind;
            var count = new HourCount();
            for (int i = 0; i < Orders; i++)
            {
                OnlineOrder(w, shop, 1, 30 + i, 4000 + i, 0, 1, 2000);
                OnlineRules.PickOrder(shop, site, 31 + i, 0, w.Take, ref count);
                OnlineRules.Deliver(w.State, shop, site, 40 + i, ref count);
                back[kind] += shop.Returns.Count;
                shop.Returns.Clear();
            }
        }
        bool near = true;
        for (int kind = 0; kind < 6; kind++)
        {
            int wanted = Orders * OnlineCatalogue.ReturnShare[kind] / 1000;
            near &= Math.Abs(back[kind] - wanted) <= Math.Max(24, wanted / 6);
        }
        Check(near, "online returns: of 6,000 delivered orders the share that comes back is the kind's: " + back[0] + " fresh, " + back[1] + " groceries, " + back[2] + " clothing, " + back[3] + " electronics, " + back[4] + " home, "
                    + back[5] + " general");
        Check(back[2] > back[0] * 8, "online returns: clothing comes back many times as often as fresh food");

        var w2 = new OnlineWorld();
        Site s2;
        WebShop shop2 = OnlineShop(w2, "B~2", 1, OnlineCatalogue.Courier, 1, 2, 2000, 2000, 60, 2000, out s2, 50);
        s2.Kind = 2;
        string item = shop2.Lines[0].Item;
        var c2 = new HourCount();
        shop2.Today.Takings = 4000;
        shop2.Today.Goods = 1800;
        shop2.Returns.Add(new WebReturn { Day = 4, Item = item, Qty = 2, Refund = 4000, Cost = 900 });
        OnlineRules.Returns(w2.State, shop2, s2, 3 * 24 + 15, w2.Put, ref c2);
        OnlineRules.Returns(w2.State, shop2, s2, 4 * 24 + 9, w2.Put, ref c2);
        Check(shop2.Returns.Count == 1 && c2.Returned == 0, "online returns: a return is not dealt with before its day, nor before ten in the morning");
        // Whether goods go back is a draw: take the first of many returns that goes back and the first that does not.
        bool wentBack = false, writtenOff = false, refunded = true;
        for (int i = 0; i < 40; i++)
        {
            shop2.Returns.Clear();
            shop2.Today.Refunds = 0;
            shop2.Today.Goods = 1800;
            w2.Stock[item] = 50;
            shop2.Returns.Add(new WebReturn { Day = 4, Item = item, Qty = 2, Refund = 4000 + i, Cost = 900 });
            OnlineRules.Returns(w2.State, shop2, s2, 4 * 24 + 10, w2.Put, ref c2);
            refunded &= shop2.Today.Refunds == 4000 + i && shop2.Returns.Count == 0;
            bool back2 = w2.Stock[item] == 52;
            wentBack |= back2 && shop2.Today.Goods == 0;
            writtenOff |= !back2 && shop2.Today.Goods == 1800 && w2.Stock[item] == 50;
        }
        Check(refunded, "online returns: every return is refunded in full on the day it is back");
        Check(wentBack && writtenOff, "online returns: goods fit to sell again go back into stock and their cost comes off the day's goods; the rest is written off and the cost stays");
        w2.NoRoom = true;
        shop2.Today.Goods = 1800;
        w2.Stock[item] = 50;
        bool noRoom = true;
        for (int i = 0; i < 40; i++)
        {
            shop2.Returns.Add(new WebReturn { Day = 4, Item = item, Qty = 2, Refund = 100 + i, Cost = 900 });
            OnlineRules.Returns(w2.State, shop2, s2, 4 * 24 + 10, w2.Put, ref c2);
            noRoom &= w2.Stock[item] == 50 && shop2.Today.Goods == 1800;
        }
        Check(noRoom, "online returns: with no room in the building's storage a return is written off");
        w2.NoRoom = false;
        s2.Kind = 0;
        bool fresh = true;
        for (int i = 0; i < 40; i++)
        {
            shop2.Returns.Add(new WebReturn { Day = 4, Item = item, Qty = 1, Refund = 300 + i, Cost = 900 });
            OnlineRules.Returns(w2.State, shop2, s2, 4 * 24 + 10, w2.Put, ref c2);
            fresh &= w2.Stock[item] == 50;
        }
        Check(fresh, "online returns: returned fresh food never goes back on the shelf");

        // A late order that comes back has only the rest of its value refunded; no more returns are kept than the list holds.
        var w3 = new OnlineWorld();
        Site s3;
        WebShop shop3 = OnlineShop(w3, "C~3", 1, OnlineCatalogue.Courier, 1, 2, 2000, 2000, 60, 2000, out s3);
        s3.Kind = 2;
        var c3 = new HourCount();
        bool rest = true;
        int seen = 0;
        for (int i = 0; i < 400; i++)
        {
            OnlineOrder(w3, shop3, 1, 30 + i, 31 + i, 0, 2, 2000);
            OnlineRules.PickOrder(shop3, s3, 31 + i, shop3.Orders.Count - 1, w3.Take, ref c3);
            OnlineRules.Deliver(w3.State, shop3, s3, 40 + i, ref c3);   // late: due at 31 + i, at the door at 33 + i
            for (int k = seen; k < shop3.Returns.Count; k++)
            {
                rest &= shop3.Returns[k].Refund == 3000 && shop3.Returns[k].Qty == 2 && shop3.Returns[k].Cost == 900 && shop3.Returns[k].Day == (40 + i) / 24 + OnlineCatalogue.ReturnDays;
            }
            seen = shop3.Returns.Count;
        }
        Check(seen > 20 && rest, "online returns: a late order that comes back has the three quarters not yet refunded paid back, two days after it was delivered");
        Check(shop3.Returns.Count <= OnlineCatalogue.ReturnsMost, "online returns: no more returns are kept on their way than the list holds");
        Check(shop3.Today.Late == 400 && shop3.Today.Refunds == 400 * 1000, "online returns: (and each of those late orders had its quarter refunded)");
    }

    private static void OnlineDayAndMoney()
    {
        var b = new DayBook { Takings = 10000, Refunds = 700, Delivery = 1200, Hosting = 300, Goods = 4500 };
        Check(b.PaidIn == 7800 && b.Profit == 3300, "online books: what is paid in is takings less refunds, delivery and hosting; what is left is that less the goods");

        // Sixty days of a site with a courier, then with vans: every cent the books show is a cent the wallet got.
        for (int way = 0; way < 2; way++)
        {
            var w = new OnlineWorld();
            Site site;
            WebShop shop = OnlineShop(w, "A~1", 2, way, 0, 8, 2400, 2500, 70, 3000, out site, 400);
            site.Kind = 2;
            w.State.Vans.Add(new Van { Driver = "d1", Capacity = 6, Ready = true });
            var total = new DayBook();
            long hosting = 0;
            for (int day = 1; day <= 60; day++)
            {
                OnlineRun(w, shop, site, day * 24, day * 24 + 23, day % 7 == 0 ? 0 : 2, total);
                if (OnlineRules.HostingDue(shop, day))
                {
                    OnlineRules.HostingPaid(shop, day, 30000, 30);
                    w.Cash -= 30000;
                    hosting += 30000;
                }
            }
            total.Add(w.Settle(shop, 61));
            Check(w.Cash == (long)total.Takings - total.Refunds - total.Delivery - total.Hosting && total.Hosting == hosting && w.Cash == total.PaidIn,
                "online books, " + (way == 0 ? "vans" : "a courier") + ": over sixty days the wallet holds exactly takings less refunds, delivery and hosting (" + w.Cash + " cents)");
            Check(total.Units == w.TakenUnits && total.Goods == w.TakenCost - w.PutUnits * (2500 * 45 / 100) && !w.WentUnder,
                "online books, " + (way == 0 ? "vans" : "a courier") + ": the units and the cost of goods in the books are what left the stock, less what returns put back; stock never went under nothing");
            long lines = 0;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                lines += shop.Lines[l].BookTakings;
            }
            Check(lines == total.Takings && shop.BookRefunds == total.Refunds, "online books, " + (way == 0 ? "vans" : "a courier") + ": what waits to be entered in the game's statement is the same takings and refunds, product by product");
            Check(total.Orders >= total.Picked + total.Cancelled && total.Picked > 500 && total.Cancelled + total.Short > 0 && total.Returned > 0 && total.OnTime + total.Late + total.Missing <= total.Picked,
                "online books, " + (way == 0 ? "vans" : "a courier") + ": the sixty days had orders, stock gaps and returns in them (" + total.Orders + " came, " + total.Picked + " picked, " + total.Cancelled + " cancelled, "
                + total.Late + " late, " + total.Returned + " returned)");
        }

        // Closing a day.
        var w2 = new OnlineWorld();
        Site s2;
        WebShop shop2 = OnlineShop(w2, "B~2", 1, OnlineCatalogue.Courier, 1, 3, 2000, 2000, 60, 2000, out s2);
        OnlineRun(w2, shop2, s2, 24, 47, 3);
        int units = shop2.Lines[0].Units + shop2.Lines[1].Units + shop2.Lines[2].Units, takings = shop2.Today.Takings, stars = shop2.Stars;
        var closed = new DayBook();
        OnlineRules.CloseDay(shop2, 2, closed);
        Check(closed.Day == 1 && closed.Takings == takings && shop2.Today.Day == 2 && shop2.Today.Takings == 0 && shop2.Today.Orders == 0, "online books: closing a day hands its book over and opens an empty one for the new day");
        Check(shop2.Week[1].Takings == takings && shop2.StarLog[1] == stars && shop2.Lines[0].Units == 0 && shop2.Lines[0].WeekUnits[1] + shop2.Lines[1].WeekUnits[1] + shop2.Lines[2].WeekUnits[1] == units && units > 0,
            "online books: the closed day is one of the week's seven, with the rating at its close and each product's units");
        var week = new DayBook();
        shop2.WeekInto(week, 2);
        Check(week.Takings == takings && shop2.Lines[0].SoldWeek == shop2.Lines[0].WeekUnits[1], "online books: the week is the closed days and today together");
        OnlineRules.CloseDay(shop2, 10, closed);
        shop2.WeekInto(week, 10);
        Check(week.Takings == 0 && shop2.Lines[0].SoldWeek == 0 && shop2.StarsBefore(10) == 0, "online books: after more than a week away nothing of the days before it is in the week any more, nor a rating of a week ago");
        shop2.Week[4].Day = 4;
        shop2.Week[4].Takings = 500;
        shop2.Week[3].Day = 3;
        shop2.Week[3].Takings = 900;
        shop2.WeekInto(week, 10);
        shop2.Week[3].Clear(0);
        shop2.Week[4].Clear(0);
        Check(week.Takings == 500, "online books: a day six days back is in the week, one seven days back is not");

        // Hosting and advertising.
        Check(!OnlineRules.HostingDue(shop2, 30) && OnlineRules.HostingDue(shop2, 31), "online hosting: the set-up has a month in it; the next is due thirty days on");
        OnlineRules.HostingMissed(shop2);
        OnlineRules.Plan(shop2, s2, 31, 0, 1, 1, 1);
        Check(shop2.Offline && s2.PerDay == 0 && OnlineRules.HostingDue(shop2, 32), "online hosting: unpaid, the site is offline, takes no orders, and the fee is asked for again the next day");
        OnlineRules.HostingPaid(shop2, 32, 30000, 30);
        OnlineRules.Plan(shop2, s2, 32, 0, 1, 1, 1);
        Check(!shop2.Offline && s2.PerDay > 0 && shop2.HostDue == 62 && shop2.Today.Hosting == 30000, "online hosting: paid, it is online again for a month, and the fee is in the day's book");
        int cost, percent;
        OnlineRules.AdQuote(shop2, 40, 1000, (int)CycleState.Steady, out cost, out percent);
        Check(cost == 400 && percent == 20 && OnlineRules.CanAdvertise(shop2), "online advertising: 400 for 20% more orders in steady times");
        OnlineRules.AdQuote(shop2, 40, 1500, (int)CycleState.Boom, out cost, out percent);
        Check(cost == 600 && percent == 22, "online advertising: prices half as high again make it 600, and a boom makes it 22%");
        OnlineRules.Advertise(shop2, 40, 20);
        Check(!shop2.Advertising(40) && shop2.Advertising(41) && shop2.Advertising(45) && !shop2.Advertising(46) && !OnlineRules.CanAdvertise(shop2), "online advertising: booked today it runs for five days from tomorrow, and no second one can be booked meanwhile");
        OnlineRules.Plan(shop2, s2, 41, 0, 1, 1, 1);
        int with = s2.PerDay;
        OnlineRules.Plan(shop2, s2, 46, 0, 1, 1, 1);
        Check(with > s2.PerDay && s2.Ads == 0, "online advertising: orders are up while it runs and back afterwards");
        OnlineRules.CloseDay(shop2, 46, closed);
        OnlineRules.AdQuote(shop2, 46, 1000, (int)CycleState.Steady, out cost, out percent);
        Check(shop2.AdEnded == 46 && OnlineRules.CanAdvertise(shop2) && percent == 11, "online advertising: the day it ends another can be booked, at little more than half the effect so soon after");
        OnlineRules.AdQuote(shop2, 60, 1000, (int)CycleState.Steady, out cost, out percent);
        Check(percent == 20, "online advertising: ten days on it works in full again");

        // A gap without the feature.
        var w3 = new OnlineWorld();
        Site s3;
        WebShop shop3 = OnlineShop(w3, "C~3", 1, OnlineCatalogue.OwnVans, 1, 2, 2000, 2000, 60, 2000, out s3);
        var c3 = new HourCount();
        OnlineOrder(w3, shop3, 1, 30, 60, 0, 1, 2000);
        OnlineRules.Pick(shop3, s3, 31, 1, w3.Take, ref c3);
        OnlineOrder(w3, shop3, 1, 31, 60, 0, 1, 2000);
        w3.State.Vans.Add(new Van { Driver = "d1", FreeAt = 99999 });
        shop3.Idle = 5;
        int rating = shop3.Stars;
        OnlineRules.Resume(w3.State, shop3);
        Check(shop3.Orders.Count == 0 && shop3.Today.OnTime == 1 && shop3.Today.Cancelled == 0 && shop3.Stars == rating && shop3.Idle == 0 && w3.State.Vans[0].FreeAt == 0 && shop3.Today.Takings == 2000,
            "online: after hours without the feature nobody is blamed: the unpicked order is dropped unrated, the packed one counts as delivered, its takings stay, and the vans are free");
    }

    private static void OnlineLinesAndCosts()
    {
        var w = new OnlineWorld();
        WebShop shop = OnlineRules.Open(w.State, "A~1", "A online", 1, OnlineCatalogue.Courier, 1, false, 5, 30);
        Check(shop != null && shop.HostDue == 35 && shop.Stars == 4000 && shop.Today.Day == 5 && OnlineRules.Open(w.State, "A~1", "again", 1, 1, 1, false, 5, 30) == null, "online: a business has one site, opened at four stars with a month of hosting");
        for (int i = 1; i < OnlineCatalogue.ShopsMost; i++)
        {
            OnlineRules.Open(w.State, "P~" + i, "p", 0, 1, 1, false, 5, 30);
        }
        Check(w.State.Shops.Count == OnlineCatalogue.ShopsMost && OnlineRules.Open(w.State, "Z~99", "z", 0, 1, 1, false, 5, 30) == null, "online: a player has twelve sites at most");
        bool added = true;
        for (int i = 0; i < OnlineCatalogue.LinesMost; i++)
        {
            added &= OnlineRules.Offer(shop, "item" + i) != null;
        }
        Check(added && shop.Lines.Count == 12 && OnlineRules.Offer(shop, "item12") == null && OnlineRules.Offer(shop, "item3") == shop.Lines[3], "online catalogue: twelve products at most; offering one that is there again is no new one");
        WebOrder waits = OnlineOrder(w, shop, 0, 130, 160, 4, 1, 1000), packed = OnlineOrder(w, shop, 0, 130, 160, 2, 1, 1000), later = OnlineOrder(w, shop, 0, 130, 160, 9, 1, 1000);
        packed.Stage = OrderStage.Picked;
        shop.Lines[2].On = shop.Lines[4].On = shop.Lines[6].On = shop.Lines[7].On = false;
        shop.Lines[7].BookTakings = 500;
        int removed = OnlineRules.Prune(shop);
        Check(removed == 2 && shop.Lines.Count == 10 && shop.Line("item2") == null && shop.Line("item6") == null && shop.Line("item4") != null && shop.Line("item7") != null,
            "online catalogue: a product taken off the site leaves the list once nothing hangs on it; one with an order to pick, or with sales not yet in the books, stays");
        Check(packed.Line[0] == -1 && shop.Lines[waits.Line[0]].Item == "item4" && shop.Lines[later.Line[0]].Item == "item9", "online catalogue: the orders still point at their own products after the list has closed up");

        Check(OnlineRules.SetUpCost(0, false, 1000) == 2500 && OnlineRules.SetUpCost(1, false, 1000) == 6000 && OnlineRules.SetUpCost(2, false, 1000) == 12000, "online costs: 2,500, 6,000 and 12,000 to set up at the start's prices");
        Check(OnlineRules.SetUpCost(1, true, 1000) == 3600 && OnlineRules.SetUpCost(2, true, 1200) == 8640, "online costs: the player's own web agency builds it for forty per cent less, at today's prices");
        Check(OnlineRules.HostingCost(0, 1000) == 150 && OnlineRules.HostingCost(1, 1000) == 300 && OnlineRules.HostingCost(2, 1250) == 750, "online costs: hosting is 150, 300 and 600 a month and follows the city's prices");
    }

    private static void OnlineSaveAndLoad()
    {
        string history = OnlineHistory(4242, 12);
        string line = history.Substring(0, history.IndexOf('#'));
        int version;
        OnlineState read = OnlineStore.Read(line, out version);
        Check(read != null && version == OnlineStore.Version && OnlineStore.Write(read) == line, "online save: twelve days of two sites, with open orders, returns, a van and the books, are read back exactly as they were written");
        Check(read.Shops.Count == 2 && read.Shops[0].Lines.Count == 6 && read.Shops[1].Lines.Count == 9 && read.Districts.Count == OnlineDistricts && read.Vans.Count == 1 && read.Shops[0].Orders.Count + read.Shops[1].Orders.Count > 0,
            "online save: (the line holds both sites, their catalogues, the districts, the van and open orders)");
        var empty = new OnlineState { Seed = 9, FirstDay = 3 };
        OnlineState again = OnlineStore.Read(OnlineStore.Write(empty), out version);
        Check(again != null && again.Seed == 9 && again.FirstDay == 3 && again.LastHour == -1 && again.NextOrder == 1 && again.Shops.Count == 0, "online save: a state with no site at all round-trips");
        Check(OnlineStore.Read(null, out version) == null && OnlineStore.Read(string.Empty, out version) == null && OnlineStore.Read("rubbish", out version) == null && OnlineStore.Read("v=x^h:1|2", out version) == null
              && OnlineStore.Read("v=1^s:A~1|a", out version) == null, "online save: nothing, rubbish, a version that is no number and a line without its header are all read as no state");
        var data = new Dictionary<string, string> { { "other", "x" } };
        Check(OnlineStore.Load(data, OnlineStore.Key, out version) == null && OnlineStore.Load(null, OnlineStore.Key, out version) == null, "online save: a save made without the feature has no line, and that is no error");
        OnlineState newer = OnlineStore.Read("v=7^h:5|1|100|9|77|extra^q:something|new^d:north;south^s:A~1|Shop|1|1|0|0|1|31|0|4200|0|0|0|0|0|0|0|0|0|0|0|0|150|999|more^l:cola|250|1|2|500|225|0|1|0|0|0|0|0|0|0|0|0|0|0|0|0|500|225|x"
                                             + "^o:8|1|90|116|0|0|1|0|1000|0|0|2|0|250|0|future^b:4|3|2|2|500|225|0|0|0|0|0|0|0|0|0|0|1", out version);
        Check(newer != null && version == 7 && newer.Seed == 5 && newer.NextOrder == 9 && newer.Districts.Count == 2 && newer.Shops.Count == 1 && newer.Shops[0].Stars == 4200 && newer.Shops[0].BookRefunds == 150
              && newer.Shops[0].Lines.Count == 1 && newer.Shops[0].Lines[0].Price == 250 && newer.Shops[0].Lines[0].BookTakings == 500 && newer.Shops[0].Orders.Count == 1 && newer.Shops[0].Orders[0].Qty[0] == 2
              && newer.Shops[0].Today.Takings == 500, "online save: a line from a later version, with fields and a part this one does not know, still loads everything it does know");
        OnlineState wild = OnlineStore.Read("v=1^h:5|1|100|0^d:north^s:A~1|Shop|7|9|4|1|1|31|0|9999^l:cola|-5|1|-3^o:1|5|1|2|3|0|1|0|0|0|0|1|0|100|0;2|0|1|2|3|9|1|0|0|0|0|1|0|100|0;3|0|1|2|3|0|1|0|0|0|40|50|60|100|0", out version);
        Check(wild != null && wild.NextOrder == 1 && wild.Shops[0].Quality == 2 && wild.Shops[0].Way == 1 && wild.Shops[0].Promise == 1 && wild.Shops[0].Stars == 5000 && wild.Shops[0].Lines[0].Price == 0 && wild.Shops[0].Lines[0].Units == 0,
            "online save: figures out of their bounds are put inside them");
        Check(wild.Shops[0].Orders.Count == 1 && wild.Shops[0].Orders[0].Id == 3 && wild.Shops[0].Orders[0].Line[0] == -1 && wild.Shops[0].Orders[0].Qty[0] == 9 && wild.Shops[0].Orders[0].Got[0] == 9,
            "online save: an order for a district or at a stage the save does not know is not kept; one whose product has left the catalogue is, without the product");
        var named = new OnlineState { Seed = 1 };
        WebShop odd = OnlineRules.Open(named, "A~1", "Nuts|Bolts;More^Less", 1, 1, 1, false, 1, 30);
        OnlineState back = OnlineStore.Read(OnlineStore.Write(named), out version);
        Check(odd != null && back != null && back.Shops.Count == 1 && back.Shops[0].Name == "Nuts Bolts More Less", "online save: a name with the line's own dividers in it cannot break the line apart");
    }

    // A site's month under given conditions, with all the stock and staff it can use: orders, takings, goods, delivery, refunds a day (cents).
    private static DayBook OnlineMonth(int quality, int way, int promise, int ask, int stars, int ads, int cycle, int season, int kind, int pickers, int vans, out int most)
    {
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", quality, way, promise, 8, ask, 2500, 60, 2000, out site);
        site.Kind = kind;
        site.Cycle = cycle;
        site.Season = season;
        shop.Stars = stars;
        for (int v = 0; v < vans; v++)
        {
            w.State.Vans.Add(new Van { Driver = "d" + v, Capacity = 8, Ready = true });
        }
        var total = new DayBook();
        most = 0;
        const int Days = 30;
        for (int day = 1; day <= Days; day++)
        {
            if (ads > 0)
            {
                shop.AdFrom = day;
                shop.AdEnd = day + 1;
                shop.AdPercent = ads;
            }
            shop.Stars = stars;   // (held where it is: a month of good service would lift it, and the orders with it)
            OnlineRules.Plan(shop, site, day, 0, cycle, season, 1);
            for (int h = 0; h < 24; h++)
            {
                OnlineRules.Hour(w.State, shop, site, day * 24 + h, h >= 8 && h < 20 ? pickers : 0, w.Take, w.Put);
            }
            most = Math.Max(most, shop.Today.Orders);
            total.Add(w.Settle(shop, day + 1));
        }
        total.Orders /= Days;
        total.Picked /= Days;
        total.Takings /= Days;
        total.Goods /= Days;
        total.Delivery /= Days;
        total.Refunds /= Days;
        return total;
    }

    private static void OnlineBalance()
    {
        // What a site adds at the reference conditions, quality by quality and by who delivers. The measure is a mid-game
        // shop taking 6,000 a day over the counter (600,000 cents).
        const int Counter = 600000;
        bool meaningful = true, bounded = true, vansCheaper = true;
        Console.WriteLine("  online balance, a month at the going price, next day, four stars, steady times, spring, 2 km, general goods (a day's figures):");
        for (int q = 0; q < 3; q++)
        {
            int most;
            DayBook courier = OnlineMonth(q, OnlineCatalogue.Courier, 1, 2500, 4000, 0, 1, 1, 5, 6, 0, out most);
            DayBook vans = OnlineMonth(q, OnlineCatalogue.OwnVans, 1, 2500, 4000, 0, 1, 1, 5, 6, 2, out most);
            int hosting = OnlineCatalogue.Hosting[q] * 100 / 30;
            int left = courier.Takings - courier.Goods - courier.Delivery - courier.Refunds - hosting, leftVans = vans.Takings - vans.Goods - vans.Delivery - vans.Refunds - hosting;
            Console.WriteLine("    quality " + q + ": " + courier.Orders + " orders, takings " + courier.Takings / 100 + ", goods " + courier.Goods / 100 + ", courier " + courier.Delivery / 100 + ", refunds " + courier.Refunds / 100
                              + ", hosting " + hosting / 100 + ", left " + left / 100 + " (" + courier.Takings * 100 / Counter + "% of a 6,000 counter); with own vans delivery " + vans.Delivery / 100 + ", left " + leftVans / 100);
            meaningful &= courier.Orders >= OnlineCatalogue.OrdersADay[q] - 2 && courier.Orders <= OnlineCatalogue.OrdersADay[q] + 2 && left > 0 && courier.Takings * 100 / Counter >= 6;
            bounded &= courier.Takings * 100 / Counter <= 35 && most <= OnlineCatalogue.OrdersMost[q];
            vansCheaper &= vans.Delivery < courier.Delivery && leftVans > left;
        }
        Check(meaningful, "online balance: at the reference conditions a site takes its quality's orders a day, leaves money over after goods, delivery, refunds and hosting, and adds at least six per cent to a 6,000 counter");
        Check(bounded, "online balance: at the reference conditions the best site adds no more than about a third to that counter");
        Check(vansCheaper, "online balance: the player's own vans deliver for less than a courier (their drivers' wages are the game's own)");

        // Everything at its best together: the bound holds.
        bool held = true;
        Console.WriteLine("  online balance, everything at its best (30% under the going price, same day, five stars, advertising, a boom, winter, staff and vans to spare):");
        for (int q = 0; q < 3; q++)
        {
            int most;
            DayBook best = OnlineMonth(q, OnlineCatalogue.Courier, 0, 1750, 5000, 30, 0, 0, 5, 12, 0, out most);
            DayBook fair = OnlineMonth(q, OnlineCatalogue.Courier, 0, 2500, 5000, 30, 0, 0, 5, 12, 0, out most);
            Console.WriteLine("    quality " + q + ": " + best.Orders + " orders a day (bound " + OnlineCatalogue.OrdersMost[q] + "), takings " + best.Takings / 100 + " at 17.50, " + fair.Takings / 100 + " at the going 25.00 ("
                              + fair.Takings * 100 / Counter + "% of a 6,000 counter)");
            held &= best.Orders <= OnlineCatalogue.OrdersMost[q] && fair.Orders <= OnlineCatalogue.OrdersMost[q] && most <= OnlineCatalogue.OrdersMost[q] && fair.Takings * 100 / Counter <= 70;
        }
        Check(held, "online balance: with everything at its best together no site passes its bound of 20, 40 or 60 orders a day, or about two thirds of a 6,000 counter");

        // What bounds it short of demand: staff, vans, stock.
        int m;
        DayBook oneHour = OnlineMonth(2, OnlineCatalogue.Courier, 0, 1750, 5000, 30, 0, 0, 5, 0, 0, out m);
        Check(oneHour.Picked == 0 && oneHour.Takings == 0, "online balance: with nobody on shift nothing is picked and nothing is earned, whatever the demand");
        var w = new OnlineWorld();
        Site site;
        WebShop shop = OnlineShop(w, "A~1", 2, OnlineCatalogue.Courier, 0, 8, 1750, 2500, 100, 2000, out site);
        shop.Stars = 5000;
        int picked = 0;
        for (int h = 0; h < 24; h++)
        {
            OnlineRules.Plan(shop, site, 1, 0, 0, 0, 1);
            HourCount c = OnlineRules.Hour(w.State, shop, site, 24 + h, h >= 9 && h < 17 ? 1 : 0, w.Take, w.Put);
            picked += c.Picked;
        }
        Check(picked <= 8 * OnlineCatalogue.PickPerHour && shop.Today.Orders == OnlineCatalogue.OrdersMost[2], "online balance: one member of staff on an eight-hour shift picks 48 orders at the very most, of the 60 that came (" + picked + ")");
        var w2 = new OnlineWorld();
        Site s2;
        WebShop shop2 = OnlineShop(w2, "B~2", 2, OnlineCatalogue.Courier, 0, 8, 1750, 2500, 100, 2000, out s2, 5);
        shop2.Stars = 5000;
        OnlineRun(w2, shop2, s2, 24, 24 * 6, 6);
        long sold = 0;
        for (int l = 0; l < 8; l++)
        {
            sold += 5 - w2.Stock[shop2.Lines[l].Item];
        }
        Check(sold == 40 && w2.TakenUnits == 40 && !w2.WentUnder, "online balance: a place with forty units in stock sells forty units online and not one more, in five days of full demand");
    }

    private static void OnlineTiming()
    {
        // Forty sites, a day of hourly steps: under a millisecond in all.
        const int Sites = 40, Runs = 30, Warm = 120;
        var w = new OnlineWorld();
        var shops = new WebShop[Sites];
        var sites = new Site[Sites];
        for (int i = 0; i < Sites; i++)
        {
            w.State.Shops.Clear();   // (the rule's limit of twelve is the player's, not the timing's)
            shops[i] = OnlineShop(w, "T~" + i, i % 3, i % 2, i % 2, 12, 2200 + 20 * i, 2500, 70, 1500 + 200 * i, out sites[i]);
            sites[i].Kind = i % 6;
        }
        for (int v = 0; v < 6; v++)
        {
            w.State.Vans.Add(new Van { Driver = "d" + v, Capacity = 8, Ready = true });
        }
        // (the rules are timed, not this file's stand-in for a building: stock that is always there, at a round cost)
        StockTake take = (string item, int wanted, out int cost) =>
        {
            cost = wanted * 1100;
            return wanted;
        };
        StockBack put = (item, units, unitCost) => units;
        var watch = new Stopwatch();
        double best = double.MaxValue, sum = 0;
        long orders = 0;
        for (int run = 0; run < Runs + Warm; run++)
        {
            int day = run + 1;
            if (run == Warm - 20)
            {
                System.Threading.Thread.Sleep(300);   // (the runtime compiles busy code again, better, a moment after it has seen it run: that is the code to time)
            }
            for (int i = 0; i < Sites; i++)
            {
                var closed = new DayBook();
                OnlineRules.CloseDay(shops[i], day, closed);
                orders += closed.Orders;
            }
            watch.Restart();
            for (int h = 0; h < 24; h++)
            {
                for (int i = 0; i < Sites; i++)
                {
                    OnlineRules.Hour(w.State, shops[i], sites[i], day * 24 + h, h >= 8 && h < 20 ? 2 : 0, take, put);
                }
            }
            watch.Stop();
            if (run >= Warm)   // (the first runs warm the code up)
            {
                best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
                sum += watch.Elapsed.TotalMilliseconds;
            }
        }
        Check(orders > Sites * (Runs + Warm) * 5, "online timing: (the forty sites were busy: " + orders + " orders in " + (Runs + Warm) + " days)");
        Check(sum / Runs < 1.0, "online timing: forty sites, a day of hourly steps, in under a millisecond in all (the mean of " + Runs + " days was " + (sum / Runs).ToString("0.000") + " ms, the quickest " + best.ToString("0.000") + " ms)");
        Console.WriteLine("  online timing, 40 sites x 24 hours: quickest " + best.ToString("0.000") + " ms, mean " + (sum / Runs).ToString("0.000") + " ms");
    }
}
