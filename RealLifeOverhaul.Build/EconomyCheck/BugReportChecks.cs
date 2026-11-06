using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Meta;

// "Report a bug" (Scripts/Meta/BugReport.cs, BugReportGame.cs, BugReportRules.cs): report.txt's fields in their order from
// made-up facts, the player's user folder taken out wherever it is written and however, a log's tail and its size cap, which
// lines of the game's log are the mod's (with no allocation for a line that is not) and the ring of the last fifty, the
// summary for the clipboard, and over the sources: nothing sent, the logs read with sharing, the button in its place;
// and every new text in the three locales.
internal static partial class Program
{
    private const string BugProfile = @"C:\Users\ann";

    private static readonly string[] BugKeys =
    {
        "reallife_meta_bug_button", "reallife_opt_caption_report", "reallife_opt_sec_help", "reallife_bug_title", "reallife_bug_placeholder", "reallife_bug_make",
        "reallife_bug_ask_menu", "reallife_bug_waits", "reallife_bug_busy", "reallife_bug_done", "reallife_bug_done_folder", "reallife_bug_failed", "reallife_guide_trouble_report"
    };

    private static void BugReports()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        BugText();
        BugMask();
        BugTail();
        BugFilter();
        BugRing();
        BugSummary();
        BugSources();
        BugReportFixes();   // the review's fixes (BugReportFixChecks.cs)
        BugWords();
        Console.WriteLine("  report a bug: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static BugFacts BugFakeFacts()
    {
        var f = new BugFacts
        {
            Words = "The bus left without me.\nTwice, at " + BugProfile + @"\Desktop.",
            Written = "2026-10-15 14:03:22",
            ModVersion = "0.21.2",
            GameBuild = 3682,
            MadeFor = 3682,
            Platform = "WindowsPlayer, Windows 11, a processor, 32768 MB memory",
            Language = "en",
            GoingPublic = "switched on, no session",
            Preset = "Custom, nearest Standard",
            InCity = true,
            Day = 42,
            Hour = 9,
            Minute = 5,
            Money = 1234567.8,
            LinesTotal = 3,
            Contract = "this game is the build the mod is made for; parts off: none",
            Profile = BugProfile,
            PhoneOpen = true
        };
        f.Changed.Add("fuel = 0 (preset 1)");
        f.Changed.Add("bus_fare = 300 (default 250)");
        f.Lines.Add(new BugLine { Kind = BugLineKind.Warning, Ticks = new DateTime(2026, 10, 15, 8, 30, 1, DateTimeKind.Utc).Ticks, Text = "[Mod:Real Life Overhaul] Bus: no stop near " + BugProfile.Replace('\\', '/') + "/x" });
        f.Lines.Add(new BugLine { Kind = BugLineKind.Exception, Ticks = new DateTime(2026, 10, 15, 8, 30, 2, DateTimeKind.Utc).Ticks, Text = "NullReferenceException: Object reference not set",
                                  Stack = "UnityEngine.Something.Run ()\nRealLifeOverhaul.Transport.BusDirector.Tick () (at <abc>:0)\nRealLifeOverhaul.Other.Step ()" });
        f.Lines.Add(new BugLine { Kind = BugLineKind.Error, Ticks = new DateTime(2026, 10, 15, 8, 30, 3, DateTimeKind.Utc).Ticks, Text = "[RealLifeOverhaul] " + new string('x', 600) + "\nsecond line" });
        f.Files.Add("report.txt (this file)");
        f.Files.Add("Player.log (the game's log of this session)");
        return f;
    }

    // ---------------------------------------------------------------- report.txt

    private static void BugText()
    {
        BugFacts f = BugFakeFacts();
        string text = BugReportRules.Text(f);
        string[] order =
        {
            "Real Life Overhaul bug report, written 2026-10-15 14:03:22", "In this report: report.txt (this file), Player.log", "Nothing outside the game's own folders was read",
            "What went wrong: The bus left without me.", "Mod version: 0.21.2", "Game build: 3682 (this version of the mod is made for Build 3682)", "Platform: WindowsPlayer",
            "Language: en", "Going Public: switched on, no session", "Preset: Custom, nearest Standard, 2 option(s) changed from it", "  fuel = 0 (preset 1)",
            "  bus_fare = 300 (default 250)", "Day and time: day 42, 09:05", "Money: about 1,235,000", "Phone: open (the screenshot shows it)", "The mod's warnings and errors this session: 3:",
            "  08:30:01 UTC warning: [Mod:Real Life Overhaul] Bus: no stop near %USERPROFILE%/x", "  08:30:02 UTC exception: NullReferenceException",
            "  08:30:03 UTC error: [RealLifeOverhaul] xxx", "Game contract: this game is the build the mod is made for"
        };
        int at = -1;
        string lost = null;
        foreach (string field in order)
        {
            int next = text.IndexOf(field, at + 1, StringComparison.Ordinal);
            if (next < 0 && lost == null)
            {
                lost = field;
            }
            at = Math.Max(at, next);
        }
        Check(lost == null, "bug report: report.txt gives every field the report needs, in its order (the first missing or out of order: " + (lost ?? "none") + ")");
        Check(text.Contains("  Twice, at %USERPROFILE%\\Desktop."), "bug report: the player's second line is indented under the first, its user folder taken out");
        Check(!text.Contains(BugProfile) && !text.Contains("ann/") && !text.Contains("ann\\"), "bug report: the user folder is nowhere in report.txt");
        Check(text.Contains("| at RealLifeOverhaul.Transport.BusDirector.Tick () (at <abc>:0)") && !text.Contains("RealLifeOverhaul.Other.Step"),
            "bug report: an exception's line names the first place in the mod's code, and only that one");
        Check(text.Contains("[RealLifeOverhaul] " + new string('x', BugReportRules.LineChars - 19) + "...") && !text.Contains(new string('x', BugReportRules.LineChars - 18)) && !text.Contains("second line"),
            "bug report: a log line is its first line, cut to " + BugReportRules.LineChars + " characters");

        f = BugFakeFacts();
        f.Words = "   ";
        f.InCity = false;
        f.Lines.Clear();
        f.LinesTotal = 0;
        f.Changed.Clear();
        f.Preset = "Standard";
        f.GameBuild = 0;
        text = BugReportRules.Text(f);
        Check(text.Contains("What went wrong: (not said)") && text.Contains("Day and time: no save loaded") && text.Contains("Money: no save loaded")
              && text.Contains("this session: 0, none") && text.Contains("Preset: Standard, 0 option(s) changed from it") && text.Contains("Game build: could not be read"),
            "bug report: no words, no save, no warnings, no changes and an unknown build are each said so");
        f = BugFakeFacts();
        for (int i = 0; i < BugLog.Most; i++)
        {
            f.Lines.Add(f.Lines[0]);
        }
        f.LinesTotal = 120;
        Check(BugReportRules.Text(f).Contains("this session: 120, the last " + (BugLog.Most + 3) + ":"), "bug report: more warnings than are kept says how many there were and how many follow");

        Check(BugReportRules.RoundMoney(0) == 0 && BugReportRules.RoundMoney(49) == 0 && BugReportRules.RoundMoney(50) == 100 && BugReportRules.RoundMoney(9949) == 9900
              && BugReportRules.RoundMoney(12499) == 12000 && BugReportRules.RoundMoney(-12500) == -13000 && BugReportRules.RoundMoney(2.5e10) == 25000000000L
              && BugReportRules.RoundMoney(double.NaN) == 0 && BugReportRules.RoundMoney(double.PositiveInfinity) == 0,
            "bug report: money is rounded to 100 under 10,000 and to 1,000 above, a sum that is no number to 0");
        Check(BugReportRules.FolderName(new DateTime(2026, 1, 2, 3, 4, 5)) == "2026-01-02_03-04-05", "bug report: the folder's name is the date and time, sortable and safe in a file name");
    }

    // ---------------------------------------------------------------- the user folder

    private static void BugMask()
    {
        string m = BugReportRules.Mask;
        Check(BugReportRules.MaskUser(@"at C:\Users\ann\AppData\x", BugProfile) == @"at " + m + @"\AppData\x", "bug report: the user folder with backslashes is taken out");
        Check(BugReportRules.MaskUser("at C:/Users/ann/AppData/x", BugProfile) == "at " + m + "/AppData/x", "bug report: the user folder with forward slashes (as the engine writes it) is taken out");
        Check(BugReportRules.MaskUser(@"{""p"":""C:\\Users\\ann\\x""}", BugProfile) == @"{""p"":""" + m + @"\\x""}", "bug report: the user folder with doubled backslashes (as in JSON) is taken out");
        Check(BugReportRules.MaskUser(@"c:\users\ANN\x and C:/USERS/Ann", BugProfile) == m + @"\x and " + m, "bug report: the user folder is taken out in any case, at the end of a text too");
        Check(BugReportRules.MaskUser(@"C:\Users\anna\x C:\Users\ann.b\y C:\Users\ann", BugProfile) == @"C:\Users\<user>\x C:\Users\<user>\y " + m,
            "bug report: another folder that starts with the same name is not taken for the player's own (the rule's pass takes it out as another user's)");
        Check(BugReportRules.MaskUser(@"C:\Users\ann\", BugProfile + "\\") == m + "\\" && BugReportRules.MaskUser("C:/Users/ann/a", "C:/Users/ann") == m + "/a",
            "bug report: the folder is found however it was given");
        Check(BugReportRules.MaskUser(@"C:\x", @"C:\") == @"C:\x" && BugReportRules.MaskUser("a", null) == "a" && BugReportRules.MaskUser(null, BugProfile) == null && BugReportRules.MaskUser(string.Empty, BugProfile) == string.Empty,
            "bug report: a folder too short to be a user's, none, or no text, leaves the text as it is");
        Check(BugReportRules.MaskUser(@"C:\Users\ann C:\Users\ann", BugProfile) == m + " " + m, "bug report: every time it is written, not only the first");
    }

    // ---------------------------------------------------------------- a log's tail

    private static string BugCopy(string log, long cap, out int lines)
    {
        using var from = new MemoryStream(new UTF8Encoding(false).GetBytes(log));
        using var to = new StringWriter();
        lines = BugReportRules.CopyTail(from, to, cap, BugProfile);
        return to.ToString();
    }

    private static void BugTail()
    {
        var log = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            log.Append("line ").Append(i.ToString("0000")).Append(" at C:/Users/ann/AppData ünï 名\n");
        }
        string whole = log.ToString();
        int bytes = Encoding.UTF8.GetByteCount(whole);
        string copy = BugCopy(whole, bytes, out int lines);
        Check(lines == 1000 && copy == whole.Replace("C:/Users/ann", BugReportRules.Mask), "bug report: a log no longer than the cap is copied whole, line for line, the user folder taken out");
        copy = BugCopy(whole, 0, out lines);
        Check(lines == 1000 && !copy.StartsWith("[Real Life Overhaul:", StringComparison.Ordinal), "bug report: no cap copies the whole log");

        long cap = 10000;
        copy = BugCopy(whole, cap, out lines);
        string[] got = copy.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Check(got.Length > 1 && got[0].StartsWith("[Real Life Overhaul: the first ", StringComparison.Ordinal) && got[0].Contains("KB of this log were left out"),
            "bug report: a log over the cap begins with a line that says how much was left out");
        Check(got.Length > 1 && Regex.IsMatch(got[1], @"^line \d{4} at %USERPROFILE%/AppData ünï 名$") && got[got.Length - 1] == "line 0999 at %USERPROFILE%/AppData ünï 名",
            "bug report: the tail starts at a whole line (the one the cut fell in is dropped) and ends with the log's last line");
        Match head = Regex.Match(got.Length > 1 ? got[1] : string.Empty, @"^line (\d{4})");
        int first = head.Success ? int.Parse(head.Groups[1].Value) : -1;
        Check(first >= 0 && lines == 1000 - first && Encoding.UTF8.GetByteCount(copy.Replace(BugReportRules.Mask, "C:/Users/ann")) - Encoding.UTF8.GetByteCount(got[0]) - 1 <= cap
              && Encoding.UTF8.GetByteCount(whole.Substring(whole.IndexOf("line " + first.ToString("0000"), StringComparison.Ordinal))) > cap - 80,
            "bug report: the tail is every line after the cut and no more than the cap (" + lines + " lines)");
        Check(BugReportRules.TailStart(100, 40) == 60 && BugReportRules.TailStart(40, 40) == 0 && BugReportRules.TailStart(0, 40) == 0 && BugReportRules.TailStart(100, 0) == 0,
            "bug report: the cut starts the cap's length before the end, or not at all");
        copy = BugCopy(string.Empty, cap, out lines);
        Check(lines == 0 && copy.Length == 0, "bug report: an empty log is copied as an empty file");
        copy = BugCopy("no newline at the end", cap, out lines);
        Check(lines == 1 && copy == "no newline at the end\n", "bug report: a last line without its newline is kept");
        Check(BugReportRules.LogCap == 6L * 1024 * 1024 && BugReportRules.PrevLogCap < BugReportRules.LogCap && BugReportRules.LogCap + BugReportRules.PrevLogCap < 10L * 1024 * 1024,
            "bug report: the two logs together are capped under Discord's 10 MB for a file, unzipped");
    }

    // ---------------------------------------------------------------- the mod's lines

    private static void BugFilter()
    {
        const int error = BugReportRules.TypeError, assert = BugReportRules.TypeAssert, warning = BugReportRules.TypeWarning, log = BugReportRules.TypeLog, exception = BugReportRules.TypeException;
        const string stack = "Game.Thing.Run ()\nRealLifeOverhaul.Core.Guard.Fail ()";
        Check(BugReportRules.Keep(warning, "[Mod:Real Life Overhaul] Bus: late", null, out BugLineKind k) && k == BugLineKind.Warning, "bug report: a warning the mod's logger wrote is kept, as a warning");
        Check(BugReportRules.Keep(warning, "[RealLifeOverhaul] Meta: odd", null, out k) && k == BugLineKind.Warning, "bug report: a warning the mod wrote to the engine itself is kept");
        Check(BugReportRules.Keep(error, "[Mod:Real Life Overhaul] broke", null, out k) && k == BugLineKind.Error, "bug report: an error the mod wrote is kept, as an error");
        Check(BugReportRules.Keep(exception, "NullReferenceException", stack, out k) && k == BugLineKind.Exception, "bug report: an exception through the mod's code is kept, as an exception");
        Check(BugReportRules.Keep(assert, "Assertion failed", stack, out k) && k == BugLineKind.Error, "bug report: an assertion through the mod's code is kept, as an error");
        Check(!BugReportRules.Keep(log, "[Mod:Real Life Overhaul] Bus: check PASS", stack, out _), "bug report: a plain line, even the mod's, is never kept");
        Check(!BugReportRules.Keep(warning, "A game warning", stack, out _), "bug report: a warning of someone else's with the mod in its stack is not the mod's");
        Check(!BugReportRules.Keep(exception, "NullReferenceException", "Game.Thing.Run ()\nUI.Hud.Update ()", out _), "bug report: the game's own exception is not kept");
        Check(!BugReportRules.Keep(error, "[Mod:Going Public] lost", null, out _) && !BugReportRules.Keep(error, "x [Mod:Real Life Overhaul]", null, out _),
            "bug report: another mod's line, or one that only mentions the mod, is not kept");
        Check(!BugReportRules.Keep(error, null, null, out _) && !BugReportRules.Keep(7, "[Mod:Real Life Overhaul] x", null, out _) && !BugReportRules.Keep(-1, "[Mod:Real Life Overhaul] x", null, out _),
            "bug report: no text, or a type the engine does not have, is not kept");
        BugFilterTime(out double ms, out long bytes, out int kept);
        Check(kept == 50000 && bytes < 1024 && ms < 200.0, "bug report: deciding 200,000 lines of the game's log allocates nothing and takes " + ms.ToString("0.0") + " ms (" + bytes + " bytes, wanted under 200 ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void BugFilterTime(out double ms, out long bytes, out int kept)
    {
        string plain = "Loaded scene 'City' in 2.4 s", theirs = "Shader warning in 'HDRP/Lit': x", mine = "[Mod:Real Life Overhaul] Bus: late";
        string stack = "UnityEngine.Debug:LogWarning (object)\nGame.Thing:Run ()\nGame.Other:Step ()\nUnityEngine.MonoBehaviour:Update ()";
        kept = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 50000; i++)
        {
            kept += BugReportRules.Keep(BugReportRules.TypeLog, plain, stack, out _) ? 1 : 0;
            kept += BugReportRules.Keep(BugReportRules.TypeWarning, theirs, stack, out _) ? 1 : 0;
            kept += BugReportRules.Keep(BugReportRules.TypeException, theirs, stack, out _) ? 1 : 0;
            kept += BugReportRules.Keep(BugReportRules.TypeWarning, mine, stack, out _) ? 1 : 0;
        }
        ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }

    private static void BugRing()
    {
        var ring = new BugLog();
        var into = new List<BugLine>();
        Check(ring.Copy(into) == 0 && into.Count == 0 && ring.Total == 0, "bug report: an empty ring gives nothing");
        ring.Add(BugLineKind.Warning, 1, "one", null);
        Check(ring.Copy(into) == 1 && into.Count == 1 && into[0].Text == "one", "bug report: one line is given back");
        for (int i = 2; i <= 60; i++)
        {
            ring.Add(BugLineKind.Error, i, "n" + i, null);
        }
        long total = ring.Copy(into);
        Check(total == 60 && into.Count == BugLog.Most && into[0].Ticks == 11 && into[BugLog.Most - 1].Ticks == 60 && into.Select(l => l.Ticks).SequenceEqual(Enumerable.Range(11, 50).Select(n => (long)n)),
            "bug report: past " + BugLog.Most + " lines the ring keeps the last " + BugLog.Most + ", oldest first, and counts them all");
        into.Add(default);
        ring.Copy(into);
        Check(into.Count == BugLog.Most, "bug report: the list given is emptied first");
        BugRingAdd(ring, out long bytes);
        Check(bytes < 1024 && ring.Total == 1060, "bug report: keeping a line allocates nothing (" + bytes + " bytes for 1,000 lines)");
        var threads = new BugLog();
        System.Threading.Tasks.Parallel.For(0, 8, t =>
        {
            for (int i = 0; i < 5000; i++)
            {
                threads.Add(BugLineKind.Warning, i, "t", null);
            }
        });
        Check(threads.Total == 40000 && threads.Copy(into) == 40000 && into.Count == BugLog.Most, "bug report: lines from eight threads at once are all counted and the ring stays whole");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void BugRingAdd(BugLog ring, out long bytes)
    {
        ring.Add(BugLineKind.Warning, 0, "warm", null);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 999; i++)
        {
            ring.Add(BugLineKind.Warning, i, "kept", null);
        }
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }

    // ---------------------------------------------------------------- the clipboard

    private static void BugSummary()
    {
        string summary = BugReportRules.Summary("0.21.2", 3682, "  It broke at C:\\Users\\ann\\x  ", @"C:\Users\ann\AppData\LocalLow\Hovgaard Games\Big Ambitions\RealLifeOverhaul\Reports\2026-10-15_14-03-22.zip", BugProfile);
        Check(summary == "Real Life Overhaul 0.21.2, game Build 3682\nWhat went wrong: It broke at %USERPROFILE%\\x\nReport: %USERPROFILE%\\AppData\\LocalLow\\Hovgaard Games\\Big Ambitions\\RealLifeOverhaul\\Reports\\2026-10-15_14-03-22.zip",
            "bug report: the summary gives the versions, the words and where the file is, the user folder taken out: " + summary.Replace("\n", " | "));
        summary = BugReportRules.Summary("0.21.2", 0, new string('w', 5000), "f", BugProfile);
        Check(summary.Contains("game Build ?") && summary.Contains(new string('w', BugReportRules.SummaryWords) + "...") && summary.Length < 2000,
            "bug report: long words are cut so that the summary fits one Discord message (" + summary.Length + " characters)");
        Check(BugReportRules.Summary("1", 1, null, "f", null).Contains("What went wrong: (not said)"), "bug report: no words are said so on the clipboard too");
        Check(BugReportRules.MostWords <= BugReportRules.SummaryWords, "bug report: everything the text box takes fits the summary");
    }

    // ---------------------------------------------------------------- the sources

    private static void BugSources()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string game = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\BugReportGame.cs"));
        string flow = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\BugReport.cs"));
        string rules = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\BugReportRules.cs"));
        string all = game + flow + rules;
        var sends = new Regex(@"\b(WebClient|HttpClient|HttpWebRequest|WebRequest|UnityWebRequest|WWWForm|Socket|TcpClient|UdpClient|SmtpClient)\b|""[^""\n]*webhook|discord(app)?\.com/api", RegexOptions.IgnoreCase);   // (a webhook in a string, not the word in a comment)
        Match send = sends.Match(all);
        Check(!send.Success, "bug report: nothing in it can send anything over the network, and it holds no webhook" + (send.Success ? " (found '" + send.Value + "')" : string.Empty));
        Check(Regex.Matches(game, @"https?://[^\s""]+").Cast<Match>().All(u => u.Value == "https://discord.gg/tzCh2VXs9E") && game.Contains("DiscordUrl = \"https://discord.gg/tzCh2VXs9E\""),
            "bug report: the one address it opens is the mod's Discord invite");
        Check(Regex.IsMatch(game, @"new FileStream\(from, FileMode\.Open, FileAccess\.Read, FileShare\.ReadWrite \| FileShare\.Delete"),
            "bug report: the game's logs are opened for reading with sharing, so the game's own file is never locked");
        Check(game.Contains("Application.consoleLogPath") && game.Contains("Application.persistentDataPath") && !Regex.IsMatch(all, @"GetFolderPath\(Environment\.SpecialFolder\.(?!UserProfile)"),
            "bug report: the logs are read only from the game's own data folder, and the user folder is asked for only to be taken out");
        Check(Regex.IsMatch(flow, @"Task\.Run\(\(\) => Write\(given\)\)") && flow.Contains("BugReportGame.CopyLog(") && flow.Contains("BugReportGame.Zip(") && flow.Contains("BugReportGame.WriteShot("),
            "bug report: the copies, the screen's encoding and the zip are the worker's");
        Check(Regex.IsMatch(game, @"logMessageReceivedThreaded \+= Heard") && Regex.Matches(all, @"logMessageReceived").Count == 1,
            "bug report: the mod's lines are heard once, on any thread, through the one listener");
        string layout = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\OptionsLayout.cs"));
        string tree = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\OptionsTree.cs"));
        string captions = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\OptionsLayout.Captions.cs"));
        string guide = File.ReadAllText(System.IO.Path.Combine(root, @"Meta\Guide.cs"));
        string start = Between(tree, "G(StartId,", ").Flat(),");
        Check(layout.Contains("new ButtonOption(\"reallife_meta_bug_button\", BugReport.Press)") && start.Contains("S(\"help\").Buttons(\"reallife_meta_bug_button\")")
              && captions.Contains("{ \"reallife_meta_bug_button\", \"reallife_opt_caption_report\" }"),
            "bug report: the button is on the options page under Start here (which the Settings app shows too), with its own word");
        Check(guide.Contains("\"reallife_guide_trouble_report\".GetLocalization()"), "bug report: the Guide's Troubleshooting page says how to report a bug");
        string mod = File.ReadAllText(System.IO.Path.Combine(root, @"Core\RealLifeOverhaulMod.cs"));
        Check(mod.Contains("Meta.BugReportGame.Listen"), "bug report: the mod's lines are kept from the game's start");
    }

    // ---------------------------------------------------------------- the texts

    private static void BugWords()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "bug report words: the three locales are where the checks look for them (" + folder + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var missing = new List<string>();
        foreach (string key in BugKeys)
        {
            if (!en.TryGetValue(key, out string e) || !pt.TryGetValue(key, out string p) || !zh.TryGetValue(key, out string z) || e.Length == 0 || p.Length == 0 || z.Length == 0)
            {
                missing.Add(key);
                continue;
            }
            string want = Holes(holes, e);
            if (Holes(holes, p) != want || Holes(holes, z) != want)
            {
                missing.Add(key + " (its {names} differ)");
            }
        }
        Check(missing.Count == 0, "bug report words: every text is in en.json, pt.json and zh-cn.json with the same {names} (" + BugKeys.Length + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        if (missing.Count > 0)
        {
            return;
        }
        Check(new[] { "reallife_bug_waits", "reallife_bug_busy", "reallife_bug_done", "reallife_bug_done_folder" }.All(k => en[k].Length <= 240) && en["reallife_bug_failed"].Length + 120 <= 240,
            "bug report words: each notification fits the game's toast, the failure with its error filled (240 characters)");
        Check(en["reallife_bug_failed"].Contains("{error}") && BugKeys.Where(k => k != "reallife_bug_failed" && k != "reallife_guide_trouble_report").All(k => !en[k].Contains("{")),
            "bug report words: only the failure has a name to fill, its error");
        foreach (Dictionary<string, string> words in new[] { en, pt, zh })
        {
            Check(words["reallife_bug_done"].Contains("#bug-reports") && words["reallife_bug_done_folder"].Contains("#bug-reports") && words["reallife_bug_ask_menu"].Contains("#bug-reports")
                  && words["reallife_guide_trouble_report"].Contains("#bug-reports") && words["reallife_bug_failed"].Contains("#bug-reports"),
                "bug report words: every text that says where to post names #bug-reports, in each language");
        }
        Check(en["reallife_meta_bug_button"] == "Report a bug" && en["reallife_bug_title"] == en["reallife_meta_bug_button"] && pt["reallife_bug_title"] == pt["reallife_meta_bug_button"]
              && zh["reallife_bug_title"] == zh["reallife_meta_bug_button"], "bug report words: the button and the notices' title are the same words, in each language");
        Check(en["reallife_guide_trouble_report"].StartsWith("# ", StringComparison.Ordinal) && pt["reallife_guide_trouble_report"].StartsWith("# ", StringComparison.Ordinal)
              && zh["reallife_guide_trouble_report"].StartsWith("# ", StringComparison.Ordinal), "bug report words: the Guide's section has its heading in each language");
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Report a bug from inside the game"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "bug report words: What's new has the feature's line, with no prefix");
        var style = new Regex("[\u2013\u2014!]| - ");
        Check(BugKeys.All(k => !style.IsMatch(en[k].Replace("\n- ", "\n"))), "bug report words: no dashes and no exclamation marks in the English texts");
    }
}
