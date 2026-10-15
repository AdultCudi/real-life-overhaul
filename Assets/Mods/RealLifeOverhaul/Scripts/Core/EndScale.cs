using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The end game at scale: the late ventures (data centres, the cloud and the hyperscaler; the broadband company; the
    /// player's own bank) cost and earn at the size of the real thing, with upkeep, crews of staff by kind and income that
    /// grows with what is built. Each venture keeps its figures of before and its figures at scale in its own catalogue,
    /// and asks these rules for the mix of the two at a share: 0 gives the figures of before exactly (the option off), 1000
    /// the figures at scale. A save that already held a venture when the scale first met it moves from one to the other
    /// over <see cref="PhaseDays"/> days, so that nothing it owns is ruined overnight; a venture started at scale is at
    /// scale at once. Crews are hired by the venture itself, a share of what it needs a day, so a fast build is short of
    /// people for a few weeks and works worse meanwhile. What is bought (racks, a fit-out, a network, a licence) is at
    /// the full scale's price whenever the option is on; only the running costs and income come in over the weeks. What
    /// the scale adds to a day that the cash cannot meet is owed and carried to the next day, and the crews it could not
    /// pay leave in proportion. Game-free (checked by EconomyCheck, EndScaleChecks). Cost: nothing per frame; a few
    /// integer sums when a venture's day is worked out.
    /// </summary>
    public static class EndScaleRules
    {
        public const int Full = 1000;              // the share at scale; 0 is the figures of before
        public const int PhaseDays = 28;           // days an old save takes to come to scale: four game weeks
        public const int Unset = int.MinValue;     // a venture's day of coming to scale before it is known (an older save, or one never on)
        public const int WorkingLeast = 500;       // thousandths a venture with none of a crew it needs still does
        public const int OutageMost = 3000;        // thousandths: a venture with none of a crew fails three times as often
        public const int HireLeast = 1;            // people a crew takes on a day at the least while it is short
        // The aim, held by EconomyCheck (EndScaleChecks). Data centres and the cloud earn and spend by the day, so their aim
        // is in game days, whatever the year's length; the broadband company and the bank bill and lend by the save's own
        // months and years, so theirs is in the save's own years.
        public const int PaybackDaysLeast = 120;   // a data centre or cloud well run at scale earns back what was put in after this many game days...
        public const int PaybackDaysMost = 360;    // ...and before this many
        public const int PaybackYearsLeast = 2;    // a broadband network or a bank: after this many of the save's years...
        public const int PaybackYearsMost = 6;     // ...and before this many

        /// <summary>The share what is bought is priced at: the full scale whenever the option is on, so nothing bought during the weeks of coming to scale is cheap.</summary>
        public static int Buying(bool on) => on ? Full : 0;

        /// <summary>Whole money from the game's float money, never negative, and safe beyond an int: worked out here, not left to the runtime's cast.</summary>
        public static long Whole(double money)
        {
            if (!(money > 0))
            {
                return 0;
            }
            return money >= long.MaxValue / 4 ? long.MaxValue / 4 : (long)Math.Floor(money);
        }

        /// <summary>
        /// The crew left after a day whose scale costs were not all paid: the unpaid share of them (thousandths of what was
        /// due) leaves, rounded up, so that cover and output fall as long as the costs stay unpaid.
        /// </summary>
        public static int Leave(int have, int unpaidPerMille)
        {
            int h = Math.Max(0, have), u = Clamp(unpaidPerMille);
            return u == 0 ? h : h - (int)(((long)h * u + Full - 1) / Full);
        }

        /// <summary>The share of what was due that was not paid, thousandths.</summary>
        public static int Unpaid(long due, long paid)
        {
            return due <= 0 ? 0 : (int)Math.Min(Full, Math.Max(0L, due - paid) * Full / due);
        }

        /// <summary>A figure of before and its figure at scale, mixed at a share (0 to 1000).</summary>
        public static long Mix(long before, long scale, int share)
        {
            int s = Clamp(share);
            return s == 0 ? before : s == Full ? scale : before + (scale - before) * s / Full;
        }

        public static int Mix(int before, int scale, int share)
        {
            return (int)Mix((long)before, scale, share);
        }

        /// <summary>
        /// The day a venture's coming to scale counts from. Known: as it was, whether the option is on or off (switching it
        /// off and on again neither restarts the weeks nor tells the notice twice). On, and not known yet: today if the
        /// venture already holds something (it is phased in), else so long ago that it is at scale at once. Off and not
        /// known: still not known.
        /// </summary>
        public static int From(bool on, int from, int today, bool holds)
        {
            if (!on || from != Unset)
            {
                return from;
            }
            return holds ? today : today - PhaseDays;
        }

        /// <summary>The share a venture is at on a day: 0 with the option off or the day unknown, then a step a day to the whole over <see cref="PhaseDays"/>.</summary>
        public static int Share(bool on, int from, int today)
        {
            if (!on || from == Unset)
            {
                return 0;
            }
            long done = (long)today - from;
            return done <= 0 ? 0 : done >= PhaseDays ? Full : (int)(done * Full / PhaseDays);
        }

        /// <summary>True on the day a venture that held something began coming to scale: its notice is due.</summary>
        public static bool Begins(int before, int after, int today)
        {
            return before == Unset && after == today;
        }

        /// <summary>The people a crew needs at a share: the need at scale, a part of it while the share is partial (rounded up).</summary>
        public static int Need(int atScale, int share)
        {
            int s = Clamp(share);
            return atScale <= 0 || s == 0 ? 0 : (int)(((long)atScale * s + Full - 1) / Full);
        }

        /// <summary>One for every so many of a thing, rounded up; none for none.</summary>
        public static int Per(long things, long each)
        {
            return things <= 0 || each <= 0 ? 0 : (int)Math.Min(int.MaxValue / 4, (things + each - 1) / each);
        }

        /// <summary>A crew after a day: short of its need, it takes on a part of the need (one at the least); over it, it lets the rest go at once.</summary>
        public static int Hire(int have, int need, int pacePerMille)
        {
            int h = Math.Max(0, have), n = Math.Max(0, need);
            if (h >= n)
            {
                return n;
            }
            int step = Math.Max(HireLeast, (int)((long)n * Math.Max(0, pacePerMille) / Full));
            return Math.Min(n, h + step);
        }

        /// <summary>How far a crew covers its need, thousandths: all of it when nothing is needed.</summary>
        public static int Cover(int have, int need)
        {
            return need <= 0 ? Full : (int)Math.Min(Full, Math.Max(0L, (long)have) * Full / need);
        }

        /// <summary>What a venture does at a cover of its crews, thousandths: half at none, all of it when they are all there.</summary>
        public static int Working(int cover)
        {
            int c = Clamp(cover);
            return WorkingLeast + (Full - WorkingLeast) * c / Full;
        }

        /// <summary>How much more often a venture fails at a cover of its crews, thousandths: as usual when they are all there, three times at none.</summary>
        public static int Outages(int cover)
        {
            int c = Clamp(cover);
            return Full + (OutageMost - Full) * (Full - c) / Full;
        }

        /// <summary>
        /// The part of a cost at scale that is charged today: never more than the room left (the cash over what the day's
        /// other costs took), so that what the scale adds never takes the money below nothing in one step. The room is
        /// what is left after it.
        /// </summary>
        public static long Take(ref long room, long cost)
        {
            if (cost <= 0)
            {
                return 0;
            }
            long taken = Math.Min(cost, Math.Max(0L, room));
            room -= taken;
            return taken;
        }

        private static int Clamp(int share)
        {
            return share < 0 ? 0 : share > Full ? Full : share;
        }
    }
}
