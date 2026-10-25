using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using BAModAPI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A record of the session for "it stutters after a while": with "Developer diagnostics" on, one line a minute in
    /// Player.log, a second saying what grew since the first minute (<see cref="ObjectKinds"/>), and one line for every
    /// frame over 80 ms or with a heap leap of 8 MB: where the frame went (<see cref="FrameSteps"/>), what the mod was doing
    /// (<see cref="Mark"/>) and the cause the figures point to (<see cref="FrameRules"/>). How to read the lines:
    /// docs/reference/logs-and-debugging.md, "The session record". Off, as shipped, the component is disabled and costs
    /// nothing. On, per frame: a dozen reads of figures the engine and the game keep and as many comparisons, the mod's own
    /// script timing (Core/HotSpots), and with the step timing some two hundred and fifty clock reads and four kernel32
    /// calls. Nothing is allocated but a line that is written.
    /// </summary>
    public sealed partial class SessionWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("diagnostics", "SessionWatch", typeof(SessionWatch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float Every = 60f;           // seconds between two lines
        internal const float LongFrame = 0.080f;    // seconds: a frame this long gets a line of its own
        internal const int LongFramesPerMinute = 12;   // lines for long frames in one minute; the rest are only counted
        private const int MarkLastsFrames = 3;     // a piece of work named this many frames back still explains a long frame (loading finishes a frame or two later)
        private const float CutWindow = 1f;        // seconds before a long frame in which a changed navigation obstacle is counted (the engine cuts about half a second after the change)
        private const float NewsLasts = 5f;        // seconds a walk into or out of a building, or a scene coming or going, is still mentioned in a long frame's line
        private const string RootPrefix = "RealLifeOverhaul";
        private const float RestNamed = 5f;        // milliseconds of "the rest" above which the engine's step it was mostly in is named
        private const double SpanDiffers = 2.0;    // milliseconds by which the steps' own clock may differ from the engine's frame time before the line gives both
        private const long Leap = 8L * 1024 * 1024;   // bytes the heap may grow by in one frame before the frame gets a line of its own
        private const int LeapsPerMinute = 6;

        public static IModLogger Logger;

        private static readonly MarkRing Marks = new MarkRing();
        private static SessionWatch _instance;

        private readonly List<GameObject> _roots = new List<GameObject>(64);
        private readonly List<Transform> _under = new List<Transform>(512);
        private readonly StringBuilder _line = new StringBuilder(1024);
        private readonly ObjectKinds _holders = new ObjectKinds();   // what grows, by holder and by kind, against the first minute
        private readonly ObjectKinds _kinds = new ObjectKinds();
        private Scene _kept;                   // the scene of the objects kept across loads (DontDestroyOnLoad), read once
        private bool _keptRead;
        private int _keptObjects, _keptFirst = -1;
        private long _texturesFirst = -1, _textureBytesFirst;
        private long _afterCollection = -1, _afterCollectionFirst = -1;   // the managed heap in the first frame after a collection: what is kept, not garbage
        private int _linesAtMinute = -1;

        private float _since;
        private float _started;
        private int _frames;
        private float _worst;
        private int _over33;
        private int _over50;
        private int _long;                     // frames over LongFrame in the minute
        private int _longHidden;               // of them, behind the game's black screen or its loading screen
        private int _collections;
        private int _collectionsLastFrame;
        private int _looks;
        private int _looksInPlay;
        private bool _resumed = true;   // the next frame's time is not a frame of play (the record has just started, or a loading screen or a pause has just ended)

        // What the frame before this one is judged against, and the minute's sums (the allocation budget).
        private ProfilerRecorder _collector;   // the engine's own time in the collector, a frame at a time
        private long _heapLastFrame;
        private long _grown;                   // bytes the managed heap grew by, frame over frame, in the minute
        private long _collectorNanos, _collectorWorst;
        private bool _insideLastFrame, _fadedLastFrame, _savingLastFrame;
        private bool _coveredLastFrame;        // the game's black screen was drawn at full strength at the last look (Core/SceneFinds.Covered)
        private bool _loadingLastFrame;        // the game's loading screen was there (up or fading out) at the last look
        private bool _loadingUnread;           // this build's loading screen could not be read: left out from then on
        private float _buildingAt = -1000f;    // when the player was last seen to have gone into or come out of a building
        private int _buildingFrame = -1000;
        private bool _buildingIn;
        private float _sceneAt = -1000f;       // when a scene last arrived or went
        private int _sceneFrame = -1000;
        private bool _sceneLoaded;
        private string _sceneName;
        private bool _scenesHooked;
        private UnityEngine.Events.UnityAction<Scene, LoadSceneMode> _loaded;
        private UnityEngine.Events.UnityAction<Scene> _unloaded;
        private int _cuts, _looked, _putBack;  // the counts as they stood when the minute began
        private int _searches, _searchesInPlay;
        private bool _exact;                   // the runtime counts what the main thread allocates (ExactCount)
        private int _jumps;                    // lines for a frame in which the heap leapt, in the minute

        // Where the minute's frames went, as the long-frame line splits one (FrameSteps, MachineClock, FrameRules).
        private int _split;                    // frames of play the step timing covered whole
        private double _spanSum, _waitSum, _waitWorst, _outsideSum, _outsideWorst;
        private int _threadFrames, _othersFrames;
        private double _threadSum, _threadSpan, _othersSum, _othersWorst;
        private long _faultsSum, _faultsWorst;
        private int _faultFrames;
        private readonly int[] _causes = new int[6];   // the frames over 33 ms, by FrameCause
        private bool _timing;                  // the engine offers its own frame timing (FrameTimingManager)
        private readonly FrameTiming[] _timings = new FrameTiming[4];
        private double _gpuWorst;

        /// <summary>
        /// Names the large piece of work the mod is starting in this frame, for the long-frame line. A constant
        /// text only; never per frame. What it returns is for <see cref="Done"/>.
        /// </summary>
        public static int Mark(string what)
        {
            return Marks.Add(what, Time.frameCount, Time.unscaledTime, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Names a piece of work as <see cref="Mark"/> does, and says it is done (<see cref="Done"/>) at the end of the block:
        /// <c>using var piece = SessionWatch.Piece("...");</c>. Then the long-frame line has its time beside its name.
        /// </summary>
        public static Work Piece(string what)
        {
            return new Work(Mark(what));
        }

        /// <summary>A piece of work named with <see cref="Piece"/>: done when it is disposed.</summary>
        public readonly struct Work : IDisposable
        {
            private readonly int _mark;

            internal Work(int mark)
            {
                _mark = mark;
            }

            public void Dispose()
            {
                Done(_mark);
            }
        }

        /// <summary>
        /// The piece of work that <see cref="Mark"/> named is done: its time is written beside its name in a long
        /// frame's line, which is what tells the mod's share of a frame from the game's. One read of the clock.
        /// </summary>
        public static void Done(int mark)
        {
            Marks.Close(mark, Stopwatch.GetTimestamp());
        }

        /// <summary>The test "write the session record's lines now": a long-frame line for the frame before this one and the line a minute, early.</summary>
        public static void TestSay()
        {
            try
            {
                SessionWatch watch = _instance;
                if (watch == null || !watch.enabled)
                {
                    TestRefusals.TellKey(Logger, "Session record", "reallife_perf_test_session_off");
                    return;
                }
                HotSpots.Settle(Time.frameCount);
                int collections = GC.CollectionCount(0);
                long heap = GC.GetTotalMemory(false);
                watch.Long(Time.unscaledDeltaTime, watch._collectionsLastFrame, collections, watch._heapLastFrame, heap,
                    watch._collector.Valid ? watch._collector.LastValue : 0L,
                    FrameRules.Screen(UI.UiFader.isFading, watch._fadedLastFrame, watch._coveredLastFrame, watch.LoadingThere(), watch._loadingLastFrame), SaveGameManager.SavingGameInProgress);
                watch.Minute(collections);
                PerfDirector.Notify("reallife_perf_test_session_done", 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Starts or stops the record with the "Developer diagnostics" switch (Meta.Dev.Apply).</summary>
        public static void Apply(bool on)
        {
            if (_instance != null && _instance.enabled != on)
            {
                _instance.enabled = on;
            }
        }

        private void Awake()
        {
            _instance = this;
            HotSpots.FrameSource = () => Time.frameCount;          // what the mod's own scripts' timing reads (Core/HotSpots)
            enabled = false;   // Meta.Dev.Apply switches it on when the option is
        }

        private void OnEnable()
        {
            _since = Time.unscaledTime;
            _resumed = true;
            if (_started <= 0f)
            {
                _started = Time.unscaledTime;
            }
            Clear();
            _exact = ExactCount();
            HotSpots.HeapSource = _exact ? (Func<long>)GC.GetAllocatedBytesForCurrentThread : () => GC.GetTotalMemory(false);
            HotSpots.SampleEvery = _exact ? 1 : HotSpots.AllocEvery;
            HotSpots.Reset();
            HotSpots.On = true;   // the mod's own scripts are timed from now on (Core/HotSpots)
            _collections = GC.CollectionCount(0);
            _collectionsLastFrame = _collections;
            _heapLastFrame = GC.GetTotalMemory(false);
            _looks = Looks.Built;
            _looksInPlay = Looks.BuiltInPlay;
            _cuts = MeshCuts.Total;
            _looked = EventHold.Looked;
            _putBack = EventHold.PutBack;
            _searches = SceneFinds.Made;
            _searchesInPlay = SceneFinds.InPlay;
            try
            {
                if (!_collector.Valid)
                {
                    _collector = PerfSampler.Find("GC.Collect", ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame);
                }
                if (_collector.Valid)
                {
                    _collector.Start();
                }
                if (!_scenesHooked)
                {
                    _scenesHooked = true;
                    _loaded = _loaded ?? Guard.Listener<Scene, LoadSceneMode>(OnSceneLoaded);   // (made once, through the guard: Core/Guard)
                    _unloaded = _unloaded ?? Guard.Listener<Scene>(OnSceneUnloaded);
                    SceneManager.sceneLoaded += _loaded;
                    SceneManager.sceneUnloaded += _unloaded;
                }
                FrameSteps.Want(true);
                _timing = TimingOffered();
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the record goes on without what could not be had
            }
            Logger?.Info("Session: recording. One line a minute, and one for every frame over " + (LongFrame * 1000f).ToString("F0") + " ms. The collector has "
                         + (GC.MaxGeneration + 1) + " generation(s); its time in a frame is " + (_collector.Valid ? "read from the engine" : "not offered by this build")
                         + "; the frame's steps are "
                         + (FrameSteps.Installed ? "timed" : "not timed (the option 'Measurements time every step of the frame' is off, or the engine's loop could not be read)")
                         + "; the mod's own scripts are timed by feature and method, and what they allocate is "
                         + (_exact ? "counted in every frame (the runtime counts it)" : "read from the heap's size, one frame in " + HotSpots.AllocEvery) + ".");
            Logger?.Info("Session: the frame's pace: " + Pace() + "; the engine's own frame timing (the graphics card's time) is " + (_timing ? "offered" : "not offered by this build")
                         + "; processors " + SystemInfo.processorCount + "; the system's figures for each frame (kernel32): " + MachineClock.Status()
                         + (FrameSteps.Installed ? "; the frame's steps: " + FrameSteps.Shape : string.Empty) + ".");
        }

        private void OnDisable()
        {
            LetGoOfDay();
            HotSpots.On = false;
            FrameSteps.Want(false);
            if (_collector.Valid)
            {
                _collector.Stop();
            }
            if (_scenesHooked)
            {
                _scenesHooked = false;
                SceneManager.sceneLoaded -= _loaded;
                SceneManager.sceneUnloaded -= _unloaded;
            }
        }

        private void OnDestroy()
        {
            if (_collector.Valid)
            {
                _collector.Dispose();
            }
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SceneNews(scene, true);

        private void OnSceneUnloaded(Scene scene) => SceneNews(scene, false);

        private void SceneNews(Scene scene, bool loaded)
        {
            _sceneAt = Time.unscaledTime;
            _sceneFrame = Time.frameCount;
            _sceneLoaded = loaded;
            _sceneName = scene.name;
        }

        private void Clear()
        {
            _frames = 0;
            _jumps = 0;
            _worst = 0f;
            _over33 = 0;
            _over50 = 0;
            _long = 0;
            _longHidden = 0;
            _grown = 0;
            _collectorNanos = 0;
            _collectorWorst = 0;
            _split = 0;
            _spanSum = _waitSum = _waitWorst = _outsideSum = _outsideWorst = 0.0;
            _threadFrames = _othersFrames = _faultFrames = 0;
            _threadSum = _threadSpan = _othersSum = _othersWorst = 0.0;
            _faultsSum = _faultsWorst = 0L;
            Array.Clear(_causes, 0, _causes.Length);
            _gpuWorst = 0.0;
        }

        private void Update()
        {
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
            HotSpots.Settle(Time.frameCount);   // the mod's own scripts' figures for the frame before this one are complete
            // The time the frame before this one took, loading screens and pauses left out: they are not stutter.
            float took = Time.unscaledDeltaTime;
            Midnight(took);
            int collections = GC.CollectionCount(0);
            int collectionsBefore = _collectionsLastFrame;
            _collectionsLastFrame = collections;
            long heap = GC.GetTotalMemory(false);
            long heapBefore = _heapLastFrame;
            _heapLastFrame = heap;
            if (collections != collectionsBefore)
            {
                _afterCollection = heap;
            }
            long collectorNanos = _collector.Valid ? _collector.LastValue : 0L;   // the frame before this one, complete
            bool inside = BuildingManager.IsInsideBuilding;
            bool faded = UI.UiFader.isFading;
            bool covered = faded && SceneFinds.Covered;   // (its overlay read only while it fades)
            bool loading = LoadingThere();
            bool saving = SaveGameManager.SavingGameInProgress;
            // A frame under the loading screen (it fades out for 0.8 s after isLoading goes false) or the black screen at full
            // strength as it began is not seen; its fades, the picture showing through, are.
            FrameScreen screen = FrameRules.Screen(faded, _fadedLastFrame, _coveredLastFrame, loading, _loadingLastFrame);
            bool hidden = FrameRules.Unseen(screen);
            bool saved = saving || _savingLastFrame;
            if (inside != _insideLastFrame)
            {
                _buildingAt = Time.unscaledTime;
                _buildingFrame = Time.frameCount - 1;   // the game moves the player in its coroutines, after this look of the frame before
                _buildingIn = inside;
            }
            _insideLastFrame = inside;
            _fadedLastFrame = faded;
            _coveredLastFrame = covered;
            _loadingLastFrame = loading;
            _savingLastFrame = saving;
            if (UI.Load.LoadScene.isLoading || Time.timeScale <= 0f || !Application.isFocused)
            {
                _since += took;   // the minute is a minute of play
                _resumed = true;
                return;
            }
            if (_resumed)
            {
                _resumed = false;   // the time in hand is the last frame of the loading screen or the pause, not a frame of play
                return;
            }
            _frames++;
            HotSpots.Keep(Time.frameCount - 1);   // the frame before this one was play: the mod's own scripts' share of it goes into the minute
            if (heap > heapBefore)
            {
                _grown += heap - heapBefore;   // what was allocated, less what a collection gave back in the same frame
            }
            _collectorNanos += collectorNanos;
            if (collectorNanos > _collectorWorst)
            {
                _collectorWorst = collectorNanos;
            }
            if (took > _worst)
            {
                _worst = took;
            }
            if (took > 0.033f)
            {
                _over33++;
                if (took > 0.050f)
                {
                    _over50++;
                }
            }
            try
            {
                Split(took);
                if (heap - heapBefore >= Leap && ++_jumps <= LeapsPerMinute)
                {
                    Leapt(heap - heapBefore, took);
                }
                if (took > LongFrame)
                {
                    _long++;
                    _longHidden += hidden ? 1 : 0;
                    if (_long <= LongFramesPerMinute)   // the rest are counted in the minute's line; a bad minute does not fill the log
                    {
                        Long(took, collectionsBefore, collections, heapBefore, heap, collectorNanos, screen, saved);
                    }
                }
                if (Time.unscaledTime - _since >= Every)
                {
                    Minute(collections);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                enabled = false;   // a record must never be the fault
            }
        }

        // Whether the game's loading screen is up or fading out, from its register of singletons (no search); a build where it
        // cannot be read leaves it out from then on, with one line.
        private bool LoadingThere()
        {
            if (_loadingUnread)
            {
                return false;
            }
            try
            {
                return LoadingScreenUp();
            }
            catch (Exception e)
            {
                _loadingUnread = true;
                Logger?.Warn("Session: the game's loading screen cannot be read in this build (" + e.GetType().Name + "); a frame while it fades out is counted as one the player saw.");
                return false;
            }
        }

        // In a method of its own: a build without the loading screen's type fails here alone (Mono compiles a method whole).
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool LoadingScreenUp() => InstanceBehavior<UI.Load.LoadingScreen>.IsInitialized;

        private void Long(float took, int collectionsBefore, int collections, long heapBefore, long heap, long collectorNanos, FrameScreen screen, bool saved)
        {
            // The time is that of the frame before this one: work named in this very frame is not what made it long.
            int frame = Time.frameCount - 1;
            float began = Time.unscaledTime - took;
            int pick = Marks.Costliest(frame);   // the costliest piece closed in that frame, else the newest (Core/WorkRings)
            bool marked = Marks.Get(pick, out string mark, out int markFrame, out _, out long markSpent) && frame - markFrame >= 0 && frame - markFrame < MarkLastsFrames;
            _line.Length = 0;
            _line.Append("Session: a frame of ").Append((took * 1000f).ToString("F0")).Append(" ms; ");
            if (marked)
            {
                _line.Append("the mod was at: ").Append(mark);
                long named = Marks.SpentIn(frame);
                if (named > 0)
                {
                    _line.Append(" (the mod's named and timed work in that frame took ").Append(Ms(named).ToString("F1", CultureInfo.InvariantCulture)).Append(" ms of it)");
                }
                else if (markSpent < 0)
                {
                    _line.Append(" (not timed)");
                }
                if (pick > 0 && Marks.Get(0, out string newest, out _, out _, out _))
                {
                    _line.Append(" (the newest piece of work then: ").Append(newest).Append(')');   // so both are seen
                }
            }
            else
            {
                _line.Append("none of the mod's large pieces of work was under way");
            }

            // Where the engine spent it: cut where the engine cuts the frame, so the parts add up to its length.
            _line.Append("; where it went: ");
            FrameFigures figures = Figures(took, true);
            if (FrameSteps.Valid)
            {
                bool any = false;
                for (int g = 0; g < FrameSteps.Count; g++)
                {
                    float ms = FrameSteps.LastMs(g);
                    if (ms >= 1f)
                    {
                        _line.Append(any ? ", " : string.Empty).Append(FrameSteps.Names[g]).Append(' ').Append(ms.ToString("F0")).Append(" ms");
                        any = true;
                    }
                }
                double rest = FrameSteps.LastRestMs;
                _line.Append(any ? ", " : string.Empty).Append("the rest ").Append(rest.ToString("F0")).Append(" ms");
                if (rest >= RestNamed && FrameSteps.LastOther(out string step, out float stepMs) && stepMs >= 1f)
                {
                    _line.Append(" (the most of it in the frame's step ").Append(step).Append(", ").Append(stepMs.ToString("F0")).Append(" ms)");
                }
                _line.Append(", outside the engine's steps ").Append(figures.OutsideMs.ToString("F0")).Append(" ms");
                if (Math.Abs(figures.SpanMs - took * 1000.0) >= SpanDiffers)
                {
                    _line.Append(" (by the steps' own clock the frame took ").Append(figures.SpanMs.ToString("F0")).Append(" ms)");
                }
            }
            else
            {
                _line.Append("not timed");
            }
            FrameRules.Describe(_line, figures, FrameSteps.Valid ? MachineClock.ThreadWhy : "not measured (the frame's steps are not timed)",
                FrameSteps.Valid ? MachineClock.SystemWhy : "not measured", FrameSteps.Valid ? MachineClock.MemoryWhy : "not measured",
                _timing ? "none yet" : "not offered by this build");

            // The mod's own scripts: every Update, LateUpdate and FixedUpdate of its, and its handlers of the game's events.
            _line.Append("; the mod's own scripts in it: ");
            double own = HotSpots.FrameMs(frame);
            if (own <= 0.0)
            {
                _line.Append("none ran");
            }
            else
            {
                _line.Append(own.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
                int named = 0;
                for (int k = 0; k < 3 && HotSpots.FrameTop(frame, k, out int slot, out double ms) && ms >= 0.05; k++)
                {
                    _line.Append(named++ == 0 ? " (" : ", ").Append(HotSpots.Name(slot)).Append(' ').Append(ms.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
                }
                if (named > 0)
                {
                    _line.Append(')');
                }
            }

            // The collector.
            _line.Append("; the collector: ");
            if (collections != collectionsBefore)
            {
                _line.Append("a collection finished in it (").Append(collectionsBefore).Append(" before, ").Append(collections).Append(" after)");
            }
            else
            {
                _line.Append("no collection (").Append(collections).Append(" so far)");
            }
            if (_collector.Valid)
            {
                _line.Append(", at work ").Append((collectorNanos / 1000000.0).ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
            }
            _line.Append(", heap ").Append(Megabytes(heapBefore)).Append(" MB before and ").Append(Megabytes(heap)).Append(" MB after");

            // The game.
            _line.Append("; the game: ").Append(FrameRules.ScreenWords(screen));
            if (began - _buildingAt < NewsLasts)
            {
                _line.Append(_buildingIn ? ", the player went into a building " : ", the player came out of a building ");
                Ago(_buildingFrame, _buildingAt, frame, began);
            }
            _line.Append(saved ? ", saving" : ", not saving");
            if (began - _sceneAt < NewsLasts)
            {
                _line.Append(", the scene '").Append(_sceneName).Append(_sceneLoaded ? "' loaded " : "' unloaded ");
                Ago(_sceneFrame, _sceneAt, frame, began);
            }
            else
            {
                _line.Append(", no scene loaded or unloaded");
            }

            // The navigation mesh: only the mod's own obstacles are known here.
            _line.Append("; the mod's navigation obstacles changed in the second before it and in it: ").Append(MeshCuts.Between(began - CutWindow, Time.unscaledTime));
            float cutAgo = began - MeshCuts.At;
            if (MeshCuts.Last != null && cutAgo < 2.5f)
            {
                _line.Append(" (the last: ").Append(MeshCuts.Last).Append(", ").Append(Mathf.Max(0f, cutAgo).ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" s before; the engine cuts about half a second after)");
            }

            // What the mod last did, whether or not it explains the frame.
            _line.Append("; the mod's last pieces of work: ");
            for (int back = 0; back < Marks.Count; back++)
            {
                Marks.Get(back, out string text, out int textFrame, out float at, out long spent);
                _line.Append(back > 0 ? ", \"" : "\"").Append(text).Append("\" (");
                if (spent >= 0)
                {
                    _line.Append(Ms(spent).ToString("F1", CultureInfo.InvariantCulture)).Append(" ms) ");
                }
                else
                {
                    _line.Append("not timed) ");
                }
                Ago(textFrame, at, frame, began);
            }
            if (Marks.Count == 0)
            {
                _line.Append("none yet");
            }
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
        }

        private void Minute(int collections)
        {
            float seconds = Time.unscaledTime - _since;
            _since = Time.unscaledTime;
            int minute = Mathf.RoundToInt((Time.unscaledTime - _started) / 60f);
            int objects = 0, shown = 0, roots = 0;
            CountObjects(ref objects, ref shown, ref roots, minute);
            _line.Length = 0;
            _line.Append("Session ").Append(minute).Append(" min: frames ").Append(_frames)
                .Append(", average ").Append((_frames > 0 ? seconds * 1000f / _frames : 0f).ToString("F1", CultureInfo.InvariantCulture))
                .Append(" ms, worst ").Append((_worst * 1000f).ToString("F0"))
                .Append(" ms, over 33 ms: ").Append(_over33).Append(", over 50 ms: ").Append(_over50)
                .Append("; managed heap ").Append(Megabytes(GC.GetTotalMemory(false))).Append(" MB of ").Append(Megabytes(Profiler.GetMonoHeapSizeLong()))
                .Append(" MB, collections in the minute ").Append(collections - _collections)
                // The allocation budget: what the heap grew by, frame over frame, and what the collector cost for it.
                .Append(", grown by ").Append((_grown / (1024.0 * 1024.0)).ToString("F1", CultureInfo.InvariantCulture)).Append(" MB in the minute (")
                    .Append((seconds > 0f ? _grown / 1024.0 / seconds : 0.0).ToString("F0"))
                .Append(" KB a second)");
            if (_collector.Valid)
            {
                _line.Append(", the collector at work ").Append((_collectorNanos / 1000000.0).ToString("F0")).Append(" ms in the minute and ")
                    .Append((_collectorWorst / 1000000.0).ToString("F1", CultureInfo.InvariantCulture)).Append(" ms at most in a frame");
            }
            _line.Append("; engine memory ").Append(Megabytes(Profiler.GetTotalAllocatedMemoryLong()))
                .Append(" MB; mod objects ").Append(objects).Append(" (").Append(shown).Append(" shown) under ").Append(roots).Append(" roots");
            People(_line);
            // Characters' looks built (a mesh merge each: People/Looks): "while playing" are the ones that could be felt.
            _line.Append("; looks built in the minute ").Append(Looks.Built - _looks).Append(" (").Append(Looks.BuiltInPlay - _looksInPlay)
                .Append(" while playing), since the city loaded ").Append(Looks.Built).Append(" (").Append(Looks.BuiltInPlay).Append(" while playing)");
            _looks = Looks.Built;
            _looksInPlay = Looks.BuiltInPlay;
            // The stutters seen, and two counts that must stand still (a holder putting itself back every second shows as 60).
            _line.Append("; frames over ").Append((LongFrame * 1000f).ToString("F0")).Append(" ms: ").Append(_long).Append(", of them ").Append(_longHidden)
                .Append(" behind the game's black screen (into or out of a building) or its loading screen and ").Append(_long - _longHidden).Append(" seen")
                .Append("; the mod's navigation obstacles changed in the minute ").Append(MeshCuts.Total - _cuts)
                .Append("; handlers put back on the game's events ").Append(EventHold.PutBack - _putBack).Append(" (changed events looked into ").Append(EventHold.Looked - _looked).Append(')');
            // Searches of the whole scene (Core/SceneFinds): each is a frame of about 110 ms, so none belongs in play.
            _line.Append("; searches of the whole scene ").Append(SceneFinds.Made - _searches).Append(" (").Append(SceneFinds.InPlay - _searchesInPlay).Append(" while playing)");
            _cuts = MeshCuts.Total;
            _looked = EventHold.Looked;
            _putBack = EventHold.PutBack;
            _searches = SceneFinds.Made;
            _searchesInPlay = SceneFinds.InPlay;
            OwnScripts(seconds);
            Went();
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
            Grew(minute);
            _collections = collections;
            Clear();
        }

        // The second line a minute: what grew most since the first minute, so that a leak names itself.
        private void Grew(int minute)
        {
            long textures = (long)Texture.nonStreamingTextureCount, bytes = (long)Texture.nonStreamingTextureMemory;
            if (_texturesFirst < 0)
            {
                _texturesFirst = textures;
                _textureBytesFirst = bytes;
            }
            if (_keptFirst < 0)
            {
                _keptFirst = _keptObjects;
            }
            _line.Length = 0;
            _line.Append("Session ").Append(minute).Append(" min, what grew since minute ").Append(_holders.FirstMinute).Append(": textures ").Append(textures)
                .Append(" (").Append(Signed(textures - _texturesFirst)).Append("), ").Append(Megabytes(bytes)).Append(" MB (")
                .Append(Signed(Megabytes(bytes) - Megabytes(_textureBytesFirst))).Append(')');
            _line.Append("; the mod's holders: ");
            Growers(_holders);
            _line.Append("; the kinds below them: ");
            Growers(_kinds);
            _line.Append("; in the scene kept across loads ").Append(_keptObjects).Append(" of the mod's objects (").Append(Signed(_keptObjects - _keptFirst)).Append(")");
            _line.Append(" (").Append(_holders.Count).Append(" holders and ").Append(_kinds.Count).Append(" kinds counted)");
            // What the heap keeps (read just after a collection, so garbage is not in it), and the log's own share of it.
            if (_afterCollection >= 0 && _afterCollectionFirst < 0)
            {
                _afterCollectionFirst = _afterCollection;
            }
            _line.Append("; the managed heap after its last collection ");
            if (_afterCollection >= 0)
            {
                _line.Append(Megabytes(_afterCollection)).Append(" MB (").Append(Signed(Megabytes(_afterCollection) - Megabytes(_afterCollectionFirst))).Append(" since the first collection seen)");
            }
            else
            {
                _line.Append("not seen yet");
            }
            int lines = Meta.Dev.Lines;
            _line.Append("; the mod's log lines in the minute ").Append(_linesAtMinute >= 0 ? lines - _linesAtMinute : 0).Append(", ").Append(lines)
                .Append(" this run (the game keeps every log line in memory for the whole run).");
            _linesAtMinute = lines;
            Logger?.Info(_line.ToString());
        }

        private void CountScene(Scene scene, ref int objects, ref int shown, ref int roots)
        {
            scene.GetRootGameObjects(_roots);
            for (int r = 0; r < _roots.Count; r++)
            {
                GameObject root = _roots[r];
                string rootName = root != null ? root.name : null;
                if (rootName == null || !rootName.StartsWith(RootPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                roots++;
                objects++;
                shown += root.activeInHierarchy ? 1 : 0;
                _holders.Add(ObjectKinds.Key(rootName, null, null), 1);
                Transform top = root.transform;
                for (int h = 0; h < top.childCount; h++)
                {
                    Transform holder = top.GetChild(h);
                    string holderName = holder.name;
                    int held = 1;
                    shown += holder.gameObject.activeInHierarchy ? 1 : 0;
                    for (int b = 0; b < holder.childCount; b++)
                    {
                        Transform below = holder.GetChild(b);
                        below.GetComponentsInChildren(true, _under);   // fills the list it is given
                        for (int i = 0; i < _under.Count; i++)
                        {
                            shown += _under[i].gameObject.activeInHierarchy ? 1 : 0;
                        }
                        held += _under.Count;
                        _kinds.Add(ObjectKinds.Key(rootName, holderName, below.name), _under.Count);
                        _under.Clear();
                    }
                    objects += held;
                    _holders.Add(ObjectKinds.Key(rootName, holderName, null), held);
                }
            }
            _roots.Clear();
        }

        // The scene the engine keeps objects in across loads has no handle of its own: an object put there for a moment says
        // which it is. Once.
        private Scene KeptScene()
        {
            if (!_keptRead)
            {
                _keptRead = true;
                var probe = new GameObject("RealLifeOverhaul Scene Probe");
                DontDestroyOnLoad(probe);
                _kept = probe.scene;
                DestroyImmediate(probe);
            }
            return _kept;
        }
    }
}
