using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>The finding part of <see cref="TwoWheelers"/>: the marker on one, where it is, and "Bring it here".</summary>
    public sealed partial class TwoWheelers
    {
        private const float PointSeconds = 20f;      // seconds
        private const float PointNear = 3f;          // metres
        private const float BringFreeWithin = 25f;   // metres

        /// <summary>
        /// Marks a two-wheeler with the game's destination marker until the player is near it, gets on, goes indoors or
        /// the time is up. While a job or an outing has the marker it is asked for again four times a second.
        /// </summary>
        public void PointTo(BikeMount mount, Vector3 at, BikeDef def, float seconds = PointSeconds)
        {
            _pointMount = mount;
            _pointAt = mount != null ? mount.transform.position : at;
            _pointLabel = GarageText.T("reallife_bike_marker", "name", GarageText.T(def.Name));
            _pointUntil = Time.unscaledTime + seconds;
            bool shown = _mark.Show(_pointAt + Vector3.up * 1.4f, _pointLabel);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (marker): '" + _pointLabel + "' at " + _pointAt.ToString("F1", CultureInfo.InvariantCulture) + " is " + (shown ? "on the game's destination marker: PASS" : "NOT marked yet (the game's marker is on a job or an outing): FAIL")
                             + ".");
            }
        }

        // Four times a second while pointing: a few comparisons.
        private void Point(GameManager gm)
        {
            bool tracked = !ReferenceEquals(_pointMount, null);
            if (_pointMount != null && (_pointMount.transform.position - _pointAt).sqrMagnitude > 1f)
            {
                _pointAt = _pointMount.transform.position;
            }
            PlayerController player = gm.playerController;
            if (Time.unscaledTime >= _pointUntil || player == null || BuildingManager.IsInsideBuilding
                || (tracked && (_pointMount == null || _pointMount.Ridden))
                || CitySpots.Flat(player.transform.position - _pointAt) < PointNear * PointNear)
            {
                StopPointing();
                return;
            }
            _mark.Show(_pointAt + Vector3.up * 1.4f, _pointLabel);
        }

        private void StopPointing()
        {
            if (_pointUntil <= 0f)
            {
                return;
            }
            _pointUntil = 0f;
            _pointMount = null;
            _mark.Hide();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (marker): taken off (reached, ridden, indoors, or its time was up).");
            }
        }

        /// <summary>The scooter that carries a two-wheeler, or null if the game does not have it in the world now.</summary>
        public static ScooterController Find(string id)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is ScooterController scooter && scooter.vehicleInstance != null && scooter.vehicleInstance.id == id)
                {
                    return scooter;
                }
            }
            return null;
        }

        /// <summary>Where a two-wheeler stands now if it is in the world, otherwise where it was last left.</summary>
        public static Vector3 Where(BikeRecord record)
        {
            ScooterController scooter = Find(record.Id);
            return scooter != null ? scooter.transform.position : record.Position;
        }

        // "ahead of you", "to your left"...: which way a place lies from the player, as the camera sees it.
        private static string Direction(Vector3 to, out int metres)
        {
            PlayerController player = PlayerHelper.PlayerController;
            metres = 0;
            if (player == null)
            {
                return GarageText.T("reallife_bike_dir_ahead");
            }
            Vector3 way = to - player.transform.position;
            way.y = 0f;
            metres = Mathf.RoundToInt(way.magnitude);
            Camera camera = GameManager.GetMainCamera();
            Vector3 ahead = camera != null ? camera.transform.forward : player.transform.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.01f)
            {
                ahead = player.transform.forward;
            }
            if (way.sqrMagnitude < 0.01f)
            {
                return GarageText.T("reallife_bike_dir_ahead");
            }
            float angle = Vector3.SignedAngle(ahead, way, Vector3.up);   // to the right is positive
            return GarageText.T(Mathf.Abs(angle) <= 45f ? "reallife_bike_dir_ahead"
                : Mathf.Abs(angle) >= 135f ? "reallife_bike_dir_behind"
                : angle > 0f ? "reallife_bike_dir_right" : "reallife_bike_dir_left");
        }

        // Its name, how far and which way.
        private static void SayWhere(NotificationType type, string key, BikeDef def, Vector3 at, float seconds = 10f)
        {
            string direction = Direction(at, out int metres);
            Notifications.Show(type, key, new Dictionary<string, string>(3)
            {
                { "name", def.Name.GetLocalization() }, { "metres", metres.ToString() }, { "direction", direction }
            }, seconds);
        }

        /// <summary>"Show me" of My vehicles: the marker on it for half a minute, and a notice of how far and which way.</summary>
        public void ShowMe(string id)
        {
            BikeRecord record = GarageState.Bike(id);
            if (record == null)
            {
                return;
            }
            BikeDef def = BikeCatalogue.Of(record.Kind);
            ScooterController scooter = Find(id);
            BikeMount mount = scooter != null ? scooter.GetComponent<BikeMount>() : null;
            Vector3 at = scooter != null ? scooter.transform.position : record.Position;
            PointTo(mount, at, def, 30f);
            SayWhere(NotificationType.Info, BuildingManager.IsInsideBuilding ? "reallife_bike_where_indoors" : "reallife_bike_where", def, at);
            Logger?.Info("Two-wheelers: shown " + (mount != null ? mount.Report() : def.Kind + ", not in the world now; last left at " + record.Position.ToString("F1", CultureInfo.InvariantCulture)
                + "."));
        }

        /// <summary>
        /// "Bring it here" of My vehicles: the two-wheeler stood in front of the player, on foot and out of doors; free
        /// from close by, else a small fee. One the game no longer has is made again, the same sort and colour.
        /// </summary>
        public void BringHere(string id)
        {
            BikeRecord record = GarageState.Bike(id);
            TestRefusal why = TestRefusals.Street();
            var gm = InstanceBehavior<GameManager>.Instance;
            if (why == TestRefusal.None && (record == null || !(Settings.Garage && Settings.TwoWheelers)))
            {
                why = TestRefusal.FeatureOff;
            }
            if (why == TestRefusal.None && gm.selectedVehicle != null)
            {
                why = TestRefusal.InVehicle;
            }
            if (why != TestRefusal.None)
            {
                Cannot(why, null);
                return;
            }
            Transform player = gm.playerController.transform;
            if (!BikePlaces.SpotAhead(player, out Vector3 spot, out Quaternion facing))
            {
                Cannot(TestRefusal.NoRoom, null);
                return;
            }
            BikeDef def = BikeCatalogue.Of(record.Kind);
            ScooterController scooter = Find(id);
            BikeMount mount = scooter != null ? scooter.GetComponent<BikeMount>() : null;
            Vector3 was = scooter != null ? scooter.transform.position : record.Position;
            int fee = BringFee(was, player.position);
            if (fee > 0 && !Crime.Pay(fee, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(fee) } }, 7f);
                return;
            }
            if (mount != null)
            {
                spot.y += Lift(scooter, was);
                Place(scooter, spot, facing);
                record.Position = spot;
                record.Heading = facing.eulerAngles.y;
                Watch(mount, spot, facing);
                Logger?.Info("Two-wheelers: brought to the player from " + was.ToString("F1", CultureInfo.InvariantCulture) + " (" + (fee > 0 ? "fee " + fee : "free") + "): " + mount.Report());
            }
            else
            {
                // The game took its scooter away or never spawned it: a new one, then the old entry goes.
                mount = Spawn(record.Kind, spot, facing, record.Owned, record.Colour);
                if (mount == null)
                {
                    if (fee > 0)
                    {
                        Crime.Give(fee, "reallife_transaction_bike");
                    }
                    Cannot(TestRefusal.NothingFits, "the scooter could not be made or dressed");
                    return;
                }
                mount.Record.Day = record.Day;
                Remove(id);
                Logger?.Info("Two-wheelers: it was not in the world (last left at " + was.ToString("F1", CultureInfo.InvariantCulture) + "); made again in front of the player ("
                    + (fee > 0 ? "fee " + fee : "free") + ").");
            }
            PointTo(mount, spot, def);
            SayWhere(NotificationType.Success, "reallife_bike_brought", def, spot);
        }

        // "Bring it here" is not a test button: its refusals are said in its own words.
        private static void Cannot(TestRefusal why, string detail)
        {
            Logger?.Info("Two-wheelers: \"Bring it here\" was not done (" + why + (detail != null ? "; " + detail : string.Empty) + ").");
            string key;
            switch (why)
            {
                case TestRefusal.Indoors: key = "reallife_bike_bring_indoors"; break;
                case TestRefusal.TrafficOff: key = "reallife_bike_bring_paused"; break;
                case TestRefusal.FeatureOff: key = "reallife_bike_bring_off"; break;
                case TestRefusal.InVehicle: key = "reallife_bike_bring_in_vehicle"; break;
                case TestRefusal.NoRoom: key = "reallife_bike_bring_no_room"; break;
                case TestRefusal.NotLoaded: key = "reallife_bike_bring_loading"; break;
                default: key = "reallife_bike_bring_failed"; break;
            }
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }

        /// <summary>What "Bring it here" costs from where a two-wheeler stands.</summary>
        public static int BringFee(Vector3 bike, Vector3 player)
        {
            return CitySpots.Flat(bike - player) < BringFreeWithin * BringFreeWithin ? 0 : BikeCatalogue.BringFee;
        }
    }
}
