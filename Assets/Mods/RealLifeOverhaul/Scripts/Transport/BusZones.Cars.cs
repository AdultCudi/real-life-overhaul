using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus zones' part for the parking: the game's boxes shortened, its parked cars taken in, the player's moved.</summary>
    public static partial class BusZones
    {
        private const float Moving = 0.5f;        // m/s above which a car of the player's is being driven, and is left alone
        private static readonly List<Transform> Cars = new List<Transform>(16);
        private static bool _told;      // the player has been told a car of theirs was moved from a bay, once a session

        // The cars in a zone and its margin: the game's parked cars handed back, the player's own moved, each once out of view.
        // A lane that reaches into it again (the game made it afresh) is cut again first. True when none is left; sent: how
        // many of the game's went.
        private static bool Sweep(TransitStop stop, Vector3 right, out int sent)
        {
            BusZone zone = stop.Zone;
            sent = 0;
            int left = 0;
            float from = zone.From - Margin;
            float to = zone.To + Margin;
            int runs = Beside(stop, right, from, to, out string why);
            for (int i = 0; i < runs; i++)
            {
                Run run = Runs[i];
                float keepStart = run.Start, keepEnd = run.End;
                if (why == null && Mathf.Min(run.End, to) - Mathf.Max(run.Start, from) > 0.1f && BusBay.Keep(run.Start, run.End, from, to, out keepStart, out keepEnd, out _))
                {
                    zone.Spots += Shorten(run, keepStart, keepEnd);
                }
                sent += SendAway(run, stop, keepStart, keepEnd, ref left);
            }
            zone.PlayerCars += MovePlayerCars(stop, right, ref left);
            return left == 0;
        }

        // Shortens a lane's box to keepStart..keepEnd along the stop's lane, and its fixed count of spots with it. The box as the
        // game had it is kept, once, to be put back. Returns the spots given up.
        private static int Shorten(Run run, float keepStart, float keepEnd)
        {
            BoxCollider box = run.Box;
            ParkingLaneGenerator lane = run.Lane;
            if (!Originals.ContainsKey(lane))
            {
                Originals[lane] = new Original { Box = box, Centre = box.center, Size = box.size, Override = (int)OverrideField.GetValue(lane) };
            }
            float per = Mathf.Abs(run.PerLocal) > 0.0001f ? run.PerLocal : 1f;
            float near = box.center.z + (keepStart - run.Middle) / per;
            float far = box.center.z + (keepEnd - run.Middle) / per;
            Vector3 centre = box.center;
            Vector3 size = box.size;
            float was = size.z;
            centre.z = (near + far) * 0.5f;
            size.z = Mathf.Max(BoxLeast, Mathf.Abs(far - near));
            box.center = centre;
            box.size = size;
            float spot = Mathf.Max(0.1f, (float)SpotField.GetValue(lane));
            int spots = (int)OverrideField.GetValue(lane);
            int lost;
            if (spots > 0)
            {
                // A fixed count of spots is laid from the box's middle: no more than the box now holds.
                int now = Mathf.Min(spots, Mathf.FloorToInt(size.z / spot));
                OverrideField.SetValue(lane, now);
                lost = spots - now;
            }
            else
            {
                lost = BusBay.SpotsLost(was, size.z, spot);
            }
            ParkingLaneGenerator.RegenerateAutoParkSpotsNear(lane.transform.TransformPoint(centre));
            return lost;
        }

        // The game's parked cars of a lane that stand in a zone, its margin, or beyond what is left of the lane: handed back to
        // the game's store as the lane does itself, where nobody is looking. Returns how many; those in view count in left.
        private static int SendAway(Run run, TransitStop stop, float keepStart, float keepEnd, ref int left)
        {
            ParkingLaneGenerator lane = run.Lane;
            Transform frame = lane.transform;
            BusZone zone = stop.Zone;
            Cars.Clear();
            for (int i = 0; i < frame.childCount; i++)
            {
                Transform child = frame.GetChild(i);
                if (child.gameObject.layer == LayerHelper.ParkedVehiclesLayerIndex && child.gameObject.activeSelf)
                {
                    Cars.Add(child);
                }
            }
            int sent = 0;
            for (int i = 0; i < Cars.Count; i++)
            {
                Transform car = Cars[i];
                MeshCollider body = car.GetComponent<MeshCollider>();
                float half = body != null && body.sharedMesh != null ? body.sharedMesh.bounds.extents.z : 2.5f;
                float at = Vector3.Dot(Flat(car.position - stop.Lane), stop.Along);
                bool inZone = BusBay.CarInZone(at - half, at + half, zone.From, zone.To);
                bool beyond = at - half < keepStart - 0.3f || at + half > keepEnd + 0.3f;
                if (!inZone && !beyond)
                {
                    continue;
                }
                if (CitySpots.InView(car.position))
                {
                    left += inZone ? 1 : 0;   // one beyond the lane's end but clear of the zone goes when it can, and holds nothing up
                    continue;
                }
                lane.onReleaseVehicle?.Invoke(car.gameObject);
                ParkingSimulator.ReleaseParkedVehicle(car.gameObject);
                sent++;
            }
            Cars.Clear();
            return sent;
        }

        // The player's own cars in the stop's zone or its margin, between the lane and the kerb: each one nobody is in, standing
        // still and out of view, is moved to the nearest free space the game's auto-park would offer outside every zone
        // (VehicleTow.ParkNearby), and the player is told once. Returns how many; those that must wait count in left. One with
        // nowhere to go stays, parked illegally.
        private static int MovePlayerCars(TransitStop stop, Vector3 right, ref int left)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            BusZone zone = stop.Zone;
            int moved = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                VehicleController vehicle = all[i];
                if (vehicle == null || vehicle.controlledByPlayer || !vehicle.gameObject.activeInHierarchy || vehicle.vehicleInstance == null || !GoingPublic.MineVehicle(vehicle))
                {
                    continue;   // in a Going Public session another player's car is on the game's list too, and never moved here
                }
                Vector3 offset = Flat(vehicle.transform.position - stop.Lane);
                float aside = Vector3.Dot(offset, right);
                if (aside < BusBay.LaneHalf - 0.6f || aside > stop.KerbAside + 0.3f)
                {
                    continue;   // in the lane (it is driving) or up on the pavement: not in the bay
                }
                float along = Vector3.Dot(offset, stop.Along);
                float half = Half(vehicle);
                if (!BusBay.CarInZone(along - half, along + half, zone.From, zone.To))
                {
                    continue;
                }
                Rigidbody body = vehicle.TryGetComponent(out NWH.VehiclePhysics2.VehicleController physics) ? physics.vehicleRigidbody : null;
                if ((body != null && body.velocity.sqrMagnitude > Moving * Moving) || CitySpots.InView(vehicle.transform.position))
                {
                    left++;
                    continue;
                }
                if (!VehicleTow.ParkNearby(vehicle, vehicle.transform.position, Refused))
                {
                    Logger?.Info("Transit: a car of the player's stands in the bay at " + stop.Name + " and no free space was found near it: it is left where it is (parked illegally).");
                    continue;
                }
                moved++;
                Logger?.Info("Transit: a car of the player's stood in the bay at " + stop.Name + ": moved to a free space nearby, out of view.");
                if (!_told)
                {
                    _told = true;
                    Notifications.Show(NotificationType.Info, "reallife_bus_car_moved", null, 10f, trackOnSaveGame: false);
                }
            }
            return moved;
        }

        // A space no car may be moved to: in view, or in any bus zone or within its margin.
        private static bool Refused(Vector3 place)
        {
            return CitySpots.InView(place) || InAnyZone(place, 2.5f);
        }

        // Whether a car of the given half length standing at a place would stand in a bus zone or its margin.
        private static bool InAnyZone(Vector3 place, float half)
        {
            for (int i = 0; i < TransitMap.Stops.Count; i++)
            {
                TransitStop stop = TransitMap.Stops[i];
                if (!stop.HasBay || stop.Zone == null)
                {
                    continue;
                }
                Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
                Vector3 offset = Flat(place - stop.Lane);
                float aside = Vector3.Dot(offset, right);
                float along = Vector3.Dot(offset, stop.Along);
                if (aside > BusBay.LaneHalf - 1f && aside < stop.KerbAside + 0.5f && Mathf.Abs(offset.y) < 3f
                    && BusBay.CarInZone(along - half, along + half, stop.Zone.From, stop.Zone.To))
                {
                    return true;
                }
            }
            return false;
        }

        private static float Half(VehicleController vehicle)
        {
            try
            {
                float length = VehicleHelper.GetVehicleColliderCenterAndSize(vehicle.vehicleInstance.vehicleTypeName).Item2.z;
                return length > 0.5f ? length * 0.5f : 2.5f;
            }
            catch (Exception)
            {
                return 2.5f;
            }
        }

        // One made zone after another, every few seconds: a lane that has come back whole is cut again and the cars in the zone
        // go; where one of them is in view, the zone is made again from the timer's slices.
        private static void Watch()
        {
            int count = TransitMap.Stops.Count;
            for (int tried = 0; tried < count; tried++)
            {
                _watch = (_watch + 1) % count;
                TransitStop stop = TransitMap.Stops[_watch];
                if (!stop.HasBay || stop.Zone == null || !stop.Zone.Made)
                {
                    continue;
                }
                try
                {
                    bool clear = Sweep(stop, Vector3.Cross(Vector3.up, stop.Along), out int sent);
                    if (sent > 0)
                    {
                        stop.Zone.CarsSent += sent;
                        Logger?.Info("Transit: " + sent + " of the game's parked car(s) had come back into the bay at " + stop.Name + " and were taken in out of view.");
                    }
                    if (!clear)
                    {
                        stop.Zone.Made = false;
                        stop.Zone.Waiting = "a car in it is in view";
                        stop.ZoneLook = 0f;
                        Pending.Add(stop);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                return;
            }
        }
    }
}
