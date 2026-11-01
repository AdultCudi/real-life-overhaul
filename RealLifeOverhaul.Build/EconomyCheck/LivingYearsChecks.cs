using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

// The living economy's features together, offline (report only; "--living-years"): each feature's own checks run it on a
// documented city, so a drift that only the features together make would show nowhere. Here the population lives through
// the markets' ten years (ScaleCatalogue.SimDays, 365 days a year, whatever the calendar's length; the population ages by
// the save's year), and each market year the figures the other features read from it are taken as the wiring hands them
// over: the tech markets' city (Wiring.SectorLinks.City: people, firms by size, the households' digital spending against
// the first day's) and the studio's audience (Wiring.PopulationLinks.Audience: the residents who play, against the start,
// times the world). Then the studio's and the other tech firms' ten years run again on that living city and are set beside
// their runs on the documented one; the rents by district are shown against their top. It checks nothing and is not in
// the full run: what it prints is read by whoever integrates the overhaul.
internal static partial class Program
{
    private const int LivingYearDays = 365;      // days of a market year: the snapshots' step
    private const int LivingSeed = 1234;         // the city PopulationYears lives, so the two runs can be read together

    private sealed class LivingYear
    {
        public int Day, People, Households, Unemployment, Small, Mid, Large, Digital, RentLeast = int.MaxValue, RentMost, RentTopped, WageLeast = int.MaxValue, WageMost;
        public long Players, PlayersStart;
        public int[] Games = new int[GameRules.Cells];
        public int[] Rent;
        public readonly SectorCityFigures City = new SectorCityFigures();
    }

    private static void LivingYears()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        PopulationSim sim = PopulationMade(LivingSeed, out PopulationState s, out _);
        int days = ScaleCatalogue.SimDays;
        var years = new List<LivingYear>(days / LivingYearDays + 1);
        long anchor = 0;
        for (int day = 2; day <= days + 1; day++)
        {
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
            if (day == 2 || (day - 1) % LivingYearDays == 0)
            {
                LivingYear y = LivingSnapshot(sim, s, day, ref anchor);
                years.Add(y);
            }
        }
        double popMs = clock.Elapsed.TotalMilliseconds;
        int[] start = (int[])s.GameStart.Clone();
        Console.WriteLine("  the population through " + days + " market days (" + days / Math.Max(1, s.YearDays) + " of its own years of " + s.YearDays + " days) in "
                          + (popMs / 1000).ToString("F1", CultureInfo.InvariantCulture) + " s");
        string[] names = new string[s.Districts.Count];
        for (int d = 0; d < names.Length; d++)
        {
            names[d] = s.Districts[d].Id.Replace("ba:neighborhood_", string.Empty);
        }
        Console.WriteLine("  year  people  households  unemployed  small/mid/large firms  digital  players/start  rents least..most (topped)  pay least..most");
        for (int i = 0; i < years.Count; i++)
        {
            LivingYear y = years[i];
            Console.WriteLine("  " + i.ToString().PadLeft(4) + "  " + y.People.ToString().PadLeft(6) + "  " + y.Households.ToString().PadLeft(10) + "  " + (y.Unemployment / 10.0).ToString("F1", CultureInfo.InvariantCulture).PadLeft(9) + "%"
                              + "  " + (y.Small + "/" + y.Mid + "/" + y.Large).PadLeft(21) + "  " + y.Digital.ToString().PadLeft(7) + "  " + (y.PlayersStart > 0 ? (y.Players * 1000 / y.PlayersStart).ToString() : "-").PadLeft(13)
                              + "  " + (y.RentLeast + ".." + y.RentMost + " (" + y.RentTopped + ")").PadLeft(25) + "  " + y.WageLeast + ".." + y.WageMost);
        }
        LivingYear last = years[years.Count - 1];
        var rents = new System.Text.StringBuilder("  rents by district, first year and last (thousandths of the game's, top " + PopulationCatalogue.RentMost + "):");
        for (int d = 0; d < names.Length && years[0].Rent != null && d < years[0].Rent.Length; d++)
        {
            int topped = -1;
            for (int i = 0; i < years.Count && topped < 0; i++)
            {
                topped = years[i].Rent[d] >= PopulationCatalogue.RentMost ? i : -1;
            }
            rents.Append(' ').Append(names[d]).Append(' ').Append(years[0].Rent[d]).Append("->").Append(last.Rent[d]).Append(topped >= 0 ? " (top in market year " + topped + ")" : string.Empty).Append(';');
        }
        Console.WriteLine(rents.ToString());

        // The studio's ten years on the living city's audience, beside the documented world's.
        StudioBridges.AudienceAsk was = StudioBridges.Population, alive = LivingAudience(years, start);
        try
        {
            for (int sc = 0; sc < (int)Scope.Count; sc++)
            {
                StudioBridges.Population = alive;
                LivingStudio((Scope)sc, out int livingRate, out long livingDay);
                StudioBridges.Population = null;
                LivingStudio((Scope)sc, out int worldRate, out long worldDay);
                Console.WriteLine("  studio " + (Scope)sc + ": " + livingRate + " ppm a day on the living city (payback " + (livingRate > 0 ? 1000000 / livingRate : -1) + " days, $" + livingDay.ToString("N0", CultureInfo.InvariantCulture)
                                  + " a day) against " + worldRate + " on the documented world (payback " + (worldRate > 0 ? 1000000 / worldRate : -1) + " days, $" + worldDay.ToString("N0", CultureInfo.InvariantCulture) + ")");
            }
        }
        finally
        {
            StudioBridges.Population = was;
        }

        // The other tech firms' ten years on the living city's markets, beside the documented city's.
        Action<SectorFile, int> living = (f, day) => f.City.CopyFrom(years[Math.Max(0, Math.Min(years.Count - 1, (day - 1) / LivingYearDays))].City);
        LivingSector("SaaS for businesses", SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, days, 11, 1, 1, living), SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, days, 11));
        LivingSector("SaaS for enterprises", SectorProductSim(ProductKind.Saas, Segment.Enterprise, 5, 1, 2, 2, 1, 2, days, 12, 1, 1, living), SectorProductSim(ProductKind.Saas, Segment.Enterprise, 5, 1, 2, 2, 1, 2, days, 12));
        LivingSector("consumer app", SectorProductSim(ProductKind.App, Segment.Consumer, 4, 1, 1, 0, 1, 3, days, 13, 1, 1, living), SectorProductSim(ProductKind.App, Segment.Consumer, 4, 1, 1, 0, 1, 3, days, 13));
        LivingSector("AI model", SectorProductSim(ProductKind.Model, Segment.Enterprise, 4, 0, 1, 1, 1, 2, days, 15, 1, 1, living), SectorProductSim(ProductKind.Model, Segment.Enterprise, 4, 0, 1, 1, 1, 2, days, 15));
        LivingSector("consultancy", SectorServicesSim(FirmKind.Consultancy, 4, 1, days, 16, false, living), SectorServicesSim(FirmKind.Consultancy, 4, 1, days, 16));
        LivingSector("cybersecurity", SectorServicesSim(FirmKind.Security, 4, 1, days, 17, false, living), SectorServicesSim(FirmKind.Security, 4, 1, days, 17));
        LivingSector("design studio", SectorServicesSim(FirmKind.Design, 3, 1, days, 18, false, living), SectorServicesSim(FirmKind.Design, 3, 1, days, 18));
        Console.WriteLine("  living years took " + clock.ElapsedMilliseconds + " ms");
    }

    private static StudioBridges.AudienceAsk LivingAudience(List<LivingYear> years, int[] start)
    {
        return (int genre, int platform, int day, out long residents) =>
        {
            LivingYear y = years[Math.Max(0, Math.Min(years.Count - 1, day / LivingYearDays))];
            int cell = genre * GameRules.Platforms + platform;
            residents = cell >= 0 && cell < y.Games.Length ? y.Games[cell] : 0;
            return cell >= 0 && cell < start.Length ? GameRules.Audience(residents, start[cell], StudioBridges.World(genre, platform, day)) : -1;
        };
    }

    // The studio's ten years at a scope over the seeds StudioTenYears uses: the mean return a day, millionths of capital, and the profit a day in whole money.
    private static void LivingStudio(Scope scope, out int rate, out long day)
    {
        const int Seeds = 16;
        long sum = 0;
        day = 0;
        for (int seed = 1; seed <= Seeds; seed++)
        {
            StudioTen r = TenYears(scope, seed * 7919 + (int)scope);
            sum += r.RatePpm;
            day += r.Cash / ScaleCatalogue.SimDays / 100;
        }
        rate = (int)(sum / Seeds);
        day /= Seeds;
    }

    private static void LivingSector(string what, SectorRun living, SectorRun documented)
    {
        Console.WriteLine("  " + what + ": payback day " + living.Payback + ", $" + living.Day.ToString("N0", CultureInfo.InvariantCulture) + " a day in the last year, " + living.Customers.ToString("N0", CultureInfo.InvariantCulture) + " of "
                          + living.Buyers.ToString("N0", CultureInfo.InvariantCulture) + " buyers on the living city; payback day " + documented.Payback + ", $" + documented.Day.ToString("N0", CultureInfo.InvariantCulture) + " a day, "
                          + documented.Customers.ToString("N0", CultureInfo.InvariantCulture) + " of " + documented.Buyers.ToString("N0", CultureInfo.InvariantCulture) + " on the documented one");
    }

    // A market year's figures, as the wiring reads them from the population for the tech markets and the studio.
    private static LivingYear LivingSnapshot(PopulationSim sim, PopulationState s, int day, ref long anchor)
    {
        var y = new LivingYear { Day = day, People = s.People.Live, Households = s.Households.Live, Unemployment = Unemployment(s) };
        SectorRules.FirmsOf(s.Employers.Kind, s.Employers.Slots, s.Employers.Count, out y.Small, out y.Mid, out y.Large);
        long digital = 0;
        PoolRules.DemandDay dd = sim.Demand;
        for (int h = 0; dd != null && dd.Day >= 0 && h < s.Households.Count && (h + 1) * PopulationCatalogue.Categories <= dd.Budget.Length; h++)
        {
            digital += s.Households.Alive(h) ? dd.Budget[h * PopulationCatalogue.Categories + (int)Spend.Digital] : 0;
        }
        if (anchor <= 0)
        {
            anchor = SectorRules.DigitalPerPerson(digital, y.People);
        }
        y.Digital = SectorRules.Digital(digital, y.People, anchor);
        y.City.Set(y.People, y.Small, y.Mid, y.Large, y.Digital, y.People > 0);
        Array.Copy(s.GameCounts, y.Games, Math.Min(y.Games.Length, s.GameCounts.Length));
        for (int c = 0; c < s.GameCounts.Length && c < s.GameStart.Length; c++)
        {
            y.Players += s.GameCounts[c];
            y.PlayersStart += s.GameStart[c];
        }
        y.Rent = (int[])s.RentIndex.Clone();
        for (int d = 0; d < y.Rent.Length; d++)
        {
            y.RentLeast = Math.Min(y.RentLeast, y.Rent[d]);
            y.RentMost = Math.Max(y.RentMost, y.Rent[d]);
            y.RentTopped += y.Rent[d] >= PopulationCatalogue.RentMost ? 1 : 0;
        }
        for (int f = 0; f < s.FieldWage.Length; f++)
        {
            y.WageLeast = Math.Min(y.WageLeast, s.FieldWage[f]);
            y.WageMost = Math.Max(y.WageMost, s.FieldWage[f]);
        }
        return y;
    }
}
