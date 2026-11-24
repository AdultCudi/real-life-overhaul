using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What an exam's mark is made of, part by part, whole points, so that a page can show every line and its sum is the mark.</summary>
    public struct ExamMark
    {
        public int Turning;        // for turning up (StudyRules.MarkTurning)
        public int Attendance;     // of StudyRules.MarkAttendance, by the share of the counted classes attended
        public int Homework;       // of StudyRules.MarkHomework, by the pieces done of the course's
        public int Skill;          // of StudyRules.MarkSkill, by how far the course's first skill has come
        public int Revision;       // two a piece done beyond the course's, of StudyRules.MarkRevisionMost
        public int Tired;          // 0, or minus StudyRules.MarkTired
        public int Tutor;          // the tutor's edge, a point or two either way
        public int Luck;           // the sitting's luck, drawn once
        public int Total;          // the mark: the parts added, 0 to 100
        public int Grade;          // what it earns (StudyRules.Grade)
    }

    /// <summary>The exam's mark, and the same sum as a forecast: one function for both, so a forecast is the mark the sitting would give.</summary>
    public static partial class StudyRules
    {
        public const int MarkTurning = 20, MarkAttendance = 40, MarkHomework = 25, MarkSkill = 15, MarkTired = 5;   // the parts of a mark out of 100
        public const int MarkRevision = 2;                                                                         // points a piece of revision adds
        public const int MarkRevisionMost = StudyCatalogue.RevisionMost * MarkRevision;                            // ...for four pieces

        private static int Whole(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

        /// <summary>
        /// The mark out of 100 an exam gives for the enrolment as it stands: 20 for turning up, 40 by attendance, 25 by homework, 15 by skill, 2 a piece of
        /// revision, 5 off for sitting it tired, the tutor's edge and the sitting's luck. The exam's sitting and the page's forecast both call this,
        /// the forecast with <paramref name="tired"/> false (rested) and the luck and edge the sitting will have (both fixed beforehand).
        /// </summary>
        public static ExamMark Mark(Enrolment e, int skillPoints, bool tired, int luck, int edge)
        {
            var m = new ExamMark();
            CourseDef c = e.Def;
            if (c == null)
            {
                return m;
            }
            m.Turning = MarkTurning;
            m.Attendance = Whole(MarkAttendance * (Attendance(e) / 1000.0));
            m.Homework = c.Homework <= 0 ? MarkHomework : Whole(MarkHomework * Math.Min(1.0, e.Homework / (double)c.Homework));
            int target = Math.Max(10, Math.Min(100, 20 + c.GainA));
            m.Skill = Whole(MarkSkill * Math.Min(1.0, skillPoints / (double)target));
            m.Revision = Math.Max(0, Math.Min(StudyCatalogue.RevisionMost, e.Homework - c.Homework)) * MarkRevision;
            m.Tired = tired ? -MarkTired : 0;
            m.Tutor = edge;
            m.Luck = TalentCatalogue.Clamp(luck, -StudyCatalogue.ExamNoise, StudyCatalogue.ExamNoise);
            m.Total = TalentCatalogue.Clamp(m.Turning + m.Attendance + m.Homework + m.Skill + m.Revision + m.Tired + m.Tutor + m.Luck, 0, 100);
            m.Grade = Grade(c, m.Total);
            return m;
        }

        /// <summary>The exam's mark out of 100 without a tutor's edge: <see cref="Mark"/>'s total.</summary>
        public static int ExamScore(Enrolment e, int skillPoints, bool tired, int luck)
        {
            return Mark(e, skillPoints, tired, luck, 0).Total;
        }

        /// <summary>True if a student whose energy is <paramref name="energy"/> as an exam begins sits it tired (five points off).</summary>
        public static bool SatTired(float energy) => energy < StudyCatalogue.ExamTired;
    }
}
