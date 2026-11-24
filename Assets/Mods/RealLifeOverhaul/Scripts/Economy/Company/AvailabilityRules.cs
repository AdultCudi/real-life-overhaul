using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The kinds of part-timer with set hours. Saved nowhere: a person's kind comes from their id.</summary>
    public enum AvailabilityKind
    {
        None = 0,
        Student = 1,        // three weekdays after lessons
        Parent = 2,         // the school day, Monday to Friday
        EarlyRiser = 3,     // mornings, Monday to Friday
        Weekender = 4       // Saturday and Sunday
    }

    /// <summary>The days and hours a part-timer can work. Days are bits, bit 0 Monday to bit 6 Sunday; the hours run from <see cref="From"/> to <see cref="To"/>, which is not itself worked.</summary>
    public struct AvailabilityWindow
    {
        public AvailabilityKind Kind;
        public int Days;
        public int From, To;

        public bool None => Kind == AvailabilityKind.None;

        /// <summary>Whether the hour of that day (0 Monday to 6 Sunday) is inside it.</summary>
        public bool Allows(int day, int hour) => Kind != AvailabilityKind.None && day >= 0 && day < 7 && (Days >> day & 1) != 0 && hour >= From && hour < To;

        /// <summary>The hours of a shift on a day (0 Monday to 6 Sunday) from <paramref name="start"/> to <paramref name="end"/>, the end not worked, that fall outside it.</summary>
        public int HoursOff(int day, int start, int end)
        {
            int off = 0;
            for (int h = Math.Max(0, start); h < Math.Min(24, end); h++)
            {
                off += Allows(day, h) ? 0 : 1;
            }
            return off;
        }
    }

    /// <summary>
    /// Part-timers with real availability (the option Part-timers keep set hours). A candidate or employee whose hours a week the game limits to a part-time week
    /// (its own Part-time demand, up to <see cref="PartTimeMostHours"/> hours) is a student, a parent, an early riser or a weekender, by their id alone, so the same
    /// person has the same hours on every machine and in every save, and nothing is stored. Game-free; nothing allocated.
    /// </summary>
    public static class AvailabilityRules
    {
        public const int PartTimeMostHours = 30;    // the game's Part-time demand asks between 10 and 30 hours a week

        // The mix of the part-timers, per cent, in the order of the kinds.
        private static readonly int[] Share = { 40, 25, 15, 20 };

        // Three of the five weekdays (bit 0 Monday to bit 4 Friday), the ten ways a student may have them.
        private static readonly int[] StudentDays = { 0x07, 0x0B, 0x13, 0x0D, 0x15, 0x19, 0x0E, 0x16, 0x1A, 0x1C };

        public const int WeekdaysMask = 0x1F, WeekendMask = 0x60;

        /// <summary>
        /// The going rate of a person with set hours is this many per cent less than that of the same skill: they take less for the hours that suit them,
        /// so they are not asked to be paid more for it.
        /// </summary>
        public const int DiscountPercent = 6;

        /// <summary>The mood a shift outside their hours costs, tenths of a point for each hour: what a medium demand of the game's takes, the same weight the rota planner gives it.</summary>
        public static int CostTenths => RotaCatalogue.DemandWeight[1];

        /// <summary>The most one day's shifts outside their hours may take, tenths of a point: past it a day is no worse.</summary>
        public const int CostMostTenths = 100;

        /// <summary>A stable number from an id (a string's own hash differs between runs and machines).</summary>
        public static uint Seed(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; id != null && i < id.Length; i++)
                {
                    h = (h ^ id[i]) * 16777619u;
                }
                return h;
            }
        }

        /// <summary>Whether a week of up to this many hours a week (0: no upper end) is a part-time week.</summary>
        public static bool PartTime(int mostHours) => mostHours > 0 && mostHours <= PartTimeMostHours;

        /// <summary>The window of a person from their id and the most hours a week their demands allow; none for anybody who is not a part-timer or has no id.</summary>
        public static AvailabilityWindow Of(string id, int mostHours)
        {
            var window = new AvailabilityWindow();
            if (string.IsNullOrEmpty(id) || !PartTime(mostHours))
            {
                return window;
            }
            uint seed = Seed(id);
            int roll = (int)(seed % 100u), kind = 0, sum = Share[0];
            while (kind < Share.Length - 1 && roll >= sum)
            {
                kind++;
                sum += Share[kind];
            }
            window.Kind = (AvailabilityKind)(kind + 1);
            switch (window.Kind)
            {
                case AvailabilityKind.Student:
                    window.Days = StudentDays[(int)(seed / 100u % (uint)StudentDays.Length)];
                    window.From = 16;
                    window.To = 22;
                    break;
                case AvailabilityKind.Parent:
                    window.Days = WeekdaysMask;
                    window.From = 9;
                    window.To = 15;
                    break;
                case AvailabilityKind.EarlyRiser:
                    window.Days = WeekdaysMask;
                    window.From = 6;
                    window.To = 14;
                    break;
                default:
                    window.Days = WeekendMask;
                    window.From = 8;
                    window.To = 20;
                    break;
            }
            return window;
        }

        /// <summary>The hours outside the window as the rota planner's mask (bit 0 midnight to one).</summary>
        public static ulong HoursOffMask(AvailabilityWindow w)
        {
            ulong mask = 0;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                mask |= h < w.From || h >= w.To ? 1UL << h : 0UL;
            }
            return mask;
        }

        /// <summary>The days outside the window as the rota planner's mask (bit 0 Monday).</summary>
        public static ulong DaysOffMask(AvailabilityWindow w) => ~(ulong)w.Days & 0x7FUL;

        /// <summary>The going hourly rate of somebody with this window, hundredths: the rate less <see cref="DiscountPercent"/> per cent; the rate itself with none.</summary>
        public static int Going(int rate, AvailabilityWindow w) => w.None || rate <= 0 ? rate : rate * (100 - DiscountPercent) / 100;

        /// <summary>The mood a day's hours outside the window cost, tenths of a point, held to <see cref="CostMostTenths"/>.</summary>
        public static int Cost(int hoursOff) => hoursOff <= 0 ? 0 : (int)Math.Min(CostMostTenths, (long)hoursOff * CostTenths);

        /// <summary>The kind's text key: "Student", "Parent" and so on, for the card and the page.</summary>
        public static string KindKey(AvailabilityKind kind)
        {
            switch (kind)
            {
                case AvailabilityKind.Student: return "reallife_avail_student";
                case AvailabilityKind.Parent: return "reallife_avail_parent";
                case AvailabilityKind.EarlyRiser: return "reallife_avail_early";
                case AvailabilityKind.Weekender: return "reallife_avail_weekend";
                default: return null;
            }
        }

        /// <summary>How many days the window has.</summary>
        public static int DayCount(AvailabilityWindow w)
        {
            int n = 0;
            for (int d = 0; d < 7; d++)
            {
                n += (w.Days >> d & 1);
            }
            return n;
        }
    }
}
