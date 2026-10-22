using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// Checks for the timing of the mod's own scripts that the session record reads (Core/HotSpots.cs: the frame before
// this one by feature and method, nested scopes, the minute's sums and the heap's sampled growth under them), and for
// when the whole scene may be searched for something that can turn up at any time (Core/SearchPace.cs, on a model of
// the game's own fades in DoorFadeCheck.cs), with the before and after of the hair finish's search. Part of the
// work against long frames (HitchCheck.cs). Compiled as they ship.
internal static partial class Program
{
    private static int _hotFrame;
    private static long _hotHeap;

    private static void HotSpotChecks()
    {
        HotSlots();
        HotNames();
        HotOff();
        HotFrames();
        HotStretch();
        HotHeap();
        HotCost();
        DoorFadeChecks();
        HotSpots.On = false;
        HotSpots.Reset();
    }

    // Spends about this many milliseconds of real time (a scope's work, for the checks).
    private static void Spin(double ms)
    {
        long until = Stopwatch.GetTimestamp() + (long)(ms * Stopwatch.Frequency / 1000.0);
        while (Stopwatch.GetTimestamp() < until)
        {
        }
    }

    private static void HotUse()
    {
        HotSpots.FrameSource = () => _hotFrame;
        HotSpots.HeapSource = () => _hotHeap;
        HotSpots.SampleEvery = HotSpots.AllocEvery;
        HotSpots.Reset();
        HotSpots.On = true;
    }

    private static void HotSlots()
    {
        int a = HotSpots.Slot("Check: A.Update");
        int b = HotSpots.Slot("Check: B.Update");
        Check(a != b && HotSpots.Slot("Check: A.Update") == a && HotSpots.Name(a) == "Check: A.Update", "hot spots: a name keeps its number, two names have two");
        Check(HotSpots.Name(-1) == "unnamed" && HotSpots.Name(HotSpots.Most) == "unnamed", "hot spots: a number that was never given has no name");
    }

    // The names Core/Guard gives the slots of the coroutines and handlers it times (HotSpots.Written), read from what the
    // compiler really makes here of a coroutine, a method, a lambda of each kind and a local function.
    private static void HotNames()
    {
        var named = new HotNamed();
        Check(HotSpots.Written(named.Tick().GetType(), null) == "HotNamed.Tick", "hot names: a coroutine by the method it is written in (" + HotSpots.Written(named.Tick().GetType(), null) + ")");
        Action method = named.OnHour;
        Check(HotSpots.Written(method.Method.DeclaringType, method.Method.Name) == "HotNamed.OnHour", "hot names: a method by its own name");
        Action own = named.Own();
        Check(HotSpots.Written(own.Method.DeclaringType, own.Method.Name) == "HotNamed.Own", "hot names: a lambda that uses the object, by the method it is written in (" + HotSpots.Written(own.Method.DeclaringType, own.Method.Name) + ")");
        Action alone = named.Alone();
        Check(HotSpots.Written(alone.Method.DeclaringType, alone.Method.Name) == "HotNamed.Alone", "hot names: a lambda that uses nothing, out of the compiler's class (" + HotSpots.Written(alone.Method.DeclaringType, alone.Method.Name) + ")");
        Action captured = named.Captured(3);
        Check(HotSpots.Written(captured.Method.DeclaringType, captured.Method.Name) == "HotNamed.Captured", "hot names: a lambda that keeps a local, out of the compiler's class (" + HotSpots.Written(captured.Method.DeclaringType, captured.Method.Name) + ")");
        Action local = named.Local();
        Check(HotSpots.Written(local.Method.DeclaringType, local.Method.Name) == "HotNamed.Local", "hot names: a local function by the method it is written in (" + HotSpots.Written(local.Method.DeclaringType, local.Method.Name) + ")");
        Check(HotSpots.Written(typeof(HotWalk), null) == "HotWalk.MoveNext", "hot names: an IEnumerator written by hand, by its MoveNext");
        Check(HotSpots.Written(null, null) == "a handler.MoveNext", "hot names: nothing to name it by");
    }

    private sealed class HotNamed
    {
        private int _count;

        public IEnumerator Tick()
        {
            _count++;
            yield return null;
        }

        public void OnHour()
        {
            _count++;
        }

        public Action Own()
        {
            return () => _count++;
        }

        public Action Alone()
        {
            return () => Console.Write(string.Empty);
        }

        public Action Captured(int by)
        {
            return () => _count += by;
        }

        public Action Local()
        {
            void Count()
            {
                Console.Write(string.Empty);
            }
            return Count;
        }
    }

    private sealed class HotWalk : IEnumerator
    {
        public object Current => null;

        public bool MoveNext()
        {
            return false;
        }

        public void Reset()
        {
        }
    }

    // Off, as the mod ships: a scope is an empty value, nothing is kept and nothing is allocated.
    private static void HotOff()
    {
        HotUse();
        HotSpots.On = false;
        int a = HotSpots.Slot("Check: A.Update");
        _hotFrame = 1;
        using (HotSpots.Time(a))
        {
            Spin(0.2);
        }
        _hotFrame = 2;
        HotSpots.Settle(2);
        Check(HotSpots.FrameMs(1) == 0.0 && !HotSpots.FrameTop(1, 0, out _, out _), "hot spots: off, nothing is measured");
        long allocated = OffScopes(a, out double ms);
        Check(allocated == 0, "hot spots: off, a million scopes allocate nothing (" + allocated + " bytes)");
        Console.WriteLine("hot spots: a scope while the session record is off: " + (ms * 1000000.0 / 1000000).ToString("F2") + " ns");
    }

    // One frame read whole the frame after: the total, the order, a scope inside another counted once in the total.
    private static void HotFrames()
    {
        HotUse();
        int a = HotSpots.Slot("Check: A.Update");
        int b = HotSpots.Slot("Check: B.Update");
        int c = HotSpots.Slot("Check: C.OnHour");
        _hotFrame = 10;
        using (HotSpots.Time(a))
        {
            Spin(2.0);
            using (HotSpots.Time(c))   // an event of the game's that A set off
            {
                Spin(1.0);
            }
        }
        using (HotSpots.Time(b))
        {
            Spin(0.8);
        }
        using (HotSpots.Time(b))   // (twice in the frame: one sum)
        {
            Spin(0.8);
        }
        _hotFrame = 11;
        HotSpots.Settle(11);
        bool first = HotSpots.FrameTop(10, 0, out int s0, out double ms0);
        bool second = HotSpots.FrameTop(10, 1, out int s1, out double ms1);
        bool third = HotSpots.FrameTop(10, 2, out int s2, out double ms2);
        bool fourth = HotSpots.FrameTop(10, 3, out _, out _);
        Check(first && second && third && !fourth && s0 == a && s1 == b && s2 == c, "hot spots: the frame's three, the most first (" + HotSpots.Name(s0) + ", " + HotSpots.Name(s1) + ", " + HotSpots.Name(s2) + ")");
        Check(ms0 >= 3.0 && ms1 >= 1.6 && ms2 >= 1.0 && ms2 < ms0, "hot spots: each with its time, the one inside A within A's (" + ms0.ToString("F2") + ", " + ms1.ToString("F2") + ", " + ms2.ToString("F2") + " ms)");
        double total = HotSpots.FrameMs(10);
        Check(Math.Abs(total - (ms0 + ms1)) < 1e-6, "hot spots: the frame's total counts the scope inside another once (" + total.ToString("F2") + " ms = A and B)");
        Check(HotSpots.FrameMs(9) == 0.0 && HotSpots.FrameMs(11) == 0.0, "hot spots: a frame in which nothing ran, or that is not over, has nothing");

        // A frame with nothing of the mod's in it, then one with: the frame before is always the right one.
        _hotFrame = 12;
        HotSpots.Settle(12);
        Check(HotSpots.FrameMs(11) == 0.0 && HotSpots.FrameMs(10) == 0.0, "hot spots: after a frame of nothing, the frame before has nothing");
        using (HotSpots.Time(b))
        {
            Spin(0.2);
        }
        HotSpots.Settle(12);   // (asked again in the same frame: nothing moves)
        _hotFrame = 13;
        using (HotSpots.Time(a))   // the first scope of a frame rolls it over by itself, before the reader asks
        {
            Spin(0.2);
        }
        HotSpots.Settle(13);
        Check(HotSpots.FrameTop(12, 0, out int only, out _) && only == b && !HotSpots.FrameTop(12, 1, out _, out _), "hot spots: the frame before is rolled over by the first scope of the next, as by the reader");

        // Turned off in the middle of a scope: it still closes, and nothing is left open.
        _hotFrame = 14;
        HotSpots.Scope open = HotSpots.Time(a);
        HotSpots.On = false;
        open.Dispose();
        HotSpots.On = true;
        _hotFrame = 15;
        HotSpots.Settle(15);
        Check(HotSpots.FrameTop(14, 0, out int closed, out _) && closed == a, "hot spots: a scope open when the record stops still closes");
    }

    // The minute: only the frames the reader keeps, each slot's sum and worst, the most first.
    private static void HotStretch()
    {
        HotUse();
        int a = HotSpots.Slot("Check: A.Update");
        int b = HotSpots.Slot("Check: B.Update");
        for (int f = 100; f < 110; f++)
        {
            _hotFrame = f;
            HotSpots.Settle(f);
            if (f > 100)
            {
                HotSpots.Keep(f - 1);   // the reader counts the frame before as play...
            }
            using (HotSpots.Time(a))
            {
                Spin(f == 104 ? 2.0 : 0.1);
            }
            if (f % 2 == 0)
            {
                using (HotSpots.Time(b))
                {
                    Spin(0.3);
                }
            }
        }
        _hotFrame = 110;
        HotSpots.Settle(110);
        HotSpots.Keep(109);
        // ...and a loading screen of fifty frames is not kept: the mod's work behind it is not in the minute.
        for (int f = 111; f < 161; f++)
        {
            _hotFrame = f;
            HotSpots.Settle(f);
            using (HotSpots.Time(b))
            {
                Spin(0.05);
            }
        }
        Check(HotSpots.Frames == 10, "hot spots: the minute has the frames the reader kept (" + HotSpots.Frames + ")");
        bool one = HotSpots.StretchTop(0, false, out int top, out double topMs, out double topWorst, out _);
        bool two = HotSpots.StretchTop(1, false, out int next, out double nextMs, out _, out _);
        Check(one && two && top == a && next == b && !HotSpots.StretchTop(2, false, out _, out _, out _, out _), "hot spots: the minute's most first, nothing more than ran");
        Check(topWorst >= 2.0 && topMs >= 2.9 && nextMs >= 1.5 && nextMs < 2.5, "hot spots: each with its sum and its worst frame (" + topMs.ToString("F2") + " and " + topWorst.ToString("F2") + ", " + nextMs.ToString("F2") + " ms)");
        Check(HotSpots.WorstFrameMs >= 2.0 && HotSpots.TotalMs >= topMs + nextMs - 1e-6, "hot spots: the minute's total and its worst frame");
        HotSpots.ClearStretch();
        Check(HotSpots.Frames == 0 && HotSpots.TotalMs == 0.0 && !HotSpots.StretchTop(0, false, out _, out _, out _, out _), "hot spots: a new minute starts from nothing");
    }

    // The heap: read round each scope in one frame of eight, and only those frames' growth is in the minute.
    private static void HotHeap()
    {
        HotUse();
        int a = HotSpots.Slot("Check: A.Update");
        int b = HotSpots.Slot("Check: B.Update");
        long expectedA = 0, expectedB = 0;
        for (int f = 0; f < 64; f++)
        {
            _hotFrame = f;
            HotSpots.Settle(f);
            if (f > 0)
            {
                HotSpots.Keep(f - 1);
            }
            using (HotSpots.Time(a))
            {
                _hotHeap += 4096;   // A makes a block of garbage every frame
                using (HotSpots.Time(b))
                {
                    _hotHeap += 1024;   // ...and B, inside it, a smaller one
                }
            }
            _hotHeap += 100000;   // the game's own, outside the mod's scopes
            if (f % HotSpots.SampleEvery == 0)
            {
                expectedA += 4096 + 1024;
                expectedB += 1024;
            }
        }
        _hotFrame = 64;
        HotSpots.Settle(64);
        HotSpots.Keep(63);
        HotSpots.StretchTop(0, true, out int most, out _, out _, out long mostBytes);
        HotSpots.StretchTop(1, true, out int less, out _, out _, out long lessBytes);
        Check(HotSpots.Sampled == 8 && HotSpots.Frames == 64, "hot spots: one frame in eight is a sample of the heap (" + HotSpots.Sampled + " of " + HotSpots.Frames + ")");
        Check(most == a && mostBytes == expectedA && less == b && lessBytes == expectedB, "hot spots: the heap's growth under each scope, in the sampled frames (" + mostBytes + ", " + lessBytes + ")");
        Check(HotSpots.SampledBytes == expectedA, "hot spots: the mod's total counts the growth inside a nested scope once, and none of the game's (" + HotSpots.SampledBytes + ")");

        // The frame before, for the line of a frame in which the heap leapt: known only for a sampled frame.
        long frameBytes = HotSpots.FrameBytes(63, out bool sampled63);
        Check(!sampled63 && frameBytes == 0, "hot spots: a frame that was not a sample says it is not known");
        _hotFrame = 64;
        using (HotSpots.Time(b))
        {
            _hotHeap += 9 * 1024 * 1024;
        }
        _hotFrame = 65;
        HotSpots.Settle(65);
        frameBytes = HotSpots.FrameBytes(64, out bool sampled64);
        Check(sampled64 && frameBytes == 9 * 1024 * 1024 && HotSpots.FrameTopBytes(64, 0, out int leapt, out _) && leapt == b, "hot spots: a sampled frame's growth under the mod's scripts, and under which");

        // Where the runtime counts allocations, every frame is a sample.
        HotUse();
        HotSpots.SampleEvery = 1;
        HotSpots.Reset();
        for (int f = 1; f <= 16; f++)
        {
            _hotFrame = f;
            HotSpots.Settle(f);
            if (f > 1)
            {
                HotSpots.Keep(f - 1);
            }
            using (HotSpots.Time(a))
            {
                _hotHeap += 100;
            }
        }
        Check(HotSpots.Sampled == 15 && HotSpots.SampledBytes == 1500, "hot spots: counted exactly, every frame is a sample (" + HotSpots.Sampled + ", " + HotSpots.SampledBytes + ")");
        HotSpots.SampleEvery = HotSpots.AllocEvery;
    }

    // On: what a scope costs, and that it allocates nothing.
    private static void HotCost()
    {
        HotUse();
        HotSpots.HeapSource = () => GC.GetTotalMemory(false);
        int a = HotSpots.Slot("Check: A.Update");
        const int Scopes = 1000000;
        long allocated = OnScopes(a, Scopes, out double ms);
        double each = ms * 1000000.0 / Scopes;
        Check(allocated == 0, "hot spots: on, a million scopes allocate nothing (" + allocated + " bytes)");
        Check(each < 2000.0, "hot spots: on, a scope costs well under two microseconds (" + each.ToString("F0") + " ns)");
        Console.WriteLine("hot spots: a scope while the session record runs: " + each.ToString("F0") + " ns; a frame of the city's 138 scopes: " + (each * 138 / 1000.0).ToString("F1") + " microseconds");
    }

    // The two measured loops are compiled in full before they run. Left to the .NET runtime's tiers, a loop of a million
    // turns is swapped for compiled code part way through (on-stack replacement), and the swap itself is counted as
    // about 6 KB allocated on this thread: the runtime's, not the scopes' (seen on 0.9.10's checks; with tiering or the
    // swap switched off the count is 0). The game runs the mod on Unity's own runtime, which does no such swap.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long OffScopes(int slot, out double ms)
    {
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < 1000000; i++)
        {
            using var hotSpot = HotSpots.Time(slot);
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.Elapsed.TotalMilliseconds;
        return allocated;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long OnScopes(int slot, int scopes, out double ms)
    {
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < scopes; i++)
        {
            if (i % 120 == 0)
            {
                _hotFrame++;   // a hundred and twenty scopes a frame, as in the city
            }
            using var hotSpot = HotSpots.Time(slot);
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.Elapsed.TotalMilliseconds;
        return allocated;
    }
}
