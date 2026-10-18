using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The venture rules of the player's own firms: their cap tables, term sheets, the board and its target, bridges, options and share sales.</summary>
    public static partial class VentureRules
    {
        /// <summary>A cap table for a firm of the player's that is raising for the first time: the player holds it all.</summary>
        public static Cap NewCap(string place)
        {
            var c = new Cap { Place = place, Stage = -1 };
            c.Holders.Add(new Holder { Kind = (int)HolderKind.Player, Shares = FounderShares });
            return c;
        }

        /// <summary>The player's share of a firm, thousandths.</summary>
        public static int PlayerShare(Cap c)
        {
            long total = Math.Max(1, c.Total), mine = 0;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                mine += c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i].Shares : 0;
            }
            return (int)(mine * 1000 / total);
        }

        /// <summary>A term sheet for the player's firm: its worth moved by the meeting and the mood; a lead takes a board seat.</summary>
        public static TermSheet Sheet(int vc, long firmValue, long amount, int score, int mood, int day)
        {
            long pre = firmValue * (600 + TalentCatalogue.Clamp(score, 0, 1000) * 9 / 10) / 1000 * mood / 1000;
            pre = Math.Max(VentureCatalogue.FloorValue[0], pre);
            bool board = !VentureCatalogue.Firms[TalentCatalogue.Clamp(vc, 0, VentureCatalogue.FirmCount - 1)].Angels;
            return new TermSheet { Vc = vc, Amount = amount, Pre = pre, Board = board, ProRata = true, Lapse = day + VentureCatalogue.TermSheetDays, Score = score };
        }

        /// <summary>A term sheet taken: shares to the investor (and a bridge converted), its seat, a new target. Returns the cents paid in.</summary>
        public static long Accept(Cap c, TermSheet t, long mrr, int day)
        {
            long total = Math.Max(1, c.Total);
            var h = new Holder { Kind = (int)HolderKind.Vc, Who = t.Vc, Invested = t.Amount, Board = t.Board, ProRata = t.ProRata, Round = c.Stage + 1 };
            h.Shares = (long)((double)t.Amount * total / Math.Max(1, t.Pre));
            h.Preference = t.Amount * VentureCatalogue.LiquidationPreference / 1000;
            c.Holders.Add(h);
            if (c.Bridge > 0)
            {
                long price = Math.Max(1, t.Pre) * (1000 - VentureCatalogue.BridgeDiscount) / 1000;
                c.Holders.Add(new Holder
                {
                    Kind = (int)HolderKind.Vc, Who = t.Vc, Invested = c.Bridge, Round = c.Stage + 1, Shares = (long)((double)c.Bridge * total / price), Preference = c.Bridge
                });
                c.Bridge = 0;
            }
            c.Stage = Math.Min((int)RoundStage.Growth, c.Stage + 1);
            c.Post = t.Pre + t.Amount;
            if (t.Board)
            {
                c.BoardSeats += VentureCatalogue.BoardSeatsPerRound;
                c.VcSeats += VentureCatalogue.BoardSeatsPerRound;
            }
            c.RaiseUntil = 0;
            c.Sheets.Clear();
            c.Misses = 0;   // a new round: the board starts afresh
            c.Pressure = 0;
            Target(c, mrr, day);
            return t.Amount;
        }

        /// <summary>The board's target: recurring revenue a quarter on, a set share higher than now.</summary>
        public static void Target(Cap c, long mrr, int day)
        {
            c.LastMrr = mrr;
            c.TargetMrr = Math.Max(100000, mrr * (1000 + VentureCatalogue.TargetGrowth) / 1000);
            c.TargetDay = day + VentureCatalogue.TargetDays;
        }

        /// <summary>The board on its target day: 0 nothing due, 1 met, 2 missed, 3 the chief executive replaced.</summary>
        public static int Review(Cap c, long mrr, int day)
        {
            if (c.VcSeats == 0 || c.TargetDay <= 0 || day < c.TargetDay)
            {
                return 0;
            }
            if (mrr >= c.TargetMrr)
            {
                c.Misses = 0;
                c.Pressure = 0;
                Target(c, mrr, day);
                return 1;
            }
            c.Misses++;
            Target(c, mrr, day);
            if (c.Misses >= VentureCatalogue.MissesToReplace && c.VcSeats * 2 > c.BoardSeats)
            {
                c.Pressure = 2;
                c.Misses = 0;
                return 3;
            }
            c.Pressure = 1;
            return 2;
        }

        /// <summary>The board meant to replace the chief executive and could hire nobody: a warning, and the next target missed replaces them.</summary>
        public static void ChiefKept(Cap c)
        {
            c.Pressure = 1;
            c.Misses = VentureCatalogue.MissesToReplace - 1;
        }

        /// <summary>A bridge loan from the last lead: months of the burn, owed with interest until the next round converts it.</summary>
        public static long BridgeFor(long burnPerDay) => Math.Max(0, burnPerDay) * 30 * VentureCatalogue.BridgeMonths;

        /// <summary>A day's interest on a bridge, cents.</summary>
        public static long BridgeInterest(long owed) => owed * VentureCatalogue.BridgeInterestBp / 10000 / 365;

        /// <summary>Share options granted: new shares for the staff pool. Returns the shares made.</summary>
        public static long Grant(Cap c, int thousandths)
        {
            long total = Math.Max(1, c.Total);
            long shares = total * thousandths / (1000 - thousandths);
            Holder pool = null;
            for (int i = 0; i < c.Holders.Count && pool == null; i++)
            {
                pool = c.Holders[i].Kind == (int)HolderKind.Pool ? c.Holders[i] : null;
            }
            if (pool == null)
            {
                pool = new Holder { Kind = (int)HolderKind.Pool };
                c.Holders.Add(pool);
            }
            pool.Shares += shares;
            c.OptionsGranted += thousandths;
            return shares;
        }

        /// <summary>Options split evenly among a firm's people: each one's share, with what does not divide (all of it with nobody) left over.</summary>
        public static long Split(long shares, int people, out long left)
        {
            if (people <= 0 || shares <= 0)
            {
                left = Math.Max(0L, shares);
                return 0L;
            }
            long each = shares / people;
            left = shares - each * people;
            return each;
        }

        /// <summary>The staff options of the player's firm that nobody holds: its pool less what its people hold, never below 0.</summary>
        public static long Unheld(Cap c, long held)
        {
            long pool = 0L;
            for (int i = 0; c != null && i < c.Holders.Count; i++)
            {
                pool += c.Holders[i].Kind == (int)HolderKind.Pool ? c.Holders[i].Shares : 0L;
            }
            return Math.Max(0L, pool - Math.Max(0L, held));
        }

        /// <summary>The share of a grant that has vested, thousandths: none before the cliff, then by the day over the vesting years.</summary>
        public static int Vested(int grantedDay, int day, int year)
        {
            int y = Math.Max(60, year);
            int age = day - grantedDay;
            if (age < VentureCatalogue.CliffYears * y)
            {
                return 0;
            }
            return Math.Min(1000, age * 1000 / (VentureCatalogue.VestYears * y));
        }

        /// <summary>The player sells some of their shares to an investor at a fifth off the last round's price. Returns the cents paid.</summary>
        public static long SellShares(Cap c, int thousandthsOfMine, int vc)
        {
            Holder me = null;
            for (int i = 0; i < c.Holders.Count && me == null; i++)
            {
                me = c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i] : null;
            }
            if (me == null || c.Post <= 0)
            {
                return 0;
            }
            long total = Math.Max(1, c.Total);
            long shares = me.Shares * TalentCatalogue.Clamp(thousandthsOfMine, 0, 1000) / 1000;
            long price = (long)((double)c.Post * shares / total * 800 / 1000);
            me.Shares -= shares;
            c.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Who = vc, Shares = shares, Invested = price, Round = Math.Max(0, c.Stage) });
            return price;
        }
    }
}
