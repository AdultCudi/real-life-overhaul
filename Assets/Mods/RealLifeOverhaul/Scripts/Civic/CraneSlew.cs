using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The tower crane's jib turning on its slewing ring by the engine's own animation: one legacy clip made once a city and
    /// played looping on the jib by Unity's Animation component, culled with the jib's renderer. The mod runs no code of its
    /// own for it. Cost: nothing per frame of the mod's; the engine samples the clip while the jib is drawn.
    /// </summary>
    internal static class CraneSlew
    {
        public const string ClipName = "slew";

        // A round of the jib: from over the frame to the stacks, a wait for the load, back, out the other way, home. Seconds and degrees.
        private static readonly float[] Times = { 0f, 14f, 34f, 48f, 70f, 84f, 100f };
        private static readonly float[] Turns = { 0f, 0f, 70f, 70f, -35f, -35f, 0f };

        private static AnimationClip _clip;

        /// <summary>Starts the jib slewing (an Animation is added the first time), a round in from <paramref name="offset"/> seconds. False if it could not.</summary>
        public static bool Play(GameObject jib, float offset)
        {
            if (!jib.TryGetComponent(out Animation animation))
            {
                animation = jib.AddComponent<Animation>();
            }
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.BasedOnRenderers;
            if (animation.GetClip(ClipName) == null)
            {
                animation.AddClip(Clip(), ClipName);
            }
            animation.wrapMode = WrapMode.Loop;
            if (!animation.Play(ClipName))
            {
                return false;
            }
            AnimationState state = animation[ClipName];
            if (state != null)
            {
                state.time = offset % Times[Times.Length - 1];
            }
            return true;
        }

        /// <summary>Whether a jib is slewing now: its Animation has the clip and is playing it.</summary>
        public static bool Playing(GameObject jib) => jib != null && jib.TryGetComponent(out Animation animation) && animation.IsPlaying(ClipName);

        /// <summary>The length of a round, seconds.</summary>
        public static float Round => Times[Times.Length - 1];

        // Four curves of the jib's own rotation about its ring (the clip's path is the jib itself), eased at every wait.
        private static AnimationClip Clip()
        {
            if (_clip != null)
            {
                return _clip;
            }
            var x = new Keyframe[Times.Length];
            var y = new Keyframe[Times.Length];
            var z = new Keyframe[Times.Length];
            var w = new Keyframe[Times.Length];
            for (int i = 0; i < Times.Length; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, Turns[i], 0f);
                x[i] = new Keyframe(Times[i], turn.x, 0f, 0f);
                y[i] = new Keyframe(Times[i], turn.y, 0f, 0f);
                z[i] = new Keyframe(Times[i], turn.z, 0f, 0f);
                w[i] = new Keyframe(Times[i], turn.w, 0f, 0f);
            }
            _clip = new AnimationClip { name = "RealLifeOverhaul crane slew", legacy = true, wrapMode = WrapMode.Loop };
            _clip.SetCurve(string.Empty, typeof(Transform), "localRotation.x", new AnimationCurve(x));
            _clip.SetCurve(string.Empty, typeof(Transform), "localRotation.y", new AnimationCurve(y));
            _clip.SetCurve(string.Empty, typeof(Transform), "localRotation.z", new AnimationCurve(z));
            _clip.SetCurve(string.Empty, typeof(Transform), "localRotation.w", new AnimationCurve(w));
            _clip.EnsureQuaternionContinuity();
            return _clip;
        }

        /// <summary>The clip goes with the city.</summary>
        public static void Dispose()
        {
            if (_clip != null)
            {
                Object.Destroy(_clip);
            }
            _clip = null;
        }
    }
}
