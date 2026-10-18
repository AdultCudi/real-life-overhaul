using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Why a course cannot be started now; never saved, each the end of a locale key.</summary>
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

    /// <summary>Where the player is, as far as study cares: read from the game by the director, decided on here.</summary>
    public struct StudyHere
    {
        public bool School;       // inside the game's school (Manhattan Business School)
        public bool Hospital;     // inside a hospital or clinic
        public bool University;   // at the university's sign or door, or inside its building
        public bool Home;         // in a home of the player's
        public bool Online;       // that home has a working internet connection
        public HomeNet Net;       // ...and if not, why not: an outage is said apart from the rest
    }

    /// <summary>
    /// The home's internet as careers reads it (Utilities, through CareersBridges): up, or why it is down. Only an outage
    /// is the city's doing; no plan, one being put in and one cut for a bill not paid are the player's own. Never saved.
    /// </summary>
    public enum HomeNet
    {
        Online = 0,       // up, or no utilities to ask (a home is then taken to be connected)
        Outage = 1,       // the provider's outage: what it keeps the player from is excused (StudyRules.Excuses)
        NoHome = 2,       // no home of the player's
        NoPlan = 3,       // no internet plan at the home, or one closed
        Installing = 4,   // a plan bought and not yet connected
        Cut = 5           // cut off for a bill not paid
    }

    /// <summary>What the panel where the player stands offers: nothing, a class to attend, an exam to sit.</summary>
    public enum StudyPrompt
    {
        None = 0,
        Class = 1,
        Exam = 2
    }

    /// <summary>The rules of studying in plain numbers (Careers/Study/StudyCatalogue); game-free: EconomyCheck runs every one.</summary>
    public static partial class StudyRules
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
                return here.Online ? null : NoNetWhy(here.Net == HomeNet.Online ? HomeNet.NoPlan : here.Net);   // not online with no reason given: no working internet
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
        /// What the panel where the player stands offers for a course, from whether its class can be joined and its exam sat
        /// here and now (null: yes; else the reason key): a class first, then an exam, and nothing while the player is busy.
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

        /// <summary>
        /// The one rule for what the home's internet being down excuses: only a provider's outage, which is not the
        /// player's doing. No home, no plan, one not yet connected or one cut for an unpaid bill excuses nothing.
        /// </summary>
        public static bool Excuses(HomeNet net) => net == HomeNet.Outage;

        /// <summary>Why something done at the home's computer cannot be done now (a reason key), or null with the internet up: the one reason every such activity gives.</summary>
        public static string NoNetWhy(HomeNet net)
        {
            return net == HomeNet.Online ? null : net == HomeNet.Outage ? "reallife_study_why_outage" : "reallife_study_why_no_internet";
        }

        /// <summary>
        /// The minutes of the save (day * 1440 + minute) a day's class can be joined in: from
        /// <see cref="StudyCatalogue.ArriveEarlyMinutes"/> before its start to <see cref="StudyCatalogue.LateMinutes"/> after.
        /// False on a day it has no class.
        /// </summary>
        public static bool JoinWindow(CourseDef c, StudyMode mode, int day, out int from, out int to)
        {
            from = to = 0;
            if (c == null || !c.Timetabled || !On(Days(c, mode), day))
            {
                return false;
            }
            int start = day * 24 * 60 + Hour(c, mode) * 60;
            from = start - StudyCatalogue.ArriveEarlyMinutes;
            to = start + StudyCatalogue.LateMinutes;
            return true;
        }

        /// <summary>True if a class gone by without the player is excused rather than missed: one held online from home, with the home's internet down over its window for an outage.</summary>
        public static bool ClassExcused(CourseDef c, StudyMode mode, HomeNet net)
        {
            return ClassPlayRules.HeldOnline(c, mode) && Excuses(net);
        }

        public const int LiveMost = 4;   // courses at once

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

        public const int HeldLookBack = 28;   // days: classes gone by but not yet counted are looked for this far back (the hour and the day count them sooner)

        /// <summary>
        /// The classes of an enrolment held so far: those counted, those whose window has shut since but are not counted yet,
        /// and the one being sat now (<paramref name="sitting"/>: its course, or -1). At most the course's classes; a loop of
        /// at most four weeks, run when a page is written.
        /// </summary>
        public static int Held(Enrolment e, int today, int minuteOfDay, int sitting)
        {
            CourseDef c = e != null ? e.Def : null;
            if (c == null)
            {
                return 0;
            }
            if (!c.Timetabled || e.Stage != (int)EnrolStage.Studying || e.Seen >= c.Sessions)
            {
                return e.Seen;
            }
            var mode = (StudyMode)e.Mode;
            int held = e.Seen, days = Days(c, mode);
            for (int d = Math.Max(Math.Max(e.Start, e.SettledDay + 1), today - HeldLookBack); d <= today && held < c.Sessions; d++)
            {
                if (Gone(c, mode, d, today, minuteOfDay) || (d == today && sitting == e.Course && On(days, d)))
                {
                    held++;
                }
            }
            return held;
        }

        /// <summary>
        /// The next class of an enrolment that can still be joined, as day * 1440 + its starting minute, or -1: none while
        /// it is not at its classes, and none once every class of the course has been held (<see cref="Held"/>). The one rule
        /// every page that names the next class reads: the Campus app, Persona, the desks and doors, the notices.
        /// </summary>
        public static int NextClass(Enrolment e, int today, int minuteOfDay, int sitting)
        {
            CourseDef c = e != null ? e.Def : null;
            if (c == null || !c.Timetabled || e.Stage != (int)EnrolStage.Studying || Held(e, today, minuteOfDay, sitting) >= c.Sessions)
            {
                return -1;
            }
            int from = Math.Max(e.Start, e.SettledDay + 1);
            return NextClass(c, (StudyMode)e.Mode, today, minuteOfDay, sitting == e.Course ? Math.Max(from, today + 1) : from);
        }

        /// <summary>
        /// True if "your class starts now" is due for an enrolment at this minute: its next class (<see cref="NextClass(Enrolment, int, int, int)"/>,
        /// so a class beyond the course's last, or one being sat, never) begins now, and it has not been told for that class
        /// (<paramref name="told"/>: <see cref="Enrolment.StartTold"/>, kept in the save, so a reload does not tell it again).
        /// </summary>
        public static bool StartsNow(Enrolment e, int today, int minuteOfDay, int sitting, int told)
        {
            int next = NextClass(e, today, minuteOfDay, sitting);
            return next >= 0 && next == today * 24 * 60 + minuteOfDay && next > told;
        }

        /// <summary>True if the reason a class cannot be joined is only that the player is not inside the place that holds it (the school, the university, a ward).</summary>
        public static bool Elsewhere(string why)
        {
            return why == "reallife_study_why_not_school" || why == "reallife_study_why_not_university" || why == "reallife_study_why_not_hospital";
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

        /// <summary>
        /// A class gone by without the player, excused (<see cref="ClassExcused"/>): held, so the course moves on, but left
        /// out of the classes that count (<see cref="Counted"/>), neither attended nor missed. True if it was one.
        /// </summary>
        public static bool Excuse(Enrolment e, int day)
        {
            if (!Miss(e, day))
            {
                return false;
            }
            e.Excused++;
            e.ExcusedDay = day;
            return true;
        }

        /// <summary>The classes of a course that count towards attendance: all of them but those excused.</summary>
        public static int Counted(Enrolment e)
        {
            CourseDef c = e.Def;
            return c == null ? 0 : Math.Max(0, c.Sessions - Math.Max(0, e.Excused));
        }

        /// <summary>The share of a course's counted classes attended so far, thousandths (all of them for a course without classes, or with every class excused).</summary>
        public static int Attendance(Enrolment e)
        {
            CourseDef c = e.Def;
            int counted = Counted(e);
            return c == null || !c.Timetabled ? 1000 : counted <= 0 ? 1000 : Math.Min(1000, e.Done * 1000 / counted);
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
        public static bool Honours(CourseDef c, int grade) => c != null && grade >= 3;

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
    }
}
