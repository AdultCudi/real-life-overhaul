using System;
using System.Diagnostics;
using System.Text;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.BizPace
{
    /// <summary>
    /// What the mod's own handlers add to BizMan's business view, counted by step (<see cref="BizStep"/>): the open, the switch to another
    /// business and the change of tab. A handler on those paths opens a scope on its work, <c>using var timed = BizPaceWatch.Time(slot);</c>
    /// (a HotSpots scope as well, so the session record names it). While the watch is armed (Developer diagnostics on, or the test in
    /// Options > Tests walking) the game's own move into a step is marked (<see cref="BizPaceGame"/>'s marks on the view and on each tab's
    /// page) and the scopes of that frame and the <see cref="BizPaceRules.Window"/> after it are summed into one episode, with the handler that
    /// took most; a scope that ran in the frame of the mark before the mark did is taken in. Work that runs outside an episode, a second
    /// after (the registration change handlers' consequences on their timers), is counted apart (<see cref="AfterMs"/>). Each episode
    /// is one Verbose line, the minute's session line ends with the minute's book (<see cref="MinuteLine"/>), and the test reads the walk's.
    /// COST: as shipped, one flag test a scope and nothing else (no component is on BizMan); armed, a clock read at each end of a scope, a
    /// few array adds, one frame comparison a frame. Nothing is allocated except a Verbose line.
    /// </summary>
    public static class BizPaceWatch
    {
        public static IModLogger Logger;

        /// <summary>The business the view shows now, in words, for a line; set by <see cref="BizPaceGame"/>.</summary>
        public static Func<string> Where;

        /// <summary>The walk of the test while it runs: every episode closed goes into this book as well as the minute's.</summary>
        public static BizPaceBook Walk;

        private static int _armers;
        private static readonly BizPaceBook Minute = new BizPaceBook();

        // The open episode: the time of the mod's scopes by slot, which slots ran, and the sum.
        private static readonly long[] Ticks = new long[HotSpots.Most];
        private static readonly int[] Touched = new int[HotSpots.Most];
        private static int _touched;
        private static long _sum;
        private static bool _open;
        private static BizStep _step;
        private static int _began;
        private static string _where;

        // Scopes that ran outside any episode in the frame they ended in: a mark in the same frame takes them in (the game's switch-on may
        // run a handler of the mod's before the mark on the same object does); after that frame they are counted apart.
        private const int Loose = 24;
        private static readonly int[] LooseSlot = new int[Loose];
        private static readonly long[] LooseTicks = new long[Loose];
        private static int _loose;
        private static int _looseFrame = int.MinValue;

        // The work outside episodes since it was last cleared.
        private static readonly long[] After = new long[HotSpots.Most];
        private static readonly int[] AfterTouched = new int[HotSpots.Most];
        private static int _afterTouched;
        private static long _afterSum;

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>True while handlers are being timed into episodes.</summary>
        public static bool Armed => _armers > 0;

        /// <summary>Asked for by the session record and by the test: timing starts with the first and ends with the last to let go.</summary>
        public static void Arm(bool on)
        {
            bool was = _armers > 0;
            _armers = Math.Max(0, _armers + (on ? 1 : -1));
            bool now = _armers > 0;
            if (was == now)
            {
                return;
            }
            Reset();
            if (now)
            {
                BizPaceGame.Keep(true);
            }
            else
            {
                BizPaceGame.Remove();
            }
        }

        /// <summary>A handler's scope: the same as a HotSpots scope, and while armed also counted into the episode.</summary>
        public static Scope Time(int slot)
        {
            HotSpots.Scope hot = HotSpots.Time(slot);
            return new Scope(hot, slot, _armers > 0 ? Math.Max(1L, Stopwatch.GetTimestamp()) : 0L);
        }

        /// <summary>What <see cref="Time"/> returns, closed at the end of its using block; a struct, so nothing is allocated.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly HotSpots.Scope _hot;
            private readonly int _slot;
            private readonly long _began;

            internal Scope(HotSpots.Scope hot, int slot, long began)
            {
                _hot = hot;
                _slot = slot;
                _began = began;
            }

            public void Dispose()
            {
                _hot.Dispose();
                if (_began != 0L)
                {
                    Add(_slot, Math.Max(1L, Stopwatch.GetTimestamp() - _began));
                }
            }
        }

        private static void Add(int slot, long ticks)
        {
            if (slot < 0 || slot >= HotSpots.Most)
            {
                return;
            }
            if (_open)
            {
                Count(slot, ticks);
                return;
            }
            int frame = UnityEngine.Time.frameCount;
            if (frame != _looseFrame)
            {
                FoldLoose();
                _looseFrame = frame;
            }
            if (_loose < Loose)
            {
                LooseSlot[_loose] = slot;
                LooseTicks[_loose++] = ticks;
            }
            else
            {
                Apart(slot, ticks);
            }
        }

        private static void Count(int slot, long ticks)
        {
            if (Ticks[slot] == 0L)
            {
                Touched[_touched++] = slot;
            }
            Ticks[slot] += ticks;
            _sum += ticks;
        }

        private static void Apart(int slot, long ticks)
        {
            if (After[slot] == 0L)
            {
                AfterTouched[_afterTouched++] = slot;
            }
            After[slot] += ticks;
            _afterSum += ticks;
        }

        // The loose scopes of an earlier frame are work outside any episode.
        private static void FoldLoose()
        {
            for (int i = 0; i < _loose; i++)
            {
                Apart(LooseSlot[i], LooseTicks[i]);
            }
            _loose = 0;
        }

        /// <summary>
        /// The game's own move into a step: an episode begins in this frame (taking in the scopes that ran in it already), or the one
        /// collecting becomes the stronger of the two.
        /// </summary>
        public static void Mark(BizStep step)
        {
            if (_armers <= 0)
            {
                return;
            }
            int frame = UnityEngine.Time.frameCount;
            if (_open)
            {
                if (BizPaceRules.Collects(_began, frame))
                {
                    _step = BizPaceRules.Stronger(_step, step);
                    return;
                }
                Close();
            }
            _open = true;
            _step = step;
            _began = frame;
            _where = Meta.Dev.Verbose && Where != null ? Where() : null;
            if (_looseFrame == frame)
            {
                for (int i = 0; i < _loose; i++)
                {
                    Count(LooseSlot[i], LooseTicks[i]);
                }
                _loose = 0;
            }
        }

        /// <summary>Once a frame while armed (the session record's frame, the test's loop): closes an episode whose window is over.</summary>
        public static void Frame(int frame)
        {
            if (_armers <= 0)
            {
                return;
            }
            if (_open && !BizPaceRules.Collects(_began, frame))
            {
                Close();
            }
            if (_loose > 0 && frame != _looseFrame)
            {
                FoldLoose();
            }
            if (!_open)
            {
                BizPaceGame.Keep(false);
            }
        }

        private static void Close()
        {
            _open = false;
            double ms = Ms(_sum);
            int best = -1;
            long bestTicks = 0L;
            for (int i = 0; i < _touched; i++)
            {
                int slot = Touched[i];
                if (Ticks[slot] > bestTicks)
                {
                    bestTicks = Ticks[slot];
                    best = slot;
                }
            }
            string handler = best >= 0 ? HotSpots.Name(best) : null;
            double handlerMs = Ms(bestTicks);
            string parts = Meta.Dev.Verbose && Logger != null ? Parts() : null;
            for (int i = 0; i < _touched; i++)
            {
                Ticks[Touched[i]] = 0L;
            }
            _touched = 0;
            _sum = 0L;
            Minute.Add(_step, ms, handler, handlerMs);
            Walk?.Add(_step, ms, handler, handlerMs);
            if (parts != null)
            {
                Logger.Info("BizMan check (" + BizPaceRules.Word(_step, false) + (_where != null ? " at " + _where : string.Empty) + "): the mod's handlers took "
                            + BizPaceRules.Ms(ms) + " ms" + parts + ", wanted under " + BizPaceRules.Ms(BizPaceRules.Budget(_step)) + " ms: "
                            + (BizPaceRules.Within(_step, ms) ? "PASS." : "OVER."));
            }
        }

        // The slots that took part, most first, for the Verbose line: " (Interiors: InteriorTab.Fix 0.20 ms, ...)"; empty when none ran.
        private static string Parts()
        {
            if (_touched == 0)
            {
                return " (nothing of the mod's ran)";
            }
            var text = new StringBuilder(96);
            bool first = true;
            for (int k = 0; k < 3; k++)
            {
                int best = -1;
                long bestTicks = 0L;
                for (int i = 0; i < _touched; i++)
                {
                    int slot = Touched[i];
                    long ticks = Ticks[slot];
                    bool after = k == 0 || ticks < _rankBound || (ticks == _rankBound && slot > _rankSlot);
                    if (after && ticks > bestTicks)
                    {
                        bestTicks = ticks;
                        best = slot;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                _rankBound = bestTicks;
                _rankSlot = best;
                text.Append(first ? " (" : ", ").Append(HotSpots.Name(best)).Append(' ').Append(BizPaceRules.Ms(Ms(bestTicks))).Append(" ms");
                first = false;
            }
            return text.Append(')').ToString();
        }

        private static long _rankBound;
        private static int _rankSlot;

        /// <summary>The minute's book added to the session line, then cleared.</summary>
        public static void MinuteLine(StringBuilder into)
        {
            Minute.Append(into);
            Minute.Clear();
        }

        /// <summary>A walk begins: its book is empty and the work outside episodes starts again from nothing.</summary>
        public static void BeginWalk(BizPaceBook book)
        {
            ClearAfter();
            Walk = book;
        }

        /// <summary>Milliseconds the mod's BizMan handlers spent outside any episode since the walk began, and the one that spent most.</summary>
        public static double AfterMs(out string handler, out double handlerMs)
        {
            FoldLoose();
            int best = -1;
            long bestTicks = 0L;
            for (int i = 0; i < _afterTouched; i++)
            {
                int slot = AfterTouched[i];
                if (After[slot] > bestTicks)
                {
                    bestTicks = After[slot];
                    best = slot;
                }
            }
            handler = best >= 0 ? HotSpots.Name(best) : null;
            handlerMs = Ms(bestTicks);
            return Ms(_afterSum);
        }

        private static void ClearAfter()
        {
            FoldLoose();
            for (int i = 0; i < _afterTouched; i++)
            {
                After[AfterTouched[i]] = 0L;
            }
            _afterTouched = 0;
            _afterSum = 0L;
        }

        private static void Reset()
        {
            _open = false;
            for (int i = 0; i < _touched; i++)
            {
                Ticks[Touched[i]] = 0L;
            }
            _touched = 0;
            _sum = 0L;
            _loose = 0;
            _looseFrame = int.MinValue;
            ClearAfter();
            Minute.Clear();
            Walk = null;
        }
    }
}
