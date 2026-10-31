using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// What the population asks of the rest of the mod, filled in by Wiring/PopulationLinks; one left unset gives nothing, and the population goes on
    /// without it. Asked once a game day, as the day's turn begins, on the main thread.
    /// </summary>
    public static class PopulationBridges
    {
        /// <summary>The mod's own people who are residents too (the partner, roommates, household staff, people met, the tech sector's professionals and jobseekers), each by its system's id.</summary>
        public static Action<List<LinkAsk>> People;

        public static void Reset()
        {
            People = null;
        }
    }
}
