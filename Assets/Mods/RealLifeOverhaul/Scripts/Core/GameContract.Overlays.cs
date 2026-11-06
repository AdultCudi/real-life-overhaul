namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the cards over the world's items on the RLO interface (Scripts/Ui/Overlays): the words each
    /// of the game's overlay kinds writes into its own fields, read after the game and other mods wrote them; the drop-down the
    /// click's card hands its choices to; and the workstation's name field. Each card is its own part, so a change in one of
    /// the game's overlays gives back only that card to the game.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiOverlayHover = ContractBook.Part("ui_overlay_hover", "ui", "the small card over an item under the pointer drawn on the RLO interface");
        public static readonly GamePart UiOverlayClick = ContractBook.Part("ui_overlay_click", "ui", "the large card a click opens over an item drawn on the RLO interface");

        public static readonly GamePart UiTutorialAim = ContractBook.Part("ui_tutorial_aim", "ui", "the tutorial's arrow aimed at the kit's own button where the kit hides the game's");

        private const string Overlays = "Player.HUD.ItemInfoOverlays.";

        // The small card: its header and each kind's own words.
        public static readonly GameMember OverlayBoxHeader = Field(new[] { UiOverlayHover, UiOverlayClick }, Game, Overlays + "OverlayBase`1", "headerField", "TMP_Text");
        public static readonly GameMember OverlayStockContent = Field(UiOverlayHover, Game, Overlays + "StockOverlay", "contentField", "TMP_Text");
        public static readonly GameMember OverlayStockAmount = Field(UiOverlayHover, Game, Overlays + "StockOverlay", "amountField", "TMP_Text");
        public static readonly GameMember OverlayPrice = Field(UiOverlayHover, Game, Overlays + "PriceOverlay", "priceField", "TMP_Text");
        public static readonly GameMember OverlayCta = Field(UiOverlayHover, Game, Overlays + "CtaOverlay", "ctaField", "TextLocalizationComponent");
        public static readonly GameMember OverlaySecurity = Field(UiOverlayHover, Game, Overlays + "SecurityOverlay", "securityField", "TMP_Text");
        public static readonly GameMember OverlayInfo = Field(UiOverlayHover, Game, Overlays + "InfoOverlay", "infoTextField", "TextLocalizationComponent");
        public static readonly GameMember OverlayCapacity = Field(UiOverlayHover, Game, Overlays + "CustomerCapacitySimpleOverlay", "capacityLabel", "TMP_Text");
        public static readonly GameMember OverlayName = Field(UiOverlayHover, Game, Overlays + "EmployeeNameOverlay", "employeeNameField", "TMP_Text");
        public static readonly GameMember OverlayRequirementTemplate = Field(UiOverlayHover, Game, Overlays + "RequirementsOverlay", "requirementTemplate", "RequirementOverlayEntry");
        public static readonly GameMember OverlayRequirementHeader = Field(UiOverlayHover, Game, Overlays + "RequirementsOverlay", "requirementHeader", "TextLocalizationComponent");
        public static readonly GameMember OverlayRequirementText = Field(UiOverlayHover, Game, Overlays + "RequirementOverlayEntry", "requirementText", "TextLocalizationComponent");

        // The large card: the machine's choices and what it needs, the radio's slider and its words, the one sentence of a machine's note.
        public static readonly GameMember OverlayDropdown = Field(UiOverlayClick, Game, Overlays + "DropdownOverlay", "dropdown", "Dropdown");
        public static readonly GameMember OverlayStockLabel = Field(UiOverlayClick, Game, Overlays + "DropdownOverlay", "stockLabel", "TMP_Text");
        public static readonly GameMember OverlayWorkstation = Field(UiOverlayClick, Game, Overlays + "MachineOverlay", "workstationDropdown", "Dropdown");
        public static readonly GameMember OverlayRecipe = Field(UiOverlayClick, Game, Overlays + "MachineOverlay", "recipeDropdown", "Dropdown");
        public static readonly GameMember OverlayMachines = Field(UiOverlayClick, Game, Overlays + "MachineOverlay", "productionMachineTemplate", "Transform");
        public static readonly GameMember OverlayIngredients = Field(UiOverlayClick, Game, Overlays + "MachineOverlay", "recipeIngredientsLabel", "TMP_Text");
        public static readonly GameMember OverlayMachineNote = Field(UiOverlayClick, Game, Overlays + "MachineInfoOverlay", "infoTextComponent", "TextLocalizationComponent");
        public static readonly GameMember OverlayRadioSlider = Field(UiOverlayClick, Game, Overlays + "RadioOverlay", "slider", "Slider");
        public static readonly GameMember OverlayRadioStation = Field(UiOverlayClick, Game, Overlays + "RadioOverlay", "stationField", "TMP_Text");
        public static readonly GameMember OverlayRadioSong = Field(UiOverlayClick, Game, Overlays + "RadioOverlay", "songField", "TMP_Text");
        public static readonly GameMember OverlayRadioControls = Field(UiOverlayClick, Game, Overlays + "RadioOverlay", "songControlsObj", "GameObject");
        public static readonly GameMember OverlayRename = Field(UiOverlayClick, Game, Overlays + "EditableWorkstationHeader", "inputField", "TMP_InputField");

        // The game's drop-down: its options as the game keeps them, and whether they are keys to translate.
        public static readonly GameMember OverlayOptions = Field(UiOverlayClick, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember OverlayOptionsLocalized = Optional(Field(UiOverlayClick, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean"));

        // The tutorial's pointers (Tutorial.TutorialPointersManager): the arrows of the quest entry now, and the game's button each aims at.
        public static readonly GameMember TutorialPointers = StaticField(UiTutorialAim, Game, "Tutorial.TutorialPointersManager", "CurrentQuestEntryPointers", "List<TutorialPointer>");
        public static readonly GameMember TutorialTarget = Field(UiTutorialAim, Game, "Tutorial.TutorialPointerDataUiElement", "uiElementTarget", "RectTransform");
    }
}
