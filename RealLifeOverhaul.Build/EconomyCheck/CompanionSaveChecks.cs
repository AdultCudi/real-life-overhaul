using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// The companion report in the save (Core/CompanionGame.Begin and End, Core/CompanionSnapshot.Collect and Render). A player's log
// (28 Oct 2026, 6,900 district records) had the mod's part of a save at 1,029 ms, 930 of them in "the kept lines, the companion and the
// district save", the district save 659 and the rest the report: its readers, then each module written to JSON twice (once to check it,
// once into the report) on the main thread. Now each module is written once, the report's text is written on a worker while the district
// save writes its section, and no list of keys is made for each row. Checked here: the same text as the two-pass way gives, on a
// report of a large save's size; fewer bytes allocated and less time; a module that cannot be written fails alone; and the order of the calls in the
// save (the readers before the district save takes the mod's places out of the game's lists, the text joined after it).
internal static partial class Program
{
    private static void CompanionSaveChecks()
    {
        // A large save's report: 10 modules, 6,900 rows in all, each row a dozen fields, one of them an array of rows.
        var readers = new Dictionary<string, Func<CompanionModule>>(StringComparer.Ordinal);
        for (int m = 0; m < 10; m++)
        {
            int module = m;
            readers["module" + m.ToString("D2")] = () =>
            {
                var rows = new List<object>(690);
                for (int i = 0; i < 690; i++)
                {
                    rows.Add(CompanionSnapshot.Row("street", "ba:street_" + module, "number", i, "name", "Business " + i, "typeId", "ba:businesstype_shop",
                        "temporarilyClosed", i % 7 == 0, "cents", (long)i * 1234, "fromDay", i % 365, "toDay", i % 365 + 1, "scope", "place", "basis", "paid",
                        "lines", new object[] { CompanionSnapshot.Row("a", i, "b", "x"), CompanionSnapshot.Row("a", i + 1, "b", "y") }));
                }
                return new CompanionModule("ready", rows);
            };
        }
        readers["zzbad"] = () => new CompanionModule("ready", new object[] { new object() });   // a value JSON cannot hold: failed alone
        // What the readers gave, once: the writing alone is timed, the same modules each way.
        var made = new Dictionary<string, CompanionModule>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Func<CompanionModule>> reader in readers)
        {
            made[reader.Key] = reader.Value();
        }
        readers = new Dictionary<string, Func<CompanionModule>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, CompanionModule> kept in made)
        {
            CompanionModule module = kept.Value;
            readers[kept.Key] = () => module;
        }
        Dictionary<string, object> clock = CompanionClock();

        CompanionSnapshot.Collected got = CompanionSnapshot.Collect("test", clock, readers);
        string text = CompanionSnapshot.Render(got);
        string reference = TwoPass("test", clock, readers);
        Check(text == reference, "companion save: the report written once per module is the text the two-pass way gives (" + text.Length / 1024 + " KB)");
        Check(got.Failed.Count == 1 && got.Failed[0].Key == "zzbad" && text.Contains("\"zzbad\":{\"data\":null,\"status\":\"failed\",\"version\":1}"),
            "companion save: a module that cannot be written is failed alone and told to the caller, not lost");
        Check(CompanionSnapshot.Capture("test", clock, readers) == text, "companion save: a live reader's capture is the same text");

        // Another thread writes the same text (the worker beside the district save).
        string other = null;
        var thread = new System.Threading.Thread(() => other = CompanionSnapshot.Render(CompanionSnapshot.Collect("test", clock, readers)));
        thread.Start();
        thread.Join();
        Check(other == text, "companion save: a worker writes the same text as the main thread");

        long before = GC.GetAllocatedBytesForCurrentThread();
        Written(got);
        long once = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        TwoPass("test", clock, readers);
        long twice = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(once > 0 && once < twice, "companion save: writing each module once allocates less than writing it twice (" + once / 1024 + " KB against " + twice / 1024 + " KB)");

        double best = double.MaxValue, bestTwo = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            best = Math.Min(best, Time(() => Written(got)));
            bestTwo = Math.Min(bestTwo, Time(() => TwoPass("test", clock, readers)));
        }
        if (Environment.GetEnvironmentVariable("RLO_SAVE_REPORT") == "1")
        {
            Console.WriteLine("companion report of " + text.Length / 1024 + " KB: written once " + best.ToString("F1") + " ms, the two-pass way " + bestTwo.ToString("F1") + " ms");
        }
        Check(best < bestTwo, "companion save: writing the report once is quicker than the two-pass way (" + best.ToString("F1") + " ms against " + bestTwo.ToString("F1") + " ms)");

        // The save's order: the readers before the district save, its text joined after, and the keys listed.
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string hook = File.ReadAllText(System.IO.Path.Combine(root, "Assets/Mods/RealLifeOverhaul/Scripts/Core/SaveHook.cs"));
        string districtSave = File.ReadAllText(System.IO.Path.Combine(root, "Assets/Mods/RealLifeOverhaul/Scripts/DistrictSave/DistrictSaveDirector.cs"));
        int begin = hook.IndexOf("CompanionGame.Begin(", StringComparison.Ordinal);
        int district = hook.IndexOf("DistrictSaveDirector.BeforeWrite()", StringComparison.Ordinal);
        int writes = districtSave.IndexOf("Write(save, s, out int records", StringComparison.Ordinal);
        int joins = districtSave.IndexOf("CompanionGame.End(save);", writes, StringComparison.Ordinal);
        Check(begin > 0 && district > begin && joins > writes && districtSave.Split(new[] { "CompanionGame.End(save);" }, StringSplitOptions.None).Length == 3 && !hook.Contains("CompanionGame.Save("),
            "companion save: the save runs the report's readers, then the district save, which joins the report's text beside its section (and on its early exit)");
        string game = File.ReadAllText(System.IO.Path.Combine(root, "Assets/Mods/RealLifeOverhaul/Scripts/Core/CompanionGame.cs"));
        Check(game.Contains("ThreadPool.UnsafeQueueUserWorkItem(Worker, job)") && game.Contains("Interlocked.CompareExchange(ref job.Taken, 1, 0)") && game.Contains("job.Done.Wait()")
              && game.Contains("CompanionSnapshot.Render(job.Got)"), "companion save: the text is written by a worker, or by the save itself if none started, and awaited");
        Check(!OnWorkerLogs(game), "companion save: the worker writes no log line and reads nothing of the game (the faults are told at the join)");
    }

    // The worker's method and the text it runs hold no log call and no game member.
    private static bool OnWorkerLogs(string game)
    {
        int at = game.IndexOf("private static void Write(Job job)", StringComparison.Ordinal);
        int stop = game.IndexOf("private static double Ms(", at, StringComparison.Ordinal);
        string body = at >= 0 && stop > at ? game.Substring(at, stop - at) : "Logger";
        return body.Contains("Logger") || body.Contains("SaveGameManager") || body.Contains("Failed(");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Written(CompanionSnapshot.Collected got)
    {
        return CompanionSnapshot.Render(got);
    }

    private static double Time(Action work)
    {
        long began = Stopwatch.GetTimestamp();
        work();
        return (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
    }

    // The report as it was made before: each module written to check it, and again into the report.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string TwoPass(string version, Dictionary<string, object> captured, IDictionary<string, Func<CompanionModule>> readers)
    {
        var modules = new Dictionary<string, object>(StringComparer.Ordinal);
        var keys = new List<string>(readers.Keys);
        keys.Sort(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            Dictionary<string, object> row;
            try
            {
                CompanionModule module = readers[key]() ?? new CompanionModule("unavailable");
                row = CompanionSnapshot.Row("version", module.Version, "status", module.Status, "data", module.Data);
                CompanionSnapshot.Json(row);
            }
            catch (Exception)
            {
                row = CompanionSnapshot.Row("version", 1, "status", "failed", "data", null);
            }
            modules.Add(key, row);
        }
        return CompanionSnapshot.Json(CompanionSnapshot.Row("schemaVersion", CompanionSnapshot.SchemaVersion, "provider", "rlo", "modVersion", version,
            "captured", captured, "modules", modules));
    }
}
