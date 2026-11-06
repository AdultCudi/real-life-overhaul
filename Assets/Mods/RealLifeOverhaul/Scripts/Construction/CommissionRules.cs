using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The player as a client who hires one of the city's eight builders to put up their building: each builder's quote (its
    /// bid as on a tender, the works priced off the plans' estimate), its days and its due day from the site's own schedule,
    /// the contract's money (a deposit, a payment at each inspection passed, the retention at the handover, a penalty for each
    /// day late), the pace its crews keep and the quality the inspector sees in its work. The quotes stand for the week, drawn
    /// from the save's seed, the project and the week, so every machine and every look gives the same. Game-free, nothing
    /// allocated but the caller's array.
    /// </summary>
    public static class CommissionRules
    {
        /// <summary>Every builder's bid for some works, into the caller's array (one for each of the city's builders); its length.</summary>
        public static int Bids(int seed, int salt, long value, int days, Speciality need, int[] busy, TenderBid[] into)
        {
            int count = Math.Min(BuildCatalogue.RivalCount, into != null ? into.Length : 0);
            for (int r = 0; r < count; r++)
            {
                into[r] = TenderRules.RivalBid(seed, salt, r, value, days, need, busy != null && r < busy.Length ? busy[r] : 0);
            }
            return count;
        }

        /// <summary>
        /// The working days a site's hours take at the city's own pace in the city's own working day (its hours and speed under
        /// the permits policy, -1 to 1): what each builder bids its days of works around. (A tender's ten hours a working day knew
        /// nothing of the code, so a strict one's shorter, slower days made every builder late.)
        /// </summary>
        public static int UsualDays(int hours, int permits)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(1, hours) / Math.Max(0.1, SiteRules.DayHours(permits)) - 1e-9));
        }

        /// <summary>The pace (thousandths of the city's) a builder's bid needs: the site's usual days of works got through in the days it bid.</summary>
        public static int Promised(int usualDays, int bidDays)
        {
            return BuildCatalogue.Clamp(Math.Max(1, usualDays) * 1000 / Math.Max(1, bidDays), BuildCatalogue.HirePaceLeast, BuildCatalogue.HirePaceMost);
        }

        /// <summary>
        /// A builder's quote as the client reads it: the working days from a working day's start to the handover of a site of
        /// these hours at the pace its bid needs, each inspection's wait in them, by the site's own schedule
        /// (<see cref="SiteRules.HandoverDay"/>). The same however late in the week it is read.
        /// </summary>
        public static int Days(ProjectKind kind, int hours, int pace, int permits)
        {
            const int monday = 1;
            return Math.Max(1, SiteRules.WorkingDays(monday, SiteRules.HandoverDay(kind, hours, pace, permits, monday, CivicCatalogue.SiteOpens(permits))));
        }

        /// <summary>A contract's due day: the day its site, set out at a day's hour, is handed over at the pace its builder's bid needs (<see cref="SiteRules.HandoverDay"/>).</summary>
        public static int Due(ProjectKind kind, int hours, int pace, int permits, int day, int hour)
        {
            return SiteRules.HandoverDay(kind, hours, pace, permits, day, hour);
        }

        /// <summary>The draw a project's quotes come from this week: a new week, new quotes.</summary>
        public static int Salt(int project, int week) => project * 131 + week * 7 + 3;

        /// <summary>The last day this week's quotes stand: the week's last day (FirmRules.Week's weeks).</summary>
        public static int StandUntil(int day) => (FirmRules.Week(day) + 1) * 7;

        /// <summary>What a client's building is worth to a builder: the plans' estimate and the margin a client's tender pays.</summary>
        public static long Value(long estimate) => TenderRules.BuildingValue(estimate);

        /// <summary>The speciality a project's works suit: homes or offices for a new building, renovation for a rebuild.</summary>
        public static Speciality NeedOf(ProjectKind kind, BuildUse use)
        {
            return kind == ProjectKind.Plot ? TenderRules.NeedOf(TenderSource.Public, 5, use) : TenderRules.NeedOf(TenderSource.Client, 4, use);
        }

        /// <summary>The cheapest bid of those given (the first of equal ones), or -1.</summary>
        public static int Cheapest(TenderBid[] bids, int count)
        {
            int best = -1;
            for (int i = 0; bids != null && i < count && i < bids.Length; i++)
            {
                best = best < 0 || bids[i].Price < bids[best].Price ? i : best;
            }
            return best;
        }

        /// <summary>The fastest bid of those given, the cheaper of equal ones, or -1.</summary>
        public static int Fastest(TenderBid[] bids, int count)
        {
            int best = -1;
            for (int i = 0; bids != null && i < count && i < bids.Length; i++)
            {
                best = best < 0 || bids[i].Days < bids[best].Days || (bids[i].Days == bids[best].Days && bids[i].Price < bids[best].Price) ? i : best;
            }
            return best;
        }

        /// <summary>A contract's price as signed: the bid rounded to ten.</summary>
        public static long Price(TenderBid bid) => Math.Max(10L, bid.Price / 10L * 10L);

        /// <summary>The deposit paid as the contract is signed.</summary>
        public static long Deposit(long price) => Math.Max(0L, price) * BuildCatalogue.HireDepositPermille / 1000L;

        /// <summary>The payment due as an inspection passes (0 the foundations, 1 the frame, 2 the final).</summary>
        public static long StagePay(long price, int inspection)
        {
            if (inspection < 0 || inspection >= BuildCatalogue.HireStagePermille.Length)
            {
                return 0L;
            }
            return Math.Max(0L, price) * BuildCatalogue.HireStagePermille[inspection] / 1000L;
        }

        /// <summary>What is kept back until the handover: the rest of the price, so the payments come to the price exactly.</summary>
        public static long Retention(long price)
        {
            long paid = Deposit(price);
            for (int k = 0; k < BuildCatalogue.HireStagePermille.Length; k++)
            {
                paid += StagePay(price, k);
            }
            return Math.Max(0L, price - paid);
        }

        /// <summary>What the builder pays back for handing over some days late: a share a day, a tenth of the price at most.</summary>
        public static long Penalty(long price, int daysLate)
        {
            if (daysLate <= 0 || price <= 0)
            {
                return 0L;
            }
            long most = price * BuildCatalogue.HirePenaltyMostPermille / 1000L;
            return Math.Min(most, price * BuildCatalogue.HirePenaltyDayPermille * daysLate / 1000L);
        }

        /// <summary>
        /// The handover's settling up: the retention less the penalty goes to the builder; a penalty larger than the retention
        /// comes back to the client. Returns what the client pays (0 or more); <paramref name="back"/> is what they are paid.
        /// </summary>
        public static long Settle(long price, long paid, long penalty, out long back)
        {
            long owed = Math.Max(0L, price - paid) - Math.Max(0L, penalty);
            back = owed < 0 ? -owed : 0L;
            return owed > 0 ? owed : 0L;
        }

        /// <summary>
        /// The pace a hired builder's crews keep, in thousandths of the city's: as fast as its quote needs, then a little of its own
        /// luck, the less of it the better it is thought of. The same for the same save, project and builder.
        /// </summary>
        public static int Pace(int seed, int project, int rival, int quotedDays, int standardDays, int reputation)
        {
            int quoted = Math.Max(1, standardDays) * 1000 / Math.Max(1, quotedDays);
            BuildRandom random = BuildRandom.Of(seed, project, 241 + BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1));
            int least = BuildCatalogue.HireLuckLeast + BuildCatalogue.Clamp(reputation, 0, 100) * (BuildCatalogue.HireLuckMost - BuildCatalogue.HireLuckLeast) / 200;
            int luck = random.Range(least, BuildCatalogue.HireLuckMost + 1);
            return BuildCatalogue.Clamp(quoted * luck / 1000, BuildCatalogue.HirePaceLeast, BuildCatalogue.HirePaceMost);
        }

        /// <summary>The quality the inspector sees in a builder's work: its standing most, never as poor as an untrained crew's.</summary>
        public static int Quality(int reputation) => BuildCatalogue.Clamp(35 + BuildCatalogue.Clamp(reputation, 0, 100) * 6 / 10, 30, 95);
    }
}
