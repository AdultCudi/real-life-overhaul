using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The study part: the courses the player is on and has done, and their skills (Careers/Study/StudyRules). On the hour
    /// a class that has gone by unattended is counted as missed, and a class about to start is reminded of; on the day,
    /// classes missed while the game was elsewhere are counted, a lapsed exam is a fail, a fee instalment falls due, the
    /// student loan's interest is added and unused skills fade a little. A class, homework or an exam is one of the game's
    /// activities, offered from the Campus app where it can be done.
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

        public bool StudyRunning => _studyOn;

        /// <summary>The player's learning and skills; null while the part is off.</summary>
        public StudyFile Study => _studyOn ? _study : null;

        private int StudySeed => _study != null ? _study.Seed : 1;

        // ---------------------------------------------------------------- starting and stopping

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
        }

        private void WriteStudy(Dictionary<string, string> data)
        {
            if (_study != null && _studyOn)
            {
                data[CareersStore.Key] = CareersStore.Write(_study);
            }
        }

        // ---------------------------------------------------------------- what is on offer

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

        // ---------------------------------------------------------------- the hour and the day

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
                    changed |= StudyRules.Miss(e, day);
                    changed |= AfterClass(e, day, false);
                }
                ClassDue(e, day, hour);   // a class beginning now: the log line, and a notice if it cannot be joined where the player is
                // A class about to start: the reminder an hour before (once a class).
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

        private bool InClass(int course) => _sitting != null && _sitting.Kind == SittingKind.Class && _sitting.Of == course;

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
                        if (StudyRules.On(StudyRules.Days(c, (StudyMode)e.Mode), d) && StudyRules.Miss(e, d))
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
                Instalment(e, day);
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
        private void Instalment(Enrolment e, int day)
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

        // ---------------------------------------------------------------- enrolling

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
                return "reallife" + "_study_why_" + Refusal(refusal);   // (put together, so the text check reads only whole keys)
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
                StudyRules.Advance(e, day);   // (a certificate with nothing to study: never)
            }
            int next = StudyRules.Next(c, mode, e.SettledDay >= day ? day + 1 : day);
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

        // ---------------------------------------------------------------- where it can be done

        /// <summary>The words for where a course's classes are.</summary>
        public string PlaceOf(CourseDef c, StudyMode mode)
        {
            if (mode == StudyMode.Distance || c.Campus == Campus.Online)
            {
                return CareersText.Campus(Campus.Online);
            }
            if (c.Campus == Campus.University && CampusRules.FloorOf(c.Id) > 0 && _interior != null && _interior.Available)
            {
                return CareersText.Fill("reallife_campus_place_room", "campus", CampusWhere(c.Campus), "floor", CampusRules.FloorOf(c.Id).ToString(), "room", CampusRules.RoomOf(c.Id).ToString());
            }
            return CampusWhere(c.Campus);
        }

        /// <summary>Where the player is now, as study reads it.</summary>
        private StudyHere Here()
        {
            bool home = CareersGame.AtHome();
            return new StudyHere
            {
                School = CareersGame.InSchool(),
                Hospital = CareersGame.InHospital(),
                University = AtUniversity(),
                Home = home,
                Online = home && CareersBridges.HomeOnline()
            };
        }

        /// <summary>True if the player is where a course's classes are held now; else the reason key.</summary>
        private string AtClass(CourseDef c, StudyMode mode)
        {
            return StudyRules.AtClass(c, mode, Here());
        }

        /// <summary>Homework is done at home or where the classes are.</summary>
        private string AtDesk(CourseDef c, StudyMode mode)
        {
            if (CareersGame.AtHome())
            {
                return c.Campus == Campus.Online && !CareersBridges.HomeOnline() ? "reallife_study_why_no_internet" : null;
            }
            return c.Timetabled && mode != StudyMode.Distance && AtClass(c, mode) == null ? null : "reallife_study_why_not_desk";
        }

        /// <summary>Exams are sat where the classes are; online courses and certificates are examined at the school.</summary>
        private string AtExam(CourseDef c, StudyMode mode)
        {
            return StudyRules.AtExam(c, mode, Here());
        }

        // ---------------------------------------------------------------- a class, homework, an exam

        /// <summary>Whether the class of a course can be joined now, and how long it still runs; else the reason key.</summary>
        public string CanAttend(Enrolment e, out int minutes)
        {
            minutes = 0;
            CourseDef c = e != null ? e.Def : null;
            if (c == null || e.Stage != (int)EnrolStage.Studying || !c.Timetabled)
            {
                return "reallife_study_why_no_class";
            }
            if (e.FeeLate)
            {
                return "reallife_study_why_fee_owed";
            }
            StudyMode mode = (StudyMode)e.Mode;
            if (e.SettledDay >= Save.Day || !StudyRules.Joinable(c, mode, Save.Day, CareersGame.MinuteOfDay, out minutes))
            {
                return "reallife_study_why_no_class";
            }
            return AtClass(c, mode);
        }

        public string Attend(int course)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            int minutes;
            string why = CanAttend(e, out minutes);
            if (InRooms(e))
            {
                return ToRoom(e, why);   // (attended in its classroom: the way there)
            }
            if (why != null)
            {
                return why;
            }
            CourseDef c = e.Def;
            return Offer(new Sitting(this, SittingKind.Class, c.Id, minutes, CareersText.Course(c.Id), true,
                CareersText.T("reallife_study_skip_class", "number", (e.Seen + 1).ToString(), "of", c.Sessions.ToString()),
                CareersText.T("reallife_study_skip_until", "time", CareersText.Minutes(minutes))));
        }

        public string CanHomework(Enrolment e)
        {
            CourseDef c = e != null ? e.Def : null;
            if (c == null || !StudyRules.HomeworkOpen(e))
            {
                return "reallife_study_why_no_homework";
            }
            return AtDesk(c, (StudyMode)e.Mode);
        }

        public string Homework(int course)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            string why = CanHomework(e);
            if (why != null)
            {
                return why;
            }
            CourseDef c = e.Def;
            bool revision = e.Homework >= StudyRules.HomeworkSet(e) || e.Homework >= c.Homework;
            return Offer(new Sitting(this, SittingKind.Homework, c.Id, StudyCatalogue.HomeworkHours * 60, CareersText.Course(c.Id), true,
                CareersText.T(revision ? "reallife_study_skip_revision" : "reallife_study_skip_homework", "number", (e.Homework + 1).ToString(), "of", c.Homework.ToString())));
        }

        public string CanSit(Enrolment e)
        {
            CourseDef c = e != null ? e.Def : null;
            if (c == null || !StudyRules.ExamDay(e, Save.Day))
            {
                return "reallife_study_why_no_exam";
            }
            int hour = Save.Hour;
            if (hour < StudyCatalogue.ExamOpens || hour >= StudyCatalogue.ExamCloses)
            {
                return "reallife_study_why_exam_hours";
            }
            if (e.Stage == (int)EnrolStage.Retake && e.FeeLate)
            {
                return "reallife_study_why_fee_owed";
            }
            return AtExam(c, (StudyMode)e.Mode);
        }

        public string Sit(int course)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            string why = CanSit(e);
            if (InRooms(e))
            {
                return ToRoom(e, why);   // (sat in its classroom: the way there)
            }
            if (why != null)
            {
                return why;
            }
            CourseDef c = e.Def;
            if (e.Stage == (int)EnrolStage.Retake)
            {
                int fee = Prices.At(StudyRules.RetakeFee(c));
                if (!CareersGame.Charge(fee, CareersGame.FeeKey, CareersText.Course(c.Id)))
                {
                    return "reallife_study_why_money";
                }
                e.Fee += fee;
            }
            return Offer(new Sitting(this, SittingKind.Exam, c.Id, StudyCatalogue.ExamHours * 60, CareersText.Course(c.Id), true,
                CareersText.T(e.Stage == (int)EnrolStage.Retake ? "reallife_study_skip_retake" : "reallife_study_skip_exam", "pass", c.Pass.ToString())));
        }

        private void StudyEnded(Sitting s, int minutes, bool whole)
        {
            Enrolment e = _study.Live(s.Of);
            CourseDef c = e != null ? e.Def : null;
            int day = Save.Day;
            if (c == null)
            {
                return;
            }
            bool stressed = StudyRules.Studied(_study, day, minutes);
            if (stressed)
            {
                CareersGame.Mood(ModState.Stressed);
                Tell(NotificationType.Warning, "reallife_study_note_stressed", Data("hours", StudyCatalogue.StressedWeekHours.ToString()), TabStudying, null);
            }
            int learning = StudyRules.Learning(StudyRules.School(_study));
            switch (s.Kind)
            {
                case SittingKind.Class:
                    if (minutes * 1000 < s.Length * StudyCatalogue.CountedShare)
                    {
                        Tell(NotificationType.Warning, "reallife_study_note_left_early", Data("course", CareersText.Course(c.Id)), TabStudying, CampusApp.CourseRow + c.Id);
                        return;
                    }
                    if (StudyRules.Attend(e, day))
                    {
                        Teach(c.SkillA, StudyRules.ClassGain(c.GainA, c.Sessions, StudyRules.Points(_study, c.SkillA), learning), day);
                        Teach(c.SkillB, StudyRules.ClassGain(c.GainB, c.Sessions, StudyRules.Points(_study, c.SkillB), learning), day);
                        AfterClass(e, day, true);
                    }
                    break;
                case SittingKind.Homework:
                    if (!whole)
                    {
                        return;
                    }
                    e.Homework++;
                    Teach(c.SkillA, StudyRules.HomeworkGain(c, true, StudyRules.Points(_study, c.SkillA), learning), day);
                    Teach(c.SkillB, StudyRules.HomeworkGain(c, false, StudyRules.Points(_study, c.SkillB), learning), day);
                    if (!c.Timetabled && StudyRules.Advance(e, day))
                    {
                        Tell(NotificationType.Success, "reallife_study_note_exam_open", Data("course", CareersText.Course(c.Id), "day", CareersText.Day(e.ExamTo)), TabStudying, CampusApp.CourseRow + c.Id);
                    }
                    break;
                case SittingKind.Exam:
                    if (!whole)
                    {
                        Tell(NotificationType.Warning, "reallife_study_note_exam_left", Data("course", CareersText.Course(c.Id)), TabStudying, CampusApp.CourseRow + c.Id);
                        return;
                    }
                    Examine(e, day);
                    break;
            }
        }

        private void Teach(int skill, int thousandths, int day)
        {
            StudyRules.Learn(_study, skill, thousandths, day);
        }

        /// <summary>After a class is counted or missed: the course moves on to its exam (a short course's is sat at once, at its last class), or the student is barred. True if anything changed.</summary>
        private bool AfterClass(Enrolment e, int day, bool attended)
        {
            CourseDef c = e.Def;
            if (!StudyRules.Advance(e, day))
            {
                return attended;
            }
            string name = CareersText.Course(c.Id);
            if (e.Stage == (int)EnrolStage.Barred)
            {
                _study.Finish(e);
                CareersGame.Mood(ModState.MoodDown);
                Tell(NotificationType.Warning, "reallife_study_note_barred", Data("course", name, "share", CareersText.Mille(StudyCatalogue.Barred)), TabResults, null);
                return true;
            }
            if (c.Kind == CourseKind.Short && attended)
            {
                Examine(e, day);   // a short course's assessment is its last class
                return true;
            }
            Tell(NotificationType.Info, "reallife_study_note_exam_open", Data("course", name, "day", CareersText.Day(e.ExamTo)), TabStudying, CampusApp.CourseRow + c.Id);
            return true;
        }

        private void Examine(Enrolment e, int day)
        {
            CourseDef c = e.Def;
            int score = StudyRules.ExamScore(e, StudyRules.Points(_study, c.SkillA), CareersGame.Energy < 30f, StudyRules.Luck(StudySeed, e));
            bool passed = StudyRules.Sit(e, score, day);
            StudyRules.Use(_study, c.SkillA, day);
            StudyRules.Use(_study, c.SkillB, day);
            Results(e, day, passed);
        }

        /// <summary>A result: a pass and its grade, a fail with a retake to come, or a fail for good.</summary>
        private void Results(Enrolment e, int day, bool passed)
        {
            CourseDef c = e.Def;
            string name = CareersText.Course(c.Id);
            if (passed)
            {
                _study.Finish(e);
                bool top = e.Grade >= StudyRules.TopGrade(c);
                CareersGame.Mood(top ? ModState.MoodUpBig : ModState.MoodUp);
                Tell(NotificationType.Success, "reallife_study_note_passed", Data("course", name, "grade", CareersText.Grade(c, e.Grade), "score", e.Score.ToString()), TabResults, null);
                Passed(e, day);
                return;
            }
            CareersGame.Mood(ModState.MoodDown);
            if (e.Stage == (int)EnrolStage.Retake)
            {
                Tell(NotificationType.Warning, "reallife_study_note_failed_retake", Data("course", name, "score", e.Score.ToString(), "day", CareersText.Day(e.ExamFrom)), TabStudying,
                    CampusApp.CourseRow + c.Id);
                return;
            }
            _study.Finish(e);
            Tell(NotificationType.Warning, "reallife_study_note_failed", Data("course", name, "score", e.Score.ToString()), TabResults, null);
        }

        /// <summary>What a pass brings beyond the qualification itself.</summary>
        private void Passed(Enrolment e, int day)
        {
            CourseDef c = e.Def;
            if (c.Id == StudyCatalogue.DrivingRefresher)
            {
                int off = 0;
                try
                {
                    off = CareersBridges.Refresher != null ? CareersBridges.Refresher(StudyCatalogue.RefresherPoints) : 0;
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
                if (off > 0)
                {
                    Tell(NotificationType.Success, "reallife_study_note_points_off", Data("points", off.ToString()), TabResults, null);
                }
            }
            PassedMore(e, day);
        }

        // ---------------------------------------------------------------- the phone

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
                    Logger?.Warn("Careers: the game's phone is not built as this version expects, so the Campus app is left off. Courses under way still run and the notifications still say what is due.");
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

        // ---------------------------------------------------------------- what the later parts add (each in its own file)

        /// <summary>True while the student loan can be used (the university's part).</summary>
        private static bool LoanOn => UniversityOpen;


        private void Lend(Enrolment e, int whole)
        {
            e.Loan += whole;
            _study.LoanCents += whole * 100L;
            _study.LoanTaken += whole * 100L;
            Borrowed(whole);
        }

        partial void Borrowed(int whole);

        partial void PassedMore(Enrolment e, int day);
    }
}
