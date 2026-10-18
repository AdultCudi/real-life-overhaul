using System.Globalization;
using BigAmbitions.InputSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>The bus rider's part for the doors: the way to one on the walkable ground, and getting off by one.</summary>
    public sealed partial class BusRider
    {
        private bool _frontWayOut;
        private BusInside.Approach[] _tries = BusInside.BoardTries;
        private bool _byChoice;             // the middle door is the nearer to where the player stood
        private static int _propsMask = -1;
        private static NavMeshPath _way;

        private static bool WantsOff()
        {
            return !ThirdPersonCamera.IsUiOpen() && !GameManager.ShouldBlockKeyboardShortcuts() && PlayerAction.Move.Vector().sqrMagnitude > 0.25f;
        }

        // Off again before the bus has left: the fare back, up from the seat, out by the nearest door.
        private void GetOff(float now, string why)
        {
            if (_gotOff)
            {
                return;
            }
            _gotOff = true;
            GiveBack(why);
            Logger?.Info("Transit check (getting off): " + why + ": they get off again by the " + (_at.Z > 3.7f ? "front" : "middle") + " door: PASS.");
            Up(now);
        }

        private void Up(float now)
        {
            _clips.StopOver(0.1f);
            if (_step == Step.Seated || _step == Step.SittingDown)
            {
                if (Play("sit_bench_up", 0.15f, ClipEnd.Return))
                {
                    _step = Step.Rising;
                    _until = now + Length("sit_bench_up", 1.6f);
                    return;
                }
            }
            WayOut(now);
        }

        private void WayOut(float now)
        {
            if (_clips.Active && _clips.Current != null)
            {
                _clips.Stop(0.25f);
            }
            _frontWayOut = BusInside.WayOut(_at, _path);
            // The place to step down to has a way from it onto the pavement, by the nearer door first.
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            _offChosen = false;
            if (bus != null)
            {
                bool arriving = _stage == BusInside.Stage.Arriving;
                TransitStop stop = arriving ? _to : _from;
                BusInside.Approach[] tries = _frontWayOut ? BusInside.OffTriesFront : BusInside.OffTries;
                Vector3 road = default;
                int chosen = BusInside.First(tries, a => OffAt(a, bus, stop, out road));
                if (chosen >= 0)
                {
                    _offChosen = true;
                    _road = road;
                    bool front = !tries[chosen].Middle;
                    if (front != _frontWayOut)
                    {
                        BusInside.WayOutBy(_at, front, _path);
                        _frontWayOut = front;
                    }
                    if (chosen > 0)
                    {
                        Logger?.Info("Transit check (getting off): beside the " + (_frontWayOut ? "front" : "middle")
                                     + " door nothing could be stepped down to with a way onto the pavement: they get off "
                                     + (tries[chosen].Middle ? "by the middle door" : "by the front door")
                                     + (Mathf.Abs(tries[chosen].Along) > 0.01f ? ", " + Mathf.Abs(tries[chosen].Along).ToString("F1", CultureInfo.InvariantCulture) + " m along the side" : "")
                                     + ": PASS.");
                    }
                }
            }
            StartWalk(Then.Off);
        }

        // A place to step down to: walkable, nothing standing there, and a way from it to the waiting place.
        private bool OffAt(BusInside.Approach approach, BusVehicle bus, TransitStop stop, out Vector3 road)
        {
            BusInside.Spot at = approach.At;
            if (!Ground(bus.transform.TransformPoint(new Vector3(at.X, 0f, at.Z)), bus, out road) || Furnished(road))
            {
                return false;
            }
            return stop == null || Reachable(road, stop.Wait);
        }

        // From the threshold down onto the road, to the place found beside the door.
        private void StepDown(float now, BusVehicle bus)
        {
            if (bus == null)
            {
                Abort("the bus went", put: true);
                return;
            }
            BusInside.Spot to = _frontWayOut ? BusInside.FrontExitTo : BusInside.ExitTo;
            Vector3 want = bus.transform.TransformPoint(new Vector3(to.X, 0f, to.Z));
            if (!_offChosen && !Ground(want, bus, out _road))
            {
                _road = SafeSpot();   // nothing to step down to beside the door: put down at the stop
                Finish("there was no ground to step down to beside the door", put: true);
                return;
            }
            _roadFrom = bus.OnFloor(_at);
            _step = Step.SteppingDown;
            _since = now;
            _until = now + BusInside.StepDown;
        }

        private void StepDownFrame(float delta, float now, BusVehicle bus)
        {
            float k = Mathf.Clamp01((now - _since) / BusInside.StepDown);
            Vector3 at = Vector3.Lerp(_roadFrom, _road, k);
            at.y = Mathf.Lerp(_roadFrom.y, _road.y, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f - 0.3f)));   // the drop in the middle of the stride
            Vector3 way = _road - _roadFrom;
            way.y = 0f;
            Quaternion facing = way.sqrMagnitude > 0.001f ? Quaternion.LookRotation(way.normalized) : _player.transform.rotation;
            _body.Move(k < 1f ? way.normalized * (way.magnitude / BusInside.StepDown) : Vector3.zero);
            _player.transform.SetPositionAndRotation(at, facing);
            _lastPut = at;
            if (k < 1f)
            {
                return;
            }
            bool arriving = _stage == BusInside.Stage.Arriving;
            Release(_road);   // the blocker off first: the game's walk onto the pavement is refused while one is on
            if (arriving)
            {
                Logger?.Info("Transit check (getting off): the player stands up, walks to the middle door and steps down onto the road at " + Name(_to) + ", "
                             + Mathf.Sqrt(CitySpots.Flat(_road - _to.Wait)).ToString("F1", CultureInfo.InvariantCulture) + " m from the waiting place; the game walks them onto the pavement: PASS.");
            }
            _player.SetGoal(arriving ? _to.Wait : _from.Wait, null);
            _director.RiderOff(_bus, arriving);
            Clear();
        }

        // The place to walk to, to get on: the first of the tries that is walkable, free and reachable by the game's navigation.
        // Returns which was taken (0 for the director's own place), or -1 for none.
        private int ChooseApproach(BusVehicle bus, Vector3 from, Vector3 door, out Vector3 at, out bool middle)
        {
            at = door;
            middle = false;
            if (bus == null)
            {
                return -1;
            }
            Vector3 start = Nearest(from);
            // The nearer door: many city buses are boarded by any door, with a validator by each.
            Vector3 front = bus.transform.TransformPoint(new Vector3(BusInside.BoardFrom.X, 0f, BusInside.BoardFrom.Z));
            Vector3 back = bus.transform.TransformPoint(new Vector3(BusInside.MiddleBoardFrom.X, 0f, BusInside.MiddleBoardFrom.Z));
            _byChoice = BusInside.ByMiddleDoor(Mathf.Sqrt(CitySpots.Flat(from - front)), Mathf.Sqrt(CitySpots.Flat(from - back)));
            _tries = _byChoice ? BusInside.BoardTriesMiddle : BusInside.BoardTries;
            if (!_byChoice && !Furnished(door) && !ByPole(door) && Reachable(start, door))
            {
                return 0;
            }
            Vector3 found = door;
            int chosen = BusInside.First(_tries, a =>
            {
                BusInside.Spot spot = a.At;
                return Ground(bus.transform.TransformPoint(new Vector3(spot.X, 0f, spot.Z)), bus, out found) && !Furnished(found) && !ByPole(found)
                       && Reachable(start, found);
            });
            if (chosen < 0)
            {
                return -1;
            }
            at = found;
            middle = _tries[chosen].Middle;
            return chosen + 1;
        }

        private string ApproachWords(int tried)
        {
            if (tried <= 0)
            {
                return "the front door's own place";
            }
            BusInside.Approach a = _tries[tried - 1];
            string door = a.Middle ? _byChoice ? "the middle door, the nearer one" : "the middle door (the front door's place could not be walked to)"
                : _byChoice ? "the front door (the middle door's place could not be walked to)" : "the front door";
            return door + (Mathf.Abs(a.Along) > 0.01f ? ", " + Mathf.Abs(a.Along).ToString("F1", CultureInfo.InvariantCulture) + " m along the kerb" : "");
        }

        // A whole way on the game's navigation mesh from one point to the other.
        private static bool Reachable(Vector3 from, Vector3 to)
        {
            if (_way == null)
            {
                _way = new NavMeshPath();
            }
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _way) && _way.status == NavMeshPathStatus.PathComplete;
        }

        // The stop's own pole (on no layer the game's furniture is on) where a person would stand, or so near they would walk into it.
        private bool ByPole(Vector3 at)
        {
            if (_from == null)
            {
                return false;
            }
            float reach = BusInside.PoleReach + BusInside.PoleClear + 0.22f;
            return CitySpots.Flat(at - _from.Sign) < reach * reach;
        }

        private static bool Furnished(Vector3 at)
        {
            if (_propsMask < 0)
            {
                _propsMask = LayerMask.GetMask("StreetProps");
            }
            return _propsMask != 0 && Physics.CheckCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.6f, 0.22f, _propsMask, QueryTriggerInteraction.Ignore);
        }
    }
}
