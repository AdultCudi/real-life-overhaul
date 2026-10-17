using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy shared in a Going Public session (Core/SharedCity, part E): a guest takes the host's city, keeping its own figures. Game-free.</summary>
    public static class CityShare
    {
        /// <summary>The city's part of a state, as a <see cref="CityStore"/> line: what is carried, the rival and the goods stocked left out.</summary>
        public static string Write(CityState s)
        {
            var city = new CityState();
            Copy(s, city);
            for (int i = 0; i < s.News.Count; i++)
            {
                if (s.News[i].Kind != CityNewsKind.Rival)
                {
                    city.News.Add(s.News[i]);
                }
            }
            return CityStore.Write(city);
        }

        /// <summary>The host's line applied to a guest's state; false, the state untouched, for a line that cannot be read.</summary>
        public static bool Apply(CityState mine, string text, int monthDays, out CityTick tick)
        {
            tick = new CityTick { ShockAnnounced = -1 };
            int version;
            CityState host = CityStore.Read(text, out version);
            if (host == null || mine == null)
            {
                return false;
            }
            monthDays = Math.Max(1, monthDays);
            tick.MonthTurned = host.Age / monthDays > mine.Age / monthDays;
            tick.RateMoved = tick.MonthTurned ? host.Base - mine.Base : 0;
            tick.CycleChanged = host.Cycle != mine.Cycle;
            for (int k = 0; k < host.Supply.Length && k < mine.Supply.Length; k++)
            {
                if (host.Supply[k].Announced(host.Age) && (!mine.Supply[k].Announced(mine.Age) || mine.Supply[k].From != host.Supply[k].From))
                {
                    tick.ShockAnnounced = k;
                }
            }
            Copy(host, mine);
            for (int i = mine.News.Count - 1; i >= 0; i--)
            {
                if (mine.News[i].Kind != CityNewsKind.Rival)
                {
                    mine.News.RemoveAt(i);
                }
            }
            for (int i = 0; i < host.News.Count && mine.News.Count < 16; i++)
            {
                if (host.News[i].Kind != CityNewsKind.Rival)
                {
                    mine.News.Add(host.News[i]);
                }
            }
            return true;
        }

        /// <summary>True when every figure of the city's is equal in the two states (the player's own are not compared).</summary>
        public static bool SameCity(CityState a, CityState b)
        {
            if (a.Seed != b.Seed || a.Age != b.Age || a.LastDay != b.LastDay || a.Cycle != b.Cycle || a.MonthsLeft != b.MonthsLeft || a.MonthsIn != b.MonthsIn
                || a.Base != b.Base || a.BaseBefore != b.BaseBefore || a.Pending != b.Pending || a.PendingMove != b.PendingMove || a.PendingReason != b.PendingReason
                || a.LastMove != b.LastMove || a.LastReason != b.LastReason || a.Index != b.Index || a.Inflation != b.Inflation || a.Pressure != b.Pressure
                || a.MonthChange != b.MonthChange || a.Districts.Count != b.Districts.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Districts.Count; i++)
            {
                CityDistrict x = a.Districts[i], y = b.Districts[i];
                if (x.Name != y.Name || x.Start != y.Start || x.Income != y.Income || x.Before != y.Before)
                {
                    return false;
                }
            }
            for (int k = 0; k < a.Supply.Length; k++)
            {
                SupplyLine x = a.Supply[k], y = b.Supply[k];
                if (x.Factor != y.Factor || x.Shock != y.Shock || x.From != y.From || x.Days != y.Days || x.Size != y.Size)
                {
                    return false;
                }
            }
            return true;
        }

        // The city's figures only; the player's own of 'to' are left alone.
        private static void Copy(CityState from, CityState to)
        {
            to.Seed = from.Seed;
            to.Age = from.Age;
            to.LastDay = from.LastDay;
            to.Cycle = from.Cycle;
            to.MonthsLeft = from.MonthsLeft;
            to.MonthsIn = from.MonthsIn;
            to.Base = from.Base;
            to.BaseBefore = from.BaseBefore;
            to.Pending = from.Pending;
            to.PendingMove = from.PendingMove;
            to.PendingReason = from.PendingReason;
            to.LastMove = from.LastMove;
            to.LastReason = from.LastReason;
            to.Index = from.Index;
            to.Inflation = from.Inflation;
            to.Pressure = from.Pressure;
            to.MonthChange = from.MonthChange;
            to.Districts.Clear();
            for (int i = 0; i < from.Districts.Count; i++)
            {
                CityDistrict d = from.Districts[i];
                to.Districts.Add(new CityDistrict { Name = d.Name, Start = d.Start, Income = d.Income, Before = d.Before });
            }
            for (int k = 0; k < from.Supply.Length && k < to.Supply.Length; k++)
            {
                to.Supply[k] = from.Supply[k];
            }
        }
    }
}
