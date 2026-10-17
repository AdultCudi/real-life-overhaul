using BAModAPI;
using UI.Notification;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Runs the performance work in the city: switches the tweaks on and off as the options (or the benchmark)
    /// ask, gives each its timer tick, and starts the probe and the benchmark on their keys (Core/Keys: F7 and Shift+F7 unless rebound).
    /// Its own cost per frame while nothing is being measured: one key test, four flags and one time compare. Tweaks are
    /// switched one a frame (<see cref="Sync"/>): after a load, or when the options change, each switch has a frame of its
    /// own, where every tweak used to be switched in the same frame (a frame of 9 to 20 ms of this director in play).
    /// What each looks at in the scene is taken behind the loading screen (PerfTweaks.Prewarm).
    /// </summary>
    public sealed class PerfDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("performance", "PerfDirector", typeof(PerfDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

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

        // A measurement asked for from the options page waits for the menus to close: nobody wants the menu measured.
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
            // The tweaks' looks at the scene (the skies, the scenery) behind the loading screen, not in the frame they come on.
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

        // Two seconds of the game running with no menu up, then go.
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
        }

        // A measurement that meets something unexpected is dropped, with everything it changed put back.
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

        // Brings the tweaks to the state their options (or the benchmark) ask for, one a frame: the first that is not as asked is
        // switched, and the next frame looks again. Done once every tweak is as asked.
        private void Sync(float now)
        {
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                bool want = tweak.ShouldBeActive;
                if (want != tweak.Active)
                {
                    tweak.SetActive(want, Logger);
                    tweak.NextTick = now + tweak.Interval;
                    Logger?.Info("Performance: '" + tweak.Id + "' " + (tweak.Active ? "on. " + tweak.Describe() : "off, the game's own state is back."));
                    Schedule();
                    return;   // one a frame (a tweak that faulted is off and no longer asked for: it is not switched again)
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

        // The tweaks only note it: they look again on their own timers, a slice at a time, never in this frame.
        // The scene's events, through the guard (a handler that throws would stop the engine's other handlers of it).
        private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> _loaded;
        private static UnityEngine.Events.UnityAction<Scene> _unloaded;
        private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> SceneLoadedCall => _loaded ?? (_loaded = Guard.Listener<Scene, LoadSceneMode>(OnSceneLoaded));
        private static UnityEngine.Events.UnityAction<Scene> SceneUnloadedCall => _unloaded ?? (_unloaded = Guard.Listener<Scene>(OnSceneUnloaded));

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PerfTweaks.WorldChanged(true);

        private static void OnSceneUnloaded(Scene scene) => PerfTweaks.WorldChanged(false);

        /// <summary>A toast from the performance tools; never kept in the savegame.</summary>
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
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
