using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What the game side reads of the player and their businesses for the requirements and the audit.</summary>
    public struct ListingFacts
    {
        public int FirstDay;             // the player's first business still running; 0 none
        public bool BackTaxes;           // the game's tax: a bill unpaid past its day, or back taxes owed
        public int LateReturns;          // filed late in the last year
        public bool CashOffBooks;        // at a business now
        public bool Bankrupt;            // on the credit file, or a debt arrangement
        public int BribesOut;            // came out in the last year
        public long Debt;                // what the player owes on loans
        public int Standing;             // 0 to 100; -1 not known
    }

    /// <summary>One requirement for coming to the market, as the page lists it.</summary>
    public struct Requirement
    {
        public int Which;                // ListingBook.Need...
        public long Needed, Has;
        public bool Met;
    }

    /// <summary>
    /// The way to the market, by rule. Game-free. What it takes, what the advisers, auditors, underwriters and exchange
    /// charge, the company's books from the game's statements, the prospectus's range and the book that sets the price.
    /// A share is priced at what the books say it is worth less the advisers' discount; the book's own keenness is drawn
    /// from the pricing day's secret, so a load does not draw it again.
    /// </summary>
    public static class ListingBook
    {
        // What the auditors found (ListingFile.Findings): the first four stop a listing, the last is disclosed.
        public const int BackTaxes = 1, LateReturns = 2, CashOffBooks = 4, Bankrupt = 8, BribesOut = 16;
        public const int Qualifying = BackTaxes | LateReturns | CashOffBooks | Bankrupt;

        // The requirements, in the order the page lists them.
        public const int NeedYears = 0, NeedQuarters = 1, NeedTurnover = 2, NeedProfit = 3, NeedProfitable = 4, NeedAudit = 5, NeedCount = 6;

        /// <summary>One statement day into its quarter of the books; the last twelve quarters are kept.</summary>
        public static void AddDay(ListingFile f, int statementDay, long revenue, long profit, int year)
        {
            int quarter = MarketCatalogue.QuarterOf(statementDay, year);
            int at = f.IndexOf(quarter);
            if (at < 0)
            {
                int place = f.Books.Count;
                while (place > 0 && f.Books[place - 1].Quarter > quarter)
                {
                    place--;
                }
                f.Books.Insert(place, new BookQuarter { Quarter = quarter });
                at = place;
                while (f.Books.Count > 12)
                {
                    f.Books.RemoveAt(0);
                    at--;
                }
                if (at < 0)
                {
                    return;
                }
            }
            BookQuarter q = f.Books[at];
            q.Revenue += Math.Max(0L, revenue);
            q.Profit += profit;
            q.Days++;
            f.Books[at] = q;
            f.BooksDay = Math.Max(f.BooksDay, statementDay);
        }

        /// <summary>The last complete quarters before the day's, up to so many: their takings, profit before tax and how many made a profit.</summary>
        public static int Last(ListingFile f, int day, int year, int count, out long revenue, out long profit, out int profitable)
        {
            revenue = profit = 0;
            profitable = 0;
            int now = MarketCatalogue.QuarterOf(day, year), found = 0;
            for (int back = 1; back <= count; back++)
            {
                int at = f.IndexOf(now - back);
                if (at < 0 || f.Books[at].Days <= 0)
                {
                    break;
                }
                revenue += f.Books[at].Revenue;
                profit += f.Books[at].Profit;
                profitable += f.Books[at].Profit > 0 ? 1 : 0;
                found++;
            }
            return found;
        }

        /// <summary>Every requirement, met or not, as of a day, money at today's prices; true when all are met.</summary>
        public static bool Requirements(ListingFile f, in ListingFacts facts, int day, int year, double level, Requirement[] into)
        {
            year = Math.Max(8, year);
            int quarters = Last(f, day, year, ListingCatalogue.QuartersLeast, out long revenue, out long profit, out int profitable);
            long age = facts.FirstDay > 0 ? day - facts.FirstDay : 0;
            Set(into, NeedYears, (long)ListingCatalogue.BooksYears * year, age);
            Set(into, NeedQuarters, ListingCatalogue.QuartersLeast, quarters);
            Set(into, NeedTurnover, At(ListingCatalogue.TurnoverLeastAtStart, level), quarters >= ListingCatalogue.QuartersLeast ? revenue : 0);
            Set(into, NeedProfit, At(ListingCatalogue.ProfitLeastAtStart, level), quarters >= ListingCatalogue.QuartersLeast ? profit : 0);
            Set(into, NeedProfitable, ListingCatalogue.ProfitableQuartersLeast, profitable);
            into[NeedAudit] = new Requirement { Which = NeedAudit, Needed = 1, Has = AuditGood(f, day, year) ? 1 : 0, Met = AuditGood(f, day, year) };
            bool all = true;
            for (int i = 0; i < NeedCount; i++)
            {
                all &= into[i].Met;
            }
            return all;
        }

        /// <summary>All requirements but the audit: what the advisers ask before they take the company on.</summary>
        public static bool ReadyForAdvisers(Requirement[] r)
        {
            for (int i = 0; i < NeedCount; i++)
            {
                if (i != NeedAudit && !r[i].Met)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Set(Requirement[] into, int which, long needed, long has)
        {
            into[which] = new Requirement { Which = which, Needed = needed, Has = has, Met = has >= needed };
        }

        /// <summary>A clean audit signed within the time it is good for.</summary>
        public static bool AuditGood(ListingFile f, int day, int year)
        {
            return f.AuditDay > 0 && (f.Findings & Qualifying) == 0 && day - f.AuditDay <= Math.Max(1, Math.Max(8, year) * ListingCatalogue.AuditValidQuarters / 4);
        }

        /// <summary>What the auditors find in the facts, as ListingBook bits.</summary>
        public static int Findings(in ListingFacts facts)
        {
            return (facts.BackTaxes ? BackTaxes : 0) | (facts.LateReturns > 0 ? LateReturns : 0) | (facts.CashOffBooks ? CashOffBooks : 0) | (facts.Bankrupt ? Bankrupt : 0)
                   | (facts.BribesOut > 0 ? BribesOut : 0);
        }

        public static long At(long atStart, double level)
        {
            return (long)Math.Round(atStart * Math.Max(0.1, level));
        }

        private static long Within(long value, long least, long most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>The advisers' retainer: a share of what the company looks worth, within its bounds.</summary>
        public static long Retainer(double worth, double level)
        {
            return Within((long)Math.Round(Math.Max(0.0, worth) * ListingCatalogue.RetainerBp / 10000.0), At(ListingCatalogue.RetainerLeastAtStart, level),
                At(ListingCatalogue.RetainerMostAtStart, level));
        }

        /// <summary>The auditors: a share of a year's takings, within its bounds.</summary>
        public static long AuditFee(long turnover, double level)
        {
            return Within(Math.Max(0L, turnover) * ListingCatalogue.AuditBp / 10000L, At(ListingCatalogue.AuditLeastAtStart, level), At(ListingCatalogue.AuditMostAtStart, level));
        }

        /// <summary>The underwriters: a share of the money raised (less for a large offer).</summary>
        public static long Underwriting(long raised, double level)
        {
            int bp = raised >= At(ListingCatalogue.UnderwritingLargeAtStart, level) ? ListingCatalogue.UnderwritingLargeBp : ListingCatalogue.UnderwritingBp;
            return Math.Max(0L, raised) * bp / 10000L;
        }

        /// <summary>The exchange on the first day: a share of the company's worth at its price, at least its least.</summary>
        public static long ListingFee(double worth, double level)
        {
            return Math.Max(At(ListingCatalogue.ListingFeeLeastAtStart, level), (long)Math.Round(Math.Max(0.0, worth) * ListingCatalogue.ListingFeeBp / 10000.0));
        }

        /// <summary>The exchange each year after, at least its least.</summary>
        public static long YearlyFee(double worth, double level)
        {
            return Math.Max(At(ListingCatalogue.YearlyFeeLeastAtStart, level), (long)Math.Round(Math.Max(0.0, worth) * ListingCatalogue.YearlyFeeBp / 10000.0));
        }

        /// <summary>The founder's shares: as many as make one worth about a set price at today's prices.</summary>
        public static long FounderShares(double worth, double level)
        {
            double each = ListingCatalogue.SharePriceAtStart / 100.0 * Math.Max(0.1, level);
            return Math.Max(ListingCatalogue.FounderSharesLeast, (long)Math.Round(Math.Max(0.0, worth) / each / 1000.0) * 1000L);
        }

        /// <summary>The new shares that make so much of the company: the founder keeps the rest.</summary>
        public static long NewShares(long founder, int floatBp)
        {
            int f = Math.Max(1, Math.Min(9000, floatBp));
            return (long)Math.Round(founder * (double)f / (10000.0 - f));
        }

        /// <summary>The nearest float the page offers to a share asked for.</summary>
        public static int FloatStep(int bp)
        {
            int best = ListingCatalogue.FloatSteps[0];
            for (int i = 0; i < ListingCatalogue.FloatSteps.Length; i++)
            {
                if (Math.Abs(ListingCatalogue.FloatSteps[i] - bp) < Math.Abs(best - bp))
                {
                    best = ListingCatalogue.FloatSteps[i];
                }
            }
            return best;
        }

        /// <summary>The least price taken: the range's bottom, its middle, or any price at all.</summary>
        public const int WalkLow = 0, WalkMiddle = -2, WalkAny = 1;

        /// <summary>
        /// The prospectus: the shares offered and a range around the advisers' price, from what the books say a share is
        /// worth. The least price taken is in cents, or <see cref="WalkLow"/> or <see cref="WalkMiddle"/>.
        /// </summary>
        public static void Prospectus(ListingFile f, int floatBp, int fairCents, int walkCents)
        {
            f.FloatBp = FloatStep(floatBp);
            f.FairCents = Math.Max(1, fairCents);
            int mid = Math.Max(1, (int)Math.Round(fairCents * (1.0 - ListingCatalogue.IpoDiscount)));
            f.LowCents = Step(mid * (1.0 - ListingCatalogue.RangeSpread), false);
            f.HighCents = Math.Max(f.LowCents + ListingCatalogue.PriceStepCents, Step(mid * (1.0 + ListingCatalogue.RangeSpread), true));
            f.WalkCents = walkCents > 0 ? walkCents : walkCents == WalkMiddle ? Step((f.LowCents + f.HighCents) * 0.5, false) : f.LowCents;
        }

        /// <summary>A price as a whole number of steps, rounded down or up.</summary>
        public static int Step(double cents, bool up)
        {
            double steps = cents / ListingCatalogue.PriceStepCents;
            long whole = up ? (long)Math.Ceiling(steps - 1e-9) : (long)Math.Floor(steps + 1e-9);
            return (int)Math.Max(1L, whole * ListingCatalogue.PriceStepCents);
        }

        /// <summary>
        /// The price the book comes to, in cents: the range's middle moved by how keen it is (its own chance, the index's
        /// last month in bp, the founder's Standing, a matter disclosed, how much is offered), held near the range.
        /// </summary>
        public static int Price(ListingFile f, ulong secret, int stockId, int indexMonthBp, int standing, out double keen)
        {
            double mid = (f.LowCents + f.HighCents) * 0.5;
            double index = MarketValuation.Clamp(indexMonthBp / 10000.0, -ListingCatalogue.DemandIndexMost, ListingCatalogue.DemandIndexMost) * ListingCatalogue.DemandIndexShare;
            double name = standing >= 0 ? (standing - 50) / 50.0 * ListingCatalogue.DemandStanding : 0.0;
            double disclosed = (f.Findings & BribesOut) != 0 ? ListingCatalogue.DemandDisclosed : 0.0;
            double supply = f.FloatBp / 5000.0 * ListingCatalogue.DemandFloat;
            keen = ListingCatalogue.DemandSpread * MarketRandom.Normal(secret, MarketRandom.Book, stockId, 1) + index + name - disclosed - supply;
            double price = mid * Math.Exp(keen);
            double least = f.LowCents * (1.0 - ListingCatalogue.PriceBeyondRange), most = f.HighCents * (1.0 + ListingCatalogue.PriceBeyondRange);
            return Step(MarketValuation.Clamp(price, least, most), false);
        }

        /// <summary>What a share is worth from the books, in cents, with all the shares there will be.</summary>
        public static int FairCents(ListingFile f, ulong owner, int day, int year, in MarketEnv env, long debt)
        {
            Stock s = ListingRules.Provisional(f, owner, day, year, env, debt);
            return Math.Max(1, (int)Math.Round(MarketValuation.Fair(s, env) * 100.0));
        }

        /// <summary>What the company looks worth before an offer: the market's model on the books, a share worth a unit.</summary>
        public static double Worth(ListingFile f, ulong owner, int day, int year, in MarketEnv env, long debt)
        {
            long old = f.OldShares, fresh = f.NewShares;
            f.OldShares = 1000000;
            f.NewShares = 0;
            Stock s = ListingRules.Provisional(f, owner, day, year, env, debt);
            double worth = MarketValuation.Fair(s, env) * s.Shares;
            f.OldShares = old;
            f.NewShares = fresh;
            return worth;
        }

        /// <summary>The company as a listed stock on its first day, at its price, its last four quarters as past results.</summary>
        public static Stock Make(ListingFile f, ulong owner, int day, int year, in MarketEnv env, long debt, int indexNow)
        {
            Stock s = ListingRules.Provisional(f, owner, day, year, env, debt);
            OwnListing o = s.Own;
            int cents = Math.Max(1, f.PriceCents);
            s.Quarter = MarketCatalogue.QuarterOf(day, year);
            int at = f.IndexOf(s.Quarter);
            double tax = MarketInputs.TaxRate(env);
            if (at >= 0)
            {
                long after = ListingRules.AfterPay(f.Books[at].Profit, o.PayBp);
                s.QRev = f.Books[at].Revenue;
                s.QNi = after > 0 ? after * (1.0 - tax) : after;
            }
            o.Expect = ListingRules.Expected(s, s.Quarter, year, env);
            for (int back = 4; back >= 1; back--)
            {
                int q = f.IndexOf(s.Quarter - back);
                if (q < 0 || f.Books[q].Days <= 0)
                {
                    continue;
                }
                long after = ListingRules.AfterPay(f.Books[q].Profit, o.PayBp);
                double ni = after > 0 ? after * (1.0 - tax) : after;
                int eps = MarketLife.PerShare(ni, s.Shares);
                s.Results.Add(new QuarterResult
                {
                    Quarter = f.Books[q].Quarter, Day = MarketCatalogue.QuarterStart(f.Books[q].Quarter + 1, year), RevenueK = f.Books[q].Revenue / 1000L, ProfitK = (long)(ni / 1000.0),
                    Eps = eps, Consensus = eps
                });
            }
            s.LastExDay = day;
            s.LogFair = MarketLife.LogFairOf(s, env, day, year);
            s.Mis = Math.Log(cents / 100.0) - s.LogFair;
            s.Sent = s.Mis;
            s.PrevClose = s.Open = s.High = s.Low = s.Last = cents;
            s.Adv = Math.Max(1000.0, o.Others(s) * s.Def.TurnoverBp / 10000.0);
            o.ListedCents = cents;
            o.LockDay = day + Math.Max(1, Math.Max(8, year) / ListingCatalogue.LockUpYearShare);
            o.YearCents = cents;
            o.YearIndex = indexNow;
            return s;
        }
    }
}
