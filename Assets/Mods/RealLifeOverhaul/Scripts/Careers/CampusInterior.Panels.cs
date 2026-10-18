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
    /// <summary>The university's panels at the door and the lift, its doors, the building card and the game's refusals at its door.</summary>
    internal sealed partial class CampusInterior
    {
        // The panel at the door outside: the university's name, its hours, and a button a floor.
        private void DoorPanel(bool open, PlayerController player)
        {
            WorldPanel panel = _door.Panel;
            panel.Lines(CareersText.T("reallife_study_campus_university"), HoursLine(open), (open ? "common_open" : "common_closed").GetLocalization(), open ? WorldPanel.Green() : WorldPanel.Red());
            int classFloor = _director.UniversityBusinessNow(out int classCourse) ? CampusRules.FloorOf(classCourse) : 0;
            FloorButtons(panel, _doorPress, _doorFloor, _doorPage, 0, open, classFloor, classCourse);
            Vector3 standing = player.transform.position;
            _doorWhatOpen = open;
            if (_door.Show(Campus.Door, standing, 2.2f, _doorWhat ?? (_doorWhat = () => "at the door of " + Campus.Name + (_doorWhatOpen ? ", open" : ", closed")))
                && !open && _toldClosed != SaveGameManager.Current.Day * 100 + SaveGameManager.Current.Hour)
            {
                _toldClosed = SaveGameManager.Current.Day * 100 + SaveGameManager.Current.Hour;
                _logger?.Info("Careers: the player is at the university's door at " + Clock() + " and it is closed (" + HoursLine(false) + "); the floors are offered greyed.");
            }
        }

        // The lift by the entrance inside: the other floors.
        private void LiftPanel(PlayerController player)
        {
            Vector3 standing = player.transform.position;
            Vector3 offset = _spawn - standing;
            bool busy = ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen || _director.SittingNow;
            if (busy || new Vector2(offset.x, offset.z).magnitude > LiftReach || Mathf.Abs(offset.y) > 2f || CampusRules.Floors < 2)
            {
                _lift.Hide("away from the lift");
                return;
            }
            WorldPanel panel = _lift.Panel;
            panel.Lines(CareersText.T("reallife_study_campus_university"), CareersText.T("reallife_campus_lift_here", "floor", Floor.ToString()), null, WorldPanel.White());
            int classFloor = _director.UniversityBusinessNow(out int classCourse) ? CampusRules.FloorOf(classCourse) : 0;
            FloorButtons(panel, _liftPress, _liftFloor, _liftPage, Floor, true, classFloor, classCourse);
            _lift.Show(_spawn, standing, 2.1f, _liftWhat ?? (_liftWhat = () => "the lift on floor " + Floor));
        }

        // Up to four buttons: the floors (leaving out the one the player is on), or three and "more floors" when they do not fit.
        private void FloorButtons(WorldPanel panel, Action[] press, int[] floors, int page, int skip, bool enabled, int classFloor, int classCourse)
        {
            List<int> list = _floorList;
            list.Clear();
            for (int f = 1; f <= CampusRules.Floors; f++)
            {
                if (f != skip)
                {
                    list.Add(f);
                }
            }
            bool paged = list.Count > 4;
            int per = paged ? 3 : 4;
            int start = paged ? (page * per) % list.Count : 0;
            for (int slot = 0; slot < 4; slot++)
            {
                if (paged && slot == 3)
                {
                    floors[slot] = -1;
                    panel.Button(slot, "reallife_campus_floor_more", press[slot], true, false);
                    continue;
                }
                int i = start + slot;
                if (i >= list.Count || slot >= per)
                {
                    floors[slot] = 0;
                    panel.Button(slot, null, null, false, false);
                    continue;
                }
                int floor = list[i];
                floors[slot] = floor;
                bool here = floor == classFloor;
                string key = here ? "reallife_campus_floor_class" : "reallife_campus_floor";
                panel.Button(slot, key, Args(here, floor, here ? classCourse : -1), press[slot], enabled, here || slot == 0);
            }
        }

        // A floor button's arguments, made once for each (the class's key or the floor's, the floor, the course).
        private object Args(bool here, int floor, int course)
        {
            long id = ((here ? 1L : 0L) << 62) | ((long)(uint)floor << 31) | (uint)(course + 1);
            if (!_args.TryGetValue(id, out object args))
            {
                args = course >= 0 ? (object)new { floor = floor.ToString(), course = CareersText.Course(course) } : new { floor = floor.ToString() };
                _args[id] = args;
            }
            return args;
        }

        private void DoorPressed(int slot)
        {
            if (_doorFloor[slot] < 0)
            {
                _doorPage++;
                return;
            }
            if (_doorFloor[slot] > 0)
            {
                int course = _director.UniversityBusinessNow(out int now) && CampusRules.FloorOf(now) == _doorFloor[slot] ? now : -1;
                string why = Enter(_doorFloor[slot], course, "the floor button at the door");
                if (why != null)
                {
                    Tell(why);
                }
            }
        }

        private void LiftPressed(int slot)
        {
            if (_liftFloor[slot] < 0)
            {
                _liftPage++;
                return;
            }
            if (_liftFloor[slot] > 0)
            {
                ChangeFloor(_liftFloor[slot], -1);
            }
        }

        // While it is open the building's doors are the game's walk-in kind ("always open"), so its own click goes in.
        private void Doors(bool open)
        {
            CityBuildingController b = _doorsOf;
            if (b == null || b.entranceDoors == null || open == _doorsOpen)
            {
                return;
            }
            if (_doorWas == null)
            {
                _doorWas = new bool[b.entranceDoors.Length];
                for (int i = 0; i < b.entranceDoors.Length; i++)
                {
                    _doorWas[i] = b.entranceDoors[i] != null && b.entranceDoors[i].alwaysOpen;
                }
            }
            for (int i = 0; i < b.entranceDoors.Length; i++)
            {
                if (b.entranceDoors[i] != null)
                {
                    b.entranceDoors[i].alwaysOpen = open || (i < _doorWas.Length && _doorWas[i]);
                }
            }
            _doorsOpen = open;
            _logger?.Info("Careers: the university's doors are " + (open ? "open" : "shut") + " (" + Clock() + ", " + HoursLine(open) + ").");
        }

        /// <summary>The game's card over the building: the university's hours in place of the game's line, and no owners' lines.</summary>
        public void Card(bool force)
        {
            BuildingResume card = _card;
            if (card == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                card = _card = uis != null ? uis.buildingResume : null;   // kept: the card lives as long as the city
                if (card == null)
                {
                    return;
                }
            }
            CampusBuilding campus = Campus;
            if (campus == null || campus.Building == null)
            {
                return;
            }
            EntityController target = GameMembers.TargetOf(card);
            if (!force && ReferenceEquals(target, _carded))
            {
                return;
            }
            bool turned = !ReferenceEquals(target, _carded);
            _carded = target;
            if (!ReferenceEquals(target, campus.Building) || !CareersDirector.UniversityOpen || SpecialBuildings.NameFor(campus.Building) == null)
            {
                return;
            }
            try
            {
                bool open = OpenNow();
                if (card.openStateLabel != null)
                {
                    card.openStateLabel.gameObject.SetActive(true);
                    long hours = HoursStamp(open);
                    TMP_Text shown = card.openStateLabel.TextContainer;
                    if (turned || hours != _cardHours || shown == null || !ReferenceEquals(shown.text, _cardText))
                    {
                        // written again when the card turned to it, the hours changed, or the game wrote its own line over it (on the hour and
                        // when a building changes hands: BuildingResume.UpdateDetails)
                        card.openStateLabel.Prefix = string.Empty;
                        card.openStateLabel.SetData(LanguageChangeEventDataHolder.Create("reallife_campus_card_state", new { state = HoursLine(open) }, "common_closed"));
                        card.openStateLabel.TextContainer.color = open ? WorldPanel.Green() : WorldPanel.Red();
                        _cardHours = hours;
                        _cardText = card.openStateLabel.TextContainer.text;
                    }
                }
                card.rivalBuildingOwner?.gameObject.SetActive(false);
                card.rivalBusinessOwner?.gameObject.SetActive(false);
                if (card.clickToEnterLabel != null)
                {
                    card.clickToEnterLabel.gameObject.SetActive(open && !CityMap.IsOpen && !PlayerHelper.IsUsingVehicle);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void CardNow() => Card(true);

        private static readonly int HotNoticed = HotSpots.Slot("Careers: CampusInterior.Noticed");

        // The game's own refusal at a door: when it is the university's, said in the log and to the player with the hours.
        private void Noticed(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action click, bool sound, bool track)
        {
            using var hotSpot = HotSpots.Time(HotNoticed);
            if (key != "buildingmanager_notification_business_closed" && key != "buildingmanager_notification_private_property" && key != "educationdoorcontroller_closed")
            {
                return;
            }
            try
            {
                CampusBuilding campus = Campus;
                if (campus == null || !campus.Usable || !Available)
                {
                    return;
                }
                bool inside = CareersGame.InsideAt(campus.Street, campus.Number);
                Vector3 offset = campus.Door - PlayerHelper.GetPosition();
                if (!inside && (BuildingManager.IsInsideBuilding || new Vector2(offset.x, offset.z).magnitude > WatchReach))
                {
                    return;
                }
                _logger?.Info("Careers: the game refused the player at the university (" + key + ", at " + Clock() + "): " + (inside ? "a classroom door while the classrooms are shut"
                              : "its own door: the building holds no business of the game's, so the game's rule is that it is shut") + "; the university is " + (OpenNow() ? "open" : "closed")
                              + " (" + HoursLine(OpenNow()) + ").");
                if (!OpenNow())
                {
                    Tell("reallife_campus_why_closed");
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }
    }
}
