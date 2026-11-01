using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

// The living economy's features together, offline: each feature's own checks run it on a documented city, so a drift that only the features
// together make shows nowhere else. The population lives its years on the one clock (CalendarRules.MoneyYearDays's note: the overhaul's years
// are the save's), and at each of them the figures the other features read from it are taken as the wiring hands them over: the tech markets'
// city (Wiring.SectorLinks.City: people, firms by size, the households' digital spending against the first day's) and the studio's audience
// (Wiring.PopulationLinks.Audience: the residents who play, against the start, times the world). The studio's and the other tech firms' money
// years (3,650 game days) then run on that living city, its last year held past the population's own, beside the documented city. Every band
// is asserted over ten of the save's years: people and households, births and deaths, unemployment, rents (none at its bound), pay (none at its
// floor), every business's demand, digital spending, the residents who play, every product's payback on both cities, and the documented city
// standing for the living one on its first day.
//
// "--living-years" lives 3,650 days at the game's default year of 60 days (61 of its years: a long game, held to the long run's bands too) and
// at 365, with the businesses' demand. The full run holds a short version at no cost of its own (LivingShort, on the demand's ten years) and the
// one clock (LivingClock: a year of 365 days against the default's first).
internal static partial class Program
{
    private const int LivingSeed = 1234;         // the city PopulationYears and the demand's ten years live, so the runs can be read together
    private const int LivingMoneyDays = 3650;    // the money scale's horizon: a payback and a year's profit are money, by the game day

    // The bands of ten of the save's years (the balance brief, 1 Nov 2026: the people moving, inside realistic bands, at any year length). A city of
    // fixed homes whose generated households are larger than Manhattan's low fertility keeps: its people drift a few per cent as its children grow up.
    private const int PeopleBand = 100;                                // thousandths either way of the first day's people, at every year's end
    private const int HouseholdsBand = 50;                             // ...of its households
    private const int UnemploymentLeast = 20, UnemploymentMost = 100;  // thousandths of the labour force
    private const int BirthsLeast = 6, BirthsMost = 16, DeathsLeast = 4, DeathsMost = 12;   // a thousand people a year of the save (Manhattan: about 11 and 7)
    private const int RentBandLeast = 800, RentBandMost = 1600;        // thousandths of the game's own rent, each district at every year's end: a cheap district
                                                                       // gentrifying by half in ten years, never to the index's bound
    private const int RentCityLeast = 900, RentCityMost = 1300;        // ...the city's flats on average (New York's real rents: a fifth or so in ten years)
    private const int DigitalBandLeast = 850, DigitalBandMost = 1400;  // the households' digital spending a person against the first day's (incomes' growth)
    private const int PlayersBandLeast = 900, PlayersBandMost = 1100;  // residents who play against the start: the city's ages move slowly
    private const int DemandBandLeast = 600, DemandBandMost = 1700;    // every business's demand against its anchor day, after it (a landlord's applicants rise by
                                                                       // up to two thirds while a cheap district's rents climb and its flats fill)...
    private const int SavingsBandLeast = 500, SavingsBandMost = 2000;  // ...but the savings pools, a stock that grows with what households put by and its interest:
                                                                       // the own bank's own band (PopDemandBusinessChecks), half to twice their first day
    private const int ReferenceBand = 20;                              // thousandths: the documented city stands for the living one's first day within this

    private sealed class LivingYear
    {
        public int Day, People, Households, Unemployment, Small, Mid, Large, Digital, RentLeast = int.MaxValue, RentMost, RentTopped, RentMean, WageLeast = int.MaxValue, WageMost, WageFloored;
        public int Births, Deaths;   // in the year that ended here
        public long Players, PlayersStart;
        public int[] Games = new int[GameRules.Cells];
        public int[] Rent;
        public readonly SectorCityFigures City = new SectorCityFigures();
    }

    // A city's years lived: a snapshot at the start and at the end of each of the save's years; the businesses' demand's least and most over its
    // first ten years after the anchor (when lived with it).
    private sealed class LivingRun
    {
        public int YearDays;
        public readonly List<LivingYear> Years = new List<LivingYear>(64);
        public int[] GameStart = new int[GameRules.Cells];
        public readonly int[] DemandLeast = new int[PopDemandCatalogue.Demands], DemandMost = new int[PopDemandCatalogue.Demands];
        public bool Demand;
        public long DigitalAnchor;
        public int Births, Deaths;   // the running year's

        public LivingRun()
        {
            for (int i = 0; i < DemandLeast.Length; i++)
            {
                DemandLeast[i] = int.MaxValue;
            }
        }

        public LivingYear YearOf(int day) => Years[Math.Max(0, Math.Min(Years.Count - 1, (day - 1) / Math.Max(1, YearDays)))];
    }

    // ---------------------------------------------------------------- the full mode

    private static void LivingYears()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (int year in new[] { CalendarRules.YearDefault, 365 })
        {
            LivingRun run = LivingLive(year, LivingMoneyDays);
            string label = " (years of " + year + " days)";
            LivingReport(run, label);
            LivingBands(run, 10, label);
            if (LivingMoneyDays / year > 10)
            {
                LivingLongRun(run, label);
            }
            LivingReference(run, label);
            LivingMoney(run, label, true);
        }
        Console.WriteLine("  living years took " + clock.ElapsedMilliseconds + " ms");
    }

    // A city generated at a year's length and lived so many days with the businesses' demand, a snapshot at each year's end.
    private static LivingRun LivingLive(int yearDays, int days)
    {
        PopulationSim sim = PopulationMade(LivingSeed, out PopulationState s, out _, yearDays);
        var m = new PopDemandState();
        var job = new PopDemandJob();
        job.Begin(s, sim.Demand, sim.Pulls, m);
        job.RunAll();
        var run = new LivingRun { YearDays = yearDays, Demand = true };
        LivingStart(run, sim, s);
        for (int day = 2; day <= days + 1; day++)
        {
            int month = s.MonthNumber;
            DemandDay(sim, s, m, job, day);
            LivingDay(run, sim, s, m, month, day);
        }
        return run;
    }

    // The first day's snapshot and the start the others are read against.
    private static void LivingStart(LivingRun run, PopulationSim sim, PopulationState s)
    {
        run.Years.Add(LivingSnapshot(run, sim, s, s.Day));
        Array.Copy(s.GameStart, run.GameStart, Math.Min(run.GameStart.Length, s.GameStart.Length));
    }

    // A day lived: the month's births and deaths counted, the demand's figures in their least and most for the first ten years after the anchor,
    // and a snapshot at a year's end.
    private static void LivingDay(LivingRun run, PopulationSim sim, PopulationState s, PopDemandState m, int month, int day)
    {
        if (s.MonthNumber != month)
        {
            run.Births += s.BirthsLast;
            run.Deaths += s.DeathsLast;
        }
        if (m != null && m.AnchorDay >= 0 && day <= run.YearDays * 10 + 1)
        {
            for (int i = 0; i < run.DemandLeast.Length && i < m.Factors.Length; i++)
            {
                run.DemandLeast[i] = Math.Min(run.DemandLeast[i], m.Factors[i]);
                run.DemandMost[i] = Math.Max(run.DemandMost[i], m.Factors[i]);
            }
        }
        if ((day - 1) % run.YearDays == 0)
        {
            run.Years.Add(LivingSnapshot(run, sim, s, day));
        }
    }

    // A year's figures, as the wiring reads them from the population for the tech markets and the studio.
    private static LivingYear LivingSnapshot(LivingRun run, PopulationSim sim, PopulationState s, int day)
    {
        var y = new LivingYear { Day = day, People = s.People.Live, Households = s.Households.Live, Unemployment = Unemployment(s), Births = run.Births, Deaths = run.Deaths };
        run.Births = run.Deaths = 0;
        SectorRules.FirmsOf(s.Employers.Kind, s.Employers.Slots, s.Employers.Count, out y.Small, out y.Mid, out y.Large);
        long digital = 0;
        PoolRules.DemandDay dd = sim.Demand;
        for (int h = 0; dd != null && dd.Day >= 0 && h < s.Households.Count && (h + 1) * PopulationCatalogue.Categories <= dd.Budget.Length; h++)
        {
            digital += s.Households.Alive(h) ? dd.Budget[h * PopulationCatalogue.Categories + (int)Spend.Digital] : 0;
        }
        if (run.DigitalAnchor <= 0)
        {
            run.DigitalAnchor = SectorRules.DigitalPerPerson(digital, y.People);
        }
        y.Digital = SectorRules.Digital(digital, y.People, run.DigitalAnchor);
        y.City.Set(y.People, y.Small, y.Mid, y.Large, y.Digital, y.People > 0);
        Array.Copy(s.GameCounts, y.Games, Math.Min(y.Games.Length, s.GameCounts.Length));
        for (int c = 0; c < s.GameCounts.Length && c < s.GameStart.Length; c++)
        {
            y.Players += s.GameCounts[c];
            y.PlayersStart += s.GameStart[c];
        }
        y.Rent = (int[])s.RentIndex.Clone();
        long flats = 0, weighted = 0;
        for (int d = 0; d < y.Rent.Length; d++)
        {
            if (d < s.Districts.Count && s.Districts[d].Villas)
            {
                continue;   // villas have no rental market
            }
            int f = d < s.Figures.Length ? s.Figures[d].Flats : 0;
            flats += f;
            weighted += (long)f * y.Rent[d];
            y.RentLeast = Math.Min(y.RentLeast, y.Rent[d]);
            y.RentMost = Math.Max(y.RentMost, y.Rent[d]);
            y.RentTopped += y.Rent[d] >= PopulationCatalogue.RentMost || y.Rent[d] <= PopulationCatalogue.RentLeast ? 1 : 0;
        }
        y.RentMean = flats > 0 ? (int)(weighted / flats) : 1000;
        for (int f = 1; f < s.FieldWage.Length; f++)
        {
            y.WageLeast = Math.Min(y.WageLeast, s.FieldWage[f]);
            y.WageMost = Math.Max(y.WageMost, s.FieldWage[f]);
            y.WageFloored += s.FieldWage[f] <= LabourRules.WageLeast ? 1 : 0;
        }
        return y;
    }

    // ---------------------------------------------------------------- the bands

    // The bands over a run's first so many years of the save.
    private static void LivingBands(LivingRun run, int years, string label)
    {
        LivingYear first = run.Years[0];
        int count = Math.Min(run.Years.Count - 1, years);
        bool people = true, households = true, unemployment = true, rents = true, city = true, wages = true, digital = true, players = true, births = true;
        string where = string.Empty;
        for (int i = 1; i <= count; i++)
        {
            LivingYear y = run.Years[i];
            people &= Within(y.People, first.People, PeopleBand);
            households &= Within(y.Households, first.Households, HouseholdsBand);
            unemployment &= y.Unemployment >= UnemploymentLeast && y.Unemployment <= UnemploymentMost;
            bool rentOk = y.RentTopped == 0 && y.RentLeast >= RentBandLeast && y.RentMost <= RentBandMost;
            if (!rentOk && where.Length == 0)
            {
                where = " (year " + i + ": " + y.RentLeast + " to " + y.RentMost + ", " + y.RentTopped + " at a bound)";
            }
            rents &= rentOk;
            city &= y.RentMean >= RentCityLeast && y.RentMean <= RentCityMost;
            wages &= y.WageFloored == 0 && y.WageLeast >= LabourRules.WageTargetLeast - 20 && y.WageMost <= LabourRules.WageTargetMost;
            digital &= y.Digital >= DigitalBandLeast && y.Digital <= DigitalBandMost;
            int share = y.PlayersStart > 0 ? (int)(y.Players * 1000 / y.PlayersStart) : 1000;
            players &= share >= PlayersBandLeast && share <= PlayersBandMost;
            int births1000 = y.Births * 1000 / Math.Max(1, y.People), deaths1000 = y.Deaths * 1000 / Math.Max(1, y.People);
            births &= births1000 >= BirthsLeast && births1000 <= BirthsMost && deaths1000 >= DeathsLeast && deaths1000 <= DeathsMost;
        }
        LivingYear last = run.Years[count];
        Check(people && households, "living years" + label + ": in ten years the people stay within a tenth of the first day's and the households within a twentieth ("
                                    + first.People + " to " + last.People + ", " + first.Households + " to " + last.Households + ")");
        Check(births, "living years" + label + ": births and deaths come at Manhattan's rates every year of the save (" + last.Births + " and " + last.Deaths + " in the last)");
        Check(unemployment, "living years" + label + ": unemployment between " + UnemploymentLeast / 10.0 + " and " + UnemploymentMost / 10.0 + "% every year (" + last.Unemployment / 10.0 + "% in the last)");
        Check(rents && city, "living years" + label + ": every district's rents within " + RentBandLeast + " to " + RentBandMost + " of the game's and none at its bound every year, the city's on average within "
                             + RentCityLeast + " to " + RentCityMost + where + " (the last year " + last.RentLeast + " to " + last.RentMost + ", on average " + last.RentMean + ")");
        Check(wages, "living years" + label + ": every field's pay on its wage curve every year, none at its floor (" + last.WageLeast + " to " + last.WageMost + " in the last)");
        Check(digital, "living years" + label + ": the households' digital spending a person within " + DigitalBandLeast + " to " + DigitalBandMost + " of the first day's every year (" + last.Digital + " in the last)");
        Check(players, "living years" + label + ": the residents who play within " + PlayersBandLeast + " to " + PlayersBandMost + " of the start every year (" + (last.PlayersStart > 0 ? last.Players * 1000 / last.PlayersStart : 0)
                       + " in the last)");
        if (run.Demand)
        {
            bool sane = true;
            string worst = string.Empty;
            for (int i = 0; i < run.DemandLeast.Length; i++)
            {
                bool savings = i == (int)PopDemand.SaversPeople || i == (int)PopDemand.SaversFirms;
                bool ok = run.DemandLeast[i] >= (savings ? SavingsBandLeast : DemandBandLeast) && run.DemandMost[i] <= (savings ? SavingsBandMost : DemandBandMost);
                if (!ok && worst.Length == 0)
                {
                    worst = " (" + (PopDemand)i + " " + run.DemandLeast[i] + " to " + run.DemandMost[i] + ")";
                }
                sane &= ok;
            }
            Check(sane, "living years" + label + ": every business's demand within " + DemandBandLeast + " to " + DemandBandMost + " of its anchor day through ten years, the savings pools within "
                        + SavingsBandLeast + " to " + SavingsBandMost + worst + " (tenants " + run.DemandLeast[(int)PopDemand.Tenants] + " to " + run.DemandMost[(int)PopDemand.Tenants] + ", the people's savings "
                        + run.DemandLeast[(int)PopDemand.SaversPeople] + " to " + run.DemandMost[(int)PopDemand.SaversPeople] + ")");
        }
    }

    private static bool Within(long value, long start, int band) => start > 0 && Math.Abs(value - start) * 1000 <= start * band;

    // A long game in a short year (61 of its years in the money's ten): the people, rents, pay and digital spending stay in their bounds, never resting
    // on one.
    private static void LivingLongRun(LivingRun run, string label)
    {
        LivingYear first = run.Years[0];
        bool ok = true;
        int lowest = int.MaxValue, highest = 0, rentMost = 0, wageLeast = int.MaxValue, digitalMost = 0;
        for (int i = 1; i < run.Years.Count; i++)
        {
            LivingYear y = run.Years[i];
            ok &= Within(y.People, first.People, 150) && y.RentTopped == 0 && y.WageFloored == 0 && y.Digital < SectorCatalogue.DigitalMost && y.Digital > SectorCatalogue.DigitalLeast;
            lowest = Math.Min(lowest, y.People);
            highest = Math.Max(highest, y.People);
            rentMost = Math.Max(rentMost, y.RentMost);
            wageLeast = Math.Min(wageLeast, y.WageLeast);
            digitalMost = Math.Max(digitalMost, y.Digital);
        }
        Check(ok, "living years" + label + ": through " + (run.Years.Count - 1) + " of the save's years the people stay within a seventh of the first day's (" + lowest + " to " + highest
                  + "), no district's rents and no field's pay at a bound (rents to " + rentMost + ", pay down to " + wageLeast + "), digital spending inside its bounds (to " + digitalMost + ")");
    }

    // The documented city stands for the living one on its first day: so a market is the same size whether the population runs or not.
    private static void LivingReference(LivingRun run, string label)
    {
        LivingYear first = run.Years[0];
        Check(Within(first.People, SectorCatalogue.ReferencePeople, ReferenceBand) && Within(first.Small, SectorCatalogue.ReferenceSmall, ReferenceBand)
              && Within(first.Mid, SectorCatalogue.ReferenceMid, ReferenceBand) && first.Large == SectorCatalogue.ReferenceLarge,
            "living years" + label + ": the documented city is the living city's first day (" + first.People + " people and " + first.Small + "/" + first.Mid + "/" + first.Large + " firms against "
            + SectorCatalogue.ReferencePeople + " and " + SectorCatalogue.ReferenceSmall + "/" + SectorCatalogue.ReferenceMid + "/" + SectorCatalogue.ReferenceLarge + ")");
    }

    // ---------------------------------------------------------------- the money on the living city

    // Each kind's money years on the living city and the documented one in the run's calendar, each in its band on both.
    private static void LivingMoney(LivingRun run, string label, bool print)
    {
        int year = run.YearDays, days = LivingMoneyDays;
        int[] least = ScaleCatalogue.PaybackLeast, most = ScaleCatalogue.PaybackMost;
        Action<SectorFile, int> living = (f, day) => f.City.CopyFrom(run.YearOf(day).City);
        LivingSector("SaaS for businesses", PaybackKind.Software, SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, days, 11, 1, 1, living, year),
            SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, days, 11, 1, 1, null, year), label, print);
        LivingSector("SaaS for enterprises", PaybackKind.Software, SectorProductSim(ProductKind.Saas, Segment.Enterprise, 5, 1, 2, 2, 1, 2, days, 12, 1, 1, living, year),
            SectorProductSim(ProductKind.Saas, Segment.Enterprise, 5, 1, 2, 2, 1, 2, days, 12, 1, 1, null, year), label, print);
        LivingSector("consumer app", PaybackKind.Software, SectorProductSim(ProductKind.App, Segment.Consumer, 4, 1, 1, 0, 1, 3, days, 13, 1, 1, living, year),
            SectorProductSim(ProductKind.App, Segment.Consumer, 4, 1, 1, 0, 1, 3, days, 13, 1, 1, null, year), label, print);
        LivingSector("AI model", PaybackKind.AiLab, SectorProductSim(ProductKind.Model, Segment.Enterprise, 4, 0, 1, 1, 1, 2, days, 15, 1, 1, living, year),
            SectorProductSim(ProductKind.Model, Segment.Enterprise, 4, 0, 1, 1, 1, 2, days, 15, 1, 1, null, year), label, print);
        LivingSector("consultancy", PaybackKind.Services, SectorServicesSim(FirmKind.Consultancy, 4, 1, days, 16, false, living, year),
            SectorServicesSim(FirmKind.Consultancy, 4, 1, days, 16, false, null, year), label, print);
        LivingSector("cybersecurity", PaybackKind.Services, SectorServicesSim(FirmKind.Security, 4, 1, days, 17, false, living, year),
            SectorServicesSim(FirmKind.Security, 4, 1, days, 17, false, null, year), label, print);
        LivingSector("design studio", PaybackKind.Services, SectorServicesSim(FirmKind.Design, 3, 1, days, 18, false, living, year),
            SectorServicesSim(FirmKind.Design, 3, 1, days, 18, false, null, year), label, print);
        // The studio's ten years on the living city's audience, beside the documented world's.
        StudioBridges.AudienceAsk was = StudioBridges.Population, alive = LivingAudience(run);
        try
        {
            for (int sc = 0; sc < (int)Scope.Count; sc++)
            {
                StudioBridges.Population = alive;
                LivingStudio((Scope)sc, year, out int livingRate, out long livingDay);
                StudioBridges.Population = null;
                LivingStudio((Scope)sc, year, out int worldRate, out long worldDay);
                int livingPayback = livingRate > 0 ? 1000000 / livingRate : -1, worldPayback = worldRate > 0 ? 1000000 / worldRate : -1;
                if (print)
                {
                    Console.WriteLine("  studio " + (Scope)sc + label + ": " + livingRate + " ppm a day on the living city (payback " + livingPayback + " days, $" + livingDay.ToString("N0", CultureInfo.InvariantCulture)
                                      + " a day) against " + worldRate + " on the documented world (payback " + worldPayback + " days, $" + worldDay.ToString("N0", CultureInfo.InvariantCulture) + ")");
                }
                Check(ScaleRules.OnTarget(PaybackKind.GameStudio, livingPayback) && ScaleRules.OnTarget(PaybackKind.GameStudio, worldPayback),
                    "living years" + label + ": a " + (Scope)sc + " studio's ten years pay back inside the studio's " + least[(int)PaybackKind.GameStudio] + " to " + most[(int)PaybackKind.GameStudio]
                    + " days on the living city's audience (" + livingPayback + ") and the documented world's (" + worldPayback + ")");
            }
        }
        finally
        {
            StudioBridges.Population = was;
        }
    }

    // The studio's audience from the living city's years: the residents who play in that year's snapshot, against the start, times the world.
    private static StudioBridges.AudienceAsk LivingAudience(LivingRun run)
    {
        return (int genre, int platform, int day, int year, out long residents) =>
        {
            LivingYear y = run.YearOf(Math.Max(1, day));
            int cell = genre * GameRules.Platforms + platform;
            residents = cell >= 0 && cell < y.Games.Length ? y.Games[cell] : 0;
            return cell >= 0 && cell < run.GameStart.Length ? GameRules.Audience(residents, run.GameStart[cell], StudioBridges.World(genre, platform, day, year)) : -1;
        };
    }

    // The studio's ten years at a scope over the seeds StudioTenYears uses: the mean return a day, millionths of capital, and the profit a day in whole money.
    private static void LivingStudio(Scope scope, int year, out int rate, out long day)
    {
        const int Seeds = 16;
        long sum = 0;
        day = 0;
        for (int seed = 1; seed <= Seeds; seed++)
        {
            StudioTen r = TenYears(scope, seed * 7919 + (int)scope, year);
            sum += r.RatePpm;
            day += r.Cash / ScaleCatalogue.SimDays / 100;
        }
        rate = (int)(sum / Seeds);
        day /= Seeds;
    }

    // A kind's run on the living city beside the documented one: each payback in its kind's band, and its last year paying its way.
    private static void LivingSector(string what, PaybackKind kind, SectorRun living, SectorRun documented, string label, bool print)
    {
        if (print)
        {
            Console.WriteLine("  " + what + label + ": payback day " + living.Payback + ", $" + living.Day.ToString("N0", CultureInfo.InvariantCulture) + " a day in the last year, "
                              + living.Customers.ToString("N0", CultureInfo.InvariantCulture) + " of " + living.Buyers.ToString("N0", CultureInfo.InvariantCulture) + " buyers on the living city; payback day "
                              + documented.Payback + ", $" + documented.Day.ToString("N0", CultureInfo.InvariantCulture) + " a day, " + documented.Customers.ToString("N0", CultureInfo.InvariantCulture) + " of "
                              + documented.Buyers.ToString("N0", CultureInfo.InvariantCulture) + " on the documented one");
        }
        Check(ScaleRules.OnTarget(kind, living.Payback) && ScaleRules.OnTarget(kind, documented.Payback) && living.Day > 0 && documented.Day > 0,
            "living years" + label + ": " + what + " pays back inside its " + ScaleCatalogue.PaybackLeast[(int)kind] + " to " + ScaleCatalogue.PaybackMost[(int)kind] + " days on the living city ("
            + living.Payback + ", $" + living.Day + " a day in its last year) and the documented one (" + documented.Payback + ", $" + documented.Day + ")");
    }

    // ---------------------------------------------------------------- the report

    private static void LivingReport(LivingRun run, string label)
    {
        Console.WriteLine("  the population" + label + " through " + (run.Years.Count - 1) + " of its years (" + (run.Years.Count - 1) * run.YearDays + " days):");
        Console.WriteLine("  year  people  households  unemployed  births/deaths  small/mid/large firms  digital  players/start  rents least..most (bound) mean  pay least..most");
        for (int i = 0; i < run.Years.Count; i++)
        {
            LivingYear y = run.Years[i];
            if (i > 12 && i % 10 != 0 && i != run.Years.Count - 1)
            {
                continue;   // a long run's every tenth year after the first twelve
            }
            Console.WriteLine("  " + i.ToString().PadLeft(4) + "  " + y.People.ToString().PadLeft(6) + "  " + y.Households.ToString().PadLeft(10) + "  " + (y.Unemployment / 10.0).ToString("F1", CultureInfo.InvariantCulture).PadLeft(9) + "%"
                              + "  " + (y.Births + "/" + y.Deaths).PadLeft(13) + "  " + (y.Small + "/" + y.Mid + "/" + y.Large).PadLeft(21) + "  " + y.Digital.ToString().PadLeft(7)
                              + "  " + (y.PlayersStart > 0 ? (y.Players * 1000 / y.PlayersStart).ToString() : "-").PadLeft(13)
                              + "  " + (y.RentLeast + ".." + y.RentMost + " (" + y.RentTopped + ") " + y.RentMean).PadLeft(30) + "  " + y.WageLeast + ".." + y.WageMost);
        }
        if (run.Demand)
        {
            var sb = new System.Text.StringBuilder("  demand through ten years, least..most:");
            for (int i = 0; i < run.DemandLeast.Length; i++)
            {
                sb.Append(' ').Append((PopDemand)i).Append(' ').Append(run.DemandLeast[i]).Append("..").Append(run.DemandMost[i]).Append(';');
            }
            Console.WriteLine(sb.ToString());
        }
    }

    // ---------------------------------------------------------------- the full run's short version and the one clock

    // The demand's ten years (PopDemandYears) read as the living years: the bands, the documented city on the first day, the money on the living city.
    private static void LivingShort(LivingRun run)
    {
        string label = " (years of " + run.YearDays + " days)";
        bool report = Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1";
        if (report)
        {
            LivingReport(run, label);
        }
        LivingBands(run, 10, label);
        LivingReference(run, label);
        LivingMoney(run, label, report);
        LivingClock(run);
    }

    // The one clock: the same city lived through one of the save's years at 365 days goes as it does through the default's first (60 days): its
    // people and households, its births and deaths, unemployment, rents, pay and digital spending moving alike, so a year of the game is a year of
    // the city at any year length the player picks.
    private static void LivingClock(LivingRun shortYears)
    {
        PopulationSim sim = PopulationMade(LivingSeed, out PopulationState s, out _, 365);
        var run = new LivingRun { YearDays = 365 };
        LivingStart(run, sim, s);
        for (int day = 2; day <= 366; day++)
        {
            int month = s.MonthNumber;
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
            LivingDay(run, sim, s, null, month, day);
        }
        LivingYear a0 = shortYears.Years[0], a = shortYears.Years[1], b0 = run.Years[0], b = run.Years[1];
        int peopleA = (a.People - a0.People) * 1000 / a0.People, peopleB = (b.People - b0.People) * 1000 / b0.People;
        int householdsA = (a.Households - a0.Households) * 1000 / a0.Households, householdsB = (b.Households - b0.Households) * 1000 / b0.Households;
        bool rents = true;
        for (int d = 0; d < a.Rent.Length && d < b.Rent.Length; d++)
        {
            rents &= Math.Abs(a.Rent[d] - b.Rent[d]) <= 40;
        }
        bool ok = Math.Abs(peopleA - peopleB) <= 8 && Math.Abs(householdsA - householdsB) <= 8 && Math.Abs(a.Births - b.Births) * 5 <= a.Births && Math.Abs(a.Deaths - b.Deaths) * 5 <= a.Deaths
                  && Math.Abs(a.Unemployment - b.Unemployment) <= 15 && rents && Math.Abs(a.WageLeast - b.WageLeast) <= 15 && Math.Abs(a.Digital - b.Digital) <= 40;
        string figures = "people " + peopleA + " and " + peopleB + " thousandths, households " + householdsA + " and " + householdsB + ", births " + a.Births + " and " + b.Births + ", deaths " + a.Deaths
                         + " and " + b.Deaths + ", unemployment " + a.Unemployment + " and " + b.Unemployment + ", rents to " + a.RentMost + " and " + b.RentMost + ", pay down to " + a.WageLeast + " and "
                         + b.WageLeast + ", digital " + a.Digital + " and " + b.Digital;
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  the one clock, a year of 60 days against one of 365: " + figures);
        }
        Check(ok, "living years: the one clock: a year of 365 days and one of 60 move the city alike (" + figures + ")");
    }
}
