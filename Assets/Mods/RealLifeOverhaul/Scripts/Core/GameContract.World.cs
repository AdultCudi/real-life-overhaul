namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the world's pieces on the RLO interface (Scripts/Ui/World): the box the game stands
    /// over a door, a lift or a fuel stop (OverlayUI, which every WorldPanel and DoorPanel of the mod's stands in too). The
    /// building's box (BuildingResume) leans on the line WorldPanel.Beside already has (ResumePanel) and on public members.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiWorldPrompt = ContractBook.Part("ui_world_prompt", "ui", "the game's box over a door, a lift or a desk drawn on the RLO interface");

        // The box over the world (UI.Overlays.OverlayUI): its words read after the game and other mods wrote them, its buttons pressed as they are.
        public static readonly GameMember WorldOverlayGroup = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "canvasGroup", "CanvasGroup");
        public static readonly GameMember WorldOverlayHeader = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "headerGameObject", "GameObject");
        public static readonly GameMember WorldOverlayFirst = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "firstLineLabel", "TextLocalizationComponent");
        public static readonly GameMember WorldOverlayLeft = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "secondLineLeftLabel", "TextLocalizationComponent");
        public static readonly GameMember WorldOverlayRight = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "secondLineRightLabel", "TextLocalizationComponent");
        public static readonly GameMember WorldOverlayButtons = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "buttonsGameObject", "GameObject");
        public static readonly GameMember WorldOverlayTemplate = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "buttonTemplate", "Button");
        public static readonly GameMember WorldOverlayCurrent = Field(UiWorldPrompt, Game, "UI.Overlays.OverlayUI", "_currentOverlay", "IOverlay");
    }
}
