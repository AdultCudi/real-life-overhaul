using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;

// Checks for keeping the game's culling lists free of what the mod destroys (Core/SceneryCullRules.cs, used by
// Core/SceneryCulling; docs/standards/performance.md, RLO-PERF-023): the list compacted in one pass, the order of the rest
// kept and the culling group's spheres moved in step with it (the group reads sphere i for entry i, so a sphere left
// behind would hand an entry another's distance band), each entry taken out handed over once, a group not made yet
// left alone, nothing allocated and a city's list done well inside a frame; and the line of the check made as a city
// unloads. Compiled as it ships.
internal static partial class Program
{
    private static void SceneryCulls()
    {
        SceneryCullCompaction();
        SceneryCullEdges();
        SceneryCullCost();
        SceneryCullLine();
    }

    private sealed class Listed
    {
        public readonly int Id;
        public readonly bool Gone;

        public Listed(int id, bool gone)
        {
            Id = id;
            Gone = gone;
        }
    }

    private static readonly Predicate<Listed> ListedGone = l => l.Gone;

    // A list of n entries, those whose index the test says gone, with a sphere each that carries its entry's id.
    private static List<Listed> CullList(int n, Func<int, bool> gone, out int[] spheres, int extra = 8)
    {
        var list = new List<Listed>(n);
        spheres = new int[n + extra];
        for (int i = 0; i < n; i++)
        {
            list.Add(new Listed(i, gone(i)));
            spheres[i] = i;
        }
        for (int i = n; i < spheres.Length; i++)
        {
            spheres[i] = -7;   // the group's array is longer than its count: what lies past the count is never read
        }
        return list;
    }

    // Every entry kept, in order, with its own sphere at its index, none of the gone ones left.
    private static bool InStep(List<Listed> list, int[] spheres, out string why)
    {
        why = null;
        int last = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Gone)
            {
                why = "a gone entry is still listed at " + i;
                return false;
            }
            if (list[i].Id <= last)
            {
                why = "the order changed at " + i;
                return false;
            }
            last = list[i].Id;
            if (spheres != null && spheres[i] != list[i].Id)
            {
                why = "entry " + list[i].Id + " at " + i + " reads the sphere of " + spheres[i];
                return false;
            }
        }
        return true;
    }

    private static void SceneryCullCompaction()
    {
        (string Name, int N, Func<int, bool> Gone)[] cases =
        {
            ("none gone", 40, i => false),
            ("the first gone", 40, i => i == 0),
            ("the last gone", 40, i => i == 39),
            ("one in the middle gone", 40, i => i == 17),
            ("a run in the middle gone", 40, i => i >= 10 && i < 22),
            ("every other gone", 41, i => i % 2 == 0),
            ("all but the last gone", 40, i => i < 39),
            ("all gone", 40, i => true),
            ("a city's art gone at the end of the list", 12000, i => i >= 11850),
            ("a city's art scattered through the list", 12000, i => i % 97 == 5),
        };
        foreach ((string name, int n, Func<int, bool> gone) in cases)
        {
            List<Listed> list = CullList(n, gone, out int[] spheres);
            int expected = 0;
            for (int i = 0; i < n; i++)
            {
                expected += gone(i) ? 1 : 0;
            }
            var dropped = new List<int>();
            int taken = SceneryCullRules.Compact(list, spheres, ListedGone, l => dropped.Add(l.Id));
            Check(taken == expected && list.Count == n - expected, "scenery culling (" + name + "): " + expected + " taken out, " + (n - expected) + " left, not "
                                                                   + taken + " and " + list.Count);
            Check(InStep(list, spheres, out string why), "scenery culling (" + name + "): the rest in order, each with its own sphere: " + why);
            bool order = dropped.Count == expected;
            for (int k = 1; k < dropped.Count && order; k++)
            {
                order = dropped[k] > dropped[k - 1];
            }
            Check(order, "scenery culling (" + name + "): each entry taken out handed over once, in the list's order (" + dropped.Count + " of " + expected + ")");
            bool tail = true;
            for (int i = n; i < spheres.Length; i++)
            {
                tail &= spheres[i] == -7;
            }
            Check(tail, "scenery culling (" + name + "): nothing written past the list's old count");
        }
    }

    private static void SceneryCullEdges()
    {
        // The game's group not made yet: the list alone.
        List<Listed> list = CullList(30, i => i % 3 == 1, out _);
        int taken = SceneryCullRules.Compact<Listed, int>(list, null, ListedGone, null);
        bool inStep = InStep(list, null, out string why);
        Check(taken == 10 && list.Count == 20 && inStep, "scenery culling: with no group made yet the list alone is compacted: " + why);

        // An array shorter than the list (never so in the game: its array grows with the list) is not read past its end.
        list = CullList(50, i => i < 5, out _);
        int[] shortSpheres = new int[20];
        for (int i = 0; i < shortSpheres.Length; i++)
        {
            shortSpheres[i] = i;
        }
        bool threw = false;
        try
        {
            taken = SceneryCullRules.Compact(list, shortSpheres, ListedGone, null);
        }
        catch (IndexOutOfRangeException)
        {
            threw = true;
        }
        Check(!threw && taken == 5 && list.Count == 45, "scenery culling: an array of spheres shorter than the list is never read past its end");

        // Empty, null and no test at all.
        list = new List<Listed>();
        Check(SceneryCullRules.Compact(list, new int[4], ListedGone, null) == 0 && list.Count == 0, "scenery culling: an empty list stays empty");
        Check(SceneryCullRules.Compact<Listed, int>(null, null, ListedGone, null) == 0, "scenery culling: no list is nothing to do");
        list = CullList(6, i => true, out int[] spheres);
        Check(SceneryCullRules.Compact(list, spheres, null, null) == 0 && list.Count == 6, "scenery culling: with no test of what is gone nothing is taken out");

        // Twice: a second pass over a compacted list takes nothing more.
        list = CullList(100, i => i % 10 == 0, out spheres);
        SceneryCullRules.Compact(list, spheres, ListedGone, null);
        int again = SceneryCullRules.Compact(list, spheres, ListedGone, null);
        inStep = InStep(list, spheres, out why);
        Check(again == 0 && list.Count == 90 && inStep,
            "scenery culling: a second pass takes nothing more and moves nothing: " + why);
    }

    // A city's list (some twelve thousand entries: trees, lamps, benches, signs) compacted in well under a millisecond or
    // two and nothing allocated, as it runs on every destroy of a copy that holds a cullable.
    private static void SceneryCullCost()
    {
        List<Listed> template = CullList(12000, i => i % 50 == 3, out int[] spheres);
        var work = new List<Listed>(template.Count);
        Action<Listed> none = l => { };
        double best = double.MaxValue;
        long allocated = long.MaxValue;
        for (int run = 0; run < 7; run++)
        {
            work.Clear();
            work.AddRange(template);
            for (int i = 0; i < template.Count; i++)
            {
                spheres[i] = i;
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            SceneryCullRules.Compact(work, spheres, ListedGone, none);
            long end = Stopwatch.GetTimestamp();
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            best = Math.Min(best, (end - start) * 1000.0 / Stopwatch.Frequency);
        }
        Check(allocated == 0, "scenery culling: a pass over a city's list allocates nothing (" + allocated + " bytes)");
        Check(best < 2.0, "scenery culling: a pass over a city's 12,000 entries under 2 ms (" + best.ToString("F3") + " ms, the best of 7)");
    }

    private static void SceneryCullLine()
    {
        string pass = SceneryCullRules.UnloadLine(0, 11873, false, 412, 0);
        Check(pass.StartsWith("Culling check (unload): 0 dead cullables", StringComparison.Ordinal) && pass.EndsWith(": PASS", StringComparison.Ordinal),
            "scenery culling: none dead is 'Culling check (unload): 0 dead cullables ...: PASS': " + pass);
        Check(pass.Contains("11873 listed") && pass.Contains("412 taken out") && pass.Contains("not asked for"),
            "scenery culling: the line says how many are listed, how many the mod took out and whether a rebuild is asked for: " + pass);
        string fail = SceneryCullRules.UnloadLine(21, 11894, true, 0, 0);
        Check(fail.StartsWith("Culling check (unload): 21 dead cullables", StringComparison.Ordinal) && fail.EndsWith(": FAIL", StringComparison.Ordinal) && fail.Contains("asked for"),
            "scenery culling: dead entries left fail the check: " + fail);
        string byGame = SceneryCullRules.UnloadLine(0, 100, false, 9, 9);
        Check(byGame.Contains("9 through the game's own way"), "scenery culling: entries taken out the game's own way are said: " + byGame);
        string unread = SceneryCullRules.UnloadLine(-1, 0, false, 3, 0);
        Check(unread.Contains("not checked") && !unread.Contains("PASS") && !unread.Contains("FAIL"), "scenery culling: lists that could not be read are not judged: " + unread);
        Check(!SceneryCullRules.UnloadPasses(1) && SceneryCullRules.UnloadPasses(0), "scenery culling: the check passes on none dead only");
    }
}
