using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Tags;
using Extensions;
using Helpers;
using UI.Load;
using Localizor;
using NWH.VehiclePhysics2.Modules.Fuel;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Owning a car, beyond what the game does (see <see cref="CarRules"/> for what that is and
    /// for every figure). For each car in the save's own list of the player's vehicles a
    /// <see cref="CarRecord"/> is kept in modData. While one of them is driven:
    ///
    /// - the distance is counted, and with it the service comes due and tyres and brakes wear;
    /// - worn tyres and brakes are felt, mildly, through the wheels' own grip figures and the
    ///   brakes' own torque, each recorded before it is touched and put back exactly on getting
    ///   out, changing car, switching the option off and unloading;
    /// - fuel is used at the rate of the car's kind (the fuel system's own multiplier, put back
    ///   the same way), and shown as a gauge beside the speedometer with a word under it for
    ///   whatever needs attention;
    /// - a neglected service can end in a breakdown: the engine no longer pulls (the pedal is
    ///   held at nothing through the car's own input hook), the hazard lights go on, and the
    ///   car coasts to a stop until roadside assistance, a tow or a garage has seen to it;
    /// - standing in a station's repair bay, one key has everything that is due done;
    /// - a repair of crash damage is noticed and, for an insured car, most of the bill refunded.
    ///
    /// Once a game day: premiums are taken, old claims forgotten, registrations reminded of.
    /// The budget: a timer twice a second while a car is driven, once a second otherwise (a few comparisons);
    /// per frame only a key test, and only while a prompt is on screen. A fault puts the car
    /// back as the game made it and switches the feature off for the session.
    /// </summary>
    public sealed class CarOwnership : MonoBehaviour
    {
        private const float DriveTick = 0.5f;
        private const float IdleTick = 1f;
        private const float LowFuel = 0.15f;
        private const float StoppedSpeed = 0.5f;   // m/s

        public static IModLogger Logger;
        public static CarOwnership Instance { get; private set; }

        /// <summary>True while one of this feature's key prompts is on screen.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._promptOn || Instance._keyFrame == Time.frameCount);

        private readonly List<float> _stockSide = new List<float>(6);
        private readonly List<float> _stockAlong = new List<float>(6);
        private readonly StringBuilder _text = new StringBuilder(256);
        private CarRoadside _roadside;
        private GasStationController[] _stations;
        private CarController _car;
        private NwhVehicle _vehicle;
        private Rigidbody _body;
        private CarRecord _record;
        private CarKind _kind;
        private float _price;
        private float _nextTick;
        private float _enteredAt;
        private float _stockBrake;
        private float _grip = 1f;
        private float _brake = 1f;
        private bool _wearOn;
        private FuelModule _fuel;
        private float _stockThirst;
        private bool _stockUseFuel;
        private bool _fuelOn;
        private UnityAction _cut;
        private bool _cutting;
        private bool _hazards;
        private bool _lowFuelTold;
        private bool _promptOn;
        private int _promptKind;        // 1 the garage, 2 a breakdown, 3 out of fuel
        private int _panelKey = int.MinValue;
        private int _keyFrame = -1;
        private int _lastDay = -1;
        private float _serviceTotal;
        private int _serviceItems;
        private bool _servicing;
        private bool _faded;
        private bool _failed;
        private bool _loaded;
        private string _noteService, _noteTyres, _noteBrakes, _noteBroken;

        public CarRoadside Roadside => _roadside;

        private void Awake()
        {
            Instance = this;
            CarGarage.Reset();
            _roadside = new CarRoadside(this);
            _cut = CutPedal;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                Leave();
                _roadside.Dispose();
                if (_faded && isActiveAndEnabled)
                {
                    StartCoroutine(UiFader.UnFade());
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            CarGarage.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save != null && save.modData != null)
                {
                    CarGarage.Save(save.modData, save.VehicleInstances, save.privateDriverVehicleInstances);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Update()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (_promptOn && JobKeys.InteractDown())
                {
                    _keyFrame = Time.frameCount;
                    Pressed();
                }
                float now = Time.time;
                if (now < _nextTick)
                {
                    return;
                }
                Tick(now);
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Car ownership: switched off for this session after the error above; the car is as the game made it.");
                try
                {
                    Leave();
                    _roadside.Cancel();
                }
                catch (Exception again)
                {
                    Logger?.Error(again);
                }
            }
        }

        private void Tick(float now)
        {
            var game = InstanceBehavior<GameManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            _nextTick = now + IdleTick;
            if (game == null || game.playerController == null || save == null || !ModState.Loaded || LoadScene.isLoading)
            {
                return;
            }
            if (!_loaded)
            {
                _loaded = true;
                CarGarage.Load(save.modData);
                int gone = CarGarage.Prune(save.VehicleInstances, save.privateDriverVehicleInstances);
                CarPhone.Setup();
                Logger?.Info("Car ownership: records read" + (gone > 0 ? "; " + gone + " dropped for cars the save no longer has" : "") + ".");
            }
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

            if (!Settings.CarOwnership)
            {
                Leave();
                _roadside.Cancel();
                return;
            }
            if (TimeHelper.CurrentDay != _lastDay)
            {
                bool first = _lastDay < 0;
                _lastDay = TimeHelper.CurrentDay;
                try
                {
                    NewDay(save, first);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);   // a car of a kind the game no longer knows: the round is skipped for today, the feature goes on
                }
            }
            if (_roadside.Active)
            {
                _nextTick = now + DriveTick;
                _roadside.Tick(now);
            }
            var car = game.selectedVehicle as CarController;
            if (car != null && (car.vehicleController == null || car.vehicleType == null || !car.vehicleType.IsMotorVehicle))
            {
                car = null;
            }
            if (car != _car)
            {
                Leave();
                if (car != null)
                {
                    Enter(car, save, now);
                }
            }
            if (_car == null)
            {
                return;
            }
            _nextTick = now + DriveTick;
            Drive(now, save);
        }

        // ---------------------------------------------------------------- getting in and out

        private void Enter(CarController car, GameInstance save, float now)
        {
            _car = car;
            _vehicle = car.vehicleController;
            _body = _vehicle.vehicleRigidbody != null ? _vehicle.vehicleRigidbody : car.GetComponent<Rigidbody>();
            _fuel = car.fuelModule;
            _price = car.vehicleType.price;
            var traits = new CarTraits(true, car.vehicleType.spawnInPlayerObject, car.vehicleType.maxSpeed, car.vehicleType.maxCargoCapacity, car.vehicleType.isLuxuryCar,
                _body != null ? _body.mass : 1400f);
            _kind = CarCatalogue.For(car.vehicleType.vehicleTypeName, traits).Kind;
            bool owned = car.vehicleInstance != null && save.VehicleInstances != null && save.VehicleInstances.Contains(car.vehicleInstance);
            _record = owned ? CarGarage.For(car.vehicleInstance.id, TimeHelper.CurrentDay) : null;   // a hired or borrowed car has no record
            if (_record != null)
            {
                _record.LastDamage = car.vehicleInstance.damage;
            }
            _enteredAt = now;
            _lowFuelTold = false;
            _panelKey = int.MinValue;
        }

        /// <summary>Everything this put on the driven car comes off, exactly. Safe to call with no car.</summary>
        private void Leave()
        {
            NwhVehicle vehicle = _vehicle;
            try
            {
                if (vehicle != null)
                {
                    if (_cutting)
                    {
                        vehicle.input.inputModifyCallback.RemoveListener(_cut);
                    }
                    if (_hazards)
                    {
                        vehicle.input.states.hazardLights = true;   // the lights' own toggle: on once, off once
                    }
                    if (_wearOn)
                    {
                        for (int i = 0; i < vehicle.powertrain.wheels.Count && i < _stockSide.Count; i++)
                        {
                            NWH.Common.Vehicles.WheelUAPI wheel = vehicle.powertrain.wheels[i].wheelUAPI;
                            if (wheel != null)
                            {
                                wheel.LateralFrictionGrip = _stockSide[i];
                                wheel.LongitudinalFrictionGrip = _stockAlong[i];
                            }
                        }
                        vehicle.brakes.maxTorque = _stockBrake;
                    }
                }
                if (_fuelOn && _fuel != null)
                {
                    _fuel.consumptionMultiplier = _stockThirst;
                    _fuel.useFuel = _stockUseFuel;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Car ownership: a car could not be put back completely. " + e);
            }
            _cutting = false;
            _hazards = false;
            _wearOn = false;
            _fuelOn = false;
            _grip = 1f;
            _brake = 1f;
            _stockSide.Clear();
            _stockAlong.Clear();
            _car = null;
            _vehicle = null;
            _body = null;
            _fuel = null;
            _record = null;
            HidePrompt();
            if (TransportHud.Exists)
            {
                TransportHud.Instance.HideFuel();
                if (_panelKey != int.MinValue)
                {
                    TransportHud.Instance.HidePanel();
                }
            }
            _panelKey = int.MinValue;
        }

        // ---------------------------------------------------------------- at the wheel, twice a second

        private void Drive(float now, GameInstance save)
        {
            bool easy = Settings.CarEasy;
            Vector3 velocity = _body != null ? _body.velocity : Vector3.zero;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            float km = speed * DriveTick / 1000f;
            float damage = _car.vehicleInstance != null ? _car.vehicleInstance.damage : 0f;

            if (_record != null)
            {
                _record.Km += km;
                if (!easy)
                {
                    // A build from the custom shop wears its tyres and brakes faster (Scripts/Garage: 0 for a stock car).
                    float worn = km * (1f + (_car.vehicleInstance != null ? GarageBridges.WearOf(_car.vehicleInstance.id) : 0f));
                    _record.Tyres = Mathf.Max(0f, _record.Tyres - worn / CarRules.TyreLife(_kind));
                    _record.Brakes = Mathf.Max(0f, _record.Brakes - worn / CarRules.BrakeLife(_kind));
                }
                // A repair (the game's own, at a station's garage) shows as the damage figure dropping.
                if (_record.LastDamage >= 0f && damage < _record.LastDamage - 0.03f)
                {
                    Repaired((_record.LastDamage - damage) * _price * 0.5f);
                }
                _record.LastDamage = damage;

                if (!easy && !_record.Broken && speed > 4f && now - _enteredAt > 30f && _record.Km > _record.GraceKm && km > 0f
                    && UnityEngine.Random.value < CarRules.BreakdownPerKm(_record.Km - _record.ServicedKm, CarRules.ServiceEvery(_kind), damage) * km)
                {
                    BreakDown();
                }
            }

            // One tick after getting in, so that the car's own tuning has read the car as the game made it first.
            if (now - _enteredAt >= DriveTick)
            {
                ApplyWear(easy);
            }

            bool broken = _record != null && _record.Broken && !easy;
            if (broken != _cutting)
            {
                _cutting = broken;
                if (broken)
                {
                    _vehicle.input.inputModifyCallback.AddListener(_cut);
                }
                else
                {
                    _vehicle.input.inputModifyCallback.RemoveListener(_cut);
                }
            }
            if (broken != _hazards)
            {
                _hazards = broken;
                _vehicle.input.states.hazardLights = true;   // toggles
            }

            // The gauge.
            bool fuelled = _fuel != null && _fuel.capacity > 0f && !save.gameVariables.disableVehicleFuel;
            float level = fuelled ? Mathf.Clamp01(_fuel.amount / _fuel.capacity) : 1f;
            if (fuelled && !ThirdPersonCamera.IsUiOpen())
            {
                TransportHud.Instance.ShowFuel(Mathf.CeilToInt(level * 100f), Note(easy));
            }
            else if (TransportHud.Exists)
            {
                TransportHud.Instance.HideFuel();
            }
            if (fuelled && !easy && !_lowFuelTold && level <= LowFuel && level > 0f)
            {
                _lowFuelTold = true;
                int metres = NearestStation(_car.transform.position);
                Notifications.Show(NotificationType.Warning, metres > 0 ? "reallife_car_low_fuel_station" : "reallife_car_low_fuel",
                    new Dictionary<string, string>(2) { { "percent", Mathf.CeilToInt(level * 100f).ToString() }, { "metres", metres.ToString() } }, 8f);
            }
            else if (level > LowFuel + 0.1f)
            {
                _lowFuelTold = false;
            }

            // What the key would do, standing still: the garage, a breakdown, an empty tank.
            bool still = speed < StoppedSpeed && !_servicing && !ThirdPersonCamera.IsUiOpen() && !JobDirector.TakesInteractKey && !TransitDirector.TakesInteractKey;
            if (still && _record != null && InBay())
            {
                ShowGarage(save);
            }
            else
            {
                if (_panelKey != int.MinValue)
                {
                    _panelKey = int.MinValue;
                    TransportHud.Instance.HidePanel();
                }
                bool dry = fuelled && !easy && level <= 0.005f;
                if (still && !_roadside.Active && _record != null && (broken || dry))
                {
                    Prompt(broken ? 2 : 3, TransitText.T(broken ? "reallife_car_prompt_breakdown" : "reallife_car_prompt_fuel",
                        "amount", TransitText.Money(CarRoadside.Fee(broken ? RoadsideJob.Breakdown : RoadsideJob.Fuel, _record))));
                }
                else
                {
                    HidePrompt();
                }
            }
        }

        // The wheels' grip and the brakes' torque for the wear there is, and the fuel system for the kind of car and the easy option.
        private void ApplyWear(bool easy)
        {
            float grip = easy || _record == null ? 1f : CarRules.Grip(_record.Tyres);
            float brake = easy || _record == null ? 1f : CarRules.BrakeForce(_record.Brakes);
            if (!_wearOn && (grip < 1f || brake < 1f))
            {
                _stockSide.Clear();
                _stockAlong.Clear();
                for (int i = 0; i < _vehicle.powertrain.wheels.Count; i++)
                {
                    NWH.Common.Vehicles.WheelUAPI wheel = _vehicle.powertrain.wheels[i].wheelUAPI;
                    _stockSide.Add(wheel != null ? wheel.LateralFrictionGrip : 1f);
                    _stockAlong.Add(wheel != null ? wheel.LongitudinalFrictionGrip : 1f);
                }
                _stockBrake = _vehicle.brakes.maxTorque;
                _wearOn = true;
                _grip = 1f;
                _brake = 1f;
            }
            if (_wearOn && (Mathf.Abs(grip - _grip) > 0.004f || Mathf.Abs(brake - _brake) > 0.004f))
            {
                _grip = grip;
                _brake = brake;
                for (int i = 0; i < _vehicle.powertrain.wheels.Count && i < _stockSide.Count; i++)
                {
                    NWH.Common.Vehicles.WheelUAPI wheel = _vehicle.powertrain.wheels[i].wheelUAPI;
                    if (wheel != null)
                    {
                        wheel.LateralFrictionGrip = _stockSide[i] * grip;
                        wheel.LongitudinalFrictionGrip = _stockAlong[i] * grip;
                    }
                }
                _vehicle.brakes.maxTorque = _stockBrake * brake;
            }
            if (_fuel == null)
            {
                return;
            }
            if (!_fuelOn)
            {
                _stockThirst = _fuel.consumptionMultiplier;
                _stockUseFuel = _fuel.useFuel;
                _fuelOn = true;
            }
            float thirst = _stockThirst * (_kind == CarKind.Untouched ? 1f : CarRules.Thirst(_kind));
            bool uses = _stockUseFuel && !easy;
            if (!Mathf.Approximately(_fuel.consumptionMultiplier, thirst))
            {
                _fuel.consumptionMultiplier = thirst;
            }
            if (_fuel.useFuel != uses)
            {
                _fuel.useFuel = uses;
            }
        }

        // Called by the car's own input handler each frame while it is broken down: the engine gets no pedal, the brakes still work.
        private void CutPedal()
        {
            NwhVehicle vehicle = _vehicle;
            if (vehicle == null)
            {
                return;
            }
            if (vehicle.input.IsInputSwapped)
            {
                vehicle.input.Brakes = 0f;
            }
            else
            {
                vehicle.input.Throttle = 0f;
            }
        }

        private void BreakDown()
        {
            _record.Broken = true;
            _record.Breakdowns++;
            Logger?.Info("Car ownership: breakdown at " + _record.Km.ToString("F1") + " km, " + (_record.Km - _record.ServicedKm).ToString("F1") + " km since its service (due every "
                         + CarRules.ServiceEvery(_kind).ToString("F0") + "); breakdown number " + _record.Breakdowns + " for this car.");
            Notifications.Show(NotificationType.Error, "reallife_car_breakdown", null, 10f);
        }

        private void Repaired(float bill)
        {
            bool insured = _record.InsuredUntil >= TimeHelper.CurrentDay;
            float payout = insured ? CarRules.Payout(bill) : 0f;
            Logger?.Info("Car ownership: a repair of about " + bill.ToString("F0") + " noticed; " + (insured ? "the insurer pays " + payout.ToString("F0") : "the car is not insured") + ".");
            if (payout < 1f)
            {
                return;
            }
            GameManager.ChangeMoneySafe(payout, new TransactionInfo("reallife_transaction_insurance_payout"), null, null, force: true);
            _record.Claims++;
            _record.LastClaimDay = TimeHelper.CurrentDay;
            Notifications.Show(NotificationType.Success, "reallife_car_insurance_paid",
                new Dictionary<string, string>(2) { { "amount", TransitText.Money(payout) }, { "excess", TransitText.Money(CarRules.Excess) } }, 10f);
        }

        private string Note(bool easy)
        {
            if (_record == null || easy)
            {
                return null;
            }
            if (_noteService == null)
            {
                _noteService = TransitText.T("reallife_car_note_service");
                _noteTyres = TransitText.T("reallife_car_note_tyres");
                _noteBrakes = TransitText.T("reallife_car_note_brakes");
                _noteBroken = TransitText.T("reallife_car_note_breakdown");
            }
            return _record.Broken ? _noteBroken
                : _record.Km - _record.ServicedKm > CarRules.ServiceEvery(_kind) ? _noteService
                : _record.Tyres < CarRules.WarnBelow ? _noteTyres
                : _record.Brakes < CarRules.WarnBelow ? _noteBrakes : null;
        }

        // ---------------------------------------------------------------- the garage

        private GasStationController[] Stations()
        {
            if (_stations == null)
            {
                _stations = FindObjectsOfType<GasStationController>();   // once per city load
            }
            return _stations;
        }

        // The car stands in a station's repair bay of the right kind (the game has separate ones for trucks).
        private bool InBay()
        {
            if (_car.vehicleCollider == null)
            {
                return false;
            }
            Bounds bounds = _car.vehicleCollider.bounds;
            bool truck = _car.vehicleType.HasTag(TagRef.Vehicletag.istruck);
            GasStationController[] stations = Stations();
            for (int s = 0; s < stations.Length; s++)
            {
                GasStationTrigger[] bays = stations[s] != null ? stations[s].repairStationTriggers : null;
                for (int i = 0; bays != null && i < bays.Length; i++)
                {
                    if (bays[i] != null && bays[i].stationCollider != null && bays[i].isTruckGarage == truck && bays[i].IntersectsBounds(bounds))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int NearestStation(Vector3 from)
        {
            float best = float.MaxValue;
            GasStationController[] stations = Stations();
            for (int s = 0; s < stations.Length; s++)
            {
                if (stations[s] != null && stations[s].gasStationTriggers != null && stations[s].gasStationTriggers.Length > 0)
                {
                    best = Mathf.Min(best, (stations[s].transform.position - from).sqrMagnitude);
                }
            }
            return best < float.MaxValue ? Mathf.RoundToInt(Mathf.Sqrt(best)) : 0;
        }

        /// <summary>Days until a car's registration runs out; negative once it has.</summary>
        public static int RegistrationLeft(CarRecord record)
        {
            int year = SaveGameManager.Current != null && SaveGameManager.Current.gameVariables != null ? Mathf.Max(28, SaveGameManager.Current.gameVariables.daysPerYear) : 112;
            return record.RegisteredDay + year - TimeHelper.CurrentDay;
        }

        // What a service would do now and what it costs; shown as a panel with the key that orders it.
        private void ShowGarage(GameInstance save)
        {
            float since = _record.Km - _record.ServicedKm;
            float every = CarRules.ServiceEvery(_kind);
            bool service = _record.Broken || since >= every * 0.6f;
            bool tyres = _record.Tyres < CarRules.ReplaceBelow;
            bool brakes = _record.Brakes < CarRules.ReplaceBelow;
            int left = RegistrationLeft(_record);
            bool registration = left <= CarRules.RegistrationEarly;
            int key = (service ? 1 : 0) | (tyres ? 2 : 0) | (brakes ? 4 : 0) | (registration ? 8 : 0) | (left < 0 ? 16 : 0) | (_record.Broken ? 32 : 0) | (Mathf.RoundToInt(_price) << 6);
            if (key == _panelKey)
            {
                return;
            }
            _panelKey = key;
            _text.Length = 0;
            float total = 0f;
            int items = 0;
            if (service)
            {
                total += Line("reallife_garage_line_service", CarRules.Service(_price), ref items);
            }
            if (tyres)
            {
                total += Line("reallife_garage_line_tyres", CarRules.TyreSet(_price), ref items);
            }
            if (brakes)
            {
                total += Line("reallife_garage_line_brakes", CarRules.BrakeSet(_price), ref items);
            }
            if (registration)
            {
                total += Line("reallife_garage_line_registration", CarRules.Registration(_price) + (left < 0 ? CarRules.RegistrationLate : 0f), ref items);
            }
            _serviceTotal = total;
            _serviceItems = items;
            if (items == 0)
            {
                _text.Append(TransitText.T("reallife_garage_nothing_due", "km", Mathf.Max(0f, every - since).ToString("F0"), "tyres", Mathf.RoundToInt(_record.Tyres * 100f).ToString(),
                    "brakes", Mathf.RoundToInt(_record.Brakes * 100f).ToString()));
                HidePrompt();
            }
            else
            {
                _text.Append('\n').Append(TransitText.T("reallife_garage_total", "amount", TransitText.Money(total)));
                Prompt(1, TransitText.T("reallife_garage_prompt", "amount", TransitText.Money(total)));
            }
            TransportHud.Instance.ShowPanel(TransitText.T("reallife_garage_title"), _text.ToString());
        }

        private float Line(string key, float cost, ref int items)
        {
            _text.Append(TransitText.T(key, "amount", TransitText.Money(cost))).Append('\n');
            items++;
            return cost;
        }

        private void Pressed()
        {
            if (_car == null || _record == null || _servicing)
            {
                return;
            }
            if (_promptKind == 1)
            {
                if (_serviceItems == 0 || !InBay())
                {
                    return;
                }
                if (!GameManager.ChangeMoneySafe(-_serviceTotal, new TransactionInfo("reallife_transaction_car_service"), null, null, force: false, showNotification: true))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                HidePrompt();
                StartCoroutine(Service(_car, _record, _kind, _serviceTotal, 25f + 15f * _serviceItems));
            }
            else if (_promptKind == 2 || _promptKind == 3)
            {
                if (_roadside.Call(_promptKind == 2 ? RoadsideJob.Breakdown : RoadsideJob.Fuel, _car, _record, Time.time))
                {
                    HidePrompt();
                }
            }
        }

        // The work is done behind a fade while the clock runs on: the mechanic has the car for most of an hour.
        private IEnumerator Service(CarController car, CarRecord record, CarKind kind, float total, float minutes)
        {
            _servicing = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            try
            {
                _faded = true;
                yield return UiFader.Fade(0.6f, "reallife_garage_fade");
                SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
                if (sounds != null && car != null)
                {
                    sounds.PlayAudio(BigAmbitions.SoundSystem.SoundType.CarRepair, car.transform.position);
                }
                if (uis != null && uis.timeMachine != null && LifeEvents.IsSafeForEvent())
                {
                    Timestamp until = TimeHelper.Now();
                    until.AddMinutes(minutes);
                    uis.timeMachine.StartTimeMachine(until, disableCancel: true, "reallife_garage_fade");
                    float waited = 0f;
                    while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < 60f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                bool wasBroken = record.Broken;
                float since = record.Km - record.ServicedKm;
                if (record.Broken || since >= CarRules.ServiceEvery(kind) * 0.6f)
                {
                    record.ServicedKm = record.Km;
                    record.Broken = false;
                    record.GraceKm = 0f;
                }
                if (record.Tyres < CarRules.ReplaceBelow)
                {
                    record.Tyres = 1f;
                }
                if (record.Brakes < CarRules.ReplaceBelow)
                {
                    record.Brakes = 1f;
                }
                if (RegistrationLeft(record) <= CarRules.RegistrationEarly)
                {
                    record.RegisteredDay = TimeHelper.CurrentDay;
                    record.RemindedDay = 0;
                }
                Logger?.Info("Car ownership: serviced for " + total.ToString("F0") + " at " + record.Km.ToString("F1") + " km" + (wasBroken ? " (it had broken down)" : "") + ".");
                _faded = false;
                yield return UiFader.UnFade();
                Notifications.Show(NotificationType.Success, "reallife_garage_done", new Dictionary<string, string>(1) { { "amount", TransitText.Money(total) } }, 9f);
            }
            finally
            {
                _servicing = false;
                _panelKey = int.MinValue;
                if (_faded)
                {
                    _faded = false;
                    if (isActiveAndEnabled)
                    {
                        StartCoroutine(UiFader.UnFade());
                    }
                }
            }
        }

        private void Prompt(int kind, string text)
        {
            _promptKind = kind;
            _promptOn = true;
            TransportHud.Instance.ShowPrompt(text);
        }

        private void HidePrompt()
        {
            if (_promptOn)
            {
                _promptOn = false;
                _promptKind = 0;
                if (TransportHud.Exists)
                {
                    TransportHud.Instance.HidePrompt();
                }
            }
        }

        // ---------------------------------------------------------------- once a game day

        private void NewDay(GameInstance save, bool first)
        {
            int today = TimeHelper.CurrentDay;
            CarGarage.Prune(save.VehicleInstances, save.privateDriverVehicleInstances);
            for (int i = 0; save.VehicleInstances != null && i < save.VehicleInstances.Count; i++)
            {
                VehicleInstance instance = save.VehicleInstances[i];
                if (instance == null || instance.VehicleType == null || !instance.VehicleType.IsMotorVehicle || !CarGarage.Has(instance.id))
                {
                    continue;   // a car gets its record the first time it is driven or looked up on the Garage page
                }
                CarRecord record = CarGarage.For(instance.id, today);
                string name = instance.vehicleTypeName.GetLocalization();
                if (record.Claims > 0 && today - record.LastClaimDay > CarRules.ClaimForgottenAfter)
                {
                    record.Claims--;
                    record.LastClaimDay = today;
                }
                if (record.InsuredUntil > 0 && today > record.InsuredUntil && !first)
                {
                    float premium = Mathf.Round(CarRules.Premium(instance.VehicleType.price, record.Claims) * (1f + GarageBridges.InsuranceOf(instance.id)));   // a modified car costs more to insure (Scripts/Garage)
                    var data = new Dictionary<string, string>(2) { { "car", name }, { "amount", TransitText.Money(premium) } };
                    if (GameManager.ChangeMoneySafe(-premium, new TransactionInfo("reallife_transaction_car_insurance"), null, null, force: false))
                    {
                        record.InsuredUntil = today + CarRules.PolicyDays - 1;
                        Notifications.Show(NotificationType.Info, "reallife_car_insurance_renewed", data, 9f);
                    }
                    else
                    {
                        record.InsuredUntil = 0;
                        Notifications.Show(NotificationType.Warning, "reallife_car_insurance_lapsed", data, 10f);
                    }
                }
                else if (record.InsuredUntil > 0 && today > record.InsuredUntil)
                {
                    record.InsuredUntil = 0;   // ran out while the mod was not looking (a save from long ago): no back premiums
                }
                int left = RegistrationLeft(record);
                if (left <= 14 && today - record.RemindedDay >= 7 && Settings.CarOwnership && !Settings.CarEasy)
                {
                    record.RemindedDay = today;
                    Notifications.Show(NotificationType.Warning, left < 0 ? "reallife_car_registration_overdue" : "reallife_car_registration_due",
                        new Dictionary<string, string>(2) { { "car", name }, { "days", Mathf.Abs(left).ToString() } }, 10f);
                }
            }
        }

        // ---------------------------------------------------------------- the test buttons

        private bool Driving(string test, bool ownOnly)
        {
            TestRefusal why = !ModState.Loaded || SaveGameManager.Current == null ? TestRefusal.NotLoaded
                : !Settings.CarOwnership || _failed ? TestRefusal.FeatureOff
                : _car == null ? TestRefusal.NotDriving
                : ownOnly && _record == null ? TestRefusal.NoOwnCar : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            return true;
        }

        /// <summary>Options button: the driven car's tank at five percent.</summary>
        public void TestFuel()
        {
            _nextTick = 0f;
            Tick(Time.time);
            if (!Driving("Fuel", false))
            {
                return;
            }
            _car.SetFuel(_car.vehicleType.maxFuel * 0.05f);
            _lowFuelTold = false;
            Logger?.Info("Fuel test: the tank is at 5 % (" + (_car.vehicleType.maxFuel * 0.05f).ToString("F1") + " of " + _car.vehicleType.maxFuel.ToString("F0") + " litres).");
        }

        /// <summary>Options button: the driven car breaks down now.</summary>
        public void TestBreakdown()
        {
            _nextTick = 0f;
            Tick(Time.time);
            if (!Driving("Breakdown", true))
            {
                return;
            }
            if (Settings.CarEasy)
            {
                TestRefusals.Tell(Logger, "Breakdown", TestRefusal.FeatureOff, 0, "easy car ownership is on: no breakdowns");
                return;
            }
            if (!_record.Broken)
            {
                BreakDown();
            }
        }

        /// <summary>Options button: the Garage page, as the phone shows it.</summary>
        public void TestGarage()
        {
            GameInstance save = SaveGameManager.Current;
            TestRefusal why = !ModState.Loaded || save == null ? TestRefusal.NotLoaded
                : !Settings.CarOwnership ? TestRefusal.FeatureOff
                : !LifeEvents.IsSafeForEvent() ? TestRefusal.SceneActive
                : CarPhone.OwnedCars(save) == 0 ? TestRefusal.NoOwnCar : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Garage", why);
                return;
            }
            CarPhone.OpenGarage();
        }
    }
}
