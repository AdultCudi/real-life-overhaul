using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;

// Brands and franchises fixed, each reproduced here:
//   1. a renewal could not be answered on the day it was dated: the agreement lapsed that morning;
//   2. a converted shop whose start day the save never knew was given a start day of -1 when its agreement ended;
//   3. the Guide gave support, the idle fund, the inspector and retraining at today's prices on a page at an index of 100;
//   4. "Paid yesterday" showed a chain shop's weekly average beside the franchises' yesterday;
//   5. a shop that joined a brand or chain kept the prices last written for its old one, so a price set in between
//      was taken for one changed by something else;
//   6. the test tool's "put brands back" left a converted test shop with the start day the daily keep last wrote;
//   7. an inspection counted as evidence for eleven days where the texts say the last ten.
internal static partial class Program
{
    private static void BrandFixes()
    {
        BrandFixRenewal();
        BrandFixBorn();
        BrandFixChainYesterday();
        BrandFixEvidence();
        BrandFixSources();
    }

    // The renewal is dated the agreement's last day (the ask, the notice, the Planner's row): it can be answered all that day.
    private static void BrandFixRenewal()
    {
        Brand b;
        BrandsState s = NewBrands(3, out b);
        Franchise f = Signed(s, b, Temper.Careful, 10);
        f.Health = 500000;
        int until = f.Until, lapsed = 0;
        bool open = false;
        for (int day = 11; day <= until + 2 && lapsed == 0; day++)
        {
            FranchiseDay x = FranchiseRules.Day(s, b, f, day, 100, CycleState.Steady, 1000);
            lapsed = x.Event == DayEvent.Lapsed ? day : 0;
            open |= day == until && x.Event == DayEvent.None && f.Ask == Ask.Renewal && f.AskUntil == until;
        }
        Check(open, "brand fix: on the agreement's last day the renewal still waits for an answer, dated that day");
        Check(lapsed == until + 1, "brand fix: unanswered, the agreement lapses the morning after its last day (day " + lapsed + ", its last " + until + ")");

        Franchise g = Signed(s, b, Temper.Careful, 10);
        g.Health = 500000;
        int last = g.Until;
        bool kept = true;
        for (int day = 11; day <= last; day++)
        {
            kept &= FranchiseRules.Day(s, b, g, day, 100, CycleState.Steady, 1000).Event != DayEvent.Lapsed;
        }
        FranchiseRules.Renew(g);
        FranchiseDay next = FranchiseRules.Day(s, b, g, last + 1, 100, CycleState.Steady, 1000);
        Check(kept && next.Event != DayEvent.Lapsed && g.Until == last + g.Term, "brand fix: a renewal answered on the last day keeps the shop in the brand");
    }

    private static void BrandFixBorn()
    {
        Check(FranchiseRules.BornBack(new Franchise { Hold = Hold.Converted, Born = 37 }) == 37 && FranchiseRules.BornBack(new Franchise { Hold = Hold.Opened, Born = 37 }) == -1,
            "brand fix: a converted shop is given back its own start day; an opened one is closed, not given one");
        int version;
        BrandsState old = BrandStore.Read("v=1^h:5|1|9|8^b:1|Kettle|type|Main~1^f:2|1|Harbour Lane Trading|0|60|d|p|g|1|0|0|0|150|1|61|60|15000|600|100|0", out version);
        Check(old != null && old.Franchises[0].Hold == Hold.Converted && FranchiseRules.BornBack(old.Franchises[0]) == -1,
            "brand fix: a converted shop from a line without its start day is given none back, never a start day of -1");
    }

    private static void BrandFixChainYesterday()
    {
        var t = new Taken { Place = "Main~9", Chain = 1, Average = 500 };
        int paid = FranchiseRules.ChainRoyalty(t, 20000);
        Check(paid == 1200 && t.Yesterday == paid && t.Average != paid, "brand fix: a chain shop's royalty of the day is kept as yesterday's, beside the week's average");
        var s = new BrandsState { Seed = 1, FirstDay = 1, LastDay = 2 };
        s.Taken.Add(new Taken { Place = "Main~9", Chain = 2, From = 1, Until = 300, Average = 640, Paid = 9000, Yesterday = 712 });
        int version;
        BrandsState back = BrandStore.Read(BrandStore.Write(s), out version);
        BrandsState old = BrandStore.Read("v=1^h:5|1|9|8^t:Main~9|2|1|300|0|640|9000|1|0", out version);
        Check(back != null && back.Taken[0].Yesterday == 712 && old != null && old.Taken[0].Yesterday == 640 && old.Taken[0].Average == 640,
            "brand fix: yesterday's chain royalty goes through the save; a line from before shows its average until the next morning");
    }

    private static void BrandFixEvidence()
    {
        Brand b;
        NewBrands(1, out b);
        b.Minimum = 60;
        var f = new Franchise { Seen = 40, SeenDay = 100 };
        Check(FranchiseRules.Evidence(b, f, 100) && FranchiseRules.Evidence(b, f, 100 + BrandCatalogue.FreshDays - 1) && !FranchiseRules.Evidence(b, f, 100 + BrandCatalogue.FreshDays),
            "brand fix: an inspection is evidence on its own day and the nine after it, the last ten days the texts name");
    }

    private static void BrandFixSources()
    {
        string guide = EconomySource(@"Brands\BrandsGuide.cs"), deals = EconomySource(@"Brands\BrandsApp.Deals.cs"), shops = EconomySource(@"Brands\BrandsApp.Shops.cs");
        string actions = EconomySource(@"Brands\BrandsDirector.Actions.cs"), tests = EconomySource(@"Brands\BrandsDirector.Tests.cs"), game = EconomySource(@"Brands\BrandsGame.Franchises.cs");
        Check(guide.Length > 0 && !guide.Contains("Prices.Level") && Occurs(guide, "PriceRules.Start") == 8, "brand fix: every sum on the Guide's page is at a price index of 100, as the page says");
        string close = MemberText(game, "public static void Close(");
        Check(close.Contains("int born = FranchiseRules.BornBack(f);") && close.Contains("born >= 0") && !close.Contains("creationDay = f.Born"),
            "brand fix: a converted shop's start day is given back only when the save knows it");
        Check(Occurs(deals, "-t.Yesterday") == 2 && Occurs(shops, "-t.Yesterday") == 2 && !deals.Contains("-t.Average") && !shops.Contains("-t.Average")
              && shops.Contains("BrandsText.Money(t.Average), \"reallife_brand_col_paid_all\", BrandsText.Money(t.Paid)"),
            "brand fix: the Agreements and Shops tabs show what a chain shop paid yesterday, and its page names what it paid in all as that");
        bool fresh = true;
        foreach (string action in new[] { "public void Join(", "public void Leave(", "public void TakeChain(", "public void LeaveChain(" })
        {
            fresh &= MemberText(actions, action).Contains("_books.Remove(place)");
        }
        Check(fresh, "brand fix: a shop that joins or leaves a brand or a chain starts its kept prices afresh");
        Check(MemberText(tests, "public void TestBack(").Contains("Hold.Converted"), "brand fix: putting brands back gives a converted test shop its own start day again");
    }
}
