using System;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The rules of the leased lines: who may have one, what it costs by month and to install, how long the wait is and what speed it gives. A line is an add-on to a
    /// place's internet account (<see cref="Account.LineStep"/>): the plan stays as it was, and the place's speed is the faster of the two. A line changed to another
    /// step keeps the old one running until the new one is live (<see cref="Account.LineNext"/>), so a hall never loses its connection to grow. Game-free; checked offline.
    /// </summary>
    public static class LeasedLineRules
    {
        /// <summary>
        /// What a district has today: <see cref="LeasedLineCatalogue.Fibre"/>, <see cref="LeasedLineCatalogue.Cable"/> or <see cref="LeasedLineCatalogue.Dig"/>
        /// (any district the catalogue does not know).
        /// </summary>
        public static int Class(string district)
        {
            return InternetRules.Reaches(InternetCatalogue.FibreIndex, district) ? LeasedLineCatalogue.Fibre
                : InternetRules.Reaches(InternetCatalogue.CableIndex, district) ? LeasedLineCatalogue.Cable : LeasedLineCatalogue.Dig;
        }

        public static int Step(int plan) => Math.Max(0, Math.Min(LeasedLineCatalogue.Steps - 1, plan));

        /// <summary>A line's price a month at a place now, cents, at today's price level.</summary>
        public static int MonthCents(int step, int level)
        {
            long cents = LeasedLineCatalogue.MonthCents[Step(step)];
            cents = cents * Math.Max(PriceRules.Least, Math.Min(PriceRules.Most, level)) / PriceRules.Start;
            return (int)Math.Min(cents, int.MaxValue / 4);
        }

        /// <summary>The installation fee, cents at today's price level: a share of the month's price, more where the district has no fibre.</summary>
        public static int InstallCents(int step, string district, int level)
        {
            long cents = (long)MonthCents(step, level) * LeasedLineCatalogue.InstallShare / 1000;
            cents = cents * LeasedLineCatalogue.FeeShare[Class(district)] / 1000;
            return (int)Math.Min(cents, int.MaxValue / 4);
        }

        /// <summary>The wait from the order to the line being live, days.</summary>
        public static int InstallDays(int step, string district)
        {
            return LeasedLineCatalogue.InstallDays[Step(step)] + LeasedLineCatalogue.ExtraDays[Class(district)];
        }

        /// <summary>The step (plus one) of a line live on a day, 0 with none: the new step once its wait is over, else the one running.</summary>
        public static int LiveAt(Account a, int day)
        {
            if (a == null || a.State != SupplyState.Active && a.State != SupplyState.Pending)
            {
                return 0;   // a cut or closed account carries no line; a plan changed to another provider only puts the account back to being installed, and the line is a pipe of its own
            }
            if (a.LineNext > 0 && a.LineNext <= LeasedLineCatalogue.Steps && day >= a.LineNextDay)
            {
                return a.LineNext;
            }
            return a.LineStep > 0 && a.LineStep <= LeasedLineCatalogue.Steps && day >= a.LineDay ? a.LineStep : 0;
        }

        /// <summary>The line last ordered (a change ordered counts, though the old step still runs): step plus one, 0 with none.</summary>
        public static int Ordered(Account a)
        {
            if (a == null)
            {
                return 0;
            }
            int s = a.LineNext > 0 ? a.LineNext : a.LineStep;
            return s > 0 && s <= LeasedLineCatalogue.Steps ? s : 0;
        }

        public static bool Has(Account a) => Ordered(a) > 0;

        /// <summary>
        /// A line ordered. With one already running the new step takes over when its wait is over and the old one runs until then; with none (or one still being
        /// installed) it replaces it. The plan and the rest of the account are left as they were.
        /// </summary>
        public static void Order(Account a, int step, string district, int day)
        {
            int day2 = day + InstallDays(step, district);
            if (LiveAt(a, day) > 0)
            {
                a.LineNext = Step(step) + 1;
                a.LineNextDay = day2;
                return;
            }
            a.LineStep = Step(step) + 1;
            a.LineDay = day2;
            a.LineNext = 0;
            a.LineNextDay = 0;
        }

        /// <summary>The day's turn: a changed line whose wait is over becomes the line. True if it changed.</summary>
        public static bool Turn(Account a, int day)
        {
            if (a == null || a.LineNext <= 0 || day < a.LineNextDay)
            {
                return false;
            }
            a.LineStep = Math.Min(a.LineNext, LeasedLineCatalogue.Steps);
            a.LineDay = a.LineNextDay;
            a.LineNext = 0;
            a.LineNextDay = 0;
            return true;
        }

        /// <summary>A line given up, a change not yet live with it: a line is not on a contract, so nothing is owed for leaving.</summary>
        public static void Drop(Account a)
        {
            a.LineStep = a.LineDay = a.LineNext = a.LineNextDay = 0;
        }

        /// <summary>The speed the line gives on a day, Mbps; 0 with none or while it is being installed.</summary>
        public static int LineMbps(Account a, int day)
        {
            int s = LiveAt(a, day);
            return s > 0 ? LeasedLineCatalogue.Mbps[s - 1] : 0;
        }

        /// <summary>What the line live on a day costs a month, cents at the level; 0 with none live.</summary>
        public static int LineCents(Account a, int day, int level)
        {
            int s = LiveAt(a, day);
            return s > 0 ? MonthCents(s - 1, level) : 0;
        }

        /// <summary>A day of the line added to the month, as a plan's days are: nothing before it is live. Carriers hold a service level, so there is no outage credit.</summary>
        public static void AddDay(Account a, int day, int monthDays, int level)
        {
            int s = LiveAt(a, day);
            if (s <= 0)
            {
                return;
            }
            int perDay = (int)(((long)MonthCents(s - 1, level) + Math.Max(1, monthDays) / 2) / Math.Max(1, monthDays));
            a.Accrued = (int)Math.Min((long)a.Accrued + perDay, int.MaxValue / 4);
        }

        /// <summary>The faster of a plan's speed and the line's: what the place's connection gives.</summary>
        public static int Speed(int planMbps, int lineMbps) => Math.Max(planMbps, lineMbps);
    }
}
