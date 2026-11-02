namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the buying screens and what the player holds on the RLO interface (Scripts/Ui/Buy):
    /// the checkout, buying a vehicle, the item panel, manage cargo and the clothes, hair and surgery screens. Each part and
    /// its members in this file alone; the fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiCheckout = ContractBook.Part("ui_checkout", "ui", "the game's checkout drawn on the RLO interface");
        public static readonly GamePart UiVehicleBuy = ContractBook.Part("ui_vehicle_buy", "ui", "the game's vehicle purchase drawn on the RLO interface");
        public static readonly GamePart UiItemPanel = ContractBook.Part("ui_item_panel", "ui", "the game's item panel drawn on the RLO interface");
        public static readonly GamePart UiManageCargo = ContractBook.Part("ui_manage_cargo", "ui", "the game's manage cargo drawn on the RLO interface");
        public static readonly GamePart UiWardrobe = ContractBook.Part("ui_wardrobe", "ui", "the game's clothes, hair and surgery screens drawn on the RLO interface");

        // The checkout (PurchaseUI on the player's HUD): its panel the game switches on and off, its rows, total, queue and buttons.
        public static readonly GameMember BuyHud = Field(UiCheckout, Game, "UI.PlayerHUD.PlayerHUD", "purchaseUI", "PurchaseUI");
        public static readonly GameMember BuyPanel = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "panel", "Image");
        public static readonly GameMember BuyItems = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "itemsContainer", "Transform");
        public static readonly GameMember BuyItemTemplate = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "itemTemplate", "Transform");
        public static readonly GameMember BuyTaxTemplate = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "taxItemTemplate", "Transform");
        public static readonly GameMember BuyTotal = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "totalPrice", "GameObject");
        public static readonly GameMember BuyTotalValue = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "totalPriceValue", "TextMeshProUGUI");
        public static readonly GameMember BuyQueue = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "inQueueLabel", "TextMeshProUGUI");
        public static readonly GameMember BuyOrder = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "orderButton", "Button");
        public static readonly GameMember BuyCancel = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "cancelButton", "Button");
        public static readonly GameMember BuyTitle = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "titleLabel", "TextLocalizationComponent");
        public static readonly GameMember BuyCargo = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "_cargoInstances", "List<CargoInstance>");
        public static readonly GameMember BuyTaxTypes = Field(UiCheckout, Game, "UI.Purchase.PurchaseUI", "_irsTaxPaymentTypes", "List<TaxPaymentType>");
        public static readonly GameMember BuyRowName = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiItem", "nameLabel", "TextLocalizationComponent");
        public static readonly GameMember BuyRowPrice = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiItem", "priceLabel", "TextMeshProUGUI");
        public static readonly GameMember BuyRowStepper = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiItem", "amountSelector", "AmountSelector");
        public static readonly GameMember BuyRowButton = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiItem", "button", "Button");
        public static readonly GameMember BuyRowPacks = Prop(UiCheckout, Game, "UI.Purchase.PurchaseUiItem", "Amount", "Int32", false);
        public static readonly GameMember StepLess = Field(UiCheckout, Game, "UI.AmountSelector", "decreaseButton", "Button");
        public static readonly GameMember StepMore = Field(UiCheckout, Game, "UI.AmountSelector", "increaseButton", "Button");
        public static readonly GameMember TaxName = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiTax", "nameLabel", "TextLocalizationComponent");
        public static readonly GameMember TaxOwed = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiTax", "priceLabel", "TextMeshProUGUI");
        public static readonly GameMember TaxInput = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiTax", "amountInput", "InputField");
        public static readonly GameMember TaxPay = Field(UiCheckout, Game, "UI.Purchase.PurchaseUiTax", "button", "Button");
        public static readonly GameMember TaxField = Field(UiCheckout, Game, "UI.Components.InputField", "tmpInputField", "TMP_InputField");
        public static readonly GameMember TaxSetText = Method(UiCheckout, Game, "UI.Components.InputField", "SetText", "Void", "String", "Boolean");

        // The vehicle purchase (PurchaseVehicleUI): its panel, the vehicle's name and price, its specs and colour cells, the delivery and buttons.
        public static readonly GameMember CarHud = Field(UiVehicleBuy, Game, "UI.PlayerHUD.PlayerHUD", "purchaseVehicleUI", "PurchaseVehicleUI");
        public static readonly GameMember CarPanel = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "panel", "GameObject");
        public static readonly GameMember CarName = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "assetName", "TextLocalizationComponent");
        public static readonly GameMember CarPrice = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "assetPrice", "TextMeshProUGUI");
        public static readonly GameMember CarSpecTemplate = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "specTemplate", "Transform");
        public static readonly GameMember CarColourTemplate = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "colorTemplate", "Transform");
        public static readonly GameMember CarCancel = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "cancelButton", "Button");
        public static readonly GameMember CarBuy = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "purchaseButton", "Button");
        public static readonly GameMember CarDeliveryPart = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "deliveryPart", "GameObject");
        public static readonly GameMember CarDeliveryPrice = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "deliveryPrice", "TextMeshProUGUI");
        public static readonly GameMember CarDelivery = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "deliveryToggle", "Toggle");
        public static readonly GameMember CarWarehouse = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "warehouseDropdown", "Dropdown");
        public static readonly GameMember CarWarehouses = Field(UiVehicleBuy, Game, "UI.PurchaseVehicle.PurchaseVehicleUI", "_warehouseDropdownOptions", "List<String>");
        public static readonly GameMember CarNextWarehouse = Method(UiVehicleBuy, Game, "UI.Elements.Dropdown", "SelectNextOption", "Void");
        public static readonly GameMember CarChosenWarehouse = Prop(UiVehicleBuy, Game, "UI.Elements.Dropdown", "SelectedOptionIndex", "Int32", false);
        public static readonly GameMember CarNoWarehouse = Field(UiVehicleBuy, Game, "UI.Elements.Dropdown", "onMiddleBoxClicked", "UnityEvent");

        // The item panel (ItemPanelUI): its own fade group (the one thing the kit writes), the thing, its readings, cargo and buttons.
        public static readonly GameMember PanelHud = Field(UiItemPanel, Game, "UI.PlayerHUD.PlayerHUD", "itemPanelUI", "ItemPanelUI");
        public static readonly GameMember PanelGroup = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "panel", "CanvasGroup");
        public static readonly GameMember PanelShown = Prop(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "IsVisible", "Boolean", true);
        public static readonly GameMember PanelInfo = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "vehicleInfo", "VehicleInfoPanel");
        public static readonly GameMember PanelName = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "itemNameLabel", "TextLocalizationComponent");
        public static readonly GameMember PanelAutoPark = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "autoParkButton", "Button");
        public static readonly GameMember PanelSell = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "sellButton", "Button");
        public static readonly GameMember PanelMop = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "useMopButton", "Button");
        public static readonly GameMember PanelPark = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "parkButton", "Button");
        public static readonly GameMember PanelSleep = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "sleepButton", "Button");
        public static readonly GameMember PanelGrab = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "grabButton", "Button");
        public static readonly GameMember PanelDiscard = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "discardButton", "Button");
        public static readonly GameMember PanelLeave = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "leaveButton", "Button");
        public static readonly GameMember PanelPlace = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "placeButton", "Button");
        public static readonly GameMember PanelPlaceLabel = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "placeButtonLabel", "TextLocalizationComponent");
        public static readonly GameMember PanelMeta = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "metaInfo", "Transform");
        public static readonly GameMember PanelCargo = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "cargoContainer", "GameObject");
        public static readonly GameMember PanelSlotTemplate = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "cargoSlotTemplate", "Transform");
        public static readonly GameMember PanelUpkeep = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "maintenanceInfo", "Transform");
        public static readonly GameMember PanelItem = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "selectedItemInstance", "ItemInstance");
        public static readonly GameMember PanelCar = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "_vehicleController", "VehicleController");
        public static readonly GameMember PanelMiniGame = Field(UiItemPanel, Game, "UI.ItemPanel.ItemPanelUI", "_isInMiniGame", "Boolean");
        public static readonly GameMember InfoFuel = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "fuel", "GameObject");
        public static readonly GameMember InfoFuelValue = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "fuelValue", "TMP_Text");
        public static readonly GameMember InfoCondition = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "condition", "GameObject");
        public static readonly GameMember InfoConditionValue = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "conditionValue", "TMP_Text");
        public static readonly GameMember InfoCargo = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "cargo", "GameObject");
        public static readonly GameMember InfoParking = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "parkingZone", "GameObject");
        public static readonly GameMember InfoParkingValue = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "parkingZoneValue", "TextLocalizationComponent");
        public static readonly GameMember InfoSteering = Field(UiItemPanel, Game, "UI.ItemPanel.VehicleInfoPanel", "steeringAssist", "GameObject");
        public static readonly GameMember CarType = Field(UiItemPanel, Game, "VehicleController", "vehicleType", "VehicleType");
        public static readonly GameMember CarCargo = Field(UiItemPanel, Game, "VehicleController", "vehicleInstance", "VehicleInstance");

        // A stack of cargo (CargoItemUi, in the item panel's slots and in manage cargo, as the game and Going Public set it up).
        public static readonly GameMember SlotName = Field(new[] { UiItemPanel, UiManageCargo }, Game, "UI.PlayerHUD.CargoItemUi", "nameLabel", "TextLocalizationComponent");
        public static readonly GameMember SlotPieces = Field(new[] { UiItemPanel, UiManageCargo }, Game, "UI.PlayerHUD.CargoItemUi", "amountLabel", "TMP_Text");
        public static readonly GameMember SlotCount = Field(new[] { UiItemPanel, UiManageCargo }, Game, "UI.PlayerHUD.CargoItemUi", "cargoAmount", "TMP_Text");
        public static readonly GameMember SlotPrice = Field(new[] { UiItemPanel, UiManageCargo }, Game, "UI.PlayerHUD.CargoItemUi", "priceLabel", "TMP_Text");
        public static readonly GameMember SlotButton = Field(new[] { UiItemPanel, UiManageCargo }, Game, "UI.PlayerHUD.CargoItemUi", "itemButton", "Button");
        public static readonly GameMember SlotAction = Field(UiManageCargo, Game, "UI.PlayerHUD.CargoItemUi", "actionButton", "Button");
        public static readonly GameMember SlotActionLabel = Field(UiManageCargo, Game, "UI.PlayerHUD.CargoItemUi", "actionButtonLabel", "TextLocalizationComponent");
        public static readonly GameMember SlotDiscard = Field(UiManageCargo, Game, "UI.PlayerHUD.CargoItemUi", "discardButton", "Button");
        public static readonly GameMember SlotSell = Field(UiManageCargo, Game, "UI.PlayerHUD.CargoItemUi", "sellButton", "Button");

        // Manage cargo (ManageCargoUi): its panel, its stacks' template, Sell all, its contents line and the holder.
        public static readonly GameMember CargoHud = Field(UiManageCargo, Game, "UI.PlayerHUD.PlayerHUD", "manageCargoUI", "ManageCargoUi");
        public static readonly GameMember CargoPanel = Field(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "panel", "GameObject");
        public static readonly GameMember CargoTemplate = Field(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "itemTemplate", "Transform");
        public static readonly GameMember CargoSellAll = Field(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "sellAllButton", "Button");
        public static readonly GameMember CargoContents = Field(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "contentsLabel", "TextLocalizationComponent");
        public static readonly GameMember CargoItems = Field(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "_cargoItems", "List<CargoItem>");
        public static readonly GameMember CargoHolder = Field(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "currentCargoHolder", "ICargoHolder");
        public static readonly GameMember CargoClose = Method(UiManageCargo, Game, "UI.MergeCargo.ManageCargoUi", "Close", "Void");

        // The character screens: the menu of categories, the pieces and colours, the body's sliders, each screen's buttons.
        public static readonly GameMember LookClothes = Field(UiWardrobe, Game, "UI.UIs", "changeCharacterClothesUI", "ChangeCharacterClothesUI");
        public static readonly GameMember LookHair = Field(UiWardrobe, Game, "UI.UIs", "changeCharacterHairUI", "ChangeCharacterHairUI");
        public static readonly GameMember LookSurgery = Field(UiWardrobe, Game, "UI.UIs", "plasticSurgeryUI", "PlasticSurgeryUI");
        public static readonly GameMember LookMenu = Field(UiWardrobe, Game, "Character.Customization.CharacterCustomizer", "menu", "MenuVertical");
        public static readonly GameMember LookPicker = Field(UiWardrobe, Game, "Character.Customization.CharacterCustomizer", "elementPicker", "ElementPicker");
        public static readonly GameMember LookColours = Field(UiWardrobe, Game, "Character.Customization.CharacterCustomizer", "appearanceColorPicker", "AppearanceColorPicker");
        public static readonly GameMember LookShown = Field(UiWardrobe, Game, "Character.Customization.CharacterCustomizer", "currentElementType", "AppearanceElementType");
        public static readonly GameMember LookCategories = Field(UiWardrobe, Game, "MenuVertical", "categories", "MenuVertical.Category[]");
        public static readonly GameMember LookCategory = Field(UiWardrobe, Game, "MenuVertical", "_selectedCategoryId", "Int32");
        public static readonly GameMember LookSubMenu = Field(UiWardrobe, Game, "MenuVertical", "subMenu", "MenuVerticalSubMenu");
        public static readonly GameMember LookOpenCategory = Method(UiWardrobe, Game, "MenuVertical", "OnCategoryButtonClick", "Void", "MenuVertical.Category");
        public static readonly GameMember LookPieceTemplate = Field(UiWardrobe, Game, "Character.Customization.ElementPicker", "entryTemplate", "Transform");
        public static readonly GameMember LookColourTemplate = Field(UiWardrobe, Game, "Character.Customization.AppearanceColorPicker", "entryTemplate", "Transform");
        public static readonly GameMember LookSave = Field(UiWardrobe, Game, "ChangeCharacterClothesUI", "saveClothesChangesButton", "Button");
        public static readonly GameMember LookClothesLeave = Method(UiWardrobe, Game, "ChangeCharacterClothesUI", "Hide", "Void");
        public static readonly GameMember LookHairButton = Field(UiWardrobe, Game, "ChangeCharacterHairUI", "changeHairButton", "Button");
        public static readonly GameMember LookHairLabel = Field(UiWardrobe, Game, "ChangeCharacterHairUI", "changeHairButtonLabel", "TextLocalizationComponent");
        public static readonly GameMember LookHairLeave = Method(UiWardrobe, Game, "ChangeCharacterHairUI", "Hide", "Void");
        public static readonly GameMember LookBody = Field(UiWardrobe, Game, "Character.Customization.PlasticSurgeryUI", "bodyValues", "BodyValues");
        public static readonly GameMember LookApply = Field(UiWardrobe, Game, "Character.Customization.PlasticSurgeryUI", "applyButton", "Button");
        public static readonly GameMember LookApplyLabel = Field(UiWardrobe, Game, "Character.Customization.PlasticSurgeryUI", "applyButtonLabel", "TextLocalizationComponent");
        public static readonly GameMember LookRandomize = Field(UiWardrobe, Game, "Character.Customization.PlasticSurgeryUI", "randomizeButton", "Button");
        public static readonly GameMember LookRevert = Field(UiWardrobe, Game, "Character.Customization.PlasticSurgeryUI", "revertButton", "Button");
        public static readonly GameMember LookSurgeryLeave = Method(UiWardrobe, Game, "Character.Customization.PlasticSurgeryUI", "Hide", "Void", "Boolean");
        public static readonly GameMember LookStrength = Field(UiWardrobe, Game, "Character.Customization.BodyValues", "strengthSlider", "Slider");
        public static readonly GameMember LookWeight = Field(UiWardrobe, Game, "Character.Customization.BodyValues", "fatnessSlider", "Slider");
    }
}
