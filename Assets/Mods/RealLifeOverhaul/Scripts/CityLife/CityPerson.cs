using System.Collections.Generic;
using BigAmbitions.Characters;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the people of the living city: a thin wrapper over the game's own character that
    /// gives an activity what it needs (stand at a spot, walk, play a clip, hold a tool) and
    /// keeps the promises every activity must keep:
    ///
    /// - They only ever move by their navigation agent, with the game's walk animation; a clip
    ///   never moves them. While they stand, the agent stays on (stopped, top priority), so the
    ///   game's pedestrians and the player walk round them instead of through them.
    /// - A tool is in their hand only at the grip the clip being played was made with; the clip
    ///   and the tool are asked for together (<see cref="Work"/>), and if the tool cannot be had
    ///   the clip is not played.
    /// - Hit by a car, they drop what they hold where they stand and are handed to the mod's
    ///   ragdoll and casualty system whole (<see cref="Surrender"/>).
    ///
    /// Obtained from and returned to <see cref="CityPeople"/>; never created directly.
    /// </summary>
    public sealed class CityPerson
    {
        private const int Hands = 2;                // tool slots: right hand, left hand
        private const int StationaryPriority = 0;   // other agents give way to somebody who is working

        /// <summary>What an outfit recoloured on this character, put back before the next outfit. The pool's business.</summary>
        internal readonly List<CityOutfits.Dye> Dyes = new List<CityOutfits.Dye>(4);

        /// <summary>This person as the casualty system knows people: what lets a car strike hand them over.</summary>
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

        /// <summary>A civilian who has been seen in this look: the pool gives them a new one before they are used again (a thrifty pool: once the look is worn, quietly, on the shelf).</summary>
        internal bool Stale { get; set; }

        /// <summary>How many times they have been out in the look they have, when it was put on and when they last came back (unscaled seconds).</summary>
        internal int Uses;
        internal float DressedAt;
        internal float ReleasedAt;

        public bool Valid => Character != null;
        public Vector3 Position => Character.transform.position;
        public Vector3 Forward => Character.transform.forward;

        /// <summary>
        /// True once this person is no longer the activity's: knocked down by a car and in the
        /// casualty system's hands, or destroyed with the scene. The activity must not touch them again.
        /// </summary>
        public bool Lost => !Valid || !InUse || Taken;

        /// <summary>True while the casualty system has them (knocked down, lying, being seen to, walking off hurt).</summary>
        internal bool Taken => Valid && (Actor.Lease != _lease || RagdollSystem.Holds(Character));

        /// <summary>The name of the mod clip being played, or null while the game's own animation has them.</summary>
        public string Playing { get; private set; }

        /// <summary>The name of the clip on the masked layer (arms, one arm, upper body), or null.</summary>
        public string PlayingOver
        {
            get => _player != null && _player.Over != null ? _over : null;
            private set => _over = value;
        }

        /// <summary>True when the masked layer has nothing on it any more (a one-off there has run out).</summary>
        public bool OverDone => _player == null || _player.Over == null;

        /// <summary>How fast they are moving over the ground, metres a second.</summary>
        public float Speed => Character.navmeshAgent != null && Character.navmeshAgent.enabled ? Character.navmeshAgent.velocity.magnitude : 0f;

        /// <summary>True when a clip that plays once has run out (the character is back with the game's animation, or holding its last pose).</summary>
        public bool ClipOver => _player == null || _player.Current == null || !_player.Active;

        // ---------------------------------------------------------------- where they are

        /// <summary>Puts them on the pavement at a spot, facing a point, standing. False if the spot is not on the navigation mesh.</summary>
        internal bool Place(Vector3 spot, Vector3 face)
        {
            NavMeshAgent agent = Character.navmeshAgent;
            agent.agentTypeID = NavMeshHelper.NpcAgentTypeId;
            if (!agent.enabled)
            {
                // There first, then the agent: switched on at the shelf (off the mesh) the engine logs a warning every time.
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

        /// <summary>Stops where they are and stays: an obstacle other people walk round.</summary>
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

        // ---------------------------------------------------------------- sitting (People/Seating)

        private Transform _seat;            // the game's own sitting position they are on, or null
        private Vector3 _stoodAt;           // where they stood before they sat down: on the navigation mesh, and where they stand up to
        private Quaternion _stoodFacing;

        /// <summary>True while they sit on one of the game's seats.</summary>
        public bool Seated => _seat != null;

        /// <summary>
        /// Sits them on one of the game's own sitting positions (a chair, a sofa, a street bench: <see cref="Seating"/> finds
        /// them) exactly as the game seats its own people (<c>ThirdPersonCharacter.SitOnChair</c>): the game's sitting pose,
        /// made for its seats; the navigation agent switched off, since a seat is not on the mesh; the seat told that it is
        /// taken, so the game offers it to nobody else. Whatever they held is the caller's to put away first. False, and
        /// nothing changed, if they cannot (lost, seated already, no such pose on this character).
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
        /// Stands them up where they stood before they sat, by the game's own way off a seat (<c>ThirdPersonCharacter.Reset</c>:
        /// the agent on again, the seat told that it is free, the pose off). Safe at any time, seated or not; every way a
        /// person is moved, played or put away goes through it, so nobody is ever left sitting on nothing.
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
            // There first, then the agent: switched on at the seat (off the mesh) the engine logs a warning.
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

        /// <summary>Walks or jogs to a point on the navigation mesh. Any clip is ended first: nobody slides in a working pose.</summary>
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
        /// Walks up to (or back from) somebody so as to stop <paramref name="gap"/> metres from them on the line between
        /// the two: the distance a clip made for two was made at (a handshake 0.80 m, a toast 1.05 m, the rings 1.10 m).
        /// The agent's own stopping distance is allowed for. Afterwards check <see cref="Arrived"/>, <see cref="Speed"/>
        /// and the gap that was really reached before playing anything: an agent stops where it can.
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
            // Coming nearer the agent stops its stopping distance short of the goal; backing off it walks that far short too.
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

        /// <summary>True if they are within a distance of a point (flat).</summary>
        public bool Near(Vector3 point, float within)
        {
            return CitySpots.Flat(Position - point) <= within * within;
        }

        /// <summary>Turns them on the spot to face a point over a moment, as somebody who has just walked up does. In view, this is the one to use.</summary>
        public void Face(Vector3 point)
        {
            CityLifeDirector.Face(this, point);
        }

        /// <summary>Turns them on the spot to face a point, at once. For people who are standing; done when they are placed, out of view.</summary>
        public void Turn(Vector3 face)
        {
            Vector3 flat = face - Character.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
            {
                Character.transform.rotation = Quaternion.LookRotation(flat);
            }
        }

        // ---------------------------------------------------------------- what they do

        /// <summary>
        /// Plays one of the mod's clips, and puts the tool that clip was made for in their hand at
        /// the clip's own grip. All or nothing: false (and nothing changed) if the clip or its
        /// prop cannot be had, or the character cannot play it.
        /// </summary>
        /// <param name="tool">False plays the clip with empty hands (or with what they already hold): for a clip whose tool is picked up part of the way through (see <see cref="Take"/>).</param>
        /// <param name="startAt">Seconds into the clip to start at: two people on the same loop need not move as one.</param>
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
            Rise();   // (a whole-body clip is made for somebody on their feet: nobody plays one sitting on a seat of the game's)
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
        /// Plays a clip on the masked layer: only the arms, one arm or the upper body are taken
        /// from it (the mask the clip was made for unless one is given), over the game's walk or
        /// idle or over a clip played with Work. Its tool goes in the hand as with Work. This is
        /// how somebody carries a box, a cup or an umbrella while walking: nothing of the legs is
        /// ours, so nothing slides.
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

        /// <summary>The masked layer fades out; what the hands held stays in them until put away or released.</summary>
        public void StopOver(float fade = 0.3f)
        {
            if (_player != null)
            {
                _player.StopOver(fade);
            }
            PlayingOver = null;
            if (Playing == null)
            {
                for (int hand = 0; hand < Hands; hand++)
                {
                    _held[hand].LetGo();   // nothing is played that holds anything with both hands
                }
            }
        }

        /// <summary>
        /// Puts a prop object that is already out (a cup on a counter) in the hand, at the grip of
        /// the clip named: for taking something that somebody else put down. False if the clip
        /// has no such tool or the hand cannot take it; the prop is then left where it is.
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
        /// Something that goes where they go without being in a hand: on the ground in front (a
        /// pushchair, a sack truck: bone null, the pose is in their own frame) or worn (a satchel
        /// on a bone). Two at most. It is given back with everything else.
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

        /// <summary>Lets go of something brought along: it stays where it is in the world and is the caller's again. Null if they have no such thing.</summary>
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

        /// <summary>Puts the tool of a clip in the hand without playing the clip: for a second tool (the other hand's) that a later clip assumes.</summary>
        public bool Carry(string clipName, CityProps props)
        {
            if (Lost || !ClipLibrary.TryGet(clipName, out CityClip clip) || !clip.HasTool || !Hold(clip, props))
            {
                return false;
            }
            _held[clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1].LetGo();   // carried in the one hand until its own clip plays
            return true;
        }

        /// <summary>Back to the game's own animation (its idle, or walking).</summary>
        public void Idle(float fade = 0.35f)
        {
            if (_player != null)
            {
                _player.Stop(fade);
            }
            Playing = null;
            if (PlayingOver != null)
            {
                return;   // the masked layer still holds what it holds, with both hands if that is how
            }
            for (int hand = 0; hand < Hands; hand++)
            {
                _held[hand].LetGo();   // whatever they still hold is carried in one hand
            }
        }

        /// <summary>Holds one of the game's own looping poses (on the phone, texting); handObject is an optional game prop name.</summary>
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

        /// <summary>Takes the tool out of a hand and gives it to the caller (to stand on a counter, say). Null if that hand is empty.</summary>
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
        /// About to be thrown down by a car: the clip ends at once, and whatever they hold falls
        /// to the ground where it is and becomes the activity's to clear up (added to
        /// <paramref name="dropped"/>). After this the casualty system may have them.
        /// </summary>
        internal void Surrender(List<Transform> dropped)
        {
            Rise();
            if (_player != null)
            {
                _player.Dispose();
            }
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
                    // On its wheels, upright, where it was.
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
                // Lying on the ground under where it was held, along the way it pointed.
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

        /// <summary>Whole again and with empty hands, for the next activity (or for the shelf).</summary>
        internal void Reset(CityProps props)
        {
            if (!Valid)
            {
                _seat = null;
                return;
            }
            Rise();
            if (_player != null)
            {
                _player.Dispose();
            }
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
                _held[hand].Regrip(clip.Grip);   // already in that hand, at the grip every clip made for this tool shares; the other hand may come or go
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
            Transform before = Release(hand == 0);
            if (before != null)
            {
                props.Return(before);
            }
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
