using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// What the rivals seen around the city ask of the rest of the mod, set by Wiring/Wiring.SeenLinks: the tycoons of the rich list as their own file has them, the
    /// save's seed, and the City Ledger's account on Flare. A bridge left unset gives nothing: no tycoons, no post.
    /// </summary>
    public static class SeenBridges
    {
        /// <summary>The rich list's tycoons who have come forward, each as their file has them today (Rivals). Default: none.</summary>
        public static Action<List<SeenSubject>> Tycoons;

        /// <summary>The seed the rivals of this save are drawn from, for a new file's own; 0 while there is none. Default: none.</summary>
        public static Func<int> Seed;

        /// <summary>A line on the City Ledger's Flare account at an hour and minute of today. True if it went up. Default: nothing is posted.</summary>
        public static Func<string, int, int, bool> Post;

        public static void Reset()
        {
            Tycoons = null;
            Seed = null;
            Post = null;
        }
    }
}
