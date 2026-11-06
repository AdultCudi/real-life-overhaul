using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RealLifeOverhaul;

// Checks for the mod's code for the game's midnight compiled ahead (Core/CodeWarm.cs, compiled as it ships; Core/DayWarm.cs
// drives it in play). The owner's log of 25 Oct: the mod's day took 148.7 ms in the first midnight after the load, its parts
// ten to twenty times less every midnight after for the same work, which is Mono compiling each method the first time it is
// called. Here, on a made-up day of this checker's own: every method the day's handlers reach (calls, delegates, a closure's
// handler as Core/Guard wraps it, a coroutine's MoveNext, a generic method made for a value type) is taken in once, nothing
// they cannot reach is, a method with nothing to compile is left out, the walk stops at its depth; the walk done a slice at a
// time takes in exactly what one done whole takes in, in the same order, for any budget; a slice keeps its budget and a long
// method is compiled only first in a slice; a second walk over the same handlers takes nothing again; and the day computed
// after its code is compiled ahead gives the same numbers as before (compiling changes when, never what).
//
// What is not here: that Mono compiles a method when its function pointer is asked for (measured under Unity's own Mono:
// a method's first call after it took 0.001 ms against 0.1 ms cold, and RuntimeHelpers.PrepareMethod compiles nothing), and
// what the warm costs in the game, which the "Day warm check" and "Session check (the game's midnight ...)" lines name.
internal static partial class Program
{
    private static void DayWarms()
    {
        WarmReach();
        WarmSliced();
        WarmBudget();
        WarmHeavy();
        WarmAgain();
        WarmSameDay();
        WarmSources();
    }

    private const double WarmSliceMs = 1.0;   // Core/DayWarm.SliceMs, which the sources check below holds to this

    private static Module WarmModule => typeof(WarmDay).Module;

    private static string WarmName(MethodBase m) => (m.DeclaringType != null ? m.DeclaringType.Name : "?") + "." + m.Name;

    // The walk over the made-up day, compiling nothing: the order methods were taken in.
    private static List<MethodBase> WarmWalk(double sliceMs, int deepest, out int slices, int heavyIl = CodeWarm.HeavyIl, List<int> sliceOf = null)
    {
        var order = new List<MethodBase>();
        int slice = 0;
        var warm = new CodeWarm(WarmModule, 10000, deepest, m =>
        {
            order.Add(m);
            sliceOf?.Add(slice);
            return true;
        }, heavyIl);
        Action handler = WarmDay.OnDay;
        warm.Root(WarmDay.Wrap(handler));   // as Core/Guard wraps a handler: the walk finds the handler inside the closure
        slices = 0;
        while (slices < 100000)
        {
            WorkSlice s = sliceMs < 0 ? WorkSlice.Whole : WorkSlice.Of(sliceMs, 1);
            slices++;
            slice = slices;
            if (warm.Run(ref s))
            {
                break;
            }
        }
        return order;
    }

    private static bool Has(List<MethodBase> order, Type type, string name)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].DeclaringType == type && order[i].Name == name)
            {
                return true;
            }
        }
        return false;
    }

    private static void WarmReach()
    {
        List<MethodBase> order = WarmWalk(-1.0, 99, out _);
        Check(Has(order, typeof(WarmDay), nameof(WarmDay.OnDay)), "day warm: the handler inside the guard's closure is taken in");
        Check(Has(order, typeof(WarmDay), "Settle"), "day warm: a method the handler calls is taken in");
        Check(Has(order, typeof(WarmDay), "Deep3"), "day warm: a method three calls down is taken in");
        Check(Has(order, typeof(WarmDay), "Shared"), "day warm: a method only a delegate made in the day calls is taken in");
        Check(Has(order, typeof(WarmDay), "Cased"), "day warm: a method called past a switch is taken in (the switch's targets read as its operand)");
        bool moveNext = false, generic = false, built = false;
        for (int i = 0; i < order.Count; i++)
        {
            MethodBase m = order[i];
            moveNext |= m.Name == "MoveNext" && m.DeclaringType != null && m.DeclaringType.DeclaringType == typeof(WarmDay);
            generic |= m is MethodInfo g && g.IsGenericMethod && g.Name == "Pick" && g.GetGenericArguments()[0] == typeof(int);
            built |= m is ConstructorInfo && m.DeclaringType == typeof(WarmLedger);
        }
        Check(moveNext, "day warm: a coroutine the day makes has its MoveNext taken in (called through an interface)");
        Check(generic, "day warm: a generic method made for int is taken in as made");
        Check(built, "day warm: a constructor the day calls is taken in");
        Check(!Has(order, typeof(WarmDay), "Unused"), "day warm: a method nothing in the day reaches is not taken in");
        Check(!Has(order, typeof(WarmDay), nameof(WarmDay.GetTickCount)), "day warm: a native call the day makes has nothing to compile and is left out");
        Check(!Has(order, typeof(WarmShape), "Area"), "day warm: an abstract method has nothing to compile and is left out");
        var seen = new HashSet<MethodBase>();
        bool once = true;
        for (int i = 0; i < order.Count; i++)
        {
            once &= seen.Add(order[i]);
        }
        Check(once, "day warm: every method is compiled once (" + order.Count + " taken in)");

        List<MethodBase> shallow = WarmWalk(-1.0, 2, out _);
        Check(Has(shallow, typeof(WarmDay), "Deep1") && !Has(shallow, typeof(WarmDay), "Deep2") && Has(order, typeof(WarmDay), "Deep2"),
            "day warm: at a depth of two, a method two calls down is compiled and what it calls is not taken in");
    }

    // A walk a slice at a time takes in what one done whole does, in the same order, whatever the budget.
    private static void WarmSliced()
    {
        List<MethodBase> whole = WarmWalk(-1.0, 99, out int wholeSlices);
        List<MethodBase> each = WarmWalk(0.0, 99, out int eachSlices);
        List<MethodBase> ms = WarmWalk(1.0, 99, out _);
        Check(Same(whole, each) && Same(whole, ms), "day warm: done one method a slice or a millisecond a slice, the same " + whole.Count + " methods in the same order as all at once");
        Check(eachSlices == whole.Count && wholeSlices == 1, "day warm: one method a slice takes a slice each (" + eachSlices + " for " + whole.Count + "), and a whole slice one");
    }

    private static bool Same(List<MethodBase> a, List<MethodBase> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i]) && !a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    // The budget holds: each method takes a tenth of a millisecond to compile, a slice of one millisecond ends within one of it.
    private static void WarmBudget()
    {
        double worst = 0.0;
        int least = int.MaxValue;
        for (int run = 0; run < 10; run++)
        {
            int compiled = 0;
            var warm = new CodeWarm(WarmModule, 10000, 99, m =>
            {
                Spin(0.1);
                compiled++;
                return true;
            });
            warm.Root(WarmDay.Wrap(WarmDay.OnDay));
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(WarmSliceMs, 1);
            warm.Run(ref slice);
            double took = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            worst = Math.Max(worst, took);
            least = Math.Min(least, compiled);
        }
        Check(worst < WarmSliceMs + 0.6 && least >= 5, "day warm: a slice of " + WarmSliceMs + " ms with methods of 0.1 ms ends within one of them (at worst "
                                                           + worst.ToString("F2") + " ms, at least " + least + " methods)");
    }

    // A long method is compiled only as the first of a slice, so a frame holds one long method at most.
    private static void WarmHeavy()
    {
        var sliceOf = new List<int>();
        List<MethodBase> order = WarmWalk(1000.0, 99, out int slices, 64, sliceOf);
        bool alone = true;
        int heavy = 0;
        for (int i = 0; i < order.Count; i++)
        {
            byte[] il = order[i].Module == WarmModule ? order[i].GetMethodBody()?.GetILAsByteArray() : null;
            if (il != null && il.Length > 64)
            {
                heavy++;
                alone &= i == 0 || sliceOf[i - 1] != sliceOf[i];
            }
        }
        Check(heavy >= 2 && alone && slices > heavy, "day warm: a long method (" + heavy + " over 64 bytes of IL) is compiled only as the first of its slice (" + slices + " slices)");
    }

    // Compiled code lasts the run: a second load's walk over the same handlers takes nothing new.
    private static void WarmAgain()
    {
        int compiled = 0;
        var warm = new CodeWarm(WarmModule, 10000, 99, m =>
        {
            compiled++;
            return true;
        });
        warm.Root(WarmDay.Wrap(WarmDay.OnDay));
        WorkSlice first = WorkSlice.Whole;
        while (!warm.Run(ref first))
        {
            first = WorkSlice.Whole;
        }
        int once = compiled;
        warm.Root(WarmDay.Wrap(WarmDay.OnDay));   // the next city's handler: a new delegate, the same methods
        WorkSlice second = WorkSlice.Whole;
        bool done = warm.Run(ref second);
        Check(once > 10 && compiled == once && done && warm.Done, "day warm: a second load's handlers take nothing new (" + once + " then " + compiled + ")");
        warm.Root(new Action(WarmDay.Unused));
        WorkSlice third = WorkSlice.Whole;
        warm.Run(ref third);
        Check(compiled == once + 1, "day warm: a handler not met before is taken in on a later load");
    }

    // Compiling ahead changes when, never what: the day's numbers are the same before and after, on the runtime's own compiler.
    private static void WarmSameDay()
    {
        int[] before = WarmDay.Numbers(12345);
        var warm = new CodeWarm(WarmModule, 10000, 99);
        warm.Root(WarmDay.Wrap(WarmDay.OnDay));
        warm.Root(typeof(WarmDay).GetMethod(nameof(WarmDay.Numbers)));
        WorkSlice slice = WorkSlice.Whole;
        while (!warm.Run(ref slice))
        {
            slice = WorkSlice.Whole;
        }
        int[] after = WarmDay.Numbers(12345);
        bool same = before.Length == after.Length;
        for (int i = 0; same && i < before.Length; i++)
        {
            same = before[i] == after[i];
        }
        Check(warm.Compiled > 10 && warm.Refused == 0, "day warm: the runtime gives every method's code when asked (" + warm.Compiled + " compiled, " + warm.Refused + " refused)");
        Check(same, "day warm: the day's numbers are the same after its code is compiled ahead");
        Check(CodeWarm.Compile(typeof(WarmDay).GetMethod("Deep3", BindingFlags.NonPublic | BindingFlags.Static)), "day warm: a method's function pointer is given");
    }

    // Over the sources: the walk is driven from the game's two events and the economy's shared ones, and starts after the loading screen.
    private static void WarmSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string day = Read(Path.Combine(scripts, @"Core\DayWarm.cs"));
        string links = Read(Path.Combine(scripts, @"Wiring\EconomyLinks.cs"));
        string mod = Read(Path.Combine(scripts, @"Core\RealLifeOverhaulMod.cs"));
        Check(day.Contains("_warm.Root(GlobalEvents.onNewHour);") && day.Contains("_warm.Root(GlobalEvents.onNewDay);") && day.Contains("FeatureDays?.Invoke()"),
            "day warm: its roots are the game's hour and day events and the features' shared ones");
        Check(links.Contains("DayWarm.FeatureDays = () => EconomyDirector.AfterDay + EconomyDirector.AfterHour;") && links.Contains("DayWarm.FeatureDays = null;"),
            "day warm: the economy's AfterDay and AfterHour handed over in Wiring and taken back");
        Check(day.Contains("UI.Load.LoadScene.isLoading") && day.Contains("WorkSlice.Of(SliceMs, 1)") && day.Contains("public const double SliceMs = 1.0;"),
            "day warm: it starts after the loading screen and runs a slice of a millisecond a frame");
        Check(mod.Contains("Guard.Add<DayWarm>(_root)"), "day warm: the mod adds it with each city");
    }

    private static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;
}

// A made-up day: what the walk must and must not reach. Nothing here is ever called but Numbers and through the walk's checks.
internal static class WarmDay
{
    private static int _total;

    // As Core/Guard.Wrap: a closure holding the feature's handler.
    public static Action Wrap(Action handler)
    {
        return () =>
        {
            try
            {
                handler();
            }
            catch (Exception)
            {
                _total = -1;
            }
        };
    }

    public static void OnDay()
    {
        Settle(3);
        Action later = () => Shared(2);
        later();
        foreach (int n in Steps(3))
        {
            _total += n;
        }
        _total += Pick(7) + new WarmLedger(_total).Sum;
        _total += Cased(_total % 4);
        WarmShape shape = new WarmSquare();
        _total += (int)shape.Area();
        _total += (int)(GetTickCount() & 1u);
    }

    private static void Settle(int n) => _total += Deep1(n);
    private static int Deep1(int n) => Deep2(n) + 1;
    private static int Deep2(int n) => Deep3(n) + 1;
    private static int Deep3(int n) => n * 2;
    private static void Shared(int n) => _total += n;
    private static T Pick<T>(T value) => value;

    private static IEnumerable<int> Steps(int n)
    {
        for (int i = 0; i < n; i++)
        {
            yield return i;
        }
    }

    private static int Cased(int n)
    {
        switch (n)
        {
            case 0:
                return 4;
            case 1:
                return 9;
            case 2:
                return 16;
            default:
                return Deep3(n);
        }
    }

    public static void Unused() => _total += 99;

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    // The numbers a day of this made-up city gives: computed the same way whether or not its code was compiled ahead.
    public static int[] Numbers(int seed)
    {
        var r = new Random(seed);
        var result = new int[16];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Cased(r.Next(8)) + Pick(r.Next(100)) + new WarmLedger(i).Sum;
        }
        return result;
    }
}

internal sealed class WarmLedger
{
    public readonly int Sum;

    public WarmLedger(int start)
    {
        Sum = start * 3 + 1;
    }
}

internal abstract class WarmShape
{
    public abstract float Area();
}

internal sealed class WarmSquare : WarmShape
{
    public override float Area() => 4f;
}
