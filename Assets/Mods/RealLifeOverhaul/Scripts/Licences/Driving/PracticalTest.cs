using System;
using System.Text;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
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
    ///   speed against the lane's own limit (10 over held for four seconds minor, 30 over for two and a half serious),
    ///   keeping to the lane (minor), the pavement and the wrong side of the road (serious),
    ///   harsh braking (minor), touching anything (minor) or hitting it (serious),
    ///   and the parking: inside the box, straight, and for the commercial class reversed into.
    /// A test passes with no serious fault and at most six minor ones. A lesson is the same drive
    /// with the instructor saying each fault as it happens and nothing at stake.
    /// Only what the player could have helped is marked. The pedals of this game are keys, all or nothing, and the
    /// school's car stands where there was room for it, so:
    ///   nothing about where the car is (lane, pavement, direction, stop lines) is marked until it has first been
    ///   properly in a lane, or SettleAllow seconds have passed (it may start on a kerb, across a line or facing a red light);
    ///   a stop line counts only if the car came up to it from LineApproach metres back, and a red light only if it was
    ///   red while there was still room to stop firmly (a light that changes on top of the car is nobody's fault);
    ///   braking is harsh only as a full stop held from speed (a dab of the brake key is as hard as the key gets);
    ///   speed over the limit has to be held for long enough to have seen it and lifted off;
    ///   being run into while standing or crawling is not the player's collision.
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
        private const float SettleAllow = 25f;         // seconds from setting off after which the car is taken to be on the road, wherever it is
        private const float LineApproach = 3f;         // metres short of a stop line the car's middle must have been seen for that line to count (its nose behind the line)
        private const float FirmStop = 6f;             // m/s2: a firm stop, not a harsh one
        private const float ReactTime = 0.7f;          // seconds to see a light change
        private const float RoomMost = 26f;            // metres: the most room that is asked for (a stop line is seen from 30 m): a light already red from that far is the driver's at any speed
        private const float ShuntAllow = 2.5f;         // seconds after being run into from behind in which a line crossed is not the driver's doing
        private const float Abreast = 2f;              // metres along the road within which two lanes' stop lines are the same line
        private const float HarshFrom = 11f;           // m/s (40 km/h): braking is only harsh from above this...
        private const float HarshHeld = 0.9f;          // ...with the brake held flat this long: all but a full stop
        private const float OverHeld = 4f;             // seconds over the limit before it is a fault
        private const float FarOverHeld = 2.5f;        // ...and far over it
        private const float TouchFrom = 1f;            // m/s of impact that is a touch
        private const float CollisionFrom = 3.5f;      // ...and a collision
        private const float OwnMove = 1.5f;            // m/s: slower than this, what hit the car hit it

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
        private readonly SchoolScreens _screens;
        private readonly GuideMark _mark = new GuideMark();
        private readonly DoorPanel _park;
        private readonly Action _onParked;
        private readonly Action _onSheetClosed;
        private ShiftCard _card;
        private bool _parkWritten;
        private bool _closingSheet;
        private string _toldObjective;
        private int _hudNext = -1, _hudMinor = -1, _hudSerious = -1, _hudSecs = -1;

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
        // The box the drive ends in, painted on the road: the bay at the kerb, or (with no kerb to mark one at) a box in the
        // lane where the car is to stop. Whatever is judged is what is painted, and the marker stands in its middle.
        private Vector3 _boxCentre;
        private Quaternion _boxFacing = Quaternion.identity;
        private float _boxHalfWidth, _boxHalfLength;
        private bool _inBox;
        private const float StopHalfWidth = 1.7f;      // metres: the stop box in the lane, half its width...
        private const float StopHalfLength = 4f;       // ...and half its length: a car and a good metre each end
        private const float BoxAllow = 0.6f;           // metres a car may stand over the box's edge and still be in it
        private const float BoxNear = 2.5f;            // ...and still be marked only as parked badly (a minor fault)
        private int _next;
        private float _stepEnd;
        private float _nextTick;
        private float _nextSample;
        private float _outSince = -1f;
        private float _farSince = -1f;
        private float _halfWidth = 0.9f, _halfLength = 2.1f;
        private string _title, _objective, _markerLabel, _prompt;
        private bool _passed;
        private float _resultAt;

        // What the sampling remembers between looks.
        private int _line = -1;
        private float _along;
        private float _redSince = -1f;
        private bool _pastBooked;
        private float _lineFrom;            // how far short of the line the car was when the line was first seen (negative: short of it)
        private bool _couldStop;            // the light was red while there was still room to stop firmly
        private float _lastShunt = -100f;   // when the car was last run into while standing or crawling
        private bool _settled;              // the car has been properly in a lane since setting off
        private float _driveSince;
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

        public PracticalTest(TestCar lender, Func<JobCast> cast, Transform parent, IModLogger logger, SchoolScreens screens)
        {
            _screens = screens;
            _park = new DoorPanel(logger, "Licences check (parked panel)");
            _onParked = _park.Press(ParkedClicked);
            _onSheetClosed = SheetClosed;
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
            // Named and timed for the session record: the car is made by the game, in this one frame, and nothing else here is large.
            int lending = SessionWatch.Mark("licences: the school's car being made and put out (the game's own vehicle spawn)");
            CarController car = _lender.Lend(desk, kind == LicenceClass.Commercial);
            SessionWatch.Done(lending);
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
            if (_card == null || !_card.Alive)
            {
                _card = ShiftCard.Make(_logger);   // the game's job panel; null (one line says why) leaves the instruction to notifications
            }
            _hudNext = _hudMinor = _hudSerious = _hudSecs = -1;
            _toldObjective = null;
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
                Hud(now);
            }
        }

        // Five times a second: the job panel's lines (only the ones whose words changed), the game's destination marker, and the panel at the bay.
        private void Hud(float now)
        {
            if (_step == Step.None)
            {
                return;
            }
            if (_step == Step.Result || _step == Step.Leaving)
            {
                _card?.Show(false);
                _mark.Hide();
                _park.Hide("the drive is over");
                return;
            }
            bool driving = _step == Step.Drive || _step == Step.Park;
            if (_card != null)
            {
                _card.Title(_title);
                _card.Line(0, _objective, WorldPanel.White());
                if (driving)
                {
                    int minor = Minors;
                    int serious = Seriouses;
                    int done = _step == Step.Park ? _route.Checkpoints.Count : _next;
                    if (done != _hudNext)
                    {
                        _hudNext = done;
                        _card.Line(1, Licences.T("reallife_lic_card_checkpoints", "done", done.ToString(), "of", _route.Checkpoints.Count.ToString()), WorldPanel.White());
                    }
                    if (minor != _hudMinor || serious != _hudSerious)
                    {
                        _hudMinor = minor;
                        _hudSerious = serious;
                        if (_lesson)
                        {
                            _card.Line(2, Licences.T("reallife_lic_card_faults", "faults", (minor + serious).ToString()), serious > 0 ? WorldPanel.Red() : WorldPanel.White());
                            _card.Line(3, null, WorldPanel.White());
                        }
                        else
                        {
                            _card.Line(2, Licences.T("reallife_lic_card_minor", "minor", minor.ToString(), "allowed", LicenceLaw.MinorsAllowed.ToString()), WorldPanel.White());
                            _card.Line(3, Licences.T("reallife_lic_card_serious", "serious", serious.ToString()), serious > 0 ? WorldPanel.Red() : WorldPanel.White());
                        }
                    }
                }
                int secs = Mathf.Max(0, Mathf.CeilToInt(_stepEnd - now));
                if (secs != _hudSecs)
                {
                    _hudSecs = secs;
                    _card.Line(4, Licences.T("reallife_lic_card_time", "time", (secs / 60).ToString() + ":" + (secs % 60).ToString("00")), secs < 30 ? WorldPanel.Red() : WorldPanel.White());
                }
                _card.Show(true);
            }
            else if (!ReferenceEquals(_objective, _toldObjective))
            {
                _toldObjective = _objective;
                Licences.Toast(NotificationType.Info, "reallife_lic_toast", "text", _objective ?? string.Empty, 6f);
            }
            if (_markerLabel != null)
            {
                _mark.Show(_markerAt, _markerLabel);
            }
            else
            {
                _mark.Hide();
            }
            if (_prompt != null && _body != null && _step == Step.Park)
            {
                if (!_parkWritten)
                {
                    _parkWritten = true;
                    WorldPanel panel = _park.Panel;
                    panel.Lines(Licences.T("reallife_lic_park_title"), Licences.T("reallife_lic_park_line"), null, WorldPanel.White());
                    panel.Buttons("reallife_lic_park_button", _onParked, true, null, null, false);
                    panel.Button(2, null, null, false, false);
                    panel.Button(3, null, null, false, false);
                }
                _park.Show(_body.position, _body.position, 2.0f, null);
            }
            else
            {
                _park.Hide("the car is not still near the bay");
            }
        }

        private void ParkedClicked()
        {
            try
            {
                if (_step == Step.Park && _prompt != null)
                {
                    _park.Hide("the player said they have parked");
                    JudgeParking();
                    Conclude(Time.time, null);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Takes everything of the test's off the screen for good: the city is unloading.</summary>
        public void Dispose()
        {
            _mark.Hide();
            _park.Hide("the director went with the city");
            _card?.Destroy();
            _card = null;
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
            // The box: the bay, or a stop box across the lane at the last checkpoint, facing the way the lane runs.
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
                _boxHalfWidth = StopHalfWidth;
                _boxHalfLength = StopHalfLength;
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
            string prompt = near && still ? ParkedMark : null;
            if (!ReferenceEquals(prompt, _prompt))
            {
                _prompt = prompt;
            }
        }

        private const string ParkedMark = "parked";   // (a flag: the bay panel is up while the car stands still near the bay)

        // ---------------------------------------------------------------- what the examiner sees

        private void ResetSampling()
        {
            _line = -1;
            _redSince = -1f;
            _pastBooked = false;
            _couldStop = false;
            _lastShunt = -100f;
            _settled = false;
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
                BoxSign();
                Vector3 moved = position - _lastPosition;
                float along = moved.x * forward.x + moved.z * forward.z;
                if (along < 0f && along > -3f)
                {
                    _reversed -= along;
                }
            }
            _lastPosition = position;

            // The red light ahead in their own lane. A line counts once the car has come up to it from behind (not one it
            // started on or pulled out beside), and its red once it has been red with room left to stop firmly.
            if (RoadMap.LineAt(position, heading, out int line, out float past))
            {
                bool red = RoadMap.IsRed(RoadMap.Line(line));
                if (line != _line)
                {
                    // Another lane's line abreast of the one being watched (the car is changing lane, or straddles two
                    // whose lines are a little staggered) is the same line: what is known of the approach is kept.
                    bool same = _line >= 0 && Mathf.Abs(past - _along) < Abreast;
                    _line = line;
                    if (!same)
                    {
                        _along = past;
                        _lineFrom = past;
                        _redSince = -1f;
                        _couldStop = false;
                        _pastBooked = false;
                    }
                    else if (past < _lineFrom)
                    {
                        _lineFrom = past;
                    }
                    _along = Mathf.Min(_along, past);
                }
                if (!red)
                {
                    _redSince = -1f;
                    _couldStop = false;
                }
                else
                {
                    if (_redSince < 0f)
                    {
                        _redSince = now;
                    }
                    _couldStop |= -past >= Mathf.Min(speed * ReactTime + speed * speed / (2f * FirmStop), RoomMost);
                }
                bool theirs = _settled && _lineFrom <= -LineApproach && red && _couldStop && now - _redSince >= 1f && now - _lastShunt >= ShuntAllow;
                if (theirs && _along < 1f && past >= 1f && speed > 1.5f)
                {
                    Fault(TestFault.RedLight, now);
                    _pastBooked = true;
                }
                else if (theirs && !_pastBooked && speed < 0.4f && past > 1.2f && past < 4f)
                {
                    Fault(TestFault.PastLine, now);
                    _pastBooked = true;
                }
                _along = past;
            }
            else
            {
                _line = -1;
            }

            // Harsh braking: the brake held flat from a real speed for all but a full stop. (The brake is a key: a short
            // press is as hard as a long one, so only the length of it can be marked.)
            float decel = (_lastSpeed - speed) / delta;
            if (decel > 9f && (_brakeSince >= 0f || _lastSpeed > HarshFrom))
            {
                if (_brakeSince < 0f)
                {
                    _brakeSince = now;
                }
                else if (now - _brakeSince >= HarshHeld && now - _lastBrake > 6f)
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
                _farOverSince = -1f;
                _laneSince = -1f;
                _pavementSince = -1f;
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
                else if (!_overBooked && now - _overSince >= OverHeld)
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
                else if (!_farOverBooked && now - _farOverSince >= FarOverHeld)
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
            float withLane = runs.x * velocity.x + runs.z * velocity.z;
            if (!_settled)
            {
                // Not yet on the road: the car is where the school left it. In a lane and going its way, or time enough to have got there.
                _settled = now - _driveSince >= SettleAllow || (speed > 3f && lateral < 1.8f && distance < 2.2f && withLane > 0.5f * speed);
                _laneSince = -1f;
                _pavementSince = -1f;
                _wrongSince = -1f;
                return;
            }
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
                // (Standing or crawling, whatever met the car ran into it: not marked.)
                float speed = collision.relativeVelocity.magnitude;
                if (speed >= TouchFrom && _lastSpeed < OwnMove)
                {
                    _lastShunt = now;   // (pushed: a stop line crossed in the next moments is not marked either)
                }
                if (speed < TouchFrom || _lastSpeed < OwnMove)
                {
                    return;
                }
                _lastTouch = now;
                Fault(speed >= CollisionFrom ? TestFault.Collision : TestFault.Touched, now);
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

        // How far the car stands over the edge of the painted box (0 or less: wholly inside it).
        private float OverBox(Vector3 centre, float halfLength, float halfWidth)
        {
            Vector3 local = Quaternion.Inverse(_boxFacing) * (centre - _boxCentre);
            return Mathf.Max(Mathf.Abs(local.x) + halfWidth - _boxHalfWidth, Mathf.Abs(local.z) + halfLength - _boxHalfLength);
        }

        // The marker over the box says whether the car is in it, so there is no guessing before "I have parked" is pressed.
        // Looked at with the examiner's other looks (ten a second); the words change only when the answer does.
        private void BoxSign()
        {
            JobSpots.CarFrame(_car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            bool inside = OverBox(centre, halfLength, halfWidth) <= BoxAllow;
            if (inside != _inBox)
            {
                _inBox = inside;
                _markerLabel = Licences.T(inside ? "reallife_lic_marker_in" : _route.HasBay ? "reallife_lic_marker_bay" : "reallife_lic_marker_stop");
            }
        }

        // Stopped and declared: is the car in the box, straight, and (for the commercial class) backed in?
        private void JudgeParking()
        {
            JobSpots.CarFrame(_car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            if (!_route.HasBay)
            {
                // A stop in the lane: judged by the box painted there, and only by where the car stands (not by how straight).
                float out0 = OverBox(centre, halfLength, halfWidth);
                _logger?.Info("Licences: stopped " + Mathf.Max(0f, out0).ToString("F2") + " m over the edge of the stop box painted in the lane ("
                              + (_boxHalfWidth * 2f).ToString("F1") + " by " + (_boxHalfLength * 2f).ToString("F1") + " m).");
                if (out0 > BoxNear)
                {
                    _faults[(int)TestFault.NotParked]++;
                }
                else if (out0 > BoxAllow)
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
            if (!_screens.Report(this, title, string.Empty, _text.ToString(), _onSheetClosed))
            {
                Licences.Toast(_passed ? NotificationType.Success : NotificationType.Info, "reallife_lic_toast", "text", title + "\n" + _text, 20f);   // no window: the report is the notification
            }
            _objective = Licences.T("reallife_lic_test_over");
            _markerLabel = null;
            _step = Step.Result;
            _resultAt = now;
            _logger?.Info("Licences: " + (_lesson ? "lesson" : "practical test") + " over" + (ended ? " (" + endedKey + ")" : string.Empty) + ": " + minor + " minor, "
                          + serious + " serious" + (_lesson ? "." : _passed ? ": PASSED, " + _class + " licence issued." : ": failed."));
        }

        // The result page was closed by its Close entry or the cancel key: the examiner may go.
        private void SheetClosed()
        {
            if (_step == Step.Result && !_closingSheet)
            {
                CloseSheet(Time.time);
            }
        }

        private void CloseSheet(float now)
        {
            _closingSheet = true;
            if (_screens.Window.OpenFor(this))
            {
                _screens.Window.Close();
            }
            _closingSheet = false;
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
