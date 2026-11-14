using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Where a course's exam is sat: the school's study desks, the university's classroom, a ward.</summary>
    public enum ExamPlace
    {
        Desk = 0,    // a study desk inside Manhattan Business School: certificates, online and distance courses, and the school's own
        Room = 1,    // the course's classroom at Hudson City University
        Ward = 2     // a ward inside a hospital or clinic
    }

    /// <summary>What a course asks of the player before enrolling.</summary>
    public enum AskKind
    {
        Course = 0,   // one of the courses passed first (or any degree)
        Skill = 1,    // a skill at its floor
        Cash = 2,     // the fee to pay now
        Seats = 3,    // a free seat in the class
        Slots = 4,    // a place among the courses studied at once
        Loan = 5      // room on the student loan for the fee to pay now
    }

    /// <summary>One requirement of a course with the player's own level beside it.</summary>
    public struct Ask
    {
        public AskKind Kind;
        public int Id;        // the skill, for a skill
        public int Need;
        public int Have;
        public bool Met;
    }

    /// <summary>
    /// What a course asks before enrolling and where its exam is sat, in plain numbers (game-free: EconomyCheck runs it). The pages
    /// and the refusal read the same list, so a page shows what the refusal names and the refusal names what the page shows.
    /// </summary>
    public static class StudyNeedRules
    {
        /// <summary>Where the exam of a course is sat in a mode; the same rule as <see cref="StudyRules.AtExam"/>.</summary>
        public static ExamPlace ExamPlaceOf(CourseDef c, StudyMode mode)
        {
            if (c == null || mode == StudyMode.Distance || c.Campus == Campus.Online || c.Campus == Campus.School)
            {
                return ExamPlace.Desk;
            }
            return c.Campus == Campus.Hospital ? ExamPlace.Ward : ExamPlace.Room;
        }

        /// <summary>Days the exam stays open once it opens: a week, a certificate's fortnight.</summary>
        public static int ExamOpenDays(CourseDef c)
        {
            return c == null ? 0 : StudyCatalogue.ExamWindowDays * (c.Kind == CourseKind.Certificate ? 2 : 1);
        }

        /// <summary>True for a short course: it is marked in its last class, and sits an exam only after missing it.</summary>
        public static bool MarkedInClass(CourseDef c) => c != null && c.Kind == CourseKind.Short;

        /// <summary>True while an exam of the enrolment is open: its stage and its days (the hour and the weekday are asked of <see cref="StudyRules.ExamDay"/>).</summary>
        public static bool ExamOpen(Enrolment e, int day)
        {
            return e != null && (e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake) && day <= e.ExamTo;
        }

        /// <summary>The next weekday of the exam's window from a day, or -1 when none is left.</summary>
        public static int ExamNextDay(Enrolment e, int from)
        {
            if (e == null)
            {
                return -1;
            }
            for (int d = Math.Max(from, e.ExamFrom); d <= e.ExamTo; d++)
            {
                if (StudyRules.ExamDay(e, d))
                {
                    return d;
                }
            }
            return -1;
        }

        /// <summary>
        /// Every requirement of enrolling in a course, met or not, each with the player's own level: the course passed first, the skill floor,
        /// a seat, a place among the courses at once (said only when none is free), and the fee to pay now or the room on the student loan.
        /// <paramref name="seatsLeft"/> is -1 for a course without a class list.
        /// </summary>
        public static void Asks(StudyFile f, CourseDef c, bool loan, int cashWhole, int dueWhole, int loanRoomWhole, int seatsLeft, List<Ask> into)
        {
            into.Clear();
            if (c.Needs.Length > 0 || c.NeedsDegree)
            {
                int passed = 0;
                for (int i = 0; i < c.Needs.Length; i++)
                {
                    passed += f.Holds(c.Needs[i]) ? 1 : 0;
                }
                passed += c.NeedsDegree && StudyRules.HoldsAny(f, CourseKind.Degree) ? 1 : 0;
                into.Add(new Ask { Kind = AskKind.Course, Need = 1, Have = passed, Met = passed > 0 });
            }
            if (RosterRules.Floor(c, out int skill, out int points))
            {
                int have = StudyRules.Points(f, skill);
                into.Add(new Ask { Kind = AskKind.Skill, Id = skill, Need = points, Have = have, Met = have >= points });
            }
            if (seatsLeft >= 0)
            {
                into.Add(new Ask { Kind = AskKind.Seats, Need = 1, Have = seatsLeft, Met = seatsLeft > 0 });
            }
            if (f.Courses.Count >= StudyRules.LiveMost)
            {
                into.Add(new Ask { Kind = AskKind.Slots, Need = StudyRules.LiveMost - 1, Have = f.Courses.Count, Met = false });
            }
            if (loan)
            {
                into.Add(new Ask { Kind = AskKind.Loan, Need = dueWhole, Have = loanRoomWhole, Met = dueWhole <= loanRoomWhole });
            }
            else
            {
                into.Add(new Ask { Kind = AskKind.Cash, Need = dueWhole, Have = cashWhole, Met = cashWhole >= dueWhole });
            }
        }

        /// <summary>The sentences of a refusal joined with a space, as many as a notice has room for (<paramref name="most"/> characters): the first is always kept, a later one only whole.</summary>
        public static string Fit(IList<string> sentences, int most)
        {
            var b = new System.Text.StringBuilder(most);
            for (int i = 0; i < sentences.Count; i++)
            {
                string s = sentences[i];
                if (string.IsNullOrEmpty(s))
                {
                    continue;
                }
                if (b.Length > 0 && b.Length + 1 + s.Length > most)
                {
                    continue;   // a shorter one after it may still fit
                }
                b.Append(b.Length > 0 ? " " : string.Empty).Append(s);
            }
            return b.ToString();
        }

        /// <summary>How many of the asks are not met.</summary>
        public static int Short(List<Ask> asks)
        {
            int n = 0;
            for (int i = 0; i < asks.Count; i++)
            {
                n += asks[i].Met ? 0 : 1;
            }
            return n;
        }

        /// <summary>The courses that raise a skill, the biggest gain first, not the one named (<paramref name="except"/>, or -1); <paramref name="offered"/> leaves out those not on offer.</summary>
        public static int RaisingCourses(int skill, int except, Func<CourseDef, bool> offered, List<int> into)
        {
            into.Clear();
            var gains = new List<int>(4);
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                int gain = c.SkillA == skill ? c.GainA : c.SkillB == skill ? c.GainB : 0;
                if (gain <= 0 || c.Id == except || (offered != null && !offered(c)))
                {
                    continue;
                }
                int at = 0;
                while (at < gains.Count && gains[at] >= gain)
                {
                    at++;
                }
                gains.Insert(at, gain);
                into.Insert(at, c.Id);
            }
            return into.Count;
        }

        /// <summary>The professions whose work raises a skill (their own two skills; a day's work teaches both).</summary>
        public static int RaisingProfessions(int skill, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                if (p.SkillA == skill || p.SkillB == skill)
                {
                    into.Add(p.Id);
                }
            }
            return into.Count;
        }

        /// <summary>One group of courses of which any one will do (a rank's qualification), the courses' ids.</summary>
        public static void PostMissing(StudyFile f, Profession p, int rank, List<int[]> groups, List<int> certCourses)
        {
            groups.Clear();
            certCourses.Clear();
            int certs = StudyRules.Certs(f);
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                ProRank pr = p.Ranks[r];
                if (pr.Cert >= 0 && (certs & (1 << pr.Cert)) == 0)
                {
                    CourseDef c = StudyCatalogue.OfCert(pr.Cert);
                    if (c != null && !certCourses.Contains(c.Id))
                    {
                        certCourses.Add(c.Id);
                    }
                }
                if (pr.Quals.Length > 0 && !(ProRules.SelfTaught(p) && r == 0))
                {
                    bool held = false;
                    for (int i = 0; i < pr.Quals.Length && !held; i++)
                    {
                        held = f.Holds(pr.Quals[i]);
                    }
                    if (!held)
                    {
                        groups.Add(pr.Quals);
                    }
                }
            }
        }
    }
}
