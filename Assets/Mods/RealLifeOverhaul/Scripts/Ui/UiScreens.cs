namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's templates: every screen is drawn by one of them.</summary>
    public enum UiTemplate : byte
    {
        ListDetail = 0,   // groups, list and detail (Contacts)
        Dashboard,        // figures over time (the Economy app's views)
        Session,          // a desk session's steps and choices
        Form,             // settings and choices before an action
        Gallery           // the kit's own gallery
    }

    /// <summary>One registered screen: its id, its template, the class that runs it, and the words its labels use.</summary>
    public sealed class UiScreenDef
    {
        public readonly string Id;
        public readonly UiTemplate Template;
        public readonly string Runner;      // the IUiScreen class that runs it in the game, or null for a template's sample alone
        public readonly string[] Keys;      // the locale keys of its labels: the mod's own (reallife_) and the game's

        public UiScreenDef(string id, UiTemplate template, string runner, string[] keys)
        {
            Id = id;
            Template = template;
            Runner = runner;
            Keys = keys;
        }
    }

    /// <summary>
    /// The one list of the kit's screens (docs/standards/ui.md, RLO-UI-005): every screen the mod draws on the kit is here
    /// with its template and the keys of its labels, and every class that runs a kit screen is named by one entry
    /// (StandardCheck holds the two together). EconomyCheck lays each one out at every frame the game can give it, in
    /// both looks and all eleven languages, and checks the layout; the templates' own samples stand here too, so the
    /// shape the next screens take (the Economy app's population view, the studio's pages, the desk sessions) is checked
    /// before they are built. Game-free. Cost: a static array.
    /// </summary>
    public static class UiScreens
    {
        public static readonly UiScreenDef[] All =
        {
            new UiScreenDef("contacts", UiTemplate.ListDetail, "ContactsScreen", new[]
            {
                "reallife_ui_contacts_app", "reallife_ui_contacts_groups", "reallife_ui_contacts_all", "reallife_ui_contacts_search",
                "reallife_ui_contacts_none_title", "reallife_ui_contacts_none_body", "reallife_ui_contacts_quiet_title", "reallife_ui_contacts_quiet_body",
                "reallife_ui_contacts_start", "reallife_ui_contacts_classic", "reallife_ui_contacts_message", "reallife_ui_contacts_send",
                "reallife_ui_contacts_requests", "reallife_ui_contacts_profile", "reallife_ui_contacts_details", "reallife_ui_contacts_pick_title",
                "reallife_ui_contacts_pick_body", "reallife_ui_contacts_new", "reallife_ui_contacts_earlier", "reallife_ui_contacts_summary",
                "reallife_ui_contacts_day", "reallife_ui_price_free", "reallife_ui_price_quote", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                "reallife_ui_prompt_group", "reallife_ui_prompt_back", "contacts_mark_all_as_read_button", "contacts_delete_all_button", "phone_call_button",
                "phone_view_on_map_button", "contacts_manage_employee_button", "contacts_manage_delivery_button", "myemployees_manage_schedule",
                "contacts_candidates_button", "contacts_rival_button", "contacts_tax_button", "contacts_header_clear_chat_button",
                "contacts_today_opening_hours", "common_address", "contacts_schedule_demands", "myemployees_hourlywage",
                "bizman_headhunters_skill_level_title", "contactcategoryname_general", "contactcategoryname_business", "contactcategoryname_finance",
                "contactcategoryname_furnitureandequipment", "contactcategoryname_importsandgoods", "contactcategoryname_employees",
                "contactcategoryname_rivals"
            }),
            new UiScreenDef("gallery", UiTemplate.Gallery, "UiGalleryScreen", new[]
            {
                "reallife_ui_gallery_title", "reallife_ui_gallery_page_controls", "reallife_ui_gallery_page_lists", "reallife_ui_gallery_page_figures",
                "reallife_ui_gallery_page_messages", "reallife_ui_gallery_page_question", "reallife_ui_gallery_page_call", "reallife_ui_gallery_cap_buttons",
                "reallife_ui_gallery_cap_nav", "reallife_ui_gallery_cap_inputs", "reallife_ui_gallery_cap_rows", "reallife_ui_gallery_cap_cards",
                "reallife_ui_gallery_cap_data", "reallife_ui_gallery_cap_figures", "reallife_ui_gallery_cap_stats", "reallife_ui_gallery_cap_status",
                "reallife_ui_gallery_cap_thread", "reallife_ui_gallery_cap_states", "reallife_ui_gallery_armed", "reallife_ui_gallery_chip",
                "reallife_ui_gallery_disabled", "reallife_ui_gallery_tab_city", "reallife_ui_gallery_tab_district", "reallife_ui_gallery_tab_people",
                "reallife_ui_gallery_toggle_on", "reallife_ui_gallery_toggle_off", "reallife_ui_gallery_slider", "reallife_ui_gallery_choice",
                "reallife_ui_gallery_choice_motion", "reallife_ui_gallery_query", "reallife_ui_gallery_step", "reallife_ui_gallery_status_quiet",
                "reallife_ui_gallery_status_warn", "reallife_ui_gallery_status_danger", "reallife_ui_gallery_busy", "reallife_ui_gallery_question_title",
                "reallife_ui_gallery_question_body", "reallife_ui_gallery_keep", "reallife_ui_gallery_connected", "reallife_ui_gallery_end",
                "reallife_ui_gallery_toast_title", "reallife_ui_gallery_group_work", "reallife_ui_gallery_group_family", "reallife_ui_gallery_group_shops",
                "reallife_ui_gallery_group_services", "reallife_ui_gallery_person_1", "reallife_ui_gallery_person_2", "reallife_ui_gallery_person_3",
                "reallife_ui_gallery_person_4", "reallife_ui_gallery_person_5", "reallife_ui_gallery_person_6", "reallife_ui_gallery_person_7",
                "reallife_ui_gallery_line_1", "reallife_ui_gallery_line_2", "reallife_ui_gallery_line_3", "reallife_ui_gallery_line_4",
                "reallife_ui_gallery_line_5", "reallife_ui_gallery_line_6", "reallife_ui_gallery_line_7", "reallife_ui_gallery_service_1",
                "reallife_ui_gallery_service_1_about", "reallife_ui_gallery_service_2", "reallife_ui_gallery_service_2_about", "reallife_ui_gallery_address",
                "reallife_ui_gallery_reply_1", "reallife_ui_gallery_reply_2", "reallife_ui_gallery_message", "reallife_ui_gallery_draft",
                "reallife_ui_gallery_col_name", "reallife_ui_gallery_col_skill", "reallife_ui_gallery_out_of_work", "reallife_ui_gallery_rent",
                "reallife_ui_gallery_satisfaction", "reallife_ui_gallery_energy", "reallife_ui_contacts_send", "reallife_ui_contacts_all",
                "reallife_ui_contacts_search", "reallife_ui_contacts_new", "reallife_ui_contacts_earlier", "reallife_ui_contacts_on_shift",
                "reallife_ui_contacts_quiet_title", "reallife_ui_contacts_quiet_body", "reallife_ui_price_quote", "reallife_flare_theme_2",
                "reallife_ui_motion_1", "reallife_session_meeting_morale", "reallife_session_meeting_morale_trade", "reallife_session_meeting_push",
                "reallife_session_meeting_push_trade", "reallife_ui_prompt_page", "reallife_ui_prompt_look", "reallife_ui_prompt_move",
                "reallife_ui_prompt_back", "phone_call_button", "phone_view_on_map_button", "contacts_delete_all_button", "contactcategoryname_rivals",
                "common_address", "myemployees_hourlywage", "contacts_today_opening_hours", "bizman_headhunters_skill_level_title", "reallife_ui_gallery_cap_type",
                "reallife_ui_gallery_cap_colours", "reallife_ui_gallery_cap_motion", "reallife_ui_gallery_cap_sizes", "reallife_ui_gallery_col_style",
                "reallife_ui_gallery_col_face", "reallife_ui_gallery_col_size", "reallife_ui_gallery_col_weight", "reallife_ui_gallery_col_role",
                "reallife_ui_gallery_col_dark", "reallife_ui_gallery_col_light", "reallife_ui_gallery_col_motion", "reallife_ui_gallery_col_full",
                "reallife_ui_gallery_col_calm", "reallife_ui_gallery_col_token", "reallife_ui_gallery_col_px", "reallife_ui_gallery_col_kind"
            }),
            new UiScreenDef("population", UiTemplate.Dashboard, "PopulationScreen", new[]
            {
                "reallife_ui_page_details", "reallife_ui_page_empty", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                "reallife_pop_view_city", "reallife_ui_pop_household", "reallife_ui_pop_household_plain", "reallife_ui_pop_last_month", "reallife_ui_pop_home"
            }),
            new UiScreenDef("flare_city", UiTemplate.Dashboard, "FlareCityScreen", new[]
            {
                "reallife_ui_flare_tab_account", "reallife_ui_flare_tab_trends", "reallife_ui_flare_tab_offers", "reallife_ui_page_details", "reallife_ui_page_empty",
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_page", "reallife_ui_prompt_back"
            }),
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
                "reallife_studio_genre_simulation", "reallife_studio_genre_strategy"
            }),
            new UiScreenDef("session_panel", UiTemplate.Session, "ActivityScreen", new[]
            {
                "reallife_ui_page_desk", "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "reallife_ui_prompt_back"
            }),
            new UiScreenDef("template_dashboard", UiTemplate.Dashboard, null, new[]
            {
                "reallife_ui_prompt_move", "reallife_ui_prompt_page", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
            }),
            new UiScreenDef("template_session", UiTemplate.Session, null, new[]
            {
                "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "reallife_ui_prompt_back", "reallife_session_button_hand", "reallife_session_button_begin"
            }),
            new UiScreenDef("template_form", UiTemplate.Form, null, new[]
            {
                "reallife_ui_prompt_move", "reallife_ui_prompt_change", "reallife_ui_prompt_back"
            }),
        };

        /// <summary>A registered screen by its id, or null.</summary>
        public static UiScreenDef Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }
    }
}
