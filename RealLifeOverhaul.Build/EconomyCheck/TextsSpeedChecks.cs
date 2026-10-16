using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Texts;

// RLO Texts on a GPU ran at the processor's priority (the owner's session, 16 Oct 2026: the 2B on an RTX 5070 Ti, the
// first reply in 24 s and 39 words a second in the game, against 0.07 s and 294 a second outside it). The server was
// below normal priority (the process's class and llama.cpp's --prio -1) with four threads: its thread that hands each
// step to the GPU waited behind every busy thread of the game. Measured outside the game with sixteen busy threads and
// the GPU busy: below normal over 40 s to load and 3 to 6 words a second, normal 3.5 s and 110 to 200. Now a GPU runs at
// normal priority, at most two threads, out of Windows' power throttling; the processor stays below normal, its threads
// as before. The rules of the arguments, the threads and the speed's key, the timings the start's lines give, and the
// sources that use them.
internal static partial class Program
{
    private static void TextsSpeedFix()
    {
        TextsSpeedArguments();
        TextsSpeedThreads();
        TextsSpeedTimings();
        TextsSpeedSources();
    }

    private static void TextsSpeedArguments()
    {
        var cpu = new TextServerStart { Exe = "llama-server.exe", Model = @"C:\pack\model.gguf", Threads = 4, Cache = @"C:\cache\", Log = @"C:\log.txt" };
        var gpu = new TextServerStart { Exe = "llama-server.exe", Model = @"C:\pack\model.gguf", Threads = 2, Device = "Vulkan0", Cache = @"C:\cache\", Log = @"C:\log.txt" };
        string c = TextServer.Arguments(cpu, 5000, "key"), g = TextServer.Arguments(gpu, 5001, "key");
        Check(TextServer.LowPriority(null) && !TextServer.LowPriority("Vulkan0") && !TextServer.LowPriority("CUDA0"),
            "texts, priority: the processor runs below normal, a GPU (any device named) at normal priority");
        Check(c.Contains(" --prio -1 ") && !c.Contains("--prio 0") && c.Contains(" -t 4 -tb 4 ") && c.EndsWith(" -dev none -ngl 0 -lv 2 --log-file \"C:\\log.txt\"", StringComparison.Ordinal),
            "texts, arguments: on the processor llama.cpp's own threads below normal (--prio -1), its threads as chosen, no layers on a GPU (" + c + ")");
        Check(g.Contains(" --prio 0 ") && !g.Contains("--prio -1") && g.Contains(" -t 2 -tb 2 ") && g.Contains(" -dev Vulkan0 -ngl 99 "),
            "texts, arguments: on a GPU llama.cpp's threads at normal priority (--prio 0), two threads, every layer on the GPU (" + g + ")");
        foreach (string a in new[] { c, g })
        {
            Check(a.StartsWith("-m \"C:\\pack\\model.gguf\" --host 127.0.0.1 --port 500", StringComparison.Ordinal) && a.Contains(" --api-key key ") && a.Contains(" -c 1024 -np 1 --no-webui ")
                  && a.Contains(" --offline") && a.Contains(" --slot-save-path \"C:\\cache\"") && !a.Contains("--host 0.0.0.0"),
                "texts, arguments: 127.0.0.1 only, its key, one slot of 1024, no web page, offline, the cache folder without its last slash, whatever the device");
        }
        Check(!TextServer.Arguments(new TextServerStart { Model = "m", Threads = 1 }, 1, "k").Contains("--slot-save-path") && !TextServer.Arguments(new TextServerStart { Model = "m", Threads = 1 }, 1, "k").Contains("--log-file"),
            "texts, arguments: no cache folder and no log, none asked for");
    }

    private static void TextsSpeedThreads()
    {
        Check(TextSpeed.Threads(8, 0, true) == 2 && TextSpeed.Threads(16, 0, true) == 2 && TextSpeed.Threads(16, 4, true) == 2 && TextSpeed.Threads(16, 3, true) == 2,
            "texts, threads: on a GPU two, whatever the cores or the option (more only wait for the GPU)");
        Check(TextSpeed.Threads(4, 0, true) == 2 && TextSpeed.Threads(3, 0, true) == 1 && TextSpeed.Threads(2, 0, true) == 1 && TextSpeed.Threads(1, 0, true) == 1,
            "texts, threads: on a GPU still never so many that the game keeps fewer than two cores, and at least one");
        for (int cores = 1; cores <= 32; cores++)
        {
            for (int option = 0; option <= 4; option++)
            {
                if (TextSpeed.Threads(cores, option, false) != TextSpeed.Threads(cores, option))
                {
                    Check(false, "texts, threads: on the processor as before (" + cores + " cores, option " + option + ")");
                    return;
                }
            }
        }
        Check(TextSpeed.Threads(16, 0, false) == 4 && TextSpeed.Threads(16, 3, false) == 3, "texts, threads: on the processor as before, by the cores and the option");
        Check(TextSpeed.For("RLO Texts 2", null, 4) == "RLO Texts 2;cpu;4", "texts, the speed's key: the processor's as before (a speed measured there is kept)");
        Check(TextSpeed.For("RLO Texts 2", "Vulkan0", 2) == "RLO Texts 2;Vulkan0;2;normal" && TextSpeed.For("RLO Texts 2", "Vulkan0", 2) != "RLO Texts 2;Vulkan0;2"
              && TextSpeed.For("RLO Texts 2", "Vulkan0", 4) != "RLO Texts 2;Vulkan0;4",
            "texts, the speed's key: a GPU's names its priority, so a speed measured below normal (39 words a second) is measured again");
    }

    private static void TextsSpeedTimings()
    {
        const string answer = "{\"content\":\"Hi\",\"timings\":{\"cache_n\":0,\"prompt_n\":141,\"prompt_ms\":38.2,\"prompt_per_token_ms\":0.27,\"predicted_n\":1,\"predicted_ms\":0.4,\"predicted_per_second\":2500}}";
        string t = TextServer.Timings(answer, 23.9, 0.4);
        Check(t.StartsWith("141 tokens read in 38 ms and 1 written in 0 ms by the model, 23.86 s outside them, 23.90 s in all; the server used 0.40 s of processor time", StringComparison.Ordinal),
            "texts, the start's lines: a reply's tokens and the model's own times, the rest outside them and the processor time used (" + t + ")");
        string fast = TextServer.Timings(answer, 0.01, 0.01);
        Check(fast.Contains(" 0.00 s outside them"), "texts, the start's lines: a reply quicker than the server's own times has no time outside them, never less than none (" + fast + ")");
        string none = TextServer.Timings("{\"content\":\"Hi\"}", 1.5, double.NaN);
        Check(none == "no timings in the answer, 1.50 s in all; the server used unknown of processor time", "texts, the start's lines: an answer without timings, and a processor time the system cannot give (" + none + ")");
        Check(TextServer.Timings(null, 2.0, 0.1).StartsWith("no timings", StringComparison.Ordinal), "texts, the start's lines: no answer at all");
    }

    private static void TextsSpeedSources()
    {
        string server = TextsSource(@"Texts\TextServer.cs").Replace("\r\n", "\n"), director = TextsSource(@"Texts\TextsDirector.cs");
        Check(Regex.IsMatch(server, @"_process\.PriorityClass = low \? ProcessPriorityClass\.BelowNormal : ProcessPriorityClass\.Normal;")
              && Regex.IsMatch(server, @"bool low = LowPriority\(_start\.Device\);") && Regex.IsMatch(server, @"if \(!low\)\s*\{\s*FullSpeed\(_process\);"),
            "texts, the runner: the server's priority class by its device, and a GPU's kept out of power throttling");
        Check(server.Contains("Priority = LowPriority(_start.Device) ? ThreadPriority.BelowNormal : ThreadPriority.Normal"),
            "texts, the runner: the mod's worker thread below normal on the processor, normal on a GPU (it reads the answer as it comes)");
        Check(server.Contains("new ProcessStartInfo(_start.Exe, Arguments(_start, _port, _key))")
              && Regex.Matches(server, @"\.Append\(""[^""]*--prio").Count == 1 && server.Contains(".Append(\" -c 1024 -np 1 --no-webui --prio \").Append(LowPriority(start.Device) ? \"-1\" : \"0\")"),
            "texts, the runner: the server is started with Arguments, the one place its priority is written");
        Check(director.Contains("TextSpeed.Threads(_cores, Settings.TextsThreads <= 0 ? 0 : Settings.TextsThreads + 1, device != null)")
              && director.Contains("string wanted = TextSpeed.For(_pack.Name + \" \" + _pack.Version, device, threads);"),
            "texts, the director: the threads for the device chosen, and the speed's key with its priority");
        Check(Regex.IsMatch(server, @"_detail = ""sent the test card, 1 token to write: "" \+ Timings\(answer, took, Cpu\(\) - cpu\);")
              && server.Contains("(_detail != null ? \" (\" + _detail + \")\" : string.Empty)") && server.Contains("detail = Timings(answer, TextClock.Now - asked, Cpu() - cpu);")
              && server.Contains("\" priority, \" + _start.Threads + \" threads).\""),
            "texts, the start's lines: the first reply and the speed test say what was sent, the model's times, the rest, the processor time, the priority and threads");
    }
}
