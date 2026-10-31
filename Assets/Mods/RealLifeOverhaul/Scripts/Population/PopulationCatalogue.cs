using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>A household's class, as the game's districts give their people (NeighborhoodData's working, middle and upper class shares). Saved as a number.</summary>
    public enum PopClass : byte
    {
        Working = 0,
        Middle = 1,
        Upper = 2
    }

    /// <summary>What a person does with their days. Saved as a number: never renumber.</summary>
    public enum Status : byte
    {
        Child = 0,        // under school age
        Pupil = 1,        // at school
        Student = 2,      // at university
        Worker = 3,       // in a job
        Unemployed = 4,   // looking for one
        Retired = 5,      // on a pension
        Home = 6          // out of the labour force by choice: caring for a family, or living on their means
    }

    /// <summary>The highest schooling a person has finished. Saved as a number.</summary>
    public enum Education : byte
    {
        None = 0,
        School = 1,        // a school diploma
        College = 2,       // a vocational or associate qualification
        Degree = 3,        // a bachelor's degree
        Postgraduate = 4   // a master's or a doctorate
    }

    /// <summary>The kind of work a person does or seeks; employers' slots are of one. Saved as a number.</summary>
    public enum Field : byte
    {
        None = 0,
        Service = 1,       // shops, food, cleaning, care of people
        Trades = 2,        // building, repairs, warehouses, driving
        Office = 3,        // administration, sales, clerical
        Professional = 4,  // law, finance, consultancy
        Tech = 5,          // software, data, engineering
        Creative = 6,      // design, media, the stage
        Public = 7,        // teaching, health, the council
        Management = 8     // managers and executives
    }

    /// <summary>What a building is used for, as the population counts it. Saved as a number.</summary>
    public enum Use : byte
    {
        None = 0,
        Homes = 1,
        Shop = 2,
        Office = 3,
        Warehouse = 4,
        Cinema = 5,
        Theatre = 6,
        Special = 7        // one of the game's special services (a hospital, a school, a bank, a gas station...)
    }

    /// <summary>
    /// What a household spends on, about twenty kinds, each mapped to the businesses that sell it (<see cref="PopulationCatalogue.CategoryOfType"/>).
    /// Saved as a number in the ledger: never renumber.
    /// </summary>
    public enum Spend : byte
    {
        Rent = 0,
        Utilities = 1,
        Groceries = 2,
        EatingOut = 3,
        Coffee = 4,
        Clothing = 5,
        Electronics = 6,
        Furniture = 7,
        BooksGifts = 8,
        Flowers = 9,
        Jewellery = 10,
        Gym = 11,
        Cinema = 12,
        Theatre = 13,
        NightLife = 14,
        Casino = 15,
        Transport = 16,
        PersonalCare = 17,
        Professional = 18,
        Digital = 19,
        Saving = 20
    }

    /// <summary>Who an employer is. Saved as a number.</summary>
    public enum EmployerKind : byte
    {
        Business = 0,      // the business of a registration of the game's: the player's, the city's own or a rival's
        Tenants = 1,       // the firms on the other floors of an office building (the game lets one business a building)
        Public = 2,        // the city's public places: a school, the hospital, the tax office
        Special = 3,       // one of the game's special services
        Building = 4,      // a residential building's own staff (doormen, superintendents)
        Mod = 5            // one of the mod's own firms (a tech company's tower, the university, City Utilities)
    }

    /// <summary>The systems whose people are residents too, by the link a person record keeps to them. Saved as a number.</summary>
    public enum LinkKind : byte
    {
        None = 0,
        Employee = 1,      // a member of the game's staff at one of the player's businesses (EmployeeInstance.id)
        Professional = 2,  // one of the tech sector's people (Tech/People)
        Seeker = 3,        // a jobseeker on the tech sector's hiring list (Tech/Hiring)
        Partner = 4,       // the player's partner (Social)
        Roommate = 5,      // a roommate at the player's home (Roommates)
        Staff = 6,         // household staff at the player's home (Household)
        Met = 7,           // somebody the player has met (Social)
        Child = 8,         // one of the player's children (Family)
        Player = 9         // the player
    }

    /// <summary>
    /// Every figure of the population (plans/tech-overhaul/POPULATION.md). Game-free. Money is cents unless named otherwise
    /// (savings and debt are whole money); a share is thousandths; rates by the year are turned into days by the save's year
    /// (the game's year is 60 to 365 days). Each table says where its figures come from; where the game gives a figure the
    /// game's is used, and the real world's shape is laid over the game's own scale of money (its wages and rents).
    /// </summary>
    public static class PopulationCatalogue
    {
        public const int Classes = 3;
        public const int Categories = 21;                // the kinds of spending, Rent to Saving
        public const int Fields = 9;
        public const int ShopCategoryFirst = (int)Spend.Groceries;   // the kinds sold by businesses: Groceries to Professional
        public const int ShopCategoryLast = (int)Spend.Professional;

        // ---- the measure (step 1): homes and jobs from the game's buildings

        public const int HomeNetShare = 800;             // thousandths of a residential building's floor that is flats: halls, stairs and walls take the rest (New York runs 75 to 85% net)
        public const int FlatSqmDefault = 80;            // m2 of a flat where the game gives no unit size for a building's size
        public const int OfficeNetShare = 800;           // thousandths of an office building's floor that is workspace
        public const int DeskSqm = 16;                   // m2 of office floor a desk (New York offices give 14 to 18 m2 a worker; the design's 15 to 20)
        public const int ShopSqmPerJob = 28;             // m2 of a shop's sales floor a job where its kind is not known (US retail: about 300 sq ft an employee)
        public const int ShopJobsLeast = 2;              // every open shop has at least this many people working in it
        public const int WarehouseSqmPerJob = 120;       // m2 of warehouse floor a job (US warehousing: 1,000 to 1,500 sq ft a worker)
        public const int CinemaJobs = 30;                // people working at a cinema (a multiplex's ushers, projection, concessions)
        public const int TheatreJobs = 60;               // ... at a theatre (a Broadway house's front of house, crew and cast)
        public const int FlatsPerBuildingJob = 40;       // flats a residential building's staff job serves (doormen and superintendents)
        public const int SpecialJobsLeast = 4;           // a special service's people at the least

        /// <summary>The game's special services and the m2 of their floor a job, by their business type (a hospital is about 50 m2 a job, a school 60).</summary>
        public static readonly (string Type, int SqmPerJob, Field Field, EmployerKind Kind)[] SpecialDensity =
        {
            ("ba:businesstype_hospital", 50, Field.Public, EmployerKind.Public),
            ("ba:businesstype_clinic", 45, Field.Public, EmployerKind.Public),
            ("ba:businesstype_school", 60, Field.Public, EmployerKind.Public),
            ("ba:businesstype_irs", 22, Field.Office, EmployerKind.Public),
            ("ba:businesstype_bank", 25, Field.Professional, EmployerKind.Special),
            ("ba:businesstype_recruitmentagency", 22, Field.Office, EmployerKind.Special),
            ("ba:businesstype_marketingagency", 22, Field.Creative, EmployerKind.Special),
            ("ba:businesstype_interiorinstallationfirm", 60, Field.Trades, EmployerKind.Special),
            ("ba:businesstype_importexport", 120, Field.Trades, EmployerKind.Special),
            ("ba:businesstype_wholesalestore", 90, Field.Trades, EmployerKind.Special),
            ("ba:businesstype_truckgarage", 90, Field.Trades, EmployerKind.Special),
            ("ba:businesstype_movingservice", 90, Field.Trades, EmployerKind.Special),
            ("ba:businesstype_privatedriverservice", 60, Field.Trades, EmployerKind.Special),
            ("ba:businesstype_gasstation", 80, Field.Service, EmployerKind.Special),
            ("ba:businesstype_cardealership", 80, Field.Office, EmployerKind.Special),
            ("ba:businesstype_casino", 40, Field.Service, EmployerKind.Special),
        };

        public const int SpecialSqmPerJobDefault = 45;   // m2 a job at a special service not in that table (a shop-like one)

        // ---- the city's districts

        public const int DistrictsMost = 32;             // districts the population keeps at most: New York has nine with the mod's two
        public const int TargetVacancy = 50;             // thousandths of a district's flats kept free when it is generated (the game's ideal share of residential buildings to let, 5%)

        /// <summary>
        /// How a flat's size leans the class of who lives in it, thousandths on the district's own shares (working, middle, upper):
        /// a studio is for the working and the young, a penthouse for the upper class. By the game's residential sizes.
        /// </summary>
        public static readonly (int Sqm, int Working, int Middle, int Upper)[] ClassLean =
        {
            (60, 1400, 1000, 400),     // B: 54 m2
            (120, 1000, 1100, 800),    // F: 96 m2
            (300, 500, 1000, 1600),    // L: 204 m2
            (int.MaxValue, 150, 600, 2200),   // N: 589 m2, and a villa below
        };

        public static readonly int[] VillaLean = { 0, 300, 3000 };   // a Hamptons villa

        /// <summary>
        /// Household size by flat, per mille for 1 to 6 people (Manhattan has 2.0 people a household and nearly half live alone:
        /// US Census 2020, New York County; leaned by the flat's size).
        /// </summary>
        public static readonly (int Sqm, int[] Sizes)[] HouseholdSizes =
        {
            (60, new[] { 620, 320, 60, 0, 0, 0 }),
            (120, new[] { 300, 340, 180, 130, 50, 0 }),
            (300, new[] { 140, 300, 220, 220, 90, 30 }),
            (int.MaxValue, new[] { 180, 360, 200, 170, 90, 0 }),
        };

        public static readonly int[] VillaSizes = { 0, 200, 220, 300, 200, 80 };

        // Households of two: a couple, a parent and child, or two sharing (per mille); of three or more: a couple with children, a lone parent with them, or
        // others sharing (US Census household types, New York County, rounded).
        public const int TwoCouple = 680, TwoParent = 120;            // the rest share
        public const int MoreCouple = 760, MoreParent = 150;          // the rest share

        public const int AdultLeast = 18;               // years
        public const int SchoolFrom = 5;                // years: children start school
        public const int SchoolTo = 18;                 // years: they leave it with a diploma or none
        public const int UniversityYears = 4;           // a degree
        public const int PostgraduateYears = 2;
        public const int RetireAge = 65;                // years: a pension from here (POPULATION.md section 4)
        public const int OldestAge = 105;               // years: nobody lives past it
        public const int ParentAgeLeast = 20;           // a parent's years at the least over their child
        public const int ParentAgeMost = 42;

        /// <summary>University by class: per mille of school leavers who go (working, middle, upper).</summary>
        public static readonly int[] UniversityShare = { 330, 580, 800 };

        /// <summary>Of graduates, per mille who go on to a master's or doctorate.</summary>
        public static readonly int[] PostgraduateShare = { 150, 280, 420 };

        /// <summary>Of school leavers who do not go to university, per mille who take a vocational qualification.</summary>
        public static readonly int[] CollegeShare = { 300, 400, 350 };

        /// <summary>Per mille who leave school with no diploma (New York's high school completion is about 87%).</summary>
        public static readonly int[] DropOutShare = { 160, 60, 20 };

        /// <summary>Per mille of adults aged 25 to 64 in the labour force, by class (the rest care for a family or live on their means).</summary>
        public static readonly int[] Participation = { 760, 840, 780 };

        /// <summary>Per mille of those over 65 still working.</summary>
        public const int WorkingOld = 170;

        public const int UnemploymentStart = 55;        // thousandths of the labour force out of work when a city is generated (New York City, 2024: about 5%)

        // ---- ages: the share of people by age band when a city is generated, to shape who lives in each household (US Census 2020, New York County)

        /// <summary>The age of a lone adult: per mille in each band from 18 (18-24, 25-34, 35-44, 45-54, 55-64, 65-74, 75-89).</summary>
        public static readonly int[] SingleAges = { 90, 250, 140, 110, 120, 140, 150 };

        /// <summary>The age of the older adult of a couple without children at home (bands as above).</summary>
        public static readonly int[] CoupleAges = { 20, 190, 130, 130, 160, 200, 170 };

        public static readonly int[] AgeBands = { 18, 25, 35, 45, 55, 65, 75, 90 };

        // ---- pay: the game's own scale of wages, an hour (SkillData.baseHourlyWage: cleaning 12, customer service 16, delivery 18;
        // times 1 + skill^1.05 / 100: docs/features/jobs.md), with the professions above them as the Careers app pays

        /// <summary>A field's hourly pay at no experience, cents.</summary>
        public static readonly int[] FieldPay = { 0, 1400, 1900, 2100, 4200, 4000, 2800, 2600, 5800 };

        /// <summary>Per mille more a class earns in the same field and skill (the upper class's pay is at the firms that pay most).</summary>
        public static readonly int[] ClassPay = { 900, 1000, 1700 };

        public const int SkillBoostMost = 1270;         // thousandths more at skill 100: the game's 100^1.05 / 100
        public const int FullTimeHours = 40;            // hours a week
        public const int PartTimeHours = 22;
        public const int PartTimeShare = 170;           // thousandths of jobs that are part time (US: about 17%)
        public const int DaysPerWeek = 7;

        /// <summary>
        /// The fields a person of an education works in, per mille (none, school, college, degree, postgraduate) over the
        /// fields Service to Management (US Bureau of Labor Statistics occupations by education, rounded).
        /// </summary>
        public static readonly int[][] FieldByEducation =
        {
            new[] { 0, 560, 300, 90, 0, 0, 20, 30, 0 },
            new[] { 0, 400, 260, 220, 20, 20, 30, 40, 10 },
            new[] { 0, 220, 300, 220, 40, 80, 50, 70, 20 },
            new[] { 0, 60, 40, 170, 200, 170, 110, 150, 100 },
            new[] { 0, 20, 10, 60, 280, 200, 90, 180, 160 },
        };

        // ---- money: what is earned, taxed and spent

        /// <summary>
        /// Income tax by the day's gross income, cents: each step's rate in thousandths on the income above its start (federal, state and city
        /// together: an effective rate of about 10% at low pay rising to some 40% at the top, as a New York City earner pays).
        /// </summary>
        public static readonly (int From, int Rate)[] IncomeTax = { (0, 80), (10000, 180), (30000, 280), (100000, 380) };

        public const int SalesTax = 89;                  // thousandths of a price that is sales tax (New York City: 8.875%)
        public const int PropertyTaxShare = 150;         // thousandths of a rent that goes to the council as property tax
        public const int OwnerCostShare = 300;           // thousandths of the market rent an owner pays for upkeep and tax instead of rent
        public const int MortgageShare = 500;            // thousandths of the market rent a mortgaged owner pays the lender on top
        public static readonly int[] OwnerShare = { 50, 250, 500 };   // thousandths of households owning their flat, by class (Manhattan: about a quarter own)

        public const int BenefitShare = 400;             // thousandths of the last pay an unemployed person draws (New York: about half, capped)
        public const int BenefitMost = 15000;            // cents a day at most
        public const int BenefitDays = 182;              // game days it is paid for at most in a year of 365, scaled to the save's year (26 weeks)
        public const int PensionShare = 400;             // thousandths of the last pay a pension is
        public const int PensionLeast = 6000;            // cents a day at the least (the state pension's floor)
        public const int ChildBenefit = 800;             // cents a day for each child under 18 in a household with low pay
        public const int ChildBenefitBelow = 15000;      // cents a day of household pay under which it is paid
        public const int ReturnPoints = 300;             // hundredths of a per cent a year earned on savings over the bank rate's part (3%)

        /// <summary>Savings when a city is generated, in days of the household's pay, by class: the least and the most (US Federal Reserve SCF shape).</summary>
        public static readonly (int Least, int Most)[] SavingsDays = { (3, 40), (30, 240), (200, 2400) };

        /// <summary>Debt when generated, in days of pay, by class: the most (card and car debt; a mortgage is the owner's cost).</summary>
        public static readonly int[] DebtDays = { 60, 90, 120 };

        /// <summary>Per mille of households with a car, by class (Manhattan: about 22% of households; the Hamptons nearly all).</summary>
        public static readonly int[] CarShare = { 150, 280, 560 };

        public const int CarShareHamptons = 950;

        /// <summary>
        /// Spending: each kind's share of what a household has after tax and rent, thousandths, at three levels of income against the
        /// city's middle (half, the middle, three times), drawn between them on a log scale. Poorer households spend more of it on food,
        /// utilities and getting about, richer on eating out, the home, jewellery and services, and save the rest (US Bureau of Labor
        /// Statistics, Consumer Expenditure Survey 2022, shares by income quintile; Engel's law). Rent and Saving are not in the table:
        /// rent is the flat's, saving is what is left.
        /// </summary>
        public static readonly int[][] SpendShares =
        {
            //          rent util groc  eat coff cloth elec furn book flow jewel gym cine thea night casi tran care prof digi save
            new[] {     0, 128, 309,  81,  24,  54,  34,  24,  22,   4,   4,  16,  13,   3,  24,   5, 161,  19,  11,  24,   0 },   // half the city's middle: 960 spent, little saved
            new[] {     0,  87, 174, 106,  27,  56,  37,  35,  25,   6,  11,  20,  12,   7,  27,   6, 124,  20,  20,  27,   0 },   // the middle: 827 spent
            new[] {     0,  35,  70,  95,  20,  55,  32,  38,  24,   9,  30,  20,   7,  16,  22,   9,  75,  20,  32,  22,   0 },   // three times the middle: 631 spent, a third saved
        };

        public const int EssentialsKinds = 3;            // Utilities, Groceries and Transport come first when money is short

        public const int TasteTilt = 300;                // thousandths a kind is spent more or less by a household that likes it or not

        // ---- businesses: what a customer spends, by business type, whole money (the game's prices of its goods, a typical basket)

        /// <summary>
        /// Each business type of the game's and the mod's: its kind of spending, a customer's typical basket in whole money, and the m2 of its
        /// shop floor a job (food service is dense: about 12 to 15 m2 a job; a furniture store sparse; 0 where the building's use decides).
        /// </summary>
        public static readonly (string Type, Spend Kind, int Basket, int SqmPerJob)[] Types =
        {
            ("ba:businesstype_supermarket", Spend.Groceries, 32, 30),
            ("ba:businesstype_fruitandvegetablestore", Spend.Groceries, 18, 35),
            ("ba:businesstype_liquorstore", Spend.Groceries, 28, 50),
            ("reallife_tobacconist", Spend.Groceries, 16, 40),
            ("ba:businesstype_fastfoodrestaurant", Spend.EatingOut, 14, 12),
            ("reallife_icecreamparlour", Spend.EatingOut, 9, 14),
            ("ba:businesstype_coffeeshop", Spend.Coffee, 7, 14),
            ("reallife_teabar", Spend.Coffee, 7, 14),
            ("ba:businesstype_clothingstore", Spend.Clothing, 60, 40),
            ("ba:businesstype_electronicsstore", Spend.Electronics, 180, 45),
            ("ba:businesstype_appliancestore", Spend.Electronics, 240, 50),
            ("ba:businesstype_furniturestore", Spend.Furniture, 260, 70),
            ("ba:businesstype_interiorinstallationfirm", Spend.Furniture, 900, 0),
            ("ba:businesstype_bookstore", Spend.BooksGifts, 24, 45),
            ("ba:businesstype_giftshop", Spend.BooksGifts, 30, 40),
            ("ba:businesstype_officesupplystore", Spend.BooksGifts, 35, 45),
            ("ba:businesstype_florist", Spend.Flowers, 38, 30),
            ("ba:businesstype_jewelrystore", Spend.Jewellery, 420, 30),
            ("ba:businesstype_gym", Spend.Gym, 18, 60),
            ("ba:businesstype_cinema", Spend.Cinema, 16, 0),
            ("ba:businesstype_theater", Spend.Theatre, 70, 0),
            ("ba:businesstype_nightclub", Spend.NightLife, 38, 20),
            ("ba:businesstype_casino", Spend.Casino, 110, 0),
            ("ba:businesstype_gasstation", Spend.Transport, 45, 0),
            ("ba:businesstype_cardealership", Spend.Transport, 2400, 0),
            ("ba:businesstype_privatedriverservice", Spend.Transport, 40, 0),
            ("ba:businesstype_hairdresser", Spend.PersonalCare, 45, 14),
            ("ba:businesstype_lawfirm", Spend.Professional, 450, 0),
            ("ba:businesstype_travelagency", Spend.Professional, 380, 0),
            ("ba:businesstype_eventplanningagency", Spend.Professional, 600, 0),
            ("ba:businesstype_graphicdesigner", Spend.Professional, 350, 0),
            ("ba:businesstype_webdevelopmentagency", Spend.Professional, 500, 0),
            ("ba:businesstype_movingservice", Spend.Professional, 300, 0),
            ("reallife_businesstype_cardealership", Spend.Transport, 2400, 0),
            ("reallife_bankbranch", Spend.Professional, 60, 0),
        };

        /// <summary>The kind of spending a business type sells, or -1 for one nobody comes to shop at.</summary>
        public static int CategoryOfType(string type)
        {
            int i = TypeIndex(type);
            return i >= 0 ? (int)Types[i].Kind : -1;
        }

        /// <summary>A customer's typical basket at a business type, whole money; 20 for one not in the table.</summary>
        public static int BasketOfType(string type)
        {
            int i = TypeIndex(type);
            return i >= 0 ? Types[i].Basket : 20;
        }

        /// <summary>The type's place in <see cref="Types"/>, or -1.</summary>
        public static int TypeIndex(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return -1;
            }
            for (int i = 0; i < Types.Length; i++)
            {
                if (string.Equals(Types[i].Type, type, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// How far people go for each kind of spending, metres: the length over which a district's pull falls to a third
        /// (groceries round the corner, a theatre across town). Rent, utilities, digital and saving are not shopped for.
        /// </summary>
        public static readonly int[] Reach = { 0, 0, 320, 520, 260, 1500, 1500, 2600, 900, 650, 2000, 450, 1500, 3200, 1500, 5000, 1100, 650, 1600, 0, 0 };

        /// <summary>Thousandths of a working household's spending of each kind done near work instead of home (lunch, coffee, the shops on the way).</summary>
        public static readonly int[] NearWork = { 0, 0, 120, 450, 550, 150, 80, 20, 150, 120, 60, 200, 60, 80, 180, 20, 100, 120, 250, 0, 0 };

        /// <summary>Per mille of a business's takings that is value added once its goods are paid for (retail margins: groceries thin, services almost all).</summary>
        public static readonly int[] ValueAdded = { 0, 0, 250, 600, 700, 450, 250, 350, 450, 550, 500, 850, 750, 800, 750, 800, 300, 850, 900, 0, 0 };

        public const int FirmMarkup = 300;               // thousandths over its wage bill a firm's output is worth (offices, the mod's firms)

        // ---- the share of a pool among the businesses in reach

        public static readonly int[] PriceElasticity = { 2200, 1400, 700 };   // thousandths of share lost for each thousandth a price is over what a class bears, by class
        public const int QualityWeight = 500;            // thousandths of share each half of the interior's range is worth
        public const int SatisfactionWeight = 600;       // thousandths of share each half of satisfaction's range is worth
        public const int AttentionLeast = -900;          // thousandths: attention takes a business's pull down to a tenth at worst
        public const int AttentionMost = 9000;           // thousandths: a sane bound for the arithmetic; a pool is only ever shared out, so it never runs away
        // Thousandths of yesterday's share kept by each kind: people go back where they went.
        public static readonly int[] Habit = { 0, 0, 650, 350, 600, 300, 250, 200, 300, 300, 300, 700, 250, 200, 300, 400, 550, 600, 500, 0, 0 };

        // ---- the region round the city (POPULATION.md section 5): commuters and visitors, an aggregate

        public const int CommuterSpend = 1600;           // cents a day a commuter spends in the city on a working day (lunch, coffee, a shop near work)
        public const int CommuterFill = 120;             // thousandths of the vacancies the region fills a day while it has people to send
        public const int RegionPool = 400000;            // the region's people who could take a job in the city (the boroughs and the suburbs round Manhattan hold millions)
        public const int RegionWorkersShare = 600;       // thousandths of them in work elsewhere: the city draws from the rest first

        /// <summary>Visitors a day for each unit of a district's pull (its shops' room for customers times the game's customer weight), and what each spends, cents, by kind.</summary>
        public const int VisitorsPerPull = 2250;         // thousandths of a visitor a day per unit of pull: Midtown (the game's busiest, 1,336 units) draws some three thousand a day
        public static readonly int[] VisitorSpend = { 0, 0, 300, 1800, 500, 2600, 900, 300, 900, 300, 1500, 100, 800, 2500, 1600, 900, 600, 300, 400, 0, 0 };

        // ---- life (POPULATION.md section 4): rates by the year, turned into days by the save's year

        /// <summary>
        /// Births a year per mille of women in a couple, by age band from 15 (15-19 ... 45-49): about half of Manhattan's women of those ages live with a
        /// partner, so over all women it is Manhattan's low fertility, about 1.2 children a woman (CDC; NYC DOHMH).
        /// </summary>
        public static readonly int[] Fertility = { 12, 60, 104, 140, 100, 32, 4 };

        public const int MortalityA = 32;                // millionths: Gompertz's death rate at age 0 (US life tables: about 0.13% at 40, 0.9% at 60, 6% at 80)
        public const int MortalityB = 95;                // thousandths: how fast it doubles with age (every 7.3 years)
        public const int InfantMortality = 5;            // thousandths in the first year
        public const int CoupleFormation = 90;           // per mille a year of single adults 22 to 45 who pair up
        public const int CoupleSplit = 22;               // per mille a year of couples who part
        public const int LeaveHome = 140;                // per mille a year of adult children 20 to 34 in work who leave home
        public const int Turnover = 220;                 // per mille a year of jobs that end (quits and lay offs: US JOLTS separations about 3.5% a month, half of them to another job at once)
        public const int MoveShare = 110;                // per mille a year of households who move within the city (Manhattan: about 11%)
        public const int RentBurdenMove = 450;           // thousandths of income in rent past which a household looks for a cheaper flat
        public const int RentBurdenLeave = 700;          // ... past which, with nothing cheaper free, it leaves the city
        public const int UnemployedLeaveDays = 365;      // game days out of work in a year of 365 (scaled) after which a household may leave
        public const int LeaveChance = 120;              // per mille a year such a household leaves
        public const int MigrantsPerVacancy = 400;       // thousandths of a household a day that would come to the city for each hundred open vacancies (more when it has flats to spare)
        public const int ArrivalShare = 300;             // thousandths a month of a district's free flats past its target that the region's households take (Manhattan never lacks them)

        // ---- housing (POPULATION.md section 8)

        public const int RentStep = 4;                   // thousandths a month rents move for each 10 thousandths vacancy is off its target: three points off is some 15% a year
        public const int RentLeast = 650;                // thousandths of the game's own rent a district's index goes down to
        public const int RentMost = 1800;                // ... and up to
        public const int RentResidential = 65;           // thousandths: the game's residential rent multiplier (BuildingTypeData buildingPriceMultiplierForRent)
        public const int RentPerPrice = 560;             // millionths of a square metre's price that is its rent a day (NeighborhoodData rentMultiplierPerSqmPrice)
        public const int BasePricePerSqm = 20554;        // whole money: the game's base building price a square metre (NeighborhoodData)
        public const int ResidentialPriceMultiplier = 1500;   // thousandths (BuildingTypeData buildingPriceMultiplier for residential)
        public const int AffordableShare = 350;          // thousandths of income a household is willing to pay in rent when it chooses a flat
        public const int PersonsPerRoom = 40;            // m2 of flat a person wants at the least

        // ---- the city's economy as other features read it (CityEconomy.DistrictIncome's 600 to 1400 scale)

        public const int IncomeScaleLeast = 600;
        public const int IncomeScaleMost = 1400;

        // ---- the run (POPULATION.md section 11)

        public const int PeopleStep = 600;               // people a step of the day's pass looks at (about half a millisecond)
        public const int HouseholdStep = 500;
        public const int SliceMs = 2;                    // milliseconds of the population's work a frame (Core/DayPump's slice)

        /// <summary>A share of an amount, thousandths, in whole numbers.</summary>
        public static long Share(long amount, int thousandths)
        {
            return amount * thousandths / 1000;
        }

        /// <summary>A value kept between two bounds.</summary>
        public static int Clamp(int v, int least, int most)
        {
            return v < least ? least : v > most ? most : v;
        }
    }
}
