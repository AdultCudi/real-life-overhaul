using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using BigAmbitions.Tags;
using Extensions;
using Helpers;
using Localizor;
using NWH.VehiclePhysics2.Modules.Fuel;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Owning a car beyond what the game does (<see cref="CarRules"/> has every figure): distance, service, wearing tyres and
    /// brakes, fuel by the car's kind, breakdowns and insurance, kept per car in a <see cref="CarRecord"/>. Everything changed
    /// on the car is recorded first and put back exactly when it is left. Cost: one comparison a frame; a timer twice a second
    /// while a car is driven, once a second otherwise. A fault puts the car back and switches the feature off for the session.
    /// </summary>
    public sealed partial class CarOwnership : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "CarOwnership", typeof(CarOwnership));

        private const float DriveTick = 0.5f;      // seconds
        private const float IdleTick = 1f;         // seconds
        private const float LowFuel = 0.15f;       // of the tank, at which the player is told
        private const float StoppedSpeed = 0.5f;   // m/s

        public static IModLogger Logger;
        public static CarOwnership Instance { get; private set; }

        /// <summary>True while the panel over a broken-down car has its button on the game's interact key, and in the frame it ran.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._help.Showing || Instance._keyFrame == Time.frameCount);

        private readonly List<float> _stockSide = new List<float>(6);
        private readonly List<float> _stockAlong = new List<float>(6);
        private CarRoadside _roadside;
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
        private readonly GameUi.WorldPanel _help = new GameUi.WorldPanel { Name = "roadside help" };
        private Action _onHelp;
        private RoadsideJob _helpJob;
        private int _helpKey = int.MinValue;
        private int _helpState;         // 0 not up, 1 up, 2 refused by the game's own panel
        private string _carName;
        private int _keyFrame = -1;
        private int _lastDay = -1;
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
            _cut = Guard.Listener(CutPedal);   // each physics step of the vehicle's input: a fault never reaches the vehicle's own
            _onHelp = HelpClicked;
            CarDash.Logger = Logger;
            GameUi.WorldPanel.Logger = Logger;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                Leave();
                CarDash.Remove();
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

        private Action _onSave;   // made once, as SaveHook compares it

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

        private static readonly int HotUpdate = HotSpots.Slot("Transport: CarOwnership.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            try
            {
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
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // nothing is allocated while the game's save event has not changed
            GarageBay.Tick(game);   // the repair bay keeps its extra line and button even with the Garage switched off

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
                    Logger?.Error(e);   // a car of a kind the game no longer knows: today's round is skipped, the feature goes on
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
            bool owned = car.vehicleInstance != null && save.VehicleInstances != null && save.VehicleInstances.Contains(car.vehicleInstance)
                         && !Dealerships.Stock(car.vehicleInstance)    // a dealership's stock car is on its trade plates
                         && !Family.PartnerCar.Is(car.vehicleInstance); // the partner's car is theirs to keep
            _record = owned ? CarGarage.For(car.vehicleInstance.id, TimeHelper.CurrentDay) : null;   // a hired or borrowed car has no record
            if (_record != null)
            {
                _record.LastDamage = car.vehicleInstance.damage;
            }
            _enteredAt = now;
            _lowFuelTold = false;
            _carName = car.vehicleType.vehicleTypeName.GetLocalization();
            _helpKey = int.MinValue;
        }

        // Everything this put on the driven car comes off, exactly. Safe with no car.
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
            _wasAtSet = false;
            HideHelp("the car was left, or car ownership was switched off");
            CarDash.Hide();
        }

        /// <summary>The city's potholes (Wiring/FaultLinks): whether the car went over one since the last tick, its wear and jolt. Null: none.</summary>
        public static RoadHitAsk RoadHit;

        public delegate bool RoadHitAsk(Vector3 from, Vector3 to, float speed, out float tyreWear, out float jolt);

        private const float JoltAhead = 1.3f;   // metres ahead of the car's centre the jolt lifts: the front wheels hit first

        private Vector3 _wasAt;
        private bool _wasAtSet;

        // Over a pothole: the tyres worn (not on the easy setting) and the car jolted at its front wheels.
        private void Pothole(float speed, bool easy)
        {
            Vector3 here = _body != null ? _body.position : _car.transform.position;
            if (_wasAtSet && RoadHit != null && speed > 1f && RoadHit(_wasAt, here, speed, out float wear, out float jolt))
            {
                if (_record != null && !easy)
                {
                    _record.Tyres = Mathf.Max(0f, _record.Tyres - wear);
                }
                if (_body != null)
                {
                    _body.AddForceAtPosition(Vector3.up * (jolt * _body.mass), here + _car.transform.forward * JoltAhead, ForceMode.Impulse);
                }
            }
            _wasAt = here;
            _wasAtSet = true;
        }

        private void Drive(float now, GameInstance save)
        {
            bool easy = Settings.CarEasy;
            Vector3 velocity = _body != null ? _body.velocity : Vector3.zero;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            float km = VehicleCostRules.Km(speed, DriveTick);   // a speed from broken physics or a long hitch is not driving
            float damage = _car.vehicleInstance != null ? _car.vehicleInstance.damage : 0f;
            Pothole(speed, easy);

            if (_record != null)
            {
                _record.Km += km;
                if (!easy)
                {
                    // A custom shop build wears faster (0 for a stock car).
                    float worn = km * (1f + (_car.vehicleInstance != null ? GarageBridges.WearOf(_car.vehicleInstance.id) : 0f));
                    _record.Tyres = Mathf.Max(0f, _record.Tyres - worn / CarRules.TyreLife(_kind));
                    _record.Brakes = Mathf.Max(0f, _record.Brakes - worn / CarRules.BrakeLife(_kind));
                }
                // The game's own repair shows as the damage figure dropping.
                if (_record.LastDamage >= 0f && damage < _record.LastDamage - 0.03f)
                {
                    Repaired(VehicleCostRules.Repair(_record.LastDamage - damage, _price));
                }
                _record.LastDamage = damage;

                if (!easy && !_record.Broken && speed > 4f && now - _enteredAt > 30f && _record.Km > _record.GraceKm && km > 0f
                    && UnityEngine.Random.value < CarRules.BreakdownPerKm(_record.Km - _record.ServicedKm, CarRules.ServiceEvery(_kind), damage) * km)
                {
                    BreakDown();
                }
            }

            // One tick after getting in, so the car's own tuning has read it as the game made it.
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
                _vehicle.input.states.hazardLights = true;   // the lights' own toggle
            }

            bool fuelled = _fuel != null && _fuel.capacity > 0f && !save.gameVariables.disableVehicleFuel;
            float level = fuelled ? Mathf.Clamp01(_fuel.amount / _fuel.capacity) : 1f;
            string note = Note(easy);
            if (note != null)
            {
                CarDash.Show(note, ReferenceEquals(note, _noteBroken));
            }
            else
            {
                CarDash.Hide();
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

            // Standing still broken or dry outside a repair bay: the panel over the car, with roadside assistance.
            bool dry = fuelled && !easy && level <= 0.005f;
            bool still = (broken || dry) && speed < StoppedSpeed && !_servicing && _record != null && !_roadside.Active && !JobDirector.TakesInteractKey && !TransitDirector.TakesInteractKey
                         && (_help.Showing || !ThirdPersonCamera.IsUiOpen());
            if (still && !InBay())
            {
                Help(broken ? RoadsideJob.Breakdown : RoadsideJob.Fuel);
            }
            else
            {
                HideHelp("the car moves, help is on its way, the car is in a repair bay, or another screen is open");
            }
        }

        // The wheels' grip and the brakes' torque for the wear, and the fuel system for the kind of car.
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

        // The car's input handler each frame while it is broken down: no pedal, the brakes still work.
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
            Logger?.Info("Car ownership: breakdown at " + _record.Km.ToString("F1", CultureInfo.InvariantCulture) + " km, "
                         + (_record.Km - _record.ServicedKm).ToString("F1", CultureInfo.InvariantCulture) + " km since its service (due every "
                         + CarRules.ServiceEvery(_kind).ToString("F0") + "); breakdown number " + _record.Breakdowns + " for this car.");
            Notifications.Show(NotificationType.Error, "reallife_car_breakdown", null, 10f);
        }

        private void Repaired(float bill)
        {
            bool insured = _record.InsuredUntil >= TimeHelper.CurrentDay;
            bool reported = insured && CarGarage.Waives(TimeHelper.CurrentDay);   // a police report on the break-in or the accident: no excess
            float payout = insured ? CarRules.Payout(bill, reported) : 0f;
            Logger?.Info("Car ownership: a repair of about " + bill.ToString("F0") + " noticed; " + (insured ? "the insurer pays " + payout.ToString("F0") : "the car is not insured") + ".");
            if (payout < 1f)
            {
                return;
            }
            GameManager.ChangeMoneySafe(payout, new TransactionInfo("reallife_transaction_insurance_payout"), null, null, force: true);
            if (reported)
            {
                CarGarage.Spend();
            }
            _record.Claims++;
            _record.LastClaimDay = TimeHelper.CurrentDay;
            Notifications.Show(NotificationType.Success, "reallife_car_insurance_paid",
                new Dictionary<string, string>(2) { { "amount", TransitText.Money(payout) }, { "excess", TransitText.Money(reported ? 0f : CarRules.Excess) } }, 10f);
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
                if (Dealerships.Stock(instance) || Family.PartnerCar.Is(instance))
                {
                    continue;   // a dealership's stock car driven before this mod knew it was: no premium, no reminder
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
                    // One policy renews however many days have gone by (no back premiums).
                    float premium = Mathf.Round(CarRules.PremiumOf(instance.VehicleType.price, record.Claims, GarageBridges.InsuranceOf(instance.id)));
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
    }
}
