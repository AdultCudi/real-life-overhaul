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
    /// A two-wheeler called by its own name wherever the game writes the scooter's: the vehicle panel, the messages and a
    /// car park's list. Cost: a reference comparison a frame while riding, four dictionary looks a message, a comparison
    /// four times a second in a car park; nothing allocated.
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
            _onNotice = Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(Noticed);   // the game's own event: Core/Guard
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

        // First on the game's event, so that the message carries the bike's name before anything is made of it.
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

        // The game's list holds the car park's vehicles in the save's order, the ridden one left out.
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
