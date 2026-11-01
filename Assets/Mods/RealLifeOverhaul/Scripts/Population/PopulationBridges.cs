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
        /// <summary>
        /// The mod's own people who are residents too (the partner, roommates, household staff, people met, the tech sector's professionals and
        /// jobseekers), each by its system's id.
        /// </summary>
        public static Action<List<LinkAsk>> People;

        /// <summary>
        /// A building's use in the mod's records where it differs from the game's (the estate's change of use of a building of the player's: the base
        /// districts answer demand by a change of use, never new geometry, POPULATION.md section 14), as a Use; -1 for the game's own. Read at a load.
        /// </summary>
        public static Func<string, int, int> UseOf;

        /// <summary>
        /// The homes floor a district's own land can still take, m2 (its free land and the building code's room over its homes), by its id; -1 for a district
        /// the mod builds nothing new in (the game's own, POPULATION.md section 14). Asked at a month's turn.
        /// </summary>
        public static Func<string, int> LandHomes;

        public static void Reset()
        {
            People = null;
            UseOf = null;
            LandHomes = null;
        }
    }
}
