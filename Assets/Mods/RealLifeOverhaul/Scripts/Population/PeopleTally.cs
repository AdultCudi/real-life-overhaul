using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The city's people counted from the records themselves, for the Economy app's People page. A district's saved figures (PopulationState.Figures)
    /// are worked out by the day's turn and are not kept in the save, so right after a load they read nothing until the next day; the records are
    /// always there, so this counts them. What needs a closed month (who moved in and out) says so, never a nought. Game-free. Cost: one pass over the
    /// records when the page is made or the day changes, never a frame.
    /// </summary>
    public sealed class PeopleTally
    {
        public const int Ages = 3, Incomes = 3;

        public int People, Households, Workers, Unemployed, Jobs, Vacancies, Renters;
        public long RentCents;                              // cents a day, summed over the households that rent
        public readonly int[] AgeCounts = new int[Ages];    // under 25, 25 to 54, 55 and over
        public readonly int[] IncomeCounts = new int[Incomes];

        public bool MonthClosed;                            // a whole month has closed since the city was generated, so the moves of the last one are real
        public int MovedIn, MovedOut;                       // households, in the last closed month
        public int MonthClosesOnDay;                        // the first day the month's moves are kept, while none is

        public int[] DistrictPeople = new int[0];
        public int[] DistrictHouseholds = new int[0];
        public int[] DistrictRenters = new int[0];
        public long[] DistrictRentCents = new long[0];
        public int[] DistrictWorkers = new int[0];
        public int[] DistrictUnemployed = new int[0];
        public int[] DistrictJobs = new int[0];
        private int[] _districtAges = new int[0];      // Ages a district
        private int[] _districtIncomes = new int[0];   // Incomes a district

        /// <summary>The whole money a household that rents pays a day, on average.</summary>
        public int MeanRent => Renters > 0 ? (int)Math.Min(int.MaxValue, RentCents / Renters / 100) : 0;

        /// <summary>The share of the people looking for work among those who work or look, in tenths of a per cent; -1 where nobody is in the labour force.</summary>
        public int OutOfWorkPermille => Workers + Unemployed > 0 ? (int)((long)Unemployed * 1000 / (Workers + Unemployed)) : -1;

        public int DistrictMeanRent(int d) => d >= 0 && d < DistrictRenters.Length && DistrictRenters[d] > 0 ? (int)Math.Min(int.MaxValue, DistrictRentCents[d] / DistrictRenters[d] / 100) : 0;

        public int DistrictOutOfWorkPermille(int d)
        {
            if (d < 0 || d >= DistrictWorkers.Length)
            {
                return -1;
            }
            int labour = DistrictWorkers[d] + DistrictUnemployed[d];
            return labour > 0 ? (int)((long)DistrictUnemployed[d] * 1000 / labour) : -1;
        }

        /// <summary>The people of a district by age (under 25, 25 to 54, 55 and over).</summary>
        public int[] DistrictAgeCounts(int d) => Slice(_districtAges, d, Ages);

        /// <summary>The households of a district by income class.</summary>
        public int[] DistrictIncomeCounts(int d) => Slice(_districtIncomes, d, Incomes);

        private static int[] Slice(int[] all, int d, int width)
        {
            var part = new int[width];
            if (d >= 0 && (d + 1) * width <= all.Length)
            {
                Array.Copy(all, d * width, part, 0, width);
            }
            return part;
        }

        /// <summary>The district with most households (the lowest place on a tie), or -1 where nobody lives anywhere.</summary>
        public int Busiest()
        {
            int best = -1, most = 0;
            for (int d = 0; d < DistrictHouseholds.Length; d++)
            {
                if (DistrictHouseholds[d] > most)
                {
                    most = DistrictHouseholds[d];
                    best = d;
                }
            }
            return best;
        }

        /// <summary>Counts the records. A city with nobody in it gives a tally of nothing (People is 0).</summary>
        public static PeopleTally Read(PopulationState s)
        {
            var t = new PeopleTally();
            if (s == null)
            {
                return t;
            }
            int nd = Math.Max(1, s.Districts.Count);
            t.DistrictPeople = new int[nd];
            t.DistrictHouseholds = new int[nd];
            t.DistrictRenters = new int[nd];
            t.DistrictRentCents = new long[nd];
            t.DistrictWorkers = new int[nd];
            t.DistrictUnemployed = new int[nd];
            t.DistrictJobs = new int[nd];
            t._districtAges = new int[nd * Ages];
            t._districtIncomes = new int[nd * Incomes];
            PersonTable people = s.People;
            HouseholdTable homes = s.Households;
            for (int p = 0; p < people.Count; p++)
            {
                if (people.Id[p] == 0)
                {
                    continue;
                }
                t.People++;
                int age = s.Age(p, s.Day);
                int band = age < 25 ? 0 : age < 55 ? 1 : 2;
                t.AgeCounts[band]++;
                int h = people.Household[p];
                int d = h >= 0 && h < homes.Count ? homes.District[h] : -1;
                if (d >= 0 && d < nd)
                {
                    t.DistrictPeople[d]++;
                    t._districtAges[d * Ages + band]++;
                }
                var status = (Status)people.Status[p];
                if (status == Status.Worker)
                {
                    t.Workers++;
                    if (d >= 0 && d < nd)
                    {
                        t.DistrictWorkers[d]++;
                    }
                }
                else if (status == Status.Unemployed)
                {
                    t.Unemployed++;
                    if (d >= 0 && d < nd)
                    {
                        t.DistrictUnemployed[d]++;
                    }
                }
            }
            for (int h = 0; h < homes.Count; h++)
            {
                if (!homes.Alive(h))
                {
                    continue;
                }
                t.Households++;
                int c = homes.Class[h];
                if (c >= 0 && c < Incomes)
                {
                    t.IncomeCounts[c]++;
                }
                int d = homes.District[h];
                if (d >= nd)
                {
                    continue;
                }
                t.DistrictHouseholds[d]++;
                if (c >= 0 && c < Incomes)
                {
                    t._districtIncomes[d * Incomes + c]++;
                }
                if (homes.Landlord[h] != Landlords.Own && homes.Rent[h] > 0)
                {
                    t.Renters++;
                    t.RentCents += homes.Rent[h];
                    t.DistrictRenters[d]++;
                    t.DistrictRentCents[d] += homes.Rent[h];
                }
            }
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                t.Jobs += et.Slots[e];
                t.Vacancies += et.Vacancies(e);
                int b = et.Building[e];
                int d = b >= 0 && b < s.Buildings.Count ? s.Buildings[b].District : -1;
                if (d >= 0 && d < nd)
                {
                    t.DistrictJobs[d] += et.Slots[e];
                }
            }
            int monthDays = GenerateRules.MonthDays(s.YearDays);
            int firstMonth = GenerateRules.MonthOf(s.Generated, s.YearDays);
            t.MonthClosed = s.MonthNumber > firstMonth;
            t.MonthClosesOnDay = (firstMonth + 1) * monthDays + 1;
            if (t.MonthClosed)
            {
                t.MovedIn = s.Region.MovedInLast;
                t.MovedOut = s.Region.MovedOutLast;
            }
            return t;
        }
    }
}
