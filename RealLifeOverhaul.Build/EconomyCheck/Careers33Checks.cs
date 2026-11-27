using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Career app's Professions tab, four faults found playing 1.0.32: every employer "had no board" until the player first walked outdoors
// (the search for where the boards stand waited for the street), a board that was only late said the city had none, "Hiring now" pushed the
// employer selector out of the detail, and a long employer name ran under the selector's arrows (Jobs/JobBoards.cs, Jobs/JobDirector.Boards.cs,
// Jobs/BoardHomeRules.cs, Careers/CareersText.cs, Ui/Work/WorkPanel.cs, Ui/UiDraw.Controls.cs).
internal static partial class Program
{
    private static void Careers33()
    {
        BoardsBeforeTheStreet();
        BoardsLateWords();
        DetailKeepsTheSelector();
        LongNamesBetweenTheArrows();
    }

    // Fault 1: the looking at the city's buildings runs once a second whatever the player is doing; reach, the late retry and the mending stay on foot.
    private static void BoardsBeforeTheStreet()
    {
        string director = ModSource(@"Jobs\JobDirector.Boards.cs");
        string boards = ModSource(@"Jobs\JobBoards.cs");
        string pace = MemberText(director, "private void Boards(GameManager gm)");
        int off = pace.IndexOf("if (!onFoot)", StringComparison.Ordinal), foot = pace.IndexOf("else", off < 0 ? 0 : off, StringComparison.Ordinal);
        string offFoot = off >= 0 && foot > off ? pace.Substring(off, foot - off) : string.Empty;
        Check(offFoot.Contains("_boards.Survey();") && offFoot.Contains("Settings.Jobs && JobState.Loaded"),
            "boards before the street: indoors or in a car the once-a-second tick still looks at the city's buildings, with the option on and the jobs loaded");
        Check(pace.Contains("_boards.Tick(gm.playerController.transform.position);") && !offFoot.Contains("_boards.Tick("),
            "boards before the street: reach, the late retry and the mending of a spare are still asked only on foot");
        string survey = MemberText(boards, "public void Survey()");
        Check(survey.Contains("Search();") && !survey.Contains("Retry(") && !survey.Contains("Mend(") && !survey.Contains("Near") && !survey.Contains("InReach"),
            "boards before the street: the survey is the search alone, never the retry or the mend that need the player near, and never reach");
        Check(survey.Contains("if (_failed || _searched)") && survey.Contains("Fail(e);"),
            "boards before the street: a finished or failed search is not run again, and a fault turns the boards off as it does on foot");
        string search = MemberText(boards, "private void Search()");
        Check(search.Contains("++_tries >= Tries") && search.Contains("_late.Add(kind);") && search.Contains("Place(list[_spare], kind, _spare)"),
            "boards before the street: the asks, the retries per building and the late list are the same rules as on foot");
        Check(director.Contains("premises.enteringBuilding || premises.exitingBuilding") && offFoot.Contains("!Transit()"),
            "boards before the street: nothing is asked in the seconds a door is taking the player through");
        string tick = MemberText(boards, "public void Tick(Vector3 player)");
        Check(tick.Contains("Retry(player);") && tick.Contains("Mend(player);") && tick.Contains("Fail(e);"),
            "boards before the street: the on-foot tick keeps the retry and the mend, and fails through the same path");
    }

    // Fault 2: a board that is only late says so; a kind the city lacks keeps the old words.
    private static void BoardsLateWords()
    {
        string[] food = Array.Find(RealLifeOverhaul.CareerCatalogue.All, e => e.Id == RealLifeOverhaul.CareerCatalogue.Food).Premises;
        string[] retail = Array.Find(RealLifeOverhaul.CareerCatalogue.All, e => e.Id == RealLifeOverhaul.CareerCatalogue.Retail).Premises;
        string[] trades = Array.Find(RealLifeOverhaul.CareerCatalogue.All, e => e.Id == RealLifeOverhaul.CareerCatalogue.Builders).Premises;
        var late = new List<string> { "ba:businesstype_coffeeshop" };
        var none = new List<string>();
        Check(RealLifeOverhaul.BoardHomeRules.Pending(food, none, false, out string unsettled) && unsettled == null,
            "late board words: while the search has not seen the whole city a board is still to come, and its kind is not yet known");
        Check(RealLifeOverhaul.BoardHomeRules.Pending(food, late, true, out string kind) && kind == "ba:businesstype_coffeeshop",
            "late board words: Uncle Lou's Street Eats, whose coffee shop board is asked again near the player, is waiting for it, at that kind");
        Check(!RealLifeOverhaul.BoardHomeRules.Pending(food, none, true, out string never) && never == null,
            "late board words: once the search is done and none of its kinds is late, the city has no board for the employer and the old words stand");
        Check(!RealLifeOverhaul.BoardHomeRules.Pending(retail, late, true, out _),
            "late board words: another employer's late kind is not this employer's board to wait for");
        Check(!RealLifeOverhaul.BoardHomeRules.Pending(trades, late, false, out _) && !RealLifeOverhaul.BoardHomeRules.Pending(null, late, true, out _),
            "late board words: the building trades have no board to wait for");

        string boards = ModSource(@"Jobs\JobBoards.cs");
        string pending = MemberText(boards, "public bool Pending(string[] premises, out string kind)");
        Check(pending.Contains("_failed") && pending.Contains("BoardHomeRules.Pending(premises, _late, _searched, out string late)") && !pending.Contains("_boards.Add")
              && !pending.Contains("_late.Add") && !pending.Contains("_late.Remove"),
            "late board words: the question is read only (it changes and saves nothing; no board is saved), and a failed board system waits for nothing");
        string text = ModSource(@"Careers\CareersText.cs");
        string line = MemberText(text, "public static string NoBoard(BoardEmployer board)");
        Check(line.Contains("board.Pending && board.Kind != null ? Fill(\"reallife_career_boards_late\"") && line.Contains("Fill(\"reallife_career_boards_soon\"")
              && line.Contains("Fill(\"reallife_career_boards_phone\""),
            "late board words: one place chooses between the three lines, the kind's, the not-yet and the city has none");
        Check(ModSource(@"Careers\CareerApp.Map.cs").Contains("CareersText.NoBoard(board)") && ModSource(@"Ui\Work\CareerLive.Boards.cs").Contains("CareersText.NoBoard(board)"),
            "late board words: the old interface's Career page and the kit's selector both say it through that one place");
        string links = ModSource(@"Wiring\CareersLinks.cs");
        Check(links.Contains("career.BoardPending(e, out kind)") && links.Contains("\"reallife_career_why_board_late\"") && links.Contains("\"reallife_career_why_no_board\""),
            "late board words: the dimmed Show on map gives its own reason for a board that is late and the old one for a kind the city lacks");
        string director = ModSource(@"Jobs\CareerDirector.cs");
        string asks = MemberText(director, "public bool BoardPending(int employer, out string kind)");
        Check(asks.Contains("DepotOf(employer) >= 0 && DepotOf(employer) < boards.Count") && asks.Contains("EmployerPlace.Board") && asks.Contains("boards.Pending("),
            "late board words: an employer that has an office, or hires without a board, is never said to be waiting");

        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        bool texts = en != null;
        foreach (string key in new[] { "reallife_career_boards_late", "reallife_career_boards_soon", "reallife_career_why_board_late" })
        {
            string v;
            texts &= en != null && en.TryGetValue(key, out v) && !string.IsNullOrWhiteSpace(v) && v.IndexOf('!') < 0 && v.IndexOf(';') < 0 && v.IndexOf('–') < 0 && v.IndexOf('—') < 0
                     && v.IndexOf(" - ", StringComparison.Ordinal) < 0 && v.Length <= 160;
        }
        Check(texts && en["reallife_career_boards_late"].Contains("{employer}") && en["reallife_career_boards_late"].Contains("{kind}") && en["reallife_career_boards_soon"].Contains("{employer}"),
            "late board words: the three new texts are in English with their fields, no dash, semicolon or exclamation mark, each short enough for the page");
        Check(en != null && en.ContainsKey("reallife_career_boards_phone") && en["reallife_career_boards_phone"].Contains("has no board for it"),
            "late board words: the words for a kind the city truly lacks are as they were");
        Check(en != null && WhatsNewHas(en, "Fixed: the Career app now knows where each employer's help-wanted board is"),
            "late board words: What's new says both fixes");
    }

    // Fault 3: the employer the button acts on is always in sight, and the rest of the detail scrolls where it does not fit.
    private static void DetailKeepsTheSelector()
    {
        string[] names = { "Brightside Contract Cleaning", "Hudson Brick and Build", "Vale and Hudson Architects", "Fairway Markets", "Uncle Lou's Street Eats", "Courier", "Cab" };
        foreach (float height in new[] { 1080f, 720f })
        {
            float panelH = height - 190f;   // the room under the title and the tool row of a full menu at 1920 by 1080 and at 1280 by 720
            foreach (int posts in new[] { 0, 5, 7 })
            {
                var scroll = new WorkScroll();
                WorkRow row = ProfessionRow(posts, names);
                var amounts = new[] { new WorkAmount { Id = 10, Value = 0, Min = 0, Max = names.Length - 1, Words = names, Figure = names[0] } };
                var scene = new UiScene();
                scene.Begin("profession", 1920f, height, true);
                float foot = WorkPanel.Lay(scene, UiAt.Screen, row, DetailActs(), 1400f, 100f, 440f, panelH, UiDefaultKeys.Keyboard, amounts, null, scroll);
                int pick = scene.FindNode(WorkCmd.Pick, 0), map = scene.FindNode(Map33Command, 0);
                bool selector = pick >= 0 && scene.Nodes[pick].Clip < 0 && scene.Nodes[pick].Y >= 100f && scene.Nodes[pick].Y + scene.Nodes[pick].H <= 100f + panelH + 0.5f;
                bool button = map >= 0 && scene.Nodes[map].Clip < 0 && scene.Nodes[map].Y + scene.Nodes[map].H <= 100f + panelH + 0.5f;
                Check(selector && button && foot <= 100f + panelH + 0.5f,
                    "detail keeps the selector: at " + height + " px high a profession with " + posts + " posts open shows its employer selector and Show on map inside the panel");
                Console.WriteLine("    careers33: " + height + " px high, " + posts + " posts open: " + (scroll.Clip >= 0 ? "the sections scroll (" + scroll.View + " px in view of " + scroll.Content + ")" : "everything fits whole")
                                  + ", selector at y " + scene.Nodes[pick].Y + " of a panel ending at " + (100f + panelH));
                if (scroll.Clip >= 0)
                {
                    float view = scroll.View, content = scroll.Content;
                    Check(content > view && scene.Clips[scroll.Clip].ContentHeight == content && scene.Clips[scroll.Clip].H == view && scene.Clips[scroll.Clip].Y + view <= scene.Nodes[pick].Y + 0.5f,
                        "detail keeps the selector: at " + height + " px high with " + posts + " posts the sections scroll above the selector, in a region as high as the room");
                }
            }
        }

        // One tall panel holds everything, so nothing scrolls and nothing is left out.
        var tall = new WorkScroll();
        var every = new UiScene();
        every.Begin("profession", 1920f, 2000f, true);
        WorkPanel.Lay(every, UiAt.Screen, ProfessionRow(3, names), DetailActs(), 1400f, 100f, 440f, 1800f, UiDefaultKeys.Keyboard,
            new[] { new WorkAmount { Id = 10, Words = names, Figure = names[0] } }, null, tall);
        Check(tall.Clip < 0 && Shown33(every, "HIRING NOW") + Shown33(every, "Hiring now") > 0 && Shown33(every, "THE LADDER") + Shown33(every, "The ladder") > 0,
            "detail keeps the selector: a panel with room for everything lays every section whole and does not scroll");

        // The offset the view keeps is clamped to the content, and the wheel over the panel scrolls it.
        var far = new WorkScroll { Offset = 100000f };
        var hid = new UiScene();
        hid.Begin("profession", 1280f, 720f, true);
        WorkPanel.Lay(hid, UiAt.Screen, ProfessionRow(7, names), DetailActs(), 800f, 100f, 400f, 520f, UiDefaultKeys.Keyboard,
            new[] { new WorkAmount { Id = 10, Words = names, Figure = names[0] } }, null, far);
        Check(far.Clip >= 0 && Math.Abs(far.Offset - (far.Content - far.View)) < 0.5f && hid.Clips[far.Clip].Offset == far.Offset,
            "detail keeps the selector: an offset past the end is brought back to the end of the content");
        string screen = ModSource(@"Ui\Work\WorkScreen.cs");
        string wheel = MemberText(screen, "public void Wheel(int clip, float notches)");
        Check(wheel.Contains("detail.Clip >= 0 && clip == detail.Clip") && wheel.Contains("_view.DetailOffset") && screen.Contains("_view.DetailOffset = _page.Scroll.Offset;"),
            "detail keeps the selector: the wheel over the detail scrolls the detail and the clamped offset is kept in the view");
        string view2 = ModSource(@"Ui\Work\WorkPage.cs");
        Check(view2.Contains("DetailOffset = 0f;   // another row's detail starts at its top"),
            "detail keeps the selector: choosing another profession starts its detail at the top");
        string boards = ModSource(@"Ui\Work\CareerLive.Boards.cs");
        Check(Occurs(boards, "Pinned = true") == 2, "detail keeps the selector: the employer selector and its note are the pinned sections of every profession");
    }

    private const int Map33Command = 145;

    private static UiActionModel[] DetailActs()
    {
        return new[]
        {
            new UiActionModel { Kind = UiButtonKind.Primary, Label = "Vacancies", Icon = UiIcon.Search, Command = 99, Arg = 0 },
            new UiActionModel { Kind = UiButtonKind.Secondary, Label = "See the courses", Icon = UiIcon.Books, Command = 98, Arg = 0 },
            new UiActionModel { Kind = UiButtonKind.Secondary, Label = "Show on map", Icon = UiIcon.Locate, Command = Map33Command, Arg = 0 }
        };
    }

    // A profession's detail as Career builds it: the ladder, what it takes to start, the posts open now, and the pinned employer selector with its note.
    private static WorkRow ProfessionRow(int posts, string[] names)
    {
        var row = new WorkRow { Id = 1, Name = "Software development", Sub = "First post: Junior developer", Chip = "Open to you" };
        var ladder = new WorkSection { Part = WorkPart.Pairs, Title = "The ladder" };
        foreach (string rung in new[] { "Junior developer", "Developer", "Senior developer", "Lead developer" })
        {
            ladder.Pairs.Add(new KeyValuePair<string, string>(rung, "$40.00"));
        }
        row.Sections.Add(ladder);
        var needs = new WorkSection { Part = WorkPart.Checks, Title = "To start" };
        needs.Checks.Add(new WorkNeed { Words = "A degree in computing", Met = true });
        row.Sections.Add(needs);
        var hiring = new WorkSection { Part = WorkPart.People, Title = "Hiring now", Note = posts.ToString() };
        for (int i = 0; i < Math.Min(posts, 6); i++)
        {
            hiring.People.Add(new WorkPerson { Name = names[i % names.Length], Role = "Developer", Right = "$42.00", Icon = UiIcon.Store });
        }
        if (posts > 6)
        {
            hiring.People.Add(new WorkPerson { Name = "and 1 more", Role = "All of them are on the Jobs tab.", Icon = UiIcon.Search });
        }
        row.Sections.Add(hiring);
        row.Sections.Add(new WorkSection { Part = WorkPart.Amount, Title = "Employers at the help-wanted boards", Stepper = 0, Pinned = true });
        row.Sections.Add(new WorkSection { Part = WorkPart.Text, Pinned = true, Text = names[0] + ": its board is on the pavement outside Fairway Markets, at 12 Hudson Street." });
        return row;
    }

    private static int Shown33(UiScene s, string text)
    {
        int n = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            n += s.Items[i].Text == text ? 1 : 0;
        }
        return n;
    }

    // Fault 4: a long employer name stands between the chevrons, whole, and never under one.
    private static void LongNamesBetweenTheArrows()
    {
        foreach (string name in new[] { "Brightside Contract Cleaning", "Hudson Brick and Build", "Vale and Hudson Architects", "Uncle Lou's Street Eats", "Cab" })
        {
            foreach (float width in new[] { 368f, 416f })
            {
                var s = new UiScene();
                s.Begin("selector", 1920f, 1080f, true);
                UiDraw.Selector(s, UiAt.Screen, 100f, 100f, width, string.Empty, name, WorkCmd.Pick, 0);
                int words = -1;
                float leftEdge = float.MinValue, rightEdge = float.MaxValue;
                for (int i = 0; i < s.ItemCount; i++)
                {
                    if (s.Items[i].Text == name && s.Items[i].Kind == UiKind.Text)
                    {
                        words = i;
                    }
                    else if (s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.Back)
                    {
                        leftEdge = s.Items[i].X + s.Items[i].W;
                    }
                    else if (s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.Next)
                    {
                        rightEdge = s.Items[i].X;
                    }
                }
                bool between = words >= 0 && s.Items[words].X >= leftEdge - 0.01f && s.Items[words].X + s.Items[words].W <= rightEdge + 0.01f
                               && UiTextRules.Width(name, UiStyle.SmallStrong) <= s.Items[words].W + 0.01f && (s.Items[words].Flags & UiFlags.Truncate) == 0;
                Check(between, "long names: '" + name + "' stands whole between the selector's chevrons in a row " + width + " px wide");
            }
        }
        // A label keeps its room: the pill grows only into what the label leaves.
        var labelled = new UiScene();
        labelled.Begin("selector", 1920f, 1080f, true);
        UiDraw.Selector(labelled, UiAt.Screen, 100f, 100f, 400f, "Employer", "Brightside Contract Cleaning", WorkCmd.Pick, 0);
        float labelEnd = 0f, pillStart = float.MaxValue;
        for (int i = 0; i < labelled.ItemCount; i++)
        {
            if (labelled.Items[i].Text == "Employer")
            {
                labelEnd = labelled.Items[i].X + UiTextRules.Width("Employer", UiStyle.Body);
            }
            else if (labelled.Items[i].Kind == UiKind.Icon && labelled.Items[i].Icon == UiIcon.Back)
            {
                pillStart = labelled.Items[i].X;
            }
        }
        Check(labelEnd < pillStart, "long names: with a label the pill grows only into what the label leaves, so a label is never run over");
        // Past the whole row a name ends in an ellipsis rather than under a chevron, and two employers do not read alike short of that.
        var narrow = new UiScene();
        narrow.Begin("selector", 1920f, 1080f, true);
        UiDraw.Selector(narrow, UiAt.Screen, 100f, 100f, 150f, string.Empty, "Brightside Contract Cleaning", WorkCmd.Pick, 0);
        int cut = -1;
        for (int i = 0; i < narrow.ItemCount; i++)
        {
            cut = narrow.Items[i].Kind == UiKind.Text && narrow.Items[i].Text != null && narrow.Items[i].Text.StartsWith("Brights", StringComparison.Ordinal) ? i : cut;
        }
        Check(cut >= 0 && (narrow.Items[cut].Flags & UiFlags.Truncate) != 0, "long names: wider than the whole row a name is cut with an ellipsis by the renderer, not drawn over the chevrons");
    }
}
