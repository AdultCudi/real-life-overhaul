using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>The rivals' rules, part: a new move, its aim and size, its coming and ending, and what running moves do.</summary>
    public static partial class RivalRules
    {
        /// <summary>A move announced today if one is due, or null. With <paramref name="any"/> (a test) tier and gap are not asked.</summary>
        public static RivalMove Announce(RivalFile f, RivalWorld w, int day, int strength, bool any)
        {
            int tier = Tier(w.Worth, w.Level, w.FirstFrom);
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

        /// <summary>A move of one kind by one tycoon, announced today; null when there is nothing for it.</summary>
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

        /// <summary>The kind of move a tycoon makes today by their likings; <see cref="RivalMoveKind.Count"/> for none.</summary>
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

        /// <summary>Whether a tycoon could make a kind of move today.</summary>
        public static bool Available(RivalFile f, Tycoon t, RivalMoveKind kind, RivalWorld w, int day, int tier)
        {
            bool shops = w.Shops.Count > 0;
            switch (kind)
            {
                case RivalMoveKind.Property: return shops && day >= t.BarredUntil && !PropertyAt(f, w.Shops[Best(w)].Place);
                case RivalMoveKind.Outlet: return shops && LiveCount(f, RivalMoveKind.Outlet) < RivalCatalogue.OutletsMost && OutletShop(f, w) >= 0;
                case RivalMoveKind.Poach: return shops && w.Staff;
                case RivalMoveKind.Stake: return tier >= RivalCatalogue.StakeTier && CompanyOld(w) && t.Listed && (t.StakeDay < 0 || day - t.StakeDay >= RivalCatalogue.BidRestDays);
                case RivalMoveKind.Bid:
                    return tier >= RivalCatalogue.BidTier && CompanyOld(w) && t.Listed && t.StakeDay >= 0 && day - t.StakeDay <= RivalCatalogue.BidAfterStakeDays
                           && (t.BidDay < 0 || day - t.BidDay >= RivalCatalogue.BidRestDays);
                case RivalMoveKind.Lobby: return shops && w.Issues && day >= t.BarredUntil && LiveCount(f, RivalMoveKind.Lobby) == 0;
                case RivalMoveKind.Suit: return w.Court && w.Worth > 0;
                case RivalMoveKind.Press: return w.Paper || (shops && w.Issues);
                default: return false;
            }
        }

        /// <summary>The player's company is listed and has traded long enough for a stake or a bid.</summary>
        public static bool CompanyOld(RivalWorld w)
        {
            return w.Listed && w.Market && w.CompanyDays >= Math.Max(8, w.YearDays) / 4 * RivalCatalogue.CompanyQuarters;
        }

        /// <summary>True while a buying up is live at a place: never two at one business.</summary>
        public static bool PropertyAt(RivalFile f, string place)
        {
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Property && m.Live && m.Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        public static int LiveCount(RivalFile f, RivalMoveKind kind)
        {
            int n = 0;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                n += f.Moves[i].Kind == kind && f.Moves[i].Live ? 1 : 0;
            }
            return n;
        }

        // The player's best business; for an outlet, the best with no rival's outlet of its kind in its district.
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

        /// <summary>The best business with no live rival outlet of its kind in its district; -1 with none.</summary>
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

        /// <summary>How big a move is at a tier and a strength, inside its bounds.</summary>
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

        /// <summary>A suit's claim: a share of the player's worth by the power, between the least and the most.</summary>
        public static long SuitClaim(long worth, int power, int level)
        {
            int share = Math.Min(RivalCatalogue.SuitShareMost, RivalCatalogue.SuitShare * power / 1000);
            return RivalCatalogue.Clamp(Math.Max(0L, worth) / 10000L * share, At(RivalCatalogue.SuitLeast, level), At(RivalCatalogue.SuitMost, level));
        }

        // Keeps every live move; the oldest that are over go first.
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

        /// <summary>The moves that come today and those that end, in the order they were made.</summary>
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

        /// <summary>
        /// The change in customers at one of the player's businesses from running rival outlets of its kind in its district,
        /// per cent (0 or less), capped at <see cref="RivalCatalogue.OutletDipMost"/>. A walk over the moves kept.
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

        /// <summary>The premium on buildings for sale in a district, per mille (the largest running buying up).</summary>
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

        /// <summary>A day's rent rise at a rented business, in money: all its running buyings up, capped on its rent.</summary>
        public static long RentRise(RivalFile f, string place, int rent)
        {
            if (f == null || string.IsNullOrEmpty(place) || rent <= 0)
            {
                return 0;
            }
            long rise = 0;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Property && m.State == RivalMoveState.Running && m.Rented && m.Place == place)
                {
                    rise += Math.Max(0L, m.Second);
                }
            }
            return (long)rent * Math.Min(RivalCatalogue.RentRiseMost, rise) / 1000L;
        }
    }
}
