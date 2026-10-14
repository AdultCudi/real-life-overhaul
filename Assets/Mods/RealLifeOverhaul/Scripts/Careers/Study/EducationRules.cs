using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The groups the Persona app's education box lists the mod's courses in, in their order there.</summary>
    public enum EducationGroup
    {
        Degree = 0,        // degrees at Hudson City University
        Postgrad = 1,      // after a degree, at the university
        Nursing = 2,       // the nursing degree, on a hospital's ward
        Short = 3,         // short courses at Manhattan Business School
        Diploma = 4,       // diplomas in a trade or in care at the school
        Online = 5,        // live classes online, from home
        Certificate = 6    // professional certificates: study at home, an exam at the school
    }

    /// <summary>Where one of the mod's courses stands for the player, as the education box says it.</summary>
    public enum EducationState
    {
        NotStarted = 0,
        Studying = 1,
        FeeOwed = 2,
        Exam = 3,
        Retake = 4,
        Passed = 5,
        Failed = 6,
        Withdrawn = 7,
        Barred = 8
    }

    /// <summary>A course's standing with the figures its words are filled with.</summary>
    public struct EducationStatus
    {
        public EducationState State;
        public int Done, Of;    // classes attended of the timetable's (or homework done of the set pieces)
        public bool Homework;   // ...counted in homework (a certificate has no classes)
        public int NextDay;     // the day of the next class, 0 for none
        public int NextHour;    // ...and its hour
        public int Day;         // the last day of the exam's (or retake's) window
        public int Grade;       // a pass's grade
        public int Score;       // a fail's mark
    }

    /// <summary>One row of the education box after the game's own: a group's heading or a course.</summary>
    public struct EducationRow
    {
        public bool Heading;
        public EducationGroup Group;
        public int Course;      // the course (not for a heading)
        public int Count;       // a heading's courses listed under it
        public EducationStatus Status;
    }

    /// <summary>
    /// The Persona app's education box beyond the game's own courses (Careers/PersonaEducation shows it): every course of
    /// the mod's that is offered or that the player has taken, in the groups the mod's schools hold them in, each with its
    /// standing. Within a group the courses under way come first, then those passed, then those failed or left, then the
    /// rest, each in the catalogue's order; a group with nothing to list has no heading. Game-free: EconomyCheck runs it.
    /// </summary>
    public static class EducationRules
    {
        /// <summary>The groups in the order the box lists them.</summary>
        public static readonly EducationGroup[] Order =
        {
            EducationGroup.Degree, EducationGroup.Postgrad, EducationGroup.Nursing, EducationGroup.Short, EducationGroup.Diploma, EducationGroup.Online, EducationGroup.Certificate
        };

        /// <summary>The least white kept under the last row when the game's own rows leave less (or none).</summary>
        public const float LeastMargin = 30f;

        /// <summary>The room a heading's row keeps at its right for the count of its courses.</summary>
        public const float CountRoom = 220f;

        /// <summary>
        /// The education box's height for its rows. The game's box is a fixed height (a LayoutElement and the size it was
        /// drawn at) made for the game's own rows, and Persona's left column lays it out by that size, so rows added to it
        /// are drawn past its white and past the end of the column's scroll. The box is given the height its rows need:
        /// from its top to the rows' top, the rows, and under them the white the game's own rows leave in the game's box
        /// (at least <see cref="LeastMargin"/>). Never less than the game's own height.
        /// </summary>
        public static float BoxHeight(float baseHeight, float rowsTop, float gameRows, float allRows)
        {
            float margin = baseHeight - rowsTop - gameRows;
            if (margin < LeastMargin)
            {
                margin = LeastMargin;
            }
            return Math.Max(baseHeight, rowsTop + allRows + margin);
        }

        /// <summary>
        /// A heading's name width in a row <paramref name="rowWidth"/> wide: from where the name starts to the room kept
        /// for the count, which stands at the row's right where a course's status mark would be (a heading has none).
        /// </summary>
        public static float HeadingNameWidth(float rowWidth, float nameLeft, float rightMargin)
        {
            return Math.Max(0f, rowWidth - nameLeft - rightMargin - CountRoom);
        }

        /// <summary>
        /// Whether a heading's words stay on one line in a name <paramref name="width"/> wide, at the row's 28 points in
        /// bold: about 17 for a letter of the Latin alphabet and 30 for a Chinese character (a generous measure, so a
        /// heading that passes has room to spare).
        /// </summary>
        public static bool HeadingFits(string words, float width)
        {
            if (string.IsNullOrEmpty(words))
            {
                return true;
            }
            float wide = 0f;
            for (int i = 0; i < words.Length; i++)
            {
                wide += words[i] >= 0x2E80 ? 30f : 17f;
            }
            return wide <= width;
        }

        /// <summary>reallife_persona_edu_group_{key} for each group, in the enum's order.</summary>
        public static readonly string[] GroupKeys = { "degree", "postgrad", "nursing", "short", "diploma", "online", "certificate" };

        /// <summary>The group a course is listed in: where it is held first, then what kind it is.</summary>
        public static EducationGroup GroupOf(CourseDef c)
        {
            if (c.Kind == CourseKind.Certificate)
            {
                return EducationGroup.Certificate;
            }
            if (c.Campus == Campus.Hospital)
            {
                return EducationGroup.Nursing;
            }
            if (c.Campus == Campus.Online)
            {
                return EducationGroup.Online;
            }
            switch (c.Kind)
            {
                case CourseKind.Degree: return EducationGroup.Degree;
                case CourseKind.Postgrad: return EducationGroup.Postgrad;
                case CourseKind.Vocational: return EducationGroup.Diploma;
                default: return EducationGroup.Short;
            }
        }

        /// <summary>
        /// Where a course stands: the enrolment under way if there is one, else its best pass, else its latest result, else
        /// not started. The next class is the Campus app's (<see cref="StudyRules.NextClass(Enrolment, int, int, int)"/> at this
        /// minute of the day; <paramref name="sitting"/> the course whose class is being sat, -1 for none).
        /// </summary>
        public static EducationStatus Status(StudyFile f, CourseDef c, int today, int minuteOfDay, int sitting)
        {
            var s = new EducationStatus();
            Enrolment live = f != null ? f.Live(c.Id) : null;
            if (live != null)
            {
                s.Done = c.Timetabled ? live.Done : live.Homework;
                s.Of = c.Timetabled ? c.Sessions : c.Homework;
                s.Homework = !c.Timetabled;
                s.Day = live.ExamTo;
                switch ((EnrolStage)live.Stage)
                {
                    case EnrolStage.Exam:
                        s.State = EducationState.Exam;
                        break;
                    case EnrolStage.Retake:
                        s.State = live.FeeLate ? EducationState.FeeOwed : EducationState.Retake;
                        break;
                    default:
                        s.State = live.FeeLate ? EducationState.FeeOwed : EducationState.Studying;
                        int next = StudyRules.NextClass(live, today, minuteOfDay, sitting);
                        s.NextDay = next >= 0 ? next / (24 * 60) : 0;
                        s.NextHour = next >= 0 ? next % (24 * 60) / 60 : 0;
                        break;
                }
                return s;
            }
            int grade = f != null ? f.GradeOf(c.Id) : -1;
            if (grade >= 0)
            {
                s.State = EducationState.Passed;
                s.Grade = grade;
                return s;
            }
            for (int i = f != null ? f.Results.Count - 1 : -1; i >= 0; i--)
            {
                Enrolment e = f.Results[i];
                if (e.Course != c.Id)
                {
                    continue;
                }
                s.Score = e.Score;
                s.State = e.Stage == (int)EnrolStage.Withdrawn ? EducationState.Withdrawn : e.Stage == (int)EnrolStage.Barred ? EducationState.Barred : EducationState.Failed;
                return s;
            }
            s.State = EducationState.NotStarted;
            return s;
        }

        /// <summary>The order a standing comes in within its group: under way 0, passed 1, failed or left 2, not started 3.</summary>
        public static int Rank(EducationState state)
        {
            switch (state)
            {
                case EducationState.Studying:
                case EducationState.FeeOwed:
                case EducationState.Exam:
                case EducationState.Retake:
                    return 0;
                case EducationState.Passed:
                    return 1;
                case EducationState.NotStarted:
                    return 3;
                default:
                    return 2;
            }
        }

        /// <summary>True for a standing the player has begun (under way or finished).</summary>
        public static bool Started(EducationState state) => state != EducationState.NotStarted;

        /// <summary>
        /// The key of a standing's words. Not started is the game's own words for its courses ("Not started yet"); a
        /// course under way says its classes and, if one is set, the next.
        /// </summary>
        public static string StatusKey(EducationStatus s)
        {
            const string mod = "reallife";
            switch (s.State)
            {
                case EducationState.NotStarted: return "persona_education_not_started";
                case EducationState.Studying:
                    return s.Homework ? mod + "_persona_edu_homework" : s.NextDay > 0 ? mod + "_persona_edu_classes_next" : mod + "_persona_edu_classes";
                case EducationState.FeeOwed: return mod + "_persona_edu_fee";
                case EducationState.Exam: return mod + "_persona_edu_exam";
                case EducationState.Retake: return mod + "_persona_edu_retake";
                case EducationState.Passed: return mod + "_persona_edu_passed";
                case EducationState.Failed: return mod + "_persona_edu_failed";
                case EducationState.Withdrawn: return mod + "_study_stage_withdrawn";
                default: return mod + "_study_stage_barred";
            }
        }

        /// <summary>The key of a group's heading, and of its count of courses.</summary>
        public static string GroupKey(EducationGroup g) => "reallife" + "_persona_edu_group_" + GroupKeys[(int)g];

        public static string CountKey(int count) => count == 1 ? "reallife_persona_edu_count_one" : "reallife_persona_edu_count";

        /// <summary>
        /// The rows after the game's own: each group in <see cref="Order"/> with a heading and its courses. A course is
        /// listed if <paramref name="listed"/> says so (offered with the options as they are) or the player has begun it;
        /// with <paramref name="startedOnly"/> (a box that cannot scroll) only those begun. Returns the courses listed.
        /// </summary>
        public static int Rows(StudyFile f, Func<CourseDef, bool> listed, bool startedOnly, int today, int minuteOfDay, int sitting, List<EducationRow> into)
        {
            into.Clear();
            int courses = 0;
            var group = new List<EducationRow>(16);
            for (int g = 0; g < Order.Length; g++)
            {
                group.Clear();
                for (int i = 0; i < StudyCatalogue.All.Length; i++)
                {
                    CourseDef c = StudyCatalogue.All[i];
                    if (GroupOf(c) != Order[g])
                    {
                        continue;
                    }
                    EducationStatus s = Status(f, c, today, minuteOfDay, sitting);
                    bool begun = Started(s.State);
                    if ((startedOnly && !begun) || (!begun && (listed == null || !listed(c))))
                    {
                        continue;
                    }
                    var row = new EducationRow { Group = Order[g], Course = c.Id, Status = s };
                    int at = group.Count;
                    while (at > 0 && Rank(group[at - 1].Status.State) > Rank(s.State))
                    {
                        at--;   // (stable: a course goes after those of its own rank, so the catalogue's order holds within a rank)
                    }
                    group.Insert(at, row);
                }
                if (group.Count == 0)
                {
                    continue;
                }
                into.Add(new EducationRow { Heading = true, Group = Order[g], Course = -1, Count = group.Count });
                into.AddRange(group);
                courses += group.Count;
            }
            return courses;
        }
    }
}
