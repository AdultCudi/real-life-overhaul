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
                new UiScreenDef("population", UiTemplate.Dashboard, "PopulationScreen", new[]
                {
                    "reallife_ui_page_details", "reallife_ui_page_empty", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_pop_view_city", "reallife_ui_pop_household", "reallife_ui_pop_household_plain", "reallife_ui_pop_last_month", "reallife_ui_pop_home",
                    "reallife_ui_pop_age", "reallife_ui_pop_gone_body", "reallife_ui_pop_gone_named", "reallife_pop_fig_field", "reallife_pop_landlord_city"
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
