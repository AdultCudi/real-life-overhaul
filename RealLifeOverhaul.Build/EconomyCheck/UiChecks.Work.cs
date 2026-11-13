using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;

// Career, Campus, Open Roles and the Marketplace on the kit: every state laid out in every language, and the rules the wave's faults made:
// every tab its own page, every action reaches its source as the kind it was drawn with (a recording source fails an action that arrives as
// another), no action number of an app is one the screen takes itself, every action acts or says why not, a chip counts the rows it holds,
// the amounts step, a search narrows, Back steps out of a sub page, and no command of these screens is another part's.
internal static partial class Program
{
    private static bool _workUiChecked;

    private static readonly int[] WorkCommandsAll =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiAppWindow.RowCommand, UiAppWindow.MoreCommand, UiAppWindow.PersonCommand,
            UiDataTable.First + UiDataTable.Row, UiDataTable.First + UiDataTable.Act, UiDataTable.First + UiDataTable.Clear,
        WorkCmd.Less, WorkCmd.Type, WorkCmd.More, WorkCmd.Search, WorkCmd.Card, WorkCmd.PageTab, WorkCmd.Aside, WorkCmd.Pick,
            WorkCmd.Sort, WorkCmd.NudgeLess, WorkCmd.NudgeMore, WorkCmd.Read,
        UiDiary.DayCommand, UiDiary.MonthCommand, UiDiary.EntryCommand, UiChart.First + UiChart.Point, UiTrack.First + UiTrack.Step,
        UiReader.AnchorCommand, UiReader.RelatedCommand, UiReader.ContentsCommand
    };

    static partial void UiCasesWork(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "career" && def.Id != "campus" && def.Id != "open_roles" && def.Id != "marketplace")
        {
            return;
        }
        string[] states = WorkStatesOf(def.Id);
        foreach (string state in states)
        {
            string st = state, id = def.Id;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = id + " (" + st + ")", Title = def.Id, Commands = WorkCommandsAll,
                Lay = (s, f, dark, k, t) => WorkScreenOf(id, t, st).Lay(s, f, dark, k)
            });
        }
        if (!_workUiChecked)
        {
            _workUiChecked = true;
            WorkUiChecks();
        }
    }

    private static string[] WorkStatesOf(string id) => id == "career" ? UiSamples.CareerStates : id == "campus" ? UiSamples.CampusStates : id == "open_roles" ? UiSamples.RolesStates : UiSamples.MarketplaceStates;

    private static WorkScreen WorkScreenOf(string id, UiWords t, string state)
    {
        return id == "career" ? UiSamples.CareerScreenOf(t, state) : id == "campus" ? UiSamples.CampusScreenOf(t, state) : id == "open_roles" ? UiSamples.RolesScreenOf(t, state)
            : UiSamples.MarketScreenOf(t, state);
    }

    private static string WorkEnglish(string key, string english) => english;

    private static WorkScreen LaidWork(string id, string state)
    {
        WorkScreen screen = WorkScreenOf(id, WorkEnglish, state);
        screen.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        return screen;
    }

    private static void WorkUiChecks()
    {
        string[] ids = { "career", "campus", "open_roles", "marketplace" };
        var dead = new StringBuilder();
        var numbers = new StringBuilder();
        var chipFaults = new StringBuilder();
        int pressed = 0, kinds = 0;
        foreach (string id in ids)
        {
            string[] states = WorkStatesOf(id);
            var seen = new HashSet<string>();
            int tabs = 0;
            bool different = true;
            for (int tab = 0; tab < 12; tab++)
            {
                string chosen = null;
                foreach (string st in states)
                {
                    if (st.EndsWith("_pt", StringComparison.Ordinal) || st.Contains("_none") || st.Contains("_new") || st.Contains("_empty") || st.Contains("_long") || st == "person" || st == "new_ad"
                        || st == "haggle" || st == "person_contract" || st.Contains("_full") || st.Contains("_cooking") || st.Contains("_machines") || st.Contains("_stock") || st.Contains("_qualify"))
                    {
                        continue;
                    }
                    if (WorkScreenOf(id, WorkEnglish, st).View.Tab == tab)
                    {
                        chosen = st;
                        break;
                    }
                }
                if (chosen == null)
                {
                    continue;
                }
                tabs++;
                WorkScreen screen = LaidWork(id, chosen);
                different &= seen.Add(WorkSignature(screen.Page, tab));
            }
            Check(tabs >= 4 && different, "work screens: " + id + ": every section builds a page of its own (" + tabs + " sections, no two the same)");
            // every state: every action, row, link and button is pressed through the screen, and what reaches the source is what was drawn
            foreach (string st in states)
            {
                WorkScreen screen = LaidWork(id, st);
                var src = (UiSamples.SampleWork)screen.Source;
                WorkPage page = screen.Page;
                for (int a = 0; a < page.Acts.Length; a++)
                {
                    WorkDo d = page.Acts[a];
                    bool off = page.Model.Actions[a].Disabled;
                    bool own = d.Kind >= WorkKinds.AppsFirst;
                    bool screens = d.Kind >= WorkKinds.Filter && d.Kind <= WorkKinds.Back;
                    if (d.Any && !own && !screens)
                    {
                        numbers.Append(" [" + id + " " + st + " action " + a + " has the number " + d.Kind + ", between the screen's own and the apps']");
                    }
                    kinds++;
                    if (off)
                    {
                        if (d.Any && d.Why != null && (page.Why == null || !page.Why.Contains(d.Why)))
                        {
                            dead.Append(" [" + id + " " + st + " action " + a + " is disabled without its reason said]");
                        }
                        continue;
                    }
                    WorkScreen fresh = LaidWork(id, st);
                    var fsrc = (UiSamples.SampleWork)fresh.Source;
                    fsrc.Expect = d;
                    bool did = fresh.Press(UiAppWindow.ActionCommand, a);
                    pressed++;
                    if (!did)
                    {
                        dead.Append(" [" + id + " " + st + " action " + a + " (kind " + d.Kind + ") did nothing]");
                    }
                    else if (own && (fsrc.Done.Count != 1 || fsrc.Mismatch != 0 || fsrc.Done[0].Kind != d.Kind || fsrc.Done[0].Arg != d.Arg))
                    {
                        dead.Append(" [" + id + " " + st + " action " + a + " (kind " + d.Kind + ") did not reach the source as itself: " + fsrc.Done.Count + " calls, " + fsrc.Mismatch + " other]");
                    }
                    else if (screens && fsrc.Done.Count != 0)
                    {
                        dead.Append(" [" + id + " " + st + " action " + a + " (kind " + d.Kind + ") is the screen's own and reached the source]");
                    }
                }
                WorkRowsAndLinks(id, st, page, dead, ref pressed);
                WorkChips(id, st, page, chipFaults);
            }
        }
        Check(dead.Length == 0, "work screens: every action, row, link and button of every state reaches its source as the kind it was drawn with, or says why not" + dead);
        Check(numbers.Length == 0, "work screens: no action of an app has a number that is the screen's own or between: apps start at 100" + numbers);
        Check(chipFaults.Length == 0, "work screens: a filter chip names the rows it holds, and pressing it shows that many" + chipFaults);
        Check(pressed > 150 && kinds > 100, "work screens: the walk pressed " + pressed + " things, " + kinds + " of them buttons");
        WorkWordsOnPages(ids);
        // The numbering is the wave's first fault: Career's actions met the screen's own filter, tab and back numbers.
        bool apart = true;
        foreach (CareerAct a in Enum.GetValues(typeof(CareerAct)))
        {
            apart &= a == CareerAct.None || (int)a >= WorkKinds.AppsFirst;
        }
        Check(apart, "work screens: no Career action has a number below 100 (the screen's own are 1 to 3)");
        // Another part's command number never moves these screens: only their own are answered.
        WorkScreen one = LaidWork("career", "jobs");
        Check(!one.Press(UiDecision.ChoiceCommand + 40, 0) && !one.Press(9999, 0) && !one.Press(UiInfoPanel.TabCommand + 90, 0),
            "work screens: a command that is another part's (a decision's choice, a number nobody has) does nothing");
        // A section that is not there is refused.
        Check(!one.Press(UiAppWindow.NavCommand, 99), "work screens: a press on a section that is not there is refused, not acted on");
        WorkSubAndSteps();
        WorkSearchAndOrder();
        WorkWalkChecks();
        WorkFloorChecks();
        WorkTermSums();
        WorkVehicleNames();
        WorkOneLife();
        UiAppsParity();   // (what the kit's apps and the old apps carry over from each other: UiAppsChecks.cs)
    }

    // Rows that open a page, links at a row's right, aside rows and an empty page's button: each acts.
    private static void WorkRowsAndLinks(string id, string st, WorkPage page, StringBuilder dead, ref int pressed)
    {
        for (int r = 0; page.Table != null && r < page.Table.RowCount; r++)
        {
            WorkScreen fresh = LaidWork(id, st);
            if (!page.Has(page.Rows, r) || !page.Rows[r].Opens)
            {
                if (!fresh.Press(UiDataTable.First + UiDataTable.Row, r))
                {
                    dead.Append(" [" + id + " " + st + " row " + r + " does not choose]");
                }
                pressed++;
            }
            if (page.Has(page.Locates, r))
            {
                var fsrc = (UiSamples.SampleWork)fresh.Source;
                fsrc.Expect = page.Locates[r];
                fresh.Press(UiDataTable.First + UiDataTable.Act, r);
                if (fsrc.Done.Count != 1 || fsrc.Mismatch != 0)
                {
                    dead.Append(" [" + id + " " + st + " the link of row " + r + " did not reach the source as itself]");
                }
                pressed++;
            }
            if (page.Has(page.Rows, r))
            {
                var fsrc = (UiSamples.SampleWork)fresh.Source;
                fsrc.Expect = page.Rows[r];
                fresh.Press(UiDataTable.First + UiDataTable.Row, r);
                if (page.Rows[r].Kind >= WorkKinds.AppsFirst && (fsrc.Done.Count != 1 || fsrc.Mismatch != 0))
                {
                    dead.Append(" [" + id + " " + st + " row " + r + " opens a page the source was not told of]");
                }
                pressed++;
            }
        }
        for (int c = 0; page.Cards != null && c < page.Cards.Cards.Length; c++)
        {
            if (!LaidWork(id, st).Press(WorkCmd.Card, c))
            {
                dead.Append(" [" + id + " " + st + " card " + c + " does not choose]");
            }
            pressed++;
        }
        for (int a = 0; a < page.Asides.Length; a++)
        {
            if (!LaidWork(id, st).Press(WorkCmd.Aside, a))
            {
                dead.Append(" [" + id + " " + st + " aside row " + a + " does not choose]");
            }
            pressed++;
        }
        if (page.Empty.Any && page.Table != null && page.Table.RowCount == 0)
        {
            var fresh = LaidWork(id, st);
            if (!fresh.Press(UiDataTable.First + UiDataTable.Clear, 0))
            {
                dead.Append(" [" + id + " " + st + " the empty page's button does nothing]");
            }
            pressed++;
        }
    }

    // The chips of a page: each labels the number of rows it holds, and pressing it shows that many.
    private static void WorkChips(string id, string st, WorkPage page, StringBuilder faults)
    {
        for (int a = 0; a < page.Model.Actions.Length; a++)
        {
            if (page.Model.Actions[a].Kind != UiButtonKind.Chip)
            {
                continue;
            }
            string label = page.Model.Actions[a].Label;
            int space = label.LastIndexOf(' ');
            if (space < 0 || !int.TryParse(label.Substring(space + 1), out int said))
            {
                faults.Append(" [" + id + " " + st + " chip '" + label + "' carries no count]");
                continue;
            }
            WorkScreen fresh = LaidWork(id, st);
            fresh.Press(UiAppWindow.ActionCommand, a);
            fresh.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
            int shown = fresh.Page.Table != null ? fresh.Page.Table.RowCount : fresh.Page.Cards != null ? fresh.Page.Cards.Cards.Length : -1;
            if (shown != said)
            {
                faults.Append(" [" + id + " " + st + " chip '" + label + "' shows " + shown + "]");
            }
        }
    }

    // Back steps out of a sub page first, and is left to the host on a page with none; amounts step; a typed figure and a typed name are kept.
    private static void WorkSubAndSteps()
    {
        WorkScreen person = LaidWork("open_roles", "person");
        bool tookBack = person.Back();
        person.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(tookBack && person.View.Sub == 0 && !person.Back(), "work screens: Back steps out of a person's page first, and on a page with none it is left to the host, which closes");
        foreach (string pair in new[] { "open_roles new_ad", "marketplace haggle" })
        {
            string[] p = pair.Split(' ');
            WorkScreen s = LaidWork(p[0], p[1]);
            bool back = s.Back();
            Check(back && s.View.Sub == 0, "work screens: Back steps out of " + p[1] + " to its list");
        }
        // amounts: the haggle's offer steps by its step and stays inside its ends; a typed figure is kept
        WorkScreen haggle = LaidWork("marketplace", "haggle");
        int before = haggle.Page.Amounts[0].Value;
        bool less = haggle.Press(WorkCmd.Less, 0);
        haggle.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        int after = haggle.Page.Amounts[0].Value;
        bool more = haggle.Press(WorkCmd.More, 0) && haggle.Press(WorkCmd.More, 0);
        haggle.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        haggle.Press(WorkCmd.Type, 0);
        haggle.Typed("13300");
        haggle.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(less && after == before - haggle.Page.Amounts[0].Step && more && haggle.Page.Amounts[0].Value == 13300 && haggle.TypingCommand == WorkCmd.Type,
            "work screens: the haggle's offer steps down and up and takes a typed figure (" + before + ", " + after + ", " + haggle.Page.Amounts[0].Value + ")");
        // the new ad's choices cycle and its figures step; its Posts to fill is a control
        WorkScreen ad = LaidWork("open_roles", "new_ad");
        int posts = -1;
        for (int i = 0; i < ad.Page.Amounts.Length; i++)
        {
            posts = ad.Page.Amounts[i].Id == 8 ? i : posts;
        }
        bool stepped = posts >= 0 && ad.Press(WorkCmd.More, posts);
        ad.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        bool cycled = ad.Press(WorkCmd.Pick, 0);
        ad.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(stepped && cycled && ad.Page.Amounts[posts].Value == 3 && ad.Page.Amounts[0].Value == 1,
            "work screens: the new ad's Posts to fill steps (to " + (posts >= 0 ? ad.Page.Amounts[posts].Value : -1) + ") and its role choice cycles (to " + ad.Page.Amounts[0].Value + ")");
    }

    // Every state of every app, laid: no key of the language file reaches a page unmade ("reallife_..."), no bare dash stands for a missing figure, and one is never plural ("1 weeks").
    private static void WorkWordsOnPages(string[] ids)
    {
        var bad = new StringBuilder();
        var plural = new System.Text.RegularExpressions.Regex(@"(^|\s)1 (weeks|days|visits|people|listings|applicants|courses|things|posts)(\s|$)");
        int texts = 0;
        foreach (string id in ids)
        {
            foreach (string st in WorkStatesOf(id))
            {
                var scene = new UiScene();
                WorkScreenOf(id, WorkEnglish, st).Lay(scene, UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
                for (int i = 0; i < scene.ItemCount; i++)
                {
                    string text = scene.Items[i].Text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }
                    texts++;
                    if (text.Contains("reallife_") || text == "-" || plural.IsMatch(text))
                    {
                        bad.Append(" [" + id + " " + st + ": " + text + "]");
                    }
                }
            }
        }
        Check(bad.Length == 0 && texts > 2000, "work screens: no key, bare dash or one-plural reaches a page in any state (" + texts + " texts read)" + bad);
    }

    // The search narrows the list and the page says so; a tab key moves between sections; the order of the pages holds.
    private static void WorkSearchAndOrder()
    {
        WorkScreen jobs = LaidWork("career", "jobs");
        int all = jobs.Page.Table.RowCount;
        jobs.Press(WorkCmd.Search, 0);
        jobs.Typed("chef");
        jobs.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        int narrowed = jobs.Page.Table.RowCount;
        jobs.Typed("zzz");
        jobs.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(all > 5 && narrowed >= 1 && narrowed < all && jobs.Page.Table.RowCount == 0 && jobs.TypingCommand == WorkCmd.Search,
            "work screens: Career's search narrows the jobs (" + all + ", then " + narrowed + ", then " + jobs.Page.Table.RowCount + ")");
        WorkScreen courses = LaidWork("campus", "courses");
        int cards = courses.Page.Cards.Cards.Length;
        courses.Press(WorkCmd.Search, 0);
        courses.Typed("plumbing");
        courses.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(cards > 8 && courses.Page.Cards.Cards.Length == 1, "work screens: Campus's search narrows the courses (" + cards + ", then " + courses.Page.Cards.Cards.Length + ")");
        // a tab key turns the sections round
        WorkScreen tab = LaidWork("campus", "courses");
        tab.Tab(1);
        tab.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
        Check(tab.View.Tab == 1, "work screens: the tab key turns to the next section");
    }

    private static string WorkSignature(WorkPage p, int tab)
    {
        var sb = new StringBuilder();
        sb.Append(tab).Append('|').Append(p.Model.Actions.Length).Append('|').Append(p.Table != null ? p.Table.RowCount : -1).Append('|').Append(p.Cards != null ? p.Cards.Cards.Length : -1).Append('|')
            .Append(p.Diary != null).Append(p.Reader != null).Append(p.Amounts.Length).Append('|').Append(p.Model.Profile != null ? p.Model.Profile.Role : null).Append('|');
        for (int i = 0; p.Table != null && i < p.Table.Columns.Length; i++)
        {
            sb.Append(p.Table.Columns[i].Title).Append(',');
        }
        for (int i = 0; i < p.Model.Actions.Length && i < 3; i++)
        {
            sb.Append(p.Model.Actions[i].Label).Append(';');
        }
        return sb.ToString();
    }

    // A transcript's term says how many courses it holds and how many credits they carry: the two figures added from the lines under it, in every state and
    // every language (the sample once wrote "8 credits" over lines of 2, 6 and 6, and the live source read the lines, so only a check of the page could tell).
    private static void WorkTermSums()
    {
        var wrong = new StringBuilder();
        int terms = 0;
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiWords words = lang == "en" ? WorkEnglish : UiWords(lang);
            foreach (string st in WorkStatesOf("campus"))
            {
                WorkScreen screen = WorkScreenOf("campus", words, st);
                screen.Lay(new UiScene(), UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
                WorkStatement m = screen.Page != null ? screen.Page.Statement : null;
                for (int i = 0; m != null && i < m.Terms.Count; i++)
                {
                    WorkTerm t = m.Terms[i];
                    int sum = 0;
                    foreach (WorkMark mark in t.Marks)
                    {
                        int.TryParse(mark.Credits, out int c);
                        sum += c;
                    }
                    var said = new List<int>(3);
                    foreach (System.Text.RegularExpressions.Match n in System.Text.RegularExpressions.Regex.Matches(t.Note ?? string.Empty, "[0-9]+"))
                    {
                        said.Add(int.Parse(n.Value));
                    }
                    terms++;
                    if (said.Count != 2 || said[0] != t.Marks.Count || said[1] != sum)
                    {
                        wrong.Append(" [" + lang + " " + st + " '" + t.Title + "': '" + t.Note + "' over " + t.Marks.Count + " lines of " + sum + " credits]");
                    }
                }
            }
        }
        Check(terms >= 6 && wrong.Length == 0, "work screens: every transcript term's note says the courses and credits its own lines add up to, in en, pt and zh-cn (" + terms + " terms)" + wrong);
    }

    // A listing's vehicle is named as the live source names it, by the game's own key for its type: where the game has a word of its own for it in a language,
    // that word stands on the page, never the English one (the sample once passed plain English names, so pt and zh read "Red delivery van").
    private static void WorkVehicleNames()
    {
        var wrong = new StringBuilder();
        int said = 0;
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = UiWords(lang);
            var texts = new HashSet<string>(StringComparer.Ordinal);
            foreach (string st in WorkStatesOf("marketplace"))
            {
                var scene = new UiScene();
                WorkScreenOf("marketplace", words, st).Lay(scene, UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
                for (int i = 0; i < scene.ItemCount; i++)
                {
                    texts.Add(scene.Items[i].Text ?? string.Empty);
                }
            }
            foreach (KeyValuePair<string, string> v in UiSamples.MarketVehicles)
            {
                string local = words(v.Key, v.Value);
                if (local != v.Value)
                {
                    said++;
                    if (texts.Contains(v.Value) || !texts.Contains(local))
                    {
                        wrong.Append(" [" + lang + " " + v.Key + ": '" + local + "' is not what the page says]");
                    }
                }
            }
        }
        Check(said >= 2 && wrong.Length == 0, "work screens: a vehicle of a listing is named by the game's own key, so pt and zh say the game's word and not the English one (" + said + " names translated)" + wrong);
    }

    // The CV and Own work say what the player earned in every job: one figure, read from the one place (WorkFile.Earned, which the shift's pay adds to), so the two
    // tabs of an app never give two lives. The sample shows the same figure on both pages; the live source reads the same field for both.
    private static void WorkOneLife()
    {
        string live = System.IO.File.ReadAllText(System.IO.Path.Combine(FindRoot(), "Assets/Mods/RealLifeOverhaul/Scripts/Ui/Work/CareerLive.Manage.cs"));
        bool oneField = live.Contains("long earned = w.Earned;") && live.Contains("CareersText.Cents(w.Earned)") && !live.Contains("w.History[i].Earned");
        var cv = new HashSet<string>(StringComparer.Ordinal);
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, HashSet<string>> pair in new[] { new KeyValuePair<string, HashSet<string>>("cv", cv), new KeyValuePair<string, HashSet<string>>("own", own) })
        {
            var scene = new UiScene();
            WorkScreenOf("career", WorkEnglish, pair.Key).Lay(scene, UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.Keyboard);
            for (int i = 0; i < scene.ItemCount; i++)
            {
                pair.Value.Add(scene.Items[i].Text ?? string.Empty);
            }
        }
        Check(oneField && cv.Contains("$28,400") && own.Contains("$28,400") && !cv.Contains("$38,420"),
            "work screens: the CV and Own work give one figure for what the player earned in every job (the live source reads WorkFile.Earned for both)");
    }
}
