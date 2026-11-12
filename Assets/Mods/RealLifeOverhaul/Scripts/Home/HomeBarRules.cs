namespace RealLifeOverhaul
{
    /// <summary>
    /// When the bars of hygiene, stress and home stand under the game's top bar, and how many of them (Home/HomeDirector asks once a second,
    /// Home/HomeNeedsBar draws). They stand whenever the top bar does, while their option and the needs are on: never by where the player is
    /// or how they are doing, which kept them off a new game away from home and took them away again once a shower put hygiene back over 35.
    /// The home bar waits for a home of the player's own, as the interface HUD's home need does.
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, NeedsPanelChecks.cs). Cost: a comparison a second.
    /// </summary>
    public static class HomeBarRules
    {
        public const int Most = 3;   // hygiene, stress and home: as many as the game's own bars they hang under

        /// <summary>Whether the bars stand: their option and the needs are on. Nothing else decides it.</summary>
        public static bool Shows(bool bars, bool needs) => bars && needs;

        /// <summary>How many of the bars stand, in order: hygiene and stress always, home only with a home of the player's own.</summary>
        public static int Count(bool hasHome) => hasHome ? Most : Most - 1;
    }
}
