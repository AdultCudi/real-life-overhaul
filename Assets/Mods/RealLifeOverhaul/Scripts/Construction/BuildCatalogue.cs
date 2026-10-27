using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a building is for: the game's building type it is made as. Saved as a number: never renumber.</summary>
    public enum BuildUse
    {
        Homes = 0,
        Shops = 1,
        Offices = 2,
        Warehouses = 3,
        Count = 4
    }

    /// <summary>What a project builds. Saved as a number.</summary>
    public enum ProjectKind
    {
        Plot = 0,       // a new building on an Uptown lot the player owns
        Rebuild = 1,    // an old building of the player's, rebuilt behind the game's scaffolding
        Contract = 2,   // works won in a tender: the city's, a business's or a public building
        Refit = 3,      // the interior of a unit of the player's refitted, by their crews or a contractor of the city's, furnished anew or not
        Demolition = 4, // an Uptown building knocked down to its ground, which is then free ground of whoever knocked it down
        Extension = 5,  // more storeys of floor area on an Uptown building, worked as a rebuild behind the scaffolding, its front its own
        Conversion = 6, // an Uptown building made another use its structure takes, emptied first, worked as the programme's refit works
        Parking = 7     // a car park on a site of the player's: fast plans and permission, a day or three of works (CarParkRules), worked as the programme's parking works
    }

    /// <summary>What the city's developers do with a building of Uptown's (each by the player's own rules and calls). Saved as a number.</summary>
    public enum CityMove
    {
        Redevelop = 0,  // knocked down, and a new building on its ground
        Assemble = 1,   // two neighbours knocked down, and one bigger building over both
        Split = 2,      // a building over several lots knocked down, and a smaller one on each
        Extend = 3,     // storeys put on top
        Convert = 4,    // made another use its structure takes
        Count = 5
    }

    /// <summary>Where a redevelopment of the city's stands. Saved as a number.</summary>
    public enum CityStage
    {
        Offer = 0,      // the developer's offer for a building for sale, to complete on its day (the player may buy it first)
        Notice = 1,     // the permission asked, the notice to its tenants and business running
        Works = 2,      // the demolition, the floors on top or the fit-out
        Building = 3,   // the new building going up on the cleared ground
        Done = 4,
        Dropped = 5     // given up: outbid by the player, refused, or nothing of the city's fits
    }

    /// <summary>Where a project stands, from the plans to the keys. Saved as a number.</summary>
    public enum ProjectStage
    {
        Design = 0,
        Planning = 1,
        Ready = 2,
        Building = 3,
        Finished = 4,
        Abandoned = 5
    }

    /// <summary>The planning application. Saved as a number.</summary>
    public enum PlanningState
    {
        None = 0,
        Submitted = 1,
        Granted = 2,
        Refused = 3,
        Appealed = 4,
        AppealLost = 5
    }

    /// <summary>Who draws the plans. Saved as a number.</summary>
    public enum ArchitectKind
    {
        Firm = 0,     // one of the city's practices, for a fee
        Staff = 1,    // an employee of the company in the architect's role
        Player = 2    // the player, once their own training lets them
    }

    /// <summary>The four packages of work a site goes through. Saved as a number.</summary>
    public enum WorkStage
    {
        Ground = 0,
        Foundation = 1,
        Frame = 2,
        Envelope = 3,
        Count = 4
    }

    /// <summary>The building inspector's three visits. Saved as a number.</summary>
    public enum Inspection
    {
        Foundation = 0,
        Frame = 1,
        Final = 2,
        Count = 3
    }

    /// <summary>One inspection. Saved as a number.</summary>
    public enum InspectionState
    {
        NotDue = 0,
        Due = 1,
        Booked = 2,
        Passed = 3,
        Failed = 4
    }

    /// <summary>What the builders' merchants deliver. Saved as a number.</summary>
    public enum Material
    {
        Cement = 0,
        Steel = 1,
        Bricks = 2,
        Glass = 3,
        Timber = 4,
        Count = 5
    }

    /// <summary>Plant a site works with. Saved as a number.</summary>
    public enum Plant
    {
        Crane = 0,
        Digger = 1,
        Mixer = 2,
        Scaffold = 3,
        Count = 4
    }

    /// <summary>The trades of a crew, a foreman leading each. Saved as a number. Ids shared with the trades' careers (Scripts/BuildTrades).</summary>
    public enum Trade
    {
        Labourer = 0,
        Mason = 1,
        CraneOperator = 2,
        Electrician = 3,
        Plumber = 4,
        Count = 5
    }

    /// <summary>A role an employee of the company's office is given. Saved as a number.</summary>
    public enum StaffRole
    {
        None = 0,
        Architect = 1,
        Engineer = 2,
        Estimator = 3,
        SiteManager = 4,
        Count = 5
    }

    /// <summary>What the company is known for. Saved as a number.</summary>
    public enum Speciality
    {
        None = 0,
        Homes = 1,
        Offices = 2,
        Roads = 3,
        Renovation = 4,
        Demolition = 5,
        Count = 6
    }

    /// <summary>The contractor's insurance. Saved as a number.</summary>
    public enum CoverLevel
    {
        None = 0,
        Standard = 1,
        Full = 2
    }

    /// <summary>What becomes of a finished building on a lot. Saved as a number.</summary>
    public enum AfterUse
    {
        Keep = 0,        // the player's own: used, let or sold through the game's own BizMan
        SellFlats = 1    // homes sold flat by flat; the building is its residents'
    }

    /// <summary>Who asked for works put out to tender. Saved as a number.</summary>
    public enum TenderSource
    {
        CityWorks = 0,   // one of the city's construction programme's sites
        Client = 1,      // a business of the city's that wants its building rebuilt
        Public = 2,      // a public building on an Uptown lot
        Refit = 3,       // a business of the city's that wants its interior refitted
        Landmark = 4     // one of Uptown's landmarks, the city's megaproject on a block of its own (Scripts/Landmarks)
    }

    /// <summary>A tender. Saved as a number.</summary>
    public enum TenderState
    {
        Open = 0,
        Won = 1,
        Lost = 2,
        Gone = 3
    }

    /// <summary>The contractor's licence. Saved as a number.</summary>
    public enum LicenceState
    {
        None = 0,
        Applied = 1,
        Granted = 2,
        Lapsed = 3,
        Refused = 4
    }

    /// <summary>
    /// Every number of the construction game, and the names its texts are made of. Money is at the start's prices (a price
    /// index of 100) and is charged at today's (Core/Prices); a share is in thousandths. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class BuildCatalogue
    {
        public const string Prefix = "reallife";   // apart, so a key made of parts is not taken for a whole key

        // ------------------------------------------------------------------ uses and the game's types

        public static readonly string[] UseTypes =
        {
            "ba:buildingtype_residential", "ba:buildingtype_retail", "ba:buildingtype_office", "ba:buildingtype_warehouse"
        };

        public static readonly string[] UseKeys =
        {
            Prefix + "_bld_use_homes", Prefix + "_bld_use_shops", Prefix + "_bld_use_offices", Prefix + "_bld_use_warehouses"
        };

        /// <summary>The use a game type is, or -1 for a type the construction game does not build.</summary>
        public static int UseOf(string type)
        {
            for (int i = 0; i < UseTypes.Length; i++)
            {
                if (string.Equals(UseTypes[i], type, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        public static int Zone(BuildUse use) => 1 << (int)use;

        /// <summary>The uses a lot of each of the district's classes (small, office, big, warehouse) is zoned for to begin with.</summary>
        public static readonly int[] ClassZones = { 1 | 2, 4 | 2, 2 | 1, 8 };

        /// <summary>The use each class is valued by, its usual building.</summary>
        public static readonly BuildUse[] ClassUse = { BuildUse.Homes, BuildUse.Offices, BuildUse.Shops, BuildUse.Warehouses };

        /// <summary>Floor area over lot area of each class's usual building: what its land is priced on (the city's own measured buildings).</summary>
        public static readonly int[] PlotRatioPermille = { 2000, 5000, 2500, 1500 };

        public const int LandSharePermille = 680;       // of the usual building's value: what a lot costs; the rest pays for building and its margin
        public const int LandRound = 1000;              // money: lot prices end in thousands
        public const int WideFrom = 21;                 // m of land along the street: from here a plot has its yard beside the frame (PlotRules.MinWidth)
        public const float FrameMargin = 3.5f;          // m of land kept round a frame (PlotRules.Margin)
        public const float FrameYard = 9f;              // m of yard beside a wide plot's frame (PlotRules.Yard)
        public const float NarrowSide = 1f;             // m kept each side of a narrow plot's frame (PlotRules.NarrowSide)
        public const float NarrowYard = 7.5f;           // m of yard behind a narrow plot's frame (PlotRules.NarrowYard)
        public const int NarrowFrameStoreys = 6, FrameStoreysMost = 10;   // storeys a plot's frame shows at most (PlotRules.NarrowStoreys, MostStoreys)
        public const int ClassCount = 4;

        // ------------------------------------------------------------------ the plans

        public const int FirmCount = 3;
        public static readonly int[] FirmFeePermille = { 35, 55, 80 };     // of the build's estimate
        public static readonly int[] FirmDays = { 6, 4, 3 };               // days to draw the plans
        public static readonly int[] FirmQuality = { 56, 72, 88 };         // the plans' quality, 0 to 100
        public const int StaffQualityBase = 40;                            // an architect on the staff: this plus their skill's share
        public const int StaffQualityPermille = 550;                       // of the employee's skill (0 to 100)
        public const int StaffDays = 5;
        public const int PlayerQualityBase = 30;                           // the player's own design: this plus their design score's share
        public const int PlayerQualityPermille = 700;
        public const int DaysPerArea = 2500;                               // m2 of floor: a day more to draw for each
        public const int DesignCostLeast = 600, DesignCostMost = 1600;    // thousandths: the player's own design's cost against its budget, bounded
        public const int RedrawFeePermille = 400;                          // of the first fee: plans drawn again after a refusal
        public const int RedrawDaysPermille = 600;

        public const int EngineerFirmFeePermille = 8;                      // of the estimate: an outside engineer's sign off
        public const int EngineerFirmDays = 3;
        public const int EngineerStaffDays = 2;
        public const int EngineerFromStoreys = 3;                          // storeys: a frame this tall needs an engineer's sign off

        public const int ContingencyPermille = 50;                         // the estimate's allowance for what goes wrong
        public const int EstimateSpreadNone = 150;                         // thousandths either way: an estimate with no estimator
        public const int EstimateSpreadFirm = 60;                          // a practice's own quantity surveyor
        public const int EstimateSpreadBase = 140;                         // an estimator on the staff: this less their skill's share
        public const int EstimateSpreadPerSkill = 1;                       // thousandths a point of skill
        public const int EstimateSpreadLeast = 30;

        /// <summary>Loads a hundred square metres of floor needs at each stage, by material (cement, steel, bricks, glass, timber), in tenths.</summary>
        public static readonly int[][] StageLoads =
        {
            new[] { 0, 0, 0, 0, 0 },
            new[] { 16, 6, 0, 0, 0 },
            new[] { 14, 12, 12, 0, 0 },
            new[] { 0, 0, 8, 9, 11 }
        };

        public static readonly int[] LoadPrice = { 9000, 32000, 11000, 21000, 14000 };   // money a lorry load, by material

        /// <summary>
        /// The raw material of the exchange (Scripts/Markets) each material's price follows, by the exchange's name: the
        /// exchange's own building materials (no recipe of the game's has cement, steel, bricks or timber), and the game's
        /// glass, so a project's materials can be hedged with futures on what it really buys.
        /// </summary>
        public static readonly string[] ExchangeItem =
        {
            Prefix + "_mkt_drv_mat_cement", Prefix + "_mkt_drv_mat_steel", Prefix + "_mkt_drv_mat_bricks", "ba:itemname_glass", Prefix + "_mkt_drv_mat_timber"
        };

        public static readonly int[] UseMaterialsPermille = { 1000, 950, 1150, 550 };    // materials a square metre by use, against homes
        public const int StoreyMaterialsPermille = 40;                                   // more a storey over two: a taller frame is heavier
        public const int TallFrom = 2;

        public static readonly int[] HoursPerHundred = { 50, 45, 55, 30 };   // working hours a hundred square metres needs, by use, in tenths
        public const int StoreyHoursPermille = 30;                           // more a storey over one
        public const int HoursLeast = 40, HoursMost = 480;                   // a site's working hours, at least and at most

        /// <summary>The share of a site's progress at which each stage ends, in thousandths, for a new building and for a rebuild.</summary>
        public static readonly int[] PlotStageEnds = { 400, 600, 800, 1000 };
        public static readonly int[] RebuildStageEnds = { 200, 400, 800, 1000 };
        public static readonly int[] ContractStageEnds = { 250, 500, 750, 1000 };
        public const double LastStageEnd = 0.999;               // of the site's progress: the last stage stops here for the final inspection, the handover takes the rest
        public const int ScheduleMostDays = 400;                // days: a site's schedule is run no further (480 hours at the slowest pace and code take about 280)

        // ------------------------------------------------------------------ requirements

        public static readonly int[] ParkingPerSpace = { 120, 60, 80, 200 };   // m2 of floor a parking space is needed for, by use
        public const int ParkingSpaceCost = 9000;                               // money a space under the building
        public const int ExitArea = 600;                                        // m2 of a storey: one more fire exit for each
        public const int ExitTallFrom = 3;                                      // storeys: from here two exits at least
        public const int ExitVeryTallFrom = 9;                                  // storeys: from here three
        public const int ExitCostPermille = 6;                                  // of the build's cost, for each exit after the first
        public const double StoreyHeight = 3.6;                                 // m: a storey of a design's height, as the sites' frames are
        public static readonly string[] DistrictKeys = { "reallife_neighborhood_uptown", "reallife_neighborhood_cedarheights" };   // their names, as locale keys
        public const int Districts = 2;                                         // the band's districts (Uptown 0, Cedar Heights 1): each has its own demand, market and cap on grown sites
        public const int UptownHeightLimit = 12;                                // storeys: the new district's limit

        /// <summary>Storeys a district allows, by the game's district ids; any other has <see cref="DefaultHeightLimit"/>.</summary>
        public static readonly string[] HeightDistricts =
        {
            "ba:neighborhood_midtown", "ba:neighborhood_garmentdistrict", "ba:neighborhood_murrayhill", "ba:neighborhood_hellskitchen",
            "ba:neighborhood_lowermanhattan", "ba:neighborhood_industrycity", "ba:neighborhood_thehamptons"
        };

        public static readonly int[] HeightLimits = { 40, 30, 20, 20, 30, 10, 3 };
        public const int DefaultHeightLimit = 12;

        // ------------------------------------------------------------------ planning

        public const int PlanningFee = 1500;                    // money, and the estimate's share below
        public const int PlanningFeePermille = 2;
        public const int PlanningDays = 6;                      // working days to a decision
        public const int PlanningDaysArea = 1500;               // m2: a working day more for each
        public const int PlanningDaysMost = 14;
        public const int ConsultDay = 3;                        // working days in: the neighbours have had their say
        public const int ObjectionsMost = 9;
        public const int GrantBasePermille = 780;               // the odds of a plain application
        public const int ObjectionPermille = 70;                // less for each objection
        public const int UnmetPermille = 180;                   // less for each requirement not met
        public const int PermitsPermille = 60;                  // more for each step of the council's permits policy
        public const int GrantLeastPermille = 50, GrantMostPermille = 970;
        public const int ConditionsFrom = 2;                    // objections: from here the permission comes with conditions
        public const int ContributionPermille = 5;              // of the estimate: a contribution to the street, a condition
        public const int AppealFee = 4500;
        public const int AppealDaysLeast = 8, AppealDaysMost = 12;
        public const int AppealBasePermille = 300;
        public const int AppealMetPermille = 400;               // more for the share of requirements met
        public const int AppealLawyerPermille = 100;
        public const int AppealObjectionPermille = 40;

        public const int ZoneFee = 2500;
        public const int ZoneDaysLeast = 3;                     // days: the council hears it at its next meeting this many days on at least
        public const int CouncilWeekday = 3;                    // the council's day: Thursday (day 1 is a Monday)

        /// <summary>The council's odds of adding a use to a lot, by the use asked for and the lot's class, in thousandths.</summary>
        public static readonly int[][] ZoneOdds =
        {
            new[] { 900, 450, 700, 300 },   // homes on a small, office, big or warehouse lot
            new[] { 900, 800, 900, 600 },   // shops
            new[] { 450, 900, 650, 500 },   // offices
            new[] { 150, 250, 300, 900 }    // warehouses
        };

        public const int LicenceFee = 2800;
        public const int LicenceDays = 2;                       // days to decide
        public const int LicenceRefusedRest = 30;               // days before a refused company may ask again
        public const int LicenceRemind = 5;                     // days before it runs out: a reminder

        // ------------------------------------------------------------------ the site

        /// <summary>People of each trade a stage needs to go at its usual pace (labourers, masons, crane operators, electricians, plumbers).</summary>
        public static readonly int[][] StageCrew =
        {
            new[] { 4, 1, 0, 0, 0 },
            new[] { 2, 3, 0, 0, 0 },
            new[] { 2, 3, 1, 0, 0 },
            new[] { 0, 1, 0, 2, 2 }
        };

        /// <summary>Plant each stage works with (crane, digger, mixer, scaffold), 1 for needed.</summary>
        public static readonly int[][] StagePlant =
        {
            new[] { 0, 1, 0, 0 },
            new[] { 0, 1, 1, 0 },
            new[] { 1, 0, 1, 1 },
            new[] { 0, 0, 0, 1 }
        };

        public const int CraneFrom = 3;                         // storeys: lower frames are lifted without a tower crane
        public const int PaceMostPermille = 1800;               // a stage goes at most this fast however many are on it
        public const int PaceLeastPermille = 250;               // and at least this slow with somebody on it
        public const int TradeMissingPermille = 500;            // the pace with a trade the stage needs missing altogether
        public const int PlantMissingPermille = 550;            // the pace for each piece of plant missing
        public const int CraneMissingPermille = 250;            // a tall frame with no crane
        public const int SkillPacePermille = 750;               // the pace at nought skill; a point of skill adds 5 thousandths
        public const int MoralePacePermille = 700;              // the pace at nought morale; a point adds 4 thousandths
        public const int RushFromPermille = 1400;               // faster than this: the inspector notices

        public const int InspectionCount = 3;
        public static readonly int[] InspectionFee = { 900, 1200, 1800 };
        public const int InspectLateHour = 15;                  // booked after this hour, the inspector comes a working day later
        public const int InspectorHour = 10;                    // the inspector comes at ten on the day booked
        public const int PassBasePermille = 550;
        public const int PassPerQuality = 4;                    // thousandths a point of the work's quality
        public const int PassRushPermille = 100;                // less when rushed
        public const int PassVisitPermille = 50;                // more when the player walked the site with the foreman in the stage
        public const int PassManagerPermille = 50;              // more with a site manager of skill 60 or more
        public const int PassMostPermille = 970;
        public const int ReworkPermille = 60;                   // of the stage's hours: put right after a failed inspection
        public const int BribePermille = 40;                    // of the stage's materials: what the inspector is offered
        public const int BribeLeast = 2000;
        public const int BribeChancePermille = 30;              // a day, for 21 days: it comes out
        public const int BribeDays = 21;

        public const int MerchantCount = 3;
        public static readonly int[] MerchantPricePermille = { 1000, 950, 1080 };
        public static readonly int[] MerchantDays = { 2, 3, 1 };          // working days to deliver
        public static readonly int[] MerchantShortPermille = { 120, 200, 50 };
        public const int ShortLeastPermille = 550, ShortMostPermille = 850;   // of the order a short delivery brings
        public const int ShortFollowDays = 2;
        public const int DeliveryHour = 7;

        public static readonly int[] HireDay = { 2400, 900, 260, 520 };   // money a day on site, by plant
        public const int HireDelivery = 600;                               // money to bring a piece to a site
        public static readonly int[] BuyPrice = { 520000, 140000, 14000, 85000 };
        public const int UpkeepPermille = 4;                               // tenths of a thousandth of the price a day: 0.04 %
        public const int ResalePermille = 600;
        public const int ResaleLossPerMonth = 20;                          // thousandths less for each thirty days owned
        public const int ResaleLeastPermille = 300;

        public const int TheftPermille = 15;                    // a night, a site with materials on it
        public const int TheftLeastPermille = 200, TheftMostPermille = 400;   // of one material on the site
        public const int StormReworkPermille = 40;              // of a frame's hours, a storm's damage

        // ------------------------------------------------------------------ crews

        public static readonly int[] TradeRate = { 210, 290, 360, 330, 320 };   // money a person a working day, by trade
        public const int ForemanRate = 400;
        public const int GangCount = 5;

        /// <summary>The kinds of crew the labour market offers: people of each trade, a foreman leading each.</summary>
        public static readonly int[][] Gangs =
        {
            new[] { 4, 1, 0, 0, 0 },   // groundworks
            new[] { 2, 3, 0, 0, 0 },   // concrete
            new[] { 2, 3, 1, 0, 0 },   // frame
            new[] { 0, 1, 0, 2, 2 },   // fit-out
            new[] { 2, 2, 0, 1, 1 }    // general
        };

        public const int OffersAWeek = 4;
        public const int SkillLeast = 30, SkillMost = 85;
        public const int MoraleStart = 65;
        public const int MoraleUnpaid = 2, MoraleWorking = 1, MoraleIdle = 1, MoraleRework = 3, MoraleRaise = 4;
        public const int IdleFrom = 3;                          // days without work before morale falls
        public const int StrikeAt = 20;                         // morale: at or under this two days running, a strike
        public const int StrikeLeast = 2, StrikeMost = 3;       // working days
        public const int MoraleAfterStrike = 40;
        public const int LeaveAt = 10;                          // morale: at or under this, the crew walks off
        public const int RaisePermille = 50;                    // a raise to a crew's day rate
        public const int PoachPermille = 60;                    // a week, at a skill of 60 and a rival of five crews
        public const int PoachPayLeast = 100, PoachPayMost = 200;   // thousandths more pay a rival offers
        public const int PoachDays = 3;                         // days to match it
        public const int ReleaseDays = 2;                       // days' pay when a crew is let go
        public const int CrewsMost = 12;

        // ------------------------------------------------------------------ the company

        public const int ReputationStart = 30;
        public const int RepOnTime = 4, RepSlightlyLate = 2, RepLateStep = 1, RepLateMost = 6, RepQuality = 2, RepPassed = 1, RepFailed = 1;
        public const int RepDefect = 6, RepClaimLost = 4, RepBribe = 15, RepTenderWon = 1, RepContractOnTime = 3;
        public const int QualityRepFrom = 85;
        public const int SpecialityRest = 30;                   // days between two changes of speciality
        public const int SpecialPacePermille = 100;             // more pace on the speciality's work
        public const int SpecialCostPermille = 50;              // less materials cost on it
        public const int SpecialBidPermille = 30;               // a better tender score on it

        public static readonly int[] CoverDayPerMillion = { 0, 50, 90 };   // money a day a million of works under way, by cover
        public const int CoverLeast = 40;                       // money a day at least
        public static readonly int[] CoverTheftPermille = { 0, 500, 800 };
        public static readonly int[] CoverClaimPermille = { 0, 400, 700 };
        public static readonly bool[] CoverStorm = { false, false, true };
        public const int CoverExcess = 5000;

        public const int StaffMost = 40;
        public const int ProjectsMost = 24;                     // the player's projects kept, finished ones included
        public const int BuildingMost = 4;                      // the player's sites going up at once
        public const int LotsMost = 40;
        public const int SiteSpacesMost = 7;                    // spaces one site joins at most: the widest size the district's grid takes (P1's)
        public const float SiteSliver = 1f;                     // m of a site its building may leave: no gap to the eye (Uptown's SpacesRules.FillSliver)

        /// <summary>
        /// True when a site of <paramref name="siteSpaces"/> spaces offers a design of a size taking <paramref name="sizeSpaces"/>:
        /// exactly the site's, or fewer when the design's frontage alone fills the site (<paramref name="fills"/>), so no ground
        /// is left beside a building inside its site. As Uptown's SpacesRules.Offered; the checks hold the two together.
        /// </summary>
        public static bool Offered(int siteSpaces, int sizeSpaces, bool fills)
        {
            return sizeSpaces > 0 && siteSpaces > 0 && (sizeSpaces == siteSpaces || (sizeSpaces < siteSpaces && fills));
        }

        // ------------------------------------------------------------------ money

        public const int LoanToCostPermille = 650;              // of the land and the build's estimate
        public const double LoanPointsOver = 1.5;               // per cent a year over the business loan's rate
        public const double LoanRateDefault = 10.0;             // per cent a year when the credit side gives none
        public const int LoanFeePermille = 10;
        public static readonly int[] LoanDrawPermille = { 250, 300, 300, 150 };   // drawn at the start, and after each inspection
        public const int LoanYears = 2;                         // game years to repay from completion
        public const int LoanFromSalesPermille = 700;           // of each flat sold, to the loan while it is owed

        public const int InvestorCount = 3;
        public static readonly int[] InvestorSharePermille = { 200, 280, 350 };   // of the estimate they put in
        public static readonly int[] InvestorReturnPermille = { 160, 200, 240 };  // more than they put in, owed at completion
        public const int InvestorPayDays = 30;                  // days after completion to pay them

        public const int FlatArea = 85;                         // m2 of floor a flat
        public const int FlatsLeast = 2;
        public const int OffPlanPermille = 900;                 // of a flat's value: sold before it is built
        public const int DepositPermille = 100;
        public static readonly int[] AskPermille = { 900, 1000, 1100 };            // the asking price, of a flat's value
        public static readonly int[] AskChancePermille = { 650, 450, 250 };        // a day, a buyer at that price
        public const int QualityPricePermille = 1;              // of the price a point of quality over 60 (and less under)

        // ------------------------------------------------------------------ show flats

        public const int ShowUpkeepDay = 240;                   // money a day the sales suite costs while open: its rent, power and cleaning
        public const int AgentDay = 210;                        // money a day for a sales agent from the estate agency
        public static readonly int[] ViewingHours = { 11, 14, 17 };   // the hours buyers come to see the show flat, in order
        public const int ViewingsLeast = 1;                     // viewings a day with the show flat open
        public const int ViewingOver = 10, ViewingWellOver = 25;   // points over the district's standard: a viewing more for each
        public const int ShowBuyerBase = 170;                   // per mille a viewing brings a buyer, a show flat at the district's standard
        public const int ShowBuyerPerPoint = 6;                 // per mille more for each point over the standard, less under
        public const int ShowBuyerRepPerPoint = 2;              // per mille more for each point of the company's reputation over 50
        public const int ShowBuyerLeast = 30, ShowBuyerMost = 560;
        public const int ShowShownPermille = 150;               // per mille more when the player shows the buyers round
        public const int ShowAgentPermille = 90;                // per mille more when the agent does
        public const int ShowAloneShare = 450;                  // thousandths of the chance with nobody there to show them round
        public const int ShowPricePerPoint = 3;                 // thousandths of the off plan price more for each point over the standard, less under
        public const int ShowPriceLeast = 900, ShowPriceMost = 1120;   // thousandths of the off plan price a show flat's buyer pays

        // ------------------------------------------------------------------ refits

        public const int RefitHoursBase = 16;                   // working hours a refit takes, and one more for each so many m2
        public const int RefitSqmPerHour = 6;
        public const int RefitHoursLeast = 24, RefitHoursMost = 160;
        public const int RefitHold = 3;                         // hours a refit's closure is held at a time, held again each hour
        public static readonly int[] RefitGradeFrom = { 0, 60, 80 };   // the work's quality from which a refit is plain, good or fine
        public const int ContractorGrade = 1;                   // a contractor's refit: good
        public const int RefitEveryLeast = 5, RefitEveryMost = 9;      // days between two businesses' refit tenders

        // ------------------------------------------------------------------ a builder hired by the player

        public const int HireDepositPermille = 100;             // of the contract's price, paid as it is signed
        public static readonly int[] HireStagePermille = { 250, 300, 300 };   // paid as each inspection passes: the foundations, the frame, the final
        public const int HirePenaltyDayPermille = 5;            // of the price the builder pays back for each day it hands over late
        public const int HirePenaltyMostPermille = 100;         // the most it pays back: a tenth of the price, the usual cap on such damages
        public const int HirePaceLeast = 400, HirePaceMost = 1600;   // thousandths of the city's pace a hired builder's crews work at
        public const int HireLuckLeast = 850, HireLuckMost = 1050;   // thousandths of its quoted pace its crews really keep, before its standing

        public const int LandTaxPermille = 12;                  // of the land's price a year
        public const int VacantTaxPermille = 25;                // more a year, on land kept empty
        public const int VacantAfter = 30;                      // days without works before land counts as empty
        public const int TaxEvery = 7;                          // days between two land tax bills

        // ------------------------------------------------------------------ tenders

        /// <summary>Money a working hour of the city's sites pays, by the programme's kind (roadworks, scaffold, refit, utility, rebuild, plot, demolition).</summary>
        public static readonly int[] WorksRate = { 1900, 1500, 1700, 2200, 3800, 4200, 3000, 2400 };

        /// <summary>Materials a working hour of those sites takes, by material, in thousandths of a load.</summary>
        public static readonly int[][] WorksLoads =
        {
            new[] { 40, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 20, 30 },
            new[] { 0, 20, 0, 0, 0 },
            new[] { 0, 0, 30, 20, 20 },
            new[] { 30, 20, 20, 10, 10 },
            new[] { 0, 0, 0, 0, 0 },     // a demolition takes nothing in: its rubble goes out (RedevelopRules.RubbleCost)
            new[] { 60, 8, 0, 0, 0 }     // a car park: the surface's cement, the poles' and the barrier's steel
        };

        /// <summary>People of each trade the city's works need at their usual pace, by the programme's kind (roadworks to plot).</summary>
        public static readonly int[][] WorksCrew =
        {
            new[] { 4, 1, 0, 0, 0 },
            new[] { 3, 1, 0, 0, 0 },
            new[] { 1, 2, 0, 1, 1 },
            new[] { 2, 0, 0, 1, 2 },
            new[] { 2, 3, 0, 1, 1 },
            new[] { 2, 3, 1, 1, 1 },
            new[] { 4, 1, 1, 0, 0 },     // labourers, a mason for the walls, a crane operator for the machines and the skip
            new[] { 4, 1, 0, 1, 0 }      // a car park: labourers, a mason for the kerbs, an electrician for the lamps and the barrier
        };

        /// <summary>Plant the city's works need the whole time, by the programme's kind (crane, digger, mixer, scaffold).</summary>
        public static readonly int[][] WorksPlant =
        {
            new[] { 0, 1, 1, 0 },
            new[] { 0, 0, 0, 1 },
            new[] { 0, 0, 0, 1 },
            new[] { 0, 1, 0, 0 },
            new[] { 0, 0, 1, 1 },
            new[] { 1, 1, 1, 1 },
            new[] { 0, 1, 0, 1 },        // the digger and the scaffold the building is wrapped in
            new[] { 0, 1, 1, 0 }         // a car park: the digger and the mixer
        };

        public const int TenderHours = 30;                      // hours a tender is open: bids close the next day
        public const int ClientEveryLeast = 6, ClientEveryMost = 10;   // days between two clients' tenders
        public const int PublicEveryLeast = 15, PublicEveryMost = 25;  // days between two public buildings' tenders
        public const int PublicMarginPermille = 220;            // the city pays the cost and this much more
        public const int LandmarkPremiumPermille = 400;         // a landmark's works cost this much more again: a signature design, its finish, its programme
        public const int LandmarkTenderHours = 96;              // hours a landmark's tender is open: four days for so large a bid
        public const int BidLeastPermille = 850, BidMostPermille = 1300, BidStepPermille = 50;
        public const int FastPermille = 800;                    // a fast bid's time, against the usual
        public const int ScorePricePermille = 700, ScoreTimePermille = 100, ScoreRepPermille = 200;
        public const int TendersKept = 24;

        // ------------------------------------------------------------------ rivals and the city's developers

        public const int RivalCount = 8;

        /// <summary>The city's contractors (Civic's names, the same order): their speciality, standing, margin and size.</summary>
        public static readonly Speciality[] RivalSpeciality =
        {
            Speciality.Roads, Speciality.Demolition, Speciality.Renovation, Speciality.Homes, Speciality.Roads, Speciality.Renovation, Speciality.Offices, Speciality.Homes
        };

        public static readonly int[] RivalReputation = { 62, 55, 50, 66, 58, 60, 70, 64 };
        public static readonly int[] RivalMarginPermille = { 60, 50, 40, 70, 50, 60, 80, 50 };
        public static readonly int[] RivalCrews = { 5, 4, 3, 6, 4, 3, 7, 4 };

        public static readonly string[] RivalKeys =
        {
            Prefix + "_civic_contractor_1", Prefix + "_civic_contractor_2", Prefix + "_civic_contractor_3", Prefix + "_civic_contractor_4",
            Prefix + "_civic_contractor_5", Prefix + "_civic_contractor_6", Prefix + "_civic_contractor_7", Prefix + "_civic_contractor_8"
        };

        // Each rival's office: a business of the construction company's type in one of Uptown's leased office lots, the rival's
        // index its lease's (Uptown's BuildingsRules.Leased, eight of them, the same number as the rivals).
        public const int OfficeDayPerCrew = 1200;                       // dollars a day an office is valued at for each crew its builder runs: what the game's valuation of an AI business averages
        public const string OfficeAboutKey = Prefix + "_bld_office_about";   // BizMan's description of a rival's office

        public const int DevSharePermille = 500;                // of the district's empty lots the city's developers build on; the rest wait for the player
        public const int DevFirstDay = 10;                      // the game day the first development may start
        public const int DevSpacing = 3;                        // days between two developments starting
        public const int DevDaysLeast = 9, DevDaysSpread = 8;   // days a development takes: 9 to 16
        public const int DevSitesMost = 5;                      // developments shown going up at once
        public const int SitesMost = 16;                        // every site of the city at once: what a Going Public host's line carries

        // ------------------------------------------------------------------ names

        public static readonly string[] FirmKeys = { Prefix + "_bld_firm_0", Prefix + "_bld_firm_1", Prefix + "_bld_firm_2" };
        public static readonly string[] MerchantKeys = { Prefix + "_bld_merchant_0", Prefix + "_bld_merchant_1", Prefix + "_bld_merchant_2" };
        public static readonly string[] InvestorKeys = { Prefix + "_bld_investor_0", Prefix + "_bld_investor_1", Prefix + "_bld_investor_2" };
        public const string EngineerFirmKey = Prefix + "_bld_engineer_firm";
        public const string PlantFirmKey = Prefix + "_bld_plant_firm";

        public static readonly string[] ForemanKeys =
        {
            Prefix + "_bld_foreman_0", Prefix + "_bld_foreman_1", Prefix + "_bld_foreman_2", Prefix + "_bld_foreman_3", Prefix + "_bld_foreman_4", Prefix + "_bld_foreman_5",
            Prefix + "_bld_foreman_6", Prefix + "_bld_foreman_7", Prefix + "_bld_foreman_8", Prefix + "_bld_foreman_9", Prefix + "_bld_foreman_10", Prefix + "_bld_foreman_11"
        };

        public static readonly string[] ClientKeys =
        {
            Prefix + "_bld_client_0", Prefix + "_bld_client_1", Prefix + "_bld_client_2", Prefix + "_bld_client_3", Prefix + "_bld_client_4", Prefix + "_bld_client_5"
        };

        public static readonly string[] PublicKeys = { Prefix + "_bld_public_0", Prefix + "_bld_public_1", Prefix + "_bld_public_2", Prefix + "_bld_public_3" };

        public static readonly string[] MaterialKeys =
        {
            Prefix + "_bld_mat_cement", Prefix + "_bld_mat_steel", Prefix + "_bld_mat_bricks", Prefix + "_bld_mat_glass", Prefix + "_bld_mat_timber"
        };

        public static readonly string[] PlantKeys = { Prefix + "_bld_plant_crane", Prefix + "_bld_plant_digger", Prefix + "_bld_plant_mixer", Prefix + "_bld_plant_scaffold" };

        public static readonly string[] TradeKeys =
        {
            Prefix + "_bld_trade_labourer", Prefix + "_bld_trade_mason", Prefix + "_bld_trade_crane_operator", Prefix + "_bld_trade_electrician", Prefix + "_bld_trade_plumber"
        };

        /// <summary>The trades' ids, shared with the trades' careers (Scripts/BuildTrades: TradeRoles) so a role reads the same in both.</summary>
        public static readonly string[] TradeIds = { "labourer", "mason", "crane_operator", "electrician", "plumber" };

        public static readonly string[] RoleKeys =
        {
            Prefix + "_bld_role_none", Prefix + "_bld_role_architect", Prefix + "_bld_role_engineer", Prefix + "_bld_role_estimator", Prefix + "_bld_role_site_manager"
        };

        /// <summary>What each role does for the company, in words for its page.</summary>
        public static readonly string[] RoleNoteKeys =
        {
            Prefix + "_bld_role_note_none", Prefix + "_bld_role_note_architect", Prefix + "_bld_role_note_engineer", Prefix + "_bld_role_note_estimator",
            Prefix + "_bld_role_note_site_manager"
        };

        /// <summary>The roles' ids, shared with the trades' careers (TradeRoles) where they have the same role.</summary>
        public static readonly string[] RoleIds = { "none", "architect", "engineer", "estimator", "site_manager" };

        public static readonly string[] GangKeys =
        {
            Prefix + "_bld_gang_ground", Prefix + "_bld_gang_concrete", Prefix + "_bld_gang_frame", Prefix + "_bld_gang_fitout", Prefix + "_bld_gang_general"
        };

        public static readonly string[] StageKeys = { Prefix + "_bld_stage_ground", Prefix + "_bld_stage_foundation", Prefix + "_bld_stage_frame", Prefix + "_bld_stage_envelope" };
        public static readonly string[] InspectionKeys = { Prefix + "_bld_insp_foundation", Prefix + "_bld_insp_frame", Prefix + "_bld_insp_final" };

        public static readonly string[] SpecialityKeys =
        {
            Prefix + "_bld_spec_none", Prefix + "_bld_spec_homes", Prefix + "_bld_spec_offices", Prefix + "_bld_spec_roads", Prefix + "_bld_spec_renovation", Prefix + "_bld_spec_demolition"
        };

        public static readonly string[] CoverKeys = { Prefix + "_bld_cover_none", Prefix + "_bld_cover_standard", Prefix + "_bld_cover_full" };

        /// <summary>A lot's class in words: small, office, big, warehouse (LotView.Class).</summary>
        public static readonly string[] ClassKeys = { Prefix + "_bld_class_small", Prefix + "_bld_class_office", Prefix + "_bld_class_big", Prefix + "_bld_class_warehouse" };

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;
    }
}
