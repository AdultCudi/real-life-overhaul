using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>Why a professional is going: the saved number of <see cref="Profile.Leaving"/>.</summary>
    public enum LeaveKind
    {
        None = 0, Resigned = 1, LaidOff = 2, Dismissed = 3, Poached = 4, BurntOut = 5
    }

    /// <summary>One professional's file: the game's employee by its id, or one on the mod's payroll ("pro:n"). Skills in thousandths of a point.</summary>
    public sealed class Profile
    {
        public string Id = string.Empty;
        public int Role, Level;
        public int A, B;                 // the role's two specialisms, thousandths of a point
        public int Main;                 // the main skill, thousandths of a point; for the game's employees read from the game each day
        public int YearsDays;            // experience before joining, in days of a 365-day year
        public int School;
        public int Certs;                // one bit per TalentCatalogue.Certs
        public int Morale = TalentCatalogue.MoraleStart, Burnout;   // thousandths
        public int Joined, LevelDay, ReviewDay;   // days
        public int Rating;               // the last review, 1 to 5; 0 before the first
        public int LowRuns;              // ratings of 1 in a row
        public int Work;                 // Arrangement
        public bool NonCompete;
        public int NoticeEnd;            // the day they go; 0: not going
        public int Leaving;              // LeaveKind
        public int CourseSkill = -1, CourseEnd;   // a course outside working hours: the slot (0 A, 1 B) and the day it ends
        public int Exam = -1;            // the certificate whose exam they sit when the course day ends; -1 none
        public int RaiseAsk;             // basis points asked for at the last review, 0 none
        public int AskEnd;               // the day an unanswered request for a rise counts as refused
        public int RecognisedDay = -1000;   // the day of the last rise or promotion
        public int PoachDay, PoachBp;    // a rival's offer: the day it lapses and what it adds
        public string PoachBy = string.Empty;
        public string Place = string.Empty;   // the business they work at (a place key)
        public int Options;              // share options granted (startups)
        public bool Record;              // a past conviction (found by a background check)
        // a professional on the mod's own payroll only
        public string Name = string.Empty;
        public int Wage;                 // cents an hour
        public bool Female;
        public int Age;
        public bool Sick;
        // the hours worked, counted on the game's hour (the game's own employees): on a shift, at a business that is open
        public int WorkDay = -1, WorkHours;   // the day being counted and its hours so far
        public int PrevDay = -1, PrevHours;   // the day counted before it and its hours
        // read from the game each day, never saved
        public int GameWage;             // cents an hour the game pays (or the mod's wage)
        public int Going;                // the going rate for the role and skill, cents an hour
        public int WeekHours;            // hours worked this week, as the game counts them
        public bool Seen;

        public bool Payroll => Id.StartsWith("pro:", System.StringComparison.Ordinal);

        public RoleDef Def => TalentCatalogue.Role(Role);

        public bool Has(int cert) => cert >= 0 && cert < 31 && (Certs & (1 << cert)) != 0;

        public bool OnCourse(int day) => CourseSkill >= 0 && day < CourseEnd;

        /// <summary>A specialism by its slot (0 the role's first, 1 its second).</summary>
        public int Slot(int slot) => slot == 0 ? A : B;

        /// <summary>The score of a specialism of the catalogue in whole points, or -1 if the role has no such one.</summary>
        public int ScoreOf(int skill)
        {
            RoleDef r = Def;
            return skill == r.SkillA ? A / 1000 : skill == r.SkillB ? B / 1000 : -1;
        }
    }

    /// <summary>A move of one of the player's businesses to another building that the Tech parts followed: a part not running then follows it when it starts. Game-free.</summary>
    public sealed class MoveRecord
    {
        public string From = string.Empty, To = string.Empty;   // place keys
        public int Day, Hour;                                   // when the game's moving company moved it
        public int Parts;                                       // the parts that have followed it, MoveRules.Part*
        public bool Whole;      // the firm's own record went with it (its cap table then takes the place of one at the new building)
        public bool Replaced;   // a firm of the player's at the new building was closed, its business replaced by the move
    }

    /// <summary>Everything the people part keeps in a save: the professionals' files and the numbers it hands out. Game-free.</summary>
    public sealed class TalentFile
    {
        public int Seed;
        public int FirstDay;
        public int LastDay = -1;         // the last day worked through
        public int NextPro = 1;          // the next id of the mod's own payroll
        public readonly List<Profile> People = new List<Profile>(64);
        public readonly List<MoveRecord> Moves = new List<MoveRecord>(4);   // businesses the moving company moved, and which parts have followed (MoveRules)

        public Profile Of(string id)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Id == id)
                {
                    return People[i];
                }
            }
            return null;
        }

        /// <summary>Every file by its id (the first of an id, as <see cref="Of"/> finds it), so a read of 600 people asks a dictionary, not a pass each.</summary>
        public void Index(Dictionary<string, Profile> into)
        {
            into.Clear();
            for (int i = 0; i < People.Count; i++)
            {
                Profile p = People[i];
                if (p != null && p.Id != null && !into.ContainsKey(p.Id))
                {
                    into.Add(p.Id, p);
                }
            }
        }
    }
}
