using System.Collections.Generic;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using NWH.VehiclePhysics2.Damage;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes crashing the player's car matter beyond bodywork: liability when hitting another
    /// car (settled on the spot if the player stops, otherwise a matter for PolicePursuit),
    /// injuries at speed, and an ambulance to hospital after a severe crash.
    /// </summary>
    public class CrashConsequences : MonoBehaviour
    {
        // Impact speeds in m/s (relative velocity of the two colliders).
        private const float LiabilitySpeed = 7f;    // ~25 km/h
        private const float InjurySpeed = 10f;      // ~36 km/h
        private const float SeriousSpeed = 14f;     // ~50 km/h: may put the player in hospital
        private const float HospitalSpeed = 17f;    // ~61 km/h: always does
        private const float Cooldown = 4f;

        public static IModLogger Logger;

        private CarController _hookedCar;
        private Rigidbody _hookedBody;
        private float _lastCrashTime = -100f;

        private void Update()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            if (car == _hookedCar)
            {
                return;
            }
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                car.vehicleController.onCollision.AddListener(OnCollision);
                _hookedCar = car;
                _hookedBody = car.GetComponent<Rigidbody>();   // looked up once per vehicle
            }
        }

        private void OnDestroy()
        {
            Unhook();
        }

        private void Unhook()
        {
            if (_hookedCar != null && _hookedCar.vehicleController != null)
            {
                _hookedCar.vehicleController.onCollision.RemoveListener(OnCollision);
            }
            _hookedCar = null;
            _hookedBody = null;
        }

        private void OnCollision(Collision collision)
        {
            if (!Settings.CrashConsequences || !ModState.Loaded || LifeEvents.Instance == null
                || Time.unscaledTime - _lastCrashTime < Cooldown || !DamageHandler.IsCollisionValid(collision))
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < LiabilitySpeed)
            {
                return;
            }
            // Running into a person (or a body in the road) is PedestrianIncidents' business, and does not hurt the driver.
            if (collision.collider != null && collision.collider.GetComponentInParent<ThirdPersonCharacter>() != null)
            {
                return;
            }
            _lastCrashTime = Time.unscaledTime;

            VehicleComponent struck = collision.collider != null ? collision.collider.GetComponentInParent<VehicleComponent>() : null;
            bool hitTraffic = struck != null;
            // The one car that is being driven recklessly (Traffic/Reckless): a collision with it is its driver's fault. The
            // player owes nothing and nobody comes after them for it; what the impact does to the player is as for any crash.
            bool theirFault = hitTraffic && RecklessDrivers.AtFault(struck);
            News.Report(NewsKind.PlayerCrash, Helpers.PlayerHelper.GetPosition());
            Logger?.Info($"Crash: impact {speed:F1} m/s, hitTraffic={hitTraffic}{(theirFault ? ", with the reckless car: not the player's fault, no liability" : string.Empty)}");

            if (theirFault)
            {
                RecklessDrivers.Instance.HitPlayer(struck, speed);   // the scene starts round it, and says whose fault it was
            }
            else if (hitTraffic)
            {
                int liability = Mathf.RoundToInt(Mathf.Clamp(speed * speed * 2.5f, 150f, 3000f) / 10f) * 10;

                // With police on, the player first gets the chance to stop; the bill comes when
                // they do, or the police come if they don't.
                PolicePursuit pursuit = PolicePursuit.Instance;
                bool deferred = pursuit != null && _hookedBody != null
                                && pursuit.ReportCollision(_hookedBody, liability, _hookedBody.position);
                if (!deferred)
                {
                    ChargeLiability(liability, "reallife_crash_liability");
                }
            }

            // Between the serious and hospital speeds it comes down to luck, weighted by how hard the hit was.
            bool hospital = speed >= HospitalSpeed
                            || (speed >= SeriousSpeed && Random.value < Mathf.InverseLerp(SeriousSpeed, HospitalSpeed, speed) * 0.7f + 0.15f);
            if (hospital)
            {
                LifeEvents.Instance.Injure(48, 25f, "reallife_crash_severe");
                LifeEvents.Instance.SendToHospital();
            }
            else if (speed >= InjurySpeed)
            {
                LifeEvents.Instance.Injure(24, 12f, "reallife_crash_injured");
            }
        }

        /// <summary>Takes the damages for hitting another car and tells the player, using the given message.</summary>
        public static void ChargeLiability(int liability, string notificationKey)
        {
            if (liability <= 0)
            {
                return;
            }
            GameManager.ChangeMoneySafe(-liability, new TransactionInfo("reallife_transaction_accident_liability"), null, null, force: true);
            Justice.CrashVictim(liability, 0, 0);   // now and then the other driver sues (Scripts/Justice)
            var data = new Dictionary<string, string> { { "amount", ((float)liability).ToCurrencyFormat() } };
            Notifications.Show(NotificationType.Error, notificationKey, data, 8f);
        }
    }
}
