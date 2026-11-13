using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The cards over the world's items on the kit: every case laid out in every language, frame and input as any screen, and the rules
// the job made: every drawn kind says why, a card's every target is answered by one handler of its own (no dead button), its commands
// are its own block and no other part's, a card is laid out again only when what it says changed, a long drop-down is shown a page at
// a time, the numbers behind the words (mood, stock, margin, shift, ingredients) are the game's own, the world host reaches the cards,
// and nothing of a card's frame searches or queries the game.
internal static partial class Program
{
    private static bool _overlaysChecked;

    private static readonly int[] OverlayCommands =
    {
        UiOverlayCard.ActionCommand, UiOverlayCard.ChoiceCommand, UiOverlayCard.OptionCommand, UiOverlayCard.PageCommand, UiOverlayCard.StepCommand
    };

    static partial void UiCasesOverlays(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "world_overlay_hover":
                foreach (UiSamples.OverlayCase which in UiSamples.OverlayHover)
                {
                    UiSamples.OverlayCase w = which;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + w + ")", Title = "HUD", Commands = HudCommands.Concat(OverlayCommands).ToArray(),
                        Lay = (s, f, d, k, t) => UiSamples.OverlayScene(s, f, d, k, t, UiSamples.OverlayModel(t, k, w), 0.5f, 0.55f) });
                }
                if (!_overlaysChecked)
                {
                    _overlaysChecked = true;
                    OverlaysUiChecks();
                }
                break;
            case "world_overlay_click":
                foreach (UiSamples.OverlayCase which in UiSamples.OverlayClick)
                {
                    UiSamples.OverlayCase w = which;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + w + ")", Title = "HUD", Commands = HudCommands.Concat(OverlayCommands).ToArray(),
                        Lay = (s, f, d, k, t) => UiSamples.OverlayScene(s, f, d, k, t, UiSamples.OverlayModel(t, k, w), 0.5f, 0.55f) });
                }
                break;
            case "world_overlay_person":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a person on shift)", Title = "HUD", Commands = HudCommands.Concat(new[] { UiInfoPanel.FooterCommand, UiInfoPanel.LinkCommand }).ToArray(),
                    Lay = (s, f, d, k, t) => UiSamples.OverlayPersonScene(s, f, d, k, t) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (followed)", Title = "HUD", Commands = HudCommands.Concat(new[] { UiInfoPanel.FooterCommand, UiInfoPanel.LinkCommand }).ToArray(),
                    Lay = (s, f, d, k, t) => UiSamples.OverlayPersonScene(s, f, d, k, t, true) });
                break;
        }
    }

    private static string OverlayEnglish(string key, string english) => english;

    private static void OverlaysUiChecks()
    {
        OverlayKindChecks();
        OverlayRuleChecks();
        OverlayCardChecks();
        OverlaySourceChecks();
        OverlayBehindChecks();
    }

    // ---------------------------------------------------------------- the table of kinds

    private static void OverlayKindChecks()
    {
        var names = new HashSet<string>();
        bool why = true, unique = true;
        foreach (OverlayKind k in OverlayKinds.All)
        {
            why &= !string.IsNullOrWhiteSpace(k.Why) && k.Why.Length > 20;
            unique &= names.Add(k.Name);
        }
        Check(why, "overlays: every one of the game's overlay kinds says in plain words why it is drawn on the kit or left the game's");
        Check(unique, "overlays: each kind stands once in the table");
        string[] hover = { "StockOverlay", "EmployeeNameOverlay", "CtaOverlay", "PriceOverlay", "RequirementsOverlay", "SecurityOverlay", "InfoOverlay", "CustomerCapacitySimpleOverlay" };
        string[] click = { "ButtonOverlay", "VehicleOverlay", "StorageShelfOverlay", "JobBoardOverlay", "SellerStandOverlay", "CustomizableButtonsOverlay", "DecorativeItemHolderOverlay",
            "MachineInfoOverlay", "DropdownOverlay", "MachineOverlay", "RadioOverlay", "EmployeeOverlay", "TextInputOverlay" };
        bool all = true;
        foreach (string n in hover.Concat(click))
        {
            all &= OverlayKinds.All.Any(k => k.Name == n);
        }
        Check(all, "overlays: the nine simple kinds and the thirteen detailed kinds of the game's OverlayManager are all in the table");
        Check(OverlayKinds.All.Count(k => k.Form == OverlayForm.Hover && k.Drawn) == 9, "overlays: the nine hover kinds are all drawn on the kit");
        Check(OverlayKinds.All.Count(k => !k.Drawn && k.Form != OverlayForm.Keeper && k.Form != OverlayForm.Hint && k.Form != OverlayForm.Marker) == 1,
            "overlays: only the kind the game never shows is left out of the drawn ones among the boxes");
        Check(OverlayKinds.All.Length >= 44, "overlays: the table holds the game's whole set of overlay classes and markers (" + OverlayKinds.All.Length + ")");
    }

    // ---------------------------------------------------------------- the numbers

    private static void OverlayRuleChecks()
    {
        Check(OverlayRules.Mood(29.9f) == OverlayMood.Low && OverlayRules.Mood(30f) == OverlayMood.Middle && OverlayRules.Mood(59.9f) == OverlayMood.Middle && OverlayRules.Mood(60f) == OverlayMood.High,
            "overlays: a mood reads at the game's own thresholds, 30 and 60");
        Check(OverlayRules.MoodTone(OverlayMood.Low) == UiRole.Danger && OverlayRules.MoodTone(OverlayMood.High) == UiRole.Ok, "overlays: a mood's ring is its tone");
        Check(OverlayRules.StockShare(12, 40) == 30 && OverlayRules.StockShare(5, 0) == -1 && OverlayRules.StockShare(50, 40) == 100, "overlays: a stock's share of its room is kept inside 0 to 100, none without room");
        Check(OverlayRules.StockTone(24) == UiRole.Danger && OverlayRules.StockTone(25) == UiRole.Warn && OverlayRules.StockTone(50) == UiRole.Ok, "overlays: a stock's tone is the game's, red under a quarter, yellow under a half");
        Check(OverlayRules.Margin(4.5f, 1.38f) == 69 && OverlayRules.Margin(4.5f, 0f) == -1 && OverlayRules.Margin(0f, 1f) == -1 && OverlayRules.Margin(2f, 3f) == -50,
            "overlays: a margin is price against cost, and nothing where a cost is not known");
        var starts = new[] { 7, 11 };
        var ends = new[] { 11, 15 };
        Check(OverlayRules.OnShift(starts, ends, 2, 9, out int from, out int until) && from == 7 && until == 15, "overlays: two shifts in a row are one stretch at work: from the first, until the end of the last");
        Check(!OverlayRules.OnShift(starts, ends, 2, 15, out from, out until), "overlays: a person is off shift at the hour a shift ends");
        Check(OverlayRules.WorkedMinutes(7, 10, 15) == 195 && OverlayRules.WorkedMinutes(7, 6, 30) == 0, "overlays: the time worked is from the shift's start to the clock, never under nothing");
        OverlayRules.Split(195, out int h, out int m);
        Check(h == 3 && m == 15, "overlays: 195 minutes are 3 h 15 min");
        Check(OverlayRules.ShiftShare(7, 15, 11, 0) == 50 && OverlayRules.ShiftShare(7, 15, 20, 0) == 100, "overlays: a shift's bar fills with the clock and stops at its end");
        Check(Math.Abs(OverlayRules.Earned(34, 18f) - 612f) < 0.01f, "overlays: a week's pay is the week's hours at the wage");
        Check(OverlayRules.Percent(72.9f) == 72 && OverlayRules.SkillShare(58f) == 58, "overlays: a skill is the game's 0 to 100 and is not printed as a score of five");
        Check(OverlayRules.SeatShare(6, 16) == 37 && OverlayRules.SeatShare(0, 0) == -1, "overlays: seats taken are a share of the seats");
        Check(Math.Abs(OverlayRules.MakingCost(new[] { 1f, 1f, 1f }, new[] { 0.5f, 0.3f, 0.4f }, 3, 1) - 1.2f) < 0.001f && OverlayRules.MakingCost(new[] { 1f }, new[] { 0f }, 1, 1) < 0f,
            "overlays: what making a unit costs is its ingredients at their costs, and unknown when any cost is");
        Check(OverlayRules.PageOf(13, 6) == 2 && OverlayRules.ClampPage(9, 15, 6) == 2 && OverlayRules.ClampPage(-1, 15, 6) == 0, "overlays: a long choice is shown a page at a time, the page kept inside the pages");
        Check(OverlayRules.Step(60, 1) == 70 && OverlayRules.Step(60, -1) == 50 && OverlayRules.Step(100, 1) == 100 && OverlayRules.Step(0, -1) == 0 && OverlayRules.Step(63, 1) == 70, "overlays: the volume steps by a tenth and stays inside 0 to 100");
        Check(OverlayRules.After("Contents: Flour", "Contents") == "Flour" && OverlayRules.After("<b>Flour</b>", null) == "Flour", "overlays: the game's lead and its tags come off its words");
        var names = new string[6];
        var lacking = new bool[6];
        int n = OverlayRules.Ingredients("<color=#FFFFFF>Patty</color>, <color=#E5483B>Cheese</color>", "E5483B", names, lacking);
        Check(n == 2 && names[0] == "Patty" && !lacking[0] && names[1] == "Cheese" && lacking[1], "overlays: the ingredients the pallets lack are the ones the game colours red");
        Check(OverlayRules.Short("Maria Rossi") == "Maria R." && OverlayRules.Short("Cher") == "Cher", "overlays: a name shortens to a first name and the last initial");
        Check(OverlayRules.LeaderShows(500f, 400f, 356f, 400f - UiHudRules.Lift - 120f, 288f, 120f, UiHudRules.Lift) && !OverlayRules.LeaderShows(500f, 400f, 356f, 20f, 288f, 120f, UiHudRules.Lift),
            "overlays: the leader shows only while the card stands where its point puts it");
    }

    // ---------------------------------------------------------------- the cards

    private static void OverlayCardChecks()
    {
        var wrong = new System.Text.StringBuilder();
        int targets = 0, pressed = 0;
        foreach (UiSamples.OverlayCase which in UiSamples.OverlayHover.Concat(UiSamples.OverlayClick))
        {
            UiOverlayModel model = UiSamples.OverlayModel(OverlayEnglish, UiDefaultKeys.Keyboard, which);
            int open = which == UiSamples.OverlayCase.HoldsOpen ? 0 : -1;
            var scene = new UiScene();
            scene.Begin("overlay", UiOverlayCard.Width(model), UiOverlayCard.Height(model), true);
            UiOverlayCard.Lay(scene, UiAt.Screen, model, 0f, 0f);
            for (int i = 0; i < scene.NodeCount; i++)
            {
                targets++;
                UiNode node = scene.Nodes[i];
                OverlayAct act = OverlayRules.Route(model, node.Command, node.Arg, model.Detail && model.ChoiceCount > 0 && model.Choices[0].Open ? 0 : open);
                if (act == OverlayAct.None)
                {
                    wrong.Append(which).Append(" ").Append(node.Command).Append("/").Append(node.Arg).Append("; ");
                }
                else
                {
                    pressed++;
                }
            }
            Check(model.Detail || scene.NodeCount == 0, "overlays: the small card (" + which + ") takes no target, so the pointer passes through it to the world");
            Check(UiOverlayCard.Height(model) > UiFrame.Strip && UiOverlayCard.Height(model) < 640f, "overlays: the card (" + which + ") is no taller than the screen allows: " + UiOverlayCard.Height(model));
        }
        Check(wrong.Length == 0, "overlays: every target of every card is answered by one handler of its own, none is dead or another part's: " + wrong);
        Check(targets > 20 && pressed == targets, "overlays: every one of " + targets + " targets is routed to a game button, handler, choice, page or step");

        // A command of the card is its own: inside the block of 9620 to 9659 and no other part's.
        bool block = OverlayCommands.All(c => c >= 9620 && c <= 9659) && OverlayCommands.Distinct().Count() == OverlayCommands.Length;
        int[] others = { UiInfoPanel.TabCommand, UiInfoPanel.LinkCommand, UiInfoPanel.FooterCommand, UiAppWindow.NavCommand, UiDecision.ChoiceCommand, UiWorldPrompt.ActionCommand,
            UiPromptCard.ActionCommand, UiPicker.EntryCommand, UiDraw.CloseCommand };
        Check(block && !OverlayCommands.Intersect(others).Any(), "overlays: the card's five commands are in its own block of numbers and are none of the kit's other parts'");

        // A long choice is a page at a time: at most six options are drawn, with the pager when more.
        UiOverlayModel open15 = UiSamples.OverlayModel(OverlayEnglish, UiDefaultKeys.Keyboard, UiSamples.OverlayCase.HoldsOpen);
        var s15 = new UiScene();
        s15.Begin("overlay", UiOverlayCard.Width(open15), UiOverlayCard.Height(open15), true);
        UiOverlayCard.Lay(s15, UiAt.Screen, open15, 0f, 0f);
        int options = 0, pages = 0;
        for (int i = 0; i < s15.NodeCount; i++)
        {
            options += s15.Nodes[i].Command == UiOverlayCard.OptionCommand ? 1 : 0;
            pages += s15.Nodes[i].Command == UiOverlayCard.PageCommand ? 1 : 0;
        }
        Check(options == UiOverlayModel.PerPage && pages == 2, "overlays: an open choice of fifteen shows a page of " + options + " options with its two page buttons");
        Check(UiOverlayCard.Pages(ref open15.Choices[0]) == 3, "overlays: fifteen options are three pages");

        // The card is laid out again only when what it says changed.
        UiOverlayModel a = UiSamples.OverlayModel(OverlayEnglish, UiDefaultKeys.Keyboard, UiSamples.OverlayCase.StockLow);
        UiOverlayModel b = UiSamples.OverlayModel(OverlayEnglish, UiDefaultKeys.Keyboard, UiSamples.OverlayCase.StockLow);
        int same = a.Digest();
        Check(same == b.Digest(), "overlays: the same words digest the same, so the card is not laid out again for them");
        b.Figure = "8 of 40";
        Check(same != b.Digest(), "overlays: a changed figure changes the digest");
        b.Clear();
        Check(b.Digest() != same && b.RowCount == 0 && b.ActionCount == 0, "overlays: a cleared card digests as nothing and keeps its arrays");
        UiOverlayModel c = UiSamples.OverlayModel(OverlayEnglish, UiDefaultKeys.Keyboard, UiSamples.OverlayCase.HoldsOpen);
        int openDigest = c.Digest();
        c.Choices[0].Page = 0;
        Check(openDigest != c.Digest(), "overlays: turning an open choice's page changes what is laid out");

        // The person's panel: its sections each have their own rows and its footer is five buttons (a page, locate, route, follow, the schedule).
        UiInfoModel person = UiSamples.OverlayPerson(OverlayEnglish);
        Check(person.Sections.Length == 4 && person.Sections.All(x => x.Rows.Length > 0 && !string.IsNullOrEmpty(x.Label)), "overlays: each section of a person's panel has its words and its rows");
        Check(person.Footer.Length == 4 && !string.IsNullOrEmpty(person.FooterLabel) && person.Footer.Distinct().Count() == 4, "overlays: a person's footer buttons are each their own glyph: locate, route, follow, schedule");
        Check(UiSamples.OverlayPerson(OverlayEnglish, true).Footer[2] != person.Footer[2], "overlays: the follow button shows another glyph while the camera is on the person");
        var ps = new UiScene();
        UiInfoPanel.Lay(ps, person, UiFrame.FullMenu(1.78f, "HUD"), true, UiDefaultKeys.Keyboard);
        Check(Enumerable.Range(0, ps.NodeCount).Count(i => ps.Nodes[i].Command == UiInfoPanel.FooterCommand) == 5 && Enumerable.Range(0, ps.NodeCount).Any(i => ps.Nodes[i].Command == UiInfoPanel.LinkCommand),
            "overlays: a person's panel has its page, locate, route, follow and schedule buttons and the link to their page");
    }

    // ---------------------------------------------------------------- the code

    private static void OverlaySourceChecks()
    {
        string scripts = ModScripts();
        string folder = System.IO.Path.Combine(scripts, "Ui", "Overlays");
        string host = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldHost.cs"));
        Check(host.Contains("_overlays.Frame(") && host.Contains("_overlays.Give()") && host.Contains("EmployeeFollowLive.Tick()"), "overlays: the world's host runs the cards each frame and gives the game's boxes back with it");
        string cards = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.cs"));
        string frame = OverlayBetween(cards, "public void Frame(UiFrame frame, bool hoverOn, bool clickOn, bool personOn)", "/// <summary>The game's boxes back as they were");
        string place = OverlayBetween(cards, "private void Place(UiFrame frame)", "// The model laid out");
        string[] searches = { "FindObject", "FindAnyObject", "GetComponent", "Camera.main", "WorldToScreenPoint", "FindGameObject", "Physics.", "GetComponentsInChildren" };
        Check(frame.Length > 200 && place.Length > 100 && !searches.Any(x => frame.Contains(x) || place.Contains(x)), "overlays: a card's frame searches and queries nothing: the game's own point is read, the layout moves");
        string click = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Click.cs"));
        Check(cards.Contains("UiWidgets.Press(") && click.Contains("drop.SelectOption(") && click.Contains("UiWidgets.Slide(") && click.Contains("UiWidgets.Calling("),
            "overlays: a card's buttons, choices and volume go through the game's own button, drop-down and slider");
        string person = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Person.cs"));
        Check(person.Contains("_person.OnManageEmployeeClicked") && person.Contains("_person.OnManageScheduleClicked") && person.Contains("FocusOnBuilding") && person.Contains("SetGuiderTarget"),
            "overlays: a person's page, schedule, locate and route are the game's own calls");
        string aim = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "TutorialAimLive.cs"));
        Check(aim.Contains("Every = 0.25f") && aim.Contains("arrow.position = At[i]") && aim.Contains("SetActive(true)") && host.Contains("TutorialAimLive.Late()") && host.Contains("TutorialAimLive.Release()")
              && System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "UiPointer.cs")).Contains("Views"),
            "overlays: the tutorial's arrow is aimed at the kit's own button four times a second, kept there each frame after the game's move, and given back as found");
        bool quit = System.IO.Directory.GetFiles(folder, "*.cs").Any(f => Regex.IsMatch(System.IO.File.ReadAllText(f), @"Application\.Quit|LoadGame\(|SaveGameManager\.Load"));
        Check(!quit, "overlays: nothing of the cards quits the game or loads a save");
        string follow = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "EmployeeFollowLive.cs"));
        Check(follow.Contains("PlayerAction.Cancel.Pressed()") && follow.Contains("PlayerAction.Move.Vector()") && follow.Contains("ThirdPersonCamera.IsUiOpen()") && follow.Contains("ThirdPersonCamera.Focus = null"),
            "overlays: the follow camera ends on a movement key, Escape and any panel that needs the camera, and gives the camera back");
        string hoverRead = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Hover.cs"));
        Check(hoverRead.Contains("OverlayFactsLive.NextDelivery") && hoverRead.Contains("OverlayFactsLive.Price") && hoverRead.Contains("OverlayFactsLive.Seats") && hoverRead.Contains("OverlayFactsLive.Queue") && hoverRead.Contains("OverlayFactsLive.Machine"),
            "overlays: the small card reads the delivery, the price and margin, the seats, the line and the machine's recipe from the game");
        string locales = System.IO.Path.Combine(scripts, "..", "Locales");
        string en = System.IO.File.ReadAllText(System.IO.Path.Combine(locales, "en.json"));
        bool texts = true;
        foreach (UiScreenDef def in UiScreens.All.Where(d => d.Id.StartsWith("world_overlay", StringComparison.Ordinal)))
        {
            texts &= en.Contains("\"reallife_ui_screen_" + def.Id + "\"") && en.Contains("\"reallife_opt_tip_ui_screen_" + def.Id + "\"");
            foreach (string key in def.Keys)
            {
                texts &= en.Contains("\"" + key + "\"");
            }
        }
        Check(texts && UiScreens.All.Count(d => d.Id.StartsWith("world_overlay", StringComparison.Ordinal)) == 3 && UiScreens.All.Any(d => d.Id == UiOverlays.PointerId)
            && en.Contains("\"reallife_ui_screen_world_tutorial_pointer\"") && en.Contains("\"reallife_opt_sec_uiscreens_overlays\""),
            "overlays: the three screens are registered, each has its switch with its label and tip, and every key it names is in the English locale");
    }

    // ---------------------------------------------------------------- the game's box behind a card (a player, 13 Nov: three of the game's blue buttons stuck out from behind the computer's card)

    private static void OverlayBehindChecks()
    {
        // The rules: a held group is still hidden only while it is unseen and takes no choice; a box is looked through again when its transforms changed in number or the card is read.
        Check(UiHostRules.StillHidden(0f, false, false, false), "overlay behind: a group held hidden (unseen, no choice, no pointer) is still hidden");
        Check(!UiHostRules.StillHidden(1f, false, false, false) && !UiHostRules.StillHidden(0.5f, false, false, false),
            "overlay behind: a group the game, a refresh or another mod made seen again is no longer hidden");
        Check(!UiHostRules.StillHidden(0f, true, false, false) && !UiHostRules.StillHidden(0f, false, true, false),
            "overlay behind: a group that takes a choice or the pointer again is no longer hidden");
        Check(UiHostRules.StillHidden(0f, false, true, true) && !UiHostRules.StillHidden(0f, false, false, true),
            "overlay behind: a backdrop kept for its clicks is hidden while it still blocks, and not hidden once it stopped");
        Check(!OverlayRules.SweepDue(7, 7, false) && OverlayRules.SweepDue(7, 8, false) && OverlayRules.SweepDue(7, 6, false) && OverlayRules.SweepDue(-1, 7, false) && OverlayRules.SweepDue(7, 7, true),
            "overlay behind: the box is looked through again when its transforms changed in number, after a card was put away, and at each read, never otherwise");

        // The code: every piece of the game's box is held, wherever it came from, and the box comes back whole.
        string folder = System.IO.Path.Combine(ModScripts(), "Ui", "Overlays");
        string cards = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.cs"));
        string hide = System.IO.File.Exists(System.IO.Path.Combine(folder, "OverlayCardsLive.Hide.cs")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "OverlayCardsLive.Hide.cs")) : string.Empty;
        string conceal = ModSource(@"Ui\Host\UiConceal.cs");
        string keep = OverlayBetween(hide, "private void KeepHidden(", "// Each button and nested canvas");
        string sweep = hide.Contains("private static void Sweep(") ? hide.Substring(hide.IndexOf("private static void Sweep(", StringComparison.Ordinal)) : string.Empty;
        Check(keep.Contains("root.hierarchyCount") && keep.Contains("OverlayRules.SweepDue(") && keep.Contains("Sweep(hide, root)") && keep.Contains("hide.Reassert()")
              && keep.Contains("_form == Form.Hover") && keep.Contains("_simple.transform") && keep.Contains("_detailed.transform"),
            "overlay behind: while a card stands, the game's box (small or large) is counted each frame, looked through when it changed, and every group held is hidden again if shown");
        Check(sweep.Contains("GetComponentsInChildren(true, BoxButtons)") && sweep.Contains("hide.Add(BoxButtons[i].transform)")
              && sweep.Contains("GetComponentsInChildren(true, BoxCanvases)") && sweep.Contains("hide.Add(BoxCanvases[i].transform)") && sweep.Contains("hide.Prune()"),
            "overlay behind: every button (the game's, the mod's Work for, another mod's) and every nested canvas under the box gets a group of its own, held with the box's");
        string frame = OverlayBetween(cards, "public void Frame(UiFrame frame, bool hoverOn, bool clickOn, bool personOn)", "/// <summary>The game's boxes back as they were");
        int concealAt = frame.IndexOf("Conceal();", StringComparison.Ordinal), keepAt = frame.IndexOf("KeepHidden(read);", StringComparison.Ordinal), shownAt = frame.IndexOf("_view.Root.gameObject.SetActive(true);", StringComparison.Ordinal);
        Check(concealAt > 0 && keepAt > concealAt && shownAt > keepAt && frame.Contains("bool read = now >= _due || _relay || renaming;"),
            "overlay behind: the box is held piece by piece before the card is shown, in the frame that shows it, and at every read");
        Check(conceal.Contains("public int Reassert()") && conceal.Contains("UiHostRules.StillHidden(group.alpha, group.interactable, group.blocksRaycasts, keep)")
              && conceal.Contains("group.alpha = 0f;") && conceal.Contains("if (!_hiding)"),
            "overlay behind: a held group is written hidden again only while hiding, and only where it differs");
        string leave = OverlayBetween(cards, "private void Leave()", "// The game's box under what is drawn over it");
        string give = OverlayBetween(cards, "public void Give()", "// The card put away");
        Check(leave.Contains("_simpleHide.Show();") && leave.Contains("_detailHide.Show();") && leave.Contains("_simpleSeen = _detailSeen = -1") && give.Contains("Leave();") && give.Contains("_detailHide.Clear();"),
            "overlay behind: the game's box comes back as found when the card is put away, and the next card looks through it again");
        string director = ModSource(@"Ui\UiDirector.cs"), kits = ModSource(@"Ui\World\WorldKits.cs"), world = ModSource(@"Ui\World\WorldHost.cs");
        Check(OverlayBetween(director, "internal static void LetGoAll(", "private static void Quietly(").Contains("RemoveAll(why)") && director.Contains("case Kit.World: WorldKits.Remove(why); break;")
              && kits.Contains("WorldHost.Remove(why);") && world.Contains("host._overlays.Give();"),
            "overlay behind: the interface switched off, a screen's switch off or the guard switching the part off gives the game's box back through the world host");
        Check(OverlayBetween(cards, "private void Conceal()", "// The game's manager and its two boxes").Contains("_detailHide.Hide();"), "overlay behind: the large card still hides the large box at its root first");
        string carriers = ModSource(@"Careers\ComputerButtons.cs");
        Check(carriers.Contains("Transform list = template.transform.parent;") && carriers.Contains("workCopy.transform.SetParent(list, false);"),
            "overlay behind: the mod's Work for button stands in the game's own list under the box, so the box's walk meets it");
    }

    private static string OverlayBetween(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        int b = a < 0 ? -1 : text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return a < 0 || b < 0 ? string.Empty : text.Substring(a, b - a);
    }
}
