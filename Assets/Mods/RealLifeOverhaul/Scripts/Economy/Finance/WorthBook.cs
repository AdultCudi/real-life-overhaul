using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A fortune's days on record: one figure for each game day the interface saw (a day jumped over by a time skip has none, the
    /// line then runs straight across it), the oldest dropped past <see cref="Most"/>. Game-free.
    /// </summary>
    public sealed class NetWorthRing
    {
        public const int Most = 60;

        private readonly int[] _day;
        private readonly long[] _worth;
        private readonly int _room;

        public NetWorthRing(int room = Most)
        {
            _room = Math.Max(2, Math.Min(Most, room));
            _day = new int[_room];
            _worth = new long[_room];
        }

        public int Room => _room;

        public int Count { get; private set; }

        public int DayAt(int i) => _day[i];

        public long WorthAt(int i) => _worth[i];

        public int LastDay => Count > 0 ? _day[Count - 1] : int.MinValue;

        /// <summary>A day's figure. The same day again takes the later figure; an earlier day than the last (a clock put back) is not kept.</summary>
        public void Push(int day, long worth)
        {
            if (Count > 0 && day < _day[Count - 1])
            {
                return;
            }
            if (Count > 0 && day == _day[Count - 1])
            {
                _worth[Count - 1] = worth;
                return;
            }
            if (Count == _room)
            {
                Array.Copy(_day, 1, _day, 0, _room - 1);
                Array.Copy(_worth, 1, _worth, 0, _room - 1);
                Count--;
            }
            _day[Count] = day;
            _worth[Count] = worth;
            Count++;
        }

        public void Clear() => Count = 0;
    }

    /// <summary>The figures the net worth card shows, over the days of a window and today's figure.</summary>
    public struct WorthSummary
    {
        public long Now;
        public bool Has;            // two days or more are on record in the window, today's included: a line and a change can be drawn
        public long High, Low;
        public int HighDay, LowDay;
        public long Change;         // today's figure less the window's first
        public int FromDay;         // the window's first day on record
        public int Points;          // how many days the line has
    }

    /// <summary>
    /// The player's net worth and each rival tycoon's by day, kept in the save for the net worth card over the money pill and
    /// for the rich list: sixty days for the player, thirty for each tycoon. A fortune only ever gets a figure on a day the city
    /// played; there is nothing before the save was first played with this version, and the card says so. Game-free.
    /// </summary>
    public sealed class WorthBook
    {
        public const int Window = 30;          // days the card looks back
        public const int Tycoons = 8;          // most rivals kept (the catalogue has fewer)
        public const int TycoonDays = 30;      // days kept for each of them

        public readonly NetWorthRing Player = new NetWorthRing();
        public readonly NetWorthRing[] Rivals = new NetWorthRing[Tycoons];

        public WorthBook()
        {
            for (int i = 0; i < Rivals.Length; i++)
            {
                Rivals[i] = new NetWorthRing(TycoonDays);
            }
        }

        public bool Empty
        {
            get
            {
                if (Player.Count > 0)
                {
                    return false;
                }
                for (int i = 0; i < Rivals.Length; i++)
                {
                    if (Rivals[i].Count > 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// One day's snapshot: the player's net worth, and each active tycoon's (their index, their worth), taken the same way. Called once a
        /// day by whoever watches the days (WorthWatch); the same day again only replaces the day's figure. Deterministic.
        /// </summary>
        public void Day(int day, long player, long[] tycoon, bool[] active, int count)
        {
            Player.Push(day, player);
            for (int i = 0; i < count && i < Rivals.Length; i++)
            {
                if (active != null && active[i])
                {
                    Rivals[i].Push(day, tycoon[i]);
                }
            }
        }

        /// <summary>
        /// A ring over the last <see cref="Window"/> days to <paramref name="today"/>, with <paramref name="now"/> as today's figure: highest
        /// and lowest with their days, the change from the window's first day on record, and how many days the line has. The line's values
        /// go to <paramref name="line"/> (oldest first, today's last) when it has room for them.
        /// </summary>
        public static WorthSummary Summary(NetWorthRing ring, int today, long now, float[] line)
        {
            var s = new WorthSummary { Now = now, High = now, Low = now, HighDay = today, LowDay = today, FromDay = today };
            int first = today - Window + 1, n = 0;
            long start = now;
            for (int i = 0; i < ring.Count; i++)
            {
                int day = ring.DayAt(i);
                if (day < first || day >= today)
                {
                    continue;   // today's own figure is the live one, never an older snapshot of the same day
                }
                long w = ring.WorthAt(i);
                if (n == 0)
                {
                    start = w;
                    s.FromDay = day;
                }
                if (w > s.High)
                {
                    s.High = w;
                    s.HighDay = day;
                }
                if (w < s.Low)
                {
                    s.Low = w;
                    s.LowDay = day;
                }
                if (line != null && n < line.Length)
                {
                    line[n] = w;
                }
                n++;
            }
            if (line != null && n < line.Length)
            {
                line[n] = now;
            }
            s.Points = n + 1;
            s.Has = n >= 1;
            s.Change = now - start;
            return s;
        }

        /// <summary>The number of values <see cref="Summary"/> writes into its line for a ring: the days in the window before today, and today's.</summary>
        public static int LineLength(NetWorthRing ring, int today)
        {
            int first = today - Window + 1, n = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                if (ring.DayAt(i) >= first && ring.DayAt(i) < today)
                {
                    n++;
                }
            }
            return n + 1;
        }
    }
}
