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
                _paperBefore = News.Extra;
                News.Extra = Paper;
                PlannerDirector.Instance?.AddSource(Classes);
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
