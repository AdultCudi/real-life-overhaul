namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the HUD's live pieces over the running city (Scripts/Ui/HudScreens): the game's top bar,
    /// its dances and accessories, the job and building panels, the objectives window, the short notices and the control
    /// hints the HUD hides by leaf and mirrors, and the methods its presses call. Each part and its members are in this file
    /// alone; a part file's fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart HudScreens = ContractBook.Part("hud_screens", "ui", "the RLO interface's HUD over the game's top bar, panels, objectives and notices");

        private const string HudItemsAssembly = "BigAmbitions.Items";
        private const string HudInputAssembly = "BigAmbitions.InputSystem";

        // What the HUD stands over, and the top bar's own pieces: its date (another mod's season under it), its dances and
        // its accessories, with the save's accessories and the player's dance as the animator holds it.
        public static readonly GameMember HudTopBar = Field(HudScreens, Game, "UI.UIs", "topBar", "Topbar");
        public static readonly GameMember HudPlayerHud = Field(HudScreens, Game, "UI.UIs", "playerHUD", "PlayerHUD");
        public static readonly GameMember HudDate = Field(HudScreens, Game, "UI.Topbar.Topbar", "dateLabel", "TextLocalizationComponent");
        public static readonly GameMember HudDances = Field(HudScreens, Game, "UI.Topbar.Topbar", "playerDancesUI", "PlayerDancesUI");
        public static readonly GameMember HudDanceOpen = Method(HudScreens, Game, "UI.Topbar.PlayerDancesUI", "OnDanceButtonClick", "Void");
        public static readonly GameMember HudDanceAllowed = Prop(HudScreens, Game, "Character.PlayerDances", "IsEnabled", "Boolean", true);
        public static readonly GameMember HudDanceStop = StaticMethod(HudScreens, Game, "Character.PlayerDances", "StopDancing", "Void");
        public static readonly GameMember HudDanceList = StaticMethod(HudScreens, Game, "Dancing.Dances", "GetAllDances", "DanceType[]");
        public static readonly GameMember HudAnimator = Field(HudScreens, Game, "BaseHuman", "animator", "Animator");
        public static readonly GameMember HudCharacter = Field(HudScreens, Game, "PlayerController", "Character", "ThirdPersonCharacter");
        public static readonly GameMember HudAccessories = Field(HudScreens, Game, "UI.Topbar.Topbar", "accessoriesUI", "AccessoriesUI");
        public static readonly GameMember HudAccessoriesToggle = Method(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesUI", "OnAccessoriesButtonClick", "Void");
        public static readonly GameMember HudHandOff = Method(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesUI", "OnHandAccessoryButtonClick", "Void");
        public static readonly GameMember HudHeadOff = Method(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesUI", "OnHeadAccessoryButtonClick", "Void");
        public static readonly GameMember HudPhoneOff = Method(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesUI", "OnPhoneAccessoryButtonClick", "Void");
        public static readonly GameMember HudHandShown = Method(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesUI", "OnHandAccessoryVisibleButtonClick", "Void");
        public static readonly GameMember HudHeadShown = Method(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesUI", "OnHeadAccessoryVisibleButtonClick", "Void");
        public static readonly GameMember HudAccessoriesData = Field(HudScreens, Game, "GameInstance", "accessoriesData", "AccessoriesData");
        public static readonly GameMember HudAccessoriesOpen = Field(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesData", "isPanelOpen", "Boolean");
        public static readonly GameMember HudHandItem = Field(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesData", "handAccessoryCargoInstance", "CargoInstance");
        public static readonly GameMember HudHeadItem = Field(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesData", "headAccessoryCargoInstance", "CargoInstance");
        public static readonly GameMember HudPhoneItem = Field(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesData", "phoneAccessoryCargoInstance", "CargoInstance");
        public static readonly GameMember HudHandVisible = Field(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesData", "handAccessoryVisible", "Boolean");
        public static readonly GameMember HudHeadVisible = Field(HudScreens, Game, "UI.Topbar.Accessories.AccessoriesData", "headAccessoryVisible", "Boolean");
        public static readonly GameMember HudItemName = Field(HudScreens, HudItemsAssembly, "BigAmbitions.Items.CargoInstance", "itemName", "String");

        // The side panels: the game's job panel's labels and its panel, the building panel and its walls' picture, the
        // building the player is in and today's opening hours.
        public static readonly GameMember HudJobUi = Field(HudScreens, Game, "UI.PlayerHUD.PlayerHUD", "currentJobUI", "CurrentJob");
        public static readonly GameMember HudBuildingUi = Field(HudScreens, Game, "UI.PlayerHUD.PlayerHUD", "currentBuildingUI", "CurrentBuildingUI");
        public static readonly GameMember HudJobTitle = Field(HudScreens, Game, "UI.CurrentJob.CurrentJob", "title", "TextLocalizationComponent");
        public static readonly GameMember HudJobBusiness = Field(HudScreens, Game, "UI.CurrentJob.CurrentJob", "businessName", "TextMeshProUGUI");
        public static readonly GameMember HudJobAddress = Field(HudScreens, Game, "UI.CurrentJob.CurrentJob", "address", "TextLocalizationComponent");
        public static readonly GameMember HudJobToday = Field(HudScreens, Game, "UI.CurrentJob.CurrentJob", "todayWorkStatus", "TextLocalizationComponent");
        public static readonly GameMember HudJobTomorrow = Field(HudScreens, Game, "UI.CurrentJob.CurrentJob", "tomorrowWorkStatus", "TextLocalizationComponent");
        public static readonly GameMember HudJobPanel = Field(HudScreens, Game, "UI.CurrentJob.CurrentJob", "panel", "GameObject");
        public static readonly GameMember HudWallsImage = Field(HudScreens, Game, "UI.CurrentBuilding.CurrentBuildingUI", "wallsButtonImage", "Image");
        public static readonly GameMember HudWallsSprite = Field(HudScreens, Game, "UI.CurrentBuilding.CurrentBuildingUI", "wallsVisibleSprite", "Sprite");
        public static readonly GameMember HudBuildingReg = Field(HudScreens, Game, "BuildingManager", "buildingRegistration", "BuildingRegistration");
        public static readonly GameMember HudBuildingType = Field(HudScreens, Game, "BuildingManager", "businessType", "BusinessType");
        public static readonly GameMember HudBuildingOwned = Prop(HudScreens, Game, "BuildingManager", "IsPlayerOwnedBusiness", "Boolean", false);
        public static readonly GameMember HudOpeningHours = StaticMethod(HudScreens, Game, "Helpers.BuildingHelper", "GetTodaySchedule", "ScheduleDay", "BuildingRegistration");
        public static readonly GameMember HudInParking = Prop(HudScreens, Game, "Parking.UndergroundParking.UndergroundParkingManager", "IsInsideParking", "Boolean", true);

        // The objectives window: its fold, and the delivery driver's mission for its way so far.
        public static readonly GameMember HudTasks = Field(HudScreens, Game, "UI.UIs", "tasksUI", "TasksUI");
        public static readonly GameMember HudTasksFolded = Prop(HudScreens, Game, "UI.Tasks.TasksUI", "IsCollapsed", "Boolean", false);
        public static readonly GameMember HudTasksFold = Method(HudScreens, Game, "UI.Tasks.TasksUI", "SetCollapsedState", "Void", "Boolean");
        public static readonly GameMember HudMission = Field(HudScreens, Game, "GameInstance", "currentPlayerMission", "PlayerMission");
        public static readonly GameMember HudDeliveryStops = Field(HudScreens, Game, "Player.PlayerMissions.DeliveryDriverMission", "destinations", "List<DeliveryJobDestination>");
        public static readonly GameMember HudDeliveriesMade = Method(HudScreens, Game, "Player.PlayerMissions.DeliveryDriverMission", "GetCompletedDeliveries", "Int32");

        // The short notices and the control hints.
        public static readonly GameMember HudDaySummary = Field(HudScreens, Game, "UI.UIs", "dailySummary", "DailySummary");
        public static readonly GameMember HudDayPanel = Field(HudScreens, Game, "UI.DailySummary.DailySummary", "panel", "CanvasGroup");
        public static readonly GameMember HudHospital = Field(HudScreens, Game, "UI.UIs", "hospitalizationNotification", "HospitalizationNotification");
        public static readonly GameMember HudCasino = Field(HudScreens, Game, "UI.UIs", "casinoMessage", "CasinoMessageUI");
        public static readonly GameMember HudHintsOn = Prop(HudScreens, Game, "PlayerPrefSettings", "ControlHints", "Boolean", true);
        public static readonly GameMember HudKeyName = StaticMethod(HudScreens, HudInputAssembly, "BigAmbitions.InputSystem.InputActionHelper", "GetDisplayName", "String", "PlayerAction");
        public static readonly GameMember HudImperial = StaticField(HudScreens, Game, "UnitHelper", "useImperial", "Boolean");

        // The money pill's today (the balance at the last midnight), the customers in the shop the player is in, the
        // personal goals as the Persona app reads them, the collapse and the hospital's bill, and the day statement's best
        // earner.
        public static readonly GameMember HudMidnights = Field(HudScreens, Game, "GameInstance", "midnightBankBalances", "List<Single>");
        public static readonly GameMember HudCustomers = StaticField(HudScreens, Game, "IndoorCustomerSpawner", "Customers", "List<Customer>");
        public static readonly GameMember HudSpawnsCustomers = Field(HudScreens, Game, "BusinessType", "spawnCustomers", "Boolean");
        public static readonly GameMember HudGoalList = Field(HudScreens, Game, "GameManager", "personalGoals", "List<GenericPersonalGoal>");
        public static readonly GameMember HudGoalsReached = Field(HudScreens, Game, "GameInstance", "completedPersonalGoals", "List<String>");
        public static readonly GameMember HudGoalHidden = Field(HudScreens, Game, "GenericPersonalGoal", "isHidden", "Boolean");
        public static readonly GameMember HudGoalKind = Field(HudScreens, Game, "GenericPersonalGoal", "tierIdentifier", "String");
        public static readonly GameMember HudGoalId = Field(HudScreens, Game, "GenericPersonalGoal", "identifier", "String");
        public static readonly GameMember HudGoalTitle = Method(HudScreens, Game, "GenericPersonalGoal", "GetTitle", "LanguageChangeEventDataHolder");
        public static readonly GameMember HudGoalProgress = Method(HudScreens, Game, "GenericPersonalGoal", "GetProgress", "LanguageChangeEventDataHolder");
        public static readonly GameMember HudGoalHasProgress = Method(HudScreens, Game, "GenericPersonalGoal", "HasProgress", "Boolean");
        public static readonly GameMember HudGoalOrder = Method(HudScreens, Game, "GenericPersonalGoal", "GetSortValue", "Single");
        public static readonly GameMember HudCollapse = StaticField(HudScreens, Game, "GlobalEvents", "onHospitalRespawnStarts", "Action");
        public static readonly GameMember HudTransactions = Field(HudScreens, Game, "GameInstance", "Transactions", "Queue<Transaction>");
        public static readonly GameMember HudStatements = Field(HudScreens, Game, "Entities.FinancialSummary", "businessIncomeStatements",
            "List<FinancialSummary.BusinessIncomeStatement>");
    }
}
