using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// An arrest, the same wherever it happens. The suspect stands with their hands up (the
    /// hands_up and hands_up_hold clips, kept up until the cuffs are on; the gesture that stood
    /// in for them where they cannot be had); the
    /// officer closes in; the suspect is turned away and cuffed (hands behind the back from then
    /// on, walking included: the cuffed_stand and cuffed_walk clips, or CuffPose where they cannot
    /// be had). The cuffing itself is the pair of clips made for it where they can be had: the
    /// officer walks to the place they are measured from, 0.9 m straight behind the suspect and
    /// facing the same way, and the two are started together (arrest_cuffed straight out of the
    /// raised hands and on into cuffed_stand; officer_cuff whole-body, its last pose held until
    /// the suspect is walked off). At their click the officer's handcuffs leave the hand and
    /// CuffPose's pair is on the wrists: one pair is ever seen. Anywhere else (no clips, the
    /// officer could not get to that place, the suspect's hands were never up in the clip) it is
    /// the reach and the cut to the cuffed stance it always was. The officer walks them to the kerb, a pace behind
    /// with a hand on their arm; a patrol car is called and pulls up (ResponseUnit, lights on);
    /// the suspect goes in the back (cuffed_car_enter at the rear door where they could walk to
    /// the place that clip is made for; otherwise gone, with the sound of the door), the officer
    /// gets in, and the car drives away.
    /// Where a patrol car is already standing at the scene with its crew (a traffic stop, a
    /// hit-and-run driver caught: BeginAtCar), nothing is called: the suspect is walked to that
    /// car and put in the back, the officer gets in, and its owner drives it away.
    /// With a car of the sequence's own, a second officer gets out of it at the kerb and stands by
    /// while the suspect is put in, if the pool has one to spare.
    /// If no patrol car can be had (the option is off, no road, no free car) the officer walks
    /// the cuffed suspect away down the street, and neither leaves the street while the camera
    /// is on them. Indoors there is no car: they go out through the shop door.
    /// Owned and ticked by a scene; the two people are the scene's own roles, and the patrol
    /// car is the one all scenes share (CrimeDirector.Patrol). Clear undoes everything.
    /// </summary>
    internal sealed class ArrestSequence
    {
        private const float RepathInterval = 0.4f;
        private const float CarReach = 38f;      // a patrol car that stopped further away than this is not walked to
        private const float CarWait = 32f;
        private const int WalkAwayLegs = 3;
        private const float PlaceWait = 4.5f;     // seconds the officer is given to get behind the suspect, and the suspect to have turned away
        private const float Squared = 0.97f;      // cosine of how far off the line (14 degrees) someone may still be turned when a clip for two starts
        private const float DoorExact = 0.2f;     // metres from where the getting-in clip is made for at which it is still played (as the taxi's passenger)
        private const float DoorTurn = 1.2f;      // seconds given to turn square to the door before it is played anyway

        private enum Step { None, Closing, Placing, Cuffing, ToKerb, AwaitCar, Boarding, OfficerBoards, WalkAway, Done }

        private ActorRole _suspect;
        private ActorRole _officer;
        private Step _step = Step.None;
        private float _stepEnd;
        private float _nextRepath;
        private Vector3 _kerb;
        private Vector3 _exit;       // indoors: the point beyond the shop door
        private bool _indoors;
        private bool _carComing;
        private VehicleComponent _car;   // a patrol car that was already there and is somebody else's to drive away; null for the shared one
        private Vector3 _cellDoor;       // beside that car, where the suspect gets in
        private readonly ActorRole _partner = new ActorRole();   // the second officer of the sequence's own car
        private int _legs;
        private Vector3 _behind;         // where officer_cuff is made for: 0.9 m straight behind the suspect
        private float _click = -1f;      // when the cuffs go on in the pair of clips; negative when that is not being played
        private bool _led;               // the suspect is in arrest_cuffed / cuffed_stand as a stance of the role's
        private bool _officerHolds;      // the officer is held on officer_cuff's last pose
        private bool _doorClip;          // the suspect is walking to where cuffed_car_enter is made for
        private bool _doorRight;         // ...at a door on the car's right-hand side
        private Vector3 _doorStand;
        private Vector3 _doorInwards;
        private float _doorFrom = -1f;   // when they came to stand there
        private float _seated = -1f;     // when the getting-in clip has them in the seat; negative while it is not playing

        public bool Active => _step != Step.None && _step != Step.Done;

        /// <summary>True once it is over, one way or the other: they have driven off, or walked out of sight.</summary>
        public bool Finished => _step == Step.Done;

        /// <summary>True from the moment the cuffs are on: the point of no return, and when a scene should say how it ended.</summary>
        public bool Cuffed { get; private set; }

        /// <summary>True if it is ending in a patrol car and not on foot.</summary>
        public bool ByCar { get; private set; }

        public void Begin(ActorRole suspect, ActorRole officer, float now)
        {
            Start(suspect, officer, now, false, default, null);
        }

        /// <summary>
        /// The same beside a patrol car that is already standing there, parked, with this officer
        /// out of it: into the back of that one. When Finished, its owner drives it away.
        /// </summary>
        public void BeginAtCar(ActorRole suspect, ActorRole officer, VehicleComponent patrol, float now)
        {
            Start(suspect, officer, now, false, default, patrol);
        }

        /// <summary>The same inside a shop: no car, out through the door.</summary>
        public void BeginIndoors(ActorRole suspect, ActorRole officer, Vector3 beyondDoor, float now)
        {
            Start(suspect, officer, now, true, beyondDoor, null);
        }

        private void Start(ActorRole suspect, ActorRole officer, float now, bool indoors, Vector3 exit, VehicleComponent car)
        {
            Clear();
            _suspect = suspect;
            _officer = officer;
            _indoors = indoors;
            _exit = exit;
            _legs = 0;
            _click = -1f;
            _led = false;
            _officerHolds = false;
            _doorClip = false;
            _doorFrom = -1f;
            _seated = -1f;
            Cuffed = false;
            ByCar = false;
            if (!suspect.HandsRaised)
            {
                suspect.Quiet();   // (someone a scene already has standing with their hands up keeps them up: they are not lowered to be raised again)
                suspect.Drop();
            }
            suspect.Stand(officer.Present ? officer.Position : suspect.Position);
            suspect.Surrender();
            _step = Step.Closing;
            _stepEnd = now + 10f;
            _nextRepath = 0f;
            // Sent for at once, so that with luck it is pulling up as the cuffs go on.
            _car = car;
            if (car != null)
            {
                // The back door on the kerb side if there is ground there, else the other; the officer's own door after.
                bool right = StreetSpots.BesideCar(car, StreetSpots.Right, 1f, out _cellDoor);
                bool left = StreetSpots.BesideCar(car, StreetSpots.Left, 1f, out _kerb);
                if (!right && !left)
                {
                    _car = null;   // nowhere to stand beside it: they walk away instead
                }
                else if (!right)
                {
                    _cellDoor = _kerb;
                }
                else if (!left)
                {
                    _kerb = _cellDoor;
                }
                _carComing = false;
                return;
            }
            _carComing = !indoors && Settings.Police && CallCar(suspect.Position);
        }

        public void Tick(float now, Vector3 player)
        {
            if (!Active)
            {
                return;
            }
            _partner.Tick(now);
            // (Once they are being put in the car, the suspect being gone is the arrest going as it should.)
            if (!_suspect.Present && _step != Step.Boarding && _step != Step.OfficerBoards)
            {
                Finish();   // taken from us (run over, the street unloaded): there is nobody to arrest
                return;
            }
            switch (_step)
            {
                case Step.Closing:
                    if (!_officer.Present || now >= _stepEnd || Near(_officer, _suspect, 1.9f))
                    {
                        // Turned to face away, and the cuffs go on.
                        Vector3 away = _suspect.Position * 2f - (_officer.Present ? _officer.Position : player);
                        if (!_suspect.HandsRaised)
                        {
                            _suspect.Quiet();   // the stand-in gesture stops here; hands held up in the clip stay up until the cuffs are on
                        }
                        _suspect.Stand(away);
                        // With the pair of clips made for it the officer first walks to where they are measured from: straight
                        // behind, 0.9 m off. (Only out of hands that are up in the clip: arrest_cuffed begins in that pose.)
                        Vector3 back = _officer.Present ? _officer.Position - _suspect.Position : default;
                        back.y = 0f;
                        if (_suspect.HandsRaised && back.sqrMagnitude > 0.04f && ClipLibrary.Has(SceneClips.ArrestCuffed) && ClipLibrary.Has(SceneClips.CuffedStand))
                        {
                            _behind = _suspect.Position + back.normalized * SceneClips.CuffBehind;
                            _officer.GoExactly(_behind, Pace.Walk, _suspect.Position, PlaceWait - 0.5f);
                            Go(Step.Placing, now, PlaceWait);
                        }
                        else
                        {
                            Reach(now);
                        }
                    }
                    else if (now >= _nextRepath)
                    {
                        _nextRepath = now + RepathInterval;
                        _officer.Go(Crime.Short(_suspect.Position, _officer.Position, 1.2f), Pace.Jog, _suspect.Position, 10f);
                    }
                    break;

                case Step.Placing:
                    if (_officer.Present && now < _stepEnd && !(_officer.Settled && TurnedFrom(_suspect, _behind)))
                    {
                        break;   // still walking round, or the suspect has not turned their back yet
                    }
                    PutCuffsOn(now);
                    break;

                case Step.Cuffing:
                    if (_click >= 0f && now >= _click)
                    {
                        Click();
                    }
                    if (now < _stepEnd)
                    {
                        break;
                    }
                    Click();
                    _suspect.Quiet();   // hands down from beside the head, and behind the back (they are there already after arrest_cuffed)
                    // Hands behind the back in the clips made for it, standing and walking (at the pace the walk was made
                    // for); CuffPose sets the arms by hand whenever no such clip is on them.
                    _suspect.Gait(SceneClips.CuffedWalk, SceneClips.WalkPace, SceneClips.CuffedStand);
                    LetGo();   // the hand comes off the shoulder as they are walked off
                    if (_indoors)
                    {
                        _suspect.WalkOff(_exit, Pace.Walk, 30f);
                        _officer.WalkOff(_exit, Pace.Walk, 32f);
                        Go(Step.WalkAway, now, 34f);
                    }
                    else if (_car != null)
                    {
                        // The patrol car is right there: straight to its back door.
                        ByCar = true;
                        ToDoor(_car, _cellDoor, now);
                    }
                    else if (_carComing && Kerb(out _kerb))
                    {
                        ByCar = true;
                        _suspect.Go(_kerb, Pace.Walk, _kerb, 20f);
                        Go(Step.ToKerb, now, 21f);
                    }
                    else
                    {
                        ReleaseCar();
                        NextLeg(now, player);
                    }
                    break;

                case Step.ToKerb:
                    Escort(now);
                    if (_suspect.Settled || now >= _stepEnd)
                    {
                        Go(Step.AwaitCar, now, CarWait);
                    }
                    break;

                case Step.AwaitCar:
                    Escort(now);
                    AwaitCar(now, player);
                    break;

                case Step.Boarding:
                    Escort(now);
                    GetIn(now);
                    if (now >= _stepEnd)
                    {
                        _suspect.Exit();
                    }
                    break;

                case Step.OfficerBoards:
                    if (!_officer.Present || now >= _stepEnd)
                    {
                        _officer.Exit();
                        _partner.Exit();
                        if (_car == null)
                        {
                            // Away with the lights on; the game takes the car off the road in its own time.
                            ReleaseCar();
                        }
                        Finish();
                    }
                    break;

                case Step.WalkAway:
                    WalkAway(now, player);
                    break;
            }

            // In the car: cuffs off the character before the pool has it back, then the officer goes round to the front.
            if (_step == Step.Boarding && !_suspect.Present)
            {
                _officer.Board(_kerb, Pace.Walk, 8f);
                Go(Step.OfficerBoards, now, 9f);
            }
        }

        /// <summary>Stops at once: cuffs off, the patrol car let go. The two people remain the scene's to release.</summary>
        public void Clear()
        {
            if (_suspect != null)
            {
                Uncuff();

            }
            if (_step != Step.None && _step != Step.Done && _car == null)
            {
                ReleaseCar();
            }
            _partner.Exit();
            _car = null;
            _suspect = null;
            _officer = null;
            _step = Step.None;
            Cuffed = false;
        }

        private void Finish()
        {
            if (_suspect != null)
            {
                Uncuff();

            }
            _step = Step.Done;
        }

        // Cuffs off: the clips first (they walk and stand as the game has them again), then the hand-set arms and the handcuffs.
        private void Uncuff()
        {
            if (Cuffed)
            {
                _suspect.Gait(null);
            }
            if (_led)
            {
                _led = false;
                _suspect.Drop();   // the cuffed stance the role was given with arrest_cuffed
            }
            LetGo();
            _click = -1f;
            _seated = -1f;
            _doorClip = false;
            CuffPose.Remove(_suspect.Character);
        }

        // ---------------------------------------------------------------- the cuffs going on

        // The reach and the cut to the cuffed stance: what an arrest was before the pair of clips, and still is wherever they cannot be played.
        private void Reach(float now)
        {
            _officer.Stand(_suspect.Position);
            _officer.Gesture(CrimeSceneGestures.Reach);
            Go(Step.Cuffing, now, 2.2f);
        }

        // The officer is as near the place behind the suspect as the walk brought them. The pair of clips from exactly
        // there, started in the same tick (they are the same length with the same click); the old reach from anywhere else.
        private void PutCuffsOn(float now)
        {
            Vector3 off = _officer.Present ? _officer.Position - _behind : default;
            off.y = 0f;
            bool placed = _officer.Present && _officer.Settled && off.sqrMagnitude <= SceneClips.CuffSlack * SceneClips.CuffSlack;
            bool turned = TurnedFrom(_suspect, _behind);
            if (!placed || !turned || !ClipLibrary.TryGet(SceneClips.ArrestCuffed, out CityClip cuffing)
                || !_suspect.LeadInto(SceneClips.ArrestCuffed, SceneClips.CuffedStand))
            {
                Crime.Logger?.Info($"Crime: cuffed by the reach, not the pair of clips (officer {off.magnitude:F2} m from the place 0.9 m behind, "
                                   + $"allowed {SceneClips.CuffSlack:F2}; suspect turned away {turned}; clips to be had {ClipLibrary.Has(SceneClips.ArrestCuffed)}).");
                Reach(now);
                return;
            }
            _led = true;
            _officer.PlaceAt(_behind, _suspect.Position);   // the last centimetres, under the cross-fade into the step in
            _officerHolds = _officer.Act(SceneClips.OfficerCuff, ClipEnd.Hold);
            if (!_officerHolds)
            {
                _officer.Gesture(CrimeSceneGestures.Reach);   // (too heavily built for a clip with a tool, or no handcuffs in the prop bundle)
            }
            _click = now + Mathf.Clamp(cuffing.Mark(SceneClips.CuffClick, cuffing.Length * 0.6f), 0.1f, Mathf.Max(0.1f, cuffing.Length));
            Crime.Logger?.Info($"Crime: cuffed in the pair of clips, the officer {off.magnitude:F2} m from the place they are made for"
                               + (_officerHolds ? "." : " (the officer's half could not be had: the reach instead)."));
            Go(Step.Cuffing, now, cuffing.Length);
        }

        // The cuffs are on: the pair on the wrists is shown and the one in the officer's hand is put away, so one pair is ever seen.
        private void Click()
        {
            _click = -1f;
            if (Cuffed)
            {
                return;
            }
            Cuffed = true;
            CuffPose.Put(_suspect.Character);
            if (_officer.Present)
            {
                _officer.StowTool();
            }
        }

        // The officer's hand off the suspect's shoulder: officer_cuff is held on its last pose only while they stand so.
        private void LetGo()
        {
            if (_officerHolds)
            {
                _officerHolds = false;
                if (_officer != null && _officer.Present)
                {
                    _officer.StowTool();
                    _officer.EndAct();
                }
            }
        }

        // True once somebody standing has their back to a point (the officer behind them), near enough for a clip made for two.
        private static bool TurnedFrom(ActorRole who, Vector3 point)
        {
            ThirdPersonCharacter body = who.Character;
            if (body == null)
            {
                return false;
            }
            Vector3 away = who.Position - point;
            away.y = 0f;
            Vector3 facing = body.transform.forward;
            facing.y = 0f;
            return away.sqrMagnitude > 0.0001f && facing.sqrMagnitude > 0.0001f && Vector3.Dot(away.normalized, facing.normalized) >= Squared;
        }

        // ---------------------------------------------------------------- into the back of the car

        // To the back door. With the clip for getting in cuffed, and a way there, they walk to where it is made for
        // (square to the rear door, the feet 0.75 m out from the middle of the seat) and get in in it; otherwise to the
        // ground beside the car as before, and gone with the sound of the door.
        private void ToDoor(VehicleComponent car, Vector3 beside, float now)
        {
            _doorClip = false;
            _doorFrom = -1f;
            _seated = -1f;
            if (car != null && Cuffed)
            {
                Vector3 across = beside - car.transform.position;
                float side = Vector3.Dot(across, car.transform.right) >= 0f ? StreetSpots.Right : StreetSpots.Left;
                string clip = side > 0f ? SceneClips.CuffedCarEnter : SceneClips.CuffedCarEnterLeft;
                if (ClipLibrary.Has(clip) && StreetSpots.AtRearDoor(car, side, out _doorStand, out _doorInwards)
                    && _suspect.RouteLength(_doorStand, out bool complete) >= 0f && complete)
                {
                    _doorClip = true;
                    _doorRight = side > 0f;
                    _suspect.GoExactly(_doorStand, Pace.Walk, _doorStand + _doorInwards * 3f, 24f);
                    Go(Step.Boarding, now, 28f);
                    return;
                }
            }
            _suspect.Board(beside, Pace.Walk, 24f);
            Go(Step.Boarding, now, 25f);
        }

        // At the door: turned square to it, then into the seat in the clip (its last pose held), and only then off the street.
        private void GetIn(float now)
        {
            if (!_doorClip || !_suspect.Present)
            {
                return;
            }
            if (_seated >= 0f)
            {
                if (now >= _seated)
                {
                    _seated = -1f;
                    _doorClip = false;
                    _suspect.Exit();
                }
                return;
            }
            if (!_suspect.Settled)
            {
                return;
            }
            if (_doorFrom < 0f)
            {
                _doorFrom = now;
            }
            Vector3 off = _suspect.Position - _doorStand;
            off.y = 0f;
            bool there = off.sqrMagnitude <= DoorExact * DoorExact;
            if (there && now - _doorFrom < DoorTurn && !TurnedFrom(_suspect, _doorStand - _doorInwards * 3f))
            {
                return;   // still turning to the door
            }
            string clip = _doorRight ? SceneClips.CuffedCarEnter : SceneClips.CuffedCarEnterLeft;
            ActorPool.PlayDoorSound(_suspect.Position);
            if (there && ClipLibrary.TryGet(clip, out CityClip enter) && _suspect.Act(clip, ClipEnd.Hold))
            {
                _seated = now + Mathf.Clamp(enter.Mark("seated", enter.Length), 0.5f, Mathf.Max(0.5f, enter.Length));
                Crime.Logger?.Info($"Crime: the cuffed suspect gets in at the {(_doorRight ? "right" : "left")} rear door, {off.magnitude:F2} m from where the clip is made for.");
                return;
            }
            Crime.Logger?.Info($"Crime: the cuffed suspect is in the car with no getting-in clip (they stopped {off.magnitude:F2} m from the place it is made for, allowed {DoorExact:F2}).");
            _doorClip = false;
            _suspect.Exit();
        }

        private void Go(Step step, float now, float seconds)
        {
            _step = step;
            _stepEnd = now + seconds;
        }

        private static bool Near(ActorRole a, ActorRole b, float metres)
        {
            return (a.Position - b.Position).sqrMagnitude < metres * metres;
        }

        // The officer stays a pace behind the suspect, a hand on their arm.
        private void Escort(float now)
        {
            if (now < _nextRepath || !_officer.Present || !_suspect.Present)
            {
                return;
            }
            _nextRepath = now + RepathInterval;
            if (Near(_officer, _suspect, 1.1f))
            {
                if (_suspect.Settled && !_officer.Settled)
                {
                    _officer.Stand(_suspect.Position);
                }
                return;
            }
            _officer.Go(Crime.Short(_suspect.Position, _officer.Position, 0.8f), Pace.Walk, _suspect.Position, 8f);
        }

        // ---------------------------------------------------------------- the patrol car

        private static bool CallCar(Vector3 scene)
        {
            ResponseUnit car = CrimeDirector.Patrol;
            if (car == null || !TrafficTools.Ready)
            {
                return false;
            }
            if (car.InUse)
            {
                // Already here for this scene (it chased the suspect): close enough is good enough.
                return !car.IsLost && car.Car != null && (car.Car.transform.position - scene).sqrMagnitude < CarReach * CarReach;
            }
            // It pulls up as the accident units do: in its lane, on a plain stretch of road, never in a junction
            // or on a crossing. Its route ends on that road point and it stops there; the officer walks the
            // suspect to the kerb beside it. Only where the scene has no such stretch nearby does it stop
            // wherever it gets within reach.
            Waypoint anchor = TrafficManager.Instance.GetClosestWaypoint(scene, 45f, PlainLane);
            bool plain = anchor != null;
            if (!plain)
            {
                anchor = TrafficManager.Instance.GetClosestWaypoint(scene, 40f);
            }
            if (!car.Spawn(UnitKind.Police, anchor, Random.Range(80f, 130f), 40f))
            {
                return false;
            }
            if (plain)
            {
                car.Driver.BeginRoute(car.Car, car.Path, 30f);
            }
            else
            {
                car.Driver.Begin(car.Car, car.Path, scene, 10f, 30f);
            }
            return true;
        }

        private static readonly System.Predicate<Waypoint> PlainLane = IsPlainLane;

        // One way in and one way on, open, not in a junction, and no crossing over it.
        private static bool IsPlainLane(Waypoint point)
        {
            return !point.temporaryDisabled && !point.IsInIntersection()
                   && point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1
                   && !UnityEngine.AI.NavMesh.SamplePosition(point.position, out _, 0.6f, StreetSpots.Pavement);
        }

        private static void ReleaseCar()
        {
            ResponseUnit car = CrimeDirector.Patrol;
            if (car != null)
            {
                car.Release(TrafficTools.Ready, remove: false, quiet: true);
            }
        }

        // Where they wait: the pavement nearest the road the car is coming along.
        private bool Kerb(out Vector3 kerb)
        {
            ResponseUnit car = CrimeDirector.Patrol;
            Vector3 from = _suspect.Position;
            Vector3 road = from;
            if (car != null && car.InUse && car.Path.Count > 0)
            {
                road = car.Path[car.Path.Count - 1];
            }
            Vector3 towards = Vector3.Lerp(from, road, 0.85f);
            return StreetSpots.OnPavement(towards, 9f, out kerb) || StreetSpots.OnPavement(from, 3f, out kerb);
        }

        private void AwaitCar(float now, Vector3 player)
        {
            ResponseUnit car = CrimeDirector.Patrol;
            if (!car.InUse || car.IsLost)
            {
                ReleaseCar();
                ByCar = false;
                NextLeg(now, player);
                return;
            }
            EmergencyDriver driver = car.Driver;
            if (!car.Parked && !driver.Arrived && driver.Active && now < _stepEnd)
            {
                return;
            }

            // It has pulled up (or got as close as it could). To the kerb beside it, and in.
            car.Park();
            Vector3 at = car.Car.transform.position;
            if ((at - _suspect.Position).sqrMagnitude < CarReach * CarReach && StreetSpots.OnPavement(at, 9f, out Vector3 beside))
            {
                _kerb = beside;
                ToDoor(car.Car, beside, now);
                // The driver of the patrol car gets out at the kerb and stands by while the suspect is put in.
                Vector3 along = car.Car.transform.forward;
                if (StreetSpots.OnPavement(beside + along * 1.8f, 1.5f, out Vector3 watch) && (watch - beside).sqrMagnitude > 1f
                    && _partner.Enter(watch, ActorLook.Officer, 1.5f, onRoad: false))
                {
                    ActorPool.PlayDoorSound(watch);
                    _partner.Stand(_suspect.Position);
                }
            }
            else
            {
                ReleaseCar();
                ByCar = false;
                NextLeg(now, player);
            }
        }

        // ---------------------------------------------------------------- on foot

        // One more stretch of pavement away from the player. They only leave the street where the camera is not on them.
        private void NextLeg(float now, Vector3 player)
        {
            _legs++;
            if (StreetSpots.AwayFrom(_suspect.Position, player, 38f, onRoad: false, out Vector3 corner))
            {
                _suspect.Go(corner, Pace.Walk, corner, 34f);
                Go(Step.WalkAway, now, 36f);
            }
            else
            {
                Go(Step.WalkAway, now, 0f);
            }
        }

        private void WalkAway(float now, Vector3 player)
        {
            if (_indoors)
            {
                if (!_suspect.Present || now >= _stepEnd)
                {
                    _suspect.Exit();
                    _officer.Exit();
                    Finish();
                }
                return;
            }
            Escort(now);
            if (!_suspect.Settled && now < _stepEnd)
            {
                return;
            }
            if (_legs >= WalkAwayLegs || !StreetSpots.IsInView(_suspect.Position))
            {
                _suspect.Exit();
                _officer.Exit();
                Finish();
            }
            else
            {
                NextLeg(now, player);
            }
        }
    }

    /// <summary>The stand-in gestures shared by scenes and the arrest: the game has no hands-up or frisking animation.</summary>
    internal static class CrimeSceneGestures
    {
        public const BigAmbitions.Characters.AnimationType Reach = BigAmbitions.Characters.AnimationType.PressAction;
    }
}
