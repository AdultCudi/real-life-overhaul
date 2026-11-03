using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        static partial void AddFlare(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("flare", UiTemplate.AppWindow, "FlareScreen", new[]
            {
                "reallife_flare_app", "reallife_flare_post", "reallife_flare_field", "reallife_flare_empty_feed", "reallife_flare_empty_following",
                "reallife_flare_nav_home", "reallife_flare_nav_explore", "reallife_flare_nav_notices", "reallife_flare_nav_messages",
                "reallife_flare_nav_profile", "reallife_flare_nav_business", "reallife_flare_nav_city", "reallife_flare_nav_hire", "reallife_flare_nav_studio",
                "reallife_flare_chip_photo", "reallife_flare_chip_place", "reallife_flare_chip_tag", "reallife_flare_chip_promote",
                "reallife_flare_preview", "reallife_flare_stars", "reallife_flare_star_line",
                "reallife_flare_you", "reallife_flare_followers", "reallife_flare_posts", "reallife_flare_result", "reallife_flare_expect",
                "reallife_flare_visits", "reallife_flare_takings", "reallife_flare_hours", "reallife_flare_cost", "reallife_flare_gain",
                "reallife_flare_city_fig_reach",
                "reallife_flare_rating", "reallife_flare_tier", "reallife_flare_trend", "reallife_flare_terms", "reallife_flare_answer",
                "reallife_flare_perk_sponsor", "reallife_flare_perk_job", "reallife_flare_perk_raise", "reallife_flare_perk_investor", "reallife_flare_perk_vip",
                "reallife_flare_yes", "reallife_flare_no",
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                "reallife_ui_screen_flare", "reallife_opt_tip_ui_screen_flare"
            }).Switch(UiScreenSwitches.Social));
        }
    }
}
