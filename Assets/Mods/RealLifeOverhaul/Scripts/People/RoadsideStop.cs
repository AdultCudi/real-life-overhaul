using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The crew of a parked patrol car dealing with the car it stopped: an officer walks to the driver's door and back, or
    /// arrests the driver (Crime/ArrestSequence). Ticked by the patrol car's owner.
    /// </summary>
    public sealed class RoadsideStop
    {
        private const float TickInterval = 0.25f;   // seconds
        private const float ArrestMost = 60f;   // seconds an arrest may take before everyone is taken off the street

        private enum Step { None, Approach, Talk, Return, Arrest }

        private readonly ActorRole _officer = new ActorRole();
        private readonly ActorRole _partner = new ActorRole();
        private readonly ActorRole _suspect = new ActorRole();
        private readonly ArrestSequence _arrest = new ArrestSequence();

        private Step _step = Step.None;
        private float _stepEnd;
        private float _nextTick;
        private Vector3 _patrolDoor;
        private Vector3 _stoppedDoor;

        /// <summary>True when nobody is out of the cars: the stop is over, or never started.</summary>
        public bool Finished => _step == Step.None;


        /// <summary>
        /// Starts the stop; false, with nothing done, if no officer could be put out. <paramref name="stoppedDoor"/> is a
        /// point on the road beside the stopped car's driver's door.
        /// </summary>
        public bool Begin(VehicleComponent patrol, Vector3 stoppedDoor, bool arrest, float now)
        {
            Clear();
            if (!Settings.People || patrol == null)
            {
                return false;
            }
            bool left = StreetSpots.BesideCar(patrol, StreetSpots.Left, 1f, out _patrolDoor);
            bool right = StreetSpots.BesideCar(patrol, StreetSpots.Right, 1f, out Vector3 kerbDoor);
            if (!left && !right)
            {
                return false;
            }
            if (!left)
            {
                _patrolDoor = kerbDoor;
            }
            if (!_officer.Enter(_patrolDoor, ActorLook.Officer, 2f, onRoad: true))
            {
                return false;
            }
            ActorPool.PlayDoorSound(_patrolDoor);
            _stoppedDoor = stoppedDoor;
            _nextTick = now;

            // The second officer only where they can stand out of the traffic: on a pavement by the patrol car.
            Vector3 outward = kerbDoor - _patrolDoor;
            outward.y = 0f;
            if (left && right && outward.sqrMagnitude > 0.25f
                && StreetSpots.OnPavement(kerbDoor + outward.normalized * 1.2f, 2.5f, out Vector3 kerb)
                && _partner.Enter(kerb, ActorLook.Officer, 1.5f, onRoad: false))
            {
                _partner.Stand(stoppedDoor);
            }

            if (arrest && _suspect.Enter(stoppedDoor, ActorLook.Civilian, 2f, onRoad: true))
            {
                ActorPool.PlayDoorSound(stoppedDoor);
                _arrest.BeginAtCar(_suspect, _officer, patrol, now);
                _step = Step.Arrest;
                _stepEnd = now + ArrestMost;
                return true;
            }

            // Stops a pace or two short of the door, on the side they came from.
            Vector3 back = _patrolDoor - stoppedDoor;
            back.y = 0f;
            float distance = back.magnitude;
            Vector3 stand = distance < 2f ? _patrolDoor : stoppedDoor + back / distance * 1.5f;
            _officer.Go(stand, distance > 16f ? Pace.Jog : Pace.Walk, stoppedDoor, 25f);
            _step = Step.Approach;
            _stepEnd = now + 26f;
            return true;
        }

        /// <summary>Everyone leaves the street at once. Safe to call at any time.</summary>
        public void Clear()
        {
            _arrest.Clear();   // cuffs off before the driver goes back to the pool
            _officer.Exit();
            _partner.Exit();
            _suspect.Exit();
            _step = Step.None;
        }

        private static readonly int HotTick = HotSpots.Slot("Traffic: RoadsideStop.Tick");
        public void Tick(float now)
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (_step == Step.None || now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;
            _officer.Tick(now);
            _partner.Tick(now);
            _suspect.Tick(now);

            switch (_step)
            {
                case Step.Arrest:
                    _arrest.Tick(now, PlayerHelper.GetPosition());
                    if (_arrest.Finished || now >= _stepEnd)
                    {
                        Clear();
                    }
                    break;

                case Step.Approach:
                    if (_officer.Settled || !_officer.Present || now >= _stepEnd)
                    {
                        _officer.Stand(_stoppedDoor);
                        _officer.Work(SceneClips.Clipboard);   // writing it out at the window; they stand if the clip is missing
                        _step = Step.Talk;
                        _stepEnd = now + Random.Range(3.5f, 6f);
                    }
                    break;

                case Step.Talk:
                    if (now >= _stepEnd)
                    {
                        _officer.Board(_patrolDoor, Pace.Walk);
                        _partner.Board(_partner.Position, Pace.Walk, 4f);   // back in at the door they are standing by
                        _step = Step.Return;
                        _stepEnd = now + 22f;
                    }
                    break;

                case Step.Return:
                    if (!_officer.Present || now >= _stepEnd)
                    {
                        Clear();
                    }
                    break;
            }
        }
    }
}
