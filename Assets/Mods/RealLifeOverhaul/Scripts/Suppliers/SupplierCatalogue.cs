using System;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>How a supplier treats a customer who cannot pay on the day. Saved as a number: never renumber.</summary>
    public enum LatePolicy
    {
        Lenient = 0,   // delivers on account, a week to pay, no fee
        Firm = 1,      // delivers on account, four days to pay, a late fee
        Strict = 2     // no goods without payment, two days to pay what is owed
    }

    /// <summary>One supplier as the catalogue writes it: its character, held in data. Game-free.</summary>
    public sealed class SupplierDef
    {
        public readonly int Id;             // 1 to Firms.Length; 0 is the city's own wholesale stores (the game's)
        public readonly string NameKey;
        public readonly int Kinds;          // the kinds of goods it trades, a bit each (MacroCatalogue: fresh 0, groceries 1, clothing 2, electronics 3, home 4, general 5)
        public readonly int Level;          // thousandths of the market price it sells at on the day
        public readonly int Reliability;    // thousandths of its deliveries that come whole and on the day in calm conditions
        public readonly int LeadDays;       // days from an order to its first delivery
        public readonly int MinOrder;       // whole money a delivery must be worth at least, at the start's prices
        public readonly LatePolicy Late;
        public readonly int Owner;          // a rival tycoon's index (their holding owns it), or -1 for a firm of its own
        public readonly int HealthStart;    // thousandths: where its health rests

        public SupplierDef(int id, string nameKey, int kinds, int level, int reliability, int leadDays, int minOrder, LatePolicy late, int owner, int healthStart)
        {
            Id = id;
            NameKey = nameKey;
            Kinds = kinds;
            Level = level;
            Reliability = reliability;
            LeadDays = leadDays;
            MinOrder = minOrder;
            Late = late;
            Owner = owner;
            HealthStart = healthStart;
        }

        public bool Trades(int kind) => kind >= 0 && kind < SupplierCatalogue.Kinds && (Kinds & (1 << kind)) != 0;
    }

    /// <summary>
    /// Every number of suppliers and contracts, each with its unit and where it comes from. Money is whole money at the start's prices unless it says cents or mills
    /// (thousandths of a money unit, a unit price); a share is thousandths. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class SupplierCatalogue
    {
        public const int Kinds = 6;                 // the city's kinds of goods (MacroCatalogue.SupplyKinds): the goods the game's wholesalers and the city's economy really trade
        public const int Wholesalers = 0;           // the id of the city's own wholesale stores: the game's, the spot market, never a contract of the mod's

        private const int Fresh = 1, Groceries = 2, Clothing = 4, Electronics = 8, Home = 16, General = 32;

        /// <summary>
        /// The suppliers, three for each kind of goods: a dear and dependable one, a cheap one that asks a large order and forgives nothing, and one between. A level is against
        /// the market price of the day (the game's wholesale price); the game's own wholesale stores sell at it and add their delivery fee and their building's price index.
        /// </summary>
        public static readonly SupplierDef[] Firms =
        {
            new SupplierDef(1, "reallife_supply_firm_1", Fresh | Groceries, 1030, 975, 1, 300, LatePolicy.Lenient, -1, 800),
            new SupplierDef(2, "reallife_supply_firm_2", Fresh | Groceries | General, 950, 900, 3, 1500, LatePolicy.Strict, 0, 600),
            new SupplierDef(3, "reallife_supply_firm_3", Fresh | Groceries, 990, 945, 2, 700, LatePolicy.Firm, -1, 700),
            new SupplierDef(4, "reallife_supply_firm_4", Clothing, 945, 905, 3, 1800, LatePolicy.Strict, -1, 600),
            new SupplierDef(5, "reallife_supply_firm_5", Clothing | General, 1025, 970, 2, 500, LatePolicy.Lenient, 1, 800),
            new SupplierDef(6, "reallife_supply_firm_6", Clothing | Home, 985, 940, 2, 900, LatePolicy.Firm, -1, 700),
            new SupplierDef(7, "reallife_supply_firm_7", Electronics | Home, 950, 900, 4, 2500, LatePolicy.Strict, -1, 600),
            new SupplierDef(8, "reallife_supply_firm_8", Electronics, 1030, 975, 2, 800, LatePolicy.Lenient, 2, 800),
            new SupplierDef(9, "reallife_supply_firm_9", Electronics | Home | General, 990, 945, 2, 1000, LatePolicy.Firm, -1, 700)
        };

        public static readonly string[] KindKeys =
        {
            "reallife_supply_kind_0", "reallife_supply_kind_1", "reallife_supply_kind_2", "reallife_supply_kind_3", "reallife_supply_kind_4", "reallife_supply_kind_5"
        };

        // ---- a contract's terms
        public const int WeekDays = 7;              // days between two deliveries of a contract: the game's own week (DeliveryHelper delivers every Monday)
        public const int DeliveryHour = 8;          // the hour a delivery comes: the game's own (DeliveryHelper.DeliveryHour)
        /// <summary>The terms a fixed price is agreed for, in weekly deliveries; 0 in a contract is one delivery at the day's price, no fixed price.</summary>
        public static readonly int[] TermWeeks = { 2, 4, 8 };
        /// <summary>What a fixed price is under the supplier's price of the day in a calm market, thousandths, by term: the longer the promise, the more it is worth to them.</summary>
        public static readonly int[] TermDiscount = { 20, 35, 50 };
        /// <summary>The shares of a week's sales a contract's quantities may be set at, thousandths.</summary>
        public static readonly int[] Shares = { 500, 750, 1000, 1250, 1500 };
        public const int ShareDefault = 2;          // the step of Shares a page starts at: the week's sales
        public const int LinesMost = 12;            // products in one contract
        public const int ContractsMost = 24;        // contracts running at once
        public const int ContractsKept = 12;        // ended contracts the page keeps
        public const int VolumeFrom1 = 4, VolumeFrom2 = 10;   // a delivery worth this many minimum orders earns a volume step
        public const int VolumeOff1 = 10, VolumeOff2 = 20;    // thousandths off for it
        public const int SizeOff = 5;               // thousandths off the price for each step a supplier has grown
        public const int SizeReliability = 10;      // thousandths more of its deliveries on time for each step it has grown
        public const int SizeMost = 3;

        // ---- how a supplier judges a customer
        public const int CreditNone = 640;          // the rating read where the credit side has none (CreditCatalogue.CleanStart)
        public const int CreditGood = 740;          // from here (the credit side's "very good") a customer earns CreditOff
        public const int CreditPoor = 580;          // under here (the credit side's "poor") a customer pays CreditCost more, and a strict supplier offers nothing
        public const int CreditVeryPoor = 500;      // under here a firm supplier offers nothing either; a lenient one always offers
        public const int CreditOff = 10, CreditCost = 15;   // thousandths
        public const int LateWindow = 60;           // days a late payment is held against the customer
        public const int LateCost = 10;             // thousandths more for each late payment in the window
        public const int LateCostMost = 40;
        public const int SourDays = 90;             // days a supplier offers nothing after a customer defaulted on it

        // ---- take or pay
        public const int BreakShare = 250;          // thousandths of the value still to come that ending a contract early costs: what the supplier loses selling the goods on
        public static readonly int[] GraceDays = { 7, 4, 2 };      // days to pay what is owed, by late policy
        public static readonly int[] LateFee = { 0, 20, 50 };      // thousandths added to what is owed, once, by late policy
        public const int WithheldMost = 2;          // deliveries a strict supplier withholds for want of payment before it ends the contract
        public const int OwedMost = 10000;          // whole money, at the start's prices, that a customer may owe suppliers in all: past it a supplier brings nothing on account
        public const int OwedLinesMost = 64;        // lines of what is owed the save keeps: past it a new sum joins the oldest line of its contract

        // ---- late and short deliveries (thousandths of chance added to a supplier's own, by cause)
        public const int ChanceMost = 900;
        public const int HealthWeak = 400;          // under this health a supplier's cash trouble shows in its deliveries
        public const int HealthChanceDiv = 2;       // each point under it adds half a thousandth
        public const int TroubleChance = 300;       // while a strike or a fire at the depot runs
        public const int IceChance = 250, ColdChance = 80, StormChance = 80, HeatFreshChance = 120;
        public const int EventChance = 100;         // a parade, a protest, a fun run, a film shoot or fireworks on the day: streets closed
        public const int WinterFreshChance = 50;    // fresh goods in winter
        public const int ShortageDiv = 2;           // a market shortage of the kind adds half its size
        public const int LateDaysMost = 3;
        public const int ShortLeast = 400, ShortMost = 900;   // thousandths of a delivery a short one brings

        // ---- the remedy when the supplier itself fails (its own unreliability, its trouble, its cash): never for the weather, the streets or the whole market
        public const int ShortCredit = 100;         // thousandths of the value not brought, credited
        // (a late delivery's credit is the short credit on the days' share of the week it missed: SupplierRules.LateCredit)
        public const int FailuresToLeave = 3;       // failures of the supplier's own in one contract that give the right to leave with nothing to pay

        // ---- a supplier's own books
        public const int HealthMost = 1000;
        public const int Recover = 4;               // points a day back towards where its health rests
        public const int RecoverPerCustomers = 10;  // a point more for each so many shops it serves, up to RecoverMost more
        public const int RecoverMost = 4;
        public const int Regress = 2;               // points a day down towards its rest from above it
        public const int BookShare = 400;           // thousandths of its trade sold at fixed prices
        public const int BookRollDays = 28;         // days its book of fixed prices takes to follow the market
        public const int StrainDiv = 10;            // a market so many thousandths over its book takes a point of health a day for each ten on the fixed share
        public const int TroubleDrain = 15;         // points a day while its trouble runs
        public static readonly int[] CycleDrift = { 2, 0, -2, -4 };   // points a day by the city's cycle: boom, steady, slowdown, recession
        public const int TroubleDaysLeast = 3, TroubleDaysMost = 9;
        public const int TroubleDiv = 10;           // its chance of trouble a day is its unreliability over this, thousandths
        public const int ReopenDays = 28;           // days a supplier that went under stays shut before new owners open it
        public const int ReopenHealth = 500;
        public const int GrowHealth = 850;          // health at or over this ...
        public const int GrowDays = 30;             // ... for this many days running and it grows a step
        public const int FarMetres = 2500;          // a place this far from its depot, on the map, waits a day longer for a first delivery

        // ---- the rivals (the city's rival shops as the game values them, and the tycoons' outlets)
        public const int RivalContractShare = 600;  // thousandths of the rival shops that hold a fixed price contract with their supplier
        public const int RivalGoodsShare = 500;     // thousandths of a shop's day that is the cost of its goods (the game lets a rival sell down to its wholesale cost)
        public const int RivalLateLoss = 60;        // thousandths of a day's sales lost for each day its delivery is late
        public const int RivalShortLoss = 300;      // thousandths of a day's sales lost when a delivery brings nothing, less in proportion
        public const int RivalMost = 150;           // thousandths of a day the supply side moves a rival's day by at most, either way
        public const int OutletDayGoods = 300;      // whole money of goods a tycoon's outlet buys a day, at the start's prices
        public const int OwnerShare = 80;           // thousandths of what a supplier takes that reaches the tycoon whose holding owns it: its margin

        // ---- the paper and the log
        public const int PaperKept = 3;             // days a supplier's news stays a story
        public const int NotesMost = 16;
        public const int LogDays = 14;              // days of deliveries the log keeps (a manager's order counts them)
        public const int LogMost = 160;
        public const int RenewAheadDays = 3;        // days before a contract's last delivery that its renewal is looked at
        public const int RivalFromHour = 1;         // the game values the city's shops at the turn of the day: their books are read from this hour
        public const int CatchUpDays = 14;          // elapsed days a time skip plays one by one; the days before them are a pause (SupplierEngine.Resume)
        /// <summary>The most a renewal may cost over the day's market price before a manager leaves it to the player, thousandths, by the rule's step; the last is any price.</summary>
        public static readonly int[] RenewLimits = { 0, 50, 100, 200, 100000 };
        public const int RenewLimitDefault = 2;

        // ---- a contract's rules for its buyer, as a page names them when it moves one to its next value
        public const int RuleRenew = 0, RuleLimit = 1, RuleCover = 2, RuleLeave = 3;

        public static SupplierDef Firm(int id) => id >= 1 && id <= Firms.Length ? Firms[id - 1] : null;

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        public static long Clamp(long value, long least, long most) => value < least ? least : value > most ? most : value;

        /// <summary>The term's place in <see cref="TermWeeks"/>; -1 for one delivery at the day's price.</summary>
        public static int TermStep(int weeks)
        {
            return Array.IndexOf(TermWeeks, weeks);
        }
    }
}
