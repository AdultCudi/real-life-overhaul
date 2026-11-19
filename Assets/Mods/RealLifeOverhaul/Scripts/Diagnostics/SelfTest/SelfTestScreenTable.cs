using System;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// How a screen of the kit's registry (Ui/UiScreens) is opened from a running city by the self-test, game-free so an
    /// offline check can hold it to the registry and to the options page: a phone app of the game's (opened through its
    /// own phone), or the button of Options, Tests that the mod already has for it. A screen in neither list stands in the
    /// game only in a situation (a desk session, a door, a funeral) or is a template's sample: <c>open_every_screen</c>
    /// lists it as skipped, with that reason. A developer diagnostic; read only by a running plan.
    /// </summary>
    public static class SelfTestScreenTable
    {
        /// <summary>Screen id, then the game's app name (the AppName enum).</summary>
        public static readonly string[][] Apps =
        {
            new[] { "contacts", "Contacts" }, new[] { "bizman", "BizMan" }, new[] { "econoview", "EconoView" }, new[] { "my_employees", "MyEmployees" },
            new[] { "persona", "Persona" }, new[] { "rivals", "Rivals" }, new[] { "market_insider", "MarketInsider" }
        };

        /// <summary>Screen id, then the key of the label of the mod's Options, Tests button that opens it.</summary>
        public static readonly string[][] Tests =
        {
            new[] { "gallery", "reallife_ui_test_gallery" }, new[] { "kit_parts", "reallife_ui_test_parts" }, new[] { "finance", "reallife_ui_test_finance" },
            new[] { "banks", "reallife_ui_test_banks" }, new[] { "own_bank", "reallife_ui_test_own_bank" }, new[] { "utilities", "reallife_ui_test_utilities" },
            new[] { "career", "reallife_ui_test_career" }, new[] { "campus", "reallife_ui_test_campus" }, new[] { "open_roles", "reallife_ui_test_roles" },
            new[] { "marketplace", "reallife_ui_test_marketplace" }, new[] { "company", "reallife_ui_test_company" }, new[] { "economy", "reallife_ui_test_economy" },
            new[] { "people", "reallife_ui_test_people" }, new[] { "newsstand", "reallife_ui_test_newsstand" }, new[] { "ledger", "reallife_ui_test_ledger" },
            new[] { "ending", "reallife_ui_test_ending" }, new[] { "construction", "reallife_ui_test_construction" }, new[] { "estate", "reallife_ui_test_estate" },
            new[] { "dealer", "reallife_ui_test_dealer" }, new[] { "services", "reallife_ui_test_services" }, new[] { "tech", "reallife_ui_test_tech" },
            new[] { "ventures", "reallife_ui_test_ventures" }, new[] { "brands", "reallife_ui_test_brands" }, new[] { "webshop", "reallife_ui_test_webshop" },
            new[] { "population", "reallife_ui_test_population" }, new[] { "studio", "reallife_ui_test_firm" }, new[] { "all_apps", "reallife_ui_test_all_apps" },
            new[] { "question_box", "reallife_ui_test_question" }, new[] { "acknowledge_box", "reallife_ui_test_acknowledge" }, new[] { "text_box", "reallife_ui_test_text_box" },
            new[] { "mod_box", "reallife_ui_test_mod_box" }, new[] { "notice_cards", "reallife_ui_test_notices" }, new[] { "bizops", "reallife_ui_test_bizops_schedule" },
            new[] { "uniforms", "reallife_ui_test_uniforms" }, new[] { "dialog", "reallife_ui_test_dialog_bank" }, new[] { "checkout", "reallife_ui_test_checkout" },
            new[] { "vehicle_purchase", "reallife_ui_test_vehicle_buy" }, new[] { "item_panel", "reallife_ui_test_item_panel" }, new[] { "manage_cargo", "reallife_ui_test_cargo" },
            new[] { "wardrobe", "reallife_ui_test_wardrobe" }, new[] { "pause_menu", "reallife_ui_test_pause_menu" }, new[] { "options_city", "reallife_ui_test_options" },
            new[] { "map_filters", "reallife_ui_test_map_open" }, new[] { "hud", "reallife_ui_test_hud" }, new[] { "world_building", "reallife_ui_test_world_building" },
            new[] { "world_job_offer", "reallife_ui_test_world_offer" }, new[] { "world_private_driver", "reallife_ui_test_world_driver" },
            new[] { "world_items_list", "reallife_ui_test_world_items" }, new[] { "world_sign", "reallife_ui_test_world_sign" },
            new[] { "world_preview_terminal", "reallife_ui_test_world_terminal" }, new[] { "world_building_preview", "reallife_ui_test_world_preview" },
            new[] { "world_colour_picker", "reallife_ui_test_world_colour" }, new[] { "funeral", "reallife_ui_test_activity_funeral" },
            new[] { "activity_panel", "reallife_ui_test_activity_work" }, new[] { "session_panel", "reallife_ui_test_activity_chair" }
        };

        /// <summary>The game's app name that opens the screen, or null.</summary>
        public static string AppOf(string id) => Look(Apps, id);

        /// <summary>The key of the test button that opens the screen, or null.</summary>
        public static string TestOf(string id) => Look(Tests, id);

        private static string Look(string[][] table, string id)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (string.Equals(table[i][0], id, StringComparison.Ordinal))
                {
                    return table[i][1];
                }
            }
            return null;
        }
    }
}
