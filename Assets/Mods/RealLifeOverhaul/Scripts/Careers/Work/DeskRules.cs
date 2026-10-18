using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The kind of the game's own station a profession works at (the game's EmployeeStationController and its kinds): the
    /// saved number is never kept.
    /// </summary>
    public enum StationKind
    {
        None = 0,
        Desk = 1,       // a desk computer with its chair (DesktopWorkstationController, WorkstationController, ComputerController)
        Register = 2,   // a till (CashRegisterController, a ticket booth's)
        Counter = 3,    // a counter of the tax office (IRSStationController)
        Chair = 4,      // a salon chair (HairdresserChairController)
        Locker = 5,     // a security guard's post (SecurityGuardLockerController)
        Machine = 6,    // a machine on a factory floor (FactoryAssemblyMachineController)
        Booth = 7       // a cloakroom (CoatCheckController)
    }

    /// <summary>
    /// What is fitted out for the player where an employer's building has no station of the game's that suits the job
    /// (FitOut's presets, FitKind): the saved number is never kept.
    /// </summary>
    public enum DeskPreset
    {
        None = 0,
        OfficeRow = 1,     // a row of desks, each with a computer and a chair, colleagues at some (an office's)
        Ward = 2,          // a nurses' station: two desks with computers and chairs, medical cabinets
        Classroom = 3,     // a teacher's desk with a computer, a chair and a cabinet
        ServiceDesk = 4,   // the council's service desks: a computer, a visitor's chair, filing
        BackOffice = 5     // a back office desk with a computer, a cabinet and a bin (a business's)
    }

    /// <summary>What the work panel at the player's desk says now (WorkActivity's labels for the mod's job).</summary>
    public enum DeskState
    {
        NoJob = 0,
        Open,         // the working day can be started (or carried on with): Start work
        StartsSoon,   // within the hour before it: Start work, as the game's job allows ahead of its shift
        NotYet,       // earlier than that
        TooLate,      // more than the late allowance after its hour, and nothing of it worked
        DayOff,       // no working day of the job today
        NotHired,     // the job begins on a later day
        Done,         // today's working day is counted already
        HomeDay,      // the office desk on a day worked from home
        OfficeDay     // the home computer on a day worked at the employer's
    }

    /// <summary>The home computer's button for the job, beside the game's Pass the time (Careers/ComputerButtons).</summary>
    public enum HomeWork
    {
        None = 0,     // no button: no job, or no working day of it at home today
        Start = 1,    // Start the working day (or carry on with it)
        Late = 2,     // Work late: today's day is worked, and overtime can be
        NotYet = 3,   // greyed, with the working hours: too early to begin it
        Why = 4       // greyed, with what the desk's panel says (DeskRules.Label): too late to begin it, or worked already
    }

    /// <summary>
    /// Where a professional job is worked and what the work panel says, in plain numbers (Careers/Work). The game's own job
    /// (El Gato Food Market's till) is worked at one of the employer's stations: the player goes in, clicks the station,
    /// and the game's work panel (PlayerActivity.WorkActivity) says whether the shift can be started. The mod's jobs are
    /// worked the same way at a station of the right kind for the profession, the same one every working day, chosen from
    /// the save's seed and the job; where the building has none, a desk is fitted out for the player (FitOut's presets).
    /// Game-free: EconomyCheck runs every rule.
    /// </summary>
    public static class DeskRules
    {
        public const int SoonMinutes = ProRules.ClockEarlyMinutes;   // the panel says "starts soon" and offers Start this long before the hour
        private const int PickSalt = 9201;                          // the dice of the station chosen

        private static readonly StationKind[] DeskOnly = { StationKind.Desk };
        private static readonly StationKind[] None = new StationKind[0];
        private static readonly StationKind[] CouncilKinds = { StationKind.Counter, StationKind.Desk };
        private static readonly StationKind[] SalesKinds = { StationKind.Desk, StationKind.Register };
        private static readonly StationKind[] TillKinds = { StationKind.Register };
        private static readonly StationKind[] SalonKinds = { StationKind.Chair, StationKind.Register };
        private static readonly StationKind[] BarKinds = { StationKind.Register, StationKind.Booth };
        private static readonly StationKind[] GuardKinds = { StationKind.Locker };
        private static readonly StationKind[] WarehouseKinds = { StationKind.Desk, StationKind.Machine, StationKind.Register };
        private static readonly StationKind[] TradeKinds = { StationKind.Machine, StationKind.Desk };

        /// <summary>
        /// The kinds of the game's stations a profession works at, the best first; empty where none of the game's suits it
        /// (a teacher: the school's computers are its students' study desks, so a teacher's desk is fitted out instead).
        /// </summary>
        public static StationKind[] Kinds(Profession p)
        {
            if (p == null)
            {
                return None;
            }
            switch (p.Id)
            {
                case ProCatalogue.Teacher: return None;
                case ProCatalogue.CouncilOfficer: return CouncilKinds;
                case ProCatalogue.Sales: return SalesKinds;
                case ProCatalogue.Chef: return TillKinds;
                case ProCatalogue.Stylist: return SalonKinds;
                case ProCatalogue.BarStaff: return BarKinds;
                case ProCatalogue.SecurityGuard: return GuardKinds;
                case ProCatalogue.Warehouse: return WarehouseKinds;
                case ProCatalogue.Electrician:
                case ProCatalogue.Plumber:
                case ProCatalogue.Carpenter: return TradeKinds;
                default: return DeskOnly;   // the office professions, management, and nursing at the hospital's computers
            }
        }

        /// <summary>What is fitted out for the player where the building has no station of the kinds above, by the workplace.</summary>
        public static DeskPreset Preset(Profession p)
        {
            if (p == null)
            {
                return DeskPreset.None;
            }
            switch (p.Place)
            {
                case Workplace.Office: return DeskPreset.OfficeRow;
                case Workplace.Hospital: return DeskPreset.Ward;
                case Workplace.School: return DeskPreset.Classroom;
                case Workplace.CityHall: return DeskPreset.ServiceDesk;
                default: return DeskPreset.BackOffice;
            }
        }

        /// <summary>A station's place among those that fit (in their fixed order), from the save's seed and the job; -1 for none.</summary>
        public static int Pick(int count, int seed, int job)
        {
            return count <= 0 ? -1 : TechRandom.Range(seed, PickSalt, job, 0, 0, count - 1);
        }

        /// <summary>The job's own number for the dice: its post and the day it began, the same on every load.</summary>
        public static int JobKey(Job j) => j == null ? 0 : j.Post.Id * 7919 + j.Hired;

        /// <summary>
        /// What the panel at a desk says at a minute of a day: at the employer's (<paramref name="atHome"/> false) or at the
        /// home computer. Minutes is what is left of the day to work; late if it starts after the late allowance.
        /// </summary>
        public static DeskState State(Job j, int day, int minute, bool atHome, out int minutes, out bool late)
        {
            minutes = 0;
            late = false;
            Profession p = j != null ? j.Def : null;
            if (p == null)
            {
                return DeskState.NoJob;
            }
            if (day < j.Hired)
            {
                return DeskState.NotHired;
            }
            if (!ProRules.WorkDay(p, day) || (j.NoticeEnd > 0 && day > j.NoticeEnd))
            {
                return DeskState.DayOff;
            }
            bool home = ProRules.FromHome((WorkMode)j.Work, day);
            if (home != atHome)
            {
                return home ? DeskState.HomeDay : DeskState.OfficeDay;
            }
            if (j.SettledDay >= day)
            {
                return DeskState.Done;
            }
            if (j.TodayDay == day && j.TodayMinutes > 0)
            {
                minutes = ProRules.Remaining(p, j, day, minute);
                return minutes > 0 ? DeskState.Open : DeskState.Done;
            }
            int start = p.Hour * 60;
            if (minute < start - ProRules.ClockEarlyMinutes)
            {
                return DeskState.NotYet;
            }
            if (!ProRules.CanStart(p, day, minute, out minutes, out late))
            {
                minutes = 0;
                late = false;
                return DeskState.TooLate;
            }
            return minute < start ? DeskState.StartsSoon : DeskState.Open;
        }

        /// <summary>True for the states whose panel offers Start work.</summary>
        public static bool Startable(DeskState s) => s == DeskState.Open || s == DeskState.StartsSoon;

        /// <summary>
        /// The home computer's button for the job, by what the work panel would say there (<see cref="State"/> at home):
        /// Start on a day that can be begun or carried on with, Work late once it is done and overtime can be worked;
        /// greyed with the hours before it, and with the panel's words when it is too late to begin or worked already;
        /// none on a day off, a day at the employer's, or before the job begins.
        /// </summary>
        public static HomeWork AtComputer(DeskState state, bool canLate)
        {
            switch (state)
            {
                case DeskState.Open:
                case DeskState.StartsSoon: return HomeWork.Start;
                case DeskState.NotYet: return HomeWork.NotYet;
                case DeskState.TooLate: return HomeWork.Why;
                case DeskState.Done: return canLate ? HomeWork.Late : HomeWork.Why;
                default: return HomeWork.None;
            }
        }

        /// <summary>
        /// The words of the panel's state: the game's own where it has them (its job's day off, shift not yet started, starts
        /// soon), the mod's for the rest. Null for a state that says nothing of its own (the day can be worked).
        /// </summary>
        public static string Label(DeskState s)
        {
            switch (s)
            {
                case DeskState.DayOff: return "playerhud_currentjob_day_off";
                case DeskState.NotYet: return "workpanel_business_shift_not_yet_started";
                case DeskState.StartsSoon: return "workpanel_business_shift_starts_soon";
                case DeskState.TooLate: return "reallife_work_why_not_hours";
                case DeskState.NotHired: return "reallife_desk_label_first";
                case DeskState.Done: return "reallife_work_why_done_today";
                case DeskState.HomeDay: return "reallife_desk_label_home";
                case DeskState.OfficeDay: return "reallife_work_why_not_today";
                case DeskState.NoJob: return "reallife_work_why_no_job";
                default: return null;
            }
        }

        /// <summary>The words for what the player's desk is, by the station's kind or the preset fitted out.</summary>
        public static string What(StationKind kind, DeskPreset preset)
        {
            switch (kind)
            {
                case StationKind.Desk: return "reallife_desk_what_desk";
                case StationKind.Register: return "reallife_desk_what_till";
                case StationKind.Counter: return "reallife_desk_what_counter";
                case StationKind.Chair: return "reallife_desk_what_chair";
                case StationKind.Locker: return "reallife_desk_what_locker";
                case StationKind.Machine: return "reallife_desk_what_machine";
                case StationKind.Booth: return "reallife_desk_what_booth";
            }
            switch (preset)
            {
                case DeskPreset.OfficeRow: return "reallife_desk_what_row";
                case DeskPreset.Ward: return "reallife_desk_what_ward";
                case DeskPreset.Classroom: return "reallife_desk_what_teacher";
                case DeskPreset.ServiceDesk: return "reallife_desk_what_service";
                case DeskPreset.BackOffice: return "reallife_desk_what_back";
                default: return "reallife_desk_what_desk";
            }
        }

        /// <summary>The words for the desk a job is expected to have before the building has been seen: its first kind, or its preset.</summary>
        public static string Expected(Profession p)
        {
            StationKind[] kinds = Kinds(p);
            return What(kinds.Length > 0 ? kinds[0] : StationKind.None, Preset(p));
        }

        /// <summary>
        /// The hour of the day the boss texts an absent player: an hour into the working day, as the game's own job checks on
        /// the hour once the shift is under way; -1 on a day with nothing to text about.
        /// </summary>
        public static int AwayHour(Profession p) => p == null ? -1 : Math.Min(23, p.Hour + 1);

        /// <summary>True if the boss texts at this hour: a working day of the job, begun, not yet counted, nothing of it worked.</summary>
        public static bool Away(Job j, int day, int hour)
        {
            Profession p = j != null ? j.Def : null;
            return p != null && hour == AwayHour(p) && day >= j.Hired && ProRules.WorkDay(p, day) && j.SettledDay < day && !(j.NoticeEnd > 0 && day > j.NoticeEnd)
                   && !(j.TodayDay == day && j.TodayMinutes > 0) && j.OffUntil < day;
        }
    }
}
