using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// What the rivals seen around the city ask of the rest of the mod, set by Wiring/Wiring.SeenLinks: the rivals' own feature and the tycoons of the rich list as
    /// their file has them, the playthrough's own id, and the City Ledger's account on Flare. A bridge left unset gives nothing: no tycoons, no post.
    /// </summary>
    public static class SeenBridges
    {
        /// <summary>
        /// The rivals' own feature now: <see cref="SeenRules.RivalsWait"/> before it has started for this save, <see cref="SeenRules.RivalsOff"/> when it gives no
        /// tycoons (switched off, a fault, not made), <see cref="SeenRules.RivalsOn"/> while it runs. Default: off, so nothing waits for tycoons who cannot come.
        /// </summary>
        public static Func<int> Rivals;

        /// <summary>The rich list's tycoons who have come forward, each as their file has them today (Rivals). Default: none.</summary>
        public static Action<List<SeenSubject>> Tycoons;

        /// <summary>Whether the tycoon at a place of the rich list is still there (come forward, not taken over), asked as the hour turns. Default: yes.</summary>
        public static Func<int, bool> TycoonHere;

        /// <summary>The playthrough's own id, made once with the save (the game's seed of it), for a new file's seed. Default: none.</summary>
        public static Func<string> Playthrough;

        /// <summary>A line on the City Ledger's Flare account at an hour and minute of today. True if it went up. Default: nothing is posted.</summary>
        public static Func<string, int, int, bool> Post;

        public static void Reset()
        {
            Rivals = null;
            Tycoons = null;
            TycoonHere = null;
            Playthrough = null;
            Post = null;
        }
    }
}
