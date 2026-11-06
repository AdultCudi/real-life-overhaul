using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] OfferCommands = { UiOfferCard.BackCommand, UiOfferCard.GoCommand };
    private static bool _worldCallersChecked;

    // The world's pieces (Ui/World), every state each has: laid out in every language, frame and input as any screen. The
    // callers' check runs once, with the first of them.
    static partial void UiCasesWorld(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case UiWorld.PromptId:
                if (!_worldCallersChecked)
                {
                    _worldCallersChecked = true;
                    WorldCallerChecks();
                    WorldRuleChecks();
                    WorldFeatureChecks();
                }
                WorldBox(def, cases, "a tower's door, four floors on the game's four keys, one greyed",
                    (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldDoor(t, k), 0.62f, 0.58f));
                WorldBox(def, cases, "the game's own door at night, closed", (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldGameDoor(t, k), 0.55f, 0.5f, true));
                WorldBox(def, cases, "a lift on the ninth floor", (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldLift(t, k), 0.4f, 0.45f));
                WorldBox(def, cases, "a bus stop with two routes", (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldBusStop(t, k), 0.7f, 0.62f));
                WorldBox(def, cases, "a coffee cart's counter, a coffee and a chat", (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldCart(t, k), 0.5f, 0.55f));
                WorldBox(def, cases, "a door at the screen's right edge", (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldDoor(t, k), 0.99f, 0.7f));
                break;
            case UiWorld.BuildingId:
                WorldBox(def, cases, "a shop the player rents", (s, f, d, k, t) => UiSamples.WorldBuildingScene(s, f, d, k, t, UiSamples.WorldPlace.Rented));
                WorldBox(def, cases, "a rival's, closed", (s, f, d, k, t) => UiSamples.WorldBuildingScene(s, f, d, k, t, UiSamples.WorldPlace.Rival));
                WorldBox(def, cases, "an empty shop for rent", (s, f, d, k, t) => UiSamples.WorldBuildingScene(s, f, d, k, t, UiSamples.WorldPlace.ForRent, true));
                WorldBox(def, cases, "the owners' tab", (s, f, d, k, t) => UiSamples.WorldBuildingScene(s, f, d, k, t, UiSamples.WorldPlace.Owners));
                WorldBox(def, cases, "the card at the door before it is picked", (s, f, d, k, t) => UiSamples.WorldPromptScene(s, f, d, k, t, UiSamples.WorldBuildingCard(t), 0.5f, 0.4f));
                break;
            case UiWorld.PickerId:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a front desk, eight things to ask, one greyed)", Title = "Front desk", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldDesk(t, false)) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (eleven, over two pages)", Title = "Front desk", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldDesk(t, true)) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (custody, locked, the charge sheet whole)", Title = "Holding cell", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldCustody(t, true), true) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (nothing to choose)", Title = "Front desk", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldNothing(t)) });
                break;
            case UiWorld.OfferId:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a barista's job)", Title = "HUD", Commands = OfferCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldOfferScene(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (the job already held)", Title = "HUD", Commands = OfferCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldOfferScene(s, f, d, k, t, true) });
                break;
            case UiWorld.DriverId:
                WorldTool(def, cases, "nothing hired", (s, f, d, k, t) => UiSamples.WorldDriverScene(s, f, d, k, t, UiSamples.DriverState.Nothing));
                WorldTool(def, cases, "two cars to call under a contract", (s, f, d, k, t) => UiSamples.WorldDriverScene(s, f, d, k, t, UiSamples.DriverState.Cars));
                WorldTool(def, cases, "a car on its way", (s, f, d, k, t) => UiSamples.WorldDriverScene(s, f, d, k, t, UiSamples.DriverState.Coming));
                WorldTool(def, cases, "the car waiting", (s, f, d, k, t) => UiSamples.WorldDriverScene(s, f, d, k, t, UiSamples.DriverState.Waiting));
                break;
            case UiWorld.ItemsId:
                WorldItems(def, cases, "a store's goods to look at", (s, f, d, k, t) => UiSamples.WorldItemsScene(s, f, d, k, t, UiSamples.WorldItems(t, false)));
                WorldItems(def, cases, "an order's goods, four in it, one past the most", (s, f, d, k, t) => UiSamples.WorldItemsScene(s, f, d, k, t, UiSamples.WorldItems(t, true)));
                WorldItems(def, cases, "a search with two found", (s, f, d, k, t) => UiSamples.WorldItemsScene(s, f, d, k, t, UiSamples.WorldItems(t, true, "rack")));
                WorldItems(def, cases, "a search with none found", (s, f, d, k, t) => UiSamples.WorldItemsScene(s, f, d, k, t, UiSamples.WorldItems(t, true, "zebra")));
                WorldItems(def, cases, "nothing to list", (s, f, d, k, t) => UiSamples.WorldItemsScene(s, f, d, k, t, UiSamples.WorldItems(t, false, null, true)));
                break;
            case UiWorld.SignId:
                WorldTool(def, cases, "a neon sign", (s, f, d, k, t) => UiSamples.WorldSignScene(s, f, d, k, t, false, false));
                WorldTool(def, cases, "neon and bulbs", (s, f, d, k, t) => UiSamples.WorldSignScene(s, f, d, k, t, true, false));
                WorldTool(def, cases, "its kinds open", (s, f, d, k, t) => UiSamples.WorldSignScene(s, f, d, k, t, false, true));
                break;
            case UiWorld.TerminalId:
                WorldTool(def, cases, "its first choice", (s, f, d, k, t) => UiSamples.WorldTerminalScene(s, f, d, k, t, 0, false));
                WorldTool(def, cases, "all four made", (s, f, d, k, t) => UiSamples.WorldTerminalScene(s, f, d, k, t, 4, false));
                WorldTool(def, cases, "the designs open", (s, f, d, k, t) => UiSamples.WorldTerminalScene(s, f, d, k, t, 3, true));
                break;
            case UiWorld.PreviewId:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a building)", Title = "HUD", Commands = ToolCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPreviewScene(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a design, the walls cut away)", Title = "HUD", Commands = ToolCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPreviewScene(s, f, d, k, t, true) });
                break;
            case UiWorld.ColourId:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (round the game's field)", Title = "HUD", Commands = ToolCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.WorldColourScene(s, f, d, k, t) });
                break;
        }
    }

    // The commands of the world's small windows (UiToolPanel): a line, a second button, a colour's add, an open choice's option
    // and every Swatches line's colours.
    private static readonly int[] ToolCommands = Enumerable.Range(UiToolPanel.SwatchCommand, UiToolPanel.MostLines)
        .Concat(new[] { UiToolPanel.LineCommand, UiToolPanel.SecondCommand, UiToolPanel.AddCommand, UiToolPanel.OptionCommand }).ToArray();

    private static readonly int[] ItemsCommands =
    {
        UiItemsList.AddCommand, UiItemsList.LessCommand, UiItemsList.MoreCommand, UiItemsList.TypeCommand, UiItemsList.ClearCommand
    };

    // A small window over the HUD: the HUD's own targets with the window's.
    private static void WorldTool(UiScreenDef def, List<UiScreenCase> cases, string name, Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay) =>
        cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + name + ")", Title = "HUD", Commands = HudCommands.Concat(ToolCommands).ToArray(), Lay = lay });

    // The items list over the world, its keys its own.
    private static void WorldItems(UiScreenDef def, List<UiScreenCase> cases, string name, Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay) =>
        cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + name + ")", Title = "HUD", Commands = ItemsCommands, Lay = lay });

    // A world piece over the HUD: the HUD's own targets with the card's actions.
    private static void WorldBox(UiScreenDef def, List<UiScreenCase> cases, string name, Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay) =>
        cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + name + ")", Title = "HUD", Commands = HudCommands.Concat(new[] { UiPromptCard.ActionCommand }).ToArray(), Lay = lay });

    // ---------------------------------------------------------------- every caller through the kit

    // Every world box and every list window of the mod's, found in its scripts, and the proof that each draws through the
    // kit while the interface and its switch are on: a WorldPanel or DoorPanel (and the garage's own overlay) shows only
    // in the game's OverlayUI, which the kit's world prompt draws whatever is in it; a TravelList (and a DeskList, which
    // stands on one) asks the kit's picker at each opening before its window shows, and the picker takes every list but
    // the conversations' own scenes. A caller that drew a box of its own, or a list the picker never sees, fails here.
    private static void WorldCallerChecks()
    {
        string scripts = ModScripts();
        string gameUi = System.IO.Path.Combine(scripts, "Core", "GameUi");
        var makers = new[]
        {
            new KeyValuePair<string, Regex>("WorldPanel", new Regex(@"new\s+(?:GameUi\.)?WorldPanel\s*[\(\{]")),
            new KeyValuePair<string, Regex>("DoorPanel", new Regex(@"new\s+(?:GameUi\.)?DoorPanel\s*\(")),
            new KeyValuePair<string, Regex>("TravelList", new Regex(@"TravelList\.Make\s*\(")),
            new KeyValuePair<string, Regex>("DeskList", new Regex(@"new\s+(?:GameUi\.)?(?:DeskList|SceneDesk)\s*\(")),   // a conversation's SceneDesk stands on one
        };
        var callers = makers.ToDictionary(p => p.Key, p => new List<string>());
        var listNames = new List<KeyValuePair<string, string>>();   // a list window's literal name, where it is made
        var nameAt = new Regex(@"(?:TravelList\.Make|new\s+(?:GameUi\.)?(?:DeskList|SceneDesk))\s*\(\s*""([^""]+)""");
        var sceneNames = new List<string>();   // the scene windows: each names itself to the scene kit as its desk is made, and the picker leaves it there
        var sceneAt = new Regex(@"new\s+SceneDesk\s*\(\s*""([^""]+)""");
        var overlays = new List<string>();
        var bypass = new List<string>();
        var insides = new Regex(@"\b(headerGameObject|buttonsGameObject|firstLineLabel|secondLineLeftLabel|secondLineRightLabel)\b");
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = System.IO.Path.GetRelativePath(scripts, path).Replace('\\', '/');
            bool renderer = rel == "Core/GameUi/WorldPanel.cs" || rel.StartsWith("Core/GameUi/WorldPanel.", StringComparison.Ordinal) || rel == "Core/GameUi/DoorPanel.cs"
                            || rel == "Core/GameUi/TravelList.cs" || rel == "Core/GameUi/DeskList.cs";
            string text = File.ReadAllText(path);
            string code = Regex.Replace(text, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
            if (!renderer)
            {
                foreach (KeyValuePair<string, Regex> maker in makers)
                {
                    if (maker.Value.IsMatch(code))
                    {
                        callers[maker.Key].Add(rel);
                    }
                }
                foreach (Match m in sceneAt.Matches(code))
                {
                    sceneNames.Add(m.Groups[1].Value);
                }
                foreach (Match m in nameAt.Matches(code))
                {
                    listNames.Add(new KeyValuePair<string, string>(m.Groups[1].Value, rel));
                }
            }
            if (Regex.IsMatch(code, @"class\s+\w+\s*:[^{]*\bIOverlay\b") && !rel.StartsWith("Core/GameUi/WorldPanel", StringComparison.Ordinal))
            {
                overlays.Add(rel);
            }
            // The game's box's insides belong to the kit's prompt (and its contract lines): a caller writing them draws a box of its own.
            if (!rel.StartsWith("Ui/World/", StringComparison.Ordinal) && !rel.StartsWith("Core/GameContract", StringComparison.Ordinal) && !rel.StartsWith("Core/GameUi/", StringComparison.Ordinal)
                && insides.IsMatch(code))
            {
                bypass.Add(rel);
            }
        }
        int panels = callers["WorldPanel"].Count, doors = callers["DoorPanel"].Count, lists = callers["TravelList"].Count, desks = callers["DeskList"].Count;
        Console.WriteLine("    world callers: " + panels + " files make a WorldPanel, " + doors + " a DoorPanel, " + lists + " a TravelList, " + desks + " a DeskList; "
                          + overlays.Count + " an overlay of their own (" + string.Join(", ", overlays) + ")");
        foreach (KeyValuePair<string, List<string>> pair in callers)
        {
            Console.WriteLine("      " + pair.Key + ": " + string.Join(", ", pair.Value));
        }
        Check(panels >= 5 && doors >= 5 && lists >= 2 && desks >= 5,
            "world callers: the scan finds the mod's world boxes and lists (" + panels + " WorldPanel, " + doors + " DoorPanel, " + lists + " TravelList, " + desks
            + " DeskList files): the scan reads");

        // A world box: WorldPanel shows only in the game's OverlayUI, DoorPanel stands on a WorldPanel, the garage's overlay too.
        string panel = string.Join("\n", Directory.GetFiles(gameUi, "WorldPanel*.cs").Select(File.ReadAllText));
        string door = File.ReadAllText(System.IO.Path.Combine(gameUi, "DoorPanel.cs"));
        Check(panel.Contains("OverlayUI.Show(this)") && !panel.Contains("AddComponent<Canvas>") && !panel.Contains("Instantiate("),
            "world callers: a WorldPanel shows only in the game's OverlayUI, never on a canvas or a copy of its own");
        Check(Regex.IsMatch(door, @"new\s+WorldPanel\b") && door.Contains(".Show(") && !door.Contains("AddComponent<Canvas>"),
            "world callers: a DoorPanel stands on a WorldPanel and shows through it");
        bool overlaysShown = overlays.All(o => File.ReadAllText(System.IO.Path.Combine(scripts, o)).Contains("OverlayUI.Show("));
        Check(overlays.Count >= 1 && overlaysShown, "world callers: the mod's own overlays (" + string.Join(", ", overlays) + ") show through the game's OverlayUI.Show");
        Check(bypass.Count == 0, "world callers: no caller writes the game's box's insides (" + (bypass.Count == 0 ? "none" : string.Join(", ", bypass)) + ")");
        string prompt = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldPrompt.cs"));
        Check(prompt.Contains("OverlayUI.IsVisible") && prompt.Contains("GetButtons()") && prompt.Contains("_conceal.Add(_header.transform)") && prompt.Contains("_conceal.Add(_buttons.transform)")
              && prompt.Contains("UiWidgets.Press(b)"),
            "world callers: the kit's prompt draws whatever the game's OverlayUI shows, hides the game's header and buttons, and presses the game's own buttons");
        Check(prompt.Contains("_overlay.transform.position") && !prompt.Contains("WorldToScreenPoint") && !prompt.Contains("Camera.main"),
            "world callers: one projection a frame per shown prompt, the game's own: the card reads where the game put its box");

        // A list: TravelList asks the face before its window shows; DeskList stands on TravelList; the picker is the face.
        string travel = File.ReadAllText(System.IO.Path.Combine(gameUi, "TravelList.cs"));
        int open = travel.IndexOf("public void Open(", StringComparison.Ordinal);
        int asks = open >= 0 ? travel.IndexOf("_faced = TakeFace();", open, StringComparison.Ordinal) : -1;
        int shows = open >= 0 ? travel.IndexOf("_root.SetActive(true);", open, StringComparison.Ordinal) : -1;
        Check(open >= 0 && asks > open && shows > asks, "world callers: a TravelList asks the kit's picker at each opening before its window shows");
        Check(File.ReadAllText(System.IO.Path.Combine(gameUi, "DeskList.cs")).Contains("TravelList.Make("), "world callers: a DeskList stands on a TravelList");
        string kits = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldKits.cs"));
        string picker = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldPicker.cs"));
        Check(kits.Contains("TravelList.Face = WorldPicker.Instance"), "world callers: the kit's picker is the lists' face while its switch is on");
        var theirs = sceneNames.Distinct().ToList();
        Check(picker.Contains("SceneKit.Owns(windowName)") && !picker.Contains("\"RealLifeOverhaul "), "world callers: the picker leaves a window to the scene kit by the name its SceneDesk gave it, never by a list of its own");
        var named = listNames.Select(p => p.Key).Distinct().ToList();
        var stale = theirs.Where(n => !named.Contains(n)).ToList();
        Check(theirs.Count >= 1 && stale.Count == 0, "world callers: the conversations' scenes the picker leaves are lists the mod makes ("
                                                     + (stale.Count == 0 ? "all" : "not made: " + string.Join(", ", stale)) + ")");
        var drawn = named.Where(n => !theirs.Contains(n)).ToList();
        Console.WriteLine("    world lists: " + drawn.Count + " drawn by the picker (" + string.Join(", ", drawn) + "); " + theirs.Count + " left to the conversations");
        Check(drawn.Count >= 10, "world callers: the picker draws the mod's lists (" + drawn.Count + " named windows)");
        string building = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldBuilding.cs"));
        Check(building.Contains("Foreign(_resume.setDestinationButton != null ? _resume.setDestinationButton.transform.parent : null);")
              && Regex.IsMatch(building, @"case UiInfoPanel\.FooterCommand:[^}]*UiWidgets\.Press\(b\)"),
            "world callers: another mod's button beside the game's set destination (Voogle Route's copy of it) stands in the building's footer and is pressed as it is");
        string screen = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldPickerScreen.cs"));
        Check(screen.Contains("if (_list == null || _list.Locked)") && screen.Contains("_m.EmptyAction = _locked ? null")
              && Regex.IsMatch(screen, @"case UiPicker\.EmptyCommand:\s*case UiDraw\.CloseCommand:\s*return Back\(\);"),
            "world callers: a locked list keeps the player (custody): Back, the close and the empty state's way out are refused while it is locked");
    }

    // ---------------------------------------------------------------- the rules the world pieces stand on

    private static void WorldRuleChecks()
    {
        string[] week = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        Check(UiWorld.Days(new[] { true, true, true, true, true, false, false }, week, "{from} to {to}", "{list} and {last}", "Day Off") == "Monday to Friday",
            "world offer: five days in a row read as a run");
        Check(UiWorld.Days(new[] { true, false, true, false, true, false, false }, week, "{from} to {to}", "{list} and {last}", "Day Off") == "Monday, Wednesday and Friday",
            "world offer: days apart read as a list joined by its last");
        Check(UiWorld.Days(new[] { false, false, false, false, false, true, true }, week, "{from} to {to}", "{list} and {last}", "Day Off") == "Saturday and Sunday",
            "world offer: two days read as a pair, not a run");
        Check(UiWorld.Days(new[] { false, false, true, false, false, false, false }, week, "{from} to {to}", "{list} and {last}", "Day Off") == "Wednesday",
            "world offer: one day reads as its name");
        Check(UiWorld.Days(new bool[7], week, "{from} to {to}", "{list} and {last}", "Day Off") == "Day Off", "world offer: no day worked reads as the game's day off");

        // The card keeps its width between its least and its most, and a long line of a stop's routes ends in an ellipsis inside it.
        var card = new UiPromptCardModel
        {
            Title = "Bus stop: Avenue of the Americas and West 45th Street",
            Sub = "M104 to Harlem: 4 min\nM7 to South Ferry: 11 min\nM5 to Washington Heights: 19 min\nM1 to East Village: 25 min\nM2 to Battery Park: 31 min",
            State = "4 min", Actions = new[] { new UiPromptAction { Label = "Choose a stop", Key = new UiGameKey { Name = "F" }, Main = true } }, ActionCount = 1,
        };
        float w = UiPromptCard.Width(card), h = UiPromptCard.Height(card);
        var s = new UiScene();
        s.Begin("world card", 1920f, 1080f, true);
        UiPromptCard.Lay(s, UiAt.Screen, card, 100f, 100f);
        float bottom = 0f;
        for (int i = 0; i < s.ItemCount; i++)
        {
            bottom = Math.Max(bottom, s.Items[i].Y + s.Items[i].H);
        }
        Check(w <= UiPromptCard.MostWidth && w >= UiPromptCard.LeastWidth && bottom <= 100f + h + 0.5f,
            "world prompt: a stop with five routes keeps the card's width (" + w + " px) and stands inside its height, seven lines and the rest in an ellipsis");
        var one = new UiPromptCardModel { Title = "Lift", Actions = new UiPromptAction[0] };
        Check(UiPromptCard.Width(one) == UiPromptCard.LeastWidth && UiPromptCard.Height(one) > 0f, "world prompt: a box with one short line keeps the card's least width");
    }

    // ---------------------------------------------------------------- the pieces on the world's screens and where they come from

    private static void WorldFeatureChecks()
    {
        UiWords en = UiWords("en");
        UiInfoModel mine = UiSamples.WorldBuilding(en, UiSamples.WorldPlace.Rented), rival = UiSamples.WorldBuilding(en, UiSamples.WorldPlace.Rival);
        UiInfoModel rent = UiSamples.WorldBuilding(en, UiSamples.WorldPlace.ForRent);
        int at = Array.FindIndex(mine.Sections[1].Rows, r => r.Meter);
        UiInfoRow meter = at >= 0 ? mine.Sections[1].Rows[at] : default(UiInfoRow);
        Check(at >= 0 && meter.Value == "31 of 48" && Math.Abs(meter.Fill - 31f / 48f) < 0.001f && rival.Sections[1].Rows.Any(r => r.Meter && r.Fill == 0f),
            "world building: the business shows its customers this hour against the building's capacity as a meter (31 of 48), a closed one at none");
        Check(mine.Sections[0].Value == "Rented by you" && mine.Sections[1].Value == "Yours" && rival.Sections[0].Value == "A rival's" && rival.Sections[1].Value == "A rival's"
              && rent.Sections[0].Value == "For rent",
            "world building: each section says whose it is in a word or two (Rented by you, Yours, A rival's, For rent)");
        Check(mine.Sections[0].Rows[0].Link && mine.Sections[0].Rows[2].Link && !mine.Sections[0].Rows[1].Link && rival.Sections[1].Rows.Any(r => r.Link) && !rent.Sections[0].Rows[0].Link
              && mine.Footer.Contains(UiIcon.Locate) && rent.Footer.Contains(UiIcon.Locate),
            "world building: the owner and the neighbourhood are links, the city's own sentence is not, and the footer has the button that finds the building on the map");
        UiPickerModel cell = UiSamples.WorldCustody(en);
        UiPickEntry doctor = cell.Entries.Last();
        Check(cell.Context != null && cell.Context.Length == 1 && cell.Context[0].Label == "Held since" && cell.Context[0].Value == "02:10 today, 6 h 35 min"
              && cell.Entries[1].Figure == "Today, 14:10" && cell.Entries[2].Figure == "$5,000",
            "world custody: the cell says since when the player is held and for how long, the wait when it ends, and bail what it costs");
        Check(doctor.Disabled && doctor.Reason == "You are fine" && doctor.Figure == "Free" && doctor.Icon == UiIcon.Heart,
            "world custody: the duty doctor is free and greyed with its reason while the player is well");
        UiOfferModel offer = UiSamples.WorldOffer(en, UiDefaultKeys.Keyboard, false);
        Check(offer.Facts.Last().Sub == "About $720 a week", "world offer: the wage says about what the week's shifts pay");
        UiPromptCardModel lift = UiSamples.WorldLift(en, UiDefaultKeys.Keyboard);
        Check(lift.Actions[0].Main && lift.Actions[0].Label == "Go up to floor 11", "world prompt: the lift's main button takes the player to their own floor");

        // Where the pieces come from in the game, read in the sources: the door's line from the business's own hours, the
        // cell's held-since and hearing from the hold, the doctor's treatment the phone doctor's, and the cart's box from the
        // director's one-second look, never a frame.
        string scripts = ModScripts();
        string prompt = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldPrompt.cs"));
        Check(prompt.Contains("BusinessHelper.IsBusinessOpen(reg)") && prompt.Contains("reg.scheduleDays") && prompt.Contains("Door(current);"),
            "world prompt: a business's door line comes from its own opening hours, as the game's building box reads them");
        Check(prompt.Contains("line == null || !GameAddress(cbc)") && prompt.Contains("AddressHelper.ToFormattedString(cbc.building.Address)"),
            "world prompt: the door line takes the address's place only while it is the game's own address (another mod's words there stay)");
        string buildingCode = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldBuilding.cs"));
        Check(buildingCode.Contains("|| hour != _readHour)") && buildingCode.Contains("_readHour = hour;"),
            "world building: the panel's customers are read again as the game's hour turns");
        string cellCode = File.ReadAllText(System.IO.Path.Combine(scripts, "Law", "Custody.Cell.cs"));
        Check(cellCode.Contains("_panel.Since(state.StartedAt);") && cellCode.Contains("When(state.ReleaseAt)") && cellCode.Contains("LifeEvents.Instance?.Cure(null);"),
            "world custody: held since the hold's start, the hearing when the hold ends (the court sits), the doctor's cure the phone doctor's");
        string city = File.ReadAllText(System.IO.Path.Combine(scripts, "CityLife", "CityLifeDirector.cs"));
        int update = city.IndexOf("private void OnUpdate()", StringComparison.Ordinal);
        int updateEnd = update >= 0 ? city.IndexOf("\n        }", update, StringComparison.Ordinal) : -1;
        string frame = update >= 0 && updateEnd > update ? city.Substring(update, updateEnd - update) : null;
        Check(frame != null && !frame.Contains("Counter()") && !frame.Contains("Keys.Down") && Regex.IsMatch(city, @"_cart = cart;\s*\}\s*\}\s*Counter\(\);"),
            "world prompt: the cart's box is set from city life's one-second look, nothing a frame, and the game's own panel takes its keys");
        CriticChecks(en);
    }

    // The wave 2 critic's findings on the buying and world screens (3 Nov), each held by its sample.
    private static void CriticChecks(UiWords en)
    {
        UiPickerModel cell = UiSamples.WorldCustody(en), file = UiSamples.WorldCustody(en, true);
        Check(cell.Choose == "Choose" && cell.Second == "Read the file" && file.Second == "Close the file" && cell.Chosen == 1 && cell.Surfaces
              && cell.Entries[0].Detail == null && file.Entries[0].Detail != null,
            "world custody: F Choose and P Read the file at the foot, Sit it out chosen at rest, the charge sheet folded to its heading until P opens it");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "HUD");
        UiItemPanelModel bag = UiSamples.ItemPanel(en, "paper_bag_minimap");
        UiItemPanel.Lay(s, bag, frame, true, UiDefaultKeys.Keyboard);
        int panel = -1, tiles = 0;
        for (int i = 0; i < s.ItemCount && panel < 0; i++)
        {
            panel = (s.Items[i].Flags & UiFlags.Panel) != 0 ? i : -1;
        }
        for (int i = 0; i < s.NodeCount; i++)
        {
            tiles += s.Nodes[i].Command == UiItemPanel.CargoCommand ? 1 : 0;
        }
        UiItem box = panel >= 0 ? s.Items[panel] : default(UiItem);
        Check(panel >= 0 && !UiHudRules.Overlaps(box.X, box.Y, box.W, box.H, bag.AvoidX, bag.AvoidY, bag.AvoidW, bag.AvoidH) && tiles == 1
              && box.W == UiItemPanel.Narrow && UiDraw.CapWord("BACKSPACE") == "Bksp" && bag.Actions[0].Label == "Sell for $12",
            "item panel: a paper bag stands beside the minimap's card, as narrow as a thing in hand, one tile for its one stack and no wall of empty slots, Sell with its price on a short cap");
        UiCheckoutModel goods = UiSamples.Checkout(en, "goods");
        UiCheckoutLine popcorn = Array.Find(goods.Lines, l => l.Name == "Popcorn");
        Check(popcorn.Packs == 0 && popcorn.Total == "$0" && goods.Sums.Length == 2 && goods.QueueDetail != null,
            "checkout: a row of none taken totals $0, the goods and the sales tax in the price stand over the total, and the place in the line shows while choosing");
        // The owner's right click on a building (3 Nov): with no kit HUD up, the picked panel stood at the top left on the
        // world's canvas, under the game's HUD canvas, and nothing showed. It stands at the door now, and says so once.
        string building = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "World", "WorldBuilding.cs"));
        int dock = building.IndexOf("private void Dock(UiFrame frame)", StringComparison.Ordinal);
        int dockEnd = dock >= 0 ? building.IndexOf("private void Undock()", dock, StringComparison.Ordinal) : -1;
        string dockCode = dock >= 0 && dockEnd > dock ? building.Substring(dock, dockEnd - dock) : "";
        Check(dockCode.Contains("Door(frame, _dockW, _dockH)") && dockCode.Contains("_awaySaid") && dockCode.Contains("UiDirector.Logger?.Info(") && !building.Contains("TopBarFoot"),
            "world building: a picked building's panel with no kit HUD to hold it stands at its door where the game shows its options, and Player.log says why once");
        string host = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "World", "WorldHost.cs"));
        Check(host.Contains("bool write = UiFonts.CanDraw;") && host.Contains("_prompt.Frame(frame, write && ") && host.Contains("_building.Frame(frame, write && ") && host.Contains("_noFaceSaid = true;"),
            "world host: the door box and the building's panel stay the game's own until the kit has a font to write with, and Player.log says so once");
    }
}
