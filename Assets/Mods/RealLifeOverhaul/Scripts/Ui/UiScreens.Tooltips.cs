using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's tooltips drawn as the kit's card (Ui/Tooltips), and the money pill's net worth card: every tooltip the game's TooltipSystem
        // is given in the city, beside the pointer as the game places it. Its switch stands with the pause menu's.
        static partial void AddTooltips(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("tooltips", UiTemplate.Hud, null, new[]
            {
                "reallife_ui_tip_mood", "reallife_ui_tip_mood_low", "reallife_ui_tip_mood_fair", "reallife_ui_tip_mood_good", "reallife_ui_tip_today",
                "reallife_ui_tip_served", "reallife_ui_tip_shift_until", "reallife_ui_tip_shift_next", "reallife_ui_tip_shift_none", "reallife_ui_tip_shift_away",
                "reallife_ui_tip_day", "reallife_ui_tip_worth_title", "reallife_ui_tip_worth_span", "reallife_ui_tip_worth_now", "reallife_ui_tip_worth_high",
                "reallife_ui_tip_worth_low", "reallife_ui_tip_worth_change", "reallife_ui_tip_worth_on_day", "reallife_ui_tip_worth_early",
                "reallife_ui_tip_money_total", "reallife_ui_tip_money_yesterday",
                "reallife_ui_tip_money_yesterday_note", "reallife_ui_tip_money_yesterday_game", "reallife_avail_tip_off", "reallife_avail_words",
                "reallife_avail_weekdays", "reallife_avail_weekend_days",
                "reallife_avail_student", "reallife_avail_parent", "reallife_avail_early", "reallife_avail_weekend"
            }).Switch(UiScreenSwitches.Pause));
        }
    }
}
