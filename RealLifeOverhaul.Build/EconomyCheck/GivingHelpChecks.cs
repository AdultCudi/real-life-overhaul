using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

// Giving, the second pass (Scripts/Giving, run with --giving): the causes added to the first three (a night shelter, a scholarship fund, a care fund, a district's upkeep), gifts as big as
// the player's cash with a most that follows their wealth, a name on a hall for a big gift, the deduction carried forward over years, help for a person beyond a plain gift (the rent, a debt,
// a business, a course, a car, a home) with what each really changes in the person's household and self, who refuses and why, how leaning on the player's help builds and the no that
// costs nothing the first time, and every gift a bank line with a label. The rules are game-free and checked here; the director's order of work is checked in its sources.
internal static partial class Program
{
    private static void GivingHelpChecks()
    {
        GivingBigGiftChecks();
        GivingCarryChecks();
        GivingNewCauseChecks();
        GivingHelpRuleChecks();
        GivingHelpEffectChecks();
        GivingLeanChecks();
        GivingBusinessLoanChecks();
        GivingNewStoreChecks();
        GivingNewPageChecks();
        GivingHelpSourceChecks();
    }

    // ---- gifts as big as the cash, a most that follows wealth, a name on a hall

    private static void GivingBigGiftChecks()
    {
        Check(GivingRules.ShownSteps(0, 1000) == GivingCatalogue.StepsBase && GivingRules.ShownSteps(99999, 1000) == GivingCatalogue.StepsBase,
            "giving big: a player with little cash is shown the usual eleven steps, to 100,000");
        int shown = GivingRules.ShownSteps(3000000, 1000);
        Check(shown == 15 && GivingRules.StepNow(shown - 1, 1000) == 2500000 && GivingRules.StepNow(shown, 1000) == 5000000,
            "giving big: with three million to give the steps go on to two and a half million and stop before the first step over the cash (" + shown + " steps)");
        Check(GivingRules.ShownSteps(long.MaxValue / 4, 1000) == GivingCatalogue.Steps.Length && GivingRules.ShownSteps(3000000, 2000) == 14,
            "giving big: a fortune is shown every step, and prices that rise show fewer steps for the same cash");
        bool inside = true;
        for (int level = 500; level <= 100000; level *= 3)
        {
            for (int i = 0; i < GivingCatalogue.Steps.Length; i++)
            {
                int step = GivingRules.StepNow(i, level);
                inside &= step > 0 && step <= GivingCatalogue.AmountMost;
            }
        }
        Check(inside && GivingRules.At(int.MaxValue / 2, 100000) == GivingCatalogue.AmountMost, "giving big: a step at any price level is a whole sum that no int can overflow");
        Check(GivingRules.PersonMost(10000, 0) == 10000 && GivingRules.PersonMost(10000, 1) == 20000 && GivingRules.PersonMost(10000, 5) == 320000 && GivingRules.PersonMost(10000, 9) == 320000
              && GivingRules.PersonMost(10000, -3) == 10000, "giving big: the most a person can be given doubles with each wealth step the player has passed, to 320,000 at the fifth");
        Check(!GivingRules.IsNaming(GivingCatalogue.NamingFrom - 1) && GivingRules.IsNaming(GivingCatalogue.NamingFrom) && GivingRules.IsNaming(GivingCatalogue.Steps[GivingCatalogue.Steps.Length - 1]),
            "giving big: a gift of " + GivingCatalogue.NamingFrom + " or more takes the giver's name, and one under it never does");
        // The district's upkeep: standing by the sum, held to the most, and more custom for longer for a bigger gift.
        long prior = -1;
        bool rising = true, bounded = true;
        for (int amount = 0; amount <= 20000000; amount += amount < 100000 ? 250 : 50000)
        {
            int points = GivingRules.UpkeepPoints(amount);
            rising &= points >= prior;
            bounded &= points <= GivingCatalogue.UpkeepPointsMost;
            prior = points;
        }
        Check(rising && bounded && GivingRules.UpkeepPoints(0) == 0 && GivingRules.UpkeepPoints(100) == 0 && GivingRules.UpkeepPoints(GivingCatalogue.UpkeepHalf) == GivingCatalogue.UpkeepPointsMost / 2
              && GivingRules.UpkeepPoints(20000000) >= GivingCatalogue.UpkeepPointsMost - 1,
            "giving upkeep: the standing a district's upkeep earns rises with the gift, is half the most at " + GivingCatalogue.UpkeepHalf + " and never passes the most (" + GivingCatalogue.UpkeepPointsMost + ")");
        Check(GivingRules.UpkeepDays(999) == 0 && GivingRules.UpkeepDays(1000) == 5 && GivingRules.UpkeepDays(10000) == 10 && GivingRules.UpkeepDays(99999) == 10 && GivingRules.UpkeepDays(100000) == 20
              && GivingCatalogue.UpkeepBoost <= RealLifeOverhaul.Fame.FameCity.BoostMost && GivingCatalogue.UpkeepBoost == RealLifeOverhaul.Fame.FameCity.SponsorBoost,
            "giving upkeep: more custom in the district for 5, 10 or 20 days by the sum, by the same per cent as the city's own sponsored event and never over what Standing allows");
        // The paper: a naming leads, and a gift is printed the morning after, once.
        string facts = ModSource(@"Giving\GivingDirector.Facts.cs"), gifts = ModSource(@"Giving\GivingDirector.Gifts.cs"), game = ModSource(@"Giving\GivingGame.cs");
        Check(facts.Contains("n.Naming == 1 ||") && facts.Contains("n.Day != day - 1") && gifts.Contains("Naming = 1") && gifts.Contains("GivingBridges.Integrity(GivingCatalogue.NamingIntegrity)")
              && gifts.Contains("GivingBridges.Press(GivingCatalogue.NamingPress)"), "giving naming: a naming leads the City Ledger the morning after (and Flare posts the headlines), and earns once a little integrity and the press's attention through Standing's own figures");
        Check(gifts.Contains("bool naming = !standing && GivingRules.IsNaming(atStart);") && gifts.Contains("naming ? GivingGame.NamingKey : standing ? GivingGame.StandingKey"),
            "giving naming: only a single gift names a hall, never a standing one, and its bank line says it is a naming gift");
        Check(ModSource(@"Wiring\Wiring.GivingLinks.cs").Contains("into.Insert(1, item)") && ModSource(@"Wiring\FlareLinks.cs").Contains("for (int i = 1; today != null && i < today.Count && into.Count < 3; i++)"),
            "giving naming: the paper's lead goes after the masthead, where Flare's account of the City Ledger takes its first headlines");
        Check(game.Contains("CityDistricts.NewYork") && game.Contains("CityCheck.Here(CityDistricts.NewYork[i])"), "giving upkeep: the districts offered are the ones this city has, so another city or a free update's district never throws");
    }

    // ---- the deduction carried forward

    private static void GivingCarryChecks()
    {
        var s = new GivingState { PeriodStart = 0 };
        long held = 0, deducted = 0;
        Func<int, int, long> income = (from, to) => 310000L;   // a year's sales: the cap is 6% of it, 18,600
        Func<long> holds = () => held;
        Action<long> deduct = x => { held += x; deducted += x; };
        s.TaxGifts = 50000;
        long first = GivingYear.ClaimToday(s, 30, income, holds, deduct);
        Check(first == 18600 && s.TaxClaimed == 18600 && GivingYear.CarryLeft(s) == 0, "giving carry: a year's gifts over the cap are held to it (18,600 of 50,000) and nothing is carried until the year turns");
        Check(GivingYear.ClaimToday(s, 31, income, holds, deduct) == 0, "giving carry: the same day's gifts are never claimed twice");
        held = 0;
        Check(GivingYear.Turn(s, 60, 60) && s.PeriodStart == 60 && s.Carry[0] == 31400 && s.TaxGifts == 0 && s.TaxClaimed == 0 && s.CarryTaken == 0,
            "giving carry: the year turns and the 31,400 the cap held back is carried, the year's sums starting again");
        long second = GivingYear.ClaimToday(s, 90, income, holds, deduct);
        Check(second == 18600 && GivingYear.CarryLeft(s) == 12800 && s.CarryTaken == 18600, "giving carry: next year, with no new gift, the cap is used on the carry (18,600), and 12,800 is left");
        Check(GivingYear.ClaimToday(s, 91, income, holds, deduct) == 0 && GivingYear.CarryLeft(s) == 12800, "giving carry: the carry is taken once however many times the day's claim is made");
        held = 0;
        GivingYear.Turn(s, 120, 60);
        long third = GivingYear.ClaimToday(s, 150, income, holds, deduct);
        Check(third == 12800 && GivingYear.CarryLeft(s) == 0, "giving carry: the third year takes what is left, no more than it is");
        // This year's gifts are used before the carry, the oldest carry first, and total relief never passes the gifts.
        var t = new GivingState { PeriodStart = 0 };
        long tHeld = 0, tTotal = 0, gave = 0;
        Func<long> tHolds = () => tHeld;
        Action<long> tDeduct = x => { tHeld += x; tTotal += x; };
        long[] givenIn = { 100000, 0, 20000, 0, 0, 0, 0, 0 };
        bool never = true;
        for (int year = 0; year < givenIn.Length; year++)
        {
            if (year > 0)
            {
                GivingYear.Turn(t, year * 60, 60);
                tHeld = 0;
            }
            t.TaxGifts += givenIn[year];
            gave += givenIn[year];
            long claim = GivingYear.ClaimToday(t, year * 60 + 40, income, tHolds, tDeduct);
            never &= claim <= 18600 && tHeld <= 18600 && tTotal <= gave;
        }
        // 100,000 and 20,000 over eight years at 18,600 a year: five years' carry lapse the oldest, so not all of it is ever claimed.
        Check(never && tTotal > 18600 * 5 && tTotal < gave, "giving carry: eight years of two gifts are claimed at the cap a year, never over it and never over what was given, and what outlives " + GivingCatalogue.CarryYears + " years lapses (" + tTotal + " of " + gave + ")");
        var u = new GivingState { PeriodStart = 0, TaxGifts = 10000 };
        for (int year = 1; year <= GivingCatalogue.CarryYears + 2; year++)
        {
            GivingYear.Turn(u, year * 60, 60);
        }
        Check(GivingYear.CarryLeft(u) == 0, "giving carry: a carry nobody could use lapses after " + GivingCatalogue.CarryYears + " years");
        var v = new GivingState { PeriodStart = 0, TaxGifts = 10000 };
        long vHeld = 0;
        GivingYear.Turn(v, 60, 60);
        long claimNoIncome = GivingYear.ClaimToday(v, 70, null, () => vHeld, x => vHeld += x);
        Check(claimNoIncome == 0 && GivingYear.CarryLeft(v) == 10000, "giving carry: with no income known nothing is claimed and nothing is lost");
        // The game's list: a claim that made the list hold more than the state records is never claimed again.
        var w = new GivingState { PeriodStart = 0, TaxGifts = 5000 };
        long wHeld = 5000;
        Check(GivingYear.ClaimToday(w, 30, income, () => wHeld, x => wHeld += x) == 0 && w.TaxClaimed == 5000, "giving carry: what the game's list holds under giving's name is the floor of what is claimed");
        string yearSource = ModSource(@"Giving\GivingYear.cs");
        Check(yearSource.Contains("CarryOn(s);") && yearSource.Contains("DrawCarry(s);") && yearSource.IndexOf("CarryOn(s);", StringComparison.Ordinal) < yearSource.IndexOf("s.TaxGifts = 0;", StringComparison.Ordinal),
            "giving carry: the carry is worked out before the year's sums start again");
    }

    // ---- the new causes on a city

    private static void GivingNewCauseChecks()
    {
        int level = 1000;
        // The shelter: tenants the rent is about to drive out keep their homes at a rent they can bear.
        PopulationState a = ShelterCity(17), b = ShelterCity(17), clean = ShelterCity(17);
        int keptA = GivingPeople.PayShelter(a, 1000000, level, out long spentA);
        int keptB = GivingPeople.PayShelter(b, 1000000, level, out long spentB);
        Check(keptA > 0 && keptA <= GivingCatalogue.HelpedMost && spentA > 0 && spentA <= (long)keptA * GivingCatalogue.ShelterMost, "giving shelter: it keeps the worst few households in their homes, within its most for each (" + keptA + " for " + spentA + ")");
        Check(keptA == keptB && spentA == spentB, "giving shelter: the same city and money give the same month on every machine");
        bool held = true, costs = true, untouched = true, burdens = true;
        long total = 0;
        int monthDays = GenerateRules.MonthDays(a.YearDays);
        for (int h = 0; h < a.Households.Count; h++)
        {
            HouseholdTable ha = a.Households, hc = clean.Households;
            bool changed = ha.Rent[h] != hc.Rent[h] || ha.Flags[h] != hc.Flags[h];
            if (!changed)
            {
                untouched &= ha.Content[h] == hc.Content[h] && ha.Savings[h] == hc.Savings[h] && ha.Debt[h] == hc.Debt[h];
                continue;
            }
            bool bad = (ha.Flags[h] & (HouseholdFlags.Players)) != 0 || ha.Landlord[h] != Landlords.City;
            untouched &= !bad;
            held &= (ha.Flags[h] & HouseholdFlags.Regulated) != 0 && (ha.Flags[h] & HouseholdFlags.WantsMove) == 0 && ha.Rent[h] < hc.Rent[h] && ha.Content[h] == Math.Min(100, hc.Content[h] + GivingCatalogue.ShelterContent);
            long bearable = GivingPeople.MonthIncome(clean, h) * PopulationCatalogue.RentBurdenMove / 1000;
            long afterMonth = (long)ha.Rent[h] * monthDays / 100;
            burdens &= afterMonth <= bearable + 1 && GivingPeople.RentBurden(clean, h) >= PopulationCatalogue.RentBurdenLeave;
            total += ((long)hc.Rent[h] - ha.Rent[h]) * monthDays / 100;
            costs &= total > 0;
        }
        Check(held && untouched && burdens && costs, "giving shelter: a household it keeps is a city tenant whose rent was past the line, its rent is held at what it can bear as a regulated tenancy, it no longer looks for another flat, and nobody else is touched");
        int second = GivingPeople.PayShelter(a, 1000000, level, out long spentAgain);
        int regulated = 0;
        for (int h = 0; h < a.Households.Count; h++)
        {
            regulated += (a.Households.Flags[h] & HouseholdFlags.Regulated) != 0 ? 1 : 0;
        }
        Check(second > 0 && second <= GivingCatalogue.HelpedMost && regulated == keptA + second && spentAgain > 0, "giving shelter: a second month goes to others, for those already held are no longer asked again (" + second + " more, " + regulated + " in all)");
        Check(GivingPeople.PayShelter(GivingCity(100, 2), 0, level, out long none) == 0 && none == 0, "giving shelter: with no money nothing is done");
        // The scholarship fund: households with a pupil or a student in debt, a student's first, the debt paid and the student's skill up.
        PopulationState sc = GivingCity(2000, 23);
        for (int h = 0; h < sc.Households.Count; h++)
        {
            int p = sc.Households.First[h];
            if (h % 5 == 1)
            {
                sc.People.Status[p] = (byte)Status.Student;
            }
            else if (h % 5 == 2)
            {
                sc.People.Status[p] = (byte)Status.Pupil;
            }
            if (h % 5 == 1 || h % 5 == 2)
            {
                sc.Households.Debt[h] = Math.Max(sc.Households.Debt[h], 1200);
            }
        }
        PopulationState scBefore = GivingCity(2000, 23);
        for (int h = 0; h < scBefore.Households.Count; h++)
        {
            scBefore.People.Status[scBefore.Households.First[h]] = sc.People.Status[sc.Households.First[h]];
            scBefore.Households.Debt[h] = sc.Households.Debt[h];
        }
        int taught = GivingPeople.PayScholars(sc, 100000000, level, out long spentSc);
        long debtBefore = 0, debtAfter = 0;
        bool onlySchool = true, studentsFirst = true, skills = true;
        int studentsHelped = 0, pupilsHelped = 0;
        for (int h = 0; h < sc.Households.Count; h++)
        {
            bool helped = sc.Households.Debt[h] != scBefore.Households.Debt[h];
            debtBefore += scBefore.Households.Debt[h];
            debtAfter += sc.Households.Debt[h];
            int first = sc.Households.First[h];
            bool student = sc.People.Status[first] == (byte)Status.Student, pupil = sc.People.Status[first] == (byte)Status.Pupil;
            if (helped)
            {
                onlySchool &= student || pupil;
                studentsHelped += student ? 1 : 0;
                pupilsHelped += pupil ? 1 : 0;
                skills &= !student || sc.People.Skill[first] == Math.Min(100, scBefore.People.Skill[first] + GivingCatalogue.ScholarSkill);
                onlySchool &= scBefore.Households.Debt[h] - sc.Households.Debt[h] <= GivingCatalogue.ScholarMost;
            }
            else
            {
                skills &= sc.People.Skill[first] == scBefore.People.Skill[first];
            }
        }
        studentsFirst = pupilsHelped == 0 && studentsHelped == taught;
        Check(taught > 0 && taught <= GivingCatalogue.HelpedMost && debtBefore - debtAfter == spentSc && onlySchool && skills && studentsFirst,
            "giving scholars: the grants go against the debts of households with a pupil or a student, a student's first, each at most " + GivingCatalogue.ScholarMost + ", and a student gains "
            + GivingCatalogue.ScholarSkill + " points of skill (" + taught + " households for " + spentSc + ")");
        // The care fund: part of a household's own share of a bill, never more than the pot or the month's cap leaves.
        Check(GivingRules.CareCover(1000, 100000, 100000, 0) == 700 && GivingRules.CareCover(1000, 300, 100000, 0) == 300 && GivingRules.CareCover(1000, 100000, 500, 400) == 100
              && GivingRules.CareCover(1000, 100000, 500, 600) == 0 && GivingRules.CareCover(-5, 100, 100, 0) == 0 && GivingRules.CareCover(0, 100, 100, 0) == 0,
            "giving care: the fund pays " + GivingCatalogue.CareShare / 10 + "% of a household's own share of a bill, never more than it holds, never past the month's cap, and nothing for no bill");
        string people = ModSource(@"Health\HealthPeople.cs"), bridges = ModSource(@"Health\HealthBridges.cs"), links = ModSource(@"Wiring\Wiring.HealthLinks.cs");
        int cover = people.IndexOf("HealthBridges.CareFund(own, level)", StringComparison.Ordinal);
        Check(cover > people.IndexOf("long own = bill;", StringComparison.Ordinal) && cover < people.IndexOf("long due = lost + own;", StringComparison.Ordinal) && bridges.Contains("CareFund = null;")
              && links.Contains("HealthBridges.CareFund = CareFund;") && people.Contains("Math.Min(own, HealthBridges.CareFund(own, level))"),
            "giving care: a city household's own share of a care bill is met in part by the fund before it reaches its savings and debt, and the bridge is cleared with the others");
        string cd = ModSource(@"Giving\GivingDirector.Gifts.cs");
        int care = cd.IndexOf("public long CareCover(", StringComparison.Ordinal);
        Check(care > 0 && cd.IndexOf("CauseWhy != 0", care, StringComparison.Ordinal) > care && cd.IndexOf("b.Pot -= pay;", care, StringComparison.Ordinal) > cd.IndexOf("GivingRules.CareCover(", care, StringComparison.Ordinal)
              && cd.IndexOf("b.PaidMonth += pay;", care, StringComparison.Ordinal) > care,
            "giving care: the fund pays nothing in a Going Public session or with the people off, and what it pays leaves its pot and counts against the month's cap");
        // Every cause is read by its own pay-out step and the month's steps follow the number of causes.
        Check(GivingMonth.Steps == GivingMonth.FirstPayStep + (int)Charity.Count + 1 && GivingMonth.NeighbourStep == GivingMonth.FirstPayStep + (int)Charity.Count,
            "giving month: the month's pass has a step for each of the " + (int)Charity.Count + " causes and then the neighbours");
        var st = new GivingState { MonthNumber = 9 };
        st.Book(Charity.Care).PaidMonth = 800;
        st.Book(Charity.Care).MonthPeople = 5;
        GivingMonth.Close(st);
        Check(st.Book(Charity.Care).LastMoney == 800 && st.Book(Charity.Care).LastPeople == 5 && st.Book(Charity.Care).PaidMonth == 0 && st.Book(Charity.Care).MonthPeople == 0 && st.Book(Charity.Care).LastMonth == 9
              && st.Book(Charity.Upkeep).LastMonth == 9, "giving month: the care fund's month, which it pays as bills come, is closed with the counters and shown as last month's");
    }

    // ---- who takes what

    private static void GivingHelpRuleChecks()
    {
        var g = new PersonGiving { Person = 1 };
        // Not close enough.
        Check(GivingHelp.Answer(HelpKind.Rent, false, 1000, 20, 20, false, true, false, false, g, 100) == HelpAnswer.Distant && GivingHelp.Answer(HelpKind.Rent, false, 1000, 40, 40, false, true, false, false, g, 100) == HelpAnswer.Takes,
            "giving help: somebody who barely knows the player would not take the rent from them, a friend does");
        Check(GivingHelp.Answer(HelpKind.Car, false, 20000, 90, 90, false, false, false, false, g, 100) == HelpAnswer.Distant && GivingHelp.Answer(HelpKind.Car, false, 20000, 90, 90, true, false, false, false, g, 100) == HelpAnswer.Takes
              && GivingHelp.Answer(HelpKind.Home, false, 400000, 60, 60, true, false, false, false, g, 100) == HelpAnswer.Distant && GivingHelp.Answer(HelpKind.Home, false, 400000, 80, 80, true, false, false, false, g, 100) == HelpAnswer.Takes,
            "giving help: a car or a home only from a close friend, a partner or family, and the closer for a home");
        bool lines = true;
        for (int k = 1; k < (int)HelpKind.Count; k++)
        {
            lines &= GivingCatalogue.HelpLeast[k] > 0 && GivingCatalogue.HelpLeast[k] <= 200 && (k < 2 || GivingCatalogue.HelpLeast[k] >= GivingCatalogue.HelpLeast[1] - 10);
        }
        Check(lines && GivingCatalogue.HelpLeast.Length == (int)HelpKind.Count && GivingCatalogue.HelpLeast[(int)HelpKind.Home] > GivingCatalogue.HelpLeast[(int)HelpKind.Car] && GivingCatalogue.HelpLeast[(int)HelpKind.Car] > GivingCatalogue.HelpLeast[(int)HelpKind.Business],
            "giving help: every kind has a line of closeness that fits the social life's own (a friend is 50, a close friend 115), and a home asks the most");
        // Generous people give rather than take.
        Check(GivingHelp.Answer(HelpKind.Money, false, 1000, 80, 80, false, false, false, true, g, 100) == HelpAnswer.Needless && GivingHelp.Answer(HelpKind.Money, false, 1000, 80, 80, false, true, false, true, g, 100) == HelpAnswer.Takes
              && GivingHelp.Answer(HelpKind.Car, false, 20000, 90, 90, true, false, false, true, g, 100) == HelpAnswer.Takes,
            "giving help: somebody generous who needs nothing would rather give than take, but takes help when they need it and a car from somebody close");
        // Pride: the first gift over a small kindness is refused, a loan is not, and it is taken after the gap, and never again refused once help is taken.
        var proud = new PersonGiving { Person = 2 };
        bool first = GivingHelp.Answer(HelpKind.Debt, false, 2000, 80, 80, false, true, true, false, proud, 100) == HelpAnswer.Proud;
        proud.RefusedDay = 100;
        bool soon = GivingHelp.Answer(HelpKind.Debt, false, 2000, 80, 80, false, true, true, false, proud, 100 + GivingCatalogue.ProudGapDays - 1) == HelpAnswer.Proud;
        bool later = GivingHelp.Answer(HelpKind.Debt, false, 2000, 80, 80, false, true, true, false, proud, 100 + GivingCatalogue.ProudGapDays) == HelpAnswer.Takes;
        bool loan = GivingHelp.Answer(HelpKind.Debt, true, 2000, 80, 80, false, true, true, false, new PersonGiving(), 100) == HelpAnswer.Takes;
        bool small = GivingHelp.Answer(HelpKind.Debt, false, GivingCatalogue.ProudAbove, 80, 80, false, true, true, false, new PersonGiving(), 100) == HelpAnswer.Takes;
        bool past = GivingHelp.Answer(HelpKind.Debt, false, 2000, 80, 80, false, true, true, false, new PersonGiving { Helps = 1 }, 100) == HelpAnswer.Takes;
        Check(first && soon && later && loan && small && past, "giving help: a proud person refuses the first gift over " + GivingCatalogue.ProudAbove + ", takes it " + GivingCatalogue.ProudGapDays + " days later, takes a loan or a small kindness at once, and has no pride left once helped");
        int prouds = 0;
        for (int person = 0; person < 2000; person++)
        {
            prouds += GivingHelp.Proud(77, person) ? 1 : 0;
        }
        Check(prouds > 250 && prouds < 550 && GivingHelp.Proud(77, 5) == GivingHelp.Proud(77, 5), "giving help: about one person in five is proud, by the save's seed and the same every time (" + prouds + " in 2,000)");
        // What money can never buy: the same however it is split, and the most a window earns.
        var one = new PersonGiving { Person = 3 };
        GivingRules.PersonGain(one, 400000, 50, true, out int a1, out int t1);
        Check(a1 <= GivingCatalogue.AffectionMost * 10 && t1 <= GivingCatalogue.TrustMost * 10, "giving help: a home worth 400,000 earns no more warmth in a window than the most any gift can (" + a1 / 10f + " affection, " + t1 / 10f + " trust)");
        string help = ModSource(@"Giving\GivingDirector.Help.cs");
        Check(help.Contains("SocialLife.Change(person, 0f, affection / 10f, trust / 10f)") && !help.Contains("SocialLife.Change(person, 1f") && !help.Contains("Stage =") && help.Contains("trust = trust * GivingCatalogue.EnviousTrustShare / 1000"),
            "giving help: money never moves how well they know the player or a romance (only affection and trust, in the window), and an envious person feels less trust");
    }

    // ---- what each kind of help changes in the person

    private static void GivingHelpEffectChecks()
    {
        PopulationState s = GivingCity(60, 41);
        int h = 7, p = s.Households.First[h];
        s.Households.Savings[h] = 500;
        s.Households.Debt[h] = 900;
        s.Households.Rent[h] = 4000;
        s.Households.Landlord[h] = Landlords.City;
        int content = s.Households.Content[h];
        long rent = GivingHelp.RentFor(s, h, 3, 5);
        Check(rent == 3 * Math.Max(GivingPeople.MonthRent(s, h), 4000L * 5 / 100) && GivingHelp.CanRent(s, h) && GivingHelp.RentFor(s, h, 1, 5) * 3 == rent && GivingHelp.RentFor(s, h, 0, 5) == GivingHelp.RentFor(s, h, 1, 5),
            "giving rent: paying the rent is its months of the month's rent, and at least a month");
        GivingHelp.PayRent(s, h, rent);
        Check(s.Households.Savings[h] == 500 + rent && s.Households.Debt[h] == 900 && s.Households.Content[h] == content + GivingCatalogue.ContentLift, "giving rent: the money is in the household's savings and its content a little up, and its debt is as it was");
        s.Households.Landlord[h] = Landlords.Own;
        Check(!GivingHelp.CanRent(s, h), "giving rent: an owner has no rent to pay for");
        Check(GivingHelp.CanDebt(s, h) && GivingHelp.DebtFor(s, h, 500) == 500 && GivingHelp.DebtFor(s, h, 5000) == 900 && GivingHelp.DebtFor(s, h, -1) == 0, "giving debt: what can be cleared at once is the debt, held to the most");
        long paid = GivingHelp.ClearDebt(s, h, 2000);
        Check(paid == 900 && s.Households.Debt[h] == 0 && GivingHelp.ClearDebt(s, h, 50) == 0 && !GivingHelp.CanDebt(s, h), "giving debt: clearing a debt pays no more than is owed, and the person is out of debt");
        // A business: for somebody out of work it is work of their own; for a worker the pay that goes with their skill.
        PopulationState b = GivingCity(60, 43);
        int unemployed = -1, worker = -1;
        for (int person = 0; person < b.People.Count; person++)
        {
            if (b.People.Status[person] == (byte)Status.Unemployed && unemployed < 0)
            {
                unemployed = person;
            }
            else if (b.People.Status[person] == (byte)Status.Worker && worker < 0)
            {
                worker = person;
            }
        }
        b.People.Field[unemployed] = 3;
        b.People.Field[worker] = 3;
        int hu = b.People.Household[unemployed], hw = b.People.Household[worker];
        int skillU = b.People.Skill[unemployed], savingsU = b.Households.Savings[hu], skillW = b.People.Skill[worker], payW = b.People.Pay[worker];
        b.People.Pay[worker] = 100;
        bool gotWork = GivingHelp.BackBusiness(b, unemployed, hu, 5000, 500);
        Check(gotWork && b.People.Status[unemployed] == (byte)Status.Worker && b.People.Job[unemployed] == -1 && b.People.Pay[unemployed] > 0 && b.People.Hours[unemployed] == PopulationCatalogue.FullTimeHours
              && b.People.Skill[unemployed] == skillU + GivingCatalogue.BusinessSkill && b.Households.Savings[hu] == savingsU + 5000 && b.People.Since[unemployed] == 500,
            "giving business: somebody out of work is in work of their own, with the money in their household and the skill running it teaches");
        long fetched = LabourRules.PayFor(b, 3, b.People.Skill[unemployed], b.Households.Class[hu]);
        Check(b.People.Pay[unemployed] == Math.Max(1, (int)(fetched * GivingCatalogue.BusinessPayShare / 1000)) && b.People.Pay[unemployed] < fetched,
            "giving business: they earn " + GivingCatalogue.BusinessPayShare / 10 + "% of what their skill fetches at first, so a business is a start and no windfall");
        bool noWork = GivingHelp.BackBusiness(b, worker, hw, 2000, 500);
        Check(!noWork && b.People.Skill[worker] == skillW + GivingCatalogue.BusinessSkill && b.People.Pay[worker] >= 100 && b.People.Status[worker] == (byte)Status.Worker,
            "giving business: somebody already in work gains the skill and the pay that goes with it, and is still in work");
        Check(GivingHelp.BusinessExtra(10000) == 400 && GivingHelp.BusinessExtra(0) == 0 && GivingHelp.BusinessExtra(-5) == 0 && GivingHelp.BusinessExtra(int.MaxValue / 2) > 0, "giving business: a business loan carries " + GivingCatalogue.BusinessInterest / 10f + "% on top, once");
        // A course.
        PopulationState c = GivingCity(60, 47);
        int pc = 3;
        int hc = c.People.Household[pc];
        c.People.Status[pc] = (byte)Status.Unemployed;
        c.People.Field[pc] = 2;
        c.People.Skill[pc] = 98;
        c.People.Pay[pc] = 50;
        GivingHelp.Teach(c, pc, hc);
        Check(c.People.Skill[pc] == 100 && c.People.Pay[pc] > 50, "giving course: a course adds skill up to a hundred and the pay it fetches follows");
        c.People.Skill[pc] = 100;
        Check(!GivingHelp.CanStudy(c, pc, c.Day), "giving course: nobody at a hundred has a course to take");
        // A car and a home.
        PopulationState k = GivingCity(60, 49);
        int hk = 9;
        k.Households.Cars[hk] = 0;
        int ck = k.Households.Content[hk];
        GivingHelp.GiveCar(k, hk);
        GivingHelp.GiveCar(k, hk);
        GivingHelp.GiveCar(k, hk);
        Check(k.Households.Cars[hk] == GivingCatalogue.CarsMost && k.Households.Content[hk] == Math.Min(100, ck + 3 * GivingCatalogue.CarContent) && !GivingHelp.CanCar(k, hk),
            "giving car: a household holds one more car each time, to " + GivingCatalogue.CarsMost + ", and has no room for another");
        k.Households.Landlord[hk] = Landlords.City;
        k.Households.Flags[hk] = (byte)(k.Households.Flags[hk] | HouseholdFlags.Mortgage | HouseholdFlags.WantsMove | HouseholdFlags.Regulated);
        int home = k.Households.Content[hk];
        GivingHelp.GiveHome(k, hk);
        Check(k.Households.Landlord[hk] == Landlords.Own && (k.Households.Flags[hk] & (HouseholdFlags.Mortgage | HouseholdFlags.WantsMove | HouseholdFlags.Regulated)) == 0 && k.Households.Content[hk] == home + GivingCatalogue.HomeContent && !GivingHelp.CanHome(k, hk),
            "giving home: the household owns its flat, with no mortgage, no wish to move and no regulated tenancy, and its content up a good deal");
        Check(GivingHelp.HomePrice(k, hk) == 0, "giving home: a household with no flat the city knows has no price to give");
        // Nobody the player may not help is helped by any of it: the guard sits before every effect.
        string help = ModSource(@"Giving\GivingDirector.Help.cs");
        int give = help.IndexOf("public GiveResult GiveHelp(", StringComparison.Ordinal);
        int pay = help.IndexOf("GivingGame.Pay(amount, KeyOf(kind))", give, StringComparison.Ordinal), takes = help.IndexOf("Takes(person, kind, false, atStart, inNeed, g)", give, StringComparison.Ordinal);
        int effect = help.IndexOf("GivingHelp.PayRent(s, h, amount);", give, StringComparison.Ordinal), session = help.IndexOf("GiveResult.Session", give, StringComparison.Ordinal), helpable = help.IndexOf("GivingPeople.Helpable(s, h)", give, StringComparison.Ordinal);
        Check(session > 0 && session < pay && helpable > 0 && helpable < pay && takes > 0 && takes < pay && pay < effect && help.IndexOf("Answered(person);", give, StringComparison.Ordinal) > effect,
            "giving help: with no city people nothing is taken, only a household the player may help is helped, the person answers first, the money is taken, and only then the household changes");
        Check(help.Contains("long most = PersonCap(false);") && help.Contains("GivingBridges.GaveToPerson?.Invoke(amount);") && help.Contains("g.CoveredUntil = Math.Max(g.CoveredUntil, day + months * GivingGame.MonthDays);"),
            "giving help: a sum is held to the most the player's wealth allows, counted in the estate as given for good, and a rent paid is theirs to pay again after its months");
    }

    // ---- leaning on the player's help

    private static void GivingLeanChecks()
    {
        var g = new PersonGiving { Person = 4 };
        Check(GivingRules.Dependence(null, 100) == 0 && GivingRules.Dependence(g, 100) == 0 && !GivingRules.Cooling(g, 100) && !GivingRules.Cooling(null, 100), "giving lean: somebody never helped leans on nobody and is not cooling");
        g.Helps = 3;
        g.HelpDay = 100;
        Check(GivingRules.Dependence(g, 100) == 3 && GivingRules.Dependence(g, 129) == 3 && GivingRules.Dependence(g, 130) == 2 && GivingRules.Dependence(g, 190) == 0 && GivingRules.Dependence(g, 5000) == 0,
            "giving lean: dependence falls a point each " + GivingCatalogue.DependDecayDays + " days since the last help, and is gone after a few months");
        g.Helps = 50;
        Check(GivingRules.Dependence(g, 100) == GivingCatalogue.DependMost, "giving lean: dependence never passes " + GivingCatalogue.DependMost);
        Check(GivingRules.Cooling(g, 100) && GivingRules.Cooling(g, 100 + GivingCatalogue.HelpCooldownDays - 1) && !GivingRules.Cooling(g, 100 + GivingCatalogue.HelpCooldownDays), "giving lean: nobody asks for " + GivingCatalogue.HelpCooldownDays + " days after being helped");
        int basic = GivingRules.AskPerTenThousand(0, 0);
        int last = -1;
        bool rising = true;
        for (int dep = 0; dep <= GivingCatalogue.DependMost; dep++)
        {
            int chance = GivingRules.AskLeaning(basic, dep);
            rising &= chance > last;
            last = chance;
        }
        Check(rising && GivingRules.AskLeaning(basic, 0) == basic && GivingRules.AskLeaning(basic, 5) == basic * (1000 + 5 * GivingCatalogue.AskPerDependence) / 1000 && GivingRules.AskLeaning(basic, -2) == basic,
            "giving lean: the more somebody leans on the player the likelier they ask, " + GivingCatalogue.AskPerDependence / 10 + "% more for each point");
        Check(!GivingRules.AsksForMore(1, false) && GivingRules.AsksForMore(GivingCatalogue.MoreFrom, false) && !GivingRules.AsksForMore(5, true), "giving lean: somebody asks for more with no real need only after leaning " + GivingCatalogue.MoreFrom + " points, and never the reliable sort");
        Check(GivingRules.MoreAmount(1000, 1000) == 800 && GivingRules.MoreAmount(0, 1000) == 100 && GivingRules.MoreAmount(99999999, 1000) == GivingCatalogue.AskMost && GivingRules.MoreAmount(100, 1000) == 100 && GivingRules.MoreAmount(1000, 2000) == 1600,
            "giving lean: what they ask for is " + GivingCatalogue.MoreShare / 10 + "% of their last help, never under 100 nor over " + GivingCatalogue.AskMost + ", at today's prices");
        Check(GivingRules.NoCost(0) == 0 && GivingRules.NoCost(GivingCatalogue.FreeNos) == GivingCatalogue.NoCost && GivingRules.NoCost(9) == GivingCatalogue.NoCost,
            "giving lean: the first no costs nothing and every later one " + GivingCatalogue.NoCost / 10f + " of a point of affection");
        // A year of hours for a person leaning on the player: asks are rarer than the gaps allow, never within the cooldown, and rise with dependence.
        int none = 0, heavy = 0;
        for (int hour = 0; hour < 200000; hour++)
        {
            none += GivingRules.Asks(8, 4, hour / 24, hour % 24, GivingRules.AskLeaning(basic, 0)) ? 1 : 0;
            heavy += GivingRules.Asks(8, 4, hour / 24, hour % 24, GivingRules.AskLeaning(basic, 5)) ? 1 : 0;
        }
        Check(heavy > none && heavy < none * 3, "giving lean: somebody who leans a great deal asks more often, but no more than about two and a half times (" + none + " and " + heavy + " in 200,000 hours)");
        string loans = ModSource(@"Giving\GivingDirector.Loans.cs"), help = ModSource(@"Giving\GivingDirector.Help.cs");
        int ask = loans.IndexOf("public bool TryAsk(", StringComparison.Ordinal);
        Check(loans.IndexOf("GivingRules.Cooling(g, day)", ask, StringComparison.Ordinal) > ask && loans.IndexOf("GivingRules.AskLeaning(", ask, StringComparison.Ordinal) > ask
              && loans.IndexOf("GivingRules.AsksForMore(dependence, person.Has(SocialTrait.Reliable))", ask, StringComparison.Ordinal) > ask && loans.Contains("asking.MorePending = more ? 1 : 0;")
              && loans.Contains("reallife_giving_ask_more"), "giving lean: nobody in their cooldown asks, a draw leans with dependence, and an ask for more is sent as such and remembered for the call");
        Check(help.Contains("public bool Declined(") && help.Contains("GivingRules.NoCost(g.Nos)") && help.Contains("Answered(person);") && ModSource(@"Social\PersonDialog.Giving.cs").Contains("giver.Declined(_p)")
              && ModSource(@"Social\PersonDialog.Giving.cs").Contains("reallife_giving_plea_no_first") && !ModSource(@"Social\PersonDialog.Giving.cs").Contains("SocialLife.Change(_p, 0f, -1f, 0f)"),
            "giving lean: the player's first no to a plea costs nothing and says so, every later one costs a little, and a no closes the ask");
        string live = help.Substring(help.IndexOf("private void Helped(", StringComparison.Ordinal), 400);
        Check(live.Contains("g.Helps = GivingRules.Dependence(g, day) + 1;") && live.Contains("g.HelpDay = day;"), "giving lean: each help counts on the dependence as it stands, so help long ago does not come back all at once");
    }

    // ---- a business loan's terms

    private static void GivingBusinessLoanChecks()
    {
        PopulationState s = GivingCity(60, 53);
        int h = 11, p = s.Households.First[h];
        s.People.Status[p] = (byte)Status.Unemployed;
        s.People.Field[p] = 4;
        s.Households.Savings[h] = 6000000;
        var st = new GivingState();
        GiveResult ok = GivingLoans.Check(st, s, 77, 20000, 1000, 40000, out _, out _);
        Check(ok == GiveResult.NoPerson, "giving business loan: a person the population does not hold is not lent to");
        s.Links.Add(new PersonLink { Kind = LinkKind.Met, Key = "social:77", Person = s.People.Id[p], Slot = p });
        Check(GivingLoans.Check(st, s, 77, 20000, 1000, 40000, out int rp, out int rh) == GiveResult.Done && rp == p && rh == h && GivingLoans.Check(st, s, 77, 40001, 1000, 40000, out _, out _) == GiveResult.Limit,
            "giving business loan: the most is the player's own, which grows with their wealth");
        int skill = s.People.Skill[p];
        LoanOut loan = GivingLoans.Make(st, s, h, 77, "A Name", 20000, 400, 5, true, p);
        Check(loan.Principal == 20000 && loan.Extra == 800 && loan.Remaining == 20800 && loan.Parts == GivingCatalogue.BusinessParts && loan.Part == GivingRules.LoanPart(20800, GivingCatalogue.BusinessParts) && loan.Part == 1734
              && s.People.Status[p] == (byte)Status.Worker && s.People.Skill[p] == skill + GivingCatalogue.BusinessSkill && s.Households.Savings[h] == 6020000,
            "giving business loan: " + GivingCatalogue.BusinessParts + " parts of 1,734 for 20,000 and 4% on top, the money backing their business");
        long back = 0;
        int months = 0;
        for (int day = 405; day < 405 + 5 * 40 && loan.State == LoanState.Open; day += 5)
        {
            LoanTurn turn = GivingLoans.Collect(loan, s, day, 5, out long took);
            back += took;
            months += turn == LoanTurn.None ? 0 : 1;
        }
        Check(loan.State == LoanState.Repaid && back == 20800 && loan.Paid == 20800 && months == GivingCatalogue.BusinessParts, "giving business loan: a household that can pay it pays back all 20,800 over " + months + " months, the part's end in the last");
        var plain = GivingLoans.Make(new GivingState(), GivingCity(60, 53), 11, 77, "B", 6000, 400, 5);
        Check(plain.Parts == GivingCatalogue.LoanParts && plain.Extra == 0 && plain.Remaining == 6000 && plain.Part == 1000, "giving business loan: an ordinary loan is six parts and no interest, as it always was");
    }

    // ---- the save, with the new parts

    private static void GivingNewStoreChecks()
    {
        var s = new GivingState { Seed = 5, PeriodStart = 120, TaxGifts = 40000, TaxClaimed = 52000, CarryTaken = 20000 };
        s.Carry[0] = 31400;
        s.Carry[2] = 9000;
        s.UpkeepTotal[1] = 25000;
        s.UpkeepTotal[7] = 5;
        s.Book(Charity.Care).Pot = 2100;
        s.Book(Charity.Care).MonthPeople = 6;
        s.Standing.Add(new StandingGift { Charity = (int)Charity.Upkeep, Amount = 1000, Since = 3, Tag = 2, Month = 1 });
        s.Namings.Add(new GivingNaming { Day = 99, Charity = 4, Amount = 250000, Tag = -1, Name = "Dana; Reed | Jr" });
        s.Namings.Add(new GivingNaming { Day = 110, Charity = 6, Amount = 300000, Tag = 3, Name = "Dana Reed" });
        s.Notes.Add(new GivingNote { Day = 110, Charity = 6, Amount = 300000, Giver = -1, Naming = 1, Tag = 3 });
        s.Loans.Add(new LoanOut { Person = 2, Name = "Biz", Principal = 20000, Remaining = 20800, Made = 100, Part = 1734, Next = 130, State = LoanState.Open, Parts = 12, Extra = 800 });
        PersonGiving g = s.PersonOf(2, true);
        g.Helps = 3;
        g.HelpDay = 118;
        g.LastHelp = 1200;
        g.RefusedDay = 90;
        g.CoveredUntil = 160;
        g.Nos = 2;
        g.MorePending = 1;
        g.GivenTotal = 23000;
        string line = GivingStore.Write(s);
        GivingState back = GivingStore.Read(line, out int version);
        Check(back != null && version == GivingStore.Version && GivingStore.Write(back) == line, "giving save: the new parts (the carry, the names, the districts' totals, a standing gift's district, a business loan's terms, a person's lean) write and read back to the same line");
        Check(back.Carry[0] == 31400 && back.Carry[2] == 9000 && back.CarryTaken == 20000 && back.TaxClaimed == 52000 && back.UpkeepTotal[1] == 25000 && back.Namings.Count == 2 && back.Namings[0].Name == "Dana; Reed | Jr"
              && back.Namings[1].Tag == 3 && back.Notes[0].Naming == 1 && back.Notes[0].Tag == 3 && back.Standing[0].Tag == 2 && back.Loans[0].Parts == 12 && back.Loans[0].Extra == 800 && back.Book(Charity.Care).MonthPeople == 6,
            "giving save: every new figure is read back, a name with marks in it included, and what is claimed is held to the gifts and the carry it was claimed from");
        PersonGiving r = back.PersonOf(2, false);
        Check(r != null && r.Helps == 3 && r.HelpDay == 118 && r.LastHelp == 1200 && r.RefusedDay == 90 && r.CoveredUntil == 160 && r.Nos == 2 && r.MorePending == 1 && r.GivenTotal == 23000, "giving save: what a person leans on, refused and was said no to is read back");
        // A save from before these parts loads as it did: no carry, no names, a loan of six parts.
        GivingState old = GivingStore.Read("v=1^h:5|400|6|361|12000|7000|6|2500|1|2|390|5|800|4000|12|399|-1|0^b:4200|0|0|0|0|0|0|0|0|0|-1|0|0^s:1|250|300|1|1500|4^l:3|1200|800|380|200|1|410|400|0|0|A+Name^p:3|100|3000|20|30|350|3000^r:0|0|0|0|0|-1^n:399|1|30000|-1",
            out _);
        Check(old != null && old.Carry.All(x => x == 0) && old.CarryTaken == 0 && old.Namings.Count == 0 && old.Loans[0].Parts == GivingCatalogue.LoanParts && old.Loans[0].Extra == 0 && old.Standing[0].Tag == -1
              && old.Notes[0].Naming == 0 && old.Notes[0].Tag == -1 && old.PersonOf(3, false).Helps == 0 && old.TaxClaimed == 7000 && old.Book(Charity.FoodBank).Pot == 4200,
            "giving save: a line from before the newer causes and ways to help loads as it was, with nothing carried, no names and a loan of the usual six parts");
        GivingState hostile = GivingStore.Read("v=1^h:1|1|1|1|-5|9999999^c:-9|-1|-2|x|5^m:1|99|-5|-1|x;1|99|100|99|y^u:-5|x|9^s:1|5|1|0|0|0|99^p:1|0|0|0|0|0|0|-4|-1|-1|-1|-1|-9|7^l:1|100|50|1|1|0|1|0|0|0|N|99|-5", out _);
        Check(hostile != null && hostile.CarryTaken == 0 && hostile.Carry.All(x => x >= 0) && hostile.Namings.Count == 1 && hostile.Namings[0].Charity == (int)Charity.Count - 1 && hostile.Namings[0].Tag == -1 && hostile.UpkeepTotal.All(x => x >= 0)
              && hostile.Standing.Count == 1 && hostile.Standing[0].Tag == -1 && hostile.PersonOf(1, false).Helps == 0 && hostile.PersonOf(1, false).MorePending == 0 && hostile.Loans.Count == 1 && hostile.Loans[0].Parts == GivingCatalogue.BusinessParts && old.Loans[0].Parts == GivingCatalogue.LoanParts && hostile.Loans[0].Extra == 0,
            "giving save: new figures out of bounds are put inside them, and a naming with no sum is dropped");
        Check(GivingCatalogue.NamingsKept == 12 && GivingStore.Read(GivingStore.Write(ManyNamings(30)), out _).Namings.Count == GivingCatalogue.NamingsKept, "giving save: only the last " + GivingCatalogue.NamingsKept + " names are kept");
    }

    // A city for the shelter: the households' rents are what their ledgers say they paid (a month's rent in cents over the month's days), as they are in a real city.
    private static PopulationState ShelterCity(int seed)
    {
        PopulationState s = GivingCity(2000, seed);
        int monthDays = GenerateRules.MonthDays(s.YearDays);
        for (int h = 0; h < s.Households.Count; h++)
        {
            s.Households.Rent[h] = (int)(s.Households.Last[h * RealLifeOverhaul.Population.Ledger.Lines + RealLifeOverhaul.Population.Ledger.Rent] / Math.Max(1, monthDays));
        }
        return s;
    }

    private static GivingState ManyNamings(int n)
    {
        var s = new GivingState();
        for (int i = 0; i < n; i++)
        {
            s.Namings.Add(new GivingNaming { Day = i + 1, Charity = i % 7, Amount = 250000 + i, Name = "N" + i });
        }
        return s;
    }

    // ---- the pages: seven causes, a district, steps up to the cash, the people, the names

    private static void GivingNewPageChecks()
    {
        UiWords words = ModEnglish();
        // Every cause opens its page, with its name, its words and an amount.
        for (int cause = 0; cause < (int)Charity.Count; cause++)
        {
            var view = new VentView { Pick = GivePages.CharityKey + cause, A = 2 };
            var press = new UiSamples.GivingStub();
            VentModel m = UiSamples.Giving(words, "charities", view, press);
            Check(m.Right.Length >= 6 && m.Right[0].Text == words(GivingCatalogue.NameKeys[cause], string.Empty) && m.Right.Any(b => b.Kind == VentBlockKind.Stepper) && m.Right.Any(b => b.Kind == VentBlockKind.Actions),
                "giving pages: the " + cause + "th cause opens its page with its name, its words, an amount and its buttons");
        }
        // A district's upkeep: the district is chosen, and the gift names it.
        var upView = new VentView();
        var up = new UiSamples.GivingStub();
        VentModel upkeep = UiSamples.Giving(words, "charities_upkeep", upView, up);
        VentBlock choice = upkeep.Right.First(b => b.Kind == VentBlockKind.Choice);
        Check(choice.Value == "Lower Manhattan" && !upkeep.Right.Any(b => b.Kind == VentBlockKind.Row && b.Text == words("reallife_giving_row_held", "Held now")),
            "giving pages: a district's upkeep shows the district chosen and what it has had, and holds nothing to show");
        upkeep.Runs[choice.Run]();
        Check(upView.B == 2, "giving pages: pressing the district goes to the next");
        VentBlock buttons = upkeep.Right.First(b => b.Kind == VentBlockKind.Actions);
        upkeep.Runs[buttons.Actions[buttons.Actions.Length - 1].Arg]();
        Check(up.Last == "give" && up.Charity == 6 && up.District == 1 && up.Step == 4, "giving pages: Give in a district's upkeep gives to that district");
        var noDistrict = new VentView { Pick = GivePages.CharityKey + 6 };
        GiveFacts bare = UiSamples.GivingFactsOf("charities");
        bare.Districts = new string[0];
        bare.DistrictTotals = new long[0];
        var model = new VentModel();
        GivePages.Fill(model, noDistrict, bare, new UiSamples.GivingStub(), UiSamples.VentWordsOf(words));
        VentBlock none = model.Right.First(b => b.Kind == VentBlockKind.Actions);
        Check(none.Actions.All(a => a.Kind != UiButtonKind.Primary || a.Disabled) && model.Right.Any(b => b.Kind == VentBlockKind.Note && b.Text == words("reallife_giving_no_district", string.Empty)),
            "giving pages: a city with no district the mod knows offers no gift to upkeep, and says why");
        // A rich player is shown steps up to their cash, and a big gift says it will take their name.
        VentModel rich = UiSamples.Giving(words, "charities_rich", new VentView(), new UiSamples.GivingStub());
        VentBlock stepper = rich.Right.First(b => b.Kind == VentBlockKind.Stepper);
        VentModel poor = UiSamples.Giving(words, "charities", new VentView { Pick = GivePages.CharityKey + 1, A = 10 }, new UiSamples.GivingStub());
        VentBlock poorStepper = poor.Right.First(b => b.Kind == VentBlockKind.Stepper);
        Check(stepper.CanMore && stepper.Value.Contains("1M") && !poorStepper.CanMore && poorStepper.Value.Contains("100,000"),
            "giving pages: a player with 90 million to give is shown steps to a million and on, and one with little stops at 100,000 (" + stepper.Value + ", " + poorStepper.Value + ")");
        Check(rich.Right.Any(b => b.Kind == VentBlockKind.Note && b.Text == words("reallife_giving_naming_hint", string.Empty)) && !poor.Right.Any(b => b.Kind == VentBlockKind.Note && b.Text == words("reallife_giving_naming_hint", string.Empty)),
            "giving pages: a gift of the naming size says it takes the giver's name, and a small one does not");
        // The People tab lists everyone helped, and says what to do when nobody is.
        VentModel people = UiSamples.Giving(words, "people", new VentView(), new UiSamples.GivingStub());
        VentModel nobody = UiSamples.Giving(words, "people_empty", new VentView(), new UiSamples.GivingStub());
        Check(people.Nav.Length == 4 && people.Nav[3].Count == "4" && people.Left.Any(b => b.Kind == VentBlockKind.Items && b.Items.Length == 4 && b.Items[0].Title == "Maya Okafor" && b.Items[0].Amount.Contains("3,200"))
              && people.Right.Count(b => b.Kind == VentBlockKind.Note) >= 2, "giving pages: the People tab lists the four people the player has helped, the most given first, with what they are short of");
        Check(nobody.Left.Any(b => b.Kind == VentBlockKind.Note && b.Text == words("reallife_giving_people_none", string.Empty)) && nobody.Nav[3].Count == null, "giving pages: with nobody helped the People tab says how to begin");
        VentModel year = UiSamples.Giving(words, "year", new VentView(), new UiSamples.GivingStub());
        Check(year.Left.Any(b => b.Kind == VentBlockKind.Row && b.Value.Contains("12,500")) && year.Right.Any(b => b.Kind == VentBlockKind.Row && b.Text.Contains("Scholarship") && b.Value.Contains("250,000")),
            "giving pages: the year's tab shows what is carried forward and the halls and scholarships that carry the player's name");
        // The old frame draws the same facts: its tab keys are the same list.
        string app = ModSource(@"Giving\GivingApp.cs");
        Check(app.Contains("\"reallife_giving_tab_people\"") && app.Contains("GivingColumns.People") && app.Contains("reallife_giving_row_district_yours"), "giving pages: the app in the game's frame has the People tab and the district too");
    }

    // ---- sources: every gift a bank line, the people's pages, the interfaces

    private static void GivingHelpSourceChecks()
    {
        string game = ModSource(@"Giving\GivingGame.cs");
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        var keys = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(game, "public const string (\\w+Key) = \"(reallife_transaction_giving_[a-z_]+)\""))
        {
            keys.Add(m.Groups[2].Value);
        }
        Check(keys.Count == 15, "giving bank: every kind of gift has a bank line of its own (" + keys.Count + ": charity, standing, naming, upkeep, person, loan, loan back, deduction, rent, debt, business, business loan, course, car, home)");
        foreach (string key in keys)
        {
            Check(english != null && english.ContainsKey(key) && english.ContainsKey(key + "_label") && english[key].Length > 0 && english[key + "_label"].Length > 0, "giving bank: " + key + " has its words and its label in English");
        }
        string gifts = ModSource(@"Giving\GivingDirector.Gifts.cs"), help = ModSource(@"Giving\GivingDirector.Help.cs"), loans = ModSource(@"Giving\GivingDirector.Loans.cs");
        int books = CountOfText(gifts + help + loans, "GivingGame.Pay(");
        Check(books == 5 && CountOfText(gifts + help + loans, "GivingGame.Receive(") == 1, "giving bank: money leaves the player's cash at five places only (a gift to a cause, a plain gift, any other help, a loan, a business loan) and comes back at one (a loan's part)");
        Check(gifts.Contains("GivingGame.CharityKey") && gifts.Contains("GivingGame.UpkeepKey") && gifts.Contains("GivingGame.StandingKey") && gifts.Contains("GivingGame.NamingKey") && gifts.Contains("GivingGame.PersonKey")
              && help.Contains("GivingGame.RentKey") && help.Contains("GivingGame.DebtKey") && help.Contains("GivingGame.BusinessKey") && help.Contains("GivingGame.CourseKey") && help.Contains("GivingGame.CarKey")
              && help.Contains("GivingGame.HomeKey") && loans.Contains("GivingGame.BusinessLoanKey") && loans.Contains("GivingGame.LoanKey"), "giving bank: every kind of gift books its own labelled line, so Finance shows what the money was for");
        Check(gifts.IndexOf("TaxGifts +=", StringComparison.Ordinal) > 0 && CountOfText(help, "TaxGifts") == 0, "giving tax: only gifts to causes reach the deduction, never help for a person");
        // The pages.
        string talk = ModSource(@"Giving\GivingTalk.cs"), people = ModSource(@"Social\PeopleDialog.cs"), dialog = ModSource(@"Social\PersonDialog.cs"), app = ModSource(@"Giving\GivingApp.cs"), pages = ModSource(@"Ui\Ventures\GivePages.cs");
        Check(talk.Contains("HelpKind.Rent") && talk.Contains("HelpKind.Debt") && talk.Contains("HelpKind.Business") && talk.Contains("HelpKind.Course") && talk.Contains("HelpKind.Car") && talk.Contains("HelpKind.Home")
              && talk.Contains("OptionLend") && talk.Contains("OptionPartnerCar") && talk.Contains("giver.GiveHelp(_p, HelpKind.Business, at, true)") && talk.Contains("giver.GiveHelp(_p, HelpKind.Business, at, false)"),
            "giving pages: the talk offers every way to help, a business as a gift or a loan, and a car for a partner through the family");
        Check(talk.Contains("entry.Pays(DialogButton.Confirm, \"reallife_ui_dialog_cost_now\"") && CountOfText(talk, "entry.Pays(DialogButton.Confirm") >= 7 && talk.Contains("GivingResults.Refused(r)"),
            "giving pages: each answer shows its price under the button, and a refusal is said in the person's own words");
        Check(dialog.Contains("_actions.Add(\"help\")") && dialog.Contains("Talk.Menu(0)") && people.Contains("MoneyPage(at)") && people.Contains("new GivingTalk(new MoneyHost(p)).Menu(0)") && people.Contains("giver.MoneyOf(p, fact)"),
            "giving pages: help is offered on a call under Something else and on the person's own page in People, through the same talk");
        Check(ModSource(@"Wiring\FamilyLinks.cs").Contains("GivingTalk.PartnerCarCan") && ModSource(@"Wiring\FamilyLinks.cs").Contains("GivingTalk.PartnerCarOffer") && ModSource(@"Family\PartnerCar.cs").Contains("public static bool CanGift"),
            "giving pages: the partner's car is the family's own conversation, reached from Help with money whether or not they have asked");
        Check(app.Contains("case 3: spec = People(box); break;") && app.Contains("f.Districts[i]") && app.Contains("GivingRules.ShownSteps(f.Spare, f.Level)") && app.Contains("f.Namings[i].Words") && app.Contains("f.Carried")
              && pages.Contains("case TabPeople: People(m, f, w); break;") && pages.Contains("f.Districts[district]") && pages.Contains("GivingRules.ShownSteps(f.Spare, f.Level)") && pages.Contains("f.Namings[i].Words") && pages.Contains("f.Carried"),
            "giving pages: the app in the game's frame and the kit's page both show the people, the districts, the steps up to the cash, the names and the carry");
        Check(ModSource(@"Social\PersonDialog.Giving.cs").Contains("giver.AsksForMore(_p)") && ModSource(@"Social\PersonDialog.Giving.cs").Contains("reallife_giving_plea_more"), "giving pages: a plea for more is answered by the call as such");
        Check(ModSource(@"Giving\GivingGuide.cs").Contains("\"giving_naming\"") && ModSource(@"Giving\GivingGuide.cs").Contains("\"giving_carry\"") && ModSource(@"Giving\GivingGuide.cs").Contains("\"giving_business_parts\"")
              && ModSource(@"Giving\GivingGuide.cs").Contains("\"giving_business_share\"") && ModSource(@"Giving\GivingGuide.cs").Contains("\"giving_cooldown\"") && ModSource(@"Giving\GivingGuide.cs").Contains("reallife_giving_test_btn_naming")
              && ModSource(@"Giving\GivingGuide.cs").Contains("reallife_giving_test_btn_help"), "giving guide: the Guide's new numbers are filled from the rules and the two new tests are listed");
        Check(ModSource(@"Giving\GivingDirector.Tests.cs").Contains("public void TestHelp()") && ModSource(@"Giving\GivingDirector.Tests.cs").Contains("public void TestNaming()") && ModSource(@"Giving\GivingDirector.Tests.cs").Contains("c == Charity.Upkeep ? 0 : -1"),
            "giving tests: help and a naming have their test, and the test gift reaches a district's upkeep too");
        Check(ModSource(@"Wiring\Wiring.GivingLinks.cs").Contains("GivingBridges.Upkeep = (district, points, percent, days)") && ModSource(@"Fame\FameDirector.City.cs").Contains("FameCity.DistrictBoost(_file, district, percent, Save.Day + days)")
              && ModSource(@"Fame\FameDirector.City.cs").Contains("Moved(FamePart.Neighbourhood, FameCause.Charity, points, district)"), "giving upkeep: a gift to a district's upkeep reaches Standing's neighbourhood and its custom boost through Standing's own moves");
    }
}
