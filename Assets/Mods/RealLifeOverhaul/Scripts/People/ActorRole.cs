using BigAmbitions.Characters;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One person's part in a scene: holds a character from the pool and carries out one order at a time. Every call is
    /// safe with nobody behind the role. Ticked by its scene a few times a second.
    /// </summary>
    public sealed partial class ActorRole
    {
        private const float SettleDelay = 0.3f;   // seconds: a route asked for this recently has not been worked out yet
        private const float Exact = 0.05f;        // metres from the end of the route at which someone who must be at a measured distance has arrived
        private const float ExactWait = 1f;       // ...and the seconds they are given to close that last step before they are taken as there

        private enum Order { None, Standing, Going, Boarding, Leaving, Led, Seated }

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

        /// <summary>The same for someone only there to be seen, whom the pool may take back at any moment.</summary>
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

        /// <summary>Takes over someone already on the street from whatever role had them (it finds them gone); no order is given.</summary>
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

        /// <summary>For someone lying down about to be carried: orders forgotten, pose kept; the carrier moves them. Null if nobody.</summary>
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

        public void Gesture(AnimationType gesture)
        {
            if (Present && !_actor.Limp)
            {
                _actor.Gesture(gesture);
            }
        }

        public void Gesture(AnimationType preferred, AnimationType otherwise)
        {
            if (Present && !_actor.Limp)
            {
                _actor.Gesture(_actor.Knows(preferred) ? preferred : otherwise);
            }
        }

        public void CarryBag(bool carry)
        {
            if (Present)
            {
                _actor.CarryBag(carry);
            }
        }

        public void HiVis(bool on)
        {
            if (Present)
            {
                _actor.HiVis(on);
            }
        }

        /// <summary>False while the pool has them switched off (far away or behind the camera), or with nobody there.</summary>
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

        /// <summary>Sits them on a seat as the game seats its employees; a repeated gesture goes on. False if they cannot sit.</summary>
        public bool Sit(Transform seat)
        {
            if (!Present || _actor.Limp || seat == null)
            {
                return false;
            }
            Drop();
            if (!_actor.Sit(seat))
            {
                return false;
            }
            _hasThen = false;
            _order = Order.Seated;
            _orderFrom = Time.time;
            if (_repeats)
            {
                _nextGesture = Time.time + Random.Range(0.3f, _gestureMax);
            }
            return true;
        }

        public bool Knows(AnimationType gesture) => Present && _actor.Knows(gesture);


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

        /// <summary>The same by way of another place first (round someone lying there); the time allowed covers both stretches.</summary>
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

        /// <summary>Goes to a point a clip for two is measured from, and is not taken as arrived a step short of it.</summary>
        public void GoExactly(Vector3 destination, Pace pace, Vector3 thenFace, float allow = 10f)
        {
            Go(destination, pace, thenFace, allow);
            _exact = _order == Order.Going;
        }

        /// <summary>Stood exactly at a place now: only for the last centimetres a route left. False unless they are standing.</summary>
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
        /// The length in metres of the way to a place as their agent would walk it now, and whether it gets there; -1 if it
        /// cannot be asked. Plans a route: for a check when a crew sets off, never on a timer.
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


        /// <summary>Every order forgotten: whoever asked moves them frame by frame (ConveyTo). False with nobody or a ragdoll.</summary>
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

        public void ConveyTo(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (Present)
            {
                _actor.ConveyTo(position, rotation, velocity);
            }
        }

        public bool WalksUnaided => Present && _actor.WalksUnaided;

        /// <summary>True while somebody else is moving them (Convey) and they have been given no order since.</summary>
        public bool Conveyed => _order == Order.Led && Present;

        public void StowBag(Transform onto, Vector3 position, Quaternion rotation)
        {
            if (Present)
            {
                _actor.StowBag(onto, position, rotation);
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

                case Order.Seated:
                    if (!_actor.Seated)
                    {
                        Settle(now);   // stood up by something else: standing where they got up
                    }
                    else if (_repeats && now >= _nextGesture)
                    {
                        _nextGesture = now + Random.Range(_gestureMin, _gestureMax);
                        _actor.Gesture(_gesture);
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
            _hasThen = false;   // GoVia sets it again after its first stretch is asked for
            // A chaser is given a new destination a few times a second: only a fresh order restarts the arrival delay.
            if (_order != order)
            {
                _orderFrom = now;
            }
            _order = order;
            _orderUntil = now + allow;
            _closeSince = -1f;
            _exact = false;   // GoExactly sets it again once the route is asked for

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

        // At the end of the route; someone kneeling a measured distance from what they face is let come to rest first.
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
    }
}
