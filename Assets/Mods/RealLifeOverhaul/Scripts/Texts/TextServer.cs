using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Real seconds since the mod started, the same on every thread (the queue's and the worker's clock).</summary>
    public static class TextClock
    {
        private static readonly long Zero = Stopwatch.GetTimestamp();

        public static double Now => (Stopwatch.GetTimestamp() - Zero) / (double)Stopwatch.Frequency;
    }

    /// <summary>The stages of the runner's start, in order.</summary>
    public enum TextStartStage
    {
        Launch,      // a port, the exe started, tied to the game's process
        Load,        // the model read (/health)
        WarmUp,      // the first reply (a GPU's shaders built)
        Head,        // the prompt's head read and its state saved
        Speed        // the speed test
    }

    /// <summary>
    /// The start's watch: the stage the worker is in, since when, and its limit. The watchdog (a timer of the runner's)
    /// asks it twice a second; a stage past its limit and <see cref="Grace"/> is overdue once, and the start then ends for
    /// certain, whatever the wait it is stuck in (a runtime that does not honour a socket's time-out, a server that takes the
    /// connection and never answers). Game-free and thread-safe (EconomyCheck, TextsChecks.cs).
    /// COST: a lock and a few numbers a stage; nothing on the game's thread.
    /// </summary>
    public sealed class TextStartWatch
    {
        public const double Grace = 5.0;   // seconds past a stage's own limit before the watchdog ends it (the stage's own time-out comes first)

        private readonly object _lock = new object();
        private TextStartStage _stage;
        private double _began, _limit = -1.0;
        private bool _over;

        /// <summary>True once the watchdog has ended the start.</summary>
        public bool Over
        {
            get
            {
                lock (_lock)
                {
                    return _over;
                }
            }
        }

        /// <summary>A stage begins, with the most it may take.</summary>
        public void Enter(TextStartStage stage, double limit, double now)
        {
            lock (_lock)
            {
                _stage = stage;
                _began = now;
                _limit = Math.Max(0.0, limit);
            }
        }

        /// <summary>The stage has ended; returns the seconds it took.</summary>
        public double Leave(double now)
        {
            lock (_lock)
            {
                _limit = -1.0;
                return Math.Max(0.0, now - _began);
            }
        }

        /// <summary>True once, when the stage under way is past its limit and the grace: the start is over from then on.</summary>
        public bool Overdue(double now, out TextStartStage stage, out double limit)
        {
            lock (_lock)
            {
                stage = _stage;
                limit = _limit;
                if (_over || _limit < 0.0 || now - _began <= _limit + Grace)
                {
                    return false;
                }
                _over = true;
                return true;
            }
        }

        /// <summary>A stage as the log names it.</summary>
        public static string Name(TextStartStage stage)
        {
            switch (stage)
            {
                case TextStartStage.Launch: return "starting the server";
                case TextStartStage.Load: return "loading the model";
                case TextStartStage.WarmUp: return "the first reply";
                case TextStartStage.Head: return "reading the prompt's head";
                default: return "the speed test";
            }
        }
    }

    /// <summary>How the runner stands.</summary>
    public enum TextServerState
    {
        Off,
        Loading,     // the server is starting and reading the model
        Ready,
        Failed       // it would not start, or stopped: the written lines are used until the pack is switched off and on
    }

    /// <summary>The GPU driver's shader cache at the first reply, as the start's line names it.</summary>
    public enum TextShaderCache
    {
        None,        // on the processor: no shaders
        Unknown,     // the system could not give the server's processor time
        Used,        // the driver had the shaders kept: nothing built
        Created,     // built for the first time (this pack, device and runtime), kept by the driver for the next start
        Missing      // built again although built before: the driver did not keep them (its cache cleared, or a new driver)
    }

    /// <summary>What the runner is started with.</summary>
    public sealed class TextServerStart
    {
        public string Exe;           // the pack's llama-server.exe
        public string Model;         // the pack's .gguf
        public string Log;           // where the server writes its own log (null: nowhere)
        public int Threads = 2;      // 1 to 4 (at most 2 on a GPU: TextSpeed.Threads)
        public string Device;        // null for the processor; else a llama.cpp device name ("Vulkan0")
        public double LoadSeconds = 90.0;   // the most the model may take to load before the runner gives up
        public double WarmSeconds = 30.0;   // the most the first reply on the processor, or the head's read, may take
        public double BuildSeconds = 90.0;  // the most the first reply may take on a GPU, where it builds the shaders when the driver has none kept (measured 10 s idle, 22 s in the game's load on a Ryzen 7700X, on one thread: a slower processor takes longer)
        public string Cache;         // a folder for the cached head's state (the server's slot file); null: no prompt cache
        public string SpeedFor;      // what a speed test is for (pack, device, threads)
        public TextSpeedResult Known; // a speed measured before for the same: no test this time
    }

    /// <summary>
    /// The runner: llama.cpp's own server (llama-server.exe, MIT) started by the mod as a child process, on 127.0.0.1 and a
    /// free port of the moment, with a key of its own, and one worker thread of the mod that takes the queue's requests one
    /// at a time and asks the server for each over a local socket. Chosen over loading llama.cpp's DLLs into the game (see
    /// TextsDirector's summary for the reasons): a native crash or a leak stays in the child.
    ///
    ///   priority   on the processor below normal (the server's threads and the worker's), so the game's frame always comes
    ///              first. On a GPU normal, with at most two threads (<see cref="LowPriority"/>, TextSpeed.GpuThreads): the
    ///              model's work is the GPU's, but each step of it is handed over and waited for by a thread of the server,
    ///              and below normal that thread waits behind every busy thread of the game. Measured (the 2B on
    ///              an RTX 5070 Ti, a Ryzen 7700X's sixteen threads busy at normal priority and the GPU busy): below normal the
    ///              model took over 40 s to load, the first reply 5 s and words came at 3 to 6 a second; at normal priority
    ///              3.5 s, 0.1 s and 110 to 200 a second, the server using half a thread of the processor for the 0.1 to 0.3 s
    ///              a reply takes. In the game below normal: the first reply in 24 s, 39 words a second
    ///   shaders    a GPU's first reply builds llama.cpp's shaders, unless the driver kept them from an earlier start in its
    ///              own cache (llama.cpp keeps none of its own). Steam starts the game with variables that point the driver's
    ///              cache at the game's folder of Steam's shader pre-caching, and a child inherits them: there the NVIDIA
    ///              driver kept nothing for llama.cpp, and every start in the game built every shader again (the 2B on an
    ///              RTX 5070 Ti: 22 s each session, 21 s of it processor time; outside the game 0.05 s). They are
    ///              not passed on (<see cref="OwnShaderCache"/>), so the driver keeps the shaders in its own cache: built once,
    ///              behind the written lines, within <see cref="TextServerStart.BuildSeconds"/>; the start's line says whether
    ///              the cache was used, created or missing (<see cref="ShaderCache"/>)
    ///   start      a free port from the system, the exe started hidden (no window), at its priority, its own log; tied to a
    ///              Windows job object that ends it when the game's process ends, even in a crash; then the worker waits
    ///              for /health (the model loading, within <see cref="TextServerStart.LoadSeconds"/>) and writes one short
    ///              reply to warm it (the first reply on a GPU builds its shaders). Each stage is logged with its time and
    ///              has a limit of its own; the first reply and the speed test also say what was sent, how long the model's
    ///              own reading and writing took, the time around them and the processor time the server used, so that a
    ///              slow start shows whether the server was working (building shaders) or waiting to run; a watchdog on a
    ///              timer (<see cref="TextStartWatch"/>) ends a stage stuck past
    ///              it for certain (the line written, the socket closed, the server killed). Every wait on the server is
    ///              bounded by Socket.Poll, not the socket's time-outs, which Mono on Windows does not honour
    ///   the head   every prompt starts with the same system turn and "who: " (<see cref="Head"/>, 57 tokens of the 2B
    ///              model): read once, its state saved in the server's slot file, and put back before each request, so
    ///              only the card itself is read (the 2B is a hybrid model whose state llama.cpp cannot reuse in part by
    ///              itself; measured on two threads: the head's 0.4 s read once instead of every request, the restore 20 ms)
    ///   speed      on first load, or when the pack, device or threads change: one reply from a fixed card, within
    ///              <see cref="TextSpeed.TestSeconds"/> s, measures reading and writing (<see cref="Speed"/>)
    ///   requests   one at a time, each with a time-out of the request's own deadline and at most <see cref="TimeOut"/>
    ///              (<see cref="TimeOutGpu"/> on a GPU); nothing on the game's thread but handing words over
    ///   stop       the server is killed, the worker ends; the game closing, the option off, the pack switched
    ///
    /// Game-free (no Unity types): the integration check runs it with the Lite model (EconomyCheck, TextsChecks.cs).
    /// COST: nothing on the game's thread; a thread that sleeps until there is a request.
    /// </summary>
    public sealed class TextServer : IDisposable, ITextRunner
    {
        public const double TimeOut = 4.0;          // seconds a reply may take on the processor before it is given up
        public const double TimeOutGpu = 2.0;       // ...on a GPU
        public const int WakeMs = 250;              // the worker's longest sleep between two looks at the queue
        public const double LaunchSeconds = 10.0;   // seconds the exe may take to start (a port, the process, the job object)
        public const double SaveSeconds = 10.0;     // seconds the head's state may take to be saved, after its read
        public const double RestoreSeconds = 2.0;   // seconds the head's state may take to be put back before the speed test
        public const int DogMs = 500;               // milliseconds between two looks of the start's watchdog

        private readonly TextQueue _queue;
        private readonly TextServerStart _start;
        private readonly Action<string> _log;
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private Process _process;
        private IntPtr _job;
        private Thread _thread;
        private volatile bool _stop;
        private volatile TextServerState _state = TextServerState.Off;
        private volatile bool _quiet = true;
        private int _port;
        private string _key;
        private int _written, _missed;
        private readonly Random _seeds = new Random(Guid.NewGuid().GetHashCode());   // (a fresh sampling seed each request: the same card never gives the same words by itself)
        private double _took, _headMs;
        private string _detail;   // what the stage under way adds to its log line (the first reply's timings), or null
        private bool _headSaved;
        private string _headFile;
        private readonly TextStartWatch _watch = new TextStartWatch();
        private readonly object _failing = new object();
        private Timer _dog;
        private TcpClient _socket;   // the request under way (the worker's), closed by the watchdog or a stop to end its wait

        /// <summary>The fixed head of every prompt: the system turn and the opening of the user's.</summary>
        public static string Head => "<|im_start|>system\n" + TextCard.System + "<|im_end|>\n<|im_start|>user\n";

        /// <summary>The speed measured (or known from before); null until it is.</summary>
        public TextSpeedResult Speed { get; private set; }

        /// <summary>Milliseconds the cached head saves on every request (0 without it).</summary>
        public double HeadSavesMs => _headSaved ? _headMs : 0.0;

        public TextServer(TextQueue queue, TextServerStart start, Action<string> log)
        {
            _queue = queue;
            _start = start;
            _log = log;
        }

        public TextServerState State => _state;

        /// <summary>Why it failed, for the status row and the log.</summary>
        public string Why { get; private set; }

        /// <summary>Seconds it took to be ready (0 until it is).</summary>
        public double LoadedIn { get; private set; }

        /// <summary>Replies written and the mean seconds each took (prompt and words).</summary>
        public int Written => _written;
        public double MeanSeconds => _written > 0 ? _took / _written : 0.0;

        /// <summary>Requests the model failed on (a time-out, an error).</summary>
        public int Failed => _missed;

        /// <summary>The game is quiet enough for the background (set by the game's thread, read by the worker).</summary>
        public bool Quiet
        {
            set => _quiet = value;
        }

        /// <summary>A new request is waiting: the worker looks now rather than at its next quarter second.</summary>
        public void Nudge()
        {
            _wake.Set();
        }

        /// <summary>Starts the server and the worker; returns at once (the loading is the worker's).</summary>
        public void Start()
        {
            if (_state != TextServerState.Off)
            {
                return;
            }
            _state = TextServerState.Loading;
            _stop = false;
            _thread = new Thread(Run) { IsBackground = true, Name = "RLO Texts", Priority = LowPriority(_start.Device) ? ThreadPriority.BelowNormal : ThreadPriority.Normal };   // (it mostly waits on the socket; on a GPU it must not read the answer late)
            _thread.Start();
        }

        public void Dispose()
        {
            _stop = true;
            _wake.Set();
            Interlocked.Exchange(ref _dog, null)?.Dispose();
            try
            {
                Interlocked.Exchange(ref _socket, null)?.Close();   // (a request under way ends its wait now)
            }
            catch (Exception)
            {
                // (closed already)
            }
            Kill();
            if (_thread != null && !_thread.Join(1500))
            {
                _log?.Invoke("RLO Texts: the worker did not end within 1.5 s; it ends with the game.");
            }
            _thread = null;
            if (_state != TextServerState.Failed)
            {
                _state = TextServerState.Off;
            }
        }

        // ---------------------------------------------------------------- the worker

        private void Run()
        {
            try
            {
                _dog = new Timer(Watchdog, null, DogMs, DogMs);
                bool ready = Stage(TextStartStage.Launch, LaunchSeconds, Launch) && Stage(TextStartStage.Load, _start.LoadSeconds, WaitReady)
                             && Stage(TextStartStage.WarmUp, WarmLimit(_start), WarmUp) && Stage(TextStartStage.Head, _start.WarmSeconds + SaveSeconds, KeepHead)
                             && Stage(TextStartStage.Speed, TextSpeed.TestSeconds + RestoreSeconds, SpeedTest);
                Interlocked.Exchange(ref _dog, null)?.Dispose();
                if (!ready)
                {
                    Kill();
                    _state = TextServerState.Failed;
                    _queue.Clear();
                    return;
                }
                _state = TextServerState.Ready;
                while (!_stop)
                {
                    if (_process == null || _process.HasExited)
                    {
                        Fail("the server stopped by itself (exit code " + Exit() + ")");
                        Kill();
                        _queue.Clear();
                        return;
                    }
                    TextJob job = _queue.Take(TextClock.Now, _quiet);
                    if (job == null)
                    {
                        _wake.WaitOne(WakeMs);
                        continue;
                    }
                    Write(job);
                }
            }
            catch (Exception e)
            {
                Interlocked.Exchange(ref _dog, null)?.Dispose();
                Fail("the worker: " + e.GetType().Name + ": " + e.Message);
                Kill();
                _queue.Clear();
            }
        }

        // One stage of the start, timed and watched: its own limit first, the watchdog's a little past it. Logged with its time when
        // it ends, so a start that hangs shows where.
        private bool Stage(TextStartStage stage, double limit, Func<bool> step)
        {
            _watch.Enter(stage, limit, TextClock.Now);
            _detail = null;
            bool ok = !_stop && step();
            double took = _watch.Leave(TextClock.Now);
            if (_watch.Over)
            {
                return false;   // (the watchdog ended it and wrote its line)
            }
            if (ok)
            {
                _log?.Invoke("RLO Texts: start-up, " + TextStartWatch.Name(stage) + " in " + took.ToString("0.0") + " s" + (_detail != null ? " (" + _detail + ")" : string.Empty) + ".");
            }
            return ok;
        }

        // The watchdog, on a timer of its own while the start runs: a stage past its limit and the grace (a wait the runtime did
        // not end; Mono on Windows does not honour a socket's ReceiveTimeout) ends the start for certain: the line written, the
        // request's socket closed, the server killed, so the worker's wait returns.
        private void Watchdog(object unused)
        {
            try
            {
                if (!_watch.Overdue(TextClock.Now, out TextStartStage stage, out double limit))
                {
                    return;
                }
                Fail(TextStartWatch.Name(stage) + " took longer than " + limit.ToString("0") + " s");
                Interlocked.Exchange(ref _socket, null)?.Close();
                Kill();
            }
            catch (Exception)
            {
                // (the process gone already: the worker sees it)
            }
        }

        private void Write(TextJob job)
        {
            double begun = TextClock.Now;
            double limit = Math.Min(job.Deadline - begun, _start.Device != null ? TimeOutGpu : TimeOut);
            string why = null, words = null;
            if (limit <= 0.05)
            {
                why = "its time ran out before it started";
            }
            else
            {
                try
                {
                    Restore(job.Prompt);
                    double left = job.Deadline - TextClock.Now;
                    string answer = Post("/completion", TextJson.Request(job.Prompt, _seeds.Next(1, int.MaxValue)), (int)(Math.Max(0.05, Math.Min(left, limit)) * 1000.0));
                    words = TextJson.Field(answer, "content");
                    if (words == null)
                    {
                        why = "no words in the server's answer";
                    }
                }
                catch (Exception e)
                {
                    why = e is SocketException || e is IOException ? "it took longer than " + limit.ToString("0.0") + " s" : e.GetType().Name + ": " + e.Message;
                }
            }
            double took = TextClock.Now - begun;
            if (words != null)
            {
                Interlocked.Increment(ref _written);
                _took += took;
            }
            else
            {
                Interlocked.Increment(ref _missed);
            }
            _queue.Done(job, words, took, why);
        }

        // ---------------------------------------------------------------- the server

        private bool Launch()
        {
            if (!File.Exists(_start.Exe) || !File.Exists(_start.Model))
            {
                return Fail("the pack's files are missing (" + Path.GetFileName(File.Exists(_start.Exe) ? _start.Model : _start.Exe) + ")");
            }
            _port = FreePort();
            _key = Guid.NewGuid().ToString("N");
            if (_start.Cache != null)
            {
                Directory.CreateDirectory(_start.Cache);
                Stale(_start.Cache);
            }
            var info = new ProcessStartInfo(_start.Exe, Arguments(_start, _port, _key))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(_start.Exe),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            Offline(info);
            string steam = OwnShaderCache(info);
            try
            {
                _process = Process.Start(info);
            }
            catch (Exception e)
            {
                return Fail("the server would not start: " + e.Message);
            }
            if (_process == null)
            {
                return Fail("the server would not start");
            }
            bool low = LowPriority(_start.Device);
            try
            {
                _process.PriorityClass = low ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal;
            }
            catch (Exception)
            {
                // (it may have ended already; the next look sees it)
            }
            if (!low)
            {
                FullSpeed(_process);
            }
            TieToGame(_process);
            _log?.Invoke("RLO Texts: the server started on 127.0.0.1:" + _port + " (" + (_start.Device ?? "the processor") + ", " + _start.Threads + " threads, "
                         + (low ? "below normal" : "normal") + " priority, " + Path.GetFileName(_start.Model) + ")"
                         + (steam.Length > 0 ? "; the GPU driver's shader cache is its own (" + steam + " from Steam not passed on)." : "."));
            return true;
        }

        /// <summary>The first reply's limit: on a GPU long enough to build the shaders the driver has not kept.</summary>
        public static double WarmLimit(TextServerStart start) => start.Device != null ? Math.Max(start.WarmSeconds, start.BuildSeconds) : start.WarmSeconds;

        public const double BuiltCpuSeconds = 2.0;   // processor seconds of the first reply on a GPU from which its shaders were built (measured: 0.03 to 0.05 s with the driver's cache, 10 to 21 s without)
        public const string ShaderMark = "gpu-shaders.txt";   // in the cache folder: the pack, device and runtime whose shaders were last built (whether a build is the first)

        /// <summary>
        /// The driver's shader cache at the first reply, from the processor time the server used for it: building a GPU's
        /// shaders is the processor's work (seconds), a reply from kept shaders is not (hundredths). Built with the same
        /// pack, device and runtime built before (<paramref name="builtBefore"/>): the driver did not keep them. Game-free
        /// (EconomyCheck, TextsCacheChecks.cs).
        /// </summary>
        public static TextShaderCache ShaderCache(string device, double cpuSeconds, bool builtBefore)
        {
            if (device == null)
            {
                return TextShaderCache.None;
            }
            if (double.IsNaN(cpuSeconds) || double.IsInfinity(cpuSeconds))
            {
                return TextShaderCache.Unknown;
            }
            if (cpuSeconds < BuiltCpuSeconds)
            {
                return TextShaderCache.Used;
            }
            return builtBefore ? TextShaderCache.Missing : TextShaderCache.Created;
        }

        /// <summary>The cache's state as the start's line says it (empty on the processor).</summary>
        public static string ShaderCacheName(TextShaderCache state)
        {
            switch (state)
            {
                case TextShaderCache.Used: return "; the GPU's shaders: the driver's cache used";
                case TextShaderCache.Created: return "; the GPU's shaders: built, the driver's cache created for the next start";
                case TextShaderCache.Missing: return "; the GPU's shaders: built again, the driver's cache missing (cleared, or a new driver)";
                case TextShaderCache.Unknown: return "; the GPU's shaders: the driver's cache unknown";
                default: return string.Empty;
            }
        }

        /// <summary>What the shader mark holds for a start: the pack's model, the device and the runtime's release.</summary>
        public static string ShaderKey(TextServerStart start) => Path.GetFileName(start.Model) + ";" + start.Device + ";" + TextRuntime.Release;

        // Whether this pack, device and runtime built their shaders before, and the mark kept when they are built now.
        private TextShaderCache KeepShaderMark(double cpuSeconds)
        {
            string mark = _start.Cache != null && _start.Device != null ? Path.Combine(_start.Cache, ShaderMark) : null;
            bool before = false;
            try
            {
                before = mark != null && File.Exists(mark) && File.ReadAllText(mark).Trim() == ShaderKey(_start);
            }
            catch (Exception)
            {
                // (unreadable: taken as never built)
            }
            TextShaderCache state = ShaderCache(_start.Device, cpuSeconds, before);
            if (mark != null && (state == TextShaderCache.Created || state == TextShaderCache.Missing))
            {
                try
                {
                    File.WriteAllText(mark, ShaderKey(_start));
                }
                catch (Exception)
                {
                    // (a read-only folder: the next build says created again)
                }
            }
            return state;
        }

        /// <summary>
        /// True when the server runs below normal priority: on the processor, where its threads do the model's work and the
        /// game's frame must come first. On a GPU (a device named) it runs at normal priority: its few threads only hand the
        /// work to the GPU and wait for it, and below normal they wait behind every busy thread of the game (see the summary).
        /// </summary>
        public static bool LowPriority(string device) => device == null;

        /// <summary>
        /// llama-server's arguments, as the runner starts it: the model, 127.0.0.1 and the port, the key, the threads, one
        /// slot of 1024 tokens, no web page, offline; its priority (--prio -1, below normal, on the processor; 0, normal, on
        /// a GPU: <see cref="LowPriority"/>); the cache folder for the head's state; the device and its layers; its log of
        /// warnings and errors. Game-free (EconomyCheck, TextsSpeedChecks.cs).
        /// </summary>
        public static string Arguments(TextServerStart start, int port, string key)
        {
            var args = new StringBuilder(512);
            args.Append("-m \"").Append(start.Model).Append("\" --host 127.0.0.1 --port ").Append(port)
                .Append(" --api-key ").Append(key)
                .Append(" -t ").Append(start.Threads).Append(" -tb ").Append(start.Threads)
                .Append(" -c 1024 -np 1 --no-webui --prio ").Append(LowPriority(start.Device) ? "-1" : "0")
                .Append(" --offline");   // (offline: llama.cpp never reaches the network, even to fetch a model)
            if (start.Cache != null)
            {
                args.Append(" --slot-save-path \"").Append(start.Cache.TrimEnd('\\', '/')).Append('"');
            }
            if (start.Device != null)
            {
                args.Append(" -dev ").Append(start.Device).Append(" -ngl 99");
            }
            else
            {
                args.Append(" -dev none -ngl 0");
            }
            if (start.Log != null)
            {
                args.Append(" -lv 2 --log-file \"").Append(start.Log).Append('"');   // (warnings and errors only: a long session's log stays small)
            }
            return args.ToString();
        }

        private bool WaitReady()
        {
            double begun = TextClock.Now;
            while (!_stop && !_watch.Over)
            {
                Process p = _process;
                if (p == null || p.HasExited)
                {
                    return Fail("the server ended while loading (exit code " + Exit() + ")");
                }
                if (TextClock.Now - begun > _start.LoadSeconds)
                {
                    return Fail("the model took longer than " + _start.LoadSeconds.ToString("0") + " s to load");
                }
                try
                {
                    string health = Get("/health", 1000);
                    if (health != null && health.IndexOf("\"ok\"", StringComparison.Ordinal) >= 0)
                    {
                        LoadedIn = TextClock.Now - begun;
                        return true;
                    }
                }
                catch (Exception)
                {
                    // (not listening yet)
                }
                _wake.WaitOne(200);
            }
            return false;
        }

        // One short reply before the first real one (the first reply builds a GPU's shaders and fills the processor's caches).
        private bool WarmUp()
        {
            try
            {
                double begun = TextClock.Now, cpu = Cpu();
                string answer = Post("/completion", TextJson.Request(TextCard.ChatMl(TestCard()), 1, 1), Ms(WarmLimit(_start)));
                double took = TextClock.Now - begun, used = Cpu() - cpu;
                LoadedIn += took;
                _detail = "sent the test card, 1 token to write: " + Timings(answer, took, used) + ShaderCacheName(KeepShaderMark(used));
                return true;
            }
            catch (Exception e)
            {
                return Fail("the first reply failed: " + e.Message);
            }
        }

        // The head read and its state saved for every request after (without a cache folder, each request reads it).
        private bool KeepHead()
        {
            if (_start.Cache == null)
            {
                return true;
            }
            try
            {
                string head = Post("/completion", TextJson.Request(Head, 1, 0), Ms(_start.WarmSeconds));
                _headFile = "rlo-head-" + _port + ".bin";
                string saved = Post("/slots/0?action=save", "{\"filename\":\"" + _headFile + "\"}", Ms(SaveSeconds));
                _headSaved = saved != null && saved.IndexOf("n_saved", StringComparison.Ordinal) >= 0;
                _headMs = TextJson.Number(head, "prompt_ms");
                _log?.Invoke("RLO Texts: the prompt's fixed head (" + TextJson.Number(head, "prompt_n") + " tokens, " + _headMs.ToString("0") + " ms to read) is "
                             + (_headSaved ? "kept and put back before each request." : "not kept (the server would not save it): each request reads it."));
            }
            catch (Exception e)
            {
                _headSaved = false;
                _log?.Invoke("RLO Texts: the prompt's fixed head is not kept (" + e.Message + "); each request reads it.");
            }
            return true;
        }

        private static int Ms(double seconds) => (int)(seconds * 1000.0);

        // The head's state put back, so the server reads only the card (its own cache does this by itself for a model it can reuse in part).
        private void Restore(string prompt)
        {
            if (!_headSaved || !prompt.StartsWith(Head, StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                Post("/slots/0?action=restore", "{\"filename\":\"" + _headFile + "\"}", Ms(RestoreSeconds));
            }
            catch (Exception)
            {
                _headSaved = false;   // (the next requests read the head themselves)
            }
        }

        // One reply from a fixed card: how fast this machine reads and writes (unless known from before for the same).
        private bool SpeedTest()
        {
            if (_start.Known != null)
            {
                Speed = _start.Known;
                return true;
            }
            var result = new TextSpeedResult { For = _start.SpeedFor, HeadMs = _headMs };
            double begun = TextClock.Now, cpu = Cpu(), restored = 0.0;
            string detail;
            try
            {
                string prompt = TextCard.ChatMl(TestCard());
                Restore(prompt);
                restored = TextClock.Now - begun;
                double asked = TextClock.Now;
                cpu = Cpu();
                string answer = Post("/completion", TextJson.Request(prompt, 7, TextSpeed.ReplyTokens), (int)(TextSpeed.TestSeconds * 1000.0));
                result.PromptMsPerToken = TextJson.Number(answer, "prompt_per_token_ms");
                result.TokensPerSecond = TextJson.Number(answer, "predicted_per_second");
                if (double.IsNaN(result.PromptMsPerToken) || double.IsNaN(result.TokensPerSecond))
                {
                    throw new IOException("no timings");
                }
                detail = Timings(answer, TextClock.Now - asked, Cpu() - cpu);
            }
            catch (Exception e)
            {
                // Past the cap: at least as slow as the cap says.
                double spent = Math.Max(0.1, TextClock.Now - begun);
                result.Capped = true;
                result.PromptMsPerToken = spent * 1000.0 / TextSpeed.CardTokens;
                result.TokensPerSecond = TextSpeed.ReplyTokens / spent;
                detail = "no answer within " + TextSpeed.TestSeconds.ToString("0") + " s (" + e.Message + "); the server used " + Seconds(Cpu() - cpu) + " of processor time";
            }
            Speed = result;
            _log?.Invoke("RLO Texts: speed test: " + result.PromptMsPerToken.ToString("0.0") + " ms a token read, " + result.TokensPerSecond.ToString("0.0")
                         + " tokens a second written: an ordinary reply in about " + result.Estimate.ToString("0.0") + " s" + (result.Capped ? " (past the test's cap)" : "")
                         + " (sent the test card, " + TextSpeed.ReplyTokens + " tokens to write, " + (_headSaved ? "the head put back first in " + (restored * 1000.0).ToString("0") + " ms" : "the head read with it")
                         + ": " + detail + "; " + (LowPriority(_start.Device) ? "below normal" : "normal") + " priority, " + _start.Threads + " threads).");
            return true;
        }

        /// <summary>
        /// What a reply of the start took, for its log line: the tokens the model read and wrote and its own time for each
        /// (the server's "timings"), the rest of the request's time outside them, and the processor time the server used
        /// meanwhile. A slow first reply then tells its cause: the model's own times long with the processor time close to
        /// them is work (a GPU's shaders built); the model's times long with little processor time, or a long rest, is the
        /// server waiting to run (its threads starved by busier ones, or the GPU busy). Game-free (EconomyCheck, TextsSpeedChecks.cs).
        /// </summary>
        public static string Timings(string answer, double seconds, double cpuSeconds)
        {
            double readN = TextJson.Number(answer, "prompt_n"), readMs = TextJson.Number(answer, "prompt_ms");
            double wroteN = TextJson.Number(answer, "predicted_n"), wroteMs = TextJson.Number(answer, "predicted_ms");
            if (double.IsNaN(readN) || double.IsNaN(readMs) || double.IsNaN(wroteN) || double.IsNaN(wroteMs))
            {
                return "no timings in the answer, " + Seconds(seconds) + " in all; the server used " + Seconds(cpuSeconds) + " of processor time";
            }
            double rest = Math.Max(0.0, seconds - (readMs + wroteMs) / 1000.0);
            return readN.ToString("0") + " tokens read in " + readMs.ToString("0") + " ms and " + wroteN.ToString("0") + " written in " + wroteMs.ToString("0")
                   + " ms by the model, " + Seconds(rest) + " outside them, " + Seconds(seconds) + " in all; the server used " + Seconds(cpuSeconds) + " of processor time";
        }

        // Seconds for a log line ("0.42 s"), or "unknown" where the system could not say.
        private static string Seconds(double seconds)
        {
            return double.IsNaN(seconds) || double.IsInfinity(seconds) ? "unknown" : seconds.ToString("0.00") + " s";
        }

        // The processor time the server has used so far, in seconds (NaN where the system cannot say).
        private double Cpu()
        {
            try
            {
                Process p = _process;
                return p != null ? p.TotalProcessorTime.TotalSeconds : double.NaN;
            }
            catch (Exception)
            {
                return double.NaN;   // (ended, or the runtime cannot read it)
            }
        }

        /// <summary>The fixed card of the warm-up and the speed test.</summary>
        public static TextSheet TestCard()
        {
            var sheet = new TextSheet
            {
                Who = "Dana Whitlock, 31, florist, lives in Midtown", Role = "a friend, met at work", Mood = "in a good mood", Style = "warm", Player = "Alex",
                When = "Monday 18:05", News = "nothing has happened; Dana is just thinking of you", Write = "text: affection"
            };
            sheet.Traits.Add("generous");
            return sheet;
        }

        private string Get(string path, int timeoutMs) => Http("GET", path, null, timeoutMs);

        private string Post(string path, string body, int timeoutMs) => Http("POST", path, body, timeoutMs);

        // One request over a fresh local socket (Connection: close), the answer's body as text. Throws on a time-out. Every wait
        // is bounded by this method's own clock (Socket.Poll before each read), never by the socket's ReceiveTimeout, which
        // Mono on Windows does not honour: a server that takes the connection and never answers would hold the worker for
        // ever. The socket is kept in _socket while in use, so the watchdog or a stop can close it.
        private string Http(string method, string path, string body, int timeoutMs)
        {
            byte[] payload = body != null ? Encoding.UTF8.GetBytes(body) : null;
            var head = new StringBuilder(256);
            head.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\nHost: 127.0.0.1:").Append(_port)
                .Append("\r\nAuthorization: Bearer ").Append(_key).Append("\r\nConnection: close\r\n");
            if (payload != null)
            {
                head.Append("Content-Type: application/json\r\nContent-Length: ").Append(payload.Length).Append("\r\n");
            }
            head.Append("\r\n");
            double until = TextClock.Now + timeoutMs / 1000.0;
            var client = new TcpClient(AddressFamily.InterNetwork);
            Interlocked.Exchange(ref _socket, client);
            try
            {
                client.NoDelay = true;
                client.SendTimeout = timeoutMs;
                client.ReceiveTimeout = timeoutMs;
                IAsyncResult connect = client.BeginConnect(IPAddress.Loopback, _port, null, null);
                if (!connect.AsyncWaitHandle.WaitOne(Math.Min(timeoutMs, 1000)))
                {
                    throw new IOException("no connection");
                }
                client.EndConnect(connect);
                NetworkStream net = client.GetStream();
                byte[] h = Encoding.ASCII.GetBytes(head.ToString());
                net.Write(h, 0, h.Length);
                if (payload != null)
                {
                    net.Write(payload, 0, payload.Length);
                }
                var all = new MemoryStream(1024);
                var buffer = new byte[4096];
                while (true)
                {
                    double left = until - TextClock.Now;
                    if (left <= 0.0 || !client.Client.Poll((int)(left * 1000000.0), SelectMode.SelectRead))
                    {
                        throw new IOException("too slow");   // (nothing within the time: the wait ends here, whatever the runtime does with time-outs)
                    }
                    int read = net.Read(buffer, 0, buffer.Length);   // (readable: data, or the end of the answer)
                    if (read <= 0)
                    {
                        break;
                    }
                    all.Write(buffer, 0, read);
                }
                string text = Encoding.UTF8.GetString(all.GetBuffer(), 0, (int)all.Length);
                int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (!text.StartsWith("HTTP/1.1 200", StringComparison.Ordinal) && !text.StartsWith("HTTP/1.0 200", StringComparison.Ordinal))
                {
                    return null;
                }
                return split >= 0 ? text.Substring(split + 4) : null;
            }
            finally
            {
                Interlocked.CompareExchange(ref _socket, null, client);
                client.Close();
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string Exit()
        {
            try
            {
                return _process.ExitCode.ToString();
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private bool Fail(string why)
        {
            lock (_failing)
            {
                if (_state == TextServerState.Failed)
                {
                    return false;   // (one line: the watchdog and the worker may both end the same start)
                }
                Why = why;
                _state = TextServerState.Failed;
            }
            _log?.Invoke("RLO Texts: " + why + "; the written lines are used.");
            return false;
        }

        private void Kill()
        {
            Process p = Interlocked.Exchange(ref _process, null);   // (once: Dispose and the worker may both end it)
            if (p != null)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                }
                catch (Exception)
                {
                    // (gone already)
                }
                p.Dispose();
            }
            IntPtr job = Interlocked.Exchange(ref _job, IntPtr.Zero);
            if (job != IntPtr.Zero)
            {
                CloseHandle(job);   // (kill on close: anything still in the job ends now)
            }
            string head = Interlocked.Exchange(ref _headFile, null);
            if (head != null && _start.Cache != null)
            {
                try
                {
                    File.Delete(Path.Combine(_start.Cache, head));   // (this server's own head state: the cache never grows)
                }
                catch (Exception)
                {
                    // (in use or gone: the next start's sweep takes it)
                }
            }
        }

        /// <summary>The head files a server of an earlier session left behind (a crash), taken away at a start.</summary>
        private static void Stale(string cache)
        {
            try
            {
                foreach (string file in Directory.GetFiles(cache, "rlo-head-*.bin"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        // (another game's server has it open: left)
                    }
                }
            }
            catch (Exception)
            {
                // (no folder: nothing to sweep)
            }
        }

        /// <summary>
        /// A child process of llama.cpp's kept offline and to the mod's own arguments: no LLAMA_ARG_ variable (which would set
        /// any of its options, a host or a model's address among them) and no HF_ one (Hugging Face's downloads) reaches it.
        /// </summary>
        public static void Offline(ProcessStartInfo info)
        {
            var drop = new List<string>(4);
            foreach (System.Collections.DictionaryEntry e in info.EnvironmentVariables)
            {
                string name = e.Key as string;
                if (name != null && (name.StartsWith("LLAMA_ARG_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("HF_", StringComparison.OrdinalIgnoreCase)))
                {
                    drop.Add(name);
                }
            }
            for (int i = 0; i < drop.Count; i++)
            {
                info.EnvironmentVariables.Remove(drop[i]);
            }
        }

        /// <summary>
        /// True for a variable that points a GPU driver's shader cache somewhere of its own: the ones Steam sets for a game
        /// it starts, for its shader pre-caching (NVIDIA's __GL_SHADER_DISK_CACHE_PATH, _APP_NAME, _READ_ONLY_APP_NAME and
        /// _SKIP_CLEANUP; Mesa's and DXVK's caches), and any other __GL_SHADER_DISK_CACHE one. Game-free (EconomyCheck, TextsCacheChecks.cs).
        /// </summary>
        public static bool ShaderCacheVariable(string name)
        {
            return name != null && (name.StartsWith("__GL_SHADER_DISK_CACHE", StringComparison.OrdinalIgnoreCase) || name.StartsWith("MESA_SHADER_CACHE", StringComparison.OrdinalIgnoreCase)
                                    || name.StartsWith("MESA_GLSL_CACHE", StringComparison.OrdinalIgnoreCase) || name.StartsWith("MESA_DISK_CACHE", StringComparison.OrdinalIgnoreCase)
                                    || name.StartsWith("DXVK_STATE_CACHE", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// A child process of llama.cpp's given the GPU driver's own shader cache: no <see cref="ShaderCacheVariable"/> reaches
        /// it (see the summary: under Steam's, the driver kept no shader for llama.cpp, and every start built them all again).
        /// Returns the names taken out, for the log (empty: none). Measured outside the game (the 2B on an RTX
        /// 5070 Ti, the server started as the runner starts it): with Steam's variables every start's first reply 9.9 s, the
        /// processor busy throughout, and nothing kept; without them 10.1 s once, then 0.05 s each start, killed or not.
        /// </summary>
        public static string OwnShaderCache(ProcessStartInfo info)
        {
            var drop = new List<string>(4);
            foreach (System.Collections.DictionaryEntry e in info.EnvironmentVariables)
            {
                string name = e.Key as string;
                if (ShaderCacheVariable(name))
                {
                    drop.Add(name);
                }
            }
            drop.Sort(StringComparer.OrdinalIgnoreCase);   // (the log's order the same each start)
            for (int i = 0; i < drop.Count; i++)
            {
                info.EnvironmentVariables.Remove(drop[i]);
            }
            return string.Join(", ", drop.ToArray());
        }

        // ---------------------------------------------------------------- a job object: the server ends with the game, even in a crash

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint KillOnJobClose = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, int length);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private void TieToGame(Process p)
        {
            try
            {
                _job = CreateJobObject(IntPtr.Zero, null);
                if (_job == IntPtr.Zero)
                {
                    return;
                }
                var limits = new ExtendedLimits();
                limits.Basic.LimitFlags = KillOnJobClose;
                if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, ref limits, Marshal.SizeOf(typeof(ExtendedLimits)))
                    || !AssignProcessToJobObject(_job, p.Handle))
                {
                    _log?.Invoke("RLO Texts: the server could not be tied to the game's process; it is still ended when the game closes normally.");
                }
            }
            catch (Exception e)
            {
                _log?.Invoke("RLO Texts: no job object (" + e.GetType().Name + "); the server is ended when the game closes normally.");
            }
        }

        // ---------------------------------------------------------------- on a GPU: no power throttling (Windows' EcoQoS)

        private const int ProcessPowerThrottling = 4;
        private const uint ThrottleExecutionSpeed = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerThrottling
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottling info, int length);

        // A server on a GPU is a windowless process in the background, which Windows may run slowly to save power (EcoQoS)
        // while its thread hands each step to the GPU. llama.cpp keeps its own worker threads out of that at normal
        // priority, but not the thread that drives the GPU: the whole process is kept out here. Windows 10 1709 and later;
        // where the call is missing, nothing changes.
        private void FullSpeed(Process p)
        {
            try
            {
                var state = new PowerThrottling { Version = 1, ControlMask = ThrottleExecutionSpeed, StateMask = 0 };
                if (!SetProcessInformation(p.Handle, ProcessPowerThrottling, ref state, Marshal.SizeOf(typeof(PowerThrottling))))
                {
                    _log?.Invoke("RLO Texts: Windows would not keep the server out of power throttling; it runs as Windows decides.");
                }
            }
            catch (Exception e)
            {
                _log?.Invoke("RLO Texts: no power throttling setting on this Windows (" + e.GetType().Name + "); the server runs as Windows decides.");
            }
        }
    }

    /// <summary>
    /// The machine's real cores (GetLogicalProcessorInformation: one record per core), for the threads' choice
    /// (TextSpeed.Threads). Where the system cannot say: the logical processors, halved when there are four or more (a
    /// core running two threads is the rule on such machines). Asked once, off the game's thread. Game-free.
    /// </summary>
    public static class TextCores
    {
        private const int RelationProcessorCore = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorInfo
        {
            public UIntPtr Mask;
            public int Relationship;
            public long Union0;
            public long Union1;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint length);

        public static int Real()
        {
            try
            {
                uint length = 0;
                GetLogicalProcessorInformation(IntPtr.Zero, ref length);
                if (length > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)length);
                    try
                    {
                        if (GetLogicalProcessorInformation(buffer, ref length))
                        {
                            int size = Marshal.SizeOf(typeof(ProcessorInfo)), cores = 0;
                            for (int at = 0; at + size <= length; at += size)
                            {
                                var info = (ProcessorInfo)Marshal.PtrToStructure(new IntPtr(buffer.ToInt64() + at), typeof(ProcessorInfo));
                                cores += info.Relationship == RelationProcessorCore ? 1 : 0;
                            }
                            if (cores > 0)
                            {
                                return cores;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch (Exception)
            {
                // (not Windows, or the call is missing: the rule below)
            }
            int logical = Environment.ProcessorCount;
            return TextSpeed.RealCores(logical, logical >= 4);
        }
    }
}
