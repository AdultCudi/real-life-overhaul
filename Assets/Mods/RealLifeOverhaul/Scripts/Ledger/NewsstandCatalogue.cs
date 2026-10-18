namespace RealLifeOverhaul.Ledger
{
    /// <summary>Newsstand's fixed figures: the four titles and the pass, what each prints and costs a week, the mornings kept. Game-free.</summary>
    public static class NewsstandCatalogue
    {
        private const string P = "reallife";   // in parts: a key's beginning, not a key

        // The titles, and the pass: a subscription that is no paper of its own.
        public const int Ledger = 0, Exchange = 1, Street = 2, Review = 3;
        public const int Titles = 4;
        public const int Pass = 4;
        public const int Subscriptions = 5;

        // A week of each at the start's prices; the City Ledger online costs less than delivered.
        public static readonly int[] WeekAtStart = { 6, 5, 3, 3, 12 };

        public const int WeekDays = LedgerCatalogue.WeekDays;   // days
        public const int KeepDays = 4;          // today's edition and the three mornings before can be read
        public const int CloseDays = 20;        // the index's last closes kept with an earlier edition

        // The markets a title prints: the figures in full, the index and its movers in short, or one line.
        public const int MarketsLine = 0, MarketsShort = 1, MarketsFull = 2;

        public static readonly int[] Markets = { MarketsFull, MarketsFull, MarketsLine, MarketsLine };

        /// <summary>Whether a title prints every listed company's close at the end.</summary>
        public static readonly bool[] SharePrices = { true, true, false, false };

        /// <summary>What each title prints, in the order its page runs. The weather comes at the end of every one.</summary>
        public static readonly LedgerSection[][] Sections =
        {
            new[] { LedgerSection.City, LedgerSection.Economy, LedgerSection.Business, LedgerSection.Markets, LedgerSection.People, LedgerSection.AroundTown },
            new[] { LedgerSection.Markets, LedgerSection.Economy, LedgerSection.Business },
            new[] { LedgerSection.City },
            new[] { LedgerSection.AroundTown, LedgerSection.People }
        };

        public static readonly string[] NameKeys =
        {
            P + "_ledger_online_name_ledger", P + "_ledger_online_name_exchange", P + "_ledger_online_name_street", P + "_ledger_online_name_review",
            P + "_ledger_online_name_pass"
        };

        public static readonly string[] AboutKeys =
        {
            P + "_ledger_online_about_ledger", P + "_ledger_online_about_exchange", P + "_ledger_online_about_street", P + "_ledger_online_about_review",
            P + "_ledger_online_about_pass"
        };

        public static readonly string[] TaglineKeys =
        {
            P + "_ledger_online_tagline_ledger", P + "_ledger_online_tagline_exchange", P + "_ledger_online_tagline_street", P + "_ledger_online_tagline_review"
        };

        /// <summary>Each subscription's charge is a transaction of its own (the Finance app lists them by name).</summary>
        public static readonly string[] TransactionKeys =
        {
            P + "_transaction_ledger_online_ledger", P + "_transaction_ledger_online_exchange", P + "_transaction_ledger_online_street",
            P + "_transaction_ledger_online_review", P + "_transaction_ledger_online_pass"
        };
    }
}
