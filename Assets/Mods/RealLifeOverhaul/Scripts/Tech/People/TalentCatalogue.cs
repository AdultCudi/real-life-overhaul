using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A family of professional roles: who can stand in for whom, who mentors whom, and which labour market a role is sold in.</summary>
    public enum Family
    {
        Engineering = 0, Data = 1, Security = 2, Infrastructure = 3, Product = 4, Design = 5, Commercial = 6, Support = 7, Corporate = 8, Built = 9, Count = 10
    }

    /// <summary>Where a professional works: in the building, two days at home, or from home (a role that allows it).</summary>
    public enum Arrangement
    {
        OnSite = 0, Hybrid = 1, Remote = 2
    }

    /// <summary>One professional role: its family, the game skill it is hired under (none: the game has no such job), its specialisms and pay.</summary>
    public sealed class RoleDef
    {
        public readonly int Id;
        public readonly string Key;          // reallife_talent_role_{Key}
        public readonly Family Family;
        public readonly string GameSkill;    // the game's skill it is hired and paid under; empty: always on the mod's own payroll, not drawn
        public readonly int SkillA, SkillB;  // the two specialisms, TalentCatalogue.Skills
        public readonly int Pay;             // thousandths of the engineer's rate at the same level
        public readonly int LeastLevel, MostLevel;
        public readonly bool Remote;         // can be done from home
        public readonly bool Tech;           // a tech sector role, not one of the professions every company has

        public RoleDef(int id, string key, Family family, string gameSkill, int skillA, int skillB, int pay, int least, int most, bool remote, bool tech)
        {
            Id = id;
            Key = key;
            Family = family;
            GameSkill = gameSkill ?? string.Empty;
            SkillA = skillA;
            SkillB = skillB;
            Pay = pay;
            LeastLevel = least;
            MostLevel = most;
            Remote = remote;
            Tech = tech;
        }

        /// <summary>True for a role the game itself hires: its people are characters at a desk in the building.</summary>
        public bool InGame => GameSkill.Length > 0;
    }

    /// <summary>
    /// Every professional role, specialism, level, school, certificate and course. A role sits on a game skill (a programmer may be
    /// a data scientist); a role the game has no job for is on the mod's own payroll. Game-free; amounts at the start's prices.
    /// </summary>
    public static class TalentCatalogue
    {
        // The game's skills the roles are hired under
        public const string Programmer = "ba:skill_programmer";
        public const string GraphicDesigner = "ba:skill_graphicdesigner";
        public const string CustomerService = "ba:skill_customerservice";
        public const string HrManager = "ba:skill_hrmanager";
        public const string Headhunter = "ba:skill_headhunter";
        public const string Lawyer = "ba:skill_lawyer";

        // Specialisms
        public const int Coding = 0, SystemDesign = 1, Frontend = 2, Backend = 3, Mobile = 4, Automation = 5, Reliability = 6, Pipelines = 7, Statistics = 8,
            Learning = 9, SecurityOps = 10, Testing = 11, Troubleshooting = 12, Networking = 13, ProductSense = 14, Research = 15, Visual = 16, Writing = 17,
            Selling = 18, Relationships = 19, Leadership = 20, Delivery = 21, Accounting = 22, Law = 23, Recruiting = 24, Marketing = 25, Analysis = 26,
            Structures = 27, Planning = 28, SkillCount = 29;

        /// <summary>reallife_talent_skill_{name}</summary>
        public static readonly string[] Skills =
        {
            "coding", "system_design", "front_end", "back_end", "mobile", "automation", "reliability", "pipelines", "statistics", "machine_learning", "security_ops",
            "testing", "troubleshooting", "networking", "product_sense", "user_research", "visual_design", "writing", "selling", "relationships", "leadership",
            "delivery", "accounting", "law", "recruiting", "marketing", "analysis", "structures", "site_planning"
        };

        // Levels
        public const int Junior = 0, Mid = 1, Senior = 2, Staff = 3, Principal = 4, Levels = 5;

        /// <summary>reallife_talent_level_{name}</summary>
        public static readonly string[] LevelKeys = { "junior", "mid", "senior", "staff", "principal" };

        /// <summary>The main skill (the game's own, 0 to 100) a level is held at: its least and its most.</summary>
        public static readonly int[] LevelSkillLeast = { 10, 35, 55, 70, 82 };
        public static readonly int[] LevelSkillMost = { 42, 62, 82, 92, 100 };

        /// <summary>Years of experience at a level: least and most.</summary>
        public static readonly int[] LevelYearsLeast = { 0, 2, 5, 8, 12 };
        public static readonly int[] LevelYearsMost = { 2, 6, 10, 15, 25 };

        /// <summary>Output at a level, thousandths of a mid-level's, before skill and mood.</summary>
        public static readonly int[] LevelOutput = { 650, 1000, 1300, 1500, 1650 };

        // Pay: the going rate is the game's own wage for the main skill (EmployeeHelper's formula), times the role's pay and
        // the labour market's shortage.
        public const string RateSkill = Programmer;   // the game's skill whose wage a role without one is priced from
        public const int RateLeast = 1200;            // no professional is offered less, cents an hour at the start's prices
        public const int WeekHours = 40;
        public const int DayHours = 8;
        public const int YearHours = 2000;            // a year's pay for the fees that are a share of one (search fees)

        // Roles
        public static readonly RoleDef[] Roles =
        {
            new RoleDef(0, "software_engineer", Family.Engineering, Programmer, Coding, SystemDesign, 1000, Junior, Principal, true, true),
            new RoleDef(1, "front_end_engineer", Family.Engineering, Programmer, Frontend, Coding, 980, Junior, Principal, true, true),
            new RoleDef(2, "back_end_engineer", Family.Engineering, Programmer, Backend, SystemDesign, 1020, Junior, Principal, true, true),
            new RoleDef(3, "mobile_engineer", Family.Engineering, Programmer, Mobile, Coding, 1000, Junior, Principal, true, true),
            new RoleDef(4, "devops_engineer", Family.Infrastructure, Programmer, Automation, Reliability, 1050, Junior, Principal, true, true),
            new RoleDef(5, "site_reliability_engineer", Family.Infrastructure, Programmer, Reliability, Networking, 1080, Mid, Principal, true, true),
            new RoleDef(6, "data_engineer", Family.Data, Programmer, Pipelines, Backend, 1030, Junior, Principal, true, true),
            new RoleDef(7, "data_scientist", Family.Data, Programmer, Statistics, Learning, 1060, Junior, Principal, true, true),
            new RoleDef(8, "ml_engineer", Family.Data, Programmer, Learning, Coding, 1150, Mid, Principal, true, true),
            new RoleDef(9, "security_analyst", Family.Security, Programmer, SecurityOps, Networking, 1040, Junior, Principal, true, true),
            new RoleDef(10, "qa_tester", Family.Engineering, Programmer, Testing, Coding, 780, Junior, Staff, true, true),
            new RoleDef(11, "it_support", Family.Support, CustomerService, Troubleshooting, Networking, 600, Junior, Senior, false, true),
            new RoleDef(12, "network_engineer", Family.Infrastructure, Programmer, Networking, Troubleshooting, 950, Junior, Principal, false, true),
            new RoleDef(13, "engineering_manager", Family.Engineering, Programmer, Leadership, Delivery, 1250, Senior, Principal, true, true),
            new RoleDef(14, "cto", Family.Engineering, Programmer, Leadership, SystemDesign, 1700, Staff, Principal, true, true),
            new RoleDef(15, "technical_writer", Family.Product, string.Empty, Writing, ProductSense, 820, Junior, Senior, true, true),
            new RoleDef(16, "sales_engineer", Family.Commercial, CustomerService, Selling, ProductSense, 1050, Mid, Principal, true, true),
            new RoleDef(17, "product_manager", Family.Product, string.Empty, ProductSense, Delivery, 1150, Junior, Principal, true, true),
            new RoleDef(18, "ux_designer", Family.Design, GraphicDesigner, Research, Visual, 950, Junior, Principal, true, true),
            new RoleDef(19, "graphic_designer", Family.Design, GraphicDesigner, Visual, Marketing, 780, Junior, Staff, true, false),
            new RoleDef(20, "web_designer", Family.Design, Programmer, Frontend, Visual, 850, Junior, Staff, true, false),
            new RoleDef(21, "account_manager", Family.Commercial, CustomerService, Relationships, Selling, 880, Junior, Principal, true, false),
            new RoleDef(22, "customer_success", Family.Support, CustomerService, Relationships, Troubleshooting, 800, Junior, Staff, true, true),
            new RoleDef(23, "sales_executive", Family.Commercial, CustomerService, Selling, Relationships, 850, Junior, Principal, true, false),
            new RoleDef(24, "marketing_manager", Family.Commercial, string.Empty, Marketing, Analysis, 950, Junior, Principal, true, false),
            new RoleDef(25, "hr_manager", Family.Corporate, HrManager, Recruiting, Leadership, 900, Junior, Principal, false, false),
            new RoleDef(26, "recruiter", Family.Corporate, Headhunter, Recruiting, Relationships, 850, Junior, Staff, true, false),
            new RoleDef(27, "lawyer", Family.Corporate, Lawyer, Law, Writing, 1300, Junior, Principal, false, false),
            new RoleDef(28, "accountant", Family.Corporate, string.Empty, Accounting, Analysis, 1000, Junior, Principal, true, false),
            new RoleDef(29, "financial_analyst", Family.Corporate, string.Empty, Analysis, Accounting, 1050, Junior, Principal, true, false),
            new RoleDef(30, "business_analyst", Family.Product, string.Empty, Analysis, ProductSense, 950, Junior, Staff, true, false),
            new RoleDef(31, "architect", Family.Built, string.Empty, Structures, Planning, 1100, Junior, Principal, false, false),
            new RoleDef(32, "civil_engineer", Family.Built, string.Empty, Structures, Planning, 1050, Junior, Principal, false, false),
            new RoleDef(33, "data_protection_officer", Family.Security, string.Empty, Law, SecurityOps, 1100, Mid, Principal, true, true),
            new RoleDef(34, "research_scientist", Family.Data, Programmer, Learning, Statistics, 1350, Mid, Principal, true, true),
            new RoleDef(35, "data_centre_technician", Family.Infrastructure, string.Empty, Troubleshooting, Networking, 700, Junior, Senior, false, true),
            new RoleDef(36, "chief_executive", Family.Corporate, string.Empty, Leadership, Delivery, 2600, Staff, Principal, true, true)
        };

        public const int RoleCount = 37;
        public const int SoftwareEngineer = 0, DevOps = 4, Sre = 5, DataScientist = 7, MlEngineer = 8, SecurityAnalyst = 9, QaTester = 10, ItSupport = 11,
            NetworkEngineer = 12, EngineeringManager = 13, Cto = 14, ProductManager = 17, UxDesigner = 18, GraphicDesignerRole = 19, WebDesigner = 20,
            AccountManager = 21, CustomerSuccess = 22, SalesExecutive = 23, Accountant = 28, FinancialAnalyst = 29, DataProtection = 33, ResearchScientist = 34, Technician = 35,
            ChiefExecutive = 36;

        public static RoleDef Role(int id)
        {
            return id >= 0 && id < Roles.Length ? Roles[id] : Roles[0];
        }

        /// <summary>The roles a game skill can be hired as; the first is a newcomer's when nothing else fits.</summary>
        public static int RolesOf(string gameSkill, int[] into)
        {
            int n = 0;
            for (int i = 0; i < Roles.Length && n < into.Length; i++)
            {
                if (Roles[i].GameSkill == gameSkill && gameSkill.Length > 0)
                {
                    into[n++] = i;
                }
            }
            return n;
        }

        /// <summary>True for a game skill that has professional roles; every other skill keeps the game's own hiring.</summary>
        public static bool Professional(string gameSkill)
        {
            if (string.IsNullOrEmpty(gameSkill))
            {
                return false;
            }
            for (int i = 0; i < Roles.Length; i++)
            {
                if (Roles[i].GameSkill == gameSkill)
                {
                    return true;
                }
            }
            return false;
        }

        // Schools and certificates
        public const int SelfTaught = 0, Bootcamp = 1, Diploma = 2, Bachelor = 3, Master = 4, Doctorate = 5, Schools = 6;

        /// <summary>reallife_talent_school_{name}</summary>
        public static readonly string[] SchoolKeys = { "self_taught", "bootcamp", "diploma", "bachelor", "master", "doctorate" };

        /// <summary>What a school adds to how fast a specialism is learnt, thousandths.</summary>
        public static readonly int[] SchoolLearning = { 900, 1000, 1000, 1080, 1150, 1200 };

        /// <summary>A certificate: the specialism it is in, the score in it that it needs, its fee and its effect on pay.</summary>
        public sealed class CertDef
        {
            public readonly int Id;
            public readonly string Key;   // reallife_talent_cert_{Key}
            public readonly int Skill, Needs, Fee, PayBp;

            public CertDef(int id, string key, int skill, int needs, int fee, int payBp)
            {
                Id = id;
                Key = key;
                Skill = skill;
                Needs = needs;
                Fee = fee;
                PayBp = payBp;
            }
        }

        public static readonly CertDef[] Certs =
        {
            new CertDef(0, "cloud_associate", Automation, 40, 300, 200),
            new CertDef(1, "cloud_architect", SystemDesign, 65, 900, 500),
            new CertDef(2, "security_practitioner", SecurityOps, 50, 700, 400),
            new CertDef(3, "network_professional", Networking, 50, 600, 350),
            new CertDef(4, "data_analyst", Statistics, 40, 400, 250),
            new CertDef(5, "agile_delivery", Delivery, 35, 350, 200),
            new CertDef(6, "chartered_accountant", Accounting, 60, 1500, 800),
            new CertDef(7, "chartered_engineer", Structures, 60, 1400, 700),
            new CertDef(8, "practising_lawyer", Law, 55, 1600, 700),
            new CertDef(9, "privacy_professional", Law, 45, 800, 400),
            new CertDef(10, "machine_learning", Learning, 55, 1000, 500),
            new CertDef(11, "testing_professional", Testing, 40, 350, 200)
        };

        public const int CertCount = 12;

        // Courses
        public const int CourseDays = 3;           // days away for a course
        public const int CourseFee = 250;          // per point of the gain at the start's prices, scaled by the score already held
        public const int CourseGain = 8;           // points of the specialism a course adds (less near the top)
        public const int ExamDays = 1;             // days away for an exam
        public const int ExamPassBase = 55;        // per cent a candidate who just meets the score passes; up 2 a point over it
        public const int ExamMoraleUp = 60;        // thousandths of morale for a pass
        public const int DegreeDays = 120;         // a degree the business sponsors (study leave), for a year of 365 days
        public const int DegreeGain = 10;          // points it adds to each of the role's two skills

        // Learning on the job
        public const int LearnPerHour = 15;        // thousandths of a point of a specialism an hour of work, at 0; less as the score rises
        public const int MentorBonus = 600;        // thousandths more for a junior or mid with a senior or above of the family at the same business
        public const int MentorsEach = 3;          // one mentor helps this many
        public const int MainLearnPerHour = 3;     // thousandths of a point of the game's own skill an hour, from a mentor (on top of the game's own)

        // Mood and burnout (thousandths)
        public const int MoraleStart = 650;
        public const int BurnoutLong = 45;         // hours a week over which burnout builds
        public const int BurnoutPerHour = 9;       // thousandths of burnout an hour over it
        public const int BurnoutRest = 25;         // thousandths it eases a day under it
        public const int BurnoutSick = 750;        // over this a professional may go off sick
        public const int BurnoutQuit = 900;        // over this they may hand in their notice
        public const int MoodToGame = 4;           // points of the game's satisfaction a day morale moves it by at most

        // Reviews and promotion
        public const int ReviewsAYear = 2;         // reviews a year of the game's length
        public const int PromoteRating = 4;        // the rating (1 to 5) a promotion needs
        public const int PromoteDaysInLevel = 180; // days at a level before the next, written for a year of 365 days (Scaled)
        public const int PromotePayBp = 1200;      // a promotion's rise, basis points
        public const int ReviewRiseBp = 300;       // what a rating of 4 asks for; 5 asks twice that
        public const int LowRatingsToGo = 2;       // ratings of 1 in a row that make a dismissal fair (no severance)
        public const int AskDays = 7;              // days to answer a request for a rise
        public const int RefuseMorale = 70;        // thousandths of morale a refusal costs

        // Notice, probation, severance, restraints
        public const int ProbationDays = 90;       // written for a year of 365 days (Scaled)
        public const int ProbationNotice = 7;      // days
        public const int NoticePerYear = 7;        // days of notice a year of service
        public const int NoticeLeast = 7, NoticeMost = 84;   // days
        public const int SeveranceWeeksPerYear = 1;          // weeks' pay a year of service
        public const int SeveranceLeastWeeks = 2;
        public const int LayoffsNews = 3;          // lay-offs in one day that make the paper
        public const int NonCompeteBp = 300;       // what a restraint clause costs in pay, basis points
        public const int NonCompetePoach = 500;    // thousandths of the rivals' chance of an approach with one

        // Poaching of professionals
        public const int PoachLevel = Senior;      // rivals go after seniors and above
        public const int PoachChance = 6;          // per mille a day, before shortage, mood and pay
        public const int PoachPremiumBp = 1500;    // what a rival offers over the professional's pay
        public const int PoachAnswerDays = 3;      // days to answer an approach
        public const int PoachRestDays = 21;       // days after an answer before the next approach

        /// <summary>A number of days written for a year of 365, in a year of the game's length (at least one).</summary>
        public static int Scaled(int days, int year)
        {
            return Math.Max(1, (int)Math.Round(days * (double)Math.Max(60, year) / 365.0));
        }

        public static int Clamp(int v, int least, int most)
        {
            return v < least ? least : v > most ? most : v;
        }
    }
}
