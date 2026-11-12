namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The designer's tools and steps as the bar shows them: each its own glyph and the game's own word for it, so the tool row, the tip over a tile and
    /// the title of the panel that rises all say what the game's toolbar says. The order is the tool row's. Game-free.
    /// </summary>
    public static class DesignerGlyphs
    {
        /// <summary>Each tool's glyph, in the row's order. One glyph a tool, one tool a glyph.</summary>
        public static readonly UiIcon[] ToolIcons =
        {
            UiIcon.ToolCursor,   // Hand
            UiIcon.Sofa,         // Furniture
            UiIcon.Copy,         // Duplicate
            UiIcon.Package,      // Package
            UiIcon.Wall,         // Wall
            UiIcon.Grid,         // Floor: the tiles of a floor seen from above
            UiIcon.Palette,      // Palette
            UiIcon.Eyedropper,   // Eyedropper
            UiIcon.Factory,      // Producer: what a shelf makes or shows
            UiIcon.People,       // Queue: where customers wait
            UiIcon.ShieldCheck,  // Security
            UiIcon.Coin,         // Sell
            UiIcon.Sun,          // Time of day
            UiIcon.Layout        // Save blueprint
        };

        /// <summary>The game's own words for each tool (the keys of its toolbar's tooltips and hotkeys).</summary>
        public static readonly string[] ToolKeys =
        {
            "interiordesigner_hand_tool", "input_key_openfurnituretool", "input_key_openduplicatetool", "interiordesigner_package_tool", "interiordesigner_wall_tool",
            "interiordesigner_floor_tool", "interiordesigner_palette_tool", "interiordesigner_eyedropper_tool", "interiordesigner_producer_tool", "interiordesigner_queue_tool",
            "interiordesigner_security_tool", "interiordesigner_sell_tool", "interiordesigner_timeofday_tool", "interiordesigner_blueprint_tool"
        };

        /// <summary>The same words in English, which the offline previews and checks read.</summary>
        public static readonly string[] ToolEnglish =
        {
            "Hand tool", "Furniture Tool", "Duplicate Tool", "Package Tool", "Wall tool", "Floor tool", "Palette tool", "Eyedropper tool", "Product selector tool", "Queue tool",
            "Security tool", "Sell tool", "Time of day tool", "Blueprint tool"
        };

        /// <summary>The steps after the tools: their glyphs (the walls' glyph changes with how the walls stand).</summary>
        public static readonly UiIcon[] StepIcons = { UiIcon.Undo, UiIcon.Redo, UiIcon.Down, UiIcon.Up, UiIcon.Eye };

        public static readonly string[] StepKeys = { "common_undo", "common_redo", "interiordesigner_show_lower_floor", "interiordesigner_show_upper_floor", "reallife_ui_designer_walls" };

        public static readonly string[] StepEnglish = { "Undo", "Redo", "Show lower floor", "Show upper floor", "Show or hide the walls" };

        /// <summary>The walls' glyph for how they stand: all shown, all hidden or some lowered.</summary>
        public static UiIcon WallsIcon(int walls) => walls == 1 ? UiIcon.EyeSlash : walls == 2 ? UiIcon.Building : UiIcon.Eye;
    }
}
