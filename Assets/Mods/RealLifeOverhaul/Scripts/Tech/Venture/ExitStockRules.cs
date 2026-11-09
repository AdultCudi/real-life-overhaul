using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A firm of the player's that listed, and the stock it became: the cap table and the stock hold the same shares, the stock being the one that moves
    /// once the firm trades. The rules of making the stock's ask from the cap table and the listing, of bringing the two back into agreement, and of what
    /// the public is owed when the firm ends. Game-free; every figure is the cap table's or the exit's own, cents at the start's prices unless said.
    /// </summary>
    public static class ExitStockRules
    {
        public const int BlockedNone = 0, BlockedTaken = 1, BlockedLadder = 2;

        /// <summary>
        /// Why a firm may not go to the market now, 0 when it may: money of its investors' is counted as taken (spent elsewhere and not put back), or the board has a
        /// question, a demand or a removal open. The public is not sold shares of a firm that owes its investors what it took.
        /// </summary>
        public static int ListBlocked(Cap c)
        {
            if (c == null || !c.Acc.Open || c.Acc.Wound)
            {
                return BlockedNone;
            }
            if (c.Acc.Taken > 0)
            {
                return BlockedTaken;
            }
            return c.Acc.Step > StartupMoney.StepNone ? BlockedLadder : BlockedNone;
        }

        /// <summary>
        /// Whether a firm's shares are the stock market's: the market has the stock, or the tech sector's record says one was made and has not ended while
        /// the market cannot be asked (it is off, or the player is a guest and the firm is set aside). No other path sells those shares then, or they would be
        /// sold twice when the market is back.
        /// </summary>
        public static bool Held(in StockInfo now, StockFirm record)
        {
            return now.Live || (record != null && record.Live);
        }

        /// <summary>The shares the public holds, every line of them.</summary>
        public static long PublicShares(Cap c)
        {
            long n = 0;
            for (int i = 0; c != null && i < c.Holders.Count; i++)
            {
                n += c.Holders[i].Kind == (int)HolderKind.Public ? c.Holders[i].Shares : 0;
            }
            return n;
        }

        /// <summary>The shares that are neither the player's nor the public's: the investors', the co-founders' and the staff pool's.</summary>
        public static long Others(Cap c)
        {
            return c == null ? 0L : c.Total - ExitRules.PlayerShares(c) - PublicShares(c);
        }

        /// <summary>The days a founder's vesting takes to its cliff and to its end, in the game's year; the cap table's own (VentureRules.Vested).</summary>
        public static int VestCliff(int year) => VentureCatalogue.CliffYears * Math.Max(60, year);

        public static int VestSpan(int year) => VentureCatalogue.VestYears * Math.Max(60, year);

        /// <summary>A share's price in cents: a value over so many shares, never under a cent.</summary>
        public static int PriceCents(long cents, long shares)
        {
            return (int)Math.Max(1L, Math.Min(int.MaxValue, cents / Math.Max(1L, shares)));
        }

        /// <summary>
        /// The ask for a listed firm's stock from its cap table and its listing. <paramref name="value"/> is what the shares are worth together, cents at
        /// today's prices, and <paramref name="over"/> the shares it is a value of: a priced offer's value before the money over the shares before it; an
        /// older listing's, the firm's worth now over all its shares.
        /// </summary>
        public static StockAsk Ask(Cap c, FirmExit e, string place, string name, long value, long over, int year)
        {
            return new StockAsk
            {
                Place = place, Name = name, Shares = c.Total, Founder = ExitRules.PlayerShares(c), Float = PublicShares(c), PriceCents = PriceCents(value, over),
                LockDay = e.LockTo, ListedDay = e.ListDay, VestBase = ExitRules.PlayerShares(c), VestDay = c.Acc.FounderDay, VestCliff = VestCliff(year), VestSpan = VestSpan(year),
                Debt = Math.Max(0L, c.Bridge)
            };
        }

        /// <summary>
        /// How many shares the stock must grow by (under nothing: shrink by) for the cap table's lines that are neither the player's nor the public's to
        /// agree with it: staff options granted since it last looked, an investor's line, a co-founder's.
        /// </summary>
        public static long Grown(Cap c, in StockInfo i)
        {
            return Others(c) - (i.Shares - i.Founder - i.Float);
        }

        /// <summary>
        /// The cap table brought into agreement with the stock: the player's line is what the founder holds there, the public's what the public does. The
        /// stock is the one that moves once the firm trades, so a sale or a purchase at the market is the cap table's too. Returns true if a line changed.
        /// </summary>
        public static bool Align(Cap c, in StockInfo i)
        {
            bool changed = false;
            Holder me = null, pub = null;
            for (int k = 0; k < c.Holders.Count; k++)
            {
                Holder h = c.Holders[k];
                if (h.Kind == (int)HolderKind.Player)
                {
                    if (me == null)
                    {
                        me = h;
                    }
                    else
                    {
                        me.Invested += h.Invested;   // a second line of the player's is one with the first: the stock holds one stake
                        h.Shares = 0;
                        changed = true;
                    }
                }
                else if (h.Kind == (int)HolderKind.Public)
                {
                    if (pub == null)
                    {
                        pub = h;
                    }
                    else
                    {
                        pub.Shares += h.Shares;   // lines of the public from sales before there was a stock are one line now
                        pub.Invested += h.Invested;
                        h.Shares = 0;
                        changed = true;
                    }
                }
            }
            if (me != null && me.Shares != i.Founder)
            {
                me.Shares = Math.Max(0L, i.Founder);
                changed = true;
            }
            if (pub == null && i.Float > 0)
            {
                pub = new Holder { Kind = (int)HolderKind.Public, Round = Math.Max(0, c.Stage) };
                c.Holders.Add(pub);
                changed = true;
            }
            if (pub != null && pub.Shares != i.Float)
            {
                pub.Shares = Math.Max(0L, i.Float);
                changed = true;
            }
            for (int k = c.Holders.Count - 1; k >= 0; k--)
            {
                Holder h = c.Holders[k];
                if ((h.Kind == (int)HolderKind.Public || (h.Kind == (int)HolderKind.Player && h != me)) && h.Shares <= 0)
                {
                    c.Holders.RemoveAt(k);
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// What each of the public's shares is paid when the pot is shared by the cap table's waterfall (VentureRules.Waterfall), cents: the same sum the
        /// public's line is owed, over the shares it holds. What the stock pays whoever holds the shares on the market, so a holder there is made whole by
        /// what the cap table owes.
        /// </summary>
        public static long PublicEach(Cap c, long pot)
        {
            long shares = PublicShares(c);
            if (shares <= 0 || pot <= 0)
            {
                return 0L;
            }
            long[] lines = VentureRules.Waterfall(c.Holders, pot);
            long owed = 0;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                owed += c.Holders[i].Kind == (int)HolderKind.Public ? lines[i] : 0L;
            }
            return owed / shares;
        }

        /// <summary>The firm's worth for an offer once its shares trade: all its shares at the market's last price, cents at the start's prices.</summary>
        public static long MarketValue(long shares, int lastCents, int level)
        {
            return (long)((double)Math.Max(0L, shares) * Math.Max(0, lastCents) * 1000.0 / Math.Max(1, level));
        }

        /// <summary>The days of the lock-up left for the player's shares at a day; 0 when it has run out.</summary>
        public static int LockLeft(in StockInfo i, int day)
        {
            return i.Live && i.LockDay > day ? i.LockDay - day : 0;
        }

        /// <summary>How an ended stock's shares are paid, as the market's reasons number them: 0 failed (nothing was left), 1 taken over, 2 delisted.</summary>
        public static int Reason(int ended, bool dry)
        {
            return ended == StockEnd.Sold ? 1 : dry ? 0 : 2;
        }
    }
}
