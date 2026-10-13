using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Player.HUD.ItemInfoOverlays;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
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
    ///
    /// Getting out of it always ends with the player on their feet beside it (CarExitRules). The
    /// game's own exit puts them at one point by the driver's door a frame later, and puts them back
    /// at the wheel with a warning when that point is not walkable; the school, which then sees them
    /// out again, would ask for ever and the player was held against the car's side until the game
    /// was restarted. So when the school sees them out it names a walkable point around the car that
    /// the game's exit takes instead. Should the exit be cut short however it began (a fault in the
    /// game's exit, a handler of its exit event, the player's own exit), the school finishes it once a
    /// second for half a minute after: out from under the car, shown, no longer held as in a vehicle,
    /// and on the navigation mesh beside it. The car is never taken back within two seconds of the
    /// player getting out, and the game's card over it is put away first, so nothing of the game's
    /// still points at a car that has gone.
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
        private bool _wasDriven;        // at its wheel at the last look
        private float _leftAt = -100f;  // when the player last got out of it (Time.time)
        private Vector3 _exitSpot;      // where the school last asked the game to put them
        private bool _haveSpot;
        private int _freed;             // exits the school has had to finish since the player last got out

        private static readonly FieldInfo TargetsField = typeof(EntityController).GetField("navMeshTargets", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BlockersField = typeof(PlayerController).GetField("_activeNavigationBlockers", BindingFlags.Instance | BindingFlags.NonPublic);

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
                _wasDriven = false;
                _leftAt = -100f;
                _haveSpot = false;
                _freed = 0;
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
                if (_car == null)
                {
                    return;
                }
                bool driven = Driven;
                if (_wasDriven && !driven)
                {
                    _leftAt = Time.time;   // out by whatever way: the game's exit, the player's, the school's
                    _freed = 0;
                    _haveSpot = false;     // (the school's own exit sets these itself, with the place it named)
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

        // The game's exit, told where to put the player: a walkable place around the car found while the car
        // still cuts no hole in the navigation mesh, written over the game's own door point after its exit has
        // checked that point and before the frame in which it puts the player there. A fault on the way is
        // finished at once if it left the player half out.
        private bool PutOut(CarController car)
        {
            bool found = false;
            Vector3 spot = default;
            try
            {
                found = ExitSpot(car, out spot);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _exitSpot = spot;
            _haveSpot = found;
            _leftAt = Time.time;
            _freed = 0;
            _wasDriven = false;
            try
            {
                car.ExitVehicle();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Licences: the game's exit from the school's car stopped half way (the error above); the school finishes it.");
                if (!Driven)
                {
                    Free(car, "the game's exit stopped half way");
                }
                return false;
            }
            if (found)
            {
                Aim(car, spot);
            }
            return true;
        }

        // The game's exit warps the player to its first door point one frame after it was called.
        private void Aim(CarController car, Vector3 spot)
        {
            try
            {
                if (TargetsField != null && TargetsField.GetValue(car) is Transform[] targets && targets.Length > 0 && targets[0] != null)
                {
                    targets[0].position = spot;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // A place to stand around the car that the player can walk on and walk off from: the driver's door
        // first (CarExitRules.Place), on the player's own navigation mesh, at the height of the street, out of
        // the car's body and of any other vehicle. Failing all six, the nearest pavement.
        private static bool ExitSpot(CarController car, out Vector3 spot)
        {
            spot = default;
            if (car == null)
            {
                return false;
            }
            JobSpots.CarFrame(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            NavMeshQueryFilter filter = PlayerController.navMeshQueryFilter;
            for (int i = 0; i < CarExitRules.Places; i++)
            {
                CarExitRules.Place(i, halfWidth, halfLength, out float across, out float along);
                Vector3 at = centre + right * across + forward * along;
                bool grounded = StreetSpots.Ground(at, out float ground);
                at.y = ground;
                if (!NavMesh.SamplePosition(at, out NavMeshHit hit, CarExitRules.SampleRadius, filter)
                    || (grounded && Mathf.Abs(hit.position.y - ground) > 0.45f))
                {
                    continue;
                }
                Vector3 local = hit.position - centre;
                if (CarExitRules.InBody(Vector3.Dot(local, right), Vector3.Dot(local, forward), halfWidth, halfLength))
                {
                    continue;
                }
                Vector3 clear = hit.position;
                if (!StreetSpots.OutOfVehicles(ref clear) || !NavMesh.SamplePosition(clear, out hit, 0.5f, filter))
                {
                    continue;
                }
                local = hit.position - centre;
                if (CarExitRules.InBody(Vector3.Dot(local, right), Vector3.Dot(local, forward), halfWidth, halfLength))
                {
                    continue;
                }
                spot = hit.position;
                return true;
            }
            if (StreetSpots.OnPavement(centre, 12f, out Vector3 pavement) && NavMesh.SamplePosition(pavement, out NavMeshHit onFoot, 1f, filter))
            {
                spot = onFoot.position;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- out, and free

        // Once a second for half a minute after the player got out, while they are near the car: a few comparisons,
        // and the exit finished only if something of it was left undone.
        private void WatchExit(Vector3 player)
        {
            float since = Time.time - _leftAt;
            if (since < CarExitRules.SettleSeconds || since > CarExitRules.WatchFor || _car == null)
            {
                return;
            }
            PlayerController pc = PlayerHelper.PlayerController;
            if (pc == null || pc.Character == null || PlayerHelper.IsUsingVehicle || HeldByOther(pc))
            {
                return;
            }
            JobSpots.CarFrame(_car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 local = player - centre;
            local.y = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            NavMeshAgent agent = pc.Character.navmeshAgent;
            bool underCar = pc.transform.IsChildOf(_car.transform);
            bool marked = HasBlocker(pc, NavigationBlocker.Vehicle);
            bool hidden = !pc.gameObject.activeInHierarchy || pc.Character.transform.localScale.sqrMagnitude < 0.01f;
            bool onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
            bool inBody = CarExitRules.InBody(Vector3.Dot(local, right), Vector3.Dot(local, forward), halfWidth, halfLength);
            if (!CarExitRules.Stuck(false, since, local.magnitude, underCar, marked, hidden, onMesh, inBody))
            {
                return;
            }
            string what = (underCar ? "still under the car, " : string.Empty) + (marked ? "still held as in a vehicle, " : string.Empty)
                          + (hidden ? "not shown, " : string.Empty) + (!onMesh ? "off the navigation mesh, " : string.Empty)
                          + (inBody ? "inside the car's body, " : string.Empty);
            Free(_car, what.TrimEnd(' ', ','));
        }

        // The rest of the game's exit (CarController.ExitVehicle, and PlayerController.Unstuck for what it does
        // after a failed exit), done by the school: shown, out from under the car, no longer held as in a vehicle,
        // the walking camera, and warped to a walkable place beside the car. Three times at most per exit.
        private void Free(CarController car, string why)
        {
            if (_freed >= 3)
            {
                return;
            }
            _freed++;
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                PlayerController pc = gm != null ? gm.playerController : null;
                if (pc == null || pc.Character == null || Driven)
                {
                    return;
                }
                bool underCar = car != null && pc.transform.IsChildOf(car.transform);
                bool hidden = !pc.gameObject.activeInHierarchy || pc.Character.transform.localScale.sqrMagnitude < 0.01f;
                if (underCar || pc.transform.parent != gm.transform)
                {
                    pc.transform.SetParent(gm.transform);
                }
                if (hidden)
                {
                    pc.Character.ToggleVisibility(show: true);
                }
                if (HasBlocker(pc, NavigationBlocker.Vehicle))
                {
                    pc.UnsetNavigationBlocker(NavigationBlocker.Vehicle);
                }
                if (underCar || hidden)
                {
                    CameraHelper.SetCamera(BuildingManager.IsInsideBuilding || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking ? gm.indoorCamera : gm.pedestrianCamera);
                }
                Vector3 spot = _exitSpot;
                bool found = _haveSpot && car != null && (spot - car.transform.position).sqrMagnitude < CarExitRules.StuckNear * CarExitRules.StuckNear;
                if (!found)
                {
                    found = ExitSpot(car, out spot);
                }
                NavMeshAgent agent = pc.Character.navmeshAgent;
                bool warped = false;
                if (agent != null && found)
                {
                    if (!agent.enabled)
                    {
                        agent.enabled = true;
                    }
                    warped = agent.Warp(spot);
                }
                pc.ResetNavigation();
                HideCard(car);
                _logger?.Info("Licences: out of the school's car, the player was " + why + "; the school finished the exit"
                              + (warped ? " and put them on their feet " + Vector3.Distance(spot, car != null ? car.transform.position : spot).ToString("F1") + " m from the car's middle."
                                  : ", but found no walkable place to put them (" + (found ? "the warp was refused" : "none within reach") + ")."));
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // Something else holds the player still (a sequence of the game's, the mod's custody, a fall): left to it.
        private static bool HeldByOther(PlayerController pc)
        {
            if (BlockersField == null || !(BlockersField.GetValue(pc) is HashSet<NavigationBlocker> set))
            {
                return false;
            }
            foreach (NavigationBlocker blocker in set)
            {
                if (blocker != NavigationBlocker.Vehicle)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasBlocker(PlayerController pc, NavigationBlocker blocker)
        {
            return BlockersField != null && BlockersField.GetValue(pc) is HashSet<NavigationBlocker> set && set.Contains(blocker);
        }

        private bool UnderCar()
        {
            PlayerController pc = PlayerHelper.PlayerController;
            return pc != null && _car != null && pc.transform.IsChildOf(_car.transform);
        }

        // The game's card over the car (shown while the cursor is on it, or after a click) and its vehicle panel are
        // put away before the car goes: the game hides the first only when the cursor leaves the car, and a card left
        // pointing at a deleted car throws in every frame (OverlayManager.LateUpdate) and keeps the player from walking.
        private static void HideCard(CarController car)
        {
            try
            {
                OverlayGuard.Release(car);
                var gm = InstanceBehavior<GameManager>.Instance;
                UIs hud = InstanceBehavior<UIs>.Instance;
                if (car != null && gm != null && ReferenceEquals(gm.selectedVehicle, car) && !PlayerHelper.IsUsingVehicle
                    && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                {
                    hud.playerHUD.itemPanelUI.Toggle(false);
                }
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
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
                Free(car, "still under the car as it was taken back");   // (deleting the car would take the player with it)
            }
            HideCard(car);
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
            _wasDriven = false;
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
