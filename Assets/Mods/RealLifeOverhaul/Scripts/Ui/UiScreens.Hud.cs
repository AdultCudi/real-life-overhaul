using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The HUD's pieces over the running game: the HUD itself, run in the game by HudScreen on the HUD host,
        // and the pieces that stand over it as screens of their own (the picker, the activity's card, the time skip veil,
        // the city map's side panel), samples the waves build on until wave 2 and the map wave run them.
        static partial void AddHud(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("hud", UiTemplate.Hud, "HudScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu", "reallife_season_name_autumn", "topbar_date_format",
                    "common_monday", "common_tuesday", "common_wednesday", "common_friday",
                    "reallife_ui_hud_weather_sunny", "reallife_ui_hud_weather_clear",
                    "reallife_ui_hud_weather_rain", "reallife_ui_hud_weather_mist", "reallife_ui_hud_weather_storm", "reallife_ui_hud_weather_snow", "reallife_ui_hud_weather_heat",
                    "reallife_ui_hud_weather_cold", "reallife_ui_hud_weather_ice", "tutorial_objectives", "common_setdestination",
                    "playeractivityui_timemachine", "playeractivityui_fast_forward", "common_cancel", "common_close", "reallife_ui_shell_all_apps",
                    "reallife_driver_tool_call", "reallife_driver_tool_choose", "reallife_driver_tool_none", "reallife_driver_tool_coming", "reallife_driver_tool_waiting",
                    "reallife_driver_tool_driving", "common_energy", "common_hunger", "common_happiness", "reallife_home_strip_hygiene", "reallife_home_strip_stress",
                    "reallife_home_strip_home", "common_casino", "daily_summary", "daily_summary_businesses", "daily_summary_details", "menu_continue", "personalgoal_achieved",
                    "personalgoal_bank_balance", "playerhud_skip_bridge_button", "reallife_ui_hud_best_earner", "reallife_ui_hud_bridge", "reallife_ui_hud_bridge_title",
                    "reallife_ui_hud_goal_next", "reallife_ui_hud_hospital", "reallife_ui_hud_hospital_bill", "reallife_ui_hud_hospital_energy", "reallife_ui_hud_hospital_missed",
                    "reallife_ui_hud_hospital_title", "reallife_ui_hud_hospital_where", "total_profit"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("hud_picker", UiTemplate.Hud, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "input_key_menu" }),
                new UiScreenDef("hud_activity", UiTemplate.Hud, null, new[] { "reallife_ui_prompt_move", "common_cancel", "playeractivityui_timemachine", "playeractivityui_fast_forward" }),
                new UiScreenDef("hud_time_skip", UiTemplate.Hud, null, new[] { "playeractivityui_timemachine", "common_cancel" }),
            });
        }
    }
}
