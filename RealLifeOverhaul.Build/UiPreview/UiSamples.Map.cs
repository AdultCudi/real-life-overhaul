using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    /// <summary>The states of the city map's left dock the previews and the checks draw.</summary>
    public static readonly string[] MapFilterStates = { "filters", "search", "search_none", "foreign", "subway", "taxi", "picker", "picker_long" };

    // ------------------------------------------------------------------------------------------------- the filters

    // A name of the sample's own city (a bank, a bakery) is plain English; only the game's and the mod's words are keys.
    private static string Say(UiWords t, string key, string english) => key.StartsWith("reallife_sample", System.StringComparison.Ordinal) ? english : t(key, english);

    private static MapRow Group(UiWords t, string key, string english, UiIcon icon, UiRole tint, int on, int total, bool collapsed, bool foreign = false) => new MapRow
    {
        Kind = MapRowKind.Group, Name = Say(t, key, english), Icon = icon, Tint = tint, OnCount = on, Total = total, On = on == total && total > 0, Collapsed = collapsed, Foreign = foreign
    };

    private static MapRow Line(UiWords t, string key, string english, UiIcon icon, bool on, int count, bool locate = true, string note = null, bool off = false) => new MapRow
    {
        Kind = MapRowKind.Line, Name = Say(t, key, english), Icon = icon, On = on, Count = count, Locate = locate, Note = note, Off = off
    };

    /// <summary>The rows of the map's filters as the game lists them: the groups in its order, three open and the rest folded.</summary>
    public static MapRow[] MapFilterRows(UiWords t, bool foreign)
    {
        var rows = new List<MapRow>(40)
        {
            Group(t, "bizman_status", "Status", UiIcon.Check, UiRole.Ok, 2, 3, false),
            Line(t, "buildingresume_rented_by_you", "Rented by you", UiIcon.Key, true, 4),
            Line(t, "citymap_filter_hide_closed", "Hide closed businesses", UiIcon.Clock, true, -1, false),
            Line(t, "reallife_sample_rival_rented", "Rented by Onyx Luxury", UiIcon.Flag, false, 11),
            Group(t, "citymap_category_jobs", "Jobs", UiIcon.Briefcase, UiRole.None, 0, 3, true),
            Group(t, "citymap_category_rentable", "Buildings for rent", UiIcon.Building, UiRole.None, 3, 4, false),
            Line(t, "reallife_sample_retail", "Retail", UiIcon.Store, true, 38),
            Line(t, "reallife_sample_office", "Office", UiIcon.Building, true, 27),
            Line(t, "reallife_sample_residential", "Residential", UiIcon.Home, true, 22),
            Line(t, "reallife_sample_warehouse", "Warehouse", UiIcon.Package, false, 13),
            Group(t, "citymap_category_special_buildings", "Special buildings", UiIcon.Star, UiRole.None, 5, 7, false),
            Line(t, "common_subwaystation", "Subway Station", UiIcon.Train, true, 14),
            Line(t, "reallife_sample_bank", "Bank", UiIcon.Bank, true, 6),
            Line(t, "reallife_sample_police", "Police stations", UiIcon.ShieldCheck, true, 5),
            Line(t, "reallife_sample_court", "The courthouse", UiIcon.Scales, true, 1),
            Line(t, "reallife_sample_school", "Schools", UiIcon.Cap, true, 9),
            Line(t, "reallife_sample_dealer", "Car dealerships", UiIcon.Car, false, 4),
            Line(t, "reallife_sample_wholesale", "Wholesale stores", UiIcon.ShoppingCart, false, 3),
            Group(t, "common_real_estate", "Real Estate", UiIcon.Tag, UiRole.None, 0, 1, true),
            Group(t, "citymap_category_activities", "Activities", UiIcon.Trophy, UiRole.None, 0, 4, true),
            Group(t, "common_neighborhoods", "Neighborhoods", UiIcon.MapTrifold, UiRole.None, 0, 7, true),
            Group(t, "citymap_category_active_businesses", "Active Businesses", UiIcon.Store, UiRole.None, 2, 31, true),
        };
        if (foreign)
        {
            rows.Insert(4, Group(t, "reallife_sample_players", "Players", UiIcon.People, UiRole.None, 1, 1, false, true));
            rows.Insert(5, Line(t, "reallife_sample_players_line", "Players", UiIcon.People, true, 4, true, t("reallife_map_other_mod", "Other mod")));
        }
        return rows.ToArray();
    }

    /// <summary>The filters' groups on the rail with the lines on in each.</summary>
    public static MapRailItem[] MapRail(UiWords t, bool foreign)
    {
        var rail = new List<MapRailItem>(10)
        {
            new MapRailItem { Icon = UiIcon.Check, Name = t("bizman_status", "Status"), Count = 2 },
            new MapRailItem { Icon = UiIcon.Briefcase, Name = t("citymap_category_jobs", "Jobs") },
            new MapRailItem { Icon = UiIcon.Building, Name = t("citymap_category_rentable", "Buildings for rent"), Count = 3 },
            new MapRailItem { Icon = UiIcon.Star, Name = t("citymap_category_special_buildings", "Special buildings"), Count = 5 },
            new MapRailItem { Icon = UiIcon.Tag, Name = t("common_real_estate", "Real Estate") },
            new MapRailItem { Icon = UiIcon.Trophy, Name = t("citymap_category_activities", "Activities") },
            new MapRailItem { Icon = UiIcon.MapTrifold, Name = t("common_neighborhoods", "Neighborhoods") },
            new MapRailItem { Icon = UiIcon.Store, Name = t("citymap_category_active_businesses", "Active Businesses"), Count = 2 },
        };
        if (foreign)
        {
            rail.Add(new MapRailItem { Icon = UiIcon.People, Name = "Players", Count = 1 });
        }
        return rail.ToArray();
    }

    public static MapChip[] MapChips(UiWords t) => new[]
    {
        new MapChip { Label = t("reallife_map_chip_food", "Food"), Icon = UiIcon.ForkKnife, On = false },
        new MapChip { Label = t("reallife_map_chip_groceries", "Groceries"), Icon = UiIcon.ShoppingBag, On = false },
        new MapChip { Label = t("reallife_map_chip_jobs", "Jobs"), Icon = UiIcon.Briefcase, On = false },
        new MapChip { Label = t("reallife_map_chip_rent", "For rent"), Icon = UiIcon.Key, On = true },
        new MapChip { Label = t("reallife_map_chip_yours", "Your places"), Icon = UiIcon.Star, On = false },
    };

    /// <summary>The place picked on the map: a café the player rents next door to a vacant unit, with how far it is and whether it is open.</summary>
    public static MapPicked MapPickedPlace(UiWords t, bool rival)
    {
        return new MapPicked
        {
            Icon = UiIcon.Coffee, Tint = UiRole.CatFood, Name = "Speedy Bites", Address = "214 5th Avenue, Garment District", Distance = t("reallife_map_distance_road", "{distance} by road").Replace("{distance}", "1.2 km"),
            Chips = rival
                ? new[] { new MapStateChip { Label = t("common_open", "Open"), Tone = UiRole.Ok }, new MapStateChip { Label = "Rival: Onyx Luxury", Tone = UiRole.Warn } }
                : new[] { new MapStateChip { Label = t("common_open", "Open"), Tone = UiRole.Ok }, new MapStateChip { Label = t("buildingresume_rented_by_you", "Rented by you"), Tone = UiRole.Accent } },
            BlockLabel = t("reallife_map_picked", "Picked place"), ClearLabel = t("reallife_map_clear", "Clear"), LocateLabel = t("reallife_map_locate", "Locate"),
            DestinationLabel = t("common_setdestination", "Set destination"), CanDestination = true, CanDetails = true,
            CanBizMan = true, BizManLabel = t("reallife_co_open_bizman", "Open in BizMan"),
        };
    }

    /// <summary>The dock in one of its states.</summary>
    public static MapPanelModel MapFilters(UiWords t, string state)
    {
        bool foreign = state == "foreign";
        var m = new MapPanelModel
        {
            Page = MapPage.Filters, Title = t("reallife_map_filters_title", "Map filters"), Icon = UiIcon.MapTrifold, SearchPlaceholder = t("citymap_map_search_placeholder", "Find a place..."),
            Rail = MapRail(t, foreign), RailShown = 2, Chips = MapChips(t), ShowAll = true, ShowAllLabel = t("reallife_map_show_all", "Show all"), ShowAllCount = t("reallife_map_on_of", "{on} of {total} on").Replace("{on}", "11").Replace("{total}", "31"), ShowAllOn = true,
            Rows = MapFilterRows(t, foreign), Picked = true, Place = MapPickedPlace(t, state == "foreign"),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
            },
            PromptCount = 3,
        };
        switch (state)
        {
            case "search":
                m.Search = "bak";
                m.Rows = new[]
                {
                    Group(t, "citymap_category_active_businesses", "Active Businesses", UiIcon.Store, UiRole.None, 2, 3, false),
                    Line(t, "reallife_sample_bakery", "Bakery", UiIcon.ForkKnife, true, 9),
                    Line(t, "reallife_sample_bakery_wholesale", "Bakery wholesale", UiIcon.Package, false, 2),
                };
                m.Results = new[]
                {
                    new MapResult { Name = "Bakery Rosetta", Address = "12 Mulberry Street" },
                    new MapResult { Name = "Baker & Sons", Address = "88 6th Avenue" },
                    new MapResult { Name = "Bakehouse 41", Address = "41 Spring Street" },
                };
                m.Picked = false;
                m.Chips = new MapChip[0];
                break;
            case "search_none":
                m.Search = "zzq";
                m.Rows = new MapRow[0];
                m.ResultsNone = t("reallife_map_no_place", "No place has that name.");
                m.Empty = t("reallife_map_no_line", "No line on the map has that name. Clear the search to see them all.");
                m.Picked = false;
                m.Chips = new MapChip[0];
                break;
            case "taxi":
                m.Note = t("reallife_map_taxi_note", "Pick a place to ride to. The taxi shows its price and time.");
                m.Picked = true;
                m.Place.DestinationLabel = "Taxi, 12 min, $14";   // the game's own words on its button
                break;
            case "subway":
                m.Page = MapPage.Subway;
                m.Title = t("common_subwaystation", "Subway Station");
                m.Icon = UiIcon.Train;
                m.Note = t("reallife_map_subway_note", "Choose the station to ride to.");
                m.Rail = new MapRailItem[0];
                m.Chips = new MapChip[0];
                m.ShowAll = false;
                m.Picked = false;
                m.Rows = new[]
                {
                    Station(t, "Grand Central", "Midtown", false, true), Station(t, "Times Square", "Midtown", false, false), Station(t, "Penn Station", "Garment District", false, false),
                    Station(t, "Union Square", "Lower Manhattan", false, false), Station(t, "Brooklyn Bridge", "Lower Manhattan", false, false), Station(t, "Columbus Circle", "Uptown", false, false),
                    Station(t, "Central Park North", "Uptown", false, false), Station(t, "Hudson Yards", "Hell's Kitchen", false, false),
                };
                break;
            case "picker":
            case "picker_long":
                bool lng = state == "picker_long";
                m.Page = MapPage.Picker;
                m.Title = t("reallife_map_choose_title", "Choose a place");
                m.Icon = UiIcon.MapPin;
                m.Note = "Pick a stop for the bus to leave you at.";   // the owner's sentence about its choice
                m.Rail = new MapRailItem[0];
                m.Chips = new MapChip[0];
                m.ShowAll = false;
                m.Picked = false;
                m.FooterLabel = t("common_cancel", "Cancel");
                m.Rows = new[]
                {
                    Choice(t, lng ? "Broadway and West 42nd Street, Times Square northbound platform" : "Broadway and 42nd", lng ? "Midtown, 4 stops from here" : "Midtown", false),
                    Choice(t, "5th Avenue and 34th", "Garment District", false), Choice(t, "6th Avenue and 23rd", "Lower Manhattan", false),
                    Choice(t, "Hudson Street", "Too far from the line", true), Choice(t, "Canal Street", "Lower Manhattan", false),
                    Choice(t, "Madison Avenue and 59th", "Uptown", false), Choice(t, "Lexington and 86th", "Uptown", false), Choice(t, "Tenth Avenue and 30th", "Hell's Kitchen", false),
                };
                break;
        }
        return m;
    }

    private static MapRow Station(UiWords t, string name, string district, bool off, bool here) => new MapRow
    {
        Kind = MapRowKind.Station, Name = name, Note = district, Icon = UiIcon.Train, Off = off || here, Chip = here ? t("reallife_map_here", "You are here") : null
    };

    private static MapRow Choice(UiWords t, string name, string note, bool off) => new MapRow
    {
        Kind = MapRowKind.Choice, Name = name, Note = note, Icon = UiIcon.MapPin, Off = off
    };

    /// <summary>The dock over the map with the status bar under it, as the HUD host stands it.</summary>
    public static void MapFiltersScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state) =>
        WithBar(Over(HudBar(t, false, false, false)), () => UiMapFilters.Lay(s, MapFilters(t, state), f, dark, k));
}
