using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The city's growth as the population keeps it: the mod's land each district can still take homes on, the housing pressure and the homes a year
    /// the city wants built (the construction demand), the homes started and finished, and the office floors' firms. Saved with the population (its
    /// version 3); an older save starts it empty and measures it at its first month.
    /// </summary>
    public sealed class GrowthState
    {
        public int[] Land = new int[0];        // flats each district's own land can still take; -1 for one the mod builds nothing new in (the game's districts)
        public int[] LandStart = new int[0];   // ... the most it was measured at: the share left slows the building as it runs out
        public int Pressure;                   // 0 to 100 at the last month's turn
        public int Wanted;                     // homes a year the city wants started, at the last month's turn: the construction demand
        public int Started, Built;             // homes the city's developers started, and homes finished by anyone, in the running year
        public int StartedLast, BuiltLast;     // ... in the last year
        public int BuiltAll;                   // homes finished since the population began
        public int Unformed;                   // households forming in the running month (grown children, partners parting) that found no flat
        public int Year = -1;                  // the running year's number from the population's first (-1 before its first month's turn)
        public int Stock = 1000;               // thousandths of the measured office floor's firms' jobs the firms hold now
        public int Opened, Closed;             // firms opened and closed in the running year
        public int OpenedLast, ClosedLast;
        public int FirmsYear;                  // firms at the running year's start, the churn's base
        public long SpendAnchor;               // cents: the households' spending in the first month the firms were measured against
        public int LabourAnchor;               // the residents in the labour force then
        public int Target;                     // thousandths: the level the city asked of the firms at the last month's turn; not saved (the next month works it out)
        public int OfficesWanted;              // office jobs a year the crowded firms want built on the mod's land, at the last month's turn
        public int OfficesStarted;             // ... started in the running year
        public int PeopleStart;                // the people when the studio's start was taken: the residents who play are read against it as a share
        public int OfficeAnchor;               // the office floors' firms' jobs as measured at the first month: the jobs the city asks for are read against it

        /// <summary>The arrays sized to the city's districts, a new district not the mod's land until measured.</summary>
        public void Size(int districts)
        {
            if (Land.Length == districts)
            {
                return;
            }
            int was = Land.Length;
            Array.Resize(ref Land, districts);
            Array.Resize(ref LandStart, districts);
            for (int d = was; d < districts; d++)
            {
                Land[d] = -1;
            }
        }
    }

    /// <summary>
    /// The city's homes grow (the brief's gentle growth; POPULATION.md section 14: new building only on the mod's own land): once a month the housing
    /// pressure is read from the city's free flats against the share a market keeps and the households turned away (the region's that wanted to come,
    /// grown children and parting partners that found no flat), and the homes a year the city wants started follows from it, at most
    /// <see cref="PopulationCatalogue.PaceMost"/> thousandths of its homes and less as its land runs out. That figure is the construction demand:
    /// the mod's land districts' homes pull is raised while the year's homes are still wanted and closed once they are started, so Uptown's market
    /// asks the city's developers for homes only then, and the player's construction company sees the same pull in its tenders. Homes anyone
    /// finishes (the player's included) join the city's table (<see cref="PopulationSim.Rebuild"/>) and draw households as every flat does.
    /// Game-free. COST: nothing per frame; a pass over the districts once a month.
    /// </summary>
    public static class GrowthRules
    {
        /// <summary>The month's turn: the year rolled, the pressure and the homes wanted worked out, the mod's land's homes pull set in the pull table.</summary>
        public static void Month(PopulationState s, int[] pulls)
        {
            GrowthState g = s.Growth;
            g.Size(s.Districts.Count);
            Roll(s);
            int flats = 0, vacant = 0, waiting = g.Unformed;
            for (int d = 0; d < s.Districts.Count && d < s.Figures.Length; d++)
            {
                if (s.Districts[d].Villas)
                {
                    continue;   // villas change hands as the game sells them
                }
                flats += s.Figures[d].Flats;
                vacant += s.Figures[d].Vacant;
                waiting += s.Figures[d].MigrantsWanting;
            }
            g.Unformed = 0;
            g.Pressure = Pressure(flats, vacant, waiting);
            g.Wanted = Wanted(flats, g.Pressure, LandLeft(g));
            Pulls(s, pulls);
        }

        /// <summary>
        /// The mod's land's homes pull from the homes still wanted this year (open, or closed once they are started), and its offices pull raised while
        /// the firms are crowded; the game's districts and the other uses keep theirs. Again at each start, so a month never starts more than it wants.
        /// </summary>
        public static void Pulls(PopulationState s, int[] pulls)
        {
            GrowthState g = s.Growth;
            int room = Room(g);
            for (int d = 0; d < g.Land.Length && pulls != null && d * 4 < pulls.Length; d++)
            {
                if (g.Land[d] < 0)
                {
                    continue;   // the game's districts answer with rents and vacancy alone
                }
                pulls[d * 4] = room > 0 && g.Land[d] > 0 ? PopulationCatalogue.PullRoom + g.Pressure * (HousingRules.PullScale - PopulationCatalogue.PullRoom) / 100
                    : PopulationCatalogue.PullClosed;
                if (g.OfficesStarted < g.OfficesWanted && g.Land[d] > 0 && d * 4 + 2 < pulls.Length)
                {
                    pulls[d * 4 + 2] = Math.Max(pulls[d * 4 + 2], OfficesPull);   // the firms have filled the city's office floors: build offices on the mod's land
                }
            }
        }

        public const int OfficesPull = 60;   // the mod's land's offices pull while the firms are crowded: Uptown's market reads it over the developers' threshold

        /// <summary>
        /// The housing pressure, 0 to 100: how far the city's free flats are under the share at which it builds (the whole span the most), and the
        /// households turned away last month as a share of its flats.
        /// </summary>
        public static int Pressure(int flats, int vacant, int waiting)
        {
            if (flats <= 0)
            {
                return 0;
            }
            int vacancy = (int)(1000L * Math.Max(0, vacant) / flats);
            int shortage = (PopulationCatalogue.VacancyBuild - vacancy) * 100 / PopulationCatalogue.VacancySpan;
            int turned = (int)(1000L * Math.Max(0, waiting) * PopulationCatalogue.WaitingWeight / flats);
            return PopulationCatalogue.Clamp(Math.Max(0, shortage) + turned, 0, 100);
        }

        /// <summary>Homes a year the city wants started: its homes times the pace at that pressure, times the thousandths of its land left.</summary>
        public static int Wanted(int flats, int pressure, int landLeft)
        {
            long wanted = (long)Math.Max(0, flats) * PopulationCatalogue.PaceMost * PopulationCatalogue.Clamp(pressure, 0, 100) / 100 / 1000;
            return (int)(wanted * PopulationCatalogue.Clamp(landLeft, 0, 1000) / 1000);
        }

        /// <summary>Thousandths of the mod's land left against the most it was measured at; nought with none measured (nothing to build on).</summary>
        public static int LandLeft(GrowthState g)
        {
            long left = 0, start = 0;
            for (int d = 0; d < g.Land.Length; d++)
            {
                if (g.Land[d] >= 0)
                {
                    left += g.Land[d];
                    start += Math.Max(g.Land[d], g.LandStart[d]);
                }
            }
            return start <= 0 ? 0 : (int)(1000L * left / start);
        }

        /// <summary>Homes the city still wants started this year.</summary>
        public static int Room(GrowthState g) => Math.Max(0, g.Wanted - g.Started);

        /// <summary>A district's own land measured: the flats it can still take (-1 for a district the mod builds nothing new in).</summary>
        public static void SetLand(PopulationState s, int d, int flats)
        {
            GrowthState g = s.Growth;
            g.Size(s.Districts.Count);
            if (d < 0 || d >= g.Land.Length)
            {
                return;
            }
            g.Land[d] = flats < 0 ? -1 : flats;
            g.LandStart[d] = flats < 0 ? 0 : Math.Max(g.LandStart[d], flats);
        }

        /// <summary>The flats so many m2 of homes floor in a district holds (the net share in flats of its size).</summary>
        public static int FlatsOf(PopulationState s, int d, long sqm)
        {
            PopDistrict district = d >= 0 && d < s.Districts.Count ? s.Districts[d] : null;
            return (int)Math.Min(int.MaxValue / 4, Math.Max(0L, sqm) * PopulationCatalogue.HomeNetShare / 1000 / MeasureRules.FlatNet(district));
        }

        /// <summary>The city's developers started homes of so many flats: the pull closes at once when the year's are all started.</summary>
        public static void Start(PopulationState s, int flats, int[] pulls)
        {
            s.Growth.Started += Math.Max(0, flats);
            Pulls(s, pulls);
        }

        /// <summary>The city's developers started offices of so many jobs: the offices pull closes at once when the year's are all started.</summary>
        public static void StartOffices(PopulationState s, int jobs, int[] pulls)
        {
            s.Growth.OfficesStarted += Math.Max(0, jobs);
            Pulls(s, pulls);
        }

        /// <summary>The jobs so many m2 of office floor holds (a desk for every so many m2 of its net floor).</summary>
        public static int JobsOf(long sqm) => (int)Math.Min(int.MaxValue / 4, Math.Max(0L, sqm) * PopulationCatalogue.OfficeNetShare / 1000 / PopulationCatalogue.DeskSqm);

        /// <summary>Homes finished (any builder's): counted in the year and since the start.</summary>
        public static void Finished(PopulationState s, int flats)
        {
            if (flats > 0)
            {
                s.Growth.Built += flats;
                s.Growth.BuiltAll += flats;
            }
        }

        /// <summary>The flats of the city's homes, its villas left out.</summary>
        public static int Homes(PopulationState s)
        {
            int n = 0;
            for (int b = 0; b < s.Buildings.Count && b < s.FlatCount.Length; b++)
            {
                int d = s.Buildings[b].District;
                n += d >= 0 && d < s.Districts.Count && !s.Districts[d].Villas ? s.FlatCount[b] : 0;
            }
            return n;
        }

        // A new year of the population's: the running year's counts become the last's.
        private static void Roll(PopulationState s)
        {
            GrowthState g = s.Growth;
            int year = s.MonthNumber / 12;
            if (g.Year == year)
            {
                return;
            }
            if (g.Year >= 0)
            {
                g.StartedLast = g.Started;
                g.BuiltLast = g.Built;
                g.OpenedLast = g.Opened;
                g.ClosedLast = g.Closed;
            }
            g.Started = g.Built = g.Opened = g.Closed = g.OfficesStarted = 0;
            g.FirmsYear = CityFirmRules.Count(s);
            g.Year = year;
        }

        /// <summary>
        /// The buildings a city of these buildings has that the population's table has not, has no more, or has with another use or floor: the table
        /// is bound to them again when one differs (a building finished, demolished, extended or converted). A walk with a lookup a building.
        /// </summary>
        public static bool Differs(PopulationState s, List<CityBuilding> now)
        {
            if (now == null || now.Count != s.Buildings.Count)
            {
                return now != null;
            }
            for (int i = 0; i < now.Count; i++)
            {
                CityBuilding b = now[i];
                int at = s.BuildingOfKey(PopulationState.KeyOf(b.Street, b.Number));
                if (at < 0)
                {
                    return true;
                }
                CityBuilding was = s.Buildings[at];
                if (was.Use != b.Use || was.TotalSqm != b.TotalSqm || was.UnitSqm != b.UnitSqm)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
