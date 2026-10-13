using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The tow truck that takes a wreck away once an accident scene has cleared, where until now the wreck simply went.
    /// TrafficAccidents hands over every car it would have taken off the road as "towed" (<see cref="Take"/>); this keeps the
    /// car where it stands, sends the truck (<see cref="TowTruck"/>) for it, and takes the car off the road only when the truck
    /// has driven away with it and nobody can see them. One truck at a time: the wrecks of a severe crash wait their turn.
    ///
    /// ONE VISIT. Held out of sight for TowRules.HoldLeast to HoldMost seconds (the notice that the road is open says how long
    /// it will be, TowRules.Minutes), the truck is put on a lane beside the wreck's some 170 m back, out of sight and away from
    /// the player (TowRoads), and driven up past the wreck and over into its lane ahead of it, where it stands with its light
    /// bar flashing and its hazards on. Its operator (a person of the street, ActorPool, in hi-vis) gets out, goes to the levers
    /// and lowers the bed, walks back to the wreck and hooks the cable on, then works the winch: the wreck is set back on its
    /// wheels if it lies on its side or its roof, drawn along the road to the foot of the bed and up it (TowRules.Drag and
    /// CarOnBed), and the bed comes back up with it. The operator fills in the paperwork at the cab and gets in, and the truck
    /// drives off along the lane. It is taken off the road, and the wreck with it, once nobody can see it and it is
    /// TransitSight.NearKeep or more from the player (TowRules.OutOfSight); until then it drives on.
    ///
    /// AS IT WAS, WHEREVER THIS CANNOT BE DONE. The wreck goes as it always went (taken off the road, out of sight if it is in
    /// view) when the option is off, on a guest's machine in a Going Public session (the traffic there is the host's:
    /// GoingPublic.TrafficMirrored, which TrafficTools.Ready says), when the wreck is further from the player than the street
    /// is drawn, too big for the bed, or on a road the truck cannot stand ahead of it on (TowRoads), when the truck's model is
    /// not in the bundle, and when the player goes indoors or the traffic stops while a truck is out. Nothing of it is saved:
    /// traffic is not kept in a save, so after a load the wreck is gone, as it always was.
    ///
    /// COST. Disabled while there is nothing to tow (no Update at all). While a truck is out: a timer comparison a frame,
    /// four times a second a few distance tests (and while it drives away, TransitSight's handful of rays); while it works,
    /// a handful of transform writes a frame for the bed, the cable and the wreck. Its driver runs in the physics step only
    /// while it drives (BusDriver). Nothing is allocated after the first truck is made.
    /// </summary>
    public sealed class TowService : MonoBehaviour
    {
        private const float TickEvery = 0.25f;
        private const int QueueMost = 3;              // wrecks waiting at once; any more go as they always went
        private const float RetryEvery = 4f;          // seconds between tries to put the truck on the road
        private const int Retries = 6;
        private const float DriveMost = 150f;         // seconds it may take to reach its stand before the visit is given up
        private const float StandNearEnough = 6f;     // held up with no more than this left, where it stands will do
        private const float WalkAllow = 14f;          // seconds the operator is given for any walk
        private const float OnwardMetres = 300f;
        private const float AwayMost = 240f;          // seconds driving away after which it is still only taken off out of sight (it keeps driving)
        private const float ClearMost = 90f;          // seconds a wreck that gets no truck waits to be out of sight before it goes anyway
        private const float HookOut = 0.70f;          // metres beyond the wreck's near end the operator kneels at the hook
        private const float PanelStand = 0.46f;       // metres out from the knobs the operator stands (pump_panel: the panel 0.50 ahead, the hand 0.16 right)
        private const float DoorOut = 0.80f;          // metres out from the cab's side the operator gets out and in
        private const float CableAbove = 0.45f;       // metres over the road the cable is hooked on to a car

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
        // The wreck's way to the bed, worked out when the winch starts: in the truck's own space.
        private Quaternion _carFrom;        // its rotation as it stood (world)
        private Quaternion _carUp;          // ...set on its wheels with the heading it had (world)
        private Vector3 _localCentre;       // the middle of its body from its own origin, in its own space
        private float _x0, _z0, _yaw0;      // where it starts on the road (across, ahead) and its heading, degrees from the truck's
        private float _load;                // its heading on the bed: 0 nose to the cab, 180 tail to it
        private float _ground;              // the road under it, in the truck's own space
        private Vector3 _onBed;             // its origin and rotation on the bed, once it is up it (the bed's own space)
        private Quaternion _onBedTurn;
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

        // ---------------------------------------------------------------- what TrafficAccidents asks

        /// <summary>
        /// Whether a truck would be sent for this car if it were handed over now: the option is on, the traffic is this
        /// machine's (never a guest's of Going Public), the street is running, the car fits the bed, and there is room in the queue.
        /// The road is looked at by Take. Two field reads and a few comparisons.
        /// </summary>
        public bool Accepts(VehicleComponent car)
        {
            return Settings.TowTrucks && !_failed && !GoingPublic.TrafficMirrored && TrafficTools.Ready && car != null && car.rb != null
                   && _queue.Count < QueueMost && (_job == null || _job.Car != car) && TowTruck.Available;
        }

        /// <summary>
        /// Takes a wreck the accident scene would have taken off the road, with the renderers of the driver it shows in its
        /// cab (hidden again: the driver is out of it). The car must be held out of traffic already, unpinned. False if no
        /// truck can be sent for it: the caller then takes it off the road as it always has. <paramref name="minutes"/>, for
        /// the notice, is how long the truck will be, by the game's clock (0 if it has to wait for another wreck first).
        /// </summary>
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
                    Logger?.Info("Tow: '" + TrafficTools.Name(car) + "' is " + (halfLength * 2f).ToString("F1") + " m long: too big for the bed; it goes as before.");
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
                Logger?.Info("Tow: '" + TrafficTools.Name(car) + "' is waiting for the tow truck" + (first ? ", held " + job.Hold.ToString("F0") + " s, about " + minutes + " min" : " (" + _queue.Count + " in the queue)") + ".");
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
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
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

        // ---------------------------------------------------------------- the visit

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

        private void Update()
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

        // The next wreck in the queue, or nothing more to do.
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

        // The truck on the road at the start of its route, out of sight, and away.
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
            Logger?.Info("Tow check (stand): the truck stands " + short_.ToString("F1") + " m short of its stand (wanted under " + StandNearEnough.ToString("F0") + "): " + (short_ < StandNearEnough + 0.5f ? "PASS" : "FAIL"));
            if (_operator.Enter(Door(), ActorLook.Civilian, 2f, onRoad: true))
            {
                _operator.HiVis(true);
                _operator.Go(PanelSpot(), Pace.Walk, _truck.transform.TransformPoint(new Vector3(_side * TowTruck.PanelOut, 1f, TowTruck.PanelAhead)), WalkAllow);
            }
            Go(Step.Out);
        }

        // The operator walks to the wreck's near end and kneels there, hooking the cable on; the cable runs out with them.
        private void BeginHook(float now)
        {
            if (_job == null || _job.Car == null)
            {
                Leave(now);
                return;
            }
            Transform truck = _truck.transform;
            TrafficTools.Footprint(_job.Car, out Vector3 centre, out _, out float halfLength, out _);
            Vector3 towards = truck.forward;
            Vector3 hook = centre + towards * (halfLength + HookOut);
            hook.y = truck.position.y;
            _hookedFrom = 0f;
            _operator.Go(hook, Pace.Walk, centre, WalkAllow);
            _operator.Kneel();
            Go(Step.Hooking);
        }

        // The winch starts: the wreck's way to the bed worked out once, in the truck's own space.
        private void BeginWinch(float now)
        {
            if (_job == null || _job.Car == null)
            {
                Leave(now);
                return;
            }
            Transform truck = _truck.transform;
            Transform car = _job.Car.transform;
            TrafficTools.Footprint(_job.Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            _job.Centre = centre;
            _job.HalfLength = halfLength;
            _job.HalfWidth = halfWidth;
            _localCentre = car.InverseTransformPoint(centre);
            Vector3 local = truck.InverseTransformPoint(centre);
            Vector3 heading = truck.InverseTransformDirection(forward);
            _x0 = local.x;
            _z0 = local.z;
            _yaw0 = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
            _load = TowRules.LoadHeading(_yaw0);
            _ground = CitySpots.Ground(centre, out float y) ? truck.InverseTransformPoint(new Vector3(centre.x, y, centre.z)).y : 0f;
            _carFrom = car.rotation;
            _carUp = truck.rotation * Quaternion.Euler(0f, _yaw0, 0f);
            _operator.Go(PanelSpot(), Pace.Walk, truck.TransformPoint(new Vector3(_side * TowTruck.PanelOut, 1f, TowTruck.PanelAhead)), WalkAllow);
            _operator.Work(Clip("pump_panel"));
            bool upright = Vector3.Dot(car.up, Vector3.up) > 0.85f;
            Logger?.Info("Tow check (winch): the wreck stands " + (_z0 - TowRules.FootAhead(halfLength)).ToString("F1") + " m from the foot of the bed along the truck (wanted 0 or less: drawn forward), "
                         + _x0.ToString("F1") + " m across, " + _yaw0.ToString("F0") + " degrees round, " + (upright ? "on its wheels" : "on its side or roof") + ": "
                         + (_z0 <= TowRules.FootAhead(halfLength) + 0.5f ? "PASS" : "FAIL"));
            if (upright)
            {
                BeginDrag();
            }
            else
            {
                _rightFrom = car.rotation;
                _rightMiddle = centre;
                _rightTo = truck.TransformPoint(new Vector3(_x0, _ground, _z0)) + Vector3.up * HeightOfMiddle();
                Go(Step.Righting, TowRules.RightSeconds);
            }
        }

        // The pull along the road, from the pose it has now: what the curve's start leaves over (a car standing a little
        // askew of the road under it) is eased out over the pull, so that it starts exactly where it stands.
        private void BeginDrag()
        {
            Transform car = _job.Car.transform;
            _carFrom = car.rotation;
            _dragOff = car.TransformPoint(_localCentre) - (_truck.transform.TransformPoint(new Vector3(_x0, _ground, _z0)) + Vector3.up * HeightOfMiddle());
            float foot = TowRules.FootAhead(_job.HalfLength);
            float reach = Mathf.Sqrt(_x0 * _x0 + (foot - _z0) * (foot - _z0));
            Go(Step.Dragging, TowRules.PullSeconds(reach, TowRules.DragPace));
        }

        private Quaternion _rightFrom;      // a car on its side or roof: its rotation as it lay...
        private Vector3 _rightMiddle;       // ...its middle...
        private Vector3 _rightTo;           // ...and its middle once it is back on its wheels
        private Vector3 _dragOff;           // what the pull eases out over its course

        private void EndPull(float now)
        {
            switch (_step)
            {
                case Step.Righting:
                    BeginDrag();
                    break;
                case Step.Dragging:
                    float climb = TowRules.RestAlong(_job.HalfLength) - TowRules.FootAlong(_job.HalfLength);
                    Go(Step.Climbing, TowRules.PullSeconds(climb, TowRules.ClimbPace));
                    break;
                case Step.Climbing:
                    // On the bed: from now on it is carried as it stands on it.
                    PoseOnBed(1f);
                    Transform bed = _truck.Bed;
                    Transform car = _job.Car.transform;
                    _onBed = bed.InverseTransformPoint(car.position);
                    _onBedTurn = Quaternion.Inverse(bed.rotation) * car.rotation;
                    _carried = true;
                    _truck.Cable(false, Vector3.zero);
                    Go(Step.Raising, TowRules.UpSeconds);
                    break;
                case Step.Raising:
                    _truck.Deck(0f);
                    Carry();
                    _paperFrom = 0f;
                    Transform truck = _truck.transform;
                    Vector3 door = Door();
                    _operator.Go(door, Pace.Walk, truck.TransformPoint(new Vector3(0f, 1.2f, TowTruck.DoorAhead)), WalkAllow);
                    _operator.Work(SceneClips.Clipboard);
                    Go(Step.Paperwork);
                    break;
            }
        }

        // Every frame while it works: the bed, the cable and the wreck where the step and its share of time put them.
        private void Animate(float now)
        {
            if (_truck == null || !_truck.Out)
            {
                return;
            }
            float u = _stepFor > 0f ? Mathf.Clamp01((now - _stepAt) / _stepFor) : 1f;
            switch (_step)
            {
                case Step.Lowering:
                    _truck.Deck(u);
                    break;
                case Step.Hooking:
                    if (_operator.Present)
                    {
                        _truck.Cable(true, _operator.Position + Vector3.up * 0.6f);
                    }
                    break;
                case Step.Righting:
                    if (_job != null && _job.Car != null)
                    {
                        // Turned back on to its wheels about its middle, which lifts a little as it goes over and comes to
                        // rest as high over the road as it stands on its wheels.
                        float s = TowRules.Smooth(u);
                        Quaternion turn = Quaternion.Slerp(_rightFrom, _carUp, s);
                        Vector3 middle = Vector3.Lerp(_rightMiddle, _rightTo, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI) * 0.35f);
                        _job.Car.transform.SetPositionAndRotation(middle - turn * _localCentre, turn);
                        _truck.Cable(true, Hook());
                    }
                    break;
                case Step.Dragging:
                    if (_job != null && _job.Car != null)
                    {
                        float foot = TowRules.FootAhead(_job.HalfLength);
                        TowRules.Drag(TowRules.Smooth(u), _x0, _z0, _yaw0, 0f, foot, _load, out float x, out float z, out float yaw);
                        PoseOnRoad(x, z, yaw, u);
                        _truck.Cable(true, Hook());
                    }
                    break;
                case Step.Climbing:
                    if (_job != null && _job.Car != null)
                    {
                        PoseOnBed(TowRules.Smooth(u));
                        _truck.Cable(true, Hook());
                    }
                    break;
                case Step.Raising:
                    _truck.Deck(1f - u);
                    break;
            }
        }

        // The wreck on the road behind the bed: its middle at (across, ahead) in the truck's space on the road, turned so; its
        // tilt as it stood easing out to level over the pull.
        private void PoseOnRoad(float x, float z, float yaw, float u)
        {
            Transform truck = _truck.transform;
            Quaternion level = truck.rotation * Quaternion.Euler(0f, yaw, 0f);
            Quaternion stood = _carFrom * Quaternion.Inverse(_carUp);   // what is left of its tilt as it stood (none on a level road)
            Quaternion turn = Quaternion.Slerp(stood, Quaternion.identity, TowRules.Smooth(u)) * level;
            float height = Mathf.Lerp(_ground, 0f, TowRules.Smooth(u));
            Vector3 middle = truck.TransformPoint(new Vector3(x, height, z)) + Vector3.up * HeightOfMiddle() + _dragOff * (1f - TowRules.Smooth(u));
            _job.Car.transform.SetPositionAndRotation(middle - turn * _localCentre, turn);
        }

        // The wreck going up the bed: share s of the way from the foot to its place on it.
        private void PoseOnBed(float s)
        {
            float along = Mathf.Lerp(TowRules.FootAlong(_job.HalfLength), TowRules.RestAlong(_job.HalfLength), s);
            TowRules.CarOnBed(along, _job.HalfLength, out float up, out float ahead, out float pitch);
            Transform truck = _truck.transform;
            Quaternion turn = truck.rotation * Quaternion.Euler(-pitch, 0f, 0f) * Quaternion.Euler(0f, _load, 0f);
            Vector3 point = truck.TransformPoint(new Vector3(0f, up, ahead));
            Vector3 middle = point + turn * new Vector3(0f, HeightOfMiddle(), 0f);
            _job.Car.transform.SetPositionAndRotation(middle - turn * _localCentre, turn);
        }

        // The middle of the car's body over the line its wheels run on: its own origin is on the ground under it, so the
        // middle stands as high over the origin as it does in the car's own space.
        private float HeightOfMiddle()
        {
            return _localCentre.y;
        }

        // The end of the wreck the cable is hooked to: the one nearer the truck, a little over the road.
        private Vector3 Hook()
        {
            Transform car = _job.Car.transform;
            Vector3 centre = car.TransformPoint(_localCentre);
            Vector3 towards = _truck.transform.position - centre;
            Vector3 end = car.forward * (Vector3.Dot(car.forward, towards) >= 0f ? 1f : -1f);
            return centre + end * _job.HalfLength - car.up * (_localCentre.y - CableAbove);
        }

        // While it is carried, every frame after the truck has moved: the wreck where it stands on the bed.
        private void LateUpdate()
        {
            if (_carried)
            {
                Carry();
            }
        }

        private void Carry()
        {
            if (_job == null || _job.Car == null || _truck == null)
            {
                return;
            }
            Transform bed = _truck.Bed;
            _job.Car.transform.SetPositionAndRotation(bed.TransformPoint(_onBed), bed.rotation * _onBedTurn);
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
            if (_truck != null)
            {
                _driver.Forget();
                _truck.Hide();
            }
            _operator.Exit();
            _seenBeacon = false;
            _awaySaid = false;
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
            if (_truck != null)
            {
                _driver.Forget();
                _truck.Hide();
            }
            _operator.Exit();
            _seenBeacon = false;
            _awaySaid = false;
            Go(Step.Idle);
            enabled = false;
        }

        // ---------------------------------------------------------------- places by the truck

        // Where the operator gets out and in: beside the cab's door on their side, on the road.
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

        // ---------------------------------------------------------------- the test

        /// <summary>
        /// Options > Tests: a traffic car near the player is stopped where it stands and a tow truck sent for it at once, as
        /// if it were the wreck of an accident just cleared. Says why not if it cannot.
        /// </summary>
        public void Test()
        {
            const string test = "Tow truck";
            const int reach = 120;
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Settings.TowTrucks)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why == TestRefusal.None && (Busy || TrafficAccidents.IsActive))
                {
                    why = TestRefusal.SceneActive;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, test, why);
                    return;
                }
                if (!TowTruck.Available || _failed)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_test_tow_no_model", "the tow truck's model is not in the prop bundle");
                    return;
                }
                VehicleComponent car = NearCar(25f, reach);
                if (car == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NoMovingCar, reach);
                    return;
                }
                Manager.SetVehicleIgnored(car.GetIndex(), true);
                if (!Take(car, null, out int minutes, hold: 2f))
                {
                    TrafficTools.ReturnToTraffic(car, "the tow truck test: no road for the truck");
                    TestRefusals.Tell(Logger, test, TestRefusal.NoRoad, reach, _plan.Refusal);
                    return;
                }
                Logger?.Info("Tow test: '" + TrafficTools.Name(car) + "' stopped where it stood; the truck comes in about " + minutes + " min.");
                TellComing(minutes, 1);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // An ordinary traffic car between these distances of the player that the mod holds for nothing else.
        private static VehicleComponent NearCar(float least, float most)
        {
            List<VehicleComponent> cars = Manager.GetVehicleList();
            Vector3 player = PlayerHelper.GetPosition();
            VehicleComponent best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; cars != null && i < cars.Count; i++)
            {
                VehicleComponent car = cars[i];
                if (car == null || !car.gameObject.activeInHierarchy || car.presetPath != null || TrafficTools.IsAmbulance(car) || car.GetComponent<TaxiController>() != null
                    || TrafficManager.Instance.IsVehicleIgnored(car.GetIndex()) || car.rb == null)
                {
                    continue;
                }
                float sqr = (car.transform.position - player).sqrMagnitude;
                if (sqr < least * least || sqr > most * most || sqr >= bestSqr)
                {
                    continue;
                }
                bestSqr = sqr;
                best = car;
            }
            return best;
        }
    }
}
