using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Helpers;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Brings the player's car to them after they have been taken away without it (a crash that
    /// ends in hospital). It is parked in the nearest free kerbside space the game itself would
    /// offer for auto-parking; if there is none, the game's own tow to the nearest repair shop is
    /// used instead. Runs once per hospital trip, never per frame.
    /// </summary>
    public static class VehicleTow
    {
        public static int Fee => Prices.At(180);

        private const float SearchRadius = 80f;

        public static IModLogger Logger;

        private static readonly Collider[] Lanes = new Collider[48];
        private static readonly Collider[] Blockers = new Collider[16];

        /// <summary>Moves the car to a space near the given point and bills the tow. Safe to call with null.</summary>
        public static void BringNear(VehicleInstance instance, Vector3 near)
        {
            BringNear(instance, near, Fee, "reallife_event_car_towed", "reallife_event_car_towed_shop", "reallife_event_car_not_towed", "reallife_transaction_towing");
        }

        /// <summary>
        /// The same for somebody else's reasons (the police impound, Law/Custody): another fee, and the three
        /// messages that say where the car went (parked nearby, at a repair shop, not moved).
        /// </summary>
        public static void BringNear(VehicleInstance instance, Vector3 near, int fee, string parkedKey, string shopKey, string leftKey, string transactionKey)
        {
            if (instance == null)
            {
                return;
            }
            try
            {
                VehicleController vehicle = Find(instance);
                if (vehicle == null)
                {
                    Logger?.Warn("Tow: the car is not in the city scene; left where it is.");
                    return;
                }

                if (TryParkNear(vehicle, near))
                {
                    Charge(fee, transactionKey);
                    Notify(parkedKey, null, fee);
                    return;
                }

                // No kerb space nearby: fall back to the game's own tow, which uses a free repair-shop bay.
                Address shop = VehicleHelper.TowVehicle(instance, "ba:towdestination_autorepairshop");
                if (shop != null && !shop.IsUndefined())
                {
                    Charge(fee, transactionKey);
                    Notify(shopKey, shop.ToFormattedString(), fee);
                }
                else
                {
                    Logger?.Info("Tow: no free space or repair bay found; the car stays at the crash site.");
                    Notifications.Show(NotificationType.Warning, leftKey, null, 8f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static VehicleController Find(VehicleInstance instance)
        {
            foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
            {
                if (vehicle != null && vehicle.vehicleInstance != null && vehicle.vehicleInstance.id == instance.id)
                {
                    return vehicle;
                }
            }
            return null;
        }

        // Kerbside spaces are generated per parking lane on demand, so ask each nearby lane for its spaces.
        private static bool TryParkNear(VehicleController vehicle, Vector3 near)
        {
            float length = VehicleHelper.GetVehicleColliderCenterAndSize(vehicle.vehicleInstance.vehicleTypeName).Item2.z;
            int count = Physics.OverlapSphereNonAlloc(near, SearchRadius, Lanes, LayerHelper.parkingAreaLayerMask, QueryTriggerInteraction.Collide);

            // Nearest lanes first; the list is short, so a simple selection pass is enough.
            for (int pass = 0; pass < count; pass++)
            {
                int best = -1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (Lanes[i] == null)
                    {
                        continue;
                    }
                    float sqr = (Lanes[i].ClosestPoint(near) - near).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                Collider laneCollider = Lanes[best];
                Lanes[best] = null;

                var lane = laneCollider.GetComponentInParent<ParkingLaneGenerator>();
                if (lane == null || lane.designatedDeliveryVanSpot || lane.isHandicapParking)
                {
                    continue;
                }
                ParkingLaneGenerator.RegenerateAutoParkSpotsNear(laneCollider.ClosestPoint(near));

                List<AutoParkSpot> spots = lane.autoParkSpots;
                if (spots == null)
                {
                    continue;
                }
                foreach (AutoParkSpot spot in spots)
                {
                    if (spot == null || spot.transform.parent == null || spot.maxVehicleLength < length)
                    {
                        continue;
                    }
                    Vector3 position = spot.transform.position;
                    Quaternion rotation = spot.transform.parent.rotation;
                    float width = spot.visuals != null ? spot.visuals.size.y : 2.4f;
                    if (IsOccupied(vehicle, position, rotation, width, length))
                    {
                        continue;
                    }

                    Teleport(vehicle, position, rotation);
                    Rigidbody moved = vehicle.TryGetComponent(out NWH.VehiclePhysics2.VehicleController drives) ? drives.vehicleRigidbody : null;
                    float apart = moved != null ? Vector3.Distance(moved.position, vehicle.transform.position) : 0f;
                    Logger?.Info("Tow check (one car): the model and the map's marker stand " + apart.ToString("0.00") + " m from the car's body after the move" + (apart < 0.5f ? ": PASS" : ": FAIL"));
                    vehicle.vehicleInstance.parkingState = ParkingState.Legal;
                    vehicle.vehicleInstance.SetStreetData(string.Empty, 0);
                    if (vehicle.poi != null)
                    {
                        vehicle.poi.SetBackground(InstanceBehavior<GlobalReferences>.Instance.vehiclePOIBackgroundColor);
                    }
                    if (vehicle is CarController car)
                    {
                        car.UpdateParkingZone();
                    }
                    vehicle.SavePosition();
                    vehicle.UpdateNavMeshTargets();
                    Logger?.Info($"Tow: car parked {Mathf.Sqrt((position - near).sqrMagnitude):F0} m from where it was wanted.");
                    return true;
                }
            }
            return false;
        }

        // The game's own move, as its tow does it (VehicleHelper.TeleportVehicleToGround: the body to the ground under the
        // space, the car's reset, its wheels and its engine), then the car is put there as one: the game moves only the
        // body, and the model and the map's marker, which go with the car's transform, follow at the next physics step
        // for a car that is moving. A parked car's body is held still (kinematic); it is let go for the game's call (the
        // velocities the game sets would otherwise be refused, with a warning each) and held again before any physics
        // step, so the transform never followed: the model and the marker stayed at the crash site while the collision
        // and the car's door went to the space, and the player walked through a car they could not get into. So the
        // transform is set to where the body went, and the body to it, before the car is held again.
        private static void Teleport(VehicleController vehicle, Vector3 position, Quaternion rotation)
        {
            Rigidbody body = vehicle.TryGetComponent(out NWH.VehiclePhysics2.VehicleController physics) ? physics.vehicleRigidbody : null;
            bool held = body != null && body.isKinematic;
            if (held)
            {
                body.isKinematic = false;
            }
            try
            {
                VehicleHelper.TeleportVehicleToGround(vehicle, position, rotation);
            }
            finally
            {
                if (body != null)
                {
                    Vector3 at = body.position;
                    Quaternion turn = body.rotation;
                    vehicle.transform.SetPositionAndRotation(at, turn);
                    body.position = at;
                    body.rotation = turn;
                    Physics.SyncTransforms();
                    if (held)
                    {
                        body.isKinematic = true;
                    }
                }
            }
        }

        // The same test the game's auto-park uses for a free space.
        private static bool IsOccupied(VehicleController vehicle, Vector3 position, Quaternion rotation, float width, float length)
        {
            var half = new Vector3(width, 2f, length) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(position, half, Blockers, rotation, LayerHelper.freeParkingSpotDetectionMask, QueryTriggerInteraction.Ignore);
            if (count >= Blockers.Length)
            {
                return true;
            }
            Transform own = vehicle.transform;
            for (int i = 0; i < count; i++)
            {
                if (!Blockers[i].transform.IsChildOf(own))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Charge(int fee, string transactionKey)
        {
            GameManager.ChangeMoneySafe(-fee, new TransactionInfo(transactionKey), null, null, force: true);
        }

        private static void Notify(string key, string address, int fee)
        {
            var data = new Dictionary<string, string> { { "amount", ((float)fee).ToCurrencyFormat() } };
            if (address != null)
            {
                data["address"] = address;
            }
            Notifications.Show(NotificationType.Info, key, data, 10f);
        }
    }
}
