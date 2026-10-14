using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// The look of the apps' white box (Core/GameUi/InfoBox.Look.cs, InfoBox.Parts.cs, LookRules.cs). Its rules are checked as
// the box uses them: how wide a text is (a Chinese character two letters, mark-up nothing), when a value goes under its
// label, how a label loses its colon, how an old "Label <b>value</b>" line is read as a row and a sentence is not, how
// full a meter is, which days of a week run on, and each app's accent. And the sources: no app writes a label and a
// value as one bare line any more (a Pair inside a Line, or ": <b>" put together by hand), every app's page that has a
// title uses the box's own parts, every one of the box's own words is in all three languages, and the rules of a
// whole page cost next to nothing.
internal static partial class Program
{
    private static readonly Regex LineOfPair = new Regex(@"\.Line\((?:[^;]*?)\bPair\(", RegexOptions.Compiled);
    private static readonly Regex LineByHand = new Regex(@"\.Line\([^;]*\+\s*"":\s*<b>""", RegexOptions.Compiled);
    private static readonly Regex NewParts = new Regex(@"\b(?:box|b)\.(?:Section|SectionWords|Row|RowWords|RowPair|Meter|MeterWords|Check|Note|Chip|Subtitle|GuideLink)\(", RegexOptions.Compiled);
    private static readonly Regex BoxKey = new Regex(@"""(reallife_ibox_[a-z0-9_]+)""", RegexOptions.Compiled);

    private static void LookChecks(string scripts, string locales)
    {
        Section("the apps' white box: its look's rules (Core/GameUi/LookRules)");
        LookWidths();
        LookPairs();
        LookMeters();
        LookAccents();
        LookTiming();
        Section("the apps' white box: every app written with its parts, in " + scripts);
        LookSources(scripts, locales);
    }

    private static void LookWidths()
    {
        Check(LookRules.Units("Pay") == 3 && LookRules.Units(string.Empty) == 0 && LookRules.Units(null) == 0, "a word of Latin letters is as wide as its letters, nothing nothing");
        Check(LookRules.Units("薪酬") == 4 && LookRules.Units("第 11 天") == 8, "a Chinese character counts two letters, a space and a figure one");
        Check(LookRules.Units("<b>$20.00</b>") == 6 && LookRules.Units("<color=#596975>Pay</color>") == 3, "mark-up counts nothing");
        Check(LookRules.Capacity(761f, LookRules.BodySize) == 48, "a row of the phone's box holds 48 letters of the body's size (" + LookRules.Capacity(761f, LookRules.BodySize) + ")");
        Check(LookRules.Capacity(0f, 28f) == 0 && LookRules.Capacity(float.NaN, 28f) == 0 && LookRules.Capacity(500f, 0f) == 0, "a box of no width holds nothing, and no NaN is read as a width");
        int cap = LookRules.Capacity(761f, LookRules.BodySize);
        Check(!LookRules.Stack("Pay", "$20.00 an hour", cap), "a short label and value stand on one line");
        Check(LookRules.Stack("Satisfaction (the game's own figure, not changed here)", "64 of 100", cap), "a long label puts its value under it");
        Check(LookRules.Stack("Hours", "Mon, Tue, Wed, Thu, Fri, Sat, Sun, 06:00 to 14:00, and on the evening shift", cap), "a long value goes under its label");
        Check(LookRules.Stack("Needs", "one\ntwo", cap), "a value of two lines goes under its label");
        Check(!LookRules.Stack("a very long label that would never fit beside anything at all, not in any box", "x", 0), "a box of no known width keeps label and value side by side");
        Check(LookRules.Stack("获得录用的几率最高的职位要求和条件说明", "约 45% 的录用机会", cap) && !LookRules.Stack("薪酬", "每小时$20.00", cap), "Chinese labels and values are measured as their square characters are set");
        float chip = LookRules.ChipWidth("Applied"), longer = LookRules.ChipWidth("Not qualified");
        Check(chip > 36f && longer > chip && LookRules.ChipWidth(null) == 36f, "a chip is as wide as its padding and its word, a longer word wider");
    }

    private static void LookPairs()
    {
        Check(LookRules.Label("Balance:") == "Balance" && LookRules.Label("余额：") == "余额" && LookRules.Label("Owed here: ") == "Owed here" && LookRules.Label("Pay") == "Pay", "a label loses the colon it ends in, Chinese or not");
        Check(LookRules.Label(string.Empty) == string.Empty && LookRules.Label(null) == string.Empty && LookRules.Label(":") == string.Empty, "an empty label stays empty");
        Check(LookRules.Colon("Guide") == ": " && LookRules.Colon("指南") == "：" && LookRules.Colon(null) == ": ", "the colon after a label in running text is the full-width one after Chinese");
        string label, value;
        Check(LookRules.SplitPair("Vacancies open to you <b>3</b>", out label, out value) && label == "Vacancies open to you" && value == "3", "FinanceText.Pair's line is read as a row");
        Check(LookRules.SplitPair("Age: <b>31</b>", out label, out value) && label == "Age" && value == "31", "the game's own \"Age: <b>31</b>\" is read as a row without its colon");
        Check(LookRules.SplitPair("总体：<b>68</b>/100", out label, out value) && label == "总体" && value == "68/100", "a Chinese label with its full-width colon and a few words after the figure is a row");
        Check(LookRules.SplitPair("Overall: <b>68</b> of 100", out label, out value) && value == "68 of 100", "a few words after the figure are part of the value");
        Check(!LookRules.SplitPair("Recognition: <b>12</b> city-wide, <b>30</b> in its best district", out label, out value), "two bold parts are a sentence");
        Check(!LookRules.SplitPair("Standards are <b>64</b> out of 100", out label, out value), "words after a figure with no colon before it are a sentence");
        Check(!LookRules.SplitPair("Selling nothing: <b>closed</b>. The interior brings nobody extra until something sells.", out label, out value), "another sentence after the figure is a sentence");
        Check(!LookRules.SplitPair("<color=#fff>Pay</color> <b>3</b>", out label, out value) && !LookRules.SplitPair("You have applied for this post.", out label, out value)
              && !LookRules.SplitPair("<b>Only a figure</b>", out label, out value) && !LookRules.SplitPair("Empty <b></b>", out label, out value) && !LookRules.SplitPair(null, out label, out value),
            "mark-up in a label, a sentence, a figure with no label and an empty figure are not rows");
    }

    private static void LookMeters()
    {
        Check(LookRules.Fill(11, 10) == 1f && LookRules.Fill(5, 10) == 0.5f && LookRules.Fill(0, 10) == 0f && LookRules.Fill(-3, 10) == 0f, "a meter is what is had over what is wanted, full at or over it, empty for nothing");
        Check(LookRules.Fill(0, 0) == 1f && LookRules.Fill(3, 0) == 1f && LookRules.Fill(0, -1) == 0f, "nothing wanted is a full meter");
        Check(LookRules.Clamp(0.45f) == 0.45f && LookRules.Clamp(2f) == 1f && LookRules.Clamp(-1f) == 0f && LookRules.Clamp(float.NaN) == 0f, "a share is brought into the meter, NaN to empty");
        int[] from = new int[7], to = new int[7];
        Check(LookRules.DayRuns(0x1F, from, to) == 1 && from[0] == 0 && to[0] == 4, "Monday to Friday is one run");
        Check(LookRules.DayRuns(0x15, from, to) == 3 && from[0] == 0 && from[1] == 2 && from[2] == 4 && to[2] == 4, "Monday, Wednesday and Friday are three days");
        Check(LookRules.DayRuns(0x2F, from, to) == 2 && to[0] == 3 && from[1] == 5 && to[1] == 5, "Monday to Thursday and Saturday are a run and a day");
        Check(LookRules.DayRuns(0x60, from, to) == 2 && from[0] == 5 && from[1] == 6, "a weekend of two days is two days, not a run");
        Check(LookRules.DayRuns(0x7F, from, to) == 1 && to[0] == 6 && LookRules.DayRuns(0, from, to) == 0, "every day is one run, no day none");
    }

    private static void LookAccents()
    {
        Check(LookRules.AccentIds.Length == LookRules.AccentColours.Length, "every app's id has its colour");
        Check(LookRules.AccentIds.Distinct().Count() == LookRules.AccentIds.Length, "no app's id is listed twice");
        Check(LookRules.Accent("RealLifeBank3") == LookRules.Accent("RealLifeBank1") && LookRules.Accent("RealLifeOwnBank") != LookRules.Accent("RealLifeBank1"),
            "every bank's app has the banks' colour, the player's own bank its own");
        Check(LookRules.Accent("SomethingElse") == LookRules.Slate && LookRules.Accent(null) == LookRules.Slate && LookRules.Accent("RealLifeCareer") == 0x284080, "an app not listed has the game's slate; Career its icon's blue");
        Check(LookRules.AccentColours.All(c => c >= 0 && c <= 0xFFFFFF && Luma(c) < 0.62), "every accent is a colour dark enough to show on the white box");
    }

    private static double Luma(int rgb)
    {
        return (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;
    }

    // A big page's rules: forty rows read, measured and stacked, two thousand times over (a page is written on a click).
    private static void LookTiming()
    {
        string[] lines =
        {
            "Balance: <b>$12,480</b>", "Daily payment: <b>$142</b>", "Overall: <b>68</b> of 100", "Pays $20.00 an hour, Mon to Fri, 06:00 to 14:00.",
            "薪酬：<b>每小时$20.00</b>", "Satisfaction (the game's own figure, not changed here): <b>64</b>", "Credit grade <b>Good</b>", "Vacancies open to you <b>3</b>"
        };
        int cap = LookRules.Capacity(761f, LookRules.BodySize), rows = 0;
        var clock = Stopwatch.StartNew();
        for (int page = 0; page < 2000; page++)
        {
            for (int i = 0; i < 40; i++)
            {
                string label, value;
                if (LookRules.SplitPair(lines[i % lines.Length], out label, out value))
                {
                    rows += LookRules.Stack(label, value, cap) ? 2 : 1;
                }
            }
        }
        clock.Stop();
        double each = clock.Elapsed.TotalMilliseconds / 2000.0;
        Check(rows > 0 && each < 0.05, "the rules of a page of forty rows take " + each.ToString("0.0000") + " ms, wanted under 0.05 ms");
        Console.WriteLine("   a page of forty rows' rules: " + each.ToString("0.0000") + " ms.");
    }

    private static void LookSources(string scripts, string locales)
    {
        if (!Directory.Exists(scripts))
        {
            Check(false, "the mod's scripts are not at " + scripts);
            return;
        }
        var bare = new List<string>();
        var plain = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int pages = 0, files = 0;
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (name.StartsWith("Core/GameUi/InfoBox", StringComparison.Ordinal))
            {
                continue;
            }
            string text = File.ReadAllText(file);
            string[] lines = File.ReadAllLines(file);
            // whole statements: a Line( call and everything up to its semicolon
            string flat = Regex.Replace(text, @"//[^\n]*", string.Empty);
            foreach (Match m in Regex.Matches(flat, @"\b(?:box|b|_box)\.Line\([^;]*;"))
            {
                if (LineOfPair.IsMatch(m.Value) || LineByHand.IsMatch(m.Value))
                {
                    int at = flat.Substring(0, m.Index).Count(c => c == '\n') + 1;
                    bare.Add(name + ":" + at);
                }
            }
            bool titled = Regex.IsMatch(flat, @"\b(?:box|b)\.Title\(");
            if (titled && text.Contains("InfoBox"))
            {
                files++;
                pages += Regex.Matches(flat, @"\b(?:box|b)\.Title\(").Count;
                if (!NewParts.IsMatch(flat))
                {
                    plain.Add(name);
                }
            }
            foreach (Match k in BoxKey.Matches(text))
            {
                keys.Add(k.Groups[1].Value);
            }
        }
        Check(files >= 50 && pages >= 150, "fewer apps' pages with a title read than the mod has (" + files + " files, " + pages + " pages): the reading is broken");
        Check(bare.Count == 0, "a label and its value written as one bare line (a Pair inside Line, or \": <b>\" by hand), where a Row is meant: " + string.Join(", ", bare.Take(12)));
        Check(plain.Count == 0, "an app's box written without any of the box's own parts (Section, Row, Meter, Check, Note, Chip, Subtitle): " + string.Join(", ", plain));
        Check(LineOfPair.IsMatch("box.Line(FinanceText.Pair(\"k\", v));") && LineByHand.IsMatch("box.Line(T(k) + \": <b>\" + v + \"</b>\");") && !LineOfPair.IsMatch("box.Row(\"k\", v);"),
            "the reading finds a pair written as a line, and not a row");
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            string path = Path.Combine(locales, lang + ".json");
            var have = new HashSet<string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    have.Add(p.Name);
                }
            }
            var missing = keys.Where(k => !have.Contains(k)).ToList();
            Check(missing.Count == 0, "the box's words missing in " + lang + ".json: " + string.Join(", ", missing.Take(12)));
        }
        Check(keys.Count >= 150, "fewer of the box's own words named in the sources than it has (" + keys.Count + ")");
        Console.WriteLine("   " + files + " sources write " + pages + " pages with a title; " + bare.Count + " bare label and value lines; " + keys.Count + " of the box's own words, in all three languages.");
    }
}
