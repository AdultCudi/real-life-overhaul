namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The labour market's figures: one list of seekers the player's ads, the rivals and the recruitment office all draw on.
    /// Game-free; amounts at the start's prices.
    /// </summary>
    public static class HiringCatalogue
    {
        // The seekers
        public const int SeekersLeast = 50;                         // seekers on the market
        public const int SeekersMost = 160;
        public const int SeekerDaysLeast = 10, SeekerDaysMost = 40;   // days a seeker looks before they find work elsewhere
        public const int GraduatesSummer = 30;                      // more juniors in the summer

        /// <summary>Unemployment among professionals by the city's cycle (boom, steady, slowdown, recession), per mille.</summary>
        public static readonly int[] Unemployment = { 35, 50, 70, 100 };

        /// <summary>The share of seekers in each family, thousandths, in the order of <see cref="Family"/>.</summary>
        public static readonly int[] FamilyShare = { 200, 80, 50, 80, 70, 100, 150, 110, 130, 30 };

        /// <summary>A family's shortage before the market's own figures, thousandths: engineers, data, security and infrastructure are scarce.</summary>
        public static readonly int[] FamilyScarcity = { 1150, 1200, 1250, 1180, 1050, 980, 950, 900, 1000, 1050 };

        /// <summary>The share of seekers at each level, thousandths: seniors and above are few.</summary>
        public static readonly int[] LevelShare = { 360, 330, 200, 75, 35 };

        public const int EmployedShare = 450;       // thousandths of seekers in work now: they ask more and have notice to give
        public const int EmployedPremiumBp = 800;
        public const int UnemployedDiscountBp = 500;
        public const int AskSpreadBp = 800;         // a seeker's own ask either side of the going rate
        public const int ShortageLeast = 850, ShortageMost = 1500;   // thousandths

        // The rivals' ads
        public const int RivalAdsMost = 40;
        public const int RivalAdDays = 14;                          // days an ad runs
        public static readonly int[] RivalAdsByCycle = { 30, 22, 14, 8 };   // ads, boom to recession
        public const int RivalTakeChance = 120;     // per mille a day a rival's ad fills from a matching seeker

        // The player's ads
        public const int AdsMost = 12;
        public static readonly int[] AdDays = { 7, 14, 30 };   // days an ad can run
        public const int AdDayCost = 25;            // whole money a day at the start's prices
        public const int BoostCostBp = 25000;       // a boosted ad costs this much of a plain one
        public const int AdWeekMost = 3;            // applicants a week of an ad brings at most
        public const int AdWeekMostBoosted = 4;
        public const int ApplyChance = 90;          // per mille a day a matching seeker who likes the ad applies
        public const int UnderRequirement = 200;    // thousandths of that for a seeker short of the skill asked
        public const int BoostReach = 2000;         // thousandths: a boosted ad is seen by twice as many

        // Screening, interviews and offers
        public const int ReferencesCost = 60;       // whole money at the start's prices
        public const int BackgroundCost = 120;      // whole money at the start's prices
        public const int ClaimMost = 15;            // points a CV may claim over the truth
        public const int InterviewRounds = 3;
        public const int AcceptBp = 9700;           // an offer this share of the ask (or more) is taken
        public const int CounterBp = 8800;          // from this share they come back with their ask
        public const int EsteemBp = 50;             // basis points of the ask each point of standing as an employer is worth
        public const int RivalOfferChance = 250;    // per mille that an applicant in a scarce family has another offer by the time of yours
        public const int RivalOfferBp = 1000;       // ...for this much over their ask
        public const int OfferDays = 3;             // days an applicant takes to answer
        public const int ApplicantDays = 21;        // an application not acted on lapses after this
        public const int StartLeast = 1, StartUnemployedMost = 3;
        public const int StartTries = 3;   // days past the first day that a start the game refuses is tried again before the applicant withdraws, and the player is told
        public const int ApplicantsMost = 120;

        // The recruitment office
        public const string AgencyType = "ba:businesstype_recruitmentagency";
        public const int OfficeBonusLeast = 4;      // more candidates than asked for, at the least
        public const int OfficeBonusShare = 500;    // ...or half as many again, if more
        public const int OfficeSkillFloor = 25;     // the office's own candidates are screened to this at the least

        // Executive search
        public const int SearchesMost = 4;
        public const int SearchFeeBp = 2000;        // a share of a year's pay at the ask
        public const int SearchRetainerShare = 333; // thousandths of the fee paid when the search starts; the rest on a hire
        public const int SearchDaysLeast = 4, SearchDaysMost = 8;
        public const int SearchCandidates = 3;
        public const int SearchLeastLevel = TalentCatalogue.Senior;

        // A data centre's crew by a search firm, briefed from the player's headquarters: juniors to staff level, a fee a hire
        public const string Headquarters = "ba:businesstype_headquarters";      // the game's business type of a company's headquarters
        public const string NoShifts = "ba:businesstype_warehouse";             // the game's business type with no desks and no shifts for an employee (a data centre is one)
        public static readonly int[] CrewRoles = { TalentCatalogue.Technician, TalentCatalogue.NetworkEngineer };   // who looks after racks on site
        public const int CrewLeastLevel = TalentCatalogue.Junior, CrewMostLevel = TalentCatalogue.Staff;
        public static readonly int[] CrewFeeBp = { 800, 1000, 1300, 1600 };      // a share of a year's pay at the going rate, a hire, junior to staff
        public const int CrewSearchesMost = 3;                                     // crew searches open at once, beside the senior searches
        public const int CrewMost = 8;                                             // technicians one brief asks for
        public const int CrewCandidatesMost = 12;                                  // candidates one brief brings at most
        public const int CrewDaysLeast = 2, CrewDaysMost = 4;                      // days the firm takes
        /// <summary>The candidates of every search open at once, held besides the open market: the caps of the seekers and applicants.</summary>
        public const int SearchSeekersMost = SearchCandidates * SearchesMost + CrewCandidatesMost * CrewSearchesMost;

        /// <summary>The fictional companies of the city that hire professionals, beside the game's own rivals.</summary>
        public static readonly string[] RivalFirms =
        {
            "Codeharbour Software", "Skyvault Systems", "Abacus Lane Software", "Shopfront Online", "Pixel Harbour Games", "Tallyrow Analytics", "Signpost Search",
            "Circuitry Works", "Quartzline Semiconductors", "Ledgerline Payments", "Signalhill Telecom", "Fibreway Networks"
        };
    }
}
