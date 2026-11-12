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

        // The recent saves card: the game's list of saves as its load screen reads it, each save's own fields, and loading
        // one from the city as the game's funeral loads its back-up (Ui/Pause/PauseSaves).
        private const string SaveEntry = "SaveGameManager+SaveGameStruct";
        public static readonly GameMember PauseSaveList = StaticMethod(UiPauseMenu, Game, "SaveGamePathHelper", "GetAllSaveGamesFromVersion", "List<SaveGameManager.SaveGameStruct>", "String");
        public static readonly GameMember PauseSaveName = Field(UiPauseMenu, Game, SaveEntry, "name", "String");
        public static readonly GameMember PauseSaveCharacter = Field(UiPauseMenu, Game, SaveEntry, "characterId", "String");
        public static readonly GameMember PauseSaveDate = Field(UiPauseMenu, Game, SaveEntry, "lastPlayedDate", "DateTime");
        public static readonly GameMember PauseSaveDay = Field(UiPauseMenu, Game, SaveEntry, "day", "Int32");
        public static readonly GameMember PauseSaveRecover = Field(UiPauseMenu, Game, SaveEntry, "isRecoverSave", "Boolean");
        public static readonly GameMember PauseSaveTemporary = Field(UiPauseMenu, Game, SaveEntry, "isTemporary", "Boolean");
        public static readonly GameMember PauseSaveThreads = StaticMethod(UiPauseMenu, Game, "SaveGameManager", "JoinSaveGameThreads", "Void");
        public static readonly GameMember PauseSaveToLoad = StaticField(UiPauseMenu, Game, "TransitionToSave", "saveToLoadData", "SaveGameManager.SaveGameStruct");
        public static readonly GameMember PauseLoadSave = StaticMethod(UiPauseMenu, Game, "UI.Load.LoadScene", "LoadTransitionToSave", "Void");

        // The city's card: the game's own count of employees and the weekday.
        public static readonly GameMember PauseStaff = StaticMethod(UiPauseMenu, Game, "Helpers.EmployeeHelper", "GetEmployeeInstances", "List<EmployeeInstance>",
            "EmployeeInstancesQueryInfo", "List<EmployeeInstance>");
        public static readonly GameMember PauseWeekday = StaticMethod(UiPauseMenu, Game, "TimeHelper", "GetDayOfWeekIndex", "Int32", "DayOfWeekOrdered");
    }
}
