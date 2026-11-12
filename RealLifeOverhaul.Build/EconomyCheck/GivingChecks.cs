using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

// Giving (Scripts/Giving, run with --giving): the steps and every cap, the charities' months on a city of households (the debt, the rent and the courses that really change, the
// players' own and their tenants' never), the tax deduction (never over its cap, never twice, never for a person or a loan), standing and the press and a person's warmth earned
// by the sum given and not by how it is split, loans paid back out of what a household can spare, who asks and when, the rivals' gifts, the neighbours, the save, old saves and
// the option off, a session, the pages and every button, the sources (money only through the one call, from the spare cash only) and the texts in every language. The parts that run
// over time (the tax year turning, the month's pass, a loan's months, the save under a culture with decimal commas) are in GivingYearChecks.cs.
internal static partial class Program
{
    private static readonly string[] GivingLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void Giving()
    {
        GivingRuleChecks();
        GivingTaxChecks();
        GivingYearChecks();
        GivingMonthChecks();
        GivingPopulationChecks();
        GivingStandingChecks();
        GivingCityChecks();
        GivingMoneyChecks();
        GivingLoanChecks();
        GivingLoanTurnChecks();
        GivingAskChecks();
        GivingRivalChecks();
        GivingStoreChecks();
        GivingCultureChecks();
        GivingPageChecks();
        GivingSourceChecks();
        GivingTextChecks();
    }

    // ---- a small city of households to give to

    private static PopulationState GivingCity(int households, int seed)
    {
        var s = new PopulationState { Seed = seed, Day = 400, YearDays = 60 };
        HouseholdTable hh = s.Households;
        PersonTable pt = s.People;
        for (int i = 0; i < households; i++)
        {
            int h = hh.Add(i + 1);
            uint r = GivingRules.Hash(seed, 1, i, 0);
            hh.Class[h] = (byte)(r % 3);
            hh.Savings[h] = (int)(GivingRules.Hash(seed, 2, i, 0) % (hh.Class[h] == 2 ? 90000u : 6000u));
            hh.Debt[h] = (int)(GivingRules.Hash(seed, 3, i, 0) % 3 == 0 ? GivingRules.Hash(seed, 4, i, 0) % 9000u : 0u);
            hh.Rent[h] = (int)(3000 + GivingRules.Hash(seed, 5, i, 0) % 6000u);   // cents a day
            hh.Content[h] = 60;
            int row = h * RealLifeOverhaul.Population.Ledger.Lines;
            int wage = (int)(150000 + GivingRules.Hash(seed, 6, i, 0) % 400000u);   // cents a month
            hh.Last[row + RealLifeOverhaul.Population.Ledger.Wages] = i % 9 == 0 ? 0 : wage;
            hh.Last[row + RealLifeOverhaul.Population.Ledger.Rent] = hh.Rent[h] * 5;   // a five day month in the check's small year: the rent is a share of the income below
            hh.Last[row + RealLifeOverhaul.Population.Ledger.Rent] = (int)(wage * (300 + GivingRules.Hash(seed, 7, i, 0) % 500u) / 1000);
            if (i % 40 == 3)
            {
                hh.Flags[h] |= HouseholdFlags.Players;
            }
            if (i % 40 == 5)
            {
                hh.Landlord[h] = Landlords.Player;
            }
            int first = -1, prev = -1;
            for (int k = 0; k < 2; k++)
            {
                int p = pt.Add(i * 4 + k + 1);
                pt.Household[p] = h;
                pt.Birth[p] = -9000 - k * 400;
                pt.Status[p] = (byte)(i % 9 == 0 && k == 0 ? Status.Unemployed : Status.Worker);
                pt.Skill[p] = (byte)(10 + GivingRules.Hash(seed, 8, i * 4 + k, 0) % 60u);
                pt.Since[p] = 300;
                if (first < 0)
                {
                    first = p;
                }
                if (prev >= 0)
                {
                    pt.Next[prev] = p;
                }
                prev = p;
            }
            hh.First[h] = first;
            hh.Size[h] = 2;
        }
        return s;
    }

    private static long GivingMoneyOf(PopulationState s)
    {
        long sum = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            sum += s.Households.Alive(h) ? (long)s.Households.Savings[h] - s.Households.Debt[h] : 0L;
        }
        return sum;
    }

    // ---- the steps and the caps

    private static void GivingRuleChecks()
    {
        int[] steps = GivingCatalogue.Steps;
        bool rising = true;
        for (int i = 1; i < steps.Length; i++)
        {
            rising &= steps[i] > steps[i - 1];
        }
        Check(rising && steps[0] == 50 && steps[steps.Length - 1] == 100000, "giving: the steps rise from 50 to 100,000 at the start's prices");
        Check(GivingRules.StepNow(4, 1000) == 1000 && GivingRules.StepNow(4, 2000) == 2000 && GivingRules.StepNow(4, 500) == 500, "giving: a step follows the city's prices, as Prices.At does");
        Check(GivingRules.StepNow(-5, 1000) == 50 && GivingRules.StepNow(99, 1000) == 100000, "giving: a step out of the list is held inside it");
        Check(GivingRules.StepOf(480) == 3 && GivingRules.StepOf(0) == 0 && GivingRules.StepOf(9999999) == steps.Length - 1, "giving: the nearest step to an amount");
        Check(GivingRules.Back(2000, 2000) == 1000 && GivingRules.Back(1000, 1000) == 1000, "giving: an amount at today's prices goes back to the start's");
        Check(GivingRules.MonthOf(1, 5) == 0 && GivingRules.MonthOf(5, 5) == 0 && GivingRules.MonthOf(6, 5) == 1 && GivingRules.MonthOf(0, 5) == 0, "giving: a month is the twelfth of the year the economy uses");
        Check(GivingCatalogue.CapPerHousehold.Length == (int)Charity.Count && GivingCatalogue.NameKeys.Length == (int)Charity.Count && GivingCatalogue.AboutKeys.Length == (int)Charity.Count
              && GivingCatalogue.DeductionShare == 60, "giving: three charities, each with its cap, its name and its words, and the deduction share of 6%");
        long cap = GivingRules.MonthCap(Charity.FoodBank, 20000, 1000);
        Check(cap == 20000L * 14 && GivingRules.MonthCap(Charity.Housing, 20000, 2000) == 20000L * 20 && GivingRules.MonthCap(Charity.Training, 0, 1000) == 0, "giving: a charity's cap is its share for each household of the city, at today's prices");
        Check(GivingRules.PayNow(1000, 600, 0) == 600 && GivingRules.PayNow(300, 600, 0) == 300 && GivingRules.PayNow(1000, 600, 600) == 0 && GivingRules.PayNow(1000, 600, 900) == 0,
            "giving: a charity pays what it holds under its cap, less what it has paid this month, and never a negative");
        Check(GivingResults.KeyOf(GiveResult.Broke) == "reallife_giving_result_broke" && GivingResults.Moved(GiveResult.DoneOnly) && !GivingResults.Moved(GiveResult.Limit), "giving: every result has its line");
        Check(GivingCatalogue.LoanParts == 6 && GivingCatalogue.LoansMost == 8 && GivingCatalogue.LoanMost == 10000 && GivingCatalogue.GiftsMost == 10000 && GivingCatalogue.AskGapDays == 14,
            "giving: six parts, eight loans open, 10,000 the most of a gift or a loan, and two weeks between asks");
    }

    // ---- the deduction

    private static void GivingTaxChecks()
    {
        Check(GivingRules.DeductionCap(310000) == 18600 && GivingRules.DeductionCap(0) == 0 && GivingRules.DeductionCap(-5) == 0, "giving tax: the cap is 6% of the year's income and never negative");
        Check(GivingRules.DeductionDue(4000, 0, 310000) == 4000 && GivingRules.DeductionDue(50000, 0, 310000) == 18600 && GivingRules.DeductionDue(50000, 18600, 310000) == 0
              && GivingRules.DeductionDue(4000, 4000, 310000) == 0 && GivingRules.DeductionDue(-1, 0, 310000) == 0 && GivingRules.DeductionDue(4000, 9000, 310000) == 0
              && GivingRules.DeductionDue(4000, 0, 0) == 0 && GivingRules.DeductionDue(4000, -50, 310000) == 4000,
            "giving tax: what is due is the gifts held to the cap, less what is claimed already, and never under nothing");
        string gifts2 = ModSource(@"Giving\GivingDirector.Gifts.cs"), loans = ModSource(@"Giving\GivingDirector.Loans.cs"), director = ModSource(@"Giving\GivingDirector.cs");
        string all = string.Concat(Directory.GetFiles(System.IO.Path.GetDirectoryName(ModPath(@"Giving\GivingDirector.cs")), "*.cs").Select(File.ReadAllText));
        Check(CountOfText(all, "TaxGifts +=") == 1 && gifts2.IndexOf("TaxGifts +=", StringComparison.Ordinal) > gifts2.IndexOf("GiveToCharity", StringComparison.Ordinal)
              && gifts2.IndexOf("TaxGifts +=", StringComparison.Ordinal) < gifts2.IndexOf("GiveToPerson", StringComparison.Ordinal), "giving tax: only a gift to a charity adds to the year's gifts, nothing for a person or a loan does");
        Check(!loans.Contains("GivingGame.Deduct") && !loans.Contains("TaxGifts") && !gifts2.Substring(gifts2.IndexOf("GiveToPerson", StringComparison.Ordinal)).Contains("TaxGifts"),
            "giving tax: a loan, a loan written off and a gift to a person never reach the deduction");
        // What cannot run offline: that the director calls the rules in the order the checks above drive them.
        string give = gifts2.Substring(gifts2.IndexOf("public GiveResult GiveToCharity", StringComparison.Ordinal), gifts2.IndexOf("private void Reward", StringComparison.Ordinal) - gifts2.IndexOf("public GiveResult GiveToCharity", StringComparison.Ordinal));
        Check(give.IndexOf("Roll(GivingGame.Day)", StringComparison.Ordinal) > 0 && give.IndexOf("Roll(GivingGame.Day)", StringComparison.Ordinal) < give.IndexOf("TaxGifts +=", StringComparison.Ordinal)
              && give.IndexOf("TaxGifts +=", StringComparison.Ordinal) < give.IndexOf("ClaimNow();", StringComparison.Ordinal),
            "giving tax: a gift turns the year first, is counted, and is claimed at once, so a gift on the last day of the year is in the game's list before the game reads it");
        Check(director.Contains("GivingYear.Turn(_state, day, GivingGame.DaysPerYear)") && director.Contains("GivingYear.ClaimToday(_state, day, GivingBridges.Income, HoldsNow, DeductNow)")
              && director.Contains("Roll(day);") && director.IndexOf("Roll(day);", StringComparison.Ordinal) < director.IndexOf("ClaimNow();", StringComparison.Ordinal) && !director.Contains("Boundary"),
            "giving tax: the day's pass turns the year, then claims, and no day is skipped");
        Check(ModSource(@"Giving\GivingGame.cs").Contains("LocalizorManager.IsLocalizedKey(DeductName) ? DeductName : DeductName.GetLocalization()") && ModSource(@"Giving\GivingGame.cs").Contains("GivingYear.Ours(list[i].key, DeductName, words)"),
            "giving tax: the deduction goes in the game's list under giving's key, which the game translates for the statement, and is read back by the key and by the words");
        Check(ModSource(@"Giving\GivingDirector.Facts.cs").Contains("GivingYear.Claimed(_state, GivingGame.Deducted())") && ModSource(@"Giving\GivingDirector.Facts.cs").Contains("YearIncome("),
            "giving tax: the year's page shows what the game will deduct and the income its cap is worked out on");
        // The cap's numbers.
        Check(GivingRules.DeductionDue(50000, 0, 310000) != 50000 && GivingRules.DeductionDue(50000, 0, 310000) == 18600, "giving tax: a claim without the cap would be caught: 50,000 of gifts on 310,000 of income is held to 18,600");
    }

    // ---- standing, the press and a person's warmth

    private static void GivingStandingChecks()
    {
        long total = 10000;
        int once = GivingRules.IntegrityOf(total);
        int applied = 0;
        for (int i = 1; i <= 10; i++)
        {
            int target = GivingRules.IntegrityOf(total * i / 10);
            applied += Math.Max(0, target - applied);
        }
        Check(applied == once && once <= GivingCatalogue.IntegrityMost && once >= 1, "giving standing: one gift of 10,000 earns what ten of 1,000 earn (" + once + " points of " + GivingCatalogue.IntegrityMost + ")");
        int small = GivingRules.IntegrityOf(100), big = GivingRules.IntegrityOf(1000000);
        Check(small == 0 && big >= GivingCatalogue.IntegrityMost - 1 && big <= GivingCatalogue.IntegrityMost && GivingRules.IntegrityOf(1000000000) <= GivingCatalogue.IntegrityMost,
            "giving standing: a small gift earns nothing and a huge one comes to the month's most and never passes it (" + big + " of " + GivingCatalogue.IntegrityMost + ")");
        Check(GivingRules.PressOf(0) == 0 && GivingRules.PressOf(100) == 0 && GivingRules.PressOf(100000000) <= GivingCatalogue.PressMost && GivingRules.PressOf(20000) >= 1,
            "giving standing: the press prints nothing of a small gift and no more than the month's most of any");
        long prior = -1;
        bool monotone = true;
        for (long g = 0; g <= 200000; g += 500)
        {
            long v = GivingRules.IntegrityOf(g) * 100L + GivingRules.PressOf(g);
            monotone &= v >= prior;
            prior = v;
        }
        Check(monotone, "giving standing: more given never earns less");
        // A person: the same sum earns the same however it is split, and half as much when they needed nothing.
        var one = new PersonGiving { Person = 1 };
        GivingRules.PersonGain(one, 10000, 100, true, out int a1, out int t1);
        var ten = new PersonGiving { Person = 2 };
        int a10 = 0, t10 = 0;
        for (int i = 0; i < 10; i++)
        {
            GivingRules.PersonGain(ten, 1000, 100 + i, true, out int a, out int t);
            a10 += a;
            t10 += t;
        }
        Check(a1 == a10 && t1 == t10 && a1 <= GivingCatalogue.AffectionMost * 10 && t1 <= GivingCatalogue.TrustMost * 10 && a1 > 0, "giving people: one gift of 10,000 earns what ten of 1,000 earn in a window (" + a1 / 10f + " affection, " + t1 / 10f + " trust)");
        var need = new PersonGiving { Person = 3 };
        var none = new PersonGiving { Person = 4 };
        GivingRules.PersonGain(need, 1000, 100, true, out int an, out _);
        GivingRules.PersonGain(none, 1000, 100, false, out int ao, out _);
        Check(ao < an && ao * 2 >= an - 1, "giving people: a gift to someone in no need counts half");
        var window = new PersonGiving { Person = 5 };
        GivingRules.PersonGain(window, 100000, 100, true, out int w1, out _);
        GivingRules.PersonGain(window, 100000, 100 + GivingCatalogue.WindowDays, true, out int w2, out _);
        GivingRules.PersonGain(window, 100000, 100 + GivingCatalogue.WindowDays + 3, true, out int w3, out _);
        var edge = new PersonGiving { Person = 6 };
        GivingRules.PersonGain(edge, 100000, 100, true, out _, out _);
        GivingRules.PersonGain(edge, 100000, 100 + GivingCatalogue.WindowDays - 1, true, out int w0, out int wt0);
        Check(w1 > 0 && w2 > 0 && w3 == 0 && w0 == 0 && wt0 == 0, "giving people: a new window starts after thirty days, and a gift in a window already full earns nothing more (" + w1 / 10f + ", " + w2 / 10f + ", " + w3 / 10f + ")");
        Check(w1 <= GivingCatalogue.AffectionMost * 10, "giving people: money is no shortcut to the top: a window earns " + GivingCatalogue.AffectionMost + " points of affection at most");
    }

    // ---- the charities' months on a city

    private static void GivingCityChecks()
    {
        PopulationState a = GivingCity(2000, 7), b = GivingCity(2000, 7);
        long before = GivingMoneyOf(a);
        int level = 1000;
        long cap = GivingRules.MonthCap(Charity.FoodBank, a.Households.Live, level);
        int helped = GivingPeople.PayFoodBank(a, cap, level, out long spent);
        int helpedB = GivingPeople.PayFoodBank(b, cap, level, out long spentB);
        Check(helped > 0 && spent > 0 && spent <= cap && helped <= GivingCatalogue.HelpedMost, "giving city: the food bank helps the worst off, within its cap and its most households (" + helped + " for " + spent + ")");
        Check(helped == helpedB && spent == spentB && GivingMoneyOf(a) == GivingMoneyOf(b), "giving city: the same city and money give the same month on every machine");
        Check(GivingMoneyOf(a) - before == spent, "giving city: every dollar a charity pays reaches a household's debt: the households' net money rises by exactly what was paid");
        bool noPlayers = true, noTenants = true;
        PopulationState fresh = GivingCity(2000, 7);
        GivingPeople.PayFoodBank(fresh, 100000000, level, out long _);
        GivingPeople.PayHousing(fresh, 100000000, level, out long _);
        GivingPeople.PayTraining(fresh, 100000000, level, out long _);
        PopulationState clean = GivingCity(2000, 7);
        for (int h = 0; h < fresh.Households.Count; h++)
        {
            bool player = (fresh.Households.Flags[h] & HouseholdFlags.Players) != 0, tenant = fresh.Households.Landlord[h] == Landlords.Player;
            if ((player || tenant) && (fresh.Households.Debt[h] != clean.Households.Debt[h] || fresh.Households.Savings[h] != clean.Households.Savings[h] || fresh.Households.Content[h] != clean.Households.Content[h]))
            {
                noPlayers &= !player;
                noTenants &= !tenant;
            }
        }
        for (int p = 0; p < fresh.People.Count; p++)
        {
            int h = fresh.People.Household[p];
            bool excluded = (fresh.Households.Flags[h] & HouseholdFlags.Players) != 0 || fresh.Households.Landlord[h] == Landlords.Player;
            if (excluded && fresh.People.Skill[p] != clean.People.Skill[p])
            {
                noPlayers = false;
            }
        }
        Check(noPlayers && noTenants, "giving city: the player's own household and the tenants the player lets to are never helped, so no charity money comes back to the player as rent");
        PopulationState rent = GivingCity(2000, 9);
        long rentBefore = GivingMoneyOf(rent);
        int rentHelped = GivingPeople.PayHousing(rent, 1000000, level, out long rentSpent);
        Check(rentHelped > 0 && GivingMoneyOf(rent) - rentBefore == rentSpent && rentSpent <= (long)rentHelped * GivingCatalogue.HelpMost, "giving city: the housing trust pays the rent over the line, a household at most " + GivingCatalogue.HelpMost + ", and the money lands (" + rentHelped + " for " + rentSpent + ")");
        PopulationState train = GivingCity(2000, 11);
        int unemployed = 0, skillBefore = 0;
        for (int p = 0; p < train.People.Count; p++)
        {
            if (train.People.Status[p] == (byte)Status.Unemployed)
            {
                unemployed++;
                skillBefore += train.People.Skill[p];
            }
        }
        int course = GivingRules.At(GivingCatalogue.TrainingCourse, level);
        int taught = GivingPeople.PayTraining(train, course * 10 + 5, level, out long trainSpent);
        int skillAfter = 0;
        for (int p = 0; p < train.People.Count; p++)
        {
            skillAfter += train.People.Status[p] == (byte)Status.Unemployed ? train.People.Skill[p] : 0;
        }
        Check(unemployed > 10 && taught == 10 && trainSpent == (long)course * 10 && skillAfter - skillBefore == 10 * GivingCatalogue.TrainingSkill, "giving city: the training fund pays for as many courses as its money buys, each a few points of skill for the least skilled out of work (" + taught + ")");
        Check(GivingPeople.PayTraining(train, course - 1, level, out long none) == 0 && none == 0 && GivingPeople.PayFoodBank(train, 0, level, out long nothing) == 0 && nothing == 0, "giving city: with less than a course, or no money, nothing is done");
        // Helping a household: the debt first, then the savings, the content a little, and money is exactly conserved.
        PopulationState one = GivingCity(10, 3);
        one.Households.Debt[2] = 500;
        one.Households.Savings[2] = 100;
        GivingPeople.Help(one, 2, 800, out long toDebt, out long toSavings);
        Check(toDebt == 500 && toSavings == 300 && one.Households.Debt[2] == 0 && one.Households.Savings[2] == 400 && one.Households.Content[2] == 60 + GivingCatalogue.ContentLift, "giving city: help pays the debt first and puts the rest in savings");
        one.Households.Savings[2] = 2500;
        long spare = GivingPeople.Spare(one, 2);
        long took = GivingPeople.Take(one, 2, 99999);
        Check(spare > 1500 && spare < 2500 && took == spare && one.Households.Savings[2] == 2500 - took && GivingPeople.Spare(one, 2) == 0 && GivingPeople.Take(one, 2, 5) == 0 && one.Households.Savings[2] == 2500 - took,
            "giving city: a household can pay back no more than it can spare (" + took + " of " + spare + ", the rest kept for its rent)");
        one.Households.Savings[2] = 2500;
        Check(GivingPeople.Take(one, 2, 100) == 100 && one.Households.Savings[2] == 2400 && GivingPeople.Take(one, 2, -5) == 0, "giving city: a household pays the part asked when it can, and nothing for a negative ask");
        // Neighbours: every dollar leaves one household and reaches another.
        PopulationState folks = GivingCity(4000, 21);
        long folksBefore = GivingMoneyOf(folks);
        int neighbours = GivingPeople.Neighbours(folks, 5, level, out long moved);
        Check(neighbours > 0 && moved > 0 && neighbours <= folks.Households.Live / GivingCatalogue.NeighboursPer && GivingMoneyOf(folks) - folksBefore == 0, "giving city: neighbours help one another, one gift for each " + GivingCatalogue.NeighboursPer + " households, and money only moves between households (" + neighbours + " gifts of " + moved + ")");
        // Timing: a month's pass over a city of twenty thousand households.
        PopulationState big = GivingCity(20000, 31);
        long best = long.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            var clock = Stopwatch.StartNew();
            GivingPeople.PayFoodBank(big, 1000000, level, out long _);
            GivingPeople.PayHousing(big, 1000000, level, out long _);
            GivingPeople.PayTraining(big, 1000000, level, out long _);
            GivingPeople.Neighbours(big, round, level, out long _);
            best = Math.Min(best, clock.ElapsedMilliseconds);
        }
        Check(best < 150, "giving city: a month's four passes over 20,000 households take " + best + " ms, under 150 ms (each runs in a second of its own)");
    }

    // ---- money out of the player

    private static void GivingMoneyChecks()
    {
        string game = ModSource(@"Giving\GivingGame.cs"), gifts = ModSource(@"Giving\GivingDirector.Gifts.cs"), wiring = ModSource(@"Wiring\Wiring.GivingLinks.cs");
        Check(game.Contains("BizBooks.Book(") && !game.Contains("GameManager.ChangeMoney") && game.Contains("amount > Spare"), "giving money: money moves only through the mod's one booking call, and a gift larger than the spare cash is refused");
        Check(game.Contains("Cash - Math.Max(0L, reserved)") && game.Contains("GivingBridges.Reserved()") && wiring.Contains("GivingRules.Reserve(tech.FloorNow)"),
            "giving money: only the cash over what the investors of the player's firms are owed can be given, so a firm's account never leaks through a gift");
        // The floor is held in cents and a gift is whole money: the reserve is rounded up, so a gift of exactly the spare cash never leaves the wallet a cent under the floor.
        bool never = true;
        for (long floor = 0; floor <= 100000; floor += 37)
        {
            for (long wallet = floor; wallet <= floor + 400; wallet += 13)
            {
                long spare = Math.Max(0L, wallet / 100 - GivingRules.Reserve(floor));
                never &= wallet - spare * 100 >= floor;
            }
        }
        Check(GivingRules.Reserve(0) == 0 && GivingRules.Reserve(-5) == 0 && GivingRules.Reserve(1) == 1 && GivingRules.Reserve(100) == 1 && GivingRules.Reserve(101) == 2 && GivingRules.Reserve(250050) == 2501 && never,
            "giving money: the investors' floor is rounded up to whole money, so giving every dollar of the spare cash leaves the wallet at or over the floor");
        Check(!game.Contains("VentureGame") && !game.Contains("FirmAccount") && !wiring.Contains("Acc."), "giving money: no path reaches a firm's account: gifts come from the wallet's spare cash alone");
        Check(game.Contains("force: false") && game.Contains("public static void Receive") && CountOfText(ModSource(@"Giving\GivingDirector.Month.cs") + gifts, "GivingGame.Receive") == 0,
            "giving money: a gift is never forced, and no charity, rival or neighbour pays anything into the player's cash");
        Check(CountOfText(ModSource(@"Giving\GivingDirector.Loans.cs"), "GivingGame.Receive") == 1, "giving money: the one thing that comes back to the player is a loan's part, taken from the person's household");
        // A rival's gift only lowers the rival's worth and fills a pot.
        string rules = ModSource(@"Giving\GivingMonth.cs");
        Check(rules.Contains("w.RivalPays(i, gift)") && rules.Contains("s.Credit((Charity)charity, paid, i)") && !rules.Contains("GivingGame.") && wiring.Contains("GivingRules.RivalPaid(amount, t.Worth, RivalRules.At(t.Def.StartWorth, Prices.Level))"),
            "giving money: a rival's gift comes out of their worth over the floor and goes into a pot, never into the player's pocket");
    }

    // ---- loans

    private static void GivingLoanChecks()
    {
        Check(GivingRules.LoanPart(6000) == 1000 && GivingRules.LoanPart(1) == 1 && GivingRules.LoanPart(100) == 17, "giving loans: a loan is paid back in six equal parts, rounded up");
        Check(GivingRules.Repayment(800, 200, 5000) == 200 && GivingRules.Repayment(100, 200, 5000) == 100 && GivingRules.Repayment(800, 200, 50) == 50 && GivingRules.Repayment(800, 200, -9) == 0,
            "giving loans: a part is the most a month asks, what is owed, or what the household can spare, whichever is least, and never negative");
        string loans = ModSource(@"Giving\GivingDirector.Loans.cs");
        string rules = ModSource(@"Giving\GivingLoans.cs");
        Check(rules.Contains("WrittenOff") && !(loans + rules).Contains("DebtClaims") && !(loans + rules).Contains("Justice") && !(loans + rules).Contains("TaxCases") && loans.Contains("SocialLife.Change(person, 0f, -3f, -5f)"),
            "giving loans: a default costs the relationship and no court is asked");
        int lend = loans.IndexOf("public GiveResult Lend", StringComparison.Ordinal);
        Check(loans.IndexOf("GivingLoans.Check(", lend, StringComparison.Ordinal) > lend && loans.IndexOf("GivingLoans.Check(", lend, StringComparison.Ordinal) < loans.IndexOf("GivingGame.Pay(", lend, StringComparison.Ordinal)
              && loans.IndexOf("GivingGame.Pay(", lend, StringComparison.Ordinal) < loans.IndexOf("GivingLoans.Make(", lend, StringComparison.Ordinal) && loans.Contains("GivingLoans.Collect(l, s, day, GivingGame.MonthDays, out long took)"),
            "giving loans: the director asks the rules whether a loan may be made, then takes the money, then makes it, and collects through the rules");
        Check(!loans.Contains("0.5f, 1f") && !loans.Contains("0.2f, 0.5f") && loans.Contains("GivingRules.LoanCounts("), "giving loans: a loan's warmth goes through the person's window and a repayment adds none");
        Check(ModSource(@"Giving\GivingDirector.cs").Contains("_state.Loans[i].Next = day"), "giving loans: after a load or the option back on, no month of the time away is held against the person");
    }

    // ---- who asks

    private static void GivingAskChecks()
    {
        int basic = GivingRules.AskPerTenThousand(0, 0), rich = GivingRules.AskPerTenThousand(5, 100), middle = GivingRules.AskPerTenThousand(2, 30);
        Check(basic > 0 && basic < middle && middle < rich && rich <= basic * 2, "giving asks: the richer and better known the player is, the likelier an ask, to twice the base at most (" + basic + ", " + middle + ", " + rich + " in ten thousand)");
        Check(GivingRules.MayAsk(100, 0, 0, 100, 100) && !GivingRules.MayAsk(105, 100, 0, 100, 100) && GivingRules.MayAsk(114, 100, 0, 100, 100) && !GivingRules.MayAsk(150, 100, 140, 100, 100)
              && GivingRules.MayAsk(200, 100, 140, 100, 100) && !GivingRules.MayAsk(100, 0, 0, 50, 50), "giving asks: not within two weeks of any ask, nor of one person's within two months, nor from someone not close");
        Check(GivingRules.AskAmount(900, 1000) == 900 && GivingRules.AskAmount(99999, 1000) == GivingCatalogue.AskMost && GivingRules.AskAmount(-1, 1000) == 0 && GivingRules.AskAmount(120, 1000) == 100,
            "giving asks: they ask for their debt, rounded to fifty and never over " + GivingCatalogue.AskMost);
        // Only a person in trouble as the population holds it can ask.
        PopulationState s = GivingCity(300, 5);
        int work = 0, debt = 0, rent = 0, nothing = 0;
        for (int p = 0; p < s.People.Count; p++)
        {
            switch (GivingPeople.NeedOf(s, p, out _))
            {
                case GivingNeed.Work: work++; break;
                case GivingNeed.Debt: debt++; break;
                case GivingNeed.Rent: rent++; break;
                default: nothing++; break;
            }
        }
        Check(work > 0 && debt > 0 && rent > 0 && nothing > work, "giving asks: out of work, debt past a month's income and rent past the line are the needs, and most people have none (" + work + ", " + debt + ", " + rent + " of " + s.People.Count + ")");
        // A year of hours: how many asks one person in trouble would make, with the gaps.
        int asks = 0, last = 0;
        for (int day = 1; day <= 360; day++)
        {
            for (int hour = 9; hour < 22; hour++)
            {
                if (GivingRules.Asks(12, 3, day, hour, GivingRules.AskPerTenThousand(2, 30)) && GivingRules.MayAsk(day, last, 0, 80, 80))
                {
                    asks++;
                    last = day;
                }
            }
        }
        Check(asks >= 2 && asks <= 360 / GivingCatalogue.AskGapDays, "giving asks: somebody who may ask asks about once a fortnight at the most, " + asks + " in a year of 360 days");
        // The chance an hour: one in a thousand, and up to twice that for a rich and well known player.
        int hours = 0, hoursRich = 0;
        for (int hour = 0; hour < 100000; hour++)
        {
            hours += GivingRules.Asks(8, 4, hour / 24, hour % 24, GivingRules.AskPerTenThousand(0, 0)) ? 1 : 0;
            hoursRich += GivingRules.Asks(8, 4, hour / 24, hour % 24, GivingRules.AskPerTenThousand(5, 100)) ? 1 : 0;
        }
        Check(GivingRules.AskPerTenThousand(0, 0) == 10 && GivingRules.AskPerTenThousand(5, 100) == 20 && hours > 50 && hours < 160 && hoursRich > hours,
            "giving asks: the chance an hour is 1 in 1,000, and up to twice that for a rich and well known player (" + hours + " and " + hoursRich + " in a hundred thousand hours)");
        // A person in trouble as the population holds them asks for their debt (or a month's rent), and nobody the population does not hold asks at all.
        PopulationState linked = GivingCity(300, 5);
        int asking = 0, asked = 0;
        for (int p = 0; p < linked.People.Count; p++)
        {
            if (GivingPeople.NeedOf(linked, p, out _) == GivingNeed.None)
            {
                continue;
            }
            asking++;
            linked.Links.Add(new PersonLink { Kind = LinkKind.Met, Key = "social:" + (1000 + p), Person = linked.People.Id[p], Slot = p });
            if (GivingPeople.AskOf(linked, 1000 + p, 1000, out GivingNeed need, out int amount))
            {
                asked++;
                Check(need != GivingNeed.None && amount >= 100 && amount <= GivingCatalogue.AskMost && amount % 50 == 0, "giving asks: a person in trouble asks for a round sum between a hundred and " + GivingCatalogue.AskMost + " (" + amount + ")");
            }
            Check(!GivingPeople.AskOf(linked, 5000 + p, 1000, out _, out int none) && none == 0, "giving asks: a person the population does not hold asks for nothing");
        }
        Check(asking > 10 && asked > 0 && asked <= asking, "giving asks: of the people in trouble, those with something to ask for ask (" + asked + " of " + asking + ")");
        string director = ModSource(@"Giving\GivingDirector.Loans.cs"), contact = ModSource(@"Social\SocialDirector.Contact.cs");
        Check(contact.Contains("if (free && Giving.GivingDirector.Instance != null && Giving.GivingDirector.Instance.TryAsk(person))") && director.Contains("person.Stage >= SocialStage.Friend || person.IsFamily"),
            "giving asks: only when no plan or offer is waiting, and only from a friend or family, through the social life's own hour and its text");
        Check(director.Contains("SocialLife.SendText(person, key,") && director.Contains("offer.Kind = SocialOffer.Help") && director.Contains("GivingPeople.AskOf(s, person.Index, GivingGame.Level, out GivingNeed need, out int amount)")
              && director.Contains("GivingRules.Asks(_state.Seed, person.Index, day, hour, chance)"), "giving asks: by text, then answered on the call, and never for someone the population does not hold in trouble");
    }

    // ---- the rivals

    private static void GivingRivalChecks()
    {
        long gift1 = GivingRules.RivalGift(7, 0, 100000000L, 12, out int ch1), gift2 = GivingRules.RivalGift(7, 0, 100000000L, 12, out int ch2);
        Check(gift1 == gift2 && ch1 == ch2 && gift1 > 0, "giving rivals: a tycoon's gift is the same every time for the same save");
        long low = long.MaxValue, high = 0, sum = 0;
        for (int t = 0; t < 40; t++)
        {
            long g = GivingRules.RivalGift(100 + t, t % 3, 100000000L, 12, out int _);
            low = Math.Min(low, g);
            high = Math.Max(high, g);
            sum += g;
        }
        long yearly = 100000000L * GivingCatalogue.RivalYearShare / 1000;
        Check(low * 12 >= yearly * (1000 - GivingCatalogue.RivalSpread) / 1000 - 12 && high * 12 <= yearly * (1000 + GivingCatalogue.RivalSpread) / 1000 + 12 && Math.Abs(sum / 40 * 12 - yearly) < yearly / 4,
            "giving rivals: each tycoon gives a little more or less than the average share of their worth a year (" + GivingCatalogue.RivalYearShare / 10f + "%), within the spread");
        Check(GivingRules.RivalGift(7, 0, 0, 12, out _) == 0 && GivingRules.RivalGift(7, 0, -5, 12, out _) == 0, "giving rivals: no worth, no gift");
        bool seen = false;
        for (int m = 0; m < 24; m++)
        {
            int c = GivingRules.RivalCharity(7, 1, m);
            seen |= c != GivingRules.RivalCharity(7, 1, 0);
            Check(c >= 0 && c < (int)Charity.Count, "giving rivals: a tycoon's charity is one of the three");
        }
        Check(seen, "giving rivals: a tycoon has a favourite and now and then another");
        string wiring = ModSource(@"Wiring\Wiring.GivingLinks.cs"), month = ModSource(@"Giving\GivingMonth.cs");
        Check(wiring.Contains("!GoingPublic.InSession") && month.Contains("r.Month == month"), "giving rivals: they rest in a Going Public session, and give once a month");
        Check(ModSource(@"Rivals\RivalPage.cs").Contains("RivalBridges.GivingLine") && ModSource(@"Giving\GivingText.cs").Contains("reallife_giving_paper_rival"), "giving rivals: their gifts show on the rich list's line, their panel and the paper");
    }

    // ---- the save, old saves, the option off, a session

    private static void GivingStoreChecks()
    {
        var s = new GivingState { Seed = 99, LastDay = 400, Month = 6, PeriodStart = 361, TaxGifts = 12000, TaxClaimed = 7000, StandingMonth = 6, MonthGiven = 2500, StandingDone = 1, PressDone = 2, LastAsk = 390, NeighbourMonth = 5, NeighbourMoney = 800, NeighbourYear = 4000, NeighbourHelped = 12, PaperDay = 399 };
        s.Book(Charity.FoodBank).Pot = 4200;
        s.Book(Charity.Housing).PlayerYear = 900;
        s.Book(Charity.Training).LastPeople = 4;
        s.Standing.Add(new StandingGift { Charity = 1, Amount = 250, Since = 300, Skipped = true, PaidTotal = 1500 });
        s.Loans.Add(new LoanOut { Person = 3, Name = "A Name; with | marks", Principal = 1200, Remaining = 800, Made = 380, Part = 200, Misses = 1, Next = 410, Paid = 400, State = LoanState.Open });
        s.Loans.Add(new LoanOut { Person = 4, Name = "Other", Principal = 500, Remaining = 0, Made = 300, Part = 100, Paid = 500, State = LoanState.Repaid, Closed = 390 });
        s.PersonOf(3, true).Given = 3000;
        s.PersonOf(3, true).AskDay = 350;
        s.RivalBooks[1].Total = 50000;
        s.RivalBooks[1].LastCharity = 2;
        s.Notes.Add(new GivingNote { Day = 399, Charity = 1, Amount = 30000, Giver = -1 });
        string line = GivingStore.Write(s);
        GivingState back = GivingStore.Read(line, out int version);
        Check(back != null && version == GivingStore.Version && GivingStore.Write(back) == line && back.Loans[0].Name == "A Name; with | marks" && back.Standing[0].Skipped,
            "giving save: every part writes and reads back to the same line, a name with marks in it included");
        Check(back.Book(Charity.FoodBank).Pot == 4200 && back.TaxGifts == 12000 && back.TaxClaimed == 7000 && back.OpenLoans() == 1 && back.OutstandingLoans() == 800 && back.LoanOf(3) != null && back.LoanOf(4) == null,
            "giving save: the pots, the year's gifts, and the loans open and closed are read back");
        Check(GivingStore.Read(null, out int v0) == null && v0 == 0 && GivingStore.Read("", out _) == null && GivingStore.Read("nonsense", out _) == null, "giving save: a save from before giving has no line, and a line that is not giving's reads as none");
        GivingState future = GivingStore.Read("v=9^h:5|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|-1|0|99|98^z:later^b:7|8|9;1|2", out int fv);
        Check(future != null && fv == 9 && future.Seed == 5 && future.Book(Charity.FoodBank).Pot == 7 && future.Book(Charity.Housing).Pot == 1, "giving save: a newer line adds fields and parts this reader skips, and its known figures are read");
        GivingState hostile = GivingStore.Read("v=1^h:1|1|1|1|-5|9999999|1|-3^b:-1|-2^s:99|5;1|-4^l:1|0|0|0|0|0|0|0|9|0|x^r:^n:1|1|-9|1", out _);
        Check(hostile != null && hostile.TaxGifts == 0 && hostile.TaxClaimed == 0 && hostile.Standing.Count == 0 && hostile.Loans.Count == 0 && hostile.Notes.Count == 0 && hostile.Book(Charity.FoodBank).Pot == 0,
            "giving save: a line with figures out of bounds is put inside them, and a loan, a standing gift or a note with nothing in it is dropped");
        var full = new GivingState();
        for (int i = 0; i < 20; i++)
        {
            full.Standing.Add(new StandingGift { Charity = i % 3, Amount = 100 });
        }
        GivingState capped = GivingStore.Read(GivingStore.Write(full), out _);
        Check(capped.Standing.Count <= (int)Charity.Count, "giving save: a charity has one standing gift, whatever the line says");
        // The option off, a session, an old save.
        string director = ModSource(@"Giving\GivingDirector.cs"), settings = ModSource(@"Core\Settings.cs"), gifts = ModSource(@"Giving\GivingDirector.Gifts.cs");
        Check(director.Contains("public bool Running => _loaded && !_broken && Settings.Giving;") && director.Contains("if (!Settings.Giving)") && director.Contains("PutAway();") && settings.Contains("public static bool Giving = true;"),
            "giving off: with the option off nothing runs, no standing gift is paid, the line stays in the save and loans are not asked");
        Check(director.Contains("save.modData.Remove(GivingStore.Key)") && ModSource(@"Giving\GivingGuide.cs").Contains(".ResetData("), "giving off: the save tools can forget it");
        string putAway = director.Substring(director.IndexOf("private void PutAway()", StringComparison.Ordinal), 500);
        Check(putAway.Contains("Write();") && putAway.Contains("RemoveApp();") && !putAway.Contains("MonthStep") && ModSource(@"Giving\GivingDirector.App.cs").Contains("_app.Remove();"),
            "giving off: with the option off the app leaves the phone, the line is kept with the month's pass at its step, and the next look puts the app back when it is on");
        Check(gifts.Contains("if (!Running)") && ModSource(@"Giving\GivingGame.cs").Contains("GoingPublic.InSession") && ModSource(@"Giving\GivingGame.cs").Contains("CityPopulation.Following"),
            "giving session: in a Going Public session the city's people are the host's and are never changed by a guest's gift");
        string all = string.Concat(Directory.GetFiles(System.IO.Path.GetDirectoryName(ModPath(@"Giving\GivingDirector.cs")), "*.cs").Select(File.ReadAllText));
        Check(!all.Contains("SharedCity") && !all.Contains("SendOver") && !all.Contains("Network"), "giving session: nothing new goes over the network");
        Check(CountOfText(all, "UnityEngine.Random") == 1 && director.Contains("_state.Seed = UnityEngine.Random.Range(1, int.MaxValue);"), "giving session: the only unseeded draw is the save's own seed, made once and kept; every other draw is a hash of it");
    }

    // ---- the pages and their buttons

    private static void GivingPageChecks()
    {
        Check(UiScreens.All.Any(sc => sc.Id == "giving" && sc.Section == UiScreenSwitches.Ventures), "giving pages: the app has its own switch in Mod settings");
        foreach (string state in UiSamples.GivingStates)
        {
            var stub = new UiSamples.GivingStub();
            VentModel m = UiSamples.Giving(ModEnglish(), state, null, stub);
            bool off = state == "off";
            Check(off ? m.Shape == VentShape.Off : m.Shape == VentShape.Page && m.Nav.Length == 3, "giving pages: " + state + " builds as " + (off ? "the off page" : "a page of three tabs"));
            if (off)
            {
                continue;
            }
            int blocks = m.Left.Length + m.Right.Length;
            Check(blocks >= 3 && m.HeroFigures.Length == 3, "giving pages: " + state + " has its figures and blocks (" + blocks + ")");
            int buttons = 0, primary = 0;
            foreach (VentBlock b in m.Right.Concat(m.Left))
            {
                if (b.Kind != VentBlockKind.Actions)
                {
                    continue;
                }
                foreach (UiActionModel a in b.Actions)
                {
                    buttons++;
                    primary += a.Kind == UiButtonKind.Primary ? 1 : 0;
                    if (a.Disabled)
                    {
                        continue;
                    }
                    int before = stub.Count;
                    bool ran = a.Arg >= 0 && a.Arg < m.Runs.Length && m.Runs[a.Arg] != null && m.Runs[a.Arg]();
                    Check(ran && stub.Count == before + 1, "giving pages: " + state + ": the button '" + a.Label + "' reaches its call (" + stub.Last + ")");
                }
            }
            Check(primary <= 1, "giving pages: " + state + " has one primary action at most (" + primary + ")");
        }
        var press = new UiSamples.GivingStub();
        VentModel pick = UiSamples.Giving(ModEnglish(), "charities_pick", new VentView(), press);
        VentBlock actions = pick.Right.First(b => b.Kind == VentBlockKind.Actions);
        pick.Runs[actions.Actions[actions.Actions.Length - 1].Arg]();
        Check(press.Last == "give" && press.Charity == 1 && press.Step == 5, "giving pages: Give gives the chosen amount to the chosen charity");
        pick.Runs[actions.Actions[actions.Actions.Length - 2].Arg]();
        Check(press.Last == "monthly" && press.Charity == 1, "giving pages: Give every month sets the standing gift");
        VentModel standing = UiSamples.Giving(ModEnglish(), "charities", new VentView(), press);
        VentBlock first = standing.Right.First(b => b.Kind == VentBlockKind.Actions);
        Check(first.Actions.Length == 3 && first.Actions[0].Label.Length > 0, "giving pages: a charity with a standing gift offers to stop it");
        VentModel poor = UiSamples.Giving(ModEnglish(), "charities_broke", new VentView(), press);
        VentBlock giveBlock = poor.Right.First(b => b.Kind == VentBlockKind.Actions);
        Check(giveBlock.Actions[giveBlock.Actions.Length - 1].Disabled, "giving pages: Give is off when the spare cash does not cover the amount");
        VentModel session = UiSamples.Giving(ModEnglish(), "charities_session", new VentView(), press);
        VentBlock sessionBlock = session.Right.First(b => b.Kind == VentBlockKind.Actions);
        Check(sessionBlock.Actions.All(a => a.Kind != UiButtonKind.Primary || a.Disabled), "giving pages: in a session no gift to a charity can be made");
        VentModel step = UiSamples.Giving(ModEnglish(), "charities", new VentView(), press);
        VentBlock stepper = step.Right.First(b => b.Kind == VentBlockKind.Stepper);
        var view = new VentView { A = 3 };
        VentModel more = UiSamples.Giving(ModEnglish(), "charities", view, press);
        VentBlock stepper2 = more.Right.First(b => b.Kind == VentBlockKind.Stepper);
        more.Runs[stepper2.Run2]();
        Check(view.A == 4 && !stepper.CanLess && stepper2.CanLess && stepper2.CanMore, "giving pages: the stepper moves the amount a step up the list");
        Check(UiSamples.Giving(ModEnglish(), "loans", null, press).Left.Any(b => b.Kind == VentBlockKind.Items && b.Items.Length == 4), "giving pages: the loans tab lists every loan, open and closed");
        Check(UiSamples.Giving(ModEnglish(), "year", null, press).Left.Any(b => b.Kind == VentBlockKind.Row && b.Value != null && (b.Value.Contains("18,600") || b.Value.Contains("18600") || b.Value.Contains("18.6"))),
            "giving pages: the year's tab states the cap on the deduction");
        string app = ModSource(@"Giving\GivingApp.cs"), live = ModSource(@"Ui\Ventures\GivingLive.cs");
        Check(app.Contains("GivingLive.Instance.Give(") && app.Contains("GivingLive.Instance.Monthly(") && app.Contains("GivingLive.Instance.StopMonthly(") && live.Contains("d.GiveToCharity(") && live.Contains("d.SetStanding(") && live.Contains("d.CancelStanding("),
            "giving pages: the app in the game's frame and the kit's page press the same calls of the director");
        string call = ModSource(@"Social\PersonDialog.Giving.cs") + ModSource(@"Social\PersonDialog.cs");
        Check(call.Contains("GiveFlow(false, 0)") && call.Contains("GiveFlow(true, 0)") && call.Contains("giver.Lend(_p, amount)") && call.Contains("giver.GiveToPerson(_p, amount, false)") && call.Contains("return Plea();")
              && call.Contains("PleaLess(") && call.Contains("PleaLend(") && call.Contains("PleaNo"), "giving pages: the call offers Give money and Lend money, and a plea is answered by giving, giving less, lending or saying no");
        Check(ModSource(@"Social\PersonDialog.cs").Contains("if (Giver != null && Giving.GivingDirector.CauseWhy == 0)"), "giving pages: the call offers Give money and Lend money only where the city's people are there to receive them");
        string gifts3 = ModSource(@"Giving\GivingDirector.Gifts.cs"), loans3 = ModSource(@"Giving\GivingDirector.Loans.cs");
        int toPerson = gifts3.IndexOf("public GiveResult GiveToPerson", StringComparison.Ordinal);
        int lend3 = loans3.IndexOf("public GiveResult Lend", StringComparison.Ordinal);
        Check(gifts3.IndexOf("GivingGame.People()", toPerson, StringComparison.Ordinal) > toPerson && gifts3.IndexOf("GivingGame.People()", toPerson, StringComparison.Ordinal) < gifts3.IndexOf("GivingGame.Pay(", toPerson, StringComparison.Ordinal)
              && gifts3.IndexOf("GiveResult.Session", toPerson, StringComparison.Ordinal) < gifts3.IndexOf("GivingGame.Pay(", toPerson, StringComparison.Ordinal)
              && loans3.IndexOf("GiveResult.Session", lend3, StringComparison.Ordinal) < loans3.IndexOf("GivingGame.Pay(", lend3, StringComparison.Ordinal),
            "giving session: a gift or a loan to a person is refused before any money is taken when there are no people to receive it (a session, the living population off)");
        Check(gifts3.IndexOf("Answered(person);", toPerson, StringComparison.Ordinal) > gifts3.IndexOf("GivingGame.Pay(", toPerson, StringComparison.Ordinal) && loans3.IndexOf("Answered(person);", lend3, StringComparison.Ordinal) > loans3.IndexOf("GivingGame.Pay(", lend3, StringComparison.Ordinal)
              && gifts3.Contains("offer.Kind == SocialOffer.Help && offer.Person == person.Index") && gifts3.Contains("offer.Clear();"),
            "giving asks: money given or lent to somebody who is asking closes the ask, so it is never left to run out and cost trust");
        Check(gifts3.Contains("_state.StandingDone += standing") && gifts3.Contains("_state.PressDone += press") && gifts3.Contains("GivingBridges.Integrity(points - _state.StandingDone)") && !gifts3.Contains("StandingDone = points"),
            "giving standing: what is counted as earned is what Fame really applied, so a part it held back is asked again with the next gift");
    }

    private static UiWords ModEnglish()
    {
        Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        return (key, english) => words != null && words.TryGetValue(key, out string v) ? v : english;
    }

    // ---- the sources

    private static string ModPath(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));

    private static void GivingSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Giving\GivingDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 15, "giving sources: the feature's files are found (" + files.Length + ")");
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("Update()") && !text.Contains("FindObjectsByType"), "giving sources: " + name + " books no money outside the one call and has no per-frame code or scene search");
            Check(!text.Contains("ProjectSettings") && !text.Contains("TODO") && !text.Contains("NotImplemented"), "giving sources: " + name + " has no stub and no note of work left");
        }
        string pop = ModSource(@"Giving\GivingPeople.cs");
        Check(pop.Contains("Landlords.Player") && pop.Contains("HouseholdFlags.Players") && pop.Contains("PersonFlags.Linked | PersonFlags.Frozen"), "giving sources: the player's household, the player's tenants and the people another feature owns are never helped");
        string dir = ModSource(@"Giving\GivingDirector.cs");
        Check(dir.Contains("StartCoroutine(Guard.Routine(Tick()))") && dir.Contains("catch (Exception e)"), "giving sources: the timer is a guarded coroutine, once a second, and a fault switches the feature off for the session");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "GivingLinks") == 2, "giving sources: the wiring connects and disconnects giving");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Giving.GivingGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.Giving\", \"giving\"") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"giving\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"giving\""), "giving sources: the feature has its option, its Guide page, its owner and its place in the cities list");
    }

    // ---- the texts

    private static void GivingTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources = { @"Giving\GivingDirector.cs", @"Giving\GivingDirector.App.cs", @"Giving\GivingDirector.Facts.cs", @"Giving\GivingDirector.Gifts.cs", @"Giving\GivingDirector.Loans.cs", @"Giving\GivingDirector.Month.cs",
            @"Giving\GivingDirector.Tests.cs", @"Giving\GivingGuide.cs", @"Giving\GivingText.cs", @"Giving\GivingApp.cs", @"Giving\GiveResult.cs", @"Giving\GivingGame.cs", @"Ui\Ventures\GivePages.cs", @"Ui\Ventures\GivingLive.cs",
            @"Social\PersonDialog.Giving.cs", @"Wiring\Wiring.GivingLinks.cs" };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_giving_btn_give", "reallife_giving_btn_lend", "reallife_opt_sec_giving", "reallife_opt_tip_giving", "reallife_ui_screen_giving", "reallife_opt_tip_ui_screen_giving" })
        {
            keys.Add(k);
        }
        keys.Remove("reallife_giving_charity_");
        keys.Remove("reallife_ui_dialog_cost_now");
        Check(keys.Count >= 130, "giving texts: its keys are found: " + keys.Count);
        string[] notices = { "reallife_giving_gave", "reallife_giving_monthly_set", "reallife_giving_monthly_full", "reallife_giving_monthly_stopped", "reallife_giving_standing_skipped", "reallife_giving_loan_part",
            "reallife_giving_loan_repaid", "reallife_giving_loan_off", "reallife_giving_result_done", "reallife_giving_result_done_only", "reallife_giving_result_off", "reallife_giving_result_broke",
            "reallife_giving_result_no_cause", "reallife_giving_result_session", "reallife_giving_result_no_person", "reallife_giving_result_limit", "reallife_giving_result_nothing", "reallife_giving_test_gift",
            "reallife_giving_test_month", "reallife_giving_test_person", "reallife_giving_test_lend", "reallife_giving_test_ask", "reallife_giving_test_nobody", "reallife_giving_test_nobody_in_trouble",
            "reallife_giving_test_back", "reallife_giving_test_back_none", "reallife_giving_test_off" };
        string[] facts = { "{giving_deduct}", "{giving_loan_parts}", "{giving_loan_most}", "{giving_gift_most}", "{giving_loans_most}", "{giving_ask_gap}", "{giving_standing_most}", "{giving_misses}", "{giving_big}", "{giving_window}" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string code in GivingLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "giving texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "giving texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "giving texts: " + key + " has no exclamation mark or dash in " + code);
                if (english != null && english.TryGetValue(key, out string en))
                {
                    var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    Check(wanted.SetEquals(got), "giving texts: " + key + " keeps its {names} in " + code);
                    Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "giving texts: " + key + " keeps its tags and lines in " + code);
                }
            }
            foreach (string key in notices)
            {
                Check(words.TryGetValue(key, out string value) && value.Length <= 240, "giving texts: the notice " + key + " is at most 240 characters in " + code);
            }
            if (words.TryGetValue("reallife_guide_giving", out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "giving texts: the Guide page has " + fact + " in " + code);
                }
            }
        }
        string guide = ModSource(@"Giving\GivingGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "giving texts: the Guide's " + fact + " is filled from the rules");
        }
        // What's new: the feature's line in the next row of every language, before the first fix mark.
        string sentence = english != null && english.TryGetValue("reallife_giving_app", out string unused) ? "You can now give to the city's charities once or every month" : string.Empty;
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int i = 0; i < GivingLanguages.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, GivingLanguages[i] + ".json"));
            string next = WhatsNewText(words, WhatsNewKey(LoadWords(System.IO.Path.Combine(DesignLocales, "en.json")), sentence));   // the row that holds the line: the next row, or the version's once numbered
            int mark = next.IndexOf(marks[i], StringComparison.Ordinal);
            string before = mark >= 0 ? next.Substring(0, mark) : next;
            if (i == 0)
            {
                Check(next.Contains(sentence) && next.IndexOf(sentence, StringComparison.Ordinal) < (mark < 0 ? int.MaxValue : mark), "giving texts: What's new has the feature's line before the first fix mark in en");
            }
            else
            {
                Check(!next.Contains(sentence) && before.Length > 40, "giving texts: What's new has the feature's line, translated, in " + GivingLanguages[i]);
            }
        }
    }
}
