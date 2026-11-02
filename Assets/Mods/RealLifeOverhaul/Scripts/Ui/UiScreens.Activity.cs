using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The words the HUD's bar and its walking prompts use, under the strip and the cards as they stand in the game.
        // (Made when asked: the screens' list is gathered while the class is first set up, before this file's fields are.)
        private static string[] ActivityBarKeys => new[]
        {
            "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu", "reallife_season_name_autumn", "reallife_ui_hud_weather_sunny", "reallife_ui_hud_weather_clear",
            "reallife_ui_hud_weather_rain", "reallife_ui_hud_weather_mist", "reallife_ui_hud_weather_storm", "reallife_ui_hud_weather_snow", "reallife_ui_hud_weather_heat",
            "reallife_ui_hud_weather_cold", "reallife_ui_hud_weather_ice", "tutorial_objectives", "common_setdestination", "common_close", "reallife_ui_shell_all_apps",
        };

        // The game's activity panel before it starts and while it runs, its time skip, the funeral and the time of day tool,
        // each run in the game over the game's own screen (Scripts/Ui/Activity, ActivityKit) with its own switch.
        static partial void AddActivity(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("activity_panel", UiTemplate.Hud, "ActivityPanelScreen", ActivityWords(ActivityBarKeys, new[]
                {
                    "playeractivityui_timemachine", "playeractivityui_fast_forward", "common_cancel", "common_back", "common_hours", "common_hours_abbr", "workui_start",
                    "reallife_ui_activity_moving", "reallife_ui_activity_waiting", "reallife_ui_activity_done", "reallife_ui_activity_how_long", "reallife_ui_activity_work_kicker",
                    "reallife_ui_activity_work_title", "reallife_ui_activity_work_body", "reallife_ui_activity_work_for", "reallife_ui_activity_pay", "reallife_ui_activity_pay_hour",
                    "reallife_ui_activity_hours", "reallife_ui_activity_skill", "reallife_ui_activity_skill_day", "reallife_ui_activity_standing", "reallife_ui_activity_stress",
                    "reallife_ui_activity_finish", "reallife_ui_activity_before_tax", "reallife_ui_activity_work_strip", "reallife_ui_activity_hour_of", "reallife_ui_activity_earned",
                    "reallife_ui_activity_skip_end", "reallife_ui_activity_stop", "reallife_ui_activity_shift_hours",
                })).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("time_skip", UiTemplate.Hud, "TimeSkipScreen", ActivityWords(ActivityBarKeys, new[]
                {
                    "playeractivityui_timemachine", "common_cancel", "reallife_ui_skip_until", "reallife_ui_skip_to_end", "reallife_ui_skip_goal_day", "reallife_ui_skip_left",
                    "reallife_ui_skip_until_left", "reallife_ui_skip_hours_minutes", "reallife_ui_skip_minutes",
                })).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("funeral", UiTemplate.Decision, "FuneralScreen", ActivityWords(ActivityBarKeys, new[]
                {
                    "reallife_ui_prompt_choose", "reallife_ui_funeral_kicker", "reallife_ui_funeral_kicker_plain", "reallife_ui_funeral_title", "reallife_ui_funeral_body",
                    "reallife_ui_funeral_age", "reallife_ui_funeral_days", "reallife_ui_funeral_backup", "reallife_ui_funeral_backup_line", "reallife_ui_funeral_menu",
                    "reallife_ui_funeral_menu_line", "reallife_ui_funeral_menu_short", "reallife_ui_funeral_quit", "reallife_ui_funeral_quit_line", "reallife_ui_funeral_note",
                    "reallife_ui_funeral_note_two", "reallife_ui_funeral_you", "reallife_ui_funeral_quit_short", "reallife_ui_funeral_rival",
                    "reallife_pd_obit_lived", "reallife_pd_obit_no_business", "reallife_pd_obit_founded_one", "reallife_pd_obit_founded_many", "reallife_pd_obit_staff_one",
                    "reallife_pd_obit_staff_many",
                })).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("time_of_day", UiTemplate.Hud, "TimeOfDayScreen", ActivityWords(ActivityBarKeys, new[]
                {
                    "reallife_ui_time_of_day", "reallife_ui_time_of_day_earlier", "reallife_ui_time_of_day_later",
                })).Switch(UiScreenSwitches.Hud),
            });
        }

        private static string[] ActivityWords(string[] a, string[] b)
        {
            var all = new string[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }
    }
}
