using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The HUD's live pieces over the running city (Ui/HudScreens), each drawn by the HUD's own screen: the side panels,
        // the objectives and the notice cards, each with a switch under the HUD's own; and the bar's popups and key chips,
        // which stand and fall with the bar (the HUD's switch). The bar's prompts while walking name the move and the menu.
        static partial void AddHudScreens(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("hud_side", UiTemplate.Hud, "HudScreen", new[]
                {
                    "reallife_ui_prompt_move", "input_key_menu", "reallife_ui_hud_shift", "reallife_ui_hud_job", "reallife_ui_hud_your_business",
                    "bizman_schedule_opening_hours", "contacts_today_opening_hours_open", "contacts_today_opening_hours_closed", "common_today",
                    "common_tomorrow", "common_setdestination", "playerhud_currentjob_quit_job", "open_in_bizman", "interior_designer",
                    "bizman_temporarily_closed", "common_open", "common_closed", "reallife_job_card_time", "reallife_job_card_earned",
                    "reallife_ui_hud_customers"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("hud_objectives", UiTemplate.Hud, "HudScreen", new[]
                {
                    "reallife_ui_prompt_move", "input_key_menu", "tutorial_objectives", "reallife_ui_hud_locate", "reallife_ui_hud_goals_open",
                    "reallife_ui_hud_goals_empty", "reallife_ui_hud_goals_empty_body", "bizman_alerts", "uncle_fred", "workout_plan",
                    "ba:skill_deliverydriver", "food_delivery_job_title", "reallife_bld_track_header", "common_cancel", "reallife_ui_hud_goals_done",
                    "common_personal_goals", "personalgoal_bank_balance", "personalgoal_minemployees", "personalgoal_realestate", "reallife_ui_hud_goal_of"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("hud_notices", UiTemplate.Hud, "HudScreen", new[]
                {
                    "reallife_ui_prompt_move", "input_key_menu", "personalgoal_achieved", "daily_summary", "total_profit",
                    "daily_summary_businesses", "daily_summary_details", "reallife_ui_hud_hospital", "reallife_ui_hud_hospital_title",
                    "hospital_respawn_notification", "menu_continue", "common_casino", "reallife_ui_hud_bridge", "reallife_ui_hud_bridge_title",
                    "playerhud_skip_bridge_button", "personalgoal_bank_balance", "reallife_ui_hud_goal_next", "reallife_ui_hud_best_earner",
                    "reallife_ui_hud_hospital_where", "reallife_ui_hud_hospital_when", "reallife_ui_hud_hospital_bill", "reallife_ui_hud_hospital_missed",
                    "reallife_ui_hud_hospital_energy"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("hud_popups", UiTemplate.Hud, null, new[]
                {
                    "reallife_ui_prompt_move", "input_key_menu", "reallife_ui_hud_dances", "reallife_ui_hud_dancing", "reallife_ui_hud_dances_empty",
                    "reallife_ui_hud_dance_stop", "reallife_ui_hud_accessories", "reallife_ui_hud_accessories_empty", "reallife_ui_hud_accessories_off",
                    "reallife_ui_hud_slot_hand", "reallife_ui_hud_slot_head", "reallife_ui_hud_slot_phone", "reallife_ui_hud_season_weather", "input_key_openmap",
                    "input_key_pause", "input_key_throttle", "input_key_brakes", "input_key_handbrake", "reallife_ui_hud_dances_known",
                    "reallife_ui_hud_accessories_shown", "reallife_study_course_dance_class", "reallife_ui_hud_money_today"
                }),
            });
        }
    }
}
