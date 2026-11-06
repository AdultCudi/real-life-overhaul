using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one bound on every wage the mod raises (strikes, rises, promotions, matched offers, hires at an agreed wage).
    /// The measure is the game's own wage for the employee's main skill (CalculateHourlyWageForSkill, hundredths an
    /// hour): a raised wage never passes <see cref="CapBp"/> of it, never goes down, and a rise of the player's choosing
    /// comes at most once in <see cref="PeriodDays"/>, so percentages cannot stack. A wage the mod raised that ran away
    /// is mended on load (<see cref="Mended"/>). Game-free; a few integer operations, nothing per frame.
    /// </summary>
    public static class WageRules
    {
        public const int CapBp = 40000;     // basis points of the game's wage for the skill: the best paid tech chief is offered about 2.9 times it
        public const int PeriodDays = 14;   // days: one rise of the player's choosing per employee in this many (CompanyCatalogue.RaiseRestDays)

        /// <summary>The most a raise of the mod's takes a wage to, hundredths an hour; nothing with no formula to measure by.</summary>
        public static int Cap(int formula)
        {
            return formula <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)formula * CapBp / 10000);
        }

        /// <summary>
        /// A wage after a rise of so many basis points, hundredths an hour: never past the cap, never lower than it was;
        /// with no formula known it is not raised.
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

        /// <summary>A wage brought up to a figure: never past the cap, never lower than it was.</summary>
        public static int UpTo(int wage, int target, int formula)
        {
            wage = Math.Max(0, wage);
            return formula <= 0 ? wage : Math.Max(wage, Math.Min(target, Cap(formula)));
        }

        /// <summary>True if a rise of the player's choosing may be given today, the last given on <paramref name="lastDay"/>.</summary>
        public static bool Due(int lastDay, int day)
        {
            return (long)day - lastDay >= PeriodDays;
        }

        public static int NextDay(int lastDay)
        {
            return lastDay + PeriodDays;
        }

        /// <summary>A wage the mod raised, on load: brought back to the cap if past it; left alone with no formula known.</summary>
        public static int Mended(int wage, int formula)
        {
            int cap = Cap(formula);
            return formula > 0 && wage > cap ? cap : wage;
        }
    }
}
