namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the city map's panels on the RLO interface (Scripts/Ui/Map): the filters, their groups and lines, the subway stations' list, the map
    /// camera's turn, and the building card's picked place. The game's public members are checked by the contract check as the built mod names them; the private ones the
    /// kit reads by name are these lines, each in this one part, so a game that changes one gives the whole panel back to the game's own. Leans on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiMapDock = ContractBook.Part("ui_map_dock", "ui", "the city map's filters, subway list and picked place drawn on the RLO interface");

        // The filters (CityMapFilters): every line by its name, the master switch, the places the search looks through and the game's own filtering of the lines by the typed words.
        public static readonly GameMember MapFilterEntries = Field(UiMapDock, Game, "CityMapFilters", "_filterEntries", "Dictionary<String,CityMapFilter>");
        public static readonly GameMember MapFilterAll = Field(UiMapDock, Game, "CityMapFilters", "_toggleAll", "Toggle");
        public static readonly GameMember MapFilterBuildings = Field(UiMapDock, Game, "CityMapFilters", "_searchableBuildings", "List<CityBuildingController>");
        public static readonly GameMember MapFilterSearch = Method(UiMapDock, Game, "CityMapFilters", "OnSearchTextChanged", "Void", "String");

        // A line (CityMapFilter): its words, its picture and its locate button; and a group (CityMapFilterCategory): its words and its lines in order.
        public static readonly GameMember MapLineLabel = Field(UiMapDock, Game, "City.CityMap.CityMapFilter", "labelText", "TMP_Text");
        public static readonly GameMember MapLineIcon = Field(UiMapDock, Game, "City.CityMap.CityMapFilter", "icon", "Image");
        public static readonly GameMember MapLineFocus = Field(UiMapDock, Game, "City.CityMap.CityMapFilter", "focusButton", "Button");
        public static readonly GameMember MapGroupLabel = Field(UiMapDock, Game, "City.CityMap.CityMapFilterCategory", "label", "TextLocalizationComponent");
        public static readonly GameMember MapGroupLines = Field(UiMapDock, Game, "City.CityMap.CityMapFilterCategory", "_filters", "List<CityMapFilter>");

        // The subway mode's list of stations (CityMapSubwayStations): its entries, each with its station and its travel button.
        public static readonly GameMember MapStationEntries = Field(UiMapDock, Game, "CityMapSubwayStations", "_generatedEntries", "List<CityMapSubwayStationEntry>");

        // The map camera's own turn, which the north button uses so the camera's 90 degree steps still start from where it stands.
        public static readonly GameMember MapCamRotate = Method(UiMapDock, Game, "CityMapCam", "RotateCamera", "Void", "Single");
    }
}
