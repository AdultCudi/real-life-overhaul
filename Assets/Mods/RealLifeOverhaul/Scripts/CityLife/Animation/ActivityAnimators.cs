using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one place the mod's clip players and held tools are stepped from.
    ///
    ///     ActivityAnimator player = ActivityAnimators.For(character.animator);
    ///     player.Play(clip);           // cross-fades in; a loop runs until Stop, a one-off returns by itself
    ///     player.Stop();               // cross-fades back to the game's own animation
    ///
    /// Update steps only the players whose weights are changing or whose one-off clip is
    /// counting down; LateUpdate keeps the second hand on tools held in both hands. With
    /// nothing fading and no such tool this component is switched off and costs nothing.
    /// Any fault hands every character back to the game and switches the service off for the
    /// session (reported once through <see cref="Fault"/>).
    /// </summary>
    public sealed class ActivityAnimators : MonoBehaviour
    {
        private static ActivityAnimators _instance;
        private static readonly Dictionary<int, ActivityAnimator> Players = new Dictionary<int, ActivityAnimator>(16);
        private static readonly List<ActivityAnimator> Stepping = new List<ActivityAnimator>(16);
        private static readonly List<HeldTool> Reaching = new List<HeldTool>(8);
        private static bool _failed;

        /// <summary>Told about the fault that switched the service off. Set by whoever owns the log.</summary>
        public static Action<Exception> Fault;

        /// <summary>False after a fault: Play then does nothing, and callers leave people to the game's animations.</summary>
        public static bool Working => !_failed;

        /// <summary>The clip player for a character's Animator: made the first time, the same one after that.</summary>
        public static ActivityAnimator For(Animator animator)
        {
            if (animator == null)
            {
                return null;
            }
            int id = animator.GetInstanceID();
            if (!Players.TryGetValue(id, out ActivityAnimator player) || player.Animator == null)
            {
                player?.Dispose();   // its character was destroyed: the graph must not outlive it
                player = new ActivityAnimator(animator);
                Players[id] = player;
            }
            return player;
        }

        /// <summary>Hands one character back to the game at once and forgets its player (the character is going away).</summary>
        public static void Forget(Animator animator)
        {
            if (animator != null && Players.TryGetValue(animator.GetInstanceID(), out ActivityAnimator player))
            {
                player.Dispose();
                Players.Remove(animator.GetInstanceID());
            }
        }

        /// <summary>Everyone back to the game's own animation, now. For city unload and for switching the feature off.</summary>
        public static void ReleaseAll()
        {
            foreach (ActivityAnimator player in Players.Values)
            {
                player.Dispose();
            }
            Players.Clear();
            Stepping.Clear();
            Reaching.Clear();
            ActivityAnimator.FreeMasks();   // no graph is left that reads them
            if (_instance != null)
            {
                _instance.enabled = false;
            }
        }

        internal static void Step(ActivityAnimator player)
        {
            if (!Stepping.Contains(player))
            {
                Stepping.Add(player);
            }
            Wake();
        }

        internal static void Reach(HeldTool tool)
        {
            if (!Reaching.Contains(tool))
            {
                Reaching.Add(tool);
            }
            Wake();
        }

        /// <summary>One frame's worth of fading, by hand: for tests, which have no frames of their own.</summary>
        public static void StepAll(float delta)
        {
            for (int i = Stepping.Count - 1; i >= 0; i--)
            {
                if (!Stepping[i].Advance(delta))
                {
                    Stepping.RemoveAt(i);
                }
            }
        }

        /// <summary>The same for the hands on tools: call after the animation has been evaluated.</summary>
        public static void ReachAll(float delta)
        {
            for (int i = Reaching.Count - 1; i >= 0; i--)
            {
                if (!Reaching[i].Reach(delta))
                {
                    Reaching.RemoveAt(i);
                }
            }
        }

        private static void Wake()
        {
            if (_instance != null && !_failed)
            {
                _instance.enabled = true;
            }
        }

        private void Awake()
        {
            _instance = this;
            _failed = false;
            enabled = Stepping.Count > 0 || Reaching.Count > 0;
        }

        private void OnDestroy()
        {
            ReleaseAll();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("CityLife: ActivityAnimators.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            try
            {
                StepAll(Time.deltaTime);
                if (Stepping.Count == 0 && Reaching.Count == 0)
                {
                    enabled = false;
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("CityLife: ActivityAnimators.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            try
            {
                ReachAll(Time.deltaTime);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Fail(Exception error)
        {
            _failed = true;
            ReleaseAll();
            Fault?.Invoke(error);
        }
    }
}
