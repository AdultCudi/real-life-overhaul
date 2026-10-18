using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a roadside call is for.</summary>
    public enum RoadsideJob { None, Breakdown, Fuel }

    /// <summary>
    /// Roadside assistance at the player's car: a mechanic in hi-vis (a <see cref="JobCast"/> of this feature's own) walks
    /// up from out of view on the navigation mesh, kneels at the front for a few seconds and walks off; then the car runs or
    /// has ten litres in it, and the call-out is charged. If nobody can be put on the street the job is still done when the
    /// time is up, so the player is never stranded. One call at a time, ticked twice a second by <see cref="CarOwnership"/>.
    /// </summary>
    public sealed class CarRoadside
    {
        private const float EtaLeast = 22f;      // seconds from the call to the mechanic's arrival, at least
        private const float EtaMost = 40f;       // and at most
        private const float WorkFor = 10f;       // seconds kneeling at the car
        private const float GiveUp = 75f;        // seconds after the call by which the work is done, visit or no visit

        private enum Step { None, OnTheWay, Walking, Working, Leaving }

        private readonly MonoBehaviour _host;
        private JobCast _cast;
        private StreetActor _mechanic;
        private CarController _car;
        private CarRecord _record;
        private Step _step;
        private Vector3 _carAt;
        private Vector3 _spot;
        private Vector3 _away;
        private float _calledAt;
        private float _until;
        private float _fee;
        private bool _done;

        public CarRoadside(MonoBehaviour host)
        {
            _host = host;
        }

        public RoadsideJob Job { get; private set; }
        public bool Active => Job != RoadsideJob.None || _step == Step.Leaving;
        public CarController Car => _car;

        /// <summary>What a call costs for this car.</summary>
        public static float Fee(RoadsideJob job, CarRecord record)
        {
            if (job == RoadsideJob.Fuel)
            {
                return CarRules.FuelCallOut + CarRules.FuelLitres * CarRules.FuelPerLitre;
            }
            bool insured = record != null && record.InsuredUntil >= TimeHelper.CurrentDay;
            return insured ? CarRules.RoadsideCallOut * 0.5f : CarRules.RoadsideCallOut;
        }

        /// <summary>Takes the call. False if one is already on or the car is moving.</summary>
        public bool Call(RoadsideJob job, CarController car, CarRecord record, float now)
        {
            if (Job != RoadsideJob.None || car == null || job == RoadsideJob.None)
            {
                return false;
            }
            Finish();   // whoever is still walking away from the last call goes now
            Job = job;
            _car = car;
            _record = record;
            _carAt = car.transform.position;
            _calledAt = now;
            _until = now + Random.Range(EtaLeast, EtaMost);
            _fee = Fee(job, record);
            _done = false;
            _step = Step.OnTheWay;
            CarOwnership.Logger?.Info("Roadside: called for " + job + " (" + _fee.ToString("F0") + "); expected in " + (_until - now).ToString("F0") + " s.");
            Notifications.Show(NotificationType.Info, "reallife_roadside_called", null, 7f);
            return true;
        }

        public void Tick(float now)
        {
            if (_step == Step.None)
            {
                return;
            }
            if (_step != Step.Leaving)
            {
                // The car was driven or towed away, or is gone: nobody comes and nothing is charged.
                if (_car == null || (_car.transform.position - _carAt).sqrMagnitude > 9f)
                {
                    CarOwnership.Logger?.Info("Roadside: the car is no longer where it was; the call is off.");
                    Notifications.Show(NotificationType.Info, "reallife_roadside_cancelled", null, 6f);
                    Job = RoadsideJob.None;
                    Leave(now);
                    return;
                }
                if (now - _calledAt > GiveUp && !_done)
                {
                    Complete();   // held up on the way, or nowhere to work: done all the same
                    Leave(now);
                    return;
                }
            }
            bool lost = _mechanic != null && (!_mechanic.IsValid || _mechanic.Limp || !_mechanic.InUse);
            if (lost)
            {
                _mechanic = null;   // knocked down by traffic: the casualty system has them
            }
            switch (_step)
            {
                case Step.OnTheWay:
                    if (now >= _until)
                    {
                        _step = Step.Walking;
                        if (!Arrive())
                        {
                            CarOwnership.Logger?.Info("Roadside: nobody could be put on the street near the car (no clear place beside it, or no way to it on foot); the job is done without the visit.");
                            Complete();
                            Finish();
                        }
                    }
                    break;

                case Step.Walking:
                    if (_mechanic == null)
                    {
                        Complete();
                        Leave(now);
                    }
                    else if (_mechanic.Within(0.3f) && CitySpots.Flat(_mechanic.Position - _spot) < 0.6f * 0.6f)
                    {
                        _mechanic.StandAt(_mechanic.Position, _carAt);
                        _mechanic.Kneel();
                        _until = now + WorkFor;
                        _step = Step.Working;
                    }
                    break;

                case Step.Working:
                    if (_mechanic == null || now >= _until)
                    {
                        Complete();
                        Leave(now);
                    }
                    break;

                case Step.Leaving:
                    if (_mechanic == null || now >= _until || (_mechanic.Within(0.4f) && !CitySpots.InView(_mechanic.Position)))
                    {
                        Finish();
                    }
                    break;
            }
            if (_cast != null && _cast.AnyOut)
            {
                _cast.Cull();
            }
        }

        // The mechanic appears out of view and walks to the front of the car, or to a front wheel if the bonnet is against something.
        private bool Arrive()
        {
            JobSpots.CarFrame(_car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            bool found = false;
            for (int i = 0; i < 3 && !found; i++)
            {
                Vector3 want = i == 0 ? centre + forward * (halfLength + 0.55f)
                    : centre + forward * (halfLength * 0.6f) + right * ((i == 1 ? 1f : -1f) * (halfWidth + 0.5f));
                // Walkable ground touching no vehicle but the car, and no parked car or street furniture.
                found = StreetSpots.OnRoad(want, 0.7f, out _spot) && CitySpots.Flat(_spot - want) < 0.4f * 0.4f && JobSpots.Clear(_spot, _car.transform)
                        && !SceneParking.HasParked(_spot, Quaternion.identity, 0.3f, 0.3f);
            }
            if (!found || !CitySpots.PavementRing(_spot, 28f, 46f, true, out Vector3 from))
            {
                return false;
            }
            _away = from;
            if (_cast == null)
            {
                _cast = new JobCast(_host, CarOwnership.Logger);
            }
            _mechanic = _cast.Put(from, true);
            if (_mechanic == null)
            {
                return false;
            }
            _mechanic.HiVis(true);
            if (!_mechanic.MoveTo(_spot, Pace.Walk))
            {
                Finish();
                return false;
            }
            CarOwnership.Logger?.Info("Roadside: the mechanic walks in from " + Mathf.Sqrt(CitySpots.Flat(from - _spot)).ToString("F0") + " m away.");
            return true;
        }

        private void Complete()
        {
            if (_done || Job == RoadsideJob.None)
            {
                return;
            }
            _done = true;
            var data = new Dictionary<string, string>(1) { { "amount", TransitText.Money(_fee) } };
            GameManager.ChangeMoneySafe(-_fee, new TransactionInfo("reallife_transaction_roadside"), null, null, force: true);
            if (Job == RoadsideJob.Fuel)
            {
                if (_car != null)
                {
                    _car.AddFuel(CarRules.FuelLitres);
                }
                Notifications.Show(NotificationType.Success, "reallife_roadside_fuel_done", data, 8f);
            }
            else
            {
                if (_record != null)
                {
                    _record.Broken = false;
                    _record.GraceKm = _record.Km + CarRules.GraceKm;
                }
                Notifications.Show(NotificationType.Success, "reallife_roadside_fixed", data, 9f);
            }
            SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
            if (sounds != null && _car != null)
            {
                sounds.PlayAudio(BigAmbitions.SoundSystem.SoundType.CarRepair, _car.transform.position);
            }
            CarOwnership.Logger?.Info("Roadside: " + Job + " seen to; " + _fee.ToString("F0") + " charged.");
            Job = RoadsideJob.None;
        }

        private void Leave(float now)
        {
            Job = RoadsideJob.None;
            if (_mechanic != null && _mechanic.IsValid && !_mechanic.Limp)
            {
                _mechanic.ClearPose();
                if (_mechanic.MoveTo(_away, Pace.Walk))
                {
                    _step = Step.Leaving;
                    _until = now + 50f;
                    return;
                }
            }
            Finish();
        }

        /// <summary>Everyone back in at once: the end of a visit, the option switched off, the city going.</summary>
        public void Finish()
        {
            if (_mechanic != null && _cast != null)
            {
                if (_mechanic.IsValid && !_mechanic.Limp)
                {
                    _mechanic.HiVis(false);
                }
                _cast.Release(_mechanic);
            }
            _mechanic = null;
            _step = Step.None;
            if (Job == RoadsideJob.None)
            {
                _car = null;
                _record = null;
            }
        }

        public void Cancel()
        {
            Job = RoadsideJob.None;
            Finish();
        }

        public void Dispose()
        {
            Cancel();
            _cast?.Dispose();
            _cast = null;
        }
    }
}
