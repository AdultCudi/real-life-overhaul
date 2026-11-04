using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The office floors' firms move with the city (the brief's firms that open and close): the measured office jobs are the let floors (a tenth of
    /// the floor stood empty, <see cref="PopulationCatalogue.OfficeVacancyStart"/>), and once a month the firms on the other floors of every office
    /// building take on or shed jobs part of the way to the level the city's people ask for: three parts its labour force and one its residents'
    /// spending, each against the first month's. Once every empty desk is let and the city asks for more, offices go up on the mod's land for the jobs
    /// still wanted, a hundredth of the office jobs a year at the most. Firms open and close all the while at the churn real cities show, and more open than close while the city
    /// grows. New office buildings (the mod's land, the player's) bring their own firms when they join the table. The firm counts by size the tech
    /// markets read (SectorRules.FirmsOf) follow the jobs, so the markets grow with the living city. Game-free. COST: nothing per frame; a pass over
    /// the employers once a month.
    /// </summary>
    public static class CityFirmRules
    {
        /// <summary>The most the firms' jobs reach, thousandths of the start's: every empty desk of the floor let.</summary>
        public static int StockMost => 1000 * 1000 / (1000 - PopulationCatalogue.OfficeVacancyStart);

        /// <summary>The month's turn, with the households' spending in the month that ended: the firms' level moved and their jobs set.</summary>
        public static void Month(PopulationState s, long spending)
        {
            GrowthState g = s.Growth;
            int labour = LabourForce(s);
            long measured = MeasuredJobs(s);
            if (g.SpendAnchor <= 0 || g.LabourAnchor <= 0 || g.OfficeAnchor <= 0)
            {
                g.SpendAnchor = Math.Max(1, spending);   // the first month measured: the firms stand where the city made them
                g.LabourAnchor = Math.Max(1, labour);
                g.OfficeAnchor = (int)Math.Min(int.MaxValue, Math.Max(1, measured));
            }
            int asked = Asked(spending, g.SpendAnchor, labour, g.LabourAnchor);
            g.Target = PopulationCatalogue.Clamp(asked, PopulationCatalogue.StockLeast, StockMost);
            g.Stock = Step(g.Stock, g.Target);
            Apply(s);
            g.OfficesWanted = Crowded(g) ? OfficesWantedOf(asked, g.OfficeAnchor, measured, g.Stock) : 0;
            Churn(s);
        }

        /// <summary>
        /// The level the city asks for, thousandths: three parts its labour force's and one its spending's against the first month's, within the floor's room
        /// (firms hire where the people are; richer households buy more, but much of it from firms beyond the city).
        /// </summary>
        public static int Target(long spending, long spendAnchor, int labour, int labourAnchor)
        {
            return PopulationCatalogue.Clamp(Asked(spending, spendAnchor, labour, labourAnchor), PopulationCatalogue.StockLeast, StockMost);
        }

        /// <summary>The level asked before the floor's bounds: past its top it is jobs the city's offices cannot hold.</summary>
        public static int Asked(long spending, long spendAnchor, int labour, int labourAnchor)
        {
            long bySpend = spendAnchor > 0 ? spending * 1000 / spendAnchor : 1000;
            long byLabour = labourAnchor > 0 ? (long)labour * 1000 / labourAnchor : 1000;
            return (int)Math.Min(int.MaxValue, (bySpend + byLabour * 3) / 4);
        }

        /// <summary>
        /// Office jobs a year wanted on new land: the jobs asked of the first month's office floors less those the offices hold now (new ones counted),
        /// at most a hundredth of them a year, as the homes.
        /// </summary>
        public static int OfficesWantedOf(int asked, long anchor, long measured, int stock)
        {
            long gap = (long)asked * anchor / 1000 - measured * stock / 1000;
            return (int)Math.Max(0L, Math.Min(gap, measured * PopulationCatalogue.PaceMost / 1000));
        }

        /// <summary>The firms' level a month on: part of the way to the level asked, a thousandth at the least so it arrives, within its bounds.</summary>
        public static int Step(int stock, int target)
        {
            int gap = target - stock;
            int step = gap * PopulationCatalogue.FirmAdjust / 1000;
            step = step != 0 ? step : Math.Sign(gap);
            return PopulationCatalogue.Clamp(stock + step, PopulationCatalogue.StockLeast, StockMost);
        }

        /// <summary>
        /// True while the firms want more floor than the city's offices hold: they are at the top of the floor's room and the city asks for more. The
        /// jobs still wanted (<see cref="OfficesWantedOf"/>) raise the mod's land's offices pull then (GrowthRules), so the developers build offices there.
        /// </summary>
        public static bool Crowded(GrowthState g) => g.Target >= StockMost && g.Stock >= StockMost - CrowdedMargin;

        public const int CrowdedMargin = 10;   // thousandths under the top that read as full

        /// <summary>Every office building's other firms' jobs at the level now; one with more people than jobs lets the newest go.</summary>
        public static void Apply(PopulationState s)
        {
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Kind[e] == (byte)EmployerKind.Tenants && et.Base[e] > 0)
                {
                    et.Slots[e] = SlotsNow(s, e);
                    LabourRules.Shed(s, e);
                }
            }
        }

        // The office floors' firms' jobs as measured, before their level.
        private static long MeasuredJobs(PopulationState s)
        {
            long n = 0;
            for (int e = 0; e < s.Employers.Count; e++)
            {
                n += s.Employers.Kind[e] == (byte)EmployerKind.Tenants ? s.Employers.Base[e] : 0;
            }
            return n;
        }

        /// <summary>
        /// An office building's other firms' jobs now: the level's, but once the city has lived a month no more than its people and a tenth of its floor
        /// more, so a new building's floors are let over the months (a new office tower leases up in a year or so) and never open all at once.
        /// </summary>
        public static int SlotsNow(PopulationState s, int e)
        {
            EmployerTable et = s.Employers;
            int full = SlotsOf(et.Base[e], s.Growth.Stock);
            if (s.Growth.Year < 0)
            {
                return full;   // a city being made: its floors stand let as measured
            }
            long let = (long)et.Filled[e] + et.Commuters[e] + Math.Max(1, et.Base[e] * LeaseUp / 1000);
            return (int)Math.Min(full, let);
        }

        /// <summary>A business of the city or of a rival that is new to the table has opened: noted for the paper with its name and its day.
        /// The player's and a guest's are no news of the city's, and a business with no name has no story.</summary>
        public static void Opened(PopulationState s, in BusinessSeed bs, int day)
        {
            if ((bs.Owner == Owner.City || bs.Owner == Owner.Rival) && !string.IsNullOrEmpty(bs.Name))
            {
                s.Events.Add(FirmEventKind.Opened, bs.Owner, day, 0, bs.Name);
            }
        }

        public const int LeaseUp = 100;   // thousandths of a floor's jobs its firms take on a month at the most

        /// <summary>The jobs of a floor's firms at a level.</summary>
        public static int SlotsOf(int measured, int stock) => (int)((long)Math.Max(0, measured) * Math.Max(0, stock) / 1000);

        /// <summary>
        /// The city's firms counted for the churn: each employer of its own (a business, a special service, the public sector) one, and an office
        /// building's other floors one for every <see cref="PopulationCatalogue.TenantFirmJobs"/> jobs.
        /// </summary>
        public static int Count(PopulationState s)
        {
            EmployerTable et = s.Employers;
            long firms = 0, tenants = 0;
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Slots[e] <= 0)
                {
                    continue;
                }
                switch ((EmployerKind)et.Kind[e])
                {
                    case EmployerKind.Tenants:
                        tenants += et.Slots[e];
                        break;
                    case EmployerKind.Building:
                        break;   // a residential building's own staff are no firm of their own
                    default:
                        firms++;
                        break;
                }
            }
            return (int)Math.Min(int.MaxValue, firms + tenants / PopulationCatalogue.TenantFirmJobs);
        }

        // The year's firms opened and closed so far: the churn's share of the months gone, and the change since the year began on the side it falls.
        private static void Churn(PopulationState s)
        {
            GrowthState g = s.Growth;
            int months = s.MonthNumber % 12 + 1;
            int churn = (int)((long)g.FirmsYear * PopulationCatalogue.FirmChurn / 1000 * months / 12);
            int net = Count(s) - g.FirmsYear;
            g.Opened = churn + Math.Max(0, net);
            g.Closed = churn + Math.Max(0, -net);
        }

        private static int LabourForce(PopulationState s)
        {
            int n = 0;
            for (int p = 0; p < s.People.Count; p++)
            {
                if (s.People.Id[p] != 0)
                {
                    n += s.People.Status[p] == (byte)Status.Worker || s.People.Status[p] == (byte)Status.Unemployed ? 1 : 0;
                }
            }
            return n;
        }
    }
}
