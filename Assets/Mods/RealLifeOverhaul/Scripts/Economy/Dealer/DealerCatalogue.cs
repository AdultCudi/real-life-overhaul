namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// Every number that balances the car dealership, game-free so the offline checks read the same table. Money is in
    /// whole units; a car's worth follows the game's list price at the city's price level (<see cref="Prices"/>), and the
    /// sums marked "at the start's prices" are asked for at today's. Shares and chances are in thousandths.
    /// The offline check "dealer balance" holds a month of a mid-sized showroom to 3,000 to 7,000 of margin a day before
    /// wages and rent: visitors stop at the game's own customer count, a salesperson serves <see cref="ServePerStaff"/> an
    /// hour, a lot holds <see cref="SpotsMost"/> cars and a dear car hardly draws.
    /// </summary>
    public static class DealerCatalogue
    {
        // The business and its lot
        public const string TypeName = "reallife_businesstype_cardealership";   // its own business type (the name is also its text key)
        public const string BuildingType = "ba:buildingtype_retail";
        public const int YardsMost = 8;             // dealerships a player keeps the figures of
        public const int SpotsLeast = 2;            // a lot always has this many places, however small the floor
        public const int SpotsMost = 12;            // ...and never more than this
        public const int SpotWidth = 340;           // centimetres across a place, side by side
        public const int SpotDepth = 620;           // centimetres along a place, nose to tail
        public const int WallGap = 200;             // centimetres kept clear along every wall
        public const int CarsMost = SpotsMost;      // cars a dealership holds: one on each place

        // What a car is worth: by kilometres (thousandths of its worth new) and by condition (thousandths of that), along
        // straight lines between the points, the last point holding beyond.
        public static readonly int[] KmAt = { 0, 5000, 20000, 50000, 90000, 140000, 200000, 300000 };
        public static readonly int[] KmWorth = { 1000, 900, 830, 720, 600, 480, 380, 300 };
        public static readonly int[] ConditionAt = { 400, 500, 600, 700, 800, 900, 1000 };
        public static readonly int[] ConditionWorth = { 540, 620, 710, 800, 880, 950, 1000 };
        public const int WorthLeast = 200;          // a car is never worth less than this share of its price new
        public const int MarkUp = 1050;             // the asking price a car is put on the lot at, of its worth
        public const int AskLeast = 800, AskMost = 1400, AskStep = 20;   // the asking price the player may set, of its worth
        public const int Round = 50;                // prices are named in fifties
        public const int TradeShare = 780;          // selling a car to the trade (another dealer, at once): of its worth

        // The wholesaler's new cars
        public const int WholesaleADay = 6;         // new cars listed each morning
        public const int WholesaleLeast = 895, WholesaleMost = 935;   // their price, of their worth new
        public const int WholesaleKmMost = 60;      // delivery kilometres on a new car

        // Used cars offered by their owners
        public const int UsedADay = 5;              // offers each morning
        public static readonly int[] UsedKmBand = { 8000, 35000, 70000, 120000, 190000 };   // the bands an offer's kilometres are drawn from...
        public static readonly int[] UsedKmShare = { 180, 320, 280, 160, 60 };              // ...and how often each band comes (thousandths)
        public const int UsedConditionSpread = 90;  // the condition drawn either side of what its kilometres would make it
        public const int UsedConditionLeast = 420, UsedConditionMost = 980;   // thousandths
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

        // The trade auction
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

        // Customers
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

        // Three kinds of buyer: how often each comes, their first offer and how far above it they go (of the asking price).
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

        public const int TestDriveChance = 300;     // buyers who ask for a test drive first
        public const int TestDriveHours = 1;        // hours the car is out
        public const int TestDriveKmLeast = 5, TestDriveKmMost = 30;   // kilometres a drive puts on
        public const int TestDriveWearChance = 100; // a drive that leaves a mark...
        public const int TestDriveWear = 20;        // ...of this much condition
        public const int TestDriveLift = 80;        // added to the chance that a counter is taken

        public const int TradeInChance = 250;       // buyers who bring their own car in part exchange
        public const int TradeInMost = 700;         // a part exchange is worth at most this share of the car's asking price
        public const int AllowanceLeast = 820, AllowanceMost = 900;   // what the dealership allows for it, of its worth

        // The dealership's own finance
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
        public const int PlansMost = 200;           // finance plans kept

        // Warranties
        public const int Tiers = 3;                 // basic, extended, complete
        public static readonly int[] TierDays = { 30, 60, 120 };     // days each covers
        public static readonly int[] TierPremium = { 35, 60, 95 };   // of the car's price
        public const int PremiumLeast = 150;        // at the start's prices
        public static readonly int[] TierTakeUp = { 300, 200, 120 }; // buyers offered the tier who take it
        public static readonly int[] ClaimChance = { 5, 6, 7 };      // a day, for a car in condition 1000 (doubled at 500)
        public const int ClaimLeast = 8, ClaimMost = 30;             // what a claim costs, of the car's worth...
        public const int ClaimFloor = 150, ClaimCeiling = 5000;      // ...within these at the start's prices
        public const int ClaimRest = 10;            // days after a claim before another
        public const int ClaimsMost = 3;            // claims one warranty pays
        public const int CoversMost = 200;          // warranties kept

        // Preparing a car
        public const int PrepareBase = 120;         // at the start's prices: a valet and a check
        public const int PrepareShare = 300;        // and this share of the worth the missing condition costs it
        public const int PrepareRestore = 650;      // of the condition a car is missing, put back...
        public const int PrepareLeast = 50;         // ...and at least this much

        // How condition draws buyers (thousandths): in full from 950, so a new car and one the workshop has been over are alike.
        public static readonly int[] ConditionDrawAt = { 400, 600, 800, 950 };
        public static readonly int[] ConditionDraw = { 650, 800, 920, 1000 };

        // Advertising events, one at a time at a showroom and paid when booked. Their promotion points count only in the
        // showroom's own visitors and never past the game's top, so a campaign that has it high leaves little to add.
        public const int EventKinds = 4;            // an open weekend, test drive days, a part exchange week, a featured kind of car
        public const int OpenWeekend = 0, TestDriveDays = 1, PartExchangeWeek = 2, FeaturedKind = 3;
        public static readonly int[] EventCost = { 2400, 1600, 1800, 2000 };   // at the start's prices, paid when booked
        public static readonly int[] EventDays = { 3, 3, 5, 4 };               // days it runs, the day it is booked the first
        public static readonly int[] EventPromotion = { 15, 6, 5, 4 };         // points on the building's promotion for the showroom's visitors
        public static readonly int[] EventOffer = { 1080, 1000, 1060, 1000 };  // the chance a visitor looked after makes an offer, thousandths of itself
        public static readonly int[] EventAccept = { 0, 120, 0, 0 };           // added to the chance a counter is taken, on an offer made while it runs
        public static readonly int[] EventDrive = { 0, 250, 0, 0 };            // added to the buyers who ask for a test drive
        public static readonly int[] EventTradeIn = { 0, 0, 200, 0 };          // added to the buyers who bring a car in part exchange
        public static readonly int[] EventAllowance = { 0, 0, 40, 0 };         // added to what is allowed for it, of its worth
        public const int FeaturedDraw = 1500;       // a car of the featured kind draws this much of what it would (thousandths)...
        public const int FeaturedOffer = 1150;      // ...and its offer chance is this much of itself
        public const int EventRest = 5;             // days after an event ends before the same kind may be booked again at that showroom
        public const int PromotionMost = 100;       // the game's own top of a promotion

        // Reputation, 0 to RepMost: won and lost by sales and missions, it opens perks step by step. It never touches the
        // visitors, which are the promotion's.
        public const int RepMost = 1000;
        public static readonly int[] RepSteps = { 0, 100, 250, 450, 700 };   // where each step starts: new, known locally, trusted, respected, renowned
        public const int RepSale = 8;               // a car sold to a customer
        public const int RepGoodCar = 2;            // ...and this more for one in condition of at least RepGoodAt
        public const int RepGoodAt = 900;           // condition
        public const int RepFault = 6;              // taken for each fault the buyer finds that the showroom did not put right...
        public const int RepFaultsMost = 18;        // ...at most this for one car
        public const int RepWalked = 2;             // a buyer who walks away after the counters
        public const int RepLapsed = 1;             // an offer left to lapse with no answer
        public const int RepClaim = 2;              // a warranty claim paid: a promise kept
        public const int RepTakenBack = 4;          // a car on the showroom's finance taken back
        // The perks, each from its step on.
        public const int PerkOfferStep = 1, PerkOffer = 10;          // buyers' first offers higher by this share of the asking price
        public const int PerkExtrasStep = 2, PerkExtras = 40;        // added to the take-up of each extra and warranty
        public const int PerkWholesaleStep = 3, PerkWholesale = 10;  // the wholesaler's price lower by this share
        public const int PerkConvertStep = 4, PerkConvert = 1080;    // the offer chance, thousandths of itself

        // Missions: the tally each counts, the count it needs and the reputation it gives, each done once per showroom.
        public const int Tallies = 10;
        public const int TallySold = 0, TallyMargin = 1, TallyTradeIn = 2, TallyFinance = 3, TallyExtras = 4, TallyWorkshop = 5, TallyEvents = 6, TallyDelivered = 7, TallyQuick = 8, TallyLot = 9;
        public const int QuickDays = 3;             // a car sold within this many days of coming onto the lot is a quick sale
        public const int GoodMargin = 100;          // a sale whose margin is at least this share of its price counts as a good one
        public static readonly int[] MissionTally =
        {
            TallySold, TallyLot, TallyEvents, TallyTradeIn, TallyFinance, TallyExtras, TallyWorkshop, TallyDelivered, TallyQuick, TallySold, TallyMargin, TallySold
        };
        public static readonly int[] MissionTarget = { 1, 6, 1, 2, 3, 5, 3, 2, 3, 10, 5, 50 };
        public static readonly int[] MissionReward = { 20, 20, 15, 25, 25, 30, 25, 25, 30, 40, 40, 80 };
        public static int Missions => MissionTally.Length;

        // Extras a buyer may take with a car, beyond the warranty
        public const int ExtraKinds = 4;            // paint and interior protection, a service plan, an accessories pack, tyre and wheel cover
        public static readonly int[] ExtraPrice = { 15, 25, 10, 20 };         // of the car's price
        public static readonly int[] ExtraLeast = { 150, 250, 100, 200 };     // at the start's prices
        public static readonly int[] ExtraCost = { 350, 550, 500, 700 };      // what the showroom pays for it, of what it sells it for
        public static readonly int[] ExtraTakeUp = { 220, 160, 250, 120 };    // buyers offered it who take it
        public const int ExtraKeen = 50;            // a keen buyer takes each this much more often

        // The workshop: a day or more off show, then every fault put right (hidden ones too) and most missing condition back.
        public const int WorkshopBays = 2;          // cars one showroom's workshop works on at once
        public const int WorkshopBase = 250;        // at the start's prices: the bay and a day's labour
        public const int WorkshopShare = 350;       // and this share of what the missing condition takes off its worth new...
        public const int WorkshopRestore = 900;     // ...for this share of the condition still missing once the faults are put right
        public const int WorkshopLeast = 100;       // ...and at least this much of it
        public const int WorkshopHoursLeast = 24, WorkshopHoursMost = 72;   // how long the work takes, in hours
        public const int WorkshopPerHour = 10;      // condition missing that adds an hour to the work

        // Deliveries of lots won at the trade auction
        public const int DeliveryDaysLeast = 2, DeliveryDaysMost = 3;   // days after it is won that a lot arrives...
        public const int DeliveryHour = 8;          // ...at this hour
        public const int DeliveriesMost = 16;       // cars on their way at once

        // What is kept
        public const int WeekBooks = 7;             // days of figures a week's sums are of
        public const int ReportDays = 60;           // days of figures kept for each showroom, for its reports
        public const int AverageDays = 7;           // the line over a report's bars: the average of this many days
        public const int SalesKept = 40;            // recent sales kept for the app
        public const int NameMost = 32;             // characters of a name kept

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
