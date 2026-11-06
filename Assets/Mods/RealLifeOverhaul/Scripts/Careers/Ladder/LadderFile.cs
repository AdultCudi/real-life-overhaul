using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a member of staff, or the player, asks for: the saved number of <see cref="Colleague.Want"/> and <see cref="LadderFile.Ask"/>.</summary>
    public enum StaffWant
    {
        None = 0, Raise = 1, Shift = 2, Equipment = 3, Insurance = 4
    }

    /// <summary>A matter waiting for the player's decision: the saved number of <see cref="LadderTask.Kind"/>.</summary>
    public enum TaskKind
    {
        Cover = 0,       // a gap on today's shift (a supervisor)
        Hire = 1,        // a post to fill (a store manager)
        Demand = 2,      // a member of staff asks for something (a supervisor or store manager)
        Escalation = 3,  // sent up from a store (a regional manager or an executive)
        Appoint = 4      // a store without a manager (a regional manager)
    }

    /// <summary>What an escalation is about: the saved number of <see cref="LadderTask.About"/>.</summary>
    public enum Concern
    {
        Gap = 0,     // a post left unfilled past the policy's days
        Morale = 1,  // the staff's morale under the threshold for days
        Budget = 2   // a demand over the store's budget
    }

    /// <summary>A decision on a task, as the Career app's buttons send it.</summary>
    public enum Verdict
    {
        Yes = 0, No = 1, Up = 2
    }

    /// <summary>One person of the company: staff, a supervisor, a store manager, a regional manager or an executive. Game-free.</summary>
    public sealed class Colleague
    {
        public int Id;
        public string GameId = string.Empty;   // the game's own member of staff at that store (AiBusinessEmployeeData.id); empty for one the company took on since
        public string Name = string.Empty;
        public int Store = -1;                 // the store they work at (its place in CityCompany.Stores); -1 head office
        public int Region = -1;                // a regional manager's region
        public int Skill;                      // 0 to 100, the game's skill value
        public int Wage;                       // cents an hour at the start's prices
        public int Morale;                     // 0 to 100
        public int Want;                       // StaffWant
        public int WantDay;                    // the day they asked
        public int Role;                       // Rung they hold: Frontline for staff
        public int Seat;                       // an executive's ExecSeat
        public int RaisedDay = -1000;          // the last day they had a raise
    }

    /// <summary>One store of the company in the city. Game-free.</summary>
    public sealed class Outlet
    {
        public string Street = string.Empty, Name = string.Empty;
        public int Number;
        public int Region;          // its place in the company's districts
        public int Needed;          // staff the store's work needs, from the game's own staffing of it
        public int Rota = 1;        // the store's schedule: 0 lean, 1 standard, 2 full
        public int GapDays;         // days a post has been unfilled in a row
        public int LowDays;         // days its staff's morale has been under the threshold in a row
        public int Spent;           // whole money of this week's budget spent, at the start's prices
        public int Results;         // 0 to 100: staffing and morale, last worked out
        public int Covered = -1;    // the day a gap on the shift was covered
        public int Pushed = -1;     // the day a gap was passed up to the store manager: filled sooner
        public int Present = -1;    // the last day the player worked a full day there as a manager
    }

    /// <summary>The company's policy: what every manager below its executives keeps to. Game-free.</summary>
    public sealed class CompanyPolicy
    {
        public int Floor = 35;      // the least skill of a new member of staff
        public int WageTop = 2200;  // the most a new member of staff is paid, cents an hour at the start's prices
        public int Band = 500;      // the most a manager's raise may be, basis points
        public int Budget = 600;    // whole money a week each store may spend on its staff's demands, at the start's prices
        public int Staffing = 1;    // the fullest schedule a store may run: 0 lean, 1 standard, 2 full
        public int Escalate = 3;    // days a gap or low morale is left before it is sent up
    }

    /// <summary>A matter waiting for the player. Game-free.</summary>
    public sealed class LadderTask
    {
        public int Id;
        public int Kind;            // TaskKind
        public int Store;           // the store it is about
        public int Person = -1;     // the person it is about, if any
        public int About;           // an escalation's Concern; a demand's StaffWant
        public int Day, Lapse;      // raised on, and the last day to decide
        public int Amount;          // a demand's cost, whole money at the start's prices
    }

    /// <summary>The city company the player works for, modelled: its stores, its people and its policy. Game-free.</summary>
    public sealed class CityCompany
    {
        public string Key = string.Empty;    // a rival's id, or "n:" and the business's name and type for one of the city's own chains
        public string Name = string.Empty;
        public string Rival = string.Empty;
        public int NextPerson = 1;
        public readonly CompanyPolicy Policy = new CompanyPolicy();
        public readonly List<Outlet> Stores = new List<Outlet>(CareerLadderRules.StoresMost);
        public readonly List<string> Districts = new List<string>(4);
        public readonly List<Colleague> People = new List<Colleague>(64);

        /// <summary>Everything modelled let go, for a company seeded again.</summary>
        public void Clear()
        {
            Key = Name = Rival = string.Empty;
            NextPerson = 1;
            Policy.Floor = 35;
            Policy.WageTop = 2200;
            Policy.Band = 500;
            Policy.Budget = 600;
            Policy.Staffing = 1;
            Policy.Escalate = 3;
            Stores.Clear();
            Districts.Clear();
            People.Clear();
        }

        public Colleague PersonOf(int id)
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
    }

    /// <summary>
    /// The player's place on the management ladder at the company their job is with, and the company modelled; in its own line of
    /// the save (LadderStore). Tied to the job by when it began and where, so a new job starts a new one. Game-free.
    /// </summary>
    public sealed class LadderFile
    {
        public int Hired;                  // the job it belongs to: its first day...
        public string Street = string.Empty, Employer = string.Empty;   // ...where...
        public int Number;
        public int Rung;                   // Rung
        public int Seat;                   // an executive's ExecSeat
        public int Aim;                    // the seat the player aims for (ExecSeat; None: the best fit of their skills)
        public int RungDay;                // the day the player came to the rung
        public int Score = CareerLadderRules.ScoreStart * 10;   // the performance score, tenths
        public bool Recommended;           // a superior's recommendation stands
        public bool Aiming = true;         // the player's name goes forward for the next rung
        public int Ask;                    // the player's own request waiting (StaffWant), None for none
        public int AskDay, AskWay;         // asked on; a shift's way (-1 earlier, 1 later)
        public int Prefer;                 // the job's start moved by so many hours (a shift request granted)
        public bool Equipped, Insured;     // equipment and insurance granted
        public int Handled, Lapsed;        // tasks decided and tasks left to lapse since the last review
        public int NextTask = 1, LastDay = -1, Week = -1;
        public readonly CityCompany Company = new CityCompany();
        public readonly List<LadderTask> Tasks = new List<LadderTask>(CareerLadderRules.TasksMost);

        public RungDef Def => LadderRanks.Of(Rung);

        public LadderTask TaskOf(int id)
        {
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (Tasks[i].Id == id)
                {
                    return Tasks[i];
                }
            }
            return null;
        }

        /// <summary>True if this record is the one of a job: the same first day and place.</summary>
        public bool Of(Job j) => j != null && j.Hired == Hired && j.Post.Street == Street && j.Post.Number == Number && j.Post.Employer == Employer;
    }
}
