using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The region round the city and the month's moves (POPULATION.md sections 4 and 5; the owner, 1 Nov 2026: the region as an aggregate):
    /// households move within the city when their rent is too much for them or their flat too small, and at the city's own rate; households leave
    /// after a long time out of work, or when no flat they can pay is to be had; households come from the region while jobs are open and flats free,
    /// and those that would come but find nothing are counted for the districts' pull for homes. Game-free. COST: nothing per frame; a pass over the
    /// households once a month, sliced by its caller.
    /// </summary>
    public static class RegionRules
    {
        /// <summary>A household's income after tax a day last month, cents (its ledger's last month over the month's days).</summary>
        public static long Disposable(PopulationState s, int h)
        {
            int row = h * Ledger.Lines;
            int[] last = s.Households.Last;
            long gross = (long)last[row + Ledger.Wages] + last[row + Ledger.Benefits] + last[row + Ledger.Pension] + last[row + Ledger.Returns];
            if (gross <= 0)
            {
                SpendRules.Income(s, h, s.Day, 0, out DayIncome inc);   // a household with no month yet: today's
                return inc.Disposable;
            }
            return (gross - last[row + Ledger.IncomeTax]) / GenerateRules.MonthDays(s.YearDays);
        }

        /// <summary>
        /// One household's month: it leaves the city (a long time out of work, or a rent past bearing with nothing cheaper), moves within it (it wants
        /// to, or at the city's rate), or stays. True when it is gone.
        /// </summary>
        public static bool Household(PopulationState s, LifeRules.LifeDay ld, int h, int day)
        {
            HouseholdTable hh = s.Households;
            if ((hh.Flags[h] & HouseholdFlags.Players) != 0 || HasLinked(s, h))
            {
                return false;   // the player's household, and a household of other systems' people, are theirs to move
            }
            long disposable = Disposable(s, h);
            int id = hh.Id[h], month = s.MonthNumber;
            if ((hh.Flags[h] & HouseholdFlags.Away) != 0)
            {
                // Its building is not standing at this load: it looks for a flat, and stays the city's while it finds none (its building may stand again).
                int found = HousingRules.FindFlat(s, ld.Free, s.Seed, id, day, hh.Size[h], Math.Max(1, disposable), hh.Class[h], hh.District[h], PoolRules.WorkDistrict(s, h), out int into);
                if (found >= 0)
                {
                    HousingRules.MoveInto(s, h, found, into, day);
                }
                return false;
            }
            if (LongOutOfWork(s, h, day) && PopDraw.Mille(s.Seed, PopDraw.Migrate, id, month, PopulationCatalogue.LeaveChance / 12 + 1))
            {
                LeaveFreeing(s, ld, h, day);
                return true;
            }
            int youngest = YoungestIfRetired(s, h, day);
            if (youngest >= 0 && PopDraw.Daily(s.Seed, PopDraw.Older, id, month, youngest >= PopulationCatalogue.OldestFrom ? PopulationCatalogue.OldestLeave : PopulationCatalogue.OlderLeave, 12))
            {
                LeaveFreeing(s, ld, h, day);   // its flat back on the market this month, where the region's younger households are first in line
                return true;
            }
            int rate = (HeadAge(s, h, day) < PopulationCatalogue.SettledFrom ? PopulationCatalogue.YoungLeave : PopulationCatalogue.SettledLeave)
                       + (HasChild(s, h, day) ? PopulationCatalogue.FamilyLeave : 0);
            if (youngest < 0 && PopDraw.Daily(s.Seed, PopDraw.Older, id, month + 5000, rate, 12))
            {
                LeaveFreeing(s, ld, h, day);   // a job, family or a cheaper life elsewhere in the country: the young move most
                return true;
            }
            bool wants = HousingRules.WantsMove(s, h, disposable);
            bool roams = PopDraw.Mille(s.Seed, PopDraw.Move, id, month, PopulationCatalogue.MoveShare / 12);
            if (!wants && !roams)
            {
                return false;
            }
            int work = PoolRules.WorkDistrict(s, h);
            int flat = HousingRules.FindFlat(s, ld.Free, s.Seed, id, day, hh.Size[h], Math.Max(1, disposable), hh.Class[h], hh.District[h], work, out int building);
            if (flat >= 0)
            {
                int oldDistrict = hh.District[h];
                int oldBuilding = hh.Building[h], oldUnit = hh.Unit[h];
                HousingRules.MoveInto(s, h, flat, building, day);
                if (oldBuilding >= 0 && oldDistrict < ld.Free.Length)
                {
                    ld.Free[oldDistrict].Add(s.FlatFirst[oldBuilding] + oldUnit);   // the flat it left is free for the next
                }
                if (oldDistrict != hh.District[h] && hh.District[h] < s.Figures.Length)
                {
                    s.Figures[hh.District[h]].MovedIn++;
                    if (oldDistrict < s.Figures.Length)
                    {
                        s.Figures[oldDistrict].MovedOut++;
                    }
                }
                return false;
            }
            if (wants)
            {
                if (hh.District[h] < s.WantFlats.Length)
                {
                    s.WantFlats[hh.District[h]]++;
                }
                if (disposable > 0 && hh.Rent[h] * 1000L / disposable > PopulationCatalogue.RentBurdenLeave
                    || PopDraw.Mille(s.Seed, PopDraw.Priced, id, month, HousingRules.WantLeave(s, hh.District[h])))
                {
                    LeaveFreeing(s, ld, h, day);   // nothing to be had here: it takes a flat in the region, surely past bearing its rent, more often the dearer the district
                    return true;
                }
                hh.Flags[h] |= HouseholdFlags.WantsMove;
            }
            return false;
        }

        // A household leaves and its flat goes on the month's free list at once, so this month's movers and arrivals can take it.
        private static void LeaveFreeing(PopulationState s, LifeRules.LifeDay ld, int h, int day)
        {
            int b = s.Households.Building[h], unit = s.Households.Unit[h], d = s.Households.District[h];
            Leave(s, h, day);
            if (b >= 0 && unit >= 0 && b < s.FlatFirst.Length && d < ld.Free.Length && ld.Free[d] != null && s.Occupant[s.FlatFirst[b] + unit] == -1)
            {
                ld.Free[d].Add(s.FlatFirst[b] + unit);
            }
        }

        // True when a child under 18 lives in the household.
        private static bool HasChild(PopulationState s, int h, int day)
        {
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                if (s.Age(p, day) < PopulationCatalogue.AdultLeast)
                {
                    return true;
                }
            }
            return false;
        }

        // The head's age (the household's first member), 0 for none.
        private static int HeadAge(PopulationState s, int h, int day)
        {
            int head = s.Households.First[h];
            return head >= 0 ? s.Age(head, day) : 0;
        }

        // The youngest member's age when every member is past retirement, else -1: an older household with nobody of working age or younger at home.
        private static int YoungestIfRetired(PopulationState s, int h, int day)
        {
            int youngest = int.MaxValue;
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                int age = s.Age(p, day);
                if (age < PopulationCatalogue.RetireAge)
                {
                    return -1;
                }
                youngest = Math.Min(youngest, age);
            }
            return youngest == int.MaxValue ? -1 : youngest;
        }

        private static bool HasLinked(PopulationState s, int h)
        {
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                if ((s.People.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Every adult of the household who could work has been out of work longer than the region's patience.
        private static bool LongOutOfWork(PopulationState s, int h, int day)
        {
            int limit = PopulationCatalogue.UnemployedLeaveDays * Math.Max(1, s.YearDays) / 365;
            bool any = false;
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                var status = (Status)s.People.Status[p];
                if (status == Status.Worker)
                {
                    return false;
                }
                if (status == Status.Unemployed)
                {
                    if (day - s.People.Since[p] < limit)
                    {
                        return false;
                    }
                    any = true;
                }
            }
            return any;
        }

        /// <summary>A household leaves for the region: its people and its flat.</summary>
        public static void Leave(PopulationState s, int h, int day)
        {
            HouseholdTable hh = s.Households;
            int d = hh.District[h];
            while (hh.First[h] >= 0)
            {
                int p = hh.First[h];
                if (s.People.Job[p] >= 0)
                {
                    LabourRules.Lose(s, p, day, false);
                }
                int partner = s.People.Partner[p];
                if (partner >= 0 && s.People.Alive(partner) && s.People.Household[partner] != h)
                {
                    s.People.Partner[partner] = -1;
                }
                GenerateRules.Leave(s, p);
                s.People.Remove(p);
            }
            LifeRules.Dissolve(s, h);
            s.Region.MovedOut++;
            if (d < s.Figures.Length)
            {
                s.Figures[d].MovedOut++;
            }
        }

        /// <summary>
        /// Households from the region this month: while the city's vacancies are open and flats free past the floor the city keeps, some come and
        /// take the free flats (drawn as the city's own were, their workers taking the open jobs); those that would come with no flat for them are
        /// counted in the districts they wanted.
        /// </summary>
        public static int Arrivals(PopulationState s, LifeRules.LifeDay ld, int day, int vacancies, List<int>[] open)
        {
            long wanting = (long)vacancies * PopulationCatalogue.MigrantsPerVacancy / 1000 / 100;
            int came = 0;
            for (int d = 0; d < s.Districts.Count && d < ld.Free.Length; d++)
            {
                if (s.Districts[d].Villas || ld.Free[d] == null)
                {
                    continue;   // a villa changes hands as the game sells it, not to the region's households
                }
                int floor = (int)((long)(d < s.Figures.Length ? s.Figures[d].Flats : 0) * PopulationCatalogue.TargetVacancy / 1000);
                int spare = ld.Free[d].Count - floor;
                for (int n = 0; n < (long)spare * PopulationCatalogue.ArrivalShare / 1000 * HousingRules.RegionPull(s, d) / 1000; n++)
                {
                    came += Arrive(s, ld, d, day, 100000 + d * 1000 + n, open) ? 1 : 0;
                }
            }
            for (int n = 0; n < wanting; n++)
            {
                int d = PickDistrict(s, day, n);
                if (d < 0)
                {
                    break;
                }
                List<int> free = d < ld.Free.Length ? ld.Free[d] : null;
                // the free share a market keeps: flats changing hands, and room for the city's own households
                int floor = Math.Max(1, (int)((long)(d < s.Figures.Length ? s.Figures[d].Flats : 0) * PopulationCatalogue.TargetVacancy / 1000));
                if (free == null || free.Count <= floor)
                {
                    if (d < s.Figures.Length)
                    {
                        s.Figures[d].MigrantsWanting++;
                    }
                    s.Region.Wanting++;
                    continue;
                }
                if (!PopDraw.Mille(s.Seed, PopDraw.Priced, n, day, HousingRules.RegionPull(s, d)))
                {
                    continue;   // priced out: it stays in the region, where the rent is the game's own
                }
                came += Arrive(s, ld, d, day, n, open) ? 1 : 0;
            }
            return came;
        }

        // A household from the region into one of a district's free flats, its workers into the open jobs, its money set.
        private static bool Arrive(PopulationState s, LifeRules.LifeDay ld, int d, int day, int n, List<int>[] open)
        {
            List<int> free = ld.Free[d];
            if (free.Count == 0)
            {
                return false;
            }
            int at = PopDraw.Range(s.Seed, PopDraw.Migrate, n, day, free.Count);
            int flat = free[at];
            free[at] = free[free.Count - 1];
            free.RemoveAt(free.Count - 1);
            int b = HousingRules.BuildingOfFlat(s, flat);
            int h = b >= 0 ? GenerateRules.Arrive(s, b, flat - s.FlatFirst[b], day) : -1;
            if (h < 0)
            {
                return false;
            }
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                if (s.People.Status[p] == (byte)Status.Unemployed)
                {
                    int e = LabourRules.Match(s, p, open, day);
                    if (e >= 0)
                    {
                        LabourRules.Hire(s, p, e, day);
                    }
                }
            }
            GenerateRules.Finish(s, h);
            s.Region.MovedIn++;
            if (d < s.Figures.Length)
            {
                s.Figures[d].MovedIn++;
            }
            return true;
        }

        // The district a household from the region heads for: weighed by its flats (where the homes are), drawn by the day.
        private static int PickDistrict(PopulationState s, int day, int n)
        {
            int districts = Math.Min(s.Districts.Count, s.Figures.Length);
            long sum = 0;
            for (int d = 0; d < districts; d++)
            {
                sum += s.Districts[d].Villas ? 0 : s.Figures[d].Flats;
            }
            if (sum <= 0)
            {
                return -1;
            }
            long roll = (long)(PopDraw.Hash(s.Seed, PopDraw.Migrate, n + 7919, day) % (ulong)sum);
            for (int d = 0; d < districts; d++)
            {
                long w = s.Districts[d].Villas ? 0 : s.Figures[d].Flats;
                if (roll < w)
                {
                    return d;
                }
                roll -= w;
            }
            return districts - 1;
        }
    }
}
