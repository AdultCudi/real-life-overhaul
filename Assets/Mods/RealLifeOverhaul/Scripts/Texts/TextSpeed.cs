using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Texts
{
    /// <summary>What the speed test measured on this machine, for this pack, device and number of threads.</summary>
    public sealed class TextSpeedResult
    {
        public string For;                   // pack name and version, device and threads: a change asks for a new test
        public double PromptMsPerToken;      // reading the card, a token
        public double TokensPerSecond;       // writing the reply
        public double HeadMs;                // reading the fixed head once (what the prompt cache saves on every request)
        public bool Capped;                  // the test ran past its time cap: the machine is slow

        /// <summary>Seconds an ordinary reply is expected to take (a card of <see cref="TextSpeed.CardTokens"/> tokens after the cached head, a reply of <see cref="TextSpeed.ReplyTokens"/>).</summary>
        public double Estimate => TextSpeed.Estimate(PromptMsPerToken, TokensPerSecond);

        public string Line()
        {
            return For + "|" + PromptMsPerToken.ToString("0.###", CultureInfo.InvariantCulture) + "|" + TokensPerSecond.ToString("0.###", CultureInfo.InvariantCulture)
                   + "|" + HeadMs.ToString("0.#", CultureInfo.InvariantCulture) + "|" + (Capped ? "1" : "0");
        }

        public static TextSpeedResult Read(string line)
        {
            string[] f = line != null ? line.Split('|') : null;
            if (f == null || f.Length < 5 || !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double p)
                || !double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
                || !double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
            {
                return null;
            }
            return new TextSpeedResult { For = f[0], PromptMsPerToken = p, TokensPerSecond = t, HeadMs = h, Capped = f[4] == "1" };
        }
    }

    /// <summary>
    /// How fast this machine is, and what follows from it (so that weaker processors are served too):
    ///
    ///   threads    2 on four cores or fewer, 3 on six, 4 on eight or more (real cores: logical ones halved where a core
    ///              runs two); the option overrides it; never so many that the game keeps fewer than <see cref="GameKeeps"/>.
    ///              On a GPU at most <see cref="GpuThreads"/>: the model's work is the GPU's, the processor only hands it over
    ///              and picks each word (measured on an RTX 5070 Ti beside sixteen busy threads: 1, 2 and 4 threads alike)
    ///   too slow   a reply estimated at more than <see cref="FullSlow"/> s on the full pack: a notice, once, suggesting
    ///              the Lite pack. Any pack past <see cref="AheadSlow"/> s writes only what is asked well ahead
    ///              (<see cref="AheadSeconds"/> or more), never a spoken line or what the player is waiting for
    ///
    /// Game-free (EconomyCheck, TextsChecks.cs). COST: arithmetic.
    /// </summary>
    public static class TextSpeed
    {
        public const int GameKeeps = 2;          // real cores always left to the game
        public const double FullSlow = 4.0;      // seconds a reply of the full pack may take before the Lite pack is suggested
        public const double AheadSlow = 3.0;     // ...past this, any pack writes only what is asked ahead
        public const double AheadSeconds = 30.0; // seconds of notice that count as asked ahead
        public const int CardTokens = 90;        // tokens of an ordinary card after the cached head
        public const int ReplyTokens = 25;       // tokens of an ordinary reply
        public const double TestSeconds = 3.0;   // the speed test's time cap
        public const int GpuThreads = 2;         // threads at most on a GPU: more only wait for it

        /// <summary>Threads to run on: the option (2 to 4) or, with the option at 0, by the real cores; capped to leave the game its cores; at least one.</summary>
        public static int Threads(int realCores, int option)
        {
            int wanted = option >= 2 ? Math.Min(option, 4) : realCores >= 8 ? 4 : realCores >= 6 ? 3 : 2;
            return Math.Max(1, Math.Min(wanted, realCores - GameKeeps));
        }

        /// <summary>Threads to run on for a device: as <see cref="Threads"/> on the processor, at most <see cref="GpuThreads"/> on a GPU.</summary>
        public static int Threads(int realCores, int option, bool gpu)
        {
            int threads = Threads(realCores, option);
            return gpu ? Math.Min(threads, GpuThreads) : threads;
        }

        /// <summary>
        /// What a speed measured is for: the pack, the device and the threads, and on a GPU its priority (normal since the
        /// speed fix: a speed measured below normal priority, while the game starved the server, is never used again).
        /// </summary>
        public static string For(string pack, string device, int threads)
        {
            return pack + ";" + (device ?? "cpu") + ";" + threads + (device != null ? ";normal" : string.Empty);
        }

        /// <summary>Real cores from the logical ones when the system cannot say: halved where a core runs two threads.</summary>
        public static int RealCores(int logical, bool twoPerCore)
        {
            return Math.Max(1, twoPerCore ? logical / 2 : logical);
        }

        /// <summary>Seconds an ordinary reply takes at these speeds.</summary>
        public static double Estimate(double promptMsPerToken, double tokensPerSecond)
        {
            if (double.IsNaN(promptMsPerToken) || double.IsNaN(tokensPerSecond) || tokensPerSecond <= 0.0)
            {
                return double.MaxValue;
            }
            return CardTokens * promptMsPerToken / 1000.0 + ReplyTokens / tokensPerSecond;
        }

        /// <summary>True when the Lite pack should be suggested: the full pack (rank 2 and up) is too slow here.</summary>
        public static bool SuggestLite(int rank, double estimate) => rank >= 2 && estimate > FullSlow;

        /// <summary>True when only what is asked ahead may be written (spoken lines and what the player waits for take the written line).</summary>
        public static bool AheadOnly(double estimate) => estimate > AheadSlow;

        /// <summary>Whether a request may go to the model at this machine's speed.</summary>
        public static bool Allows(bool aheadOnly, TextPriority priority, bool guess, double seconds)
        {
            return !aheadOnly || (priority != TextPriority.Now && !guess && seconds >= AheadSeconds);
        }
    }

    /// <summary>
    /// The budget that adapts: the share of requests that missed their deadline over the last <see cref="Window"/> is
    /// watched; past <see cref="MissUp"/> per cent the budget goes down a step (fewer requests a minute, and the least
    /// urgent kind left out), and after a window under <see cref="MissDown"/> per cent it comes back up a step.
    ///
    ///   step 0   12 a minute, 6 of them the background's, every priority
    ///   step 1   8 a minute, 3 the background's
    ///   step 2   5 a minute, no background
    ///   step 3   3 a minute, only what the player is looking at
    ///
    /// Game-free (EconomyCheck, TextsChecks.cs). COST: a few comparisons a request.
    /// </summary>
    public sealed class TextBudget
    {
        public const int Window = 20;            // requests looked back over
        public const int Least = 10;             // ...at least this many before a step down
        public const int MissUp = 20;            // per cent missed: a step down
        public const int MissDown = 10;          // per cent missed over a whole window: a step back up
        public static readonly int[] PerMinute = { TextQueue.PerMinute, 8, 5, 3 };
        public static readonly int[] Background = { TextQueue.BackgroundPerMinute, 3, 0, 0 };
        public static readonly TextPriority[] Floor = { TextPriority.Background, TextPriority.Background, TextPriority.Soon, TextPriority.Now };

        private readonly Queue<bool> _last = new Queue<bool>(Window + 1);
        private int _missed;

        public int Step { get; private set; }

        public int Count => _last.Count;

        /// <summary>Per cent of the window that missed.</summary>
        public int MissPercent => _last.Count == 0 ? 0 : _missed * 100 / _last.Count;

        /// <summary>Per cent that made it: the status row's hit rate.</summary>
        public int HitPercent => _last.Count == 0 ? 100 : 100 - MissPercent;

        /// <summary>A request ended: in time (words, checked or not) or missed (its deadline, a time-out). True if the step changed.</summary>
        public bool Ended(bool missed)
        {
            _last.Enqueue(missed);
            _missed += missed ? 1 : 0;
            if (_last.Count > Window && _last.Dequeue())
            {
                _missed--;
            }
            if (_last.Count >= Least && MissPercent > MissUp && Step < PerMinute.Length - 1)
            {
                Step++;
                Reset();
                return true;
            }
            if (_last.Count >= Window && MissPercent < MissDown && Step > 0)
            {
                Step--;
                Reset();
                return true;
            }
            return false;
        }

        /// <summary>Applies the step to the queue.</summary>
        public void Apply(TextQueue queue)
        {
            queue.SetBudget(PerMinute[Step], Background[Step]);
            queue.Floor = Floor[Step];
        }

        private void Reset()
        {
            _last.Clear();
            _missed = 0;
        }
    }
}
