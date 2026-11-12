using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The pause menu's blueprints window and the game's help window (Scripts/Ui/Blueprints, Scripts/Ui/Help): the game's own
        // screens drawn by the kit, every press through the game's own buttons and calls. The main menu's copy of the blueprints
        // stays the game's.
        static partial void AddBlueprints(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("blueprints", UiTemplate.AppWindow, "BlueprintsScreen", new[]
                {
                    "common_blueprints", "common_search", "common_building_size", "common_all", "common_building_type", "blueprintdata_businesstypename",
                    "blueprint_list_no_results_connection", "reallife_ui_bp_library", "reallife_ui_bp_workshop", "reallife_ui_bp_sort",
                    "reallife_ui_bp_filters", "reallife_ui_bp_filters_on",
                    "reallife_ui_bp_count", "reallife_ui_bp_page", "reallife_ui_bp_page_of", "reallife_ui_bp_prev", "reallife_ui_bp_next", "reallife_ui_bp_loading",
                    "reallife_ui_bp_none", "reallife_ui_bp_none_found", "reallife_ui_bp_none_body", "reallife_ui_bp_none_found_body", "reallife_ui_bp_clear",
                    "reallife_ui_bp_pick", "reallife_ui_bp_by", "reallife_ui_bp_yours", "reallife_ui_bp_shared", "reallife_ui_bp_subscribed", "reallife_ui_bp_in_library",
                    "reallife_ui_bp_facts", "reallife_ui_bp_cost", "reallife_ui_bp_subscribers", "reallife_ui_bp_released", "reallife_ui_bp_items",
                    "reallife_ui_bp_size_floors", "reallife_ui_bp_inside", "reallife_ui_bp_in_counters", "reallife_ui_bp_in_shelves", "reallife_ui_bp_in_tables",
                    "reallife_ui_bp_in_seats", "reallife_ui_bp_in_workstations", "reallife_ui_bp_in_machines", "reallife_ui_bp_counting", "reallife_ui_bp_not_here",
                    "reallife_ui_bp_no_list", "reallife_ui_bp_nothing", "reallife_ui_bp_older", "reallife_ui_bp_mods", "reallife_ui_bp_upload_held",
                    "reallife_ui_bp_terms_title", "reallife_ui_bp_terms_body", "reallife_ui_bp_terms_yes", "reallife_ui_bp_terms_no", "reallife_ui_bp_terms_read",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Pause),
                new UiScreenDef("game_help", UiTemplate.AppWindow, "HelpScreen", new[]
                {
                    "common_search", "reallife_ui_help_title", "reallife_ui_help_back", "reallife_ui_help_forward", "reallife_ui_help_found", "reallife_ui_help_none",
                    "reallife_ui_help_none_body", "reallife_ui_help_media", "reallife_ui_help_media_open", "reallife_ui_help_next",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }).Switch(UiScreenSwitches.Help)
            });
        }
    }
}
