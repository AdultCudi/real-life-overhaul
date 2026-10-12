using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Utilities;

// The city's internet (Scripts/Utilities): reach, prices, a plan bought, the exit fee, the day's share, outages drawn
// from the save's seed, the month's price, and the save.
internal static partial class Program
{
    private static void Internet()
    {
        const int Kestrel = 0, Brightvein = 1, Aerowave = 2;
        Check(InternetRules.Reaches(Kestrel, "ba:neighborhood_midtown") && !InternetRules.Reaches(Kestrel, "ba:neighborhood_thehamptons")
              && InternetRules.Reaches(Aerowave, "ba:neighborhood_thehamptons") && !InternetRules.Reaches(Brightvein, "ba:neighborhood_industrycity")
              && !InternetRules.Reaches(9, "ba:neighborhood_midtown") && !InternetRules.Reaches(Kestrel, null),
            "internet: each provider reaches its own districts (fibre only in three)");
        foreach (Provider p in InternetCatalogue.Providers)
        {
            Check(p.Mbps.Length == p.MonthCents.Length && p.Mbps.Length > 0 && p.OutagePerMille > 0 && p.OutagePerMille < 1000, "internet: " + p.Id + " has plans, prices and a reliability");
            for (int i = 1; i < p.Mbps.Length; i++)
            {
                Check(p.Mbps[i] > p.Mbps[i - 1] && p.MonthCents[i] > p.MonthCents[i - 1], "internet: " + p.Id + "'s faster plans cost more");
            }
        }

        // ---------------------------------------------------------------- prices
        Check(InternetRules.MonthCents(Kestrel, 0, false, true, 1000, 1000) == 4500 && InternetRules.MonthCents(Kestrel, 0, false, false, 1000, 1000) == 5400
              && InternetRules.MonthCents(Kestrel, 0, true, true, 1000, 1000) == 8100 && InternetRules.MonthCents(Kestrel, 0, false, true, 1500, 1000) == 6750
              && InternetRules.MonthCents(Kestrel, 0, false, true, 1000, 800) == 3600 && InternetRules.MonthCents(Kestrel, 9, false, true, 1000, 5000) == 8500 * 1100 / 1000,
            "internet: a business plan costs more, month by month costs more, at today's prices and the provider's month, within bounds");

        // ---------------------------------------------------------------- a plan bought
        var a = new RealLifeOverhaul.Utilities.Account { Place = "Main%20Street~12", Owed = 0 };
        int price = InternetRules.MonthCents(Brightvein, 1, true, true, 1000, 1000);
        InternetRules.SignUp(a, Brightvein, 1, true, true, 20, 5, price);
        Check(a.Service == Service.Internet && a.State == SupplyState.Pending && a.ConnectDay == 22 && a.ContractUntil == 20 + 12 * 5 && a.PriceCents == price
              && a.BusinessPlan && a.Provider == Brightvein && !UtilitiesRules.Supplied(a, 21) && UtilitiesRules.Turn(a, 22) && a.State == SupplyState.Active,
            "internet: bought online, installed two days later, its price held for a contract of twelve months");
        Check(InternetRules.ExitFee(a, 22, 5) == UtilitiesRules.Money(12L * price * 500 / 1000) && InternetRules.ExitFee(a, 75, 5) == UtilitiesRules.Money(1L * price * 500 / 1000)
              && InternetRules.ExitFee(a, 80, 5) == 0,
            "internet: leaving early costs half of each month left, a month begun counting; nothing once out of contract");
        int day = InternetRules.DayCents(a, 5);
        InternetRules.AddDay(a, 5, false);
        InternetRules.AddDay(a, 5, true);
        Check(day == (price + 2) / 5 && a.Accrued == day, "internet: a day's share of the month, and none on a day the provider failed (its service credit)");
        Check(UtilitiesRules.Bill(a, 25, 5) == UtilitiesRules.Money(day) && a.Owed > 0, "internet: billed with power and water");

        // ---------------------------------------------------------------- outages: as often as the provider's reliability says, and the same on a reload
        int outages = 0, firsts = 0;
        bool inHours = true, halved = true;
        for (int d = 1; d <= 20000; d++)
        {
            if (InternetRules.Outage(12345, d, Aerowave, false, out int from, out int hours))
            {
                outages++;
                inHours &= from >= InternetCatalogue.OutageFirstHour && from <= InternetCatalogue.OutageLastHour
                           && hours >= InternetCatalogue.OutageLeastHours && hours <= InternetCatalogue.OutageMostHours;
                InternetRules.Outage(12345, d, Aerowave, true, out _, out int business);
                halved &= business == Math.Max(1, (hours + 1) / 2);
                if (outages == 1)
                {
                    InternetRules.Outage(12345, d, Aerowave, false, out int again, out int againHours);
                    firsts = again == from && againHours == hours ? 1 : 0;
                }
            }
        }
        Check(inHours && halved, "internet: an outage falls in the day's hours and lasts two to eight, half as long on a business plan");
        Check(outages > 20000 * 60 / 1000 * 8 / 10 && outages < 20000 * 60 / 1000 * 12 / 10 && firsts == 1,
            "internet: outages come about as often as the reliability says (" + outages + " in 20,000 days), the same each time they are drawn");

        // ---------------------------------------------------------------- the month's price
        int promo = 1000, least = 1000, most = 1000;
        for (int m = 0; m < 600; m++)
        {
            promo = InternetRules.NextPromo(777, m, Kestrel, promo);
            least = Math.Min(least, promo);
            most = Math.Max(most, promo);
        }
        Check(least >= InternetCatalogue.PromoLeast && most <= InternetCatalogue.PromoMost && least < 1000 && most > 1000 && InternetRules.NextPromo(777, 3, Kestrel, 1000) == InternetRules.NextPromo(777, 3, Kestrel, 1000),
            "internet: a provider's price walks within its bounds, both ways, the same for the same month");

        // ---------------------------------------------------------------- the save
        var s = new UtilitiesState { LastDay = 30, MonthStart = 26, Seed = -42, Month = 7 };
        s.Promo[2] = 860;
        a.OutageFrom = 30 * 24 + 9;
        a.OutageUntil = 30 * 24 + 13;
        s.Accounts.Add(a);
        int version;
        UtilitiesState r = UtilitiesStore.Read(UtilitiesStore.Write(s), out version);
        RealLifeOverhaul.Utilities.Account b = r != null ? r.Find("Main%20Street~12", Service.Internet) : null;
        Check(r != null && r.Seed == -42 && r.Month == 7 && r.Promo[2] == 860 && r.Promo[0] == 1000 && b != null && b.Provider == Brightvein && b.Plan == 1 && b.BusinessPlan
              && b.ContractUntil == a.ContractUntil && b.PriceCents == price && b.OutageFrom == a.OutageFrom && b.OutageUntil == a.OutageUntil
              && InternetRules.InOutage(b, 30 * 24 + 12) && !InternetRules.InOutage(b, 30 * 24 + 13) && UtilitiesStore.Write(r) == UtilitiesStore.Write(s),
            "internet: the save round-trips exactly, the seed, the prices and the connection too");
        UtilitiesState old = UtilitiesStore.Read("v=1~h:5|3~a:" + UtilitiesStore.Escape("A~1") + "|0|0|1|0|1|0|0|0|0|0|0|0|0|0|0|0|0|0", out version);
        Check(old != null && old.Promo[0] == 1000 && old.Accounts.Count == 1 && old.Accounts[0].Provider == -1, "internet: a line from before the internet reads with no connection and list prices");
    }
}
