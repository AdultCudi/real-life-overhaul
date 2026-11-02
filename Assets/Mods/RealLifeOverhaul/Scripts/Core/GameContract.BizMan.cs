namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for BizMan on the RLO interface (Scripts/Ui/BizMan): the game's own pieces of its business
    /// list and business page the kit reads after the game (and other mods) filled them, and the buttons it presses. A line
    /// an update takes away leaves its piece out, except the few the host's check needs before it draws BizMan at all.
    /// Self-contained: leans on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart BizManKitPart = ContractBook.Part("bizman_kit", "ui", "BizMan drawn on the RLO interface");

        public static readonly GameMember BizMenu = Field(BizManKitPart, Game, "BizManBusiness", "menu", "Transform");
        public static readonly GameMember BizContainers = Field(BizManKitPart, Game, "BizManBusiness", "containers", "Transform");
        public static readonly GameMember BizInfo = Optional(Field(BizManKitPart, Game, "BizManBusiness", "businessInfo", "Transform"));
        public static readonly GameMember BizBuildingInfo = Optional(Field(BizManKitPart, Game, "BizManBusiness", "buildingInfo", "BizManBuildingInfo"));
        public static readonly GameMember BizSwitch = Field(BizManKitPart, Game, "BizManBusiness", "businessNameDropdown", "Dropdown");
        public static readonly GameMember BizLogo = Optional(Field(BizManKitPart, Game, "BizManBusiness", "businessLogo", "Image"));
        public static readonly GameMember BizKind = Optional(Field(BizManKitPart, Game, "BizManBusiness", "businessTypeLabel", "TextLocalizationComponent"));
        public static readonly GameMember BizNeed = Optional(Field(BizManKitPart, Game, "BizManBusiness", "requirementTemplate", "Transform"));
        public static readonly GameMember BizAlert = Optional(Field(BizManKitPart, Game, "BizManBusiness", "alertTemplate", "Transform"));
        public static readonly GameMember BizClosed = Optional(Field(BizManKitPart, Game, "BizManBusiness", "temporarilyClosedToggle", "Toggle"));
        public static readonly GameMember BizSecurity = Optional(Field(BizManKitPart, Game, "BizManBusiness", "securityInfo", "GameObject"));
        public static readonly GameMember BizSecurityLabel = Optional(Field(BizManKitPart, Game, "BizManBusiness", "securityLevelLabel", "TextLocalizationComponent"));
        public static readonly GameMember BizTab = Field(BizManKitPart, Game, "BizManBusiness", "_selectedTab", "String");
        public static readonly GameMember BizOptions = Field(BizManKitPart, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember BizOptionsLocalized = Optional(Field(BizManKitPart, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean"));
        public static readonly GameMember BizShops = Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManList", "businessScrollerController", "BusinessScrollerController");
        public static readonly GameMember BizHomes =
            Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManList", "privateResidenceScrollerController", "PrivateResidenceScrollerController"));
        public static readonly GameMember BizStores = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManList", "warehouseList", "WarehouseList"));
        public static readonly GameMember BizHeads = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManList", "headquartersList", "HeadquartersList"));
        public static readonly GameMember BizFilter = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BusinessScrollerController", "filterController", "BizManFilterController"));
        public static readonly GameMember BizStoreEntry = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.WarehouseList", "warehouseEntry", "Transform"));
        public static readonly GameMember BizHeadEntry = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.HeadquartersList", "headquartersEntry", "Transform"));
        public static readonly GameMember ShowTop = Optional(Field(BizManKitPart, Game, "BizManPresentation", "topInfoBox", "Transform"));
        public static readonly GameMember ShowBottom = Optional(Field(BizManKitPart, Game, "BizManPresentation", "bottomInfoBox", "Transform"));
        public static readonly GameMember ShowForSale = Optional(Field(BizManKitPart, Game, "BizManPresentation", "forSaleBox", "Transform"));
        public static readonly GameMember ShowNotForSale = Optional(Field(BizManKitPart, Game, "BizManPresentation", "notForSaleBox", "Transform"));
        public static readonly GameMember ShowStart = Optional(Field(BizManKitPart, Game, "BizManPresentation", "startBusinessUI", "StartBusinessUI"));
        public static readonly GameMember ShowRent = Optional(Field(BizManKitPart, Game, "BizManPresentation", "rentBuildingUI", "RentBuildingUI"));
        public static readonly GameMember ShowVilla = Optional(Field(BizManKitPart, Game, "BizManPresentation", "hamptonsPurchaseBoxUI", "HamptonsPurchaseBoxUI"));
        public static readonly GameMember ShowPhoto = Optional(Field(BizManKitPart, Game, "BizManPresentation", "buildingImage", "Image"));
        public static readonly GameMember RentDaily = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RentBuildingUI", "rentPerDayLabel", "TextLocalizationComponent"));
        public static readonly GameMember RentDeposit = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RentBuildingUI", "depositLabel", "TMP_Text"));
        public static readonly GameMember RentAppliances = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RentBuildingUI", "electricalAppliancesLabel", "TMP_Text"));
        public static readonly GameMember RentFirstDay = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RentBuildingUI", "firstDayRentLabel", "TMP_Text"));
        public static readonly GameMember RentTotal = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RentBuildingUI", "totalInitialPaymentLabel", "TextLocalizationComponent"));
        public static readonly GameMember RentFree = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RentBuildingUI", "availableForRentGameObject", "GameObject"));
        public static readonly GameMember VillaValuation =
            Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.HamptonsPurchaseBoxUI", "estimatedValuationLocalizationComponent", "TextLocalizationComponent"));
        public static readonly GameMember VillaPrice =
            Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.HamptonsPurchaseBoxUI", "buildingPriceLocalizationComponent", "TextLocalizationComponent"));
        public static readonly GameMember StartName = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessUI", "businessNameField", "TMP_InputField"));
        public static readonly GameMember StartKinds = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessUI", "businessesContainer", "Transform"));
        public static readonly GameMember KindSelect = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "selectButton", "Button"));
        public static readonly GameMember KindIcon = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "businessTypeIcon", "Image"));
        public static readonly GameMember KindName =
            Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "localizedBusinessTypeName", "TextLocalizationComponent"));
        public static readonly GameMember KindRivals =
            Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "competitorsLabel", "TextLocalizationComponent"));
        public static readonly GameMember KindRivalCount = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "competitorsAmountLabel", "TMP_Text"));
        public static readonly GameMember KindGoodsTitle =
            Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "primaryProductsTitle", "TextLocalizationComponent"));
        public static readonly GameMember KindGoods = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "productsListContainer", "GameObject"));
        public static readonly GameMember KindChosen = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI", "selectedOutline", "GameObject"));
        public static readonly GameMember SeePromotion = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "promotionProgressBar", "ProgressBar"));
        public static readonly GameMember SeeTraffic = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "trafficProgressBar", "ProgressBar"));
        public static readonly GameMember SeeMarketing = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "marketingProgressBar", "ProgressBar"));
        public static readonly GameMember SeeSatisfied = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "overallSatisfactionProgressBar", "ProgressBar"));
        public static readonly GameMember SeeService = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "customerServiceProgressBar", "ProgressBar"));
        public static readonly GameMember SeePricing = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "pricingProgressBar", "ProgressBar"));
        public static readonly GameMember SeeInterior = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "interiorProgressBar", "ProgressBar"));
        public static readonly GameMember SeeClean = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "cleanlinessProgressBar", "ProgressBar"));
        public static readonly GameMember SeeSatisfiedShown = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "customerSatisfactionData", "GameObject"));
        public static readonly GameMember SeeCapacity = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "customerCapacityEntry", "Transform"));
        public static readonly GameMember SeeCapacityNow = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "currentCapacityPerHourLabel", "TextMeshProUGUI"));
        public static readonly GameMember SeeCapacityLimit = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "buildingLimitLabel", "TextMeshProUGUI"));
        public static readonly GameMember SeeChart = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManInsight", "chartController", "ChartController"));
        public static readonly GameMember DealSettings = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManDeliveries", "contractSettings", "BizManContractSettings"));
        public static readonly GameMember DealNone = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManDeliveries", "noContractsWarning", "GameObject"));
        public static readonly GameMember DealEntries = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManDeliveries", "_instantiatedEntries", "List<DeliveryContractEntry>"));
        public static readonly GameMember DealChosen = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManDeliveries", "_selectedContractEntry", "DeliveryContractEntry"));
        public static readonly GameMember DealButton = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.DeliveryContractEntry", "button", "Button"));
        public static readonly GameMember DealLogo = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.DeliveryContractEntry", "businessLogo", "Image"));
        public static readonly GameMember DealName = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.DeliveryContractEntry", "businessNameText", "TMP_Text"));
        public static readonly GameMember DealFee = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.DeliveryContractEntry", "deliveryFeeText", "TMP_Text"));
        public static readonly GameMember DealWarn = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.DeliveryContractEntry", "warningSign", "GameObject"));
        public static readonly GameMember AdsDaily = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManMarketing", "totalDailyExpenses", "TMP_Text"));
        public static readonly GameMember AdsEfficiency = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManMarketing", "efficiencyProgressBar", "ProgressBar"));
        public static readonly GameMember AdsAgency = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManMarketing", "agencyTemplate", "Transform"));
        public static readonly GameMember AdsContact = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManMarketing", "contactEntry", "Transform"));
        public static readonly GameMember AdsContactSplit = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.BizManMarketing", "contactSplitterEntry", "Transform"));
        public static readonly GameMember SetSave = Optional(Field(BizManKitPart, Game, "BizManSettings", "saveChangesButton", "Button"));
        public static readonly GameMember SetName = Optional(Field(BizManKitPart, Game, "BizManSettings", "businessName", "TMP_InputField"));
        public static readonly GameMember SetKind = Optional(Field(BizManKitPart, Game, "BizManSettings", "businessTypeDropdown", "Dropdown"));
        public static readonly GameMember SetLogo = Optional(Field(BizManKitPart, Game, "BizManSettings", "logoCustomizer", "LogoCustomizer"));
        public static readonly GameMember SetUniforms = Optional(Field(BizManKitPart, Game, "BizManSettings", "setUpUniformsWindow", "SetUpUniformsWindow"));
        public static readonly GameMember LogoSave = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.LogoCustomizer", "saveLogoChangesButton", "Button"));
        public static readonly GameMember LogoFont = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.LogoCustomizer", "fontDropdown", "Dropdown"));
        public static readonly GameMember EstateRent = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "rentManagementGameObject", "GameObject"));
        public static readonly GameMember EstateRentNow = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "currentRent", "TextLocalizationComponent"));
        public static readonly GameMember EstateRentMarket = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "avgMarketPrice", "TextLocalizationComponent"));
        public static readonly GameMember EstateSlider = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "newRentSlider", "Slider"));
        public static readonly GameMember EstateApply = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "applyRentButton", "Button"));
        public static readonly GameMember EstateNewRent = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "newRentValue", "TMP_Text"));
        public static readonly GameMember EstateWhen = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "timeLeftUntilNewRent", "TextLocalizationComponent"));
        public static readonly GameMember EstateOccupancy = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "occupancy", "TMP_Text"));
        public static readonly GameMember EstateDailyLabel = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "dailyRentLabel", "TextLocalizationComponent"));
        public static readonly GameMember EstateDaily = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "dailyRentIncome", "TMP_Text"));
        public static readonly GameMember EstateUnit = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "selfOccupiedUnitGameObject", "GameObject"));
        public static readonly GameMember EstateUnitState = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "selfOccupiedUnitState", "TextLocalizationComponent"));
        public static readonly GameMember EstateTakeover = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "selfOccupiedTakeoverUnitButton", "Button"));
        public static readonly GameMember EstateBoughtLabel = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "priceAtPurchaseLabel", "TextLocalizationComponent"));
        public static readonly GameMember EstateBought = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "priceAtPurchaseValue", "TMP_Text"));
        public static readonly GameMember EstateWorth = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "estimatedMarketPriceValue", "TMP_Text"));
        public static readonly GameMember EstatePrice = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "salesPriceInput", "TMP_InputField"));
        public static readonly GameMember EstateGain = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "potentialGainValue", "TMP_Text"));
        public static readonly GameMember EstateForSale = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "cancelSaleLabel", "TextLocalizationComponent"));
        public static readonly GameMember EstateCancelPanel = Optional(Field(BizManKitPart, Game, "UI.Smartphone.Apps.BizMan.RealEstateSettings", "cancelSetForSalePanel", "GameObject"));
        public static readonly GameMember PlaceKind = Optional(Field(BizManKitPart, Game, "BizManBuildingInfo", "buildingType", "TextLocalizationComponent"));
        public static readonly GameMember PlaceTaxLabel = Optional(Field(BizManKitPart, Game, "BizManBuildingInfo", "taxesInfo", "TextLocalizationComponent"));
        public static readonly GameMember PlaceTax = Optional(Field(BizManKitPart, Game, "BizManBuildingInfo", "taxesValue", "TMP_Text"));
    }
}
