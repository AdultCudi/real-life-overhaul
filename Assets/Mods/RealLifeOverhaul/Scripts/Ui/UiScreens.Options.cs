using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's Options in the city and its custom difficulty panel (Scripts/Ui/Options): the game's own controls drawn by the kit, every
        // press through the game's own control. The main menu's Options stays the game's.
        // The game's own words the pages label their rows with, and the sentence under each action.
        private static string[] OptionsGameKeys() => new[]
        {
            "menu_options_graphics_fullscreen", "menu_options_graphics_resolution", "menu_options_graphics_fps_limit", "menu_options_ui_zooming", "menu_options_brightness",
            "menu_options_graphics_lowdetailcitymap", "menu_options_graphics_showfps", "menu_options_reset_windows", "menu_options_reset_windows_button", "menu_options_reset_windows_tip",
            "menu_options_graphics_gfx_quality", "menu_options_graphics_particle_quality", "menu_options_graphics_texture_quality", "menu_options_graphics_aa", "menu_options_graphics_shadows",
            "menu_options_graphics_hbao", "menu_options_audio_global_volume", "menu_options_audio_menumusic_volume", "menu_options_audio_radio_volume", "menu_options_audio_sfx_volume",
            "menu_options_audio_ai_music_volume", "menu_options_local_radio_songs", "menu_options_open_folder", "common_refresh", "menu_options_controls_vehicle_mouse_input",
            "menu_options_controls_invert_rotation", "menu_options_controls_run_by_default_indoor", "menu_options_controls_steering_assist", "menu_options_others_language",
            "menu_options_others_number_formatting", "menu_options_others_time_format", "menu_options_others_unit_system", "menu_options_others_seasonal_decorations",
            "ba:menu_options_others_control_hints", "ba:menu_options_others_difficulty", "ba:difficulty_customize", "menu_options_time_between_auto_saves",
            "menu_options_max_auto_saves_per_game", "menu_options_game_speed", "options_unstuck_title", "options_unstuck_button", "options_unstuck_description",
            "menu_options_others_allow_tracking", "menu_options_others_request_data_deletion"
        };

        private static string[] OptionsKeys(string[] own, bool actions)
        {
            var keys = new List<string>(own);
            keys.AddRange(OptionsGameKeys());
            for (int i = 0; actions && i < OptionsRules.Described.Length; i++)
            {
                keys.Add(OptionsRules.ActPrefix + OptionsRules.Described[i]);
            }
            return keys.ToArray();
        }

        static partial void AddOptions(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("options_city", UiTemplate.AppWindow, "OptionsScreen", OptionsKeys(new[]
                {
                    "reallife_ui_opt_title", "reallife_ui_opt_build", "reallife_ui_opt_mods", "reallife_ui_opt_mods_one", "reallife_ui_opt_search", "reallife_ui_opt_apply_note",
                    "reallife_ui_opt_reset_category", "reallife_ui_opt_reset_all", "reallife_ui_opt_reset_category_body", "reallife_ui_opt_reset_yes",
                    "reallife_ui_opt_cat_game", "reallife_ui_opt_scheme", "reallife_ui_opt_another", "reallife_ui_opt_shared", "reallife_ui_opt_taken", "reallife_ui_opt_locked",
                    "reallife_ui_opt_found_none", "reallife_ui_opt_found_none_body", "reallife_ui_opt_open", "reallife_ui_opt_open_classic", "reallife_ui_opt_press",
                    "reallife_ui_opt_mods_rlo", "reallife_ui_opt_mods_options", "reallife_ui_opt_songs_again", "reallife_ui_opt_diff_tip",
                    "reallife_ui_opt_sec_display", "reallife_ui_opt_sec_quality", "reallife_ui_opt_sec_volume", "reallife_ui_opt_sec_radio", "reallife_ui_opt_sec_play",
                    "reallife_ui_opt_sec_formats", "reallife_ui_opt_sec_difficulty", "reallife_ui_opt_sec_saving", "reallife_ui_opt_sec_help", "reallife_ui_opt_sec_privacy",
                    "reallife_ui_opt_group_movement", "reallife_ui_opt_group_time", "reallife_ui_opt_group_interaction", "reallife_ui_opt_group_apps",
                    "reallife_ui_opt_group_vehicle", "reallife_ui_opt_group_designer", "reallife_ui_opt_group_other",
                    "reallife_set_press", "reallife_meta_on", "reallife_meta_off", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "menu_options_graphics", "menu_options_audio", "menu_options_controls", "menu_options_mods", "menu_options_controls_keyboard", "menu_options_controls_controller"
                }, true)).Switch(UiScreenSwitches.Pause),
                new UiScreenDef("options_difficulty", UiTemplate.AppWindow, "OptionsScreen", OptionsKeys(new[]
                {
                    "reallife_ui_opt_diff_title", "reallife_ui_opt_diff_presets", "reallife_ui_opt_diff_general", "reallife_ui_opt_diff_character", "reallife_ui_opt_diff_economy",
                    "reallife_ui_opt_diff_preset", "reallife_ui_opt_diff_unsaved", "reallife_ui_opt_diff_name", "reallife_ui_opt_diff_name_hint", "reallife_ui_opt_diff_note",
                    "reallife_ui_opt_diff_changed", "reallife_ui_opt_diff_locked", "common_discard_changes", "common_apply_changes", "custom_game_presets_save_preset",
                    "custom_game_presets_tooltip_copy", "custom_game_presets_tooltip_paste", "custom_game_presets_tooltip_delete", "notification_save_game_invalid_characters",
                    "reallife_ui_opt_search", "reallife_meta_on", "reallife_meta_off", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }, false)).Switch(UiScreenSwitches.Pause)
            });
        }
    }
}
