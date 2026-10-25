using System.Collections.Generic;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes crashing the player's car matter beyond bodywork: liability for hitting another car (settled on the spot if the
    /// player stops, else PolicePursuit's), injuries at speed, and hospital after a severe crash. Cost: a comparison of the
    /// selected vehicle a frame; the rest on a collision.
    /// </summary>
    public class CrashConsequences : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "CrashConsequences", typeof(CrashConsequences));

        internal const float LiabilityLeast = 150f, LiabilityMost = 3000f;   // damages to another car, at the start's prices
        // Impact speeds in m/s along the contact's normal (CarContacts: only the car hitting something, never the road or a kerb).
        internal const float LiabilitySpeed = 7f;    // ~25 km/h
        private const float InjurySpeed = 10f;      // ~36 km/h
        private const float SeriousSpeed = 14f;     // ~50 km/h: may put the player in hospital
        private const float HospitalSpeed = 17f;    // ~61 km/h: always does
        private const float Cooldown = 4f;          // seconds before another crash counts

        public static IModLogger Logger;

        private CarController _hookedCar;
        private Rigidbody _hookedBody;
        private float _lastCrashTime = -100f;

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: CrashConsequences.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
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
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            if (car == _hookedCar)
            {
                return;
            }
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                _onCollision = _onCollision ?? Guard.Listener<Collision>(OnCollision);   // (made once, through the guard: a fault never reaches the vehicle's own listeners)
                car.vehicleController.onCollision.AddListener(_onCollision);
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
                _hookedCar.vehicleController.onCollision.RemoveListener(_onCollision);
            }
            _hookedCar = null;
            _hookedBody = null;
        }

        private UnityAction<Collision> _onCollision;

        private void OnCollision(Collision collision)
        {
            if (!Settings.CrashConsequences || !ModState.Loaded || LifeEvents.Instance == null
                || Time.unscaledTime - _lastCrashTime < Cooldown || _hookedCar == null)
            {
                return;
            }

            CarContact contact = CarContacts.Judge(collision, _hookedCar.transform);
            if (!contact.Counts(LiabilitySpeed))
            {
                return;
            }
            float speed = contact.Impact;
            // Running into a person (or a body in the road) is PedestrianIncidents' business, and does not hurt the driver.
            if (collision.collider != null && collision.collider.GetComponentInParent<ThirdPersonCharacter>() != null)
            {
                return;
            }
            _lastCrashTime = Time.unscaledTime;

            VehicleComponent struck = collision.collider != null ? collision.collider.GetComponentInParent<VehicleComponent>() : null;
            bool hitTraffic = struck != null;
            // The reckless car (Traffic/Reckless): a collision with it is its driver's fault, and the player owes nothing for it.
            bool theirFault = hitTraffic && RecklessDrivers.AtFault(struck);
            News.Report(NewsKind.PlayerCrash, Helpers.PlayerHelper.GetPosition());
            Logger?.Info(System.FormattableString.Invariant(
                $"Crash: impact {speed:F1} m/s, hitTraffic={hitTraffic}{(theirFault ? ", with the reckless car: not the player's fault, no liability" : string.Empty)}"));

            if (theirFault)
            {
                RecklessDrivers.Instance.HitPlayer(struck, speed);   // the scene starts round it, and says whose fault it was
            }
            else if (hitTraffic)
            {
                int liability = Mathf.RoundToInt(Prices.At(Mathf.Clamp(speed * speed * 2.5f, LiabilityLeast, LiabilityMost)) / 10f) * 10;   // (at today's prices)

                // With police on, the player first gets the chance to stop: the bill comes then, or the police do.
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
            // With Permadeath on, a very fast impact is a near miss the first time and may end the life after (Permadeath/PermadeathRules).
            Permadeath.Verdict fate = Permadeath.Mortality.Judge(Permadeath.DeathCause.Crash, speed);
            if (fate == Permadeath.Verdict.Death && Permadeath.Mortality.Die(Permadeath.DeathCause.Crash))
            {
                return;
            }
            if (hospital)
            {
                LifeEvents.Instance.Injure(48, 25f, "reallife_crash_severe");
                LifeEvents.Instance.SendToHospital();
                if (fate == Permadeath.Verdict.NearMiss)
                {
                    Permadeath.Mortality.NearMiss(Permadeath.DeathCause.Crash);
                }
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
