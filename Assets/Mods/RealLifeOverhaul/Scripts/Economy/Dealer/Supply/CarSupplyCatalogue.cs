namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// Every number of the car showroom's suppliers, game-free so the offline checks read the same table. A showroom buys its new cars
    /// by the kind of car (the six kinds it deals in, as the mod reads the game's vehicle types: CarKind 1 to 6) from one of three
    /// trade suppliers, each with its own price for each kind and its own delivery. The wholesaler is as it always was; the other
    /// two are cheaper for the kinds they bring in and dearer for the rest, and their cars come days later. Prices are thousandths
    /// of the wholesaler's listed price for the same car, kept within what the wholesaler's own listings span.
    /// </summary>
    public static class CarSupplyCatalogue
    {
        public const int Kinds = 6;                 // the kinds a showroom deals in: economy, saloon, luxury, sports, utility, van
        public const int KindFirst = 1;             // CarKind.Economy: a kind's place in the tables is its CarKind less this
        public const int Suppliers = 3;
        public const int Wholesaler = 0;            // the city's wholesaler: every kind at its listed price, on the lot at once
        public const int Importer = 1;              // a volume importer: everyday cars and vans for less, three days to come
        public const int Specialist = 2;            // a prestige trade house: luxury, sports and utility cars for less, two days to come

        public static readonly string[] NameKeys = { "reallife_carsup_name_0", "reallife_carsup_name_1", "reallife_carsup_name_2" };
        public static readonly string[] AboutKeys = { "reallife_carsup_about_0", "reallife_carsup_about_1", "reallife_carsup_about_2" };
        public static readonly string[] KindKeys =   // the showroom's own names for the kinds
        {
            "reallife_dealer_carkind_1", "reallife_dealer_carkind_2", "reallife_dealer_carkind_3", "reallife_dealer_carkind_4", "reallife_dealer_carkind_5", "reallife_dealer_carkind_6"
        };

        // What each supplier asks for a car of each kind, thousandths of the wholesaler's listed price: a row a supplier, a column a kind.
        public static readonly int[][] Price =
        {
            new[] { 1000, 1000, 1000, 1000, 1000, 1000 },
            new[] { 965, 972, 1012, 1015, 985, 968 },
            new[] { 1014, 990, 962, 960, 974, 1012 }
        };
        public const int PriceLeast = 960;          // no supplier is cheaper than this of the wholesaler's price: the width of the wholesaler's own listings...
        public const int PriceMost = 1015;          // ...and none dearer than this (DealerCatalogue.WholesaleLeast to WholesaleMost is 40 thousandths wide)

        // Days from the order to the car on the lot, a row a supplier: nothing, then the trade auction's own longest and shortest delivery.
        public static readonly int[] Days = { 0, DealerCatalogue.DeliveryDaysMost, DealerCatalogue.DeliveryDaysLeast };

        public const int KeepMost = DealerCatalogue.SpotsMost;   // new cars of one kind a manager keeps a showroom stocked with, at most: a full lot
        public const int RivalKeen = 1000;          // a rival dealer whose usual price is under this of a car's worth buys from the cheapest supplier for the kind...
        public const int RivalQuick = 1100;         // ...one at or over this wants the car on its lot at once; between, a draw of its own for each kind
        public const int RivalDraw = 500;           // the chance, in thousandths, that a dealer between the two takes the cheapest
    }
}
