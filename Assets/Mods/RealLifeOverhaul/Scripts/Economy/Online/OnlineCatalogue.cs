using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every number that balances the online shops, in one place. Game-free: the offline checks read the same table.
    /// Money is in cents wherever a price or a day's total is kept (a product costs what the game says, to the cent);
    /// the round sums a player pays at once (the set-up, hosting, an advertising campaign) are in whole units at the
    /// start's price index and follow the city's prices (<see cref="CompanyCatalogue.AtPrices"/>).
    ///
    /// WHAT AN ONLINE SHOP ADDS, AND WHAT BOUNDS IT
    /// The measure is a mid-game shop that takes about 6,000 a day over the counter and keeps about 2,000 of it after
    /// goods, wages and rent. An online order is 1 to 3 products, 1 to 3 of each: about 2.5 units, 62 at an average
    /// price of 25. Goods cost about 45% of the going price. A day of a site, as the offline check measures it over
    /// a month (EconomyCheck, "online balance"):
    ///
    ///   site            orders   takings   goods   courier   refunds   hosting   left over   of the shop's 6,000
    ///   Basic              7-8       455     199        41        19         5         190          +7%
    ///   Standard          15-16      962     423        86        29        10         413         +16%
    ///   Professional      23-24    1,441     634       130        45        20         611         +24%
    ///
    /// (at the going price, a next-day promise, a rating held at four stars, no advertising, a steady economy, spring,
    /// a courier at 2 km, general goods, staff to pick everything.) With the player's own vans the 41, 86 or 130 of
    /// courier fees become 17, 32 or 44 of running costs, so 214, 467 or 696 is left; the drivers' wages are the
    /// game's own. A site that is run well climbs to five stars and takes a fifth more; a same-day promise adds a
    /// quarter and makes lateness likely unless picking and vans keep up.
    ///
    /// THE BOUND: a site never takes more than <see cref="OrdersMost"/> orders in a day (20, 40, 60: two and a half
    /// times its ordinary day), whatever the price, the rating, the advertising, the economy and the season do
    /// together. With all of them at their best the check measures 19, 39 and 59 orders a day, worth 1,075, 2,162 and
    /// 3,255 at the going price: 17%, 36% and 54% of the shop's counter, and no more. What enforces it, in the order
    /// it bites:
    ///   demand     the rule itself stops at <see cref="OrdersMost"/> (OnlineRules.PerDay and OnlineRules.Arrive)
    ///   stock      every unit sold leaves the real shelves or pallets; an order with nothing to pick is cancelled
    ///              (a place with forty units sells forty)
    ///   pickers    one member of staff picks <see cref="PickPerHour"/> orders in an hour of their shift, so 60 orders
    ///              are ten staff hours; with nobody on shift orders wait, and are given up a day after their promise
    ///   drivers    a driver is paid by the game for 5.7 hours a day (<see cref="DriverMinutes"/>); a round of eight
    ///              parcels to a district 4.4 km off is about 75 minutes, so one van delivers about 30 parcels a day
    ///   the city   each further site of the same player shares the city's orders (<see cref="CrowdStep"/>): four
    ///              sites take 2.3 times what one does, and the twelve a player may have 3.2 times
    /// A dear site sells less (the square of the price ratio) and is rated lower; a cheap one sells more but each sale
    /// leaves less, and with goods at 45% of the going price the best price is a little under the going one.
    /// </summary>
    public static class OnlineCatalogue
    {
        // ---------------------------------------------------------------- the site

        public const int Qualities = 3;                                         // Basic, Standard, Professional
        public static readonly int[] SetUp = { 2500, 6000, 12000 };             // the one-off fee, at the start's prices
        public static readonly int[] Hosting = { 150, 300, 600 };               // a month, at the start's prices
        public static readonly int[] OrdersADay = { 8, 16, 24 };                // at the reference conditions in the summary above
        public static readonly int[] OrdersMost = { 20, 40, 60 };               // the bound: never more in one day
        public const int AgencyDiscount = 400;      // thousandths off the set-up for a player who runs a web development agency: their own people build it
        public const int AgencyOrders = 1100;       // thousandths of the orders with a site built in-house (it is kept up to date)
        public const int ShopsMost = 12;            // online shops a player can have
        public const int LinesMost = 12;            // products in a site's catalogue
        public const int NameMost = 28;             // characters of a site's name
        public const int OpenMost = 80;             // orders a site holds at once; with more it takes none
        public const int ReturnsMost = 40;          // returns on their way back, kept
        public const int VansMost = 24;             // vans the rounds count

        // ---------------------------------------------------------------- demand (factors in thousandths)

        public const int BreadthLeast = 400;        // a catalogue of one product
        public const int BreadthLines = 8;          // ...and from this many it is at its full 1000
        public const int NeedMiddle = 60;           // the game's own product demand (0 to 100) at which the factor is 1000
        public const int NeedLeast = 500, NeedMost = 1500;
        public const int PriceFloor = 700;          // an online price under this share of the going price sells no more than at it
        public const int PriceCeiling = 1600;       // ...and at or over this nobody orders
        public const int PriceFactorMost = 2000;
        public static readonly int[] PromiseFactor = { 1250, 1000 };            // same day, next day
        public const int StarsLeast = 1000, StarsMost = 5000, StarsStart = 4000;   // thousandths of a star
        public const int StarsFloorFactor = 200;    // the rating's factor is this plus a fifth of the stars: 400 at one star, 1000 at four, 1200 at five
        public static readonly int[] CycleFactor = { 1060, 1000, 950, 880 };    // boom, steady, slowdown, recession
        public static readonly int[] SeasonFactor = { 1100, 1000, 920, 1040 };  // winter, spring, summer, autumn: people order in when it is cold
        public const int CampaignShare = 500;       // thousandths of a shop campaign's customers that also find the site
        public const int CrowdStep = 250;           // thousandths: each further site of the player's divides every site's orders by this much more

        // The share of a day's orders that comes in each hour, thousandths (they add up to 1000): little at night, a
        // rise at lunch, most in the evening.
        public static readonly int[] HourShare = { 8, 4, 2, 2, 2, 4, 12, 26, 40, 50, 54, 58, 66, 58, 50, 46, 48, 58, 74, 86, 90, 74, 54, 34 };

        // ---------------------------------------------------------------- an order

        public const int OrderLines = 3;            // products in one order at most
        public const int OneLine = 55, TwoLines = 85;   // per cent of orders with one product, and with one or two
        public const int CheapUnder = 1000;         // cents: under this a customer takes up to three of a product
        public const int DearFrom = 6000;           // cents: from this, one
        public const int SameDayCutoff = 15;        // the hour from which a same-day order is promised for the next morning
        public const int SameDayHours = 6;
        public const int SameDayLatest = 21;        // no same-day promise is for later than this hour
        public const int NextMorning = 12;          // ...and an order after the cut-off is promised for this hour of the next day
        public const int NextDayHour = 20;          // a next-day order is promised for this hour of the next day

        // ---------------------------------------------------------------- picking

        public const int PickPerHour = 6;           // orders one member of staff picks in an hour of their shift (ten minutes an order)
        public const int WaitMost = 24;             // hours past its promise after which an order nobody picked is given up by the customer
        public const int IdleAlert = 3;             // hours of orders waiting with nobody to pick before the Planner says so
        public const int WarehouseFrom = 7, WarehouseUntil = 19;   // the hours a warehouse's drivers pick in (the game keeps no shifts for them)

        // ---------------------------------------------------------------- delivery

        public const int Ways = 2;
        public const int OwnVans = 0, Courier = 1;
        public const int DriveFrom = 8, DriveUntil = 20;    // the hours a round may leave in
        public const int DriverMinutes = 342;       // a driver's day: the 5.7 hours the game pays a delivery driver for (EmployeeInstance.PayWage)
        public const int LoadMinutes = 10;          // loading a round
        public const int StopMinutes = 5;           // each door
        public const int VanMetresPerHour = 22000;  // through the city's traffic
        public const int VanCostPerKm = 60;         // cents of fuel and wear, there and back
        public const int VanLeast = 2, VanMost = 40;    // parcels a van takes, from the game's cargo capacity
        public const int CourierBase = 350;         // cents a parcel
        public const int CourierPerKm = 100;        // cents a kilometre of road
        public const int CourierHours = 2;          // collected within the hour, at the door this much later...
        public const int CourierMetresPerHour = 8000;   // ...and an hour more for each of these (it shares its van with other firms' parcels)
        public const int MissingHours = 24;         // a picked order no van took by this long after its promise is refunded in full
        public const int LateRefund = 250;          // thousandths of an order's value paid back when it comes late
        public const int RoadFactor = 1350;         // thousandths: road for a straight line, until the roads have been measured

        // ---------------------------------------------------------------- the rating

        public const int ScoreGood = 5000, ScoreShort = 3000, ScoreLate = 2000, ScoreLost = 1000;   // on time and whole, short of something, late, cancelled or never delivered
        public const int RatingWeight = 20;         // one order moves the rating a twentieth of the way to its score: 0.2 of a star at the very most
        public const int DearAbove = 1100, DearCost = 500;      // an order dearer than this against the going price scores this much less
        public const int CheapBelow = 950, CheapGain = 250;     // ...and one cheaper than this, this much more
        public const int FallingBy = 300;           // thousandths of a star lost in a week that the Planner calls falling
        public const int PoorStars = 3000;          // ...and under this it says so whatever the week did

        // ---------------------------------------------------------------- returns (by the kind of goods, MacroCatalogue's six)

        public static readonly int[] ReturnShare = { 10, 20, 180, 90, 60, 50 };    // thousandths of delivered orders: fresh, groceries, clothing, electronics, home, general
        public static readonly int[] ResaleShare = { 0, 0, 800, 600, 700, 700 };   // thousandths of returns fit to go back on the shelf; the rest is written off
        public const int ReturnDays = 2;            // days after delivery that a return comes back
        public const int ReturnHour = 10;           // the hour of the day returns are dealt with

        // ---------------------------------------------------------------- advertising online

        public const int AdCost = 400;              // at the start's prices
        public const int AdDays = 5;
        public const int AdPercent = 20;            // per cent more orders, before the economy and repeats
        public const int AdMost = 30;

        public static int Quality(int quality)
        {
            return Clamp(quality, 0, Qualities - 1);
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>a x b / 1000 without running over.</summary>
        public static int Share(long amount, int thousandths)
        {
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, amount * thousandths / 1000));
        }
    }
}
