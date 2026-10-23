using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A site at work: its pace from the crews on it (their people of each trade against what the stage needs, their skill
    /// and morale), the plant on it and the materials delivered; the inspector's visits and verdicts; deliveries and short
    /// ones; a crew's morale, strikes and walking off; theft at night and a storm's damage. Every draw is seeded by the save's
    /// seed with what it is about, so it comes out the same however often it is asked. Game-free.
    /// </summary>
    public static class SiteRules
    {
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
