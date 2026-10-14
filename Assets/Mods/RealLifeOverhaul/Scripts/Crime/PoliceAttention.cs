using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using GleyTrafficSystem;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the police do about the player's own offences, in one model:
    ///   seen       an offence only counts if somebody could have seen it: one of the mod's
    ///              officers on the street nearby, or (by a chance set per offence in CrimeLaw)
    ///              a patrol car that happened to be around the corner. Nobody is ever fined
    ///              out of thin air: a patrol car has to appear behind them and stop them.
    ///   comply     stop, the patrol car pulls up behind, an officer walks to the window:
    ///              a ticket for the offences, each named with its amount.
    ///   flee       keep going and it is a pursuit. The bill grows with every quarter minute
    ///              and every further offence.
    ///   escape     break the patrol car's line of sight for long enough (or leave it far
    ///              behind) and they have lost you. No fine, but for the rest of the day your
    ///              car is known: the next offence is always seen and costs half as much again.
    ///   caught     stop (or be stopped) with the patrol car on you after fleeing: the larger
    ///              fine, and some hours in custody. The clock is moved on with the game's own
    ///              time skip and you are released where you were caught.
    /// On foot, crossing against the lights or walking in the road in front of an officer gets
    /// a shouted warning, and now and then a small fine.
    /// Being caught after a hit-and-run (PolicePursuit) ends in the same custody.
    /// One patrol car and one officer at most. Idle, this is one time comparison per frame
    /// plus the timed looks described in PlayerOffences; all of it is off while indoors.
    /// </summary>
    public class PoliceAttention : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("crime", "PoliceAttention", typeof(PoliceAttention));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float TickInterval = 0.1f;
        private const float LookupInterval = 0.5f;
        private const float ComplyWindow = 15f;        // seconds to stop once the lights are on
        private const float StoppedSpeedSqr = 1f;
        private const float TicketDistance = 28f;      // the patrol car is close enough to deal with a stopped driver
        private const float TicketHold = 1.5f;
        private const float BustHold = 3f;
        private const float LostDistance = 240f;
        private const float PursuitTime = 150f;
        private const float PursuitMost = 300f;        // seconds of fleeing in all, however close the patrol car still is
        private const float PatrolGap = 9f;
        private const float CrumbSpacing = 6f;
        private const int MaxCrumbs = 900;
        private const float OffenceCooldown = 25f;     // seconds before another offence can start a new stop
        private const float FootCooldown = 120f;
        private const float StopAllowance = 28f;
        private const float SignalledMost = 60f;       // a stop nobody completes is given up after this long
        private const float OfficerSight = 60f;        // metres within which an officer on foot can notice the player's driving
        private const float OfficerOnWatch = 0.55f;    // the chance that one who is only on patrol does, when close with a clear view
        private const float OfficerAtWork = 0.30f;     // ...and one who is busy at a scene
        private const float AfterTicket = 75f;         // seconds after a ticket before anything new can be noticed

        private enum State { Idle, Summoned, Signalled, Fleeing, Ticketed, Custody }

        public static IModLogger Logger;
        public static PoliceAttention Instance { get; private set; }

        /// <summary>True while the police have business with the player, here or in PolicePursuit. No crime scene starts meanwhile.</summary>
        public static bool IsBusy =>
            (Instance != null && Instance._state != State.Idle) || (PolicePursuit.Instance != null && PolicePursuit.Instance.Busy)
            || Law.Custody.Active;   // being arrested or held (Scripts/Law)

        /// <summary>
        /// True while the player is being pulled over for something they did: from the moment a patrol car is
        /// behind them with its lights on and they have been told to stop, until that is settled (they stop for
        /// the ticket, get away, or are caught). Not the patrol car still on its way, not the ticket being
        /// written, not custody, and not PolicePursuit's wait after a crash. Safe to read at any time.
        /// </summary>
        public static bool PulledOver =>
            Instance != null && (Instance._state == State.Signalled || Instance._state == State.Fleeing);

        /// <summary>
        /// Raised when an officer has the player stopped for traffic offences: the ticket at the window (false) or caught after
        /// fleeing (true). The first argument has one bit per kind of offence on the bill (1 &lt;&lt; (int)Offence). For Licences.
        /// </summary>
        public static event Action<int, bool> Stopped;

        /// <summary>
        /// Takes the player into custody for some hours with the same routine as after a pursuit (for an arrest made by another
        /// feature, such as Licences). False if custody is already running or this component is off.
        /// </summary>
        public static bool TakeIntoCustody(int hours)
        {
            PoliceAttention attention = Instance;
            if (attention == null || attention._custodyRunning || !attention.isActiveAndEnabled)
            {
                return false;
            }
            attention.BeginCustody(Mathf.Clamp(hours, 1, 48));
            return true;
        }

        private readonly PlayerOffences _offences = new PlayerOffences();
        private readonly RoadsideStop _stop = new RoadsideStop();
        private readonly List<Vector3> _trail = new List<Vector3>(128);

        private ResponseUnit _patrol;
        private PursuitBackup _backup;     // the second patrol car, once they have fled for a while
        private float _pace;               // the patrol cars' top speed for this stop, m/s (PursuitPace)
        private float _nextSpeeding;       // speeding is looked at less often than anything else (CrimeLaw.SpeedingCooldown)
        private State _state = State.Idle;
        private Rigidbody _body;           // the player's car, looked up twice a second
        private bool _driving;
        private float _nextLookup;
        private float _nextTick;
        private float _nextOffence;
        private float _nextFootOffence;
        private float _stateEnd;
        private float _holdSince;
        private PursuitSight _sight;       // shared with PolicePursuit: the same driving loses either
        private float _fledAt;
        private float _nextSpawn;
        private int _spawnsTried;
        private Vector3 _lastCrumb;
        private int _fine;                 // for the offences so far
        private Offence _first;            // the one they were stopped for
        private int _knownDay = -1;        // the day on which the player's car is known to the police
        private int _faults;
        private bool _custodyRunning;
        private bool _custodyFaded;
        private bool _custodyResting;
        private int _booked;               // which kinds of offence are already on this stop's bill
        private float _signalledAt;
        private int _owed;                 // damages for cars hit while being pulled over or chased: on the same bill
        private int _hurt;                 // ...and people injured
        private int _killed;               // ...and killed
        private bool _lawHolds;            // the arrest has been handed to Law/Custody: the patrol car stays until its scene is over

        // True while a patrol car is coming for the player or after them: whatever else they do belongs to this stop.
        private bool Pursuing => _state == State.Summoned || _state == State.Signalled || _state == State.Fleeing;

        /// <summary>
        /// The player's car hit a traffic car while being pulled over or chased for a traffic
        /// offence. The damages go on this stop's bill, so there is one pursuit and one
        /// settlement. False if no such stop is on: PolicePursuit then deals with the crash itself.
        /// </summary>
        internal static bool TakeDamages(int liability)
        {
            PoliceAttention attention = Instance;
            if (attention == null || !attention.Pursuing)
            {
                return false;
            }
            attention._owed += Mathf.Max(0, liability);
            Logger?.Info("Police attention: a crash on the way (" + liability + " in damages) goes on the same bill.");
            return true;
        }

        /// <summary>The same for someone on foot hit by the player's car during a stop or a chase.</summary>
        internal static bool TakeCasualty(bool fatal)
        {
            PoliceAttention attention = Instance;
            if (attention == null || !attention.Pursuing)
            {
                return false;
            }
            if (fatal)
            {
                attention._killed++;
            }
            else
            {
                attention._hurt++;
            }
            Notifications.Show(NotificationType.Error, "reallife_pedhit_another", null, 6f);
            Logger?.Info("Police attention: a person hit on the way (" + (fatal ? "killed" : "injured") + ") goes on the same bill.");
            return true;
        }

        /// <summary>The player's car is known to the police for the rest of the day (they got away from something).</summary>
        internal static void KnowCar()
        {
            if (Instance != null)
            {
                Instance._knownDay = TimeHelper.CurrentDay;
            }
        }

        // One charge and one message for everything on the bill: the traffic fines, the damages for any
        // crash on the way and the fines for anyone hurt. The amount shown is all of it.
        private void Settle(int fine, bool ran, string key, string keyWithCrash)
        {
            int owed = _owed;
            int people = ran ? _hurt * PolicePursuit.InjuryRunFine + _killed * PolicePursuit.DeathRunFine
                : _hurt * PolicePursuit.InjuryFine + _killed * PolicePursuit.DeathFine;
            bool killed = _killed > 0;
            _owed = 0;
            _hurt = 0;
            _killed = 0;
            if (owed <= 0 && people <= 0)
            {
                Crime.Fine(fine, key);
                return;
            }
            if (SaveGameManager.Current == null)
            {
                return;
            }
            if (owed > 0)
            {
                GameManager.ChangeMoneySafe(-owed, new TransactionInfo("reallife_transaction_accident_liability"), null, null, force: true);
            }
            GameManager.ChangeMoneySafe(-(fine + people), new TransactionInfo("reallife_transaction_fine"), null, null, force: true);
            if (people > 0)
            {
                PolicePursuit.Weigh(killed);
            }
            Crime.ShowAmount(NotificationType.Error, keyWithCrash, fine + owed + people, 10f);
        }

        private void Awake()
        {
            Instance = this;
            _patrol = new ResponseUnit(gameObject);
            _backup = new PursuitBackup(gameObject);
            PolicePursuit.CaughtAfterFleeing += OnHitAndRunCaught;
        }

        private void OnDestroy()
        {
            PolicePursuit.CaughtAfterFleeing -= OnHitAndRunCaught;
            if (_custodyRunning && _custodyResting)
            {
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);   // the coroutine that would have done it dies with us
            }
            Clear();
            RoadMap.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Crime: PoliceAttention.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;
            try
            {
                Tick(now);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear();
                if (++_faults >= 3)
                {
                    Logger?.Warn("Police attention: switched off for this session after repeated errors (see above).");
                    enabled = false;
                }
            }
        }

        private void Tick(float now)
        {
            if (_state == State.Custody)
            {
                if (_lawHolds && !Law.Custody.OnScene)
                {
                    Clear();   // the screen is dark (or the arrest came to nothing): the patrol car goes
                }
                return;   // the coroutine has it, or Law/Custody does
            }
            if (now >= _nextLookup)
            {
                _nextLookup = now + LookupInterval;
                Lookup();
            }
            if (_state == State.Idle)
            {
                if (Settings.PlayerOffences && ModState.Loaded && TrafficTools.Ready)
                {
                    Detect(now);
                }
                return;
            }
            Pursue(now);
        }

        // Twice a second: which car, if any, is the player driving.
        private void Lookup()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            bool driving = vehicle != null && PlayerHelper.IsUsingVehicle && vehicle.vehicleType != null && vehicle.vehicleType.IsMotorVehicle;
            if (!driving)
            {
                _body = null;
            }
            else if (_body == null || _body.gameObject != vehicle.gameObject)
            {
                _body = vehicle.GetComponent<Rigidbody>();
                _offences.Reset();
            }
            _driving = driving && _body != null;
        }

        // ---------------------------------------------------------------- noticing

        private void Detect(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            if (_driving)
            {
                if (!RoadMap.Ensure())
                {
                    return;
                }
                Offence offence = _offences.Driving(_body, now);
                // Speeding is something every car can now do on every straight, so it is looked at far less often
                // than a red light: once, and then not again for a while, whether or not anyone saw.
                if (offence != Offence.None && now >= _nextOffence && (offence != Offence.Speeding || now >= _nextSpeeding)
                    && !PoliceAttention.IsBusy && LifeEvents.IsSafeForEvent())
                {
                    _nextOffence = now + OffenceCooldown;
                    if (offence == Offence.Speeding)
                    {
                        _nextSpeeding = now + CrimeLaw.SpeedingCooldown;
                    }
                    if (Seen(offence, _body.position))
                    {
                        Summon(offence, now);
                    }
                }
                return;
            }

            if (PlayerHelper.IsUsingVehicle || now < _nextFootOffence)
            {
                return;
            }
            Offence onFoot = _offences.OnFoot(player.transform.position, now);
            if (onFoot != Offence.None)
            {
                _nextFootOffence = now + FootCooldown;
                Shout(onFoot);
            }
        }

        // Whether anyone saw. A patrol car may have happened to be about (a chance per offence), and an
        // officer on foot nearby may have noticed: only one with a clear view (no building between), more
        // likely the nearer they are, and more likely one who is on the lookout (on the beat, by a parked
        // car, at a junction) than one busy at a scene. An officer used to mean a certain ticket; with
        // officers on most streets that would leave no way to drive, so nobody on foot is ever certain.
        private bool Seen(Offence offence, Vector3 at)
        {
            if (!Settings.Police)
            {
                return false;   // no patrol cars: nobody to pull anyone over
            }
            bool speeding = offence == Offence.Speeding;
            bool known = _knownDay == TimeHelper.CurrentDay;
            if (known && !speeding)
            {
                return true;
            }
            float chance = offence == Offence.RedLight ? CrimeLaw.RedLightSeen
                : speeding ? CrimeLaw.SpeedingSeen
                : CrimeLaw.RecklessSeen;
            if (speeding && _body != null && _body.velocity.sqrMagnitude > CrimeLaw.SpeedingGrossKmh * CrimeLaw.SpeedingGrossKmh / (3.6f * 3.6f))
            {
                chance *= CrimeLaw.SpeedingGrossTimes;   // far beyond any limit: that is noticed
            }
            chance *= Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(1f, 10f, Settings.CrimeFrequency));
            float sight = OfficerSight * Events.Weather.SightFactor;   // an officer on foot sees less far in rain, mist and the dark
            StreetActor officer = PlayerOffences.OfficerNear(at, sight);
            if (officer != null && !PursuitSight.Blocked(officer.Position, at))
            {
                // (Someone on foot is a poor judge of a passing car's speed: for speeding they count for a third.)
                float near = Mathf.InverseLerp(sight, sight * 0.4f, Vector3.Distance(officer.Position, at));
                chance = Mathf.Max(chance, (officer.Yields ? OfficerOnWatch : OfficerAtWork) * Mathf.Lerp(0.4f, 1f, near) * (speeding ? CrimeLaw.SpeedingOnFoot : 1f));
            }
            if (known)
            {
                chance = Mathf.Max(chance, CrimeLaw.SpeedingSeenKnown);   // a car they are looking for: likely, but no longer certain
            }
            if (Settings.LawReputation)
            {
                chance *= Law.Reputation.Scrutiny;   // 1, or up to 1.5 for somebody whose record has grown lately
            }
            return Random.value < chance * CivicPolicy.EnforcementScale;   // the traffic policy of the city (1 unless a council has changed it)
        }

        /// <summary>
        /// Another feature knows of something the player is doing at the wheel that the road data cannot show (driving after
        /// drinking: Leisure). It is looked at like any other offence: somebody has to see it, and then they are pulled over.
        /// Safe to call every second: it answers false at once while a stop is on or the last look was under 25 s ago.
        /// </summary>
        internal static bool Report(Offence offence)
        {
            PoliceAttention attention = Instance;
            float now = Time.time;
            if (attention == null || !attention.isActiveAndEnabled || !attention._driving || attention._body == null || now < attention._nextOffence
                || IsBusy || !LifeEvents.IsSafeForEvent())
            {
                return false;
            }
            attention._nextOffence = now + OffenceCooldown;
            if (!attention.Seen(offence, attention._body.position))
            {
                return false;
            }
            attention.Summon(offence, now);
            return true;
        }

        // The officer at the window can tell: drink at the wheel goes on the bill of whatever the stop was for.
        private void Breathalyse()
        {
            if (Leisure.LeisureDirector.Tipsy)
            {
                Book(Offence.DrunkDriving);
            }
        }

        // Each kind of offence goes on a stop's bill once, however long they keep it up.
        private void Book(Offence offence)
        {
            int bit = 1 << (int)offence;
            if ((_booked & bit) == 0)
            {
                _booked |= bit;
                _fine += FineFor(offence);
            }
        }

        private static int FineFor(Offence offence)
        {
            switch (offence)
            {
                case Offence.RedLight: return CrimeLaw.RedLightFine;
                case Offence.Speeding: return CrimeLaw.SpeedingFine;
                case Offence.Reckless:
                case Offence.WrongSide: return CrimeLaw.RecklessFine;
                case Offence.DrunkDriving: return CrimeLaw.DrunkDrivingFine;
                default: return CrimeLaw.JaywalkFine;
            }
        }

        // The officer calls out; one time in five it is a ticket.
        private void Shout(Offence offence)
        {
            StreetActor officer = _offences.Witness;
            ActorPool pool = ActorPool.Instance;
            if (officer != null && pool != null)
            {
                pool.ShowEmoji(officer, CharacterEmojiName.CommonRedAngry, 3f);
            }
            if (Random.value < CrimeLaw.JaywalkFineChance || (Settings.LawReputation && Law.Reputation.NoWarnings))   // (a long record: no warnings)
            {
                Crime.Fine(CrimeLaw.JaywalkFine, offence == Offence.CrossOnRed ? "reallife_crime_jaywalk_fine_red" : "reallife_crime_jaywalk_fine");
            }
            else
            {
                Crime.Show(NotificationType.Warning, offence == Offence.CrossOnRed
                    ? (Random.value < 0.5f ? "reallife_crime_jaywalk_warn_red_1" : "reallife_crime_jaywalk_warn_red_2")
                    : (Random.value < 0.5f ? "reallife_crime_jaywalk_warn_1" : "reallife_crime_jaywalk_warn_2"), 6f);
            }
        }

        // ---------------------------------------------------------------- being stopped

        private void Summon(Offence offence, float now)
        {
            _first = offence;
            _fine = 0;
            _booked = 0;
            Book(offence);
            _offences.Reset();
            _trail.Clear();
            _lastCrumb = _body.position;
            _trail.Add(_lastCrumb);
            _spawnsTried = 0;
            _nextSpawn = now + Random.Range(0.5f, 2f);
            _state = State.Summoned;
            _stateEnd = now + 12f;
            Logger?.Info($"Police attention: {offence} seen.");
        }

        /// <summary>Options-menu test button: a patrol car comes up behind the player's car as if they had just run a red light.</summary>
        public void TestStop()
        {
            try
            {
                Lookup();
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None)
                {
                    why = !_driving ? TestRefusal.NotDriving
                        : _state != State.Idle || PolicePursuit.Instance == null || PolicePursuit.Instance.Busy ? TestRefusal.PoliceBusy
                        : TestRefusal.None;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Traffic stop", why);
                    return;
                }
                Summon(Offence.RedLight, Time.time);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear();
            }
        }

        private void Pursue(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !TrafficTools.Ready)
            {
                // Indoors, or time is being skipped: the street is gone. Whoever was after them has lost them.
                if (_state == State.Signalled || _state == State.Fleeing)
                {
                    Escaped(announce: true);
                }
                else
                {
                    Clear();
                }
                return;
            }
            Vector3 position = _driving ? _body.position : player.transform.position;
            bool stopped = !_driving || _body.velocity.sqrMagnitude < StoppedSpeedSqr;
            Crumb(position);

            // Anything else they do wrong while the police are watching goes on the bill.
            if (_driving && (_state == State.Signalled || _state == State.Fleeing))
            {
                Offence more = _offences.Driving(_body, now);
                if (more != Offence.None)
                {
                    Book(more);
                }
            }

            switch (_state)
            {
                case State.Summoned:
                    if (now >= _nextSpawn && !SpawnPatrol(position))
                    {
                        _nextSpawn = now + 1.5f;
                        if (++_spawnsTried >= 4 || now >= _stateEnd)
                        {
                            Clear();   // no patrol car could get onto the road: nobody saw after all
                        }
                    }
                    break;

                case State.Signalled:
                    if (_patrol.IsLost || now - _signalledAt > SignalledMost)
                    {
                        Clear();
                    }
                    else if (stopped)
                    {
                        _stateEnd = Mathf.Max(_stateEnd, now + 4f);   // waiting for them, not running from them
                        if (Held(now, position, TicketDistance, TicketHold))
                        {
                            Ticket(now);
                        }
                    }
                    else
                    {
                        _holdSince = 0f;
                        if (now >= _stateEnd)
                        {
                            _state = State.Fleeing;
                            _fledAt = now;
                            _stateEnd = now + PursuitTime;
                            _backup.Begin(now);
                            _sight.Reset();
                            Crime.Show(NotificationType.Error, "reallife_crime_stop_fleeing");
                        }
                    }
                    break;

                case State.Fleeing:
                    if (!_patrol.IsLost && _driving && _backup.Tick(now, _body, _pace, PatrolGap))
                    {
                        Logger?.Info($"Police attention: a second patrol car has joined after {now - _fledAt:F0} s of fleeing.");
                    }
                    if (!_patrol.IsLost && now >= _stateEnd && now - _fledAt < PursuitMost && StillOn(position))
                    {
                        // Time is up, but a patrol car is right behind them with the car in sight: it does not turn
                        // round and go home. It carries on a little at a time, up to the longest a pursuit may last.
                        _stateEnd = now + PursuitPace.Extension;
                    }
                    if (_patrol.IsLost || now >= _stateEnd || Unseen(now, position))
                    {
                        Escaped(announce: true);
                    }
                    else if (stopped && Held(now, position, 25f, BustHold))
                    {
                        Bust(now);
                    }
                    else if (!stopped)
                    {
                        _holdSince = 0f;
                    }
                    break;

                case State.Ticketed:
                    _stop.Tick(now);
                    if (_patrol.IsLost || now >= _stateEnd || (now >= _holdSince && _stop.Finished))
                    {
                        Clear();
                    }
                    break;
            }
        }

        // The patrol car has been within reach of the stopped player for long enough.
        private bool Held(float now, Vector3 position, float within, float seconds)
        {
            if ((_patrol.Car.transform.position - position).sqrMagnitude > within * within)
            {
                _holdSince = 0f;
                return false;
            }
            if (_holdSince <= 0f)
            {
                _holdSince = now;
                return false;
            }
            return now - _holdSince >= seconds;
        }

        // True once the patrol car has been without sight of the player for long enough, or is hopelessly far behind.
        private bool Unseen(float now, Vector3 position)
        {
            Vector3 from = _backup.Nearer(_patrol.Car.transform.position, position);
            return (from - position).sqrMagnitude > LostDistance * LostDistance || _sight.Lost(from, position, now);
        }

        // True while a patrol car is close behind the player with them in sight (one line test, asked when the time runs out).
        private bool StillOn(Vector3 position)
        {
            Vector3 from = _backup.Nearer(_patrol.Car.transform.position, position);
            return (from - position).sqrMagnitude < PursuitPace.CloseEnough * PursuitPace.CloseEnough && !PursuitSight.Blocked(from, position);
        }

        // The trail the patrol car follows, so that it stays on roads the player really drove.
        private void Crumb(Vector3 position)
        {
            if ((position - _lastCrumb).sqrMagnitude < CrumbSpacing * CrumbSpacing)
            {
                return;
            }
            _lastCrumb = position;
            _backup.Crumb(position, MaxCrumbs);
            List<Vector3> points = _patrol.InUse ? _patrol.Path : _trail;
            if (points.Count < MaxCrumbs)
            {
                points.Add(position);
            }
        }

        // A patrol car on the road the player came along, some way behind them, lights on.
        private bool SpawnPatrol(Vector3 position)
        {
            if (!_driving)
            {
                return false;
            }
            Vector3 origin = _trail.Count > 0 ? _trail[0] : position;
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(origin, _body.velocity, 40f);
            if (!_patrol.Spawn(UnitKind.Police, anchor, Random.Range(45f, 80f), 25f))
            {
                return false;
            }
            _patrol.Path.AddRange(_trail);
            _trail.Clear();
            EmergencyDriver driver = _patrol.Driver;
            driver.Begin(_patrol.Car, _patrol.Path, position, PatrolGap);
            driver.Follow(_body, PatrolGap);
            _pace = PursuitPace.For(_body);
            driver.CruiseSpeed = _pace;
            Logger?.Info($"Police attention: patrol car out for {_first}, top speed {_pace * 3.6f:F0} km/h.");
            _state = State.Signalled;
            _signalledAt = Time.time;
            _stateEnd = Time.time + ComplyWindow;
            _holdSince = 0f;
            Crime.Show(NotificationType.Warning, PullOverKey(_first), 9f);
            return true;
        }

        private static string PullOverKey(Offence offence)
        {
            switch (offence)
            {
                case Offence.RedLight: return "reallife_crime_pullover_redlight";
                case Offence.Speeding: return "reallife_crime_pullover_speeding";
                case Offence.WrongSide: return "reallife_crime_pullover_wrongside";
                case Offence.DrunkDriving: return "reallife_crime_pullover_drunk";
                default: return "reallife_crime_pullover_reckless";
            }
        }

        private static string TicketKey(Offence offence)
        {
            switch (offence)
            {
                case Offence.RedLight: return "reallife_crime_ticket_redlight";
                case Offence.Speeding: return "reallife_crime_ticket_speeding";
                case Offence.WrongSide: return "reallife_crime_ticket_wrongside";
                case Offence.DrunkDriving: return "reallife_crime_ticket_drunk";
                default: return "reallife_crime_ticket_reckless";
            }
        }

        // They stopped. The officer walks up; the ticket names the offence and the amount.
        private void Ticket(float now)
        {
            Breathalyse();
            int fine = Known(_fine);
            int killed = _killed;
            int paid = fine + _owed + _hurt * PolicePursuit.InjuryFine + _killed * PolicePursuit.DeathFine;
            Justice.Summon(JusticeReports.Struck(_hurt, killed, true, paid - fine - _owed));   // a death on the way goes to court as it does after a pursuit (null, and nothing, otherwise)
            // More than one kind of offence on the bill: the ticket says so instead of naming only the first.
            Settle(fine, false, (_booked & (_booked - 1)) != 0 ? "reallife_crime_ticket_several" : TicketKey(_first), "reallife_crime_ticket_crash");
            Logger?.Info($"Police attention: ticket for {_first}, {fine}.");
            Justice.PoliceContact(driving: true);   // a bench warrant is served, a suspended licence charged (Scripts/Justice)
            Stopped?.Invoke(_booked, false);
            _backup.Release(TrafficTools.Ready);   // one car makes the stop
            _patrol.Park();
            _state = State.Ticketed;
            _nextOffence = now + AfterTicket;   // dealt with: nobody is pulled over twice in a minute
            Law.Arrests.Cited(fine);
            if (Law.Arrests.StayedAtScene(killed, paid, _patrol.Car))
            {
                _state = State.Custody;   // somebody died under their wheels on the way: no ticket at the window, an arrest
                _lawHolds = true;
                return;
            }
            _holdSince = now + Random.Range(8f, 12f);   // (from here: the earliest the patrol car leaves)
            _stateEnd = _holdSince;
            if (WalkUp(now))
            {
                _stateEnd = _holdSince + StopAllowance;
            }
        }

        // Caught after running: everything so far, the fine for fleeing, and a few hours in a cell.
        private void Bust(float now)
        {
            int fleeing = Mathf.Min(CrimeLaw.FleeingMost,
                CrimeLaw.FleeingFine + Mathf.FloorToInt((now - _fledAt) / 15f) * CrimeLaw.FleeingPerQuarterMinute);
            Breathalyse();
            int fine = Known(_fine) + fleeing;
            int hours = Random.Range(CrimeLaw.CustodyHoursMin, CrimeLaw.CustodyHoursMax + 1);
            int hurt = _hurt, killed = _killed;
            bool crashed = _owed > 0;
            int paid = fine + _owed + hurt * PolicePursuit.InjuryRunFine + killed * PolicePursuit.DeathRunFine;
            // Caught after running: a case is opened (Scripts/Justice); the fine is credited there. The hours are the old roadside
            // custody's; when a precinct holds them instead, its own hours reach the case when the cell door shuts (Wiring.Arrests).
            Justice.Arrest(JusticeReports.Fled(_hurt, _killed, fine, Law.Custody.Ready ? 0 : hours));
            Settle(fine, true, "reallife_crime_bust", "reallife_crime_bust_crash");
            Logger?.Info($"Police attention: caught after fleeing a stop for {_first}, {fine}, {hours} h in custody.");
            Stopped?.Invoke(_booked, true);
            _backup.Release(TrafficTools.Ready);   // one car makes the stop
            _patrol.Park();
            if (Law.Arrests.FledStop(crashed, hurt, killed, paid, _patrol.Car))
            {
                _state = State.Custody;   // cuffed here, held at the nearest precinct (Law/Custody); what follows is the old way, for when that is off
                _lawHolds = true;
                return;
            }
            WalkUp(now);
            BeginCustody(hours);
        }

        // A car the police already know costs half as much again.
        private int Known(int fine)
        {
            return _knownDay == TimeHelper.CurrentDay ? Mathf.RoundToInt(fine * 1.5f / 10f) * 10 : fine;
        }

        private bool WalkUp(float now)
        {
            try
            {
                return _body != null && _patrol.Car != null
                       && StreetSpots.Beside(_body.worldCenterOfMass, _body.rotation * Vector3.forward, 0.95f, StreetSpots.Left, 0.9f, out Vector3 door)
                       && _stop.Begin(_patrol.Car, door, arrest: false, now);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _stop.Clear();
                return false;
            }
        }

        private void Escaped(bool announce)
        {
            bool fled = _state == State.Fleeing;
            Clear();
            _knownDay = TimeHelper.CurrentDay;
            Logger?.Info("Police attention: the player got away; their car is known for the rest of the day.");
            if (announce)
            {
                Crime.Show(NotificationType.Warning, fled ? "reallife_crime_stop_escaped" : "reallife_crime_stop_dodged");
            }
        }

        // Everything let go; whatever state it was in, afterwards nothing is after the player.
        private void Clear()
        {
            _state = State.Idle;
            _lawHolds = false;
            _fine = 0;
            // Damages and casualties that no ticket settled (they got away, or the stop came to nothing) are not
            // forgotten: the bill follows in a few hours, as after any hit-and-run that was not caught.
            if (_owed > 0 || _hurt > 0 || _killed > 0)
            {
                int owed = _owed;
                int people = _hurt * PolicePursuit.InjuryEscapedFine + _killed * PolicePursuit.DeathEscapedFine;
                bool killed = _killed > 0;
                _owed = 0;
                _hurt = 0;
                _killed = 0;
                try
                {
                    PolicePursuit.BillLater(owed, people, killed, 0);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _holdSince = 0f;
            _trail.Clear();
            try
            {
                _stop.Clear();
                if (_patrol != null)
                {
                    _patrol.Release(TrafficTools.Ready, remove: false, quiet: true);
                    _backup.Release(TrafficTools.Ready);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- custody

        private void OnHitAndRunCaught()
        {
            if (_state == State.Idle && !_custodyRunning && isActiveAndEnabled)
            {
                BeginCustody(Random.Range(CrimeLaw.CustodyHoursMin, CrimeLaw.CustodyHoursMax + 1));
            }
        }

        private void BeginCustody(int hours)
        {
            if (_custodyRunning)
            {
                return;
            }
            _state = State.Custody;
            _custodyRunning = true;
            News.Report(NewsKind.PlayerArrested, PlayerHelper.GetPosition());
            SocialHooks.PlayerArrested();   // word gets round (Scripts/Social)
            Wiring.Arrests.FamilyHears(0);   // and at home (this is the way it is told when the Law feature does not hold the player)
            StartCoroutine(CustodyRoutine(hours));
        }

        // The officer gets to the window, the screen goes dark, the clock moves on with the game's
        // own time skip (the same call its doctor and taxi use), and the player is let go where they were.
        private IEnumerator CustodyRoutine(int hours)
        {
            // Whatever happens in here, the finally puts the game back: energy as it was, the screen lit, the state idle.
            _custodyFaded = false;
            _custodyResting = false;
            try
            {
                yield return new WaitForSecondsRealtime(5f);

                // Never over a dialog, a menu, another time skip or a hospital trip: then it is the fine alone.
                float waited = 0f;
                while (!LifeEvents.IsSafeForEvent() && waited < 15f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (!LifeEvents.IsSafeForEvent() || uis == null || uis.timeMachine == null || SaveGameManager.Current == null)
                {
                    yield break;
                }

                _custodyFaded = true;
                yield return UiFader.Fade(0.7f, "reallife_crime_custody_fade");
                Clear();   // patrol car and officer go while nobody can see
                _state = State.Custody;

                Timestamp release = TimeHelper.Now();
                release.AddHours(hours);
                _custodyResting = true;
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Hospital);   // a bench in a cell: a little rest, as in the game's own waiting rooms
                if (!GoingPublic.ClockShared)
                {
                    uis.timeMachine.StartTimeMachine(release, disableCancel: true, "reallife_crime_custody_info");   // in a Going Public session the hours pass at once (Core/GoingPublic)
                }

                waited = 0f;
                while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < 240f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _custodyResting = false;
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                _custodyFaded = false;
                yield return UiFader.UnFade();
                if (SaveGameManager.Current != null)
                {
                    ModState.AddModifier(ModState.MoodDownBig);
                    var data = new Dictionary<string, string>(1) { { "hours", hours.ToString() } };
                    Notifications.Show(NotificationType.Info, "reallife_crime_custody_released", data, 10f);
                }
            }
            finally
            {
                EndCustody();
            }
        }

        // Safe to call more than once, and from OnDestroy: puts back whatever the custody routine had changed.
        private void EndCustody()
        {
            try
            {
                if (_custodyResting)
                {
                    _custodyResting = false;
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                }
                if (_custodyFaded)
                {
                    _custodyFaded = false;
                    if (isActiveAndEnabled)
                    {
                        StartCoroutine(UiFader.UnFade());
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            FinishCustody();
        }

        private void FinishCustody()
        {
            _custodyRunning = false;
            Clear();
        }
    }
}
