namespace RealLifeOverhaul
{
    /// <summary>
    /// When a record of the game's pooled objects (by instance id) should start again: the pools destroy and remake
    /// objects with new ids, so a record of every id seen would grow all session. Game-free, checked by NavCheck.
    /// COST: one comparison a look.
    /// </summary>
    public static class CacheRules
    {
        public const int Slack = 32;   // entries kept past twice the live ones before a record starts again

        /// <summary>True if a record of <paramref name="kept"/> entries should start again, <paramref name="live"/> being pooled now.</summary>
        public static bool Stale(int kept, int live)
        {
            return kept > 2 * (live > 0 ? live : 0) + Slack;
        }
    }
}
