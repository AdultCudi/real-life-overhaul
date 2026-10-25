using System;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What an examiner writes down.</summary>
    internal enum TestFault
    {
        RedLight,        // serious
        PastLine,        // stopped beyond the stop line at a red light
        Speeding,        // more than 10 km/h over for a few seconds
        SpeedingFar,     // serious: more than 30 over
        LeftLane,
        Pavement,        // serious
        WrongWay,        // serious
        HarshBrake,
        Touched,         // a light touch
        Collision,       // serious
        ParkedBadly,
        NotParked,       // serious
        NoReverse,       // the commercial bay was driven into forwards
        PoliceStop,      // serious: pulled over during the test
        Count
    }

    /// <summary>
    /// A driving lesson or practical test, driven in the school's car with the examiner beside the player, checkpoint by
    /// checkpoint to a box at the kerb; a lesson says each fault as it happens. Cost while one is on: ten looks a second
    /// at the car and a step five times a second; a route planning when it is booked and when it starts.
    /// </summary>
    internal sealed partial class PracticalTest
    {
        private const float TickInterval = 0.2f;       // seconds
        private const float MeetAllow = 120f;          // seconds to get into the car
        private const float TimeLimit = 600f;          // seconds for the drive
        private const float Reach = 9f;                // metres from a checkpoint that count as through it
        private const float OutOfCarAllow = 30f;       // seconds
        private const float OffRouteAllow = 40f;       // seconds

        private enum Step { None, Meet, Drive, Park, Result, Leaving }

        private static readonly bool[] Serious =
        {
            true, false, false, true, false, true, true, false, false, true, false, true, false, true
        };

        private static readonly string[] FaultKeys =
        {
            "reallife_lic_fault_redlight", "reallife_lic_fault_pastline", "reallife_lic_fault_speeding", "reallife_lic_fault_speeding_far",
            "reallife_lic_fault_lane", "reallife_lic_fault_pavement", "reallife_lic_fault_wrongway", "reallife_lic_fault_brake",
            "reallife_lic_fault_touched", "reallife_lic_fault_collision", "reallife_lic_fault_parked_badly", "reallife_lic_fault_not_parked",
            "reallife_lic_fault_no_reverse", "reallife_lic_fault_police"
        };

        private readonly TestCar _lender;
        private readonly Func<JobCast> _cast;
        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private readonly TestRoute _route = new TestRoute();
        private readonly int[] _faults = new int[(int)TestFault.Count];
        private readonly SchoolScreens _screens;
        private readonly GuideMark _mark = new GuideMark("the driving test").Ranked(GuideMarkRank.Course);   // a scene nearby never hides the checkpoint ahead
        private readonly Func<Waypoint, bool, RouteOutcome> _planFrom;   // asked by the school's car for each place it could stand
        private int _wanted;                // checkpoints asked of the route being planned
        private int _seed;                  // ...and the seed of its turns
        private bool _endWaived;            // the bay's or stop's lane closed during the drive: the parking is not marked

        private Step _step = Step.None;
        private LicenceClass _class;
        private bool _lesson;
        private bool _free;                 // from the test button: nothing charged
        private int _feePaid;
        private CarController _car;
        private Rigidbody _body;
        private StreetActor _examiner;
        private bool _aboard;
        private bool _waiting;
        private Vector3 _boardSpot;
        private Vector3 _desk;
        private int _next;
        private float _stepEnd;
        private float _nextTick;
        private float _outSince = -1f;
        private float _farSince = -1f;
        private bool _passed;
        private float _resultAt;

        public PracticalTest(TestCar lender, Func<JobCast> cast, Transform parent, IModLogger logger, SchoolScreens screens)
        {
            _screens = screens;
            _park = new DoorPanel(logger, "Licences check (parked panel)");
            _onParked = _park.Press(ParkedClicked);
            _onSheetClosed = SheetClosed;
            _planFrom = PlanFrom;
            _lender = lender;
            _cast = cast;
            _parent = parent;
            _logger = logger;
        }

        /// <summary>The component whose life keeps the marker: when it goes, so does the marker.</summary>
        internal void KeptBy(Behaviour host)
        {
            _mark.Keep(host);
        }

        public bool Active => _step != Step.None;

        /// <summary>True while the parked panel is offered: the mod's own enter-a-car key stands aside.</summary>
        public bool Prompting => _parkOffered;

        public CarController Car => Active ? _car : null;

        /// <summary>Asked before the fee is taken: whether a car and a route could be had from a desk now. False with a locale key why.</summary>
        public bool CanStage(LicenceClass kind, Vector3 desk, out string reasonKey)
        {
            reasonKey = Active ? "reallife_lic_no_busy"
                : !TrafficTools.Ready || !RoadMap.Ensure() ? "reallife_lic_no_roads"
                : null;
            if (reasonKey == null)
            {
                NewDraw();
                reasonKey = _lender.Placeable(desk, kind, _planFrom);
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Licences check (route, before the fee): " + (reasonKey == null
                    ? "a place for the car and a route from it (" + _route.Expanded + " lane points looked at): PASS."
                    : "none (" + reasonKey + "): the fee is not taken and the desk says why."));
            }
            return reasonKey == null;
        }

        // The checkpoints and the turns of the next route.
        private void NewDraw()
        {
            _wanted = UnityEngine.Random.Range(TestRoutePlanner.Least, TestRoutePlanner.Most + 1);
            _seed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        private RouteOutcome PlanFrom(Waypoint start, bool askWhy)
        {
            return _route.Plan(start, _wanted, _seed, askWhy);
        }

        /// <summary>Starts a lesson or test from a desk. False, with a locale key why and nothing left behind, if it cannot.</summary>
        public bool Begin(LicenceClass kind, bool lesson, bool free, int feePaid, Vector3 desk, out string reasonKey)
        {
            reasonKey = null;
            if (Active)
            {
                reasonKey = "reallife_lic_no_busy";
                return false;
            }
            if (!TrafficTools.Ready || !RoadMap.Ensure())
            {
                reasonKey = "reallife_lic_no_roads";
                return false;
            }
            NewDraw();
            int lending = SessionWatch.Mark("licences: the school's car being made and put out (its route planned, the game's own vehicle spawn)");
            CarController car = _lender.Lend(desk, kind, _planFrom, out reasonKey);
            SessionWatch.Done(lending);
            if (car == null)
            {
                return false;
            }
            _car = car;
            _body = car.GetComponent<Rigidbody>();
            JobSpots.CarFrame(car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            if (_body == null || !_route.Build(halfWidth, halfLength))
            {
                _car = null;
                _body = null;
                _lender.Recall();
                reasonKey = "reallife_lic_no_route";
                return false;
            }
            _class = kind;
            _lesson = lesson;
            _free = free;
            _feePaid = feePaid;
            _desk = desk;
            Array.Clear(_faults, 0, _faults.Length);
            ResetSampling();
            _next = 0;
            _aboard = false;
            _passed = false;
            _outSince = -1f;
            _farSince = -1f;
            _reversed = 0f;
            _endWaived = false;
            if (_card == null || !_card.Alive)
            {
                _card = ShiftCard.Make(_logger);   // the game's job panel; null leaves the instructions to notifications
            }
            _hudNext = _hudMinor = _hudSerious = _hudSecs = -1;
            _toldObjective = null;
            _title = Licences.T(lesson ? "reallife_lic_test_title_lesson" : "reallife_lic_test_title", "class", Licences.ClassName(kind));
            Hook(car);
            PutExaminer(centre);
            _step = Step.Meet;
            _stepEnd = Time.time + MeetAllow;
            _nextTick = 0f;
            bool truck = kind == LicenceClass.Heavy;
            Say(truck ? "reallife_lic_test_meet_truck" : "reallife_lic_test_meet");
            Show(Licences.T(truck ? "reallife_lic_test_get_in_truck" : "reallife_lic_test_get_in"), car.transform.position,
                Licences.T(truck ? "reallife_lic_marker_truck" : "reallife_lic_marker_car"));
            _logger?.Info("Licences: " + (lesson ? "lesson" : "practical test") + " (" + kind + ") started: " + _route.Checkpoints.Count + " checkpoints over "
                          + _route.Length.ToString("F0") + " m, " + (_route.HasBay ? "a bay at the kerb" : "no kerb with room for a bay: a stop at the end")
                          + "; planned over the lanes open now (" + _route.Expanded + " lane points looked at).");
            return true;
        }

        // The examiner walks from the desk to the car's passenger side. Without one the test still runs.
        private void PutExaminer(Vector3 carCentre)
        {
            _examiner = null;
            _waiting = false;
            try
            {
                JobCast cast = _cast();
                if (cast == null || !StreetSpots.OnPavement(_desk, 4f, out Vector3 start) || !JobSpots.BesideCar(_car, start, out _boardSpot))
                {
                    return;
                }
                _examiner = cast.Put(start, true);
                if (_examiner != null)
                {
                    _examiner.StandAt(start, carCentre);
                    if (!_examiner.MoveTo(_boardSpot, Pace.Walk))
                    {
                        cast.Release(_examiner);
                        _examiner = null;
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _examiner = null;
            }
            if (_examiner == null)
            {
                _logger?.Info("Licences: nobody could be put on the pavement to walk to the car; the examiner is taken to be in it already.");
            }
        }

        /// <summary>Stopped before any result: everything goes back, and the fee too if <paramref name="refund"/>.</summary>
        public void Abort(string reasonKey, bool refund)
        {
            if (_step == Step.None)
            {
                return;
            }
            bool decided = _step == Step.Result || _step == Step.Leaving;
            if (!decided && reasonKey != null)
            {
                Licences.Toast(NotificationType.Warning, reasonKey, 10f);
                if (refund && _feePaid > 0 && !_free)
                {
                    Licences.Give(_feePaid, "reallife_transaction_licence_refund");
                    Licences.Toast(NotificationType.Info, "reallife_lic_fee_returned", "amount", Licences.Money(_feePaid), 8f);
                }
            }
            _logger?.Info("Licences: " + (_lesson ? "lesson" : "practical test") + " stopped (" + (reasonKey ?? "no reason given") + ")"
                          + (refund && !decided ? ", fee returned." : "."));
            Finish();
        }

        /// <summary>The city is unloading: everything of the test's goes off the screen for good.</summary>
        public void Dispose()
        {
            _mark.Hide();
            _park.Hide("the director went with the city");
            _card?.Destroy();
            _card = null;
        }

        /// <summary>Once a frame while a test is on.</summary>
        public void Frame(float now)
        {
            if ((_step == Step.Drive || _step == Step.Park) && now >= _nextSample)
            {
                float delta = now - (_nextSample - SampleInterval);
                _nextSample = now + SampleInterval;
                if (Driving)
                {
                    Sample(now, Mathf.Clamp(delta, 0.05f, 0.5f));
                }
            }
            if (now >= _nextTick)
            {
                _nextTick = now + TickInterval;
                Tick(now);
                Hud(now);
            }
        }

        private bool Driving
        {
            get
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                return gm != null && _car != null && ReferenceEquals(gm.selectedVehicle, _car) && PlayerHelper.IsUsingVehicle;
            }
        }

        private void Tick(float now)
        {
            if ((object)_car != null && _car == null)
            {
                Abort("reallife_lic_test_car_gone", refund: true);
                return;
            }
            // Indoors, the clock skipped or a shift begun: the street is gone and the test with it.
            if ((!TrafficTools.Ready || JobDirector.OnShift) && _step != Step.Result && _step != Step.Leaving)
            {
                Abort("reallife_lic_test_interrupted", refund: _step == Step.Meet);
                return;
            }
            JobCast cast = _cast();
            cast?.Cull();
            switch (_step)
            {
                case Step.Meet:
                    Meet(now, cast);
                    break;

                case Step.Drive:
                case Step.Park:
                    Drive(now);
                    break;

                case Step.Result:
                    if (now >= _resultAt + 45f)
                    {
                        CloseSheet(now);
                    }
                    break;

                case Step.Leaving:
                    if (now >= _stepEnd || _examiner == null || !_examiner.IsValid
                        || (_examiner.Position - PlayerHelper.GetPosition()).sqrMagnitude > 45f * 45f)
                    {
                        Finish();
                    }
                    break;
            }
        }

        private void Meet(float now, JobCast cast)
        {
            if (_examiner != null && (!_examiner.IsValid || !_examiner.InUse))
            {
                _examiner = null;
            }
            if (_examiner != null && !_aboard)
            {
                float gap = JobSpots.Flat(_examiner.Position, _boardSpot);
                if (_examiner.HasArrived && gap < 1.4f)
                {
                    if (!_waiting)
                    {
                        // At the door with the clipboard until the candidate is at the wheel.
                        _waiting = true;
                        _examiner.StandAt(_examiner.Position, _car.transform.position);
                        _examiner.Work("clipboard_write");
                    }
                    if (Driving)
                    {
                        _examiner.EndClips();
                        JobCast.DoorSound(_boardSpot);
                        cast.Seat(_examiner);
                        _aboard = true;
                    }
                }
                else if (_examiner.HasArrived && !_examiner.MoveTo(_boardSpot, Pace.Walk))
                {
                    // No way to the door after all: taken to have got in on the other side.
                    cast.Release(_examiner);
                    _examiner = null;
                }
            }
            bool ready = Driving && (_examiner == null || _aboard);
            if (ready)
            {
                _step = Step.Drive;
                _driveSince = now;
                _settled = false;
                _stepEnd = now + TimeLimit;
                _lastPosition = _body.position;
                _lastSpeed = 0f;
                Say(_lesson ? "reallife_lic_test_go_lesson" : "reallife_lic_test_go");
                Aim();
                return;
            }
            if (now >= _stepEnd)
            {
                Abort("reallife_lic_test_no_show", refund: true);
            }
        }

        // The next checkpoint, or past the last the box to park in.
        private void Aim()
        {
            int count = _route.Checkpoints.Count;
            if (_next < count - (_route.HasBay ? 1 : 0))
            {
                Vector3 at = _route.Checkpoints[_next];
                Show(Licences.T("reallife_lic_test_checkpoint", "number", (_next + 1).ToString(), "of", (count + (_route.HasBay ? 0 : 1)).ToString()),
                    at, Licences.T("reallife_lic_marker_checkpoint"));
                Beacon(at, _route.Runs[_next]);
                return;
            }
            _step = Step.Park;
            _reversed = 0f;
            HideBeacon();
            // Without a bay, a stop box across the lane at the last checkpoint.
            _inBox = false;
            if (_route.HasBay)
            {
                _boxCentre = _route.BayCentre;
                _boxFacing = _route.BayFacing;
                _boxHalfWidth = _route.BayHalfWidth;
                _boxHalfLength = _route.BayHalfLength;
            }
            else
            {
                Vector3 runs = _route.Runs.Count > 0 ? _route.Runs[_route.Runs.Count - 1] : Vector3.forward;
                runs.y = 0f;
                _boxCentre = _route.StopAt;
                _boxFacing = runs.sqrMagnitude > 0.01f ? Quaternion.LookRotation(runs.normalized, Vector3.up) : Quaternion.identity;
                // A car's box, or one a truck fits in with the same metre to spare at each end.
                JobSpots.CarFrame(_car, out _, out _, out float carHalfLength, out float carHalfWidth);
                _boxHalfWidth = Mathf.Max(StopHalfWidth, carHalfWidth + 0.45f);
                _boxHalfLength = Mathf.Max(StopHalfLength, carHalfLength + 1.2f);
            }
            if (_bay == null)
            {
                _bay = LicenceProps.MakeBay(_parent);
            }
            if (_bay != null)
            {
                LicenceProps.SizeBay(_bay, _boxHalfWidth, _boxHalfLength);
                _bay.SetPositionAndRotation(_boxCentre + Vector3.up * 0.02f, _boxFacing);
                _bay.gameObject.SetActive(true);
            }
            if (_route.HasBay)
            {
                Show(Licences.T(Reverses(_class) ? "reallife_lic_test_park_reverse" : "reallife_lic_test_park"),
                    _route.BayCentre, Licences.T("reallife_lic_marker_bay"));
                Say(Reverses(_class) ? "reallife_lic_test_say_park_reverse" : "reallife_lic_test_say_park");
            }
            else
            {
                Show(Licences.T("reallife_lic_test_stop"), _route.StopAt, Licences.T("reallife_lic_marker_stop"));
                Beacon(_route.StopAt, Vector3.forward);
                Say("reallife_lic_test_say_stop");
            }
        }

        private void Drive(float now)
        {
            if (PoliceAttention.PulledOver)
            {
                Fault(TestFault.PoliceStop);
                Conclude(now, "reallife_lic_test_ended_police");
                return;
            }
            if (!Driving)
            {
                _parkOffered = false;
                if (_outSince < 0f)
                {
                    _outSince = now;
                    Say("reallife_lic_test_get_back");
                }
                else if (now - _outSince > OutOfCarAllow)
                {
                    Conclude(now, "reallife_lic_test_ended_left");
                }
                return;
            }
            _outSince = -1f;
            if (now >= _stepEnd)
            {
                Conclude(now, "reallife_lic_test_ended_time");
                return;
            }
            // A lane closed after the planning: its checkpoint is skipped; at the bay or stop the drive ends unmarked.
            if (_route.ClosedAt(_next))
            {
                _logger?.Info("Licences: the lane at " + (_step == Step.Drive ? "checkpoint " + (_next + 1) : "the end of the route")
                              + " was closed during the drive (an accident or a street event); " + (_step == Step.Drive ? "it is passed over." : "the drive ends here, the parking not marked."));
                if (_step == Step.Drive)
                {
                    Say("reallife_lic_test_closed_skip");
                    _next++;
                    _farSince = -1f;
                    Aim();
                    return;
                }
                _endWaived = true;
                Conclude(now, null);
                return;
            }
            Vector3 position = _body.position;
            if (_step == Step.Drive)
            {
                Vector3 to = _route.Checkpoints[_next] - position;
                float sqr = to.x * to.x + to.z * to.z;
                if (sqr < Reach * Reach && Mathf.Abs(to.y) < 6f)
                {
                    _next++;
                    _farSince = -1f;
                    Aim();
                }
                else if (sqr > 420f * 420f)
                {
                    if (_farSince < 0f)
                    {
                        _farSince = now;
                        Say("reallife_lic_test_off_route");
                    }
                    else if (now - _farSince > OffRouteAllow)
                    {
                        Conclude(now, "reallife_lic_test_ended_route");
                    }
                }
                else
                {
                    _farSince = -1f;
                }
                return;
            }
            // The player says when the car is parked, standing still near the box.
            Vector3 goal = _route.HasBay ? _route.BayCentre : _route.StopAt;
            _parkOffered = JobSpots.Flat(position, goal) < 14f && _body.velocity.sqrMagnitude < 0.3f * 0.3f;
        }

        /// <summary>The classes whose bay is reversed into: the van's and the truck's, as on a real test.</summary>
        internal static bool Reverses(LicenceClass kind) => kind == LicenceClass.Commercial || kind == LicenceClass.Heavy;

        // Everything given back, whatever state it was in. Never throws.
        private void Finish()
        {
            _step = Step.None;
            Unhook();
            try
            {
                JobCast cast = _cast();
                if (_examiner != null && cast != null)
                {
                    cast.Release(_examiner);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _examiner = null;
            _aboard = false;
            _parkOffered = false;
            _markerLabel = null;
            HideProps();
            _lender.Return();
            _car = null;
            _body = null;
            _card?.Show(false);
            _mark.Hide();
            _park.Hide("the test ended");
            _parkWritten = false;
            _closingSheet = true;
            if (_screens.Window.OpenFor(this))
            {
                _screens.Window.Close();
            }
            _closingSheet = false;
        }
    }
}
