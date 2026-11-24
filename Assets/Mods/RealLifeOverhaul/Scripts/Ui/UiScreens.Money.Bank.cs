using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Every label the Banks screen writes (BanksBuild, BankParts and BanksLive ask for these and no others), then the shared lines.
        private static string[] BanksKeys()
        {
            return new[]
            {
                "reallife_util_day", "reallife_bank_apply_intro", "reallife_bank_apply_one_desk", "reallife_bank_card_elsewhere", "reallife_bank_card_offer", "reallife_bank_card_pay_minimum",
                "reallife_bank_current", "reallife_bank_current_elsewhere", "reallife_bank_current_here", "reallife_bank_daily_payment", "reallife_bank_off_body",
                "reallife_bank_off_title", "reallife_bank_overdraft_elsewhere", "reallife_bank_overdraft_offer", "reallife_bank_owed_here", "reallife_bank_rating_lends",
                "reallife_bank_rating_refuses", "reallife_bank_refused", "reallife_bank_row_card_quiet", "reallife_bank_row_no_loans", "reallife_bank_row_refused",
                "reallife_bank_row_refused_all", "reallife_bank_tab_accounts", "reallife_bank_tab_apply", "reallife_bank_tab_card", "reallife_bank_tab_loans",
                "reallife_bank_tab_rating", "reallife_banks_app", "reallife_collect_cannot", "reallife_collect_plan", "reallife_econ_account_arranged_pay",
                "reallife_econ_account_card_due", "reallife_econ_account_card_next", "reallife_econ_account_close", "reallife_econ_account_draw", "reallife_econ_account_pay",
                "reallife_econ_account_pay_off", "reallife_econ_account_pay_overdue", "reallife_econ_account_plan", "reallife_econ_approval_cancel",
                "reallife_econ_consolidate_do", "reallife_econ_finance_approve", "reallife_econ_kind_card", "reallife_econ_kind_overdraft", "reallife_econ_offer_amount",
                "reallife_econ_offer_drawn", "reallife_econ_offer_interest", "reallife_econ_offer_limit", "reallife_econ_offer_open", "reallife_econ_offer_payment",
                "reallife_econ_offer_rate", "reallife_econ_over_leaves", "reallife_econ_over_owed", "reallife_econ_rating_value", "reallife_econ_row_owed_of",
                "reallife_fin_col_day", "reallife_fin_col_owed", "reallife_fin_col_rate", "reallife_fin_col_state", "reallife_fin_col_when", "reallife_fin_l_deposit",
                "reallife_fin_l_first", "reallife_fin_l_interest_term", "reallife_fin_l_now", "reallife_fin_l_now_day", "reallife_fin_l_price", "reallife_fin_l_then",
                "reallife_fin_l_then_day", "reallife_fin_range", "reallife_fin_rate_year", "reallife_fin_step_deposit", "reallife_fin_step_term", "reallife_ibox_used",
                "reallife_money_apply", "reallife_money_col_balance", "reallife_money_col_in", "reallife_money_col_out", "reallife_money_col_what", "reallife_money_col_when",
                "reallife_money_headroom", "reallife_money_other_banks", "reallife_money_threshold", "reallife_tab_loan_left", "reallife_ui_bank_answer",
                "reallife_ui_bank_apply_at", "reallife_ui_bank_apply_empty", "reallife_ui_bank_balance", "reallife_ui_bank_bands", "reallife_ui_bank_checked",
                "reallife_ui_bank_chk_apps", "reallife_ui_bank_chk_limit", "reallife_ui_bank_chk_overdue", "reallife_ui_bank_chk_rating", "reallife_ui_bank_col_band",
                "reallife_ui_bank_col_car", "reallife_ui_bank_col_card", "reallife_ui_bank_col_daily", "reallife_ui_bank_col_debt", "reallife_ui_bank_col_drawn",
                "reallife_ui_bank_col_ends", "reallife_ui_bank_col_fee", "reallife_ui_bank_col_loan", "reallife_ui_bank_col_most", "reallife_ui_bank_col_next",
                "reallife_ui_bank_col_overdraft", "reallife_ui_bank_col_paid_in", "reallife_ui_bank_col_product", "reallife_ui_bank_col_score", "reallife_ui_bank_col_what",
                "reallife_ui_bank_customer", "reallife_ui_bank_debts", "reallife_ui_bank_elsewhere", "reallife_ui_bank_f_most", "reallife_ui_bank_f_owed",
                "reallife_ui_bank_f_rate", "reallife_ui_bank_f_rating", "reallife_ui_bank_f_since", "reallife_ui_bank_free", "reallife_ui_bank_house",
                "reallife_ui_bank_loans_at", "reallife_ui_bank_loans_empty_body", "reallife_ui_bank_moves_body", "reallife_ui_bank_moves_group", "reallife_ui_bank_new",
                "reallife_ui_bank_no", "reallife_ui_bank_no_debt", "reallife_ui_bank_no_history", "reallife_ui_bank_no_moves", "reallife_ui_bank_none_body",
                "reallife_ui_bank_none_title", "reallife_ui_bank_not_lending", "reallife_ui_bank_nothing", "reallife_ui_bank_opened", "reallife_ui_bank_owed_here",
                "reallife_ui_bank_pick_debt", "reallife_ui_bank_rank", "reallife_ui_bank_standing", "reallife_ui_bank_tab_overdraft", "reallife_ui_bank_yes",
                "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select"
            };
        }

        // The labels the newer pages of the Your bank screen write: the risk page, the cards tab, the desk tab and the dividends' next sum.
        private static string[] OwnBankGrowthKeys()
        {
            return new[]
            {
                "reallife_ob_card_applicants", "reallife_ob_card_applicants_value", "reallife_ob_card_approve", "reallife_ob_card_auto_head",
                "reallife_ob_card_auto_hint", "reallife_ob_card_balance", "reallife_ob_card_chip_issuing", "reallife_ob_card_col_balance", "reallife_ob_card_col_cards",
                "reallife_ob_card_col_issue", "reallife_ob_card_col_kind", "reallife_ob_card_col_limit", "reallife_ob_card_col_wait", "reallife_ob_card_costs",
                "reallife_ob_card_decline", "reallife_ob_card_decline_head", "reallife_ob_card_decline_hint", "reallife_ob_card_empty", "reallife_ob_card_f_book",
                "reallife_ob_card_f_cards", "reallife_ob_card_f_month", "reallife_ob_card_f_waiting", "reallife_ob_card_fee_head", "reallife_ob_card_fee_hint",
                "reallife_ob_card_fee_none", "reallife_ob_card_fee_option", "reallife_ob_card_fees", "reallife_ob_card_fraud", "reallife_ob_card_holders",
                "reallife_ob_card_holders_value", "reallife_ob_card_interchange", "reallife_ob_card_interest", "reallife_ob_card_issue_switch",
                "reallife_ob_card_limit_head", "reallife_ob_card_lost", "reallife_ob_card_note", "reallife_ob_card_queue", "reallife_ob_card_queue_note",
                "reallife_ob_card_rate_head", "reallife_ob_card_rate_option", "reallife_ob_card_rival", "reallife_ob_card_spend", "reallife_ob_card_switch",
                "reallife_ob_card_title", "reallife_ob_card_yours", "reallife_ob_desk_activity", "reallife_ob_desk_activity_value", "reallife_ob_desk_all",
                "reallife_ob_desk_cuts", "reallife_ob_desk_cuts_value", "reallife_ob_desk_derivs", "reallife_ob_desk_empty", "reallife_ob_desk_f_book",
                "reallife_ob_desk_f_commission", "reallife_ob_desk_f_custody", "reallife_ob_desk_f_drawdown", "reallife_ob_desk_limit", "reallife_ob_desk_limit_hint",
                "reallife_ob_desk_limit_option", "reallife_ob_desk_mandate", "reallife_ob_desk_mandate_hint", "reallife_ob_desk_market_off",
                "reallife_ob_desk_month_value", "reallife_ob_desk_never", "reallife_ob_desk_no_derivs", "reallife_ob_desk_note", "reallife_ob_desk_peak",
                "reallife_ob_desk_room", "reallife_ob_desk_share", "reallife_ob_desk_share_option", "reallife_ob_desk_shares", "reallife_ob_desk_switch",
                "reallife_ob_desk_title", "reallife_ob_desk_waiting", "reallife_ob_div_expected", "reallife_ob_div_expected_none", "reallife_ob_div_expected_value",
                "reallife_ob_div_tax", "reallife_ob_div_tax_note", "reallife_ob_div_tax_value", "reallife_ob_note_stress_off", "reallife_ob_note_stress_quiet",
                "reallife_ob_note_stress_signs", "reallife_ob_own_chip", "reallife_ob_own_close", "reallife_ob_own_intro", "reallife_ob_own_limit",
                "reallife_ob_own_limit_hint", "reallife_ob_own_lost", "reallife_ob_own_needs_capital", "reallife_ob_own_needs_cards", "reallife_ob_own_open",
                "reallife_ob_own_open_note", "reallife_ob_own_owed", "reallife_ob_own_rate", "reallife_ob_own_title", "reallife_ob_row_stress",
                "reallife_ob_sale_amount", "reallife_ob_sale_button", "reallife_ob_sale_head", "reallife_ob_sale_hint", "reallife_ob_sale_terms",
                "reallife_ob_stress_borrow", "reallife_ob_stress_borrow_button", "reallife_ob_stress_borrow_hint", "reallife_ob_stress_borrow_room",
                "reallife_ob_stress_borrow_room_value", "reallife_ob_stress_borrowed", "reallife_ob_stress_borrowed_none", "reallife_ob_stress_borrowed_value",
                "reallife_ob_stress_clear", "reallife_ob_stress_clear_hint", "reallife_ob_stress_conc_over", "reallife_ob_stress_cover",
                "reallife_ob_stress_cover_value", "reallife_ob_stress_no_signs", "reallife_ob_stress_off_body", "reallife_ob_stress_prudence",
                "reallife_ob_stress_ratio", "reallife_ob_stress_repay_button", "reallife_ob_stress_shock_next", "reallife_ob_stress_shock_now",
                "reallife_ob_stress_signs", "reallife_ob_stress_sold", "reallife_ob_stress_sold_value", "reallife_ob_stress_title", "reallife_ob_stress_warn_line",
                "reallife_ob_stress_warned", "reallife_ob_stress_watched", "reallife_ob_stress_wf", "reallife_ob_stress_wf_short", "reallife_ob_stress_wf_value",
                "reallife_ob_tab_cards", "reallife_ob_tab_desk", "reallife_ob_black_tier", "reallife_ob_black_wealth_head", "reallife_ob_black_wealth_hint",
                "reallife_ob_black_score_head", "reallife_ob_black_score_hint", "reallife_ob_black_fee_head", "reallife_ob_black_fee_hint", "reallife_ob_black_reward_head",
                "reallife_ob_black_reward_hint", "reallife_ob_black_rich", "reallife_ob_black_scores", "reallife_ob_black_switch", "reallife_ob_black_needs_cards",
                "reallife_ob_black_note"
            };
        }

        // Every label the Your bank screen writes (OwnBankBuild and BankParts ask for these and no others).
        private static string[] OwnBankKeys()
        {
            return Join(OwnBankGrowthKeys(), new[]
            {
                "reallife_ob_box_intro", "reallife_util_day", "reallife_branch_box_books", "reallife_branch_box_brings", "reallife_branch_box_brings_value", "reallife_branch_box_costs",
                    "reallife_branch_box_count",
                "reallife_branch_box_counter_note", "reallife_branch_box_fees", "reallife_branch_box_last", "reallife_branch_box_lending",
                "reallife_branch_box_lending_value", "reallife_branch_box_margin", "reallife_branch_box_money", "reallife_branch_box_money_value",
                "reallife_branch_box_month", "reallife_branch_box_none", "reallife_branch_box_off", "reallife_branch_box_pick", "reallife_branch_box_reach",
                "reallife_branch_box_reach_value", "reallife_branch_box_tellers", "reallife_branch_box_tellers_value", "reallife_branch_box_visitors_value",
                "reallife_branch_box_week", "reallife_branch_chip_closed", "reallife_branch_chip_open", "reallife_branch_col_branch", "reallife_branch_col_fees",
                "reallife_branch_col_served", "reallife_branch_col_visitors", "reallife_branch_empty", "reallife_branch_tab", "reallife_fin_chart_empty",
                "reallife_fin_chart_empty_body", "reallife_ibox_chip_closed", "reallife_ibox_chip_lending", "reallife_ibox_chip_sold", "reallife_ibox_chip_stopped",
                "reallife_ibox_licence", "reallife_money_col_balance", "reallife_money_col_in", "reallife_money_col_out", "reallife_money_col_what",
                "reallife_money_col_when", "reallife_money_fund_days", "reallife_ob_app", "reallife_ob_box_arrears", "reallife_ob_box_arrears_value",
                "reallife_ob_box_branches", "reallife_ob_box_closed", "reallife_ob_box_closed_terms", "reallife_ob_box_courts_off", "reallife_ob_box_courts_on",
                "reallife_ob_box_defaults", "reallife_ob_box_defaults_value", "reallife_ob_box_deposits", "reallife_ob_box_firms_note", "reallife_ob_box_found",
                "reallife_ob_box_fund_dividends", "reallife_ob_box_fund_room", "reallife_ob_box_fund_rules", "reallife_ob_box_fund_value", "reallife_ob_box_fund_value_value",
                "reallife_ob_box_hq", "reallife_ob_box_intro_level", "reallife_ob_box_last", "reallife_ob_box_last_value", "reallife_ob_box_lend_switch",
                "reallife_ob_box_lending_pick", "reallife_ob_box_lent", "reallife_ob_box_level_note", "reallife_ob_box_level_over", "reallife_ob_box_level_top",
                "reallife_ob_box_need_hq", "reallife_ob_box_no_lending", "reallife_ob_box_none_yet", "reallife_ob_box_people_note", "reallife_ob_box_put_in",
                "reallife_ob_box_rate_head", "reallife_ob_box_ratio", "reallife_ob_box_ratio_value", "reallife_ob_box_rival_rate", "reallife_ob_box_rivals",
                "reallife_ob_box_run", "reallife_ob_box_savers_intro", "reallife_ob_box_savers_pick", "reallife_ob_box_sell", "reallife_ob_box_sold",
                "reallife_ob_box_take_out", "reallife_ob_box_test", "reallife_ob_box_trust", "reallife_ob_box_trust_value", "reallife_ob_box_up_button",
                "reallife_ob_box_up_fee", "reallife_ob_box_up_holds", "reallife_ob_box_value", "reallife_ob_box_wanted", "reallife_ob_box_written",
                "reallife_ob_box_written_value", "reallife_ob_box_year", "reallife_ob_check_capital", "reallife_ob_check_record", "reallife_ob_check_regulator",
                "reallife_ob_col_amount", "reallife_ob_col_arrears", "reallife_ob_col_borrowers", "reallife_ob_col_company", "reallife_ob_col_cost",
                "reallife_ob_col_defaults", "reallife_ob_col_deposits", "reallife_ob_col_dividends", "reallife_ob_col_gain", "reallife_ob_col_item", "reallife_ob_col_lends",
                "reallife_ob_col_lent", "reallife_ob_col_note", "reallife_ob_col_rate", "reallife_ob_col_rivals", "reallife_ob_col_savers", "reallife_ob_col_share",
                "reallife_ob_col_shares", "reallife_ob_col_state", "reallife_ob_col_value", "reallife_ob_div_all", "reallife_ob_div_head", "reallife_ob_div_kept",
                "reallife_ob_div_last", "reallife_ob_div_last_value", "reallife_ob_div_next", "reallife_ob_div_share", "reallife_ob_div_title", "reallife_ob_empty_lending",
                "reallife_ob_empty_savers", "reallife_ob_figure_no_limit", "reallife_ob_figure_of", "reallife_ob_fund_limit_option", "reallife_ob_lends_no",
                "reallife_ob_lends_yes", "reallife_ob_level_title", "reallife_ob_meter_deposits", "reallife_ob_meter_loans", "reallife_ob_note_city",
                "reallife_ob_note_div_off", "reallife_ob_note_div_on", "reallife_ob_note_fund", "reallife_ob_note_level", "reallife_ob_note_level_top",
                "reallife_ob_note_pay_fixed", "reallife_ob_note_pay_none", "reallife_ob_note_pay_share", "reallife_ob_note_ratio", "reallife_ob_note_reserves",
                "reallife_ob_note_risk", "reallife_ob_note_share", "reallife_ob_note_written", "reallife_ob_off_body", "reallife_ob_off_title", "reallife_ob_pay_all",
                "reallife_ob_pay_day", "reallife_ob_pay_fixed_row", "reallife_ob_pay_head", "reallife_ob_pay_last", "reallife_ob_pay_mode_fixed",
                "reallife_ob_pay_mode_share", "reallife_ob_pay_month", "reallife_ob_pay_set", "reallife_ob_pay_share", "reallife_ob_pay_title", "reallife_ob_points",
                "reallife_ob_points_same", "reallife_ob_rate_option", "reallife_ob_row_arrears", "reallife_ob_row_capital", "reallife_ob_row_deposits", "reallife_ob_row_div",
                "reallife_ob_row_fund", "reallife_ob_row_level", "reallife_ob_row_loans", "reallife_ob_row_pay", "reallife_ob_row_reserves", "reallife_ob_row_risk",
                "reallife_ob_sec_level_next", "reallife_ob_state_running", "reallife_ob_state_saving", "reallife_ob_tab_bank", "reallife_ob_tab_fund",
                "reallife_ob_tab_lending", "reallife_ob_tab_savers", "reallife_ui_ob_books_empty", "reallife_ui_ob_capital_title", "reallife_ui_ob_costs",
                "reallife_ui_ob_costs_day", "reallife_ui_ob_div_note", "reallife_ui_ob_f_week", "reallife_ui_ob_found_capital", "reallife_ui_ob_fund_empty",
                "reallife_ui_ob_holds", "reallife_ui_ob_in_steps", "reallife_ui_ob_income", "reallife_ui_ob_kicker_new", "reallife_ui_ob_law", "reallife_ui_ob_limit",
                "reallife_ui_ob_month_title", "reallife_ui_ob_next", "reallife_ui_ob_now", "reallife_ui_ob_paid", "reallife_ui_ob_pay_note", "reallife_ui_ob_put",
                "reallife_ui_ob_reserve", "reallife_ui_ob_sell_not", "reallife_ui_ob_shares", "reallife_ui_ob_strategy", "reallife_ui_ob_take", "reallife_ui_ob_take_most",
                "reallife_ui_ob_take_none", "reallife_ui_ob_written", "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select"
            });
        }

        private static string[] Join(string[] first, string[] second)
        {
            var all = new string[first.Length + second.Length];
            first.CopyTo(all, 0);
            second.CopyTo(all, first.Length);
            return all;
        }
    }
}
