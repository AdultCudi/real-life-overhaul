using BigAmbitions.Characters;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One person's part in a scene: a driver, a paramedic, a suspect. It holds a character
    /// from the pool for as long as the part lasts and carries out one order at a time (go
    /// there and stand, get into that vehicle, walk off), so that scenes read as a timetable
    /// instead of a pile of arrival checks.
    /// Every call is safe when there is nobody behind the role: the pool was full, the person
    /// has already left, or the street was unloaded. Scenes never need to check first.
    /// The owning scene calls Tick a few times a second; nothing here runs by itself.
    /// </summary>
    public sealed class ActorRole
    {
        private const float SettleDelay = 0.3f;   // a route asked for this recently has not been worked out yet
        private const float Exact = 0.05f;        // metres from the end of the route at which someone who must be at a measured distance has arrived
        private const float ExactWait = 1f;       // ...and the seconds they are given to close that last step before they are taken as there

        private enum Order { None, Standing, Going, Boarding, Leaving, Led }

        private StreetActor _actor;
        private int _lease;
        private bool _onRoad;
        private Order _order;
        private float _orderFrom;
        private float _orderUntil;
        private Vector3 _face;
        private bool _hasFace;
        private Vector3 _lastPosition;

        private bool _hasPose;              // wanted while standing; put on at arrival
        private bool _kneels;               // ...and it is kneeling, not one of the game's animations
        private string _stance;             // ...in this clip of the mod's own, if it can be had (the pose below, or the hand-set kneel, if not)
        private bool _fallback;             // ...and there is a pose of the game's to fall back on
        private float _reach;               // a kneeling clip made for something this far in front: worn only at that distance from what they face
        private float _closeSince = -1f;
        private PermanentAnimationType _pose;
        private string _prop;
        private bool _posed;
        private bool _surrenders;           // the stance is hands up: raised (one clip) and kept up (another); Quiet ends it
        private bool _clipWorn;             // ...and what is worn is a clip of the mod's own, not a pose or a gesture of the game's

        private bool _repeats;
        private string _repeatClip;         // ...a clip of the mod's own that plays once, where it can be had; the gesture below where not
        private AnimationType _gesture;
        private float _gestureMin;
        private float _gestureMax;
        private float _nextGesture;
        private bool _hasEmoji;
        private CharacterEmojiName _emoji;
        private float _emojiChance;

        /// <summary>True while there is a person on the street playing this part.</summary>
        public bool Present => _actor != null && _actor.InUse && _actor.Lease == _lease && _actor.IsValid;

        /// <summary>True once the person is standing where they were last sent (or were placed).</summary>
        public bool Settled => _order == Order.Standing && Present;

        /// <summary>Where the person is, or was when last seen.</summary>
        public Vector3 Position
        {
            get
            {
                if (Present)
                {
                    _lastPosition = _actor.Position;
                }
                return _lastPosition;
            }
        }

        /// <summary>Puts someone on the street near a point, free to walk. False if nobody could be had.</summary>
        public bool Enter(Vector3 near, ActorLook look, float radius, bool onRoad)
        {
            return Enter(near, look, radius, onRoad, presence: false);
        }

        /// <summary>
        /// The same for someone who is only there to be seen (an officer on the beat or by a parked
        /// patrol car). The pool hands out few of these, and takes one back at once, without
        /// warning, when a scene needs a person: this role then simply finds nobody behind it.
        /// </summary>
        public bool EnterPresence(Vector3 near, ActorLook look, float radius, bool onRoad)
        {
            return Enter(near, look, radius, onRoad, presence: true);
        }

        private bool Enter(Vector3 near, ActorLook look, float radius, bool onRoad, bool presence)
        {
            Exit();
            ActorPool pool = ActorPool.Instance;
            StreetActor actor = pool == null ? null : presence ? pool.SpawnPresence(near, look, radius, onRoad) : pool.Spawn(near, look, radius, onRoad);
            if (actor == null)
            {
                return false;
            }
            _actor = actor;
            _lease = actor.Lease;
            _onRoad = onRoad;
            _lastPosition = actor.Position;
            _order = Order.Going;        // free on the navigation mesh until told to stand
            _orderFrom = Time.time;
            _orderUntil = Time.time;     // ...which the next tick does if no order comes first
            return true;
        }

        /// <summary>
        /// Takes over someone who is already on the street, whatever part they were playing: the
        /// role that had them finds them gone, as if they had left. Used when a person in one
        /// scene becomes the casualty of another. They are given no order.
        /// </summary>
        public bool Adopt(StreetActor actor)
        {
            Exit();
            if (actor == null || !actor.InUse || !actor.IsValid)
            {
                return false;
            }
            actor.Lease++;
            _actor = actor;
            _lease = actor.Lease;
            _onRoad = true;
            _lastPosition = actor.Position;
            return true;
        }

        /// <summary>The character behind the role, for the few things a role cannot do for it (being thrown down). Null if nobody.</summary>
        public ThirdPersonCharacter Character => Present ? _actor.Character : null;

        /// <summary>For someone about to be thrown down: orders, pose and prop dropped, and shown whether in view or not.</summary>
        public void GoLimp()
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _actor.ClearPose();
            _actor.EndClips();   // physics and a playing clip must not share bones
            _actor.Halt();
            _actor.SetVisible(true);
            _order = Order.None;
            _hasFace = false;
            _repeats = false;
        }

        /// <summary>Back on their feet at a point on the road, standing. False if they could not be stood there.</summary>
        public bool Rise(Vector3 at)
        {
            if (!Present || _actor.Limp)
            {
                return false;
            }
            _onRoad = true;
            if (!_actor.Place(at, onRoad: true))
            {
                return false;
            }
            _lastPosition = at;
            Settle(Time.time);
            return true;
        }

        /// <summary>
        /// For someone lying down who is about to be picked up: every order is forgotten and
        /// nothing turns them any more, but they keep the pose they are in. Whoever carries
        /// them moves the character from here on. Null if there is nobody.
        /// </summary>
        public ThirdPersonCharacter Lifted()
        {
            if (!Present || _actor.Limp)
            {
                return null;
            }
            _actor.Halt();
            _actor.Character.LookTarget = Vector3.zero;
            _order = Order.None;
            _hasFace = false;
            _repeats = false;
            return _actor.Character;
        }

        /// <summary>Plays a gesture once, now.</summary>
        public void Gesture(AnimationType gesture)
        {
            if (Present && !_actor.Limp)
            {
                _actor.Gesture(gesture);
            }
        }

        /// <summary>Plays the first of two gestures these characters have; the second is one they are known to.</summary>
        public void Gesture(AnimationType preferred, AnimationType otherwise)
        {
            if (Present && !_actor.Limp)
            {
                _actor.Gesture(_actor.Knows(preferred) ? preferred : otherwise);
            }
        }

        /// <summary>A paramedic takes the medic bag along: in the hand while walking, on the ground beside them while kneeling.</summary>
        public void CarryBag(bool carry)
        {
            if (Present)
            {
                _actor.CarryBag(carry);
            }
        }

        /// <summary>An officer's shirt turned hi-vis yellow for traffic duty, or back. It goes back by itself when they leave the street.</summary>
        public void HiVis(bool on)
        {
            if (Present)
            {
                _actor.HiVis(on);
            }
        }

        /// <summary>True if the person can be seen (the pool switches off whoever is far away or behind the camera). False if nobody.</summary>
        public bool Visible => Present && _actor.Visible;

        /// <summary>Takes the person off the street at once and forgets every order.</summary>
        public void Exit()
        {
            if (Present)
            {
                _lastPosition = _actor.Position;
                ActorPool pool = ActorPool.Instance;
                if (pool != null)
                {
                    pool.Release(_actor);
                }
            }
            _actor = null;
            _order = Order.None;
            _hasFace = false;
            _hasPose = false;
            _posed = false;
            _repeats = false;
        }

        /// <summary>Stops where they are and turns to face a point.</summary>
        public void Stand(Vector3 lookAt)
        {
            if (!Present)
            {
                return;
            }
            _face = lookAt;
            _hasFace = true;
            Settle(Time.time);
        }

        /// <summary>Turns to face a point: now if standing, on arrival if on the way somewhere.</summary>
        public void Face(Vector3 lookAt)
        {
            if (!Present)
            {
                return;
            }
            _face = lookAt;
            _hasFace = true;
            if (_order == Order.Standing)
            {
                _actor.Face(lookAt);
            }
        }

        /// <summary>Goes to a point and stands there facing another. Stands where they are if it takes longer than allowed.</summary>
        public void Go(Vector3 destination, Pace pace, Vector3 thenFace, float allow = 30f)
        {
            if (!Present)
            {
                return;
            }
            _face = thenFace;
            _hasFace = true;
            if (!Move(destination, pace, Order.Going, allow))
            {
                Settle(Time.time);
            }
        }

        /// <summary>
        /// The same by way of another place first: round the head or the feet of someone lying there, say, where the
        /// straight line would go over them. The time allowed is for both stretches together.
        /// </summary>
        public void GoVia(Vector3 via, Vector3 destination, Pace pace, Vector3 thenFace, float allow = 30f)
        {
            if (!Present)
            {
                return;
            }
            _face = thenFace;
            _hasFace = true;
            if (Move(via, pace, Order.Going, allow))
            {
                _hasThen = true;
                _then = destination;
                _thenPace = pace;
            }
            else if (!Move(destination, pace, Order.Going, allow))
            {
                Settle(Time.time);
            }
        }

        /// <summary>
        /// Goes to a point that a clip for two is measured from (0.9 m behind whoever is cuffed; the place a getting-in
        /// clip is made for) and stands there facing another: not taken as arrived a step short of it, but let come to
        /// rest (for a moment at most), as someone sent to kneel a measured distance from a chest is.
        /// </summary>
        public void GoExactly(Vector3 destination, Pace pace, Vector3 thenFace, float allow = 10f)
        {
            Go(destination, pace, thenFace, allow);
            _exact = _order == Order.Going;
        }

        /// <summary>
        /// Stood exactly at a place, facing a point, there and then: only for the last few centimetres a route left
        /// (the caller measures them), never as a way of getting anywhere. False if they are not standing.
        /// </summary>
        public bool PlaceAt(Vector3 at, Vector3 lookAt)
        {
            if (!Present || _order != Order.Standing || _actor.Limp)
            {
                return false;
            }
            _face = lookAt;
            _hasFace = true;
            _actor.StandAt(at, lookAt);
            return true;
        }

        private bool _exact;        // on the way to a place a clip is measured from: arrived only when they are at it
        private bool _hasThen;      // on the way to a place they only pass: at it, they go on to...
        private Vector3 _then;      // ...this one
        private Pace _thenPace;

        private static NavMeshPath _route;

        /// <summary>
        /// How long the way to a place is as this person's own navigation agent would walk it now, in metres, and
        /// whether it gets there at all. For a check and its log line when a crew sets off (it works the route out
        /// there and then): never on a timer. -1 if it cannot be asked.
        /// </summary>
        public float RouteLength(Vector3 to, out bool complete)
        {
            complete = false;
            NavMeshAgent agent = Present && !_actor.Limp ? _actor.Character.navmeshAgent : null;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return -1f;
            }
            if (_route == null)
            {
                _route = new NavMeshPath();
            }
            if (!agent.CalculatePath(to, _route))
            {
                return -1f;
            }
            complete = _route.status == NavMeshPathStatus.PathComplete;
            Vector3[] corners = _route.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            return length;
        }

        /// <summary>Walks to a vehicle's door and gets in: gone, with the sound of the door. Gone anyway once the time allowed is up.</summary>
        public void Board(Vector3 door, Pace pace, float allow = 20f)
        {
            if (!Present)
            {
                return;
            }
            if (!Move(door, pace, Order.Boarding, allow))
            {
                ActorPool.PlayDoorSound(_actor.Position);
                Exit();
            }
        }

        /// <summary>Walks away and leaves the street on arrival, or sooner if nobody can see them go.</summary>
        public void WalkOff(Vector3 destination, Pace pace, float allow = 25f)
        {
            if (!Present)
            {
                return;
            }
            if (!Move(destination, pace, Order.Leaving, allow))
            {
                Exit();
            }
        }

        /// <summary>Walks off in the direction away from a point (the other party, the player).</summary>
        public void WalkAwayFrom(Vector3 avoid, Pace pace, float distance = 30f, float allow = 25f)
        {
            if (!Present)
            {
                return;
            }
            if (StreetSpots.AwayFrom(_actor.Position, avoid, distance, _onRoad, out Vector3 spot))
            {
                WalkOff(spot, pace, allow);
            }
            else
            {
                Exit();
            }
        }

        /// <summary>A looping pose (with an optional prop in the hand) worn whenever the person is standing.</summary>
        public void Hold(PermanentAnimationType pose, string prop = null)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = false;
            _stance = null;
            _fallback = true;
            _pose = pose;
            _prop = prop;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>
        /// One of the mod's clips, played whenever the person is standing (directing traffic, say).
        /// If it cannot be had they simply stand.
        /// </summary>
        public void Work(string clip)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = false;
            _stance = clip;
            _fallback = false;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>The same, with the pose of the game's (and its prop) that stood in for it as the fallback.</summary>
        public void Work(string clip, PermanentAnimationType otherwise, string prop = null)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = false;
            _stance = clip;
            _fallback = true;
            _pose = otherwise;
            _prop = prop;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>
        /// Down beside someone on the ground whenever the person is standing: in a kneeling clip of
        /// the mod's own, or on one knee in the pose set by hand if that cannot be had.
        /// </summary>
        /// <param name="reach">
        /// For a clip whose hands work on something a measured distance in front (CPR): they walk
        /// right up to where they were sent, and the clip is worn only if what they face is then
        /// that far away; otherwise they kneel at work beside it.
        /// </param>
        public void Kneel(string clip = SceneClips.Kneel, float reach = 0f)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = true;
            _stance = clip;
            _reach = reach;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>
        /// Hands up, whenever the person is standing, until Quiet (or the next pose): raised in one clip of the mod's
        /// own and kept up in another. Where those cannot be had it is the gesture that stood in for it, again and
        /// again (the game has no hands-up animation); under a clip of ours that gesture is never played.
        /// </summary>
        public void Surrender()
        {
            if (!Present)
            {
                return;
            }
            if (!_surrenders || !_hasPose)
            {
                Drop();
                _hasPose = true;
                _surrenders = true;
                _kneels = false;
                _stance = SceneClips.HandsUp;
                _fallback = false;
            }
            Repeat(SurrenderGesture, 2.4f, 3.2f);
            if (_order == Order.Standing && !_posed)
            {
                Wear();
            }
        }

        /// <summary>True while their hands are up in the clips made for it (not in the gesture that stands in for them).</summary>
        public bool HandsRaised => _surrenders && _posed && _clipWorn && Present;

        private const AnimationType SurrenderGesture = AnimationType.VictoryStanding;

        /// <summary>Plays one of the mod's clips once, now, where they stand. False if it cannot be had.</summary>
        public bool Act(string clip)
        {
            return Present && !_actor.Limp && _actor.Work(clip);
        }

        /// <summary>
        /// The same, saying how it ends: ClipEnd.Hold keeps the last pose (officer_cuff ends stepped in with a hand on
        /// a shoulder; a getting-in clip ends in the seat) until EndAct, the next order to go somewhere, or Exit.
        /// </summary>
        public bool Act(string clip, ClipEnd end)
        {
            return Present && !_actor.Limp && _actor.Work(clip, end);
        }

        /// <summary>Lets go of a clip held on its last pose. Nothing if none is.</summary>
        public void EndAct()
        {
            if (Present && !_actor.Limp)
            {
                _actor.ReleaseClip();
            }
        }

        /// <summary>The tool of the clip being played out of the hand while the clip goes on (the officer's handcuffs, from the click).</summary>
        public void StowTool()
        {
            if (Present)
            {
                _actor.StowTool();
            }
        }

        /// <summary>
        /// The stance they stand in changes through a clip that plays once into a loop (arrest_cuffed, then
        /// cuffed_stand), straight from the clip they are in: the loop is what they then wear whenever they stand,
        /// until Drop or the next pose. False, with nothing changed, if either cannot be had or they are not standing.
        /// </summary>
        public bool LeadInto(string once, string then)
        {
            if (!Present || _order != Order.Standing || _actor.Limp || !_actor.Work(once, ClipEnd.Hold, then))
            {
                return false;
            }
            _repeats = false;
            _surrenders = false;
            _kneels = false;
            _fallback = false;
            _reach = 0f;
            _stance = then;
            _hasPose = true;
            _posed = true;
            _clipWorn = true;
            return true;
        }

        /// <summary>
        /// How they carry themselves from now on: a clip while walking, at the pace it was made for,
        /// and optionally another while standing still (handcuffed; pushing the stretcher). Null:
        /// as the game has them. False if the walking clip cannot be had: they walk as before.
        /// </summary>
        public bool Gait(string walk, float pace = SceneClips.WalkPace, string idle = null)
        {
            return Present && _actor.Carriage(walk, pace, idle);
        }

        /// <summary>For an owner that runs every frame (the stretcher): takes someone who is held up out of their walking clip without waiting for the next tick.</summary>
        public void Sync(float now)
        {
            if (Present)
            {
                _actor.SyncGait(now);
            }
        }

        /// <summary>
        /// Every order forgotten: whoever asked puts them where they are to be from here on, frame by frame
        /// (ConveyTo), until the next order. Their navigation agent finds no way and moves nothing meanwhile.
        /// False if they cannot be (nobody there, or a ragdoll).
        /// </summary>
        public bool Convey()
        {
            if (!Present)
            {
                return false;
            }
            Drop();
            _repeats = false;
            _hasFace = false;
            _hasThen = false;
            if (!_actor.Convey())
            {
                return false;
            }
            _order = Order.Led;
            return true;
        }

        /// <summary>Where someone conveyed is this frame, the way they face and how fast they are going (their walk follows it).</summary>
        public void ConveyTo(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (Present)
            {
                _actor.ConveyTo(position, rotation, velocity);
            }
        }

        /// <summary>True if the game's own walk plays for them while they are conveyed (their agent is on the navigation mesh).</summary>
        public bool WalksUnaided => Present && _actor.WalksUnaided;

        /// <summary>True while somebody else is moving them (Convey) and they have been given no order since.</summary>
        public bool Conveyed => _order == Order.Led && Present;

        /// <summary>The medic bag rides on something (the stretcher, at a place in its own frame) instead of in the hand; null puts it back in the hand.</summary>
        public void StowBag(Transform onto, Vector3 position, Quaternion rotation)
        {
            if (Present)
            {
                _actor.StowBag(onto, position, rotation);
            }
        }

        public void Drop()
        {
            _hasPose = false;
            _surrenders = false;
            _clipWorn = false;
            _reach = 0f;
            if (_posed && Present)
            {
                _actor.ClearPose();
            }
            _posed = false;
        }

        /// <summary>While standing, plays a gesture every so often.</summary>
        public void Repeat(AnimationType gesture, float minGap, float maxGap)
        {
            if (!Present)
            {
                return;
            }
            _repeats = true;
            _hasEmoji = false;
            _repeatClip = null;
            _gesture = gesture;
            _gestureMin = minGap;
            _gestureMax = maxGap;
            _nextGesture = Time.time + Random.Range(0.3f, maxGap);
        }

        /// <summary>The same with the first of two gestures these characters have; the second is one they are known to.</summary>
        public void Repeat(AnimationType preferred, AnimationType otherwise, float minGap, float maxGap)
        {
            if (Present)
            {
                Repeat(_actor.Knows(preferred) ? preferred : otherwise, minGap, maxGap);
            }
        }

        /// <summary>
        /// While standing, plays one of the mod's clips that plays once (pointing someone along) every so often;
        /// each time it cannot be had, the first of the two gestures these characters have instead.
        /// </summary>
        public void Repeat(string clip, AnimationType preferred, AnimationType otherwise, float minGap, float maxGap)
        {
            Repeat(preferred, otherwise, minGap, maxGap);
            _repeatClip = _repeats ? clip : null;
        }

        /// <summary>The same, with an emoji bubble over the head on some of the gestures.</summary>
        public void Repeat(AnimationType gesture, float minGap, float maxGap, CharacterEmojiName emoji, float emojiChance)
        {
            Repeat(gesture, minGap, maxGap);
            _hasEmoji = _repeats;
            _emoji = emoji;
            _emojiChance = emojiChance;
        }

        /// <summary>Stops the repeated gesture, and puts the hands of someone who had them up down again.</summary>
        public void Quiet()
        {
            _repeats = false;
            if (_surrenders)
            {
                Drop();
            }
        }

        public void Emote(CharacterEmojiName emoji, float seconds = 2.5f)
        {
            ActorPool pool = ActorPool.Instance;
            if (Present && pool != null)
            {
                pool.ShowEmoji(_actor, emoji, seconds);
            }
        }

        /// <summary>Carries the current order forward. Call a few times a second while the scene runs.</summary>
        public void Tick(float now)
        {
            if (_order == Order.None)
            {
                return;
            }
            if (!Present)
            {
                Exit();   // taken away under us: the street was unloaded, or the pool was torn down
                return;
            }
            _actor.Probe(now);
            _actor.SyncGait(now);

            bool arrived = now >= _orderUntil || (now - _orderFrom >= SettleDelay && Arrived(now));
            switch (_order)
            {
                case Order.Going:
                    if (arrived && _hasThen)
                    {
                        // At the place they only pass: on to where they are going, in what is left of the time allowed.
                        float left = Mathf.Max(2f, _orderUntil - now);
                        if (Move(_then, _thenPace, Order.Going, left))
                        {
                            _orderFrom = now;
                        }
                        else
                        {
                            Settle(now);
                        }
                    }
                    else if (arrived)
                    {
                        Settle(now);
                    }
                    break;

                case Order.Boarding:
                    if (arrived)
                    {
                        ActorPool.PlayDoorSound(_actor.Position);
                        Exit();
                    }
                    break;

                case Order.Leaving:
                    if (arrived || !_actor.Visible)
                    {
                        Exit();
                    }
                    break;

                case Order.Standing:
                    if (_repeats && now >= _nextGesture)
                    {
                        _nextGesture = now + Random.Range(_gestureMin, _gestureMax);
                        if (_repeatClip == null || !_actor.Work(_repeatClip))
                        {
                            _actor.Gesture(_gesture);
                        }
                        if (_hasEmoji && Random.value < _emojiChance)
                        {
                            Emote(_emoji);
                        }
                    }
                    break;
            }
        }

        private bool Move(Vector3 destination, Pace pace, Order order, float allow)
        {
            if (_posed)
            {
                _actor.ClearPose();   // nobody walks about in a standing pose
                _posed = false;
            }
            float now = Time.time;
            _hasThen = false;   // (GoVia sets it again after its first stretch has been asked for)
            // A chaser is given a new destination a few times a second: only a fresh order restarts the arrival delay.
            if (_order != order)
            {
                _orderFrom = now;
            }
            _order = order;
            _orderUntil = now + allow;
            _closeSince = -1f;
            _exact = false;   // (GoExactly sets it again once the route has been asked for)

            // Scenes aim beside people and cars; the route wants the nearest point that can be walked on.
            bool onMesh = _onRoad ? StreetSpots.OnRoad(destination, 2.5f, out Vector3 spot) : StreetSpots.OnPavement(destination, 2.5f, out spot);
            if (onMesh && _onRoad)
            {
                StreetSpots.OutOfVehicles(ref spot);   // a place on the road is never inside a vehicle, whatever the mesh says
            }
            return _actor.MoveTo(onMesh ? spot : destination, pace);
        }

        private void Settle(float now)
        {
            _hasThen = false;
            _order = Order.Standing;
            Vector3 here = _actor.Position;
            _actor.StandAt(here, _hasFace ? _face : here);
            if (_hasPose && !_posed)
            {
                Wear();
            }
            if (_repeats)
            {
                _nextGesture = now + Random.Range(0.3f, _gestureMax);
            }
        }

        // At the end of the route. Someone who is to kneel a measured distance from what they face is not
        // stopped a step short of it: they are let come to rest, for a moment at most.
        private bool Arrived(float now)
        {
            if (!_actor.HasArrived)
            {
                _closeSince = -1f;
                return false;
            }
            if (((_reach <= 0f || !_hasPose) && !_exact) || _hasThen || _actor.Within(Exact))
            {
                return true;
            }
            if (_closeSince < 0f)
            {
                _closeSince = now;
            }
            return now - _closeSince >= ExactWait;
        }

        private void Wear()
        {
            if (_kneels)
            {
                _actor.Kneel(_reach > 0f && !InReach() ? SceneClips.Kneel : _stance);
            }
            else if (_stance == null || !(_clipWorn = _actor.Work(_stance, ClipEnd.Return, _surrenders ? SceneClips.HandsUpHold : null)))
            {
                if (_fallback)
                {
                    _actor.Pose(_pose, _prop);
                }
            }
            _posed = true;
        }

        // Whether what they face is as far in front of them as the clip's hands will work, said in the log each time it is asked.
        private bool InReach()
        {
            Vector3 gap = _face - _actor.Position;
            gap.y = 0f;
            float off = _hasFace ? gap.magnitude : 0f;
            bool reached = _hasFace && Mathf.Abs(off - _reach) <= SceneClips.CprSlack;
            ActorPool.Logger?.Info($"People: kneeling {off:F2} m from the chest of whoever is down; '{_stance}' is made for {_reach:F2} m: "
                                   + (reached ? "played." : "not played, they kneel at work beside them instead."));
            return reached;
        }
    }
}
