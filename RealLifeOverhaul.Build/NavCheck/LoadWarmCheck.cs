using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul;

// Checks for a job's code compiled behind the loading screen (Core/LoadWarm.cs over CodeWarm.RootAll, compiled as it ships).
// The owner's log of 2 Nov 2026: the city drivers' first look 7.2 ms and the traffic's own 6.9 ms for one car, against some
// 0.1 ms a look once compiled; the bare ground check's first step 3.4 ms; the office count 8.5 ms; the far squares' survey's
// slowest tick 3.9 ms: Mono compiling each method the first time it is called, in a frame of play. Here, on a made-up job of
// this checker's own: every method of the job's type is taken in, a path it takes only rarely, a closure, a coroutine nobody
// started and a type nested in it among them, with what they call down to the depth; nothing of it is run; a static
// constructor and a type of another module are left alone; a second load takes nothing new; and, broken on purpose, a walk
// from the job's one entry (as DayWarm walks from a handler) misses the rare path, the coroutine and the nested type, which
// is why the job's types are rooted whole. Over the sources: the street looks, the office count, the far squares' survey and
// the bare ground check hand their code over, and the warm runs whole only behind the loading screen.
internal static partial class Program
{
    private const int LoadDeepest = 3;   // Core/LoadWarm.Deepest, which the sources check below holds to this

    private static void LoadWarms()
    {
        LoadWarmReach();
        LoadWarmAgain();
        LoadWarmSources();
    }

    private static List<MethodBase> LoadWalk(Action<CodeWarm> roots, int deepest)
    {
        var order = new List<MethodBase>();
        var warm = new CodeWarm(WarmModule, 10000, deepest, m =>
        {
            order.Add(m);
            return true;
        });
        roots(warm);
        WorkSlice whole = WorkSlice.Whole;
        while (!warm.Run(ref whole))
        {
            whole = WorkSlice.Whole;
        }
        return order;
    }

    private static bool HasIn(List<MethodBase> order, string typeStarts, string name)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].DeclaringType != null && order[i].DeclaringType.Name.StartsWith(typeStarts, StringComparison.Ordinal) && order[i].Name == name)
            {
                return true;
            }
        }
        return false;
    }

    private static void LoadWarmReach()
    {
        int runsBefore = WarmJob.Runs;
        List<MethodBase> order = LoadWalk(w => w.RootAll(typeof(WarmJob)), LoadDeepest);
        Check(Has(order, typeof(WarmJob), nameof(WarmJob.Look)) && Has(order, typeof(WarmJob), "Car"),
            "load warm: the job's entry and what it calls each look are taken in (" + order.Count + " methods)");
        Check(Has(order, typeof(WarmJob), "Rare") && Has(order, typeof(WarmJobHelp), nameof(WarmJobHelp.OnlyRare)) && Has(order, typeof(WarmJobHelp), "Further"),
            "load warm: a path the job takes rarely (a first hold line) is taken in, with what it calls two calls down");
        Check(HasIn(order, "<>c__DisplayClass", "<Rare>b__0") || HasIn(order, "<>c", "<Rare>b__"), "load warm: a closure the job makes is taken in");
        Check(HasIn(order, "<Steps>", "MoveNext"), "load warm: a coroutine of the job's that nothing has started has its MoveNext taken in");
        Check(HasIn(order, "Inner", "Twice"), "load warm: a type nested in the job's is taken in whole");
        Check(!Has(order, typeof(WarmJobHelp), nameof(WarmJobHelp.Unused)), "load warm: a method of another type that the job never calls is not taken in");
        bool cctor = false;
        foreach (MethodBase m in order)
        {
            cctor |= m.IsConstructor && m.IsStatic;
        }
        Check(!cctor, "load warm: a static constructor is left to the runtime, never asked for");
        Check(WarmJob.Runs == runsBefore, "load warm: nothing of the job is run by its warm (" + WarmJob.Runs + " runs)");
        var seen = new HashSet<MethodBase>();
        bool once = true;
        foreach (MethodBase m in order)
        {
            once &= seen.Add(m);
        }
        Check(once, "load warm: every method is compiled once (" + order.Count + " taken in)");
        List<MethodBase> other = LoadWalk(w => w.RootAll(typeof(string)), LoadDeepest);
        Check(other.Count == 0, "load warm: a type of another module is left alone (" + other.Count + " taken in)");

        // Broken on purpose: the job walked from its one entry, as DayWarm walks from a handler, misses what that entry does not call.
        List<MethodBase> entry = LoadWalk(w => w.Root(typeof(WarmJob).GetMethod(nameof(WarmJob.Look))), LoadDeepest);
        bool misses = !HasIn(entry, "<Steps>", "MoveNext") && !HasIn(entry, "Inner", "Twice");
        Check(misses && Has(entry, typeof(WarmJob), "Car"),
            "load warm, fails on purpose: from the job's entry alone, the coroutine nobody started and the nested type are missed (" + entry.Count + " against " + order.Count + " methods)");
        List<MethodBase> shallow = LoadWalk(w => w.RootAll(typeof(WarmJob)), 1);
        Check(!Has(shallow, typeof(WarmJobHelp), "Further") && Has(order, typeof(WarmJobHelp), "Further"),
            "load warm, fails on purpose: at a depth of one, a method two calls down from the job is missed");
    }

    // Compiled code lasts the run: the next load's warm over the same types takes nothing new.
    private static void LoadWarmAgain()
    {
        int compiled = 0;
        var warm = new CodeWarm(WarmModule, 10000, LoadDeepest, m =>
        {
            compiled++;
            return true;
        });
        warm.RootAll(typeof(WarmJob));
        WorkSlice first = WorkSlice.Whole;
        while (!warm.Run(ref first))
        {
            first = WorkSlice.Whole;
        }
        int once = compiled;
        warm.RootAll(typeof(WarmJob));
        WorkSlice second = WorkSlice.Whole;
        bool done = warm.Run(ref second);
        Check(once > 8 && compiled == once && done, "load warm: a second load's warm over the same types takes nothing new (" + once + " then " + compiled + ")");
    }

    // Over the sources: who hands what over, and the warm whole only behind the loading screen.
    private static void LoadWarmSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string load = Read(Path.Combine(scripts, @"Core\LoadWarm.cs"));
        Check(load.Contains("internal const int Deepest = " + LoadDeepest + ";") && load.Contains("_warm.RootAll(types[i]);") && load.Contains("ScenePrewarm.Add(what, "),
            "load warm: the job's types rooted whole at a depth of " + LoadDeepest + ", as a piece of the loading screen's work");
        Check(load.Contains("bool loading = UI.Load.LoadScene.isLoading || ScenePrewarm.InLate;") && load.Contains("WorkSlice.Of(SliceMs, 1)") && load.Contains("ScenePrewarm.NextFrame();"),
            "load warm: whole only behind the loading screen; behind a black screen in play, a slice of a millisecond a frame");
        (string file, string[] words, string what)[] handed =
        {
            (@"Traffic\Drivers\CityDrivers.cs", new[] { "LoadWarm.Add(\"traffic: the city drivers' look compiled\",",
                "new[] { typeof(CityDrivers), typeof(DriverGame), typeof(DriverBrakes), typeof(DriverRules), typeof(TrafficTools) }" }, "the city drivers' look"),
            (@"Traffic\Solid\SolidStreets.cs", new[] { "LoadWarm.Add(\"traffic: the look for people in the road compiled\",",
                "new[] { typeof(SolidStreets), typeof(SolidGame), typeof(SolidRules), typeof(SolidBrakes) }" }, "the traffic's look for people in the road"),
            (@"Tech\Sites\TechSites.cs", new[] { "LoadWarm.Add(\"tech sites: the office count compiled\", new[] { typeof(OfficeCensus), typeof(OfficeCount), typeof(OfficeTally) },",
                "typeof(TechSites), nameof(Count), nameof(Census));" }, "the office count"),
            (@"Performance\Tweaks\FarDistricts.cs", new[] { "LoadWarm.Now(\"performance: the far squares' survey compiled\", new[] { typeof(FarDistricts) });" }, "the far squares' survey"),
            (@"Construction\ConstructionDirector.BareGround.cs", new[] { "LoadWarm.Add(", "nameof(BareGroundFrame), nameof(BareGroundSaid), nameof(FactsOf));" },
                "the bare ground check and a column's facts"),
        };
        foreach ((string file, string[] words, string what) in handed)
        {
            string text = Read(Path.Combine(scripts, file));
            bool all = text.Length > 0;
            foreach (string word in words)
            {
                all &= text.Contains(word);
            }
            Check(all, "load warm: " + what + " handed to the loading screen (" + file + ")");
        }
        Check(Read(Path.Combine(scripts, @"Construction\ConstructionDirector.cs")).Contains("BareGroundWarm();"), "load warm: the construction director hands its check over as it wakes");
    }
}

// A made-up job of the street's kind: what the load's warm must and must not reach. Nothing here is ever called by the checks.
internal sealed class WarmJob
{
    public static int Runs;                          // counts every run of the job's code: the warm must leave it as it was
    private static readonly int[] Table = Build();   // a static constructor, which the warm leaves alone
    private int _n;

    public int Look(int cars)
    {
        Runs++;
        int sum = 0;
        for (int i = 0; i < cars; i++)
        {
            sum += Car(i);
        }
        if (cars > 1000)
        {
            sum += Rare(cars);   // taken rarely: never on the day a walk from here would be read
        }
        return sum;
    }

    private int Car(int i)
    {
        Runs++;
        return i * 2 + Table[i & 3];
    }

    private int Rare(int n)
    {
        Runs++;
        int k = n & 7;
        Func<int, int> f = x => x + k + _n;
        return f(n) + WarmJobHelp.OnlyRare(n);
    }

    private IEnumerator Steps()
    {
        Runs++;
        yield return _n;
        _n++;
    }

    private static int[] Build() => new[] { 1, 2, 3, 4 };

    private sealed class Inner
    {
        public int Twice(int x)
        {
            Runs++;
            return x * 2;
        }
    }
}

internal static class WarmJobHelp
{
    public static int OnlyRare(int n)
    {
        WarmJob.Runs++;
        return Further(n) + 1;
    }

    private static int Further(int n)
    {
        WarmJob.Runs++;
        return n * 3;
    }

    public static int Unused(int n) => n;
}
