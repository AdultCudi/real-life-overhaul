using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people at a traffic accident: the drivers who get out of the crashed cars, the
    /// paramedics from the ambulance and the officers from the patrol car. TrafficAccidents
    /// tells it what happened (the crash, a unit parking, a unit leaving, a driver bolting) and
    /// ticks it; this class decides who stands where and what they do. It keeps positions, not
    /// cars, so a pooled car being recycled cannot leave it holding something stale.
    /// At most six people: two drivers (or, where one car crashed on its own and its driver is
    /// on the ground, that driver and a passer-by who comes over), two paramedics, two officers.
    /// The paramedic who treats carries the medic bag; the officer on the traffic post wears hi-vis.
    /// The paramedics do a job with an end: out with the stretcher (two of them at it whenever there
    /// are two, the bag riding on it), down beside the chest of whoever is lying there (compressions
    /// for someone dying or dead, kneeling work otherwise), then that person onto the stretcher (under a sheet if they are
    /// dead), back to the rear of their own ambulance and in. Nobody who was lying in the road
    /// gets up and walks. The officers each work through a rotation of small jobs (see
    /// SceneOfficer); a driver who is unhurt behaves like a bystander until an officer comes
    /// to them (see SceneWitness).
    /// Everyone travels by their navigation agent only. The wrecks, the parked units and the
    /// tape are cut out of the navigation mesh while the scene lasts (SceneObstacles,
    /// SceneCordon), so routes go round them and through the cordon's entries, and every place
    /// to stand comes from the mesh as it then is.
    /// The same crews attend someone knocked down by a car (BeginCasualty): that person is not
    /// one of this cast, only the patient.
    /// </summary>
    public sealed class AccidentCast
    {
        private const float TickInterval = 0.25f;
        private const float CrewRange = 160f;       // a unit parked further from the scene than this keeps its crew aboard
        private const float SecondCrewChance = 0.6f;
        private const float JogSpeed = 2.4f;        // m/s, cautious guesses used only to plan the crew's time
        private const float WheelSpeed = 1.1f;
        private const float Detour = 1.35f;         // routes go round vehicles and through the entry: this much longer than a straight line
        private const float ReachMinimum = 26f;     // seconds a crew is given to reach the people, at least
        private const float LiftAllowance = 9f;     // seconds to get to the ends of the stretcher, round the casualty's head or feet
        private const float KneelOut = 0.75f;       // metres from the middle of someone lying down to whoever kneels beside them
        private const float CprOut = SceneClips.CprReach + 0.02f;   // ...and to whoever is doing compressions on their chest: the reach of that clip, and what a walker stops short by
        private const float LieOut = 1.7f;          // metres from the side of their car to the middle of a driver lying beside it
        private const float StretcherOut = 1.8f;    // ...and to the middle of the stretcher brought up beside them
        private const float TalkRange = 3.2f;       // people this close are talking to each other
        private const int MarkersWanted = 2;        // numbered markers an officer sets out at the point of impact

        // The game has no lying-injured animation. This is the stand-in.
        private const PermanentAnimationType LyingPose = PermanentAnimationType.LayingInTowel01;
        private const PermanentAnimationType PhonePose = PermanentAnimationType.TalkingPhone;
        private const string PhoneProp = "PhoneOff";

        private enum Crew { Away, Working, Returning }

        // The paramedics' job, in order: out to the patient; to their places beside them; a spell of treatment; each to an
        // end of the stretcher; the lift; (a wait, if something has come to stand on the way back); wheeled back; aboard.
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

        // Whoever the paramedics have come for: the person knocked down, or the driver lying by their car.
        private ICasualty Patient => _patient ?? (_lying ? _down : null);

        /// <summary>True while someone is lying at the scene who has not been taken away.</summary>
        public bool PatientDown
        {
            get
            {
                ICasualty patient = Patient;
                return _active && patient != null && patient.Present;
            }
        }

        /// <summary>
        /// Where the driver of the worst car is going to be lying (a severe crash), so that the
        /// cordon can be drawn round them as well as the wrecks. Valid once Begin has been called.
        /// </summary>
        public bool WillLie(out Vector3 where, out Vector3 along)
        {
            where = _doorA;
            along = _roadForward;
            return _active && _patient == null && _hasA && _mayLie && _severity == TrafficAccidents.Severity.Severe;
        }

        /// <summary>
        /// The crash has happened and the cars are where they will stay. The drivers get out a
        /// few seconds later.
        /// </summary>
        /// <param name="worst">The most damaged car; its driver is the casualty in a severe crash.</param>
        /// <param name="other">The second car, or null.</param>
        /// <param name="otherWillFlee">The second car's driver is going to bolt (a hit-and-run), so stays by the door.</param>
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

            // Someone lying beside the car lies along it, not across the lane.
            Vector3 along = worst != null ? worst.transform.forward : Vector3.forward;
            along.y = 0f;
            _roadForward = along.sqrMagnitude < 0.01f ? Vector3.forward : along.normalized;

            // Where each driver's door is, on whichever side has ground to stand on. In a severe crash the
            // driver of the worst car will be lying there: further out, and where the whole length of a body
            // (and a crew's width beside it) is clear of every vehicle and of street furniture. Either side, then
            // further out; if nowhere is clear, the driver is not laid down at all.
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

        /// <summary>
        /// The scene is about someone knocked down and lying in the road, who is not one of this
        /// cast: the crews go to them instead of to a driver. If a traffic car hit them, its
        /// driver gets out, goes over and phones for help.
        /// </summary>
        /// <param name="car">The car that hit them, already stopped; or null if there is none at the scene.</param>
        /// <param name="fatal">They are dead: covered and taken away, not treated.</param>
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

        // True if someone can lie at a place, along a direction: no vehicle of any kind (the car they got out of
        // included) and no street furniture anywhere within the length of a body and a little to each side, from
        // just above the road to knee height. A point test finds the place clear when the legs are under a truck.
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

        /// <summary>
        /// The road round the scene has been measured (and, unless it is a minor bump, closed): from
        /// now on everyone who works beside the casualty is kept inside the rectangle the tape runs
        /// round, whether or not the tape is up yet, so that nobody and nothing stands across it.
        /// </summary>
        public void OnRoadClosed(SceneCordon cordon)
        {
            _plan = cordon;
        }

        /// <summary>Everyone leaves the street at once. Safe to call at any time, any number of times.</summary>
        public void Clear()
        {
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

        /// <summary>The road has been taped off: from now on there is a traffic post and an edge for the officers to work.</summary>
        public void OnCordonRaised(SceneCordon cordon)
        {
            _cordon = cordon;
        }

        /// <summary>Waiting traffic is being let past the scene: whoever is on the traffic post waves it on.</summary>
        public void WaveTrafficOn()
        {
            _officerA.WaveOn();
            _officerB.WaveOn();
        }

        /// <summary>
        /// The ambulance is about to leave past the scene by the lane traffic is let through in: whoever is on
        /// the traffic post steps out of that lane and waves it through, until <see cref="UnitPast"/> (or for
        /// so many seconds at most). Nothing happens if nobody is on the post.
        /// </summary>
        public void LetUnitPast(float now, float seconds)
        {
            if (_cordon == null || !_cordon.Up || _officers != Crew.Working)
            {
                return;
            }
            Vector3 aside = _cordon.PassPost;
            Vector3 from = _cordon.Oncoming;
            if (!_officerA.LetPass(aside, from, now, seconds))
            {
                _officerB.LetPass(aside, from, now, seconds);   // (whoever is on the post says in the log how they let it past)
            }
        }

        /// <summary>
        /// The ambulance is loaded and about to drive on through where the scene is: whoever is standing about in
        /// the road (an unhurt driver, a passer-by who came over) is sent to the kerb, and stays there until it is over.
        /// </summary>
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
            if (_driverA.Present && !_lying)
            {
                _driverA.Go(kerb, Pace.Walk, _scene, 40f);
            }
            if (_driverB.Present)
            {
                _driverB.Go(kerb + _roadForward * 0.9f, Pace.Walk, _scene, 40f);
            }
            Logger?.Info("People: the ambulance is about to go: whoever is standing in the road is sent to the kerb.");
        }

        /// <summary>The ambulance is past the scene (or is not going after all): the officer goes back to the traffic post.</summary>
        public void UnitPast(float now)
        {
            _officerA.PassDone(now);
            _officerB.PassDone(now);
        }

        /// <summary>
        /// A unit has stopped and parked: its crew gets out and goes to work, walking in from
        /// wherever that is.
        /// </summary>
        /// <param name="latest">When the crew must be on its way back at the very latest.</param>
        /// <returns>When the crew expects to be back aboard, or 0 if nobody got out. The unit should wait at least that long.</returns>
        public float OnUnitParked(bool police, VehicleComponent car, float latest, float now)
        {
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
                    _officerA.Stop();
                    _officerB.Stop();
                    _officerA.Role.Board(_patrolDoorA, Pace.Walk);
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

        /// <summary>True while the paramedics are out of their ambulance.</summary>
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

        /// <summary>True once the drivers have got out of their cars (those of them who could be had).</summary>
        public bool DriversStepped { get; private set; }

        /// <summary>True if that car's driver is out on the road as one of the scene's people: the worst-hit car's, or the other's.</summary>
        public bool DriverOut(bool worst)
        {
            return worst ? _outA : _outB;
        }

        private bool _outA;
        private bool _outB;

        /// <summary>True once the paramedics have been out, done what they came for and got back in: the ambulance need not wait any longer.</summary>
        public bool CrewWorked => _medicsWorked;

        /// <summary>The unit is leaving, or was taken off the road: whoever belongs to it goes with it.</summary>
        public void OnUnitLeft(bool police)
        {
            if (police)
            {
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

        /// <summary>The second driver has jumped back into their car and driven off.</summary>
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

        public void Tick(float now)
        {
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

            // Who an officer is actually standing with, if anyone: accounts are given to someone who is there.
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

        // ---------------------------------------------------------------- drivers

        private void DriversOut(float now)
        {
            Brief();
            DriversStepped = true;
            if (_patient != null)
            {
                // The driver who hit someone: out of the car and over to them.
                if (_hasA && _driverA.Enter(_doorA, ActorLook.Civilian, 2f, onRoad: true))
                {
                    _outA = true;
                    ActorPool.PlayDoorSound(_doorA);
                    _witness.Begin(_driverA, _brief, now);
                }
                return;
            }

            bool gotA = _hasA && _driverA.Enter(_doorA, ActorLook.Civilian, 2f, onRoad: true);
            bool gotB = _hasB && _driverB.Enter(_doorB, ActorLook.Civilian, 2f, onRoad: true);
            _outA = gotA;
            _outB = gotB;
            if (gotA)
            {
                ActorPool.PlayDoorSound(_doorA);
            }
            if (gotB)
            {
                ActorPool.PlayDoorSound(_doorB);
            }
            Vector3 a = gotA ? _driverA.Position : _doorA;
            Vector3 b = gotB ? _driverB.Position : _doorB;

            switch (_severity)
            {
                case TrafficAccidents.Severity.Minor:
                    if (gotA && gotB)
                    {
                        // One walks over to the other and they have it out (see Argue): nobody waves their
                        // arms until they are face to face. Someone about to bolt stays by their door.
                        if (_bWillFlee)
                        {
                            _driverB.Stand(a);
                            _driverA.Go(Towards(b, a, 1.7f), Pace.Walk, b);
                        }
                        else
                        {
                            _driverA.Stand(b);
                            _driverB.Go(Towards(a, b, 1.7f), Pace.Walk, a);
                        }
                        _arguing = true;
                    }
                    else
                    {
                        // Only one car, or only one person to be had: they look at the damage and make a call.
                        _driverA.Stand(_scene);
                        _driverB.Stand(_scene);
                        _driverA.Hold(PhonePose, PhoneProp);
                        _driverB.Hold(PhonePose, PhoneProp);
                    }
                    break;

                case TrafficAccidents.Severity.Moderate:
                    // The driver of the damaged car stays by it and phones; the other comes over and waits about.
                    _driverA.Stand(_scene);
                    _driverA.Hold(PhonePose, PhoneProp);
                    WitnessBegins(gotB, a, now);
                    break;

                default:
                    // Severe: the worst car's driver is lying in the road beside it; the other sees to them.
                    if (gotA && _mayLie && BodyClear(_driverA.Position, _roadForward))
                    {
                        _lying = true;
                        _driverA.Stand(_lieTowards);
                        _driverA.Hold(LyingPose);
                        Brief();   // there is now someone on the ground
                    }
                    else if (gotA)
                    {
                        // Nowhere beside the wreck where a whole body lies clear of every vehicle: nobody is put half under one.
                        _driverA.Stand(_scene);
                        _driverA.Hold(PhonePose, PhoneProp);
                        Logger?.Info("People: no clear road beside the wreck for its driver to lie on (a vehicle or street furniture within a body's length); they stay on their feet.");
                    }
                    WitnessBegins(gotB, a, now);
                    if (!gotB && _lying && !_hasB && !_bWillFlee)
                    {
                        Bystander(now);
                    }
                    break;
            }
        }

        // One car on its own with its driver on the ground: until help comes there would be nobody on the street
        // who had noticed. Someone comes over from the pavement (from out of sight if they can) and does what an
        // unhurt driver would: goes down beside them, phones, looks for the ambulance, and is sent aside by the police.
        private void Bystander(float now)
        {
            if (StreetSpots.PavementAround(_scene, 7f, 16f, true, out Vector3 from) && _driverB.Enter(from, ActorLook.Civilian, 2f, onRoad: true))
            {
                _witness.Begin(_driverB, _brief, now);
                Logger?.Info("People: a passer-by comes over to the driver lying in the road.");
            }
        }

        // The other driver: a bystander with something to do, unless they are about to run (they stay by their door).
        private void WitnessBegins(bool gotB, Vector3 a, float now)
        {
            if (!gotB)
            {
                return;
            }
            if (_bWillFlee)
            {
                _driverB.Stand(_hasA ? a : _scene);
            }
            else
            {
                _witness.Begin(_driverB, _brief, now);
            }
        }

        // True if an officer who is taking an account is actually standing with the witness.
        private bool Listening(out Vector3 officer)
        {
            officer = default;
            SceneOfficer with = _officerA.Interviewing ? _officerA : _officerB.Interviewing ? _officerB : null;
            if (with == null)
            {
                return false;
            }
            officer = with.Role.Position;
            return (officer - _brief.Witness).sqrMagnitude < TalkRange * TalkRange;
        }

        // An officer has walked up to take an account, or has left again. While one is there, whoever is on
        // their feet and not already telling it (the witness does that for themselves) tells it, with their
        // hands, facing the officer. When the officer goes, the hands stop: nobody talks to the air.
        private void TakeStatements(bool heard, Vector3 officer)
        {
            _statements = heard;
            bool aTalks = _driverA.Present && !_lying && _patient == null && (officer - _driverA.Position).sqrMagnitude < TalkRange * TalkRange * 2f;
            bool bTalks = _driverB.Present && !_witness.Active && (officer - _driverB.Position).sqrMagnitude < TalkRange * TalkRange * 2f;
            if (!heard)
            {
                if (!_lying && _patient == null)
                {
                    _driverA.Quiet();
                }
                if (!_witness.Active)
                {
                    _driverB.Quiet();
                }
                _argueOn = false;
                return;
            }
            if (aTalks)
            {
                _driverA.Drop();
                _driverA.Face(officer);
                _driverA.Repeat(AnimationType.IDontThinkSo, 5f, 9f);
            }
            if (bTalks)
            {
                _driverB.Drop();
                _driverB.Face(officer);
                _driverB.Repeat(AnimationType.IDontThinkSo, 4f, 8f);
            }
            if (_severity == TrafficAccidents.Severity.Minor && aTalks)
            {
                _driverA.Emote(CharacterEmojiName.CommonYellowNeutral);
            }
        }

        // Two drivers having it out: only while both are there and face to face.
        private void Argue()
        {
            bool near = _driverA.Present && _driverB.Present && _driverA.Settled && _driverB.Settled
                        && (_driverA.Position - _driverB.Position).sqrMagnitude < TalkRange * TalkRange;
            if (near == _argueOn)
            {
                return;
            }
            _argueOn = near;
            if (near)
            {
                _driverA.Face(_driverB.Position);
                _driverB.Face(_driverA.Position);
                _driverA.Repeat(AnimationType.IDontThinkSo, 4f, 8f, CharacterEmojiName.CommonRedAngry, 0.5f);
                _driverB.Repeat(AnimationType.IDontThinkSo, 4.5f, 9f, CharacterEmojiName.CommonRedAngry, 0.4f);
            }
            else
            {
                _driverA.Quiet();
                _driverB.Quiet();
            }
        }

        // ---------------------------------------------------------------- paramedics

        // How long each part of sending the crew out took, in milliseconds, for the log: this is the one frame of an
        // accident in which a way is planned, the cordon is run round it and a lane is shut.
        private static readonly System.Diagnostics.Stopwatch OutWatch = new System.Diagnostics.Stopwatch();
        private float _msPlan, _msCrew, _msWheel, _msCordon, _msLane;

        private static float Lap()
        {
            float ms = (float)OutWatch.Elapsed.TotalMilliseconds;
            OutWatch.Restart();
            return ms;
        }

        private float MedicsOut(VehicleComponent ambulance, float latest, float now)
        {
            SessionWatch.Mark("accident: the ambulance crew being sent out (their way planned, the cordon run round it, a lane shut)");
            _msPlan = _msCrew = _msWheel = _msCordon = _msLane = 0f;
            OutWatch.Restart();
            // Out at the rear doors of their own vehicle; a side door or the road beside it if the rear is walled in.
            Vector3 forward = ambulance.transform.forward;
            forward.y = 0f;
            _ambulanceBack = forward.sqrMagnitude < 0.01f ? Vector3.back : -forward.normalized;
            if (!StreetSpots.BehindCar(ambulance, 1.3f, out _ambulanceDoor)
                && !StreetSpots.BesideCar(ambulance, StreetSpots.Right, 1f, out _ambulanceDoor)
                && !StreetSpots.BesideCar(ambulance, StreetSpots.Left, 1f, out _ambulanceDoor))
            {
                Logger?.Warn("People: no clear ground by the ambulance for its crew to step out onto; the paramedics stay aboard.");
                return 0f;
            }
            ICasualty patient = Patient;
            bool down = patient != null && patient.Present;   // someone lying there: the stretcher comes out, and both of the crew if they can be had

            // The casualty if there is one, else the driver of the worst car, else the car itself. For someone lying
            // there, everything is fixed now, before anyone steps out: how they lie, the layout beside them (CrewWay.Lay)
            // and the one way on the road from the rear doors to it and back (CrewWay.Plan).
            Vector3 from = _ambulanceDoor;
            Lie(patient, from, out _patientAt, out Vector3 axis, out _);
            _cpr = false;
            _stretcher = null;
            _bearer = null;
            _lead = null;
            _rear = null;
            _medicsWorked = false;
            _wheeled = false;
            _hurried = false;
            _gateSaid = false;
            _blockedSince = 0f;
            ReleaseWay();
            _way.Clear();
            bool planned = false;
            if (down)
            {
                // Compressions for someone who is dying or dead, kneeling work for anyone else; the first
                // from right beside the chest (the clip's hands work 0.40 m in front), the second a little off.
                _cpr = _fatal || _patient == null;
                bool beside = _patient == null && _hasWreck && Flat(_wreckAt - _patientAt).sqrMagnitude < 4f * 4f;
                _way.Lay(_patientAt, axis, from, _cpr ? CprOut : KneelOut, _cpr ? 0f : 0.25f, KneelOut, beside, _wreckAt);
                _stretcher = Stretcher.Take();
                planned = _stretcher != null && _way.Plan(ambulance, _plan);
                _msPlan = Lap();
                _kneelAt = _way.Kneel;   // (after the plan: the stretcher may stand on the other side of them from the one first chosen)
                _secondKneel = _way.Second;
                if (!planned)
                {
                    Check("set off", false, _stretcher == null ? "there is no stretcher to be had this session"
                        : $"no way for the stretcher could be planned ({_way.Why}); rear doors to its place {_way.Straight:F1} m straight"
                          + (_way.Failed != null ? "; its place failed its clear test: " + _way.Failed : "")
                          + "; the stretcher stays in the ambulance, the crew goes on foot with the bag and treats where the casualty lies");
                    _stretcher = null;
                }
            }

            // With the stretcher: whoever pushes it steps out on the middle line behind the doors (it comes out sideways,
            // towards the side of the ambulance the way runs along); the other at its leading end, where they will walk.
            Vector3 right = ambulance.transform.right;
            Vector3 second = planned ? _way.SecondOut : _ambulanceDoor;
            Vector3 pusher = planned ? _way.Door : _ambulanceDoor + right * 0.9f;
            OutWatch.Restart();
            bool gotA = _medicA.Enter(second, ActorLook.Paramedic, 2f, onRoad: true);
            bool gotB = (down || Random.value < SecondCrewChance) && _medicB.Enter(pusher, ActorLook.Paramedic, 2f, onRoad: true);
            _msCrew = Lap();
            if (!gotA && !gotB)
            {
                _stretcher = null;
                Logger?.Warn("People: nobody could be had to play the paramedics (every character is in use, or there is no ground to stand on).");
                return 0f;
            }
            ActorPool.PlayDoorSound(_ambulanceDoor);
            if (!down)
            {
                _patientAt = _driverA.Present ? _driverA.Position : _hasA ? _doorA : _scene;
            }
            float straight = Vector3.Distance(_patientAt, from);
            float distance = straight * Detour;
            float reachFor = ReachFor(distance, false);

            // Whoever treats has the medic bag: in the hand while they walk without the stretcher, on the bed
            // while their hands are on it, on the ground beside them while they kneel.
            ActorRole treats = gotA ? _medicA : _medicB;
            _treats = treats;
            treats.CarryBag(true);
            bool paced = false;   // somebody walks in a clip made for one pace: the crew keeps to it, and is given the time
            if (down && planned)
            {
                // One pushing, the other (if there are two) walked at its leading end, the bag riding on it: along the
                // way planned, the stretcher moving all of them. From here until it stands beside the casualty nobody
                // has an order of their own, and nothing but the end of that line ends this step.
                _bearer = gotB ? _medicB : _medicA;
                ActorRole other = gotA && gotB ? _medicA : null;
                OutWatch.Restart();
                _wheeled = _stretcher.Wheel(_bearer, other, _way.Out, _way.Guide, _way.Handle, _way.Swing, _way.Bed, _way.Heading);
                _msWheel = Lap();
                if (_wheeled)
                {
                    paced = true;
                    distance = _way.Out.Travel;
                    if (_plan != null)
                    {
                        _plan.SetCorridor(_way.Out, _way.Home);   // the tape opens where it crosses; no post, cone or barrier stands on it
                        _msCordon = Lap();
                        HoldLane();
                        _msLane = Lap();
                    }
                    SetOffCheck(gotA && gotB);
                }
                else
                {
                    Check("set off", false, "the stretcher could not be taken out (whoever was to push it cannot be moved); the crew goes on foot with the bag");
                    planned = false;
                    _stretcher = null;
                }
            }
            if (down && !_wheeled)
            {
                paced = ToTheChest(treats, Pace.Jog, reachFor);
                reachFor = ReachFor(distance, paced);
                if (gotA && gotB)
                {
                    _goB = _secondKneel;
                    Send(_medicB, _secondKneel, paced ? Pace.Walk : Pace.Jog, _patientAt, reachFor);
                    _medicB.Kneel();
                }
            }
            else if (!down)
            {
                // Nobody on the ground: they see to whoever was in the worst car, on their feet.
                Vector3 near = Towards(_patientAt, from, 1.4f);
                Vector3 apart = Vector3.Cross(Vector3.up, Flat(_patientAt - from).normalized) * 0.8f;
                paced = treats.Gait(SceneClips.CarryBag);
                reachFor = ReachFor(distance, paced);
                Pace pace = paced ? Pace.Walk : Pace.Jog;
                _goA = Stand(near - apart, "paramedic seeing to the driver");
                _goB = Stand(near + apart, "second paramedic seeing to the driver");
                _medicA.Go(_goA, pace, _patientAt, reachFor);
                _medicB.Go(_goB, pace, _patientAt, reachFor);
            }

            // Out, a spell with the patient, then back at a walk with the stretcher.
            float home = _wheeled ? _way.Home.Travel + (_way.Swing ? 4f : 0f) + (_way.Square ? 0f : 4f) : distance;
            float back = home / WheelSpeed + (down ? LiftAllowance + 8f : 3f);
            float reach = distance / (paced ? SceneClips.WalkPace : JogSpeed) + (_wheeled && _way.Swing ? 6f : 2f);
            float treat = _fatal ? Random.Range(5f, 8f) : down ? Random.Range(9f, 16f) : Random.Range(8f, 14f);
            _careLatest = Mathf.Max(now + reach + 4f, latest - back);
            _careUntil = now + reachFor;
            _care = Care.Out;
            _treatFor = treat;
            Phase(now, reach);
            Logger?.Info($"People: paramedics out ({(gotA && gotB ? 2 : 1)}){(_stretcher != null ? " with the stretcher" : "")}, {straight:F0} m from "
                         + (down ? "the casualty" : "the driver") + (paced ? $", at the {SceneClips.WalkPace:F1} m/s their walking clips are made for" : "")
                         + $"; back aboard in about {reach + treat + back:F0} s. Sending them out took: the way {_msPlan:F1} ms ({_way.Cost}), the two characters {_msCrew:F0} ms, "
                         + $"the stretcher taken {_msWheel:F0} ms, the cordon run round it {_msCordon:F0} ms, the lane shut {_msLane:F0} ms.");
            return now + reach + treat + back;
        }

        // The crew's self-check, one line a step, each with what was measured against what is wanted and PASS or FAIL:
        // set off, through the opening, parked beside, lifted, set off back, loaded. A run can be judged from these alone.
        private static void Check(string step, bool pass, string measured)
        {
            string line = "People: crew check (" + step + "): " + measured + ": " + (pass ? "PASS." : "FAIL.");
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        private const float PlaceWithin = 0.1f;    // metres the stretcher may stand from where it belongs...
        private const float AngleWithin = 3f;      // ...and degrees it may be off the way it should point
        private const float HandleWithin = 0.25f;  // metres someone may stand from the end of the stretcher they are to take
        private const float DoorsWithin = 0.15f;   // metres its leading end may be off the 0.25 m it is loaded from
        private const float TapeWithin = 0.18f;    // metres anything of the cordon must keep from the crew at the least (0.30 is laid out)

        // Step 1: the way as planned, where the ambulance stands for it, and that the stretcher and whoever pushes it start on it.
        private void SetOffCheck(bool two)
        {
            Vector3 pusher = _bearer.Position;
            Vector3 bed = _stretcher.transform.position;
            _way.Out.Pose(0f, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out _, out _);
            float pusherOff = Flat(pusher - new Vector3(pusherX, 0f, pusherZ)).magnitude;
            float bedOff = Flat(bed - new Vector3(bedX, 0f, bedZ)).magnitude;
            bool clearAhead = _way.Between == null;
            bool pass = pusherOff <= PlaceWithin && bedOff <= PlaceWithin && clearAhead && _way.HomeChecked;
            Check("set off", pass,
                $"way out {_way.Out.Travel:F1} m with {_way.Out.Corners} corner(s) rounded to 1.2 m" + (_way.Swing ? ", then a swing-in from 2.1 m off the casualty's line" : ", a run-in along the casualty")
                + $", way back {_way.Home.Travel:F1} m with {_way.Home.Corners}" + (_way.Square ? ", round behind the ambulance to the doors square" : ", swung square at the doors (no room to turn behind)")
                + (_way.HomeChecked ? "" : " (NOT clear when planned: something stands behind the ambulance)")
                + $"; rear doors to the stretcher's place {_way.Straight:F1} m straight; by the ambulance's {(_way.AwaySide < 0f ? "left" : "right")} side"
                + (_way.Tier == 0 ? ", on the carriageway" : ", wherever there was room (not all of it carriageway)")
                + (_way.OpenLane ? ", through the lane traffic is let past in (held shut meanwhile)" : "")
                + (_way.BySide ? "; the stretcher goes to the other side of the casualty from the one first chosen (the only one with a clear way)" : "")
                + (_way.DoorsToTape >= 0f ? $"; rear doors {_way.DoorsToTape:F1} m from the tape" + (clearAhead ? ", nothing between the ambulance and it" : $", and {_way.Between} stands between the ambulance and it") : "; no tape between the ambulance and the casualty")
                + $"; every pose tested against vehicles, fixtures, the tape and the casualty; {(two ? "two" : "one")} of the crew; whoever pushes starts {pusherOff:F2} m from the line, the stretcher {bedOff:F2} m ({PlaceWithin:F2} m allowed)"
                + (_way.Failed != null ? "; (the side first chosen failed its clear test: " + _way.Failed + ")" : ""));
        }

        // Step 2 (and 5 b): said once, when the bed's middle crosses the tape.
        private void GateCheck(float gate, string step)
        {
            if (_gateSaid || gate < 0f || _stretcher == null || _stretcher.Progress < gate)
            {
                return;
            }
            _gateSaid = true;
            float clear = _plan != null ? _plan.CorridorClear : -1f;
            bool taped = _plan != null && _plan.Up && !_plan.Lowered;
            Check(step, !taped || clear >= TapeWithin,
                !taped ? "the stretcher crosses where the tape will run; none is up yet"
                    : $"the stretcher crosses the tape by its opening; the nearest post, cone, barrier or tape is {clear:F2} m from anything of the crew ({TapeWithin:F2} m at least)");
        }

        // The step the crew is on began now and should take so long. (Only for finishing it at once when it is well overdue and nobody can see.)
        private void Phase(float now, float seconds)
        {
            _phaseAt = now;
            _phaseFor = seconds;
        }

        // Nothing here ends a step early in sight of the player. Out of sight (both of the crew switched off by the pool: far away, or
        // behind the camera) and well past its time, a step is finished where it would have ended: the stretcher at the end of its
        // line, whoever was walking at their place.
        private void Overdue(float now)
        {
            if (_stretcher == null || now < _phaseAt + _phaseFor + 8f || _medicA.Visible || _medicB.Visible)
            {
                return;
            }
            _stretcher.Hurry();
            _stretcher.HurryWalks();
            if (!_hurried)
            {
                _hurried = true;
                Logger?.Info("People: the ambulance crew is out of sight and behind its time; the step it was on is finished where it would have ended. (Said once an outing.)");
            }
        }

        // The lane traffic is let past in is the crew's for as long as the stretcher is in it (only if the way has to use it).
        private void HoldLane()
        {
            if (_way.OpenLane && _plan != null && !_heldLane)
            {
                _heldLane = true;
                _plan.HoldForCrew(true);
            }
        }

        private void GiveLaneBack()
        {
            if (_heldLane && _plan != null)
            {
                _plan.HoldForCrew(false);
            }
            _heldLane = false;
        }

        // The crew is off the road (or the scene is over): the lane is given back, and the cordon forgets the way.
        private void ReleaseWay()
        {
            GiveLaneBack();
            if (_plan != null)
            {
                _plan.SetCorridor(null, null);
            }
        }

        // To a place beside the casualty or the stretcher: round a corner of the bed and round the casualty's head or feet where the
        // straight line would go through either. Walked by the stretcher when it is out (so they get exactly there, by exactly that
        // way); on their own navigation agent only when there is no stretcher.
        private void Send(ActorRole who, Vector3 to, Pace pace, Vector3 face, float allow)
        {
            Vector3 at = who.Position;
            bool haveBed = _stretcher != null && _stretcher.Out;
            Vector3 corner = default;
            bool roundBed = haveBed && CrewWay.RoundBed(_stretcher.transform, at, to, out corner);
            bool round = _way.Round(roundBed ? corner : at, to, out Vector3 via);
            if (haveBed && _stretcher.Walk(who, roundBed, corner, round, via, to, face))
            {
                return;
            }
            if (round)
            {
                who.GoVia(via, to, pace, face, allow);
            }
            else
            {
                who.Go(to, pace, face, allow);
            }
        }

        // Seconds a crew is given to reach the people over a distance: jogging, or held to the pace of a walking clip.
        private static float ReachFor(float distance, bool paced)
        {
            return Mathf.Max(ReachMinimum, distance / (paced ? SceneClips.WalkPace : JogSpeed) + 6f);
        }

        // To the casualty's chest with the bag, and down beside it. True if they walk there in the bag-carrying
        // clip, which holds them to its pace.
        private bool ToTheChest(ActorRole medic, Pace pace, float allow)
        {
            bool paced = medic == _treats && medic.Gait(SceneClips.CarryBag);   // (only whoever has the bag walks as if carrying one)
            if (medic == _medicA)
            {
                _goA = _kneelAt;
            }
            else
            {
                _goB = _kneelAt;
            }
            Send(medic, _kneelAt, paced ? Pace.Walk : pace, _patientAt, paced ? Mathf.Max(allow, ReachFor(Flat(_kneelAt - medic.Position).magnitude * Detour, true)) : allow);
            if (_cpr)
            {
                medic.Kneel(SceneClips.Cpr, SceneClips.CprReach);
            }
            else
            {
                medic.Kneel();
            }
            return paced;
        }

        // The paramedics' job, a step at a time. With the stretcher out every step ends when it has been done (the stretcher
        // at the end of its line, both of the crew at their places, the lift played): never on a clock, so nobody is left
        // "working from where they are" or walking off without it. Only the crew on foot, with no stretcher, is on a clock.
        private void TickCare(float now)
        {
            switch (_care)
            {
                case Care.Out:
                    if (_wheeled)
                    {
                        if (StretcherLost(now))
                        {
                            break;
                        }
                        GateCheck(_way.OutGate, "through the opening");
                        Overdue(now);
                        if (_stretcher.Arrived)
                        {
                            ParkStretcher(now);
                        }
                        break;
                    }
                    OnFoot(now);
                    break;

                case Care.Settling:
                    Overdue(now);
                    if (Placed(_medicA) && Placed(_medicB))
                    {
                        Logger?.Info("People: paramedics with the patient.");
                        _care = Care.Treating;
                        _careUntil = Mathf.Min(now + _treatFor, Mathf.Max(now + 2f, _careLatest));
                    }
                    break;

                case Care.Treating:
                    if (now >= _careUntil)
                    {
                        BeginLift(now);
                    }
                    break;

                case Care.Lifting:
                    if (StretcherLost(now))
                    {
                        break;
                    }
                    Overdue(now);
                    if (Placed(_lead) && (_rear == null || Placed(_rear)))
                    {
                        LiftOn(now);
                    }
                    break;

                case Care.Loading:
                    if (now >= _careUntil && !StretcherLost(now))
                    {
                        WheelBack(now);
                    }
                    break;

                case Care.Clearing:
                    if (!StretcherLost(now))
                    {
                        WheelBack(now);
                    }
                    break;

                case Care.Wheeling:
                    if (StretcherLost(now))
                    {
                        break;
                    }
                    GateCheck(_way.HomeGate, "back through the opening");
                    Overdue(now);
                    if (_stretcher.Arrived)
                    {
                        LoadIn();
                    }
                    break;

                case Care.Returning:
                    if (!_medicA.Present && !_medicB.Present)
                    {
                        _care = Care.Away;
                        _medicsWorked = true;
                        Logger?.Info("People: paramedics back aboard.");
                    }
                    break;
            }
        }

        // True if that one of the crew is where they were walked to (or is not there to be walked anywhere).
        private bool Placed(ActorRole who)
        {
            return who == null || !who.Present || (who.Settled && (_stretcher == null || !_stretcher.Walking(who)));
        }

        // The crew on foot with no stretcher (nobody on the ground; or no way for one could be had): by their own navigation
        // agents. Whoever has not arrived when their time is up is given more, and out of sight is put at their place; only
        // at the very latest do they work from where they are, which is then said as what it is.
        private void OnFoot(float now)
        {
            bool there = _medicA.Settled || _medicB.Settled;
            if (!there && now >= _careUntil && now < _careLatest)
            {
                bool seen = _medicA.Visible || _medicB.Visible;
                if (!seen)
                {
                    there = (_medicA.Present && !_medicA.Settled && _medicA.Rise(_goA)) | (_medicB.Present && !_medicB.Settled && _medicB.Rise(_goB));
                }
                if (!there)
                {
                    _careUntil = Mathf.Min(now + 8f, _careLatest);
                    return;
                }
            }
            if (there || now >= _careUntil)
            {
                if (there)
                {
                    Logger?.Info("People: paramedics with the patient.");
                }
                else
                {
                    Check("on foot", false, "the paramedics did not reach the patient on foot by the latest they could; they work from where they are");
                }
                _care = Care.Treating;
                _careUntil = Mathf.Min(now + _treatFor, Mathf.Max(now + 2f, _careLatest));
            }
        }

        // The stretcher is gone from under the crew (it faulted and was put away, or whoever had it is no longer there): said
        // once, and they go back to the ambulance on foot. True if that is what happened.
        private bool StretcherLost(float now)
        {
            if (_stretcher != null && _stretcher.Out && (!_wheeled || _stretcher.Held))
            {
                return false;
            }
            Check("stretcher", false, "the stretcher was lost in the middle of the job (it faulted, or whoever had it left the street); the crew walks back to the ambulance without it");
            _wheeled = false;
            ReleaseWay();
            PutStretcherAway();
            _medicA.Drop();
            _medicB.Drop();
            _medicA.Board(_ambulanceDoor, Pace.Walk, 40f);
            _medicB.Board(_ambulanceDoor, Pace.Walk, 40f);
            _care = Care.Returning;
            return true;
        }

        // The stretcher is at the end of its line, beside the casualty: it stays there, measured, and the crew goes to work.
        private void ParkStretcher(float now)
        {
            _wheeled = false;
            Transform bed = _stretcher.transform;
            float beside = Mathf.Abs(Vector3.Dot(bed.position - _way.Chest, _way.Side));
            float off = Flat(bed.position - _way.Bed).magnitude;
            float skew = Vector3.Angle(bed.forward, _way.Axis);
            skew = skew > 90f ? 180f - skew : skew;
            Check("parked beside", off <= PlaceWithin && skew <= AngleWithin && Mathf.Abs(beside - CrewWay.BedOut) <= PlaceWithin,
                $"the stretcher stands {off:F2} m from its place ({PlaceWithin:F2} m allowed), its middle line {beside:F2} m from the casualty's ({CrewWay.BedOut:F2} m wanted), "
                + $"{skew:F1} deg off parallel ({AngleWithin:F0} allowed)" + (_way.Swing ? ", swung in" : ", wheeled straight in"));
            _stretcher.Park();   // hands off it, and the bag off the bed into the hand of whoever treats
            GiveLaneBack();
            // Nobody kneels where it stands: the places were laid out clear of where it was to stand, which is where it is.
            bool moved = CrewWay.OffTheBed(bed, ref _kneelAt);
            moved |= CrewWay.OffTheBed(bed, ref _secondKneel);
            // The one with the bag goes down at the chest, on the side away from the stretcher; the other, if there
            // are two, at the head or the hips. Each goes round the bed's corner and the head or the feet, never over anything.
            ActorRole treats = _treats != null && _treats.Present ? _treats : _medicA.Present ? _medicA : _medicB;
            ActorRole other = treats == _medicA ? _medicB : _medicA;
            ToTheChest(treats, Pace.Walk, 10f);
            if (other.Present)
            {
                Send(other, _secondKneel, Pace.Walk, _patientAt, 10f);
                other.Kneel();
            }
            Logger?.Info("People: stretcher brought up beside the casualty" + (moved ? "; a place to kneel was moved clear of where it stands" : "") + "; "
                         + (_cpr ? "compressions at the chest." : "they kneel to work at the chest."));
            _care = Care.Settling;
            Phase(now, 8f);
        }

        // Treatment is over. With a stretcher out, each takes an end of it; without, they walk back.
        private void BeginLift(float now)
        {
            _medicA.Drop();
            _medicB.Drop();
            if (_stretcher == null || !_stretcher.Out)
            {
                _medicA.Board(_ambulanceDoor, Pace.Walk, 40f);
                _medicB.Board(_ambulanceDoor, Pace.Walk, 40f);
                _care = Care.Returning;
                Logger?.Info("People: paramedics done, walking back to the ambulance.");
                return;
            }
            _stretcher.Park();
            _wheeled = false;
            // Each to an end of it: whoever will push to the end that trails on the way back (so that it is not turned round
            // with somebody on it more than the way needs), the other to the end that leads. Of two, whichever pairing is
            // the shorter walk in all; round the bed's corner and the casualty's head or feet, never over them.
            Vector3 heading = _way.HomeHeading;
            Vector3 handle = _stretcher.HandleFor(heading);
            Vector3 leading = _stretcher.LeadingFor(heading);
            ActorRole first = _medicA.Present ? _medicA : _medicB;
            ActorRole other = _medicA.Present && _medicB.Present ? _medicB : null;
            bool swap = other != null && Flat(other.Position - handle).magnitude + Flat(first.Position - leading).magnitude
                        < Flat(first.Position - handle).magnitude + Flat(other.Position - leading).magnitude;
            _lead = swap ? other : first;
            _rear = swap ? first : other;
            Send(_lead, handle, Pace.Walk, _stretcher.transform.position, LiftAllowance);
            if (_rear != null)
            {
                Send(_rear, leading, Pace.Walk, _stretcher.transform.position, LiftAllowance);
            }
            _care = Care.Lifting;
            Phase(now, LiftAllowance);
        }

        // The patient goes onto the stretcher as they lie (lifted on by the crew at its ends, where they
        // have the clip for it), or under a sheet.
        private void LiftOn(float now)
        {
            ICasualty patient = Patient;
            bool there = patient != null && patient.Present;
            bool covered = there && (_fatal || !_stretcher.Load(patient));
            if (covered)
            {
                // Dead, or not to be moved as they are: covered, and the body itself is off the street from here.
                _stretcher.Cover();
                patient.TakeAway();
            }
            float leadOff = _lead != null && _lead.Present ? _stretcher.FromAnEnd(_lead.Position) : 0f;
            float rearOff = _rear != null && _rear.Present ? _stretcher.FromAnEnd(_rear.Position) : 0f;
            float lifting = there && !covered ? _stretcher.Lift(_lead, _rear) : 0f;
            Check("lifted", leadOff <= HandleWithin && rearOff <= HandleWithin && (!there || covered || _stretcher.Laden),
                $"whoever pushes back stands {leadOff:F2} m from their end of the stretcher" + (_rear != null ? $", the other {rearOff:F2} m from theirs" : ", alone")
                + $" ({HandleWithin:F2} m allowed); "
                + (!there ? "nobody left to lift, the stretcher goes back empty"
                    : covered ? "the casualty is covered on the stretcher and off the road"
                    : lifting > 0f ? $"the crew lifts the casualty onto it ({lifting:F1} s)" : "the casualty is lifted onto it"));
            if (lifting > 0f)
            {
                _care = Care.Loading;
                _careUntil = now + lifting;
                return;
            }
            WheelBack(now);
        }

        // Wheeled back along the way planned for it: round behind the ambulance and up to the rear doors square (or swung
        // square there). If something has come to stand on that way since it was planned, traffic that is only standing there
        // is taken off the road first (out of view at once, in view after five seconds, as the scene does for its units).
        private void WheelBack(float now)
        {
            Collider inWay = _way.HomeBlocked();
            if (inWay != null && StillWaiting(inWay, now))
            {
                _care = Care.Clearing;
                return;
            }
            ActorRole second = _rear != null && _rear.Present ? _rear : null;
            bool final = !_way.Square;
            bool held = _lead != null && _stretcher.Wheel(_lead, second, _way.Home, -_way.Guide, 1f, final, _way.HomeBed, -_way.Back);
            if (!held && second != null && _stretcher.Wheel(second, null, _way.Home, -_way.Guide, 1f, final, _way.HomeBed, -_way.Back))
            {
                _lead = second;
                _rear = null;
                held = true;
            }
            _wheeled = held;
            _gateSaid = false;
            if (!held)
            {
                StretcherLost(now);
                return;
            }
            HoldLane();
            Vector3 start = _stretcher.HandleFor(_way.HomeHeading);
            float pusherOff = Flat(_lead.Position - start).magnitude;
            Check("set off back", pusherOff <= HandleWithin && inWay == null,
                $"whoever pushes takes it up {pusherOff:F2} m from its handle ({HandleWithin:F2} m allowed)"
                + (second != null ? ", the other of the crew walked at its leading end" : ", alone")
                + $"; way back {_way.Home.Travel:F1} m" + (_way.Swing ? ", swung out to it first" : "")
                + (inWay == null ? "; nothing stands on it" : "; '" + inWay.name + "' (layer " + LayerMask.LayerToName(inWay.gameObject.layer) + ") stands on it and could not be moved"));
            _care = Care.Wheeling;
            Phase(now, _way.Home.Travel / SceneClips.WalkPace + (_way.Swing ? 5f : 1f) + (final ? 5f : 1f));
        }

        // Something stands on the way back. A traffic car that is only standing there (nobody's wreck, no unit, not the
        // player's) is taken off the road, at once if nobody is looking and after five seconds if somebody is; the crew
        // waits for that. Anything else cannot be moved: true only while there is something to wait for.
        private bool StillWaiting(Collider inWay, float now)
        {
            if (_blockedSince <= 0f)
            {
                _blockedSince = now;
            }
            VehicleComponent car = inWay.GetComponentInParent<VehicleComponent>();
            bool movable = car != null && car.gameObject.activeInHierarchy && car.presetPath == null && !TrafficTools.IsAmbulance(car) && !TrafficTools.IsHeld(car)
                           && car.GetComponent<TaxiController>() == null && ((1 << inWay.gameObject.layer) & TrafficTools.TrafficMask) != 0;
            if (!movable || now - _blockedSince > 12f)
            {
                return false;
            }
            if (now - _blockedSince >= 5f || !StreetSpots.IsInView(car.transform.position))
            {
                Logger?.Info("People: '" + car.name + "' has come to stand on the ambulance crew's way back; it is taken off the road before they set off.");
                TrafficTools.Remove(car, "standing on the ambulance crew's way back to the ambulance");
            }
            return true;
        }

        // At the back of the ambulance: stretcher, patient and crew are in, and it can go.
        private void LoadIn()
        {
            // Measured where it matters: from the end of the stretcher that leads to the rear doors themselves, and how square it stands to them.
            float gap = Vector3.Dot(_stretcher.LeadingEnd - _way.RearFace, _way.Back);
            float aside = Flat(_stretcher.LeadingEnd - _way.RearFace - _way.Back * gap).magnitude;
            float skew = Vector3.Angle(_stretcher.Heading, -_way.Back);
            Check("loaded", Mathf.Abs(gap - CrewWay.DoorGap) <= DoorsWithin && aside <= DoorsWithin && skew <= AngleWithin,
                $"the stretcher's leading end is {gap:F2} m from the rear doors ({CrewWay.DoorGap:F2} m wanted, {DoorsWithin:F2} m either way), {aside:F2} m off their middle, "
                + $"{skew:F1} deg off square ({AngleWithin:F0} allowed)" + (_way.Square ? ", brought round behind the ambulance" : ", swung square on the spot") + "; crew aboard");
            if (_stretcher.Laden)
            {
                ICasualty patient = Patient;
                _stretcher.Unload();   // an animated body gets its animator back before its owner puts it away
                if (patient != null)
                {
                    patient.TakeAway();
                }
            }
            _wheeled = false;
            ReleaseWay();
            PutStretcherAway();
            ActorPool.PlayDoorSound(_ambulanceDoor);
            _medicA.Exit();
            _medicB.Exit();
            _care = Care.Away;
            _medicsWorked = true;
        }

        private void PutStretcherAway()
        {
            if (_stretcher != null)
            {
                _stretcher.PutAway();
            }
            _stretcher = null;
            _bearer = null;
            _lead = null;
            _rear = null;
            _treats = null;
        }

        // ---------------------------------------------------------------- officers

        private float OfficersOut(VehicleComponent patrol, float now)
        {
            bool doorA = StreetSpots.BesideCar(patrol, StreetSpots.Left, 1f, out _patrolDoorA);
            bool doorB = StreetSpots.BesideCar(patrol, StreetSpots.Right, 1f, out _patrolDoorB);
            if (!doorA && !doorB)
            {
                Logger?.Warn("People: no clear ground by the patrol car for its crew to step out onto; the officers stay aboard.");
                return 0f;
            }
            if (!doorA)
            {
                _patrolDoorA = _patrolDoorB;
            }
            if (!doorB)
            {
                _patrolDoorB = _patrolDoorA;
            }
            // Two officers whenever two can be had: one for the scene, one for the traffic.
            bool gotA = _officerA.Role.Enter(_patrolDoorA, ActorLook.Officer, 2f, onRoad: true);
            bool gotB = doorB && _officerB.Role.Enter(_patrolDoorB, ActorLook.Officer, 2f, onRoad: true);
            if (!gotA && !gotB)
            {
                Logger?.Warn("People: nobody could be had to play the officers (every character is in use, or there is no ground to stand on).");
                return 0f;
            }
            ActorPool.PlayDoorSound(_patrolDoorA);

            _statements = false;
            _officers = Crew.Working;
            Brief();
            _officerB.OnTraffic = gotB;
            _officerB.Alone = !gotA;
            _officerA.OnTraffic = !gotB;   // alone, the one officer divides their time
            _officerA.Alone = !gotB;
            if (gotA)
            {
                _officerA.Begin(_brief, now);
            }
            if (gotB)
            {
                _officerB.Begin(_brief, now);
            }
            Logger?.Info($"People: officers out ({(gotA && gotB ? 2 : 1)}), {Vector3.Distance(_patrolDoorA, _scene):F0} m from the scene.");
            return 0f;   // they work until they are called back
        }

        // What there is for an officer (and a bystander) to see to, as things stand now.
        private void Brief()
        {
            ICasualty patient = Patient;
            _brief.Scene = _scene;
            _brief.HasBody = patient != null && patient.Present && (_stretcher == null || !_stretcher.Laden);
            if (!_brief.HasBody)
            {
                _brief.Body = _scene;
                _brief.BodyKneel = _scene;
                _kneelKnown = false;
            }
            else if (!_kneelKnown || (patient.Position - _kneelFor).sqrMagnitude > 0.25f)
            {
                // Worked out when they come to rest and again only if they are moved. Paramedics work on
                // the side help comes up on; anyone else kneels on the other side of them.
                _kneelKnown = true;
                _kneelFor = patient.Position;
                Lie(patient, _scene - _roadForward * 15f, out Vector3 chest, out Vector3 axis, out Vector3 side);
                _brief.Body = chest;
                // Beside the driver's own car the far side of them is the car: the officer kneels at their head instead.
                // The same where a held vehicle stands within a metre of that side (someone knocked down beside the car that hit them).
                side = CrewSide(chest, axis, side);
                bool carBeside = (_patient == null && _hasWreck && Flat(_wreckAt - chest).sqrMagnitude < 3.5f * 3.5f)
                                 || VehicleWithin(chest - side * KneelOut + axis * 0.2f, chest - side * KneelOut + axis * 0.2f, VehicleClear);
                _brief.BodyKneel = Stand(carBeside ? chest + axis * 1.05f : chest - side * KneelOut + axis * 0.2f, "officer looking the casualty over");
            }
            _brief.BodyAttended = _care == Care.Settling || _care == Care.Treating || _care == Care.Lifting || _care == Care.Loading
                                  || _care == Care.Clearing || _care == Care.Wheeling || (_care == Care.Out && (_medicA.Settled || _medicB.Settled));
            _brief.CrewOut = _care != Care.Away;
            bool witnessB = _driverB.Present;
            bool witnessA = !witnessB && _driverA.Present && !_lying;
            _brief.HasWitness = witnessA || witnessB;
            _brief.Witness = witnessB ? _driverB.Position : witnessA ? _driverA.Position : _scene;
            _brief.CanMark = _plan != null && _plan.HasImpact && _markers < MarkersWanted;
            if (_brief.CanMark)
            {
                _brief.Evidence = _plan.Evidence(_markers);
            }
            _brief.HasCordon = _cordon != null && _cordon.Up;
            if (_brief.HasCordon)
            {
                _brief.TrafficPost = _cordon.DirectorPost;
                _brief.EntryPost = _cordon.EntryPost;
                _brief.EntryLook = _cordon.EntryLook;
                _brief.Oncoming = _cordon.Oncoming;
                _brief.Kerbside = _cordon.Kerbside;
                for (int i = 0; i < 4; i++)
                {
                    _brief.Corners[i] = _cordon.Corner(i);
                }
            }
            else
            {
                _brief.Oncoming = _scene - _roadForward * 20f;
                _brief.Kerbside = _plan != null && _plan.Surveyed ? _plan.Kerbside : _scene;   // (measured when the road was closed, tape or no tape)
            }
            // Whoever waits outside the tape does not wait on the ambulance crew's way: the corner of the taped area furthest from it instead.
            if (_way.Planned && _plan != null && _plan.Surveyed && OnTheWay(_brief.Kerbside))
            {
                float most = 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = _plan.Corner(i);
                    float clear = Mathf.Min(_way.Out.Clearance(corner.x, corner.z), _way.Home.Clearance(corner.x, corner.z));
                    if (clear > most)
                    {
                        most = clear;
                        _brief.Kerbside = corner;
                    }
                }
            }
        }

        // True if a place is on the crew's way out or back, or within a metre of anything of the crew on it.
        private bool OnTheWay(Vector3 place)
        {
            return Mathf.Min(_way.Out.Clearance(place.x, place.z), _way.Home.Clearance(place.x, place.z)) < 1f;
        }

        // How someone lying down lies: the middle of their chest, the way their body runs (hips to
        // head) and the side of them that faces a given place. Someone who cannot say is a point,
        // lying across the line to that place.
        private static void Lie(ICasualty patient, Vector3 facing, out Vector3 chest, out Vector3 axis, out Vector3 side)
        {
            chest = patient != null ? patient.Position : facing;
            axis = Vector3.zero;
            if (patient is ILyingBody body && body.Spine(out Vector3 hips, out Vector3 head))
            {
                axis = Flat(head - hips);
                chest = hips + (head - hips) * 0.55f;
                chest.y = Mathf.Min(hips.y, head.y);
            }
            Vector3 towards = Flat(facing - chest);
            towards = towards.sqrMagnitude < 0.01f ? Vector3.forward : towards.normalized;
            axis = axis.sqrMagnitude < 0.01f ? Vector3.Cross(Vector3.up, towards) : axis.normalized;
            side = Vector3.Cross(Vector3.up, axis);
            if (Vector3.Dot(side, towards) < 0f)
            {
                side = -side;
            }
        }

        private const float VehicleClear = 1f;   // metres kept between any vehicle and where a crew kneels, parks the stretcher and walks between the two

        // The side of someone lying down that the crew works on. The side help comes up on, unless a
        // vehicle (the car that hit them, their own wreck, anything parked) stands within a metre of where
        // the crew would kneel and the stretcher would wait there and the other side is clear: then that
        // one. With no room on either, the old rule: away from the casualty's own car.
        private Vector3 CrewSide(Vector3 chest, Vector3 axis, Vector3 side)
        {
            if (CrewRoom(chest, axis, side))
            {
                return side;
            }
            if (CrewRoom(chest, axis, -side))
            {
                if (!_sideLogged)
                {
                    _sideLogged = true;
                    Logger?.Info("People: a vehicle stands within " + VehicleClear.ToString("F0") + " m of the casualty on the side help comes up on; the crew works on the other side of them.");
                }
                return -side;
            }
            if (!_sideLogged)
            {
                _sideLogged = true;
                Logger?.Info("People: vehicles stand within " + VehicleClear.ToString("F0") + " m of the casualty on both sides; the crew works where it can.");
            }
            return _patient == null && _hasWreck && Vector3.Dot(side, Flat(_wreckAt - chest)) > 0f ? -side : side;
        }

        // Kneeling at the chest, the stretcher beside the legs, and the way between the two.
        private static bool CrewRoom(Vector3 chest, Vector3 axis, Vector3 side)
        {
            return !VehicleWithin(chest + side * KneelOut + axis * 0.25f, chest + side * StretcherOut - axis * 0.9f, VehicleClear + 0.35f);
        }

        // True if any vehicle (traffic, parked, the player's) comes within a distance of the line between two points at body height.
        private static bool VehicleWithin(Vector3 from, Vector3 to, float distance)
        {
            int vehicles = TrafficTools.VehicleMask;
            return vehicles != 0 && Physics.CheckCapsule(from + Vector3.up * 0.9f, to + Vector3.up * 0.9f, distance, vehicles, QueryTriggerInteraction.Ignore);
        }

        private bool _sideLogged;   // which side the crew works on has been said for this scene

        // The nearest place to a point that someone can stand: on the street and, with the wrecks and
        // parked units cut out of the navigation mesh, clear of every vehicle at the scene. The first
        // planned place of a scene that is not clear as planned is said in the log, with what was in it.
        private Vector3 Stand(Vector3 wanted, string who)
        {
            // Inside the rectangle the tape runs (or will run) round, never across its line; then on the street;
            // then, whatever the navigation mesh says, out of any vehicle it turns out to be in.
            Vector3 planned = wanted;
            if (_plan != null)
            {
                wanted = _plan.KeepInside(wanted, 0.75f);
            }
            bool onStreet = StreetSpots.OnRoad(wanted, 1.5f, out Vector3 spot);
            bool found = onStreet && StreetSpots.OutOfVehicles(ref spot);
            wanted = planned;
            float moved = found ? Flat(spot - wanted).magnitude : 0f;
            if (!_standLogged && (!found || moved > StandSlack))
            {
                _standLogged = true;
                if (_standMask == 0)
                {
                    _standMask = TrafficTools.VehicleMask | LayerMask.GetMask("StreetProps");
                }
                int count = _standMask != 0 ? Physics.OverlapSphereNonAlloc(wanted + Vector3.up * 0.9f, 0.45f, StandHits, _standMask, QueryTriggerInteraction.Ignore) : 0;
                string what = "no walkable road there: a vehicle's cut-out, the tape's wall or the kerb";
                if (count > 0)
                {
                    what = "it is inside '" + StandHits[0].name + "' (layer " + LayerMask.LayerToName(StandHits[0].gameObject.layer) + ")";
                }
                for (int i = 0; i < count; i++)
                {
                    StandHits[i] = null;
                }
                string then = found ? "they stand " + moved.ToString("F1") + " m from it instead." : "nowhere clear within 1.5 m, so they make for it as planned.";
                Logger?.Info("People: the place planned for the " + who + " failed its clear test: " + what + "; " + then + " (First of this scene.)");
            }
            return onStreet ? spot : wanted;
        }

        private const float StandSlack = 0.6f;   // metres a place to stand may be moved before it counts as not clear as planned
        private static readonly Collider[] StandHits = new Collider[4];
        private static int _standMask;
        private bool _standLogged;

        // The point a given distance short of a target, on the line from somewhere to it.
        private static Vector3 Towards(Vector3 target, Vector3 from, float shortBy)
        {
            Vector3 back = Flat(from - target);
            return back.sqrMagnitude < 0.01f ? target : target + back.normalized * shortBy;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        /// <summary>
        /// The driver lying beside their car, seen as a casualty: so that the paramedics treat
        /// one of the cast exactly as they treat someone knocked down who is not.
        /// </summary>
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
