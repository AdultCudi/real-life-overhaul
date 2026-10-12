using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What happens after the player's car hits a traffic car. They get a few seconds to stop
    /// near the scene and settle it. If they drive off it is a hit-and-run: a patrol car comes
    /// up the road behind them and follows the trail their car leaves. Stopping with the patrol
    /// car close means being caught (damages plus a fine); getting away means a bigger fine
    /// some hours later, which is kept in the savegame until it is charged.
    /// Injuring or killing someone on foot goes the same way with its own, much larger fines
    /// and a lasting weight on the player's mood; nobody goes to jail and the game goes on.
    /// One pursuit at a time. Each frame of a pursuit reads the car's position and speed and
    /// compares a few distances; everything else happens on events.
    /// </summary>
    public class PolicePursuit : MonoBehaviour
    {
        private const float StopWindow = 12f;           // seconds to stop after the crash
        private const float StopRadius = 40f;           // ...within this far of it
        private const float StoppedSpeedSqr = 1f;       // (m/s) squared
        private const float CrumbSpacing = 6f;          // metres between recorded trail points
        private const int MaxCrumbs = 900;
        private const float PursuitTime = 90f;
        private const float PursuitMost = 240f;         // seconds in all, however close the patrol car still is
        private const float EscapeDistance = 230f;
        private const float CatchDistance = 25f;
        private const float CatchHold = 3f;             // seconds stopped with the patrol car close
        private const float PatrolGap = 9f;
        private const int SpawnAttempts = 5;
        private const float FineCheckInterval = 5f;
        private const float StopAllowance = 26f;        // extra seconds the patrol car waits while its officer is out

        public static int CaughtFine => Prices.At(600);              // on top of the damages
        public static int EscapedFine => Prices.At(900);             // on top of the damages, charged later

        // Hitting a person, per person. Stopping makes it an accident; driving off makes it a crime.
        public static int InjuryFine => Prices.At(1500);             // stopped at the scene
        public static int DeathFine => Prices.At(5000);
        public static int InjuryRunFine => Prices.At(6000);          // drove off and was caught
        public static int DeathRunFine => Prices.At(20000);
        public static int InjuryEscapedFine => Prices.At(8000);      // drove off and got away: charged later
        public static int DeathEscapedFine => Prices.At(25000);

        private enum State { Idle, Window, Pursuit, Caught }

        public static IModLogger Logger;
        public static PolicePursuit Instance { get; private set; }

        /// <summary>True from the moment of a crash until it is settled, the pursuit is over and the patrol car has gone.</summary>
        public bool Busy => _state != State.Idle;

        /// <summary>Raised when the player is caught after driving off; Crime/PoliceAttention adds the hours in custody.</summary>
        public static event Action CaughtAfterFleeing;

        /// <summary>Raised when the player is charged for people hit with their car: injured, killed, and whether they stayed at the scene. For Licences.</summary>
        public static event Action<int, int, bool> PeopleCharged;

        private readonly List<Vector3> _trail = new List<Vector3>(256);   // where the car went before the patrol car existed

        private readonly RoadsideStop _stop = new RoadsideStop();   // the officer who walks up once the player is caught

        private ResponseUnit _patrol;
        private PursuitBackup _backup;   // the second patrol car of a long pursuit
        private float _pace;             // the patrol cars' top speed in this pursuit, m/s (PursuitPace)
        private float _pursuitFrom;
        private float _graceUntil;       // until then a fast patrol car is still getting up to speed: it cannot be called left behind
        private State _state = State.Idle;
        private Rigidbody _body;         // the player's car
        private Vector3 _crashSite;
        private Vector3 _lastCrumb;
        private int _owed;               // damages for everything hit so far
        private int _hurt;               // people injured so far
        private int _killed;             // people killed so far
        private float _stateEnd;
        private float _caughtMin;
        private float _stoppedSince;
        private float _nextSpawn;
        private int _spawnsTried;
        private float _nextFineCheck;
        private PursuitSight _sight;     // shared with Crime/PoliceAttention: the same driving loses either
        private bool _lawHolds;          // the arrest has been handed to Law/Custody: the patrol car stays until its scene is over

        private void Awake()
        {
            Instance = this;
            _patrol = new ResponseUnit(gameObject);
            _backup = new PursuitBackup(gameObject);
        }

        private void OnDestroy()
        {
            _stop.Clear();
            if (_patrol != null)
            {
                _patrol.Release(resumeTraffic: true, remove: false, quiet: true);
            }
            if (_backup != null)
            {
                _backup.Release(resumeTraffic: true);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Called when the player's car has hit a traffic car hard enough to owe damages.
        /// Returns false if this is not being handled here, in which case the caller charges
        /// the damages on the spot as before.
        /// </summary>
        public bool ReportCollision(Rigidbody playerCar, int liability, Vector3 position)
        {
            // Already being pulled over or chased for a traffic offence: one pursuit, one bill. It goes on that one.
            if (PoliceAttention.TakeDamages(liability))
            {
                return true;
            }
            if (!Settings.PolicePursuit || playerCar == null || _state == State.Caught)
            {
                return false;
            }
            if (_state != State.Idle)
            {
                // Hitting more cars while deciding, or while being chased, adds to the bill.
                _owed += liability;
                return true;
            }

            _owed = liability;
            OpenWindow(playerCar, position);
            Notifications.Show(NotificationType.Warning, "reallife_crash_stop_prompt", null, 6f);
            return true;
        }

        /// <summary>
        /// Called when the player's car has injured or killed someone on foot. The same choice as
        /// after hitting a car, with more at stake: stop near the scene and it is an accident (a
        /// fine and a bad conscience); drive off and it is a hit-and-run, with a patrol car after
        /// them and a far larger fine whether or not it catches them.
        /// Returns false if this is not being handled here, in which case the caller settles it
        /// as an accident on the spot.
        /// </summary>
        public bool ReportPedestrian(Rigidbody playerCar, bool fatal, Vector3 position)
        {
            if (PoliceAttention.TakeCasualty(fatal))
            {
                return true;
            }
            if (!Settings.PolicePursuit || playerCar == null || _state == State.Caught)
            {
                return false;
            }
            if (fatal)
            {
                _killed++;
            }
            else
            {
                _hurt++;
            }
            if (_state != State.Idle)
            {
                // Already deciding, or already being chased: one more on the charge sheet.
                Notifications.Show(NotificationType.Error, "reallife_pedhit_another", null, 6f);
                return true;
            }
            OpenWindow(playerCar, position);
            Notifications.Show(NotificationType.Error, "reallife_pedhit_stop_prompt", null, 8f);
            return true;
        }

        // The few seconds in which stopping near the scene settles it.
        private void OpenWindow(Rigidbody playerCar, Vector3 position)
        {
            _body = playerCar;
            _crashSite = position;
            _lastCrumb = position;
            _trail.Clear();
            _trail.Add(position);
            _state = State.Window;
            _stateEnd = Time.time + StopWindow;
        }

        /// <summary>
        /// Fines the player for the people they hit and weighs on their mood, more for a death.
        /// Public for the case where pursuits are switched off and it is settled where it happened.
        /// </summary>
        /// <param name="stopped">True for an accident (they stayed); false for leaving the scene.</param>
        /// <returns>What was actually taken at the roadside (see Roadside): the rest of a fine too large for the cash in hand is the court's.</returns>
        public static int ChargeForPeople(int hurt, int killed, bool stopped)
        {
            int nominal = stopped ? hurt * InjuryFine + killed * DeathFine : hurt * InjuryRunFine + killed * DeathRunFine;
            int fine = Roadside(nominal, out int rest);
            if (fine <= 0)
            {
                return 0;
            }
            try
            {
                GameManager.ChangeMoneySafe(-fine, new TransactionInfo("reallife_transaction_fine"), null, null, force: true);
                Weigh(killed > 0);
                var data = new Dictionary<string, string> { { "amount", ((float)fine).ToCurrencyFormat() } };
                if (stopped)
                {
                    Notifications.Show(NotificationType.Error, killed > 0 ? "reallife_pedhit_stopped_dead" : "reallife_pedhit_stopped_injured", data, 10f);
                }
                else
                {
                    Notifications.Show(NotificationType.Error, killed > 0 ? "reallife_pedhit_fled_dead" : "reallife_pedhit_fled_injured", data, 10f);
                }
                Logger?.Info($"Pedestrian hit settled ({(stopped ? "stopped" : "left the scene")}): {hurt} injured, {killed} killed, charged {fine} at the roadside of {nominal} (at most {JusticeLaw.FineShareOfCash:P0} of the cash in hand, never bounded below {JusticeLaw.FineFloor}); {rest} left to the court.");
                JusticeReport report = JusticeReports.Struck(hurt, killed, stopped, fine);
                SayRest(rest, report != null);
                Justice.Summon(report);   // a death, or leaving the scene, goes to court (Scripts/Justice), where what was paid here is credited
                Justice.CrashVictim(0, hurt, killed);
                PeopleCharged?.Invoke(hurt, killed, stopped);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return fine;
        }

        /// <summary>
        /// What of a fine for people struck is taken at the roadside: no more than the share of the cash in hand
        /// that a court's own fines are bounded to (Justice: 35 %, never bounded below 250), so that one accident
        /// does not empty the player's pocket on the spot. The rest is left to the court case that follows, where
        /// what was paid here is credited (JusticeReport.PaidAtScene) and the sentence is bounded the same way.
        /// </summary>
        internal static int Roadside(int nominal, out int rest)
        {
            int taken = nominal <= 0 ? 0 : Mathf.Min(nominal, JusticeLaw.Bound(nominal, Crime.Cash, JusticeLaw.FineShareOfCash, JusticeLaw.FineFloor));
            rest = Mathf.Max(0, nominal - taken);
            return taken;
        }

        // Said when less than the whole fine was taken: where the rest goes.
        private static void SayRest(int rest, bool toCourt)
        {
            if (rest <= 0)
            {
                return;
            }
            var data = new Dictionary<string, string> { { "rest", ((float)rest).ToCurrencyFormat() } };
            Notifications.Show(NotificationType.Info, toCourt ? "reallife_fine_rest_court" : "reallife_fine_rest_waived", data, 10f);
        }

        // What it does to the player to have hurt someone: a rough day, or something that stays for days.
        internal static void Weigh(bool killed)
        {
            ModState.AddModifier(killed ? ModState.Remorse : ModState.MoodDownBig);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextFineCheck)
            {
                _nextFineCheck = Time.unscaledTime + FineCheckInterval;
                CheckPendingFine();
            }
            if (_state == State.Idle)
            {
                return;
            }
            try
            {
                Tick(Time.time);
            }
            catch (Exception e)
            {
                // End it rather than fault again next frame; what is owed is still collected.
                Logger?.Error(e);
                Escape(announce: false);
            }
        }

        private void Tick(float now)
        {
            if (_state == State.Caught)
            {
                // The patrol car sits behind the player for a moment (longer while its officer
                // is out of it), then goes back on patrol.
                _stop.Tick(now);
                if (!TrafficTools.Ready || _patrol.IsLost || now >= _stateEnd || (now >= _caughtMin && _stop.Finished)
                    || (_lawHolds && !Law.Custody.OnScene))
                {
                    _lawHolds = false;
                    _stop.Clear();
                    _patrol.Release(TrafficTools.Ready, remove: false, quiet: true);
                    _state = State.Idle;
                }
                return;
            }

            if (_body == null)
            {
                // The car itself is gone (sold, towed, scene change): close the matter.
                if (_state == State.Window)
                {
                    Settle(stopped: true);
                }
                else
                {
                    Escape(announce: true);
                }
                return;
            }

            Vector3 position = _body.position;
            bool stopped = _body.velocity.sqrMagnitude < StoppedSpeedSqr;
            RecordCrumb(position);

            if (_state == State.Window)
            {
                bool near = (position - _crashSite).sqrMagnitude < StopRadius * StopRadius;
                if (near && stopped)
                {
                    Settle(stopped: true);
                }
                else if (!near || now >= _stateEnd)
                {
                    BeginPursuit(now);
                }
                return;
            }

            // Going indoors or fast-forwarding takes traffic away: the patrol car has lost them.
            if (!TrafficTools.Ready)
            {
                Escape(announce: true);
                return;
            }
            if (now >= _stateEnd)
            {
                // Time is up. A patrol car that is right behind them with the car in sight does not turn
                // round and go home: it carries on a little at a time, up to the longest a pursuit may last.
                if (!_patrol.InUse || _patrol.IsLost || now - _pursuitFrom >= PursuitMost || !StillOn(position))
                {
                    Escape(announce: true);
                    return;
                }
                _stateEnd = now + PursuitPace.Extension;
            }
            if (!_patrol.InUse)
            {
                if (now >= _nextSpawn && !TrySpawnPatrol(position))
                {
                    _nextSpawn = now + 2f;
                    if (++_spawnsTried >= SpawnAttempts)
                    {
                        Escape(announce: true);   // no patrol car could get onto the road
                    }
                }
                return;
            }
            if (_patrol.IsLost)
            {
                Escape(announce: true);
                return;
            }

            if (_backup.Tick(now, _body, _pace, PatrolGap))
            {
                Logger?.Info($"Pursuit: a second patrol car has joined after {now - _pursuitFrom:F0} s.");
            }

            // Left far behind, or kept out of its sight for long enough (behind buildings): they have lost it.
            // With two cars out it is the nearer one that has to be lost. A fast patrol car is given a moment
            // to get up to speed first: it starts from rest well behind a car that is already going.
            Vector3 patrolAt = _backup.Nearer(_patrol.Car.transform.position, position);
            float gapSqr = (patrolAt - position).sqrMagnitude;
            if (now >= _graceUntil && (gapSqr > EscapeDistance * EscapeDistance || _sight.Lost(patrolAt, position, now)))
            {
                Escape(announce: true);
            }
            else if (stopped && (_patrol.Car.transform.position - position).sqrMagnitude < CatchDistance * CatchDistance)
            {
                // (The first car makes the stop: its officer is the one who walks up.)
                if (_stoppedSince <= 0f)
                {
                    _stoppedSince = now;
                }
                else if (now - _stoppedSince >= CatchHold)
                {
                    Catch(now);
                }
            }
            else
            {
                _stoppedSince = 0f;
            }
        }

        // True while a patrol car is close behind the player with their car in sight (one line test, asked when the time runs out).
        private bool StillOn(Vector3 position)
        {
            Vector3 patrolAt = _backup.Nearer(_patrol.Car.transform.position, position);
            return (patrolAt - position).sqrMagnitude < PursuitPace.CloseEnough * PursuitPace.CloseEnough && !PursuitSight.Blocked(patrolAt, position);
        }

        // The trail the patrol car follows, so it stays on roads the player really drove.
        private void RecordCrumb(Vector3 position)
        {
            if ((position - _lastCrumb).sqrMagnitude < CrumbSpacing * CrumbSpacing)
            {
                return;
            }
            _lastCrumb = position;
            List<Vector3> points = _patrol.InUse ? _patrol.Path : _trail;
            if (points.Count < MaxCrumbs)
            {
                points.Add(position);
            }
            _backup.Crumb(position, MaxCrumbs);
        }

        // The player stopped (or nothing more can come of it): damages are paid, as they always were.
        private void Settle(bool stopped)
        {
            int owed = _owed;
            int hurt = _hurt;
            int killed = _killed;
            Reset();
            if (stopped)
            {
                CrashConsequences.ChargeLiability(owed, "reallife_crash_stopped");
            }
            else
            {
                CrashConsequences.ChargeLiability(owed, "reallife_crash_liability");
            }
            int paid = ChargeForPeople(hurt, killed, stopped);
            if (stopped)
            {
                // They stayed. An injury is the fine above; a death is an arrest on the spot, an officer walking up to them.
                Law.Arrests.StayedAtScene(killed, Mathf.Max(0, owed) + paid, null);
            }
        }

        private void BeginPursuit(float now)
        {
            // Never while indoors or while something else has the player's attention: then it is just the bill.
            if (!TrafficTools.Ready || !LifeEvents.IsSafeForEvent())
            {
                Settle(stopped: false);
                return;
            }
            _state = State.Pursuit;
            _stateEnd = now + PursuitTime;
            _pursuitFrom = now;
            _backup.Begin(now);
            _stoppedSince = 0f;
            _sight.Reset();
            _nextSpawn = now;
            _spawnsTried = 0;
            Logger?.Info($"Pursuit: the player left the scene of a crash owing {_owed}, with {_hurt} injured and {_killed} killed.");
            if (_hurt + _killed > 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_pedhit_pursuit_start", null, 8f);
            }
            else
            {
                Notifications.Show(NotificationType.Error, "reallife_pursuit_start", null, 8f);
            }
        }

        // Puts a patrol car on the road the player came along, some way back from the crash.
        private bool TrySpawnPatrol(Vector3 playerPosition)
        {
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(_crashSite, playerPosition - _crashSite, 40f);
            if (!_patrol.Spawn(UnitKind.Police, anchor, Random.Range(45f, 80f), 25f))
            {
                return false;
            }

            // Its route runs to the crash and from there along the trail, which keeps growing.
            _patrol.Path.AddRange(_trail);
            _trail.Clear();
            EmergencyDriver driver = _patrol.Driver;
            driver.Begin(_patrol.Car, _patrol.Path, playerPosition, PatrolGap);
            driver.Follow(_body, PatrolGap);
            _pace = PursuitPace.For(_body);
            driver.CruiseSpeed = _pace;
            _graceUntil = _pace > EmergencyDriver.FastFrom ? Time.time + PursuitPace.StartGrace : 0f;
            Logger?.Info($"Pursuit: {_patrol.Car.prefab.name} in pursuit, {_patrol.Path.Count} trail points, top speed {_pace * 3.6f:F0} km/h"
                         + (_pace > EmergencyDriver.FastFrom ? $" (the car it is after: {CarTuning.Instance.TargetTopKmh:F0} km/h)." : " (a car with the game's own performance)."));
            return true;
        }

        private void Catch(float now)
        {
            // For cars alone it is the damages and the usual fine. With people hurt, their fines replace it.
            int people = _hurt * InjuryRunFine + _killed * DeathRunFine;
            int rest = 0;
            int fine = people > 0 ? Roadside(people, out rest) : CaughtFine;   // (for people hurt, no more at the roadside than they can pay: the rest is the court's)
            int total = _owed + fine;
            if (_owed > 0)
            {
                GameManager.ChangeMoneySafe(-_owed, new TransactionInfo("reallife_transaction_accident_liability"), null, null, force: true);
            }
            GameManager.ChangeMoneySafe(-fine, new TransactionInfo("reallife_transaction_fine"), null, null, force: true);
            var data = new Dictionary<string, string> { { "amount", ((float)total).ToCurrencyFormat() } };
            if (_killed > 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_pedhit_caught_dead", data, 10f);
            }
            else if (_hurt > 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_pedhit_caught_injured", data, 10f);
            }
            else
            {
                Notifications.Show(NotificationType.Error, "reallife_pursuit_caught", data, 10f);
            }
            if (people > 0)
            {
                Weigh(_killed > 0);
            }
            SayRest(rest, true);
            Logger?.Info($"Pursuit: caught, charged {total}" + (rest > 0 ? $" (the fine for the people hurt bounded to {fine} of {people} at the roadside; {rest} left to the court)." : "."));
            Justice.Arrest(JusticeReports.HitAndRun(_hurt, _killed, fine, 0));   // something for a court (Scripts/Justice); the fine above is credited there
            Justice.CrashVictim(_owed, _hurt, _killed);

            Rigidbody stopped = _body;
            int hurt = _hurt, killed = _killed;
            bool crashed = _owed > 0;
            _backup.Release(TrafficTools.Ready);   // one car makes the stop; the other goes back on patrol
            _patrol.Park();
            _state = State.Caught;
            _caughtMin = now + Random.Range(8f, 12f);
            _stateEnd = _caughtMin;
            _owed = 0;
            _hurt = 0;
            _killed = 0;
            _body = null;
            if (Law.Arrests.HitAndRun(crashed, hurt, killed, total, _patrol.Car))
            {
                // Cuffed here and held at the nearest precinct (Law/Custody): the patrol car waits until the screen is dark.
                _lawHolds = true;
                _caughtMin = now + 75f;
                _stateEnd = _caughtMin;
                return;
            }
            OnPlayerPulledOver(stopped, now);
            CaughtAfterFleeing?.Invoke();
        }

        /// <summary>
        /// The player has been stopped and the patrol car is parked behind them. This is the one
        /// place that knows it, so this is where an officer gets out and walks up to their door.
        /// Nothing about the fine depends on it: that has already been charged.
        /// </summary>
        private void OnPlayerPulledOver(Rigidbody stopped, float now)
        {
            try
            {
                if (stopped != null && _patrol.Car != null
                    && StreetSpots.Beside(stopped.worldCenterOfMass, stopped.rotation * Vector3.forward, 0.95f, StreetSpots.Left, 0.9f, out Vector3 door)
                    && _stop.Begin(_patrol.Car, door, arrest: false, now))
                {
                    _stateEnd = _caughtMin + StopAllowance;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _stop.Clear();
            }
        }

        // They got away for now. The plate was seen: the bill follows in a few in-game hours.
        private void Escape(bool announce)
        {
            int owed = _owed;
            int people = _hurt * InjuryEscapedFine + _killed * DeathEscapedFine;
            bool killed = _killed > 0;
            int hurtCount = _hurt, killedCount = _killed;
            Reset();   // first, so that whatever happens below the pursuit is over
            _stop.Clear();
            _patrol.Release(TrafficTools.Ready, remove: false, quiet: true);
            _backup.Release(TrafficTools.Ready);
            if (BillLater(owed, people, killed, EscapedFine))
            {
                if (announce)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_pursuit_escaped", null, 8f);
                }
                Law.Arrests.Escaped(hurtCount, killedCount);   // no cell, but the car is known for the rest of the day
            }
        }

        /// <summary>
        /// The bill for a crash the player got away from, sent a few in-game hours later: the
        /// damages, and the fines for anyone hurt (or, with nobody hurt, the fine for running).
        /// Also used by Crime/PoliceAttention when a driver who crashed while fleeing a stop gets away.
        /// </summary>
        /// <returns>False if nothing is owed or there is no game to owe it in.</returns>
        internal static bool BillLater(int owed, int people, bool killed, int runningFine)
        {
            if ((owed <= 0 && people <= 0) || !ModState.Loaded || SaveGameManager.Current == null)
            {
                return false;
            }
            // As when caught: for people hurt, their fines stand in for the usual one.
            int later = owed + (people > 0 ? people : runningFine);
            if (people > 0)
            {
                Weigh(killed);   // they know what they did, whether or not anyone caught them
                Justice.Summon(JusticeReports.Escaped(killed));   // the plate was seen: a summons follows (Scripts/Justice)
            }
            ModState.AddPendingFine(later, TimeHelper.NowInMinutes() + Random.Range(3f, 10f) * 60f);
            Logger?.Info("Pursuit: escaped; " + later + " will be charged later.");
            return true;
        }

        private void Reset()
        {
            _state = State.Idle;
            _lawHolds = false;
            _body = null;
            _owed = 0;
            _hurt = 0;
            _killed = 0;
            _trail.Clear();
        }

        // Polled every few seconds: the fine for an earlier escape arrives once its time has come.
        private static void CheckPendingFine()
        {
            if (!ModState.Loaded || ModState.PendingFine <= 0 || SaveGameManager.Current == null)
            {
                return;
            }
            float now = TimeHelper.NowInMinutes();
            if (ModState.PendingFineDue - now > 48f * 60f)
            {
                ModState.PendingFineDue = now + 10f * 60f;   // a date that makes no sense must not hold the fine back for ever
            }
            if (now < ModState.PendingFineDue || !LifeEvents.IsSafeForEvent())
            {
                return;
            }

            int amount = ModState.PendingFine;
            ModState.ClearPendingFine();
            try
            {
                GameManager.ChangeMoneySafe(-amount, new TransactionInfo("reallife_transaction_fine"), null, null, force: true);
                var data = new Dictionary<string, string> { { "amount", ((float)amount).ToCurrencyFormat() } };
                Notifications.Show(NotificationType.Error, "reallife_pursuit_fine_later", data, 10f);
                Logger?.Info($"Pursuit: fine of {amount} for an earlier hit-and-run charged.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
