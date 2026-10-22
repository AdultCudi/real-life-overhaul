using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The stop: the patrol car behind the player, the ticket, the pursuit, and the one bill for all of it.</summary>
    public partial class PoliceAttention
    {
        private const float ComplyWindow = 15f;        // seconds to stop once the lights are on
        private const float StoppedSpeedSqr = 1f;      // (m/s)^2
        private const float TicketDistance = 28f;      // metres: the patrol car is close enough to deal with a stopped driver
        private const float TicketHold = 1.5f;         // seconds
        private const float BustHold = 3f;             // seconds
        private const float LostDistance = 240f;       // metres
        private const float PursuitTime = 150f;        // seconds
        private const float PursuitMost = 300f;        // seconds of fleeing in all, however close the patrol car still is
        private const float PatrolGap = 9f;            // metres
        private const float CrumbSpacing = 6f;         // metres
        private const int MaxCrumbs = 900;
        private const float StopAllowance = 28f;       // seconds
        private const float SignalledMost = 60f;       // seconds: a stop nobody completes is given up
        private const float AfterTicket = 75f;         // seconds after a ticket before anything new can be noticed

        private readonly RoadsideStop _stop = new RoadsideStop();
        private readonly List<Vector3> _trail = new List<Vector3>(128);

        private PursuitBackup _backup;     // the second patrol car, once they have fled for a while
        private float _pace;               // the patrol cars' top speed for this stop, m/s
        private float _stateEnd;
        private float _holdSince;
        private PursuitSight _sight;       // shared with PolicePursuit: the same driving loses either
        private float _fledAt;
        private float _nextSpawn;
        private int _spawnsTried;
        private Vector3 _lastCrumb;
        private int _fine;                 // for the offences so far
        private Offence _first;            // the one they were stopped for
        private int _booked;               // a bit for each kind of offence already on this stop's bill
        private float _signalledAt;
        private int _owed;                 // damages for cars hit during the stop or the chase
        private int _hurt;                 // people injured
        private int _killed;               // people killed
        private bool _lawHolds;            // the arrest is Law/Custody's: the patrol car stays until its scene is over

        // A patrol car is coming for the player or after them: whatever else they do belongs to this stop.
        private bool Pursuing => _state == State.Summoned || _state == State.Signalled || _state == State.Fleeing;

        /// <summary>A crash during a stop or a chase goes on its bill. False if none is on: PolicePursuit deals with it.</summary>
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

        /// <summary>Test button: a patrol car comes up behind the player's car as if they had run a red light.</summary>
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

        // Drink at the wheel goes on the bill of whatever the stop was for.
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

        private void Pursue(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !TrafficTools.Ready)
            {
                // Indoors, or time is being skipped: whoever was after them has lost them.
                if (_state == State.Signalled || _state == State.Fleeing)
                {
                    Escaped();
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
                        // Time is up, but a patrol car right behind with the car in sight carries on, a little at a time.
                        _stateEnd = now + PursuitPace.Extension;
                    }
                    if (_patrol.IsLost || now >= _stateEnd || Unseen(now, position))
                    {
                        Escaped();
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

        private bool Unseen(float now, Vector3 position)
        {
            Vector3 from = _backup.Nearer(_patrol.Car.transform.position, position);
            return (from - position).sqrMagnitude > LostDistance * LostDistance || _sight.Lost(from, position, now);
        }

        // A patrol car close behind with the player in sight: one line test, when the time runs out.
        private bool StillOn(Vector3 position)
        {
            Vector3 from = _backup.Nearer(_patrol.Car.transform.position, position);
            return (from - position).sqrMagnitude < PursuitPace.CloseEnough * PursuitPace.CloseEnough && !PursuitSight.Blocked(from, position);
        }

        // The trail the patrol car follows, so that it keeps to roads the player really drove.
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

        private void Ticket(float now)
        {
            Breathalyse();
            int fine = Known(_fine);
            int killed = _killed;
            int paid = fine + _owed + _hurt * PolicePursuit.InjuryFine + _killed * PolicePursuit.DeathFine;
            Justice.Summon(JusticeReports.Struck(_hurt, killed, true, paid - fine - _owed));   // a death on the way goes to court
            Settle(fine, false, (_booked & (_booked - 1)) != 0 ? "reallife_crime_ticket_several" : TicketKey(_first), "reallife_crime_ticket_crash");
            Logger?.Info($"Police attention: ticket for {_first}, {fine}.");
            Justice.PoliceContact(driving: true);   // a bench warrant is served, a suspended licence charged
            Stopped?.Invoke(_booked, false);
            _backup.Release(TrafficTools.Ready);   // one car makes the stop
            _patrol.Park();
            _state = State.Ticketed;
            _nextOffence = now + AfterTicket;
            Law.Arrests.Cited(fine);
            if (Law.Arrests.StayedAtScene(killed, paid, _patrol.Car))
            {
                _state = State.Custody;   // somebody died under their wheels on the way: an arrest, not a ticket
                _lawHolds = true;
                return;
            }
            _holdSince = now + Random.Range(8f, 12f);   // from here, the earliest the patrol car leaves
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
            // The case credits the fine; a precinct that holds the player gives the case its own hours.
            Justice.Arrest(JusticeReports.Fled(_hurt, _killed, fine, Law.Custody.Ready ? 0 : hours));
            Settle(fine, true, "reallife_crime_bust", "reallife_crime_bust_crash");
            Logger?.Info($"Police attention: caught after fleeing a stop for {_first}, {fine}, {hours} h in custody.");
            Stopped?.Invoke(_booked, true);
            _backup.Release(TrafficTools.Ready);
            _patrol.Park();
            if (Law.Arrests.FledStop(crashed, hurt, killed, paid, _patrol.Car))
            {
                _state = State.Custody;   // held at the nearest precinct; the custody below is for when that is off
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

        // One charge and one message for the whole bill: fines, damages, and the fines for anyone hurt.
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

        private void Escaped()
        {
            bool fled = _state == State.Fleeing;
            Clear();
            _knownDay = TimeHelper.CurrentDay;
            Logger?.Info("Police attention: the player got away; their car is known for the rest of the day.");
            Crime.Show(NotificationType.Warning, fled ? "reallife_crime_stop_escaped" : "reallife_crime_stop_dodged");
        }

        private void Clear()
        {
            _state = State.Idle;
            _lawHolds = false;
            _fine = 0;
            // Damages and casualties no ticket settled are billed a few hours later, as after a hit-and-run.
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
    }
}
