namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger's fixed figures and its table of what each kind of piece is: the section it goes in, how much it
    /// matters (the front page leads with the heaviest) and the paper's own headline for the kinds whose words have none.
    /// Every amount of money is at the start's prices and goes through Prices.At when it is charged or shown. Game-free.
    /// </summary>
    public static class LedgerCatalogue
    {
        private const string P = "reallife";   // (apart, so that a key made from parts is not taken for a whole one)

        // ---- what it costs
        public const int CopyAtStart = 2;           // a copy at a kiosk
        public const int WeekAtStart = 9;           // a week of home delivery, paid a week at a time

        // ---- when
        public const int WeekDays = 7;
        public const int DeliveryHour = 6;          // the paper is at the door once the city's paper is written, from 06:00
        public const int KioskOpenHour = 6, KioskCloseHour = 20;

        // ---- the kiosks near the home
        public const int KiosksWanted = 2;
        public const float KioskNearest = 30f;      // metres from the home's door: not on its doorstep
        public const float KioskFurthest = 320f;    // ...and within a few minutes' walk
        public const float KioskApart = 90f;        // between two kiosks
        public const float KioskKeepClear = 7f;     // from a bus stop, a help-wanted board, a bike dock

        // ---- the edition
        public const int ChartDays = 60;            // the index's last closes, for the little chart on the markets page
        public const int MarketNewsDays = 4;        // the market's news from at most this many days back (the first edition after a gap)
        public const int PiecesMost = 60;           // pieces in an edition

        public static readonly string[] SectionKeys =
        {
            P + "_ledger_sec_city", P + "_ledger_sec_economy", P + "_ledger_sec_business", P + "_ledger_sec_markets",
            P + "_ledger_sec_weather", P + "_ledger_sec_people", P + "_ledger_sec_around"
        };

        /// <summary>One kind of piece: every key that starts with <see cref="Prefix"/>.</summary>
        public struct Kind
        {
            public string Prefix;
            public LedgerSection Section;
            public int Weight;
            public string Head;

            public Kind(string prefix, LedgerSection section, int weight, string head)
            {
                Prefix = prefix;
                Section = section;
                Weight = weight;
                Head = head;
            }
        }

        private static Kind K(string suffix, LedgerSection section, int weight, string head = null)
        {
            return new Kind(P + suffix, section, weight, head != null ? P + "_ledger_head_" + head : null);
        }

        /// <summary>
        /// The kinds, the first that fits a key wins (so a longer prefix stands before a shorter one that it starts with).
        /// A key that fits none goes to the city's pages at an ordinary weight: a feature that writes for the paper and is
        /// not in this table yet is printed all the same.
        /// </summary>
        public static readonly Kind[] Kinds =
        {
            // the city: the street, the courts, City Hall (the paper's own words, Social/News)
            K("_news_ped_killed", LedgerSection.City, 100),
            K("_news_accident_severe", LedgerSection.City, 90),
            K("_news_story_fire", LedgerSection.City, 85),
            K("_news_story_prison", LedgerSection.City, 84),
            K("_news_story_convicted", LedgerSection.City, 82),
            K("_news_story_acquitted", LedgerSection.City, 80),
            K("_news_hitrun", LedgerSection.City, 80),
            K("_news_playerarrest", LedgerSection.City, 75),
            K("_news_breakin", LedgerSection.City, 70),
            K("_news_ped_hurt", LedgerSection.City, 65),
            K("_news_accident_moderate", LedgerSection.City, 60),
            K("_news_arrest", LedgerSection.City, 55),
            K("_news_playercrash", LedgerSection.City, 50),
            K("_news_playerknocked", LedgerSection.City, 50),
            K("_news_crime", LedgerSection.City, 45),
            K("_news_carbreakin", LedgerSection.City, 35),
            K("_news_accident_minor", LedgerSection.City, 30),
            K("_news_tally", LedgerSection.City, 15),
            K("_news_quiet", LedgerSection.City, 12),
            K("_civic_news_result_body", LedgerSection.City, 95, "election"),
            K("_civic_news_recall_body", LedgerSection.City, 93, "recall"),
            K("_civic_news_scandal_body", LedgerSection.City, 88, "scandal"),
            K("_civic_news_paytoplay_body", LedgerSection.City, 86, "paytoplay"),
            K("_civic_news", LedgerSection.City, 70),
            K("_ledger_policy_on", LedgerSection.City, 68, "policy_on"),
            K("_ledger_policy_off", LedgerSection.City, 58, "policy_off"),
            K("_event_today", LedgerSection.AroundTown, 18),
            K("_event_tomorrow", LedgerSection.AroundTown, 16),

            // the economy (Economy/City, through the paper's Extra pieces)
            K("_macro_news_rate_up", LedgerSection.Economy, 88, "rate_up"),
            K("_macro_news_rate_down", LedgerSection.Economy, 88, "rate_down"),
            K("_macro_news_rate_hold", LedgerSection.Economy, 50, "rate_hold"),
            K("_macro_news_cycle_recession", LedgerSection.Economy, 86, "recession"),
            K("_macro_news_cycle_slowdown", LedgerSection.Economy, 75, "slowdown"),
            K("_macro_news_cycle_boom", LedgerSection.Economy, 72, "boom"),
            K("_macro_news_cycle_steady", LedgerSection.Economy, 55, "steady"),
            K("_macro_news_shortage", LedgerSection.Economy, 60, "shortage"),
            K("_macro_news_glut", LedgerSection.Economy, 55, "glut"),
            K("_macro_news_prices_up", LedgerSection.Economy, 45, "prices_up"),
            K("_macro_news_prices_down", LedgerSection.Economy, 45, "prices_down"),
            K("_macro_news_prices_flat", LedgerSection.Economy, 35, "prices_flat"),
            K("_macro_news_district_up", LedgerSection.Economy, 40, "district_up"),
            K("_macro_news_district_down", LedgerSection.Economy, 40, "district_down"),
            K("_macro_news_rival", LedgerSection.Business, 45, "rival"),
            K("_macro_news", LedgerSection.Economy, 40),

            // business: the player's own shops and their staff
            K("_co_news_strike", LedgerSection.Business, 72, "strike"),
            K("_ob_news_closed", LedgerSection.Business, 90),
            K("_ob_news_run_over", LedgerSection.Business, 58),
            K("_ob_news_run", LedgerSection.Business, 86),
            K("_ob_news_fined", LedgerSection.Business, 78),
            K("_ob_news_breach", LedgerSection.Business, 74),
            K("_ob_news", LedgerSection.Business, 56),
            K("_co_news", LedgerSection.Business, 50),
            K("_news_shop_loss", LedgerSection.Business, 30),
            K("_news_shop_good", LedgerSection.Business, 25),
            K("_news_shop_none", LedgerSection.Business, 10),

            // the markets (Markets, through the wiring: the market's own sentences, MarketText)
            K("_mkt_news_crash", LedgerSection.Markets, 92, "crash"),
            K("_mkt_news_failed", LedgerSection.Markets, 87, "failed"),
            K("_mkt_news_bid_failed", LedgerSection.Markets, 60, "bid_failed"),
            K("_mkt_news_bid", LedgerSection.Markets, 83, "bid"),
            K("_mkt_news_taken_over", LedgerSection.Markets, 78, "taken_over"),
            K("_mkt_news_craze_burst", LedgerSection.Markets, 76, "craze_burst"),
            K("_mkt_news_craze", LedgerSection.Markets, 66, "craze"),
            K("_mkt_news_crisis", LedgerSection.Markets, 74, "crisis"),
            K("_mkt_news_windfall", LedgerSection.Markets, 60, "windfall"),
            K("_mkt_news_rate_up", LedgerSection.Markets, 70, "market_rate"),
            K("_mkt_news_rate_down", LedgerSection.Markets, 70, "market_rate"),
            K("_mkt_news_listed", LedgerSection.Markets, 62, "listed"),
            K("_mkt_news_listing", LedgerSection.Markets, 40, "listing"),
            K("_mkt_news_delisted", LedgerSection.Markets, 58, "delisted"),
            K("_mkt_news_results_beat", LedgerSection.Markets, 48, "results_beat"),
            K("_mkt_news_results_miss", LedgerSection.Markets, 48, "results_miss"),
            K("_mkt_news_split", LedgerSection.Markets, 40, "split"),
            K("_mkt_news_sector_up", LedgerSection.Markets, 45, "sector_up"),
            K("_mkt_news_sector_down", LedgerSection.Markets, 45, "sector_down"),
            K("_mkt_news_dividend", LedgerSection.Markets, 30, "dividend"),
            K("_mkt_news_up", LedgerSection.Markets, 35, "up"),
            K("_mkt_news_down", LedgerSection.Markets, 35, "down"),
            K("_mkt_news_close", LedgerSection.Markets, 20, "close"),
            K("_mkt_news", LedgerSection.Markets, 30),

            // the rest of the paper
            K("_news_rain_now", LedgerSection.Weather, 10),
            K("_news_rained", LedgerSection.Weather, 10),
            K("_news_dry", LedgerSection.Weather, 10),
            K("_ledger_spell", LedgerSection.Weather, 14),
            K("_ledger_night", LedgerSection.AroundTown, 13),
            K("_news_person", LedgerSection.People, 20),
            K("_news_filler", LedgerSection.AroundTown, 5),
            K("_news_masthead", LedgerSection.AroundTown, -1)   // the old paper's masthead: the page has its own (left out)
        };

        /// <summary>The weight of a piece that fits no kind.</summary>
        public const int UnknownWeight = 40;
    }
}
