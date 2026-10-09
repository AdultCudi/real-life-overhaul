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
    /// The part of the body a clip played with <see cref="ActivityAnimator.PlayOver"/> takes; the rest stays
    /// whatever is underneath (the game's walk or idle, or a clip played with Play).
    /// Upper is trunk, head and both arms (a gesture made standing); Arms, LeftArm and RightArm are
    /// arms and fingers only, which is what to use on somebody who is walking.
    /// </summary>
    public enum BodyMask { Full, Upper, Arms, LeftArm, RightArm }

    /// <summary>
    /// Plays the mod's own animation clips on one of the game's characters without touching the
    /// game's animator controller. The clips are Humanoid clips, so they play on any character
    /// whose Animator has a human avatar (all of the game's people do).
    ///
    /// A small playable graph is put on the Animator the first time something is played: two
    /// clip slots into a mixer, and an output whose weight is how much of the pose is ours.
    /// Fading that weight in cross-fades from whatever the game's controller is doing (idle,
    /// walking) into the clip; fading it out hands the character back, and when it reaches
    /// nothing the graph is destroyed. Playing a second clip cross-fades between the two slots.
    /// The character is never moved: the clips are baked into the pose, so where somebody
    /// stands stays the business of their navigation agent.
    ///
    /// On top of that there is a second, masked layer (<see cref="PlayOver"/>): a clip that takes
    /// only the arms, one arm or the upper body, over whatever is below it. It is how somebody
    /// carries a box, a cup, an umbrella or a stack of flyers while the game walks them, and how
    /// a gesture is made without the legs being taken. It has its own two slots, its own weight
    /// and its own output, evaluated after the first: that output starts from the pose the
    /// controller (and the first layer) arrived at, and a small animation job copies into it only
    /// the muscles the mask names (an AvatarMask on a layer mixer does not do this: it resets the
    /// rest of the body). The job runs with the Animator's own evaluation, off the main thread,
    /// only for a character that has such a clip, and allocates nothing.
    ///
    /// Cost: a looping clip that has finished fading in is evaluated by the engine with the rest
    /// of the Animator and needs nothing from script. Script runs only while a weight is
    /// changing or a one-off clip is counting down (ActivityAnimators steps exactly those), and
    /// an Animator the game has switched off (someone out of view) evaluates nothing at all.
    /// Get one from <see cref="ActivityAnimators.For"/>; do not create them.
    /// </summary>
    public sealed class ActivityAnimator
    {
        private const float Instant = 0.0001f;

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
            public float Blend;             // 0 = all slot 0, 1 = all slot 1
            public float BlendRate;
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

        /// <summary>False for a character these clips cannot play on; Play then does nothing and says so.</summary>
        public bool Usable => _animator != null && _animator.isHuman && _animator.avatar != null && _animator.avatar.isValid;

        /// <summary>True from Play until the character has been handed back completely.</summary>
        public bool Active => _graph.IsValid();

        /// <summary>The clip being played or faded towards; null when the game's controller has the character.</summary>
        public AnimationClip Current => _body.Current >= 0 && _body.WeightGoal > 0f ? _body.Clips[_body.Current] : null;

        /// <summary>How much of the pose is this player's right now, 0 to 1.</summary>
        public float Weight => _body.Weight;

        /// <summary>The clip on the masked layer; null when there is none.</summary>
        public AnimationClip Over => _over.Current >= 0 && _over.WeightGoal > 0f ? _over.Clips[_over.Current] : null;

        /// <summary>How much of the masked part of the pose is the masked layer's right now, 0 to 1.</summary>
        public float OverWeight => _over.Weight;

        /// <summary>The playback speed of the clip played with Play (1 = as authored).</summary>
        public float Speed => _body.Speed;

        /// <summary>
        /// Cross-fades into a clip over <paramref name="fade"/> seconds. A looping clip plays until
        /// Stop; a one-off clip plays once and then either fades back to the game's controller
        /// (the default) or holds its last pose. Returns false if nothing could be played.
        /// </summary>
        public bool Play(AnimationClip clip, float fade = 0.25f, float speed = 1f, ClipEnd end = ClipEnd.Return, float startAt = 0f)
        {
            return Start(_body, clip, BodyMask.Full, fade, speed, end, startAt);
        }

        /// <summary>
        /// The same for the masked layer: the clip takes only the part of the body the mask names,
        /// over the game's own animation (walking included) or over a clip played with Play.
        /// A loop runs until <see cref="StopOver"/>; a one-off returns or holds as asked.
        /// </summary>
        public bool PlayOver(AnimationClip clip, BodyMask mask, float fade = 0.25f, float speed = 1f, ClipEnd end = ClipEnd.Return, float startAt = 0f)
        {
            if (mask == BodyMask.Full)
            {
                return Play(clip, fade, speed, end, startAt);
            }
            return Start(_over, clip, mask, fade, speed, end, startAt);
        }

        /// <summary>Fades back to the game's own animation; the graph is gone when the fade ends (and no masked clip is left).</summary>
        public void Stop(float fade = 0.25f)
        {
            Halt(_body, fade);
        }

        /// <summary>Fades the masked layer out; what was under it shows again.</summary>
        public void StopOver(float fade = 0.25f)
        {
            Halt(_over, fade);
        }

        /// <summary>
        /// Sets how fast the clip played with Play runs (1 = as authored). Cheap: the engine is told
        /// only when the value changes by more than a hundredth.
        /// </summary>
        public void SetSpeed(float speed)
        {
            SetSpeed(_body, speed);
        }

        /// <summary>The same for the masked layer.</summary>
        public void SetOverSpeed(float speed)
        {
            SetSpeed(_over, speed);
        }

        /// <summary>
        /// Makes a walking clip keep step with the ground: <paramref name="groundSpeed"/> is how fast
        /// the character is really moving (its agent's velocity), <paramref name="clipSpeed"/> the
        /// speed the clip was made for (CityClip.Speed). Call it from the owner's own tick, not per
        /// frame. Returns the playback speed set. Standing still slows the clip to its least, which
        /// is the cue to change to a standing clip.
        /// </summary>
        public float MatchSpeed(float groundSpeed, float clipSpeed, float least = 0.35f, float most = 1.6f)
        {
            float speed = clipSpeed > 0.01f ? Mathf.Clamp(groundSpeed / clipSpeed, least, most) : 1f;
            SetSpeed(_body, speed);
            return speed;
        }

        /// <summary>
        /// Evaluates the graph by hand, now. The engine does this itself every frame; this is for
        /// tests and for putting someone in a pose in the same frame they are shown.
        /// </summary>
        public void Evaluate(float delta)
        {
            if (_graph.IsValid())
            {
                _graph.Evaluate(delta);
            }
        }

        /// <summary>Hands the character back at once and frees the graph. Safe to call at any time, any number of times.</summary>
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

        /// <summary>
        /// One step of whatever is changing. Returns false when nothing is any more, which is
        /// when ActivityAnimators stops calling it.
        /// </summary>
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
                slot = 0;      // nothing of this layer is showing: no cross-fade between slots is needed
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
            playable.SetTime(startAt);   // twice: the first call only sets where the next evaluation starts from
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

        // The masked layer: made the first time something is played over, and only then. Its output comes
        // after the first one and starts from what that (or the game's controller) left, so the part of the
        // body its mask leaves out stays exactly as it was.
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

        /// <summary>Frees the lists of muscles the masks are made of. Only when no player has a graph (ActivityAnimators.ReleaseAll).</summary>
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

        // The muscles a mask takes: made the first time the mask is used and kept. The pelvis and the legs are
        // never among them: where the body is and how it stands stays the business of what is underneath.
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
