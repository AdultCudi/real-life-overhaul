using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The selling part of <see cref="TwoWheelers"/>: hiring at a dock and the stand's list of what is for sale.</summary>
    public sealed partial class TwoWheelers
    {
        private void Hire(BikePlace place)
        {
            if (!BikePlaces.HandOverSpot(place, out Vector3 spot))
            {
                Notifications.Show(NotificationType.Warning, "reallife_bike_no_room", null, 7f);
                return;
            }
            if (!Crime.Pay(BikeCatalogue.DockFee, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(BikeCatalogue.DockFee) } }, 7f);
                return;
            }
            BikeMount mount = Spawn(BikeKind.CityBicycle, spot, place.Rotation, false, 0x1E88E5);
            if (mount == null)
            {
                Crime.Give(BikeCatalogue.DockFee, "reallife_transaction_bike");   // nothing was handed over
                return;
            }
            PointTo(mount, spot, mount.Def);
            SayWhere(NotificationType.Success, "reallife_bike_hired_where", mount.Def, spot);
        }

        // What a rack or a stand sells, in the game's list: a name, what it does and costs, and a button.
        private void OpenPanel(BikePlace place)
        {
            float began = Time.realtimeSinceStartup;
            TravelList list = GarageList.Take(this, false);
            if (list == null)
            {
                return;   // the game's window could not be copied: a "Game UI:" line says why
            }
            bool motor = place.Kind == BikePlaceKind.MotorbikeStand;
            _panelPlace = place;
            _acts.Clear();
            list.Begin(GarageText.T(motor ? "reallife_bike_panel_motorbikes" : "reallife_bike_panel_bicycles"),
                GarageText.T(motor ? "reallife_bike_list_info_motor" : "reallife_bike_list_info_bicycle"));
            foreach (BikeDef def in BikeCatalogue.All)
            {
                if (def.Motor != motor)
                {
                    continue;
                }
                BikeDef model = def;
                string facts = def.Motor
                    ? GarageText.T("reallife_bike_facts_motor", "seconds", def.Seconds.ToString("F1"), "top", def.TopKmh.ToString("F0"))
                    : GarageText.T("reallife_bike_facts_bicycle", "speed", def.CruiseKmh.ToString("F0"));
                Row(list, GarageText.T(def.Name), GarageText.T("reallife_bike_list_price", "price", GarageText.Money(def.Cost)) + " " + facts, "reallife_bike_button_buy", true, () => Buy(model));
            }
            if (motor)
            {
                Row(list, GarageText.T("reallife_bike_helmet"), GarageState.Helmet ? GarageText.T("reallife_bike_list_helmet_owned")
                        : GarageText.T("reallife_bike_list_price", "price", GarageText.Money(BikeCatalogue.HelmetPrice)) + " " + GarageText.T("reallife_bike_helmet_note"),
                    "reallife_bike_button_buy", !GarageState.Helmet, BuyHelmet);
            }
            // One of the player's own of this sort standing by can be sold here.
            BikeMount near = OwnedNear(place.Position, motor, 12f);
            if (near != null)
            {
                int offer = Mathf.RoundToInt(near.Def.Cost * GarageCatalogue.Resale);
                string id = near.Record.Id;
                Row(list, GarageText.T(near.Def.Name), GarageText.T("reallife_bike_sell", "name", GarageText.T(near.Def.Name), "price", GarageText.Money(offer)),
                    "reallife_bike_button_sell", true, () => Sell(id, offer));
            }
            _listClosing = null;
            list.Open(_onPick, _onListClosed);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (stand list): the game's list is open at a " + place.Kind + " with " + _acts.Count + " entries"
                             + (near != null ? " (one of them sells the player's own standing by)" : string.Empty) + "; "
                             + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
            }
        }

        private void Row(TravelList list, string name, string detail, string button, bool enabled, Action act)
        {
            _acts.Add(act);
            list.Add(_acts.Count - 1, name, detail, button, enabled);
        }

        private void Picked(int id)
        {
            try
            {
                if (id >= 0 && id < _acts.Count && _acts[id] != null)
                {
                    _acts[id]();
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: that did not work. " + e);
                ClosePanel();
            }
        }

        private void ListClosed()
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (stand list): closed (" + (_listClosing ?? "cancelled by the player, or another list of the Garage was opened") + ").");
            }
            _listClosing = null;
            _panelPlace = null;
            _acts.Clear();
        }

        public void ClosePanel()
        {
            if (PanelOpen)
            {
                GarageList.Close();
            }
            _panelPlace = null;
        }

        private void Buy(BikeDef def)
        {
            BikePlace place = _panelPlace;
            if (place == null)
            {
                return;
            }
            if (!BikePlaces.HandOverSpot(place, out Vector3 spot))
            {
                Notifications.Show(NotificationType.Warning, "reallife_bike_no_room", null, 7f);
                return;
            }
            int cost = def.Cost;
            if (!Crime.Pay(cost, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(cost) } }, 7f);
                return;
            }
            BikeMount mount = Spawn(def.Kind, spot, place.Rotation, true);
            if (mount == null)
            {
                Crime.Give(cost, "reallife_transaction_bike");
                return;
            }
            _listClosing = "a " + def.Kind + " was bought";
            ClosePanel();
            PointTo(mount, spot, def);
            SayWhere(NotificationType.Success, def.Motor && !GarageState.Helmet ? "reallife_bike_bought_where_no_helmet" : "reallife_bike_bought_where", def, spot, 12f);
            if (def.Motor && !GarageBridges.Licensed())
            {
                Notifications.Show(NotificationType.Warning, "reallife_bike_no_licence", null, 12f);   // the sale stands; the dealer says what riding it needs
            }
        }

        private void BuyHelmet()
        {
            if (GarageState.Helmet)
            {
                return;
            }
            if (!Crime.Pay(BikeCatalogue.HelmetPrice, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(BikeCatalogue.HelmetPrice) } }, 7f);
                return;
            }
            GarageState.Helmet = true;
            GarageState.Touch();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (choice): a helmet was bought for " + BikeCatalogue.HelmetPrice + ".");
            }
            BikePlace place = _panelPlace;
            if (place != null)
            {
                OpenPanel(place);   // the entry now says it is owned
            }
        }

        private void Sell(string id, int offer)
        {
            BikeRecord record = GarageState.Bike(id);
            if (record == null || !record.Owned)
            {
                return;
            }
            Remove(id);
            Crime.Give(offer, "reallife_transaction_bike_sale");
            Notifications.Show(NotificationType.Success, "reallife_bike_sold", new Dictionary<string, string>(1) { { "amount", GarageText.Money(offer) } }, 7f);
            _listClosing = "a two-wheeler was sold";
            ClosePanel();
        }

        private static BikeMount OwnedNear(Vector3 from, bool motor, float within)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                BikeMount mount = all[i] != null ? all[i].GetComponent<BikeMount>() : null;
                if (mount != null && mount.Record != null && mount.Record.Owned && mount.Def.Motor == motor && !mount.Ridden
                    && (mount.transform.position - from).sqrMagnitude < within * within)
                {
                    return mount;
                }
            }
            return null;
        }
    }
}
