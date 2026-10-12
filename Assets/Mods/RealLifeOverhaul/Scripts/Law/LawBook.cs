using System;

namespace RealLifeOverhaul.Law
{
    // Game-free: nothing in this file touches the game or the engine, so the sums can be checked on their own.
    // Every locale key is written out in full.

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
        public int Fine;            // what the offence costs, for a route that has no settlement of its own (none today: every caller bills first)
        public int Police;          // what it does to the player's standing with the police
        public int Public;          // ...and with everyone else
        public bool Custodial;      // false for the two record-only kinds
    }

    /// <summary>
    /// The offence table, and the sums done with it: how long, what bail, what a lawyer takes off.
    /// Everything a player might want to tune about custody is here. The fines for the offences
    /// themselves stay where they were (CrimeLaw in Crime/CrimeCatalogue, PolicePursuit's
    /// constants): whoever stops the player charges them, once, and the charge sheet shows what
    /// was paid.
    /// </summary>
    public static class LawBook
    {
        public const int MostHours = 48;            // "a day or two, max": nothing is ever longer
        public const int LeastHours = 1;
        public const float OtherChargesShare = 0.25f;   // each further charge adds this much of its own time to the longest
        public const float RepeatStep = 0.15f;      // per time held in the last RepeatDays
        public const float RepeatMost = 1.6f;
        public const int RepeatDays = 14;
        public const int LawyerFee = 750;
        public const int LawyerFeeLiked = 600;      // for somebody the city thinks well of (Reputation.Public of 70 or more)
        public const int ImpoundFee = 300;          // the city pound's, whatever the car was taken for (Licences charges the same)
        public const int WastedCallsForCustody = 5; // the fifth call about nothing in one day

        // What the cell does to the body: never worse than this on the way out.
        public const float ReleaseEnergy = 45f;
        public const float ReleaseHunger = 50f;

        private static readonly ChargeDef[] Table =
        {
            new ChargeDef { Id = Charge.WastedCalls, Name = "reallife_law_charge_wasted_calls", HoursMin = 2, HoursMax = 4, Bail = 300, Fine = 0, Police = -6, Public = -1, Custodial = true },
            new ChargeDef { Id = Charge.FledStop, Name = "reallife_law_charge_fled_stop", HoursMin = 4, HoursMax = 8, Bail = 1500, Fine = 900, Police = -10, Public = -2, Custodial = true },
            new ChargeDef { Id = Charge.HitAndRun, Name = "reallife_law_charge_hit_and_run", HoursMin = 8, HoursMax = 16, Bail = 3000, Fine = 600, Police = -12, Public = -4, Custodial = true },
            new ChargeDef { Id = Charge.InjuryFled, Name = "reallife_law_charge_injury_fled", HoursMin = 18, HoursMax = 30, Bail = 8000, Fine = 6000, Police = -16, Public = -8, Custodial = true },
            new ChargeDef { Id = Charge.DeathStayed, Name = "reallife_law_charge_death_stayed", HoursMin = 24, HoursMax = 36, Bail = 0, Fine = 5000, Police = -14, Public = -10, Custodial = true },
            new ChargeDef { Id = Charge.DeathFled, Name = "reallife_law_charge_death_fled", HoursMin = 36, HoursMax = 48, Bail = 0, Fine = 20000, Police = -25, Public = -15, Custodial = true },
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

        public static bool In(int charges, Charge charge) => (charges & Bit(charge)) != 0;

        /// <summary>How much longer somebody is held who has been held before: 1 for a first time, never above <see cref="RepeatMost"/>.</summary>
        public static float RepeatFactor(int timesHeldLately)
        {
            float factor = 1f + RepeatStep * Math.Max(0, timesHeldLately);
            return factor > RepeatMost ? RepeatMost : factor;
        }

        /// <summary>
        /// Hours in the cell for a set of charges: the longest of them in full (each rolled between
        /// its two ends), a quarter of each of the others on top, times the repeat factor, and never
        /// more than two days whatever is on the sheet.
        /// </summary>
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

        /// <summary>The fines of a set of charges as the table has them: for a new route into a cell that has no bill of its own to pass on.</summary>
        public static int Fine(int charges)
        {
            int fine = 0;
            for (int i = 0; i < Table.Length; i++)
            {
                if ((charges & (1 << i)) != 0)
                {
                    fine += Table[i].Fine;
                }
            }
            return fine;
        }

        /// <summary>The most serious charge of a set (the last in the table that is in it).</summary>
        public static Charge Worst(int charges)
        {
            for (int i = Table.Length - 1; i >= 0; i--)
            {
                if (Table[i].Custodial && (charges & (1 << i)) != 0)
                {
                    return Table[i].Id;
                }
            }
            for (int i = Table.Length - 1; i >= 0; i--)
            {
                if ((charges & (1 << i)) != 0)
                {
                    return Table[i].Id;
                }
            }
            return Charge.Citation;
        }

        /// <summary>What a lawyer leaves of the time still to serve: two thirds, and never less than a quarter of an hour.</summary>
        public static float AfterLawyer(float minutesLeft)
        {
            float left = minutesLeft * 2f / 3f;
            return left < 15f ? Math.Min(15f, minutesLeft) : left;
        }
    }
}
