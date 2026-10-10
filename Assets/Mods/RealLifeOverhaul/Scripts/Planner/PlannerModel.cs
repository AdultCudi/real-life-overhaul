using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Planner
{
    // Everything in this file is free of the game and of the engine: RealLifeOverhaul.Build/PlannerCheck compiles it
    // as it is and runs it outside the game.

    /// <summary>
    /// The game's clock as the Planner counts it. The game numbers days from 1, day 1 is a Monday, a week has seven
    /// days and a year has as many days as the save says (TimeHelper, Timestamp.GetTotalMinutes: (day * 24 + hour) * 60 + minute).
    /// </summary>
    public static class PlannerTime
    {
        public const int MinutesPerDay = 1440;

        public static int At(int day, int hour, int minute = 0)
        {
            return (day * 24 + hour) * 60 + minute;
        }

        public static int DayOf(int minutes)
        {
            return minutes >= 0 ? minutes / MinutesPerDay : -((-minutes + MinutesPerDay - 1) / MinutesPerDay);
        }

        /// <summary>Minutes since midnight, 0 to 1439.</summary>
        public static int OfDay(int minutes)
        {
            return minutes - DayOf(minutes) * MinutesPerDay;
        }

        /// <summary>0 = Monday ... 6 = Sunday, as TimeHelper.GetDayOfWeek(int) has it. Safe for any number.</summary>
        public static int Weekday(int day)
        {
            int index = (day - 1) % 7;
            return index < 0 ? index + 7 : index;
        }

        /// <summary>The game's year of a day (TimeHelper.GetYearsByDays: day / daysPerYear).</summary>
        public static int Year(int day, int daysPerYear)
        {
            return daysPerYear > 0 && day > 0 ? day / daysPerYear : 0;
        }

        /// <summary>The first day of the year after the one this day is in.</summary>
        public static int NextYearStart(int day, int daysPerYear)
        {
            return daysPerYear > 0 ? (Year(day, daysPerYear) + 1) * daysPerYear : 0;
        }
    }

    public enum PlannerCategory
    {
        Work = 0,
        Court = 1,
        Licence = 2,
        Family = 3,
        Social = 4,
        Travel = 5,
        City = 6,
        Business = 7,
        Health = 8,
        Money = 9,
        Note = 10,
        Count = 11
    }

    public enum PlannerState
    {
        Upcoming = 0,
        Now = 1,
        Done = 2,
        Missed = 3,
        Over = 4      // past, and nothing was lost by it (a market that has packed up)
    }

    public enum RowKind
    {
        Entry = 0,
        DayHeader = 1,
        Info = 2      // an empty state or a calendar fact: a line of text nobody can open
    }

    /// <summary>Who an entry is, so that a reminder set today finds the same entry tomorrow. No strings are built for it.</summary>
    public struct EntryId : IEquatable<EntryId>
    {
        public string Source;
        public int A;
        public int B;

        public EntryId(string source, int a, int b = 0)
        {
            Source = source;
            A = a;
            B = b;
        }

        public bool Equals(EntryId other)
        {
            return A == other.A && B == other.B && string.Equals(Source, other.Source, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EntryId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((Source != null ? Source.GetHashCode() : 0) * 397 ^ A) * 397 ^ B;
        }
    }

    /// <summary>
    /// One thing to do. Entries are taken from a pool (PlannerList.Add) and filled by a source; the numbers are always
    /// filled, the texts only when the list asks for them (PlannerList.WantText), which the hourly look does not.
    /// </summary>
    public sealed class PlannerEntry
    {
        public EntryId Id;
        public PlannerCategory Category;
        public int Day;
        public int Start = -1;       // minutes since midnight; -1: no hour ("all day", or a deadline with only End)
        public int End = -1;         // minutes since midnight on the same day; -1: no end
        public bool Deadline;        // "by 17:00": End is the last moment, there is no start
        public bool Attend;          // the player has to be there: it can clash with another, and it is missed if not done
        public bool Done;
        public bool Own;             // one of the player's notes: it can be edited, ticked off and deleted

        public string Title;
        public string Where;
        public string What;          // one sentence: what it is
        public string Money;         // "Costs $120" / "Pays $64": a finished phrase, or null
        public string IfMissed;      // one sentence taken from the real rules, or null

        public bool HasPlace;        // a point in the city the guider can lead to
        public float X, Y, Z;
        public string Street;        // or one of the game's own addresses (street name and number)
        public int Number;

        // worked out by PlannerLogic
        public PlannerState State;
        public bool Clash;
        public bool Next;

        /// <summary>When it begins, in game minutes; an all-day entry begins with its day, a deadline at its last moment.</summary>
        public int Begins => Day * PlannerTime.MinutesPerDay + (Start >= 0 ? Start : Deadline && End >= 0 ? End : 0);

        /// <summary>When it is over, in game minutes.</summary>
        public int Ends
        {
            get
            {
                int day = Day * PlannerTime.MinutesPerDay;
                if (End >= 0)
                {
                    return day + (Start >= 0 && End < Start ? End + PlannerTime.MinutesPerDay : End);   // past midnight
                }
                return Start >= 0 ? day + Start + 60 : day + PlannerTime.MinutesPerDay;
            }
        }

        public bool AllDay => Start < 0 && !(Deadline && End >= 0);

        internal void Reset()
        {
            Id = default(EntryId);
            Category = PlannerCategory.Note;
            Day = 0;
            Start = End = -1;
            Deadline = Attend = Done = Own = false;
            Title = Where = What = Money = IfMissed = Street = null;
            HasPlace = false;
            X = Y = Z = 0f;
            Number = 0;
            State = PlannerState.Upcoming;
            Clash = Next = false;
        }
    }

    /// <summary>The entries of one look at the sources. Its entries are reused from look to look.</summary>
    public sealed class PlannerList
    {
        private readonly List<PlannerEntry> _pool = new List<PlannerEntry>(32);
        public readonly List<PlannerEntry> Items = new List<PlannerEntry>(32);

        /// <summary>Whether sources should fill in the texts (the app is open, or a reminder is being written).</summary>
        public bool WantText;
        public int FromDay, ToDay;

        public void Begin(int fromDay, int toDay, bool wantText)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                _pool.Add(Items[i]);
            }
            Items.Clear();
            FromDay = fromDay;
            ToDay = toDay;
            WantText = wantText;
        }

        /// <summary>A blank entry on that day, already in the list; null if the day is outside what was asked for.</summary>
        public PlannerEntry Add(string source, int a, int b, PlannerCategory category, int day)
        {
            if (day < FromDay || day > ToDay || Items.Count >= PlannerLogic.MostEntries)
            {
                return null;
            }
            PlannerEntry entry;
            if (_pool.Count > 0)
            {
                entry = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                entry.Reset();
            }
            else
            {
                entry = new PlannerEntry();
            }
            entry.Id = new EntryId(source, a, b);
            entry.Category = category;
            entry.Day = day;
            Items.Add(entry);
            return entry;
        }

        public PlannerEntry Find(EntryId id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id.Equals(id))
                {
                    return Items[i];
                }
            }
            return null;
        }
    }

    /// <summary>Order, clashes, states and "next up". No allocation: it runs on the hourly look as well.</summary>
    public static class PlannerLogic
    {
        /// <summary>A look never holds more than this: a source gone wrong cannot fill the phone.</summary>
        public const int MostEntries = 400;

        private static readonly Comparison<PlannerEntry> ByTime = Compare;

        private static int Compare(PlannerEntry a, PlannerEntry b)
        {
            if (a.Day != b.Day)
            {
                return a.Day < b.Day ? -1 : 1;
            }
            // Within a day: what lasts all day first, then by the clock; a deadline stands at its last moment.
            int ka = a.AllDay ? -1 : a.Start >= 0 ? a.Start : a.End;
            int kb = b.AllDay ? -1 : b.Start >= 0 ? b.Start : b.End;
            if (ka != kb)
            {
                return ka < kb ? -1 : 1;
            }
            if (a.Category != b.Category)
            {
                return a.Category < b.Category ? -1 : 1;
            }
            int source = string.CompareOrdinal(a.Id.Source, b.Id.Source);
            if (source != 0)
            {
                return source;
            }
            return a.Id.A != b.Id.A ? (a.Id.A < b.Id.A ? -1 : 1) : a.Id.B.CompareTo(b.Id.B);
        }

        /// <summary>Puts the list in order and works out every entry's state, the clashes and the one that is next.</summary>
        public static void Arrange(PlannerList list, int nowMinutes)
        {
            List<PlannerEntry> items = list.Items;
            items.Sort(ByTime);
            PlannerEntry next = null;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                e.Clash = false;
                e.Next = false;
                e.State = StateOf(e, nowMinutes);
            }
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry a = items[i];
                if (!Blocks(a))
                {
                    continue;
                }
                for (int j = i + 1; j < items.Count && items[j].Day == a.Day; j++)
                {
                    PlannerEntry b = items[j];
                    if (Blocks(b) && a.Begins < b.Ends && b.Begins < a.Ends)
                    {
                        a.Clash = true;
                        b.Clash = true;
                    }
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.State == PlannerState.Now && !e.AllDay)
                {
                    next = e;   // what is going on now is what is next to do
                    break;
                }
                if (e.State == PlannerState.Upcoming && !e.AllDay && (next == null || e.Begins < next.Begins))
                {
                    next = e;
                }
            }
            if (next != null)
            {
                next.Next = true;
            }
        }

        // Two things clash only if the player has to be at both, both have an hour, and neither is over and done with.
        private static bool Blocks(PlannerEntry e)
        {
            return e.Attend && e.Start >= 0 && !e.Done && e.State != PlannerState.Missed && e.State != PlannerState.Over;
        }

        public static PlannerState StateOf(PlannerEntry e, int nowMinutes)
        {
            if (e.Done)
            {
                return PlannerState.Done;
            }
            if (nowMinutes >= e.Ends)
            {
                return e.Attend || e.Deadline ? PlannerState.Missed : PlannerState.Over;
            }
            if (e.Deadline)
            {
                return PlannerState.Upcoming;   // open until its last moment
            }
            return nowMinutes >= e.Begins ? PlannerState.Now : PlannerState.Upcoming;
        }

        /// <summary>How many entries begin (or fall due) within the next minutes and are still to do: the number on the app's badge.</summary>
        public static int DueSoon(PlannerList list, int nowMinutes, int withinMinutes)
        {
            int count = 0;
            List<PlannerEntry> items = list.Items;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.Done || e.AllDay && !e.Own)
                {
                    continue;
                }
                PlannerState state = StateOf(e, nowMinutes);
                if (state == PlannerState.Now && !e.AllDay)
                {
                    count++;
                }
                else if (state == PlannerState.Upcoming && !e.AllDay && e.Begins - nowMinutes <= withinMinutes)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// A number that changes when what is due soon changes: the badge comes back for something new and stays away
        /// for what the player has already seen.
        /// </summary>
        public static int DueStamp(PlannerList list, int nowMinutes, int withinMinutes)
        {
            int stamp = 17;
            List<PlannerEntry> items = list.Items;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.Done || e.AllDay)
                {
                    continue;
                }
                PlannerState state = StateOf(e, nowMinutes);
                if (state == PlannerState.Now || state == PlannerState.Upcoming && e.Begins - nowMinutes <= withinMinutes)
                {
                    stamp = stamp * 31 + e.Id.GetHashCode();
                    stamp = stamp * 31 + e.Begins;
                }
            }
            return stamp;
        }
    }
}
