using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The life of a listed company: made, its business day by day, quarters, results, dividends, a split. Game-free. Every
    /// chance is drawn from a day's secret when the thing happens: nothing to come is decided ahead and saved.
    /// </summary>
    public static class MarketLife
    {
        public const double SurpriseShare = 0.06;        // of expected profit: the surprise's spread
        public const double SurpriseRevenueShare = 0.004;// plus this much of the quarter's turnover
        public const double SurpriseStays = 0.6;         // of a margin surprise, what carries into the next quarter
        public const double RevenueSurprise = 0.015;
        public const double IpoPopMean = 0.06, IpoPopSpread = 0.12;
        public const double StartSpread = 0.04;          // prices against worth when a new market starts

        /// <summary>
        /// A catalogue company as a listed stock on a day. Its shares are as many as make a share worth its catalogue price at
        /// the price level, or exactly <paramref name="startCents"/> when given.
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
            // The quarterly dividend: what it can pay out of its earnings.
            double eps = MarketValuation.Earnings(s, env);
            s.DpsCents = eps > 0.0 ? Math.Max(0, (int)Math.Round(eps * MarketValuation.PayoutOf(s, MarketValuation.GrowthNow(s, env)) / 4.0 * 100.0)) : 0;
            s.Quarter = MarketCatalogue.QuarterOf(day, year);
            // The quarter so far, as if it had traded at today's rate since the quarter began.
            int elapsed = Math.Max(0, day - MarketCatalogue.QuarterStart(s.Quarter, year));
            s.QRev = s.Rev / year * elapsed;
            s.QNi = profit / year * elapsed;
            s.LastExDay = day - Math.Max(1, (day - MarketCatalogue.QuarterStart(s.Quarter, year)) % Math.Max(1, year / 4));
            if (TrustRules.Is(s))
            {
                TrustRules.Begin(s, day, year);   // pays every few days: its first distribution
            }
            s.LogFair = Math.Log(MarketValuation.Fair(s, env) + Accrued(s, day, year));
            // The mood to begin with; a price brought over is kept exactly.
            s.Mis = startCents > 0 ? Math.Log(startCents / 100.0) - s.LogFair : StartSpread * MarketRandom.Normal(seed, MarketRandom.Start, d.Id, 3);
            s.Sent = s.Mis;
            int cents = startCents > 0 ? startCents : s.MidCents;
            s.PrevClose = s.Open = s.High = s.Low = s.Last = cents;
            PastResults(s, day, year, seed, env);
            return s;
        }

        // Four quarters of results before the market's first day, from the business as it stands, for the figures page.
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

        /// <summary>
        /// One day of a company's business, <paramref name="n"/> the calendar day for its draws. Margin and growth go where the
        /// day's city puts them at once, so a change is priced the moment it is known, with its own swings fading as new ones
        /// come; turnover grows, competition takes its share, debt drifts to its usual, and the outlook comes through a little.
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
            // The player's businesses in its trade take a share of its turnover, by the change since yesterday.
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
        /// A day's share of what the market expects beyond the figures comes through in them: turnover and debt move by it and
        /// the outlook left is less by as much. The price does not move: the worth already counted it.
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

        /// <summary>A quarter ended: its figures wait for results, set for a day in the next weeks.</summary>
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
        /// The waiting quarter's results, the surprise drawn now: the business takes most of it, the market reads it as news of
        /// the years to come, and the board declares the dividend. The price answers at the open.
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
            // The board moves its dividend towards what the last four quarters' earnings pay; a small cut is not made.
            long lastYear = eps;
            for (int i = s.Results.Count - 1, n = 1; i >= 0 && n < 4; i--, n++)
            {
                lastYear += s.Results[i].Eps;
            }
            double target = lastYear > 0 ? lastYear / 10000.0 * MarketValuation.PayoutOf(s, MarketValuation.GrowthNow(s, env)) / 4.0 * 100.0 : 0.0;
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
            if (!TrustRules.Is(s))
            {
                s.DeclaredCents = declared;
                s.RecordDay = declared > 0 ? MarketTrading.MarketDays(day, MarketCatalogue.DividendRecordDays) : 0;
                s.PayDay = declared > 0 ? MarketTrading.MarketDays(s.RecordDay, MarketCatalogue.DividendPayDays) : 0;
                s.ExDone = declared <= 0;
            }
            // A property trust's distributions follow its rate (TrustRules.Declare).
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

        /// <summary>
        /// The dividend built up in the price since the last, in money: the next one times the share of the way to its record
        /// day, so a price holds all of it on the record day and drops by it the morning after.
        /// </summary>
        public static double Accrued(Stock s, int day, int year)
        {
            if (s.Own != null)
            {
                return s.Own.Rps;   // a player's company: its reserve a share is in the price (ListingRules)
            }
            bool declared = s.DeclaredCents > 0 && !s.ExDone && s.RecordDay > 0;
            if (!declared && TrustRules.Is(s))
            {
                return 0.0;   // a property trust with nothing declared this period
            }
            int cents = declared ? s.DeclaredCents : s.DpsCents;
            if (cents <= 0)
            {
                return 0.0;
            }
            int span = declared ? s.RecordDay - s.LastExDay : Math.Max(2, year / 4);   // whole by the record day
            double share = MarketValuation.Clamp((day - s.LastExDay) / (double)Math.Max(1, span), 0.0, 1.0);
            return cents / 100.0 * share;
        }

        /// <summary>
        /// What the market thinks a share is worth today, as a log: the worth with its outlook and the dividend built up, or
        /// under a bid the bid less the chance it falls through.
        /// </summary>
        public static double LogFairOf(Stock s, in MarketEnv env, int day, int year)
        {
            return LogFairAt(s, MarketValuation.Fair(s, env), day, year);
        }

        /// <summary>The same from a worth a share already worked out.</summary>
        public static double LogFairAt(Stock s, double worth, int day, int year)
        {
            double fair = worth + Accrued(s, day, year);
            if (s.BidCents > 0)
            {
                double bid = s.BidCents / 100.0;
                fair = bid - MarketCatalogue.TakeoverFailChance * Math.Max(0.0, bid - fair);
            }
            return Math.Log(Math.Max(0.01, fair));
        }

        /// <summary>
        /// Every listed company's, the index's and each sector's close the day's move is measured from, out of the shared
        /// histories so every machine agrees: the last close at the open, or by whether a session runs when read from a save.
        /// Moved only at the open, so the session just ended shows until the next. Once an open or a load; no allocation.
        /// </summary>
        public static void DayBases(MarketState state, bool session)
        {
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                if (s.Listed)
                {
                    s.PrevClose = Math.Max(1, MarketHistory.DayBase(s.Daily, session, s.PrevClose));
                }
            }
            state.IndexPrevClose = MarketHistory.DayBase(state.IndexDaily, session, state.IndexPrevClose);
            for (int sec = 0; sec < (int)MarketSector.Count; sec++)
            {
                state.SectorPrevClose[sec] = MarketHistory.DayBase(state.SectorDaily[sec], session, state.SectorPrevClose[sec]);
            }
        }

        /// <summary>
        /// A split takes effect: prices and history divided, the player's holdings and orders multiplied. Worth and cost are
        /// unchanged; an order's price is rounded so it is no likelier to fill.
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
            if (s.Own != null)
            {
                // A player's company: the founder's shares and deals, its reserve and its prices a share too.
                OwnListing own = s.Own;
                own.Founder *= ratio;
                own.Rps /= ratio;
                own.ProposedCents = (int)Math.Round(own.ProposedCents / (double)ratio);
                own.ListedCents = Div(own.ListedCents, ratio);
                own.YearCents = Div(own.YearCents, ratio);
                for (int i = 0; i < own.Deals.Count; i++)
                {
                    OwnDeal d = own.Deals[i];
                    d.Shares *= ratio;
                    d.Cents = Div(d.Cents, ratio);
                    own.Deals[i] = d;
                }
            }
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
            MarketBots.Split(state, s, ratio);   // the player's bots' shares and stops
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

        /// <summary>
        /// A history for a company just come to a new market, ending at today's price: days walked back with its moves (closes
        /// brought over used as they are), and months before those. Made once, when the market opens for a save.
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
