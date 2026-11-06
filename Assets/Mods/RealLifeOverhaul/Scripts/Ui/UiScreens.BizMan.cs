using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's BizMan, its portfolio and a place's page, drawn on the kit over the game's own.
        static partial void AddBizMan(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("bizman", UiTemplate.AppWindow, "BizManScreen", new[]
                {
                    "ba:transaction_deposit_label", "bizman_alerts", "bizman_avg_daily_income", "bizman_browse_store_inventory", "bizman_building_total_size",
                    "bizman_buy_building", "bizman_customers_capacity", "bizman_deliveries_stock", "bizman_deliveries_urgent_order",
                    "bizman_delivery_contract_cancel_contract", "bizman_delivery_contracts", "bizman_drivers", "bizman_empty_building", "bizman_headquarters",
                    "bizman_insight_cleanliness", "bizman_insight_customer_satisfaction", "bizman_insight_customer_service", "bizman_insight_customers",
                    "bizman_insight_customers_chart_last_week", "bizman_insight_customers_chart_yesterday", "bizman_insight_customers_over_time",
                    "bizman_insight_interior", "bizman_insight_marketing", "bizman_insight_pricing", "bizman_insight_promotion", "bizman_insight_satisfaction",
                    "bizman_inventory_days_until_empty", "bizman_list_real_estate_type", "bizman_logisticsmanagers_no_driver", "bizman_market_price",
                    "bizman_market_value", "bizman_marketing_cancel_campaign", "bizman_marketing_efficiency", "bizman_marketing_no_campaigns",
                    "bizman_marketing_total_daily_expenses", "bizman_menu_deliveries", "bizman_menu_drivers", "bizman_menu_factory", "bizman_menu_headhunters",
                    "bizman_menu_insight", "bizman_menu_inventory", "bizman_menu_marketing", "bizman_menu_schedule", "bizman_menu_settings", "bizman_not_for_sale",
                    "bizman_presentation_show_employees", "bizman_preview_button", "bizman_primary_products", "bizman_purchasingagents_next_delivery",
                    "bizman_purchasingagents_order", "bizman_real_estate_apply_new_rent", "bizman_real_estate_average_market_price", "bizman_real_estate_cancel_sale",
                    "bizman_real_estate_current_rent", "bizman_real_estate_enter_sales_price", "bizman_real_estate_estimated_market_price",
                    "bizman_real_estate_mark_for_sale", "bizman_real_estate_occupancy", "bizman_real_estate_price_at_purchase", "bizman_real_estate_rent_management",
                    "bizman_real_estate_rent_not_applied", "bizman_real_estate_self_occupied_unit", "bizman_real_estate_set_for_sale",
                    "bizman_real_estate_takeover_unit", "bizman_real_estate_taxes_info", "bizman_real_estate_total_daily_rent",
                    "bizman_real_estate_unit_owned_by_player", "bizman_real_estate_your_potential_gain", "bizman_rent_building_button", "bizman_retail_price",
                    "bizman_schedule_opening_hours", "bizman_security", "bizman_security_level_good", "bizman_send_offer_button", "bizman_send_purchase_offer",
                    "bizman_set_up_uniforms", "bizman_settings_manage_business_label", "bizman_settings_notification_name_empty", "bizman_settings_save_logo",
                    "bizman_settings_sign_appearance_info", "bizman_shutdown_business", "bizman_sold_last_7_days", "bizman_start_business_button",
                    "bizman_start_new_business_button", "bizman_status", "bizman_stock_count", "bizman_temporarily_closed", "bizman_terminate_contract_button",
                    "bizman_traffic_index", "bizman_vehicle_slots", "bizman_warehouses", "change_character_clothes_unsaved_changes_warning", "common_address",
                    "common_amount", "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday",
                    "common_building_type", "common_business_name", "common_business_type", "common_businesses", "common_closed", "common_font",
                    "common_neighborhood", "common_open", "common_open_in_econoview", "common_price", "common_product", "common_requirements", "common_sales_price",
                    "common_save_changes", "common_total", "phone_call_button", "reallife_interior_tab", "reallife_ui_bizman_agencies",
                    "reallife_ui_bizman_agencies_none", "reallife_ui_bizman_any_building", "reallife_ui_bizman_any_district", "reallife_ui_bizman_any_kind",
                    "reallife_ui_bizman_any_status", "reallife_ui_bizman_appliances", "reallife_ui_bizman_best_sellers", "reallife_ui_bizman_campaigns",
                    "reallife_ui_bizman_campaigns_none", "reallife_ui_bizman_capacity_line", "reallife_ui_bizman_chosen", "reallife_ui_bizman_classic_body",
                    "reallife_ui_bizman_classic_hint", "reallife_ui_bizman_classic_open", "reallife_ui_bizman_classic_title", "reallife_ui_bizman_clear",
                    "reallife_ui_bizman_contracts_none", "reallife_ui_bizman_contracts_none_body", "reallife_ui_bizman_customers_none",
                    "reallife_ui_bizman_customers_none_body", "reallife_ui_bizman_filtered_body", "reallife_ui_bizman_filtered_title", "reallife_ui_bizman_first_day",
                    "reallife_ui_bizman_for_rent", "reallife_ui_bizman_for_sale", "reallife_ui_bizman_foreign_body", "reallife_ui_bizman_foreign_tab",
                    "reallife_ui_bizman_foreign_title", "reallife_ui_bizman_goods_none", "reallife_ui_bizman_goods_none_body", "reallife_ui_bizman_homes",
                    "reallife_ui_bizman_kinds_none", "reallife_ui_bizman_lease", "reallife_ui_bizman_logo_more", "reallife_ui_bizman_name_hint",
                    "reallife_ui_bizman_needs", "reallife_ui_bizman_none_body", "reallife_ui_bizman_none_title", "reallife_ui_bizman_other_mods",
                    "reallife_ui_bizman_overall", "reallife_ui_bizman_pause", "reallife_ui_bizman_prompt_section", "reallife_ui_bizman_randomise",
                    "reallife_ui_bizman_rent_new", "reallife_ui_bizman_rented", "reallife_ui_bizman_rival", "reallife_ui_bizman_satisfaction_none",
                    "reallife_ui_bizman_search", "reallife_ui_bizman_shared", "reallife_ui_bizman_sold_week", "reallife_ui_bizman_start_note",
                    "reallife_ui_bizman_sum_alerts", "reallife_ui_bizman_sum_best", "reallife_ui_bizman_sum_mood", "reallife_ui_bizman_sum_open", "reallife_ui_bizman_tab_building",
                    "reallife_ui_bizman_tab_estate", "reallife_ui_bizman_tab_hr", "reallife_ui_bizman_tab_logistics", "reallife_ui_bizman_tab_prices",
                    "reallife_ui_bizman_tab_pricing", "reallife_ui_bizman_tab_purchasing", "reallife_ui_bizman_take_over", "reallife_ui_bizman_takings_week",
                    "reallife_ui_bizman_the_building", "reallife_ui_bizman_the_business", "reallife_ui_bizman_unsaved", "reallife_ui_prompt_back",
                    "reallife_ui_prompt_group", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "repeating_order", "smartphone_bizman",
                    "common_day_number", "common_setdestination", "reallife_ui_bizman_logo", "reallife_ui_bizman_nothing_ordered", "reallife_ui_bizman_runs_out",
                    "reallife_ui_bizman_this_week", "reallife_ui_bizman_competitors", "reallife_ui_bizman_competitors_here", "reallife_ui_bizman_find_kind",
                    "reallife_ui_bizman_start_pick", "reallife_ui_bizman_start_pick_body", "reallife_ui_bizman_start_sources", "reallife_ui_bizman_start_source_missing",
                    "reallife_ui_bizman_start_needs", "reallife_ui_bizman_start_diploma", "reallife_ui_bizman_start_diploma_missing",
                    "ba:businesstype_bookstore", "ba:businesstype_clothingstore", "ba:businesstype_coffeeshop", "ba:businesstype_electronicsstore",
                    "ba:businesstype_fastfoodrestaurant", "ba:businesstype_florist", "ba:businesstype_giftshop", "ba:businesstype_gym", "ba:businesstype_jewelrystore",
                    "reallife_ui_bizman_customers_total", "common_products",
                    "reallife_interior_head_place", "reallife_interior_head_decor", "reallife_interior_head_condition", "reallife_interior_page_effects",
                    "reallife_interior_page_items", "reallife_interior_page_customers", "reallife_interior_page_rules", "reallife_interior_head_next", "reallife_set_link",
                    "reallife_interior_refresh_button", "reallife_interior_renovate_button", "reallife_guide_app",
                    "reallife_ui_bizman_logo_shapes", "reallife_ui_bizman_logo_shapes_none", "reallife_ui_bizman_logo_note", "reallife_ui_bizman_logo_own", "reallife_ui_bizman_logo_done",
                    "reallife_ui_bizman_logo_colour_shape", "reallife_ui_bizman_logo_colour_text", "reallife_ui_bizman_logo_colour_back", "reallife_ui_bizman_logo_colours"
                }).Switch(UiScreenSwitches.Business),
            });
        }
    }
}
