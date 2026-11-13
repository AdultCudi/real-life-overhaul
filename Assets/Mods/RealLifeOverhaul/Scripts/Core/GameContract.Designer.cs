namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the interior designer on the RLO interface (Scripts/Ui/Designer): the private pieces of the game's designer its face
    /// reads (the toolbar, the undo stack, the panels' fields, the actions' prices) and the ones whose clicks it makes. What the game makes public (the
    /// tools, undo, redo, apply, the floors, the walls, the placing of furniture, its favourites, its events) is named in the code and held by ContractCheck's
    /// own reading of the built mod. Each part and its members are in this file alone.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiDesigner = ContractBook.Part("ui_designer", "ui", "the interior designer drawn on the RLO interface");

        private const string DesignerAssembly = "BigAmbitions.InteriorDesigner";
        private const string DesignerUi = "UI.InteriorDesigner.InteriorDesignerUI";
        private const string DesignerTools = "BigAmbitions.InteriorDesigner.Tools.";

        // The designer's own screen: its toolbar (the game switches it on as the designer opens and off as it closes), the info panels' and action panels' roots,
        // the redo button's state, the buttons for the floors, and the two stacks of what was done (the undo stack says what this edit is made of).
        public static readonly GameMember DesignerToolbar = Field(UiDesigner, Game, DesignerUi, "toolbar", "GameObject");
        public static readonly GameMember DesignerInfoPanels = Field(UiDesigner, Game, DesignerUi, "infoPanelUI", "InteriorDesignerInfoPanelUI");
        public static readonly GameMember DesignerActionPanels = Field(UiDesigner, Game, DesignerUi, "actionPanelUI", "InteriorDesignerActionPanelUI");
        public static readonly GameMember DesignerRedo = Field(UiDesigner, Game, DesignerUi, "redoButton", "Button");
        public static readonly GameMember DesignerUndoStack = Field(UiDesigner, Game, DesignerUi, "_undoStack", "Stack<IRevertibleAction>");
        public static readonly GameMember DesignerRedoStack = Field(UiDesigner, Game, DesignerUi, "_redoStack", "Stack<IRevertibleAction>");

        // The furniture panel's own list of what it offers, and the tags it shows; the time of day's slider and its two ends; the blueprint's name field.
        public static readonly GameMember DesignerAvailable = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureActionPanelUi", "_availableItemNames", "List<String>");
        public static readonly GameMember DesignerVisibleTags = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureActionPanelUi", "visibleTagsInFurnitureTool", "List<String>");
        public static readonly GameMember DesignerSlider = Field(UiDesigner, Game, "UI.InteriorDesigner.TimeOfDayActionPanelUI", "timeOfDaySlider", "Slider");
        public static readonly GameMember DesignerSliderLeast = Field(UiDesigner, Game, "UI.InteriorDesigner.TimeOfDayActionPanelUI", "minMinute", "Int32");
        public static readonly GameMember DesignerSliderMost = Field(UiDesigner, Game, "UI.InteriorDesigner.TimeOfDayActionPanelUI", "maxMinute", "Int32");
        public static readonly GameMember DesignerBlueprintUi = Field(UiDesigner, Game, "UI.InteriorDesigner.SaveBlueprintActionPanelUI", "saveBlueprintUI", "SaveBlueprintUI");
        public static readonly GameMember DesignerBlueprintName = Field(UiDesigner, Game, "UI.InteriorDesigner.SaveBlueprintUI", "blueprintNameInputField", "TMP_InputField");

        // The wall and floor panel: the picker of a material's colours, and the buttons the game made for each material (a click on one is the game's own choosing).
        public static readonly GameMember DesignerPicker = Field(UiDesigner, Game, "UI.InteriorDesigner.InteriorElementActionPanelUI", "colorPicker", "Transform");
        public static readonly GameMember DesignerPickerTile = Field(UiDesigner, Game, "UI.InteriorDesigner.InteriorElementActionPanelUI", "colorTemplate", "Transform");

        // The question of what a blueprint is for, when the unit has no kind of business: its list, its two buttons.
        public static readonly GameMember DesignerAskList = Field(UiDesigner, Game, "UI.InteriorDesigner.DropdownSelectorUI", "dropdown", "Dropdown");
        public static readonly GameMember DesignerAskConfirm = Field(UiDesigner, Game, "UI.InteriorDesigner.DropdownSelectorUI", "confirmButton", "Button");
        public static readonly GameMember DesignerAskOptions = Field(UiDesigner, Game, "UI.Elements.Dropdown", "_options", "List<String>");

        // What each action of the undo stack cost or paid, and what it touched: a bought piece, a copy, a sale, a laid floor or wall, and the composites of them.
        public static readonly GameMember DesignerBoughtPrice = Field(UiDesigner, DesignerAssembly, DesignerTools + "FurnitureRevertibleAction", "_price", "Single");
        public static readonly GameMember DesignerCopyPrice = Field(UiDesigner, DesignerAssembly, DesignerTools + "DuplicateRevertibleAction", "_price", "Single");
        public static readonly GameMember DesignerSoldPrice = Field(UiDesigner, DesignerAssembly, DesignerTools + "SellRevertibleAction", "_price", "Single");
        public static readonly GameMember DesignerSoldPackPrice = Field(UiDesigner, DesignerAssembly, DesignerTools + "PackageSellRevertibleAction", "_price", "Single");
        public static readonly GameMember DesignerLaidCost = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_cost", "Single");
        public static readonly GameMember DesignerLaidIds = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_interiorElementIds", "String[]");
        public static readonly GameMember DesignerLaidIndices = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_materialIndices", "Int32[]");
        public static readonly GameMember DesignerLaidOldIds = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_oldPresetIds", "String[]");
        public static readonly GameMember DesignerLaidOldColours = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_oldPresetColorIndices", "Int32[]");
        public static readonly GameMember DesignerLaidNewId = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_newPresetId", "String");
        public static readonly GameMember DesignerLaidNewColour = Field(UiDesigner, DesignerAssembly, DesignerTools + "InteriorElementRevertibleAction", "_newPresetColorIndex", "Int32");
        public static readonly GameMember DesignerComposed = Field(UiDesigner, DesignerAssembly, "BigAmbitions.InteriorDesigner.CompositeRevertibleAction", "_actions", "IRevertibleAction[]");

        // The action panels' own list (the furniture panel, the materials, the item lists, the time of day, the blueprint), found once as the designer opens.
        public static readonly GameMember DesignerPanelList = Field(UiDesigner, Game, "UI.InteriorDesigner.InteriorDesignerActionPanelUI", "actionPanels", "List<ActionPanelUI>");

        // The info panels' own list (cost, score, capacity, requirements), read so a panel another mod added to it can be left showing.
        public static readonly GameMember DesignerInfoList = Field(UiDesigner, Game, "UI.InteriorDesigner.InteriorDesignerInfoPanelUI", "infoPanels", "List<InfoPanelUI>");

        // The furniture panel's pieces the face mirrors and presses: its table of tiles, its search, its All and Favourites switches, and a category's own switch.
        public static readonly GameMember DesignerScroller = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureActionPanelUi", "scrollingController", "IDItemTemplateScrollingController");
        public static readonly GameMember DesignerSearch = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureActionPanelUi", "searchField", "InputField");
        public static readonly GameMember DesignerAllToggle = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureActionPanelUi", "showAllItemsToggle", "Toggle");
        public static readonly GameMember DesignerFavToggle = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureActionPanelUi", "showFavoriteItemsToggle", "Toggle");
        public static readonly GameMember DesignerCategoryToggle = Field(UiDesigner, Game, "UI.InteriorDesigner.FurnitureCategoryToggle", "toggle", "Toggle");

        // The item lists (producers, counters, cameras): the things the panel lists, where its buttons stand, which one is chosen and each button's own press.
        public static readonly GameMember DesignerListControllers = Field(UiDesigner, Game, "UI.InteriorDesigner.ItemActionPanelUI", "allItemControllers", "List<ItemController>");
        public static readonly GameMember DesignerListContainer = Field(UiDesigner, Game, "UI.InteriorDesigner.ItemActionPanelUI", "itemsContainer", "Transform");
        public static readonly GameMember DesignerListSelected = Field(UiDesigner, Game, "UI.InteriorDesigner.ItemActionPanelUI", "_selectedButtonHashCode", "Int32");
        public static readonly GameMember DesignerListNone = Field(UiDesigner, Game, "UI.InteriorDesigner.ItemActionPanelUI", "noItemsLabel", "GameObject");
        public static readonly GameMember DesignerListFocus = Field(UiDesigner, Game, "UI.InteriorDesigner.IDItemTemplateBase", "focusButton", "Button");

        // The question's box and its two texts.
        public static readonly GameMember DesignerAskContainer = Field(UiDesigner, Game, "UI.InteriorDesigner.DropdownSelectorUI", "container", "GameObject");
        public static readonly GameMember DesignerAskHeader = Field(UiDesigner, Game, "UI.InteriorDesigner.DropdownSelectorUI", "headerLabel", "TextLocalizationComponent");
        public static readonly GameMember DesignerAskBody = Field(UiDesigner, Game, "UI.InteriorDesigner.DropdownSelectorUI", "bodyLabel", "TextLocalizationComponent");
    }
}
