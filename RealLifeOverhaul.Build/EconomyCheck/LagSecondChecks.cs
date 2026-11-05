using System;
using RealLifeOverhaul;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// A player's "stuttering literally every second" (5 Nov 2026). In the second log the session record gave about a dozen frames of 99 to 114 ms
// that it also called 10 to 17 ms "by the steps' own clock", with the mod's scripts at 0.1 ms and the processor idle. Two clocks disagreed by
// some 90 ms and the line said so without saying which was right. FrameRules.Reading now tells apart a frame the mod's clock gives whole,
// one whose extra time lies between the mod's mark and the engine's own read of the time, one the engine counts a frame late, and a jump of the
// engine's time that the machine's clock did not see; and the mod never drives the engine's clock or pacing itself. Offline: run with --lag-second.
internal static partial class Program
{
    private static void LagSecond()
    {
        // The player's own frames: 104 ms by the engine, 10 ms between the mod's marks, no read of the time found, 12 ms in the frame before.
        Check(FrameRules.Reading(104.0, 10.0, -1.0, 12.0) == FrameTime.Jump, "lag second: 104 ms by the engine against 10 and 12 ms by the mod's clock is a jump of the game's time");
        Check(FrameRules.Reading(108.0, 16.0, 17.0, 15.0) == FrameTime.Jump, "lag second: the same with the engine's own reads of the time 17 ms apart");
        Check(FrameRules.Reading(104.0, 10.0, -1.0, 103.0) == FrameTime.Earlier, "lag second: the time sat in the frame before when that frame took the engine's figure");
        Check(FrameRules.Reading(104.0, 10.0, 103.0, 12.0) == FrameTime.Later, "lag second: the time sat between the mod's mark and the engine's read when the engine's reads are that far apart");
        Check(FrameRules.Reading(104.0, 103.0, -1.0, 12.0) == FrameTime.Whole, "lag second: a frame the mod's clock gives whole is whole");
        Check(FrameRules.Reading(16.7, 16.0, 16.1, 16.4) == FrameTime.Whole, "lag second: an ordinary frame is whole");
        Check(FrameRules.Reading(104.0, -1.0, -1.0, -1.0) == FrameTime.Unmeasured, "lag second: with the steps not timed nothing is claimed");
        Check(FrameRules.Reading(104.0, 10.0, -1.0, -1.0) == FrameTime.Jump, "lag second: with only the one span known and far from the engine's figure it is a jump");

        // Two clocks agree within 2 ms or a tenth of the longer figure, in either order.
        Check(FrameRules.Agree(16.0, 17.9) && !FrameRules.Agree(16.0, 18.1), "lag second: 2 ms is allowed on a short frame");
        Check(FrameRules.Agree(104.0, 94.0) && FrameRules.Agree(94.0, 104.0) && !FrameRules.Agree(104.0, 90.0), "lag second: a tenth is allowed on a long frame, both ways round");

        // The old line gave a Work or Spread cause from the small window; a jump is its own cause and the line says so.
        FrameFigures small = FrameFigures.Unknown(104.0);
        small.SpanMs = 10.0;
        small.StepsMs = 9.0;
        small.WaitMs = 1.0;
        small.ThreadMs = 16.0;
        small.Busy = 0.08;
        small.Others = 0.0;
        Check(FrameRules.Cause(small) != FrameCause.TimeJump, "lag second: without the reading the figures alone never claim a jump");
        small.TimeJumped = true;
        Check(FrameRules.Cause(small) == FrameCause.TimeJump, "lag second: a jumped frame's cause is the jump, not the small window's work or wait");
        var line = new StringBuilder();
        FrameRules.Describe(line, small, "n/a", "n/a", "n/a", "n/a");
        string text = line.ToString();
        Check(text.Contains("the game's frame time jumped") && !text.Contains("the main thread was working") && !text.Contains("spread over the engine's steps"),
            "lag second: the long frame's line says the game's time jumped and does not blame work");
        Check(FrameRules.Short(FrameCause.TimeJump).Contains("jumping"), "lag second: the minute's count has words for it");

        // The clause after the steps.
        Check(Said(FrameTime.Jump, 104.0, 10.0, -1.0, 12.0).Contains("longer than the machine's clock saw pass") && Said(FrameTime.Jump, 104.0, 10.0, -1.0, 12.0).Contains("10 ms between the mod's marks")
              && Said(FrameTime.Jump, 104.0, 10.0, -1.0, 12.0).Contains("12 ms in the frame before"), "lag second: the jump clause gives the clock's figures");
        Check(Said(FrameTime.Earlier, 104.0, 10.0, -1.0, 103.0).Contains("a frame late") && Said(FrameTime.Earlier, 104.0, 10.0, -1.0, 103.0).Contains("103 ms"),
            "lag second: the clause for a time counted a frame late gives that frame's length");
        Check(Said(FrameTime.Later, 104.0, 10.0, 103.0, 12.0).Contains("Initialization.PlayerUpdateTime") && Said(FrameTime.Later, 104.0, 10.0, 103.0, 12.0).Contains("103 ms"),
            "lag second: the clause for a stall before the engine's read names that step");
        Check(Said(FrameTime.Whole, 104.0, 103.0, -1.0, 12.0).Length == 0, "lag second: a frame the clock gives whole needs no clause");
        Check(Said(FrameTime.Whole, 104.0, 100.0, -1.0, 12.0).Contains("by the steps' own clock the frame took 100 ms"), "lag second: a small gap is still given as before");

        LagSecondSources();
        LagSecondTexts();
    }

    private static string Said(FrameTime reading, double took, double span, double read, double before)
    {
        var line = new StringBuilder();
        FrameRules.Say(line, reading, took, span, read, before);
        return line.ToString();
    }

    private static void LagSecondSources()
    {
        string watch = ModSource("Diagnostics/SessionWatch.cs"), timing = ModSource("Diagnostics/SessionWatch.Timing.cs"), steps = ModSource("Diagnostics/FrameSteps.cs");
        string lines = ModSource("Diagnostics/SessionWatch.Lines.cs");
        Check(watch.Contains("FrameRules.Say(_line, _reading,") && !watch.Contains("SpanDiffers"), "lag second source: the long frame's line gives the reading in place of the bare 2 ms gap");
        Check(timing.Contains("FrameRules.Reading(took * 1000.0, _spanMs, _readMs, _beforeMs)") && timing.Contains("TimeJumped = _reading == FrameTime.Jump"),
            "lag second source: the figures are read against the mod's clock, and a jump is marked");
        Check(timing.Contains("_earlier ? FrameSteps.BeforeStepsMs") && timing.Contains("_earlier ? MachineClock.ThreadMsBefore"), "lag second source: a time counted a frame late takes that frame's steps and processor figures");
        Check(steps.Contains("\"PlayerUpdateTime\"") && steps.Contains("ReadStep") && steps.Contains("Before[g] = Last[g]"), "lag second source: the loop gets a mark at the engine's own read of the time, and the frame before is kept");
        Check(lines.Contains("the engine counted") && lines.Contains("s between its marks"), "lag second source: the minute's line sets the engine's seconds of play beside the mod's clock");

        // The mod must never drive the engine's clock or pacing itself, or hold the main thread: any write would be what the player's stutter is looked for in.
        string[] banned =
        {
            @"\bTime\.(timeScale|maximumDeltaTime|fixedDeltaTime|captureFramerate|captureDeltaTime|maximumParticleDeltaTime)\s*(=|\+=|-=|\*=)[^=]",
            @"\bApplication\.(targetFrameRate|runInBackground|backgroundLoadingPriority)\s*=[^=]",
            @"\bQualitySettings\.(vSyncCount|maxQueuedFrames|asyncUploadTimeSlice|asyncUploadBufferSize)\s*=[^=]",
            @"\bGC\.(Collect|WaitForPendingFinalizers|TryStartNoGCRegion)\s*\(",
            @"\bGarbageCollector\.(GCMode|incrementalTimeSliceNanoseconds)\s*=[^=]",
            @"\bResources\.UnloadUnusedAssets\s*\(",
            @"\bAssetBundle\.UnloadAllAssetBundles\s*\(",
            @"\bOnDemandRendering\.renderFrameInterval\s*=[^=]",
        };
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var hits = new List<string>();
        int files = 0;
        if (Directory.Exists(scripts))
        {
            foreach (string path in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", string.Empty);   // a comment may name them
                foreach (string pattern in banned)
                {
                    if (Regex.IsMatch(code, pattern))
                    {
                        hits.Add(System.IO.Path.GetFileName(path) + " " + pattern);
                    }
                }
            }
        }
        Check(files > 500, "lag second source: the mod's scripts were read (" + files + " files)");
        Check(hits.Count == 0, "lag second source: nothing in the mod sets the engine's time scale, frame pacing, vSync, collector or screen, or unloads assets (" + string.Join("; ", hits) + ")");
    }

    private static void LagSecondTexts()
    {
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: with Developer diagnostics on, a long frame was blamed on the wrong frame" }, { "pt", "Corrigido: com o diagnóstico de desenvolvedor ligado" },
            { "zh-cn", "已修复：开启开发者诊断后" }, { "de", "Behoben: Bei eingeschalteter Entwicklerdiagnose" }, { "fr", "Corrigé : avec les diagnostics de développement activés" },
            { "es", "Corregido: con el diagnóstico para desarrolladores activado" }, { "ru", "Исправлено: при включённой диагностике для разработчиков" },
            { "nl", "Opgelost: met de ontwikkelaarsdiagnose aan" }, { "tr", "Düzeltildi: Geliştirici Tanılama açıkken" }, { "pl", "Naprawiono: przy włączonej diagnostyce dla programistów" },
            { "ko", "수정: 개발자 진단을 켜면" }
        };
        foreach (KeyValuePair<string, string> start in starts)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, start.Key + ".json"));
            int rows = 0;
            bool plain = true;
            if (words != null && words.TryGetValue("reallife_changes_next", out string next))
            {
                rows = next.Contains(start.Value) ? 1 : 0;
                plain = !next.Contains("  ") && !next.Contains((char)0x2014) && !next.Contains((char)0x2013) && !next.Contains('!');
            }
            Check(rows == 1 && plain, "lag second texts: the next version's What's new has the fix once in " + start.Key + ", with no double space, dash or exclamation mark (" + rows + " row(s))");
        }
    }
}
