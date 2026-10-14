using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BAModAPI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A record of the session for "it stutters after a while": with "Developer diagnostics" on, one line a
    /// minute in Player.log and one line for every frame that took longer than 80 ms. Off (as the mod ships)
    /// this component is disabled: the engine does not call it, and nothing is measured.
    ///
    /// The line a minute:
    ///
    ///   Session 12 min: frames 5210, average 11.5 ms, worst 61 ms, over 33 ms: 4, over 50 ms: 1; managed heap
    ///   412 MB of 610 MB, collections in the minute 3, grown by 31.0 MB in the minute (517 KB a second), the
    ///   collector at work 212 ms in the minute and 3.1 ms at most in a frame; engine memory 2950 MB; mod objects
    ///   345 (118 shown) under 6 roots; people 2 out, 12 made of 12; city people 3 out, 8 made of 8, city props
    ///   made 14; looks built in the minute 1 (1 while playing), since the city loaded 23 (3 while playing);
    ///   frames over 80 ms: 2, of them 2 behind the game's black screen (into or out of a building) and 0 seen;
    ///   the mod's navigation obstacles changed in the minute 0; handlers put back on the game's events 0
    ///   (changed events looked into 3); searches of the whole scene 0 (0 while playing); on foot in the street,
    ///   game hour 9.
    ///
    /// How to read it over a session: a managed heap that climbs minute after minute with collections coming
    /// more often is memory being used up on a timer ("grown by" is the allocation budget: what the heap grew by,
    /// frame over frame, the game's share and the mod's together; it should not rise from one version of the mod
    /// to the next); engine memory that climbs is meshes, textures or materials made and never destroyed; mod
    /// objects that climb is something of the mod's that is never put away (they are pooled: the count should
    /// rise while each kind of scene is seen for the first time and then stand still); "over 33 ms" and "over
    /// 50 ms" are the stutters themselves, and "seen" are the long ones the player could see.
    ///
    /// The line for a long frame says what the mod was doing, if one of its large one-off pieces of work was
    /// under way: the places that do such work name it with <see cref="Mark"/> (a character being built, a bus
    /// stop or a precinct front being put out, a scene starting, the save being written), and those that also
    /// say when it was done (<see cref="Done"/>) have its time written beside it. Mark is a handful of writes
    /// into a fixed ring with a constant text and one read of the clock: it costs the same whether diagnostics
    /// are on or off, and it is never to be called per frame or with a text that is put together for the call.
    ///
    /// The same line then says everything that can tell the mod's work from the game's and from the engine's:
    ///
    ///   Session: a frame of 231 ms; none of the mod's large pieces of work was under way; where it went:
    ///   coroutines and timers 204 ms, rendering 14 ms, the rest 13 ms; the collector: no collection (41 so far),
    ///   at work 0.0 ms, heap 731 MB before and 733 MB after; the game: its screen was black or fading (its own
    ///   walk into or out of a building: not seen as a stutter), the player came out of a building in it, not
    ///   saving, no scene loaded or unloaded; the mod's navigation obstacles changed in the second before it and
    ///   in it: 0; the mod's last pieces of work: "people: a character being made" (3.1 ms) 41.0 s before; on
    ///   foot in the street, game hour 7.
    ///
    /// How to read that: time under "navigation" is the engine cutting the walkable mesh (a carving obstacle
    /// changed half a second earlier); time under "coroutines and timers" with the screen black is the game
    /// taking the player into or out of a building; a collection with tens of milliseconds "at work" is the
    /// collector; time under "scripts' Update" with a piece of the mod's work named "in it" is the mod's, and
    /// the time beside the name says how much of the frame it was. Where "the rest" is large, the engine's step
    /// it was mostly in is named beside it (Diagnostics/FrameSteps). After that comes "the mod's own scripts in
    /// it": the time every Update, LateUpdate and FixedUpdate of the mod's and its handlers of the game's events
    /// took in that frame, with the three that took the most, by feature and method (Core/HotSpots). Time under
    /// "scripts' Update" that is not under the mod's own scripts is the game's, or another mod's.
    ///
    /// The line a minute says the same of the minute: the mod's own scripts' time a frame on average and at the
    /// most, the three that took the most, and how much of the heap's growth happened under them (every frame
    /// counted where the runtime counts allocations, else one frame in eight sampled), with the three under which
    /// it grew the most. A frame in which the heap grows by 8 MB or more at once gets a line of its own, with how
    /// much of it was under the mod's own scripts and under which.
    ///
    /// Per frame while recording: a dozen reads of numbers the engine and the game already keep (the frame's
    /// time, the collection count, the heap's size, the collector's time, three of the game's flags) and as many
    /// comparisons; the mod's own scripts' timing, a few microseconds (Core/HotSpots); with the step timing on,
    /// some two hundred and fifty reads of the clock more (Diagnostics/FrameSteps). Nothing is allocated except
    /// for a line that is written.
    /// </summary>
    public sealed class SessionWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("diagnostics", "SessionWatch", typeof(SessionWatch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float Every = 60f;           // seconds between two lines
        private const float LongFrame = 0.080f;    // seconds: a frame this long gets a line of its own
        private const int LongFramesPerMinute = 12;   // lines for long frames in one minute; the rest are only counted
        private const int MarkLastsFrames = 3;     // a piece of work named this many frames back still explains a long frame (loading finishes a frame or two later)
        private const float CutWindow = 1f;        // seconds before a long frame in which a changed navigation obstacle is counted (the engine cuts about half a second after the change)
        private const float NewsLasts = 5f;        // seconds a walk into or out of a building, or a scene coming or going, is still mentioned in a long frame's line
        private const string RootPrefix = "RealLifeOverhaul";
        private const float RestNamed = 5f;        // milliseconds of "the rest" above which the engine's step it was mostly in is named
        private const long Leap = 8L * 1024 * 1024;   // bytes the heap may grow by in one frame before the frame gets a line of its own
        private const int LeapsPerMinute = 6;

        public static IModLogger Logger;

        private static readonly MarkRing Marks = new MarkRing();
        private static SessionWatch _instance;

        private readonly List<GameObject> _roots = new List<GameObject>(64);
        private readonly List<Transform> _under = new List<Transform>(512);
        private readonly StringBuilder _line = new StringBuilder(1024);

        private float _since;
        private float _started;
        private int _frames;
        private float _worst;
        private int _over33;
        private int _over50;
        private int _long;                     // frames over LongFrame in the minute
        private int _longHidden;               // of them, behind the game's black screen
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
        private float _buildingAt = -1000f;    // when the player was last seen to have gone into or come out of a building
        private int _buildingFrame = -1000;
        private bool _buildingIn;
        private float _sceneAt = -1000f;       // when a scene last arrived or went
        private int _sceneFrame = -1000;
        private bool _sceneLoaded;
        private string _sceneName;
        private bool _scenesHooked;
        private int _cuts, _looked, _putBack;  // the counts as they stood when the minute began
        private int _searches, _searchesInPlay;
        private bool _exact;                   // the runtime counts what the main thread allocates (ExactCount)
        private int _jumps;                    // lines for a frame in which the heap leapt, in the minute

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

        /// <summary>
        /// The test "write the session record's lines now" (Options, Tests): the line of a long frame, written for the
        /// frame before this one whatever its length, and the line a minute, written early (its counts then start
        /// again). So that every part of both can be read without waiting for a stutter.
        /// </summary>
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
                    watch._collector.Valid ? watch._collector.LastValue : 0L, UI.UiFader.isFading || watch._fadedLastFrame, SaveGameManager.SavingGameInProgress);
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
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    SceneManager.sceneUnloaded += OnSceneUnloaded;
                }
                FrameSteps.Want(true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the record goes on without what could not be had
            }
            Logger?.Info("Session: recording. One line a minute, and one for every frame over " + (LongFrame * 1000f).ToString("F0") + " ms. The collector has "
                         + (GC.MaxGeneration + 1) + " generation(s); its time in a frame is " + (_collector.Valid ? "read from the engine" : "not offered by this build")
                         + "; the frame's steps are " + (FrameSteps.Installed ? "timed" : "not timed (the option 'Measurements time every step of the frame' is off, or the engine's loop could not be read)")
                         + "; the mod's own scripts are timed by feature and method, and what they allocate is " + (_exact ? "counted in every frame (the runtime counts it)" : "read from the heap's size, one frame in " + HotSpots.AllocEvery) + ".");
        }

        // Whether the runtime counts what the main thread allocates (then the heap's growth under each of the mod's scripts
        // is exact, and read in every frame): a megabyte asked for must show in the count. Once, when the record starts.
        private static bool ExactCount()
        {
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var probe = new byte[1 << 20];
                long after = GC.GetAllocatedBytesForCurrentThread();
                GC.KeepAlive(probe);
                return after - before >= 1 << 20;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnDisable()
        {
            HotSpots.On = false;
            FrameSteps.Want(false);
            if (_collector.Valid)
            {
                _collector.Stop();
            }
            if (_scenesHooked)
            {
                _scenesHooked = false;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
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
            int collections = GC.CollectionCount(0);
            int collectionsBefore = _collectionsLastFrame;
            _collectionsLastFrame = collections;
            long heap = GC.GetTotalMemory(false);
            long heapBefore = _heapLastFrame;
            _heapLastFrame = heap;
            long collectorNanos = _collector.Valid ? _collector.LastValue : 0L;   // the frame before this one, complete
            bool inside = BuildingManager.IsInsideBuilding;
            bool faded = UI.UiFader.isFading;
            bool saving = SaveGameManager.SavingGameInProgress;
            bool hidden = faded || _fadedLastFrame;   // the game's black screen was up, or coming or going, during the frame
            bool saved = saving || _savingLastFrame;
            if (inside != _insideLastFrame)
            {
                _buildingAt = Time.unscaledTime;
                _buildingFrame = Time.frameCount - 1;   // the game moves the player in its coroutines, after this look of the frame before
                _buildingIn = inside;
            }
            _insideLastFrame = inside;
            _fadedLastFrame = faded;
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
                        Long(took, collectionsBefore, collections, heapBefore, heap, collectorNanos, hidden, saved);
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

        private void Long(float took, int collectionsBefore, int collections, long heapBefore, long heap, long collectorNanos, bool hidden, bool saved)
        {
            // The time is that of the frame before this one: work named in this very frame is not what made it long.
            int frame = Time.frameCount - 1;
            float began = Time.unscaledTime - took;
            bool marked = Marks.Get(0, out string mark, out int markFrame, out _, out long markSpent) && frame - markFrame >= 0 && frame - markFrame < MarkLastsFrames;
            _line.Length = 0;
            _line.Append("Session: a frame of ").Append((took * 1000f).ToString("F0")).Append(" ms; ");
            if (marked)
            {
                _line.Append("the mod was at: ").Append(mark);
                long named = Marks.SpentIn(frame);
                if (named > 0)
                {
                    _line.Append(" (the mod's named and timed work in that frame took ").Append(Ms(named).ToString("F1")).Append(" ms of it)");
                }
                else if (markSpent < 0)
                {
                    _line.Append(" (not timed)");
                }
            }
            else
            {
                _line.Append("none of the mod's large pieces of work was under way");
            }

            // Where the engine spent it.
            _line.Append("; where it went: ");
            if (FrameSteps.Valid)
            {
                float rest = took * 1000f;
                bool any = false;
                for (int g = 0; g < FrameSteps.Count; g++)
                {
                    float ms = FrameSteps.LastMs(g);
                    rest -= ms;
                    if (ms >= 1f)
                    {
                        _line.Append(any ? ", " : string.Empty).Append(FrameSteps.Names[g]).Append(' ').Append(ms.ToString("F0")).Append(" ms");
                        any = true;
                    }
                }
                _line.Append(any ? ", " : string.Empty).Append("the rest ").Append(Mathf.Max(0f, rest).ToString("F0")).Append(" ms");
                if (rest >= RestNamed && FrameSteps.LastOther(out string step, out float stepMs) && stepMs >= 1f)
                {
                    _line.Append(" (the most of it in the frame's step ").Append(step).Append(", ").Append(stepMs.ToString("F0")).Append(" ms)");
                }
            }
            else
            {
                _line.Append("not timed");
            }

            // The mod's own scripts: every Update, LateUpdate and FixedUpdate of its, and its handlers of the game's events.
            _line.Append("; the mod's own scripts in it: ");
            double own = HotSpots.FrameMs(frame);
            if (own <= 0.0)
            {
                _line.Append("none ran");
            }
            else
            {
                _line.Append(own.ToString("F1")).Append(" ms");
                int named = 0;
                for (int k = 0; k < 3 && HotSpots.FrameTop(frame, k, out int slot, out double ms) && ms >= 0.05; k++)
                {
                    _line.Append(named++ == 0 ? " (" : ", ").Append(HotSpots.Name(slot)).Append(' ').Append(ms.ToString("F1")).Append(" ms");
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
                _line.Append(", at work ").Append((collectorNanos / 1000000.0).ToString("F1")).Append(" ms");
            }
            _line.Append(", heap ").Append(Megabytes(heapBefore)).Append(" MB before and ").Append(Megabytes(heap)).Append(" MB after");

            // The game.
            _line.Append("; the game: ").Append(hidden
                ? "its screen was black or fading (its own walk into or out of a building: not seen as a stutter)"
                : "its screen was clear");
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
                _line.Append(" (the last: ").Append(MeshCuts.Last).Append(", ").Append(Mathf.Max(0f, cutAgo).ToString("F1")).Append(" s before; the engine cuts about half a second after)");
            }

            // What the mod last did, whether or not it explains the frame.
            _line.Append("; the mod's last pieces of work: ");
            for (int back = 0; back < Marks.Count; back++)
            {
                Marks.Get(back, out string text, out int textFrame, out float at, out long spent);
                _line.Append(back > 0 ? ", \"" : "\"").Append(text).Append("\" (");
                if (spent >= 0)
                {
                    _line.Append(Ms(spent).ToString("F1")).Append(" ms) ");
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

        // A frame in which the managed heap grew by megabytes at once: how much, and how much of it under the mod's own scripts
        // (where that frame was a sample of the heap), and under which. A third of a minute's growth came in one such frame
        // in the 13 October log.
        private void Leapt(long grown, float took)
        {
            int frame = Time.frameCount - 1;
            _line.Length = 0;
            _line.Append("Session: the managed heap grew by ").Append(Megabytes(grown)).Append(" MB in one frame of ").Append((took * 1000f).ToString("F0"))
                .Append(" ms; under the mod's own scripts: ");
            long own = HotSpots.FrameBytes(frame, out bool sampled);
            if (!sampled)
            {
                _line.Append(HotSpots.FrameMs(frame) > 0.0 ? "not known (not a sampled frame)" : "nothing (none ran)");
            }
            else
            {
                _line.Append(own / 1024).Append(" KB");
                for (int k = 0; k < 3 && HotSpots.FrameTopBytes(frame, k, out int slot, out long bytes) && bytes >= 1024; k++)
                {
                    _line.Append(k == 0 ? " (" : ", ").Append(HotSpots.Name(slot)).Append(' ').Append(bytes / 1024).Append(" KB");
                    if (k == 2 || !HotSpots.FrameTopBytes(frame, k + 1, out _, out long next) || next < 1024)
                    {
                        _line.Append(')');
                    }
                }
                _line.Append(own * 2 < grown ? "; the rest is the game's, or another mod's" : string.Empty);
            }
            _line.Append("; the mod's last pieces of work: ");
            float began = Time.unscaledTime - took;
            for (int back = 0; back < Marks.Count; back++)
            {
                Marks.Get(back, out string text, out int textFrame, out float at, out _);
                _line.Append(back > 0 ? ", \"" : "\"").Append(text).Append("\" ");
                Ago(textFrame, at, frame, began);
            }
            if (Marks.Count == 0)
            {
                _line.Append("none yet");
            }
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
        }

        // "in it", "just after it" or "1.2 s before": when something happened, seen from the long frame.
        private void Ago(int happenedFrame, float happenedAt, int frame, float began)
        {
            if (happenedFrame > frame)
            {
                _line.Append("just after it");
            }
            else if (happenedFrame == frame)
            {
                _line.Append("in it");
            }
            else
            {
                _line.Append(Mathf.Max(0f, began - happenedAt).ToString("F1")).Append(" s before");
            }
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private void Minute(int collections)
        {
            float seconds = Time.unscaledTime - _since;
            _since = Time.unscaledTime;
            int objects = 0, shown = 0, roots = 0;
            CountObjects(ref objects, ref shown, ref roots);
            _line.Length = 0;
            _line.Append("Session ").Append(Mathf.RoundToInt((Time.unscaledTime - _started) / 60f)).Append(" min: frames ").Append(_frames)
                .Append(", average ").Append((_frames > 0 ? seconds * 1000f / _frames : 0f).ToString("F1"))
                .Append(" ms, worst ").Append((_worst * 1000f).ToString("F0"))
                .Append(" ms, over 33 ms: ").Append(_over33).Append(", over 50 ms: ").Append(_over50)
                .Append("; managed heap ").Append(Megabytes(GC.GetTotalMemory(false))).Append(" MB of ").Append(Megabytes(Profiler.GetMonoHeapSizeLong()))
                .Append(" MB, collections in the minute ").Append(collections - _collections)
                // The allocation budget: what the heap grew by, frame over frame, and what the collector cost for it.
                .Append(", grown by ").Append((_grown / (1024.0 * 1024.0)).ToString("F1")).Append(" MB in the minute (").Append((seconds > 0f ? _grown / 1024.0 / seconds : 0.0).ToString("F0"))
                .Append(" KB a second)");
            if (_collector.Valid)
            {
                _line.Append(", the collector at work ").Append((_collectorNanos / 1000000.0).ToString("F0")).Append(" ms in the minute and ")
                    .Append((_collectorWorst / 1000000.0).ToString("F1")).Append(" ms at most in a frame");
            }
            _line.Append("; engine memory ").Append(Megabytes(Profiler.GetTotalAllocatedMemoryLong()))
                .Append(" MB; mod objects ").Append(objects).Append(" (").Append(shown).Append(" shown) under ").Append(roots).Append(" roots");
            People(_line);
            // Characters' looks built (a mesh merge each: People/Looks): "while playing" are the ones that could be felt.
            _line.Append("; looks built in the minute ").Append(Looks.Built - _looks).Append(" (").Append(Looks.BuiltInPlay - _looksInPlay)
                .Append(" while playing), since the city loaded ").Append(Looks.Built).Append(" (").Append(Looks.BuiltInPlay).Append(" while playing)");
            _looks = Looks.Built;
            _looksInPlay = Looks.BuiltInPlay;
            // The stutters that were seen, and the two things that must stand still: obstacles of the mod's that cut the
            // navigation mesh, and handlers put back on the game's events (a holder that puts itself back every second shows as 60).
            _line.Append("; frames over ").Append((LongFrame * 1000f).ToString("F0")).Append(" ms: ").Append(_long).Append(", of them ").Append(_longHidden)
                .Append(" behind the game's black screen (into or out of a building) and ").Append(_long - _longHidden).Append(" seen")
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
            _line.Append("; ").Append(Where()).Append('.');
            Logger?.Info(_line.ToString());
            _collections = collections;
            Clear();
        }

        private static long Megabytes(long bytes) => bytes / (1024L * 1024L);

        // The minute of the mod's own scripts (Core/HotSpots): their time a frame, the three that took the most, and the
        // heap's growth under them in the sampled frames, scaled to the minute and set beside the growth of the heap in all.
        private void OwnScripts(float seconds)
        {
            int frames = HotSpots.Frames;
            _line.Append("; the mod's own scripts: ").Append((frames > 0 ? HotSpots.TotalMs / frames : 0.0).ToString("F2")).Append(" ms a frame on average and ")
                .Append(HotSpots.WorstFrameMs.ToString("F1")).Append(" ms at most in a frame");
            for (int k = 0; k < 3 && HotSpots.StretchTop(k, false, out int slot, out double total, out double worst, out _); k++)
            {
                _line.Append(k == 0 ? ", the most in the minute: " : ", ").Append(HotSpots.Name(slot)).Append(" (")
                    .Append((frames > 0 ? total / frames : 0.0).ToString("F2")).Append(" ms a frame, ").Append(worst.ToString("F1")).Append(" ms at worst)");
            }
            int sampled = HotSpots.Sampled;
            if (sampled > 0 && seconds > 0f)
            {
                double scale = (double)HotSpots.Frames / sampled / seconds / 1024.0;   // sampled bytes to kilobytes a second
                _line.Append("; the heap grew under them by ").Append(_exact ? string.Empty : "about ").Append((HotSpots.SampledBytes * scale).ToString("F0"))
                    .Append(_exact ? " KB a second (every frame counted)" : " KB a second (one frame in " + HotSpots.AllocEvery + " sampled)")
                    .Append(" of the ").Append((_grown / 1024.0 / seconds).ToString("F0")).Append(" KB a second it grew in all");
                for (int k = 0; k < 3 && HotSpots.StretchTop(k, true, out int slot, out _, out _, out long bytes) && bytes > 0L; k++)
                {
                    _line.Append(k == 0 ? ", the most under " : ", ").Append(HotSpots.Name(slot)).Append(" (").Append((bytes * scale).ToString("F0")).Append(" KB a second)");
                }
            }
            HotSpots.ClearStretch();
        }

        // Every object under a root object of the mod's (they are all named "RealLifeOverhaul ..."), in every loaded
        // scene. What the mod hangs on the game's own objects (a prop in a hand, a part on a car) is not counted.
        private void CountObjects(ref int objects, ref int shown, ref int roots)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                scene.GetRootGameObjects(_roots);
                for (int r = 0; r < _roots.Count; r++)
                {
                    GameObject root = _roots[r];
                    if (root == null || !root.name.StartsWith(RootPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    roots++;
                    root.GetComponentsInChildren(true, _under);   // fills the list it is given
                    objects += _under.Count;
                    for (int i = 0; i < _under.Count; i++)
                    {
                        if (_under[i].gameObject.activeInHierarchy)
                        {
                            shown++;
                        }
                    }
                    _under.Clear();
                }
                _roots.Clear();
            }
        }

        // The two pools of characters: how many exist and how many are out.
        private static void People(StringBuilder line)
        {
            ActorPool actors = ActorPool.Instance;
            if (actors != null)
            {
                int made = actors.ActorCount, used = 0;
                for (int i = 0; i < made; i++)
                {
                    used += actors.ActorAt(i).InUse ? 1 : 0;
                }
                line.Append("; people ").Append(used).Append(" out, ").Append(made).Append(" made of ").Append(ActorPool.MaxActors);
            }
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city != null && city.People != null && city.Props != null)
            {
                int made = city.People.Count;
                line.Append("; city people ").Append(made - city.People.Free).Append(" out, ").Append(made).Append(" made of ").Append(CityPeople.MaxPeople)
                    .Append(", city props made ").Append(city.Props.Made);
            }
        }

        private static string Where()
        {
            try
            {
                return (BuildingManager.IsInsideBuilding ? "inside a building" : PerfGame.InVehicle() ? "in a vehicle" : "on foot in the street")
                       + ", game hour " + TimeHelper.CurrentHour;
            }
            catch (Exception)
            {
                return "place unknown";
            }
        }
    }
}
