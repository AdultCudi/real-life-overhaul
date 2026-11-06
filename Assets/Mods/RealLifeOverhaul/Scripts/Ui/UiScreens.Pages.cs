using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The overhaul's own pages: the city's people, the tech firms and the desk session's panel.
        static partial void AddPages(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("population", UiTemplate.AppWindow, "PopulationScreen", new[]
                {
                    "reallife_ui_page_details", "reallife_ui_page_empty", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_ui_pop_household", "reallife_ui_pop_household_plain", "reallife_ui_pop_last_month", "reallife_ui_pop_home", "reallife_ui_pop_age",
                    "reallife_ui_pop_gone_body", "reallife_ui_pop_gone_named", "reallife_pop_view_city", "reallife_pop_view_contributors", "reallife_pop_fig_people",
                    "reallife_pop_fig_households", "reallife_pop_fig_unemployment", "reallife_pop_fig_rent", "reallife_pop_fig_births", "reallife_pop_fig_deaths",
                    "reallife_pop_fig_moved_in", "reallife_pop_fig_moved_out", "reallife_pop_fig_jobs", "reallife_pop_fig_vacancies", "reallife_pop_fig_commuters",
                    "reallife_pop_fig_taxes", "reallife_pop_fig_vacant", "reallife_pop_fig_income", "reallife_pop_fig_flats", "reallife_pop_fig_free",
                    "reallife_pop_fig_flat_rent", "reallife_pop_fig_flat_size", "reallife_pop_fig_earned", "reallife_pop_fig_tax_paid", "reallife_pop_fig_spent",
                    "reallife_pop_fig_saved", "reallife_pop_fig_rent_paid", "reallife_pop_fig_landlord", "reallife_pop_fig_savings", "reallife_pop_fig_debt",
                    "reallife_pop_fig_top_spend", "reallife_pop_fig_age", "reallife_pop_fig_pay", "reallife_pop_fig_hours", "reallife_pop_fig_education",
                    "reallife_pop_fig_field", "reallife_pop_fig_value_added", "reallife_pop_fig_customers", "reallife_pop_fig_takings", "reallife_pop_fig_against_first",
                    "reallife_pop_fig_working", "reallife_pop_fig_middle", "reallife_pop_fig_upper", "reallife_pop_fig_month", "reallife_pop_fig_sales_tax",
                    "reallife_pop_fig_wage_bill", "reallife_pop_fig_residents", "reallife_pop_fig_your_people", "reallife_pop_fig_your_spending",
                    "reallife_pop_fig_your_taxes", "reallife_pop_fig_top_district", "reallife_pop_rows_districts", "reallife_pop_rows_buildings",
                    "reallife_pop_rows_households", "reallife_pop_rows_members", "reallife_pop_rows_from", "reallife_pop_rows_employers",
                    "reallife_pop_act_contributors", "reallife_pop_act_busiest", "reallife_pop_act_where", "reallife_pop_act_employer", "reallife_pop_act_household",
                    "reallife_pop_act_staff", "reallife_pop_act_top_district", "reallife_pop_empty_city", "reallife_pop_empty_district", "reallife_pop_empty_building",
                    "reallife_pop_empty_household", "reallife_pop_empty_person", "reallife_pop_empty_business", "reallife_pop_empty_contributors",
                    "reallife_pop_class_working", "reallife_pop_class_middle", "reallife_pop_class_upper", "reallife_pop_status_child", "reallife_pop_status_pupil",
                    "reallife_pop_status_student", "reallife_pop_status_worker", "reallife_pop_status_unemployed", "reallife_pop_status_retired",
                    "reallife_pop_status_home", "reallife_pop_education_none", "reallife_pop_education_school", "reallife_pop_education_college",
                    "reallife_pop_education_degree", "reallife_pop_education_postgraduate", "reallife_pop_field_none", "reallife_pop_field_service",
                    "reallife_pop_field_trades", "reallife_pop_field_office", "reallife_pop_field_professional", "reallife_pop_field_tech",
                    "reallife_pop_field_creative", "reallife_pop_field_public", "reallife_pop_field_management", "reallife_pop_landlord_own",
                    "reallife_pop_landlord_player", "reallife_pop_landlord_city", "reallife_pop_employer_business", "reallife_pop_employer_tenants",
                    "reallife_pop_employer_public", "reallife_pop_employer_special", "reallife_pop_employer_building", "reallife_pop_employer_mod",
                    "reallife_pop_fig_homes_wanted", "reallife_pop_fig_homes_started", "reallife_pop_fig_homes_built", "reallife_pop_fig_firms_opened",
                    "reallife_pop_fig_firms_closed", "reallife_pop_rows_lives_with", "reallife_pop_empty_alone"
                }).Switch(UiScreenSwitches.Apps),
                new UiScreenDef("studio", UiTemplate.Dashboard, "StudioPageScreen", new[]
                {
                    "reallife_ui_page_empty", "reallife_ui_prompt_move", "reallife_ui_prompt_back", "reallife_studio_page_games_out", "reallife_studio_page_copies",
                    "reallife_studio_page_takings", "reallife_studio_page_col_game", "reallife_studio_page_col_genre", "reallife_studio_page_col_score",
                    "reallife_studio_page_col_copies", "reallife_studio_page_col_kept", "reallife_studio_page_col_status", "reallife_studio_page_no_games",
                    "reallife_studio_page_col_rival", "reallife_studio_page_col_studio", "reallife_studio_studio_rivals_none", "reallife_studio_page_team",
                    "reallife_studio_page_group_count", "reallife_studio_page_needed", "reallife_studio_page_group_engineers", "reallife_studio_page_group_designers",
                    "reallife_studio_page_group_testers", "reallife_studio_page_group_product", "reallife_studio_page_group_data", "reallife_studio_page_group_security",
                    "reallife_studio_page_group_infrastructure", "reallife_studio_page_group_support", "reallife_studio_page_group_sales",
                    "reallife_studio_page_group_business", "reallife_studio_page_facts", "reallife_studio_page_tickets", "reallife_studio_page_pirated",
                    "reallife_studio_page_drm", "reallife_studio_page_asks", "reallife_studio_page_engine", "reallife_studio_page_trend", "reallife_studio_page_known",
                    "reallife_tech_mkt_page_products", "reallife_tech_mkt_page_customers", "reallife_tech_mkt_page_contracts", "reallife_tech_mkt_page_offers",
                    "reallife_tech_mkt_page_takings", "reallife_tech_mkt_page_col_product", "reallife_tech_mkt_page_col_kind", "reallife_tech_mkt_page_col_customers",
                    "reallife_tech_mkt_page_col_takings", "reallife_tech_mkt_page_col_status", "reallife_tech_mkt_page_no_products", "reallife_tech_mkt_page_col_rival",
                    "reallife_tech_mkt_page_col_market", "reallife_tech_mkt_page_col_features", "reallife_tech_mkt_page_no_rivals", "reallife_tech_mkt_page_col_work",
                    "reallife_tech_mkt_page_col_client", "reallife_tech_mkt_page_col_value", "reallife_tech_mkt_page_col_due", "reallife_tech_mkt_page_no_work",
                    "reallife_tech_mkt_page_col_done", "reallife_tech_mkt_page_col_paid", "reallife_tech_mkt_page_col_stage", "reallife_tech_mkt_page_no_done",
                    "reallife_tech_mkt_page_facts", "reallife_tech_mkt_page_support", "reallife_tech_mkt_page_limit", "reallife_tech_mkt_page_offers_month",
                    "reallife_tech_mkt_page_desk", "reallife_tech_mkt_page_reputation", "reallife_tech_mkt_market_businesssoftware", "reallife_tech_mkt_market_apps",
                    "reallife_ui_sample_firm", "reallife_ui_sample_firm_note", "reallife_ui_sample_engine", "reallife_studio_genre_adventure",
                    "reallife_studio_genre_simulation", "reallife_studio_genre_strategy", "reallife_studio_next_hire", "reallife_studio_page_needed_how",
                    "reallife_studio_page_no_games_hire", "reallife_studio_page_age_days", "reallife_tech_mkt_page_building", "reallife_tech_mkt_page_ready"
                }).Switch(UiScreenSwitches.Apps),
                new UiScreenDef("session_panel", UiTemplate.Decision, "ActivityScreen", new[]
                {
                    "reallife_ui_page_desk", "reallife_session_enabled", "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Apps),
            });
        }
    }
}
