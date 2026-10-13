using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Fame;
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
        ///   the paper     education's and careers' stories, chained on to the City Ledger's other pieces (News.Extra)
        ///   the Planner   a source of its own: the classes of the courses under way, and their exams
        ///
        /// Everything is read from what those features already keep in public; nothing of theirs is changed but the
        /// licence's record.
        /// </summary>
        public static class CareersLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<TechStory> Stories = new List<TechStory>(4);
            private static readonly StudySource Classes = new StudySource();

            internal static void Connect()
            {
                CareersBridges.HomeInternet = HomeInternet;
                CareersBridges.Refresher = Refresher;
                CareersBridges.Standing = Moved;
                CareersBridges.Reputation = Reputation;
                CareersBridges.Board = Board;
                CareersBridges.TookAd = TookAd;
                CareersBridges.Shortage = Shortage;
                CareersBridges.Friends = Friends;
                CareersBridges.Record = () => Standing.HasRecord;
                CareersBridges.Cycle = () => Economy.CityEconomy.Running ? (int)Economy.CityEconomy.State : 1;
                _paperBefore = News.Extra;
                News.Extra = Paper;
                PlannerDirector.Instance?.AddSource(Classes);
                PlannerDirector.Instance?.AddSource(Jobs);
            }

            internal static void Disconnect()
            {
                CareersBridges.Reset();
                News.Extra = _paperBefore;
                _paperBefore = null;
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
