namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the pause menu's blueprints window on the RLO interface (Scripts/Ui/Blueprints): the game's
    /// panel, its list and filters, one card's blueprint and picture, and the chosen blueprint's own buttons. Each part and its
    /// members in this file alone; the fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiBlueprints = ContractBook.Part("ui_blueprints", "ui", "the game's blueprints window of the pause menu drawn on the RLO interface");

        // The panel (BlueprintsPanel, the pause menu's blueprintsPanel): its list, its tabs and what it says about its own panels.
        public static readonly GameMember BpPanelList = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsPanel", "blueprintsListUI", "BlueprintsListUI");
        public static readonly GameMember BpPanelTabs = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsPanel", "categoryTemplate", "Transform");
        public static readonly GameMember BpPanelInfoOpen = Prop(UiBlueprints, Game, "BlueprintsUI.BlueprintsPanel", "IsBlueprintInfoOpen", "Boolean", false);
        public static readonly GameMember BpPanelConfirmOpen = Prop(UiBlueprints, Game, "BlueprintsUI.BlueprintsPanel", "IsWorkshopConfirmOpen", "Boolean", false);

        // The list: the page it shows, the cards' template, the page's buttons, the empty sentence and the Workshop confirm.
        public static readonly GameMember BpListFilter = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "filterUI", "BlueprintFilterUI");
        public static readonly GameMember BpListSelected = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "selectedBlueprintUI", "SelectedBlueprintUI");
        public static readonly GameMember BpListTemplate = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "blueprintTemplate", "Transform");
        public static readonly GameMember BpListConfirm = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "uploadToWorkshopConfirm", "GameObject");
        public static readonly GameMember BpListNoResults = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "noResultsLabel", "TextLocalizationComponent");
        public static readonly GameMember BpListNext = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "nextPageButton", "Button");
        public static readonly GameMember BpListPrevious = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "previousPageButton", "Button");
        public static readonly GameMember BpListPage = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "_currentPage", "Int32");
        public static readonly GameMember BpListTotal = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "_totalPages", "Int32");
        public static readonly GameMember BpListCategory = StaticField(UiBlueprints, Game, "BlueprintsUI.BlueprintsListUI", "BlueprintCategory", "BlueprintCategory");

        // The filters: the sort's toggles, the filters' own widgets, the search field, and the library's cache of every page it holds.
        public static readonly GameMember BpFilterSorts = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintFilterUI", "sortByOptionTemplate", "Transform");
        public static readonly GameMember BpFilterTemplate = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintFilterUI", "filterTemplate", "Transform");
        public static readonly GameMember BpFilterSearch = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintFilterUI", "searchInputField", "TMP_InputField");
        public static readonly GameMember BpLibraryCache = Field(UiBlueprints, Game, "Blueprints.BlueprintController", "blueprintsCache", "Dictionary<Int32,List<Blueprint>>");

        // One card: the blueprint it shows and the picture the game has loaded for it.
        public static readonly GameMember BpElementBlueprint = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintElementUI", "_blueprint", "Blueprint");
        public static readonly GameMember BpElementPicture = Field(UiBlueprints, Game, "BlueprintsUI.BlueprintElementUI", "thumbnail", "Image");

        // The chosen blueprint: which it is, who made it as the game words it, and the buttons the game offers for it.
        public static readonly GameMember BpSelectedBlueprint = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "_selectedBlueprint", "Blueprint");
        public static readonly GameMember BpSelectedAuthor = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "authorLabel", "TextLocalizationComponent");
        public static readonly GameMember BpSelectedAdd = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "addToYourLibraryButton", "Button");
        public static readonly GameMember BpSelectedUpload = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "uploadToWorkshopButton", "Button");
        public static readonly GameMember BpSelectedUploadTip = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "uploadToWorkshopTooltip", "BasicTooltip");
        public static readonly GameMember BpSelectedRemoveWorkshop = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "removeFromWorkshopButton", "Button");
        public static readonly GameMember BpSelectedRemoveLibrary = Field(UiBlueprints, Game, "BlueprintsUI.SelectedBlueprintUI", "removeFromLibraryButton", "Button");
    }
}
