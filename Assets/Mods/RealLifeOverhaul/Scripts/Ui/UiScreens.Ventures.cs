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
        }
    }
}
