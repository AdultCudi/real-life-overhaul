namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What family life does by the clock: once an in-game hour and once an in-game day, the
    /// director hands over to each part here. Everything that is "once a day at six" is written
    /// as "today, at six or later, if not yet done today", so an hour skipped by the game's own
    /// fast-forward is never an evening lost.
    /// </summary>
    internal static class FamilyCalendar
    {
        public static void Daily(int day, int elapsed)
        {
            FamilySave s = FamilyState.S;
            FamilyState.RollMonth(day);
            Partner.Resync();
            HomeLife.Daily(day);
            Children.Daily(day, elapsed);
            Relatives.Daily(day);
            HardTimes.Daily(day);
            bool warm = s.Harmony >= 40f;
            FamilyModifiers.Keep(FamilyModifiers.Married, FamilyState.Bond == Bond.Married && warm);
            FamilyModifiers.Keep(FamilyModifiers.Engaged, FamilyState.Bond == Bond.Engaged && warm);
        }

        public static void Hourly(int day, int hour)
        {
            bool held = FamilyBridges.Law != null && FamilyBridges.Law.InCustody;
            HomeLife.Contribution(day, hour);
            Children.Hourly(day, hour);
            if (held)
            {
                return;   // family life waits for somebody who is locked up
            }
            HomeLife.Chore(day, hour);
            HomeLife.Support(day, hour);
            WeddingPlanner.Eve(day, hour);
        }
    }
}
