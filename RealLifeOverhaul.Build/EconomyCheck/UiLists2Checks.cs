using System;
using System.Linq;
using RealLifeOverhaul.Ui;

// The lists pass over the buying screens and the frame (13 Nov 2026): every warehouse of a vehicle purchase in an open list, a long cart that scrolls under
// its totals, a market filter of several types, a bundle's contents over its tile, the season's size on the weather pill, the clock's and the money's cards in
// the full menu's bar, the player's portrait at the needs, every hint line as a chip, objectives that scroll, a phone that gives way only while another mod's
// screen is open, a tile for other mods' apps, a faulted screen's switch reading off, and the pages of Settings and Staff and Flare's compose sheet taking
// what is asked of them. Game-free rules are run; the rest is held to the sources that carry it out.
internal static partial class Program
{
    private static void UiLists2()
    {
        UiLists2Buying();
        UiLists2Frame();
        UiLists2Drag();
        UiLists2Sources();
    }

    private static bool ListHas(UiScene s, string text)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Text == text)
            {
                return true;
            }
        }
        return false;
    }

    private static int ListNodes(UiScene s, int command)
    {
        int n = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            n += s.Nodes[i].Command == command ? 1 : 0;
        }
        return n;
    }

    // ------------------------------------------------------------------------------------------------- buying

    private static void UiLists2Buying()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Buying");

        // A vehicle purchase's warehouses: the field opens a list of every one, the chosen ticked, eight in view and the rest scrolling.
        var car = new UiVehicleBuyModel { Name = "Car", Price = "$9,000", Delivery = true, Warehouse = "Pick one", Buy = "Buy", Cancel = "Cancel", BuyEnabled = true, CancelEnabled = true };
        var s = new UiScene();
        UiVehicleBuy.Lay(s, car, frame, true, UiDefaultKeys.Keyboard);
        Check(ListNodes(s, UiVehicleBuy.WarehouseCommand) == 1 && ListNodes(s, UiVehicleBuy.WarehouseOptionCommand) == 0,
            "lists: a shut warehouse field is one target and shows no list");
        car.WarehouseOptions = Enumerable.Range(1, 20).Select(i => "Warehouse " + i).ToArray();
        car.WarehouseAt = 3;
        car.WarehouseOpen = true;
        s = new UiScene();
        UiVehicleBuy.Lay(s, car, frame, true, UiDefaultKeys.Keyboard);
        Check(ListNodes(s, UiVehicleBuy.WarehouseOptionCommand) >= 8 && ListHas(s, "Warehouse 1") && !ListHas(s, "Warehouse 20"),
            "lists: an open warehouse list shows its first eight and scrolls the rest (twenty warehouses, " + ListNodes(s, UiVehicleBuy.WarehouseOptionCommand) + " rows laid)");
        car.WarehouseOffset = 20000f;
        s = new UiScene();
        UiVehicleBuy.Lay(s, car, frame, true, UiDefaultKeys.Keyboard);
        Check(ListHas(s, "Warehouse 20") && !ListHas(s, "Warehouse 1"), "lists: scrolled to its end the list reaches the last warehouse");
        Check(UiVehicleBuy.Commands.Contains(UiVehicleBuy.WarehouseOptionCommand), "lists: the warehouse option is a command of the screen's own list");

        // A long cart scrolls: every row is a target, the total and the buttons stay outside the scrolling region.
        var cart = new UiCheckoutModel { Title = "Purchase", Choose = true, Total = "$900", TotalLabel = "To pay", Order = "Place order", OrderEnabled = true, Cancel = "Cancel", CancelEnabled = true };
        cart.Lines = Enumerable.Range(0, 60).Select(i => new UiCheckoutLine { Name = "Item " + i, Price = "$1", Total = "$0", Packs = 0, CanMore = true }).ToArray();
        s = new UiScene();
        UiCheckout.Lay(s, cart, frame, true, UiDefaultKeys.Keyboard);
        int rows = 0;
        for (int i = 0; i < 60; i++)
        {
            rows += s.FindNode(UiCheckout.MoreCommand, i) >= 0 ? 1 : 0;
        }
        bool scrolls = false, footOut = true;
        for (int c = 0; c < s.ClipCount; c++)
        {
            scrolls |= s.Clips[c].ContentHeight > s.Clips[c].H + 1f;
        }
        int order = s.FindNode(UiCheckout.OrderCommand, 0), cancel = s.FindNode(UiCheckout.CancelCommand, 0);
        footOut = order >= 0 && cancel >= 0 && s.Nodes[order].Clip < 0 && s.Nodes[cancel].Clip < 0;
        Check(rows == 60 && scrolls, "lists: all sixty rows of a long cart are targets in a scrolling region (" + rows + ")");
        Check(footOut && cart.Rows >= UiCheckoutModel.PerPage && UiCheckout.Height(cart) <= frame.BandY, "lists: the totals and the buttons stand outside the scroll and the receipt fits the screen (" + UiCheckout.Height(cart) + " of " + frame.BandY + ")");
        cart.Offset = 0f;
        cart.Reveal = 55;
        s = new UiScene();
        UiCheckout.Lay(s, cart, frame, true, UiDefaultKeys.Keyboard);
        Check(cart.Offset > 0f, "lists: a row the keys move to is scrolled into view");

        // A bundle's contents over its tile on the item panel, only while the pointer rests on it.
        var panel = new UiItemPanelModel { Title = "Item", Name = "Box", CargoSlots = 3, CargoLabel = "Cargo", CargoValue = "2 of 3", CargoContainsLabel = "Contains" };
        panel.Cargo = new[] { new UiCatalogueCard { Name = "Bundle", Figure = "1" }, new UiCatalogueCard { Name = "Apple", Figure = "4" } };
        panel.CargoContents = new[] { new[] { "Apples (3)", "Pears (2)" }, null };
        panel.CargoHover = -1;
        s = new UiScene();
        UiItemPanel.Lay(s, panel, frame, true, UiDefaultKeys.Keyboard);
        bool before = ListHas(s, "Apples (3)");
        panel.CargoHover = 0;
        s = new UiScene();
        UiItemPanel.Lay(s, panel, frame, true, UiDefaultKeys.Keyboard);
        bool over = ListHas(s, "Apples (3)") && ListHas(s, "Pears (2)");
        panel.CargoHover = 1;
        s = new UiScene();
        UiItemPanel.Lay(s, panel, frame, true, UiDefaultKeys.Keyboard);
        Check(!before && over && !ListHas(s, "Apples (3)"), "lists: a bundle's contents show over its tile while the pointer rests on it, and a tile of one thing shows none");
    }

    // ------------------------------------------------------------------------------------------------- the frame

    private static UiScene BarOf(UiStatusModel m, UiFrame frame)
    {
        var s = new UiScene();
        Func<UiStatusModel> was = UiChrome.Bar;
        UiChrome.Bar = () => m;
        try
        {
            s.Begin("bar", frame.Width, frame.Height, true);
            UiDraw.StatusBar(s, frame, UiDefaultKeys.Keyboard);
        }
        finally
        {
            UiChrome.Bar = was;
        }
        return s;
    }

    private static void UiLists2Frame()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Frame");
        UiStatusModel plain = new UiStatusModel { Time = "09:30", Day = "Monday (Day 3)", Money = "$1,200", Weather = UiIcon.CloudSun, Season = "Autumn", WeatherWords = "Sunny" };

        // The season's size grows the season's words inside a pill that keeps its height.
        float normal = UiDraw.PlanPills(plain, frame.Width).Weather;
        var large = new UiStatusModel { Time = plain.Time, Day = plain.Day, Money = plain.Money, Weather = plain.Weather, Season = plain.Season, WeatherWords = plain.WeatherWords, SeasonSize = 2 };
        float biggest = UiDraw.PlanPills(large, frame.Width).Weather;
        Check(biggest > normal && Math.Abs(UiDraw.BarPill - UiFrame.Toolbar) < 0.01f, "lists: the season size option grows the season's line on the weather pill, which keeps its height (" + normal + " to " + biggest + ")");

        // The full menu's bar: the clock and the money are hover targets that press nothing; the HUD keeps its own.
        plain.PillsHover = true;
        UiScene s = BarOf(plain, frame);
        Check(ListNodes(s, UiDraw.ClockHoverCommand) == 1 && ListNodes(s, UiDraw.MoneyHoverCommand) == 1 && UiDraw.IsBarHover(UiDraw.ClockHoverCommand) && UiDraw.IsBarHover(UiDraw.MoneyHoverCommand)
              && UiDraw.IsChrome(UiDraw.ClockHoverCommand) && UiDraw.IsChrome(UiDraw.MoneyHoverCommand),
            "lists: the full menu's bar has the clock and the money as hover targets of the frame's own commands");
        plain.PillsHover = false;
        s = BarOf(plain, frame);
        Check(ListNodes(s, UiDraw.ClockHoverCommand) == 0 && ListNodes(s, UiDraw.MoneyHoverCommand) == 0, "lists: a bar with no hover asked for makes none");

        // The player's portrait at the head of the needs, a figure wider for it.
        var needs = new UiStatusModel { Time = "09:30", Day = "Monday", Money = "$1", Energy = 80, Hunger = 70, Happiness = 60 };
        float without = UiDraw.PlanPills(needs, frame.Width).Needs;
        needs.Player = "Alex Doe";
        float with = UiDraw.PlanPills(needs, frame.Width).Needs;
        s = BarOf(needs, frame);
        bool portrait = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            portrait |= s.Items[i].Kind == UiKind.Portrait && s.Items[i].Text == "Alex Doe";
        }
        Check(with > without && portrait, "lists: the player's portrait stands at the head of the needs pill (" + without + " to " + with + ")");

        // Hint lines: a line of words alone and the lines past the number keys are chips too, none without its words or its key.
        var chips = new UiScene();
        chips.Begin("chips", frame.Width, frame.Height, true);
        chips.AddPrompt(UiAction.Count, "Hold to sprint");
        chips.AddPrompt(UiAction.Pick1, "Walk");
        UiDraw.KeyChips(chips, frame.Width - 24f, 100f, UiDefaultKeys.Keyboard, 800f);
        int caps = 0;
        for (int i = 0; i < chips.ItemCount; i++)
        {
            caps += chips.Items[i].Kind == UiKind.KeyCap ? 1 : 0;
        }
        Check(caps == 1 && ListHas(chips, "Hold to sprint") && ListHas(chips, "Walk"), "lists: a hint of words alone is a chip without a key cap (" + caps + " caps)");

        // The objectives' groups scroll once they outgrow the room, none cut: fourteen groups of thirty lines.
        var goals = new UiObjectivesModel { Title = "Objectives", GroupCount = 14, Groups = new UiObjectiveGroup[14], MaxHeight = 640f };
        for (int g = 0; g < 14; g++)
        {
            goals.Groups[g] = new UiObjectiveGroup { Title = "Group " + g, Icon = UiIcon.Flag, LineCount = 30, Lines = new UiObjectiveLine[30] };
            for (int l = 0; l < 30; l++)
            {
                goals.Groups[g].Lines[l] = new UiObjectiveLine { Text = "Line " + g + "." + l };
            }
        }
        s = new UiScene();
        s.Begin("goals", frame.Width, frame.Height, true);
        UiObjectives.Lay(s, UiAt.Screen, goals, 24f, 24f);
        bool clipped = goals.Clip >= 0 && goals.Clip < s.ClipCount && s.Clips[goals.Clip].ContentHeight > s.Clips[goals.Clip].H + 1f;
        int lineTargets = ListNodes(s, UiObjectives.LineCommand);
        Check(UiObjectives.Height(goals) <= goals.MaxHeight + 0.5f && clipped && lineTargets == 14 * 30,
            "lists: a long list of objectives stands in its room and scrolls inside it, every group and line laid (" + lineTargets + " lines, " + UiObjectives.Height(goals) + " of " + goals.MaxHeight + ")");
        Check(UiObjectives.LineStride > 30, "lists: a line's command carries its group and its place in the group past thirty lines");
        goals.MaxHeight = 0f;
        s = new UiScene();
        s.Begin("goals", frame.Width, frame.Height, true);
        UiObjectives.Lay(s, UiAt.Screen, goals, 24f, 24f);
        Check(goals.Clip == -1, "lists: objectives that fit their room have no scroll");

        // A tile for other mods' apps that the row has no room for, before All apps, on the Other mods tab.
        var row = new UiStatusModel { Time = "09:30", Day = "Monday", Money = "$1", Apps = new[] { UiIcon.Grid, UiIcon.Grid }, AppTints = new UiRole[2], AppBadges = new int[2], AppPictures = new int[2], AllApps = true, AppsPress = true };
        float lacking = UiDraw.PlanPills(row, frame.Width).RowW;
        s = BarOf(row, frame);
        Check(ListNodes(s, UiDraw.MoreAppsCommand) == 0, "lists: a row with no other mods' apps left over has no tile for them");
        row.OtherLeft = 3;
        row.OtherBadge = 2;
        float having = UiDraw.PlanPills(row, frame.Width).RowW;
        s = BarOf(row, frame);
        Check(ListNodes(s, UiDraw.MoreAppsCommand) == 1 && having > lacking && UiDraw.IsChrome(UiDraw.MoreAppsCommand), "lists: other mods' apps past the row have a tile of their own on it (" + lacking + " to " + having + ")");

        // A tool's glyph in the colour another mod gave its button: a purple report button stays purple, a plain one is plain.
        Check(HudNoteRules.TintOf(270f, 0.6f, 0.9f) == UiRole.Chart7 && HudNoteRules.TintOf(0f, 0f, 1f) == UiRole.None && HudNoteRules.TintOf(200f, 0.05f, 0.9f) == UiRole.None
              && HudNoteRules.TintOf(10f, 0.8f, 0.9f) == UiRole.Chart6 && HudNoteRules.TintOf(120f, 0.7f, 0.8f) == UiRole.Chart3,
            "lists: a button's colour from another mod is the kit's nearest hue, and the game's own white is none");

        // A faulted screen's switch reads off and gives the screen another try when it is turned on.
        UiBreaker.Reset();
        Check(!UiScreenSwitches.Faulted("ui_screen_hud") && UiScreenSwitches.ScreenOf("ui_screen_hud") == "hud" && UiScreenSwitches.ScreenOf("ui_motion") == null,
            "lists: a screen's option id names its screen and no other option does");
        UiBreaker.Trip("hud");
        bool faulted = UiScreenSwitches.Faulted("ui_screen_hud") && !UiScreenSwitches.Faulted("ui_screen_checkout") && !UiScreenSwitches.On("hud");
        bool retried = UiScreenSwitches.Retry("ui_screen_hud") && UiScreenSwitches.On("hud") && !UiScreenSwitches.Faulted("ui_screen_hud");
        UiBreaker.Reset();
        Check(faulted && retried, "lists: a faulted screen's switch reads as faulted, and turning it on closes its breaker");
    }

    // ------------------------------------------------------------------------------------------------- dragging a scroll region

    private static void UiLists2Drag()
    {
        // Two regions, the small one inside the large, and a button standing in the small one.
        var s = new UiScene();
        s.Begin("pan", 1000f, 800f, true);
        short outer = s.AddClip(0f, 0f, 1000f, 800f, 100f);
        s.Clips[outer].ContentHeight = 2000f;
        short inner = s.AddClip(100f, 100f, 300f, 200f, 0f);
        s.Clips[inner].ContentHeight = 600f;
        // A drag down inside the small region at its start: nothing above to show, so the large one takes it; at its middle the small one does.
        Check(UiPanRules.ClipAt(s, 200f, 150f, 30f) == outer, "drag: a drag down over a region at its start goes to the region round it, which can still move that way");
        Check(UiPanRules.ClipAt(s, 200f, 150f, -30f) == inner, "drag: a drag up takes the innermost region that can move up");
        s.Clips[inner].Offset = 100f;
        Check(UiPanRules.ClipAt(s, 200f, 150f, 30f) == inner, "drag: the innermost region takes a drag it can follow");
        s.Clips[inner].Offset = 400f;
        Check(UiPanRules.ClipAt(s, 200f, 150f, -30f) == outer, "drag: a region at its end passes a drag further that way to the one round it");
        Check(UiPanRules.ClipAt(s, 900f, 700f, -30f) == outer && UiPanRules.ClipAt(s, 2000f, 2000f, 30f) == -1, "drag: a press outside every region drags none");
        s.Clips[inner].ContentHeight = 150f;
        Check(UiPanRules.ClipAt(s, 200f, 150f, -30f) == outer, "drag: a region whose content fits has nothing to drag");

        // The content follows the pointer from where the drag began and stops at its ends.
        Check(Math.Abs(UiPanRules.Want(100f, 40f, 600f, 200f) - 60f) < 0.01f && Math.Abs(UiPanRules.Want(100f, -40f, 600f, 200f) - 140f) < 0.01f,
            "drag: the content follows the pointer, down for down");
        Check(UiPanRules.Want(100f, 500f, 600f, 200f) == 0f && UiPanRules.Want(100f, -900f, 600f, 200f) == 400f && UiPanRules.Want(100f, -900f, 100f, 200f) == 0f,
            "drag: at either end it stops, whatever the pointer does past it");
        Check(Math.Abs(UiPanRules.Notches(-64f, 64f) - 1f) < 0.001f && UiPanRules.Step(-96f, 1.5f, 64f) == 64f && UiPanRules.Step(0f, 1f, 64f) == 64f && UiPanRules.Step(-30f, 1f, 64f) == 30f,
            "drag: a distance is a number of wheel notches of the length the last one measured, a nonsense length is not learned");

        // A sheet on a higher layer is dragged by its own region, never the page under it.
        var sheet = new UiScene();
        sheet.Begin("sheet", 1000f, 800f, true);
        short page = sheet.AddClip(0f, 0f, 1000f, 800f, 0f);
        sheet.Clips[page].ContentHeight = 3000f;
        UiDraw.Node(sheet, new UiAt { Parent = -1, Clip = page, Layer = 0, Order = -1 }, 0f, 0f, 100f, 40f, 1, 0, -1);
        UiDraw.Node(sheet, new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 }, 300f, 300f, 100f, 40f, 2, 0, -1);
        Check(UiPanRules.ClipAt(sheet, 350f, 320f, -30f) == -1, "drag: with a sheet over the page, a drag on the sheet does not scroll the page behind it");

        // The press: a drag past the threshold is no click, a short press stays one (UiPress, as the view tells it).
        var press = new UiPress();
        var nodes = new UiScene();
        nodes.Begin("press", 400f, 400f, true);
        int box = UiDraw.Box(nodes, UiAt.Screen, 10f, 10f, 100f, 40f, UiRole.Panel, UiRole.None, 0f);
        UiDraw.Node(nodes, UiAt.Screen, 10f, 10f, 100f, 40f, 7, 0, box);
        press.Down(nodes, 0);
        press.Dragged();
        Check(press.Up(nodes, 50f, 30f) == -1, "drag: a press that became a drag fires no command when it ends");
        press.Down(nodes, 0);
        Check(press.Up(nodes, 50f, 30f) == 0, "drag: a short press that never moved past the threshold is still a click");
    }

    // ------------------------------------------------------------------------------------------------- the sources

    private static void UiLists2Sources()
    {
        string car = ModSource(@"Ui\Buy\VehicleBuyScreen.cs");
        Check(car.Contains("case UiVehicleBuy.WarehouseOptionCommand:") && car.Contains("list.SelectOption(arg)") && !car.Contains("list.SelectNextOption()") && car.Contains("_warehouseOpen = false;"),
            "lists sources: a warehouse is chosen from the game's list by its own SelectOption (its listener prices the delivery), never by cycling, and Back shuts the list first");
        Check(car.Contains("DeliveryDetail") && car.Contains("words.IndexOf('\\n')"), "lists sources: the delivery's two lines, the words and the fee, are the game's own two");

        string checkout = ModSource(@"Ui\Buy\CheckoutScreen.cs") + ModSource(@"Ui\UiCheckout.cs");
        Check(!checkout.Contains("PageCommand") && !checkout.Contains("Pages(") && checkout.Contains("_m.Reveal = arg;") && checkout.Contains("_m.Offset = Mathf.Max(0f, _m.Offset - notches"),
            "lists sources: the checkout scrolls by the wheel and by the keys' focus and has no pages");

        string market = ModSource(@"Ui\Market\MarketScreen.cs");
        Check(market.Contains("SetOn(_toggles[k], !IsOn(_toggles[k]))") && market.Contains("_v.OptionTicks[k + 1] = IsOn(_toggles[k])") && ModSource(@"Ui\UiMarket.cs").Contains("m.OptionOffset, OptionCommand, 1, m.OptionTicks"),
            "lists sources: a market filter takes several types, each its game's own toggle, and the list shows every one ticked");

        string kit = ModSource(@"Ui\Buy\ItemPanelKit.cs");
        Check(kit.Contains("GameContract.SlotBundle") && kit.Contains("IUiPointing") && kit.Contains("_m.CargoHover") && ModSource(@"Core\GameContract.Buy.cs").Contains("\"bundleItemsTooltip\""),
            "lists sources: a bundle's contents come from the game's own tooltip on the slot and show as the pointer rests on its tile");

        string game = ModSource(@"Ui\Hud\HudStatusGame.cs");
        Check(game.Contains("m.SeasonSize = SeasonRules.BarSizeOf(Settings.SeasonBarSize)") && game.Contains("m.PillsHover = !ForHud") && game.Contains("FaceSources.Player(player)"),
            "lists sources: the bar reads the season's size option, hovers in the full menu and the player's portrait");
        string pointer = ModSource(@"Ui\UiView.Pointer.cs");
        Check(pointer.Contains("UiPanRules.ClipAt(") && pointer.Contains("_pressed.Dragged();   // the control under the press") && pointer.Contains("Wheeled?.Invoke(_panClip, notches)") && pointer.Contains("DragTaken != null && DragTaken()")
              && ModSource(@"Ui\UiHost.cs").Contains("_view.DragTaken = () => _dragging || _grip.Moving;") && !MemberText(pointer, "private void Pan(").Contains("new "),
            "drag sources: the view drags a region by the wheel's own way once nothing else took the drag, cancels the press, and allocates nothing while it does");
        string host = ModSource(@"Ui\UiHost.cs");
        Check(host.Contains("TooltipKit.Years(x, y, w) : TooltipKit.Money(x, y, w)") && host.Contains("if (UiDraw.IsBarHover(n.Command))") && host.Contains("CardGone();"),
            "lists sources: the host brings the clock's and the money's cards over the pill, quietly takes a press on it, and takes the card away with the pointer");

        string hints = ModSource(@"Ui\HudScreens\HudHints.cs");
        Check(!hints.Contains("Most") && hints.Contains("PickSlots") && hints.Contains("UiAction.Count, Label = words"), "lists sources: no cap on the hint lines, one that has no key or no number key left is a chip all the same");
        string goalsText = ModSource(@"Ui\HudScreens\HudGoals.cs");
        Check(!goalsText.Contains("MostGroups") && !goalsText.Contains("MostLines") && goalsText.Contains("RoomForGroup") && goalsText.Contains("Model.MaxHeight") && goalsText.Contains("public static bool Wheel(")
              && ModSource(@"Ui\Hud\HudScreen.cs").Contains("HudGoals.Wheel(clip, notches)") && ModSource(@"Ui\Hud\HudHost.cs").Contains("_view.Wheeled"),
            "lists sources: the objectives' arrays grow to the game's window, the panel scrolls by the wheel the HUD host passes on");

        string phone = ModSource(@"Ui\Phone\PhoneKit.cs");
        Check(!phone.Contains("_stopped") && phone.Contains("_named.gameObject.activeInHierarchy") && phone.Contains("the classic phone while it lasts"),
            "lists sources: the kit phone gives way only while the cause stands, another mod's screen only while it is open");

        string settings = ModSource(@"Ui\Settings\SettingsScreen.Rows.cs") + ModSource(@"Ui\Settings\SettingsScreen.cs");
        Check(settings.Contains("UiScreenSwitches.Faulted(OptionsLayout.AppId(line))") && settings.Contains("\"reallife_ui_note_fallback\"") && settings.Contains("UiScreenSwitches.Retry("),
            "lists sources: a faulted screen's switch reads off with the words that say why, and a press on it tries the screen again");
        Check(settings.Contains("public static void Want(string target)") && ModSource(@"Meta\SettingsApp.cs").Contains("SettingsScreen.Want(target)")
              && ModSource(@"Ui\Settings\StaffScreen.cs").Contains("public static void Want(string key)") && ModSource(@"Meta\StaffApp.cs").Contains("StaffScreen.Want(entry)"),
            "lists sources: the Settings and Staff pages of the kit take the section, option or entry another feature opens them on");
        string flare = ModSource(@"Ui\Flare\FlareScreen.cs");
        Check(flare.Contains("TakeKitCompose()") && ModSource(@"Social\Flare\FlareDirector.Camera.cs").Contains("KitCompose = d.Id;") && ModSource(@"Social\Flare\FlareDirector.Use.cs").Contains("public int KitCompose = -1;"),
            "lists sources: a picture shared to Flare from another feature opens the kit's compose sheet on it");

        string clock = ModSource(@"Ui\Tooltips\TooltipEmployee.cs") + ModSource(@"Ui\HudScreens\HudSides.cs") + ModSource(@"Ui\Phone\PhoneNextGame.cs");
        Check(!clock.Contains("GetFormattedTime") && !clock.Contains("PlannerText.Clock") && clock.Contains("UiClock.At(") && clock.Contains("UiClock.AtMinute("),
            "lists sources: the shift times of a tooltip, the opening hours of a building and a day's next entry go through the kit's one clock");

        string top = ModSource(@"Ui\HudScreens\HudTopGame.cs");
        Check(top.Contains("ToolTints") && top.Contains("HudNoteRules.TintOf(") && top.Contains("tip.descriptionKey.Split(',')") && ModSource(@"Ui\UiDraw.HudBar.cs").Contains("m.ToolTints[i]"),
            "lists sources: the report button's colour and tooltip, as another mod gave them, stand on the pill and its hover card");
    }
}
