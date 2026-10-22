using BAModAPI;
using UI.Notification;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Runs the performance work: the tweaks switched one a frame as the options or the benchmark ask, their timer ticks,
    /// and the probe and benchmark keys. Cost a frame while nothing is measured: a key test, five flags, a time comparison.
    /// </summary>
    public sealed class PerfDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("performance", "PerfDirector", typeof(PerfDirector));

        private const float SettleSeconds = 3f;   // after the city loads, before the first tweak looks at the scene

        public static IModLogger Logger;
        public static PerfDirector Instance { get; private set; }

        private PerfSampler _sampler;
        private PerfLoop _loop;
        private PerfProbe _probe;
        private PerfBench _bench;
        private float _nextTick = float.MaxValue;
        private float _settledAt;
        private Pending _pending;
        private float _pendingSince;

        // a measurement asked for from the options page waits for the menus to close
        private enum Pending { None, Probe, Bench }

        private void Awake()
        {
            Instance = this;
            _sampler = gameObject.AddComponent<PerfSampler>();
            _loop = new PerfLoop();
            _probe = new PerfProbe(_sampler, _loop, Logger);
            _bench = new PerfBench(_sampler, _loop, Logger);
            _settledAt = Time.unscaledTime + SettleSeconds;
            PerfTweaks.Dirty = true;
            SceneManager.sceneLoaded += SceneLoadedCall;
            SceneManager.sceneUnloaded += SceneUnloadedCall;
            ScenePrewarm.Add("performance: the scene looked at for the tweaks that are on (the skies, the scenery)", PerfTweaks.Prewarm);
        }

        /// <summary>Starts the ten-second capture, or arms it to start once the menus are closed.</summary>
        public void StartProbe() => Ask(Pending.Probe);

        /// <summary>Starts the benchmark, or arms it to start once the menus are closed.</summary>
        public void StartBench() => Ask(Pending.Bench);

        private void Ask(Pending what)
        {
            if (_probe.Running || _bench.Running)
            {
                return;
            }
            if (ThirdPersonCamera.IsUiOpen() || Time.timeScale <= 0f)
            {
                _pending = what;
                _pendingSince = float.MaxValue;
                Notify("reallife_perf_armed", 6f);
                return;
            }
            Begin(what);
        }

        private void Begin(Pending what)
        {
            _pending = Pending.None;
            if (what == Pending.Probe)
            {
                _probe.Begin();
            }
            else if (what == Pending.Bench)
            {
                _bench.Begin();
            }
        }

        // two seconds of the game running with no menu up, then go
        private void WaitForMenus(float now)
        {
            if (ThirdPersonCamera.IsUiOpen() || Time.timeScale <= 0f)
            {
                _pendingSince = float.MaxValue;
            }
            else if (_pendingSince == float.MaxValue)
            {
                _pendingSince = now;
            }
            else if (now - _pendingSince >= 2f)
            {
                Begin(_pending);
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Performance: PerfDirector.Update");
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
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Keys.Down(KeyAction.PerfBench))
            {
                StartBench();
            }
            else if (Keys.Down(KeyAction.PerfProbe))
            {
                StartProbe();
            }
            if (_probe.Running || _bench.Running)
            {
                Measure();
            }

            float now = Time.unscaledTime;
            if (_pending != Pending.None)
            {
                WaitForMenus(now);
            }
            if (PerfTweaks.Dirty && now >= _settledAt)
            {
                Sync(now);
            }
            if (now >= _nextTick)
            {
                Tick(now);
            }
            ModCulling.Frame();   // one comparison unless a thing of the mod's has changed band (Performance/ModCulling)
        }

        // a measurement that meets something unexpected is dropped, with everything it changed put back
        private void Measure()
        {
            try
            {
                if (_probe.Running)
                {
                    _probe.Step();
                }
                if (_bench.Running)
                {
                    _bench.Step();
                }
            }
            catch (System.Exception e)
            {
                Logger?.Error("Performance: the measurement was dropped and everything put back. " + e);
                _probe.Cancel();
                _bench.Cancel();
                _loop.Close();
            }
        }

        // the first tweak not as asked is switched, and the next frame looks again
        private void Sync(float now)
        {
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                bool want = tweak.ShouldBeActive;
                if (tweak.Stale)
                {
                    tweak.Stale = false;
                    if (want && tweak.Active)
                    {
                        tweak.SetActive(false, Logger);   // its setting moved: the game's own state back, then the new value
                        want = tweak.ShouldBeActive;
                    }
                }
                if (want != tweak.Active)
                {
                    tweak.SetActive(want, Logger);
                    tweak.NextTick = now + tweak.Interval;
                    Logger?.Info("Performance: '" + tweak.Id + "' " + (tweak.Active ? "on. " + tweak.Describe() : "off, the game's own state is back."));
                    Schedule();
                    return;   // one a frame; a tweak that faulted is no longer asked for
                }
            }
            PerfTweaks.Dirty = false;
            Schedule();
        }

        private void Tick(float now)
        {
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                if (tweak.Active && tweak.Interval > 0f && now >= tweak.NextTick)
                {
                    tweak.RunTick(now, Logger);
                    tweak.NextTick = now + tweak.Interval;   // after the tick: a tweak working through the scene asks for a short one
                }
            }
            Schedule();
        }

        private void Schedule()
        {
            _nextTick = float.MaxValue;
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                if (tweak.Active && tweak.Interval > 0f && tweak.NextTick < _nextTick)
                {
                    _nextTick = tweak.NextTick;
                }
            }
        }

        // Through the guard: a handler that throws would stop the engine's other handlers. The tweaks only note the change
        // and look again on their own timers.
        private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> _loaded;
        private static UnityEngine.Events.UnityAction<Scene> _unloaded;
        private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> SceneLoadedCall => _loaded ?? (_loaded = Guard.Listener<Scene, LoadSceneMode>(OnSceneLoaded));
        private static UnityEngine.Events.UnityAction<Scene> SceneUnloadedCall => _unloaded ?? (_unloaded = Guard.Listener<Scene>(OnSceneUnloaded));

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PerfTweaks.WorldChanged(true);

        private static void OnSceneUnloaded(Scene scene) => PerfTweaks.WorldChanged(false);

        /// <summary>A notification from the performance tools, never kept in the save.</summary>
        public static void Notify(string key, float seconds)
        {
            Notifications.Show(NotificationType.Info, key, null, seconds, null, null, notificationSound: false, trackOnSaveGame: false);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoadedCall;
            SceneManager.sceneUnloaded -= SceneUnloadedCall;
            _probe.Cancel();
            _bench.Cancel();
            _loop.Close();   // the game's own frame loop back, whatever happened
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                tweak.Shutdown(Logger);
            }
            ModCulling.Reset();   // the city's things go with it
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
