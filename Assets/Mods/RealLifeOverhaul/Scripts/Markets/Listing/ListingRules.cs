using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A player's company on the city's market: its listing, its business day by day from the game's statements, its
    /// quarters, dividends and reserve, and what its founder's dealings cost. Game-free, run on the market's steps. Its
    /// price is worked out as every company's, but from its real books: takings and profit followed over about a quarter
    /// (what it makes now) and a year (what it usually makes), so one day's spike moves the price little and real growth
    /// lifts it for good.
    /// </summary>
    public static class ListingRules
    {
        /// <summary>The founder's company's id on the market: a hash of the founder, among ids no city company has.</summary>
        public static int IdFor(ulong owner)
        {
            return ListingCatalogue.IdFirst + (int)(MarketRandom.Mix64(owner) % (ulong)ListingCatalogue.IdCount);
        }

        public static bool IsPlayer(int id)
        {
            return id >= ListingCatalogue.IdFirst && id < ListingCatalogue.IdFirst + ListingCatalogue.IdCount;
        }

        /// <summary>A ticker of three or four capitals: the first letters of the name's words, then of the name.</summary>
        public static string TickerOf(string name)
        {
            var t = new System.Text.StringBuilder(4);
            string text = name ?? string.Empty;
            bool start = true;
            for (int i = 0; i < text.Length && t.Length < 4; i++)
            {
                char c = text[i];
                if (char.IsLetter(c))
                {
                    if (start)
                    {
                        t.Append(char.ToUpperInvariant(c));
                    }
                    start = false;
                }
                else
                {
                    start = true;
                }
            }
            for (int i = 0; i < text.Length && t.Length < 3; i++)
            {
                char c = text[i];
                if (char.IsLetter(c) && i > 0)
                {
                    t.Append(char.ToUpperInvariant(c));
                }
            }
            while (t.Length < 3)
            {
                t.Append('X');
            }
            return t.ToString();
        }

        /// <summary>The stock the advisers price from the company's books, with all its shares, old and new.</summary>
        public static Stock Provisional(ListingFile f, ulong owner, int day, int year, in MarketEnv env, long debt)
        {
            int id = f.StockId > 0 ? f.StockId : IdFor(owner);
            Books(f, day, year, out double revY, out double niY, out double revQ, out double niQ, out double growth);
            int debtBp = revY > 0.0 ? (int)Math.Max(0.0, Math.Min(30000.0, debt / (revY * Math.Max(8, year)) * 10000.0)) : 0;
            string trade = f.Trade >= 0 && f.Trade < MarketInputs.Trades.Length ? MarketInputs.Trades[f.Trade] : null;
            CompanyDef d = MarketCatalogue.PlayerDef(id, f.Name, f.Ticker, (MarketSector)MarketValuation.Clamp(f.Sector, 0, (int)MarketSector.Count - 1), debtBp, trade);
            var s = new Stock
            {
                Id = id, Def = d, Name = d.Name, Ticker = d.Ticker, Sector = d.Sector, Size = d.Size, Status = StockStatus.Listed, ListedDay = day,
                Shares = Math.Max(1L, f.OldShares + f.NewShares)
            };
            s.Own = new OwnListing { Owner = owner, Test = f.Test, Founder = f.OldShares, PayBp = ListingCatalogue.PayRefBp, DebtBp = debtBp };
            Seed(s, env, year, revY, niY, revQ, niQ, debt, growth);
            return s;
        }

        /// <summary>
        /// The books as rates a day: the last four complete quarters' takings and profit after fair pay (with eight on
        /// record, the lower of the last year and the two years' mean), the last quarter's, and the year's growth in takings
        /// as a log a year before inflation. All nothing with nothing on record.
        /// </summary>
        public static void Books(ListingFile f, int day, int year, out double revY, out double niY, out double revQ, out double niQ, out double growth)
        {
            revY = niY = revQ = niQ = growth = 0.0;
            int now = MarketCatalogue.QuarterOf(day, year);
            long[] rev = new long[8], profit = new long[8];
            int[] days = new int[8];
            int found = 0;
            for (int back = 1; back <= 8; back++)
            {
                int at = f.IndexOf(now - back);
                if (at < 0 || f.Books[at].Days <= 0)
                {
                    break;
                }
                rev[back - 1] = f.Books[at].Revenue;
                profit[back - 1] = AfterPay(f.Books[at].Profit, ListingCatalogue.PayRefBp);
                days[back - 1] = f.Books[at].Days;
                found = back;
            }
            if (found == 0)
            {
                return;
            }
            revQ = rev[0] / (double)days[0];
            niQ = profit[0] / (double)days[0];
            int n = Math.Min(4, found);
            long r4 = 0, p4 = 0;
            int d4 = 0;
            for (int i = 0; i < n; i++)
            {
                r4 += rev[i];
                p4 += profit[i];
                d4 += days[i];
            }
            revY = r4 / (double)d4;
            niY = p4 / (double)d4;
            if (found >= 8)
            {
                long r8 = 0, p8 = 0;
                int d8 = 0;
                for (int i = 4; i < 8; i++)
                {
                    r8 += rev[i];
                    p8 += profit[i];
                    d8 += days[i];
                }
                niY = Math.Min(niY, (p4 + p8) / (double)(d4 + d8));   // a year better than the two years' mean is not taken at its word
                double before = r8 / (double)d8;
                growth = before > 0.0 && revY > 0.0 ? Math.Log(revY / before) : 0.0;
            }
            else if (found >= 4)
            {
                double recent = (rev[0] + rev[1]) / (double)(days[0] + days[1]), earlier = (rev[2] + rev[3]) / (double)(days[2] + days[3]);
                growth = earlier > 0.0 && recent > 0.0 ? Math.Log(recent / earlier) * 2.0 : 0.0;
            }
        }

        /// <summary>A profit after the founder's pay, which is a share of a profit and nothing of a loss.</summary>
        public static long AfterPay(long profit, int payBp)
        {
            return profit > 0 ? profit - profit * payBp / 10000L : profit;
        }

        /// <summary>The market's view of the business set from rates a day over a year and a quarter.</summary>
        public static void Seed(Stock s, in MarketEnv env, int year, double revY, double niY, double revQ, double niQ, double debt, double growth)
        {
            OwnListing o = s.Own;
            o.RevY = Math.Max(0.0, revY);
            o.NiY = niY;
            o.RevQ = revQ > 0.0 ? revQ : o.RevY;
            o.NiQ = revQ > 0.0 ? niQ : niY;
            o.LeverY = LeverMargin(s, env);
            s.Debt = Math.Max(0.0, debt);
            s.DebtRate = MarketValuation.UsualDebtRate(s, env);
            s.GrowthTarget = MarketCatalogue.LongRealGrowth;
            s.Growth = GrowthFrom(growth, env);
            Figures(s, env, year);
            s.Equity = Math.Max(s.Rev * 0.1, MarketValuation.Profit(s, s.Margin, MarketInputs.TaxRate(env)) / MarketCatalogue.RoeLong);
        }

        /// <summary>The growth the market believes from a log a year of the books' takings: half its lead over the city's long run.</summary>
        public static double GrowthFrom(double nominal, in MarketEnv env)
        {
            double real = nominal - env.InflationBp / 10000.0;
            return MarketValuation.Clamp(MarketCatalogue.LongRealGrowth + ListingCatalogue.GrowthShrink * (real - MarketCatalogue.LongRealGrowth), -0.25, 0.4);
        }

        /// <summary>What the council's policies and the bank rate do to the company's margin, for its sector.</summary>
        public static double LeverMargin(Stock s, in MarketEnv env)
        {
            return (MarketInputs.LeversBp(env, s.Sector) + s.Def.RateBp * (env.RateBp - 300) / 100.0) / 10000.0;
        }

        /// <summary>
        /// The business as the market sees it: turnover at the quarter's rate, the usual margin the year's with the council's
        /// policies on top, and the quarter's margin against it as a patch that passes.
        /// </summary>
        public static void Figures(Stock s, in MarketEnv env, int year)
        {
            OwnListing o = s.Own;
            double y = Math.Max(8, year);
            s.Rev = Math.Max(1.0, o.RevQ * y);
            double marginY = o.RevY > 1e-9 ? MarketValuation.Clamp(o.NiY / o.RevY, -0.5, 0.8) : 0.0;
            double marginQ = o.RevQ > 1e-9 ? MarketValuation.Clamp(o.NiQ / o.RevQ, -0.5, 0.8) : marginY;
            s.MarginShift = marginY - s.Def.MarginBp / 10000.0 - o.LeverY;
            s.MarginTarget = MarketValuation.TargetMargin(s, env);
            s.Margin = MarketValuation.Clamp(s.MarginTarget + (marginQ - marginY), -0.5, 0.8);
            s.Distress = MarketValuation.DistressOf(s, env);
        }

        /// <summary>
        /// One day of a listed player's company: yesterday's statement into what the market follows, the quarter and the
        /// reserve. Returns the money moved from the founder's cash into the reserve (negative: back out of a loss).
        /// </summary>
        public static double Day(Stock s, in MarketEnv env, int year, int day)
        {
            OwnListing o = s.Own;
            int statement = day - 1;
            if (env.OwnDays <= 0 || statement < env.OwnFrom || statement >= env.OwnFrom + env.OwnDays || s.Shares <= 0)
            {
                return 0.0;
            }
            double y = Math.Max(8, year);
            double rev = Math.Max(0.0, env.OwnRev / (double)env.OwnDays);
            double before = env.OwnProfit / (double)env.OwnDays;
            double profit = before > 0.0 ? before * (1.0 - o.PayBp / 10000.0) : before;
            double ni = profit > 0.0 ? profit * (1.0 - MarketInputs.TaxRate(env)) : profit;
            double quarter = Math.Min(1.0, 4.0 / y), whole = 1.0 / y;
            // A day far beyond the usual (a stock sale, a bad night) is followed as a one-off, at most so many times the
            // year's rate; the reserve and the quarter take the day as it was.
            double seenRev = o.RevY > 0.0 ? Math.Min(rev, ListingCatalogue.DaySeenMost * o.RevY) : rev;
            double most = ListingCatalogue.DaySeenMost * Math.Max(Math.Abs(o.NiY), 0.02 * o.RevY);
            double seen = o.RevY > 0.0 ? MarketValuation.Clamp(profit, -most, most) : profit;
            o.RevQ += quarter * (seenRev - o.RevQ);
            o.NiQ += quarter * (seen - o.NiQ);
            o.RevY += whole * (seenRev - o.RevY);
            o.NiY += whole * (seen - o.NiY);
            o.LeverY += whole * (LeverMargin(s, env) - o.LeverY);
            int q = MarketCatalogue.QuarterOf(statement, year);
            if (q == s.Quarter)
            {
                s.QRev += rev;
                s.QNi += ni;
            }
            else if (q == s.PendingQuarter)
            {
                s.PendingRev += rev;
                s.PendingNi += ni;
            }
            s.Equity += ni;
            return ToReserve(s, ni / s.Shares);
        }

        /// <summary>The reserve a share moved by so much; returns the other holders' part, moved from the founder's cash.</summary>
        public static double ToReserve(Stock s, double perShare)
        {
            OwnListing o = s.Own;
            double was = Math.Max(0.0, o.Rps);
            o.Rps += perShare;
            return (Math.Max(0.0, o.Rps) - was) * Math.Max(0L, o.Others(s));
        }

        /// <summary>After a market morning's days: the view set again, and what was expected beyond the figures fading.</summary>
        public static void Morning(Stock s, in MarketEnv env, int year, int days)
        {
            Figures(s, env, year);
            s.DebtRate += (MarketValuation.UsualDebtRate(s, env) - s.DebtRate) / (MarketCatalogue.DebtRepriceYears * Math.Max(8, year)) * Math.Max(1, days);
            if (env.OwnDays > 0)
            {
                s.Debt = Math.Max(0.0, env.OwnDebt);
            }
            if (Math.Abs(s.Outlook) > 1e-9)
            {
                s.Outlook *= Math.Exp(-Math.Max(1, days) / (MarketCatalogue.OutlookYears * Math.Max(8, year)));
            }
            s.Distress = MarketValuation.DistressOf(s, env);
        }

        /// <summary>A quarter ended: what was expected of the waiting quarter kept, the next one's set from the rate now.</summary>
        public static void CloseQuarter(Stock s, int quarter, int year, in MarketEnv env)
        {
            OwnListing o = s.Own;
            MarketLife.CloseQuarter(s, quarter, year);
            o.PendingExpect = o.Expect;
            o.Expect = Expected(s, quarter, year, env);
        }

        /// <summary>What the market expects a quarter to make after tax and pay, at the year's followed rate.</summary>
        public static double Expected(Stock s, int quarter, int year, in MarketEnv env)
        {
            OwnListing o = s.Own;
            int days = MarketCatalogue.QuarterStart(quarter + 1, year) - MarketCatalogue.QuarterStart(quarter, year);
            double rate = o.NiY > 0.0 ? o.NiY * (1.0 - MarketInputs.TaxRate(env)) : o.NiY;
            return rate * days;
        }

        /// <summary>
        /// The waiting quarter's results from the books, nothing drawn: profit against expectation, growth read again, and
        /// the dividend declared (the founder's proposal while they chair the board, else a share of the reserve).
        /// </summary>
        public static MarketNews Results(Stock s, int day, int year, in MarketEnv env)
        {
            OwnListing o = s.Own;
            double reported = s.PendingNi, expected = o.PendingExpect;
            // Growth: the year's takings, this quarter and the three before, against the four before them.
            long recent = (long)(s.PendingRev / 1000.0), earlier = 0;
            int recentCount = 1, earlierCount = 0;
            for (int i = s.Results.Count - 1, n = 1; i >= 0 && n < 8; i--, n++)
            {
                if (n < 4)
                {
                    recent += s.Results[i].RevenueK;
                    recentCount++;
                }
                else
                {
                    earlier += s.Results[i].RevenueK;
                    earlierCount++;
                }
            }
            if (recentCount == 4 && earlierCount == 4 && recent > 0 && earlier > 0)
            {
                s.Growth = GrowthFrom(Math.Log(recent / (double)earlier), env);
            }
            int eps = MarketLife.PerShare(reported, s.Shares), consensus = MarketLife.PerShare(expected, s.Shares);
            int declared = Declared(s);
            s.DpsCents = declared;
            s.DeclaredCents = declared;
            s.RecordDay = declared > 0 ? MarketTrading.MarketDays(day, MarketCatalogue.DividendRecordDays) : 0;
            s.PayDay = declared > 0 ? MarketTrading.MarketDays(s.RecordDay, MarketCatalogue.DividendPayDays) : 0;
            s.ExDone = declared <= 0;
            o.YearDividendCents += declared;
            if (o.Board)
            {
                o.ProposedCents = 0;   // a proposal is for one set of results
            }
            s.Results.Add(new QuarterResult
            {
                Quarter = s.PendingQuarter, Day = day, RevenueK = (long)(s.PendingRev / 1000.0), ProfitK = (long)(reported / 1000.0), Eps = eps, Consensus = consensus, DpsCents = declared
            });
            while (s.Results.Count > MarketCatalogue.QuartersKept)
            {
                s.Results.RemoveAt(0);
            }
            if ((s.PendingQuarter & 3) == 3)
            {
                o.AgmDay = MarketTrading.MarketDays(day, ListingCatalogue.AgmNoticeDays);   // the year's last results: the yearly meeting follows
            }
            s.PendingQuarter = -1;
            s.ResultsDay = 0;
            s.ResultsBoostDay = day;
            return new MarketNews { Day = day, Kind = MarketNewsKind.Results, Stock = s.Id, A = eps, B = consensus };
        }

        /// <summary>The dividend a share the board declares with results, in cents, within the reserve a share.</summary>
        public static int Declared(Stock s)
        {
            OwnListing o = s.Own;
            int most = Most(o);
            int asked = o.Board ? o.ProposedCents : (int)Math.Floor(Math.Max(0.0, o.Rps) * ListingCatalogue.BoardPayoutBp / 10000.0 * 100.0);
            return Math.Max(0, Math.Min(most, asked));
        }

        /// <summary>The most the company can pay a share now, in cents: its reserve a share, nothing of a deficit.</summary>
        public static int Most(OwnListing o)
        {
            return (int)Math.Floor(Math.Max(0.0, Math.Min(1e7, o.Rps)) * 100.0);
        }

        /// <summary>A dividend out of the reserve on its record day; the founder's part was theirs already.</summary>
        public static void PaidOut(Stock s, int cents)
        {
            s.Own.Rps -= cents / 100.0;
        }

        /// <summary>
        /// The mood at the open from what the city made of the founder yesterday (ln): their Standing's move, the papers,
        /// and a scandal, which also cuts the company's outlook for good.
        /// </summary>
        public static double OpenNews(Stock s, in MarketEnv env)
        {
            OwnListing o = s.Own;
            int bp = 0;
            if (env.OwnStanding >= 0)
            {
                if (o.LastStanding >= 0)
                {
                    bp += Capped((env.OwnStanding - o.LastStanding) * ListingCatalogue.StandingKickBp, ListingCatalogue.StandingKickMostBp);
                }
                o.LastStanding = env.OwnStanding;
            }
            bp += Capped(env.OwnPress * ListingCatalogue.PressKickBp, ListingCatalogue.PressKickMostBp);
            if (env.OwnScandal > 0)
            {
                bp += ListingCatalogue.ScandalKickBp;
                s.Outlook += ListingCatalogue.ScandalOutlook;
                o.YearScandals++;
            }
            return bp / 10000.0;
        }

        public static int Capped(int bp, int most)
        {
            return bp > most ? most : bp < -most ? -most : bp;
        }

        /// <summary>True from the last third of a quarter until its results are out: the founder knows the figures first.</summary>
        public static bool Closed(Stock s, int day, int year)
        {
            if (s.PendingQuarter >= 0)
            {
                return true;
            }
            int from = MarketCatalogue.QuarterStart(s.Quarter, year), next = MarketCatalogue.QuarterStart(s.Quarter + 1, year);
            return day >= next - Math.Max(1, (next - from) / ListingCatalogue.ClosedShare);
        }

        /// <summary>The first day the founder may deal again after a closed time.</summary>
        public static int OpensAgain(Stock s, int day, int year)
        {
            if (!Closed(s, day, year))
            {
                return day;
            }
            int results = s.ResultsDay > 0 ? s.ResultsDay : MarketCatalogue.QuarterStart(s.Quarter + 1, year) + Math.Max(1, year * MarketCatalogue.ResultsLagMostPct / 100);
            return MarketTrading.NextTradingDay(results - 1);
        }

        /// <summary>
        /// A founder's deal before commission, the reserve its shares carry included: positive is what the founder pays,
        /// negative what they receive.
        /// </summary>
        public static long DealMoney(long shares, int cents, double rps, bool buying)
        {
            long value = MarketTrading.Value(Math.Abs(shares), cents, buying);
            long reserve = (long)Math.Floor(Math.Max(0.0, rps) * Math.Abs(shares));
            return buying ? value - reserve : -(value - reserve);
        }

        /// <summary>
        /// A founder's deal matched oldest first against the opposite deals within a year / ShortSwingYearShare; returns
        /// the gain on the matches, which the founder pays back to the company. What is left is kept for the next deal.
        /// </summary>
        public static long ShortSwing(OwnListing o, int day, long shares, int cents, int year)
        {
            int window = Math.Max(1, Math.Max(8, year) / ListingCatalogue.ShortSwingYearShare);
            for (int i = o.Deals.Count - 1; i >= 0; i--)
            {
                if (day - o.Deals[i].Day > window || o.Deals[i].Shares == 0)
                {
                    o.Deals.RemoveAt(i);
                }
            }
            long left = Math.Abs(shares);
            double gain = 0.0;
            for (int i = 0; i < o.Deals.Count && left > 0; i++)
            {
                OwnDeal d = o.Deals[i];
                if (Math.Sign(d.Shares) == Math.Sign(shares))
                {
                    continue;
                }
                long matched = Math.Min(left, Math.Abs(d.Shares));
                double each = shares < 0 ? cents - d.Cents : d.Cents - cents;   // selling now what was bought; buying now what was sold
                gain += Math.Max(0.0, each * matched / 100.0);
                d.Shares += d.Shares > 0 ? -matched : matched;
                o.Deals[i] = d;
                left -= matched;
            }
            for (int i = o.Deals.Count - 1; i >= 0; i--)
            {
                if (o.Deals[i].Shares == 0)
                {
                    o.Deals.RemoveAt(i);
                }
            }
            if (left > 0)
            {
                if (o.Deals.Count >= 16)
                {
                    o.Deals.RemoveAt(0);
                }
                o.Deals.Add(new OwnDeal { Day = day, Shares = shares > 0 ? left : -left, Cents = cents });
            }
            return (long)Math.Floor(gain);
        }

        /// <summary>The market's reading (ln) of a move in the company's shares: so much for each 1%, within its limit.</summary>
        public static double Reading(long shares, long issued, int perPercentBp)
        {
            if (issued <= 0)
            {
                return 0.0;
            }
            double percent = shares * 100.0 / issued;
            int bp = (int)Math.Round(percent * perPercentBp);
            return Capped(bp, ListingCatalogue.KickMostBp) / 10000.0;
        }

        /// <summary>The year's room for new shares, against the company now.</summary>
        public static long IssueRoom(Stock s, int day, int year)
        {
            OwnListing o = s.Own;
            int y = MarketCatalogue.YearOf(day, year);
            int used = o.IssuedYear == y ? o.IssuedBp : 0;
            return Math.Max(0L, s.Shares * Math.Max(0, ListingCatalogue.IssueMostBpYear - used) / 10000L);
        }

        /// <summary>Why the founder may not deal now; None if they may.</summary>
        public static OwnWhy DealBlocked(Stock s, int day, int year, long shares)
        {
            OwnListing o = s.Own;
            if (shares == 0)
            {
                return OwnWhy.TooLarge;
            }
            if (Closed(s, day, year))
            {
                return OwnWhy.Closed;
            }
            if (shares < 0 && day < o.LockDay)
            {
                return OwnWhy.LockedUp;
            }
            if (shares < 0 && -shares > o.Founder)
            {
                return OwnWhy.NoShares;
            }
            if (shares > 0 && (o.Founder + shares) * 10000L > s.Shares * (long)ListingCatalogue.FounderMostBp)
            {
                return OwnWhy.TooMuch;
            }
            if (Math.Abs(shares) > MarketTrading.MostPerOrder(s))
            {
                return OwnWhy.TooLarge;
            }
            if (YearUsedBp(o, day, year, shares > 0 ? o.BoughtBp : o.SoldBp) + Bp(Math.Abs(shares), s.Shares) > ListingCatalogue.FounderDealMostBpYear)
            {
                return OwnWhy.YearLimit;
            }
            return OwnWhy.None;
        }

        /// <summary>Shares as bp of so many, rounded up so a rounding never passes the year's limits.</summary>
        public static int Bp(long shares, long of)
        {
            return of > 0 ? (int)Math.Min(100000L, (shares * 10000L + of - 1) / of) : 100000;
        }

        /// <summary>What has been used of a year's limit this game year.</summary>
        public static int YearUsedBp(OwnListing o, int day, int year, int used)
        {
            return o.DealYear == MarketCatalogue.YearOf(day, Math.Max(8, year)) ? used : 0;
        }

        /// <summary>A deal, a buy-back, counted against the year's limits.</summary>
        public static void Count(OwnListing o, int day, int year, int bought, int sold, int back)
        {
            int y = MarketCatalogue.YearOf(day, Math.Max(8, year));
            if (o.DealYear != y)
            {
                o.DealYear = y;
                o.BoughtBp = o.SoldBp = o.BackBp = 0;
            }
            o.BoughtBp += bought;
            o.SoldBp += sold;
            o.BackBp += back;
        }

        /// <summary>
        /// What a share is paid when the founder takes the company private: the price with the premium, never under the
        /// last year's highest close nor, in its first year, its listing price, so running it down first does not pay.
        /// </summary>
        public static int PrivateCents(Stock s, int day, int year)
        {
            int cents = (int)Math.Round(s.Last * (1.0 + ListingCatalogue.PrivatePremiumBp / 10000.0));
            int since = day - Math.Max(8, year);
            for (int i = s.Daily.Count - 1; i >= 0 && s.Daily[i].Day >= since; i--)
            {
                cents = Math.Max(cents, s.Daily[i].C);
            }
            for (int i = s.Monthly.Count - 1; i >= 0 && s.Monthly[i].Day >= since; i--)
            {
                cents = Math.Max(cents, s.Monthly[i].C);
            }
            if (s.ListedDay >= since && s.Own != null)
            {
                cents = Math.Max(cents, s.Own.ListedCents);
            }
            return Math.Max(1, cents);
        }
    }
}
