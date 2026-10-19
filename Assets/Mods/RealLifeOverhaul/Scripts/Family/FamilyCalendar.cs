namespace RealLifeOverhaul.Family
{
    /// <summary>By the in-game hour and day: "at six" means "at six or later, if not done today", so a skipped hour loses nothing.</summary>
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
                return;   // family life waits while the player is locked up
            }
            HomeLife.Chore(day, hour);
            HomeLife.Support(day, hour);
            WeddingPlanner.Eve(day, hour);
            HardTimesWatch.Hourly(day, hour);
        }
    }
}
