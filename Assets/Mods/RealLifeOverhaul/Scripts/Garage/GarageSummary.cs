using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "My vehicles": every car of the player's with what the custom shop did to it and adds to its worth, and every
    /// two-wheeler with where it stands (and a second entry to have it brought), in the game's list window
    /// (<see cref="GarageList"/>). <see cref="Lines"/> gives the same lines to a feature with a garage page of its own.
    /// </summary>
    public static class GarageSummary
    {
        private const int Rows = 8;

        private static readonly object Owner = new object();
        private static readonly List<string> Titles = new List<string>(Rows);
        private static readonly List<string> Details = new List<string>(Rows);
        private static readonly List<string> BikeIds = new List<string>(Rows);   // beside Titles: the two-wheeler of a row, null for a car
        private static readonly Action<int> OnPick = Picked;
        private static readonly Action OnClosed = Closed;
        private static string _closing;
        private static bool _wanted;     // asked for from the options page: opened when the menus are closed

        public static bool IsOpen => GarageList.OpenFor(Owner);

        public static void Toggle()
        {
            if (IsOpen || _wanted)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public static void Close()
        {
            _wanted = false;
            if (IsOpen)
            {
                GarageList.Close();
            }
        }

        /// <summary>Four times a second: an open list closes when another screen comes up; one asked for under a menu opens once it has gone.</summary>
        public static void Tick(GameManager gm)
        {
            if (_wanted && !GarageList.MenuUp)
            {
                Open();
            }
            else if (IsOpen && GarageList.MenuUp)
            {
                _closing = "another screen came up";
                GarageList.Close();
            }
        }

        public static void Open()
        {
            _wanted = false;
            if (!GarageState.Loaded)
            {
                TestRefusals.Tell(GarageDirector.Logger, "My vehicles", TestRefusal.NotLoaded);
                return;
            }
            if (GarageList.MenuUp)
            {
                // Asked for on the options page: the game's list stands on the HUD, which is not shown under a menu.
                _wanted = true;
                Notifications.Show(NotificationType.Info, "reallife_garage_summary_waits", null, 6f);
                return;
            }
            float began = Time.realtimeSinceStartup;
            TravelList list = GarageList.Take(Owner, false);
            if (list == null)
            {
                return;   // the game's window could not be copied: a "Game UI:" line says why
            }
            Lines(Titles, Details, BikeIds);
            var gm = InstanceBehavior<GameManager>.Instance;
            Vector3 player = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;
            int bikes = 0;
            for (int i = 0; i < BikeIds.Count; i++)
            {
                bikes += BikeIds[i] != null ? 1 : 0;
            }
            list.Begin(GarageText.T("reallife_garage_my_vehicles"), Titles.Count == 0 ? GarageText.T("reallife_garage_summary_none")
                : GarageText.T("reallife_garage_summary_info", "cars", (Titles.Count - bikes).ToString(), "bikes", bikes.ToString()));
            for (int i = 0; i < Titles.Count; i++)
            {
                string id = i < BikeIds.Count ? BikeIds[i] : null;
                BikeRecord bike = id != null ? GarageState.Bike(id) : null;
                // An entry's id: twice the row, plus one for "bring it here".
                list.Add(i * 2, Titles[i], Details[i], "reallife_garage_summary_show", bike != null);
                if (bike != null)
                {
                    // A two-wheeler can be marked, or stood in front of the player (free close by, a small fee further off).
                    int fee = TwoWheelers.BringFee(TwoWheelers.Where(bike), player);
                    list.Add(i * 2 + 1, fee > 0 ? GarageText.T("reallife_garage_summary_bring_fee", "price", GarageText.Money(fee)) : GarageText.T("reallife_garage_summary_bring"),
                        GarageText.T("reallife_garage_summary_bring_detail", "name", GarageText.T(BikeCatalogue.Of(bike.Kind).Name)), "reallife_garage_list_bring", true);
                }
            }
            _closing = null;
            list.Open(OnPick, OnClosed);
            if (Meta.Dev.Verbose)
            {
                GarageDirector.Logger?.Info("Garage check (my vehicles): the game's list is open with " + (Titles.Count - bikes) + " car(s) with a build and " + bikes + " two-wheeler(s); "
                                            + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
            }
        }

        private static void Picked(int id)
        {
            int row = id / 2;
            string bike = row >= 0 && row < BikeIds.Count ? BikeIds[row] : null;
            if (bike == null)
            {
                return;
            }
            bool bring = (id & 1) == 1;
            if (Meta.Dev.Verbose)
            {
                GarageDirector.Logger?.Info("Garage check (choice): '" + (bring ? "Bring it here" : "Show me") + "' for two-wheeler " + bike + ", from 'My vehicles'.");
            }
            _closing = bring ? "a two-wheeler is brought" : "a two-wheeler is shown";
            if (bring)
            {
                Bring(bike);
            }
            else
            {
                Show(bike);
            }
        }

        private static void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                GarageDirector.Logger?.Info("Garage check (my vehicles): closed (" + (_closing ?? "cancelled by the player, or another list of the Garage was opened") + ").");
            }
            _closing = null;
        }

        /// <summary>A title and a line per vehicle for a garage page of another feature's. Builds strings: not per frame.</summary>
        public static void Lines(List<string> titles, List<string> details)
        {
            Lines(titles, details, null);
        }

        private static void Lines(List<string> titles, List<string> details, List<string> bikeIds)
        {
            titles.Clear();
            details.Clear();
            bikeIds?.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null || !GarageState.Loaded)
            {
                return;
            }
            var text = new StringBuilder(160);
            foreach (VehicleInstance vehicle in save.VehicleInstances)
            {
                GarageBuild build = vehicle != null ? GarageState.Of(vehicle.id) : null;
                if (build == null)
                {
                    continue;
                }
                text.Length = 0;
                text.Append(GarageText.T(GarageCatalogue.FinishName(build.Finish)));
                text.Append(build.Paint >= 0 ? " #" + build.Paint.ToString("X6") : " " + GarageText.T("reallife_garage_summary_factory"));
                if (build.Lamps != LampTint.Stock)
                {
                    text.Append(", ").Append(GarageText.T(GarageCatalogue.LampName(build.Lamps)));
                }
                if (build.Glow >= 0)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_summary_glow"));
                }
                if (build.Spoiler)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_body_spoiler"));
                }
                if (build.Rack)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_body_rack"));
                }
                if (build.Bar)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_body_bar"));
                }
                for (int i = 0; i < build.Parts.Length; i++)
                {
                    PartDef part = GarageCatalogue.Part(build.Parts[i]);
                    if (part != null)
                    {
                        text.Append(", ").Append(GarageText.T(part.Name));
                    }
                }
                titles.Add(GarageText.T("reallife_garage_summary_car", "car", vehicle.vehicleTypeName.GetLocalization(),
                    "value", GarageText.Money(GarageBridges.ValueAdded(vehicle.id))));
                details.Add(text.ToString());
                bikeIds?.Add(null);
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            Vector3 player = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;
            foreach (KeyValuePair<string, BikeRecord> pair in GarageState.Bikes)
            {
                BikeRecord bike = pair.Value;
                BikeDef def = BikeCatalogue.Of(bike.Kind);
                titles.Add(GarageText.T(bike.Owned ? "reallife_garage_summary_bike" : "reallife_garage_summary_hired", "name", GarageText.T(def.Name),
                    "value", GarageText.Money(def.Cost * GarageCatalogue.Resale)));
                // Where it stands now (where it was last left, if the game does not have it in the world).
                Vector3 at = TwoWheelers.Where(bike);
                string metres = Mathf.RoundToInt(Vector3.Distance(player, at)).ToString();
                string place = BikePlaces.Whereabouts(at);
                details.Add(place.Length > 0
                    ? GarageText.T("reallife_garage_summary_place", "place", place, "metres", metres)
                    : GarageText.T("reallife_garage_summary_where", "metres", metres));
                bikeIds?.Add(bike.Id);
            }
        }

        // The list closes first: the marker is not drawn under it, and what is brought stands in front of the player.
        private static void Show(string id)
        {
            Close();
            try
            {
                GarageDirector.Instance?.Bikes.ShowMe(id);
            }
            catch (Exception e)
            {
                GarageDirector.Logger?.Warn("Two-wheelers: \"Show me\" did not work. " + e);
            }
        }

        private static void Bring(string id)
        {
            Close();
            try
            {
                GarageDirector.Instance?.Bikes.BringHere(id);
            }
            catch (Exception e)
            {
                GarageDirector.Logger?.Warn("Two-wheelers: \"Bring it here\" did not work. " + e);
            }
        }
    }
}
