using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A player's company on the city's market: how it is made a listed company, its business day by day from the game's
    /// own statements, its quarters and dividends, its reserve, and what its founder's dealings cost. Game-free
    /// (EconomyCheck compiles it). Called from MarketEngine on the market's own steps: nothing here runs per frame.
    ///
    /// THE PRICE is worked out as every company's is (MarketValuation: what its profits to come are worth, with the
    /// market's mood and its news), but its business is not made up: it is the company's real takings and profit, a
    /// statement day at a time, followed over about a quarter (what it makes now) and about a year (what it usually
    /// makes). Its turnover is the quarter's rate; its usual margin the year's; the gap between the two is a good or bad
    /// patch the market expects to pass within a year. So one day's spike moves the price by a few hundredths of what a
    /// whole quarter at that rate would, and a business that really grows lifts it for good. The council's policies are
    /// priced the moment they change (as for every company) and fade out of the reckoning as the year's figures come to
    /// show them. Its growth is read from its books at each results (the year's takings against the year before, half
    /// believed), so a quarter that only the founder knows about moves nothing until it is out.
    ///
    /// THE RESERVE (see OwnListing): each day's profit after tax and pay belongs to every holder by their shares. The other
    /// holders' part is taken from the founder's cash into the reserve the moment the market counts the day, and comes
    /// back to it out of a loss. A share is worth the business's profits to come and the reserve a share; a dividend is
    /// paid out of the reserve and goes out of the price; every deal of the founder's carries the reserve of its shares.
    /// </summary>
    public static class ListingRules
    {
        // ---------------------------------------------------------------- what a company is

        /// <summary>The id a founder's company has on the market: from a hash of the founder, among ids no company of the city's has.</summary>
        public static int IdFor(ulong owner)
        {
            return ListingCatalogue.IdFirst + (int)(MarketRandom.Mix64(owner) % (ulong)ListingCatalogue.IdCount);
        }

        public static bool IsPlayer(int id)
        {
            return id >= ListingCatalogue.IdFirst && id < ListingCatalogue.IdFirst + ListingCatalogue.IdCount;
        }

        /// <summary>A ticker from a name: the first letters of its words, or its first letters, three or four of them in capitals.</summary>
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

        // ---------------------------------------------------------------- the business, from the books

        /// <summary>What a share is worth from the company's books, and the stock the advisers worked it out on (all its shares, old and new).</summary>
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
        /// The books as the advisers read them, as rates a day: the last four complete quarters' takings and profit after
        /// the founder's fair pay (with eight on record, the lower of the last year and the two years' mean), the last
        /// complete quarter's, and the year's growth in takings (against the year before, or the last half against the
        /// one before; a log a year, before inflation). Nothing on record: all nothing.
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

        /// <summary>A day's (or a quarter's) profit after the founder's pay: pay is a share of a profit, nothing of a loss.</summary>
        public static long AfterPay(long profit, int payBp)
        {
            return profit > 0 ? profit - profit * payBp / 10000L : profit;
        }

        /// <summary>The market's view of the business set from rates a day (takings and profit after pay, over a year and a quarter).</summary>
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

        /// <summary>The growth the market believes from a log a year of the books' takings: half the company's own over the city's long run, after inflation.</summary>
        public static double GrowthFrom(double nominal, in MarketEnv env)
        {
            double real = nominal - env.InflationBp / 10000.0;
            return MarketValuation.Clamp(MarketCatalogue.LongRealGrowth + ListingCatalogue.GrowthShrink * (real - MarketCatalogue.LongRealGrowth), -0.25, 0.4);
        }

        /// <summary>What the council's policies and the bank rate do to the company's margin, as the market reckons them for its sector.</summary>
        public static double LeverMargin(Stock s, in MarketEnv env)
        {
            return (MarketInputs.LeversBp(env, s.Sector) + s.Def.RateBp * (env.RateBp - 300) / 100.0) / 10000.0;
        }

        /// <summary>
        /// The business as the market sees it, from what it has followed: its turnover a year at the quarter's rate, its
        /// usual margin the year's (the council's policies since then counted on top), the quarter's against it a patch
        /// that passes, its debt and how near it is to failing.
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
        /// One day of the calendar of a listed player's company: the statement of the day before (if the day's city has
        /// it) into what the market follows, the quarter's figures and the reserve. Returns the money that moves from the
        /// founder's cash into the reserve for the other holders (under nothing: back to the founder, out of a loss).
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
            // What the market follows takes a day far beyond the company's usual as a one-off (a sale of stock, a bad
            // night): counted at most so many times the year's rate. The reserve and the quarter take the day as it was.
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

        /// <summary>The reserve a share moved by so much: the money the other holders' part of that moves into it from the founder's cash (under nothing: out of it back).</summary>
        public static double ToReserve(Stock s, double perShare)
        {
            OwnListing o = s.Own;
            double was = Math.Max(0.0, o.Rps);
            o.Rps += perShare;
            return (Math.Max(0.0, o.Rps) - was) * Math.Max(0L, o.Others(s));
        }

        /// <summary>
        /// After the days of a market morning: the market's view set again from what it followed, and what it expected of
        /// the company beyond its figures passing as they come to show it (its own figures, not made up: nothing is added
        /// to them, the expectation only fades).
        /// </summary>
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

        /// <summary>A quarter has ended on the calendar: as every company's, and what the market expected of the quarter that waits is kept, the next one's set from its rate now.</summary>
        public static void CloseQuarter(Stock s, int quarter, int year, in MarketEnv env)
        {
            OwnListing o = s.Own;
            MarketLife.CloseQuarter(s, quarter, year);
            o.PendingExpect = o.Expect;
            o.Expect = Expected(s, quarter, year, env);
        }

        /// <summary>What the market expects a quarter to make after tax and pay: the rate it has followed over the year, for the quarter's days.</summary>
        public static double Expected(Stock s, int quarter, int year, in MarketEnv env)
        {
            OwnListing o = s.Own;
            int days = MarketCatalogue.QuarterStart(quarter + 1, year) - MarketCatalogue.QuarterStart(quarter, year);
            double rate = o.NiY > 0.0 ? o.NiY * (1.0 - MarketInputs.TaxRate(env)) : o.NiY;
            return rate * days;
        }

        // ---------------------------------------------------------------- results and dividends

        /// <summary>
        /// The results of the quarter that waits, as the books have them (nothing is drawn: they are the company's own):
        /// what it made against what the market expected, its growth read again from the year's takings, and the
        /// dividend the board declares with them (the founder's proposal while they chair it, else half the reserve; never
        /// more than the reserve a share). The price answers at the open, as for every company.
        /// </summary>
        public static MarketNews Results(Stock s, int day, int year, in MarketEnv env)
        {
            OwnListing o = s.Own;
            double reported = s.PendingNi, expected = o.PendingExpect;
            // Growth: the year's takings (this quarter and the three before) against the four before them.
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
                o.ProposedCents = 0;   // (a proposal is for one set of results)
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

        /// <summary>The dividend a share the board declares with results, in cents: within the reserve a share.</summary>
        public static int Declared(Stock s)
        {
            OwnListing o = s.Own;
            int most = Most(o);
            int asked = o.Board ? o.ProposedCents : (int)Math.Floor(Math.Max(0.0, o.Rps) * ListingCatalogue.BoardPayoutBp / 10000.0 * 100.0);
            return Math.Max(0, Math.Min(most, asked));
        }

        /// <summary>The most the company can pay a share now, in cents: its reserve a share (nothing of a deficit).</summary>
        public static int Most(OwnListing o)
        {
            return (int)Math.Floor(Math.Max(0.0, Math.Min(1e7, o.Rps)) * 100.0);
        }

        /// <summary>A dividend goes out of the reserve on its record day: the other holders are paid out of it (the founder's part was theirs already).</summary>
        public static void PaidOut(Stock s, int cents)
        {
            s.Own.Rps -= cents / 100.0;
        }

        // ---------------------------------------------------------------- the morning's news of the founder

        /// <summary>
        /// The market's mood at the open from what the city made of the founder yesterday (ln, to add to the mood): their
        /// Standing's move, what the papers said, and a scandal (which also cuts what the market expects of the company,
        /// for good). The day's mood is held to its limits either way.
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

        // ---------------------------------------------------------------- the founder's dealings

        /// <summary>
        /// True while the founder may not deal: from the last third of a quarter until that quarter's results are out
        /// (they know its figures before the market does).
        /// </summary>
        public static bool Closed(Stock s, int day, int year)
        {
            if (s.PendingQuarter >= 0)
            {
                return true;
            }
            int from = MarketCatalogue.QuarterStart(s.Quarter, year), next = MarketCatalogue.QuarterStart(s.Quarter + 1, year);
            return day >= next - Math.Max(1, (next - from) / ListingCatalogue.ClosedShare);
        }

        /// <summary>The first day the founder may deal again after a closed time (the day after the results that end it), for the app.</summary>
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
        /// What a deal of the founder's moves, before commission: the shares at the price, with the reserve they carry (a
        /// purchase takes theirs out of the reserve to the founder, a sale puts it in for the buyer). Above nothing: what
        /// the founder pays; under nothing: what they receive.
        /// </summary>
        public static long DealMoney(long shares, int cents, double rps, bool buying)
        {
            long value = MarketTrading.Value(Math.Abs(shares), cents, buying);
            long reserve = (long)Math.Floor(Math.Max(0.0, rps) * Math.Abs(shares));
            return buying ? value - reserve : -(value - reserve);
        }

        /// <summary>
        /// A founder's deal against those before it within the time the rules look back (a year / ShortSwingYearShare):
        /// shares bought and sold in that time are matched oldest first, and any gain on a match (sold dearer than bought,
        /// or bought back cheaper than sold) is what the founder pays back to the company. The deal is then kept for the
        /// next. Returns the gain, in money.
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
                o.Deals.Add(new OwnDeal { Day = day, Shares = shares > 0 ? left : -left, Cents = cents });   // (what was not matched, for a deal the other way to come)
            }
            return (long)Math.Floor(gain);
        }

        /// <summary>The market's reading of a move in the company's shares by the founder or the company, ln: so much for each 1% of the company, within its limit.</summary>
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

        /// <summary>The year's room for new shares, against the company now: what was sold this year is counted.</summary>
        public static long IssueRoom(Stock s, int day, int year)
        {
            OwnListing o = s.Own;
            int y = MarketCatalogue.YearOf(day, year);
            int used = o.IssuedYear == y ? o.IssuedBp : 0;
            return Math.Max(0L, s.Shares * Math.Max(0, ListingCatalogue.IssueMostBpYear - used) / 10000L);
        }

        /// <summary>Why the founder may not deal now (None: they may): a sale in the lock-up, a deal in a closed time, more than they hold, or past the most they may hold.</summary>
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
            return OwnWhy.None;
        }
    }
}
