using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player getting on a bus, riding it and getting off, seen: the game walks them to a door, board_step takes them up,
    /// and from then on the bus carries them as the game carries the player on a scooter (agent, capsule and physics off) but
    /// never parented to it, put each frame where they stand on its floor; the validator, a seat or the strap at
    /// <see cref="BusInside"/>'s places; the ride; off at the far end. Every way out other than getting off puts the player on
    /// the ground at once (<see cref="Abort"/>). Per frame only while a ride is on: the placing, the walk, the clips' steps.
    /// </summary>
    public sealed partial class BusRider
    {
        private const NavigationBlocker Blocker = NavigationBlocker.Vehicle;   // not the ride's own Taxi: TransitRide lifts that at the far end
        private const float ToDoorFor = 16f;       // seconds the walk to the door may take
        private const float TurnSpeed = 300f;      // degrees a second, turning on the spot
        private const float SquareFor = 2.5f;      // seconds for the last steps to the door and the turn to it

        public enum Step { None, ToDoor, Squaring, Stepping, Walking, Turning, Tapping, SittingDown, Seated, Holding, Leaving, Fading, Away, Arrived, Rising, SteppingDown, Riding }
        private enum Then { Tap, Seat, Strap, Off }

        public static IModLogger Logger;

        private readonly TransitDirector _director;
        private readonly List<BusInside.Spot> _path = new List<BusInside.Spot>(12);
        private Step _step;
        private BusInside.Stage _stage;
        private Then _then;
        private PlayerController _player;
        private ThirdPersonCharacter _body;
        private ActivityAnimator _clips;
        private TransitBus _bus;            // the bus they are on: the stop's, then the one at the far end
        private TransitStop _from, _to;
        private BusInside.Place _place;     // the seat or the strap
        private bool _held;
        private bool _blocked;
        private bool _departing;
        private bool _gotOff;               // they got off again before the bus left
        private bool _reached;
        private float _toDoor;              // metres to the door when the walk began
        private float _fare;
        private float _until;
        private float _since;
        private float _walked;
        private float _pace = BusInside.Walk;   // the game's own walking pace, so its walk plays at its own speed
        private float _length;
        private float _facing;
        private float _turnTo;
        private float _up;
        private BusInside.Spot _at;
        private Vector3 _lastPut;
        private Vector3 _road;              // where they step down to, on the navigation mesh
        private Vector3 _roadFrom;
        private Vector3 _door;              // where they step in from, on the road
        private Vector3 _shutAt;
        private UnityEngine.Events.UnityAction _onReached;
        private bool _middleIn;             // in by the middle door: the nearer one, or the front door's place could not be reached
        private bool _waitedOff;            // and somebody getting off there was waited for
        private const float MiddleWaitMost = 20f;   // seconds after setting off for the door that somebody getting off is waited for, at the most
        private TransitStop _passed;        // riding through the city: the last stop the bus called at, or null
        private int _passedCount;
        private bool _offChosen;            // the place to step down to has been chosen and found reachable

        public BusRider(TransitDirector director)
        {
            _director = director;
        }

        /// <summary>From the walk to the door until they are on the road again.</summary>
        public bool Active => _step != Step.None;

        /// <summary>From the step up (the fare taken) until they are off: the game's events and the other scenes stand aside.</summary>
        public bool Aboard => _step != Step.None && _step != Step.ToDoor;

        /// <summary>True while the bus must stand for them, from the walk to its door until it has left with them.</summary>
        public bool HoldsBus => _bus != null && (_stage == BusInside.Stage.Walking || _stage == BusInside.Stage.Boarding || (_stage == BusInside.Stage.Aboard && !_departing));

        /// <summary>The same for this bus, and while they get off it at the end of a ride.</summary>
        public bool Holds(TransitBus bus)
        {
            return bus != null && _bus == bus && (HoldsBus || _stage == BusInside.Stage.Arriving);
        }

        /// <summary>True while riding through the city in real time.</summary>
        public bool RidingThrough => _step == Step.Riding && _stage == BusInside.Stage.Riding;

        public TransitStop Passed => _passed;   // the last stop the bus called at on the way, or null

        /// <summary>True while the front door is theirs, until they are past the validator.</summary>
        public bool AtDoor => _step == Step.ToDoor || _step == Step.Squaring || _step == Step.Stepping || (_step == Step.Walking && _then == Then.Tap);

        /// <summary>True once seated or holding on: the bus may shut its doors and go.</summary>
        public bool WantsDeparture => !_departing && !_gotOff && _stage == BusInside.Stage.Aboard && ((_step == Step.Seated || _step == Step.Holding) && Time.time >= _until);

        public Step Now => _step;
        public BusInside.Stage Stage => _stage;

        /// <summary>True when the last boarding failed for the fare (the game has said so).</summary>
        public bool RefusedFare { get; private set; }

        public TransitBus ArrivalBus => _stage == BusInside.Stage.Arriving ? _bus : null;   // the bus at the far end with them in it

        /// <summary>Whether the clips this needs are there and the bus can be seen into.</summary>
        public static bool Possible(BusVehicle bus)
        {
            return bus != null && bus.HasInside && ClipLibrary.Ready && ActivityAnimators.Working && ClipLibrary.Has("board_step") && ClipLibrary.Has("sit_bench")
                   && ClipLibrary.Has("sit_wait") && ClipLibrary.Has("sit_bench_up") && ClipLibrary.Has("strap_hang");
        }

        /// <summary>Sets the player off for a door of the bus at the stop; false, with why and nothing changed, if it cannot be done.</summary>
        public bool Board(TransitBus bus, TransitStop from, TransitStop to, Vector3 door, bool full, out string why)
        {
            why = null;
            var game = InstanceBehavior<GameManager>.Instance;
            PlayerController player = game != null ? game.playerController : null;
            ThirdPersonCharacter body = player != null ? player.Character : null;
            if (Active || bus == null || bus.Vehicle == null || from == null || to == null || body == null || body.animator == null || body.navmeshAgent == null)
            {
                why = "a ride is already under way, or the player or the bus is not there";
                return false;
            }
            if (!Possible(bus.Vehicle))
            {
                why = bus.Vehicle.HasInside ? "the clips cannot be played now" : "the bus is not the modelled one with its inside (the props are older)";
                return false;
            }
            if (!body.visible || RagdollSystem.Holds(body) || PlayerHelper.IsHoldingItem || !body.navmeshAgent.isActiveAndEnabled || !body.navmeshAgent.isOnNavMesh)
            {
                why = PlayerHelper.IsHoldingItem ? "the player has something in their hands" : "the player is not on their feet on the street";
                return false;
            }
            ActivityAnimator clips = ActivityAnimators.For(body.animator);
            if (clips == null || !clips.Usable)
            {
                why = "the player's animator cannot play the mod's clips";
                return false;
            }
            _player = player;
            _body = body;
            _clips = clips;
            _pace = GamePace();
            _bus = bus;
            _from = from;
            _to = to;
            _door = door;
            _place = full ? BusInside.Place.Strap : (UnityEngine.Random.value < 0.5f ? BusInside.Place.Seat : BusInside.Place.WindowSeat);
            _fare = 0f;
            _departing = false;
            _gotOff = false;
            RefusedFare = false;
            _stage = BusInside.Stage.Walking;
            _since = Time.time;
            _passed = null;
            _passedCount = 0;
            _offChosen = false;
            _waitedOff = false;
            // The nearer door's place if it can be walked to and nothing stands there, else a little along the kerb, else the other door.
            int tried = ChooseApproach(bus.Vehicle, player.transform.position, door, out door, out _middleIn);
            if (tried < 0)
            {
                Logger?.Info("Transit check (boarding): no way on the walkable ground to either of the bus's doors at " + Name(from) + " (" + BusInside.BoardTries.Length
                             + " places by them tried): the player is not walked onto the bus: FAIL.");
                why = "no way on the walkable ground to either door";
                Clear();
                return false;
            }
            _door = door;
            _toDoor = Mathf.Sqrt(CitySpots.Flat(player.transform.position - door));
            if (_toDoor < 0.25f)
            {
                _step = Step.ToDoor;
                Reached();
                why = RefusedFare ? "the fare could not be paid" : "the bus had left";
                return _step != Step.None;
            }
            _reached = false;
            _step = Step.ToDoor;
            _until = Time.time + ToDoorFor;
            player.SetGoal(door, Guard.Listener(_onReached ?? (_onReached = Reached)));
            if (!player.hasOnGoalReachedAction && !_reached)
            {
                Finish("the game would not walk the player to the door", put: false);
                why = "the game would not walk the player to the door";
                return false;
            }
            Logger?.Info("Transit check (boarding): the player walks to the " + (_middleIn ? "middle" : "front") + " door of the " + Line() + " at " + Name(from) + ", "
                         + Mathf.Sqrt(CitySpots.Flat(player.transform.position - door)).ToString("F1", CultureInfo.InvariantCulture) + " m, by the game's own walk, to "
                         + ApproachWords(tried) + "; " + (full ? "the bus is full: they will stand at the strap"
                             : "they will sit on the " + (_place == BusInside.Place.Seat ? "aisle" : "window") + " seat opposite the middle door") + ": PASS.");
            return true;
        }

        // At the door: the fare, the blocker, and the last steps square to the door.
        private void Reached()
        {
            _reached = true;
            if (_step != Step.ToDoor)
            {
                return;   // a walk the ride has already let go of
            }
            if (_bus == null || _bus.State != TransitBus.Phase.Standing || _bus.Vehicle == null)
            {
                Finish("the bus had left by the time they reached its door", put: false);
                return;
            }
            if (!TransitState.PayBus(out bool pass))
            {
                Logger?.Info("Transit check (boarding): the player reached the door but cannot pay the fare of " + TransitState.BusFare.ToString("F2", CultureInfo.InvariantCulture)
                    + ": they stay on the pavement: FAIL.");
                RefusedFare = true;
                Finish("no fare", put: false);
                return;
            }
            _fare = pass ? 0f : TransitState.BusFare;
            _stage = BusInside.Stage.Boarding;
            _player.ResetNavigation();
            _player.SetNavigationBlocker(Blocker);
            _blocked = true;
            _step = Step.Squaring;
            _until = Time.time + SquareFor;
            _director.RiderBoards(_from, _to, pass);
        }

        /// <summary>Per frame, only while <see cref="Active"/>.</summary>
        public void Frame(float delta, float now)
        {
            if (_step == Step.None || _step == Step.Away)
            {
                return;
            }
            if (_body == null || _player == null || _body.animator == null)
            {
                Abort("the player is no longer there", put: false);
                return;
            }
            if (RagdollSystem.Holds(_body) || EnergyHelper.goingToHospital || GameMembers.PlayerDead || UI.Load.LoadScene.isLoading)
            {
                Abort("the player was knocked over, taken to hospital or the game is loading", put: false);
                return;
            }
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            if (_held && (bus == null || !bus.Out))
            {
                if (_stage == BusInside.Stage.Riding)
                {
                    // Taken off the street on the way: the rest under the fade.
                    Logger?.Info("Transit check (journey): the bus was taken off the street with the player riding it: the rest of the ride passes under the fade: PASS.");
                    Release(SafeSpot());
                    _step = Step.Fading;
                    _since = Time.time;
                    _director.JourneySkip("the bus was taken off the street");
                    return;
                }
                if (_stage == BusInside.Stage.Leaving)
                {
                    // Taken off the street as it pulled away: the ride goes on from the stop they boarded at.
                    Logger?.Info("Transit check (ride): the bus was taken off the street as it pulled away with the player in it: the ride goes on under the fade from "
                                 + Name(_from) + ": PASS.");
                    Release(SafeSpot());
                    if (_step != Step.Fading)
                    {
                        _step = Step.Fading;
                        _since = Time.time;
                        _director.RiderRides(_from, _to);
                    }
                    return;
                }
                Abort("the bus was taken off the street with the player on it", put: true);
                return;
            }
            if (_held && _step != Step.Squaring && (_player.transform.position - _lastPut).sqrMagnitude > 1f)
            {
                Abort("something else moved the player " + Vector3.Distance(_player.transform.position, _lastPut).ToString("F1", CultureInfo.InvariantCulture) + " m", put: false);
                return;
            }
            switch (_step)
            {
                case Step.ToDoor:
                    // A click elsewhere replaces the game's walk with one away from the door.
                    if (!_reached && (!_player.hasOnGoalReachedAction || now > _until || _bus == null || _bus.State != TransitBus.Phase.Standing
                                      || Mathf.Sqrt(CitySpots.Flat(_player.transform.position - _door)) > _toDoor + 4f))
                    {
                        Finish(now > _until ? "the walk to the door took too long" : _bus == null || _bus.State != TransitBus.Phase.Standing ? "the bus left before they reached it"
                            : "the player went another way", put: false);
                    }
                    return;

                case Step.Squaring:
                    Square(delta, now, bus);
                    return;

                case Step.Stepping:
                    if (bus == null || !bus.Out || _bus.State != TransitBus.Phase.Standing)
                    {
                        Abort("the bus went while they stepped in", put: false);
                        return;
                    }
                    if (now >= _until)
                    {
                        StepUp(bus);
                    }
                    return;

                case Step.Walking:
                    WalkOn(delta, now, bus);
                    break;

                case Step.Turning:
                    _facing = Mathf.MoveTowardsAngle(_facing, _turnTo, TurnSpeed * delta);
                    if (Mathf.Abs(Mathf.DeltaAngle(_facing, _turnTo)) < 1f)
                    {
                        _facing = _turnTo;
                        Arrive(now);
                    }
                    break;

                case Step.Tapping:
                    if (now >= _until)
                    {
                        WalkTo(_middleIn ? BusInside.Place.MiddleReader : BusInside.Place.Reader, _place, _place == BusInside.Place.Strap ? Then.Strap : Then.Seat);
                    }
                    break;

                case Step.SittingDown:
                    if (now >= _until)
                    {
                        // sit_bench ends in sit_wait's first pose exactly, so no fade.
                        if (!Play("sit_wait", 0f, ClipEnd.Return))
                        {
                            GetOff(now, "sit_wait would not play");
                            break;
                        }
                        _step = _departing ? Step.Leaving : Step.Seated;
                        _until = now + 0.4f;
                        if (_stage == BusInside.Stage.Aboard)
                        {
                            Logger?.Info("Transit check (aboard): the player sits on the " + (_place == BusInside.Place.Seat ? "aisle" : "window") + " seat opposite the middle door, "
                                         + (now - _since).ToString("F1", CultureInfo.InvariantCulture) + " s after setting off for the door: PASS.");
                        }
                    }
                    break;

                case Step.Seated:
                case Step.Holding:
                    if (_stage == BusInside.Stage.Aboard && !_departing && WantsOff())
                    {
                        GetOff(now, "the player moved to get off before the bus left");
                    }
                    break;

                case Step.Leaving:
                    Leave(now, bus);
                    break;

                case Step.Fading:
                    if (!_director.RideRunning && now - _since > 3f)
                    {
                        // The fade never began (another screen took it): no ride, and off at the stop.
                        float fare = BusInside.SaveFareBack(_stage == BusInside.Stage.Riding ? BusInside.Stage.Leaving : _stage, _passedCount) ? _fare : 0f;
                        _fare = 0f;
                        _director.RiderFareBack(fare, "the ride's fade did not begin");
                        Finish("the ride's fade did not begin", put: true);
                        return;
                    }
                    break;

                case Step.Riding:
                    // A move of the player's own asks to get off at the next stop.
                    if (WantsOff())
                    {
                        _director.JourneyGetOff("the player moved");
                    }
                    break;

                case Step.Arrived:
                    ArrivedFrame(now, bus);
                    break;

                case Step.Rising:
                    if (now >= _until)
                    {
                        WayOut(now);
                    }
                    break;

                case Step.SteppingDown:
                    StepDownFrame(delta, now, bus);
                    return;
            }
            if (_step != Step.None && _held && bus != null)
            {
                Put(bus);
                // Seen from above the roof is not drawn, so the player is seen in the bus.
                Camera camera = GameManager.GetMainCamera();
                bus.ShowRoof(camera == null || camera.transform.position.y < bus.transform.position.y + 2.6f);
            }
            if (_step != Step.None && _stage == BusInside.Stage.Aboard && !_departing && _step != Step.Seated && _step != Step.Holding && _step != Step.SittingDown && _step != Step.Rising
                && _step != Step.SteppingDown && !_gotOff && WantsOff())
            {
                GetOff(now, "the player moved to get off before the bus left");
            }
        }

        // The last centimetres to where board_step begins, and the turn to the door; then up the step.
        private void Square(float delta, float now, BusVehicle bus)
        {
            if (bus == null || !bus.Out || _bus.State != TransitBus.Phase.Standing)
            {
                Abort("the bus left as they reached its door", put: false);
                return;
            }
            // Held from here: where board_step begins may be nearer the bus than the navigation mesh reaches.
            Hold();
            Transform place = _player.transform;
            BusInside.Spot from = _middleIn ? BusInside.MiddleBoardFrom : BusInside.BoardFrom;
            Vector3 target = bus.transform.TransformPoint(new Vector3(from.X, 0f, from.Z));
            target.y = place.position.y;
            Quaternion facing = bus.Facing(-90f);
            Vector3 way = target - place.position;
            way.y = 0f;
            Vector3 next = Vector3.MoveTowards(place.position, target, _pace * delta);
            _body.Move(way.sqrMagnitude > 0.0004f ? way.normalized * _pace : Vector3.zero);
            Quaternion turn = way.sqrMagnitude > 0.01f ? Quaternion.LookRotation(way.normalized) : facing;
            place.SetPositionAndRotation(next, Quaternion.RotateTowards(place.rotation, turn, TurnSpeed * delta));
            _lastPut = next;
            bool there = way.sqrMagnitude < 0.03f * 0.03f;
            if (there && Quaternion.Angle(place.rotation, facing) > 2f && now < _until)
            {
                place.rotation = Quaternion.RotateTowards(place.rotation, facing, TurnSpeed * delta);
                return;
            }
            if (!there && now < _until)
            {
                return;
            }
            if (way.sqrMagnitude > 0.35f * 0.35f)
            {
                Abort("something kept them from the door's step", put: false);
                return;
            }
            if (_middleIn && _director.MiddleDoorBusy && now < _since + MiddleWaitMost)
            {
                // Somebody is getting off by the middle door: the player waits for them.
                if (!_waitedOff)
                {
                    _waitedOff = true;
                    Logger?.Info("Transit check (boarding): at the middle door somebody is getting off: the player waits until they are off, then steps up: PASS.");
                }
                _until = Mathf.Max(_until, now + 0.3f);
                return;
            }
            _body.Move(Vector3.zero);
            place.rotation = facing;
            if (!Play("board_step", 0.15f, ClipEnd.Hold))
            {
                Abort("board_step would not play", put: false);
                return;
            }
            _step = Step.Stepping;
            _until = now + Length("board_step", 1.4f);
            JobCast.DoorSound(place.position);
        }

        // board_step has ended on the step: from here on the bus carries them.
        private void StepUp(BusVehicle bus)
        {
            Vector3 was = _player.transform.position;
            Hold();
            _clips.Dispose();   // at once: its last pose is the standing one where they are now put, a hand's breadth higher
            _at = _middleIn ? BusInside.MiddleAboard : BusInside.Aboard;
            _facing = -90f;
            _up = 0f;
            Put(bus);
            _stage = BusInside.Stage.Aboard;
            Logger?.Info("Transit check (boarding): up the step at the " + (_middleIn ? "middle door: they tap in at the validator inside it" : "front door")
                         + " with board_step; on the floor " + (_player.transform.position.y - was.y).ToString("F2", CultureInfo.InvariantCulture)
                         + " m over the road (the clip ends " + BusInside.StepUp.ToString("F2", CultureInfo.InvariantCulture) + " up, the floor is "
                         + BusInside.Floor.ToString("F2", CultureInfo.InvariantCulture) + "): PASS.");
            WalkTo(_middleIn ? BusInside.Place.MiddleAboard : BusInside.Place.Aboard, _middleIn ? BusInside.Place.MiddleReader : BusInside.Place.Reader, Then.Tap);
        }

        private void WalkTo(BusInside.Place from, BusInside.Place to, Then then)
        {
            BusInside.Path(from, to, _path);
            StartWalk(then);
        }

        private void StartWalk(Then then)
        {
            _then = then;
            _walked = 0f;
            _length = BusInside.Length(_path);
            _step = Step.Walking;
        }

        private void WalkOn(float delta, float now, BusVehicle bus)
        {
            _walked = Mathf.Min(_length, _walked + _pace * delta);
            _at = BusInside.Along(_path, _walked, out float heading);
            _facing = Mathf.MoveTowardsAngle(_facing, heading, TurnSpeed * delta);
            bool done = _walked >= _length - 0.001f;
            if (bus != null)
            {
                _body.Move(done ? Vector3.zero : bus.Facing(heading) * Vector3.forward * _pace);   // the game's walk, at the pace walked
            }
            if (!done)
            {
                return;
            }
            switch (_then)
            {
                case Then.Tap:
                    TurnTo(_middleIn ? BusInside.MiddleReaderFacing : BusInside.ReaderFacing);
                    break;
                case Then.Seat:
                case Then.Strap:
                    TurnTo(0f);
                    break;
                default:
                    StepDown(now, bus);
                    break;
            }
        }

        private void TurnTo(float facing)
        {
            _turnTo = facing;
            _step = Step.Turning;
        }

        // Turned to where they were going: tap, sit or take the strap.
        private void Arrive(float now)
        {
            switch (_then)
            {
                case Then.Tap:
                    _step = Step.Tapping;
                    if (ClipLibrary.TryGet("pay_tap", out CityClip tap) && _clips.PlayOver(tap.Clip, BodyMask.Upper, 0.15f))
                    {
                        _until = now + tap.Length;
                        TransitDirector.TapSound();
                    }
                    else
                    {
                        _until = now;
                    }
                    break;
                case Then.Seat:
                    if (!Play("sit_bench", 0.2f, ClipEnd.Hold))
                    {
                        _place = BusInside.Place.Strap;
                        WalkFrom(_at, BusInside.Place.Strap);
                        break;
                    }
                    _step = Step.SittingDown;
                    _until = now + Length("sit_bench", 1.6f);
                    break;
                case Then.Strap:
                    if (!Play("strap_hang", 0.3f, ClipEnd.Return))
                    {
                        GetOff(now, "strap_hang would not play");
                        break;
                    }
                    _step = _departing ? Step.Leaving : Step.Holding;
                    _until = now + 0.5f;
                    if (_stage == BusInside.Stage.Aboard)
                    {
                        Logger?.Info("Transit check (aboard): the bus is full: the player stands opposite the middle door holding the strap, "
                                     + (now - _since).ToString("F1", CultureInfo.InvariantCulture)
                                     + " s after setting off for the door: PASS.");
                    }
                    break;
            }
        }

        // Straight from where they stand to a place, when a clip would not play.
        private void WalkFrom(BusInside.Spot at, BusInside.Place to)
        {
            _path.Clear();
            _path.Add(at);
            _path.Add(BusInside.At(to));
            StartWalk(to == BusInside.Place.Strap ? Then.Strap : Then.Seat);
        }

        // The game's walking pace, held to what fits a bus's aisle.
        private static float GamePace()
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            return references != null ? Mathf.Clamp(references.walkingSpeedWalk, 0.8f, 1.4f) : BusInside.Walk;
        }

        private bool Play(string clip, float fade, ClipEnd end)
        {
            return _clips != null && ClipLibrary.TryGet(clip, out CityClip played) && _clips.Play(played.Clip, fade, 1f, end);
        }

        private static float Length(string clip, float otherwise)
        {
            return ClipLibrary.TryGet(clip, out CityClip played) ? played.Length : otherwise;
        }

        private string Line()
        {
            return _from != null && _from.Line >= 0 && _from.Line < TransitMap.Lines.Count ? TransitMap.Lines[_from.Line].Number : "bus";
        }

        private static string Name(TransitStop stop)
        {
            return stop == null ? "?" : "the stop at " + (stop.Name ?? "number " + (stop.Order + 1));
        }
    }
}
