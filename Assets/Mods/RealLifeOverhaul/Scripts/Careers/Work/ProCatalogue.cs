using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Where a profession's people work: the saved number is never kept.</summary>
    public enum Workplace
    {
        Business = 0,   // the employer's own building: one of the city's businesses, a rival's included
        Office = 1,     // an office: the employer's own business, or for one of the city's tech companies an office building it is given
        Hospital = 2,   // the city's hospitals and clinics: any of them
        School = 3,     // the city's school
        CityHall = 4    // the council's offices: the city's tax office building
    }

    /// <summary>Who employs the player: the saved number of <see cref="Opening.Kind"/> and <see cref="Job.Kind"/>.</summary>
    public enum EmployerKind
    {
        City = 0,       // one of the city's own businesses, of no rival's
        Rival = 1,      // a business of one of the game's rivals
        TechFirm = 2,   // one of the city's tech companies (Tech/Hiring), at an office building
        Public = 3      // the hospitals, the school, the council
    }

    /// <summary>How the player's day is worked: the saved number of <see cref="Job.Work"/>. The same numbers as the professions' (Tech/People Arrangement).</summary>
    public enum WorkMode
    {
        OnSite = 0, Hybrid = 1, Remote = 2
    }

    /// <summary>One rung of a profession's ladder.</summary>
    public sealed class ProRank
    {
        public readonly int Role;        // the professions' role (TalentCatalogue.Roles) whose two skills the rank is judged on; -1: the profession's own two
        public readonly int Level;       // the professions' level (junior to principal) it stands for on the jobs board
        public readonly int Pay;         // cents an hour at the start's prices
        public readonly int Need;        // the points of its two skills (the first counting three fifths) it asks for
        public readonly int[] Quals;     // any one of these courses passed; empty: nothing
        public readonly int Cert;        // a professional certificate it needs (TalentCatalogue.Certs), -1 none
        public readonly int Days;        // days at the rank below before this one, written for a year of 365 days
        public readonly bool Exec;       // a seat at the top: a chief officer, a partner, the head of the place

        public ProRank(int role, int level, int payWhole, int need, int days, int[] quals = null, int cert = -1, bool exec = false)
        {
            Role = role;
            Level = level;
            Pay = payWhole * 100;
            Need = need;
            Days = days;
            Quals = quals ?? new int[0];
            Cert = cert;
            Exec = exec;
        }
    }

    /// <summary>One profession: its ladder, the skills it is judged on, the kinds of business that employ it, where its people work and when.</summary>
    public sealed class Profession
    {
        public readonly int Id;
        public readonly string Key;          // reallife_pro_name_{Key}; its ranks reallife_pro_rank_{Key}_{n}
        public readonly int Family;          // the professions' family it is hired in on the jobs board (Tech/People Family), -1 none
        public readonly int SkillA, SkillB;  // its own two skills (each rank's role may ask for others)
        public readonly string[] Types;      // business types that employ it, matched as a part of the game's type name; empty: only the public sector
        public readonly Workplace Place;
        public readonly bool Remote;         // its days can be worked from home
        public readonly int Days, Hour, Hours;   // the working week (bit 0 Monday), the hour a day starts and its length
        public readonly ProRank[] Ranks;

        public Profession(int id, string key, int family, int skillA, int skillB, string[] types, Workplace place, bool remote, int days, int hour, int hours, params ProRank[] ranks)
        {
            Id = id;
            Key = key;
            Family = family;
            SkillA = skillA;
            SkillB = skillB;
            Types = types ?? new string[0];
            Place = place;
            Remote = remote;
            Days = days;
            Hour = hour;
            Hours = hours;
            Ranks = ranks;
        }

        public bool Public => Types.Length == 0;

        public ProRank Rank(int rank) => Ranks[rank < 0 ? 0 : rank >= Ranks.Length ? Ranks.Length - 1 : rank];

        /// <summary>The two skills a rank is judged on: its role's, or the profession's own.</summary>
        public void SkillsOf(int rank, out int a, out int b)
        {
            ProRank r = Rank(rank);
            if (r.Role >= 0)
            {
                RoleDef role = TalentCatalogue.Role(r.Role);
                a = role.SkillA;
                b = role.SkillB;
            }
            else
            {
                a = SkillA;
                b = SkillB;
            }
        }
    }

    /// <summary>
    /// Every profession the player can work in, with its ranks, pay and what each rank needs. Game-free (the checkers compile
    /// it); every amount is at the start's prices and is asked of Prices when it is paid or shown.
    ///
    /// THE GAME'S OWN. The game has two jobs of its own for the player: the cashier's job at El Gato Food Market (a building's
    /// SpecialService.playerJob: a till, a weekly schedule, 25 an hour, three warnings and the sack) and the delivery van
    /// (Vehicles.DeliveryDriverJob), and food deliveries from a restaurant's till. The mod's gigs and their employers
    /// (Jobs/JobCatalogue, CareerCatalogue) are street work. These are the professions: an employer of the city (one of its
    /// own businesses, a rival's, one of its tech companies, or the public sector) takes the player on at a rank, judged on
    /// the player's own skills (Careers/Study) and qualifications, and pays by the hour of a working day.
    ///
    /// PAY. The game's cashier earns 25 an hour and its van about 100 an hour of play; the gigs come to 40 to 55. A newcomer
    /// to a profession earns 16 to 40 an hour, a senior 40 to 90, the top of a ladder 70 to 260: a working week of a senior
    /// developer is about 3,200, a chief executive's about 10,000. Working is a way to start and a way to rise, not a better use
    /// of a day than a busy business of one's own.
    /// </summary>
    public static class ProCatalogue
    {
        private const int Mon = 1, Tue = 2, Wed = 4, Thu = 8, Fri = 16, Sat = 32, Sun = 64, Weekdays = Mon | Tue | Wed | Thu | Fri;
        private const int Engineering = (int)Tech.Family.Engineering, Data = (int)Tech.Family.Data, Security = (int)Tech.Family.Security, Design = (int)Tech.Family.Design,
            Commercial = (int)Tech.Family.Commercial, Support = (int)Tech.Family.Support, Corporate = (int)Tech.Family.Corporate, Built = (int)Tech.Family.Built, Product = (int)Tech.Family.Product;

        public const string TechTypes = "reallife:businesstype_";   // the tech sector's own types (Tech/Firms/TechTypes)

        public const int Developer = 0, DataPro = 1, SecurityPro = 2, Designer = 3, Accountant = 4, Lawyer = 5, Architect = 6, CivilEngineer = 7, Nurse = 8, Teacher = 9,
            Marketer = 10, Banker = 11, Manager = 12, CouncilOfficer = 13, Electrician = 14, Plumber = 15, Carpenter = 16, Chef = 17, Stylist = 18, ItSupport = 19, Sales = 20,
            HumanResources = 21, BarStaff = 22, SecurityGuard = 23, Warehouse = 24, Count = 25;

        private static int[] Q(params int[] courses) => courses;

        public static readonly Profession[] All =
        {
            new Profession(Developer, "developer", Engineering, TalentCatalogue.Coding, TalentCatalogue.SystemDesign,
                new[] { TechTypes, "webdevelopmentagency", "headquarters", "bank" }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Junior, 38, 20, 0,
                    Q(StudyCatalogue.ComputerScience, StudyCatalogue.SoftwareEngineering, StudyCatalogue.DataScience, StudyCatalogue.CodingBootcamp, StudyCatalogue.MscComputing)),
                new ProRank(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 55, 38, 120),
                new ProRank(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Senior, 80, 55, 180),
                new ProRank(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Staff, 110, 70, 240),
                new ProRank(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Principal, 140, 82, 300),
                new ProRank(TalentCatalogue.EngineeringManager, TalentCatalogue.Staff, 150, 60, 240),
                new ProRank(TalentCatalogue.Cto, TalentCatalogue.Principal, 220, 75, 360, null, -1, true)),
            new Profession(DataPro, "data", Data, TalentCatalogue.Statistics, TalentCatalogue.Learning,
                new[] { TechTypes, "bank", "headquarters", "marketingagency" }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.DataScientist, TalentCatalogue.Junior, 36, 20, 0,
                    Q(StudyCatalogue.DataScience, StudyCatalogue.ComputerScience, StudyCatalogue.MscComputing, StudyCatalogue.DataAnalyst, StudyCatalogue.Finance)),
                new ProRank(TalentCatalogue.DataScientist, TalentCatalogue.Mid, 58, 38, 120),
                new ProRank(TalentCatalogue.DataScientist, TalentCatalogue.Senior, 82, 55, 180),
                new ProRank(TalentCatalogue.MlEngineer, TalentCatalogue.Staff, 108, 68, 240),
                new ProRank(TalentCatalogue.ResearchScientist, TalentCatalogue.Principal, 150, 80, 300)),
            new Profession(SecurityPro, "security", Security, TalentCatalogue.SecurityOps, TalentCatalogue.Networking,
                new[] { TechTypes, "bank", "headquarters" }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.SecurityAnalyst, TalentCatalogue.Junior, 38, 20, 0,
                    Q(StudyCatalogue.Cybersecurity, StudyCatalogue.ComputerScience, StudyCatalogue.SecurityPractitioner, StudyCatalogue.NetworkProfessional, StudyCatalogue.ItSupport)),
                new ProRank(TalentCatalogue.SecurityAnalyst, TalentCatalogue.Mid, 60, 38, 120),
                new ProRank(TalentCatalogue.SecurityAnalyst, TalentCatalogue.Senior, 85, 55, 180, null, 2),
                new ProRank(TalentCatalogue.SecurityAnalyst, TalentCatalogue.Staff, 115, 70, 240, null, 2),
                new ProRank(TalentCatalogue.SecurityAnalyst, TalentCatalogue.Principal, 170, 82, 300, null, 2, true)),
            new Profession(Designer, "design", Design, TalentCatalogue.Visual, TalentCatalogue.Research,
                new[] { "graphicdesigner", "webdevelopmentagency", "marketingagency", TechTypes }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.UxDesigner, TalentCatalogue.Junior, 30, 20, 0, Q(StudyCatalogue.Design)),
                new ProRank(TalentCatalogue.UxDesigner, TalentCatalogue.Mid, 45, 38, 120),
                new ProRank(TalentCatalogue.UxDesigner, TalentCatalogue.Senior, 65, 55, 180),
                new ProRank(TalentCatalogue.UxDesigner, TalentCatalogue.Staff, 85, 68, 240),
                new ProRank(TalentCatalogue.UxDesigner, TalentCatalogue.Principal, 115, 80, 300, null, -1, true)),
            new Profession(Accountant, "accountancy", Corporate, TalentCatalogue.Accounting, TalentCatalogue.Analysis,
                new[] { "headquarters", "bank", "lawfirm", "recruitmentagency" }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.Accountant, TalentCatalogue.Junior, 28, 15, 0, Q(StudyCatalogue.Bookkeeping, StudyCatalogue.Accounting, StudyCatalogue.Finance)),
                new ProRank(TalentCatalogue.Accountant, TalentCatalogue.Mid, 48, 35, 120, Q(StudyCatalogue.Accounting, StudyCatalogue.Finance)),
                new ProRank(TalentCatalogue.Accountant, TalentCatalogue.Senior, 70, 52, 180, null, 6),
                new ProRank(TalentCatalogue.Accountant, TalentCatalogue.Staff, 95, 66, 240, null, 6),
                new ProRank(TalentCatalogue.Accountant, TalentCatalogue.Principal, 130, 78, 300, null, 6),
                new ProRank(TalentCatalogue.Accountant, TalentCatalogue.Principal, 200, 85, 360, null, 6, true)),
            new Profession(Lawyer, "law", Corporate, TalentCatalogue.Law, TalentCatalogue.Writing,
                new[] { "lawfirm", "headquarters", "bank" }, Workplace.Office, false, Weekdays, 9, 8,
                new ProRank(27, TalentCatalogue.Junior, 28, 15, 0, Q(StudyCatalogue.Law)),
                new ProRank(27, TalentCatalogue.Junior, 36, 30, 90, Q(StudyCatalogue.LegalPractice)),
                new ProRank(27, TalentCatalogue.Mid, 60, 45, 120, null, 8),
                new ProRank(27, TalentCatalogue.Senior, 90, 62, 240, null, 8),
                new ProRank(27, TalentCatalogue.Principal, 180, 78, 360, null, 8, true)),
            new Profession(Architect, "architecture", Built, TalentCatalogue.Structures, TalentCatalogue.Planning,
                new[] { "interiorinstallation", "headquarters" }, Workplace.Office, false, Weekdays, 9, 8,
                new ProRank(31, TalentCatalogue.Junior, 30, 20, 0, Q(StudyCatalogue.Architecture)),
                new ProRank(31, TalentCatalogue.Mid, 52, 38, 120),
                new ProRank(31, TalentCatalogue.Senior, 75, 55, 180, null, 7),
                new ProRank(31, TalentCatalogue.Staff, 100, 68, 240, null, 7),
                new ProRank(31, TalentCatalogue.Principal, 140, 80, 300, null, 7, true)),
            new Profession(CivilEngineer, "civil", Built, TalentCatalogue.Structures, TalentCatalogue.Planning,
                new[] { "interiorinstallation", "factory", "headquarters" }, Workplace.Office, false, Weekdays, 8, 8,
                new ProRank(32, TalentCatalogue.Junior, 34, 20, 0, Q(StudyCatalogue.CivilEngineering)),
                new ProRank(32, TalentCatalogue.Mid, 52, 38, 120),
                new ProRank(32, TalentCatalogue.Senior, 75, 55, 180, null, 7),
                new ProRank(32, TalentCatalogue.Staff, 100, 68, 240, null, 7),
                new ProRank(32, TalentCatalogue.Principal, 135, 80, 300, null, 7, true)),
            new Profession(Nurse, "nursing", -1, Skills.Clinical, Skills.Care, null, Workplace.Hospital, false, Mon | Wed | Fri | Sat, 7, 10,
                new ProRank(-1, TalentCatalogue.Junior, 22, 15, 0, Q(StudyCatalogue.CareWorker, StudyCatalogue.FirstAid)),
                new ProRank(-1, TalentCatalogue.Junior, 40, 35, 60, Q(StudyCatalogue.Nursing)),
                new ProRank(-1, TalentCatalogue.Mid, 55, 50, 180),
                new ProRank(-1, TalentCatalogue.Senior, 72, 64, 240),
                new ProRank(-1, TalentCatalogue.Principal, 95, 76, 300, null, -1, true)),
            new Profession(Teacher, "teaching", -1, Skills.Teaching, TalentCatalogue.Writing, null, Workplace.School, false, Weekdays, 8, 8,
                new ProRank(-1, TalentCatalogue.Junior, 20, 10, 0),
                new ProRank(-1, TalentCatalogue.Junior, 38, 35, 60, Q(StudyCatalogue.Teaching, StudyCatalogue.Pgce)),
                new ProRank(-1, TalentCatalogue.Mid, 52, 50, 180),
                new ProRank(-1, TalentCatalogue.Senior, 68, 62, 240),
                new ProRank(-1, TalentCatalogue.Principal, 90, 74, 300, null, -1, true)),
            new Profession(Marketer, "marketing", Commercial, TalentCatalogue.Marketing, TalentCatalogue.Analysis,
                new[] { "marketingagency", "headquarters", "eventplanning" }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(24, TalentCatalogue.Junior, 28, 15, 0, Q(StudyCatalogue.Marketing, StudyCatalogue.DigitalMarketing, StudyCatalogue.Business)),
                new ProRank(24, TalentCatalogue.Mid, 42, 35, 120),
                new ProRank(24, TalentCatalogue.Senior, 65, 52, 180),
                new ProRank(24, TalentCatalogue.Staff, 90, 66, 240),
                new ProRank(24, TalentCatalogue.Principal, 140, 78, 300, null, -1, true)),
            new Profession(Banker, "banking", Corporate, TalentCatalogue.Analysis, TalentCatalogue.Accounting,
                new[] { "bank", "headquarters" }, Workplace.Office, false, Weekdays, 8, 9,
                new ProRank(29, TalentCatalogue.Junior, 40, 22, 0, Q(StudyCatalogue.Finance, StudyCatalogue.Accounting, StudyCatalogue.Business, StudyCatalogue.Mba)),
                new ProRank(29, TalentCatalogue.Mid, 62, 40, 120),
                new ProRank(29, TalentCatalogue.Senior, 95, 56, 180),
                new ProRank(29, TalentCatalogue.Staff, 130, 70, 240),
                new ProRank(29, TalentCatalogue.Principal, 220, 82, 360, Q(StudyCatalogue.Mba, StudyCatalogue.CharteredAccountant), -1, true)),
            new Profession(Manager, "management", Product, TalentCatalogue.Leadership, TalentCatalogue.Delivery,
                new[] { "headquarters", "supermarket", "wholesale", "clothing", "electronics", "furniture", "cardealership", "hotel", "restaurant" }, Workplace.Business, false, Weekdays, 9, 8,
                new ProRank(-1, TalentCatalogue.Junior, 30, 15, 0, Q(StudyCatalogue.Business, StudyCatalogue.Mba, StudyCatalogue.Marketing, StudyCatalogue.Finance, StudyCatalogue.Accounting)),
                new ProRank(-1, TalentCatalogue.Mid, 45, 32, 120),
                new ProRank(-1, TalentCatalogue.Senior, 65, 48, 180),
                new ProRank(-1, TalentCatalogue.Staff, 90, 60, 240),
                new ProRank(-1, TalentCatalogue.Principal, 120, 70, 300),
                new ProRank(-1, TalentCatalogue.Principal, 170, 78, 360, Q(StudyCatalogue.Mba, StudyCatalogue.Business), -1, true),
                new ProRank(TalentCatalogue.ChiefExecutive, TalentCatalogue.Principal, 260, 85, 420, null, -1, true)),
            new Profession(CouncilOfficer, "council", -1, TalentCatalogue.Writing, TalentCatalogue.Delivery, null, Workplace.CityHall, true, Weekdays, 9, 7,
                new ProRank(-1, TalentCatalogue.Junior, 26, 15, 0),
                new ProRank(-1, TalentCatalogue.Mid, 38, 32, 120),
                new ProRank(-1, TalentCatalogue.Senior, 55, 48, 180),
                new ProRank(-1, TalentCatalogue.Staff, 75, 62, 240),
                new ProRank(-1, TalentCatalogue.Principal, 110, 74, 360, null, -1, true)),
            new Profession(Electrician, "electrician", -1, Skills.Electrical, TalentCatalogue.Troubleshooting, new[] { "interiorinstallation", "factory" }, Workplace.Business, false, Weekdays, 8, 8,
                new ProRank(-1, TalentCatalogue.Junior, 18, 5, 0),
                new ProRank(-1, TalentCatalogue.Mid, 32, 35, 60, Q(StudyCatalogue.Electrician)),
                new ProRank(-1, TalentCatalogue.Senior, 42, 50, 150),
                new ProRank(-1, TalentCatalogue.Staff, 55, 62, 240)),
            new Profession(Plumber, "plumber", -1, Skills.Plumbing, TalentCatalogue.Troubleshooting, new[] { "interiorinstallation", "factory" }, Workplace.Business, false, Weekdays, 8, 8,
                new ProRank(-1, TalentCatalogue.Junior, 18, 5, 0),
                new ProRank(-1, TalentCatalogue.Mid, 31, 35, 60, Q(StudyCatalogue.Plumber)),
                new ProRank(-1, TalentCatalogue.Senior, 41, 50, 150),
                new ProRank(-1, TalentCatalogue.Staff, 54, 62, 240)),
            new Profession(Carpenter, "carpenter", -1, Skills.Joinery, TalentCatalogue.Structures, new[] { "interiorinstallation", "furniture", "factory" }, Workplace.Business, false, Weekdays, 8, 8,
                new ProRank(-1, TalentCatalogue.Junior, 17, 5, 0),
                new ProRank(-1, TalentCatalogue.Mid, 29, 35, 60, Q(StudyCatalogue.Carpenter)),
                new ProRank(-1, TalentCatalogue.Senior, 39, 50, 150),
                new ProRank(-1, TalentCatalogue.Staff, 52, 62, 240)),
            new Profession(Chef, "chef", -1, Skills.Cooking, Skills.Hospitality, new[] { "restaurant", "fastfood", "coffeeshop", "hotel" }, Workplace.Business, false, Wed | Thu | Fri | Sat | Sun, 12, 8,
                new ProRank(-1, TalentCatalogue.Junior, 18, 10, 0),
                new ProRank(-1, TalentCatalogue.Mid, 26, 35, 60, Q(StudyCatalogue.Chef)),
                new ProRank(-1, TalentCatalogue.Senior, 36, 50, 150),
                new ProRank(-1, TalentCatalogue.Staff, 50, 62, 240),
                new ProRank(-1, TalentCatalogue.Principal, 70, 74, 300, null, -1, true)),
            new Profession(Stylist, "salon", -1, Skills.Hair, Skills.Beauty, new[] { "hairdresser", "beauty" }, Workplace.Business, false, Tue | Wed | Thu | Fri | Sat, 10, 8,
                new ProRank(-1, TalentCatalogue.Junior, 16, 10, 0),
                new ProRank(-1, TalentCatalogue.Mid, 24, 35, 60, Q(StudyCatalogue.Hairdresser, StudyCatalogue.Beautician)),
                new ProRank(-1, TalentCatalogue.Senior, 32, 50, 150),
                new ProRank(-1, TalentCatalogue.Staff, 42, 62, 240, null, -1, true)),
            new Profession(ItSupport, "it_support", Support, TalentCatalogue.Troubleshooting, TalentCatalogue.Networking,
                new[] { "headquarters", "webdevelopmentagency", "lawfirm", "bank", "recruitmentagency", TechTypes }, Workplace.Office, false, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.ItSupport, TalentCatalogue.Junior, 24, 15, 0, Q(StudyCatalogue.ItSupport, StudyCatalogue.ComputerScience, StudyCatalogue.Cybersecurity, StudyCatalogue.NetworkProfessional)),
                new ProRank(TalentCatalogue.ItSupport, TalentCatalogue.Mid, 34, 35, 120),
                new ProRank(TalentCatalogue.NetworkEngineer, TalentCatalogue.Senior, 55, 52, 180)),
            new Profession(Sales, "sales", Commercial, TalentCatalogue.Selling, TalentCatalogue.Relationships,
                new[] { "cardealership", "electronics", "furniture", "appliance", "wholesale", "headquarters", TechTypes }, Workplace.Business, false, Weekdays, 9, 8,
                new ProRank(TalentCatalogue.SalesExecutive, TalentCatalogue.Junior, 26, 10, 0),
                new ProRank(TalentCatalogue.SalesExecutive, TalentCatalogue.Mid, 40, 32, 120),
                new ProRank(TalentCatalogue.AccountManager, TalentCatalogue.Senior, 58, 48, 180),
                new ProRank(TalentCatalogue.SalesExecutive, TalentCatalogue.Staff, 80, 62, 240),
                new ProRank(TalentCatalogue.SalesExecutive, TalentCatalogue.Principal, 110, 74, 300, null, -1, true)),
            new Profession(HumanResources, "hr", Corporate, TalentCatalogue.Recruiting, TalentCatalogue.Leadership,
                new[] { "recruitmentagency", "headquarters" }, Workplace.Office, true, Weekdays, 9, 8,
                new ProRank(25, TalentCatalogue.Junior, 24, 12, 0, Q(StudyCatalogue.Business, StudyCatalogue.CustomerService)),
                new ProRank(25, TalentCatalogue.Mid, 36, 32, 120),
                new ProRank(25, TalentCatalogue.Senior, 55, 50, 180),
                new ProRank(25, TalentCatalogue.Staff, 80, 64, 240, null, -1, true)),
            new Profession(BarStaff, "bar", -1, Skills.Hospitality, TalentCatalogue.Relationships, new[] { "coffeeshop", "nightclub", "bar", "restaurant", "hotel" }, Workplace.Business, false, Wed | Thu | Fri | Sat | Sun, 14, 8,
                new ProRank(-1, TalentCatalogue.Junior, 18, 10, 0, Q(StudyCatalogue.Barista, StudyCatalogue.Bartending)),
                new ProRank(-1, TalentCatalogue.Mid, 24, 30, 90),
                new ProRank(-1, TalentCatalogue.Senior, 34, 45, 180)),
            new Profession(SecurityGuard, "guard", -1, Skills.Guarding, Skills.FirstAid, new[] { "nightclub", "casino", "bank", "supermarket", "hotel" }, Workplace.Business, false, Wed | Thu | Fri | Sat | Sun, 14, 8,
                new ProRank(-1, TalentCatalogue.Junior, 20, 10, 0, Q(StudyCatalogue.SecurityLicence)),
                new ProRank(-1, TalentCatalogue.Mid, 28, 30, 90),
                new ProRank(-1, TalentCatalogue.Senior, 40, 45, 180)),
            new Profession(Warehouse, "warehouse", -1, Skills.Handling, TalentCatalogue.Delivery, new[] { "warehouse", "wholesale", "importexport", "factory" }, Workplace.Business, false, Weekdays, 6, 8,
                new ProRank(-1, TalentCatalogue.Junior, 20, 10, 0, Q(StudyCatalogue.Forklift)),
                new ProRank(-1, TalentCatalogue.Mid, 28, 30, 90),
                new ProRank(-1, TalentCatalogue.Senior, 42, 45, 180))
        };

        // ---------------------------------------------------------------- the city's tech companies and the public sector
        public const string HospitalType = "ba:businesstype_hospital", ClinicType = "ba:businesstype_clinic", SchoolType = "ba:businesstype_school", CityHallType = "ba:businesstype_irs";

        public static Profession Find(int id) => id >= 0 && id < All.Length ? All[id] : null;

        /// <summary>
        /// True if a business type employs a profession, as <see cref="Employs"/> says, from a set of the professions kept
        /// for each type: the city's thousand-odd businesses are of a few dozen types, and the day's vacancies ask of each
        /// many times (25 professions' stems as IndexOf each: some 40,000 a day). One dictionary hit and a bit after the
        /// type's first time. On the game's thread only.
        /// </summary>
        public static bool EmploysKept(Profession p, string type)
        {
            int bit = p != null ? p.Id : -1;
            if (bit < 0 || bit >= 64 || bit >= All.Length || !ReferenceEquals(All[bit], p))
            {
                return Employs(p, type);   // (a profession that is not the catalogue's own at its place: asked whole)
            }
            return (Kept(type) & (1L << bit)) != 0;
        }

        /// <summary>The professions a business type employs, a bit each by their place in the catalogue (kept per type); every bit for a catalogue of more than 64.</summary>
        public static long EmployedBy(string type)
        {
            return All.Length > 64 ? -1L : Kept(type);
        }

        /// <summary>True if a set from <see cref="EmployedBy"/> of a type holds a profession: what <see cref="Employs"/> says of the type, a bit test.</summary>
        public static bool Holds(long set, Profession p, string type)
        {
            int bit = p != null ? p.Id : -1;
            if (All.Length > 64 || bit < 0 || bit >= All.Length || !ReferenceEquals(All[bit], p))
            {
                return Employs(p, type);
            }
            return (set & (1L << bit)) != 0;
        }

        /// <summary>True if a business type employs any profession of the catalogue (from the same kept sets).</summary>
        public static bool AnyEmploys(string type)
        {
            if (All.Length > 64)
            {
                for (int p = 0; p < All.Length; p++)
                {
                    if (Employs(All[p], type))
                    {
                        return true;
                    }
                }
                return false;
            }
            return Kept(type) != 0L;
        }

        private const int KeptMost = 1024;   // types kept at most (the game has under a hundred)
        private static readonly System.Collections.Generic.Dictionary<string, long> KeptSets = new System.Collections.Generic.Dictionary<string, long>(64);

        // The professions of the catalogue (its first 64, by place) a type employs, a bit each.
        private static long Kept(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return 0L;
            }
            if (KeptSets.TryGetValue(type, out long set))
            {
                return set;
            }
            set = 0L;
            for (int p = 0; p < All.Length && p < 64; p++)
            {
                if (Employs(All[p], type))
                {
                    set |= 1L << p;
                }
            }
            if (KeptSets.Count < KeptMost)
            {
                KeptSets[type] = set;
            }
            return set;
        }

        /// <summary>True if a business type employs a profession (a part of the type's name, as the game writes it).</summary>
        public static bool Employs(Profession p, string type)
        {
            if (p == null || string.IsNullOrEmpty(type))
            {
                return false;
            }
            for (int i = 0; i < p.Types.Length; i++)
            {
                if (type.IndexOf(p.Types[i], System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The profession an ad of a family of the jobs board is for: one of the professions of that family, by the roll (the
        /// infrastructure family's are IT support's); -1 for a family that has none here.
        /// </summary>
        public static int OfFamily(int family, int roll)
        {
            if (family == (int)Tech.Family.Infrastructure)
            {
                family = (int)Tech.Family.Support;
            }
            int count = 0;
            for (int i = 0; i < All.Length; i++)
            {
                count += All[i].Family == family ? 1 : 0;
            }
            if (count == 0)
            {
                return -1;
            }
            int pick = (roll & int.MaxValue) % count;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Family == family && pick-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The rank of a profession that stands for a level of the jobs board: the first at or above it.</summary>
        public static int RankOfLevel(Profession p, int level)
        {
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                if (p.Ranks[r].Level >= level)
                {
                    return r;
                }
            }
            return p.Ranks.Length - 1;
        }
    }
}
