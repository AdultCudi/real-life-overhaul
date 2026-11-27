using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The tech fixes of 27 Nov 2026: a game on a subscription earns on every day of its sale and says what it takes a month, a firm's price in a sale or a round is not lifted by a
// run of one-off licence deals, a firm that withdraws a licence and launches another does not find its buyers again, and a product's page says where the engineers' day went.
// Run alone with --tech28.
internal static partial class Program
{
    private static void Tech28()
    {
        Tech28Subscription();
        Tech28Value();
        Tech28Relaunch();
        Tech28Points();
        Tech28Saves();
    }

    private static GameDev Tech28Game(int id, GameModel model, int day, out StudioFile f, out long[] plat)
    {
        plat = new long[StudioCatalogue.GamePlatforms];
        f = StudioFileFor(1);
        GameDev g = MadeGame(id, Genre.Adventure, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 70, 0, day);
        g.Model = (int)model;
        g.Price = StudioRules.PriceShown((int)model, 2, 1, 0);
        StudioRules.Release(g, f, day, 500, f.Seed, 3000, plat, 0);
        return g;
    }

    // A subscription game with its copies all sold still pays its subscribers each day (a game bought once pays nothing then), and a day of a few copies brings its share of subscribers.
    private static void Tech28Subscription()
    {
        GameDev sub = Tech28Game(41, GameModel.Subscription, StudioDay, out StudioFile f, out long[] plat);
        sub.Sold = 4000000000L;
        sub.Subscribers = 20000;
        long expected = 20000L * StudioRules.PriceOn(sub, 0) / 30;
        long pays = 0;
        bool each = true;
        for (int d = 0; d < 3; d++)
        {
            GameSales r = StudioRules.SellDay(sub, f, StudioDay + 500 + d, 3000, plat, 1000, 1000);
            each &= r.Units == 0 && Math.Abs(r.Gross - expected) * 50 <= expected && r.Payout > 0 && sub.Daily == r.Payout;
            pays += r.Payout;
            if (!each)
            {
                Console.WriteLine("TECH28 day " + d + ": units " + r.Units + " gross " + r.Gross + " expected " + expected + " payout " + r.Payout + " daily " + sub.Daily);
                break;
            }
        }
        Check(each && expected > 0, "tech28 subscription: 20,000 subscribers of a game with no copies left pay " + expected / 100 + " a day on every day of its sale, the studio keeps " + pays / 300 + " of it");
        GameDev once = Tech28Game(42, GameModel.Premium, StudioDay, out _, out long[] plat2);
        once.Sold = 4000000000L;
        once.Subscribers = 20000;
        GameSales none = StudioRules.SellDay(once, f, StudioDay + 500, 3000, plat2, 1000, 1000);
        Check(none.Gross == 0 && once.Daily == 0, "tech28 subscription: the same game sold by the copy earns nothing once its copies are gone, so the check above is the subscription's own pay");
        Check(sub.Daily * FirmsCatalogue.MonthDays > 0 && Tech28MonthLine(sub), "tech28 subscription: the game's page says what it takes a month while a subscription pays");
        sub.Daily = 0;
        Check(!Tech28MonthLine(sub), "tech28 subscription: and says nothing of a month while nothing has been paid");

        GameDev few = Tech28Game(43, GameModel.Subscription, StudioDay, out StudioFile f2, out long[] plat3);
        few.Score = 100;
        few.WindowStart = -1;
        long life = StudioRules.LifeUnits(few, f2, StudioDay + 300, 3000, plat3, out _);
        few.Sold = Math.Max(0L, life - 4000);   // a few copies left, so a day sells one or two
        long start = few.Sold, old = 0;
        int take = StudioCatalogue.SubscribeTake[StudioCatalogue.Online[(int)Genre.Adventure] ? 1 : 0];
        for (int d = StudioDay + 300; d < StudioDay + 400 && few.Subscribers < 600; d++)
        {
            GameSales r = StudioRules.SellDay(few, f2, d, 3000, plat3, 1000, 1000);
            old += r.Units * take / 1000;
        }
        long exact = few.Sold * take / 1000 - start * take / 1000;
        Check(few.Sold > start && few.Subscribers == exact && old < exact, "tech28 subscription: " + (few.Sold - start) + " copies of a few a day bring " + few.Subscribers + " subscribers, where cutting each day's share down brought " + old);
    }

    private static bool Tech28MonthLine(GameDev g)
    {
        var f = StudioFileFor(1);
        f.Games.Add(g);
        StudioScreen screen = StudioViews.Game(f, g, "G", new StudioPlace(), 1000, 0, StudioDay + 10, StudioYear, 0, 0, new SectorWords());
        foreach (ViewSection s in screen.Sections)
        {
            foreach (ViewLine l in s.Lines)
            {
                if (l.Key != null && l.Key.EndsWith("sales_month", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static long Tech28Takings(Firm firm, Func<int, long> day, int days)
    {
        for (int d = 0; d < days; d++)
        {
            FirmsRules.Roll(firm, d, day(d));
        }
        return firm.Revenue30;
    }

    private static long Tech28OldValue(Firm firm, long arr)
    {
        int bp = 0;
        if (TechStatusRules.Change(firm.Revenue30, firm.Prev30, firm.PrevDays, out int percent))
        {
            bp = TalentCatalogue.Clamp(percent * 100, -5000, 20000);
        }
        return FirmsRules.Value(firm, arr, bp, firm.Revenue30);
    }

    private static long Tech28NewValue(Firm firm, long arr) => FirmsRules.Value(firm, arr, FirmsRules.Growth(firm));

    // A run of one-off licence deals in the last days lifted a firm's takings, its growth and so its price; its steady months and three-month growth no longer take it for the firm.
    private static void Tech28Value()
    {
        const long arr = 24000000;       // $20,000 a month of recurring revenue
        const long daily = 66667;
        var steady = new Firm { Place = "s" };
        var spiked = new Firm { Place = "k" };
        var growing = new Firm { Place = "g" };
        Tech28Takings(steady, d => daily, 150);
        Tech28Takings(spiked, d => daily + (d >= 140 ? 4000000 : 0), 150);   // ten days of a $40,000 deal a day on top
        Tech28Takings(growing, d => (long)(daily * Math.Pow(1.08, d / 30.0)), 150);
        long steadyOld = Tech28OldValue(steady, arr), steadyNew = Tech28NewValue(steady, arr);
        long spikeOld = Tech28OldValue(spiked, arr), spikeNew = Tech28NewValue(spiked, arr);
        Console.WriteLine("TECH28 value: a steady firm is worth " + steadyOld / 100 + " by the old reading and " + steadyNew / 100 + " now; the same firm after ten days of licence deals "
            + spikeOld / 100 + " then and " + spikeNew / 100 + " now (growth " + FirmsRules.Growth(spiked) + " bp, was 20000)");
        Check(Math.Abs(steadyNew - steadyOld) * 100 <= steadyOld * 5, "tech28 value: a steady firm is worth about what it was (" + steadyOld / 100 + " then, " + steadyNew / 100 + " now)");
        Check(spikeOld > steadyOld * 3, "tech28 value: ten days of licence deals multiplied the old price (" + steadyOld / 100 + " to " + spikeOld / 100 + ")");
        Check(spikeNew * 100 <= steadyNew * 110, "tech28 value: they leave the price what a steady firm's is (" + spikeNew / 100 + " against " + steadyNew / 100 + ")");
        int growth = FirmsRules.Growth(growing);
        Check(growth > 300 && growth < 1500, "tech28 value: a firm that grows 8% a month for five months still counts its growth (" + growth + " bp)");
        var young = new Firm { Place = "y" };
        Tech28Takings(young, d => daily + (d >= 60 ? 4000000 : 0), 75);
        Check(FirmsRules.Growth(young) <= 0, "tech28 value: a firm with under three months behind it counts no growth, however good its last month");
        var fell = new Firm { Place = "f" };
        Tech28Takings(fell, d => d < 120 ? daily : daily / 2, 150);
        Check(FirmsRules.Growth(fell) < 0, "tech28 value: a fall still counts as one");
        string offers = ModSource("Tech/TechDirector.Exits.cs");
        Check(MemberText(offers, "internal void FirmValue(").Contains("FirmsRules.Growth(firm.File)") && ModSource("Tech/Firms/FirmsRules.cs").Contains("return Value(firm, arr, growthBp, Steady(firm));"),
            "tech28 value: an offer, a round and a listing all read the firm's price on its steady month and its three-month growth");
    }

    // A licence withdrawn leaves its customers counted for the firm's next licence of the market, fading over a year.
    private static void Tech28Relaunch()
    {
        var ff = new FirmsFile { Seed = 5 };
        Product gone = FirmsRules.NewProduct(ff, "p", ProductKind.Licence, Segment.Enterprise, 0, "Old");
        gone.Stage = 2;
        gone.Users = 100;
        gone.Ended = 1000;
        Product fresh = FirmsRules.NewProduct(ff, "p", ProductKind.Licence, Segment.Enterprise, 0, "New");
        fresh.Stage = 1;
        fresh.Features = 10;
        Product other = FirmsRules.NewProduct(ff, "q", ProductKind.Licence, Segment.Enterprise, 0, "Elsewhere");
        ff.Products.Add(gone);
        ff.Products.Add(fresh);
        ff.Products.Add(other);
        other.Stage = 2;
        other.Users = 100;
        other.Ended = 1000;
        long soon = SectorRules.Former(ff, fresh, 1010, 60), later = SectorRules.Former(ff, fresh, 1040, 60), over = SectorRules.Former(ff, fresh, 1060, 60);
        Check(soon == 100L * 50 / 60 && later == 100L * 20 / 60 && over == 0, "tech28 relaunch: the customers of a withdrawn licence count " + soon + " ten days on and " + later + " forty days on, none after a year of 60 days");
        gone.Ended = 0;
        Check(SectorRules.Former(ff, fresh, 1010, 60) == 0, "tech28 relaunch: a licence withdrawn before the day was kept counts for none, as the save was");
        gone.Ended = 1000;
        Product saas = FirmsRules.NewProduct(ff, "p", ProductKind.Saas, Segment.Business, 0, "Service");
        Check(SectorRules.Former(ff, saas, 1010, 60) == 0 && SectorRules.Former(ff, gone, 1010, 60) == 0, "tech28 relaunch: only a licence of the same firm meets them, no service and no other firm's");

        var firm = new Firm { Place = "p", Reputation = 700 };
        var m = new SectorMarketDay { Buyers = 20000, All = 10, Who = (int)SectorBuyers.Enterprises };
        long target = SectorRules.Target(m, SectorRules.Attraction(100, 100));
        long free = 0, held = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            fresh.Users = 0;
            fresh.Carry = 400000000;
            ProductDay r = SectorRules.SellDay(ff, firm, fresh, default(Crew), 0, 1010, m, 100, 100, 1000, pass == 0 ? 0 : soon);
            if (pass == 0)
            {
                free = r.Won;
            }
            else
            {
                held = r.Won;
            }
        }
        Check(target > soon && free > 0 && held < free && held <= target - soon, "tech28 relaunch: a share of " + target + " buyers closes " + free + " deals for a new licence, and " + held + " with " + soon + " already served");
        string dir = ModSource("Tech/TechDirector.FirmsApp.cs"), sell = ModSource("Tech/TechDirector.Sector.cs");
        Check(MemberText(dir, "internal void Withdraw(int id)").Contains("p.Ended = Save != null ? Save.Day : 0;") && sell.Contains("SectorRules.Former(_firmsFile, p, day, _sector.YearDays)"),
            "tech28 relaunch: a withdrawal keeps its day and a licence's day reads it");
    }

    private static void Tech28Points()
    {
        var fmt = new SectorWords();
        ViewLine none = SectorViews.Points(0, 0, 0, fmt), warn = SectorViews.Points(0, 440, 440, fmt), plain = SectorViews.Points(100, 440, 300, fmt);
        Check(none.Key == "reallife_tech_mkt_product_points_none" && none.Tone == ViewTone.Warn, "tech28 points: a firm with no engineer at work says that only engineers build a product");
        Check(warn.Key == "reallife_tech_mkt_product_points" && warn.Tone == ViewTone.Warn && string.Join(",", warn.Pairs) == "put,0,total,440,contracts,440,others,0",
            "tech28 points: client contracts that take all the engineers' points show as a warning with the product's none");
        Check(plain.Tone == ViewTone.Plain && string.Join(",", plain.Pairs) == "put,100,total,440,contracts,300,others,40", "tech28 points: the day's points split into the product's, the contracts' and the other products'");
        string day = ModSource("Tech/TechDirector.Firms.cs");
        Check(day.Contains("p.Put = points;") && day.Contains("firm.Contracted = c.Engineering - engineering;") && ModSource("Tech/TechDirector.SectorDesk.cs").Contains("SectorViews.Points(p.Put, firm.Crew.Engineering, firm.Contracted, fmt)"),
            "tech28 points: the firms' day keeps what it gave and the market section of a product's page shows it");
    }

    private static void Tech28Saves()
    {
        var ff = new FirmsFile { Seed = 3 };
        ff.Firms.Add(new Firm { Place = "Long%20Rd~7", Kind = (int)FirmKind.Software, Prev60 = 777, Prev60Days = 30 });
        Product p = FirmsRules.NewProduct(ff, "Long%20Rd~7", ProductKind.Licence, Segment.Enterprise, 0, "L");
        p.Stage = 2;
        p.Ended = 1234;
        ff.Products.Add(p);
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(ff), out _);
        Check(back.Firms[0].Prev60 == 777 && back.Firms[0].Prev60Days == 30 && back.Products[0].Ended == 1234, "tech28 saves: a firm's third month and a product's day of withdrawal are kept");
        var st = StudioFileFor(2);
        GameDev g = Tech28Game(44, GameModel.Subscription, StudioDay, out _, out _);
        g.Daily = 55555;
        st.Games.Add(g);
        StudioFile again = StudioStore.Read(StudioStore.Write(st), out _);
        Check(again.Games.Count == 1 && again.Games[0].Daily == 55555, "tech28 saves: a subscription's last day's net is kept");
    }
}
