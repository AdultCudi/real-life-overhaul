using System.Collections.Generic;
using BigAmbitions.Characters;
using Extensions;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    public enum ActorLook { Civilian, Officer, Paramedic }

    public enum Pace { Walk, Jog, Run }

    /// <summary>
    /// One mod-controlled person on the street: a thin wrapper over the game's own character
    /// that exposes just what scenes need (walk, run, stand, pose, play a gesture). Obtained
    /// from and returned to <see cref="ActorPool"/>; never created directly. Scenes do not hold
    /// one of these themselves: they hold an <see cref="ActorRole"/>, which knows when the
    /// person behind it has been handed to someone else.
    /// </summary>
    public sealed class StreetActor
    {
        private const float GroundSnap = 1.2f;   // metres: a standing person this far from the ground is put on it

        private static readonly int AnimationSpeedId = Animator.StringToHash("AnimationSpeed");
        private static NavMeshQueryFilter _roadFilter = new NavMeshQueryFilter { agentTypeID = NavMeshHelper.PlayerAgentTypeId, areaMask = NavMesh.AllAreas };

        // Whether the street characters' animator knows each pose and gesture: asked once each, then remembered.
        private static readonly Dictionary<int, bool> KnownPoses = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> KnownGestures = new Dictionary<int, bool>();

        // Jogging and running. The game only ever walks its street characters, so their agents are set up for
        // that: turning at 120 degrees a second and of no particular importance to the people round them. A
        // runner turns faster, picks up speed sooner and is given way to; a walker has the game's own values.
        private const float JogAcceleration = 14f;   // m/s2
        private const float RunAcceleration = 20f;
        private const float JogTurning = 360f;       // degrees per second
        private const float RunTurning = 540f;
        private const int JogPriority = 15;          // the lower the number, the more others give way
        private const int RunPriority = 5;
        private const float ProbeTime = 2.5f;        // seconds the first runner of a session is watched for the log

        private static bool _runLogged;

        private readonly float _acceleration;   // the agent's own values, as the game made it
        private readonly float _turning;
        private readonly int _priority;
        private Pace _tuned = Pace.Walk;
        private bool _probing;
        private Pace _probePace;
        private float _probeFrom;
        private float _probePeak;
        private Vector3 _probeStart;

        private bool _hasPose;
        private PermanentAnimationType _pose;
        private bool _standing;    // frozen in place with pathfinding and collision off
        private Transform _seat;   // the place they sit on, while seated (Sit / Rise)
        private Vector3 _stoodAt;
        private Quaternion _stoodFacing;
        private KneelPose _kneel;  // made the first time this person kneels
        private ActorClips _clips; // made the first time this person plays one of the mod's clips
        private Pace _pace = Pace.Walk;   // the pace last asked for, for when a carriage that held them to its own is dropped

        /// <summary>What a uniform recoloured on this character, put back before the next outfit. The pool's business.</summary>
        internal readonly List<Uniforms.Dye> Dyes = new List<Uniforms.Dye>(4);

        /// <summary>The shirt of the uniform this character wears, for the hi-vis look. The pool's business.</summary>
        internal Uniforms.Shirt Shirt;

        /// <summary>The equipment worn with a uniform; null until this character has worn one. The pool's business.</summary>
        internal ServiceKit Kit;

        private readonly Vector3 _scale;   // the character's own scale, as the game made it

        /// <summary>How much bigger than the game built them this character stands (Stature); 1 for everyone but officers and paramedics.</summary>
        internal float Size { get; private set; } = 1f;

        /// <summary>
        /// Scales the whole character about its feet: skeleton, drawn body, whatever hangs on the bones,
        /// capsule and navigation agent (the engine scales both with the object they are on). The pool's business.
        /// </summary>
        internal void Resize(float size)
        {
            if (Size == size)
            {
                return;
            }
            Size = size;
            Character.transform.localScale = _scale * size;
        }

        internal StreetActor(ThirdPersonCharacter character)
        {
            Character = character;
            _scale = character.transform.localScale;
            NavMeshAgent agent = character.navmeshAgent;
            if (agent != null)
            {
                _acceleration = agent.acceleration;
                _turning = agent.angularSpeed;
                _priority = agent.avoidancePriority;
            }
        }

        public ThirdPersonCharacter Character { get; }
        public ActorLook Look { get; internal set; }
        public bool InUse { get; internal set; }

        /// <summary>Goes up every time the pool hands this person out, so an old holder can tell it is no longer theirs.</summary>
        public int Lease { get; internal set; }

        /// <summary>
        /// True for someone who is only there to be seen (an officer on the beat, at a parked patrol
        /// car, at a junction): the pool takes them back at once when a scene needs a person.
        /// </summary>
        public bool Yields { get; internal set; }

        /// <summary>A civilian whose look has been out often enough, or long enough: the pool gives them a new one quietly, on the shelf (People/Looks).</summary>
        internal bool Stale { get; set; }

        /// <summary>How many times they have been out in the look they have, when it was put on and when they last came back (unscaled seconds).</summary>
        internal int Uses;
        internal float DressedAt;
        internal float ReleasedAt;

        public bool IsValid => Character != null;
        public Vector3 Position => Character.transform.position;

        /// <summary>False while the pool has switched this person's animation and rendering off (far away, or behind the camera).</summary>
        public bool Visible => Character.visible;

        /// <summary>True while this person is a ragdoll: physics has the body, and nothing here may move, hide or animate it.</summary>
        public bool Limp => RagdollSystem.Holds(Character);

        /// <summary>The navigation filter for places only the player can normally walk, such as the road.</summary>
        public static NavMeshQueryFilter RoadFilter => _roadFilter;

        /// <summary>Walks, jogs or runs to a point. Returns false if no route could be requested.</summary>
        public bool MoveTo(Vector3 destination, Pace pace)
        {
            Rise();
            Unconvey(walksOn: true);
            MakeMobile();
            NavMeshAgent agent = Character.navmeshAgent;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            // Someone whose carriage is a walking clip keeps to the pace that clip was made for, whatever was asked.
            _pace = pace;
            float held = _clips != null ? _clips.Pace : 0f;
            if (held > 0f)
            {
                pace = Pace.Walk;
            }
            Character.SetWalkingSpeed(ToGame(pace));
            if (held > 0f)
            {
                agent.speed = held;
            }
            Tune(pace);
            agent.isStopped = false;
            bool going = agent.SetDestination(destination);
            if (going && _clips != null)
            {
                _clips.Going(Time.time);
                BagInStep();
            }
            if (going && pace != Pace.Walk && !_runLogged && !_probing)
            {
                _probing = true;
                _probePace = pace;
                _probeFrom = Time.time;
                _probePeak = 0f;
                _probeStart = Character.transform.position;
            }
            return going;
        }

        // The agent set up for the pace asked for: pushed only when the pace changes.
        private void Tune(Pace pace)
        {
            if (pace == _tuned)
            {
                return;
            }
            _tuned = pace;
            NavMeshAgent agent = Character.navmeshAgent;
            if (pace == Pace.Walk)
            {
                agent.acceleration = _acceleration;
                agent.angularSpeed = _turning;
                agent.avoidancePriority = _priority;
                return;
            }
            bool run = pace == Pace.Run;
            agent.acceleration = Mathf.Max(_acceleration, run ? RunAcceleration : JogAcceleration);
            agent.angularSpeed = Mathf.Max(_turning, run ? RunTurning : JogTurning);
            agent.avoidancePriority = Mathf.Min(_priority, run ? RunPriority : JogPriority);
        }

        /// <summary>
        /// Watches the first person of a session who is told to jog or run, for a couple of seconds, and
        /// says in the log (once) how fast they actually got: so that a run which looks like a walk can be
        /// told from the log. A comparison when there is nothing to watch. Called by the role that ticks them.
        /// </summary>
        internal void Probe(float now)
        {
            if (!_probing)
            {
                return;
            }
            NavMeshAgent agent = Character.navmeshAgent;
            bool moving = agent.enabled && _tuned != Pace.Walk;
            if (moving)
            {
                _probePeak = Mathf.Max(_probePeak, agent.velocity.magnitude);
                if (now - _probeFrom < ProbeTime)
                {
                    return;
                }
            }
            _probing = false;
            if (_runLogged || now - _probeFrom < 0.6f)
            {
                return;   // stopped before anything could be told: the next runner is watched instead
            }
            _runLogged = true;
            Vector3 gone = Character.transform.position - _probeStart;
            gone.y = 0f;
            RuntimeAnimatorController controller = Character.animator != null ? Character.animator.runtimeAnimatorController : null;
            ActorPool.Logger?.Info(System.FormattableString.Invariant($"People: first runner of the session ({_probePace}): asked for {agent.speed:F1} m/s, reached {_probePeak:F1} m/s and covered ")
                                   + System.FormattableString.Invariant(
                                       $"{gone.magnitude:F1} m in {now - _probeFrom:F1} s; acceleration {agent.acceleration:F0} m/s2 (the game's own {_acceleration:F0}), ")
                                   + $"turning {agent.angularSpeed:F0} deg/s (the game's own {_turning:F0}), gives way at priority {agent.avoidancePriority}, "
                                   + $"{(agent.agentTypeID == NavMeshHelper.NpcAgentTypeId ? "pavement" : "road")} agent, animator '{(controller != null ? controller.name : "none")}'.");
        }

        public bool HasArrived
        {
            get
            {
                NavMeshAgent agent = Character.navmeshAgent;
                return !agent.enabled || !agent.isOnNavMesh
                       || (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.3f));
            }
        }

        /// <summary>
        /// True once the route last asked of <see cref="MoveTo"/> has been worked out by the engine (it is no longer
        /// pending). A few reads of the agent; nothing is asked of the navigation mesh.
        /// </summary>
        public bool RouteKnown
        {
            get
            {
                NavMeshAgent agent = Character.navmeshAgent;
                return agent.enabled && agent.isOnNavMesh && !agent.pathPending;
            }
        }

        /// <summary>
        /// False when that route, once worked out, does not reach the place asked for: the engine then walks them to the
        /// nearest point it can and stops there (a crossing a vehicle stands on, a pavement a cordon has cut). Read with
        /// <see cref="RouteKnown"/>.
        /// </summary>
        public bool RouteWhole => Character.navmeshAgent.pathStatus == NavMeshPathStatus.PathComplete;

        public void Halt()
        {
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        /// <summary>
        /// Plants the actor at an exact spot facing a point, with pathfinding and its collider off.
        /// This is how the game places its own stationary street characters: it costs nothing to
        /// hold, and traffic does not stop for someone who is only standing at the kerb.
        /// </summary>
        public void StandAt(Vector3 position, Vector3 lookAt)
        {
            Rise();
            Unconvey(walksOn: false);
            if (!_standing)
            {
                _standing = true;
                Halt();
                Character.navmeshAgent.enabled = false;
                Character.capsuleCollider.enabled = false;
                NotifyTrafficColliderGone();
            }
            if (_clips != null)
            {
                _clips.Stood();
                BagInStep();
            }
            // On the street itself: a point handed over by a route or a car can be a little above or below it.
            if (StreetSpots.Ground(position, out float ground) && Mathf.Abs(ground - position.y) < GroundSnap)
            {
                position.y = ground;
            }
            Character.transform.position = position;
            Face(lookAt);
        }

        /// <summary>
        /// Turns a standing actor to face a point. The game only turns a character towards its
        /// look target while its pathfinding is off, so this does nothing to someone walking.
        /// </summary>
        public void Face(Vector3 lookAt)
        {
            // The game builds a rotation from this every frame, and complains every frame about a zero direction.
            Vector3 flat = lookAt - Character.transform.position;
            flat.y = 0f;
            Character.LookTarget = flat.sqrMagnitude > 0.04f ? lookAt : Vector3.zero;
        }

        /// <summary>Holds a looping pose until <see cref="ClearPose"/>; handObject is an optional game prop name.</summary>
        public void Pose(PermanentAnimationType pose, string handObject = null)
        {
            ClearPose();
            if (!Knows(pose))
            {
                return;   // these characters cannot do it; they simply stand
            }
            Character.animator.SetBool(pose);
            if (!string.IsNullOrEmpty(handObject))
            {
                Character.AddHandObject(handObject);
            }
            _pose = pose;
            _hasPose = true;
        }

        /// <summary>
        /// Goes down where they stand, bent over whatever is in front of them, until
        /// <see cref="ClearPose"/>: in one of the mod's kneeling clips if it can be had, else on one
        /// knee in the pose set by hand (which costs nothing to hold). Does nothing to a ragdoll,
        /// or to a skeleton that cannot be posed (they simply stand).
        /// </summary>
        public void Kneel(string clip = SceneClips.Kneel)
        {
            ClearPose();
            if (Limp)
            {
                return;
            }
            if (Work(clip))
            {
                if (Kit != null)
                {
                    Kit.PutDown(Character.transform, true);   // the bag stands beside them while they work
                }
                return;
            }
            if (_kneel == null)
            {
                _kneel = new KneelPose();
            }
            if (_kneel.Apply(Character) && Kit != null)
            {
                Kit.PutDown(Character.transform, true);   // the bag stands beside them while they work
            }
        }

        /// <summary>A paramedic takes the medic bag along (or leaves it in the ambulance). Nobody else has one.</summary>
        public void CarryBag(bool carry)
        {
            if (Kit != null)
            {
                Kit.Carry(carry);
            }
        }

        /// <summary>
        /// An officer's shirt turned hi-vis yellow for traffic duty, with two reflective bands round the
        /// chest and waist, or back to the plain uniform. The pool puts it back when it takes the
        /// character in. Does nothing to a ragdoll.
        /// </summary>
        public void HiVis(bool on)
        {
            if (Limp)
            {
                return;
            }
            if (Kit != null)
            {
                Kit.Vest(on);   // the two reflective bands: with them the yellow reads as a vest
            }
            if (Shirt.Entry == null || Shirt.Bright == on)
            {
                return;
            }
            Uniforms.HiVis(Character.appearanceSetter, ref Shirt, on);
            // If the look had to be built again, its new mesh is drawn or not as the old one was.
            SkinnedMeshRenderer skin = Character.appearanceSetter.lastMergedMeshRenderer;
            if (skin != null && skin.enabled != Character.visible)
            {
                skin.enabled = Character.visible;
            }
        }

        /// <summary>True while they sit on a seat (Sit).</summary>
        public bool Seated => _seat != null;

        /// <summary>
        /// Sits them on a place to sit exactly as the game seats its own people and its employees at a desk
        /// (<c>ThirdPersonCharacter.SitOnChair</c>: the game's sitting pose, the navigation agent off, the body put on the place
        /// and turned as it is), from where they stand. Any clip or pose is ended first. False, and nothing changed, if they
        /// cannot (seated already, lying in the road, or these characters have no sitting pose).
        /// </summary>
        public bool Sit(Transform seat)
        {
            if (seat == null || _seat != null || Limp || Kneeling || !Knows(PermanentAnimationType.Sitting))
            {
                return false;
            }
            EndClips();
            ClearPose();
            if (!_standing)
            {
                StandAt(Position, Position);
            }
            _stoodAt = Character.transform.position;
            _stoodFacing = Character.transform.rotation;
            _seat = seat;
            Character.SitOnChair(seat);
            return true;
        }

        /// <summary>
        /// On their feet again where they stood before they sat, by the game's own way off a seat (<c>ThirdPersonCharacter.Reset</c>),
        /// and left standing there. Safe at any time; every way they are moved, placed or put away goes through it first.
        /// </summary>
        public void Rise()
        {
            if (_seat == null)
            {
                return;
            }
            _seat = null;
            if (Character == null || Limp)
            {
                return;
            }
            // There first, then the game's reset (which switches the agent on: at the seat, off the mesh, the engine warns).
            Character.transform.SetPositionAndRotation(_stoodAt, _stoodFacing);
            Character.Reset();
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled)
            {
                agent.Warp(_stoodAt);
                agent.enabled = false;   // standing, as they were before they sat: whoever sends them on switches it on again
            }
            Character.transform.rotation = _stoodFacing;
            Character.LookTarget = Vector3.zero;
            _standing = true;
        }

        /// <summary>True while down on one knee.</summary>
        public bool Kneeling => _kneel != null && _kneel.Active;

        /// <summary>Ends whatever pose is held, kneeling included.</summary>
        public void ClearPose()
        {
            if (_clips != null && _clips.Standing)
            {
                _clips.Rest();
                BagInStep();
                if (Kit != null)
                {
                    Kit.PutDown(null, false);
                }
            }
            if (Kneeling)
            {
                _kneel.Clear(Character);
                if (Kit != null)
                {
                    Kit.PutDown(null, false);
                }
            }
            if (!_hasPose)
            {
                return;
            }
            _hasPose = false;
            Character.animator.SetBool(_pose, false);
            Character.RemoveHandObject();
        }

        // ---------------------------------------------------------------- the mod's own clips (ActorClips)

        /// <summary>
        /// Plays one of the mod's clips while they stand, until <see cref="ClearPose"/> (a clip that
        /// plays once ends by itself). False, with nothing changed, if it cannot be had: the caller
        /// keeps to the pose it had before.
        /// </summary>
        /// <param name="then">A loop that a clip which plays once leads into (ActorClips.Stand).</param>
        public bool Work(string clip, ClipEnd end = ClipEnd.Return, string then = null)
        {
            if (clip == null || Limp || Kneeling)
            {
                return false;
            }
            if (!HasClips())
            {
                return false;
            }
            bool played = _clips.Stand(clip, end, then);
            BagInStep();
            return played;
        }

        /// <summary>The tool of the clip being played out of the hand, the clip going on (ActorClips.Stow).</summary>
        public void StowTool()
        {
            if (_clips != null)
            {
                _clips.Stow();
            }
        }

        /// <summary>Lets go of a clip held on its last pose (ActorClips.Release).</summary>
        public void ReleaseClip()
        {
            if (_clips != null)
            {
                _clips.Release();
                BagInStep();
            }
        }

        /// <summary>
        /// How they carry themselves from now on: one clip while walking, at the pace it was made for
        /// (their navigation agent is held to it), and optionally another while standing still. Null
        /// gives them back the game's own walk and the pace last asked for. False if the walking clip
        /// cannot be had; they then walk as the game has them walk.
        /// </summary>
        public bool Carriage(string walk, float pace = SceneClips.WalkPace, string idle = null)
        {
            if (walk == null ? _clips == null : Limp || !HasClips())
            {
                return false;
            }
            float before = _clips.Pace;
            bool carried = walk != null && !Limp && _clips.Carry(walk, pace, idle);
            if (!carried)
            {
                _clips.Upright();
            }
            BagInStep();
            float held = _clips.Pace;
            NavMeshAgent agent = Character.navmeshAgent;
            if (held != before && !Limp && agent != null && agent.enabled)
            {
                // Under way already: the pace changes now, not at the next order.
                Character.SetWalkingSpeed(ToGame(held > 0f ? Pace.Walk : _pace));
                if (held > 0f)
                {
                    agent.speed = held;
                }
            }
            return carried;
        }

        /// <summary>
        /// Whether someone with a walking clip is in fact walking: held up, they leave the clip, and
        /// take it up again when they move. A comparison unless that changed. Called by whoever
        /// ticks them.
        /// </summary>
        public void SyncGait(float now)
        {
            if (_clips == null || _conveyed || (_clips.Pace <= 0f && !_clips.LeadingIn))
            {
                return;   // (someone conveyed is told their speed every frame by whoever moves them: ConveyTo)
            }
            NavMeshAgent agent = Character.navmeshAgent;
            _clips.Sync(agent != null && agent.enabled ? agent.velocity.sqrMagnitude : 0f, now);
            BagInStep();
        }

        /// <summary>
        /// Every clip of ours off the body at once, and its tool out of the hand: before they are
        /// thrown down as a ragdoll, and before the pool has them back.
        /// </summary>
        public void EndClips()
        {
            if (_clips == null)
            {
                return;
            }
            bool paced = _clips.Pace > 0f;
            _clips.End();
            BagInStep();
            NavMeshAgent agent = Character.navmeshAgent;
            if (paced && agent != null && agent.enabled)
            {
                Character.SetWalkingSpeed(ToGame(_pace));
            }
        }

        /// <summary>
        /// From now on somebody else puts this person where they are to be, every frame (ConveyTo): the stretcher
        /// its crew, along a line worked out beforehand. Their navigation agent no longer moves them and is
        /// asked for no route; it is only told how fast they are going, which is what the game's own walk plays
        /// to. Until the next order (MoveTo, StandAt) or the pool has them back. False for a ragdoll.
        /// </summary>
        public bool Convey()
        {
            if (Limp)
            {
                return false;
            }
            MakeMobile();
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            _conveyed = true;
            Character.LookTarget = Vector3.zero;
            _pace = Pace.Walk;
            Character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            Tune(Pace.Walk);
            if (_clips != null)
            {
                _clips.Going(Time.time);
                BagInStep();
            }
            return true;
        }

        private bool _conveyed;   // moved by whoever conveys them, not by their navigation agent

        /// <summary>
        /// Where a conveyed person is this frame, the way they face and how fast they are going. Their walking
        /// clip of the mod's own (if they have one) follows that speed; so does the game's walk, as long as
        /// their agent is on the navigation mesh at all (it need not be able to get anywhere on it).
        /// </summary>
        public void ConveyTo(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Character.transform.SetPositionAndRotation(position, rotation);
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = position;
                agent.velocity = velocity;
            }
            if (_clips != null && (_clips.Pace > 0f || _clips.LeadingIn))
            {
                _clips.Sync(velocity.sqrMagnitude, Time.time);
                BagInStep();
            }
        }

        /// <summary>True if the game's own walk will play for this person while they are conveyed: their agent is on the navigation mesh.</summary>
        public bool WalksUnaided
        {
            get
            {
                NavMeshAgent agent = Character.navmeshAgent;
                return agent != null && agent.enabled && agent.isOnNavMesh;
            }
        }

        // No longer conveyed: the agent has its say over the body again. It was left wherever it could follow to,
        // so before it moves anything it is put where the body is; where it cannot be (off the mesh), or when the
        // person is about to be stood, it is switched off instead, so that the body stays exactly where it was put.
        private void Unconvey(bool walksOn)
        {
            if (!_conveyed)
            {
                return;
            }
            _conveyed = false;
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent == null)
            {
                return;
            }
            bool onMesh = agent.enabled && agent.isOnNavMesh;
            if (onMesh)
            {
                agent.velocity = Vector3.zero;
            }
            if (!walksOn || !onMesh || !agent.Warp(Character.transform.position))
            {
                agent.enabled = false;
                _standing = walksOn || _standing;   // (MakeMobile switches it on again for whoever is sent somewhere)
            }
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        /// <summary>The medic bag rides on something (the stretcher) instead of in the hand; null puts it back in the hand.</summary>
        public void StowBag(Transform onto, Vector3 position, Quaternion rotation)
        {
            if (Kit != null)
            {
                Kit.Stow(onto, position, rotation);
            }
        }

        /// <summary>True once their route has brought them to within a distance of its end, however slowly they are still closing.</summary>
        public bool Within(float slack)
        {
            NavMeshAgent agent = Character.navmeshAgent;
            return !agent.enabled || !agent.isOnNavMesh
                   || (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + slack));
        }

        // This character's use of the mod's clips: made the first time one is asked for after the clip bundle has
        // loaded (before that there is nothing to play), and told whether they are standing or on their way.
        private bool HasClips()
        {
            if (_clips == null)
            {
                if (!ClipLibrary.Ready)
                {
                    return false;
                }
                _clips = new ActorClips(Character);
                _clips.Show(Character.visible);
                if (!_standing)
                {
                    _clips.Going(Time.time);
                }
            }
            return true;
        }

        // A clip that brings a medic bag of its own holds it at its own grip: the bag of the uniform is not drawn as well.
        private void BagInStep()
        {
            if (Kit != null && _clips != null)
            {
                Kit.InClip(_clips.Tool == BagProp);
            }
        }

        private const string BagProp = "medic_bag";

        private static ThirdPersonCharacter.WalkingSpeed ToGame(Pace pace)
        {
            return pace == Pace.Run ? ThirdPersonCharacter.WalkingSpeed.Run
                : pace == Pace.Jog ? ThirdPersonCharacter.WalkingSpeed.Jog
                : ThirdPersonCharacter.WalkingSpeed.Walk;
        }

        // The poses were chosen from animations the game uses on other characters; this says so in
        // the log, once, if the street characters turn out not to have one of them.
        private bool Knows(PermanentAnimationType pose)
        {
            if (!KnownPoses.TryGetValue((int)pose, out bool known))
            {
                known = Character.animator.HasParameter(pose.ToString());
                KnownPoses[(int)pose] = known;
                if (!known)
                {
                    ActorPool.Logger?.Warn($"People: street characters have no '{pose}' animation; that pose is skipped.");
                }
            }
            return known;
        }

        /// <summary>Plays a one-off gesture and returns how long it lasts, in seconds. Searches the clip list: not for repeated use.</summary>
        public float Play(AnimationType gesture)
        {
            return Character.animator.RunAnimationLength(gesture);
        }

        /// <summary>Plays a one-off gesture without asking how long it is, which is what the game's own call spends its time on.</summary>
        public void Gesture(AnimationType gesture)
        {
            if (Kneeling || (_clips != null && _clips.Busy))
            {
                return;   // the animator is off while that pose is held; under a clip of ours a gesture would not show
            }
            Animator animator = Character.animator;
            animator.SetFloat(AnimationSpeedId, 1f);
            animator.SetTrigger(gesture);
        }

        /// <summary>True if these characters have the gesture. Asked of the animator once per gesture, then remembered.</summary>
        public bool Knows(AnimationType gesture)
        {
            if (!KnownGestures.TryGetValue((int)gesture, out bool known))
            {
                known = Character.animator.HasParameter(gesture.ToString());
                KnownGestures[(int)gesture] = known;
                if (!known)
                {
                    ActorPool.Logger?.Warn($"People: street characters have no '{gesture}' gesture; another is used instead.");
                }
            }
            return known;
        }

        /// <summary>Switches animation and rendering off for scenes that are far away or out of view.</summary>
        public void SetVisible(bool visible)
        {
            ThirdPersonCharacter character = Character;
            // Showing someone switches their animator back on, which a ragdoll must not have.
            if (character.visible == visible || Limp)
            {
                return;
            }
            // The game's switch assumes all three exist; a character whose look failed to build has no merged mesh.
            if (character.animator == null || character.shadowCaster == null
                || character.appearanceSetter == null || character.appearanceSetter.lastMergedMeshRenderer == null)
            {
                return;
            }
            character.OnVisibleChanged(visible);
            if (Kit != null)
            {
                Kit.Show(visible);
            }
            if (_clips != null)
            {
                _clips.Show(visible);
            }
            if (visible && Kneeling)
            {
                _kneel.Set(character);   // being shown switched the animator back on
            }
        }

        /// <summary>Puts the actor on the navigation mesh at a point, free to walk. False if the point is not on it.</summary>
        internal bool Place(Vector3 position, bool onRoad)
        {
            Rise();
            Unconvey(walksOn: false);
            _standing = false;
            NavMeshAgent agent = Character.navmeshAgent;
            agent.agentTypeID = onRoad ? NavMeshHelper.PlayerAgentTypeId : NavMeshHelper.NpcAgentTypeId;
            if (!agent.enabled)
            {
                // There first, then the agent: switched on where the character was shelved (off the mesh) the
                // engine logs "Failed to create agent because it is not close enough to the NavMesh" every time.
                Character.transform.position = position;
            }
            agent.enabled = true;
            Character.capsuleCollider.enabled = true;
            bool placed = agent.Warp(position) && agent.isOnNavMesh;
            Character.LookTarget = Vector3.zero;
            Character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            Tune(Pace.Walk);
            return placed;
        }

        internal void ResetForReuse()
        {
            Rise();
            Unconvey(walksOn: false);
            RagdollSystem.Release(Character);   // whoever is let go while lying in the road is whole again first
            EndClips();
            SetVisible(true);
            ClearPose();
            // Whatever posed them by hand (a stretcher's passenger) and did not get to say so: animated again.
            if (Character.visible && Character.animator != null && !Character.animator.enabled && !Limp)
            {
                Character.animator.enabled = true;
            }
            Halt();
            Tune(Pace.Walk);
            _probing = false;
            Character.LookTarget = Vector3.zero;
            NotifyTrafficColliderGone();
            CarryBag(false);
            HiVis(false);
        }

        private void MakeMobile()
        {
            if (!_standing)
            {
                return;
            }
            _standing = false;
            Character.LookTarget = Vector3.zero;
            Character.capsuleCollider.enabled = true;
            Character.navmeshAgent.enabled = true;
            Character.navmeshAgent.Warp(Character.transform.position);
        }

        // A traffic car that has stopped for this person keeps waiting unless it is told the collider went away.
        private void NotifyTrafficColliderGone()
        {
            if (Character.capsuleCollider != null && TrafficManager.HasInstance && TrafficManager.IsInitialized)
            {
                Manager.TriggerColliderRemovedEvent(Character.capsuleCollider);
            }
        }
    }
}
