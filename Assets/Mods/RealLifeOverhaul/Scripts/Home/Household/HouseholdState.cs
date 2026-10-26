using System.Collections.Generic;

namespace RealLifeOverhaul.Household
{
    /// <summary>One person on the household staff, as taken on: who they are, the wage agreed, and how their visits have gone.</summary>
    public sealed class StaffMember
    {
        public StaffKind Kind;
        public string Name;
        public bool Female;
        public int Age;
        public int Seed;           // of their look
        public int Skill;          // 1..100
        public int Hourly;         // cents an hour, agreed when taken on; 0 for the mechanic, who is paid by the job
        public int HiredDay;
        public int Visits;         // visits paid for
        public int Missed;         // visits in a row that could not be paid
        public int PaidTotal;      // whole money paid them so far
        public int OnDay = -1;     // the day of the visit under way, paid for; -1 between visits
        public int Dish = -1;      // that visit's dish (an index into HouseholdRules.CookDishes); -1 for none
        public int Charged;        // whole money that visit cost, wages and shopping, for the day's note
        public string HomeStreet;  // the home of the player's they work at (its street id); a member always has one
        public int HomeNumber;

        public bool WorksAt(string street, int number) => !string.IsNullOrEmpty(street) && HomeNumber == number && HomeStreet == street;

        public static StaffMember From(in StaffCandidate c, int hourly, int day, string street, int number)
        {
            return new StaffMember
            {
                Kind = c.Kind, Name = c.Name, Female = c.Female, Age = c.Age, Seed = c.Seed, Skill = c.Skill, Hourly = hourly, HiredDay = day, HomeStreet = street,
                HomeNumber = number
            };
        }
    }

    /// <summary>A home of the player's by its address, game-free: the street's id and the number.</summary>
    public readonly struct HomeKey
    {
        public readonly string Street;
        public readonly int Number;

        public HomeKey(string street, int number)
        {
            Street = street;
            Number = number;
        }

        public bool Valid => !string.IsNullOrEmpty(Street);

        public bool Is(string street, int number) => Valid && Number == number && Street == street;
    }

    /// <summary>The household staff of one save: the people, each with the home they work at, and the day's note so far. Game-free.</summary>
    public sealed class HouseholdState
    {
        public int Seed;                  // the save's own: every candidate comes from it
        public int LastStamp;             // day * 24 + hour of the last hour gone through
        public readonly List<StaffMember> Staff = new List<StaffMember>(HouseholdRules.Most);
        public readonly int[] Rolls = new int[HouseholdRules.Roles.Length + 1];   // by kind: candidates put forward so far
        public int NoteDay;               // the day the note below belongs to
        public string Note;               // the day's lines so far, in the player's language; null for none

        public bool Any => Staff.Count > 0;

        /// <summary>True if somebody of the kind works at that home.</summary>
        public bool Has(StaffKind kind, string street, int number) => HouseholdRules.FindAt(Staff, kind, street, number) != null;

        /// <summary>True if somebody of the kind works at any home of the player's.</summary>
        public bool HasAnywhere(StaffKind kind) => HouseholdRules.Find(Staff, kind) != null;

        public int RollsOf(StaffKind kind)
        {
            int k = (int)kind;
            return k > 0 && k < Rolls.Length ? Rolls[k] : 0;
        }

        public void NextRoll(StaffKind kind)
        {
            int k = (int)kind;
            if (k > 0 && k < Rolls.Length && Rolls[k] < int.MaxValue)
            {
                Rolls[k]++;
            }
        }

        /// <summary>A line for the day's note; a new day begins a new note.</summary>
        public void AddNote(int day, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            if (NoteDay != day)
            {
                NoteDay = day;
                Note = null;
            }
            Note = Note == null ? line : Note + " " + line;
        }

        /// <summary>The day's note, taken: null if there is none for that day.</summary>
        public string TakeNote(int day)
        {
            string note = NoteDay == day ? Note : null;
            Note = null;
            return note;
        }

        public void Remove(StaffMember m)
        {
            Staff.Remove(m);
        }
    }
}
