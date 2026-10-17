using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

// Ventures fixed, each reproduced here:
//   1. a ninth deal was dropped at the next save while the player kept the investor's money: the cap is kept when an
//      offer is taken up, a promise is made and a promise opens;
//   2. one new business paid out two promises of the same kind, the second on a business the player already had;
//   4. the investors' sums, the first target, the budget steps and the pitch's strongest figures stayed at the start's prices;
//   5. a business opened in the last hour of a promise's last day was not found before the promise lapsed;
//   6. asking for more money from an investor already at their largest sum was "agreed" and spent the counter;
//   7. the buy-out of a business that is gone was priced at 110% of the sum, while the next day called only what was unpaid;
//   8. the test tools took a real offer, called a real deal back and put back real changes made between tests;
//   9. with no court, a called sum's missed days counted on while the notice said they would go to court;
//   10. the Raise money page found the business clicked by its place in a list read afresh;
//   11. a staff count that could not be read was asked about as 0.
//   (3, the Investors option switched off stopping every deal's day, waits for a decision.)
internal static partial class Program
{
    private static void VenturesFixes()
    {
        VenturesFixCap();
        VenturesFixTwoPromises();
        VenturesFixPrices();
        VenturesFixCounter();
        VenturesFixGone();
        VenturesFixTests();
        VenturesFixNoCourt();
        VenturesFixStaff();
        VenturesFixSources();
    }

    private static VenturesFile VenturesFixFile(int deals, Investor investor)
    {
        VenturesFile file = NewVentures(4242);
        for (int i = 0; i < deals; i++)
        {
            file.Deals.Add(new Deal
            {
                Id = file.NextId++, InvestorId = investor != null ? investor.Id : file.Investors[0].Id, Place = "d#" + i, Business = "D" + i, Amount = 10000, ShareBp = 1000,
                StartDay = 1, Mood = VenturesCatalogue.MoodStart, Stage = DealStage.Running
            });
        }
        return file;
    }

    private static void VenturesFixCap()
    {
        VenturesFile file = VenturesFixFile(7, null);
        Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient);
        Investor other = Find(file, InvestorKind.Tycoon, Temperament.Demanding);
        var a = new Offer { Id = file.NextId++, InvestorId = patient.Id, Place = "x#1", Business = "X", Amount = 30000, ShareBp = 2000, TargetPct = 10, TargetDays = 30 };
        var b = new Offer { Id = file.NextId++, InvestorId = other.Id, Place = "y#1", Business = "Y", Amount = 30000, ShareBp = 2000, TargetPct = 10, TargetDays = 30 };
        file.Offers.Add(a);
        file.Offers.Add(b);
        long invested = file.InvestedTotal;
        Deal eighth = OfferRules.Accept(file, a, Steady(5000f), 20), ninth = OfferRules.Accept(file, b, Steady(5000f), 20);
        Check(eighth != null && ninth == null && file.Deals.Count == VenturesCatalogue.DealsMost && file.Offers.Contains(b) && file.InvestedTotal == invested + 30000,
            "ventures fix: with seven deals, two offers taken up make the eighth deal and refuse the ninth, whose offer stands");
        VenturesFile back = VenturesStore.Read(VenturesStore.Write(file), out _);
        Check(back != null && back.Deals.Count == VenturesCatalogue.DealsMost, "ventures fix: every deal taken up is in the save");
        var round = new Offer { Id = file.NextId++, InvestorId = patient.Id, Place = "x#1", Business = "X", Amount = 10000, ShareBp = 1000, TargetPct = 10, TargetDays = 30 };
        file.Offers.Add(round);
        Check(OfferRules.Accept(file, round, Steady(5000f), 21) != null && file.Deals.Count == VenturesCatalogue.DealsMost, "ventures fix: a second round on a deal there is still fits a full file");
        var crowded = new Offer { Id = file.NextId++, InvestorId = other.Id, Place = "x#1", Business = "X", Amount = 10000, ShareBp = 3500, TargetPct = 10, TargetDays = 30 };
        file.Deals.RemoveAt(0);
        file.Offers.Add(crowded);
        Check(OfferRules.Accept(file, crowded, Steady(5000f), 22) == null, "ventures fix: an offer that would take a business over six tenths of its profit is refused when taken up");

        VenturesFile full = VenturesFixFile(8, null);
        var forNew = new Offer { Id = full.NextId++, InvestorId = full.Investors[0].Id, TypeName = "ba:businesstype_coffeeshop", Budget = 50000, Amount = 20000, ShareBp = 2000, TargetPct = 10, TargetDays = 30 };
        full.Offers.Add(forNew);
        Check(OfferRules.Promise(full, forNew, 20, 0) == null && full.Offers.Contains(forNew), "ventures fix: no promise of money is made while the deals are full");
        VenturesFile seven = VenturesFixFile(7, null);
        var p1 = new Offer { Id = seven.NextId++, InvestorId = seven.Investors[0].Id, TypeName = "ba:businesstype_coffeeshop", Budget = 50000, Amount = 20000, ShareBp = 2000, TargetPct = 10, TargetDays = 30 };
        var p2 = new Offer { Id = seven.NextId++, InvestorId = seven.Investors[1].Id, TypeName = "ba:businesstype_coffeeshop", Budget = 50000, Amount = 20000, ShareBp = 2000, TargetPct = 10, TargetDays = 30 };
        seven.Offers.Add(p1);
        seven.Offers.Add(p2);
        Check(OfferRules.Promise(seven, p1, 20, 0) != null && OfferRules.Promise(seven, p2, 20, 0) == null, "ventures fix: a promise holds a deal's place, so promises and deals stay within eight");
        Pledge waiting = seven.Pledges[0];
        seven.Deals.Add(new Deal { Id = seven.NextId++, InvestorId = seven.Investors[2].Id, Place = "z#9", Business = "Z", Amount = 1000, ShareBp = 100, StartDay = 1, Stage = DealStage.Running });
        Check(OfferRules.Open(seven, waiting, "c#1", "Cafe", 25) == null && seven.Pledges.Contains(waiting) && seven.Deals.Count == VenturesCatalogue.DealsMost,
            "ventures fix: a promise does not open a ninth deal; it waits");
    }

    private static void VenturesFixTwoPromises()
    {
        VenturesFile file = NewVentures(99);
        const string coffee = "ba:businesstype_coffeeshop";
        file.Pledges.Add(new Pledge { Id = file.NextId++, InvestorId = file.Investors[0].Id, TypeName = coffee, Amount = 20000, ShareBp = 2000, TargetPct = 10, TargetDays = 30, MadeDay = 10, OpenBy = 24, Had = 1 });
        file.Pledges.Add(new Pledge { Id = file.NextId++, InvestorId = file.Investors[1].Id, TypeName = coffee, Amount = 20000, ShareBp = 2000, TargetPct = 10, TargetDays = 30, MadeDay = 10, OpenBy = 24, Had = 1 });
        file.Pledges.Add(new Pledge { Id = file.NextId++, InvestorId = file.Investors[2].Id, TypeName = "ba:businesstype_giftshop", Amount = 20000, ShareBp = 2000, TargetPct = 10, TargetDays = 30, MadeDay = 10, OpenBy = 24, Had = 0 });
        Deal opened = OfferRules.Open(file, file.Pledges[0], "c#2", "Second Cafe", 15);
        Check(opened != null && file.Pledges.Count == 2 && file.Pledges[0].Had == 2 && file.Pledges[1].Had == 0,
            "ventures fix: a new business pays one promise of its kind; the other waits for another new one");
    }

    private static void VenturesFixPrices()
    {
        System.Func<int> was = Prices.Factor;
        try
        {
            Prices.Factor = () => 1200;
            VenturesFile file = NewVentures(31337);
            Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient);
            Check(patient.Least == PriceRules.At(patient.ChequeLeast, 1200) && patient.Most == PriceRules.At(patient.ChequeMost, 1200) && patient.Most > patient.ChequeMost,
                "ventures fix: an investor's sums are at today's prices, the save's at the start's");
            Check(OfferRules.YoungTarget == 360 && VenturesCatalogue.BudgetAt(3) == PriceRules.At(VenturesCatalogue.BudgetSteps[3], 1200) && VenturesCatalogue.BudgetAt(99) == VenturesCatalogue.BudgetAt(8),
                "ventures fix: the first target and the budget steps are at today's prices");
            OfferRules.Terms(file, patient, "big#1", 400000f, 60, 2000, 20, out int big, out _);
            Check(big == patient.Most, "ventures fix: a sum over the largest is cut to it at today's prices (" + big + ")");
            Check(Pitch.FactPoints(Fact.SalesYesterday, 4000) < VenturesCatalogue.PointsMost && Pitch.FactPoints(Fact.SalesYesterday, 4800) == VenturesCatalogue.PointsMost,
                "ventures fix: a figure is as strong as it gets at today's prices, not the start's");
        }
        finally
        {
            Prices.Factor = was;
        }
        Check(Pitch.FactPoints(Fact.SalesYesterday, 4000) == VenturesCatalogue.PointsMost && OfferRules.YoungTarget == 300, "ventures fix: at the start's prices nothing moves");
    }

    private static void VenturesFixCounter()
    {
        VenturesFile file = NewVentures(31337);
        Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient);
        var top = new Offer { Id = 77, InvestorId = patient.Id, Place = "p", Amount = patient.Most, ShareBp = 1500 };
        Check(!OfferRules.MoreMoney(top, patient) && OfferRules.Counter(file, top, patient, Steady(3000f, 14), true) == CounterAnswer.Refused && !top.Countered && top.Amount == patient.Most,
            "ventures fix: more money cannot be asked of an investor at their largest sum, and the one counter is kept");
        var under = new Offer { Id = 78, InvestorId = patient.Id, Place = "p", Amount = 40000, ShareBp = 2000 };
        Check(OfferRules.MoreMoney(under, patient), "ventures fix: under it, more money can be asked");
        string app = EconomySource(@"Ventures\VenturesApp.Investors.cs");
        Check(MemberText(app, "private void WriteCounter(").Contains("if (OfferRules.MoreMoney(offer, investor))"), "ventures fix: the counter page offers more money only where it can be had");
    }

    private static void VenturesFixGone()
    {
        VenturesFile file = NewVentures(2024);
        Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient);
        var deal = new Deal { Id = file.NextId++, InvestorId = patient.Id, Place = "g#1", Business = "Gone", Amount = 60000, ShareBp = 2500, PaidOut = 20000, StartDay = 1, Mood = 60, Stage = DealStage.Running, Arrears = 500 };
        file.Deals.Add(deal);
        BusinessFigures gone = Steady(5000f);
        gone.Exists = false;
        int price = DealRules.BuyOutPrice(deal, gone);
        DealRules.RunDay(file, deal, gone, 40, VenturesCatalogue.Standard, new Purse { Money = 0 }, new List<DealEvent>(), null);
        Check(price == 40000 && deal.Stage == DealStage.Called && deal.CallOwed == price, "ventures fix: a business that is gone is bought out for what the next day calls (" + price + ")");
    }

    private static void VenturesFixTests()
    {
        var file = new VenturesFile { Reputation = 50 };
        file.MoveReputation(-10, true);
        file.MoveReputation(5);
        file.MoveReputation(-60, true);
        Check(file.Reputation == 0 && file.TestMoved == -55, "ventures fix: what tests move the name by is kept apart, as far as it moved");
        string tests = EconomySource(@"Ventures\VenturesDirector.Tests.cs");
        string offer = MemberText(tests, "private void TestOfferNow("), call = MemberText(tests, "private void TestCallNow("), back = MemberText(tests, "private void TestBackNow(");
        Check(offer.Contains("_file.Offers[i].Test && (") && offer.Contains("TestRefusal.NothingFits"), "ventures fix: a test offer makes room only among the tests' own");
        Check(call.Contains("_file.Deals[i].Stage != DealStage.Called && _file.Deals[i].Test ?") && !call.Contains("deal == null || _file.Deals[i].Test"), "ventures fix: a test calls back only a test's deal");
        Check(back.Contains("_file.Reputation - _file.TestMoved") && back.Contains("_testBackup.Investors[i]") && !back.Contains("_file.Reputation = _testBackup.Reputation"),
            "ventures fix: putting ventures back undoes the tests' own changes and keeps real ones made between");
        string rules = EconomySource(@"Ventures\DealRules.cs") + EconomySource(@"Ventures\Pitch.cs");
        Check(Occurs(rules, "file.MoveReputation(") == 5 && Occurs(rules, ", deal.Test);") == 4 && rules.Contains("file.MoveReputation(VenturesCatalogue.ReputationCaught, state.Test)"),
            "ventures fix: every move of the name says whether a test made it");
    }

    private static void VenturesFixNoCourt()
    {
        VenturesFile file = NewVentures(5);
        Investor demanding = Find(file, InvestorKind.Tycoon, Temperament.Demanding);
        var deal = new Deal { Id = file.NextId++, InvestorId = demanding.Id, Place = "c#1", Business = "C", Amount = 30000, ShareBp = 2000, StartDay = 1, Mood = 60 };
        file.Deals.Add(deal);
        DealRules.StartCall(deal, 30000, VenturesCatalogue.Standard);
        var said = new List<DealEvent>();
        for (int day = 2; day < 2 + VenturesCatalogue.CallMissesMost + 2; day++)
        {
            DealRules.RunDay(file, deal, Steady(1000f), day, VenturesCatalogue.Standard, new Purse { Money = 0 }, said, amount => 0);
        }
        Check(DealRules.NoCourt(deal) && file.Deals.Contains(deal), "ventures fix: with no court a called sum missed past the most is known as such, and waits");
        var court = new Deal { Id = file.NextId++, InvestorId = demanding.Id, Place = "c#2", Business = "C2", Amount = 30000, ShareBp = 2000, StartDay = 1, Mood = 60 };
        file.Deals.Add(court);
        DealRules.StartCall(court, 30000, VenturesCatalogue.Standard);
        for (int day = 2; day < 2 + VenturesCatalogue.CallMissesMost - 1; day++)
        {
            DealRules.RunDay(file, court, Steady(1000f), day, VenturesCatalogue.Standard, new Purse { Money = 0 }, said, amount => 7);
        }
        Check(!DealRules.NoCourt(court), "ventures fix: before the most, a missed day is not the no-court case");
        string day2 = EconomySource(@"Ventures\VenturesDirector.Day.cs"), app = EconomySource(@"Ventures\VenturesApp.Investors.cs");
        Check(day2.Contains("DealRules.NoCourt(deal) ? \"reallife_ven_note_call_missed_no_court\"") && app.Contains("DealRules.NoCourt(deal) ? FinanceText.T(\"reallife_ven_deal_call_no_court\""),
            "ventures fix: the notice and the deal's page say there is no court rather than that one comes");
    }

    private static void VenturesFixStaff()
    {
        var unknown = new PitchFacts { Running = true, Staff = -1, AgeDays = 30 };
        var known = new PitchFacts { Running = true, Staff = 3, AgeDays = 30 };
        var values = new long[3];
        Question staff = default(Question);
        foreach (Question q in Pitch.Bank)
        {
            staff = q.Kind == QuestionKind.Fact && q.Fact == Fact.Staff ? q : staff;
        }
        Check(Pitch.Options(Fact.Staff, unknown, values) == 1 && !Pitch.Askable(staff, unknown, values) && Pitch.Askable(staff, known, values),
            "ventures fix: a staff count the game could not give is not asked about");
        Check(MemberText(EconomySource(@"Ventures\VenturesGame.cs"), "private static int StaffAt(").Contains("return -1;"), "ventures fix: an unread staff list is told apart from no staff");
    }

    private static void VenturesFixSources()
    {
        string day = MemberText(EconomySource(@"Ventures\VenturesDirector.Day.cs"), "private void InvestorsDay(");
        int early = day.IndexOf("CheckPledges(day - 1);", System.StringComparison.Ordinal), lapse = day.IndexOf("day > pledge.OpenBy", System.StringComparison.Ordinal);
        Check(early > 0 && lapse > early && !day.Contains("CheckPledges(day);"), "ventures fix: at midnight promises are checked against the day that ended before any lapses");
        string app = EconomySource(@"Ventures\VenturesApp.Investors.cs");
        string click = MemberText(app, "private bool InvestorRowClicked(");
        Check(click.Contains("_raisePlace = _raiseKeys[row.Index]") && !click.Contains("Books.Businesses(") && MemberText(app, "private TableSpec WriteRaiseBusiness(").Contains("_raiseKeys.Add(Books.Key(r.Address))"),
            "ventures fix: the business clicked on Raise money is the one its row was written for");
        string accept = MemberText(EconomySource(@"Ventures\VenturesDirector.App.cs"), "public bool Accept(");
        Check(accept.Contains("if (deal == null)") && accept.Contains("NoRoom("), "ventures fix: an offer with no room is refused with the reason");
    }
}
