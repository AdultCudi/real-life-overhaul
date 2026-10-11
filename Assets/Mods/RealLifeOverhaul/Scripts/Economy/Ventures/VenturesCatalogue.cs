using System;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>Who puts money into a business: a private backer of young firms, a rich individual, a fund.</summary>
    public enum InvestorKind { Angel = 0, Tycoon = 1, Firm = 2, Count = 3 }

    /// <summary>How an investor behaves when things go badly, and which answers they like.</summary>
    public enum Temperament { Patient = 0, Even = 1, Demanding = 2 }

    /// <summary>The game's business types in five groups: what an investor "likes".</summary>
    public enum Sector { Retail = 0, Food = 1, Office = 2, Leisure = 3, Industry = 4, Count = 5 }

    /// <summary>The parts of the city's economy the listed companies are in: the ones the mod's news is about.</summary>
    public enum MarketSector { Transport = 0, Property = 1, Retail = 2, Food = 3, Security = 4, Utilities = 5, Banking = 6, Entertainment = 7, Count = 8 }

    /// <summary>
    /// Every figure of investors and the market, in one place. Game-free: the offline checks compile this file, the
    /// Guide prints from it (Meta/FeatureList) and the README lists it ("Economy: investors and the market").
    /// Shares are in basis points (2,500 is 25%); prices in cents; money in whole units of the game's currency.
    /// </summary>
    public static class VenturesCatalogue
    {
        // ---------------------------------------------------------------- strictness (the presets set it)

        public const int Relaxed = 0, Standard = 1, Realistic = 2, Hardcore = 3;
        public const int LevelDefault = Standard;

        public static int Level(int level)
        {
            return level < Relaxed ? Relaxed : level > Hardcore ? Hardcore : level;
        }

        /// <summary>How much growth a target asks, as a percentage of the investor's own figure, by level.</summary>
        public static readonly int[] GrowthScalePct = { 50, 100, 125, 150 };
        /// <summary>Targets missed before a demanding investor calls their money back; 0: nobody ever does.</summary>
        public static readonly int[] MissesToCall = { 0, 3, 3, 3 };
        /// <summary>The days a called sum is paid back over.</summary>
        public static readonly int[] CallDays = { 30, 30, 20, 14 };
        /// <summary>Added to every investor's diligence (how hard they check a figure).</summary>
        public static readonly int[] DiligenceShift = { -15, 0, 10, 20 };
        /// <summary>Added to the score an investor wants before investing.</summary>
        public static readonly int[] ThresholdShift = { -5, 0, 3, 6 };

        // ---------------------------------------------------------------- the cast

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
        /// <summary>The share of a business's profit an investor of each kind takes: the least (a strong pitch) and the most.</summary>
        public static readonly int[] ShareLeastBp = { 800, 1500, 2000 };
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

        // The game's business types by the words in their ids ("ba:businesstype_coffeeshop"). A type none of these fits is retail.
        private static readonly string[] FoodWords = { "coffee", "fastfood", "restaurant", "supermarket", "fruit", "liquor", "bakery", "food" };
        private static readonly string[] OfficeWords = { "agency", "lawfirm", "designer", "headquarters", "bank", "school", "clinic", "hospital" };
        private static readonly string[] LeisureWords = { "gym", "nightclub", "cinema", "casino", "theater", "hairdresser" };
        private static readonly string[] IndustryWords = { "factory", "warehouse", "wholesale", "importexport", "cardealership", "garage", "movingservice", "installation", "gasstation" };

        /// <summary>The group one of the game's business types belongs to, from its id.</summary>
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

        // ---------------------------------------------------------------- an offer that comes to you

        public const int ProfitWindowDays = 14;      // the days a business is judged over
        public const int ProfitDaysNeeded = 10;      // of those, the days it must have made a profit
        public const int AverageDays = 7;            // "recent profit": the mean of this many days
        public const int OfferProfitFloor = 1500;    // that mean must be at least this
        public const int OfferEveryDays = 7;         // the businesses are looked at this often
        public const int OfferChancePct = 25;        // the chance, each look, for a business that qualifies
        public const int LikedBonusPct = 15;         // added where an investor who is free likes its sector
        public const int SlowdownChancePct = 50;     // the chance is this much of itself in a slowdown...
        public const int BoomChancePct = 125;        // ...and this much in a boom
        public const int OfferValueDays = 60;        // a business is valued at this many days of its recent profit
        public const int FollowOnValueDays = 70;     // ...and at this many for a second round after a target was met
        public const int OfferLapseDays = 5;         // an offer waits this long for an answer
        public const int OffersMost = 4;             // open at once

        /// <summary>The growth in recent profit an investor expects, in per cent, and the days they give it, by temperament.</summary>
        public static readonly int[] TargetGrowthPct = { 10, 15, 25 };
        public static readonly int[] TargetDays = { 30, 24, 18 };

        // A counter-offer: asked once per offer.
        public const int CounterMoneyPct = 25;       // "more money": this much more for the same share
        public const int CounterSharePct = 25;       // "a smaller share": the share cut by this much for the same money
        public static readonly int[] CounterAcceptPct = { 40, 30, 15 };    // by temperament
        public static readonly int[] CounterHalfPct = { 40, 40, 35 };      // meets you half way; the rest refuse
        public const int CounterQualityPct = 5;      // added to the chance of a yes for each profitable day over the days needed

        // ---------------------------------------------------------------- a deal

        public const int DealsMost = 8;
        public const int DealsPerBusiness = 2;
        public const int ShareCapBp = 6000;          // of one business's profit, all investors together
        public const int MoodStart = 60, MoodMet = 20, MoodMissed = -15, MoodArrears = -5, MoodKeptPromise = 5;
        public const int DemandDays = 5;             // to answer a demand; unanswered, the investor takes the seat (a fund: the promise)
        public const int BoardExtraBp = 1000;        // a seat on the board: this much more of the profit...
        public const int BoardDays = 30;             // ...for this long
        public const int CostCutPct = 10;            // a promise to cut costs: daily costs this much lower...
        public const int CostCutDays = 14;           // ...on average over this many days
        public const int CallKeptPct = 50;           // called back: the sum put in, less this much of what has been paid out...
        public const int CallLeastPct = 25;          // ...and never less than this much of the sum put in
        public const int CallMissesMost = 5;         // days running a called sum's payment may be missed before the claim
        public const int BuyOutDays = 75;            // the price to buy an investor out: their share of this many days of recent profit...
        public const int BuyOutFloorPct = 110;       // ...and never less than this much of what they put in

        public static readonly string[] MoodKeys = { "reallife_ven_mood_unhappy", "reallife_ven_mood_uneasy", "reallife_ven_mood_content", "reallife_ven_mood_pleased" };

        public static int MoodWord(int mood)
        {
            return mood >= 75 ? 3 : mood >= 50 ? 2 : mood >= 25 ? 1 : 0;
        }

        // ---------------------------------------------------------------- money for a business not yet open

        public const int PledgeDays = 14;            // the business must be opened within this many days
        public const int PledgesMost = 3;
        public static readonly int[] BudgetSteps = { 10000, 20000, 35000, 50000, 75000, 100000, 150000, 250000, 500000 };
        public const int BudgetShareLeastPct = 30;   // of the budget, at the score an investor just accepts...
        public const int BudgetShareMostPct = 70;    // ...and at a perfect one

        // ---------------------------------------------------------------- the pitch

        public const int PitchCooldownDays = 10;     // one approach to an investor in this many days
        public const int CaughtCloseDays = 60;       // an investor who catches an untrue figure sees nobody for this long
        public const int ReputationStart = 70, ReputationCaught = -25, ReputationMet = 3, ReputationBoughtOut = 2, ReputationClaim = -15;
        public static readonly int[] Questions = { 6, 7, 8 };             // by kind of investor
        public static readonly int[][] QuestionMix =                      // facts, judgement, the founder
        {
            new[] { 2, 2, 2 }, new[] { 3, 2, 2 }, new[] { 4, 3, 1 }
        };
        public const int PointsMost = 10;            // for one answer
        public const int TruthBase = 4;              // a true figure scores at least this, however weak
        public const int StretchCatchPct = 45;       // the chance a figure made to sound better is caught, as a share of the investor's diligence...
        public const int BoldCatchPct = 90;          // ...and one made to sound much better
        public const int CatchLeastPct = 3, CatchMostPct = 95;
        public static readonly int[] ScoreNeeded = { 45, 55, 62 };        // by kind of investor, of 100
        public const int LikedScore = 5;             // added for a business in a sector the investor likes
        public const int ValueDaysLeast = 40;        // a pitch values a business at this many days of recent profit at the score just accepted...
        public const int ValueDaysMost = 90;         // ...and at this many at a perfect score
        public const int YoungDays = 30;             // an angel backs a business younger than this on its founder: the smallest sum they put in, whatever the profit

        // ---------------------------------------------------------------- the market

        public const int Companies = 12;
        public const int RivalsListed = 4;           // of those, the game's own rivals at most
        public const int HistoryDays = 30;
        public const int CommissionBp = 100;         // 1% of every purchase and sale...
        public const int CommissionLeast = 5;        // ...and never less than this
        public const int HoldingMost = 5000;         // shares of one company
        public const int DividendEveryDays = 15;
        public const int DividendLeastBp = 40, DividendMostBp = 110;     // of the price, each payout
        public const int VolLeastBp = 80, VolMostBp = 200;               // a company's ordinary daily move
        public const int PriceLeastCents = 2000, PriceMostCents = 24000; // where a company starts
        public const int PriceFloorCents = 100;
        public const double DriftBp = 4.0;           // the daily rise of what a company is worth
        public const double PullPerDay = 0.004;      // how fast a price comes back towards what the company is worth
        public const int NewsCapBp = 150;            // the most a day's news can move one company: less than the 2% it costs to sell and buy back
        public const double ClimateBp = 2.0;         // added each day in a boom, taken off in a slowdown
        public const int RateMoveBp = 40;            // a bank's shares for each point the bank rate has moved (up: up)

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

        /// <summary>Invented firms, two to a sector: the first of each is always listed, the second where the game shows no rival to list.</summary>
        public static readonly string[][] CompanyNames =
        {
            new[] { "Metro Line Carriers", "Harbour Freight" }, new[] { "Crown Square Estates", "Eastbank Land" },
            new[] { "Fairlane Stores", "Corner Row Stores" }, new[] { "Greenmarket Foods", "Third Street Kitchens" },
            new[] { "Watchfield Security", "Ironkey Alarms" }, new[] { "City Light and Power", "Riverside Water" },
            new[] { "Merchants Union Bank", "Dockside Savings" }, new[] { "Marquee Pictures", "Night Row Venues" }
        };

        // ---------------------------------------------------------------- arithmetic used everywhere

        /// <summary>A share of an amount, rounded down; never of a loss.</summary>
        public static int Share(float amount, int shareBp)
        {
            return amount <= 0f || shareBp <= 0 ? 0 : (int)Math.Floor((double)amount * shareBp / 10000.0);
        }

        /// <summary>A linear step between two whole figures: at 0 the first, at 1 the second.</summary>
        public static int Lerp(int from, int to, double part)
        {
            part = part < 0.0 ? 0.0 : part > 1.0 ? 1.0 : part;
            return (int)Math.Round(from + (to - from) * part);
        }

        /// <summary>An amount of money as people say it: two figures that count and noughts after them (1,234 is 1,200).</summary>
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

    /// <summary>
    /// Numbers that are always the same for the same seed, on every machine: the cast, a counter-offer's answer, a
    /// check of a figure, a day of the market. A roll is a hash of what it is about, so that loading a save and trying
    /// again gives the same answer and the order things are asked in changes nothing.
    /// </summary>
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

        /// <summary>A whole number from 0 up to, not including, the count.</summary>
        public int Below(int count)
        {
            return count <= 1 ? 0 : (int)(Next() % (uint)count);
        }

        /// <summary>A whole number from the first to the second, both included.</summary>
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

        /// <summary>A whole number from 0 to 99 for one thing in one save.</summary>
        public static int Percent(int seed, int what, int which)
        {
            return (int)(Hash(seed, what, which) % 100u);
        }

        /// <summary>A number from 0 up to, not including, 1.</summary>
        public static double Unit(int seed, int what, int which)
        {
            return (Hash(seed, what, which) & 0xFFFFFFu) / (double)0x1000000;
        }

        /// <summary>The hash of a text that is the same on every machine (string.GetHashCode is not).</summary>
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
