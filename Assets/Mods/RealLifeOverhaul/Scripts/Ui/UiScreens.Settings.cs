using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Mod settings, the Guide, Staff and roles, the bug report and the save tools.
        static partial void AddSettings(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("mod_settings", UiTemplate.AppWindow, "SettingsScreen", new[]
                {
                    "reallife_set_app", "reallife_set_not_here", "reallife_set_group_count", "reallife_set_about", "reallife_set_about_tip",
                    "reallife_set_headline_in_use", "reallife_set_differ_count", "reallife_set_list_differ", "reallife_set_apply_preset",
                    "reallife_set_search_hint", "reallife_set_empty_found", "reallife_set_box_found_none", "reallife_set_box_same",
                    "reallife_set_box_needs_line", "reallife_set_box_tests", "reallife_set_keys_note", "reallife_meta_on", "reallife_meta_off",
                    "reallife_meta_title_tests", "reallife_opt_caption_run", "reallife_set_press", "reallife_keys_press",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_opt_group_start", "reallife_opt_group_you", "reallife_opt_group_city", "reallife_opt_group_around",
                    "reallife_opt_group_work", "reallife_opt_group_money", "reallife_opt_group_business", "reallife_opt_group_controls",
                    "reallife_opt_group_performance", "reallife_opt_group_trouble"
                }).Switch(UiScreenSwitches.Help),
                new UiScreenDef("guide", UiTemplate.AppWindow, "GuideScreen", new[]
                {
                    "reallife_guide_app", "reallife_guide_find", "reallife_guide_none", "reallife_guide_none_body", "reallife_guide_own",
                    "reallife_guide_on_page", "reallife_guide_next", "reallife_guide_new_since", "reallife_guide_whatsnew_title",
                    "reallife_guide_start_title", "reallife_changes_063_9_title", "reallife_changes_063_9", "reallife_opt_group_money",
                    "reallife_calendar_month_11", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Help),
                new UiScreenDef("staff_roles", UiTemplate.AppWindow, "StaffScreen", new[]
                {
                    "reallife_staff_app", "reallife_staff_search", "reallife_staff_none", "reallife_staff_none_body",
                    "reallife_staff_businesses", "reallife_staff_roles", "reallife_guide_on_page", "reallife_guide_next",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Help),
                new UiScreenDef("bug_report", UiTemplate.Decision, "BugReportScreen", new[]
                {
                    "reallife_bug_title", "reallife_bug_card_body", "reallife_bug_happened", "reallife_bug_happened_hint",
                    "reallife_bug_expected", "reallife_bug_expected_hint", "reallife_bug_where", "reallife_bug_where_city",
                    "reallife_bug_where_prefix", "reallife_bug_log_line", "reallife_bug_version_line", "reallife_bug_shot_line",
                    "reallife_bug_shot_add", "reallife_bug_save", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                    "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Help),
                new UiScreenDef("save_tools", UiTemplate.Decision, "SaveToolsScreen", new[]
                {
                    "reallife_meta_remove_title", "reallife_save_prepare_body", "reallife_meta_remove_yes",
                    "reallife_meta_resetdata_title", "reallife_save_reset_body", "reallife_meta_resetdata_yes",
                    "reallife_meta_repair_title", "reallife_save_repair_body", "reallife_save_repair_yes",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Help)
            });
        }
    }
}
