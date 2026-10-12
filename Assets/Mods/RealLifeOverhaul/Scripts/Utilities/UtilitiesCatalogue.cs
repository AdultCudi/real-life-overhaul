namespace RealLifeOverhaul.Utilities
{
    /// <summary>The services the city's utilities supply.</summary>
    public enum Service
    {
        Power = 0,
        Water = 1
    }

    /// <summary>What kind of place an account supplies: a home, or a business by what it uses most.</summary>
    public enum PlaceKind
    {
        Home = 0,
        Office = 1,
        Retail = 2,
        FoodAndDrink = 3,
        Salon = 4,       // hairdressers and clinics: water by the basin
        Gym = 5,
        Nightlife = 6,
        Storage = 7,
        Industry = 8
    }

    /// <summary>Where an account stands.</summary>
    public enum SupplyState
    {
        Pending = 0,     // applied for in person: connects on ConnectDay
        Active = 1,      // supplied and billed by the month
        Temporary = 2,   // a bought place with no account yet: supplied at a surcharge until TemporaryUntil
        Cut = 3,         // not supplied: a bill went unpaid, or the temporary supply ran out
        Closed = 4       // the place is no longer the player's: kept until its last bill is settled
    }

    /// <summary>
    /// The city's power (City Light and Power) and water (Riverside Water), at the start's prices: charged at today's
    /// (Core/Prices) and by the game's calendar (Core/CalendarRules: a month is a twelfth of the year). Game-free, checked
    /// by EconomyCheck. Use is in thousandths of a unit (a kWh, a cubic metre); money in cents until it is billed.
    /// </summary>
    public static class UtilitiesCatalogue
    {
        public const int Services = 2;

        // ---------------------------------------------------------------- tariffs, cents at the start's prices
        public static readonly int[] UnitCents = { 15, 200 };          // a kWh of power, a cubic metre of water
        public static readonly int[] StandingCents = { 50, 30 };       // a day's standing charge, whatever is used

        // ---------------------------------------------------------------- use, a day
        // A home: power by its floor and the people in it, water by the person.
        public const int HomePowerBase = 4000;          // thousandths of a kWh a day, whatever the size
        public const int HomePowerPerSqm = 120;         // ...and so much a square metre
        public const int ExtraPersonShare = 250;        // thousandths more power for each person past the first
        public const int HomeWaterPerPerson = 150;      // thousandths of a cubic metre a person a day
        // A business: so much a square metre on a day it opens, by kind (thousandths of a kWh, of a cubic metre).
        public static readonly int[] PowerPerSqm = { 0, 250, 300, 900, 500, 600, 800, 80, 1500 };
        public static readonly int[] WaterPerSqm = { 0, 4, 3, 30, 60, 20, 10, 1, 25 };
        public const int ClosedShare = 250;             // thousandths of a day's use on a day it does not open (fridges, alarms)
        // The seasons, thousandths, in the order of CalendarRules.Season (winter, spring, summer, autumn): heating in
        // winter and cooling in summer; gardens and showers in summer.
        public static readonly int[] PowerSeason = { 1250, 1000, 1150, 1050 };
        public static readonly int[] WaterSeason = { 950, 1000, 1150, 1000 };

        // ---------------------------------------------------------------- bills
        public const int DueLeast = 2, DueMost = 14;    // a bill is due in half a month, within these days
        public const int CutAfterDays = 3;              // days after its due day an unpaid bill cuts the supply
        public static int LateFee => Prices.At(15);     // once, the day after a bill falls due unpaid
        public static int ReconnectFee => Prices.At(60);

        // ---------------------------------------------------------------- new accounts
        public const int TemporaryDays = 3;             // a bought place is supplied this long before it needs an account
        public const int TemporarySurcharge = 500;      // thousandths on top of the tariff while it is
        public const int ConnectDays = 1;               // an account applied for connects the next morning
        // A month's use held as a deposit, by credit grade (Economy.CreditGrade, very poor to excellent): never refused.
        public static readonly int[] DepositMonths = { 2, 2, 1, 0, 0, 0 };
        public const int DepositBackBills = 6;          // bills paid by their due day in a row before a deposit comes back
    }
}
