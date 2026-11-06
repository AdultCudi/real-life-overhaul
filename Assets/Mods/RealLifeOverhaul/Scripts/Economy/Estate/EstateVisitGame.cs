using System;
using BAModAPI;
using Buildings;
using Helpers;
using PlayerActivity;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.GameUi;
using Streets;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A tenant's repair the player does himself: the game's destination marker on the building's door, the game's panel
    /// there with the job, and the job in the game's own work panel (Jobs/ShiftSkip) with its clock. Told by the director
    /// once a second while such a job waits, and nothing at all otherwise.
    /// </summary>
    internal sealed class EstateVisitGame : ISkippableShift
    {
        private const float Reach = 3.5f;   // m from the spot in front of the door

        private readonly EstateDirector _director;
        private readonly DoorPanel _door;
        private readonly GuideMark _mark = new GuideMark("a tenant's repair");
        private readonly Action _onPress;
        private ShiftSkip _skip;
        private string _place, _found;   // the job's building, and the building whose door was found
        private Vector3 _front;
        private bool _hasFront;
        private int _unit, _minutes, _materials;
        private string _title = string.Empty, _what = string.Empty;

        public EstateVisitGame(EstateDirector director, IModLogger logger)
        {
            _director = director;
            _door = new DoorPanel(logger, "Estate check (repair panel)");
            _onPress = _door.Press(Pressed);
            _mark.Keep(director);   // goes with the director, or when the guard stops it
        }

        public bool TakesKey => _door.TakesKey;

        /// <summary>True while the game's work panel has the job (offered or under way).</summary>
        public bool Busy => _skip != null && _skip.Open;

        /// <summary>
        /// Once a second while a job waits: the marker on its door, and the panel there while the player stands at it on
        /// foot with nothing else open. <paramref name="title"/> and <paramref name="what"/> are the panel's words.
        /// </summary>
        public void Second(string place, int unit, string title, string what, int minutes, int materials)
        {
            _place = place;
            _unit = unit;
            _title = title;
            _what = what;
            _minutes = Math.Max(1, minutes);
            _materials = materials;
            if (_skip != null && !_skip.Open)
            {
                _skip = null;
            }
            if (_found != place)
            {
                _found = place;
                CityBuildingController building = FindBuilding(place);
                _hasFront = building != null && JobSpots.Front(building, out _front);
            }
            if (!_hasFront)
            {
                _door.Hide("the building has no pavement at its door");
                _mark.Hide();
                return;
            }
            _mark.Show(_front + Vector3.up * 1.4f, title);
            PlayerController player = PlayerHelper.PlayerController;
            Vector3 at = player != null ? player.transform.position : Vector3.zero;
            Vector3 offset = _front - at;
            bool near = offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < Reach * Reach;
            bool free = player != null && !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && !ThirdPersonCamera.IsUiOpen() && !PlayerActivityUI.IsPanelOpen
                        && !JobDirector.TakesInteractKey && !CareersDirector.TakesInteractKey && _skip == null;
            if (!near || !free)
            {
                _door.Hide(near ? "something else is open" : "the player left the door");
                return;
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(title, what, EstateText.Money(materials), WorldPanel.White());
            panel.Button(0, "reallife_estate_visit_button", _onPress, true, true);
            _door.Show(_front, at, 1.4f, () => "a repair at " + title);
        }

        /// <summary>No job waits now, or the feature stands down: the panel, the marker and the work panel are let go.</summary>
        public void Close(string why)
        {
            _door.Hide(why);
            _mark.Hide();
            if (_skip != null)
            {
                ShiftSkip skip = _skip;
                _skip = null;
                skip.Close();
            }
            _found = null;
        }

        private static CityBuildingController FindBuilding(string place)
        {
            Address address = EstateGame.AddressOf(place);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            try
            {
                return address != null && city != null ? city.FindCityBuildingController(address) : null;
            }
            catch (Exception)
            {
                return null;   // the city is going: no door to show
            }
        }

        private void Pressed()
        {
            _door.Hide("the job was taken on");
            if (_skip == null && _place != null)
            {
                _skip = ShiftSkip.Offer(_director, this);
            }
        }

        string ISkippableShift.SkipTitle => _title;

        int ISkippableShift.SkipMinutes => _minutes;

        LabelInfo[] ISkippableShift.SkipLabels()
        {
            return new[]
            {
                DeskGame.Label(_what, 0),
                DeskGame.Label(EstateText.T("reallife_estate_visit_minutes", "minutes", _minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)), 0),
                DeskGame.Label(EstateText.T("reallife_estate_visit_materials", "amount", EstateText.Money(_materials)), 1)
            };
        }

        void ISkippableShift.SkipStarted()
        {
        }

        void ISkippableShift.SkipWorked(int minutes)
        {
        }

        void ISkippableShift.SkipEnded(int minutes, bool whole)
        {
            string place = _place;
            int unit = _unit;
            _skip = null;
            if (whole && place != null)
            {
                _director.YourselfDone(place, unit);   // stopped early, the job waits for another go
            }
        }
    }
}
