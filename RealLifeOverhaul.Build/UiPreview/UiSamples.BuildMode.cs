using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    /// <summary>The states of Build mode the previews and the checks draw.</summary>
    public static readonly string[] BuildModeStates = { "land", "zone", "place_idle", "catalogue", "catalogue_long", "rents", "traffic", "tour", "ghost" };

    private static readonly string[] BuildToolKeys =
    {
        "reallife_bld_mode_tool_land", "reallife_bld_mode_tool_zone", "reallife_bld_mode_tool_place", "reallife_bld_mode_tool_bulldoze", "reallife_bld_mode_tool_join", "reallife_bld_mode_tool_refurbish"
    };

    private static readonly string[] BuildToolEnglish = { "Buy land", "Zone", "Place a building", "Bulldoze", "Join or split", "Refurbish" };
    private static readonly UiIcon[] BuildToolIcons = { UiIcon.MapPin, UiIcon.PaintBrush, UiIcon.Building, UiIcon.Trash, UiIcon.Handshake, UiIcon.Wrench };

    private static readonly string[] BuildViewKeys =
    {
        "reallife_bld_mode_view_status", "reallife_bld_mode_view_zoning", "reallife_bld_mode_view_value", "reallife_bld_mode_view_occupancy", "reallife_bld_mode_view_underway",
        "reallife_bld_mode_view_rents", "reallife_bld_mode_view_traffic"
    };

    private static readonly string[] BuildViewEnglish = { "Land and buildings", "Zoning", "Land value", "Occupancy", "Under construction", "Rents", "Traffic" };
    private static readonly UiIcon[] BuildViewIcons = { UiIcon.Eye, UiIcon.Grid, UiIcon.Coin, UiIcon.People, UiIcon.Hammer, UiIcon.Wallet, UiIcon.Car };

    /// <summary>Build mode as the kit draws it: a tool chosen, the view's legend, what the district wants, the tool panel and the design window.</summary>
    public static UiBuildModel BuildMode(UiWords t, string state)
    {
        int tool = state == "zone" ? 1 : state == "land" ? 0 : 2;
        int view = state == "rents" ? 5 : state == "traffic" ? 6 : state == "land" || state == "zone" ? 1 : 0;
        var m = new UiBuildModel
        {
            Title = t("reallife_bld_mode_title", "Build mode"), District = "Uptown", Step = state == "place_idle" ? 3 : state == "ghost" ? 5 : 2,
            Hint = state == "place_idle" ? "Pick squares of your own land to place a building" : state == "ghost" ? "Check the cost, then Build here" : "Choose the land you want to buy",
            HintMore = "Esc clears what is picked", ViewCount = BuildViewKeys.Length, View = view, DemandTitle = t("reallife_ui_build_wants", "What the district wants"),
            LeaveLabel = t("reallife_ui_build_leave", "Leave"), SheetIcon = BuildToolIcons[tool],
        };
        for (int v = 0; v < m.ViewCount; v++)
        {
            m.ViewIcons[v] = BuildViewIcons[v];
            m.ViewNames[v] = t(BuildViewKeys[v], BuildViewEnglish[v]);
        }
        m.ViewTip = view == 5 ? t("reallife_bld_tip_view_rents", "What a square metre lets for, by each square's use. Greener is dearer.")
            : view == 6 ? t("reallife_bld_tip_view_traffic", "The traffic the game gives each lot, from quiet to busy.")
            : t("reallife_bld_tip_view_zoning", "Every square by its council zoning, in the game's colours for each use.");
        Legend(t, m, view);
        string[] use = { "Homes", "Shops", "Offices", "Warehouses" };
        int[] demand = { 72, 41, 63, 18 };
        int[] colours = { 0x4CD959, 0xFFA040, 0x4080FF, 0xFFCC26 };
        for (int u = 0; u < 4; u++)
        {
            m.Demand[u] = new UiBuildDemand
            {
                Name = t("reallife_bld_use_" + new[] { "homes", "shops", "offices", "warehouses" }[u], use[u]), Value = demand[u], Rgb = colours[u],
                Words = t(demand[u] >= 60 ? "reallife_ui_build_dem_high" : demand[u] >= 40 ? "reallife_ui_build_dem_some" : "reallife_ui_build_dem_low", demand[u] >= 60 ? "wants more" : demand[u] >= 40 ? "balanced" : "enough"),
                Tone = demand[u] >= 60 ? UiRole.Ok : demand[u] >= 40 ? UiRole.Accent : UiRole.Warn
            };
        }
        m.DemandCount = 4;
        m.Tools = new UiToolRowModel { Tools = new UiTool[BuildToolKeys.Length], Tool = tool };
        for (int i = 0; i < BuildToolKeys.Length; i++)
        {
            m.Tools.Tools[i] = new UiTool { Icon = BuildToolIcons[i], Name = t(BuildToolKeys[i], BuildToolEnglish[i]) };
        }
        m.SheetTitle = t(BuildToolKeys[tool], BuildToolEnglish[tool]);
        Sheet(t, m, state);
        m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") } };
        m.PromptCount = 1;
        if (state == "catalogue" || state == "catalogue_long")
        {
            m.Catalogue = BuildCatalogue(t, state == "catalogue_long");
        }
        if (state == "tour")
        {
            m.Tour = new UiBuildTourModel
            {
                Step = 1, Count = "Tip 2 of 4", Title = t("reallife_bld_tour_tools_title", "The tools, in the order you use them"),
                Body = t("reallife_bld_tour_tools_body", "Buy land, zone it, then place a building. A tool you cannot use yet is greyed, and its reason is a hover away."),
                Next = t("reallife_bld_tour_next", "Next"), Skip = t("reallife_bld_tour_skip", "Skip the tour")
            };
        }
        return m;
    }

    private static void Legend(UiWords t, UiBuildModel m, int view)
    {
        string[][] keys = view == 5 ? new[] { new[] { "reallife_bld_mode_key_rent_low", "Least a square metre", "24 70 52" }, new[] { "reallife_bld_mode_key_rent_high", "Most a square metre", "214 245 120" } }
            : view == 6 ? new[] { new[] { "reallife_bld_mode_key_traffic_low", "Quiet", "40 70 110" }, new[] { "reallife_bld_mode_key_traffic_high", "Busy", "235 70 60" } }
            : new[]
            {
                new[] { "reallife_bld_use_homes", "Homes", "76 217 89" }, new[] { "reallife_bld_use_shops", "Shops", "255 160 64" }, new[] { "reallife_bld_use_offices", "Offices", "64 128 255" },
                new[] { "reallife_bld_use_warehouses", "Warehouses", "255 204 38" }, new[] { "reallife_bld_mode_key_mixed", "Mixed", "214 90 214" }, new[] { "reallife_bld_mode_key_nozone", "No zoning", "150 150 150" }
            };
        for (int i = 0; i < keys.Length; i++)
        {
            string[] rgb = keys[i][2].Split(' ');
            m.Keys[i] = new UiBuildKey { FillRgb = (int.Parse(rgb[0]) << 16) | (int.Parse(rgb[1]) << 8) | int.Parse(rgb[2]), EdgeRgb = 0xFFFFFF, Words = t(keys[i][0], keys[i][1]) };
        }
        m.KeyCount = keys.Length;
        m.LegendTitle = m.ViewNames[m.View];
        m.LegendMore = view < 5 ? "Held ground, for sale edge, zoned edge" : null;
    }

    private static void Sheet(UiWords t, UiBuildModel m, string state)
    {
        int n = 0;
        UiBuildLine[] l = m.Lines;
        if (state == "land")
        {
            m.SheetCount = "8 squares";
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Text, Label = "On 125th Street", Tone = UiRole.Text2 };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Price", Value = "$1,900,000", Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Land tax a year", Value = "$22,800", Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Zoned", Value = "Homes", Tone = UiRole.OkText };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Height limit", Value = "12 storeys", Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Buttons, Label = "Buy for $1,900,000", Value = "Clear", Id = 1, Id2 = 2, Usable = true, Usable2 = true };
        }
        else if (state == "zone")
        {
            m.SheetCount = "24 squares of your own";
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Choices, Names = new[] { "Fill a side", "Rectangle", "Brush", "Unzone" }, Chosen = 0, Id = 60, Usable = true };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Text, Label = "Yours since Day 188. Zoned for Homes and Shops.", Tone = UiRole.Text2 };
            string[] names = { "Homes", "Shops", "Offices", "Warehouses" };
            int[] rgb = { 0x4CD959, 0xFFA040, 0x4080FF, 0xFFCC26 };
            for (int i = 0; i < 4; i++)
            {
                l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Zone, Label = names[i], Value = i < 2 ? "Allowed here" : "Asks City Hall: $20,000", Tone = i < 2 ? UiRole.OkText : UiRole.WarnText, Id = 10 + i, Rgb = rgb[i], On = i == 1, Usable = true };
            }
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Zone, Label = "Take the zone away", Id = 14, Rgb = -1, Usable = true, Tone = UiRole.Text2 };
        }
        else if (state == "place_idle")
        {
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Text, Label = "Pick the squares of your own land you want to build on.", Tone = UiRole.Text2 };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Text, Label = t("reallife_bld_fit_title", "You could build here"), Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Site, Label = "75 W 125th Street", Value = "Homes 9, Shops 12", Id = 200, On = true, Usable = true };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Site, Label = "112 W 126th Street", Value = "Shops 6, Offices 8", Id = 201, On = true, Usable = true };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Site, Label = "30 Lenox Avenue", Value = "Offices 5", Id = 202, Usable = true };
        }
        else if (state == "ghost")
        {
            m.SheetTitle = "C1, Apartment block";
            m.SheetCount = "6 squares";
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Cost", Value = "$1,900,000", Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Time", Value = "49 days", Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "Storeys", Value = "4 of 12", Tone = UiRole.Text };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Text, Label = "The plans ask for parking and none is chosen.", Tone = UiRole.WarnText };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Buttons, Value = "Parking under it: off", Id2 = 20, Usable2 = true };
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Buttons, Label = "Build here", Value = "Another design", Id = 21, Id2 = 22, Usable = true, Usable2 = true };
        }
        else
        {
            m.SheetCount = "24 squares";
            l[n++] = new UiBuildLine { Kind = UiBuildLineKind.Text, Label = "Pick squares to see what they are.", Tone = UiRole.Text2 };
        }
        m.LineCount = n;
    }

    private static UiBuildCatalogueModel BuildCatalogue(UiWords t, bool many)
    {
        var c = new UiBuildCatalogueModel
        {
            Context = "For your 6 squares on 125th Street, zoned Homes, up to 12 storeys", Tabs = new[] { "Homes 9", "Shops 12", "Offices 8", "Warehouses 5" }, Tab = 0,
            SearchPlaceholder = t("reallife_ui_build_search", "Search buildings"), FavouritesLabel = t("reallife_ui_build_favourites", "Favourites"),
            WantedLabel = t("reallife_ui_build_wanted", "WANTED"), FavouriteLabel = t("reallife_ui_build_favourite", "Favourite"), PriceLabel = t("reallife_ui_build_price", "Price"), SizeLabel = t("reallife_ui_build_size", "Size"),
            BuildTimeLabel = t("reallife_ui_build_time", "Build time"), RentLabel = t("reallife_ui_build_rent", "Rent when let"), PlanningLabel = t("reallife_ui_build_planning", "Planning"),
            DemandLabel = t("reallife_ui_build_demand", "Demand"), PlaceLabel = t("reallife_bld_cat_place", "Place it on the map"), BackLabel = t("reallife_bld_cat_back", "Back to the map"),
            NoneLabel = t("reallife_bld_cat_none", "No design of this kind fits these squares."), WeeksWords = t("reallife_ui_build_weeks", "weeks"), PlaceUsable = true,
            SummaryLine = "Walk-up flats: about $1,900,000 and 49 days to build, then planning permission."
        };
        string[] names = { "Walk-up flats", "Apartment tower", "Town house row", "Villa with pool", "Shop row", "Flat over shop", "Brick mansion block", "Glass corner unit", "Low flats", "Courtyard flats",
            "Plaza apartments", "Broadway lofts" };
        string[] costs = { "$1,900,000", "$6,800,000", "$900,000", "$3,200,000", "$1,300,000", "$700,000", "$4,400,000", "$500,000", "$2,100,000", "$2,600,000", "$5,900,000", "$7,400,000" };
        int count = many ? 12 : 9;
        for (int i = 0; i < count; i++)
        {
            c.Cards[i] = new UiBuildCard
            {
                Index = i, Name = names[i], Kind = "City design, Homes", Size = (3 + i % 6) + " storeys, " + (420 + 70 * i) + " m2", Cost = costs[i], Usable = i != 5, Chosen = i == 0, Wanted = i == 0 || i == 2,
                Favourite = i % 4 == 0, Picture = "build:use:0", Planning = i == 5 ? "Needs City Hall" : "Allowed here", PlanningOk = i != 5, Floors = 4, Width = 20 + i, Depth = 30 - i, Weeks = 7,
                Rent = "$31,200 a month", Demand = 72, WhyNot = i == 5 ? "Homes are not zoned there" : null
            };
        }
        c.CardCount = count;
        return c;
    }

    /// <summary>Build mode's panels over the open map, with the status bar's tool row where its apps were.</summary>
    public static void BuildModeScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        UiBuildModel m = BuildMode(t, state);
        UiStatusModel bar = Over(HudBar(t, true, false, false));
        bar.Apps = null;
        bar.AllApps = false;
        bar.MoneyChange = null;
        bar.Middle = UiToolRow.Width(m.Tools);
        WithBar(bar, () => UiBuildPanels.Lay(s, m, f, dark, k));
    }
}
