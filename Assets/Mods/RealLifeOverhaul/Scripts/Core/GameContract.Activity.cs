namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the activity panel, the time skip, the funeral and the time of day tool drawn on the
    /// RLO interface (Scripts/Ui/Activity): the pieces of the game's own screens the kit reads after the game (and other
    /// mods) set them. Each part and its members are in this file alone; a part file's fields are made in their own order
    /// and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiActivityPanel = ContractBook.Part("ui_activity_panel", "ui", "the game's activity panel drawn on the RLO interface");
        public static readonly GamePart UiTimeSkip = ContractBook.Part("ui_time_skip", "ui", "the game's time skip drawn on the RLO interface");
        public static readonly GamePart UiFuneral = ContractBook.Part("ui_funeral", "ui", "the game's funeral screen drawn on the RLO interface");
        public static readonly GamePart UiTimeOfDay = ContractBook.Part("ui_time_of_day", "ui", "the game's time of day tool drawn on the RLO interface");

        // The panel's own fade: the game sets it to nothing while the player walks there, over the city map, and Going Public
        // while its session runs; the kit shows what the game shows.
        public static readonly GameMember ActivityFade = Field(UiActivityPanel, Game, "PlayerActivity.PlayerActivityUI", "canvasGroup", "CanvasGroup");

        // What an entertaining activity is doing (play, the television, the decks, a book): the panel's glyph says which.
        public static readonly GameMember ActivityEntertainDevice = Field(UiActivityPanel, Game, "PlayerActivity.EntertainActivity", "_entertainDevice", "EntertainDevice");

        // The time machine's goal and its run: the ring fills from the time it set out to the goal, at the pace it keeps.
        public static readonly GameMember SkipGoal = Field(UiTimeSkip, Game, "Timemachine.TimeMachine", "_goal", "Timestamp");
        public static readonly GameMember SkipDistance = Field(UiTimeSkip, Game, "Timemachine.TimeMachine", "_timeDistance", "Single");
        public static readonly GameMember SkipConstant = Field(UiTimeSkip, Game, "Timemachine.TimeMachine", "_useConstantSpeed", "Boolean");

        // The funeral's backup button's label: the game hides its row when there is no backup (the one made at 70).
        public static readonly GameMember FuneralBackup = Field(UiFuneral, Game, "UI.FuneralUI", "loadBackUpSaveLabel", "TextLocalizationComponent");

        // The backup the funeral found, as the game read it (its day, and the time its file was written); null when there is none.
        public static readonly GameMember FuneralBackupMeta = Field(UiFuneral, Game, "UI.FuneralUI", "_backUpMetaData", "SaveGameManager.SaveGameStruct");

        // The time of day tool keeps itself and its slider to itself: found, read and moved as its own buttons move it.
        public static readonly GameMember DayToolSelf = StaticField(UiTimeOfDay, Game, "InGameTimeOfDayPanel", "Instance", "InGameTimeOfDayPanel");
        public static readonly GameMember DayToolSlider = Field(UiTimeOfDay, Game, "InGameTimeOfDayPanel", "timeOfDaySlider", "Slider");
    }
}
