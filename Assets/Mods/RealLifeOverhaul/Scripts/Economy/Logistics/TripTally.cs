using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Why goods a route's trip meant to carry did not reach their stop. Several may hold in one run.</summary>
    [Flags]
    public enum TripWhy
    {
        None = 0,
        Full = 1,          // the vehicle's boxes were all taken by other stops or goods
        NoStock = 2,       // the warehouse held none (or too little) of it
        ShelvesFull = 4,   // the stop had no shelf or storage with room: the game handed it back
        StopGone = 8,      // the stop was taken off the route, or the route changed, before the vehicle came
        NoDriver = 16,     // the vehicle had no driver at work, or no vehicle
        NoManager = 32,    // the route's manager was not at work
        Lost = 64,         // it left the warehouse and is not in the stop's count
        Stale = 128        // the trip was too old when the save was read, or the trips were off for days: dropped, its goods never left the warehouse
    }

    /// <summary>
    /// What one route's trips did in one game day, in pieces: taken onto vehicles, delivered, handed back to the warehouse, lost
    /// (left the warehouse and was not credited to the stop), and wanted but not loaded. Units are conserved: taken is
    /// delivered plus returned plus lost. Kept in memory for the day's one line in Player.log and its one notice; not saved.
    /// </summary>
    public sealed class RouteTally
    {
        public int Day = -1;
        public string Name;
        public int Runs, Taken, Delivered, Returned, Lost, Left;
        public TripWhy Why;
        public bool Told;       // the day's notice of a failed delivery was shown

        public bool Quiet => Runs == 0 && Taken == 0 && Left == 0 && Why == TripWhy.None;

        public void Clear(int day, string name)
        {
            Day = day;
            Name = name;
            Runs = Taken = Delivered = Returned = Lost = Left = 0;
            Why = TripWhy.None;
            Told = false;
        }
    }

    /// <summary>The routes' tallies of the day. Game-free.</summary>
    public sealed class TripTally
    {
        private readonly Dictionary<string, RouteTally> _by = new Dictionary<string, RouteTally>(StringComparer.Ordinal);
        private readonly List<string> _keys = new List<string>(8);

        public RouteTally Of(string plan, int day, string name)
        {
            if (!_by.TryGetValue(plan, out RouteTally t))
            {
                t = new RouteTally();
                t.Clear(day, name);
                _by[plan] = t;
            }
            else if (t.Day != day)
            {
                t.Clear(day, name);
            }
            else if (!string.IsNullOrEmpty(name))
            {
                t.Name = name;
            }
            return t;
        }

        /// <summary>The route's tally of <paramref name="day"/>, or null when it has none; nothing is made.</summary>
        public RouteTally Peek(string plan, int day) => _by.TryGetValue(plan, out RouteTally t) && t.Day == day ? t : null;

        /// <summary>The routes whose tally is of a day before <paramref name="today"/> (every one when <paramref name="all"/>), into the list; each is dropped from the book.</summary>
        public int Take(int today, bool all, List<KeyValuePair<string, RouteTally>> into)
        {
            into.Clear();
            _keys.Clear();
            foreach (KeyValuePair<string, RouteTally> kv in _by)
            {
                if (all || kv.Value.Day < today)
                {
                    _keys.Add(kv.Key);
                }
            }
            _keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < _keys.Count; i++)
            {
                into.Add(new KeyValuePair<string, RouteTally>(_keys[i], _by[_keys[i]]));
                _by.Remove(_keys[i]);
            }
            return into.Count;
        }

        public void Clear() => _by.Clear();

        /// <summary>Pieces that were taken onto vehicles and are neither delivered, returned nor lost: always 0 when the book balances.</summary>
        public static int Unaccounted(RouteTally t) => t.Taken - t.Delivered - t.Returned - t.Lost;

        /// <summary>The day's one line for Player.log.</summary>
        public static string Line(RouteTally t)
        {
            var s = new StringBuilder(160);
            s.Append("Delivery trips (day ").Append(t.Day.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(string.IsNullOrEmpty(t.Name) ? "a route" : t.Name).Append("): ")
                .Append(t.Runs.ToString(CultureInfo.InvariantCulture)).Append(" run(s), taken ").Append(t.Taken.ToString(CultureInfo.InvariantCulture))
                .Append(" piece(s), delivered ").Append(t.Delivered.ToString(CultureInfo.InvariantCulture))
                .Append(", returned ").Append(t.Returned.ToString(CultureInfo.InvariantCulture))
                .Append(", lost ").Append(t.Lost.ToString(CultureInfo.InvariantCulture))
                .Append("; wanted but not loaded ").Append(t.Left.ToString(CultureInfo.InvariantCulture));
            if (t.Why != TripWhy.None)
            {
                s.Append("; because: ").Append(Words(t.Why));
            }
            s.Append('.');
            return s.ToString();
        }

        /// <summary>The reasons in plain English, for the log.</summary>
        public static string Words(TripWhy why)
        {
            var s = new StringBuilder(64);
            Add(s, why, TripWhy.Full, "vehicle full");
            Add(s, why, TripWhy.NoStock, "warehouse out of stock");
            Add(s, why, TripWhy.ShelvesFull, "shelves and storage full");
            Add(s, why, TripWhy.StopGone, "stop left the route");
            Add(s, why, TripWhy.NoDriver, "no driver at work");
            Add(s, why, TripWhy.NoManager, "manager not at work");
            Add(s, why, TripWhy.Lost, "goods not found at the stop");
            Add(s, why, TripWhy.Stale, "trip too old after a load");
            return s.ToString();
        }

        private static void Add(StringBuilder s, TripWhy all, TripWhy one, string words)
        {
            if ((all & one) != 0)
            {
                s.Append(s.Length > 0 ? ", " : string.Empty).Append(words);
            }
        }

        /// <summary>The most telling reason of those that hold: what a player is told first.</summary>
        public static TripWhy First(TripWhy why)
        {
            TripWhy[] order = { TripWhy.NoManager, TripWhy.NoDriver, TripWhy.ShelvesFull, TripWhy.NoStock, TripWhy.Full, TripWhy.StopGone, TripWhy.Lost, TripWhy.Stale };
            for (int i = 0; i < order.Length; i++)
            {
                if ((why & order[i]) != 0)
                {
                    return order[i];
                }
            }
            return TripWhy.None;
        }

        /// <summary>The key of a reason's words and of its advice for the player (the mod's texts).</summary>
        public static string ReasonKey(TripWhy one) => "reallife_trips_why_" + Name(one);

        public static string AdviceKey(TripWhy one) => "reallife_trips_fix_" + Name(one);

        private static string Name(TripWhy one)
        {
            switch (one)
            {
                case TripWhy.Full: return "full";
                case TripWhy.NoStock: return "stock";
                case TripWhy.ShelvesFull: return "shelves";
                case TripWhy.StopGone: return "stop";
                case TripWhy.NoDriver: return "driver";
                case TripWhy.NoManager: return "manager";
                case TripWhy.Stale: return "stale";
                default: return "lost";
            }
        }
    }
}
