using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Texts;

// RLO Texts on a GPU built every shader again at each start in the game (the owner's sessions, 16 Oct 2026: the 2B on an
// RTX 5070 Ti, the first reply in 22 to 24 s, 21 s of it the server's processor time; outside the game 0.05 s). Steam
// starts the game with variables that point the GPU driver's shader cache at its own folder of shader pre-caching
// (__GL_SHADER_DISK_CACHE_PATH and its kin), the server inherited them, and the NVIDIA driver kept nothing there.
// Measured outside the game with the runner's arguments: with Steam's variables 9.9 s every start and nothing kept;
// without them 10.1 s once, then 0.05 s, killed or not. Now the variables are not passed on, the first reply on a GPU may
// take long enough to build them once, and its line says whether the cache was used, created or missing.
internal static partial class Program
{
    private static void TextsCacheFix()
    {
        TextsCacheVariables();
        TextsCacheLimit();
        TextsCacheState();
        TextsCacheSources();
    }

    private static void TextsCacheVariables()
    {
        foreach (string steam in new[] { "__GL_SHADER_DISK_CACHE_PATH", "__GL_SHADER_DISK_CACHE_APP_NAME", "__GL_SHADER_DISK_CACHE_READ_ONLY_APP_NAME", "__GL_SHADER_DISK_CACHE_SKIP_CLEANUP",
                                         "__gl_shader_disk_cache_path", "__GL_SHADER_DISK_CACHE", "MESA_SHADER_CACHE_DIR", "MESA_SHADER_CACHE_MAX_SIZE", "MESA_GLSL_CACHE_DIR",
                                         "MESA_DISK_CACHE_SINGLE_FILE", "MESA_DISK_CACHE_READ_ONLY_FOZ_DBS", "DXVK_STATE_CACHE_PATH" })
        {
            Check(TextServer.ShaderCacheVariable(steam), "texts, the shader cache: " + steam + " (Steam's shader pre-caching) is not passed on, whatever its case");
        }
        foreach (string kept in new[] { "PATH", "TEMP", "TMP", "LOCALAPPDATA", "APPDATA", "USERPROFILE", "SystemRoot", "GGML_VK_VISIBLE_DEVICES", "__GL_SYNC_TO_VBLANK", "SteamAppId", "MESA_LOADER_DRIVER_OVERRIDE", "", null })
        {
            Check(!TextServer.ShaderCacheVariable(kept), "texts, the shader cache: " + (kept ?? "no name") + " is passed on as it is");
        }
        var info = new ProcessStartInfo("x");
        info.EnvironmentVariables["__GL_SHADER_DISK_CACHE_PATH"] = @"G:\Steam\steamapps\shadercache\1331550\nvidiav1";
        info.EnvironmentVariables["__GL_SHADER_DISK_CACHE_APP_NAME"] = "steamapp_shader_cache";
        info.EnvironmentVariables["__GL_SHADER_DISK_CACHE_READ_ONLY_APP_NAME"] = "steam_app_1331550";
        info.EnvironmentVariables["__GL_SHADER_DISK_CACHE_SKIP_CLEANUP"] = "1";
        info.EnvironmentVariables["MESA_SHADER_CACHE_DIR"] = @"G:\Steam\steamapps\shadercache\1331550";
        info.EnvironmentVariables["RLO_KEEP"] = "1";
        string localAppData = info.EnvironmentVariables["LOCALAPPDATA"], temp = info.EnvironmentVariables["TEMP"];
        string taken = TextServer.OwnShaderCache(info);
        bool none = true;
        foreach (System.Collections.DictionaryEntry e in info.EnvironmentVariables)
        {
            none &= !TextServer.ShaderCacheVariable(e.Key as string);
        }
        Check(none && info.EnvironmentVariables.ContainsKey("RLO_KEEP") && info.EnvironmentVariables["LOCALAPPDATA"] == localAppData && info.EnvironmentVariables["TEMP"] == temp,
            "texts, the shader cache: Steam's variables are taken out of the server's environment, every other one (the driver's own cache under LOCALAPPDATA among them) kept");
        Check(Regex.Matches(taken, ", ").Count == 4 && taken.IndexOf("__GL_SHADER_DISK_CACHE_PATH", StringComparison.OrdinalIgnoreCase) >= 0
              && taken.IndexOf("MESA_SHADER_CACHE_DIR", StringComparison.OrdinalIgnoreCase) >= 0 && taken.IndexOf("RLO_KEEP", StringComparison.OrdinalIgnoreCase) < 0,
            "texts, the shader cache: the names taken out are given for the log (" + taken + ")");
        Check(TextServer.OwnShaderCache(info) == string.Empty && info.EnvironmentVariables.ContainsKey("RLO_KEEP"), "texts, the shader cache: none to take out, nothing taken and nothing said");
    }

    private static void TextsCacheLimit()
    {
        var gpu = new TextServerStart { Device = "Vulkan0" };
        var cpu = new TextServerStart();
        Check(TextServer.WarmLimit(gpu) == 90.0 && TextServer.WarmLimit(cpu) == 30.0,
            "texts, the first reply's limit: on a GPU 90 s, long enough to build the shaders once (22 s measured in the game's load), on the processor 30 s as before");
        Check(TextServer.WarmLimit(new TextServerStart { Device = "Vulkan0", WarmSeconds = 120.0 }) == 120.0 && TextServer.WarmLimit(new TextServerStart { WarmSeconds = 5.0, BuildSeconds = 60.0 }) == 5.0,
            "texts, the first reply's limit: never below the warm-up's own on a GPU; the build's is a GPU's only");
        Check(TextServer.WarmLimit(gpu) + TextStartWatch.Grace < gpu.LoadSeconds + 10.0 && TextServer.WarmLimit(gpu) > 22.2 * 3.0,
            "texts, the first reply's limit: room for a processor three times slower than the owner's at building, still bounded");
    }

    private static void TextsCacheState()
    {
        Check(TextServer.ShaderCache(null, 20.0, false) == TextShaderCache.None && TextServer.ShaderCache(null, 0.01, true) == TextShaderCache.None,
            "texts, the shader cache: the processor has none, whatever the time");
        Check(TextServer.ShaderCache("Vulkan0", double.NaN, true) == TextShaderCache.Unknown && TextServer.ShaderCache("Vulkan0", double.PositiveInfinity, false) == TextShaderCache.Unknown,
            "texts, the shader cache: a processor time the system cannot give says unknown");
        Check(TextServer.ShaderCache("Vulkan0", 0.05, false) == TextShaderCache.Used && TextServer.ShaderCache("Vulkan0", 0.0, true) == TextShaderCache.Used
              && TextServer.ShaderCache("Vulkan0", TextServer.BuiltCpuSeconds - 0.01, true) == TextShaderCache.Used,
            "texts, the shader cache: a first reply of hundredths of processor time used the driver's cache (0.05 s measured)");
        Check(TextServer.ShaderCache("Vulkan0", TextServer.BuiltCpuSeconds, false) == TextShaderCache.Created && TextServer.ShaderCache("Vulkan0", 20.94, false) == TextShaderCache.Created,
            "texts, the shader cache: seconds of processor time built the shaders, the first time: created (20.94 s in the game)");
        Check(TextServer.ShaderCache("Vulkan0", 20.94, true) == TextShaderCache.Missing && TextServer.ShaderCache("Vulkan0", TextServer.BuiltCpuSeconds, true) == TextShaderCache.Missing,
            "texts, the shader cache: built again after a build with the same pack, device and runtime: missing");
        Check(TextServer.BuiltCpuSeconds >= 0.05 * 10.0 && TextServer.BuiltCpuSeconds <= 9.6 / 2.0,
            "texts, the shader cache: the line between used and built lies well between the times measured (0.05 s with the cache, 9.6 to 21 s without)");
        Check(TextServer.ShaderCacheName(TextShaderCache.Used).Contains("cache used") && TextServer.ShaderCacheName(TextShaderCache.Created).Contains("cache created")
              && TextServer.ShaderCacheName(TextShaderCache.Missing).Contains("cache missing") && TextServer.ShaderCacheName(TextShaderCache.Unknown).Contains("unknown")
              && TextServer.ShaderCacheName(TextShaderCache.None) == string.Empty,
            "texts, the shader cache: the start's line says used, created, missing or unknown; nothing on the processor");
        string key = TextServer.ShaderKey(new TextServerStart { Model = @"C:\pack\model\qwen2_v4e1.IQ4_XS.gguf", Device = "Vulkan0" });
        Check(key == "qwen2_v4e1.IQ4_XS.gguf;Vulkan0;" + TextRuntime.Release && key != TextServer.ShaderKey(new TextServerStart { Model = @"C:\pack\model\qwen2_v4e1.IQ4_XS.gguf", Device = "Vulkan1" }),
            "texts, the shader cache: the mark names the model, the device and the runtime, so a new one of any builds as created, not missing (" + key + ")");
        Check(TextServer.ShaderMark.EndsWith(".txt", StringComparison.Ordinal) && !Regex.IsMatch(TextServer.ShaderMark, @"^rlo-head-.*\.bin$"),
            "texts, the shader cache: the mark is never taken by the head files' sweep");
    }

    private static void TextsCacheSources()
    {
        string server = TextsSource(@"Texts\TextServer.cs").Replace("\r\n", "\n");
        int offline = server.IndexOf("Offline(info);\n", StringComparison.Ordinal), own = server.IndexOf("string steam = OwnShaderCache(info);", StringComparison.Ordinal),
            start = server.IndexOf("_process = Process.Start(info);", StringComparison.Ordinal);
        Check(offline > 0 && own > offline && start > own && Regex.Matches(server, @"OwnShaderCache\(info\)").Count == 1,
            "texts, the runner: the server is started without Steam's shader cache variables, once, before Process.Start");
        Check(server.Contains("Stage(TextStartStage.WarmUp, WarmLimit(_start), WarmUp)") && server.Contains("TextJson.Request(TextCard.ChatMl(TestCard()), 1, 1), Ms(WarmLimit(_start)))"),
            "texts, the runner: the first reply's stage and its request both wait the first reply's limit");
        Check(server.Contains("_detail = \"sent the test card, 1 token to write: \" + Timings(answer, took, used) + ShaderCacheName(KeepShaderMark(used));")
              && server.Contains("double took = TextClock.Now - begun, used = Cpu() - cpu;"),
            "texts, the start's line: the first reply's times and processor time, then the shader cache's state from that same processor time");
        Check(server.Contains("(steam.Length > 0 ? \"; the GPU driver's shader cache is its own (\" + steam + \" from Steam not passed on).\" : \".\")"),
            "texts, the start's line: the server's start says which of Steam's variables were not passed on");
    }
}
