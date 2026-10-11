using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The driving school's car: one of the game's own vehicles, made the way the game makes
    /// one for the player (<c>VehicleHelper.CreateAndSpawnVehicle</c>), lent for a lesson or a
    /// test and taken back afterwards with the game's own delete. Once it is due back it is not
    /// the player's to drive: they are seen out of it when it has stopped (after twelve seconds
    /// at the latest), seen out again if they get back in, and it goes twenty seconds after
    /// they left it, sooner when they have walked off or look away.
    ///
    /// It is never in a savegame. The game serialises the save in the same call that raises its
    /// save event, so on that event the school's car is taken off the save's vehicle list and
    /// put back a frame later (<see cref="BeforeSave"/>): a save made in the middle of a test
    /// holds no trace of it, with or without the mod. Should the player be at its wheel in that
    /// save, the game finds no vehicle of that id on load, says so in its log and puts them on
    /// their feet (GameManager.SpawnPlayerVehicles). As a last line its id is also remembered in
    /// modData and anything of that id found after a load is deleted.
    ///
    /// It is an instance of the game's job-vehicle class (unsellable, not assignable to a
    /// warehouse), which also keeps the mod's own jobs from counting it as the player's car.
    /// </summary>
    public sealed class TestCar
    {
        public const string Key = "reallife:licence_car";
        private const string CarType = "ba:vehicletype_honzamimic";
        private const string VanType = "ba:vehicletype_mersaididash";
        private const string VanFallback = "ba:vehicletype_umcdesert";
        private const float HalfWidth = 1.05f;
        private const float HalfLength = 2.7f;

        private readonly IModLogger _logger;
        private readonly MonoBehaviour _host;
        private readonly List<Waypoint> _route = new List<Waypoint>(24);
        private CarController _car;
        private VehicleInstance _instance;
        private string _leftover;       // an id from modData that should not exist: deleted if it does
        private bool _returning;
        private float _returnFrom;
        private bool _wasLeft;          // the player has been out of it since it was due back
        private bool _seeingOut;
        private bool _failed;
        private int _tickets;           // parking tickets the game has written on the lent car, as last seen (LentVehicle)

        public TestCar(MonoBehaviour host, IModLogger logger)
        {
            _host = host;
            _logger = logger;
        }

        /// <summary>The car while a lesson or test has it, or null.</summary>
        public CarController Car => !_returning && _car != null ? _car : null;

        public bool Out => _car != null;

        /// <summary>When it was last put on the road (Time.time).</summary>
        public float SpawnedAt { get; private set; } = -100f;

        /// <summary>True while the player sits in it.</summary>
        public bool Driven => _instance != null && SaveGameManager.Current != null && SaveGameManager.Current.ActiveVehicleId == _instance.id;

        /// <summary>
        /// Puts the school's car (a small hatchback, or a panel van for the commercial class) at
        /// the roadside near a point. Null, with nothing left behind, if there is no free stretch
        /// of road near or the game would not make one.
        /// </summary>
        public CarController Lend(Vector3 near, bool van)
        {
            if (_failed || _car != null || !FindSpot(near, out Vector3 spot, out Quaternion heading))
            {
                return null;
            }
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
                SpawnedAt = Time.time;
                _logger?.Info("Licences: the school's " + (van ? "van" : "car") + " ('" + type + "', id " + instance.id + ") stands "
                              + Vector3.Distance(near, spot).ToString("F0") + " m from the desk.");
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

        /// <summary>The lesson or test is over: the player is seen out of the car, and it goes back once they have walked off or looked away (or twenty seconds have passed).</summary>
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

        /// <summary>Taken back this instant (it was never driven: the test could not start after all).</summary>
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
                LentVehicle.KeepFree(_instance, ref _tickets);   // the school's car is never the player's to be ticketed for
                if ((object)_car != null && _car == null)
                {
                    Forget();   // wrecked and removed by the game
                }
                if (_car == null || !_returning)
                {
                    return;
                }
                if (Driven)
                {
                    // Still at its wheel, or back at it: seen out. The time it may stand counts from when they are out.
                    _returnFrom = Time.time;
                    if (!_seeingOut && _host != null && _host.isActiveAndEnabled)
                    {
                        if (_wasLeft)
                        {
                            Licences.Toast(NotificationType.Warning, "reallife_lic_car_not_yours", 8f);
                        }
                        _seeingOut = true;
                        _host.StartCoroutine(SeeOut(_car));
                    }
                    return;
                }
                _wasLeft = true;
                if (BuildingManager.IsInsideBuilding)
                {
                    return;
                }
                float away = (_car.transform.position - player).sqrMagnitude;
                if (away > 30f * 30f || Time.time - _returnFrom > 20f || (away > 12f * 12f && !StreetSpots.IsInView(_car.transform.position)))
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

        // Out of the school's car with the game's own exit: when it has stopped, or after twelve seconds if they drive on
        // (the exit halts it where it is).
        private IEnumerator SeeOut(CarController car)
        {
            float waited = 0f;
            while (Held(car) && Mathf.Abs(car.CurrentSpeed) > 1f && waited < 12f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Held(car) && PutOut(car))
            {
                yield return new WaitForSecondsRealtime(0.5f);
                ClosePanel();
            }
            _seeingOut = false;
        }

        private bool Held(CarController car)
        {
            return car != null && ReferenceEquals(car, _car) && _returning && Driven;
        }

        private bool PutOut(CarController car)
        {
            try
            {
                car.ExitVehicle();
                return true;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                return false;
            }
        }

        // The game clears the selected vehicle only when its panel closes (as in Law/Custody).
        private static void ClosePanel()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            UIs hud = InstanceBehavior<UIs>.Instance;
            if (gm != null && gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
            {
                hud.playerHUD.itemPanelUI.Toggle(false);
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

        // ---------------------------------------------------------------- the save

        /// <summary>
        /// On the game's save event: the school's car comes off the save's vehicle list for the
        /// length of the serialisation and goes back on a frame later. Its id is written to
        /// modData so that a copy that reached a save by any other road is deleted after a load.
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
                _host.StartCoroutine(PutBack(_instance));
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

        // A school car that is in a loaded save after all: deleted the game's own way once the player is not in it.
        private void DeleteLeftover()
        {
            if (SaveGameManager.Current == null)
            {
                return;
            }
            string id = _leftover;
            if (SaveGameManager.Current.ActiveVehicleId == id)
            {
                return;   // at its wheel: once they are out
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
                    all[i].Delete(controller);
                    _logger?.Info("Licences: a school car left in the save (" + id + ") was removed.");
                }
            }
        }

        private void TakeBack()
        {
            CarController car = _car;
            VehicleInstance instance = _instance;
            Forget();
            if (instance != null)
            {
                instance.Delete(car);   // the game's own way of removing a vehicle of the player's
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
        }

        // A stretch of lane near the desk with nothing standing in it: the kerb side first, then the lane itself.
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
                if (point == null || point.enter || point.exit || point.temporaryDisabled)
                {
                    continue;
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
                    Vector3 flat = at - near;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 25f && SceneParking.IsFree(at, along, HalfWidth, HalfLength, 0.5f))
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
