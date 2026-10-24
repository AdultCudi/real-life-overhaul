using System;
using System.Collections;
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
    /// A hired van: the game's own job van, made as its delivery job makes one. Its <c>DeliveryJobVehicle</c> takes it off the
    /// save's list when destroyed and destroys an idle van each hour, so a save loaded without the mod loses it. The hire is one
    /// line in modData; the van goes back once the player is out of it, the hourly check unhooked meanwhile. A prefab without
    /// the component is refused at once. No mission is created or touched.
    /// For a heavy load the hire is the game's Freight Truck instead, made as the driving school makes its car: it has no
    /// clean-up of the game's, so it comes off the save's vehicle list for each serialisation and back a frame later, and is
    /// deleted the game's own way when it goes back. No cost a frame: the director ticks it once a second.
    /// </summary>
    public sealed class JobRental
    {
        public static int DailyFee => Prices.At(125);                // charged once a day, however many shifts
        private const string Key = "reallife:job_rental";
        private const string FallbackType = "ba:vehicletype_umcdesertdeliveryjob";
        private const string FeeTransaction = "reallife_transaction_job_rental";
        private const string FeeCategory = "ba:transactioncategory_licensingfees";
        private const float HalfWidth = 1.05f;   // metres: the van's half width, for a free stretch of road
        private const float HalfLength = 2.7f;   // metres: its half length
        private const string TruckType = "ba:vehicletype_" + HeavyRules.FreightTruck;
        private const float TruckHalfWidth = 1.5f;    // metres: the Freight Truck's body and a little more (GarageFit)
        private const float TruckHalfLength = 5.9f;
        private const float TruckFromPlayer = 8f;     // metres: never set down on top of the player

        /// <summary>A day's hire of a truck for heavy loads; a van's fee paid the same day counts towards it.</summary>
        public static int TruckFee => HeavyRules.TruckHireFee;

        private readonly MonoBehaviour _host;
        private readonly IModLogger _logger;
        private readonly List<Waypoint> _route = new List<Waypoint>(24);
        private CarController _car;
        private int _tickets;   // parking tickets the game has written on the van, as last seen (LentVehicle)
        private string _id;             // of the hired van, or of one left by a saved hire
        private int _paidDay;           // the day the fee was last paid
        private bool _returning;        // the shift is over: taken back once the player is out of it
        private bool _hooked = true;    // the game's hourly check is still attached to the van
        private float _hookTries;
        private bool _failed;
        private bool _truckFailed;      // the game would not make the truck: heavy loads go without one this session
        private bool _heavy;            // what is out is the truck
        private int _paidAmount;        // paid on _paidDay so far: a van's fee, or a truck's
        private VehicleInstance _truck; // the truck's record while it is out, for the saves

        public JobRental(MonoBehaviour host, IModLogger logger)
        {
            _host = host;
            _logger = logger;
        }

        /// <summary>Whether what is hired out now is the truck.</summary>
        public bool Heavy => _heavy && _car != null;

        /// <summary>The hired van while a shift has it, or null.</summary>
        public CarController Car => !_returning && _car != null ? _car : null;

        /// <summary>When the van was last put on the road (Time.time): the game resets its destination guider a frame later.</summary>
        public float SpawnedAt { get; private set; } = -100f;

        private bool PaidToday => _paidDay == TimeHelper.CurrentDay && _paidDay > 0;

        // What is still to pay today for a van, or for the truck.
        private int Due(bool heavy)
        {
            int paid = PaidToday ? _paidAmount : 0;
            return heavy ? HeavyRules.TruckFeeDue(paid, TruckFee) : paid > 0 ? 0 : DailyFee;
        }

        /// <summary>Whether a van could be hired right now. The reason is a key the phone can say.</summary>
        public bool CanHire(bool test, out string reasonKey) => CanHire(test, false, out reasonKey);

        /// <summary>Whether a van, or with <paramref name="heavy"/> the truck, could be hired right now.</summary>
        public bool CanHire(bool test, bool heavy, out string reasonKey)
        {
            reasonKey = null;
            if (heavy ? _truckFailed : _failed)
            {
                reasonKey = "reallife_job_no_car";
            }
            else if (!test && SaveGameManager.Current.Money < Due(heavy))
            {
                reasonKey = heavy ? "reallife_job_heavy_no_money" : "reallife_job_rental_no_money";
            }
            return reasonKey == null;
        }

        /// <summary>Puts a hired van at the roadside near a point, charging the day's fee if unpaid; null, with nothing charged or left, if it cannot.</summary>
        public CarController Hire(Vector3 near, bool test, out string reasonKey) => Hire(near, test, false, out reasonKey);

        /// <summary>As the van's hire; with <paramref name="heavy"/> the truck for a heavy load.</summary>
        public CarController Hire(Vector3 near, bool test, bool heavy, out string reasonKey)
        {
            reasonKey = null;
            if (_car != null && _heavy != heavy)
            {
                // The other kind is out: it goes back first, at once if nobody sits in it.
                if (IsDriven(_id))
                {
                    reasonKey = "reallife_job_rental_swap";
                    return null;
                }
                TakeBack();
            }
            if (_car != null && (!_returning || JobSpots.Flat(_car.transform.position, near) < 80f))
            {
                // The van of the shift before is still near, or they are sitting in it: it is kept on, and a new day's fee is due.
                if (!CanHire(test, heavy, out reasonKey))
                {
                    return null;
                }
                _returning = false;
                Charge(test, heavy);
                return _car;
            }
            if (_car != null)
            {
                reasonKey = "reallife_job_rental_returning";   // still out somewhere else: it has to come back first
                return null;
            }
            if (!CanHire(test, heavy, out reasonKey))
            {
                return null;
            }
            if (!FindSpot(near, heavy, out Vector3 spot, out Quaternion heading))
            {
                reasonKey = heavy ? "reallife_job_heavy_no_room" : "reallife_job_no_car";
                return null;
            }
            if (heavy)
            {
                return HireTruck(near, spot, heading, test, out reasonKey);
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
                _tickets = 0;
                _returning = false;
                _hooked = true;
                _hookTries = 0f;
                _heavy = false;
                SpawnedAt = Time.time;
                if (!Charge(test, false))
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

        // The truck, made as the driving school makes its car: no clean-up of the game's comes with it, so the saves and the
        // return look after it (BeforeSerialise, TakeBack).
        private CarController HireTruck(Vector3 near, Vector3 spot, Quaternion heading, bool test, out string reasonKey)
        {
            reasonKey = "reallife_job_no_car";
            VehicleController made = null;
            DeliveryVehicleInstance instance = null;
            try
            {
                VehicleType known = VehicleTypeHelper.GetVehicleType(TruckType);
                if (known == null)
                {
                    _logger?.Warn("Jobs: the game has no vehicle type '" + TruckType + "'; no truck is hired out this session.");
                    _truckFailed = true;
                    return null;
                }
                instance = new DeliveryVehicleInstance(TruckType)
                {
                    id = UuidHelper.GenerateBase64Uuid(),
                    fuel = known.maxFuel,
                    zeroSellingPrice = true,
                    preventWarehouseAssign = true
                };
                made = VehicleHelper.CreateAndSpawnVehicle(instance, spot, heading);
                var car = made as CarController;
                if (car == null)
                {
                    instance.Delete(made);
                    _truckFailed = true;
                    return null;
                }
                _car = car;
                _id = instance.id;
                _truck = instance;
                _heavy = true;
                _tickets = 0;
                _returning = false;
                _hooked = false;   // the truck has no hourly check of the game's to unhook
                SpawnedAt = Time.time;
                if (!Charge(test, true))
                {
                    JobText.Toast(NotificationType.Info, "reallife_job_rental_again", 7f);
                }
                _logger?.Info("Jobs: a truck was hired for a heavy load ('" + TruckType + "', id " + _id + ") and stands " + JobSpots.Flat(near, spot).ToString("F0") + " m from the player.");
                reasonKey = null;
                return car;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Jobs: the game would not make a hired truck; no truck is hired out this session.");
                _truckFailed = true;
                try
                {
                    if (instance != null && made != null)
                    {
                        instance.Delete(made);
                    }
                    else if (instance != null && SaveGameManager.Current != null)
                    {
                        SaveGameManager.Current.VehicleInstances.Remove(instance);
                    }
                }
                catch (Exception again)
                {
                    _logger?.Error(again);
                }
                _car = null;
                _id = null;
                _truck = null;
                _heavy = false;
                return null;
            }
        }

        // The day's fee, once a day; for the truck, less what a van's hire has already paid that day. True if charged just now.
        private bool Charge(bool test, bool heavy)
        {
            int due = Due(heavy);
            if (test || due <= 0)
            {
                return false;
            }
            if (!PaidToday)
            {
                _paidAmount = 0;
            }
            _paidDay = TimeHelper.CurrentDay;
            _paidAmount += due;
            GameManager.ChangeMoneySafe(-due, new TransactionInfo(FeeTransaction, FeeCategory), null, null, force: true);
            JobText.Toast(NotificationType.Info, heavy ? "reallife_job_heavy_hired" : "reallife_job_rental_hired", "amount", JobText.Money(due), 9f);
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
                    // Wrecked and removed by the game, or left over from a save and not on the street yet.
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
                LentVehicle.KeepFree(_car.vehicleInstance, ref _tickets);   // a hired van is never the player's to be ticketed for
                if (_hooked && !_returning && !_heavy)
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
                // Never left without its own clean-up: taken back now if it can be, forgotten either way.
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

        public void Save(Dictionary<string, string> data)
        {
            if (data == null)
            {
                return;
            }
            BeforeSerialise();
            if (_paidDay <= 0 && _id == null)
            {
                data.Remove(Key);
                return;
            }
            // The truck's id is kept too: a copy that reached a save after all is found and taken back after the load.
            data[Key] = _paidDay.ToString(CultureInfo.InvariantCulture) + "|" + (_id ?? string.Empty) + "|" + (_heavy ? "1" : "0")
                        + "|" + _paidAmount.ToString(CultureInfo.InvariantCulture);
        }

        // The game serialises inside its save event: the truck comes off the vehicle list for it and back a frame later.
        private void BeforeSerialise()
        {
            List<VehicleInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.VehicleInstances : null;
            if (_truck == null || all == null || !all.Remove(_truck))
            {
                return;
            }
            if (_host != null && _host.isActiveAndEnabled)
            {
                _host.StartCoroutine(Guard.Routine(PutBack(_truck)));
            }
        }

        private IEnumerator PutBack(VehicleInstance instance)
        {
            yield return null;
            List<VehicleInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.VehicleInstances : null;
            if (ReferenceEquals(instance, _truck) && all != null && !all.Contains(instance))
            {
                all.Add(instance);
            }
        }

        /// <summary>After a load: the fee already paid today, and a van a saved hire left out (taken back by <see cref="Tick"/>).</summary>
        public void Load(Dictionary<string, string> data)
        {
            _car = null;
            _id = null;
            _paidDay = 0;
            _paidAmount = 0;
            _returning = false;
            _heavy = false;
            _truck = null;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] parts = text.Split('|');
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _paidDay);
            _heavy = parts.Length > 2 && parts[2] == "1";
            // A line from before the truck: what was paid that day was a van's fee.
            _paidAmount = parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int paid) ? paid : _paidDay > 0 ? DailyFee : 0;
            if (parts.Length > 1 && parts[1].Length > 0 && StillSaved(parts[1]))
            {
                _id = parts[1];
                _returning = true;   // no job is ever on after a load
                _hooked = true;      // the loaded van was built afresh, with the game's hourly check on it
                _hookTries = 0f;
                _logger?.Info("Jobs: a hired van from a saved shift is still out; it is taken back once the player is not in it.");
            }
        }

        private void TakeBack()
        {
            CarController car = _car;
            string id = _id;
            bool truck = _heavy;
            _car = null;
            _id = null;
            _returning = false;
            _heavy = false;
            _truck = null;
            if (car != null && truck && car.vehicleInstance != null)
            {
                // No clean-up of the game's on the truck: deleted the game's own way, as the driving school's car is.
                OverlayGuard.Release(car);
                car.vehicleInstance.Delete(car);
                _logger?.Info("Jobs: the hired truck (" + id + ") was taken back.");
                return;
            }
            if (car != null)
            {
                // As the game removes its job van; taken off the save's list here too, so a save in the same frame cannot carry it.
                VehicleInstance instance = car.vehicleInstance;
                UnityEngine.Object.Destroy(car.gameObject);
                if (instance != null && SaveGameManager.Current != null)
                {
                    SaveGameManager.Current.VehicleInstances.Remove(instance);
                }
                _logger?.Info("Jobs: the hired van (" + id + ") was taken back.");
            }
        }

        // The van's own hourly check, unhooked for the length of the hire.
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

        // A stretch of lane near the player with nothing in it: the kerb side first, then the lane itself.
        private bool FindSpot(Vector3 near, bool heavy, out Vector3 spot, out Quaternion heading)
        {
            spot = default;
            heading = Quaternion.identity;
            if (!TrafficTools.Street)
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
                    if (SceneParking.IsFree(at, along, heavy ? TruckHalfWidth : HalfWidth, heavy ? TruckHalfLength : HalfLength, 0.5f)
                        && JobSpots.Flat(at, near) > (heavy ? TruckFromPlayer : 5f))   // never on top of the player
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
