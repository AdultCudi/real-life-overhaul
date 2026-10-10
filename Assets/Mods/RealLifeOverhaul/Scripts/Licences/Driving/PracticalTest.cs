using System;
using System.Text;
using BAModAPI;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>What an examiner writes down.</summary>
    internal enum TestFault
    {
        RedLight,        // serious
        PastLine,        // stopped beyond the stop line at a red light
        Speeding,        // more than 10 km/h over for a couple of seconds
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
    /// A driving lesson or a practical test, actually driven. The school lends a car (TestCar),
    /// the examiner walks from the desk and gets in beside the player, and the route (TestRoute)
    /// is driven checkpoint by checkpoint to a marked bay at the kerb. What is marked:
    ///   red lights (crossing the stop line on red is a serious fault, stopping past it a minor one),
    ///   speed against the lane's own limit (10 over for two seconds minor, 30 over serious),
    ///   keeping to the lane (minor), the pavement and the wrong side of the road (serious),
    ///   harsh braking (minor), touching anything (minor) or hitting it (serious),
    ///   and the parking: inside the box, straight, and for the commercial class reversed into.
    /// A test passes with no serious fault and at most six minor ones. A lesson is the same drive
    /// with the instructor saying each fault as it happens and nothing at stake.
    /// Cost: while one is on, ten looks a second at the car (a few subtractions against RoadMap's
    /// grid, one navigation-mesh sample), a 0.2 s step, and the marker drawn per frame. Otherwise nothing.
    /// </summary>
    internal sealed class PracticalTest
    {
        private const float SampleInterval = 0.1f;
        private const float TickInterval = 0.2f;
        private const float MeetAllow = 120f;          // seconds to get into the car
        private const float TimeLimit = 600f;          // seconds for the drive
        private const float Reach = 9f;                // metres from a checkpoint that counts as through it
        private const float OutOfCarAllow = 30f;
        private const float OffRouteAllow = 40f;

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
        private readonly StringBuilder _text = new StringBuilder(512);

        private Step _step = Step.None;
        private LicenceClass _class;
        private bool _lesson;
        private bool _free;                 // started by the test button: nothing charged, nothing granted... except by a pass
        private int _feePaid;
        private CarController _car;
        private CarController _hooked;
        private Rigidbody _body;
        private StreetActor _examiner;
        private bool _aboard;
        private bool _waiting;
        private Vector3 _boardSpot;
        private bool _boardFound;
        private Vector3 _desk;
        private Transform _beacon;
        private Transform _bay;
        private int _next;
        private float _stepEnd;
        private float _nextTick;
        private float _nextSample;
        private float _outSince = -1f;
        private float _farSince = -1f;
        private float _halfWidth = 0.9f, _halfLength = 2.1f;
        private string _title, _objective, _status, _markerLabel, _prompt;
        private int _statusMinor = -1, _statusSerious = -1;
        private bool _passed;
        private float _resultAt;

        // What the sampling remembers between looks.
        private int _line = -1;
        private float _along;
        private float _redSince = -1f;
        private bool _pastBooked;
        private float _overSince = -1f, _farOverSince = -1f;
        private bool _overBooked, _farOverBooked;
        private float _laneSince = -1f;
        private bool _laneBooked;
        private float _pavementSince = -1f;
        private bool _pavementBooked;
        private float _wrongSince = -1f;
        private bool _wrongBooked;
        private float _lastSpeed;
        private float _brakeSince = -1f;
        private float _lastBrake = -100f;
        private float _lastTouch = -100f;
        private float _reversed;            // metres backed near the bay
        private Vector3 _lastPosition;

        public PracticalTest(TestCar lender, Func<JobCast> cast, Transform parent, IModLogger logger)
        {
            _lender = lender;
            _cast = cast;
            _parent = parent;
            _logger = logger;
        }

        public bool Active => _step != Step.None;

        /// <summary>True while a key prompt of the test's is on screen (the mod's own enter-a-car key stands aside).</summary>
        public bool Prompting => _prompt != null;

        /// <summary>The school's car of the test that is on, or null.</summary>
        public CarController Car => Active ? _car : null;

        /// <summary>
        /// Starts a lesson or a test from a desk. False with a reason (a locale key) and nothing
        /// charged or left behind if it cannot be staged here.
        /// </summary>
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
            CarController car = _lender.Lend(desk, kind == LicenceClass.Commercial);
            if (car == null)
            {
                reasonKey = "reallife_lic_no_car_spot";
                return false;
            }
            _car = car;
            _body = car.GetComponent<Rigidbody>();
            JobSpots.CarFrame(car, out Vector3 centre, out Vector3 forward, out _halfLength, out _halfWidth);
            if (_body == null || !_route.Build(centre, forward, _halfWidth, _halfLength, UnityEngine.Random.Range(5, 8)))
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
            _statusMinor = -1;
            _statusSerious = -1;
            _title = Licences.T(lesson ? "reallife_lic_test_title_lesson" : "reallife_lic_test_title", "class", Licences.ClassName(kind));
            Hook(car);
            PutExaminer(centre);
            _step = Step.Meet;
            _stepEnd = Time.time + MeetAllow;
            _nextTick = 0f;
            Say("reallife_lic_test_meet");
            Show(Licences.T("reallife_lic_test_get_in"), car.transform.position, Licences.T("reallife_lic_marker_car"));
            _logger?.Info("Licences: " + (lesson ? "lesson" : "practical test") + " (" + kind + ") started: " + _route.Checkpoints.Count + " checkpoints over "
                          + _route.Length.ToString("F0") + " m, " + (_route.HasBay ? "a bay at the kerb" : "no kerb with room for a bay: a stop at the end") + ".");
            return true;
        }

        // The examiner comes from the desk and walks to the car's passenger side. Without one the test still runs.
        private void PutExaminer(Vector3 carCentre)
        {
            _examiner = null;
            _boardFound = false;
            _waiting = false;
            try
            {
                JobCast cast = _cast();
                if (cast == null || !StreetSpots.OnPavement(_desk, 4f, out Vector3 start))
                {
                    return;
                }
                _boardFound = JobSpots.BesideCar(_car, start, out _boardSpot);
                if (!_boardFound)
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

        // ---------------------------------------------------------------- per frame and per step

        /// <summary>Once a frame while a test is on: the looks at the car ten times a second, the step five times, and the marker.</summary>
        public void Frame(float now)
        {
            if (_step == Step.None)
            {
                return;
            }
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
            }
            if (_step == Step.None || !LicenceHud.Exists)
            {
                return;
            }
            LicenceHud hud = LicenceHud.Instance;
            if (ThirdPersonCamera.IsUiOpen())
            {
                hud.HideObjective();
                hud.HideMarker();
                hud.HidePrompt();
                return;
            }
            hud.ShowObjective(_title, _objective);
            if (_step == Step.Drive || _step == Step.Park)
            {
                if (_statusMinor != Minors || _statusSerious != Seriouses)
                {
                    _statusMinor = Minors;
                    _statusSerious = Seriouses;
                    _status = _lesson ? Licences.T("reallife_lic_status_lesson", "faults", (_statusMinor + _statusSerious).ToString())
                        : Licences.T("reallife_lic_status_faults", "minor", _statusMinor.ToString(), "serious", _statusSerious.ToString());
                }
                hud.ShowStatus(_status);
            }
            else
            {
                hud.ShowStatus(null);
            }
            if (_markerLabel != null)
            {
                PlayerController player = PlayerHelper.PlayerController;
                hud.ShowMarkerAt(_markerLabel, _markerAt, _body != null && Driving ? _body.position : player != null ? player.transform.position : _markerAt);
            }
            else
            {
                hud.HideMarker();
            }
            if (_prompt != null)
            {
                hud.ShowPrompt(_prompt);
            }
            else
            {
                hud.HidePrompt();
            }
        }

        private Vector3 _markerAt;

        private bool Driving
        {
            get
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                return gm != null && _car != null && ReferenceEquals(gm.selectedVehicle, _car) && PlayerHelper.IsUsingVehicle;
            }
        }

        private int Minors
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _faults.Length; i++)
                {
                    total += Serious[i] ? 0 : _faults[i];
                }
                return total;
            }
        }

        private int Seriouses
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _faults.Length; i++)
                {
                    total += Serious[i] ? _faults[i] : 0;
                }
                return total;
            }
        }

        private void Tick(float now)
        {
            if ((object)_car != null && _car == null)
            {
                Abort("reallife_lic_test_car_gone", refund: true);
                return;
            }
            if (!TrafficTools.Ready && _step != Step.Result && _step != Step.Leaving)
            {
                // Indoors, or the clock is being skipped: the street is gone and the test with it.
                Abort("reallife_lic_test_interrupted", refund: _step == Step.Meet);
                return;
            }
            if (JobDirector.OnShift && _step != Step.Result && _step != Step.Leaving)
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
                    // No way to the door after all: they are taken to have got in on the other side.
                    cast.Release(_examiner);
                    _examiner = null;
                }
            }
            bool ready = Driving && (_examiner == null || _aboard);
            if (ready)
            {
                _step = Step.Drive;
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

        // The next checkpoint: what the objective says, where the marker and the beacon stand.
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
            if (_route.HasBay)
            {
                if (_bay == null)
                {
                    _bay = LicenceProps.MakeBay(_parent);
                }
                if (_bay != null)
                {
                    LicenceProps.SizeBay(_bay, _route.BayHalfWidth, _route.BayHalfLength);
                    _bay.SetPositionAndRotation(_route.BayCentre + Vector3.up * 0.02f, _route.BayFacing);
                    _bay.gameObject.SetActive(true);
                }
                Show(Licences.T(_class == LicenceClass.Commercial ? "reallife_lic_test_park_reverse" : "reallife_lic_test_park"),
                    _route.BayCentre, Licences.T("reallife_lic_marker_bay"));
                Say(_class == LicenceClass.Commercial ? "reallife_lic_test_say_park_reverse" : "reallife_lic_test_say_park");
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
                Fault(TestFault.PoliceStop, now);
                Conclude(now, "reallife_lic_test_ended_police");
                return;
            }
            if (!Driving)
            {
                _prompt = null;
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

            // Parking: they say when they are done, with the car standing still.
            Vector3 goal = _route.HasBay ? _route.BayCentre : _route.StopAt;
            bool near = JobSpots.Flat(position, goal) < 14f;
            bool still = _body.velocity.sqrMagnitude < 0.3f * 0.3f;
            string prompt = near && still ? PromptText : null;
            if (!ReferenceEquals(prompt, _prompt))
            {
                _prompt = prompt;
            }
        }

        private string _promptText;
        private int _promptKeys = -1;

        private string PromptText
        {
            get
            {
                if (_promptText == null || _promptKeys != Keys.Version)
                {
                    _promptKeys = Keys.Version;
                    _promptText = Licences.T("reallife_lic_test_prompt_parked", "key", Keys.Label(KeyAction.Interact));
                }
                return _promptText;
            }
        }

        /// <summary>The interaction key: "I have parked", or putting the result sheet away.</summary>
        public void Interact(float now)
        {
            if (_step == Step.Park && _prompt != null)
            {
                JudgeParking();
                Conclude(now, null);
            }
            else if (_step == Step.Result && now >= _resultAt + 1f)
            {
                CloseSheet(now);
            }
        }

        // ---------------------------------------------------------------- what the examiner sees

        private void ResetSampling()
        {
            _line = -1;
            _redSince = -1f;
            _pastBooked = false;
            _overSince = -1f;
            _farOverSince = -1f;
            _overBooked = false;
            _farOverBooked = false;
            _laneSince = -1f;
            _laneBooked = false;
            _pavementSince = -1f;
            _pavementBooked = false;
            _wrongSince = -1f;
            _wrongBooked = false;
            _brakeSince = -1f;
            _lastSpeed = 0f;
            _nextSample = 0f;
        }

        private void Sample(float now, float delta)
        {
            Vector3 position = _body.position;
            Vector3 velocity = _body.velocity;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            Vector3 forward = _body.rotation * Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            Vector3 heading = speed > 1.5f ? new Vector3(velocity.x, 0f, velocity.z) / speed : forward;

            // Backing up near the bay: the commercial class has to reverse in.
            if (_step == Step.Park)
            {
                Vector3 moved = position - _lastPosition;
                float along = moved.x * forward.x + moved.z * forward.z;
                if (along < 0f && along > -3f)
                {
                    _reversed -= along;
                }
            }
            _lastPosition = position;

            // The red light ahead in their own lane.
            if (RoadMap.LineAt(position, heading, out int line, out float past))
            {
                bool red = RoadMap.IsRed(RoadMap.Line(line));
                if (line != _line)
                {
                    _line = line;
                    _along = past;
                    _redSince = red ? now : -1f;
                    _pastBooked = false;
                }
                else
                {
                    if (!red)
                    {
                        _redSince = -1f;
                    }
                    else if (_redSince < 0f)
                    {
                        _redSince = now;
                    }
                    bool held = red && now - _redSince >= 1f;
                    if (held && _along < 1f && past >= 1f && speed > 1.5f)
                    {
                        Fault(TestFault.RedLight, now);
                        _pastBooked = true;
                    }
                    else if (held && !_pastBooked && speed < 0.4f && past > 0.6f && past < 4f)
                    {
                        Fault(TestFault.PastLine, now);
                        _pastBooked = true;
                    }
                    _along = past;
                }
            }
            else
            {
                _line = -1;
            }

            // Harsh braking: a hard stop held for a moment, from a real speed.
            float decel = (_lastSpeed - speed) / delta;
            if (decel > 9f && _lastSpeed > 7f)
            {
                if (_brakeSince < 0f)
                {
                    _brakeSince = now;
                }
                else if (now - _brakeSince >= 0.3f && now - _lastBrake > 6f)
                {
                    _lastBrake = now;
                    Fault(TestFault.HarshBrake, now);
                }
            }
            else
            {
                _brakeSince = -1f;
            }
            _lastSpeed = speed;

            if (!RoadMap.NearestLane(position, out float distance, out Vector3 runs, out float limit))
            {
                _overSince = -1f;
                _laneSince = -1f;
                _wrongSince = -1f;
                return;
            }

            // Speed against the lane's own limit.
            float over = speed * 3.6f - limit;
            if (over > 10f && distance < 8f)
            {
                if (_overSince < 0f)
                {
                    _overSince = now;
                }
                else if (!_overBooked && now - _overSince >= 2f)
                {
                    _overBooked = true;
                    Fault(TestFault.Speeding, now);
                }
            }
            else
            {
                _overSince = -1f;
                _overBooked = false;
            }
            if (over > 30f && distance < 8f)
            {
                if (_farOverSince < 0f)
                {
                    _farOverSince = now;
                }
                else if (!_farOverBooked && now - _farOverSince >= 1f)
                {
                    _farOverBooked = true;
                    Fault(TestFault.SpeedingFar, now);
                }
            }
            else
            {
                _farOverSince = -1f;
                _farOverBooked = false;
            }

            // Out of the lane: measured sideways from the middle of the lane the route follows (the road data's own points
            // may lie metres apart, so the distance to the nearest of them says little). Not in a junction, where there is
            // no lane to keep to, not while parking, and not once they are so far off that it is another road altogether.
            float lateral = _route.Lateral(position, out bool junction);
            if (_step == Step.Drive && speed > 3f && !junction && lateral > 2.6f && lateral < 7f)
            {
                if (_laneSince < 0f)
                {
                    _laneSince = now;
                }
                else if (!_laneBooked && now - _laneSince >= 3f)
                {
                    _laneBooked = true;
                    Fault(TestFault.LeftLane, now);
                }
            }
            else if (lateral < 1.8f || speed < 1f || junction)
            {
                _laneSince = -1f;
                _laneBooked = false;
            }

            // On the pavement: where people walk, well to one side of the lane.
            if (speed > 2f && lateral > 3.2f && NavMesh.SamplePosition(position, out NavMeshHit hit, 0.6f, StreetSpots.Pavement)
                && (hit.position.x - position.x) * (hit.position.x - position.x) + (hit.position.z - position.z) * (hit.position.z - position.z) < 0.25f)
            {
                if (_pavementSince < 0f)
                {
                    _pavementSince = now;
                }
                else if (!_pavementBooked && now - _pavementSince >= 1.5f)
                {
                    _pavementBooked = true;
                    Fault(TestFault.Pavement, now);
                }
            }
            else
            {
                _pavementSince = -1f;
                _pavementBooked = false;
            }

            // Against the traffic: close to a lane point whose lane runs the other way. The clock only stops when they are
            // seen to be with a lane again (or slow): between two lane points nothing is known either way.
            float withLane = runs.x * velocity.x + runs.z * velocity.z;
            if (speed > 5f && distance < 2.2f && withLane < -0.85f * speed)
            {
                if (_wrongSince < 0f)
                {
                    _wrongSince = now;
                }
                else if (!_wrongBooked && now - _wrongSince >= 2.5f)
                {
                    _wrongBooked = true;
                    Fault(TestFault.WrongWay, now);
                }
            }
            else if (speed < 3f || (distance < 2.2f && withLane > 0.5f * speed))
            {
                _wrongSince = -1f;
                _wrongBooked = false;
            }
        }

        private void Hook(CarController car)
        {
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                car.vehicleController.onCollision.AddListener(OnCollision);
                _hooked = car;
            }
        }

        private void Unhook()
        {
            if (_hooked != null && _hooked.vehicleController != null)
            {
                _hooked.vehicleController.onCollision.RemoveListener(OnCollision);
            }
            _hooked = null;
        }

        private void OnCollision(Collision collision)
        {
            try
            {
                float now = Time.time;
                if ((_step != Step.Drive && _step != Step.Park) || collision == null || now - _lastTouch < 2.5f || now - _lender.SpawnedAt < 3f)
                {
                    return;
                }
                float speed = collision.relativeVelocity.magnitude;
                if (speed < 0.6f)
                {
                    return;
                }
                _lastTouch = now;
                Fault(speed >= 2.5f ? TestFault.Collision : TestFault.Touched, now);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Fault(TestFault fault, float now)
        {
            _faults[(int)fault]++;
            _logger?.Info("Licences: fault marked: " + fault + " (" + (Serious[(int)fault] ? "serious" : "minor") + ").");
            if (_lesson)
            {
                // The instructor says it there and then.
                Licences.Toast(NotificationType.Warning, FaultKeys[(int)fault], 6f);
            }
        }

        // Stopped and declared: is the car in the box, straight, and (for the commercial class) backed in?
        private void JudgeParking()
        {
            JobSpots.CarFrame(_car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            if (!_route.HasBay)
            {
                if (JobSpots.Flat(centre, _route.StopAt) > 6f)
                {
                    _faults[(int)TestFault.NotParked]++;
                }
                else if (JobSpots.Flat(centre, _route.StopAt) > 3f)
                {
                    _faults[(int)TestFault.ParkedBadly]++;
                }
                return;
            }
            Vector3 local = Quaternion.Inverse(_route.BayFacing) * (centre - _route.BayCentre);
            Vector3 bayForward = _route.BayFacing * Vector3.forward;
            float angle = Vector3.Angle(forward, bayForward);
            float overSide = Mathf.Abs(local.x) + halfWidth - _route.BayHalfWidth;
            float overEnd = Mathf.Abs(local.z) + halfLength - _route.BayHalfLength;
            float over = Mathf.Max(overSide, overEnd);
            _logger?.Info("Licences: parked " + local.x.ToString("F2") + " m across and " + local.z.ToString("F2") + " m along from the middle of the bay, "
                          + angle.ToString("F0") + " degrees off, " + Mathf.Max(0f, over).ToString("F2") + " m over its edge; reversed " + _reversed.ToString("F1") + " m.");
            if (over > 0.6f || angle > 25f)
            {
                _faults[(int)TestFault.NotParked]++;
            }
            else if (over > 0.1f || angle > 12f)
            {
                _faults[(int)TestFault.ParkedBadly]++;
            }
            if (_class == LicenceClass.Commercial && _reversed < 2.5f && over <= 0.6f)
            {
                _faults[(int)TestFault.NoReverse]++;
            }
        }

        // ---------------------------------------------------------------- the result

        // The drive is over, one way or another: the sheet, and for a test the licence.
        private void Conclude(float now, string endedKey)
        {
            Unhook();
            _prompt = null;
            HideBeacon();
            if (_bay != null)
            {
                _bay.gameObject.SetActive(false);
            }
            bool ended = endedKey != null;   // cut short: a test cut short is failed, whatever was marked
            int minor = Minors;
            int serious = Seriouses;
            _passed = !_lesson && !ended && serious == 0 && minor <= LicenceLaw.MinorsAllowed;
            int day = TimeHelper.CurrentDay;
            int index = _class == LicenceClass.Commercial ? 1 : 0;
            if (_lesson)
            {
                if (!ended)
                {
                    LicenceState.Lessons[index]++;
                }
            }
            else if (_passed)
            {
                LicenceState.Grant(_class, day);
                LicenceState.RetestDue[index] = false;
                LicenceState.RetestDay[index] = 0;
                LicenceState.TestsPassed++;
                ModState.AddModifier(ModState.MoodUpBig);
            }
            else
            {
                LicenceState.RetestDue[index] = true;
                LicenceState.RetestDay[index] = day + LicenceLaw.RetestWaitDays;
                LicenceState.TestsFailed++;
                ModState.AddModifier(ModState.MoodDown);
            }

            _text.Length = 0;
            if (ended)
            {
                _text.Append(Licences.T(endedKey)).Append('\n').Append('\n');
            }
            if (_lesson)
            {
                _text.Append(Licences.T(ended ? "reallife_lic_sheet_lesson_cut" : "reallife_lic_sheet_lesson_done")).Append('\n');
            }
            else
            {
                _text.Append(Licences.T("reallife_lic_sheet_counts", "minor", minor.ToString(), "serious", serious.ToString())
                    .Replace("{allowed}", LicenceLaw.MinorsAllowed.ToString())).Append('\n');
            }
            bool any = false;
            for (int i = 0; i < _faults.Length; i++)
            {
                if (_faults[i] > 0)
                {
                    any = true;
                    _text.Append("  ").Append(_faults[i]).Append(" x  ").Append(Licences.T(FaultKeys[i])).Append('\n');
                }
            }
            if (!any)
            {
                _text.Append("  ").Append(Licences.T("reallife_lic_sheet_clean")).Append('\n');
            }
            if (!_lesson)
            {
                _text.Append('\n').Append(_passed
                    ? Licences.T("reallife_lic_sheet_pass", "class", Licences.ClassName(_class), "days", LicenceLaw.ValidDays.ToString())
                    : Licences.T("reallife_lic_sheet_fail", "amount", Licences.Money(LicenceLaw.RetestFee)));
            }
            string title = _lesson ? Licences.T("reallife_lic_sheet_title_lesson")
                : Licences.T(_passed ? "reallife_lic_sheet_title_pass" : "reallife_lic_sheet_title_fail");
            LicenceHud.Instance.ShowSheet(title, _text.ToString(), null, Licences.T("reallife_lic_sheet_close", "key", Keys.Label(KeyAction.Interact)));
            _objective = Licences.T("reallife_lic_test_over");
            _markerLabel = null;
            _step = Step.Result;
            _resultAt = now;
            _logger?.Info("Licences: " + (_lesson ? "lesson" : "practical test") + " over" + (ended ? " (" + endedKey + ")" : string.Empty) + ": " + minor + " minor, "
                          + serious + " serious" + (_lesson ? "." : _passed ? ": PASSED, " + _class + " licence issued." : ": failed."));
        }

        private void CloseSheet(float now)
        {
            if (LicenceHud.Exists)
            {
                LicenceHud.Instance.HideSheet();
            }
            Licences.Toast(_passed ? NotificationType.Success : NotificationType.Info,
                _lesson ? "reallife_lic_test_bye_lesson" : _passed ? "reallife_lic_test_bye_pass" : "reallife_lic_test_bye_fail", 8f);
            // The examiner gets out and walks back towards the desk; the car goes back once the player has left it.
            JobCast cast = _cast();
            bool walking = false;
            try
            {
                if (_examiner != null && _aboard && cast != null && _car != null && JobSpots.BesideCar(_car, _desk, out Vector3 spot)
                    && cast.Unseat(_examiner, spot, true))
                {
                    JobCast.DoorSound(spot);
                    _aboard = false;
                    Vector3 away = StreetSpots.PavementAround(spot, 30f, 55f, true, out Vector3 found) ? found : _desk;
                    walking = _examiner.MoveTo(away, Pace.Walk);
                }
                else if (_examiner != null && !_aboard)
                {
                    walking = _examiner.MoveTo(_desk, Pace.Walk);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            if (!walking && _examiner != null)
            {
                cast?.Release(_examiner);   // no room to get out, or no way on: gone unseen with the car
                _examiner = null;
            }
            _lender.Return();
            _step = Step.Leaving;
            _stepEnd = now + 40f;
        }

        /// <summary>Stopped before any result (no car, the street gone, a fault): everything goes back, and the fee too if nothing was driven.</summary>
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
            _prompt = null;
            _markerLabel = null;
            HideBeacon();
            if (_bay != null)
            {
                _bay.gameObject.SetActive(false);
            }
            _lender.Return();
            _car = null;
            _body = null;
            if (LicenceHud.Exists)
            {
                LicenceHud.Instance.HideActivity();
                LicenceHud.Instance.HidePrompt();
            }
        }

        // ---------------------------------------------------------------- small parts

        private void Show(string objective, Vector3 markerAt, string markerLabel)
        {
            _objective = objective;
            _markerAt = markerAt;
            _markerLabel = markerLabel;
        }

        private static void Say(string key)
        {
            Licences.Toast(NotificationType.Info, key, 7f);
        }

        // One beacon, moved: on the kerb side of the checkpoint, clear of the lane.
        private void Beacon(Vector3 at, Vector3 runs)
        {
            if (_beacon == null)
            {
                _beacon = LicenceProps.Make(LicenceProps.Beacon, _parent);
            }
            if (_beacon == null)
            {
                return;
            }
            Vector3 place = at + Vector3.Cross(Vector3.up, runs) * 2.4f;
            if (StreetSpots.Ground(place, out float ground))
            {
                place.y = ground;
            }
            // The modelled beacon flies its flag to its own +X and shows it along +Z and -Z: turned so that a face looks back
            // down the lane at whoever drives up, and the flag points in at the checkpoint instead of to wherever the world's +X is.
            Vector3 back = new Vector3(-runs.x, 0f, -runs.z);
            _beacon.SetPositionAndRotation(place, back.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(back, Vector3.up) : Quaternion.identity);
            _beacon.gameObject.SetActive(true);
        }

        private void HideBeacon()
        {
            if (_beacon != null)
            {
                _beacon.gameObject.SetActive(false);
            }
        }
    }
}
