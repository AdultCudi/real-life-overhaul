using System;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>Who puts money into a business: a private backer of young firms, a rich individual, a fund.</summary>
    public enum InvestorKind { Angel = 0, Tycoon = 1, Firm = 2, Count = 3 }

    /// <summary>How an investor behaves when things go badly, and which answers they like.</summary>
    public enum Temperament { Patient = 0, Even = 1, Demanding = 2 }

    /// <summary>The game's business types in five groups: what an investor "likes".</summary>
    public enum Sector { Retail = 0, Food = 1, Office = 2, Leisure = 3, Industry = 4, Count = 5 }

    /// <summary>The parts of the city's economy the listed companies are in.</summary>
    public enum MarketSector { Transport = 0, Property = 1, Retail = 2, Food = 3, Security = 4, Utilities = 5, Banking = 6, Entertainment = 7, Count = 8 }

    /// <summary>Every figure of investors and the old market, game-free: shares in basis points (2,500 is 25%), prices in cents.</summary>
    public static class VenturesCatalogue
    {
        public const int Relaxed = 0, Standard = 1, Realistic = 2, Hardcore = 3;   // how demanding investors are: the presets set it
        public const int LevelDefault = Standard;

        public static int Level(int level)
        {
            return level < Relaxed ? Relaxed : level > Hardcore ? Hardcore : level;
        }

        // By level.
        public static readonly int[] GrowthScalePct = { 50, 100, 125, 150 };   // a target's growth, of the investor's own figure
        public static readonly int[] MissesToCall = { 0, 3, 3, 3 };            // targets missed before a demanding investor calls their money; 0: never
        public static readonly int[] CallDays = { 30, 30, 20, 14 };            // days a called sum is paid back over
        public static readonly int[] DiligenceShift = { -15, 0, 10, 20 };      // added to every investor's diligence
        public static readonly int[] ThresholdShift = { -5, 0, 3, 6 };         // added to the score an investor wants

        // The cast, by kind of investor.
        public const int TycoonsLeast = 2, TycoonsMost = 3, Angels = 3, Firms = 2;
        public const int InvestorsMost = TycoonsMost + Angels + Firms;

        public static readonly int[][] ChequeLeast =
        {
            new[] { 4000, 5000, 7500 }, new[] { 25000, 40000, 60000 }, new[] { 75000, 100000, 150000 }
        };
        public static readonly int[][] ChequeMost =
        {
            new[] { 30000, 45000, 60000 }, new[] { 400000, 750000, 1500000 }, new[] { 1000000, 2500000, 5000000 }
        };
        public static readonly int[] DiligenceLeast = { 25, 50, 78 };
        public static readonly int[] DiligenceMost = { 45, 75, 95 };
        public static readonly int[] ShareLeastBp = { 800, 1500, 2000 };   // of a business's profit, after a strong pitch
        public static readonly int[] ShareMostBp = { 2000, 3500, 4000 };
        public const int LikedSectors = 2;

        public static readonly string[] FirstNames =
        {
            "Adele", "Marcus", "Ines", "Tobias", "Leona", "Victor", "Nadia", "Rupert", "Selma", "Hugo", "Oriel", "Dexter", "Mirren", "Caspar", "Thea", "Lowell"
        };
        public static readonly string[] LastNames =
        {
            "Ashdown", "Brandt", "Calloway", "Delacroix", "Everhart", "Fenwick", "Garrow", "Halloran", "Ilves", "Jessop", "Kestrel", "Lindqvist", "Marlowe", "Northcott", "Okafor", "Penhale"
        };
        public static readonly string[] FirmNames =
        {
            "Harrow Vale Capital", "Northgate Ventures", "Quillon Partners", "Stonebridge Growth", "Larkfield Capital", "Tidewater Partners"
        };

        public static readonly string[] KindKeys = { "reallife_ven_kind_angel", "reallife_ven_kind_tycoon", "reallife_ven_kind_firm" };
        public static readonly string[] TemperKeys = { "reallife_ven_temper_patient", "reallife_ven_temper_even", "reallife_ven_temper_demanding" };
        public static readonly string[] SectorKeys =
        {
            "reallife_ven_sector_retail", "reallife_ven_sector_food", "reallife_ven_sector_office", "reallife_ven_sector_leisure", "reallife_ven_sector_industry"
        };

        // Words in the game's business type ids ("ba:businesstype_coffeeshop"); a type none of them fits is retail.
        private static readonly string[] FoodWords = { "coffee", "fastfood", "restaurant", "supermarket", "fruit", "liquor", "bakery", "food", "icecream", "teabar" };
        private static readonly string[] OfficeWords =
        {
            "agency", "lawfirm", "designer", "headquarters", "bank", "school", "clinic", "hospital", "reallife:businesstype_"   // the last: the tech sector's types
        };
        private static readonly string[] LeisureWords = { "gym", "nightclub", "cinema", "casino", "theater", "hairdresser" };
        private static readonly string[] IndustryWords = { "factory", "warehouse", "wholesale", "importexport", "cardealership", "garage", "movingservice", "installation", "gasstation" };

        public static Sector SectorOf(string businessType)
        {
            if (string.IsNullOrEmpty(businessType))
            {
                return Sector.Retail;
            }
            return Has(businessType, FoodWords) ? Sector.Food : Has(businessType, OfficeWords) ? Sector.Office
                : Has(businessType, LeisureWords) ? Sector.Leisure : Has(businessType, IndustryWords) ? Sector.Industry : Sector.Retail;
        }

        private static bool Has(string text, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (text.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public const int ProfitWindowDays = 14;      // days a business is judged over
        public const int ProfitDaysNeeded = 10;      // of those, days in profit
        public const int AverageDays = 7;            // "recent profit" is the mean of this many days
        public static int OfferProfitFloor => Prices.At(1500);    // the least recent profit a day
        public const int OfferEveryDays = 7;         // days between looks at the businesses
        public const int OfferChancePct = 25;        // each look, for a business that qualifies
        public const int LikedBonusPct = 15;         // added where a free investor likes its sector
        public const int SlowdownChancePct = 50;     // of the chance, in a slowdown
        public const int BoomChancePct = 125;        // of the chance, in a boom
        public const int OfferValueDays = 60;        // days of recent profit a business is valued at
        public const int FollowOnValueDays = 70;     // the same for a second round after a target met
        public const int OfferLapseDays = 5;         // days an offer waits for an answer
        public const int OffersMost = 4;

        // By temperament.
        public static readonly int[] TargetGrowthPct = { 10, 15, 25 };   // of recent profit
        public static readonly int[] TargetDays = { 30, 24, 18 };

        // A counter-offer, one per offer.
        public const int CounterMoneyPct = 25;       // more money for the same share
        public const int CounterSharePct = 25;       // the share cut, for the same money
        public static readonly int[] CounterAcceptPct = { 40, 30, 15 };    // by temperament
        public static readonly int[] CounterHalfPct = { 40, 40, 35 };      // half way; the rest refuse
        public const int CounterQualityPct = 5;      // added to the yes for each profitable day over those needed

        public const int DealsMost = 8;
        public const int DealsPerBusiness = 2;
        public const int ShareCapBp = 6000;          // of one business's profit, all investors together
        public const int MoodStart = 60, MoodMet = 20, MoodMissed = -15, MoodArrears = -5, MoodKeptPromise = 5;
        public const int DemandDays = 5;             // to answer a demand before the investor chooses
        public const int BoardExtraBp = 1000;        // a seat on the board: this much more of the profit...
        public const int BoardDays = 30;             // ...for this many days
        public const int CostCutPct = 10;            // a promise to cut daily costs by this much...
        public const int CostCutDays = 14;           // ...within this many days
        public const int CallKeptPct = 50;           // called back: the sum put in less this much of what was paid out...
        public const int CallLeastPct = 25;          // ...and at least this much of the sum put in
        public const int CallMissesMost = 5;         // days running a called payment may be missed before the claim
        public const int BuyOutDays = 75;            // a buy-out: their share of this many days of recent profit...
        public const int BuyOutFloorPct = 110;       // ...and at least this much of what they put in

        public static readonly string[] MoodKeys = { "reallife_ven_mood_unhappy", "reallife_ven_mood_uneasy", "reallife_ven_mood_content", "reallife_ven_mood_pleased" };

        public static int MoodWord(int mood)
        {
            return mood >= 75 ? 3 : mood >= 50 ? 2 : mood >= 25 ? 1 : 0;
        }

        // Money for a business not yet open.
        public const int PledgeDays = 14;            // to open the business in
        public const int PledgesMost = 3;
        public static readonly int[] BudgetSteps = { 10000, 20000, 35000, 50000, 75000, 100000, 150000, 250000, 500000 };   // at the start's prices

        /// <summary>A budget step at today's prices.</summary>
        public static int BudgetAt(int step) => Prices.At(BudgetSteps[Math.Max(0, Math.Min(BudgetSteps.Length - 1, step))]);
        public const int BudgetShareLeastPct = 30;   // of the budget, at the score an investor just accepts...
        public const int BudgetShareMostPct = 70;    // ...and at a perfect one

        public const int PitchCooldownDays = 10;     // between approaches to one investor
        public const int CaughtCloseDays = 60;       // an investor who catches an untrue figure sees nobody for this long
        public const int ReputationStart = 70, ReputationCaught = -25, ReputationMet = 3, ReputationBoughtOut = 2, ReputationClaim = -15;
        public const int ReputationCold = -10;       // an investor lost interest before they had their money back...
        public const int ColdClosedDays = 100000;    // ...and sees the founder no more
        public static readonly int[] Questions = { 6, 7, 8 };             // by kind of investor
        public static readonly int[][] QuestionMix =                      // facts, judgement, the founder
        {
            new[] { 2, 2, 2 }, new[] { 3, 2, 2 }, new[] { 4, 3, 1 }
        };
        public const int PointsMost = 10;            // for one answer
        public const int TruthBase = 4;              // the least a true figure scores, however weak
        public const int StretchCatchPct = 45;       // of the investor's diligence: the chance a better-sounding figure is caught...
        public const int BoldCatchPct = 90;          // ...and one that sounds much better
        public const int CatchLeastPct = 3, CatchMostPct = 95;
        public static readonly int[] ScoreNeeded = { 45, 55, 62 };        // of 100, by kind of investor
        public const int LikedScore = 5;             // added for a sector the investor likes
        public const int ValueDaysLeast = 40;        // days of recent profit a business is valued at, at the score just accepted...
        public const int ValueDaysMost = 90;         // ...and at a perfect score
        public const int YoungDays = 30;             // an angel backs a business younger than this on its founder, with their smallest sum

        // The old market, kept for the save and its carry-over to Scripts/Markets.
        public const int Companies = 12;
        public const int RivalsListed = 4;           // of those, the game's own rivals at most
        public const int HistoryDays = 30;
        public const int CommissionBp = 100;         // of every purchase and sale...
        public static int CommissionLeast => Prices.At(5);        // ...and at least this
        public const int HoldingMost = 5000;         // shares of one company
        public const int DividendEveryDays = 15;     // market days
        public const int DividendLeastBp = 40, DividendMostBp = 110;     // of the price, each payout
        public const int VolLeastBp = 80, VolMostBp = 200;               // a company's ordinary daily move
        public const int PriceLeastCents = 2000, PriceMostCents = 24000; // where a company starts
        public const int PriceFloorCents = 100;
        public const double DriftBp = 4.0;           // the daily rise of what a company is worth
        public const double PullPerDay = 0.004;      // how fast a price comes back towards what the company is worth
        public const int NewsCapBp = 150;            // a day's news on one company: less than the 2% it costs to sell and buy back
        public const double ClimateBp = 2.0;         // a day, added in a boom and taken off in a slowdown
        public const int RateMoveBp = 40;            // a bank's shares, for each point the bank rate moves (up: up)

        public static readonly string[] MarketSectorKeys =
        {
            "reallife_ven_market_transport", "reallife_ven_market_property", "reallife_ven_market_retail", "reallife_ven_market_food",
            "reallife_ven_market_security", "reallife_ven_market_utilities", "reallife_ven_market_banking", "reallife_ven_market_entertainment"
        };
        public static readonly string[] MovesKeys =
        {
            "reallife_ven_moves_transport", "reallife_ven_moves_property", "reallife_ven_moves_retail", "reallife_ven_moves_food",
            "reallife_ven_moves_security", "reallife_ven_moves_utilities", "reallife_ven_moves_banking", "reallife_ven_moves_entertainment"
        };

        /// <summary>Invented firms, two to a sector: the first always listed, the second where there is no rival of the game's to list.</summary>
        public static readonly string[][] CompanyNames =
        {
            new[] { "Metro Line Carriers", "Harbour Freight" }, new[] { "Crown Square Estates", "Eastbank Land" },
            new[] { "Fairlane Stores", "Corner Row Stores" }, new[] { "Greenmarket Foods", "Third Street Kitchens" },
            new[] { "Watchfield Security", "Ironkey Alarms" }, new[] { "City Light and Power", "Riverside Water" },
            new[] { "Merchants Union Bank", "Dockside Savings" }, new[] { "Marquee Pictures", "Night Row Venues" }
        };

        /// <summary>A share of an amount, rounded down; never of a loss.</summary>
        public static int Share(float amount, int shareBp)
        {
            return amount <= 0f || shareBp <= 0 ? 0 : (int)Math.Floor((double)amount * shareBp / 10000.0);
        }

        /// <summary>A linear step between two whole figures: the first at 0, the second at 1.</summary>
        public static int Lerp(int from, int to, double part)
        {
            part = part < 0.0 ? 0.0 : part > 1.0 ? 1.0 : part;
            return (int)Math.Round(from + (to - from) * part);
        }

        /// <summary>An amount as people say it: two figures that count and noughts after them (1,234 is 1,200).</summary>
        public static long Round2(double amount)
        {
            if (amount == 0.0 || double.IsNaN(amount) || double.IsInfinity(amount))
            {
                return 0L;
            }
            double size = Math.Abs(amount);
            double unit = Math.Pow(10.0, Math.Max(0.0, Math.Floor(Math.Log10(size)) - 1.0));
            return (long)(Math.Round(amount / unit) * unit);
        }
    }

    /// <summary>Seeded numbers, the same on every machine: a roll is a hash of what it is about, so trying again gives the same answer.</summary>
    public struct VenturesRandom
    {
        private uint _state;

        public VenturesRandom(int seed)
        {
            _state = Hash(seed, 0x51ed, 0x2f) | 1u;
        }

        public uint Next()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }

        /// <summary>0 up to, not including, the count.</summary>
        public int Below(int count)
        {
            return count <= 1 ? 0 : (int)(Next() % (uint)count);
        }

        /// <summary>From the first to the second, both included.</summary>
        public int Range(int from, int to)
        {
            return to <= from ? from : from + Below(to - from + 1);
        }

        public static uint Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u;
                h ^= (uint)b * 0x85EBCA6Bu + 0x7F4A7C15u + (h << 6) + (h >> 2);
                h ^= (uint)c * 0xC2B2AE35u + 0x165667B1u + (h << 6) + (h >> 2);
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>0 to 99 for one thing in one save.</summary>
        public static int Percent(int seed, int what, int which)
        {
            return (int)(Hash(seed, what, which) % 100u);
        }

        /// <summary>From 0 up to, not including, 1.</summary>
        public static double Unit(int seed, int what, int which)
        {
            return (Hash(seed, what, which) & 0xFFFFFFu) / (double)0x1000000;
        }

        /// <summary>A text's hash, the same on every machine (string.GetHashCode is not).</summary>
        public static int Of(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)h;
            }
        }
    }
}
