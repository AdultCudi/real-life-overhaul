using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The world's pieces over the running game (Ui/World): the box over a door, a lift or a desk; a building's panel;
        // the mod's list windows as the picker; the game's job offer. Each has its switch under "status bar and world".
        static partial void AddWorld(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("world_prompt", UiTemplate.Hud, null, new[] { "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu" })
                    .Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_building", UiTemplate.InfoPanel, null, new[]
                {
                    "reallife_ui_world_building", "reallife_ui_world_business", "reallife_ui_world_owner", "reallife_ui_world_neighbourhood",
                    "reallife_ui_world_area", "reallife_ui_world_capacity",
                    "reallife_ui_world_kind", "reallife_ui_world_open", "reallife_ui_world_traffic", "buildingresume_open_in_bizman", "buildingresume_left_click",
                    "buildingresume_right_click", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_picker", UiTemplate.Hud, "WorldPickerScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "input_key_menu", "reallife_ui_world_pick_custody",
                    "reallife_ui_world_pick_custody_foot", "reallife_ui_world_pick_locked_foot", "reallife_ui_world_pick_page", "reallife_ui_world_pick_empty",
                    "reallife_ui_world_pick_empty_body", "common_close"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_job_offer", UiTemplate.Decision, "JobOfferScreen", new[]
                {
                    "reallife_ui_world_offer_kicker", "reallife_ui_world_offer_hours", "reallife_ui_world_offer_hours_vary", "reallife_ui_world_offer_days",
                    "joboffer_wage_per_hour", "reallife_ui_world_offer_wage", "reallife_ui_world_offer_not_now", "reallife_ui_world_offer_hired", "joboffer_accept_job",
                    "reallife_ui_world_offer_boss", "reallife_ui_world_offer_span", "reallife_ui_world_offer_run", "reallife_ui_world_offer_and", "playerhud_currentjob_day_off",
                    "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
                }).Switch(UiScreenSwitches.Hud),
            });
        }
    }
}
