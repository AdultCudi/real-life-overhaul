using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A hired vehicle, so that the driving jobs can be worked before the player owns a car.
    ///
    /// It is the game's own job van, made the game's own way: exactly what its delivery-van job
    /// does when the player accepts a round (DeliveryJobStartController.OnAcceptJob). A
    /// <c>DeliveryVehicleInstance</c> of the van type the game's depots use, unsellable and not
    /// assignable to a warehouse, spawned with <c>VehicleHelper.CreateAndSpawnVehicle</c>. That
    /// prefab carries the game's <c>DeliveryJobVehicle</c> component, and that component is what
    /// makes this safe:
    ///  - when the van object is destroyed it takes its own entry out of the save's vehicle list;
    ///  - once an hour it destroys any van that belongs to no delivery mission, is not being
    ///    driven and is not in view.
    /// So a save made in the middle of a hire holds one vehicle of a type the base game made and
    /// knows how to remove: loaded without this mod, the van is an orphan job van and the game
    /// takes it away within the hour, out of the player's sight. Loaded with the mod, the hire
    /// is remembered by the van's id (one line in modData) and the van is taken back as soon as
    /// the player is not sitting in it. Nothing else is written, and no mission is ever created
    /// or touched.
    ///
    /// While a hire is on, that hourly check is unhooked for this one van (it would take the van
    /// from behind a courier's back at the top of the hour) and the van is removed here instead:
    /// at the end of the shift, once the player has got out. A loaded save builds the van afresh
    /// with the game's check in place.
    ///
    /// If the prefab turns out not to carry the component, the hire is refused and the van is
    /// deleted on the spot: nothing without the game's own clean-up is ever left standing.
    /// </summary>
    public sealed class JobRental
    {
        public const int DailyFee = 45;                 // charged once a day, however many shifts
        private const string Key = "reallife:job_rental";
        private const string FallbackType = "ba:vehicletype_umcdesertdeliveryjob";
        private const string FeeTransaction = "reallife_transaction_job_rental";
        private const string FeeCategory = "ba:transactioncategory_licensingfees";
        private const float HalfWidth = 1.05f;
        private const float HalfLength = 2.7f;

        private readonly IModLogger _logger;
        private readonly List<Waypoint> _route = new List<Waypoint>(24);
        private CarController _car;
        private string _id;             // of the hired van, or of one left by a saved hire
        private int _paidDay;           // the day the fee was last paid
        private bool _returning;        // the shift is over: taken back once the player is out of it
        private bool _hooked = true;    // the game's hourly check is still attached to the van
        private float _hookTries;
        private bool _failed;

        public JobRental(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>The hired van while a shift has it, or null.</summary>
        public CarController Car => !_returning && _car != null ? _car : null;

        /// <summary>When the van was last put on the road (Time.time): the game resets its destination guider a frame later.</summary>
        public float SpawnedAt { get; private set; } = -100f;

        public bool PaidToday => _paidDay == TimeHelper.CurrentDay && _paidDay > 0;

        /// <summary>Whether a van could be hired right now. The reason is a key the phone can say.</summary>
        public bool CanHire(bool test, out string reasonKey)
        {
            reasonKey = null;
            if (_failed)
            {
                reasonKey = "reallife_job_no_car";
            }
            else if (!test && !PaidToday && SaveGameManager.Current.Money < DailyFee)
            {
                reasonKey = "reallife_job_rental_no_money";
            }
            return reasonKey == null;
        }

        /// <summary>
        /// Puts a hired van at the roadside near a point and charges the day's fee if it has not
        /// been paid today. Null (and nothing charged, nothing left behind) if there is no free
        /// stretch of road near, or the game would not make one.
        /// </summary>
        public CarController Hire(Vector3 near, bool test, out string reasonKey)
        {
            reasonKey = null;
            if (_car != null && (!_returning || JobSpots.Flat(_car.transform.position, near) < 80f))
            {
                // The van of the shift before is still standing near (or they are sitting in it): it is kept on,
                // and a new day's fee is due like any other.
                if (!CanHire(test, out reasonKey))
                {
                    return null;
                }
                _returning = false;
                Charge(test);
                return _car;
            }
            if (_car != null)
            {
                reasonKey = "reallife_job_rental_returning";   // still out, somewhere else: it has to come back first
                return null;
            }
            if (!CanHire(test, out reasonKey))
            {
                return null;
            }
            if (!FindSpot(near, out Vector3 spot, out Quaternion heading))
            {
                reasonKey = "reallife_job_no_car";
                return null;
            }
            VehicleController made = null;
            try
            {
                string type = FallbackType;
                string colour = null;
                DeliveryJobStartController depot = DeliveryJobStartController.GetFirst();
                if (depot != null && depot.building != null && depot.building.deliveryJobStartLocation != null)
                {
                    DeliveryJobStartLocation location = depot.building.deliveryJobStartLocation;
                    type = string.IsNullOrEmpty(location.vehicleTypeName) ? type : location.vehicleTypeName;
                    colour = location.vehicleColor != null ? location.vehicleColor.name : null;
                }
                var instance = new DeliveryVehicleInstance(type)
                {
                    id = UuidHelper.GenerateBase64Uuid(),
                    fuel = VehicleTypeHelper.GetVehicleType(type).maxFuel,
                    zeroSellingPrice = true,
                    preventWarehouseAssign = true
                };
                if (colour != null)
                {
                    instance.vehicleColorName = colour;
                }
                made = VehicleHelper.CreateAndSpawnVehicle(instance, spot, heading);
                var car = made as CarController;
                if (car == null || made.GetComponent<DeliveryJobVehicle>() == null)
                {
                    // Not the van the game cleans up after: it does not stay.
                    _logger?.Warn("Jobs: the game's job van ('" + type + "') did not come with its own clean-up; no vehicle is hired out this session.");
                    instance.Delete(made);
                    _failed = true;
                    reasonKey = "reallife_job_no_car";
                    return null;
                }
                _car = car;
                _id = instance.id;
                _returning = false;
                _hooked = true;
                _hookTries = 0f;
                SpawnedAt = Time.time;
                if (!Charge(test))
                {
                    JobText.Toast(NotificationType.Info, "reallife_job_rental_again", 7f);
                }
                _logger?.Info("Jobs: a van was hired ('" + type + "', id " + _id + ") and stands " + JobSpots.Flat(near, spot).ToString("F0") + " m from the player.");
                return car;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Jobs: the game would not make a hired van; no vehicle is hired out this session.");
                _failed = true;
                try
                {
                    if (made != null && made.vehicleInstance != null)
                    {
                        made.vehicleInstance.Delete(made);
                    }
                }
                catch (Exception again)
                {
                    _logger?.Error(again);
                }
                _car = null;
                _id = null;
                reasonKey = "reallife_job_no_car";
                return null;
            }
        }

        // The day's fee, once a day. True if it was charged just now.
        private bool Charge(bool test)
        {
            if (test || PaidToday)
            {
                return false;
            }
            _paidDay = TimeHelper.CurrentDay;
            GameManager.ChangeMoneySafe(-DailyFee, new TransactionInfo(FeeTransaction, FeeCategory), null, null, force: true);
            JobText.Toast(NotificationType.Info, "reallife_job_rental_hired", "amount", JobText.Money(DailyFee), 9f);
            return true;
        }

        /// <summary>The shift is over: the van goes back as soon as the player is out of it.</summary>
        public void Return()
        {
            if (_car != null && !_returning)
            {
                _returning = true;
                if (IsDriven(_id))
                {
                    JobText.Toast(NotificationType.Info, "reallife_job_rental_return", 9f);
                }
            }
        }

        /// <summary>Once a second from the director, job or no job. A few comparisons when there is no van.</summary>
        public void Tick()
        {
            if (_id == null)
            {
                return;
            }
            try
            {
                if (_car == null)
                {
                    // Wrecked and removed by the game, or left over from a save and not found on the street yet.
                    _car = Find(_id);
                    if (_car == null)
                    {
                        if (!_returning || !StillSaved(_id))
                        {
                            _id = null;
                            _returning = false;
                        }
                        return;
                    }
                }
                if (_hooked && !_returning)
                {
                    Unhook();
                }
                if (_returning && !IsDriven(_id) && !BuildingManager.IsInsideBuilding)
                {
                    TakeBack();
                }
            }
            catch (Exception e)
            {
                // Not left standing without its own clean-up: taken back now if it can be, forgotten either way.
                _logger?.Error(e);
                try
                {
                    if (_car != null && !IsDriven(_id))
                    {
                        TakeBack();
                    }
                }
                catch (Exception again)
                {
                    _logger?.Error(again);
                }
                _id = null;
                _car = null;
                _returning = false;
            }
        }

        /// <summary>The city is going, or jobs are switched off: an idle van is taken back now.</summary>
        public void Dispose()
        {
            try
            {
                if (_car != null && !IsDriven(_id) && !GameManager.isCitySceneBeingUnloaded)
                {
                    TakeBack();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _car = null;
        }

        // ---------------------------------------------------------------- the save

        public void Save(Dictionary<string, string> data)
        {
            if (data == null)
            {
                return;
            }
            if (_paidDay <= 0 && _id == null)
            {
                data.Remove(Key);
                return;
            }
            data[Key] = _paidDay.ToString(CultureInfo.InvariantCulture) + "|" + (_id ?? string.Empty);
        }

        /// <summary>After a load: the fee already paid today, and a van that a saved hire left on the street (taken back by <see cref="Tick"/>).</summary>
        public void Load(Dictionary<string, string> data)
        {
            _car = null;
            _id = null;
            _paidDay = 0;
            _returning = false;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] parts = text.Split('|');
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _paidDay);
            if (parts.Length > 1 && parts[1].Length > 0 && StillSaved(parts[1]))
            {
                _id = parts[1];
                _returning = true;   // no job is ever on after a load
                _hooked = true;      // the loaded van was built afresh, with the game's hourly check on it
                _hookTries = 0f;
                _logger?.Info("Jobs: a hired van from a saved shift is still out; it is taken back once the player is not in it.");
            }
        }

        // ---------------------------------------------------------------- small parts

        private void TakeBack()
        {
            CarController car = _car;
            string id = _id;
            _car = null;
            _id = null;
            _returning = false;
            if (car != null)
            {
                // The way the game removes its own job van: the object is destroyed, and the van's own component takes
                // it off the save's vehicle list as it goes. Taken off the list here as well, so that a save made in the
                // same frame cannot carry it.
                VehicleInstance instance = car.vehicleInstance;
                UnityEngine.Object.Destroy(car.gameObject);
                if (instance != null && SaveGameManager.Current != null)
                {
                    SaveGameManager.Current.VehicleInstances.Remove(instance);
                }
                _logger?.Info("Jobs: the hired van (" + id + ") was taken back.");
            }
        }

        // The van's own hourly "does anyone still need me" check, unhooked for the length of the hire.
        private void Unhook()
        {
            _hookTries++;
            DeliveryJobVehicle check = _car.GetComponent<DeliveryJobVehicle>();
            Action hourly = GlobalEvents.onNewHour;
            if (check != null && hourly != null)
            {
                Delegate[] listeners = hourly.GetInvocationList();
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (ReferenceEquals(listeners[i].Target, check))
                    {
                        GlobalEvents.onNewHour = (Action)Delegate.Remove(GlobalEvents.onNewHour, listeners[i]);
                        _hooked = false;
                    }
                }
            }
            if (_hooked && _hookTries >= 5f)
            {
                _hooked = false;   // never attached: nothing to unhook
            }
        }

        private static bool IsDriven(string id)
        {
            return SaveGameManager.Current != null && id != null && SaveGameManager.Current.ActiveVehicleId == id;
        }

        private static bool StillSaved(string id)
        {
            List<VehicleInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id && all[i] is DeliveryVehicleInstance)
                {
                    return true;
                }
            }
            return false;
        }

        private static CarController Find(string id)
        {
            List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < own.Count; i++)
            {
                if (own[i] is CarController car && car.vehicleInstance != null && car.vehicleInstance.id == id
                    && car.vehicleInstance is DeliveryVehicleInstance)
                {
                    return car;
                }
            }
            return null;
        }

        // A stretch of lane near the player with nothing standing in it: the kerb side first, then the lane itself.
        private bool FindSpot(Vector3 near, out Vector3 spot, out Quaternion heading)
        {
            spot = default;
            heading = Quaternion.identity;
            if (!TrafficTools.Ready)
            {
                return false;
            }
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(near, Vector3.zero, 45f);
            if (anchor == null)
            {
                return false;
            }
            TrafficTools.WalkForward(anchor, 70f, 20, _route);
            for (int i = 0; i + 1 < _route.Count; i++)
            {
                Waypoint point = _route[i];
                if (point == null || point.enter || point.exit)
                {
                    continue;   // not inside a junction
                }
                Vector3 runs = _route[i + 1].position - point.position;
                runs.y = 0f;
                if (runs.sqrMagnitude < 0.25f)
                {
                    continue;
                }
                runs.Normalize();
                Quaternion along = Quaternion.LookRotation(runs);
                Vector3 kerb = Vector3.Cross(Vector3.up, runs);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 at = point.position + kerb * (side == 0 ? 0.8f : 0f);
                    if (!StreetSpots.Ground(at, out float ground) || Mathf.Abs(ground - point.position.y) > 0.6f)
                    {
                        continue;
                    }
                    at.y = ground + 0.05f;
                    if (SceneParking.IsFree(at, along, HalfWidth, HalfLength, 0.5f) && JobSpots.Flat(at, near) > 5f)   // never on top of the player
                    {
                        spot = at;
                        heading = along;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
