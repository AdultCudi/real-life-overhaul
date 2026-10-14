using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the mod's own share of a frame goes, by feature and method, for the session record's lines
    /// (Diagnostics/SessionWatch). Every Update, LateUpdate and FixedUpdate of the mod's, and its handlers of the
    /// game's events (a new hour, a new day, going in or out of a building, the city map), open a scope on their
    /// first line:
    ///
    ///     private static readonly int HotUpdate = HotSpots.Slot("Transport: TransitDirector.Update");
    ///     private void Update()
    ///     {
    ///         using var hotSpot = HotSpots.Time(HotUpdate);
    ///
    /// The directors' one-second timers open one in the method their tick calls ("Tech: TechDirector.Tick", and each
    /// follower of the credit side's second, "Economy: EstateDirector.Second"), never round a yield, so that the time
    /// the engine counts under "coroutines and timers" has the mod's names too. Every step of a coroutine started through
    /// Core/Guard.Routine, and every handler Core/Guard wraps (the game's events, UnityEvent listeners, the questions the
    /// game asks the mod), is timed by the guard itself under "Coroutine: Class.Method", "Event: ...", "Listener: ...",
    /// "Asked: ..." and "Callback: ..." (Guard.RoutineSlot, Guard.HandlerSlot): nothing the mod runs is left out.
    ///
    /// While the session record is off (as the mod ships) a scope is one flag test and nothing else. While it is
    /// on, a scope is two reads of the clock and one of the frame count, and a few additions into fixed arrays:
    /// for the mod's hundred-odd scopes a frame, some five microseconds. One frame in <see cref="SampleEvery"/> is
    /// also a sample of the managed heap: two reads round each scope, which say how much of the heap's growth
    /// happened under the mod's scripts. Where the runtime counts what each thread allocates, that count is read and
    /// every frame is a sample (exact); otherwise the heap's size is read, one frame in eight (an estimate: the heap
    /// grows in the blocks the collector hands out, not object by object). Diagnostics/SessionWatch finds out which
    /// when the record starts. Nothing is allocated after the arrays are made, whether it is on or off.
    ///
    /// The time of a scope opened inside another (an event of the game's that one of the mod's Updates set off)
    /// is counted under both names, and once in the mod's total.
    ///
    /// The frame before this one is kept whole (<see cref="FrameMs"/>, <see cref="FrameTop"/>); the reader adds
    /// the frames it counts as play into a stretch (<see cref="Keep"/>), which it reads and clears once a minute.
    ///
    /// No Unity or game types: the frame count and the heap's size are handed in (<see cref="FrameSource"/>,
    /// <see cref="HeapSource"/>), and it is checked offline (NavCheck, HitchCheck.cs).
    /// </summary>
    public static class HotSpots
    {
        public const int Most = 768;       // names that can be told apart (the Updates, and every coroutine and handler the guard wraps); any more share the last one
        public const int AllocEvery = 8;   // one frame in this many is a sample of the heap, where only its size can be read

        /// <summary>One frame in this many is a sample of the heap: <see cref="AllocEvery"/>, or 1 where the reads are exact.</summary>
        public static int SampleEvery = AllocEvery;

        /// <summary>True while the session record runs: only then is anything measured.</summary>
        public static bool On;

        /// <summary>The engine's frame count (Diagnostics/SessionWatch hands it in).</summary>
        public static Func<int> FrameSource;

        /// <summary>Bytes of the managed heap in use (Diagnostics/SessionWatch hands it in).</summary>
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
        private static int _depth;

        // The frame before it, complete.
        private static readonly long[] Last = new long[Most];
        private static readonly long[] LastBytes = new long[Most];
        private static readonly int[] LastTouched = new int[Most];
        private static int _lastCount;
        private static long _lastTotal, _lastBytesTotal;
        private static int _lastFrame = int.MinValue;
        private static bool _lastSampled;

        // The stretch the reader keeps (a minute of play).
        private static readonly long[] Sum = new long[Most];
        private static readonly long[] Worst = new long[Most];
        private static readonly long[] Bytes = new long[Most];
        private static long _sumTotal, _worstTotal, _bytesTotal;
        private static int _frames, _sampled;

        /// <summary>
        /// The number for a name ("Feature: Class.Method", a constant text). The same name always gets the same
        /// number, so a component made again with every city keeps its own. Made once, in a static field.
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
                if (_named >= Most)
                {
                    Names[Most - 1] = "others";
                    return Most - 1;
                }
                slot = _named++;
                Names[slot] = name;
                ByName[name] = slot;
                return slot;
            }
        }

        /// <summary>The name a number was given.</summary>
        public static string Name(int slot) => slot >= 0 && slot < Most && Names[slot] != null ? Names[slot] : "unnamed";

        /// <summary>
        /// "Class.Method" as the code is written, for a slot of a coroutine or a handler (Core/Guard.RoutineSlot and
        /// HandlerSlot): out of the classes the compiler makes for lambdas and coroutines ("&lt;&gt;c", "&lt;&gt;c__DisplayClass4_0",
        /// "&lt;Tick&gt;d__12") to the class they are written in, and a method the compiler made ("&lt;Awake&gt;b__12_0") by the method
        /// it is written in. A coroutine (no method given) is named by the method its class was made from; a class written by
        /// hand as an IEnumerator by its MoveNext. Asked once per coroutine or handler method, never per frame.
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

        /// <summary>One scope: what <see cref="Time"/> returns, closed at the end of its using block.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly int _slot;
            private readonly long _began;
            private readonly long _heap;

            internal Scope(int slot, long began, long heap)
            {
                _slot = slot;
                _began = began;
                _heap = heap;
            }

            public void Dispose()
            {
                if (_began != 0L)
                {
                    End(_slot, _began, _heap);
                }
            }
        }

        /// <summary>Opens a scope for the slot. While nothing is measured it is an empty value and closing it does nothing.</summary>
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
            _depth++;
            long heap = _sampling && HeapSource != null ? HeapSource() : -1L;
            long began = Stopwatch.GetTimestamp();
            return new Scope(slot, began == 0L ? 1L : began, heap);
        }

        private static void End(int slot, long began, long heap)
        {
            long spent = Stopwatch.GetTimestamp() - began;
            if (spent < 1L)
            {
                spent = 1L;   // (a slot that ran is never 0: 0 means it did not run in the frame)
            }
            if (_depth > 0)
            {
                _depth--;
            }
            if (Now[slot] == 0L)
            {
                NowTouched[_nowCount++] = slot;   // (each slot once a frame: the list cannot overrun)
            }
            Now[slot] += spent;
            if (_depth == 0)
            {
                _nowTotal += spent;
            }
            if (heap >= 0L && HeapSource != null)
            {
                long grown = HeapSource() - heap;
                if (grown > 0L)
                {
                    NowBytes[slot] += grown;
                    if (_depth == 0)
                    {
                        _nowBytesTotal += grown;
                    }
                }
            }
        }

        // A new frame has begun: the one before it becomes "the frame before".
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
            _lastFrame = _frame;
            _lastSampled = _sampling;
            _nowCount = 0;
            _nowTotal = _nowBytesTotal = 0L;
            _depth = 0;
            _frame = frame;
            _sampling = SampleEvery <= 1 || frame % SampleEvery == 0;
        }

        /// <summary>
        /// Brings the record up to this frame before it is read: afterwards "the frame before" is the last frame in which
        /// any scope ran. Called once a frame by the reader, whether or not anything of the mod's ran yet in this one.
        /// </summary>
        public static void Settle(int frame)
        {
            if (frame != _frame)
            {
                Roll(frame);
            }
        }

        /// <summary>
        /// The reader counts a frame (after <see cref="Settle"/>) as one of play: it goes into the stretch, whether or not
        /// anything of the mod's ran in it. A loading screen or a pause the reader leaves out is simply not kept.
        /// </summary>
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
            if (_lastTotal > _worstTotal)
            {
                _worstTotal = _lastTotal;
            }
            if (_lastSampled)
            {
                _bytesTotal += _lastBytesTotal;
            }
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds the mod's scopes took in a frame (after <see cref="Settle"/>); 0 if none ran in it.</summary>
        public static double FrameMs(int frame) => frame == _lastFrame ? Ms(_lastTotal) : 0.0;

        /// <summary>
        /// The slot that took the most of a frame's time after <paramref name="skip"/> larger ones (0 the largest), with
        /// its milliseconds. False when there is no such slot, or nothing ran in that frame.
        /// </summary>
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
        /// Bytes the heap grew by under the mod's scopes in a frame (after <see cref="Settle"/>), the inner scopes counted once;
        /// <paramref name="sampled"/> false (and 0) when that frame was not a sample of the heap or nothing of the mod's ran in it.
        /// </summary>
        public static long FrameBytes(int frame, out bool sampled)
        {
            sampled = frame == _lastFrame && _lastSampled;
            return sampled ? _lastBytesTotal : 0L;
        }

        /// <summary>The slot under which the heap grew the most in a frame, after <paramref name="skip"/> larger ones. False when there is none.</summary>
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

        /// <summary>Bytes the heap grew by under the mod's scopes in the sampled frames of the stretch.</summary>
        public static long SampledBytes => _bytesTotal;

        /// <summary>
        /// The slot after <paramref name="skip"/> larger ones by the stretch's time (<paramref name="byBytes"/>: by the heap's
        /// growth under it instead), with its sums. False when there is no such slot.
        /// </summary>
        public static bool StretchTop(int skip, bool byBytes, out int slot, out double totalMs, out double worstMs, out long bytes)
        {
            slot = Rank(byBytes ? Bytes : Sum, null, _named < Most ? _named : Most, skip);
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

        // The slot of the (skip + 1)th largest value above 0, ties to the lower slot; -1 if there are not that many. The
        // slots looked at are those listed, or the first "count" slots when there is no list.
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

        /// <summary>The stretch's sums start again (the line a minute has been written).</summary>
        public static void ClearStretch()
        {
            Array.Clear(Sum, 0, Most);
            Array.Clear(Worst, 0, Most);
            Array.Clear(Bytes, 0, Most);
            _sumTotal = _worstTotal = _bytesTotal = 0L;
            _frames = _sampled = 0;
        }

        /// <summary>Everything measured is forgotten (the record starts or stops). The names stay.</summary>
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
            _depth = 0;
            _sampling = _lastSampled = false;
        }
    }
}
