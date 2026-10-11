namespace RealLifeOverhaul
{
    /// <summary>
    /// What leaving the rest of a shift to the game's clock (<see cref="ShiftSkip"/>) costs and pays, for every job that
    /// offers it. One place: the jobs, the Guide and the phone all read these.
    ///
    /// A shift is four game hours, the hours an employer pays basic wage for, split evenly over the units the
    /// catalogue says the shift is (a drinks cart's twelve customers are twenty minutes each, a bundle of
    /// twenty-four flyers ten minutes each). The clock pays <see cref="Share"/> of what a unit pays by hand, and no
    /// tips and no bonus.
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
