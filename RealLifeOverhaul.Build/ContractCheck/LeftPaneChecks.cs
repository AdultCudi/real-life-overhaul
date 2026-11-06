using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.GameUi;

// The apps' left pane, the white card beside the tables (Core/GameUi/InfoBox.Frame.cs, InfoBox.Look.cs, InfoBox.Parts.cs,
// LeftPaneRules.cs). It was a fixed card whose vertical layout, given more than it could hold, squeezed every text towards a
// height of nothing, so section heads were drawn over the text above and wrapped lines over the next row. The rules: how a
// fact's label and value share a line in whatever language (the label keeps what it needs, the value takes the rest and wraps
// under itself), when chips go under the title, which rows of buttons stand in the fixed foot, where the scroll stands after a
// page is written again, when two texts' boxes cross, and that the scroll reaches the end. And the sources: the card is a frame
// of head, scrolling body and foot made of Unity's layout parts; no part is placed or sized by the mod in the code that writes a
// page; no caller reaches into a part's transform or writes a label and a value with mark-up; and every caller writes its page
// with the card's own parts. Each source rule is shown to fail on a bad sample, and was made to fail once in the real sources.
internal static partial class Program
{
    // a rect property written where a page is written: the body places and sizes nothing there
    private static readonly Regex PlacedByHand = new Regex(@"\b(?:anchoredPosition|sizeDelta|offsetMin|offsetMax|localPosition|anchoredPosition3D)\b", RegexOptions.Compiled);
    // a margin of the mod's own on a text (a fixed room above or beside it) in the code that makes the card's parts
    private static readonly Regex MarginByHand = new Regex(@"\.margin\s*=\s*new\s+Vector4\(", RegexOptions.Compiled);
    // a label and value put on one line by mark-up (the old line height of nought and right alignment), in any caller or part
    private static readonly Regex MarkupRow = new Regex(@"<line-height|<align=right|<pos=|<indent=|<margin=", RegexOptions.Compiled);
    // a caller reaching into the transform of a part it was given
    private static readonly Regex ReachesIn = new Regex(@"\b(?:box|b|_box)\.\w+\([^;]*\)\s*\.(?:rectTransform|transform)\b", RegexOptions.Compiled);

    private static void LeftPaneChecks(string scripts, string locales)
    {
        Section("the apps' left pane: its rules (Core/GameUi/LeftPaneRules)");
        LeftColumns();
        LeftChips();
        LeftFoot();
        LeftScroll();
        LeftCrossing();
        LeftTiming();
        Section("the apps' left pane: its frame and every caller, in " + scripts);
        LeftSources(scripts, locales);
    }

    private static void LeftColumns()
    {
        float gap = LeftPaneRules.RowGap;
        Check(LeftPaneRules.LabelWidth(120f, 200f, 800f, gap) == 120f && LeftPaneRules.OneLine(120f, 200f, 800f, gap), "a short label and a short value stand on one line, the label as wide as it is");
        Check(LeftPaneRules.LabelWidth(300f, 700f, 800f, gap) == 300f && !LeftPaneRules.OneLine(300f, 700f, 800f, gap),
            "a long value wraps beside a short label: the label keeps its width, the value the rest");
        Check(LeftPaneRules.LabelWidth(600f, 250f, 800f, gap) == 800f - 250f - gap, "a long label wraps beside a short value: the value keeps its width, the label the rest");
        Check(LeftPaneRules.LabelWidth(600f, 600f, 800f, gap) == (800f - gap) / 2f, "a long label and a long value get half the line each");
        Check(LeftPaneRules.LabelWidth(100f, 100f, 0f, gap) == 100f && LeftPaneRules.LabelWidth(-5f, 100f, 800f, gap) == 0f && LeftPaneRules.LabelWidth(float.NaN, 100f, 800f, gap) == 0f,
            "a card of unknown width leaves the label as it is, and no width is negative or NaN");
        Check(!LeftPaneRules.OneLine(100f, 100f, 0f, gap) && !LeftPaneRules.OneLine(100f, 100f, float.NaN, gap), "a fact does not fit a line of no width");
        // the property that makes overlap impossible: whatever the words, the label and the room it leaves the value always fit the line, and
        // the label never asks for more than it measured
        var random = new Random(7);
        bool whole = true;
        for (int i = 0; i < 20000 && whole; i++)
        {
            float label = (float)random.NextDouble() * 1500f, value = (float)random.NextDouble() * 2500f, room = 200f + (float)random.NextDouble() * 900f;
            float w = LeftPaneRules.LabelWidth(label, value, room, gap);
            whole = w >= 0f && w <= label + 0.001f && w + gap <= room + 0.001f;
        }
        Check(whole, "for any label, value and line from 200 to 1100 wide, the label's width is at most its own and leaves the gap and room for the value");
        Check(Math.Abs(LeftPaneRules.Half(843f) - 407.5f) < 0.001f && LeftPaneRules.Half(10f) == 0f, "a pair's half is the line less the gap, shared, never negative");
    }

    private static void LeftChips()
    {
        Check(!LeftPaneRules.ChipsBelow(300f, 800f) && LeftPaneRules.ChipsBelow(400f, 800f) && !LeftPaneRules.ChipsBelow(400f, 0f),
            "chips stay at the title's right while they take under 42% of the line, else go under it; none for a line of no width");
        Check(LeftPaneRules.ChipsBelow(LeftPaneRules.DefaultRoom * LeftPaneRules.ChipsShare + 1f, LeftPaneRules.DefaultRoom), "a chip wider than its share of the card goes under the title");
    }

    private static void LeftFoot()
    {
        var kinds = new[] { PartKind.Light, PartKind.Heavy, PartKind.Heavy, PartKind.Light, PartKind.Buttons, PartKind.Light };
        var go = new bool[kinds.Length];
        Check(LeftPaneRules.FootRows(kinds, kinds.Length, go) == 1 && go[4] && !go[0] && !go[5], "a row of buttons after the page's last heavy part stands in the foot, a note after it stays in the body");
        kinds = new[] { PartKind.Heavy, PartKind.Buttons, PartKind.Heavy, PartKind.Buttons, PartKind.Buttons };
        go = new bool[kinds.Length];
        Check(LeftPaneRules.FootRows(kinds, kinds.Length, go) == 2 && !go[1] && go[3] && go[4], "a row between heavy parts stays where it is, the rows at the end go to the foot");
        kinds = new[] { PartKind.Light, PartKind.Buttons };
        go = new bool[kinds.Length];
        Check(LeftPaneRules.FootRows(kinds, kinds.Length, go) == 1 && go[1], "a page of notes and buttons has its buttons in the foot");
        Check(LeftPaneRules.FootRows(new PartKind[0], 0, new bool[0]) == 0 && LeftPaneRules.FootRows(null, 3, new bool[3]) == 0 && LeftPaneRules.FootRows(new[] { PartKind.Buttons }, 5, new bool[1]) == 1,
            "no parts, no list or a count past the lists are no fault");
        kinds = new[] { PartKind.Heavy, PartKind.Light, PartKind.Light };
        go = new bool[kinds.Length];
        Check(LeftPaneRules.FootRows(kinds, kinds.Length, go) == 0 && !go.Any(g => g), "a page with no buttons sends nothing to the foot");
    }

    private static void LeftScroll()
    {
        Check(LeftPaneRules.KeepOffset(false, 500f, 2000f, 800f) == 0f, "another page (another title) is scrolled to the top");
        Check(LeftPaneRules.KeepOffset(true, 500f, 2000f, 800f) == 500f, "the same page written again keeps its place");
        Check(LeftPaneRules.KeepOffset(true, 1500f, 2000f, 800f) == 1200f && LeftPaneRules.KeepOffset(true, 500f, 600f, 800f) == 0f, "the kept place stays inside the new page's length, none for a page that fits");
        Check(LeftPaneRules.KeepOffset(true, -5f, 2000f, 800f) == 0f && LeftPaneRules.KeepOffset(true, float.NaN, 2000f, 800f) == 0f && LeftPaneRules.KeepOffset(true, 500f, float.MaxValue, 0f) == 500f,
            "a negative or NaN place is the top, and a page of unknown length keeps the place for the scroll to clamp");
        Check(LeftPaneRules.SamePage(null, null) && LeftPaneRules.SamePage("A", "A") && !LeftPaneRules.SamePage("A", "B") && !LeftPaneRules.SamePage(null, "A"), "a page is the same page when its title is, or neither has one");
        Check(LeftPaneRules.ReachesEnd(-500f, -500f) && LeftPaneRules.ReachesEnd(-499f, -500f) && !LeftPaneRules.ReachesEnd(-520f, -500f), "the scroll reaches the end when the last part's bottom is in the view");
    }

    private static void LeftCrossing()
    {
        Check(LeftPaneRules.Crosses(0f, 100f, 50f, 20f, 50f, 150f, 40f, 10f), "two texts whose boxes overlap cross");
        Check(!LeftPaneRules.Crosses(0f, 100f, 50f, 20f, 100f, 200f, 50f, 20f), "a label and its value side by side do not cross");
        Check(!LeftPaneRules.Crosses(0f, 100f, 50f, 20f, 0f, 100f, 20f, -10f), "a text and the one under it, touching, do not cross");
        Check(!LeftPaneRules.Crosses(0f, 100f, 50f, 20f, 0f, 100f, 10f, -20f), "a text and the one under it with a gap do not cross");
        Check(LeftPaneRules.Crosses(0f, 100f, 50f, 20f, 10f, 90f, 40f, 30f), "a text inside another's box crosses it");
        Check(LeftPaneRules.Crosses(0f, 100f, 50f, 20f, 0f, 100f, 25f, -5f), "a text drawn five pixels over the one under it crosses it");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double LeftPass(int pages)
    {
        var kinds = new PartKind[60];
        var go = new bool[60];
        for (int i = 0; i < kinds.Length; i++)
        {
            kinds[i] = i % 7 == 6 ? PartKind.Buttons : i % 3 == 0 ? PartKind.Light : PartKind.Heavy;
        }
        float sum = 0f;
        var clock = Stopwatch.StartNew();
        for (int page = 0; page < pages; page++)
        {
            for (int i = 0; i < 40; i++)
            {
                sum += LeftPaneRules.LabelWidth(80f + i * 7f, 150f + page % 50 * 9f, 800f, LeftPaneRules.RowGap);
            }
            sum += LeftPaneRules.FootRows(kinds, kinds.Length, go);
        }
        clock.Stop();
        return sum < 0f ? -1.0 : clock.Elapsed.TotalMilliseconds / pages;
    }

    // A page of forty facts and sixty parts fitted, two thousand times over: the rules run on a click, never a frame.
    private static void LeftTiming()
    {
        double each = LeftPass(2000);
        Check(each >= 0.0 && each < 0.05, "the rules of a page of forty facts and sixty parts take " + each.ToString("0.0000") + " ms, wanted under 0.05 ms");
        Console.WriteLine("   a page's left pane rules: " + each.ToString("0.0000") + " ms.");
    }

    // ---------------------------------------------------------------- the sources

    private static string Stripped(string path)
    {
        string text = File.ReadAllText(path);
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//[^\n]*", string.Empty);
    }

    // The lines of a source that place or size something by hand, a bar's fill share (the meter) and the foot's own hairline aside.
    private static List<string> Placed(string stripped)
    {
        var hits = new List<string>();
        foreach (string line in stripped.Split('\n'))
        {
            if (PlacedByHand.IsMatch(line) && !line.Contains("fill."))
            {
                hits.Add(line.Trim());
            }
        }
        return hits;
    }

    private static void LeftSources(string scripts, string locales)
    {
        string ui = Path.Combine(scripts, "Core", "GameUi");
        string[] cardFiles = { "InfoBox.cs", "InfoBox.Frame.cs", "InfoBox.Look.cs", "InfoBox.Parts.cs", "InfoBox.Keep.cs", "LeftPaneRules.cs", "LookRules.cs" };
        var texts = new Dictionary<string, string>();
        foreach (string name in cardFiles)
        {
            string path = Path.Combine(ui, name);
            Check(File.Exists(path), "the card's source is there: Core/GameUi/" + name);
            if (File.Exists(path))
            {
                texts[name] = Stripped(path);
            }
        }
        if (texts.Count != cardFiles.Length)
        {
            return;
        }
        string frame = texts["InfoBox.Frame.cs"], look = texts["InfoBox.Look.cs"], parts = texts["InfoBox.Parts.cs"], box = texts["InfoBox.cs"], keep = texts["InfoBox.Keep.cs"];

        // 1. the frame is Unity's layout: a scroll, a fitted content, vertical layouts, a mask, the game's bar
        Check(frame.Contains("ScrollRect") && frame.Contains("ContentSizeFitter") && frame.Contains("VerticalLayoutGroup") && frame.Contains("RectMask2D") && frame.Contains("verticalScrollbar"),
            "the card's frame is a ScrollRect over a ContentSizeFitter and vertical layouts, masked, with the game's scroll bar (InfoBox.Frame.cs)");
        Check(frame.Contains("flexibleHeight = 1f") && frame.Contains("preferredHeight = 0f") && frame.Contains("verticalFit = ContentSizeFitter.FitMode.PreferredSize"),
            "the body takes the height the head and foot leave and the content is as tall as its parts ask");
        Check(frame.Contains("style.scrollSensitivity") && frame.Contains("style.movementType") && frame.Contains("Instantiate(source.gameObject"),
            "the scroll has the game's own table's step and movement, and its bar is a copy of the table's");
        Check(frame.Contains("LeftPaneRules.KeepOffset") && frame.Contains("_pageKey") && box.Contains("Settle()"), "the scroll goes to the top for another page and is kept for the same page written again");
        Check(frame.Contains("public int Audit(") && frame.Contains("LeftPaneRules.Crosses") && frame.Contains("LeftPaneRules.ReachesEnd"), "the card can measure itself for the in-game test (Audit)");

        // 2. nothing in the code that writes a page places or sizes anything by hand
        List<string> placed = Placed(parts).Concat(Placed(box)).Concat(Placed(keep)).ToList();
        Check(placed.Count == 0, "the code that writes a page (InfoBox.Parts.cs, InfoBox.cs, InfoBox.Keep.cs) sets no position, size or offset of its own: " + string.Join(" | ", placed.Take(4)));
        Check(!MarginByHand.IsMatch(look) && !MarginByHand.IsMatch(parts) && !MarginByHand.IsMatch(box), "no part is given a margin of the mod's own, only the layout's padding and spacing");
        Check(!MarkupRow.IsMatch(look) && !MarkupRow.IsMatch(parts) && !MarkupRow.IsMatch(box), "no label and value are put on one line by mark-up (a line height of nought, right alignment)");
        Check(parts.Contains("LeftPaneRules.LabelWidth") && parts.Contains(".preferredWidth"), "a fact's label is given the width its words measure (LeftPaneRules.LabelWidth), not one guessed from letters");
        Check(look.Contains("HorizontalLayoutGroup") && look.Contains("_rowT") && look.Contains("_entryT") && look.Contains("_groupT"), "a fact, a list entry and a section are layouts of the card's own parts (InfoBox.Look.cs)");

        // 3. the sources' rules fail on what they are for
        Check(PlacedByHand.IsMatch("rect.anchoredPosition = new Vector2(0f, -120f);") && !PlacedByHand.IsMatch("text.text = words;"), "the reading finds a part placed by hand, and not a text written");
        Check(MarginByHand.IsMatch("text.margin = new Vector4(28f, 0f, 0f, 0f);") && !MarginByHand.IsMatch("text.margin = Vector4.zero;"), "the reading finds a margin of the mod's own, and not none");
        Check(MarkupRow.IsMatch("\"<line-height=0>\\n</line-height><align=right>\"") && !MarkupRow.IsMatch("\"<b>value</b>\""), "the reading finds a row put on one line by mark-up");
        Check(ReachesIn.IsMatch("box.Section(\"k\").transform.localPosition = p;") && !ReachesIn.IsMatch("box.Section(\"k\");"), "the reading finds a caller reaching into a part's transform");

        // 4. every caller writes its page with the card's own parts
        var reach = new List<string>();
        var markup = new List<string>();
        var callers = new List<string>();
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (name.StartsWith("Core/GameUi/", StringComparison.Ordinal) || name.StartsWith("Meta/LeftPaneTest", StringComparison.Ordinal))
            {
                continue;
            }
            string text = Stripped(file);
            if (!Regex.IsMatch(text, @"\bInfoBox\b"))
            {
                continue;
            }
            callers.Add(name);
            if (ReachesIn.IsMatch(text))
            {
                reach.Add(name);
            }
            foreach (Match m in Regex.Matches(text, @"\b(?:box|b|_box)\.(?:Line|Note|Row|RowWords|Subtitle|Title|Section|SectionWords|Check|Entry)\([^;]*;"))
            {
                if (MarkupRow.IsMatch(m.Value))
                {
                    markup.Add(name);
                    break;
                }
            }
        }
        Check(callers.Count >= 100, "fewer files write a page with the card than the mod has (" + callers.Count + "): the reading is broken");
        Check(reach.Count == 0, "a caller reaches into the transform of a part it was given, to place or size it: " + string.Join(", ", reach.Take(8)));
        Check(markup.Count == 0, "a caller puts a label and a value on one line by mark-up, where a Row is meant: " + string.Join(", ", markup.Take(8)));
        Console.WriteLine("   " + callers.Count + " source files write pages with the card; none reaches into a part, none puts a row on one line by mark-up.");

        // 5. the career ladder is a list of entries
        string start = Path.Combine(scripts, "Careers", "CareerApp.Start.cs");
        string startText = File.Exists(start) ? Stripped(start) : string.Empty;
        Check(startText.Contains("box.Entry(") && !Regex.IsMatch(startText, @"box\.RowWords\(CareersText\.Title\(p\.Id, r\)"), "a profession's ladder is a list of entries, a rung's name and pay on one line and what it asks under them");

        // 6. the test's words, in every language that ships
        foreach (string lang in ModLanguages.All.Take(ModLanguages.Required))
        {
            string path = Path.Combine(locales, lang + ".json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            string[] wanted = { "reallife_leftpane_test", "reallife_leftpane_test_pass", "reallife_leftpane_test_fail", "reallife_leftpane_test_none" };
            var missing = wanted.Where(k => !doc.RootElement.TryGetProperty(k, out _)).ToList();
            Check(missing.Count == 0, "the left pane test's words are missing in " + lang + ".json: " + string.Join(", ", missing));
        }
        string mod = Path.Combine(scripts, "Core", "RealLifeOverhaulMod.cs");
        string list = Path.Combine(scripts, "Meta", "FeatureList.cs");
        Check(File.Exists(mod) && File.ReadAllText(mod).Contains("\"reallife_leftpane_test\", Meta.LeftPaneTest.Run") && File.Exists(list) && File.ReadAllText(list).Contains("\"reallife_leftpane_test\""),
            "the left pane test is a button of Options > Tests and one of the phone feature's tests");
    }
}
