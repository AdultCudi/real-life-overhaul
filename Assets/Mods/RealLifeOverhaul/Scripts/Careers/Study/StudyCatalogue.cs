using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a course is: the saved number of <see cref="CourseDef.Kind"/>.</summary>
    public enum CourseKind
    {
        Short = 0,        // a day or two at the school: first aid, a forklift ticket, a refresher
        Vocational = 1,   // a diploma in a trade or in care, a term at the school
        Degree = 2,       // a bachelor's degree at the university
        Postgrad = 3,     // a master's or a professional course after a degree
        Certificate = 4   // a professional certificate: study at home, an exam at the school
    }

    /// <summary>Where a course's classes are held.</summary>
    public enum Campus
    {
        School = 0,       // the game's own Manhattan Business School: inside the building
        University = 1,   // the university's lecture hall: at its sign, by the door of the building it uses
        Online = 2,       // live online classes: at home, with the home's internet
        Hospital = 3      // a teaching hospital's ward: inside one of the city's hospitals or clinics
    }

    /// <summary>How a course is followed: the saved number of <see cref="Enrolment.Mode"/>.</summary>
    public enum StudyMode
    {
        FullTime = 0,     // the course's own timetable
        PartTime = 1,     // evenings or Saturdays, fewer classes a week: it takes longer
        Distance = 2      // the same timetable followed online from home (a course that allows it)
    }

    /// <summary>
    /// The player's own skills: the 29 specialisms of the professions (Tech/People, the same numbers, so a role's two
    /// specialisms are read straight from the player's), then fourteen of the trades, care and everyday work that no role
    /// of the professions has. Each is 0 to 100 points, kept in thousandths.
    /// </summary>
    public static class Skills
    {
        public const int Electrical = 29, Plumbing = 30, Joinery = 31, Cooking = 32, Hair = 33, Beauty = 34, Care = 35, Clinical = 36, Teaching = 37,
            Hospitality = 38, Guarding = 39, Handling = 40, FirstAid = 41, Driving = 42, Count = 43;

        /// <summary>reallife_study_skill_{key}, for the skills after the professions' own.</summary>
        public static readonly string[] OwnKeys =
        {
            "electrical", "plumbing", "joinery", "cooking", "hair", "beauty", "care", "clinical", "teaching", "hospitality", "guarding", "handling", "first_aid", "driving"
        };

        public static bool Professional(int skill) => skill >= 0 && skill < TalentCatalogue.SkillCount;
    }

    /// <summary>One course. Every amount is at the start's prices and is asked of Prices when it is charged or shown.</summary>
    public sealed class CourseDef
    {
        public readonly int Id;
        public readonly string Key;          // reallife_study_course_{Key} (its name) and reallife_study_about_{Key} (a line on it)
        public readonly CourseKind Kind;
        public readonly Campus Campus;
        public readonly int[] Needs;         // any one of these courses passed first; empty: nothing
        public readonly bool NeedsDegree;    // ...or instead any degree passed
        public readonly int Sessions;        // classes on the timetable
        public readonly int Homework;        // pieces of homework set over the course
        public readonly int Hours;           // a class's length, game hours
        public readonly int Days;            // the full-time timetable's days (bit 0 Monday)
        public readonly int Hour;            // ...and the hour a class starts
        public readonly int Fee;             // the whole course, whole money
        public readonly int SkillA, GainA;   // what it teaches: a skill and the points it adds over the course
        public readonly int SkillB, GainB;
        public readonly int School;          // the schooling it counts as for an employer (TalentCatalogue's schools), -1 none
        public readonly int Cert;            // the professional certificate it is (TalentCatalogue.Certs), -1 none
        public readonly bool Distance;       // may be followed online from home
        public readonly int Pass;            // the exam's pass mark out of 100

        public CourseDef(int id, string key, CourseKind kind, Campus campus, int sessions, int homework, int hours, int days, int hour, int fee,
            int skillA, int gainA, int skillB, int gainB, int school = -1, int cert = -1, bool distance = false, int[] needs = null, bool needsDegree = false)
        {
            Id = id;
            Key = key;
            Kind = kind;
            Campus = campus;
            Sessions = sessions;
            Homework = homework;
            Hours = hours;
            Days = days;
            Hour = hour;
            Fee = fee;
            SkillA = skillA;
            GainA = gainA;
            SkillB = skillB;
            GainB = gainB;
            School = school;
            Cert = cert;
            Distance = distance;
            Needs = needs ?? new int[0];
            NeedsDegree = needsDegree;
            Pass = kind == CourseKind.Degree || kind == CourseKind.Postgrad ? StudyCatalogue.DegreePass : StudyCatalogue.Pass;
        }

        /// <summary>True for a course of the university (degrees and what follows them).</summary>
        public bool Higher => Kind == CourseKind.Degree || Kind == CourseKind.Postgrad;

        /// <summary>True for a course a student loan can pay for.</summary>
        public bool Loanable => Kind == CourseKind.Vocational || Higher;

        /// <summary>True for a course whose classes are a timetable (a certificate is studied at home and examined).</summary>
        public bool Timetabled => Sessions > 0;
    }

    /// <summary>
    /// Every course of the city's schools and every figure of studying: the timetable, fees, grants, the student loan,
    /// homework, exams and grades, and how skills are learnt and fade; game-free (the checkers compile it). The game's own
    /// seven courses at Manhattan Business School stay as they are; these are the city's other learning. One game hour is a
    /// minute of play: a class is three or four of them, and a degree about two dozen classes, three a week.
    /// </summary>
    public static class StudyCatalogue
    {
        public const int Mon = 1, Tue = 2, Wed = 4, Thu = 8, Fri = 16, Sat = 32, Sun = 64;

        // the courses' ids
        public const int FirstAid = 0, Barista = 1, Bartending = 2, Forklift = 3, SecurityLicence = 4, DrivingRefresher = 5, CodingBootcamp = 6, Bookkeeping = 7,
            CustomerService = 8, DigitalMarketing = 9;
        public const int Electrician = 10, Plumber = 11, Carpenter = 12, Chef = 13, Hairdresser = 14, Beautician = 15, CareWorker = 16, ItSupport = 17;
        public const int ComputerScience = 20, SoftwareEngineering = 21, DataScience = 22, Cybersecurity = 23, Business = 24, Accounting = 25, Finance = 26, Law = 27,
            Marketing = 28, Architecture = 29, CivilEngineering = 30, Nursing = 31, Teaching = 32, Design = 33;
        public const int Mba = 40, MscComputing = 41, LegalPractice = 42, Pgce = 43;
        public const int CloudAssociate = 50, CloudArchitect = 51, SecurityPractitioner = 52, ProjectManagement = 53, CharteredAccountant = 54, BarExam = 55,
            CharteredEngineer = 56, DataAnalyst = 57, NetworkProfessional = 58, MachineLearning = 59, PrivacyProfessional = 60, TestingProfessional = 61;
        public const int IdsMost = 64;   // ids are under this: a set of courses is a 64-bit mask

        private const int Bootcamp = TalentCatalogue.Bootcamp, Diploma = TalentCatalogue.Diploma, Bachelor = TalentCatalogue.Bachelor, Master = TalentCatalogue.Master;

        public static readonly CourseDef[] All =
        {
            // short courses at the school (and two online)
            new CourseDef(FirstAid, "first_aid", CourseKind.Short, Campus.School, 2, 0, 3, Wed | Sat, 10, 120, Skills.FirstAid, 25, Skills.Care, 5),
            new CourseDef(Barista, "barista", CourseKind.Short, Campus.School, 2, 0, 3, Mon | Thu, 10, 150, Skills.Hospitality, 20, Skills.Cooking, 5),
            new CourseDef(Bartending, "bartending", CourseKind.Short, Campus.School, 3, 0, 3, Tue | Fri, 14, 180, Skills.Hospitality, 25, TalentCatalogue.Relationships, 5),
            new CourseDef(Forklift, "forklift", CourseKind.Short, Campus.School, 2, 0, 3, Mon | Wed, 9, 250, Skills.Handling, 30, Skills.Driving, 5),
            new CourseDef(SecurityLicence, "security_licence", CourseKind.Short, Campus.School, 3, 1, 3, Tue | Thu, 9, 300, Skills.Guarding, 30, TalentCatalogue.Relationships, 5),
            new CourseDef(DrivingRefresher, "driving_refresher", CourseKind.Short, Campus.School, 1, 0, 3, Sat, 10, 90, Skills.Driving, 15, Skills.FirstAid, 2),
            new CourseDef(CodingBootcamp, "coding_bootcamp", CourseKind.Short, Campus.Online, 10, 6, 3, Mon | Wed | Fri, 18, 2500, TalentCatalogue.Coding, 25, TalentCatalogue.Frontend, 15, Bootcamp),
            new CourseDef(Bookkeeping, "bookkeeping", CourseKind.Short, Campus.School, 4, 2, 3, Tue | Thu, 13, 400, TalentCatalogue.Accounting, 20, TalentCatalogue.Analysis, 5),
            new CourseDef(CustomerService, "customer_service", CourseKind.Short, Campus.School, 2, 0, 3, Wed | Fri, 10, 120, TalentCatalogue.Relationships, 15, TalentCatalogue.Selling, 10),
            new CourseDef(DigitalMarketing, "digital_marketing", CourseKind.Short, Campus.Online, 5, 2, 3, Tue | Thu, 19, 600, TalentCatalogue.Marketing, 20, TalentCatalogue.Analysis, 5),

            // vocational diplomas at the school
            new CourseDef(Electrician, "electrician", CourseKind.Vocational, Campus.School, 14, 4, 4, Mon | Wed | Fri, 9, 3200, Skills.Electrical, 45, TalentCatalogue.Troubleshooting, 10, Diploma),
            new CourseDef(Plumber, "plumber", CourseKind.Vocational, Campus.School, 14, 4, 4, Tue | Thu | Sat, 9, 3000, Skills.Plumbing, 45, TalentCatalogue.Troubleshooting, 10, Diploma),
            new CourseDef(Carpenter, "carpenter", CourseKind.Vocational, Campus.School, 14, 4, 4, Mon | Wed | Fri, 13, 2800, Skills.Joinery, 45, TalentCatalogue.Structures, 5, Diploma),
            new CourseDef(Chef, "chef", CourseKind.Vocational, Campus.School, 12, 4, 4, Tue | Thu | Sat, 10, 2600, Skills.Cooking, 45, Skills.Hospitality, 10, Diploma),
            new CourseDef(Hairdresser, "hairdresser", CourseKind.Vocational, Campus.School, 12, 4, 4, Mon | Thu, 10, 2200, Skills.Hair, 45, TalentCatalogue.Relationships, 10, Diploma),
            new CourseDef(Beautician, "beautician", CourseKind.Vocational, Campus.School, 12, 4, 4, Wed | Fri, 10, 2200, Skills.Beauty, 45, TalentCatalogue.Relationships, 10, Diploma),
            new CourseDef(CareWorker, "care_worker", CourseKind.Vocational, Campus.School, 10, 4, 4, Mon | Wed, 13, 1800, Skills.Care, 40, Skills.Clinical, 10, Diploma),
            new CourseDef(ItSupport, "it_support", CourseKind.Vocational, Campus.School, 10, 4, 4, Tue | Thu, 13, 2400, TalentCatalogue.Troubleshooting, 35, TalentCatalogue.Networking, 20, Diploma, -1, true),

            // degrees at the university
            new CourseDef(ComputerScience, "computer_science", CourseKind.Degree, Campus.University, 24, 10, 4, Mon | Wed | Fri, 10, 9000, TalentCatalogue.Coding, 35, TalentCatalogue.SystemDesign, 25, Bachelor, -1, true),
            new CourseDef(SoftwareEngineering, "software_engineering", CourseKind.Degree, Campus.University, 24, 10, 4, Tue | Thu | Fri, 10, 9000, TalentCatalogue.Coding, 35, TalentCatalogue.Backend, 25, Bachelor, -1, true),
            new CourseDef(DataScience, "data_science", CourseKind.Degree, Campus.University, 24, 10, 4, Mon | Wed | Fri, 14, 9000, TalentCatalogue.Statistics, 35, TalentCatalogue.Learning, 25, Bachelor, -1, true),
            new CourseDef(Cybersecurity, "cybersecurity", CourseKind.Degree, Campus.University, 24, 10, 4, Tue | Thu | Fri, 14, 9000, TalentCatalogue.SecurityOps, 40, TalentCatalogue.Networking, 20, Bachelor, -1, true),
            new CourseDef(Business, "business", CourseKind.Degree, Campus.University, 22, 10, 4, Mon | Wed | Thu, 10, 8500, TalentCatalogue.Leadership, 25, TalentCatalogue.Delivery, 25, Bachelor, -1, true),
            new CourseDef(Accounting, "accounting", CourseKind.Degree, Campus.University, 22, 10, 4, Tue | Thu | Fri, 10, 8500, TalentCatalogue.Accounting, 40, TalentCatalogue.Analysis, 20, Bachelor, -1, true),
            new CourseDef(Finance, "finance", CourseKind.Degree, Campus.University, 22, 10, 4, Mon | Wed | Fri, 14, 8800, TalentCatalogue.Analysis, 40, TalentCatalogue.Accounting, 20, Bachelor, -1, true),
            new CourseDef(Law, "law", CourseKind.Degree, Campus.University, 26, 12, 4, Mon | Tue | Thu, 10, 9500, TalentCatalogue.Law, 40, TalentCatalogue.Writing, 20, Bachelor),
            new CourseDef(Marketing, "marketing", CourseKind.Degree, Campus.University, 22, 10, 4, Tue | Wed | Fri, 14, 8500, TalentCatalogue.Marketing, 40, TalentCatalogue.Selling, 15, Bachelor, -1, true),
            new CourseDef(Architecture, "architecture", CourseKind.Degree, Campus.University, 30, 14, 4, Mon | Wed | Fri, 10, 11000, TalentCatalogue.Structures, 35, TalentCatalogue.Planning, 30, Bachelor),
            new CourseDef(CivilEngineering, "civil_engineering", CourseKind.Degree, Campus.University, 28, 12, 4, Tue | Thu | Fri, 10, 10500, TalentCatalogue.Structures, 40, TalentCatalogue.Planning, 25, Bachelor),
            new CourseDef(Nursing, "nursing", CourseKind.Degree, Campus.Hospital, 28, 10, 4, Mon | Tue | Thu, 8, 9000, Skills.Clinical, 45, Skills.Care, 25, Bachelor),
            new CourseDef(Teaching, "teaching", CourseKind.Degree, Campus.University, 24, 10, 4, Mon | Wed | Thu, 14, 8500, Skills.Teaching, 45, TalentCatalogue.Writing, 15, Bachelor),
            new CourseDef(Design, "design", CourseKind.Degree, Campus.University, 22, 10, 4, Tue | Wed | Fri, 10, 8500, TalentCatalogue.Visual, 40, TalentCatalogue.Research, 20, Bachelor, -1, true),

            // after a degree
            new CourseDef(Mba, "mba", CourseKind.Postgrad, Campus.University, 14, 6, 4, Tue | Thu, 18, 18000, TalentCatalogue.Leadership, 25, TalentCatalogue.Delivery, 20, Master, -1, true, null, true),
            new CourseDef(MscComputing, "msc_computing", CourseKind.Postgrad, Campus.University, 14, 6, 4, Mon | Wed, 18, 12000, TalentCatalogue.Learning, 25, TalentCatalogue.SystemDesign, 20, Master, -1, true,
                new[] { ComputerScience, SoftwareEngineering, DataScience, Cybersecurity }),
            new CourseDef(LegalPractice, "legal_practice", CourseKind.Postgrad, Campus.University, 12, 6, 4, Mon | Wed | Fri, 14, 12000, TalentCatalogue.Law, 25, TalentCatalogue.Writing, 10, Master, -1, false, new[] { Law }),
            new CourseDef(Pgce, "pgce", CourseKind.Postgrad, Campus.University, 10, 4, 4, Tue | Thu, 14, 7000, Skills.Teaching, 30, TalentCatalogue.Leadership, 5, Master, -1, false, null, true),

            // professional certificates: the same twelve the professions' staff sit (TalentCatalogue.Certs)
            new CourseDef(CloudAssociate, "cloud_associate", CourseKind.Certificate, Campus.Online, 0, 6, 0, 0, 0, 450, TalentCatalogue.Automation, 10, TalentCatalogue.SystemDesign, 5, -1, 0),
            new CourseDef(CloudArchitect, "cloud_architect", CourseKind.Certificate, Campus.Online, 0, 8, 0, 0, 0, 1200, TalentCatalogue.SystemDesign, 12, TalentCatalogue.Automation, 5, -1, 1, false, new[] { CloudAssociate }),
            new CourseDef(SecurityPractitioner, "security_practitioner", CourseKind.Certificate, Campus.Online, 0, 8, 0, 0, 0, 950, TalentCatalogue.SecurityOps, 12, TalentCatalogue.Networking, 5, -1, 2),
            new CourseDef(ProjectManagement, "project_management", CourseKind.Certificate, Campus.Online, 0, 6, 0, 0, 0, 500, TalentCatalogue.Delivery, 12, TalentCatalogue.Leadership, 5, -1, 5),
            new CourseDef(CharteredAccountant, "chartered_accountant", CourseKind.Certificate, Campus.Online, 0, 12, 0, 0, 0, 2000, TalentCatalogue.Accounting, 15, TalentCatalogue.Analysis, 5, -1, 6, false,
                new[] { Accounting, Finance }),
            new CourseDef(BarExam, "bar_exam", CourseKind.Certificate, Campus.Online, 0, 12, 0, 0, 0, 2200, TalentCatalogue.Law, 15, TalentCatalogue.Writing, 5, -1, 8, false, new[] { LegalPractice }),
            new CourseDef(CharteredEngineer, "chartered_engineer", CourseKind.Certificate, Campus.Online, 0, 10, 0, 0, 0, 1900, TalentCatalogue.Structures, 12, TalentCatalogue.Planning, 8, -1, 7, false,
                new[] { CivilEngineering, Architecture }),
            new CourseDef(DataAnalyst, "data_analyst", CourseKind.Certificate, Campus.Online, 0, 6, 0, 0, 0, 550, TalentCatalogue.Statistics, 10, TalentCatalogue.Analysis, 8, -1, 4),
            new CourseDef(NetworkProfessional, "network_professional", CourseKind.Certificate, Campus.Online, 0, 6, 0, 0, 0, 800, TalentCatalogue.Networking, 12, TalentCatalogue.Troubleshooting, 5, -1, 3),
            new CourseDef(MachineLearning, "machine_learning", CourseKind.Certificate, Campus.Online, 0, 8, 0, 0, 0, 1300, TalentCatalogue.Learning, 12, TalentCatalogue.Statistics, 5, -1, 10),
            new CourseDef(PrivacyProfessional, "privacy_professional", CourseKind.Certificate, Campus.Online, 0, 6, 0, 0, 0, 1050, TalentCatalogue.Law, 8, TalentCatalogue.SecurityOps, 8, -1, 9),
            new CourseDef(TestingProfessional, "testing_professional", CourseKind.Certificate, Campus.Online, 0, 6, 0, 0, 0, 500, TalentCatalogue.Testing, 12, TalentCatalogue.Coding, 5, -1, 11)
        };

        public const int ArriveEarlyMinutes = 30;     // a class can be joined this long before it starts
        public const int LateMinutes = 60;            // ...and until this long after; after that it is missed
        public const int CountedShare = 750;          // thousandths of a class that must be sat for it to count
        public const int PartTimeDaysSchool = Sat;    // part time at the school: Saturday mornings
        public const int PartTimeHourSchool = 10;     // hour
        public const int PartTimeDaysUniversity = Tue | Thu;   // part time at the university: two evenings
        public const int PartTimeHourUniversity = 18;          // hour
        public const int HomeworkHours = 2;           // a piece of homework, game hours
        public const int ExamHours = 3;               // the exam, game hours
        public const int ExamWindowDays = 7;          // the exam can be sat on any weekday this many days after the last class
        public const int ExamOpens = 9, ExamCloses = 17;   // ...between these hours, at the course's campus (a certificate's at the school)

        public const int Pass = 50;                   // the pass mark of a course that is not a degree
        public const int DegreePass = 40;             // a degree's (a third)
        public const int Distinction = 80, Merit = 65;           // a course's grades
        public const int First = 70, UpperSecond = 60, LowerSecond = 50;   // a degree's classes (a third from the pass mark)
        public const int Barred = 500;                // thousandths: a student who attended fewer of the classes than this may not sit the exam
        public const int RetakesMost = 2;             // retakes after the first sitting
        public const int RetakeDays = 7;              // days after a fail before the retake can be sat
        public const int RetakeFeeShare = 150;        // thousandths of the course's fee a retake costs
        public const int RetakeFeeLeast = 50;         // whole money at the start's prices
        public const int ExamNoise = 10;              // points either way that the day itself brings
        public const int RevisionMost = 4;            // pieces of homework beyond the set ones that still count as revision

        public const int GainAtTop = 500;             // thousandths of a class's gain that is still learnt at 100 points (less the nearer the top)
        public const int HomeworkShare = 500;         // thousandths of a class's gain a piece of homework teaches
        public const int WorkLearn = 12;              // thousandths of a point of each of a job's two skills an hour of work, at 0 points
        public const int FadeAfterDays = 21;          // days a skill can go unused before it fades
        public const int FadePerDay = 40;             // thousandths of a point it fades a day after that
        public const int FadeFloor = 600;             // thousandths of the best it ever reached that it never fades below
        public const int FadeLeast = 20;              // points under which nothing fades

        public const int InstalmentsHigher = 3;       // a degree's fee is paid in thirds: at the start, a third of the way, two thirds of the way
        public const int MeritGrantShare = 250;       // thousandths off for a student whose last result was a distinction or a first
        public const int NeedGrantShare = 200;        // thousandths off for a student with little money (a bursary)
        public const int NeedGrantCash = 2000;        // ...under this much in hand, whole money at the start's prices
        public const int CouncilShare = 150;          // thousandths off (or on) by the council's policy on education
        public const int GrantMost = 500;             // thousandths off at most, all grants together
        public const int LoanRateBp = 450;            // the student loan's interest, basis points a year
        public const int LoanShareBp = 900;           // what is taken from pay above the threshold, basis points
        public const int LoanThreshold = 2500;        // cents an hour of pay above which the loan is repaid (at the start's prices)
        public const int LoanMost = 60000;            // whole money that can be owed on it at most

        public const int StressedWeekHours = 30;      // hours of classes and homework in a week over which the player is stressed
        public const int TiredEnergy = 15;            // a class or homework cannot be started below this much energy
        public const int RefresherPoints = 3;         // penalty points a driving refresher takes off the licence

        public const int TutorLeast = 30;             // points of a subject it takes to tutor it
        public const int TutorBase = 1500;            // cents an hour at the start's prices...
        public const int TutorPerPoint = 60;          // ...and this much more a point of the subject
        public const int TutorMinutes = 120;          // a session
        public const int TutorPerDay = 2;             // sessions
        public const int TutorFrom = 15, TutorTo = 21;   // sessions are given between these hours (after school), from home online
        public const int AcademyPerStaff = 12;        // students an academy's staff member teaches
        public const int AcademyDemand = 30;          // students who would come to a new academy...
        public const int AcademyFeeCents = 900;       // ...each paying this a day, at the start's prices
        public const int AcademyStaffCourseBp = 5000; // what the player's own staff pay for a course while the player has an academy

        public static CourseDef Find(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>The course of a professional certificate (TalentCatalogue.Certs), or null.</summary>
        public static CourseDef OfCert(int cert)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Cert == cert && cert >= 0)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>The key of a skill's name: the professions' own words for theirs, these for the rest.</summary>
        public static string SkillKey(int skill)
        {
            const string mod = "reallife";
            if (Skills.Professional(skill))
            {
                return mod + "_talent_skill_" + TalentCatalogue.Skills[skill];
            }
            int own = skill - TalentCatalogue.SkillCount;
            return mod + "_study_skill_" + Skills.OwnKeys[own < 0 ? 0 : own >= Skills.OwnKeys.Length ? Skills.OwnKeys.Length - 1 : own];
        }
    }
}
