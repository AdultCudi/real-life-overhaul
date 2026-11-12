using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why units of the fund cannot be dealt in. Each has a sentence (ExtrasText).</summary>
    public enum FundWhy
    {
        None = 0,
        Off,             // the extras are switched off, or the index has nothing in it
        Closed,          // the market is not trading
        Held,            // after a load, until the market is back where it was
        Bad,             // no units asked for
        NoMoney,
        NoUnits,         // more units than are held
        TooLarge,        // a slice of it is more of a company than one order may be
        HoldingLimit,    // the units held would stand for more of the market than one holder may have
        Count
    }

    /// <summary>What a deal in the fund's units comes to before it is made.</summary>
    public struct FundDeal
    {
        public long Units;
        public int NavCents;         // what a unit holds now, at the middle prices
        public int PriceCents;       // what a unit is dealt at: its slice of every company at the offer or the bid, moved by its size
        public long Value, Fee, Total;
        public int CostBp;           // how far the dealing price is from what a unit holds
        public long Most;            // the most units that can be dealt this way now
        public FundWhy Why;
    }

    /// <summary>
    /// The city's index fund. A unit holds a slice of every listed company in the index's own weights (free float, none over
    /// a tenth), so what it holds is the index, read from the companies' prices of this very moment: it never lags them.
    /// It is dealt at what its slices cost at each company's own offer or bid, each moved by its size as an order of that
    /// size is and by the player's recent trading in that company, and never at a price the player's own lasting push has
    /// made better for them. So a unit is never cheaper to buy, or dearer to sell, than its parts. A yearly fee comes out of
    /// its worth a market day at a time (<see cref="ExtrasState.Track"/>); a company that leaves paying its holders less
    /// than its price takes that out too. What its companies pay is passed on by the units held at their record day.
    /// Game-free; a pass over the listed companies on a press, a close or a page, never per frame.
    /// </summary>
    public static class FundRules
    {
        private static bool[] _capped;   // MarketIndex's scratch: the market runs on one thread

        /// <summary>The index now, in hundredths of a point, from the companies' prices of this moment; 0 with no index yet.</summary>
        public static double IndexNow(MarketState s, out double whole)
        {
            whole = MarketIndex.Total(s.Stocks, null, null, ref _capped);
            return s.IndexDivisor > 0.0 ? whole / s.IndexDivisor * 100.0 : 0.0;
        }

        /// <summary>What a unit holds now, in cents and not rounded.</summary>
        public static double Nav(MarketState s)
        {
            return IndexNow(s, out _) / ExtrasCatalogue.FundUnitDivisor * s.Extras.Track;
        }

        public static int NavCents(MarketState s)
        {
            double nav = Nav(s);
            return nav >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)Math.Round(nav));
        }

        // A company's weight in the index after the pass that set the scratch.
        private static double Weight(MarketState s, int at, double whole)
        {
            Stock k = s.Stocks[at];
            return !k.Listed || whole <= 0.0 ? 0.0 : (_capped[at] ? MarketIndex.CapShare * whole : MarketIndex.Value(k)) / whole;
        }

        /// <summary>
        /// A deal quoted: nothing is changed unless <paramref name="book"/>, which adds each slice to the player's recent trading
        /// in its company, as an order of it would. <paramref name="held"/> is the units the dealer has already.
        /// </summary>
        public static FundDeal Quote(MarketState s, long units, bool buying, int phase, int least, long cash, long held, bool on, bool trading, bool waiting, bool book = false)
        {
            var d = new FundDeal { Units = units };
            double index = IndexNow(s, out double whole);
            double nav = index / ExtrasCatalogue.FundUnitDivisor * s.Extras.Track;
            d.NavCents = nav >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)Math.Round(nav));
            if (!on || nav < 1.0 || whole <= 0.0)
            {
                d.Why = FundWhy.Off;
                return d;
            }
            d.Most = Most(s, buying, held, nav, whole);
            if (waiting)
            {
                d.Why = FundWhy.Held;
                return d;
            }
            if (!trading)
            {
                d.Why = FundWhy.Closed;
                return d;
            }
            if (units <= 0)
            {
                d.Why = FundWhy.Bad;
                return d;
            }
            if (!buying && units > held)
            {
                d.Why = FundWhy.NoUnits;
                return d;
            }
            double money = units * nav / 100.0, ratio = 0.0;
            bool large = false;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                double w = Weight(s, i, whole);
                if (w <= 0.0)
                {
                    continue;
                }
                double mid = Math.Max(0.01, k.Mid);
                long qty = Math.Max(1L, (long)Math.Ceiling(money * w / mid));
                large |= qty > MarketTrading.MostPerOrder(k);
                Quote q = MarketTrading.QuoteOf(s, k, phase);
                double impact = MarketTrading.Impact(s, k, qty), own = Math.Exp(-s.Extras.PushOf(k.Id));
                ratio += w * (buying ? q.Ask / (mid * 100.0) * (1.0 + impact) * Math.Max(1.0, own) : q.Bid / (mid * 100.0) * (1.0 - impact) * Math.Min(1.0, own));
                if (book)
                {
                    k.Impact += qty;
                }
            }
            double price = nav * ratio;
            d.PriceCents = buying ? (int)Math.Min(int.MaxValue, Math.Ceiling(price)) : Math.Max(1, (int)Math.Floor(price));
            d.CostBp = (int)Math.Round(Math.Abs(ratio - 1.0) * 10000.0);
            d.Value = MarketTrading.Value(units, d.PriceCents, buying);
            d.Fee = MarketTrading.Commission(d.Value, least);
            if (!buying)
            {
                d.Fee = Math.Min(d.Fee, d.Value);
            }
            d.Total = buying ? d.Value + d.Fee : d.Value - d.Fee;
            if (large)
            {
                d.Why = FundWhy.TooLarge;
            }
            else if (buying && units > d.Most)
            {
                d.Why = FundWhy.HoldingLimit;
            }
            else if (buying && d.Total > cash)
            {
                d.Why = FundWhy.NoMoney;
            }
            return d;
        }

        /// <summary>The most units one deal may be: within what one holder may have of the market, and no slice more of its company than one order.</summary>
        public static long Most(MarketState s, bool buying, long held, double nav, double whole)
        {
            if (nav <= 0.0 || whole <= 0.0)
            {
                return 0L;
            }
            double most = double.MaxValue;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                double w = Weight(s, i, whole);
                if (w > 0.0)
                {
                    most = Math.Min(most, MarketTrading.MostPerOrder(k) * Math.Max(0.01, k.Mid) / w);   // money
                }
            }
            long byOrder = most >= double.MaxValue / 2 ? 0L : (long)Math.Floor(most * 100.0 / nav);
            if (!buying)
            {
                return Math.Max(0L, Math.Min(held, byOrder));
            }
            long room = (long)Math.Floor(whole * ExtrasCatalogue.FundHoldingBp / 10000.0 * 100.0 / nav) - held;
            return Math.Max(0L, Math.Min(room, byOrder));
        }

        /// <summary>What units are worth sold now, each slice at its company's bid: money.</summary>
        public static long Worth(MarketState s, long units, int phase)
        {
            if (units <= 0)
            {
                return 0L;
            }
            double index = IndexNow(s, out double whole);
            double nav = index / ExtrasCatalogue.FundUnitDivisor * s.Extras.Track, ratio = 0.0;
            if (nav <= 0.0 || whole <= 0.0)
            {
                return 0L;
            }
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                double w = Weight(s, i, whole);
                if (w > 0.0)
                {
                    Stock k = s.Stocks[i];
                    ratio += w * MarketTrading.QuoteOf(s, k, phase).Bid / (Math.Max(0.01, k.Mid) * 100.0);
                }
            }
            return MarketTrading.Value(units, Math.Max(1, (int)Math.Floor(nav * Math.Min(1.0, ratio))), false);
        }

        /// <summary>A deal booked in the player's units: what is kept keeps its average cost, what is sold is a gain or a loss taken.</summary>
        public static void Book(ExtrasState x, in FundDeal d, bool buying)
        {
            if (buying)
            {
                x.Units += d.Units;
                x.Cost += d.Total;
                return;
            }
            long part = x.Units > 0 ? x.Cost * d.Units / x.Units : 0L;
            x.Realised += d.Total - part;
            x.Cost -= part;
            x.Units -= d.Units;
            if (x.Units <= 0)
            {
                x.Units = 0;
                x.Cost = 0;
            }
        }

        /// <summary>
        /// A market day's close: the day's share of the yearly fee comes out of the fund's worth, once a day, and the units
        /// held now are owed their share of every dividend whose record day this is, on that company's own payment day.
        /// </summary>
        public static void Close(MarketState s, int day, double tradingYear)
        {
            ExtrasState x = s.Extras;
            if (x.TrackDay >= day)
            {
                return;   // this close has been counted: a day played again takes no second fee
            }
            x.TrackDay = day;
            double index = IndexNow(s, out double whole);
            double nav = index / ExtrasCatalogue.FundUnitDivisor * x.Track;
            if (x.Units > 0 && whole > 0.0 && nav > 0.0)
            {
                for (int i = 0; i < s.Stocks.Count; i++)
                {
                    Stock k = s.Stocks[i];
                    if (!k.Listed || k.RecordDay != day || k.DeclaredCents <= 0)
                    {
                        continue;
                    }
                    double shares = x.Units * nav * Weight(s, i, whole) / (Math.Max(0.01, k.Mid) * 100.0);   // of this company, behind the units held
                    long cents = (long)Math.Floor(shares * k.DeclaredCents);
                    if (cents > 0)
                    {
                        Owe(x, k.PayDay, cents);
                    }
                }
            }
            double fee = ExtrasCatalogue.FundFeeBp / 10000.0 / Math.Max(1.0, tradingYear);
            x.Fees += (long)Math.Floor(x.Units * nav * fee);
            x.Track *= 1.0 - fee;
        }

        private static void Owe(ExtrasState x, int payDay, long cents)
        {
            for (int i = 0; i < x.Dues.Count; i++)
            {
                if (x.Dues[i].PayDay == payDay)
                {
                    x.Dues[i] = new FundDue { PayDay = payDay, Cents = x.Dues[i].Cents + cents };
                    return;
                }
            }
            if (x.Dues.Count >= ExtrasCatalogue.FundDuesMost)
            {
                x.Dues[0] = new FundDue { PayDay = x.Dues[0].PayDay, Cents = x.Dues[0].Cents + cents };   // never lost: with the earliest
                return;
            }
            x.Dues.Add(new FundDue { PayDay = payDay, Cents = cents });
        }

        /// <summary>The distributions due by a day, taken off the list: whole money to pay now, the cents under a unit kept for the next.</summary>
        public static long Due(ExtrasState x, int day)
        {
            long cents = 0;
            for (int i = x.Dues.Count - 1; i >= 0; i--)
            {
                if (x.Dues[i].PayDay <= day)
                {
                    cents += x.Dues[i].Cents;
                    x.Dues.RemoveAt(i);
                }
            }
            if (cents <= 0)
            {
                return 0L;
            }
            long all = x.OddCents + cents;
            x.OddCents = all % 100L;
            return all / 100L;
        }

        /// <summary>
        /// A company leaves the index: the index's divisor keeps the index where it was, but the fund held the company and is
        /// paid only what its holders are. Called before the company is taken out, with what each share is paid in cents.
        /// </summary>
        public static void Left(MarketState s, Stock gone, int centsEach)
        {
            if (gone == null || !gone.Listed)
            {
                return;
            }
            IndexNow(s, out double whole);
            int at = s.Stocks.IndexOf(gone);
            if (at < 0 || whole <= 0.0)
            {
                return;
            }
            double w = Weight(s, at, whole), price = Math.Max(0.01, gone.Mid) * 100.0;
            double change = w * (Math.Max(0, centsEach) / price - 1.0);
            s.Extras.Track = Math.Max(1e-6, Math.Min(4.0, s.Extras.Track * (1.0 + change)));
        }
    }
}
