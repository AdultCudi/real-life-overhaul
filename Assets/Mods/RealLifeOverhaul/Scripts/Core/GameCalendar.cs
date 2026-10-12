namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's calendar as every part of the mod reads it (the rules are <see cref="CalendarRules"/>): the year the
    /// player chose, the mod's month and the season. Safe at any time: with no save the game's default year is given.
    /// </summary>
    public static class GameCalendar
    {
        /// <summary>The year in days, as the save was started with (60 to 365).</summary>
        public static int DaysPerYear
        {
            get
            {
                var save = SaveGameManager.Current;
                return CalendarRules.Year(save != null && save.gameVariables != null ? save.gameVariables.daysPerYear : 0);
            }
        }

        /// <summary>The mod's month in days: a twelfth of the year, three at least.</summary>
        public static int MonthDays => CalendarRules.MonthDays(DaysPerYear);

        /// <summary>The day of the year, 1 to DaysPerYear.</summary>
        public static int DayOfYear(int day) => CalendarRules.DayOfYear(day, DaysPerYear);

        /// <summary>How far through the year a day is, 0 to just under 1.</summary>
        public static float YearPart(int day) => CalendarRules.YearPart(day, DaysPerYear);

        /// <summary>The season of a day.</summary>
        public static Events.Season SeasonOf(int day) => (Events.Season)CalendarRules.Season(day, DaysPerYear);

        /// <summary>The season of a day counted from spring (0 spring to 3 winter), as travel and property count theirs.</summary>
        public static int SeasonFromSpring(int day) => CalendarRules.SeasonFromSpring(day, DaysPerYear);

        /// <summary>A month's worth of something priced by the day.</summary>
        public static int Monthly(int perDay) => CalendarRules.Monthly(perDay, DaysPerYear);
    }
}
