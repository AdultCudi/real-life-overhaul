namespace RealLifeOverhaul.Outlook
{
    /// <summary>The Economy app's tabs, in the order they stand.</summary>
    public static class OutlookTab
    {
        public const int Overview = 0, Prices = 1, Jobs = 2, Districts = 3, Markets = 4, Finances = 5, Insights = 6;
        public const int Count = 7;

        public static readonly string[] Keys =
        {
            "reallife_econapp_tab_overview", "reallife_econapp_tab_prices", "reallife_econapp_tab_jobs", "reallife_econapp_tab_districts",
            "reallife_econapp_tab_markets", "reallife_econapp_tab_finances", "reallife_econapp_tab_insights"
        };

        public static int Clamp(int tab)
        {
            return tab < 0 ? 0 : tab >= Count ? Count - 1 : tab;
        }
    }

    /// <summary>How a figure's change reads for a typical player: green, red, or neither (a figure that is good news to an owner and bad to a tenant).</summary>
    public enum OutlookTone
    {
        Flat = 0,
        Good = 1,
        Bad = 2,
        Neutral = 3
    }

    /// <summary>How a change is written: a per cent of the figure, or the figure's own points.</summary>
    public enum OutlookChange
    {
        Percent = 0,
        Points = 1
    }

    /// <summary>The state of the economy in one word.</summary>
    public enum OutlookVerdict
    {
        Growing = 0,
        Steady = 1,
        Slowing = 2,
        Overheating = 3,
        Recession = 4
    }

    /// <summary>One of the headline tiles on the Overview: the figure it shows, how its change reads, and the tab it leads to.</summary>
    public sealed class OutlookTile
    {
        public readonly string Id;
        public readonly string Series;       // the book's name for its history
        public readonly string LabelKey;
        public readonly OutlookChange Kind;
        public readonly int Good;            // +1: a rise is good news, -1: a rise is bad news, 0: neither
        public readonly int Tab;

        public OutlookTile(string id, string series, string labelKey, OutlookChange kind, int good, int tab)
        {
            Id = id;
            Series = series;
            LabelKey = labelKey;
            Kind = kind;
            Good = good;
            Tab = tab;
        }
    }

    /// <summary>
    /// Every number of the Economy app, game-free (EconomyCheck's OutlookChecks): how long the history is, the names of the
    /// series it keeps, the headline tiles and the thresholds the verdict and the insights are read by. A threshold is in the
    /// units of the figure it judges, which the city's economy keeps in whole numbers (MacroCatalogue): prices in thousandths,
    /// inflation in hundredths of a per cent a year, the bank rate in quarter points, an income in tenths of a point.
    /// </summary>
    public static class OutlookCatalogue
    {
        public const string Key = "reallife:economy_app";     // the save's one line
        public const int Version = 1;
        public const int HistoryDays = 120;                    // days of each figure kept: two of the game's 60-day years
        public const int ShortDays = 7;                        // the short change on a tile
        public const int LongDays = 30;                        // ...and the long one
        public const int LinesMost = 72;                       // figures the book keeps: the national ones, then a few for each district
        public const int NoValue = int.MinValue;               // a day with no reading

        // The national figures, by the name the save keeps them under (never changed).
        public const string Prices = "prices";                 // the price index, thousandths
        public const string Inflation = "infl";                // hundredths of a per cent a year
        public const string Rate = "rate";                     // the bank rate on top of the banks' own, quarter points
        public const string Cycle = "cycle";                   // 0 boom to 3 recession
        public const string Income = "income";                 // the districts' mean income level, tenths of a point
        public const string Spend = "spend";                   // what a customer spends against the usual, thousandths
        public const string Footfall = "foot";                 // the cycle's change to customers, per cent
        public const string Property = "prop";                 // what property is worth against the game's figure, thousandths (the mean of the districts)
        public const string Reserve = "res";                   // the council's reserve, thousands of money
        public const string Revenue = "rev";                   // ...a week's takings, thousands
        public const string Spending = "spd";                  // ...a week's spending, thousands
        public const string Growing = "grow";                  // buildings going up in the mod's own districts
        public const string Unemployment = "unemp";            // unemployment among professionals, per mille (Tech/Hiring's table, by the cycle)
        public const string Vacancies = "vac";                 // the vacancies the city has open at once (Careers' table, by the cycle)

        // By district: the name followed by the district's id (and a use's number for demand).
        public const string DistrictIncome = "di:";
        public const string DistrictLevel = "dl:";
        public const string DistrictDemand = "dd:";

        public static string OfDistrictIncome(string district) => DistrictIncome + Clean(district);
        public static string OfDistrictLevel(string district) => DistrictLevel + Clean(district);
        public static string OfDistrictDemand(string district, int use) => DistrictDemand + use + ":" + Clean(district);

        /// <summary>A name with the dividers of the save's line taken out, so a district's id can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', '_').Replace(';', '_').Replace('~', '_').Replace(',', '_').Replace('=', '_');
        }

        public const int TilePrices = 0, TileInflation = 1, TileRate = 2, TileUnemployment = 3, TileIncome = 4, TileProperty = 5, TileStocks = 6, TileReserve = 7;

        /// <summary>The headline tiles on the Overview, in their order.</summary>
        public static readonly OutlookTile[] Tiles =
        {
            new OutlookTile("prices", Prices, "reallife_econapp_tile_prices", OutlookChange.Percent, -1, OutlookTab.Prices),
            new OutlookTile("inflation", Inflation, "reallife_econapp_tile_inflation", OutlookChange.Points, -1, OutlookTab.Prices),
            new OutlookTile("rate", Rate, "reallife_econapp_tile_rate", OutlookChange.Points, -1, OutlookTab.Prices),
            new OutlookTile("unemployment", Unemployment, "reallife_econapp_tile_unemployment", OutlookChange.Points, -1, OutlookTab.Jobs),
            new OutlookTile("income", Income, "reallife_econapp_tile_income", OutlookChange.Points, 1, OutlookTab.Districts),
            new OutlookTile("property", Property, "reallife_econapp_tile_property", OutlookChange.Percent, 1, OutlookTab.Districts),
            new OutlookTile("stocks", null, "reallife_econapp_tile_stocks", OutlookChange.Percent, 1, OutlookTab.Markets),
            new OutlookTile("reserve", Reserve, "reallife_econapp_tile_reserve", OutlookChange.Percent, 1, OutlookTab.Finances),
        };

        // ------------------------------------------------------------------ the verdict

        public const int HotGap = 150;             // hundredths: inflation this far over the 2% target (3.5% a year) is overheating
        public const int IncomeFalls = -30;        // tenths: a steady economy whose districts lost this much income in 30 days is slowing...
        public const int IncomeRises = 20;         // ...and one that gained this much is growing
        public const int StocksFall = -800;        // basis points over 30 days: a steady economy whose shares fell this far is slowing...
        public const int StocksRise = 500;         // ...and one whose shares rose this far is growing

        // ------------------------------------------------------------------ the insights

        public const int InsightsMost = 24;        // pieces of advice kept for the Insights tab
        public const int InsightsOnOverview = 3;   // ...and shown on the Overview
        public const int InflationHigh = 350;      // hundredths: 3.5% a year or more is high, as far over the 2% target as overheating is (HotGap)
        public const int InflationLow = 50;        // ...and 0.5% or less is low, as far under it
        public const int JobsTight = 40;           // per mille: unemployment at or under 4.0% is low
        public const int JobsSlack = 70;           // ...and at or over 7.0% is high
        public const int WorthMoves = 300;         // basis points of property's worth over 30 days that are worth saying
        public const int IncomeMoves = 20;         // tenths of a point of a district's income over 30 days that are worth saying
        public const int StocksMove = 500;         // basis points of the index over 30 days
        public const int DemandHigh = 70;          // a use's demand out of 100
        public const int DemandLow = 25;
        public const int BuildingsMany = 3;        // buildings going up in a district
        public const int RateAheadDays = 3;        // a decision announced this close is news
        public const int ShockPercent = 15;        // a shortage or glut of this many per cent or more is worth a line
    }
}
