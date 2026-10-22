using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>Why a counter cannot be used now. Each has a sentence of its own (reallife_rival_why_*).</summary>
    public enum RivalWhy
    {
        None = 0,
        Out = 1,        // that tycoon has not come forward, or is out already
        Rest = 2,       // the same counter was used against them too lately
        Nothing = 3,    // nothing of theirs for it to stop
        NoHolding = 4,  // a takeover: their holding is not on the stock market here
        Short = 5,      // a takeover: the player does not hold the most of it one holder may have
        Money = 6,      // the player has not the money
        Count = 7
    }

    /// <summary>What one day did, for the director to tell the player.</summary>
    public struct RivalDayReport
    {
        public int Days;           // days played (more than one after a long sleep or a load)
        public int Activated;      // a bit for each tycoon who came forward today
        public int Announced;      // the id of the move announced today, or 0
        public bool Top;           // the player topped the rich list for the first time
        public bool Week;          // a weekly mark was made today (the rich list's day)
    }

    /// <summary>
    /// The rival tycoons' rules: growth, coming forward, the moves and their size, the player's counters and the rich
    /// list. Game-free, checked offline. Every draw is a hash of the save's seed, the day and its purpose, so the same save
    /// gives the same rivals on every machine. A tycoon grows by the city's cycle and closes part of the gap to their aim,
    /// a share of the player's worth. One move at a time for all rivals, announced <see cref="RivalCatalogue.TelegraphDays"/>
    /// days ahead, then the tier's gap. Cost: a few dozen sums once a game day; nothing per frame.
    /// </summary>
    public static partial class RivalRules
    {
        public const int CatchUpMost = 60;          // days of growth played at once after a long gap; the rest are skipped (a save left alone for a year)

        private const int PurposeTycoon = 1, PurposeKind = 2, PurposeJitter = 3, PurposeSeed = 5;

        /// <summary>A number from 0 to <paramref name="n"/> less one, the same on every machine for the same inputs.</summary>
        public static int Draw(int seed, int purpose, int a, int b, int n)
        {
            if (n <= 1)
            {
                return 0;
            }
            unchecked
            {
                ulong x = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)purpose << 48 ^ (ulong)(uint)a << 20 ^ (ulong)(uint)b;
                return (int)(Mix(x) % (ulong)n);
            }
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>A new file's seed from the playthrough's id and the day: the same save gives the same seed.</summary>
        public static int SeedFrom(ulong playthrough, int day)
        {
            unchecked
            {
                return (int)(Mix(playthrough ^ (ulong)(uint)day * 0xD1B54A32D192ED03UL ^ PurposeSeed) & 0x7FFFFFFF);
            }
        }

        /// <summary>An amount at the start's prices, at a price level (tenths of a point).</summary>
        public static long At(long amount, int level)
        {
            return amount * RivalCatalogue.Clamp(level, 500, 4000) / 1000L;
        }

        /// <summary>The player's tier by their worth, the thresholds at today's prices.</summary>
        public static int Tier(long worth, int level)
        {
            int tier = 0;
            for (int i = 1; i < RivalCatalogue.Tiers; i++)
            {
                if (worth >= At(RivalCatalogue.TierFrom[i], level))
                {
                    tier = i;
                }
            }
            return tier;
        }

        /// <summary>Days between two moves for a tier and a strength, before the drawn jitter.</summary>
        public static int Gap(int tier, int strength)
        {
            tier = RivalCatalogue.Clamp(tier, 0, RivalCatalogue.Tiers - 1);
            strength = RivalCatalogue.Clamp(strength, 0, RivalCatalogue.Strengths - 1);
            return Math.Max(1, RivalCatalogue.GapDays[tier] * RivalCatalogue.StrengthGap[strength] / 1000);
        }

        /// <summary>How hard a move hits at a tier and a strength, per mille of its Normal size.</summary>
        public static int Power(int tier, int strength)
        {
            tier = RivalCatalogue.Clamp(tier, 0, RivalCatalogue.Tiers - 1);
            strength = RivalCatalogue.Clamp(strength, 0, RivalCatalogue.Strengths - 1);
            return RivalCatalogue.TierPower[tier] * RivalCatalogue.StrengthPower[strength] / 1000;
        }

        /// <summary>A new file: nobody has come forward, and no move before the first tier's gap.</summary>
        public static RivalFile Open(int seed, int day, int level)
        {
            var f = new RivalFile { Seed = seed, StartDay = day, NextMoveDay = day + RivalCatalogue.GapDays[1], WeekDay = day };
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                t.Worth = At(t.Def.StartWorth, level);
                t.WeekAgo = t.Worth;
            }
            return f;
        }

        /// <summary>The fortune a tycoon aims for: their share of the player's worth, never under their start.</summary>
        public static long Aim(Tycoon t, long playerWorth, int tier, int strength, int level)
        {
            long floor = At(t.Def.StartWorth, level);
            if (tier <= 0 || playerWorth <= 0)
            {
                return floor;
            }
            strength = RivalCatalogue.Clamp(strength, 0, RivalCatalogue.Strengths - 1);
            long aim = playerWorth / 1000L * (t.Def.TargetPermille * RivalCatalogue.StrengthAim[strength] / 1000);
            return Math.Max(floor, aim);
        }

        /// <summary>A tycoon's worth after one day: the cycle, the catch-up while behind, half a gain while slowed.</summary>
        public static long Grow(Tycoon t, long aim, int cycle, int day, int level)
        {
            long worth = Math.Max(0L, t.Worth);
            long own = worth / RivalCatalogue.MonthDays * RivalCatalogue.CycleGrowthBp[RivalCatalogue.Clamp(cycle, 0, 3)] / 10000L;
            long catchUp = aim > worth ? Math.Min((aim - worth) / RivalCatalogue.CatchDays, worth / 10000L * RivalCatalogue.CatchMostBp) : 0L;
            long gain = own + catchUp;
            if (gain > 0 && day < t.SlowUntil)
            {
                gain = gain * RivalCatalogue.SlowShare / 1000L;
            }
            return Math.Max(At(t.Def.StartWorth, level), worth + gain);
        }

        /// <summary>
        /// The rivals' day: growth since the last day played, tycoons forward, the weekly mark, the rich list's top and a new
        /// move if due. Moves that come or end today are left to the director (<see cref="Due"/>).
        /// </summary>
        public static RivalDayReport Day(RivalFile f, RivalWorld w, int day, int strength)
        {
            var r = new RivalDayReport();
            if (f == null || w == null || day <= f.SettledDay)
            {
                return r;
            }
            int tier = Tier(w.Worth, w.Level);
            int from = f.SettledDay < 0 ? day : Math.Max(f.SettledDay + 1, day - CatchUpMost + 1);
            for (int d = from; d <= day; d++)
            {
                for (int i = 0; i < f.Tycoons.Length; i++)
                {
                    Tycoon t = f.Tycoons[i];
                    if (t.Active)
                    {
                        t.Worth = Grow(t, Aim(t, w.Worth, tier, strength, w.Level), w.Cycle, d, w.Level);
                    }
                }
                r.Days++;
            }
            r.Activated = Activate(f, tier, day, w.Level, w.Worth, strength, RivalCatalogue.ForwardPerDay);
            f.Tier = tier;
            f.PlayerWorth = w.Worth;
            if (f.WeekDay < 0 || day - f.WeekDay >= 7)
            {
                Mark(f, day);
                r.Week = true;
            }
            if (f.TopDay < 0 && AnyActive(f) && Rank(f, w.Worth, w.Level) == 1)
            {
                f.TopDay = day;
                r.Top = true;
            }
            RivalMove m = Announce(f, w, day, strength, false);
            r.Announced = m != null ? m.Id : 0;
            f.SettledDay = day;
            return r;
        }

        /// <summary>Tycoons come forward in order until the tier's number; a bit for each that came today.</summary>
        public static int Activate(RivalFile f, int tier, int day, int level, long playerWorth = 0, int strength = RivalCatalogue.Normal, int most = RivalCatalogue.TycoonCount)
        {
            int want = RivalCatalogue.ActiveAt[RivalCatalogue.Clamp(tier, 0, RivalCatalogue.Tiers - 1)], came = 0, have = 0, now = 0;
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                have += f.Tycoons[i].ActiveDay >= 0 ? 1 : 0;
            }
            for (int i = 0; i < f.Tycoons.Length && have < want && now < most; i++)
            {
                Tycoon t = f.Tycoons[i];
                if (t.ActiveDay >= 0)
                {
                    continue;
                }
                t.ActiveDay = day;
                t.Worth = Math.Max(Math.Max(t.Worth, At(t.Def.StartWorth, level)), Aim(t, playerWorth, tier, strength, level) / 1000L * RivalCatalogue.ForwardShare);
                t.WeekAgo = t.Worth;
                have++;
                now++;
                came |= 1 << i;
            }
            return came;
        }

        /// <summary>
        /// Back after standing still (off, or resting in a Going Public session): the missed days are not played and every
        /// date after the last day played moves on by them. Returns the days skipped.
        /// </summary>
        public static int Resume(RivalFile f, int day)
        {
            if (f == null || f.SettledDay < 0 || day - 1 <= f.SettledDay)
            {
                return 0;
            }
            int skipped = day - 1 - f.SettledDay;
            f.SettledDay = day - 1;
            f.NextMoveDay += skipped;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.State == RivalMoveState.Announced)
                {
                    m.Land += skipped;
                    m.End += skipped;
                }
                else if (m.State == RivalMoveState.Running)
                {
                    m.End += skipped;
                }
            }
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                t.QuietUntil += t.QuietUntil > f.SettledDay - skipped ? skipped : 0;
                t.SlowUntil += t.SlowUntil > f.SettledDay - skipped ? skipped : 0;
                t.BarredUntil += t.BarredUntil > f.SettledDay - skipped ? skipped : 0;
                t.BreakUpDay += t.BreakUp > 0 ? skipped : 0;
                t.LastMoveDay += t.LastMoveDay >= 0 ? skipped : 0;
                t.StakeDay += t.StakeDay >= 0 ? skipped : 0;
                t.BidDay += t.BidDay >= 0 ? skipped : 0;
                for (int k = 0; k < t.CounterDay.Length; k++)
                {
                    t.CounterDay[k] += t.CounterDay[k] >= 0 ? skipped : 0;
                }
            }
            if (f.WeekDay >= 0)
            {
                f.WeekDay += skipped;
            }
            return skipped;
        }

        public static bool AnyActive(RivalFile f)
        {
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                if (f.Tycoons[i].Active)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The weekly mark the rich list's change is counted from.</summary>
        public static void Mark(RivalFile f, int day)
        {
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                f.Tycoons[i].WeekAgo = f.Tycoons[i].Worth;
            }
            f.PlayerWeekAgo = f.PlayerWorth;
            f.WeekDay = day;
        }
    }
}
