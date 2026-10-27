using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Where a course stands: the saved number of <see cref="Enrolment.Stage"/>.</summary>
    public enum EnrolStage
    {
        Studying = 0,   // classes and homework under way
        Exam = 1,       // the classes are over: the exam is to be sat between ExamFrom and ExamTo
        Passed = 2,
        Failed = 3,     // failed and out of retakes, or the exam window missed for the last time
        Withdrawn = 4,
        Barred = 5,     // too few classes attended to sit the exam
        Retake = 6      // failed: a retake can be sat from ExamFrom to ExamTo
    }

    /// <summary>One course the player is on, or was on (a result). Game-free.</summary>
    public sealed class Enrolment
    {
        public int Course;
        public int Mode;            // StudyMode
        public int Start;           // the day enrolled
        public int Seen;            // classes of the timetable gone by, attended or missed
        public int Done;            // ...of them attended
        public int SettledDay = -1; // the last day whose class has been counted (attended or missed)
        public int Homework;        // pieces of homework done (beyond the set ones: revision)
        public int Fee;             // whole money charged so far
        public int Instalments;     // instalments of the fee paid (a degree's are three)
        public int Loan;            // whole money of the fee the student loan paid
        public bool OnLoan;         // the student loan pays the instalments as they fall due
        public int Grant;           // thousandths of the fee taken off
        public int Stage;           // EnrolStage
        public int ExamFrom, ExamTo;
        public int Retakes;
        public int Score, Grade;
        public int Ended;           // the day it ended
        public bool Graduated;      // a degree's graduation day has been held (or let pass)
        public bool FeeLate;        // an instalment fell due and could not be paid: the classes wait for it
        public int StartTold;       // the class whose start was last told ("your class starts now"), day * 1440 + its minute; 0 none (StudyRules.StartsNow)
        public int Excused;         // classes of Seen excused for an internet outage: neither attended nor missed (StudyRules.Excuse)
        public int ExcusedDay = -1; // the day of the last of them; -1 none
        public int HomeworkMinutes; // game minutes sat on the piece of homework (or revision) being done, under StudyRules.HomeworkFull; 0 none (StudyRules.HomeworkSat)

        public EnrolStage At => (EnrolStage)Stage;

        public bool Live => Stage == (int)EnrolStage.Studying || Stage == (int)EnrolStage.Exam || Stage == (int)EnrolStage.Retake;

        public CourseDef Def => StudyCatalogue.Find(Course);
    }

    /// <summary>Where a child's university stands: the saved number of <see cref="ChildStudy.Stage"/>.</summary>
    public enum ChildStep
    {
        Offered = 0, Studying = 1, Graduated = 2, Declined = 3
    }

    /// <summary>A grown child of the family and their university (Family's children, read through Wiring/CareersLinks). Game-free.</summary>
    public sealed class ChildStudy
    {
        public string Name = string.Empty;
        public int Course;          // the degree they read
        public int Stage;           // ChildStep
        public int Start, End;      // the days it runs
        public int Paid;            // whole money the player paid (the rest from the college fund)
        public int Grade;           // the class they graduate with

        public ChildStep At => (ChildStep)Stage;
    }

    /// <summary>The player's learning, skills (thousandths, with the best and the day last used), the student loan and their working life (<see cref="Work"/>). Game-free.</summary>
    public sealed class StudyFile
    {
        public int Seed = 1, FirstDay, LastDay = -1;
        public readonly int[] Skill = new int[Skills.Count];
        public readonly int[] Best = new int[Skills.Count];
        public readonly int[] Used = new int[Skills.Count];
        public readonly List<Enrolment> Courses = new List<Enrolment>(8);    // live ones
        public readonly List<Enrolment> Results = new List<Enrolment>(32);   // finished ones, the newest last
        public long LoanCents;       // what is owed on the student loan
        public long LoanTaken;       // ...ever borrowed
        public long LoanRepaid;
        public int WeekOf = -1, WeekMinutes;   // the week (day / 7) and the minutes of classes and homework in it
        public int GraduationDay, GraduationCourse = -1;   // a graduation ceremony to come
        public int Taught;           // minutes of tutoring given, ever
        public readonly WorkFile Work = new WorkFile();   // the working life, kept in the same line (Careers/Work)
        public readonly List<ChildStudy> Children = new List<ChildStudy>(3);   // the family's grown children at university
        public int TutorDay = -1, TutorToday;   // tutoring sessions given on a day

        public const int ResultsMost = 40;

        public Enrolment Live(int course)
        {
            for (int i = 0; i < Courses.Count; i++)
            {
                if (Courses[i].Course == course)
                {
                    return Courses[i];
                }
            }
            return null;
        }

        /// <summary>True if the course has been passed.</summary>
        public bool Holds(int course)
        {
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].Course == course && Results[i].Stage == (int)EnrolStage.Passed)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The best result of a passed course (its grade), or -1.</summary>
        public int GradeOf(int course)
        {
            int best = -1;
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].Course == course && Results[i].Stage == (int)EnrolStage.Passed && Results[i].Grade > best)
                {
                    best = Results[i].Grade;
                }
            }
            return best;
        }

        public void Finish(Enrolment e)
        {
            Courses.Remove(e);
            Results.Add(e);
            while (Results.Count > ResultsMost)
            {
                // the oldest result that is not a pass goes first; passes are qualifications and are kept
                int drop = 0;
                for (int i = 0; i < Results.Count; i++)
                {
                    if (Results[i].Stage != (int)EnrolStage.Passed)
                    {
                        drop = i;
                        break;
                    }
                }
                Results.RemoveAt(drop);
            }
        }
    }
}
