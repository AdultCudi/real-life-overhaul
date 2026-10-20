using System;

namespace RealLifeOverhaul.Careers
{
    /// <summary>How a course's classes and a profession's working days fit on the days they share (<see cref="StudyRules.WorkFit"/>).</summary>
    public enum WorkFit
    {
        Fits = 0,         // no day shared, or a whole class and the working day each with room for the way between
        LeaveClass = 1,   // the class comes first and is left once it counts (its share sat) to be at work on time: say when
        LeaveWork = 2,    // the working day comes first and is left once it counts, in time to join the class
        Clash = 3         // even so one of the two is missed or late on those days
    }

    /// <summary>
    /// A course's timetable against a job's working days. A class counts once <see cref="StudyCatalogue.CountedShare"/>
    /// of it is sat and a working day once <see cref="ProRules.CountedShare"/> of it is worked, so a class from 10:00 to
    /// 14:00 and a shift from 14:00 fit when the class is left at 13:00; nothing in the mod said so, and the two looked
    /// like a clash every week (a player's report, 0.36.1). Game-free, nothing allocated; checked offline (EconomyCheck).
    /// </summary>
    public static partial class StudyRules
    {
        public const int WayMinutes = 45;   // game minutes allowed for the way between a class and work: across the city by car, or a few stops by subway

        /// <summary>
        /// How a course followed in a mode fits a profession's working days: the weekdays they share where it matters
        /// (<paramref name="days"/>, bit 0 Monday) and, for <see cref="WorkFit.LeaveClass"/> and <see cref="WorkFit.LeaveWork"/>,
        /// the minute of the day the first of the two counts from and may be left (<paramref name="leaveAt"/>). A course with no timetable (a certificate) always fits.
        /// </summary>
        public static WorkFit Fit(CourseDef c, StudyMode mode, Profession p, out int days, out int leaveAt)
        {
            days = 0;
            leaveAt = -1;
            if (c == null || p == null || !c.Timetabled || p.Hours <= 0)
            {
                return WorkFit.Fits;
            }
            int shared = Days(c, mode) & p.Days;
            if (shared == 0)
            {
                return WorkFit.Fits;
            }
            int classFrom = Hour(c, mode) * 60, classTo = classFrom + c.Hours * 60;
            int classCounts = classFrom + (c.Hours * 60 * StudyCatalogue.CountedShare + 999) / 1000;
            int workFrom = p.Hour * 60, workTo = workFrom + p.Hours * 60;
            int workOnTime = workFrom + ProRules.LateAfterMinutes;
            int workCounts = workFrom + (p.Hours * 60 * ProRules.CountedShare + 999) / 1000;
            WorkFit fit;
            if (classTo + WayMinutes <= workFrom || workTo + WayMinutes <= classFrom)
            {
                fit = WorkFit.Fits;   // the whole of each, and the way between
            }
            else if (classFrom < workFrom && classCounts + WayMinutes <= workOnTime)
            {
                fit = WorkFit.LeaveClass;   // the class first, left once it counts
                leaveAt = classCounts;
            }
            else if (workFrom <= classFrom && workCounts + WayMinutes <= classFrom + StudyCatalogue.LateMinutes)
            {
                fit = WorkFit.LeaveWork;    // the working day first, left once it counts, in time to join the class
                leaveAt = workCounts;
            }
            else
            {
                fit = WorkFit.Clash;
            }
            days = fit == WorkFit.Fits ? 0 : shared;
            return fit;
        }

        /// <summary>The worst fit of every course being followed (classes still to come) against a profession, with the days and the minute to leave of that worst one.</summary>
        public static WorkFit Fit(StudyFile f, Profession p, out int days, out int leaveAt, out int course)
        {
            days = 0;
            leaveAt = -1;
            course = -1;
            WorkFit worst = WorkFit.Fits;
            for (int i = 0; f != null && i < f.Courses.Count; i++)
            {
                Enrolment e = f.Courses[i];
                CourseDef c = e.Def;
                if (c == null || e.Stage != (int)EnrolStage.Studying || e.Seen >= c.Sessions)
                {
                    continue;
                }
                WorkFit fit = Fit(c, (StudyMode)e.Mode, p, out int d, out int at);
                if (fit > worst)
                {
                    worst = fit;
                    days = d;
                    leaveAt = at;
                    course = c.Id;
                }
            }
            return worst;
        }

        /// <summary>The minute of the day a class joined at <paramref name="joinedAt"/> for <paramref name="minutes"/> counts from: its share sat.</summary>
        public static int CountsFrom(int joinedAt, int minutes)
        {
            return joinedAt + (Math.Max(1, minutes) * StudyCatalogue.CountedShare + 999) / 1000;
        }
    }
}
