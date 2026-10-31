using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// What one of the mod's own businesses takes from the city's people (POPULATION.md section 10; the owner, 1 Nov 2026: "everything will be either touched
    /// or changed"): a demand that was a fixed figure times the cycle, now a factor on that figure, a thousand on the first day it is measured and moving
    /// with the people after. Saved as a number in the save's line and the shared city's part: never renumber, add at the end.
    /// </summary>
    public enum PopDemand : byte
    {
        SaversPeople = 0,     // the own bank's people's savings: the households' savings
        SaversFirms = 1,      // its firms' savings: the firms' cash, by their takings and wage bills
        BorrowPoor = 2,       // what the poorly rated borrow: the incomes of the households in that band, which their borrowing follows
        BorrowFair = 3,
        BorrowGood = 4,
        BorrowShops = 5,      // what shops borrow: by their takings
        BorrowFirms = 6,      // what companies borrow: by their wage bills
        HotelGuests = 7,      // hotels' guests: business travel, the visitors the city draws, residents' own trips
        CarHire = 8,          // car hire's customers: visitors, and residents without a car
        Tenants = 9,          // a landlord's applicants: households taking a flat or still looking for one
        TenantBudget = 10,    // what they offer: their budget for rent against the market's rent
        Instructions = 11,    // the estate agency's owners: households moving, and owners selling
        FlatBuyers = 12,      // a builder's buyers: households of an age to buy a first home, and the savings to buy with
        ClientTenders = 13,   // a builder's private clients: the city's pull for building, and its businesses' takings
        PublicTenders = 14,   // the council's own buildings: the council's taxes
        CarBuyers = 15,       // a car showroom's buyers: households replacing a car or buying a first, by income
        Online = 16,          // online shops: households' online share of what they spend (each kind its own, PopDemandState.Online)
        Listings = 17,        // the marketplace's sellers: households moving, and households short of money
        Bargains = 18,        // the marketplace's buyers: working and middle class households' spending on goods
        Students = 19,        // the academy's students: residents who want a course
        CouncilRevenue = 20,  // the council's revenue: the population's taxes
        Satisfaction = 21,    // the residents' satisfaction, which the mayor's approval follows
        TechSeekers = 22,     // the tech sector's jobseekers: the professions' unemployed and those looking while in work
        Openings = 23         // the player's job openings: the professions' vacancies at the city's employers
    }

    /// <summary>A figure measured from the people each day, which the demands are made of. Saved as a number: never renumber, add at the end.</summary>
    public enum PopSource : byte
    {
        Savings = 0,      // whole money: the households' savings
        BorrowPoor = 1,   // cents a day: the income after tax of the households in each credit band (the band from their debt against their income), what they borrow against
        BorrowFair = 2,
        BorrowGood = 3,
        Turnover = 4,     // cents a day: the businesses' takings
        Wages = 5,        // cents a day: the private employers' wage bill, residents and commuters
        Visitors = 6,     // thousandths of a person a day: the visitors the city's districts draw, at their ordinary number
        Leisure = 7,      // cents a day: the households' eating out, cinema, theatre, night life and casino
        Carless = 8,      // households without a car that could hire one
        Housing = 9,      // households that took a flat in the last month, and those still looking for one
        Budget = 10,      // thousandths: what those households would pay in rent, against the market's rent
        Sellers = 11,     // thousandths of a household a month: households moving, and owners selling at their usual rate
        Starters = 12,    // households of 25 to 44 that took a home in the last month
        Wealth = 13,      // whole money: the middle and upper class households' savings
        Taxes = 14,       // cents a day: the council's taxes from the people (income tax, property tax in rents, sales tax in takings)
        Pull = 15,        // points: every district's pull for homes, shops, offices and warehouses, each raised by a hundred, summed
        CarWant = 16,     // thousandths of a household a year: households that would buy a car this year
        Online = 17,      // cents a day: what households spend online, all kinds
        Listings = 18,    // households that would sell things second hand
        Bargains = 19,    // cents a day: working and middle class households' spending on goods
        Learners = 20,    // people who want a course
        Content = 21,     // thousandths: the households' satisfaction on average
        ProSeekers = 22,  // people in the professions out of work or looking while in work
        ProOpenings = 23, // the professions' vacancies at employers that are not the player's
        Adults = 24,      // people of 18 and over (the electorate; no demand of its own)
        Households = 25   // households (no demand of its own)
    }

    /// <summary>
    /// Every figure of the population's demand for the mod's own businesses: what each demand is made of (its recipe: sources and their weights, which sum to
    /// a thousand), the bands and shares the sources are measured by, each with its unit and its source, and the bounds that keep the arithmetic sane.
    /// Game-free. A demand's factor is the weighted sum of its sources' figures against what they were on the anchor day, so on that day every demand reads a
    /// thousand (the mod's businesses keep today's figures: nothing jumps) and from then on it moves with the people.
    /// </summary>
    public static class PopDemandCatalogue
    {
        public const int Demands = 24;                 // PopDemand's members
        public const int Sources = 26;                 // PopSource's members
        public const int Kinds = PopulationCatalogue.Categories;
        public const int FactorMost = 10000;           // thousandths: ten times the first day's at most, a bound for the arithmetic (a market runs out of people long before)
        public const int Neutral = 1000;               // thousandths: a demand as the first day had it, or with no population

        /// <summary>
        /// What each demand is made of, in the order of PopDemand: its sources and their weights in thousandths (summing to a thousand). Where a business
        /// had several drivers the weights say how much of its custom each brings.
        /// </summary>
        public static readonly (PopSource Source, int Weight)[][] Recipes =
        {
            new[] { (PopSource.Savings, 1000) },                                                // the households' own savings
            new[] { (PopSource.Turnover, 400), (PopSource.Wages, 600) },                         // a firm keeps cash in proportion to what passes through it
            new[] { (PopSource.BorrowPoor, 1000) },                                             // a band's borrowing by its households' incomes: the population keeps debt, not
            new[] { (PopSource.BorrowFair, 1000) },                                             // the loans lenders would make (it charges no interest and writes nothing off),
            new[] { (PopSource.BorrowGood, 1000) },                                             // so its debt sets the band and its income the size
            new[] { (PopSource.Turnover, 1000) },                                               // a shop borrows against its takings
            new[] { (PopSource.Wages, 1000) },                                                  // a company borrows by its size
            // NYC Tourism + Conventions: about a third of hotel nights business, the rest leisure; residents' own stays a small part.
            new[] { (PopSource.Wages, 350), (PopSource.Visitors, 500), (PopSource.Leisure, 150) },
            new[] { (PopSource.Visitors, 500), (PopSource.Carless, 500) },                       // in Manhattan most hires are residents without a car going away, the rest visitors
            new[] { (PopSource.Housing, 1000) },
            new[] { (PopSource.Budget, 1000) },
            new[] { (PopSource.Sellers, 1000) },
            new[] { (PopSource.Starters, 500), (PopSource.Wealth, 500) },                        // a first home wants the age to buy and the deposit
            new[] { (PopSource.Pull, 500), (PopSource.Turnover, 500) },
            new[] { (PopSource.Taxes, 1000) },
            new[] { (PopSource.CarWant, 1000) },
            new[] { (PopSource.Online, 1000) },
            new[] { (PopSource.Listings, 1000) },
            new[] { (PopSource.Bargains, 1000) },
            new[] { (PopSource.Learners, 1000) },
            new[] { (PopSource.Taxes, 1000) },
            new[] { (PopSource.Content, 1000) },
            new[] { (PopSource.ProSeekers, 1000) },
            new[] { (PopSource.ProOpenings, 1000) },
        };

        // ---- the households' credit band (the own bank's segments Poor, Fair and Good), from their income, debt and savings

        public const int Bands = 3;                    // poorly, fairly and well rated
        public const int PoorDebtDays = 120;           // days of income after tax in debt past which a household is poorly rated (a debt to income ratio of a third of a year)
        public const int GoodLevel = 1000;             // thousandths of the city's middle income a well rated household earns at the least
        public const int PoorLevel = 500;              // ... under which a household is poorly rated (US: thin files and low incomes score lowest)
        public const int DefaultDebtDays = 365;        // days of income in debt past which a household is in default: no lender lends to it (the population writes
                                                       // no debt off, so without this line a household spending past its income would stay in the market for ever)

        // ---- residents' satisfaction (no record keeps it: worked out each day from a household's circumstances), thousandths

        public const int ContentBase = 600;            // a household with nothing for or against it
        public const int ContentRentFair = 300;        // thousandths of income in rent that neither pleases nor annoys (the US rule of a third of income)
        public const int ContentRentLeast = -200, ContentRentMost = 100;   // what the rent can take away or add
        public const int ContentOutOfWork = 150;       // lost for each adult of the household out of work...
        public const int ContentOutOfWorkMost = 300;   // ...at most
        public const int ContentThinDays = 30;         // days of income in savings under which a household worries...
        public const int ContentThin = 80;             // ...and loses this
        public const int ContentCushionDays = 180;     // days of income in savings over which it is at ease...
        public const int ContentCushion = 60;          // ...and gains this
        public const int ContentCrowded = 100;         // lost when its flat is smaller than its people want
        public const int ContentDebtDays = 120;        // days of income in debt past which a household is weighed down...
        public const int ContentDebt = 100;            // ...and loses this

        // ---- a district's crime against the city's (the usual correlates of crime in the criminology literature: low income, unemployment, the share of young people)

        public const int CrimeIncome = 450, CrimeUnemployment = 350, CrimeYouth = 200;   // thousandths of the index each part makes
        public const int CrimeTermLeast = 250, CrimeTermMost = 4000;                      // thousandths a part keeps within against the city's
        public const int CrimeLeast = 400, CrimeMost = 2500;                              // thousandths of the city's rate a district keeps within
        public const int YouthFrom = 15, YouthTo = 24;                                    // years: the ages the youth share counts
        public const int CrimeReports = 3;             // crimes the residents of a district of the city's average report over the district map's 28 days
        public const int SatisfactionApproval = 40;    // points of the mayor's approval for the residents' satisfaction moving by its whole first-day figure (a tenth less satisfied, four points less)

        // ---- car hire: who could hire a car

        public const int HireLevelLeast = 500;         // thousandths of the city's middle income a household without a car needs to hire one

        // ---- the housing market: who moves, sells and buys

        public const int OwnerYears = 10;              // years an owner keeps a home before selling (US: a median tenure of about ten years)
        public const int StarterFrom = 25, StarterTo = 44;   // years: a first-time buyer's age (US: the median first-time buyer is about 35)

        // ---- car buyers, by income (S&P Global Mobility: US cars are kept about twelve years)

        public const int CarLifeYears = 12;            // years a household keeps its car
        public const int FirstCarPerMille = 60;        // per mille a year of households without a car that could keep one buying a first
        public const int FirstCarLevel = 800;          // thousandths of the city's middle income at which a household without a car could keep one
        public const int CarLeanLeast = 500, CarLeanMost = 2000;   // thousandths: how far income speeds or slows a replacement

        /// <summary>
        /// Households' online share of each kind of spending, thousandths, in the order of Spend (US Census Bureau Annual Retail Trade Survey, e-commerce by kind
        /// of business, rounded; meal delivery for eating out). The rest is spent in shops; a kind no site sells is nought.
        /// </summary>
        public static readonly int[] OnlineShare = { 0, 0, 120, 150, 50, 350, 400, 300, 450, 250, 200, 0, 0, 0, 0, 0, 0, 30, 100, 0, 0 };

        // ---- the marketplace

        public const int ShortSavingsDays = 7;         // days of income in savings under which a household with debt sells what it can

        // ---- the academy

        public const int LearnerFrom = 18, LearnerTo = 45;   // years: the ages that come to a training academy
        public const int LearnerSkillUnder = 40;       // a worker with no more than college and less skill than this wants a course

        // ---- the labour market (US JOLTS: quits about 2.3% a month; many more look while in work)

        public const int SwitchPerMille = 40;          // per mille of the professions' workers looking for another job at any time

        /// <summary>True for a field the professions' labour market counts (the tech sector's seekers, the player's openings): office work to management, the public sector left out.</summary>
        public static bool Profession(int field)
        {
            return field == (int)Field.Office || field == (int)Field.Professional || field == (int)Field.Tech || field == (int)Field.Creative || field == (int)Field.Management;
        }

        /// <summary>A demand's recipe, or an empty one.</summary>
        public static (PopSource Source, int Weight)[] Recipe(PopDemand d)
        {
            int i = (int)d;
            return i >= 0 && i < Recipes.Length ? Recipes[i] : new (PopSource, int)[0];
        }

        /// <summary>A figure against its anchor, thousandths, within the bounds; a thousand while there is no anchor yet.</summary>
        public static int Ratio(long now, long anchor)
        {
            if (anchor <= 0)
            {
                return Neutral;
            }
            if (now <= 0)
            {
                return 0;
            }
            if (now / (FactorMost / 1000) >= anchor)
            {
                return FactorMost;   // ten times the anchor or more
            }
            return (int)(now <= long.MaxValue / 1000 ? now * 1000 / anchor : now / anchor * 1000);
        }
    }
}
