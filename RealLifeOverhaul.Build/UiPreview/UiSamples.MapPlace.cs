using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    /// <summary>The states of the place card the previews and the checks draw.</summary>
    public static readonly string[] MapPlaceStates = { "overview", "reviews", "business", "unrated", "yours", "hover" };

    /// <summary>The district view's layers the previews and the checks draw.</summary>
    public static readonly string[] MapDistrictStates = { "rent", "wealth", "crime", "no_month" };

    // ------------------------------------------------------------------------------------------------- the place card

    public static MapPlaceModel MapPlace(UiWords t, string state)
    {
        bool yours = state == "yours", unrated = state == "unrated";
        var m = new MapPlaceModel
        {
            Title = t("reallife_map_place_title", "Place card"), Icon = UiIcon.Store, Glyph = unrated ? UiIcon.Building : UiIcon.ForkKnife, Tint = unrated ? UiRole.CatOffice : UiRole.CatFood,
            Name = unrated ? "Hollis & Reed, Accountants" : "Speedy Bites",
            StarHalves = unrated ? 0 : 9, Rating = unrated ? null : "4.3", Count = unrated ? null : "(212)", Kind = unrated ? "Office" : "Fast food",
            Price = unrated ? null : "$$",
            Chips = unrated
                ? new[] { new MapStateChip { Label = t("common_closed", "Closed"), Tone = UiRole.Danger } }
                : new[]
                {
                    new MapStateChip { Label = "Open until 23:00", Tone = UiRole.Ok },
                    yours ? new MapStateChip { Label = t("buildingresume_rented_by_you", "Rented by you"), Tone = UiRole.Accent } : new MapStateChip { Label = "Rival: Onyx Luxury", Tone = UiRole.Warn },
                },
            DirectionsLabel = t("reallife_map_directions", "Directions"), SaveLabel = t("reallife_map_save", "Save"), SavedLabel = t("reallife_map_saved", "Saved"),
            DestinationLabel = t("common_setdestination", "Set destination"), Saved = yours, RouteShown = true, CanDestination = true,
            CanBizMan = true, BizManLabel = t("reallife_co_open_bizman", "Open in BizMan"),
            Tabs = new[] { t("reallife_map_tab_overview", "Overview"), t("reallife_map_tab_reviews", "Reviews"), t("reallife_map_tab_business", "Business") },
            Tab = state == "reviews" ? 1 : state == "business" ? 2 : 0,
            Rows = new[]
            {
                new MapPlaceRow { Label = t("reallife_map_row_address", "Address"), Value = "214 5th Avenue, Garment District" },
                new MapPlaceRow { Label = t("reallife_map_row_distance", "Distance"), Value = t("reallife_map_distance_road", "{distance} by road").Replace("{distance}", "1.2 km") },
                new MapPlaceRow { Label = t("reallife_map_row_hours", "Hours today"), Value = unrated ? t("common_closed", "Closed") : "07:00 to 23:00", Ink = UiRole.None },
                new MapPlaceRow { Label = t("reallife_map_row_next", "Next door"), Value = t("reallife_map_row_for_rent", "For rent, {rent} per {unit} a day").Replace("{rent}", "$96").Replace("{unit}", "m2"), Link = true },
            },
            BusyTitle = t("reallife_map_busy", "Busy times"), BusyNote = unrated ? null : t("reallife_map_busier", "Busier than usual"), BusyUp = !unrated,
            Busy = unrated ? new int[0] : new[] { 12, 22, 40, 62, 48, 30, 58, 74, 88, 64, 46, 34, 20 }, BusyFrom = 9, BusyNow = 6,
            PeopleTitle = t("reallife_map_people", "What people say"), PeopleAll = unrated ? null : t("reallife_map_all_reviews", "All {count}").Replace("{count}", "212"),
            Reviews = unrated ? new MapReview[0] : new[]
            {
                new MapReview { Name = "Jonas R.", Hue = 0.08f, Halves = 9, When = Days(t, 2), Text = t("reallife_map_review_high_wait_0", "Quick service even when it was busy.") },
                new MapReview { Name = "Dana K.", Hue = 0.58f, Halves = 7, When = Days(t, 5), Text = t("reallife_map_review_mid_price_1", "Good, but you pay for it.") },
                new MapReview { Name = "Mei L.", Hue = 0.33f, Halves = 10, When = Days(t, 9), Text = t("reallife_map_review_high_clean_1", "Clean, bright and easy to like.") },
                new MapReview { Name = "Omar S.", Hue = 0.80f, Halves = 5, When = Days(t, 12), Text = t("reallife_map_review_low_wait_1", "The queue never moved so I gave up and left.") },
                new MapReview { Name = "Lena P.", Hue = 0.18f, Halves = 8, When = Days(t, 15), Text = t("reallife_map_review_mid_mixed_0", "An ordinary visit with nothing to complain about.") },
            },
            ReviewsEmpty = t("reallife_map_no_reviews", "No one has reviewed this place yet. Reviews come from residents who shop here."),
            BusinessTitle = t("reallife_map_business", "Business"),
            Business = new[]
            {
                new MapPlaceRow { Label = t("reallife_map_row_owner", "Owner"), Value = yours ? t("reallife_map_you", "You") : "Onyx Luxury" },
                new MapPlaceRow { Label = t("reallife_map_row_type", "Type"), Value = unrated ? "Office" : "Fast food" },
                new MapPlaceRow { Label = t("common_monday", "Monday"), Value = "07:00 to 23:00" }, new MapPlaceRow { Label = t("common_tuesday", "Tuesday"), Value = "07:00 to 23:00" },
                new MapPlaceRow { Label = t("common_wednesday", "Wednesday"), Value = "07:00 to 23:00" }, new MapPlaceRow { Label = t("common_thursday", "Thursday"), Value = "07:00 to 23:00" },
                new MapPlaceRow { Label = t("common_friday", "Friday"), Value = "07:00 to 01:00" }, new MapPlaceRow { Label = t("common_saturday", "Saturday"), Value = "09:00 to 01:00" },
                new MapPlaceRow { Label = t("common_sunday", "Sunday"), Value = t("common_closed", "Closed"), Ink = UiRole.Muted },
            },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_map_back_filters", "Map filters") },
            },
            PromptCount = 3,
        };
        return m;
    }

    private static string Days(UiWords t, int days) => t("reallife_map_ago_days", "{days} days ago").Replace("{days}", days.ToString());

    public static MapRouteModel MapRoute(UiWords t) => new MapRouteModel
    {
        Options = new[]
        {
            new MapRouteOption { Icon = UiIcon.Walk, Minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "14"), Sub = t("reallife_map_route_walk", "Walk") },
            new MapRouteOption { Icon = UiIcon.Taxi, Minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "4"), Sub = t("reallife_map_route_taxi", "Taxi, {amount}").Replace("{amount}", "$14") },
            new MapRouteOption { Icon = UiIcon.Train, Minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "9"), Sub = t("reallife_map_route_subway", "Subway, {amount}").Replace("{amount}", "$3") },
            new MapRouteOption { Icon = UiIcon.Car, Minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "5"), Sub = t("reallife_map_route_car", "Your car") },
        },
        Chosen = 1, GoLabel = t("reallife_map_go", "Go"), CanGo = true,
    };

    public static MapLayersModel MapLayers(UiWords t) => new MapLayersModel
    {
        Title = t("reallife_map_layers", "Layers"), HideAllLabel = t("reallife_map_hide_all", "Hide all"),
        Layers = new[]
        {
            new MapLayer { Icon = UiIcon.Key, Name = t("buildingresume_rented_by_you", "Rented by you"), On = true },
            new MapLayer { Icon = UiIcon.Briefcase, Name = t("citymap_category_jobs", "Jobs"), On = false },
            new MapLayer { Icon = UiIcon.Building, Name = t("citymap_category_rentable", "Buildings for rent"), On = true },
            new MapLayer { Icon = UiIcon.Store, Name = t("citymap_category_active_businesses", "Active Businesses"), On = true },
            new MapLayer { Icon = UiIcon.Train, Name = t("common_subwaystation", "Subway Station"), On = false },
            new MapLayer { Icon = UiIcon.Clock, Name = t("citymap_filter_hide_closed", "Hide closed businesses"), On = true },
        },
    };

    public static MapButtonsModel MapButtons(UiWords t, bool tilted) => new MapButtonsModel { North = t("reallife_map_north", "N"), Tilt = tilted ? "3D" : "2D", Tilted = tilted };

    /// <summary>The place card over the map with the route strip, the Layers panel and the map's buttons, the status bar under them.</summary>
    public static void MapPlaceScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state, bool routeTag = false) =>
        WithBar(Over(HudBar(t, false, false, false)), () =>
        {
            UiMap.Lay(s, new MapScreenModel
            {
                Place = MapPlace(t, state), Route = state == "unrated" ? null : MapRoute(t), Layers = MapLayers(t), Buttons = MapButtons(t, true),
                HasLabel = state == "hover", Label = MapNextLabel(t),
            }, f, dark, k);
            if (routeTag && state != "unrated")
            {
                // The label on the route's line, as the HUD's host keeps it over the middle of the line (the preview only).
                string minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "14");
                UiMapChrome.RouteTagSize(minutes, "1.2 km", out float w, out float h);
                UiMapChrome.RouteTag(s, UiAt.Screen, 1013f - w / 2f, 592f - h / 2f, w, h, true, minutes, "1.2 km");
            }
        });

    // The label over the unit next door, where the unit stands on the map, while the pointer rests on the card's row for it.
    public static MapLabel MapNextLabel(UiWords t) => new MapLabel
    {
        Icon = UiIcon.Key, Title = t("reallife_map_next_label", "For rent next door"), X = 900f, Y = 560f,
        Figure = t("reallife_map_next_figure", "{rent} per {unit} a day").Replace("{rent}", "$96").Replace("{unit}", "m2"),
    };

    // ------------------------------------------------------------------------------------------------- the district view

    private static string DistrictTitle(UiWords t, string id) =>
        t("reallife_map_district_title", "Districts, {layer}").Replace("{layer}", t("reallife_dv_row_" + id, id));

    private static MapRailItem[] MapLayerRail(UiWords t) => new[]
    {
        new MapRailItem { Icon = UiIcon.Wallet, Name = t("reallife_dv_row_wealth", "Wealth") }, new MapRailItem { Icon = UiIcon.Coin, Name = t("reallife_dv_row_rent", "Rent") },
        new MapRailItem { Icon = UiIcon.Home, Name = t("reallife_dv_row_property", "Property") }, new MapRailItem { Icon = UiIcon.People, Name = t("reallife_dv_row_footfall", "Footfall") },
        new MapRailItem { Icon = UiIcon.ShieldCheck, Name = t("reallife_dv_row_crime", "Crime") }, new MapRailItem { Icon = UiIcon.Heart, Name = t("reallife_dv_row_standing", "Standing") },
        new MapRailItem { Icon = UiIcon.Lightning, Name = t("reallife_dv_row_supply", "Supply") }, new MapRailItem { Icon = UiIcon.Globe, Name = t("reallife_dv_row_internet", "Internet") },
        new MapRailItem { Icon = UiIcon.Store, Name = t("reallife_dv_row_competition", "Competition") }, new MapRailItem { Icon = UiIcon.Flag, Name = t("reallife_dv_row_council", "Council") },
    };

    // tone: Ok or Danger where the way it moved is good or bad for the player (a crime count falling), None where a rise is neither (a rent)
    private static MapDistrictRow District(string name, int step, string figure, string month, int sign, UiRole tone, string empty, bool chosen = false) => new MapDistrictRow
    {
        Name = name, Swatch = step < 0 ? UiRole.None : UiTokens.Ramp(step), Figure = figure, Month = month, MonthSign = sign, MonthTone = tone, Empty = empty, Chosen = chosen
    };

    public static MapDistrictModel MapDistrict(UiWords t, string state)
    {
        var m = new MapDistrictModel
        {
            FiltersLabel = t("reallife_map_filters_title", "Map filters"), Layers = MapLayerRail(t), Layer = state == "wealth" ? 0 : state == "crime" ? 4 : 1,
            AverageTitle = t("reallife_map_average", "City average"), ShowLabel = t("reallife_map_show_on_map", "Show on map"),
            ColDistrict = t("reallife_map_col_district", "District"), ColMonth = t("reallife_map_col_month", "Month"), ColEmpty = t("reallife_map_col_empty", "Empty"),
            HasChosen = true, ChosenName = "Midtown", ChosenRank = t("reallife_map_rank", "Rank {n} of {m}").Replace("{n}", "1").Replace("{m}", "7"),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_map_hide_view", "Hide view") },
            },
            PromptCount = 3,
        };
        switch (state)
        {
            case "wealth":
                m.Title = DistrictTitle(t, "wealth");
                m.Icon = UiIcon.Wallet;
                m.About = t("reallife_dv_about_wealth", "How well off the people who live there are, against the middle of the city.");
                m.Average = new[]
                {
                    new UiInfoRow { Label = t("reallife_map_avg_wealth", "Income level"), Value = "101" }, new UiInfoRow { Label = t("reallife_map_highest", "Highest"), Value = "Uptown, 124" },
                    new UiInfoRow { Label = t("reallife_map_lowest", "Lowest"), Value = "Industry City, 71" }, new UiInfoRow { Label = t("reallife_map_change", "Change this month"), Value = "+0.6%", Ink = UiRole.OkText },
                };
                m.ChartTitle = t("reallife_map_classes", "Classes");
                m.ChartCentre = "18.4K";
                m.ChartCentreSub = t("reallife_map_residents_lower", "people");
                m.ChartLabels = new[] { t("reallife_map_class_working", "Working"), t("reallife_map_class_middle", "Middle"), t("reallife_map_class_upper", "Upper") };
                m.ChartValues = new[] { "42%", "41%", "17%" };
                m.ChartShares = new[] { 0.42f, 0.41f, 0.17f };
                m.LegendTitle = t("reallife_map_key", "Map legend");
                m.ScaleLabel = t("reallife_dv_row_wealth", "Wealth");
                m.ScaleLow = "71";
                m.ScaleHigh = "124";
                m.TableTitle = t("reallife_map_ranked_wealth", "Ranked by wealth");
                m.ColFigure = t("reallife_map_col_wealth", "Level");
                m.Chosen = new[] { new UiInfoRow { Label = t("reallife_map_avg_wealth", "Income level"), Value = "118" }, new UiInfoRow { Label = t("reallife_map_residents", "Residents"), Value = "18,400" } };
                m.Rows = new[]
                {
                    District("Uptown", 4, "124", "+0.9%", 1, UiRole.Ok, "6.4%"), District("Midtown", 3, "118", "+0.6%", 1, UiRole.Ok, "5.1%", true), District("Lower Manhattan", 3, "112", "-0.2%", -1, UiRole.Danger, "7.9%"),
                    District("Murray Hill", 2, "103", "+0.4%", 1, UiRole.Ok, "6.8%"), District("Garment District", 1, "96", "+0.1%", 1, UiRole.Ok, "9.2%"), District("Hell's Kitchen", 1, "88", "-0.5%", -1, UiRole.Danger, "11.7%"),
                    District("Industry City", 0, "71", "+0.0%", 0, UiRole.Ok, "16.4%"),
                };
                break;
            case "crime":
                m.Title = DistrictTitle(t, "crime");
                m.Icon = UiIcon.ShieldCheck;
                m.About = t("reallife_dv_about_crime", "Crimes reported there over the last four weeks.");
                m.Average = new[]
                {
                    new UiInfoRow { Label = t("reallife_map_avg_crime", "Reports in 28 days"), Value = "37" }, new UiInfoRow { Label = t("reallife_map_highest", "Highest"), Value = "Hell's Kitchen, 11" },
                    new UiInfoRow { Label = t("reallife_map_lowest", "Lowest"), Value = "Uptown, 1" }, new UiInfoRow { Label = t("reallife_map_change", "Change this month"), Value = "-8.0%", Ink = UiRole.OkText },
                };
                m.LegendTitle = t("reallife_map_key", "Map legend");
                m.ScaleLabel = t("reallife_dv_row_crime", "Crime");
                m.ScaleLow = "0";
                m.ScaleHigh = "10";
                m.TableTitle = t("reallife_map_ranked_crime", "Ranked by crime");
                m.ColFigure = t("reallife_map_col_crime", "Reports");
                m.Chosen = new[] { new UiInfoRow { Label = t("reallife_map_avg_crime", "Reports in 28 days"), Value = "4" }, new UiInfoRow { Label = t("reallife_map_residents", "Residents"), Value = "18,400" } };
                m.Rows = new[]
                {
                    District("Hell's Kitchen", 4, "11", "-2", -1, UiRole.Ok, null), District("Industry City", 4, "10", "+1", 1, UiRole.Danger, null), District("Garment District", 2, "6", "-1", -1, UiRole.Ok, null),
                    District("Lower Manhattan", 2, "5", "0", 0, UiRole.Ok, null), District("Midtown", 1, "4", "-3", -1, UiRole.Ok, null, true), District("Murray Hill", 0, "2", "0", 0, UiRole.Ok, null),
                    District("Uptown", 0, "1", "-1", -1, UiRole.Ok, null),
                };
                break;
            case "no_month":
                m.Title = DistrictTitle(t, "rent");
                m.Icon = UiIcon.Coin;
                FillRent(t, m);
                m.Average[4] = new UiInfoRow { Label = t("reallife_map_change", "Change this month"), Value = t("reallife_map_no_change", "No change yet"), Ink = UiRole.Muted };
                for (int i = 0; i < m.Rows.Length; i++)
                {
                    m.Rows[i].Month = null;
                    m.Rows[i].MonthSign = 0;
                }
                break;
            default:
                m.Title = DistrictTitle(t, "rent");
                m.Icon = UiIcon.Coin;
                FillRent(t, m);
                break;
        }
        for (int i = 0; i < m.Rows.Length; i++)
        {
            if (m.Rows[i].Chosen)
            {
                m.ChosenRank = t("reallife_map_rank", "Rank {n} of {m}").Replace("{n}", (i + 1).ToString()).Replace("{m}", m.Rows.Length.ToString());
            }
        }
        return m;
    }

    private static void FillRent(UiWords t, MapDistrictModel m)
    {
        m.About = t("reallife_dv_about_rent", "What shops, offices and warehouses are let for, a square metre a day.");
        m.Average = new[]
        {
            new UiInfoRow { Label = t("reallife_map_avg_rent", "Mean rent"), Value = "$118" }, new UiInfoRow { Label = t("reallife_map_highest", "Highest"), Value = "Midtown, $212" },
            new UiInfoRow { Label = t("reallife_map_lowest", "Lowest"), Value = "Industry City, $52" }, new UiInfoRow { Label = t("reallife_map_empty", "Space standing empty"), Value = "8.4%" },
            new UiInfoRow { Label = t("reallife_map_change", "Change this month"), Value = "+1.2%", Ink = UiRole.WarnText },
        };
        m.ChartTitle = t("reallife_map_for_rent_by_type", "For rent by type");
        m.ChartCentre = "104";
        m.ChartCentreSub = t("reallife_map_for_rent", "for rent");
        m.ChartLabels = new[] { t("reallife_map_kind_retail", "Retail"), t("reallife_map_kind_office", "Office"), t("reallife_map_kind_home", "Homes"), t("reallife_map_kind_warehouse", "Warehouse") };
        m.ChartValues = new[] { "38%", "27%", "22%", "13%" };
        m.ChartShares = new[] { 0.38f, 0.27f, 0.22f, 0.13f };
        m.LegendTitle = t("reallife_map_key", "Map legend");
        m.ScaleLabel = t("reallife_dv_row_rent", "Rents");
        m.ScaleLow = "$50";
        m.ScaleHigh = "$210";
        m.TableTitle = t("reallife_map_ranked_rent", "Ranked by rent");
        m.ColFigure = t("reallife_map_col_rent", "Per {unit}").Replace("{unit}", "m2");
        m.Chosen = new[]
        {
            new UiInfoRow { Label = t("reallife_map_avg_rent", "Mean rent"), Value = "$212" }, new UiInfoRow { Label = t("reallife_map_residents", "Residents"), Value = "18,400" },
            new UiInfoRow { Label = t("reallife_map_businesses", "Businesses"), Value = "612" },
        };
        m.Rows = new[]
        {
            District("Midtown", 4, "$212", "+1.8%", 1, UiRole.None, "5.1%", true), District("Uptown", 4, "$188", "+0.9%", 1, UiRole.None, "6.4%"), District("Lower Manhattan", 3, "$176", "-0.4%", -1, UiRole.None, "7.9%"),
            District("Murray Hill", 2, "$152", "+1.1%", 1, UiRole.None, "6.8%"), District("Garment District", 1, "$118", "+2.6%", 1, UiRole.None, "9.2%"), District("Hell's Kitchen", 1, "$84", "-1.3%", -1, UiRole.None, "11.7%"),
            District("Industry City", 0, "$52", "+0.2%", 1, UiRole.None, "16.4%"),
        };
    }

    /// <summary>The district view over the map with the status bar under it and the map's buttons and layers.</summary>
    public static void MapDistrictScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state, bool tags = false) =>
        WithBar(Over(HudBar(t, false, false, false)), () =>
        {
            MapDistrictModel m = MapDistrict(t, state);
            UiMap.Lay(s, new MapScreenModel { District = m, Buttons = MapButtons(t, true) }, f, dark, k);
            if (tags)
            {
                MapTags(s, m);   // the names over the districts, as the HUD's host keeps them beside each district's middle (the preview only)
            }
        });

    // Where seven districts stand on the sample's map, in px of the layout.
    private static readonly float[] MapTagAt = { 1180f, 250f, 800f, 300f, 1420f, 470f, 760f, 560f, 1050f, 650f, 1500f, 780f, 880f, 840f };

    private static void MapTags(UiScene s, MapDistrictModel m)
    {
        for (int i = 0; i < m.Rows.Length && i * 2 + 1 < MapTagAt.Length; i++)
        {
            MapDistrictRow d = m.Rows[i];
            UiMapDistrict.TagSize(d.Name, d.Figure, out float w, out float h);
            UiMapDistrict.Tag(s, UiAt.Screen, MapTagAt[i * 2], MapTagAt[i * 2 + 1], w, h, d.Name, d.Figure, d.Swatch, d.Chosen);
        }
    }
}
