namespace RealLifeOverhaul.Hustles
{
    /// <summary>What a thing on the Marketplace is: the four kinds of goods and a car (the game's own vehicle).</summary>
    public enum ThingKind
    {
        Electronics = 0,
        Furniture = 1,
        Clothes = 2,
        Shoes = 3,
        Car = 4
    }

    /// <summary>The freelance work clients ask for: each a visit to an address, Moving two.</summary>
    public enum GigKind
    {
        Detailing = 0,
        Handyman = 1,
        Junk = 2,
        Moving = 3
    }

    /// <summary>One kind of goods: its name, what it costs new at the start's prices, and what fixing and cleaning do for it.</summary>
    public sealed class ThingDef
    {
        public readonly string Id;
        public readonly string NameKey;
        public readonly ThingKind Kind;
        public readonly int NewPrice;      // at the start's prices
        public readonly int FixMinutes;    // game minutes a repair by hand takes; 0: it cannot be mended by hand
        public readonly int FixMost;       // the condition a repair brings it back to, at best (percent)
        public readonly int CleanGain;     // condition a clean adds, once; 0: nothing to clean

        public ThingDef(string id, string nameKey, ThingKind kind, int newPrice, int fixMinutes, int fixMost, int cleanGain)
        {
            Id = id;
            NameKey = nameKey;
            Kind = kind;
            NewPrice = newPrice;
            FixMinutes = fixMinutes;
            FixMost = fixMost;
            CleanGain = cleanGain;
        }

        /// <summary>Only electronics are taken in by a repair shop (the city's electronics stores).</summary>
        public bool ShopFix => Kind == ThingKind.Electronics;
    }

    /// <summary>One kind of freelance work: what it pays at the start's prices, how long it takes, what it needs and trains.</summary>
    public sealed class GigDef
    {
        public readonly GigKind Kind;
        public readonly string NameKey;
        public readonly int Pay;          // at the start's prices, before the client's mood and the player's rating
        public readonly int Minutes;      // game minutes of work at each address
        public readonly bool Van;         // the load goes in a van or a pickup
        public readonly bool TwoStops;    // loaded at one address, unloaded at another
        public readonly string Skill;     // the game's own skill it trains and draws on; null: the player's repair experience
        public readonly int Weight;       // how often clients ask for it

        public GigDef(GigKind kind, string nameKey, int pay, int minutes, bool van, bool twoStops, string skill, int weight)
        {
            Kind = kind;
            NameKey = nameKey;
            Pay = pay;
            Minutes = minutes;
            Van = van;
            TwoStops = twoStops;
            Skill = skill;
            Weight = weight;
        }
    }

    /// <summary>
    /// Every number of the Marketplace and freelance work; game-free so the offline checks read the same table. Money is
    /// in whole units at the start's prices (asked for at today's through the price level); shares and chances are in
    /// thousandths, condition in percent. A flip played well (bought under worth, fixed, cleaned, listed near worth) makes
    /// 20 to 60 % on the money put in over a day or two; a freelance visit pays 70 to 120 an hour, against 40 to 55 for a
    /// gig on foot and about 100 for a gig with a car (docs/features/jobs.md), because the player brings the van or the kit.
    /// </summary>
    public static class HustleCatalogue
    {
        public const string CleaningSkill = "ba:skill_cleaning";
        public const string DriverSkill = "ba:skill_deliverydriver";
        public const string NegotiationSkill = "ba:skill_negotiation";

        public static readonly ThingDef[] Things =
        {
            new ThingDef("phone", "reallife_hustle_thing_phone", ThingKind.Electronics, 650, 90, 92, 0),
            new ThingDef("laptop", "reallife_hustle_thing_laptop", ThingKind.Electronics, 1100, 120, 90, 0),
            new ThingDef("console", "reallife_hustle_thing_console", ThingKind.Electronics, 450, 90, 92, 0),
            new ThingDef("television", "reallife_hustle_thing_television", ThingKind.Electronics, 800, 120, 88, 0),
            new ThingDef("speaker", "reallife_hustle_thing_speaker", ThingKind.Electronics, 260, 60, 92, 0),
            new ThingDef("camera", "reallife_hustle_thing_camera", ThingKind.Electronics, 700, 120, 90, 0),
            new ThingDef("sofa", "reallife_hustle_thing_sofa", ThingKind.Furniture, 900, 150, 80, 14),
            new ThingDef("armchair", "reallife_hustle_thing_armchair", ThingKind.Furniture, 350, 90, 82, 14),
            new ThingDef("table", "reallife_hustle_thing_table", ThingKind.Furniture, 500, 120, 85, 10),
            new ThingDef("bookcase", "reallife_hustle_thing_bookcase", ThingKind.Furniture, 240, 60, 85, 8),
            new ThingDef("jacket", "reallife_hustle_thing_jacket", ThingKind.Clothes, 320, 45, 85, 18),
            new ThingDef("coat", "reallife_hustle_thing_coat", ThingKind.Clothes, 600, 60, 85, 18),
            new ThingDef("suit", "reallife_hustle_thing_suit", ThingKind.Clothes, 450, 60, 85, 16),
            new ThingDef("trainers", "reallife_hustle_thing_trainers", ThingKind.Shoes, 160, 30, 80, 20),
            new ThingDef("boots", "reallife_hustle_thing_boots", ThingKind.Shoes, 220, 45, 85, 16),
            new ThingDef("heels", "reallife_hustle_thing_heels", ThingKind.Shoes, 380, 45, 85, 16)
        };

        public static readonly GigDef[] Gigs =
        {
            // Two hours on a client's car with the player's own kit: 150, 75 an hour.
            new GigDef(GigKind.Detailing, "reallife_hustle_gig_detailing", 150, 120, false, false, CleaningSkill, 30),
            // An hour and a half of odd jobs in a client's home: 130, 87 an hour.
            new GigDef(GigKind.Handyman, "reallife_hustle_gig_handyman", 130, 90, false, false, null, 30),
            // Two hours clearing a flat into a van or a pickup: 220, 110 an hour, the tip fees included.
            new GigDef(GigKind.Junk, "reallife_hustle_gig_junk", 220, 120, true, false, DriverSkill, 25),
            // An hour and a half loading at one address and as long unloading at another: 360, 120 an hour.
            new GigDef(GigKind.Moving, "reallife_hustle_gig_moving", 360, 90, true, true, DriverSkill, 15)
        };

        // The day's listings
        public const int GoodsADay = 9;             // goods the city's people put up each morning
        public const int CarsADay = 2;              // and cars
        public const int ListingConditionLeast = 12, ListingConditionMost = 88;   // percent, goods
        public const int OverstateChance = 250;     // an electronics seller who says it is better than it is
        public const int OverstateLeast = 8, OverstateMost = 22;                  // ...by this much
        public const int AskingLeast = 820, AskingMost = 1300;   // a seller's asking price, of what the thing is really worth
        public const int FloorLeast = 760, FloorMost = 930;      // the least they take, of their asking price
        public const int HaggleLeast = 650, HaggleStep = 25;     // what the player may offer, of the asking price
        public const int HagglesMost = 3;           // offers before the seller sells to someone else
        public const int CounterShare = 300;        // a seller's counter: their least and this share of the gap above it
        public const int NegotiationLever = 60;     // at negotiation skill 100, a seller's least comes down by this (of the asking price)

        // What goods are worth used, of their price new, by condition (percent), along straight lines between the points.
        public static readonly int[] WorthAt = { 0, 25, 50, 75, 90, 100 };
        public static readonly int[] WorthShare = { 40, 160, 330, 500, 620, 700 };

        // Getting it home
        public const int DeliveryLeast = 25;        // a delivery home, at the start's prices...
        public const int DeliveryShare = 50;        // ...and this share of the price
        public const int DeliveryHour = 8;          // it comes the next morning from this hour

        // Fixing and cleaning
        public const int PartsShare = 60;           // parts for a repair by hand, of the price new
        public const int FixChanceBase = 450;       // a repair by hand works this often with no experience...
        public const int FixChancePerXp = 5;        // ...and this much more for each point of it (0 to 100)
        public const int FixChanceLeast = 150, FixChanceMost = 950;
        public const int FixWreckCut = 150;         // less for a thing in under WreckBelow percent
        public const int WreckBelow = 25;
        public const int FixMiss = 6;               // condition a failed repair costs, at most
        public const int XpWorked = 6, XpFailed = 3, XpMost = 100;
        public const int ShopShare = 300;           // a repair shop charges this share of the price new for a thing in nothing...
        public const int ShopLeast = 25;            // ...less as it is in better condition, never less than this
        public const int ShopCondition = 92;        // and gives it back in this condition
        public const int ShopMinutes = 60;          // while the player waits
        public const int CleanMinutes = 30;
        public const int CleanSupplies = 8;         // at the start's prices
        public const int CleanMost = 95;            // percent a clean never takes a thing past
        public const int EnergyAnHour = 6;          // energy a fix, a clean or an hour of a visit costs

        // Selling
        public const int AskLeastShare = 700, AskMostShare = 1500, AskStepShare = 50;   // the asking price the player may set, of the thing's worth
        public const int OfferChanceGoods = 70;     // an hour, a thing at its worth and in fair condition draws a buyer this often
        public const int OfferChanceCar = 45;
        public const int BuyersFrom = 8, BuyersUntil = 22;   // hours buyers write in
        public static readonly int[] PriceAt = { 600, 800, 1000, 1150, 1300, 1600 };   // the asking price, of the worth...
        public static readonly int[] PriceDraw = { 2600, 1800, 1000, 550, 250, 40 };  // ...and how it draws buyers
        public static readonly int[] ConditionAt = { 0, 30, 60, 85, 100 };
        public static readonly int[] ConditionDraw = { 450, 650, 900, 1100, 1250 };
        public const int BuyerOfferLeast = 720, BuyerOfferMost = 950;   // a buyer's first offer, of the asking price
        public const int CeilingLeast = 950, CeilingMost = 1180;        // the most they would pay, of the worth (never above the asking price)
        public const int OfferHours = 6;            // an offer stands this long
        public const int CountersMost = 2;          // counters before the buyer goes
        public const int AcceptAtCeiling = 100;     // the chance a counter at the buyer's ceiling is taken; at their offer it is certain
        public const int CounterStepsMost = 11;
        public const int CarDirtWorth = 100;        // a car as dirty as it gets is worth this much less (thousandths)

        // Freelance work
        public const int GigsADay = 3;              // requests each morning, one more for a rating of 4.5 or better, one fewer under 3
        public const int GigFromLeast = 9, GigFromMost = 16;   // the hour a visit is wanted from
        public const int GigWindow = 4;             // hours the client waits; arriving in the first hour is on time
        public const int GigEarly = 1;              // hours early the work may begin
        public const int GoodRating = 4500, PoorRating = 3000;   // thousandths of a star
        public const int GoodRatingPay = 1100;      // a client pays this for a good rating
        public const int ClientLeast = 900, ClientMost = 1150;   // the client's own figure on the pay
        public const int RatingsKept = 20;
        public const int HelperShare = 550;         // a helper takes this share of the pay
        public const int TipShare = 100;            // a five-star client tips this share
        public const int StoppedEarlyCut = 2000;    // thousandths of a star off for a visit left before the end
        public const int CancelRating = 2;          // stars a cancelled booking leaves
        public const int MissedRating = 1;          // ...and one nobody came to
        public const float GigSkill = 0.5f;         // skill points a visit trains
        public const int VanCargo = 14;             // the game's cargo room that makes a vehicle a van or a pickup
        public const float VanReach = 35f;          // metres from the door the van is to stand

        // What the save keeps
        public const int OwnedMost = 24;
        public const int SalesKept = 12;
        public const int OffersMost = 12;
        public const int GigsMost = 12;
        public const int NameMost = 40;

        public static ThingDef Thing(string id)
        {
            for (int i = 0; i < Things.Length; i++)
            {
                if (Things[i].Id == id)
                {
                    return Things[i];
                }
            }
            return null;
        }

        public static GigDef Gig(GigKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < Gigs.Length ? Gigs[i] : Gigs[0];
        }

        public static string KindKey(ThingKind kind)
        {
            switch (kind)
            {
                case ThingKind.Electronics: return "reallife_hustle_kind_electronics";
                case ThingKind.Furniture: return "reallife_hustle_kind_furniture";
                case ThingKind.Clothes: return "reallife_hustle_kind_clothes";
                case ThingKind.Shoes: return "reallife_hustle_kind_shoes";
                default: return "reallife_hustle_kind_car";
            }
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        public static int Share(int amount, int thousandths)
        {
            return (int)System.Math.Round(amount * (double)thousandths / 1000.0, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>A price as a person names it: in fives under 200, tens under 1,000, fifties above; never under 5.</summary>
        public static int Named(int amount)
        {
            int step = amount >= 1000 ? 50 : amount >= 200 ? 10 : 5;
            int named = (amount + step / 2) / step * step;
            return named < 5 ? 5 : named;
        }
    }
}
