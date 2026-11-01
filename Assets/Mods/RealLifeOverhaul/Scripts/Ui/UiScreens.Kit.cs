using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The kit's own gallery and each template's sample, so the shape a screen takes is checked before one stands on it.
        static partial void AddKit(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
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
                    "reallife_session_meeting_push_trade", "reallife_ui_prompt_page", "reallife_ui_prompt_move",
                    "reallife_ui_prompt_back", "phone_call_button", "phone_view_on_map_button", "contacts_delete_all_button", "contactcategoryname_rivals",
                    "common_address", "myemployees_hourlywage", "contacts_today_opening_hours", "bizman_headhunters_skill_level_title", "reallife_ui_gallery_cap_type",
                    "reallife_ui_gallery_cap_colours", "reallife_ui_gallery_cap_motion", "reallife_ui_gallery_cap_sizes", "reallife_ui_gallery_col_style",
                    "reallife_ui_gallery_col_face", "reallife_ui_gallery_col_size", "reallife_ui_gallery_col_weight", "reallife_ui_gallery_col_role",
                    "reallife_ui_gallery_col_dark", "reallife_ui_gallery_col_light", "reallife_ui_gallery_col_motion", "reallife_ui_gallery_col_full",
                    "reallife_ui_gallery_col_calm", "reallife_ui_gallery_col_token", "reallife_ui_gallery_col_px", "reallife_ui_gallery_col_kind",
                    "reallife_ui_prompt_select", "reallife_ui_prompt_choose", "reallife_studio_page_age_days", "reallife_ui_gallery_page_icons"
                }),
                new UiScreenDef("template_info_panel", UiTemplate.InfoPanel, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back" }),
                new UiScreenDef("template_info_view", UiTemplate.InfoView, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back" }),
                new UiScreenDef("template_app_window", UiTemplate.AppWindow, null, new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_studio_page_age_days"
                }),
                new UiScreenDef("template_decision", UiTemplate.Decision, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "reallife_ui_prompt_back" }),
                new UiScreenDef("template_phone", UiTemplate.Phone, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back" }),
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
            });
        }
    }
}
