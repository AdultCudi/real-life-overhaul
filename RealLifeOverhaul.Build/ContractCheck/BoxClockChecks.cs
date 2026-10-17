using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// The white box's self-check times the box's own work (Core/GameUi/BoxClock.cs, InfoBox.Parts.cs). It timed a page from
// Clear to Done, and the Settings app does its own work between them (every option compared with the presets, its table
// of a hundred rows filled): "a page of 5 parts written in 5.5 ms, wanted under 3 ms: SLOW" all session,
// while a page of 17 parts of the Planner's took 2.6 ms. Now only the time inside the box's calls counts, the page's
// whole time is said beside it. The rules on made-up timestamps, a made-up page with the app's work between its parts
// (the box's share stays its own however long the app takes, and costs nothing), and the sources: every call of the box
// that writes a part is timed, and the line says both times.
internal static partial class Program
{
    private static void BoxClockChecks(string scripts)
    {
        Section("the apps' white box: its self-check times the box's own work (Core/GameUi/BoxClock)");
        BoxClockRules();
        BoxClockPage();
        Section("the apps' white box: every call that writes a part is timed, in " + scripts);
        BoxClockSources(scripts);
    }

    private static void BoxClockRules()
    {
        var c = new BoxClock();
        c.Begin(1000);
        Check(c.Own(1000) == 0 && c.Page(1000) == 0, "a page begun has no time yet");
        c.Enter(1010);
        c.Leave(1030);
        Check(c.Own(1100) == 20 && c.Page(1100) == 100, "a call's time is the box's; the time after it, until the next, is the page's (the app's own work)");
        c.Enter(1100);
        c.Enter(1110);   // (a row's text inside the row)
        c.Leave(1150);
        Check(c.Own(1155) == 20 + 55, "a call under way counts to now, the call inside it with it");
        c.Leave(1160);
        Check(c.Own(5000) == 80 && c.Page(5000) == 4000, "a call inside another is counted once, with the outer one");
        c.Leave(6000);
        Check(c.Own(7000) == 80, "a leave with no call under way counts nothing");
        c.Begin(9000);
        Check(c.Own(9500) == 0 && c.Page(9500) == 500, "a new page starts from nothing, a call left open on the last page forgotten");
        c.Enter(9500);
        c.Leave(9500);
        Check(c.Own(9600) == 0, "a call that takes no time adds none");
    }

    // A made-up page: forty parts, each a call with one inside it, and the app's own work between them (a spin of 50
    // microseconds, two milliseconds in all). The box's share is its calls only; timing them costs next to nothing.
    private static void BoxClockPage()
    {
        var c = new BoxClock();
        WriteFakePage(c, 40, 0.0);   // warmed
        long before = GC.GetAllocatedBytesForCurrentThread();
        double page = WriteFakePage(c, 40, 0.05), own = c.Own(Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(page >= 2.0 && own < 0.5 && own < InfoBoxBudgetMs,
            "a page of forty parts with 2 ms of the app's own work between them: the box's own share " + own.ToString("0.000") + " ms, the page " + page.ToString("0.000") + " ms (wanted under 0.5 of the box's)");
        Check(bytes == 0, "timing a page's calls allocates nothing (" + bytes + " bytes)");
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            WriteFakePage(c, 40, 0.0);
        }
        clock.Stop();
        double each = clock.Elapsed.TotalMilliseconds / 2000.0;
        Check(each < 0.05, "timing a page of forty parts costs " + each.ToString("0.0000") + " ms, wanted under 0.05 ms");
        Console.WriteLine("   a page of forty parts: the box's share " + own.ToString("0.000") + " ms of " + page.ToString("0.000") + " ms; timing it costs " + each.ToString("0.0000") + " ms, " + bytes + " bytes.");
    }

    private const double InfoBoxBudgetMs = 3.0;   // InfoBox.FillBudgetMs (the box is a Unity type: its constant is read from the source below)

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double WriteFakePage(BoxClock c, int parts, double appMsBetween)
    {
        long began = Stopwatch.GetTimestamp();
        c.Begin(began);
        long spin = (long)(appMsBetween * Stopwatch.Frequency / 1000.0);
        for (int i = 0; i < parts; i++)
        {
            c.Enter(Stopwatch.GetTimestamp());   // (a row)
            c.Enter(Stopwatch.GetTimestamp());   // (its text, its copy of a template)
            c.Leave(Stopwatch.GetTimestamp());
            c.Leave(Stopwatch.GetTimestamp());
            long until = Stopwatch.GetTimestamp() + spin;   // (the app's own work before its next part)
            while (Stopwatch.GetTimestamp() < until)
            {
            }
        }
        return (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
    }

    // Every public call of the box that writes into the page opens the clock, but those that only hand over to one that
    // does; the self-check's line says the box's own time and the page's; the budget is the one this check holds.
    private static void BoxClockSources(string scripts)
    {
        string box = Read(scripts, "Core/GameUi/InfoBox.cs"), parts = Read(scripts, "Core/GameUi/InfoBox.Parts.cs");
        if (box == null || parts == null)
        {
            Check(false, "the box's sources are not where they were");
            return;
        }
        var handOver = new HashSet<string>(StringComparer.Ordinal)
        {
            "public void Title(string words)", "public void Headline(string key)", "public bool Selector(int count, int index, Func<int, string> text, Action<int> changed)",
            "public void EndSection()", "public static InfoBox From(Transform whiteBox, Transform templates)",
            "public static InfoBox Into(Transform whiteBox, Transform templates, Transform box)", "public string Fit()", "public void SetAccent(int rgb)"
        };
        var untimed = new List<string>();
        int timed = 0;
        foreach (string source in new[] { box, parts })
        {
            foreach (Match m in Regex.Matches(source, @"\n        (public (?:static )?[\w<>]+ \w+\([^)]*\))\n        \{\n(.*)\n"))
            {
                string signature = m.Groups[1].Value;
                if (handOver.Contains(signature))
                {
                    continue;
                }
                bool opens = m.Groups[2].Value.Contains("using var own = Timed();") || m.Groups[2].Value.Contains("using var own = TimedWriting();")
                             || (signature == "public void Clear()" && source.Contains("            Begin();\n            using var own = Timed();"))
                             || (signature == "public void Done()" && Regex.IsMatch(Body(source, signature) ?? string.Empty, @"using \(Timed\(\)\)\s*\{\s*LayoutRebuilder\.MarkLayoutForRebuild"));
                if (opens)
                {
                    timed++;
                }
                else
                {
                    untimed.Add(signature);
                }
            }
        }
        Check(untimed.Count == 0 && timed >= 25, "every call of the box that writes a part opens its clock first (" + timed + " timed" + (untimed.Count > 0 ? "; not: " + string.Join("; ", untimed) : string.Empty) + ")");
        string button = Body(box, "public static Button Button(Transform row, int kind, string key, Action click)");
        Check(button != null && button.Contains("using var own = TimedWriting();") && parts.Contains("_writing = this;") && parts.Contains("_writing = null;"),
            "a row's buttons count to the box whose page is being written, from its Clear to its Done");
        string finish = Body(parts, "private void Finish()");
        Check(finish != null && finish.Contains("_clock.Own(now)") && finish.Contains("_clock.Page(now)") && finish.Contains("of the box's own work") && finish.Contains("from Clear to Done")
              && Regex.IsMatch(finish, @"\(ms < FillBudgetMs \? ""PASS"" : ""SLOW""\)") && !finish.Contains("_began"),
            "the self-check judges the box's own time against its budget and says the page's whole time beside it");
        Match budget = Regex.Match(parts, @"public const double FillBudgetMs = ([0-9.]+);");
        Check(budget.Success && double.Parse(budget.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) == InfoBoxBudgetMs,
            "the box's budget is " + InfoBoxBudgetMs + " ms of its own work, as this check holds it (" + (budget.Success ? budget.Groups[1].Value : "not found") + ")");
        Check(parts.Contains("private struct Own : IDisposable") && !Regex.IsMatch(parts + box, @"using \(var own = new Own"),
            "the clock's scope is a struct: opening it allocates nothing");
    }
}
