namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// Every number that balances the car dealership, in one place. Game-free: the offline checks read the same table.
    /// Money is in whole units. A car's worth starts from the game's own list price of its type (VehicleType.price) at
    /// the city's price level (<see cref="Prices"/>), so a dealership's figures move with the city's prices; the round
    /// sums the dealership pays (an inspection's least, a warranty's least) are written at the start's prices and asked
    /// for at today's. Shares and chances are in thousandths.
    ///
    /// WHAT A DEALERSHIP MAKES, AND WHAT BOUNDS IT
    /// The measure is a dealership in a mid-sized shop unit (the game's own count gives a shop there 25 customers an
    /// hour) with a fair promotion of 50, two salespeople of middling skill on shift from 9 to 19, a lot kept full from
    /// the wholesaler, staff left to close at 95% of the asking price and the rest countered once at the asking price.
    /// It sees <see cref="VisitorShare"/> of a shop's customers: about one an hour, nine or ten a day. A car is bought at
    /// about 91.5% of its worth new and asked at <see cref="MarkUp"/> of it; buyers offer 85 to 100% of the asking price
    /// and go a little above, so a sale leaves about 9% of a car's price as margin before wages and rent: some 4,000 on a
    /// 45,000 car. Used cars, auctions and part exchanges leave more and carry their risk: a car bought without an
    /// inspection can hide what costs money to put right.
    ///
    /// THE BOUND. The offline check ("dealer balance") runs a month of that dealership on five of the game's cars from
    /// 9,000 to 85,000 and measures about 1.4 cars a day and 5,900 a day of margin; it holds it within 3,000 to 7,000.
    /// Nothing a player does takes it far past that: visitors stop at what the game's own customer count allows (the
    /// promotion is the game's, at most 100), each salesperson serves <see cref="ServePerStaff"/> visitors an hour and no
    /// more, a lot holds <see cref="SpotsMost"/> cars, a dear car sells slowly (its draw falls to almost nothing at 35%
    /// over its worth), and a buyer's ceiling is a share of the asking price, so a high asking price finds no richer buyer.
    /// </summary>
    public static class DealerCatalogue
    {
        // ---------------------------------------------------------------- the business and its lot

        public const string TypeName = "reallife_businesstype_cardealership";   // its own business type (the name is also its text key)
        public const string BuildingType = "ba:buildingtype_retail";
        public const int YardsMost = 8;             // dealerships a player keeps the figures of
        public const int SpotsLeast = 2;            // a lot always has this many places, however small the floor
        public const int SpotsMost = 12;            // ...and never more than this
        public const int SpotWidth = 340;           // centimetres across a place, side by side
        public const int SpotDepth = 620;           // centimetres along a place, nose to tail
        public const int WallGap = 200;             // centimetres kept clear along every wall
        public const int CarsMost = SpotsMost;      // cars a dealership holds: one on each place

        // ---------------------------------------------------------------- what a car is worth

        // Its worth against its kilometres (km: thousandths of its worth new) and its condition (thousandths: thousandths of
        // that). Straight lines between the points; the last point holds beyond.
        public static readonly int[] KmAt = { 0, 5000, 20000, 50000, 90000, 140000, 200000, 300000 };
        public static readonly int[] KmWorth = { 1000, 900, 830, 720, 600, 480, 380, 300 };
        public static readonly int[] ConditionAt = { 400, 500, 600, 700, 800, 900, 1000 };
        public static readonly int[] ConditionWorth = { 540, 620, 710, 800, 880, 950, 1000 };
        public const int WorthLeast = 200;          // a car is never worth less than this share of its price new
        public const int MarkUp = 1050;             // the asking price a car is put on the lot at, of its worth
        public const int AskLeast = 800, AskMost = 1400, AskStep = 20;   // the asking price the player may set, of its worth
        public const int Round = 50;                // prices are named in fifties
        public const int TradeShare = 780;          // selling a car to the trade (another dealer, at once): of its worth

        // ---------------------------------------------------------------- the market: new cars from the wholesaler

        public const int WholesaleADay = 6;         // new cars listed each morning
        public const int WholesaleLeast = 895, WholesaleMost = 935;   // their price, of their worth new
        public const int WholesaleKmMost = 60;      // delivery kilometres on a new car

        // ---------------------------------------------------------------- used cars offered by their owners

        public const int UsedADay = 5;              // offers each morning
        public static readonly int[] UsedKmBand = { 8000, 35000, 70000, 120000, 190000 };   // the bands an offer's kilometres are drawn from...
        public static readonly int[] UsedKmShare = { 180, 320, 280, 160, 60 };              // ...and how often each band comes (thousandths)
        public const int UsedConditionSpread = 90;  // the condition drawn either side of what its kilometres would make it
        public const int UsedConditionLeast = 420, UsedConditionMost = 980;
        public const int OverstateMost = 120;       // condition an owner who knows of a fault says the car has beyond the truth
        public const int AskingLeast = 900, AskingMost = 1060;   // an owner's asking price, of what the car is really worth
        public const int FloorLeast = 900, FloorMost = 960;      // the least they take, of their asking price
        public const int InspectionShare = 10;      // an inspection costs this share of the car's worth...
        public const int InspectionLeast = 150, InspectionMost = 1200;   // ...within these, at the start's prices
        public const int InspectionLever = 30;      // each fault found takes this off the owner's least (of their asking)
        public const int HagglesMost = 3;           // offers the player may make an owner before they sell elsewhere
        public const int CounterShare = 250;        // an owner's counter: their least and this share of the gap above it
        public const int HaggleLeast = 750, HaggleStep = 25;     // what the player may offer, of the asking price

        // The faults a used car may hide: what each costs to put right (of the car's worth) and the condition it takes.
        public const int Faults = 6;                // tyres, brakes, suspension, bodywork, engine, interior
        public static readonly int[] FaultCost = { 6, 8, 15, 20, 40, 10 };
        public static readonly int[] FaultCondition = { 30, 30, 50, 60, 100, 30 };
        public static readonly int[] FaultChance = { 220, 160, 110, 120, 60, 140 };   // at 100,000 km and condition 700; more with more wear

        // ---------------------------------------------------------------- the trade auction

        public const int AuctionLots = 4;           // lots of a sale
        public const int AuctionFrom = 8, AuctionTo = 20;        // a sale opens only between these hours
        public static readonly int[] LotHours = { 3, 4, 5, 6 };  // the hours each lot of a sale runs
        public const int OpeningShare = 640;        // the opening bid, of the car's worth
        public const int IncrementShare = 20;       // a step of the bidding, of the car's worth...
        public const int IncrementLeast = 50, IncrementMost = 2500;   // ...within these
        public const int RivalCeilingLeast = 860, RivalCeilingMost = 1060;   // what the other dealers in the room go to, of the worth
        public const int RivalChanceLeading = 450;  // the chance another dealer bids in an hour while the player leads...
        public const int RivalChanceBehind = 250;   // ...and while another dealer does (they bid against each other)
        public const int BuyersFee = 50;            // the auction house's fee on a lot won, of the hammer price...
        public const int BuyersFeeLeast = 100;      // ...at least this at the start's prices
        public const int AuctionShownSpread = 60;   // the condition in the catalogue is the truth within this
        public const int AuctionInspection = 200;   // a look over a lot before bidding, at the start's prices
        public const int NoPayDays = 3;             // a lot won and not paid for: no bids for this many days
        public static readonly string[] RivalNames = { "Halden Motors", "Crossway Cars", "Pier Street Autos", "Northgate Trade", "Riverside Car Sales", "Brookfield Motor Group" };

        // ---------------------------------------------------------------- customers

        public const int VisitorShare = 35;         // a dealership's visitors an hour, of the customers a shop of its size would see
        public static readonly int[] HourWeight =   // when people come (thousandths of an average hour), midnight first
        {
            200, 100, 100, 100, 100, 200, 400, 600, 800, 1000, 1100, 1200, 1300, 1200, 1100, 1100, 1200, 1400, 1500, 1300, 1000, 700, 400, 300
        };
        public const int StockPullLeast = 400;      // a lot with one car draws this share of the visitors of a full one...
        public const int StockPullCar = 100;        // ...and each car more draws this much more, up to the whole
        public const int ServePerStaff = 2;         // visitors one salesperson looks after in an hour
        public const int OfferChance = 240;         // visitors looked after who make an offer, at a fair price and middling skill
        public const int SkillLeast = 800, SkillMost = 1200;   // the offer chance at a salesperson's skill of 0 and of 100
        public const int SatisfactionWeight = 300;  // how far the game's customer satisfaction of the building moves it (at 0 or 100)

        // How a price against a car's worth draws buyers: straight lines between the points (asking of worth, factor).
        public static readonly int[] PriceAt = { 900, 1000, 1120, 1200, 1350 };
        public static readonly int[] PriceDraw = { 1300, 1150, 1000, 600, 50 };

        // How long a car has stood on the lot (days) against how much it draws.
        public static readonly int[] AgeAt = { 0, 3, 7, 14, 30, 60 };
        public static readonly int[] AgeDraw = { 1150, 1100, 1000, 900, 750, 600 };

        // Three kinds of buyer: how often each comes, what they first offer and how far above it they will go (of the asking price).
        public const int Profiles = 3;              // a bargain hunter, an ordinary buyer, a keen one
        public static readonly int[] ProfileShare = { 350, 450, 200 };
        public static readonly int[] OfferLeast = { 850, 900, 950 };
        public static readonly int[] OfferMost = { 920, 960, 1000 };
        public static readonly int[] ReachLeast = { 30, 50, 50 };
        public static readonly int[] ReachMost = { 80, 100, 120 };
        public const int CeilingMost = 1080;        // nobody pays more than this of the asking price
        public const int SkillOffer = 20;           // a salesperson of skill 100 lifts a first offer by this much of the asking price
        public const int DealHours = 24;            // an offer stands this long
        public const int CountersMost = 2;          // counters the player may make before the buyer walks away
        public const int AcceptAtCeiling = 100;     // the chance a counter at the buyer's ceiling is taken; at their offer it is certain
        public const int StaffFloor = 950;          // staff close a deal on their own at this share of the asking price unless the player says otherwise
        public const int StaffFloorLeast = 850, StaffFloorMost = 1000, StaffFloorStep = 10;
        public const int TellEvery = 6;             // hours between two notices that more offers wait for the player
        public const int DealsMost = 16;            // offers a dealership holds at once

        // A test drive: who asks, how long it takes, what it does to the car and to the deal.
        public const int TestDriveChance = 300;
        public const int TestDriveHours = 1;
        public const int TestDriveKmLeast = 5, TestDriveKmMost = 30;
        public const int TestDriveWearChance = 100; // a drive that leaves a mark...
        public const int TestDriveWear = 20;        // ...of this much condition
        public const int TestDriveLift = 80;        // added to the chance that a counter is taken

        // A buyer's own car offered in part exchange.
        public const int TradeInChance = 250;
        public const int TradeInMost = 700;         // a part exchange is worth at most this share of the car's asking price
        public const int AllowanceLeast = 820, AllowanceMost = 900;   // what the dealership allows for it, of its worth

        // ---------------------------------------------------------------- the dealership's own finance

        public const int FinanceLeast = 5000;       // a car under this (at the start's prices) is paid for in cash
        public const int FinanceWant = 380;         // buyers of a dearer car who ask to pay over time
        public const int FinanceCash = 600;         // of those turned down for finance, the share who pay in cash all the same
        public const int ScoreLeast = 480, ScoreMost = 840;   // a buyer's credit rating, as the city's banks would score it
        public const int DepositLeast = 10, DepositMost = 30, DepositStep = 10;   // per cent of the price, paid at once
        public const int OwnRate = 3;               // the dealership lends at the banks' rate for the rating and this many points more
        public const int WeekDays = 7;              // an instalment is due every week
        public static readonly int[] MissChance = { 400, 220, 90, 45, 20, 8 };   // a week's instalment missed, by the buyer's credit grade
        public static readonly int[] CycleMisses = { 850, 1000, 1250, 1600 };   // the instalments missed in a boom, a steady economy, a slowdown, a recession (thousandths of the usual)
        public const int CatchUpChance = 500;       // a missed instalment is paid with the next one this often
        public const int MissesToDefault = 3;       // missed weeks running before the car is taken back
        public const int RecoverShare = 550;        // a car taken back is sold at the trade auction for this of its worth
        public const int PlansMost = 200;

        // ---------------------------------------------------------------- warranties

        public const int Tiers = 3;                 // basic, extended, complete
        public static readonly int[] TierDays = { 30, 60, 120 };
        public static readonly int[] TierPremium = { 35, 60, 95 };   // of the car's price
        public const int PremiumLeast = 150;        // at the start's prices
        public static readonly int[] TierTakeUp = { 300, 200, 120 }; // buyers offered the tier who take it
        public static readonly int[] ClaimChance = { 5, 6, 7 };      // a day, for a car in condition 1000 (doubled at 500)
        public const int ClaimLeast = 8, ClaimMost = 30;             // what a claim costs, of the car's worth...
        public const int ClaimFloor = 150, ClaimCeiling = 5000;      // ...within these at the start's prices
        public const int ClaimRest = 10;            // days after a claim before another
        public const int ClaimsMost = 3;            // claims one warranty pays
        public const int CoversMost = 200;

        // ---------------------------------------------------------------- preparing a car

        public const int PrepareBase = 120;         // at the start's prices: a valet and a check
        public const int PrepareShare = 300;        // and this share of the worth the missing condition costs it
        public const int PrepareRestore = 650;      // of the condition a car is missing, put back...
        public const int PrepareLeast = 50;         // ...and at least this much

        // ---------------------------------------------------------------- what is kept

        public const int WeekBooks = 7;             // days of figures kept for each dealership
        public const int SalesKept = 40;            // recent sales kept for the app
        public const int NameMost = 32;

        public static readonly string[] FirstNames =
        {
            "Amira", "Ben", "Carla", "Dev", "Elena", "Femi", "Grace", "Hugo", "Iris", "Jonah", "Keira", "Liam",
            "Maya", "Nico", "Olive", "Pavel", "Quinn", "Rosa", "Sami", "Tara", "Umar", "Vera", "Wes", "Yara"
        };

        public static readonly string[] LastNames =
        {
            "Abbott", "Barros", "Chandler", "Dimitrov", "Ellison", "Fontaine", "Garner", "Hollis", "Ibarra", "Jansen", "Kowalski", "Lambert",
            "Mendes", "Nakamura", "Osei", "Prescott", "Quintero", "Rourke", "Sandoval", "Thorne", "Underwood", "Vance", "Whitlock", "Yilmaz"
        };

        /// <summary>A share of an amount, in thousandths, rounded to the whole unit.</summary>
        public static int Share(int amount, int thousandths)
        {
            return (int)System.Math.Round(amount * (double)thousandths / 1000.0, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>A price named in fifties: never under one fifty for anything above nothing.</summary>
        public static int Named(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }
            int named = (int)System.Math.Round(amount / (double)Round, System.MidpointRounding.AwayFromZero) * Round;
            return named < Round ? Round : named;
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
