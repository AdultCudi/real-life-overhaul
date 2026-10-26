using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Buildings;
using Entities;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul
{
    /// <summary>
    /// THE DLC RULE: the mod never picks, claims, converts, copies, sends a delivery to or uses anything the game keeps
    /// behind a DLC (a building's or a boat type's requiredDLC other than None, which the game refuses to a player who
    /// does not own it: CityBuildingController, BizManPresentation's "common_requires_dlc"), and never asks which DLCs
    /// anybody owns. Players of one Going Public session may own different DLCs, and the game hides a scene object by
    /// ownership (ToggleObjectDLC), so CityManager's list itself can differ between their machines: a pick made without
    /// what a DLC keeps is the same on every one. Every pick of the mod's from the game's buildings goes through here:
    /// the city's list through <see cref="Open"/>, one building, registration or boat type through <see cref="Gated(Building)"/>
    /// and its kin. ContractCheck fails a source that reads CityManager's list, requiredDLC or DLC ownership anywhere
    /// else. Build 3682 keeps no building behind a DLC, so today <see cref="Open"/> hands back the game's own list.
    /// COST: a field read per building asked; <see cref="Open"/> a reference comparison, and one walk over the list when
    /// the game lists the city's buildings anew (once a city load); nothing allocated while no building is gated.
    /// </summary>
    public static class GameDlc
    {
        private static CityBuildingController[] _source;   // the game's list the open one was made from
        private static CityBuildingController[] _open;     // that list without what a DLC keeps; the game's own list while none is
        private static bool _noGate;                        // this game has no requiredDLC as the mod reads it: nothing is taken to be gated

        /// <summary>How many buildings of the city's list a DLC keeps, as last counted by <see cref="Open"/>.</summary>
        public static int GatedInCity { get; private set; }

        /// <summary>True when the game keeps the building behind a DLC. Reads the building's data, never ownership.</summary>
        public static bool Gated(Building building)
        {
            if (_noGate || building == null)
            {
                return false;
            }
            try
            {
                return BuildingDlc(building) != 0;
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Lacks(e, "a building's DLC");
                return false;
            }
        }

        public static bool Gated(CityBuildingController building)
        {
            return building != null && Gated(building.building);
        }

        public static bool Gated(BuildingRegistration registration)
        {
            return registration != null && Gated(GameRegistrations.BuildingOf(registration));
        }

        /// <summary>True when the game keeps the kind of boat behind a DLC. Reads the boat type's data, never ownership.</summary>
        public static bool Gated(BoatType boat)
        {
            if (_noGate || boat == null)
            {
                return false;
            }
            try
            {
                return BoatDlc(boat) != 0;
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Lacks(e, "a boat type's DLC");
                return false;
            }
        }

        /// <summary>
        /// The city's buildings the mod may pick from: CityManager's list without what a DLC keeps, the game's own array
        /// while nothing is kept (as in Build 3682), else a copy made once per list the game makes. Null while the city has
        /// no list. The array is the game's or shared: read it, never change it.
        /// </summary>
        public static CityBuildingController[] Open(CityManager city)
        {
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            if (all == null)
            {
                return null;
            }
            if (!ReferenceEquals(all, _source))
            {
                _source = all;
                _open = Without(all);
            }
            return _open;
        }

        /// <summary>
        /// A building made in play put in the city's list, at its end, in place of any building listed at the same address (one
        /// knocked down there, hidden); everything the game keeps behind a DLC stays in the list, which <see cref="Open"/> then
        /// leaves out of the mod's picks as before. A new array, so the game's own readers (the map, the closest building) see it
        /// whole. False when the city has no list. The caller makes the game's closest-building job again from it.
        /// </summary>
        public static bool Join(CityManager city, CityBuildingController made, Address address)
        {
            CityBuildingController[] was = city != null ? city.cityBuildingControllers : null;
            if (was == null || made == null)
            {
                return false;
            }
            city.cityBuildingControllers = CityList.With(was, made, each => each.building != null && each.building.Address == address);
            return true;
        }

        /// <summary>
        /// A building knocked down in play taken out of the city's list: the map's filters walk that list, so a controller left in it is lit
        /// again at every opening of the map, a marker on ground with no building. A new array, so the game's own readers see it whole;
        /// the caller makes the game's closest-building job again from it. True when it was in the list; false when it was not (nothing changed).
        /// </summary>
        public static bool Leave(CityManager city, CityBuildingController gone)
        {
            CityBuildingController[] was = city != null ? city.cityBuildingControllers : null;
            if (was == null || gone == null)
            {
                return false;
            }
            CityBuildingController[] now = CityList.Without(was, gone, out bool removed);
            if (removed)
            {
                city.cityBuildingControllers = now;
            }
            return removed;
        }

        // One walk over a list the game has just made; a copy only if a DLC keeps any of it.
        private static CityBuildingController[] Without(CityBuildingController[] all)
        {
            int gated = 0;
            for (int i = 0; i < all.Length; i++)
            {
                gated += Gated(all[i]) ? 1 : 0;
            }
            GatedInCity = gated;
            if (gated == 0)
            {
                return all;
            }
            var open = new CityBuildingController[all.Length - gated];
            for (int i = 0, n = 0; i < all.Length; i++)
            {
                if (!Gated(all[i]))
                {
                    open[n++] = all[i];
                }
            }
            Guard.Logger?.Info("DLC rule: " + gated + " of the city's " + all.Length + " buildings are kept behind a DLC and are never picked by the mod.");
            return open;
        }

        private static void Lacks(Exception e, string what)
        {
            _noGate = true;
            Guard.DoesWithout(e, what, "no building or boat is taken to be kept behind a DLC");
        }

        // In methods of their own: a game whose DLC field has changed fails here alone (Mono compiles a method whole).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int BuildingDlc(Building building)
        {
            return (int)building.requiredDLC;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int BoatDlc(BoatType boat)
        {
            return (int)boat.requiredDLC;
        }
    }
}
