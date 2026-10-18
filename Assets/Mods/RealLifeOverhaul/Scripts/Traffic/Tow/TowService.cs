using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The tow truck that takes a wreck away once an accident scene has cleared (<see cref="Take"/>): the truck
    /// (<see cref="TowTruck"/>) comes up a lane beside the wreck out of sight (TowRoads), stands ahead of it, and its operator
    /// winches it onto the bed (TowRules); it drives off and is removed with the wreck once unseen. One at a time; where it
    /// cannot be done the wreck goes as before, and nothing is saved. Cost: disabled when idle; out, a timer a frame and a few
    /// distance tests four times a second, and a few transform writes a frame while it works.
    /// </summary>
    public sealed partial class TowService : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "TowService", typeof(TowService));

        private const float TickEvery = 0.25f;        // seconds
        private const int QueueMost = 3;              // wrecks waiting at once; any more go as they always went
        private const float RetryEvery = 4f;          // seconds between tries to put the truck on the road
        private const int Retries = 6;                // tries to put the truck on the road
        private const float DriveMost = 150f;         // seconds it may take to reach its stand before the visit is given up
        private const float StandNearEnough = 6f;     // metres: held up with no more than this left, where it stands will do
        private const float WalkAllow = 14f;          // seconds the operator is given for any walk
        private const float OnwardMetres = 300f;      // metres of road it is given at a time to drive away along
        private const float AwayMost = 240f;          // seconds driving away after which it is still only taken off out of sight (it keeps driving)
        private const float ClearMost = 90f;          // seconds a wreck that gets no truck waits to be out of sight before it goes anyway
        private const float PanelStand = 0.46f;       // metres out from the knobs the operator stands (pump_panel: the panel 0.50 ahead, the hand 0.16 right)
        private const float DoorOut = 0.80f;          // metres out from the cab's side the operator gets out and in

        private enum Step { Idle, Holding, Driving, Out, Lowering, Hooking, Righting, Dragging, Climbing, Raising, Paperwork, Boarding, Leaving }

        public static IModLogger Logger;
        public static TowService Instance { get; private set; }

        private sealed class Job
        {
            public VehicleComponent Car;
            public readonly List<Renderer> Seat = new List<Renderer>(4);
            public readonly List<int> Preset = new List<int>();     // marks the car as ours (TrafficAccidents.Recheck leaves it be)
            public AiCarRescueCheck Rescue;
            public RigidbodyInterpolation Interpolation;
            public float Since;
            public float HalfLength, HalfWidth;
            public Vector3 Centre;          // the middle of its body, as it was handed over
            public Vector3 Forward;
            public float Hold;              // seconds it was to be held out of sight
            public bool Seeded;             // the plan was found when it was handed over
        }

        private readonly List<Job> _queue = new List<Job>(QueueMost);
        private readonly TowRoads.Plan _plan = new TowRoads.Plan();
        private readonly List<Vector3> _onward = new List<Vector3>(200);
        private readonly ActorRole _operator = new ActorRole();
        private TowTruck _truck;
        private BusDriver _driver;
        private bool _failed;               // the truck could not be made: nothing is taken any more this session
        private Job _job;                   // the wreck the truck is out for
        private Step _step = Step.Idle;
        private float _stepAt;              // when the current step began
        private float _stepFor;             // ...and how long it lasts
        private float _nextTick;
        private float _holdUntil;
        private int _tries;
        private int _side;                  // the side the operator works on: +1 right, -1 left
        private bool _seen;
        private bool _carried;              // the wreck is on the bed and goes where the bed goes

        /// <summary>True while a truck is out or a wreck waits for one.</summary>
        public bool Busy => _queue.Count > 0 || _step != Step.Idle;

        private void Awake()
        {
            Instance = this;
            enabled = false;
        }

        private void OnDestroy()
        {
            try
            {
                EndAll("the city was unloaded");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Whether a truck would be sent for this car now: the option on, this machine's traffic, the street running, room in the queue.</summary>
        public bool Accepts(VehicleComponent car)
        {
            return Settings.TowTrucks && !_failed && !GoingPublic.TrafficMirrored && TrafficTools.Ready && car != null && car.rb != null
                   && _queue.Count < QueueMost && (_job == null || _job.Car != car) && TowTruck.Available;
        }

        /// <summary>Takes a held wreck the scene would have removed; false if no truck can come. minutes: the game minutes the notice gives (0: queued).</summary>
        public bool Take(VehicleComponent car, List<Renderer> seat, out int minutes, float hold = -1f)
        {
            minutes = 0;
            try
            {
                if (!Accepts(car) || !TrafficTools.IsHeld(car))
                {
                    return false;
                }
                TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
                if (!TowRules.Fits(halfLength * 2f, halfWidth * 2f))
                {
                    Logger?.Info("Tow: '" + TrafficTools.Name(car) + "' is " + (halfLength * 2f).ToString("F1", CultureInfo.InvariantCulture) + " m long: too big for the bed; it goes as before.");
                    return false;
                }
                if (!TowRules.WorthSending(Mathf.Sqrt(CitySpots.Flat(centre - PlayerHelper.GetPosition())), TransitSight.Far))
                {
                    return false;
                }
                bool first = _queue.Count == 0 && _step == Step.Idle;
                if (first && !TowRoads.Find(centre, forward, halfLength, halfWidth, car.rb, _plan))
                {
                    Logger?.Info("Tow: no truck for '" + TrafficTools.Name(car) + "': " + _plan.Refusal + "; it goes as before.");
                    return false;
                }
                var job = new Job
                {
                    Car = car,
                    Since = Time.time,
                    Centre = centre,
                    Forward = forward,
                    HalfLength = halfLength,
                    HalfWidth = halfWidth,
                    Hold = hold >= 0f ? hold : Random.Range(TowRules.HoldLeast, TowRules.HoldMost),
                    Seeded = first
                };
                Hold(job, seat);
                _queue.Add(job);
                if (first)
                {
                    float metres = TowRoads.PickStart(_plan) ? _plan.Metres : 200f;   // picked again when it sets off: the player may have moved
                    minutes = TowRules.Minutes(TowRules.ArrivalSeconds(job.Hold, metres), PlayerPrefSettings.GameSpeed);
                    BeginHold(job);
                }
                enabled = true;
                Logger?.Info("Tow: '" + TrafficTools.Name(car) + "' is waiting for the tow truck"
                    + (first ? ", held " + job.Hold.ToString("F0") + " s, about " + minutes + " min" : " (" + _queue.Count + " in the queue)") + ".");
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // The car is ours until the truck has taken it away: frozen where it stands, out of traffic, its driver not shown.
        private static void Hold(Job job, List<Renderer> seat)
        {
            VehicleComponent car = job.Car;
            Rigidbody body = car.rb;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;          // (a frozen body takes no velocity: the engine warns of it)
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            job.Interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;    // it is posed by hand while it is drawn and carried
            job.Rescue = TrafficTools.SuspendRescue(car);
            car.presetPath = job.Preset;
            job.Seat.Clear();
            if (seat != null)
            {
                for (int i = 0; i < seat.Count; i++)
                {
                    if (seat[i] != null)
                    {
                        seat[i].forceRenderingOff = true;
                        job.Seat.Add(seat[i]);
                    }
                }
            }
            TrafficTools.Touch(car, "held for the tow truck");
        }

        // The car goes back to the pool as a car the pool can hand out again: as TrafficTools.Remove leaves it, its driver shown.
        private static void Let(Job job, string why)
        {
            VehicleComponent car = job.Car;
            for (int i = 0; i < job.Seat.Count; i++)
            {
                if (job.Seat[i] != null)
                {
                    job.Seat[i].forceRenderingOff = false;
                }
            }
            job.Seat.Clear();
            if (car == null)
            {
                return;
            }
            if (job.Rescue != null)
            {
                job.Rescue.enabled = true;
            }
            if (car.rb != null)
            {
                car.rb.interpolation = job.Interpolation;
            }
            if (car.presetPath == job.Preset)
            {
                car.presetPath = null;
            }
            if (TrafficTools.IsHeld(car))
            {
                TrafficTools.Remove(car, why);
            }
        }

        private void BeginHold(Job job)
        {
            _job = job;
            _queue.Remove(job);
            _tries = 0;
            _holdUntil = Time.time + job.Hold;
            Go(Step.Holding, job.Hold);
        }

        private void Go(Step step, float lasts = 0f)
        {
            _step = step;
            _stepAt = Time.time;
            _stepFor = lasts;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: TowService.Update");
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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            try
            {
                Animate(now);
                if (now < _nextTick)
                {
                    return;
                }
                _nextTick = now + TickEvery;
                Tick(now);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                EndAll("an error");
                _failed = true;   // a fault is not repeated: the wrecks go as they always went for the rest of the session
            }
        }

        private void Tick(float now)
        {
            if (!Settings.TowTrucks || !TrafficTools.Ready)
            {
                EndAll(!Settings.TowTrucks ? "the option was switched off" : "the traffic stopped (indoors, the fast forward, or a guest of Going Public)");
                return;
            }
            _operator.Tick(now);
            if (_clearing.Count > 0)
            {
                TickClearing(now);
            }
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (!TrafficTools.IsHeld(_queue[i].Car))
                {
                    Logger?.Info("Tow: a wreck waiting for the truck was taken off the road by the game; it is forgotten.");
                    _queue.RemoveAt(i);
                }
            }
            if (_job != null && !TrafficTools.IsHeld(_job.Car) && _step < Step.Leaving)
            {
                Logger?.Info("Tow: the wreck the truck was out for is gone; the truck drives off.");
                _job.Car = null;
                _carried = false;
                if (_step == Step.Holding)
                {
                    Next();
                    return;
                }
                Leave(now);
            }
            if (_step == Step.Hooking && _operator.Settled && _hookedFrom <= 0f)
            {
                _hookedFrom = now;   // kneeling at the wreck: the hooking is timed from here
            }
            if (_step == Step.Paperwork && _operator.Settled && _paperFrom <= 0f)
            {
                _paperFrom = now;
            }
            switch (_step)
            {
                case Step.Idle:
                    Next();
                    break;
                case Step.Holding:
                    if (now >= _holdUntil)
                    {
                        Dispatch(now);
                    }
                    break;
                case Step.Driving:
                    TickDriving(now);
                    break;
                case Step.Out:
                    if (_operator.Settled || now - _stepAt > WalkAllow || !_operator.Present)
                    {
                        _operator.Work(Clip("pump_panel"));
                        _operator.Face(_truck.transform.TransformPoint(new Vector3(_side * TowTruck.PanelOut, 1f, TowTruck.PanelAhead)));
                        Go(Step.Lowering, TowRules.DownSeconds);
                    }
                    break;
                case Step.Lowering:
                    if (now - _stepAt >= _stepFor)
                    {
                        _truck.Deck(1f);
                        BeginHook(now);
                    }
                    break;
                case Step.Hooking:
                    if ((_hookedFrom > 0f && now - _hookedFrom >= TowRules.HookSeconds) || now - _stepAt > WalkAllow + TowRules.HookSeconds || !_operator.Present)
                    {
                        BeginWinch(now);
                    }
                    break;
                case Step.Righting:
                case Step.Dragging:
                case Step.Climbing:
                case Step.Raising:
                    if (now - _stepAt >= _stepFor)
                    {
                        EndPull(now);
                    }
                    break;
                case Step.Paperwork:
                    if ((_paperFrom > 0f && now - _paperFrom >= TowRules.StrapSeconds) || now - _stepAt > WalkAllow + TowRules.StrapSeconds || !_operator.Present)
                    {
                        _operator.Board(Door(), Pace.Walk, 8f);
                        Go(Step.Boarding);
                    }
                    break;
                case Step.Boarding:
                    if (!_operator.Present || now - _stepAt > 10f)
                    {
                        _operator.Exit();
                        Leave(now);
                    }
                    break;
                case Step.Leaving:
                    TickLeaving(now);
                    break;
            }
        }

        private float _hookedFrom;
        private float _paperFrom;

        private void Next()
        {
            _job = null;
            _carried = false;
            Go(Step.Idle);
            while (_queue.Count > 0)
            {
                Job job = _queue[0];
                if (!TrafficTools.IsHeld(job.Car))
                {
                    _queue.RemoveAt(0);
                    continue;
                }
                TrafficTools.Footprint(job.Car, out job.Centre, out job.Forward, out job.HalfLength, out job.HalfWidth);
                if (!TowRoads.Find(job.Centre, job.Forward, job.HalfLength, job.HalfWidth, job.Car.rb, _plan))
                {
                    Logger?.Info("Tow: no truck for the next wreck: " + _plan.Refusal + "; it goes as before, out of sight.");
                    _queue.RemoveAt(0);
                    Clear(job);
                    return;
                }
                job.Hold = Random.Range(4f, 9f);   // the truck is on its way back already
                BeginHold(job);
                return;
            }
            Rest();
        }

        // Nothing out and nothing waiting: no Update at all until the next wreck.
        private void Rest()
        {
            if (_step == Step.Idle && _queue.Count == 0 && _clearing.Count == 0 && (_truck == null || !_truck.Out))
            {
                enabled = false;
            }
        }

        // A wreck that gets no truck goes as it always went: off the road, out of sight if somebody is looking.
        private readonly List<Job> _clearing = new List<Job>(QueueMost);
        private float _clearSince;

        private void Clear(Job job)
        {
            if (_clearing.Count == 0)
            {
                _clearSince = Time.time;
            }
            _clearing.Add(job);
            enabled = true;
        }

        private void TickClearing(float now)
        {
            for (int i = _clearing.Count - 1; i >= 0; i--)
            {
                Job job = _clearing[i];
                bool gone = !TrafficTools.IsHeld(job.Car);
                if (!gone)
                {
                    Vector3 at = job.Car.transform.position;
                    gone = TowRules.OutOfSight(TransitSight.Seen(at, job.Forward, job.HalfLength), Mathf.Sqrt(CitySpots.Flat(at - PlayerHelper.GetPosition())), TransitSight.NearKeep)
                           || now - _clearSince > ClearMost;
                }
                if (gone)
                {
                    Let(job, "no tow truck could come for it");
                    _clearing.RemoveAt(i);
                }
            }
            Rest();
        }

        private void Dispatch(float now)
        {
            Job job = _job;
            bool planned = job.Seeded && _plan.Back.Count > 0;
            job.Seeded = false;
            if (!planned)
            {
                TrafficTools.Footprint(job.Car, out job.Centre, out job.Forward, out job.HalfLength, out job.HalfWidth);
                planned = TowRoads.Find(job.Centre, job.Forward, job.HalfLength, job.HalfWidth, job.Car.rb, _plan);
            }
            if (planned && TowRoads.PickStart(_plan) && MakeTruck())
            {
                _seenBeacon = false;
                _truck.Place(_plan.Start, _plan.StartAlong);
                _driver.Take(_truck);
                _driver.CruiseSpeed = 10f;
                _driver.BeginRoute(_truck, _plan.Route, StandNearEnough);
                if (_driver.Active)
                {
                    Go(Step.Driving);
                    Logger?.Info("Tow: the truck set off " + _plan.Metres.ToString("F0") + " m from the wreck's stand, "
                                 + Mathf.Sqrt(CitySpots.Flat(_plan.Start - PlayerHelper.GetPosition())).ToString("F0") + " m from the player, out of sight.");
                    return;
                }
                // A route its driver cannot drive (too short to make a path of): off the road again at once, out of sight.
                _driver.Forget();
                _truck.Hide();
                planned = false;
                _plan.Refusal = "the route laid for it could not be driven";
            }
            if (++_tries >= Retries || _failed)
            {
                Logger?.Info("Tow: the truck could not be put on the road (" + (planned ? "no start out of sight" : _plan.Refusal) + "); the wreck goes as before.");
                Clear(job);
                Next();
                return;
            }
            _holdUntil = now + RetryEvery;
        }

        private bool MakeTruck()
        {
            if (_truck != null)
            {
                return true;
            }
            _truck = TowTruck.Make(transform, "RealLifeOverhaul Tow Truck");
            if (_truck == null)
            {
                Logger?.Warn("Tow: the tow truck's model is not in the prop bundle; wrecks go as before. (Logged once.)");
                _failed = true;
                return false;
            }
            _driver = _truck.gameObject.AddComponent<BusDriver>();
            return true;
        }

        private void TickDriving(float now)
        {
            float left = Vector3.Distance(_driver.Front, _plan.Stand);
            if (left < 60f && !_seenBeacon)
            {
                _seenBeacon = true;
                _truck.Beacon(true);
            }
            if (_driver.Arrived || (!_driver.Active && left < StandNearEnough + 2f))
            {
                Arrive(now);
                return;
            }
            if (now - _stepAt > DriveMost)
            {
                Logger?.Info("Tow: the truck did not reach the wreck in " + DriveMost.ToString("F0") + " s (" + left.ToString("F0") + " m short); it drives off and the wreck goes as before.");
                Clear(_job);
                _job = null;
                Leave(now);
            }
        }

        private bool _seenBeacon;

        // At its stand: hazards on, the operator out at the door on the side away from the lane it came by, to the levers.
        private void Arrive(float now)
        {
            _driver.Stop();
            _truck.Beacon(true);
            _truck.Hazards(true);
            _side = Vector3.Dot(_plan.Across, _truck.transform.right) > 0f ? -1 : 1;
            float short_ = Vector3.Dot(_plan.Stand - _driver.Front, _plan.Along);
            Logger?.Info("Tow check (stand): the truck stands " + short_.ToString("F1", CultureInfo.InvariantCulture) + " m short of its stand (wanted under "
                + StandNearEnough.ToString("F0") + "): " + (short_ < StandNearEnough + 0.5f ? "PASS" : "FAIL"));
            if (_operator.Enter(Door(), ActorLook.Civilian, 2f, onRoad: true))
            {
                _operator.HiVis(true);
                _operator.Go(PanelSpot(), Pace.Walk, _truck.transform.TransformPoint(new Vector3(_side * TowTruck.PanelOut, 1f, TowTruck.PanelAhead)), WalkAllow);
            }
            Go(Step.Out);
        }

        // Off along the lane, its light bar dark, its hazards off: taken off the road with the wreck when nobody sees it.
        private void Leave(float now)
        {
            if (_truck == null || !_truck.Out)
            {
                FinishVisit("the truck was not out");
                return;
            }
            _truck.Deck(0f);
            _truck.Cable(false, Vector3.zero);
            _truck.Hazards(false);
            _truck.Beacon(false);
            if (_job != null && _job.Car != null && !_carried)
            {
                Clear(_job);   // left where it stands: it goes as before, out of sight
                _job = null;
            }
            if (TowRoads.Onward(_plan, _driver.Front, OnwardMetres, _onward))
            {
                _driver.BeginRoute(_truck, _onward, 0f);
            }
            Go(Step.Leaving);
        }

        private void TickLeaving(float now)
        {
            _truck.Footprint(out Vector3 centre, out Vector3 forward, out float halfLength, out _);
            _seen = TransitSight.Seen(centre, forward, halfLength);
            float distance = Mathf.Sqrt(CitySpots.Flat(centre - PlayerHelper.GetPosition()));
            if (TowRules.OutOfSight(_seen, distance, TransitSight.NearKeep))
            {
                FinishVisit("driven away out of sight, " + distance.ToString("F0") + " m from the player");
                return;
            }
            if (!_driver.Active)
            {
                // The road it was given ends with somebody still looking: on along the next stretch.
                if (TowRoads.Onward(_plan, _driver.Front, OnwardMetres, _onward))
                {
                    _driver.BeginRoute(_truck, _onward, 0f);
                }
            }
            if (now - _stepAt > AwayMost && !_awaySaid)
            {
                _awaySaid = true;
                Logger?.Info("Tow: the truck has driven away for " + AwayMost.ToString("F0") + " s and is still in view or near; it drives on until it is not.");
            }
        }

        private bool _awaySaid;

        private void FinishVisit(string how)
        {
            bool towed = _job != null && _job.Car != null && _carried;
            if (_job != null)
            {
                Let(_job, towed ? "taken away by the tow truck" : "the tow truck's visit ended");
            }
            PutTruckAway();
            Logger?.Info("Tow: the truck is off the road (" + how + ")" + (towed ? " with the wreck." : "."));
            if (towed)
            {
                Notice("reallife_tow_gone", null);
            }
            Next();
        }

        /// <summary>Everything at once (indoors, the option off, the city unloading): the truck off the road, every wreck as it always went.</summary>
        private void EndAll(string why)
        {
            if (_step == Step.Idle && _queue.Count == 0 && _clearing.Count == 0)
            {
                enabled = false;
                return;
            }
            Logger?.Info("Tow: everything ended (" + why + "): the truck is off the road and the wrecks are taken away as before.");
            _carried = false;
            if (_job != null)
            {
                Let(_job, "the tow truck's visit ended: " + why);
                _job = null;
            }
            for (int i = 0; i < _queue.Count; i++)
            {
                Let(_queue[i], "the tow truck's visit ended: " + why);
            }
            _queue.Clear();
            for (int i = 0; i < _clearing.Count; i++)
            {
                Let(_clearing[i], "the tow truck's visit ended: " + why);
            }
            _clearing.Clear();
            PutTruckAway();
            Go(Step.Idle);
            enabled = false;
        }

        private void PutTruckAway()
        {
            if (_truck != null)
            {
                _driver.Forget();
                _truck.Hide();
            }
            _operator.Exit();
            _seenBeacon = false;
            _awaySaid = false;
        }

        private Vector3 Door()
        {
            return _truck.transform.TransformPoint(new Vector3(_side * (TowRules.HalfWidth + DoorOut), 0f, TowTruck.DoorAhead));
        }

        // Where the operator stands at the levers: pump_panel's hands are on a panel 0.50 m ahead, the right one 0.16 m right.
        private Vector3 PanelSpot()
        {
            return _truck.transform.TransformPoint(new Vector3(_side * (TowTruck.PanelOut + PanelStand), 0f, TowTruck.PanelAhead - 0.16f * _side));
        }

        private static string Clip(string wished)
        {
            return ClipLibrary.Has(wished) ? wished : SceneClips.Clipboard;
        }

        private static void Notice(string key, Dictionary<string, string> data)
        {
            if (Settings.AccidentNotices)
            {
                Notifications.Show(NotificationType.Info, key, data, 7f, "reallife_accident");
            }
        }

        /// <summary>The notice that the road is open again and a truck is coming: its wait in the game's minutes, and how many wrecks it comes for.</summary>
        public static void TellComing(int minutes, int wrecks)
        {
            if (minutes <= 0)
            {
                Notice("reallife_tow_queued", null);   // the truck is out for another wreck: it comes after that one
                return;
            }
            var data = new Dictionary<string, string>(1) { { "minutes", minutes.ToString() } };   // the game keeps the toast's data
            Notice(wrecks > 1 ? "reallife_tow_coming_more" : "reallife_tow_coming", data);
        }
    }
}
