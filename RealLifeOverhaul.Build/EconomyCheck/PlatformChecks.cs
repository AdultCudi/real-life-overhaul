using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Texts;
using RealLifeOverhaul.Ui;

// The mod on a Mac (docs/standards/platforms.md). A player's log from macOS, version 1.0.0, showed the mod loading and
// running there with no exception, with Windows' own calls answered "not available". Read against that log, the code
// would have left the game's log out of a bug report (a Mac keeps Player.log apart from the game's data), named the
// wrong folder for it, and tried to start llama.cpp's Windows program for a player with an RLO Texts pack. Held here,
// game-free: which system this is, where the log is, the report's rules for a Mac's paths, the packs standing aside, a
// key held for the session, the bundles' check and its line, and the sources that carry each out. Nothing here runs on
// a Mac: each rule is given a Mac's own paths and answers.
internal static partial class Program
{
    private const string MacHome = "/Users/ann";
    private const string MacData = MacHome + "/Library/Application Support/com.Hovgaard-Games.Big-Ambitions";   // as the player's log names it
    private const string MacLog = MacHome + "/Library/Logs/Hovgaard Games/Big Ambitions/Player.log";
    private const string BundleStem = "reallife";   // a bundle's name is written in two parts, so no check takes it for a text's key
    private const string WinData = @"C:\Users\ann\AppData\LocalLow\Hovgaard Games\Big Ambitions";

    private static void HostPlatform()
    {
        PlatformSystemChecks();
        PlatformLogChecks();
        PlatformReportChecks();
        PlatformTextsChecks();
        PlatformBundleChecks();
        PlatformSourceChecks();
        PlatformWordChecks();
    }

    private static void PlatformSystemChecks()
    {
        Check(HostSystem.Windows == (Environment.OSVersion.Platform == PlatformID.Win32NT) && (HostSystem.Windows ? HostSystem.Name == "Windows" : HostSystem.Name != "Windows")
              && !string.IsNullOrEmpty(HostSystem.Name),
            "platform: the system is told without the engine, Windows by its own name (here: " + HostSystem.Name + ")");
    }

    private static void PlatformLogChecks()
    {
        Check(LogPlaceRules.Folder(WinData + @"\Player.log", WinData) == WinData && LogPlaceRules.Folder(WinData.Replace('\\', '/') + "/Player.log", "x") == WinData.Replace('\\', '/'),
            "platform, the log: on Windows it is in the game's data folder, written with either slash");
        Check(LogPlaceRules.Folder(MacLog, MacData) == MacHome + "/Library/Logs/Hovgaard Games/Big Ambitions" && LogPlaceRules.Folder(MacLog, MacData) != MacData,
            "platform, the log: on a Mac it is under Library/Logs, apart from the game's data, and that is the folder given");
        Check(LogPlaceRules.Folder(null, MacData) == MacData && LogPlaceRules.Folder(string.Empty, WinData) == WinData && LogPlaceRules.Folder("Player.log", WinData) == WinData,
            "platform, the log: with no log named by the engine, or one with no folder, the game's data folder stands in");
        Check(LogPlaceRules.Name == "Player.log", "platform, the log: its name is the engine's own");
    }

    private static void PlatformReportChecks()
    {
        Check(BugReportRules.OwnLog(WinData + @"\Player.log", WinData, @"C:\Users\ann") && BugReportRules.OwnLog(WinData.Replace('\\', '/') + "/Player.log", WinData + "\\", @"C:\Users\ann")
              && BugReportRules.OwnLog(WinData.ToUpperInvariant() + @"\Player.log", WinData, null),
            "platform, the report: on Windows the game's log in its own data folder is read, however the folder is written");
        Check(BugReportRules.OwnLog(MacLog, MacData, MacHome) && BugReportRules.OwnLog(MacLog, MacData, MacHome + "/"),
            "platform, the report: on a Mac the game's log under the user's Library/Logs is read, though it is not in the game's data folder");
        Check(!BugReportRules.OwnLog("/tmp/game.log", MacData, MacHome) && !BugReportRules.OwnLog(@"D:\logs\Player.log", WinData, @"C:\Users\ann")
              && !BugReportRules.OwnLog(MacHome + "/Library/Logs/Hovgaard Games/Big Ambitions/other.log", MacData, MacHome)
              && !BugReportRules.OwnLog(MacHome + "/Library/Logs/Player.log", MacData, MacHome) && !BugReportRules.OwnLog("/Users/bob/Library/Logs/Game/Player.log", MacData, MacHome)
              && !BugReportRules.OwnLog(MacLog, MacData, null) && !BugReportRules.OwnLog(null, MacData, MacHome) && !BugReportRules.OwnLog(string.Empty, MacData, MacHome)
              && !BugReportRules.OwnLog("Player.log", MacData, MacHome),
            "platform, the report: a log sent elsewhere, another file there, another user's, or none at all is not read");
        const string Reports = MacData + "/RealLifeOverhaul/Reports";
        Check(BugReportRules.FileUrl(Reports, false) == "file:///Users/ann/Library/Application%20Support/com.Hovgaard-Games.Big-Ambitions/RealLifeOverhaul/Reports"
              && BugReportRules.FileUrl("/home/jo\u00E3o/x y", false) == "file:///home/jo%C3%A3o/x%20y",
            "platform, the report: the folder's address for a Mac has every part escaped, since it opens none that holds a space (\"Application Support\")");
        Check(BugReportRules.FileUrl("C:\\Users\\Jo\u00E3o\\AppData\\LocalLow\\Hovgaard Games\\x", true) == "file:///C:/Users/Jo\u00E3o/AppData/LocalLow/Hovgaard Games/x"
              && BugReportRules.FileUrl(Reports) == BugReportRules.FileUrl(Reports, HostSystem.Windows),
            "platform, the report: for Windows the address stays plain, as before, and the system decides which is given");
        Check(BugReportRules.MaskUser("Mono path[0] = '" + MacHome + "/Library/Application Support/Steam/steamapps/common/Big Ambitions/Big Ambitions.app/Contents/Resources/Data/Managed'", MacHome)
              == "Mono path[0] = '%USERPROFILE%/Library/Application Support/Steam/steamapps/common/Big Ambitions/Big Ambitions.app/Contents/Resources/Data/Managed'"
              && BugReportRules.MaskUser("Compressed to " + MacData + "/SaveGames/x.hsg in 519 ms", MacHome) == "Compressed to %USERPROFILE%/Library/Application Support/com.Hovgaard-Games.Big-Ambitions/SaveGames/x.hsg in 519 ms"
              && BugReportRules.MaskUser("at /Users/annabel/x", MacHome) == "at /Users/" + BugReportRules.OtherUser + "/x",
            "platform, the report: a Mac's user folder is taken out of a log's lines as Windows' is, and a longer name that starts the same is another user's");
        Check(BugReportRules.MaskUser("read /Users/bob/Library/x and '/Users/Shared/y'", MacHome) == "read /Users/" + BugReportRules.OtherUser + "/Library/x and '/Users/Shared/y'"
              && BugReportRules.MaskUser("C:/Users/Public/x and D:/data/Users/list.txt", @"C:\Users\ann") == "C:/Users/Public/x and D:/data/Users/list.txt"
              && BugReportRules.MaskUser(@"C:\Users\bob\x", @"C:\Users\ann") == @"C:\Users\" + BugReportRules.OtherUser + @"\x",
            "platform, the report: another user of a Mac is taken out too, the folder everyone shares is left, and Windows' paths are judged as before");
    }

    private static void PlatformTextsChecks()
    {
        Check(TextPacks.WorkshopOf(@"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data", "1331550") == @"G:\Steam\steamapps\workshop\content\1331550",
            "platform, RLO Texts: the Workshop's folder is found from the Windows game's data folder as before");
        string mac = TextPacks.WorkshopOf(MacHome + "/Library/Application Support/Steam/steamapps/common/Big Ambitions/Big Ambitions.app/Contents", "1331550");
        Check(mac != null && mac.Replace('\\', '/').EndsWith("/Library/Application Support/Steam/steamapps/workshop/content/1331550", StringComparison.Ordinal),
            "platform, RLO Texts: and from a Mac's, which lies two folders deeper (Big Ambitions.app/Contents): the folder named steamapps is looked for, not a number of steps");
        Check(TextPacks.WorkshopOf("/Applications/Big Ambitions.app/Contents", "1331550") == null && TextPacks.WorkshopOf(@"D:\Games\Big Ambitions\Big Ambitions_Data", "1331550") == null,
            "platform, RLO Texts: a game that is not under Steam has no Workshop folder");
        Check(TextPacks.ShortHome(WinData + @"\RLO Texts") == "LocalLow/\u2026/RLO Texts" && TextPacks.ShortHome(MacData + "/RLO Texts") == "Application Support/\u2026/RLO Texts"
              && TextPacks.ShortHome("/home/jo/.config/unity3d/Hovgaard Games/Big Ambitions/RLO Texts") == ".config/\u2026/RLO Texts"
              && TextPacks.ShortHome("/odd/place/RLO Texts") == "RLO Texts" && TextPacks.ShortHome(null) == TextPacks.Fixed,
            "platform, RLO Texts: the packs' folder in a notice is named from where each system keeps a game's data, as short as before on Windows");

        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-platform-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var session = new TextKeys(folder, true);
            bool kept = session.Set("key.2", "sk-session-0123456789");
            Check(session.SessionOnly && kept && session.Why == null && session.Get("key.2") == "sk-session-0123456789" && session.Has("key.2")
                  && !File.Exists(System.IO.Path.Combine(folder, TextKeys.FileName)) && Directory.GetFiles(folder).Length == 0,
                "platform, your own AI: where Windows' protection is not there a key is held for the session, and nothing at all is written");
            Check(new TextKeys(folder, true).Get("key.2") == null, "platform, your own AI: such a key is gone with the session: the next one starts without it");
            session.Set("key.2", null);
            Check(!session.Has("key.2") && Directory.GetFiles(folder).Length == 0, "platform, your own AI: a session's key is forgotten the same way, with nothing written");
            if (HostSystem.Windows)
            {
                var sealedKeys = new TextKeys(folder);
                Check(!sealedKeys.SessionOnly && sealedKeys.Set("key.3", "sk-sealed-0123456789") && File.Exists(System.IO.Path.Combine(folder, TextKeys.FileName)),
                    "platform, your own AI: on Windows a key is sealed and written as before");
                Check(new TextKeys(folder, true).Get("key.3") == null && new TextKeys(folder).Get("key.3") == "sk-sealed-0123456789",
                    "platform, your own AI: a file sealed on Windows is not opened where the protection is not there, and is left as it is");
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static void PlatformBundleChecks()
    {
        string[] six = { BundleStem + "_anim", BundleStem + "_character", BundleStem + "_props", BundleStem + "_props_2", BundleStem + "_props_site", BundleStem + "_ui" };
        var listed = new List<string>(BundleShaderRules.Bundles);
        listed.Sort(StringComparer.Ordinal);
        Check(string.Join(",", listed) == string.Join(",", six) && Array.IndexOf(BundleShaderRules.Bundles, UiShaderRules.Bundle) >= 0,
            "platform, bundles: the six bundles the mod ships are listed, the fonts' among them");
        string bundles = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Bundles"));
        var files = new List<string>();
        foreach (string path in Directory.Exists(bundles) ? Directory.GetFiles(bundles) : new string[0])
        {
            if (!path.EndsWith(".meta", StringComparison.Ordinal) && !path.EndsWith(".manifest", StringComparison.Ordinal))
            {
                files.Add(System.IO.Path.GetFileName(path));
            }
        }
        files.Sort(StringComparer.Ordinal);
        Check(string.Join(",", files) == string.Join(",", six), "platform, bundles: and they are the files that ship, letter for letter (" + string.Join(", ", files) + ")");
        Check(BundleShaderRules.Fallback(UiShaderRules.Text) == UiShaderRules.TextSmall && BundleShaderRules.Fallback("HDRP/Lit") == null && BundleShaderRules.Fallback(null) == null
              && BundleShaderRules.Fallback(UiShaderRules.TextSmall) == null,
            "platform, bundles: only a pair of shaders written down is ever swapped: the text shader for its lighter cut, and nothing for the lit shader");
        Check(BundleShaderRules.BringsShader("assets/generated/overpass-bold sdf material.mat") && BundleShaderRules.BringsShader("Assets/X.SHADER") && BundleShaderRules.BringsShader("a.shadergraph")
              && !BundleShaderRules.BringsShader("assets/props/bench.fbx") && !BundleShaderRules.BringsShader("props_base.png") && !BundleShaderRules.BringsShader("catalogue.json")
              && !BundleShaderRules.BringsShader(null) && !BundleShaderRules.BringsShader("format.material.txt"),
            "platform, bundles: a name in a bundle's list that is a material or a shader is told by its ending");
        string with = BundleShaderRules.With("HDRP/Lit", true, true, 0);
        string props = BundleShaderRules.Line(BundleStem + "_props", 0, 0, 0, 0, 0, null, true, with, "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        Check(props == "Shaders check (" + BundleStem + "_props): 0 materials, 0 shaders, 0 not supported on Metal, 0 repointed to the game's own, 0 left: PASS. Its list names no material or shader; "
                       + "its pieces are drawn with the game's 'HDRP/Lit', which draws here. Graphics: Metal (Apple M4 Pro). System: Mac OS X 15.1.0. Game: Build 3682.",
            "platform, bundles: the line of a bundle that brought no material says so, what its pieces are drawn with, the graphics device, the system and the game");
        string fonts = UiShaderRules.Line(BundleStem + "_props", 2, 1, 1, 1, 0, null, "x", "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        string same = BundleShaderRules.Line(BundleStem + "_props", 2, 1, 1, 1, 0, null, true, "x", "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        int head = fonts.IndexOf("left: PASS.", StringComparison.Ordinal);
        Check(head > 0 && same.StartsWith(fonts.Substring(0, head + 11), StringComparison.Ordinal) && fonts.EndsWith("Game: Build 3682.", StringComparison.Ordinal)
              && same.EndsWith("Graphics: Metal (Apple M4 Pro). System: Mac OS X 15.1.0. Game: Build 3682.", StringComparison.Ordinal),
            "platform, bundles: every bundle's line begins and ends with the same words as the fonts' line, so one search of a log finds all six");
        string fail = BundleShaderRules.Line(BundleStem + "_props_2", 2, 1, 1, 0, 2, "Bench, Lamp", false, BundleShaderRules.With("HDRP/Lit", true, false, 0), "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        Check(fail.Contains("1 not supported on Metal, 0 repointed to the game's own, 2 left: FAIL (Bench, Lamp: ") && !fail.Contains("PASS") && fail.Contains("which does not draw here")
              && fail.IndexOf('\n') < 0,
            "platform, bundles: materials left on a shader that does not draw are a FAIL that names them, on one line");
        string bare = BundleShaderRules.Line(BundleStem + "_props", 0, 0, 0, 0, 0, null, false, BundleShaderRules.With("HDRP/Lit", false, false, 0), "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        Check(bare.Contains("0 left: FAIL (the game's shader for the bundle's pieces is not there to draw them). ") && !bare.Contains("PASS") && bare.Contains("which was not found")
              && BundleShaderRules.Passes(0, true) && !BundleShaderRules.Passes(0, false) && !BundleShaderRules.Passes(1, true),
            "platform, bundles: a bundle whose pieces' own shader the game has not got, or cannot draw, is a FAIL too, though no material is left");
        Check(BundleShaderRules.With(null, false, false, 0) == "Its list names no material or shader; nothing of it is drawn with a material of its own"
              && BundleShaderRules.With("HDRP/Lit", false, false, 0).EndsWith("which was not found", StringComparison.Ordinal)
              && BundleShaderRules.With(null, false, false, 2).StartsWith("Its list names 2 material(s) or shader(s) that were not read", StringComparison.Ordinal),
            "platform, bundles: what a bundle's pieces are drawn with has its words for clips, for a shader not found, and for names that were not read");
    }

    private static void PlatformSourceChecks()
    {
        string clock = ModSource(@"Diagnostics\MachineClock.cs"), cores = ModSource(@"Texts\TextCores.cs"), keys = ModSource(@"Texts\TextKeys.cs");
        string process = ModSource(@"Texts\TextServer.Process.cs"), start = ModSource(@"Texts\TextServer.Start.cs"), packs = ModSource(@"Texts\TextPacks.cs");
        Check(MemberText(clock, "public static void Sample()").Contains("if (!HostSystem.Windows)") && Occurs(clock, "HostSystem.Windows && ") == 4
              && MemberText(clock, "public static void Start()").Contains("if (!_tried && !HostSystem.Windows)"),
            "platform, sources: the frame clock makes none of its four Windows calls on another system, and says each figure is not available");
        Check(MemberText(cores, "public static int Real()").Contains("if (HostSystem.Windows)") && MemberText(keys, "private static byte[] Crypt(byte[] data, bool seal)").Contains("if (!HostSystem.Windows)")
              && MemberText(keys, "public static bool Sealable()").Contains("if (!HostSystem.Windows)"),
            "platform, sources: the cores are asked of Windows only on Windows, and its protection of a user's data likewise");
        Check(MemberText(process, "private void TieToGame(Process p)").Contains("if (!HostSystem.Windows)") && MemberText(process, "private void FullSpeed(Process p)").Contains("if (!HostSystem.Windows)")
              && MemberText(process, "private void Kill()").Contains("if (HostSystem.Windows && job != IntPtr.Zero)"),
            "platform, sources: the server's job object and power setting are Windows' and are touched there alone");
        string launch = MemberText(start, "private bool Launch()"), devices = MemberText(packs, "public static string ListDevices(string exe, int timeoutMs)");
        Check(launch.IndexOf("if (!HostSystem.Windows)", StringComparison.Ordinal) > 0 && launch.IndexOf("if (!HostSystem.Windows)", StringComparison.Ordinal) < launch.IndexOf("Process.Start(", StringComparison.Ordinal)
              && devices.IndexOf("if (!HostSystem.Windows)", StringComparison.Ordinal) > 0
              && devices.IndexOf("if (!HostSystem.Windows)", StringComparison.Ordinal) < devices.IndexOf("Process.Start(", StringComparison.Ordinal),
            "platform, sources: llama.cpp's Windows program is never started on another system, for its devices or as the server");
        string director = ModSource(@"Texts\TextsDirector.cs"), status = ModSource(@"Texts\TextsDirector.Status.cs"), service = ModSource(@"Texts\TextsDirector.Service.cs");
        string find = MemberText(director, "private static void Find()"), run = MemberText(director, "private static void Run()"), aside = MemberText(director, "private static void Aside()");
        Check(find.Contains("_aside = !HostSystem.Windows;") && find.Contains("i < _packs.Count && !_aside;") && find.IndexOf("_aside = ", StringComparison.Ordinal) < find.IndexOf("_found = true;", StringComparison.Ordinal)
              && director.Contains("private static volatile bool _aside;") && run.IndexOf("if (_aside)", StringComparison.Ordinal) > 0
              && run.IndexOf("if (_aside)", StringComparison.Ordinal) < run.IndexOf("TextPacks.Pick(", StringComparison.Ordinal),
            "platform, sources: on another system RLO Texts counts the packs and checks, asks and starts nothing of one");
        Check(aside.Contains("Meta.Dev.Always(Logger, ") && aside.Contains("TextsGame.Notice(\"reallife_texts_status_windows\"") && aside.Contains("!Told(\"windows\")") && aside.Contains("Remember(\"windows\");")
              && aside.Contains("if (_asideTold)") && MemberText(status, "public static Dictionary<string, string> Status(out string key)").Contains("key = \"reallife_texts_status_windows\";"),
            "platform, sources: it says so once in the log, once ever to a player who has a pack, and in its status, and the written lines go on");
        Check(service.Contains("Keys.SessionOnly ? \"reallife_textsai_key_session\" : \"reallife_textsai_key_kept\"")
              && MemberText(service, "public static void SignIn()").Contains("TextsGame.Notice(\"reallife_textsai_signin_windows\", null, 16f);")
              && MemberText(service, "public static void SignIn()").IndexOf("Keys.SessionOnly", StringComparison.Ordinal) < MemberText(service, "public static void SignIn()").IndexOf("TextKeys.Sealable()", StringComparison.Ordinal),
            "platform, sources: a key held for the session is told as that, and the ChatGPT sign-in says it needs Windows");
        string report = ModSource(@"Meta\BugReportGame.cs");
        Check(MemberText(report, "public static string GameLog()").Contains("BugReportRules.OwnLog(full, Path.GetFullPath(Application.persistentDataPath), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))"),
            "platform, sources: the bug report takes the game's log from where the engine says it is, by the rule");
        foreach (string file in new[] { @"Performance\PerfProbe.cs", @"Performance\PerfBench.cs", @"Diagnostics\CharacterLab.cs", @"Meta\Guide.cs" })
        {
            Check(ModSource(file).Contains("LogPlaceRules.Folder(Application.consoleLogPath, Application.persistentDataPath)"), "platform, sources: " + file + " names the folder the log is in by the rule");
        }
        Check(ModSource(@"Core\PropLibrary.cs").Contains("BundleShaders.Check(Logger, into.Name, null, reading.allAssets, LitShader);")
              && ModSource(@"CityLife\ClipLibrary.cs").Contains("BundleShaders.Check(Logger, Path.GetFileName(BundlePath), null, reading.allAssets, null);")
              && ModSource(@"Character\ModAssets.cs").Contains("BundleShaders.Check(Logger, Path.GetFileName(BundlePath), _bundle, null, null);"),
            "platform, sources: the props', the clips' and the looks' bundles are each checked as they are read");
        string shaders = ModSource(@"Core\BundleShaders.cs");
        Check(shaders.Contains("Meta.Dev.Always(logger, line);") && shaders.Contains("logger?.Warn(line);") && shaders.Contains("m.shaderKeywords = keywords;") && shaders.Contains("m.renderQueue = queue;")
              && shaders.Contains("SceneFinds.InMemory<Shader>(") && !Regex.IsMatch(shaders, @"\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(|FindObjectsOfTypeAll"),
            "platform, sources: the bundles' check writes its line whatever Developer diagnostics says, keeps a material's keywords and queue, and runs in no frame");
    }

    private static void PlatformWordChecks()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null, "platform texts: en.json is read");
        if (en == null)
        {
            return;
        }
        foreach (string key in new[] { "reallife_texts_status_windows", "reallife_textsai_key_session", "reallife_textsai_signin_windows" })
        {
            Check(en.TryGetValue(key, out string text) && text.Length > 20 && text.Length <= 240, "platform texts: " + key + " is there, a notice's length");
        }
        Check(en["reallife_textsai_key_session"].Contains("{service}") && en["reallife_texts_status_windows"].Contains("Windows only"), "platform texts: the session key's notice names the service, the packs' says Windows only");
        Check(WhatsNewHas(en, "Fixed: on a Mac, a bug report left out the game's log") && WhatsNewHas(en, "RLO Texts tried to run its packs, which only Windows can"),
            "platform texts: What's new tells of the fixes in some row");
        Check(en["reallife_guide_trouble"].Contains("A Mac keeps the log apart") && en["reallife_guide_limits"].Contains("It also runs on a Mac")
              && en["reallife_guide_textsai"].Contains("On a Mac nothing protects it that way") && en["reallife_guide_texts"].Contains("The packs run on Windows only")
              && en["reallife_guide_trouble_report"].Contains("Your user name on this computer"),
            "platform texts: the Guide says where a Mac keeps the log and what the mod does without there");
    }
}
