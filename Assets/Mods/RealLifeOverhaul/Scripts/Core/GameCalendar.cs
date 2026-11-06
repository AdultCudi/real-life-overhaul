namespace RealLifeOverhaul
{
    /// <summary>
    /// The calendar as the mod reads it, by the <see cref="CalendarRules"/> and the year the save was started with.
    /// Safe at any time: with no save, the game's default year.
    /// </summary>
    public static class GameCalendar
    {
        /// <summary>The year in days, 60 to 365.</summary>
        public static int DaysPerYear
        {
            get
            {
                var save = SaveGameManager.Current;
                return CalendarRules.Year(save != null && save.gameVariables != null ? save.gameVariables.daysPerYear : 0);
            }
        }

        public static int MonthDays => CalendarRules.MonthDays(DaysPerYear);

        public static int DayOfYear(int day) => CalendarRules.DayOfYear(day, DaysPerYear);

        public static float YearPart(int day) => CalendarRules.YearPart(day, DaysPerYear);

        public static Events.Season SeasonOf(int day) => (Events.Season)CalendarRules.Season(day, DaysPerYear);

        /// <summary>0 spring to 3 winter, as travel and property count seasons.</summary>
        public static int SeasonFromSpring(int day) => CalendarRules.SeasonFromSpring(day, DaysPerYear);

        public static int Monthly(int perDay) => CalendarRules.Monthly(perDay, DaysPerYear);
    }
}
