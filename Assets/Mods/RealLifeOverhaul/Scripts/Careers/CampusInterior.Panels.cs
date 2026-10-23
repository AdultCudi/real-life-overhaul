using System;
using System.Collections.Generic;
using Helpers;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.InGameUI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The game's card over the university's building and the game's refusals at its door. Its panels at the door and the lift
    /// are the floors core's (Floors/FloorsDirector.Panels), with the university's words (CampusHost, CampusFloors).
    /// </summary>
    internal sealed partial class CampusInterior
    {
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
                        // written again when the card turned to it, the hours changed, or the game wrote its own line over it (BuildingResume.UpdateDetails)
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
