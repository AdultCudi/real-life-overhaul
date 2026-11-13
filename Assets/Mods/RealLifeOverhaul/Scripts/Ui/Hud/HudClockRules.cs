namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A time of day as the game's top bar writes it (TimeHelper.SetFormattedTime): "08:45", or with the game's 12 hour
    /// option on "08:45 AM", midnight as "12:00 AM" and noon as "12:00 PM". Game-free, so the frame's pieces and the offline
    /// checks share one rule; the callers pass the game's own option. Cost: one string.
    /// </summary>
    public static class HudClockRules
    {
        /// <summary>The time of day in the player's chosen clock; the minute is cut to a whole one, as the game cuts it.</summary>
        public static string Format(int hour, float minute, bool twelve) => UiClockRules.Format(hour, (int)minute, twelve);

        /// <summary>The widest time the clock can write, to keep the time pill one width all day.</summary>
        public static string Widest(bool twelve) => twelve ? "00:00 PM" : "00:00";
    }
}
