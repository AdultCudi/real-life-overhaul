using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using Parking.UndergroundParking;
using Tooltip;
using UI;
using UI.CurrentBuilding;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A bicycle or a motorbike called by its own name wherever the game names the vehicle, never "electric scooter" (to
    /// the game and in the save each one is a scooter: see <see cref="TwoWheelers"/>). Where the game names the thing the
    /// pointer is on, the bike's own header does it (<see cref="BikeMount"/>); the map marker and the mod's own lists use
    /// the bike's name already. This looks after the three places the game writes the scooter's name itself:
    /// - the HUD's vehicle panel while one is ridden: the game writes the name when it shows the panel (on getting on,
    ///   after a tow, after placing something); each frame of a ride the panel's key is compared with the scooter's and,
    ///   when the game has written it again, given the bike's;
    /// - the game's messages that name the vehicle being ridden ("{type} is full" when something is bought on it): while
    ///   one is ridden this listens first on the game's own event for a message (Notifications.onShow) and gives the
    ///   message's words the bike's name before the game's toast and list are made from them;
    /// - the list of vehicles parked in an underground car park (the game's tooltip on the car park's panel): four times
    ///   a second while the player is in one, a list the game has made afresh is given the bikes' names in the places the
    ///   game put its own.
    /// Nothing of the game's is kept changed: the panel's key and the tooltip's list are the game's own, written again by
    /// it each time it shows them, and the message's words are its words for this one message. Nothing goes over the
    /// network, and only the player's own two-wheelers are named (a guest's bike in Going Public is its owner's).
    /// Cost: while riding, one reference comparison a frame (a string comparison when the game has written the panel);
    /// a message while riding, four dictionary looks; in a car park, a reference comparison four times a second and a
    /// walk of the save's vehicles when the game has made its list again. Nothing is allocated.
    /// </summary>
    public sealed class BikeNames
    {
        private const string Parked = "ba:street_parking";                                         // the game's street of a car park's vehicles
        private static readonly string[] NameWords = { "type", "vehicle", "vehicleName", "vehicleTypeName" };   // the words the game's messages put a vehicle's name under

        private readonly Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _onNotice;
        private BikeMount _riding;
        private TextLocalizationComponent _panelName;
        private bool _listening;
        private LocalizedListTooltip[] _tips;
        private CurrentBuildingUI _tipsOf;
        private List<string> _named;   // the car park's list last given the bikes' names
        private List<string> _other;   // a list of that panel's that is not the car park's vehicles

        public BikeNames()
        {
            _onNotice = Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(Noticed);   // (the game's own event: Core/Guard)
        }

        /// <summary>The player has got on one of theirs (null: off it, or on something else).</summary>
        public void Ride(BikeMount mount)
        {
            _riding = mount;
            if (mount == null)
            {
                _panelName = null;
                Listen(false);
                return;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            _panelName = uis != null && uis.playerHUD != null && uis.playerHUD.itemPanelUI != null ? uis.playerHUD.itemPanelUI.itemNameLabel : null;
            Listen(true);
            Frame();
        }

        /// <summary>Each frame of a ride: the panel's name the bike's, if the game has written the scooter's again.</summary>
        public void Frame()
        {
            TextLocalizationComponent label = _panelName;
            if (label == null || _riding == null)
            {
                return;
            }
            string key = label.Key;
            if (!ReferenceEquals(key, _riding.Def.Name) && key == TwoWheelers.ScooterType)
            {
                label.Key = _riding.Def.Name;
            }
        }

        // First on the game's event while riding, so that the message is in the bike's name before anything is made of it.
        private void Listen(bool on)
        {
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onNotice);
            _listening = on;
            if (on)
            {
                Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Combine(_onNotice, Notifications.onShow);
            }
        }

        private void Noticed(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action onClick, bool sound, bool track)
        {
            BikeMount mount = _riding;
            if (mount == null || data == null || data.Count == 0)
            {
                return;
            }
            string shown = null;
            for (int i = 0; i < NameWords.Length; i++)
            {
                if (!data.TryGetValue(NameWords[i], out string value) || string.IsNullOrEmpty(value))
                {
                    continue;
                }
                if (value == TwoWheelers.ScooterType)
                {
                    data[NameWords[i]] = mount.Def.Name;   // a key the message localises itself
                }
                else if (value == (shown ?? (shown = TwoWheelers.ScooterType.GetLocalization())))
                {
                    data[NameWords[i]] = GarageText.T(mount.Def.Name);
                }
            }
        }

        /// <summary>Four times a second: a car park's list of parked vehicles, made again by the game, given the bikes' names.</summary>
        public void Quick()
        {
            if (GarageState.Bikes.Count == 0 || !UndergroundParkingManager.IsInsideParking || UndergroundParkingManager.currentParkingEntrance == null)
            {
                return;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            CurrentBuildingUI panel = uis != null && uis.playerHUD != null ? uis.playerHUD.currentBuildingUI : null;
            if (panel == null)
            {
                return;
            }
            if (_tips == null || !ReferenceEquals(_tipsOf, panel))
            {
                _tips = panel.GetComponentsInChildren<LocalizedListTooltip>(true);   // once a session: the panel is the HUD's own
                _tipsOf = panel;
            }
            for (int i = 0; i < _tips.Length; i++)
            {
                List<string> list = _tips[i] != null ? _tips[i].list : null;
                if (list != null && list.Count > 0 && !ReferenceEquals(list, _named) && !ReferenceEquals(list, _other))
                {
                    Name(list);
                }
            }
        }

        // The game's list is the keys of the car park's vehicles in the save's order, the ridden one left out (CurrentBuildingUI.GetParkedVehicles):
        // walked the same way, the bikes' places in it are known.
        private void Name(List<string> list)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null)
            {
                return;
            }
            VehicleInstance ridden = VehicleHelper.GetCurrentVehicle();
            int number = UndergroundParkingManager.currentParkingEntrance.parkingNumber;
            int at = 0;
            bool ours = false;
            List<VehicleInstance> all = save.VehicleInstances;
            for (int i = 0; i < all.Count && at < list.Count; i++)
            {
                VehicleInstance vehicle = all[i];
                if (vehicle == null || vehicle == ridden || vehicle.streetName != Parked || vehicle.streetNumber != number)
                {
                    continue;
                }
                if (list[at] != vehicle.vehicleTypeName)
                {
                    _other = list;   // not the list it seemed: left as the game made it
                    return;
                }
                BikeRecord bike = vehicle.vehicleTypeName == TwoWheelers.ScooterType ? GarageState.Bike(vehicle.id) : null;
                if (bike != null)
                {
                    list[at] = BikeCatalogue.Of(bike.Kind).Name;
                    ours = true;
                }
                at++;
            }
            if (ours || at > 0)
            {
                _named = list;
            }
            else
            {
                _other = list;
            }
        }

        /// <summary>Off the game's event (the mod closing, the Garage switched off).</summary>
        public void Dispose()
        {
            _riding = null;
            _panelName = null;
            if (_listening)
            {
                Listen(false);
            }
        }
    }
}
