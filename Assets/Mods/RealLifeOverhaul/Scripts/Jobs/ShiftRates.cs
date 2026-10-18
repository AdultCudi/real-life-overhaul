namespace RealLifeOverhaul
{
    /// <summary>
    /// What leaving the rest of a shift to the game's clock (<see cref="ShiftSkip"/>) costs and pays, read by the jobs, the
    /// Guide and the phone. A shift is four game hours split evenly over its units (a cart's twelve customers are twenty
    /// minutes each); the clock pays <see cref="Share"/> of a unit's pay by hand, without tips or bonus.
    /// </summary>
    public static class ShiftRates
    {
        /// <summary>The share of a unit's pay the shift earns when the clock does the unit.</summary>
        public const float Share = 0.85f;

        /// <summary>Game minutes in a whole shift.</summary>
        public const int ShiftMinutes = 240;

        /// <summary>Game minutes the clock takes for one unit of this job's shift.</summary>
        public static int Minutes(JobDef def)
        {
            return def != null && def.ShiftUnits > 0 ? ShiftMinutes / def.ShiftUnits : ShiftMinutes;
        }

        /// <summary><see cref="Share"/> as a whole percent, for texts.</summary>
        public static int Percent => (int)(Share * 100f + 0.5f);
    }
}
