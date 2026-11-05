namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the rival businesses the tycoons' outlets stand on (Rivals/RivalSeatsGame): a rival's registration, its crew of AI employees (id, main
    /// skill and its value, whether the player poached them, their expected hours) and the wage the game pays for a skill. Without them the outlets are seeded stand-ins.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart RivalSeatsPart = ContractBook.Part("rival_seats", "rivals", "the tycoons' outlets standing on the game's own rival businesses: their crew, wages and rent");

        private const string AiHands = "Buildings.BuildingTypes.Shared.AiBusinessEmployeeData";

        public static readonly GameMember SeatCrew = Field(RivalSeatsPart, Game, "BuildingRegistration", "aiEmployees", "List<AiBusinessEmployeeData>");
        public static readonly GameMember SeatRent = Field(RivalSeatsPart, Game, "BuildingRegistration", "RentPerDay", "Single");
        public static readonly GameMember SeatOwner = Field(RivalSeatsPart, Game, "BuildingRegistration", "businessOwnerRivalId", "String");
        public static readonly GameMember HandId = Field(RivalSeatsPart, Game, AiHands, "id", "String");
        public static readonly GameMember HandSkill = Field(RivalSeatsPart, Game, AiHands, "primarySkillName", "String");
        public static readonly GameMember HandValue = Field(RivalSeatsPart, Game, AiHands, "primarySkillValue", "Single");
        public static readonly GameMember HandPoached = Field(RivalSeatsPart, Game, AiHands, "isPoached", "Boolean");
        public static readonly GameMember HandName = Method(RivalSeatsPart, Game, AiHands, "GetFullName", "String");
        public static readonly GameMember HandHours = Method(RivalSeatsPart, Game, AiHands, "GetExpectedHoursPerWeek", "Int32");
        public static readonly GameMember HandWage = StaticMethod(RivalSeatsPart, Game, "Helpers.EmployeeHelper", "CalculateHourlyWageForSkill", "Single", "String", "Single");
    }
}
