using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The Campus app's Studying tab: the courses under way, and one picked with its classes, homework, fees and exam.</summary>
    public sealed partial class CampusApp
    {
        private TableSpec WriteStudying(InfoBox box, StudyFile f, int day)
        {
            Enrolment picked = null;
            int id = PickedId(_picked, CourseRow);
            int minute = CareersGame.MinuteOfDay, sitting = _director.SittingClass;
            for (int i = 0; i < f.Courses.Count; i++)
            {
                Enrolment e = f.Courses[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                StudyMode mode = (StudyMode)e.Mode;
                TableRow row = Row(CourseRow + c.Id);
                row.Set(0, CareersText.Course(c.Id));
                row.Set(1, CareersText.Mode(mode));
                int next = StudyRules.NextClass(e, day, minute, sitting);
                row.Set(2, next >= 0 ? CareersText.T("reallife_campus_row_next", "day", CareersText.Day(next / (24 * 60)), "hour", CareersText.Hour(next % (24 * 60) / 60)) : "-", next);
                row.Set(3, c.Timetabled ? CareersText.T("reallife_campus_row_of", "done", e.Done.ToString(), "of", StudyRules.Counted(e).ToString()) : "-", StudyRules.Attendance(e),
                    StudyRules.Attendance(e) < StudyCatalogue.Barred + 150 && e.Seen > 2 ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, CareersText.T("reallife_campus_row_of", "done", e.Homework.ToString(), "of", c.Homework.ToString()), e.Homework);
                row.Set(5, CareersText.Money(e.Fee), e.Fee);
                row.Set(6, e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake
                    ? CareersText.T("reallife_campus_row_exam", "from", CareersText.Day(e.ExamFrom), "to", CareersText.Day(e.ExamTo)) : "-", e.ExamFrom);
                int minutes;
                bool now = e.Stage == (int)EnrolStage.Studying && e.SettledDay < day && StudyRules.Joinable(c, mode, day, minute, out minutes);
                row.Set(7, e.FeeLate ? CareersText.T("reallife_campus_state_fee") : now ? CareersText.T("reallife_campus_state_class_now") : CareersText.Stage(e.At),
                    e.FeeLate ? TableRow.Red : now || StudyRules.ExamDay(e, day) ? TableRow.Green : TableRow.Plain);
                picked = c.Id == id ? e : picked;
            }
            if (picked != null)
            {
                WriteEnrolment(box, picked, day);
                return CampusColumns.Studying;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_studying_title"));
            if (f.GraduationCourse >= 0 && f.GraduationDay >= day)
            {
                box.Line(CareersText.Fill("reallife_campus_ceremony", "course", CareersText.Course(f.GraduationCourse), "day", CareersText.Day(f.GraduationDay),
                    "hour", CareersText.Hour(CareersDirector.CeremonyHour), "place", _director.PlaceOf(StudyCatalogue.Find(f.GraduationCourse), StudyMode.FullTime)), CellTone.Green);
                if (f.GraduationDay == day)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_campus_button_graduate", () => Do(_director.Graduate));
                }
            }
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_campus_sum_week", CareersText.Minutes(f.WeekOf == (day - 1) / 7 ? f.WeekMinutes : 0));
            if (f.LoanCents > 0)
            {
                box.Row("reallife_campus_sum_loan", CareersText.Cents(f.LoanCents), CellTone.Yellow);
            }
            string university = _director.UniversityLine();
            if (university != null)
            {
                box.Line(university);
            }
            box.Section("reallife_ibox_sec_how");
            box.Note(CareersText.T("reallife_campus_studying_about", "early", StudyCatalogue.ArriveEarlyMinutes.ToString(), "late", StudyCatalogue.LateMinutes.ToString()));
            Meta.Guide.Point(box, StudyGuide);
            return CampusColumns.Studying;
        }

        private void WriteEnrolment(InfoBox box, Enrolment e, int day)
        {
            CourseDef c = e.Def;
            StudyMode mode = (StudyMode)e.Mode;
            int id = c.Id;
            int minutes;
            string attend = _director.CanAttend(e, out minutes), homework = _director.CanHomework(e), sit = _director.CanSit(e);
            bool canAttend = attend == null || attend != "reallife_study_why_no_class", canHomework = homework == null || homework != "reallife_study_why_no_homework",
                canSit = sit == null || sit != "reallife_study_why_no_exam";
            box.Title(CareersText.Course(c.Id));
            box.Subtitle(CareersText.Fill("reallife_ibox_course_sub", "kind", CareersText.Mode(mode), "place", _director.PlaceOf(c, mode)));
            box.Chip(e.FeeLate ? CareersText.T("reallife_campus_state_fee") : CareersText.Stage(e.At),
                e.FeeLate ? CellTone.Red : StudyRules.ExamDay(e, day) ? CellTone.Green : e.Stage == (int)EnrolStage.Retake ? CellTone.Yellow : CellTone.Plain);
            if (c.Timetabled)
            {
                // How far through its classes the course is: those held so far (StudyRules.Held, as the attendance meter counts them).
                box.Chip(CareersText.Fill("reallife_ibox_chip_classes", "held", StudyRules.Held(e, day, CareersGame.MinuteOfDay, _director.SittingClass).ToString(), "of", c.Sessions.ToString()));
            }
            if (e.FeeLate)
            {
                box.Line(CareersText.T("reallife_campus_on_owed", "amount", CareersText.Money(_director.Owed(e))), CellTone.Red);
            }
            if (canAttend && attend != null)
            {
                box.Line(CareersText.T(attend), CellTone.Yellow);
            }
            if (canSit && sit != null)
            {
                box.Line(CareersText.T(sit), CellTone.Yellow);
            }
            if (e.Excused > 0 && e.ExcusedDay >= 0)
            {
                // Classes lost to the provider's outage (StudyRules.Excuse): left out of the attendance below, and said why.
                box.Line(CareersText.Fill(e.Excused == 1 ? "reallife_campus_excused_one" : "reallife_campus_excused", "count", e.Excused.ToString(), "day", CareersText.Day(e.ExcusedDay)));
            }
            string room = _director.RoomLine(e);
            if (room != null)
            {
                box.Line(room);
            }
            int minute = CareersGame.MinuteOfDay, sitting = _director.SittingClass;
            int next = StudyRules.NextClass(e, day, minute, sitting);
            box.Section("reallife_campus_col_classes");
            box.Row("reallife_ibox_since", CareersText.Day(e.Start));
            if (next >= 0 && attend != null)
            {
                box.Row("reallife_campus_col_next", CareersText.T("reallife_campus_row_next", "day", CareersText.Day(next / (24 * 60)), "hour", CareersText.Hour(next % (24 * 60) / 60)),
                    CellTone.Green);
            }
            if (c.Timetabled)
            {
                int held = StudyRules.Held(e, day, minute, sitting), share = StudyRules.Attendance(e), counted = Mathf.Max(0, held - e.Excused);   // an excused class counts neither way
                box.Meter("reallife_campus_col_attended", LookRules.Fill(e.Done, counted),
                    CareersText.Fill("reallife_ibox_attended", "done", e.Done.ToString(), "seen", counted.ToString(), "share", CareersText.Mille(share)),
                    share < StudyCatalogue.Barred ? CellTone.Red : share < StudyCatalogue.Barred + 150 && e.Seen > 2 ? CellTone.Yellow : CellTone.Green);
                box.Row("reallife_ibox_in_all", c.Sessions.ToString());
                box.Row("reallife_campus_col_timetable", CareersText.Timetable(StudyRules.Days(c, mode), StudyRules.Hour(c, mode)));
            }
            box.Section("reallife_campus_col_homework");
            int set = StudyRules.HomeworkSet(e);
            box.Meter("reallife_ibox_done", LookRules.Fill(e.Homework, c.Homework),
                CareersText.Fill("reallife_ibox_homework", "done", e.Homework.ToString(), "set", set.ToString(), "of", c.Homework.ToString()),
                e.Homework < set ? CellTone.Yellow : CellTone.Green);
            box.Section("reallife_ibox_sec_money");
            box.RowPair("reallife_campus_col_paid", CareersText.Money(e.Fee), "reallife_ibox_student_loan", CareersText.Money(e.Loan));
            box.Row("reallife_ibox_instalments", CareersText.T("reallife_ibox_of", "value", e.Instalments.ToString(), "most", StudyRules.Instalments(c).ToString()));
            if (e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake)
            {
                box.Section("reallife_campus_col_exam");
                box.Line(CareersText.Fill(e.Stage == (int)EnrolStage.Retake ? "reallife_campus_on_retake" : "reallife_campus_on_exam",
                    "from", CareersText.Day(e.ExamFrom), "to", CareersText.Day(e.ExamTo), "opens", CareersText.Hour(StudyCatalogue.ExamOpens),
                    "closes", CareersText.Hour(StudyCatalogue.ExamCloses), "fee", CareersText.Money(Prices.At(StudyRules.RetakeFee(c))), "score", e.Score.ToString()),
                    e.Stage == (int)EnrolStage.Retake ? CellTone.Yellow : CellTone.Plain);
            }
            Transform buttons = box.ButtonRow();
            if (e.FeeLate)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_pay", () => Do(() => _director.PayOwed(id)));
            }
            if (canAttend)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_attend", () => Do(() => _director.Attend(id)));
            }
            if (canHomework)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_campus_button_homework", () => Do(() => _director.Homework(id)));
            }
            if (canSit)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_sit", () => Do(() => _director.Sit(id)));
            }
            Transform more = box.ButtonRow();
            string name = CareersText.Course(id);
            InfoBox.Button(more, InfoBox.Negative, "reallife_campus_button_withdraw", () => GameConfirm.Show(
                "reallife_campus_confirm_withdraw_title".Localize(new { name }), "reallife_campus_confirm_withdraw_body".Localize(),
                () => UiClone.Safe(() =>
                {
                    _picked = null;
                    _director.Withdraw(id);
                }), null, null, null, true, true));
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, StudyGuide);
        }
    }
}
