using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Family tab of the Campus app: the family's children with their age, schooling and college fund, and for a grown
    /// one their place at the university (paid by the fund and the player) and how they did; and tutoring on the Skills tab.
    /// </summary>
    public sealed partial class CampusApp
    {
        private const string ChildRow = "child:";

        private TableSpec WriteFamily(InfoBox box, StudyFile f, int day)
        {
            if (!CareersDirector.FamilyOn)
            {
                _picked = null;
                box.Title(CareersText.T("reallife_campus_family_title"));
                box.Line(CareersText.T("reallife_campus_family_off"));
                return CampusColumns.Family;
            }
            IReadOnlyList<ChildView> kids = _director.Kids();
            string pickedName = _picked != null && _picked.StartsWith(ChildRow, System.StringComparison.Ordinal) ? _picked.Substring(ChildRow.Length) : null;
            ChildView picked = default(ChildView);
            bool any = false;
            for (int i = 0; i < kids.Count; i++)
            {
                ChildView k = kids[i];
                ChildStudy c = _director.ChildOf(k.Name);
                TableRow row = Row(ChildRow + k.Name);
                row.Set(0, k.Name);
                row.Set(1, ((int)k.Age).ToString(), k.Age);
                row.Set(2, CareersText.T(k.School == 2 ? "reallife_campus_school_private" : k.School == 1 ? "reallife_campus_school_state" : "reallife_campus_school_none"));
                row.Set(3, CareersText.Money(k.Fund), k.Fund);
                row.Set(4, c != null ? CareersText.Course(c.Course) : "-");
                row.Set(5, CareersText.T(c == null ? (k.Grown ? "reallife_campus_child_grown" : "reallife_campus_child_home") : ChildStateKey(c)),
                    c != null && c.Stage == (int)ChildStep.Offered ? TableRow.Green : TableRow.Plain);
                if (k.Name == pickedName)
                {
                    picked = k;
                    any = true;
                }
            }
            if (any)
            {
                WriteChild(box, picked, _director.ChildOf(picked.Name));
                return CampusColumns.Family;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_family_title"));
            box.Line(CareersText.T("reallife_campus_family_about"));
            return CampusColumns.Family;
        }

        private static string ChildStateKey(ChildStudy c)
        {
            switch (c.At)
            {
                case ChildStep.Offered: return "reallife_campus_child_offered";
                case ChildStep.Studying: return "reallife_campus_child_studying";
                case ChildStep.Graduated: return "reallife_campus_child_graduated";
                default: return "reallife_campus_child_declined";
            }
        }

        private void WriteChild(InfoBox box, ChildView k, ChildStudy c)
        {
            box.Title(k.Name);
            box.Line(CareersText.Fill("reallife_campus_child_line", "age", ((int)k.Age).ToString(), "fund", CareersText.Money(k.Fund)));
            string name = k.Name;
            if (c == null)
            {
                box.Line(CareersText.T(k.Grown ? "reallife_campus_child_no_place" : "reallife_campus_child_later"));
            }
            else
            {
                CourseDef course = StudyCatalogue.Find(c.Course);
                switch (c.At)
                {
                    case ChildStep.Offered:
                        box.Line(CareersText.Fill("reallife_campus_child_place", "course", CareersText.Course(c.Course), "fee", CareersText.Money(Prices.At(course.Fee)),
                            "cost", CareersText.Money(Prices.At(StudyRules.ChildCost(course, k.Fund)))));
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_child_pay", () => Do(() => _director.PayChild(name)));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_campus_button_child_decline", () => Do(() => _director.DeclineChild(name)));
                        break;
                    case ChildStep.Studying:
                        box.Line(CareersText.T("reallife_campus_child_reading", "course", CareersText.Course(c.Course), "day", CareersText.Day(c.End)));
                        break;
                    case ChildStep.Graduated:
                        box.Line(CareersText.T("reallife_campus_child_done", "degree", CareersText.T("reallife_study_degree_with", "course", CareersText.Course(c.Course), "grade",
                            CareersText.Grade(course, c.Grade))));
                        break;
                    default:
                        box.Line(CareersText.T("reallife_campus_child_no"));
                        break;
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
        }

        private void TutorButton(InfoBox box)
        {
            if (!CareersDirector.FamilyOn)
            {
                return;
            }
            int skill;
            string why = _director.CanTutor(out skill);
            StudyFile f = _director.Study;
            int rate = f != null ? Prices.At(StudyRules.TutorRate(StudyRules.Points(f, skill))) : 0;
            box.Line(rate > 0
                ? CareersText.Fill("reallife_campus_tutor_line", "subject", CareersText.Skill(skill), "rate", CareersText.Rate(rate), "from", CareersText.Hour(StudyCatalogue.TutorFrom),
                    "to", CareersText.Hour(StudyCatalogue.TutorTo), "most", StudyCatalogue.TutorPerDay.ToString())
                : CareersText.T("reallife_campus_tutor_none", "least", StudyCatalogue.TutorLeast.ToString()));
            if (rate > 0)
            {
                if (why != null)
                {
                    box.Line(CareersText.T(why));
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_campus_button_tutor", () => Do(_director.Tutor));
            }
        }
    }
}
