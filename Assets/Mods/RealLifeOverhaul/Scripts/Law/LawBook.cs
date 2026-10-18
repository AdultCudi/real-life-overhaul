using System;

namespace RealLifeOverhaul.Law
{
    // Game-free: nothing here touches the game or the engine. Every locale key is written out in full.

    /// <summary>What somebody can be held for, and the two things that only ever go on the record.</summary>
    public enum Charge
    {
        WastedCalls,    // 911 calls about nothing, again and again in one day
        FledStop,       // told to pull over, drove on, was caught
        HitAndRun,      // hit a car, drove off, was caught (reckless driving causing a crash)
        InjuryFled,     // injured someone on foot, drove off, was caught
        DeathStayed,    // killed someone on foot and waited for the police
        DeathFled,      // killed someone on foot, drove off, was caught
        Citation,       // record only: a ticket at the roadside
        Wanted          // record only: drove off after hurting someone and was not caught
    }

    /// <summary>How an entry on the record ended.</summary>
    public enum Outcome
    {
        Held,       // still in the cell
        Served,     // sat it out
        Bailed,     // paid to walk
        Cited,      // a ticket, no cell
        Escaped     // never caught
    }

    /// <summary>One row of the offence table.</summary>
    public sealed class ChargeDef
    {
        public Charge Id;
        public string Name;         // locale key: how it reads on the charge sheet
        public int HoursMin;        // in the cell, before anything else is added
        public int HoursMax;
        public int Bail;            // to walk at once; 0: no bail for this
        public int Police;          // what it does to the player's standing with the police
        public int Public;          // ...and with everyone else
        public bool Custodial;      // false for the two record-only kinds
    }

    /// <summary>
    /// The offence table and its sums: how long, what bail, what a lawyer takes off. The fines themselves are charged by
    /// whoever stops the player (Crime/CrimeCatalogue, PolicePursuit), once; the charge sheet shows what was paid.
    /// </summary>
    public static class LawBook
    {
        public const int MostHours = 48;            // a day or two at most: nothing is ever longer
        public const int LeastHours = 1;
        public const float OtherChargesShare = 0.25f;   // each further charge adds this much of its own time to the longest
        public const float RepeatStep = 0.15f;      // for each time held in the last RepeatDays
        public const float RepeatMost = 1.6f;
        public const int RepeatDays = 14;
        public static int LawyerFee => Prices.At(750);
        public static int LawyerFeeLiked => Prices.At(600);      // for somebody the public likes (Reputation.Liked)
        public static int ImpoundFee => Prices.At(300);          // the city pound's, whatever the car was taken for (Licences charges the same)
        public const int WastedCallsForCustody = 5; // the fifth call about nothing in one day

        // What the cell does to the body: never worse than this on the way out.
        public const float ReleaseEnergy = 45f;
        public const float ReleaseHunger = 50f;

        private static readonly ChargeDef[] Table =
        {
            new ChargeDef { Id = Charge.WastedCalls, Name = "reallife_law_charge_wasted_calls", HoursMin = 2, HoursMax = 4, Bail = 300, Police = -6, Public = -1, Custodial = true },
            new ChargeDef { Id = Charge.FledStop, Name = "reallife_law_charge_fled_stop", HoursMin = 4, HoursMax = 8, Bail = 1500, Police = -10, Public = -2, Custodial = true },
            new ChargeDef { Id = Charge.HitAndRun, Name = "reallife_law_charge_hit_and_run", HoursMin = 8, HoursMax = 16, Bail = 3000, Police = -12, Public = -4, Custodial = true },
            new ChargeDef { Id = Charge.InjuryFled, Name = "reallife_law_charge_injury_fled", HoursMin = 18, HoursMax = 30, Bail = 8000, Police = -16, Public = -8, Custodial = true },
            new ChargeDef { Id = Charge.DeathStayed, Name = "reallife_law_charge_death_stayed", HoursMin = 24, HoursMax = 36, Bail = 0, Police = -14, Public = -10, Custodial = true },
            new ChargeDef { Id = Charge.DeathFled, Name = "reallife_law_charge_death_fled", HoursMin = 36, HoursMax = 48, Bail = 0, Police = -25, Public = -15, Custodial = true },
            new ChargeDef { Id = Charge.Citation, Name = "reallife_law_charge_citation", Police = -3, Public = 0 },
            new ChargeDef { Id = Charge.Wanted, Name = "reallife_law_charge_wanted", Police = -10, Public = -3 }
        };

        public static int Count => Table.Length;

        public static ChargeDef Of(Charge charge)
        {
            int index = (int)charge;
            return index >= 0 && index < Table.Length ? Table[index] : Table[0];
        }

        public static int Bit(Charge charge) => 1 << (int)charge;

        /// <summary>How much longer somebody is held who has been held before: 1 for a first time, never above <see cref="RepeatMost"/>.</summary>
        public static float RepeatFactor(int timesHeldLately)
        {
            float factor = 1f + RepeatStep * Math.Max(0, timesHeldLately);
            return factor > RepeatMost ? RepeatMost : factor;
        }

        /// <summary>Hours in the cell: the longest charge in full, a quarter of each other, times the repeat factor; two days at most.</summary>
        public static int Sentence(int charges, int timesHeldLately, Func<float> random01)
        {
            float longest = 0f;
            float all = 0f;
            for (int i = 0; i < Table.Length; i++)
            {
                ChargeDef def = Table[i];
                if (!def.Custodial || (charges & (1 << i)) == 0)
                {
                    continue;
                }
                float roll = random01 != null ? random01() : 0.5f;
                float hours = def.HoursMin + (def.HoursMax - def.HoursMin) * (roll < 0f ? 0f : roll > 1f ? 1f : roll);
                all += hours;
                if (hours > longest)
                {
                    longest = hours;
                }
            }
            if (longest <= 0f)
            {
                return 0;
            }
            float total = (longest + (all - longest) * OtherChargesShare) * RepeatFactor(timesHeldLately);
            int rounded = (int)Math.Round(total);
            return rounded < LeastHours ? LeastHours : rounded > MostHours ? MostHours : rounded;
        }

        /// <summary>What it costs to walk at once: the dearest of the charges, times the repeat factor. 0 when any of them allows no bail.</summary>
        public static int Bail(int charges, int timesHeldLately)
        {
            int most = 0;
            for (int i = 0; i < Table.Length; i++)
            {
                ChargeDef def = Table[i];
                if (!def.Custodial || (charges & (1 << i)) == 0)
                {
                    continue;
                }
                if (def.Bail <= 0)
                {
                    return 0;
                }
                if (def.Bail > most)
                {
                    most = def.Bail;
                }
            }
            return (int)Math.Round(most * RepeatFactor(timesHeldLately) / 50f) * 50;
        }

        /// <summary>What a lawyer leaves of the time still to serve: two thirds, and never less than a quarter of an hour.</summary>
        public static float AfterLawyer(float minutesLeft)
        {
            float left = minutesLeft * 2f / 3f;
            return left < 15f ? Math.Min(15f, minutesLeft) : left;
        }
    }
}
