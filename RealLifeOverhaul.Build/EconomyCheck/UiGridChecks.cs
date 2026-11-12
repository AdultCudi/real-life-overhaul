using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The app window's page shapes (Scripts/Ui: UiDiary, UiBoard, UiReader, UiSettingsList, UiConversation, UiMessageList,
// UiFeed, UiEdition) and the kit's links: every link's mark one grid step from its words as drawn, on every registered
// screen; a diary's cells never overflow; a board's bars stand on its ruler and its edits snap and keep to it; the reader
// keeps its measure, reads the Guide's longest page in every language and wraps a bold name in its own face; the
// settings list holds four hundred rows at a screen's cost; a conversation's choices answer their number keys; the
// edition's columns balance with every story whole; and what each costs to lay out.
internal static partial class Program
{
    private static void UiGrids()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        UiGridLayouts();
        UiLinkGaps();
        UiDiaryChecks();
        UiBoardChecks();
        UiReaderChecks();
        UiSettingsListChecks();
        UiConversationChecks();
        UiEditionChecks();
        UiGridTimings();
        Console.WriteLine("  Page shapes: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // Each shape's gallery page in English at 16:9, every finding of every rule said, so a broken layout names each fault.
    private static void UiGridLayouts()
    {
        var s = new UiScene();
        foreach (UiScreenCase c in UiCases())
        {
            if (!c.Def.Id.StartsWith("shape_", StringComparison.Ordinal))
            {
                continue;
            }
            UiFrame frame = UiFrame.FullMenu(16f / 9f, c.Title);
            c.Lay(s, frame, true, UiDefaultKeys.Keyboard, UiWords("en"));
            var said = new List<string>();
            Action<string> say = line =>
            {
                if (said.Count < 12)
                {
                    said.Add(line);
                }
            };
            int found = UiCheckRules.Overlaps(s, say) + UiCheckRules.Spills(s, say) + UiCheckRules.OffGrid(s, say) + UiCheckRules.Unfit(s, say) + UiCheckRules.SmallTargets(s, say)
                        + UiCheckRules.Crowded(s, say) + UiCheckRules.DeadSpace(s, say) + UiCheckRules.KeyClash(s, say) + UiCheckRules.LowContrast(s, say) + UiCheckRules.Strays(s, say)
                        + UiCheckRules.TypeLevels(s, say) + UiCheckRules.EmptyPanels(s, say) + UiCheckRules.Stacked(s, say) + UiCheckRules.UnknownCommands(s, c.Commands ?? Commands(c.Def.Template), say)
                        + UiFocusRules.Unreached(s, out string missed);
            Check(found == 0, "ui shapes: " + c.Name + " lays out clean in English at 16:9" + (found == 0 ? string.Empty : ": " + string.Join(" | ", said) + (missed != null ? " | unreached " + missed : string.Empty)));
        }
    }

    // ---------------------------------------------------------------- links

    // Every link the kit draws: its mark's box one grid step from its words as drawn, before them (a value's magnifier) or
    // after them (a row's arrow), whatever the words' width, on every registered screen and a long name cut to its room.
    private static void UiLinkGaps()
    {
        var s = new UiScene();
        int links = 0, wrong = 0;
        string said = "none";
        foreach (UiScreenCase c in UiCases())
        {
            c.Lay(s, UiFrame.FullMenu(16f / 9f, c.Title), true, UiDefaultKeys.Keyboard, UiWords("en"));
            LinkGaps(s, ref links, ref wrong, ref said);
        }
        s.Begin("links", 800f, 400f, true);
        int panel = UiDraw.Box(s, UiAt.Screen, 0f, 0f, 400f, 300f, UiRole.Panel, UiRole.None, 0f, UiFlags.Panel);
        UiAt at = UiAt.Screen.In(panel);
        UiDraw.InfoRow(s, at, 16f, 16f, 360f, "Residence", "The Stanley household at the far end of Fifth Avenue by the park", true, 2, 0);
        UiDraw.InfoRow(s, at, 16f, 48f, 360f, "Residence", "70 Fifth Avenue", true, 2, 1);
        UiDraw.LinkRow(s, at, 16f, 80f, 200f, UiTokens.PairRow, "See every one of the twelve people on the team", 3, 0);
        UiDraw.LinkRow(s, at, 16f, 112f, 360f, UiTokens.Control, "See all 12", 3, 1);
        int before = links;
        LinkGaps(s, ref links, ref wrong, ref said);
        Check(links - before == 4, "ui links: a long name cut to its room and a short one each keep their mark (" + (links - before) + " of 4 found)");
        Check(links > 20 && wrong == 0, "ui links: every link's mark stands " + UiDraw.LinkGap + " px from its words as drawn, on every screen (" + links + " links, " + wrong + " off: " + said + ")");
        Check(UiDraw.LinkGap % UiTokens.Grid == 0f, "ui links: the gap is a step of the grid");
    }

    private static void LinkGaps(UiScene s, ref int links, ref int wrong, ref string said)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem mark = ref s.Items[i];
            if (mark.Kind != UiKind.Icon || mark.Fill != UiRole.AccentText || (mark.Icon != UiIcon.Search && mark.Icon != UiIcon.Next) || (mark.Flags & UiFlags.Decor) == 0)
            {
                continue;
            }
            int words = -1;
            for (int j = Math.Max(0, i - 2); j < Math.Min(s.ItemCount, i + 3); j++)
            {
                ref UiItem t = ref s.Items[j];
                if (t.Kind == UiKind.Text && t.Ink == UiRole.AccentText && t.Parent == mark.Parent && t.Clip == mark.Clip && t.Y < mark.Y + mark.H && t.Y + t.H > mark.Y)
                {
                    words = j;
                }
            }
            if (words < 0)
            {
                continue;
            }
            ref UiItem w = ref s.Items[words];
            float ink = UiTextRules.Width(w.Text, w.Style);
            float left = (w.Flags & UiFlags.Right) != 0 ? w.X + w.W - ink : (w.Flags & UiFlags.Centre) != 0 ? w.X + (w.W - ink) / 2f : w.X;
            float gap = mark.Icon == UiIcon.Search ? left - (mark.X + mark.W) : mark.X - (left + ink);
            links++;
            if (Math.Abs(gap - UiDraw.LinkGap) > 0.01f)
            {
                wrong++;
                said = s.Name + ": \"" + w.Text + "\" " + gap.ToString("0.##") + " px from its mark";
            }
        }
    }

    // ---------------------------------------------------------------- the diary

    private static void UiDiaryChecks()
    {
        Check(UiDiary.Weeks(0, 28) == 4 && UiDiary.Weeks(6, 31) == 6 && UiDiary.Weeks(2, 30) == 5 && UiDiary.Weeks(0, 0) == 1,
            "ui diary: a month stands in as many weeks as its first day and its length need (four to six)");
        Check(UiDiary.Shown(2, 3) == 2 && UiDiary.Shown(3, 3) == 3 && UiDiary.Shown(5, 3) == 2 && UiDiary.Shown(5, 1) == 0 && UiDiary.Shown(9, 8) == 2,
            "ui diary: a day shows its things while they fit (three at most), then the last line says how many more");
        // Every cell of every frame holds its words: nothing in a day's cell reaches past it, and a full day says what it hides.
        var s = new UiScene();
        int overflow = 0, more = 0, cells = 0;
        foreach (float aspect in UiAspects)
        {
            UiSamples.Diary(s, UiFrame.FullMenu(aspect, "Calendar"), true, UiDefaultKeys.Keyboard, UiWords("en"), false);
            for (int n = 0; n < s.NodeCount; n++)
            {
                if (s.Nodes[n].Command != UiDiary.DayCommand || s.Nodes[n].H <= UiTokens.S12)
                {
                    continue;   // the days ahead's rows choose a day too; a cell is the taller target
                }
                cells++;
                int face = s.Nodes[n].Face;
                for (int i = face + 1; i < s.ItemCount; i++)
                {
                    if (!UiDraw.Inside(s, i, face))
                    {
                        continue;
                    }
                    ref UiItem it = ref s.Items[i];
                    overflow += it.X < s.Items[face].X - 0.5f || it.Y < s.Items[face].Y - 0.5f || it.X + it.W > s.Items[face].X + s.Items[face].W + 0.5f
                                || it.Y + it.H > s.Items[face].Y + s.Items[face].H + 0.5f ? 1 : 0;
                    more += it.Kind == UiKind.Text && it.Text != null && it.Text.StartsWith("+", StringComparison.Ordinal) ? 1 : 0;
                }
            }
        }
        Check(cells == 30 * UiAspects.Length && overflow == 0, "ui diary: every day's cell holds its date and its lines at every frame (" + cells + " cells, " + overflow + " over an edge)");
        Check(more >= UiAspects.Length, "ui diary: the busy day says how many more it holds (" + more + ")");
    }

    // ---------------------------------------------------------------- the board

    private static void UiBoardChecks()
    {
        var s = new UiScene();
        UiBoardModel m = UiSamples.Schedule(UiWords("en"), false, false);
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Business");
        s.Begin("board", frame.Width, frame.Height, true);
        UiAt at = UiAppWindow.Shell(s, frame, "Bloom Cafe", UiIcon.Store, new UiNavEntry[0], 0, false, UiDefaultKeys.Keyboard, out float x, out float y, out float w, out float bottom);
        UiBoard.Lay(s, at, m, x, y, w, bottom - y, UiDefaultKeys.Keyboard);
        float per = UiBoardRules.PerMinute(m);
        int bars = 0, off = 0;
        for (int n = 0; n < s.NodeCount; n++)
        {
            if (s.Nodes[n].Command != UiBoard.ShiftCommand)
            {
                continue;
            }
            bars++;
            UiBoardShift shift = m.Shifts[s.Nodes[n].Arg];
            s.NodeBox(n, out float bx, out float by, out float bw, out float bh);
            float wantX = m.RulerX + (shift.Start - m.FirstHour * 60) * per, wantW = (shift.End - shift.Start) * per;
            float laneTop = m.LanesTop + shift.Lane * m.LaneHeight - m.Offset;
            off += Math.Abs(bx - wantX) > 0.5f || Math.Abs(bw - Math.Max(wantW, UiTokens.MinTarget)) > 0.5f || by < laneTop || by + bh > laneTop + m.LaneHeight ? 1 : 0;
        }
        Check(bars > 8 && off == 0, "ui board: every shift's bar stands on the ruler at its hours and inside its lane (" + bars + " bars, " + off + " off)");
        // The edits: a body dragged moves by whole hours and keeps its length, an end keeps to the shortest and longest, a
        // stroke over an empty lane makes a shift of whole hours, and nothing leaves the ruler.
        float hour = per * 60f;
        UiBoardRules.Drag(m, 0, UiBoardGrip.Body, 500f, 0f, 500f + hour * 2.4f, 0f, out int lane, out int start, out int end);
        Check(start == 9 * 60 && end == 17 * 60 && lane == 0, "ui board: a body dragged two hours and a bit moves by two whole hours and keeps its length (" + start + " to " + end + ")");
        UiBoardRules.Drag(m, 0, UiBoardGrip.Body, 500f, 0f, 500f - hour * 9f, 0f, out lane, out start, out end);
        Check(start == m.FirstHour * 60 && end - start == 8 * 60, "ui board: a body dragged past the ruler's start stops there, its length kept");
        float laneY = m.LanesTop + 3 * m.LaneHeight + 10f - m.Offset;
        UiBoardRules.Drag(m, 0, UiBoardGrip.Body, 500f, m.LanesTop + 10f, 500f, laneY, out lane, out start, out end);
        Check(lane == 3 && start == m.Shifts[0].Start, "ui board: a body dragged down lands on the lane under the pointer");
        UiBoardRules.Drag(m, 0, UiBoardGrip.End, 0f, 0f, -hour * 20f, 0f, out lane, out start, out end);
        Check(end - start == m.Shortest, "ui board: an end dragged past its start keeps the shortest shift (" + (end - start) + " minutes)");
        UiBoardRules.Drag(m, 0, UiBoardGrip.End, 0f, 0f, hour * 20f, 0f, out lane, out start, out end);
        Check(end - start <= m.Longest && end <= m.LastHour * 60, "ui board: an end dragged far keeps the longest shift and the ruler (" + (end - start) + " minutes)");
        UiBoardRules.Drag(m, 0, UiBoardGrip.Start, 0f, 0f, -hour * 1.6f, 0f, out lane, out start, out end);
        Check(start == m.FirstHour * 60, "ui board: a start dragged back past the ruler snaps to the hour and stops at the ruler's start (" + start + ")");
        UiBoardRules.Paint(m, m.RulerX + hour * 3.2f, m.RulerX + hour * 6.6f, out start, out end);
        Check(start == 9 * 60 && end == 13 * 60, "ui board: a stroke over an empty lane makes a shift of the whole hours it covers (" + start + " to " + end + ")");
        UiBoardRules.Paint(m, m.RulerX + hour * 3.1f, m.RulerX + hour * 3.2f, out start, out end);
        Check(end - start == m.Shortest, "ui board: a click's stroke makes the shortest shift");
        Check(UiBoardRules.Grip(m, 0, ShiftX(m, 0, per) + 2f) == UiBoardGrip.Start && UiBoardRules.Grip(m, 0, ShiftX(m, 0, per) + 8f * hour / 2f) == UiBoardGrip.Body
              && UiBoardRules.Grip(m, 0, ShiftX(m, 0, per) + 8f * hour - 2f) == UiBoardGrip.End, "ui board: a pointer takes a shift by its start, its body or its end");
        UiBoardRules.Nudge(m, 0, 1, 0, false, out lane, out start, out end);
        Check(start == m.Shifts[0].Start + 60 && end == m.Shifts[0].End + 60, "ui board: a key's step moves the chosen shift an hour");
        UiBoardRules.Nudge(m, 0, -1, 0, true, out lane, out start, out end);
        Check(start == m.Shifts[0].Start && end == m.Shifts[0].End - 60, "ui board: Shift or a shoulder with a step resizes its end");
        UiBoardRules.Nudge(m, 0, 0, 1, false, out lane, out start, out end);
        Check(lane == 1, "ui board: down moves the chosen shift onto the next lane");
        Check(UiBoardRules.LaneAt(m, m.LanesTop + 5f) == 0 && UiBoardRules.LaneAt(m, m.LanesTop - 5f) == -1 && UiBoardRules.MinuteAt(m, m.RulerX - 50f) == m.FirstHour * 60,
            "ui board: a place reads as a lane and a minute, and nothing outside the lanes or the ruler");
        m.Offset = 0f;
        Check(UiBoardRules.EdgeScroll(m, m.LanesTop + 2f) == 0 && UiBoardRules.EdgeScroll(m, m.LanesTop + m.LanesHeight - 2f) == 1,
            "ui board: a drag resting at the foot scrolls the lanes on, and at the top of the first lane nothing more that way");
        Check(UiBoardRules.Reveal(m, m.Lanes.Length - 1) > 0f, "ui board: the last lane is brought into view by scrolling");
    }

    private static float ShiftX(UiBoardModel m, int shift, float per) => m.RulerX + (m.Shifts[shift].Start - m.FirstHour * 60) * per;

    // ---------------------------------------------------------------- the reader

    private static void UiReaderChecks()
    {
        float column = UiReaderRules.ColumnWidth();
        float letters = UiReaderRules.Letters(column);
        Check(letters >= UiReaderRules.LeastLetters && letters <= UiReaderRules.MostLetters, "ui reader: the column holds a reading measure, " + letters.ToString("0") + " letters (60 to 75)");
        UiReaderBlock[] parsed = UiReaderRules.Parse("# What it is\n- A <b>Construction Company</b> is a business.\n- Hire staff.\n\nA paragraph\nover two lines.\n\n## Fees\n1. Pay\n> A note.\n![A site](Hammer)\n[Hiring a builder](7)");
        Check(parsed.Length == 9 && parsed[0].Kind == UiReaderKind.Heading && parsed[1].Kind == UiReaderKind.Bullet && parsed[3].Kind == UiReaderKind.Paragraph
              && parsed[3].Text == "A paragraph over two lines." && parsed[4].Kind == UiReaderKind.Subheading && parsed[5].Kind == UiReaderKind.Numbered && parsed[5].Number == 1
              && parsed[6].Kind == UiReaderKind.Note && parsed[7].Kind == UiReaderKind.Picture && parsed[7].Icon == UiIcon.Hammer && parsed[8].Kind == UiReaderKind.Link && parsed[8].Target == 7,
            "ui reader: a page's marks read as headings, lines, paragraphs, numbers, notes, pictures and links");
        var bold = new List<bool>();
        string plain = UiReaderRules.Plain("Open it with <b>BizMan</b>, then <i>press</i> <b>Found</b>.", bold);
        Check(plain == "Open it with BizMan, then press Found." && bold[13] && !bold[19] && bold[plain.IndexOf("Found", StringComparison.Ordinal)] && bold.Count == plain.Length,
            "ui reader: the tags go and the bold letters are marked");
        // Wrapping measures each letter in its own face: no line of bold names runs past its width, and plain words wrap as the kit's own rule does.
        var lines = new List<UiReaderLine>();
        string guide = UiWords("en")("reallife_guide_construction", string.Empty);
        int over = 0, total = 0, same = 0, compared = 0;
        foreach (UiReaderBlock b in UiReaderRules.Parse(guide))
        {
            string p = UiReaderRules.Plain(b.Text, bold);
            int n = UiReaderRules.Wrap(p, bold, UiStyle.Body, column, lines);
            total += n;
            for (int l = 0; l < n; l++)
            {
                over += UiReaderRules.Width(p, bold, UiStyle.Body, lines[l].Start, lines[l].End) > column + 0.01f ? 1 : 0;
            }
            bool anyBold = bold.Contains(true);
            if (!anyBold && p.Length > 0)
            {
                compared++;
                same += n == UiTextRules.Lines(p, UiStyle.Body, column, 0, out float _) ? 1 : 0;
            }
        }
        Check(guide.Length > 20000 && total > 300 && over == 0, "ui reader: the Guide's longest page (" + guide.Length + " letters, " + total + " lines) wraps with no line past the column");
        Check(compared > 10 && same == compared, "ui reader: plain words wrap into as many lines as the kit's own rule finds (" + same + " of " + compared + ")");
        // Laid out in every language, every anchor in its list, only the blocks in view in the scene.
        var s = new UiScene();
        int most = 0;
        foreach (string lang in ModLanguages.All)
        {
            UiSamples.Reader(s, UiFrame.FullMenu(16f / 9f, "Guide"), true, UiDefaultKeys.Keyboard, UiWords(lang), UiWords(lang)("reallife_guide_construction", string.Empty), 30);
            most = Math.Max(most, s.ItemCount);
        }
        Check(most < 900, "ui reader: the longest page lays out only what is in view, in every language (" + most + " items at most)");
        UiReaderModel m = UiSamples.GuidePage(guide, -1);
        s.Begin("reader", 1920f, 1080f, true);
        UiReader.Lay(s, UiAt.Screen, m, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);
        int sections = m.SectionAnchors.Length;
        m.Reveal = m.SectionAnchors[sections - 1];
        UiReader.Lay(s, UiAt.Screen, m, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);
        Check(sections > 15 && m.InView >= sections - 3 && m.Offset > 0f && m.Offset <= m.ContentHeight - m.ViewHeight + 0.5f,
            "ui reader: an anchor stepped to scrolls into view and the contents follow (" + sections + " sections, section " + (m.InView + 1) + " in view)");
    }

    // ---------------------------------------------------------------- the settings list

    private static void UiSettingsListChecks()
    {
        UiSettingsModel m = UiSamples.SettingsList(UiWords("en"), null, 0);
        int sections = 0;
        for (int i = 0; i < m.RowCount; i++)
        {
            sections += m.Rows[i].Control == UiSettingControl.None ? 1 : 0;
        }
        Check(m.RowCount >= 380 && sections >= 120, "ui settings: the sample is the mod's whole options page (" + m.RowCount + " rows in " + sections + " sections)");
        var s = new UiScene();
        int mostItems = 0, mostTargets = 0;
        float[] tops = { 0f, 4000f, 9000f, 1e6f };
        foreach (float offset in tops)
        {
            m.Offset = offset;
            s.Begin("settings", 1920f, 1080f, true);
            UiSettingsList.Lay(s, UiAt.Screen, m, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);
            mostItems = Math.Max(mostItems, s.ItemCount);
            mostTargets = Math.Max(mostTargets, s.NodeCount);
        }
        Check(mostItems < 300 && mostTargets <= UiCheckRules.MostTargets, "ui settings: four hundred rows lay out at a screenful's cost (" + mostItems + " items, " + mostTargets + " targets at most)");
        Check(Math.Abs(m.Offset - (UiSettingsList.ContentHeight(m) - s.Clips[0].H)) < 0.5f, "ui settings: a scroll past the end stops at the last row");
        // A greyed row takes no press and reads quieter; the search's letters are marked.
        UiSamples.Settings(s, UiFrame.FullMenu(16f / 9f, "Mod settings"), true, UiDefaultKeys.Keyboard, UiWords("en"), null, UiSamples.SettingSection("Family"), 1);
        int greyed = 0, live = 0;
        for (int n = 0; n < s.NodeCount; n++)
        {
            if (s.Nodes[n].Command == UiSettingsList.RowCommand)
            {
                greyed += s.Nodes[n].Disabled ? 1 : 0;
                live += s.Nodes[n].Disabled ? 0 : 1;
            }
        }
        Check(greyed >= 3 && live >= 1, "ui settings: a section's main switch off greys the rest of its rows, the switch itself still pressed (" + greyed + " greyed)");
        UiSamples.Settings(s, UiFrame.FullMenu(16f / 9f, "Mod settings"), true, UiDefaultKeys.Keyboard, UiWords("en"), "police", 0, 0);
        int marks = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            marks += s.Items[i].Kind == UiKind.Box && s.Items[i].Fill == UiRole.AccentSoft ? 1 : 0;
        }
        Check(marks >= 3, "ui settings: a search marks the letters it found in each row's name (" + marks + ")");
        m.Offset = 0f;
        m.Reveal = 200;
        s.Begin("settings", 1920f, 1080f, true);
        UiSettingsList.Lay(s, UiAt.Screen, m, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);
        bool shown = false;
        for (int n = 0; n < s.NodeCount; n++)
        {
            shown |= s.Nodes[n].List >= 0 && s.Nodes[n].Index == 200;
        }
        Check(shown, "ui settings: the d-pad's next row past those in view scrolls into view");
    }

    // ---------------------------------------------------------------- the conversation

    private static void UiConversationChecks()
    {
        var s = new UiScene();
        foreach (bool docked in new[] { false, true })
        {
            if (docked)
            {
                UiSamples.ConversationDocked(s, UiFrame.FullMenu(16f / 9f, "Talk"), true, UiDefaultKeys.Keyboard, UiWords("en"));
            }
            else
            {
                UiSamples.ConversationWindow(s, UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard, UiWords("en"));
            }
            int choices = 0, keyed = 0;
            for (int n = 0; n < s.NodeCount; n++)
            {
                if (s.Nodes[n].Command == UiConversation.ChoiceCommand)
                {
                    choices++;
                    keyed += s.Nodes[n].Key == (UiAction)((int)UiAction.Pick1 + s.Nodes[n].Arg) && UiFocusRules.Keyed(s, s.Nodes[n].Key) == n ? 1 : 0;
                }
            }
            Check(choices == 3 && keyed == 3, "ui conversation: each choice answers its number's key" + (docked ? " (docked)" : " (in the window)"));
        }
        // Docked, it stands at the right third over the world, the city at its left.
        int panel = -1;
        for (int i = 0; i < s.ItemCount && panel < 0; i++)
        {
            panel = (s.Items[i].Flags & UiFlags.Panel) != 0 ? i : -1;
        }
        Check(panel >= 0 && s.Floats && s.Items[panel].X >= 1920f * 0.6f && s.Items[panel].X + s.Items[panel].W <= 1920f,
            "ui conversation: docked, it stands at the screen's right third with the world beside it");
    }

    // ---------------------------------------------------------------- the edition

    private static void UiEditionChecks()
    {
        foreach (bool quiet in new[] { false, true })
        {
            UiEditionModel m = UiSamples.Paper(quiet);
            foreach (float w in new[] { 1380f, 1100f, 1896f })
            {
                int columns = UiEdition.ColumnCount(w);
                float cw = UiDraw.SnapDown((w - UiTokens.S6 * (columns - 1)) / columns);
                var units = new List<UiEdition.Unit>();
                var column = new int[64];
                var heights = new float[columns];
                UiEdition.Balance(m, cw, columns, units, column, heights);
                float tallest = 0f, shortest = float.MaxValue, biggest = 0f;
                bool ordered = true;
                for (int c = 0; c < columns; c++)
                {
                    tallest = Math.Max(tallest, heights[c]);
                    shortest = Math.Min(shortest, heights[c]);
                }
                bool whole = true;
                for (int i = 0, from = 0; i < units.Count; i++)
                {
                    ordered &= i == 0 || column[i] >= column[i - 1];
                    whole &= i == 0 || units[i].Section != units[i - 1].Section || column[i] == column[i - 1];
                    if (i + 1 == units.Count || units[i + 1].Section != units[i].Section)
                    {
                        float section = 32f;
                        for (int k = from; k <= i; k++)
                        {
                            section += units[k].Height;
                        }
                        biggest = Math.Max(biggest, section);
                        from = i + 1;
                    }
                }
                bool balanced = quiet || tallest - shortest <= biggest + 0.5f;
                Check(ordered && whole && balanced && units.Count > 0, "ui edition: " + (quiet ? "a quiet" : "a busy") + " day's stories run in " + columns + " columns at " + w
                    + " px, in order, each section whole in one column, the columns within a section of each other (" + tallest + " and " + shortest + "; " + UnitHeights(units, column) + ")");
            }
        }
    }

    private static string UnitHeights(List<UiEdition.Unit> units, int[] column)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < units.Count; i++)
        {
            sb.Append(units[i].Section).Append(':').Append(units[i].Height).Append('@').Append(column[i]).Append(' ');
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- what it costs

    private static void UiGridTimings()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Guide");
        UiWords en = UiWords("en");
        string guide = en("reallife_guide_construction", string.Empty);
        UiReaderModel m = UiSamples.GuidePage(guide, -1);
        UiAt at = UiAt.Screen;
        UiReader.Lay(s, at, m, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);   // the first: every block's height found once
        var watch = Stopwatch.StartNew();
        const int runs = 40;
        for (int i = 0; i < runs; i++)
        {
            m.Offset = i * 300f;
            s.Begin("reader", frame.Width, frame.Height, true);
            UiReader.Lay(s, at, m, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);
        }
        double reader = watch.Elapsed.TotalMilliseconds / runs;
        Check(reader < 4.0, "ui reader: a scroll of the Guide's longest page lays out in " + reader.ToString("0.00") + " ms (under 4; its heights kept)");
        UiSettingsModel settings = UiSamples.SettingsList(en, null, 0);
        watch.Restart();
        for (int i = 0; i < runs; i++)
        {
            settings.Offset = i * 400f;
            s.Begin("settings", frame.Width, frame.Height, true);
            UiSettingsList.Lay(s, at, settings, 400f, 100f, 1300f, 800f, UiDefaultKeys.Keyboard);
        }
        double rows = watch.Elapsed.TotalMilliseconds / runs;
        Check(rows < 4.0, "ui settings: a scroll of four hundred rows lays out in " + rows.ToString("0.00") + " ms (under 4)");
    }
}
