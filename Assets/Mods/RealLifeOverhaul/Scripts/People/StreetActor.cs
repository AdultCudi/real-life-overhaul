using System.Collections.Generic;
using BigAmbitions.Characters;
using Extensions;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>How a person of the mod's is dressed: at random, or in a uniform.</summary>
    public enum ActorLook { Civilian, Officer, Paramedic }

    /// <summary>How fast a person of the mod's is sent somewhere.</summary>
    public enum Pace { Walk, Jog, Run }

    /// <summary>
    /// One mod-controlled person on the street: a thin wrapper over the game's character with what scenes need (walk, run,
    /// stand, pose, gesture). Handed out and taken back by <see cref="ActorPool"/>; scenes hold an <see cref="ActorRole"/>,
    /// which knows when the person behind it has gone to someone else.
    /// </summary>
    public sealed partial class StreetActor
    {
        private const float GroundSnap = 1.2f;   // metres: a standing person this far from the ground is put on it

        private static readonly int AnimationSpeedId = Animator.StringToHash("AnimationSpeed");
        private static NavMeshQueryFilter _roadFilter = new NavMeshQueryFilter { agentTypeID = NavMeshHelper.PlayerAgentTypeId, areaMask = NavMesh.AllAreas };

        private static readonly Dictionary<int, bool> KnownPoses = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> KnownGestures = new Dictionary<int, bool>();

        // The game only walks its street characters, so their agents turn slowly and give way to everyone; a jogger or runner
        // turns faster, picks up speed sooner and is given way to. A walker keeps the game's own values.
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

        // The pool's business: what a uniform recoloured, its shirt, and its equipment (null until one is worn).
        internal readonly List<Uniforms.Dye> Dyes = new List<Uniforms.Dye>(4);
        internal Uniforms.Shirt Shirt;
        internal ServiceKit Kit;

        private readonly Vector3 _scale;   // the character's own scale, as the game made it

        /// <summary>How much bigger than the game built them this character stands (Stature); 1 but for officers and paramedics.</summary>
        internal float Size { get; private set; } = 1f;

        /// <summary>Scales the whole character about its feet, capsule and agent included. The pool's business.</summary>
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

        /// <summary>True for someone only there to be seen: the pool takes them back at once when a scene needs a person.</summary>
        public bool Yields { get; internal set; }

        /// <summary>A civilian whose look has been out often or long enough: the pool quietly gives them a new one (Looks).</summary>
        internal bool Stale { get; set; }

        /// <summary>How many times they have been out in the look they have, when it was put on and when they last came back (unscaled seconds).</summary>
        internal int Uses;
        internal float DressedAt;
        internal float ReleasedAt;

        public bool IsValid => Character != null;
        public Vector3 Position => Character.transform.position;

        /// <summary>False while the pool has their animation and rendering off (far away, or behind the camera).</summary>
        public bool Visible => Character.visible;

        /// <summary>True while this person is a ragdoll: physics has the body, and nothing here may move, hide or animate it.</summary>
        public bool Limp => RagdollSystem.Holds(Character);

        public static NavMeshQueryFilter RoadFilter => _roadFilter;

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

        /// <summary>Watches the first jogger or runner of a session for a moment and logs once how fast they got; a comparison otherwise.</summary>
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

        /// <summary>True once the engine has worked out the route last asked of <see cref="MoveTo"/>.</summary>
        public bool RouteKnown
        {
            get
            {
                NavMeshAgent agent = Character.navmeshAgent;
                return agent.enabled && agent.isOnNavMesh && !agent.pathPending;
            }
        }

        /// <summary>False when that route does not reach the place asked; the engine then stops them at the nearest point.</summary>
        public bool RouteWhole => Character.navmeshAgent.pathStatus == NavMeshPathStatus.PathComplete;

        public void Halt()
        {
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        /// <summary>Plants them at a spot facing a point, agent and collider off, as the game places its standing figures.</summary>
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

        /// <summary>Turns someone standing to face a point; the game turns a character to its look target only with its agent off.</summary>
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

        /// <summary>Kneels over whatever is in front of them until <see cref="ClearPose"/>: a kneeling clip, else the hand-set pose.</summary>
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
            KneelByHand();
        }

        /// <summary>
        /// The kneel set by hand (<see cref="KneelPose"/>), the animator off; also what puts it back after <see cref="ClearPose"/>
        /// took it off for something that then did not happen (a car that could not throw them down after all).
        /// </summary>
        public void KneelByHand()
        {
            if (Limp || Kneeling)
            {
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

        public void CarryBag(bool carry)
        {
            if (Kit != null)
            {
                Kit.Carry(carry);
            }
        }

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

        public bool Seated => _seat != null;

        /// <summary>Sits them on a seat as the game seats its own people; false, with nothing changed, if they cannot.</summary>
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

        /// <summary>On their feet where they stood before sitting; safe at any time, and called by every way they are moved.</summary>
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

        public bool Kneeling => _kneel != null && _kneel.Active;

        /// <summary>
        /// Drawn and kneeling by hand: the animator is off only for the pose, and <see cref="ClearPose"/> switches it on again
        /// (KneelPose.Clear). Someone hidden has it off for being hidden, and is not counted.
        /// </summary>
        public bool PosedByHand => Kneeling && Character != null && Character.visible;

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

        /// <summary>True once their route has brought them to within a distance of its end, however slowly they are still closing.</summary>
        public bool Within(float slack)
        {
            NavMeshAgent agent = Character.navmeshAgent;
            return !agent.enabled || !agent.isOnNavMesh
                   || (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + slack));
        }

        private static ThirdPersonCharacter.WalkingSpeed ToGame(Pace pace)
        {
            return pace == Pace.Run ? ThirdPersonCharacter.WalkingSpeed.Run
                : pace == Pace.Jog ? ThirdPersonCharacter.WalkingSpeed.Jog
                : ThirdPersonCharacter.WalkingSpeed.Walk;
        }

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


        /// <summary>Plays a one-off gesture without asking its length, which is what the game's own call spends its time on.</summary>
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

        /// <summary>Puts them on the navigation mesh at a point, free to walk; false if the point is not on it.</summary>
        internal bool Place(Vector3 position, bool onRoad)
        {
            Rise();
            Unconvey(walksOn: false);
            _standing = false;
            NavMeshAgent agent = Character.navmeshAgent;
            agent.agentTypeID = onRoad ? NavMeshHelper.PlayerAgentTypeId : NavMeshHelper.NpcAgentTypeId;
            if (!agent.enabled)
            {
                // There first, then the agent: switched on off the mesh, the engine logs "Failed to create agent".
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
