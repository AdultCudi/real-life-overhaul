using System.Collections.Generic;
using BAModAPI;
using Localizor;
using UI.Notification;
using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The "Measure this car" button: times the next full-throttle run from a standstill (0-50, 0-100 and
    /// the speed after 20 seconds), so a car's targets can be checked on the road. Says what it found in a
    /// toast and in the log. Switched off except between the button and the end of the run.
    /// </summary>
    public sealed class CarMeasure : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("driving", "CarMeasure", typeof(CarMeasure));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float WaitFor = 90f;      // seconds the button stays armed
        private const float RunFor = 20f;
        private const float Standing = 0.3f;    // m/s

        public static IModLogger Logger;

        private static CarMeasure _instance;

        private NwhVehicle _vehicle;
        private string _type;
        private bool _running;
        private float _armedAt;
        private float _startedAt;
        private float _to50;
        private float _to100;
        private float _lastSpeed;

        /// <summary>The options button.</summary>
        public static void Arm()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            if (CarTuning.Instance == null)
            {
                return;   // not in the city
            }
            if (car == null || car.vehicleController == null)
            {
                TestRefusals.Tell(Logger, "Car measurement", TestRefusal.NotDriving);
                return;
            }
            if (_instance == null)
            {
                _instance = CarTuning.Instance.gameObject.AddComponent<CarMeasure>();
            }
            _instance._vehicle = car.vehicleController;
            _instance._type = car.vehicleType != null ? car.vehicleType.vehicleTypeName : car.name;
            _instance._running = false;
            _instance._armedAt = Time.unscaledTime;
            _instance.enabled = true;
            Logger?.Info("Car measurement: armed for " + _instance._type + ".");
            Notifications.Show(NotificationType.Info, "reallife_car_measure_armed", null, 8f);
        }

        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            NwhVehicle vehicle = _vehicle;
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            if (vehicle == null || car == null || car.vehicleController != vehicle)
            {
                Stop("the player left the car");
                return;
            }

            float speed = vehicle.Speed;
            float pedal = vehicle.input.InputSwappedThrottle;
            if (!_running)
            {
                if (Time.unscaledTime - _armedAt > WaitFor)
                {
                    Stop("no run was started");
                }
                else if (speed < Standing && pedal > 0.05f && vehicle.powertrain.transmission.Gear >= 0)
                {
                    _running = true;
                    _startedAt = Time.time;
                    _to50 = -1f;
                    _to100 = -1f;
                    _lastSpeed = speed;
                }
                return;
            }

            // In game time, like the car itself. A crossing is placed within the frame it happened in.
            float time = Time.time - _startedAt;
            if (time > 1.5f && pedal < 0.9f)
            {
                Stop("the throttle was lifted");
                return;
            }
            if (_to50 < 0f && speed >= 13.8889f)
            {
                _to50 = Crossed(13.8889f, speed, time);
            }
            if (_to100 < 0f && speed >= 27.7778f)
            {
                _to100 = Crossed(27.7778f, speed, time);
            }
            _lastSpeed = speed;
            if (time >= RunFor)
            {
                Finish(speed * 3.6f);
            }
        }

        private float Crossed(float mark, float speed, float time)
        {
            float gained = speed - _lastSpeed;
            return gained > 0.0001f ? time - Time.deltaTime * (speed - mark) / gained : time;
        }

        private void Finish(float kmhAfter20)
        {
            CarTuning tuning = CarTuning.Instance;
            bool tuned = tuning != null && tuning.Tuned == _vehicle;
            var data = new Dictionary<string, string>(6)
            {
                { "car", _type.GetLocalization() },
                { "to50", Seconds(_to50) },
                { "to100", Seconds(_to100) },
                { "after20", Mathf.RoundToInt(kmhAfter20).ToString() },
                { "target", tuned ? tuning.TargetSeconds.ToString("F1") : "-" },
                { "top", tuned ? Mathf.RoundToInt(tuning.TargetTopKmh).ToString() : "-" }
            };
            Logger?.Info($"Car measurement: {_type}: 0-50 {data["to50"]} s, 0-100 {data["to100"]} s, {data["after20"]} km/h after 20 s"
                + (tuned ? $" (target 0-100 {data["target"]} s, top {data["top"]} km/h)." : " (this car is as the game made it)."));
            Notifications.Show(NotificationType.Success, "reallife_car_measure_result", data, 14f);
            enabled = false;
            _vehicle = null;
        }

        private static string Seconds(float value)
        {
            return value < 0f ? "-" : value.ToString("F1");
        }

        private void Stop(string why)
        {
            Logger?.Info("Car measurement: stopped (" + why + ").");
            Notifications.Show(NotificationType.Warning, "reallife_car_measure_stopped", null, 6f);
            enabled = false;
            _vehicle = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
