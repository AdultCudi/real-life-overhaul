using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's part for the people at the stop: those who wait, board and get out.</summary>
    public sealed partial class TransitDirector
    {
        private void RunRiders(float now)
        {
            if (_served == null)
            {
                return;
            }
            bool standing = _bus.State == TransitBus.Phase.Standing;

            // Somebody new walks up now and then, while no bus stands.
            if (!standing && now >= _nextWaiter && Settings.TransitPeople)
            {
                _nextWaiter = now + Random.Range(35f, 75f);
                for (int i = 0; i < Waiters; i++)
                {
                    if (_waiting[i].Step == RiderStep.None)
                    {
                        Arrive(i);
                        break;
                    }
                }
            }

            bool doorBusy = _alighter.Step == RiderStep.Exiting || _rider.AtDoor;   // the player on their way to the front door has it first
            for (int i = 0; i < Waiters; i++)
            {
                doorBusy |= _waiting[i].Step == RiderStep.Boarding || _waiting[i].Step == RiderStep.Entering;
            }
            for (int i = 0; i < Waiters; i++)
            {
                StreetActor person = _waiting[i].Person;
                if (_waiting[i].Step == RiderStep.None)
                {
                    continue;
                }
                if (person == null || !person.IsValid || person.Limp || !person.InUse)
                {
                    Drop(ref _waiting[i], _waiters);   // knocked down, or gone with the scene: no longer ours to move
                    continue;
                }
                switch (_waiting[i].Step)
                {
                    case RiderStep.Arriving:
                        if (person.Within(0.3f))
                        {
                            Vector3 here = person.Position;
                            BusStopDressing shelter = _waiting[i].Seat >= 0 ? DressingOf(_served) : null;
                            if (shelter != null && shelter.Sheltered && CitySpots.Flat(here - _waiting[i].Spot) < 0.6f * 0.6f)
                            {
                                // Their seat: feet where the clip wants them, back to the bench, down, then the seated wait.
                                person.StandAt(_waiting[i].Spot, _waiting[i].Spot + shelter.SeatFacing * 3f);
                                _waiting[i].Sat = person.Work("sit_bench", ClipEnd.Hold, "sit_wait");
                                if (!_waiting[i].Sat)
                                {
                                    person.Work("queue_wait");
                                }
                            }
                            else
                            {
                                Unseat(ref _waiting[i]);
                                person.StandAt(here, here - Vector3.Cross(Vector3.up, _served.Along) * 3f + _served.Along * -2f);
                                person.Work("queue_wait");
                            }
                            _waiting[i].Step = RiderStep.Waiting;
                        }
                        break;

                    case RiderStep.Waiting:
                        if (standing && _boardable && !doorBusy && _doorFound && _bus.Standing > 1.5f)
                        {
                            if (_waiting[i].Sat && person.Work("sit_bench_up") && ClipLibrary.TryGet("sit_bench_up", out CityClip up))
                            {
                                _waiting[i].Step = RiderStep.Rising;
                                _waiting[i].Until = now + up.Length;
                                doorBusy = true;   // the door is theirs: nobody sets off for it while they get up
                                break;
                            }
                            ToDoor(ref _waiting[i], now);
                            doorBusy |= _waiting[i].Step == RiderStep.Boarding;
                        }
                        break;

                    case RiderStep.Rising:
                        if (now >= _waiting[i].Until)
                        {
                            if (standing && _boardable && _doorFound)
                            {
                                ToDoor(ref _waiting[i], now);
                            }
                            else
                            {
                                Unseat(ref _waiting[i]);
                                person.ClearPose();
                                person.Work("queue_wait");
                                _waiting[i].Step = RiderStep.Waiting;
                            }
                        }
                        break;

                    case RiderStep.Boarding:
                        if (!standing || now > _waiting[i].Until)
                        {
                            _waiting[i].Spot = _served.Wait;
                            _waiting[i].Step = person.MoveTo(_waiting[i].Spot, Pace.Walk) ? RiderStep.Arriving : RiderStep.Waiting;
                            break;
                        }
                        if (person.Within(0.15f))
                        {
                            Vector3 here = person.Position;
                            if (CitySpots.Flat(here - _door) > 0.4f * 0.4f)
                            {
                                break;   // still closing on it, or held up: the clip is made for that one place
                            }
                            person.StandAt(here, here + _doorInwards * 3f);
                            string clipName = ClipLibrary.Has("board_step") ? "board_step" : "job_car_enter";
                            bool clip = ClipLibrary.TryGet(clipName, out CityClip enter) && person.Work(clipName, ClipEnd.Hold);
                            _waiting[i].Until = now + (clip ? enter.Length : 0.4f);
                            _waiting[i].Step = RiderStep.Entering;
                        }
                        break;

                    case RiderStep.Entering:
                        if (now >= _waiting[i].Until)
                        {
                            JobCast.DoorSound(person.Position);
                            Drop(ref _waiting[i], _waiters);
                            _boarded++;
                            _dwellUntil = Mathf.Max(_dwellUntil, now + 2f);
                        }
                        break;
                }
            }

            if (_alighter.Step == RiderStep.None)
            {
                return;
            }
            StreetActor leaving = _alighter.Person;
            if (leaving == null || !leaving.IsValid || leaving.Limp || !leaving.InUse)
            {
                Drop(ref _alighter, _others);
                return;
            }
            if (_alighter.Step == RiderStep.Exiting && now >= _alighter.Until)
            {
                leaving.ClearPose();
                _alighter.Step = RiderStep.Leaving;
                _alighter.Until = now + 60f;
                if (!leaving.Place(leaving.Position, true) || !leaving.MoveTo(_alighter.Spot, Pace.Walk))
                {
                    _alighter.Until = now + 4f;
                }
            }
            else if (_alighter.Step == RiderStep.Leaving && ((leaving.Within(0.4f) && !CitySpots.InView(leaving.Position)) || now >= _alighter.Until))
            {
                Drop(ref _alighter, _others);
            }
        }

        // Off to the door: they came along the pavement or off the bench; the last steps are on the road surface.
        private void ToDoor(ref Rider rider, float now)
        {
            StreetActor person = rider.Person;
            Unseat(ref rider);
            person.ClearPose();
            if (person.Place(person.Position, true) && person.MoveTo(_door, Pace.Walk))
            {
                rider.Step = RiderStep.Boarding;
                rider.Until = now + 16f;
            }
            else
            {
                Vector3 here = person.Position;
                person.StandAt(here, here + _doorInwards * -3f);
                rider.Step = RiderStep.Waiting;
            }
        }

        // Somebody walks up to the stop from out of view and waits: on the bench if a seat is free and the clips are there, else by the pole.
        private void Arrive(int slot)
        {
            Vector3 right = Vector3.Cross(Vector3.up, _served.Along);
            Vector3 want = _served.Wait - _served.Along * (slot * 1.15f) + right * (slot * 0.25f);
            if (!CitySpots.Stand(want, 0.35f, out Vector3 spot))
            {
                return;
            }
            int seat = -1;
            BusStopDressing shelter = DressingOf(_served);
            if (shelter != null && shelter.Sheltered && TransitSeat.Possible && Random.value < 0.6f)
            {
                // A seat nobody has: theirs from now until they board or go.
                for (int i = BusStopDressing.Seats - 1; i >= 0 && seat < 0; i--)
                {
                    if (shelter.SeatFree(i) && NavMesh.SamplePosition(shelter.SeatFeetAt(i), out NavMeshHit hit, 0.12f, NavMesh.AllAreas))
                    {
                        seat = i;
                        spot = hit.position;
                    }
                }
            }
            Vector3 from = spot;
            bool walksIn = CitySpots.InView(spot);
            if (walksIn && (!CitySpots.PavementRing(spot, 26f, 44f, true, out from) || !CitySpots.Reachable(from, spot)))
            {
                return;   // nowhere out of sight to come from: nobody comes just now
            }
            if (_waiters == null)
            {
                _waiters = new JobCast(this, Logger);
            }
            StreetActor person = _waiters.Put(from, false);   // on the pavement only, until the bus is there
            if (person == null)
            {
                return;
            }
            _waiting[slot].Person = person;
            _waiting[slot].Spot = spot;
            _waiting[slot].Seat = seat;
            _waiting[slot].Sat = false;
            if (seat >= 0)
            {
                shelter.TakeSeat(seat, false);
            }
            if (walksIn && person.MoveTo(spot, Pace.Walk))
            {
                _waiting[slot].Step = RiderStep.Arriving;
                return;
            }
            if (seat >= 0)
            {
                person.StandAt(spot, spot + shelter.SeatFacing * 3f);
                _waiting[slot].Sat = person.Work("sit_bench", ClipEnd.Hold, "sit_wait");
                if (!_waiting[slot].Sat)
                {
                    person.Work("queue_wait");
                }
            }
            else
            {
                person.StandAt(spot, spot - right * 3f - _served.Along * 2f);
                person.Work("queue_wait");
            }
            _waiting[slot].Step = RiderStep.Waiting;
        }

        // Somebody gets out at the door and walks away along the pavement. Down a bus's step there is no clip: they walk.
        private void Alight(float now)
        {
            if (!Settings.TransitPeople || _alighter.Step != RiderStep.None
                || !CitySpots.PavementRing(_served.Wait, 30f, 48f, true, out Vector3 away) || !CitySpots.Reachable(_served.Wait, away))
            {
                return;
            }
            if (_others == null)
            {
                _others = new JobCast(this, Logger);
            }
            StreetActor person = _others.Put(_exitDoor, true);
            if (person == null)
            {
                return;
            }
            person.StandAt(_exitDoor, _exitDoor - _doorInwards * 3f);
            JobCast.DoorSound(_exitDoor);
            _alighter.Person = person;
            _alighter.Spot = away;
            _alighter.Seat = -1;
            _alighter.Sat = false;
            _alighter.Step = RiderStep.Exiting;
            _alighter.Until = now + 0.8f;
            _alighted++;
        }

        private void Unseat(ref Rider rider)
        {
            if (rider.Seat >= 0)
            {
                BusStopDressing shelter = DressingOf(_served);
                if (shelter != null)
                {
                    shelter.LeaveSeat(rider.Seat);
                }
            }
            rider.Seat = -1;
            rider.Sat = false;
        }

        private void Drop(ref Rider rider, JobCast cast)
        {
            Unseat(ref rider);
            if (cast != null && rider.Person != null)
            {
                cast.Release(rider.Person);
            }
            rider.Person = null;
            rider.Step = RiderStep.None;
        }

        private void ReleaseRiders()
        {
            for (int i = 0; i < Waiters; i++)
            {
                Drop(ref _waiting[i], _waiters);
            }
            Drop(ref _alighter, _others);
            _waiters?.ReleaseAll();
            _others?.ReleaseAll();
        }
    }
}
