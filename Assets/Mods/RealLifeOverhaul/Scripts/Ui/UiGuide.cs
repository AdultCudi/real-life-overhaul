using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's entry in the mod's feature list: its switch, its motion and each screen's own switch, its tests
    /// and its Guide page.
    /// Called once when the feature list is built.
    /// </summary>
    public static class UiGuide
    {
        public static void Register()
        {
            Features.Add("ui", "reallife_meta_title_ui")
                .About("reallife_guide_ui_about").Help("reallife_guide_ui")
                .Owns(Settings.UiInterfaceId, Settings.UiMotionId)
                .Owns(UiScreenSwitches.OptionIds())
                .Test("reallife_ui_test_gallery", "reallife_ui_test_parts", "reallife_ui_test_contacts", "reallife_ui_test_population", "reallife_ui_test_firm")
                .Test("reallife_ui_test_all_apps", "reallife_ui_test_question", "reallife_ui_test_question_negative", "reallife_ui_test_acknowledge",
                    "reallife_ui_test_text_box", "reallife_ui_test_mod_box", "reallife_ui_test_notices", "reallife_ui_test_contacts_switch")
                .Test("reallife_ui_test_bizman", "reallife_ui_test_bizman_rival", "reallife_ui_test_bizman_rent", "reallife_ui_test_bizman_switch")
                .Test("reallife_ui_test_hud", "reallife_ui_test_hud_box", "reallife_ui_test_hud_sides", "reallife_ui_test_hud_goals", "reallife_ui_test_hud_summary",
                    "reallife_ui_test_hud_own")
                .Test("reallife_ui_test_world_door", "reallife_ui_test_world_desk", "reallife_ui_test_world_bus", "reallife_ui_test_world_building", "reallife_ui_test_world_offer",
                    "reallife_ui_test_world_custody", "reallife_ui_test_world_switch")
                .Test("reallife_ui_test_overlay_stock", "reallife_ui_test_overlay_machine", "reallife_ui_test_overlay_switch")
                .Test("reallife_ui_test_activity_work", "reallife_ui_test_activity_class", "reallife_ui_test_activity_chair", "reallife_ui_test_activity_skip",
                    "reallife_ui_test_activity_funeral")
                .Test("reallife_ui_test_bizops_schedule", "reallife_ui_test_bizops_managers", "reallife_ui_test_bizops_factory", "reallife_ui_test_bizops_warehouse",
                    "reallife_ui_test_bizops_switch", "reallife_ui_test_uniforms", "reallife_ui_test_uniforms_switch")
                .Test("reallife_ui_test_world_driver", "reallife_ui_test_world_items", "reallife_ui_test_world_sign", "reallife_ui_test_world_terminal",
                    "reallife_ui_test_world_preview", "reallife_ui_test_world_colour")
                .Test("reallife_phone_test_second", "reallife_phone_test_kit_folder", "reallife_phone_test_classic")
                .Test("reallife_ui_test_employees", "reallife_ui_test_employee_page", "reallife_ui_test_employees_switch")
                .Test("reallife_ui_test_checkout", "reallife_ui_test_vehicle_buy", "reallife_ui_test_item_panel", "reallife_ui_test_cargo", "reallife_ui_test_wardrobe",
                    "reallife_ui_test_buy_switch")
                .Test("reallife_ui_test_pause_menu", "reallife_ui_test_pause_switch")
                .Test("reallife_ui_test_tooltip", "reallife_ui_test_worth_card", "reallife_ui_test_tooltips_switch")
                .Test("reallife_ui_test_blueprints", () => UiDirector.Instance?.TestBlueprints())
                .Test("reallife_ui_test_help", () => UiDirector.Instance?.TestHelp())
                .Test("reallife_ui_test_bp_switch", () => UiDirector.Instance?.TestBlueprintsSwitch())
                .Test("reallife_ui_test_options", "reallife_ui_test_options_switch")
                .Test("reallife_ui_test_map_open", "reallife_ui_test_map_switch")
                .Test("reallife_ui_test_econoview", "reallife_ui_test_econoview_loans", "reallife_ui_test_econoview_switch")
                .Test("reallife_ui_test_persona", "reallife_ui_test_rivals", "reallife_ui_test_market", "reallife_ui_test_persona_switch", "reallife_ui_test_rivals_switch",
                    "reallife_ui_test_market_switch")
                .Test("reallife_ui_test_contacts_call", "reallife_ui_test_contacts_offer", "reallife_ui_test_contacts_tax", "reallife_ui_test_contacts_wage")
                .Test("reallife_ui_test_settings", () => UiDirector.Instance?.TestSettings())
                .Test("reallife_ui_test_settings_search", () => UiDirector.Instance?.TestSettingsSearch())
                .Test("reallife_ui_test_guide", () => UiDirector.Instance?.TestGuide())
                .Test("reallife_ui_test_staff", () => UiDirector.Instance?.TestStaff())
                .Test("reallife_ui_test_bug", () => UiDirector.Instance?.TestBugReport())
                .Test("reallife_ui_test_save", () => UiDirector.Instance?.TestSaveTools())
                .Test("reallife_ui_test_settings_switch", () => UiDirector.Instance?.TestSettingsSwitch())
                .Test("reallife_ui_test_dialog_bank", "reallife_ui_test_dialog_wholesale", "reallife_ui_test_dialog_import", "reallife_ui_test_dialog_desk", "reallife_ui_test_dialog_court",
                    "reallife_ui_test_dialog_tax_court", "reallife_ui_test_dialog_fred", "reallife_ui_test_dialog_switch")
                .Test("reallife_ui_test_finance", "reallife_ui_test_banks", "reallife_ui_test_own_bank", "reallife_ui_test_utilities",
                    "reallife_ui_test_money_switch")
                .Test("reallife_ui_test_career", "reallife_ui_test_campus", "reallife_ui_test_roles", "reallife_ui_test_marketplace", "reallife_ui_test_work_switch")
                .Test("reallife_ui_test_economy", "reallife_ui_test_people", "reallife_ui_test_newsstand", "reallife_ui_test_ledger",
                    "reallife_ui_test_ending", "reallife_ui_test_news_switch")
                .Test("reallife_ui_test_company", "reallife_ui_test_company_manager", "reallife_ui_test_company_rota", "reallife_ui_test_company_switch")
                .Test("reallife_ui_test_construction", "reallife_ui_test_construction_switch")
                .Test("reallife_ui_test_estate", "reallife_ui_test_estate_rents", "reallife_ui_test_dealer", "reallife_ui_test_estate_switch")
                .Test("reallife_ui_test_services", "reallife_ui_test_services_sell", "reallife_ui_test_services_switch")
                .Test("reallife_ui_test_build_catalogue", "reallife_ui_test_build_switch")
                .Test("reallife_ui_test_designer_state", "reallife_ui_test_designer_switch")
                .Test("reallife_ui_test_tech", "reallife_ui_test_tech_game", "reallife_ui_test_tech_centre", "reallife_ui_test_tech_switch")
                .Test("reallife_ui_test_ventures", "reallife_ui_test_brands", "reallife_ui_test_webshop", "reallife_ui_test_ventures_switch")
                .Data(PlayTimeStore.Key)   // the time played in the city, for the pause menu's card (Ui/Pause/PlayTime)
                .ResetData(PlayTime.Forget)
                .Data(Economy.WorthStore.Key)   // the days of net worth, the player's and the rivals', for the money pill's card (Economy/Finance/WorthWatch)
                .ResetData(Economy.WorthWatch.Forget)
                .Data(MapSaved.Key, MapBusy.Key)   // the city map's saved places and what the places looked at usually draw (Ui/MapDock/MapStoreGame)
                .ResetData(MapStoreGame.Forget)
                .Cleanup(() => UiDirector.RemoveAll("cleaned up"), 0);
        }
    }
}
