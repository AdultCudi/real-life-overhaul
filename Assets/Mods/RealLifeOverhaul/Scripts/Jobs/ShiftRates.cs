namespace RealLifeOverhaul
{
    /// <summary>What leaving a shift to the game's clock costs and pays: four game hours over its units, at <see cref="Share"/> of the pay.</summary>
    public static class ShiftRates
    {
        /// <summary>The share of a unit's pay the shift earns when the clock does the unit.</summary>
        public const float Share = 0.85f;

        public const int ShiftMinutes = 240;   // game minutes in a whole shift

        /// <summary>Game minutes the clock takes for one unit of this job's shift.</summary>
        public static int Minutes(JobDef def)
        {
            return def != null && def.ShiftUnits > 0 ? ShiftMinutes / def.ShiftUnits : ShiftMinutes;
        }

        /// <summary><see cref="Share"/> as a whole percent, for texts.</summary>
        public static int Percent => (int)(Share * 100f + 0.5f);
    }
}
