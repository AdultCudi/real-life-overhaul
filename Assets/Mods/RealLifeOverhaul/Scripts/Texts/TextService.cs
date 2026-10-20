using System;
using System.Collections.Generic;
using System.Threading;

namespace RealLifeOverhaul.Texts
{
    /// <summary>A runner that sends cards to an AI service (TextService; the checks put a pretend one in).</summary>
    public interface ITextServiceRunner : ITextRunner
    {
        /// <summary>True while the service rests after a fault (no connection, too many requests): nothing is sent meanwhile.</summary>
        bool Resting { get; }
    }

    /// <summary>What a service runner is started with.</summary>
    public sealed class TextServiceStart
    {
        public TextProviderInfo Provider;
        public string Base;               // the address asked (TextProviders.Base)
        public string Model;              // the model named by the player, or null for the provider's own, or the first listed
        public string Key;                // an API key (null for a sign-in or a program on this PC)
        public TextKeys Keys;             // where the ChatGPT sign-in's tokens are kept (a sign-in only)
        public string Auth = TextProviders.ChatGptAuth;   // the sign-in's address (a sign-in only; the checks point it at a pretend one)
        public int Workers = TextService.Workers;
    }

    /// <summary>
    /// Your own AI's runner: the cards RLO Texts would give its local pack sent to the AI service the player chose, through
    /// one queue of its own (TextQueue: the same order, deadlines and budget a minute), by at most <see cref="Workers"/>
    /// worker threads, each with one request at a time, so never more than two in flight. Each request's time is the
    /// time left to its deadline, at most TextServiceRules.TimeOut; its words go back to the queue, and the hub checks
    /// them with TextCheck as it does the pack's. A streamed reply (the ChatGPT plan's) marks its job the moment its first
    /// words come (TextJob.Speaking) and is read only until its last word (TextStreamRead). After a fault it rests as the
    /// rules say (no connection, too many requests, the service's own fault), and every request waiting meanwhile is
    /// missed at once, so the hub falls back
    /// to the pack or the written line without waiting; a refused key, sign-in or model stops it (Failed) until the
    /// player mends it. A field the service refused (a temperature, a reasoning effort) is left out from then on. A
    /// program on this PC (Ollama, LM Studio) with no model named is asked for its list first, and its first model used.
    /// Keys and tokens are only ever in the request's headers; every log line goes through TextServiceRules.Clean.
    /// Game-free (EconomyCheck, TextsServiceChecks.cs, with a pretend web and a pretend local service).
    /// COST: nothing on the game's thread; worker threads that sleep until there is a request.
    /// </summary>
    public sealed class TextService : IDisposable, ITextServiceRunner
    {
        public const int Workers = 2;               // requests in flight at most
        public const int WakeMs = 250;              // a worker's longest sleep between two looks at the queue
        public const int PerMinute = 20;            // requests started a minute at most (the free tiers allow 10 to 30)
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
        private double _took;

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

        /// <summary>Why it failed or rests, for the status and the log (no key in it).</summary>
        public string Why { get; private set; }

        /// <summary>The fault that stopped it or that it rests after; None while all is well.</summary>
        public TextFault Fault => _lastFault;

        /// <summary>The model asked (the player's, the provider's own, or the first the service listed); null until known.</summary>
        public string Model => _model;

        public TextProviderInfo Provider => _start.Provider;

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
            if (string.IsNullOrEmpty(_start.Base))
            {
                Stop(TextFault.Model, "the address for " + p.Name + " is not one on this PC");
                return;
            }
            _stop = false;
            _state = _model == null ? TextServerState.Loading : TextServerState.Ready;
            for (int i = 0; i < Math.Max(1, _start.Workers); i++)
            {
                var t = new Thread(Run) { IsBackground = true, Name = "RLO Texts service " + (i + 1), Priority = ThreadPriority.BelowNormal };   // (each mostly waits on the network)
                _threads.Add(t);
                t.Start(i);
            }
        }

        /// <summary>
        /// Stops it taking anything more, at once and without waiting (the game's thread, before a runner of other options
        /// takes the same queue): a request already under way ends by its own time, and nothing new is sent.
        /// </summary>
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
                _threads[i].Join(TextWeb.GraceMs);   // (a request under way ends by its own time; the thread is a background one)
            }
            _threads.Clear();   // (what waits is the hub's to hand over: it clears it as the service stops, and a runner after this one may share the queue)
            if (_state != TextServerState.Failed)
            {
                _state = TextServerState.Off;
            }
        }

        // ---------------------------------------------------------------- the workers

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
                        _queue.MissAll(Why ?? "the service rests");   // (the hub falls back at once rather than wait for nothing)
                        _wake.WaitOne(WakeMs);
                        continue;
                    }
                    TextJob job = _queue.Take(TextClock.Now, _quiet);
                    if (job == null)
                    {
                        _wake.WaitOne(WakeMs);
                        continue;
                    }
                    _wake.Set();   // (the other worker looks too: a second request may be waiting)
                    Write(job);
                }
            }
            catch (Exception e)
            {
                Stop(TextFault.Busy, "the worker: " + e.GetType().Name);
                _queue.MissAll("the service stopped");
            }
        }

        // No model named: the service's first listed one (a program on this PC, the ChatGPT plan), asked again after a rest while
        // nothing answers; Failed with why when it refuses or lists none. Ready only once a model is known.
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

        // The rest waited out, every request meanwhile missed at once.
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
                _queue.Done(job, null, 0.0, "the service was switched off");   // (taken as it stopped: never sent to a service the player has left)
                return;
            }
            string secret = Secret(out TextFault fault, out string why);
            if (secret == null && _start.Provider.Access != TextAccess.Local)
            {
                _queue.Done(job, null, TextClock.Now - begun, why);
                After(fault, why, 0);
                return;
            }
            TextWebRequest request = TextServiceRules.Build(_start.Provider, _start.Base, _model, TextCard.System, job.Card, secret, _noReasoning, _noTemperature);
            if (_start.Provider.Api == TextApi.Responses)
            {
                var read = new TextStreamRead { OnBegun = () => job.Speaking = true };   // (streamed: the first words tell the hub, and the read ends at the last word)
                request.Lines = read.Feed;
            }
            TextWebAnswer answer = _web.Send(request, (int)(Math.Max(0.05, TextServiceRules.Limit(job.Deadline, TextClock.Now)) * 1000.0));
            TextServiceResult result = TextServiceRules.Read(_start.Provider.Api, answer);
            double took = TextClock.Now - begun;
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
                return;
            }
            Interlocked.Increment(ref _missed);
            string clean = Clean(result.Why);
            _queue.Done(job, null, took, clean);
            if (result.Fault == TextFault.BadRequest && Leave(result.Param))
            {
                return;   // (sent again without it from the next request on)
            }
            After(result.Fault, clean, answer.RetryAfter);
        }

        // The key, or the ChatGPT sign-in's access token (refreshed when it is near its end); null with the fault when there is none.
        private string Secret(out TextFault fault, out string why)
        {
            fault = TextFault.None;
            why = null;
            switch (_start.Provider.Access)
            {
                case TextAccess.Key: return _start.Key;
                case TextAccess.SignIn: return TextSignIn.Access(_web, _start.Keys, _start.Auth, out fault, out why);
                default: return null;
            }
        }

        // A refused field left out from now on: true when it was one the mod sends and may leave out.
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

        // What a fault means next: stop, rest, or carry on.
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
            string clean = TextServiceRules.Clean(line, _start.Key);
            return tokens != null ? TextServiceRules.Clean(clean, tokens) : clean;
        }

        private void Log(string line)
        {
            _log?.Invoke(Clean(line));
        }
    }
}
