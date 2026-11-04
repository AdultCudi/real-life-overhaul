using System;
using System.Reflection;
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
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private static bool _tried;

        /// <summary>Wraps the queue's step the first time it is asked in a session; later asks do nothing.</summary>
        internal static void Install(IModLogger logger)
        {
            if (_tried)
            {
                return;
            }
            _tried = true;
            try
            {
                DistributedWork<CityBuildingController> work = CityMapFilters.Work;
                // the contract says whether the queue still has its step; the field is read on the closed type itself
                FieldInfo field = GameContract.MapWork.Off ? null : typeof(DistributedWork<CityBuildingController>).GetField("_updateAction", Hidden);
                var step = field != null && work != null ? field.GetValue(work) as Action<CityBuildingController> : null;
                if (step == null)
                {
                    logger?.Info("Navigation: the city map's look at its buildings is not as expected; the 2.5D city map reads the highlights when the game's queue has work, "
                                 + "when it ends, and at each mouse release.");
                    return;
                }
                field.SetValue(work, NavMapWork.Wrap(step));
                NavMapWork.Following = true;
                logger?.Info("Navigation: the 2.5D city map counts the buildings the game's filters work on, so a toggle whose work ends in one frame is seen too.");
            }
            catch (Exception e)
            {
                logger?.Error(e);
                logger?.Info("Navigation: the city map's look at its buildings could not be followed after the error above; the 2.5D city map reads the highlights when the game's "
                             + "queue has work, when it ends, and at each mouse release.");
            }
        }
    }
}
