using System;
using System.Collections.Generic;
using System.Threading;

namespace RealLifeOverhaul.Texts
{
    /// <summary>A runner that sends cards to an AI service; the checks put a pretend one in.</summary>
    public interface ITextServiceRunner : ITextRunner
    {
        /// <summary>True while the service rests after a fault: nothing is sent meanwhile.</summary>
        bool Resting { get; }
    }

    /// <summary>What a service runner is started with.</summary>
    public sealed class TextServiceStart
    {
        public TextProviderInfo Provider;
        public string Base;               // the address asked (TextProviders.Base)
        public string Model;              // the model named by the player; null for the provider's own, or the first listed
        public string Key;                // an API key; null for a sign-in or a program on this PC
        public TextKeys Keys;             // where the ChatGPT sign-in's tokens are kept
        public string ClientId;           // the game client id the Player2 App is asked a key with; null for every other service
        public string Auth = TextProviders.ChatGptAuth;   // the sign-in's address; the checks point it at a pretend one
        public int Workers = TextService.Workers;
    }

    /// <summary>
    /// Your own AI's runner: cards sent to the AI service the player chose, by at most <see cref="Workers"/> threads. After a
    /// fault it rests and misses every waiting request at once; a refused key or model stops it until mended. Keys stay in
    /// the request's headers, never the log. Game-free. COST: worker threads that sleep until there is a request.
    /// </summary>
    public sealed class TextService : IDisposable, ITextServiceRunner
    {
        public const int Workers = 2;               // requests in flight at most
        public const int WakeMs = 250;              // a worker's longest sleep between two looks at the queue
        public const int PerMinute = 20;            // requests started a minute at most; the free tiers allow 10 to 30
        public const int BackgroundPerMinute = 8;   // ...of which the city's background

        private readonly TextQueue _queue;
        private readonly TextServiceStart _start;
        private readonly ITextWeb _web;
        private readonly Action<string> _log;
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly List<Thread> _threads = new List<Thread>(Workers);
        private readonly object _lock = new object();
        private volatile TextServerState _state = TextServerState.Off;
        private volatile bool _stop, _quiet = true, _noReasoning, _noTemperature;
        private string _model;
        private double _restUntil;
        private TextFault _lastFault;
        private int _inRow, _written, _missed;
        private int _closedLogged;   // 1 once the closing line is written: the workers share it
        private double _took;
        private readonly object _appLock = new object();   // one worker asks the Player2 App for the key at a time
        private string _appKey;                            // Player2's key for this player: in memory only, never kept, shown or logged
        private double _beatAt;                            // the clock time the next health call is due
        private volatile TextAppState _appState = TextAppState.SignedIn;

        public TextService(TextQueue queue, TextServiceStart start, ITextWeb web, Action<string> log)
        {
            _queue = queue;
            _start = start;
            _web = web;
            _log = log;
            _model = start.Model ?? start.Provider.Model;
            queue.SetBudget(PerMinute, BackgroundPerMinute);
        }

        public TextServerState State => _state;

        public bool Resting
        {
            get
            {
                lock (_lock)
                {
                    return TextClock.Now < _restUntil;
                }
            }
        }

        public bool Quiet
        {
            set => _quiet = value;
        }

        public string Why { get; private set; }

        /// <summary>The fault that stopped it or that it rests after; None while all is well.</summary>
        public TextFault Fault => _lastFault;

        /// <summary>The model asked; null until known.</summary>
        public string Model => _model;

        public TextProviderInfo Provider => _start.Provider;

        /// <summary>For the Player2 App: why it gives no key now (not running, nobody signed in); SignedIn otherwise.</summary>
        public TextAppState AppState => _appState;

        /// <summary>Replies that came back with words, and the mean seconds each took.</summary>
        public int Written => _written;
        public int Missed => _missed;
        public double MeanSeconds => _written > 0 ? _took / _written : 0.0;

        public void Nudge()
        {
            _wake.Set();
        }

        /// <summary>Starts the workers; returns at once. Failed at once when there is nothing to sign in with.</summary>
        public void Start()
        {
            if (_state != TextServerState.Off)
            {
                return;
            }
            TextProviderInfo p = _start.Provider;
            if (p.Access == TextAccess.Key && string.IsNullOrEmpty(_start.Key))
            {
                Stop(TextFault.Key, "no API key is kept for " + p.Name);
                return;
            }
            if (p.Access == TextAccess.SignIn && (_start.Keys == null || !TextSignIn.SignedIn(_start.Keys)))
            {
                Stop(TextFault.Key, "not signed in to ChatGPT");
                return;
            }
            if (p.Access == TextAccess.App && string.IsNullOrEmpty(_start.ClientId))
            {
                Stop(TextFault.Key, TextPlayer2.Why(TextAppState.NoClientId));
                return;
            }
            if (string.IsNullOrEmpty(_start.Base))
            {
                Stop(TextFault.Model, "the address for " + p.Name + " is not one on this PC");
                return;
            }
            _stop = false;
            _state = _model == null ? TextServerState.Loading : TextServerState.Ready;
            for (int i = 0; i < Math.Max(1, _start.Workers); i++)
            {
                var t = new Thread(Run) { IsBackground = true, Name = "RLO Texts service " + (i + 1), Priority = ThreadPriority.BelowNormal };   // each mostly waits on the network
                _threads.Add(t);
                t.Start(i);
            }
        }

        /// <summary>Stops it taking anything more at once, before another runner takes the queue; a request under way ends by its own time.</summary>
        public void Halt()
        {
            _stop = true;
            _wake.Set();
        }

        public void Dispose()
        {
            _stop = true;
            for (int i = 0; i < _threads.Count; i++)
            {
                _wake.Set();
            }
            for (int i = 0; i < _threads.Count; i++)
            {
                _threads[i].Join(TextWeb.GraceMs);   // a request under way ends by its own time; the thread is a background one
            }
            _threads.Clear();   // what waits is the hub's to hand over, and a runner after this one may share the queue
            if (_state != TextServerState.Failed)
            {
                _state = TextServerState.Off;
            }
        }

        private void Run(object index)
        {
            try
            {
                if ((int)index == 0 && _model == null && !FindModel())
                {
                    return;
                }
                while (!_stop && _state != TextServerState.Failed)
                {
                    if (_state != TextServerState.Ready || Resting)
                    {
                        _queue.MissAll(Why ?? "the service rests");   // the hub falls back at once rather than wait for nothing
                        _wake.WaitOne(WakeMs);
                        continue;
                    }
                    TextJob job = _queue.Take(TextClock.Now, _quiet);
                    if (job == null)
                    {
                        _wake.WaitOne(WakeMs);
                        continue;
                    }
                    _wake.Set();   // the other worker looks too: a second request may be waiting
                    Write(job);
                }
            }
            catch (Exception e)
            {
                if (TextServiceRules.Closing(e.GetType().Name, _stop))
                {
                    Closed();   // the game closing (the engine aborts its threads) or the service switched off: not a fault
                }
                else
                {
                    Stop(TextFault.Busy, "the worker: " + e.GetType().Name);
                }
                _queue.MissAll("the service stopped");
            }
        }

        // A worker ended by the game's closing or the service switched off: one plain line for all the workers, and the state
        // never Failed.
        private void Closed()
        {
            _stop = true;
            if (Interlocked.Exchange(ref _closedLogged, 1) == 0)
            {
                Log("Your own AI: " + _start.Provider.Name + " stopped (the game is closing).");
            }
        }

        // No model named: the service's first listed one, asked again after a rest while nothing answers; Failed when it
        // refuses or lists none.
        private bool FindModel()
        {
            int tries = 0;
            while (!_stop)
            {
                string secret = Secret(out TextFault fault, out string why);
                if (secret == null && _start.Provider.Access != TextAccess.Local)
                {
                    if (TextServiceRules.Stops(fault))
                    {
                        Stop(fault, why);
                        return false;
                    }
                    Rest(fault, why, TextServiceRules.Rest(TextFault.Network, ++tries, 0));
                    Rested();
                    continue;
                }
                TextWebAnswer a = _web.Send(TextServiceRules.Models(_start.Provider, _start.Base, secret), (int)(TextServiceRules.ListSeconds * 1000));
                if (a.Status == 0 || a.Status >= 500)
                {
                    Rest(TextFault.Network, "nothing answers at " + _start.Base + " (is " + _start.Provider.Name + " running?)", TextServiceRules.Rest(TextFault.Network, ++tries, 0));
                    Rested();
                    continue;
                }
                if (a.Status == 401 || a.Status == 403)
                {
                    Stop(TextFault.Key, _start.Provider.Name + " refused the sign-in when asked for its models");
                    return false;
                }
                List<string> models = a.Status == 200 ? TextServiceRules.ModelList(a.Body) : new List<string>();
                if (models.Count == 0)
                {
                    Stop(TextFault.Model, _start.Provider.Name + " lists no model to use");
                    return false;
                }
                _model = models[0];
                lock (_lock)
                {
                    _restUntil = 0;
                    _lastFault = TextFault.None;
                }
                _state = TextServerState.Ready;
                Log("Your own AI: " + _start.Provider.Name + " lists " + models.Count + " model(s); " + _model + " is used.");
                return true;
            }
            return false;
        }

        // Waits out the rest, every request meanwhile missed at once.
        private void Rested()
        {
            while (!_stop && Resting)
            {
                _queue.MissAll(Why ?? "the service rests");
                _wake.WaitOne(WakeMs);
            }
        }

        private void Write(TextJob job)
        {
            double begun = TextClock.Now;
            double limit = TextServiceRules.Limit(job.Deadline, begun);
            if (limit <= 0.05)
            {
                _queue.Done(job, null, 0.0, "its time ran out before it started");
                return;
            }
            if (_stop)
            {
                _queue.Done(job, null, 0.0, "the service was switched off");   // never sent to a service the player has left
                return;
            }
            string secret = Secret(out TextFault fault, out string why);
            if (secret == null && _start.Provider.Access != TextAccess.Local)
            {
                _queue.Done(job, null, TextClock.Now - begun, why);
                After(fault, why, 0);
                return;
            }
            TextWebRequest request = TextServiceRules.Build(_start.Provider, _start.Base, _model, job.System ?? TextCard.System, job.Card, secret, _noReasoning, _noTemperature);
            if (_start.Provider.Api == TextApi.Responses)
            {
                var read = new TextStreamRead { OnBegun = () => job.Speaking = true };   // streamed: the first words tell the hub, the read ends at the last word
                request.Lines = read.Feed;
            }
            TextWebAnswer answer = _web.Send(request, (int)(Math.Max(0.05, TextServiceRules.Limit(job.Deadline, TextClock.Now)) * 1000.0));
            bool app = _start.Provider.Access == TextAccess.App;
            TextServiceResult result = TextServiceRules.Read(_start.Provider.Api, answer, app);
            double took = TextClock.Now - begun;
            if (app && result.Fault == TextFault.Key)
            {
                result.Fault = TextFault.Network;   // the key lapsed or was withdrawn: the App is asked for it again after a rest, not a stop
                lock (_appLock)
                {
                    _appKey = null;
                }
            }
            if (result.Fault == TextFault.None)
            {
                Interlocked.Increment(ref _written);
                lock (_lock)
                {
                    _took += took;
                    _inRow = 0;
                    _lastFault = TextFault.None;
                }
                _queue.Done(job, result.Words, took, null);
                Beat(secret);
                return;
            }
            Interlocked.Increment(ref _missed);
            string clean = Clean(result.Why);
            _queue.Done(job, null, took, clean);
            if (result.Fault == TextFault.BadRequest && Leave(result.Param))
            {
                return;   // sent without it from the next request on
            }
            After(result.Fault, clean, answer.RetryAfter);
        }

        // The key, or the ChatGPT sign-in's access token refreshed near its end; null with the fault when there is none.
        private string Secret(out TextFault fault, out string why)
        {
            fault = TextFault.None;
            why = null;
            switch (_start.Provider.Access)
            {
                case TextAccess.Key: return _start.Key;
                case TextAccess.SignIn: return TextSignIn.Access(_web, _start.Keys, _start.Auth, out fault, out why);
                case TextAccess.App: return AppKey(out fault, out why);
                default: return null;
            }
        }

        // Player2: the key the App holds for the signed-in player, asked on this PC when there is none yet or the last was refused, and kept
        // in memory only. Nothing running, or nobody signed in, is a rest like a lost connection, not a stop: the App may be started later.
        private string AppKey(out TextFault fault, out string why)
        {
            fault = TextFault.None;
            why = null;
            lock (_appLock)
            {
                if (_appKey != null)
                {
                    return _appKey;
                }
                TextWebAnswer a = _web.Send(TextPlayer2.Login(_start.ClientId), TextPlayer2.AppMs);
                TextAppState state = TextPlayer2.State(a, _start.ClientId);
                _appState = state;
                if (state != TextAppState.SignedIn)
                {
                    fault = TextFault.Network;
                    why = TextPlayer2.Why(state);
                    return null;
                }
                _appKey = TextPlayer2.Key(a);
                Log("Your own AI: the Player2 App signed in; its key is held in memory only.");
                return _appKey;
            }
        }

        // Player2 asks for its health call about once a minute while it is used, so the usage is counted for the game; sent after a
        // reply is handed over, so it never delays words, and its answer matters only when it says the key is refused.
        private void Beat(string key)
        {
            if (!_start.Provider.Heartbeat || key == null)
            {
                return;
            }
            double now = TextClock.Now;
            lock (_lock)
            {
                if (now < _beatAt)
                {
                    return;
                }
                _beatAt = now + TextPlayer2.BeatSeconds;
            }
            TextWebAnswer a = _web.Send(TextPlayer2.Health(key), TextPlayer2.PingMs);
            if (a != null && a.Status == 401)
            {
                lock (_appLock)
                {
                    _appKey = null;   // asked for again with the next request
                }
            }
        }

        // Leaves a refused field out from now on: true when it was one the mod may leave out.
        private bool Leave(string param)
        {
            if (param == null)
            {
                return false;
            }
            if (!_noTemperature && param.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _noTemperature = true;
                Log("Your own AI: " + _start.Provider.Name + " refused the temperature; it is left out from now on.");
                return true;
            }
            if (!_noReasoning && (param.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) >= 0 || param.IndexOf("effort", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                _noReasoning = true;
                Log("Your own AI: " + _start.Provider.Name + " refused the reasoning effort; it is left out from now on.");
                return true;
            }
            return false;
        }

        // A fault's consequence: stop, rest, or carry on.
        private void After(TextFault fault, string why, double asked)
        {
            if (TextServiceRules.Stops(fault))
            {
                Stop(fault, why);
                return;
            }
            int times;
            lock (_lock)
            {
                _inRow = fault == _lastFault ? _inRow + 1 : 1;
                _lastFault = fault;
                times = _inRow;
            }
            double rest = TextServiceRules.Rest(fault, times, asked);
            if (rest > 0)
            {
                Rest(fault, why, rest);
            }
        }

        private void Rest(TextFault fault, string why, double seconds)
        {
            if (seconds <= 0)
            {
                seconds = TextServiceRules.Rest(fault, 1, 0);
            }
            lock (_lock)
            {
                _restUntil = TextClock.Now + seconds;
                _lastFault = fault;
            }
            Why = why;
            Log("Your own AI: " + _start.Provider.Name + ": " + why + "; it rests " + seconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                + " s, and the pack or the written lines are used meanwhile.");
        }

        private void Stop(TextFault fault, string why)
        {
            _lastFault = fault;
            Why = Clean(why);
            _state = TextServerState.Failed;
            Log("Your own AI: " + _start.Provider.Name + " stopped: " + Why + ". The pack or the written lines are used until it is mended in the options.");
        }

        private string Clean(string line)
        {
            string[] tokens = _start.Keys != null ? _start.Keys.Secrets() : null;
            string clean = TextServiceRules.Clean(line, _start.Key, _start.ClientId, _appKey);
            return tokens != null ? TextServiceRules.Clean(clean, tokens) : clean;
        }

        private void Log(string line)
        {
            _log?.Invoke(Clean(line));
        }
    }
}
