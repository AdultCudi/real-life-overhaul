using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Company app on the kit: seven tabs and their pages (Company/CompanyFace). Its switch stands in its own section, the apps' section being full.
        // The key list is every label its face and its samples write with (tools/harvest: the face's w.T and w.F calls), so a check can see that each is named here.
        static partial void AddCompany(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("company", UiTemplate.AppWindow, "CompanyScreen", new[]
            {
                // KEYS BEGIN
                "reallife_co_after_day", "reallife_co_all_businesses", "reallife_co_all_count", "reallife_co_also_waiting", "reallife_co_and_more",
                "reallife_co_app", "reallife_co_back", "reallife_co_business", "reallife_co_chip_open", "reallife_co_chip_waiting",
                "reallife_co_col_business", "reallife_co_col_costs", "reallife_co_col_result", "reallife_co_col_takings", "reallife_co_collapse_all",
                "reallife_co_coming_up", "reallife_co_cost_marketing", "reallife_co_cost_rent", "reallife_co_cost_stock", "reallife_co_cost_wages",
                "reallife_co_cost_wages_staff", "reallife_co_costs_total", "reallife_co_costs_went", "reallife_co_cover_fire_theft",
                "reallife_co_d_courier", "reallife_co_d_moved", "reallife_co_d_prices", "reallife_co_d_reorder", "reallife_co_d_turned",
                "reallife_co_decide_later", "reallife_co_decided_asked", "reallife_co_done_none", "reallife_co_done_today", "reallife_co_expand_all",
                "reallife_co_f_profit", "reallife_co_f_staff", "reallife_co_f_standing", "reallife_co_f_takings", "reallife_co_group_company",
                "reallife_co_group_money", "reallife_co_head_line", "reallife_co_licences_held", "reallife_co_managers_log",
                "reallife_co_managers_today", "reallife_co_mgr_none_body", "reallife_co_mgr_none_title", "reallife_co_nav_tax",
                "reallife_co_needs_you", "reallife_co_no_books", "reallife_co_no_cost", "reallife_co_no_managers", "reallife_co_none",
                "reallife_co_not_yet", "reallife_co_off", "reallife_co_off_body", "reallife_co_off_title", "reallife_co_on", "reallife_co_open",
                "reallife_co_open_bank", "reallife_co_open_bizman", "reallife_co_open_cleaning", "reallife_co_open_managers",
                "reallife_co_open_page", "reallife_co_operations", "reallife_co_part_off", "reallife_co_part_off_title", "reallife_co_pick_body",
                "reallife_co_pick_title", "reallife_co_prompt_later", "reallife_co_q_ask_again", "reallife_co_q_asks_for", "reallife_co_q_bonus_do",
                "reallife_co_q_c_bonus", "reallife_co_q_c_haggle", "reallife_co_q_c_hold", "reallife_co_q_c_limit", "reallife_co_q_c_no",
                "reallife_co_q_c_raise", "reallife_co_q_c_renew", "reallife_co_q_cashier_days", "reallife_co_q_covered", "reallife_co_q_fewer",
                "reallife_co_q_haggle_do", "reallife_co_q_haggle_line", "reallife_co_q_hold_do", "reallife_co_q_hold_line",
                "reallife_co_q_kicker_manager", "reallife_co_q_lease_body", "reallife_co_q_lease_head", "reallife_co_q_lease_title",
                "reallife_co_q_limit", "reallife_co_q_limit_do", "reallife_co_q_limit_line", "reallife_co_q_manager_asks", "reallife_co_q_may_leave",
                "reallife_co_q_no_do", "reallife_co_q_nocost_note", "reallife_co_q_now", "reallife_co_q_now_note", "reallife_co_q_once_note",
                "reallife_co_q_raise_body", "reallife_co_q_raise_do", "reallife_co_q_raise_head", "reallife_co_q_raise_title",
                "reallife_co_q_renew_do", "reallife_co_q_rent_now", "reallife_co_q_rent_offer", "reallife_co_q_rise_limit",
                "reallife_co_q_standing_note", "reallife_co_q_stays", "reallife_co_q_term", "reallife_co_q_today", "reallife_co_q_year_more",
                "reallife_co_quiet_body", "reallife_co_quiet_title", "reallife_co_r_campaign", "reallife_co_r_cover",
                "reallife_co_r_district_income", "reallife_co_r_hidden", "reallife_co_r_licences", "reallife_co_r_manager",
                "reallife_co_result_label", "reallife_co_sec_do", "reallife_co_sec_state", "reallife_co_sell", "reallife_co_shop_where",
                "reallife_co_tab_cover", "reallife_co_tab_managers", "reallife_co_tab_marketing", "reallife_co_tab_overview", "reallife_co_tab_rota",
                "reallife_co_tab_staff", "reallife_co_the_day", "reallife_co_w_lease", "reallife_co_w_licence", "reallife_co_w_licence_line",
                "reallife_co_w_order", "reallife_co_w_rota", "reallife_co_w_rota_line", "reallife_co_who_decides", "reallife_hier_do_pass",
                "reallife_hier_do_try", "reallife_rota_advised", "reallife_rota_edit", "reallife_rota_hours", "reallife_rota_most_hours",
                "reallife_rota_off", "reallife_rota_overtime", "reallife_rota_person", "reallife_rota_pick_body", "reallife_rota_pick_title",
                "reallife_rota_plan", "reallife_rota_repeats", "reallife_rota_staff_hours", "reallife_rota_the_week", "reallife_rota_view_advice",
                "reallife_rota_view_day", "reallife_rota_view_week", "reallife_ui_prompt_back", "reallife_ui_prompt_move",
                "reallife_ui_prompt_select"
                // KEYS END
            }).Switch(UiScreenSwitches.Company));
        }
    }
}
