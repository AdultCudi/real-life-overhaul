using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The world's pieces over the running game (Ui/World): the box over a door, a lift or a desk; a building's panel;
        // the mod's list windows as the picker; the game's job offer; the game's smaller windows (the private driver, the
        // items list, a sign's look, the preview terminal, the preview's bar, the colour picker's frame). Each has its switch
        // under "status bar and world".
        static partial void AddWorld(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("world_prompt", UiTemplate.Hud, null, new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu", "reallife_ui_world_door_until", "reallife_ui_world_door_open",
                    "reallife_ui_world_door_opens", "reallife_ui_world_door_closed", "reallife_floors_lift_up", "reallife_floors_lift_down", "reallife_city_cart_coffee",
                    "reallife_city_cart_hotdogs", "reallife_city_cart_buy", "reallife_city_cart_chat"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_building", UiTemplate.InfoPanel, null, new[]
                {
                    "reallife_ui_world_building", "reallife_ui_world_business", "reallife_ui_world_owner", "reallife_ui_world_neighbourhood",
                    "reallife_ui_world_area", "reallife_ui_world_capacity", "reallife_ui_world_owner_one", "reallife_ui_world_state_for_rent",
                    "reallife_ui_world_state_rented_out", "reallife_ui_world_state_rival", "reallife_ui_world_state_yours", "reallife_ui_world_customers",
                    "reallife_ui_world_customers_of",
                    "reallife_ui_world_kind", "reallife_ui_world_open", "reallife_ui_world_traffic", "buildingresume_open_in_bizman", "buildingresume_left_click",
                    "buildingresume_right_click", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "input_key_menu"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_picker", UiTemplate.Hud, "WorldPickerScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "input_key_menu", "reallife_ui_world_pick_custody",
                    "reallife_ui_world_pick_custody_foot", "reallife_ui_world_pick_locked_foot", "reallife_ui_world_pick_page", "reallife_ui_world_pick_empty",
                    "reallife_ui_world_pick_empty_body", "common_close", "reallife_law_cell_held", "reallife_law_cell_held_today", "reallife_law_cell_held_yesterday",
                    "reallife_law_cell_held_days", "reallife_law_cell_span", "reallife_law_cell_when_today", "reallife_law_cell_when_tomorrow", "reallife_law_cell_when_days",
                    "reallife_law_cell_hearing", "reallife_law_cell_hearing_detail", "reallife_law_cell_doctor", "reallife_law_cell_doctor_detail",
                    "reallife_law_cell_doctor_free", "reallife_law_cell_doctor_fine", "reallife_ui_world_pick_choose", "reallife_ui_world_pick_file",
                    "reallife_ui_world_pick_file_close"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_job_offer", UiTemplate.Decision, "JobOfferScreen", new[]
                {
                    "reallife_ui_world_offer_kicker", "reallife_ui_world_offer_hours", "reallife_ui_world_offer_hours_vary", "reallife_ui_world_offer_days",
                    "joboffer_wage_per_hour", "reallife_ui_world_offer_wage", "reallife_ui_world_offer_week", "reallife_ui_world_offer_not_now", "reallife_ui_world_offer_hired", "joboffer_accept_job",
                    "reallife_ui_world_offer_boss", "reallife_ui_world_offer_liked", "reallife_ui_world_offer_fine", "reallife_ui_world_offer_narrow", "reallife_ui_world_offer_let_go",
                    "reallife_ui_world_offer_span", "reallife_ui_world_offer_run", "reallife_ui_world_offer_and", "playerhud_currentjob_day_off",
                    "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
                }).Switch(UiScreenSwitches.Hud),
                new UiScreenDef("world_private_driver", UiTemplate.Hud, "PrivateDriverScreen", new[]
                {
                    "ba:private_driver", "ba:private_driver_waiting", "ba:private_driver_no_vehicles", "ba:private_driver_type_premium", "reallife_ui_world_driver_state_riding",
                    "reallife_ui_world_driver_state_waiting", "reallife_ui_world_driver_state_coming", "reallife_ui_world_driver_riding_line", "reallife_ui_world_driver_waiting_line",
                    "reallife_ui_world_driver_coming_line", "reallife_ui_world_driver_riding", "reallife_ui_world_driver_coming", "reallife_ui_world_driver_dismiss",
                    "reallife_ui_world_driver_state_cars", "reallife_ui_world_driver_contract", "reallife_ui_world_driver_ready", "reallife_ui_world_driver_call",
                    "reallife_ui_world_driver_fee_due", "reallife_ui_world_driver_fee_paid", "reallife_ui_world_driver_state_none", "reallife_ui_world_driver_state_no_cars",
                    "reallife_ui_world_driver_none", "reallife_ui_world_driver_locate", "reallife_ui_prompt_move", "input_key_menu"
                }).Switch(UiScreenSwitches.Tools),
                new UiScreenDef("world_items_list", UiTemplate.Hud, "ItemsListScreen", new[]
                {
                    "bizman_store_inventory_title", "bizman_store_furniture_delivery_title", "common_search", "common_close", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                    "reallife_ui_world_items_count", "reallife_ui_world_items_none_found", "reallife_ui_world_items_none_found_body", "reallife_ui_world_items_empty",
                    "reallife_ui_world_items_empty_body", "reallife_ui_world_items_clear", "reallife_ui_world_items_each"
                }).Switch(UiScreenSwitches.Tools),
                new UiScreenDef("world_sign", UiTemplate.Hud, "SignAppearanceScreen", new[]
                {
                    "sign_appearance", "sign_appearance_sign_type", "sign_appearance_sign_light_color", "sign_appearance_sign_bulb_color", "sign_type", "common_select_type",
                    "reallife_ui_prompt_move", "input_key_menu"
                }).Switch(UiScreenSwitches.Tools),
                new UiScreenDef("world_preview_terminal", UiTemplate.Hud, "PreviewTerminalScreen", new[]
                {
                    "ba:itemname_previewterminal", "preview_terminal_select_building_type", "preview_terminal_select_building_size", "preview_terminal_select_business_type",
                    "preview_terminal_select_design", "preview_terminal_preview_button", "reallife_ui_world_terminal_note", "reallife_ui_world_terminal_building",
                    "reallife_ui_world_terminal_size", "reallife_ui_world_terminal_business", "reallife_ui_world_terminal_design", "reallife_ui_world_terminal_blueprints",
                    "reallife_ui_prompt_move", "input_key_menu"
                }).Switch(UiScreenSwitches.Tools),
                new UiScreenDef("world_building_preview", UiTemplate.Hud, "BuildingPreviewScreen", new[]
                {
                    "buildingpreview_cancel_preview", "common_toggle_walls", "reallife_ui_world_preview_walls_up", "reallife_ui_world_preview_walls_down",
                    "reallife_ui_world_preview_walls_cut", "reallife_ui_world_preview_title"
                }).Switch(UiScreenSwitches.Tools),
                new UiScreenDef("world_colour_picker", UiTemplate.Hud, "ColourPickerScreen", new[]
                {
                    "custom_color_picker_headline", "common_add", "common_cancel"
                }).Switch(UiScreenSwitches.Tools),
            });
        }
    }
}
