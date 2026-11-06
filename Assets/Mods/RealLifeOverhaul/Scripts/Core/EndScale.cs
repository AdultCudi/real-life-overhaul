using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The end game at scale: the late ventures (data centres and the cloud, broadband, the player's own bank) cost and
    /// earn at real size. Each mixes its figures of before and at scale by a share, 0 (the option off) to
    /// <see cref="Full"/>; a save that already held a venture comes to scale over <see cref="PhaseDays"/>. Purchases are
    /// at full scale whenever the option is on. Crews are hired a share a day; what the cash cannot meet is carried over,
    /// and unpaid crews leave in proportion. Game-free, checked by EconomyCheck. COST: nothing per frame.
    /// </summary>
    public static class EndScaleRules
    {
        public const int Full = 1000;              // the share at scale; 0 is the figures of before
        public const int PhaseDays = 28;           // days an old save takes to come to scale: four game weeks
        public const int Unset = int.MinValue;     // a venture's day of coming to scale, not yet known
        public const int WorkingLeast = 500;       // thousandths a venture with none of a crew it needs still does
        public const int OutageMost = 3000;        // thousandths: a venture with none of a crew fails three times as often
        public const int HireLeast = 1;            // people a crew takes on a day at the least while it is short
        // The payback aim, held by EconomyCheck: in game days for what earns by the day, in the save's years for what
        // bills and lends by its months.
        public const int PaybackDaysLeast = 120;   // a data centre or cloud well run at scale earns back what was put in after this many game days...
        public const int PaybackDaysMost = 360;    // ...and before this many
        public const int PaybackYearsLeast = 2;    // a broadband network or a bank: after this many of the save's years...
        public const int PaybackYearsMost = 6;     // ...and before this many

        /// <summary>The share purchases are priced at: full whenever the option is on, so nothing bought while phasing in is cheap.</summary>
        public static int Buying(bool on) => on ? Full : 0;

        /// <summary>Whole money from the game's float money: never negative, capped rather than left to the runtime's cast.</summary>
        public static long Whole(double money)
        {
            if (!(money > 0))
            {
                return 0;
            }
            return money >= long.MaxValue / 4 ? long.MaxValue / 4 : (long)Math.Floor(money);
        }

        /// <summary>The crew left after a day with costs unpaid: the unpaid share (thousandths) leaves, rounded up.</summary>
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
        /// The day a venture's coming to scale counts from. Once known it stays, so switching the option off and on
        /// neither restarts the weeks nor tells the notice twice. Set only with the option on: today if the venture holds
        /// something, else far enough back to be at scale at once.
        /// </summary>
        public static int From(bool on, int from, int today, bool holds)
        {
            if (!on || from != Unset)
            {
                return from;
            }
            return holds ? today : today - PhaseDays;
        }

        /// <summary>The share on a day: 0 with the option off or the day unknown, then rising to full over <see cref="PhaseDays"/>.</summary>
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

        /// <summary>The people a crew needs at a share of the need at scale, rounded up.</summary>
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

        /// <summary>A crew after a day: short, it takes on a part of the need (one at least); over, it lets the rest go at once.</summary>
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

        /// <summary>How far a crew covers its need, thousandths; full when nothing is needed.</summary>
        public static int Cover(int have, int need)
        {
            return need <= 0 ? Full : (int)Math.Min(Full, Math.Max(0L, (long)have) * Full / need);
        }

        /// <summary>What a venture does at a cover of its crews, thousandths: half at none, all at full.</summary>
        public static int Working(int cover)
        {
            int c = Clamp(cover);
            return WorkingLeast + (Full - WorkingLeast) * c / Full;
        }

        /// <summary>How often a venture fails at a cover of its crews, thousandths of usual: as usual at full, three times at none.</summary>
        public static int Outages(int cover)
        {
            int c = Clamp(cover);
            return Full + (OutageMost - Full) * (Full - c) / Full;
        }

        /// <summary>
        /// The part of a cost at scale charged today: never more than the <paramref name="room"/> left in the cash,
        /// which is lowered by it, so the scale never takes the money below 0 in one step.
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
