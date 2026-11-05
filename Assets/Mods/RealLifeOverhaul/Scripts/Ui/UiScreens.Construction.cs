using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Construction app: the site office's overview, land, projects, tenders, crews, the company, the rival builders and the city, with its own switch.
        static partial void AddConstruction(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("construction", UiTemplate.AppWindow, "ConstructionScreen", ConstructionKeys())
                .Switch(UiScreenSwitches.Construction));
        }

        // Every label the Construction screen writes (ConstructionLive asks for these and the keys of the old app's words it reuses).
        private static string[] ConstructionKeys()
        {
            return new[]
            {
                "reallife_bld_all_city", "reallife_bld_all_land", "reallife_bld_all_people", "reallife_bld_app", "reallife_bld_back",
                "reallife_bld_bid_note", "reallife_bld_button_bid", "reallife_bld_button_build_mode", "reallife_bld_button_buy_lot",
                "reallife_bld_button_city_hall", "reallife_bld_button_found", "reallife_bld_button_hire", "reallife_bld_button_land",
                "reallife_bld_button_map", "reallife_bld_button_money", "reallife_bld_button_release", "reallife_bld_button_start",
                "reallife_bld_city_note", "reallife_bld_col_bid", "reallife_bld_col_builder", "reallife_bld_col_built", "reallife_bld_col_closes",
                "reallife_bld_col_day", "reallife_bld_col_kind", "reallife_bld_col_morale", "reallife_bld_col_name", "reallife_bld_col_next",
                "reallife_bld_col_place", "reallife_bld_col_price", "reallife_bld_col_progress", "reallife_bld_col_project",
                "reallife_bld_col_reputation", "reallife_bld_col_size", "reallife_bld_col_skill", "reallife_bld_col_speciality",
                "reallife_bld_col_spent", "reallife_bld_col_stage", "reallife_bld_col_state", "reallife_bld_col_value", "reallife_bld_col_what",
                "reallife_bld_col_when", "reallife_bld_col_where", "reallife_bld_col_won", "reallife_bld_col_works", "reallife_bld_col_zoned",
                "reallife_bld_company_pick", "reallife_bld_crews_pick", "reallife_bld_delivery_coming", "reallife_bld_empty_projects",
                "reallife_bld_glance_client", "reallife_bld_glance_found", "reallife_bld_glance_title", "reallife_bld_headline",
                "reallife_bld_land_pick", "reallife_bld_licence_note", "reallife_bld_next_book_short", "reallife_bld_next_done",
                "reallife_bld_next_start", "reallife_bld_next_wait_decision", "reallife_bld_next_working", "reallife_bld_off_body",
                "reallife_bld_off_title", "reallife_bld_projects_pick", "reallife_bld_rivals_pick", "reallife_bld_row_closes",
                "reallife_bld_row_crews", "reallife_bld_row_decided", "reallife_bld_row_estimate", "reallife_bld_row_fee", "reallife_bld_row_hours",
                "reallife_bld_row_land_tax", "reallife_bld_row_morale", "reallife_bld_row_price", "reallife_bld_row_rate", "reallife_bld_row_size",
                "reallife_bld_row_skill", "reallife_bld_row_traffic", "reallife_bld_row_until", "reallife_bld_row_usual", "reallife_bld_row_value",
                "reallife_bld_row_zoned", "reallife_bld_sec_bid", "reallife_bld_sec_materials", "reallife_bld_sec_ready", "reallife_bld_source_city",
                "reallife_bld_stage_frame", "reallife_bld_stage_ready", "reallife_bld_switch_fast", "reallife_bld_tab_city",
                "reallife_bld_tab_company", "reallife_bld_tab_crews", "reallife_bld_tab_land", "reallife_bld_tab_overview",
                "reallife_bld_tab_projects", "reallife_bld_tab_rivals", "reallife_bld_tab_tenders", "reallife_bld_tenders_pick",
                "reallife_ui_cons_all_projects", "reallife_ui_cons_budgets", "reallife_ui_cons_cash_held", "reallife_ui_cons_close",
                "reallife_ui_cons_crews_title", "reallife_ui_cons_fig_a_day", "reallife_ui_cons_fig_bills", "reallife_ui_cons_fig_books",
                "reallife_ui_cons_fig_builders", "reallife_ui_cons_fig_crews", "reallife_ui_cons_fig_done", "reallife_ui_cons_fig_for_sale",
                "reallife_ui_cons_fig_lost", "reallife_ui_cons_fig_on_site", "reallife_ui_cons_fig_open", "reallife_ui_cons_fig_owed",
                "reallife_ui_cons_fig_rank", "reallife_ui_cons_fig_spent", "reallife_ui_cons_fig_standing", "reallife_ui_cons_fig_under_way",
                "reallife_ui_cons_fig_won", "reallife_ui_cons_fig_yours", "reallife_ui_cons_group_builder", "reallife_ui_cons_group_office",
                "reallife_ui_cons_head_sub", "reallife_ui_cons_lead_next", "reallife_ui_cons_lead_start", "reallife_ui_cons_money_title",
                "reallife_ui_cons_no_cost", "reallife_ui_cons_of", "reallife_ui_cons_open", "reallife_ui_cons_planning_title",
                "reallife_ui_cons_projects_title", "reallife_ui_cons_rep", "reallife_ui_cons_rivals_title", "reallife_ui_cons_spent_far",
                "reallife_ui_cons_state_done", "reallife_ui_cons_state_later", "reallife_ui_cons_state_next", "reallife_ui_cons_state_now",
                "reallife_ui_cons_step_building", "reallife_ui_cons_step_design", "reallife_ui_cons_step_done", "reallife_ui_cons_step_planning",
                "reallife_ui_cons_step_ready", "reallife_ui_cons_still_pay", "reallife_ui_cons_track", "reallife_ui_cons_week_title",
                "reallife_ui_cons_wk_council", "reallife_ui_cons_wk_delivery", "reallife_ui_cons_wk_inspect", "reallife_ui_cons_wk_opening",
                "reallife_ui_cons_workers", "reallife_ui_cons_x_of_y", "reallife_ui_prompt_back", "reallife_ui_prompt_move",
                "reallife_ui_prompt_select"
            };
        }
    }
}
