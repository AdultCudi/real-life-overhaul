using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every number that balances the online shops, game-free: the offline checks read the same table. Money is in
    /// cents, except the sums paid at once (set-up, hosting, advertising): whole units at the start's prices, raised
    /// to today's by <see cref="CompanyCatalogue.AtPrices"/>. The yardstick is a mid-game shop that takes about 6,000 a
    /// day over its counter: a Basic, Standard or Professional site adds about 7%, 16% or 24% to what it keeps
    /// (EconomyCheck, "online balance"), and at its best never more than <see cref="OrdersMost"/> orders a day, worth
    /// about 17%, 36% or 54% of the counter. The real stock, the staff who pick, the drivers' day and the player's other
    /// sites (<see cref="CrowdStep"/>) bound it further.
    /// </summary>
    public static class OnlineCatalogue
    {
        // The site.
        public const int Qualities = 3;                                         // Basic, Standard, Professional
        public static readonly int[] SetUp = { 2500, 6000, 12000 };             // one-off, at the start's prices
        public static readonly int[] Hosting = { 150, 300, 600 };               // a month, at the start's prices
        public static readonly int[] OrdersADay = { 8, 16, 24 };                // on an ordinary day
        public static readonly int[] OrdersMost = { 20, 40, 60 };               // the bound: never more in one day
        public const int AgencyDiscount = 400;      // thousandths off the set-up when the player's own agency builds it
        public const int AgencyOrders = 1100;       // thousandths of the orders with a site built in-house (kept up to date)
        public const int ShopsMost = 12;
        public const int LinesMost = 12;            // products in a catalogue
        public const int NameMost = 28;             // characters
        public const int OpenMost = 80;             // open orders a site holds; with more it takes none
        public const int ReturnsMost = 40;
        public const int VansMost = 24;

        // Demand: factors in thousandths.
        public const int BreadthLeast = 400;        // a catalogue of one product
        public const int BreadthLines = 8;          // products from which breadth is 1000
        public const int NeedMiddle = 60;           // the game's own product demand (0 to 100) at which the factor is 1000
        public const int NeedLeast = 500, NeedMost = 1500;
        public const int PriceFloor = 700;          // price to the going price under which a price sells no more
        public const int PriceCeiling = 1600;       // ...and at or over which nobody orders
        public const int PriceFactorMost = 2000;
        public static readonly int[] PromiseFactor = { 1250, 1000 };            // same day, next day
        public const int StarsLeast = 1000, StarsMost = 5000, StarsStart = 4000;   // thousandths of a star
        public const int StarsFloorFactor = 200;    // plus a fifth of the stars: 400 at one star, 1000 at four, 1200 at five
        public static readonly int[] CycleFactor = { 1060, 1000, 950, 880 };    // boom, steady, slowdown, recession
        public static readonly int[] SeasonFactor = { 1100, 1000, 920, 1040 };  // winter, spring, summer, autumn: people order in when it is cold
        public const int CampaignShare = 500;       // thousandths of a shop campaign's customers that also find the site
        public const int CrowdStep = 250;           // thousandths each further site of the player's adds to the divisor of every site's orders

        // Thousandths of a day's orders that come in each hour, adding up to 1000: little at night, most in the evening.
        public static readonly int[] HourShare = { 8, 4, 2, 2, 2, 4, 12, 26, 40, 50, 54, 58, 66, 58, 50, 46, 48, 58, 74, 86, 90, 74, 54, 34 };

        // An order.
        public const int OrderLines = 3;            // products in one order at most
        public const int OneLine = 55, TwoLines = 85;   // per cent of orders with one product, and with one or two
        public const int CheapUnder = 1000;         // cents: under this a customer takes up to three of a product
        public const int DearFrom = 6000;           // cents: from this, one
        public const int SameDayCutoff = 15;        // hour of the day from which a same-day order is promised for the next morning
        public const int SameDayHours = 6;
        public const int SameDayLatest = 21;        // hour of the day: no same-day promise is later
        public const int NextMorning = 12;          // hour of the next day for an order after the cut-off
        public const int NextDayHour = 20;          // hour of the next day for a next-day order

        // Picking.
        public const int PickPerHour = 6;           // orders one member of staff picks in an hour of their shift
        public const int WaitMost = 24;             // hours past its promise after which an unpicked order is given up
        public const int IdleAlert = 3;             // hours of orders waiting with nobody to pick before the Planner says so
        public const int WarehouseFrom = 7, WarehouseUntil = 19;   // hours a warehouse's drivers pick in: the game keeps no shifts for them

        // Delivery.
        public const int Ways = 2;
        public const int OwnVans = 0, Courier = 1;
        public const int DriveFrom = 8, DriveUntil = 20;    // hours a round may leave in
        public const int DriverMinutes = 342;       // the 5.7 hours a day the game pays a delivery driver for (EmployeeInstance.PayWage)
        public const int LoadMinutes = 10;          // loading a round
        public const int StopMinutes = 5;           // each door
        public const int VanMetresPerHour = 22000;  // through the city's traffic
        public const int VanCostPerKm = 60;         // cents of fuel and wear a kilometre driven
        public const int VanLeast = 2, VanMost = 40;    // parcels a van takes, from the game's cargo capacity
        public const int CourierBase = 350;         // cents a parcel
        public const int CourierPerKm = 100;        // cents a kilometre of road
        public const int CourierHours = 2;          // from collection to the door...
        public const int CourierMetresPerHour = 8000;   // ...and an hour more for each of these: it shares its van with other firms
        public const int MissingHours = 24;         // past its promise, after which a packed order no van took is refunded in full
        public const int LateRefund = 250;          // thousandths of a late order's value paid back
        public const int RoadFactor = 1350;         // thousandths: road for a straight line, until the roads are measured

        // The rating, in thousandths of a star.
        public const int ScoreGood = 5000, ScoreShort = 3000, ScoreLate = 2000, ScoreLost = 1000;   // whole and on time, short, late, cancelled or lost
        public const int RatingWeight = 20;         // one order moves the rating a twentieth of the way to its score
        public const int DearAbove = 1100, DearCost = 500;      // an order dearer than this against the going price scores this much less
        public const int CheapBelow = 950, CheapGain = 250;     // ...and one cheaper than this, this much more
        public const int FallingBy = 300;           // lost in a week, that the Planner calls falling
        public const int PoorStars = 3000;          // ...and under which it says so whatever the week did

        // Returns, by MacroCatalogue's kind of goods: fresh, groceries, clothing, electronics, home, general.
        public static readonly int[] ReturnShare = { 10, 20, 180, 90, 60, 50 };    // thousandths of delivered orders
        public static readonly int[] ResaleShare = { 0, 0, 800, 600, 700, 700 };   // thousandths of returns fit to sell again; the rest is written off
        public const int ReturnDays = 2;            // days after delivery
        public const int ReturnHour = 10;           // hour of the day returns are dealt with

        // Advertising online.
        public const int AdCost = 400;              // at the start's prices
        public const int AdDays = 5;
        public const int AdPercent = 20;            // per cent more orders, before the economy and repeats
        public const int AdMost = 30;               // per cent

        public static int Quality(int quality)
        {
            return Clamp(quality, 0, Qualities - 1);
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>amount x thousandths / 1000, kept inside an int.</summary>
        public static int Share(long amount, int thousandths)
        {
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, amount * thousandths / 1000));
        }
    }
}
