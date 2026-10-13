using System;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The university's part: degrees and what follows them at Hudson City University (Careers/CampusBuilding) and on the
    /// wards of the city's hospitals, the professional certificates studied at home, the student loan (on the credit file,
    /// through Wiring/CareersLinks), and graduation day: a ceremony at the university on the Saturday after a degree is
    /// passed, in the Planner, with a story in the City Ledger and a post to make on Flare.
    /// </summary>
    public sealed partial class CareersDirector
    {
        public const int CeremonyHour = 11, CeremonyMinutes = 120;

        private CampusBuilding _campus;
        private int _graduatedToday = -1;
        private string _graduatedName;

        /// <summary>True while the university's part is on: degrees, certificates and the student loan.</summary>
        public static bool UniversityOpen => Settings.CareersStudy && Settings.CareersDegrees;

        /// <summary>The university's building, once found (null while the part is off).</summary>
        public CampusBuilding University => UniversityOpen ? _campus : null;

        /// <summary>Once a second: the university's building is looked for, and dropped with its part.</summary>
        private void CampusTick()
        {
            if (UniversityOpen && _studyOn)
            {
                if (_campus == null)
                {
                    _campus = new CampusBuilding(Logger);
                }
                _campus.Tick();
            }
            else if (_campus != null)
            {
                _campus.Dispose();
                _campus = null;
            }
        }

        private void CampusGone()
        {
            _campus?.Dispose();
            _campus = null;
        }

        private bool AtUniversity()
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            return _campus != null && player != null && _campus.At(player.transform.position);
        }

        /// <summary>A campus in words: its name, and for the university its address.</summary>
        private string CampusWhere(Campus campus)
        {
            if (campus == Campus.University && _campus != null && _campus.Usable)
            {
                return CareersText.T("reallife_study_campus_at", "campus", CareersText.Campus(campus), "address", _campus.Name);
            }
            return CareersText.Campus(campus);
        }

        // ---------------------------------------------------------------- graduation

        /// <summary>A degree or a master's passed: the graduation ceremony is set for the Saturday after.</summary>
        partial void PassedMore(Enrolment e, int day)
        {
            CourseDef c = e.Def;
            if (c == null || !c.Higher || !UniversityOpen)
            {
                return;
            }
            int on = StudyRules.CeremonyDay(day);
            _study.GraduationDay = on;
            _study.GraduationCourse = c.Id;
            CareersBridges.Standing?.Invoke(1, StudyRules.Honours(c, e.Grade) ? 2 : 1);
            Tell(NotificationType.Success, "reallife_study_note_graduation", Data("course", CareersText.Course(c.Id), "day", CareersText.Day(on), "hour", CareersText.Hour(CeremonyHour)),
                TabResults, null);
        }

        /// <summary>The day after a ceremony that was not attended: the degree is the player's all the same, and the paper prints it.</summary>
        private void GraduationDay(int day)
        {
            if (_study.GraduationCourse < 0 || day <= _study.GraduationDay)
            {
                return;
            }
            Enrolment e = Best(_study.GraduationCourse);
            if (e != null && !e.Graduated)
            {
                e.Graduated = true;
                Story("reallife_study_news_graduated", "name", PlayerName(), "degree", Degree(e));
            }
            _study.GraduationCourse = -1;
            _study.GraduationDay = 0;
        }

        private Enrolment Best(int course)
        {
            Enrolment best = null;
            for (int i = 0; i < _study.Results.Count; i++)
            {
                Enrolment e = _study.Results[i];
                if (e.Course == course && e.Stage == (int)EnrolStage.Passed && (best == null || e.Grade > best.Grade))
                {
                    best = e;
                }
            }
            return best;
        }

        /// <summary>A degree in words with its class: "LLB Law, First".</summary>
        private static string Degree(Enrolment e)
        {
            return CareersText.T("reallife_study_degree_with", "course", CareersText.Course(e.Course), "grade", CareersText.Grade(e.Def, e.Grade));
        }

        public string CanGraduate()
        {
            if (!_studyOn || _study.GraduationCourse < 0)
            {
                return "reallife_study_why_no_ceremony";
            }
            int day = Save.Day, minute = CareersGame.MinuteOfDay;
            if (day != _study.GraduationDay || minute < CeremonyHour * 60 - 30 || minute > CeremonyHour * 60 + 60)
            {
                return "reallife_study_why_ceremony_hour";
            }
            return AtUniversity() ? null : "reallife_study_why_not_university";
        }

        public string Graduate()
        {
            string why = CanGraduate();
            if (why != null)
            {
                return why;
            }
            return Offer(new Sitting(this, SittingKind.Ceremony, _study.GraduationCourse, CeremonyMinutes, CareersText.T("reallife_study_skip_ceremony"), false,
                CareersText.Course(_study.GraduationCourse)));
        }

        partial void OtherEndedMore(Sitting sitting, int minutes, bool whole)
        {
            if (sitting.Kind != SittingKind.Ceremony || !_studyOn || !whole)
            {
                return;
            }
            Enrolment e = Best(sitting.Of);
            if (e == null)
            {
                return;
            }
            e.Graduated = true;
            _study.GraduationCourse = -1;
            _study.GraduationDay = 0;
            _graduatedToday = Save.Day;
            _graduatedName = Degree(e);
            CareersGame.Mood(ModState.MoodUpBig);
            Story("reallife_study_news_graduated", "name", PlayerName(), "degree", _graduatedName);
            Tell(NotificationType.Success, "reallife_study_note_graduated", Data("degree", _graduatedName), TabResults, null);
        }

        /// <summary>For Flare: the degree the player graduated with today at the ceremony, or null.</summary>
        public string GraduatedToday()
        {
            return Save != null && _graduatedToday == Save.Day ? _graduatedName : null;
        }

        // ---------------------------------------------------------------- the student loan on the credit file

        partial void Borrowed(int whole)
        {
            CareersBridges.Credit?.Invoke(0, whole);
        }

        partial void LoanCleared()
        {
            CareersBridges.Credit?.Invoke(1, 0);
            Tell(NotificationType.Success, "reallife_study_note_loan_cleared", null, TabStudying, null);
        }
    }
}
