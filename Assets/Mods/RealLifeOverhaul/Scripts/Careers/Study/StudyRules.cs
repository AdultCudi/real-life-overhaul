using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Why a course cannot be started now: the saved number is never kept, each is a locale key's end.</summary>
    public enum EnrolRefusal
    {
        None = 0,
        Already,        // on it now
        Passed,         // passed before
        Needs,          // a course it needs is not passed
        Full,           // four courses at once already
        NoMoney,
        NoDistance,     // asked for online study of a course that has none
        Closed          // the part of studying it belongs to is switched off
    }

    /// <summary>
    /// The rules of studying, in plain numbers (Careers/Study/StudyCatalogue): when classes are, what a class and a piece of
    /// homework teach, what an exam comes to and the grade it earns, retakes, fees and grants, the student loan, and how
    /// skills fade. Game-free: EconomyCheck runs every one.
    /// </summary>
    /// <summary>Where the player is, as far as study cares: read from the game by the director, decided on here.</summary>
    public struct StudyHere
    {
        public bool School;       // inside the game's school (Manhattan Business School)
        public bool Hospital;     // inside a hospital or clinic
        public bool University;   // at the university's sign or door, or inside its building
        public bool Home;         // in a home of the player's
        public bool Online;       // that home has a working internet connection
    }

    /// <summary>What the panel where the player stands offers: nothing, a class to attend, an exam to sit.</summary>
    public enum StudyPrompt
    {
        None = 0,
        Class = 1,
        Exam = 2
    }

    public static class StudyRules
    {
        /// <summary>Null if the player is where a course's classes are held; else the reason key (what to say, and where to go).</summary>
        public static string AtClass(CourseDef c, StudyMode mode, StudyHere here)
        {
            if (mode == StudyMode.Distance || c.Campus == Campus.Online)
            {
                if (!here.Home)
                {
                    return "reallife_study_why_not_home";
                }
                return here.Online ? null : "reallife_study_why_no_internet";
            }
            switch (c.Campus)
            {
                case Campus.School: return here.School ? null : "reallife_study_why_not_school";
                case Campus.Hospital: return here.Hospital ? null : "reallife_study_why_not_hospital";
                default: return here.University ? null : "reallife_study_why_not_university";
            }
        }

        /// <summary>Exams are sat where the classes are; online courses and certificates are examined at the school.</summary>
        public static string AtExam(CourseDef c, StudyMode mode, StudyHere here)
        {
            if (mode == StudyMode.Distance || c.Campus == Campus.Online)
            {
                return here.School ? null : "reallife_study_why_exam_school";
            }
            return AtClass(c, mode, here);
        }

        /// <summary>
        /// What the panel where the player stands offers for one course, from whether its class can be joined and its exam
        /// sat here and now (null: yes; else the reason key): a class first, then an exam, and nothing while the player is
        /// busy with something else (a sitting under way, the wheel, another screen).
        /// </summary>
        public static StudyPrompt Prompt(string classWhy, string examWhy, bool busy)
        {
            if (busy)
            {
                return StudyPrompt.None;
            }
            return classWhy == null ? StudyPrompt.Class : examWhy == null ? StudyPrompt.Exam : StudyPrompt.None;
        }

        /// <summary>
        /// The notice when a class's hour comes: none if it cannot be joined at all now (no class, a fee owed is said by its
        /// own notice); "press the key" if it can be joined where the player is; "go there" if only the place is wrong.
        /// </summary>
        public static int ClassNotice(string why)
        {
            if (why == null)
            {
                return 1;   // here: press the key
            }
            if (why == "reallife_study_why_no_class")
            {
                return 0;
            }
            return 2;       // elsewhere, or another reason the mod knows: why, where, and for how long
        }

        public const int LiveMost = 4;   // courses at once

        // ---------------------------------------------------------------- the timetable

        /// <summary>The days a course's classes are on in a mode (bit 0 Monday).</summary>
        public static int Days(CourseDef c, StudyMode mode)
        {
            if (c == null || !c.Timetabled)
            {
                return 0;
            }
            if (mode != StudyMode.PartTime || c.Kind == CourseKind.Short)
            {
                return c.Days;
            }
            return c.Campus == Campus.School ? StudyCatalogue.PartTimeDaysSchool : StudyCatalogue.PartTimeDaysUniversity;
        }

        /// <summary>The hour a course's classes start in a mode.</summary>
        public static int Hour(CourseDef c, StudyMode mode)
        {
            if (c == null)
            {
                return 0;
            }
            if (mode != StudyMode.PartTime || c.Kind == CourseKind.Short)
            {
                return c.Hour;
            }
            return c.Campus == Campus.School ? StudyCatalogue.PartTimeHourSchool : StudyCatalogue.PartTimeHourUniversity;
        }

        /// <summary>True on a day of a mask (day 1 is a Monday, as the game counts).</summary>
        public static bool On(int days, int day)
        {
            return day > 0 && (days & (1 << ((day - 1) % 7))) != 0;
        }

        /// <summary>Classes a week in a mode.</summary>
        public static int PerWeek(CourseDef c, StudyMode mode)
        {
            int days = Days(c, mode), n = 0;
            for (int i = 0; i < 7; i++)
            {
                n += (days >> i) & 1;
            }
            return n;
        }

        /// <summary>About how many days the classes take, from the first: whole weeks of the timetable.</summary>
        public static int Length(CourseDef c, StudyMode mode)
        {
            int week = PerWeek(c, mode);
            return week <= 0 ? 0 : (c.Sessions + week - 1) / week * 7;
        }

        /// <summary>
        /// Whether a class can be joined at this minute of this day (from <see cref="StudyCatalogue.ArriveEarlyMinutes"/>
        /// before its start until <see cref="StudyCatalogue.LateMinutes"/> after), with the minutes it still has to run.
        /// </summary>
        public static bool Joinable(CourseDef c, StudyMode mode, int day, int minuteOfDay, out int minutesLeft)
        {
            minutesLeft = 0;
            if (c == null || !c.Timetabled || !On(Days(c, mode), day))
            {
                return false;
            }
            int start = Hour(c, mode) * 60;
            if (minuteOfDay < start - StudyCatalogue.ArriveEarlyMinutes || minuteOfDay > start + StudyCatalogue.LateMinutes)
            {
                return false;
            }
            minutesLeft = start + c.Hours * 60 - Math.Max(minuteOfDay, start);
            return minutesLeft > 0;
        }

        /// <summary>
        /// The next class of a course that can still be joined, from now on (today's if its window is still open), as
        /// day * 1440 + its starting minute; -1 if none in the next four weeks. <paramref name="fromDay"/>: the first day it
        /// may be on (the course's start, the day after the last one settled).
        /// </summary>
        public static int NextClass(CourseDef c, StudyMode mode, int today, int minuteOfDay, int fromDay)
        {
            if (c == null || !c.Timetabled)
            {
                return -1;
            }
            int start = Hour(c, mode) * 60;
            for (int d = Math.Max(today, fromDay); d < today + 28; d++)
            {
                if (On(Days(c, mode), d) && (d > today || minuteOfDay <= start + StudyCatalogue.LateMinutes))
                {
                    return d * 24 * 60 + start;
                }
            }
            return -1;
        }

        /// <summary>True once a day's class can no longer be joined: it is missed if not attended.</summary>
        public static bool Gone(CourseDef c, StudyMode mode, int day, int today, int minuteOfDay)
        {
            if (c == null || !c.Timetabled || !On(Days(c, mode), day))
            {
                return false;
            }
            return day < today || (day == today && minuteOfDay > Hour(c, mode) * 60 + StudyCatalogue.LateMinutes);
        }

        /// <summary>The next day on or after a day with a class of the course; -1 for none.</summary>
        public static int Next(CourseDef c, StudyMode mode, int from)
        {
            int days = Days(c, mode);
            for (int i = 0; days != 0 && i < 7; i++)
            {
                if (On(days, from + i))
                {
                    return from + i;
                }
            }
            return -1;
        }

        /// <summary>A class attended: counted, unless the day's class has been counted already.</summary>
        public static bool Attend(Enrolment e, int day)
        {
            CourseDef c = e.Def;
            if (c == null || e.Stage != (int)EnrolStage.Studying || e.SettledDay >= day || e.Seen >= c.Sessions)
            {
                return false;
            }
            e.Seen++;
            e.Done++;
            e.SettledDay = day;
            return true;
        }

        /// <summary>A class gone by without the player: counted as missed. True if it was one.</summary>
        public static bool Miss(Enrolment e, int day)
        {
            CourseDef c = e.Def;
            if (c == null || e.Stage != (int)EnrolStage.Studying || e.SettledDay >= day || e.Seen >= c.Sessions || day < e.Start)
            {
                return false;
            }
            e.Seen++;
            e.SettledDay = day;
            return true;
        }

        /// <summary>The share of a course's classes attended so far, thousandths (all of them for a course without classes).</summary>
        public static int Attendance(Enrolment e)
        {
            CourseDef c = e.Def;
            return c == null || !c.Timetabled ? 1000 : c.Sessions <= 0 ? 1000 : e.Done * 1000 / c.Sessions;
        }

        /// <summary>
        /// After a class is counted, or a certificate's last piece of study: the course moves on to its exam, or the student
        /// is barred for having missed too much. True if the stage changed.
        /// </summary>
        public static bool Advance(Enrolment e, int today)
        {
            CourseDef c = e.Def;
            if (c == null || e.Stage != (int)EnrolStage.Studying)
            {
                return false;
            }
            bool over = c.Timetabled ? e.Seen >= c.Sessions : e.Homework >= c.Homework;
            if (!over)
            {
                return false;
            }
            if (c.Timetabled && Attendance(e) < StudyCatalogue.Barred)
            {
                e.Stage = (int)EnrolStage.Barred;
                e.Ended = today;
                return true;
            }
            e.Stage = (int)EnrolStage.Exam;
            e.ExamFrom = today + (c.Kind == CourseKind.Short ? 0 : 1);
            e.ExamTo = today + StudyCatalogue.ExamWindowDays * (c.Kind == CourseKind.Certificate ? 2 : 1);
            return true;
        }

        /// <summary>True on a day the exam of a course at this stage can be sat (weekdays, within its window).</summary>
        public static bool ExamDay(Enrolment e, int day)
        {
            if (e.Stage != (int)EnrolStage.Exam && e.Stage != (int)EnrolStage.Retake)
            {
                return false;
            }
            int weekday = (day - 1) % 7;
            return day >= e.ExamFrom && day <= e.ExamTo && weekday < 5;
        }

        /// <summary>The exam's window has gone by unsat: it counts as a fail. True if it had.</summary>
        public static bool ExamMissed(Enrolment e, int today)
        {
            if ((e.Stage != (int)EnrolStage.Exam && e.Stage != (int)EnrolStage.Retake) || today <= e.ExamTo)
            {
                return false;
            }
            Fail(e, 0, today);
            return true;
        }

        // ---------------------------------------------------------------- what a class and homework teach

        /// <summary>
        /// The thousandths of a point one class adds to a skill that stands at <paramref name="points"/> (whole points): the
        /// course's gain over its classes, less the nearer the top, by how well the player learns (the schooling they hold).
        /// </summary>
        public static int ClassGain(int gain, int classes, int points, int learning)
        {
            if (gain <= 0 || classes <= 0)
            {
                return 0;
            }
            int per = gain * 1000 / classes;
            int factor = 1000 - (1000 - StudyCatalogue.GainAtTop) * TalentCatalogue.Clamp(points, 0, 100) / 100;
            return per * factor / 1000 * TalentCatalogue.Clamp(learning, 500, 1500) / 1000;
        }

        /// <summary>What a piece of homework teaches: a share of a class, or for a certificate (no classes) its own part of the gain.</summary>
        public static int HomeworkGain(CourseDef c, bool first, int points, int learning)
        {
            if (c == null)
            {
                return 0;
            }
            int gain = first ? c.GainA : c.GainB;
            if (!c.Timetabled)
            {
                return ClassGain(gain, Math.Max(1, c.Homework), points, learning);
            }
            return ClassGain(gain, c.Sessions, points, learning) * StudyCatalogue.HomeworkShare / 1000;
        }

        /// <summary>How well the player learns, thousandths: by the best schooling they hold (the professions' own figures).</summary>
        public static int Learning(int school)
        {
            return TalentCatalogue.SchoolLearning[TalentCatalogue.Clamp(school, 0, TalentCatalogue.Schools - 1)];
        }

        /// <summary>Homework set so far: one piece for each class attended, up to the course's number (all of a certificate's at once).</summary>
        public static int HomeworkSet(Enrolment e)
        {
            CourseDef c = e.Def;
            if (c == null)
            {
                return 0;
            }
            if (!c.Timetabled)
            {
                return c.Homework;
            }
            return Math.Min(c.Homework, c.Sessions > 0 ? e.Done * c.Homework / c.Sessions : 0);
        }

        /// <summary>True if a piece of homework can be done now: one is set and not done, or the classes are over and revision is still worth something.</summary>
        public static bool HomeworkOpen(Enrolment e)
        {
            CourseDef c = e.Def;
            if (c == null || !e.Live)
            {
                return false;
            }
            if (e.Homework < HomeworkSet(e))
            {
                return true;
            }
            return e.Stage != (int)EnrolStage.Studying && e.Homework < c.Homework + StudyCatalogue.RevisionMost;
        }

        // ---------------------------------------------------------------- the exam

        /// <summary>
        /// The exam's mark out of 100: a fifth for turning up, then by the share of classes attended (four tenths), the
        /// homework done (a quarter), how far the course's first skill has come against what the course takes it to (three
        /// twentieths), two for each piece of revision, five off for sitting it tired, and the day's own luck
        /// (<paramref name="luck"/>, -<see cref="StudyCatalogue.ExamNoise"/> to +).
        /// </summary>
        public static int ExamScore(Enrolment e, int skillPoints, bool tired, int luck)
        {
            CourseDef c = e.Def;
            if (c == null)
            {
                return 0;
            }
            double attended = Attendance(e) / 1000.0;
            int set = Math.Max(1, c.Homework);
            double homework = c.Homework <= 0 ? 1.0 : Math.Min(1.0, e.Homework / (double)set);
            int revision = Math.Max(0, Math.Min(StudyCatalogue.RevisionMost, e.Homework - c.Homework));
            int target = Math.Max(10, Math.Min(100, 20 + c.GainA));
            double prepared = Math.Min(1.0, skillPoints / (double)target);
            double score = 20 + 40 * attended + 25 * homework + 15 * prepared + revision * 2 - (tired ? 5 : 0) + TalentCatalogue.Clamp(luck, -StudyCatalogue.ExamNoise, StudyCatalogue.ExamNoise);
            return TalentCatalogue.Clamp((int)Math.Round(score), 0, 100);
        }

        /// <summary>The day's luck in an exam, the same on a reload: -10 to 10.</summary>
        public static int Luck(int seed, Enrolment e)
        {
            return TechRandom.Range(seed, 7101, e.Course, e.Start * 8 + e.Retakes, -StudyCatalogue.ExamNoise, StudyCatalogue.ExamNoise);
        }

        /// <summary>
        /// The grade a mark earns. A course: 0 fail, 1 pass, 2 merit, 3 distinction. A degree or a master's: 0 fail, 1 a
        /// third, 2 a lower second, 3 an upper second, 4 a first.
        /// </summary>
        public static int Grade(CourseDef c, int score)
        {
            if (c == null || score < c.Pass)
            {
                return 0;
            }
            if (c.Higher)
            {
                return score >= StudyCatalogue.First ? 4 : score >= StudyCatalogue.UpperSecond ? 3 : score >= StudyCatalogue.LowerSecond ? 2 : 1;
            }
            return score >= StudyCatalogue.Distinction ? 3 : score >= StudyCatalogue.Merit ? 2 : 1;
        }

        /// <summary>The best grade a course can be given (a first, a distinction).</summary>
        public static int TopGrade(CourseDef c) => c != null && c.Higher ? 4 : 3;

        /// <summary>True for a grade with honours: a first or an upper second, a distinction.</summary>
        public static bool Honours(CourseDef c, int grade) => c != null && (c.Higher ? grade >= 3 : grade >= 3);

        /// <summary>The exam sat: the mark and grade are kept, and it is passed, or failed with a retake to come (or none left). True if passed.</summary>
        public static bool Sit(Enrolment e, int score, int today)
        {
            CourseDef c = e.Def;
            e.Score = score;
            e.Grade = Grade(c, score);
            if (e.Grade > 0)
            {
                e.Stage = (int)EnrolStage.Passed;
                e.Ended = today;
                return true;
            }
            Fail(e, score, today);
            return false;
        }

        private static void Fail(Enrolment e, int score, int today)
        {
            e.Score = score;
            e.Grade = 0;
            if (e.Retakes < StudyCatalogue.RetakesMost)
            {
                e.Retakes++;
                e.Stage = (int)EnrolStage.Retake;
                e.ExamFrom = today + StudyCatalogue.RetakeDays;
                e.ExamTo = e.ExamFrom + StudyCatalogue.ExamWindowDays;
            }
            else
            {
                e.Stage = (int)EnrolStage.Failed;
                e.Ended = today;
            }
        }

        /// <summary>What a retake costs, whole money at the start's prices.</summary>
        public static int RetakeFee(CourseDef c)
        {
            return c == null ? 0 : Math.Max(StudyCatalogue.RetakeFeeLeast, c.Fee * StudyCatalogue.RetakeFeeShare / 1000);
        }

        /// <summary>The day of a degree's graduation: the first Saturday at least two days after the result.</summary>
        public static int CeremonyDay(int resultDay)
        {
            int on = resultDay + 2;
            while ((on - 1) % 7 != 5)
            {
                on++;
            }
            return on;
        }

        // ---------------------------------------------------------------- the family's children

        /// <summary>What a child's degree costs the player, whole money at the start's prices: its fee less what the college fund covers.</summary>
        public static int ChildCost(CourseDef c, int fundWhole)
        {
            return c == null ? 0 : Math.Max(0, c.Fee - Math.Max(0, fundWhole));
        }

        /// <summary>
        /// The class a child graduates with (a degree's 1 to 4): by how close they are to the player (0 to 100), a private
        /// school, and their own luck (-15 to 15).
        /// </summary>
        public static int ChildGrade(float closeness, bool privateSchool, int luck)
        {
            int mark = 40 + (int)(Math.Max(0f, Math.Min(100f, closeness)) * 0.3f) + (privateSchool ? 8 : 0) + TalentCatalogue.Clamp(luck, -15, 15);
            return mark >= StudyCatalogue.First ? 4 : mark >= StudyCatalogue.UpperSecond ? 3 : mark >= StudyCatalogue.LowerSecond ? 2 : 1;
        }

        /// <summary>The students an academy teaches a day: as many as its staff can take, of those who come (more with the council's grants up).</summary>
        public static int AcademyStudents(int staff, int council)
        {
            int demand = StudyCatalogue.AcademyDemand * (council > 0 ? 12 : council < 0 ? 8 : 10) / 10;
            return Math.Max(0, Math.Min(staff * StudyCatalogue.AcademyPerStaff, demand + staff * 4));
        }

        // ---------------------------------------------------------------- tutoring

        /// <summary>What an hour of tutoring pays, cents at the start's prices: a base and more for the subject's points; nothing under the least.</summary>
        public static int TutorRate(int points)
        {
            return points < StudyCatalogue.TutorLeast ? 0 : StudyCatalogue.TutorBase + points * StudyCatalogue.TutorPerPoint;
        }

        /// <summary>The best subject the player could tutor: the skill with the most points among the professions' and teaching.</summary>
        public static int TutorSkill(StudyFile f)
        {
            int best = Skills.Teaching, points = -1;
            for (int s = 0; s < Skills.Count; s++)
            {
                bool subject = Skills.Professional(s) || s == Skills.Teaching;
                if (subject && f.Skill[s] > points)
                {
                    best = s;
                    points = f.Skill[s];
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- starting a course

        /// <summary>Whether the player may start a course now, and why not.</summary>
        public static EnrolRefusal CanEnrol(StudyFile f, CourseDef c, StudyMode mode)
        {
            if (c == null)
            {
                return EnrolRefusal.Closed;
            }
            if (f.Live(c.Id) != null)
            {
                return EnrolRefusal.Already;
            }
            if (f.Holds(c.Id))
            {
                return EnrolRefusal.Passed;
            }
            if (!Qualified(f, c))
            {
                return EnrolRefusal.Needs;
            }
            if (f.Courses.Count >= LiveMost)
            {
                return EnrolRefusal.Full;
            }
            if (mode == StudyMode.Distance && !c.Distance)
            {
                return EnrolRefusal.NoDistance;
            }
            return EnrolRefusal.None;
        }

        /// <summary>True if the player has passed what the course needs first.</summary>
        public static bool Qualified(StudyFile f, CourseDef c)
        {
            if (c.Needs.Length == 0 && !c.NeedsDegree)
            {
                return true;
            }
            for (int i = 0; i < c.Needs.Length; i++)
            {
                if (f.Holds(c.Needs[i]))
                {
                    return true;
                }
            }
            return c.NeedsDegree && HoldsAny(f, CourseKind.Degree);
        }

        /// <summary>True if any course of a kind has been passed.</summary>
        public static bool HoldsAny(StudyFile f, CourseKind kind)
        {
            for (int i = 0; i < f.Results.Count; i++)
            {
                CourseDef c = f.Results[i].Def;
                if (c != null && c.Kind == kind && f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The best schooling the player's results count as (TalentCatalogue's schools): self-taught with none.</summary>
        public static int School(StudyFile f)
        {
            int best = TalentCatalogue.SelfTaught;
            for (int i = 0; i < f.Results.Count; i++)
            {
                CourseDef c = f.Results[i].Def;
                if (c != null && f.Results[i].Stage == (int)EnrolStage.Passed && c.School > best)
                {
                    best = c.School;
                }
            }
            return best;
        }

        /// <summary>The professional certificates held (TalentCatalogue.Certs), one bit each, as a professional's file keeps them.</summary>
        public static int Certs(StudyFile f)
        {
            int bits = 0;
            for (int i = 0; i < f.Results.Count; i++)
            {
                CourseDef c = f.Results[i].Def;
                if (c != null && c.Cert >= 0 && c.Cert < 31 && f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    bits |= 1 << c.Cert;
                }
            }
            return bits;
        }

        /// <summary>The courses passed, one bit each by id.</summary>
        public static long Passed(StudyFile f)
        {
            long bits = 0;
            for (int i = 0; i < f.Results.Count; i++)
            {
                int id = f.Results[i].Course;
                if (f.Results[i].Stage == (int)EnrolStage.Passed && id >= 0 && id < StudyCatalogue.IdsMost)
                {
                    bits |= 1L << id;
                }
            }
            return bits;
        }

        // ---------------------------------------------------------------- fees and grants

        /// <summary>
        /// The share of the fee taken off, thousandths: a merit grant for a student whose last result was the top grade, a
        /// bursary for one with little money, and the council's policy on education (+1 a subsidy, -1 a surcharge, which
        /// adds). Never more than <see cref="StudyCatalogue.GrantMost"/> off; a short course has no grants but the council's.
        /// </summary>
        public static int Grant(StudyFile f, CourseDef c, int cashWhole, int priceLevel, int council)
        {
            int off = 0;
            if (c.Kind != CourseKind.Short)
            {
                Enrolment last = LastPass(f);
                if (last != null && last.Grade >= TopGrade(last.Def))
                {
                    off += StudyCatalogue.MeritGrantShare;
                }
                if (cashWhole < Prices(StudyCatalogue.NeedGrantCash, priceLevel))
                {
                    off += StudyCatalogue.NeedGrantShare;
                }
            }
            off += council > 0 ? StudyCatalogue.CouncilShare : council < 0 ? -StudyCatalogue.CouncilShare : 0;
            return TalentCatalogue.Clamp(off, -StudyCatalogue.CouncilShare, StudyCatalogue.GrantMost);
        }

        private static Enrolment LastPass(StudyFile f)
        {
            for (int i = f.Results.Count - 1; i >= 0; i--)
            {
                if (f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    return f.Results[i];
                }
            }
            return null;
        }

        /// <summary>A course's whole fee after its grant, whole money at the start's prices.</summary>
        public static int Fee(CourseDef c, int grant)
        {
            return c == null ? 0 : Math.Max(0, (int)((long)c.Fee * (1000 - grant) / 1000));
        }

        /// <summary>How many instalments a course's fee is paid in: a degree's thirds, every other course's at once.</summary>
        public static int Instalments(CourseDef c) => c != null && c.Higher ? StudyCatalogue.InstalmentsHigher : 1;

        /// <summary>
        /// The instalment of the fee that falls due now, whole money at the start's prices, or 0: the first at enrolment,
        /// a degree's others once a third and two thirds of its classes have gone by.
        /// </summary>
        public static int InstalmentDue(Enrolment e)
        {
            CourseDef c = e.Def;
            int parts = Instalments(c);
            if (c == null || e.Instalments >= parts)
            {
                return 0;
            }
            int due = e.Instalments == 0 ? 0 : c.Sessions * e.Instalments / parts;
            if (e.Seen < due)
            {
                return 0;
            }
            int whole = Fee(c, e.Grant);
            return e.Instalments == parts - 1 ? whole - whole / parts * (parts - 1) : whole / parts;
        }

        private static int Prices(int amount, int level) => PriceRules.At(amount, level);

        // ---------------------------------------------------------------- the student loan

        /// <summary>A day's interest on the student loan, cents.</summary>
        public static long LoanInterest(long owed, int daysPerYear)
        {
            return owed <= 0 ? 0 : owed * StudyCatalogue.LoanRateBp / 10000 / Math.Max(60, daysPerYear);
        }

        /// <summary>
        /// What the student loan takes from a day's pay: <see cref="StudyCatalogue.LoanShareBp"/> of what an hour pays over
        /// the threshold, for the hours paid; never more than is owed.
        /// </summary>
        public static long LoanRepayment(long owed, int centsAnHour, int hours, int priceLevel)
        {
            int threshold = PriceRules.At(StudyCatalogue.LoanThreshold, priceLevel);
            if (owed <= 0 || centsAnHour <= threshold || hours <= 0)
            {
                return 0;
            }
            long over = (long)(centsAnHour - threshold) * hours;
            return Math.Min(owed, over * StudyCatalogue.LoanShareBp / 10000);
        }

        /// <summary>True if the loan can pay so much more (whole money at the start's prices).</summary>
        public static bool LoanRoom(StudyFile f, int whole, int priceLevel)
        {
            return f.LoanCents + whole * 100L <= PriceRules.At(StudyCatalogue.LoanMost, priceLevel) * 100L;
        }

        // ---------------------------------------------------------------- skills

        /// <summary>Thousandths added to a skill, to 100 points at most; the best ever is kept and the day it was used.</summary>
        public static void Learn(StudyFile f, int skill, int thousandths, int day)
        {
            if (skill < 0 || skill >= Skills.Count || thousandths <= 0)
            {
                return;
            }
            f.Skill[skill] = Math.Min(100000, f.Skill[skill] + thousandths);
            f.Best[skill] = Math.Max(f.Best[skill], f.Skill[skill]);
            f.Used[skill] = day;
        }

        /// <summary>A skill used at work or in a class without being taught: it stops fading for a while.</summary>
        public static void Use(StudyFile f, int skill, int day)
        {
            if (skill >= 0 && skill < Skills.Count)
            {
                f.Used[skill] = day;
            }
        }

        /// <summary>
        /// A day gone by: every skill unused for more than <see cref="StudyCatalogue.FadeAfterDays"/> days fades by
        /// <see cref="StudyCatalogue.FadePerDay"/> thousandths, never below <see cref="StudyCatalogue.FadeFloor"/> of its best
        /// nor below <see cref="StudyCatalogue.FadeLeast"/> points. Returns the thousandths lost in all.
        /// </summary>
        public static int Fade(StudyFile f, int day)
        {
            int lost = 0;
            for (int s = 0; s < Skills.Count; s++)
            {
                int now = f.Skill[s];
                if (now <= StudyCatalogue.FadeLeast * 1000 || day - f.Used[s] <= StudyCatalogue.FadeAfterDays)
                {
                    continue;
                }
                int floor = Math.Max(StudyCatalogue.FadeLeast * 1000, (int)((long)f.Best[s] * StudyCatalogue.FadeFloor / 1000));
                int next = Math.Max(floor, now - StudyCatalogue.FadePerDay);
                lost += now - next;
                f.Skill[s] = next;
            }
            return lost;
        }

        /// <summary>A skill in whole points.</summary>
        public static int Points(StudyFile f, int skill) => skill >= 0 && skill < Skills.Count ? f.Skill[skill] / 1000 : 0;

        /// <summary>The week's minutes of study, the week starting again each seventh day. True if the week has just passed the stressed mark.</summary>
        public static bool Studied(StudyFile f, int day, int minutes)
        {
            int week = (day - 1) / 7;
            if (week != f.WeekOf)
            {
                f.WeekOf = week;
                f.WeekMinutes = 0;
            }
            int before = f.WeekMinutes;
            f.WeekMinutes += Math.Max(0, minutes);
            int mark = StudyCatalogue.StressedWeekHours * 60;
            return before < mark && f.WeekMinutes >= mark;
        }
    }
}
