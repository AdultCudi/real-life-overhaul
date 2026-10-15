using System;
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

    /// <summary>How the runner stands.</summary>
    public enum TextServerState
    {
        Off,
        Loading,     // the server is starting and reading the model
        Ready,
        Failed       // it would not start, or stopped: the written lines are used until the pack is switched off and on
    }

    /// <summary>What the runner is started with.</summary>
    public sealed class TextServerStart
    {
        public string Exe;           // the pack's llama-server.exe
        public string Model;         // the pack's .gguf
        public string Log;           // where the server writes its own log (null: nowhere)
        public int Threads = 2;      // 2 to 4
        public string Device;        // null for the processor; else a llama.cpp device name ("Vulkan0")
        public double LoadSeconds = 90.0;   // the most the model may take to load before the runner gives up
        public string Cache;         // a folder for the cached head's state (the server's slot file); null: no prompt cache
        public string SpeedFor;      // what a speed test is for (pack, device, threads)
        public TextSpeedResult Known; // a speed measured before for the same: no test this time
    }

    /// <summary>
    /// The runner: llama.cpp's own server (llama-server.exe, MIT) started by the mod as a child process, on 127.0.0.1 and a
    /// free port of the moment, with a key of its own, at low priority, and one worker thread of the mod that takes the
    /// queue's requests one at a time and asks the server for each over a local socket. Chosen over loading llama.cpp's
    /// DLLs into the game (see TextsDirector's summary for the reasons): a native crash or a leak stays in the child.
    ///
    ///   start      a free port from the system, the exe started hidden (no window), BelowNormal, its own log; tied to a
    ///              Windows job object that ends it when the game's process ends, even in a crash; then the worker waits
    ///              for /health (the model loading, within <see cref="TextServerStart.LoadSeconds"/>) and writes one short
    ///              reply to warm it (the first reply on a GPU builds its shaders)
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
        private bool _headSaved;
        private string _headFile;

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
            _thread = new Thread(Run) { IsBackground = true, Name = "RLO Texts", Priority = ThreadPriority.BelowNormal };
            _thread.Start();
        }

        public void Dispose()
        {
            _stop = true;
            _wake.Set();
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
                if (!Launch() || !WaitReady() || !WarmUp() || !SpeedTest())
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
                Fail("the worker: " + e.GetType().Name + ": " + e.Message);
                Kill();
                _queue.Clear();
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
            var args = new StringBuilder(512);
            args.Append("-m \"").Append(_start.Model).Append("\" --host 127.0.0.1 --port ").Append(_port)
                .Append(" --api-key ").Append(_key)
                .Append(" -t ").Append(_start.Threads).Append(" -tb ").Append(_start.Threads)
                .Append(" -c 1024 -np 1 --no-webui --prio -1");
            if (_start.Cache != null)
            {
                Directory.CreateDirectory(_start.Cache);
                args.Append(" --slot-save-path \"").Append(_start.Cache.TrimEnd('\\', '/')).Append('"');
            }
            if (_start.Device != null)
            {
                args.Append(" -dev ").Append(_start.Device).Append(" -ngl 99");
            }
            else
            {
                args.Append(" -dev none -ngl 0");
            }
            if (_start.Log != null)
            {
                args.Append(" -lv 2 --log-file \"").Append(_start.Log).Append('"');   // (warnings and errors only: a long session's log stays small)
            }
            var info = new ProcessStartInfo(_start.Exe, args.ToString())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(_start.Exe),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
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
            try
            {
                _process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception)
            {
                // (it may have ended already; the next look sees it)
            }
            TieToGame(_process);
            _log?.Invoke("RLO Texts: the server started on 127.0.0.1:" + _port + " (" + (_start.Device ?? "the processor") + ", " + _start.Threads + " threads, " + Path.GetFileName(_start.Model) + ").");
            return true;
        }

        private bool WaitReady()
        {
            double begun = TextClock.Now;
            while (!_stop)
            {
                if (_process.HasExited)
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

        // One short reply before the first real one (the first reply builds a GPU's shaders and fills the processor's
        // caches), then the head read and its state saved for every request after.
        private bool WarmUp()
        {
            try
            {
                double begun = TextClock.Now;
                Post("/completion", TextJson.Request(TextCard.ChatMl(TestCard()), 1, 1), 30000);
                LoadedIn += TextClock.Now - begun;
            }
            catch (Exception e)
            {
                return Fail("the first reply failed: " + e.Message);
            }
            if (_start.Cache == null)
            {
                return true;
            }
            try
            {
                string head = Post("/completion", TextJson.Request(Head, 1, 0), 30000);
                _headFile = "rlo-head-" + _port + ".bin";
                string saved = Post("/slots/0?action=save", "{\"filename\":\"" + _headFile + "\"}", 10000);
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

        // The head's state put back, so the server reads only the card (its own cache does this by itself for a model it can reuse in part).
        private void Restore(string prompt)
        {
            if (!_headSaved || !prompt.StartsWith(Head, StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                Post("/slots/0?action=restore", "{\"filename\":\"" + _headFile + "\"}", 2000);
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
            double begun = TextClock.Now;
            try
            {
                string prompt = TextCard.ChatMl(TestCard());
                Restore(prompt);
                string answer = Post("/completion", TextJson.Request(prompt, 7, TextSpeed.ReplyTokens), (int)(TextSpeed.TestSeconds * 1000.0));
                result.PromptMsPerToken = TextJson.Number(answer, "prompt_per_token_ms");
                result.TokensPerSecond = TextJson.Number(answer, "predicted_per_second");
                if (double.IsNaN(result.PromptMsPerToken) || double.IsNaN(result.TokensPerSecond))
                {
                    throw new IOException("no timings");
                }
            }
            catch (Exception)
            {
                // Past the cap: at least as slow as the cap says.
                double spent = Math.Max(0.1, TextClock.Now - begun);
                result.Capped = true;
                result.PromptMsPerToken = spent * 1000.0 / TextSpeed.CardTokens;
                result.TokensPerSecond = TextSpeed.ReplyTokens / spent;
            }
            Speed = result;
            _log?.Invoke("RLO Texts: speed test: " + result.PromptMsPerToken.ToString("0.0") + " ms a token read, " + result.TokensPerSecond.ToString("0.0")
                         + " tokens a second written: an ordinary reply in about " + result.Estimate.ToString("0.0") + " s" + (result.Capped ? " (past the test's cap)" : "") + ".");
            return true;
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

        // One request over a fresh local socket (Connection: close), the answer's body as text. Throws on a time-out.
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
            using (var client = new TcpClient(AddressFamily.InterNetwork))
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
                using (NetworkStream net = client.GetStream())
                {
                    byte[] h = Encoding.ASCII.GetBytes(head.ToString());
                    net.Write(h, 0, h.Length);
                    if (payload != null)
                    {
                        net.Write(payload, 0, payload.Length);
                    }
                    var all = new MemoryStream(1024);
                    var buffer = new byte[4096];
                    double until = TextClock.Now + timeoutMs / 1000.0;
                    int read;
                    while ((read = net.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        all.Write(buffer, 0, read);
                        if (TextClock.Now > until)
                        {
                            throw new IOException("too slow");
                        }
                    }
                    string text = Encoding.UTF8.GetString(all.GetBuffer(), 0, (int)all.Length);
                    int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (!text.StartsWith("HTTP/1.1 200", StringComparison.Ordinal) && !text.StartsWith("HTTP/1.0 200", StringComparison.Ordinal))
                    {
                        return null;
                    }
                    return split >= 0 ? text.Substring(split + 4) : null;
                }
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
            Why = why;
            _state = TextServerState.Failed;
            _log?.Invoke("RLO Texts: " + why + "; the written lines are used.");
            return false;
        }

        private void Kill()
        {
            Process p = _process;
            _process = null;
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
            if (_job != IntPtr.Zero)
            {
                CloseHandle(_job);   // (kill on close: anything still in the job ends now)
                _job = IntPtr.Zero;
            }
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
