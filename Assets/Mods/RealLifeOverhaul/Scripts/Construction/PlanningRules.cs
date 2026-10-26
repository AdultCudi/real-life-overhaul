using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Why the council refused, as bits: a requirement not met, a use not zoned, the objections.</summary>
    [Flags]
    public enum Refusal
    {
        None = 0,
        Exits = 1,
        Parking = 2,
        Height = 4,
        Engineer = 8,
        Zoning = 16,
        Objections = 32,
        Quality = 64
    }

    /// <summary>
    /// Planning permission from City Hall: the wait, the neighbours' objections, the decision and its conditions, an appeal
    /// to the court; the council's hearing of a zoning change; the contractor's licence. Every draw is seeded by the save's
    /// seed and the project, so a decision is the same however often it is asked. Game-free.
    /// </summary>
    public static class PlanningRules
    {
        /// <summary>Working days from an application to the decision: more for a bigger building.</summary>
        public static int WorkingDays(int area)
        {
            return Math.Min(BuildCatalogue.PlanningDaysMost, BuildCatalogue.PlanningDays + Math.Max(0, area) / BuildCatalogue.PlanningDaysArea);
        }

        /// <summary>The day so many working days after another (Monday to Friday count).</summary>
        public static int AfterWorkingDays(int day, int working)
        {
            int d = day;
            int left = Math.Max(0, working);
            while (left > 0)
            {
                d++;
                if (LandRules.Weekday(d) < 5)
                {
                    left--;
                }
            }
            return d;
        }

        /// <summary>
        /// How many neighbours object: more for a building taller than its street's (<paramref name="norm"/> storeys), for a use
        /// the lot is not zoned for, for parking it does not provide, and for a builder of little standing; fewer for good plans.
        /// </summary>
        public static int Objections(int seed, int project, int storeys, int norm, bool zoned, int parkingShort, int reputation, int quality)
        {
            double expected = 0.5 + Math.Max(0, storeys - norm) * 0.6 + (zoned ? 0.0 : 1.5) + Math.Max(0, parkingShort) * 0.01
                              + (reputation < 40 ? 0.5 : 0.0) - (quality >= 80 ? 0.4 : 0.0);
            expected = Math.Max(0.1, expected);
            // A count drawn as the city's sites draw theirs: a small Poisson walk from the project's own seed.
            BuildRandom random = BuildRandom.Of(seed, project, 41);
            double limit = Math.Exp(-expected), product = random.Value;
            int count = 0;
            while (product > limit && count < BuildCatalogue.ObjectionsMost)
            {
                count++;
                product *= random.Value;
            }
            return count;
        }

        /// <summary>
        /// The council's odds, in thousandths, of granting a plain application: the plans' quality and the company's standing,
        /// less for each objection and each requirement not met, more or less by the permits policy (-1 to 1).
        /// </summary>
        public static int GrantOdds(int quality, int reputation, int objections, int unmet, int permits)
        {
            int odds = BuildCatalogue.GrantBasePermille + (quality - 60) * 4 + (reputation - 50) * 2 - objections * BuildCatalogue.ObjectionPermille
                       - unmet * BuildCatalogue.UnmetPermille + permits * BuildCatalogue.PermitsPermille;
            return BuildCatalogue.Clamp(odds, BuildCatalogue.GrantLeastPermille, BuildCatalogue.GrantMostPermille);
        }

        /// <summary>
        /// The decision: refused outright for a use the lot is not zoned for or a building over the height limit; otherwise
        /// granted at the odds, drawn from the project's own seed and the application's number (a new application draws anew).
        /// The reasons say why a refusal came.
        /// </summary>
        public static bool Decide(int seed, int project, int application, bool zoned, int unmetReasons, int quality, int reputation, int objections, int permits, out Refusal why)
        {
            why = Refusal.None;
            if (!zoned)
            {
                why |= Refusal.Zoning;
            }
            if ((unmetReasons & 4) != 0)
            {
                why |= Refusal.Height;
            }
            if (why != Refusal.None)
            {
                return false;
            }
            int unmet = 0;
            for (int r = unmetReasons; r != 0; r >>= 1)
            {
                unmet += r & 1;
            }
            int odds = GrantOdds(quality, reputation, objections, unmet, permits);
            BuildRandom random = BuildRandom.Of(seed, project * 31 + application, 43);
            if (random.Range(0, 1000) < odds)
            {
                return true;
            }
            why = (Refusal)(unmetReasons & 11);
            if (objections >= BuildCatalogue.ConditionsFrom)
            {
                why |= Refusal.Objections;
            }
            if (quality < 60)
            {
                why |= Refusal.Quality;
            }
            if (why == Refusal.None)
            {
                why = Refusal.Objections;   // the council's own judgement: put as the street's
            }
            return false;
        }

        /// <summary>True when a permission comes with conditions: a contribution to the street, for an application many objected to.</summary>
        public static bool Conditions(int objections)
        {
            return objections >= BuildCatalogue.ConditionsFrom;
        }

        /// <summary>The contribution a condition asks: a share of the estimate.</summary>
        public static long Contribution(long estimate)
        {
            return Math.Max(0L, estimate) * BuildCatalogue.ContributionPermille / 1000L;
        }

        /// <summary>The day an appeal is heard: so many days on, by the project's own seed.</summary>
        public static int HearingDay(int seed, int project, int day)
        {
            BuildRandom random = BuildRandom.Of(seed, project, 47);
            return day + random.Range(BuildCatalogue.AppealDaysLeast, BuildCatalogue.AppealDaysMost + 1);
        }

        /// <summary>
        /// The court's odds, in thousandths, of allowing an appeal: better for plans that meet more of the requirements, with
        /// a lawyer, and for good plans; worse for many objections, and half as good against a refusal for zoning or height.
        /// </summary>
        public static int AppealOdds(int metShare, int quality, bool lawyer, int objections, Refusal why)
        {
            int odds = BuildCatalogue.AppealBasePermille + metShare * BuildCatalogue.AppealMetPermille / 1000 + (quality - 60) * 5 + (lawyer ? BuildCatalogue.AppealLawyerPermille : 0)
                       - objections * BuildCatalogue.AppealObjectionPermille;
            if ((why & (Refusal.Zoning | Refusal.Height)) != 0)
            {
                odds /= 2;
            }
            return BuildCatalogue.Clamp(odds, 30, 900);
        }

        /// <summary>The court's judgement on an appeal, from the project's own seed.</summary>
        public static bool Allowed(int seed, int project, int odds)
        {
            BuildRandom random = BuildRandom.Of(seed, project, 53);
            return random.Range(0, 1000) < odds;
        }

        /// <summary>The share of the four requirements met, in thousandths.</summary>
        public static int MetShare(int unmetReasons)
        {
            int unmet = 0;
            for (int r = unmetReasons & 15; r != 0; r >>= 1)
            {
                unmet += r & 1;
            }
            return (4 - unmet) * 250;
        }

        /// <summary>The council's vote on a change of zoning, from the lot and the use asked for.</summary>
        public static bool ZoneVote(int seed, string lot, BuildUse use, int odds)
        {
            BuildRandom random = BuildRandom.Of(seed, BuildRandom.Hash(lot), 59 + (int)use);
            return random.Range(0, 1000) < odds;
        }

        /// <summary>The licence's answer: refused while a conviction still counts against the applicant.</summary>
        public static LicenceState Licence(bool record)
        {
            return record ? LicenceState.Refused : LicenceState.Granted;
        }

        /// <summary>The last day a licence granted on a day holds: a game year of the save's length.</summary>
        public static int LicenceUntil(int granted, int daysPerYear)
        {
            return granted + Math.Max(8, daysPerYear) - 1;
        }

        /// <summary>
        /// True when a company may ask for a licence now: none held or asked for, one held that runs out within the reminder's
        /// days (a renewal), and a refusal's rest over.
        /// </summary>
        public static bool MayAsk(LicenceState state, int since, int until, int day)
        {
            switch (state)
            {
                case LicenceState.Applied:
                    return false;
                case LicenceState.Granted:
                    return until - day <= BuildCatalogue.LicenceRemind;
                case LicenceState.Refused:
                    return day - since >= BuildCatalogue.LicenceRefusedRest;
                default:
                    return true;
            }
        }
    }
}
