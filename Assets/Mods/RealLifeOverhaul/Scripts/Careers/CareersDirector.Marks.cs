using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One line of what an exam's mark is made of, as both interfaces write it.</summary>
    public struct MarkLine
    {
        public string Label, Figure;
        public float Share;      // how much of its part is earned, 0 to 1; -1 for a line that is not a share (the tutor's edge, the luck)
        public bool Warn;        // a line the player can still act on (a part not full yet, the tiredness)
    }

    /// <summary>
    /// What the forecast of an exam's mark is made of, written once for the old Campus app and the kit: the same sum the sitting is marked by
    /// (<see cref="MarkOf"/>), sat rested. Read when a page is written, never per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        /// <summary>The words that say the forecast is a forecast and what moves it.</summary>
        internal static string MarkAbout() => CareersText.T("reallife_study_mark_about");

        /// <summary>The forecast's lines, the mark first; empty for a course that is not under way. A short course's line on its last class is the last of them.</summary>
        internal void MarkLines(Enrolment e, List<MarkLine> into)
        {
            into.Clear();
            CourseDef c = e != null ? e.Def : null;
            if (c == null || !e.Live)
            {
                return;
            }
            ExamMark m = MarkOf(e, false);
            into.Add(new MarkLine { Label = CareersText.T("reallife_study_mark_forecast"), Share = m.Total / 100f,
                Figure = CareersText.T("reallife_study_mark_value", "mark", m.Total.ToString(), "grade", CareersText.Grade(c, m.Grade)) });
            into.Add(Part("reallife_study_mark_turning", m.Turning, StudyRules.MarkTurning, false));
            into.Add(Part("reallife_study_mark_attendance", m.Attendance, StudyRules.MarkAttendance, c.Timetabled && m.Attendance < StudyRules.MarkAttendance));
            into.Add(Part("reallife_study_mark_homework", m.Homework, StudyRules.MarkHomework, m.Homework < StudyRules.MarkHomework));
            into.Add(Part("reallife_study_mark_skill", m.Skill, StudyRules.MarkSkill, false));
            into.Add(Part("reallife_study_mark_revision", m.Revision, StudyRules.MarkRevisionMost, false));
            into.Add(new MarkLine { Label = CareersText.T("reallife_study_mark_tutor"), Figure = Signed(m.Tutor), Share = -1f });
            into.Add(new MarkLine { Label = CareersText.T("reallife_study_mark_luck"), Figure = Signed(m.Luck), Share = -1f });
            bool tired = StudyRules.SatTired(CareersGame.Energy);
            into.Add(new MarkLine { Label = CareersText.T("reallife_study_mark_tired"), Share = -1f, Warn = tired,
                Figure = CareersText.Fill("reallife_study_mark_tired_value", "mark", StudyRules.MarkTired.ToString(), "need", StudyCatalogue.ExamTired.ToString(),
                    "energy", Mathf.RoundToInt(CareersGame.Energy).ToString()) });
            if (StudyRules.MarkedAtLastClass(c))
            {
                into.Add(new MarkLine { Label = CareersText.T("reallife_study_mark_short"), Share = -1f, Warn = StudyRules.HomeworkWaiting(e) > 0 });
            }
        }

        private static MarkLine Part(string key, int got, int of, bool warn)
        {
            return new MarkLine
            {
                Label = CareersText.T(key), Figure = CareersText.T("reallife_ibox_of", "value", got.ToString(), "most", of.ToString()), Share = of > 0 ? got / (float)of : 0f, Warn = warn
            };
        }

        private static string Signed(int v) => v > 0 ? "+" + v.ToString() : v.ToString();

        /// <summary>What a course's page promises of its homework: how many pieces and after which classes each is set; null for a course with none or no classes.</summary>
        internal static string HomeworkPlan(CourseDef c)
        {
            if (c == null || !c.Timetabled || c.Homework <= 0)
            {
                return null;
            }
            var list = new System.Text.StringBuilder(24);
            for (int k = 1; k <= c.Homework; k++)
            {
                list.Append(k > 1 ? ", " : string.Empty).Append(StudyRules.HomeworkSetAfter(c, k));
            }
            return CareersText.T(c.Homework == 1 ? "reallife_campus_hw_plan_one" : "reallife_campus_hw_plan", "n", c.Homework.ToString(), "list", list.ToString());
        }

        /// <summary>The homework's lines as both pages write them: pieces open now, and when the next is set (or that every piece is); null for a course that sets none.</summary>
        internal static void HomeworkLines(Enrolment e, out string open, out string next)
        {
            CourseDef c = e.Def;
            open = StudyRules.HomeworkWaiting(e).ToString();
            if (c.Homework <= 0)
            {
                next = null;
                return;
            }
            int after = StudyRules.HomeworkNext(e);
            next = after > 0 ? CareersText.T("reallife_campus_hw_next_at", "n", after.ToString(), "done", e.Done.ToString())
                : CareersText.T("reallife_campus_hw_all_set", "of", c.Homework.ToString());
        }
    }
}
