using System.Collections.Generic;

namespace RealLifeOverhaul.Health
{
    /// <summary>What ails the player now, as Health follows it: Home's own illness mirrored, or an injury of Health's own.</summary>
    public sealed class HealthCase
    {
        public Condition Kind;
        public int Began;          // the game day it began
        public int Until;          // an injury: the day it is healed (an illness runs by Home's hours)
        public bool Treated;       // seen to: a prescription, a dressing, the hospital
        public bool Serious;       // needs the hospital
        public bool Work;          // an accident at work: the employer's insurer pays the care, and its sick days are outside the year's count
        public int Neglect;        // untreated days without rest, running
        public int LastDay;        // the last day its day was lived

        public bool Open => Kind != Condition.None;

        public void Clear()
        {
            Kind = Condition.None;
            Began = Until = Neglect = LastDay = 0;
            Treated = Serious = Work = false;
        }
    }

    /// <summary>The player's own health plan.</summary>
    public sealed class HealthPolicy
    {
        public Plan Plan;
        public int Since;          // the day it was taken
        public int NextDue;        // the day the next month's premium is taken
        public int Missed;         // premiums missed running
        public int Year = -1;      // the year (of the save) PaidYear counts
        public long PaidYear;      // what it has paid out this year, against the plan's cap
        public readonly List<int> ClaimDays = new List<int>(HealthCatalogue.ClaimsKept);   // the days of the claims the next premium is priced on
    }

    /// <summary>A stay in hospital under way: it is in the save from the admission to the discharge, so a load in the middle goes on.</summary>
    public sealed class HealthStay
    {
        public bool Active;
        public int From;           // minutes of the save
        public int Until;          // minutes of the save: the discharge
        public int Days;           // days billed
        public Condition Kind;
        public bool GameNight;     // its first night was the game's own (billed by the game)

        public void Clear()
        {
            Active = GameNight = false;
            From = Until = Days = 0;
            Kind = Condition.None;
        }
    }

    /// <summary>What is owed for care that could not be paid on the day.</summary>
    public sealed class HealthDebt
    {
        public long Owed;
        public int PerDay;         // the instalment
        public int Late;           // days running with nothing taken
        public int Claim;          // the court's number once the lender filed; 0 none
        public int LastDay;        // the last day an instalment was tried
        public long Paid;          // paid off in all
    }

    /// <summary>The player's sick leave at their job, a year at a time.</summary>
    public sealed class LeaveBook
    {
        public int Year = -1;      // the year PaidUsed and Claims count
        public int PaidUsed;       // paid sick days taken
        public int SpellLast;      // the last sick working day settled
        public int SpellDays;      // sick working days running, up to SpellLast
        public bool LastCovered;   // what SpellLast was settled as
        public int ClaimDay;       // the day the player called in sick while well; 0 none
        public int Claims;         // such days this year
        public int Noticed;        // ...of which the employer saw through
        public int NoteFrom, NoteUntil;   // a hospital's note covers the working days from one day to another
        public int PaidDay;        // the last day sick pay was paid for
        public long PaidMoney;     // sick pay this year
        public int PrevYear = -1;  // the year before Year, while its paid days can still be counted against (a day settled after the year turned)
        public int PrevPaidUsed;   // ...and the paid days it had used
        public int CaseFrom;       // the span of days the player's last case ran, so a day settled later is judged by what was true on it: its first day, 0 none
        public int CaseTo;         // ...the last day it was seen open
        public bool CaseWork;      // ...an accident at work
        public int CareFrom;       // ...the day care began on it (treated, or in hospital); 0 not yet
        public int WorkTold;       // the accident at work (its first day) whose pay stopping has been said
    }

    /// <summary>An appointment with the doctor.</summary>
    public sealed class HealthAppointment
    {
        public int Day;            // 0: none
        public int Hour;
        public bool Kept;
        public string Street = string.Empty;   // the clinic's address as the game writes it
        public int Number;

        public bool Booked => Day > 0;

        public void Clear()
        {
            Day = Hour = Number = 0;
            Kept = false;
            Street = string.Empty;
        }
    }

    /// <summary>One of the player's staff off with something Health gave them: off through the game's own sick days, a day at a time.</summary>
    public sealed class StaffCase
    {
        public string Id = string.Empty;
        public int From, Until;    // the first and the last day off
    }

    /// <summary>A rival tycoon's illness.</summary>
    public sealed class RivalHealth
    {
        public Condition Kind;
        public int Day;            // the day it began
        public int Until;          // the last day of it
        public bool Serious;
        public long Bill;          // what the hospital took of their worth
        public int LastDay;        // the last day drawn for them
    }

    /// <summary>The city's people: the day's pass and where it has got to, who is ill, and the month's sums.</summary>
    public sealed class PopHealth
    {
        public int Day;            // the day the pass under way is for
        public int Cursor = -1;    // the next person's slot; -1 no pass under way
        public int Pending;        // a day whose pass waits for the one under way to finish; 0 none
        public int IllNow;         // people ill today
        public readonly int[] Ends = new int[HealthCatalogue.CourseLongest + 2];   // by day (modulo): the cases that end on it
        public int Month = -1;
        public int Cases, Serious, LostDays;
        public long LostWages, Bills, Debt;
        public int LastCases, LastSerious, LastLostDays;
        public long LastLostWages, LastBills, LastDebt;
    }

    /// <summary>A hospital stay of a rival's the paper has still to print.</summary>
    public struct HealthNote
    {
        public int Day;
        public int Rival;
        public Condition Kind;
    }

    /// <summary>Everything Health keeps of a save: one line of modData (HealthStore). Game-free.</summary>
    public sealed class HealthState
    {
        public const int Rivals = 3;   // RivalCatalogue.TycoonCount

        public int Seed;
        public int LastDay;
        public int LastHour = -1;
        public int GameStays = -1;       // the game's count of hospital nights when last looked at; -1 not yet
        public int Year = -1;            // the year BillsYear and ClaimsYear count
        public long BillsYear, ClaimsYear;
        public long LastBill, LastClaim; // the latest bill and what a plan paid of it
        public int LastBillDay;
        public int Injuries;             // injuries so far: a draw's own number
        public int StaffDay;             // the last day the staff were drawn for
        public int DrawDay;              // the day Home's chances have been drawn on
        public readonly int[] Draws = new int[HealthCatalogue.DrawSlots];   // by Home's chance: how many times it was drawn that day (the next draw is the next number)

        public readonly HealthCase Case = new HealthCase();
        public readonly HealthPolicy Policy = new HealthPolicy();
        public readonly HealthStay Stay = new HealthStay();
        public readonly HealthDebt Debt = new HealthDebt();
        public readonly LeaveBook Leave = new LeaveBook();
        public readonly HealthAppointment Appointment = new HealthAppointment();
        public readonly List<StaffCase> Staff = new List<StaffCase>(8);
        public readonly RivalHealth[] RivalCases = new RivalHealth[Rivals];
        public readonly PopHealth Pop = new PopHealth();
        public readonly List<HealthNote> Notes = new List<HealthNote>(2);

        public HealthState()
        {
            for (int i = 0; i < RivalCases.Length; i++)
            {
                RivalCases[i] = new RivalHealth();
            }
        }
    }
}
