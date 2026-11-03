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
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldDesk(t, false), false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (eleven, over two pages)", Title = "Front desk", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldDesk(t, true), false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (custody, locked, the charge sheet whole)", Title = "Holding cell", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldCustody(t), true, true) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (nothing to choose)", Title = "Front desk", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldNothing(t), false) });
                break;
            case UiWorld.OfferId:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a barista's job)", Title = "HUD", Commands = OfferCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldOfferScene(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (the job already held)", Title = "HUD", Commands = OfferCommands,
                    Lay = (s, f, d, k, t) => UiSamples.WorldOfferScene(s, f, d, k, t, true) });
                break;
        }
    }

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
        Match scenes = Regex.Match(picker, @"Scenes\s*=\s*\{([^}]*)\}");
        var theirs = scenes.Success ? Regex.Matches(scenes.Groups[1].Value, @"""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList() : new List<string>();
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
            "world prompt: a stop with five routes keeps the card's width (" + w + " px) and stands inside its height, four lines and the rest in an ellipsis");
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
        Check(mine.Sections[0].Rows[0].Link && mine.Sections[0].Rows[1].Link && rival.Sections[1].Rows.Last().Link && !rent.Sections[0].Rows[0].Link
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
    }
}
