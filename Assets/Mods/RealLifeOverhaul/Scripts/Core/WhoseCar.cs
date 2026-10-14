using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whose a vehicle is, and which car of the traffic pool may be put on the road: the decisions alone, with nothing of the
    /// game in them, so that NavCheck (CarCheck.cs) checks the very code the mod runs by.
    ///
    /// THE PLAYER'S OWN (<see cref="Mine"/>, for Core/GoingPublic.MineVehicle). Going Public, the multiplayer mod, shows
    /// another player's car on this machine as a car of the game's player's list, and a player may drive it: it is spawned
    /// with an id of "BAMP_" and the owner's id and taken off the save's own list (its VehicleManager.SpawnRemoteVehicle and
    /// DeregisterGhostFromSave). So in a session a car is the player's only if the save has it, among their cars or their
    /// private drivers'. Outside a session, and while the test pretends one, every car of the list is.
    ///
    /// A CAR LEFT BEHIND (<see cref="BringLater"/>). When the player is taken away from the car they drive (to hospital, into
    /// custody, to prison), the car is remembered and brought to them later with a fee (Driving/VehicleTow). Only their
    /// own: another player's car is left where it stopped, untouched and unbilled, as it stands on its owner's machine.
    /// Not one carried with the player either (a bike the game keeps on the player).
    ///
    /// A CAR OF THE POOL (<see cref="FreeSlot"/>). The game's TrafficManager.LoadVehicle takes the first idle car of the
    /// prefab it is given; with none idle it takes the first car of that name in the pool, whatever it is doing (driving in
    /// view, held at a scene, somebody's ride), and DensityManager.AddVehicleAtPositionNow takes it off the road to put it
    /// where it was asked. So the mod asks for a car only when an idle one of that very prefab is there.
    ///
    /// A handful of comparisons; the pool is walked once, on a dispatch, never per frame. Nothing is allocated.
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

        /// <summary>True if the car the player was taken from is to be brought to them later: there is one, it is not carried with them, and it is theirs.</summary>
        public static bool BringLater(bool hasCar, bool carriedWithPlayer, bool mine)
        {
            return hasCar && !carriedWithPlayer && mine;
        }

        /// <summary>
        /// The place in the pool of an idle car of that very prefab (the same object, not one of the same name), or -1 if
        /// there is none: then no car of that kind may be asked for.
        /// </summary>
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
