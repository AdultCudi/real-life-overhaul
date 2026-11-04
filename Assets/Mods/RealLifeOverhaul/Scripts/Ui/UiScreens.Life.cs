using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Planner, the Calendar, the Camera, the Gallery and the viewfinder (Scripts/Ui/Life).
        static partial void AddLife(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("planner", UiTemplate.AppWindow, "PlannerScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_prompt_page",
                    "reallife_planner_app", "reallife_ui_life_tab_today", "reallife_planner_tab_upcoming", "reallife_planner_tab_week",
                    "reallife_ui_life_tab_notes", "reallife_planner_filter_all", "reallife_ui_life_group_work", "reallife_ui_life_group_life",
                    "reallife_planner_group_money", "reallife_planner_group_business", "reallife_ui_life_group_study", "reallife_ui_life_week",
                    "reallife_ui_life_locate", "reallife_ui_life_empty_today_title", "reallife_ui_life_empty_today_body",
                    "reallife_ui_life_empty_upcoming_title", "reallife_ui_life_empty_upcoming_body", "reallife_ui_life_empty_notes_title",
                    "reallife_ui_life_empty_notes_body", "reallife_ui_life_add", "reallife_ui_life_jump", "common_cancel", "common_close",
                    "common_setdestination", "filebrowser_delete", "menu_save", "reallife_ibox_sec_getting_there", "reallife_ibox_sec_what",
                    "reallife_ibox_sec_when_where", "reallife_planner_cat_business", "reallife_planner_cat_city", "reallife_planner_cat_court",
                    "reallife_planner_cat_family", "reallife_planner_cat_health", "reallife_planner_cat_licence", "reallife_planner_cat_money",
                    "reallife_planner_cat_note", "reallife_planner_cat_social", "reallife_planner_cat_travel", "reallife_planner_cat_work",
                    "reallife_planner_detail_clash", "reallife_planner_detail_clash_reach", "reallife_planner_glance_free_span",
                    "reallife_planner_lead_at", "reallife_planner_lead_evening", "reallife_planner_lead_half", "reallife_planner_lead_hour",
                    "reallife_planner_lead_leave", "reallife_planner_lead_none", "reallife_planner_lead_quarter", "reallife_planner_new_note",
                    "reallife_planner_note_clear_place", "reallife_planner_note_done", "reallife_planner_note_edit", "reallife_planner_note_here",
                    "reallife_planner_note_here_street", "reallife_planner_note_no_place", "reallife_planner_note_not_done",
                    "reallife_planner_note_placeholder", "reallife_planner_notes_full", "reallife_planner_preset_bill",
                    "reallife_planner_preset_call", "reallife_planner_preset_clean", "reallife_planner_preset_collect",
                    "reallife_planner_preset_gym", "reallife_planner_preset_meet", "reallife_planner_preset_rest",
                    "reallife_planner_preset_shopping", "reallife_planner_reminder_head", "reallife_planner_reminder_too_late",
                    "reallife_planner_repeat_daily", "reallife_planner_repeat_once", "reallife_planner_settings_lead",
                    "reallife_planner_settings_show", "reallife_planner_show_business", "reallife_planner_show_city", "reallife_planner_show_court",
                    "reallife_planner_show_health", "reallife_planner_show_notes", "reallife_planner_show_people", "reallife_planner_show_travel",
                    "reallife_planner_show_work", "reallife_planner_state_clash", "reallife_planner_state_done", "reallife_planner_state_missed",
                    "reallife_planner_state_next", "reallife_planner_state_now", "reallife_planner_state_over", "reallife_planner_way_about_car",
                    "reallife_planner_way_about_foot", "reallife_planner_way_here", "reallife_planner_way_late", "reallife_planner_way_now",
                    "reallife_planner_when_all_day", "reallife_planner_when_by", "reallife_planner_when_long_all_day",
                    "reallife_planner_when_long_at", "reallife_planner_when_long_by", "reallife_planner_when_long_from_to",
                    "reallife_ui_life_a_business", "reallife_ui_life_all_day", "reallife_ui_life_all_day_head", "reallife_ui_life_clash_many",
                    "reallife_ui_life_clash_one", "reallife_ui_life_count_done", "reallife_ui_life_count_missed", "reallife_ui_life_count_todo",
                    "reallife_ui_life_d_kind", "reallife_ui_life_d_leave", "reallife_ui_life_d_way", "reallife_ui_life_d_when",
                    "reallife_ui_life_d_where", "reallife_ui_life_f_day", "reallife_ui_life_f_place", "reallife_ui_life_f_ready",
                    "reallife_ui_life_f_repeat", "reallife_ui_life_f_time", "reallife_ui_life_f_title", "reallife_ui_life_free_head",
                    "reallife_ui_life_free_none", "reallife_ui_life_later_head", "reallife_ui_life_money_due", "reallife_ui_life_next_head",
                    "reallife_ui_life_next_more", "reallife_ui_life_next_more_day", "reallife_ui_life_next_none", "reallife_ui_life_off",
                    "reallife_ui_life_on", "reallife_ui_life_repeat_weekly", "reallife_ui_life_settings", "reallife_ui_life_totals_head",
                }).Switch(UiScreenSwitches.Life),
                new UiScreenDef("calendar", UiTemplate.AppWindow, "CalendarScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_calendar_app",
                    "reallife_calendar_month_4", "reallife_ui_life_week", "reallife_ui_life_day_line", "reallife_ui_life_nothing_day",
                    "reallife_ui_life_tomorrow", "reallife_ui_life_more", "reallife_ui_life_locate", "reallife_ui_life_fact_place",
                    "reallife_ui_life_weather", "reallife_ui_hud_weather_sunny", "reallife_ui_life_add", "reallife_ui_life_open_planner",
                    "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday",
                    "reallife_calendar_app_gone", "reallife_calendar_month_1", "reallife_calendar_month_10", "reallife_calendar_month_11",
                    "reallife_calendar_month_12", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_5",
                    "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8", "reallife_calendar_month_9",
                    "reallife_calendar_open_app", "reallife_planner_notes_full", "reallife_planner_when_all_day", "reallife_planner_when_by",
                    "reallife_ui_life_a_plan", "reallife_ui_life_day_after", "reallife_ui_life_today", "reallife_ui_hud_weather_rain",
                    "reallife_ui_hud_weather_mist", "reallife_ui_hud_weather_storm", "reallife_ui_hud_weather_snow", "reallife_ui_hud_weather_heat",
                    "reallife_ui_hud_weather_cold", "reallife_ui_hud_weather_ice", "reallife_ui_hud_weather_clear",
                }).Switch(UiScreenSwitches.Life),
                new UiScreenDef("camera", UiTemplate.Decision, "CameraScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_phonecam_app", "reallife_ui_life_camera_full_title", "reallife_ui_life_camera_full_body", "reallife_ui_life_newest",
                    "reallife_phonecam_page_note", "reallife_ui_life_empty_gallery_title",
                    "reallife_ui_life_open_gallery", "reallife_ui_life_camera_stay", "common_close",
                }).Switch(UiScreenSwitches.Life),
                new UiScreenDef("photo_gallery", UiTemplate.AppWindow, "GalleryScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_gallery_app", "reallife_gallery_tab_all", "reallife_gallery_tab_selfies",
                    "reallife_ui_life_sort_new", "reallife_ui_life_sort_old", "reallife_ui_life_favourites", "reallife_ui_life_select", "reallife_ui_life_delete_selected",
                    "reallife_ui_life_day_band", "reallife_ui_life_photos_day", "reallife_ui_life_album_count",
                    "reallife_ui_life_empty_gallery_title", "reallife_ui_life_empty_gallery_body",
                    "reallife_ui_life_at_night", "reallife_ui_life_of_today", "reallife_ui_life_facts",
                    "reallife_ui_life_fact_taken", "reallife_ui_life_fact_place", "reallife_ui_life_fact_camera", "reallife_ui_life_fact_filter",
                    "reallife_ui_life_fact_frame", "reallife_ui_life_fact_size", "reallife_ui_life_camera_rear",
                    "reallife_phonecam_filter_warm", "reallife_phonecam_frame_none", "reallife_ui_life_tags", "reallife_ui_life_tag_night",
                    "reallife_ui_life_where", "reallife_ui_life_flare_line", "reallife_ui_life_share", "reallife_ui_life_wallpaper",
                    "reallife_gallery_search", "reallife_gallery_select_done", "reallife_ui_life_favourite_picked", "reallife_phonecam_app", "reallife_gallery_count_many",
                    "reallife_gallery_day_today", "reallife_gallery_pick_none", "reallife_gallery_filter_place", "reallife_gallery_filter_business", "reallife_gallery_filter_day",
                    "reallife_gallery_album_favourites", "reallife_gallery_album_businesses", "reallife_gallery_album_places", "reallife_gallery_album_flare",
                    "reallife_gallery_day_yesterday", "reallife_gallery_day", "reallife_gallery_picked", "reallife_gallery_none_match_title", "reallife_gallery_none_match_body",
                    "reallife_gallery_count_one", "reallife_gallery_filter_clear", "reallife_gallery_hour_only", "reallife_ui_life_of_day", "reallife_gallery_post_flare",
                    "reallife_gallery_edit", "reallife_gallery_flare_posted", "reallife_gallery_taken", "reallife_gallery_fact_district", "reallife_gallery_fact_weather",
                    "reallife_ui_hud_weather_sunny", "reallife_gallery_fact_people", "reallife_gallery_people_none", "reallife_phonecam_filter_none", "reallife_gallery_fact_shape",
                    "reallife_phonecam_aspect_169", "reallife_gallery_fact_flare", "reallife_gallery_fact_business", "reallife_ui_hud_weather_rain", "reallife_gallery_people_many",
                    "reallife_gallery_flare_not_posted", "reallife_gallery_tag_business", "reallife_gallery_tag_people", "reallife_phonecam_filter_title", "reallife_phonecam_frame_title",
                    "reallife_gallery_edit_note", "reallife_gallery_edit_turn", "reallife_gallery_edit_save", "common_cancel", "reallife_gallery_edit_reset",
                    "reallife_phonecam_filter_cool", "reallife_phonecam_filter_mono", "reallife_phonecam_filter_vivid", "reallife_phonecam_filter_fade", "reallife_phonecam_filter_sepia",
                    "reallife_phonecam_filter_golden", "reallife_phonecam_filter_matte", "reallife_phonecam_filter_chrome", "reallife_phonecam_filter_dream",
                    "reallife_phonecam_frame_polaroid", "reallife_phonecam_frame_film", "reallife_phonecam_frame_border", "reallife_gallery_shape_original", "reallife_phonecam_aspect_43",
                    "reallife_phonecam_aspect_11", "reallife_phonecam_aspect_916",
                }).Switch(UiScreenSwitches.Life),
                new UiScreenDef("viewfinder", UiTemplate.Hud, "ViewfinderScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_ui_life_mode_photo", "reallife_ui_life_mode_selfie", "reallife_ui_hud_weather_sunny", "reallife_ui_life_photos_today",
                    "reallife_phonecam_filter_none", "reallife_phonecam_filter_warm", "reallife_phonecam_filter_cool", "reallife_phonecam_filter_mono",
                    "reallife_phonecam_filter_vivid", "reallife_phonecam_filter_fade",
                    "reallife_phonecam_frame_none", "reallife_phonecam_frame_border", "reallife_phonecam_frame_film",
                    "reallife_ui_life_saved", "reallife_ui_life_open",
                    "reallife_phonecam_filter_sepia", "reallife_phonecam_filter_golden", "reallife_phonecam_filter_matte", "reallife_phonecam_filter_chrome",
                    "reallife_phonecam_filter_dream", "reallife_phonecam_tool_timer", "reallife_phonecam_guide_thirds", "reallife_phonecam_tool_level", "reallife_phonecam_aspect_169",
                    "reallife_phonecam_tool_body", "reallife_phonecam_tool_clean", "reallife_phonecam_app", "reallife_ui_life_mode_portrait", "reallife_ui_life_mode_wide",
                    "reallife_phonecam_frame_polaroid", "reallife_phonecam_filter_title", "reallife_phonecam_frame_title", "reallife_ui_life_newest_photo", "reallife_ui_life_open_gallery",
                    "reallife_ui_life_take", "reallife_phonecam_clean_back", "reallife_phonecam_pose", "reallife_phonecam_aspect_43",
                    "reallife_phonecam_timer_s", "reallife_phonecam_tool_boost", "reallife_ui_hud_weather_clear",
                }).Switch(UiScreenSwitches.Life),
            });
        }
    }
}
