using System;
using BAModAPI;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The city's utilities office at 13 7th Street, Midtown (Core/CityReserves), its desk at the door; meanwhile elsewhere
    /// while a player holds it. Looked for once a city load, then a few reads every five seconds.
    /// </summary>
    public sealed class UtilitiesOffice
    {
        private const float ReachAtDoor = 2.4f;     // metres: how near the door front the player must be to speak to the desk

        private readonly IModLogger _logger;
        private bool _failed;
        private int _look;

        public UtilitiesOffice(IModLogger logger)
        {
            _logger = logger;
        }

        public bool Searched { get; private set; }

        public bool Usable { get; private set; }

        /// <summary>Where the player stands to speak to the desk: in front of the door.</summary>
        public Vector3 Front { get; private set; }

        /// <summary>The office's building; null until found (it lights up on the city map: MapPlaces).</summary>
        public CityBuildingController Building { get; private set; }

        /// <summary>The address as the game writes it; empty until found.</summary>
        public string Name { get; private set; } = string.Empty;

        public bool InReach(Vector3 player)
        {
            if (!Usable)
            {
                return false;
            }
            Vector3 offset = Front - player;
            return offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < ReachAtDoor * ReachAtDoor;
        }

        /// <summary>Changes each time the office is chosen (for the map).</summary>
        public int Moves { get; private set; }

        /// <summary>Once a second: finds the building; then every five seconds moves off one a player took, or back home.</summary>
        public void Tick()
        {
            if (_failed)
            {
                return;
            }
            if (Searched)
            {
                if (Building != null && ++_look >= 5)
                {
                    _look = 0;
                    CityManager now = InstanceBehavior<CityManager>.Instance;
                    bool back = !CivicHost.IsFixed(Building) && now != null && CivicHost.FixedServes(now.cityBuildingControllers);
                    if (SpecialBuildings.Players(Building) || back)
                    {
                        _logger?.Info("Utilities: the office's building, " + Name
                                      + (back ? ", was meanwhile: 13 7th Street is free again; the office moves back for good." : ", is the player's; the office moves to another."));
                        Dispose();
                        Building = null;
                        Name = string.Empty;
                        Searched = false;
                    }
                }
                return;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
                if (buildings == null || buildings.Length == 0)
                {
                    return;
                }
                CityBuildingController picked = CivicHost.Pick(buildings, out bool fixedOne);
                Searched = true;
                if (picked == null)
                {
                    _logger?.Warn("Utilities: no building could serve as the office; accounts are applied for by phone (the Utilities app) instead.");
                    return;
                }
                Building = picked;
                Moves++;
                Name = picked.building.Address.ToFormattedString();
                if (JobSpots.Front(picked, out Vector3 door))
                {
                    Front = door;
                    Usable = true;
                    SpecialBuildings.Set(CityReserves.UtilitiesId, picked, SpecialKind.Civic, "reallife_util_desk_title", "reallife_util_card_type", () => Settings.Utilities);
                    PlaceMoves.Chosen(CityReserves.UtilitiesKey, UtilitiesStore.Key, CityReserves.OldUtilities, picked.building.Address.streetName, picked.building.Address.streetNumber, Name,
                        "reallife_util_note_moved", "Utilities: the office", _logger);
                }
                Meta.Dev.Always(_logger, "Utilities: the office is inside " + Name + " (" + (fixedOne ? "its permanent building" : "meanwhile: 13 7th Street is a player's") + "; "
                                         + picked.building.totalSqm + " sqm); " + (Usable ? "the desk is at the door." : "no pavement at its door: accounts are applied for in the app."));
            }
            catch (Exception e)
            {
                _failed = true;
                Searched = true;
                Usable = false;
                _logger?.Error(e);
                _logger?.Warn("Utilities: the office could not be set up after the error above; accounts are applied for in the app.");
            }
        }

        public void Dispose()
        {
            if (Building != null)
            {
                SpecialBuildings.Remove(CityReserves.UtilitiesId);
            }
            Usable = false;
        }
    }
}
