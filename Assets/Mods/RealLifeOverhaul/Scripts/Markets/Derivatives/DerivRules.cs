using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The futures' and options' rules. Game-free. Their days (the next four Fridays at least two closes away), what one is
    /// offered at (the fair price with the exchange's spread around it), its margin, what it may be on and how much, and
    /// the money of a day's move or its end, every sum rounded against the player. Asked when a page is written or a
    /// contract bought, sold or settled, never per frame.
    /// </summary>
    public static class DerivRules
    {
        public const int ClosesLeast = 2;   // a contract's day is at least this many closes away when it is bought

        /// <summary>The market days the contracts on offer settle at, the nearest first; how many (DerivCatalogue.WeeksAhead).</summary>
        public static int Days(int day, int phase, int[] into)
        {
            int found = 0;
            for (int d = day; found < into.Length && found < DerivCatalogue.WeeksAhead && d < day + 7 * (DerivCatalogue.WeeksAhead + 2); d++)
            {
                if (MarketCatalogue.Weekday(d) == DerivCatalogue.SettleWeekday && MarketCatalogue.TradingDay(d) && Closes(day, phase, d) >= ClosesLeast)
                {
                    into[found++] = d;
                }
            }
            return found;
        }

        /// <summary>Whether a day is one of those on offer now.</summary>
        public static bool Offered(int day, int phase, int settleDay)
        {
            if (MarketCatalogue.Weekday(settleDay) != DerivCatalogue.SettleWeekday || Closes(day, phase, settleDay) < ClosesLeast)
            {
                return false;
            }
            int ahead = 0;
            for (int d = day; d <= settleDay; d++)
            {
                if (MarketCatalogue.Weekday(d) == DerivCatalogue.SettleWeekday && Closes(day, phase, d) >= ClosesLeast)
                {
                    ahead++;
                }
            }
            return ahead >= 1 && ahead <= DerivCatalogue.WeeksAhead;
        }

        /// <summary>Market closes from now to a day's close, that close counted; today's if it is still to come.</summary>
        public static int Closes(int day, int phase, int toDay)
        {
            int closes = 0;
            for (int d = day; d <= toDay; d++)
            {
                if (MarketCatalogue.TradingDay(d) && (d > day || phase < MarketCatalogue.PhaseClose))
                {
                    closes++;
                }
            }
            return closes;
        }

        /// <summary>The first day after a settlement day: the week a material's contract is for begins then.</summary>
        public static int WeekFrom(int settleDay)
        {
            return MarketTrading.NextTradingDay(settleDay);
        }

        /// <summary>Micros a unit as a price is quoted, four figures kept: an option's strike.</summary>
        public static long Round(long micros)
        {
            if (micros <= 0)
            {
                return 1;
            }
            long scale = 1;
            while (micros / scale >= 10000)
            {
                scale *= 10;
            }
            return Math.Max(1L, (micros + scale / 2) / scale * scale);
        }

        /// <summary>An option's strike: the price now moved by one of the ticket's steps, four figures kept.</summary>
        public static long Strike(long priceMicros, int stepBp)
        {
            return Round((long)Math.Round(priceMicros * (1.0 + stepBp / 10000.0)));
        }

        /// <summary>Money for so many units at a price in micros: a purchase rounded up to the whole unit, a sale down.</summary>
        public static long Money(long qty, long micros, bool paying)
        {
            if (qty <= 0 || micros <= 0)
            {
                return 0;
            }
            // micros x units can pass a long's range only far beyond the limits; split the product so it never does.
            long whole = micros / DerivCatalogue.Micro, part = micros % DerivCatalogue.Micro;
            long money = whole * qty + part * qty / DerivCatalogue.Micro;
            bool rest = part * qty % DerivCatalogue.Micro != 0;
            return paying && rest ? money + 1 : money;
        }

        /// <summary>Cents a future's holder makes on a move from one price to another (under nothing, loses): rounded against them.</summary>
        public static long Move(ContractKind kind, long qty, long fromMicros, long toMicros)
        {
            long diff = (toMicros - fromMicros) * qty;   // micro-money
            if (kind == ContractKind.Short)
            {
                diff = -diff;
            }
            const long PerCent = DerivCatalogue.Micro / 100;
            return diff >= 0 ? diff / PerCent : -((-diff + PerCent - 1) / PerCent);
        }

        /// <summary>A future's prices either side of its fair one: the exchange's half spread, the buyer's rounded up.</summary>
        public static void FutureSides(long fair, out long ask, out long bid)
        {
            ask = (long)Math.Ceiling(fair * (1.0 + DerivCatalogue.FutureHalfSpreadBp / 10000.0));
            bid = Math.Max(1L, (long)Math.Floor(fair * (1.0 - DerivCatalogue.FutureHalfSpreadBp / 10000.0)));
            ask = Math.Max(ask, bid + 1);
        }

        /// <summary>An option's prices from its fair premium and the price under it: a markup and half a spread each way.</summary>
        public static void OptionSides(double fairMicros, long underMicros, out long ask, out long bid)
        {
            double half = underMicros * DerivCatalogue.OptionHalfSpreadBp / 10000.0;
            ask = (long)Math.Ceiling(fairMicros * (1.0 + DerivCatalogue.OptionMarkupBp / 10000.0) + half);
            bid = Math.Max(0L, (long)Math.Floor(fairMicros * (1.0 - DerivCatalogue.OptionCutBp / 10000.0) - half));
            ask = Math.Max(1L, Math.Max(ask, bid + 1));
        }

        /// <summary>A future's margin as a share of its worth: three spreads of a week's move, in its bounds, whole per cent.</summary>
        public static int MarginPct(MaterialGroup group)
        {
            double week = MaterialRules.Spread(group, 5);
            int pct = (int)Math.Ceiling(week * DerivCatalogue.MarginSdTimes * 100.0);
            return Math.Max(DerivCatalogue.MarginLeastPct, Math.Min(DerivCatalogue.MarginMostPct, pct));
        }

        public static long MarginMoney(long value, MaterialGroup group)
        {
            return (value * MarginPct(group) + 99) / 100;
        }

        /// <summary>The margins of the open futures, in cents.</summary>
        public static long Needed(DerivBook book)
        {
            long need = 0;
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                if (c.IsOpen && c.IsFuture)
                {
                    need += c.Margin;
                }
            }
            return need;
        }

        /// <summary>What must stay in the margin account at a close, in cents: the maintenance share of the margins.</summary>
        public static long Maintenance(DerivBook book)
        {
            return (Needed(book) * DerivCatalogue.MaintenancePct + 99) / 100;
        }

        public static int OpenCount(DerivBook book)
        {
            int n = 0;
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                n += book.Contracts[i].IsOpen ? 1 : 0;
            }
            return n;
        }

        /// <summary>Units under open contracts on a material, every kind together.</summary>
        public static long UnitsOn(DerivBook book, string material)
        {
            long units = 0;
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                if (c.IsOpen && !c.OnShares && string.Equals(c.Material, material, StringComparison.Ordinal))
                {
                    units += c.Qty;
                }
            }
            return units;
        }

        /// <summary>Shares under open options on a company.</summary>
        public static long SharesOn(DerivBook book, int stockId)
        {
            long shares = 0;
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                if (c.IsOpen && c.StockId == stockId)
                {
                    shares += c.Qty;
                }
            }
            return shares;
        }

        /// <summary>The most shares under option on a company: a share of a day's usual volume and of the most a player may hold.</summary>
        public static long SharesMost(Stock s)
        {
            long byVolume = (long)(s.Adv * DerivCatalogue.ShareOptionAdvPct / 100.0);
            long byHolding = MarketTrading.MostHeld(s) * DerivCatalogue.ShareOptionHoldBp / 10000L;
            return Math.Max(0L, Math.Min(byVolume, byHolding));
        }

        /// <summary>
        /// Whether options are written on a company: the city's own (no player's, no rival's holding, no property trust), large
        /// or middling, on the market a while, not under a bid and not near failing.
        /// </summary>
        public static DerivWhy ShareOffered(Stock s, int day)
        {
            if (s == null || !s.Listed)
            {
                return DerivWhy.Unknown;
            }
            if (s.Own != null || s.Def == null || s.Def.Holding || s.RivalId != null || MarketCatalogue.IsRival(s.Id) || TrustRules.Is(s) || s.Size == MarketSize.Small
                || day - s.ListedDay < DerivCatalogue.ShareListedDaysLeast || s.BidCents > 0 || s.Distress >= DerivCatalogue.ShareDistressMost)
            {
                return DerivWhy.NotOffered;
            }
            return DerivWhy.None;
        }

        /// <summary>The price a share option settles at, in micros: the mean of the last hour of its day's steps, else the last price.</summary>
        public static long SettleMicros(Stock s)
        {
            int n = Math.Min(DerivCatalogue.SettleSteps, s.BarCount);
            if (n <= 0)
            {
                return Math.Max(1, s.Last) * (DerivCatalogue.Micro / 100);
            }
            long sum = 0;
            for (int i = s.BarCount - n; i < s.BarCount; i++)
            {
                sum += Math.Max(1, s.Bars[i].C);
            }
            return sum * (DerivCatalogue.Micro / 100) / n;
        }

        /// <summary>A share's split: every option on it covers ratio times the shares at a strike a ratio smaller, worth the same.</summary>
        public static void Split(DerivBook book, int stockId, int ratio)
        {
            if (ratio <= 1)
            {
                return;
            }
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                if (c.StockId != stockId || !c.IsOpen)
                {
                    continue;
                }
                c.Qty *= ratio;
                // A call's strike rounded up and a put's down: never worth more than before.
                c.Price = c.Kind == ContractKind.Call ? (c.Price + ratio - 1) / ratio : Math.Max(1L, c.Price / ratio);
                c.Premium = Math.Max(0L, c.Premium / ratio);
            }
        }

        /// <summary>Ended contracts older than the tab keeps them go.</summary>
        public static void Trim(DerivBook book, int day)
        {
            for (int i = book.Contracts.Count - 1; i >= 0; i--)
            {
                DerivContract c = book.Contracts[i];
                if (!c.IsOpen && day - c.EndDay > DerivCatalogue.EndedKeptDays * 7 / 5)   // market days as calendar days
                {
                    book.Contracts.RemoveAt(i);
                }
            }
        }
    }
}
