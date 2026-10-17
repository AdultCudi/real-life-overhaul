namespace RealLifeOverhaul
{
    /// <summary>
    /// When a record the mod keeps of the game's pooled objects (by instance id) has outgrown the pool: the game's pools
    /// destroy what they overflow with and make new objects, each with an id of its own, so a record of every id ever seen
    /// grows all session and keeps the destroyed objects' managed parts alive. Past twice what the pools hold now, and a
    /// little more, the record starts again: what is still there is looked up again on the next look. Game-free, checked
    /// offline (NavCheck, MemoryCheck.cs). COST: one comparison a look.
    /// </summary>
    public static class CacheRules
    {
        public const int Slack = 32;   // entries kept past twice the live ones before a record starts again

        /// <summary>True if a record of so many entries should start again, the pools holding so many objects now.</summary>
        public static bool Stale(int kept, int live)
        {
            return kept > 2 * (live > 0 ? live : 0) + Slack;
        }
    }
}
