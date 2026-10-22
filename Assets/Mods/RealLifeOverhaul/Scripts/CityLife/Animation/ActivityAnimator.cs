using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace RealLifeOverhaul
{
    /// <summary>What happens when a clip that does not loop reaches its end.</summary>
    public enum ClipEnd { Return, Hold }

    /// <summary>
    /// The part of the body a clip played with <see cref="ActivityAnimator.PlayOver"/> takes, over whatever is underneath.
    /// Upper is trunk, head and arms (a gesture made standing); Arms, LeftArm and RightArm are arms and fingers only, for
    /// somebody walking.
    /// </summary>
    public enum BodyMask { Full, Upper, Arms, LeftArm, RightArm }

    /// <summary>
    /// Plays the mod's Humanoid clips on one of the game's characters without touching the game's animator controller. A
    /// small playable graph goes on the Animator the first time something plays: two clip slots into a mixer, and an output
    /// whose weight is how much of the pose is ours, faded in from whatever the controller is doing and out to hand the
    /// character back, when the graph is destroyed. The clips are baked into the pose, so the character is never moved.
    /// A second, masked layer (<see cref="PlayOver"/>) takes only the arms, one arm or the upper body, for carrying while
    /// the game walks them: its output starts from the pose already reached, and a small animation job copies in only the
    /// muscles the mask names (an AvatarMask on a layer mixer would reset the rest of the body).
    /// Cost: a loop that has faded in runs in the engine with the Animator and needs no script; script runs only while a
    /// weight changes or a one-off counts down (<see cref="ActivityAnimators"/> steps exactly those). Get one from
    /// <see cref="ActivityAnimators.For"/>.
    /// </summary>
    public sealed class ActivityAnimator
    {
        private const float Instant = 0.0001f;   // seconds: the shortest fade

        // One layer: two slots cross-faded, a weight, an end.
        private sealed class Layer
        {
            public readonly AnimationClipPlayable[] Slots = new AnimationClipPlayable[2];
            public readonly AnimationClip[] Clips = new AnimationClip[2];
            public readonly BodyMask[] Masks = new BodyMask[2];
            public AnimationPlayableOutput Output;
            public int Current = -1;        // the slot being faded towards
            public float Weight;            // how much of the pose is this layer's, now
            public float WeightGoal;
            public float WeightRate;        // per second
            public float Blend;             // 0 is all slot 0, 1 all slot 1
            public float BlendRate;   // per second
            public float Remaining = -1f;   // seconds of a one-off clip still to play; negative for a loop
            public float FadeOut;
            public float Speed = 1f;
            public ClipEnd End;

            public void Clear()
            {
                Slots[0] = default;
                Slots[1] = default;
                Clips[0] = null;
                Clips[1] = null;
                Current = -1;
                Weight = 0f;
                WeightGoal = 0f;
                Remaining = -1f;
                Speed = 1f;
            }
        }

        // Copies the muscles a mask names from each connected clip into the pose the stream already holds.
        private struct MaskedPoseJob : IAnimationJob
        {
            [ReadOnly] public NativeArray<MuscleHandle> First, Second;
            public float FirstWeight, SecondWeight;

            public void ProcessRootMotion(AnimationStream stream)
            {
            }

            public void ProcessAnimation(AnimationStream stream)
            {
                if (!stream.isHumanStream)
                {
                    return;
                }
                AnimationHumanStream pose = stream.AsHuman();
                Take(stream, pose, 0, First, FirstWeight);
                Take(stream, pose, 1, Second, SecondWeight);
            }

            private static void Take(AnimationStream stream, AnimationHumanStream pose, int input, NativeArray<MuscleHandle> muscles, float weight)
            {
                if (weight <= 0f || !muscles.IsCreated)
                {
                    return;
                }
                AnimationStream from = stream.GetInputStream(input);
                if (!from.isValid || !from.isHumanStream)
                {
                    return;
                }
                AnimationHumanStream source = from.AsHuman();
                for (int i = 0; i < muscles.Length; i++)
                {
                    MuscleHandle muscle = muscles[i];
                    float value = source.GetMuscle(muscle);
                    pose.SetMuscle(muscle, weight >= 1f ? value : Mathf.Lerp(pose.GetMuscle(muscle), value, weight));
                }
            }
        }

        private static readonly NativeArray<MuscleHandle>[] MuscleSets = new NativeArray<MuscleHandle>[5];

        private readonly Animator _animator;
        private readonly Layer _body = new Layer();
        private readonly Layer _over = new Layer();
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationScriptPlayable _overMixer;
        private MaskedPoseJob _overJob;
        private bool _overBuilt;

        internal ActivityAnimator(Animator animator)
        {
            _animator = animator;
        }

        public Animator Animator => _animator;

        /// <summary>False for a character these clips cannot play on; Play then does nothing.</summary>
        public bool Usable => _animator != null && _animator.isHuman && _animator.avatar != null && _animator.avatar.isValid;

        /// <summary>True from Play until the character has been handed back completely.</summary>
        public bool Active => _graph.IsValid();

        /// <summary>The clip being played or faded towards; null when the game's controller has the character.</summary>
        public AnimationClip Current => _body.Current >= 0 && _body.WeightGoal > 0f ? _body.Clips[_body.Current] : null;

        /// <summary>How much of the pose is this player's right now, 0 to 1.</summary>
        public float Weight => _body.Weight;

        /// <summary>The clip on the masked layer; null when there is none.</summary>
        public AnimationClip Over => _over.Current >= 0 && _over.WeightGoal > 0f ? _over.Clips[_over.Current] : null;

        /// <summary>The playback speed of the clip played with Play (1 = as authored).</summary>
        public float Speed => _body.Speed;

        /// <summary>
        /// Cross-fades into a clip over <paramref name="fade"/> seconds. A loop plays until Stop; a one-off plays once, then
        /// fades back to the game's controller or holds its last pose. False if nothing could be played.
        /// </summary>
        public bool Play(AnimationClip clip, float fade = 0.25f, float speed = 1f, ClipEnd end = ClipEnd.Return, float startAt = 0f)
        {
            return Start(_body, clip, BodyMask.Full, fade, speed, end, startAt);
        }

        /// <summary>
        /// The same on the masked layer: the clip takes only the masked part of the body, over the game's own animation or a
        /// clip played with Play. A loop runs until <see cref="StopOver"/>.
        /// </summary>
        public bool PlayOver(AnimationClip clip, BodyMask mask, float fade = 0.25f, float speed = 1f, ClipEnd end = ClipEnd.Return, float startAt = 0f)
        {
            if (mask == BodyMask.Full)
            {
                return Play(clip, fade, speed, end, startAt);
            }
            return Start(_over, clip, mask, fade, speed, end, startAt);
        }

        /// <summary>Fades back to the game's own animation; the graph goes when the fade ends and no masked clip is left.</summary>
        public void Stop(float fade = 0.25f)
        {
            Halt(_body, fade);
        }

        /// <summary>Fades the masked layer out; what was under it shows again.</summary>
        public void StopOver(float fade = 0.25f)
        {
            Halt(_over, fade);
        }

        /// <summary>Sets how fast the clip played with Play runs (1 as made); the engine is told only of a change over 0.01.</summary>
        public void SetSpeed(float speed)
        {
            SetSpeed(_body, speed);
        }


        /// <summary>
        /// Keeps a walking clip in step with the ground: <paramref name="groundSpeed"/> is the agent's real speed,
        /// <paramref name="clipSpeed"/> the one the clip was made for (CityClip.Speed). From the owner's tick, not per frame.
        /// Returns the playback speed set; standing still slows the clip to its least, the cue for a standing clip.
        /// </summary>
        public float MatchSpeed(float groundSpeed, float clipSpeed, float least = 0.35f, float most = 1.6f)
        {
            float speed = clipSpeed > 0.01f ? Mathf.Clamp(groundSpeed / clipSpeed, least, most) : 1f;
            SetSpeed(_body, speed);
            return speed;
        }

        /// <summary>Evaluates the graph now, for tests and for a pose shown in the same frame it is set.</summary>
        public void Evaluate(float delta)
        {
            if (_graph.IsValid())
            {
                _graph.Evaluate(delta);
            }
        }

        /// <summary>Hands the character back at once and frees the graph. Safe at any time, any number of times.</summary>
        public void Dispose()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
            _body.Clear();
            _over.Clear();
            _overBuilt = false;
        }

        /// <summary>One step of whatever is changing; false once nothing is, when ActivityAnimators stops calling it.</summary>
        internal bool Advance(float delta)
        {
            if (!_graph.IsValid())
            {
                return false;
            }
            if (_animator == null)
            {
                Dispose();   // the character was destroyed under us
                return false;
            }
            bool busy = Advance(_body, delta);
            if (_overBuilt)
            {
                busy |= Advance(_over, delta);
            }
            if (_body.Weight <= 0f && _body.WeightGoal <= 0f && _over.Weight <= 0f && _over.WeightGoal <= 0f)
            {
                Dispose();
                return false;
            }
            Apply();
            return busy;
        }

        private bool Start(Layer layer, AnimationClip clip, BodyMask mask, float fade, float speed, ClipEnd end, float startAt)
        {
            if (clip == null || !Usable || !ActivityAnimators.Working)
            {
                return false;
            }
            if (!_graph.IsValid())
            {
                Build();
            }
            bool over = layer == _over;
            if (over && !_overBuilt)
            {
                BuildOver();
            }
            if (layer.Current >= 0 && layer.Clips[layer.Current] == clip && layer.WeightGoal > 0f && clip.isLooping && layer.Masks[layer.Current] == mask)
            {
                return true;   // already playing it
            }
            int slot = layer.Current == 0 ? 1 : 0;
            if (layer.Weight <= 0f || layer.Current < 0)
            {
                slot = 0;      // nothing of this layer shows: no cross-fade between slots
                layer.Blend = 0f;
            }
            if (layer.Slots[slot].IsValid())
            {
                if (over)
                {
                    _overMixer.DisconnectInput(slot);
                }
                else
                {
                    _mixer.DisconnectInput(slot);
                }
                layer.Slots[slot].Destroy();
            }
            speed = Mathf.Max(0.01f, speed);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(speed);
            playable.SetTime(startAt);
            playable.SetTime(startAt);   // twice: the first call only sets where the next evaluation starts
            if (over)
            {
                _overMixer.ConnectInput(slot, playable, 0, 1f);
                if (slot == 0)
                {
                    _overJob.First = MusclesOf(mask);
                }
                else
                {
                    _overJob.Second = MusclesOf(mask);
                }
            }
            else
            {
                _mixer.ConnectInput(slot, playable, 0);
            }
            layer.Slots[slot] = playable;
            layer.Clips[slot] = clip;
            layer.Masks[slot] = mask;
            layer.Current = slot;
            layer.End = end;
            layer.Speed = speed;
            layer.FadeOut = Mathf.Max(Instant, fade);
            layer.Remaining = clip.isLooping ? -1f : Mathf.Max(0f, clip.length - startAt) / speed;
            layer.BlendRate = 1f / Mathf.Max(Instant, fade);
            layer.WeightGoal = 1f;
            layer.WeightRate = 1f / Mathf.Max(Instant, fade);
            Apply();
            if (!_graph.IsPlaying())
            {
                _graph.Play();
            }
            ActivityAnimators.Step(this);
            return true;
        }

        private void Halt(Layer layer, float fade)
        {
            if (!_graph.IsValid() || layer.Current < 0)
            {
                return;
            }
            layer.WeightGoal = 0f;
            layer.WeightRate = 1f / Mathf.Max(Instant, fade);
            layer.Remaining = -1f;
            ActivityAnimators.Step(this);
        }

        private void SetSpeed(Layer layer, float speed)
        {
            speed = Mathf.Max(0.01f, speed);
            if (!_graph.IsValid() || layer.Current < 0 || Mathf.Abs(speed - layer.Speed) < 0.01f)
            {
                return;
            }
            AnimationClipPlayable playable = layer.Slots[layer.Current];
            if (!playable.IsValid())
            {
                return;
            }
            if (layer.Remaining > 0f)
            {
                layer.Remaining *= layer.Speed / speed;   // a one-off still counting down ends when its clip does
            }
            playable.SetSpeed(speed);
            layer.Speed = speed;
        }

        private static bool Advance(Layer layer, float delta)
        {
            bool busy = false;
            float goal = layer.Current == 1 ? 1f : 0f;
            if (layer.Blend != goal)
            {
                layer.Blend = Mathf.MoveTowards(layer.Blend, goal, layer.BlendRate * delta);
                busy = true;
            }
            if (layer.Remaining >= 0f && layer.WeightGoal > 0f)
            {
                layer.Remaining = Mathf.Max(0f, layer.Remaining - delta);
                busy = true;
                if (layer.End == ClipEnd.Return && layer.Remaining <= layer.FadeOut)
                {
                    layer.WeightGoal = 0f;
                    layer.WeightRate = 1f / layer.FadeOut;
                    layer.Remaining = -1f;
                }
                else if (layer.End == ClipEnd.Hold && layer.Remaining <= 0f)
                {
                    layer.Remaining = -1f;   // held: nothing left to count
                }
            }
            if (layer.Weight != layer.WeightGoal)
            {
                layer.Weight = Mathf.MoveTowards(layer.Weight, layer.WeightGoal, layer.WeightRate * delta);
                busy = true;
            }
            return busy;
        }

        private void Build()
        {
            _graph = PlayableGraph.Create("RealLifeOverhaul Activity");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            _body.Clear();
            _over.Clear();
            _overBuilt = false;
            _body.Output = AnimationPlayableOutput.Create(_graph, "Activity", _animator);
            _body.Output.SetSourcePlayable(_mixer);
        }

        // The masked layer, made the first time something is played over. Its output comes after the first and starts from
        // what that or the game's controller left, so the body outside its mask stays exactly as it was.
        private void BuildOver()
        {
            _overJob = default;
            _overMixer = AnimationScriptPlayable.Create(_graph, _overJob, 2);
            _overMixer.SetProcessInputs(false);
            _over.Output = AnimationPlayableOutput.Create(_graph, "Activity (masked)", _animator);
            _over.Output.SetSourcePlayable(_overMixer);
            _over.Output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            _over.Output.SetSortingOrder(200);
            _over.Output.SetWeight(0f);
            _overBuilt = true;
        }

        private void Apply()
        {
            _mixer.SetInputWeight(0, 1f - _body.Blend);
            _mixer.SetInputWeight(1, _body.Blend);
            _body.Output.SetWeight(Smooth(_body.Weight));
            if (_overBuilt)
            {
                _overJob.FirstWeight = _over.Slots[0].IsValid() ? 1f - _over.Blend : 0f;
                _overJob.SecondWeight = _over.Slots[1].IsValid() ? _over.Blend : 0f;
                _overMixer.SetJobData(_overJob);
                _over.Output.SetWeight(Smooth(_over.Weight));
            }
        }

        /// <summary>Frees the masks' muscle lists, only when no player has a graph (ActivityAnimators.ReleaseAll).</summary>
        internal static void FreeMasks()
        {
            for (int i = 0; i < MuscleSets.Length; i++)
            {
                if (MuscleSets[i].IsCreated)
                {
                    MuscleSets[i].Dispose();
                }
                MuscleSets[i] = default;
            }
        }

        // The muscles a mask takes, made the first time it is used and kept. Never the pelvis or the legs: where the body is
        // and how it stands is left to what is underneath.
        private static NativeArray<MuscleHandle> MusclesOf(BodyMask mask)
        {
            int index = (int)mask;
            if (MuscleSets[index].IsCreated)
            {
                return MuscleSets[index];
            }
            bool left = mask == BodyMask.Upper || mask == BodyMask.Arms || mask == BodyMask.LeftArm;
            bool right = mask == BodyMask.Upper || mask == BodyMask.Arms || mask == BodyMask.RightArm;
            bool trunk = mask == BodyMask.Upper;
            var all = new MuscleHandle[MuscleHandle.muscleHandleCount];
            MuscleHandle.GetMuscleHandles(all);
            int count = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    MuscleSets[index] = new NativeArray<MuscleHandle>(count, Allocator.Persistent);
                    count = 0;
                }
                for (int i = 0; i < all.Length; i++)
                {
                    HumanPartDof part = all[i].humanPartDof;
                    bool taken = (trunk && (part == HumanPartDof.Body || part == HumanPartDof.Head))
                                 || (left && (part == HumanPartDof.LeftArm || (part >= HumanPartDof.LeftThumb && part <= HumanPartDof.LeftLittle)))
                                 || (right && (part == HumanPartDof.RightArm || (part >= HumanPartDof.RightThumb && part <= HumanPartDof.RightLittle)));
                    if (!taken)
                    {
                        continue;
                    }
                    if (pass == 1)
                    {
                        NativeArray<MuscleHandle> set = MuscleSets[index];
                        set[count] = all[i];
                    }
                    count++;
                }
            }
            return MuscleSets[index];
        }

        // Ease in and out: a straight ramp shows as a jerk at both ends of a fade.
        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
