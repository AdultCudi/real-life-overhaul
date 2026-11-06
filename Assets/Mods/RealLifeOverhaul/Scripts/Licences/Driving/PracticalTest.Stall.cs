using System;
using System.Globalization;
using System.Text;
using UI;
using UI.Notification;
using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A drive that cannot start. Three things were found to leave the school's car where it stood and the candidate pressing the
    /// accelerator: the examiner who never reached the car (the test waits for them to be aboard before it begins), a game whose time is
    /// stopped (paused, or its time controls left locked by a map or a mode that closed without letting them go), and the car itself held
    /// (a body fixed in place, a handbrake, an engine off, an empty tank, a car wedged against something). This watches the real clock
    /// a few times a second while a test is on, writes why in the log and puts right what can be, and when nothing helps calls the
    /// drive off with the fee returned, so a player is never left sitting in a car that will not go.
    /// COST: nothing while no test is on; while one is on, a time comparison a frame and a few reads four times a second.
    /// </summary>
    internal sealed partial class PracticalTest
    {
        private const float WatchEvery = 0.25f;   // real seconds between looks
        private const float WatchHitch = 1f;      // real seconds at most one look counts for: a long frame is not held against the car
        private const float Lift = 0.3f;          // metres a car that nothing else explains is lifted, to leave whatever it sits on or against

        private readonly TestStallRules _stall = new TestStallRules();
        private float _nextWatch, _lastWatch, _seatedFor, _frozenSince = -1f, _frozenTold = -100f;
        private bool _stalledSaid;

        // A test begins: the watch starts afresh.
        private void WatchBegin()
        {
            _stall.Begin();
            _nextWatch = 0f;
            _lastWatch = Time.unscaledTime;
            _seatedFor = 0f;
            _frozenSince = -1f;
            _frozenTold = -100f;
            _stalledSaid = false;
        }

        // Once a frame while a test is on: a comparison, and four times a second the looks below.
        private void Watch()
        {
            float real = Time.unscaledTime;
            if (real < _nextWatch)
            {
                return;
            }
            _nextWatch = real + WatchEvery;
            float seconds = Mathf.Min(real - _lastWatch, WatchHitch);
            _lastWatch = real;
            if (_step != Step.Meet && _step != Step.Drive && _step != Step.Park)
            {
                return;
            }
            if (!Driving)
            {
                _seatedFor = 0f;
                _frozenSince = -1f;
                return;
            }
            if (TestBoardRules.Frozen(Time.timeScale))
            {
                WatchFrozen(real);   // nothing is held against a car that cannot move
                return;
            }
            _frozenSince = -1f;
            _seatedFor += seconds;
            if (_step == Step.Meet)
            {
                WatchBoard();
            }
            WatchStall(seconds);
        }

        // The candidate is at the wheel and the examiner has not come aboard: they could not reach the door, so they are taken to be in.
        private void WatchBoard()
        {
            JobCast cast = _cast();
            if (_examiner == null || cast == null || !TestBoardRules.SeatNow(true, _seatedFor, _aboard, true))
            {
                return;
            }
            _logger?.Warn("Licences: the examiner had not reached the car " + _seatedFor.ToString("F0", CultureInfo.InvariantCulture)
                          + " s after the candidate took the wheel (arrived " + (_examiner.HasArrived ? "yes" : "no") + ", "
                          + JobSpots.Flat(_examiner.Position, _boardSpot).ToString("F1", CultureInfo.InvariantCulture) + " m from the door); they are taken to be aboard.");
            _examiner.EndClips();
            JobCast.DoorSound(_boardSpot);
            cast.Seat(_examiner);
            _aboard = true;
        }

        // Time has stood still for a few seconds with the candidate at the wheel.
        private void WatchFrozen(float real)
        {
            if (_frozenSince < 0f)
            {
                _frozenSince = real;
                return;
            }
            if (real - _frozenSince < TestBoardRules.FrozenFor)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            if (speed != null && TestBoardRules.LockLeft(speed.isTimeControlDisabled, CityMap.IsOpen, UI.Smartphone.FullMenu.IsOpen || UI.MiniMenu.MiniMenu.IsOpen
                    || (uis.playerActivityUI != null && uis.playerActivityUI.ShouldBePaused),
                    uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen))
            {
                _logger?.Warn("Licences check (the car moves): FAIL: time had stood still for " + (real - _frozenSince).ToString("F0", CultureInfo.InvariantCulture)
                              + " s with the time controls locked and no map, menu or panel open; the lock is let go and time is put back as the player left it.");
                speed.DisableTimeControl(false);
                speed.Reset();
                _frozenSince = real;
                return;
            }
            if (real - _frozenTold >= TestBoardRules.FrozenTold)
            {
                _frozenTold = real;
                Licences.Toast(NotificationType.Warning, "reallife_lic_test_paused", 8f);
                _logger?.Info("Licences: the game is paused with the candidate at the wheel; the car cannot move until time runs again.");
            }
        }

        // The accelerator held and the car not going.
        private void WatchStall(float seconds)
        {
            NwhVehicle vehicle = _car != null ? _car.vehicleController : null;
            if (vehicle == null || vehicle.input == null || _body == null)
            {
                return;
            }
            Vector3 velocity = _body.velocity;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            Vector3 at = _body.position;
            float pedal = Mathf.Max(vehicle.input.Throttle, vehicle.input.InputSwappedThrottle);
            bool went = _stall.TookOff;
            StallStage stage = _stall.Look(seconds, pedal, speed, at.x, at.z);
            if (!went && _stall.TookOff && (Meta.Dev.Verbose || _free))   // a test button's drive always says
            {
                _logger?.Info("Licences check (the car moves): PASS: the school's car set off " + _stall.SetOffAfter.ToString("F1", CultureInfo.InvariantCulture)
                              + " s after the candidate took the wheel, at " + (speed * 3.6f).ToString("F0", CultureInfo.InvariantCulture) + " km/h.");
            }
            if (stage == StallStage.Mend)
            {
                MendStuck(vehicle, pedal);
            }
            else if (stage == StallStage.GiveUp)
            {
                _logger?.Warn("Licences check (the car moves): FAIL: still no travel after " + _stall.Held.ToString("F0", CultureInfo.InvariantCulture)
                              + " s of accelerator and the mend; the drive is called off and the fee goes back.");
                Abort("reallife_lic_test_car_stuck", refund: true);
            }
        }

        // Says why in the log, in one line, and puts right what can be put right.
        private void MendStuck(NwhVehicle vehicle, float pedal)
        {
            var said = new StringBuilder(384);
            var mended = new StringBuilder(128);
            Rigidbody body = _body;
            said.Append("accelerator ").Append(pedal.ToString("F2", CultureInfo.InvariantCulture)).Append(" held ").Append(_stall.Held.ToString("F0", CultureInfo.InvariantCulture))
                .Append(" s, time scale ").Append(Time.timeScale.ToString("F2", CultureInfo.InvariantCulture)).Append(", body ").Append(body.isKinematic ? "KINEMATIC" : "free")
                .Append(' ').Append(body.constraints).Append(body.IsSleeping() ? ", asleep" : string.Empty).Append(", mass ").Append(body.mass.ToString("F0", CultureInfo.InvariantCulture));
            if (body.isKinematic)
            {
                body.isKinematic = false;
                mended.Append(" body let free;");
            }
            if (body.constraints != RigidbodyConstraints.None)
            {
                body.constraints = RigidbodyConstraints.None;
                mended.Append(" constraints cleared;");
            }
            if (body.IsSleeping())
            {
                body.WakeUp();
                mended.Append(" body woken;");
            }
            float handbrake = vehicle.input.Handbrake;
            said.Append(", handbrake ").Append(handbrake.ToString("F2", CultureInfo.InvariantCulture));
            if (handbrake > 0.05f)
            {
                vehicle.input.Handbrake = 0f;
                mended.Append(" handbrake released;");
            }
            NWH.VehiclePhysics2.Powertrain.EngineComponent engine = vehicle.powertrain != null ? vehicle.powertrain.engine : null;
            if (engine != null)
            {
                said.Append(", engine ").Append(engine.IsRunning ? "running" : "NOT running").Append(engine.IsStalled ? " (stalled)" : string.Empty)
                    .Append(engine.canRun ? string.Empty : " (cannot run)");
                if (!engine.IsRunning && engine.canRun)
                {
                    engine.StartEngine();
                    mended.Append(" engine started;");
                }
            }
            if (vehicle.powertrain != null && vehicle.powertrain.transmission != null)
            {
                said.Append(", gear ").Append(vehicle.powertrain.transmission.Gear);
            }
            Vehicles.VehicleTypes.VehicleType type = _car.vehicleType;
            if (_car.vehicleInstance != null && type != null)
            {
                said.Append(", fuel ").Append(_car.vehicleInstance.fuel.ToString("F1", CultureInfo.InvariantCulture)).Append(" of ").Append(type.maxFuel.ToString("F0", CultureInfo.InvariantCulture));
                if (_car.vehicleInstance.fuel <= 0.01f)
                {
                    _car.vehicleInstance.fuel = type.maxFuel;
                    mended.Append(" tank filled;");
                }
            }
            if (mended.Length == 0)
            {
                // Nothing wrong that can be seen: it may sit on or against something. Lifted clear and woken.
                body.position += Vector3.up * Lift;
                body.WakeUp();
                mended.Append(" lifted ").Append(Lift.ToString("F1", CultureInfo.InvariantCulture)).Append(" m;");
            }
            if (!_stalledSaid)
            {
                _stalledSaid = true;
                _logger?.Warn("Licences check (the car moves): FAIL: " + said + "; put right:" + mended + " the drive goes on and is watched again.");
            }
        }
    }
}
