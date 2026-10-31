using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Nav;

// Checks for the work of the first minute after a save loads (Core/WorkSlice.cs, Core/InputPrint.cs, Core/RouteSearch.cs and
// Navigation/NavModel.cs and NavBaseRules.cs, compiled as they ship): what a frame may do of work spread over frames (a budget
// of milliseconds kept, the first item always done, a heavy item only alone, all of the work done in the end, the same count
// for the same budget, nothing allocated); the print a minimap picture is kept by across loads (the same inputs the same
// print, any change another, and the kept city read back from its file giving the print it was drawn with); and the city's
// one road graph searched by several (the GPS and the bus network): a search of its own over the shared roads finds the
// same routes as a graph of its own, its own edge costs changed leave the roads' alone, and a search made a slice at a time
// and held to a number of steps in all decides exactly as one made whole (the bus network's searches in play).
//
// What is not here: how long the work takes in the game, which the "Session: a frame of ..." lines name, and that the work
// is behind the loading screen, which PrewarmCheck.cs holds over the sources.
internal static partial class Program
{
    private static void LoadPace()
    {
        SliceBudget();
        SliceTiming();
        SliceAllocation();
        Prints();
        SharedRoads();
        SlicedSeeks();
        PicturesAlike();
    }

    // The slice's rules: the first item always, a heavy item alone, an end that holds, all done with an endless one.
    private static void SliceBudget()
    {
        WorkSlice none = WorkSlice.Of(0.0, 16);
        int done = 0;
        while (done < 100 && none.More())
        {
            done++;
        }
        Check(done == 1 && none.Over && !none.More(), "slice: with no time at all exactly one item is done (" + done + "), and none after it");

        WorkSlice whole = WorkSlice.Whole;
        done = 0;
        while (done < 100000 && whole.More())
        {
            done++;
        }
        Check(done == 100000 && !whole.Over, "slice: an endless slice does all of the work");

        WorkSlice heavy = WorkSlice.Of(1000.0, 1);
        Check(heavy.More(true), "slice: a heavy item is done when it is the first of the slice");
        Check(!heavy.More(true) && heavy.Over, "slice: a heavy item is not done after another");
        WorkSlice light = WorkSlice.Of(1000.0, 1);
        Check(light.More() && light.More() && !light.More(true), "slice: light items go on within the budget, a heavy one ends the slice");

        WorkSlice ended = WorkSlice.Of(1000.0, 1);
        ended.More();
        ended.End();
        Check(!ended.More() && ended.Over && ended.Done == 1, "slice: ended, it gives no more");

        // All of a piece of work is done, slice after slice, whatever the budget: the same count for the same work.
        int[] budgets = { 0, 1 };
        foreach (int ms in budgets)
        {
            int total = 0, slices = 0;
            while (total < 5000 && slices < 100000)
            {
                WorkSlice slice = WorkSlice.Of(ms, 8);
                while (total < 5000 && slice.More())
                {
                    total++;
                }
                slices++;
            }
            Check(total == 5000 && (ms > 0 || slices == 5000), "slice: 5000 items all done in slices of " + ms + " ms (" + slices + " slices)");
        }
    }

    // The budget is kept: items of a tenth of a millisecond each, a slice of one millisecond looked at every item, ends within one item of it.
    private static void SliceTiming()
    {
        double worst = 0.0;
        int least = int.MaxValue;
        for (int run = 0; run < 20; run++)
        {
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(1.0, 1);
            int items = 0;
            while (items < 1000 && slice.More())
            {
                Spin(0.1);
                items++;
            }
            double took = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            worst = Math.Max(worst, took);
            least = Math.Min(least, items);
        }
        Check(worst < 1.6 && least >= 5, "slice: a budget of 1 ms with items of 0.1 ms ends within an item of it (at worst " + worst.ToString("F2") + " ms, at least " + least + " items)");
    }

    // Nothing is allocated: a slice is a struct, made a frame at a time.
    private static void SliceAllocation()
    {
        Slices();   // (once to have it compiled)
        long allocated = Slices();
        Check(allocated == 0, "slice: a million slices and their items allocate nothing (" + allocated + " bytes)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long Slices()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 1000000; i++)
        {
            WorkSlice slice = WorkSlice.Of(i % 2 == 0 ? 0.0 : 0.001, 4);
            while (slice.More(i % 7 == 0))
            {
                sum++;
                if (slice.Done > 2)
                {
                    slice.End();
                }
            }
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return sum > 0 ? allocated : -1;
    }

    // The print: the same inputs the same print; any value changed, an array longer or shorter, or two values moved, another.
    private static void Prints()
    {
        float[] x = { 1f, 2f, 3f };
        int[] e = { 0, 2, 5 };
        bool[] u = { true, false, true };
        ulong a = Print(x, e, u, 7);
        Check(a == Print(new[] { 1f, 2f, 3f }, new[] { 0, 2, 5 }, new[] { true, false, true }, 7), "print: the same inputs give the same print");
        Check(a != Print(new[] { 1f, 2f, 3.0001f }, e, u, 7), "print: one coordinate a little different gives another print");
        Check(a != Print(x, new[] { 0, 2, 6 }, u, 7), "print: one edge different gives another");
        Check(a != Print(x, e, new[] { true, true, true }, 7), "print: one point usable or not gives another");
        Check(a != Print(x, e, u, 8), "print: the size of the picture gives another");
        Check(a != Print(new[] { 2f, 1f, 3f }, e, u, 7), "print: two values moved give another");
        Check(a != Print(new[] { 1f, 2f }, e, u, 7) && a != Print(new[] { 1f, 2f, 3f, 0f }, e, u, 7), "print: an array shorter or longer gives another");
        Check(Print(null, e, u, 7) != Print(new float[0], e, u, 7), "print: no array and an empty one differ");
        var empty = new InputPrint();
        Check(empty.Value == 14695981039346656037UL, "print: the print of nothing is FNV-1a's offset");
        var bytes = new InputPrint();
        bytes.Add(new byte[] { 1, 2, 3 });
        var other = new InputPrint();
        other.Add(new byte[] { 1, 2, 4 });
        Check(bytes.Value != other.Value, "print: one byte different gives another");
    }

    private static ulong Print(float[] x, int[] edges, bool[] usable, int width)
    {
        var print = new InputPrint();
        print.Add(width);
        print.Add(x);
        print.Add(edges);
        print.Add(usable);
        return print.Value;
    }

    // The city's one graph searched by several: a search of its own over the shared roads (RouteSearch.Over) finds the route a
    // graph of its own finds, for every pair tried; its own costs changed (the bus network's corridors) leave the roads' alone.
    private static void SharedRoads()
    {
        var town = new Town(6);
        RouteSearch roads = town.Drive;
        RouteSearch own = CopyOf(roads);
        RouteSearch shared = RouteSearch.Over(roads, true);
        Check(ReferenceEquals(shared.X, roads.X) && ReferenceEquals(shared.EdgeStart, roads.EdgeStart) && ReferenceEquals(shared.EdgeTo, roads.EdgeTo),
            "shared roads: the places and the edges are the roads' own, not copies");
        Check(!ReferenceEquals(shared.EdgeExtra, roads.EdgeExtra) && Alike(shared.EdgeExtra, roads.EdgeExtra), "shared roads: with costs of its own, a copy of the roads' costs");
        Check(ReferenceEquals(RouteSearch.Over(roads, false).EdgeExtra, roads.EdgeExtra), "shared roads: without, the roads' costs themselves");

        var a = new List<int>();
        var b = new List<int>();
        var rng = new Random(20261014);
        int same = 0, tried = 0;
        for (int i = 0; i < 300; i++)
        {
            int from = rng.Next(roads.Count), to = rng.Next(roads.Count);
            bool fa = own.Find(new[] { from }, new[] { to }, roads.X[to], roads.Y[to], roads.Z[to], 0f, 400000, a);
            bool fb = shared.Find(new[] { from }, new[] { to }, roads.X[to], roads.Y[to], roads.Z[to], 0f, 400000, b);
            tried++;
            same += fa == fb && Alike(a, b) && own.Expanded == shared.Expanded ? 1 : 0;
        }
        Check(same == tried, "shared roads: " + same + " of " + tried + " routes the same as over a graph of its own, step for step");

        // The bus network counts a corridor's edges for less on its own costs: the GPS's roads are untouched, and both searches still run.
        float[] before = (float[])roads.EdgeExtra.Clone();
        for (int k = 0; k < shared.EdgeExtra.Length; k += 3)
        {
            shared.EdgeExtra[k] = -0.2f;
        }
        Check(Alike(roads.EdgeExtra, before), "shared roads: costs changed on a search of its own leave the roads' costs as they were");
        int from2 = town.Mid(0, 0, 1, 0), to2 = town.Mid(4, 5, 5, 5);
        bool found = roads.Find(new[] { from2 }, new[] { to2 }, roads.X[to2], 0f, roads.Z[to2], 0f, 1000000, a)
                     && own.Find(new[] { from2 }, new[] { to2 }, roads.X[to2], 0f, roads.Z[to2], 0f, 1000000, b);
        Check(found && Alike(a, b), "shared roads: the roads' own search still finds what a graph of its own finds after that");
    }

    private static RouteSearch CopyOf(RouteSearch roads)
    {
        var copy = new RouteSearch(roads.Count);
        Array.Copy(roads.X, copy.X, roads.Count);
        Array.Copy(roads.Y, copy.Y, roads.Count);
        Array.Copy(roads.Z, copy.Z, roads.Count);
        copy.SetEdges((int[])roads.EdgeStart.Clone(), (int[])roads.EdgeTo.Clone(), (float[])roads.EdgeExtra.Clone());
        return copy;
    }

    // The bus network's searches in play: begun, advanced 64 steps at a time while the slice allows, held to a number of steps in
    // all and given up there (TransitMap.Seek). The outcome, the route, the steps, the limit and what was reached are those of
    // RouteSearch.Find made whole with that limit, for every pair and every limit tried.
    private static void SlicedSeeks()
    {
        var town = new Town(7, (ax, az, bx, bz) => (ax + bz) % 3 == 0);
        RouteSearch whole = CopyOf(town.Drive);
        RouteSearch sliced = CopyOf(town.Drive);
        var a = new List<int>();
        var b = new List<int>();
        var rng = new Random(7);
        int[] limits = { 1, 63, 64, 65, 200, 1000, 400000 };
        int same = 0, tried = 0, limited = 0;
        for (int i = 0; i < 120; i++)
        {
            int from = rng.Next(whole.Count), to = rng.Next(whole.Count);
            int limit = limits[i % limits.Length];
            var starts = new[] { from };
            var goals = new[] { to };
            bool fa = whole.Find(starts, goals, whole.X[to], 0f, whole.Z[to], 2f, limit, a);
            bool fb = Seek(sliced, starts, goals, to, limit, b, out int slices);
            bool reachedSame = true;
            for (int n = 0; n < whole.Count && reachedSame; n++)
            {
                reachedSame = whole.WasReached(n) == sliced.WasReached(n);
            }
            tried++;
            limited += whole.HitLimit ? 1 : 0;
            same += fa == fb && Alike(a, b) && whole.HitLimit == sliced.HitLimit && whole.Expanded == sliced.Expanded && reachedSame ? 1 : 0;
        }
        Check(same == tried && limited > 0, "sliced searches: " + same + " of " + tried + " decided exactly as a search made whole (" + limited + " of them at their limit)");
    }

    private static bool Seek(RouteSearch search, IList<int> starts, IList<int> goals, int to, int limit, List<int> path, out int slices)
    {
        slices = 0;
        path.Clear();
        if (!search.Begin(starts, goals, search.X[to], 0f, search.Z[to], 2f))
        {
            return false;
        }
        RouteSearch.Outcome outcome;
        do
        {
            slices++;
            WorkSlice slice = WorkSlice.Of(0.0, 1);   // one chunk of 64 steps a slice: the most slices there can be
            do
            {
                outcome = search.Advance(Math.Min(search.Expanded + 64, limit), path);
            }
            while (outcome == RouteSearch.Outcome.Searching && search.Expanded < limit && slice.More());
        }
        while (outcome == RouteSearch.Outcome.Searching && search.Expanded < limit);
        if (outcome == RouteSearch.Outcome.Searching)
        {
            search.GiveUp();
        }
        return outcome == RouteSearch.Outcome.Found;
    }

    // A kept picture is the picture: the road picture and the buildings drawn twice from the same inputs are the same bytes,
    // and the kept city read back from its file gives the print it was drawn with (so the next load finds the picture kept).
    private static void PicturesAlike()
    {
        var town = new Town(3);
        int w = 280, h = 280;
        var one = new byte[w * h];
        var two = new byte[w * h];
        NavRaster.Draw(one, w, h, -40f, -40f, 1f, 2.3f, town.Drive.X, town.Drive.Z, town.Drive.EdgeStart, town.Drive.EdgeTo, town.Drive.EdgeExtra, town.Usable, town.Drive.Count);
        NavRaster.Draw(two, w, h, -40f, -40f, 1f, 2.3f, town.Drive.X, town.Drive.Z, town.Drive.EdgeStart, town.Drive.EdgeTo, town.Drive.EdgeExtra, town.Usable, town.Drive.Count);
        Check(Alike(one, two), "kept pictures: the road picture drawn twice from the same roads is the same, byte for byte");

        NavBaseData data = BaseData();
        var first = new InputPrint();
        data.AddTo(ref first);
        byte[] file = data.Encode();
        NavBaseData read = NavBaseData.Decode(file, data.Build, data.OriginX, data.OriginZ, data.WorldWidth, data.WorldHeight);
        var again = new InputPrint();
        if (read != null)
        {
            read.AddTo(ref again);
        }
        Check(read != null && first.Value == again.Value, "kept pictures: the city read back from its file gives the print it was drawn with");
        var cover1 = new byte[200 * 120];
        var cover2 = new byte[200 * 120];
        int drawn1 = NavBaseRaster.Buildings(cover1, 200, 120, data.OriginX, data.OriginZ, 1f, data);
        int drawn2 = NavBaseRaster.Buildings(cover2, 200, 120, read != null ? read.OriginX : 0f, read != null ? read.OriginZ : 0f, 1f, read ?? data);
        Check(drawn1 > 0 && drawn1 == drawn2 && Alike(cover1, cover2), "kept pictures: the buildings drawn from the file are those drawn from what was measured");
        data.Cells[3] = (byte)(data.Cells[3] == NavBaseWater.Water ? NavBaseWater.Land : NavBaseWater.Water);
        var changed = new InputPrint();
        data.AddTo(ref changed);
        Check(changed.Value != first.Value, "kept pictures: one square of water more or less gives another print (the picture is drawn again)");
    }

    // A small measured city: two square buildings and a grid of land with a strip of water.
    private static NavBaseData BaseData()
    {
        var data = new NavBaseData
        {
            Build = 3682, OriginX = -10f, OriginZ = -10f, WorldWidth = 200f, WorldHeight = 120f, Cell = 8f,
            Columns = 25, Rows = 15, Hulls = 2,
            HullStart = new[] { 0, 4, 8 },
            HullKey = new[] { 11L, 22L },
            HullX = new[] { 10f, 30f, 30f, 10f, 60f, 90f, 90f, 60f },
            HullZ = new[] { 10f, 10f, 30f, 30f, 40f, 40f, 70f, 70f },
        };
        data.Cells = new byte[data.Columns * data.Rows];
        for (int i = 0; i < data.Cells.Length; i++)
        {
            data.Cells[i] = i % data.Columns > 20 ? NavBaseWater.Water : NavBaseWater.Land;
        }
        return data;
    }

    private static bool Alike<T>(IList<T> a, IList<T> b)
    {
        if (a == null || b == null || a.Count != b.Count)
        {
            return a == null && b == null;
        }
        var equal = EqualityComparer<T>.Default;
        for (int i = 0; i < a.Count; i++)
        {
            if (!equal.Equals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }
}
