namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's line for the subway stations the mod stands itself (Scripts/Subway): the game's window of stations keeps its entries in a
    /// private list, which a station of the mod's own joins at its name's place so the game's own show, pick and search treat it as one of its
    /// twenty. A game that keeps them another way leaves the districts without their stations, with one line. Leans on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart OwnStationsPart = ContractBook.Part("own_stations", "uptown_subway", "the subway stations of Uptown and Cedar Heights in the game's window of stations");

        public static readonly GameMember OwnStationEntries = Field(OwnStationsPart, Game, "CityMapSubwayStations", "_generatedEntries", "List<CityMapSubwayStationEntry>");
    }
}
