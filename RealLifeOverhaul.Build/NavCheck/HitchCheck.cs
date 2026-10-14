using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// Checks for the game-free parts of the work against long frames, compiled as they ship: the keeper of handlers on the
// game's event fields (Core/EventHold.cs: several holders on one event must not put each other back for ever), and the
// two fixed rings the session record reads for the line of a long frame (Core/WorkRings.cs: the last pieces of work
// the mod named, with their times, and the navigation obstacles it changed); and, in HotSpotCheck.cs, the timing of the
// mod's own scripts by feature and method (Core/HotSpots.cs) and when the whole scene may be searched (Core/SearchPace.cs).
//
// What is not here: anything of the game or the engine. Whether a frame is long, and what was in it, is for the game
// to show in the "Session: a frame of ..." lines of Player.log; whether a search of the scene was made in play, in its
// "Scene search check (...)" lines.
internal static partial class Program
{
    private static void Hitch()
    {
        HoldOne();
        HoldSeveral();
        HoldEmptied();
        HoldOrder();
        HoldEdges();
        HoldCost();
        Marks();
        Counts();
        HotSpotChecks();
        Prewarms();
    }

    private static Action Handler(int[] calls, int slot)
    {
        return () => calls[slot]++;
    }

    // One holder: put on once, and asking again changes nothing.
    private static void HoldOne()
    {
        Action field = null, seen = null;
        var calls = new int[1];
        Action handler = Handler(calls, 0);
        int put = EventHold.PutBack;
        EventHold.Keep(ref field, handler, ref seen);
        Check(EventHold.PutBack - put == 1 && ReferenceEquals(field, seen) && EventHold.Holds(field, handler), "hold: a handler is put on an empty event once");
        Action after = field;
        int looked = EventHold.Looked;
        for (int second = 0; second < 60; second++)
        {
            EventHold.Keep(ref field, handler, ref seen);
        }
        Check(ReferenceEquals(field, after) && EventHold.PutBack - put == 1 && EventHold.Looked == looked, "hold: asking again for a minute puts nothing on and looks into nothing");
        field();
        Check(calls[0] == 1, "hold: the handler is called once per event");
    }

    // The fault this replaces: three holders on one event, each asking once a second. Each used to take itself off and
    // put itself back whenever the field was not the one it last saw, which changed it for the other two, for ever.
    private static void HoldSeveral()
    {
        Action field = null;
        var seen = new Action[3];
        var calls = new int[4];
        var handlers = new[] { Handler(calls, 0), Handler(calls, 1), Handler(calls, 2) };
        Action game = Handler(calls, 3);
        field += game;   // something of the game's is on the event already
        int put = EventHold.PutBack;
        for (int h = 0; h < 3; h++)
        {
            EventHold.Keep(ref field, handlers[h], ref seen[h]);
        }
        Check(EventHold.PutBack - put == 3, "hold: three holders of one event are each put on once");
        // The second time round each finds a field that is not the one it saw (the others joined after it), and itself still in it.
        int looked = EventHold.Looked;
        for (int h = 0; h < 3; h++)
        {
            EventHold.Keep(ref field, handlers[h], ref seen[h]);
        }
        Action settled = field;
        Check(EventHold.PutBack - put == 3 && EventHold.Looked - looked == 2, "hold: the second time round nobody is put back, and only the two who joined before the last look again");
        looked = EventHold.Looked;
        for (int second = 0; second < 3600; second++)
        {
            for (int h = 0; h < 3; h++)
            {
                EventHold.Keep(ref field, handlers[h], ref seen[h]);
            }
        }
        Check(ReferenceEquals(field, settled) && EventHold.PutBack - put == 3 && EventHold.Looked == looked, "hold: an hour of three holders asking every second changes the event not once");
        field();
        Check(calls[0] == 1 && calls[1] == 1 && calls[2] == 1 && calls[3] == 1, "hold: each of the three and the game's own handler is called once");

        // Something of the game's joins and leaves (a customer walks in, an employee goes home): the event changes, and nobody is put back for it.
        var extra = new int[1];
        Action comer = Handler(extra, 0);
        field += comer;
        for (int h = 0; h < 3; h++)
        {
            EventHold.Keep(ref field, handlers[h], ref seen[h]);
        }
        field -= comer;
        looked = EventHold.Looked;
        for (int h = 0; h < 3; h++)
        {
            EventHold.Keep(ref field, handlers[h], ref seen[h]);
        }
        Check(EventHold.PutBack - put == 3 && EventHold.Looked - looked == 3, "hold: a change by somebody else is looked into once by each holder and puts nobody back");
        Check(field.GetInvocationList().Length == 4, "hold: the event holds the game's handler and the three, nothing twice");
    }

    // The game empties its events on every load: everybody is put back, once.
    private static void HoldEmptied()
    {
        Action field = null;
        var seen = new Action[3];
        var calls = new int[3];
        var handlers = new[] { Handler(calls, 0), Handler(calls, 1), Handler(calls, 2) };
        for (int h = 0; h < 3; h++)
        {
            EventHold.Keep(ref field, handlers[h], ref seen[h]);
        }
        field = null;
        int put = EventHold.PutBack;
        for (int second = 0; second < 10; second++)
        {
            for (int h = 0; h < 3; h++)
            {
                EventHold.Keep(ref field, handlers[h], ref seen[h]);
            }
        }
        Check(EventHold.PutBack - put == 3 && field != null && field.GetInvocationList().Length == 3, "hold: after the game empties the event each holder is put back once");
        field();
        Check(calls[0] == 1 && calls[1] == 1 && calls[2] == 1, "hold: and each is called once");

        // Somebody takes one holder's handler off: it alone is put back.
        field -= handlers[1];
        put = EventHold.PutBack;
        for (int second = 0; second < 5; second++)
        {
            for (int h = 0; h < 3; h++)
            {
                EventHold.Keep(ref field, handlers[h], ref seen[h]);
            }
        }
        Check(EventHold.PutBack - put == 1 && EventHold.Holds(field, handlers[1]) && field.GetInvocationList().Length == 3, "hold: a handler somebody took off is put back, and only that one");
    }

    // A handler stands behind everything that was on the event when it joined, and stays there.
    private static void HoldOrder()
    {
        string order = string.Empty;
        Action field = null, seenA = null, seenB = null;
        Action game = () => order += "g";
        Action a = () => order += "a";
        Action b = () => order += "b";
        Action late = () => order += "l";
        field += game;
        EventHold.Keep(ref field, a, ref seenA);
        EventHold.Keep(ref field, b, ref seenB);
        field += late;
        for (int second = 0; second < 5; second++)
        {
            EventHold.Keep(ref field, a, ref seenA);
            EventHold.Keep(ref field, b, ref seenB);
        }
        field();
        Check(order == "gabl", "hold: handlers are called in the order they joined (" + order + ")");
    }

    private static void HoldEdges()
    {
        Action field = null, seen = null;
        int put = EventHold.PutBack, looked = EventHold.Looked;
        EventHold.Keep(ref field, null, ref seen);
        Check(field == null && seen == null && EventHold.PutBack == put && EventHold.Looked == looked, "hold: no handler, nothing done");

        var calls = new int[2];
        Action one = Handler(calls, 0), two = Handler(calls, 1);
        Check(!EventHold.Holds(null, one) && !EventHold.Holds(one, null), "hold: an empty event holds nothing, and nothing is not held");
        Check(EventHold.Holds(one, one) && !EventHold.Holds(one, two), "hold: an event of one handler holds that handler and no other");
        Action both = one + two;
        Check(EventHold.Holds(both, one) && EventHold.Holds(both, two) && !EventHold.Holds(both, Handler(calls, 0)), "hold: an event of two holds both and not a third");

        // A typed event (the game's "entered a building" has an argument) is kept the same way.
        Action<int> typed = null, typedSeen = null;
        int got = 0;
        Action<int> taker = value => got += value;
        for (int second = 0; second < 3; second++)
        {
            EventHold.Keep(ref typed, taker, ref typedSeen);
        }
        typed(7);
        Check(got == 7 && typed.GetInvocationList().Length == 1, "hold: an event with an argument is kept the same way");

        // One handler asked for through two holders' own memories (two features sharing a handler) is still on once.
        Action shared = null, seenOne = null, seenTwo = null;
        for (int second = 0; second < 4; second++)
        {
            EventHold.Keep(ref shared, one, ref seenOne);
            EventHold.Keep(ref shared, one, ref seenTwo);
        }
        Check(shared.GetInvocationList().Length == 1, "hold: one handler kept through two memories is on the event once");

        // The same asking gives the same counts every time.
        int[] first = HoldRun(), second2 = HoldRun();
        Check(first[0] == second2[0] && first[1] == second2[1] && first[0] == 5, "hold: the same asking puts back the same number every time (" + first[0] + ", " + second2[0] + ")");
    }

    private static int[] HoldRun()
    {
        Action field = null;
        var seen = new Action[5];
        var calls = new int[5];
        var handlers = new Action[5];
        int put = EventHold.PutBack, looked = EventHold.Looked;
        for (int h = 0; h < 5; h++)
        {
            handlers[h] = Handler(calls, h);
        }
        for (int second = 0; second < 30; second++)
        {
            for (int h = 0; h < 5; h++)
            {
                EventHold.Keep(ref field, handlers[h], ref seen[h]);
            }
        }
        return new[] { EventHold.PutBack - put, EventHold.Looked - looked };
    }

    // What asking costs while nothing changes: one comparison, nothing allocated; and what the old way cost.
    private static void HoldCost()
    {
        const int Holders = 8, Others = 200;
        Action field = null;
        var others = new int[1];
        for (int i = 0; i < Others; i++)
        {
            field += Handler(others, 0);   // a busy event: two hundred of the game's own handlers
        }
        var seen = new Action[Holders];
        var handlers = new Action[Holders];
        var calls = new int[Holders];
        for (int h = 0; h < Holders; h++)
        {
            handlers[h] = Handler(calls, h);
        }
        for (int round = 0; round < 2; round++)
        {
            for (int h = 0; h < Holders; h++)
            {
                EventHold.Keep(ref field, handlers[h], ref seen[h]);
            }
        }
        long allocated = Asks(ref field, handlers, seen, out long ms);
        Check(allocated == 0, "hold: eight holders asking 100,000 times on a settled event allocate nothing (" + allocated + " bytes)");
        Check(ms < 500, "hold: and take no time to speak of (" + ms + " ms for 800,000 askings)");

        // The old way, for the record of what was removed: each holder takes itself off and puts itself back when the field is not the one it saw.
        Action old = null;
        for (int i = 0; i < Others; i++)
        {
            old += Handler(others, 0);
        }
        var oldSeen = new Action[3];
        long before = GC.GetAllocatedBytesForCurrentThread();
        int putBack = 0;
        for (int second = 0; second < 60; second++)
        {
            for (int h = 0; h < 3; h++)
            {
                if (oldSeen[h] == null || !ReferenceEquals(old, oldSeen[h]))
                {
                    old = (Action)Delegate.Combine(Delegate.Remove(old, handlers[h]), handlers[h]);
                    oldSeen[h] = old;
                    putBack++;
                }
            }
        }
        long oldBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(putBack == 180 && oldBytes > 100000, "hold: the old way put three holders back 180 times in a minute (" + putBack + ") and allocated for each (" + oldBytes + " bytes a minute on an event of 200)");
    }

    // The measured loop is compiled in full before it runs, as HotSpotCheck's are. Left to the .NET runtime's tiers, a loop
    // of 800,000 askings is swapped for compiled code part way through (on-stack replacement), and the swap is counted as
    // about 6 KB allocated on this thread: the runtime's, not the holders' (seen on 0.11.2's checks; with tiering switched
    // off the count is 0). The game runs the mod on Unity's own runtime, which does no such swap.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long Asks(ref Action field, Action[] handlers, Action[] seen, out long ms)
    {
        Stopwatch watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int second = 0; second < 100000; second++)
        {
            for (int h = 0; h < handlers.Length; h++)
            {
                EventHold.Keep(ref field, handlers[h], ref seen[h]);
            }
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.ElapsedMilliseconds;
        return allocated;
    }

    private static void Marks()
    {
        var ring = new MarkRing();
        Check(ring.Count == 0 && !ring.Get(0, out _, out _, out _, out _) && ring.SpentIn(5) == 0, "marks: an empty ring has nothing to say");

        int a = ring.Add("a", 10, 1.5f, 1000);
        bool got = ring.Get(0, out string text, out int frame, out float at, out long spent);
        Check(got && text == "a" && frame == 10 && at == 1.5f && spent < 0 && ring.Count == 1, "marks: a piece of work is remembered with its frame and time, not timed until it is closed");
        ring.Close(a, 1250);
        ring.Get(0, out _, out _, out _, out spent);
        Check(spent == 250, "marks: closing it gives its time (" + spent + ")");
        ring.Close(a, 9999);
        ring.Get(0, out _, out _, out _, out spent);
        Check(spent == 250, "marks: closing it a second time leaves the first time standing");

        // Work inside work: both are closed by their own number.
        int outer = ring.Add("outer", 11, 2f, 2000);
        int inner = ring.Add("inner", 11, 2f, 2100);
        ring.Close(inner, 2150);
        ring.Close(outer, 2400);
        ring.Get(0, out text, out _, out _, out spent);
        ring.Get(1, out string second, out _, out _, out long secondSpent);
        Check(text == "inner" && spent == 50 && second == "outer" && secondSpent == 400, "marks: a piece inside another is closed by its own number");
        Check(ring.SpentIn(11) == 450 && ring.SpentIn(10) == 250 && ring.SpentIn(12) == 0, "marks: the time named in a frame is that of the pieces that began in it");

        // The ring keeps three: the oldest goes, and its number closes nothing any more.
        int late = ring.Add("late", 12, 3f, 3000);
        Check(ring.Count == MarkRing.Kept && ring.Get(2, out text, out _, out _, out _) && text == "outer" && !ring.Get(3, out _, out _, out _, out _), "marks: the ring keeps the last three");
        ring.Close(a, 5000);
        ring.Get(0, out text, out _, out _, out spent);
        Check(text == "late" && spent < 0, "marks: the number of a piece that has left the ring closes nothing");
        ring.Close(0, 5000);
        ring.Close(-3, 5000);
        ring.Close(late + 30, 5000);
        ring.Get(0, out _, out _, out _, out spent);
        Check(spent < 0, "marks: a number that was never given closes nothing");
        Check(!ring.Get(-1, out _, out _, out _, out _), "marks: there is nothing before the newest");

        // A piece nobody closes stays "not timed" and adds nothing to its frame.
        Check(ring.SpentIn(12) == 0, "marks: a piece nobody closed adds no time to its frame");

        // Many pieces: the numbers never collide and the ring stays three long.
        var many = new MarkRing();
        bool right = true;
        for (int i = 1; i <= 10000; i++)
        {
            int serial = many.Add("p", i, i, i * 10L);
            many.Close(serial, i * 10L + i);
            many.Get(0, out _, out int f, out _, out long s);
            right &= serial == i && f == i && s == i && many.Count == Math.Min(i, MarkRing.Kept);
        }
        Check(right, "marks: ten thousand pieces, each with its own number, frame and time");

        Stopwatch watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000000; i++)
        {
            many.Close(many.Add("p", i, i, i), i + 1);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        watch.Stop();
        Check(allocated == 0 && watch.ElapsedMilliseconds < 500, "marks: a million pieces named and closed allocate nothing (" + allocated + " bytes, " + watch.ElapsedMilliseconds + " ms)");
    }

    private static void Counts()
    {
        var ring = new CountRing(4);
        Check(ring.Total == 0 && ring.Between(0f, 100f) == 0, "cuts: nothing noted, nothing counted");
        ring.Note(1f, 5);
        ring.Note(1.5f, 1);
        ring.Note(3f, 2);
        Check(ring.Between(1f, 1.5f) == 6 && ring.Between(1.01f, 3f) == 3 && ring.Between(0f, 0.99f) == 0 && ring.Between(3.01f, 9f) == 0, "cuts: what was noted between two times, both ends counted");
        ring.Note(4f, 0);
        ring.Note(4f, -2);
        Check(ring.Total == 8 && ring.Between(4f, 4f) == 0, "cuts: a note of nothing is no note");
        ring.Note(5f, 1);
        ring.Note(6f, 1);
        Check(ring.Total == 10 && ring.Between(0f, 100f) == 5 && ring.Between(1f, 1f) == 0, "cuts: the ring forgets the oldest notes; the total does not (" + ring.Between(0f, 100f) + " of " + ring.Total + ")");
        var one = new CountRing(0);
        one.Note(1f, 3);
        one.Note(2f, 4);
        Check(one.Between(0f, 9f) == 4 && one.Total == 7, "cuts: a ring asked to keep nothing keeps one note");
        Check(ring.Between(9f, 1f) == 0, "cuts: a stretch that ends before it begins holds nothing");
    }
}
