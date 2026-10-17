using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The driving school's car: one of the game's job vehicles, lent for a lesson or test, set out only where a route
    /// can start, and deleted the game's way afterwards. It is never in a save.
    /// </summary>
    public sealed partial class TestCar
    {
        public const string Key = "reallife:licence_car";
        private const string CarType = "ba:vehicletype_honzamimic";
        private const string VanType = "ba:vehicletype_mersaididash";
        private const string VanFallback = "ba:vehicletype_umcdesert";
        private const float HalfWidth = 1.05f;   // metres
        private const float HalfLength = 2.7f;
        private const float NearReach = 70f;     // metres of lane from the desk the car is looked for along first...
        private const float FarReach = 160f;     // ...then this far, past a stretch an accident has closed
        private const int WalkPoints = 96;       // lane points walked along each lane at most
        private const int MostPlans = 8;         // routes planned at most in one look, each well under a millisecond
        private const int MostRooms = 48;        // places looked at for room at most in one look: a ground ray and an overlap test each

        private readonly IModLogger _logger;
        private readonly MonoBehaviour _host;
        private readonly List<Waypoint> _route = new List<Waypoint>(64);
        private readonly List<Waypoint> _routeOther = new List<Waypoint>(64);   // the lane running the other way
        private CarController _car;
        private VehicleInstance _instance;
        private string _leftover;       // an id from modData that should not exist: deleted if it does
        private bool _returning;
        private float _returnFrom;
        private bool _wasLeft;          // the player has been out of it since it was due back
        private bool _seeingOut;
        private bool _failed;
        private int _tickets;           // parking tickets the game has written on it, as last seen

        public TestCar(MonoBehaviour host, IModLogger logger)
        {
            _host = host;
            _logger = logger;
        }

        public bool Out => _car != null;

        /// <summary>When it was last put on the road (Time.time).</summary>
        public float SpawnedAt { get; private set; } = -100f;

        /// <summary>True while the player sits in it.</summary>
        public bool Driven => _instance != null && SaveGameManager.Current != null && SaveGameManager.Current.ActiveVehicleId == _instance.id;

        /// <summary>
        /// Puts the car, or a van for the commercial class, at the roadside near a point where <paramref name="route"/>
        /// plans a drive from. Null, with nothing left behind and a locale key why, if it cannot.
        /// </summary>
        public CarController Lend(Vector3 near, bool van, Func<Waypoint, bool, RouteOutcome> route, out string refusal)
        {
            refusal = TestRouteRules.NoSpotKey;
            if (_failed || _car != null)
            {
                return null;
            }
            refusal = FindSpot(near, route, out Vector3 spot, out Quaternion heading);
            if (refusal != null)
            {
                return null;
            }
            refusal = TestRouteRules.NoSpotKey;
            VehicleController made = null;
            VehicleInstance instance = null;
            try
            {
                string type = van ? VanType : CarType;
                VehicleType known = VehicleTypeHelper.GetVehicleType(type);
                if (known == null && van)
                {
                    type = VanFallback;
                    known = VehicleTypeHelper.GetVehicleType(type);
                }
                if (known == null)
                {
                    _logger?.Warn("Licences: the game has no vehicle type '" + type + "'; the school has no car to lend this session.");
                    _failed = true;
                    return null;
                }
                instance = new DeliveryVehicleInstance(type)
                {
                    fuel = known.maxFuel,
                    zeroSellingPrice = true,
                    preventWarehouseAssign = true
                };
                made = VehicleHelper.CreateAndSpawnVehicle(instance, spot, heading);
                var car = made as CarController;
                if (car == null)
                {
                    instance.Delete(made);
                    _failed = true;
                    return null;
                }
                _car = car;
                _instance = instance;
                _tickets = 0;
                _returning = false;
                _wasDriven = false;
                _leftAt = -100f;
                _haveSpot = false;
                _freed = 0;
                SpawnedAt = Time.time;
                _logger?.Info("Licences: the school's " + (van ? "van" : "car") + " ('" + type + "', id " + instance.id + ") stands "
                              + Vector3.Distance(near, spot).ToString("F0") + " m from the desk.");
                refusal = null;
                return car;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Licences: the game would not make the school's car; no lessons or tests this session.");
                _failed = true;
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
                _instance = null;
                return null;
            }
        }

        /// <summary>The lesson or test is over: the player is seen out, and the car goes once they have walked off or looked away.</summary>
        public void Return()
        {
            if (_car != null && !_returning)
            {
                _returning = true;
                _returnFrom = Time.time;
                _wasLeft = !Driven;
                if (Driven)
                {
                    Licences.Toast(NotificationType.Info, "reallife_lic_car_step_out", 8f);
                }
            }
        }

        /// <summary>Taken back at once: it was never driven.</summary>
        public void Recall()
        {
            try
            {
                if (_car != null && !Driven)
                {
                    TakeBack();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                Forget();
            }
        }

        /// <summary>Once a second, test or no test. A comparison when there is no car.</summary>
        public void Tick(Vector3 player)
        {
            try
            {
                if (_leftover != null)
                {
                    DeleteLeftover();
                }
                LentVehicle.KeepFree(_instance, ref _tickets);   // never the player's to be ticketed for
                if ((object)_car != null && _car == null)
                {
                    Forget();   // wrecked and removed by the game
                }
                if (_car == null)
                {
                    return;
                }
                bool driven = Driven;
                if (_wasDriven && !driven)
                {
                    _leftAt = Time.time;   // out by whatever way; the school's own exit sets these itself
                    _freed = 0;
                    _haveSpot = false;
                }
                _wasDriven = driven;
                if (!driven)
                {
                    WatchExit(player);
                }
                if (!_returning)
                {
                    return;
                }
                if (driven)
                {
                    // Still or again at its wheel: seen out. The time it may stand counts from when they are out.
                    _returnFrom = Time.time;
                    if (!_seeingOut && _host != null && _host.isActiveAndEnabled)
                    {
                        if (_wasLeft)
                        {
                            Licences.Toast(NotificationType.Warning, "reallife_lic_car_not_yours", 8f);
                        }
                        _seeingOut = true;
                        _host.StartCoroutine(Guard.Routine(SeeOut(_car)));
                    }
                    return;
                }
                _wasLeft = true;
                if (BuildingManager.IsInsideBuilding)
                {
                    return;
                }
                float away = (_car.transform.position - player).sqrMagnitude;
                bool due = away > 30f * 30f || Time.time - _returnFrom > 20f || (away > 12f * 12f && !StreetSpots.IsInView(_car.transform.position));
                if (due && CarExitRules.MayTakeBack(false, Time.time - _leftAt, UnderCar()))
                {
                    TakeBack();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                try
                {
                    if (!Driven)
                    {
                        TakeBack();
                    }
                }
                catch (Exception again)
                {
                    _logger?.Error(again);
                    Forget();
                }
            }
        }

        /// <summary>The city is going or the feature is off: an idle car is taken back now.</summary>
        public void Dispose()
        {
            try
            {
                if (_car != null && !Driven && !GameManager.isCitySceneBeingUnloaded)
                {
                    TakeBack();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Forget();
        }

        /// <summary>
        /// The game serialises inside its save event, so the car comes off the vehicle list for it and back a frame later.
        /// Its id goes into modData, so a copy that reached a save anyway is deleted after a load.
        /// </summary>
        public void BeforeSave(Dictionary<string, string> data)
        {
            if (data == null)
            {
                return;
            }
            if (_instance == null || SaveGameManager.Current == null)
            {
                data.Remove(Key);
                return;
            }
            data[Key] = _instance.id;
            List<VehicleInstance> all = SaveGameManager.Current.VehicleInstances;
            if (all != null && all.Remove(_instance) && _host != null && _host.isActiveAndEnabled)
            {
                _host.StartCoroutine(Guard.Routine(PutBack(_instance)));
            }
        }

        private IEnumerator PutBack(VehicleInstance instance)
        {
            yield return null;
            if (ReferenceEquals(instance, _instance) && SaveGameManager.Current != null && SaveGameManager.Current.VehicleInstances != null
                && !SaveGameManager.Current.VehicleInstances.Contains(instance))
            {
                SaveGameManager.Current.VehicleInstances.Add(instance);
            }
        }

        public void Load(Dictionary<string, string> data)
        {
            Forget();
            _leftover = data != null && data.TryGetValue(Key, out string id) && !string.IsNullOrEmpty(id) ? id : null;
        }

        // A school car in a loaded save after all: deleted the game's way once the player is not in it.
        private void DeleteLeftover()
        {
            if (SaveGameManager.Current == null)
            {
                return;
            }
            string id = _leftover;
            if (SaveGameManager.Current.ActiveVehicleId == id)
            {
                return;   // at its wheel: tried again once they are out
            }
            _leftover = null;
            List<VehicleInstance> all = SaveGameManager.Current.VehicleInstances;
            for (int i = all != null ? all.Count - 1 : -1; i >= 0; i--)
            {
                if (all[i] != null && all[i].id == id && all[i] is DeliveryVehicleInstance)
                {
                    VehicleController controller = null;
                    List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
                    for (int v = 0; v < own.Count; v++)
                    {
                        if (own[v] != null && own[v].vehicleInstance != null && own[v].vehicleInstance.id == id)
                        {
                            controller = own[v];
                        }
                    }
                    OverlayGuard.Release(controller);
                    all[i].Delete(controller);
                    _logger?.Info("Licences: a school car left in the save (" + id + ") was removed.");
                }
            }
        }

        private void TakeBack()
        {
            CarController car = _car;
            VehicleInstance instance = _instance;
            if (car != null && UnderCar())
            {
                Free(car, "still under the car as it was taken back");   // deleting the car would take the player with it
            }
            HideCard(car);
            Forget();
            if (instance != null)
            {
                instance.Delete(car);
                _logger?.Info("Licences: the school's car (" + instance.id + ") was taken back.");
            }
        }

        private void Forget()
        {
            _car = null;
            _instance = null;
            _returning = false;
            _wasLeft = false;
            _seeingOut = false;
            _wasDriven = false;
        }

        /// <summary>Whether the car could be set out near a point now, without making it. Null if so, else the reason.</summary>
        public string Placeable(Vector3 near, Func<Waypoint, bool, RouteOutcome> route)
        {
            return _failed ? TestRouteRules.NoSpotKey : FindSpot(near, route, out _, out _);
        }

        // A place by a lane near the desk that a drive can be planned from: the nearest lane, then the other way (a road
        // closed on one side may be open on the other), near first. Null when found, else why not. A few ms at worst.
        private string FindSpot(Vector3 near, Func<Waypoint, bool, RouteOutcome> route, out Vector3 spot, out Quaternion heading)
        {
            spot = default;
            heading = Quaternion.identity;
            if (!TrafficTools.Ready)
            {
                return TestRouteRules.NoSpotKey;
            }
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(near, Vector3.zero, 45f);
            if (anchor == null)
            {
                return TestRouteRules.NoSpotKey;
            }
            TrafficTools.WalkForward(anchor, FarReach, WalkPoints, _route);
            _routeOther.Clear();
            if (_route.Count > 1)
            {
                Vector3 runs = _route[1].position - anchor.position;
                runs.y = 0f;
                Waypoint other = runs.sqrMagnitude > 0.01f ? TrafficTools.ClosestWaypointAlong(near, -runs.normalized * 10f, 45f) : null;
                if (other != null && !OnWalk(_route, other))
                {
                    TrafficTools.WalkForward(other, FarReach, WalkPoints, _routeOther);
                }
            }
            bool closedSeen = false, roomSeen = false, asked = false;
            int plans = 0, rooms = 0;
            for (int pass = 0; pass < 2 && plans < MostPlans && rooms < MostRooms; pass++)
            {
                float from = pass == 0 ? 0f : NearReach;
                float to = pass == 0 ? NearReach : FarReach;
                for (int lane = 0; lane < 2 && plans < MostPlans && rooms < MostRooms; lane++)
                {
                    List<Waypoint> walk = lane == 0 ? _route : _routeOther;
                    float along = 0f;
                    for (int i = 0; i + 1 < walk.Count && plans < MostPlans && rooms < MostRooms; i++)
                    {
                        if (i > 0)
                        {
                            along += Vector3.Distance(walk[i - 1].position, walk[i].position);
                        }
                        Waypoint point = walk[i];
                        if ((pass == 0 ? along > to : along <= from || along > to) || point == null || point.enter || point.exit)
                        {
                            continue;
                        }
                        if (point.temporaryDisabled)
                        {
                            closedSeen = true;
                            continue;
                        }
                        rooms++;
                        if (!Room(near, point, walk[i + 1], out Vector3 at, out Quaternion facing))
                        {
                            continue;
                        }
                        roomSeen = true;
                        plans++;
                        // Whether a closure is why there is no route is asked once, of the first place with room.
                        RouteOutcome outcome = route(point, !asked && !closedSeen);
                        if (outcome == RouteOutcome.Planned)
                        {
                            spot = at;
                            heading = facing;
                            return null;
                        }
                        asked = true;
                        closedSeen |= outcome == RouteOutcome.Closed;
                    }
                }
            }
            return TestRouteRules.Refusal(false, closedSeen, roomSeen);
        }

        // The car's place at one lane point, kerb side first: on the ground, clear of the desk, with nothing standing there.
        private static bool Room(Vector3 near, Waypoint point, Waypoint next, out Vector3 spot, out Quaternion heading)
        {
            spot = default;
            heading = Quaternion.identity;
            if (next == null)
            {
                return false;
            }
            Vector3 runs = next.position - point.position;
            runs.y = 0f;
            if (runs.sqrMagnitude < 0.25f)
            {
                return false;
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
                Vector3 flat = at - near;
                flat.y = 0f;
                if (flat.sqrMagnitude > 25f && SceneParking.IsFree(at, along, HalfWidth, HalfLength, 0.5f))
                {
                    spot = at;
                    heading = along;
                    return true;
                }
            }
            return false;
        }

        private static bool OnWalk(List<Waypoint> walk, Waypoint point)
        {
            for (int i = 0; i < walk.Count; i++)
            {
                if (ReferenceEquals(walk[i], point))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
