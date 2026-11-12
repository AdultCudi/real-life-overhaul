namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the game's Options in the city and its custom difficulty panel on the RLO interface
    /// (Scripts/Ui/Options). Each part and its members in this file alone; the fields are made in their own order and lean
    /// on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiOptions = ContractBook.Part("ui_options", "ui", "the game's Options in the city and its custom difficulty panel drawn on the RLO interface");

        private const string OptionsType = "Scenes.MainMenu.Options";
        private const string HandlerType = "UI.MainMenu.CustomGameOptionsHandler";
        private const string RebindType = "UnityEngine.InputSystem.Samples.RebindUI.RebindActionUI";
        private const string Rebind = "Unity.InputSystem.RebindingUI";
        private const string ModsInternal = "BigAmbitions.ModsInternal";

        // The controls of the game's Options: each a private field the game's own handlers listen to, read and set through itself.
        public static readonly GameMember OptFullscreen = Field(UiOptions, Game, OptionsType, "fullscreen", "Toggle");
        public static readonly GameMember OptResolution = Field(UiOptions, Game, OptionsType, "resolutionDropdown", "Dropdown");
        public static readonly GameMember OptFps = Field(UiOptions, Game, OptionsType, "vSyncAndFPSLimitDropdown", "Dropdown");
        public static readonly GameMember OptGfx = Field(UiOptions, Game, OptionsType, "gfxQualityDropdown", "Dropdown");
        public static readonly GameMember OptParticles = Field(UiOptions, Game, OptionsType, "particleQualityDropdown", "Dropdown");
        public static readonly GameMember OptTextures = Field(UiOptions, Game, OptionsType, "textureQualityDropdown", "Dropdown");
        public static readonly GameMember OptAa = Field(UiOptions, Game, OptionsType, "antiAliasingDropdown", "Dropdown");
        public static readonly GameMember OptShade = Field(UiOptions, Game, OptionsType, "shadowsDropdown", "Dropdown");
        public static readonly GameMember OptHbao = Field(UiOptions, Game, OptionsType, "hbaoQualityDropdown", "Dropdown");
        public static readonly GameMember OptGamma = Field(UiOptions, Game, OptionsType, "gammaSlider", "Slider");
        public static readonly GameMember OptGammaText = Field(UiOptions, Game, OptionsType, "gammaPercentage", "TMP_Text");
        public static readonly GameMember OptZoom = Field(UiOptions, Game, OptionsType, "uiZoomingSlider", "Slider");
        public static readonly GameMember OptZoomText = Field(UiOptions, Game, OptionsType, "uiZoomingPercentage", "TMP_Text");
        public static readonly GameMember OptLowMap = Field(UiOptions, Game, OptionsType, "lowDetailCityMap", "Toggle");
        public static readonly GameMember OptWindows = Field(UiOptions, Game, OptionsType, "resetWindowsPositionsSection", "GameObject");
        public static readonly GameMember OptShowFps = Field(UiOptions, Game, OptionsType, "showFps", "Toggle");
        public static readonly GameMember OptGlobal = Field(UiOptions, Game, OptionsType, "globalVolume", "Slider");
        public static readonly GameMember OptMenuMusic = Field(UiOptions, Game, OptionsType, "menuMusicVolume", "Slider");
        public static readonly GameMember OptRadio = Field(UiOptions, Game, OptionsType, "radioVolume", "Slider");
        public static readonly GameMember OptSfx = Field(UiOptions, Game, OptionsType, "sfxVolume", "Slider");
        public static readonly GameMember OptAiMusic = Field(UiOptions, Game, OptionsType, "aiStoreMusicVolume", "Slider");
        public static readonly GameMember OptSongs = Field(UiOptions, Game, OptionsType, "songsFound", "TextLocalizationComponent");
        public static readonly GameMember OptMouseDrive = Field(UiOptions, Game, OptionsType, "vehicleMouseInput", "Toggle");
        public static readonly GameMember OptInvert = Field(UiOptions, Game, OptionsType, "invertRotation", "Toggle");
        public static readonly GameMember OptRun = Field(UiOptions, Game, OptionsType, "runByDefaultIndoors", "Toggle");
        public static readonly GameMember OptBindings = Field(UiOptions, Game, OptionsType, "bindings", "Transform");
        public static readonly GameMember OptSteer = Field(UiOptions, Game, OptionsType, "steeringAssist", "Toggle");
        public static readonly GameMember OptLanguage = Field(UiOptions, Game, OptionsType, "languageDropdown", "Dropdown");
        public static readonly GameMember OptDifficultyBox = Field(UiOptions, Game, OptionsType, "difficultyContainer", "GameObject");
        public static readonly GameMember OptDifficulty = Field(UiOptions, Game, OptionsType, "difficultyDropdown", "Dropdown");
        public static readonly GameMember OptCustomize = Field(UiOptions, Game, OptionsType, "difficultyCustomizeButton", "GameObject");
        public static readonly GameMember OptCustomUi = Field(UiOptions, Game, OptionsType, "difficultyCustomizeUI", "GameObject");
        public static readonly GameMember OptCustomPanel = Field(UiOptions, Game, OptionsType, "difficultyCustomizePanel", "CustomGamePanel");
        public static readonly GameMember OptNumbers = Field(UiOptions, Game, OptionsType, "numberFormatDropdown", "Dropdown");
        public static readonly GameMember OptTime = Field(UiOptions, Game, OptionsType, "timeFormatToggle", "Toggle");
        public static readonly GameMember OptUnits = Field(UiOptions, Game, OptionsType, "unitsToggle", "Toggle");
        public static readonly GameMember OptSeasonal = Field(UiOptions, Game, OptionsType, "seasonalDecorationsToggle", "Toggle");
        public static readonly GameMember OptHints = Field(UiOptions, Game, OptionsType, "controlsHintsToggle", "Toggle");
        public static readonly GameMember OptSaveEvery = Field(UiOptions, Game, OptionsType, "timeBetweenAutoSavesSlider", "Slider");
        public static readonly GameMember OptSaveEveryText = Field(UiOptions, Game, OptionsType, "timeBetweenAutoSavesAmount", "TextLocalizationComponent");
        public static readonly GameMember OptSaveMost = Field(UiOptions, Game, OptionsType, "maxAutoSavesPerGameSlider", "Slider");
        public static readonly GameMember OptSaveMostText = Field(UiOptions, Game, OptionsType, "maxAutoSavesPerGameAmount", "TextLocalizationComponent");
        public static readonly GameMember OptUnstuckBox = Field(UiOptions, Game, OptionsType, "unstuckPanel", "GameObject");
        public static readonly GameMember OptUnstuck = Field(UiOptions, Game, OptionsType, "unstuckButton", "Button");
        public static readonly GameMember OptSpeed = Field(UiOptions, Game, OptionsType, "gameSpeedSlider", "Slider");
        public static readonly GameMember OptSpeedText = Field(UiOptions, Game, OptionsType, "gameSpeedMultiplier", "TMP_Text");
        public static readonly GameMember OptTracking = Field(UiOptions, Game, OptionsType, "allowTrackingToggle", "Toggle");
        public static readonly GameMember OptDeletion = Field(UiOptions, Game, OptionsType, "requestDataDeletionButton", "Button");
        public static readonly GameMember OptModsTab = Field(UiOptions, Game, OptionsType, "modsTabButton", "GameObject");

        // What the game's reset, songs, windows and custom difficulty buttons call, and the pause menu's way back out of Options.
        public static readonly GameMember OptResetCategory = Method(UiOptions, Game, OptionsType, "ResetSelectedCategoryToDefaults", "Void");
        public static readonly GameMember OptResetAll = Method(UiOptions, Game, OptionsType, "ResetAllCategoriesToDefaultsWithConfirm", "Void");
        public static readonly GameMember OptResetWindows = Method(UiOptions, Game, OptionsType, "ResetWindowsPosition", "Void");
        public static readonly GameMember OptOpenRadio = Method(UiOptions, Game, OptionsType, "OpenRadioFolder", "Void");
        public static readonly GameMember OptRefreshSongs = Method(UiOptions, Game, OptionsType, "RefreshSongs", "Void");
        public static readonly GameMember OptAskDeletion = Method(UiOptions, Game, OptionsType, "RequestDataDeletion", "Void");
        public static readonly GameMember OptOpenCustom = Method(UiOptions, Game, OptionsType, "OpenDifficultyCustomizeUI", "Void");
        public static readonly GameMember OptApplyCustom = Method(UiOptions, Game, OptionsType, "ConfirmDifficultyCustomizeUI", "Void");
        public static readonly GameMember OptCloseCustom = Method(UiOptions, Game, OptionsType, "TryCloseDifficultyCustomizeUI", "Boolean");
        public static readonly GameMember OptCloseOptions = Method(UiOptions, Game, "UI.MiniMenu.MiniMenu", "CloseOptions", "Void");

        // The rebinding rows (the input system's sample UI the game uses): read through their public members, as the mod is not built against that assembly.
        public static readonly GameMember RebindRow = TypeOf(UiOptions, Rebind, RebindType);
        public static readonly GameMember RebindAction = Prop(UiOptions, Rebind, RebindType, "actionReference", "InputActionReference", false);
        public static readonly GameMember RebindId = Prop(UiOptions, Rebind, RebindType, "bindingId", "String", false);
        public static readonly GameMember RebindLabel = Prop(UiOptions, Rebind, RebindType, "actionLabel", "TextLocalizationComponent", false);
        public static readonly GameMember RebindText = Prop(UiOptions, Rebind, RebindType, "bindingText", "TextLocalizationComponent", false);
        public static readonly GameMember RebindOngoing = Prop(UiOptions, Rebind, RebindType, "ongoingRebind", null, false);
        public static readonly GameMember RebindCollision = Prop(UiOptions, Rebind, RebindType, "rebindingCollisionText", "GameObject", false);
        public static readonly GameMember RebindStart = Method(UiOptions, Rebind, RebindType, "StartInteractiveRebind", "Void");
        public static readonly GameMember DropdownGroup = Field(UiOptions, Game, "UI.Elements.Dropdown", "canvasGroup", "CanvasGroup");

        // The custom difficulty panel: its preset list, the name it asks for and the options it holds (the game's own sliders and checkboxes).
        public static readonly GameMember DiffPresets = Field(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "presetDropdown", "Dropdown");
        public static readonly GameMember DiffNaming = Field(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "nameInput", "CustomGameNameInput");
        public static readonly GameMember DiffHandler = Field(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "optionsHandler", "CustomGameOptionsHandler");
        public static readonly GameMember DiffSave = Method(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "SavePreset", "Void");
        public static readonly GameMember DiffDelete = Method(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "DeletePreset", "Void");
        public static readonly GameMember DiffCopy = Method(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "CopyPresetValues", "Void");
        public static readonly GameMember DiffPaste = Method(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "PastePresetValues", "Void");
        public static readonly GameMember DiffFromCurrent = Method(UiOptions, Game, "UI.MainMenu.CustomGamePanel", "SetPresetValuesFromCurrent", "Void");
        public static readonly GameMember NameField = Field(UiOptions, Game, "UI.MainMenu.CustomGameNameInput", "inputField", "TMP_InputField");
        public static readonly GameMember NameButton = Field(UiOptions, Game, "UI.MainMenu.CustomGameNameInput", "confirmButton", "Button");
        public static readonly GameMember SliderPiece = Field(UiOptions, Game, "UI.MainMenu.CustomGameSliderOption", "slider", "Slider");
        public static readonly GameMember SliderFigure = Field(UiOptions, Game, "UI.MainMenu.CustomGameSliderOption", "label", "TMP_Text");
        public static readonly GameMember SliderLowest = Prop(UiOptions, Game, "UI.MainMenu.CustomGameSliderOption", "MinValue", "Single", false);
        public static readonly GameMember SliderHighest = Prop(UiOptions, Game, "UI.MainMenu.CustomGameSliderOption", "MaxValue", "Single", false);
        public static readonly GameMember CheckPiece = Field(UiOptions, Game, "UI.MainMenu.CustomGameCheckboxOption", "toggle", "Toggle");
        public static readonly GameMember DiffPreset = StaticField(UiOptions, Game, HandlerType, "currentPreset", "GameVariables");
        public static readonly GameMember DiffDays = Field(UiOptions, Game, HandlerType, "daysPerYearSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffMoney = Field(UiOptions, Game, HandlerType, "startingMoneyInputField", "InputField");
        public static readonly GameMember DiffRivals = Field(UiOptions, Game, HandlerType, "rivalAttacksMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffAge = Field(UiOptions, Game, HandlerType, "startingAgeSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffAging = Field(UiOptions, Game, HandlerType, "disableAgingToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffHappy = Field(UiOptions, Game, HandlerType, "disableHappinessToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffEnergy = Field(UiOptions, Game, HandlerType, "disableEnergyToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffCourses = Field(UiOptions, Game, HandlerType, "allCoursesUnlockedToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffContacts = Field(UiOptions, Game, HandlerType, "allContactsUnlockedToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffDamage = Field(UiOptions, Game, HandlerType, "disableVehicleDamageToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffFuel = Field(UiOptions, Game, HandlerType, "disableVehicleFuelToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffTax = Field(UiOptions, Game, HandlerType, "taxPercentageSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffWages = Field(UiOptions, Game, HandlerType, "employeeHourlySalaryMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffPrices = Field(UiOptions, Game, HandlerType, "publicPricesMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffInterest = Field(UiOptions, Game, HandlerType, "bankInterestMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffPromotion = Field(UiOptions, Game, HandlerType, "baseCustomerPromotionMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffWholesale = Field(UiOptions, Game, HandlerType, "wholesaleUrgentFeeMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffImporter = Field(UiOptions, Game, HandlerType, "importerUrgentFeeMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffLimits = Field(UiOptions, Game, HandlerType, "disableWholesaleAndImportLimitsToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffCatalogue = Field(UiOptions, Game, HandlerType, "allProductsAvailableFromImportersToggle", "CustomGameCheckboxOption");
        public static readonly GameMember DiffExport = Field(UiOptions, Game, HandlerType, "exportMultiplierSlider", "CustomGameSliderOption");
        public static readonly GameMember DiffSelling = Field(UiOptions, Game, HandlerType, "sellingPriceMultiplierSlider", "CustomGameSliderOption");

        // Other mods' options in the Mods category: the game's own controller of that view and the controls it makes, matched by their labels.
        public static readonly GameMember ModsRoot = Field(UiOptions, ModsInternal, "BigAmbitions.ModsInternal.ModOptionsViewController", "contentRoot", "Transform");
        public static readonly GameMember ModsToggle = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsToggleControl", "toggle", "Toggle");
        public static readonly GameMember ModsToggleLabel = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsToggleControl", "label", "TextLocalizationComponent");
        public static readonly GameMember ModsSlider = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsSliderControl", "slider", "Slider");
        public static readonly GameMember ModsSliderLabel = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsSliderControl", "label", "TextLocalizationComponent");
        public static readonly GameMember ModsSliderFigure = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsSliderControl", "valueLabel", "TextLocalizationComponent");
        public static readonly GameMember ModsDropdown = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsDropdownControl", "dropdown", "Dropdown");
        public static readonly GameMember ModsDropdownLabel = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsDropdownControl", "label", "TextLocalizationComponent");
        public static readonly GameMember ModsButton = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsButtonControl", "button", "Button");
        public static readonly GameMember ModsButtonLabel = Field(UiOptions, Game, "BigAmbitions.ModsInternal.ModOptionsButtonControl", "label", "TextLocalizationComponent");
        public static readonly GameMember ModsActive = StaticMethod(UiOptions, ModsInternal, "BigAmbitions.ModsInternal.ModLifecycleLoader", "GetActiveMods", null);
    }
}
