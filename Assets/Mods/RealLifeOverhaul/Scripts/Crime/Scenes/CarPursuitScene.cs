using System.Collections.Generic;
using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    internal enum CarCrime
    {
        Stop,      // an ordinary driver is pulled over for something they did
        Chase,     // a reckless driver who does not stop
        Theft      // somebody takes a car at a red light and drives off in it
    }

    /// <summary>
    /// A traffic car that the police want: pulled over quietly, or chased. The car is taken out
    /// of traffic (never an ambulance, taxi or working vehicle; see CrimeCars), driven by the
    /// mod along the road ahead of it with a patrol car behind, and either stops for the patrol
    /// car (pulling in to the kerb first where the roadside is free, the patrol car tucking in
    /// behind it) or loses it. When it stops, an officer walks up to the driver's door; an arrest
    /// puts the driver in the patrol car and takes the car off the road. Whatever happens,
    /// both cars are handed back to the traffic system when the scene ends.
    /// Endings: ticket, warning, arrested (a stop); caught, escaped (a chase or a theft).
    /// </summary>
    internal sealed class CarPursuitScene : CrimeScene
    {
        private const int Taking = 0, Driving = 1, Stopping = 2, Held = 3, Clearing = 4;
        private const float CalmSpeed = 11f;       // m/s: a driver who has not noticed the lights yet
        private const float FleeSpeed = 19.5f;
        private const float PatrolSpeed = 21f;
        private const float Gap = 9f;
        private const float PullInSpeed = 6f;      // m/s while moving over to the kerb
        private const float PullInTime = 2.4f;     // seconds from starting to move over to braking

        private readonly string _id;
        private readonly CarCrime _kind;
        private readonly List<Waypoint> _scratch = new List<Waypoint>();
        private readonly List<Vector3> _road = new List<Vector3>(256);
        private readonly RoadsideStop _stop = new RoadsideStop();

        private float _patrolAt;
        private float _holdMin;
        private float _brakeAt;        // when a car that is pulling in to the kerb is braked; 0 if it is not
        private int _patrolTries;
        private bool _takeAway;

        public CarPursuitScene(string id, CarCrime kind)
        {
            _id = id;
            _kind = kind;
        }

        public override string Id => _id;
        protected override float MaxTime => 140f;
        protected override float AbandonDistance => 600f;   // a chase goes where it goes; the time limit ends it

        public override Vector3 Focus
        {
            get
            {
                ResponseUnit suspect = CrimeDirector.Suspect;
                return suspect != null && suspect.InUse && suspect.Car != null ? suspect.Car.transform.position : Scene;
            }
        }

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            ResponseUnit suspect = CrimeDirector.Suspect;
            if (suspect == null || suspect.InUse || CrimeDirector.Patrol.InUse)
            {
                return false;
            }
            VehicleComponent car = _kind == CarCrime.Theft
                ? CrimeCars.FindTrafficCar(setup.Player, 25f, 95f, stopped: true)
                : CrimeCars.FindTrafficCar(setup.Player, 45f, 140f, stopped: false);
            if (car == null || !CrimeCars.RoadAhead(car, 460f, 170f, _scratch, _road) || !suspect.Take(car))
            {
                return false;
            }
            Scene = car.transform.position;
            _patrolTries = 0;
            _brakeAt = 0f;
            _takeAway = false;

            if (_kind == CarCrime.Theft)
            {
                // The thief comes off the pavement at a run and is in the driver's seat before the light changes.
                if (!StreetSpots.BesideCar(car, StreetSpots.Left, 0.9f, out Vector3 door)
                    || !StreetSpots.OnPavement(Scene, 14f, out Vector3 kerb)
                    || !First.Enter(kerb, ActorLook.Civilian, 3f, onRoad: true))
                {
                    return false;
                }
                First.Board(door, Pace.Run, 10f);
                Go(Taking, now, 11f);
                return true;
            }

            Drive(now, _kind == CarCrime.Chase ? FleeSpeed : CalmSpeed);
            _patrolAt = now + Random.Range(1.5f, 4f);
            return true;
        }

        private void Drive(float now, float speed)
        {
            ResponseUnit suspect = CrimeDirector.Suspect;
            suspect.Path.AddRange(_road);
            suspect.Driver.Begin(suspect.Car, suspect.Path, _road[_road.Count - 1], 6f);
            suspect.Driver.CruiseSpeed = speed;
            Go(Driving, now, _kind == CarCrime.Stop ? Random.Range(9f, 14f) : Random.Range(22f, 42f));
        }

        // A patrol car on the road behind the suspect, lights on, settling in behind it.
        private bool SendPatrol()
        {
            ResponseUnit suspect = CrimeDirector.Suspect;
            ResponseUnit patrol = CrimeDirector.Patrol;
            if (!Settings.Police || patrol.InUse || !suspect.InUse || suspect.Car == null)
            {
                return patrol.InUse;
            }
            Transform body = suspect.Car.transform;
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(body.position, body.forward, 40f);
            if (!patrol.Spawn(UnitKind.Police, anchor, Random.Range(55f, 95f), 25f))
            {
                return false;
            }
            // Its own route ends where the suspect is now; from there, the road the suspect has still to drive.
            int from = 0;
            float nearest = float.MaxValue;
            for (int i = 0; i < _road.Count; i++)
            {
                float sqr = (_road[i] - body.position).sqrMagnitude;
                if (sqr < nearest)
                {
                    nearest = sqr;
                    from = i;
                }
            }
            for (int i = from; i < _road.Count; i++)
            {
                patrol.Path.Add(_road[i]);
            }
            EmergencyDriver driver = patrol.Driver;
            driver.Begin(patrol.Car, patrol.Path, body.position, Gap);
            driver.Follow(suspect.Car.rb, Gap);
            driver.CruiseSpeed = PatrolSpeed;
            return true;
        }

        public override bool Report(float now)
        {
            if (Active && !Reported)
            {
                _patrolAt = now;   // the call puts a patrol car on it at once
                _patrolTries = 0;
            }
            Reported = Active;
            return Active;
        }

        protected override void Run(float now, Vector3 player)
        {
            ResponseUnit suspect = CrimeDirector.Suspect;
            ResponseUnit patrol = CrimeDirector.Patrol;
            if (Step != Clearing && (suspect.IsLost || !suspect.InUse || (patrol.InUse && patrol.IsLost)))
            {
                End();   // one of the cars was taken off the road under us: nothing left to play
                return;
            }

            switch (Step)
            {
                case Taking:
                    if (!First.Present || now >= StepEnd)
                    {
                        First.Exit();
                        // The owner is left standing in the road.
                        if (StreetSpots.BesideCar(suspect.Car, StreetSpots.Left, 1.6f, out Vector3 beside)
                            && Second.Enter(beside, ActorLook.Civilian, 2f, onRoad: true))
                        {
                            Second.Stand(_road[_road.Count - 1]);
                            Second.Repeat(Despair, 2.5f, 4f, CharacterEmojiName.CommonRedAngry, 0.8f);
                        }
                        Say(NotificationType.Warning, Entry.Extra, 7f);
                        Drive(now, FleeSpeed);
                        _patrolAt = now + Random.Range(3f, 7f);
                    }
                    break;

                case Driving:
                    if (!patrol.InUse && now >= _patrolAt && _patrolTries < 2 && !SendPatrol())
                    {
                        _patrolTries++;         // the spawn point was taken: once more, then nobody comes
                        _patrolAt = now + 5f;
                    }
                    if (Second.Settled && now > StepEnd - 14f)
                    {
                        Second.Quiet();
                        Second.WalkAwayFrom(player, Pace.Walk, 25f, 20f);
                    }
                    if (now < StepEnd && suspect.Driver.Active)
                    {
                        break;
                    }
                    if (patrol.InUse && !Is("escaped"))
                    {
                        // Over to the kerb if the roadside is free, then stopped; in its lane if it is not.
                        float over = suspect.Driver.Active ? SceneParking.KerbRoom(suspect.Car, patrol.Car != null ? patrol.Car.rb : null) : 0f;
                        if (over > 0f)
                        {
                            suspect.Driver.CruiseSpeed = PullInSpeed;
                            suspect.Driver.PullOver(over);
                            patrol.Driver.PullOver(over * 0.7f);   // a little further out than the car it has stopped: its officer's side is covered
                            _brakeAt = now + PullInTime;
                            CrimeDirector.Logger?.Info(System.FormattableString.Invariant($"Crime: the stopped car pulls in {over:F1} m to the kerb, the patrol car behind it."));
                        }
                        else
                        {
                            suspect.Driver.Brake();
                        }
                        Go(Stopping, now, over > 0f ? 8f + PullInTime : 8f);
                    }
                    else
                    {
                        // Lost the patrol car, or there never was one: the car melts back into traffic.
                        Conclude("escaped");
                        Finish(now);
                    }
                    break;

                case Stopping:
                    if (_brakeAt > 0f && now >= _brakeAt)
                    {
                        _brakeAt = 0f;
                        suspect.Driver.Brake();
                    }
                    if (!suspect.Driver.Active)
                    {
                        suspect.Park();   // stopped: held still from now (nothing steadies an unparked car the traffic system ignores)
                    }
                    // Wait for the suspect to stop and the patrol car to pull up behind it.
                    bool pulledUp = patrol.Driver.Speed < 0.5f
                                    && (patrol.Car.transform.position - suspect.Car.transform.position).sqrMagnitude < 30f * 30f;
                    if ((!suspect.Driver.Active && pulledUp) || now >= StepEnd)
                    {
                        suspect.Park();
                        patrol.Park();
                        _takeAway = Is("arrested") || Is("caught");
                        _holdMin = now + Random.Range(8f, 13f);
                        StepEnd = _holdMin;
                        if (StreetSpots.BesideCar(suspect.Car, StreetSpots.Left, 1f, out Vector3 door)
                            && _stop.Begin(patrol.Car, door, _takeAway, now))
                        {
                            StepEnd = _holdMin + 30f;
                        }
                        Step = Held;
                    }
                    break;

                case Held:
                    _stop.Tick(now);
                    if (now >= StepEnd || (now >= _holdMin && _stop.Finished))
                    {
                        Conclude();
                        Finish(now);
                    }
                    break;

                case Clearing:
                    if (now >= StepEnd)
                    {
                        End();
                    }
                    break;
            }
        }

        // The cars go their ways now; the scene stays a few seconds for whoever is still on foot.
        private void Finish(float now)
        {
            _stop.Clear();
            ReleaseCars();
            Second.Quiet();
            if (Second.Present)
            {
                Second.WalkAwayFrom(Scene, Pace.Walk, 20f, 6f);
            }
            Go(Clearing, now, Second.Present ? 7f : 1f);
        }

        private void ReleaseCars()
        {
            // An arrested driver's car is taken away; every other car drives on.
            CrimeDirector.Suspect.Release(TrafficTools.Ready, remove: _takeAway && CrimeDirector.Suspect.Parked);
            CrimeDirector.Patrol.Release(TrafficTools.Ready, remove: false, quiet: true);
        }

        protected override void Release()
        {
            _stop.Clear();
            ReleaseCars();
            _road.Clear();
        }
    }
}
