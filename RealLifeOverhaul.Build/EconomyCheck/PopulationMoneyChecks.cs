using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using PopLedger = RealLifeOverhaul.Population.Ledger;

// A generated city's households and the money they start with (the owner's beta.3 save, day 1: a middle-class man of 30 alone "At home" with no pay,
// savings of nothing and his first day's rent his debt): somebody brings money in wherever somebody stays home, on every way a household is made;
// savings and card debt by usual income and age as the Survey of Consumer Finances 2022 has them; a first rent puts nobody in debt; the commuters
// are the jobs residents do not fill, a Manhattan share; and every person a household's, a building's or a district's screen leads to is there.
internal static partial class Program
{
    private static void PopulationMoneyChecks()
    {
        PopulationSim sim = PopulationMade(20261101, out PopulationState s, out _);
        int unprovided = Unprovided(s, out int homeAlone);
        Check(unprovided == 0 && homeAlone == 0,
            "population money: nobody is at home of working age in a household with nobody in work, looking for work or on a pension, and nobody lives alone at home ("
            + unprovided + " households, " + homeAlone + " alone)");

        HouseholdTable hh = s.Households;
        var days = new List<long>[6];
        var young = new List<long>();
        var old = new List<long>();
        var card = new List<long>();
        int households = 0, carrying = 0, noIncome = 0, noIncomeShort = 0, roseFirstDay = 0;
        for (int i = 0; i < days.Length; i++)
        {
            days[i] = new List<long>();
        }
        for (int h = 0; h < hh.Count; h++)
        {
            if (!hh.Alive(h))
            {
                continue;
            }
            households++;
            long usual = SpendRules.UsualDaily(s, h);
            roseFirstDay += hh.Month[h * PopLedger.Lines + PopLedger.DebtChange] > 0 ? 1 : 0;
            if (usual <= 0)
            {
                noIncome++;
                noIncomeShort += hh.Savings[h] * 100L < (long)hh.Rent[h] * (PopulationCatalogue.SavingsRentDays - 2) ? 1 : 0;
                continue;
            }
            long inDays = (long)hh.Savings[h] * 100 * 1000 / usual;   // thousandths of a day of usual income
            days[GenerateRules.IncomeGroup(usual)].Add(inDays);
            int age = s.Age(hh.First[h], s.Day);
            (age < 35 ? young : age >= 65 ? old : null)?.Add(inDays);
            if (hh.Debt[h] > 0)
            {
                carrying++;
                card.Add((long)hh.Debt[h] * 100 * 1000 / usual);
            }
        }
        var all = new List<long>();
        foreach (List<long> group in days)
        {
            all.AddRange(group);
        }
        long median = Median(all), lowest = Median(days[0]), highest = Median(days[5]), medianYoung = Median(young), medianOld = Median(old);
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            var groups = new string[6];
            for (int i = 0; i < 6; i++)
            {
                groups[i] = days[i].Count + " at " + Median(days[i]) / 1000;
            }
            Console.WriteLine("  money: savings a median " + median / 1000 + " days of usual income (by the survey's income groups " + string.Join(", ", groups) + "; heads under 35 "
                              + medianYoung / 1000 + ", 65 and over " + medianOld / 1000 + "); " + carrying * 1000 / Math.Max(1, households) / 10.0 + "% carry card debt of a median "
                              + Median(card) / 1000 + " days; " + noIncome + " households with no usual income (students); " + roseFirstDay + " in more debt after the first day");
        }
        Check(median >= 120000 && median <= 400000 && lowest < highest && medianOld > medianYoung * 3 / 2,
            "population money: savings are days of usual income as the survey has them (all families' financial assets a median 202 days, more in a city as rich as this), more days the richer and the older ("
            + median / 1000 + " days; poorest " + lowest / 1000 + ", richest " + highest / 1000 + "; under 35 " + medianYoung / 1000 + ", 65 and over " + medianOld / 1000 + ")");
        Check(carrying * 1000 / Math.Max(1, households) >= 300 && carrying * 1000 / Math.Max(1, households) <= 520 && Median(card) >= 5000 && Median(card) <= 20000,
            "population money: about half carry a card balance of some days' income (the survey: 45.2%, a median $2,700), no household starts deep in debt ("
            + carrying * 100 / Math.Max(1, households) + "%, " + Median(card) / 1000 + " days)");
        GenerateRules.StartCheck(s, out int unprovidedLogged, out int deeperLogged);
        Check(roseFirstDay == 0 && noIncomeShort == 0 && noIncome > 0 && unprovidedLogged == unprovided && deeperLogged == roseFirstDay,
            "population money: every household comes with its first month's rent and deposit at least, so the first day's rent put none into debt ("
            + roseFirstDay + " in more debt; " + noIncomeShort + " of the " + noIncome + " with no usual income, students, short of it)");
        Check(GenerateRules.LadderDays(PopulationCatalogue.UsualMiddle) == 158 && GenerateRules.LadderDays(1) == 25 && GenerateRules.LadderDays(PopulationCatalogue.UsualMiddle * 20L) == 1187
              && GenerateRules.IncomeGroup(PopulationCatalogue.UsualMiddle) == 2 && GenerateRules.IncomeGroup(0) == 0 && GenerateRules.MoneyAge(30) == 0 && GenerateRules.MoneyAge(80) == 5
              && GenerateRules.StartSavings(1, 2, 0, 40) == 0 && GenerateRules.StartCard(1, 2, 0, 40) == 0 && GenerateRules.StartSavings(9, 9, 19249, 40) == GenerateRules.StartSavings(9, 9, 19249, 40),
            "population money: the ladder's ends and middle, the income and age groups, nothing for no income, the same draw every time");

        int middleLabour = 0, middleOut = 0, middleAdults = 0, middleNoJob = 0;
        for (int p = 0; p < s.People.Count; p++)
        {
            int h = s.People.Household[p];
            if (s.People.Id[p] == 0 || h < 0 || hh.Class[h] != (byte)PopClass.Middle)
            {
                continue;
            }
            int age = s.Age(p, s.Day);
            var status = (Status)s.People.Status[p];
            middleLabour += status == Status.Worker || status == Status.Unemployed ? 1 : 0;
            middleOut += status == Status.Unemployed ? 1 : 0;
            if (age >= 25 && age < 65)
            {
                middleAdults++;
                middleNoJob += status != Status.Worker ? 1 : 0;
            }
        }
        Check(middleOut * 1000 / Math.Max(1, middleLabour) <= 80 && middleNoJob * 1000 / Math.Max(1, middleAdults) <= 200,
            "population money: few of the middle class are without a job: out of work at the city's rate, and most of the rest caring for a family another earner keeps ("
            + middleOut * 1000 / Math.Max(1, middleLabour) / 10.0 + "% of its labour force, " + middleNoJob * 100 / Math.Max(1, middleAdults) + "% of its adults 25 to 64 not in work)");

        int jobs = 0, filled = 0, commuters = 0, open = 0, workers = 0;
        for (int e = 0; e < s.Employers.Count; e++)
        {
            jobs += s.Employers.Slots[e];
            filled += s.Employers.Filled[e];
            commuters += s.Employers.Commuters[e];
            open += Math.Max(0, s.Employers.Slots[e] - s.Employers.Filled[e] - s.Employers.Commuters[e]);
        }
        for (int p = 0; p < s.People.Count; p++)
        {
            workers += s.People.Id[p] != 0 && s.People.Status[p] == (byte)Status.Worker && s.People.Job[p] >= 0 ? 1 : 0;
        }
        int share = commuters * 1000 / Math.Max(1, jobs);
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  work: the middle class " + middleOut * 1000 / Math.Max(1, middleLabour) / 10.0 + "% out of work, " + middleNoJob * 100 / Math.Max(1, middleAdults)
                              + "% of its adults 25 to 64 not in work; " + jobs + " jobs, " + workers + " residents in them, " + commuters + " commuters (" + share / 10.0 + "%), " + open + " open");
        }
        Check(filled == workers && commuters == s.Region.Commuting && filled + commuters + open == jobs && share >= 650 && share <= 800 && jobs * 100 / Math.Max(1, s.People.Live) <= 250,
            "population money: the jobs are the residents in work, the commuters and the open places, and the commuters hold a Manhattan share (71% of its workers lived outside it in 2017) of a city of about two jobs a resident ("
            + jobs + " jobs, " + workers + " residents, " + commuters + " commuters " + share / 10.0 + "%, " + open + " open, " + jobs * 100 / Math.Max(1, s.People.Live) / 100.0 + " a resident)");

        int unresolved = UnresolvedViews(s, sim.Demand, out int followed, out string first);
        Check(unresolved == 0 && followed > s.People.Live, "population views: every row of the city's, each district's, building's, household's and person's screen leads to a record that is there ("
                                                           + followed + " followed, " + unresolved + " not" + (first != null ? ": " + first : string.Empty) + ")");
        ViewChecks(s, sim.Demand);

        PopulationSim lived = PopulationMade(77, out PopulationState later, out _, 60);
        for (int day = 2; day <= 121; day++)
        {
            lived.BeginDay(day, Ordinary, null);
            lived.RunAll();
        }
        int unprovidedLater = Unprovided(later, out int homeAloneLater);
        int unresolvedLater = UnresolvedViews(later, lived.Demand, out int followedLater, out string firstLater);
        Check(unprovidedLater == 0 && homeAloneLater == 0 && unresolvedLater == 0 && followedLater > later.People.Live,
            "population money: after two years of births, deaths, partings, children leaving home and arrivals, still nobody at home with no money coming in, and every screen's row there ("
            + unprovidedLater + ", " + homeAloneLater + " alone; " + unresolvedLater + " of " + followedLater + (firstLater != null ? ": " + firstLater : string.Empty) + ")");
    }

    // Households with an adult of working age at home and nobody in work, looking for work or on a pension; and people at home living alone.
    private static int Unprovided(PopulationState s, out int homeAlone)
    {
        int count = 0;
        homeAlone = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (!s.Households.Alive(h))
            {
                continue;
            }
            bool earner = false, home = false;
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                var status = (Status)s.People.Status[p];
                earner |= status == Status.Worker || status == Status.Unemployed || status == Status.Retired;
                int age = s.Age(p, s.Day);
                home |= status == Status.Home && age >= PopulationCatalogue.AdultLeast && age < PopulationCatalogue.RetireAge;
            }
            count += home && !earner ? 1 : 0;
            homeAlone += s.Households.Size[h] == 1 && s.People.Status[s.Households.First[h]] == (byte)Status.Home ? 1 : 0;
        }
        return count;
    }

    // Every row's target of the city's screen, every district's, every building's, every household's and every person's: how many were followed, how
    // many lead nowhere (a slot that is not alive, or holds another record than the one the row was made for), and the first of those.
    private static int UnresolvedViews(PopulationState s, PoolRules.DemandDay dd, out int followed, out string first)
    {
        int bad = 0, count = 0;
        string firstBad = null;
        void Follow(PopulationView from, ViewTarget to)
        {
            count++;
            if (!to.Valid || !PopulationViews.Resolves(s, to))
            {
                bad++;
                firstBad = firstBad ?? from.Kind + " " + from.Index + " to " + to.Kind + " " + to.Index + " (id " + to.Id + ")";
            }
        }
        void Rows(PopulationView v)
        {
            if (v == null || v.Gone)
            {
                bad++;
                firstBad = firstBad ?? (v == null ? "a screen not made" : v.Kind + " " + v.Index + " gone");
                return;
            }
            foreach (ViewRow r in v.Rows)
            {
                Follow(v, r.Target);
            }
            if (v.Primary != null)
            {
                Follow(v, v.PrimaryTarget);
            }
        }
        Rows(PopulationViews.Open(s, dd, ViewTarget.To(ViewKind.City, 0)));
        for (int d = 0; d < s.Districts.Count && d < s.Figures.Length; d++)
        {
            Rows(PopulationViews.Open(s, dd, ViewTarget.To(ViewKind.District, d)));
        }
        for (int b = 0; b < s.Buildings.Count; b++)
        {
            if (b < s.FlatCount.Length && s.FlatCount[b] > 0)
            {
                Rows(PopulationViews.Open(s, dd, ViewTarget.To(ViewKind.Building, b)));
            }
        }
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (s.Households.Alive(h))
            {
                PopulationView v = PopulationViews.Open(s, dd, ViewTarget.To(ViewKind.Household, h, s.Households.Id[h]));
                Rows(v);
                bad += v != null && v.Rows.Count != Math.Min(PopulationViews.RowsMost, (int)s.Households.Size[h]) ? 1 : 0;
            }
        }
        for (int p = 0; p < s.People.Count; p++)
        {
            // a person's own screen for those whose pay sums no employer (a worker's sums its whole staff, too slow for every one of them here); every
            // person is reached as a household's row above
            if (s.People.Alive(p) && s.People.Household[p] >= 0 && (s.People.Status[p] != (byte)Status.Worker || p % 97 == 0))
            {
                Rows(PopulationViews.Open(s, dd, ViewTarget.To(ViewKind.Person, p, s.People.Id[p])));
            }
        }
        followed = count;
        first = firstBad;
        return bad;
    }

    // A person's screen: who they live with as its list, words that they live alone, gone once their slot holds somebody else; the same for a household.
    private static void ViewChecks(PopulationState s, PoolRules.DemandDay dd)
    {
        int alone = -1, shared = -1;
        for (int h = 0; h < s.Households.Count && (alone < 0 || shared < 0); h++)
        {
            if (!s.Households.Alive(h))
            {
                continue;
            }
            if (s.Households.Size[h] == 1 && alone < 0)
            {
                alone = s.Households.First[h];
            }
            else if (s.Households.Size[h] >= 3 && shared < 0)
            {
                shared = s.Households.First[h];
            }
        }
        PopulationView single = PopulationViews.Person(s, alone, s.People.Id[alone]);
        PopulationView family = PopulationViews.Person(s, shared, s.People.Id[shared]);
        int home = s.People.Household[shared];
        Check(!single.Gone && single.Rows.Count == 0 && single.Empty == "reallife_pop_empty_alone" && !family.Gone && family.Rows.Count == s.Households.Size[home] - 1
              && family.RowsHeader == "reallife_pop_rows_lives_with" && family.PrimaryTarget.Kind == ViewKind.Household && family.PrimaryTarget.Id == s.Households.Id[home],
            "population views: a person's screen lists who they live with, says they live alone when nobody does, and leads to their household (the beta's \"This person has moved on.\" was the empty list of a person there)");
        PopulationView stale = PopulationViews.Person(s, shared, s.People.Id[shared] + 1);
        PopulationView staleHome = PopulationViews.Household(s, dd, home, s.Households.Id[home] + 1);
        PopulationView ended = PopulationViews.Person(s, -1, 0);
        Check(stale.Gone && stale.Empty == "reallife_pop_empty_person" && stale.Rows.Count == 0 && staleHome.Gone && staleHome.Empty == "reallife_pop_empty_household" && ended.Gone
              && !PopulationViews.Resolves(s, ViewTarget.To(ViewKind.Person, shared, s.People.Id[shared] + 1)) && PopulationViews.Resolves(s, ViewTarget.To(ViewKind.Person, shared, s.People.Id[shared])),
            "population views: a screen opened for somebody whose slot now holds another person or household opens as gone, never as the other");
    }

    private static long Median(List<long> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        var sorted = new List<long>(values);
        sorted.Sort();
        return sorted[sorted.Count / 2];
    }
}
