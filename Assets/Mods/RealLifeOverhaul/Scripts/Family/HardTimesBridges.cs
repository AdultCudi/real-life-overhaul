namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What the other features know of the player's days that a difficult moment can come from. Each call fills in only
    /// what its features keep, and leaves the rest as it was given (unknown, so no moment comes of it). Never throws: a
    /// fault is the wiring's to log, and the figures stay unknown.
    /// </summary>
    public interface IHardTimesBridge
    {
        /// <summary>
        /// Home life, careers, the social life and travel, for the partner by this name: field reads, once a game hour
        /// while moments are weighed.
        /// </summary>
        void Days(ref HardSigns signs, string partner);

        /// <summary>
        /// The economy: net worth, the player's businesses, and the last three days' purchases and borrowing from the
        /// game's records. Once a game day, and again before a moment that comes of money.
        /// </summary>
        void Money(ref HardSigns signs);
    }

    /// <summary>
    /// The door the wiring fills in (Wiring/HardTimesLinks); null until then, and the moments go by what family life
    /// and the game keep.
    /// </summary>
    public static class HardTimesBridges
    {
        public static IHardTimesBridge Life;
    }
}
