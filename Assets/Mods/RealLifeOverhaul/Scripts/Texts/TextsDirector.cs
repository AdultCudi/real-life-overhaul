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
    /// RLO Texts: a small text model on the player's own PC words the city's messages. The game decides everything; the
    /// model is asked only for the words, every word is checked, and the written lines are shown whenever it is not there
    /// in time. It runs in llama.cpp's own server as a child process, not as DLLs in the game: a native crash ends only the
    /// child, and another mod's llama.dll cannot clash.
    /// COST: per frame one time comparison; a look twice a second while idle, ten times while requests wait.
    /// </summary>
    public sealed partial class TextsDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("texts", "TextsDirector", typeof(TextsDirector));
        private static readonly int HotUpdate = HotSpots.Slot("Texts: TextsDirector.Update");
        private static readonly int HotLook = HotSpots.Slot("Texts: TextsDirector.Look");

        private const float IdleEvery = 0.5f;      // real seconds between two looks with nothing waiting
        private const float BusyEvery = 0.1f;      // ...while requests wait: a reply on the GPU comes back in 0.05 s
        private const float HookEvery = 2f;        // ...between two checks that the save hook is attached
        private const int DevicesWaitMs = 8000;    // milliseconds llama.cpp may take to list its devices
        private const int WhyMost = 60;            // characters of a reason in the status notice

        public static IModLogger Logger;
        public static TextsDirector Instance { get; private set; }

        // Kept across saves and scenes: one model loaded once a session.
        private static readonly TextQueue Queue = new TextQueue();
        private static readonly TextQueue ServiceQueue = new TextQueue();   // Your own AI's
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
        private static readonly List<TextPack> Verified = new List<TextPack>(4);   // every pack whose runtime is llama.cpp's own, the best first
        private static string _refusedWhy, _largeSlow, _locale;   // the graphics card RLO Texts Large was too slow on; the game's language last seen
        private static bool _largeTold, _largeRead;
        private static string _passedWhy;                  // why RLO Texts Large was passed over, for the status when no other pack runs
        private static int _pickedFor = int.MinValue;      // the device option a pick found no pack for
        private static int _runDevice = -1, _runThreads = -1;   // the options the server was started with
        private static List<TextDevice> _devices;
        private static readonly System.Text.StringBuilder Changes = new System.Text.StringBuilder(1024);

        private float _next, _last, _nextHook, _frameTime;
        private int _frameCount;
        private GameInstance _save;
        private bool _loaded;
        private int _prunedDay = -1;
        private Action _onSave;

        /// <summary>Whether the game still has what a kept text's key belongs to; set by Wiring/TextsLinks.</summary>
        public static Func<string, bool> StillThere;

        /// <summary>The hub requests go through; Wiring/TextsLinks hands it to the features' bridges.</summary>
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
                _onQuit = Guard.Wrap(StopOnQuit);   // made once and kept: the game's process ends the server with it either way
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
                // the job object ends it with the process anyway
            }
        }

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
            _frameTime = frames > 0 && since > 0f ? since * 1000f / frames : 0f;   // the mean frame since the last look
            SignInLook();
            SignOutLook();
            string locale = TextsGame.Locale;
            if (!ReferenceEquals(locale, _locale))
            {
                _locale = locale;
                Hub.Language = TextLanguages.Of(locale);
                Hub.English = TextsGame.English;
            }
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
            Hour();
            if (!_found)
            {
                Find();
                return;
            }
            if (!_loaded)
            {
                return;   // the main menu: the model starts only once a save is loaded, so its memory is used only while playing
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
                ForgetStores();
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
                PruneMemory(save.Day);
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
            ReadMemory(save);
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
                    WriteMemory(_save);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

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
                    var verified = new List<TextPack>(4);
                    for (int i = 0; i < _packs.Count; i++)
                    {
                        string why = TextPacks.Verify(_packs[i], TextRuntime.Files);   // before anything of the pack is started
                        if (why == null)
                        {
                            verified.Add(_packs[i]);
                            if (_verified == null)
                            {
                                _verified = _packs[i];
                                _devices = TextPacks.Devices(TextPacks.ListDevices(_verified.Exe, DevicesWaitMs));
                            }
                            if (_packs[i].Rank != TextPacks.LargeTier)
                            {
                                break;   // the best that is not Large: none below it is ever run; Large may still give way to it
                            }
                            continue;
                        }
                        _refused = _refused ?? _packs[i];
                        _refusedWhy = _refusedWhy ?? why;
                    }
                    lock (Verified)
                    {
                        Verified.Clear();
                        Verified.AddRange(verified);
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
                return;   // as it was started: the common case, two comparisons
            }
            if (_pack == null && _verified != null)
            {
                if (_pickedFor == Settings.TextsDevice)
                {
                    return;   // no pack may run with this option: picked again only when it changes
                }
                _pickedFor = Settings.TextsDevice;
                if (!_largeRead)
                {
                    _largeRead = true;
                    _largeSlow = StateLine("large_slow=");   // once a session
                }
                List<TextPack> verified;
                lock (Verified)
                {
                    verified = new List<TextPack>(Verified);
                }
                _pack = TextPacks.Pick(verified, _devices, Mathf.Clamp(Settings.TextsDevice, 0, 2), _largeSlow, out _passedWhy);
                if (_passedWhy != null && !_largeTold)
                {
                    _largeTold = true;
                    Logger?.Info("RLO Texts: " + _passedWhy + (_pack != null ? "; the next pack is run." : "; no other pack is installed, so the written lines are used."));
                }
                if (_pack == null)
                {
                    return;
                }
                _pickedFor = int.MinValue;
                Hub.Pack = _pack.Name + " " + _pack.Version + " (data v" + _pack.Data + ")";
                Hub.Languages = _pack.Languages;
                Logger?.Info("RLO Texts: the pack " + _pack.Name + " (version " + _pack.Version + ", data v" + _pack.Data + ", " + (_pack.ModelBytes / (1024 * 1024)) + " MB, "
                             + (_pack.Data >= TextIntents.DataV6 ? "writes " + string.Join(", ", _pack.Languages) : "writes English") + ") in " + _pack.Folder
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
            int threads = TextSpeed.Threads(_cores, Settings.TextsThreads <= 0 ? 0 : Settings.TextsThreads + 1, device != null);   // the option: 0 Auto, then 2, 3, 4
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
                Cache = Path.Combine(TextsGame.Home, "cache"), SpeedFor = wanted, Known = KnownSpeed(wanted), System = TextCard.SystemOf(_pack.Data)
            }, Log);
            Hub.Runner = _server;
            Hub.Data = _pack.Data;   // the intents its model learnt
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
                    server.Dispose();   // the game is closing: no thread pool to wait for
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(_ => server.Dispose());   // killing it and ending its worker can take a second: never on the game's thread
                }
                Logger?.Info("RLO Texts: the model stopped (" + why + "); the written lines are used.");
            }
        }

        private static void Log(string line)
        {
            Logger?.Info(line);   // the worker's lines: Unity's log takes them from any thread
        }

        // The speed is known: kept for next time, what this machine may write decided, the Lite pack suggested once if the full one is slow.
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
            Hub.Queue.Expected(estimate);   // the queue starts from this machine's own figure, not its cautious guess
            KeepSpeed(speed);
            if (Hub.AheadOnly)
            {
                Logger?.Info("RLO Texts: a reply takes about " + estimate.ToString("0.0", CultureInfo.InvariantCulture)
                             + " s here: only what is asked well ahead is written by the model (posts, replies due later, the paper); spoken lines keep the written ones.");
            }
            if (TextSpeed.SuggestLite(_pack.Rank, estimate) && !Told("lite"))
            {
                TextsGame.Notice("reallife_texts_note_lite", new Dictionary<string, string>(1) { { "seconds", estimate.ToString("0") } }, 16f);
                Remember("lite");
            }
            string card = _deviceLabel ?? _device;
            if (_pack.Rank == TextPacks.LargeTier && TextSpeed.LargeTooSlow(estimate))
            {
                _largeSlow = card ?? "the processor";
                Rewrite("large_slow=", "large_slow=" + _largeSlow);   // not tried on this card again
                Logger?.Info("RLO Texts: RLO Texts Large takes about " + estimate.ToString("0.0", CultureInfo.InvariantCulture) + " s a reply on " + _largeSlow + ", more than "
                             + TextSpeed.LargeSlow.ToString("0.0", CultureInfo.InvariantCulture) + " s: the next pack is run instead.");
                StopServer("RLO Texts Large is too slow here");
                _pack = null;   // Run picks again, passing over Large on this card
                return;
            }
            if (_device != null && _largeSlow == null && !LargeFound() && TextPacks.Card(_devices, out TextDevice gpu)
                && TextSpeed.OfferLarge(_pack.Rank, estimate, TextSpeed.LargeFits(gpu, TextSpeed.LargeBytes), TextPacks.LargePublished) && !Told("large"))
            {
                TextsGame.Notice("reallife_texts_note_large", new Dictionary<string, string>(1) { { "card", gpu.Label } }, 16f);
                Remember("large");
            }
        }

        // Whether the running pack writes the game's language; Your own AI's is the hub's ServiceWrites.
        private static bool LanguageOk() => Hub.WritesLanguage;

        // Whether a use may ask now, by its options; the language is the hub's to judge.
        private static bool Allowed(TextUse use, bool shared)
        {
            if (!Settings.Texts)
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

        // The host: a shared text was kept, and goes with the next update as a change; only a late joiner's snapshot carries
        // the whole part.
        private static void SharedKept(TextKept k)
        {
            if (!GoingPublic.IsHost)
            {
                return;
            }
            TextsShare.Line(Changes, TextsShare.NetKey(k.Key), k.Intent, k.Words, k.Lang);
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
            int kept = TextsShare.Apply(text, Hub.Store, TextsGame.Day, refused, whole, Hub.Language);   // words in another language than this game's keep its written lines
            if (kept > 0 || refused.Count > 0)
            {
                Logger?.Info("RLO Texts: the host's words for " + kept + " shared item(s)" + (refused.Count > 0 ? "; refused on arrival: " + string.Join(", ", refused) : "") + ".");
            }
            return true;
        }
    }
}
