using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>How urgent a text is: what the player is looking at first, the city's background last.</summary>
    public enum TextPriority
    {
        Now = 0,         // the player is looking at it: a spoken line, an open chat, the open Flare post (and the line guessed before a click)
        Soon = 1,        // a message due shortly: a text about to arrive, a reply under the player's post
        Background = 2   // the city's posts, the Ledger's next edition: written long before anyone reads them
    }

    /// <summary>Where a text's request stands.</summary>
    public enum TextJobState
    {
        Waiting,     // in the queue
        Running,     // being written
        Written,     // words came back (not yet checked: the checker runs when they are handed over)
        Missed,      // its time ran out (or the budget, or the model failed): the written line is used
        Dropped      // its owner no longer wants it
    }

    /// <summary>
    /// One request for words: the prompt, how urgent, the real time by which it must be shown, and what it is for. The
    /// owner keeps it and reads its state; the queue and the worker fill in the rest. Game-free.
    /// </summary>
    public sealed class TextJob
    {
        public int Id;
        public string Key;              // what it is for ("fp:123", "ph:dana:42"): the same key asked again finds this one
        public string Prompt;           // the ChatML text (TextCard.ChatMl)
        public TextPriority Priority;
        public double Asked;            // real seconds (TextQueue's clock) it was asked
        public double Deadline;         // ...by which it must be shown; past it the written line is used
        public bool Guess;              // asked before the player clicked (face to face): shown only if ready when needed
        public volatile TextJobState State;
        public string Raw;              // the model's words as they came back
        public double Took;             // seconds the model took
        public string Why;              // why it was missed, for the log
        public bool Ran;                // the worker took it (the model was asked): a miss after that is the model's or the machine's, never the budget's own holding back
        public object Owner;            // the asker's own record (what to do with the words), never read by the queue
    }

    /// <summary>
    /// The one queue every request goes through, with the owner's scheduling rules (16 Oct 2026):
    ///
    ///   order      the worker always takes the most urgent request that can still be written by its deadline (Now before
    ///              Soon before Background; within one, the earliest deadline); one at a time
    ///   deadlines  a request that can no longer be written in time (now plus the expected time past its deadline) is
    ///              missed at once: its owner shows the written line and it is dropped. Generate ahead: a message the
    ///              game schedules for later is asked at once with its arrival as the deadline
    ///   bursts     everything is queued; what cannot finish by its deadline is missed. A budget of requests a minute keeps
    ///              the model from ever being kept busy (<see cref="PerMinute"/>), and the background may use only part of
    ///              it (<see cref="BackgroundPerMinute"/>), so a burst of the city's posts never holds up a message
    ///   quiet      background requests wait while the game is not quiet (loading, fast forward or a time skip, a busy
    ///              scene, a slow frame); Now and Soon still run
    ///   guesses    a request asked before the player clicks (face to face) is Now, and shown only if it is written by
    ///              the time the line is needed
    ///   size       at most <see cref="Most"/> requests wait; past that the least urgent latest one is missed
    ///
    /// Thread-safe: the game's thread adds and drops, the worker takes. Every call holds the lock for a pass over at most
    /// <see cref="Most"/> requests. Game-free (EconomyCheck, TextsChecks.cs). COST: nothing per frame.
    /// </summary>
    public sealed class TextQueue
    {
        public const int Most = 48;                     // requests waiting at once
        public const int PerMinute = 12;                // requests started a minute at most: about a fifth of two threads on the 2B model
        public const int BackgroundPerMinute = 6;       // ...of which the background's
        public const double StartGuess = 1.5;           // seconds a reply is expected to take before any is measured (the 2B on two threads)
        public const double GuessWeight = 0.2;          // how much each measured time moves the expectation
        public const double ExpectedMost = 60.0;        // seconds: a speed test's figure past this is no figure (it failed), and the guess stays

        private readonly object _lock = new object();
        private readonly List<TextJob> _waiting = new List<TextJob>(Most);
        private readonly Queue<double> _started = new Queue<double>(PerMinute + 1);
        private readonly Queue<double> _startedBack = new Queue<double>(BackgroundPerMinute + 1);
        private readonly List<TextJob> _missed = new List<TextJob>(8);
        private int _nextId = 1;
        private double _expect = StartGuess;
        private int _perMinute = PerMinute, _backPerMinute = BackgroundPerMinute;

        /// <summary>Seconds a reply is expected to take now (a running mean of the measured ones).</summary>
        public double Expect
        {
            get { lock (_lock) { return _expect; } }
        }

        public int Count
        {
            get { lock (_lock) { return _waiting.Count; } }
        }

        /// <summary>
        /// The expected time set from what the speed test measured on this machine (TextSpeedResult.Estimate), so that the
        /// first spoken lines are not refused for the start's cautious guess (<see cref="StartGuess"/>: 1.5 s, past a
        /// spoken line's hold of 1.1 s, which only measured replies used to bring down). Measured replies move it as before.
        /// </summary>
        public void Expected(double seconds)
        {
            if (seconds > 0 && seconds < ExpectedMost)   // (NaN fails both; no figure at all keeps the guess)
            {
                lock (_lock)
                {
                    _expect = seconds;
                }
            }
        }

        /// <summary>The least urgent kind taken now (TextBudget lowers it while too many requests miss): Background takes every kind.</summary>
        public TextPriority Floor { get; set; } = TextPriority.Background;

        /// <summary>The budgets a minute (TextBudget's step): at least one in all, the background's none or more.</summary>
        public void SetBudget(int perMinute, int backgroundPerMinute)
        {
            lock (_lock)
            {
                _perMinute = Math.Max(1, perMinute);
                _backPerMinute = Math.Max(0, Math.Min(backgroundPerMinute, _perMinute));
            }
        }

        /// <summary>
        /// Queues a request. False when it cannot be written in time even now (its deadline is nearer than the expected
        /// time): the owner shows the written line at once. The same key waiting already is replaced (its owner is told
        /// through its state: Dropped).
        /// </summary>
        public bool Add(TextJob job, double now)
        {
            lock (_lock)
            {
                job.Asked = now;
                if (job.Deadline < now + _expect)
                {
                    job.State = TextJobState.Missed;
                    job.Why = "no time";
                    return false;
                }
                for (int i = _waiting.Count - 1; i >= 0; i--)
                {
                    if (_waiting[i].Key == job.Key)
                    {
                        _waiting[i].State = TextJobState.Dropped;
                        _waiting.RemoveAt(i);
                    }
                }
                if (_waiting.Count >= Most)
                {
                    int worst = Worst();
                    if (Before(job, _waiting[worst]))
                    {
                        Miss(_waiting[worst], "the queue was full");
                        _waiting.RemoveAt(worst);
                    }
                    else
                    {
                        job.State = TextJobState.Missed;
                        job.Why = "the queue was full";
                        return false;
                    }
                }
                job.Id = _nextId++;
                job.State = TextJobState.Waiting;
                _waiting.Add(job);
                return true;
            }
        }

        /// <summary>The owner no longer wants it (a scene changed, the option was switched off): taken out if it waits.</summary>
        public void Drop(TextJob job)
        {
            lock (_lock)
            {
                if (_waiting.Remove(job))
                {
                    job.State = TextJobState.Dropped;
                }
            }
        }

        /// <summary>Every waiting request dropped (the pack switched off, the game closing).</summary>
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _waiting.Count; i++)
                {
                    _waiting[i].State = TextJobState.Dropped;
                }
                _waiting.Clear();
            }
        }

        /// <summary>
        /// The worker's next request, or null: the most urgent that can still be written by its deadline, within the
        /// budget, the background only while <paramref name="quiet"/>. Requests that can no longer make their deadline
        /// are missed on the way (the owner sees their state).
        /// </summary>
        public TextJob Take(double now, bool quiet)
        {
            lock (_lock)
            {
                Forget(_started, now);
                Forget(_startedBack, now);
                TextJob best = null;
                int at = -1;
                for (int i = _waiting.Count - 1; i >= 0; i--)
                {
                    TextJob j = _waiting[i];
                    if (now + _expect > j.Deadline)
                    {
                        Miss(j, "its time ran out in the queue");
                        _waiting.RemoveAt(i);
                        at = at > i ? at - 1 : at;
                        continue;
                    }
                    if (j.Priority > Floor || (j.Priority == TextPriority.Background && (!quiet || _startedBack.Count >= _backPerMinute)))
                    {
                        continue;
                    }
                    if (best == null || Before(j, best))
                    {
                        best = j;
                        at = i;
                    }
                }
                if (best == null || _started.Count >= _perMinute)
                {
                    return null;
                }
                _waiting.RemoveAt(at);
                _started.Enqueue(now);
                if (best.Priority == TextPriority.Background)
                {
                    _startedBack.Enqueue(now);
                }
                best.State = TextJobState.Running;
                best.Ran = true;
                return best;
            }
        }

        /// <summary>The worker's report on a request it ran: the words, or why there are none.</summary>
        public void Done(TextJob job, string raw, double took, string why)
        {
            lock (_lock)
            {
                job.Took = took;
                if (raw != null)
                {
                    _expect += (took - _expect) * GuessWeight;
                    job.Raw = raw;
                    job.State = job.State == TextJobState.Dropped ? TextJobState.Dropped : TextJobState.Written;
                }
                else
                {
                    Miss(job, why ?? "the model gave nothing");
                }
            }
        }

        /// <summary>Requests missed since the last call (their owners show the written line), handed over once.</summary>
        public void TakeMissed(List<TextJob> into)
        {
            lock (_lock)
            {
                into.AddRange(_missed);
                _missed.Clear();
            }
        }

        /// <summary>Seconds until the soonest waiting deadline (for the owner's next look); a large number with none.</summary>
        public double Soonest(double now)
        {
            lock (_lock)
            {
                double soonest = double.MaxValue;
                for (int i = 0; i < _waiting.Count; i++)
                {
                    soonest = Math.Min(soonest, _waiting[i].Deadline - now);
                }
                return soonest;
            }
        }

        private void Miss(TextJob job, string why)
        {
            if (job.State == TextJobState.Dropped)
            {
                return;
            }
            job.State = TextJobState.Missed;
            job.Why = why;
            _missed.Add(job);
        }

        // The least urgent, latest waiting request.
        private int Worst()
        {
            int worst = 0;
            for (int i = 1; i < _waiting.Count; i++)
            {
                if (Before(_waiting[worst], _waiting[i]))
                {
                    worst = i;
                }
            }
            return worst;
        }

        private static bool Before(TextJob a, TextJob b)
        {
            if (a.Priority != b.Priority)
            {
                return a.Priority < b.Priority;
            }
            if (a.Guess != b.Guess)
            {
                return !a.Guess;   // a request the player is waiting for before a guess at one
            }
            return a.Deadline < b.Deadline;
        }

        private static void Forget(Queue<double> started, double now)
        {
            while (started.Count > 0 && now - started.Peek() >= 60.0)
            {
                started.Dequeue();
            }
        }
    }
}
