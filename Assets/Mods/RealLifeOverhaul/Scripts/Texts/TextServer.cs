using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Real seconds since the mod started, the same on every thread: the queue's and the worker's clock.</summary>
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
        WarmUp,      // the first reply, which builds a GPU's shaders
        Head,        // the prompt's head read and its state saved
        Speed        // the speed test
    }

    /// <summary>
    /// The start's watch: the stage the worker is in, since when, and its limit. The runner's watchdog asks it twice a
    /// second; a stage past its limit and <see cref="Grace"/> is overdue once, and the start then ends for certain, whatever
    /// wait it is stuck in. Game-free and thread-safe. COST: a lock and a few numbers a stage; nothing on the game's thread.
    /// </summary>
    public sealed class TextStartWatch
    {
        public const double Grace = 5.0;   // seconds past a stage's own limit before the watchdog ends it

        private readonly object _lock = new object();
        private TextStartStage _stage;
        private double _began, _limit = -1.0;
        private bool _over;

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

        public void Enter(TextStartStage stage, double limit, double now)
        {
            lock (_lock)
            {
                _stage = stage;
                _began = now;
                _limit = Math.Max(0.0, limit);
            }
        }

        /// <summary>Ends the stage; the seconds it took.</summary>
        public double Leave(double now)
        {
            lock (_lock)
            {
                _limit = -1.0;
                return Math.Max(0.0, now - _began);
            }
        }

        /// <summary>True once, when the stage under way is past its limit and the grace: the start is over from then.</summary>
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
        Failed       // it would not start, or stopped answering past its restarts: written lines until the pack is switched off and on
    }

    /// <summary>The GPU driver's shader cache at the first reply, as the start's line names it.</summary>
    public enum TextShaderCache
    {
        None,        // on the processor: no shaders
        Unknown,     // the system could not give the server's processor time
        Used,        // the driver had the shaders kept: nothing built
        Created,     // built for the first time for this pack, device and runtime, kept by the driver for the next start
        Missing      // built again although built before: the driver did not keep them
    }

    /// <summary>What the runner is started with.</summary>
    public sealed class TextServerStart
    {
        public string Exe;           // the pack's llama-server.exe
        public string Model;         // the pack's .gguf
        public string Log;           // where the server writes its own log; null for nowhere
        public int Threads = 2;      // 1 to 4, at most 2 on a GPU (TextSpeed.Threads)
        public string Device;        // null for the processor; else a llama.cpp device name ("Vulkan0")
        public double LoadSeconds = 90.0;   // seconds the model may take to load at most
        public double WarmSeconds = 30.0;   // seconds the first reply on the processor, or the head's read, may take at most
        public double BuildSeconds = 90.0;  // seconds the first reply may take on a GPU, building the shaders the driver did not keep (22 s seen on a fast processor)
        public string Cache;         // a folder for the cached head's state; null for no prompt cache
        public string SpeedFor;      // what a speed test is for: pack, device, threads
        public TextSpeedResult Known; // a speed measured before for the same: no test this time
        public double ProbeSeconds;  // seconds a probe after a missed reply waits for the slot at most; 0 by the speed measured
        public string System = TextCard.System;   // the pack's system turn, with which the cached head starts
    }

    /// <summary>
    /// The runner: llama.cpp's own server (llama-server.exe) started as a child process on 127.0.0.1 with a key of its own,
    /// so a native crash or leak stays in the child, and one worker thread that takes the queue's requests one at a time.
    /// The start's stages (launch, load, first reply, head, speed test) are each timed, limited and watched. A missed reply
    /// is followed by a probe of the server's slot, and a hung or stopped server is started again in the background, at
    /// most TextHang.RestartsMost times a session. Game-free: the integration check runs it with the Lite model.
    /// COST: nothing on the game's thread; a thread that sleeps until there is a request.
    /// </summary>
    public sealed partial class TextServer : IDisposable, ITextRunner
    {
        public const double TimeOut = 4.0;          // seconds a reply may take on the processor before it is given up
        public const double TimeOutGpu = 2.0;       // ...on a GPU
        public const int WakeMs = 250;              // the worker's longest sleep between two looks at the queue
        public const double LaunchSeconds = 10.0;   // seconds the exe may take to start: a port, the process, the job object
        public const double SaveSeconds = 10.0;     // seconds the head's state may take to be saved, after its read
        public const double RestoreSeconds = 2.0;   // seconds the head's state may take to be put back before the speed test
        public const int DogMs = 500;               // milliseconds between two looks of the start's watchdog
        public const int NamesMost = 32;            // names whose tokens are kept; past this they start again
        public const int TokenizeMs = 500;          // milliseconds a /tokenize may take before the request goes without its ban

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
        private readonly Random _seeds = new Random(Guid.NewGuid().GetHashCode());   // a fresh sampling seed each request, so a card does not repeat its words
        private double _took, _headMs;
        private string _detail;   // what the stage under way adds to its log line, or null
        private bool _headSaved;
        private string _headFile;
        private volatile TextStartWatch _watch = new TextStartWatch();   // a fresh one each start, restarts included
        private int _restarts;
        private readonly object _failing = new object();
        private Timer _dog;
        private TcpClient _socket;   // the worker's request under way, closed by the watchdog or a stop to end its wait
        private readonly Dictionary<string, List<int>> _names = new Dictionary<string, List<int>>(4, StringComparer.Ordinal);   // the worker's: a name's banned tokens
        private string _head;

        /// <summary>The fixed head of every prompt of the first packs: the system turn and the opening of the user's.</summary>
        public static string Head => HeadOf(TextCard.System);

        /// <summary>The fixed head of every prompt of a system turn.</summary>
        public static string HeadOf(string system) => "<|im_start|>system\n" + system + "<|im_end|>\n<|im_start|>user\n";

        private string OwnHead => _head ?? (_head = HeadOf(_start.System ?? TextCard.System));

        /// <summary>The speed measured, or known from before; null until it is.</summary>
        public TextSpeedResult Speed { get; private set; }

        /// <summary>Milliseconds the cached head saves on every request; 0 without it.</summary>
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

        /// <summary>Seconds it took to be ready; 0 until it is.</summary>
        public double LoadedIn { get; private set; }

        /// <summary>Replies written and the mean seconds each took.</summary>
        public int Written => _written;
        public double MeanSeconds => _written > 0 ? _took / _written : 0.0;

        /// <summary>Requests the model failed on: a time-out, an error.</summary>
        public int Failed => _missed;

        /// <summary>Times the server was started again this session, hung or stopped by itself.</summary>
        public int Restarts => _restarts;

        /// <summary>The game is quiet enough for the background: set by the game's thread, read by the worker.</summary>
        public bool Quiet
        {
            set => _quiet = value;
        }

        /// <summary>Wakes the worker for a new request rather than at its next quarter second.</summary>
        public void Nudge()
        {
            _wake.Set();
        }

        /// <summary>Starts the server and the worker; returns at once, the loading being the worker's.</summary>
        public void Start()
        {
            if (_state != TextServerState.Off)
            {
                return;
            }
            _state = TextServerState.Loading;
            _stop = false;
            // On a GPU the worker must not read the answer late.
            _thread = new Thread(Run) { IsBackground = true, Name = "RLO Texts", Priority = LowPriority(_start.Device) ? ThreadPriority.BelowNormal : ThreadPriority.Normal };
            _thread.Start();
        }

        public void Dispose()
        {
            _stop = true;
            _wake.Set();
            Interlocked.Exchange(ref _dog, null)?.Dispose();
            try
            {
                Interlocked.Exchange(ref _socket, null)?.Close();   // a request under way ends its wait now
            }
            catch (Exception)
            {
                // closed already
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

        private void Run()
        {
            try
            {
                if (!Begin(true))
                {
                    return;
                }
                while (!_stop)
                {
                    Process p = _process;
                    if (p == null || p.HasExited)
                    {
                        if (_stop || !Recover("stopped by itself (exit code " + Exit() + ")"))
                        {
                            return;
                        }
                        continue;
                    }
                    TextJob job = _queue.Take(TextClock.Now, _quiet);
                    if (job == null)
                    {
                        _wake.WaitOne(WakeMs);
                        continue;
                    }
                    if (Write(job) && Hung() && !Recover("did not answer"))
                    {
                        return;
                    }
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

        // The start's stages with a fresh watch and its watchdog, the speed test only at the first start. False when the server
        // would not start: it is killed and the waiting requests dropped.
        private bool Begin(bool first)
        {
            _watch = new TextStartWatch();
            _dog = new Timer(Watchdog, null, DogMs, DogMs);
            bool ready = Stage(TextStartStage.Launch, LaunchSeconds, Launch) && Stage(TextStartStage.Load, _start.LoadSeconds, WaitReady)
                         && Stage(TextStartStage.WarmUp, WarmLimit(_start), WarmUp) && Stage(TextStartStage.Head, _start.WarmSeconds + SaveSeconds, KeepHead)
                         && (!first || Stage(TextStartStage.Speed, TextSpeed.TestSeconds + RestoreSeconds, SpeedTest));
            Interlocked.Exchange(ref _dog, null)?.Dispose();
            if (!ready)
            {
                Kill();
                _state = TextServerState.Failed;
                _queue.Clear();
                return false;
            }
            _state = TextServerState.Ready;
            return true;
        }

        // After a missed reply, the server's slot is looked at until it is free or the probe's time is up. True when the server
        // is hung: its slot still at work, or no answer.
        private bool Hung()
        {
            double begun = TextClock.Now;
            double limit = _start.ProbeSeconds > 0.0 ? _start.ProbeSeconds : TextHang.ProbeSeconds(Speed);
            double until = begun + limit;
            TextSlotLook look = TextSlotLook.Silent;
            while (!_stop)
            {
                Process p = _process;
                double left = until - TextClock.Now;
                if (p == null || p.HasExited || left <= 0.05)
                {
                    break;   // a server gone is no better than a hung one; with the time up the last look decides
                }
                try
                {
                    string slots = Get("/slots", Ms(left));
                    look = slots != null ? TextHang.Read(slots) : TextSlotLook.Unreadable;   // an answer other than 200: the server's loop answered
                }
                catch (Exception)
                {
                    look = TextSlotLook.Silent;   // no answer in the time left, or no connection
                }
                if (TextHang.Settled(look))
                {
                    break;
                }
                _wake.WaitOne(Ms(TextHang.LookEvery));
            }
            bool hung = !_stop && TextHang.Hung(look);
            Check("Texts check (server answers): a reply did not come in its time; then " + TextHang.Name(look) + " after "
                  + (TextClock.Now - begun).ToString("0.0", CultureInfo.InvariantCulture) + " s of the probe's " + limit.ToString("0.0", CultureInfo.InvariantCulture) + " s: "
                  + (hung ? "FAIL (the server is hung: started again)" : "PASS (a slow moment, not a hang: nothing restarted)"));
            return hung;
        }

        // Kills a hung or stopped server and starts it again, at most TextHang.RestartsMost times a session; past that the
        // runner stops for the session. False when it is not running.
        private bool Recover(string why)
        {
            if (_stop)
            {
                return false;
            }
            if (!TextHang.MayRestart(_restarts))
            {
                Check("Texts check (server restarts): the server " + why + " after " + _restarts + " restart(s) this session, the most is " + TextHang.RestartsMost
                      + "; the written lines stay until the pack is switched off and on: FAIL");
                Fail("the server stopped answering " + (_restarts + 1) + " times this session");
                Kill();
                _queue.Clear();
                return false;
            }
            _restarts++;
            _state = TextServerState.Loading;
            _log?.Invoke("RLO Texts: the server " + why + "; it is started again in the background (" + _restarts + " of " + TextHang.RestartsMost
                         + " this session); the written lines are used meanwhile.");
            Kill();
            double begun = TextClock.Now;
            bool ok = Begin(false);
            Check("Texts check (server restart): started again in " + (TextClock.Now - begun).ToString("0.0", CultureInfo.InvariantCulture) + " s, "
                  + (ok ? "the head " + (_headSaved ? "kept" : "read with each request") + ", " + _restarts + " of " + TextHang.RestartsMost + " restarts used: PASS"
                        : "it would not start (" + Why + "): FAIL"));
            return ok;
        }

        // A self-check line; the log writes it with Developer diagnostics on.
        private void Check(string line)
        {
            _log?.Invoke(line);
        }

        // One stage of the start, timed and watched, logged with its time so a start that hangs shows where.
        private bool Stage(TextStartStage stage, double limit, Func<bool> step)
        {
            _watch.Enter(stage, limit, TextClock.Now);
            _detail = null;
            bool ok = !_stop && step();
            double took = _watch.Leave(TextClock.Now);
            if (_watch.Over)
            {
                return false;   // the watchdog ended it and wrote its line
            }
            if (ok)
            {
                _log?.Invoke("RLO Texts: start-up, " + TextStartWatch.Name(stage) + " in " + took.ToString("0.0", CultureInfo.InvariantCulture) + " s"
                    + (_detail != null ? " (" + _detail + ")" : string.Empty) + ".");
            }
            return ok;
        }

        // The watchdog, on a timer while the start runs: a stage past its limit and the grace ends the start for certain, the
        // socket closed and the server killed so the worker's wait returns (Mono on Windows ignores a socket's ReceiveTimeout).
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
                // the process gone already: the worker sees it
            }
        }

        // One request asked of the server; true when the server did not answer it in its time, so the probe follows.
        private bool Write(TextJob job)
        {
            double begun = TextClock.Now;
            double limit = Math.Min(job.Deadline - begun, _start.Device != null ? TimeOutGpu : TimeOut);
            string why = null, words = null;
            bool late = false;
            if (limit <= 0.05)
            {
                why = "its time ran out before it started";
            }
            else
            {
                try
                {
                    Restore(job.Prompt);
                    string bias = job.BanName != null ? TextDecoding.Bias(NameTokens(job.BanName)) : null;
                    double left = job.Deadline - TextClock.Now;
                    string body = TextJson.Request(job.Prompt, _seeds.Next(1, int.MaxValue), job.Tokens > 0 ? job.Tokens : TextJson.Tokens, job.Grammar, bias);
                    string answer = Post("/completion", body, (int)(Math.Max(0.05, Math.Min(left, limit)) * 1000.0));
                    words = TextJson.Field(answer, "content");
                    job.Cut = words != null && TextJson.Cut(answer);
                    if (words == null)
                    {
                        why = "no words in the server's answer";
                    }
                }
                catch (Exception e)
                {
                    late = e is SocketException || e is IOException;
                    why = late ? "it took longer than " + limit.ToString("0.0", CultureInfo.InvariantCulture) + " s" : e.GetType().Name + ": " + e.Message;
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
            return late && !_stop;
        }
    }
}
