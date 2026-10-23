using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>The rivals' rules, part: the player's counters, a takeover of a holding, and the rich list.</summary>
    public static partial class RivalRules
    {
        /// <summary>What a counter costs now: a share of the worth, bounded at today's prices (not a takeover).</summary>
        public static long CounterCost(RivalCounter kind, long worth, int level)
        {
            int k = (int)kind;
            if (k < 0 || k >= (int)RivalCounter.Takeover)
            {
                return 0;
            }
            long most = At(RivalCatalogue.CounterMost[k], level) * RivalCatalogue.CounterTierScale[Tier(worth, level)];
            return RivalCatalogue.Clamp(Math.Max(0L, worth) / 10000L * RivalCatalogue.CounterShare[k], At(RivalCatalogue.CounterLeast[k], level), most);
        }

        /// <summary>Why a counter cannot be used against a tycoon today (None: it can), and the move it would stop or null.</summary>
        public static RivalWhy CanCounter(RivalFile f, int tycoon, RivalCounter kind, int day, out RivalMove target)
        {
            target = null;
            if (f == null || tycoon < 0 || tycoon >= f.Tycoons.Length || !f.Tycoons[tycoon].Active)
            {
                return RivalWhy.Out;
            }
            Tycoon t = f.Tycoons[tycoon];
            int k = (int)kind;
            if (kind != RivalCounter.Takeover && day - t.CounterDay[k] < RivalCatalogue.CounterRestDays[k])
            {
                return RivalWhy.Rest;
            }
            switch (kind)
            {
                case RivalCounter.Outbid:
                    target = FirstOf(f, tycoon, RivalMoveKind.Property, RivalMoveKind.Outlet, false);
                    return target == null ? RivalWhy.Nothing : RivalWhy.None;
                case RivalCounter.Lobby:
                    target = FirstOf(f, tycoon, RivalMoveKind.Lobby, RivalMoveKind.Lobby, true);
                    return RivalWhy.None;
                case RivalCounter.Press:
                {
                    RivalMove pending = f.Pending();
                    // A bid is answered by the founder's own defences, not a piece in the paper.
                    target = pending != null && pending.Tycoon == tycoon && pending.Kind != RivalMoveKind.Bid ? pending : null;
                    return RivalWhy.None;
                }
                default:
                    return RivalWhy.None;
            }
        }

        private static RivalMove FirstOf(RivalFile f, int tycoon, RivalMoveKind a, RivalMoveKind b, bool runningOnly)
        {
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Tycoon == tycoon && (m.Kind == a || m.Kind == b) && (runningOnly ? m.State == RivalMoveState.Running : m.Live))
                {
                    return m;
                }
            }
            return null;
        }

        /// <summary>
        /// A counter used, its cost already paid: its effect and a note of it, returned. The checks of
        /// <see cref="CanCounter"/> are made again; a counter that cannot be used does nothing and returns null.
        /// </summary>
        public static RivalCounterNote Counter(RivalFile f, int tycoon, RivalCounter kind, int day, long cost, int level)
        {
            if (kind == RivalCounter.Takeover || CanCounter(f, tycoon, kind, day, out RivalMove target) != RivalWhy.None)
            {
                return null;
            }
            Tycoon t = f.Tycoons[tycoon];
            switch (kind)
            {
                case RivalCounter.Outbid:
                    target.State = RivalMoveState.Stopped;
                    break;
                case RivalCounter.Poach:
                    t.QuietUntil = Math.Max(t.QuietUntil, day + RivalCatalogue.PoachQuietDays);
                    t.SlowUntil = Math.Max(t.SlowUntil, day + RivalCatalogue.PoachSlowDays);
                    break;
                case RivalCounter.Press:
                    if (target != null)
                    {
                        target.State = RivalMoveState.Stopped;
                    }
                    else
                    {
                        t.QuietUntil = Math.Max(t.QuietUntil, day + RivalCatalogue.PressDelayDays);
                        t.Worth = Math.Max(At(t.Def.StartWorth, level), t.Worth - t.Worth / 1000L * RivalCatalogue.PressWorthCut);
                    }
                    break;
                case RivalCounter.Lobby:
                    if (target != null)
                    {
                        target.State = RivalMoveState.Stopped;
                    }
                    t.BarredUntil = Math.Max(t.BarredUntil, day + RivalCatalogue.LobbyBarDays);
                    break;
            }
            t.CounterDay[(int)kind] = day;   // (an answer that stops a move ends it: a second finds nothing of it to stop)
            return Note(f, tycoon, kind, day, cost, target != null ? target.Id : 0);
        }

        private static RivalCounterNote Note(RivalFile f, int tycoon, RivalCounter kind, int day, long cost, int move)
        {
            var n = new RivalCounterNote { Id = f.NextId++, Tycoon = tycoon, Kind = kind, Day = day, Cost = cost, Move = move };
            f.Counters.Add(n);
            while (f.Counters.Count > RivalCatalogue.CountersKept)
            {
                f.Counters.RemoveAt(0);
            }
            return n;
        }

        /// <summary>
        /// A takeover's cost (the shares not held at the price, plus the premium) and its break-up money after
        /// <see cref="RivalCatalogue.BreakUpDays"/>. Prices in cents, money in dollars.
        /// </summary>
        public static void TakeoverTerms(long shares, long held, int midCents, long fortune, out long cost, out long breakUp)
        {
            shares = Math.Max(1L, shares);
            long others = Math.Max(0L, shares - Math.Max(0L, held));
            long cents = Math.Max(Math.Max(0, midCents), WorthCents(fortune, shares));   // (the holding is the tycoon's fortune: never bought for less than it is worth a share)
            long value = others / 100L * cents + others % 100L * cents / 100L;   // (cents a share times shares, in dollars, without passing the long's range)
            cost = value + value / 10000L * RivalCatalogue.TakeoverPremiumBp;
            breakUp = value / 1000L * RivalCatalogue.BreakUpPermille;
        }

        /// <summary>A holding's worth a share in cents: the fortune is the founder's stake (<see cref="Markets.MarketCatalogue.HoldingFounderBp"/>).</summary>
        public static long WorthCents(long fortune, long shares)
        {
            long whole = fortune > 0 ? fortune / Markets.MarketCatalogue.HoldingFounderBp * 10000L : 0L;
            return shares > 0 && whole > 0 ? whole / shares * 100L + whole % shares * 100L / shares : 0L;
        }

        /// <summary>Why the player cannot take a holding over: not listed here, or they hold less than the most allowed.</summary>
        public static RivalWhy CanTakeOver(RivalFile f, int tycoon, bool listed, long held, long most)
        {
            if (f == null || tycoon < 0 || tycoon >= f.Tycoons.Length || !f.Tycoons[tycoon].Active)
            {
                return RivalWhy.Out;
            }
            if (!listed)
            {
                return RivalWhy.NoHolding;
            }
            return held >= most && most > 0 ? RivalWhy.None : RivalWhy.Short;
        }

        /// <summary>A tycoon taken over: out for good, their moves stopped, the break-up money to come. Returns the note.</summary>
        public static RivalCounterNote Defeat(RivalFile f, int tycoon, int day, long cost, long breakUp)
        {
            Tycoon t = f.Tycoons[tycoon];
            t.DefeatedDay = day;
            t.Listed = false;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                if (f.Moves[i].Tycoon == tycoon && f.Moves[i].Live)
                {
                    f.Moves[i].State = RivalMoveState.Stopped;
                }
            }
            t.BreakUp = Math.Max(0L, breakUp);
            t.BreakUpDay = day + RivalCatalogue.BreakUpDays;
            return Note(f, tycoon, RivalCounter.Takeover, day, cost, 0);
        }

        /// <summary>A tycoon's break-up money due by a day, taken off the file; 0 when none is.</summary>
        public static long BreakUpDue(Tycoon t, int day)
        {
            if (t == null || t.BreakUp <= 0 || day < t.BreakUpDay)
            {
                return 0;
            }
            long due = t.BreakUp;
            t.BreakUp = 0;
            t.BreakUpDay = 0;
            return due;
        }

        /// <summary>The break-up money still to come, all takeovers together, and the soonest pay day (0 for none).</summary>
        public static long BreakUpToCome(RivalFile f, out int day)
        {
            long total = 0;
            day = 0;
            for (int i = 0; f != null && i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                if (t.BreakUp > 0)
                {
                    total += t.BreakUp;
                    day = day == 0 ? t.BreakUpDay : Math.Min(day, t.BreakUpDay);
                }
            }
            return total;
        }

        /// <summary>The rich list, richest first, equals by who they are so the order is the same everywhere.</summary>
        public static void RichList(RivalFile f, long playerWorth, int level, string playerName, List<RichRow> into)
        {
            into.Clear();
            for (int i = 0; f != null && i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                if (t.Active)
                {
                    into.Add(new RichRow { Who = i, Worth = t.Worth, WeekAgo = t.WeekAgo, Name = t.Def.Name });
                }
            }
            into.Add(new RichRow { Who = RichRow.Player, Worth = Math.Max(0L, playerWorth), WeekAgo = f != null ? f.PlayerWeekAgo : 0, Name = playerName ?? string.Empty });
            for (int i = 0; i < RivalCatalogue.Fortunes.Length; i++)
            {
                into.Add(new RichRow { Who = RichRow.FortuneBase + i, Worth = At(RivalCatalogue.Fortunes[i].Worth, level), Name = RivalCatalogue.Fortunes[i].Name });
            }
            into.Sort(Richer);
        }

        private static int Richer(RichRow a, RichRow b)
        {
            int c = b.Worth.CompareTo(a.Worth);
            return c != 0 ? c : a.Who.CompareTo(b.Who);
        }

        /// <summary>The player's place on the rich list, from 1.</summary>
        public static int Rank(RivalFile f, long playerWorth, int level)
        {
            int rank = 1;
            var me = new RichRow { Who = RichRow.Player, Worth = Math.Max(0L, playerWorth) };
            for (int i = 0; f != null && i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                rank += t.Active && Richer(new RichRow { Who = i, Worth = t.Worth }, me) < 0 ? 1 : 0;
            }
            for (int i = 0; i < RivalCatalogue.Fortunes.Length; i++)
            {
                rank += Richer(new RichRow { Who = RichRow.FortuneBase + i, Worth = At(RivalCatalogue.Fortunes[i].Worth, level) }, me) < 0 ? 1 : 0;
            }
            return rank;
        }
    }
}
