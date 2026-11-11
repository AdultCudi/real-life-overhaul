using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Extensions;
using Helpers;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Brings the player's car to them after a crash that ends in hospital: parked in the nearest free kerbside space the
    /// game's auto-park would offer, or else the game's own tow. Once per trip, never per frame.
    /// </summary>
    public static class VehicleTow
    {
        public static int Fee => Prices.At(180);

        /// <summary>What a car's policy pays of a tow's fee (Insurance, wired in Wiring/InsuranceLinks), asked once the car is towed and before the fee is taken. Null: nothing.</summary>
        public static Func<string, int, int> Cover;

        private const float SearchRadius = 80f;

        public static IModLogger Logger;

        private static readonly Collider[] Lanes = new Collider[48];
        private static readonly Collider[] Blockers = new Collider[16];

        /// <summary>
        /// The car the player drives, remembered as they are taken from it: null for none, for one the game carries, and for
        /// another player's car in Going Public (GoingPublic.MineVehicle), which is left where it stopped, never towed or
        /// billed, and the player told once (WhoseCar.BringLater).
        /// </summary>
        public static VehicleInstance LeftBehind(VehicleController driven)
        {
            bool has = driven != null && driven.vehicleInstance != null && driven.vehicleType != null;
            bool carried = has && driven.vehicleType.spawnInPlayerObject;
            bool mine = has && GoingPublic.MineVehicle(driven);
            if (WhoseCar.BringLater(has, carried, mine))
            {
                return driven.vehicleInstance;
            }
            if (has && !carried)
            {
                Logger?.Info("Tow: the car being driven is another player's (Going Public); it is left where it stopped and nothing is billed.");
                try
                {
                    Notifications.Show(NotificationType.Info, "reallife_tow_not_yours", null, 10f);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            return null;
        }

        /// <summary>
        /// Moves the car to a space near the given point and bills the tow. Safe to call with null. A tow after a crash is one a car's policy may pay
        /// its part of (<paramref name="insured"/>); one after a stay in prison is not.
        /// </summary>
        public static void BringNear(VehicleInstance instance, Vector3 near, bool insured = true)
        {
            BringNear(instance, near, Fee, "reallife_event_car_towed", "reallife_event_car_towed_shop", "reallife_event_car_not_towed", "reallife_transaction_towing", insured);
        }

        /// <summary>The part of a tow's fee a car's policy pays: never under nothing, never over the fee, and nothing where no policy answers.</summary>
        public static int Covered(string id, int fee)
        {
            Func<string, int, int> cover = Cover;
            if (cover == null || fee <= 0 || string.IsNullOrEmpty(id))
            {
                return 0;
            }
            try
            {
                return Mathf.Clamp(cover(id, fee), 0, fee);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0;
            }
        }

        /// <summary>The same for somebody else's reasons (the police impound): another fee, and the messages of where the car went. No policy pays for those.</summary>
        public static void BringNear(VehicleInstance instance, Vector3 near, int fee, string parkedKey, string shopKey, string leftKey, string transactionKey, bool insured = false)
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
                if (!GoingPublic.MineVehicle(vehicle))
                {
                    Logger?.Info("Tow: the car is another player's (Going Public); it is never moved or billed for here.");
                    return;
                }

                if (TryParkNear(vehicle, near))
                {
                    int paid = insured ? Covered(instance.id, fee) : 0;
                    Charge(fee - paid, transactionKey);
                    Notify(paid > 0 ? parkedKey + CoveredSuffix : parkedKey, null, fee - paid, paid);
                    return;
                }

                // No kerb space nearby: fall back to the game's own tow, which uses a free repair-shop bay.
                Address shop = VehicleHelper.TowVehicle(instance, "ba:towdestination_autorepairshop");
                if (shop != null && !shop.IsUndefined())
                {
                    int paid = insured ? Covered(instance.id, fee) : 0;
                    Charge(fee - paid, transactionKey);
                    Notify(paid > 0 ? shopKey + CoveredSuffix : shopKey, shop.ToFormattedString(), fee - paid, paid);
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

        /// <summary>Parks one of the player's cars in the nearest free kerbside space not refused; no fee, no message, false if none.</summary>
        public static bool ParkNearby(VehicleController vehicle, Vector3 near, Func<Vector3, bool> refuse)
        {
            if (vehicle == null || vehicle.vehicleInstance == null || !GoingPublic.MineVehicle(vehicle))
            {
                return false;   // another player's car, in Going Public, is theirs: never moved here
            }
            try
            {
                return TryParkNear(vehicle, near, refuse);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // Kerbside spaces are generated per parking lane on demand, so ask each nearby lane for its spaces.
        private static bool TryParkNear(VehicleController vehicle, Vector3 near, Func<Vector3, bool> refuse = null)
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
                    // Never a space in a bus stop's kerb: when it is the mod that moves a car, the player's included, it does not put it there.
                    if (IsOccupied(vehicle, position, rotation, width, length) || (refuse != null && refuse(position)) || BusZones.Refuses(position))
                    {
                        continue;
                    }

                    Teleport(vehicle, position, rotation);
                    Rigidbody moved = vehicle.TryGetComponent(out NWH.VehiclePhysics2.VehicleController drives) ? drives.vehicleRigidbody : null;
                    float apart = moved != null ? Vector3.Distance(moved.position, vehicle.transform.position) : 0f;
                    Logger?.Info("Tow check (one car): the model and the map's marker stand " + apart.ToString("0.00", CultureInfo.InvariantCulture)
                                 + " m from the car's body after the move" + (apart < 0.5f ? ": PASS" : ": FAIL"));
                    vehicle.vehicleInstance.parkingState = ParkingState.Legal;
                    vehicle.vehicleInstance.SetStreetData(string.Empty, 0);
                    if (vehicle.poi != null)
                    {
                        vehicle.poi.SetBackground(InstanceBehavior<GlobalReferences>.Instance.vehiclePOIBackgroundColor);
                    }
                    // The game's parking sign is the driven car's, on the dashboard: asked of any other car it fails.
                    if (vehicle is CarController car && InstanceBehavior<GameManager>.Instance != null && InstanceBehavior<GameManager>.Instance.selectedVehicle == vehicle)
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

        // The game's own move, then the car put there as one: the game moves only the body, and a parked car's held body would
        // leave the model and the map's marker at the crash site. So the transform follows the body, and the body it.
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

        /// <summary>Added to a tow's message key when a policy paid part of the fee: the message then says what it paid.</summary>
        public const string CoveredSuffix = "_covered";

        private static void Charge(int fee, string transactionKey)
        {
            if (fee > 0)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(-fee, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        private static void Notify(string key, string address, int fee, int paid = 0)
        {
            var data = new Dictionary<string, string> { { "amount", ((float)fee).ToCurrencyFormat() } };
            if (address != null)
            {
                data["address"] = address;
            }
            if (paid > 0)
            {
                data["payout"] = ((float)paid).ToCurrencyFormat();
            }
            Notifications.Show(NotificationType.Info, key, data, 10f);
        }
    }
}
