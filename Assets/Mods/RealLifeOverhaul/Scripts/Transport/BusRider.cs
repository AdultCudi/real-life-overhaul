using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.InputSystem;
using GleyTrafficSystem;
using Helpers;
using UI;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player getting on a bus, riding it and getting off, seen. Built from what the game and the mod already have:
    ///
    /// - TO THE DOOR the game walks the player itself (PlayerController.SetGoal, as a click on the ground does), to where
    ///   somebody stands on the road to step in at the front door (TransitBus.DoorPlace). Any move of the player's own on
    ///   the way is the game's to cancel the walk with, and then nothing has happened. At the door the fare is taken.
    /// - UP THE STEP: the mod's board_step clip, made for this bus's step and rail, from that place. When it ends the
    ///   player is on the bus: from then on they are carried by it, as the game carries the player on a scooter
    ///   (PlayerController.AttachToObject: the navigation agent, the capsule and the physics off, a navigation blocker
    ///   on) but never parented to it, so that the bus going away can never take the player with it. Each frame the
    ///   player is put where they stand on its floor (BusVehicle.OnFloor), leaning with the body on its springs. Their own
    ///   per-frame driver (ThirdPersonCharacter, whose Update would stand the walk still every frame without its agent)
    ///   is paused, and the walk is the game's own: its Move, the same call that walks the player everywhere else.
    /// - INSIDE: a step to the validator by the driver and the card held to it (pay_tap over the standing pose), down the
    ///   aisle to one of the two seats opposite the middle door, a turn, and sit_bench onto the seat and sit_wait in it;
    ///   on a full bus to the strap there instead, and strap_hang. The places are <see cref="BusInside"/>'s, and the bus
    ///   is modelled to them. The doors close, the bus pulls away, and the player is seen riding in it (from above the
    ///   bus's roof is not drawn: BusVehicle.ShowRoof) until it has gone some way (BusInside.FadeNow); then the screen
    ///   fades and the game's time machine runs the ride's minutes as before (<see cref="TransitRide"/>).
    /// - AT THE FAR END, under the black, a bus stands at the stop (TransitBus.PutStanding) with the player in their seat.
    ///   The screen clears, the doors open, they stand up (sit_bench_up), walk to the middle door and down onto the road,
    ///   are given back to the game on the navigation mesh, and the game walks them onto the pavement while the bus
    ///   closes its doors and leaves.
    ///
    /// - THE WAY TO THE DOOR is one the game's navigation can walk (NavMesh.CalculatePath), to a place with nothing standing on
    ///   it: the front door's, else a little along the kerb, and as a last resort the middle door, from whose step they walk up
    ///   the aisle to the validator and pay there (BusInside.BoardTries). Getting off, the place stepped down to is one with a
    ///   way from it onto the pavement, by the nearer door first (BusInside.OffTries).
    /// - THROUGH THE CITY: where the director can drive the line to their stop (TransitDirector.Journey), the doors shutting is the
    ///   start of the ride in real time on the game's activity panel (Step.Riding): they stay in their seat as the bus calls at
    ///   the stops on the way, and get up and off at theirs (GetOffHere), or the rest passes under the fade (SkipRest).
    ///
    /// Until the doors close a move of the player's own gets them off again, the fare given back. Every other way out
    /// (the bus taken off the street, the player knocked over or taken to hospital, moved by something else, the city
    /// going, a fault) ends it at once with the player on the ground beside the bus or at the stop, everything given back
    /// (<see cref="Abort"/>). A save written meanwhile has the player at the stop they boarded at, or at the one they are
    /// getting off at once the ride's time has passed (<see cref="SafeSpot"/>, <see cref="BusInside.SaveAtStart"/>).
    ///
    /// Per frame only while a ride is on: the placing, the walk, the clips' steps and a few tests. Nothing otherwise.
    /// </summary>
    public sealed class BusRider
    {
        private const NavigationBlocker Blocker = NavigationBlocker.Vehicle;   // (not the ride's own Taxi: TransitRide lifts that at the far end)
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
        private TransitBus _bus;            // the bus they are on (the stop's bus, then the one at the far end)
        private TransitStop _from, _to;
        private BusInside.Place _place;     // the seat or the strap
        private bool _held;
        private Transform _camera;          // the bus the camera is kept inside of (ThirdPersonCamera.Confine)
        private bool _blocked;
        private bool _bodyPaused;
        private bool _departing;
        private bool _frontWayOut;
        private bool _gotOff;               // they got off again before the bus left
        private bool _reached;
        private float _toDoor;              // metres to the door when the walk began
        private float _fare;
        private float _until;
        private float _since;
        private float _walked;
        private float _pace = BusInside.Walk;   // the game's own walking pace (GlobalReferences.walkingSpeedWalk), so that its walk plays at its own speed
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
        private float _shutSince = -1f;
        private float _movingSince = -1f;
        private UnityEngine.Events.UnityAction _onReached;
        private bool _middleIn;             // in by the middle door: the front door's place on the road could not be reached
        private TransitStop _passed;        // riding through the city: the last stop the bus called at, or null
        private int _passedCount;
        private bool _offChosen;            // the place to step down to has been chosen and found reachable (WayOut)
        private static int _propsMask = -1;
        private static NavMeshPath _way;

        public BusRider(TransitDirector director)
        {
            _director = director;
        }

        /// <summary>From the walk to the door until they are on the road again at either end.</summary>
        public bool Active => _step != Step.None;

        /// <summary>From the step up (the fare taken) until they are off: the game's events and the other scenes stand aside meanwhile.</summary>
        public bool Aboard => _step != Step.None && _step != Step.ToDoor;

        /// <summary>The bus must stand for them: they are on their way to its door, getting on, sitting down, or getting off again.</summary>
        public bool HoldsBus => _bus != null && (_stage == BusInside.Stage.Walking || _stage == BusInside.Stage.Boarding || (_stage == BusInside.Stage.Aboard && !_departing));

        /// <summary>The bus must stand for them, this one: as <see cref="HoldsBus"/>, and while they get off it at the end of a ride.</summary>
        public bool Holds(TransitBus bus)
        {
            return bus != null && _bus == bus && (HoldsBus || _stage == BusInside.Stage.Arriving);
        }

        /// <summary>Riding a bus through the city in real time (TransitDirector.Journey).</summary>
        public bool RidingThrough => _step == Step.Riding && _stage == BusInside.Stage.Riding;

        /// <summary>The last stop the bus called at on the way, or null.</summary>
        public TransitStop Passed => _passed;

        /// <summary>The front door is theirs: nobody else sets off for it until they are past the validator.</summary>
        public bool AtDoor => _step == Step.ToDoor || _step == Step.Squaring || _step == Step.Stepping || (_step == Step.Walking && _then == Then.Tap);

        /// <summary>Seated or holding on and ready: the bus may close its doors and go (<see cref="Departing"/>).</summary>
        public bool WantsDeparture => !_departing && !_gotOff && _stage == BusInside.Stage.Aboard && ((_step == Step.Seated || _step == Step.Holding) && Time.time >= _until);

        public Step Now => _step;
        public BusInside.Stage Stage => _stage;

        /// <summary>The last boarding came to nothing because the fare could not be paid (the game has said so).</summary>
        public bool RefusedFare { get; private set; }

        /// <summary>The fare at stake: given back if the ride ends before the bus has carried them away.</summary>
        public float Fare => _fare;

        /// <summary>The bus that stands at the far end with them in it, while it does.</summary>
        public TransitBus ArrivalBus => _stage == BusInside.Stage.Arriving ? _bus : null;

        /// <summary>Whether the clips this needs are there, and the bus can be seen into (its modelled body with its inside).</summary>
        public static bool Possible(BusVehicle bus)
        {
            return bus != null && bus.HasInside && ClipLibrary.Ready && ActivityAnimators.Working && ClipLibrary.Has("board_step") && ClipLibrary.Has("sit_bench")
                   && ClipLibrary.Has("sit_wait") && ClipLibrary.Has("sit_bench_up") && ClipLibrary.Has("strap_hang");
        }

        // ---------------------------------------------------------------- getting on

        /// <summary>
        /// Sets the player off for the front door of the bus standing at the stop. False, with why, and nothing changed, if it
        /// cannot be done (the caller then has the ride the old way: a fade at the stop).
        /// </summary>
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
            // Where to walk to: the front door's place if there is a way to it on the walkable ground and nothing stands there,
            // else a little along the kerb, and only then the middle door (BusInside.BoardTries).
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
            player.SetGoal(door, _onReached ?? (_onReached = Reached));
            if (!player.hasOnGoalReachedAction && !_reached)
            {
                Finish("the game would not walk the player to the door", put: false);
                why = "the game would not walk the player to the door";
                return false;
            }
            Logger?.Info("Transit check (boarding): the player walks to the " + (_middleIn ? "middle" : "front") + " door of the " + Line() + " at " + Name(from) + ", " + Mathf.Sqrt(CitySpots.Flat(player.transform.position - door)).ToString("F1")
                         + " m, by the game's own walk, to " + ApproachWords(tried) + "; " + (full ? "the bus is full: they will stand at the strap" : "they will sit on the " + (_place == BusInside.Place.Seat ? "aisle" : "window") + " seat opposite the middle door") + ": PASS.");
            return true;
        }

        // The game's walk has brought them to the door: the fare, the blocker, and the last steps square to the door.
        private void Reached()
        {
            _reached = true;
            if (_step != Step.ToDoor)
            {
                return;   // (a walk the ride has already let go of)
            }
            if (_bus == null || _bus.State != TransitBus.Phase.Standing || _bus.Vehicle == null)
            {
                Finish("the bus had left by the time they reached its door", put: false);
                return;
            }
            if (!TransitState.PayBus(out bool pass))
            {
                Logger?.Info("Transit check (boarding): the player reached the door but cannot pay the fare of " + TransitState.BusFare.ToString("F2") + ": they stay on the pavement: FAIL.");
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

        // ---------------------------------------------------------------- every frame while on

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
            if (RagdollSystem.Holds(_body) || EnergyHelper.goingToHospital || FuneralHelper.PlayerDead || UI.Load.LoadScene.isLoading)
            {
                Abort("the player was knocked over, taken to hospital or the game is loading", put: false);
                return;
            }
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            if (_held && (bus == null || !bus.Out))
            {
                if (_stage == BusInside.Stage.Riding)
                {
                    // Taken off the street on the way (traffic shut down, the city's own clean-up): the rest of the ride under the fade.
                    Logger?.Info("Transit check (journey): the bus was taken off the street with the player riding it: the rest of the ride passes under the fade: PASS.");
                    Release(SafeSpot());
                    _step = Step.Fading;
                    _since = Time.time;
                    _director.JourneySkip("the bus was taken off the street");
                    return;
                }
                if (_stage == BusInside.Stage.Leaving)
                {
                    // Taken off the street as it pulled away (the end of its line out of view, traffic shut down): the ride
                    // goes on all the same, from the stop they boarded at, under the fade.
                    Logger?.Info("Transit check (ride): the bus was taken off the street as it pulled away with the player in it: the ride goes on under the fade from " + Name(_from) + ": PASS.");
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
                Abort("something else moved the player " + Vector3.Distance(_player.transform.position, _lastPut).ToString("F1") + " m", put: false);
                return;
            }
            switch (_step)
            {
                case Step.ToDoor:
                    // (A click elsewhere replaces the game's walk with another: they are then heading away from the door.)
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
                        WalkTo(BusInside.Place.Reader, _place, _place == BusInside.Place.Strap ? Then.Strap : Then.Seat);
                    }
                    break;

                case Step.SittingDown:
                    if (now >= _until)
                    {
                        // sit_bench ends in sit_wait's first pose exactly: no fade.
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
                                         + (now - _since).ToString("F1") + " s after setting off for the door: PASS.");
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
                        // The fade never began (another screen took it): no ride, the fare back, off at the stop (riding through
                        // the city: at the last stop it called at, and the fare only if it has called at none).
                        float fare = BusInside.SaveFareBack(_stage == BusInside.Stage.Riding ? BusInside.Stage.Leaving : _stage, _passedCount) ? _fare : 0f;
                        _fare = 0f;
                        _director.RiderFareBack(fare, "the ride's fade did not begin");
                        Finish("the ride's fade did not begin", put: true);
                        return;
                    }
                    break;

                case Step.Riding:
                    // Through the city in real time (TransitDirector.Journey): a move of the player's own asks to get off at the next stop.
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
                // From over the roof (the game's own view from above) the roof is not drawn: the player is seen in the bus.
                Camera camera = GameManager.GetMainCamera();
                bus.ShowRoof(camera == null || camera.transform.position.y < bus.transform.position.y + 2.6f);
            }
            if (_step != Step.None && _stage == BusInside.Stage.Aboard && !_departing && _step != Step.Seated && _step != Step.Holding && _step != Step.SittingDown && _step != Step.Rising
                && _step != Step.SteppingDown && !_gotOff && WantsOff())
            {
                GetOff(now, "the player moved to get off before the bus left");
            }
        }

        // The last centimetres on the road to where board_step begins, and the turn to face the door. Then up the step.
        private void Square(float delta, float now, BusVehicle bus)
        {
            if (bus == null || !bus.Out || _bus.State != TransitBus.Phase.Standing)
            {
                Abort("the bus left as they reached its door", put: false);
                return;
            }
            // Held from here: the place board_step begins may be a little nearer the bus than the navigation mesh reaches
            // (where the bay's kerb is close), so the last steps are the game's walk given by hand, as on the bus's floor.
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
            _body.Move(Vector3.zero);
            place.rotation = facing;
            if (!Play("board_step", 0.15f, ClipEnd.Hold))
            {
                Abort("board_step would not play", put: false);
                return;
            }
            _step = Step.Stepping;
            _until = now + Length("board_step", 1.4f);
            TransitDirector.DoorSound(place.position);
        }

        // board_step has ended standing on the step: from here on the bus carries them.
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
            Logger?.Info("Transit check (boarding): up the step at the " + (_middleIn ? "middle door (the front one could not be reached): they walk up the aisle to the validator" : "front door")
                         + " with board_step; on the floor " + (_player.transform.position.y - was.y).ToString("F2")
                         + " m over the road (the clip ends " + BusInside.StepUp.ToString("F2") + " up, the floor is " + BusInside.Floor.ToString("F2") + "): PASS.");
            WalkTo(_middleIn ? BusInside.Place.MiddleAboard : BusInside.Place.Aboard, BusInside.Place.Reader, Then.Tap);
        }

        // ---------------------------------------------------------------- walking on the floor

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
                    TurnTo(BusInside.ReaderFacing);
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

        // Turned to where they were going: tap, sit, take the strap.
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
                        Logger?.Info("Transit check (aboard): the bus is full: the player stands opposite the middle door holding the strap, " + (now - _since).ToString("F1")
                                     + " s after setting off for the door: PASS.");
                    }
                    break;
            }
        }

        // From wherever they stand to a place: straight there (the rare way round when a clip would not play).
        private void WalkFrom(BusInside.Spot at, BusInside.Place to)
        {
            _path.Clear();
            _path.Add(at);
            _path.Add(BusInside.At(to));
            StartWalk(to == BusInside.Place.Strap ? Then.Strap : Then.Seat);
        }

        // ---------------------------------------------------------------- getting off

        // A move of the player's own, with no screen in the way.
        private static bool WantsOff()
        {
            return !ThirdPersonCamera.IsUiOpen() && !GameManager.ShouldBlockKeyboardShortcuts() && PlayerAction.Move.Vector().sqrMagnitude > 0.25f;
        }

        // Off again before the bus has left: the fare back, up from the seat if they sit, out by the nearest door.
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

        // Up from the seat if they sit (sit_bench_up), else straight to the way out.
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
            // The place to step down to: one with a way from it onto the pavement, by the nearer door first (BusInside.OffTries).
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
                        Logger?.Info("Transit check (getting off): beside the " + (_frontWayOut ? "front" : "middle") + " door nothing could be stepped down to with a way onto the pavement: they get off "
                                     + (tries[chosen].Middle ? "by the middle door" : "by the front door") + (Mathf.Abs(tries[chosen].Along) > 0.01f ? ", " + Mathf.Abs(tries[chosen].Along).ToString("F1") + " m along the side" : "") + ": PASS.");
                    }
                }
            }
            StartWalk(Then.Off);
        }

        // A place to step down to beside a door: on the walkable ground, nothing standing there, and a way from it to the stop's waiting place.
        private bool OffAt(BusInside.Approach approach, BusVehicle bus, TransitStop stop, out Vector3 road)
        {
            BusInside.Spot at = approach.At;
            if (!Ground(bus.transform.TransformPoint(new Vector3(at.X, 0f, at.Z)), bus, out road) || Furnished(road))
            {
                return false;
            }
            return stop == null || Reachable(road, stop.Wait);
        }

        // At the threshold: down onto the road, walking, to the place beside the door found on the navigation mesh.
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
                _road = SafeSpot();   // (nothing to step down to beside the door: put down at the stop, as the ride always did)
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
            Release(_road);   // (the blocker off first: the game's walk onto the pavement is refused while one is on)
            if (arriving)
            {
                Logger?.Info("Transit check (getting off): the player stands up, walks to the middle door and steps down onto the road at " + Name(_to) + ", "
                             + Mathf.Sqrt(CitySpots.Flat(_road - _to.Wait)).ToString("F1") + " m from the waiting place; the game walks them onto the pavement: PASS.");
            }
            _player.SetGoal(arriving ? _to.Wait : _from.Wait, null);
            _director.RiderOff(_bus, arriving);
            Clear();
        }

        // ---------------------------------------------------------------- the ride

        /// <summary>The director has closed the doors (TransitBus.Depart): no getting off now; the pull-away is watched.</summary>
        public void Departing()
        {
            _departing = true;
            _stage = BusInside.Stage.Leaving;
            _shutSince = -1f;
            _movingSince = -1f;
            if (_step == Step.Seated || _step == Step.Holding)
            {
                _step = Step.Leaving;
            }
            // (Still on their way to it, they sit down or take the strap as the bus moves off, and the watch begins then.)
        }

        private void Leave(float now, BusVehicle bus)
        {
            if (bus == null)
            {
                return;
            }
            if (_shutSince < 0f)
            {
                if (bus.DoorsShut)
                {
                    _shutSince = now;
                    _shutAt = bus.transform.position;
                    // Seated or holding on, the doors shut: the ride through the city in real time on the game's panel, where
                    // the line can be driven to their stop; else, as before, the bus is seen pulling away and the time passes.
                    if (_director.JourneyBegins(_bus, _from, _to))
                    {
                        _step = Step.Riding;
                        _stage = BusInside.Stage.Riding;
                        _since = now;
                    }
                }
                return;
            }
            float metres = Mathf.Sqrt(CitySpots.Flat(bus.transform.position - _shutAt));
            if (_movingSince < 0f && metres > 0.3f)
            {
                _movingSince = now;
            }
            if (BusInside.FadeNow(now - _shutSince, _movingSince < 0f ? 0f : now - _movingSince, metres))
            {
                Logger?.Info("Transit check (ride): the doors shut and the bus pulls away with the player in it: " + metres.ToString("F0") + " m in " + (now - _shutSince).ToString("F1")
                             + " s are seen before the time of the ride passes: " + (metres >= 5f ? "PASS" : "FAIL (it was held where it stood)") + ".");
                _step = Step.Fading;
                _since = now;
                _director.RiderRides(_from, _to);
            }
        }

        /// <summary>Riding through the city: the bus has called at a stop on the way and left it. A save now has them there.</summary>
        public void PassedStop(TransitStop stop)
        {
            if (_stage == BusInside.Stage.Riding && stop != null)
            {
                _passed = stop;
                _passedCount++;
            }
        }

        /// <summary>
        /// Riding through the city: the bus stands at the stop they get off at with its doors open. They get up and off by the
        /// middle door, as at the end of a ride under the fade. False if they are not riding it.
        /// </summary>
        public bool GetOffHere(TransitBus bus, TransitStop stop)
        {
            if (!RidingThrough || bus == null || bus != _bus || stop == null || bus.Vehicle == null)
            {
                return false;
            }
            _to = stop;
            _stage = BusInside.Stage.Arriving;
            _step = Step.Arrived;
            _since = Time.time;
            _until = float.MaxValue;
            _fare = 0f;
            return true;
        }

        /// <summary>Riding through the city: the rest of the ride passes under the fade (TransitRide), from here.</summary>
        public void SkipRest()
        {
            if (_step == Step.Riding)
            {
                _step = Step.Fading;
                _since = Time.time;
            }
        }

        /// <summary>TransitRide: the screen is black. They are taken off the moving bus and put at the stop they left, out of the world.</summary>
        public void AtBlack()
        {
            if (_step != Step.Fading && _step != Step.Leaving && _step != Step.Riding)
            {
                return;
            }
            Release(SafeSpot());
            _step = Step.Away;
            _stage = BusInside.Stage.Away;
        }

        /// <summary>
        /// TransitRide, at the far end with the screen still black and the player in the world again: a bus standing at the stop
        /// with them in their seat, or on the strap. False (and nothing changed) if no bus can stand there now: they are put down
        /// on the pavement as before.
        /// </summary>
        public bool ArriveAboard(TransitBus bus, TransitStop to)
        {
            var game = InstanceBehavior<GameManager>.Instance;
            PlayerController player = game != null ? game.playerController : null;
            ThirdPersonCharacter body = player != null ? player.Character : null;
            if (bus == null || to == null || body == null || body.animator == null || (_step != Step.Away && _step != Step.None))
            {
                Clear();
                return false;
            }
            if (_step == Step.None)
            {
                _place = BusInside.Place.Seat;   // (the test button's ride: nobody boarded, so nobody chose)
                _from = null;
            }
            if (!bus.PutStanding(to, out string refusal))
            {
                Logger?.Info("Transit check (arrival aboard): no bus can stand at " + Name(to) + " now (" + refusal + "): the player is put down on the pavement under the fade: FAIL.");
                Clear();
                return false;
            }
            BusVehicle vehicle = bus.Vehicle;
            bus.MiddleDoorPlace(out Vector3 door, out _);
            ActivityAnimator clips = ActivityAnimators.For(body.animator);
            if (!Possible(vehicle) || clips == null || !clips.Usable || !Ground(door, vehicle, out _))
            {
                Logger?.Info("Transit check (arrival aboard): the bus stands at " + Name(to) + " but " + (!Possible(vehicle) ? "cannot be seen into or the clips are not there" : "nothing is free beside its middle door to step down to")
                             + ": the player is put down on the pavement under the fade: FAIL.");
                bus.Release(remove: true);
                Clear();
                return false;
            }
            _player = player;
            _body = body;
            _clips = clips;
            _pace = GamePace();
            _bus = bus;
            _to = to;
            if (_from == null)
            {
                _from = to;   // (the test button's ride: nobody boarded)
            }
            _fare = 0f;
            _gotOff = false;
            _departing = false;
            _stage = BusInside.Stage.Arriving;
            Hold();
            if (_blocked == false)
            {
                _player.SetNavigationBlocker(Blocker);
                _blocked = true;
            }
            bool standing = _place == BusInside.Place.Strap;
            _at = BusInside.At(_place);
            _facing = 0f;
            _up = 0f;
            Put(vehicle);
            if (!Play(standing ? "strap_hang" : "sit_wait", 0f, ClipEnd.Return))
            {
                Release(SafeSpot());
                bus.Release(remove: true);
                Clear();
                return false;
            }
            _clips.Evaluate(0f);
            _step = Step.Arrived;
            _since = Time.time;
            _until = float.MaxValue;
            return true;
        }

        // The screen clears: the doors open, and they get up and off.
        private void ArrivedFrame(float now, BusVehicle bus)
        {
            if (UiFader.isFading)
            {
                return;
            }
            if (_until == float.MaxValue)
            {
                bus.Doors(true);
                _until = now + 0.9f;   // (they stand as the doors finish opening)
                return;
            }
            if (now < _until)
            {
                return;
            }
            if (_place == BusInside.Place.Strap)
            {
                _clips.Stop(0.3f);
                _step = Step.Holding;
                WayOut(now);
                return;
            }
            _step = Step.Seated;
            Up(now);
        }

        // ---------------------------------------------------------------- holding and letting go of the player

        // On the bus: carried by it from now on, as the game carries the player on a scooter, but not parented to it.
        private void Hold()
        {
            if (_held)
            {
                return;
            }
            NavMeshAgent agent = _body.navmeshAgent;
            _player.ResetNavigation();
            if (agent != null)
            {
                agent.enabled = false;
            }
            _body.isKinematic = true;
            if (_body.characterRigidbody != null)
            {
                _body.characterRigidbody.isKinematic = true;
            }
            if (_body.capsuleCollider != null)
            {
                _body.capsuleCollider.enabled = false;
                if (TrafficManager.HasInstance && TrafficManager.IsInitialized)
                {
                    Manager.TriggerColliderRemovedEvent(_body.capsuleCollider);
                }
            }
            _body.enabled = false;      // its Update would stand the walk still every frame: the walk is ours to give it (Move)
            _bodyPaused = true;
            _held = true;
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            if (bus != null)
            {
                // The third-person camera keeps to the inside of the bus, its sides and ends a little in from the walls.
                _camera = bus.Shell;
                ThirdPersonCamera.Confine(_camera, new Vector3(0f, BusInside.Floor + 1f, 0f), new Vector3(BusInside.Wall - 0.1f, 1f, 5.15f));
            }
        }

        // Back on the ground at a point of the navigation mesh, everything of theirs as it was.
        private void Unhold(Vector3 ground)
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            if (_camera != null)
            {
                ThirdPersonCamera.Unconfine(_camera);
                _camera = null;
            }
            try
            {
                _body.Move(Vector3.zero);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (_bodyPaused)
            {
                _body.enabled = true;
                _bodyPaused = false;
            }
            _player.transform.position = ground;
            NavMeshAgent agent = _body.navmeshAgent;
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.Warp(ground);
                }
            }
            if (_body.capsuleCollider != null)
            {
                _body.capsuleCollider.enabled = true;
            }
            if (_body.characterRigidbody != null)
            {
                _body.characterRigidbody.isKinematic = false;
            }
            _body.isKinematic = false;
            _body.Reset();
            if (_bus != null && _bus.Vehicle != null)
            {
                _bus.Vehicle.ShowRoof(true);
            }
        }

        // Where they stand on the floor, now.
        private void Put(BusVehicle bus)
        {
            Vector3 at = bus.OnFloor(_at, _up);
            _player.transform.SetPositionAndRotation(at, bus.Facing(_facing));
            _lastPut = at;
        }

        // A point on the navigation mesh at a place by the bus, clear of every vehicle but the bus itself.
        private static bool Ground(Vector3 want, BusVehicle bus, out Vector3 ground)
        {
            ground = want;
            if (!NavMesh.SamplePosition(want, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || CitySpots.Flat(hit.position - want) > 0.45f * 0.45f)
            {
                return false;
            }
            ground = hit.position;
            return JobSpots.Clear(ground, bus != null ? bus.transform : null);
        }

        /// <summary>
        /// Where the player is to be if a save is written now, or if the ride ends here other than by getting off: the stop
        /// they boarded at until the time of the ride has passed, the one they get off at after (BusInside.SaveAtStart).
        /// </summary>
        public Vector3 SafeSpot()
        {
            TransitStop stop = _stage == BusInside.Stage.Arriving ? _to : _passed ?? _from;
            Vector3 want = stop != null ? stop.Wait : (_player != null ? _player.transform.position : Vector3.zero);
            return NavMesh.SamplePosition(want, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : want;
        }

        /// <summary>For a save: the player's place in it, and a fare to give back when it is loaded (0 for none).</summary>
        public bool ForSave(out Vector3 place, out float fareBack)
        {
            place = Vector3.zero;
            fareBack = 0f;
            if (!Aboard)
            {
                return false;
            }
            place = SafeSpot();
            fareBack = BusInside.SaveFareBack(_stage, _passedCount) ? _fare : 0f;
            return true;
        }

        // ---------------------------------------------------------------- endings

        /// <summary>The walk to the door stopped short (the bus left, or the player went another way): nothing was paid.</summary>
        public void Cancel(string why)
        {
            if (_step == Step.ToDoor)
            {
                Finish(why, put: false);
            }
        }

        /// <summary>Everything undone at once, whatever the step: the player on the ground (with <paramref name="put"/>, beside the bus or at the stop), the fare back if the bus had not yet carried them away. Never throws.</summary>
        public void Abort(string why, bool put = true)
        {
            if (_step == Step.None)
            {
                return;
            }
            if (_step == Step.Away)
            {
                return;   // (the ride's own routine has the player while its time passes, and gives them back; the director clears this when it ends)
            }
            Logger?.Info("Transit check (ride ended early): " + why + " (" + _step + "): the player is given back on the ground" + (_fare > 0f && BusInside.FareBackOnCancel(_stage) ? ", the fare with them" : "") + ".");
            GiveBack(why);
            Finish(why, put);
        }

        private void GiveBack(string why)
        {
            if (_fare > 0f && BusInside.FareBackOnCancel(_stage))
            {
                float fare = _fare;
                _fare = 0f;
                _director.RiderFareBack(fare, why);
            }
        }

        // Ends it: the player let go of (on the ground beside the bus's door if there is any, else at the stop), the blocker off, the clips handed back.
        private void Finish(string why, bool put)
        {
            try
            {
                // (A walk to the door the game is still making goes on; at its end nothing boards: Reached lets it go.)
                if (_held)
                {
                    // (Riding through the city the bus may be anywhere on its way, in the traffic: at the last stop it called at.)
                    Release(_stage == BusInside.Stage.Riding ? SafeSpot() : put ? Beside() : _player != null ? Nearest(_player.transform.position) : Vector3.zero);
                }
                if (_clips != null && _clips.Active)
                {
                    _clips.Stop(0.2f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Clear();
        }

        // Taken off the bus and put on the ground: the clips handed back, everything of theirs as it was.
        private void Release(Vector3 ground)
        {
            if (_clips != null && _clips.Active)
            {
                _clips.Dispose();
            }
            Unhold(ground);
            if (_blocked && _player != null)
            {
                _player.UnsetNavigationBlocker(Blocker);
                _blocked = false;
            }
        }

        // Beside the bus's door on the road if there is ground there, else at the stop.
        private Vector3 Beside()
        {
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            if (bus != null && bus.Out)
            {
                BusInside.Spot to = _at.Z > 3.7f ? BusInside.FrontExitTo : BusInside.ExitTo;
                if (Ground(bus.transform.TransformPoint(new Vector3(to.X, 0f, to.Z)), bus, out Vector3 ground))
                {
                    return ground;
                }
            }
            return SafeSpot();
        }

        private static Vector3 Nearest(Vector3 at)
        {
            return NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : at;
        }

        private void Clear()
        {
            try
            {
                if (_held)
                {
                    Unhold(SafeSpot());
                }
                if (_blocked && _player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _blocked = false;
            _held = false;
            _step = Step.None;
            _stage = BusInside.Stage.None;
            _bus = null;
            _departing = false;
            _gotOff = false;
            _fare = 0f;
            _middleIn = false;
            _offChosen = false;
            _passed = null;
            _passedCount = 0;
            _path.Clear();
            _director.RiderCleared();
        }

        /// <summary>Called once the time of the ride has passed and nothing stood at the far end for them: the ride is over.</summary>
        public void Done()
        {
            if (_step == Step.Away)
            {
                Clear();
            }
        }

        // ---------------------------------------------------------------- reaching the doors

        // The place to walk to, to get on: the first of BusInside.BoardTries (the director's place at the front door first)
        // that is on the walkable ground, has nothing standing on it and can be walked to from where the player is, by the
        // game's own navigation. Returns which was taken (0 for the director's own place), or -1 for none.
        private int ChooseApproach(BusVehicle bus, Vector3 from, Vector3 door, out Vector3 at, out bool middle)
        {
            at = door;
            middle = false;
            if (bus == null)
            {
                return -1;
            }
            Vector3 start = Nearest(from);
            if (!Furnished(door) && Reachable(start, door))
            {
                return 0;
            }
            Vector3 found = door;
            int chosen = BusInside.First(BusInside.BoardTries, a =>
            {
                BusInside.Spot spot = a.At;
                return Ground(bus.transform.TransformPoint(new Vector3(spot.X, 0f, spot.Z)), bus, out found) && !Furnished(found) && Reachable(start, found);
            });
            if (chosen < 0)
            {
                return -1;
            }
            at = found;
            middle = BusInside.BoardTries[chosen].Middle;
            return chosen + 1;
        }

        private static string ApproachWords(int tried)
        {
            if (tried <= 0)
            {
                return "the front door's own place";
            }
            BusInside.Approach a = BusInside.BoardTries[tried - 1];
            return (a.Middle ? "the middle door (the front door's place could not be walked to)" : "the front door")
                   + (Mathf.Abs(a.Along) > 0.01f ? ", " + Mathf.Abs(a.Along).ToString("F1") + " m along the kerb" + (a.Middle ? "" : " (its own place could not be walked to)") : "");
        }

        // A way on the walkable ground from one point to the other, all the way, by the game's own navigation mesh.
        private static bool Reachable(Vector3 from, Vector3 to)
        {
            if (_way == null)
            {
                _way = new NavMeshPath();
            }
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _way) && _way.status == NavMeshPathStatus.PathComplete;
        }

        // Street furniture standing where a person would: a pole, a bin, a bench.
        private static bool Furnished(Vector3 at)
        {
            if (_propsMask < 0)
            {
                _propsMask = LayerMask.GetMask("StreetProps");
            }
            return _propsMask != 0 && Physics.CheckCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.6f, 0.22f, _propsMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- small things

        // The game's own walking pace, held to what fits a bus's aisle.
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
