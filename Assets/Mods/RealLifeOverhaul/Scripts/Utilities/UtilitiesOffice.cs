using System;
using BAModAPI;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The city's utilities office at 13 7th Street, Midtown (Core/CityReserves), its desk at the door; meanwhile elsewhere
    /// while a player holds it, and nowhere while every building that could hold it is a player's (the app takes the
    /// desk's work by phone then: <see cref="ByPhone"/>). Looked for once a city load, then a few reads every five
    /// seconds, and one walk over the city every half minute while there is no office.
    /// </summary>
    public sealed class UtilitiesOffice
    {
        private const float ReachAtDoor = 2.4f;     // metres: how near the door front the player must be to speak to the desk
        private const int LookAgainSeconds = 30;    // seconds between searches while no building can hold it: a building let go is found within half a minute, one walk over the city each time

        private readonly IModLogger _logger;
        private bool _failed;
        private bool _usable;
        private bool _held;     // a test holds the office away, as if no building could hold it
        private bool _none;     // the last search found no building: told once, looked for again, and quiet about the move when found
        private int _look;

        public UtilitiesOffice(IModLogger logger)
        {
            _logger = logger;
        }

        public bool Searched { get; private set; }

        /// <summary>True while the desk can be visited.</summary>
        public bool Usable => _usable && !_held;

        /// <summary>
        /// True while there is no desk to visit (no building could hold it, no pavement at its door, it failed, or a test
        /// holds it away): accounts are applied for and reconnected in the app, by phone, on the desk's rules.
        /// </summary>
        public bool ByPhone => !Usable;

        /// <summary>Test: the office held away (true), as if every building that could hold it were a player's, or given back.</summary>
        public void Hold(bool away)
        {
            _held = away;
        }

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
            if (Searched && Building == null)
            {
                if (++_look < LookAgainSeconds)
                {
                    return;
                }
                _look = 0;   // and looked for again below: a building may have been let go
            }
            else if (Searched)
            {
                if (++_look >= 5)
                {
                    _look = 0;
                    CityManager now = InstanceBehavior<CityManager>.Instance;
                    bool back = !CivicHost.IsFixed(Building) && now != null && CivicHost.FixedServes(GameDlc.Open(now));
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
                CityBuildingController[] buildings = GameDlc.Open(city);
                if (buildings == null || buildings.Length == 0)
                {
                    return;
                }
                CityBuildingController picked = CivicHost.Pick(buildings, out bool fixedOne);
                Searched = true;
                if (picked == null)
                {
                    if (!_none)
                    {
                        _none = true;
                        _logger?.Warn("Utilities: no building could serve as the office; accounts are applied for by phone (the Utilities app) instead, and the office is looked for again every "
                                      + LookAgainSeconds + " s.");
                    }
                    return;
                }
                // back after a spell with no office: the director tells the player the desk is open again, so the move is not told twice
                bool back = _none;
                _none = false;
                _look = 0;
                Building = picked;
                Moves++;
                Name = picked.building.Address.ToFormattedString();
                if (JobSpots.Front(picked, out Vector3 door))
                {
                    Front = door;
                    _usable = true;
                    SpecialBuildings.Set(CityReserves.UtilitiesId, picked, SpecialKind.Civic, "reallife_util_desk_title", "reallife_util_card_type", () => Settings.Utilities);
                    PlaceMoves.Chosen(CityReserves.UtilitiesKey, UtilitiesStore.Key, CityReserves.OldUtilities, picked.building.Address.streetName, picked.building.Address.streetNumber, Name,
                        "reallife_util_note_moved", "Utilities: the office", _logger, !back);
                }
                Meta.Dev.Always(_logger, "Utilities: the office is inside " + Name + " (" + (fixedOne ? "its permanent building" : "meanwhile: 13 7th Street is a player's") + "; "
                                         + picked.building.totalSqm + " sqm); " + (Usable ? "the desk is at the door." : "no pavement at its door: accounts are applied for in the app."));
            }
            catch (Exception e)
            {
                _failed = true;
                Searched = true;
                _usable = false;
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
            _usable = false;
        }
    }
}
