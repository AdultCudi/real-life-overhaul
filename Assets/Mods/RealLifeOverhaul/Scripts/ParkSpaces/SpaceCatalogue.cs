using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>
    /// Every number of the parking spaces a player may buy or rent in a building's car park, game-free so the offline checks read the
    /// same table. Each comes from a figure the game or the mod already holds: the game's own charge for a car in a car park, the
    /// month and share the mod's surface car parks let a bay by, the yield and the agent's fee of the property market, and the
    /// floor area the building code asks a space for. Money is in whole units.
    /// </summary>
    public static class SpaceCatalogue
    {
        public const int HourFee = 25;                              // the game's charge an hour for a car left in a building's car park (ParkingSimulator.UndergroundParkingHourlyFee)
        public const int RentDays = CarParkRules.LeaseDays;         // a rented space runs in terms of this many days, as a bay leased at a surface car park
        public const int RentShare = CarParkRules.LeaseShare;       // thousandths of the hourly charge round the clock that a term's rent is, as a leased bay's
        public const int Yield = 84;                                // thousandths a year a space's rent is of its price: the property market's for storage (EstateCatalogue.YieldMiddle)
        public const int BackFee = EstateCatalogue.AgentFee;        // thousandths the building's owner keeps when buying a space back: the property market's agent's fee
        public const int RoundRent = 10, RoundPrice = 100;          // a rent is named in tens, a price in hundreds

        public const int CapacityLeast = 8, CapacityMost = 60;      // spaces a car park has: what the building code asks of the building over it, within these
        public const int HeldMost = 12;                             // spaces one player holds in all
        public const int HeldPerPark = 4;                           // ...and in one car park

        // The city's people: the share of a car park's spaces they hold, by the street's parking rate in the district (dear street parking fills garages).
        public const int CityShareBase = 500;                       // thousandths held where street parking costs nothing
        public const int CitySharePerDollar = 20;                   // ...and this many more for each dollar an hour the street costs
        public const int CityShareLeast = 550, CityShareMost = 900;
        public const int CityTurnDays = 45;                         // days after which a space of theirs may change hands

        // The rival tycoons: one space each at most, looked at every so many days.
        public const int RivalEvery = 20;                           // days between two looks of a tycoon's
        public const int RivalBuyChance = 500;                      // thousandths: a tycoon without a space buys one at a look
        public const int RivalHoldDays = 90;                        // days a tycoon keeps a space at the least
        public const int RivalSellChance = 250;                     // thousandths: a tycoon who has held one long enough sells it back at a look

        public const int GoneDays = 3;                              // days a car park must have been missing before its spaces are settled

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
