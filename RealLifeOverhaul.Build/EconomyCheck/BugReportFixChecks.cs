using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RealLifeOverhaul.Meta;

// The review's fixes to "Report a bug" (Scripts/Meta/BugReport*.cs): the folder opened without an escaped address, Steam ids
// and names taken out, a file that fails said in report.txt while the rest still come, the failure's message masked and cut,
// the user folder found by rule (other users, 8.3 names, JSON, percent-encoded, Proton's home, a full stop after it), a press
// that fails said, the newest five reports kept, a cut on a line's start keeping that line, a second report in the same
// second, and the open phone said.
internal static partial class Program
{
    private static void BugReportFixes()
    {
        BugFixOpen();
        BugFixSteam();
        BugFixParts();
        BugFixError();
        BugFixForms();
        BugFixPress();
        BugFixKeep();
        BugFixLineStart();
        BugFixUnique();
        BugFixPhone();
    }

    private static string BugSource(string file)
    {
        return File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Meta")), file));
    }

    // 1. The folder: Explorer on the zip, else a file address that is not escaped.
    private static void BugFixOpen()
    {
        Check(BugReportRules.FileUrl(@"C:\Users\João\AppData\LocalLow\Hovgaard Games\Big Ambitions\RealLifeOverhaul\Reports") == "file:///C:/Users/João/AppData/LocalLow/Hovgaard Games/Big Ambitions/RealLifeOverhaul/Reports"
              && BugReportRules.FileUrl(@"C:\Users\张伟\x") == "file:///C:/Users/张伟/x" && BugReportRules.FileUrl("/home/deck/x") == "file:///home/deck/x",
            "bug fixes: the folder's address is not escaped, so Windows does not read 'João' or a Chinese name in its own code page");
        Check(BugReportRules.ExplorerSelect(@"C:\Users\João\r.zip") == "/select,\"C:\\Users\\João\\r.zip\"", "bug fixes: the explorer is asked to pick the zip, its path in quotes");
        string game = BugSource("BugReportGame.cs");
        Check(game.Contains("Process.Start(\"explorer.exe\", BugReportRules.ExplorerSelect(picked))") && game.Contains("Application.OpenURL(BugReportRules.FileUrl(")
              && !game.Contains("new Uri(") && game.Contains("RuntimePlatform.WindowsPlayer") && game.Contains("Explore(zip ?? folder)"),
            "bug fixes: on Windows the explorer is started on the zip (in a try), else and elsewhere the unescaped address is opened");
    }

    // 2. Steam ids and names.
    private static void BugFixSteam()
    {
        string m(string t) => BugReportRules.MaskUser(t, BugProfile);
        Check(m("[GoingPublic] Stable id: steam-76561198012345678") == "[GoingPublic] Stable id: steam-<steamid>", "bug fixes: the player's Steam id is taken out");
        Check(m("[Steam] Signed in. id=76561198012345678 name='Ann B'") == "[Steam] Signed in. id=<steamid> name='<name>'", "bug fixes: the Steam line's id and name are taken out");
        Check(m("[GoingPublic] Using configured player name: Ann B") == "[GoingPublic] Using configured player name: <name>", "bug fixes: the configured player name is taken out");
        Check(m("peer joined ip=steam:76561198099999999 port 7777") == "peer joined ip=steam:<steamid> port 7777", "bug fixes: another player's Steam id is taken out");
        Check(m("x 7656119801234567 y 765611980123456789 z 86561198012345678") == "x 7656119801234567 y 765611980123456789 z 86561198012345678",
            "bug fixes: a number that is not a 17-digit Steam id is left alone");
        Check(m("Prop name='Desk A' placed") == "Prop name='Desk A' placed", "bug fixes: a quoted name on a line that is not Steam's is left alone");
        using (var from = new MemoryStream(Encoding.UTF8.GetBytes("a\n[Steam] id=76561198012345678 name='Ann'\nb\n")))
        using (var to = new StringWriter())
        {
            BugReportRules.CopyTail(from, to, 0, BugProfile);
            Check(to.ToString() == "a\n[Steam] id=<steamid> name='<name>'\nb\n", "bug fixes: the copied log has the ids and names taken out");
        }
        string text = BugReportRules.Text(BugFakeFacts());
        Check(text.Contains("Steam ids as <steamid>") && text.Contains("Names, such as your Steam name, may still be in the logs: look through the zip before you post it"),
            "bug fixes: report.txt says Steam ids are taken out and names may still be there");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en["reallife_guide_trouble_report"].Contains("look through the zip before you post it") && pt["reallife_guide_trouble_report"].Contains("confira o zip antes de publicá-lo")
              && zh["reallife_guide_trouble_report"].Contains("请先检查一遍 zip"), "bug fixes: the Guide says names may still be in the log, in each language");
    }

    // 3. A file that fails is said, and the rest still come.
    private static void BugFixParts()
    {
        Check(BugReportRules.NotRead("Player.log", "IOException: denied at " + BugProfile + "\\x\nmore", BugProfile) == "Player.log: could not be read (IOException: denied at %USERPROFILE%\\x)",
            "bug fixes: a file that failed is listed with its reason's first line, the user folder taken out");
        BugFacts f = BugFakeFacts();
        f.Files.Add(BugReportRules.NotRead("screenshot.jpg", "out of memory", null));
        Check(BugReportRules.Text(f).Contains("In this report: report.txt (this file), Player.log (the game's log of this session), screenshot.jpg: could not be read (out of memory)."),
            "bug fixes: report.txt's list says what could not be read");
        string flow = BugSource("BugReport.cs");
        string write = Between(flow, "private static void Write(Job job)", "private static void Part(");
        Check(write.Contains("Part(f, \"Player.log\"") && write.Contains("Part(f, \"Player-prev.log\"") && write.Contains("Part(f, Presets.SettingsFile") && write.Contains("Part(f, \"screenshot.jpg\""),
            "bug fixes: each optional file is written on its own (both logs, the settings, the screenshot)");
        string part = Between(flow, "private static void Part(", "private static void Clear(");
        Check(part.Contains("catch (Exception e)") && part.Contains("BugReportRules.NotRead(name,"), "bug fixes: a file that fails is caught on its own and said in the list");
    }

    // 4. The failure's message.
    private static void BugFixError()
    {
        string message = "Access to the path '" + BugProfile + "\\AppData\\LocalLow\\Hovgaard Games\\Big Ambitions\\RealLifeOverhaul\\Reports\\2026-10-15_14-03-22\\Player.log' is denied, and a long tail " + new string('z', 300);
        string shown = BugReportRules.ErrorText(message, BugProfile);
        Check(!shown.Contains(BugProfile) && !shown.Contains("ann") && shown.StartsWith("Access to the path '%USERPROFILE%", StringComparison.Ordinal)
              && shown.Length <= BugReportRules.ErrorChars + 3 && shown.EndsWith("...", StringComparison.Ordinal), "bug fixes: the failure's message is masked and cut (" + shown.Length + " characters)");
        Check(BugReportRules.ErrorText(null, null) == string.Empty, "bug fixes: no message is an empty one");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales")), "en.json"));
        Check(en["reallife_bug_failed"].Replace("{error}", shown).Length <= 240, "bug fixes: the failure notification fits the toast with the longest message");
        Check(BugSource("BugReport.cs").Contains(".Replace(\"{error}\", BugReportRules.ErrorText(e.Message,"), "bug fixes: the failure notification shows the masked, cut message");
    }

    // 5. The user folder by rule.
    private static void BugFixForms()
    {
        string m(string t) => BugReportRules.MaskUser(t, BugProfile);
        var cases = new (string In, string Out)[]
        {
            (@"at C:\Users\ANNSMI~1\AppData\Local\Temp", @"at C:\Users\<user>\AppData\Local\Temp"),
            ("at c:/USERS/bob/x", "at c:/USERS/<user>/x"),
            ("at C:/Users\\bob/x", "at C:/Users\\<user>/x"),
            ("{\"p\":\"D:\\\\Users\\\\bob\\\\x\"}", "{\"p\":\"D:\\\\Users\\\\<user>\\\\x\"}"),
            ("{\"p\":\"C:\\/Users\\/bob\\/x\"}", "{\"p\":\"C:\\/Users\\/<user>\\/x\"}"),
            ("file:///C:/Users/Jo%C3%A3o/AppData", "file:///C:/Users/<user>/AppData"),
            ("C%3A%5CUsers%5Cbob%5Cx", "C%3A%5CUsers%5C<user>%5Cx"),
            ("Z:\\home\\deck\\.steam and /home/steamuser/.local/share", "Z:\\home\\<user>\\.steam and /home/<user>/.local/share"),
            (@"in C:\Users\bob. Then", @"in C:\Users\<user>. Then"),
            (@"in C:\Users\bob.", @"in C:\Users\<user>."),
            (@"in C:\Users\bob is", @"in C:\Users\<user> is"),
            (@"in C:\Users\ann. Then C:\Users\ann.", @"in %USERPROFILE%. Then %USERPROFILE%."),
            (@"C:\Users\Public\Documents and C:\Users\Default\x", @"C:\Users\Public\Documents and C:\Users\Default\x"),
            ("no folder here, users of the bus", "no folder here, users of the bus"),
        };
        string wrong = null;
        foreach (var c in cases)
        {
            string got = m(c.In);
            if (got != c.Out && wrong == null)
            {
                wrong = c.In + " gave " + got;
            }
        }
        Check(wrong == null, "bug fixes: every other form of a user folder is taken out by rule, the shared ones left (" + (wrong ?? "all " + cases.Length) + ")");
        Check(BugReportRules.MaskUser(@"C:\Users\bob\x", null) == @"C:\Users\<user>\x", "bug fixes: the rule's pass runs without the profile too");
    }

    // 6. A press that fails is said; a text box that fails still makes the report.
    private static void BugFixPress()
    {
        string flow = BugSource("BugReport.cs");
        string press = Between(flow, "public static void Press()", "private static IEnumerator WaitForMenus()");
        string ask = Between(flow, "private static void Ask()", "private sealed class Job");
        Check(press.Contains("catch (Exception e)") && Between(press, "catch (Exception e)", "}").Contains("Fail(e);"), "bug fixes: a press that fails tells the player");
        Check(ask.Contains("asked = BugReportGame.AskWords(Make);") && ask.Contains("catch (Exception e)") && ask.Contains("if (!asked)") && ask.Contains("Make(null);"),
            "bug fixes: a text box that fails makes the report without words");
    }

    // 7. The newest five reports kept.
    private static void BugFixKeep()
    {
        var names = new List<string>
        {
            "2026-10-15_14-03-22", "2026-10-15_14-03-22.zip", "2026-10-15_14-03-22_2", "2026-10-15_14-03-22_2.zip", "2026-10-14_09-00-00.zip", "2026-10-13_09-00-00",
            "2026-10-12_09-00-00", "2026-10-12_09-00-00.zip", "2026-10-11_09-00-00", "2026-10-16_08-00-00", "settings.txt", "notes", "2026-10-10.zip"
        };
        List<string> older = BugReportRules.Older(names, BugReportRules.KeepReports);
        Check(older.SequenceEqual(new[] { "2026-10-13_09-00-00", "2026-10-12_09-00-00", "2026-10-11_09-00-00" }),
            "bug fixes: before a new report the oldest go, so that with it there are " + BugReportRules.KeepReports + ", newest by date, time and number (" + string.Join(", ", older) + ")");
        Check(BugReportRules.Older(names.Take(4).ToList(), 5).Count == 0 && BugReportRules.Older(new List<string>(), 5).Count == 0, "bug fixes: with fewer reports, nothing goes");
        Check(BugReportRules.KeepReports == 5, "bug fixes: five reports are kept");
        string clear = Between(BugSource("BugReport.cs"), "private static void Clear(", "// Back on the main thread");
        Check(clear.Contains("BugReportRules.Older(names, BugReportRules.KeepReports)") && Between(BugSource("BugReport.cs"), "private static void Write(Job job)", "Directory.CreateDirectory(job.Folder);").Contains("Clear(reports);")
              && clear.IndexOf("catch (Exception)", StringComparison.Ordinal) >= 0, "bug fixes: the older reports are cleared in a try at the start of the worker's writing");
    }

    // 8. A cut on a line's start keeps that line.
    private static void BugFixLineStart()
    {
        using (var from = new MemoryStream(Encoding.UTF8.GetBytes("aaaa\nbbbb\ncccc\n")))
        using (var to = new StringWriter())
        {
            int lines = BugReportRules.CopyTail(from, to, 10, null);
            string[] got = to.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Check(lines == 2 && got.Length == 3 && got[1] == "bbbb" && got[2] == "cccc", "bug fixes: a cut that falls on a line's start keeps that line (" + to.ToString().Replace("\n", " | ") + ")");
        }
        using (var from = new MemoryStream(Encoding.UTF8.GetBytes("aaaa\nbbbb\ncccc\n")))
        using (var to = new StringWriter())
        {
            int lines = BugReportRules.CopyTail(from, to, 9, null);
            Check(lines == 1 && to.ToString().EndsWith("]\ncccc\n", StringComparison.Ordinal), "bug fixes: a cut inside a line still drops what is left of it");
        }
    }

    // 9. A second report in the same second.
    private static void BugFixUnique()
    {
        var taken = new HashSet<string> { "r", "r_2" };
        Check(BugReportRules.UniqueName("r", taken.Contains) == "r_3" && BugReportRules.UniqueName("s", taken.Contains) == "s" && BugReportRules.UniqueName("r_2", taken.Contains) == "r_2_2",
            "bug fixes: a report's name already there gets _2, _3 and so on");
        Check(BugReportRules.Older(new List<string> { "2026-10-15_14-03-22_3", "2026-10-15_14-03-22_2.zip", "2026-10-15_14-03-22" }, 2).SequenceEqual(new[] { "2026-10-15_14-03-22_2", "2026-10-15_14-03-22" }),
            "bug fixes: reports of the same second are ordered by their number");
        Check(Between(BugSource("BugReport.cs"), "private static void Write(Job job)", "Directory.CreateDirectory(job.Folder);").Contains("BugReportRules.UniqueName("),
            "bug fixes: the worker takes a name no report has");
    }

    // 10. The open phone.
    private static void BugFixPhone()
    {
        BugFacts f = BugFakeFacts();
        f.PhoneOpen = false;
        string closed = BugReportRules.Text(f);
        f.InCity = false;
        string menu = BugReportRules.Text(f);
        Check(BugReportRules.Text(BugFakeFacts()).Contains("Phone: open (the screenshot shows it)") && closed.Contains("Phone: closed") && !menu.Contains("Phone:"),
            "bug fixes: report.txt says whether the phone was open, in a city only");
        string game = BugSource("BugReportGame.cs");
        Check(game.Contains("f.PhoneOpen = f.InCity && PhoneOpen();") && game.Contains("return UI.Smartphone.FullMenu.IsOpen;") && !game.Contains("FullMenu.Close"),
            "bug fixes: the phone is read, never closed");
    }
}
