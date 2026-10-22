using System.Collections.Generic;
using BigAmbitions.Characters;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the living city's people: a thin wrapper over the game's character that lets an activity place, walk, play
    /// clips and hand tools, and keeps three promises. They move only by their navigation agent with the game's walk, and
    /// while standing the agent stays on (stopped, top priority) so others walk round them. A tool is in a hand only at the
    /// grip of the clip playing, asked for with it (<see cref="Work"/>): no tool, no clip. Hit by a car, they drop what
    /// they hold and go whole to the casualty system (<see cref="Surrender"/>). Taken from and returned to
    /// <see cref="CityPeople"/>.
    /// </summary>
    public sealed class CityPerson
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

        /// <summary>
        /// True once they are no longer the activity's: knocked down and with the casualty system, or destroyed with the scene.
        /// The activity must not touch them again.
        /// </summary>
        public bool Lost => !Valid || !InUse || Taken;

        /// <summary>True while the casualty system has them: knocked down, lying, seen to, walking off hurt.</summary>
        internal bool Taken => Valid && (Actor.Lease != _lease || RagdollSystem.Holds(Character));

        /// <summary>The mod clip being played, or null while the game's own animation has them.</summary>
        public string Playing { get; private set; }

        /// <summary>The clip on the masked layer, or null.</summary>
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

        /// <summary>True while they sit on one of the game's seats.</summary>
        public bool Seated => _seat != null;

        /// <summary>
        /// Sits them on one of the game's sitting positions (<see cref="Seating"/> finds them) as the game seats its own people
        /// (<c>ThirdPersonCharacter.SitOnChair</c>): its sitting pose, the agent off, the seat taken for anyone else. Whatever
        /// they hold is the caller's to put away first. False, with nothing changed, if they cannot.
        /// </summary>
        public bool Sit(Transform seat)
        {
            if (Lost || seat == null || _seat != null || !Character.animator.HasParameter(PermanentAnimationType.Sitting.ToString()))
            {
                return false;
            }
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

        /// <summary>
        /// Stands them up where they stood before they sat, by the game's own way off a seat (<c>ThirdPersonCharacter.Reset</c>).
        /// Safe at any time; every way a person is moved, played or put away goes through it, so nobody is left sitting on
        /// nothing.
        /// </summary>
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

        /// <summary>Walks or jogs to a point on the navigation mesh, ending any clip first: nobody slides in a working pose.</summary>
        public bool WalkTo(Vector3 destination, bool jog = false)
        {
            if (Lost)
            {
                return false;
            }
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

        /// <summary>
        /// Walks up to or back from somebody so as to stop <paramref name="gap"/> metres from them, the distance a clip for two
        /// was made at (a handshake 0.80 m, a toast 1.05 m), allowing for the agent's stopping distance. An agent stops where it
        /// can: check <see cref="Arrived"/>, <see cref="Speed"/> and the gap reached before playing anything.
        /// </summary>
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

        /// <summary>Whether they are within a flat distance of a point.</summary>
        public bool Near(Vector3 point, float within)
        {
            return CitySpots.Flat(Position - point) <= within * within;
        }

        /// <summary>Turns them to face a point over a moment, as somebody who has just walked up does: the one to use in view.</summary>
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

        /// <summary>
        /// Plays one of the mod's clips with the tool it was made for in their hand at its grip. All or nothing: false, with
        /// nothing changed, if the clip or its prop cannot be had.
        /// </summary>
        /// <param name="tool">False plays it with the hands as they are, for a clip whose tool is picked up part-way (<see cref="Take"/>).</param>
        /// <param name="startAt">Seconds into the clip: two people on the same loop need not move as one.</param>
        public bool Work(string clipName, CityProps props, float fade = 0.35f, ClipEnd end = ClipEnd.Return, bool tool = true, float startAt = 0f)
        {
            if (Lost || _player == null || !_player.Usable || !ClipLibrary.TryGet(clipName, out CityClip clip))
            {
                return false;
            }
            if (tool && clip.HasTool && !Hold(clip, props))
            {
                return false;
            }
            Rise();   // a whole-body clip is made for somebody on their feet
            ClearPose();
            if (!_player.Play(clip.Clip, fade, 1f, end, startAt))
            {
                return false;
            }
            Playing = clipName;
            ClipLibrary.Played(clipName, "a street character");
            return true;
        }

        /// <summary>
        /// Plays a clip on the masked layer (the clip's own mask unless one is given) over the game's walk or idle or a clip
        /// played with Work, its tool in the hand as with Work: how somebody carries a box or an umbrella while walking.
        /// </summary>
        public bool WorkOver(string clipName, CityProps props, float fade = 0.3f, ClipEnd end = ClipEnd.Return, BodyMask mask = BodyMask.Full, bool tool = true)
        {
            if (Lost || _player == null || !_player.Usable || !ClipLibrary.TryGet(clipName, out CityClip clip))
            {
                return false;
            }
            if (mask == BodyMask.Full)
            {
                mask = clip.Mask == BodyMask.Full ? BodyMask.Upper : clip.Mask;
            }
            if (tool && clip.HasTool && !Hold(clip, props))
            {
                return false;
            }
            if (!_player.PlayOver(clip.Clip, mask, fade, 1f, end))
            {
                return false;
            }
            PlayingOver = clipName;
            ClipLibrary.Played(clipName, "a street character");
            return true;
        }

        /// <summary>The masked layer fades out; what the hands hold stays until put away or released.</summary>
        public void StopOver(float fade = 0.3f)
        {
            _player?.StopOver(fade);
            PlayingOver = null;
            if (Playing == null)
            {
                for (int hand = 0; hand < Hands; hand++)
                {
                    _held[hand].LetGo();   // nothing playing holds anything with both hands
                }
            }
        }

        /// <summary>
        /// Puts a prop that is already out (a cup on a counter) in the hand at the named clip's grip; false, with the prop left
        /// where it is, if the clip has no such tool or the hand cannot take it.
        /// </summary>
        public bool Take(Transform tool, string clipName, CityProps props)
        {
            if (Lost || tool == null || !ClipLibrary.TryGet(clipName, out CityClip clip) || !clip.HasTool || clip.Prop != tool.name)
            {
                return false;
            }
            int hand = clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1;
            Transform before = Release(hand == 0);
            if (before != null && before != tool && props != null)
            {
                props.Return(before);
            }
            if (!_held[hand].Attach(Character.animator, tool, clip.Grip))
            {
                return false;
            }
            _tools[hand] = tool;
            Show(tool, _toolsShown);
            return true;
        }

        /// <summary>
        /// Something that goes with them without being in a hand, two at most: on the ground in front (a pushchair; bone null,
        /// the pose in their own frame) or worn on a bone (a satchel). Given back with everything else.
        /// </summary>
        public bool Bring(Transform prop, HumanBodyBones? bone, Vector3 position, Quaternion rotation)
        {
            if (Lost || prop == null)
            {
                return false;
            }
            Transform parent = bone.HasValue ? Character.animator.GetBoneTransform(bone.Value) : Character.transform;
            int slot = _with[0] == null ? 0 : _with[1] == null ? 1 : -1;
            if (parent == null || slot < 0)
            {
                return false;
            }
            Vector3 scale = parent.lossyScale;
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f)
            {
                return false;
            }
            prop.SetParent(parent, false);
            prop.localPosition = new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z);
            prop.localRotation = rotation;
            prop.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            _with[slot] = prop;
            Show(prop, _toolsShown);
            return true;
        }

        /// <summary>Lets go of something brought along, where it is now; null if they have no such thing.</summary>
        public Transform Leave(string id)
        {
            for (int i = 0; i < _with.Length; i++)
            {
                Transform prop = _with[i];
                if (prop != null && prop.name == id)
                {
                    _with[i] = null;
                    prop.SetParent(null, true);
                    prop.localScale = Vector3.one;
                    Show(prop, true);
                    return prop;
                }
            }
            return null;
        }

        /// <summary>Puts a clip's tool in the hand without playing the clip, for a second tool a later clip assumes.</summary>
        public bool Carry(string clipName, CityProps props)
        {
            if (Lost || !ClipLibrary.TryGet(clipName, out CityClip clip) || !clip.HasTool || !Hold(clip, props))
            {
                return false;
            }
            _held[clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1].LetGo();   // carried in the one hand until its own clip plays
            return true;
        }

        /// <summary>Back to the game's own animation, its idle or walking.</summary>
        public void Idle(float fade = 0.35f)
        {
            _player?.Stop(fade);
            Playing = null;
            if (PlayingOver != null)
            {
                return;   // the masked layer still holds what it holds, with both hands if so
            }
            for (int hand = 0; hand < Hands; hand++)
            {
                _held[hand].LetGo();   // whatever they still hold is carried in one hand
            }
        }

        /// <summary>
        /// Sets the walking clip's speed from how fast they really go, so the planted foot stays put; clipSpeed is
        /// <see cref="CityClip.Speed"/>. From the owner's tick, never per frame.
        /// </summary>
        public void KeepStep(float clipSpeed)
        {
            if (_player != null && Playing != null && !Lost)
            {
                _player.MatchSpeed(Speed, clipSpeed);
            }
        }

        /// <summary>Holds one of the game's looping poses (on the phone, texting), with an optional game prop in the hand.</summary>
        public bool Pose(PermanentAnimationType pose, string handObject = null)
        {
            if (Lost || !Character.animator.HasParameter(pose.ToString()))
            {
                return false;
            }
            Idle();
            ClearPose();
            Character.animator.SetBool(pose);
            if (!string.IsNullOrEmpty(handObject))
            {
                Character.AddHandObject(handObject);
            }
            _pose = pose;
            _hasPose = true;
            return true;
        }

        public void ClearPose()
        {
            if (!_hasPose)
            {
                return;
            }
            _hasPose = false;
            Character.animator.SetBool(_pose, false);
            Character.RemoveHandObject();
        }

        /// <summary>Takes the tool out of a hand and gives it to the caller; null if that hand is empty.</summary>
        public Transform Release(bool rightHand)
        {
            int hand = rightHand ? 0 : 1;
            Transform tool = _tools[hand];
            if (tool != null)
            {
                _held[hand].Detach(null);
                Show(tool, true);
                _tools[hand] = null;
            }
            return tool;
        }

        /// <summary>Everything out of their hands and back on the shelf.</summary>
        public void PutAway(CityProps props)
        {
            for (int hand = 0; hand < Hands; hand++)
            {
                Transform tool = Release(hand == 0);
                if (tool != null && props != null)
                {
                    props.Return(tool);
                }
            }
            for (int i = 0; i < _with.Length; i++)
            {
                Transform prop = _with[i];
                _with[i] = null;
                if (prop != null)
                {
                    prop.SetParent(null, true);
                    props?.Return(prop);
                }
            }
        }

        /// <summary>
        /// About to be thrown down by a car: the clip ends at once, and whatever they hold falls where it is and is added to
        /// <paramref name="dropped"/> for the activity to clear up. The casualty system may have them after this.
        /// </summary>
        internal void Surrender(List<Transform> dropped)
        {
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
                return;
            }
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

        private bool Hold(CityClip clip, CityProps props)
        {
            int hand = clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1;
            if (_tools[hand] != null && _tools[hand].name == clip.Prop)
            {
                _held[hand].Regrip(clip.Grip);   // already in that hand at the grip every clip for this tool shares; the other hand may come or go
                return true;
            }
            if (props == null)
            {
                return false;
            }
            Transform tool = props.Take(clip.Prop);
            if (tool == null)
            {
                return false;
            }
            props.Return(Release(hand == 0));
            if (!_held[hand].Attach(Character.animator, tool, clip.Grip))
            {
                props.Return(tool);
                return false;
            }
            _tools[hand] = tool;
            Show(tool, _toolsShown);
            return true;
        }

        private static void Show(Transform tool, bool visible)
        {
            if (tool != null && tool.TryGetComponent(out Renderer renderer))
            {
                renderer.enabled = visible;
            }
        }
    }
}
