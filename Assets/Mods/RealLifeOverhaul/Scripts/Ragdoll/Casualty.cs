using System;
using BigAmbitions.Characters;
using Extensions;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One person hit by a vehicle, from the impact until they are off our hands: a game pedestrian (borrowed through
    /// <see cref="PedestrianTakeover"/>), one of the mod's people (an <see cref="ActorRole"/>), or a game standing figure,
    /// which stays its spawner's. Someone knocked down gets up angry and walks off; someone injured or killed stays down
    /// until taken away. Owned and ticked four times a second by <see cref="PedestrianIncidents"/>.
    /// </summary>
    public sealed class Casualty : ICasualty, ILyingBody
    {
        public enum Outcome { Knocked, Injured, Dead }

        private const float UpLatest = 9f;        // seconds: on their feet by then even if still rolling
        private const float AngryFor = 3f;        // seconds
        private const float KerbAllowance = 14f;  // seconds a pedestrian may take to reach the pavement
        private const float LeaveAllowance = 27f; // seconds
        private const float RemoveAfter = 175f;   // seconds: nobody lies in the road longer

        private enum Stage { None, Down, Angry, ToKerb, Leaving }

        private readonly PedestrianIncidents _owner;
        private readonly PedestrianTakeover _pedestrian = new PedestrianTakeover();
        private readonly ActorRole _role = new ActorRole();

        private Ragdoll _ragdoll;
        private int _serial;
        private bool _isActor;
        private BaseHuman _figure;                       // one of the game's standing figures, while it is this casualty
        private Entities.StationaryAiBehavior _figureAi; // its fidgeting, off while it is down
        private Transform _figureSeat;                   // the seat it was thrown from, sat on again if it gets up
        private Stage _stage = Stage.None;
        private float _stageEnd;
        private float _upAt;
        private Vector3 _lastPosition;
        private Vector3 _kerb;

        public Casualty(PedestrianIncidents owner)
        {
            _owner = owner;
        }

        public Outcome Result { get; private set; }
        public bool Dead => Result == Outcome.Dead;

        /// <summary>The player's car did it, so the player answers for it.</summary>
        public bool ByPlayer { get; private set; }

        /// <summary>The emergency response for this person has been asked for (once).</summary>
        public bool SceneAsked { get; set; }

        public float StruckAt { get; private set; }

        public bool InUse => _stage != Stage.None;

        public bool Resting => _stage == Stage.Down && Mine && _ragdoll.Resting;

        public bool Present => _stage == Stage.Down && Mine;

        /// <summary>True once the body lies without physics, having made way for someone thrown down later.</summary>
        public bool Still => Resting && _ragdoll.Still;

        public Vector3 Position
        {
            get
            {
                if (Present)
                {
                    _lastPosition = _ragdoll.Position;
                }
                return _lastPosition;
            }
        }

        // False once someone else ended the ragdoll: room, a fault, or the game taking the character back.
        private bool Mine => _ragdoll != null && _ragdoll.Active && _ragdoll.Serial == _serial;

        /// <summary>One of the game's pedestrians is hit. False, with the pedestrian left walking, if it cannot be done.</summary>
        public bool BeginPedestrian(Pedestrian pedestrian, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || pedestrian == null || ragdolls == null || !ragdolls.CanBegin(pedestrian.tpc) || !_pedestrian.Take(pedestrian))
            {
                return false;
            }
            _isActor = false;
            Ragdoll ragdoll = ragdolls.Begin(pedestrian.tpc, velocity);
            if (ragdoll == null)
            {
                // Not thrown down after all: back to the game where it stands, walking on.
                Vector3 here = pedestrian.tpc.transform.position;
                if (!StreetSpots.PavementAround(here, 15f, 30f, false, out Vector3 onward))
                {
                    onward = here;
                }
                _pedestrian.HandBack(onward);
                return false;
            }
            Start(ragdoll, outcome, byPlayer, now);
            return true;
        }

        /// <summary>One of the game's standing figures is hit: its fidgeting is switched off the game's way. False, with the figure as it was, if it cannot be done.</summary>
        public bool BeginFigure(BaseHuman figure, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || figure == null || ragdolls == null || !ragdolls.CanBegin(figure))
            {
                return false;
            }
            // Sitting: let go of the seat the way the game ends sitting (the seat is told it is free), then thrown from it.
            Transform seat = figure.isSittingOn;
            if (seat != null)
            {
                figure.UnFreeze();
                if (figure.isSittingOn != null)
                {
                    return false;   // the game would not let go of them: left alone
                }
            }
            var fidget = figure.GetComponent<Entities.StationaryAiBehavior>();   // on a hit only
            if (fidget != null)
            {
                fidget.Disable();   // also takes the cup or phone out of its hand, as the pool does
            }
            Ragdoll ragdoll = ragdolls.Begin(figure, velocity);
            if (ragdoll == null)
            {
                if (fidget != null)
                {
                    fidget.Enable();
                }
                if (seat != null)
                {
                    figure.SitOnChair(seat);
                }
                return false;
            }
            _isActor = false;
            _figure = figure;
            _figureSeat = seat;
            _figureAi = fidget;
            Start(ragdoll, outcome, byPlayer, now);
            return true;
        }

        /// <summary>One of the mod's own people is hit, whatever scene they were part of; that scene loses them.</summary>
        public bool BeginActor(StreetActor actor, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || actor == null || !actor.InUse || !actor.IsValid || ragdolls == null)
            {
                return false;
            }
            // Someone kneeling by hand has the animator off, which a ragdoll needs on: asked as it will be once the pose is
            // cleared. Someone hidden (not drawn) is refused, as PedestrianIncidents.CanStrike refuses them.
            bool byHand = actor.PosedByHand;
            if (!ragdolls.CanBegin(actor.Character, byHand))
            {
                return false;
            }
            actor.ClearPose();
            if (!ragdolls.CanBegin(actor.Character) || !_role.Adopt(actor))
            {
                if (byHand)
                {
                    actor.KneelByHand();   // not thrown after all: kneeling again, as the scene left them
                }
                return false;
            }
            return Throw(ragdolls, velocity, outcome, byPlayer, now);
        }

        /// <summary>A new person is put on the road at a point and hit at once: the victim of a staged incident.</summary>
        public bool BeginNewActor(Vector3 near, Vector3 velocity, Outcome outcome, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (InUse || ragdolls == null || !_role.Enter(near, ActorLook.Civilian, 3f, onRoad: true))
            {
                return false;
            }
            try
            {
                // Just switched on: one animation step, so it falls from a standing pose, not whatever it last held.
                ThirdPersonCharacter character = _role.Character;
                if (character != null && character.animator != null && character.animator.enabled)
                {
                    character.animator.Update(0f);
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
            return Throw(ragdolls, velocity, outcome, false, now);
        }

        private bool Throw(RagdollSystem ragdolls, Vector3 velocity, Outcome outcome, bool byPlayer, float now)
        {
            _isActor = true;
            _role.GoLimp();
            ThirdPersonCharacter character = _role.Character;
            Ragdoll ragdoll = character != null ? ragdolls.Begin(character, velocity) : null;
            if (ragdoll == null)
            {
                _role.Exit();
                return false;
            }
            Start(ragdoll, outcome, byPlayer, now);
            return true;
        }

        private void Start(Ragdoll ragdoll, Outcome outcome, bool byPlayer, float now)
        {
            _ragdoll = ragdoll;
            _serial = ragdoll.Serial;
            Result = outcome;
            ByPlayer = byPlayer;
            SceneAsked = false;
            StruckAt = now;
            _lastPosition = ragdoll.Position;
            _stage = Stage.Down;
            _upAt = now + Random.Range(4f, 6f);
            _stageEnd = now + (outcome == Outcome.Knocked ? UpLatest : RemoveAfter);
        }

        /// <param name="avoid">What they walk away from once on their feet: the player.</param>
        public void Tick(float now, Vector3 avoid)
        {
            switch (_stage)
            {
                case Stage.Down:
                    if (!Mine || _ragdoll.Astray)
                    {
                        Remove();   // nothing left lying there, or it fell out of the world
                    }
                    else if (Result == Outcome.Knocked)
                    {
                        if (now >= _stageEnd || (now >= _upAt && _ragdoll.Resting))
                        {
                            GetUp(now, avoid);
                        }
                    }
                    else if (now >= _stageEnd)
                    {
                        Remove();
                    }
                    break;

                case Stage.Angry:
                    if (_isActor)
                    {
                        _role.Tick(now);
                    }
                    if (StillOurs() && now >= _stageEnd)
                    {
                        Leave(now, avoid);
                    }
                    break;

                case Stage.ToKerb:
                    if (StillOurs() && (_pedestrian.Arrived || now >= _stageEnd))
                    {
                        // On the pavement: the game has its pedestrian back, walking on up the street.
                        if (!StreetSpots.AwayFrom(_kerb, avoid, 25f, onRoad: false, out Vector3 onward))
                        {
                            onward = _kerb;
                        }
                        _stage = Stage.None;
                        _pedestrian.HandBack(onward);
                    }
                    break;

                case Stage.Leaving:
                    _role.Tick(now);
                    if (!_role.Present || now >= _stageEnd)
                    {
                        Remove();
                    }
                    break;
            }
        }

        // After getting up: false (and cleaned up) if the person was taken from us meanwhile.
        private bool StillOurs()
        {
            bool ours = _isActor ? _role.Present
                : (object)_figure != null ? _figure != null && _figure.gameObject.activeInHierarchy
                : _pedestrian.Held && !_pedestrian.PutAway;
            if (!ours)
            {
                Remove();
            }
            return ours;
        }

        // There is no getting-up animation: the ragdoll ends and they are standing where they landed.
        private void GetUp(float now, Vector3 avoid)
        {
            // A standing figure cannot walk back, so it gets up where it stood; so does anyone who landed where nobody can stand.
            if ((object)_figure != null || !StreetSpots.OnRoad(_ragdoll.Position, 3f, out Vector3 at))
            {
                at = _ragdoll.Origin;
            }
            _ragdoll.End(true, at, false);
            _ragdoll = null;
            _lastPosition = at;
            if ((object)_figure != null)
            {
                // Back where it stood or sat, and cross about it: the emoji, and the gesture if it has one.
                try
                {
                    if (_figureSeat != null && _figure != null)
                    {
                        _figure.SitOnChair(_figureSeat);
                    }
                    ActorPool people = ActorPool.Instance;
                    if (people != null && _figure != null)
                    {
                        people.ShowEmojiAt(_figure.head, CharacterEmojiName.CommonRedAngry, AngryFor);
                        Animator animator = _figure.animator;
                        if (_figureSeat == null && animator != null && animator.HasParameter(AnimationType.IDontThinkSo.ToString()))
                        {
                            animator.SetTrigger(AnimationType.IDontThinkSo);
                        }
                    }
                }
                catch (Exception e)
                {
                    PedestrianIncidents.Logger?.Error(e);
                }
                _stage = Stage.Angry;
                _stageEnd = now + AngryFor;
                return;
            }

            ActorPool pool = ActorPool.Instance;
            if (_isActor)
            {
                if (!_role.Rise(at))
                {
                    Remove();
                    return;
                }
                _role.Face(avoid);
                _role.Emote(CharacterEmojiName.CommonRedAngry, AngryFor);
                _role.Gesture(AnimationType.IDontThinkSo);
            }
            else
            {
                if (!_pedestrian.StandUp(at))
                {
                    Remove();
                    return;
                }
                if (pool != null)
                {
                    pool.ShowEmoji(_pedestrian.Character, CharacterEmojiName.CommonRedAngry, AngryFor);
                }
            }
            _stage = Stage.Angry;
            _stageEnd = now + AngryFor;
        }

        private void Leave(float now, Vector3 avoid)
        {
            if ((object)_figure != null)
            {
                _stage = Stage.None;
                LetFigureGo(hide: false);
                return;
            }
            if (_isActor)
            {
                _role.WalkAwayFrom(avoid, Pace.Walk, 30f, 25f);
                _stage = Stage.Leaving;
                _stageEnd = now + LeaveAllowance;
                return;
            }
            // A pedestrian first walks to the nearest pavement, the only ground the game lets it use.
            ThirdPersonCharacter character = _pedestrian.Character;
            if (character != null && StreetSpots.OnPavement(character.transform.position, 14f, out _kerb) && _pedestrian.WalkTo(_kerb))
            {
                _stage = Stage.ToKerb;
                _stageEnd = now + KerbAllowance;
            }
            else
            {
                Remove();
            }
        }

        /// <summary>Picked up by the ambulance crew: a body at rest is frozen, so it moves as one piece by its root until taken away.</summary>
        public bool Lift(out Transform body, out Transform hips, out Transform head)
        {
            body = null;
            hips = null;
            head = null;
            if (!Present || !_ragdoll.Resting || _ragdoll.Astray || _ragdoll.Character == null)
            {
                return false;
            }
            body = _ragdoll.Character.transform;
            hips = _ragdoll.Hips;
            head = _ragdoll.Head;
            return hips != null && head != null;
        }

        /// <summary>Which way they lie: read from the body, nothing changed.</summary>
        public bool Spine(out Vector3 hips, out Vector3 head)
        {
            hips = default;
            head = default;
            if (!Present || _ragdoll.Hips == null || _ragdoll.Head == null)
            {
                return false;
            }
            hips = _ragdoll.Hips.position;
            head = _ragdoll.Head.position;
            return true;
        }

        // A standing figure leaves our hands: one lying there is switched off (its spawner pools it when the spot is next
        // cleared), one on its feet fidgets again, and one the game already put away is only forgotten.
        private void LetFigureGo(bool hide)
        {
            BaseHuman figure = _figure;
            Entities.StationaryAiBehavior fidget = _figureAi;
            _figure = null;
            _figureAi = null;
            _figureSeat = null;
            try
            {
                if (figure == null || !figure.gameObject.activeInHierarchy)
                {
                    return;
                }
                if (hide)
                {
                    figure.gameObject.SetActive(false);
                }
                else if (fidget != null)
                {
                    fidget.Enable();
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
        }

        /// <summary>The ambulance is leaving with them, or the scene is over.</summary>
        public void TakeAway()
        {
            if (!InUse)
            {
                return;
            }
            bool wasDown = _stage == Stage.Down;
            Remove();
            if (wasDown)
            {
                _owner.OnCollected(this);
            }
        }

        /// <summary>
        /// Off the street at once, in any state, with the character whole again: the ragdoll ends, then each kind goes
        /// back to its own pool. Safe at any time; no step can stop the next.
        /// </summary>
        public void Remove()
        {
            _stage = Stage.None;
            bool lying = false;
            try
            {
                if (Mine)
                {
                    lying = true;
                    _lastPosition = _ragdoll.Position;
                    _ragdoll.End(false, default, true);
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
            _ragdoll = null;
            LetFigureGo(hide: lying);
            try
            {
                _role.Exit();
                if (_pedestrian.PutAway)
                {
                    _pedestrian.Forget();   // the game already has it back (the player went indoors)
                }
                else
                {
                    _pedestrian.Remove();
                }
            }
            catch (Exception e)
            {
                PedestrianIncidents.Logger?.Error(e);
            }
        }
    }
}
