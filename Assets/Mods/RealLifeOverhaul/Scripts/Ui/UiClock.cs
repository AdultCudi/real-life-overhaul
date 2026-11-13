namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one way the kit writes a time of day, honouring the game's 12 hour clock option (TimeHelper.use12h, set from Options) the way the game's own
    /// top bar and notice list do. A screen that shows a clock time calls this and never formats the hour and minute itself.
    /// </summary>
    public static class UiClock
    {
        /// <summary>Whether the game's 12 hour clock option is on.</summary>
        public static bool TwelveHour => TimeHelper.use12h;

        /// <summary>An hour and a minute of the day as the game writes them.</summary>
        public static string At(int hour, int minute) => UiClockRules.Format(hour, minute, TimeHelper.use12h);

        /// <summary>Minutes since midnight as the game writes them.</summary>
        public static string AtMinute(int minuteOfDay) => UiClockRules.Format(minuteOfDay, TimeHelper.use12h);
    }
}
