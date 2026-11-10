using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// The city's people fall ill by the same seeded rules, in the living population's own terms: a person's day is one hash of the save's seed, the person and the day, so
    /// nothing is kept for any of them and a load changes nothing. A case costs its household the lost days' pay an employer does not make up and the part of the care no
    /// plan pays, out of its savings and then onto its debt (which Giving's food bank and a friend's ask can meet). The flu's wave lifts everybody's chance in midwinter, and
    /// what is catching passes to housemates once. The player's own staff are drawn here too. Game-free.
    /// COST: the day's pass is a few operations a person and is run in slices of <see cref="HealthCatalogue.PassSlice"/>; nothing is allocated.
    /// </summary>
    public static class HealthPeople
    {
        // The air of today and the two days before, set once a slice: the wave and the season each housemate's own draw reads.
        private static readonly int[] AirWave = new int[3];
        private static readonly int[] AirSeason = new int[3];

        public static int AgeOf(PopulationState s, int p, int day) => Math.Max(0, (day - s.People.Birth[p]) / Math.Max(1, s.YearDays));

        /// <summary>
        /// What a person comes down with on a day by their own draw, or nothing: a cold by the season, the flu by the wave, food poisoning, and for adults a bad back, for
        /// those on long hours exhaustion, and now and then a sprain or a broken arm.
        /// </summary>
        public static Condition Primary(int seed, int id, int day, int age, bool adult, bool longHours, int wave, int season)
        {
            int r = HealthRules.Roll(seed, id, day, 1, 100000);
            int at = HealthCatalogue.ColdADay[HealthCatalogue.Clamp(season, 0, 3)] * (age < HealthCatalogue.ChildAge ? HealthCatalogue.ChildColdShare : 1000) / 1000;
            if (r < at)
            {
                return Condition.Cold;
            }
            at += (HealthCatalogue.FluADay + (int)((long)HealthCatalogue.WavePeak * wave / 1000)) * (age >= HealthCatalogue.ElderAge ? HealthCatalogue.ElderFluShare : 1000) / 1000;
            if (r < at)
            {
                return Condition.Flu;
            }
            at += HealthCatalogue.FoodADay;
            if (r < at)
            {
                return Condition.FoodPoisoning;
            }
            if (adult)
            {
                at += HealthCatalogue.BackADay;
                if (r < at)
                {
                    return Condition.BackPain;
                }
                at += longHours ? HealthCatalogue.ExhaustionADay : 0;
                if (r < at)
                {
                    return Condition.Exhaustion;
                }
            }
            at += HealthCatalogue.SprainADay;
            if (r < at)
            {
                return Condition.Sprain;
            }
            return r < at + HealthCatalogue.ArmADay ? Condition.BrokenArm : Condition.None;
        }

        private static Condition PrimaryOf(PopulationState s, int seed, int p, int day, int wave, int season)
        {
            PersonTable pt = s.People;
            int age = AgeOf(s, p, day);
            return Primary(seed, pt.Id[p], day, age, age >= 18, pt.Status[p] == (byte)Status.Worker && pt.Hours[p] >= HealthCatalogue.LongHours, wave, season);
        }

        /// <summary>
        /// What a person comes down with today: their own draw, or what a housemate came down with yesterday or the day before if it is catching and their second draw says
        /// so. A housemate's own draw is all that is passed on, so an illness goes one step through a home and no further.
        /// </summary>
        private static Condition Onset(PopulationState s, int seed, int p, int day)
        {
            Condition own = PrimaryOf(s, seed, p, day, AirWave[0], AirSeason[0]);
            if (own != Condition.None)
            {
                return own;
            }
            PersonTable pt = s.People;
            int h = pt.Household[p];
            if (h < 0 || s.Households.Size[h] < 2 || HealthRules.Roll(seed, pt.Id[p], day, 2, 100000) >= HealthCatalogue.HouseContact)
            {
                return Condition.None;
            }
            int guard = 0;
            for (int m = s.Households.First[h]; m >= 0 && guard < 16; m = pt.Next[m], guard++)
            {
                if (m == p || pt.Id[m] == 0)
                {
                    continue;
                }
                for (int back = 1; back <= 2; back++)
                {
                    Condition theirs = PrimaryOf(s, seed, m, day - back, AirWave[back], AirSeason[back]);
                    if (HealthCatalogue.Is(HealthCatalogue.Infectious, theirs))
                    {
                        return theirs;
                    }
                }
            }
            return Condition.None;
        }

        /// <summary>True if a case needs the hospital: a broken arm always, a few of the flu, food poisoning and exhaustion, three times as often for the old.</summary>
        public static bool SeriousFor(int seed, int id, int day, Condition c, int age)
        {
            if (c == Condition.BrokenArm)
            {
                return true;
            }
            int share = c == Condition.Flu ? HealthCatalogue.SeriousFlu : c == Condition.FoodPoisoning ? HealthCatalogue.SeriousFood
                : c == Condition.Exhaustion ? HealthCatalogue.SeriousExhaustion : 0;
            share = age >= HealthCatalogue.ElderAge ? share * HealthCatalogue.ElderSeriousShare / 1000 : share;
            return share > 0 && HealthRules.Roll(seed, id, day, 3, 1000) < share;
        }

        /// <summary>True if a household has a health plan: anybody in a full job has the employer's, and otherwise a share of households by class do, the same ones every day.</summary>
        public static bool Insured(PopulationState s, int seed, int h)
        {
            PersonTable pt = s.People;
            int guard = 0;
            for (int m = s.Households.First[h]; m >= 0 && guard < 16; m = pt.Next[m], guard++)
            {
                if (pt.Id[m] != 0 && pt.Status[m] == (byte)Status.Worker && pt.Hours[m] >= HealthCatalogue.FullJobHours)
                {
                    return true;
                }
            }
            int cls = HealthCatalogue.Clamp(s.Households.Class[h], 0, HealthCatalogue.InsuredShare.Length - 1);
            return HealthRules.Roll(seed, s.Households.Id[h], 0, 9, 1000) < HealthCatalogue.InsuredShare[cls];
        }

        /// <summary>The care a case costs at today's prices: nothing for a cold, the doctor and a prescription for the rest, and the hospital on top when it is serious.</summary>
        public static long CareBill(Condition c, bool serious, int level)
        {
            if (c == Condition.None || c == Condition.Cold)
            {
                return 0;
            }
            return HealthRules.At(HealthCatalogue.DoctorFee, level) + HealthRules.At(HealthCatalogue.PrescriptionFee, level)
                   + (serious ? HealthCover.StayBill(HealthRules.StayDays(c), false, level) : 0L);
        }

        /// <summary>
        /// A case lands on a person's household: the lost days' pay the employer does not make up, and the care a plan does not pay (the standard plan's shares), out of the
        /// savings first and then onto the debt, which no case raises by more than <see cref="HealthCatalogue.NpcDebtMost"/> at today's prices. Every dollar is counted in
        /// the month's sums. Returns the days it runs.
        /// </summary>
        public static int Apply(PopulationState s, PopHealth o, int seed, int p, int day, Condition c, int level)
        {
            PersonTable pt = s.People;
            HouseholdTable hh = s.Households;
            int h = pt.Household[p], id = pt.Id[p], age = AgeOf(s, p, day);
            int course = HealthRules.Course(seed, id, day, c);
            bool serious = SeriousFor(seed, id, day, c, age);
            int lostDays = 0;
            long lost = 0;
            if (pt.Status[p] == (byte)Status.Worker && pt.Job[p] >= 0)
            {
                lostDays = Math.Min(course, HealthCatalogue.LostDaysMost);
                long dayPay = (long)pt.Pay[p] * pt.Hours[p] / 700;   // cents an hour times the week's hours over seven days, in whole money
                lost = dayPay * lostDays * (1000 - HealthCatalogue.NpcSickPay) / 1000;
            }
            long bill = CareBill(c, serious, level);
            long own = bill;
            if (bill > 0 && Insured(s, seed, h))
            {
                long doctor = HealthRules.At(HealthCatalogue.DoctorFee, level) + HealthRules.At(HealthCatalogue.PrescriptionFee, level);
                own = bill - HealthCover.Claim(Plan.Standard, Care.Doctor, doctor, 0, level) - HealthCover.Claim(Plan.Standard, Care.Hospital, bill - doctor, 0, level);
            }
            long due = lost + own;
            long fromSavings = Math.Min(Math.Max(0, hh.Savings[h]), due);
            long toDebt = Math.Min(due - fromSavings, Math.Min(HealthRules.At(HealthCatalogue.NpcDebtMost, level), (long)int.MaxValue / 2 - hh.Debt[h]));
            toDebt = Math.Max(0, toDebt);
            hh.Savings[h] -= (int)fromSavings;
            hh.Debt[h] += (int)toDebt;
            if (serious && hh.Content[h] > 0)
            {
                hh.Content[h]--;
            }
            // What neither the savings nor the debt's bound could carry is not charged: the sums count what really left the household.
            long paid = fromSavings + toDebt;
            long lostPaid = Math.Min(lost, paid);
            o.Cases++;
            o.Serious += serious ? 1 : 0;
            o.LostDays += lostDays;
            o.LostWages += lostPaid;
            o.Bills += paid - lostPaid;
            o.Debt += toDebt;
            o.IllNow++;
            o.Ends[(day + course) % o.Ends.Length]++;
            return course;
        }

        /// <summary>The day turns for the people: those whose case ends on a day gone by are well again, and a new month closes the month's sums.</summary>
        public static void Begin(PopHealth o, int day, int month)
        {
            int from = Math.Max(o.Day + 1, day - o.Ends.Length + 1);
            for (int d = from; d <= day; d++)
            {
                int at = d % o.Ends.Length;
                o.IllNow = Math.Max(0, o.IllNow - o.Ends[at]);
                o.Ends[at] = 0;
            }
            if (o.Month != month)
            {
                if (o.Month >= 0)
                {
                    o.LastCases = o.Cases;
                    o.LastSerious = o.Serious;
                    o.LastLostDays = o.LostDays;
                    o.LastLostWages = o.LostWages;
                    o.LastBills = o.Bills;
                    o.LastDebt = o.Debt;
                }
                o.Cases = o.Serious = o.LostDays = 0;
                o.LostWages = o.Bills = o.Debt = 0;
                o.Month = month;
            }
            o.Day = day;
            o.Cursor = 0;
        }

        /// <summary>
        /// A slice of the day's pass: so many people from the cursor, each drawn once. The player, the player's own household and households away are left out (the player's
        /// people fall ill by the same draw, read by <see cref="IllAtHome"/>, but their money is the player's). The cursor is in the save, so a pass a save or a load cut in
        /// two goes on where it was. Returns true when the day is done.
        /// </summary>
        public static bool Slice(PopulationState s, PopHealth o, int seed, int level, int count)
        {
            if (o.Cursor < 0)
            {
                return true;
            }
            int day = o.Day, year = Math.Max(1, s.YearDays);
            for (int back = 0; back < 3; back++)
            {
                AirWave[back] = HealthRules.Wave(seed, day - back, year);
                AirSeason[back] = CalendarRules.Season(day - back, year);
            }
            PersonTable pt = s.People;
            HouseholdTable hh = s.Households;
            int end = Math.Min(pt.Count, o.Cursor + Math.Max(1, count));
            for (int p = o.Cursor; p < end; p++)
            {
                if (pt.Id[p] == 0 || (pt.Flags[p] & PersonFlags.Frozen) != 0)
                {
                    continue;
                }
                int h = pt.Household[p];
                if (h < 0 || !hh.Alive(h) || (hh.Flags[h] & (HouseholdFlags.Players | HouseholdFlags.Away)) != 0)
                {
                    continue;
                }
                Condition c = Onset(s, seed, p, day);
                if (c != Condition.None)
                {
                    Apply(s, o, seed, p, day, c, level);
                }
            }
            o.Cursor = end >= pt.Count ? -1 : end;
            return o.Cursor < 0;
        }

        /// <summary>
        /// The people ill with something catching in the player's own home today (a partner, children, anybody the population holds in the player's household), by their own
        /// draws of the last days. One look over the households a day.
        /// </summary>
        public static int IllAtHome(PopulationState s, int seed, int day)
        {
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            int ill = 0, year = Math.Max(1, s.YearDays);
            for (int h = 0; h < hh.Count; h++)
            {
                if (hh.Id[h] == 0 || (hh.Flags[h] & HouseholdFlags.Players) == 0)
                {
                    continue;
                }
                int guard = 0;
                for (int m = hh.First[h]; m >= 0 && guard < 16; m = pt.Next[m], guard++)
                {
                    if (pt.Id[m] == 0 || (pt.Flags[m] & PersonFlags.Frozen) != 0)
                    {
                        continue;
                    }
                    for (int back = 0; back < 3; back++)
                    {
                        int d = day - back;
                        Condition c = PrimaryOf(s, seed, m, d, HealthRules.Wave(seed, d, year), CalendarRules.Season(d, year));
                        if (HealthCatalogue.Is(HealthCatalogue.Infectious, c) && HealthRules.Course(seed, pt.Id[m], d, c) > back)
                        {
                            ill++;
                            break;
                        }
                    }
                }
            }
            return ill;
        }

        // ---- the player's own staff

        /// <summary>
        /// The days one of the player's staff is off with what the wave or their colleagues gave them, or 0 for none: only the wave and colleagues off at the same place add
        /// to the game's own sick days, the game's health plan cuts the chance by a step for each of its levels (0 none, 1 bronze, 2 silver, 3 gold) and from silver takes a
        /// day off the course.
        /// </summary>
        public static int StaffCourse(int seed, int who, int day, int wave, int colleaguesOff, int planTier)
        {
            planTier = HealthCatalogue.Clamp(planTier, 0, 3);
            long chance = (long)HealthCatalogue.StaffWavePeak * wave / 1000
                          + (long)HealthCatalogue.StaffContact * HealthCatalogue.Clamp(colleaguesOff, 0, HealthCatalogue.StaffContactMost);
            chance = chance * (1000 - HealthCatalogue.StaffPlanCut * planTier) / 1000;
            if (chance <= 0 || HealthRules.Roll(seed, who, day, 41, 100000) >= chance)
            {
                return 0;
            }
            int course = 1 + HealthRules.Roll(seed, who, day, 42, HealthCatalogue.StaffCourseMost);
            return planTier >= 2 ? Math.Max(1, course - 1) : course;
        }

        /// <summary>The first day off: tomorrow, or the day after where the firm pays no sick pay (they come in ill the first day, and pass it on).</summary>
        public static int StaffFrom(int day, int sickPolicy) => day + (sickPolicy <= 0 ? 2 : 1);
    }
}
