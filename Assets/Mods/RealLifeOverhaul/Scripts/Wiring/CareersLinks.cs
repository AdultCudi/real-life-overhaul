using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Education and careers with the rest of the mod (Careers/CareersBridges). Careers calls no other feature and none
        /// calls it; what they have to do with each other is here.
        ///
        ///   HomeInternet  the home the player is in, or the first they have: its internet line up and not in an outage
        ///                 (Utilities), for live classes online
        ///   Refresher     a driving refresher takes penalty points off the licence (Licences: LicenceState.TakeOff)
        ///   Standing      a qualification or a career's turn moves a part of Standing (FameDirector.Moved)
        ///   Reputation    the player's name as a professional: Standing's recognition and integrity
        ///   Board, TookAd the rivals' ads on the jobs board are vacancies too; one the player takes is filled (TechDirector.FillRivalAd)
        ///   Shortage      the labour market's shortage of a family sets what a post pays (Tech/Hiring)
        ///   Friends       friends of the social life put the player forward now and then (a referral)
        ///   Record        the law's record, as an employer reads it
        ///   Cycle         the city's economy: how many vacancies there are and how choosy employers are
        ///   Claim         a former employer's claim over a restraint clause is a civil suit of Justice's (RestraintClaims.File)
        ///   TechFirm, JoinFirm  colleagues who leave with the player join a tech firm of theirs on the payroll (TechDirector.JoinPayroll)
        ///   Founder       the venture firms read the player's record as a founder at a pitch (TechBridges.Founder)
        ///   Credit        the student loan is an account of the credit file: borrowing and paying it off are on the record
        ///   Council       the council's grants policy covers tuition too: a subsidy when it is up, a surcharge when it is down (CivicPolicy)
        ///   Children      the family's children: a grown one is offered a place at the university
        ///   CourseShareBp the player's own academy halves what their professional staff pay for a course (Tech/People)
        ///   DegreeFee     a professional can be sent on a degree, paid by the business (Tech/People)
        ///   AcademyType, Schools  BizMan's start screen counts an academy's competitors in the city: the game's school, the
        ///                 university while it is open, other academies (Tech/Firms/StartCards)
        ///   Qualified     the banking licence asks for a qualified person: a finance qualification of the player's, or a senior professional in finance
        ///   the obituary  the best qualification and the career, for Permadeath's page (PermadeathLinks)
        ///   the paper     education's and careers' stories, chained on to the City Ledger's other pieces (News.Extra)
        ///   the Planner   a source of its own: the classes of the courses under way, and their exams
        ///   the desk      a fitted floor (FitOut: a headquarters, a preset desk) keeps one of its desks for a job of the
        ///                 player's worked there: the floor asks which (FitDesks.Wanted) and says when it stands or goes
        ///
        /// Everything is read from what those features already keep in public; nothing of theirs is changed but the
        /// licence's record.
        /// </summary>
        public static class CareersLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<TechStory> Stories = new List<TechStory>(4);
            private static readonly StudySource Classes = new StudySource();

            // The utilities office, the courthouse and the police stations have chosen their buildings (or their features are
            // off), so the university can choose one that is none of them, the same on every machine.
            private static bool OthersPicked()
            {
                Utilities.UtilitiesDirector utilities = Utilities.UtilitiesDirector.Instance;
                bool office = !Settings.Utilities || (utilities != null && utilities.Office != null && utilities.Office.Searched);
                JusticeDirector justice = JusticeDirector.Instance;
                bool court = !(JusticeSettings.Enabled && JusticeSettings.Courthouse) || (justice != null && justice.Court != null && justice.Court.Searched);
                bool law = !Settings.LawPrecincts || Law.LawDirector.PrecinctsChosen;   // (the police stations take empty units too)
                return office && court && law;
            }

            internal static void Connect()
            {
                CareersBridges.HomeInternet = HomeInternet;
                CareersBridges.OthersPicked = OthersPicked;
                CareersBridges.Headquarters = (string name, out string street, out int number) =>
                {
                    street = null;
                    number = 0;
                    return Tech.TechSites.Instance != null && Tech.TechSites.Instance.HeadquartersOf(name, out street, out number);
                };
                CareersBridges.HeadquartersStamp = () => Tech.TechSites.Instance != null ? Tech.TechSites.Instance.HeadquartersStamp : 0;
                CareersBridges.FloorHere = () => Tech.TechSites.Instance != null ? Tech.TechSites.Instance.FloorCompany() : null;
                CareersBridges.Refresher = Refresher;
                CareersBridges.Standing = Moved;
                CareersBridges.Reputation = Reputation;
                CareersBridges.Board = Board;
                CareersBridges.TookAd = TookAd;
                CareersBridges.Shortage = Shortage;
                CareersBridges.Friends = Friends;
                CareersBridges.Record = () => Standing.HasRecord;
                CareersBridges.Cycle = () => Economy.CityEconomy.Running ? (int)Economy.CityEconomy.State : 1;
                CareersBridges.Claim = (amount, employer) => RestraintClaims.File(amount, employer);
                CareersBridges.Credit = Credit;
                CareersBridges.Children = Children;
                TechBridges.CourseShareBp = () => CareersDirector.Instance != null && CareersDirector.Instance.HasAcademy() ? StudyCatalogue.AcademyStaffCourseBp : 10000;
                TechBridges.DegreeFee = DegreeFee;
                TechBridges.AcademyType = () => CareersDirector.FamilyOn ? AcademyType.Type : null;
                TechBridges.Schools = Schools;
                Economy.OwnBankBridges.Qualified = Qualified;
                CareersBridges.Council = () => CivicPolicy.Position(Lever.Grants);
                Economy.CreditLedger.Register(Loan);
                CareersBridges.TechFirm = TechFirm;
                CareersBridges.JoinFirm = (place, role, level, count) => TechDirector.Instance != null && SaveGameManager.Current != null
                    ? TechDirector.Instance.JoinPayroll(place, role, level, count, SaveGameManager.Current.Day) : 0;
                TechBridges.Founder = () => CareersDirector.Instance != null ? CareersDirector.Instance.FounderBonus() : 0;
                _paperBefore = News.Extra;
                News.Extra = Paper;
                FitOut.FitDesks.Wanted = CareersDirector.DeskWanted;
                FitOut.FitDesks.Made = CareersDirector.DeskMade;
                FitOut.FitDesks.Gone = CareersDirector.DeskGone;
                PlannerDirector.Instance?.AddSource(Classes);
                PlannerDirector.Instance?.AddSource(Jobs);
                PlannerDirector.Instance?.AddSource(Contracts);
            }

            internal static void Disconnect()
            {
                CareersBridges.Reset();
                FitOut.FitDesks.Wanted = null;
                FitOut.FitDesks.Made = null;
                FitOut.FitDesks.Gone = null;
                TechBridges.Founder = null;
                TechBridges.AcademyType = null;
                TechBridges.Schools = null;
                Economy.CreditLedger.Unregister(Loan);
                Economy.OwnBankBridges.Qualified = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // The places that teach courses, as an academy's competitors: the game's school, and the university while it is open.
            private static void Schools(List<string> into)
            {
                try
                {
                    if (CareersGame.SchoolAddress(out string street, out int number))
                    {
                        into.Add(CareersText.T("reallife_study_campus_school"));
                    }
                    CareersDirector careers = CareersDirector.Instance;
                    if (careers != null && careers.University != null && careers.University.Usable)
                    {
                        into.Add(CareersText.T("reallife_study_campus_university"));
                    }
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Schools", e);
                }
            }

            private static bool HomeInternet()
            {
                try
                {
                    Utilities.UtilitiesDirector d = Utilities.UtilitiesDirector.Instance;
                    Utilities.UtilitiesState state = d != null ? d.State : null;
                    GameInstance save = SaveGameManager.Current;
                    if (!Settings.Utilities || state == null || Utilities.UtilitiesDirector.Failed || save == null)
                    {
                        return true;   // (no utilities to ask: a home is taken to be connected)
                    }
                    BuildingRegistration home = HomeTidy.Home();
                    if (home == null)
                    {
                        return false;
                    }
                    Utilities.Account a = state.Find(Utilities.UtilitiesGame.Place(home), Utilities.Service.Internet);
                    return a != null && a.State == Utilities.SupplyState.Active && !Utilities.InternetRules.InOutage(a, save.Day * 24 + save.Hour);
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.HomeInternet", e);
                    return true;
                }
            }

            private static int Refresher(int points)
            {
                try
                {
                    return Settings.LicencePoints && LicenceState.Loaded ? LicenceState.TakeOff(points, SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0) : 0;
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Refresher", e);
                    return 0;
                }
            }

            private static void Moved(int part, int points)
            {
                try
                {
                    if (points != 0 && part >= 0 && part <= (int)FamePart.Integrity)
                    {
                        FameDirector.Instance?.Moved((FamePart)part, points < 0 ? FameCause.Scandal : FameCause.Press, points);
                    }
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Standing", e);
                }
            }

            // A professional's name: a fifth of the city's recognition (0 to 5) and the integrity around its middle (-5 to 5).
            private static int Reputation()
            {
                try
                {
                    return Standing.Recognition / 20 + (Standing.Integrity - 50) / 10;
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Reputation", e);
                    return 0;
                }
            }

            // ---------------------------------------------------------------- the family's children, staff courses, the obituary, the bank

            private static void Children(List<ChildView> into)
            {
                try
                {
                    if (!FamilyState.Loaded || !Settings.Family || FamilyState.S == null)
                    {
                        return;
                    }
                    for (int i = 0; i < FamilyState.S.Children.Count; i++)
                    {
                        ChildRecord c = FamilyState.S.Children[i];
                        if (c != null && !string.IsNullOrEmpty(c.Name))
                        {
                            into.Add(new ChildView { Name = c.Name, Age = c.Age, Closeness = c.Closeness, Grown = c.Grown, Fund = c.Fund, School = c.School });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Children", e);
                }
            }

            // A degree for a professional of the player's: the dearest of the university's degrees, at today's prices.
            private static int DegreeFee()
            {
                if (!CareersDirector.UniversityOpen)
                {
                    return 0;
                }
                return Prices.At(StudyCatalogue.Find(StudyCatalogue.SoftwareEngineering).Fee);
            }

            /// <summary>The player's degree and career for the obituary (Permadeath): the best qualification, and the job now or the highest held.</summary>
            internal static void Obituary(LifeStory s)
            {
                try
                {
                    CareersDirector d = CareersDirector.Instance;
                    StudyFile f = d != null ? d.Study : null;
                    if (f == null)
                    {
                        return;
                    }
                    s.Degree = LifeStory.Clean(d.ObituaryDegree());
                    s.Career = LifeStory.Clean(d.ObituaryCareer());
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Obituary", e);
                }
            }

            /// <summary>The banking licence's qualified person (the own bank): the player with a finance qualification, or a senior professional of the player's in finance.</summary>
            private static bool Qualified()
            {
                try
                {
                    CareersDirector d = CareersDirector.Instance;
                    StudyFile f = d != null ? d.Study : null;
                    if (f == null)
                    {
                        return true;   // (careers not running: the licence asks nothing of the kind)
                    }
                    if (f.Holds(StudyCatalogue.Finance) || f.Holds(StudyCatalogue.Mba) || f.Holds(StudyCatalogue.CharteredAccountant) || f.Holds(StudyCatalogue.Accounting))
                    {
                        return true;
                    }
                    TechDirector t = TechDirector.Instance;
                    IReadOnlyList<Person> people = t != null ? t.People : null;
                    for (int i = 0; people != null && i < people.Count; i++)
                    {
                        Profile p = people[i].File;
                        if ((p.Role == TalentCatalogue.Accountant || p.Role == 29) && p.Level >= TalentCatalogue.Senior)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Qualified", e);
                    return true;
                }
            }

            // ---------------------------------------------------------------- the student loan on the credit file

            /// <summary>The student loan as one of the credit file's accounts: what is owed, no instalments (it is taken from pay).</summary>
            private sealed class StudentLoan : Economy.ICreditAccount
            {
                public string Key => "studentloan";
                public string NameKey => "reallife_study_loan_account";
                public float Balance
                {
                    get
                    {
                        StudyFile f = CareersDirector.Instance != null ? CareersDirector.Instance.Study : null;
                        return f != null ? f.LoanCents / 100f : 0f;
                    }
                }
                public float Limit => Prices.At(StudyCatalogue.LoanMost);
                public float PaymentDue => 0f;
            }

            private static readonly StudentLoan Loan = new StudentLoan();

            private static void Credit(int kind, int amount)
            {
                try
                {
                    if (!Economy.CreditLedger.Ready || SaveGameManager.Current == null)
                    {
                        return;
                    }
                    Economy.CreditLedger.Register(Loan);   // (again, if the credit file was opened afresh since the city loaded)
                    int day = SaveGameManager.Current.Day;
                    if (kind == 0)
                    {
                        Economy.CreditLedger.Post(Economy.CreditKind.NewCredit, amount, day, Loan.Key);
                    }
                    else
                    {
                        Economy.CreditLedger.Post(Economy.CreditKind.PaidOff, 0, day, Loan.Key);
                    }
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Credit", e);
                }
            }

            // ---------------------------------------------------------------- the jobs board (Tech/Hiring)

            private static void Board(List<BoardAd> into)
            {
                try
                {
                    HiringFile h = TechDirector.Instance != null ? TechDirector.Instance.Hiring : null;
                    for (int i = 0; h != null && i < h.RivalAds.Count; i++)
                    {
                        RivalAd ad = h.RivalAds[i];
                        into.Add(new BoardAd { Rival = ad.Rival, Family = ad.Family, Level = ad.Level, To = ad.To, PayBp = ad.PayBp });
                    }
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Board", e);
                }
            }

            private static void TookAd(string rival, int family, int level)
            {
                try
                {
                    TechDirector.Instance?.FillRivalAd(rival, family, level);
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.TookAd", e);
                }
            }

            private static int Shortage(int family)
            {
                try
                {
                    TechDirector d = TechDirector.Instance;
                    return d != null && d.Hiring != null && family >= 0 && family < (int)Tech.Family.Count ? d.Shortage((Tech.Family)family) : 1000;
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Shortage", e);
                    return 1000;
                }
            }

            // The first business of the player's of the tech sector's own types: a firm former colleagues can join.
            private static string TechFirm()
            {
                try
                {
                    var mine = new List<BuildingRegistration>(8);
                    TalentGame.Businesses(mine);
                    for (int i = 0; i < mine.Count; i++)
                    {
                        if (TechTypes.Is(mine[i].businessTypeName))
                        {
                            return TalentGame.Place(mine[i]);
                        }
                    }
                    return null;
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.TechFirm", e);
                    return null;
                }
            }
            // Friends and close friends of the social life (a partner counts as well).
            private static int Friends()
            {
                try
                {
                    int n = 0;
                    for (int i = 0; SocialState.Loaded && Settings.Social && i < SocialRules.CastSize; i++)
                    {
                        SocialPerson p = SocialState.Get(i);
                        n += p != null && p.Met && p.Stage >= SocialStage.Friend ? 1 : 0;
                    }
                    return n;
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Friends", e);
                    return 0;
                }
            }
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Paper (the pieces before)", e);
                }
                try
                {
                    CareersDirector director = CareersDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    Stories.Clear();
                    director.Stories(day, Stories);
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        TechStory s = Stories[i];
                        var data = new Dictionary<string, string>(2);
                        if (s.AName.Length > 0)
                        {
                            data[s.AName] = s.A;
                        }
                        if (s.BName.Length > 0)
                        {
                            data[s.BName] = s.B;
                        }
                        into.Add(new NewsItem { Key = s.Key, Data = data });
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Paper", e);
                }
            }

            // ---------------------------------------------------------------- the Planner: working days and interviews

            private sealed class WorkSource : IPlannerSource
            {
                public string Id => "profession";

                public bool Available => Settings.CareersJobs && CareersDirector.Instance != null && CareersDirector.Instance.Work != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    WorkFile w = CareersDirector.Instance.Work;
                    Careers.Job j = w.Job;
                    if (j != null)
                    {
                        Profession p = j.Def;
                        string title = CareersText.Title(j.Post.Prof, j.Post.Rank), employer = CareersText.Employer(j.Post.Employer);
                        for (int day = Math.Max(fromDay, Math.Max(j.Hired, j.SettledDay + 1)); day <= toDay; day++)
                        {
                            if (!ProRules.WorkDay(p, day) || (j.NoticeEnd > 0 && day > j.NoticeEnd))
                            {
                                continue;
                            }
                            PlannerEntry e = into.Add(Id, 0, day, PlannerCategory.Work, day);
                            if (e == null)
                            {
                                continue;
                            }
                            e.Start = p.Hour * 60;
                            e.End = ((p.Hour + p.Hours) % 24) * 60;
                            e.Attend = true;
                            // a working day can be begun until a while after its hour (ProRules.CanStart); once begun it runs to its end
                            e.JoinBy = j.TodayDay == day && j.ClockIn >= 0 ? -1 : p.Hour * 60 + ProRules.ClockLateMinutes;
                            bool home = ProRules.FromHome((WorkMode)j.Work, day);
                            if (!home && p.Place != Workplace.Hospital)
                            {
                                e.Street = j.Post.Street;
                                e.Number = j.Post.Number;
                            }
                            if (into.WantText)
                            {
                                e.Title = CareersText.T("reallife_planner_pro_title", "title", title);
                                e.Where = home ? CareersText.T("reallife_career_row_home") : employer;
                                e.What = CareersText.T("reallife_planner_pro_what", "employer", employer);
                                e.Money = CareersText.T("reallife_planner_pro_pays", "pay", CareersText.Rate(Prices.At(j.Pay)));
                                e.IfMissed = CareersText.T("reallife_planner_pro_missed", "most", CareersDirector.WarningsToGo.ToString());
                            }
                        }
                    }
                    for (int i = 0; i < w.Applications.Count; i++)
                    {
                        Application a = w.Applications[i];
                        if (a.Stage != (int)ApplyStep.Interview && a.Stage != (int)ApplyStep.Offered)
                        {
                            continue;
                        }
                        bool interview = a.Stage == (int)ApplyStep.Interview;
                        PlannerEntry e = into.Add(Id, a.Post.Id, interview ? 1 : 2, PlannerCategory.Work, interview ? a.InterviewDay : a.Lapse);
                        if (e == null)
                        {
                            continue;
                        }
                        string employer = CareersText.Employer(a.Post.Employer);
                        if (interview)
                        {
                            e.Start = a.InterviewHour * 60;
                            e.End = (a.InterviewHour + 1) * 60;
                            e.Attend = true;
                            e.JoinBy = a.InterviewHour * 60 + ProRules.InterviewLateMinutes;   // the last minute it can be joined (CareersDirector.CanInterview)
                            if (a.Post.Work != (int)WorkMode.Remote && a.Post.Def.Place != Workplace.Hospital)
                            {
                                e.Street = a.Post.Street;
                                e.Number = a.Post.Number;
                            }
                        }
                        else
                        {
                            e.Deadline = true;
                            e.End = 23 * 60;
                        }
                        if (into.WantText)
                        {
                            e.Title = CareersText.T(interview ? "reallife_planner_interview_title" : "reallife_planner_offer_title", "employer", employer);
                            e.What = CareersText.T(interview ? "reallife_planner_interview_what" : "reallife_planner_offer_what", "title", CareersText.Title(a.Post.Prof, a.Post.Rank));
                            e.IfMissed = CareersText.T(interview ? "reallife_planner_interview_missed" : "reallife_planner_offer_missed");
                        }
                    }
                }
            }

            private sealed class ContractSource : IPlannerSource
            {
                public string Id => "freelance";

                public bool Available => Settings.CareersOwn && CareersDirector.Instance != null && CareersDirector.Instance.Work != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    WorkFile w = CareersDirector.Instance.Work;
                    for (int i = 0; i < w.Contracts.Count; i++)
                    {
                        Careers.Contract c = w.Contracts[i];
                        if (c.Stage != (int)ContractStep.Taken)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, c.Id, 0, PlannerCategory.Work, Math.Max(fromDay, c.Due));
                        if (e == null)
                        {
                            continue;
                        }
                        e.Deadline = true;
                        e.End = 23 * 60;
                        if (into.WantText)
                        {
                            e.Title = CareersText.T("reallife_planner_contract_title", "client", c.Client);
                            e.What = CareersText.T("reallife_planner_contract_what", "time", CareersText.Minutes(OwnRules.Left(c)));
                            e.Money = CareersText.T("reallife_planner_contract_pays", "fee", CareersText.Money(Prices.At((int)c.Fee)));
                            e.IfMissed = CareersText.T("reallife_planner_contract_missed", "cut", (OwnRules.LateCutBp / 100).ToString() + "%");
                        }
                    }
                }
            }

            private static readonly ContractSource Contracts = new ContractSource();

            private static readonly WorkSource Jobs = new WorkSource();

            // ---------------------------------------------------------------- the Planner: classes and exams

            private sealed class StudySource : IPlannerSource
            {
                public string Id => "study";

                public bool Available => Settings.CareersStudy && CareersDirector.Instance != null && CareersDirector.Instance.Study != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    CareersDirector director = CareersDirector.Instance;
                    StudyFile f = director.Study;
                    if (f.GraduationCourse >= 0 && f.GraduationDay >= fromDay && f.GraduationDay <= toDay)
                    {
                        PlannerEntry g = into.Add(Id, 9000 + f.GraduationCourse, 0, PlannerCategory.Work, f.GraduationDay);
                        if (g != null)
                        {
                            g.Start = CareersDirector.CeremonyHour * 60;
                            g.End = (CareersDirector.CeremonyHour + CareersDirector.CeremonyMinutes / 60) * 60;
                            g.Attend = true;
                            CampusBuilding u = director.University;
                            if (u != null && u.Usable)
                            {
                                g.Street = u.Street;
                                g.Number = u.Number;
                            }
                            if (into.WantText)
                            {
                                g.Title = CareersText.T("reallife_planner_graduation_title", "course", CareersText.Course(f.GraduationCourse));
                                g.What = CareersText.T("reallife_planner_graduation_what");
                            }
                        }
                    }
                    int today = PlannerDirector.Now() / PlannerTime.MinutesPerDay;
                    for (int i = 0; i < f.Courses.Count; i++)
                    {
                        Enrolment e = f.Courses[i];
                        CourseDef c = e.Def;
                        if (c == null)
                        {
                            continue;
                        }
                        StudyMode mode = (StudyMode)e.Mode;
                        string name = CareersText.Course(c.Id);
                        if (e.Stage == (int)EnrolStage.Studying && c.Timetabled)
                        {
                            int left = c.Sessions - e.Seen;
                            int days = StudyRules.Days(c, mode), hour = StudyRules.Hour(c, mode);
                            for (int day = Math.Max(fromDay, e.SettledDay + 1); day <= toDay && left > 0; day++)
                            {
                                if (day < e.Start || !StudyRules.On(days, day))
                                {
                                    continue;
                                }
                                left--;
                                PlannerEntry p = into.Add(Id, c.Id, day, PlannerCategory.Work, day);
                                if (p == null)
                                {
                                    continue;
                                }
                                p.Start = hour * 60;
                                p.End = ((hour + c.Hours) % 24) * 60;
                                p.Attend = true;
                                p.JoinBy = director.SittingClass == c.Id && day == today ? -1 : hour * 60 + StudyCatalogue.LateMinutes;   // missed once it can no longer be joined (StudyRules.Joinable)
                                CampusBuilding uni = director.University;
                                if (mode != StudyMode.Distance && c.Campus == Campus.University && uni != null && uni.Usable)
                                {
                                    p.Street = uni.Street;
                                    p.Number = uni.Number;
                                }
                                if (mode != StudyMode.Distance && c.Campus == Campus.School)
                                {
                                    string street;
                                    int number;
                                    if (CareersGame.SchoolAddress(out street, out number))
                                    {
                                        p.Street = street;
                                        p.Number = number;
                                    }
                                }
                                if (into.WantText)
                                {
                                    p.Title = CareersText.T("reallife_planner_study_title", "course", name);
                                    p.Where = director.PlaceOf(c, mode);
                                    p.What = CareersText.T("reallife_planner_study_what", "number", (c.Sessions - left).ToString(), "of", c.Sessions.ToString());
                                    p.IfMissed = CareersText.T("reallife_planner_study_missed", "share", CareersText.Mille(StudyCatalogue.Barred));
                                }
                            }
                        }
                        else if ((e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake) && e.ExamTo >= fromDay)
                        {
                            PlannerEntry p = into.Add(Id, c.Id, -1, PlannerCategory.Work, Math.Max(fromDay, Math.Min(toDay, e.ExamTo)));
                            if (p == null)
                            {
                                continue;
                            }
                            p.Deadline = true;
                            p.End = StudyCatalogue.ExamCloses * 60;
                            p.Attend = true;
                            if (into.WantText)
                            {
                                p.Title = CareersText.T("reallife_planner_exam_title", "course", name);
                                p.What = CareersText.T("reallife_planner_exam_what", "from", CareersText.Day(e.ExamFrom), "to", CareersText.Day(e.ExamTo));
                                p.IfMissed = CareersText.T("reallife_planner_exam_missed");
                            }
                        }
                    }
                }
            }
        }
    }
}
