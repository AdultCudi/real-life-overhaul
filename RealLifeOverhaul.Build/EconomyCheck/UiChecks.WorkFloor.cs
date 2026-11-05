using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;

// The floor of every Work page: no strip of the window's page leaves more than FloorMost px between its lowest thing and the window's foot (common.md 2.3, RLO-UI-010 and
// 016), measured on the laid scene of every state a list or a form fills (an empty state, a filter that leaves one card and a sub page's form are bare by nature and are named).
// And the rule that fills it: how many bands stand in a row, and that the bands under a list stand beside its detail panel first and across both under it.
internal static partial class Program
{
    private static bool _workFloorChecked;

    // The most a strip of a page may leave between its last thing and the window's foot. The detail panels and the lists end on their content, and a short page is
    // filled with bands (WorkForm.Flow), so what is left is a margin and one band's slack.
    private const float WorkFloorMost = 200f;

    // States bare by what they are: nothing to list, a filter that holds one or two cards (the whole catalogue's bands stand under it when they fit), a typed brief and a person.
    private static bool WorkFloorExempt(string state) => state.Contains("_none") || state.Contains("_empty") || state.EndsWith("_none", StringComparison.Ordinal) || state == "person"
        || state == "person_contract" || state == "new_ad" || state == "search" || state == "results_new" || state == "applicants_none" || state == "listings_none"
        || state == "courses_cooking" || state == "listings_stock" || state == "listings_machines";

    /// <summary>How far the lowest thing of the page's strips stands over the window's foot, the worst of them and where it is; -1 when no window is found.</summary>
    private static float WorkFloorOf(UiScene s, out string where)
    {
        where = string.Empty;
        int window = -1;
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].W > 1400f && s.Items[i].H > 700f && (window < 0 || s.Items[i].W * s.Items[i].H > s.Items[window].W * s.Items[window].H))
            {
                window = i;
            }
        }
        if (window < 0)
        {
            return -1f;
        }
        UiItem win = s.Items[window];
        float left = win.X, floor = win.Y + win.H - UiTokens.S4, sidebar = left;
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (i != window && s.Items[i].H > 700f && s.Items[i].X < left + UiTokens.S4 * 2f && s.Items[i].X + s.Items[i].W < left + win.W * 0.4f)
            {
                sidebar = Math.Max(sidebar, s.Items[i].X + s.Items[i].W);   // a nav's column stands apart from the page
            }
        }
        const float Strip = 160f;
        float worst = 0f;
        for (float x = sidebar + UiTokens.S4; x + Strip <= win.X + win.W - UiTokens.S4 + 0.5f; x += Strip)
        {
            float lowest = win.Y + UiTokens.S12 * 2f;   // under the window's title strip and tabs
            string by = string.Empty;
            for (int i = 0; i < s.ItemCount; i++)
            {
                UiItem it = s.Items[i];
                if (i == window || it.Layer != 0 || it.Clip >= 0 || it.W > win.W * 0.9f || it.Y >= win.Y + win.H || it.X + it.W <= x + 1f || it.X >= x + Strip - 1f)
                {
                    continue;
                }
                if (it.Y + it.H > lowest)
                {
                    lowest = it.Y + it.H;
                    by = it.Kind + " " + (it.Text ?? string.Empty) + " " + (int)it.X + "," + (int)it.Y;
                }
            }
            for (int c = 0; c < s.ClipCount; c++)
            {
                // a scrolling list or grid stands as one thing, as far down as its content goes
                UiClip clip = s.Clips[c];
                if (clip.X + clip.W > x + 1f && clip.X < x + Strip - 1f)
                {
                    lowest = Math.Max(lowest, clip.Y + Math.Min(clip.H, Math.Max(0f, clip.ContentHeight - clip.Offset)));
                }
            }
            if (floor - lowest > worst)
            {
                worst = floor - lowest;
                where = "x " + (int)x + " to " + (int)(x + Strip) + ", " + (int)(floor - lowest) + " px, lowest " + by;
            }
        }
        return worst;
    }

    private static void WorkFloorChecks()
    {
        if (_workFloorChecked)
        {
            return;
        }
        _workFloorChecked = true;
        bool report = Environment.GetEnvironmentVariable("RLO_FLOOR_REPORT") == "1";
        var faults = new StringBuilder();
        int pages = 0;
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiWords words = lang == "en" ? WorkEnglish : UiWords(lang);   // the longer words make a taller panel and a shorter list: every language is held to it
            foreach (string id in new[] { "career", "campus", "open_roles", "marketplace" })
            {
                foreach (string st in WorkStatesOf(id))
                {
                    UiScene scene = WorkSceneOf(WorkScreenOf(id, words, st));
                    float worst = WorkFloorOf(scene, out string where);
                    if (report)
                    {
                        Console.WriteLine("  floor " + lang + " " + id + " " + st + ": " + (int)worst + " (" + where + ")" + (WorkFloorExempt(st) ? " exempt" : string.Empty));
                    }
                    if (WorkFloorExempt(st))
                    {
                        continue;
                    }
                    pages++;
                    if (worst < 0f || worst > WorkFloorMost)
                    {
                        faults.Append(" [" + lang + " " + id + " " + st + ": " + (int)worst + " px at " + where + "]");
                    }
                }
            }
        }
        Check(faults.Length == 0 && pages > 60, "work screens: no page leaves more than " + (int)WorkFloorMost + " px of nothing between its lowest thing and the window's foot (" + pages + " pages)" + faults);
        // the rule that fills it: bands in balanced rows, never one alone beside a full row
        string rows = string.Join("|", Counts(WorkForm.RowCounts(5, 3))) + " " + string.Join("|", Counts(WorkForm.RowCounts(4, 3))) + " " + string.Join("|", Counts(WorkForm.RowCounts(6, 3)))
            + " " + string.Join("|", Counts(WorkForm.RowCounts(7, 4))) + " " + string.Join("|", Counts(WorkForm.RowCounts(1, 3))) + " " + WorkForm.RowCounts(0, 3).Length;
        Check(rows == "3|2 2|2 3|3 4|3 1 0", "work screens: bands stand in balanced rows (5 over 3 columns " + rows + ")");
        WorkFlowCheck();
        WorkBandMakers();
        if (Environment.GetEnvironmentVariable("RLO_CUT_REPORT") == "1")
        {
            WorkCutReport();
        }
    }

    // Every single-line text of a Work page that is wider than its box (the preview draws it with an ellipsis), in English, Brazilian Portuguese and Simplified Chinese:
    // what a player would see cut. A report for the review, not a check: a name from the game or a player's own may be cut by design.
    private static void WorkCutReport()
    {
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiWords words = lang == "en" ? WorkEnglish : UiWords(lang);
            foreach (string id in new[] { "career", "campus", "open_roles", "marketplace" })
            {
                foreach (string st in WorkStatesOf(id))
                {
                    var scene = new UiScene();
                    WorkScreenOf(id, words, st).Lay(scene, UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
                    var seen = new HashSet<string>();
                    for (int i = 0; i < scene.ItemCount; i++)
                    {
                        UiItem it = scene.Items[i];
                        if (it.Kind != UiKind.Text || string.IsNullOrEmpty(it.Text) || (it.Flags & UiFlags.Wrap) != 0 || it.Layer != 0)
                        {
                            continue;
                        }
                        float width = UiTextRules.Width(it.Text, it.Style);
                        bool ends = it.Text.EndsWith("...", StringComparison.Ordinal) || it.Text.EndsWith("…", StringComparison.Ordinal);
                        if ((width > it.W + 0.5f || ends) && seen.Add(it.Text))
                        {
                            Console.WriteLine("  cut " + lang + " " + id + " " + st + ": '" + it.Text + "' " + (int)width + " in " + (int)it.W + " at " + (int)it.X + "," + (int)it.Y);
                        }
                    }
                }
            }
        }
    }

    // The bands the pages fill their feet with are made from lists by shared makers: each puts the right line first and leaves out what it cannot say.
    private static void WorkBandMakers()
    {
        UtilitiesWords w = UiSamples.UtilitiesWordsOf(WorkEnglish);
        // who joined last and who is longest with the firm: from three people up, two bands, each ordered by the day
        var bands = new List<WorkFormBand>();
        WorkBands.Tenure(bands, w, new[] { "A", "B", "C", "D" }, new[] { "r", "r", "r", "r" }, new[] { 300, 380, 250, 390 }, new[] { "d300", "d380", "d250", "d390" });
        var few = new List<WorkFormBand>();
        WorkBands.Tenure(few, w, new[] { "A", "B" }, new[] { "r", "r" }, new[] { 300, 380 }, new[] { "d", "d" });
        Check(bands.Count == 2 && bands[0].Rows[0].Person.Name == "D" && bands[0].Rows[1].Person.Name == "B" && bands[1].Rows[0].Person.Name == "C" && bands[1].Rows[1].Person.Name == "A" && few.Count == 0,
            "work screens: the team's newest and longest serving are found by the day each joined, and a team of two is not split");
        // applicants: the strongest craft first, the lowest ask first
        var cand = new List<WorkFormBand>();
        WorkBands.Candidates(cand, w, new[] { "A", "B", "C", "D" }, new[] { "f", "f", "f", "f" }, new[] { 60, 90, 75, 40 }, new[] { 30, 40, 20, 25 }, new[] { "$30", "$40", "$20", "$25" });
        Check(cand.Count == 2 && cand[0].Rows[0].Person.Name == "B" && cand[0].Rows[1].Person.Name == "C" && cand[1].Rows[0].Person.Name == "C" && cand[1].Rows[1].Person.Name == "D",
            "work screens: the strongest applicants and the cheapest to hire are found by their craft and their ask");
        // the market: the deal furthest under its price first; the seller with most listings first
        var rows = new List<WorkRow>
        {
            new WorkRow { Id = 1, Name = "a", Seller = "S1", Price = "$1", Under = "-5%", UnderValue = -5 }, new WorkRow { Id = 2, Name = "b", Seller = "S2", Price = "$1", Under = "-20%", UnderValue = -20 },
            new WorkRow { Id = 3, Name = "c", Seller = "S2", Price = "$1", Under = "+3%", UnderValue = 3 }, new WorkRow { Id = 4, Name = "d", Seller = "S2", Price = "$1", Under = "-9%", UnderValue = -9 }
        };
        var market = new List<WorkFormBand>();
        MarketBands.Market(w, market, rows);
        Check(market.Count == 2 && market[0].Rows[0].Label == "b" && market[0].Rows[1].Label == "d" && market[1].Rows[0].Person.Name == "S2" && market[1].Rows[0].Person.Role == "3 listings",
            "work screens: the best deals and the busiest sellers are found from the listings' own figures");
        // courses: the cheapest first, the shortest first, a distance course (no length) never the shortest
        var courses = new List<WorkRow>();
        for (int i = 0; i < 5; i++)
        {
            courses.Add(new WorkRow { Id = i + 1, Name = "c" + i, Sub = "s", FootRight = "$" + (500 - i * 100), FootLeft = "w", Values = new[] { 500 - i * 100, i == 4 ? 0 : 7 * (i + 1) } });
        }
        var picks = new List<WorkFormBand>();
        CampusBands.Picks(w, picks, courses);
        Check(picks.Count == 2 && picks[0].Rows[0].Label == "c4" && picks[1].Rows[0].Label == "c0" && picks[1].Rows.Count == 3, "work screens: the cheapest and the shortest courses are found by their own fee and length");
        // the transcript's figures are the best and the lowest graded exam and the attendance on average of its own lines
        var st = new WorkStatement();
        var term = new WorkTerm();
        term.Marks.Add(new WorkMark { ExamGrade = 74, AttendanceGrade = 100 });
        term.Marks.Add(new WorkMark { ExamGrade = 52, AttendanceGrade = 90 });
        term.Marks.Add(new WorkMark { ExamGrade = -1, AttendanceGrade = 80 });
        st.Terms.Add(term);
        CampusBands.MarkFigures(w, st);
        Check(st.Below.Count == 1 && st.Below[0].Rows[0].Value == "74%" && st.Below[0].Rows[1].Value == "52%" && st.Below[0].Rows[2].Value == "90%", "work screens: the transcript's figures are those of its own lines");
        var none = new WorkStatement();
        none.Terms.Add(new WorkTerm());
        CampusBands.MarkFigures(w, none);
        Check(none.Below.Count == 0, "work screens: a transcript with no graded exam says no figures");
    }

    private static string[] Counts(int[] counts)
    {
        var text = new string[counts.Length];
        for (int i = 0; i < counts.Length; i++)
        {
            text[i] = counts[i].ToString();
        }
        return text;
    }

    // Bands under a list that has a detail panel beside it: the rows that fit stand in the list's column while the panel runs on, the rest across the whole width under the
    // lower of the two, and nothing stands past the window's foot. When that cannot lay every band whole, all of them stand in rows across the width.
    private static void WorkFlowCheck()
    {
        var bands = new List<WorkFormBand>();
        for (int i = 0; i < 7; i++)
        {
            var b = new WorkFormBand { Title = "Band " + i };
            for (int r = 0; r < 2; r++)
            {
                b.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Line " + r, Value = "1" });
            }
            bands.Add(b);
        }
        var scene = new UiScene();
        scene.Begin("flow", 1920f, 1080f, true);
        float listX = 100f, listW = 1100f, fullX = 100f, fullW = 1600f, y = 400f, panelBottom = 560f, bottom = 900f;
        int before = scene.ItemCount;
        float end = WorkForm.Flow(scene, UiAt.Screen, bands, listX, y, listW, fullX, fullW, panelBottom, bottom, UiDefaultKeys.Keyboard);
        float topRowY = float.MaxValue, lowRowY = 0f, over = 0f, listRight = listX + listW;
        bool leftRowInColumn = true, lowRowWide = false;
        for (int i = before; i < scene.ItemCount; i++)
        {
            UiItem it = scene.Items[i];
            if (it.Kind != UiKind.Box || it.Fill != UiRole.Band)
            {
                continue;
            }
            topRowY = Math.Min(topRowY, it.Y);
            lowRowY = Math.Max(lowRowY, it.Y);
            over = Math.Max(over, it.Y + it.H - bottom);
        }
        for (int i = before; i < scene.ItemCount; i++)
        {
            UiItem it = scene.Items[i];
            if (it.Kind != UiKind.Box || it.Fill != UiRole.Band)
            {
                continue;
            }
            if (it.Y < lowRowY - 1f)
            {
                leftRowInColumn &= it.X + it.W <= listRight + 1f;   // the first row stands beside the panel, inside the list's width
            }
            else
            {
                lowRowWide |= it.X + it.W > listRight + 1f;       // the last row runs on under the panel
            }
        }
        Check(over <= 0.5f && end <= bottom + 0.5f && leftRowInColumn && lowRowWide && lowRowY > panelBottom, "work screens: bands under a list stand beside its panel first and across both under it, and none past the foot (" +
            (int)topRowY + ", " + (int)lowRowY + ", past by " + (int)over + ")");
    }
}
