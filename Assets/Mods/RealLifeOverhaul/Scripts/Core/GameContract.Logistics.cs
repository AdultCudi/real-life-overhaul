namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for delivery trips and food spoilage (Economy/Logistics): the logistics plans and their stops
    /// the trips run and hold, the game's own delivery of a stop and its end of a run, a warehouse's vehicle slots, a vehicle
    /// type's room and stops, a driver's wage, the pump's price, the rival shops' valued days and the game's valuing of them.
    /// The code names the plans, the stops and the slots directly, so a game without them fails to load the feature, which
    /// the guard then switches off; the pump's price is read by name, the rule's own price without it. Self-contained.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart TripsPart = ContractBook.Part("delivery_trips", "trips", "the logistics routes run as delivery trips, and food spoilage at the player's places");

        private const string ItemsAsm = "BigAmbitions.Items";
        private const string Hq = "Buildings.Office.Headquarters.";

        public static readonly GameMember TripPlanStops = Field(TripsPart, Game, Hq + "LogisticsManagerPlan", "destinations", "List<LogisticsManagerPlanDestination>");
        public static readonly GameMember TripPlanFrom = Field(TripsPart, Game, Hq + "LogisticsManagerPlan", "targetAddress", "Address");
        public static readonly GameMember TripPlanDeliver = Method(TripsPart, Game, Hq + "LogisticsManagerPlan", "DeliverDestination", "Void", "LogisticsManagerPlanDestination");
        public static readonly GameMember TripPlanFinished = StaticMethod(TripsPart, Game, Hq + "LogisticsManagerPlan", "OnFinishedDeliveries", "Void");
        public static readonly GameMember TripStopTargets = Field(TripsPart, Game, "Entities.LogisticsManagerPlanDestination", "stockTargets", "List<ItemAmountTarget>");
        public static readonly GameMember TripTargetAmount = Field(TripsPart, ItemsAsm, "BigAmbitions.Items.ItemAmountTarget", "targetAmount", "Int32");
        public static readonly GameMember TripSlots = Field(TripsPart, Game, "Entities.Warehouse", "vehicleSlots", "List<VehicleSlot>");
        public static readonly GameMember TripSlotDriver = Field(TripsPart, Game, "Entities.VehicleSlot", "employeeDriverId", "String");
        public static readonly GameMember TripRoom = Field(TripsPart, Game, "Vehicles.VehicleTypes.VehicleType", "maxCargoCapacity", "Int32");
        public static readonly GameMember TripTypeStops = Field(TripsPart, Game, "Vehicles.VehicleTypes.VehicleType", "destinationsThatCanDeliver", "Int32");
        public static readonly GameMember TripWage = Field(TripsPart, Game, "Entities.EmployeeInstance", "hourlyWage", "Single");
        public static readonly GameMember TripBoxSize = Field(TripsPart, ItemsAsm, "BigAmbitions.Items.Item", "boxSize", "Int32");
        public static readonly GameMember TripWeighed = Field(TripsPart, ItemsAsm, "BigAmbitions.Items.Item", "requiresWeighing", "Boolean");
        public static readonly GameMember RivalDays = Field(TripsPart, Game, "BuildingRegistration", "dailyIncomes", "List<Single>");
        public static readonly GameMember RivalValuing = StaticMethod(TripsPart, Game, "Helpers.CompetitionHelper", "AreRecalculationsPending", "Boolean");

        // Economy/Logistics/LogisticsGame.PumpCents: without it the rule's own price, the game's of today (1.30 a litre)
        public static readonly GameMember PumpPrice = Optional(StaticField(TripsPart, Game, "UI.Overlays.GasStationOverlay", "PricePerLiter", "Single"));
    }
}
