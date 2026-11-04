using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Finance, Banks, Your bank and Utilities (which has a face of its own, UtilitiesFace). The money section is its own, because the apps section is already full.
        // The shared labels are a method, not a field: a static field of another part of this class is not set yet
        // when All is gathered (the parts run their initialisers in no fixed order).
        static partial void AddMoney(List<UiScreenDef> screens)
        {
            string[] shared = MoneyShared();
            screens.Add(new UiScreenDef("finance", UiTemplate.AppWindow, "FinanceScreen", MoneyKeys(shared,
                "reallife_econ_app", "reallife_econ_tab_overview", "reallife_econ_tab_credit", "reallife_econ_tab_borrow", "reallife_econ_tab_cash",
                "reallife_econ_tab_accounts", "reallife_rival_title", "reallife_money_nav_rivals", "reallife_fame_title", "reallife_fin_col_debt",
                "reallife_fin_col_owed", "reallife_fin_col_rate", "reallife_fin_col_day", "reallife_fin_col_left", "reallife_fin_group_money",
                "reallife_fin_group_city", "reallife_fin_score",
                "reallife_fin_owe_when", "reallife_fin_by_kind", "reallife_fin_next", "reallife_fin_city", "reallife_fin_no_debts",
                "reallife_fin_best_today", "reallife_fin_see_offers", "reallife_fin_col_offer", "reallife_fin_col_event", "reallife_fin_col_when",
                "reallife_fin_col_points", "reallife_fin_your_rating", "reallife_fin_score_move", "reallife_econ_credit_raise",
                "reallife_fin_col_borrower", "reallife_fin_col_amount", "reallife_econ_offer_amount", "reallife_fin_costs_term",
                "reallife_econ_borrow_refused_title", "reallife_fin_col_account", "reallife_fin_col_next", "reallife_fin_col_state",
                "reallife_fin_amount", "reallife_fin_col_tycoon", "reallife_fin_col_worth", "reallife_fin_col_week", "reallife_fin_col_path",
                "reallife_fin_answer", "reallife_fame_col_what", "reallife_fame_col_stands", "reallife_fame_col_heading", "reallife_fin_f_owed",
                "reallife_fin_f_cash", "reallife_fin_f_worth"))
                .Switch(UiScreenSwitches.Money));
            screens.Add(new UiScreenDef("banks", UiTemplate.AppWindow, "BanksScreen", MoneyKeys(shared,
                "reallife_banks_app", "reallife_bank_tab_accounts", "reallife_bank_tab_loans", "reallife_bank_tab_card",
                "reallife_bank_tab_apply", "reallife_bank_tab_rating"))
                .Switch(UiScreenSwitches.Money));
            screens.Add(new UiScreenDef("own_bank", UiTemplate.AppWindow, "OwnBankScreen", MoneyKeys(shared,
                "reallife_ob_app", "reallife_ob_tab_bank", "reallife_ob_tab_savers", "reallife_ob_tab_lending",
                "reallife_ob_tab_fund", "reallife_branch_tab"))
                .Switch(UiScreenSwitches.Money));
            screens.Add(new UiScreenDef("utilities", UiTemplate.AppWindow, "UtilitiesScreen", UtilitiesKeys())
                .Switch(UiScreenSwitches.Money));
        }

        // Every label the Utilities screen writes (UtilitiesBuild asks for these and no others).
        private static string[] UtilitiesKeys()
        {
            return new[]
            {
                "reallife_bb_box_build", "reallife_bb_box_found", "reallife_bb_box_hq", "reallife_bb_box_intro", "reallife_bb_box_need_hq",
                "reallife_bb_box_sell", "reallife_bb_box_sold", "reallife_bb_box_title", "reallife_bb_col_district",
                "reallife_bb_confirm_build_body", "reallife_bb_confirm_build_title", "reallife_bb_confirm_found_body",
                "reallife_bb_confirm_found_title", "reallife_bb_confirm_sell_body", "reallife_bb_confirm_sell_title",
                "reallife_bb_sold_out", "reallife_endscale_bb_confirm_extend_body", "reallife_endscale_bb_confirm_extend_title",
                "reallife_endscale_bb_extend", "reallife_endscale_owed_line", "reallife_money_dig", "reallife_money_live",
                "reallife_net_box_buy", "reallife_net_box_cancel", "reallife_net_box_change", "reallife_net_box_contract_note",
                "reallife_net_box_contract_switch", "reallife_net_box_grace_place", "reallife_net_box_install_day",
                "reallife_net_box_install_fee", "reallife_net_box_install_free", "reallife_net_box_install_own", "reallife_net_box_needs",
                "reallife_net_box_needs_orders", "reallife_net_box_none_here", "reallife_net_box_reconnect", "reallife_net_box_switch_fee",
                "reallife_net_col_contract", "reallife_net_col_month", "reallife_net_col_plan", "reallife_net_col_provider",
                "reallife_net_empty", "reallife_net_headline", "reallife_net_needed", "reallife_net_needed_orders", "reallife_net_none",
                "reallife_net_state_outage", "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                "reallife_ui_util_a_month", "reallife_ui_util_account_sub", "reallife_ui_util_budget", "reallife_ui_util_business",
                "reallife_ui_util_cancel_connection", "reallife_ui_util_chart", "reallife_ui_util_chart_empty",
                "reallife_ui_util_chart_empty_body", "reallife_ui_util_closed", "reallife_ui_util_col_bill",
                "reallife_ui_util_col_customers", "reallife_ui_util_col_reached", "reallife_ui_util_col_speed",
                "reallife_ui_util_col_takeup", "reallife_ui_util_connected", "reallife_ui_util_connecting", "reallife_ui_util_day_left",
                "reallife_ui_util_days_left", "reallife_ui_util_deposit", "reallife_ui_util_dig_district", "reallife_ui_util_digging",
                "reallife_ui_util_districts", "reallife_ui_util_done", "reallife_ui_util_extend", "reallife_ui_util_fibre",
                "reallife_ui_util_fig_customers", "reallife_ui_util_fig_digging", "reallife_ui_util_fig_district",
                "reallife_ui_util_fig_licence", "reallife_ui_util_fig_profit", "reallife_ui_util_fig_takings",
                "reallife_ui_util_fig_upkeep", "reallife_ui_util_found_title", "reallife_ui_util_founded", "reallife_ui_util_free",
                "reallife_ui_util_from_price", "reallife_ui_util_gbit", "reallife_ui_util_get_choice", "reallife_ui_util_get_fibre",
                "reallife_ui_util_get_own", "reallife_ui_util_get_sell", "reallife_ui_util_gets", "reallife_ui_util_home",
                "reallife_ui_util_homes", "reallife_ui_util_kwh", "reallife_ui_util_last_bill", "reallife_ui_util_m3",
                "reallife_ui_util_maintenance", "reallife_ui_util_marketing", "reallife_ui_util_marketing_hint", "reallife_ui_util_mbit",
                "reallife_ui_util_months", "reallife_ui_util_no_accounts", "reallife_ui_util_no_hq", "reallife_ui_util_no_places",
                "reallife_ui_util_no_plans", "reallife_ui_util_none", "reallife_ui_util_not_connected", "reallife_ui_util_not_started",
                "reallife_ui_util_of_usual", "reallife_ui_util_outage_hint", "reallife_ui_util_overdue", "reallife_ui_util_paid",
                "reallife_ui_util_place_line", "reallife_ui_util_plan_basic", "reallife_ui_util_plan_fast",
                "reallife_ui_util_plan_gigabit", "reallife_ui_util_plan_line", "reallife_ui_util_plan_title", "reallife_ui_util_plans_at",
                "reallife_ui_util_prices", "reallife_ui_util_prices_hint", "reallife_ui_util_provider_plan", "reallife_ui_util_providers",
                "reallife_ui_util_range", "reallife_ui_util_rival_line", "reallife_ui_util_sale_value", "reallife_ui_util_set_plans",
                "reallife_ui_util_set_prices", "reallife_ui_util_sold_day", "reallife_ui_util_sold_title", "reallife_ui_util_state_first",
                "reallife_ui_util_state_live", "reallife_ui_util_state_live_dig", "reallife_ui_util_state_none",
                "reallife_ui_util_temporary", "reallife_ui_util_to_dig", "reallife_ui_util_total_live", "reallife_ui_util_unpaid",
                "reallife_ui_util_used", "reallife_ui_util_your_plan", "reallife_ui_util_your_plans", "reallife_util_app",
                "reallife_util_box_apply", "reallife_util_box_apply_cut", "reallife_util_box_apply_temporary", "reallife_util_box_debit",
                "reallife_util_box_deposit_value", "reallife_util_box_last_value", "reallife_util_box_nothing_owed",
                "reallife_util_box_office", "reallife_util_box_office_app", "reallife_util_box_owed_value", "reallife_util_box_pay",
                "reallife_util_box_reconnect", "reallife_util_col_debit", "reallife_util_col_due", "reallife_util_col_month",
                "reallife_util_col_owed", "reallife_util_col_place", "reallife_util_col_state", "reallife_util_day",
                "reallife_util_debit_off", "reallife_util_debit_on", "reallife_util_desk_apply_cost", "reallife_util_desk_apply_free",
                "reallife_util_empty", "reallife_util_headline", "reallife_util_internet", "reallife_util_off_body",
                "reallife_util_off_title", "reallife_util_power", "reallife_util_state_cut", "reallife_util_tab_accounts",
                "reallife_util_tab_internet", "reallife_util_tab_network", "reallife_util_water"
            };
        }

        // Every label any of the three money samples asks for, so a shared line is named on each screen that can show it.
        private static string[] MoneyKeys(string[] shared, params string[] own)
        {
            var keys = new string[own.Length + shared.Length];
            for (int i = 0; i < own.Length; i++)
            {
                keys[i] = own[i];
            }
            for (int i = 0; i < shared.Length; i++)
            {
                keys[own.Length + i] = shared[i];
            }
            return keys;
        }

        private static string[] MoneyShared()
        {
            return new[]
            {
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_prompt_choose",
                "reallife_money_take", "reallife_money_apply", "reallife_money_export", "reallife_money_follow",
                "reallife_money_economy", "reallife_money_leaves", "reallife_money_all", "reallife_money_adds",
                "reallife_money_feed", "reallife_money_feed_empty", "reallife_money_headroom", "reallife_money_cover",
                "reallife_money_cut", "reallife_money_campaign", "reallife_money_hold", "reallife_money_forecast",
                "reallife_money_expanding", "reallife_money_worth", "reallife_money_cash_empty", "reallife_money_brought",
                "reallife_money_coming", "reallife_money_where", "reallife_money_threshold", "reallife_money_since",
                "reallife_money_other_banks", "reallife_money_month", "reallife_money_fund_days", "reallife_money_biggest",
                "reallife_money_customers", "reallife_money_dig", "reallife_money_live", "reallife_money_plan",
                "reallife_money_empty_debts", "reallife_money_empty_loans", "reallife_money_col_when",
                "reallife_money_col_what", "reallife_money_col_in", "reallife_money_col_out", "reallife_money_col_balance",
                "reallife_money_you", "reallife_money_owed", "reallife_money_cash", "reallife_money_locate"
            };
        }
    }
}
