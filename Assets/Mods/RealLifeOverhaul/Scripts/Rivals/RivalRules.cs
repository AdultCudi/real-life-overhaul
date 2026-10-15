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

    /// <summary>What one day did, for the director to tell the player: no text, only what happened.</summary>
    public struct RivalDayReport
    {
        public int Days;           // days played (more than one after a long sleep or a load)
        public int Activated;      // a bit for each tycoon who came forward today
        public int Announced;      // the id of the move announced today, or 0
        public bool Top;           // the player topped the rich list for the first time
        public bool Week;          // a weekly mark was made today (the rich list's day)
    }

    /// <summary>
    /// THE RIVAL TYCOONS' RULES: how their fortunes grow, when they come forward, when and what they move against the
    /// player, how big each move is, what the player's counters do, and the city's rich list. Game-free: EconomyCheck
    /// checks it (RivalChecks.cs). Every draw is a hash of the save's seed, the day and what is drawn
    /// (<see cref="Draw"/>), never the engine's random numbers: the same save played on the same days gives the same rivals
    /// on every machine.
    ///
    /// GROWTH. A tycoon who has come forward grows each day by the city's cycle (<see cref="RivalCatalogue.CycleGrowthBp"/>
    /// over thirty days) and, while behind their aim, by a part of the gap (<see cref="RivalCatalogue.CatchDays"/>, at most
    /// <see cref="RivalCatalogue.CatchMostBp"/> a day). Their aim is their share of the player's worth
    /// (<see cref="TycoonDef.TargetPermille"/>, by the strength): so they grow with the player and never run away from one
    /// who is small, and a player who passes a threshold finds them following within a season.
    ///
    /// PACING. One move at a time for all the rivals together: a move is announced, comes
    /// <see cref="RivalCatalogue.TelegraphDays"/> days later, and the next is not announced before the tier's gap
    /// (<see cref="RivalCatalogue.GapDays"/>, by the strength, and up to <see cref="RivalCatalogue.JitterDays"/> more).
    /// Below the first tier nothing moves.
    ///
    /// Cost: a day's round is a few dozen sums over three tycoons and some twenty moves, once a game day (more after a
    /// long sleep, at most <see cref="CatchUpMost"/> days). Nothing here runs per frame.
    /// </summary>
    public static class RivalRules
    {
        public const int CatchUpMost = 60;          // days of growth played at once after a long gap; the rest are skipped (a save left alone for a year)

        private const int PurposeTycoon = 1, PurposeKind = 2, PurposeJitter = 3, PurposeShop = 4, PurposeSeed = 5;

        // ---------------------------------------------------------------- draws

        /// <summary>A number from 0 to <paramref name="n"/> less one, the same for the same seed, purpose and two values on every machine.</summary>
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

        /// <summary>A seed for a new file from what the save holds (the playthrough's id and the day): the same save gives the same seed.</summary>
        public static int SeedFrom(ulong playthrough, int day)
        {
            unchecked
            {
                return (int)(Mix(playthrough ^ (ulong)(uint)day * 0xD1B54A32D192ED03UL ^ PurposeSeed) & 0x7FFFFFFF);
            }
        }

        // ---------------------------------------------------------------- money and tiers

        /// <summary>An amount written at the start's prices, at a price level (tenths of a point).</summary>
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

        /// <summary>Days between two moves for a tier and a strength, before the draw's few more.</summary>
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

        // ---------------------------------------------------------------- a new file

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

        // ---------------------------------------------------------------- growth

        /// <summary>The fortune a tycoon aims for: their share of the player's worth, never under their start; their start alone while the player is quiet.</summary>
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

        /// <summary>A tycoon's worth after one day: the city's cycle, the catch-up while behind their aim, half of a gain while slowed; never under their start.</summary>
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

        // ---------------------------------------------------------------- the day

        /// <summary>
        /// The rivals' day: every day since the last played is grown (at most <see cref="CatchUpMost"/>), tycoons come
        /// forward as the tier asks, the week's mark is made, the rich list's top is looked at, and a new move is announced
        /// if one is due. Moves that come or end today are not done here: the director does them through the other
        /// features (<see cref="Due"/>, then <see cref="Landed"/> and <see cref="Ended"/>), as only it can.
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
            r.Activated = Activate(f, tier, day, w.Level, w.Worth, strength);
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

        /// <summary>
        /// Tycoons come forward in their order until as many have as the tier asks, each worth at least their start and
        /// <see cref="RivalCatalogue.ForwardShare"/> of their aim against the player's worth; a bit for each that came today.
        /// </summary>
        public static int Activate(RivalFile f, int tier, int day, int level, long playerWorth = 0, int strength = RivalCatalogue.Normal)
        {
            int want = RivalCatalogue.ActiveAt[RivalCatalogue.Clamp(tier, 0, RivalCatalogue.Tiers - 1)], came = 0, have = 0;
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                have += f.Tycoons[i].ActiveDay >= 0 ? 1 : 0;
            }
            for (int i = 0; i < f.Tycoons.Length && have < want; i++)
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
                came |= 1 << i;
            }
            return came;
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

        // ---------------------------------------------------------------- a new move

        /// <summary>
        /// A move announced today if one is due: the player past the first tier, the gap over, nothing else announced, a
        /// tycoon who may move and a kind of move there is something for. With <paramref name="any"/> (a test) the tier and
        /// the gap are not asked. Null if none.
        /// </summary>
        public static RivalMove Announce(RivalFile f, RivalWorld w, int day, int strength, bool any)
        {
            int tier = Tier(w.Worth, w.Level);
            if (f.Pending() != null || (!any && (tier < 1 || day < f.NextMoveDay)))
            {
                return null;
            }
            int count = f.Tycoons.Length;
            int start = Draw(f.Seed, PurposeTycoon, day, 0, count);
            for (int k = 0; k < count; k++)
            {
                Tycoon t = f.Tycoons[(start + k) % count];
                if (!t.Active || day < t.QuietUntil)
                {
                    continue;
                }
                RivalMoveKind kind = PickKind(f, t, w, day, Math.Max(1, tier));
                if (kind == RivalMoveKind.Count)
                {
                    continue;
                }
                RivalMove m = Make(f, t, kind, w, day, Math.Max(1, tier), strength);
                if (m == null)
                {
                    continue;
                }
                f.NextMoveDay = day + Gap(Math.Max(1, tier), strength) + Draw(f.Seed, PurposeJitter, day, m.Id, RivalCatalogue.JitterDays + 1);
                return m;
            }
            if (!any)
            {
                f.NextMoveDay = day + 1;   // (nothing to move against today: tomorrow is asked again)
            }
            return null;
        }

        /// <summary>A move of one kind by one tycoon, announced today (a test's, or the rule's own pick). Null when there is nothing for it.</summary>
        public static RivalMove Make(RivalFile f, Tycoon t, RivalMoveKind kind, RivalWorld w, int day, int tier, int strength)
        {
            var m = new RivalMove { Id = f.NextId, Tycoon = t.Index, Kind = kind, State = RivalMoveState.Announced, Announced = day, Land = day + RivalCatalogue.TelegraphDays };
            m.End = m.Land + RivalCatalogue.MoveDays[(int)kind];
            if (!Aim(f, m, w, day))
            {
                return null;
            }
            Size(m, w, tier, strength);
            f.NextId++;
            t.LastMoveDay = day;
            f.Moves.Add(m);
            Trim(f);
            return m;
        }

        /// <summary>The kind of move a tycoon makes today: their likings, of the kinds there is something for. <see cref="RivalMoveKind.Count"/> for none.</summary>
        public static RivalMoveKind PickKind(RivalFile f, Tycoon t, RivalWorld w, int day, int tier)
        {
            int total = 0;
            int[] weights = t.Def.Weights;
            for (int k = 0; k < (int)RivalMoveKind.Count; k++)
            {
                total += Available(f, t, (RivalMoveKind)k, w, day, tier) ? weights[k] : 0;
            }
            if (total <= 0)
            {
                return RivalMoveKind.Count;
            }
            int at = Draw(f.Seed, PurposeKind, day, t.Index, total);
            for (int k = 0; k < (int)RivalMoveKind.Count; k++)
            {
                if (!Available(f, t, (RivalMoveKind)k, w, day, tier))
                {
                    continue;
                }
                at -= weights[k];
                if (at < 0)
                {
                    return (RivalMoveKind)k;
                }
            }
            return RivalMoveKind.Count;
        }

        /// <summary>Whether a tycoon could make a kind of move today: something of the player's for it, the feature it goes through running, the tier, and the counters' bars.</summary>
        public static bool Available(RivalFile f, Tycoon t, RivalMoveKind kind, RivalWorld w, int day, int tier)
        {
            bool shops = w.Shops.Count > 0;
            switch (kind)
            {
                case RivalMoveKind.Property: return shops && day >= t.BarredUntil;
                case RivalMoveKind.Outlet: return shops && LiveCount(f, RivalMoveKind.Outlet) < RivalCatalogue.OutletsMost && OutletShop(f, w) >= 0;
                case RivalMoveKind.Poach: return shops && w.Staff;
                case RivalMoveKind.Stake: return tier >= RivalCatalogue.StakeTier && w.Listed && w.Market && (t.StakeDay < 0 || day - t.StakeDay >= RivalCatalogue.BidRestDays);
                case RivalMoveKind.Bid:
                    return tier >= RivalCatalogue.BidTier && w.Listed && w.Market && t.Listed && t.StakeDay >= 0 && day - t.StakeDay <= RivalCatalogue.BidAfterStakeDays
                           && (t.BidDay < 0 || day - t.BidDay >= RivalCatalogue.BidRestDays);
                case RivalMoveKind.Lobby: return shops && w.Issues && day >= t.BarredUntil && LiveCount(f, RivalMoveKind.Lobby) == 0;
                case RivalMoveKind.Suit: return w.Court && w.Worth > 0;
                case RivalMoveKind.Press: return w.Paper || (shops && w.Issues);
                default: return false;
            }
        }

        /// <summary>Moves of a kind announced or running.</summary>
        public static int LiveCount(RivalFile f, RivalMoveKind kind)
        {
            int n = 0;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                n += f.Moves[i].Kind == kind && f.Moves[i].Live ? 1 : 0;
            }
            return n;
        }

        // What the move is aimed at: the player's best business (the most taken in a week), or, for a rival's business,
        // the best with none of a rival's of its kind in its district yet. False when there is nothing for it.
        private static bool Aim(RivalFile f, RivalMove m, RivalWorld w, int day)
        {
            switch (m.Kind)
            {
                case RivalMoveKind.Stake:
                case RivalMoveKind.Bid:
                case RivalMoveKind.Suit:
                    return true;
                case RivalMoveKind.Outlet:
                {
                    int at = OutletShop(f, w);
                    if (at < 0)
                    {
                        return false;
                    }
                    Target(m, w.Shops[at]);
                    return true;
                }
                default:
                {
                    int at = Best(w);
                    if (at < 0)
                    {
                        return m.Kind == RivalMoveKind.Press;   // (a piece in the paper needs no business)
                    }
                    Target(m, w.Shops[at]);
                    return true;
                }
            }
        }

        private static void Target(RivalMove m, in RivalShop s)
        {
            m.Place = s.Place ?? string.Empty;
            m.Name = s.Name ?? string.Empty;
            m.District = s.District ?? string.Empty;
            m.Type = s.Type ?? string.Empty;
            m.Rented = s.Rented;
        }

        /// <summary>The player's business that took the most in a week (the first of equals); -1 with none.</summary>
        public static int Best(RivalWorld w)
        {
            int best = -1;
            for (int i = 0; i < w.Shops.Count; i++)
            {
                if (best < 0 || w.Shops[i].Week > w.Shops[best].Week)
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The best business with no rival's business of its kind in its district, announced or open; -1 with none.</summary>
        public static int OutletShop(RivalFile f, RivalWorld w)
        {
            int best = -1;
            for (int i = 0; i < w.Shops.Count; i++)
            {
                RivalShop s = w.Shops[i];
                if (string.IsNullOrEmpty(s.District) || string.IsNullOrEmpty(s.Type) || HasOutlet(f, s.District, s.Type))
                {
                    continue;
                }
                if (best < 0 || s.Week > w.Shops[best].Week)
                {
                    best = i;
                }
            }
            return best;
        }

        private static bool HasOutlet(RivalFile f, string district, string type)
        {
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Outlet && m.Live && m.District == district && m.Type == type)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How big a move is at a tier and a strength, inside its bounds whatever they are.</summary>
        public static void Size(RivalMove m, RivalWorld w, int tier, int strength)
        {
            int power = Power(Math.Max(1, tier), strength);
            switch (m.Kind)
            {
                case RivalMoveKind.Property:
                    m.Amount = Math.Max(1, Math.Min(RivalCatalogue.PropertyPremiumMost, RivalCatalogue.PropertyPremium * power / 1000));
                    m.Second = m.Rented ? Math.Max(1, Math.Min(RivalCatalogue.RentRiseMost, RivalCatalogue.RentRise * power / 1000)) : 0;
                    break;
                case RivalMoveKind.Outlet:
                    m.Amount = RivalCatalogue.Clamp((RivalCatalogue.OutletDip * power + 500) / 1000, 1, RivalCatalogue.OutletDipMost);
                    break;
                case RivalMoveKind.Poach:
                    m.Amount = Math.Max(1, Math.Min(RivalCatalogue.PoachPremiumMost, RivalCatalogue.PoachPremium * power / 1000));
                    break;
                case RivalMoveKind.Lobby:
                    m.Amount = Math.Max(1, Math.Min(RivalCatalogue.LobbyWeightMost, RivalCatalogue.LobbyWeight * power / 1000));
                    break;
                case RivalMoveKind.Suit:
                    m.Amount = SuitClaim(w.Worth, power, w.Level);
                    break;
                case RivalMoveKind.Press:
                    m.Amount = RivalCatalogue.PressCustomers;
                    break;
                default:
                    m.Amount = 0;   // (a stake and a bid: the market sizes them by its own rules)
                    break;
            }
        }

        /// <summary>What a rival's suit claims: a share of the player's worth by the power, between the least and the most at today's prices.</summary>
        public static long SuitClaim(long worth, int power, int level)
        {
            int share = Math.Min(RivalCatalogue.SuitShareMost, RivalCatalogue.SuitShare * power / 1000);
            return RivalCatalogue.Clamp(Math.Max(0L, worth) / 10000L * share, At(RivalCatalogue.SuitLeast, level), At(RivalCatalogue.SuitMost, level));
        }

        // Moves kept: the newest, with every one still announced or running; the oldest that are over go first.
        private static void Trim(RivalFile f)
        {
            for (int i = 0; f.Moves.Count > RivalCatalogue.MovesKept && i < f.Moves.Count;)
            {
                if (!f.Moves[i].Live)
                {
                    f.Moves.RemoveAt(i);
                    continue;
                }
                i++;
            }
        }

        // ---------------------------------------------------------------- moves coming and ending

        /// <summary>The moves that come today (announced, their day here) and those that end (running, their end here), in the order they were made.</summary>
        public static void Due(RivalFile f, int day, List<RivalMove> land, List<RivalMove> end)
        {
            land.Clear();
            end.Clear();
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.State == RivalMoveState.Announced && m.Land <= day)
                {
                    land.Add(m);
                }
                else if (m.State == RivalMoveState.Running && m.End <= day)
                {
                    end.Add(m);
                }
            }
        }

        /// <summary>A move has come (<paramref name="done"/>: its feature took it) or came to nothing.</summary>
        public static void Landed(RivalFile f, RivalMove m, int day, bool done)
        {
            if (m == null || m.State != RivalMoveState.Announced)
            {
                return;
            }
            Tycoon t = f.Tycoons[RivalCatalogue.Clamp(m.Tycoon, 0, f.Tycoons.Length - 1)];
            if (!done)
            {
                m.State = RivalMoveState.Failed;
                return;
            }
            m.State = RivalCatalogue.MoveDays[(int)m.Kind] > 0 ? RivalMoveState.Running : RivalMoveState.Over;
            m.End = day + RivalCatalogue.MoveDays[(int)m.Kind];
            if (m.Kind == RivalMoveKind.Stake)
            {
                t.StakeDay = day;
            }
            else if (m.Kind == RivalMoveKind.Bid)
            {
                t.BidDay = day;
            }
        }

        public static void Ended(RivalMove m)
        {
            if (m != null && m.State == RivalMoveState.Running)
            {
                m.State = RivalMoveState.Over;
            }
        }

        // ---------------------------------------------------------------- what the running moves do, asked by the other features

        /// <summary>
        /// The change in customers at one of the player's businesses from the rivals' businesses near it, per cent (0 or
        /// less): every running outlet in its district of its kind, together never past <see cref="RivalCatalogue.OutletDipMost"/>.
        /// A walk over the moves kept (two dozen at most), asked when business pressure writes the business's footfall.
        /// </summary>
        public static int OutletDip(RivalFile f, string district, string type)
        {
            if (f == null || string.IsNullOrEmpty(district) || string.IsNullOrEmpty(type))
            {
                return 0;
            }
            long dip = 0;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Outlet && m.State == RivalMoveState.Running && m.District == district && m.Type == type)
                {
                    dip += m.Amount;
                }
            }
            return -(int)Math.Min(RivalCatalogue.OutletDipMost, dip);
        }

        /// <summary>The premium on buildings for sale in a district from a rival buying up there, per mille (the largest running).</summary>
        public static int Premium(RivalFile f, string district)
        {
            if (f == null || string.IsNullOrEmpty(district))
            {
                return 0;
            }
            long most = 0;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Property && m.State == RivalMoveState.Running && m.District == district)
                {
                    most = Math.Max(most, m.Amount);
                }
            }
            return (int)Math.Min(RivalCatalogue.PropertyPremiumMost, most);
        }

        /// <summary>How much more often inspections, licences and audits come, per mille (the largest lobbying running).</summary>
        public static int InspectionWeight(RivalFile f)
        {
            long most = 0;
            for (int i = 0; f != null && i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Lobby && m.State == RivalMoveState.Running)
                {
                    most = Math.Max(most, m.Amount);
                }
            }
            return (int)Math.Min(RivalCatalogue.LobbyWeightMost, most);
        }

        /// <summary>A day's rent rise at a business of the player's, in money: every running buying up there of a rented business, its rise on the rent.</summary>
        public static long RentRise(RivalMove m, int rent)
        {
            if (m == null || m.Kind != RivalMoveKind.Property || m.State != RivalMoveState.Running || !m.Rented || rent <= 0)
            {
                return 0;
            }
            return (long)rent * Math.Min(RivalCatalogue.RentRiseMost, m.Second) / 1000L;
        }

        // ---------------------------------------------------------------- the player's counters

        /// <summary>What a counter costs the player now: a share of their worth between the least and the most at today's prices (a takeover is priced by the market).</summary>
        public static long CounterCost(RivalCounter kind, long worth, int level)
        {
            int k = (int)kind;
            if (k < 0 || k >= (int)RivalCounter.Takeover)
            {
                return 0;
            }
            return RivalCatalogue.Clamp(Math.Max(0L, worth) / 10000L * RivalCatalogue.CounterShare[k], At(RivalCatalogue.CounterLeast[k], level), At(RivalCatalogue.CounterMost[k], level));
        }

        /// <summary>Why a counter cannot be used against a tycoon today (None: it can), and the move it would stop (outbid: a live buying up or business; lobby: a running lobbying; press: an announced move), or null.</summary>
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
                    return target != null ? RivalWhy.None : RivalWhy.Nothing;
                case RivalCounter.Lobby:
                    target = FirstOf(f, tycoon, RivalMoveKind.Lobby, RivalMoveKind.Lobby, true);
                    return RivalWhy.None;
                case RivalCounter.Press:
                {
                    RivalMove pending = f.Pending();
                    target = pending != null && pending.Tycoon == tycoon ? pending : null;
                    return RivalWhy.None;
                }
                default:
                    return RivalWhy.None;
            }
        }

        // The first live move of a tycoon of either kind (running only when asked).
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
        /// A counter used (its cost paid by the director first): what it does to the tycoon and the move it stops, and a
        /// note of it. Returns the note. The checks of <see cref="CanCounter"/> are made again here: a counter that cannot
        /// be used does nothing and returns null.
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
            t.CounterDay[(int)kind] = day;
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

        // ---------------------------------------------------------------- a takeover of a holding

        /// <summary>
        /// What taking a holding over costs: the shares the player does not hold at its price and the premium; and what its
        /// parts bring back after <see cref="RivalCatalogue.BreakUpDays"/>: those shares at its price, per mille. The player's
        /// own shares are paid out at its price by the market as it leaves. Prices in cents, money in dollars.
        /// </summary>
        public static void TakeoverTerms(long shares, long held, int midCents, out long cost, out long breakUp)
        {
            long others = Math.Max(0L, shares - Math.Max(0L, held));
            long value = others / 100L * Math.Max(0, midCents) + others % 100L * Math.Max(0, midCents) / 100L;   // (cents a share times shares, in dollars, without passing the long's range)
            cost = value + value / 10000L * RivalCatalogue.TakeoverPremiumBp;
            breakUp = value / 1000L * RivalCatalogue.BreakUpPermille;
        }

        /// <summary>Why the player cannot take a holding over: it is not listed here, or they hold less than the most one holder may (in shares).</summary>
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

        /// <summary>A tycoon taken over: out for good, every move of theirs stopped, the break-up money to come. Returns the note.</summary>
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
            f.BreakUp += Math.Max(0L, breakUp);
            f.BreakUpDay = day + RivalCatalogue.BreakUpDays;
            f.BreakUpTycoon = tycoon;
            return Note(f, tycoon, RivalCounter.Takeover, day, cost, 0);
        }

        /// <summary>The break-up money due today, taken off the file (0 when none is).</summary>
        public static long BreakUpDue(RivalFile f, int day)
        {
            if (f.BreakUp <= 0 || day < f.BreakUpDay)
            {
                return 0;
            }
            long due = f.BreakUp;
            f.BreakUp = 0;
            f.BreakUpDay = 0;
            return due;
        }

        // ---------------------------------------------------------------- the rich list

        /// <summary>
        /// The city's rich list: the tycoons who have come forward and still stand, the player, and the old fortunes (at
        /// today's prices), richest first; equals by who they are, so the order is the same everywhere. Into the list given.
        /// </summary>
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
