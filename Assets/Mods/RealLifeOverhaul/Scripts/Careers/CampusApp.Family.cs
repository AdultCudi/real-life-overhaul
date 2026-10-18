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

        private TableSpec WriteFamily(InfoBox box)
        {
            if (!CareersDirector.FamilyOn)
            {
                _picked = null;
                box.Title(CareersText.T("reallife_campus_family_title"));
                box.Line(CareersText.T("reallife_campus_family_off"), CellTone.Yellow);
                Meta.Guide.Point(box, FamilyGuide);
                return CampusColumns.Family;
            }
            IReadOnlyList<ChildView> kids = _director.Kids();
            string pickedName = Picked(_picked, ChildRow);
            ChildView picked = default(ChildView);
            bool any = false;
            for (int i = 0; i < kids.Count; i++)
            {
                ChildView k = kids[i];
                ChildStudy c = _director.ChildOf(k.Name);
                TableRow row = Row(ChildRow + k.Name);
                row.Set(0, k.Name);
                row.Set(1, ((int)k.Age).ToString(), k.Age);
                row.Set(2, Schooling(k.School));
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
            box.Note(CareersText.T("reallife_campus_family_about"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ibox_children", kids.Count.ToString());
            Meta.Guide.Point(box, FamilyGuide);
            return CampusColumns.Family;
        }

        private static string Schooling(int school)
        {
            return CareersText.T(school == 2 ? "reallife_campus_school_private" : school == 1 ? "reallife_campus_school_state" : "reallife_campus_school_none");
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
            box.Chip(CareersText.T(c == null ? (k.Grown ? "reallife_campus_child_grown" : "reallife_campus_child_home") : ChildStateKey(c)),
                c != null && c.Stage == (int)ChildStep.Offered ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_campus_col_age", ((int)k.Age).ToString());
            box.Row("reallife_campus_col_schooling", Schooling(k.School));
            box.Row("reallife_campus_col_fund", CareersText.Money(k.Fund));
            string name = k.Name;
            box.Section("reallife_ibox_sec_university");
            if (c == null)
            {
                box.Note(CareersText.T(k.Grown ? "reallife_campus_child_no_place" : "reallife_campus_child_later"));
            }
            else
            {
                CourseDef course = StudyCatalogue.Find(c.Course);
                switch (c.At)
                {
                    case ChildStep.Offered:
                        box.Row("reallife_campus_col_course", CareersText.Course(c.Course));
                        box.Row("reallife_campus_col_fee", CareersText.Money(Prices.At(course.Fee)));
                        box.Row("reallife_ibox_you_pay", CareersText.Money(Prices.At(StudyRules.ChildCost(course, k.Fund))), CellTone.Yellow);
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_child_pay", () => Do(() => _director.PayChild(name)));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_campus_button_child_decline", () => Do(() => _director.DeclineChild(name)));
                        break;
                    case ChildStep.Studying:
                        box.Row("reallife_campus_col_course", CareersText.Course(c.Course));
                        box.Row("reallife_ibox_finishes", CareersText.Day(c.End));
                        break;
                    case ChildStep.Graduated:
                        box.Line(CareersText.T("reallife_campus_child_done", "degree", CareersText.T("reallife_study_degree_with", "course", CareersText.Course(c.Course), "grade",
                            CareersText.Grade(course, c.Grade))), CellTone.Green);
                        break;
                    default:
                        box.Note(CareersText.T("reallife_campus_child_no"));
                        break;
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, FamilyGuide);
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
            box.Section("reallife_ibox_sec_tutoring");
            if (rate <= 0)
            {
                box.Note(CareersText.T("reallife_campus_tutor_none", "least", StudyCatalogue.TutorLeast.ToString()));
            }
            else
            {
                box.Row("reallife_ibox_subject", CareersText.Skill(skill));
                box.Row("reallife_career_col_pay", CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(rate)));
                box.Row("reallife_career_col_hours", CareersText.T("reallife_career_row_hours", "from", CareersText.Hour(StudyCatalogue.TutorFrom), "to", CareersText.Hour(StudyCatalogue.TutorTo)));
                box.Row("reallife_ibox_sessions_day", StudyCatalogue.TutorPerDay.ToString());
                box.Row("reallife_career_col_where", CareersText.T("reallife_study_mode_distance"));
                if (why != null)
                {
                    box.Line(CareersText.T(why), CellTone.Yellow);
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_campus_button_tutor", () => Do(_director.Tutor));
            }
        }
    }
}
