using System;

namespace RealLifeOverhaul.Health
{
    /// <summary>How the player is living, read once a day from Home's needs and the game's own. What is not known says so with its middle value.</summary>
    public struct HealthFactors
    {
        public int Energy, Hunger;   // the game's own, 0 to 100
        public int SleepDebt;        // hours owed (Home); 0 when Home's sleep is off
        public int Diet;             // 0 poor, 1 fair or not known, 2 good
        public int Fitness;          // 0 to 100; 50 when not known
        public bool Treated;         // Home's illness is on a prescription

        public static HealthFactors Middling => new HealthFactors { Energy = 100, Hunger = 100, Diet = 1, Fitness = 50 };
    }

    /// <summary>What a day did to the player's case.</summary>
    public enum CaseEvent
    {
        None = 0,
        TurnedSerious = 1,
        Healed = 2,
        Worse = 3      // another neglected day, said as a warning
    }

    /// <summary>
    /// The rules of health that are nobody's in particular: the seeded draws, the flu's wave, what the player's way of living does to a chance, the day of a case (rest and
    /// care against neglect, turning serious, healing), injuries, an accident at work, a stay's length and a rival's day. Every draw is a hash of the save's seed, a person
    /// and a day, so the same save gives the same answer on every machine and after every load. Game-free. COST: a few operations a call, nothing allocated.
    /// </summary>
    public static class HealthRules
    {
        // The slots of the player's own draws: a draw is the seed, the day, the slot and a number of its own.
        public const int SlotWave = 101, SlotContact = 102, SlotTurn = 103, SlotInjury = 104, SlotWork = 105, SlotNotice = 106, SlotHome = 200;

        /// <summary>A hash of four numbers, the same on every machine.</summary>
        public static uint Hash(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ (uint)c * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A draw from 0 to just under <paramref name="range"/>.</summary>
        public static int Roll(int seed, int a, int b, int c, int range) => range <= 1 ? 0 : (int)(Hash(seed, a, b, c) % (uint)range);

        /// <summary>A draw from 0 to just under 1, for Home's own chances: the same for a day, a slot and the number of the draw of that slot that day.</summary>
        public static float Unit(int seed, int day, int slot, int number) => Roll(seed, day, SlotHome + slot, 7 + Math.Max(0, number), 1000000) / 1000000f;

        /// <summary>
        /// One of Home's chances is drawn (slot 1 to 8): the draw is the save's seed, the day, the slot and how many times that chance was drawn that day already, kept in the
        /// state. Each event of the day (a shower of rain, a meal, a night in the car) gets a draw of its own, as Home's own random number gave it, and a load goes on with the next
        /// number: a day loaded again changes nothing. The day's first draw of a slot is what it was before the count.
        /// </summary>
        public static float Draw(HealthState s, int day, int slot)
        {
            if (s.DrawDay != day)
            {
                s.DrawDay = day;
                Array.Clear(s.Draws, 0, s.Draws.Length);
            }
            int k = HealthCatalogue.Clamp(slot, 0, HealthCatalogue.DrawSlots - 1);
            int number = s.Draws[k];
            s.Draws[k] = Math.Min(1000000, number + 1);
            return Unit(s.Seed, day, slot, number);
        }

        /// <summary>A text's own number, the same on every machine (a member of staff's id).</summary>
        public static int IdOf(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; id != null && i < id.Length; i++)
                {
                    h = (h ^ id[i]) * 16777619u;
                }
                return (int)(h & 0x7fffffff);
            }
        }

        public static int At(int amount, int level) => (int)Math.Round(PriceRules.At((float)amount, level));

        // ---- the calendar

        /// <summary>The year a day is in, from 0.</summary>
        public static int YearOf(int day, int yearDays) => (Math.Max(1, day) - 1) / Math.Max(1, yearDays);

        /// <summary>The month a day is in, counted from the save's first day: twelfths of the year, as the living economy counts them.</summary>
        public static int MonthOf(int day, int monthDays) => (Math.Max(1, day) - 1) / Math.Max(1, monthDays);

        /// <summary>The days of a month: a twelfth of the year, three at least.</summary>
        public static int MonthDays(int yearDays) => Math.Max(3, yearDays / 12);

        // ---- the flu's wave

        /// <summary>How long a year's wave lasts, in days.</summary>
        public static int WaveDays(int yearDays) => Math.Max(HealthCatalogue.WaveDaysLeast, yearDays * HealthCatalogue.WaveYearShare / 1000);

        /// <summary>The day the wave of a year (from 0) is at its top: in the first tenth of the year, midwinter as the calendar has it.</summary>
        public static int WaveTop(int seed, int year, int yearDays)
        {
            yearDays = Math.Max(1, yearDays);
            return year * yearDays + 1 + Roll(seed, year, 11, 0, Math.Max(1, yearDays / 10));
        }

        /// <summary>How hard a year's wave is, thousandths of the average.</summary>
        public static int WaveSeverity(int seed, int year) =>
            HealthCatalogue.WaveSeverityLeast + Roll(seed, year, 12, 0, HealthCatalogue.WaveSeverityMost - HealthCatalogue.WaveSeverityLeast + 1);

        /// <summary>
        /// The wave on a day, thousandths of an average wave's top: nothing outside it, rising to the year's severity at its top and falling again. A wave that begins before
        /// the year turns is the next year's, so a day reads the wave of its own year and of the one after.
        /// </summary>
        public static int Wave(int seed, int day, int yearDays)
        {
            yearDays = Math.Max(1, yearDays);
            int half = Math.Max(1, WaveDays(yearDays) / 2), year = YearOf(day, yearDays), best = 0;
            for (int y = year; y <= year + 1; y++)
            {
                int away = Math.Abs(day - WaveTop(seed, y, yearDays));
                if (away < half)
                {
                    best = Math.Max(best, WaveSeverity(seed, y) * (half - away) / half);
                }
            }
            return best;
        }

        // ---- the player

        /// <summary>
        /// What the player's way of living does to a chance, thousandths: sleep owed doubles it, a poor diet adds half and a good one takes a quarter off, and being fit takes
        /// a quarter off. Held between a half and three times, so staying well is in the player's hands and never certain either way.
        /// </summary>
        public static int Lifestyle(HealthFactors f)
        {
            long share = 1000;
            share = f.SleepDebt > HealthCatalogue.RestDebt ? share * 2 : share;
            share = f.Diet <= 0 ? share * 1500 / 1000 : f.Diet >= 2 ? share * 750 / 1000 : share;
            share = f.Fitness >= 70 ? share * 750 / 1000 : share;
            return (int)HealthCatalogue.Clamp(share, HealthCatalogue.LifestyleLeast, HealthCatalogue.LifestyleMost);
        }

        /// <summary>The player's chance today of catching the wave's flu, in a hundred thousand.</summary>
        public static int WaveChance(int wave, HealthFactors f) => (int)((long)HealthCatalogue.PlayerWavePeak * wave / 1000 * Lifestyle(f) / 1000);

        /// <summary>The player's chance today of catching what somebody at home has, in a hundred thousand.</summary>
        public static int ContactChance(int illAtHome, HealthFactors f) =>
            (int)((long)HealthCatalogue.PlayerContact * HealthCatalogue.Clamp(illAtHome, 0, HealthCatalogue.PlayerContactMost) * Lifestyle(f) / 1000);

        /// <summary>
        /// What the player catches today from the city and from home, or nothing: the flu from the wave first, then a cold from somebody ill at home. Nothing in a game's
        /// first days. One draw each a day, whatever the hour and however often the day is loaded.
        /// </summary>
        public static Condition PlayerCatch(int seed, int day, int wave, int illAtHome, HealthFactors f)
        {
            if (day < HealthCatalogue.SafeDays)
            {
                return Condition.None;
            }
            if (Roll(seed, day, SlotWave, 0, 100000) < WaveChance(wave, f))
            {
                return Condition.Flu;
            }
            return Roll(seed, day, SlotContact, 0, 100000) < ContactChance(illAtHome, f) ? Condition.Cold : Condition.None;
        }

        // ---- a case's day

        /// <summary>True for a day of rest or care: the case is treated, or the player slept, ate and kept their strength.</summary>
        public static bool Rested(HealthCase c, HealthFactors f) =>
            c.Treated || f.Treated || (f.SleepDebt <= HealthCatalogue.RestDebt && f.Energy >= HealthCatalogue.RestEnergy && f.Hunger >= HealthCatalogue.RestHunger);

        /// <summary>
        /// One day of the player's case, lived once (a day already lived changes nothing): rest and care take a day of neglect off, a day without adds one; an illness that can
        /// turn does so only after <see cref="HealthCatalogue.NeglectDays"/> such days and never once treated; an injury seen to heals on its day. A serious case never ends by
        /// itself and never kills: it waits for the hospital.
        /// </summary>
        public static CaseEvent CaseDay(HealthCase c, HealthFactors f, int seed, int day)
        {
            if (!c.Open || day <= c.LastDay)
            {
                return CaseEvent.None;
            }
            c.LastDay = day;
            if (HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind) && !c.Serious && c.Until > 0 && day >= c.Until)
            {
                return CaseEvent.Healed;
            }
            if (c.Serious)
            {
                return CaseEvent.None;
            }
            if (Rested(c, f))
            {
                c.Neglect = Math.Max(0, c.Neglect - 1);
                return CaseEvent.None;
            }
            c.Neglect++;
            if (c.Treated || !HealthCatalogue.Is(HealthCatalogue.CanTurn, c.Kind) || c.Neglect < HealthCatalogue.NeglectDays)
            {
                return CaseEvent.None;
            }
            int chance = c.Neglect >= HealthCatalogue.NeglectCertain ? 1000 : HealthCatalogue.TurnChance;
            if (Roll(seed, day, SlotTurn, (int)c.Kind, 1000) < chance)
            {
                c.Serious = true;
                return CaseEvent.TurnedSerious;
            }
            return CaseEvent.Worse;
        }

        /// <summary>
        /// An illness of Home's begins, or goes on, as the case: a new one is opened on the day it really began (<paramref name="since"/>, Home's own record; today when that is
        /// not known), never a later one, so cover bought before it does not wait on the day Health first saw it. The same one is left as it is.
        /// </summary>
        public static bool Mirror(HealthCase c, Condition home, bool treated, int day, int since)
        {
            if (home == Condition.None || HealthCatalogue.Is(HealthCatalogue.Injury, home))
            {
                return false;
            }
            if (c.Open && (c.Kind == home || HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind)))
            {
                c.Treated |= treated && c.Kind == home;
                return false;   // the same illness, or an injury that is the case already (one at a time, the worse one kept)
            }
            c.Clear();
            c.Kind = home;
            c.Began = since > 0 && since < day ? since : day;
            c.Treated = treated;
            return true;
        }

        // ---- injuries

        /// <summary>
        /// The injury a hurt of so many hours is (LifeEvents.Injure): none under a day, a sprain from a day, and from two days a broken arm or, one time in three, a
        /// concussion, drawn from the save's seed and the injury's own number.
        /// </summary>
        public static Condition InjuryOf(int seed, int hours, int number)
        {
            if (hours < HealthCatalogue.MinorHurtHours)
            {
                return Condition.None;
            }
            if (hours < HealthCatalogue.MajorHurtHours)
            {
                return Condition.Sprain;
            }
            return Roll(seed, number, SlotInjury, 0, 3) == 0 ? Condition.Concussion : Condition.BrokenArm;
        }

        /// <summary>
        /// An injury becomes the case: it takes the place of a milder one or of an illness that is not serious, and never of a serious case or a worse injury. A serious one
        /// (a broken arm, a concussion) needs the hospital and does not heal until it has been there; a sprain heals by itself in its days.
        /// </summary>
        public static bool Injure(HealthCase c, Condition injury, int seed, int day, int number, bool atWork)
        {
            if (!HealthCatalogue.Is(HealthCatalogue.Injury, injury) || c.Serious || (HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind) && c.Kind >= injury))
            {
                return false;
            }
            c.Clear();
            c.Kind = injury;
            c.Began = day;
            c.Work = atWork;
            c.LastDay = day;
            c.Serious = HealthCatalogue.Is(HealthCatalogue.SeriousAtOnce, injury);
            c.Until = c.Serious ? 0 : day + Course(seed, number, day, injury);
            return true;
        }

        /// <summary>The days a condition runs for somebody, between its least and its most, from a draw of their own.</summary>
        public static int Course(int seed, int who, int day, Condition c)
        {
            int k = HealthCatalogue.Clamp((int)c, 0, (int)Condition.Count - 1);
            int least = HealthCatalogue.CourseLeast[k], most = HealthCatalogue.CourseMost[k];
            return least + Roll(seed, who, day, 31 + k, most - least + 1);
        }

        /// <summary>
        /// An accident on a shift worked at a risky trade (risk 0 none, 1 some, 2 high), drawn once a day: a sprain, or now and then a broken arm. Twice as likely worked
        /// without the day's rest, so it is in the player's hands too.
        /// </summary>
        public static Condition WorkAccident(int seed, int day, int risk, HealthFactors f)
        {
            int chance = risk >= 2 ? HealthCatalogue.WorkAccidentHigh : risk == 1 ? HealthCatalogue.WorkAccidentSome : 0;
            if (chance == 0)
            {
                return Condition.None;
            }
            bool tired = f.SleepDebt > HealthCatalogue.RestDebt || f.Energy < HealthCatalogue.RestEnergy;
            chance *= tired ? HealthCatalogue.WorkAccidentTired : 1;
            if (Roll(seed, day, SlotWork, 0, 100000) >= chance)
            {
                return Condition.None;
            }
            return Roll(seed, day, SlotWork, 1, 1000) < HealthCatalogue.WorkArmShare ? Condition.BrokenArm : Condition.Sprain;
        }

        // ---- the hospital

        /// <summary>The days of a stay for a condition, held to the longest.</summary>
        public static int StayDays(Condition c) =>
            HealthCatalogue.Clamp(HealthCatalogue.StayDays[HealthCatalogue.Clamp((int)c, 0, (int)Condition.Count - 1)], 1, HealthCatalogue.StayMost);

        /// <summary>The minute of the save a stay of so many days ends: eight in the morning, so many mornings on.</summary>
        public static int StayUntil(int nowMinutes, int days)
        {
            int day = nowMinutes / 1440, minute = nowMinutes % 1440;
            int first = minute < 8 * 60 ? day : day + 1;   // the next eight o'clock
            return (first + Math.Max(1, days) - 1) * 1440 + 8 * 60;
        }

        /// <summary>
        /// The stay is over: the case is treated. An illness is gone (Home is told by the director); an injury is no longer serious and heals at home in its days, in a
        /// cast or at rest.
        /// </summary>
        public static void Discharged(HealthCase c, int day)
        {
            if (!c.Open)
            {
                return;
            }
            if (HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind))
            {
                c.Serious = false;
                c.Treated = true;
                c.Neglect = 0;
                c.Until = day + HealthCatalogue.HealDays[(int)c.Kind];
                return;
            }
            c.Clear();
        }

        /// <summary>A prescription or a dressing at the doctor's: the case is treated; an injury that is not serious heals in half of what is left.</summary>
        public static void Treat(HealthCase c, int day)
        {
            if (!c.Open || c.Treated)
            {
                return;
            }
            c.Treated = true;
            c.Neglect = 0;
            if (HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind) && !c.Serious && c.Until > day)
            {
                c.Until = day + (c.Until - day + 1) / 2;
            }
        }

        // ---- the rivals

        /// <summary>What a rival pays of a bill: from the worth over their floor only, never under nothing.</summary>
        public static long RivalPaid(long amount, long worth, long floor) => Math.Max(0L, Math.Min(amount, worth - floor));

        /// <summary>
        /// A rival tycoon's day: now and then they fall ill, more in the wave, for a few days; a few cases need the hospital. Nothing while they are ill already. True if
        /// one began today.
        /// </summary>
        public static bool RivalDay(RivalHealth r, int seed, int tycoon, int day, int wave)
        {
            if (day <= r.LastDay)
            {
                return false;
            }
            r.LastDay = day;
            if (r.Kind != Condition.None && day <= r.Until)
            {
                return false;
            }
            r.Kind = Condition.None;
            r.Serious = false;
            int chance = HealthCatalogue.RivalADay + (int)((long)HealthCatalogue.RivalWavePeak * wave / 1000);
            if (Roll(seed, 7000 + tycoon, day, 1, 100000) >= chance)
            {
                return false;
            }
            bool flu = Roll(seed, 7000 + tycoon, day, 2, HealthCatalogue.RivalADay + HealthCatalogue.RivalWavePeak) < HealthCatalogue.RivalWavePeak * wave / 1000 + HealthCatalogue.RivalADay / 3;
            r.Kind = flu ? Condition.Flu : Roll(seed, 7000 + tycoon, day, 3, 2) == 0 ? Condition.Cold : Condition.Exhaustion;
            r.Day = day;
            r.Serious = r.Kind != Condition.Cold && Roll(seed, 7000 + tycoon, day, 4, 1000) < HealthCatalogue.RivalSerious;
            int days = HealthCatalogue.RivalDaysLeast + Roll(seed, 7000 + tycoon, day, 5, HealthCatalogue.RivalDaysMost - HealthCatalogue.RivalDaysLeast + 1);
            r.Until = day + days - 1 + (r.Serious ? StayDays(r.Kind) : 0);
            r.Bill = 0;
            return true;
        }
    }
}
