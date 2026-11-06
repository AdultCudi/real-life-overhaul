namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the game's help window (F1, the HUD's info glyph, a business's requirement links) on the RLO
    /// interface (Scripts/Ui/Help): the categories the help built from its structure, and the two stacks of pages it has seen. Each
    /// part and its members in this file alone; the fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiHelp = ContractBook.Part("ui_help", "ui", "the game's help window drawn on the RLO interface");

        // The help's own list as it built it (after each load of its structure, the mod's two lists among them), and its history.
        public static readonly GameMember HelpGenerated = Field(UiHelp, Game, "HelpSystem", "_generatedHelpCategories", "List<HelpCategory>");
        public static readonly GameMember HelpBackPages = Field(UiHelp, Game, "HelpSystem", "_backHistory", "Stack<String>");
        public static readonly GameMember HelpForwardPages = Field(UiHelp, Game, "HelpSystem", "_forwardHistory", "Stack<String>");
    }
}
