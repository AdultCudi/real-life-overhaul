using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A made building stood in play joins the city's list as the game's own start of the city joins one at a load (CityManager's
    /// OnScenesLoaded): its controller in CityManager's list of its controllers (the city map's points and filters, the closest
    /// building, a door, the Market's walkers), in the private lookup of those by address (FindCityBuildingController, which BizMan,
    /// the guiders and the building card read), the closest-building job made again from the longer list, and its sign and its
    /// lights set. The game's other lists made once at a load (the city map's search of the businesses by name, built when the
    /// map first opens) are read from the list at their use, so they follow. Nothing here runs a frame: one call a building made in play.
    /// What only a load makes: the minimap's picture of the mod's buildings (read at a load from the mod's folder) and the game's
    /// generator's first businesses for a district building, which a finished building never had.
    /// </summary>
    internal static partial class MadeGame
    {
        /// <summary>
        /// The made building in the city's list, its lookup by address and the closest-building job, its sign and lights set. False,
        /// with why, when the game's pieces for it are missing in this build (then none of it is touched, and the building waits for the next load in the list).
        /// </summary>
        public static bool JoinCity(MadeBuilding made, out string why)
        {
            why = null;
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            CityBuildingController c = made != null ? made.Controller : null;
            if (city == null || c == null || made.Building == null || GameDlc.Open(city) == null)
            {
                why = "the city's list is not there";
                return false;
            }
            var lookup = GameContract.CityLookup.Field?.GetValue(city) as Dictionary<Address, CityBuildingController>;
            if (lookup == null || GameContract.ClosestReset.Method == null)
            {
                why = "this build of the game keeps its list of buildings otherwise (" + (GameContract.CityLookup.Problem ?? GameContract.ClosestReset.Problem ?? "not found") + ")";
                return false;
            }
            Address address = made.Building.Address;
            GameDlc.Join(city, c, address);   // in place of the building knocked down at this address, hidden until the city goes
            lookup[address] = c;
            ResetClosest();   // the job's arrays are as long as the list it was made from: made again, from this one, at its next ask
            Light(c);
            return true;
        }

        // In a method of its own: a game without the piece fails here alone (the contract has it optional).
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ResetClosest()
        {
            GameContract.ClosestReset.Method.Invoke(null, null);
        }

        // Its sign and its lights as the game's start of the city sets every building's.
        public static void Light(CityBuildingController c)
        {
            try
            {
                c.UpdateSign();
                c.UpdateIndoorOutdoorLight();
            }
            catch (Exception e)
            {
                Logger?.Warn("Made buildings: the sign and lights of a building made in play could not be set (" + e.GetType().Name + "); they are set at the next load.");
            }
        }
    }
}
