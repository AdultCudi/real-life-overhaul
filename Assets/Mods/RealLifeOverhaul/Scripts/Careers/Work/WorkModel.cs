using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Where an application stands: the saved number of <see cref="Application.Stage"/>.</summary>
    public enum ApplyStep
    {
        Sent = 0,         // with the employer: an answer in a day or two
        Interview = 1,    // invited: InterviewDay at InterviewHour
        Offered = 2,      // an offer to answer by Lapse
        Accepted = 3,     // starts on StartDay
        Declined = 4,     // the player said no
        Rejected = 5,     // the employer said no
        Lapsed = 6,       // nobody answered in time, or the interview was missed
        Withdrawn = 7,
        Closed = 8        // the employer closed, was bought or lost its building before the answer, the interview, the offer or the start
    }

    /// <summary>How a vacancy reached the player: the saved number of <see cref="Opening.Via"/>.</summary>
    public enum Via
    {
        Board = 0,        // the city's vacancies, as the Career app lists them
        JobsBoard = 1,    // a rival's ad on the jobs board the city shares (Tech/Hiring)
        Agency = 2,       // the recruitment office the player has registered with
        Referral = 3,     // a friend at the employer put the player's name forward
        Headhunter = 4    // an executive search firm came to the player
    }

    /// <summary>Why a job ended: the saved number of <see cref="Job.Leaving"/> and <see cref="Spell.Leaving"/>.</summary>
    public enum LeaveReason
    {
        None = 0, Resigned = 1, Dismissed = 2, Redundant = 3, Poached = 4, Retired = 5, WalkedOut = 6, SpunOut = 7, Misconduct = 8,
        EmployerClosed = 9   // the employer closed, was bought or lost its building: a lay-off with pay and a good reference, never a quit or a sacking
    }

    /// <summary>A vacancy: one post at one employer. Game-free.</summary>
    public sealed class Opening
    {
        public int Id;
        public int Prof, Rank;
        public int Kind;                      // EmployerKind
        public string Employer = string.Empty;   // its name as the city knows it
        public string Street = string.Empty;  // where the work is
        public int Number;
        public string Rival = string.Empty;   // the game's rival whose business it is, by id; empty for none
        public int Pay;                       // cents an hour at the start's prices
        public int Work;                      // WorkMode offered
        public int From, To;                  // the days it is open
        public int Via;
        public int Family = -1, Level = -1;   // the jobs board's ad it is, if it is one
        public string Lead = string.Empty;    // who of the people the player knows passed it on (Network's job lead); empty for none
        public int Shift;                     // its working week fitted to the workplace's opening hours (OpenHoursRules.Pack); 0 not fitted yet: the profession's own

        private Profession _fitted;           // the profession on that week, made again only when the week changes
        private int _fittedOf;

        public bool Open(int day) => day >= From && day <= To;

        /// <summary>The profession as this post works it: on its own fitted week, so every rule that reads a working day's hours reads the post's.</summary>
        public Profession Def
        {
            get
            {
                Profession p = ProCatalogue.Find(Prof);
                if (p == null || Shift == 0 || Shift == OpenHoursRules.Pack(p.Days, p.Hour, p.Hours))
                {
                    return p;
                }
                if (_fitted == null || _fittedOf != Shift || _fitted.Id != p.Id)
                {
                    _fitted = p.WithShift(OpenHoursRules.DaysOf(Shift), OpenHoursRules.HourOf(Shift), OpenHoursRules.HoursOf(Shift));
                    _fittedOf = Shift;
                }
                return _fitted;
            }
        }

        public void CopyTo(Opening o)
        {
            o.Id = Id;
            o.Prof = Prof;
            o.Rank = Rank;
            o.Kind = Kind;
            o.Employer = Employer;
            o.Street = Street;
            o.Number = Number;
            o.Rival = Rival;
            o.Pay = Pay;
            o.Work = Work;
            o.From = From;
            o.To = To;
            o.Via = Via;
            o.Family = Family;
            o.Level = Level;
            o.Lead = Lead;
            o.Shift = Shift;
        }

        /// <summary>True for the same post at the same place.</summary>
        public bool Same(Opening o) => o != null && o.Prof == Prof && o.Rank == Rank && o.Employer == Employer && o.Street == Street && o.Number == Number;
    }

    /// <summary>One application of the player's. Game-free.</summary>
    public sealed class Application
    {
        public readonly Opening Post = new Opening();
        public int Day;               // sent on
        public int Stage;             // ApplyStep
        public int Answer;            // the day the employer answers
        public int InterviewDay, InterviewHour;
        public int Score;             // how the employer rates the player, 0 to 100 (worked out when it was sent)
        public int Offer;             // cents an hour at the start's prices
        public int Asks;              // times the player asked for more
        public int Lapse;             // the day an invitation or offer lapses
        public int StartDay;
        public int Refused = -1;      // the day the employer turned the player down; -1 not turned down, or not known (ProRules.RefusedDay)

        public ApplyStep At => (ApplyStep)Stage;

        public bool Live => Stage <= (int)ApplyStep.Accepted;

        /// <summary>The employer turns the player down today: the day is kept, so the same post there waits <see cref="ProRules.ReapplyDays"/>.</summary>
        public void Reject(int day)
        {
            Stage = (int)ApplyStep.Rejected;
            Refused = day;
        }

        /// <summary>True while this turned-down application keeps the player from applying to the same post at the same employer again.</summary>
        public bool Bars(int day) => Stage == (int)ApplyStep.Rejected && Refused >= 0 && day < ProRules.ReapplyFrom(Refused);
    }

    /// <summary>The player's job now. Game-free.</summary>
    public sealed class Job
    {
        public readonly Opening Post = new Opening();
        public int Hired, RankDay;
        public int Pay;               // cents an hour at the start's prices, raises included
        public int Work;              // WorkMode
        public int Boss;              // the boss's manner: BossKind
        public int SettledDay = -1;   // the last working day counted (worked or missed)
        public int TodayDay = -1, TodayMinutes;   // minutes worked on the working day TodayDay
        public int ClockIn = -1;      // the minute of that day the first stint began
        public int Due, Worked, Late, Absent;     // working days since the last review: due, worked, late, missed
        public int WeekOf = -1, WeekMinutes;      // the week (day / 7) and the minutes worked in it
        public int Overtime;          // minutes over the working day since the last review
        public int Burnout;           // thousandths
        public int Rating, ReviewDay, LowRuns, Warnings;
        public int Raises;            // raises at this rank
        public int Politics = -1, PoliticsDay, Favour;   // an office matter waiting for an answer, the day it lapses; standing with colleagues
        public int Options, VestDay;  // share options granted (a tech company), and the day the next quarter of them is the player's
        public bool NonCompete, Exclusive;   // a restraint clause; side work not allowed
        public int NoticeEnd;         // the last day before leaving; 0: not leaving
        public int Leaving;           // LeaveReason
        public bool Declared;         // a conflict of interest declared to the employer
        public int ConflictSeen;      // the day a conflict was last looked into
        public long Earned;           // cents earned here
        public int Bonus;             // whole money of a bonus awarded and not yet paid
        public int AskDay = -1000;    // the last time the player asked for a rise between reviews
        public int OffUntil;          // signed off for burnout until this day: its working days are excused
        public int ShutDay = -1;      // a working day the workplace was shut at its start: excused, never a warning

        public Profession Def => Post.Def;

        public ProRank Rank => Def.Rank(Post.Rank);
    }

    /// <summary>A job of the past, as a CV lists it. Game-free.</summary>
    public sealed class Spell
    {
        public int Prof, Top, Kind;
        public string Employer = string.Empty;
        public int From, To;
        public int Reference;         // 0 none, 1 poor, 2 fair, 3 good
        public int Leaving;           // LeaveReason
        public long Earned;
    }

    /// <summary>The player's working life in the save: vacancies seen, applications, the job now and before; in the careers line (CareersStore). Game-free.</summary>
    public sealed class WorkFile
    {
        public int NextId = 1, LastDay = -1, OpeningsDay = -1;
        public readonly List<Opening> Openings = new List<Opening>(32);
        public readonly List<Application> Applications = new List<Application>(16);
        public Job Job;
        public readonly List<Spell> History = new List<Spell>(16);
        public int AgencyUntil;       // registered with the recruitment office until this day
        public long Earned;           // cents earned in every job
        public int DaysWorked;        // working days worked in every job
        public long Pension;          // cents in the pension pot
        public int BenefitUntil;      // unemployment benefit is paid until this day
        public int BenefitWeek;       // ...this much a week, whole money at the start's prices
        public int Retired;           // the day the player retired; 0 working
        public int HeadhuntDay;       // the last day a search firm came
        public int BenefitPaid;       // the last day benefit was paid
        public bool RetiredEarly;     // retired before the age: the pension pays less
        public bool StartTold;        // the Career app has said once what the first step to an offer is
        public int AgencyTold;        // the last day of a registration whose ending has been told (ProRules.AgencyEnds); 0 none
        public readonly List<Contract> Contracts = new List<Contract>(8);
        public readonly Practice Practice = new Practice();
        public int FounderDay, FounderProf = -1, FounderTop;   // the player left a profession to found a company
        public string FounderFrom = string.Empty;
        public int RestraintUntil, RestraintProf = -1, RestraintColleagues;   // a restraint clause binding the player after leaving
        public string RestraintEmployer = string.Empty;
        public long RestraintWeek;   // the last week's pay there, cents at the start's prices
        public int ClaimNumber;      // the claim filed over it (Justice), 0 none
        public bool RestraintTested; // the former employer has looked at what the player did
        public int ColleaguesUntil, ColleaguesCount, ColleaguesProf = -1, ColleaguesLevel;   // former colleagues waiting to join the player's firm
        public int PracticeAfter = -1;   // a practice of this profession to open when the notice ends
        public int LaidOffDay;        // the day an employer's closing let the player go (ClosureRules); 0 never
        public string LaidOffFrom = string.Empty;   // ...and that employer's name as the vacancies kept it

        public const int OpeningsMost = 40, ApplicationsMost = 24, HistoryMost = 16;

        public int Next() => NextId++;

        public Opening OpeningOf(int id)
        {
            for (int i = 0; i < Openings.Count; i++)
            {
                if (Openings[i].Id == id)
                {
                    return Openings[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The application to a post: its live one, else the newest. A post applied to again after a withdrawal has two, and
        /// the oldest first sent every interview, offer and withdrawal to the withdrawn one (a player's report of 25 Oct).
        /// </summary>
        public Application ApplicationOf(int id)
        {
            Application newest = null;
            for (int i = Applications.Count - 1; i >= 0; i--)
            {
                if (Applications[i].Post.Id != id)
                {
                    continue;
                }
                if (Applications[i].Live)
                {
                    return Applications[i];
                }
                newest = newest ?? Applications[i];
            }
            return newest;
        }

        /// <summary>A live application for the same post, or null.</summary>
        public Application Applied(Opening o)
        {
            for (int i = 0; i < Applications.Count; i++)
            {
                if (Applications[i].Live && Applications[i].Post.Same(o))
                {
                    return Applications[i];
                }
            }
            return null;
        }

        /// <summary>The latest no still barring the same post (matched on the post, not the vacancy); null if the player may apply. One pass over the applications.</summary>
        public Application TurnedDown(Opening o, int day)
        {
            Application last = null;
            for (int i = 0; i < Applications.Count; i++)
            {
                Application a = Applications[i];
                if (a.Bars(day) && a.Post.Same(o) && (last == null || a.Refused > last.Refused))
                {
                    last = a;
                }
            }
            return last;
        }

        public void Remember(Spell s)
        {
            History.Add(s);
            while (History.Count > HistoryMost)
            {
                History.RemoveAt(0);
            }
        }
    }
}
