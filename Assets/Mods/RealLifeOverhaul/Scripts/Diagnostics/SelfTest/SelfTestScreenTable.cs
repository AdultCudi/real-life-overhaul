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
            new[] { "activity_panel", "reallife_ui_test_activity_work" }, new[] { "session_panel", "reallife_ui_test_activity_chair" },
            // The situations: a door's prompt, a desk's list, a custody list, the bus board, the HUD's pieces, tooltips, build mode and the designer.
            new[] { "world_prompt", "reallife_ui_test_world_door", "toggle" }, new[] { "world_picker", "reallife_ui_test_world_desk", "toggle" },
            new[] { "scenes", "reallife_ui_test_world_custody", "toggle" }, new[] { "hud_picker", "reallife_ui_test_world_bus", "toggle" },
            new[] { "hud_side", "reallife_ui_test_hud_sides", "toggle" }, new[] { "hud_objectives", "reallife_ui_test_hud_goals", "toggle" },
            new[] { "hud_popups", "reallife_ui_test_hud_box" }, new[] { "hud_notices", "reallife_ui_test_notices" },
            new[] { "hud_activity", "reallife_ui_test_activity_work" }, new[] { "hud_time_skip", "reallife_ui_test_activity_skip" },
            new[] { "time_skip", "reallife_ui_test_activity_skip" }, new[] { "tooltips", "reallife_ui_test_tooltip" },
            new[] { "build_mode", "reallife_ui_test_build_catalogue" }, new[] { "designer", "reallife_ui_test_designer_state" },
            new[] { "garage_shop", "reallife_garage_test_shop" }, new[] { "flare", "reallife_flare_test_kit" }, new[] { "planner", "reallife_ui_life_test_planner" },
            new[] { "rival_staff", "reallife_test_business_rivals" }
        };

        /// <summary>
        /// Screens that no step can open, with why: a registry entry with no runner names a layout that another screen's class draws, or a
        /// template's sample that exists for the previews only, so no live host carries its id; and the speech card, opened by the
        /// monologue step instead of a button.
        /// </summary>
        public static readonly string[] Unopenable =
        {
            "template_info_panel", "template_info_view", "template_app_window", "template_decision", "template_phone", "template_session", "template_form",
            "shape_diary", "shape_board", "shape_reader", "shape_settings", "shape_conversation", "shape_conversation_docked", "shape_messages", "shape_feed", "shape_edition",
            "shell_frame"
        };

        /// <summary>
        /// Where a screen stands in the walk of the registry: 0 for one an app opens, 2 for a situation (a toggling test, a world box, the
        /// HUD's pieces, build mode, the designer, tooltips, a question), 1 for the rest.
        /// </summary>
        public static int Rank(string id, bool opensByApp)
        {
            if (opensByApp)
            {
                return 0;
            }
            bool situation = Toggles(id) || id.StartsWith("world_", StringComparison.Ordinal) || id.StartsWith("hud_", StringComparison.Ordinal) || id.EndsWith("_box", StringComparison.Ordinal)
                             || id == "build_mode" || id == "designer" || id == "tooltips" || id == "scenes" || id == "time_skip" || id == "monologue" || id == "funeral" || id == "activity_panel"
                             || id == "session_panel" || id == "checkout" || id == "vehicle_purchase" || id == "item_panel" || id == "manage_cargo" || id == "wardrobe" || id == "garage_shop";
            return situation ? 2 : 1;
        }

        /// <summary>True when the test button that opens the screen is a toggle: pressed again it takes the screen away.</summary>
        public static bool Toggles(string id)
        {
            for (int i = 0; i < Tests.Length; i++)
            {
                if (string.Equals(Tests[i][0], id, StringComparison.Ordinal))
                {
                    return Tests[i].Length > 2 && Tests[i][2] == "toggle";
                }
            }
            return false;
        }

        /// <summary>Why a screen cannot be opened by any step, or null when something opens it.</summary>
        public static string Why(string id)
        {
            for (int i = 0; i < Unopenable.Length; i++)
            {
                if (string.Equals(Unopenable[i], id, StringComparison.Ordinal))
                {
                    return id.StartsWith("template_", StringComparison.Ordinal) ? "a template's sample for the previews: no live screen carries it"
                        : id.StartsWith("shape_", StringComparison.Ordinal) ? "a page layout another screen's class draws inside its own window (Flare, the news, the campus): no host carries its id"
                        : "the frame the shell draws round every app: it has no screen of its own";
                }
            }
            return null;
        }

        /// <summary>Screen id, then the game_open step's name for the window of the game's own that the kit draws it over.</summary>
        public static readonly string[][] GameOpens = { new[] { "game_help", "help" }, new[] { "blueprints", "blueprints" }, new[] { "phone", "phone" } };

        /// <summary>The game_open name that opens the screen, or null.</summary>
        public static string GameOpenOf(string id) => Look(GameOpens, id);

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
