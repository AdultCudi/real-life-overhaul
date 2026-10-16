using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Work that needs frames of its own (a render and the graphics card's read-back of it, a file read on a worker) done
    /// while the game's loading screen is still up, for at most <see cref="MostSeconds"/> real seconds: the time it takes is
    /// then part of the load, never a frame of play. The game keeps its loading screen up until every mod's start has
    /// finished (UI.Load.LoadingScreen.LoadAsyncScenes waits for ModLifecycleLoader.LoadScopeAsync, which waits for each
    /// mod's OnLoadAsync), so the mod's start hands back <see cref="Wait"/>, which finishes once every piece is done or the
    /// time is up, whichever comes first. The frames meanwhile are the loading screen's own: the game runs them as it always
    /// does (its hints, its progress bar).
    ///
    /// A feature hands its piece over in its Awake (<see cref="Add"/>): a step called once a frame while the hold lasts,
    /// true when the piece is done, and an end called once when the hold ends (done or not), where the piece says what it
    /// did and leaves the rest for later. <see cref="Holding"/> is true only while the hold lasts and the loading screen is
    /// up. A piece's fault ends that piece alone. The hold can never keep a load waiting: its own time limit ends it in the
    /// first frame past it, and a timer that needs no frame at all ends it a few seconds later still (<see cref="Backstop"/>),
    /// even if this component were gone or switched off.
    ///
    /// Per frame: nothing (the component is disabled) unless a hold is on; then each piece's step and one time comparison.
    /// </summary>
    public sealed class LoadHold : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "LoadHold", typeof(LoadHold));   // its Update (Core/Guard)

        public const float MostSeconds = 2f;          // real seconds the loading screen is held at most, for every piece together: a load is never held up long
        private const int Backstop = 6000;            // milliseconds after which the hold ends without a frame (the time limit, the frames it waits for, a margin)

        private static readonly List<Func<bool>> Steps = new List<Func<bool>>(4);
        private static readonly List<Action<bool>> Ends = new List<Action<bool>>(4);
        private static readonly List<string> Names = new List<string>(4);
        private static readonly List<bool> Done = new List<bool>(4);
        private static TaskCompletionSource<bool> _hold;
        private static long _began;
        private static double _held;                 // milliseconds the last hold lasted (for the pieces' own lines at its end)
        private static LoadHold _instance;

        /// <summary>True while the loading screen is held for the pieces (and is up).</summary>
        public static bool Holding => _hold != null && UI.Load.LoadScene.isLoading;

        /// <summary>Milliseconds since the hold began; once it has ended, how long it lasted (0 before any).</summary>
        public static double Spent => _hold != null ? (Stopwatch.GetTimestamp() - _began) * 1000.0 / Stopwatch.Frequency : _held;

        /// <summary>
        /// Hands over a piece: 'step' is called once a frame while the hold lasts and returns true when the piece is done;
        /// 'end' is called once when the hold ends, with whether the piece was done. A constant text names it.
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
        /// The mod's start hands this back to the game: finished at once when no piece was handed over or the loading screen
        /// is not up; otherwise when every piece is done or <see cref="MostSeconds"/> have passed.
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
            return Task.WhenAny(hold.Task, Task.Delay(Backstop));   // (never faults, never waits past the backstop)
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
                End();   // (the hold ends with its guard: the load goes on)
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

        // The hold ends: each piece told, the game's load let go on, the pieces forgotten (they belonged to this load).
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
                End();   // (a city going while it holds: its load is let go on, its pieces forgotten)
                _instance = null;
            }
        }
    }
}
