using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Buying and what the player holds: the game's checkout and tax payment, a vehicle's purchase beside its showcase
        // camera, the item panel over the world, manage cargo, the clothes shop, the hairdresser and the surgeon beside
        // the 3D preview (Scripts/Ui/Buy); and the mod's garage shop beside its turntable, drawn and checked while the shop
        // itself still opens its list.
        static partial void AddBuy(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("checkout", UiTemplate.Decision, "CheckoutScreen", new[]
                {
                    "purchaseui_title_with_amounts", "purchaseui_place_order", "common_cancel", "purchaseui_totalprice", "purchaseui_waiting_in_queue", "purchaseui_pay_taxes",
                    "common_amount", "common_price", "common_total", "reallife_ui_buy_goods", "reallife_ui_buy_each", "reallife_ui_buy_pack", "reallife_ui_buy_paused",
                    "reallife_ui_buy_page", "reallife_ui_buy_owed", "reallife_ui_buy_pay", "reallife_ui_buy_amount_hint", "reallife_ui_buy_empty", "reallife_ui_buy_empty_body",
                    "taxes_pay_item", "taxes_back_taxes_pay_item", "casinoboat_ticket_pay_item", "common_purchase", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                    "reallife_ui_prompt_back", "reallife_ui_buy_delivered_to", "reallife_ui_buy_queue_served", "reallife_ui_buy_queue_next", "reallife_ui_buy_queue_place",
                    "reallife_ui_buy_sales_tax"
                }).Switch(UiScreenSwitches.Buy),
                new UiScreenDef("vehicle_purchase", UiTemplate.InfoPanel, "VehicleBuyScreen", new[]
                {
                    "common_vehicle", "purchasecar_specs", "common_cargo_capacity", "vehicle_tank_capacity", "vehicle_auto_park_support", "vehicle_max_speed", "common_yes",
                    "common_purchase", "furniture_delivery_dialog_order", "common_cancel", "purchasevehicleui_delivertowarehouse", "purchasevehicleui_no_warehouses_available",
                    "dialog_select_address", "reallife_ui_buy_colour", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_tuning_fig_to100", "reallife_ui_buy_seconds", "reallife_ui_buy_insurance", "reallife_ui_buy_a_month", "reallife_ui_buy_lot", "reallife_ui_buy_carkind_1",
                    "reallife_ui_buy_carkind_2", "reallife_ui_buy_carkind_3", "reallife_ui_buy_carkind_4", "reallife_ui_buy_carkind_5", "reallife_ui_buy_carkind_6",
                    "reallife_ui_buy_carkind_7", "reallife_ui_buy_fuel_use", "reallife_ui_buy_l100", "reallife_ui_buy_mpg",
                    "reallife_ui_buy_seats"
                }).Switch(UiScreenSwitches.Buy),
                new UiScreenDef("item_panel", UiTemplate.Hud, "ItemPanelScreen", new[]
                {
                    "common_vehicle", "itempanelui_vehicle_fuel", "itempanelui_maintenance_condition", "itempanelui_vehicle_parkingzone", "itempanelui_parkingzone_legal_payed",
                    "itempanelui_parkingzone_illegal", "itempanelui_buttons_park_vehicle", "itempanelui_buttons_auto_park", "itempanelui_buttons_sleep", "itempanelui_sell",
                    "itempanelui_buttons_discard", "itempanelui_buttons_place_box", "itempanelui_buttons_leave", "common_cargo", "reallife_ui_buy_readings",
                    "reallife_ui_buy_speed", "reallife_ui_buy_yours", "reallife_ui_buy_not_yours", "reallife_ui_buy_slots",
                    "reallife_ui_buy_in_hands", "reallife_ui_buy_unpaid_total",
                    "reallife_ui_buy_more", "reallife_ui_buy_minigame", "reallife_ui_buy_range", "reallife_ui_buy_range_value", "reallife_ui_buy_speed_of",
                    "reallife_ui_buy_manage_cargo", "reallife_ui_buy_sell_for", "action_consumefood", "action_consumedrink", "reallife_car_note_service"
                }).Switch(UiScreenSwitches.Buy),
                new UiScreenDef("manage_cargo", UiTemplate.AppWindow, "CargoScreen", new[]
                {
                    "common_cargo", "itemoverlay_contents_headline", "bizman_inventory_sell_all", "itempanelui_sell", "itempanelui_buttons_discard", "itempanelui_buttons_place",
                    "action_take", "reallife_ui_buy_move_to", "reallife_ui_buy_move_full",
                    "reallife_ui_buy_manage_cargo", "reallife_ui_buy_slots_used", "reallife_ui_buy_slots", "reallife_ui_buy_paused", "reallife_ui_buy_unpaid",
                    "reallife_ui_buy_count", "reallife_ui_buy_in_cargo", "reallife_ui_buy_empty_cargo", "reallife_ui_buy_empty_cargo_body", "reallife_ui_prompt_move",
                    "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_buy_packs", "reallife_ui_buy_pieces", "reallife_ui_buy_paid", "reallife_ui_buy_to_pay",
                    "reallife_ui_buy_sells_for", "reallife_ui_buy_each", "reallife_ui_buy_worth_now", "reallife_ui_buy_sell_for", "reallife_ui_buy_keeps", "reallife_ui_buy_keeps_cold",
                    "reallife_ui_buy_days", "reallife_ui_buy_keeps_tile"
                }).Switch(UiScreenSwitches.Buy),
                new UiScreenDef("wardrobe", UiTemplate.InfoPanel, "WardrobeScreen", new[]
                {
                    "change_character_clothes_title", "change_character_clothes_save_button", "change_character_hair_title", "change_character_hair_save_button", "plastic_surgery_title",
                    "character_customization_torso", "character_customization_legs", "character_customization_shoes", "character_customization_hair", "character_customization_head",
                    "character_customization_skin_color", "common_apply_changes", "common_revert", "intro_tooltip_randomize_appearance", "dialog_leave_button",
                    "reallife_clothes_screen_owned", "reallife_creator_tag", "reallife_ui_buy_colour", "reallife_ui_buy_strength", "reallife_ui_buy_weight",
                    "reallife_ui_buy_empty_grid", "reallife_ui_buy_empty_grid_body", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_ui_buy_filter_all", "reallife_ui_buy_filter_owned", "reallife_ui_buy_filter_rlo", "reallife_ui_buy_filter_new", "reallife_ui_buy_find",
                    "reallife_ui_buy_basket", "reallife_ui_buy_to_pay",
                    "reallife_ui_buy_reset", "reallife_ui_buy_piece", "reallife_ui_buy_style", "reallife_ui_buy_free"
                }).Switch(UiScreenSwitches.Buy),
                new UiScreenDef("garage_shop", UiTemplate.InfoPanel, null, new[]   // drawn and checked; the shop still opens its list (Garage/GarageShop)
                {
                    "reallife_customshop_title", "reallife_customshop_total", "reallife_garage_money", "reallife_garage_tab_paint", "reallife_garage_tab_finish",
                    "reallife_garage_tab_lights", "reallife_garage_tab_body", "reallife_garage_tab_parts", "reallife_tuning_tab_figures", "reallife_tuning_tab_figures_detail",
                    "reallife_garage_list_choose", "reallife_garage_list_chosen", "reallife_garage_list_back_name", "reallife_garage_list_pay", "reallife_garage_pay",
                    "reallife_garage_list_reset", "reallife_garage_stock",
                    "reallife_garage_list_stock_detail", "reallife_garage_paint_prices", "reallife_customshop_done", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                    "reallife_ui_prompt_back"
                }),
            });
        }
    }
}
