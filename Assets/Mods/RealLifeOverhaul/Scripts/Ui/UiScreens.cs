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
                "reallife_ui_contacts_app", "reallife_ui_contacts_groups", "reallife_ui_contacts_all", "reallife_ui_contacts_search", "reallife_ui_contacts_none_title",
                "reallife_ui_contacts_none_body", "reallife_ui_contacts_clear_note", "reallife_ui_contacts_quiet_title", "reallife_ui_contacts_quiet_body",
                "reallife_ui_contacts_classic", "reallife_ui_contacts_no_reply", "reallife_ui_contacts_requests", "reallife_ui_contacts_services",
                "reallife_ui_contacts_profile", "reallife_ui_contacts_details", "reallife_ui_contacts_pick_title", "reallife_ui_contacts_pick_body",
                "reallife_ui_contacts_pinned", "reallife_ui_contacts_pin", "reallife_ui_contacts_unpin", "reallife_ui_contacts_phone", "reallife_ui_contacts_group",
                "reallife_ui_contacts_open", "reallife_ui_contacts_closed", "reallife_ui_contacts_summary", "reallife_ui_price_free", "reallife_ui_price_quote",
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_group", "reallife_ui_prompt_back",
                "contacts_mark_all_as_read_button", "contacts_delete_all_button", "phone_call_button", "phone_view_on_map_button", "contacts_manage_employee_button",
                "contacts_manage_delivery_button", "myemployees_manage_schedule", "contacts_candidates_button", "contacts_rival_button", "contacts_tax_button",
                "contacts_header_clear_chat_button", "contacts_today_opening_hours", "common_address", "contacts_schedule_demands", "myemployees_hourlywage",
                "bizman_headhunters_skill_level_title"
            }),
            new UiScreenDef("gallery", UiTemplate.Gallery, "UiGalleryScreen", new[]
            {
                "reallife_ui_gallery_title", "reallife_ui_gallery_page_controls", "reallife_ui_gallery_page_lists", "reallife_ui_gallery_page_figures",
                "reallife_ui_gallery_page_messages", "reallife_ui_gallery_page_question", "reallife_ui_gallery_page_call", "reallife_ui_gallery_cap_buttons",
                "reallife_ui_gallery_cap_nav", "reallife_ui_gallery_cap_inputs", "reallife_ui_gallery_cap_rows", "reallife_ui_gallery_cap_cards",
                "reallife_ui_gallery_cap_data", "reallife_ui_gallery_cap_figures", "reallife_ui_gallery_cap_stats", "reallife_ui_gallery_cap_status",
                "reallife_ui_gallery_cap_thread", "reallife_ui_gallery_cap_states", "reallife_ui_gallery_secondary", "reallife_ui_gallery_armed",
                "reallife_ui_gallery_chip", "reallife_ui_gallery_disabled", "reallife_ui_gallery_tab_city", "reallife_ui_gallery_tab_district",
                "reallife_ui_gallery_tab_people", "reallife_ui_gallery_toggle_on", "reallife_ui_gallery_toggle_off", "reallife_ui_gallery_slider",
                "reallife_ui_gallery_choice", "reallife_ui_gallery_step", "reallife_ui_gallery_status_quiet", "reallife_ui_gallery_status_warn",
                "reallife_ui_gallery_status_danger", "reallife_ui_gallery_busy", "reallife_ui_gallery_question_title", "reallife_ui_gallery_question_body",
                "reallife_ui_gallery_keep", "reallife_ui_gallery_connected", "reallife_ui_gallery_end", "reallife_ui_gallery_toast_title",
                "reallife_ui_prompt_page", "reallife_ui_prompt_look", "reallife_ui_prompt_move", "reallife_ui_prompt_back"
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
