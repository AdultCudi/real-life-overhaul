namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the pause menu, the game's Options in the city, blueprints and tooltips on the RLO
    /// interface (Scripts/Ui/Pause). Each part and its members in this file alone; the fields are made in their own order
    /// and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiPauseMenu = ContractBook.Part("ui_pause_menu", "ui", "the game's pause menu drawn on the RLO interface");

        // The pause menu (MiniMenu on the HUD): its panel the game switches on and off, the save's name and its open flag.
        public static readonly GameMember PausePanel = Field(UiPauseMenu, Game, "UI.MiniMenu.MiniMenu", "panel", "Image");
        public static readonly GameMember PauseName = Field(UiPauseMenu, Game, "UI.MiniMenu.MiniMenu", "saveGameName", "TMP_InputField");
        public static readonly GameMember PauseOpen = Prop(UiPauseMenu, Game, "UI.MiniMenu.MiniMenu", "IsOpen", "Boolean", true);
        public static readonly GameMember PauseToggle = Method(UiPauseMenu, Game, "UI.MiniMenu.MiniMenu", "Toggle", "Void", "Boolean");
        public static readonly GameMember PauseAutosave = Prop(UiPauseMenu, Game, "PlayerPrefSettings", "MinutesBetweenAutoSaves", "Int32", true);
        public static readonly GameMember PauseOptionsOpen = Prop(UiPauseMenu, Game, "Scenes.MainMenu.Options", "IsVisible", "Boolean", true);
        public static readonly GameMember PauseBlueprintsOpen = Prop(UiPauseMenu, Game, "BlueprintsUI.BlueprintsPanel", "IsOpen", "Boolean", true);
    }
}
