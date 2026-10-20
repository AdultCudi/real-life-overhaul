using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// RLO Texts: a small text model on the player's own PC words the city's messages, so that the social app no longer
    /// gives the same generic answer in every inbox. The game
    /// decides everything as before (who, when, what happens, every effect); the model is asked only for the words, from a
    /// card built from the game's real state (TextIntents, byte for byte the cards it was trained on), and every word is
    /// checked (TextCheck) before it is shown. Whenever the model is not there, off, loading, busy, slow, or its words fail,
    /// the written lines are shown exactly as before: the player never waits on the model.
    ///
    /// THE RUNNER. llama.cpp's own server (llama-server.exe) as a child process of the game on 127.0.0.1 (TextServer), not
    /// llama.cpp's DLLs loaded into the game by P/Invoke. The reasons: a native crash or a memory fault in llama.cpp would
    /// take the whole game down in-process, and in a child it ends only the child (the mod sees it and uses the written
    /// lines); Mono in Unity loads native DLLs by name from its own search path, so a second llama.dll or ggml.dll (another
    /// mod's, a different build) could clash or be held locked, and the pack could not be switched without restarting the
    /// game; a GPU back end (Vulkan) inside the game's process would share the game's device and driver state; the model's
    /// memory (0.4 to 1.3 GB) is outside the game's own address space and its Boehm collector's view. The costs: an exe in a
    /// Workshop item (Steam allows it; an antivirus may look at it, so it is llama.cpp's own release build, unchanged and
    /// named as it is, with its licence and a README), a second or two to start (behind the loading screen or the first minutes of
    /// play, never waited for), and a local socket per request (a few hundred microseconds). The server listens on
    /// 127.0.0.1 only, on a free port of the moment, with a key of its own, below normal priority on the processor and at
    /// normal priority on a GPU (TextServer.LowPriority: the GPU's work waits on a thread the game must not starve), tied to the game's
    /// process by a job object so it ends with the game even in a crash, and is killed when the game closes, the option is
    /// switched off or the pack changes.
    ///
    /// THE QUEUE (TextQueue): one queue, by priority and deadline; what can no longer be
    /// written in time takes the written line at once; generated ahead for what the game schedules later; a budget a minute
    /// that adapts to misses (TextBudget); background work only while the game is quiet; a guess before a click face to
    /// face. SPEED (TextSpeed): a test on first load or a change of pack, device or threads; threads by the machine's cores;
    /// a notice suggesting the Lite pack when the full one is too slow; a slow machine writes only what is asked ahead.
    /// GOING PUBLIC (TextsShare): personal text on each player's own machine, shared text written once by the host and sent
    /// as text in part T; a guest never writes shared text and checks the host's again.
    ///
    /// COST: nothing per frame but one time comparison. A look twice a second while idle (a few comparisons), ten times a
    /// second while requests wait (a pass over a handful); a request is a card of a few hundred characters; the model runs
    /// in its own process on its own threads, the socket on one background thread of the mod.
    /// </summary>
    public sealed partial class TextsDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("texts", "TextsDirector", typeof(TextsDirector));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Texts: TextsDirector.Update");
        private static readonly int HotLook = HotSpots.Slot("Texts: TextsDirector.Look");

        private const float IdleEvery = 0.5f;      // real seconds between two looks with nothing waiting
        private const float BusyEvery = 0.1f;      // ...while requests wait (a reply on the GPU comes back in 0.05 s)
        private const float HookEvery = 2f;        // ...between two checks that the save hook is attached
        private const int DevicesWaitMs = 8000;    // the most llama.cpp may take to list its devices
        private const int WhyMost = 60;            // characters of a reason in the status notice

        public static IModLogger Logger;
        public static TextsDirector Instance { get; private set; }

        // Kept across saves and scenes: the server, its queue and what it has written (one model loaded once a session).
        private static readonly TextQueue Queue = new TextQueue();
        private static readonly TextQueue ServiceQueue = new TextQueue();   // Your own AI's (TextsDirector.Service)
        private static readonly TextsHub Hub = new TextsHub(Queue, ServiceQueue);
        private static TextServer _server;
        private static TextPack _pack;
        private static string _device, _deviceLabel, _serverFor;
        private static int _threads, _cores;
        private static volatile bool _finding;
        private static bool _found, _quitHooked, _speedTold, _refusedTold;
        private static Action _onQuit;
        private static List<TextPack> _packs;
        private static TextPack _verified, _refused;       // the best pack whose runtime is llama.cpp's own; the first that was not
        private static string _refusedWhy;
        private static int _runDevice = -1, _runThreads = -1;   // the options the server was started with
        private static List<TextDevice> _devices;
        private static readonly System.Text.StringBuilder Changes = new System.Text.StringBuilder(1024);

        private float _next, _last, _nextHook, _frameTime;
        private int _frameCount;
        private GameInstance _save;
        private bool _loaded;
        private int _prunedDay = -1;
        private Action _onSave;

        /// <summary>Whether the game still has what a kept text's key belongs to (a post, a message, an edition); set by Wiring/TextsLinks.</summary>
        public static Func<string, bool> StillThere;

        /// <summary>The hub requests go through (Wiring/TextsLinks hands it to the features' bridges).</summary>
        public static TextsHub Words => Hub;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSaveGame;
            Hub.Allowed = Allowed;
            Hub.SharedKept = SharedKept;
            if (!_quitHooked)
            {
                _quitHooked = true;
                _onQuit = Guard.Wrap(StopOnQuit);   // (made once, kept: the game's process ends the server with it either way)
                Application.quitting += _onQuit;
            }
            SharedCity.Register(new SharedCityPart
            {
                Id = TextsShare.Part, Name = "RLO Texts' shared words",
                Ready = () => _loaded,
                Write = () => _loaded ? TextsShare.Whole(Hub.Store) : null,
                Changes = TakeChanges,
                Apply = FromHost,
                Left = () => Hub.Store.ClearReceived()
            });
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                Hub.Clear();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static void StopOnQuit()
        {
            try
            {
                StopServer("the game is closing", true);
            }
            catch (Exception)
            {
                // (the job object ends it with the process anyway)
            }
        }

        // ---------------------------------------------------------------- running

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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            float since = now - _last;
            _last = now;
            _next = now + (Hub.Pending > 0 ? BusyEvery : IdleEvery);
            Look(now, since);
        }

        private void Look(float now, float since)
        {
            using var hotSpot = HotSpots.Time(HotLook);
            int frames = Time.frameCount - _frameCount;
            _frameCount = Time.frameCount;
            _frameTime = frames > 0 && since > 0f ? since * 1000f / frames : 0f;   // (frames counted between two looks: the mean frame since the last)
            SignInLook();
            SignOutLook();
            if (!Settings.Texts)
            {
                if (_server != null)
                {
                    StopServer("switched off");
                }
                StopService("RLO Texts switched off");
                Hub.Clear();
                Keep(now);
                return;
            }
            Keep(now);
            if (!_found)
            {
                Find();
                return;
            }
            if (!_loaded)
            {
                return;   // (the main menu: the model is started only once a save is loaded, so its memory is used only while playing)
            }
            Run();
            RunService();
            if (_pack == null && _service == null)
            {
                Hub.Clear();
                return;
            }
            bool quiet = TextsGame.Quiet(_frameTime);
            if (_service != null)
            {
                _service.Quiet = quiet;
            }
            if (_server != null)
            {
                _server.Quiet = quiet;
                if (_server.State == TextServerState.Ready && !_speedTold)
                {
                    Measured();
                }
            }
            Hub.Look(TextClock.Now);
        }

        // The save's line of kept words: read on a new save, written at every save; the save hook kept attached.
        private void Keep(float now)
        {
            GameInstance save = SaveGameManager.Current;
            if (_loaded && !ReferenceEquals(save, _save))
            {
                Hub.Clear();
                Hub.Store.Clear();
                _loaded = false;
                _save = null;
            }
            if (save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!_loaded)
            {
                Read(save);
            }
            if (now >= _nextHook)
            {
                _nextHook = now + HookEvery;
                SaveHook.Keep(_onSave);   // the game clears its event hub on every load: the save hook is kept attached
            }
            if (save.Day != _prunedDay && StillThere != null)
            {
                _prunedDay = save.Day;
                int gone = Hub.Store.Prune(StillThere);
                if (gone > 0)
                {
                    Logger?.Info("RLO Texts: " + gone + " kept text(s) let go: what they belonged to is gone from the game.");
                }
            }
        }

        private void Read(GameInstance save)
        {
            _save = save;
            _loaded = true;
            string line = null;
            if (save.modData != null && save.modData.TryGetValue(TextsStore.SaveKey, out line) && !Hub.Store.Read(line))
            {
                SaveLines.Unreadable(save.modData, TextsStore.SaveKey, "it could not be read");
                Logger?.Warn("RLO Texts: the save's line of kept words could not be read (" + TextsStore.SaveKey + "); the written lines are shown for what it held, and the line is kept as it is.");
            }
            Logger?.Info("RLO Texts: " + Hub.Store.Count + " kept text(s) read from the save.");
        }

        private void OnSaveGame()
        {
            try
            {
                int flushed = Hub.FlushForSave();
                if (flushed > 0)
                {
                    Logger?.Info("RLO Texts: the game saves: " + flushed + " message(s) waiting for their words go out with the written line.");
                }
                if (_loaded && _save != null && _save.modData != null)
                {
                    _save.modData[TextsStore.SaveKey] = Hub.Store.Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the pack and the server

        // Once a session, off the game's thread: the packs on disk, the machine's cores, llama.cpp's devices.
        private static void Find()
        {
            if (_finding)
            {
                return;
            }
            _finding = true;
            List<string> places = TextsGame.Places();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _packs = TextPacks.Find(places);
                    _cores = TextCores.Real();
                    _devices = new List<TextDevice>();
                    for (int i = 0; i < _packs.Count; i++)
                    {
                        string why = TextPacks.Verify(_packs[i], TextRuntime.Files);   // (before anything of the pack is started: llama.cpp's pinned files, byte for byte)
                        if (why == null)
                        {
                            _verified = _packs[i];
                            _devices = TextPacks.Devices(TextPacks.ListDevices(_verified.Exe, DevicesWaitMs));
                            break;
                        }
                        _refused = _refused ?? _packs[i];
                        _refusedWhy = _refusedWhy ?? why;
                    }
                }
                catch (Exception)
                {
                    _packs = _packs ?? new List<TextPack>();
                    _devices = new List<TextDevice>();
                }
                _found = true;
                _finding = false;
            });
        }

        // The server for the options as they are: started, or started again when the device or threads change.
        private static void Run()
        {
            if (_server != null && _runDevice == Settings.TextsDevice && _runThreads == Settings.TextsThreads)
            {
                return;   // (as it was started: the common case, two comparisons)
            }
            if (_pack == null && _verified != null)
            {
                _pack = _verified;
                Logger?.Info("RLO Texts: the pack " + _pack.Name + " (version " + _pack.Version + ", data v" + _pack.Data + ", " + (_pack.ModelBytes / (1024 * 1024)) + " MB) in " + _pack.Folder
                             + ", llama.cpp's files checked" + (_packs.Count > 1 ? "; " + (_packs.Count - 1) + " other pack(s) found and not used" : "") + ".");
            }
            if (_refused != null && !_refusedTold)
            {
                _refusedTold = true;
                Logger?.Warn("RLO Texts: the pack in " + _refused.Folder + " is not run: " + _refusedWhy + " (its runtime must be llama.cpp " + TextRuntime.Release + " as released).");
            }
            if (_pack == null)
            {
                return;
            }
            _runDevice = Settings.TextsDevice;
            _runThreads = Settings.TextsThreads;
            string device = TextPacks.Choose(Mathf.Clamp(Settings.TextsDevice, 0, 2), _devices, _pack.ModelBytes);
            int threads = TextSpeed.Threads(_cores, Settings.TextsThreads <= 0 ? 0 : Settings.TextsThreads + 1, device != null);   // (the option: 0 Auto, then 2, 3, 4; at most 2 on a GPU)
            string wanted = TextSpeed.For(_pack.Name + " " + _pack.Version, device, threads);
            if (_server != null && _serverFor == wanted)
            {
                return;
            }
            if (_server != null)
            {
                StopServer("the device or threads changed");
            }
            _device = device;
            _threads = threads;
            _deviceLabel = null;
            for (int i = 0; _devices != null && i < _devices.Count; i++)
            {
                if (_devices[i].Name == device)
                {
                    _deviceLabel = _devices[i].Label;
                }
            }
            _serverFor = wanted;
            _speedTold = false;
            Directory.CreateDirectory(TextsGame.Home);
            _server = new TextServer(Queue, new TextServerStart
            {
                Exe = _pack.Exe, Model = _pack.Model, Threads = threads, Device = device, Log = Path.Combine(TextsGame.Home, "server.log"),
                Cache = Path.Combine(TextsGame.Home, "cache"), SpeedFor = wanted, Known = KnownSpeed(wanted)
            }, Log);
            Hub.Runner = _server;
            Hub.Data = _pack.Data;   // (the intents its model learnt: a v2 pack is never asked v3's)
            _server.Start();
            Logger?.Info("RLO Texts: starting the model on " + (device != null ? "the GPU (" + _deviceLabel + ")" : "the processor") + " with " + threads + " thread(s) of "
                         + _cores + " core(s); the written lines are used until it is ready.");
        }

        private static void StopServer(string why, bool now = false)
        {
            Hub.Clear();
            Hub.Runner = null;
            TextServer server = _server;
            if (server != null)
            {
                _server = null;
                _serverFor = null;
                _runDevice = _runThreads = -1;
                if (now)
                {
                    server.Dispose();   // (the game is closing: no thread pool to wait for)
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(_ => server.Dispose());   // (killing it and ending its worker can take a second: never on the game's thread)
                }
                Logger?.Info("RLO Texts: the model stopped (" + why + "); the written lines are used.");
            }
        }

        private static void Log(string line)
        {
            Logger?.Info(line);   // (the worker's lines: Unity's log takes them from any thread)
        }

        // The speed is known: kept for next time, what this machine may write decided, and the Lite pack suggested once if the full one is slow.
        private static void Measured()
        {
            _speedTold = true;
            TextSpeedResult speed = _server.Speed;
            if (speed == null)
            {
                return;
            }
            double estimate = speed.Estimate;
            Hub.AheadOnly = TextSpeed.AheadOnly(estimate);
            Hub.Queue.Expected(estimate);   // (the queue starts from this machine's own figure, not its cautious guess)
            KeepSpeed(speed);
            if (Hub.AheadOnly)
            {
                Logger?.Info("RLO Texts: a reply takes about " + estimate.ToString("0.0", CultureInfo.InvariantCulture) + " s here: only what is asked well ahead is written by the model (posts, replies due later, the paper); spoken lines keep the written ones.");
            }
            if (TextSpeed.SuggestLite(_pack.Rank, estimate) && !Told("lite"))
            {
                TextsGame.Notice("reallife_texts_note_lite", new Dictionary<string, string>(1) { { "seconds", estimate.ToString("0") } }, 16f);
                Remember("lite");
            }
        }

        // ---------------------------------------------------------------- what may be asked

        private static bool Allowed(TextUse use, bool shared)
        {
            if (!Settings.Texts || !TextsGame.English)
            {
                return false;
            }
            if (shared && GoingPublic.InSession && !GoingPublic.IsHost)
            {
                return false;   // a guest never writes shared text: the host's words come in part T
            }
            switch (use)
            {
                case TextUse.Replies: return Settings.TextsReplies;
                case TextUse.Posts: return Settings.TextsPosts;
                case TextUse.People: return Settings.TextsPeople;
                case TextUse.Official: return Settings.TextsOfficial;
                case TextUse.Rivals: return Settings.TextsRivals;
                case TextUse.Ledger: return Settings.TextsLedger;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- Going Public: part T

        // The host: a shared text was kept; it goes with the next update as a change (the shared city reads Changes at every
        // update, so the whole part is never marked to go again: only a late joiner's snapshot carries it whole).
        private static void SharedKept(TextKept k)
        {
            if (!GoingPublic.IsHost)
            {
                return;
            }
            TextsShare.Line(Changes, TextsShare.NetKey(k.Key), k.Intent, k.Words);
        }

        private static string TakeChanges()
        {
            if (Changes.Length == 0)
            {
                return null;
            }
            string text = Changes.ToString();
            Changes.Length = 0;
            return text;
        }

        // A guest: the host's words, checked again here; a text that fails keeps the written line.
        private static bool FromHost(string text, bool whole)
        {
            var refused = new List<string>(2);
            int kept = TextsShare.Apply(text, Hub.Store, TextsGame.Day, refused, whole);
            if (kept > 0 || refused.Count > 0)
            {
                Logger?.Info("RLO Texts: the host's words for " + kept + " shared item(s)" + (refused.Count > 0 ? "; refused on arrival: " + string.Join(", ", refused) : "") + ".");
            }
            return true;
        }

        // ---------------------------------------------------------------- the speed file and what was told once

        private static string StateFile => Path.Combine(TextsGame.Home, "state.txt");

        private static TextSpeedResult KnownSpeed(string wanted)
        {
            try
            {
                if (!File.Exists(StateFile))
                {
                    return null;
                }
                foreach (string line in File.ReadAllLines(StateFile))
                {
                    if (line.StartsWith("speed=", StringComparison.Ordinal))
                    {
                        TextSpeedResult r = TextSpeedResult.Read(line.Substring(6));
                        if (r != null && r.For == wanted)
                        {
                            return r;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // (no file: a test this time)
            }
            return null;
        }

        private static void KeepSpeed(TextSpeedResult speed)
        {
            Rewrite("speed=", "speed=" + speed.Line());
        }

        private static bool Told(string what)
        {
            try
            {
                return File.Exists(StateFile) && Array.IndexOf(File.ReadAllLines(StateFile), "told=" + what) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Remember(string what)
        {
            Rewrite("told=" + what, "told=" + what);
        }

        // The state file with the line starting so replaced (or added).
        private static void Rewrite(string start, string line)
        {
            try
            {
                Directory.CreateDirectory(TextsGame.Home);
                var lines = new List<string>(File.Exists(StateFile) ? File.ReadAllLines(StateFile) : new string[0]);
                lines.RemoveAll(l => l.StartsWith(start, StringComparison.Ordinal));
                lines.Add(line);
                File.WriteAllLines(StateFile, lines.ToArray());
            }
            catch (Exception e)
            {
                Logger?.Warn("RLO Texts: the state file could not be written (" + e.Message + ").");
            }
        }

        // ---------------------------------------------------------------- the status row

        /// <summary>The status as the options page's status row shows it: the pack, loaded or not, the speed, threads, the hit rate, model against written lines.</summary>
        public static Dictionary<string, string> Status(out string key)
        {
            var data = new Dictionary<string, string>(10);
            if (!Settings.Texts)
            {
                key = "reallife_texts_status_off";
                return data;
            }
            if (!_found)
            {
                key = "reallife_texts_status_looking";
                return data;
            }
            if (_pack == null && _refused != null)
            {
                key = "reallife_texts_status_failed";
                data["pack"] = _refused.Name;
                data["why"] = Cap("pack files changed: " + _refusedWhy);
                return data;
            }
            if (_pack == null)
            {
                key = "reallife_texts_status_none";
                data["folder"] = "LocalLow/\u2026/" + TextPacks.Fixed;
                return data;
            }
            data["pack"] = _pack.Name;
            data["device"] = _device != null ? _deviceLabel ?? _device : TextsText.Processor();
            data["threads"] = _threads.ToString();
            data["model"] = Hub.Shown.ToString();
            data["written"] = Hub.Fallbacks.ToString();
            data["hits"] = Hub.Budget.HitPercent.ToString();
            TextServerState state = _server != null ? _server.State : TextServerState.Off;
            TextSpeedResult speed = _server != null ? _server.Speed : null;
            data["seconds"] = speed != null ? speed.Estimate.ToString("0.0") : "?";
            data["speed"] = speed != null ? speed.TokensPerSecond.ToString("0") : "?";
            data["mean"] = _server != null && _server.Written > 0 ? _server.MeanSeconds.ToString("0.0") : "?";
            key = state == TextServerState.Ready ? "reallife_texts_status_ready" : state == TextServerState.Failed ? "reallife_texts_status_failed" : "reallife_texts_status_loading";
            if (state == TextServerState.Failed)
            {
                data["why"] = Cap(_server.Why ?? "?");
            }
            return data;
        }

        // A reason short enough for a notice (the notices hold 240 characters).
        private static string Cap(string why)
        {
            return why.Length <= WhyMost ? why : why.Substring(0, WhyMost - 1) + "\u2026";
        }

        /// <summary>The options page's status button: the status as one of the game's notices.</summary>
        public static void ShowStatus()
        {
            Dictionary<string, string> data = Status(out string key);
            TextsGame.Notice(key, data, 16f);
            if (Settings.Texts && Settings.TextsService != 0)
            {
                ShowServiceStatus();   // (Your own AI's own line beside the pack's)
            }
        }
    }
}
