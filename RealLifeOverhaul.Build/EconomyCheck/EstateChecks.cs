using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// The property business's checks (Scripts/Economy/Estate): the market against the cycle, prices and the bank's rate;
// a purchase through the existing mortgage; rent and what the game is told to pay; applicants from a seed; a
// tenancy's days, arrears, notice and the court; wear, repairs and cover; works in a boom and in a slump; a sale, its
// fee and the tax on a gain; the agency's pipeline; a hotel's night, monotonic in every input and never above its
// rooms; car hire; the yearly returns the catalogue promises; the save's line; and that the same seed gives the same
// history, with a month of the whole city inside a millisecond.
internal static partial class Program
{
    private static readonly int[][] EstateGoing = { new[] { 45, 75 }, new[] { 150, 230 }, new[] { 480, 900 }, new[] { 210, 380 } };

    private static void Estate()
    {
        EstateCatalogueChecks();
        EstateMarket();
        EstateMortgage();
        EstateRent();
        EstateApplicants();
        EstateTenancy();
        EstateRepairs();
        EstateWorks();
        EstateSale();
        EstateAgency();
        EstateHotelDemand();
        EstateHotelNight();
        EstateHire();
        EstateReturns();
        EstateSaveAndLoad();
        EstateDeterminismAndTiming();
    }

    private static EstateClimate Climate(int cycle = 1, int index = 1000, int quarters = 0)
    {
        EstateClimate c = EstateClimate.Steady;
        c.Cycle = cycle;
        c.Index = index;
        c.Quarters = quarters;
        return c;
    }

    private static EstateHolding Building(int value = 1000000, int use = 1, int cond = 900, bool managed = true)
    {
        return new EstateHolding { Place = "High%20Street~" + value, Base = value, Use = use, AsBuilt = use, Income = 1000, Cond = cond, Managed = managed };
    }

    // A tenant is put in at a rent, with a record.
    private static EstateHolding Let(int rent, int record, int day = 1, int term = 90)
    {
        EstateHolding h = Building();
        h.Applicants.Add(new RentApplicant { Id = 1, Kind = 2, Offer = rent, Record = record, Term = term, Lapse = day + 4 });
        EstateRules.Accept(h, 1, day);
        return h;
    }

    private static void EstateCatalogueChecks()
    {
        Check(EstateCatalogue.RealTarget.Length == 4 && EstateCatalogue.ApplicantCycle.Length == 4 && EstateCatalogue.MissCycle.Length == 4 && EstateCatalogue.SaleCycle.Length == 4
              && EstateCatalogue.FallThrough.Length == 4 && EstateCatalogue.HotelCycle.Length == 4 && EstateCatalogue.HireCycle.Length == 4, "every table by cycle has four states");
        bool worse = true;
        for (int s = 1; s < 4; s++)
        {
            worse &= EstateCatalogue.RealTarget[s] < EstateCatalogue.RealTarget[s - 1] && EstateCatalogue.ApplicantCycle[s] < EstateCatalogue.ApplicantCycle[s - 1] && EstateCatalogue.MissCycle[s] > EstateCatalogue.MissCycle[s - 1]
                     && EstateCatalogue.SaleCycle[s] < EstateCatalogue.SaleCycle[s - 1] && EstateCatalogue.FallThrough[s] > EstateCatalogue.FallThrough[s - 1] && EstateCatalogue.HotelCycle[s] < EstateCatalogue.HotelCycle[s - 1]
                     && EstateCatalogue.HireCycle[s] < EstateCatalogue.HireCycle[s - 1];
        }
        Check(worse, "from boom to recession every table gets worse for an owner: values, applicants, missed rent, buyers, sales that fail, hotel guests, hirers");
        Check(EstateCatalogue.YieldMiddle.Length == EstateCatalogue.Uses && EstateCatalogue.Rooms.Length == EstateCatalogue.HotelTiers && EstateCatalogue.LeaseShare.Length == EstateCatalogue.HotelTiers
              && EstateCatalogue.StaffTier.Length == EstateCatalogue.HotelTiers && EstateCatalogue.BreakfastPerRoom.Length == EstateCatalogue.HotelTiers, "a figure for every use and every tier of hotel");
        Check(EstateCatalogue.LevelValue.Length == EstateCatalogue.LevelMost + 1 && EstateCatalogue.LevelRent.Length == EstateCatalogue.LevelMost + 1 && EstateCatalogue.LevelCost.Length == EstateCatalogue.LevelMost + 1
              && EstateCatalogue.LevelDays.Length == EstateCatalogue.LevelMost + 1, "a figure for every level of refurbishment");
        int cost = 0;
        bool same = true;
        for (int l = 1; l <= EstateCatalogue.LevelMost; l++)
        {
            cost += EstateCatalogue.LevelCost[l];
            same &= cost == EstateCatalogue.LevelValue[l] && EstateCatalogue.LevelRent[l] > EstateCatalogue.LevelRent[l - 1];
        }
        Check(same, "a level adds to the value the share of it that it costs, so that the market alone decides whether it pays");
        Check(EstateCatalogue.EventNight.Length == 8 && EstateCatalogue.HireEvent.Length == 8, "a figure for every kind of city event (the calendar has eight, 'none' included)");
        Check(EstateCatalogue.ReceptionWage.Length == EstateCatalogue.StaffLevels && EstateCatalogue.CleanTarget[2] > EstateCatalogue.CleanTarget[1] && EstateCatalogue.CleanTarget[1] > EstateCatalogue.CleanTarget[0]
              && EstateCatalogue.CleanTarget[0] < EstateCatalogue.InspectionPass && EstateCatalogue.CleanTarget[1] > EstateCatalogue.InspectionPass, "a basic contract fails the inspector, a standard one passes");
        Check(EstateCatalogue.HireRate.Length == EstateCatalogue.HireClasses && EstateCatalogue.HireDemand.Length == EstateCatalogue.HireClasses && EstateCatalogue.ClassUnder.Length == EstateCatalogue.HireClasses - 1,
            "a rate and a demand for every class of car");
        Check(EstateCatalogue.Share(1000, 15) == 15 && EstateCatalogue.Share(999, 500) == 500 && EstateCatalogue.Share(-1000, 15) == -15 && EstateCatalogue.Share(2000000000L, 4000) == int.MaxValue / 4,
            "a share is rounded to the nearest dollar and stays inside a whole number");
        Check(EstateCatalogue.Lever(-3) == 0 && EstateCatalogue.Lever(0) == 1 && EstateCatalogue.Lever(2) == 2 && EstateCatalogue.Cycle(CycleState.Recession) == 3 && EstateCatalogue.Cycle(CycleState.Boom) == 0,
            "a council's policy and a state of the cycle find their place in a table");
        Check(EstateCatalogue.ShopCapital.Length == 3 && EstateCatalogue.ShopYearlyBp.Length == 3 && EstateCatalogue.ShopYearlyBp[2] < EstateCatalogue.ShopYearlyBp[0], "the shop the balance compares with has three points in a game");
    }

    private static void EstateMarket()
    {
        Check(EstateRules.RealTarget(0, 0) == 1150 && EstateRules.RealTarget(1, 0) == 1000 && EstateRules.RealTarget(3, 0) == 820, "the market heads for the cycle's level");
        Check(EstateRules.RealTarget(1, 8) == 1000 - 8 * EstateCatalogue.RealPerQuarterPoint && EstateRules.RealTarget(1, -8) == 1000 + 8 * EstateCatalogue.RealPerQuarterPoint, "two points on the bank's rate take the target down, a cut puts it up");
        Check(EstateRules.RealTarget(3, 400) == EstateCatalogue.RealLeast && EstateRules.RealTarget(0, -400) == EstateCatalogue.RealMost, "the target stays inside its bounds whatever the rate");
        int real = 1000;
        bool stepOk = true;
        for (int m = 1; m <= 60; m++)
        {
            int next = EstateRules.Month(real, 7, m, 0, 0);
            stepOk &= Math.Abs(next - real) <= EstateCatalogue.RealStepMost;
            real = next;
        }
        Check(stepOk, "a month never moves the market by more than its most");
        Check(real > 1100 && real <= EstateCatalogue.RealMost, "five years of boom take the market near the boom's level (it stands at " + real + ")");
        for (int m = 61; m <= 120; m++)
        {
            real = EstateRules.Month(real, 7, m, 3, 8);
        }
        Check(real < 820 && real >= EstateCatalogue.RealLeast, "five years of recession with a high rate take it under the recession's level (it stands at " + real + ")");
        bool bounded = true;
        for (int seed = 1; seed <= 40; seed++)
        {
            int r = seed * 37 % 900 + 600;
            for (int m = 1; m <= 240; m++)
            {
                r = EstateRules.Month(r, seed, m, (m / 9 + seed) % 4, (m / 5 % 33) - 8);
                bounded &= r >= EstateCatalogue.RealLeast && r <= EstateCatalogue.RealMost;
            }
        }
        Check(bounded, "twenty years of months from forty seeds, through every state and rate: the market never leaves its bounds");
        Check(EstateRules.Month(1000, 5, 3, 1, 0) == EstateRules.Month(1000, 5, 3, 1, 0), "the same seed and month give the same move");

        Check(EstateRules.Value(1000000, 1000, 1000, 1000, 1000, 0) == 1000000, "a new building in a middling district, at the start's prices and a level market, is worth what the game says");
        Check(EstateRules.Value(1000000, 1150, 1000, 1000, 1000, 0) == 1150000 && EstateRules.Value(1000000, 820, 1000, 1000, 1000, 0) == 820000, "its value follows the market");
        Check(EstateRules.Value(1000000, 1000, 1200, 1000, 1000, 0) == 1200000, "and prices");
        Check(EstateRules.Value(1000000, 1000, 1000, 1400, 1000, 0) == 1160000 && EstateRules.Value(1000000, 1000, 1000, 600, 1000, 0) == 840000, "and its district's income against the middle");
        Check(EstateRules.Value(1000000, 1000, 1000, 1000, 0, 0) == 700000 && EstateRules.Value(1000000, 1000, 1000, 1000, 500, 0) == 850000, "a building at condition 0 keeps 70% of its value, at 50% it keeps 85%");
        Check(EstateRules.Value(1000000, 1000, 1000, 1000, 1000, 3) == 1180000, "three levels of refurbishment add 18%");
        Check(EstateRules.Value(0, 1000, 1000, 1000, 1000, 0) == 0 && EstateRules.Value(-5, 1000, 1000, 1000, 1000, 0) == 0, "nothing is worth nothing");
        Check(EstateRules.Value(2000000000, 1500, 4000, 1400, 1000, 3) == int.MaxValue / 4, "the largest value stays inside a whole number");
        Check(EstateRules.Value(1000000, 9999, 99999, 9999, 9999, 99) == EstateRules.Value(1000000, EstateCatalogue.RealMost, 4000, EstateCatalogue.IncomeMost, 1000, EstateCatalogue.LevelMost), "every input out of its bounds is put inside them");
        bool mono = true;
        for (int x = 600; x < 1500; x += 50)
        {
            mono &= EstateRules.Value(1000000, x + 50, 1000, 1000, 800, 1) > EstateRules.Value(1000000, x, 1000, 1000, 800, 1);
        }
        Check(mono, "a higher market is always a higher value");
        int runDown = 0;
        bool inRange = true;
        for (int place = 0; place < 4000; place++)
        {
            int cond = EstateRules.StreetCondition(99, place);
            runDown += cond <= EstateCatalogue.RunDownMost ? 1 : 0;
            inRange &= cond >= EstateCatalogue.RunDownLeast && cond <= EstateCatalogue.KeptMost && (cond <= EstateCatalogue.RunDownMost || cond >= EstateCatalogue.KeptLeast);
        }
        Check(inRange, "a building nobody has looked after is run down (30% to 60%) or kept up (65% to 100%)");
        Check(runDown > 850 && runDown < 1150, "about a quarter of the city is run down (" + runDown + " of 4000)");
        Check(EstateRules.StreetCondition(99, 12) == EstateRules.StreetCondition(99, 12), "the same building has the same condition whenever it is asked");
    }

    // A purchase at the mod's price through the mortgage the economy already has: nothing of the product is rewritten.
    private static void EstateMortgage()
    {
        int price = EstateRules.Value(800000, 1100, 1050, 1200, 500, 0);
        Check(price > 800000 && price < 900000, "a run-down building in a good district in a boom: " + price);
        int deposit = ProductTerms.Deposit(price, 20);
        int financed = ProductTerms.Financed(price, 20, price);
        Check(deposit + financed == price && deposit >= price / 5, "a mortgage with 20% down: the buyer's deposit and what the lender finances make the price");
        Check(ProductTerms.Financed(price, 20, financed / 2) == financed / 2, "an approval for less finances no more than was approved");
        Check(ProductTerms.DepositFor(financed, 20) >= deposit - 1, "the deposit the approval asked for covers the deposit on this price");
        CreditAccount a = AccountRules.Finance(ProductKind.Mortgage, financed, 720, 600, 10, "High Street#4", "High Street 4", price);
        Check(a.Owed == financed && a.Secured && a.SecurityValue == price && a.Term == 720, "the account opens for what was financed, with the building as its security at the price paid");
        Check(AccountRules.DueTomorrow(a, 10) >= financed / 720, "its daily instalment pays it off over the term");
        int rent = EstateRules.MarketRent(800000, 1, 1200, 500, 0, Climate(0, 1050));
        Check(rent > 0 && rent < AccountRules.DueTomorrow(a, 10) * 3, "the rent of the building and the instalment are of one scale: " + rent + " a day against " + AccountRules.DueTomorrow(a, 10));
    }

    private static void EstateRent()
    {
        Check(EstateRules.Yield(0, 1000) == 60 && EstateRules.Yield(1, 1000) == 68 && EstateRules.Yield(2, 1000) == 74 && EstateRules.Yield(3, 1000) == 84, "the gross yield of each use in a middling district");
        Check(EstateRules.Yield(1, 600) == 88 && EstateRules.Yield(1, 1400) == 48, "a poor district yields more, a dear one less");
        bool inBounds = true;
        for (int use = 0; use < 4; use++)
        {
            for (int income = 0; income <= 2000; income += 100)
            {
                int y = EstateRules.Yield(use, income);
                inBounds &= y >= EstateCatalogue.YieldLeast && y <= EstateCatalogue.YieldMost;
            }
        }
        Check(inBounds, "every yield is between 4% and 9% a year");
        EstateClimate c = Climate();
        Check(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, c) == 1000000 * 68 / 1000 / 60, "a day's rent is the yield on the game's value spread over the game's year");
        Check(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Climate(1, 1200)) == 1000000 * 68 / 1000 * 12 / 10 / 60, "rents follow prices");
        Check(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Climate(0)) == EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Climate(3)), "and not the cycle: a boom in values is a fall in yields");
        Check(EstateRules.MarketRent(1000000, 1, 1000, 500, 0, c) == 1000000 * 68 / 1000 * 8 / 10 / 60, "a building at 50% lets for 80% of its rent");
        Check(EstateRules.MarketRent(1000000, 1, 1000, EstateCatalogue.ConditionUnfit - 1, 0, c) == 0 && EstateRules.MarketRent(1000000, 1, 1000, EstateCatalogue.ConditionUnfit, 0, c) > 0, "under 25% it cannot be let at all");
        Check(EstateRules.MarketRent(1000000, 1, 1000, 1000, 3, c) == (int)(1000000L * 68 * 1210 / 1000 / 1000 / 60), "three levels add 21% to the rent");
        EstateClimate controlled = c, freed = c;
        controlled.RentLever = 1;
        freed.RentLever = -1;
        Check(EstateRules.MarketRent(1000000, 0, 1000, 1000, 0, controlled) < EstateRules.MarketRent(1000000, 0, 1000, 1000, 0, c) && EstateRules.MarketRent(1000000, 0, 1000, 1000, 0, freed) > EstateRules.MarketRent(1000000, 0, 1000, 1000, 0, c)
              && EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, controlled) == EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, c), "the council's rent policy moves the rent of homes and of nothing else");
        EstateClimate longYear = c;
        longYear.DaysPerYear = 120;
        Check(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, longYear) == EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, c) / 2, "a year twice as long halves a day's rent: the yield is a year's");
        float occupancy, price;
        EstateRules.Encode(1133, true, 2400, 100, out occupancy, out price);
        Check(occupancy == 100f && Math.Abs(price * 2400 * (occupancy / 100f) - 1133f) < 0.01f, "the game is told a share let and a rent a square metre that make exactly the agreed rent");
        EstateRules.Encode(1133, true, 2400, 99, out occupancy, out price);
        Check(occupancy == 99f && Math.Abs(price * 2400 * 0.99f - 1133f) < 0.01f, "also where the game lets one per cent less because the player uses a unit");
        EstateRules.Encode(1133, false, 2400, 100, out occupancy, out price);
        Check(occupancy == 0f && price == 0f, "a day that will not be paid is nothing let at no rent: the game pays nothing");
        EstateRules.Encode(1133, true, 0, 100, out occupancy, out price);
        Check(occupancy == 0f && price == 0f, "a building with no floor area is told as nothing rather than divided by nothing");
    }

    private static void EstateApplicants()
    {
        Check(EstateRules.ApplicantChance(1000, 900, 1) == EstateCatalogue.ApplicantChance, "at the market's rent in a steady economy, the plain chance of an applicant");
        Check(EstateRules.ApplicantChance(1200, 900, 1) < EstateRules.ApplicantChance(1000, 900, 1) && EstateRules.ApplicantChance(800, 900, 1) > EstateRules.ApplicantChance(1000, 900, 1), "asking more brings fewer, asking less brings more");
        Check(EstateRules.ApplicantChance(1000, 900, 3) < EstateRules.ApplicantChance(1000, 900, 1) && EstateRules.ApplicantChance(1000, 900, 0) > EstateRules.ApplicantChance(1000, 900, 1), "a recession brings fewer, a boom more");
        Check(EstateRules.ApplicantChance(1000, EstateCatalogue.ConditionPoor - 1, 1) == EstateCatalogue.ApplicantChance / 2 && EstateRules.ApplicantChance(1000, EstateCatalogue.ConditionUnfit - 1, 1) == 0,
            "a building in poor condition gets half the applicants, an unfit one none");
        bool bounded = true;
        for (int ask = 0; ask <= 3000; ask += 50)
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                int chance = EstateRules.ApplicantChance(ask, 900, cycle);
                bounded &= chance >= 0 && chance <= 1000;
            }
        }
        Check(bounded, "the chance is a chance whatever is asked");
        EstateClimate c = Climate();
        int came = 0, richer = 0;
        bool sane = true;
        for (int day = 1; day <= 2000; day++)
        {
            EstateHolding h = Building();
            RentApplicant a = EstateRules.Apply(h, 4242, day, c);
            if (a == null)
            {
                continue;
            }
            came++;
            int market = EstateRules.MarketRent(h, c);
            sane &= a.Record >= EstateCatalogue.RecordLeast && a.Record <= EstateCatalogue.RecordMost && a.Offer >= market * EstateCatalogue.OfferLeast / 1000 && a.Offer <= market * EstateCatalogue.OfferMost / 1000 + 1
                    && a.Term >= 60 && a.Term <= 180 && a.Term % 30 == 0 && a.Lapse == day + EstateCatalogue.ApplicantDays && a.Kind >= 0 && a.Kind < EstateCatalogue.TenantKinds && h.Applicants.Count == 1;
            richer += a.Record < 40 && a.Offer > market ? 1 : 0;
        }
        Check(came > 600 && came < 800, "over 2000 days somebody applies on about 35% of them (" + came + ")");
        Check(sane, "every applicant has a record, an offer, a term and a last day inside their bounds");
        Check(richer > 20, "applicants with a poor record bid over the market (" + richer + " did)");
        EstateHolding full = Building();
        for (int day = 1; full.Applicants.Count < EstateCatalogue.ApplicantsMost && day < 400; day++)
        {
            EstateRules.Apply(full, 9, day, c, true);
        }
        Check(full.Applicants.Count == EstateCatalogue.ApplicantsMost && EstateRules.Apply(full, 9, 500, c, true) == null, "three applications wait at most");
        Check(full.Applicants[0].Id != full.Applicants[1].Id && full.Applicants[1].Id != full.Applicants[2].Id, "each has a number of its own");
        Check(EstateRules.Lapse(full, full.Applicants[0].Lapse) == 0 && EstateRules.Lapse(full, 9999) == 3 && full.Applicants.Count == 0, "an application stands on its last day and is gone the day after");
        EstateHolding game = Building(1000000, 1, 900, false);
        Check(EstateRules.Apply(game, 9, 1, c, true) == null, "nobody applies for a building the game lets");
        EstateHolding unfit = Building(1000000, 1, 100);
        Check(EstateRules.Apply(unfit, 9, 1, c, true) == null, "or for one that is unfit");
        EstateHolding let = Let(1000, 80);
        Check(let.Let && let.Rent == 1000 && let.Deposit == 1000 * EstateCatalogue.DepositDays && let.Until == 91 && let.Applicants.Count == 0 && EstateRules.Apply(let, 9, 2, c, true) == null,
            "an accepted applicant is the tenant: the rent offered, a deposit held, a lease, and the others turned away");
        Check(!EstateRules.Accept(let, 1, 2) && !EstateRules.Accept(Building(), 7, 2), "nobody can be accepted twice, or who never applied");
    }

    private static void EstateTenancy()
    {
        EstateClimate c = Climate();
        Check(EstateRules.MissChance(95, 1) == 6 && EstateRules.MissChance(20, 1) == 96 && EstateRules.MissChance(20, 3) == 163 && EstateRules.MissChance(100, 3) == 0, "a poor record misses more days, and more again in a recession");
        Check(EstateRules.CatchUpChance(95) == 530 && EstateRules.CatchUpChance(20) == 230, "a good record pays arrears back sooner");
        Check(EstateRules.NoticeLength(-1) == 7 && EstateRules.NoticeLength(0) == 10 && EstateRules.NoticeLength(1) == 14, "notice is longer under rent controls");

        // ten years of a good tenant and of a poor one
        int goodPaid = 0, poorPaid = 0, goodMissed = 0, poorMissed = 0;
        EstateHolding good = Let(1000, 95, 1, 4000), poor = Let(1000, 20, 1, 4000);
        for (int day = 2; day <= 601; day++)
        {
            TenancyDay g = EstateRules.Day(good, 31, day, c), p = EstateRules.Day(poor, 31, day, c);
            goodPaid += g.Pays ? 1 : 0;
            poorPaid += p.Pays ? 1 : 0;
            goodMissed += g.Missed ? 1 : 0;
            poorMissed += p.Missed ? 1 : 0;
        }
        Check(goodPaid > 570 && goodMissed < 30, "a tenant with a good record pays nearly every day (" + goodPaid + " of 600)");
        Check(poorPaid < goodPaid && poorMissed > goodMissed * 3, "one with a poor record misses many more (" + poorMissed + " against " + goodMissed + ")");
        Check(good.Cond > poor.Cond && good.Cond < 900, "both wear the place, the poor one faster (" + good.Cond + " against " + poor.Cond + ")");

        // arrears, notice, the court
        EstateHolding h = Let(1000, 20, 1, 4000);
        h.Arrears = 3000;
        h.ArrearsDays = 3;
        Check(EstateRules.MayServeNotice(h, 5) && !EstateRules.MayGoToCourt(h, 5), "three days owed: notice may be served, the court not yet");
        EstateHolding early = Let(1000, 20, 1, 4000);
        early.Arrears = 2000;
        early.ArrearsDays = 2;
        Check(!EstateRules.MayServeNotice(early, 5) && !EstateRules.ServeNotice(early, 5, 0), "two days owed are not enough");
        Check(EstateRules.ServeNotice(h, 5, 0) && h.NoticeEnd == 15 && !EstateRules.ServeNotice(h, 6, 0), "notice runs ten days and is served once");
        Check(!EstateRules.MayGoToCourt(h, 15) && EstateRules.MayGoToCourt(h, 16), "the court can be asked the day after notice runs out");
        Check(EstateRules.GoToCourt(h, 16) && h.CourtDay == 16 + EstateCatalogue.CourtDays && !EstateRules.GoToCourt(h, 17), "the order comes six days after the application, which is made once");
        h.Arrears = 30000;   // (owed all the while)
        h.Cond = 800;
        TenancyDay order = EstateRules.Day(h, 31, h.CourtDay, c);
        Check(order.Evicted && !order.Pays && !h.Let && order.Lump == 8000 && order.WrittenOff == 22000, "on the day of the order the tenant is out: the deposit meets what it can of the arrears and the rest is written off");
        Check(order.Damage >= 0 && order.Damage <= EstateCatalogue.EvictionDamageMost && h.Cond == 800 - EstateCatalogue.WearLet - (100 - 20) / EstateCatalogue.WearPerRecord - order.Damage,
            "a tenant with a poor record who is put out may leave damage, never more than the most");
        Check(h.Rent == 0 && h.Deposit == 0 && h.Arrears == 0 && h.NoticeEnd == 0 && h.CourtDay == 0 && h.Tenant == -1, "an empty building owes and is owed nothing");
        TenancyDay voidDay = EstateRules.Day(h, 31, 40, c);
        Check(!voidDay.Pays && voidDay.Lump == 0 && h.VoidDays == 1, "an empty building counts its day and brings nothing");

        // arrears that are paid
        int caught = 0, lump = 0;
        bool paidOk = true, owedOk = true;
        for (int seed = 1; seed <= 200; seed++)
        {
            EstateHolding late = Let(1000, 80, 1, 4000);
            late.Arrears = 2000;
            late.ArrearsDays = 2;
            TenancyDay d = EstateRules.Day(late, seed, 5, c);
            if (d.Lump > 0)
            {
                caught++;
                lump = d.Lump;
                paidOk &= late.Arrears == 0 && late.ArrearsDays == 0 && d.Pays;
            }
            else
            {
                owedOk &= late.Arrears == 3000 && late.ArrearsDays == 3 && !d.Pays && d.Missed;
            }
        }
        Check(paidOk, "arrears paid in full: nothing is owed and the day's rent is paid again");
        Check(owedOk, "arrears not paid: another day is owed and nothing is paid");
        Check(caught > 70 && caught < 130 && lump == 2000, "a tenant with a fair record pays arrears back on about half the days (" + caught + " of 200)");

        // the end of a lease
        int renewed = 0, left = 0;
        bool endOk = true;
        for (int seed = 1; seed <= 300; seed++)
        {
            EstateHolding ending = Let(1000, 100, 1, 60);
            TenancyDay d = EstateRules.Day(ending, seed, 62, c);
            renewed += d.Renewed ? 1 : 0;
            left += d.Left ? 1 : 0;
            endOk &= d.Renewed ? ending.Until == 62 + EstateCatalogue.TermLeast && d.Pays : !ending.Let && d.Lump == 0;
        }
        Check(endOk, "at a lease's end the tenant renews for sixty days or goes with nothing owed");
        Check(renewed + left == 300 && renewed > 180 && renewed < 240, "about seven in ten renew (" + renewed + " of 300)");
        EstateHolding noticed = Let(1000, 100, 1, 60);
        Check(EstateRules.MayServeNotice(noticed, 62) && EstateRules.ServeNotice(noticed, 62, 0), "notice can be served once a lease has run out, with nothing owed");
        TenancyDay stays = EstateRules.Day(noticed, 3, 70, c);
        bool stillIn = noticed.Let;
        TenancyDay goes = EstateRules.Day(noticed, 3, 73, c);
        Check(stays.Pays && stillIn && goes.Left && goes.Lump == 0 && !noticed.Let, "a tenant under such notice pays to its last day and then goes");
    }

    private static void EstateRepairs()
    {
        Check(EstateRules.RepairCost(1000000, 0, 1000) == 50000 && EstateRules.RepairCost(1000000, 500, 1000) == 25000 && EstateRules.RepairCost(1000000, 1000, 1000) == 0, "a repair to new costs 5% of the value from nothing, in proportion from anywhere else");
        Check(EstateRules.RepairCost(1000000, 500, 1300) == 32500, "at today's prices");
        Check(EstateRules.RepairDays(1000, 0) == 1 && EstateRules.RepairDays(0, 0) == 6 && EstateRules.RepairDays(0, 1) < 6 && EstateRules.RepairDays(0, -1) > 6, "a repair takes one to six days, fewer when the council has eased its site hours");
        Check(EstateRules.BreakdownChance(1000) == EstateCatalogue.BreakdownBase && EstateRules.BreakdownChance(0) == EstateCatalogue.BreakdownBase + 20, "the worse the condition, the likelier something fails");
        Check(EstateRules.Premium(1000000, Climate()) == 1000000 * 4 / 1000 / 60 && EstateRules.Premium(0, Climate()) == 1, "a day of landlord's cover: 0.4% of the value a year, a dollar at least");
        EstateClimate c = Climate();
        int breakdowns = 0, fires = 0;
        bool lossOk = true, payOk = true;
        for (int day = 1; day <= 20000; day++)
        {
            EstateHolding h = Building(1000000, 1, 900);
            h.Cover = day % 2 == 0;
            EstateHazard z = EstateRules.Hazard(h, 77, day, c);
            breakdowns += z.Breakdown ? 1 : 0;
            fires += z.Fire ? 1 : 0;
            lossOk &= z.Loss == 900 - h.Cond && (!z.Breakdown || z.Loss == EstateCatalogue.BreakdownLoss) && (!z.Fire || z.Loss == EstateCatalogue.FireLoss) && !(z.Fire && z.Breakdown);
            payOk &= !z.Fire ? z.Payout == 0 && z.Bill == 0 : z.Bill == 15000 && z.Payout == (h.Cover ? 13500 : 0);
        }
        Check(breakdowns > 150 && breakdowns < 260, "at 90% something fails on about 1% of days (" + breakdowns + " of 20000)");
        Check(fires > 6 && fires < 40, "a fire or a flood on about one day in a thousand (" + fires + ")");
        Check(lossOk, "a failure takes 6% off the condition, a fire 30%, never both on a day");
        Check(payOk, "a fire's bill is the repair of what it took; cover pays it less a tenth, and nothing without cover");
        EstateHolding site = Building(1000000, 1, 500);
        EstateRules.Start(site, EstateWork.Repair, -1, 1, c);
        bool quiet = true;
        for (int day = 1; day <= 3000; day++)
        {
            EstateHazard z = EstateRules.Hazard(site, 77, day, c);
            quiet &= !z.Fire && !z.Breakdown;
        }
        Check(quiet && site.Cond == 500, "nothing goes wrong at a building while the builders have it");
        EstateHolding floor = Building(1000000, 1, 20);
        for (int day = 1; day <= 400; day++)
        {
            EstateRules.Day(floor, 3, day, c);
            EstateRules.Hazard(floor, 3, day, c);
        }
        Check(floor.Cond == 0, "condition never falls under nothing");
    }

    private static void EstateWorks()
    {
        EstateClimate c = Climate();
        EstateHolding h = Building(1000000, 1, 600);
        Check(EstateRules.Quote(h, EstateWork.Repair, -1, c) == 20000 && EstateRules.Quote(h, EstateWork.Refurbish, -1, c) == 50000, "an empty building the player lets himself: a repair and the first level are quoted");
        Check(EstateRules.Quote(h, EstateWork.Convert, -1, c) == 0 && EstateRules.Quote(h, EstateWork.Planning, 1, c) == 0 && EstateRules.Quote(h, EstateWork.Planning, 7, c) == 0, "no conversion without permission, and no permission for the use it has or one there is not");
        Check(EstateRules.Quote(h, EstateWork.Planning, 2, c) == 4000, "the council's fee for a change of use is 0.4% of the value");
        EstateHolding game = Building(1000000, 1, 600, false);
        Check(EstateRules.Quote(game, EstateWork.Repair, -1, c) == 20000 && EstateRules.Quote(game, EstateWork.Refurbish, -1, c) == 0, "a building the game lets can be repaired, not refurbished");
        EstateHolding occupied = Let(1000, 80);
        occupied.Cond = 600;
        Check(EstateRules.Quote(occupied, EstateWork.Repair, -1, c) > 0 && EstateRules.Quote(occupied, EstateWork.Refurbish, -1, c) == 0 && EstateRules.Quote(occupied, EstateWork.Planning, 2, c) == 0, "with a tenant in, repairs only");

        Check(EstateRules.Start(h, EstateWork.Repair, -1, 10, c) && h.Busy && h.WorkEnd == 10 + EstateRules.RepairDays(600, 0) && h.WorkCost == 20000, "a repair starts, with its last day and its cost");
        Check(!EstateRules.Start(h, EstateWork.Refurbish, -1, 10, c) && EstateRules.Quote(h, EstateWork.Refurbish, -1, c) == 0, "one job at a time");
        bool granted;
        Check(EstateRules.Finish(h, 5, h.WorkEnd - 1, c, out granted) == EstateWork.None && h.Busy, "a job is not finished before its day");
        Check(EstateRules.Finish(h, 5, 13, c, out granted) == EstateWork.Repair && !h.Busy && h.Cond == 1000 && h.Spent == 0, "a repair puts the condition back to new, and is no improvement against a gain");
        Check(EstateRules.Quote(h, EstateWork.Repair, -1, c) == 0, "a building as new needs no repair");

        Check(EstateRules.Start(h, EstateWork.Refurbish, -1, 20, c) && h.WorkEnd == 20 + EstateCatalogue.LevelDays[1], "the first level takes six days");
        Check(EstateRules.Finish(h, 5, 26, c, out granted) == EstateWork.Refurbish && h.Level == 1 && h.Spent == 50000, "and is then in place and counted as spent");
        // more than it costs in a boom, less in a slump
        EstateHolding plain = Building(1000000, 1, 1000);
        int cost = EstateRules.LevelCost(1000000, 1, 1000);
        int boom = EstateRules.LevelGain(plain, 1, 1150, 1000), level = EstateRules.LevelGain(plain, 1, 1000, 1000), slump = EstateRules.LevelGain(plain, 1, 820, 1000);
        Check(level == cost, "at a level market a refurbishment adds to the value exactly what it costs (" + level + ")");
        Check(boom > cost && boom == 57500, "in a boom it adds more than it costs (" + boom + " against " + cost + ")");
        Check(slump < cost && slump == 41000, "in a slump less (" + slump + ")");
        Check(EstateRules.LevelGain(plain, 1, 1150, 1300) > EstateRules.LevelCost(1000000, 1, 1300) && EstateRules.LevelGain(plain, 1, 820, 1300) < EstateRules.LevelCost(1000000, 1, 1300), "whatever prices have done since the start");
        for (int l = 2; l <= EstateCatalogue.LevelMost; l++)
        {
            EstateRules.Start(h, EstateWork.Refurbish, -1, 100 * l, c);
            EstateRules.Finish(h, 5, 100 * l + 50, c, out granted);
        }
        Check(h.Level == EstateCatalogue.LevelMost && EstateRules.Quote(h, EstateWork.Refurbish, -1, c) == 0 && h.Spent == 180000, "three levels and no more, 18% of the value spent in all");

        // a change of use
        int yes = 0, eased = 0, tight = 0;
        for (int day = 1; day <= 1000; day++)
        {
            yes += EstateRules.PlanningGranted(8, "A~1", day, 0) ? 1 : 0;
            eased += EstateRules.PlanningGranted(8, "A~1", day, 1) ? 1 : 0;
            tight += EstateRules.PlanningGranted(8, "A~1", day, -1) ? 1 : 0;
        }
        Check(yes > 650 && yes < 750 && eased > yes && tight < yes, "the council allows about seven in ten, more when it has eased permits and fewer when it has tightened them (" + tight + ", " + yes + ", " + eased + ")");
        EstateHolding shop = Building(1000000, 1, 1000);
        EstateRules.Start(shop, EstateWork.Planning, 2, 1, c);
        Check(shop.WorkEnd == 1 + EstateCatalogue.PlanningDays && shop.WorkTo == 2, "the council takes eight days");
        EstateWork decided = EstateRules.Finish(shop, 8, 9, c, out granted);
        Check(decided == EstateWork.Planning && shop.Use == 1 && shop.PlanUse == (granted ? 2 : -1), "its answer changes nothing built: the use is as it was, and the permission is there or is not");
        shop.PlanUse = 2;
        Check(EstateRules.Quote(shop, EstateWork.Convert, -1, c) == 90000 && EstateRules.Start(shop, EstateWork.Convert, -1, 20, c) && shop.WorkEnd == 20 + EstateCatalogue.ConvertDays, "with permission the conversion costs 9% and takes fourteen days");
        Check(EstateRules.Finish(shop, 8, 34, c, out granted) == EstateWork.Convert && shop.Use == 2 && shop.PlanUse == -1 && shop.Spent == 90000, "and the building is then let as offices");
        Check(EstateRules.SiteDays(14, 1) == 11 && EstateRules.SiteDays(14, -1) == 19 && EstateRules.SiteDays(0, 0) == 1, "a site works faster or slower by the council's hours, and a job takes a day at least");
    }

    private static void EstateSale()
    {
        Check(EstateRules.SaleChance(900000, 1000000, 1) == 500 && EstateRules.SaleChance(1000000, 1000000, 1) == 250 && EstateRules.SaleChance(1100000, 1000000, 1) == 90 && EstateRules.SaleChance(1300000, 1000000, 1) == 0,
            "the chance of an offer on a day at 90%, 100%, 110% and 130% of the value");
        Check(EstateRules.SaleChance(1050000, 1000000, 1) == 170 && EstateRules.SaleChance(500000, 1000000, 1) == 500 && EstateRules.SaleChance(5000000, 1000000, 1) == 0, "between those it runs straight, and beyond them it stays");
        Check(EstateRules.SaleChance(1000000, 1000000, 0) > 250 && EstateRules.SaleChance(1000000, 1000000, 3) == 125, "buyers are easier to find in a boom and half as many in a recession");
        Check(EstateRules.SaleChance(0, 1000000, 1) == 0 && EstateRules.SaleChance(1000000, 0, 1) == 0, "nothing listed, nothing offered");
        bool falls = true;
        for (int list = 800000; list < 1400000; list += 10000)
        {
            falls &= EstateRules.SaleChance(list + 10000, 1000000, 1) <= EstateRules.SaleChance(list, 1000000, 1);
        }
        Check(falls, "a dearer listing never has a better chance");
        int offers = 0;
        bool sane = true;
        for (int day = 1; day <= 1000; day++)
        {
            EstateHolding h = Building();
            h.List = 1000000;
            int offer = EstateRules.SaleOffer(h, 606, day, 1000000, 1);
            if (offer > 0)
            {
                offers++;
                sane &= offer >= 900000 && offer <= 1000000 && h.Offer == offer && h.OfferLapse == day + EstateCatalogue.SaleOfferDays && EstateRules.SaleOffer(h, 606, day + 1, 1000000, 1) == 0;
            }
        }
        Check(offers > 200 && offers < 300, "listed at its value, a building gets an offer on about a quarter of days (" + offers + " of 1000)");
        Check(sane, "an offer is 90% to 104% of the value, never more than is asked, stands three days, and there is one at a time");
        EstateHolding slump = Building();
        slump.List = 2000000;
        Check(EstateRules.SaleOffer(slump, 1, 1, 1000000, 3, true) <= 970000, "in a recession buyers offer less");
        EstateHolding sold = Building();
        sold.List = 1000000;
        sold.Applicants.Add(new RentApplicant { Id = 1, Offer = 5 });
        int taken = EstateRules.SaleOffer(sold, 1, 1, 1000000, 1, true);
        Check(!EstateRules.AcceptOffer(Building(), 1, 1, 1) && EstateRules.AcceptOffer(sold, 1, 2, 1) && sold.SaleDay == 2 + EstateCatalogue.CompletionDays && sold.SalePrice == taken && sold.Offer == 0 && sold.Applicants.Count == 0,
            "an accepted offer completes five days later at its price, and applicants are turned away");
        EstateHolding late = Building();
        late.List = 1000000;
        EstateRules.SaleOffer(late, 1, 1, 1000000, 1, true);
        Check(!EstateRules.AcceptOffer(late, 1, 1 + EstateCatalogue.SaleOfferDays + 1, 1), "an offer cannot be taken after its last day");
        Check(EstateRules.Quote(sold, EstateWork.Repair, -1, Climate()) == 0 && EstateRules.SaleOffer(sold, 1, 3, 1000000, 1, true) == 0, "once a sale is agreed there are no more works and no more offers");
        int fell = 0, fellRecession = 0;
        for (int seed = 1; seed <= 1000; seed++)
        {
            EstateHolding a = Building(), b = Building();
            a.List = b.List = 1000000;
            EstateRules.SaleOffer(a, seed, 1, 1000000, 1, true);
            EstateRules.SaleOffer(b, seed, 1, 1000000, 3, true);
            EstateRules.AcceptOffer(a, seed, 1, 1);
            EstateRules.AcceptOffer(b, seed, 1, 3);
            fell += a.SaleFails ? 1 : 0;
            fellRecession += b.SaleFails ? 1 : 0;
        }
        Check(fell > 70 && fell < 130 && fellRecession > 210 && fellRecession < 290, "one agreed sale in ten falls through, one in four in a recession (" + fell + " and " + fellRecession + " of 1000)");
        Check(EstateRules.AgentFee(1000000, 1000) == 15000 && EstateRules.AgentFee(10000, 1000) == 500 && EstateRules.AgentFee(300, 1000) == 300 && EstateRules.AgentFee(0, 1000) == 0, "the selling fee is 1.5% of the price, 500 at least, never more than the price");
        Check(EstateRules.AgentFee(10000, 2000) == 1000 && EstateRules.AgentFee(1000000, 2000) == 15000, "the selling fee's least follows prices; its share of the price does by itself");
        Check(EstateRules.GainsTax(1000000, 15000, 700000, 85000, 20) == 20000, "tax on a gain: half the game's rate on the price less the fee, the price paid and the improvements");
        Check(EstateRules.GainsTax(700000, 15000, 700000, 0, 20) == 0 && EstateRules.GainsTax(1000000, 0, 0, 0, 0) == 0 && EstateRules.GainsTax(1000000, 0, 0, 0, 500) == 500000, "nothing on a loss, nothing at no rate, and the rate is a rate");
    }

    private static AgencyFile Agency(int capacity = 80, int reputation = 300)
    {
        var agency = new AgencyFile { Office = "Office~1", Capacity = capacity };
        agency.Districts.Add(new DistrictRep { Name = "north", SqmPrice = 20000, Rep = reputation });
        agency.Districts.Add(new DistrictRep { Name = "south", SqmPrice = 12000, Rep = reputation });
        return agency;
    }

    private static void EstateAgency()
    {
        EstateClimate c = Climate();
        Check(AgencyRules.InstructionChance(0, 1) == 40 && AgencyRules.InstructionChance(1000, 1) == 240 && AgencyRules.InstructionChance(1000, 3) < AgencyRules.InstructionChance(1000, 1), "owners come by reputation and by the climate");
        Check(AgencyRules.Hours(true, 100) == 400 && AgencyRules.Hours(false, 100) == 170, "a sale of 100 square metres takes forty hours of a negotiator, a letting seventeen");
        Check(AgencyRules.Capacity(40, 70) == 40 && AgencyRules.Capacity(40, 100) == 57 && AgencyRules.Capacity(0, 100) == 0 && AgencyRules.Capacity(40, 0) == 0, "a negotiator on forty hours a week at 70% skill does four hours of agency work a day");
        Check(AgencyRules.MarketingCost(true, 2000000) == 1000 && AgencyRules.MarketingCost(false, 2000000) == 400 && AgencyRules.MarketingCost(false, 1000) == EstateCatalogue.MarketingLeast, "marketing costs a share of the value, fifty at least");
        Check(AgencyRules.Commission(true, 2000000) == 20000 && AgencyRules.Commission(false, 2000000) == 9600, "a sale pays 1% of the price, a letting 8% of a year's rent at 6%");
        Check(AgencyRules.DealChance(950, 0, 1) == 800 && AgencyRules.DealChance(1200, 0, 1) == 300 && AgencyRules.DealChance(1200, 1000, 1) == 350 && AgencyRules.DealChance(1200, 0, 3) == 150, "a deal is likelier the less the owner wants, with a name, and in good times");
        bool bounded = true;
        for (int ask = 0; ask <= 2000; ask += 50)
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                int chance = AgencyRules.DealChance(ask, ask, cycle);
                bounded &= chance >= EstateCatalogue.DealLeast && chance <= EstateCatalogue.DealMost;
            }
        }
        Check(bounded, "the chance of a deal stays between 5% and 95%");

        AgencyFile closed = Agency();
        closed.Office = string.Empty;
        AgencyFile idle = Agency(0);
        Check(AgencyRules.Arrive(closed, 3, 1, c, 1000, true) == null && AgencyRules.Arrive(idle, 3, 1, c, 1000) == null, "no owner comes to an agency with no office, or with nobody at work");
        AgencyFile agency = Agency();
        Instruction made = null;
        int came = 0;
        for (int day = 1; day <= 1000; day++)
        {
            AgencyFile fresh = Agency();
            Instruction i = AgencyRules.Arrive(fresh, 21, day, c, 1000);
            came += i != null ? 1 : 0;
            made = made ?? i;
        }
        Check(came > 60 && came < 150, "with a reputation of 30% an owner comes on about one day in ten (" + came + " of 1000)");
        Check(made != null && made.Stage == (int)InstructionStage.Offered && made.Sqm >= EstateCatalogue.UnitSqmLeast && made.Sqm <= EstateCatalogue.UnitSqmMost && made.Ask >= EstateCatalogue.OwnerAskLeast
              && made.Ask <= EstateCatalogue.OwnerAskMost && made.Hours == AgencyRules.Hours(made.Sale, made.Sqm) && made.Value == made.Sqm * (made.District == "north" ? 20000 : 12000),
            "an instruction has a size, a value by its district's prices, what the owner wants and the hours it takes");
        for (int n = 0; agency.Open.Count < EstateCatalogue.InstructionsMost && n < 500; n++)
        {
            AgencyRules.Arrive(agency, 21, 2000 + n, c, 1000, true);
        }
        Check(agency.Open.Count == EstateCatalogue.InstructionsMost && AgencyRules.Arrive(agency, 21, 9000, c, 1000, true) == null, "eight instructions are open at most");

        // the pipeline of one instruction
        AgencyFile one = Agency(100);
        var i1 = new Instruction { Id = 1, District = "north", Sale = true, Sqm = 100, Value = 2000000, Ask = 950, Stage = (int)InstructionStage.Offered, Hours = 400, Day = 4 };
        one.Open.Add(i1);
        var events = new List<AgencyEvent>();
        Check(AgencyRules.TakeCost(i1) == 1000 && AgencyRules.Take(i1, 2) && i1.Stage == (int)InstructionStage.Marketing && i1.Day == 2 + EstateCatalogue.InstructionLapseDays && !AgencyRules.Take(i1, 2),
            "taking an instruction on costs its marketing, starts its twenty days, and is done once");
        AgencyRules.Day(one, 5, 3, c, events);
        Check(i1.Done == 100 && events.Count == 0, "a day's hours go on the instruction");
        AgencyRules.Day(one, 5, 4, c, events);
        AgencyRules.Day(one, 5, 5, c, events);
        Check(i1.Done == 300 && one.Open.Count == 1, "day after day");
        AgencyRules.Day(one, 5, 6, c, events);
        Check(events.Count == 1 && (events[0].Kind == AgencyEvent.Agreed || events[0].Kind == AgencyEvent.NoDeal), "when its hours are done it ends in a deal or in none");
        int agreed = 0, completed = 0, fellThrough = 0, noDeal = 0, commission = 0;
        bool noDealOk = true, paidOk = true;
        for (int seed = 1; seed <= 400; seed++)
        {
            AgencyFile a = Agency(400);
            var i = new Instruction { Id = seed, District = "north", Sale = true, Sqm = 100, Value = 2000000, Ask = 950, Stage = (int)InstructionStage.Marketing, Hours = 400, Day = 30 };
            a.Open.Add(i);
            events.Clear();
            AgencyRules.Day(a, seed, 3, c, events);
            if (events[0].Kind == AgencyEvent.NoDeal)
            {
                noDeal++;
                noDealOk &= a.Open.Count == 0 && a.District("north").Rep == 300 + EstateCatalogue.RepNoDeal;
                continue;
            }
            agreed++;
            bool priced = i.Price == 2000000 * 95 / 100 && i.Day == 3 + EstateCatalogue.SaleCompletes && i.Stage == (int)InstructionStage.Completing;
            events.Clear();
            for (int day = 4; day <= 9; day++)
            {
                AgencyRules.Day(a, seed, day, c, events);
            }
            if (events.Count == 1 && events[0].Kind == AgencyEvent.Completed)
            {
                completed++;
                commission = events[0].Amount;
                paidOk &= priced && events[0].Amount == AgencyRules.Commission(true, i.Price) && a.Commission == events[0].Amount && a.Deals == 1 && a.District("north").Rep == 300 + EstateCatalogue.RepCompleted;
            }
            else
            {
                fellThrough++;
                paidOk &= events.Count == 1 && events[0].Kind == AgencyEvent.FellThrough && a.Commission == 0 && a.District("north").Rep == 300 + EstateCatalogue.RepFellThrough;
            }
        }
        Check(noDealOk, "no deal: the instruction is gone and the district thinks a little less of the agency");
        Check(paidOk, "a completion pays commission on the price agreed and makes the name in that district; a deal that falls through pays nothing");
        Check(agreed > 290 && agreed < 350 && noDeal == 400 - agreed, "at the lowest asking price about eight in ten end in a deal (" + agreed + " of 400)");
        Check(completed + fellThrough == agreed && fellThrough > agreed / 20 && fellThrough < agreed / 5 && commission == 19000, "of those about one in ten falls through and the rest pay (" + fellThrough + " fell, commission " + commission + ")");

        AgencyFile slow = Agency(0);
        slow.Open.Add(new Instruction { Id = 1, District = "south", Sale = false, Sqm = 60, Value = 720000, Ask = 1000, Stage = (int)InstructionStage.Marketing, Hours = 150, Day = 22 });
        slow.Open.Add(new Instruction { Id = 2, District = "south", Sale = false, Sqm = 60, Value = 720000, Ask = 1000, Stage = (int)InstructionStage.Offered, Hours = 150, Day = 22 });
        events.Clear();
        AgencyRules.Day(slow, 5, 22, c, events);
        Check(events.Count == 0 && slow.Open.Count == 2, "on their last day both still stand");
        AgencyRules.Day(slow, 5, 23, c, events);
        Check(events.Count == 2 && events[0].Kind == AgencyEvent.Lapsed && events[1].Kind == AgencyEvent.Withdrawn && slow.Open.Count == 0 && slow.District("south").Rep == 300 + EstateCatalogue.RepLapsed && slow.Lost == 1,
            "the day after, the one taken on and not brought to a deal is lost and costs the name; the one never answered is simply withdrawn");
        AgencyFile two = Agency(100);
        two.Open.Add(new Instruction { Id = 1, District = "north", Sale = false, Sqm = 60, Value = 100, Ask = 1000, Stage = (int)InstructionStage.Marketing, Hours = 150, Day = 99 });
        two.Open.Add(new Instruction { Id = 2, District = "north", Sale = false, Sqm = 60, Value = 100, Ask = 1000, Stage = (int)InstructionStage.Marketing, Hours = 150, Day = 99 });
        events.Clear();
        AgencyRules.Day(two, 5, 3, c, events);
        Check(two.Open[0].Done == 100 && two.Open[1].Done == 0, "the hours go to the oldest instruction first, and no hour is spent twice");
        AgencyRules.Reputation(two, "north", 5000);
        AgencyRules.Reputation(two, "south", -5000);
        AgencyRules.Reputation(two, "nowhere", 10);
        Check(two.District("north").Rep == 1000 && two.District("south").Rep == 0, "a reputation runs from nothing to 100%");
    }

    private static void EstateHotelDemand()
    {
        int plain = HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 1000, false, 0);
        int middling = HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 556, false, 0);
        Check(middling >= EstateCatalogue.Occupancy - 2 && middling <= EstateCatalogue.Occupancy, "on a spring weekday in a steady city, at the going rate and a middling rating, about 62% of the rooms are wanted (" + middling + ")");
        Check(plain == 744, "a perfect rating fills a fifth more");
        Check(HotelRules.Demand(1, 1, 0, 0, 1, 1000, 1000, 1000, false, 0) > plain && HotelRules.Demand(1, 3, 0, 0, 1, 1000, 1000, 1000, false, 0) < plain, "summer fills more, winter less");
        Check(HotelRules.Demand(3, 1, 0, 0, 1, 1000, 1000, 1000, false, 0) > HotelRules.Demand(1, 1, 0, 0, 1, 1000, 1000, 1000, false, 0) && HotelRules.Demand(3, 3, 0, 0, 1, 1000, 1000, 1000, false, 0) < HotelRules.Demand(1, 3, 0, 0, 1, 1000, 1000, 1000, false, 0),
            "the cottages swing harder with the season than the city's hotels");
        Check(HotelRules.Demand(1, 0, 4, 0, 1, 1000, 1000, 1000, false, 0) > plain && HotelRules.Demand(1, 0, 5, 0, 1, 1000, 1000, 1000, false, 0) > plain && HotelRules.Demand(1, 0, 6, 0, 1, 1000, 1000, 1000, false, 0) == plain,
            "Friday and Saturday nights fill more, Sunday does not");
        Check(HotelRules.Demand(1, 0, 0, 7, 1, 1000, 1000, 1000, false, 0) > HotelRules.Demand(1, 0, 0, 3, 1, 1000, 1000, 1000, false, 0) && HotelRules.Demand(1, 0, 0, 3, 1, 1000, 1000, 1000, false, 0) > plain
              && HotelRules.Demand(1, 0, 0, 4, 1, 1000, 1000, 1000, false, 0) < plain, "the fireworks fill most, a parade more than nothing, a protest less");
        Check(HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 1000, true, 0) > plain, "a campaign brings guests");
        bool cycle = true, income = true, rate = true, rating = true, noise = true;
        for (int s = 1; s < 4; s++)
        {
            cycle &= HotelRules.Demand(1, 0, 0, 0, s, 1000, 1000, 1000, false, 0) < HotelRules.Demand(1, 0, 0, 0, s - 1, 1000, 1000, 1000, false, 0);
        }
        for (int x = 600; x < 1400; x += 100)
        {
            income &= HotelRules.Demand(1, 0, 0, 0, 1, x + 100, 1000, 1000, false, 0) >= HotelRules.Demand(1, 0, 0, 0, 1, x, 1000, 1000, false, 0);
        }
        for (int x = EstateCatalogue.RateLeast; x < EstateCatalogue.RateMost; x += EstateCatalogue.RateStep)
        {
            rate &= HotelRules.Demand(1, 0, 0, 0, 1, 1000, x + EstateCatalogue.RateStep, 1000, false, 0) <= HotelRules.Demand(1, 0, 0, 0, 1, 1000, x, 1000, false, 0);
        }
        for (int x = 0; x < 1000; x += 50)
        {
            rating &= HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, x + 50, false, 0) >= HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, x, false, 0);
        }
        for (int x = -60; x < 60; x += 10)
        {
            noise &= HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 1000, false, x + 10) >= HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 1000, false, x);
        }
        Check(cycle, "from boom to recession fewer guests come at every step");
        Check(income, "a richer district never brings fewer");
        Check(rate, "a dearer room never brings more");
        Check(rating, "a better rating never brings fewer");
        Check(noise, "and a luckier night never fewer");
        Check(HotelRules.RateDemand(1000) == 1000 && HotelRules.RateDemand(1250) == 600 && HotelRules.RateDemand(1600) == EstateCatalogue.RateDemandLeast && HotelRules.RateDemand(600) == EstateCatalogue.RateDemandMost,
            "a quarter over the going rate loses four guests in ten; the dearest keeps a fifth; the cheapest brings half as many again");
        Check(HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 1000, false, 999) == HotelRules.Demand(1, 0, 0, 0, 1, 1000, 1000, 1000, false, EstateCatalogue.NightNoise), "a night's luck is never more than its most");
        bool never = true;
        for (int rooms = 0; rooms <= 60; rooms += 6)
        {
            for (int demand = 0; demand <= 3000; demand += 150)
            {
                int sold = HotelRules.Sold(rooms, demand);
                never &= sold >= 0 && sold <= rooms;
            }
        }
        Check(never, "the rooms sold are never more than the rooms there are, however many want one");
        Check(HotelRules.Sold(40, 620) == 25 && HotelRules.Sold(40, 2000) == 40 && HotelRules.Sold(0, 2000) == 0 && HotelRules.Sold(-4, 500) == 0 && HotelRules.Sold(40, -9) == 0, "25 of 40 at 62%, a full house when more want a room, none of none");
        int top = HotelRules.Demand(3, 1, 5, 7, 0, 1400, 600, 1000, true, 60);
        Check(top > 1000 && HotelRules.Sold(9, top) == 9, "the best night there can be wants more rooms than there are (" + top / 10 + "%), and sells them all");
        Check(HotelRules.Rooms(0) == 36 && HotelRules.Rooms(1) == 54 && HotelRules.Rooms(2) == 72 && HotelRules.Rooms(3) == 9 && HotelRules.Rooms(9, 9) == 3 && HotelRules.Rooms(-1, -1) == 24, "the rooms of each tier, and a tier or a type out of range is the nearest");
    }

    private static HotelFile Hotel(int tier = 1, int staff = 1)
    {
        var f = new HotelFile { Slot = "test" + tier, Tier = tier, Staff = staff, InspectDay = EstateCatalogue.InspectionEvery };
        f.Going[0] = EstateGoing[tier][0];
        f.Going[1] = EstateGoing[tier][1];
        return f;
    }

    private static void EstateHotelNight()
    {
        EstateClimate c = Climate();
        Check(HotelRules.Rate(150, 1000) == 150 && HotelRules.Rate(150, 1200) == 180 && HotelRules.Rate(150, 9000) == 240 && HotelRules.Rate(150, 0) == 90 && HotelRules.Rate(0, 1000) == 1, "a room's rate is the going rate by the player's share, 60% to 160% of it");
        Check(HotelRules.Costs(1, 1, 0, 1000) == 24 * 15 + 54 * 2 + 54 * 5, "an empty mid-range hotel still pays its reception round the clock, its maintenance and its utilities");
        Check(HotelRules.Costs(1, 1, 30, 1000) == HotelRules.Costs(1, 1, 0, 1000) + 30 * (12 + 4 + 12 + 2 + 2), "each room sold adds housekeeping, laundry, breakfast, utilities and wear");
        Check(HotelRules.Costs(1, 2, 30, 1000) > HotelRules.Costs(1, 1, 30, 1000) && HotelRules.Costs(1, 0, 30, 1000) < HotelRules.Costs(1, 1, 30, 1000), "a better contract costs more");
        Check(HotelRules.Costs(0, 1, 20, 1000) < HotelRules.Costs(1, 1, 20, 1000) && HotelRules.Costs(2, 1, 20, 1000) > HotelRules.Costs(1, 1, 20, 1000), "a hostel's staff cost less than a mid-range hotel's, a grand hotel's more");
        Check(HotelRules.Costs(1, 1, 30, 1300) == HotelRules.Costs(1, 1, 30, 1000) * 13 / 10, "at today's prices");
        Check(HotelRules.RatingTarget(1000, 2, 800) == 400 + 950 * 350 / 1000 + 250 && HotelRules.RatingTarget(0, 0, 1600) == 400 * 350 / 1000, "a rating is made of cleanliness, staff and price");
        Check(HotelRules.PriceScore(1000) == 700 && HotelRules.PriceScore(800) == 1000 && HotelRules.PriceScore(1600) == 0, "a cheap room pleases, a dear one does not");
        Check(HotelRules.Stars(0) == 1 && HotelRules.Stars(599) == 3 && HotelRules.Stars(600) == 4 && HotelRules.Stars(1000) == 5, "one to five stars");
        Check(HotelRules.Price(1, 150, 230, 1000, 60) == (int)((40L * 150 + 14 * 230) * 620 / 1000 * 60 * 4500 / 1000) && HotelRules.Price(1, 150, 230, 1150, 60) > HotelRules.Price(1, 150, 230, 1000, 60),
            "a hotel is asked four and a half years of its ordinary takings, more when the property market is up");
        int price = HotelRules.Price(1, 150, 230, 1000, 60);
        Check(HotelRules.LeaseDaily(1, price, 60) == (int)((long)price * 120 / 1000 / 60) && HotelRules.LeaseDeposit(1, price, 60) == HotelRules.LeaseDaily(1, price, 60) * 20, "a lease is 12% of the price a year for a mid-range hotel, with twenty days of it down");

        // sixty nights
        HotelFile f = Hotel();
        bool within = true, money = true;
        long takings = 0, costs = 0;
        int inspections = 0, reviews = 0;
        for (int day = 1; day <= 60; day++)
        {
            c.Weekday = (day - 1) % 7;
            c.Season = (day - 1) / 15;
            int out0 = f.OutEnd >= day ? f.OutRooms : 0;
            HotelNight n = HotelRules.Night(f, 500, day, c);
            if (f.AskRooms > 0)
            {
                HotelRules.RefuseGroup(f);
            }
            within &= n.Sold0 >= 0 && n.Sold0 <= 40 - out0 && n.Sold1 >= 0 && n.Sold1 <= 14 && f.Sold[0] == n.Sold0 && f.Rating >= 0 && f.Rating <= 1000 && f.Clean >= 0 && f.Clean <= 1000;
            money &= n.Takings == (n.Sold0 * 150 + n.Sold1 * 230) && n.Costs == HotelRules.Costs(1, 1, n.Sold0 + n.Sold1, 1000) && n.Lease == 0;
            takings += n.Takings;
            costs += n.Costs + n.PipeCost + n.Fine;
            inspections += n.Inspection != 0 ? 1 : 0;
            reviews += n.Review != 0 ? 1 : 0;
        }
        Check(within, "sixty nights: the rooms sold of each type are never more than there are to sell, and cleanliness and rating stay in their bounds");
        Check(money, "each night's takings are the rooms sold at their rates, and its costs those of the rooms sold");
        Check(takings > costs && f.TakingsYear == takings && f.CostsYear == costs, "a mid-range hotel at the going rate with a standard contract makes money (" + takings + " against " + costs + "), and the year's figures add up");
        Check(inspections == 2 && f.InspectDay == 90, "the inspector calls every thirty days");
        Check(reviews == 60 && f.Reviews[0] != 0 && f.Reviews[5] != 0, "a night with guests leaves a review, and the last six are kept");
        Check(f.Clean > 650 && f.Clean <= 720, "a standard contract keeps cleanliness near 72% (" + f.Clean / 10 + "%)");

        HotelFile basic = Hotel(1, 0), best = Hotel(1, 2);
        int fines = 0, bad = 0, fine = 0;
        for (int day = 1; day <= 60; day++)
        {
            HotelNight n = HotelRules.Night(basic, 500, day, c);
            HotelRules.Night(best, 500, day, c);
            fines += n.Inspection == 2 ? 1 : 0;
            bad += n.BadReview ? 1 : 0;
            fine = n.Inspection == 2 ? n.Fine : fine;
            HotelRules.RefuseGroup(basic);
            HotelRules.RefuseGroup(best);
        }
        Check(fine == 54 * EstateCatalogue.InspectionFinePerRoom, "a failed inspection fines a hotel by its rooms");
        Check(fines == 2 && bad > 0 && basic.Rating < f.Rating && best.Rating > f.Rating, "a basic contract fails both inspections, earns bad reviews and a lower rating; an excellent one a higher (" + basic.Rating + ", " + f.Rating + ", " + best.Rating + ")");

        // a group, a burst pipe, a lease
        c = Climate();
        HotelFile g = Hotel();
        Check(HotelRules.AskGroup(g, 500, 1) && g.AskRooms >= 6 && g.AskRooms <= 14 && g.AskNights >= 2 && g.AskNights <= 4 && g.AskRate == 113 && g.AskLapse == 3 && !HotelRules.AskGroup(g, 500, 1),
            "a group asks for 15% to 35% of the larger type for two to four nights at a quarter off, to be answered in two days, one group at a time");
        int rooms = g.AskRooms, nights = g.AskNights;
        Check(HotelRules.AcceptGroup(g) && g.GroupRooms == rooms && g.GroupNights == nights && g.AskRooms == 0 && !HotelRules.AcceptGroup(g), "accepted, its rooms are held for its nights");
        g.Rate[0] = g.Rate[1] = EstateCatalogue.RateMost;
        int groupRate = g.GroupRate;
        HotelNight withGroup = HotelRules.Night(g, 500, 2, c);
        Check(withGroup.Sold0 >= rooms && withGroup.Sold0 <= 40 && g.GroupNights == nights - 1 && withGroup.Takings >= rooms * groupRate, "a group's rooms are sold at its rate whatever the rate at the desk, and never more than the hotel has");
        HotelFile unanswered = Hotel();
        HotelRules.AskGroup(unanswered, 500, 1);
        HotelNight l1 = HotelRules.Night(unanswered, 500, 3, c), l2 = HotelRules.Night(unanswered, 12345, 4, c);
        Check(!l1.GroupLapsed && l2.GroupLapsed, "a group that is not answered within its days books elsewhere");
        HotelFile wet = Hotel();
        int bill = HotelRules.Pipe(wet, 10, 1000);
        Check(bill == 54 * EstateCatalogue.PipePerRoom && wet.OutRooms == 4 && wet.OutEnd == 12, "a burst pipe costs by the hotel's rooms and puts a tenth of the larger type out of use for two nights");
        wet.Rate[0] = EstateCatalogue.RateLeast;
        wet.Rate[1] = EstateCatalogue.RateLeast;
        wet.Rating = 1000;
        c.Weekday = 5;
        c.Event = 7;
        c.Season = 1;
        HotelNight full = HotelRules.Night(wet, 500, 11, c);
        Check(full.Sold0 == 36 && full.Sold1 == 14 && full.Full, "with four rooms out, a full house is thirty-six of the forty and all fourteen of the others");
        wet.OutRooms = 4;
        wet.OutEnd = 12;   // (whatever else the night brought)
        HotelRules.Night(wet, 500, 12, c);
        wet.OutEnd = wet.OutEnd > 12 ? 0 : wet.OutEnd;
        wet.OutRooms = wet.OutEnd == 0 ? 0 : wet.OutRooms;
        HotelNight after = HotelRules.Night(wet, 500, 13, c);
        Check(after.Sold0 > 36 || wet.OutEnd >= 13, "after the two nights the rooms are back (unless another pipe has burst)");
        c = Climate();
        HotelFile leased = Hotel();
        leased.Leased = true;
        leased.LeaseDaily = HotelRules.LeaseDaily(1, price, 60);
        HotelNight ln = HotelRules.Night(leased, 500, 1, c);
        Check(ln.Lease == leased.LeaseDaily && leased.Costs == ln.Costs + ln.Lease, "a leased hotel pays its lease every night on top of its costs");
        Check(HotelRules.CampaignCost(1, 1000) == 54 * EstateCatalogue.CampaignPerRoom && HotelRules.CampaignCost(1, 1500) == 54 * EstateCatalogue.CampaignPerRoom * 3 / 2, "a week's campaign costs by the rooms, at today's prices");
    }

    private static void EstateHire()
    {
        EstateClimate c = Climate();
        Check(HireRules.Class(8000) == 0 && HireRules.Class(15000) == 1 && HireRules.Class(39999) == 1 && HireRules.Class(40000) == 2 && HireRules.Class(100000) == 3 && HireRules.Class(0) == 0, "a car's class by its list price");
        Check(HireRules.Going(20000, 1000) == 108 && HireRules.Going(8000, 1000) == 46 && HireRules.Going(150000, 1000) == 690 && HireRules.Going(1000, 1000) == EstateCatalogue.HireRateLeast, "the going rate a day is about half a per cent of the list price, 25 at least");
        Check(HireRules.Going(20000, 1250) == 135 && HireRules.Rate(20000, 1500, 1000) == 162 && HireRules.Rate(20000, 100, 1000) == HireRules.Rate(20000, EstateCatalogue.RateLeast, 1000), "it follows prices, and the player's rate is 60% to 160% of it");
        int plain = HireRules.Demand(1, 0, 0, 1, 1000, 600);
        Check(plain == 480 * (800 + 600 * 350 / 1000) / 1000, "a standard car at the going rate goes out on nearly half its idle days at a middling rating");
        Check(HireRules.Demand(1, 1, 0, 1, 1000, 600) > plain && HireRules.Demand(1, 3, 0, 1, 1000, 600) < plain && HireRules.Demand(3, 1, 0, 1, 1000, 600) * 1000 / HireRules.Demand(3, 0, 0, 1, 1000, 600) > 1200,
            "more in summer, fewer in winter, and a luxury car most of all in summer");
        Check(HireRules.Demand(1, 0, 3, 1, 1000, 600) > plain && HireRules.Demand(1, 0, 1, 1, 1000, 600) == plain, "a parade hires cars, a street market does not");
        bool cycle = true, rate = true, rating = true, capped = true;
        for (int s = 1; s < 4; s++)
        {
            cycle &= HireRules.Demand(1, 0, 0, s, 1000, 600) < HireRules.Demand(1, 0, 0, s - 1, 1000, 600);
        }
        for (int x = EstateCatalogue.RateLeast; x < EstateCatalogue.RateMost; x += EstateCatalogue.RateStep)
        {
            rate &= HireRules.Demand(1, 0, 0, 1, x + EstateCatalogue.RateStep, 600) <= HireRules.Demand(1, 0, 0, 1, x, 600);
        }
        for (int x = 0; x < 1000; x += 50)
        {
            rating &= HireRules.Demand(1, 0, 0, 1, 1000, x + 50) >= HireRules.Demand(1, 0, 0, 1, 1000, x);
        }
        for (int k = 0; k < 4; k++)
        {
            capped &= HireRules.Demand(k, 1, 7, 0, EstateCatalogue.RateLeast, 1000) <= EstateCatalogue.HireDemandMost && HireRules.Demand(k, 3, 4, 3, EstateCatalogue.RateMost, 0) >= 0;
        }
        Check(cycle && rate && rating, "fewer hirers at each step from boom to recession, never more at a dearer rate, never fewer at a better rating");
        Check(capped, "the chance of a hire is never over 95% and never under nothing");
        Check(HireRules.Premium(20000, c) == 20000 * 25 / 1000 / 60 && HireRules.Premium(0, c) == 1 && HireRules.Cleaning(1000) == EstateCatalogue.HireCleaning && HireRules.Cleaning(1500) == 27, "fleet cover is 2.5% of the list price a year; cleaning follows prices");

        // a fleet of twelve over ten years: never more out than there are
        var fleet = new List<HireCar>();
        for (int i = 0; i < 12; i++)
        {
            fleet.Add(new HireCar { Id = "car" + i, Price = 9000 + i * 12000 });
        }
        bool never = true, sane = true, wear = true;
        int hires = 0, damaged = 0, late = 0, stolen = 0, outDays = 0, rating0 = 600;
        for (int day = 1; day <= 600; day++)
        {
            int outNow = 0;
            for (int i = 0; i < fleet.Count; i++)
            {
                HireCar car = fleet[i];
                if (car.State == (int)HireState.Valet)
                {
                    car.State = (int)HireState.Idle;
                }
                if (car.State == (int)HireState.Out && day >= car.Day)
                {
                    HireReturn r = HireRules.End(car, rating0, true, c);
                    HireReturn again = HireRules.End(car, rating0, true, c);
                    sane &= r.Rent == again.Rent && r.Km == again.Km && r.Damage == again.Damage && r.Stolen == again.Stolen;
                    hires++;
                    damaged += r.Damage > 0 ? 1 : 0;
                    late += r.Late ? 1 : 0;
                    stolen += r.Stolen ? 1 : 0;
                    sane &= r.Days == car.HireDays + (r.Late ? 1 : 0) && r.Rent >= car.HireRate * car.HireDays && r.Kept <= car.Deposit && r.Payout >= 0;
                    if (!r.Stolen)
                    {
                        wear &= r.Km >= r.Days * EstateCatalogue.HireKmLeast && r.Km <= r.Days * EstateCatalogue.HireKmMost && r.Fuel >= EstateCatalogue.HireFuelLeast && r.Fuel <= EstateCatalogue.HireFuelMost
                                && r.Damage <= EstateCatalogue.HireDamageMost && (r.Damage == 0 || (r.Damage >= EstateCatalogue.HireDamageLeast && r.Repair == EstateCatalogue.Share((long)EstateCatalogue.Share(car.Price, 200), r.Damage)))
                                && r.Cleaning == EstateCatalogue.HireCleaning && r.Kept == Math.Min(car.Deposit, r.Repair);
                    }
                    HireRules.Settle(car, r);
                    sane &= car.State == (int)HireState.Valet && car.HireDays == 0 && car.Deposit == 0;
                }
                else if (car.State == (int)HireState.Idle && HireRules.TryHire(car, 808, day, c, rating0))
                {
                    sane &= car.HireDays >= 1 && car.HireDays <= EstateCatalogue.HireDaysMost && car.HireRate == HireRules.Going(car.Price, 1000) && car.Deposit == car.HireRate * EstateCatalogue.HireDepositDays
                            && car.Day == day + car.HireDays && !HireRules.TryHire(car, 808, day, c, rating0, true);
                }
                outNow += car.State == (int)HireState.Out ? 1 : 0;
            }
            never &= outNow <= fleet.Count;
            outDays += outNow;
        }
        Check(never, "twelve cars over 600 days: never more out than there are");
        Check(sane, "a hire has one to four days, the going rate and a deposit of two days of it; a car that is out cannot be hired again; how it ends is the same however often it is asked");
        Check(wear, "a car comes back with 40 to 160 km a day on it, 15% to 70% of a tank, and damage of 5% to 30% of it or none; the deposit meets what it can of the repair");
        Check(hires > 700 && outDays * 100 / (600 * 12) > 30 && outDays * 100 / (600 * 12) < 75, "the fleet is out about half the time (" + outDays * 100 / (600 * 12) + "%, " + hires + " hires)");
        Check(damaged * 1000 / hires > 50 && damaged * 1000 / hires < 130 && late * 1000 / hires > 60 && late * 1000 / hires < 140, "about 9% of hires end with damage and 10% a day late (" + damaged + " and " + late + ")");
        Check(stolen <= hires / 60, "rarely a car does not come back (" + stolen + " of " + hires + ")");
        var gone = new HireCar { Id = "gone", Price = 20000, HireDays = 2, HireRate = 108, Deposit = 216 };
        HireReturn lost = default(HireReturn);
        for (int seed = 0; seed < 100000 && !lost.Stolen; seed++)
        {
            gone.HireSeed = seed;
            lost = HireRules.End(gone, 600, true, c);
        }
        Check(lost.Stolen && lost.Payout == 20000 * 7 / 10 * 8 / 10 - EstateCatalogue.HireCoverExcess && lost.Kept == 216 && lost.Km == 0 && lost.Rent == 216, "a car that does not come back: the hire and the deposit are kept, and cover pays 80% of what it was worth less its excess");
        Check(HireRules.End(gone, 600, false, c).Payout == 0, "and nothing without cover");
        var hit = new HireCar { Id = "hit", Price = 20000, HireDays = 2, HireRate = 108, Deposit = 216, HireSeed = 5 };
        HireReturn forced = HireRules.End(hit, 600, true, c, true), bare = HireRules.End(hit, 600, false, c, true);
        Check(forced.Damage >= EstateCatalogue.HireDamageLeast && !forced.Stolen && forced.Repair == 4000 * forced.Damage / 1000 && forced.Kept == Math.Min(216, forced.Repair)
              && forced.Payout == (forced.Repair - forced.Kept > 100 ? EstateCatalogue.Share(forced.Repair - forced.Kept - 100, 800) : 0) && bare.Payout == 0,
            "damage is put right at a fifth of the car's price for a write-off: the deposit first, then cover for 80% of the rest over its excess, and nothing without cover");
        Check(HireRules.Rated(600, true) == 606 && HireRules.Rated(600, false) == 570 && HireRules.Rated(998, true) == 1000 && HireRules.Rated(10, false) == 0, "a car in good order adds a little to the name, one that is not takes more away");
        var busy = new HireCar { Id = "x", Price = 20000, State = (int)HireState.Waiting };
        Check(!HireRules.TryHire(busy, 1, 1, c, 600, true) && !HireRules.TryHire(new HireCar { Id = "y" }, 1, 1, c, 600, true), "a car that waits for its space, or has no price, is not hired");
    }

    private static int Average(Func<int, int> of, int seeds)
    {
        long sum = 0;
        for (int s = 1; s <= seeds; s++)
        {
            sum += of(s * 977);
        }
        return (int)(sum / seeds);
    }

    // The yearly returns the catalogue's table promises, from the rules themselves.
    private static void EstateReturns()
    {
        int shops = Average(s => EstateBalance.LetYearlyBp(1, 1000, 1, s), 20), poorest = Average(s => EstateBalance.LetYearlyBp(1, 600, 1, s), 20), richest = Average(s => EstateBalance.LetYearlyBp(1, 1400, 1, s), 20);
        Check(shops > 300 && shops < 500, "a let shop building in a middling district returns 3% to 5% a year net (" + shops / 100f + "%)");
        Check(poorest > shops && poorest < 800 && richest < shops && richest > 100, "more in the poorest district and less in the richest, 1% to 8% (" + poorest / 100f + "% and " + richest / 100f + "%)");
        bool all = true;
        for (int use = 0; use < 4; use++)
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                int u = use, k = cycle;
                int bp = Average(s => EstateBalance.LetYearlyBp(u, 1000, k, s), 10);
                all &= bp > 200 && bp < 700;
            }
        }
        Check(all, "every use in every state of the cycle returns between 2% and 7% net in a middling district");
        Check(shops * 10 < EstateBalance.ShopYearlyBp(1), "a let building returns far less than another shop: it is the safe place for money, not the quick one");
        int[] bought = new int[4], leased = new int[4];
        for (int tier = 0; tier < 4; tier++)
        {
            int t = tier;
            bought[tier] = Average(s => EstateBalance.HotelYearlyBp(t, EstateGoing[t][0], EstateGoing[t][1], 1, false, 1, s), 10);
            leased[tier] = Average(s => EstateBalance.HotelYearlyBp(t, EstateGoing[t][0], EstateGoing[t][1], 1, true, 1, s), 10);
        }
        Check(bought[0] > 700 && bought[1] > 1200 && bought[2] > 1500 && bought[3] > 1200 && bought[0] < 2500 && bought[1] < 2500 && bought[2] < 2500 && bought[3] < 2500,
            "a hotel bought returns 9% to 22% of its price a year, by tier (" + bought[0] / 100f + ", " + bought[1] / 100f + ", " + bought[2] / 100f + ", " + bought[3] / 100f + "%)");
        Check(leased[0] > 0 && leased[1] > 0 && leased[2] > 0 && leased[3] > 0 && leased[0] < bought[0] && leased[1] < bought[1] && leased[2] < bought[2] && leased[3] < bought[3],
            "leased, every tier still makes money at the going rate, and less than bought (" + leased[0] / 100f + ", " + leased[1] / 100f + ", " + leased[2] / 100f + ", " + leased[3] / 100f + "% of the price)");
        Check(bought[2] * 3 < EstateBalance.ShopYearlyBp(2), "the best hotel returns under a third of what a shop is taken to: it is size, not speed");
        int recession = Average(s => EstateBalance.HotelYearlyBp(1, 150, 230, 1, true, 3, s), 10);
        Check(recession < leased[1] / 2, "a recession takes more than half of a leased hotel's return: the lease is owed whatever the night took (" + recession / 100f + "%)");
        Check(Average(s => EstateBalance.HotelYearlyBp(0, 45, 75, 2, false, 1, s), 10) < Average(s => EstateBalance.HotelYearlyBp(0, 45, 75, 1, false, 1, s), 10) + 200, "an excellent contract at a hostel brings little or nothing over a standard one");
        int[] hire = new int[4];
        int at = 0;
        foreach (int price in new[] { 8000, 25000, 60000, 150000 })
        {
            int p = price;
            hire[at++] = Average(s => EstateBalance.HireYearlyBp(p, 1, s), 40);
        }
        Check(hire[0] > 900 && hire[0] < 1700 && hire[1] > 900 && hire[1] < 1700 && hire[2] > 800 && hire[2] < 1500 && hire[3] > 700 && hire[3] < 1400,
            "a hire car returns 8% to 16% of its list price a year (" + hire[0] / 100f + ", " + hire[1] / 100f + ", " + hire[2] / 100f + ", " + hire[3] / 100f + "%)");
        Check(Average(s => EstateBalance.HireYearlyBp(25000, 3, s), 40) < hire[1] && Average(s => EstateBalance.HireYearlyBp(25000, 0, s), 40) > hire[1], "less in a recession, more in a boom");
        int agency = Average(s => EstateBalance.AgencyYearly(80, 20000, 1, s), 20), boom = Average(s => EstateBalance.AgencyYearly(80, 20000, 0, s), 20), slump = Average(s => EstateBalance.AgencyYearly(80, 20000, 3, s), 20);
        Check(agency > 25000 && agency < 90000, "the agency with two negotiators takes $25,000 to $90,000 a year before wages in a steady economy (" + agency + ")");
        Check(boom > agency && slump < agency / 2 && slump > -20000, "more in a boom, under half in a recession, when marketing is spent on deals that do not come (" + boom + " and " + slump + ")");
        Check(Average(s => EstateBalance.AgencyYearly(0, 20000, 1, s), 5) == 0, "an agency with nobody at work takes nothing and spends nothing");
    }

    private static EstateState EstateSample()
    {
        var s = new EstateState { Seed = 4711, FirstDay = 3, LastDay = 40, Real = 1080, Months = 7, YearFrom = 3 };
        EstateHolding h = Let(1200, 70, 10, 120);
        h.Paid = 950000;
        h.BoughtDay = 5;
        h.Spent = 50000;
        h.Level = 1;
        h.Ask = 1050;
        h.Cover = true;
        h.Arrears = 2400;
        h.ArrearsDays = 2;
        h.NoticeEnd = 44;
        h.RentYear = 30000;
        h.CostYear = 4000;
        s.Holdings.Add(h);
        var empty = new EstateHolding { Place = "Low%20Road~9", Managed = true, Use = 3, Cond = 420, Work = (int)EstateWork.Planning, WorkEnd = 46, WorkCost = 3000, WorkTo = 0, List = 800000, Offer = 770000, OfferLapse = 42, NextApplicant = 4 };
        empty.Applicants.Add(new RentApplicant { Id = 2, Kind = 5, Offer = 800, Record = 33, Term = 90, Lapse = 43 });
        empty.Applicants.Add(new RentApplicant { Id = 3, Kind = 1, Offer = 760, Record = 88, Term = 150, Lapse = 44 });
        s.Holdings.Add(empty);
        s.Agency.Office = "Office~1";
        s.Agency.NextId = 9;
        s.Agency.Commission = 41000;
        s.Agency.Deals = 3;
        s.Agency.Lost = 1;
        s.Agency.Districts.Add(new DistrictRep { Name = "ba:neighborhood_midtown", Rep = 410 });
        s.Agency.Districts.Add(new DistrictRep { Name = "ba:neighborhood_hellskitchen", Rep = 275 });
        s.Agency.Open.Add(new Instruction { Id = 7, District = "ba:neighborhood_midtown", Sale = true, Sqm = 120, Value = 2400000, Ask = 1100, Stage = (int)InstructionStage.Completing, Hours = 420, Done = 420, Day = 45, Price = 2500000, Fails = true });
        s.Agency.Open.Add(new Instruction { Id = 8, District = "ba:neighborhood_hellskitchen", Sqm = 60, Value = 700000, Ask = 980, Stage = (int)InstructionStage.Offered, Hours = 150, Day = 42 });
        HotelFile f = Hotel(2, 2);
        f.Slot = "midtown_grand";
        f.Leased = true;
        f.Paid = 300000;
        f.Day = 20;
        f.LeaseDaily = 16000;
        f.Rate[0] = 1100;
        f.Rate[1] = 900;
        f.Clean = 880;
        f.Rating = 790;
        f.PushEnd = 43;
        f.AskRooms = 12;
        f.AskNights = 3;
        f.AskRate = 360;
        f.AskLapse = 41;
        f.OutRooms = 6;
        f.OutEnd = 41;
        f.Sold[0] = 44;
        f.Sold[1] = 9;
        f.Takings = 31000;
        f.Costs = 22000;
        f.TakingsYear = 600000;
        f.CostsYear = 480000;
        f.Reviews[0] = 3;
        f.Reviews[1] = 6;
        s.Hotels.Add(f);
        s.Hire.Lot = "Lot~2";
        s.Hire.Rating = 655;
        s.Hire.Cover = true;
        s.Hire.TakingsYear = 9000;
        s.Hire.CostsYear = 1200;
        s.Hire.Cars.Add(new HireCar { Id = "aB3+/x=", Type = "ba:vehicletype_sedan", Price = 22000, Rate = 1150, State = (int)HireState.Out, Day = 42, HireDays = 3, HireRate = 136, Deposit = 272, HireSeed = 123456, X = -41250, Y = 12, Z = 99000, Yaw = 2715, Takings = 4000, Hires = 11 });
        s.Hire.Cars.Add(new HireCar { Id = "second", Type = "ba:vehicletype_van", Price = 31000 });
        return s;
    }

    private static void EstateSaveAndLoad()
    {
        EstateState s = EstateSample();
        string line = EstateStore.Write(s);
        int version;
        EstateState back = EstateStore.Read(line, out version);
        Check(back != null && version == EstateStore.Version, "a written line is read back");
        Check(back != null && EstateStore.Write(back) == line, "what is read back writes the same line again: nothing is lost or changed");
        Check(back != null && back.Holdings.Count == 2 && back.Holdings[0].Rent == 1200 && back.Holdings[0].Managed && back.Holdings[0].NoticeEnd == 44 && back.Holdings[1].Applicants.Count == 2 && back.Holdings[1].Applicants[1].Record == 88
              && back.Holdings[1].Work == (int)EstateWork.Planning && back.Holdings[1].Offer == 770000, "the buildings, their tenancies, works, listings and applicants");
        Check(back != null && back.Agency.Office == "Office~1" && back.Agency.Open.Count == 2 && back.Agency.Open[0].Fails && back.Agency.Open[0].Price == 2500000 && back.Agency.District("ba:neighborhood_midtown").Rep == 410,
            "the agency, its instructions and its name in each district");
        Check(back != null && back.Hotels.Count == 1 && back.Hotels[0].Leased && back.Hotels[0].Rate[0] == 1100 && back.Hotels[0].AskRooms == 12 && back.Hotels[0].Reviews[1] == 6 && back.Hotels[0].Sold[1] == 9, "the hotel");
        Check(back != null && back.Hire.Cars.Count == 2 && back.Hire.Cars[0].Id == "aB3+/x=" && back.Hire.Cars[0].X == -41250 && back.Hire.Cars[0].State == (int)HireState.Out && back.Hire.Cover && back.Hire.Rating == 655,
            "the hire lot and its cars, a car that is out with where it stood");
        string later = line.Replace("v=1^", "v=7^") + "^z:9|9|9";
        EstateState newer = EstateStore.Read(later.Replace("^g:Office~1|9|41000|3|1", "^g:Office~1|9|41000|3|1|77|88"), out version);
        Check(newer != null && version == 7 && EstateStore.Write(newer) == line, "a line from a later version, with fields and a part this one does not know, is read for what it knows and written as this version's");
        Check(EstateStore.Read(null, out version) == null && EstateStore.Read(string.Empty, out version) == null && EstateStore.Read("nonsense", out version) == null && EstateStore.Read("v=x^h:1", out version) == null
              && EstateStore.Read("v=0^h:1", out version) == null && EstateStore.Read("v=1^p:A~1|5", out version) == null, "a line that is not this feature's, or has no header, is left alone");
        EstateState wild = EstateStore.Read("v=1^h:5|-3|-9|99999|-1|-7^p:A~1|-4|-2|-9|17|5000|44|9999|1|1|3|500|-8|-8|-8|-8|-8|-8|-8|-8|99|-1|-1|-1|44|-5|-5|-5|-5|-5|1|-1|-1|-3|1|9^g:|0|-1|-1|-1^d:x|5000;x|4^i:0|x|1|1|1|1|1|1|1|1|1|1;3|x|1|0|-5|5|9|0|-1|-1|-1|0"
                                             + "^t:h|1|-5|-5|-5|10|99999|9|-4|5000|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|-1|99^c:|5000|1|-1|-1^v:a|t|-5|1|9|1|1|1|1|1|1|1|1|1|1|1;b|t|-5|99999|2|-1|-1|-1|-1|7|1|1|1|1|-1|-1", out version);
        EstateHolding w = wild != null && wild.Holdings.Count == 1 ? wild.Holdings[0] : null;
        Check(w != null && wild.Real == EstateCatalogue.RealMost && wild.FirstDay == 0 && w.Paid == 0 && w.Use == EstateCatalogue.Uses - 1 && w.Cond == 1000 && w.Level == EstateCatalogue.LevelMost && w.Ask == EstateCatalogue.AskMost
              && w.Tenant == 3 && w.Record == 100 && w.Rent == 0 && w.Work == 0 && w.PlanUse == EstateCatalogue.Uses - 1 && w.List == 0 && w.NextApplicant == 1 && w.Applicants.Count == 0,
            "a figure out of its bounds is put inside them, and a count of applicants with none behind it is none");
        Check(wild != null && wild.Agency.Districts.Count == 1 && wild.Agency.Districts[0].Rep == 1000 && wild.Agency.Open.Count == 0 && wild.Agency.NextId == 1,
            "a district is kept once, an instruction with no number or a stage there is not is dropped");
        Check(wild != null && wild.Hotels.Count == 1 && wild.Hotels[0].Rate[0] == EstateCatalogue.RateLeast && wild.Hotels[0].Rate[1] == EstateCatalogue.RateMost && wild.Hotels[0].Staff == EstateCatalogue.StaffLevels - 1
              && wild.Hotels[0].Rating == 1000 && wild.Hotels[0].Reviews[0] == HotelRules.ReviewDear, "a hotel's rates, staff, rating and reviews likewise");
        Check(wild != null && wild.Hire.Rating == 1000 && wild.Hire.Cars.Count == 1 && wild.Hire.Cars[0].Id == "b" && wild.Hire.Cars[0].Price == 0 && wild.Hire.Cars[0].Rate == EstateCatalogue.RateMost,
            "a car in a state there is not is dropped, and one with a rate out of bounds is given the nearest");
        Check(EstateStore.Clean("a|b;c^d") == "a b c d" && EstateStore.Clean(null) == string.Empty, "a name can never break the line apart");
        var big = new EstateState { Seed = 1 };
        for (int i = 0; i < 80; i++)
        {
            big.Holdings.Add(new EstateHolding { Place = "P~" + i });
            big.Hire.Cars.Add(new HireCar { Id = "c" + i });
            big.Agency.Open.Add(new Instruction { Id = i + 1, District = "d", Hours = 10, Ask = 1000, Sqm = 50 });
            big.Agency.Districts.Add(new DistrictRep { Name = "d" + i });
        }
        EstateState capped = EstateStore.Read(EstateStore.Write(big), out version);
        Check(capped != null && capped.Holdings.Count == EstateCatalogue.HoldingsMost && capped.Hire.Cars.Count == EstateCatalogue.HireCarsMost && capped.Agency.Open.Count == EstateCatalogue.InstructionsMost
              && capped.Agency.Districts.Count == EstateCatalogue.DistrictsMost, "the lists are capped when written");
        var data = new Dictionary<string, string> { { EstateStore.Key, line } };
        Check(EstateStore.Load(data, EstateStore.Key, out version) != null && EstateStore.Load(data, EstateStore.TestKey, out version) == null && EstateStore.Load(null, EstateStore.Key, out version) == null,
            "a save without the feature's line, or with no data of the mod's at all, has no state: the feature starts afresh");
        Check(EstateStore.Key == "reallife:economy_estate" && EstateStore.Version == 1, "the save key is the one the brief names, with a version");
        Check(EstateStore.Write(new EstateState { Seed = 3 }).StartsWith("v=1^h:3|0|0|1000|0|0^p:^g:|1|0|0|0^d:^i:^t:^c:|600|0|0|0^v:", StringComparison.Ordinal), "an empty state writes every part, empty");
    }

    // A history of the whole business from a seed, as one line of text.
    private static string EstateHistory(int seed, int days)
    {
        var s = new EstateState { Seed = seed, FirstDay = 1, Real = 1000 };
        for (int i = 0; i < 6; i++)
        {
            s.Holdings.Add(new EstateHolding { Place = "Street~" + i, Base = 600000 + i * 150000, Use = i % 4, AsBuilt = i % 4, Income = 700 + i * 120, Cond = EstateRules.StreetCondition(seed, i), Managed = true, Cover = i % 2 == 0 });
        }
        s.Agency.Office = "Office~1";
        s.Agency.Capacity = 80;
        s.Agency.Districts.Add(new DistrictRep { Name = "north", SqmPrice = 20000 });
        s.Agency.Districts.Add(new DistrictRep { Name = "south", SqmPrice = 12000 });
        s.Hotels.Add(Hotel(1, 1));
        s.Hire.Lot = "Lot~1";
        for (int i = 0; i < 4; i++)
        {
            s.Hire.Cars.Add(new HireCar { Id = "car" + i, Price = 12000 + i * 30000 });
        }
        var events = new List<AgencyEvent>();
        long money = 0;
        for (int day = 1; day <= days; day++)
        {
            EstateClimate c = Climate(day / 90 % 4, 1000 + day / 3, day / 40 % 9 - 2);
            c.Weekday = (day - 1) % 7;
            c.Season = (day - 1) / 15 % 4;
            c.Event = day % 11 == 0 ? 3 : 0;
            if (day % 5 == 0)
            {
                s.Months++;
                s.Real = EstateRules.Month(s.Real, seed, s.Months, c.Cycle, c.Quarters);
            }
            for (int i = 0; i < s.Holdings.Count; i++)
            {
                EstateHolding h = s.Holdings[i];
                bool granted;
                EstateRules.Finish(h, seed, day, c, out granted);
                EstateRules.Lapse(h, day);
                if (h.Applicants.Count > 0)
                {
                    EstateRules.Accept(h, h.Applicants[0].Id, day);
                }
                TenancyDay d = EstateRules.Day(h, seed, day, c);
                money += d.Lump + (d.Pays ? h.Rent : 0);
                EstateRules.Apply(h, seed, day, c);
                money += EstateRules.Hazard(h, seed, day, c).Payout;
                EstateRules.ServeNotice(h, day, c.RentLever);
                EstateRules.GoToCourt(h, day);
                if (!h.Let && h.Cond < 500 && EstateRules.Start(h, EstateWork.Repair, -1, day, c))
                {
                    money -= h.WorkCost;
                }
            }
            Instruction arrived = AgencyRules.Arrive(s.Agency, seed, day, c, s.Real);
            if (arrived != null)
            {
                money -= AgencyRules.TakeCost(arrived);
                AgencyRules.Take(arrived, day);
            }
            events.Clear();
            AgencyRules.Day(s.Agency, seed, day, c, events);
            for (int e = 0; e < events.Count; e++)
            {
                money += events[e].Kind == AgencyEvent.Completed ? events[e].Amount : 0;
            }
            HotelNight n = HotelRules.Night(s.Hotels[0], seed, day, c);
            money += n.Takings - n.Costs - n.PipeCost - n.Fine;
            if (s.Hotels[0].AskRooms > 0 && day % 2 == 0)
            {
                HotelRules.AcceptGroup(s.Hotels[0]);
            }
            for (int i = 0; i < s.Hire.Cars.Count; i++)
            {
                HireCar car = s.Hire.Cars[i];
                if (car.State == (int)HireState.Valet)
                {
                    car.State = (int)HireState.Idle;
                }
                else if (car.State == (int)HireState.Out && day >= car.Day)
                {
                    HireReturn r = HireRules.End(car, s.Hire.Rating, true, c);
                    money += r.Rent + r.Payout - r.Cleaning;
                    HireRules.Settle(car, r);
                }
                else if (car.State == (int)HireState.Idle)
                {
                    HireRules.TryHire(car, seed, day, c, s.Hire.Rating);
                }
            }
        }
        return EstateStore.Write(s) + "#" + money;
    }

    private static void EstateDeterminismAndTiming()
    {
        string a = EstateHistory(20260, 720), b = EstateHistory(20260, 720), other = EstateHistory(20261, 720);
        Check(a == b, "twelve years of the whole business from one seed, twice: the same history to the last figure");
        Check(a != other, "another seed is another history");
        string first = EstateHistory(20260, 300);
        string state = first.Substring(0, first.LastIndexOf('#'));
        int version;
        EstateState reloaded = EstateStore.Read(state, out version);
        Check(reloaded != null && EstateStore.Write(reloaded) == state, "a history's state goes through a save and comes back the same");
        bool rolls = true;
        int low = 0;
        for (int i = 0; i < 20000; i++)
        {
            int roll = EstateRules.Roll(31337, i, i % 7, i / 3);
            rolls &= roll >= 0 && roll < 1000;
            low += roll < 500 ? 1 : 0;
        }
        Check(rolls && low > 9500 && low < 10500, "a draw is 0 to 999, and half of them are under 500 (" + low + " of 20000)");
        Check(EstateRules.Between(0, 5, 9) == 5 && EstateRules.Between(999, 5, 9) == 9 && EstateRules.Between(500, 5, 9) == 7 && EstateRules.Between(123, 4, 4) == 4 && EstateRules.Between(123, 9, 2) == 9, "a draw between two numbers includes both, and a range of one or less is its first");
        Check(EstateRules.Hash("Fourth Avenue~45") == EstateRules.Hash("Fourth Avenue~45") && EstateRules.Hash("a") != EstateRules.Hash("b") && EstateRules.Hash(null) == EstateRules.Hash(string.Empty) && EstateRules.Hash("x") >= 0,
            "an address has one number, always the same and never negative");

        // A month for the whole city: the market's step and the value of every building (the game has some hundreds;
        // this is two thousand), and a day of forty buildings the player lets.
        var values = new int[2000];
        var owned = new List<EstateHolding>();
        for (int i = 0; i < EstateCatalogue.HoldingsMost; i++)
        {
            owned.Add(new EstateHolding { Place = "Owned~" + i, Base = 500000 + i * 40000, Use = i % 4, Income = 600 + i * 20, Cond = 400 + i * 15, Managed = true, Rent = 900, Tenant = i % 3 == 0 ? -1 : 2, Record = 60, Until = 99999 });
        }
        EstateClimate c = Climate(0, 1100, 4);
        double month = double.MaxValue, day = double.MaxValue;
        long sum = 0;
        for (int run = 0; run < 200; run++)
        {
            var watch = Stopwatch.StartNew();
            int real = EstateRules.Month(1000 + run, 77, run + 1, c.Cycle, c.Quarters);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = EstateRules.Value(300000 + i * 4100, real, c.Index, 600 + i % 800, EstateRules.StreetCondition(77, i), 0);
            }
            for (int i = 0; i < owned.Count; i++)
            {
                values[i] = EstateRules.Value(owned[i], real, c.Index);
            }
            watch.Stop();
            month = Math.Min(month, watch.Elapsed.TotalMilliseconds);
            sum += values[run];
            watch.Restart();
            for (int i = 0; i < owned.Count; i++)
            {
                EstateRules.Lapse(owned[i], run + 2);
                EstateRules.Day(owned[i], 77, run + 2, c);
                EstateRules.Apply(owned[i], 77, run + 2, c);
                EstateRules.Hazard(owned[i], 77, run + 2, c);
            }
            watch.Stop();
            day = Math.Min(day, watch.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine("  estate timing: a month of the market with 2000 building values " + month.ToString("0.000") + " ms, a day of " + owned.Count + " buildings the player lets " + day.ToString("0.000") + " ms (best of 200)");
        Check(month < 1.0 && sum > 0, "a month's step for a city of two thousand buildings takes under a millisecond (" + month.ToString("0.000") + " ms)");
        Check(day < 1.0, "a day of forty buildings the player lets takes under a millisecond (" + day.ToString("0.000") + " ms)");
    }
}
