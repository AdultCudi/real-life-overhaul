using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Where the city's economy stands. Saved as a number: never renumber.</summary>
    public enum CycleState
    {
        Boom = 0,
        Steady = 1,
        Slowdown = 2,
        Recession = 3
    }

    /// <summary>What is out of the ordinary with the supply of a kind of goods. Saved as a number.</summary>
    public enum SupplyShock
    {
        None = 0,
        Shortage = 1,
        Glut = 2
    }

    /// <summary>Why the central bank moved its rate, or did not. Saved as a number.</summary>
    public enum RateReason
    {
        Hold = 0,
        PricesHigh = 1,     // inflation is over the target
        PricesLow = 2,      // inflation is under the target
        Slowdown = 3,
        Recession = 4,
        Normal = 5          // nothing presses: the rate goes back towards the banks' own
    }

    /// <summary>The weather as supply sees it; Wiring turns the mod's weather into this.</summary>
    public enum CityWeather
    {
        Fair = 0,
        Storm = 1,
        Heat = 2,
        Cold = 3,
        Ice = 4
    }

    /// <summary>A city event as supply sees it; Wiring turns the mod's calendar into this.</summary>
    public enum CityEvent
    {
        None = 0,
        Market = 1,
        FarmersMarket = 2,
        Parade = 3,
        Protest = 4,
        FunRun = 5,
        FilmShoot = 6,
        Fireworks = 7
    }

    /// <summary>What kind of piece the city's economy has for the paper. Saved as a number.</summary>
    public enum CityNewsKind
    {
        Supply = 0,         // a shortage or a glut from tomorrow: A the kind of goods, B the shock, C its days, D its size in per cent
        RateDecision = 1,   // the rate moves tomorrow: A the move in quarter points, B the reason
        Cycle = 2,          // the economy changed state: A the new state, B the old one
        Prices = 3,         // the month's prices: A the index in thousandths, B the month's change in hundredths of a per cent
        District = 4,       // a district noticeably up or down: A its change in tenths of a point, B its income in tenths, Name its name
        Rival = 5           // a rival has opened beside a business that did well: Name the business
    }

    /// <summary>One piece for the paper, for one day.</summary>
    public struct CityNews
    {
        public int Day;
        public CityNewsKind Kind;
        public int A, B, C, D;
        public string Name;
    }

    /// <summary>
    /// Every number of the city's economy, game-free. Whole numbers, so a seed always gives the same history: the rate in
    /// quarter points, inflation in hundredths of a per cent a year, an income in tenths of a point, the index and factors in
    /// thousandths.
    /// </summary>
    public static class MacroCatalogue
    {
        // The option's four steps, as the presets set them.
        public const int Relaxed = 0, Standard = 1, Realistic = 2, Hardcore = 3;
        public const int LevelDefault = Standard;

        /// <summary>How wide every swing is at each step, in per cent of the figures below.</summary>
        public static readonly int[] SwingPercent = { 50, 100, 110, 125 };

        public static int Level(int level)
        {
            return level < Relaxed ? Relaxed : level > Hardcore ? Hardcore : level;
        }

        public static int Swing(int level)
        {
            return SwingPercent[Level(level)];
        }

        /// <summary>Relaxed never enters a recession.</summary>
        public static bool Recessions(int level)
        {
            return Level(level) != Relaxed;
        }

        public const int MonthsPerYear = 12;
        public const int MonthDaysLeast = 3;        // game days

        /// <summary>A month in game days: a twelfth of the game's year, three days at least.</summary>
        public static int MonthDays(int daysPerYear)
        {
            return Math.Max(MonthDaysLeast, daysPerYear / MonthsPerYear);
        }

        // The central bank: quarter points on top of the banks' own rates.
        public const int QuartersPerPoint = 4;
        public const int BaseStart = 0;
        public const int BaseLeast = -8;            // -2 points
        public const int BaseMost = 24;             // +6 points
        public const int MoveMost = 4;              // a decision moves the rate by 0.25 to 1 point, or holds it
        public const int MovePerGapPoint = 1;       // a quarter point for each whole point inflation is away from the target (one at least)
        public const int MoveSlowdown = -1;         // a slowdown takes a quarter point off the decision...
        public const int MoveRecession = -2;        // ...a recession half a point
        public const int MoveNormal = 1;            // with nothing pressing, a quarter point back towards the banks' own

        // Prices: hundredths of a per cent a year; the index in thousandths.
        public const int InflationTarget = 200;     // 2% a year
        public const int InflationBand = 50;        // within half a point of the target is on target
        public const int InflationLeast = -100;     // -1% a year
        public const int InflationMost = 1200;      // +12% a year
        public const int InflationFollow = 50;      // per cent of the way inflation moves each month towards where the month pushes it
        public const int RateEffectPerQuarter = 15; // each quarter point of the rate of the month before last takes 0.15 off where it is pushed
        public static readonly int[] CyclePush = { 120, 0, -80, -160 };   // by state
        public const int ShockLeast = -100, ShockMost = 150;              // the month's own surprise
        public const int PressureLeast = -150, PressureMost = 350;        // a slow pressure that wanders from month to month...
        public const int PressureStep = 60;                               // ...by up to this much either way...
        public const int PressureFade = 10;                               // ...and loses this per cent of itself each month
        public const int IndexStart = 100000;       // 100.0
        public const int IndexLeast = 50000;
        public const int IndexMost = 400000;

        public const int StateCount = 4;
        public const int StateMonthsLeast = 2, StateMonthsMost = 6;
        public const int OpeningSteadyMonths = 3;   // a save's first three months are steady
        public const int TransitionJitter = 8;      // each save moves each row's two chances by up to this many per cent

        // What follows each state, in per cent, before the save's jitter: to Boom, Steady, Slowdown, Recession.
        public static readonly int[][] Transition =
        {
            new[] { 0, 70, 30, 0 },
            new[] { 45, 20, 35, 0 },
            new[] { 0, 65, 0, 35 },
            new[] { 0, 65, 35, 0 }
        };
        // The two outcomes of each row that the jitter moves a share between (the first gains what the second loses).
        public static readonly int[][] JitterPair = { new[] { 1, 2 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 1, 2 } };

        // By state: boom, steady, slowdown, recession.
        public static readonly int[] StateFootfall = { 6, 0, -5, -12 };          // per cent of customers
        public static readonly int[] StateSpend = { 1040, 1000, 970, 930 };      // what a customer spends, in thousandths
        public static readonly int[] StateTrouble = { 850, 1000, 1150, 1350 };   // the chance of the mod's own business troubles, in thousandths
        public static readonly int[] StateProperty = { 1100, 1000, 950, 850 };   // property values, in thousandths
        public static readonly int[] StateUndercut = { 1000, 1000, 1500, 2000 }; // how often a rival undercuts, in thousandths (not moved by the strength)

        // Districts: income in tenths of a point.
        public const int IncomeLeast = 600, IncomeMost = 1400, IncomeMiddle = 1000;
        public const int ClassWorking = 70, ClassMiddle = 100, ClassUpper = 135;   // a district starts at the mean of these by the game's own class shares
        public static readonly int[] IncomeDrift = { 10, 0, -8, -16 };             // a month's push by state
        public const int IncomeNoise = 15;          // and up to this much either way of its own
        public const int IncomePull = 15;           // per cent of the way back to its start each month
        public const int IncomeBand = 200;          // never further than this from its start
        public const int IncomeNews = 20;           // a month's change the paper mentions
        public const int FootfallPerPoint = 100;    // thousandths of a per cent of customers for each point away from 100 (0.1% a point)
        public const int TolerancePerPoint = 5;     // thousandths of price a district bears more (or less) for each point its income is over (or under) its start
        public const int PropertyPerPoint = 4;      // thousandths of value for each point a district is over (or under) its start

        // Prices against what a district bears.
        public const int OverBand = 20;             // thousandths: up to 2% over is not over
        public const int OverLossPerPercent = 800;  // thousandths of a per cent of customers lost for each per cent over (0.8)
        public const int OverLossMost = 10;         // per cent of customers at most
        public const int UnderGain = 2;             // per cent of customers gained for prices at or under what the district bears
        public const int MonopolyAllowance = 300;   // thousandths the game itself lets a monopoly charge more (ItemHelper)

        public const int FootfallLeast = -25, FootfallMost = 15;   // per cent of customers, district, cycle and prices together
        public const int FootfallQuiet = 2;                        // under this either way nothing is changed

        // Supply: price factors in thousandths.
        public const int SupplyKinds = 6;
        public const int Fresh = 0, Groceries = 1, Clothing = 2, Electronics = 3, Home = 4, General = 5;
        public const int FactorLeast = 700, FactorMost = 1500;
        public const int ShockChance = 70;          // thousandths a day that a shortage or a glut is drawn for tomorrow
        public const int ShockShortageShare = 55;   // per cent of them that are shortages
        public const int ShortageLeast = 150, ShortageMost = 400;
        public const int GlutLeast = 80, GlutMost = 240;
        public const int ShockDaysLeast = 3, ShockDaysMost = 8;
        public const int ShocksAtOnce = 2;

        // Which kind a business type's goods are, by a part of the game's name for the type; the first that matches
        // decides, and a product several types sell is of the kind of the first type in this list that sells it.
        public static readonly string[] KindOfType =
        {
            "fruitandvegetable", "florist",
            "liquorstore", "coffeeshop", "fastfood", "nightclub", "cinema", "theater", "supermarket",
            "clothing", "jewelry", "giftshop", "hairdresser",
            "electronics", "appliance",
            "furniture", "officesupply", "bookstore"
        };
        public static readonly int[] KindOfTypeIs =
        {
            Fresh, Fresh,
            Groceries, Groceries, Groceries, Groceries, Groceries, Groceries, Groceries,
            Clothing, Clothing, Clothing, Clothing,
            Electronics, Electronics,
            Home, Home, Home
        };

        /// <summary>Where a business type stands in <see cref="KindOfType"/>; -1 for a type whose goods are general goods.</summary>
        public static int TypeRank(string businessTypeName)
        {
            if (!string.IsNullOrEmpty(businessTypeName))
            {
                for (int i = 0; i < KindOfType.Length; i++)
                {
                    if (businessTypeName.IndexOf(KindOfType[i], StringComparison.Ordinal) >= 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        // A season's push by kind: winter, spring, summer, autumn (the order of the mod's calendar).
        public static readonly int[][] SeasonPush =
        {
            new[] { 80, 0, -60, -20 },
            new[] { 20, 0, 20, -10 },
            new[] { 30, -20, -30, 20 },
            new[] { 40, -10, -20, 0 },
            new[] { -20, 30, 10, 0 },
            new[] { 0, 0, 0, 0 }
        };
        // The weather's push by kind: fair, a storm, a heat wave, a cold snap, ice.
        public static readonly int[][] WeatherPush =
        {
            new[] { 0, 20, 50, 60, 60 },
            new[] { 0, 0, 30, 10, 10 },
            new[] { 0, 0, 0, 10, 10 },
            new[] { 0, 0, 0, 10, 10 },
            new[] { 0, 0, 0, 10, 10 },
            new[] { 0, 0, 0, 10, 10 }
        };
        // A city event's push by kind: none, market, farmers' market, parade, protest, fun run, film shoot, fireworks.
        public static readonly int[][] EventPush =
        {
            new[] { 0, 0, -50, 0, 10, 0, 0, 0 },
            new[] { 0, 0, 0, 20, 10, 20, 0, 20 },
            new[] { 0, -20, 0, 0, 10, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 10, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 10, 0, 0, 0 },
            new[] { 0, -20, 0, 0, 10, 0, 0, 0 }
        };

        public const int AdjustQuiet = PriceRules.Quiet;   // thousandths: within 1% of nothing no rent, wage or takings adjustment is made

        public const int CopycatDays = 14;          // days running a business must have made a profit to draw a rival
        public const int CopycatChance = 60;        // thousandths a day, for the business that made the most
        public const int CopycatRestDays = 21;      // days before the next

        public const int NewsADay = 2;
        public const int NewsKinds = 6;

        /// <summary>Which piece goes first when a day has more than the paper takes: what starts tomorrow, then what touches a business, then the rest.</summary>
        public static int NewsRank(CityNewsKind kind)
        {
            switch (kind)
            {
                case CityNewsKind.Supply: return 0;
                case CityNewsKind.Rival: return 1;
                case CityNewsKind.RateDecision: return 2;
                case CityNewsKind.Cycle: return 3;
                case CityNewsKind.Prices: return 4;
                default: return 5;
            }
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
