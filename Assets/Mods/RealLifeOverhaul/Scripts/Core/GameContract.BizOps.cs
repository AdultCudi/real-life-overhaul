namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for BizMan's operations on the RLO interface (Scripts/Ui/BizMan/Ops): the schedule's day
    /// buttons, hour toggles, picker and auto fill, the managers' plan lists and plan pages, the factory's machines and
    /// recipe, the warehouse's drivers and stock, and the uniforms' editor, each read after the game (and other mods) filled
    /// them and pressed as a click presses them. Every line is optional: a line an update takes away hands that tab back to
    /// the game's own view, with one line in Player.log. Self-contained: leans on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart BizOpsPart = ContractBook.Part("bizops_kit", "ui", "BizMan's schedule, managers, factory and warehouse drawn on the RLO interface");

        private const string Sched = "UI.Smartphone.Apps.BizMan.Schedule.";

        // The schedule
        public static readonly GameMember SchedLanes = Optional(Field(BizOpsPart, Game, Sched + "BizManSchedule", "scrollerController", "ScheduleScrollerController"));
        public static readonly GameMember SchedDays = Optional(Field(BizOpsPart, Game, Sched + "BizManSchedule", "daySelectionController", "ScheduleDaySelectionController"));
        public static readonly GameMember SchedHeader = Optional(Field(BizOpsPart, Game, Sched + "BizManSchedule", "headerController", "ScheduleHeaderController"));
        public static readonly GameMember SchedDayButtons = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDaySelectionController", "_scheduleDayButtons", "List<ScheduleDayButton>"));
        public static readonly GameMember SchedDayOpen = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "openToggle", "Toggle"));
        public static readonly GameMember SchedDaySelect = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "selectDayButton", "Button"));
        public static readonly GameMember SchedDayChosen = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "selectedBackground", "GameObject"));
        public static readonly GameMember SchedDayCopy = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "copyScheduleButton", "Button"));
        public static readonly GameMember SchedDayPaste = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "pasteScheduleButton", "Button"));
        public static readonly GameMember SchedDayPasteAll = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "pasteToAllDaysButton", "Button"));
        public static readonly GameMember SchedDayAuto = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "autoScheduleButton", "Button"));
        public static readonly GameMember SchedDayClear = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDayButton", "clearScheduleButton", "Button"));
        public static readonly GameMember SchedWeekAuto = Optional(Field(BizOpsPart, Game, Sched + "ScheduleDaySelectionController", "autoScheduleButton", "Button"));
        public static readonly GameMember SchedLaneName = Optional(Field(BizOpsPart, Game, Sched + "ScheduleCellView", "workstationNameInputField", "TMP_InputField"));
        public static readonly GameMember SchedSearch = Optional(Field(BizOpsPart, Game, Sched + "ScheduleHeaderController", "searchField", "TMP_InputField"));
        public static readonly GameMember SchedHourGroup = Optional(Field(BizOpsPart, Game, Sched + "ScheduleHeaderController", "hourToggleGroup", "ScheduleHourToggleGroup"));
        public static readonly GameMember SchedHours = Optional(Field(BizOpsPart, Game, Sched + "ScheduleHourToggleGroup", "hourToggles", "List<ScheduleHourToggle>"));
        public static readonly GameMember SchedPickBox = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeSelection", "container", "GameObject"));
        public static readonly GameMember SchedPickList = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeSelection", "scrollerController", "ScheduleEmployeeScrollerController"));
        public static readonly GameMember SchedPickHour = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeSelection", "_hour", "Int32"));
        public static readonly GameMember SchedPickLane = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeSelection", "_workstationId", "String"));
        public static readonly GameMember SchedPickFilters = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeScrollerController", "filterController", "ScheduleEmployeeFilterController"));
        public static readonly GameMember SchedPickNone = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeScrollerController", "noEmployeesLabel", "TextLocalizationComponent"));
        public static readonly GameMember SchedPickRefresh = Optional(Method(BizOpsPart, Game, Sched + "ScheduleEmployeeScrollerController", "SetEmployeeData", "Void"));
        public static readonly GameMember SchedSortBy = Optional(Field(BizOpsPart, Game, Sched + "ScheduleEmployeeSortToggle", "sortingMethod", "ScheduleEmployeeSortingMethod"));
        public static readonly GameMember SchedFiller = Optional(Field(BizOpsPart, Game, "UI.Smartphone.Apps.BizMan.ScheduleAutoFillerUI", "_scheduleAutoFiller", "ScheduleAutoFiller"));
        public static readonly GameMember SchedFillerCancel = Optional(Field(BizOpsPart, Game, "UI.Smartphone.Apps.BizMan.ScheduleAutoFillerUI", "cancelButton", "Button"));

        private const string Biz = "UI.Smartphone.Apps.BizMan.";

        // The managers: each kind's plan list and plan page
        public static readonly GameMember PriceList = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagersPlanList", "pricingManagerPlanUI", "PricingManagerPlanUI"));
        public static readonly GameMember PriceEntry = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagersPlanList", "entryTemplate", "PricingManagersPlanListEntry"));
        public static readonly GameMember PriceAdd = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagersPlanList", "buttonEntry", "Transform"));
        public static readonly GameMember PriceOwned = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagersPlanList", "ownedBusinessesLabel", "TextLocalizationComponent"));
        public static readonly GameMember PriceOrder = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagersPlanList", "reorderableList", "ReorderableList"));
        public static readonly GameMember PriceDistrict = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagerPlanUI", "neighborhoodDropdown", "Dropdown"));
        public static readonly GameMember PriceGoods = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagerPlanUI", "productsScroller", "PricingManagerProductsScrollerController"));
        public static readonly GameMember PriceSuggest = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagerProductsScrollerController", "applySuggestedPricesButton", "Button"));
        public static readonly GameMember PriceInput = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagerProductCellView", "priceInput", "InputField"));
        public static readonly GameMember PricePlus = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagerProductCellView", "plusButton", "Button"));
        public static readonly GameMember PriceMinus = Optional(Field(BizOpsPart, Game, Biz + "PricingManagers.PricingManagerProductCellView", "minusButton", "Button"));
        public static readonly GameMember RouteEntry = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagersPlanList", "entryTemplate", "LogisticsManagersPlanListEntry"));
        public static readonly GameMember RouteAdd = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagersPlanList", "buttonEntry", "Transform"));
        public static readonly GameMember RouteWarehouses = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagersPlanList", "warehouseTabButton", "Button"));
        public static readonly GameMember RouteFactories = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagersPlanList", "factoryTabButton", "Button"));
        public static readonly GameMember RouteOrder = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagersPlanList", "reorderableList", "ReorderableList"));
        public static readonly GameMember RouteFrom = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerPlanUI", "warehouseDropdown", "Dropdown"));
        public static readonly GameMember RouteShow = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerPlanUI", "showWarehouseButton", "Button"));
        public static readonly GameMember RouteShowLabel = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerPlanUI", "showWarehouseLabel", "TextLocalizationComponent"));
        public static readonly GameMember RouteAddStop = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerPlanUI", "addDestinationButton", "Button"));
        public static readonly GameMember RouteMost = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerPlanUI", "destinationsAmountLabel", "TextLocalizationComponent"));
        public static readonly GameMember RouteStops = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerPlanUI", "_destinationEntries", "List<LogisticsManagerDestinationUI>"));
        public static readonly GameMember StopTarget = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerDestinationUI", "businessTargetDropdown", "Dropdown"));
        public static readonly GameMember StopRemove = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerDestinationUI", "removeButton", "Button"));
        public static readonly GameMember StopName = Optional(Field(BizOpsPart, Game, Biz + "LogisticsManagers.LogisticsManagerDestinationUI", "nameText", "TextLocalizationComponent"));
        public static readonly GameMember BuyNone = Optional(Field(BizOpsPart, Game, Biz + "PurchasingAgentsPlanList", "noPartnershipsWarning", "GameObject"));
        public static readonly GameMember BuyOrder = Optional(Field(BizOpsPart, Game, Biz + "PurchasingAgentsPlanList", "reorderableList", "ReorderableList"));
        public static readonly GameMember BuyMassTo = Optional(Field(BizOpsPart, Game, Biz + "PurchasingAgent.PurchasingAgentProductsMassActionsUI", "massActionDropdown", "Dropdown"));
        public static readonly GameMember HrEntry = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagersPlanList", "entryTemplate", "Transform"));
        public static readonly GameMember HrAdd = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagersPlanList", "buttonEntry", "Transform"));
        public static readonly GameMember HrOrder = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagersPlanList", "reorderableList", "ReorderableList"));
        public static readonly GameMember HrCount = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "employeesAssignedLabel", "TextLocalizationComponent"));
        public static readonly GameMember HrReplace = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "replaceAbsentEmployeesToggle", "Toggle"));
        public static readonly GameMember HrTrain = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "trainingTargetSlider", "Slider"));
        public static readonly GameMember HrTrainValue = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "trainingTargetValue", "TMP_Text"));
        public static readonly GameMember HrWage = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "averageSalaryLabel", "TextLocalizationComponent"));
        public static readonly GameMember HrMood = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "averageSatisfactionProgressBar", "ProgressBar"));
        public static readonly GameMember HrSkill = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "averagePrimarySkillProgressBar", "ProgressBar"));
        public static readonly GameMember HrStaff = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "employeesScrollerController", "EmployeesScrollerController"));
        public static readonly GameMember HrAssignBox = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "assignEmployeesList", "Transform"));
        public static readonly GameMember HrAssignList = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "assignableEmployeesScrollerController", "EmployeesScrollerController"));
        public static readonly GameMember HrAssignTitle = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "assignableEmployeesTitle", "TextLocalizationComponent"));
        public static readonly GameMember HrCover = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "healthInsuranceInfoLabel", "TextLocalizationComponent"));
        public static readonly GameMember HrCoverNone = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "noHealthInsuranceInfoLabel", "TextLocalizationComponent"));
        public static readonly GameMember HrCoverCancel = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "cancelHealthInsuranceButton", "GameObject"));
        public static readonly GameMember HrCoverUp = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.HrManagerPlanUI", "upgradeHealthInsuranceButton", "GameObject"));
        public static readonly GameMember HrAssignToggle = Optional(Field(BizOpsPart, Game, Biz + "HRManagers.EmployeeCellView", "assignToggle", "Toggle"));
        public static readonly GameMember HuntPlan = Optional(Field(BizOpsPart, Game, Biz + "Headhunters.HeadhuntersPlanList", "planUI", "HeadhunterPlanUI"));
        public static readonly GameMember HuntEntry = Optional(Field(BizOpsPart, Game, Biz + "Headhunters.HeadhuntersPlanList", "entryTemplate", "Transform"));
        public static readonly GameMember HuntAdd = Optional(Field(BizOpsPart, Game, Biz + "Headhunters.HeadhuntersPlanList", "buttonEntry", "Transform"));
        public static readonly GameMember HuntOrder = Optional(Field(BizOpsPart, Game, Biz + "Headhunters.HeadhuntersPlanList", "reorderableList", "ReorderableList"));
        public static readonly GameMember HuntRecruitTab = Optional(Field(BizOpsPart, Game, "HeadhunterPlanUI", "recruitingTabButton", "Button"));
        public static readonly GameMember HuntReplaceTab = Optional(Field(BizOpsPart, Game, "HeadhunterPlanUI", "automaticReplacementTabButton", "Button"));
        public static readonly GameMember HuntRecruitPanel = Optional(Field(BizOpsPart, Game, "HeadhunterPlanUI", "recruitingPanel", "GameObject"));
        public static readonly GameMember HuntReplacePanel = Optional(Field(BizOpsPart, Game, "HeadhunterPlanUI", "automaticReplacementPanel", "GameObject"));
        public static readonly GameMember HuntKindPanel = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "employeeTypeSelectorPanel", "GameObject"));
        public static readonly GameMember HuntKind = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "skillNameDropdown", "Dropdown"));
        public static readonly GameMember HuntStart = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "startRecruitingButton", "GameObject"));
        public static readonly GameMember HuntTarget = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "skillTargetSlider", "Slider"));
        public static readonly GameMember HuntTargetLabel = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "sliderSkillLabel", "TMP_Text"));
        public static readonly GameMember HuntWage = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "wageRangeLabel", "TextLocalizationComponent"));
        public static readonly GameMember HuntBreakers = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "dealBreakers", "HeadhuntersDealBreakers"));
        public static readonly GameMember HuntClear = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "clearAllDealBreakersButton", "GameObject"));
        public static readonly GameMember HuntPoints = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "pointsUsedBubbleLabel", "TextLocalizationComponent"));
        public static readonly GameMember HuntView = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "viewCurrentRecruitsButton", "GameObject"));
        public static readonly GameMember HuntStop = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "stopRecruitingButton", "GameObject"));
        public static readonly GameMember HuntNow = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "currentlyRecruitingPanel", "GameObject"));
        public static readonly GameMember HuntNowSkill = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "employeeSkillNameLabel", "TextLocalizationComponent"));
        public static readonly GameMember HuntNowLevel = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "skillLevelLabel", "TextLocalizationComponent"));
        public static readonly GameMember HuntNowWage = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "salaryRangeLabel", "TextLocalizationComponent"));
        public static readonly GameMember HuntAmount = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "amountOfCandidatesField", "TMP_InputField"));
        public static readonly GameMember HuntAlways = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "recruitContinuousToggle", "Toggle"));
        public static readonly GameMember HuntCount = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "recruitAmountOfCandidatesToggle", "Toggle"));
        public static readonly GameMember HuntSettings = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "recruitmentSettingsPanel", "GameObject"));
        public static readonly GameMember HuntWagePanel = Optional(Field(BizOpsPart, Game, "HeadhuntersRecruitingTab", "wageAndSkillPanel", "GameObject"));
        public static readonly GameMember HuntResign = Optional(Field(BizOpsPart, Game, "HeadhuntersAutomaticReplacementTab", "automaticallyReplaceOnResignToggle", "Toggle"));
        public static readonly GameMember HuntRetire = Optional(Field(BizOpsPart, Game, "HeadhuntersAutomaticReplacementTab", "automaticallyReplaceOnRetireToggle", "Toggle"));
        public static readonly GameMember HuntResignFee = Optional(Field(BizOpsPart, Game, "HeadhuntersAutomaticReplacementTab", "resignReplacementFee", "TextLocalizationComponent"));
        public static readonly GameMember HuntRetireFee = Optional(Field(BizOpsPart, Game, "HeadhuntersAutomaticReplacementTab", "retireReplacementFee", "TextLocalizationComponent"));
        public static readonly GameMember HuntWeak = Optional(Field(BizOpsPart, Game, "HeadhuntersAutomaticReplacementTab", "notEnoughSkillWarning", "GameObject"));
        public static readonly GameMember HuntStaff = Optional(Field(BizOpsPart, Game, "HeadhuntersAutomaticReplacementTab", "employeeScrollerController", "HeadhunterEmployeesScrollerController"));

        // An amount's own minus and plus (UI.AmountSelector), pressed so its limit and its lock hold
        public static readonly GameMember AmountLess = Optional(Field(BizOpsPart, Game, "UI.AmountSelector", "decreaseButton", "Button"));
        public static readonly GameMember AmountMore = Optional(Field(BizOpsPart, Game, "UI.AmountSelector", "increaseButton", "Button"));

        // The factory and the warehouse
        public static readonly GameMember FactoryKind = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryWorkstationGroupTemplate", "workstationGroupName", "TextLocalizationComponent"));
        public static readonly GameMember FactoryActive = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryWorkstationGroupTemplate", "activeWorkstationsText", "TMP_Text"));
        public static readonly GameMember FactoryMachineName = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryWorkstationTemplate", "workstationName", "TMP_Text"));
        public static readonly GameMember FactoryMakes = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryWorkstationTemplate", "createdItemName", "TextLocalizationComponent"));
        public static readonly GameMember FactoryUpTo = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "produceUpToToggle", "Toggle"));
        public static readonly GameMember FactoryAlways = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "produceContinuouslyToggle", "Toggle"));
        public static readonly GameMember FactoryAmount = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "upToAmountSelector", "AmountSelector"));
        public static readonly GameMember FactoryWorker = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "employeeNameText", "TMP_Text"));
        public static readonly GameMember FactoryManage = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "manageEmployeeButton", "Button"));
        public static readonly GameMember FactoryRecipe = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "recipeDropdown", "Dropdown"));
        public static readonly GameMember FactoryOutput = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "outputText", "TMP_Text"));
        public static readonly GameMember FactoryStock = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryValidWorkstationPanel", "stockAndIngredientTemplate", "Transform"));
        public static readonly GameMember FactoryName = Optional(Field(BizOpsPart, Game, Biz + "Factory.BizManFactoryWorkstationPanel", "workstationNameInputField", "TMP_InputField"));
        public static readonly GameMember DriverSlot = Optional(Field(BizOpsPart, Game, Biz + "Warehouse.DriverStation", "slotNumberField", "TextLocalizationComponent"));
        public static readonly GameMember DriverVehicle = Optional(Field(BizOpsPart, Game, Biz + "Warehouse.DriverStation", "vehicleNameField", "TMP_Text"));
        public static readonly GameMember DriverNoVehicle = Optional(Field(BizOpsPart, Game, Biz + "Warehouse.DriverStation", "noVehicleAssignedObj", "GameObject"));
        public static readonly GameMember DriverChoice = Optional(Field(BizOpsPart, Game, Biz + "Warehouse.DriverStation", "driverDropdown", "Dropdown"));
        public static readonly GameMember StockBoxes = Optional(Field(BizOpsPart, Game, Biz + "Warehouse.Inventory", "boxesLabel", "TextLocalizationComponent"));
        public static readonly GameMember StockSell = Optional(Field(BizOpsPart, Game, Biz + "Warehouse.Inventory", "sellAllButton", "Button"));
    }
}
