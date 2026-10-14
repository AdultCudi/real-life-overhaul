using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a holding is, on the Portfolio tab. Never saved: read from its company or its bond each time.</summary>
    public enum HoldingKind { Stock = 0, Trust = 1, Treasury = 2, City = 3, Deposit = 4, Count }

    /// <summary>A payment to come to the player: its day, the sum in cents before tax (under nothing: owed on shares sold short), and what pays it.</summary>
    public struct ComingPayment
    {
        public int Day;              // 0: none
        public long Cents;
        public HoldingKind Kind;
        public int Stock;            // the company's id; 0 for a bond
        public int Bond;             // the bond's own number (BondHolding.Id); 0 for shares
        public int Firm;             // shares a trading bot holds (its firm); 0 the player's own
        public bool Ends;            // a bond's last payment, with what it repays
        public bool Rolls;           // a bond's last payment, after which what it repays rolls over into a new one (none of it in the sum)
    }

    /// <summary>
    /// One holding as the Portfolio tab shows it (<see cref="InvestRules.Holdings"/>): the player's shares in a company, a
    /// trading bot's, the founder's stake in their own listed company, or a bond or deposit. Money in whole units, but
    /// the income over a year in cents.
    /// </summary>
    public struct PortfolioRow
    {
        public HoldingKind Kind;
        public int Stock;            // the company's id; 0 for a bond
        public int Bond;             // the bond's own number; 0 for shares
        public int Firm;             // a trading bot's holding (its firm); 0 none
        public bool Founder;         // the founder's own stake: shown first, in no total
        public bool Test;
        public long Shares;          // shares held (under nothing: sold short); 0 for a bond
        public int Price;            // the last price in cents; a bond's price for each 100 in hundredths (9875 is 98.75)
        public long Worth;           // at the bid (a short at the offer, under nothing; a bond what it brings sold now)
        public long Cost;            // what it cost, commission included (a short: what selling it brought, under nothing)
        public long Paid;            // income paid so far, before tax (a short: what it paid, under nothing)
        public long YearCents;       // income over the next year at today's rates, before tax (under nothing: owed)
        public int Reinvest;         // per cent of each payout put back: shares (Position.Reinvest), a bond (its own share)
        public bool Rollover;        // a bond that rolls over at its end
        public ComingPayment Next;         // its next payment (Day 0: none)

        public long Gain => Worth - Cost;
    }

    /// <summary>The portfolio added up by kind (<see cref="InvestRules.Sums"/>), the founder's stake apart: filled again each time it is asked for.</summary>
    public sealed class PortfolioSums
    {
        public readonly long[] Worth = new long[(int)HoldingKind.Count];      // holdings held (not sold short), money
        public readonly long[] Cost = new long[(int)HoldingKind.Count];
        public readonly long[] Paid = new long[(int)HoldingKind.Count];
        public readonly long[] YearCents = new long[(int)HoldingKind.Count];  // income over the next year, cents
        public readonly int[] Count = new int[(int)HoldingKind.Count];
        public long Shorts, ShortsCost, ShortsPaid;   // shares sold short: what buying them back costs at the offer, what selling them brought, the dividends they paid
        public long OwedCents;                        // what they will pay in dividends over the next year, cents
        public long Founder;                          // the founder's stake at the bid: in none of the sums
        public long Total, TotalCost, TotalPaid, TotalYearCents;

        /// <summary>What everything is worth less what the shorts would cost to buy back; what it all cost; the gain.</summary>
        public long Net => Total - Shorts;

        public long NetCost => TotalCost - ShortsCost;

        public long Gain => Net - NetCost;

        public void Clear()
        {
            for (int k = 0; k < (int)HoldingKind.Count; k++)
            {
                Worth[k] = Cost[k] = Paid[k] = YearCents[k] = 0;
                Count[k] = 0;
            }
            Shorts = ShortsCost = ShortsPaid = OwedCents = Founder = 0;
            Total = TotalCost = TotalPaid = TotalYearCents = 0;
        }

        /// <summary>A kind's share of what is held, in basis points.</summary>
        public int ShareBp(HoldingKind kind)
        {
            return Total > 0 ? (int)(Math.Max(0L, Worth[(int)kind]) * 10000L / Total) : 0;
        }

        /// <summary>The income over the next year on what is held, in basis points of its worth (shorts' dividends taken off), before tax.</summary>
        public int YieldBp => Total > 0 ? (int)Math.Max(-100000L, Math.Min(100000L, (TotalYearCents - OwedCents) * 100L / Total)) : 0;   // (cents over money is per cent; a hundred times it, bp)
    }

    /// <summary>
    /// A large investor's portfolio by rule (Markets: the Portfolio tab, worked orders, dividends put back). Game-free and
    /// without a chance of its own: everything is read from the market's state, so it is the same on every machine.
    ///
    ///     what each holding is   a company's shares are a stock or a property trust (TrustRules.Is); a bond is the
    ///                            Treasury's, the city's or a deposit at a bank (BondCatalogue); a trading bot's shares
    ///                            and the founder's stake are marked as such, the stake in none of the sums
    ///     what the portfolio     each holding's income over the next year at today's rates, before tax: a company's
    ///     pays                   quarterly dividend four times a share (a trust's distributions come to the same), a
    ///                            bond's payments due in the year; its yield on what is held; the next payments, earliest first
    ///     a worked order         larger than one order may be: a slice at each step of the market of at most the share of
    ///                            the step's trading one order may take of a day's (<see cref="Slice"/>), so about one
    ///                            order's most a day; its slices together move the price as one order of their size would
    ///                            (MarketTrading.SliceShare); on a cash account its day's money held at once and settled
    ///                            at the close (<see cref="DayHold"/>, <see cref="WithBroker"/>)
    ///     a dividend put back    the share chosen of what it pays after tax, bought into the same company at the next open
    ///                            as an order at the market for as many shares as it pays for (<see cref="SharesFor"/>),
    ///                            one order's most at the most and never past the holding limit
    ///
    /// The cost: a look at each holding when the Portfolio tab is written (on a click); nothing per frame.
    /// </summary>
    public static class InvestRules
    {
        /// <summary>The shares of each payout a holding of shares may put back, per cent (the same choices a bond's interest has).</summary>
        public static int[] ReinvestChoices => BondCatalogue.Reinvest;

        /// <summary>A share to put back held to one of the choices: the nearest at or under it.</summary>
        public static int ReinvestOf(int pct)
        {
            return BondRules.ReinvestOf(pct);
        }

        // ---------------------------------------------------------------- what each holding is

        public static HoldingKind KindOf(Stock s)
        {
            return TrustRules.Is(s) ? HoldingKind.Trust : HoldingKind.Stock;
        }

        public static HoldingKind KindOf(BondDef d)
        {
            return d == null || d.Kind == BondKind.Treasury ? HoldingKind.Treasury : d.Kind == BondKind.City ? HoldingKind.City : HoldingKind.Deposit;
        }

        public static bool IsBond(HoldingKind kind)
        {
            return kind >= HoldingKind.Treasury && kind < HoldingKind.Count;
        }

        /// <summary>What a share of a company pays over the next year at its rate now, in cents: its quarterly dividend four times (a trust's distributions come to the same: TrustRules.YearCents).</summary>
        public static long YearCentsOf(Stock s)
        {
            return s != null && s.Listed ? Math.Max(0, s.DpsCents) * 4L : 0L;
        }

        /// <summary>A bond's interest over the next year, before tax, in cents: its payments still to come that fall in it, at its face now.</summary>
        public static long YearInterestCents(BondHolding h, BondDef d, int day, int year)
        {
            if (h == null || d == null)
            {
                return 0L;
            }
            long each = BondRules.InterestCents(h, d), sum = 0L;
            int until = day + Math.Max(8, year);
            for (int k = h.Paid + 1; k <= h.Payments; k++)
            {
                if (BondRules.PayDay(h, k) > until)
                {
                    break;   // (its payment days only grow)
                }
                sum += each;
            }
            return sum;
        }

        /// <summary>The player's own listed company (its founder is the player of this machine), or null.</summary>
        public static Stock Founder(MarketState s)
        {
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (k.Listed && MarketBots.Mine(s, k))
                {
                    return k;
                }
            }
            return null;
        }

        /// <summary>
        /// Every holding of the player's, in the order the Portfolio tab lists them: the founder's stake, then their own
        /// shares, then each bot's, then the bonds and deposits. The list is filled again (it is kept by the caller). With
        /// 'bondsOn' (bonds and deposits switched on), a bond set to roll over repays nothing into the cash at its end: its
        /// last payment is its interest alone, marked as rolling over.
        /// </summary>
        public static void Holdings(MarketState s, int phase, List<PortfolioRow> into, bool bondsOn = true)
        {
            into.Clear();
            Stock founder = Founder(s);
            if (founder != null)
            {
                Quote q = MarketTrading.QuoteOf(s, founder, phase);
                into.Add(new PortfolioRow
                {
                    Kind = KindOf(founder), Stock = founder.Id, Founder = true, Shares = founder.Own.Founder, Price = founder.Last,
                    Worth = MarketTrading.Value(founder.Own.Founder, q.Bid, false)   // (its dividends are the founder's own reserve's: nothing paid to them)
                });
            }
            for (int i = 0; i < s.Positions.Count; i++)
            {
                Position p = s.Positions[i];
                Stock k = s.Stock(p.StockId);
                if (k == null || p.Shares == 0)
                {
                    continue;
                }
                Quote q = MarketTrading.QuoteOf(s, k, phase);
                bool held = p.Shares > 0;
                into.Add(new PortfolioRow
                {
                    Kind = KindOf(k), Stock = k.Id, Test = p.Test, Shares = p.Shares, Price = k.Last,
                    Worth = held ? MarketTrading.Value(p.Shares, q.Bid, false) : -MarketTrading.Value(-p.Shares, q.Ask, true), Cost = held ? p.Cost : -p.Cost, Paid = p.Dividends,
                    YearCents = p.Shares * YearCentsOf(k), Reinvest = held ? ReinvestOf(p.Reinvest) : 0, Next = NextOf(s, k, p.Shares, 0)
                });
            }
            for (int i = 0; i < s.Bots.Count; i++)
            {
                Bot b = s.Bots[i];
                for (int j = 0; j < b.Holdings.Count; j++)
                {
                    BotHolding h = b.Holdings[j];
                    Stock k = s.Stock(h.StockId);
                    if (k == null || h.Shares <= 0)
                    {
                        continue;
                    }
                    into.Add(new PortfolioRow
                    {
                        Kind = KindOf(k), Stock = k.Id, Firm = b.Firm, Shares = h.Shares, Price = k.Last,
                        Worth = k.Listed ? MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(s, k, phase).Bid, false) : 0L, Cost = h.Cost, YearCents = h.Shares * YearCentsOf(k),
                        Next = NextOf(s, k, h.Shares, b.Firm)
                    });
                }
            }
            int year = Math.Max(8, s.YearDays);
            for (int i = 0; i < s.Bonds.Holdings.Count; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                BondDef d = BondCatalogue.Find(h.Def);
                if (d == null)
                {
                    continue;
                }
                var row = new PortfolioRow
                {
                    Kind = KindOf(d), Bond = h.Id, Test = h.Test, Price = (int)Math.Round(BondRules.Price(h, d, s.Env, s.Day, year) * 100.0),
                    Worth = BondRules.SaleCents(h, d, s.Env, s.Day, year) / 100L, Cost = h.Cost / 100L, Paid = (h.Received + h.Reinvested + h.Taxed) / 100L,
                    YearCents = YearInterestCents(h, d, s.Day, year), Reinvest = h.Reinvest, Rollover = h.Rollover
                };
                int day = BondRules.NextPayDay(h);
                if (day > 0)
                {
                    bool last = h.Paid + 1 >= h.Payments, rolls = last && Rolls(h, bondsOn);
                    row.Next = new ComingPayment
                    {
                        Day = day, Cents = BondRules.InterestCents(h, d) + (last && !rolls ? h.Face : 0L), Kind = row.Kind, Bond = h.Id, Ends = last && !rolls, Rolls = rolls
                    };
                }
                into.Add(row);
            }
        }

        /// <summary>
        /// Whether a bond rolls over at its end (set to, with bonds and deposits on: MarketEngine.Bonds' End): what it repays
        /// then goes into a new one, not into the cash, so nothing that shows what is coming counts it.
        /// </summary>
        public static bool Rolls(BondHolding h, bool bondsOn)
        {
            return h != null && h.Rollover && bondsOn;
        }

        // A holding of shares' next payment: what is owed after its record day (every due of it, the earliest day), else the
        // dividend declared and not yet out of the price (a short's is owed by the player). None: Day 0.
        private static ComingPayment NextOf(MarketState s, Stock k, long shares, int firm)
        {
            long cents = 0;
            int day = 0;
            for (int i = 0; i < s.Dues.Count; i++)
            {
                DividendDue due = s.Dues[i];
                if (due.StockId == k.Id && due.Bot == firm)
                {
                    cents += due.Cents;
                    day = day == 0 || due.PayDay < day ? due.PayDay : day;
                }
            }
            if (day == 0 && k.Listed && k.DeclaredCents > 0 && !k.ExDone && k.PayDay > 0)
            {
                cents = shares * k.DeclaredCents;
                day = k.PayDay;
            }
            return day == 0 || cents == 0 ? default(ComingPayment) : new ComingPayment { Day = day, Cents = cents, Kind = KindOf(k), Stock = k.Id, Firm = firm };
        }

        /// <summary>The holdings added up by kind: what is held, what shares sold short owe, the founder's stake apart.</summary>
        public static void Sums(List<PortfolioRow> rows, PortfolioSums into)
        {
            into.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                PortfolioRow h = rows[i];
                if (h.Founder)
                {
                    into.Founder += h.Worth;
                    continue;
                }
                if (h.Shares < 0)
                {
                    into.Shorts -= h.Worth;
                    into.ShortsCost -= h.Cost;
                    into.ShortsPaid += h.Paid;
                    into.OwedCents -= h.YearCents;
                    continue;
                }
                int k = (int)h.Kind;
                into.Worth[k] += h.Worth;
                into.Cost[k] += h.Cost;
                into.Paid[k] += h.Paid;
                into.YearCents[k] += h.YearCents;
                into.Count[k]++;
                into.Total += h.Worth;
                into.TotalCost += h.Cost;
                into.TotalPaid += h.Paid;
                into.TotalYearCents += h.YearCents;
            }
        }

        /// <summary>
        /// The next payments to come, the earliest first (then by kind, company, bond and bot, so the order is the same
        /// every time): at most 'most', into a list kept by the caller. How many there are in all.
        /// </summary>
        public static int NextPayments(List<PortfolioRow> rows, List<ComingPayment> into, int most)
        {
            into.Clear();
            int all = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                ComingPayment p = rows[i].Next;
                if (p.Day <= 0 || rows[i].Founder)
                {
                    continue;
                }
                all++;
                int at = into.Count;
                while (at > 0 && Before(p, into[at - 1]))
                {
                    at--;
                }
                if (at >= most)
                {
                    continue;
                }
                into.Insert(at, p);
                if (into.Count > most)
                {
                    into.RemoveAt(into.Count - 1);
                }
            }
            return all;
        }

        private static bool Before(in ComingPayment a, in ComingPayment b)
        {
            if (a.Day != b.Day)
            {
                return a.Day < b.Day;
            }
            if (a.Kind != b.Kind)
            {
                return a.Kind < b.Kind;
            }
            if (a.Stock != b.Stock)
            {
                return a.Stock < b.Stock;
            }
            return a.Bond != b.Bond ? a.Bond < b.Bond : a.Firm < b.Firm;
        }

        /// <summary>
        /// What the player holds on the market is worth, in money: their own shares at the bid less what shares sold short
        /// would cost at the offer, their bots' shares, their bonds at what they would bring sold now, their stake in their
        /// own listed company, the margin account's own money (under nothing: borrowed), and the money with the broker for
        /// worked orders (<see cref="WithBroker"/>: out of the cash for the day). A bot's money is the player's cash already,
        /// so it is not counted here.
        /// </summary>
        public static long MarketWorth(MarketState s, int phase)
        {
            long worth = (s.Account.Margin ? s.Account.Cash : 0L) + WithBroker(s, out _, out _);
            for (int i = 0; i < s.Positions.Count; i++)
            {
                Position p = s.Positions[i];
                Stock k = s.Stock(p.StockId);
                if (k == null || p.Shares == 0)
                {
                    continue;
                }
                Quote q = MarketTrading.QuoteOf(s, k, phase);
                worth += p.Shares > 0 ? MarketTrading.Value(p.Shares, q.Bid, false) : -MarketTrading.Value(-p.Shares, q.Ask, true);
            }
            for (int i = 0; i < s.Bots.Count; i++)
            {
                worth += MarketBots.Worth(s, s.Bots[i], phase) - s.Bots[i].Cash;
            }
            int year = Math.Max(8, s.YearDays);
            for (int i = 0; i < s.Bonds.Holdings.Count; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                BondDef d = BondCatalogue.Find(h.Def);
                worth += (d != null ? BondRules.SaleCents(h, d, s.Env, s.Day, year) : Math.Max(0L, h.Face)) / 100L;
            }
            Stock founder = Founder(s);
            if (founder != null)
            {
                worth += MarketTrading.Value(founder.Own.Founder, MarketTrading.QuoteOf(s, founder, phase).Bid, false);
            }
            return worth;
        }

        /// <summary>
        /// The player's money with the broker for worked orders on a cash account, in money: the day's money held for
        /// purchases (not spent yet: back at the close) and the day's takings of sales after the commission (paid at the
        /// close). Out of the cash and in no holding meanwhile, so whatever counts what the player has counts it here.
        /// </summary>
        public static long WithBroker(MarketState s, out long held, out long due)
        {
            held = due = 0L;
            for (int i = 0; i < s.Orders.Count; i++)
            {
                Order o = s.Orders[i];
                held += Math.Max(0L, o.Held);
                due += Math.Max(0L, o.Due);
            }
            return held + due;
        }

        // ---------------------------------------------------------------- worked orders

        /// <summary>
        /// A worked purchase's money for a market day, held at its first slice: what so many shares cost at a price with the
        /// commission, and a tenth more for the price moving through the day. Its slices draw on it; what is left comes back
        /// at the close.
        /// </summary>
        public static long DayHold(long qty, int cents, int least)
        {
            if (qty <= 0 || cents <= 0)
            {
                return 0L;
            }
            long value = MarketTrading.Value(qty, cents, true);
            long cost = value + MarketTrading.Commission(value, least);
            return cost + (cost + 9L) / 10L;
        }

        /// <summary>
        /// The next piece of a sum the game's money takes in one transaction (it takes no more than int.MaxValue at a time),
        /// taken off what is left: a larger sum is paid or taken in pieces, none of it lost. Nothing when nothing is left.
        /// </summary>
        public static long Piece(ref long left)
        {
            long piece = Math.Max(0L, Math.Min(left, int.MaxValue));
            left -= piece;
            return piece;
        }

        /// <summary>
        /// A worked order's slice at a step of the market: what is left of it, never more than the share of the step's own
        /// trading (its shares, the player's not counted) one order may take of a day's (MarketCatalogue.OrderShareOfVolumePct).
        /// Over a day the slices come to about one order's most (MarketTrading.MostPerOrder). Nothing in a step that traded nothing.
        /// </summary>
        public static long Slice(long left, long stepVolume)
        {
            if (left <= 0 || stepVolume <= 0)
            {
                return 0L;
            }
            return Math.Min(left, stepVolume * MarketCatalogue.OrderShareOfVolumePct / 100L);
        }

        /// <summary>About how many market days a worked order of so many shares takes, at a company's usual volume (one at least).</summary>
        public static int MarketDays(long qty, double adv)
        {
            if (qty <= 0)
            {
                return 0;
            }
            double day = Math.Max(1.0, adv * MarketCatalogue.OrderShareOfVolumePct / 100.0);
            return (int)Math.Max(1.0, Math.Min(100000.0, Math.Ceiling(qty / day)));
        }

        // ---------------------------------------------------------------- dividends put back

        /// <summary>The money a dividend paid puts back: the share chosen (held to the choices) of what is left of it after the tax withheld, as on a bond's interest.</summary>
        public static long PutBack(long money, int taxStep, int pct)
        {
            long net = Math.Max(0L, money - ListingVotes.DividendTax(money, taxStep));
            return net * ReinvestOf(Math.Max(0, Math.Min(100, pct))) / 100L;
        }

        /// <summary>
        /// The most shares that so much money buys at a price in cents with the commission (its least at least): what an
        /// order at the market for that many shares takes from the cash is never more than the money. Nothing when it does
        /// not buy one share.
        /// </summary>
        public static long SharesFor(long money, int cents, int least)
        {
            if (money <= 0 || cents <= 0)
            {
                return 0L;
            }
            long lo = 0, hi = money * 100L / cents;
            while (lo < hi)
            {
                long mid = lo + (hi - lo + 1) / 2;
                long value = MarketTrading.Value(mid, cents, true);
                if (value + MarketTrading.Commission(value, least) <= money)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return lo;
        }
    }
}
