using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The interior designer on the RLO interface (Scripts/Ui/Designer): the game's own designer drawn by the kit over the room, every press through the
        // game's own methods and buttons. The room, its overlays and its question boxes stay the game's. One switch, under the world's smaller windows.
        static partial void AddDesigner(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("designer", UiTemplate.Hud, "DesignerFace", DesignerKeyList()).Switch(UiScreenSwitches.Tools));
        }

        // Every label the designer's face writes: the tools and their notes, the info panel, the catalogue, the materials, the item lists, the time, the blueprint and the buttons.
        private static string[] DesignerKeyList() => new[]
        {
                "reallife_ui_designer_note_palette", "reallife_ui_designer_on_apply", "common_apply",
                "common_close", "interior_designer", "reallife_ui_designer_floor",
                "reallife_ui_designer_edit", "reallife_ui_designer_cost_furniture", "reallife_ui_designer_cost_floor",
                "reallife_ui_designer_cost_sold", "reallife_ui_designer_cost_total", "bizman_customers_capacity",
                "reallife_ui_designer_of", "reallife_ui_designer_score", "reallife_ui_designer_wants",
                "reallife_ui_designer_needs", "reallife_ui_designer_show", "reallife_ui_designer_how",
                "reallife_ui_designer_holds", "reallife_ui_designer_ceiling", "reallife_ui_designer_wall",
                "interior_designer_furniture_panel_category_allitems", "interior_designer_furniture_panel_category_decoration", "interior_designer_furniture_panel_category_lights",
                "interior_designer_furniture_panel_category_security", "interior_designer_furniture_panel_category_tablesandchairs", "interior_designer_furniture_panel_category_warehouseandfactory",
                "interior_designer_furniture_panel_category_cinemaandtheater", "interior_designer_furniture_panel_category_outdoor", "common_search",
                "interior_designer_furniture_panel_category_favorites", "reallife_ui_designer_needed", "reallife_ui_designer_colours",
                "reallife_ui_designer_place", "reallife_ui_designer_favourite", "reallife_ui_designer_clear",
                "reallife_ui_designer_none_title", "reallife_ui_designer_none_body", "reallife_ui_designer_showing",
                "common_price", "reallife_ui_designer_fills", "interior_designer_floor",
                "interior_designer_wall", "reallife_ui_designer_none_materials", "reallife_ui_designer_pick_wall",
                "reallife_ui_designer_pick_floor", "reallife_ui_designer_shades", "reallife_ui_designer_price_piece",
                "reallife_ui_designer_score_if", "reallife_ui_designer_shade", "reallife_ui_designer_lay",
                "interior_designer_producer_panel", "interior_designer_queue_designer", "interior_designer_security_coverage",
                "reallife_ui_designer_note_producer", "reallife_ui_designer_note_queue", "reallife_ui_designer_note_security",
                "interior_designer_no_producers_in_building", "reallife_ui_designer_not_set", "reallife_ui_designer_secured",
                "reallife_ui_designer_not_covered", "interior_designer_security_rating", "reallife_ui_designer_time_title",
                "reallife_ui_designer_time_sub", "reallife_ui_designer_clock", "reallife_ui_designer_time_note",
                "reallife_ui_designer_bp_title", "reallife_ui_designer_bp_name", "reallife_ui_designer_bp_placeholder",
                "reallife_ui_designer_bp_bad", "reallife_ui_designer_bp_hint", "reallife_ui_designer_bp_saved",
                "reallife_ui_designer_bp_business", "reallife_ui_designer_bp_floor", "reallife_ui_designer_bp_area",
                "reallife_ui_designer_bp_furniture", "reallife_ui_designer_bp_items", "reallife_ui_designer_bp_mods",
                "reallife_ui_designer_bp_mods_n", "reallife_ui_designer_bp_free", "reallife_ui_designer_bp_free_side",
                "reallife_ui_designer_bp_gifts", "reallife_ui_designer_bp_gifts_side", "reallife_ui_designer_bp_foot",
                "reallife_ui_designer_bp_save", "dropdown_selector_header", "blueprint_creator_select_business_type",
                "common_ok", "common_cancel", "interiordesigner_hand_tool",
                "input_key_openfurnituretool", "input_key_openduplicatetool", "interiordesigner_package_tool",
                "interiordesigner_wall_tool", "interiordesigner_floor_tool", "interiordesigner_palette_tool",
                "interiordesigner_eyedropper_tool", "interiordesigner_producer_tool", "interiordesigner_queue_tool",
                "interiordesigner_security_tool", "interiordesigner_sell_tool", "interiordesigner_timeofday_tool",
                "interiordesigner_blueprint_tool", "common_undo", "common_redo",
                "interiordesigner_show_lower_floor", "interiordesigner_show_upper_floor", "reallife_ui_designer_walls",
                "interior_designer_no_favorites", "reallife_ui_designer_none_needed", "reallife_ui_designer_cost_walls",
                "reallife_ui_designer_lay_already", "reallife_ui_designer_lay_short", "reallife_ui_designer_note_duplicate",
                "reallife_ui_designer_note_package", "reallife_ui_designer_note_eyedropper", "reallife_ui_designer_note_sell",
                "reallife_ui_designer_none_things", "reallife_ui_designer_bp_used", "reallife_ui_designer_bp_used_side",
                "reallife_ui_designer_bp_gifts_in", "reallife_ui_designer_no_tool", "reallife_ui_designer_no_needs"
        };
    }
}
