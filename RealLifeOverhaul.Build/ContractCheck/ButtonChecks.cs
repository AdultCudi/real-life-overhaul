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

// The rows of buttons of the apps' white box (Core/GameUi/ButtonRules.cs, ButtonFlow.cs, InfoBox.Button). The game's row
// has one button of each kind and a page asked for a kind by its place, so a second button of a kind took the first
// one's place: the Campus app's "Enrol on a student loan" was written over by "Close". The rules: every button a row is
// given shows, in order, on lines from the box's left edge to its right with the game's gap, on more lines when they do
// not fit; the row asks for the height its lines take. And the sources: InfoBox.Button takes a new button for each one
// written, the row is laid out by ButtonFlow, and the Campus offers the student loan beside "Enrol and pay" for every
// course a loan can pay for (vocational courses and the university's) while the university is open.
internal static partial class Program
{
    private const float PhoneBox = 789f;   // the row's width in the apps' box: MyEmployees' white box of 949 less its padding of 80 a side
    private const float Near = 0.01f;      // canvas pixels: equal for placed edges

    private static void ButtonChecks(string scripts, string locales)
    {
        Section("the apps' white box: rows of buttons (Core/GameUi/ButtonRules)");
        ButtonCases();
        ButtonEdges();
        ButtonSweep();
        ButtonTiming();
        Section("the apps' white box: every button written shows, and the Campus offers the student loan, in " + scripts);
        ButtonSources(scripts, locales);
    }

    // A row of these needs in a box of this width: where each button stands.
    private static (int Lines, int[] Line, float[] X, float[] W) Lay(float width, params float[] needs)
    {
        var line = new int[needs.Length];
        var x = new float[needs.Length];
        var w = new float[needs.Length];
        int lines = ButtonRules.Place(width, needs, needs.Length, line, x, w);
        return (lines, line, x, w);
    }

    private static bool Same(float a, float b)
    {
        return Math.Abs(a - b) <= Near;
    }

    private static void ButtonCases()
    {
        float g = ButtonRules.Gap;
        var one = Lay(PhoneBox, 300f);
        Check(one.Lines == 1 && one.Line[0] == 0 && Same(one.X[0], 0f) && Same(one.W[0], PhoneBox), "one button runs from the box's left edge to its right, as the game's row (" + one.X[0] + ", " + one.W[0] + ")");

        var two = Lay(PhoneBox, 363f, 189f);
        float half = (PhoneBox - g) / 2f;
        Check(two.Lines == 1 && Same(two.X[0], 0f) && Same(two.W[0], half) && Same(two.X[1], half + g) && Same(two.W[1], half),
            "two buttons that fit share the line equally with the game's gap of 20 between them (" + two.W[0] + " and " + two.W[1] + " at " + two.X[1] + ")");

        var three = Lay(PhoneBox, 200f, 160f, 240f);
        float third = (PhoneBox - 2f * g) / 3f;
        Check(three.Lines == 1 && Same(three.W[0], third) && Same(three.W[2], third) && Same(three.X[1], third + g) && Same(three.X[2] + three.W[2], PhoneBox),
            "three buttons that fit a third each share the line in thirds, the last ending at the right edge (" + third + ")");

        // The Campus's course: "Enrol and pay", "Enrol on a student loan", "Close" (their words about 283, 500 and 109 wide).
        var campus = Lay(PhoneBox, ButtonRules.Need(283f), ButtonRules.Need(500f), ButtonRules.Need(109f));
        Check(campus.Lines == 2 && campus.Line[0] == 0 && campus.Line[1] == 1 && campus.Line[2] == 1, "the Campus's three buttons go on two lines: Enrol and pay, then the loan and Close (" + campus.Lines + " lines)");
        Check(Same(campus.W[0], PhoneBox) && campus.W[1] >= ButtonRules.Need(500f) - Near && campus.W[2] >= ButtonRules.Need(109f) - Near && Same(campus.X[2] + campus.W[2], PhoneBox),
            "on the Campus's second line the loan has the room its words need and Close the rest, to the right edge (" + campus.W[1] + ", " + campus.W[2] + ")");

        var four = Lay(PhoneBox, 200f, 210f, 220f, 230f);
        Check(four.Lines == 2 && four.Line[2] == 0 && four.Line[3] == 1 && Same(four.X[3], 0f) && Same(four.W[3], PhoneBox),
            "four buttons of which three fit: three on the first line, the fourth on its own line from edge to edge");

        var wide = Lay(PhoneBox, 160f, 300f, 170f, 520f);
        Check(wide.Lines == 2 && wide.Line[1] == 0 && wide.Line[2] == 0 && wide.Line[3] == 1 && wide.W[1] >= 300f - Near && wide.W[0] >= 160f - Near,
            "four buttons of different widths: those that fit stay on the first line, each with at least its own need, the widest wraps");

        Check(ButtonRules.RowHeight(1) == 120f && ButtonRules.RowHeight(2) == 220f && ButtonRules.RowHeight(0) == 0f && ButtonRules.Block(3) == 280f,
            "a row is the game's 120 high for one line, 100 more for each further line, nothing when it shows no button");
        Check(ButtonRules.Top(120f, 1, 0) == 20f && ButtonRules.Top(220f, 2, 1) == 120f && ButtonRules.Top(80f, 1, 0) == 0f && ButtonRules.Top(40f, 1, 0) == 0f,
            "a line stands 20 under the row's top as in the game's row, the next one 100 lower; a row of the buttons' own height (the top bar's) holds them at its top");
        Check(ButtonRules.Need(283f) == 363f && ButtonRules.Need(10f) == ButtonRules.Least && ButtonRules.Need(float.NaN) == ButtonRules.Least && ButtonRules.Need(-5f) == ButtonRules.Least,
            "a button needs its words and 80 beside them, never under 160, and no words when they cannot be measured");
    }

    private static void ButtonEdges()
    {
        var none = Lay(PhoneBox);
        Check(none.Lines == 0 && ButtonRules.Place(PhoneBox, null, 3, new int[3], new float[3], new float[3]) == 0, "no buttons, or no widths given, are no lines");
        var narrow = Lay(0f, 200f, 300f);
        Check(narrow.Lines == 2 && narrow.W[0] == 0f && narrow.W[1] == 0f && narrow.X[1] == 0f, "a row of no width puts each button on a line of its own, of no width");
        var nan = Lay(float.NaN, 200f);
        Check(nan.Lines == 1 && nan.W[0] == 0f, "a width that is not a number is no width");
        var huge = Lay(PhoneBox, 2000f);
        Check(huge.Lines == 1 && Same(huge.W[0], PhoneBox) && Same(huge.X[0], 0f), "a button wider than the line has the whole line (its words end in the game's dots)");
        var between = Lay(PhoneBox, 160f, 2000f, 160f);
        Check(between.Lines == 3 && between.Line[1] == 1 && Same(between.W[1], PhoneBox) && Same(between.W[2], PhoneBox), "a button wider than the line between two others has a line of its own");
        var exact = Lay(PhoneBox, 384.5f, 384.5f);
        Check(exact.Lines == 1, "two buttons that fill the line exactly stay on it");
        var over = Lay(PhoneBox, 384.6f, 384.5f);
        Check(over.Lines == 2, "a tenth of a pixel more and the second goes on the next line");
        var shortArrays = ButtonRules.Place(PhoneBox, new[] { 200f, 200f, 200f }, 5, new int[2], new float[2], new float[2]);
        Check(shortArrays == 1, "a count longer than the arrays given places what the arrays hold (" + shortArrays + ")");
        var a = Lay(PhoneBox, 210f, 380f, 170f, 290f, 160f);
        var b = Lay(PhoneBox, 210f, 380f, 170f, 290f, 160f);
        bool same = a.Lines == b.Lines;
        for (int i = 0; i < 5; i++)
        {
            same &= a.Line[i] == b.Line[i] && a.X[i] == b.X[i] && a.W[i] == b.W[i];
        }
        Check(same, "the same buttons in the same box stand in the same places every time");
    }

    // 1 to 8 buttons of widths from the game's least to wider than the box, in boxes from the top bar's 760 to the
    // widest app's: every button shows, inside the box, as wide as it needs where the line allows, lines edge to edge,
    // the game's gap between buttons, in the order given, and a line breaks only where the next button does not fit.
    private static void ButtonSweep()
    {
        var random = new Random(20261015);
        int cases = 0, faults = 0;
        string first = null;
        foreach (float width in new[] { 760f, PhoneBox, 1000f, 1400f })
        {
            for (int n = 1; n <= 8; n++)
            {
                for (int round = 0; round < 200; round++)
                {
                    var needs = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        needs[i] = ButtonRules.Need((float)(random.NextDouble() * (round % 10 == 0 ? 1000.0 : 450.0)));
                    }
                    var lay = Lay(width, needs);
                    cases++;
                    string fault = SweepFault(width, needs, lay.Lines, lay.Line, lay.X, lay.W);
                    if (fault != null)
                    {
                        faults++;
                        first = first ?? fault;
                    }
                }
            }
        }
        Check(faults == 0, "rows of 1 to 8 buttons in boxes of 760 to 1400: " + faults + " of " + cases + " placed wrong" + (first != null ? " (first: " + first + ")" : string.Empty));
        Console.WriteLine("   " + cases + " rows of 1 to 8 buttons placed and checked.");
    }

    private static string SweepFault(float width, float[] needs, int lines, int[] line, float[] x, float[] w)
    {
        int n = needs.Length;
        if (lines < 1 || line[0] != 0 || line[n - 1] != lines - 1)
        {
            return "lines " + lines;
        }
        for (int i = 0; i < n; i++)
        {
            if (x[i] < -Near || x[i] + w[i] > width + Near || w[i] < Math.Min(needs[i], width) - Near)
            {
                return "button " + i + " of " + n + " at " + x[i] + ", " + w[i] + " wide in " + width + " (needs " + needs[i] + ")";
            }
            bool starts = i == 0 || line[i] != line[i - 1];
            bool ends = i == n - 1 || line[i + 1] != line[i];
            if (starts && !Same(x[i], 0f))
            {
                return "line " + line[i] + " starts at " + x[i];
            }
            if (ends && !Same(x[i] + w[i], width))
            {
                return "line " + line[i] + " ends at " + (x[i] + w[i]) + " in " + width;
            }
            if (!starts && !Same(x[i], x[i - 1] + w[i - 1] + ButtonRules.Gap))
            {
                return "button " + i + " is not the gap after the one before";
            }
            if (i > 0 && line[i] != line[i - 1] && line[i] != line[i - 1] + 1)
            {
                return "line " + line[i] + " follows line " + line[i - 1];
            }
            if (ends && i < n - 1)
            {
                float used = 0f;
                for (int j = i; j >= 0 && line[j] == line[i]; j--)
                {
                    used += needs[j] + ButtonRules.Gap;
                }
                if (used + needs[i + 1] <= width - Near)
                {
                    return "line " + line[i] + " breaks before button " + (i + 1) + ", which fits";
                }
            }
        }
        return null;
    }

    private static void ButtonTiming()
    {
        float[] needs = { 363f, 580f, 189f, 240f, 160f, 300f };
        var line = new int[6];
        var x = new float[6];
        var w = new float[6];
        Rows(needs, line, x, w, 1000);   // warmed
        var clock = new Stopwatch();   // (made before the count: the clock itself is an allocation)
        long before = GC.GetAllocatedBytesForCurrentThread();
        clock.Start();
        int lines = Rows(needs, line, x, w, 20000);
        clock.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = clock.Elapsed.TotalMilliseconds / 20000.0;
        Check(lines > 0 && each < 0.01, "a row of six buttons is placed in " + each.ToString("0.00000") + " ms, wanted under 0.01 ms");
        Check(bytes == 0, "placing a row allocates nothing (" + bytes + " bytes over 20,000 rows)");
        Console.WriteLine("   a row of six buttons placed in " + each.ToString("0.00000") + " ms, " + bytes + " bytes.");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int Rows(float[] needs, int[] line, float[] x, float[] w, int times)
    {
        int lines = 0;
        for (int i = 0; i < times; i++)
        {
            lines += ButtonRules.Place(PhoneBox, needs, needs.Length, line, x, w);
        }
        return lines;
    }

    private static void ButtonSources(string scripts, string locales)
    {
        string box = Read(scripts, "Core/GameUi/InfoBox.cs");
        string flow = Read(scripts, "Core/GameUi/ButtonFlow.cs");
        string campus = Read(scripts, "Careers/CampusApp.cs");
        string catalogue = Read(scripts, "Careers/Study/StudyCatalogue.cs");
        string study = Read(scripts, "Careers/CareersDirector.Study.cs");
        string degrees = Read(scripts, "Careers/CareersDirector.Degrees.cs");
        if (box == null || flow == null || campus == null || catalogue == null || study == null || degrees == null)
        {
            Check(false, "the box's, the row's or the Campus's sources are not where they were");
            return;
        }

        // InfoBox: the template's row is given the flow, a row written begins it, and each button is taken from it.
        string button = Body(box, "public static Button Button(Transform row, int kind, string key, Action click)");
        Check(button != null && button.Contains("flow.Take(kind, key)") && !Regex.IsMatch(button, @"GetChild\(Mathf\.Clamp\(kind[^;]*;\s*child\.gameObject\.SetActive"),
            "InfoBox.Button takes each button from the row's flow, a new one for a further button of a kind, and never shows a kind's one button again in place of the last");
        string row = Body(box, "public Transform ButtonRow(Transform parent = null)");
        Check(row != null && row.Contains("flow.Begin()"), "InfoBox.ButtonRow begins the row's flow: the last page's buttons hidden and their copies gone");
        Check(Regex.IsMatch(box, @"PrepareButtons\(box\._buttons\)") && box.Contains("AddComponent<ButtonFlow>()") && box.Contains("DestroyImmediate(line)"),
            "the box's row of buttons is laid out by ButtonFlow in place of the game's one-line layout");
        Check(flow.Contains("ButtonRules.Place(") && flow.Contains("ButtonRules.RowHeight(") && flow.Contains("ButtonRules.Top(") && flow.Contains(": LayoutGroup")
              && Regex.Matches(flow, @"catch \(System\.Exception e\)\s*\{\s*Guard\.Fail\(FrameGuard, e\);").Count == 4,
            "ButtonFlow places the buttons by ButtonRules, asks for the lines' height, and each of the engine's four calls is behind the guard");
        Check(!Regex.IsMatch(flow, @"\bvoid (Update|LateUpdate|FixedUpdate)\("), "ButtonFlow has nothing that runs every frame");

        // The Campus's course: the loan beside "Enrol and pay" on the same row, for every course a loan pays for, with the university open.
        string pick = Between(campus, "\"reallife_campus_button_enrol\"", "\"common_close\"");
        Check(pick != null && Regex.IsMatch(pick, @"if \(c\.Loanable && CareersDirector\.UniversityOpen\)\s*\{\s*InfoBox\.Button\(buttons, InfoBox\.\w+, ""reallife_campus_button_loan""")
              && !pick.Contains("ButtonRow("),
            "the Campus offers \"Enrol on a student loan\" on the same row as \"Enrol and pay\" for a course a loan pays for while the university is open");
        Check(pick != null && Regex.IsMatch(pick, @"_director\.Enrol\(id, mode, true\)"), "the Campus's loan button enrols on the loan");
        Check(Regex.IsMatch(catalogue, @"public bool Loanable => Kind == CourseKind\.Vocational \|\| Higher;")
              && Regex.IsMatch(catalogue, @"public bool Higher => Kind == CourseKind\.Degree \|\| Kind == CourseKind\.Postgrad;"),
            "a student loan pays for the vocational courses and the university's (degrees and postgraduate courses)");
        Check(Regex.IsMatch(study, @"private static bool LoanOn => UniversityOpen;") && Regex.IsMatch(study, @"OnLoan = loan && c\.Loanable && LoanOn")
              && Regex.IsMatch(degrees, @"public static bool UniversityOpen => Settings\.CareersStudy && Settings\.CareersDegrees;"),
            "the loan the Campus offers is the loan the enrolment gives: with the university open (studying and degrees on)");
        foreach (string lang in ModLanguages.All.Take(ModLanguages.Required))
        {
            string path = Path.Combine(locales, lang + ".json");
            bool has = false;
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                has = doc.RootElement.TryGetProperty("reallife_campus_button_loan", out JsonElement words) && words.GetString().Length > 0
                      && doc.RootElement.TryGetProperty("reallife_campus_button_enrol", out JsonElement enrol) && enrol.GetString().Length > 0;
            }
            Check(has, "the Campus's enrol and loan buttons have their words in " + lang + ".json");
        }
        // What's new says so, in a row of its own language: any row (the release moves the next row to its version's).
        foreach ((string lang, string fixedWord) in new[] { ("en", "Fixed: "), ("pt", "Corrigido: "), ("zh-cn", "已修复：") })
        {
            bool told = false;
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, lang + ".json"))))
            {
                string loan = doc.RootElement.TryGetProperty("reallife_campus_button_loan", out JsonElement words) ? words.GetString() : null;
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Name.StartsWith("reallife_changes_", StringComparison.Ordinal) && !p.Name.EndsWith("_title", StringComparison.Ordinal) && p.Value.ValueKind == JsonValueKind.String)
                    {
                        string said = p.Value.GetString();
                        int at = loan != null ? said.IndexOf(loan, StringComparison.Ordinal) : -1;
                        told |= at > 0 && said.LastIndexOf(fixedWord, at, StringComparison.Ordinal) >= 0;
                    }
                }
            }
            Check(told, "What's new in " + lang + ".json says in a \"" + fixedWord.Trim() + "\" line that every button shows and the Campus offers the student loan");
        }
    }

    private static string Read(string scripts, string name)
    {
        string path = Path.Combine(scripts, name.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
    }

    // A method's body, from its signature to the closing brace at its indent.
    private static string Body(string text, string signature)
    {
        int at = text.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        int end = text.IndexOf("\n        }\n", at, StringComparison.Ordinal);
        return end < 0 ? null : text.Substring(at, end - at);
    }

    private static string Between(string text, string from, string to)
    {
        int at = text.IndexOf(from, StringComparison.Ordinal);
        int end = at < 0 ? -1 : text.IndexOf(to, at, StringComparison.Ordinal);
        return end < 0 ? null : text.Substring(at, end - at);
    }
}
