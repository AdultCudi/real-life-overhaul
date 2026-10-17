using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rota planner's figures: the game's hard limits, each named where the game keeps it, and its own weights.</summary>
    public static class RotaCatalogue
    {
        public const int Days = 7, Hours = 24, Cells = Days * Hours;

        public const int ShiftCap = 12;     // hours in one shift at most (ScheduleHelper.ShiftLengthCap)
        public const int DayCap = 14;       // hours in a day; more is overwork, which may bring a sick day (ScheduleHelper.GetOverworkedDays)

        // What an unmet demand takes from satisfaction each hour, in tenths of a point, by priority low, medium, high (JobDemand.Impact).
        public static readonly int[] DemandWeight = { 2, 3, 5 };

        /// <summary>A demand's weight by its priority, a priority out of range taken as the nearest end.</summary>
        public static int Weight(int priority)
        {
            return DemandWeight[priority < 0 ? 0 : priority > PriorityHigh ? PriorityHigh : priority];
        }

        public const int PriorityHigh = 2;  // once the game's auto fill relaxes, only demands of this priority stay (ScheduleAutoFiller skipOptional)

        public const int StationsMost = 64;     // one bit each
        public const int WorkersMost = 120;
        public const int DemandsMost = 8;       // of one employee
        public const int ReasonsMost = 24;      // unmet demands and gaps told to the player

        public const int BlocksMost = 2;        // blocks of opening hours advised for a day (the game takes any number)
        public const int BlockLeast = 2;        // hours in an advised block: a shift worth coming in for

        // What an hour at a station is worth to the planner where no customers say it.
        public const int ValueFloor = 2;        // the smallest crew in an hour without customers: the game warns of an open business with nobody at work
        public const int ValueSupport = 1;      // a station that serves nobody itself (a guard), and an hour of cleaning
        public const int ValueWork = 10;        // a work station where no customers come (a factory, a headquarters)

        public const int FullWeek = 40;         // hours a week counted for somebody with no hours demand when the people to hire are counted
        public const int BooksDays = 7;         // days of a business's books that say what a customer brings...
        public const int BooksCustomers = 20;   // ...if they hold at least so many customers; otherwise its prices say it
        public const int ImproveSteps = 24000;  // moves the improving pass may try: a fixed budget, so the same rota every time
    }

    /// <summary>The game's two kinds of shift (WorkShiftType) as the planner's numbers.</summary>
    public static class RotaKind
    {
        public const byte Work = 0, Cleaning = 1;
    }

    /// <summary>What a group of stations does for the business.</summary>
    public static class RotaGroupKind
    {
        public const byte Service = 0;      // each one staffed serves so many customers an hour
        public const byte Support = 1;      // wanted while the business is open; serves nobody itself
        public const byte Cleaning = 2;
    }

    /// <summary>The game's rota demands, one for each of its classes (Entities.Employee.JobDemands.Requirements).</summary>
    public enum RotaDemandKind
    {
        Hours = 0,          // HoursWorkingPerWeek: between A and B hours a week (B of 0: no upper end)
        Days = 1,           // DaysWorkingPerWeek: exactly A days a week
        FreeDays = 2,       // FreeOnDays: no work on the days of Mask (bit 0 Monday)
        NoHours = 3,        // HasNoShiftBetweenHours: no work in the hours of Mask (bit 0 midnight to one)
        NoShiftKind = 4,    // NoSpecificShift: no shift of the kinds of Mask (bit RotaKind)
        OnStation = 5       // WorksOnItem: at least one shift on a station of Mask (bit by station)
    }

    /// <summary>One demand of one employee, as plain numbers.</summary>
    public struct RotaDemand
    {
        public RotaDemandKind Kind;
        public int Priority;    // the game's: 0 low, 1 medium, 2 high
        public int A, B;
        public ulong Mask;
    }

    /// <summary>One employee the planner may place.</summary>
    public sealed class RotaWorker
    {
        public int Wage;        // hundredths an hour, everything they are paid
        public int Mood = 100;  // satisfaction now, 0 to 100
        public RotaDemand[] Demands = Empty;

        public static readonly RotaDemand[] Empty = new RotaDemand[0];
    }

    /// <summary>A week to fill: the stations, who may stand at which, and each hour's worth at each (0: not wanted).</summary>
    public sealed class RotaProblem
    {
        public int Stations, Workers;
        public int[] Value = new int[0];        // [cell * Stations + station], a cell being day * 24 + hour
        public byte[] Kind = new byte[0];       // [station] RotaKind
        public bool[] Whole = new bool[0];      // [station] one person for every wanted hour of the week or nobody (a headquarters desk: the game's rule)
        public byte[] Can = new byte[0];        // [worker * Stations + station] how well they do it, 1 to 100; 0: no skill it takes
        public RotaWorker[] Worker = new RotaWorker[0];

        /// <summary>Makes room for so many stations and workers; what was there is cleared.</summary>
        public void Size(int stations, int workers)
        {
            Stations = stations;
            Workers = workers;
            Value = Room(Value, RotaCatalogue.Cells * stations);
            Kind = Room(Kind, stations);
            Whole = Room(Whole, stations);
            Can = Room(Can, workers * stations);
            if (Worker.Length < workers)
            {
                var grown = new RotaWorker[workers];
                System.Array.Copy(Worker, grown, Worker.Length);
                Worker = grown;
            }
            for (int w = 0; w < workers; w++)
            {
                if (Worker[w] == null)
                {
                    Worker[w] = new RotaWorker();
                }
                Worker[w].Wage = 0;
                Worker[w].Mood = 100;
                Worker[w].Demands = RotaWorker.Empty;
            }
        }

        /// <summary>An array of at least <paramref name="count"/>, all cleared: the one given if it is large enough.</summary>
        internal static T[] Room<T>(T[] have, int count)
        {
            if (have.Length < count)
            {
                return new T[count];
            }
            System.Array.Clear(have, 0, have.Length);
            return have;
        }
    }

    /// <summary>Why a demand is not met, or an hour not covered.</summary>
    public enum RotaReason
    {
        Needed = 0,         // nobody else could cover the station then, so the demand gave way
        Short = 1,          // the business does not need enough hours (or days) for it
        NoStation = 2,      // the business has no station of the kind they ask for
        NoSkill = 3,        // nobody here has a skill the station takes
        Stretched = 4       // everybody who could is at work elsewhere or at a limit
    }

    /// <summary>A demand the rota does not meet.</summary>
    public struct RotaUnmet
    {
        public int Worker, Demand;      // which demand of which employee
        public RotaReason Reason;
        public int Day, Station;        // where it gave way; -1 where that says nothing
    }

    /// <summary>Wanted hours at a station that nobody covers: one run of them.</summary>
    public struct RotaGap
    {
        public int Day, From, To, Station;
        public RotaReason Reason;
    }

    /// <summary>A week's rota and what it comes to.</summary>
    public sealed class RotaPlan
    {
        public int Stations;
        public short[] At = new short[0];   // [cell * Stations + station] the worker, or -1
        public int Level;                   // 0: every demand kept as a limit; 1: optional ones gave way where nothing else covered an hour
        public int WantedValue, CoveredValue;
        public int WantedHours, CoveredHours;
        public int UnmetCount, UnmetWeight;
        public long Wage;                   // hundredths for the week
        public int Shifts;
        public readonly List<RotaUnmet> Unmet = new List<RotaUnmet>(RotaCatalogue.ReasonsMost);
        public readonly List<RotaGap> Gaps = new List<RotaGap>(RotaCatalogue.ReasonsMost);

        public bool Full => CoveredHours == WantedHours;

        public void Size(int stations)
        {
            Stations = stations;
            int cells = RotaCatalogue.Cells * stations;
            if (At.Length < cells)
            {
                At = new short[cells];
            }
            for (int i = 0; i < cells; i++)
            {
                At[i] = -1;
            }
            Level = 0;
            WantedValue = CoveredValue = WantedHours = CoveredHours = UnmetCount = UnmetWeight = Shifts = 0;
            Wage = 0;
            Unmet.Clear();
            Gaps.Clear();
        }
    }
}
