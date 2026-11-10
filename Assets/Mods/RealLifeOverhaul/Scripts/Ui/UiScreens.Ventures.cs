using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Brands, Ventures and the Web shop on the kit: three apps on one window and one runner (Ventures/VentScreen), each with its own pages and its own switch.
        // The key list of each is every label its pages and its samples write with (the pages' w.T and w.F calls), so a check can see that each is named here.
        static partial void AddVentures(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("ventures", UiTemplate.AppWindow, "VentScreen", new[]
            {
                // KEYS BEGIN
                "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_ven_accept",
                "reallife_ui_ven_buy_back_at", "reallife_ui_ven_buy_out_n", "reallife_ui_ven_chip_answer", "reallife_ui_ven_chip_expires",
                "reallife_ui_ven_chosen", "reallife_ui_ven_col_amount", "reallife_ui_ven_col_district", "reallife_ui_ven_col_for",
                "reallife_ui_ven_col_let", "reallife_ui_ven_col_map", "reallife_ui_ven_col_paid", "reallife_ui_ven_col_rent_week",
                "reallife_ui_ven_col_stake", "reallife_ui_ven_col_values", "reallife_ui_ven_counter_more", "reallife_ui_ven_deal_arrears",
                "reallife_ui_ven_deal_cost_promise", "reallife_ui_ven_deal_in", "reallife_ui_ven_deal_mood", "reallife_ui_ven_deal_out",
                "reallife_ui_ven_deal_signed", "reallife_ui_ven_demand_seat", "reallife_ui_ven_fig_name", "reallife_ui_ven_fig_promises",
                "reallife_ui_ven_how_short", "reallife_ui_ven_inv_committed", "reallife_ui_ven_inv_in", "reallife_ui_ven_inv_likes",
                "reallife_ui_ven_inv_off", "reallife_ui_ven_inv_out", "reallife_ui_ven_inv_range", "reallife_ui_ven_inv_sub",
                "reallife_ui_ven_inv_temper", "reallife_ui_ven_n_of_m", "reallife_ui_ven_new_budget_label", "reallife_ui_ven_new_how",
                "reallife_ui_ven_new_kind", "reallife_ui_ven_new_pick", "reallife_ui_ven_off_body", "reallife_ui_ven_off_title",
                "reallife_ui_ven_offer_for", "reallife_ui_ven_offer_share", "reallife_ui_ven_offer_target", "reallife_ui_ven_pay_off_n",
                "reallife_ui_ven_pitch", "reallife_ui_ven_pledge_drop", "reallife_ui_ven_pledge_for", "reallife_ui_ven_pledge_how",
                "reallife_ui_ven_promise_seen", "reallife_ui_ven_prop_bizman", "reallife_ui_ven_prop_bought", "reallife_ui_ven_prop_count",
                "reallife_ui_ven_prop_let", "reallife_ui_ven_prop_month", "reallife_ui_ven_prop_mortgage", "reallife_ui_ven_prop_paid",
                "reallife_ui_ven_prop_week", "reallife_ui_ven_prop_what", "reallife_ui_ven_prop_worth", "reallife_ui_ven_prop_yield",
                "reallife_ui_ven_prop_yield_note", "reallife_ui_ven_raise_again", "reallife_ui_ven_raise_how", "reallife_ui_ven_raise_how_sec",
                "reallife_ui_ven_raise_pick", "reallife_ui_ven_raise_truth", "reallife_ui_ven_report", "reallife_ui_ven_row_investors",
                "reallife_ui_ven_row_new_business", "reallife_ui_ven_row_new_business_note", "reallife_ui_ven_row_no_property", "reallife_ui_ven_row_no_types",
                "reallife_ui_ven_row_nothing", "reallife_ui_ven_row_you_run", "reallife_ui_ven_sec_also", "reallife_ui_ven_sec_investors",
                "reallife_ui_ven_sec_promise", "reallife_ui_ven_sec_table", "reallife_ui_ven_sec_terms", "reallife_ui_ven_share_of_profit",
                "reallife_ui_ven_term_buy_back", "reallife_ui_ven_term_none", "reallife_ui_ven_term_sale", "reallife_ui_ven_val_none",
                "reallife_ui_ven_val_sec", "reallife_ui_ven_val_you", "reallife_ui_ven_who_pick", "reallife_ui_ven_who_range",
                "reallife_ui_ven_who_sub", "reallife_ven_app", "reallife_ven_back", "reallife_ven_col_building",
                "reallife_ven_col_investor", "reallife_ven_col_state", "reallife_ven_col_worth", "reallife_ven_col_yield",
                "reallife_ven_counter_share", "reallife_ven_demand_costs", "reallife_ven_new_budget", "reallife_ven_new_next",
                "reallife_ven_of_hundred", "reallife_ven_raise", "reallife_ven_refuse", "reallife_ven_row_a_day",
                "reallife_ven_row_new", "reallife_ven_row_open_by", "reallife_ven_row_range", "reallife_ven_tab_investors",
                "reallife_ven_tab_property"
                // KEYS END
            }).Switch(UiScreenSwitches.Ventures));
            screens.Add(new UiScreenDef("brands", UiTemplate.AppWindow, "VentScreen", new[]
            {
                // KEYS BEGIN
                "reallife_brand_app", "reallife_brand_app_refuse", "reallife_brand_back", "reallife_brand_bid_refuse",
                "reallife_brand_chain_prices", "reallife_brand_deal_no_site", "reallife_brand_deal_sign", "reallife_brand_fr_agree",
                "reallife_brand_fr_decline", "reallife_brand_fr_give_way", "reallife_brand_fr_inspect_send", "reallife_brand_fr_retrain",
                "reallife_brand_fr_waive", "reallife_brand_fr_warn", "reallife_brand_plain_make", "reallife_brand_push_book",
                "reallife_brand_row_no_applicant", "reallife_brand_row_no_brand", "reallife_brand_set_give_up", "reallife_brand_set_hours",
                "reallife_brand_set_prices", "reallife_brand_set_rename", "reallife_brand_tab_agreements", "reallife_brand_tab_applicants",
                "reallife_brand_tab_shops", "reallife_ibox_chip_on_offer", "reallife_ibox_leaving_costs", "reallife_ibox_minimum",
                "reallife_ibox_sec_campaign", "reallife_ibox_sec_chain", "reallife_ibox_sec_franchise", "reallife_ibox_sec_shared",
                "reallife_ibox_sec_terms", "reallife_ui_bra_agreement_pick", "reallife_ui_bra_agreement_terms", "reallife_ui_bra_agreements_how",
                "reallife_ui_bra_applicant_pick", "reallife_ui_bra_applicants_how", "reallife_ui_bra_aware_none", "reallife_ui_bra_aware_sec",
                "reallife_ui_bra_awareness", "reallife_ui_bra_brand", "reallife_ui_bra_brand_value", "reallife_ui_bra_campaign_about",
                "reallife_ui_bra_campaign_for", "reallife_ui_bra_campaigns", "reallife_ui_bra_chain_keep", "reallife_ui_bra_chain_leave",
                "reallife_ui_bra_col_applicant", "reallife_ui_bra_col_brand", "reallife_ui_bra_col_capital", "reallife_ui_bra_col_day",
                "reallife_ui_bra_col_district", "reallife_ui_bra_col_ends", "reallife_ui_bra_col_franchisee", "reallife_ui_bra_col_map",
                "reallife_ui_bra_col_owner", "reallife_ui_bra_col_paid_all", "reallife_ui_bra_col_rating", "reallife_ui_bra_col_royalty_day",
                "reallife_ui_bra_col_shop", "reallife_ui_bra_col_status", "reallife_ui_bra_col_temper", "reallife_ui_bra_col_waits",
                "reallife_ui_bra_col_years", "reallife_ui_bra_cost", "reallife_ui_bra_days", "reallife_ui_bra_days_n",
                "reallife_ui_bra_district", "reallife_ui_bra_districts", "reallife_ui_bra_earned", "reallife_ui_bra_end",
                "reallife_ui_bra_exclusive", "reallife_ui_bra_exclusive_area", "reallife_ui_bra_fee", "reallife_ui_bra_fees_month",
                "reallife_ui_bra_fig_sales", "reallife_ui_bra_fig_shops", "reallife_ui_bra_fig_waiting", "reallife_ui_bra_fund",
                "reallife_ui_bra_in_days", "reallife_ui_bra_join", "reallife_ui_bra_keep", "reallife_ui_bra_kind",
                "reallife_ui_bra_leave", "reallife_ui_bra_length", "reallife_ui_bra_levy", "reallife_ui_bra_local",
                "reallife_ui_bra_minimum", "reallife_ui_bra_minimum_about", "reallife_ui_bra_net", "reallife_ui_bra_new_campaign",
                "reallife_ui_bra_no_agreements", "reallife_ui_bra_no_campaign", "reallife_ui_bra_no_shops", "reallife_ui_bra_none",
                "reallife_ui_bra_off_body", "reallife_ui_bra_off_title", "reallife_ui_bra_offer", "reallife_ui_bra_open_agreement",
                "reallife_ui_bra_quote", "reallife_ui_bra_rating", "reallife_ui_bra_royalties", "reallife_ui_bra_royalties_ring",
                "reallife_ui_bra_royalty", "reallife_ui_bra_royalty_day", "reallife_ui_bra_sales_all", "reallife_ui_bra_sales_fr",
                "reallife_ui_bra_sales_month", "reallife_ui_bra_second_shop", "reallife_ui_bra_sell_bid", "reallife_ui_bra_settings",
                "reallife_ui_bra_shared", "reallife_ui_bra_shops_badge", "reallife_ui_bra_shops_pick", "reallife_ui_bra_shops_run",
                "reallife_ui_bra_site", "reallife_ui_bra_standards", "reallife_ui_bra_standards_check", "reallife_ui_bra_standards_sec",
                "reallife_ui_bra_tab_brands", "reallife_ui_bra_target", "reallife_ui_bra_worst", "reallife_ui_bra_yesterday",
                "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select"
                // KEYS END
            }).Switch(UiScreenSwitches.Ventures));
            screens.Add(new UiScreenDef("webshop", UiTemplate.AppWindow, "VentScreen", new[]
            {
                // KEYS BEGIN
                "reallife_brand_back", "reallife_online_app", "reallife_online_row_offline", "reallife_online_state_not_offered",
                "reallife_online_state_offered", "reallife_online_tab_catalogue", "reallife_online_tab_orders", "reallife_online_tab_shops",
                "reallife_ui_bra_col_rating", "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                "reallife_ui_shop_advertise", "reallife_ui_shop_area", "reallife_ui_shop_arrives_label", "reallife_ui_shop_basket",
                "reallife_ui_shop_basket_line", "reallife_ui_shop_basket_none", "reallife_ui_shop_basket_of", "reallife_ui_shop_brand",
                "reallife_ui_shop_close", "reallife_ui_shop_col_badge", "reallife_ui_shop_col_customer", "reallife_ui_shop_col_district",
                "reallife_ui_shop_col_items", "reallife_ui_shop_col_price", "reallife_ui_shop_col_product", "reallife_ui_shop_col_rival",
                "reallife_ui_shop_col_seller", "reallife_ui_shop_col_stage", "reallife_ui_shop_col_state", "reallife_ui_shop_col_stock",
                "reallife_ui_shop_col_total", "reallife_ui_shop_delivered", "reallife_ui_shop_delivery_sec", "reallife_ui_shop_edit_desc",
                "reallife_ui_shop_edit_title", "reallife_ui_shop_express", "reallife_ui_shop_fee", "reallife_ui_shop_free",
                "reallife_ui_shop_free_over", "reallife_ui_shop_is_free", "reallife_ui_shop_late", "reallife_ui_shop_lines_how",
                "reallife_ui_shop_match", "reallife_ui_shop_missed_week", "reallife_ui_shop_n_stars", "reallife_ui_shop_never",
                "reallife_ui_shop_night", "reallife_ui_shop_no_lines", "reallife_ui_shop_no_orders", "reallife_ui_shop_no_results",
                "reallife_ui_shop_no_results_how", "reallife_ui_shop_no_reviews", "reallife_ui_shop_no_rivals", "reallife_ui_shop_none",
                "reallife_ui_shop_off_body", "reallife_ui_shop_off_title", "reallife_ui_shop_on_site", "reallife_ui_shop_only_left",
                "reallife_ui_shop_open_orders", "reallife_ui_shop_order_n", "reallife_ui_shop_order_pick", "reallife_ui_shop_orders_how",
                "reallife_ui_shop_orders_today", "reallife_ui_shop_overdue", "reallife_ui_shop_pack_30", "reallife_ui_shop_pack_60",
                "reallife_ui_shop_pack_day", "reallife_ui_shop_packing", "reallife_ui_shop_pay", "reallife_ui_shop_pick_line",
                "reallife_ui_shop_price", "reallife_ui_shop_product_line", "reallife_ui_shop_rated", "reallife_ui_shop_refund",
                "reallife_ui_shop_rename", "reallife_ui_shop_returned", "reallife_ui_shop_reviews", "reallife_ui_shop_reviews_how",
                "reallife_ui_shop_reviews_sec", "reallife_ui_shop_rival_catalogue", "reallife_ui_shop_rival_line", "reallife_ui_shop_sales_none",
                "reallife_ui_shop_sales_sec", "reallife_ui_shop_search", "reallife_ui_shop_send", "reallife_ui_shop_settings",
                "reallife_ui_shop_site_n", "reallife_ui_shop_sold_out", "reallife_ui_shop_stars", "reallife_ui_shop_stock",
                "reallife_ui_shop_stock_label", "reallife_ui_shop_store_line", "reallife_ui_shop_subtotal", "reallife_ui_shop_tab_store",
                "reallife_ui_shop_takings_today", "reallife_ui_shop_to_free", "reallife_ui_shop_total", "reallife_ui_shop_was",
                "reallife_ui_shop_was_label", "reallife_ui_shop_week", "reallife_ui_shop_you_answered"
                // KEYS END
            }).Switch(UiScreenSwitches.Ventures));
            screens.Add(new UiScreenDef("giving", UiTemplate.AppWindow, "VentScreen", new[]
            {
                // KEYS BEGIN
                "reallife_giving_app", "reallife_giving_charity_0_about", "reallife_giving_charity_0_name", "reallife_giving_charity_1_about",
                "reallife_giving_charity_1_name", "reallife_giving_charity_2_about", "reallife_giving_charity_2_name", "reallife_giving_fig_deducted",
                "reallife_giving_fig_given", "reallife_giving_fig_spare", "reallife_giving_give", "reallife_giving_item_monthly",
                "reallife_giving_item_sub", "reallife_giving_loan_closed", "reallife_giving_loan_late", "reallife_giving_loan_off_chip",
                "reallife_giving_loan_open", "reallife_giving_loan_repaid_chip", "reallife_giving_loan_sub", "reallife_giving_loans_how",
                "reallife_giving_loans_none", "reallife_giving_monthly", "reallife_giving_off_body", "reallife_giving_off_title",
                "reallife_giving_row_cap", "reallife_giving_row_cap_share", "reallife_giving_row_deducted", "reallife_giving_row_gifts",
                "reallife_giving_row_held", "reallife_giving_row_income", "reallife_giving_row_last", "reallife_giving_row_last_value",
                "reallife_giving_row_neighbours", "reallife_giving_row_neighbours_value", "reallife_giving_row_paid_value", "reallife_giving_row_rivals",
                "reallife_giving_row_yours", "reallife_giving_sec_charities", "reallife_giving_sec_city", "reallife_giving_sec_how_loans",
                "reallife_giving_sec_loans", "reallife_giving_sec_tax", "reallife_giving_stepper", "reallife_giving_stop_monthly",
                "reallife_giving_tab_charities", "reallife_giving_tab_loans", "reallife_giving_tab_year", "reallife_giving_tax_note",
                "reallife_giving_why_people", "reallife_giving_why_session", "reallife_ui_prompt_back", "reallife_ui_prompt_move",
                "reallife_ui_prompt_select"
                // KEYS END
            }).Switch(UiScreenSwitches.Ventures));
            screens.Add(new UiScreenDef("dynasty", UiTemplate.AppWindow, "VentScreen", new[]
            {
                // KEYS BEGIN
                "reallife_dynasty_app", "reallife_dynasty_bequest_lapsed", "reallife_dynasty_cannot_carry", "reallife_dynasty_charity_sub",
                "reallife_dynasty_chip_head", "reallife_dynasty_chip_heir", "reallife_dynasty_choice_heir", "reallife_dynasty_choice_no",
                "reallife_dynasty_choice_yes", "reallife_dynasty_city_note", "reallife_dynasty_discard", "reallife_dynasty_elder_age",
                "reallife_dynasty_elder_note", "reallife_dynasty_fee_own", "reallife_dynasty_fig_estate", "reallife_dynasty_fig_heir",
                "reallife_dynasty_fig_nobody", "reallife_dynasty_fig_tax", "reallife_dynasty_handing", "reallife_dynasty_handing_note",
                "reallife_dynasty_head_now", "reallife_dynasty_head_past", "reallife_dynasty_heir_rest", "reallife_dynasty_kin_note",
                "reallife_dynasty_kind_child", "reallife_dynasty_kind_friend", "reallife_dynasty_kind_partner", "reallife_dynasty_kind_relative",
                "reallife_dynasty_law_note", "reallife_dynasty_loan_note", "reallife_dynasty_mode_death", "reallife_dynasty_mode_elder",
                "reallife_dynasty_mode_retire", "reallife_dynasty_no_will", "reallife_dynasty_no_will_short", "reallife_dynasty_notary",
                "reallife_dynasty_off_body", "reallife_dynasty_off_title", "reallife_dynasty_person_sub", "reallife_dynasty_retire",
                "reallife_dynasty_retire_elder", "reallife_dynasty_retire_law", "reallife_dynasty_retire_no_heir", "reallife_dynasty_retire_no_will",
                "reallife_dynasty_retire_off", "reallife_dynasty_retire_ok", "reallife_dynasty_retire_running", "reallife_dynasty_retire_session",
                "reallife_dynasty_retire_young", "reallife_dynasty_rivals_none", "reallife_dynasty_row_age", "reallife_dynasty_row_bequest",
                "reallife_dynasty_row_cash", "reallife_dynasty_row_charity", "reallife_dynasty_row_city_last", "reallife_dynasty_row_city_now",
                "reallife_dynasty_row_city_tax", "reallife_dynasty_row_city_tax_value", "reallife_dynasty_row_city_unpaid", "reallife_dynasty_row_city_value",
                "reallife_dynasty_row_draw", "reallife_dynasty_row_fee", "reallife_dynasty_row_firm", "reallife_dynasty_row_fund",
                "reallife_dynasty_row_gifts", "reallife_dynasty_row_keep", "reallife_dynasty_row_left", "reallife_dynasty_row_left_value",
                "reallife_dynasty_row_net", "reallife_dynasty_row_other", "reallife_dynasty_row_owed", "reallife_dynasty_row_per_day",
                "reallife_dynasty_row_reserved", "reallife_dynasty_row_residue", "reallife_dynasty_row_retire_from", "reallife_dynasty_row_retire_tax",
                "reallife_dynasty_row_spouse", "reallife_dynasty_row_tax", "reallife_dynasty_row_tax_owed", "reallife_dynasty_row_threshold",
                "reallife_dynasty_row_upbringing", "reallife_dynasty_scale_note", "reallife_dynasty_sec_city", "reallife_dynasty_sec_elder",
                "reallife_dynasty_sec_kin", "reallife_dynasty_sec_line", "reallife_dynasty_sec_loan", "reallife_dynasty_sec_people",
                "reallife_dynasty_sec_retire", "reallife_dynasty_sec_retire_terms", "reallife_dynasty_sec_rivals", "reallife_dynasty_sec_sign",
                "reallife_dynasty_sec_statements", "reallife_dynasty_sec_today", "reallife_dynasty_sign", "reallife_dynasty_statement_sub",
                "reallife_dynasty_statement_title", "reallife_dynasty_statements_none", "reallife_dynasty_step_age", "reallife_dynasty_step_keep",
                "reallife_dynasty_step_share", "reallife_dynasty_tab_estate", "reallife_dynasty_tab_line", "reallife_dynasty_tab_will",
                "reallife_dynasty_under_water", "reallife_giving_charity_0_about", "reallife_giving_charity_0_name", "reallife_giving_charity_1_about",
                "reallife_giving_charity_1_name", "reallife_giving_charity_2_about", "reallife_giving_charity_2_name", "reallife_ui_prompt_back",
                "reallife_ui_prompt_move", "reallife_ui_prompt_select"
                // KEYS END
            }).Switch(UiScreenSwitches.Ventures));
        }
    }
}
