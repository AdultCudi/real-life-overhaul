using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's Persona, Rivals and Market Insider, drawn on the kit over the game's own (Scripts/Ui/Persona, Rivals, Market).
        static partial void AddPersona(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("persona", UiTemplate.AppWindow, "PersonaScreen", new[]
                {
                    "personalgoal_bank_balance", "personalgoal_business_weekly_income", "personalgoal_minemployees", "personalgoal_realestate",
                    "personalgoal_taxi_rides", "personalgoal_taxes_paid", "personalgoal_dailycustomers", "personalgoal_items_in_stock", "personalgoal_casinowin",
                    "personalgoal_owned_vehicles", "personalgoal_employee_max_skill", "personalgoal_age", "persona_characterinfo_businesses",
                    "persona_characterinfo_employees", "reallife_ui_persona_all_moods", "reallife_ui_persona_next_goals", "reallife_ui_persona_goals_done",
                    "reallife_ui_persona_all_goals",
                    "smartphone_persona", "reallife_ui_persona_overview", "reallife_ui_persona_needs", "persona_education", "reallife_ui_persona_goals",
                    "common_years_amount", "common_number_of_businesses", "reallife_ui_persona_staff", "persona_characterinfo_networth", "common_weekly_income",
                    "persona_personal_wealth", "persona_cash", "common_investments", "persona_assets", "econoview_loans", "reallife_ui_persona_wealth_empty",
                    "common_day_number", "reallife_ui_persona_income_empty", "reallife_ui_persona_income_empty_body", "common_energy", "common_hunger",
                    "common_happiness",
                    "reallife_ui_persona_progress", "reallife_ui_persona_modifiers", "reallife_ui_persona_change",
                    "reallife_ui_persona_cause", "reallife_ui_persona_lasts", "reallife_ui_persona_net", "reallife_ui_persona_ongoing", "reallife_ui_persona_no_modifiers",
                    "reallife_ui_persona_no_modifiers_body", "common_days_left", "common_hours_left", "reallife_ui_persona_course",
                    "reallife_ui_persona_kind", "bizman_status", "common_businessschool", "persona_education_completed", "persona_education_not_started",
                    "persona_education_completed_percentage", "reallife_ui_persona_no_courses", "reallife_ui_persona_no_courses_body", "reallife_ui_persona_goal",
                    "persona_personalgoal_current_goals", "persona_personalgoal_steam_achievements", "reallife_ui_persona_bronze",
                    "reallife_ui_persona_silver", "reallife_ui_persona_gold", "reallife_ui_persona_no_goals", "reallife_ui_persona_no_goals_body",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_staff_prompt_tab", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Market),
                new UiScreenDef("rivals", UiTemplate.AppWindow, "RivalsScreen", new[]
                {
                    "rivals_defense_icons_price_reduction_description", "rivals_defense_icons_hire_best_employees_description",
                    "ba:businesstype_fastfoodrestaurant", "ba:businesstype_coffeeshop", "ba:businesstype_clothingstore", "ba:businesstype_supermarket",
                    "ba:businesstype_jewelrystore", "ba:businesstype_headquarters", "ba:businesstype_florist", "ba:businesstype_electronicsstore",
                    "ba:businesstype_bookstore", "ba:businesstype_warehouse",
                    "smartphone_rivals", "rivals_leaderboard", "common_businesses", "rivals_real_estate_properties", "rivals_overview", "common_years_amount",
                    "reallife_ui_rivals_defeated", "reallife_ui_rivals_you", "rivals_defense_icons_active_rivalry_title", "common_weekly_income",
                    "reallife_ui_rivals_strength",
                    "open_in_contacts", "common_name", "reallife_ui_rivals_week", "reallife_ui_rivals_none", "reallife_ui_rivals_none_body", "common_business_name",
                    "common_business_type", "common_address", "reallife_ui_rivals_no_businesses", "reallife_ui_rivals_no_businesses_body", "common_show_on_map",
                    "reallife_ui_rivals_open", "common_setdestination", "common_neighborhood", "common_building_type", "bizman_building_total_size",
                    "real_estate_estimated_value", "common_price", "reallife_ui_market_not_for_sale", "reallife_ui_rivals_no_buildings",
                    "reallife_ui_rivals_no_buildings_body",
                    "rivals_business_development", "common_day_number", "reallife_ui_rivals_chart_empty", "reallife_ui_rivals_chart_empty_body",
                    "reallife_ui_rivals_rivalry",
                    "rivals_defense_icons_hire_best_employees_title", "rivals_defense_icons_price_reduction_title", "rivals_defense_icons_product_shortage_title",
                    "rivals_defense_icons_low_demand_title", "ba:buildingtype_retail", "ba:buildingtype_office", "ba:buildingtype_warehouse",
                    "ba:buildingtype_residential", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_staff_prompt_tab", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Market),
                new UiScreenDef("market_insider", UiTemplate.AppWindow, "MarketScreen", new[]
                {
                    "market_insider_average_building_price", "ba:itemname_cupofcoffee", "ba:itemname_burger", "ba:itemname_croissant", "ba:itemname_cheapflower",
                    "ba:itemname_laptop", "ba:itemname_hotdog", "ba:itemname_classicphone", "ba:itemname_bottleofwine", "ba:itemname_donut",
                    "ba:itemname_sofaluxury", "ba:itemname_cheapgift", "ba:itemname_paper", "marketeventtype_hype_description",
                    "marketeventtype_businessopened_description",
                    "reallife_ui_market_app", "market_insider_citizen_data", "socialclass_working", "socialclass_middle", "socialclass_upper",
                    "market_insider_real_estate_data", "market_insider_total_buildings", "phone_view_on_map_button", "common_market_demands", "common_real_estate",
                    "market_insider_show_only_for_sale", "reallife_ui_market_all_types", "reallife_ui_market_all_buildings", "reallife_ui_staff_chosen", "common_search",
                    "reallife_ui_staff_count", "reallife_ui_market_none_match", "reallife_ui_market_none_match_body", "reallife_ui_staff_clear", "common_product",
                    "marketinsider_demand", "common_events", "marketinsider_lowest_market_price", "marketinsider_import_price_index", "marketinsider_providers",
                    "reallife_ui_market_no_seller", "reallife_ui_market_service", "reallife_ui_market_no_demand", "reallife_ui_market_no_demand_body", "common_address",
                    "common_building_type", "bizman_building_total_size", "real_estate_estimated_value", "common_price", "reallife_ui_market_not_for_sale",
                    "reallife_ui_market_none_for_sale", "reallife_ui_market_none_for_sale_body", "reallife_ui_market_no_buildings",
                    "reallife_ui_market_no_buildings_body",
                    "common_day_number", "reallife_ui_market_no_events", "reallife_ui_market_no_events_body", "reallife_ui_market_prompt_district",
                    "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity", "ba:neighborhood_lowermanhattan",
                    "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons",
                    "marketeventtype_hype", "marketeventtype_businessopened", "marketeventtype_productshortage", "marketeventtype_businessclosed",
                    "ba:buildingtype_retail", "ba:buildingtype_office", "ba:buildingtype_warehouse", "ba:buildingtype_residential",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Market),
            });
        }
    }
}
