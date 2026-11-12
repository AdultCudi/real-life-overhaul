using System;
using System.Collections.Generic;
using System.Reflection;
using RealLifeOverhaul.Ui;

// Starting a business on the kit's BizMan page from a place just rented and still empty, for every kind of business a player can
// start: the page is laid out for each, Start business is there to press, and the press reaches the game's own SetUpBusiness, since
// every action of a page has its own case in the screen's dispatch and nothing answers before the cases. In 1.0.0 and 1.0.1 a range
// asked first took Start business for a button of the Interior tab, and the press came to nothing but the error sound, for every
// kind. Then the open switch on the tabs whose header the game switches off, and the line a page says when a press comes to nothing.
internal static partial class Program
{
    private static void FoundBizAll()
    {
        FoundBizRouting();
        FoundBizPages();
        FoundBizSwitch();
        FoundBizSaid();
        FoundBizTexts();
    }

    // The dispatch of 1.0.0 and 1.0.1 as it stood, for the check to refuse: Start business was 9810 then and the Interior tab's
    // buttons 100 and up, so the range took it before its own case.
    private const string FoundBizOldDo =
        "        private bool Do(BizManBusiness b, int what)\n"
        + "        {\n"
        + "            if (what >= BizManPages.DoLine)\n"
        + "            {\n"
        + "                return InteriorDo(what - BizManPages.DoLine);\n"
        + "            }\n"
        + "            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);\n"
        + "            switch (what)\n"
        + "            {\n"
        + "                case BizManPages.DoStartHere: return SetUpBusiness(b);\n"
        + "            }\n"
        + "        }\n";

    // Why an action a page offers would not reach its own case in the dispatch as written, or null when every one does: the
    // dispatch switches on the action, each action named has its case, and nothing answers before the cases.
    private static string FoundBizSwallowed(string dispatch, List<string> actions)
    {
        int cases = dispatch.IndexOf("switch (what)", StringComparison.Ordinal);
        if (cases < 0)
        {
            return "no switch on the action";
        }
        if (dispatch.Substring(0, cases).Contains("return"))
        {
            return "something answers before the cases";
        }
        foreach (string name in actions)
        {
            if (dispatch.IndexOf("case BizManPages." + name + ":", cases, StringComparison.Ordinal) < 0)
            {
                return name + " has no case";
            }
        }
        return null;
    }

    private static void FoundBizRouting()
    {
        var names = new List<string>();
        var values = new HashSet<int>();
        bool distinct = true;
        foreach (FieldInfo f in typeof(BizManPages).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.IsLiteral && f.FieldType == typeof(int) && f.Name.StartsWith("Do", StringComparison.Ordinal))
            {
                names.Add(f.Name);
                distinct &= values.Add((int)f.GetRawConstantValue());
            }
        }
        bool listed = BizManPages.Actions.Length == names.Count;
        foreach (int action in BizManPages.Actions)
        {
            listed &= values.Contains(action);
        }
        Check(distinct && listed && names.Count >= 26 && names.Contains("DoStartHere"),
            "found biz: every action of a page has a number of its own, and the list of them is whole (" + names.Count + " actions)");

        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs"), start = ModSource(@"Ui\BizMan\BizManScreen.Start.cs");
        string pages = ModSource(@"Ui\BizManPages.cs"), interior = ModSource(@"Ui\BizMan\BizManScreen.Interior.cs");
        string dispatch = MemberText(press, "private bool Do(BizManBusiness b, int what)");
        string why = dispatch.Length == 0 ? "the dispatch is not there" : FoundBizSwallowed(dispatch, names);
        Check(why == null, "found biz: every action a page offers reaches its own case in the screen's dispatch, and nothing answers before the cases (" + (why ?? "all") + ")");
        Check(FoundBizSwallowed(FoundBizOldDo, new List<string> { "DoStartHere" }) == "something answers before the cases",
            "found biz: the check refuses the dispatch of 1.0.1, where a range asked before the cases took Start business for a button of the Interior tab");
        string page = MemberText(press, "private bool PressPage(int command, int arg)");
        Check(page.Contains("case BizManPages.Action:") && page.Contains("return Do(b, command == BizManPages.Action ? arg : DoOfHead(arg));")
              && page.Contains("case BizManPages.LineAction:") && page.Contains("return InteriorDo(arg);"),
            "found biz: a page's action goes to the dispatch and a line page's button to its own handler, each by its own command");
        Check(!press.Contains("DoLine") && !pages.Contains("DoLine") && !interior.Contains("DoLine") && interior.Contains("Command = BizManPages.LineAction, Arg = _acts.Count")
              && Array.IndexOf(BizManPages.Commands, BizManPages.LineAction) >= 0 && Array.IndexOf(BizManPages.Commands, BizManPages.LineAction) == Array.LastIndexOf(BizManPages.Commands, BizManPages.LineAction),
            "found biz: the Interior and Loyalty tabs' buttons have a command of their own, so no action's number is shared with them");
        // One number is shared by design: a question's own button over the page has the sidebar's, and the screen asks for the question
        // first while one stands (PressPage). No other two commands are one number, the line pages' buttons among them.
        var commands = new HashSet<int>();
        int twice = 0;
        foreach (int command in BizManPages.Commands)
        {
            twice += commands.Add(command) ? 0 : 1;
        }
        int question = page.IndexOf("if (_m.Ops.Card != null && (command == UiCard.ButtonCommand || command == UiCard.CheckCommand))", StringComparison.Ordinal);
        Check(twice == 1 && BizManPages.Section == UiCard.ButtonCommand && question >= 0 && question < page.IndexOf("switch (command)", StringComparison.Ordinal),
            "found biz: no two commands of BizMan's pages are one number, but for a question's button over the page, which has the sidebar's and is asked for first while one stands ("
            + twice + " shared)");
        string setUp = MemberText(start, "private bool SetUpBusiness(BizManBusiness b)");
        Check(dispatch.Contains("case BizManPages.DoStartHere: return SetUpBusiness(b);") && setUp.Contains("Click(Calling(start, \"SetUpBusiness\"), \"set up the business\")")
              && setUp.Contains("Outcome(\"start a business\", b, reg != null && !string.IsNullOrEmpty(reg.BusinessName), heard);"),
            "found biz: Start business presses the game's own button wired to SetUpBusiness, and says in the log when nothing came of it");
        Check(dispatch.Contains("Click(Calling(pres, \"RentBuilding\"), \"rent the building\")") && dispatch.Contains("Outcome(\"rent\", b, b.buildingRegistration != null && b.buildingRegistration.RentedByPlayer, heard);"),
            "found biz: Rent building presses the game's own button, and says in the log when the place is not rented after it");
    }

    // Why Start business could not be pressed on the page laid out, or null when it can: there, enabled, a drawn face inside the
    // window, the one target of its action, pressed by the main action's key, with every target of the page reached by the pad and
    // every action on the page one the dispatch knows.
    private static string FoundBizTarget(UiScene s, UiFrame frame)
    {
        int n = s.FindNode(BizManPages.Action, BizManPages.DoStartHere);
        if (n < 0)
        {
            return "no Start business on the page";
        }
        UiNode node = s.Nodes[n];
        if (node.Disabled)
        {
            return "Start business is disabled";
        }
        frame.Window(out float wx, out float wy, out float ww, out float wh);
        if (node.Clip >= 0 || node.W < UiTokens.Control || node.H < UiTokens.Control || node.X < wx || node.Y < wy || node.X + node.W > wx + ww || node.Y + node.H > wy + wh)
        {
            return "Start business is not whole inside the window";
        }
        if (node.Face < 0 || s.Items[node.Face].Kind != UiKind.Box)
        {
            return "Start business has no drawn face";
        }
        if (UiFocusRules.Keyed(s, UiAction.Alt) != n)
        {
            return "the main action's key does not press Start business";
        }
        int starts = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            UiNode other = s.Nodes[i];
            starts += other.Command == BizManPages.Action && other.Arg == BizManPages.DoStartHere ? 1 : 0;
            if (other.Command == BizManPages.Action && Array.IndexOf(BizManPages.Actions, other.Arg) < 0)
            {
                return "an action the dispatch does not know (" + other.Arg + ")";
            }
            if (other.Command == BizManPages.LineAction)
            {
                return "a line page's button on the start page";
            }
        }
        if (starts != 1)
        {
            return starts + " targets for Start business";
        }
        return UiFocusRules.Unreached(s, out string missed) == 0 ? null : "the pad does not reach " + missed;
    }

    private static void FoundBizPages()
    {
        int kinds = UiSamples.FoundKinds.Length;
        Check(kinds == 24 && UiSamples.FoundNeeds.Length == kinds && UiSamples.FoundDiploma.Length == kinds,
            "found biz: the page is laid out for each of the 24 kinds of business the game lets a player start");
        var s = new UiScene();
        foreach (string lang in new[] { "en", "de", "ru", "zh-cn" })
        {
            UiWords words = UiWords(lang);
            foreach (float aspect in new[] { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f })
            {
                UiFrame frame = UiFrame.FullMenu(aspect, "BizMan");
                foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
                {
                    int good = 0;
                    string first = "all";
                    for (int k = 0; k < kinds; k++)
                    {
                        BizManModel m = UiSamples.BizManFound(words, k, true);
                        BizManPages.Lay(s, m, frame, true, keys);
                        string why = m.StartKinds.Selected < 0 || m.StartDetail == null ? "the kind is not chosen on the page" : FoundBizTarget(s, frame);
                        good += why == null ? 1 : 0;
                        first = why != null && first == "all" ? UiSamples.FoundKinds[k] + ": " + why : first;
                    }
                    Check(good == kinds, "found biz: from a place just rented and empty, Start business is there to press for every kind of business, by the mouse, the pad and its key, in "
                                         + lang + " at " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " with a pad" : "") + " (" + first + ")");
                }
            }
        }
        // With no kind chosen and no name typed the page still offers Start business: the game answers that press with its own notice,
        // which the page's foot then says.
        UiFrame wide = UiFrame.FullMenu(16f / 9f, "BizMan");
        BizManPages.Lay(s, UiSamples.BizManFound(UiWords("en"), -1, false), wide, true, UiDefaultKeys.Keyboard);
        Check(FoundBizTarget(s, wide) == null, "found biz: with no kind chosen and no name the page still offers Start business, for the game to answer (" + (FoundBizTarget(s, wide) ?? "there") + ")");
        // The page says under what a kind needs to open that it is fitted out afterwards: a list of furniture read as what starting it needs.
        BizManModel gym = UiSamples.BizManFound(UiWords("en"), Array.IndexOf(UiSamples.FoundKinds, "gym"), true);
        int needs = 0, note = -1, heading = -1;
        for (int i = 0; i < gym.StartDetail.Rows.Length; i++)
        {
            BizManStartRow row = gym.StartDetail.Rows[i];
            heading = row.Kind == BizManStartKind.Heading && row.Label == LocaleFile("en")["reallife_ui_bizman_start_needs"] ? i : heading;
            needs += heading >= 0 && note < 0 && row.Kind == BizManStartKind.Item ? 1 : 0;
            note = heading >= 0 && note < 0 && row.Kind == BizManStartKind.Note ? i : note;
        }
        Check(needs == 5 && note == heading + 6 && gym.StartDetail.Rows[note].Label == LocaleFile("en")["reallife_ui_bizman_start_note"],
            "found biz: a gym's five requirements to open are followed by the line that it is fitted out after it is started");
        string read = ModSource(@"Ui\BizMan\BizManScreen.Start.cs");
        Check(MemberText(read, "private void Needs(BizManBusiness b, BusinessType bt)").Contains("Kind = BizManStartKind.Note, Label = UiText.T(\"reallife_ui_bizman_start_note\")"),
            "found biz: the screen writes that line under the game's requirements");
    }

    private static void FoundBizSwitch()
    {
        // The game shows the toggle: a click. Only its header is switched off, by the game's own tab (the building's and the real estate's): set all the same.
        Check(BizManRules.SwitchWay(true, true, true, true, true, false) == BizManSwitchWay.Click && BizManRules.SwitchWay(true, true, true, true, true, true) == BizManSwitchWay.Click
              && BizManRules.SwitchWay(true, true, true, false, true, true) == BizManSwitchWay.Under,
            "found biz switch: the open switch is set as a click where the game shows its toggle, and all the same where only the header it stands in is switched off by the game's own tab");
        // The game gives the place no switch (a headquarters, a warehouse): its row or the toggle itself is off, or it is not there.
        Check(BizManRules.SwitchWay(false, true, true, true, true, true) == BizManSwitchWay.None && BizManRules.SwitchWay(true, false, true, false, true, true) == BizManSwitchWay.None
              && BizManRules.SwitchWay(true, true, false, false, true, true) == BizManSwitchWay.None && BizManRules.SwitchWay(true, false, false, false, false, true) == BizManSwitchWay.None,
            "found biz switch: a place the game gives no switch has none on the kit, whatever tab shows");
        // A toggle the game holds (Going Public's lock) is never set, shown or under its header.
        Check(BizManRules.SwitchWay(true, true, true, true, false, false) == BizManSwitchWay.Held && BizManRules.SwitchWay(true, true, true, false, false, true) == BizManSwitchWay.Held,
            "found biz switch: a toggle the game holds is never set");
        // A toggle out of sight for any other reason than the game's own tab is no switch: in a Going Public session what hides a control may be
        // the session's lock, and another player's business stands there as rented by the player.
        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs"), page = ModSource(@"Ui\BizMan\BizManScreen.Page.cs"), widgets = ModSource(@"Ui\Host\UiWidgets.cs");
        string way = MemberText(press, "private static BizManSwitchWay SwitchOf(BizManBusiness b, Toggle closed)");
        Check(BizManRules.SwitchWay(true, true, true, false, true, false) == BizManSwitchWay.None && BizManRules.SwitchWay(true, true, true, false, false, false) == BizManSwitchWay.None
              && way.Contains("bool byTab = !GoingPublic.InSession && (tab == BizManRules.Presentation || tab == BizManRules.RealEstate);")
              && way.Contains("string tab = GameContract.BizTab.Field?.GetValue(b) as string;"),
            "found biz switch: a toggle out of sight is set only where the game's own tab hides its header, and never in a Going Public session");
        string set = MemberText(press, "private bool SetOpen(BizManBusiness b)");
        Check(set.Contains("way == BizManSwitchWay.Click ? UiWidgets.Set(closed, close) : UiWidgets.SetUnder(closed, close)") && set.Contains("bool close = !reg.temporarilyClosed;")
              && set.Contains("Opened(b, reg);") && MemberText(widgets, "public static bool SetUnder(Toggle t, bool on)").Contains("t.isOn = on;")
              && MemberText(widgets, "public static bool SetUnder(Toggle t, bool on)").Contains("!t.gameObject.activeSelf || !t.interactable"),
            "found biz switch: the screen sets the game's own toggle either way, by what the business is, so the game's listener runs the opening or closing");
        Check(page.Contains("h.HasSwitch = own && SwitchOf(b, closed) != BizManSwitchWay.None;") && page.Contains("h.On = !reg.temporarilyClosed;")
              && page.Contains("b.buildingRegistration.temporarilyClosed ? 4 : 0"),
            "found biz switch: the head shows the switch wherever the press can set it, in the state the business is in, and is laid again when that changes");
        // A building a feature of the mod's holds has no lease and no offer on its page (the game's own are kept hidden there): the head says
        // whose it is, where it used to say "For rent" by the building's record alone, over a page with no way to rent.
        string chips = MemberText(page, "private static void Chips(BizManBusiness b, UiFirmHeadModel h)");
        Check(chips.Contains("string taken = SpecialBuildings.TakenBy(reg);") && chips.Contains("TakenPageRules.Line(SpecialBuildings.PageOf(reg, out _), UiText.T(\"reallife_claimed_taken\")")
              && chips.Contains("else if (reg.AvailableForRent && !reg.RentedByPlayer)") && chips.Contains("if (taken == null && !reg.BuildingOwnedByPlayer"),
            "found biz switch: a building a feature of the mod's holds says whose it is on its page, never For rent or For sale over a page that offers neither");
        // What an opening is worth, for the log: nothing in the way says nothing; each thing in the way is named.
        Check(BizManRules.NotTrading(5, 0, null, true, true, false) == null && BizManRules.NotTrading(0, 0, null, true, true, false) == null,
            "found biz switch: a business with every requirement met, hours today and its staff has nothing to say at its opening");
        string empty = BizManRules.NotTrading(5, 5, "ba:businessrequirement_workoutmachine", true, true, true);
        Check(empty != null && empty.Contains("5 of the game's 5 requirement(s) missing") && empty.Contains("ba:businessrequirement_workoutmachine") && empty.Contains("nobody at it")
              && BizManRules.NotTrading(3, 0, null, false, false, false).Contains("no day of its week has opening hours")
              && BizManRules.NotTrading(3, 0, null, true, false, false).Contains("no opening hours today") && !BizManRules.NotTrading(3, 0, null, true, false, false).Contains("requirement"),
            "found biz switch: an opening that will not trade yet names what stands in its way: the requirements missing, the hours, the station with nobody at it");
        string utilities = ModSource(@"Utilities\UtilitiesDirector.cs");
        string effects = MemberText(utilities, "private void Effects()");
        Check(effects.Contains("bool wasOpen = !UtilitiesGame.Closed(place.Reg);") && effects.Contains("if (wasOpen)") && effects.Contains("_toldClosed.Remove(place.Place);")
              && MemberText(utilities, "public string LookAt(BuildingRegistration reg)").Contains("Effects();")
              && MemberText(press, "private void Opened(BizManBusiness b, BuildingRegistration reg)").Contains("UtilitiesDirector.Instance.LookAt(reg)"),
            "found biz switch: a business the utilities close again after it was opened by hand is told why each time, and at once when it was opened on the kit's page");
    }

    private static void FoundBizSaid()
    {
        var s = new UiScene();
        Dictionary<string, string> en = LocaleFile("en");
        foreach (string lang in new[] { "en", "de", "ru", "zh-cn" })
        {
            UiWords words = UiWords(lang);
            foreach (float aspect in new[] { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f })
            {
                UiFrame frame = UiFrame.FullMenu(aspect, "BizMan");
                frame.Window(out float wx, out float wy, out float ww, out float wh);
                foreach (string state in new[] { "start_refused", "store_refused" })
                {
                    BizManModel m = UiSamples.BizMan(words, state);
                    BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
                    int line = -1;
                    for (int i = 0; i < s.ItemCount; i++)
                    {
                        line = s.Items[i].Kind == UiKind.Text && s.Items[i].Text == m.Refused ? i : line;
                    }
                    bool whole = line >= 0 && (s.Items[line].Flags & UiFlags.Wrap) != 0 && s.Items[line].Y >= wy && s.Items[line].Y + s.Items[line].H <= wy + wh
                                 && s.Items[line].X >= wx && s.Items[line].X + s.Items[line].W <= wx + ww
                                 && UiTextRules.WrappedHeight(m.Refused, s.Items[line].Style, s.Items[line].W, 99) <= s.Items[line].H + 0.5f;
                    // Nothing of the page stands under the line: every target over its words ends above it (the sidebar and the bar stand beside and under it).
                    bool clear = line >= 0;
                    for (int i = 0; clear && i < s.NodeCount; i++)
                    {
                        UiNode n = s.Nodes[i];
                        UiItem said = s.Items[line];
                        clear = n.Clip >= 0 || n.Layer > 0 || n.X + n.W <= said.X || n.X >= said.X + said.W || n.Y + n.H <= said.Y + 0.5f || n.Y >= said.Y + said.H;
                    }
                    Check(whole && clear, "found biz said: what the last press was told stands whole at the page's foot, inside the window, with no target under it: " + state + " in " + lang + " at "
                                          + aspect.ToString("0.00"));
                    if (state == "start_refused")
                    {
                        Check(FoundBizTarget(s, frame) == null, "found biz said: Start business stays there to press above that line in " + lang + " at " + aspect.ToString("0.00")
                                                                + " (" + (FoundBizTarget(s, frame) ?? "there") + ")");
                    }
                }
            }
        }
        // No line, no room taken: the page ends where it always did.
        BizManModel plain = UiSamples.BizMan(UiWords("en"), "start");
        UiFrame f = UiFrame.FullMenu(16f / 9f, "BizMan");
        BizManPages.Lay(s, plain, f, true, UiDefaultKeys.Keyboard);
        int at = s.FindNode(BizManPages.Action, BizManPages.DoStartHere);
        float y = at >= 0 ? s.Nodes[at].Y : -1f;
        plain.Refused = en.TryGetValue("reallife_ui_bizman_refused_gone", out string gone) ? gone.Replace("{action}", "Start business") : "x";
        BizManPages.Lay(s, plain, f, true, UiDefaultKeys.Keyboard);
        int after = s.FindNode(BizManPages.Action, BizManPages.DoStartHere);
        Check(at >= 0 && after >= 0 && s.Nodes[after].Y < y, "found biz said: the page makes room for the line only while there is one to say");
        // Each reason has its sentence, and each sentence names what was pressed.
        Check(BizManRules.RefusalKey(BizManRefusal.Gone) == "reallife_ui_bizman_refused_gone" && BizManRules.RefusalKey(BizManRefusal.Held) == "reallife_ui_bizman_refused_held"
              && BizManRules.RefusalKey(BizManRefusal.Fault) == "reallife_ui_bizman_refused_fault" && BizManRules.RefusalSaid(BizManRefusal.Fault).Contains("fault")
              && BizManRules.RefusalSaid(BizManRefusal.Gone) != BizManRules.RefusalSaid(BizManRefusal.Held),
            "found biz said: a press the game no longer offers, one it holds and one nothing answered each have their own sentence, for the page and for the log");
        string screen = ModSource(@"Ui\BizMan\BizManScreen.cs");
        string answered = MemberText(screen, "private void Answered(int command, int arg, bool done, int heardBefore)");
        string press = MemberText(screen, "public bool Press(int command, int arg)");
        Check(press.Contains("bool done = Run(command, arg);") && press.Contains("Answered(command, arg, done, heard);") && answered.Contains("UiText.Fill(BizManRules.RefusalKey(_why), \"action\"")
              && answered.Contains("ShellNotices.LastWords()") && answered.Contains("UiDirector.Logger?.Warn(\"BizMan check (press): \"") && screen.Contains("_m.Refused = _refused;"),
            "found biz said: every press of the screen is looked at once it is made: the game's own refusal is said in the game's words, and one that came to nothing is written once to Player.log");
        // A target that says in words what it does owes an answer when nothing answered it. One with nothing to do where it stands (a chart's
        // point, a lone page's tab, a stop for the pad) is never called a fault: its press keeps to the error cue, and the log has a plain line.
        Check(BizManRules.Promises(BizManPages.Action) && BizManRules.Promises(BizManPages.LineAction) && BizManRules.Promises(UiFirmHead.First + UiFirmHead.Action)
              && BizManRules.Promises(UiFirmHead.First + UiFirmHead.Switch) && !BizManRules.Promises(UiChart.First + UiChart.Point) && !BizManRules.Promises(BizManPages.Page)
              && !BizManRules.Promises(BizManPages.Heading) && !BizManRules.Promises(BizManPages.Section) && !BizManRules.Promises(UiDataTable.First + UiDataTable.Row),
            "found biz said: a button that says what it does owes an answer; a target with nothing to do where it stands is never called a fault");
        int promised = 0;
        foreach (int command in BizManPages.Commands)
        {
            promised += BizManRules.Promises(command) ? 1 : 0;
        }
        Check(promised == 4 && answered.Contains("bool told = byGame || BizManRules.Promises(command) || (action != null && _why != BizManRefusal.Fault);") && answered.Contains("if (told)")
              && answered.Contains("(_why == BizManRefusal.Fault ? \": FAIL.\" : \".\")") && answered.Contains("UiDirector.Logger?.Info(\"BizMan (kit): a press of \"")
              && MemberText(screen, "private string Pressed(int command, int arg)").Contains("if (command == BizManPages.OpsAction)"),
            "found biz said: the page's line is written for the game's refusal, for a page's own button whatever stopped it, and for any other button with words that the game holds or does not show; "
            + "any other press that did nothing has its line in the log alone");
        Check(MemberText(screen, "public void Focused(int command, int arg)").Contains("Run(command, arg);") && !MemberText(screen, "public void Focused(int command, int arg)").Contains("Press(command, arg);"),
            "found biz said: a tab followed as the pad comes to it is no press of the player's, and nothing is said of it");
        string shown = MemberText(ModSource(@"Ui\Shell\ShellNotices.cs"),
            "private static void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action click, bool sound, bool track)");
        int counted = shown.IndexOf("Heard++;", StringComparison.Ordinal), kept = shown.IndexOf("NoticeMute.KeptBack", StringComparison.Ordinal);
        Check(counted >= 0 && kept > counted, "found biz said: every notice the game is asked to show is counted, one kept from popping up too, so a press knows the game answered it");
    }

    private static void FoundBizTexts()
    {
        string[] keys =
        {
            "reallife_ui_bizman_refused_gone", "reallife_ui_bizman_refused_held", "reallife_ui_bizman_refused_fault", "reallife_ui_bizman_refused_press", "reallife_ui_bizman_refused_switch"
        };
        Dictionary<string, string> en = LocaleFile("en");
        bool all = true, named = true;
        foreach (string key in keys)
        {
            all &= en.ContainsKey(key) && !en[key].Contains("!");
        }
        foreach (string key in new[] { keys[0], keys[1], keys[2] })
        {
            named &= en.TryGetValue(key, out string text) && text.StartsWith("{action}", StringComparison.Ordinal);
        }
        Check(all && named, "found biz texts: the page's sentences for a press that came to nothing are in English, each naming what was pressed");
        Check(en.TryGetValue("reallife_ui_bizman_refused_fault", out string fault) && en.TryGetValue("reallife_opt_sec_uiscreens_business", out string section) && fault.Contains(section)
              && en.TryGetValue("reallife_set_app", out string app) && fault.Contains(app) && fault.Contains("Player.log"),
            "found biz texts: the sentence for a fault of the page says where its switch is, by the section's own name, and asks for the log");
        // A language that has them keeps what they fill in; one that does not yet reads them in English.
        foreach (string code in new[] { "pt", "zh-cn", "de", "es", "fr", "ko", "nl", "pl", "ru", "tr" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            bool kept = true;
            foreach (string key in new[] { keys[0], keys[1], keys[2] })
            {
                kept &= !texts.TryGetValue(key, out string text) || text.Contains("{action}");
            }
            Check(kept, "found biz texts: the sentences keep what was pressed in " + code + ", where it has them");
        }
        Check(WhatsNewHas(en, "a button that cannot do what it says now tells you why at the foot of the page"), "found biz texts: What's new says a press that comes to nothing is told on the page");
    }
}
