using System;

namespace RealLifeOverhaul.Economy
{
}

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a unit is, from the game's floor area: its size, and for a home its bedrooms. Game-free.</summary>
    public static class UnitRules
    {
        /// <summary>Bedrooms of a home from its floor area in square metres: a studio under 35, then one for about every 20 over 30, to five.</summary>
        public static int Bedrooms(int sqm)
        {
            return sqm < 35 ? 0 : Math.Min(5, Math.Max(1, (sqm - 15) / 20));
        }

        /// <summary>The floor area of one unit of a building: the building's over its units, at least a square metre.</summary>
        public static int UnitSqm(int totalSqm, int units)
        {
            return Math.Max(1, totalSqm / Math.Max(1, units));
        }
    }
}
