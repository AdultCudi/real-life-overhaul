using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using UI;
using UI.Guiders;
using UI.Notification;
using UI.Overlays;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>Jobs' director: the car a job is worked with, crashes on the job, and the game's guider.</summary>
    public partial class JobDirector
    {
        private const float CrashSpeed = 3f;         // m/s of closing speed along the contact's normal that counts as a crash on the job
        private const float CrashGap = 2.5f;         // seconds between two crashes that both count
        private static readonly Color GuideColour = new Color(0.20f, 0.62f, 0.95f, 1f);
        private CarController _hookedCar;
        private float _lastCrash = -100f;
        private float _guidedTime;

        private bool _guided;            // the game's own job guider (arrow, city map) points at the job's marker

        private Vector3 _guidedAt;
        private bool _guiderBroken;

        private void Hook(CarController car)
        {
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                _onCollision = _onCollision ?? Guard.Listener<Collision>(OnCollision);   // made once, through the guard: a fault never reaches the vehicle's own listeners
                car.vehicleController.onCollision.AddListener(_onCollision);
                _hookedCar = car;
            }
        }

        private void Unhook()
        {
            if (_hookedCar != null && _hookedCar.vehicleController != null)
            {
                _hookedCar.vehicleController.onCollision.RemoveListener(_onCollision);
            }
            _hookedCar = null;
        }

        private UnityAction<Collision> _onCollision;

        private void OnCollision(Collision collision)
        {
            if (_job == null || _broken || Time.time - _lastCrash < CrashGap || _hookedCar == null)
            {
                return;
            }
            try
            {
                CarContact contact = CarContacts.Judge(collision, _hookedCar.transform);   // the car hitting something, never the road or a kerb
                if (!contact.Counts(CrashSpeed))
                {
                    return;
                }
                _lastCrash = Time.time;
                _job.OnCrash(contact.Impact);
                if (_context.Shift != null && _context.Shift.Counts)
                {
                    CareerState.Crash();   // what the boss reads out at the next review
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game's destination guider puts the place on the city map too; the job's world panels handle actions there.
        private void Guide()
        {
            if (_guiderBroken || _job == null)
            {
                return;
            }
            try
            {
                if (!_job.HasMarker || _context.Indoors)   // indoors the job's world panel points the way: the game's arrow is for streets
                {
                    Unguide();
                }
                else if (!_guided || (_job.MarkerAt - _guidedAt).sqrMagnitude > 1f
                         || (_guidedTime < _rental.SpawnedAt + 1f && Time.time > _rental.SpawnedAt + 1f))   // a van just hired resets the guider a frame after it appears
                {
                    _guidedTime = Time.time;
                    GuidersManager.SetGuiderTarget(_job.MarkerAt, _job.MarkerLabel, JobPhone.Icon, GuideColour, DirectionGuiderType.JobDestination);
                    _guided = true;
                    _guidedAt = _job.MarkerAt;
                }
            }
            catch (Exception e)
            {
                _guiderBroken = true;
                _guided = false;
                Logger?.Error(e);
                Logger?.Warn("Jobs: the game's destination guider could not be used; the job's panels are still available.");
            }
        }

        private void Unguide()
        {
            if (!_guided)
            {
                return;
            }
            _guided = false;
            try
            {
                // Only ours to clear while no delivery of the game's own is using it.
                if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission == null)
                {
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                }
            }
            catch (Exception e)
            {
                _guiderBroken = true;
                Logger?.Error(e);
            }
        }

        // One of the game's job vans is nobody's car for a job, except the one hired for this shift.
        private static bool Usable(CarController car)
        {
            return !(car.vehicleInstance is Vehicles.DeliveryDriverJob.DeliveryVehicleInstance)
                   || (Instance != null && ReferenceEquals(Instance._rental.Car, car));
        }

        /// <summary>The car a driving job uses: the player's own if one is near, else a hired van at the roadside (the day's fee charged once). Null if neither.</summary>
        public CarController CarFor(bool test, out string reasonKey)
        {
            reasonKey = null;
            CarController own = NearestCar(60f);
            if (own != null)
            {
                return own;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                reasonKey = "reallife_job_no_car";
                return null;
            }
            return _rental.Hire(gm.playerController.transform.position, test, out reasonKey);
        }

        /// <summary>
        /// The jobs' hired van for work that is not a gig (the Marketplace's junk runs and moves, through Wiring/HustleLinks):
        /// the same van and the same day's fee. Refused while a gig is on, since the shift's van is the shift's.
        /// </summary>
        public CarController HireVan(Vector3 near, out string reasonKey)
        {
            if (_job != null || _broken)
            {
                reasonKey = _broken ? "reallife_job_no_off" : "reallife_job_no_busy";
                return null;
            }
            return _rental.Hire(near, false, out reasonKey);
        }

        /// <summary>Work that hired the van is over: it goes back once the player is out of it. A gig's van is left to the gig.</summary>
        public void ReturnVan()
        {
            if (_job == null)
            {
                _rental.Return();
            }
        }

        /// <summary>The truck a heavy load goes in: the player's own heavy truck if one is near, else one hired for the day. Null if neither.</summary>
        public CarController HeavyFor(bool test, out string reasonKey)
        {
            reasonKey = null;
            CarController own = NearestHeavy(60f);
            if (own != null)
            {
                return own;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                reasonKey = "reallife_job_no_car";
                return null;
            }
            return _rental.Hire(gm.playerController.transform.position, test, true, out reasonKey);
        }

        /// <summary>Whether a car is a truck the heavy class is needed for (never while that class is off).</summary>
        public static bool IsHeavyTruck(CarController car)
        {
            return car != null && Licences.Driving.ClassOf(car, out LicenceClass kind) && kind == LicenceClass.Heavy;
        }

        /// <summary>As <see cref="NearestCar"/>, only a heavy truck: the one being driven, else the player's own nearest, a hired one counting.</summary>
        public static CarController NearestHeavy(float within)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                return null;
            }
            if (gm.selectedVehicle is CarController driven && Usable(driven))
            {
                return IsHeavyTruck(driven) ? driven : null;   // at the wheel of something else: that is the vehicle they have
            }
            Vector3 from = gm.playerController.transform.position;
            CarController best = null;
            float bestSqr = within * within;
            List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < own.Count; i++)
            {
                if (!(own[i] is CarController car) || !car.isActiveAndEnabled || car.vehicleInstance == null || !GoingPublic.MineVehicle(car) || !Usable(car))
                {
                    continue;
                }
                float sqr = (car.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr && IsHeavyTruck(car))
                {
                    bestSqr = sqr;
                    best = car;
                }
            }
            return best;
        }

        /// <summary>The car being driven, else the player's own nearest within a distance (no scooter or game job van; a hired van counts), or null.</summary>
        public static CarController NearestCar(float within)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                return null;
            }
            if (gm.selectedVehicle is CarController driven && Usable(driven))
            {
                return driven;
            }
            Vector3 from = gm.playerController.transform.position;
            CarController best = null;
            float bestSqr = within * within;
            List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < own.Count; i++)
            {
                if (!(own[i] is CarController car) || !car.isActiveAndEnabled || car.vehicleInstance == null || !GoingPublic.MineVehicle(car) || !Usable(car))
                {
                    continue;
                }
                float sqr = (car.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = car;
                }
            }
            return best;
        }
    }
}
