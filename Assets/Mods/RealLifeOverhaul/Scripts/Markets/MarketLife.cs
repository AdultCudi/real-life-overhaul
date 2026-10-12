using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The life of a listed company: how it is made, its business day by day, its quarters and results, its dividends,
    /// a split, a listing. Game-free. Every chance is a draw from a day's secret (<see cref="MarketRandom"/>) made when
    /// the thing happens: nothing that is still to come is ever decided ahead and kept in the save.
    /// </summary>
    public static class MarketLife
    {
        public const double SurpriseShare = 0.06;        // a quarter's profit against what was expected: the spread of the surprise
        public const double SurpriseRevenueShare = 0.004;// plus this much of the quarter's turnover
        public const double SurpriseStays = 0.6;         // of a surprise in the margin, what carries on into the next quarter
        public const double RevenueSurprise = 0.015;
        public const double IpoPopMean = 0.06, IpoPopSpread = 0.12;
        public const double StartSpread = 0.04;          // a new market's prices against what companies are worth, at the start

        // ---------------------------------------------------------------- making a company

        /// <summary>
        /// A company of the catalogue as a listed stock on a day, at the city's price level. Its shares in issue are worked
        /// out so that a share is worth its catalogue price (at that price level), or exactly 'startCents' if given (a
        /// company brought over from the old market keeps its price).
        /// </summary>
        public static Stock Make(CompanyDef d, string name, int day, in MarketEnv env, int year, ulong seed, int startCents)
        {
            var s = new Stock
            {
                Id = d.Id, Def = d, Name = string.IsNullOrEmpty(name) ? d.Name : name, Ticker = d.Ticker, Sector = d.Sector, Size = d.Size, Status = StockStatus.Listed,
                ListedDay = day
            };
            double level = Math.Max(100, env.PriceLevel) / 1000.0;
            s.Rev = d.RevenueM * 1000000.0 * level;
            s.GrowthTarget = MarketCatalogue.LongRealGrowth + MarketValuation.CityGrowth(s, env);
            s.Growth = d.GrowthBp / 10000.0 + MarketValuation.CityGrowth(s, env) + 0.01 * MarketRandom.Normal(seed, MarketRandom.Start, d.Id, 1);
            s.MarginTarget = MarketValuation.TargetMargin(s, env);
            s.Margin = s.MarginTarget + 0.004 * MarketRandom.Normal(seed, MarketRandom.Start, d.Id, 2);
            s.Debt = d.DebtPct / 100.0 * s.Rev;
            s.DebtRate = MarketValuation.UsualDebtRate(s, env);
            s.Shares = 1000000;
            s.Distress = MarketValuation.DistressOf(s, env);
            double profit = MarketValuation.Profit(s, s.Margin, MarketInputs.TaxRate(env));
            s.Equity = Math.Max(s.Rev * 0.15, profit / MarketCatalogue.RoeLong);
            double price = startCents > 0 ? startCents / 100.0 : d.PriceCents / 100.0 * level;
            double fair = MarketValuation.Fair(s, env);
            s.Shares = Math.Max(100000L, (long)Math.Round(fair * s.Shares / Math.Max(0.5, price)));
            s.Adv = s.Shares * d.TurnoverBp / 10000.0;
            // The quarterly dividend the company pays: what it can pay out of its earnings (MarketValuation.Payout).
            double eps = MarketValuation.Earnings(s, env);
            s.DpsCents = eps > 0.0 ? Math.Max(0, (int)Math.Round(eps * MarketValuation.Payout(MarketValuation.GrowthNow(s, env)) / 4.0 * 100.0)) : 0;
            s.Quarter = MarketCatalogue.QuarterOf(day, year);
            // The quarter so far, as if the company had been trading at today's rate since it began.
            int elapsed = Math.Max(0, day - MarketCatalogue.QuarterStart(s.Quarter, year));
            s.QRev = s.Rev / year * elapsed;
            s.QNi = profit / year * elapsed;
            s.LastExDay = day - Math.Max(1, (day - MarketCatalogue.QuarterStart(s.Quarter, year)) % Math.Max(1, year / 4));
            s.LogFair = Math.Log(MarketValuation.Fair(s, env) + Accrued(s, day, year));
            // The market's mood about it to begin with (a price brought over keeps its price exactly).
            s.Mis = startCents > 0 ? Math.Log(startCents / 100.0) - s.LogFair : StartSpread * MarketRandom.Normal(seed, MarketRandom.Start, d.Id, 3);
            s.Sent = s.Mis;
            int cents = startCents > 0 ? startCents : s.MidCents;
            s.PrevClose = s.Open = s.High = s.Low = s.Last = cents;
            PastResults(s, day, year, seed, env);
            return s;
        }

        // Four quarters of results before the market's first day, from the business as it stands: something for the figures page.
        private static void PastResults(Stock s, int day, int year, ulong seed, in MarketEnv env)
        {
            double profit = MarketValuation.Profit(s, s.Margin, MarketInputs.TaxRate(env));
            int quarter = MarketCatalogue.QuarterOf(day, year);
            for (int back = 4; back >= 1; back--)
            {
                double scale = Math.Exp(-(s.Growth + env.InflationBp / 10000.0) * back / 4.0);
                double surprise = SurpriseShare * MarketRandom.Normal(seed, MarketRandom.Start, s.Id, 10 + back);
                double ni = profit / 4.0 * scale;
                double reported = ni + Math.Abs(ni) * surprise;
                s.Results.Add(new QuarterResult
                {
                    Quarter = quarter - back, Day = Math.Max(1, day - back * Math.Max(2, year / 4)), RevenueK = (long)(s.Rev / 4.0 * scale / 1000.0),
                    ProfitK = (long)(reported / 1000.0), Eps = PerShare(reported, s.Shares), Consensus = PerShare(ni, s.Shares), DpsCents = s.DpsCents
                });
            }
        }

        /// <summary>Money a share in hundredths of a cent.</summary>
        public static int PerShare(double money, long shares)
        {
            if (shares <= 0)
            {
                return 0;
            }
            double v = money / shares * 10000.0;
            return v > int.MaxValue ? int.MaxValue : v < int.MinValue ? int.MinValue : (int)Math.Round(v);
        }

        // ---------------------------------------------------------------- the business, one day of the calendar

        /// <summary>
        /// One day of a company's business. Its margin and growth are where the day's city puts them (the cycle, the
        /// council, the day's supply, inflation it cannot pass on: at once, so the market can price a change the moment
        /// it is known) together with its own ups and downs, which fade (a margin's in about a year, its own growth over
        /// about ten years) while new ones come. Its turnover grows by its growth and inflation, the player's competition
        /// takes its share, its debt comes back to its usual share of turnover and takes on the bank's rate, and the
        /// quarter's figures add up. Then what the market expects of it comes through a little (<see cref="BearOut"/>).
        /// 'n' says which day of the calendar this is, so each day has its own draws.
        /// </summary>
        public static void Business(Stock s, in MarketEnv env, int year, ulong secret, int n, double competition)
        {
            double y = Math.Max(8, year);
            double root = Math.Sqrt(y);
            CompanyDef d = s.Def;
            double target = MarketValuation.TargetMargin(s, env);
            double own = (s.Margin - s.MarginTarget) * (1.0 - 1.0 / (MarketCatalogue.MarginReturnYears * y))
                         + MarketCatalogue.MarginNoiseYear / root * MarketRandom.Normal(secret, MarketRandom.Margin, s.Id, n);
            s.Margin = MarketValuation.Clamp(target + own, -0.5, 0.8);
            s.MarginTarget = target;
            double growthTarget = MarketCatalogue.LongRealGrowth + MarketValuation.CityGrowth(s, env);
            double ownGrowth = (s.Growth - s.GrowthTarget) * (1.0 - 1.0 / (MarketCatalogue.GrowthFadeYears * y))
                               + MarketCatalogue.GrowthNoiseYear / root * MarketRandom.Normal(secret, MarketRandom.Growth, s.Id, n);
            s.Growth = MarketValuation.Clamp(growthTarget + ownGrowth, -0.25, 0.4);
            s.GrowthTarget = growthTarget;
            double nominal = s.Growth + env.InflationBp / 10000.0 * d.PassPct / 100.0;
            s.Rev *= Math.Exp(nominal / y + MarketCatalogue.RevenueNoiseYear / root * MarketRandom.Normal(secret, MarketRandom.Revenue, s.Id, n));
            // The player's businesses in its trade take a share of its turnover (the change since yesterday).
            if (Math.Abs(competition - s.Competition) > 1e-9)
            {
                s.Rev *= (1.0 - competition) / Math.Max(0.5, 1.0 - s.Competition);
                s.Competition = competition;
            }
            s.DebtRate += (MarketValuation.UsualDebtRate(s, env) - s.DebtRate) / (MarketCatalogue.DebtRepriceYears * y);
            double profit = MarketValuation.Profit(s, s.Margin, MarketInputs.TaxRate(env));
            // Debt comes back to its usual share of turnover; a loss is borrowed.
            s.Debt += (d.DebtPct / 100.0 * s.Rev - s.Debt) / (MarketCatalogue.DebtRepriceYears * y) + (profit < 0.0 ? -profit / y : 0.0);
            s.Debt = Math.Max(0.0, s.Debt);
            s.Equity += profit / y;
            s.QRev += s.Rev / y;
            s.QNi += profit / y;
            s.Distress = MarketValuation.DistressOf(s, env);
            BearOut(s, env, y);
        }

        /// <summary>
        /// What the market expects of a business beyond its figures (<see cref="Stock.Outlook"/>) comes through in them,
        /// a day's share of it: its turnover moves (its debt with it, so it is the same business, larger or smaller), and
        /// the expectation that is left is less by as much. The price is not moved by it: the market's worth counts each
        /// year's payouts with the part of the expectation that will have come through by then
        /// (<see cref="MarketValuation.Worth"/>), so what comes through today was already in the price.
        /// </summary>
        public static void BearOut(Stock s, in MarketEnv env, double y)
        {
            double outlook = s.Outlook;
            if (Math.Abs(outlook) < 1e-9)
            {
                return;
            }
            double moved = outlook * (1.0 - Math.Exp(-1.0 / (MarketCatalogue.OutlookYears * y)));
            double grow = Math.Exp(moved);
            s.Rev *= grow;
            s.Debt *= grow;
            s.Outlook = outlook - moved;
        }

        /// <summary>A quarter has ended on the calendar: its figures wait for the results, which are set for a day in the next weeks.</summary>
        public static void CloseQuarter(Stock s, int quarter, int year)
        {
            s.PendingQuarter = s.Quarter;
            s.PendingRev = s.QRev;
            s.PendingNi = s.QNi;
            s.Quarter = quarter;
            s.QRev = s.QNi = 0.0;
            int end = MarketCatalogue.QuarterStart(quarter, year) - 1;
            int lagPct = MarketCatalogue.ResultsLagLeastPct + (int)(MarketRandom.Mix32((uint)s.Id * 7919u) % (uint)(MarketCatalogue.ResultsLagMostPct - MarketCatalogue.ResultsLagLeastPct + 1));
            int day = end + Math.Max(1, (int)Math.Round(year * lagPct / 100.0));
            while (!MarketCatalogue.TradingDay(day))
            {
                day++;
            }
            s.ResultsDay = day;
        }

        /// <summary>
        /// The results of the quarter that waits: what the market expected, and what came out (a surprise drawn now,
        /// never before). The business takes in most of the surprise, the market reads it as news of the years to come
        /// (<see cref="MarketCatalogue.ResultsReaction"/>), the board declares the dividend, and the news is added. The
        /// price answers at the open, when it is worked out again: nobody can trade on results before the market has.
        /// </summary>
        public static MarketNews Results(Stock s, int day, int year, ulong secret, in MarketEnv env)
        {
            double expected = s.PendingNi;
            double surprise = SurpriseShare * MarketRandom.Fat(secret, MarketRandom.ResultsShock, MarketRandom.OwnTail, s.Id, 1);
            double reported = expected + Math.Abs(expected) * surprise + s.PendingRev * SurpriseRevenueShare * MarketRandom.Normal(secret, MarketRandom.ResultsShock, s.Id, 2);
            double revenue = s.PendingRev * (1.0 + RevenueSurprise * MarketRandom.Normal(secret, MarketRandom.ResultsShock, s.Id, 3));
            if (s.PendingRev > 0.0)
            {
                s.Margin += SurpriseStays * (reported - expected) / Math.Max(1.0, s.PendingRev) / (1.0 - MarketInputs.TaxRate(env));
                s.Margin = MarketValuation.Clamp(s.Margin, -0.5, 0.8);
            }
            s.Equity += reported - expected;
            double scale = Math.Max(Math.Abs(expected), s.PendingRev * 0.02);
            if (scale > 0.0)
            {
                s.Outlook += MarketCatalogue.ResultsReaction * MarketValuation.Clamp((reported - expected) / scale, -1.0, 1.0);
            }
            int eps = PerShare(reported, s.Shares), consensus = PerShare(expected, s.Shares);
            // The board's dividend: towards what it can pay out of the last four quarters' earnings; kept if the cut would be small.
            long lastYear = eps;
            for (int i = s.Results.Count - 1, n = 1; i >= 0 && n < 4; i--, n++)
            {
                lastYear += s.Results[i].Eps;
            }
            double target = lastYear > 0 ? lastYear / 10000.0 * MarketValuation.Payout(MarketValuation.GrowthNow(s, env)) / 4.0 * 100.0 : 0.0;
            double dps = s.DpsCents + MarketCatalogue.DividendSmooth * (target - s.DpsCents);
            if (target < s.DpsCents && target >= s.DpsCents * 0.8)
            {
                dps = s.DpsCents;
            }
            if (eps < 0 && s.Distress > 0.5)
            {
                dps = 0.0;
            }
            int declared = Math.Max(0, (int)Math.Round(dps));
            s.DpsCents = declared;
            s.DeclaredCents = declared;
            s.RecordDay = declared > 0 ? MarketTrading.MarketDays(day, MarketCatalogue.DividendRecordDays) : 0;
            s.PayDay = declared > 0 ? MarketTrading.MarketDays(s.RecordDay, MarketCatalogue.DividendPayDays) : 0;
            s.ExDone = declared <= 0;
            s.Results.Add(new QuarterResult
            {
                Quarter = s.PendingQuarter, Day = day, RevenueK = (long)(revenue / 1000.0), ProfitK = (long)(reported / 1000.0), Eps = eps, Consensus = consensus, DpsCents = declared
            });
            while (s.Results.Count > MarketCatalogue.QuartersKept)
            {
                s.Results.RemoveAt(0);
            }
            s.PendingQuarter = -1;
            s.ResultsDay = 0;
            s.ResultsBoostDay = day;
            return new MarketNews { Day = day, Kind = MarketNewsKind.Results, Stock = s.Id, A = eps, B = consensus };
        }

        // ---------------------------------------------------------------- the dividend built up in the price

        /// <summary>
        /// The dividend the price has built up since the last one went, in money: the next dividend (the one declared,
        /// or the last one's size while none is) times the share of the way to its record day. So a price does not jump
        /// when a dividend is declared, holds all of it on the record day, and drops by all of it the morning after.
        /// </summary>
        public static double Accrued(Stock s, int day, int year)
        {
            bool declared = s.DeclaredCents > 0 && !s.ExDone && s.RecordDay > 0;
            int cents = declared ? s.DeclaredCents : s.DpsCents;
            if (cents <= 0)
            {
                return 0.0;
            }
            int span = declared ? s.RecordDay - s.LastExDay : Math.Max(2, year / 4);   // (whole by the record day: the drop the morning after is all of it)
            double share = MarketValuation.Clamp((day - s.LastExDay) / (double)Math.Max(1, span), 0.0, 1.0);
            return cents / 100.0 * share;
        }

        /// <summary>
        /// What the market thinks a share is worth today, as a logarithm: the business's worth with what the market
        /// expects of it and the dividend built up, or under a takeover bid the bid less the chance it falls through.
        /// </summary>
        public static double LogFairOf(Stock s, in MarketEnv env, int day, int year)
        {
            double fair = MarketValuation.Fair(s, env) + Accrued(s, day, year);
            if (s.BidCents > 0)
            {
                double bid = s.BidCents / 100.0;
                fair = bid - MarketCatalogue.TakeoverFailChance * Math.Max(0.0, bid - fair);
            }
            return Math.Log(Math.Max(0.01, fair));
        }

        // ---------------------------------------------------------------- a split

        /// <summary>
        /// A split takes effect: so many new shares for each, every price and history divided, every holding and order of
        /// the player's multiplied. What each holding is worth and what it cost are unchanged; an order's price is
        /// rounded so that it is no more likely to be filled than before.
        /// </summary>
        public static void Split(MarketState state, Stock s, int ratio)
        {
            if (ratio <= 1)
            {
                return;
            }
            double ln = Math.Log(ratio);
            s.Shares *= ratio;
            s.LogFair -= ln;
            s.Adv *= ratio;
            s.Impact *= ratio;
            s.PrevClose = Div(s.PrevClose, ratio);
            s.Open = Div(s.Open, ratio);
            s.High = Div(s.High, ratio);
            s.Low = Div(s.Low, ratio);
            s.Last = s.MidCents;
            s.DayVolume *= ratio;
            s.DpsCents = (int)Math.Round(s.DpsCents / (double)ratio);
            s.DeclaredCents = (int)Math.Round(s.DeclaredCents / (double)ratio);
            if (s.BidCents > 0)
            {
                s.BidCents = Div(s.BidCents, ratio);
            }
            for (int i = 0; i < s.BarCount; i++)
            {
                s.Bars[i] = MarketHistory.Split(s.Bars[i], ratio);
            }
            for (int i = 0; i < s.LastBarCount; i++)
            {
                s.LastBars[i] = MarketHistory.Split(s.LastBars[i], ratio);
            }
            MarketHistory.Split(s.Daily, ratio);
            MarketHistory.Split(s.Monthly, ratio);
            for (int i = 0; i < s.Results.Count; i++)
            {
                QuarterResult r = s.Results[i];
                r.Eps = (int)Math.Round(r.Eps / (double)ratio);
                r.Consensus = (int)Math.Round(r.Consensus / (double)ratio);
                r.DpsCents = (int)Math.Round(r.DpsCents / (double)ratio);
                s.Results[i] = r;
            }
            Position p = state.Position(s.Id);
            if (p != null)
            {
                p.Shares *= ratio;   // what they cost is the same money
            }
            for (int i = 0; i < state.Orders.Count; i++)
            {
                Order o = state.Orders[i];
                if (o.StockId != s.Id || !o.IsOpen)
                {
                    continue;
                }
                o.Qty *= ratio;
                o.Filled *= ratio;
                bool buying = o.Side == OrderSide.Buy;
                // A buy limit and a sell stop rounded down, a sell limit and a buy stop up: never easier to fill than before.
                if (o.LimitCents > 0)
                {
                    o.LimitCents = buying ? Math.Max(1, o.LimitCents / ratio) : (o.LimitCents + ratio - 1) / ratio;
                }
                if (o.StopCents > 0)
                {
                    o.StopCents = buying ? (o.StopCents + ratio - 1) / ratio : Math.Max(1, o.StopCents / ratio);
                }
            }
            s.SplitDay = 0;
            s.SplitRatio = 0;
        }

        private static int Div(int cents, int ratio)
        {
            return cents <= 0 ? cents : Math.Max(1, (int)Math.Round(cents / (double)ratio, MidpointRounding.AwayFromZero));
        }

        /// <summary>The ratio a board splits by at a price: two for one, three for one, five for one.</summary>
        public static int SplitRatioAt(int cents)
        {
            return cents >= 90000 ? 5 : cents >= 60000 ? 3 : 2;
        }

        // ---------------------------------------------------------------- a past for a new market

        /// <summary>
        /// A history for a company that has just come to a new market, ending at its price today: the days before, walked
        /// back with the moves its prices have (closes brought over from the old market are used as they are), and some
        /// months before those. Made once, when the market is opened for a save.
        /// </summary>
        public static void MakePast(Stock s, int day, int year, ulong seed, double dayVol, int[] knownCloses, int knownCount)
        {
            s.Daily.Clear();
            s.Monthly.Clear();
            int days = MarketCatalogue.DailyKept;
            var tradingDays = new List<int>(days);
            int d = day - 1;
            while (tradingDays.Count < days)
            {
                if (MarketCatalogue.TradingDay(d))
                {
                    tradingDays.Add(d);
                }
                d--;
            }
            double logPrice = Math.Log(Math.Max(1, s.Last) / 100.0);
            double volume = s.Adv;
            var past = new List<Candle>(days);
            for (int i = 0; i < tradingDays.Count; i++)
            {
                int at = tradingDays[i];
                int closeCents;
                if (knownCloses != null && i < knownCount && knownCloses[knownCount - 1 - i] > 0)
                {
                    closeCents = knownCloses[knownCount - 1 - i];
                    logPrice = Math.Log(closeCents / 100.0);
                }
                else
                {
                    closeCents = Math.Max(1, (int)Math.Round(Math.Exp(logPrice) * 100.0));
                }
                // The day's own move, back from its close to its open, and its range.
                double move = dayVol * MarketRandom.Fat(seed, MarketRandom.Start, MarketRandom.OwnTail, s.Id * 1000 + i, 20);
                double openLog = logPrice - move * 0.8;
                double c = closeCents / 100.0, o = Math.Exp(openLog);
                double hi = Math.Max(o, c) * Math.Exp(Math.Abs(dayVol * 0.5 * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + i, 21)));
                double lo = Math.Min(o, c) * Math.Exp(-Math.Abs(dayVol * 0.5 * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + i, 22)));
                long v = (long)(volume * Math.Exp(0.3 * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + i, 23)) * (1.0 + 0.5 * Math.Abs(move) / Math.Max(1e-6, dayVol)));
                past.Add(new Candle(at, Cents(o), Math.Max(Cents(hi), Math.Max(Cents(o), closeCents)), Math.Min(Cents(lo), Math.Min(Cents(o), closeCents)), closeCents, v));
                logPrice = openLog - dayVol * 0.3 * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + i, 24);   // the night before
            }
            // Months before the daily window, a month's move at a time.
            int months = 24;
            var older = new List<Candle>(months);
            int month = past.Count > 0 ? MarketCatalogue.MonthOf(past[past.Count - 1].Day, year) - 1 : MarketCatalogue.MonthOf(day, year) - 1;
            double monthVol = dayVol * Math.Sqrt(Math.Max(3.0, MarketCatalogue.TradingDaysPerYear(year) / 12.0));
            for (int m = 0; m < months; m++, month--)
            {
                double move = monthVol * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + 500 + m, 30);
                double c = Math.Exp(logPrice), o = Math.Exp(logPrice - move);
                double hi = Math.Max(o, c) * Math.Exp(Math.Abs(monthVol * 0.4 * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + 500 + m, 31)));
                double lo = Math.Min(o, c) * Math.Exp(-Math.Abs(monthVol * 0.4 * MarketRandom.Normal(seed, MarketRandom.Start, s.Id * 1000 + 500 + m, 32)));
                long v = (long)(volume * Math.Max(3.0, MarketCatalogue.TradingDaysPerYear(year) / 12.0));
                older.Add(new Candle(MarketHistory.MonthStart(month, year), Cents(o), Cents(hi), Cents(lo), Cents(c), v));
                logPrice -= move;
            }
            for (int i = older.Count - 1; i >= 0; i--)
            {
                s.Monthly.Add(older[i]);
            }
            for (int i = past.Count - 1; i >= 0; i--)
            {
                s.Daily.Add(past[i]);
            }
            if (s.Daily.Count > 0)
            {
                s.PrevClose = s.Daily[s.Daily.Count - 1].C;
            }
        }

        private static int Cents(double money)
        {
            return Math.Max(1, (int)Math.Round(money * 100.0));
        }
    }
}
