using System.Globalization;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people at a traffic accident: the drivers, the paramedics and the officers, six at most, placed and directed as
    /// TrafficAccidents reports the crash and its units arriving and leaving. The paramedics' job has an end (stretcher out,
    /// treatment, onto the stretcher, back aboard); the officers work small jobs (SceneOfficer); an unhurt driver is a
    /// witness (SceneWitness); the driver at fault is questioned and ticketed, never arrested.
    /// </summary>
    public sealed partial class AccidentCast
    {
        private const float TickInterval = 0.25f;   // seconds
        private const float CrewRange = 160f;       // metres: a unit parked further from the scene keeps its crew aboard
        private const float SecondCrewChance = 0.6f;
        private const float JogSpeed = 2.4f;        // m/s, cautious guesses used only to plan the crew's time
        private const float WheelSpeed = 1.1f;
        private const float Detour = 1.35f;         // routes go round vehicles and through the entry: this much longer than a straight line
        private const float ReachMinimum = 26f;     // seconds a crew is given to reach the people, at least
        private const float LiftAllowance = 9f;     // seconds to get to the ends of the stretcher, round the casualty's head or feet
        private const float KneelOut = 0.75f;       // metres from the middle of someone lying down to whoever kneels beside them
        private const float CprOut = SceneClips.CprReach + 0.02f;   // ...and to whoever does compressions: that clip's reach, and what a walker stops short by
        private const float LieOut = 1.7f;          // metres from the side of their car to the middle of a driver lying beside it
        private const float StretcherOut = 1.8f;    // ...and to the middle of the stretcher brought up beside them
        private const float TalkRange = 3.2f;       // metres: people this close are talking to each other
        private const int MarkersWanted = 2;        // numbered markers an officer sets out at the point of impact
        private const float BookAfterLeast = 12f;   // seconds after the officers are out before the driver at fault is questioned, at the soonest...
        private const float BookAfterMost = 20f;    // ...and at the latest: they have had a look first
        private const float BookWalkAllow = 20f;    // seconds the officer has to reach them before the questions start where the officer is
        private const float BookLeast = 8f;         // seconds of questions and the ticket being written...
        private const float BookMost = 12f;
        private const float BookStand = 1.4f;       // metres from the driver the officer stands

        // The game has no lying-injured animation. This is the stand-in.
        private const PermanentAnimationType LyingPose = PermanentAnimationType.LayingInTowel01;
        private const PermanentAnimationType PhonePose = PermanentAnimationType.TalkingPhone;
        private const string PhoneProp = "PhoneOff";

        private enum Crew { Away, Working, Returning }

        // The paramedics' job in order; Clearing waits while something standing on the way back is moved.
        private enum Care { Away, Out, Settling, Treating, Lifting, Loading, Clearing, Wheeling, Returning }

        public static IModLogger Logger;

        // A is the driver of the worst-hit car (the casualty in a severe crash); B is the other driver.
        private readonly ActorRole _driverA = new ActorRole();
        private readonly ActorRole _driverB = new ActorRole();
        private readonly ActorRole _medicA = new ActorRole();
        private readonly ActorRole _medicB = new ActorRole();
        private readonly SceneOfficer _officerA = new SceneOfficer();
        private readonly SceneOfficer _officerB = new SceneOfficer();
        private readonly SceneWitness _witness = new SceneWitness();
        private readonly SceneBrief _brief = new SceneBrief();
        private readonly LyingDriver _down;

        private enum Booking { None, Walking, Writing }

        private bool _faultA;              // the worst-hit car's driver is at fault (a reckless driver's own crash), else the other's
        private bool _faultKnown;
        private bool _faultSettled;        // questioned, or found not to be had: decided once a scene
        private float _faultFrom;
        private Booking _booking;
        private float _bookingUntil;
        private ActorRole _booked;         // the driver being questioned: nothing else of the cast moves them meanwhile
        private SceneOfficer _booker;      // ...and the officer doing it, out of their round

        private bool _active;
        private float _nextTick;
        private TrafficAccidents.Severity _severity;
        private Vector3 _scene;
        private Vector3 _roadForward;   // the way the worst car was going: help comes up from behind it

        private bool _driversDue;
        private float _driversAt;
        private bool _hasA;
        private bool _hasB;
        private Vector3 _doorA;
        private Vector3 _doorB;
        private bool _bWillFlee;
        private Vector3 _lieTowards;
        private Vector3 _wreckAt;     // the middle of the worst car: nobody kneels between it and whoever lies beside it
        private bool _hasWreck;
        private SceneCordon _plan;    // the measured scene, from the moment the road is closed (the tape may go up later)
        private bool _mayLie;         // there is clear road beside the worst car for its driver to lie on
        private int _markers;         // evidence markers set out so far
        private bool _lying;          // driver A is on the ground
        private bool _arguing;        // two drivers having it out (a minor bump)...
        private bool _argueOn;        // ...and near enough to each other to be doing it
        private bool _kneelKnown;     // where to kneel beside whoever is lying there has been worked out...
        private Vector3 _kneelFor;    // ...for them lying here
        private ICasualty _patient;   // someone knocked down who is not one of ours; null at a car accident
        private bool _fatal;          // ...and they are dead

        private Care _care = Care.Away;
        private float _careUntil;         // when the current step ends, whatever has or has not happened
        private float _careLatest;        // when they must be on their way back at the very latest
        private float _treatFor;          // seconds they mean to spend with the patient
        private bool _medicsWorked;       // they have been out and are back aboard
        private Vector3 _kneelAt;         // where whoever treats kneels: beside the chest
        private Vector3 _secondKneel;     // where the other of the crew kneels once the stretcher is parked: by the hips
        private ActorRole _treats;        // who kneels at the chest, and has the medic bag
        private bool _cpr;                // whoever treats is doing compressions (dying or dead), not kneeling work
        private Stretcher _stretcher;     // out with this crew, or null
        private ActorRole _bearer;        // who brings the stretcher
        private ActorRole _lead;          // who pushes it back
        private ActorRole _rear;          // ...and who is walked at its leading end, if there are two
        private Vector3 _patientAt;
        private Vector3 _ambulanceDoor;   // behind the ambulance's rear doors
        private Vector3 _ambulanceBack;   // the way its rear faces, along the ground
        private readonly CrewWay _way = new CrewWay();   // the layout at the casualty and the one way to it and back, fixed when the crew steps out
        private bool _wheeled;            // the stretcher is on its line with the crew: nobody has an order of their own
        private float _phaseAt;           // when the step the crew is on began...
        private float _phaseFor;          // ...and the seconds it should take: well past that and out of sight, it is finished at once
        private bool _hurried;            // ...which has been said for this outing
        private bool _gateSaid;           // the crossing of the tape has been said for this stretch
        private bool _heldLane;           // the lane traffic is let past in is held shut for the crew
        private float _blockedSince;      // when something was first found standing on the way back; 0 if nothing was
        private Vector3 _goA, _goB;       // where each of the crew was last sent on foot (without the stretcher)

        private Crew _officers = Crew.Away;
        private Vector3 _patrolDoorA;
        private Vector3 _patrolDoorB;
        private bool _statements;     // an officer is with the drivers, taking their account
        private SceneCordon _cordon;  // the tape the officers work round, once it is up

        public AccidentCast()
        {
            _down = new LyingDriver(this);
        }

        private ICasualty Patient => _patient ?? (_lying ? _down : null);

        public bool PatientDown
        {
            get
            {
                ICasualty patient = Patient;
                return _active && patient != null && patient.Present;
            }
        }

        /// <summary>Where the worst car's driver is to lie in a severe crash, so the cordon is drawn round them too; valid after Begin.</summary>
        public bool WillLie(out Vector3 where, out Vector3 along)
        {
            where = _doorA;
            along = _roadForward;
            return _active && _patient == null && _hasA && _mayLie && _severity == TrafficAccidents.Severity.Severe;
        }

        /// <summary>
        /// The crash has happened and the cars are where they will stay; the drivers get out a few seconds later. The
        /// <paramref name="worst"/> car's driver is the casualty in a severe crash; <paramref name="other"/> may be null, and
        /// its driver stays by the door when <paramref name="otherWillFlee"/> (a hit-and-run).
        /// </summary>
        public void Begin(TrafficAccidents.Severity severity, Vector3 scene, VehicleComponent worst, VehicleComponent other, bool otherWillFlee, float now)
        {
            Clear();
            if (!Settings.People)
            {
                return;
            }
            _active = true;
            _severity = severity;
            _scene = scene;
            _bWillFlee = otherWillFlee;
            _nextTick = now;

            Vector3 along = worst != null ? worst.transform.forward : Vector3.forward;
            along.y = 0f;
            _roadForward = along.sqrMagnitude < 0.01f ? Vector3.forward : along.normalized;

            // Each driver's door, on whichever side has ground. In a severe crash the worst car's driver lies there: further
            // out, where a body's length and a crew's width are clear of every vehicle and fixture; if nowhere is, they are
            // not laid down at all.
            _hasWreck = worst != null;
            if (worst != null)
            {
                TrafficTools.Footprint(worst, out _wreckAt, out _, out _, out _);
            }
            _mayLie = false;
            for (int attempt = 0; attempt < 6 && !_mayLie && worst != null && severity == TrafficAccidents.Severity.Severe; attempt++)
            {
                float side = attempt % 2 == 0 ? StreetSpots.Left : StreetSpots.Right;
                _mayLie = StreetSpots.BesideCar(worst, side, LieOut + (attempt / 2) * 0.5f, out _doorA) && BodyClear(_doorA, _roadForward);
            }
            _hasA = _mayLie || StreetSpots.BesideCar(worst, StreetSpots.Left, 1.1f, out _doorA)
                    || StreetSpots.BesideCar(worst, StreetSpots.Right, 1.1f, out _doorA);
            _hasB = StreetSpots.BesideCar(other, StreetSpots.Left, 1.1f, out _doorB)
                    || StreetSpots.BesideCar(other, StreetSpots.Right, 1.1f, out _doorB);
            _driversDue = _hasA || _hasB;
            _driversAt = now + Random.Range(3f, 6f);
            _lieTowards = _doorA + _roadForward * 6f;
        }

        /// <summary>Which driver caused it: the worst-hit car's or the other's. They stayed, so an officer questions them and writes a ticket.</summary>
        public void AtFault(bool worst)
        {
            _faultA = worst;
            _faultKnown = true;
        }

        /// <summary>
        /// The scene is about someone knocked down who is not of this cast: the crews go to them. The traffic car that hit them
        /// (<paramref name="car"/>, null if none) gives a driver who goes over and phones; <paramref name="fatal"/>: they are
        /// covered and taken away, not treated.
        /// </summary>
        public void BeginCasualty(ICasualty patient, Vector3 scene, VehicleComponent car, bool fatal, float now)
        {
            Clear();
            if (!Settings.People || patient == null)
            {
                return;
            }
            _active = true;
            _patient = patient;
            _fatal = fatal;
            _severity = TrafficAccidents.Severity.Severe;
            _scene = scene;
            _bWillFlee = false;
            _nextTick = now;
            _hasA = StreetSpots.BesideCar(car, StreetSpots.Left, 1.1f, out _doorA)
                    || StreetSpots.BesideCar(car, StreetSpots.Right, 1.1f, out _doorA);
            _driversDue = _hasA;
            _driversAt = now + Random.Range(1.5f, 3f);
            Vector3 along = car != null ? car.transform.forward : Vector3.forward;
            along.y = 0f;
            _roadForward = along.sqrMagnitude < 0.01f ? Vector3.forward : along.normalized;
        }

        // True if a body can lie here along a direction: no vehicle or street furniture within its length, up to knee
        // height (a point test misses legs under a truck).
        private static bool BodyClear(Vector3 at, Vector3 along)
        {
            if (_standMask == 0)
            {
                _standMask = TrafficTools.VehicleMask | LayerMask.GetMask("StreetProps");
            }
            along.y = 0f;
            if (_standMask == 0 || along.sqrMagnitude < 0.01f)
            {
                return true;
            }
            if (StreetSpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            return !Physics.CheckBox(at + Vector3.up * 0.45f, new Vector3(0.5f, 0.35f, 1.2f), Quaternion.LookRotation(along.normalized, Vector3.up),
                _standMask, QueryTriggerInteraction.Ignore);
        }

        private static readonly int HotOnRoadClosed = HotSpots.Slot("Traffic: AccidentCast.OnRoadClosed");
        /// <summary>
        /// The road round the scene has been measured (and closed, unless a minor bump): from now on the people at the
        /// casualty keep inside the tape's rectangle, whether or not the tape is up.
        /// </summary>
        public void OnRoadClosed(SceneCordon cordon)
        {
            using var hotSpot = HotSpots.Time(HotOnRoadClosed);
            _plan = cordon;
        }

        /// <summary>Everyone leaves the street at once. Safe to call at any time, any number of times.</summary>
        public void Clear()
        {
            _booking = Booking.None;
            _booked = null;
            _booker = null;
            _faultKnown = false;
            _faultSettled = false;
            ReleaseWay();
            _plan = null;
            _hasWreck = false;
            _wheeled = false;
            _way.Clear();
            PutStretcherAway();
            _witness.Stop();
            _patient = null;
            _fatal = false;
            _cordon = null;
            _driverA.Exit();
            _driverB.Exit();
            _medicA.Exit();
            _medicB.Exit();
            _officerA.Stop();
            _officerB.Stop();
            _officerA.Role.Exit();
            _officerB.Role.Exit();
            _active = false;
            _driversDue = false;
            _hasA = false;
            _hasB = false;
            _lying = false;
            _mayLie = false;
            _markers = 0;
            _outA = false;
            _outB = false;
            DriversStepped = false;
            _brief.MarkerDue = false;
            _kneelKnown = false;
            _arguing = false;
            _argueOn = false;
            _statements = false;
            _medicsWorked = false;
            _cpr = false;
            _standLogged = false;
            _sideLogged = false;
            _care = Care.Away;
            _officers = Crew.Away;
        }

        private static readonly int HotOnCordonRaised = HotSpots.Slot("Traffic: AccidentCast.OnCordonRaised");
        public void OnCordonRaised(SceneCordon cordon)
        {
            using var hotSpot = HotSpots.Time(HotOnCordonRaised);
            _cordon = cordon;
        }

        public void WaveTrafficOn()
        {
            _officerA.WaveOn();
            _officerB.WaveOn();
        }

        /// <summary>The ambulance is loaded and about to drive through: whoever stands in the road is sent to the kerb until it is over.</summary>
        public void StandAside(float now)
        {
            if (!_active)
            {
                return;
            }
            Brief();
            Vector3 kerb = _brief.Kerbside;
            if ((kerb - _scene).sqrMagnitude < 1f && !StreetSpots.OnPavement(_scene, 9f, out kerb))
            {
                return;   // no kerb to send them to
            }
            _brief.Kerbside = kerb;
            _arguing = false;
            _argueOn = false;
            _witness.Aside(_brief, now);
            // The driver being questioned goes when the officer is done.
            if (_driverA.Present && !_lying && !Booked(_driverA))
            {
                _driverA.Go(kerb, Pace.Walk, _scene, 40f);
            }
            if (_driverB.Present && !Booked(_driverB))
            {
                _driverB.Go(kerb + _roadForward * 0.9f, Pace.Walk, _scene, 40f);
            }
            Logger?.Info("People: the ambulance is about to go: whoever is standing in the road is sent to the kerb.");
        }

        private static readonly int HotOnUnitParked = HotSpots.Slot("Traffic: AccidentCast.OnUnitParked");
        /// <summary>
        /// A unit has parked: its crew gets out and goes to work, to be on its way back by <paramref name="latest"/>. Returns
        /// when the crew expects to be back aboard, which the unit should wait for; 0 if nobody got out.
        /// </summary>
        public float OnUnitParked(bool police, VehicleComponent car, float latest, float now)
        {
            using var hotSpot = HotSpots.Time(HotOnUnitParked);
            if (!_active || !Settings.People || car == null
                || (car.transform.position - _scene).sqrMagnitude > CrewRange * CrewRange)
            {
                return 0f;
            }
            return police ? OfficersOut(car, now) : MedicsOut(car, latest, now);
        }

        /// <summary>The unit is about to go: its crew drops what it is doing and comes back.</summary>
        public void Recall(bool police)
        {
            if (police)
            {
                if (_officers == Crew.Working)
                {
                    _officers = Crew.Returning;
                    EndBooking("the officers were called back");   // a ticket being written is finished in the car
                    _officerA.Stop();
                    _officerA.Role.Board(_patrolDoorA, Pace.Walk);
                    _officerB.Stop();
                    _officerB.Role.Board(_patrolDoorB, Pace.Walk);
                    Logger?.Info("People: officers called back to the patrol car.");
                }
            }
            else if (_stretcher != null && (_care == Care.Out || _care == Care.Settling || _care == Care.Treating))
            {
                // With the stretcher out nothing is cut short but the treatment: it is never left half way, and the
                // patient is lifted from beside it, not from wherever it had got to.
                _treatFor = 0f;
                if (_care == Care.Treating)
                {
                    _careUntil = 0f;
                }
            }
            else if (_care == Care.Out || _care == Care.Treating)
            {
                _care = Care.Treating;
                _careUntil = 0f;   // the next tick has them start back
                _careLatest = 0f;
            }
        }

        public bool CrewOut => _active && _care != Care.Away;

        /// <summary>True when nobody from that unit is still out on the road, so it may drive off.</summary>
        public bool CrewAboard(bool police)
        {
            if (police)
            {
                return _officers != Crew.Working && !_officerA.Role.Present && !_officerB.Role.Present;
            }
            return _care == Care.Away && !_medicA.Present && !_medicB.Present;
        }

        public bool DriversStepped { get; private set; }

        /// <summary>True if that car's driver (the worst-hit car's, or the other's) is out as one of the scene's people.</summary>
        public bool DriverOut(bool worst)
        {
            return worst ? _outA : _outB;
        }

        private bool _outA;
        private bool _outB;

        /// <summary>True once the paramedics have been out, done their job and got back in: the ambulance need wait no longer.</summary>
        public bool CrewWorked => _medicsWorked;

        /// <summary>The unit is leaving, or was taken off the road: whoever belongs to it goes with it.</summary>
        public void OnUnitLeft(bool police)
        {
            if (police)
            {
                EndBooking("the patrol car left");
                _officerA.Stop();
                _officerB.Stop();
                _officerA.Role.Exit();
                _officerB.Role.Exit();
                _officers = Crew.Away;
                return;
            }
            // Whoever is already on the stretcher goes with the ambulance. Anyone still lying where they
            // fell is left to the scene, which takes them away when it ends.
            if (_stretcher != null && _stretcher.Laden && _lying)
            {
                _stretcher.Unload();
                _down.TakeAway();
            }
            ReleaseWay();
            PutStretcherAway();
            _medicA.Exit();
            _medicB.Exit();
            _care = Care.Away;
        }

        public void OnDriverFled()
        {
            if (_driverB.Present)
            {
                ActorPool.PlayDoorSound(_driverB.Position);
                _driverA.Emote(CharacterEmojiName.CommonRedAngry, 3f);
            }
            if (_witness.Active)
            {
                _witness.Stop();
            }
            _driverB.Exit();
            _hasB = false;
            _arguing = false;
            if (!_lying)
            {
                _driverA.Quiet();
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Traffic: AccidentCast.Tick");
        public void Tick(float now)
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (!_active || now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;

            if (_driversDue && now >= _driversAt)
            {
                _driversDue = false;
                DriversOut(now);
            }

            _driverA.Tick(now);
            _driverB.Tick(now);
            _medicA.Tick(now);
            _medicB.Tick(now);
            if (_care != Care.Away)
            {
                TickCare(now);
            }

            Brief();
            bool working = _officers == Crew.Working;
            if (working)
            {
                _officerA.Tick(_brief, now);
                _officerB.Tick(_brief, now);
                if (_brief.MarkerDue)
                {
                    // An officer is down beside what lies at the point of impact: the next numbered marker goes by it.
                    _brief.MarkerDue = false;
                    if (_plan != null && _plan.Mark(_markers))
                    {
                        _markers++;
                        if (_markers == 1)
                        {
                            Logger?.Info("People: an officer is setting out evidence markers at the point of impact.");
                        }
                    }
                    else
                    {
                        _markers = MarkersWanted;   // nothing to mark after all
                    }
                }
            }
            else
            {
                _officerA.Role.Tick(now);
                _officerB.Role.Tick(now);
            }
            TickFault(now);

            Vector3 officer = default;
            bool heard = working && _brief.HasWitness && Listening(out officer);
            _witness.Tick(_brief, heard, officer, now);
            if (heard != _statements)
            {
                TakeStatements(heard, officer);
            }
            if (_arguing && !_statements)
            {
                Argue();
            }
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        /// <summary>The driver lying beside their car, seen as a casualty, so the paramedics treat them as anyone knocked down.</summary>
        private sealed class LyingDriver : ICasualty, ILyingBody
        {
            private readonly AccidentCast _cast;

            public LyingDriver(AccidentCast cast)
            {
                _cast = cast;
            }

            public bool Present => _cast._lying && _cast._driverA.Present;

            public Vector3 Position => _cast._driverA.Position;

            public bool Spine(out Vector3 hips, out Vector3 head)
            {
                hips = default;
                head = default;
                ThirdPersonCharacter character = Present ? _cast._driverA.Character : null;
                Transform low = character != null && character.animator != null ? character.animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                Transform high = low != null ? character.animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (high == null)
                {
                    return false;
                }
                hips = low.position;
                head = high.position;
                return true;
            }

            public bool Lift(out Transform body, out Transform hips, out Transform head)
            {
                body = null;
                hips = null;
                head = null;
                ThirdPersonCharacter character = Present ? _cast._driverA.Lifted() : null;
                if (character == null || character.animator == null)
                {
                    return false;
                }
                body = character.transform;
                hips = character.animator.GetBoneTransform(HumanBodyBones.Hips);
                head = character.animator.GetBoneTransform(HumanBodyBones.Head);
                return hips != null && head != null;
            }

            public void TakeAway()
            {
                _cast._driverA.Exit();
                _cast._lying = false;
            }
        }
    }
}
