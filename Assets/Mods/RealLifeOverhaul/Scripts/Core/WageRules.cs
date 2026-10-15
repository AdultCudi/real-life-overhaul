using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one bound on every wage of the game's that the mod raises: a strike settled with a raise (Business), a rise, a
    /// promotion, a rival's offer matched, a restraint clause and a hire at the wage agreed (Tech), and the mod's own
    /// payroll. The measure is the game's own wage for the employee's main skill at its value
    /// (EmployeeHelper.CalculateHourlyWageForSkill, handed in as hundredths an hour): a wage the mod raises never goes past
    /// <see cref="CapBp"/> of it, never goes down by a raise, and a rise the player chooses comes at most once in
    /// <see cref="PeriodDays"/> for one employee, so a percentage of an already raised wage cannot be stacked day after day.
    /// A save in which wages ran away before this bound is mended once as it loads (<see cref="Mended"/>): only wages the
    /// mod is known to have raised, never one the player agreed at a hire of the game's own.
    ///
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, WageChecks.cs). Cost: a few integer operations
    /// on a click, a day's pass or a load; nothing per frame.
    /// </summary>
    public static class WageRules
    {
        public const int CapBp = 40000;     // basis points of the game's own wage for the skill: the most a raise of the mod's takes a wage to (a tech chief with every certificate in the scarcest market is offered about 2.9 times it; four times leaves room for promotions and stops any runaway)
        public const int PeriodDays = 14;   // days: one rise of the player's choosing per employee in this many, as a company's own staff wait between requests (CompanyCatalogue.RaiseRestDays)

        /// <summary>The most a raise of the mod's takes a wage to, hundredths an hour; nothing with no formula to measure by.</summary>
        public static int Cap(int formula)
        {
            return formula <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)formula * CapBp / 10000);
        }

        /// <summary>
        /// A wage after a rise of so many basis points, hundredths an hour: never past the cap, never lower than it was
        /// (a wage already over the cap is left where it is, and is not raised). With no formula known it is not raised.
        /// </summary>
        public static int Rise(int wage, int bp, int formula)
        {
            wage = Math.Max(0, wage);
            if (bp <= 0 || formula <= 0)
            {
                return wage;
            }
            long raised = wage + (long)wage * bp / 10000;
            return (int)Math.Max(wage, Math.Min(raised, Cap(formula)));
        }

        /// <summary>A wage brought up to a figure (a strike's raise, a hire at the wage agreed): never past the cap, never lower than it was.</summary>
        public static int UpTo(int wage, int target, int formula)
        {
            wage = Math.Max(0, wage);
            return formula <= 0 ? wage : Math.Max(wage, Math.Min(target, Cap(formula)));
        }

        /// <summary>True if a rise of the player's choosing may be given today, the last one having been given on <paramref name="lastDay"/>.</summary>
        public static bool Due(int lastDay, int day)
        {
            return (long)day - lastDay >= PeriodDays;
        }

        /// <summary>The first day the next rise may be given.</summary>
        public static int NextDay(int lastDay)
        {
            return lastDay + PeriodDays;
        }

        /// <summary>A wage the mod raised, as a save is loaded: brought back to the cap if it ran past it, otherwise as it is. With no formula known it is left alone.</summary>
        public static int Mended(int wage, int formula)
        {
            int cap = Cap(formula);
            return formula > 0 && wage > cap ? cap : wage;
        }
    }
}
