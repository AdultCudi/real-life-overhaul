namespace RealLifeOverhaul
{
    /// <summary>
    /// When the morning line is due after a sleep, how long the sleep was, and how the line greets (Home/HomeDirector
    /// asks, Home/HomeReport words it).
    ///
    /// A SLEEP. The game's own sleep in a bed, a car or a boat (PlayerActivity.SleepActivity while its state is Started
    /// or Running), or a night or a nap in a hotel room through its time skip (Travel/HotelStay, asked through
    /// HomeBridges.SleepingAway). Nothing else is one: not the day rolling over, a load, the game's other time skips,
    /// the mod's own (custody, a ride, an outing) or another mod's.
    ///
    /// ITS HOURS. From the clock at the last look before the player was seen asleep (they lay down after it) to the
    /// clock at which the sleep ended: the game's sleep ends in SleepActivity.Finish, which raises the game's event
    /// "ba:gameevent_playerenergyincreased" at its own minute for a sleep under way, whether it ran to its time, was
    /// stopped or had its time skip cancelled; a hotel night ends when the room says so. The game fixes a sleep's end
    /// when Start is pressed (now and the slider's time, 5 minutes to 24 hours), so the walk to the bed comes off it:
    /// the slider at 24 hours and a twelve-minute walk is a sleep of 23.8 hours.
    ///
    /// THE LINE. Once a day, after a sleep of at least <see cref="NightAtLeast"/> hours that was seen to begin and that
    /// the game itself ended (or a hotel night), with the morning summary switched on. It says "Good morning" only from
    /// <see cref="MorningFrom"/> to <see cref="MorningTo"/> o'clock, since a long sleep can end at any hour.
    ///
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, MorningNewsChecks.cs). Cost: a few comparisons.
    /// </summary>
    public static class WakeRules
    {
        public const float NightAtLeast = 3f;   // hours: a shorter sleep is a nap, which pays off sleep debt and says nothing
        public const int MorningFrom = 4;       // o'clock: a wake from this hour...
        public const int MorningTo = 12;        // ...to before this one is greeted "Good morning"

        /// <summary>The hours from lying down to getting up, both in game minutes; 0 when the lying down was not seen or the times are the wrong way round.</summary>
        public static float Hours(float laidAt, float roseAt) => laidAt < 0f || roseAt <= laidAt ? 0f : (roseAt - laidAt) / 60f;

        /// <summary>When the player got up: the minute the game ended the sleep, if it did so since they lay down; otherwise now.</summary>
        public static float RoseAt(float endedAt, float laidAt, float now) => endedAt >= 0f && endedAt >= laidAt && endedAt <= now ? endedAt : now;

        /// <summary>Whether the sleep was ended by the game (since the lying down) or was a hotel night, which the room ends.</summary>
        public static bool Ended(bool awayNight, float endedAt, float laidAt) => awayNight || (endedAt >= 0f && laidAt >= 0f && endedAt >= laidAt);

        /// <summary>Whether the morning line goes out: switched on, a sleep seen to begin and ended by the game, long enough, and not yet told today.</summary>
        public static bool Due(bool on, float laidAt, bool ended, float hours, int day, int toldDay) =>
            on && laidAt >= 0f && ended && hours >= NightAtLeast && day != toldDay;

        /// <summary>Whether a wake at this hour (0 to 23) is greeted "Good morning".</summary>
        public static bool Greets(int hour) => hour >= MorningFrom && hour < MorningTo;
    }
}
