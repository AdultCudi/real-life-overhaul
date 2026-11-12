using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A student of an intake who is not the player: a named person of the city, or an employee of the player's.</summary>
    public sealed class Student
    {
        public string Name = string.Empty;   // from the game's own name roll, kept so the same person stays
        public int PersonId;                 // the person of the city's people pool (0: a name of the catalogue); saved at the end of the cohort's record
        public int Gpa;                      // hundredths of a grade point, 0 to 400
        public bool Employee;                // an employee of the player's, enrolled in the same intake
    }

    /// <summary>One cohort of one course: the seats, who holds them, and whether the player is one of them. Game-free.</summary>
    public sealed class Intake
    {
        public int Course;
        public int Start;                    // the first day of the cohort (RosterRules.StartOf)
        public int Capacity = RosterRules.SeatsDefault;
        public bool PlayerIn;                // the player holds a seat
        public readonly List<Student> Students = new List<Student>(RosterRules.SeatsDefault);
    }

    /// <summary>The tutor of a course at its school: an employee of the school who teaches it. Game-free.</summary>
    public sealed class Tutor
    {
        public int Course;
        public string Name = string.Empty;
        public int PersonId;                 // the person of the people pool (0 none)
        public int Years;                    // years teaching
        public int Rating;                   // students' rating in tenths, 10 to 50
    }

    /// <summary>Everything the campus keeps of the people in its classes, in its own line of the save (<see cref="RosterStore"/>). Game-free.</summary>
    public sealed class RosterFile
    {
        public int Seed = 1;
        public readonly List<Intake> Intakes = new List<Intake>(RosterRules.IntakesMost);
        public readonly List<Tutor> Tutors = new List<Tutor>(RosterRules.TutorsMost);

        public Intake Find(int course, int start)
        {
            for (int i = 0; i < Intakes.Count; i++)
            {
                if (Intakes[i].Course == course && Intakes[i].Start == start)
                {
                    return Intakes[i];
                }
            }
            return null;
        }

        public Tutor TutorOf(int course)
        {
            for (int i = 0; i < Tutors.Count; i++)
            {
                if (Tutors[i].Course == course)
                {
                    return Tutors[i];
                }
            }
            return null;
        }
    }
}
