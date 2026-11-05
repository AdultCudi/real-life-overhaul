using System;
using BAModAPI;
using Helpers;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// Puts the count of <see cref="NavMapWork"/> on the game's queue of work on the city map's buildings (CityMapFilters.Work), once a session: the step the queue runs for each
    /// building is wrapped, as ClaimedMap wraps it for the mod's own rows, and the game's own step always runs first in the wrap's place. The mod patches no code of the game's and
    /// adds no loop: a count and a call for each building the game works on, only while the map is open. The wrap stays for the session (another wrap may sit over it by then). A game
    /// whose queue has no such step leaves the picture with the queue's own word, said once.
    /// </summary>
    internal static class NavMapFollow
    {
        private static bool _tried;

        /// <summary>Wraps the game's queue's step the first time it is asked in a session; later asks do nothing.</summary>
        internal static void Install(IModLogger logger)
        {
            if (_tried)
            {
                return;
            }
            _tried = true;
            Install(logger, CityMapFilters.Work);
        }

        /// <summary>
        /// Wraps the step of a queue given: the game's (CityMapFilters.Work), or in a check a replica of its class (WorkQueueStep). The contract says whether the queue
        /// still has its step. True when it is counted from now on.
        /// </summary>
        internal static bool Install(IModLogger logger, object work)
        {
            try
            {
                if (!WorkQueueStep.Wrap<CityBuildingController>(work, GameContract.MapWork.Off, NavMapWork.Wrap))
                {
                    logger?.Info("Navigation: the city map's look at its buildings is not as expected; the 2.5D city map reads the highlights when the game's queue has work, "
                                 + "when it ends, and at each mouse release.");
                    return false;
                }
                NavMapWork.Following = true;
                logger?.Info("Navigation: the 2.5D city map counts the buildings the game's filters work on, so a toggle whose work ends in one frame is seen too.");
                return true;
            }
            catch (Exception e)
            {
                logger?.Error(e);
                logger?.Info("Navigation: the city map's look at its buildings could not be followed after the error above; the 2.5D city map reads the highlights when the game's "
                             + "queue has work, when it ends, and at each mouse release.");
                return false;
            }
        }
    }
}
