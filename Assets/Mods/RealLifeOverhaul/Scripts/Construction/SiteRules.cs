using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a working hour of a project's site comes to (<see cref="SiteRules.Hour"/>), for the director to act on.</summary>
    public enum SiteCall
    {
        Worked = 0,   // the stage went on: the hour's hours done
        Short = 1,    // nothing done: the stage's materials allow no more until a delivery
        Idle = 2,     // nothing done: nobody at work on it
        Rework = 3,   // the hour went on putting right a storm's damage or what the inspector failed
        Due = 4,      // held at its stage's end: the inspection that ends the stage falls due now
        Held = 5,     // held at its stage's end for the inspector: due and not booked yet, or booked for later
        Visit = 6,    // held at its stage's end: the inspector's booked visit is this hour
        Redo = 7,     // what the inspector failed is put right: the inspection falls due again
        Done = 8      // the last inspection has passed: the works are finished, the hour's hours taking the site to its end
    }

    /// <summary>A working hour of a project's site: what it comes to, the inspection that is about (-1 for none), the site's hours done and the rework left after it.</summary>
    public struct SiteHour
    {
        public SiteCall Call;
        public int Inspection;
        public double Hours;
        public int Rework;    // tenths of an hour
    }

    /// <summary>
    /// A site at work: its pace from the crews on it (their people of each trade against what the stage needs, their skill
    /// and morale), the plant on it and the materials delivered; a working hour of it and its schedule; the inspector's visits
    /// and verdicts; deliveries and short ones; a crew's morale, strikes and walking off; theft at night and a storm's damage.
    /// Every draw is seeded by the save's seed with what it is about, so it comes out the same however often it is asked.
    /// Game-free.
    /// </summary>
    public static class SiteRules
    {
        /// <summary>
        /// One working hour of a project's site in the stage its works are in (PlansRules.WorkingStage): at the stage's end, held
        /// for its inspection (falling due, waiting, the inspector's visit from ten on the day booked, or a failure put right at
        /// the crews' pace before it falls due again), done once the last has passed; otherwise a storm's damage put right, or the
        /// stage on at the pace (thousandths of the city's) as far as its materials allow. <paramref name="held"/> is the state of
        /// the inspection that ends the stage (Passed for none); <paramref name="day"/> and <paramref name="hour"/> are the hour's
        /// own, which the programme may work some time after it (a sleep is caught up at once on waking). Nothing allocated.
        /// </summary>
        public static SiteHour Hour(ProjectKind kind, WorkStage stage, double done, double needed, double hours, int pace, double share, InspectionState held,
                                    int heldDay, int rework, int day, int hour)
        {
            var result = new SiteHour { Call = SiteCall.Worked, Inspection = -1, Rework = Math.Max(0, rework) };
            if (needed <= 0.0)
            {
                result.Hours = hours;
                return result;
            }
            PlansRules.Span(kind, stage, out double from, out double to);
            int inspection = PlansRules.InspectionAfter(kind, stage);
            bool last = stage == WorkStage.Envelope;
            double end = last ? BuildCatalogue.LastStageEnd : to;
            int put = (int)Math.Round(hours * Math.Max(0, pace) / 100.0);   // tenths of an hour the crews put in
            if (done / needed >= end - PlansRules.EndSlack)
            {
                if (inspection >= 0 && held != InspectionState.Passed)
                {
                    result.Inspection = inspection;
                    switch (held)
                    {
                        case InspectionState.NotDue:
                            result.Call = SiteCall.Due;
                            break;
                        case InspectionState.Booked:
                            result.Call = Visits(day, hour, heldDay) ? SiteCall.Visit : SiteCall.Held;
                            break;
                        case InspectionState.Failed:
                            // Put right where the failure held it, at the stage's end, and then the inspector is asked again (passed
                            // over here, a failure held the site at its end for good).
                            result.Rework = Math.Max(0, result.Rework - put);
                            result.Call = put <= 0 ? SiteCall.Held : result.Rework == 0 ? SiteCall.Redo : SiteCall.Rework;
                            break;
                        default:
                            result.Call = SiteCall.Held;   // due: the company or its site manager books the inspector
                            break;
                    }
                    return result;
                }
                if (last)
                {
                    result.Call = SiteCall.Done;
                    result.Hours = Math.Max(0.0, needed - done) + 0.01;
                    return result;
                }
            }
            if (pace <= 0)
            {
                result.Call = SiteCall.Idle;
                return result;
            }
            if (result.Rework > 0)
            {
                result.Rework = Math.Max(0, result.Rework - put);
                result.Call = result.Rework == 0 && inspection >= 0 && held == InspectionState.Failed ? SiteCall.Redo : SiteCall.Rework;
                return result;
            }
            double cap = Math.Min(end, from + (to - from) * Math.Max(0.0, Math.Min(1.0, share)));
            double allowed = cap * needed - done;
            if (allowed <= 1e-6)
            {
                result.Call = SiteCall.Short;
                return result;
            }
            result.Hours = Math.Min(hours * pace / 1000.0, allowed);
            return result;
        }

        /// <summary>True once the inspector's visit booked for a day has come: from ten on that day, or any hour after it.</summary>
        public static bool Visits(int day, int hour, int bookedDay)
        {
            return day > bookedDay || (day == bookedDay && hour >= BuildCatalogue.InspectorHour);
        }

        /// <summary>True in an hour the city's programme works its sites under the permits policy in force (-1 to 1): a working day, from its opening hour to its closing one.</summary>
        public static bool WorkingHour(int day, int hour, int permits)
        {
            return WorkingDay(day) && hour >= CivicCatalogue.SiteOpens(permits) && hour < CivicCatalogue.SiteShuts(permits);
        }

        /// <summary>The site's hours a working day gets through at the city's own pace under the permits policy in force: its working hours at the programme's speed.</summary>
        public static double DayHours(int permits)
        {
            return (CivicCatalogue.SiteShuts(permits) - CivicCatalogue.SiteOpens(permits)) * (double)CivicCatalogue.SiteSpeed(permits);
        }

        /// <summary>
        /// A site's schedule: the day its works are handed over, from a day's hour at a pace (thousandths of the city's), run hour
        /// by hour by the very rules its site keeps (the programme's working hours and speed under the permits policy,
        /// <see cref="PlansRules.WorkingStage"/> and <see cref="Hour"/>), each inspection booked the hour it falls due and passed at
        /// the inspector's first visit. A builder's quote, its contract's due day and its site come out of this one rule, so only
        /// a slower pace, a failed inspection, rain or a complaint's short hours make a site late. Its hours kept in floats, as the
        /// programme keeps them. At most <see cref="BuildCatalogue.ScheduleMostDays"/> days are run; nothing allocated.
        /// </summary>
        public static int HandoverDay(ProjectKind kind, int hours, int pace, int permits, int day, int hour)
        {
            float needed = Math.Max(1, hours), done = 0f, speed = CivicCatalogue.SiteSpeed(permits);
            int passed = 0, booked = -1, bookedDay = 0;
            long at = (long)day * 24 + Math.Max(0, hour), most = at + BuildCatalogue.ScheduleMostDays * 24L;
            for (; at < most; at++)
            {
                int d = (int)(at / 24), h = (int)(at % 24);
                if (!WorkingHour(d, h, permits))
                {
                    continue;
                }
                WorkStage stage = PlansRules.WorkingStage(kind, done / (double)needed, passed);
                int inspection = PlansRules.InspectionAfter(kind, stage);
                InspectionState held = inspection < 0 || (passed & (1 << inspection)) != 0 ? InspectionState.Passed
                    : inspection == booked ? InspectionState.Booked : InspectionState.NotDue;
                SiteHour work = Hour(kind, stage, done, needed, speed, pace, 1.0, held, bookedDay, 0, d, h);
                if (work.Call == SiteCall.Due)
                {
                    booked = work.Inspection;
                    bookedDay = InspectorDay(d, h);
                }
                else if (work.Call == SiteCall.Visit)
                {
                    passed |= 1 << work.Inspection;
                    booked = -1;
                }
                done += (float)work.Hours;
                if (work.Call == SiteCall.Done || done >= needed)
                {
                    return d;
                }
            }
            return (int)(most / 24);
        }

        /// <summary>The working days from one day to another, both counted; none when the second comes before the first.</summary>
        public static int WorkingDays(int from, int to)
        {
            int n = 0;
            for (int d = from; d <= to; d++)
            {
                n += WorkingDay(d) ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// The pace of a stage in thousandths of its usual: each trade it needs counted up to 1.8 times its need, halved with
        /// a trade missing altogether, by the crews' skill (0 to 100) and morale (0 to 100), slower for each piece of plant
        /// missing (a tall frame without its crane hardly moves), and the speciality's bonus. Nobody on it: nothing.
        /// </summary>
        public static int Pace(int[] people, int[] need, int skill, int morale, int plantMissing, bool craneMissing, int specialPermille)
        {
            int needed = 0, have = 0, anyone = 0;
            bool missing = false;
            for (int t = 0; t < (int)Trade.Count; t++)
            {
                int p = people != null && t < people.Length ? Math.Max(0, people[t]) : 0;
                int n = need != null && t < need.Length ? Math.Max(0, need[t]) : 0;
                anyone += p;
                if (n <= 0)
                {
                    continue;
                }
                needed += n;
                have += Math.Min(p * 10, n * 18);   // in tenths of a person: a trade counts up to 1.8 times its need
                missing |= p == 0;
            }
            if (anyone == 0)
            {
                return 0;
            }
            long pace = needed == 0 ? 1000L : have * 100L / needed;   // tenths over people: thousandths
            pace = Math.Min(pace, BuildCatalogue.PaceMostPermille);
            if (missing)
            {
                pace = pace * BuildCatalogue.TradeMissingPermille / 1000L;
            }
            pace = pace * (BuildCatalogue.SkillPacePermille + BuildCatalogue.Clamp(skill, 0, 100) * 5L) / 1000L;
            pace = pace * (BuildCatalogue.MoralePacePermille + BuildCatalogue.Clamp(morale, 0, 100) * 4L) / 1000L;
            for (int i = 0; i < plantMissing; i++)
            {
                pace = pace * BuildCatalogue.PlantMissingPermille / 1000L;
            }
            if (craneMissing)
            {
                pace = pace * BuildCatalogue.CraneMissingPermille / 1000L;
            }
            pace = pace * (1000L + Math.Max(0, specialPermille)) / 1000L;
            return (int)Math.Max(BuildCatalogue.PaceLeastPermille, Math.Min(BuildCatalogue.PaceMostPermille, pace));
        }

        /// <summary>The share of a stage's work the materials on site allow: the least share of any material it needs (1 for none).</summary>
        public static double MaterialsShare(int[] have, int[] need)
        {
            double share = 1.0;
            for (int m = 0; m < (int)Material.Count; m++)
            {
                int n = need != null && m < need.Length ? need[m] : 0;
                if (n <= 0)
                {
                    continue;
                }
                int h = have != null && m < have.Length ? Math.Max(0, have[m]) : 0;
                share = Math.Min(share, Math.Min(1.0, h / (double)n));
            }
            return share;
        }

        /// <summary>True when every material a stage needs is on site.</summary>
        public static bool Supplied(int[] have, int[] need)
        {
            return MaterialsShare(have, need) >= 0.999;
        }

        /// <summary>The work's quality the inspector sees: the crews' skill most, the site manager's (50 without one) and the plans'.</summary>
        public static int WorkQuality(int crewSkill, int managerSkill, int plansQuality)
        {
            int q = (BuildCatalogue.Clamp(crewSkill, 0, 100) * 55 + BuildCatalogue.Clamp(managerSkill, 0, 100) * 25 + BuildCatalogue.Clamp(plansQuality, 0, 100) * 20) / 100;
            return BuildCatalogue.Clamp(q, 0, 100);
        }

        /// <summary>The odds, in thousandths, that an inspection passes: by the work's quality, less when rushed, more when the player walked the site, more with a good site manager.</summary>
        public static int PassOdds(int workQuality, bool rushed, bool visited, bool manager)
        {
            int odds = BuildCatalogue.PassBasePermille + BuildCatalogue.Clamp(workQuality, 0, 100) * BuildCatalogue.PassPerQuality - (rushed ? BuildCatalogue.PassRushPermille : 0)
                       + (visited ? BuildCatalogue.PassVisitPermille : 0) + (manager ? BuildCatalogue.PassManagerPermille : 0);
            return BuildCatalogue.Clamp(odds, 100, BuildCatalogue.PassMostPermille);
        }

        /// <summary>The inspector's verdict, from the project, the inspection and the try: the same verdict however often it is asked.</summary>
        public static bool Passes(int seed, int project, int inspection, int attempt, int odds)
        {
            BuildRandom random = BuildRandom.Of(seed, project * 7 + inspection, 61 + attempt * 13);
            return random.Range(0, 1000) < odds;
        }

        /// <summary>The day the inspector comes for a booking made on a day at an hour: the next working day, one later when booked late.</summary>
        public static int InspectorDay(int day, int hour)
        {
            return PlanningRules.AfterWorkingDays(day, hour >= BuildCatalogue.InspectLateHour ? 2 : 1);
        }

        /// <summary>The day an order from a merchant arrives: its working days on, one more when ordered late in the day.</summary>
        public static int DeliveryDay(int day, int hour, int merchant)
        {
            int m = BuildCatalogue.Clamp(merchant, 0, BuildCatalogue.MerchantCount - 1);
            return PlanningRules.AfterWorkingDays(day, BuildCatalogue.MerchantDays[m] + (hour >= BuildCatalogue.InspectLateHour ? 1 : 0));
        }

        /// <summary>The share of an order, in thousandths, its delivery brings: all of it, or less when the merchant is short.</summary>
        public static int Delivered(int seed, int project, int order, int merchant)
        {
            int m = BuildCatalogue.Clamp(merchant, 0, BuildCatalogue.MerchantCount - 1);
            BuildRandom random = BuildRandom.Of(seed, project * 101 + order, 67);
            return random.Chance(BuildCatalogue.MerchantShortPermille[m]) ? random.Range(BuildCatalogue.ShortLeastPermille, BuildCatalogue.ShortMostPermille + 1) : 1000;
        }

        /// <summary>Tenths of an hour of work a failed inspection takes to put right: a share of the stage's hours.</summary>
        public static int Rework(int stageHours)
        {
            return Math.Max(10, stageHours * BuildCatalogue.ReworkPermille / 100);
        }

        /// <summary>What a bribe to the inspector is: a share of the stage's materials, at least the floor at today's prices.</summary>
        public static int Bribe(long stageMaterials, int prices)
        {
            long least = (long)BuildCatalogue.BribeLeast * Math.Max(1, prices) / 1000L;
            return (int)Math.Min(int.MaxValue, Math.Max(least, stageMaterials * BuildCatalogue.BribePermille / 1000L));
        }

        /// <summary>True when a bribe comes out on a day of the days it may: a chance a day, from the project and the day.</summary>
        public static bool BribeOut(int seed, int project, int day)
        {
            BuildRandom random = BuildRandom.Of(seed, project, day * 3 + 71);
            return random.Chance(BuildCatalogue.BribeChancePermille);
        }

        /// <summary>A night's theft on a site with materials on it: whether it comes, which material, and the share taken.</summary>
        public static bool Theft(int seed, int project, int day, out int material, out int share)
        {
            BuildRandom random = BuildRandom.Of(seed, project, day * 5 + 73);
            material = random.Range(0, (int)Material.Count);
            share = random.Range(BuildCatalogue.TheftLeastPermille, BuildCatalogue.TheftMostPermille + 1);
            return random.Chance(BuildCatalogue.TheftPermille);
        }

        /// <summary>Tenths of an hour a storm's damage costs a frame: a share of its hours.</summary>
        public static int StormRework(int stageHours)
        {
            return Math.Max(10, stageHours * BuildCatalogue.StormReworkPermille / 100);
        }

        /// <summary>A crew's morale after a working day: down when not paid or idle for days, up a little when paid and at work.</summary>
        public static int Morale(int morale, bool paid, bool working, int idleDays)
        {
            int m = morale;
            if (!paid)
            {
                m -= BuildCatalogue.MoraleUnpaid;
            }
            else if (working)
            {
                m += BuildCatalogue.MoraleWorking;
            }
            if (idleDays >= BuildCatalogue.IdleFrom)
            {
                m -= BuildCatalogue.MoraleIdle;
            }
            return BuildCatalogue.Clamp(m, 0, 100);
        }

        /// <summary>True when a crew strikes: at or under the strike line for two days running and not striking already.</summary>
        public static bool Strikes(int lowDays, int strikeUntil, int day)
        {
            return lowDays >= 2 && strikeUntil < day;
        }

        /// <summary>The working days a strike lasts, by the crew and the day.</summary>
        public static int StrikeDays(int seed, int crew, int day)
        {
            BuildRandom random = BuildRandom.Of(seed, crew, day + 79);
            return random.Range(BuildCatalogue.StrikeLeast, BuildCatalogue.StrikeMost + 1);
        }

        /// <summary>True when a crew walks off: its morale this low.</summary>
        public static bool WalksOff(int morale)
        {
            return morale <= BuildCatalogue.LeaveAt;
        }

        /// <summary>True on a working day, Monday to Friday, as the city's sites work.</summary>
        public static bool WorkingDay(int day)
        {
            return LandRules.Weekday(day) < 5;
        }

        /// <summary>True when a stage went faster than the inspector likes.</summary>
        public static bool Rushed(int pacePermille)
        {
            return pacePermille > BuildCatalogue.RushFromPermille;
        }
    }
}
