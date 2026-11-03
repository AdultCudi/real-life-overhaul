using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The pause menu, the game's Options in the city, blueprints and tooltips (Scripts/Ui/Pause): the game's own
        // screens drawn by the kit, every press through the game's own buttons. The main menu's copies stay the game's.
        static partial void AddPause(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("pause_menu", UiTemplate.Decision, "PauseScreen", new[]
                {
                    "reallife_ui_pause_title", "reallife_ui_pause_when", "reallife_ui_pause_name", "reallife_ui_pause_back", "reallife_ui_pause_unsaved",
                    "reallife_ui_pause_saved_now", "reallife_ui_pause_saved_minute", "reallife_ui_pause_saved_minutes", "reallife_ui_pause_saved_hour",
                    "reallife_ui_pause_saved_hours", "reallife_ui_pause_autosave", "reallife_ui_pause_autosave_minute", "reallife_ui_pause_saved_line", "common_name",
                    "menu_save", "menu_options", "common_blueprints", "menu_main_menu", "menu_save_and_exit_to_desktop", "menu_exit_to_desktop", "common_wishlist_now",
                    "reallife_ui_pause_save_game", "reallife_ui_pause_main_menu", "reallife_ui_pause_quit", "reallife_ui_pause_city", "reallife_ui_pause_money",
                    "reallife_ui_pause_worth", "reallife_ui_pause_businesses", "reallife_ui_pause_employees", "reallife_ui_pause_day", "reallife_ui_pause_played",
                    "reallife_ui_pause_played_none", "reallife_ui_pause_played_minutes", "reallife_ui_pause_played_hours", "reallife_ui_pause_saves", "reallife_ui_pause_saves_count",
                    "reallife_ui_pause_saves_none", "reallife_ui_pause_load", "reallife_ui_pause_over", "reallife_ui_pause_load_body", "reallife_ui_pause_ago_now",
                    "reallife_ui_pause_ago_minute", "reallife_ui_pause_ago_minutes", "reallife_ui_pause_ago_hour", "reallife_ui_pause_ago_hours", "reallife_ui_pause_ago_day",
                    "reallife_ui_pause_ago_days", "reallife_ui_pause_save_day", "reallife_ui_pause_save_line",
                    "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
                }).Switch(UiScreenSwitches.Pause)
            });
        }
    }
}
