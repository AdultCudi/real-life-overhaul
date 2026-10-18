using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.Items;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Controllers;
using LayoutItem = BusinessLayoutSets.Item;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using PlayerActivity;
using RealLifeOverhaul.GameUi;
using Streets;
using TMPro;
using UI;
using UI.InGameUI;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Going into the university: its building read as the school while the game loads it, and the floor's registration.</summary>
    internal sealed partial class CampusInterior
    {
        /// <summary>
        /// Goes in to a floor, and on to a course's classroom (or -1), its class begun there with <paramref name="begin"/>.
        /// Null if the game was asked to go in, else a reason key, logged with why.
        /// </summary>
        public string Enter(int floor, int course, string how, bool begin = false)
        {
            CampusBuilding campus = Campus;
            BuildingManager manager = Manager;
            if (!Available || manager == null)
            {
                return Refused("reallife_study_why_not_university", how, "the university's floors are not there");
            }
            if (!OpenNow())
            {
                return Refused("reallife_campus_why_closed", how, "closed (" + HoursLine(false) + ")");
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                return Refused("reallife_campus_why_vehicle", how, "the player is in or holding a vehicle");
            }
            Vector3 offset = campus.Door - PlayerHelper.GetPosition();
            bool insideOurs = CareersGame.InsideAt(campus.Street, campus.Number);
            if (!insideOurs && (BuildingManager.IsInsideBuilding || new Vector2(offset.x, offset.z).magnitude > WatchReach))
            {
                return Refused("reallife_study_why_not_university", how, "the player is not at its door (" + new Vector2(offset.x, offset.z).magnitude.ToString("0") + " m away)");
            }
            if (manager.enteringBuilding || _mutated)
            {
                return Refused("reallife_study_why_game_busy", how, "the game is already going into a building");
            }
            if (insideOurs && Floor > 0)
            {
                ChangeFloor(floor, course, begin);
                return null;
            }
            _pending = Mathf.Clamp(floor, 1, CampusRules.Floors);
            _goCourse = course;
            _goBegin = begin && course >= 0;
            int doorId = NearestDoor(campus.Building);
            Mutate("asked by " + how);
            _logger?.Info("Careers: going into the university, floor " + _pending + (course >= 0 ? " for " + CareersText.Course(course) : string.Empty) + " (" + how
                          + ", door " + doorId + ", at " + Clock() + ").");
            try
            {
                manager.EnterBuilding(campus.Building.building, false, false, 0, doorId);
            }
            catch (Exception e)
            {
                Restore("the game's going in failed");
                _logger?.Error(e);
                return "reallife_study_why_game_busy";
            }
            return null;
        }

        private string Refused(string key, string how, string why)
        {
            _logger?.Info("Careers: the university refused the player (" + how + ", at " + Clock() + "): " + why + ".");
            return key;
        }

        private static int NearestDoor(CityBuildingController building)
        {
            Vector3 at = PlayerHelper.GetPosition();
            int best = building.entranceDoors[0].doorId;
            float near = float.MaxValue;
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                Transform t = building.entranceDoors[i] != null ? building.entranceDoors[i].doorTransform : null;
                if (t != null && (t.position - at).sqrMagnitude < near)
                {
                    near = (t.position - at).sqrMagnitude;
                    best = building.entranceDoors[i].doorId;
                }
            }
            return best;
        }

        // The building's size and version read as the school's while the game loads the structure.
        private void Mutate(string why)
        {
            Building asset = Campus != null && Campus.Building != null ? Campus.Building.building : null;
            if (asset == null || _mutated)
            {
                return;
            }
            _asset = asset;
            _wasSize = asset.BuildingSize;
            _wasVersion = asset.BuildingVersion;
            asset.BuildingSize = SizeD;
            asset.BuildingVersion = SchoolVersion;
            _mutated = true;
            _director.StartCoroutine(Guard.Routine(Settle()));
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers check (university way in): " + why + ": the building read as D1 until the game has loaded it (was " + _wasSize + " " + _wasVersion + "): PASS");
            }
        }

        private void Restore(string why)
        {
            if (!_mutated)
            {
                return;
            }
            _mutated = false;
            if (_asset != null)
            {
                _asset.BuildingSize = _wasSize;
                _asset.BuildingVersion = _wasVersion;
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers check (university way in): the building's own size put back (" + why + "): PASS");
            }
        }

        // The safety net: whatever happens to the entering, the building's own size is put back when it is over.
        private IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + 15f;
            yield return null;
            while (_mutated && Manager != null && Manager.enteringBuilding && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            yield return null;
            if (_mutated && (Manager == null || !Manager.enteringBuilding))
            {
                Restore("the game's going in ended without the university");
            }
            else if (_mutated)
            {
                Restore("the game took longer than 15 s to go in");
            }
        }

        // While open and the player is near the door: the game's own click on the building is seen before it loads.
        private IEnumerator WatchClick()
        {
            while (_watching)
            {
                BuildingManager manager = Manager;
                if (manager != null && manager.enteringBuilding && !_mutated && !BuildingManager.IsInsideBuilding)
                {
                    _pending = Mathf.Clamp(DefaultFloor(out _goCourse), 1, CampusRules.Floors);
                    _goBegin = false;   // the game's own way in: to the classroom's door, nothing begun
                    Mutate("the game's own way in (a click on the building, or its door)");
                    _logger?.Info("Careers: the player is going into the university by the game's own door, to floor " + _pending + " (at " + Clock() + ").");
                    _watching = false;
                }
                yield return null;
            }
        }

        private int DefaultFloor(out int course)
        {
            if (_director.UniversityBusinessNow(out course))
            {
                return CampusRules.FloorOf(course);
            }
            course = -1;
            int first = _director.FirstUniversityCourse();
            return first >= 0 ? CampusRules.FloorOf(first) : 1;
        }

        private static readonly int HotEntered = HotSpots.Slot("Careers: CampusInterior.Entered");
        private static readonly int HotLeft = HotSpots.Slot("Careers: CampusInterior.Left");

        // The game has entered a building.
        private void Entered(Address address)
        {
            using var hotSpot = HotSpots.Time(HotEntered);
            try
            {
                CampusBuilding campus = Campus;
                bool ours = campus != null && campus.Usable && address != null && address.streetName == campus.Street && address.streetNumber == campus.Number;
                if (!ours)
                {
                    Restore("another building was entered");
                    return;
                }
                if (!_mutated)
                {
                    return;   // the building's own interior: Tick takes the player to a floor (Wrong)
                }
                Restore("the game has loaded the school's structure");
                BuildingManager manager = Manager;
                Floor = _pending;
                _wrongTold = false;
                _spawn = PlayerHelper.GetPosition();
                _shadow = Shadow(manager.buildingRegistration, campus);
                manager.buildingRegistration = _shadow;
                manager.isOpen = OpenNow();
                _director.StartCoroutine(Guard.Routine(Lay(++_built, _goCourse, _goBegin)));
                _goCourse = -1;
                _goBegin = false;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Left(Address address)
        {
            using var hotSpot = HotSpots.Time(HotLeft);
            CampusBuilding campus = Campus;
            if (Floor == 0 || campus == null || address == null || address.streetName != campus.Street || address.streetNumber != campus.Number)
            {
                return;
            }
            _logger?.Info("Careers: the player has left the university (floor " + Floor + ", at " + Clock() + ").");
            Floor = 0;
            _built++;
            _shadow = null;
            for (int i = 0; i < _rooms.Length; i++)
            {
                _rooms[i] = null;
                _chairs[i] = null;
                _signs[i] = null;   // destroyed with the game's indoor items
            }
            _marks.Forget();       // the game let its icons go with the building
            _lift.Hide("left the building");
        }

        // The building entered by its own interior (a save made inside it, or a way in the mod did not see): to a floor, or out.
        private void Wrong(bool open)
        {
            if (_wrongTold)
            {
                return;
            }
            _wrongTold = true;
            if (open)
            {
                _logger?.Info("Careers: the player is inside the university's building without its floors (a save made inside, or a way in the mod did not see); taken to floor 1.");
                _director.StartCoroutine(Guard.Routine(Again()));
            }
            else
            {
                _logger?.Info("Careers: the player is inside the university's building while it is closed; taken out to the street.");
                Manager.ExitFromBuilding(NearestDoor(Campus.Building));
            }
        }

        private IEnumerator Again()
        {
            yield return null;
            string why = Enter(1, -1, "a way back to the floors");
            if (why != null)
            {
                Manager?.ExitFromBuilding(0);
            }
        }

        // The building manager's registration while on a floor: the mod's own, kept nowhere.
        private static BuildingRegistration Shadow(BuildingRegistration real, CampusBuilding campus)
        {
            var r = new BuildingRegistration
            {
                StreetName = campus.Street,
                StreetNumber = campus.Number,
                BusinessName = CareersText.T("reallife_study_campus_university"),
                businessTypeName = CareersGame.SchoolType,
                Layout = SchoolLayout,
                AvailableForRent = false,
                RentedByPlayer = false
            };
            if (real != null)
            {
                r.dirtSpots = real.dirtSpots;
                r.interiorDesigns = real.interiorDesigns;
                r.buildingOwnerRivalId = real.buildingOwnerRivalId;
            }
            r.scheduleDays = new List<ScheduleDay>(7);
            for (int d = 1; d <= 7; d++)
            {
                var day = new ScheduleDay { day = (DayOfWeekOrdered)d, isOpen = CampusRules.OpenDay(d) };
                if (day.isOpen)
                {
                    day.openingHourSlots.Add(new OpeningHourSlot(CampusRules.Opens(d), CampusRules.Closes(d)));
                }
                r.scheduleDays.Add(day);
            }
            return r;
        }
    }
}
