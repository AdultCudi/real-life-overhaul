using System;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>One desk's figures for the driver: the day's, the day before's and the whole save's.</summary>
    public sealed class SideTally
    {
        public int UnitsToday;      // fares driven or parcels delivered
        public int GrossToday;      // what the customers paid
        public int KeptToday;       // the firm's share of it
        public int TipsToday;
        public int NetToday;        // what reached the driver
        public int FuelToday;       // the cost of the fuel burned on shift
        public int UnitsBefore;     // the day before's units
        public int NetBefore;       // ...and what reached the driver
        public int Shifts;          // shifts signed off, ever
        public long Units;          // ever
        public long Net;
        public long Kept;
    }

    /// <summary>
    /// The driver's own figures at the two desks and the seed a firm's books are drawn from: all the feature keeps in a save.
    /// Game-free; the rules of a day's turn and of a booking are here so that EconomyCheck runs them.
    /// </summary>
    public sealed class SideJobState
    {
        public int Seed;            // drawn once a save: the firms' other drivers come from it
        public int Day = -1;        // the day the day's figures are of

        public readonly SideTally[] Of = { new SideTally(), new SideTally() };

        public SideTally At(SideKind kind) => Of[(int)kind];

        /// <summary>Fares or parcels the desk still has for the driver today.</summary>
        public int Left(SideKind kind)
        {
            int left = SideJobRules.DayMost(kind) - At(kind).UnitsToday;
            return left > 0 ? left : 0;
        }

        /// <summary>A new day: today's figures become the day before's (none, if days were missed). True if the day turned.</summary>
        public bool NewDay(int day)
        {
            if (day == Day)
            {
                return false;
            }
            bool next = Day >= 0 && day == Day + 1;
            for (int k = 0; k < Of.Length; k++)
            {
                SideTally t = Of[k];
                t.UnitsBefore = next ? t.UnitsToday : 0;
                t.NetBefore = next ? t.NetToday : 0;
                t.UnitsToday = 0;
                t.GrossToday = 0;
                t.KeptToday = 0;
                t.TipsToday = 0;
                t.NetToday = 0;
                t.FuelToday = 0;
            }
            Day = day;
            return true;
        }

        /// <summary>A fare or a run paid: onto the day and the save. Figures below nothing count as nothing.</summary>
        public void Book(SideKind kind, int units, int gross, int kept, int tips, int net)
        {
            SideTally t = At(kind);
            units = Math.Max(0, units);
            gross = Math.Max(0, gross);
            kept = Math.Max(0, Math.Min(kept, gross));
            tips = Math.Max(0, tips);
            net = Math.Max(0, net);
            t.UnitsToday += units;
            t.GrossToday += gross;
            t.KeptToday += kept;
            t.TipsToday += tips;
            t.NetToday += net;
            t.Units += units;
            t.Net += net;
            t.Kept += kept;
        }

        /// <summary>The cost of fuel burned on a shift, onto the day.</summary>
        public void Fuel(SideKind kind, int cost)
        {
            At(kind).FuelToday += Math.Max(0, cost);
        }

        /// <summary>Every figure inside its bounds, after a read.</summary>
        public void Bound()
        {
            Day = Math.Max(-1, Day);
            for (int k = 0; k < Of.Length; k++)
            {
                SideTally t = Of[k];
                t.UnitsToday = Math.Max(0, Math.Min(t.UnitsToday, 1000));
                t.GrossToday = Math.Max(0, t.GrossToday);
                t.KeptToday = Math.Max(0, Math.Min(t.KeptToday, t.GrossToday));
                t.TipsToday = Math.Max(0, t.TipsToday);
                t.NetToday = Math.Max(0, t.NetToday);
                t.FuelToday = Math.Max(0, t.FuelToday);
                t.UnitsBefore = Math.Max(0, t.UnitsBefore);
                t.NetBefore = Math.Max(0, t.NetBefore);
                t.Shifts = Math.Max(0, t.Shifts);
                t.Units = Math.Max(0, t.Units);
                t.Net = Math.Max(0, t.Net);
                t.Kept = Math.Max(0, t.Kept);
            }
        }
    }
}
