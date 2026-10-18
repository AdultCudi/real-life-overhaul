using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The study part: the courses the player is on and has done, and their skills (Careers/Study/StudyRules). On the hour a
    /// class gone by unattended is missed and one about to start reminded of; on the day, classes missed while the game was
    /// elsewhere are counted, a lapsed exam fails, a fee falls due, the loan's interest is added and unused skills fade.
    /// </summary>
    public sealed partial class CareersDirector
    {
        public const int TabCourses = 0, TabStudying = 1, TabResults = 2, TabSkills = 3;

        private StudyFile _study;
        private bool _studyOn;
        private PhoneApp _campusApp;
        private CampusApp _campusView;
        private bool _campusGaveUp;
        private int _reminded = -1;
        private int _excuseTold = -1;   // day * 100 + course of the outage excuse told, so it is told once

        public bool StudyRunning => _studyOn;

        /// <summary>The player's learning and skills; null while the part is off.</summary>
        public StudyFile Study => _studyOn ? _study : null;

        private int StudySeed => _study != null ? _study.Seed : 1;

        private void StartStudy()
        {
            GameInstance save = Save;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            StudyFile file = CareersStore.Load(save.modData, out version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new StudyFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day, LastDay = save.Day };
                for (int s = 0; s < Skills.Count; s++)
                {
                    file.Used[s] = save.Day;
                }
            }
            _study = file;
            _studyOn = true;
            Logger?.Info("Careers: studying " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": "
                         + file.Courses.Count + " course(s) under way, " + file.Results.Count + " result(s).");
        }

        private void StopStudy(bool keep, bool unloading)
        {
            if (!_studyOn)
            {
                return;
            }
            if (keep && Save != null && _study != null)
            {
                if (Save.modData == null)
                {
                    Save.modData = new Dictionary<string, string>();
                }
                WriteStudy(Save.modData);
            }
            StopWork(unloading);
            _studyOn = false;
            if (_sitting != null && (_sitting.Kind == SittingKind.Class || _sitting.Kind == SittingKind.Homework || _sitting.Kind == SittingKind.Exam))
            {
                CloseSitting();
            }
            if (!unloading && _campusApp != null)
            {
                _campusApp.Remove();
                _campusApp = null;
                _campusView = null;
            }
            if (!unloading)
            {
                ComputerButtons.Remove();   // switched off: the game's computer as it was
            }
        }

        private void WriteStudy(Dictionary<string, string> data)
        {
            if (_study != null && _studyOn)
            {
                data[CareersStore.Key] = CareersStore.Write(_study);
            }
        }

        /// <summary>True if a course is offered with the options as they are: the school's courses with studying, the rest with the university.</summary>
        public static bool Offered(CourseDef c)
        {
            if (c == null || !Settings.CareersStudy)
            {
                return false;
            }
            return c.Kind == CourseKind.Short || c.Kind == CourseKind.Vocational || UniversityOpen;
        }

        /// <summary>The part of the council's policy that falls on fees now: -1, 0 or 1.</summary>
        public static int Council => CareersBridges.CouncilNow();

        private void StudyHour(int day, int hour)
        {
            int minute = hour * 60;
            bool changed = false;
            for (int i = 0; i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                CourseDef c = e.Def;
                if (c == null || e.Stage != (int)EnrolStage.Studying)
                {
                    continue;
                }
                StudyMode mode = (StudyMode)e.Mode;
                if (StudyRules.Gone(c, mode, day, day, minute) && e.SettledDay < day && day >= e.Start && !InClass(c.Id))
                {
                    changed |= GoneBy(e, day);
                    changed |= AfterClass(e, day, false);
                }
                ClassDue(e, day, hour);   // a class beginning now: the log line, and a notice if it cannot be joined here
                // the reminder an hour before a class, once
                if (StudyRules.On(StudyRules.Days(c, mode), day) && StudyRules.Hour(c, mode) == hour + 1 && e.SettledDay < day && day >= e.Start)
                {
                    int key = day * 100 + c.Id;
                    if (_reminded != key)
                    {
                        _reminded = key;
                        Tell(NotificationType.Info, "reallife_study_note_class_soon",
                            Data("course", CareersText.Course(c.Id), "hour", CareersText.Hour(StudyRules.Hour(c, mode)), "place", PlaceOf(c, mode)), TabStudying, CampusApp.CourseRow + c.Id);
                    }
                }
            }
            if (changed)
            {
                Changed();
            }
        }

        /// <summary>
        /// A day's class gone by without the player: excused if held online and the home's internet was down for an outage over
        /// its join window (StudyRules.ClassExcused), else missed; decided once, when the window has shut. True if counted.
        /// </summary>
        private bool GoneBy(Enrolment e, int day)
        {
            CourseDef c = e.Def;
            StudyMode mode = (StudyMode)e.Mode;
            if (!ClassPlayRules.HeldOnline(c, mode) || !StudyRules.JoinWindow(c, mode, day, out int from, out int to))
            {
                return StudyRules.Miss(e, day);
            }
            return GoneBy(e, day, from, to);
        }

        /// <summary>An online class gone by, with the home's internet read over the minutes given (its window; the test passes now).</summary>
        private bool GoneBy(Enrolment e, int day, int from, int to)
        {
            CourseDef c = e.Def;
            StudyMode mode = (StudyMode)e.Mode;
            HomeNet net = CareersBridges.HomeNetBetween(from, to);
            bool excused = StudyRules.ClassExcused(c, mode, net);
            int done = e.Done, before = e.Excused;
            bool counted = excused ? StudyRules.Excuse(e, day) : StudyRules.Miss(e, day);
            if (!counted)
            {
                return false;
            }
            int key = day * 100 + c.Id;
            if (excused && _excuseTold != key)
            {
                _excuseTold = key;
                Tell(NotificationType.Info, "reallife_study_note_class_excused", Data("course", CareersText.Course(c.Id), "day", CareersText.Day(day)), TabStudying, CampusApp.CourseRow + c.Id);
            }
            if (Verbose)
            {
                bool right = excused == StudyRules.Excuses(net) && e.Done == done && e.Excused == before + (excused ? 1 : 0)
                             && StudyRules.Counted(e) == c.Sessions - e.Excused;
                Logger?.Info("Careers check (online class gone by): " + c.Key + " on day " + day + ", the home's internet " + (net == HomeNet.Online ? "up" : "down (" + net + ")")
                             + " over its window; " + (excused ? "excused, left out of attendance (" : "missed (") + e.Done + " attended of " + StudyRules.Counted(e) + " counted, "
                             + e.Excused + " excused); wanted " + (StudyRules.Excuses(net) ? "excused" : "missed") + ": " + (right ? "PASS" : "FAIL, the count does not match the rule"));
            }
            return true;
        }

        private bool InClass(int course) => _sitting != null && _sitting.Kind == SittingKind.Class && _sitting.Of == course;

        /// <summary>The course whose class the player is sitting now, -1 for none: what <see cref="StudyRules.Held"/> and <see cref="StudyRules.NextClass(Enrolment, int, int, int)"/> take.</summary>
        internal int SittingClass => _sitting != null && _sitting.Kind == SittingKind.Class ? _sitting.Of : -1;

        private void StudyDay(int day)
        {
            int last = _study.LastDay;
            _study.LastDay = day;
            for (int i = _study.Courses.Count - 1; i >= 0; i--)
            {
                Enrolment e = _study.Courses[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    _study.Courses.RemoveAt(i);
                    continue;
                }
                // Classes of the days the game passed without an hour of ours (a long sleep, a load): missed.
                if (e.Stage == (int)EnrolStage.Studying && c.Timetabled)
                {
                    int from = Math.Max(e.Start, Math.Max(e.SettledDay + 1, last > 0 ? last - 14 : day - 14));
                    for (int d = from; d < day; d++)
                    {
                        if (StudyRules.On(StudyRules.Days(c, (StudyMode)e.Mode), d) && GoneBy(e, d))
                        {
                            AfterClass(e, d, false);
                        }
                    }
                }
                if (StudyRules.ExamMissed(e, day))
                {
                    Results(e, day, false);
                    continue;
                }
                Instalment(e);
            }
            long interest = StudyRules.LoanInterest(_study.LoanCents, GameCalendar.DaysPerYear);
            _study.LoanCents += interest;
            int faded = StudyRules.Fade(_study, day);
            if (Verbose && (faded > 0 || interest > 0))
            {
                Logger?.Info("Careers check (study day " + day + "): skills faded " + faded + " thousandths in all, loan interest " + interest + " cents: PASS");
            }
            GraduationDay(day);
            FamilyDay(day);
        }

        /// <summary>An instalment that has fallen due is charged, or lent; one that cannot be paid stands as owed and the classes wait for it.</summary>
        private void Instalment(Enrolment e)
        {
            CourseDef c = e.Def;
            int due = Prices.At(StudyRules.InstalmentDue(e));
            if (due <= 0 || e.Stage != (int)EnrolStage.Studying)
            {
                return;
            }
            string name = CareersText.Course(c.Id);
            if (e.OnLoan && LoanOn && StudyRules.LoanRoom(_study, due, Prices.Level))
            {
                Lend(e, due);
            }
            else if (!CareersGame.Charge(due, CareersGame.FeeKey, name))
            {
                if (!e.FeeLate)
                {
                    e.FeeLate = true;
                    Tell(NotificationType.Warning, "reallife_study_note_fee_owed", Data("course", name, "amount", CareersText.Money(due)), TabStudying, CampusApp.CourseRow + c.Id);
                }
                return;
            }
            else
            {
                e.Fee += due;
            }
            e.FeeLate = false;
            e.Instalments++;
        }

        /// <summary>The fee instalment owed now on a course, whole money at today's prices (0 for none).</summary>
        public int Owed(Enrolment e)
        {
            return e != null && e.FeeLate ? Prices.At(StudyRules.InstalmentDue(e)) : 0;
        }

        /// <summary>The player pays an instalment that was owed. Null if done, else a reason key.</summary>
        public string PayOwed(int course)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            int owed = Owed(e);
            if (owed <= 0)
            {
                return null;
            }
            if (!CareersGame.Charge(owed, CareersGame.FeeKey, CareersText.Course(course)))
            {
                return "reallife_study_why_money";
            }
            e.Fee += owed;
            e.Instalments++;
            e.FeeLate = false;
            Changed();
            return null;
        }

        /// <summary>The share of a course's fee the player would have off now, thousandths.</summary>
        public int GrantFor(CourseDef c)
        {
            return _studyOn ? StudyRules.Grant(_study, c, CareersGame.Cash, Prices.Level, Council) : 0;
        }

        /// <summary>Enrols on a course: the first instalment paid (or lent). Null if done, else a reason key.</summary>
        public string Enrol(int course, StudyMode mode, bool loan)
        {
            CourseDef c = StudyCatalogue.Find(course);
            if (!_studyOn || c == null || !Offered(c))
            {
                return "reallife_study_why_closed";
            }
            EnrolRefusal refusal = StudyRules.CanEnrol(_study, c, mode);
            if (refusal == EnrolRefusal.Passed && c.Id == StudyCatalogue.DrivingRefresher)
            {
                refusal = EnrolRefusal.None;   // a refresher can be taken again
            }
            if (refusal != EnrolRefusal.None)
            {
                return "reallife" + "_study_why_" + Refusal(refusal);   // put together, so the text check reads only whole keys
            }
            int day = Save.Day;
            var e = new Enrolment { Course = c.Id, Mode = (int)mode, Start = day, Grant = GrantFor(c), OnLoan = loan && c.Loanable && LoanOn };
            int due = Prices.At(StudyRules.InstalmentDue(e));
            string name = CareersText.Course(c.Id);
            if (e.OnLoan && !StudyRules.LoanRoom(_study, due, Prices.Level))
            {
                return "reallife_study_why_loan_full";
            }
            if (e.OnLoan)
            {
                Lend(e, due);
            }
            else if (!CareersGame.Charge(due, CareersGame.FeeKey, name))
            {
                return "reallife_study_why_money";
            }
            else
            {
                e.Fee += due;
            }
            e.Instalments = 1;
            // A class later today can still be joined; one already gone today is not the student's to miss.
            if (StudyRules.Gone(c, mode, day, day, CareersGame.MinuteOfDay))
            {
                e.SettledDay = day;
            }
            _study.Courses.Add(e);
            if (!c.Timetabled)
            {
                StudyRules.Advance(e, day);   // a certificate with no homework set goes straight to its exam
            }
            int next = StudyRules.NextClass(e, day, CareersGame.MinuteOfDay, -1) / (24 * 60);
            Tell(NotificationType.Success, c.Timetabled ? "reallife_study_note_enrolled" : "reallife_study_note_enrolled_home",
                Data("course", name, "day", next > 0 ? CareersText.Day(next) : "-", "hour", CareersText.Hour(StudyRules.Hour(c, mode))), TabStudying, CampusApp.CourseRow + c.Id);
            Logger?.Info("Careers: enrolled on " + c.Key + " (" + mode + ") on day " + day + ", grant " + e.Grant + " thousandths, " + (e.OnLoan ? "on the loan" : "paid") + " " + due + ".");
            Changed();
            return null;
        }

        private static string Refusal(EnrolRefusal r)
        {
            switch (r)
            {
                case EnrolRefusal.Already: return "already";
                case EnrolRefusal.Passed: return "passed";
                case EnrolRefusal.Needs: return "needs";
                case EnrolRefusal.Full: return "full";
                case EnrolRefusal.NoMoney: return "money";
                case EnrolRefusal.NoDistance: return "distance";
                default: return "closed";
            }
        }

        /// <summary>Leaves a course: nothing paid comes back, and a loan stays owed.</summary>
        public void Withdraw(int course)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            if (e == null)
            {
                return;
            }
            e.Stage = (int)EnrolStage.Withdrawn;
            e.Ended = Save.Day;
            _study.Finish(e);
            Tell(NotificationType.Info, "reallife_study_note_withdrawn", Data("course", CareersText.Course(course)), TabResults, null);
            Changed();
        }

        private void InstallCampus()
        {
            var view = new CampusApp(this);
            GameSprites.Want(CampusGlyphs);
            bool tryAgain;
            _campusApp = PhoneApp.Install(new PhoneAppDef
            {
                Id = CampusAppId,
                TitleKey = "reallife_campus_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(CampusGlyphs), new Color32(232, 176, 72, 255), new Color32(168, 96, 40, 255), 0.42f, "AppIcon-Campus"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_campusApp == null)
            {
                _campusGaveUp = !tryAgain;
                if (_campusGaveUp)
                {
                    Logger?.Warn("Careers: the game's phone is not built as this version expects, so the Campus app is left off."
                                 + " Courses under way still run and the notifications still say what is due.");
                }
                return;
            }
            _campusView = view;
            CampusBadge();
        }

        private static readonly string[] CampusGlyphs = { "Icon-Education", "Icon-School", "Icon-Book", "Icon-Building" };

        private static Sprite Glyph(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Sprite sprite = GameSprites.Get(names[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        public void OpenCampus(int tab, string pick)
        {
            if (_campusApp != null && _campusView != null)
            {
                _campusView.Want(tab, pick);
                _campusApp.Open();
            }
        }

        /// <summary>The Campus app's badge: what can be done now (a class to join, an exam to sit, a fee owed).</summary>
        private void CampusBadge()
        {
            if (_campusApp == null || !_studyOn || Save == null)
            {
                return;
            }
            int count = 0;
            for (int i = 0; i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                int minutes;
                count += e.FeeLate || StudyRules.ExamDay(e, Save.Day) || (e.Stage == (int)EnrolStage.Studying && e.SettledDay < Save.Day && e.Def != null
                                                                              && StudyRules.Joinable(e.Def, (StudyMode)e.Mode, Save.Day, CareersGame.MinuteOfDay, out minutes)) ? 1 : 0;
            }
            _campusApp.SetBadge(count);
        }

        /// <summary>True while the student loan can be used (the university's part).</summary>
        private static bool LoanOn => UniversityOpen;

        private void Lend(Enrolment e, int whole)
        {
            e.Loan += whole;
            _study.LoanCents += whole * 100L;
            _study.LoanTaken += whole * 100L;
            Borrowed(whole);
        }
    }
}
