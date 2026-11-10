using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Schemes;
using SchemeOffice = RealLifeOverhaul.Schemes.Office;

// Schemes and inquiries: what each scheme brings and what it takes it from (the insurer's own rules, the game's tax, a
// buyer, a district's households to the unit), the till and the bank, money that is never taken twice, the people and
// their tempers, a rival's fund, and the save: every field, an older and a later version, and a save and load on every
// day of an inquiry, a trial and a fund.
internal static partial class Program
{
    private static void SchemeMoneyChecks()
    {
        SchemeWorld w = SchemeWorldOf();
        int seed = SchemeRules.SeedOf("money");

        // The claim: a share of the most the cover counts, paid by the insurer's own rules.
        var company = new CompanyState();
        var policy = new Policy();
        CoverRules.Buy(policy, 1, 20, 1, 60);
        int loss = SchemeRules.ClaimLoss(CompanyCatalogue.CoverMost[0], SchemeCatalogue.Small);
        Claim claim = CoverRules.Open(company, policy, SchemePlace, "Shop", 0, loss, 10);
        CoverRules.File(claim, policy, 10);
        Check(loss == CompanyCatalogue.CoverMost[0] / 4 && claim.Payout == loss - CoverRules.Excess(1, loss) && claim.PayDay == 10 + CompanyCatalogue.InsurerPayDays[1] && policy.Claims == 1,
            "schemes money: a small invented claim names a quarter of the most the cover counts, and the insurer pays it less its excess, on its own day, counted in the term");
        Check(SchemeRules.ClaimLoss(CompanyCatalogue.CoverMost[0], SchemeCatalogue.Large) == CompanyCatalogue.CoverMost[0] && SchemeRules.ClaimLoss(8000, SchemeCatalogue.Small) == 2000
              && SchemeRules.ClaimLoss(-5, SchemeCatalogue.Large) == 0 && SchemeRules.ClaimLoss(8000, 99999) == 8000 && SchemeRules.ClaimLoss(8000, -1) == 0,
            "schemes money: a large claim names the whole of it and never more, whatever size is asked");
        SchemeState s = SchemeStateOf(seed, 10, w);
        SchemeAct act = SchemeRules.Claimed(s, SchemePlace, 10, claim.Payout, SchemeCatalogue.Small, claim.Id);
        SchemeShop shop = s.Shop(SchemePlace, false);
        Check(act.Kind == SchemeKind.Claim && act.Gain == claim.Payout && act.Ref == claim.Id && act.Paper && act.Place == SchemePlace && shop != null && shop.ClaimDay == 10
              && !SchemeRules.MayClaim(shop, 10 + SchemeCatalogue.ClaimGapDays - 1) && SchemeRules.MayClaim(shop, 10 + SchemeCatalogue.ClaimGapDays) && SchemeRules.MayClaim(null, 10),
            "schemes money: an invented claim is on the trail with what the insurer will pay, and the next one at that business waits " + SchemeCatalogue.ClaimGapDays + " days");

        // The expense: so many days of average takings, worth the game's tax on it.
        long expense = SchemeRules.ExpenseAmount(3000, SchemeCatalogue.Small);
        Check(expense == 3000L * SchemeCatalogue.ExpenseDays * SchemeCatalogue.Small / 1000 && SchemeRules.ExpenseAmount(3000, SchemeCatalogue.Large) == 3000L * SchemeCatalogue.ExpenseDays
              && SchemeRules.ExpenseAmount(-1, SchemeCatalogue.Large) == 0 && SchemeRules.ExpenseAmount(3000, 99999) == 3000L * SchemeCatalogue.ExpenseDays,
            "schemes money: an invented expense is so many days of the businesses' average takings: " + expense + " small, " + SchemeRules.ExpenseAmount(3000, SchemeCatalogue.Large) + " large");
        Check(SchemeRules.ExpenseGain(expense, 20) == expense / 5 && SchemeRules.ExpenseGain(expense, 0) == 0 && SchemeRules.ExpenseGain(expense, 250) == expense
              && SchemeRules.ExpenseGain(-5, 20) == 0 && SchemeRules.ExpenseGain(expense, -3) == 0,
            "schemes money: an invented expense saves the game's tax on it, nothing with no tax, and never more than itself");
        SchemeAct saved = SchemeRules.Expensed(s, 12, 3000, SchemeCatalogue.Small);
        Check(saved.Kind == SchemeKind.Expense && saved.Gain == 3000 && s.ExpenseDay == 12 && !SchemeRules.MayExpense(s, 12 + SchemeCatalogue.ExpenseGapDays - 1)
              && SchemeRules.MayExpense(s, 12 + SchemeCatalogue.ExpenseGapDays),
            "schemes money: an invented expense is on the trail with the tax it saved, and the next waits " + SchemeCatalogue.ExpenseGapDays + " days");

        // The books: a step, the figure a buyer sees, the part of the price the lift made.
        Check(SchemeRules.LiftStep(0) == 0 && SchemeRules.LiftStep(99) == 0 && SchemeRules.LiftStep(100) == 100 && SchemeRules.LiftStep(299) == 100 && SchemeRules.LiftStep(300) == 300
              && SchemeRules.LiftStep(5000) == 300 && SchemeRules.LiftStep(-7) == 0 && SchemeRules.WashStep(49) == 0 && SchemeRules.WashStep(50) == 50 && SchemeRules.WashStep(199) == 50
              && SchemeRules.WashStep(200) == 200 && SchemeRules.WashStep(999) == 200,
            "schemes money: the books and the till take only the steps they have, the next one down for a figure between two");
        Check(SchemeRules.Lifted(1000f, 100) == 1100f && SchemeRules.Lifted(1000f, 300) == 1300f && SchemeRules.Lifted(1000f, 900) == 1300f && SchemeRules.Lifted(-500f, 300) == -500f
              && SchemeRules.Lifted(0f, 300) == 0f && SchemeRules.Lifted(1000f, 0) == 1000f && SchemeRules.Lifted(1000f, -50) == 1000f,
            "schemes money: dressed books show a profit so much higher, never past the most, and a loss is not dressed up");
        Check(SchemeRules.Over(1300, 300) == 300 && SchemeRules.Over(1100, 100) == 100 && SchemeRules.Over(1000, 0) == 0 && SchemeRules.Over(-5, 300) == 0 && SchemeRules.Over(1300, 900) == 300
              && SchemeRules.Over((long)SchemeRules.Lifted(36000f, 100), 100) == 3600,
            "schemes money: the part of a price the lift made is what the buyer paid for sales that never happened, and no more");
        string[] buyers = { "a rival", "an investor", "the staff", "a chain" };
        for (int kind = 0; kind < 4; kind++)
        {
            foreach (int lift in new[] { SchemeCatalogue.LiftSteps[1], SchemeCatalogue.LiftSteps[2] })
            {
                int seenThrough = 0, same = 0;
                const int offers = 20000;
                for (int offer = 0; offer < offers; offer++)
                {
                    bool sees = SchemeRules.SeenThrough(seed, SchemePlace, kind, lift, offer);
                    seenThrough += sees ? 1 : 0;
                    same += offer < 500 && sees == SchemeRules.SeenThrough(seed, SchemePlace, kind, lift, offer) ? 1 : 0;
                }
                int want = SchemeCatalogue.Diligence[kind] * lift / SchemeCatalogue.LiftMost;
                Check(Math.Abs(seenThrough * 1000 / offers - want) <= 15 && same == 500,
                    "schemes money: " + buyers[kind] + " sees through books lifted by " + lift / 10 + "% about " + want / 10.0 + " times in a hundred, the same offer always the same way: "
                    + seenThrough * 100.0 / offers);
            }
        }
        Check(!SchemeRules.SeenThrough(seed, SchemePlace, 1, 0, 3) && SchemeRules.SeenThrough(seed, SchemePlace, 99, 300, 5) == SchemeRules.SeenThrough(seed, SchemePlace, 3, 300, 5),
            "schemes money: plain books are never seen through, and a kind of buyer out of range is read as the last");
        shop.Lift = SchemeRules.LiftStep(100);
        SchemeAct sold = SchemeRules.Sold(s, SchemePlace, 39600, 30);
        Check(sold != null && sold.Kind == SchemeKind.Books && sold.Gain == 3600 && sold.Size == 100 * 1000 / SchemeCatalogue.LiftMost && sold.Paper && shop.Lift == 0
              && SchemeRules.Sold(s, SchemePlace, 39600, 31) == null && SchemeRules.Sold(s, "Oak~1", 5000, 31) == null,
            "schemes money: a business sold on dressed books puts the part the lift made on the trail, the books are plain again, and a sale on plain books is no act");
        shop.Lift = SchemeCatalogue.LiftMost;
        int booksHeat = SchemeHeat(s, SchemeKind.Books);
        SchemeRules.Walked(s, SchemePlace);
        Check(shop.Lift == 0 && SchemeHeat(s, SchemeKind.Books) - booksHeat == SchemeCatalogue.HeatWalked && s.Acts.Count == 3,
            "schemes money: a buyer who saw through the books walks, the books are plain again, it is talked about, and no money moved");

        // The club: stakes out of a district's households, and back into them, to the unit.
        Check(SchemeRules.ClubHouseholds(SchemeCatalogue.Small) == SchemeCatalogue.ClubSmall && SchemeRules.ClubHouseholds(SchemeCatalogue.Large) == SchemeCatalogue.ClubLarge
              && SchemeRules.ClubStake(199, 1000) == 0 && SchemeRules.ClubStake(200, 1000) == 10 && SchemeRules.ClubStake(8000, 1000) == SchemeCatalogue.ClubStakeMost
              && SchemeRules.ClubStake(900000, 1000) == SchemeCatalogue.ClubStakeMost && SchemeRules.ClubStake(399, 2000) == 0 && SchemeRules.ClubStake(900000, 2000) == 800
              && SchemeRules.ClubStake(-50, 1000) == 0,
            "schemes money: a household's stake is a twentieth of its savings, the most at most, nothing from one with little, at today's prices");
        var people = new PopulationState();
        long there = 0, elsewhere = 0;
        for (int id = 1; id <= 40; id++)
        {
            int slot = people.Households.Add(id);
            people.Households.District[slot] = (byte)(id <= 30 ? 0 : 1);
            people.Households.Savings[slot] = id * 300;
            there += id <= 30 ? id * 300 : 0;
            elsewhere += id <= 30 ? 0 : id * 300;
        }
        var order = new List<int>();
        for (int id = 1; id <= 30; id++)
        {
            order.Add(id);
        }
        order.Sort((a, b) => SchemeRules.ClubDraw(seed, 3, a) != SchemeRules.ClubDraw(seed, 3, b) ? SchemeRules.ClubDraw(seed, 3, a).CompareTo(SchemeRules.ClubDraw(seed, 3, b)) : a.CompareTo(b));
        long expected = 0;
        for (int i = 0; i < 10; i++)
        {
            expected += SchemeRules.ClubStake(order[i] * 300, 1000);
        }
        long taken = PurseRules.Take(people, 0, 10, id => SchemeRules.ClubDraw(seed, 3, id), savings => SchemeRules.ClubStake(savings, 1000), out int joined);
        long after = 0, afterElsewhere = 0;
        bool rightOnes = true;
        for (int h = 0; h < people.Households.Count; h++)
        {
            int id = people.Households.Id[h];
            after += id <= 30 ? people.Households.Savings[h] : 0;
            afterElsewhere += id <= 30 ? 0 : people.Households.Savings[h];
            bool asked = order.IndexOf(id) >= 0 && order.IndexOf(id) < 10;
            rightOnes &= people.Households.Savings[h] == id * 300 - (asked ? SchemeRules.ClubStake(id * 300, 1000) : 0);
        }
        Check(joined == 10 && taken == expected && taken > 0 && there - after == taken && afterElsewhere == elsewhere && rightOnes,
            "schemes money: a club takes each stake from the households its draw picks in the business's district and from nobody else: " + taken + " from " + joined);
        Check(PurseRules.Give(people, 0, taken) == taken && PurseRules.Take(people, 0, 0, id => 1, sv => 5, out _) == 0 && PurseRules.Take(null, 0, 5, id => 1, sv => 5, out _) == 0
              && PurseRules.Take(people, 7, 5, id => 1, sv => 5, out _) == 0 && PurseRules.Give(people, 0, 0) == 0 && PurseRules.Give(null, 0, 50) == 0
              && PurseRules.Give(new PopulationState(), 0, 50) == 0,
            "schemes money: what goes back to a district's households is what they gain, and nothing is taken from or given to nobody");
        long back = 0, all = 0;
        for (int h = 0; h < people.Households.Count; h++)
        {
            back += people.Households.Id[h] <= 30 ? people.Households.Savings[h] : 0;
        }
        long odd = PurseRules.Give(people, 1, 7), nowhere = PurseRules.Give(people, 9, 400);
        for (int h = 0; h < people.Households.Count; h++)
        {
            all += people.Households.Savings[h];
        }
        Check(back == there && odd == 7 && nowhere == 400 && all == there + elsewhere + 407,
            "schemes money: the district's households have to the unit what they had before, a sum that does not divide is given whole, and a district with nobody gives to the city");
        long few = PurseRules.Take(people, 1, 500, id => SchemeRules.ClubDraw(seed, 4, id), savings => SchemeRules.ClubStake(savings, 1000), out int allOfThem);
        Check(allOfThem == 10 && few > 0, "schemes money: a club larger than a district asks everybody there and nobody else (" + allOfThem + ")");
        SchemeState c = SchemeStateOf(seed, 10, w);
        SchemeAct club = SchemeRules.ClubSold(c, SchemePlace, 4000, 50, SchemeCatalogue.Small, 10);
        SchemeShop clubShop = c.Shop(SchemePlace, false);
        Check(c.Dirty == 4000 && c.ClubTaken == 4000 && c.ClubBack == 0 && club.Gain == 4000 && club.Knows == 50 / SchemeCatalogue.ClubKnowsEvery && !club.Paper && club.Ref == 50
              && clubShop.ClubDay == 10 && !SchemeRules.MayClub(clubShop, 10 + SchemeCatalogue.ClubGapDays - 1) && SchemeRules.MayClub(clubShop, 10 + SchemeCatalogue.ClubGapDays)
              && SchemeRules.ClubSold(c, SchemePlace, -9, 3, SchemeCatalogue.Small, 50).Gain == 0 && c.Dirty == 4000,
            "schemes money: a club's takings are cash in hand owed to its households, with one in " + SchemeCatalogue.ClubKnowsEvery + " of them who will complain, and no paper");

        // Never twice: the till's inquiry and the club's together take back exactly what the households lost.
        int calm = SchemeSeedWhere(k => !SchemeRules.Spotted(k, SchemeKind.Club, 0) && SchemeRules.Line(k, SchemeKind.Club, 10, 0) > 600 && SchemeRules.Line(k, SchemeKind.Wash, 10, 0) > 600);
        SchemeState both = SchemeStateOf(calm, 10, w);
        SchemeRules.ClubSold(both, SchemePlace, 10000, 50, SchemeCatalogue.Small, 10);
        both.Shop(SchemePlace, true).Wash = SchemeCatalogue.WashSteps[2];
        var events = new List<SchemeEvent>();
        long wallet = 0;
        for (int d = 11; d <= 20; d++)
        {
            events.Clear();
            SchemeRules.Day(both, d, w, events);
            foreach (SchemeEvent e in events)
            {
                wallet += e.Kind == SchemeEventKind.Washed ? e.Net : 0;
            }
        }
        both.Shop(SchemePlace, true).Wash = 0;
        long perDay = SchemeRules.WashNet(600, 20);
        Check(both.Dirty == 4000 && wallet == 10 * perDay && both.Cases.Count == 0, "schemes money: ten days at the till's higher rate wash 6000 of the cash in hand into " + wallet);
        events.Clear();
        SchemeRules.OpenNow(both, SchemeKind.Wash, 20, events);
        int day = 20;
        SchemeUntil(both, ref day, 80, w, SchemeEventKind.Findings, out SchemeEvent washFound);
        Check(washFound.A == (int)SchemeKind.Wash && washFound.Amount == wallet && washFound.FromCash == 4000 && washFound.Net == SchemeCatalogue.Share(wallet, SchemeCatalogue.SurchargePermille)
              && both.Dirty == 0 && both.ClubBack == wallet && washFound.Text == JusticeLaw.FalseAccounting,
            "schemes money: the till's inquiry takes the washed money back for the households, out of the cash in hand first, with a quarter on top");
        SchemeRules.Close(both, both.Inquiry(washFound.Inquiry), InquiryEnd.Convicted, day);
        events.Clear();
        SchemeRules.OpenNow(both, SchemeKind.Club, day, events);
        SchemeUntil(both, ref day, 160, w, SchemeEventKind.Findings, out SchemeEvent clubFound);
        Check(clubFound.A == (int)SchemeKind.Club && clubFound.Amount == 10000 - wallet && clubFound.FromCash == 0 && washFound.Amount + clubFound.Amount == 10000
              && clubFound.Net == SchemeCatalogue.Share(10000 - wallet, SchemeCatalogue.SurchargePermille) && both.ClubBack == both.ClubTaken && clubFound.Text == JusticeLaw.Fraud
              && SchemeRules.Recover(both, 5000) == 0,
            "schemes money: the club's inquiry then takes only what its households are still out: the two together give back exactly the 10000 taken, never a unit twice");
        SchemeState recover = SchemeStateOf(calm, 10, w);
        SchemeRules.ClubSold(recover, SchemePlace, 3000, 50, SchemeCatalogue.Small, 10);
        SchemeRules.ClubSold(recover, "Oak~1", 2000, 50, SchemeCatalogue.Small, 12);
        long firstPart = SchemeRules.Recover(recover, 3500);
        bool oldestFirst = recover.Acts[0].Owed == 0 && recover.Acts[1].Owed == 1500;
        long rest = SchemeRules.Recover(recover, 99999);
        Check(firstPart == 3500 && oldestFirst && recover.Acts[1].Owed == 0 && rest == 1500 && recover.ClubBack == 5000 && SchemeRules.Recover(recover, 1) == 0
              && SchemeRules.Recover(recover, -5) == 0,
            "schemes money: money goes back to the oldest club first, and never more than the households are still out");

        // Watched: from the visit of an inquiry into a club or cash sales, the cash in hand does not move.
        SchemeState watched = SchemeStateOf(calm, 10, w);
        SchemeRules.ClubSold(watched, SchemePlace, 9000, 50, SchemeCatalogue.Small, 10);
        watched.Shop(SchemePlace, true).Wash = SchemeCatalogue.WashSteps[1];
        events.Clear();
        SchemeInquiry wq = SchemeRules.OpenNow(watched, SchemeKind.Club, 10, events);
        day = 10;
        int visit = SchemeUntil(watched, ref day, 60, w, SchemeEventKind.Visit, out _);
        long inHand = watched.Dirty;
        events.Clear();
        SchemeRules.Day(watched, day + 1, w, events);
        long cost = SchemeRules.RepayCost(watched, SchemeKind.Club, day + 1, null);
        SchemeRules.Repay(watched, SchemeKind.Club, day + 1, null, out long fromCash);
        Check(visit == 10 + SchemeCatalogue.VisitAfter && watched.Watched && watched.Dirty == inHand && events.Count == 0 && cost == 9000 && fromCash == 0 && watched.ClubBack == 9000,
            "schemes money: from the visit the cash in hand is watched: the till takes none of it, and paying the club back then comes from the wallet alone");
        SchemeRules.Close(watched, wq, InquiryEnd.Settled, day + 2);
        Check(!watched.Watched, "schemes money: the cash in hand is free again once nobody is looking at it");
        SchemeState unwatched = SchemeStateOf(calm, 10, w);
        SchemeRules.ClubSold(unwatched, SchemePlace, 9000, 50, SchemeCatalogue.Small, 10);
        unwatched.Dirty = 2500;
        long paidBack = SchemeRules.Repay(unwatched, SchemeKind.Club, 11, null, out long cashPart);
        Check(paidBack == 9000 && cashPart == 2500 && unwatched.Dirty == 0 && unwatched.ClubBack == 9000,
            "schemes money: a club is paid back out of the cash in hand first, as far as it goes, and the rest from the wallet");
    }

    // ------------------------------------------------------------------------------------------------- the till and the bank

    private static void SchemeWashChecks()
    {
        Check(SchemeRules.WashRoom(3000, 200, 100000) == 600 && SchemeRules.WashRoom(3000, 50, 100000) == 150 && SchemeRules.WashRoom(3000, 200, 250) == 250
              && SchemeRules.WashRoom(3000, 120, 100000) == 150 && SchemeRules.WashRoom(-5, 200, 100) == 0 && SchemeRules.WashRoom(3000, 0, 100) == 0
              && SchemeRules.WashRoom(3000, 200, 0) == 0 && SchemeRules.WashRoom(3000, 5000, 100000) == 600,
            "schemes till: a till takes a step's share of an average day's takings a day, as far as the cash in hand goes, and never more than its highest step");
        bool lossy = true, additive = true;
        for (int a = 0; a <= 6000; a += 7)
        {
            long net = SchemeRules.WashNet(a, 20);
            lossy &= net >= 0 && net <= a * 85L * 80 / 10000 && (a < 10 || net < a) && SchemeRules.WashNet(a, 0) == a * 850L / 1000 && SchemeRules.WashNet(a, 100) == 0
                     && SchemeRules.WashNet(a, 250) == 0 && SchemeRules.WashNet(a, -9) == SchemeRules.WashNet(a, 0);
            additive &= SchemeRules.WashNet(a, 20) + SchemeRules.WashNet(6000 - a, 20) <= SchemeRules.WashNet(6000, 20);
        }
        Check(lossy && additive && SchemeRules.WashNet(600, 20) == 408 && SchemeRules.WashNet(-5, 20) == 0,
            "schemes till: of cash put through a till the cut is lost and the game's tax taken off the rest, rounded against the player, so that no splitting into days keeps more");

        int calm = SchemeSeedWhere(k => SchemeRules.Line(k, SchemeKind.Wash, 10, 0) > 640 && SchemeRules.Line(k, SchemeKind.Wash, 100, 0) > 640);
        SchemeWorld w = SchemeWorldOf();
        SchemeState s = SchemeStateOf(calm, 10, w);
        s.Dirty = 10000;
        SchemeShop shop = s.Shop(SchemePlace, true);
        shop.Wash = SchemeCatalogue.WashSteps[2];
        var events = new List<SchemeEvent>();
        SchemeRules.Day(s, 11, w, events);
        Check(events.Count == 1 && events[0].Kind == SchemeEventKind.Washed && events[0].Amount == 600 && events[0].Net == 408 && events[0].Text == SchemePlace && s.Dirty == 9400
              && s.Acts.Count == 1 && s.Acts[0].Kind == SchemeKind.Wash && s.Acts[0].Gain == 408 && s.Acts[0].Size == 1000 && s.Acts[0].Place == SchemePlace
              && SchemeHeat(s, SchemeKind.Wash) == SchemeCatalogue.WashSteps[2] / SchemeCatalogue.WashHeatDivisor && s.Acted == 0,
            "schemes till: a day's washing leaves the cash in hand, reaches the player less the cut and the tax, is one act on the trail and heats it by its rate");
        events.Clear();
        SchemeRules.Day(s, 12, w, events);
        Check(events.Count == 1 && s.Acts.Count == 1 && s.Acts[0].Gain == 816 && s.Dirty == 8800 && shop.Window == 1200
              && SchemeHeat(s, SchemeKind.Wash) == 2 * (SchemeCatalogue.WashSteps[2] / SchemeCatalogue.WashHeatDivisor) - SchemeCatalogue.HeatDecay,
            "schemes till: a window's washing at a business is one act growing by the day, never ten innocent days");

        // Nothing is washed: watched, the option off, no counter, a business not read, no cash.
        foreach (string why in new[] { "watched", "off", "no counter", "not read", "no cash" })
        {
            SchemeWorld x = SchemeWorldOf(why != "off");
            if (why == "no counter")
            {
                x.Tills[0] = new SchemeTill { Place = SchemePlace, Average = 3000, Cash = false };
            }
            if (why == "not read")
            {
                x.Tills.Clear();
            }
            SchemeState n = SchemeStateOf(calm, 10, x);
            n.Dirty = why == "no cash" ? 0 : 10000;
            n.Watched = why == "watched";
            n.Shop(SchemePlace, true).Wash = SchemeCatalogue.WashSteps[2];
            events.Clear();
            SchemeRules.Day(n, 11, x, events);
            Check(events.Count == 0 && n.Dirty == (why == "no cash" ? 0 : 10000) && n.Acts.Count == 0, "schemes till: no cash goes through a till when " + why);
        }

        // The bank: a day's cash sales over a line, and a window's sum as a pattern, once a window.
        SchemeWorld busy = SchemeWorldOf(true, 20000);
        SchemeState b = SchemeStateOf(calm, 100, busy);
        b.Dirty = 1000000;
        SchemeShop bank = b.Shop(SchemePlace, true);
        bank.Wash = SchemeCatalogue.WashSteps[2];
        int reports = 0, patterns = 0, patternDay = 0, heatBefore = 0, heatAtPattern = 0;
        for (int d = 101; d <= 125; d++)
        {
            events.Clear();
            heatBefore = SchemeHeat(b, SchemeKind.Wash);
            SchemeRules.Day(b, d, busy, events);
            foreach (SchemeEvent e in events)
            {
                reports += e.Kind == SchemeEventKind.BankReport ? 1 : 0;
                if (e.Kind == SchemeEventKind.Pattern)
                {
                    patterns++;
                    patternDay = d;
                    heatAtPattern = SchemeHeat(b, SchemeKind.Wash) - heatBefore;
                }
            }
            if (b.OpenOf(SchemeKind.Wash) != null)
            {
                break;
            }
        }
        int daily = SchemeCatalogue.WashSteps[2] / SchemeCatalogue.WashHeatDivisor + SchemeCatalogue.HeatReport - SchemeCatalogue.HeatDecay;
        Check(4000 > SchemeCatalogue.ReportFrom && reports >= 7 && patterns == 1 && patternDay == 100 + (SchemeCatalogue.PatternFrom + 3999) / 4000 && bank.Flagged
              && heatAtPattern == daily + SchemeCatalogue.HeatPattern && bank.WindowFrom == 101,
            "schemes till: the bank reports each day's cash sales over " + SchemeCatalogue.ReportFrom + ", and a window's " + SchemeCatalogue.PatternFrom + " as a pattern, once (day "
            + (patternDay - 100) + ")");
        SchemeWorld dear = SchemeWorldOf(true, 20000);
        dear.Level = 2000;
        SchemeState d2 = SchemeStateOf(calm, 100, dear);
        d2.Dirty = 1000000;
        d2.Shop(SchemePlace, true).Wash = SchemeCatalogue.WashSteps[2];
        events.Clear();
        SchemeRules.Day(d2, 101, dear, events);
        Check(events.Count == 1 && events[0].Kind == SchemeEventKind.Washed && events[0].Amount == 4000, "schemes till: the bank's lines are at today's prices: at twice the level 4000 a day is not reported");
        SchemeState win = SchemeStateOf(calm, 100, w);
        win.Dirty = 100000;
        SchemeShop ws = win.Shop(SchemePlace, true);
        ws.Wash = SchemeCatalogue.WashSteps[1];
        events.Clear();
        SchemeRules.Day(win, 101, w, events);
        int firstWindow = ws.WindowFrom;
        ws.Flagged = true;
        for (int d = 102; d <= 101 + SchemeCatalogue.PatternDays; d++)
        {
            SchemeRules.Day(win, d, w, events);
        }
        Check(firstWindow == 101 && ws.WindowFrom == 101 + SchemeCatalogue.PatternDays && !ws.Flagged && ws.Window == 150 && win.Acts.Count == 2 && win.Acts[1].Day == ws.WindowFrom,
            "schemes till: the bank's window is " + SchemeCatalogue.PatternDays + " days: a new one begins a new sum and a new act on the trail");

        // Dressed books kept for long are talked about; a little is not.
        SchemeState books = SchemeStateOf(calm, 10, w);
        books.Shop(SchemePlace, true).Lift = SchemeCatalogue.LiftMost;
        SchemeState little = SchemeStateOf(calm, 10, w);
        little.Shop(SchemePlace, true).Lift = SchemeCatalogue.LiftSteps[1];
        events.Clear();
        for (int d = 11; d <= 60; d++)
        {
            SchemeRules.Day(books, d, w, events);
            SchemeRules.Day(little, d, w, events);
        }
        int lot = SchemeCatalogue.LiftMost / SchemeCatalogue.LiftHeatDivisor;
        Check(SchemeHeat(books, SchemeKind.Books) == lot + 49 * (lot - SchemeCatalogue.HeatDecay) && SchemeHeat(little, SchemeKind.Books) <= SchemeCatalogue.LiftSteps[1] / SchemeCatalogue.LiftHeatDivisor
              && events.Count == 0,
            "schemes till: books dressed a lot heat their trail a little every day they stand, books dressed a little do not, and neither is an act until a sale");
    }

    // ------------------------------------------------------------------------------------------------- the people

    private static void SchemePeopleChecks()
    {
        SchemeWorld w = SchemeWorldOf();
        Check(SchemeRules.Contact(7) == "p7" && SchemeRules.Investigator(SchemeKind.Claim, 2) == "i2" && SchemeRules.Investigator(SchemeKind.Expense, 1) == "i101" && SchemeRules.Judge(15) == "j15"
              && SchemeRules.Councillor("seat 1|x\ny") == "cseat 1 x y" && SchemeRules.Councillor(null) == "c",
            "schemes people: everybody who can be offered money has an id of their own that no save line can break");

        // Who takes money is a trait of the person.
        string[] names = { "a contact", "a councillor", "an investigator", "a judge" };
        int seed = SchemeRules.SeedOf("people");
        for (int o = 0; o < 4; o++)
        {
            int takes = 0, reports = 0, steady = 0, other = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                string who = "x" + i.ToString(CultureInfo.InvariantCulture);
                Temper t = SchemeRules.TemperOf(seed, (SchemeOffice)o, who);
                takes += t == Temper.Takes ? 1 : 0;
                reports += t == Temper.Reports ? 1 : 0;
                steady += i < 300 && t == SchemeRules.TemperOf(seed, (SchemeOffice)o, who) ? 1 : 0;
                other += i < 300 && t != SchemeRules.TemperOf(seed + 1, (SchemeOffice)o, who) ? 1 : 0;
            }
            Check(Math.Abs(takes * 1000 / n - SchemeCatalogue.TakesPermille[o]) <= 15 && Math.Abs(reports * 1000 / n - SchemeCatalogue.ReportsPermille[o]) <= 15 && steady == 300 && other > 60,
                "schemes people: " + names[o] + " takes money in " + takes * 100.0 / n + "% of cases and reports it in " + reports * 100.0 / n
                + "%, the same person always the same, another save's people otherwise");
        }
        Check(SchemeRules.TemperOf(seed, (SchemeOffice)99, "a") == SchemeRules.TemperOf(seed, SchemeOffice.Judge, "a"), "schemes people: an office out of range is read as the highest");

        int taker = -1, quiet = -1, teller = -1;
        for (int i = 0; i < 400 && (taker < 0 || quiet < 0 || teller < 0); i++)
        {
            Temper t = SchemeRules.TemperOf(seed, SchemeOffice.Contact, SchemeRules.Contact(i));
            taker = taker < 0 && t == Temper.Takes ? i : taker;
            quiet = quiet < 0 && t == Temper.Remembers ? i : quiet;
            teller = teller < 0 && t == Temper.Reports ? i : teller;
        }

        // Taken, through the bank account.
        SchemeState s = SchemeStateOf(seed, 10, w);
        long price = SchemeRules.PriceOf(s, Favour.Gift, null, 10, 1000);
        OfferResult took = SchemeRules.Offer(s, Favour.Gift, SchemeOffice.Contact, SchemeRules.Contact(taker), price, false, 10);
        SchemeAct gift = s.Acts.Count == 1 ? s.Acts[0] : new SchemeAct();
        Check(price == SchemeCatalogue.FavourPrice[(int)Favour.Gift] && took.Taken && took.Price == price && !took.Cold && took.Temper == Temper.Takes && gift.Kind == SchemeKind.Graft
              && gift.Paid == price && gift.Gain == 0 && gift.Ref == (int)Favour.Gift && gift.Paper && gift.Knows == 1 && gift.Size == SchemeCatalogue.FavourSize[(int)Favour.Gift]
              && s.Paid.Count == 1 && s.Paid[0].Who == SchemeRules.Contact(taker) && s.Paid[0].Amount == price && s.Paid[0].What == Favour.Gift && s.Paid[0].Day == 10 && s.Cold.Count == 0,
            "schemes people: money taken is an act on the trail with what was paid, a paper trail when it went through the account, and somebody who now knows");
        s.Dirty = 5000;
        OfferResult cash = SchemeRules.Offer(s, Favour.Word, SchemeOffice.Contact, SchemeRules.Contact(taker), 1500, true, 11);
        Check(cash.Taken && cash.Cash && s.Dirty == 3500 && s.Acts.Count == 2 && !s.Acts[1].Paper && s.Paid.Count == 2,
            "schemes people: paid out of the cash in hand, a favour leaves no paper and the cash is less by its price");

        // Refused: remembered, and by some reported; nobody is asked twice.
        SchemeState r = SchemeStateOf(seed, 10, w);
        OfferResult no = SchemeRules.Offer(r, Favour.Gift, SchemeOffice.Contact, SchemeRules.Contact(quiet), 800, false, 10);
        Check(!no.Taken && no.Price == 0 && no.Temper == Temper.Remembers && r.Cold.Count == 1 && SchemeRules.IsCold(r, SchemeRules.Contact(quiet)) && r.Acts.Count == 0 && r.Paid.Count == 0
              && SchemeHeat(r, SchemeKind.Graft) == 0,
            "schemes people: somebody who only remembers says no, takes nothing, tells nobody, and is not to be asked again");
        OfferResult again = SchemeRules.Offer(r, Favour.Word, SchemeOffice.Contact, SchemeRules.Contact(quiet), 1500, false, 11);
        Check(again.Cold && !again.Taken && r.Cold.Count == 1 && r.Acts.Count == 0, "schemes people: asking again changes nothing");
        OfferResult told = SchemeRules.Offer(r, Favour.Access, SchemeOffice.Contact, SchemeRules.Contact(teller), 4000, false, 12);
        int offered = SchemeRules.HeatOf(SchemeKind.Graft, SchemeCatalogue.FavourSize[(int)Favour.Access], 0, 0, 1, false);
        Check(!told.Taken && told.Temper == Temper.Reports && r.Cold.Count == 2 && r.Acts.Count == 1 && r.Acts[0].Paid == 0 && r.Acts[0].Gain == 0 && r.Acts[0].Ref == (int)Favour.Access
              && r.Paid.Count == 0 && SchemeHeat(r, SchemeKind.Graft) >= SchemeCatalogue.HeatReported + offered && SchemeRules.Count(r, SchemeKind.Graft, 12) == 1,
            "schemes people: a reported offer is on file as a payment would be, with no money moved, and heats the trail by its own weight and the report's");
        int judge = -1;
        for (int i = 1; i < 400 && judge < 0; i++)
        {
            judge = SchemeRules.TemperOf(seed, SchemeOffice.Judge, SchemeRules.Judge(i)) == Temper.Reports ? i : judge;
        }
        SchemeState j = SchemeStateOf(seed, 10, w);
        OfferResult bench = SchemeRules.Offer(j, Favour.Judge, SchemeOffice.Judge, SchemeRules.Judge(judge), 25000, false, 10);
        Check(bench.Temper == Temper.Reports && !bench.Taken && j.Acts.Count == 0 && SchemeHeat(j, SchemeKind.Graft) == SchemeCatalogue.HeatReported && j.Cold.Count == 1,
            "schemes people: a judge who reports an offer leaves no act on the trail: it goes to the court as a charge of its own, once");
        SchemeState chilled = SchemeStateOf(seed, 10, w);
        SchemeRules.Chill(chilled, SchemeRules.Contact(taker));
        SchemeRules.Chill(chilled, SchemeRules.Contact(taker));
        SchemeRules.Chill(chilled, null);
        OfferResult cold = SchemeRules.Offer(chilled, Favour.Gift, SchemeOffice.Contact, SchemeRules.Contact(taker), 800, false, 10);
        Check(chilled.Cold.Count == 1 && cold.Cold && !cold.Taken && cold.Temper != Temper.Takes && chilled.Acts.Count == 0, "schemes people: somebody who was put off once takes nothing afterwards");
        SchemeState crowd = SchemeStateOf(seed, 10, w);
        for (int i = 0; i < 400; i++)
        {
            SchemeRules.Offer(crowd, Favour.Gift, SchemeOffice.Contact, SchemeRules.Contact(i), 800, false, 10);
            SchemeRules.Buy(crowd, i);
            SchemeRules.Buy(crowd, i);
        }
        Check(crowd.Paid.Count == SchemeCatalogue.PaidMost && crowd.Cold.Count == SchemeCatalogue.ColdMost && crowd.Bought.Count == SchemeCatalogue.BoughtMost,
            "schemes people: the save keeps a bounded number of people paid, people put off and favours bought");

        // What a favour costs.
        SchemeState p = SchemeStateOf(seed, 10, w);
        SchemeRules.Record(p, SchemeKind.Claim, 10, 20000, SchemeCatalogue.Large, 0, true, SchemePlace, 1);
        var q = new SchemeInquiry { Id = 1, Kind = SchemeKind.Claim, Opened = 10, Stage = InquiryStage.Letter };
        var smallFile = new SchemeInquiry { Id = 2, Kind = SchemeKind.Expense, Opened = 10, Stage = InquiryStage.Letter };
        Check(SchemeRules.PriceOf(p, Favour.Word, null, 10, 1000) == 1500 && SchemeRules.PriceOf(p, Favour.Word, null, 10, 2000) == 3000 && SchemeRules.PriceOf(p, Favour.Judge, null, 10, 1000) == 25000
              && SchemeRules.PriceOf(p, Favour.Investigator, q, 10, 1000) == 20000L * SchemeCatalogue.InvestigatorShare / 1000
              && SchemeRules.PriceOf(p, Favour.Investigator, smallFile, 10, 1000) == SchemeCatalogue.FavourPrice[(int)Favour.Investigator]
              && SchemeRules.PriceOf(p, Favour.Investigator, null, 10, 1000) == SchemeCatalogue.FavourPrice[(int)Favour.Investigator],
            "schemes people: a favour has its price at today's level, and an investigator asks a share of what the file holds if that is more");
        Check(SchemeRules.Round(1234) == 1230 && SchemeRules.Round(1235) == 1240 && SchemeRules.Round(99) == 99 && SchemeRules.Round(-5) == 0, "schemes people: a price is named in round tens");

        // Somebody paid can talk, for as long as they hold what they know.
        int talks = 0, pressed = 0;
        const int draws = 200000;
        for (int i = 0; i < draws; i++)
        {
            talks += SchemeRules.Talks(seed, "p" + (i % 50).ToString(CultureInfo.InvariantCulture), i, false) ? 1 : 0;
            pressed += SchemeRules.Talks(seed, "p" + (i % 50).ToString(CultureInfo.InvariantCulture), i, true) ? 1 : 0;
        }
        Check(Math.Abs(talks * 10000L / draws - SchemeCatalogue.TalkPermille * 10) <= 5 && Math.Abs(pressed * 10000L / draws - SchemeCatalogue.TalkPressed * 10) <= 20
              && SchemeRules.Talks(seed, "p3", 77, true) == SchemeRules.Talks(seed, "p3", 77, true),
            "schemes people: somebody paid talks on " + talks * 1000.0 / draws + " days in a thousand, and on " + pressed * 1000.0 / draws + " while an inquiry into such payments is open");
        int talkDay = 0;
        for (int d = 20; d < 5000 && talkDay == 0; d++)
        {
            talkDay = SchemeRules.Talks(seed, "p3", d, false) ? d : 0;
        }
        SchemeState talker = SchemeStateOf(seed, talkDay - 1, w);
        talker.Paid.Add(new SchemePaid { Who = "p3", Day = talkDay - 5, Amount = 800, What = Favour.Gift });
        talker.Paid.Add(new SchemePaid { Who = "old", Day = talkDay - SchemeCatalogue.HoldDays - 1, Amount = 800, What = Favour.Gift });
        talker.Paid.Add(new SchemePaid { Who = "kept", Day = talkDay - SchemeCatalogue.HoldDays, Amount = 800, What = Favour.Gift, Talked = true });
        var graft = new SchemeInquiry { Id = 1, Kind = SchemeKind.Graft, Opened = talkDay - 1, Stage = InquiryStage.Charged };
        talker.Cases.Add(graft);
        var events = new List<SchemeEvent>();
        SchemeRules.Day(talker, talkDay, w, events);
        Check(events.Count == 1 && events[0].Kind == SchemeEventKind.Talked && events[0].Text == "p3" && talker.Paid.Count == 2 && talker.Paid[0].Talked && talker.Paid[1].Who == "kept"
              && SchemeHeat(talker, SchemeKind.Graft) == SchemeCatalogue.HeatTalk && graft.Pressed == SchemeCatalogue.EvidenceTalk,
            "schemes people: somebody who talks is heard once: the trail heats, an open inquiry into such payments gains evidence, and whoever has held it past "
            + SchemeCatalogue.HoldDays + " days is forgotten");
        events.Clear();
        SchemeRules.Day(talker, talkDay + 1, w, events);
        Check(events.Count == 0 && graft.Pressed == SchemeCatalogue.EvidenceTalk, "schemes people: nobody talks twice");

        // A gift, an evening laid on, and the councillor a push is put to.
        SchemeState h = SchemeStateOf(seed, 10, w);
        bool hosted = SchemeRules.Hosted(h, taker), hostedQuiet = SchemeRules.Hosted(h, quiet), hostedNobody = SchemeRules.Hosted(h, -1);
        SchemeRules.Buy(h, taker);
        SchemeState hc = SchemeStateOf(seed, 10, w);
        SchemeRules.Chill(hc, SchemeRules.Contact(taker));
        Check(hosted && !hostedQuiet && !hostedNobody && h.Bought.Count == 1 && h.Bought[0] == taker && h.Acts.Count == 0 && h.Paid.Count == 0 && !SchemeRules.Hosted(hc, taker),
            "schemes people: somebody hosted who takes gifts has their next favour bought by it, with no money and no trail; anybody else had an evening out");
        var seats = new int[5];
        bool inSeats = true;
        for (int push = 1; push <= 400; push++)
        {
            int seat = SchemeRules.Seat(seed, push, 5);
            inSeats &= seat >= 0 && seat < 5 && seat == SchemeRules.Seat(seed, push, 5);
            seats[Math.Max(0, Math.Min(4, seat))]++;
        }
        Check(inSeats && seats[0] > 40 && seats[1] > 40 && seats[2] > 40 && seats[3] > 40 && seats[4] > 40 && SchemeRules.Seat(seed, 3, 0) == -1,
            "schemes people: a push is put to one councillor of the council by the save and the push, always the same one, and to nobody with no council");

        // The charge a file of payments becomes, and what its findings take.
        SchemeState g = SchemeStateOf(seed, 10, w);
        SchemeRules.Record(g, SchemeKind.Graft, 10, 0, 400, 1, true, string.Empty, (int)Favour.Vote).Paid = 6000;
        bool bribery = SchemeRules.ChargeOf(g, SchemeKind.Graft, 10, 1000, false) == JusticeLaw.Bribery;
        SchemeRules.Record(g, SchemeKind.Graft, 11, 0, 500, 1, false, string.Empty, (int)Favour.Investigator).Paid = 5000;
        Check(bribery && SchemeRules.ChargeOf(g, SchemeKind.Graft, 11, 1000, false) == JusticeLaw.PervertingJustice && SchemeRules.ChargeOf(g, SchemeKind.Graft, 11, 1000, true) == JusticeLaw.Bribery,
            "schemes people: money to an official is bribery, money to an investigator or a judge perverting justice, and bribery again for somebody who told it all");
        events.Clear();
        SchemeRules.OpenNow(g, SchemeKind.Graft, 11, events);
        int day = 11;
        SchemeUntil(g, ref day, 80, w, SchemeEventKind.Findings, out SchemeEvent found);
        Check(found.Amount == 0 && found.FromCash == 0 && found.Net == SchemeCatalogue.Share(11000, SchemeCatalogue.SurchargePermille) && found.Text == JusticeLaw.PervertingJustice,
            "schemes people: an inquiry into payments takes nothing back, since nothing was gained, and a quarter of what was paid on top");
        Check(SchemeRules.ChargeOf(g, SchemeKind.Expense, 11, 1000, false) == JusticeLaw.TaxEvasion && SchemeRules.ChargeOf(g, SchemeKind.Wash, 11, 1000, false) == JusticeLaw.FalseAccounting,
            "schemes people: an invented expense is tax evasion, and cash sales of little are false accounting");
        SchemeState washed = SchemeStateOf(seed, 10, w);
        washed.Acts.Add(new SchemeAct { Kind = SchemeKind.Wash, Day = 10, Gain = SchemeCatalogue.FelonyFrom, Place = SchemePlace, Size = 1000 });
        Check(SchemeRules.ChargeOf(washed, SchemeKind.Wash, 10, 1000, false) == JusticeLaw.MoneyLaundering && SchemeRules.ChargeOf(washed, SchemeKind.Wash, 10, 1000, true) == JusticeLaw.FalseAccounting,
            "schemes people: cash sales of a felony's worth are money laundering, and the lesser charge for somebody who told it all");

        // The court's own sum, for a rival and for the checks' player.
        int guilty = 0, weak = 0, defended = 0;
        for (int i = 0; i < 4000; i++)
        {
            guilty += SchemeRules.Tried(seed, i, 700, 0, 250) ? 1 : 0;
            weak += SchemeRules.Tried(seed, i, 400, 0, 250) ? 1 : 0;
            defended += SchemeRules.Tried(seed, i, 700, 0, 830) ? 1 : 0;
        }
        int want = JusticeLaw.CaseAgainst(0.7f, 0, 0.25f, 0);
        Check(Math.Abs(guilty / 4 - want) <= 25 && weak < guilty && defended < guilty && SchemeRules.Tried(seed, 5, 700, 0, 250) == SchemeRules.Tried(seed, 5, 700, 0, 250),
            "schemes people: a file is judged by the court's own sum: convicted " + guilty / 40.0 + " times in a hundred on evidence of 700 (the court's case " + want / 10.0
            + "), less on a weaker file or with better counsel, and the same case always the same way");
        Check(SchemeRules.FineOf(JusticeLaw.Fraud, 0, 1000000f) > SchemeRules.FineOf(JusticeLaw.FalseAccounting, 0, 1000000f) && SchemeRules.FineOf(JusticeLaw.Fraud, 2, 1000000f)
              > SchemeRules.FineOf(JusticeLaw.Fraud, 0, 1000000f) && SchemeRules.FineOf(JusticeLaw.Fraud, 0, 0f) == JusticeLaw.FineFloor && SchemeRules.FineOf("nothing", 0, 100f) == 0,
            "schemes people: a court's fine is Justice's own: more for the graver charge and for a record, and bounded by the cash in hand, which is why what an inquiry finds is taken apart from it");
    }

    // ------------------------------------------------------------------------------------------------- a rival's fund

    private static SchemeWorld SchemeCityWorld(bool on = true, bool session = false, long worth = 600000)
    {
        var w = new SchemeWorld { On = on, Session = session, Worth = worth };
        for (int i = 0; i < SchemeState.Rivals; i++)
        {
            w.RivalActive[i] = true;
            w.RivalWorth[i] = 40000000L + 25000000L * i;
        }
        w.Council.AddRange(SchemeSeats);
        return w;
    }

    private static void SchemeFundChecks()
    {
        Check(SchemeRules.FundAmount(600000, 1000) == 12000 && SchemeRules.FundAmount(10, 1000) == SchemeCatalogue.FundLeast && SchemeRules.FundAmount(100000000L, 1000) == SchemeCatalogue.FundMost
              && SchemeRules.FundAmount(10, 2000) == 2 * SchemeCatalogue.FundLeast && SchemeRules.FundAmount(617777, 1000) == 12300,
            "schemes fund: a fund asks a fiftieth of the player's worth, between its least and its most at today's prices, in round hundreds");
        Check(SchemeRules.FundFee(12000, 1000) == 360 && SchemeRules.FundFee(2000, 1000) == SchemeCatalogue.FundFeeLeast && SchemeRules.FundFee(2000, 2000) == 2 * SchemeCatalogue.FundFeeLeast
              && SchemeRules.FundBack(12000) == 12960 && SchemeRules.FundBack(0) == 0,
            "schemes fund: having a fund looked at costs a share of its sum, a stated fee at least, and an honest fund pays " + SchemeCatalogue.FundReturn / 10 + "% on top");
        Check(SchemeRules.RivalGain(40000000L, SchemeCatalogue.Small, 1000) == 20000 && SchemeRules.RivalGain(40000000L, SchemeCatalogue.Large, 1000) == 160000
              && SchemeRules.RivalGain(90000000L, SchemeCatalogue.Large, 1000) == SchemeCatalogue.RivalGainMost && SchemeRules.RivalGain(90000000L, SchemeCatalogue.Large, 2000) == 360000
              && SchemeRules.RivalGain(-5, SchemeCatalogue.Large, 1000) == 0,
            "schemes fund: a rival's scheme brings a share of their worth, bounded at today's prices");
        bool bentInRange = true;
        int never = 0;
        for (int i = 0; i < 3000; i++)
        {
            int bent = SchemeRules.Bent(i * 7919 + 1, i % 3);
            never += bent == 0 ? 1 : 0;
            bentInRange &= bent == 0 || (bent >= SchemeCatalogue.RivalBentLeast && bent < SchemeCatalogue.RivalBentLeast + SchemeCatalogue.RivalBentSpread);
            bentInRange &= bent == SchemeRules.Bent(i * 7919 + 1, i % 3);
        }
        Check(bentInRange && Math.Abs(never / 3 - SchemeCatalogue.RivalCleanPermille) <= 30, "schemes fund: how bent a rival is, is a trait of theirs within its bounds, and nothing for " + never / 30.0 + "% of them");

        int shamSeed = 0, honestSeed = 0;
        var events = new List<SchemeEvent>();
        for (int i = 1; i < 400 && (shamSeed == 0 || honestSeed == 0); i++)
        {
            int seed = SchemeRules.SeedOf("fund " + i.ToString(CultureInfo.InvariantCulture));
            SchemeWorld probe = SchemeCityWorld();
            SchemeState x = SchemeStateOf(seed, 500, probe);
            events.Clear();
            if (!SchemeRules.FundNow(x, 500, probe, events))
            {
                continue;
            }
            shamSeed = shamSeed == 0 && x.Fund.Sham ? seed : shamSeed;
            honestSeed = honestSeed == 0 && !x.Fund.Sham ? seed : honestSeed;
        }
        SchemeWorld w = SchemeCityWorld();

        // An honest fund: offered, looked at, placed, paid.
        SchemeState s = SchemeStateOf(honestSeed, 500, w);
        events.Clear();
        bool offered = SchemeRules.FundNow(s, 500, w, events);
        SchemeFund f = s.Fund;
        Check(offered && events.Count == 1 && events[0].Kind == SchemeEventKind.FundOffered && events[0].Amount == 12000 && f.State == FundState.Open && f.Amount == 12000 && !f.Sham
              && f.Due == 500 + SchemeCatalogue.FundAnswerDays && f.Tycoon >= 0 && f.Tycoon < SchemeState.Rivals && f.Number == 1 && !SchemeRules.FundNow(s, 500, w, events),
            "schemes fund: a rival offers a place in a fund for a sum, open for " + SchemeCatalogue.FundAnswerDays + " days, and no second one while it stands");
        Check(SchemeRules.FundStands(s, 500 + SchemeCatalogue.FundAnswerDays) && !SchemeRules.FundStands(s, 501 + SchemeCatalogue.FundAnswerDays) && SchemeRules.FundCheck(s) && f.Checked
              && !SchemeRules.FundCheck(s) && !SchemeRules.FundReport(s),
            "schemes fund: an offer can be answered until its last day, looked at once, and there is nothing to report while it stands");
        Check(SchemeRules.FundPlace(s, 503) && f.State == FundState.Placed && f.Due == 503 + SchemeCatalogue.FundDays && !SchemeRules.FundPlace(s, 503) && !SchemeRules.FundDecline(s)
              && !SchemeRules.FundCheck(s),
            "schemes fund: money placed stays for " + SchemeCatalogue.FundDays + " days, and is placed once");
        int day = 503;
        int paid = SchemeUntil(s, ref day, 600, w, SchemeEventKind.FundPaid, out SchemeEvent pay);
        Check(paid == 503 + SchemeCatalogue.FundDays && pay.Amount == 12000 && pay.Net == 12960 && pay.Tycoon == f.Tycoon && f.State == FundState.Paid,
            "schemes fund: an honest fund pays the sum back with its return on its day");
        int next = SchemeUntil(s, ref day, 700, w, SchemeEventKind.FundOffered, out _);
        Check(next == 500 + SchemeCatalogue.FundEveryDays && f.Number == 2 && f.State == FundState.Open && !f.Checked,
            "schemes fund: the next offer comes " + SchemeCatalogue.FundEveryDays + " days after the last");
        int lapsed = SchemeUntil(s, ref day, 700, w, SchemeEventKind.FundLapsed, out _);
        Check(lapsed == next + SchemeCatalogue.FundAnswerDays + 1 && f.State == FundState.Declined, "schemes fund: an offer nobody answers lapses the day after its last, and nothing was paid");

        // A sham: the money is gone, the rival owes it, a report finds them, and part of it comes back.
        SchemeState sham = SchemeStateOf(shamSeed, 500, w);
        events.Clear();
        SchemeRules.FundNow(sham, 500, w, events);
        SchemeFund sf = sham.Fund;
        int rival = sf.Tycoon;
        SchemeRules.FundPlace(sham, 500);
        day = 500;
        int lost = SchemeUntil(sham, ref day, 600, w, SchemeEventKind.FundLost, out SchemeEvent gone);
        SchemeRival r = sham.Rival[rival];
        Check(sf.Sham && lost == 500 + SchemeCatalogue.FundDays && gone.Amount == 12000 && sf.State == FundState.Lost && r.Owes == 12000 && r.Acts >= 1 && r.Gained >= 12000,
            "schemes fund: a sham keeps the money on its day: it is a scheme on that rival's own trail, and they owe it");
        int heat = r.Heat;
        Check(SchemeRules.FundReport(sham) && sf.Reported && r.Heat - heat == SchemeCatalogue.HeatReported && !SchemeRules.FundReport(sham),
            "schemes fund: the player reports a sham to the police once, and it falls on the rival's trail as an offer reported does");
        var seen = new List<SchemeEventKind>();
        int back = SchemeUntil(sham, ref day, 700, w, SchemeEventKind.FundRecovered, out SchemeEvent recovered, seen);
        Check(back > lost && back <= lost + 1 + SchemeCatalogue.ChargeAfter + JusticeLaw.DaysToCourt(ChargeClass.Felony) + SchemeCatalogue.ChargeAfter
              && recovered.Amount == SchemeCatalogue.Share(12000, SchemeCatalogue.FundRecoverPermille) && recovered.Tycoon == rival && r.Owes == 0 && sf.State == FundState.Recovered
              && seen.Contains(SchemeEventKind.RivalFound),
            "schemes fund: the rival reported is found out, and " + SchemeCatalogue.FundRecoverPermille / 10 + "% of the sham's money comes back with their findings (" + (back - lost) + " days)");

        // What a rival still owes outlives the next offer.
        SchemeState owes = SchemeStateOf(shamSeed, 500, w);
        events.Clear();
        SchemeRules.FundNow(owes, 500, w, events);
        int debtor = owes.Fund.Tycoon;
        SchemeRules.FundPlace(owes, 500);
        day = 500;
        SchemeUntil(owes, ref day, 600, w, SchemeEventKind.FundLost, out _);
        owes.Rival[debtor].Heat = 0;
        events.Clear();
        SchemeRules.FundNow(owes, day, w, events);
        Check(owes.Fund.State == FundState.Open && owes.Fund.Number == 2 && owes.Rival[debtor].Owes == 12000 && !SchemeRules.FundReport(owes),
            "schemes fund: a new offer replaces the record of the last, but a rival still owes what a sham of theirs took");
        events.Clear();
        bool again = SchemeRules.RivalNow(owes, debtor, day, w, events);
        long cameBack = 0;
        foreach (SchemeEvent e in events)
        {
            cameBack += e.Kind == SchemeEventKind.FundRecovered ? e.Amount : 0;
        }
        Check(again && cameBack == SchemeCatalogue.Share(12000, SchemeCatalogue.FundRecoverPermille) && owes.Rival[debtor].Owes == 0 && owes.Fund.State == FundState.Open,
            "schemes fund: when that rival is found out later, the money comes back all the same");

        // No offer where none should be.
        foreach (string why in new[] { "a session", "the option off", "no rival", "no worth" })
        {
            SchemeWorld x = SchemeCityWorld(why != "the option off", why == "a session", why == "no worth" ? 0 : 600000);
            if (why == "no rival")
            {
                Array.Clear(x.RivalActive, 0, x.RivalActive.Length);
            }
            SchemeState n = SchemeStateOf(honestSeed, 500, x);
            events.Clear();
            Check(!SchemeRules.FundNow(n, 500, x, events) && n.Fund.State == FundState.None && events.Count == 0, "schemes fund: no fund is offered with " + why);
        }
        SchemeWorld resting = SchemeCityWorld(true, true);
        SchemeState held = SchemeStateOf(honestSeed, 500, w);
        events.Clear();
        SchemeRules.FundNow(held, 500, w, events);
        SchemeRules.FundPlace(held, 500);
        events.Clear();
        for (int d = 501; d <= 545; d++)
        {
            SchemeRules.Day(held, d, resting, events);
        }
        bool waits = events.Count == 0 && held.Fund.State == FundState.Placed && held.Fund.Due == 500 + SchemeCatalogue.FundDays;
        SchemeRules.Day(held, 546, w, events);
        Check(waits && events.Count == 1 && events[0].Kind == SchemeEventKind.FundPaid && held.Fund.State == FundState.Paid,
            "schemes fund: in a Going Public session a fund neither pays nor fails, and pays when the rivals are back");

        // The tests' door for a rival found out.
        SchemeState test = SchemeStateOf(honestSeed, 500, w);
        events.Clear();
        bool done = SchemeRules.RivalNow(test, 1, 500, w, events);
        SchemeRival tr = test.Rival[1];
        Check(done && events.Count == 2 && events[0].Kind == SchemeEventKind.RivalActed && events[1].Kind == SchemeEventKind.RivalFound
              && events[1].Amount == events[0].Amount + SchemeCatalogue.Share(events[0].Amount, SchemeCatalogue.SurchargePermille) && tr.Stage == InquiryStage.Charged
              && tr.TrialDay == 500 + JusticeLaw.DaysToCourt(ChargeClass.Felony) && tr.News == SchemeRules.RivalNewsFound && tr.NewsDay == 500 && tr.Gained == 0
              && !SchemeRules.RivalNow(test, 1, 500, w, events) && !SchemeRules.RivalNow(test, 9, 500, w, events),
            "schemes fund: a rival found out has what their schemes brought taken back with the same quarter on top, is tried when the court sets a felony's, and the paper has it");
        day = 500;
        int tried = SchemeUntil(test, ref day, 520, w, SchemeEventKind.RivalTried, out SchemeEvent verdict);
        Check(tried == tr.NewsDay && tried == 500 + JusticeLaw.DaysToCourt(ChargeClass.Felony) && tr.TrialDay == 0 && tr.Stage == InquiryStage.Closed
              && tr.News == (verdict.B != 0 ? SchemeRules.RivalNewsConvicted : SchemeRules.RivalNewsCleared) && tr.Convictions == verdict.B
              && verdict.B == (SchemeRules.Tried(honestSeed ^ 2 * 7919, tr.Opened, SchemeCatalogue.EvidenceMost, 0, SchemeCatalogue.RivalCounsel) ? 1 : 0),
            "schemes fund: a rival's verdict is the court's own sum on the evidence their inquiry gathered, the paper prints it, and their file is closed");
    }

    // ------------------------------------------------------------------------------------------------- the save

    /// <summary>A save with something in every field, each figure its own.</summary>
    private static SchemeState SchemeFullState()
    {
        var s = new SchemeState
        {
            Seed = 987654, StartDay = 12, Day = 345, NextInquiry = 9, Dirty = 4321, ClubTaken = 9000, ClubBack = 1234, ExpenseDay = 300, Acted = 17, Watched = true, Cautioned = 5
        };
        for (int k = 0; k < SchemeCatalogue.Kinds; k++)
        {
            s.Heat[k] = 100 + k * 37;
            s.Inquiries[k] = k + 1;
        }
        s.Acts.Add(new SchemeAct { Kind = SchemeKind.Claim, Day = 200, Gain = 4250, Back = 1000, Paid = 0, Size = 250, Knows = 0, Paper = true, Inquiry = 0, Ref = 7, Place = SchemePlace });
        s.Acts.Add(new SchemeAct { Kind = SchemeKind.Graft, Day = 210, Gain = 0, Back = 0, Paid = 6000, Size = 400, Knows = 1, Paper = false, Inquiry = 3, Ref = 3, Place = string.Empty });
        s.Acts.Add(new SchemeAct { Kind = SchemeKind.Wash, Day = 220, Gain = 816, Back = 816, Paid = 0, Size = 1000, Knows = 0, Paper = false, Inquiry = 4, Ref = 0, Place = "Oak|St\n~1" });
        s.Cases.Add(new SchemeInquiry
        {
            Id = 3, Kind = SchemeKind.Graft, Opened = 215, Stage = InquiryStage.Closed, Evidence = 640, Lost = true, Asked = true, Coop = true, Pressed = 150, Case = 44,
            End = InquiryEnd.Convicted, Closed = 240, Found = 0, Surcharge = 1500, Repaid = 0
        });
        s.Cases.Add(new SchemeInquiry { Id = 8, Kind = SchemeKind.Claim, Opened = 340, Stage = InquiryStage.Visit, Evidence = 270, Repaid = 1000 });
        s.Paid.Add(new SchemePaid { Who = "cseat 1 party 2 term 1", Day = 210, Amount = 6000, What = Favour.Vote, Talked = true });
        s.Paid.Add(new SchemePaid { Who = "p7", Day = 300, Amount = 800, What = Favour.Gift });
        s.Cold.Add("p9");
        s.Cold.Add("i101");
        s.Bought.Add(7);
        s.Bought.Add(31);
        s.Shops.Add(new SchemeShop { Place = SchemePlace, Lift = 100, Wash = 200, Window = 2400, WindowFrom = 330, Flagged = true, ClubDay = 280, ClaimDay = 200 });
        s.Shops.Add(new SchemeShop { Place = "Oak~1", Lift = 300, Wash = 50 });
        for (int i = 0; i < s.Rival.Length; i++)
        {
            SchemeRival r = s.Rival[i];
            r.Heat = 300 + i;
            r.Acts = 2 + i;
            r.LastAct = 320 + i;
            r.Gained = 50000 + i;
            r.Size = i == 1 ? 1000 : 250;
            r.Opened = 3 + i;
            r.Stage = i == 0 ? InquiryStage.Letter : i == 1 ? InquiryStage.Charged : InquiryStage.Closed;
            r.Since = 330 + i;
            r.Evidence = 400 + i;
            r.Convictions = i;
            r.TrialDay = i == 1 ? 347 : 0;
            r.Found = 60000 + i;
            r.NewsDay = 344 - i;
            r.News = i + 1;
            r.NewsKind = (SchemeKind)(i + 1);
            r.NewsAmount = 70000 + i;
            r.Owes = i == 2 ? 12000 : 0;
        }
        s.Fund.Tycoon = 2;
        s.Fund.State = FundState.Placed;
        s.Fund.Day = 320;
        s.Fund.Due = 352;
        s.Fund.Amount = 12000;
        s.Fund.Sham = true;
        s.Fund.Checked = true;
        s.Fund.Reported = true;
        s.Fund.Number = 6;
        return s;
    }

    private static void SchemeStoreChecks()
    {
        SchemeState full = SchemeFullState();
        string text = SchemeStore.Write(full);
        SchemeState read = SchemeStore.Read(text, out int version);
        string again = read != null ? SchemeStore.Write(read) : string.Empty;
        Check(read != null && version == SchemeStore.Version && again == text.Replace("Oak|St\n~1", "Oak St ~1").Replace("Oak St\n~1", "Oak St ~1") && again == SchemeStore.Write(SchemeStore.Read(again, out _)),
            "schemes save: a save with something in every field is written and read back the same, to the last figure");
        Check(read != null && read.Seed == 987654 && read.Dirty == 4321 && read.Watched && read.Cautioned == 5 && read.Heat[5] == 285 && read.Inquiries[5] == 6 && read.Acts.Count == 3
              && read.Acts[0].Gain == 4250 && read.Acts[0].Back == 1000 && read.Acts[1].Paid == 6000 && read.Acts[1].Inquiry == 3 && read.Acts[2].Place == "Oak St ~1" && read.Cases.Count == 2
              && read.Cases[0].Lost && read.Cases[0].Asked && read.Cases[0].Coop && read.Cases[0].Pressed == 150 && read.Cases[0].Case == 44 && read.Cases[0].End == InquiryEnd.Convicted
              && read.Cases[0].Surcharge == 1500 && read.Cases[1].Stage == InquiryStage.Visit && read.Cases[1].Repaid == 1000 && read.Paid.Count == 2 && read.Paid[0].Talked
              && read.Paid[0].What == Favour.Vote && read.Cold.Count == 2 && read.Bought.Count == 2 && read.Bought[1] == 31 && read.Shops.Count == 2 && read.Shops[0].Flagged
              && read.Shops[0].Window == 2400 && read.Shops[0].ClubDay == 280 && read.Shops[1].Lift == 300 && read.Rival[1].TrialDay == 347 && read.Rival[1].Stage == InquiryStage.Charged
              && read.Rival[2].Owes == 12000 && read.Rival[2].NewsKind == SchemeKind.Club && read.Rival[0].NewsAmount == 70000 && read.Fund.State == FundState.Placed && read.Fund.Sham
              && read.Fund.Checked && read.Fund.Reported && read.Fund.Number == 6 && read.Fund.Due == 352 && read.NextInquiry == 9,
            "schemes save: every part is read where it was written: the trails, the acts, the inquiries, the people, the businesses, the rivals and the fund");
        Check(text.StartsWith("SC" + SchemeStore.Version.ToString(CultureInfo.InvariantCulture) + "|", StringComparison.Ordinal) && SchemeStore.Key == "reallife:schemes"
              && text.Split('\n').Length == 2 + 3 + 2 + 2 + 2 + 2 + 2 + 3 + 1,
            "schemes save: one entry of the save, short lines, and a place's name cannot break one");

        // An old save, and nothing at all.
        var data = new Dictionary<string, string>();
        SchemeState fresh = SchemeStore.Load(data, 55, out int freshVersion);
        Check(fresh != null && fresh.Seed == 55 && fresh.Day == -1 && fresh.Acts.Count == 0 && fresh.Cases.Count == 0 && fresh.Fund.State == FundState.None && !fresh.CarriesOn
              && freshVersion == SchemeStore.Version && SchemeStore.Load(null, 56, out _).Seed == 56,
            "schemes save: a save from before the schemes loads with nothing in it and its own seed");
        data[SchemeStore.Key] = text.Replace("SC1|987654|", "SC1|0|");
        Check(SchemeStore.Load(data, 77, out _).Seed == 77, "schemes save: a line with no seed takes the save's own");
        SchemeStore.Save(data, full);
        Check(data[SchemeStore.Key] == text, "schemes save: the entry is the state's text");

        // A later version's line: read as far as this one knows it.
        var later = new StringBuilder();
        foreach (string line in again.Split('\n'))
        {
            later.Append(later.Length > 0 ? "\n" : string.Empty).Append(line.StartsWith("SC1|", StringComparison.Ordinal) ? "SC3|" + line.Substring(4) : line).Append("|99|more");
        }
        later.Append("\nZ|1|2|3\nA|77|1|1|1|1|1|1|1|1|1|x");
        SchemeState newer = SchemeStore.Read(later.ToString(), out int laterVersion);
        Check(newer != null && laterVersion == 3 && SchemeStore.Write(newer) == again,
            "schemes save: a later version's line is read as far as this one knows it: fields past the ones it knows and lines it does not know are left");

        // What is not the schemes' line is not read at all.
        Check(SchemeStore.Read("hello", out int none) == null && none == 0 && SchemeStore.Read(string.Empty, out _) == null && SchemeStore.Read(null, out _) == null
              && SchemeStore.Read("SC0|1|2", out _) == null && SchemeStore.Read("SCx|1|2", out _) == null && SchemeStore.Read("XX1|1|2", out _) == null,
            "schemes save: a line that is not the schemes' is not read, so the save keeps it as it was");

        // Another culture's numbers change nothing.
        CultureInfo before = CultureInfo.CurrentCulture;
        string foreign = string.Empty;
        bool readForeign = false;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            foreign = SchemeStore.Write(SchemeFullState());
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            readForeign = SchemeStore.Write(SchemeStore.Read(foreign, out _)) == again;
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
        Check(foreign == text && readForeign, "schemes save: numbers are written and read with the invariant culture, whatever the machine's");

        // A damaged line is brought within bounds, never trusted.
        var bad = new StringBuilder("SC1|5|1|9|0|-50|-7|-8|1|-3|1|-4\nH|99999|-5|1|1|1|1|-1|2|3|4|5|6");
        bad.Append("\nA|9|1|1|1|1|1|1|1|1|1|x\nA|0|10|100|500|-5|5000|-2|1|-3|4|Elm~7\nA|0|10");
        bad.Append("\nQ|1|0|5|1|9999|0|0|0|-5|-1|0|0|-9|-9|-9\nQ|1|0|5|1|10|0|0|0|0|0|0|0|0|0\nQ|2|1|6|2|10|0|0|0|0|0|0|0|0|0\nQ|5|0|6|2|10|0|0|0|0|0|0|0|0|0\nQ|3|9|6|2|10|0|0|0|0|0|0|0|0|0\nQ|4|2|6|7|10|0|0|0|0|0|0|0|0|0");
        for (int i = 0; i < 40; i++)
        {
            bad.Append("\nP|w").Append(i).Append("|1|5|0|0\nB|").Append(i).Append("\nB|").Append(i);
        }
        for (int i = 0; i < 90; i++)
        {
            bad.Append("\nC|c").Append(i).Append("\nC|c").Append(i);
        }
        bad.Append("\nP|w|1|5|9|0\nP||1|5|0|0\nB|-4\nS|Elm~7|120|999|-5|3|1|4|5\nS|Elm~7|300|50|0|0|0|0|0\nS||300|50|0|0|0|0|0");
        bad.Append("\nR|7|1|1|1|1|1|1|1|1|1|1|1|1|1|1|1|1\nR|0|99999|-1|1|-5|9999|-2|4|1|99999|-1|-1|-1|1|9|99|-1|-6\nR|1|500|1|1|1|1|1|9|1|1|1|1|1|1|1|1|1|1\nF|9|2|1|1|-5|1|1|1|-3\nF|1|77|1|1|5|1|1|1|3");
        SchemeState mended = SchemeStore.Read(bad.ToString(), out _);
        Check(mended != null && mended.NextInquiry == 3 && mended.Dirty == 0 && mended.ClubTaken == 0 && mended.ClubBack == 0 && mended.Acted == 0 && mended.Cautioned == 0
              && mended.Heat[0] == SchemeCatalogue.HeatMost && mended.Heat[1] == 0 && mended.Inquiries[0] == 0 && mended.Inquiries[1] == 2,
            "schemes save: money, heat and counts out of range are brought into range");
        Check(mended != null && mended.Acts.Count == 1 && mended.Acts[0].Gain == 100 && mended.Acts[0].Back == 100 && mended.Acts[0].Paid == 0 && mended.Acts[0].Size == SchemeCatalogue.Large
              && mended.Acts[0].Knows == 0 && mended.Acts[0].Inquiry == 0,
            "schemes save: an act of no kind or cut short is skipped, and one that says more was paid back than it brought is held to what it brought");
        Check(mended != null && mended.Cases.Count == 2 && mended.Cases[0].Id == 1 && mended.Cases[0].Evidence == SchemeCatalogue.EvidenceMost && mended.Cases[0].Pressed == 0
              && mended.Cases[0].Found == 0 && mended.Cases[0].Repaid == 0 && mended.Cases[1].Id == 2 && mended.Cases[1].Kind == SchemeKind.Expense && mended.Cases[1].Open,
            "schemes save: an inquiry's number is its own, a kind or a stage that does not exist is skipped, and never two are open for one kind");
        Check(mended != null && mended.Paid.Count == SchemeCatalogue.PaidMost && mended.Bought.Count == SchemeCatalogue.BoughtMost && mended.Cold.Count == SchemeCatalogue.ColdMost
              && mended.Shops.Count == 1 && mended.Shops[0].Lift == 100 && mended.Shops[0].Wash == 200 && mended.Shops[0].Window == 0,
            "schemes save: the people kept, the favours bought and the businesses are bounded, once each, with the books and the till on a step they have");
        Check(mended != null && mended.Rival[0].Heat == SchemeCatalogue.HeatMost && mended.Rival[0].Acts == 0 && mended.Rival[0].Gained == 0 && mended.Rival[0].Size == SchemeCatalogue.Large
              && mended.Rival[0].Evidence == SchemeCatalogue.EvidenceMost && mended.Rival[0].Stage == InquiryStage.Closed && mended.Rival[0].Owes == 0 && mended.Rival[0].News == 4
              && mended.Rival[1].Heat == 0 && mended.Fund.State == FundState.None && mended.Fund.Amount == 0,
            "schemes save: a rival's line and the fund's are read only when they name a rival there is and a state there is");
    }

    // ------------------------------------------------------------------------------------------------- a save and load on every day

    // A player with a full life of schemes, by a script that depends on the day and the save alone: what the director would do with each event.
    private static void SchemeScriptDay(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events, StringBuilder log)
    {
        events.Clear();
        SchemeRules.Day(s, day, w, events);
        foreach (SchemeEvent e in events)
        {
            log.Append(day).Append(':').Append((int)e.Kind).Append(':').Append(e.Inquiry).Append(':').Append(e.Tycoon).Append(':').Append(e.Amount).Append(':').Append(e.Net).Append(':')
                .Append(e.FromCash).Append(':').Append(e.Text).Append(';');
            SchemeInquiry q = e.Inquiry > 0 ? s.Inquiry(e.Inquiry) : null;
            if (e.Kind == SchemeEventKind.Letter && q != null && q.Kind == SchemeKind.Expense)
            {
                SchemeRules.Repay(s, q.Kind, day, null, out _);
            }
            if (e.Kind == SchemeEventKind.Letter && q != null && q.Kind == SchemeKind.Claim)
            {
                SchemeRules.Offer(s, Favour.Investigator, SchemeOffice.Investigator, SchemeRules.Investigator(q.Kind, s.Inquiries[(int)q.Kind]), 5000, false, day);
            }
            if (e.Kind == SchemeEventKind.Visit && q != null && q.Kind == SchemeKind.Books)
            {
                SchemeRules.Cooperate(q);
            }
            if (e.Kind == SchemeEventKind.FundOffered)
            {
                SchemeRules.FundCheck(s);
                SchemeRules.FundPlace(s, day);
            }
            if (e.Kind == SchemeEventKind.FundLost)
            {
                SchemeRules.FundReport(s);
            }
        }
        for (int i = 0; i < s.Cases.Count; i++)
        {
            SchemeInquiry q = s.Cases[i];
            if (q.Stage == InquiryStage.Charged && (day + q.Id) % 4 == 0)
            {
                SchemeRules.Close(s, q, (q.Id & 1) == 0 ? InquiryEnd.Convicted : InquiryEnd.Cleared, day);   // the court's verdict, some days on
            }
        }
        SchemeShop shop = s.Shop(SchemePlace, true);
        switch (day % 45)
        {
            case 3:
                SchemeRules.Expensed(s, day, 3000, day % 90 == 3 ? SchemeCatalogue.Small : SchemeCatalogue.Large);
                break;
            case 9:
                SchemeRules.Claimed(s, SchemePlace, day, 4250, SchemeCatalogue.Small, day);
                w.Unpaid.Add(day);
                break;
            case 14:
                w.Unpaid.Clear();
                break;
            case 20:
                SchemeRules.ClubSold(s, SchemePlace, 6000, 50, SchemeCatalogue.Small, day);
                shop.Wash = SchemeCatalogue.WashSteps[1 + day / 45 % 2];
                break;
            case 27:
                SchemeRules.Offer(s, Favour.Gift, SchemeOffice.Contact, SchemeRules.Contact(day % 13), 800, s.Dirty >= 800, day);
                break;
            case 33:
                shop.Lift = SchemeCatalogue.LiftSteps[1 + day / 45 % 2];
                break;
            case 40:
                SchemeRules.Sold(s, SchemePlace, 40000, day);
                break;
        }
    }

    private static void SchemeSaveLoadChecks()
    {
        const int days = 420;
        int seed = SchemeSeedWhere(k =>
        {
            SchemeWorld probe = SchemeCityWorld();
            probe.TaxPercent = 20;
            probe.Tills.Add(new SchemeTill { Place = SchemePlace, Average = 3000, Cash = true });
            var probeState = new SchemeState { Seed = k };
            var probeLog = new StringBuilder();
            var probeEvents = new List<SchemeEvent>();
            for (int d = 1; d <= days; d++)
            {
                SchemeScriptDay(probeState, d, probe, probeEvents, probeLog);
            }
            string all = probeLog.ToString();
            bool every = true;
            foreach (SchemeEventKind kind in new[]
                     {
                         SchemeEventKind.Washed, SchemeEventKind.Letter, SchemeEventKind.Visit, SchemeEventKind.Findings, SchemeEventKind.Caution, SchemeEventKind.RivalLetter,
                         SchemeEventKind.RivalFound, SchemeEventKind.RivalTried, SchemeEventKind.FundOffered, SchemeEventKind.FundPaid, SchemeEventKind.FundLost
                     })
            {
                every &= all.Contains(":" + ((int)kind).ToString(CultureInfo.InvariantCulture) + ":");
            }
            return every;
        });
        Check(seed != 0, "schemes save and load: a save is found in which a caution, findings, a rival's letter, findings and trial, and a fund paid and a fund lost all happen in " + days + " days");
        if (seed == 0)
        {
            return;
        }
        SchemeWorld w = SchemeCityWorld();
        w.TaxPercent = 20;
        w.Tills.Add(new SchemeTill { Place = SchemePlace, Average = 3000, Cash = true });
        var straight = new SchemeState { Seed = seed };
        var log = new StringBuilder();
        var events = new List<SchemeEvent>();
        var logAt = new int[days + 2];
        for (int d = 1; d <= days; d++)
        {
            logAt[d] = log.Length;
            SchemeScriptDay(straight, d, w, events, log);
        }
        string wanted = SchemeStore.Write(straight), wantedLog = log.ToString();
        int differs = 0, firstBad = 0, stages = 0;
        for (int cut = 1; cut < days; cut++)
        {
            SchemeWorld w2 = SchemeCityWorld();
            w2.TaxPercent = 20;
            w2.Tills.Add(new SchemeTill { Place = SchemePlace, Average = 3000, Cash = true });
            var s = new SchemeState { Seed = seed };
            var log2 = new StringBuilder();
            for (int d = 1; d <= cut; d++)
            {
                SchemeScriptDay(s, d, w2, events, log2);
            }
            stages += s.OpenOf(SchemeKind.Expense) != null || s.OpenOf(SchemeKind.Claim) != null || s.Rival[0].TrialDay > 0 || s.Fund.State == FundState.Placed ? 1 : 0;
            s = SchemeStore.Read(SchemeStore.Write(s), out _);
            for (int d = cut + 1; d <= days; d++)
            {
                SchemeScriptDay(s, d, w2, events, log2);
            }
            bool same = SchemeStore.Write(s) == wanted && log2.ToString() == wantedLog;
            differs += same ? 0 : 1;
            firstBad = !same && firstBad == 0 ? cut : firstBad;
        }
        Check(differs == 0 && stages > 100, "schemes save and load: saved and loaded on any of " + (days - 1) + " days, in the middle of inquiries, a rival's trial and a fund (" + stages
                                            + " such days), the save goes on exactly as if it never was: the same events on the same days, the same save at the end (first day that differs: "
                                            + firstBad + ")");
    }
}
