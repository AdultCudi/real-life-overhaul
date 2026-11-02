using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the mod's own share of a frame goes, by feature and method, for the session record (Diagnostics/SessionWatch).
    /// Every Update, LateUpdate and FixedUpdate opens a scope on its first line:
    /// <c>using var hotSpot = HotSpots.Time(HotUpdate);</c>, with
    /// <c>private static readonly int HotUpdate = HotSpots.Slot("Transport: TransitDirector.Update");</c>
    /// Coroutines and handlers Core/Guard wraps are timed by the guard itself ("Coroutine: Class.Method", "Event: ...").
    /// A scope inside another counts under both names, once in the total. The frame before is kept whole
    /// (<see cref="FrameMs"/>); the reader adds frames of play into a stretch (<see cref="Keep"/>) read once a minute.
    /// One frame in <see cref="SampleEvery"/> also samples the managed heap round each scope (every frame where the
    /// runtime counts each thread's allocations). The game's own work the mod calls (a button of the game's pressed for
    /// the RLO interface, so every listener runs as a click would run it) is opened with <see cref="Game"/>: while it is
    /// the innermost scope the clock and the heap's growth are the game's, kept out of every scope round it and out of
    /// the frame's total and counted apart (<see cref="FrameGameMs"/>, <see cref="GameMs"/>); a scope of the mod's that
    /// the game runs inside it (a handler of its events) is the mod's again. Game-free (the frame count and heap are
    /// handed in), checked by NavCheck.
    /// COST: one flag test a scope while the record is off, as the mod ships; while on, about five microseconds a frame;
    /// nothing allocated after the arrays.
    /// </summary>
    public static class HotSpots
    {
        public const int Most = 768;       // slots: names that can be told apart, and the last for any more ("others")
        public const int Others = Most - 1;   // the slot every name past the first Most - 1 shares; no name of its own is put there
        public const int AllocEvery = 8;   // one frame in this many is a sample of the heap, where only its size can be read

        /// <summary>One frame in this many is a sample of the heap: <see cref="AllocEvery"/>, or 1 where the reads are exact.</summary>
        public static int SampleEvery = AllocEvery;

        /// <summary>True while the session record runs: only then is anything measured.</summary>
        public static bool On;

        /// <summary>The engine's frame count, handed in by Diagnostics/SessionWatch.</summary>
        public static Func<int> FrameSource;

        /// <summary>Bytes of the managed heap in use, handed in by Diagnostics/SessionWatch.</summary>
        public static Func<long> HeapSource;

        private static readonly string[] Names = new string[Most];
        private static readonly Dictionary<string, int> ByName = new Dictionary<string, int>(Most, StringComparer.Ordinal);
        private static int _named;

        // The frame being run.
        private static readonly long[] Now = new long[Most];
        private static readonly long[] NowBytes = new long[Most];
        private static readonly int[] NowTouched = new int[Most];
        private static int _nowCount;
        private static long _nowTotal, _nowBytesTotal;
        private static int _frame = int.MinValue;
        private static bool _sampling;
        private static int _depth;      // scopes open, the mod's and the game's
        private static int _modDepth;   // of them the mod's: one that closes with none of the mod's round it adds to the total

        // The game's own work (Game): which open scopes are the game's, and its clock since the record began.
        private const int Deep = 64;    // scopes inside each other the carve follows; a game scope deeper is not carved
        private static readonly bool[] GameAt = new bool[Deep];
        private static bool _inGame;    // the innermost open scope is the game's: the clock is the game's
        private static long _gameTicks, _gameBytes;   // the game's own work handed to it, in all (they only grow)
        private static long _insideGameTicks;         // the mod's own scopes the game ran inside that work, in all
        private static long _gameSince, _gameHeapSince;   // where the game's present stretch of the clock and of the heap began
        private static long _frameGameAt;   // _gameTicks as the frame began
        private static long _lastGame;      // the frame before: the game's own work in it
        private static long _sumGame, _worstGame;   // the stretch's

        // The frame before it, complete.
        private static readonly long[] Last = new long[Most];
        private static readonly long[] LastBytes = new long[Most];
        private static readonly int[] LastTouched = new int[Most];
        private static int _lastCount;
        private static long _lastTotal, _lastBytesTotal;
        private static int _lastFrame = int.MinValue;
        private static bool _lastSampled;

        // The stretch the reader keeps: a minute of play.
        private static readonly long[] Sum = new long[Most];
        private static readonly long[] Worst = new long[Most];
        private static readonly long[] Bytes = new long[Most];
        private static long _sumTotal, _worstTotal, _bytesTotal;
        private static int _worstSlot = -1;     // the scope that took the most in the stretch's worst frame
        private static long _worstSlotTicks;
        private static int _frames, _sampled;

        /// <summary>
        /// The number for a name ("Feature: Class.Method"), the same for the same name, so a component made again with
        /// every city keeps its own. Kept in a static field.
        /// </summary>
        public static int Slot(string name)
        {
            lock (ByName)
            {
                if (name == null)
                {
                    name = "unnamed";
                }
                if (ByName.TryGetValue(name, out int slot))
                {
                    return slot;
                }
                if (_named >= Others)
                {
                    Names[Others] = "others";   // a slot of its own: the last real name keeps its number and its time
                    return Others;
                }
                slot = _named++;
                Names[slot] = name;
                ByName[name] = slot;
                return slot;
            }
        }

        public static string Name(int slot) => slot >= 0 && slot < Most && Names[slot] != null ? Names[slot] : "unnamed";

        /// <summary>
        /// "Class.Method" as the code is written, out of the compiler's classes for lambdas and coroutines
        /// ("&lt;Tick&gt;d__12") and its methods ("&lt;Awake&gt;b__12_0"); a hand-written IEnumerator is named by its MoveNext.
        /// Asked once per coroutine or handler method, never per frame.
        /// </summary>
        public static string Written(Type type, string method)
        {
            string inside = null;
            while (type != null && type.Name.IndexOf('<') >= 0)
            {
                inside = inside ?? Between(type.Name);
                if (type.DeclaringType == null)
                {
                    break;
                }
                type = type.DeclaringType;
            }
            string name = method != null && method.IndexOf('<') >= 0 ? Between(method) : method;
            return (type != null ? type.Name : "a handler") + "." + (name ?? inside ?? "MoveNext");
        }

        // The name between the compiler's angle brackets ("Tick" of "<Tick>d__12"); null when there is none ("<>c").
        private static string Between(string compiled)
        {
            int open = compiled.IndexOf('<');
            int close = compiled.IndexOf('>', open + 1);
            return open >= 0 && close > open + 1 ? compiled.Substring(open + 1, close - open - 1) : null;
        }

        /// <summary>What <see cref="Time"/> returns, closed at the end of its using block.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly int _slot;
            private readonly long _began;
            private readonly long _heap;
            private readonly long _gameAt, _gameBytesAt;

            internal Scope(int slot, long began, long heap, long gameAt, long gameBytesAt)
            {
                _slot = slot;
                _began = began;
                _heap = heap;
                _gameAt = gameAt;
                _gameBytesAt = gameBytesAt;
            }

            public void Dispose()
            {
                if (_began != 0L)
                {
                    End(_slot, _began, _heap, _gameAt, _gameBytesAt);
                }
            }
        }

        /// <summary>What <see cref="Game"/> returns, closed at the end of its using block.</summary>
        public readonly struct GameScope : IDisposable
        {
            private readonly long _began;

            internal GameScope(long began)
            {
                _began = began;
            }

            public void Dispose()
            {
                if (_began != 0L)
                {
                    EndGame();
                }
            }
        }

        /// <summary>Opens a scope for the slot; while nothing is measured, an empty value that does nothing.</summary>
        public static Scope Time(int slot)
        {
            if (!On || slot < 0 || slot >= Most)
            {
                return default;
            }
            int frame = FrameSource != null ? FrameSource() : 0;
            if (frame != _frame)
            {
                Roll(frame);
            }
            long heap = _sampling && HeapSource != null ? HeapSource() : -1L;
            long began = Stopwatch.GetTimestamp();
            if (_inGame)
            {
                GameStretchEnds(began, heap);   // the game runs a handler of the mod's: the clock is the mod's again
            }
            Push(false);
            _modDepth++;
            return new Scope(slot, began == 0L ? 1L : began, heap, _gameTicks, _gameBytes);
        }

        /// <summary>
        /// Opens a scope round the game's own work the mod calls: a button, a toggle or a field of the game's pressed for
        /// the RLO interface, every listener of the game's (and other mods') running as a click would run it. Its time and
        /// the heap's growth in it are the game's, out of the mod's scopes round it and the frame's total, less the mod's
        /// own scopes the game runs inside it. While nothing is measured, an empty value that does nothing.
        /// </summary>
        public static GameScope Game()
        {
            if (!On || _depth >= Deep)
            {
                return default;
            }
            int frame = FrameSource != null ? FrameSource() : 0;
            if (frame != _frame)
            {
                Roll(frame);
            }
            long heap = _sampling && HeapSource != null ? HeapSource() : -1L;
            long began = Stopwatch.GetTimestamp();
            if (!_inGame)
            {
                _gameSince = began;
                _gameHeapSince = heap;
            }
            Push(true);
            return new GameScope(began == 0L ? 1L : began);
        }

        private static void Push(bool game)
        {
            if (_depth < Deep)
            {
                GameAt[_depth] = game;
            }
            _depth++;
            _inGame = game && _depth <= Deep;
        }

        // The scope that closed was the innermost: the one round it decides whose the clock is now.
        private static void Pop(long now, long heap)
        {
            if (_depth > 0)
            {
                _depth--;
            }
            _inGame = _depth > 0 && _depth <= Deep && GameAt[_depth - 1];
            if (_inGame)
            {
                _gameSince = now;
                _gameHeapSince = heap;
            }
        }

        private static void GameStretchEnds(long now, long heap)
        {
            _gameTicks += Math.Max(0L, now - _gameSince);
            if (heap >= 0L && _gameHeapSince >= 0L && heap > _gameHeapSince)
            {
                _gameBytes += heap - _gameHeapSince;
            }
        }

        private static void End(int slot, long began, long heap, long gameAt, long gameBytesAt)
        {
            long ended = Stopwatch.GetTimestamp();
            long spent = ended - began - (_gameTicks - gameAt);   // less the game's own work it handed on
            if (spent < 1L)
            {
                spent = 1L;   // 0 means it did not run in the frame
            }
            long heapNow = heap >= 0L && HeapSource != null ? HeapSource() : -1L;
            if (_modDepth > 0)
            {
                _modDepth--;
            }
            Pop(ended, heapNow);
            if (_inGame)
            {
                _insideGameTicks += spent;   // a scope of the mod's the game ran while it worked for the mod
            }
            if (Now[slot] == 0L)
            {
                NowTouched[_nowCount++] = slot;   // each slot once a frame: the list cannot overrun
            }
            Now[slot] += spent;
            if (_modDepth == 0)
            {
                _nowTotal += spent;
            }
            if (heapNow >= 0L)
            {
                long grown = heapNow - heap - (_gameBytes - gameBytesAt);
                if (grown > 0L)
                {
                    NowBytes[slot] += grown;
                    if (_modDepth == 0)
                    {
                        _nowBytesTotal += grown;
                    }
                }
            }
        }

        private static void EndGame()
        {
            long heap = _sampling && HeapSource != null ? HeapSource() : -1L;
            long ended = Stopwatch.GetTimestamp();
            if (_inGame)
            {
                GameStretchEnds(ended, heap);
            }
            Pop(ended, heap);
        }

        /// <summary>The game's own work the mod has handed to it since the record began, in Stopwatch ticks: it only grows.</summary>
        public static long GameTicks => _gameTicks;

        /// <summary>
        /// The mod's own scopes the game ran inside that work (its handlers of the game's events), since the record began,
        /// in Stopwatch ticks: the mod's, in its figures, but not the work of the scope that handed over. It only grows.
        /// </summary>
        public static long InsideGameTicks => _insideGameTicks;

        private static void Roll(int frame)
        {
            for (int i = 0; i < _lastCount; i++)
            {
                int slot = LastTouched[i];
                Last[slot] = 0L;
                LastBytes[slot] = 0L;
            }
            for (int i = 0; i < _nowCount; i++)
            {
                int slot = NowTouched[i];
                Last[slot] = Now[slot];
                LastBytes[slot] = NowBytes[slot];
                LastTouched[i] = slot;
                Now[slot] = 0L;
                NowBytes[slot] = 0L;
            }
            _lastCount = _nowCount;
            _lastTotal = _nowTotal;
            _lastBytesTotal = _nowBytesTotal;
            _lastGame = _gameTicks - _frameGameAt;
            _frameGameAt = _gameTicks;
            _lastFrame = _frame;
            _lastSampled = _sampling;
            _nowCount = 0;
            _nowTotal = _nowBytesTotal = 0L;
            _depth = _modDepth = 0;
            _inGame = false;
            _frame = frame;
            _sampling = SampleEvery <= 1 || frame % SampleEvery == 0;
        }

        /// <summary>Called once a frame by the reader before reading: "the frame before" is then the last one any scope ran in.</summary>
        public static void Settle(int frame)
        {
            if (frame != _frame)
            {
                Roll(frame);
            }
        }

        /// <summary>The reader counts a frame (after <see cref="Settle"/>) as one of play: it goes into the stretch.</summary>
        public static void Keep(int frame)
        {
            _frames++;
            bool sampled = SampleEvery <= 1 || frame % SampleEvery == 0;
            _sampled += sampled ? 1 : 0;
            if (frame != _lastFrame)
            {
                return;   // nothing of the mod's ran in it
            }
            for (int i = 0; i < _lastCount; i++)
            {
                int slot = LastTouched[i];
                long spent = Last[slot];
                Sum[slot] += spent;
                if (spent > Worst[slot])
                {
                    Worst[slot] = spent;
                }
                if (_lastSampled)
                {
                    Bytes[slot] += LastBytes[slot];
                }
            }
            _sumTotal += _lastTotal;
            _sumGame += _lastGame;
            if (_lastGame > _worstGame)
            {
                _worstGame = _lastGame;
            }
            if (_lastTotal > _worstTotal)
            {
                _worstTotal = _lastTotal;
                _worstSlot = Rank(Last, LastTouched, _lastCount, 0);   // a new record only: rare, a walk over the few scopes of the frame
                _worstSlotTicks = _worstSlot >= 0 ? Last[_worstSlot] : 0L;
            }
            if (_lastSampled)
            {
                _bytesTotal += _lastBytesTotal;
            }
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds the mod's scopes took in a frame (after <see cref="Settle"/>); 0 if none ran in it.</summary>
        public static double FrameMs(int frame) => frame == _lastFrame ? Ms(_lastTotal) : 0.0;

        /// <summary>Milliseconds of the game's own work the mod handed to it in a frame (<see cref="Game"/>), not in <see cref="FrameMs"/>.</summary>
        public static double FrameGameMs(int frame) => frame == _lastFrame ? Ms(_lastGame) : 0.0;

        /// <summary>The game's own work the mod handed to it over the stretch: milliseconds in all, and the most in one frame.</summary>
        public static double GameMs => Ms(_sumGame);
        public static double GameWorstMs => Ms(_worstGame);

        /// <summary>The slot with the most of a frame's time after <paramref name="skip"/> larger ones; false when there is none.</summary>
        public static bool FrameTop(int frame, int skip, out int slot, out double ms)
        {
            slot = -1;
            ms = 0.0;
            if (frame != _lastFrame)
            {
                return false;
            }
            int found = Rank(Last, LastTouched, _lastCount, skip);
            if (found < 0)
            {
                return false;
            }
            slot = found;
            ms = Ms(Last[found]);
            return true;
        }

        /// <summary>
        /// Bytes the heap grew by under the mod's scopes in a frame; <paramref name="sampled"/> false (and 0) when the frame
        /// was not a sample or nothing ran in it.
        /// </summary>
        public static long FrameBytes(int frame, out bool sampled)
        {
            sampled = frame == _lastFrame && _lastSampled;
            return sampled ? _lastBytesTotal : 0L;
        }

        /// <summary>The slot under which the heap grew most in a frame, after <paramref name="skip"/> larger ones; false when none.</summary>
        public static bool FrameTopBytes(int frame, int skip, out int slot, out long bytes)
        {
            slot = -1;
            bytes = 0L;
            if (frame != _lastFrame || !_lastSampled)
            {
                return false;
            }
            int found = Rank(LastBytes, LastTouched, _lastCount, skip);
            if (found < 0)
            {
                return false;
            }
            slot = found;
            bytes = LastBytes[found];
            return true;
        }

        /// <summary>Frames kept in the stretch, and of them the samples of the heap.</summary>
        public static int Frames => _frames;
        public static int Sampled => _sampled;

        /// <summary>The mod's scopes over the stretch: milliseconds in all, and the most in one frame.</summary>
        public static double TotalMs => Ms(_sumTotal);
        public static double WorstFrameMs => Ms(_worstTotal);

        /// <summary>The scope that took the most in the stretch's worst frame (its slot, or -1) and its milliseconds in that frame.</summary>
        public static int WorstFrameSlot => _worstSlot;
        public static double WorstFrameSlotMs => Ms(_worstSlotTicks);

        /// <summary>Bytes the heap grew by under the mod's scopes in the sampled frames of the stretch.</summary>
        public static long SampledBytes => _bytesTotal;

        /// <summary>The slot after <paramref name="skip"/> larger ones by the stretch's time (or heap growth), with its sums.</summary>
        public static bool StretchTop(int skip, bool byBytes, out int slot, out double totalMs, out double worstMs, out long bytes)
        {
            slot = Rank(byBytes ? Bytes : Sum, null, _named < Others ? _named : Most, skip);   // once full, the "others" slot too
            if (slot < 0)
            {
                totalMs = worstMs = 0.0;
                bytes = 0L;
                return false;
            }
            totalMs = Ms(Sum[slot]);
            worstMs = Ms(Worst[slot]);
            bytes = Bytes[slot];
            return true;
        }

        // The slot of the (skip + 1)th largest value above 0, ties to the lower slot; -1 if there are not that many. Over
        // the slots listed, or the first count slots without a list.
        private static int Rank(long[] values, int[] listed, int count, int skip)
        {
            long bound = long.MaxValue;
            int boundSlot = -1;
            for (int round = 0; round <= skip; round++)
            {
                long best = 0L;
                int bestSlot = -1;
                for (int i = 0; i < count; i++)
                {
                    int s = listed != null ? listed[i] : i;
                    long value = values[s];
                    bool after = value < bound || (value == bound && s > boundSlot);
                    if (after && (value > best || (value == best && value > 0L && s < bestSlot)))
                    {
                        best = value;
                        bestSlot = s;
                    }
                }
                if (bestSlot < 0)
                {
                    return -1;
                }
                bound = best;
                boundSlot = bestSlot;
            }
            return boundSlot;
        }

        /// <summary>The stretch's sums start again, its line written.</summary>
        public static void ClearStretch()
        {
            Array.Clear(Sum, 0, Most);
            Array.Clear(Worst, 0, Most);
            Array.Clear(Bytes, 0, Most);
            _sumTotal = _worstTotal = _bytesTotal = 0L;
            _sumGame = _worstGame = 0L;
            _worstSlot = -1;
            _worstSlotTicks = 0L;
            _frames = _sampled = 0;
        }

        /// <summary>Forgets everything measured as the record starts or stops; the names stay.</summary>
        public static void Reset()
        {
            ClearStretch();
            Array.Clear(Now, 0, Most);
            Array.Clear(NowBytes, 0, Most);
            Array.Clear(Last, 0, Most);
            Array.Clear(LastBytes, 0, Most);
            _nowCount = _lastCount = 0;
            _nowTotal = _nowBytesTotal = _lastTotal = _lastBytesTotal = 0L;
            _frame = _lastFrame = int.MinValue;
            _depth = _modDepth = 0;
            _inGame = false;
            _frameGameAt = _gameTicks;
            _lastGame = 0L;
            _sampling = _lastSampled = false;
        }
    }
}
