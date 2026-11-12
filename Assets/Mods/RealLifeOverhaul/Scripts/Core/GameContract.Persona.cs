namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for Persona, Rivals and Market Insider drawn on the RLO interface (Scripts/Ui/Persona, Rivals,
    /// Market): the pieces of the game's own apps the kit reads after the game (and other mods) filled them, and presses.
    /// Each part and its members are in this file alone; a part file's fields are made in their own order and lean on nothing
    /// in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiPersona = ContractBook.Part("ui_persona", "ui", "the game's Persona drawn on the RLO interface");
        public static readonly GamePart UiRivals = ContractBook.Part("ui_rivals", "ui", "the game's Rivals drawn on the RLO interface");
        public static readonly GamePart UiMarket = ContractBook.Part("ui_market", "ui", "the game's Market Insider drawn on the RLO interface");

        // Persona: the head's labels as the game wrote them when it opened, the list of what moves the happiness (the game's
        // and the mod's moods alike), the education box's rows (the mod's courses among them), and the goals in the order
        // the game's two toggles sort them, with each entry's words.
        public static readonly GameMember PersonaName = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.CharacterInfo", "characterNameLabel", "TMP_Text");
        public static readonly GameMember PersonaAge = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.CharacterInfo", "characterAgeLabel", "TMP_Text");
        public static readonly GameMember PersonaBusinesses = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.CharacterInfo", "totalBusinessesLabel", "TMP_Text");
        public static readonly GameMember PersonaEmployees = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.CharacterInfo", "totalEmployeesLabel", "TMP_Text");
        public static readonly GameMember PersonaModifiers = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.CharacterStats", "happinessStatsList", "Transform");
        public static readonly GameMember PersonaCourseTemplate = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.EducationInfo", "educationEntryTemplate", "EducationInfoEntry");
        public static readonly GameMember PersonaCourseName = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.EducationInfoEntry", "headerLabel", "TextLocalizationComponent");
        public static readonly GameMember PersonaCourseStatus = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.EducationInfoEntry", "statusLabel", "TextLocalizationComponent");
        public static readonly GameMember PersonaCourseTick = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.EducationInfoEntry", "checkmarkObj", "GameObject");
        public static readonly GameMember PersonaGoalGroups = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.PersonalGoalsUI", "_goalTierGroups", "List<PersonalGoalTierGroup>");
        public static readonly GameMember PersonaGoalEntries = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.PersonalGoalsUI", "_goalEntries", "List<Transform>");
        public static readonly GameMember PersonaGoalSort = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.PersonalGoalsUI", "_sortCategory", null);
        public static readonly GameMember PersonaGoalOrder = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.PersonalGoalsUI", "_sortDirection", null);
        public static readonly GameMember PersonaGoalTitle = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.PersonalGoalEntry", "descriptionLabel", "TextLocalizationComponent");
        public static readonly GameMember PersonaGoalProgress = Field(UiPersona, Game, "UI.Smartphone.Apps.Persona.PersonalGoalEntry", "progressLabel", "TextLocalizationComponent");

        // Rivals: the leaderboard's buttons (each with its line's data and its war mark), the chosen rival's panel (Open in
        // Contacts, the chart and its filter, the rivalry's marks), and the two tables with their rows' own buttons.
        public static readonly GameMember RivalsBoard = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.RivalsApp", "leaderboard", "RivalLeaderboard");
        public static readonly GameMember RivalsChosen = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.RivalsApp", "selectedRivalUI", "SelectedRivalUI");
        public static readonly GameMember RivalsBusinessTable = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.RivalsApp", "businessTable", "RivalBusinessesTable");
        public static readonly GameMember RivalsBuildingTable = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.RivalsApp", "realEstateTable", "RivalRealEstateTable");
        public static readonly GameMember RivalsButtons = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.RivalLeaderboard", "_buttons", "List<RivalLeaderboardButton>");
        public static readonly GameMember RivalsWarMark = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.RivalLeaderboardButton", "activeRivalryIcon", "Image");
        public static readonly GameMember RivalsContacts = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.SelectedRivalUI", "openInContactsButton", "Button");
        public static readonly GameMember RivalsChart = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.SelectedRivalUI", "chartController", "ChartController");
        public static readonly GameMember RivalsFilter = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.SelectedRivalUI", "_selectedFilter", "String");
        public static readonly GameMember RivalsMarks = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.SelectedRivalUI", "rivalryIconsParent", "Transform");
        public static readonly GameMember RivalsMarksShown = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.SelectedRivalUI", "activeRivalryAttacksObj", "GameObject");
        public static readonly GameMember RivalsTabContainers = Field(UiRivals, Game, "UI.Components.TabMenu", "containersParent", "Transform");
        public static readonly GameMember RivalsOpenBusiness = Field(UiRivals, Game, "UI.Smartphone.Apps.Rivals.Tables.RivalBusinessesCellView", "cellButton", "Button");
        public static readonly GameMember RivalsOpenBuilding = Field(UiRivals, Game, "UI.Smartphone.Apps.MarketInsider.RealEstateCellView", "button", "Button");

        // Market Insider: the district shown, its events as the game listed them, the filters' own refresh after their toggles
        // and search were set (the panels stay shut, so their own listeners are not on), and a real estate row's button.
        public static readonly GameMember MarketDistrict = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.MarketInsiderNeighborhoodData", "_selectedNeighborhood", "String");
        public static readonly GameMember MarketEvents = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.MarketEventScrollerController", "_data", "List<MarketEventCellView.EventModel>");
        public static readonly GameMember MarketDemandFilters = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.MarketDemandScrollerController", "filterController",
            "MarketDemandFilterController");
        public static readonly GameMember MarketEstateFilters = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.RealEstateScrollerController", "filterController",
            "RealEstateFilterController");
        public static readonly GameMember MarketForSale = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.RealEstateScrollerController", "onlyForSaleToggle", "Toggle");
        public static readonly GameMember MarketDemandRefresh = Method(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.MarketDemandScrollerController", "RefreshFilterAndSort", "Void");
        public static readonly GameMember MarketEstateRefresh = Method(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.RealEstateScrollerController", "RefreshFilterAndSort", "Void");
        public static readonly GameMember MarketViewOnMap = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.MarketInsiderNeighborhoodData", "viewOnMapButton", "Button");
        public static readonly GameMember MarketOpenBuilding = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.RealEstateCellView", "button", "Button");
        public static readonly GameMember MarketDemandAll = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.MarketDemandScrollerController", "_allModels",
            "List<MarketDemandCellView.DemandModel>");
        public static readonly GameMember MarketEstateAll = Field(UiMarket, Game, "UI.Smartphone.Apps.MarketInsider.RealEstateScrollerController", "_allModels",
            "List<RealEstateCellView.RealEstateModel>");
    }
}
