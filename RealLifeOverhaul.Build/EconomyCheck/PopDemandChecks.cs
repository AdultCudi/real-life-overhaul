using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;

// What the city's people bring the mod's own businesses (the brief bizpop, 1 Nov 2026: the own bank, hotels, car hire, the landlord and the agency, the
// builder, the showroom, online shops, the marketplace, the academy, broadband, the council and the labour market off their fixed figures and onto the
// population), game-free: the day's measure counts every household, person and cent once (each district's figures sum to the city's, the takings and the taxes
// are the pools' own); nothing moves before the anchor and every demand reads a thousand on the anchor day; the same seed gives the same factors; ten years
// without the player keep every factor sane; the save's line and the shared city's part carry it whole; the screens hold together; and the measure fits the
// population's slice.
internal static partial class Program
{
    // Each day's factors over the ten years (PopDemandYears), for the businesses' own checks of their figures against the money scale.
    private static int[][] _demandYears;
    private static PopDemandState _demandLast;

    private static void PopDemandChecks()
    {
        PopDemandMeasure();
        PopDemandAnchor();
        PopDemandRulesChecks();
        PopDemandYears();
        PopDemandSave();
        PopDemandShareChecks();
        PopDemandViewsChecks();
        PopDemandCosts();
        PopDemandBusinessChecks();   // each business on the people, after the ten years it reads (PopDemandBusinessChecks.cs)
        PopDemandTextChecks();       // its words and sources (PopDemandTextChecks.cs)
    }

    // A city generated from a seed, its first day lived and measured.
    private static PopDemandState DemandMade(int seed, out PopulationSim sim, out PopulationState s)
    {
        sim = PopulationMade(seed, out s, out _);
        var m = new PopDemandState();
        var job = new PopDemandJob();
        job.Begin(s, sim.Demand, sim.Pulls, m);
        job.RunAll();
        return m;
    }

    // A day lived and measured.
    private static void DemandDay(PopulationSim sim, PopulationState s, PopDemandState m, PopDemandJob job, int day)
    {
        sim.BeginDay(day, Ordinary, null);
        sim.RunAll();
        job.Begin(s, sim.Demand, sim.Pulls, m);
        job.RunAll();
    }

    // ---------------------------------------------------------------- the day's measure: every household, person and cent once

    private static void PopDemandMeasure()
    {
        PopDemandState m = DemandMade(20261101, out PopulationSim sim, out PopulationState s);
        int households = 0, people = 0, adults = 0;
        long savings = 0, debt = 0, wealth = 0, borrow = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (!s.Households.Alive(h) || (s.Households.Flags[h] & HouseholdFlags.Players) != 0)
            {
                continue;
            }
            savings += s.Households.Savings[h];
            SpendRules.Income(s, h, s.Day, 0, out DayIncome di);
            borrow += PopDemandRules.Band(di.Disposable, s.Households.Debt[h], SpendRules.Level(di.Disposable, s.Median)) >= 0 ? Math.Max(0, di.Disposable) : 0;
            debt += di.Disposable > 0 && (s.Households.Flags[h] & HouseholdFlags.Away) == 0 ? Math.Min(s.Households.Debt[h], (long)PopDemandCatalogue.PoorDebtDays * di.Disposable / 100) : 0;
            wealth += s.Households.Class[h] >= (int)PopClass.Middle ? s.Households.Savings[h] : 0;
            if ((s.Households.Flags[h] & HouseholdFlags.Away) == 0)
            {
                households++;
            }
        }
        for (int p = 0; p < s.People.Count; p++)
        {
            int h = s.People.Household[p];
            if (s.People.Id[p] == 0 || (s.People.Flags[p] & PersonFlags.Frozen) != 0 || h < 0 || (s.Households.Flags[h] & HouseholdFlags.Away) != 0)
            {
                continue;
            }
            people++;
            adults += s.Age(p, s.Day) >= PopulationCatalogue.AdultLeast ? 1 : 0;
        }
        int dHouseholds = 0, dPeople = 0, dAdults = 0, dBusinesses = 0;
        long dDebt = 0;
        for (int d = 0; d < m.Districts; d++)
        {
            dHouseholds += m.Households[d];
            dPeople += m.People[d];
            dAdults += m.Adults[d];
            dBusinesses += m.Businesses[d];
            dDebt += m.Debt[d];
        }
        Check(m.Day == s.Day && m.Districts == s.Districts.Count, "population demand: the first day measured in every district of the city (" + m.Districts + ")");
        Check(m.Now[(int)PopSource.Savings] == savings && m.Now[(int)PopSource.Wealth] == wealth && savings > 0,
            "population demand: the households' savings are counted once, the player's household left out (" + savings + ")");
        Check(m.Now[(int)PopSource.BorrowPoor] + m.Now[(int)PopSource.BorrowFair] + m.Now[(int)PopSource.BorrowGood] == borrow && dDebt == debt && borrow > 0,
            "population demand: every household not in default falls in one credit band with its income, and the districts' debt (each to the poor band's line) is the city's (" + borrow / 100 + ", " + debt + ")");
        Check(dHouseholds == households && m.Now[(int)PopSource.Households] == households && dPeople == people && dAdults == adults && m.Now[(int)PopSource.Adults] == adults,
            "population demand: the districts' households, people and adults sum to the city's (" + households + ", " + people + ", " + adults + ")");
        long takings = 0;
        int seen = 0;
        for (int b = 0; b < s.Businesses.Count; b++)
        {
            takings += s.Businesses.Seen[b] ? s.Businesses.Takings[b] : 0;
            seen += s.Businesses.Seen[b] ? 1 : 0;
        }
        Check(m.Now[(int)PopSource.Turnover] == takings && takings == sim.Demand.Takings && dBusinesses == seen,
            "population demand: the businesses' takings are the pools' own to the cent, each business counted in its district (" + takings / 100 + ")");
        long incomeTax = 0, property = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (!s.Households.Alive(h) || (s.Households.Flags[h] & HouseholdFlags.Players) != 0)
            {
                continue;
            }
            SpendRules.Income(s, h, s.Day, 0, out DayIncome inc);
            incomeTax += inc.IncomeTax;
            property += (long)s.Households.Rent[h] * PopulationCatalogue.PropertyTaxShare / 1000;
        }
        long sales = takings * PopulationCatalogue.SalesTax / 1000;
        Check(Math.Abs(m.Now[(int)PopSource.Taxes] - (incomeTax + property + sales)) <= s.Businesses.Count && m.Now[(int)PopSource.Taxes] > 0,
            "population demand: the day's taxes are the households' income and property tax and the sales tax in the takings (" + m.Now[(int)PopSource.Taxes] / 100 + ")");
        bool every = true;
        for (int i = 0; i < PopDemandCatalogue.Sources; i++)
        {
            every &= i == (int)PopSource.Pull || m.Now[i] > 0;   // the pulls are worked out at a month's turn
        }
        Check(every, "population demand: every source but the districts' pull has a figure on the city's first day");
        bool recipes = PopDemandCatalogue.Recipes.Length == PopDemandCatalogue.Demands && PopDemandViews.DemandKeys.Length == PopDemandCatalogue.Demands
                       && PopDemandViews.SourceKeys.Length == PopDemandCatalogue.Sources;
        foreach ((PopSource, int)[] r in PopDemandCatalogue.Recipes)
        {
            int sum = 0;
            foreach ((PopSource _, int w) in r)
            {
                sum += w;
            }
            recipes &= sum == 1000;
        }
        Check(recipes, "population demand: every demand has a recipe whose weights sum to a thousand, a name, and every source a name");
        int[] grouped = new int[PopDemandCatalogue.Demands];
        foreach ((string _, PopDemand[] ds) in PopDemandViews.Groups)
        {
            foreach (PopDemand d in ds)
            {
                grouped[(int)d]++;
            }
        }
        Check(Array.TrueForAll(grouped, g => g == 1), "population demand: every demand is shown in exactly one business type's screen");
    }

    // ---------------------------------------------------------------- the anchor: nothing jumps

    private static void PopDemandAnchor()
    {
        PopDemandState m = DemandMade(42, out PopulationSim sim, out PopulationState s);
        Check(m.AnchorDay < 0 && Array.TrueForAll(m.Factors, f => f == 1000) && Array.TrueForAll(m.OnlineFactors, f => f == 1000),
            "population demand: a city on its first morning is not anchored yet, and every business keeps its own figures (every factor a thousand)");
        var job = new PopDemandJob();
        int day = s.Day, anchored = -1;
        int first = GenerateRules.MonthOf(s.Generated, s.YearDays);
        while (m.AnchorDay < 0 && day < s.Generated + s.YearDays)
        {
            DemandDay(sim, s, m, job, ++day);
            if (m.AnchorDay < 0)
            {
                Check(Array.TrueForAll(m.Factors, f => f == 1000), "population demand: every factor stays a thousand until the city has lived through its first months' turns (day " + day + ")");
            }
            anchored = m.AnchorDay;
        }
        Check(anchored == day && s.MonthNumber == first + PopDemandCatalogue.SettleMonths && PopDemandCatalogue.SettleMonths >= 2,
            "population demand: the anchor is taken on the first day after the city's first " + PopDemandCatalogue.SettleMonths + " months, the generation's moving wave behind it (day " + anchored + ")");
        bool all = Array.TrueForAll(m.Factors, f => f == 1000);
        Check(all, "population demand: on the anchor day every demand reads exactly a thousand, so every business keeps today's figures");
        bool online = true;
        for (int k = 0; k < m.OnlineFactors.Length; k++)
        {
            online &= m.OnlineFactors[k] == 1000;
        }
        Check(online, "population demand: so does every kind of online spending");
        DemandDay(sim, s, m, job, ++day);
        bool moved = false, sane = true;
        for (int i = 0; i < m.Factors.Length; i++)
        {
            moved |= m.Factors[i] != 1000;
            sane &= m.Factors[i] > 700 && m.Factors[i] < 1400;
        }
        Check(moved && sane, "population demand: the day after, the factors move with the people, by a little (" + string.Join("/", m.Factors) + ")");

        PopDemandState again = DemandMade(42, out PopulationSim sim2, out PopulationState s2);
        var job2 = new PopDemandJob();
        for (int d2 = s2.Day + 1; d2 <= day; d2++)
        {
            DemandDay(sim2, s2, again, job2, d2);
        }
        bool same = again.AnchorDay == m.AnchorDay;
        for (int i = 0; i < m.Now.Length; i++)
        {
            same &= again.Now[i] == m.Now[i] && again.Anchor[i] == m.Anchor[i];
        }
        for (int i = 0; i < m.Factors.Length; i++)
        {
            same &= again.Factors[i] == m.Factors[i];
        }
        for (int d = 0; d < m.Districts; d++)
        {
            same &= again.Crime[d] == m.Crime[d] && again.IncomeLean[d] == m.IncomeLean[d] && again.Adults[d] == m.Adults[d];
        }
        Check(same, "population demand: the same seed gives the same sources, anchor, factors and leans, to the number");

        // A city generated again (another day, or another seed) is anchored again from its own second month.
        s.Generated = s.Day;
        job.Begin(s, sim.Demand, sim.Pulls, m);
        job.RunAll();
        Check(m.AnchorDay < 0 && Array.TrueForAll(m.Factors, f => f == 1000) && m.Generated == s.Day,
            "population demand: a population generated again forgets the old anchor and keeps every business's own figures until it is settled");
    }

    // ---------------------------------------------------------------- the rules' edges

    private static void PopDemandRulesChecks()
    {
        Check(PopDemandRules.Band(10000, 0, 1000) == PopDemandRules.Good && PopDemandRules.Band(10000, 0, 900) == PopDemandRules.Fair && PopDemandRules.Band(10000, 13000, 1000) == PopDemandRules.Poor
              && PopDemandRules.Band(10000, 0, 400) == PopDemandRules.Poor && PopDemandRules.Band(0, 50, 0) == PopDemandRules.Poor && PopDemandRules.Band(0, 0, 0) == PopDemandRules.Fair
              && PopDemandRules.Band(10000, 4000, 1000) == PopDemandRules.Good && PopDemandRules.Band(10000, 2000, 700) == PopDemandRules.Fair
              && PopDemandRules.Band(10000, 36600, 1000) == PopDemandRules.Default && PopDemandRules.Band(10000, 36500, 1000) == PopDemandRules.Poor,
            "population demand: the credit bands: debt past a third of a year's income, or half the city's middle income or less, is poor; the middle income or over good; past a year's income in debt in default");
        int fine = PopDemandRules.Content(3000, 10000, 2000, 0, 0, 80, 1);
        int dear = PopDemandRules.Content(8000, 10000, 2000, 0, 0, 80, 1);
        int broke = PopDemandRules.Content(3000, 0, 0, 500, 2, 30, 4);
        Check(fine > dear && dear > broke && broke >= 0 && fine <= 1000 && PopDemandRules.Content(0, 100000, 1000000, 0, 0, 200, 1) <= 1000,
            "population demand: satisfaction falls with a dear rent, adults out of work, no savings and a crowded flat, within nought and a thousand (" + fine + ", " + dear + ", " + broke + ")");
        Check(PopDemandRules.CrimeIndex(100, 10, 5, 95, 10, 100, 1000, 100, 50, 950, 100, 1000) == 1000
              && PopDemandRules.CrimeIndex(50, 10, 10, 90, 20, 100, 1000, 100, 50, 950, 100, 1000) > 1000
              && PopDemandRules.CrimeIndex(500, 10, 1, 99, 5, 100, 1000, 100, 50, 950, 100, 1000) < 1000
              && PopDemandRules.CrimeIndex(0, 0, 0, 0, 0, 0, 1000, 100, 50, 950, 100, 1000) == 1000,
            "population demand: crime is the city's in an average district, more where income is low, work scarce and the young many, less where not, and the city's where nobody lives");
        Check(PopDemandRules.Lean(200, 10, 1000, 100) == 2000 && PopDemandRules.Lean(0, 0, 1000, 100) == 1000 && PopDemandRules.Lean(1, 10, 100000, 10) == PopDemandRules.LeanLeast
              && PopDemandRules.SavingLean(100, 1000, 100, 1000) == 1000 && PopDemandRules.SavingLean(-500, 1000, 100, 1000) < 1000,
            "population demand: a district's lean is its figure a household against the city's, within its bounds; a district that draws on its savings leans under the city");
        Check(PopDemandCatalogue.Ratio(150, 100) == 1500 && PopDemandCatalogue.Ratio(0, 100) == 0 && PopDemandCatalogue.Ratio(50, 0) == 1000 && PopDemandCatalogue.Ratio(long.MaxValue / 2, 3) == PopDemandCatalogue.FactorMost
              && PopDemandCatalogue.Ratio(999999999999L, 100000000000L) == 9999,
            "population demand: a figure against its anchor in thousandths, a thousand with no anchor, and never past ten times however large");
        var empty = new PopDemandState();
        Check(CityDemandNeutral(empty), "population demand: with no population every factor and lean is a thousand and every count none");
    }

    private static bool CityDemandNeutral(PopDemandState m)
    {
        PopDemandState was = CityDemand.Live;
        CityDemand.Live = null;
        bool ok = CityDemand.Factor(PopDemand.HotelGuests) == 1000 && CityDemand.Online("ba:businesstype_clothingstore") == 1000 && CityDemand.Crime("ba:neighborhood_midtown") == 1000
                  && CityDemand.Adults("ba:neighborhood_midtown") == -1 && CityDemand.Ask("ba:neighborhood_midtown", 0) == -1 && !CityDemand.Running;
        CityDemand.Live = m;
        ok &= CityDemand.Factor(PopDemand.HotelGuests) == 1000 && CityDemand.Adults("ba:neighborhood_midtown") == -1 && !CityDemand.Running;
        CityDemand.Live = was;
        return ok;
    }

    // ---------------------------------------------------------------- ten years: every factor sane

    private static void PopDemandYears()
    {
        PopDemandState m = DemandMade(1234, out PopulationSim sim, out PopulationState s);
        var job = new PopDemandJob();
        int days = s.YearDays * 10;
        _demandYears = new int[days][];
        var least = new int[PopDemandCatalogue.Demands];
        var most = new int[PopDemandCatalogue.Demands];
        for (int i = 0; i < least.Length; i++)
        {
            least[i] = int.MaxValue;
        }
        var clock = Stopwatch.StartNew();
        for (int n = 0; n < days; n++)
        {
            DemandDay(sim, s, m, job, s.Day + 1);
            _demandYears[n] = (int[])m.Factors.Clone();
            if (m.AnchorDay >= 0)
            {
                for (int i = 0; i < least.Length; i++)
                {
                    least[i] = Math.Min(least[i], m.Factors[i]);
                    most[i] = Math.Max(most[i], m.Factors[i]);
                }
            }
        }
        double ms = clock.Elapsed.TotalMilliseconds;
        _demandLast = m;
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  demand, ten years (" + days + " days) in " + (ms / 1000).ToString("F1", CultureInfo.InvariantCulture) + " s, anchored on day " + m.AnchorDay + ":");
            for (int i = 0; i < least.Length; i++)
            {
                Console.WriteLine("    " + (PopDemand)i + ": " + least[i] + " to " + most[i] + ", now " + m.Factors[i]);
            }
        }
        bool sane = true;
        string worst = string.Empty;
        for (int i = 0; i < least.Length; i++)
        {
            bool ok = least[i] >= 400 && most[i] <= 2500;
            if (!ok && worst.Length == 0)
            {
                worst = (PopDemand)i + " " + least[i] + " to " + most[i];
            }
            sane &= ok;
        }
        Check(m.AnchorDay > 0 && sane, "population demand: ten years without the player keep every business's demand between 0.4 and 2.5 times its first day" + (worst.Length > 0 ? " (" + worst + ")" : string.Empty));
        bool districts = true;
        for (int d = 0; d < m.Districts; d++)
        {
            districts &= m.Crime[d] >= PopDemandCatalogue.CrimeLeast && m.Crime[d] <= PopDemandCatalogue.CrimeMost && m.IncomeLean[d] >= PopDemandRules.LeanLeast && m.IncomeLean[d] <= PopDemandRules.LeanMost
                         && m.Ask[d * 3] + m.Ask[d * 3 + 1] + m.Ask[d * 3 + 2] >= 997 && m.Ask[d * 3] + m.Ask[d * 3 + 1] + m.Ask[d * 3 + 2] <= 1000;
        }
        Check(districts, "population demand: after ten years every district's crime and leans are within bounds, and its borrowers' bands sum to the whole");
    }

    // ---------------------------------------------------------------- the save's line

    private static void PopDemandSave()
    {
        PopDemandState m = _demandLast ?? DemandMade(9, out _, out _);
        string line = PopDemandStore.Write(m);
        var data = new Dictionary<string, string> { { PopDemandStore.Key, line } };
        PopDemandState back = PopDemandStore.Load(data, out int version);
        bool same = back != null && version == PopDemandState.Version && back.Seed == m.Seed && back.Generated == m.Generated && back.Day == m.Day && back.AnchorDay == m.AnchorDay
                    && back.Districts == m.Districts;
        for (int i = 0; same && i < m.Now.Length; i++)
        {
            same &= back.Now[i] == m.Now[i] && back.Anchor[i] == m.Anchor[i];
        }
        for (int i = 0; same && i < m.Factors.Length; i++)
        {
            same &= back.Factors[i] == m.Factors[i];
        }
        for (int k = 0; same && k < m.OnlineFactors.Length; k++)
        {
            same &= back.OnlineFactors[k] == m.OnlineFactors[k] && back.OnlineAnchor[k] == m.OnlineAnchor[k];
        }
        for (int d = 0; same && d < m.Districts; d++)
        {
            same &= back.Ids[d] == m.Ids[d] && back.Crime[d] == m.Crime[d] && back.IncomeLean[d] == m.IncomeLean[d] && back.SavingLean[d] == m.SavingLean[d]
                    && back.BorrowLean[d] == m.BorrowLean[d] && back.HousingLean[d] == m.HousingLean[d] && back.ConnectionFactor[d] == m.ConnectionFactor[d] && back.Adults[d] == m.Adults[d]
                    && back.AskLean[d * 3] == m.AskLean[d * 3];
        }
        Check(same, "population demand: the save's line carries the anchor, the last day's figures and every district, and a load has every factor and lean at once (" + line.Length + " characters)");
        var broken = new Dictionary<string, string> { { PopDemandStore.Key, "v=1~a:x|y~n:1|2" } };
        PopDemandState none = PopDemandStore.Load(broken, out _);
        Check(none != null && none.AnchorDay == -1 && Array.TrueForAll(none.Factors, f => f == 1000), "population demand: a damaged line reads as no anchor, and every business keeps its own figures");
        Check(PopDemandStore.Load(new Dictionary<string, string>(), out _) == null, "population demand: a save without the line has none, and the next day is measured afresh");
        var later = new Dictionary<string, string> { { PopDemandStore.Key, line.Replace("v=1~", "v=2~") + "~z:new|fields" } };
        PopDemandState newer = PopDemandStore.Load(later, out _);
        Check(newer == null || newer.Factors[(int)PopDemand.HotelGuests] == m.Factors[(int)PopDemand.HotelGuests], "population demand: a later version's line is kept as it was read, never misread");
    }

    // ---------------------------------------------------------------- Going Public: the host's figures, the guests' businesses

    private static void PopDemandShareChecks()
    {
        PopDemandState m = _demandLast ?? DemandMade(9, out _, out _);
        PopulationState host = PopulationMadeState(77);
        string part = PopulationShare.Write(host, m);
        var guest = new PopulationState();
        var mine = new PopDemandState();
        bool read = PopulationShare.Apply(guest, part, mine);
        bool same = read && mine.Guest && mine.Day == m.Day && mine.AnchorDay == m.AnchorDay && mine.Districts == m.Districts;
        for (int i = 0; same && i < m.Factors.Length; i++)
        {
            same &= mine.Factors[i] == m.Factors[i];
        }
        for (int k = 0; same && k < m.OnlineFactors.Length; k++)
        {
            same &= mine.OnlineFactors[k] == m.OnlineFactors[k];
        }
        for (int d = 0; same && d < m.Districts; d++)
        {
            same &= mine.Ids[d] == m.Ids[d] && mine.Crime[d] == m.Crime[d] && mine.IncomeLean[d] == m.IncomeLean[d] && mine.Adults[d] == m.Adults[d] && mine.ConnectionFactor[d] == m.ConnectionFactor[d]
                    && mine.Ask[d * 3] == m.Ask[d * 3] && mine.Ask[d * 3 + 2] == m.Ask[d * 3 + 2] && mine.AskLean[d * 3 + 1] == m.AskLean[d * 3 + 1];
        }
        Check(same, "population demand: a Going Public guest reads the host's factors and every district's leans from part P, so every player's businesses draw on the one city");
        var older = new PopulationState();
        Check(PopulationShare.Apply(older, part) && older.Day == host.Day, "population demand: a reader of part P from before the demand reads the part and skips its sections");
        string plain = PopulationShare.Write(host);
        var blank = new PopDemandState();
        Check(PopulationShare.Apply(new PopulationState(), plain, blank) && blank.Day == -1 && Array.TrueForAll(blank.Factors, f => f == 1000),
            "population demand: a host from before the demand sends none, and the guest's businesses keep their own figures");
        string later = part.Replace("~m:1|", "~m:2|") + "|5|6";
        var next = new PopDemandState();
        Check(PopulationShare.Apply(new PopulationState(), later, next) && next.Factors[0] == m.Factors[0], "population demand: a later version's sections read the fields this one knows");
    }

    private static PopulationState PopulationMadeState(int seed)
    {
        PopulationMade(seed, out PopulationState s, out _);
        return s;
    }

    // ---------------------------------------------------------------- the screens

    private static void PopDemandViewsChecks()
    {
        PopDemandState m = _demandLast ?? DemandMade(9, out _, out _);
        PopulationView all = PopDemandViews.Demands(m);
        bool rows = all.Rows.Count == PopulationViews.RowsMost && all.Hero.Count == 3 && all.Primary != null && all.PrimaryTarget.Valid;
        int last = int.MaxValue;
        foreach (ViewRow r in all.Rows)
        {
            int moved = (int)Math.Abs(r.Value - 1000);
            rows &= r.TitleKey && r.Target.Kind == ViewKind.Demand && r.Target.Valid && moved <= last;
            last = moved;
        }
        Check(rows, "population demand: the overview shows the eight business types whose demand moved most, the most first, each leading to its own screen");
        bool groups = true;
        for (int g = 0; g < PopDemandViews.Groups.Length; g++)
        {
            PopulationView one = PopDemandViews.Demand(m, g);
            groups &= one.Hero.Count == PopDemandViews.Groups[g].Demands.Length && one.Rows.Count > 0 && one.Details.Count > 0 && one.PrimaryTarget.Kind == ViewKind.Demands;
            for (int d = 0; d < PopDemandViews.Groups[g].Demands.Length; d++)
            {
                PopDemand dem = PopDemandViews.Groups[g].Demands[d];
                int sum = 0;
                (PopSource, int)[] recipe = PopDemandCatalogue.Recipe(dem);
                for (int r = 0; r < recipe.Length; r++)
                {
                    sum += PopDemandRules.Part(m, dem, r);
                }
                groups &= Math.Abs(sum - m.Factors[(int)dem]) <= recipe.Length;
            }
        }
        Check(groups, "population demand: each business type's screen shows its demands, and each source's part of a demand sums to that demand");
        var fresh = new PopDemandState();
        PopulationView none = PopDemandViews.Demands(fresh);
        Check(none.Rows.Count == 0 && none.Empty == "reallife_pop_dem_empty" && PopDemandViews.Demand(fresh, 99).Hero.Count == 0,
            "population demand: before the anchor the overview's list is empty and says why; a business type that is not there shows nothing");
    }

    // ---------------------------------------------------------------- the costs

    private static void PopDemandCosts()
    {
        PopulationSim sim = PopulationMade(5, out PopulationState s, out _);
        var m = new PopDemandState();
        var job = new PopDemandJob();
        DemandSlices(sim, s, m, job, out int slices, out double slowest, out double all);
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  demand: " + slices + " slices, " + all.ToString("F2", CultureInfo.InvariantCulture) + " ms in all, the slowest " + slowest.ToString("F2", CultureInfo.InvariantCulture) + " ms");
        }
        Check(slowest <= 2.0 && all <= 15.0, "population demand: a day's measure keeps to the population's slice (the slowest " + slowest.ToString("F2", CultureInfo.InvariantCulture) + " ms, "
                                             + all.ToString("F1", CultureInfo.InvariantCulture) + " ms in all, " + slices + " slices)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void DemandSlices(PopulationSim sim, PopulationState s, PopDemandState m, PopDemandJob job, out int slices, out double slowest, out double all)
    {
        job.Begin(s, sim.Demand, sim.Pulls, m);
        job.RunAll();   // warmed once, as the game's JIT will be after the first day
        job.Begin(s, sim.Demand, sim.Pulls, m);
        slices = 0;
        slowest = 0;
        all = 0;
        bool done = false;
        while (!done && slices < 10000)
        {
            long t0 = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(1.0, 1);
            done = job.Step(ref slice);
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            slowest = Math.Max(slowest, ms);
            all += ms;
            slices++;
        }
    }
}
