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
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu", "reallife_ui_hud_paused", "reallife_season_name_autumn", "tutorial_objectives", "common_setdestination",
                    "playeractivityui_timemachine", "playeractivityui_fast_forward", "common_cancel", "common_close", "reallife_ui_shell_all_apps"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("hud_picker", UiTemplate.Hud, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "input_key_menu" }),
                new UiScreenDef("hud_activity", UiTemplate.Hud, null, new[] { "reallife_ui_prompt_move", "common_cancel", "playeractivityui_timemachine", "playeractivityui_fast_forward" }),
                new UiScreenDef("hud_time_skip", UiTemplate.Hud, null, new[] { "playeractivityui_timemachine", "common_cancel" }),
                new UiScreenDef("hud_map", UiTemplate.Hud, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "common_close", "common_setdestination" }),
            });
        }
    }
}
