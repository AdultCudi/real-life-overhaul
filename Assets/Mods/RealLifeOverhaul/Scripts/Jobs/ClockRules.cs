namespace RealLifeOverhaul
{
    /// <summary>
    /// The shift clock's count as a rule with no game types, checked offline (EconomyCheck ClientHoursChecks). A run of the clock
    /// is given some units of the same length; a place of work that closes part of the way through stops the count there, so a
    /// run the game's time machine reports only at its end is still paid for what was done before the doors shut.
    /// </summary>
    public static class ClockRules
    {
        /// <summary>Units done after <paramref name="minutes"/> of a run given <paramref name="left"/> units of <paramref name="each"/>
        /// minutes (<paramref name="whole"/>: the run went to its end), with the place of work open for the first <paramref name="open"/>.</summary>
        public static int Units(int minutes, bool whole, int left, int each, int open)
        {
            int length = each > 0 ? each : 1;
            int units = whole ? left : System.Math.Min(left, minutes / length);
            return minutes > open ? System.Math.Min(units, System.Math.Max(0, open) / length) : units;
        }
    }
}
