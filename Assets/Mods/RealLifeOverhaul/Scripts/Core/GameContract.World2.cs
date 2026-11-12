namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the world's smaller windows on the RLO interface (Scripts/Ui/World): the private
    /// driver beside the phone, the items list, a sign's look, the preview terminal, the preview's bar and the custom colour
    /// picker. Each window is its own part, so a change in one game window gives only that one back to the game.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiWorldDriver = ContractBook.Part("ui_world_driver", "ui", "the phone's private driver list drawn on the RLO interface");
        public static readonly GamePart UiWorldItems = ContractBook.Part("ui_world_items", "ui", "the items list of a store's goods and an order's drawn on the RLO interface");
        public static readonly GamePart UiWorldSign = ContractBook.Part("ui_world_sign", "ui", "a business sign's look drawn on the RLO interface");
        public static readonly GamePart UiWorldTerminal = ContractBook.Part("ui_world_terminal", "ui", "the interior firm's preview terminal drawn on the RLO interface");
        public static readonly GamePart UiWorldPreview = ContractBook.Part("ui_world_preview", "ui", "the bar of a building's preview drawn on the RLO interface");
        public static readonly GamePart UiWorldColour = ContractBook.Part("ui_world_colour", "ui", "the custom colour picker framed on the RLO interface");

        // The private driver (Player.HUD.SmartphoneUI.SmartphonePrivateDriverUI): the firm that drives, for its name and its building.
        public static readonly GameMember DriverService = Field(UiWorldDriver, Game, "Player.HUD.SmartphoneUI.SmartphonePrivateDriverUI", "privateDriverService", "SpecialService");

        // The items list (ItemsList) and its rows: the words, price and picture of each, and in an order its add button and amount.
        public static readonly GameMember ItemsTitle = Field(UiWorldItems, Game, "ItemsList", "windowTitle", "TMP_Text");
        public static readonly GameMember ItemsSearch = Field(UiWorldItems, Game, "ItemsList", "searchInputField", "InputField");
        public static readonly GameMember ItemsEntries = Field(UiWorldItems, Game, "ItemsList", "_entriesList", "List<ItemsListEntry>");
        public static readonly GameMember ItemsPrice = Field(UiWorldItems, Game, "ItemsListEntry", "priceLabel", "TMP_Text");
        public static readonly GameMember ItemsIcon = Field(UiWorldItems, Game, "ItemsListEntry", "iconImage", "Image");
        public static readonly GameMember ItemsAddMark = Field(UiWorldItems, Game, "UI.Dialog.DeliveryItemEntry", "selectButtonImage", "Image");
        public static readonly GameMember ItemsAdd = Field(UiWorldItems, Game, "UI.Dialog.DeliveryItemEntry", "hoverOutlineButton", "Button");
        public static readonly GameMember ItemsLess = Field(UiWorldItems, Game, "UI.AmountSelector", "decreaseButton", "Button");
        public static readonly GameMember ItemsMore = Field(UiWorldItems, Game, "UI.AmountSelector", "increaseButton", "Button");
        public static readonly GameMember ItemsAmount = Field(UiWorldItems, Game, "UI.AmountSelector", "amountInput", "TMP_InputField");
        // The store's row the BizMan app lists its goods with, for the test that fills the list as BizMan does.
        public static readonly GameMember ItemsStoreRow = Optional(Field(UiWorldItems, Game, "BizManPresentation", "storeEntryTemplate", "ItemsListEntry"));

        // A sign's look (SignAppearance): its kind as a drop-down and its two rows of colours (ColorListUI).
        public static readonly GameMember SignKind = Field(UiWorldSign, Game, "SignAppearance", "signTypeDropdown", "Dropdown");
        public static readonly GameMember SignNeon = Field(UiWorldSign, Game, "SignAppearance", "signColorList", "ColorListUI");
        public static readonly GameMember SignBulbs = Field(UiWorldSign, Game, "SignAppearance", "bulbColorList", "ColorListUI");
        public static readonly GameMember SignColours = Field(UiWorldSign, Game, "ColorListUI", "container", "Transform");
        public static readonly GameMember SignAddColour = Field(UiWorldSign, Game, "ColorListUI", "addColorButton", "Transform");
        public static readonly GameMember SignOptions = Field(UiWorldSign, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember SignOptionsLocalized = Optional(Field(UiWorldSign, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean"));
        public static readonly GameMember SignPlaceholder = Optional(Field(UiWorldSign, Game, "UI.Elements.Dropdown", "selectedOptionPlaceholderLabel", "TMP_Text"));

        // The preview terminal (UI.CustomUI.PreviewTerminalUI): its four drop-downs, one after another, and its preview button.
        public static readonly GameMember TerminalBuilding = Field(UiWorldTerminal, Game, "UI.CustomUI.PreviewTerminalUI", "buildingTypeDropdown", "Dropdown");
        public static readonly GameMember TerminalSize = Field(UiWorldTerminal, Game, "UI.CustomUI.PreviewTerminalUI", "buildingSizeDropdown", "Dropdown");
        public static readonly GameMember TerminalBusiness = Field(UiWorldTerminal, Game, "UI.CustomUI.PreviewTerminalUI", "businessTypeDropdown", "Dropdown");
        public static readonly GameMember TerminalDesign = Field(UiWorldTerminal, Game, "UI.CustomUI.PreviewTerminalUI", "designDropdown", "Dropdown");
        public static readonly GameMember TerminalPreview = Field(UiWorldTerminal, Game, "UI.CustomUI.PreviewTerminalUI", "previewDesignButton", "Button");
        public static readonly GameMember TerminalOptions = Field(UiWorldTerminal, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember TerminalOptionsLocalized = Optional(Field(UiWorldTerminal, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean"));

        // The preview's bar (UI.BuildingPreview): the place or the design it shows.
        public static readonly GameMember PreviewHeadline = Field(UiWorldPreview, Game, "UI.BuildingPreview", "headlineLabel", "TextMeshProUGUI");

        // The custom colour picker (CustomColorPicker): the game's own colour field, which the kit frames and never draws.
        public static readonly GameMember ColourField = Field(UiWorldColour, Game, "CustomColorPicker", "picker", "ColorPicker");
    }
}
