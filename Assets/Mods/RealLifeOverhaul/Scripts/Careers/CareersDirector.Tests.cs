using System;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The test buttons of education and careers (Troubleshooting on the options page): each puts the player where a part
    /// can be seen at once, without waiting for the timetable or walking to the school. A test changes the save as the real
    /// thing would; each says what it did in a notification and in Player.log.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private bool TestReady(out string why)
        {
            why = null;
            if (_broken)
            {
                why = "reallife_study_test_off";
            }
            else if (!_studyOn || Save == null)
            {
                why = "reallife_study_test_off";
            }
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f);
                return false;
            }
            return true;
        }

        /// <summary>A class of the first course under way, now and here (first aid is enrolled on free if there is none).</summary>
        public void TestClass()
        {
            string why;
            if (!TestReady(out why))
            {
                return;
            }
            try
            {
                Enrolment e = null;
                for (int i = 0; i < _study.Courses.Count && e == null; i++)
                {
                    e = _study.Courses[i].Stage == (int)EnrolStage.Studying && _study.Courses[i].Def.Timetabled ? _study.Courses[i] : null;
                }
                if (e == null)
                {
                    e = new Enrolment { Course = StudyCatalogue.FirstAid, Start = Save.Day, Instalments = 1 };
                    _study.Courses.Add(e);
                }
                e.SettledDay = Math.Min(e.SettledDay, Save.Day - 1);
                CourseDef c = e.Def;
                string busy = Offer(new Sitting(this, SittingKind.Class, c.Id, c.Hours * 60, CareersText.Course(c.Id), true,
                    CareersText.T("reallife_study_skip_class", "number", (e.Seen + 1).ToString(), "of", c.Sessions.ToString())));
                Logger?.Info("Careers test: a class of " + c.Key + (busy == null ? " offered." : " not offered (" + busy + ")."));
                if (busy != null)
                {
                    Notifications.Show(NotificationType.Warning, busy, null, 8f);
                }
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>The exam of the first course under way, marked now as if it had been sat.</summary>
        public void TestExam()
        {
            string why;
            if (!TestReady(out why))
            {
                return;
            }
            try
            {
                if (_study.Courses.Count == 0)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_study_test_no_course", null, 8f);
                    return;
                }
                Enrolment e = _study.Courses[0];
                if (e.Stage == (int)EnrolStage.Studying)
                {
                    e.Stage = (int)EnrolStage.Exam;
                    e.ExamFrom = Save.Day;
                    e.ExamTo = Save.Day + StudyCatalogue.ExamWindowDays;
                }
                Logger?.Info("Careers test: the exam of " + e.Def.Key + " marked now.");
                Examine(e, Save.Day);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>Twenty points more in four of the player's skills, one of the trades among them.</summary>
        public void TestSkills()
        {
            string why;
            if (!TestReady(out why))
            {
                return;
            }
            try
            {
                int day = Save.Day;
                int[] picks = { Tech.TalentCatalogue.Coding, Tech.TalentCatalogue.Accounting, Tech.TalentCatalogue.Law, Skills.Clinical };
                for (int i = 0; i < picks.Length; i++)
                {
                    StudyRules.Learn(_study, picks[i], 20000, day);
                }
                Notifications.Show(NotificationType.Success, "reallife_study_test_skills_done", null, 8f);
                Logger?.Info("Careers test: twenty points added to coding, accounting, law and clinical care.");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
