using System.Collections.Generic;
using BigAmbitions.Characters;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the living city's people, a thin wrapper over the game's character that keeps three promises: they move only by
    /// their navigation agent, standing as an obstacle others walk round; a tool is in a hand only with the clip made for it
    /// (<see cref="Work"/>); hit by a car they drop it and go whole to the casualty system (<see cref="Surrender"/>).
    /// </summary>
    public sealed partial class CityPerson
    {
        private const int Hands = 2;                // tool slots: right hand, left hand
        private const int StationaryPriority = 0;   // other agents give way to somebody working

        /// <summary>What an outfit recoloured on this character, put back by the pool before the next outfit.</summary>
        internal readonly List<CityOutfits.Dye> Dyes = new List<CityOutfits.Dye>(4);

        /// <summary>This person as the casualty system knows people, so a car strike can hand them over.</summary>
        internal readonly StreetActor Actor;

        private readonly HeldTool[] _held = { new HeldTool(), new HeldTool() };
        private readonly Transform[] _tools = new Transform[Hands];
        private readonly ActivityAnimator _player;
        private readonly int _walkingPriority;
        private PermanentAnimationType _pose;
        private bool _hasPose;
        private readonly Transform[] _with = new Transform[2];   // what travels with them but is not in a hand: a pushchair, a satchel
        private string _over;
        private int _lease;
        private bool _toolsShown = true;

        internal CityPerson(ThirdPersonCharacter character)
        {
            Character = character;
            Actor = new StreetActor(character);
            _player = ActivityAnimators.For(character.animator);
            _walkingPriority = character.navmeshAgent != null ? character.navmeshAgent.avoidancePriority : 50;
        }

        public ThirdPersonCharacter Character { get; }
        public CityLook Look { get; internal set; }
        public bool InUse { get; internal set; }

        /// <summary>A civilian seen in this look: the pool gives them a new one before they are used again.</summary>
        internal bool Stale { get; set; }

        /// <summary>Times out in this look, when it was put on and when they last came back (unscaled seconds).</summary>
        internal int Uses;
        internal float DressedAt;
        internal float ReleasedAt;

        public bool Valid => Character != null;
        public Vector3 Position => Character.transform.position;
        public Vector3 Forward => Character.transform.forward;

        /// <summary>True once they are no longer the activity's to touch: with the casualty system, or destroyed with the scene.</summary>
        public bool Lost => !Valid || !InUse || Taken;

        /// <summary>True while the casualty system has them: knocked down, lying, seen to, walking off hurt.</summary>
        internal bool Taken => Valid && (Actor.Lease != _lease || RagdollSystem.Holds(Character));

        /// <summary>The mod clip being played, or null while the game's own animation has them.</summary>
        public string Playing { get; private set; }

        public string PlayingOver
        {
            get => _player != null && _player.Over != null ? _over : null;
            private set => _over = value;
        }

        /// <summary>Their speed over the ground, metres a second.</summary>
        public float Speed => Character.navmeshAgent != null && Character.navmeshAgent.enabled ? Character.navmeshAgent.velocity.magnitude : 0f;

        /// <summary>True once a one-off clip has run out: back with the game's animation, or holding its last pose.</summary>
        public bool ClipOver => _player == null || _player.Current == null || !_player.Active;

        /// <summary>Puts them standing on the pavement at a spot, facing a point; false if the spot is off the navigation mesh.</summary>
        internal bool Place(Vector3 spot, Vector3 face)
        {
            NavMeshAgent agent = Character.navmeshAgent;
            agent.agentTypeID = NavMeshHelper.NpcAgentTypeId;
            if (!agent.enabled)
            {
                // There first, then the agent: switched on at the shelf, off the mesh, the engine logs a warning each time.
                Character.transform.position = spot;
            }
            agent.enabled = true;
            Character.capsuleCollider.enabled = true;
            if (!agent.Warp(spot) || !agent.isOnNavMesh)
            {
                return false;
            }
            Character.LookTarget = Vector3.zero;
            Character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            Turn(face);
            Stand();
            _lease = Actor.Lease;
            return true;
        }

        /// <summary>Stops where they are and stays, an obstacle others walk round.</summary>
        public void Stand()
        {
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.avoidancePriority = StationaryPriority;
            }
        }

        private Transform _seat;            // the game's sitting position they are on, or null
        private Vector3 _stoodAt;           // on the navigation mesh, where they stand up to
        private Quaternion _stoodFacing;

        public bool Seated => _seat != null;

        /// <summary>Sits them on one of the game's seats (<see cref="Seating"/>) as the game does; the caller puts away what they hold first.</summary>
        public bool Sit(Transform seat)
        {
            if (Lost || seat == null || _seat != null || !Character.animator.HasParameter(PermanentAnimationType.Sitting.ToString()))
            {
                return false;
            }
            LeaveTable();
            Idle(0.15f);
            ClearPose();
            StopOver(0.15f);
            CityLifeDirector.StopFacing(this);
            Stand();
            _stoodAt = Character.transform.position;
            _stoodFacing = Character.transform.rotation;
            _seat = seat;
            Character.SitOnChair(seat);
            return true;
        }

        /// <summary>Lays them on a bed as the game lays the player down to sleep; the bed is not marked taken, so the player can still use it.</summary>
        public bool Lie(EntityController bed, Transform at)
        {
            if (Lost || bed == null || at == null || _seat != null || !Character.animator.HasParameter(PermanentAnimationType.Laying.ToString()))
            {
                return false;
            }
            LeaveTable();
            Idle(0.15f);
            ClearPose();
            StopOver(0.15f);
            CityLifeDirector.StopFacing(this);
            Stand();
            _stoodAt = Character.transform.position;
            _stoodFacing = Character.transform.rotation;
            _seat = at;
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
            agent.enabled = false;   // on the bed, off the mesh: as the game's SitOnChair does for a seat
            Character.LayOnBed(bed, at);
            return true;
        }

        /// <summary>Stands them up where they stood before they sat; safe at any time, and every way they are moved goes through it.</summary>
        public void Rise()
        {
            if (_seat == null)
            {
                return;
            }
            _seat = null;
            if (!Valid)
            {
                return;
            }
            // There first, then the agent: switched on at the seat, off the mesh, the engine logs a warning.
            Character.transform.SetPositionAndRotation(_stoodAt, _stoodFacing);
            Character.Reset();
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled)
            {
                agent.Warp(_stoodAt);
            }
            Character.transform.rotation = _stoodFacing;
            Stand();
        }

        private bool _atTable;
        private Vector3 _tableCentre;
        private ObstacleAvoidanceType _ownAvoidance;
        private bool _ownTurning;

        // The table's navigation obstacle reaches inside the agent, a stopped agent still steers off an obstacle it overlaps,
        // and the game turns its people the way their agent's velocity points (ThirdPersonCharacter.HandleNpcMovement): so
        // their own avoidance and the agent's turning are off while they sit, and given back on every way off the stool.
        /// <summary>Keeps them on their spot facing a table's centre for a clip made sitting at it; <see cref="KeepAtTable"/> mends any turn.</summary>
        public void AtTable(Vector3 centre)
        {
            if (Lost)
            {
                return;
            }
            NavMeshAgent agent = Character.navmeshAgent;
            if (!_atTable)
            {
                _ownAvoidance = agent.obstacleAvoidanceType;
                _ownTurning = agent.updateRotation;
            }
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.updateRotation = false;
            CityLifeDirector.StopFacing(this);
            _atTable = true;
            _tableCentre = centre;
            Turn(centre);
        }

        /// <summary>Once a second at a table: the degrees they faced off its centre, turned back if past TableSeat.FacedWithin; 0 if not seated.</summary>
        public float KeepAtTable(bool inView)
        {
            if (!_atTable || Lost)
            {
                return 0f;
            }
            Transform body = Character.transform;
            Vector3 forward = body.forward;
            Vector3 at = body.position;
            float off = TableSeat.Off(forward.x, forward.z, at.x, at.z, _tableCentre.x, _tableCentre.z);
            if (!TableSeat.Faces(off))
            {
                if (inView)
                {
                    Face(_tableCentre);
                }
                else
                {
                    Turn(_tableCentre);
                }
            }
            return off;
        }

        private void LeaveTable()
        {
            if (!_atTable)
            {
                return;
            }
            _atTable = false;
            if (Valid)
            {
                NavMeshAgent agent = Character.navmeshAgent;
                agent.obstacleAvoidanceType = _ownAvoidance;
                agent.updateRotation = _ownTurning;
            }
        }

        /// <summary>Walks or jogs to a point on the navigation mesh, ending any clip first: nobody slides in a working pose.</summary>
        public bool WalkTo(Vector3 destination, bool jog = false)
        {
            if (Lost)
            {
                return false;
            }
            LeaveTable();
            Rise();
            Idle(0.2f);
            NavMeshAgent agent = Character.navmeshAgent;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            CityLifeDirector.StopFacing(this);
            Character.SetWalkingSpeed(jog ? ThirdPersonCharacter.WalkingSpeed.Jog : ThirdPersonCharacter.WalkingSpeed.Walk);
            agent.avoidancePriority = _walkingPriority;
            agent.isStopped = false;
            return agent.SetDestination(destination);
        }

        /// <summary>Walks to stop gap metres from somebody, as a clip for two was made; check Arrived and the gap before playing anything.</summary>
        public bool StepTo(Vector3 other, float gap)
        {
            if (Lost)
            {
                return false;
            }
            Vector3 away = Position - other;
            away.y = 0f;
            float now = away.magnitude;
            NavMeshAgent agent = Character.navmeshAgent;
            if (now < 0.02f || agent == null)
            {
                return false;
            }
            // Coming nearer, the agent stops its stopping distance short; backing off, it walks that far short too.
            float stop = Mathf.Max(0f, agent.stoppingDistance);
            float aim = now > gap ? Mathf.Max(0.1f, gap - stop) : gap + stop;
            Vector3 goal = other + away * (aim / now);
            goal.y = Position.y;
            return WalkTo(goal);
        }

        public bool Arrived
        {
            get
            {
                NavMeshAgent agent = Character.navmeshAgent;
                return !agent.enabled || !agent.isOnNavMesh || (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.25f));
            }
        }

        public bool Near(Vector3 point, float within)
        {
            return CitySpots.Flat(Position - point) <= within * within;
        }

        /// <summary>Turns them to face a point over a moment, as somebody who has just walked up does: the one for in view.</summary>
        public void Face(Vector3 point)
        {
            CityLifeDirector.Face(this, point);
        }

        /// <summary>Turns them to face a point at once, for placing them out of view.</summary>
        public void Turn(Vector3 face)
        {
            Vector3 flat = face - Character.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
            {
                Character.transform.rotation = Quaternion.LookRotation(flat);
            }
        }

        /// <summary>About to be thrown down by a car: the clip ends and what they hold falls, added to dropped for the activity to clear up.</summary>
        internal void Surrender(List<Transform> dropped)
        {
            LeaveTable();
            Rise();
            _player?.Dispose();
            Playing = null;
            PlayingOver = null;
            CityLifeDirector.StopFacing(this);
            ClearPose();
            for (int i = 0; i < _with.Length; i++)
            {
                // What was pushed stays standing where it is; what was worn falls with the rest.
                Transform prop = _with[i];
                _with[i] = null;
                if (prop == null)
                {
                    continue;
                }
                bool worn = prop.parent != Character.transform;
                prop.SetParent(null, true);
                prop.localScale = Vector3.one;
                if (!worn)
                {
                    Vector3 stands = prop.position;
                    if (CitySpots.Ground(stands, out float road))
                    {
                        stands.y = road;
                    }
                    prop.SetPositionAndRotation(stands, Quaternion.Euler(0f, prop.eulerAngles.y, 0f));
                }
                else
                {
                    Vector3 lies = prop.position;
                    if (CitySpots.Ground(lies, out float under))
                    {
                        lies.y = under + 0.05f;
                    }
                    prop.SetPositionAndRotation(lies, Quaternion.Euler(90f, prop.eulerAngles.y, 0f));
                }
                dropped?.Add(prop);
            }
            for (int hand = 0; hand < Hands; hand++)
            {
                Transform tool = Release(hand == 0);
                if (tool == null)
                {
                    continue;
                }
                // On the ground under where it was held, along the way it pointed.
                Vector3 at = tool.position;
                if (CitySpots.Ground(at, out float ground))
                {
                    at.y = ground + 0.03f;
                }
                Vector3 along = tool.up;
                along.y = 0f;
                tool.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.down, along.sqrMagnitude > 0.01f ? along.normalized : Vector3.forward));
                dropped?.Add(tool);
            }
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.avoidancePriority = _walkingPriority;
            }
        }

        /// <summary>Switches animation and rendering off while they cannot be seen, tools included.</summary>
        internal void SetVisible(bool visible)
        {
            ThirdPersonCharacter character = Character;
            if (character.visible != visible && !RagdollSystem.Holds(character) && character.animator != null && character.shadowCaster != null
                && character.appearanceSetter != null && character.appearanceSetter.lastMergedMeshRenderer != null)
            {
                character.OnVisibleChanged(visible);
            }
            if (_toolsShown != visible)
            {
                _toolsShown = visible;
                for (int hand = 0; hand < Hands; hand++)
                {
                    Show(_tools[hand], visible);
                }
                Show(_with[0], visible);
                Show(_with[1], visible);
            }
        }

        /// <summary>Whole again with empty hands, for the next activity or the shelf.</summary>
        internal void Reset(CityProps props)
        {
            if (!Valid)
            {
                _seat = null;
                _atTable = false;
                return;
            }
            LeaveTable();
            Rise();
            _player?.Dispose();
            Playing = null;
            PlayingOver = null;
            CityLifeDirector.StopFacing(this);
            ClearPose();
            PutAway(props);
            SetVisible(true);
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
            }
            agent.avoidancePriority = _walkingPriority;
            Character.LookTarget = Vector3.zero;
        }
    }
}
