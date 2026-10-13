namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// EVERY FIGURE OF A PLAYER'S COMPANY ON THE CITY'S MARKET (Markets/Listing). Game-free: the offline checks compile
    /// this file, the Guide prints from it, and nothing else of the listing writes a figure of its own. Money "AtStart" is
    /// at the start's prices (the game side scales it with Core/Prices); shares and prices as the market keeps them
    /// (prices in cents); basis points (bp) are hundredths of a per cent; days are game days unless they say market days.
    /// </summary>
    public static class ListingCatalogue
    {
        // ---------------------------------------------------------------- ids

        public const int IdFirst = 1001;                  // a player's company has an id from here (never one of the catalogue's or a rival's: those are under 1000)
        public const int IdCount = 998;                   // ...up to IdFirst + IdCount - 1, from a hash of its founder

        // ---------------------------------------------------------------- what it takes to come to the market

        public const int BooksYears = 2;                  // years since the player took on their first business
        public const int QuartersLeast = 4;               // complete quarters in the company's books
        public const int TurnoverLeastAtStart = 1500000;  // takings over the last four quarters
        public const int ProfitLeastAtStart = 250000;     // profit before tax over the last four quarters
        public const int ProfitableQuartersLeast = 3;     // of the last four
        public const int AuditValidQuarters = 1;          // a clean audit is good for this many quarters of the year

        // ---------------------------------------------------------------- the advisers, the auditors, the exchange

        public const int RetainerBp = 50;                 // the advisers' retainer: of what the company looks worth...
        public const int RetainerLeastAtStart = 25000, RetainerMostAtStart = 400000;   // ...within these
        public const int AuditBp = 30;                    // the auditors: of a year's takings...
        public const int AuditLeastAtStart = 10000, AuditMostAtStart = 150000;         // ...within these
        public const int AuditDays = 5;                   // days the audit takes
        public const int UnderwritingBp = 500;            // the underwriters: of the money an offer raises...
        public const int UnderwritingLargeBp = 400;       // ...and of a large one
        public const int UnderwritingLargeAtStart = 20000000;
        public const int ListingFeeBp = 10;               // the exchange, on the first day: of what the company is worth at its price...
        public const int ListingFeeLeastAtStart = 10000;  // ...at least
        public const int YearlyFeeBp = 5;                 // the exchange, each year after: of what it is worth then...
        public const int YearlyFeeLeastAtStart = 5000;    // ...at least (the year's audit is paid with it, at the auditors' rate)

        // ---------------------------------------------------------------- the offer and the book

        public static readonly int[] FloatSteps = { 1000, 1500, 2000, 2500, 3000, 4000, 4900, 6000 };   // the share of the company offered, bp
        public const int FloatDefault = 2500;
        public const int SharePriceAtStart = 1000;        // cents: the founder's shares are as many as make one worth about this
        public const long FounderSharesLeast = 100000;
        public const double IpoDiscount = 0.12;           // the advisers price a share this far under what the books say it is worth
        public const double RangeSpread = 0.08;           // the prospectus's range: so far either side of that price
        public const double DemandSpread = 0.08;          // the book's own chance, as a share of the price (one deviation)
        public const double DemandIndexShare = 0.5;       // of the city index's move over the last month...
        public const double DemandIndexMost = 0.15;       // ...counted up to this either way
        public const double DemandStanding = 0.05;        // at a Standing of 100 the book is this much keener, at nothing this much less
        public const double DemandDisclosed = 0.08;       // less, for a matter the auditors disclosed (a bribe that came out)
        public const double DemandFloat = 0.04;           // less, for each half of the company offered (more shares to place)
        public const double PriceBeyondRange = 0.15;      // the price is never further than this outside the range
        public const int BookDays = 5;                    // market days the book is open; it is priced at the next open
        public const int PriceStepCents = 5;              // the price is a whole number of these

        // ---------------------------------------------------------------- the company on the market

        public static readonly int[] PaySteps = { 0, 250, 500, 1000, 1500, 2000, 2500 };   // the founder's pay, bp of each day's profit
        public const int PayRefBp = 500;                  // what the holders take as fair pay
        public const double OwnPayout = 0.95;             // of its profit after tax and pay, what the market counts as its holders' (it is: ListingRules.Day)
        public const int LockUpYearShare = 2;             // the founder sells nothing for a year / this after the listing
        public const int ShortSwingYearShare = 4;         // a founder's gain on shares bought and sold within a year / this goes back to the company
        public const int ClosedShare = 3;                 // no dealing from the last 1 / this of a quarter until its results are out
        public const int FounderMostBp = 9000;            // the founder holds at most this (beyond it, the company is taken private)
        public const int DealKickBp = 60;                 // the market's reading of a founder's deal: for each 1% of the company, up for buying, down for selling
        public const int BuyBackKickBp = 40;              // for each 1% of the company bought back
        public const int IssueKickBp = -50;               // for each 1% the company grows by new shares
        public const int KickMostBp = 800;                // a deal's reading at most, either way
        public const double PlacingDiscount = 0.05;       // new shares are placed this far under the bid
        public const int IssueMostBpYear = 2000;          // new shares in a year: at most this against the company
        public const int BuyBackMostBp = 1000;            // shares bought back at once: at most this of the company
        public const int PrivatePremiumBp = 2500;         // taking it private: every other holder is paid this much over the price
        public const int PrivateLeastBp = 5000;           // ...which the founder may do while they hold at least this
        public const int StandingKickBp = 12;             // the market's mood, for each point the founder's Standing moved...
        public const int StandingKickMostBp = 150;        // ...at most, in a day
        public const int PressKickBp = 2;                 // for each tenth of a point the papers gave or took...
        public const int PressKickMostBp = 120;           // ...at most, in a day
        public const double ScandalOutlook = -0.06;        // a scandal of the founder's: what the market expects of the company, for good
        public const int ScandalKickBp = -250;            // ...and the day's mood
        public const int BoardPayoutBp = 5000;            // a board the founder does not chair pays this share of the reserve at each results
        public const double GrowthShrink = 0.5;           // of the company's own growth over the city's, what the market believes from its books
        public const double DaySeenMost = 3.0;            // a day's takings or profit beyond so many times its year's usual is followed as no more (a one-off)

        // ---------------------------------------------------------------- the shareholders' meetings

        public const int AgmNoticeDays = 10;              // market days from the year's last results to the yearly meeting
        public const int EgmNoticeDays = 15;              // from an investor's demand to the meeting they call
        public const double TurnoutLeast = 0.55, TurnoutMost = 0.80;   // of the other holders' votes, cast
        public const double AgainstNoise = 0.08;          // the holders' mood on the day, one deviation either way
        public const double PayAgainstBase = 0.06;        // against the founder's pay at the fair rate...
        public const double PayAgainstPerRef = 0.55;      // ...and more for each fair pay over it
        public const double DividendAgainstBase = 0.04;   // against the year's dividend...
        public const double DividendAgainstHoard = 0.6;   // ...more while the reserve is high against the price and little was paid
        public const double BoardAgainstBase = 0.06;      // against the founder's place on the board...
        public const double BoardAgainstLag = 1.2;        // ...more for each share of a year's return under the city index's
        public const double BoardAgainstScandal = 0.12;   // ...for each scandal of the year
        public const double BoardAgainstPay = 0.25;       // ...for each fair pay over the fair one
        public const double BoardAgainstActivist = 0.12;  // ...with an investor pressing a demand
        public const double ActivistSupport = 0.35;       // the other holders behind an investor's demand: a base, with the board's against
        public const int MeetingKickBp = -200;            // the market's mood when the board loses a vote
        public const int ActivistWinKickBp = 150;         // ...and when an investor's demand passes

        // ---------------------------------------------------------------- investors who press the board

        public const double ActivistChanceQuarter = 0.35; // at each results, at most, for a company that gives them a reason
        public const int ActivistStakeLeastBp = 300, ActivistStakeMostBp = 800;
        public const double CheapMis = -0.12;             // a price this far under what the company is worth (ln) is cheap
        public const double HoardShare = 0.10;            // a reserve this large a share against the price is held back
        public const double LagIndex = 0.15;              // a year's return this far under the city index's lags it
        public const int SpecialPayoutBp = 6000;          // an investor's demand for money: this share of the reserve paid out

        // ---------------------------------------------------------------- a bid for it

        public const int TakeoverThresholdBp = 3000;      // a bid wins only while the founder holds less than this
        public const double OwnTakeoverChanceYear = 0.6;  // a bid's chance a year while it is cheap, more the cheaper (and after a vote to sell, in days)
        public const double OwnTakeoverPremiumLeast = 0.25, OwnTakeoverPremiumMost = 0.45;
        public const int OwnTakeoverDaysLeast = 10, OwnTakeoverDaysMost = 20;
        public const int SaleBidDays = 5;                 // after a vote to sell, market days until a bid is made
        public const double AcceptBase = 0.5;             // the holders take a bid: so likely at what the company is worth...
        public const double AcceptPerPremium = 2.0;       // ...more for each share of a premium over it
        public const double AcceptLeast = 0.2, AcceptMost = 0.95;

        // ---------------------------------------------------------------- tax on dividends

        public const int DividendTaxBp = 1500;            // withheld from every dividend the player is paid...
        public const int DividendTaxStepBp = 250;         // ...more or less for each step of the council's business tax
    }
}
