using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Utilities;

// The player's own broadband company (Scripts/Utilities/Broadband*): the catalogue, the city's market and how it moves,
// the company's books, the rivals' answer, the sale, the company as a provider of the internet, and the save.
internal static partial class Program
{
    private static void Broadband()
    {
        const int Kestrel = 0, Brightvein = 1, Garment = 0, Midtown = 4, Hamptons = 6, M = 5, Level = 1000;
        var calm = new[] { 1000, 1000, 1000, 1000 };

        // ---------------------------------------------------------------- the catalogue
        Check(BroadbandCatalogue.Districts.Length == Network.Districts && BroadbandCatalogue.Market.Length == Network.Districts && BroadbandCatalogue.BuildCost.Length == Network.Districts
              && BroadbandCatalogue.RivalHold.Length == InternetCatalogue.Providers.Length && Network.Own == InternetCatalogue.OwnIndex && Network.Providers == Network.Own + 1,
            "broadband: a figure for every district and a hold for every rival; the company comes after the rivals");
        int mix = 0;
        for (int k = 0; k < BroadbandCatalogue.Mbps.Length; k++)
        {
            mix += BroadbandCatalogue.TierMix[k];
            Check(k == 0 || (BroadbandCatalogue.Mbps[k] > BroadbandCatalogue.Mbps[k - 1] && BroadbandCatalogue.DefaultCents[k] > BroadbandCatalogue.DefaultCents[k - 1]), "broadband: a faster plan costs more");
        }
        Check(mix == 1000 && BroadbandCatalogue.TierMix.Length == BroadbandCatalogue.Mbps.Length && BroadbandCatalogue.DefaultCents.Length == BroadbandCatalogue.Mbps.Length,
            "broadband: the plans' mix of customers is the whole of them");
        for (int b = 1; b < BroadbandCatalogue.Marketing.Length; b++)
        {
            Check(BroadbandCatalogue.Marketing[b] > BroadbandCatalogue.Marketing[b - 1] && BroadbandCatalogue.Marketing[0] == 0, "broadband: the marketing budgets rise from none");
        }
        Check(BroadbandRules.BuildCost(Midtown, PriceRules.Most) == BroadbandCatalogue.BuildCost[Midtown] * 4 && BroadbandRules.BuildCost(Midtown, 1250) == 3000000
              && BroadbandRules.LicenceFee(PriceRules.Most) == 4000000 && BroadbandRules.BuildCost(9, Level) == 0,
            "broadband: millions at today's prices without overflowing, at the highest level too");
        Check(BroadbandRules.OutagePerMille(500) == 40 && BroadbandRules.OutagePerMille(1000) == 20 && BroadbandRules.OutagePerMille(1500) == 13
              && BroadbandRules.OutagePerMille(0) == 40 && BroadbandRules.OutagePerMille(99999) == 13,
            "broadband: less maintenance, more outages; within the bounds");

        // ---------------------------------------------------------------- the city's market before the company
        var n = new Network();
        BroadbandRules.Found(n, 1);
        bool whole = true;
        for (int d = 0; d < Network.Districts; d++)
        {
            int sum = 0;
            for (int p = 0; p < Network.Providers; p++)
            {
                sum += n.Share[d, p];
                whole &= n.Share[d, p] >= 0 && (InternetRules.Reaches(p, BroadbandCatalogue.Districts[d]) || n.Share[d, p] == 0);
            }
            whole &= sum == 1000 && n.Share[d, Network.Own] == 0;
        }
        Check(whole && n.Running && n.FoundedDay == 1, "broadband: each district's connections are all the rivals', where each reaches, and none the company's yet");
        Check(n.Share[Midtown, Kestrel] == 450 && n.Share[Midtown, Brightvein] == 300 && n.Share[Hamptons, Kestrel] == 0 && n.Share[Hamptons, 2] == 400 && n.Share[Hamptons, 3] == 600
              && n.Share[Garment, Brightvein] == 0 && n.Share[Garment, Kestrel] == 644,
            "broadband: the rivals' shares as their holds give them (the rounding to the largest)");
        int[] before = Shares(n, Midtown);
        BroadbandRules.Month(n, calm, 6, M);
        Check(Same(before, Shares(n, Midtown)) && BroadbandRules.Connections(n) == 0, "broadband: with no newcomer and the rivals at their lists, the market keeps still");
        var kestrelSale = new[] { 850, 1000, 1000, 1000 };
        BroadbandRules.Month(n, kestrelSale, 11, M);
        Check(n.Share[Midtown, Kestrel] > 450 && n.Share[Midtown, Brightvein] < 300 && n.Share[Hamptons, 2] == 400, "broadband: a rival's promotion wins it customers where it reaches, and only there");

        // ---------------------------------------------------------------- building
        n = new Network();
        BroadbandRules.Found(n, 1);
        int cost = BroadbandRules.BuildCost(Midtown, Level);
        BroadbandRules.StartBuild(n, Midtown, 3, M, cost);
        Check(n.Digging(Midtown) && n.ReadyDay(Midtown) == 13 && !n.Open(Midtown, 12) && BroadbandRules.Finish(n, 12) == 0 && BroadbandRules.Finish(n, 13) == 1
              && n.Open(Midtown, 13) && !n.Digging(Midtown) && n.Spent[Midtown] == cost && BroadbandRules.Finish(n, 14) == 0,
            "broadband: a district is dug for two of the game's months, then opens");
        Check(BroadbandRules.Trust(n, Midtown, 13, M) == 111 && BroadbandRules.Trust(n, Midtown, 13 + 4 * M, M) == 555 && BroadbandRules.Trust(n, Midtown, 13 + 8 * M, M) == 1000
              && BroadbandRules.Trust(n, Midtown, 13 + 30 * M, M) == 1000 && BroadbandRules.Trust(n, Hamptons, 50, M) == 0,
            "broadband: a district trusts a new network a ninth at first and fully after eight months; nowhere it is not open");
        BroadbandRules.Month(n, calm, 16, M);
        Check(n.Share[Midtown, Network.Own] > 0 && n.Share[Midtown, Network.Own] <= 30 && BroadbandRules.Connections(n) == n.Share[Midtown, Network.Own] * 30,
            "broadband: no customers in a day: the first month wins a few per cent at most");

        // ---------------------------------------------------------------- where the market settles, and what moves it
        int usual = Settled(Midtown, 1000, 1000, 0, out long usualTakings);
        int dear = Settled(Midtown, 2000, 1000, 0, out long dearTakings);
        int cheap = Settled(Midtown, 500, 1000, 0, out long cheapTakings);
        int hamptons = Settled(Hamptons, 1000, 1000, 0, out _);
        Check(Math.Abs(usual - 231) <= 15 && Math.Abs(hamptons - 545) <= 20, "broadband: a newcomer as trusted as Brightvein settles at about its share: a quarter of Midtown, half of the Hamptons, where fibre was missing");
        Check(dear < usual && usual < cheap && dearTakings < usualTakings, "broadband: dearer prices win fewer customers, and over the years take less");
        int none = Settled(Midtown, 1000, 1000, 0, out _), some = Settled(Midtown, 1000, 1000, 1, out _), most = Settled(Midtown, 1000, 1000, BroadbandCatalogue.Marketing.Length - 1, out _);
        int next = Settled(Midtown, 1000, 1000, BroadbandCatalogue.Marketing.Length - 2, out _);
        double first = (double)(some - none) / BroadbandCatalogue.Marketing[1];
        double last = (double)(most - next) / (BroadbandCatalogue.Marketing[BroadbandCatalogue.Marketing.Length - 1] - BroadbandCatalogue.Marketing[BroadbandCatalogue.Marketing.Length - 2]);
        Check(some > none && most > next && first > last * 5, "broadband: marketing wins customers, each dollar fewer than the last");
        int careless = Settled(Midtown, 1000, 500, 0, out _), careful = Settled(Midtown, 1000, 1500, 0, out _);
        Check(careless < usual && usual < careful, "broadband: a network that fails more keeps fewer customers");

        // ---------------------------------------------------------------- the books of a day and a month
        n = new Network();
        BroadbandRules.Found(n, 1);
        BroadbandRules.StartBuild(n, Midtown, 1, M, BroadbandRules.BuildCost(Midtown, Level));
        BroadbandRules.Finish(n, 11);
        n.Share[Midtown, Network.Own] = 200;   // 6,000 connections
        int conns = BroadbandRules.Connections(n), avg = BroadbandRules.AverageCents(n, Level);
        Check(conns == 6000 && avg == 6950 && BroadbandRules.DayTakings(n, Level, M, false, true) == 6000 * 6950 / 5 / 100
              && BroadbandRules.DayTakings(n, Level, M, true, true) == 0 && BroadbandRules.DayTakings(n, Level, M, false, false) == 0,
            "broadband: a day's takings are every connection's share of its month; none on a day the network failed, nor without a headquarters to bill from");
        BroadbandRules.DayCosts(n, Level, 11, M, out int staff, out int net, out int ads);
        int upkeep = BroadbandRules.BuildCost(Midtown, Level) * BroadbandCatalogue.UpkeepPerMille / 1000;
        Check(staff == 6000 * 1200 / 5 / 100 && net == (6000 * 1800 / 5 + upkeep * 100 / 5) / 100 && ads == 0, "broadband: a day's staff and network for every connection, and the upkeep of what is open");
        n.Maintenance = 500;
        n.Marketing = 2;
        BroadbandRules.DayCosts(n, Level, 11, M, out _, out int lean, out ads);
        BroadbandRules.DayCosts(n, Level, 10, M, out _, out int unopened, out _);
        Check(lean == (6000 * 1800 / 5 + upkeep / 2 * 100 / 5) / 100 && ads == BroadbandCatalogue.Marketing[2] / 5 && unopened == 6000 * 1800 / 5 / 100,
            "broadband: half the maintenance, half the upkeep; marketing a day's share of its budget; no upkeep before a district opens");
        Check(BroadbandRules.Installs(100, 350, Level) == 250 * 120 && BroadbandRules.Installs(350, 100, Level) == 0 && BroadbandRules.Installs(0, 10, 2000) == 10 * 240,
            "broadband: each new connection an engineer's visit and a box, at today's prices; none for those that leave");
        var sold = new Network();
        BroadbandRules.Found(sold, 1);
        sold.State = CompanyState.Sold;
        BroadbandRules.DayCosts(sold, Level, 11, M, out int s1, out int s2, out int s3);
        Check(BroadbandRules.DayTakings(sold, Level, M, false, true) == 0 && s1 + s2 + s3 == 0, "broadband: a company sold neither takes nor spends anything of the player's");

        // ---------------------------------------------------------------- months recorded, and the sale
        n = new Network();
        BroadbandRules.Found(n, 1);
        n.Spent[Midtown] = 2000000;
        Check(BroadbandRules.SaleValue(n) == 600000, "broadband: a company that has made nothing sells for what its network is worth to a buyer: three tenths of its cost");
        for (int month = 1; month <= 15; month++)
        {
            n.MonthTakings = 100000;
            n.MonthCosts = 100000 - month * 1000;
            BroadbandRules.CloseMonth(n, 1000 + month, 1);
        }
        long year = 0;
        for (int month = 4; month <= 15; month++)
        {
            year += month * 1000;
        }
        Check(n.HistoryCount == 12 && BroadbandRules.YearProfit(n) == year && n.History[11] == 15000 && n.History[0] == 4000 && n.MonthTakings == 0 && n.LastConnections == 1015,
            "broadband: the last twelve months' profit is kept, the oldest dropped");
        Check(BroadbandRules.SaleValue(n) == Math.Max(year * 4, 600000), "broadband: a buyer pays four years of the last year's profit");
        long honest = BroadbandRules.SaleValue(n);
        n.MonthTakings = 100000;
        n.MonthCosts = 100000 - 16000 - 50000;   // a month squeezed: no marketing, no maintenance
        BroadbandRules.CloseMonth(n, 1016, 1);
        Check(BroadbandRules.SaleValue(n) - honest <= 4L * (66000 - 4000), "broadband: one squeezed month adds four times its gain at most, never a year's worth");

        // ---------------------------------------------------------------- the rivals answer
        n = new Network();
        BroadbandRules.Found(n, 1);
        Check(BroadbandRules.AnswerCap(n, Kestrel) == InternetCatalogue.PromoMost && BroadbandRules.AnswerCap(n, Network.Own) == InternetCatalogue.PromoMost
              && BroadbandRules.AnswerCap(null, Kestrel) == InternetCatalogue.PromoMost,
            "broadband: a rival not yet answered prices as it likes");
        n.Share[Midtown, Network.Own] = 500;   // half of Midtown: 15,000 of Kestrel's 122,000
        int cap = BroadbandRules.AnswerCap(n, Kestrel);
        Check(cap == InternetCatalogue.PromoMost - 15000 * 1000 / 122000 && BroadbandRules.AnswerCap(n, Brightvein) == InternetCatalogue.PromoMost - 15000 * 1000 / 75000,
            "broadband: a rival keeps its price down by the share of its markets the company has taken");
        for (int d = 0; d < Network.Districts; d++)
        {
            n.Share[d, Network.Own] = 1000;
        }
        Check(BroadbandRules.AnswerCap(n, Kestrel) == InternetCatalogue.PromoLeast, "broadband: never under a rival's floor");

        // ---------------------------------------------------------------- the company as a provider
        BroadbandRules.Apply(null, 0);
        Check(InternetRules.Count == InternetCatalogue.Providers.Length && InternetRules.Get(InternetCatalogue.OwnIndex) == null && !InternetRules.Reaches(InternetCatalogue.OwnIndex, "ba:neighborhood_midtown"),
            "broadband: with no company there are only the rivals");
        n = new Network();
        BroadbandRules.Found(n, 1);
        BroadbandRules.StartBuild(n, Midtown, 1, M, 1);
        BroadbandRules.Apply(n, 5);
        Check(InternetRules.Count == InternetCatalogue.Providers.Length, "broadband: a network still being dug reaches nobody");
        BroadbandRules.Finish(n, 11);
        BroadbandRules.Apply(n, 11);
        Check(InternetRules.Count == InternetCatalogue.Providers.Length + 1 && InternetRules.Get(InternetCatalogue.OwnIndex) == InternetCatalogue.Own
              && InternetRules.Reaches(InternetCatalogue.OwnIndex, "ba:neighborhood_midtown") && !InternetRules.Reaches(InternetCatalogue.OwnIndex, "ba:neighborhood_hellskitchen")
              && InternetRules.MonthCents(InternetCatalogue.OwnIndex, 2, true, false, 1500, 1000) == 0 && InternetCatalogue.Own.NameKey == BroadbandCatalogue.OwnNameKey,
            "broadband: the company is a provider where its network is open, and connects the player's own places free");
        n.Maintenance = 500;
        BroadbandRules.Apply(n, 11);
        int failed = 0;
        for (int d = 1; d <= 20000; d++)
        {
            failed += InternetRules.Outage(777, d, InternetCatalogue.OwnIndex, false, out _, out _) ? 1 : 0;
        }
        n.Maintenance = 1500;
        BroadbandRules.Apply(n, 11);
        int kept = 0;
        for (int d = 1; d <= 20000; d++)
        {
            kept += InternetRules.Outage(777, d, InternetCatalogue.OwnIndex, false, out _, out _) ? 1 : 0;
        }
        Check(Math.Abs(failed - 800) < 120 && Math.Abs(kept - 260) < 70, "broadband: the network fails as often as its maintenance says: 40 days in a thousand on half, 13 on half as much again");
        n.State = CompanyState.Sold;
        BroadbandRules.Apply(n, 11);
        Check(InternetRules.MonthCents(InternetCatalogue.OwnIndex, 0, false, true, 1000, 1000) == BroadbandCatalogue.DefaultCents[0] && InternetCatalogue.Own.NameKey == BroadbandCatalogue.SoldNameKey
              && InternetCatalogue.Own.OutagePerMille == BroadbandCatalogue.OutageBase,
            "broadband: sold, the network is its buyer's, at the usual prices and reliability");

        // ---------------------------------------------------------------- the save
        var state = new UtilitiesState { LastDay = 40, MonthStart = 36, Seed = 99, Month = 7 };
        Network w = state.Network;
        BroadbandRules.Found(w, 3);
        BroadbandRules.StartBuild(w, Midtown, 3, M, 2400000);
        w.Built[Hamptons] = 20;
        w.Spent[Hamptons] = 1200000;
        w.Share[Hamptons, Network.Own] = 120;
        w.Share[Hamptons, 3] -= 120;
        w.PriceShare = 850;
        w.Maintenance = 1200;
        w.Marketing = 3;
        w.MonthTakings = 12345;
        w.MonthCosts = 2345;
        w.LastGained = -40;
        BroadbandRules.CloseMonth(w, 360, 12);
        state.Accounts.Add(new RealLifeOverhaul.Utilities.Account { Place = "Fifth%20Avenue~4", Service = Service.Internet, Provider = InternetCatalogue.OwnIndex, State = SupplyState.Active });
        string line = UtilitiesStore.Write(state);
        UtilitiesState back = UtilitiesStore.Read(line, out int version);
        Network r = back != null ? back.Network : null;
        Check(version == UtilitiesStore.Version && r != null && r.State == CompanyState.Running && r.FoundedDay == 3 && r.Built[Midtown] == -13 && r.Spent[Midtown] == 2400000
              && r.Built[Hamptons] == 20 && r.Share[Hamptons, Network.Own] == 120 && r.Share[Hamptons, 3] == w.Share[Hamptons, 3] && r.PriceShare == 850 && r.Maintenance == 1200 && r.Marketing == 3
              && r.HistoryCount == 1 && r.History[0] == 10000 && r.LastTakings == 12345 && r.LastGained == 12 && back.Accounts.Count == 1 && back.Accounts[0].Provider == InternetCatalogue.OwnIndex,
            "broadband: the company is kept in the save as it was, and a place connected to it stays so");
        UtilitiesState old = UtilitiesStore.Read("v=1~h:40|36|99|7|1000|1000|1000|1000~a:", out _);
        Check(old != null && !old.Network.Exists && UtilitiesStore.Write(old).IndexOf("~n:", StringComparison.Ordinal) < 0, "broadband: a line from before the company has none, and writes none");
        UtilitiesState odd = UtilitiesStore.Read("v=2~h:1|1|1|0~n:1|1|0|0|99999|1|99|0|0|0|0|0|0|77", out _);
        Check(odd != null && odd.Network.PriceShare == BroadbandCatalogue.PriceMost && odd.Network.Maintenance == BroadbandCatalogue.MaintenanceLeast
              && odd.Network.Marketing == BroadbandCatalogue.Marketing.Length - 1 && odd.Network.HistoryCount == BroadbandCatalogue.HistoryMonths,
            "broadband: figures out of range are brought within it");

        // ---------------------------------------------------------------- what it is worth: very costly, and paid back in good time
        var midtown = new[] { Midtown };
        var city = new[] { 0, 1, 2, 3, 4, 5, 6 };
        int payback = BroadbandPayback(midtown, 0, false, out int share);
        int withLicence = BroadbandPayback(midtown, 0, true, out _);
        Check(payback >= 18 && payback <= 30 && withLicence > payback && withLicence <= 36 && Math.Abs(share - 231) <= 15,
            "broadband: very costly: a district pays back its network in about two years (with its two months of digging), the licence besides within three");
        int plain = BroadbandPayback(city, 0, true, out _), advertised = BroadbandPayback(city, 3, true, out _), small = BroadbandPayback(midtown, 1, false, out _);
        Check(advertised <= plain && Math.Abs(small - payback) <= 1, "broadband: a marketing budget pays its way across the city, and costs a single district about what it brings");

        BroadbandRules.Apply(null, 0);   // (the rest of the checks have the city's internet as it begins)
    }

    // A district's share of the company after six years at prices, maintenance and a marketing budget.
    private static int Settled(int district, int priceShare, int maintenance, int budget, out long takings)
    {
        var n = new Network();
        BroadbandRules.Found(n, 1);
        n.PriceShare = priceShare;
        n.Maintenance = maintenance;
        n.Marketing = budget;
        BroadbandRules.StartBuild(n, district, 1, 5, 1);
        BroadbandRules.Finish(n, 11);
        var calm = new[] { 1000, 1000, 1000, 1000 };
        takings = 0;
        for (int month = 0; month < 72; month++)
        {
            BroadbandRules.Month(n, calm, 11 + month * 5, 5);
            if (month >= 60)
            {
                takings += (long)BroadbandRules.Connections(n) * BroadbandRules.AverageCents(n, 1000);
            }
        }
        return n.Share[district, Network.Own];
    }

    // Districts built from founding (day 1 of five-day months), the books day by day as the director keeps them: the month
    // in which the cash is all back (the networks, and the licence if asked), and the first district's share after five years.
    private static int BroadbandPayback(int[] districts, int budget, bool licence, out int share)
    {
        const int M = 5, Level = 1000;
        var n = new Network();
        BroadbandRules.Found(n, 1);
        n.Marketing = budget;
        long cash = licence ? -BroadbandRules.LicenceFee(Level) : 0;
        foreach (int district in districts)
        {
            int cost = BroadbandRules.BuildCost(district, Level);
            BroadbandRules.StartBuild(n, district, 1, M, cost);
            cash -= cost;
        }
        var calm = new[] { 1000, 1000, 1000, 1000 };
        int payback = -1;
        for (int day = 1; day <= 60 * M; day++)
        {
            BroadbandRules.Finish(n, day);
            int takings = BroadbandRules.DayTakings(n, Level, M, false, true);
            BroadbandRules.DayCosts(n, Level, day, M, out int staff, out int network, out int marketing);
            n.MonthTakings += takings;
            n.MonthCosts += staff + network + marketing;
            cash += takings - staff - network - marketing;
            if (day % M == 0)
            {
                int was = BroadbandRules.Connections(n);
                BroadbandRules.Month(n, calm, day + 1, M);
                int now = BroadbandRules.Connections(n);
                int installs = BroadbandRules.Installs(was, now, Level);
                n.MonthCosts += installs;
                cash -= installs;
                BroadbandRules.CloseMonth(n, now, now - was);
                if (payback < 0 && cash >= 0)
                {
                    payback = day / M;
                }
            }
        }
        share = n.Share[districts[0], Network.Own];
        return payback;
    }

    private static int[] Shares(Network n, int district)
    {
        var shares = new int[Network.Providers];
        for (int p = 0; p < Network.Providers; p++)
        {
            shares[p] = n.Share[district, p];
        }
        return shares;
    }

    private static bool Same(int[] a, int[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }
}
