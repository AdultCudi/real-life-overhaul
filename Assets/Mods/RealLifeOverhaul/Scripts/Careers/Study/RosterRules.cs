using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One line of a transcript, for the grade point average and the credits: a finished course.</summary>
    public struct Mark
    {
        public int Credits;
        public int Points;    // hundredths of a grade point, from the exam score
        public int Start;     // the day the course began
    }

    /// <summary>One line of a class list: a person and their grade point average.</summary>
    public struct ClassLine
    {
        public string Name;
        public int Gpa;       // hundredths
        public bool You;
        public bool Employee;
    }

    /// <summary>
    /// The people in a campus's classes and what they decide: seats in a cohort that NPC students fill over time, the player's own
    /// employees who may enrol beside them, a tutor for each course whose rating moves an exam a little, a skill floor, credits, the
    /// grade point average by quarter and the player's rank in the class. Names come from the game's own name roll (the caller's
    /// function); everything else is seeded from the save's seed, the course and the day, so a load gives the same class. Game-free
    /// (EconomyCheck runs it). Cost: a few additions a call; nothing per frame.
    /// </summary>
    public static class RosterRules
    {
        public const int SeatsDefault = 24;
        public const int IntakeDays = 28;                 // a cohort's length: classes begin and seats fill over this
        public const int FillRampDays = 28, HeadStartDays = 7;   // seats are taken over this many days, from a week before the cohort starts
        public const int IntakesMost = 16, TutorsMost = 64;
        public const int FullShare = 150;                 // per mille of cohorts that fill every seat
        public const int FillLeast = 250, FillMost = 900; // per mille of the seats the others take by the start, when not full
        public const int EmployeeChance = 350;            // per mille that an employee of the player's follows the same course
        public const int EdgeMost = 2;                    // points a tutor moves an exam score, either way

        /// <summary>The first day of the cohort a day falls in.</summary>
        public static int StartOf(int day) => day - ((day % IntakeDays) + IntakeDays) % IntakeDays;

        /// <summary>The seats others hold in a cohort on a day: they fill from a week before it starts, over <see cref="FillRampDays"/>.</summary>
        public static int Taken(int seed, int course, int start, int capacity, int day)
        {
            int target = TechRandom.Mille(seed, 7801 + course, start) < FullShare ? capacity
                : capacity * TechRandom.Range(seed, 7802 + course, start, 0, FillLeast, FillMost) / 1000;
            int into = day - (start - HeadStartDays);
            int seats = into <= 0 ? 0 : target * Math.Min(into, FillRampDays) / FillRampDays;
            return Math.Max(0, Math.Min(capacity, seats));
        }

        /// <summary>The seats left, counting the player's and the employees' own.</summary>
        public static int Left(Intake i) => Math.Max(0, i.Capacity - i.Students.Count - (i.PlayerIn ? 1 : 0));

        public static bool CanJoin(Intake i) => i != null && Left(i) > 0;

        /// <summary>
        /// The cohort of a course for the day, made when it is first asked for and filled up to what the others have taken by then. New
        /// students are named by <paramref name="name"/> (the game's roll). The oldest cohort without the player goes when the list is full.
        /// </summary>
        public static Intake Ensure(RosterFile f, int course, int day, Func<string> name, int capacity = SeatsDefault)
        {
            int start = StartOf(day);
            Intake i = f.Find(course, start);
            if (i == null)
            {
                i = new Intake { Course = course, Start = start, Capacity = Math.Max(1, capacity) };
                f.Intakes.Add(i);
                Trim(f);
            }
            int want = Math.Min(Taken(f.Seed, course, start, i.Capacity, day), i.Capacity - (i.PlayerIn ? 1 : 0));
            int employees = 0;
            for (int k = 0; k < i.Students.Count; k++)
            {
                employees += i.Students[k].Employee ? 1 : 0;
            }
            while (i.Students.Count - employees < want && i.Students.Count < i.Capacity - (i.PlayerIn ? 1 : 0))
            {
                string who = name != null ? name() : null;
                if (string.IsNullOrEmpty(who))
                {
                    break;
                }
                i.Students.Add(new Student { Name = who, Gpa = GpaOf(f.Seed, course, start, i.Students.Count) });
            }
            return i;
        }

        private static void Trim(RosterFile f)
        {
            while (f.Intakes.Count > IntakesMost)
            {
                int drop = 0;
                for (int k = 0; k < f.Intakes.Count; k++)
                {
                    if (!f.Intakes[k].PlayerIn)
                    {
                        drop = k;
                        break;
                    }
                }
                f.Intakes.RemoveAt(drop);
            }
        }

        /// <summary>A student's grade point average, hundredths: most between 2.4 and 3.8, a few lower or at the top.</summary>
        public static int GpaOf(int seed, int course, int start, int index)
        {
            int a = TechRandom.Range(seed, 7811 + course, start, index, 160, 400);
            int b = TechRandom.Range(seed, 7812 + course, start, index, 240, 380);
            return (a + b * 2) / 3;
        }

        /// <summary>The player takes a seat; false when none is left.</summary>
        public static bool Join(Intake i)
        {
            if (i == null || i.PlayerIn || !CanJoin(i))
            {
                return false;
            }
            i.PlayerIn = true;
            return true;
        }

        /// <summary>
        /// One of the player's employees, named by the caller, follows the same course when the seeded chance says so and a seat is left.
        /// Returns the employee's name when one enrolled, else null. The same rule for every cohort.
        /// </summary>
        public static string EmployeeFollows(RosterFile f, Intake i, IList<string> employees, int day)
        {
            if (i == null || !i.PlayerIn || employees == null || employees.Count == 0 || Left(i) == 0)
            {
                return null;
            }
            int k = TechRandom.Range(f.Seed, 7821, i.Course, i.Start, 0, employees.Count - 1);
            string who = employees[k];
            for (int s = 0; s < i.Students.Count; s++)
            {
                if (i.Students[s].Name == who)
                {
                    return null;
                }
            }
            if (TechRandom.Mille(f.Seed, 7822, i.Course, i.Start) >= EmployeeChance)
            {
                return null;
            }
            i.Students.Add(new Student { Name = who, Employee = true, Gpa = GpaOf(f.Seed, i.Course, i.Start, 90 + k) });
            return who;
        }

        /// <summary>The tutor of a course, made when first asked for: years teaching, a rating that is mostly good.</summary>
        public static Tutor TutorFor(RosterFile f, int course, Func<string> name)
        {
            Tutor t = f.TutorOf(course);
            if (t != null)
            {
                return t;
            }
            string who = name != null ? name() : null;
            if (string.IsNullOrEmpty(who))
            {
                return null;
            }
            t = new Tutor
            {
                Course = course, Name = who, Years = TechRandom.Range(f.Seed, 7831, course, 0, 2, 28),
                Rating = (TechRandom.Range(f.Seed, 7832, course, 0, 30, 50) + TechRandom.Range(f.Seed, 7833, course, 0, 36, 50)) / 2
            };
            f.Tutors.Add(t);
            while (f.Tutors.Count > TutorsMost)
            {
                f.Tutors.RemoveAt(0);
            }
            return t;
        }

        /// <summary>Points a tutor adds to an exam score (or takes off): a little, from how the students rate them against 4.0.</summary>
        public static int ExamEdge(Tutor t) => t == null ? 0 : Math.Max(-EdgeMost, Math.Min(EdgeMost, (t.Rating - 40) / 4));

        // --- a skill floor -------------------------------------------------------------------------------------------------

        /// <summary>The skill a course asks for before enrolling, and the points of it; false for the many that ask none.</summary>
        public static bool Floor(CourseDef c, out int skill, out int points)
        {
            skill = -1;
            points = 0;
            if (c == null)
            {
                return false;
            }
            switch (c.Id)
            {
                case StudyCatalogue.Accounting:
                case StudyCatalogue.Finance:
                    skill = TalentCatalogue.Accounting;
                    points = 30;
                    return true;
                case StudyCatalogue.DataScience:
                    skill = TalentCatalogue.Analysis;
                    points = 30;
                    return true;
                case StudyCatalogue.ComputerScience:
                case StudyCatalogue.SoftwareEngineering:
                    skill = TalentCatalogue.Coding;
                    points = 20;
                    return true;
                default:
                    return false;
            }
        }

        public static bool FloorMet(CourseDef c, int have) => !Floor(c, out int _, out int need) || have >= need;

        // --- credits, grades and the average -------------------------------------------------------------------------------

        /// <summary>The credits a course gives when passed: more for a longer one, a degree or a postgraduate course the most.</summary>
        public static int CreditsOf(CourseDef c)
        {
            if (c == null)
            {
                return 0;
            }
            switch (c.Kind)
            {
                case CourseKind.Degree: return 120;
                case CourseKind.Postgrad: return 60;
                case CourseKind.Vocational: return 12 + c.Sessions / 3;
                case CourseKind.Certificate: return 6;
                default: return 3;
            }
        }

        /// <summary>The letter an exam score earns: A from 85, B+ from 80, B from 70, C+ from 60, C from 50, D from 40, else F.</summary>
        public static string LetterOf(int score)
        {
            return score >= 85 ? "A" : score >= 80 ? "B+" : score >= 70 ? "B" : score >= 60 ? "C+" : score >= 50 ? "C" : score >= 40 ? "D" : "F";
        }

        /// <summary>Hundredths of a grade point an exam score earns (A 4.0, B+ 3.3, B 3.0, C+ 2.3, C 2.0, D 1.0, F 0).</summary>
        public static int PointsOf(int score)
        {
            return score >= 85 ? 400 : score >= 80 ? 330 : score >= 70 ? 300 : score >= 60 ? 230 : score >= 50 ? 200 : score >= 40 ? 100 : 0;
        }

        /// <summary>The grade point average of a set of marks, hundredths, weighted by credits; -1 for no credits.</summary>
        public static int Gpa(IList<Mark> marks)
        {
            long points = 0, credits = 0;
            for (int i = 0; i < marks.Count; i++)
            {
                points += (long)marks[i].Points * marks[i].Credits;
                credits += marks[i].Credits;
            }
            return credits == 0 ? -1 : (int)((points + credits / 2) / credits);
        }

        /// <summary>The calendar quarter a day falls in, counted from the first: a year of the game's own length is four equal quarters.</summary>
        public static int QuarterOf(int day, int year)
        {
            int quarter = Math.Max(1, year / 4);
            return Math.Max(0, day) / quarter;
        }

        /// <summary>The average of each quarter that holds marks, in order, as (quarter, hundredths) pairs. Returns how many.</summary>
        public static int ByTerm(IList<Mark> marks, int year, List<int> quarters, List<int> averages)
        {
            quarters.Clear();
            averages.Clear();
            var seen = new SortedDictionary<int, List<Mark>>();
            for (int i = 0; i < marks.Count; i++)
            {
                int q = QuarterOf(marks[i].Start, year);
                if (!seen.TryGetValue(q, out List<Mark> list))
                {
                    seen[q] = list = new List<Mark>(4);
                }
                list.Add(marks[i]);
            }
            foreach (KeyValuePair<int, List<Mark>> pair in seen)
            {
                int g = Gpa(pair.Value);
                if (g >= 0)
                {
                    quarters.Add(pair.Key);
                    averages.Add(g);
                }
            }
            return quarters.Count;
        }

        /// <summary>The class by grade point average, best first,
        /// with the player placed among the others by theirs. Returns the player's place from 1.</summary>
        public static int Rank(Intake i, string you, int yourGpa, List<ClassLine> into)
        {
            into.Clear();
            if (i != null)
            {
                for (int k = 0; k < i.Students.Count; k++)
                {
                    into.Add(new ClassLine { Name = i.Students[k].Name, Gpa = i.Students[k].Gpa, Employee = i.Students[k].Employee });
                }
            }
            into.Add(new ClassLine { Name = you, Gpa = yourGpa, You = true });
            into.Sort((a, b) =>
            {
                int c = b.Gpa.CompareTo(a.Gpa);
                return c != 0 ? c : a.You ? -1 : b.You ? 1 : string.CompareOrdinal(a.Name, b.Name);
            });
            for (int k = 0; k < into.Count; k++)
            {
                if (into[k].You)
                {
                    return k + 1;
                }
            }
            return into.Count;
        }
    }
}
