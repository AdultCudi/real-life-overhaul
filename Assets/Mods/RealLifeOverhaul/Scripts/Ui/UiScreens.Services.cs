using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Services app: Cleaning, Crew, Contracts and Sell on one window, with its own switch (in the company section of Mod settings).
        static partial void AddServices(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("services", UiTemplate.AppWindow, "ServicesScreen", ServicesKeys())
                .Switch(UiScreenSwitches.Company));
        }

        // Every label the Services screen writes (ServicesLive asks for these, with the keys of the old app's words it reuses).
        private static string[] ServicesKeys()
        {
            return new[]
            {
                "reallife_co_back", "reallife_co_col_business", "reallife_co_col_name", "reallife_co_col_state", "reallife_co_off_title",
                "reallife_co_part_off", "reallife_econ_day", "reallife_ibox_sec_how_often", "reallife_ibox_skill", "reallife_svc_add_do",
                "reallife_svc_add_no_hours", "reallife_svc_add_own_hours", "reallife_svc_add_says", "reallife_svc_app", "reallife_svc_asking_says",
                "reallife_svc_bid_accept", "reallife_svc_bid_clause", "reallife_svc_bid_counter", "reallife_svc_bid_no_clause",
                "reallife_svc_bid_price", "reallife_svc_bid_refuse", "reallife_svc_bid_staff_keep", "reallife_svc_bid_staff_not",
                "reallife_svc_bid_until", "reallife_svc_bid_until_asking", "reallife_svc_bid_until_final", "reallife_svc_buyer_chain",
                "reallife_svc_buyer_chain_0", "reallife_svc_buyer_chain_1", "reallife_svc_buyer_chain_2", "reallife_svc_buyer_investor",
                "reallife_svc_buyer_investor_0", "reallife_svc_buyer_investor_1", "reallife_svc_buyer_investor_2", "reallife_svc_buyer_rival",
                "reallife_svc_buyer_rival_0", "reallife_svc_buyer_rival_1", "reallife_svc_buyer_rival_2", "reallife_svc_buyer_staff",
                "reallife_svc_buyer_staff_0", "reallife_svc_buyer_staff_1", "reallife_svc_buyer_staff_2", "reallife_svc_client_clean",
                "reallife_svc_client_closed", "reallife_svc_client_contract", "reallife_svc_client_last", "reallife_svc_client_last_today",
                "reallife_svc_client_needs", "reallife_svc_client_needs_unknown", "reallife_svc_client_next", "reallife_svc_client_remove",
                "reallife_svc_col_cleanliness", "reallife_svc_col_crew", "reallife_svc_col_floor", "reallife_svc_col_got", "reallife_svc_col_hours",
                "reallife_svc_col_mood", "reallife_svc_col_needs", "reallife_svc_col_next", "reallife_svc_col_skill", "reallife_svc_col_standard",
                "reallife_svc_col_today", "reallife_svc_col_wage", "reallife_svc_contract_earned", "reallife_svc_contract_end",
                "reallife_svc_contract_fee", "reallife_svc_contract_missed", "reallife_svc_contract_pattern", "reallife_svc_contract_penalty",
                "reallife_svc_contract_refuse", "reallife_svc_contract_standard", "reallife_svc_contract_take", "reallife_svc_contract_until",
                "reallife_svc_contract_visits", "reallife_svc_contract_week", "reallife_svc_contract_work", "reallife_svc_contracts_running",
                "reallife_svc_contracts_says", "reallife_svc_contracts_says_week", "reallife_svc_crew_base", "reallife_svc_crew_needs_shift",
                "reallife_svc_crew_no_base", "reallife_svc_crew_no_shift", "reallife_svc_crew_no_visits", "reallife_svc_crew_off",
                "reallife_svc_crew_on", "reallife_svc_crew_put_on", "reallife_svc_crew_says_hire", "reallife_svc_crew_says_hours",
                "reallife_svc_crew_says_pay", "reallife_svc_crew_says_pick", "reallife_svc_crew_shift", "reallife_svc_crew_sick",
                "reallife_svc_crew_skill", "reallife_svc_crew_take_off", "reallife_svc_crew_visit", "reallife_svc_crew_visit_missed",
                "reallife_svc_crew_wage", "reallife_svc_empty_cleaning", "reallife_svc_kind_crew_off", "reallife_svc_kind_crew_on",
                "reallife_svc_kind_offer", "reallife_svc_list_withdraw", "reallife_svc_listed_since", "reallife_svc_minutes", "reallife_svc_mode_1",
                "reallife_svc_mode_2", "reallife_svc_mode_3", "reallife_svc_mode_needed", "reallife_svc_off_body", "reallife_svc_raise_books",
                "reallife_svc_raise_days", "reallife_svc_raise_interior", "reallife_svc_raise_met", "reallife_svc_raise_none",
                "reallife_svc_raise_rating", "reallife_svc_raise_skill", "reallife_svc_raise_to", "reallife_svc_raise_to_skill",
                "reallife_svc_refuse_busy", "reallife_svc_refuse_gift", "reallife_svc_refuse_inside", "reallife_svc_refuse_lot",
                "reallife_svc_refuse_not_business", "reallife_svc_refuse_stock", "reallife_svc_refuse_vehicle", "reallife_svc_row_a_week",
                "reallife_svc_row_clause", "reallife_svc_row_closed", "reallife_svc_row_contract", "reallife_svc_row_done", "reallife_svc_row_due",
                "reallife_svc_row_in_now", "reallife_svc_row_next", "reallife_svc_row_no_cleaner", "reallife_svc_row_no_offer",
                "reallife_svc_row_no_offer_no_crew", "reallife_svc_row_on", "reallife_svc_row_standard", "reallife_svc_row_waiting",
                "reallife_svc_rsale_buy", "reallife_svc_rsale_how", "reallife_svc_rsale_keeps", "reallife_svc_rsale_then",
                "reallife_svc_rsale_title", "reallife_svc_sale_says_after", "reallife_svc_sale_says_market", "reallife_svc_sale_says_quick",
                "reallife_svc_sale_says_value", "reallife_svc_service_no_crew", "reallife_svc_service_pick", "reallife_svc_service_round",
                "reallife_svc_service_round_left", "reallife_svc_service_who", "reallife_svc_tab_cleaning", "reallife_svc_tab_contracts",
                "reallife_svc_tab_crew", "reallife_svc_tab_sale", "reallife_svc_trend_down", "reallife_svc_trend_level", "reallife_svc_trend_up",
                "reallife_svc_value_base", "reallife_svc_value_base_value", "reallife_svc_value_fixtures", "reallife_svc_value_quality",
                "reallife_svc_value_worth", "reallife_svc_van_says_off", "reallife_svc_van_says_on", "reallife_svc_who_chain",
                "reallife_svc_who_investor", "reallife_svc_who_rival", "reallife_svc_who_staff", "reallife_svc_who_staff_note",
                "reallife_svc_why_far", "reallife_svc_why_nobody", "reallife_svc_why_time", "reallife_ui_prompt_back", "reallife_ui_prompt_move",
                "reallife_ui_prompt_select", "reallife_ui_svc_accept_list_first", "reallife_ui_svc_accept_wait", "reallife_ui_svc_bars_series",
                "reallife_ui_svc_bars_title", "reallife_ui_svc_building_stays", "reallife_ui_svc_change_to", "reallife_ui_svc_check_repaid",
                "reallife_ui_svc_check_short", "reallife_ui_svc_chip", "reallife_ui_svc_chip_all", "reallife_ui_svc_chip_contracts",
                "reallife_ui_svc_chip_not_on", "reallife_ui_svc_chip_own", "reallife_ui_svc_chip_running", "reallife_ui_svc_close",
                "reallife_ui_svc_col_against", "reallife_ui_svc_col_asking", "reallife_ui_svc_col_buyer", "reallife_ui_svc_col_client",
                "reallife_ui_svc_col_expires", "reallife_ui_svc_col_fee_week", "reallife_ui_svc_col_offer", "reallife_ui_svc_col_seller",
                "reallife_ui_svc_col_takings_week", "reallife_ui_svc_col_terms", "reallife_ui_svc_col_week_ends", "reallife_ui_svc_deposit_back",
                "reallife_ui_svc_going_public", "reallife_ui_svc_goodwill", "reallife_ui_svc_goodwill_by", "reallife_ui_svc_investors",
                "reallife_ui_svc_investors_bought", "reallife_ui_svc_lease_ends", "reallife_ui_svc_left_at_value", "reallife_ui_svc_list_at",
                "reallife_ui_svc_loans_repaid", "reallife_ui_svc_loans_to_repay", "reallife_ui_svc_min", "reallife_ui_svc_no_business",
                "reallife_ui_svc_no_business_sub", "reallife_ui_svc_no_cleaners", "reallife_ui_svc_no_contracts", "reallife_ui_svc_no_offers_listed",
                "reallife_ui_svc_no_offers_listed_body", "reallife_ui_svc_no_offers_none", "reallife_ui_svc_no_offers_unlisted",
                "reallife_ui_svc_no_offers_unlisted_body", "reallife_ui_svc_no_rival_sales", "reallife_ui_svc_no_rival_sales_body",
                "reallife_ui_svc_no_round", "reallife_ui_svc_offers_note", "reallife_ui_svc_price_hint", "reallife_ui_svc_quick_at",
                "reallife_ui_svc_rival_note", "reallife_ui_svc_rival_owner", "reallife_ui_svc_row_asking", "reallife_ui_svc_row_booked_week",
                "reallife_ui_svc_row_fees_week", "reallife_ui_svc_row_how_often", "reallife_ui_svc_row_mood", "reallife_ui_svc_row_offer",
                "reallife_ui_svc_row_own_week", "reallife_ui_svc_row_paid_day", "reallife_ui_svc_row_profit_day", "reallife_ui_svc_row_receive",
                "reallife_ui_svc_row_service_week", "reallife_ui_svc_row_then", "reallife_ui_svc_row_visit_at", "reallife_ui_svc_row_when_under",
                "reallife_ui_svc_sec_accept", "reallife_ui_svc_sec_accept_none", "reallife_ui_svc_sec_after", "reallife_ui_svc_sec_contracts",
                "reallife_ui_svc_sec_crew", "reallife_ui_svc_sec_give_up", "reallife_ui_svc_sec_price", "reallife_ui_svc_sec_raise",
                "reallife_ui_svc_sec_rival_none", "reallife_ui_svc_sec_service", "reallife_ui_svc_sec_valued", "reallife_ui_svc_sell_pick_title",
                "reallife_ui_svc_state_active", "reallife_ui_svc_state_late", "reallife_ui_svc_state_nobody", "reallife_ui_svc_state_not_on",
                "reallife_ui_svc_state_offer", "reallife_ui_svc_tax_gain", "reallife_ui_svc_term_cash", "reallife_ui_svc_term_keeps",
                "reallife_ui_svc_term_weeks", "reallife_ui_svc_then_kept", "reallife_ui_svc_then_taken", "reallife_ui_svc_then_value",
                "reallife_ui_svc_threshold_hint", "reallife_ui_svc_van", "reallife_ui_svc_view_offers", "reallife_ui_svc_view_offers_title",
                "reallife_ui_svc_view_rivals", "reallife_ui_svc_view_rivals_title", "reallife_ui_svc_visits_n", "reallife_ui_svc_visits_needed",
                "reallife_ui_svc_week_split", "reallife_ui_svc_worth_sub", "reallife_ui_svc_worth_today"
            };
        }
    }
}
