using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The kit's own gallery, the gallery of its parts and each template's sample, so the shape a screen takes is checked before one stands on it.
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
                new UiScreenDef("kit_parts", UiTemplate.AppWindow, "UiPartsGalleryScreen", new[]
                {
                    "ba:vehicletype_vordpony", "ba:vehicletype_umcdesertdeliveryjob", "ba:vehicletype_umcnunavut", "ba:vehicletype_freighttruckt1",
                    "ba:vehicletype_honzamimic", "ba:vehicletype_bima320", "ba:vehicletype_mersaidis500", "ba:vehicletype_ferdinand112", "ba:vehicletype_anselmoaf90",
                    "ba:vehicletype_petrollsfanton", "ba:vehicletype_missamvillian", "ba:vehicletype_umcdesert", "ba:skill_customerservice", "ba:skill_cleaning",
                    "ba:skill_programmer", "ba:skill_deliverydriver", "ba:skill_lawyer", "reallife_ui_parts_title", "reallife_ui_parts_nav_controls",
                    "reallife_ui_parts_nav_table", "reallife_ui_parts_nav_charts", "reallife_ui_parts_nav_catalogue", "reallife_ui_parts_nav_statement",
                    "reallife_ui_parts_nav_track", "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "reallife_ui_parts_prompt_pick", "reallife_ui_prompt_change",
                    "reallife_ui_prompt_select", "reallife_ui_prompt_page", "reallife_ui_prompt_back", "reallife_ui_parts_on", "reallife_ui_parts_off",
                    "reallife_ui_parts_sec_choices", "reallife_ui_parts_van", "reallife_ui_parts_hours", "reallife_ui_parts_hours_value", "common_neighborhood",
                    "ba:neighborhood_midtown", "reallife_ui_parts_sec_sliders", "reallife_ui_parts_ads", "reallife_ui_parts_staff", "reallife_ui_parts_markup",
                    "reallife_ui_parts_sec_amounts", "ba:itemname_paperbag", "ba:itemname_croissant", "ba:itemname_storageshelf", "ba:itemname_cupcake",
                    "reallife_ui_parts_sec_switches", "reallife_ui_parts_sundays", "reallife_ui_parts_music", "reallife_ui_parts_alcohol", "reallife_ui_parts_sec_needs",
                    "ba:itemname_cashregister", "reallife_ui_parts_need_parking", "reallife_ui_parts_need_licence", "reallife_ui_parts_sec_fields",
                    "reallife_ui_parts_name", "common_search", "reallife_ui_parts_search_goods", "reallife_ui_parts_licence_no", "reallife_ui_parts_licence_needs",
                    "reallife_ui_parts_key_phone", "reallife_ui_parts_key_pause", "reallife_ui_parts_key_photo", "reallife_ui_parts_press_key",
                    "reallife_ui_parts_at_work", "reallife_ui_parts_off_today", "common_in_training", "reallife_ui_parts_sick", "myemployees_employee_name",
                    "myemployees_hourly_wage", "reallife_ui_parts_col_hired", "myemployees_primary_skill", "myemployees_satisfaction", "reallife_ui_parts_col_now",
                    "reallife_ui_parts_all_firms", "reallife_ui_parts_all_skills", "reallife_ui_parts_search_staff", "reallife_ui_parts_people",
                    "reallife_ui_parts_people_of", "reallife_ui_parts_pick", "reallife_ui_parts_picked", "reallife_ui_parts_done", "myemployees_mass_action_fire",
                    "myemployees_mass_action_trainprimaryskill", "myemployees_mass_action_paybonuses", "common_total", "reallife_ui_parts_empty_title",
                    "reallife_ui_parts_empty_body", "reallife_ui_parts_clear_search", "reallife_ui_parts_detail_empty_title", "reallife_ui_parts_detail_empty_body",
                    "myemployees_age", "myemployees_current_business", "myemployees_hours_per_week", "common_energy", "common_day_number",
                    "reallife_ui_parts_chart_year", "reallife_ui_parts_takings", "reallife_ui_parts_costs", "reallife_ui_parts_profit",
                    "reallife_ui_parts_chart_districts", "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity",
                    "ba:neighborhood_lowermanhattan", "ba:neighborhood_murrayhill", "reallife_ui_parts_customers", "reallife_ui_parts_chart_shares",
                    "reallife_ui_parts_volume", "reallife_ui_parts_open", "reallife_ui_parts_high", "reallife_ui_parts_low", "reallife_ui_parts_close",
                    "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_4", "reallife_calendar_month_5",
                    "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8", "reallife_calendar_month_9", "reallife_calendar_month_10",
                    "reallife_calendar_month_11", "reallife_calendar_month_12", "reallife_ui_parts_owned", "reallife_ui_parts_sold_out", "reallife_ui_parts_locked",
                    "ba:itemname_computer", "ba:itemname_securitycamera", "ba:itemname_officephone", "ba:itemname_industrialcoffeemachine", "ba:itemname_painting40x30",
                    "ba:itemname_loudspeaker2", "ba:itemname_floorlamp", "ba:itemname_stackofshoppingbaskets", "ba:itemname_saladbar", "ba:itemname_trophygolf",
                    "reallife_ui_parts_decoration", "reallife_ui_parts_size", "reallife_ui_parts_stocked", "reallife_ui_parts_painting_note", "common_setdestination",
                    "common_purchase", "reallife_ui_parts_shelves_empty", "reallife_ui_parts_shelves_empty_body", "common_friday", "common_saturday", "common_sunday",
                    "common_monday", "common_tuesday", "reallife_ui_parts_account", "reallife_ui_parts_account_line", "common_balance", "reallife_ui_parts_this_week",
                    "reallife_ui_parts_last_week", "reallife_ui_parts_col_when", "reallife_ui_parts_col_what", "reallife_ui_parts_col_in", "reallife_ui_parts_col_out",
                    "reallife_ui_parts_money_in", "reallife_ui_parts_money_out", "reallife_ui_parts_change", "reallife_ui_parts_statement_empty",
                    "reallife_ui_parts_statement_empty_body", "reallife_ui_parts_savings", "reallife_ui_parts_opened_today", "reallife_ui_parts_statement_new_body",
                    "reallife_ui_parts_line_wages", "reallife_ui_parts_line_rent", "reallife_ui_parts_line_power", "reallife_ui_parts_line_goods",
                    "reallife_ui_parts_line_tax", "reallife_ui_parts_step_done", "reallife_ui_parts_step_now", "reallife_ui_parts_step_next",
                    "reallife_ui_parts_step_locked", "reallife_ui_parts_step_later", "reallife_ui_parts_track_career", "reallife_ui_parts_track_career_line",
                    "reallife_ui_parts_rung_1", "reallife_ui_parts_rung_2", "reallife_ui_parts_rung_3", "reallife_ui_parts_rung_3_needs", "reallife_ui_parts_rung_4",
                    "reallife_ui_parts_rung_4_needs", "reallife_ui_parts_rung_5", "reallife_ui_parts_rung_5_needs", "reallife_ui_parts_track_game",
                    "reallife_ui_parts_track_game_line", "reallife_ui_parts_mile_1", "reallife_ui_parts_mile_2", "reallife_ui_parts_mile_3", "common_tomorrow",
                    "reallife_ui_parts_mile_4", "reallife_ui_parts_mile_5", "reallife_ui_parts_track_build", "reallife_ui_parts_track_build_line",
                    "reallife_ui_parts_stage_1", "reallife_ui_parts_stage_2", "reallife_ui_parts_stage_3", "common_days_left", "reallife_ui_parts_stage_4",
                    "reallife_ui_parts_stage_5", "reallife_ui_parts_in_days", "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3",
                    "reallife_calendar_month_4", "reallife_calendar_month_5", "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8",
                    "reallife_calendar_month_9", "reallife_calendar_month_10", "reallife_calendar_month_11", "reallife_calendar_month_12"
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
