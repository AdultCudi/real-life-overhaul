using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The parts of the city's economy a listed company is in. Saved as a number: never renumber, add at the end.</summary>
    public enum MarketSector
    {
        Banks = 0, Insurance, Finance, Property, Construction, Power, Water, Telecoms, Energy, Transport, Logistics, Stores, Specialists,
        Groceries, Restaurants, Leisure, Media, Hardware, Software, Healthcare, Industry, Goods, Services, Trusts, Count
    }

    /// <summary>How big a company is. Saved as a number.</summary>
    public enum MarketSize { Large = 0, Mid = 1, Small = 2 }

    /// <summary>One listed company as the catalogue writes it; its profile is its sector's moved by a fixed amount from its id.</summary>
    public sealed class CompanyDef
    {
        public readonly int Id;                 // saved: never reuse or renumber
        public readonly string Name;            // a proper name, not translated
        public readonly string Ticker;
        public readonly MarketSector Sector;
        public readonly MarketSize Size;
        public readonly int RevenueM;           // turnover a year at the start's prices, in millions
        public readonly int PriceCents;         // where it starts; the shares in issue follow from it
        public readonly string Trade;           // the game's business type it competes with, or null
        public readonly bool Pipeline;          // not listed at the start: may come to the market later
        public readonly int PayDays;            // a property trust pays every so many calendar days; 0 a dividend each quarter
        public readonly bool Holding;           // a rival tycoon's: listed only when its tycoon comes forward

        public int MarginBp, GrowthBp, Beta, Cyclical, DebtPct, IdioBp, PassPct, RateBp, TurnoverBp, SpreadBp;

        public CompanyDef(int id, string name, string ticker, MarketSector sector, MarketSize size, int revenueM, int priceCents, string trade, bool pipeline = false,
            int payDays = 0, bool holding = false)
        {
            Id = id;
            Name = name;
            Ticker = ticker;
            Sector = sector;
            Size = size;
            RevenueM = revenueM;
            PriceCents = priceCents;
            Trade = trade;
            Pipeline = pipeline;
            PayDays = payDays;
            Holding = holding;
        }
    }

    /// <summary>A sector's usual figures. Margins and growth in basis points a year; beta and cyclicality in hundredths.</summary>
    public struct SectorProfile
    {
        public int MarginBp, GrowthBp, Beta, Cyclical, DebtPct, IdioBp, SectorVolBp, PassPct, RateBp;

        public SectorProfile(int margin, int growth, int beta, int cyclical, int debt, int idio, int sectorVol, int pass, int rate)
        {
            MarginBp = margin;
            GrowthBp = growth;
            Beta = beta;
            Cyclical = cyclical;
            DebtPct = debt;
            IdioBp = idio;
            SectorVolBp = sectorVol;
            PassPct = pass;
            RateBp = rate;
        }
    }

    /// <summary>
    /// Every figure of the city's stock market, and its companies. Game-free: the offline checks compile it and the Guide
    /// prints from it. A market day is a game weekday, 09:30 to 16:00 in 78 steps of five minutes; "a year" is the game's
    /// year (60 to 365 days). Model money is whole units at today's prices, share prices in cents, bp hundredths of a per cent.
    /// </summary>
    public static class MarketCatalogue
    {
        public const int OpenMinute = 9 * 60 + 30;       // 09:30
        public const int CloseMinute = 16 * 60;          // 16:00
        public const int StepMinutes = 5;
        public const int Steps = (CloseMinute - OpenMinute) / StepMinutes;   // 78
        public const int ResultsMinute = 7 * 60 + 30;    // before the open
        /// <summary>Phases of a market day: 0 not begun, 1 results out, 2 open, 3 + k after step k, then the close.</summary>
        public const int PhaseNone = 0, PhaseResults = 1, PhaseOpen = 2, PhaseClose = 3 + Steps;   // 81

        public const double IndexDayVol = 0.0095;        // the market's own daily move in ordinary times, in a long year
        public const double OvernightShare = 0.22;       // of a day's variance, at the open
        public const double WeekendExtra = 0.25;         // of a night more for each day the market was shut
        public const double SizePremiumMid = 0.003, SizePremiumSmall = 0.01;    // a year, on the cost of equity

        // Most of a move is news about the business, which stays; a part is the market's mood, which fades slowly. These are
        // the mood's share of each kind of move's variance, and how slowly it fades.
        public const double MarketMoodShare = 0.35;      // of the whole market's moves
        public const double SectorMoodShare = 0.25;      // of a sector's
        public const double OwnMoodShare = 0.08;         // of a company's own
        public const double CrashMoodShare = 0.5;        // of a crash: the panic the market makes up in time
        public const double MoodHalfLifeYears = 3.0;     // years until half a mood is gone...
        public const int MoodHalfLifeDaysLeast = 40;     // ...never under this many market days
        public const double OutlookYears = 3.0;          // years for what the market expects to show in turnover
        public const double GarchA = 0.06, GarchGamma = 0.05, GarchB = 0.895;   // the market's variance: a day's shock, a fall's extra, persistence
        public const double VarianceLeast = 0.30, VarianceMost = 9.0;          // of the ordinary variance
        public const double SectorGarchA = 0.06, SectorGarchB = 0.88;
        public const double TailShare = 0.08, TailScale = 1.85, BodyScale = 0.88;   // fat tails: a mixture of two bell curves
        public const double IntradayOpenWeight = 1.3, IntradayCloseWeight = 0.7, IntradayDecaySteps = 8.0;   // more of the day's moves near the open and the close

        // Crashes and manias: rare, and as likely to cost as to pay.
        public const double CrashChanceYear = 0.035;     // in an ordinary year of the market
        public const double CrashRecessionScale = 3.0, CrashSlowdownScale = 1.6, CrashDearScale = 2.0;
        public const double CrashLeast = 0.05, CrashMost = 0.12;   // the first day's fall
        public const double ManiaChanceYear = 0.006;     // each sector each year
        public const int ManiaDaysLeast = 15, ManiaDaysMost = 45;
        public const double ManiaDrift = 0.0035;         // a day, while it lasts

        public const double TaxRate = 0.22;              // on profit, before the council's business tax lever...
        public const double TaxPerStep = 0.02;           // ...each step of it
        public const double LongRealGrowth = 0.015;      // a year, after inflation
        public const double GrowthFadeYears = 10.0;       // years for a company's growth to come back to it
        public const double MarginReturnYears = 1.0;     // years for its own swings in margin to fade (MarketValuation.PatchFadeYears)
        public const double RevenueNoiseYear = 0.04;     // a year: what nobody outside can see coming
        public const double MarginNoiseYear = 0.008;
        public const double GrowthNoiseYear = 0.010;
        public const double DebtRepriceYears = 2.0;      // years to take on a new rate
        public const double CreditSpreadLeast = 0.012, CreditSpreadMost = 0.07;
        public const double DistressHazardMost = 1.5;    // a year, at the worst; as distress squared
        public const double DistressFrom = 0.15;         // no company with less distress than this fails
        public const double RecoveryChance = 0.6;        // how often a failed company pays its holders something...
        public const double RecoveryMost = 0.08;         // ...at most this share of its last price
        public const int PennyCents = 100;               // cents at the start's prices; closing under it...
        public const int PennyDays = 20;                 // ...this many market days running delists it
        public const double RoeLong = 0.12;              // earned on what it keeps back; it pays out the rest

        // The cycle: growth and margins by state (boom, steady, slowdown, recession), times a company's cyclicality.
        public static readonly int[] CycleGrowthBp = { 300, 0, -300, -800 };
        public static readonly int[] CycleMarginBp = { 60, 0, -80, -220 };
        public static readonly double[] CyclePremium = { 0.92, 1.0, 1.1, 1.25 };    // the equity premium asked
        public static readonly double[] CycleVolatility = { 0.9, 1.0, 1.2, 1.6 };   // of the ordinary variance
        /// <summary>What the market expects to follow each state, per cent: the city's odds before a save's own change to them.</summary>
        public static readonly int[][] CycleNext = { new[] { 0, 70, 30, 0 }, new[] { 45, 20, 35, 0 }, new[] { 0, 65, 0, 35 }, new[] { 0, 65, 35, 0 } };
        public const double CycleNowWeight = 0.6;        // the state as it is, against the state the market expects next

        /// <summary>A figure of the cycle's as the market prices it: the state now with the states that may follow it.</summary>
        public static double Expected(int[] byState, int state)
        {
            int now = state < 0 ? 0 : state > 3 ? 3 : state;
            double next = 0.0;
            for (int j = 0; j < 4; j++)
            {
                next += CycleNext[now][j] * byState[j];
            }
            return CycleNowWeight * byState[now] + (1.0 - CycleNowWeight) * next / 100.0;
        }

        public static double Expected(double[] byState, int state)
        {
            int now = state < 0 ? 0 : state > 3 ? 3 : state;
            double next = 0.0;
            for (int j = 0; j < 4; j++)
            {
                next += CycleNext[now][j] * byState[j];
            }
            return CycleNowWeight * byState[now] + (1.0 - CycleNowWeight) * next / 100.0;
        }

        public const int ResultsLagLeastPct = 4, ResultsLagMostPct = 11;   // days after a quarter ends, per cent of the year
        public const int DividendRecordDays = 4;         // market days from the results to the record day
        public const int DividendPayDays = 4;            // ...and from the record day to the payment
        public const double DividendSmooth = 0.35;       // how far a board moves its dividend to what it can pay at once
        public const int QuartersKept = 8;
        public const double ResultsDayVolScale = 1.5;    // a company's own moves on its results day
        public const double ResultsReaction = 0.5;       // of a quarter's profit surprise, the price's move at the open

        // A property trust lets buildings and pays out most of what it earns, in small sums every week or every third
        // calendar day (TrustRules). Its price follows the city's property market as well as its rents.
        public const int TrustWeekly = 7, TrustThreeDays = 3;   // days of the calendar between two distributions
        public const double TrustPayout = 0.9;           // of its profit, at least
        public const double TrustPropertyShare = 0.5;    // of its worth: the buildings at the property market's value

        public const int SplitAboveCents = 30000;        // a board splits its shares when they trade over this...
        public const int SplitNoticeDays = 5;            // ...announcing it this many market days before
        public const double TakeoverChanceYear = 0.012;  // each company, more for small ones
        public static readonly double[] TakeoverSizeScale = { 0.4, 1.2, 2.0 };   // by size
        public const double TakeoverPremiumLeast = 0.18, TakeoverPremiumMost = 0.45;
        public const int TakeoverDaysLeast = 8, TakeoverDaysMost = 20;
        public const double TakeoverFailChance = 0.2;
        // A company's own big news now and then, a crisis or a windfall: its usual margin moves for good, its price at the
        // next open, hardest for a company with much debt.
        public const double CrisisChanceYear = 0.015;    // each company, each year...
        public static readonly double[] CrisisSizeScale = { 0.7, 1.0, 1.5 };   // ...by size
        public const double CrisisMarginLeast = 0.25, CrisisMarginMost = 0.65; // of its usual margin, lost for good
        public const double WindfallChanceYear = 0.008;
        public const double WindfallMarginLeast = 0.15, WindfallMarginMost = 0.4;
        public const double ListingChanceYear = 2.5;     // new listings a year, while the pipeline has companies
        public const int ListingNoticeDays = 5;
        public const int CompaniesMost = 200;

        public const int CommissionBp = 10;              // 0.10% of every purchase and sale...
        public const int CommissionLeastAtStart = 5;     // ...and never less than this, at the start's prices (Prices.At)
        public const double ImpactTemporary = 0.6;       // of a day's move times the root of the order over a day's volume
        public const double ImpactPermanent = 0.25;      // the part of it that stays in the price
        public const double ImpactMemorySteps = 12.0;    // steps: recent trading in a company counts towards the next order
        public const int OrderShareOfVolumePct = 20;     // one order: at most this share of a day's usual volume
        public const int HoldingShareBp = 1000;          // at most 10% of a company's shares
        public const int HoldingFounderBp = 7500;        // a rival tycoon's own stake in their holding; the rest is free float
        public const int ParticipationPct = 25;          // a resting order takes at most this share of a step's volume
        public const int GoodTillDays = 60;              // market days an order "until cancelled" stays open
        public const int OrdersMost = 60;
        public const int TradesKept = 120;
        public const int NewsKept = 300;                 // the last pieces of news (about a week's)
        public const int WatchMost = 40;
        public static readonly int[] TurnoverBp = { 30, 45, 65 };       // of the shares in issue traded on an ordinary day, by size
        public static readonly int[] SpreadBp = { 4, 14, 55 };          // the bid-offer spread, by size, in ordinary times
        public const int SpreadMostBp = 400;

        public const int MarginOpenLeastAtStart = 2000;  // equity to open one, at the start's prices
        public const int InitialMarginPct = 50;          // of a new position, in equity
        public const int MaintenanceLongPct = 25, MaintenanceShortPct = 30, MaintenanceRiskyExtraPct = 15;
        public const int MarginRateOverBp = 300;         // a year, over the bank rate, on what is borrowed
        public const int BorrowFeeLeastBp = 50, BorrowFeeMostBp = 900;   // a year, on the value of shares sold short
        public const int CallDays = 1;                   // market days to meet a margin call before the broker sells
        public const int ForcedBelowPct = 80;            // of the maintenance: below this the broker sells at once

        public const int DailyKept = 100;                // daily candles; older ones are folded into months
        public const int MonthlyKept = 120;
        public const int IntradayBars = Steps;

        public static readonly SectorProfile[] Sectors =
        {
            //                margin growth beta cycl debt idio secvol pass rate
            new SectorProfile(3000, 300, 110, 120, 0, 120, 70, 100, 60),     // banks
            new SectorProfile(1200, 300, 80, 60, 20, 110, 55, 100, 20),      // insurance
            new SectorProfile(2500, 700, 110, 120, 30, 150, 80, 100, 10),    // finance
            new SectorProfile(4500, 250, 90, 90, 300, 110, 65, 80, 0),       // property
            new SectorProfile(900, 400, 115, 150, 40, 150, 80, 90, 0),       // construction
            new SectorProfile(1800, 200, 50, 30, 150, 90, 45, 60, 0),        // power
            new SectorProfile(2500, 150, 45, 20, 200, 80, 40, 70, 0),        // water and gas
            new SectorProfile(1800, 250, 70, 40, 120, 110, 50, 70, 0),       // telecoms
            new SectorProfile(1000, 150, 110, 120, 50, 170, 95, 110, 0),     // energy
            new SectorProfile(800, 300, 105, 130, 80, 140, 70, 80, 0),       // transport
            new SectorProfile(700, 450, 110, 130, 50, 140, 70, 90, 0),       // logistics
            new SectorProfile(500, 250, 100, 120, 30, 130, 65, 90, 0),       // department and discount stores
            new SectorProfile(800, 400, 110, 140, 30, 160, 75, 85, 0),       // specialist shops
            new SectorProfile(400, 250, 55, 30, 30, 100, 45, 95, 0),         // groceries
            new SectorProfile(1000, 400, 90, 100, 50, 140, 60, 90, 0),       // restaurants and cafes
            new SectorProfile(1200, 400, 120, 150, 70, 160, 80, 85, 0),      // leisure and hotels
            new SectorProfile(1400, 200, 100, 110, 50, 150, 70, 80, 0),      // media
            new SectorProfile(1600, 800, 120, 130, 20, 170, 90, 70, 0),      // technology hardware
            new SectorProfile(2200, 1200, 115, 90, 15, 180, 95, 90, 0),     // software and internet
            new SectorProfile(1500, 500, 70, 30, 40, 140, 55, 95, 0),        // health care
            new SectorProfile(900, 300, 120, 150, 50, 150, 75, 85, 0),       // industry
            new SectorProfile(1400, 300, 65, 40, 40, 110, 45, 90, 0),        // consumer goods
            new SectorProfile(1000, 400, 95, 100, 30, 140, 65, 85, 0),       // business services
            new SectorProfile(4500, 100, 75, 70, 300, 90, 55, 60, 0)         // property trusts
        };

        private const string P = "reallife";   // apart, so a key made of parts is not taken for a whole key

        public static readonly string[] SectorKeys = Keys(P + "_mkt_sector_", "banks", "insurance", "finance", "property", "construction", "power", "water", "telecoms",
            "energy", "transport", "logistics", "stores", "specialists", "groceries", "restaurants", "leisure", "media", "hardware", "software", "healthcare", "industry",
            "goods", "services", "trusts");

        public static readonly string[] SizeKeys = Keys(P + "_mkt_size_", "large", "mid", "small");

        private static string[] Keys(string prefix, params string[] names)
        {
            var keys = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                keys[i] = prefix + names[i];
            }
            return keys;
        }

        private const MarketSector Bk = MarketSector.Banks, In = MarketSector.Insurance, Fi = MarketSector.Finance, Pr = MarketSector.Property,
            Cn = MarketSector.Construction, Pw = MarketSector.Power, Wa = MarketSector.Water, Tc = MarketSector.Telecoms, En = MarketSector.Energy,
            Tr = MarketSector.Transport, Lg = MarketSector.Logistics, St = MarketSector.Stores, Sp = MarketSector.Specialists, Gr = MarketSector.Groceries,
            Rs = MarketSector.Restaurants, Le = MarketSector.Leisure, Md = MarketSector.Media, Hw = MarketSector.Hardware, Sw = MarketSector.Software,
            Hc = MarketSector.Healthcare, Id = MarketSector.Industry, Gd = MarketSector.Goods, Sv = MarketSector.Services, Pt = MarketSector.Trusts;
        private const MarketSize L = MarketSize.Large, M = MarketSize.Mid, S = MarketSize.Small;
        private const int Week = TrustWeekly, Third = TrustThreeDays;

        /// <summary>Every company there is; names are invented. A new company is one more row with an id never used before.</summary>
        public static readonly CompanyDef[] All =
        {
            new CompanyDef(1, "Merchants Union Bank", "MUB", Bk, L, 9000, 4800, "bank"),
            new CompanyDef(2, "Dockside Savings", "DOCK", Bk, M, 1800, 2200, null),
            new CompanyDef(3, "Harbour City Bancorp", "HCB", Bk, L, 14000, 6100, null),
            new CompanyDef(4, "Northfield Savings and Loan", "NFSL", Bk, M, 2400, 2700, null),
            new CompanyDef(5, "Granite Row Bank", "GRB", Bk, M, 3800, 3500, null),
            new CompanyDef(6, "Lanternhill Bank", "LHB", Bk, S, 600, 1400, null),
            new CompanyDef(7, "Old Quay Trust", "OQT", Bk, M, 1500, 8200, null),
            new CompanyDef(8, "Copperbeech Mortgage Bank", "CBMB", Bk, S, 450, 950, null),
            new CompanyDef(9, "Saltmarsh Insurance Group", "SIG", In, L, 12000, 5400, null),
            new CompanyDef(10, "Pebblecourt Assurance", "PCA", In, M, 4200, 3800, null),
            new CompanyDef(11, "Thistledown Life", "TDL", In, M, 3100, 1900, null),
            new CompanyDef(12, "Rookhaven Re", "RKH", In, M, 2600, 11200, null),
            new CompanyDef(13, "Cinderwell General Insurance", "CWG", In, S, 800, 1600, null),
            new CompanyDef(14, "Ivygate Health Cover", "IVY", In, S, 950, 2400, null),
            new CompanyDef(15, "City Exchange Group", "CXG", Fi, M, 1200, 14500, null),
            new CompanyDef(16, "Quillfield Asset Management", "QAM", Fi, M, 900, 3300, null),
            new CompanyDef(17, "Ledgerline Payments", "LDP", Fi, M, 1600, 8800, null),
            new CompanyDef(18, "Brackenridge Brokers", "BRB", Fi, S, 300, 1200, null),
            new CompanyDef(19, "Umberfield Capital", "UMB", Fi, S, 250, 780, null),
            new CompanyDef(20, "Crown Square Estates", "CSQ", Pr, L, 2200, 4100, null),
            new CompanyDef(21, "Eastbank Land", "EBL", Pr, M, 700, 1800, null),
            new CompanyDef(22, "Larchwood Property Trust", "LPT", Pr, M, 900, 2600, null),
            new CompanyDef(23, "Marrowgate Offices", "MGO", Pr, M, 650, 3100, null),
            new CompanyDef(24, "Vellum Street Retail Parks", "VSR", Pr, S, 380, 1100, null),
            new CompanyDef(25, "Kestrelmoor Homes", "KMH", Pr, M, 1100, 4400, null),
            new CompanyDef(26, "Fernhollow Storage", "FHS", Pr, S, 300, 5800, null),
            new CompanyDef(27, "Hollowmere Residential", "HMR", Pr, S, 420, 2100, null),
            new CompanyDef(28, "Gorsebank Construction", "GBC", Cn, L, 11000, 3600, null),
            new CompanyDef(29, "Ashfold Builders", "ASB", Cn, M, 3200, 2300, "interiorinstallation"),
            new CompanyDef(30, "Nettleford Cement", "NFC", Cn, M, 2100, 6700, null),
            new CompanyDef(31, "Yarrowbrook Timber", "YBT", Cn, S, 700, 1300, null),
            new CompanyDef(32, "Ironbridge Civil Works", "IBW", Cn, M, 2700, 2900, null),
            new CompanyDef(33, "Pantile Roofing", "PTR", Cn, S, 260, 840, null),
            new CompanyDef(34, "Citywide Light and Power", "CLP", Pw, L, 9500, 5200, null),
            new CompanyDef(35, "Emberline Power", "EMB", Pw, M, 3400, 3900, null),
            new CompanyDef(36, "Gridstone Electric", "GSE", Pw, M, 2800, 4700, null),
            new CompanyDef(37, "Tidewell Renewables", "TWR", Pw, S, 620, 1700, null),
            new CompanyDef(38, "Voltyard Generation", "VYG", Pw, S, 540, 2600, null),
            new CompanyDef(39, "Riverside Water", "RVW", Wa, M, 1300, 3400, null),
            new CompanyDef(40, "Clearbrook Water", "CBW", Wa, M, 900, 2800, null),
            new CompanyDef(41, "Hearthgas Distribution", "HGD", Wa, M, 1700, 4500, null),
            new CompanyDef(42, "Brookmere Water", "BMR", Wa, S, 380, 2200, null),
            new CompanyDef(43, "Signalhill Telecom", "SHT", Tc, L, 16000, 2700, null),
            new CompanyDef(44, "Wirecastle Broadband", "WCB", Tc, M, 3600, 3300, null),
            new CompanyDef(45, "Fibreway Networks", "FWN", Tc, M, 1400, 4100, null),
            new CompanyDef(46, "Townline Cable", "TNL", Tc, S, 700, 1500, null),
            new CompanyDef(47, "Pocketline Mobile", "PLM", Tc, M, 2900, 1900, null),
            new CompanyDef(48, "Harbour Point Fuels", "HPF", En, L, 22000, 7100, null),
            new CompanyDef(49, "Deepwell Oil and Gas", "DWO", En, M, 6100, 4400, null),
            new CompanyDef(50, "Fuelwell Stations", "FWS", En, M, 4800, 2600, "gasstation"),
            new CompanyDef(51, "Coalbrook Energy", "CBE", En, S, 900, 900, null),
            new CompanyDef(52, "Metro Line Carriers", "MLC", Tr, M, 2100, 2400, null),
            new CompanyDef(53, "Crosstown Coaches", "CTC", Tr, S, 500, 1200, null),
            new CompanyDef(54, "Kerbside Cabs", "KBC", Tr, S, 420, 980, null),
            new CompanyDef(55, "Eastquay Ferries", "EQF", Tr, S, 350, 1700, null),
            new CompanyDef(56, "Highwing Airways", "HWA", Tr, L, 13000, 3100, "travelagency"),
            new CompanyDef(57, "Ringline Rail", "RLR", Tr, M, 3300, 3800, null),
            new CompanyDef(58, "Harbourside Freight", "HSF", Lg, M, 2600, 2900, "movingservice"),
            new CompanyDef(59, "Packhorse Couriers", "PHC", Lg, M, 3900, 4600, "truckgarage"),
            new CompanyDef(60, "Dockyard Warehousing", "DYW", Lg, M, 1200, 5200, "warehouse"),
            new CompanyDef(61, "Milestone Haulage", "MSH", Lg, S, 800, 1400, null),
            new CompanyDef(62, "Doorstep Parcels", "DSP", Lg, M, 2200, 6300, null),
            new CompanyDef(63, "Container Row Shipping", "CRS", Lg, M, 4100, 3700, "importexport"),
            new CompanyDef(64, "Fairlane Stores", "FLS", St, L, 15000, 4300, "giftshop"),
            new CompanyDef(65, "Corner Row Stores", "CRW", St, M, 3800, 2100, "liquorstore"),
            new CompanyDef(66, "Arcade Row Department Stores", "ARD", St, M, 5200, 3400, "clothing"),
            new CompanyDef(67, "Bargain Lane Discount", "BLD", St, M, 6400, 1800, "wholesale"),
            new CompanyDef(68, "High Gate Emporium", "HGE", St, S, 900, 2700, "appliance"),
            new CompanyDef(69, "Tailor Row Clothing", "TRC", Sp, M, 2800, 3900, "clothing"),
            new CompanyDef(70, "Circuit Square Electronics", "CSE", Sp, M, 4300, 2500, "electronics"),
            new CompanyDef(71, "Parlour Lane Furnishings", "PLF", Sp, S, 900, 1600, "furniture"),
            new CompanyDef(72, "Glint Jewellers", "GLJ", Sp, S, 650, 7300, "jewelry"),
            new CompanyDef(73, "Pageturner Books", "PTB", Sp, S, 300, 1100, "bookstore"),
            new CompanyDef(74, "Uphill Sports", "UPH", Sp, S, 700, 2200, null),
            new CompanyDef(75, "Greenmarket Foods", "GMF", Gr, L, 18000, 3700, "supermarket"),
            new CompanyDef(76, "Freshcrate Grocers", "FCG", Gr, M, 6300, 2400, "supermarket"),
            new CompanyDef(77, "Orchard Row Produce", "ORP", Gr, S, 800, 1300, "fruitandvegetable"),
            new CompanyDef(78, "Millstone Bakeries", "MSB", Gr, M, 1500, 2900, null),
            new CompanyDef(79, "Dairyvale Farms", "DVF", Gr, M, 2200, 3100, null),
            new CompanyDef(80, "Sheaf and Furrow Foods", "SFF", Gr, M, 3500, 4200, null),
            new CompanyDef(81, "Corner Basket Supermarkets", "CBK", Gr, M, 4700, 1900, "supermarket"),
            new CompanyDef(82, "Third Street Kitchens", "TSK", Rs, M, 1600, 2600, "fastfood"),
            new CompanyDef(83, "Steamline Coffee", "STC", Rs, M, 2300, 5400, "coffeeshop"),
            new CompanyDef(84, "Charcoal Lane Grills", "CLG", Rs, S, 600, 1900, "fastfood"),
            new CompanyDef(85, "Noodle Row", "NDR", Rs, S, 420, 1500, "fastfood"),
            new CompanyDef(86, "Crumb and Crust Bakeries", "CCB", Rs, S, 380, 2300, "coffeeshop"),
            new CompanyDef(87, "Bistro Quarter", "BQR", Rs, S, 520, 3100, "fastfood"),
            new CompanyDef(88, "Bunline Burgers", "BLB", Rs, M, 1900, 4100, "fastfood"),
            new CompanyDef(89, "Night Row Venues", "NRV", Le, M, 1100, 3300, "nightclub"),
            new CompanyDef(90, "Dovemere Hotels", "DMH", Le, M, 2900, 4800, null),
            new CompanyDef(91, "Barbell Row Fitness", "BRF", Le, S, 600, 1700, "gym"),
            new CompanyDef(92, "Reelhouse Cinemas", "RHC", Le, M, 1200, 2100, "cinema"),
            new CompanyDef(93, "Lucky Quay Casino", "LQC", Le, M, 1500, 3600, "casino"),
            new CompanyDef(94, "Tenpin Alley Bowling", "TAB", Le, S, 250, 920, null),
            new CompanyDef(95, "Bathhouse Wellness", "BHW", Le, S, 330, 1800, "hairdresser"),
            new CompanyDef(96, "Marquee Pictures", "MQP", Md, M, 2400, 2800, "theater"),
            new CompanyDef(97, "Ledger Press Group", "LPG", Md, M, 800, 1600, null),
            new CompanyDef(98, "Cityview Broadcasting", "CVB", Md, M, 3100, 3500, null),
            new CompanyDef(99, "Gramophone Row Records", "GRR", Md, S, 400, 1200, null),
            new CompanyDef(100, "Hoarding Street Advertising", "HSA", Md, M, 1300, 2700, "marketingagency"),
            new CompanyDef(101, "Circuitry Works", "CTW", Hw, L, 12000, 9600, null),
            new CompanyDef(102, "Prism Lane Optics", "PLO", Hw, M, 1800, 5700, null),
            new CompanyDef(103, "Quartzline Semiconductors", "QZS", Hw, L, 9000, 13800, null),
            new CompanyDef(104, "Pixelpoint Displays", "PXD", Hw, M, 2600, 3400, null),
            new CompanyDef(105, "Gearwheel Robotics", "GWR", Hw, S, 700, 4800, null),
            new CompanyDef(106, "Codeharbour Software", "CHS", Sw, L, 7500, 18200, "webdevelopment"),
            new CompanyDef(107, "Skyvault Systems", "SKV", Sw, M, 2800, 11900, null),
            new CompanyDef(108, "Abacus Lane Software", "ALS", Sw, M, 1200, 6400, null),
            new CompanyDef(109, "Shopfront Online", "SFO", Sw, M, 3300, 7700, null),
            new CompanyDef(110, "Pixel Harbour Games", "PHG", Sw, M, 1500, 5200, null),
            new CompanyDef(111, "Tallyrow Analytics", "TRA", Sw, S, 420, 3900, null),
            new CompanyDef(112, "Signpost Search", "SPS", Sw, L, 9800, 15400, "graphicdesign"),
            new CompanyDef(113, "Willowmere Hospitals", "WMH", Hc, L, 8800, 6300, "hospital"),
            new CompanyDef(114, "Remedy Row Pharmacies", "RRP", Hc, M, 4200, 2900, null),
            new CompanyDef(115, "Larkspur Pharmaceuticals", "LKP", Hc, L, 11000, 8700, null),
            new CompanyDef(116, "Kindhands Care Homes", "KCH", Hc, S, 700, 1400, null),
            new CompanyDef(117, "Steadyhand Medical Devices", "SMD", Hc, M, 1900, 7100, null),
            new CompanyDef(118, "Molar Lane Dental", "MLD", Hc, S, 330, 1900, "clinic"),
            new CompanyDef(119, "Anvil Row Engineering", "ARE", Id, M, 3700, 4500, null),
            new CompanyDef(120, "Brasswork Industries", "BWI", Id, M, 2900, 3200, "factory"),
            new CompanyDef(121, "Kilnworks Ceramics", "KWC", Id, S, 500, 1800, null),
            new CompanyDef(122, "Platewright Steel", "PWS", Id, L, 8600, 2700, null),
            new CompanyDef(123, "Boxwright Packaging", "BXP", Id, M, 2300, 3900, null),
            new CompanyDef(124, "Spindle Textiles", "SPT", Id, S, 600, 1100, null),
            new CompanyDef(125, "Suds Lane Household", "SLH", Gd, L, 9400, 5800, null),
            new CompanyDef(126, "Bottlecap Beverages", "BCB", Gd, L, 7800, 4900, null),
            new CompanyDef(127, "Hopyard Brewing", "HYB", Gd, M, 2100, 3300, null),
            new CompanyDef(128, "Rosewater Cosmetics", "RWC", Gd, M, 1800, 4400, null),
            new CompanyDef(129, "Crispbake Snacks", "CRB", Gd, M, 2600, 2700, null),
            new CompanyDef(130, "Thimble Apparel", "THA", Gd, M, 1600, 3600, null),
            new CompanyDef(131, "Watchfield Security", "WFS", Sv, M, 1700, 3100, null),
            new CompanyDef(132, "Ironward Alarms", "IWA", Sv, S, 600, 1900, null),
            new CompanyDef(133, "Brightbroom Cleaning", "BRM", Sv, S, 450, 1200, null),
            new CompanyDef(134, "Hiring Hall Recruitment", "HHR", Sv, M, 2300, 2600, "recruitment"),
            new CompanyDef(135, "Counsel Row Consulting", "CRC", Sv, M, 1400, 5300, "lawfirm"),
            new CompanyDef(136, "Paperclip Office Supplies", "POS", Sv, S, 800, 1500, "officesupply"),
            // The property trusts
            new CompanyDef(137, "Cobblegate Property Trust", "CPT", Pt, L, 1600, 3400, null, false, Week),
            new CompanyDef(138, "Hearthstone Homes Trust", "HHT", Pt, M, 700, 2600, null, false, Week),
            new CompanyDef(139, "Wharfside Warehouse Trust", "WWT", Pt, M, 550, 2200, null, false, Third),
            new CompanyDef(140, "Brambleway Retail Trust", "BRT", Pt, M, 600, 1900, null, false, Week),
            new CompanyDef(141, "Mossbank Residential Trust", "MRT", Pt, S, 280, 1500, null, false, Third),
            new CompanyDef(142, "Gullpoint Hotel Trust", "GHT", Pt, S, 320, 1700, null, false, Third),
            // The rival tycoons' holdings, listed only once their tycoon comes forward (MarketEngine.ListHolding)
            new CompanyDef(143, "Karen Aerospace & Motors", "KRN", Pr, M, 1400, 3800, null, true, 0, true),
            new CompanyDef(144, "Luthor Everything Co.", "LUTH", St, M, 1800, 2900, null, true, 0, true),
            new CompanyDef(145, "Zucc Social Holdings", "ZUCC", Fi, M, 1100, 5200, null, true, 0, true),
            new CompanyDef(201, "Seedling Biotech", "SEED", Hc, S, 120, 1800, null, true),
            new CompanyDef(202, "Rooftop Solar Works", "RSW", Pw, S, 200, 1400, null, true),
            new CompanyDef(203, "Quickcart Delivery", "QCD", Lg, S, 300, 2100, null, true),
            new CompanyDef(204, "Brightpath Learning", "BPL", Sv, S, 180, 1600, "eventplanning", true),
            new CompanyDef(205, "Moonlit Studios", "MLS", Sw, S, 150, 2400, null, true),
            new CompanyDef(206, "Stacked Acre Farms", "SAF", Gr, S, 140, 1200, "florist", true),
            new CompanyDef(207, "Voltlane Motors", "VLM", Id, S, 400, 2800, "cardealership", true),
            new CompanyDef(208, "Pantry Box Meal Kits", "PBX", Rs, S, 220, 1500, null, true),
            new CompanyDef(209, "Tinker Lane Toys", "TLT", Gd, S, 260, 1700, null, true),
            new CompanyDef(210, "Fernway Clinics", "FWC", Hc, S, 310, 2200, null, true),
            new CompanyDef(211, "Cobble Lane Coffee Roasters", "CLCR", Gd, S, 160, 1300, null, true),
            new CompanyDef(212, "Overcast Hosting", "OVH", Sw, S, 230, 3100, null, true),
        };

        /// <summary>The game's own named rivals as holding companies: up to four, in the order the game names them.</summary>
        public static readonly CompanyDef[] RivalTemplates =
        {
            new CompanyDef(901, string.Empty, "RVA", St, M, 1500, 4000, null),
            new CompanyDef(902, string.Empty, "RVB", Rs, M, 1200, 3500, null),
            new CompanyDef(903, string.Empty, "RVC", Le, M, 1000, 3000, null),
            new CompanyDef(904, string.Empty, "RVD", Pr, M, 800, 2500, null)
        };

        /// <summary>The Ventures market's companies by [sector][name] to their ids here.</summary>
        public static readonly int[][] VenturesMap =
        {
            new[] { 52, 58 }, new[] { 20, 21 }, new[] { 64, 65 }, new[] { 75, 82 },
            new[] { 131, 132 }, new[] { 34, 39 }, new[] { 1, 2 }, new[] { 96, 89 }
        };

        static MarketCatalogue()
        {
            for (int i = 0; i < All.Length; i++)
            {
                Profile(All[i]);
            }
            for (int i = 0; i < RivalTemplates.Length; i++)
            {
                Profile(RivalTemplates[i]);
            }
        }

        /// <summary>A company's catalogue row by id; null for none (a rival holding is its template).</summary>
        public static CompanyDef Find(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            for (int i = 0; i < RivalTemplates.Length; i++)
            {
                if (RivalTemplates[i].Id == id)
                {
                    return RivalTemplates[i];
                }
            }
            return null;
        }

        public static bool IsRival(int id)
        {
            return id >= 900 && id < 1000;
        }

        /// <summary>
        /// A player's company as a catalogue row: small, its sector's figures moved by its id, but none of the cycle or
        /// inflation made up for it (its books carry those), and its debt its own.
        /// </summary>
        public static CompanyDef PlayerDef(int id, string name, string ticker, MarketSector sector, int debtBp, string trade)
        {
            var d = new CompanyDef(id, string.IsNullOrEmpty(name) ? ticker : name, ticker, sector, MarketSize.Small, 0, 0, trade);
            Profile(d);
            d.Cyclical = 0;
            d.PassPct = 100;
            d.DebtPct = Math.Max(0, Math.Min(300, (debtBp + 50) / 100));
            return d;
        }

        private static void Profile(CompanyDef d)
        {
            SectorProfile s = Sectors[(int)d.Sector];
            int size = (int)d.Size;
            d.MarginBp = Vary(s.MarginBp, d.Id, 1, 25);
            d.GrowthBp = s.GrowthBp + (Vary(1000, d.Id, 2, 60) - 1000) * Math.Max(150, s.GrowthBp) / 1000 + (size == 2 ? 100 : size == 0 ? -50 : 0);
            d.Beta = Vary(s.Beta, d.Id, 3, 15);
            d.Cyclical = Vary(s.Cyclical, d.Id, 4, 20);
            d.DebtPct = Vary(s.DebtPct, d.Id, 6, 35);
            d.IdioBp = Vary(s.IdioBp, d.Id, 7, 15) * (size == 0 ? 80 : size == 1 ? 95 : 120) / 100;
            d.PassPct = Math.Min(110, Vary(s.PassPct, d.Id, 8, 10));
            d.RateBp = s.RateBp;
            d.TurnoverBp = Vary(TurnoverBp[size], d.Id, 9, 25);
            d.SpreadBp = Vary(SpreadBp[size], d.Id, 10, 30);
        }

        // A figure moved by up to so many per cent either way, the same for the same id and purpose.
        private static int Vary(int value, int id, int purpose, int percent)
        {
            uint h = MarketRandom.Mix32((uint)id * 2654435761u ^ (uint)purpose * 40503u);
            int pick = (int)(h % (uint)(2 * percent + 1)) - percent;
            return value + value * pick / 100;
        }

        /// <summary>
        /// How much more every price moves in a day of a short game year, so a year's risk stays near a real one's: the fifth
        /// root of how much shorter the year is, 1.43 in a 60-day year.
        /// </summary>
        public static double VolScale(int year)
        {
            double scale = Math.Pow(260.0 / TradingDaysPerYear(year), 0.2);
            return scale < 1.0 ? 1.0 : scale > 1.5 ? 1.5 : scale;
        }

        /// <summary>0 Monday to 6 Sunday; day 1 is a Monday.</summary>
        public static int Weekday(int day)
        {
            return ((day - 1) % 7 + 7) % 7;
        }

        public static bool TradingDay(int day)
        {
            return Weekday(day) < 5;
        }

        public static double TradingDaysPerYear(int year)
        {
            return Math.Max(8, year) * 5.0 / 7.0;
        }

        /// <summary>The quarter a day is in, counted from the game's start; days before it fall in quarter -1 and down.</summary>
        public static int QuarterOf(int day, int year)
        {
            year = Math.Max(8, year);
            int y = FloorDiv(day - 1, year);
            int inYear = day - 1 - y * year;
            return y * 4 + inYear * 4 / year;
        }

        public static int QuarterStart(int quarter, int year)
        {
            year = Math.Max(8, year);
            int y = FloorDiv(quarter, 4), q = quarter - y * 4;
            return y * year + (q * year + 3) / 4 + 1;
        }

        /// <summary>The month (a twelfth of the year) a day is in, counted from the game's start.</summary>
        public static int MonthOf(int day, int year)
        {
            year = Math.Max(8, year);
            int y = FloorDiv(day - 1, year);
            int inYear = day - 1 - y * year;
            return y * 12 + inYear * 12 / year;
        }

        public static int YearOf(int day, int year)
        {
            return FloorDiv(day - 1, Math.Max(8, year));
        }

        /// <summary>The week a day is in, Monday to Sunday, counted from the start of the game.</summary>
        public static int WeekOf(int day)
        {
            return FloorDiv(day - 1, 7);
        }

        /// <summary>A whole division rounded down, below nothing too: -1 / 7 is -1.</summary>
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }

        /// <summary>The game minute of the day at which a phase of a market day is reached (see <see cref="PhaseOpen"/>).</summary>
        public static int MinuteOfPhase(int phase)
        {
            if (phase <= PhaseNone)
            {
                return 0;
            }
            if (phase == PhaseResults)
            {
                return ResultsMinute;
            }
            if (phase == PhaseOpen)
            {
                return OpenMinute;
            }
            return Math.Min(CloseMinute, OpenMinute + (phase - PhaseOpen) * StepMinutes);
        }

        public static int PhaseAt(int minute)
        {
            if (minute < ResultsMinute)
            {
                return PhaseNone;
            }
            if (minute < OpenMinute)
            {
                return PhaseResults;
            }
            if (minute >= CloseMinute)
            {
                return PhaseClose;   // the last step and the close together
            }
            return PhaseOpen + (minute - OpenMinute) / StepMinutes;
        }

        public static bool OpenAt(int day, int minute)
        {
            return TradingDay(day) && minute >= OpenMinute && minute < CloseMinute;
        }
    }
}
