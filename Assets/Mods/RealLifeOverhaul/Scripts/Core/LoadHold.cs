using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Work that needs frames of its own (a render read back, a file read on a worker) done behind the game's loading
    /// screen, for at most <see cref="MostSeconds"/>, so it is part of the load and never a frame of play. The game keeps
    /// the loading screen up until every mod's OnLoadAsync has finished, so the mod's start hands back <see cref="Wait"/>.
    /// A feature hands its piece over in its Awake (<see cref="Add"/>); a piece's fault ends that piece alone. The hold
    /// never keeps a load waiting: its time limit ends it, and a timer that needs no frame (<see cref="Backstop"/>) ends
    /// it even if this component is gone.
    /// COST: nothing per frame (disabled) unless a hold is on; then each piece's step and a time comparison.
    /// </summary>
    public sealed class LoadHold : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "LoadHold", typeof(LoadHold));

        public const float MostSeconds = 2f;          // real seconds the loading screen is held at most, for every piece together
        private const int Backstop = 6000;            // milliseconds after which the hold ends without a frame: the limit and a margin

        private static readonly List<Func<bool>> Steps = new List<Func<bool>>(4);
        private static readonly List<Action<bool>> Ends = new List<Action<bool>>(4);
        private static readonly List<string> Names = new List<string>(4);
        private static readonly List<bool> Done = new List<bool>(4);
        private static TaskCompletionSource<bool> _hold;
        private static long _began;
        private static double _held;                 // milliseconds the last hold lasted
        private static LoadHold _instance;

        /// <summary>True while the loading screen is up and held for the pieces.</summary>
        public static bool Holding => _hold != null && UI.Load.LoadScene.isLoading;

        /// <summary>Milliseconds since the hold began; once it has ended, how long it lasted (0 before any).</summary>
        public static double Spent => _hold != null ? (Stopwatch.GetTimestamp() - _began) * 1000.0 / Stopwatch.Frequency : _held;

        /// <summary>
        /// Hands over a piece: <paramref name="step"/> runs once a frame while the hold lasts, true when done;
        /// <paramref name="end"/> runs once when the hold ends, told whether the piece was done.
        /// </summary>
        public static void Add(string what, Func<bool> step, Action<bool> end)
        {
            if (step != null)
            {
                Steps.Add(step);
                Ends.Add(end);
                Names.Add(what);
                Done.Add(false);
            }
        }

        /// <summary>
        /// The mod's start hands this back to the game: finished at once with no piece or no loading screen, else when
        /// every piece is done or <see cref="MostSeconds"/> have passed.
        /// </summary>
        public static Task Wait()
        {
            if (Steps.Count == 0 || _instance == null || !UI.Load.LoadScene.isLoading)
            {
                End();
                return Task.CompletedTask;
            }
            _hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _began = Stopwatch.GetTimestamp();
            _held = 0.0;
            _instance.enabled = true;
            TaskCompletionSource<bool> hold = _hold;
            return Task.WhenAny(hold.Task, Task.Delay(Backstop));   // never faults, never waits past the backstop
        }

        private void Awake()
        {
            _instance = this;
            enabled = false;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: LoadHold.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                End();   // the hold ends with its guard: the load goes on
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                End();
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_hold == null)
            {
                enabled = false;
                return;
            }
            bool all = true;
            for (int i = 0; i < Steps.Count; i++)
            {
                if (Done[i])
                {
                    continue;
                }
                bool done = true;
                try
                {
                    done = Steps[i]();
                }
                catch (Exception e)
                {
                    if (!Guard.DoesWithout(e, "the work behind the loading screen: " + Names[i], "the feature does it later"))
                    {
                        SessionWatch.Logger?.Error(e);
                    }
                }
                Done[i] = done;
                all &= done;
            }
            if (all || Spent >= MostSeconds * 1000.0 || !UI.Load.LoadScene.isLoading)
            {
                End();
                enabled = false;
            }
        }

        // Each piece is told, the game's load goes on, and the pieces (this load's) are forgotten.
        private static void End()
        {
            double spent = Spent;
            TaskCompletionSource<bool> hold = _hold;
            _held = hold != null ? spent : 0.0;
            _hold = null;
            int done = 0;
            for (int i = 0; i < Ends.Count; i++)
            {
                done += Done[i] ? 1 : 0;
                try
                {
                    Ends[i]?.Invoke(Done[i]);
                }
                catch (Exception e)
                {
                    if (!Guard.DoesWithout(e, "the end of the work behind the loading screen: " + Names[i], "the feature does it later"))
                    {
                        SessionWatch.Logger?.Error(e);
                    }
                }
            }
            if (hold != null && Meta.Dev.Verbose)
            {
                SessionWatch.Logger?.Info("Load hold check: " + done + " of " + Steps.Count + " piece(s) done behind the loading screen in " + spent.ToString("F0") + " ms (at most "
                                          + (MostSeconds * 1000f).ToString("F0") + " ms): " + (spent <= MostSeconds * 1000.0 + 200.0 ? "PASS" : "FAIL (the load was held up past its limit)") + ".");
            }
            Steps.Clear();
            Ends.Clear();
            Names.Clear();
            Done.Clear();
            hold?.TrySetResult(true);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                End();   // a city unloading while it holds
                _instance = null;
            }
        }
    }
}
