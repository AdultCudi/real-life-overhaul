namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Every figure of a player's company on the city's market. Game-free: the offline checks compile it and the Guide
    /// prints from it. Money "AtStart" is at the start's prices (scaled by Core/Prices); share prices in cents; bp are
    /// hundredths of a per cent; days are game days unless they say market days.
    /// </summary>
    public static class ListingCatalogue
    {
        public const int IdFirst = 1001;                  // above the catalogue's and the rivals' ids (under 1000)...
        public const int IdCount = 998;                   // ...up to IdFirst + IdCount - 1, from a hash of its founder

        // Coming to the market
        public const int BooksYears = 2;                  // years since the player's first business
        public const int QuartersLeast = 4;               // complete quarters in the books
        public const int TurnoverLeastAtStart = 1500000;  // takings over the last four quarters
        public const int ProfitLeastAtStart = 250000;     // profit before tax over the last four quarters
        public const int ProfitableQuartersLeast = 3;     // of the last four
        public const int AuditValidQuarters = 1;

        // The advisers, the auditors, the exchange
        public const int RetainerBp = 50;                 // of what the company looks worth...
        public const int RetainerLeastAtStart = 25000, RetainerMostAtStart = 400000;   // ...within these
        public const int AuditBp = 30;                    // of a year's takings...
        public const int AuditLeastAtStart = 10000, AuditMostAtStart = 150000;         // ...within these
        public const int AuditDays = 5;
        public const int UnderwritingBp = 500;            // of the money an offer raises...
        public const int UnderwritingLargeBp = 400;       // ...and of a large one
        public const int UnderwritingLargeAtStart = 20000000;
        public const int ListingFeeBp = 10;               // of the company's worth, on the first day...
        public const int ListingFeeLeastAtStart = 10000;  // ...at least
        public const int YearlyFeeBp = 5;                 // of its worth each year after...
        public const int YearlyFeeLeastAtStart = 5000;    // ...at least; the year's audit is paid with it

        // The offer and the book
        public static readonly int[] FloatSteps = { 1000, 1500, 2000, 2500, 3000, 4000, 4900, 6000 };   // bp of the company offered
        public const int FloatDefault = 2500;
        public const int SharePriceAtStart = 1000;        // cents: the founder gets as many shares as make one worth about this
        public const long FounderSharesLeast = 100000;
        public const double IpoDiscount = 0.12;           // a share is priced this far under what the books say
        public const double RangeSpread = 0.08;           // the prospectus's range either side of that price
        public const double DemandSpread = 0.08;          // the book's own chance, one deviation, as a share of the price
        public const double DemandIndexShare = 0.5;       // of the city index's move over the last month...
        public const double DemandIndexMost = 0.15;       // ...up to this either way
        public const double DemandStanding = 0.05;        // keener at a Standing of 100, less keen at none
        public const double DemandDisclosed = 0.08;       // less for a matter the auditors disclosed
        public const double DemandFloat = 0.04;           // less for each half of the company offered
        public const double PriceBeyondRange = 0.15;      // the price stays within this outside the range
        public const int BookDays = 5;                    // market days; priced at the next open
        public const int PriceStepCents = 5;

        // The company on the market
        public static readonly int[] PaySteps = { 0, 250, 500, 1000, 1500, 2000, 2500 };   // the founder's pay, bp of each day's profit
        public const int PayRefBp = 500;                  // what the holders take as fair pay
        public const double OwnPayout = 0.95;             // of its profit after tax and pay, counted as its holders'
        public const int LockUpYearShare = 2;             // the founder sells nothing for a year / this after the listing
        public const int ShortSwingYearShare = 4;         // a gain on shares held under a year / this goes back to the company
        public const int ClosedShare = 3;                 // no dealing in the last 1 / this of a quarter until its results
        public const int FounderMostBp = 9000;            // beyond it the company is taken private
        public const int FounderDealMostBpYear = 1000;    // bought, or sold, by the founder in a game year
        public const int DealKickBp = 60;                 // for each 1% of the company the founder deals in, up for buying
        public const int BuyBackKickBp = 40;              // for each 1% bought back
        public const int IssueKickBp = -50;               // for each 1% of new shares
        public const int KickMostBp = 800;                // a deal's reading at most, either way
        public const double PlacingDiscount = 0.05;       // new shares placed this far under the bid
        public const int IssueMostBpYear = 2000;
        public const int BuyBackMostBp = 1000;            // in a game year
        public const int PrivatePremiumBp = 2500;         // over the price, and never under the last year's highest close
        public const int PrivateLeastBp = 5000;           // the founder's holding needed to take it private
        public const int StandingKickBp = 12;             // for each point the founder's Standing moved...
        public const int StandingKickMostBp = 150;        // ...at most, in a day
        public const int PressKickBp = 2;                 // for each tenth of a point the papers gave or took...
        public const int PressKickMostBp = 120;           // ...at most, in a day
        public const double ScandalOutlook = -0.06;       // a founder's scandal: the company's outlook, for good...
        public const int ScandalKickBp = -250;            // ...and the day's mood
        public const int BoardPayoutBp = 5000;            // of the reserve at each results, when the founder does not chair
        public const double AuditOutlook = -0.08;         // a failed yearly audit: the outlook, for good...
        public const int AuditKickBp = -400;              // ...and the day's mood
        public const int HandOverDays = 7;                // after a bid wins, before what is left is paid for
        public const double GrowthShrink = 0.5;           // of its own growth over the city's, what the market believes
        public const double DaySeenMost = 3.0;            // times its year's usual: a day beyond is followed as a one-off

        // The shareholders' meetings
        public const int AgmNoticeDays = 10;              // market days from the year's last results
        public const int EgmNoticeDays = 15;              // from an investor's demand
        public const double TurnoutLeast = 0.55, TurnoutMost = 0.80;   // of the other holders' votes
        public const double AgainstNoise = 0.08;          // the holders' mood on the day, one deviation
        public const double PayAgainstBase = 0.06;        // against the founder's pay at the fair rate...
        public const double PayAgainstPerRef = 0.55;      // ...and more for each fair pay over it
        public const double DividendAgainstBase = 0.04;   // against the year's dividend...
        public const double DividendAgainstHoard = 0.6;   // ...more while the reserve is high and little was paid
        public const double BoardAgainstBase = 0.06;      // against the founder's seat...
        public const double BoardAgainstLag = 1.2;        // ...for each share of a year's return under the index's
        public const double BoardAgainstScandal = 0.12;   // ...for each scandal of the year
        public const double BoardAgainstPay = 0.25;       // ...for each fair pay over the fair one
        public const double BoardAgainstActivist = 0.12;  // ...with an investor pressing a demand
        public const double ActivistSupport = 0.35;       // behind an investor's demand, before the board's against
        public const int MeetingKickBp = -200;            // the board loses a vote
        public const int ActivistWinKickBp = 150;         // an investor's demand passes

        // Investors who press the board
        public const double ActivistChanceQuarter = 0.35; // at each results, for a company that gives a reason
        public const int ActivistStakeLeastBp = 300, ActivistStakeMostBp = 800;
        public const double CheapMis = -0.12;             // ln of price over worth: cheap under this
        public const double HoardShare = 0.10;            // reserve against the price: held back over this
        public const double LagIndex = 0.15;              // a year's return this far under the index's lags it
        public const int SpecialPayoutBp = 6000;          // of the reserve, paid out on an investor's demand

        // A bid for it
        public const int TakeoverThresholdBp = 3000;      // a bid wins only while the founder holds less
        public const double OwnTakeoverChanceYear = 0.6;  // while cheap, more the cheaper; after a vote to sell, in days
        public const double OwnTakeoverPremiumLeast = 0.25, OwnTakeoverPremiumMost = 0.45;
        public const int OwnTakeoverDaysLeast = 10, OwnTakeoverDaysMost = 20;
        public const int SaleBidDays = 5;                 // market days after a vote to sell
        public const double AcceptBase = 0.5;             // chance the holders take a bid at the company's worth...
        public const double AcceptPerPremium = 2.0;       // ...more for each share of premium
        public const double AcceptLeast = 0.2, AcceptMost = 0.95;

        // Tax on dividends
        public const int DividendTaxBp = 1500;            // withheld from every dividend the player is paid...
        public const int DividendTaxStepBp = 250;         // ...more or less for each step of the council's business tax
    }
}
