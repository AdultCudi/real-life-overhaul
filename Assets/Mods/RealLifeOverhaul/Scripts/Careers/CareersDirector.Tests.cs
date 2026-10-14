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

        // ---------------------------------------------------------------- the working life

        private bool WorkReady()
        {
            string why;
            if (!TestReady(out why))
            {
                return false;
            }
            if (!_workOn)
            {
                Notifications.Show(NotificationType.Warning, "reallife_work_test_off", null, 8f);
                return false;
            }
            return true;
        }

        /// <summary>An offer now for the first vacancy the player could hold (or the first of all), as if the interview had gone well.</summary>
        public void TestOffer()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                WorkFile w = _study.Work;
                Vacancies(Save.Day);
                Opening pick = null;
                for (int i = 0; i < w.Openings.Count && pick == null; i++)
                {
                    pick = ProRules.Qualified(_study, w.Openings[i].Def, w.Openings[i].Rank) ? w.Openings[i] : null;
                }
                if (pick == null && w.Openings.Count > 0)
                {
                    pick = w.Openings[0];
                }
                if (pick == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_work_test_none", null, 8f);
                    return;
                }
                var a = new Application { Day = Save.Day, Score = Math.Max(70, ProRules.HireScore(CandidateFor(pick))) };
                pick.CopyTo(a.Post);
                Offer(a, Save.Day);
                w.Applications.Add(a);
                Logger?.Info("Careers test: an offer from " + pick.Employer + " for " + pick.Def.Key + " rank " + pick.Rank + ".");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>The newest application still waiting for an answer turned down now, as the employer would, so the same post there is barred (ProRules.ReapplyDays).</summary>
        public void TestReject()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                WorkFile w = _study.Work;
                Application pick = null;
                for (int i = w.Applications.Count - 1; i >= 0 && pick == null; i--)
                {
                    int stage = w.Applications[i].Stage;
                    pick = stage == (int)ApplyStep.Sent || stage == (int)ApplyStep.Interview ? w.Applications[i] : null;
                }
                if (pick == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_work_test_no_application", null, 8f);
                    return;
                }
                pick.Reject(Save.Day);
                Tell2(NotificationType.Info, "reallife_work_note_rejected", Data("employer", CareersText.Employer(pick.Post.Employer), "title", CareersText.Title(pick.Post.Prof, pick.Post.Rank)),
                    TabApplications, CareerApp.ApplicationRow + pick.Post.Id);
                Logger?.Info("Careers test: the application to " + pick.Post.Employer + " for " + pick.Post.Def.Key + " rank " + pick.Post.Rank + " turned down on day " + Save.Day
                             + "; the same post there can be applied for again from day " + ProRules.ReapplyFrom(Save.Day) + ".");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>The rest of a working day now, here, whatever the hour and the place (a job is given for it if there is none).</summary>
        public void TestWorkDay()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                WorkFile w = _study.Work;
                if (w.Job == null)
                {
                    Vacancies(Save.Day);
                    if (w.Openings.Count == 0)
                    {
                        Notifications.Show(NotificationType.Warning, "reallife_work_test_none", null, 8f);
                        return;
                    }
                    var a = new Application { Day = Save.Day, Score = 70 };
                    w.Openings[0].CopyTo(a.Post);
                    a.Offer = a.Post.Pay;
                    Begin(a, Save.Day);
                }
                Job j = w.Job;
                Profession p = j.Def;
                j.TodayDay = Save.Day;
                j.SettledDay = Math.Min(j.SettledDay, Save.Day - 1);
                string busy = Offer(new Sitting(this, SittingKind.Work, 0, p.Hours * 60, CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank),
                    "employer", CareersText.Employer(j.Post.Employer)), true, CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay)))));
                if (busy != null)
                {
                    Notifications.Show(NotificationType.Warning, busy, null, 8f);
                }
                Logger?.Info("Careers test: a working day at " + j.Post.Employer + (busy == null ? " offered." : " not offered (" + busy + ")."));
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>The city's vacancies made again now.</summary>
        public void TestVacancies()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                WorkFile w = _study.Work;
                w.Openings.Clear();
                w.OpeningsDay = -1;
                _employersDay = -1;
                Vacancies(Save.Day);
                Notifications.Show(NotificationType.Success, "reallife_work_test_vacancies_done", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "count", w.Openings.Count.ToString() }, { "employers", _employers.Count.ToString() }
                }, 8f);
                Logger?.Info("Careers test: " + w.Openings.Count + " vacancies made from " + _employers.Count + " employers, " + _offices.Count + " office buildings.");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
        // ---------------------------------------------------------------- working life

        private Job LifeReady()
        {
            if (!WorkReady())
            {
                return null;
            }
            if (!Settings.CareersLife)
            {
                Notifications.Show(NotificationType.Warning, "reallife_life_test_off", null, 8f);
                return null;
            }
            if (_study.Work.Job == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_life_test_no_job", null, 8f);
                return null;
            }
            return _study.Work.Job;
        }

        /// <summary>A review of the job now, on the days worked so far.</summary>
        public void TestReview()
        {
            Job j = LifeReady();
            if (j == null)
            {
                return;
            }
            try
            {
                Logger?.Info("Careers test: a review at " + j.Post.Employer + " now.");
                Review(j, Save.Day);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>An office matter to answer now.</summary>
        public void TestMatter()
        {
            Job j = LifeReady();
            if (j == null)
            {
                return;
            }
            try
            {
                j.Politics = -1;
                j.PoliticsDay = Save.Day;
                Matters(j, Save.Day);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>A search firm's offer now, as if the last review had been a five.</summary>
        public void TestApproach()
        {
            Job j = LifeReady();
            if (j == null)
            {
                return;
            }
            try
            {
                Approach(j, Save.Day, true);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
        // ---------------------------------------------------------------- working for oneself

        /// <summary>A freelance contract offered now, in the profession the player works in or is best qualified for.</summary>
        public void TestContract()
        {
            if (!WorkReady())
            {
                return;
            }
            if (!OwnOn)
            {
                Notifications.Show(NotificationType.Warning, "reallife_own_test_off", null, 8f);
                return;
            }
            try
            {
                WorkFile w = _study.Work;
                int before = w.Contracts.Count;
                OfferContract(Save.Day, 1 + before, true);
                Contract c = w.Contracts.Count > before ? w.Contracts[w.Contracts.Count - 1] : null;
                if (c == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_own_test_none", null, 8f);
                    return;
                }
                Logger?.Info("Careers test: a contract from " + c.Client + " for " + c.Hours + " hours.");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>The player leaves their job now to found a company, with colleagues, whatever the rung: the notice is skipped.</summary>
        public void TestFound()
        {
            if (!WorkReady())
            {
                return;
            }
            if (!OwnOn || _study.Work.Job == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_own_test_no_job", null, 8f);
                return;
            }
            try
            {
                Job j = _study.Work.Job;
                j.NonCompete = true;
                _study.Work.ColleaguesCount = OwnRules.ColleaguesMost;
                _study.Work.ColleaguesProf = j.Post.Prof;
                _study.Work.ColleaguesLevel = 1;
                _study.Work.PracticeAfter = -1;
                EndJob(LeaveReason.SpunOut, Save.Day, true);
                Logger?.Info("Careers test: left to found a company, with colleagues, under a restraint clause.");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
        /// <summary>A degree passed now with a first (the first one under way, else law), and its ceremony set for today.</summary>
        public void TestGraduate()
        {
            string why;
            if (!TestReady(out why))
            {
                return;
            }
            if (!UniversityOpen)
            {
                Notifications.Show(NotificationType.Warning, "reallife_degrees_test_off", null, 8f);
                return;
            }
            try
            {
                Enrolment e = null;
                for (int i = 0; i < _study.Courses.Count && e == null; i++)
                {
                    e = _study.Courses[i].Def != null && _study.Courses[i].Def.Higher ? _study.Courses[i] : null;
                }
                if (e == null)
                {
                    e = new Enrolment { Course = StudyCatalogue.Law, Start = Save.Day, Instalments = 3 };
                    _study.Courses.Add(e);
                }
                e.Seen = e.Done = e.Def.Sessions;
                e.Homework = e.Def.Homework;
                StudyRules.Sit(e, 75, Save.Day);
                Results(e, Save.Day, true);
                _study.GraduationDay = Save.Day;
                Logger?.Info("Careers test: " + e.Def.Key + " passed with a first, its ceremony today at " + CeremonyHour + ":00.");
                Changed();
            }
            catch (Exception ex)
            {
                Fault("a test", ex);
            }
        }

        /// <summary>A tutoring session now, here, whatever the hour (the skill to tutor is given if there is too little).</summary>
        public void TestTutor()
        {
            string why;
            if (!TestReady(out why))
            {
                return;
            }
            if (!FamilyOn)
            {
                Notifications.Show(NotificationType.Warning, "reallife_family_test_off", null, 8f);
                return;
            }
            try
            {
                int skill = StudyRules.TutorSkill(_study);
                if (StudyRules.Points(_study, skill) < StudyCatalogue.TutorLeast)
                {
                    StudyRules.Learn(_study, Skills.Teaching, StudyCatalogue.TutorLeast * 1000, Save.Day);
                    skill = Skills.Teaching;
                }
                string busy = Offer(new Sitting(this, SittingKind.Tutoring, skill, StudyCatalogue.TutorMinutes, CareersText.T("reallife_family_skip_tutor"), true,
                    CareersText.T("reallife_family_skip_subject", "subject", CareersText.Skill(skill))));
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
