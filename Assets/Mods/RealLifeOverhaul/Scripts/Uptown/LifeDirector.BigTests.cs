using System;
using System.Collections;
using System.Collections.Generic;
using Extensions;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The tests of Uptown's big special places (Options, Test tools), each from wherever the player is, with no searching:
    /// taken to the wholesale store or the filling station (on foot to the door, at the wheel to the garage door or the
    /// forecourt) with its check that it stands as one of the game's own, one order from the store delivered now by the
    /// game's own delivery to the player's business, and a fill-up at the station's pump by the game's own overlay. Each
    /// writes one "Uptown check (...)" line, PASS or FAIL, and says why in a notice when it cannot run. Only the player's own
    /// things change (their place, their money, their car's tank, their business's stock): safe in a Going Public session.
    /// Nothing runs a frame but the fill-up's few steps.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const float FillSettle = 0.5f;   // s of real time a car drawn up at a pump is given to settle on its wheels before the game's pumps are asked

        // ---------------------------------------------------------------- taken there

        /// <summary>"Uptown: take me to the wholesale store": the player at its door or, driving, in front of its first garage door; its check.</summary>
        public static void TestStore()
        {
            TakeMe(BigKind.Wholesale, "wholesale store");
        }

        /// <summary>"Uptown: take me to the gas station": the player at its shop's door or, driving, on its forecourt; its check.</summary>
        public static void TestStation()
        {
            TakeMe(BigKind.GasStation, "gas station");
        }

        private static void TakeMe(BigKind kind, string what)
        {
            try
            {
                BigPlace place = BigPlacesRules.Of(kind);
                if (Refused(true) || NoBig(kind, place, what, out MadeBuilding made))
                {
                    return;
                }
                if (!LifeGame.CanMove)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                    return;
                }
                string why = LifeGame.BigWhy(made, place);
                bool moved = LifeGame.TakeTo(made, kind);
                bool pass = why == null && moved;
                string line = "Uptown check (" + what + "): " + place.Name + " at " + made.Address + ", the game's " + place.Design + " copied whole with the game's service "
                              + place.Service + " under its own name" + (why != null ? ", but " + why : ", standing as one of the game's own") + "; the player "
                              + (moved ? "taken there" : "not moved") + ": " + (pass ? "PASS" : "FAIL");
                Say(pass, line);
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, pass ? LifeText.BigHere : LifeText.BigWrong, Named(place, made), 12f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- an order delivered now

        /// <summary>"Uptown: an order from the Uptown wholesale store delivered now": one unit of its goods to the player's business, the game's own delivery.</summary>
        public static void TestOrder()
        {
            try
            {
                BigPlace place = BigPlacesRules.Of(BigKind.Wholesale);
                if (Refused(true) || NoBig(BigKind.Wholesale, place, "wholesale delivery", out MadeBuilding store))
                {
                    return;
                }
                BuildingRegistration business = LifeGame.OrderingBusiness();
                if (business == null)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.OrderNone, "none of the player's businesses takes wholesale deliveries");
                    return;
                }
                string standing = LifeGame.BigWhy(store, place);
                bool done = LifeGame.DeliverNow(store, business, out string item, out int arrived, out float paid, out string why);
                bool pass = standing == null && done && arrived > 0 && paid > 0f;
                Say(pass, "Uptown check (wholesale delivery): an order of 1 " + (item ?? "(none)") + " from " + place.Name + " at " + store.Address + " for " + business.BusinessName
                          + " at " + MadeGame.Written(business.StreetName, business.StreetNumber) + " by the game's own delivery of a wholesale contract: "
                          + (why ?? (arrived + " arrived in its stock, "
                          + paid.ToString("F2", Plain) + " paid with the delivery fee")) + (standing != null ? "; the store: " + standing : string.Empty) + ": " + (pass ? "PASS" : "FAIL"));
                if (pass)
                {
                    Dictionary<string, string> data = Named(place, store);
                    data["item"] = item.GetLocalization();
                    data["count"] = arrived.ToString(Plain);
                    data["business"] = business.BusinessName;
                    data["money"] = paid.ToCurrencyFormat();
                    Notifications.Show(NotificationType.Info, LifeText.OrderDone, data, 12f);
                }
                else
                {
                    Notifications.Show(NotificationType.Warning, LifeText.OrderFailed, Named(place, store), 12f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- a fill-up at the pumps

        /// <summary>"Uptown: fill up at the Uptown gas station now": the player's car drawn up at a pump and filled by the game's own overlay.</summary>
        public static void TestFill()
        {
            try
            {
                BigPlace place = BigPlacesRules.Of(BigKind.GasStation);
                if (Refused(true) || NoBig(BigKind.GasStation, place, "fill-up", out MadeBuilding station))
                {
                    return;
                }
                if (!LifeGame.DrivesFillable(out VehicleController car))
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.FillCar, "the player drives no car the pumps serve");
                    return;
                }
                if (Instance == null || !LifeGame.CanMove)
                {
                    TestRefusals.Tell(Logger, TestName, Instance == null ? TestRefusal.NotLoaded : TestRefusal.Indoors);
                    return;
                }
                GasStationTrigger pump = LifeGame.Pump(station);
                if (pump == null)
                {
                    Say(false, "Uptown check (fill-up): " + place.Name + " at " + station.Address + " has no pump that fills a tank: FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.FillFailed, Named(place, station), 12f);
                    return;
                }
                _testing = true;
                Instance.StartCoroutine(Guard.Routine(Instance.Fill(place, station, car, pump)));
            }
            catch (Exception e)
            {
                _testing = false;
                Logger?.Error(e);
            }
        }

        // The car at the pump, a moment to settle, the game's pumps asked which it stands at, then the overlay's own fill-up.
        private IEnumerator Fill(BigPlace place, MadeBuilding station, VehicleController car, GasStationTrigger pump)
        {
            try
            {
                float was = LifeGame.DrawUp(car, pump, station.Controller.transform);
                float until = Time.unscaledTime + FillSettle;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                if (car == null || !LifeGame.DrivesFillable(out VehicleController now) || now != car)
                {
                    Say(false, "Uptown check (fill-up): the player left the car before it was filled: FAIL");
                    yield break;
                }
                GasStationController.RefreshActiveTriggers(car.vehicleCollider);
                GasStationTrigger open = LifeGame.OverlayPump();
                float before = car.GetCurrentFuel(), money = SaveGameManager.Current.Money;
                bool asked = open == pump && LifeGame.FillUp();
                float after = car.GetCurrentFuel(), paid = money - SaveGameManager.Current.Money;
                bool pass = asked && Mathf.Approximately(after, car.vehicleType.maxFuel) && paid > 0f;
                Say(pass, "Uptown check (fill-up): the player's car drawn up at " + place.Name + "'s pump at " + station.Address + " (its tank " + was.ToString("F1", Plain)
                          + ", drawn down to " + before.ToString("F1", Plain) + " of " + car.vehicleType.maxFuel.ToString("F1", Plain) + "), the game's overlay open for "
                          + (open == null ? "no pump" : open == pump ? "that pump" : "another pump") + (asked ? ", its fill-up pressed: the tank " + after.ToString("F1", Plain)
                          + ", " + paid.ToString("F2", Plain) + " paid" : GameContract.StationRefuel.Method == null ? " (the overlay's fill-up is not in this build)" : string.Empty)
                          + ": " + (pass ? "PASS" : "FAIL"));
                Dictionary<string, string> data = Named(place, station);
                data["money"] = paid.ToCurrencyFormat();
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, pass ? LifeText.FillDone : LifeText.FillFailed, data, 12f);
            }
            finally
            {
                _testing = false;
            }
        }

        // ---------------------------------------------------------------- shared

        // Why the place cannot be tested: it does not stand at this load (said with its line and a notice). False when it stands.
        private static bool NoBig(BigKind kind, BigPlace place, string what, out MadeBuilding made)
        {
            made = Big(kind);
            if (made != null)
            {
                return false;
            }
            Say(false, "Uptown check (" + what + "): " + place.Name + " does not stand at this load (Player.log's \"Made buildings\" and \"Uptown\" lines say why): FAIL");
            Notifications.Show(NotificationType.Warning, LifeText.BigNone, new Dictionary<string, string> { { "name", place.Name } }, 12f);
            return true;
        }

        private static Dictionary<string, string> Named(BigPlace place, MadeBuilding made)
        {
            return new Dictionary<string, string> { { "name", place.Name }, { "address", made.Address } };
        }

        // A check line, whatever Developer diagnostics say, as the test was asked for; a FAIL as a warning.
        private static void Say(bool pass, string line)
        {
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }
    }
}
