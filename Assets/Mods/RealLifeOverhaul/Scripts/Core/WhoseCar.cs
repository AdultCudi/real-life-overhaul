using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whose a vehicle is, and which car of the traffic pool may be put on the road. Going Public shows another player's
    /// car on this machine in the player's list but off the save's, so in a session a car is the player's only if the
    /// save has it (<see cref="Mine"/>); only such a car is brought back after the player is taken from it
    /// (<see cref="BringLater"/>). TrafficManager.LoadVehicle with no idle car of the prefab takes a busy one off the
    /// road, so a car is asked for only when an idle one of that very prefab is there (<see cref="FreeSlot"/>).
    /// Game-free; the pool is walked once a dispatch, nothing allocated.
    /// </summary>
    public static class WhoseCar
    {
        /// <summary>
        /// True for a car of the player's own. <paramref name="sessionRules"/>: a Going Public session is on and is not the
        /// test's pretence. <paramref name="noSave"/>: no save is loaded (nothing to tell by: taken as the player's).
        /// </summary>
        public static bool Mine(bool hasInstance, bool sessionRules, bool noSave, bool inOwnList, bool inDriverList)
        {
            if (!hasInstance)
            {
                return false;
            }
            if (!sessionRules)
            {
                return true;
            }
            return noSave || inOwnList || inDriverList;
        }

        /// <summary>True if the car the player was taken from is brought to them later: there is one, not carried, and theirs.</summary>
        public static bool BringLater(bool hasCar, bool carriedWithPlayer, bool mine)
        {
            return hasCar && !carriedWithPlayer && mine;
        }

        /// <summary>The place in the pool of an idle car of that very prefab object, or -1: then none may be asked for.</summary>
        public static int FreeSlot<T>(IReadOnlyList<T> pool, object prefab, Func<T, object> prefabOf, Func<T, bool> active)
        {
            if (pool == null || prefab == null || prefabOf == null || active == null)
            {
                return -1;
            }
            for (int i = 0; i < pool.Count; i++)
            {
                T car = pool[i];
                if (car != null && ReferenceEquals(prefabOf(car), prefab) && !active(car))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
