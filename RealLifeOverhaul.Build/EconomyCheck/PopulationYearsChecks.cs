using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;

// The population lived for ten years without the player (POPULATION.md section 4): the population stays in its band, births and deaths come at
// Manhattan's rates, households come from the region and leave for it, the labour market clears, pay and rents keep within their bounds, and every
// record still holds together; and the work's costs against the slice a frame can give it.
internal static partial class Program
{
    // The inputs of an ordinary day: the economy steady, the visitors at their ordinary number.
    private static readonly DemandInputs Ordinary = new DemandInputs { BaseBp = 0, Mood = 1000, Visitors = 1000 };

    private static void PopulationYears()
    {
        PopulationSim sim = PopulationMade(1234, out PopulationState s, out _);
        int startPeople = s.People.Live, startHouseholds = s.Households.Live;
        long births = 0, deaths = 0, inCity = 0, outCity = 0;
        int lowest = int.MaxValue, highest = 0, worstUnemployment = 0, bestUnemployment = 1000;
        int[] rentsFirst = (int[])s.RentIndex.Clone(), rentsTwo = rentsFirst, rentsEight = rentsFirst;
        var clock = Stopwatch.StartNew();
        int days = s.YearDays * 10;
        for (int day = 2; day <= days + 1; day++)
        {
            int month = s.MonthNumber;
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
            if (s.MonthNumber != month)
            {
                births += s.BirthsLast;
                deaths += s.DeathsLast;
                inCity += s.Region.MovedInLast;
                outCity += s.Region.MovedOutLast;
            }
            if (day % s.YearDays == 0)
            {
                lowest = Math.Min(lowest, s.People.Live);
                highest = Math.Max(highest, s.People.Live);
                int u = Unemployment(s);
                worstUnemployment = Math.Max(worstUnemployment, u);
                bestUnemployment = Math.Min(bestUnemployment, u);
                rentsTwo = day == s.YearDays * 2 ? (int[])s.RentIndex.Clone() : rentsTwo;
                rentsEight = day == s.YearDays * 8 ? (int[])s.RentIndex.Clone() : rentsEight;
            }
        }
        double ms = clock.Elapsed.TotalMilliseconds;
        int people = s.People.Live;
        double birthRate = births * 1000.0 / 10.0 / Math.Max(1, (startPeople + people) / 2);
        double deathRate = deaths * 1000.0 / 10.0 / Math.Max(1, (startPeople + people) / 2);
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  ten years (" + days + " days) in " + (ms / 1000).ToString("F1", CultureInfo.InvariantCulture) + " s: people " + startPeople + " to " + people + " (" + lowest + " to " + highest
                              + "), households " + startHouseholds + " to " + s.Households.Live + "; " + birthRate.ToString("F1", CultureInfo.InvariantCulture) + " births and "
                              + deathRate.ToString("F1", CultureInfo.InvariantCulture) + " deaths a thousand a year; " + inCity + " households came and " + outCity + " left; unemployment "
                              + bestUnemployment / 10.0 + " to " + worstUnemployment / 10.0 + "%; rents " + string.Join("/", s.RentIndex) + "; pay " + string.Join("/", s.FieldWage));
        }
        Check(people >= startPeople * 85 / 100 && people <= startPeople * 120 / 100 && lowest >= startPeople * 80 / 100,
            "population years: ten years without the player keep the population in its band (" + startPeople + " to " + people + ", at least " + lowest + ")");
        Check(birthRate >= 6 && birthRate <= 16, "population years: births at Manhattan's rate, about eleven a thousand a year (" + birthRate.ToString("F1", CultureInfo.InvariantCulture) + ")");
        Check(deathRate >= 4 && deathRate <= 12, "population years: deaths by age, about seven a thousand a year (" + deathRate.ToString("F1", CultureInfo.InvariantCulture) + ")");
        Check(inCity > 0 && outCity > 0, "population years: households come from the region and leave for it (" + inCity + " came, " + outCity + " left)");
        Check(worstUnemployment <= 120 && bestUnemployment >= 15,
            "population years: the labour market clears, unemployment between 1.5 and 12% every year (" + bestUnemployment / 10.0 + " to " + worstUnemployment / 10.0 + "%)");
        bool wages = true;
        for (int f = 1; f < s.FieldWage.Length; f++)
        {
            wages &= s.FieldWage[f] >= LabourRules.WageLeast && s.FieldWage[f] <= LabourRules.WageMost;
        }
        Check(wages, "population years: every field's pay stays within its bounds as it follows how hard its people are to find");
        bool rents = true;
        for (int d = 0; d < s.RentIndex.Length; d++)
        {
            rents &= s.RentIndex[d] >= PopulationCatalogue.RentLeast && s.RentIndex[d] <= PopulationCatalogue.RentMost;
        }
        Check(rents, "population years: every district's rents stay within their bounds as they follow its vacancy");
        // Three hundredths over two years is under a point of vacancy at the market's pace: a district still drifting, not one still climbing.
        bool rentsSettle = true;
        for (int d = 0; d < s.RentIndex.Length && d < rentsFirst.Length; d++)
        {
            rentsSettle &= s.RentIndex[d] - rentsEight[d] <= Math.Max(30, rentsTwo[d] - rentsFirst[d]);
        }
        Check(rentsSettle, "population years: a district's rents rise while its flats are short and settle as people come and go: none climbs in the last two years by more than it did in the first two ("
                           + string.Join("/", rentsTwo) + " after two years, " + string.Join("/", rentsEight) + " after eight, " + string.Join("/", s.RentIndex) + " after ten)");
        int usual = PopulationCatalogue.UnemploymentStart;
        Check(LabourRules.WageTarget(usual) == 1000 && LabourRules.WageTarget(usual * 2) == 1000 - LabourRules.WageCurve && LabourRules.WageTarget(0) == 1000 + LabourRules.WageCurve
              && LabourRules.WageTarget(1000) == LabourRules.WageTargetLeast && LabourRules.WageTarget(-50) <= LabourRules.WageTargetMost && LabourRules.WageTargetLeast > LabourRules.WageLeast
              && LabourRules.WageTarget(usual * 3) < LabourRules.WageTarget(usual * 2),
            "population years: the wage curve: a field's pay is drawn to its start at the city's usual unemployment, about 7% lower at twice it and as much higher at none, never to the floor that guards it");
        var market = new PopulationState();
        market.RentIndex = new[] { 1000, 1500, 700 };
        Check(HousingRules.RegionPull(market, 0) == 1000 && HousingRules.RegionPull(market, 1) < 500 && HousingRules.RegionPull(market, 2) == PopulationCatalogue.RegionPullMost
              && HousingRules.WantLeave(market, 0) == PopulationCatalogue.WantLeave && HousingRules.WantLeave(market, 1) > PopulationCatalogue.WantLeave * 2 && HousingRules.WantLeave(market, 2) < PopulationCatalogue.WantLeave,
            "population years: the region's households come at a district's own rents and fewer past them, more of those who find no flat leave where rents have climbed, fewer where they fell");
        bool linked = true;
        int members = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (!s.Households.Alive(h))
            {
                continue;
            }
            int count = 0;
            for (int p = s.Households.First[h]; p >= 0 && count < 100; p = s.People.Next[p])
            {
                linked &= s.People.Household[p] == h;
                count++;
            }
            linked &= count == s.Households.Size[h] && count > 0;
            members += count;
            int b = s.Households.Building[h];
            linked &= b >= 0 && s.Occupant[s.FlatFirst[b] + s.Households.Unit[h]] == h;
        }
        Check(linked && members == s.People.Live, "population years: after ten years every household has its people and its flat, and every person a household (" + members + ")");
        int filled = 0;
        var counted = new int[s.Employers.Count];
        for (int p = 0; p < s.People.Count; p++)
        {
            if (s.People.Id[p] != 0 && s.People.Job[p] >= 0)
            {
                counted[s.People.Job[p]]++;
            }
        }
        bool jobs = true;
        for (int e = 0; e < s.Employers.Count; e++)
        {
            jobs &= counted[e] == s.Employers.Filled[e] && s.Employers.Filled[e] + s.Employers.Commuters[e] <= s.Employers.Slots[e];
            filled += counted[e];
        }
        Check(jobs, "population years: every employer's people counted right, and never more than its jobs (" + filled + " residents in work)");
    }

    // Unemployment now, thousandths of the labour force.
    private static int Unemployment(PopulationState s)
    {
        int workers = 0, unemployed = 0;
        for (int p = 0; p < s.People.Count; p++)
        {
            if (s.People.Id[p] == 0)
            {
                continue;
            }
            workers += s.People.Status[p] == (byte)Status.Worker ? 1 : 0;
            unemployed += s.People.Status[p] == (byte)Status.Unemployed ? 1 : 0;
        }
        return unemployed * 1000 / Math.Max(1, workers + unemployed);
    }

    // ---------------------------------------------------------------- the costs

    private static void PopulationCosts()
    {
        PopulationState s = PopulationCity(out List<BusinessSeed> seeds);
        var sim = new PopulationSim();
        sim.BeginGenerate(s, 99, 1, 60, seeds);
        double worstGenerate = SlicedWorst(sim, 3, out int generateSteps, out double generateMs);
        double worstDay = 0, dayMs = 0;
        int daySteps = 0;
        for (int day = 2; day <= 4; day++)
        {
            sim.BeginDay(day, Ordinary, seeds);
            worstDay = Math.Max(worstDay, SlicedWorst(sim, 0, out int steps, out double ms));
            daySteps += steps;
            dayMs = Math.Max(dayMs, ms);
        }
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  generation: " + generateSteps + " slices, " + generateMs.ToString("F1", CultureInfo.InvariantCulture) + " ms in all, the slowest "
                              + worstGenerate.ToString("F2", CultureInfo.InvariantCulture) + " ms; a day: " + daySteps / 3 + " slices, " + dayMs.ToString("F1", CultureInfo.InvariantCulture)
                              + " ms in all, the slowest " + worstDay.ToString("F2", CultureInfo.InvariantCulture) + " ms");
        }
        Check(worstDay <= 4.0, "population costs: no slice of a day's turn over 4 ms here, the 2 ms budget in the game's own slice with a margin for this machine (" + worstDay.ToString("F2", CultureInfo.InvariantCulture) + " ms)");
        Check(dayMs <= 400.0, "population costs: a day's whole turn under 400 ms of work spread over its frames (" + dayMs.ToString("F1", CultureInfo.InvariantCulture) + " ms)");
        Check(worstGenerate <= 6.0, "population costs: no slice of the generation over 6 ms here (" + worstGenerate.ToString("F2", CultureInfo.InvariantCulture) + " ms)");
    }

    // A job run a slice of a millisecond at a time: its slowest slice (the first few of the first job, which warm the runtime up, left out), how many and the time in all.
    private static double SlicedWorst(PopulationSim sim, int warm, out int steps, out double total)
    {
        steps = 0;
        total = 0;
        double worst = 0;
        bool done = false;
        while (!done)
        {
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(1.0, 1);
            done = sim.Step(ref slice);
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            worst = steps >= warm ? Math.Max(worst, ms) : worst;
            total += ms;
            steps++;
        }
        return worst;
    }
}
