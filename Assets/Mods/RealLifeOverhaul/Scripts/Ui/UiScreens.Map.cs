using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The words the city map's panels are laid out with in every state: the filters and the subway list, the place card with its route strip, the district view and the
        // choice of a place, and the status bar under them. (Made when asked: the screens' list is gathered while the class is first set up, before this file's fields are.)
        private static string[] MapKeys => new[]
        {
            "bizman_status", "reallife_co_open_bizman", "buildingresume_rented_by_you", "citymap_category_active_businesses", "citymap_category_activities", "citymap_category_jobs",
            "bizman_market_value", "bizman_estimated_valuation", "reallife_ui_biz_valuation", "reallife_ui_biz_value_sold", "reallife_ui_world_area",
            "reallife_ui_world_capacity", "reallife_ui_world_traffic",
            "citymap_category_rentable", "citymap_category_special_buildings", "citymap_filter_hide_closed", "citymap_map_search_placeholder", "common_cancel",
            "common_close", "common_closed", "common_friday", "common_monday", "common_neighborhoods", "common_open", "common_real_estate", "common_saturday",
            "common_setdestination", "common_subwaystation", "common_sunday", "common_thursday", "common_tuesday", "common_wednesday", "reallife_dv_about_crime",
            "reallife_dv_about_rent", "reallife_dv_about_wealth", "reallife_dv_row_competition", "reallife_dv_row_council", "reallife_dv_row_crime",
            "reallife_dv_row_footfall", "reallife_dv_row_internet", "reallife_dv_row_property", "reallife_dv_row_rent", "reallife_dv_row_standing",
            "reallife_dv_row_supply", "reallife_dv_row_wealth", "reallife_map_ago_days", "reallife_map_all_reviews", "reallife_map_average", "reallife_map_avg_crime",
            "reallife_map_avg_rent", "reallife_map_avg_wealth", "reallife_map_back_filters", "reallife_map_busier", "reallife_map_business", "reallife_map_businesses",
            "reallife_map_busy", "reallife_map_change", "reallife_map_chip_food", "reallife_map_chip_fuel", "reallife_map_chip_groceries", "reallife_map_chip_jobs", "reallife_map_chip_rent",
            "reallife_map_chip_yours", "reallife_map_choose_title", "reallife_map_class_middle", "reallife_map_class_upper", "reallife_map_class_working",
            "reallife_map_classes", "reallife_map_clear", "reallife_map_col_crime", "reallife_map_col_district", "reallife_map_col_empty", "reallife_map_col_month",
            "reallife_map_col_rent", "reallife_map_col_wealth", "reallife_map_directions", "reallife_map_distance_road", "reallife_map_district_title",
            "reallife_map_empty", "reallife_map_filters_title", "reallife_map_for_rent", "reallife_map_for_rent_by_type", "reallife_map_go", "reallife_map_here",
            "reallife_map_hide_all", "reallife_map_hide_view", "reallife_map_hover", "reallife_map_highest", "reallife_map_key", "reallife_map_kind_home", "reallife_map_kind_office",
            "reallife_map_kind_retail", "reallife_map_kind_warehouse", "reallife_map_layers", "reallife_map_locate", "reallife_map_more_places", "reallife_map_lowest", "reallife_map_next_figure",
            "reallife_map_next_label", "reallife_map_no_change", "reallife_map_no_line", "reallife_map_no_place", "reallife_map_no_reviews", "reallife_map_north",
            "reallife_map_on_of", "reallife_map_other_mod", "reallife_map_people", "reallife_map_picked", "reallife_map_place_title", "reallife_map_rank",
            "reallife_map_ranked_crime", "reallife_map_ranked_rent", "reallife_map_ranked_wealth", "reallife_map_residents", "reallife_map_residents_lower",
            "reallife_map_review_high_clean_1", "reallife_map_review_high_wait_0", "reallife_map_review_low_wait_1", "reallife_map_review_mid_mixed_0",
            "reallife_map_review_mid_price_1", "reallife_map_route_car", "reallife_map_route_subway", "reallife_map_route_taxi", "reallife_map_route_walk",
            "reallife_map_row_address", "reallife_map_row_distance", "reallife_map_row_for_rent", "reallife_map_row_hours", "reallife_map_row_next", "reallife_map_row_owner",
            "reallife_map_row_type", "reallife_map_save", "reallife_map_save_full", "reallife_map_saved", "reallife_map_show_all", "reallife_map_show_on_map", "reallife_map_subway_note",
            "reallife_map_tab_business", "reallife_map_tab_overview", "reallife_map_tab_reviews", "reallife_map_taxi_note",
            // the district block's sentences, as the old card says them
            "reallife_dv_card_bears", "reallife_dv_card_businesses", "reallife_dv_card_classes", "reallife_dv_card_crime", "reallife_dv_card_demand",
            "reallife_dv_card_electorate", "reallife_dv_card_fibre_dug", "reallife_dv_card_fibre_open", "reallife_dv_card_footfall_down",
            "reallife_dv_card_footfall_up", "reallife_dv_card_home_rent", "reallife_dv_card_income", "reallife_dv_card_income_start",
            "reallife_dv_card_internet", "reallife_dv_card_internet_down", "reallife_dv_card_known", "reallife_dv_card_opinion",
            "reallife_dv_card_parking", "reallife_dv_card_rent", "reallife_dv_card_result", "reallife_dv_card_sites", "reallife_dv_card_supply",
            "reallife_dv_card_supply_cut", "reallife_dv_card_supply_none", "reallife_dv_card_traffic", "reallife_dv_card_types",
            "reallife_dv_card_value",
            "ba:buildingresume_taxi_travel", "bizman_factory_production_time_per_product_label", "reallife_map_you", "reallife_nav_minutes",
            "reallife_season_name_autumn", "topbar_date_format", "common_monday", "common_tuesday", "common_wednesday", "common_friday",
            "reallife_ui_hud_weather_sunny", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_shell_all_apps"
        };

        // The city map's panels over the game's own windows (Scripts/Ui/MapDock, MapKits): its filters with the picked place, the place card, the route strip, the layers and
        // the map's buttons; its list of subway stations; the district view's page; and the choice of a place. Each has its own switch.
        static partial void AddMap(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("map_filters", UiTemplate.Hud, "MapLive", MapKeys).Switch(UiScreenSwitches.Map),
                new UiScreenDef("map_subway", UiTemplate.Hud, "MapLive", MapKeys).Switch(UiScreenSwitches.Map),
                new UiScreenDef("map_district", UiTemplate.Hud, "MapLive", MapKeys).Switch(UiScreenSwitches.Map),
                new UiScreenDef("map_picker", UiTemplate.Hud, "MapLive", MapKeys).Switch(UiScreenSwitches.Map),
            });
        }
    }
}
