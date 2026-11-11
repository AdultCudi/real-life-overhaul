using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What an employee really costs an hour: the game's wage (EmployeeInstance.hourlyWage, which the game charges as it
    /// is) at the city's prices (the city's economy books the difference on every wage each night, Economy/City), and
    /// any raise agreed through the Company app on top (booked by the company each night). Every page that names a wage,
    /// the game's own screens included (<see cref="WageShown"/>), asks here, so the number shown is the number charged.
    /// Game-free, with the raise handed in by the wiring, so the checkers compile it. A few integer operations; nothing per frame.
    /// </summary>
    public static class PaidWage
    {
        /// <summary>The raise agreed for an employee by the game's id, hundredths an hour; set by the wiring, null: none.</summary>
        public static Func<string, int> Raise;

        /// <summary>
        /// Hundredths an hour paid for a game wage of <paramref name="baseCents"/> at a price level in thousandths (1000 at
        /// the start), with a raise on top: a x b / 1000 rounded halves away from nothing, as the company has always counted it.
        /// </summary>
        public static int Cents(int baseCents, int level, int raiseCents)
        {
            long value = (long)Math.Max(0, baseCents) * Math.Max(0, level);
            long scaled = (value + 500) / 1000;
            long paid = scaled + Math.Max(0, raiseCents);
            return (int)Math.Min(int.MaxValue / 2, paid);
        }

        /// <summary>The city's part of a wage, hundredths an hour: what its prices add (or, below the start, take off).</summary>
        public static int City(int baseCents, int level)
        {
            return Cents(baseCents, level, 0) - Math.Max(0, baseCents);
        }

        /// <summary>A game wage as the game keeps it (money an hour) in hundredths, rounded as the game's own figures are.</summary>
        public static int Base(float hourlyWage)
        {
            return hourlyWage > 0f ? (int)Math.Min(int.MaxValue / 2, Math.Round(hourlyWage * 100f)) : 0;
        }

        /// <summary>The raise agreed for an employee, hundredths an hour; 0 for none, or while nothing hands one in.</summary>
        public static int RaiseOf(string id)
        {
            Func<string, int> raise = Raise;
            return raise != null && !string.IsNullOrEmpty(id) ? Math.Max(0, raise(id)) : 0;
        }

        /// <summary>What an employee with this game wage costs an hour today, hundredths: the city's prices and their raise.</summary>
        public static int Now(float hourlyWage, string id)
        {
            return Cents(Base(hourlyWage), Prices.Level, RaiseOf(id));
        }

        /// <summary>The same in money an hour, as the game's own screens write a wage.</summary>
        public static float NowMoney(float hourlyWage, string id)
        {
            return Now(hourlyWage, id) / 100f;
        }

        /// <summary>
        /// What a screen shows somebody costs an hour, hundredths: the paid wage of somebody this machine pays, and the game's own figure for another player's
        /// (Going Public), whose wage and raise this machine neither pays nor knows.
        /// </summary>
        public static int Shown(float hourlyWage, string id, bool ours)
        {
            return ours ? Now(hourlyWage, id) : Base(hourlyWage);
        }

        /// <summary>True when what is paid differs from the game's own figure, so a screen showing the game's figure is wrong.</summary>
        public static bool Differs(int baseCents, int paidCents)
        {
            return paidCents != Math.Max(0, baseCents);
        }
    }
}
