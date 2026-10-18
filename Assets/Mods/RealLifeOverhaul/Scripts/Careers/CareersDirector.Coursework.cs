using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A class, homework and an exam: whether each can be done here and now, the sitting, and what it counts for.</summary>
    public sealed partial class CareersDirector
    {
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
            HomeNet net = home ? CareersBridges.HomeNetNow() : HomeNet.Online;
            return new StudyHere
            {
                School = CareersGame.InSchool(),
                Hospital = CareersGame.InHospital(),
                University = AtUniversity(),
                Home = home,
                Online = home && net == HomeNet.Online,
                Net = net
            };
        }

        /// <summary>Null if the player is where the course's classes are held now, else the reason key.</summary>
        private string AtClass(CourseDef c, StudyMode mode)
        {
            return StudyRules.AtClass(c, mode, Here());
        }

        /// <summary>Null if homework can be done here (at home, or where the classes are), else the reason key.</summary>
        private string AtDesk(CourseDef c, StudyMode mode)
        {
            if (CareersGame.AtHome())
            {
                return c.Campus == Campus.Online ? StudyRules.NoNetWhy(CareersBridges.HomeNetNow()) : null;   // homework has no hour: nothing to excuse
            }
            return c.Timetabled && mode != StudyMode.Distance && AtClass(c, mode) == null ? null : "reallife_study_why_not_desk";
        }

        /// <summary>Null if the exam can be sat here (where the classes are; online courses and certificates at the school), else the reason key.</summary>
        private string AtExam(CourseDef c, StudyMode mode)
        {
            return StudyRules.AtExam(c, mode, Here());
        }

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
                return ToRoom(e, why, SittingKind.Class);   // attended in its classroom: there, and sat down at its desk
            }
            if (why != null)
            {
                return why;
            }
            if (ClassPlayRules.HeldOnline(e.Def, (StudyMode)e.Mode))
            {
                return AtComputer(course, SittingKind.Class, null);   // online, from home: at the computer
            }
            if (AtTheSchool(e, false) && SchoolDesk.Works)
            {
                return ToSchoolDesk(e, SittingKind.Class);   // at one of the school's desks
            }
            return Offer(ClassSitting(e, minutes));   // a ward, or the university without its floors: where the player stands
        }

        /// <summary>A class as one of the game's activities: its course, which class of how many, and until when.</summary>
        private Sitting ClassSitting(Enrolment e, int minutes)
        {
            CourseDef c = e.Def;
            return new Sitting(this, SittingKind.Class, c.Id, minutes, CareersText.Course(c.Id), true,
                CareersText.T("reallife_study_skip_class", "number", (e.Seen + 1).ToString(), "of", c.Sessions.ToString()),
                CareersText.T("reallife_study_skip_until", "time", CareersText.Minutes(minutes)));
        }

        /// <summary>A piece of homework (or revision, once the set homework is done) as one of the game's activities.</summary>
        private Sitting HomeworkSitting(Enrolment e)
        {
            CourseDef c = e.Def;
            bool revision = e.Homework >= StudyRules.HomeworkSet(e) || e.Homework >= c.Homework;
            return new Sitting(this, SittingKind.Homework, c.Id, StudyCatalogue.HomeworkHours * 60, CareersText.Course(c.Id), true,
                CareersText.T(revision ? "reallife_study_skip_revision" : "reallife_study_skip_homework", "number", (e.Homework + 1).ToString(), "of", c.Homework.ToString()));
        }

        /// <summary>An exam (or its retake) as one of the game's activities; its fee is the caller's to charge.</summary>
        private Sitting ExamSitting(Enrolment e)
        {
            CourseDef c = e.Def;
            return new Sitting(this, SittingKind.Exam, c.Id, StudyCatalogue.ExamHours * 60, CareersText.Course(c.Id), true,
                CareersText.T(e.Stage == (int)EnrolStage.Retake ? "reallife_study_skip_retake" : "reallife_study_skip_exam", "pass", c.Pass.ToString()));
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
            if (CareersGame.AtHome())
            {
                return AtComputer(course, SittingKind.Homework, null);   // at home: at the computer
            }
            if (InRooms(e))
            {
                return ToRoom(e, null, SittingKind.Homework);   // at the university: at its classroom's desk
            }
            if (e.Def.Campus == Campus.School && CareersGame.InSchool() && SchoolDesk.Works)
            {
                return ToSchoolDesk(e, SittingKind.Homework);   // at one of the school's desks
            }
            return Offer(HomeworkSitting(e));   // on a ward, or the university without its floors: where the player stands
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
                return ToRoom(e, why, SittingKind.Exam);   // sat in its classroom: there, and sat down at its desk
            }
            if (why != null)
            {
                return why;
            }
            if (CareersGame.InSchool() && SchoolDesk.Works)
            {
                return ToSchoolDesk(e, SittingKind.Exam);   // at one of the school's desks; the retake's fee is charged as it begins
            }
            return SitInPlace(e);
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
                        Tell(NotificationType.Success, "reallife_study_note_exam_open", Data("course", CareersText.Course(c.Id), "day", CareersText.Day(e.ExamTo)),
                            TabStudying, CampusApp.CourseRow + c.Id);
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

        /// <summary>After a class is counted or missed: the course moves on to its exam (a short course's is sat at
        /// once, at its last class), or the student is barred. True if anything changed.</summary>
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
    }
}
