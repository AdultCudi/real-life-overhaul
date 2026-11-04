using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Property and the Car Showroom: two apps on one set of page shapes (EstateFace), each with its own source and its own switch.
        static partial void AddEstate(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("estate", UiTemplate.AppWindow, "EstateScreen", EstateKeys())
                .Switch(UiScreenSwitches.Estate));
            screens.Add(new UiScreenDef("dealer", UiTemplate.AppWindow, "EstateScreen", DealerKeys())
                .Switch(UiScreenSwitches.Estate));
        }

        // Every label the Property screen writes (EstateLive asks for these and the keys of the old app's words it reuses).
        private static string[] EstateKeys()
        {
            return new[]
            {
                "reallife_co_back", "reallife_estate_agency_chance", "reallife_estate_agency_office", "reallife_estate_agency_open_what",
                "reallife_estate_agency_reputation", "reallife_estate_agency_staff", "reallife_estate_app", "reallife_estate_app_note",
                "reallife_estate_app_offer", "reallife_estate_app_term", "reallife_estate_btn_accept", "reallife_estate_btn_close_agency",
                "reallife_estate_btn_contractor", "reallife_estate_btn_list", "reallife_estate_btn_notice", "reallife_estate_btn_offer",
                "reallife_estate_btn_open_agency", "reallife_estate_btn_plan", "reallife_estate_btn_refurbish", "reallife_estate_btn_refuse",
                "reallife_estate_btn_remind", "reallife_estate_btn_repair", "reallife_estate_btn_yourself", "reallife_estate_call_contractor",
                "reallife_estate_call_since", "reallife_estate_call_yourself", "reallife_estate_car_idle", "reallife_estate_car_out",
                "reallife_estate_class_economy", "reallife_estate_class_premium", "reallife_estate_class_standard", "reallife_estate_cycle_steady",
                "reallife_estate_duty_happy", "reallife_estate_duty_roll", "reallife_estate_duty_self_what", "reallife_estate_duty_who",
                "reallife_estate_filter_applicants", "reallife_estate_filter_lettings", "reallife_estate_filter_sales", "reallife_estate_flip_gain",
                "reallife_estate_hire_cover", "reallife_estate_hire_lot", "reallife_estate_hire_rating", "reallife_estate_hire_what",
                "reallife_estate_hotel_night_what", "reallife_estate_hotel_what", "reallife_estate_improve_boiler", "reallife_estate_listing_base",
                "reallife_estate_listing_market", "reallife_estate_listing_price", "reallife_estate_listing_rent", "reallife_estate_manager_self",
                "reallife_estate_market_heading", "reallife_estate_market_level", "reallife_estate_market_prices", "reallife_estate_market_what",
                "reallife_estate_off_body", "reallife_estate_off_title", "reallife_estate_own_cover", "reallife_estate_own_cover_what",
                "reallife_estate_own_list_what", "reallife_estate_own_selling", "reallife_estate_own_worth", "reallife_estate_record_fair",
                "reallife_estate_record_good", "reallife_estate_row_a_day", "reallife_estate_row_going_concern", "reallife_estate_row_hotel",
                "reallife_estate_row_letting", "reallife_estate_row_no_agency", "reallife_estate_row_no_cars", "reallife_estate_row_no_hotel",
                "reallife_estate_row_no_instructions", "reallife_estate_row_no_market", "reallife_estate_row_nobody",
                "reallife_estate_row_none_managed", "reallife_estate_row_nothing_owned", "reallife_estate_row_on_lot", "reallife_estate_row_sale",
                "reallife_estate_row_since", "reallife_estate_row_sold", "reallife_estate_row_until", "reallife_estate_stage_marketing",
                "reallife_estate_stage_offered", "reallife_estate_state_empty", "reallife_estate_state_paying", "reallife_estate_tab_agency",
                "reallife_estate_tab_hire", "reallife_estate_tab_hotel", "reallife_estate_tab_market", "reallife_estate_tab_tenants",
                "reallife_estate_tab_works", "reallife_estate_ten_arrears", "reallife_estate_ten_asking", "reallife_estate_ten_asking_what",
                "reallife_estate_ten_market", "reallife_estate_ten_to_let", "reallife_estate_tenant_shops_2", "reallife_estate_tenants_empty",
                "reallife_estate_tenants_let", "reallife_estate_tenants_notice_what", "reallife_estate_tenants_owed", "reallife_estate_use_homes",
                "reallife_estate_use_offices", "reallife_estate_use_shops", "reallife_estate_use_storage", "reallife_estate_works_cond",
                "reallife_estate_works_market", "reallife_estate_works_repair", "reallife_estate_works_wear", "reallife_estate_works_what",
                "reallife_ibox_chip_booked", "reallife_ibox_chip_empty", "reallife_ibox_chip_notice", "reallife_ibox_chip_open", "reallife_ibox_let",
                "reallife_ibox_sec_how", "reallife_ibox_sec_letting", "reallife_ibox_sec_money", "reallife_ibox_sec_price_made",
                "reallife_ibox_sec_quote", "reallife_ibox_sec_terms", "reallife_ibox_sec_works", "reallife_ibox_spaces", "reallife_ibox_stars",
                "reallife_ibox_type_in", "reallife_ibox_worth", "reallife_ui_estate_a_month", "reallife_ui_estate_all_works",
                "reallife_ui_estate_applicants", "reallife_ui_estate_asked_by_tenant", "reallife_ui_estate_asking_note", "reallife_ui_estate_bed_2",
                "reallife_ui_estate_book_kind", "reallife_ui_estate_booked_with", "reallife_ui_estate_col_building", "reallife_ui_estate_col_car",
                "reallife_ui_estate_col_commission", "reallife_ui_estate_col_condition", "reallife_ui_estate_col_cost",
                "reallife_ui_estate_col_costs", "reallife_ui_estate_col_costs_year", "reallife_ui_estate_col_day", "reallife_ui_estate_col_hires",
                "reallife_ui_estate_col_hotel", "reallife_ui_estate_col_hours", "reallife_ui_estate_col_instruction",
                "reallife_ui_estate_col_lease_ends", "reallife_ui_estate_col_price", "reallife_ui_estate_col_rate_day",
                "reallife_ui_estate_col_record", "reallife_ui_estate_col_rent_month", "reallife_ui_estate_col_rooms",
                "reallife_ui_estate_col_satisfaction", "reallife_ui_estate_col_state", "reallife_ui_estate_col_takings",
                "reallife_ui_estate_col_takings_year", "reallife_ui_estate_col_tenant", "reallife_ui_estate_col_unit",
                "reallife_ui_estate_col_value", "reallife_ui_estate_col_what", "reallife_ui_estate_col_yield", "reallife_ui_estate_condition_level",
                "reallife_ui_estate_day", "reallife_ui_estate_deposit_held", "reallife_ui_estate_due_day", "reallife_ui_estate_due_repair",
                "reallife_ui_estate_empty_hotel_body", "reallife_ui_estate_empty_market_body", "reallife_ui_estate_empty_roll_body",
                "reallife_ui_estate_empty_tenants_body", "reallife_ui_estate_empty_title", "reallife_ui_estate_empty_works_body",
                "reallife_ui_estate_fig_arrears", "reallife_ui_estate_fig_collected", "reallife_ui_estate_fig_due", "reallife_ui_estate_fig_empty",
                "reallife_ui_estate_group_business", "reallife_ui_estate_group_market", "reallife_ui_estate_group_mine",
                "reallife_ui_estate_here_since", "reallife_ui_estate_hotels_sub", "reallife_ui_estate_improve_body",
                "reallife_ui_estate_improve_head", "reallife_ui_estate_in_days", "reallife_ui_estate_kept", "reallife_ui_estate_landlord_line",
                "reallife_ui_estate_last_inspected", "reallife_ui_estate_last_months", "reallife_ui_estate_late_days",
                "reallife_ui_estate_late_head", "reallife_ui_estate_lease_ends", "reallife_ui_estate_locate", "reallife_ui_estate_m_condition",
                "reallife_ui_estate_m_heating", "reallife_ui_estate_m_repairs", "reallife_ui_estate_m_satisfaction", "reallife_ui_estate_m_value",
                "reallife_ui_estate_make_it", "reallife_ui_estate_market_note", "reallife_ui_estate_market_sub", "reallife_ui_estate_more",
                "reallife_ui_estate_nav_agency", "reallife_ui_estate_nav_hire", "reallife_ui_estate_nav_hotel", "reallife_ui_estate_nav_market",
                "reallife_ui_estate_nav_mine", "reallife_ui_estate_nav_tenants", "reallife_ui_estate_nav_works", "reallife_ui_estate_need_rent",
                "reallife_ui_estate_need_request", "reallife_ui_estate_needs_you", "reallife_ui_estate_next_inspection",
                "reallife_ui_estate_no_landlords", "reallife_ui_estate_no_repairs", "reallife_ui_estate_not_paid_yet", "reallife_ui_estate_not_yet",
                "reallife_ui_estate_of", "reallife_ui_estate_open_page", "reallife_ui_estate_other_landlords", "reallife_ui_estate_other_units",
                "reallife_ui_estate_paid", "reallife_ui_estate_paid_on", "reallife_ui_estate_quote_asked", "reallife_ui_estate_record_title",
                "reallife_ui_estate_roll_sub", "reallife_ui_estate_roll_title", "reallife_ui_estate_rv_districts", "reallife_ui_estate_rv_empty",
                "reallife_ui_estate_rv_flat_sqm", "reallife_ui_estate_rv_for_sale", "reallife_ui_estate_rv_legend", "reallife_ui_estate_rv_listings",
                "reallife_ui_estate_rv_mean_flat", "reallife_ui_estate_rv_month", "reallife_ui_estate_rv_month_wait",
                "reallife_ui_estate_rv_office_sqm", "reallife_ui_estate_rv_per_month", "reallife_ui_estate_rv_rank_note",
                "reallife_ui_estate_rv_rent", "reallife_ui_estate_rv_scale", "reallife_ui_estate_rv_shop_sqm", "reallife_ui_estate_rv_title",
                "reallife_ui_estate_rv_your_let", "reallife_ui_estate_rv_your_mean", "reallife_ui_estate_see_market",
                "reallife_ui_estate_service_line", "reallife_ui_estate_service_name", "reallife_ui_estate_service_title",
                "reallife_ui_estate_service_what", "reallife_ui_estate_servicing", "reallife_ui_estate_sound", "reallife_ui_estate_sqm",
                "reallife_ui_estate_tenants_sub", "reallife_ui_estate_the_building", "reallife_ui_estate_the_tenant", "reallife_ui_estate_total_in",
                "reallife_ui_estate_total_let", "reallife_ui_estate_unfit", "reallife_ui_estate_view_map", "reallife_ui_estate_waiting_you",
                "reallife_ui_estate_works_here", "reallife_ui_estate_works_link", "reallife_ui_estate_works_sub", "reallife_ui_estate_year_rent",
                "reallife_ui_estate_year_title", "reallife_ui_estate_year_works", "reallife_ui_prompt_back", "reallife_ui_prompt_move",
                "reallife_ui_prompt_select"
            };
        }

        // Every label the Car Showroom writes.
        private static string[] DealerKeys()
        {
            return new[]
            {
                "reallife_dealer_app", "reallife_dealer_button_accept", "reallife_dealer_button_buy", "reallife_dealer_button_counter",
                "reallife_dealer_button_decline", "reallife_dealer_button_drive", "reallife_dealer_button_event", "reallife_dealer_button_inspect",
                "reallife_dealer_button_offer", "reallife_dealer_button_prepare", "reallife_dealer_button_relay", "reallife_dealer_button_trade",
                "reallife_dealer_cash", "reallife_dealer_days", "reallife_dealer_deal_finance", "reallife_dealer_event_0",
                "reallife_dealer_event_0_about", "reallife_dealer_event_0_short", "reallife_dealer_event_at", "reallife_dealer_event_terms",
                "reallife_dealer_finance_about", "reallife_dealer_finance_covers", "reallife_dealer_finance_plans", "reallife_dealer_finance_title",
                "reallife_dealer_goes_to", "reallife_dealer_growth_events", "reallife_dealer_growth_missions", "reallife_dealer_growth_rep",
                "reallife_dealer_growth_title", "reallife_dealer_kind_auction", "reallife_dealer_kind_used", "reallife_dealer_kind_wholesale",
                "reallife_dealer_lot_count", "reallife_dealer_lots_about", "reallife_dealer_lots_staff", "reallife_dealer_lots_title",
                "reallife_dealer_market_about", "reallife_dealer_market_sum", "reallife_dealer_market_title", "reallife_dealer_mission_0",
                "reallife_dealer_mission_0_about", "reallife_dealer_none", "reallife_dealer_off_body", "reallife_dealer_off_title",
                "reallife_dealer_offers_about", "reallife_dealer_offers_title", "reallife_dealer_offers_today", "reallife_dealer_perk_2",
                "reallife_dealer_plan_missed", "reallife_dealer_plan_paying", "reallife_dealer_profile_bargain", "reallife_dealer_profile_fair",
                "reallife_dealer_progress", "reallife_dealer_report_age", "reallife_dealer_report_all", "reallife_dealer_report_arrears",
                "reallife_dealer_report_conversion", "reallife_dealer_report_none", "reallife_dealer_report_profit",
                "reallife_dealer_report_revenue", "reallife_dealer_report_sold", "reallife_dealer_report_visitors", "reallife_dealer_reports_now",
                "reallife_dealer_reports_week", "reallife_dealer_row_no_dealership", "reallife_dealer_state_event_ready",
                "reallife_dealer_state_leading", "reallife_dealer_state_mission_open", "reallife_dealer_state_new", "reallife_dealer_state_no_bid",
                "reallife_dealer_state_not_inspected", "reallife_dealer_state_on_show", "reallife_dealer_state_rival", "reallife_dealer_state_sold",
                "reallife_dealer_state_waiting", "reallife_dealer_step_0", "reallife_dealer_step_2", "reallife_dealer_sum_waiting",
                "reallife_dealer_tab_finance", "reallife_dealer_tab_growth", "reallife_dealer_tab_lots", "reallife_dealer_tab_market",
                "reallife_dealer_tab_offers", "reallife_dealer_tab_reports", "reallife_dealer_tab_stock", "reallife_dealer_tier_extended",
                "reallife_dealer_to_trade", "reallife_dealer_used_asking", "reallife_dealer_used_said", "reallife_dealer_yard_rep",
                "reallife_dealer_yard_today", "reallife_ibox_asking", "reallife_ibox_chip_not_inspected", "reallife_ibox_cost", "reallife_ibox_lot",
                "reallife_ibox_offer", "reallife_ibox_sec_car", "reallife_ibox_sec_money", "reallife_ibox_sec_now", "reallife_ibox_sec_offer",
                "reallife_ibox_sec_terms", "reallife_ibox_sec_this_week", "reallife_ibox_sec_where", "reallife_ibox_today", "reallife_ibox_worth",
                "reallife_ui_dealer_ask_to", "reallife_ui_dealer_auction_kicker", "reallife_ui_dealer_back", "reallife_ui_dealer_bid_button",
                "reallife_ui_dealer_bid_hint", "reallife_ui_dealer_bidders", "reallife_ui_dealer_bidding_on", "reallife_ui_dealer_buyers_fee",
                "reallife_ui_dealer_chart_day", "reallife_ui_dealer_chart_empty", "reallife_ui_dealer_claims", "reallife_ui_dealer_col_about",
                "reallife_ui_dealer_col_against", "reallife_ui_dealer_col_asking", "reallife_ui_dealer_col_buyer", "reallife_ui_dealer_col_car",
                "reallife_ui_dealer_col_condition", "reallife_ui_dealer_col_customer", "reallife_ui_dealer_col_kind", "reallife_ui_dealer_col_km",
                "reallife_ui_dealer_col_left", "reallife_ui_dealer_col_lot", "reallife_ui_dealer_col_offer", "reallife_ui_dealer_col_offers",
                "reallife_ui_dealer_col_owed_paid", "reallife_ui_dealer_col_price", "reallife_ui_dealer_col_progress",
                "reallife_ui_dealer_col_showroom", "reallife_ui_dealer_col_sold", "reallife_ui_dealer_col_staff", "reallife_ui_dealer_col_state",
                "reallife_ui_dealer_col_takings", "reallife_ui_dealer_col_time", "reallife_ui_dealer_col_until", "reallife_ui_dealer_col_visitors",
                "reallife_ui_dealer_col_weekly", "reallife_ui_dealer_col_what", "reallife_ui_dealer_colour_line", "reallife_ui_dealer_counters_left",
                "reallife_ui_dealer_current_bid", "reallife_ui_dealer_days_chip", "reallife_ui_dealer_days_on_lot", "reallife_ui_dealer_deal_looked",
                "reallife_ui_dealer_empty_finance", "reallife_ui_dealer_empty_market", "reallife_ui_dealer_empty_offers",
                "reallife_ui_dealer_empty_stock_body", "reallife_ui_dealer_filter_all", "reallife_ui_dealer_filter_new",
                "reallife_ui_dealer_filter_shop", "reallife_ui_dealer_filter_used", "reallife_ui_dealer_finance_sub", "reallife_ui_dealer_free_bays",
                "reallife_ui_dealer_growth_event", "reallife_ui_dealer_growth_mission", "reallife_ui_dealer_growth_rep",
                "reallife_ui_dealer_growth_sub", "reallife_ui_dealer_held_by", "reallife_ui_dealer_hour_ago", "reallife_ui_dealer_hours_ago",
                "reallife_ui_dealer_if_you_win", "reallife_ui_dealer_kicker_bay", "reallife_ui_dealer_l_condition", "reallife_ui_dealer_l_cost",
                "reallife_ui_dealer_l_km", "reallife_ui_dealer_l_margin", "reallife_ui_dealer_l_margin_offer", "reallife_ui_dealer_l_source",
                "reallife_ui_dealer_l_worth", "reallife_ui_dealer_left_for_you", "reallife_ui_dealer_looked_times", "reallife_ui_dealer_lots_sub",
                "reallife_ui_dealer_market_sub", "reallife_ui_dealer_no_bids", "reallife_ui_dealer_offers_sub", "reallife_ui_dealer_on_lot",
                "reallife_ui_dealer_rep_line", "reallife_ui_dealer_report_average", "reallife_ui_dealer_report_sub", "reallife_ui_dealer_rival_asks",
                "reallife_ui_dealer_search", "reallife_ui_dealer_sort_bay", "reallife_ui_dealer_sort_days", "reallife_ui_dealer_sort_price",
                "reallife_ui_dealer_staff_floor", "reallife_ui_dealer_staff_floor_what", "reallife_ui_dealer_stock_worth", "reallife_ui_dealer_sums",
                "reallife_ui_dealer_sw_extras", "reallife_ui_dealer_sw_finance", "reallife_ui_dealer_sw_fixed", "reallife_ui_dealer_sw_warranty",
                "reallife_ui_dealer_terms", "reallife_ui_dealer_the_car", "reallife_ui_dealer_time_left", "reallife_ui_dealer_views",
                "reallife_ui_dealer_walk_away", "reallife_ui_dealer_with_it", "reallife_ui_dealer_worth_lot", "reallife_ui_dealer_you",
                "reallife_ui_dealer_you_pay", "reallife_ui_dealer_your_bid", "reallife_ui_dealer_your_counter", "reallife_ui_dealer_your_offer",
                "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select"
            };
        }
    }
}
