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
        /// Education and careers (CareersBridges) with the internet at home, the licence's points (the one thing of
        /// another feature's it changes), Standing, the jobs board and the tech labour market, the social life, the record,
        /// the economy's vacancies, the courts, the student loan on the credit file, the council's grants, the family's
        /// children, the player's bank, the obituary, the paper, a fitted floor's desk and the Planner.
        /// </summary>
        public static class CareersLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<TechStory> Stories = new List<TechStory>(4);
            private static readonly StudySource Classes = new StudySource();

            internal static void Connect()
            {
                // The university's building is held for it from the start, before any feature chooses one, while its part is on.
                SpecialBuildings.Reserve(CampusRules.BookId, CampusRules.HomeStreet, CampusRules.HomeNumber, () => CareersDirector.UniversityOpen, SpecialKind.Campus);
                CareersBridges.HomeInternet = HomeInternet;
                CareersBridges.OthersPicked = OfficeLinks.CityOfficesPicked;   // so the university chooses none of theirs, the same on every machine
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
                Economy.EconomyBridges.OtherAccounts = StudentCredit;   // again at each start of the credit side, whose ledger forgets every account
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
                SpecialBuildings.Unreserve(CampusRules.BookId);
                CareersBridges.Reset();
                FitOut.FitDesks.Wanted = null;
                FitOut.FitDesks.Made = null;
                FitOut.FitDesks.Gone = null;
                TechBridges.Founder = null;
                TechBridges.AcademyType = null;
                TechBridges.Schools = null;
                Economy.CreditLedger.Unregister(Loan);
                Economy.EconomyBridges.OtherAccounts = null;
                Economy.OwnBankBridges.Qualified = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // The places that teach courses, as an academy's competitors: the game's school, and the university while it is open.
            private static void Schools(List<string> into)
            {
                try
                {
                    if (CareersGame.SchoolAddress(out _, out _))
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

            // The home's connection over a span of minutes, read when a class, a day or an interview is asked about or goes
            // by: one search of the accounts. The outages are drawn from the save's seed (the host's in a shared city).
            private static HomeNet HomeInternet(int fromMinute, int toMinute)
            {
                try
                {
                    Utilities.UtilitiesDirector d = Utilities.UtilitiesDirector.Instance;
                    Utilities.UtilitiesState state = d != null ? d.State : null;
                    GameInstance save = SaveGameManager.Current;
                    if (!Settings.Utilities || state == null || Utilities.UtilitiesDirector.Failed || save == null)
                    {
                        return HomeNet.Online;   // no utilities to ask: a home is taken to be connected
                    }
                    BuildingRegistration home = HomeTidy.Home();
                    if (home == null)
                    {
                        return HomeNet.NoHome;
                    }
                    if (fromMinute < 0)
                    {
                        fromMinute = toMinute = save.Day * 24 * 60 + save.Hour * 60 + (int)save.Minute;
                    }
                    Utilities.Account a = state.Find(Utilities.UtilitiesGame.Place(home), Utilities.Service.Internet);
                    switch (Utilities.InternetRules.Down(a, fromMinute, toMinute))
                    {
                        case Utilities.NetDown.Up: return HomeNet.Online;
                        case Utilities.NetDown.Outage: return HomeNet.Outage;
                        case Utilities.NetDown.Installing: return HomeNet.Installing;
                        case Utilities.NetDown.Cut: return HomeNet.Cut;
                        default: return HomeNet.NoPlan;
                    }
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.HomeInternet", e);
                    return HomeNet.Online;
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
                try
                {
                    if (!CareersDirector.UniversityOpen)
                    {
                        return 0;
                    }
                    return Prices.At(StudyCatalogue.Find(StudyCatalogue.SoftwareEngineering).Fee);
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.DegreeFee", e);
                    return 0;
                }
            }

            /// <summary>The player's best qualification, and the job now or the highest held, for the obituary.</summary>
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

            // The banking licence's qualified person: the player with a finance qualification, or a senior professional of theirs in finance.
            private static bool Qualified()
            {
                try
                {
                    CareersDirector d = CareersDirector.Instance;
                    StudyFile f = d != null ? d.Study : null;
                    if (f == null)
                    {
                        return true;   // careers not running: the licence asks nothing of the kind
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
                        if ((p.Role == TalentCatalogue.Accountant || p.Role == TalentCatalogue.FinancialAnalyst) && p.Level >= TalentCatalogue.Senior)
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

            private static void StudentCredit(List<Economy.ICreditAccount> into)
            {
                try
                {
                    into.Add(Loan);
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.StudentCredit", e);
                }
            }

            private static void Credit(int kind, int amount)
            {
                try
                {
                    if (!Economy.CreditLedger.Ready || SaveGameManager.Current == null)
                    {
                        return;
                    }
                    Economy.CreditLedger.Register(Loan);   // again, in case the credit file was opened afresh since the city loaded
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

            // Friends and close friends of the social life, a partner too.
            private static int Friends()
            {
                try
                {
                    int n = 0;
                    for (int i = 0; SocialState.Loaded && Settings.Social && i < SocialState.Count; i++)
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
                    TechLinks.AddStories(Stories, into);
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("CareersLinks.Paper", e);
                }
            }

            // A profession's working days, interviews and offers in the Planner.
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
                            e.JoinBy = a.InterviewHour * 60 + ProRules.InterviewLateMinutes;   // the last minute it can be joined
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

            // Freelance contracts' deadlines in the Planner.
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

            // Classes, exams and a graduation in the Planner.
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
                                // missed once it can no longer be joined (StudyRules.Joinable)
                                p.JoinBy = director.SittingClass == c.Id && day == today ? -1 : hour * 60 + StudyCatalogue.LateMinutes;
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
