using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one place the mod's clip players (<see cref="For"/>) and held tools are stepped from: Update steps only players fading
    /// or counting down, LateUpdate keeps the second hand on two-handed tools, and idle it is switched off and costs nothing. A
    /// fault hands every character back and switches it off for the session, reported through <see cref="Fault"/>.
    /// </summary>
    public sealed class ActivityAnimators : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("citylife", "ActivityAnimators", typeof(ActivityAnimators));

        private static ActivityAnimators _instance;
        private static readonly Dictionary<int, ActivityAnimator> Players = new Dictionary<int, ActivityAnimator>(16);
        private static readonly List<ActivityAnimator> Stepping = new List<ActivityAnimator>(16);
        private static readonly List<HeldTool> Reaching = new List<HeldTool>(8);
        private static readonly List<int> Dead = new List<int>(16);
        private const int SweepFrom = 32;   // players kept before those of destroyed characters are swept: the game's pools destroy their overflow
        private static bool _failed;

        /// <summary>Told about the fault that switched the service off; set by whoever owns the log.</summary>
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
                if (player == null && Players.Count >= SweepFrom)
                {
                    SweepDead();
                }
                player = new ActivityAnimator(animator);
                Players[id] = player;
            }
            return player;
        }

        // Lets go of the players of characters destroyed without Forget (a passer-by taken back by the game's pool). Runs only
        // as a new player is made past SweepFrom.
        private static void SweepDead()
        {
            Dead.Clear();
            foreach (KeyValuePair<int, ActivityAnimator> pair in Players)
            {
                if (pair.Value == null || pair.Value.Animator == null)
                {
                    Dead.Add(pair.Key);
                }
            }
            for (int i = 0; i < Dead.Count; i++)
            {
                ActivityAnimator dead = Players[Dead[i]];
                dead?.Dispose();
                Stepping.Remove(dead);
                Players.Remove(Dead[i]);
            }
            Dead.Clear();
        }

        /// <summary>Hands one character back to the game at once and forgets its player, as the character goes away.</summary>
        public static void Forget(Animator animator)
        {
            if (animator != null && Players.TryGetValue(animator.GetInstanceID(), out ActivityAnimator player))
            {
                player.Dispose();
                Players.Remove(animator.GetInstanceID());
            }
        }

        /// <summary>Everyone back to the game's own animation, now: as the city unloads or the feature is switched off.</summary>
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

        /// <summary>One frame's fading, by hand, for tests, which have no frames of their own.</summary>
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

        private static void ReachAll(float delta)
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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
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
