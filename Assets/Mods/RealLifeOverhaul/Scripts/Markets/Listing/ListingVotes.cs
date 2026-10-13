using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What the other holders make of a listed player's company's year, as the votes read it.</summary>
    public struct HolderMood
    {
        public double Lag;               // the city index's year's return over the company's (a share of a year; nothing when it did better)
        public double PayOver;           // the founder's pay over the fair rate, in fair rates
        public double Hoard;             // the reserve held back against the price, 0 to 1
        public double Cheap;             // the price under what the company is worth, 0 to 2
        public int Scandals;             // the founder's scandals since the last yearly meeting
        public bool Activist;            // an investor is pressing a demand
    }

    /// <summary>
    /// A listed player's company and its holders: the yearly meeting's votes (the founder's pay, a payout of the reserve
    /// the holders may ask for, and the founder's place on the board), an investor who buys in to press a demand and the
    /// meeting they call, and a bid for the company while it is cheap. Game-free; every chance is a draw from the market
    /// day's secret, made on the day.
    ///
    /// A VOTE: the founder's shares vote for the board; of the other holders' shares a share is cast (the turnout), and
    /// of those a share goes against the board, larger the worse the year was for them (below the city index, pay above
    /// the fair rate, a reserve held back, scandals, an investor's campaign), with the day's mood. An investor's stake
    /// votes against. So a founder with half the shares or more carries every vote; under half they can lose one, and
    /// the less they hold the likelier. What a lost vote does: pay back to the fair rate; a payout of the reserve the
    /// holders asked for; the board no longer the founder's to the next yearly meeting (it then pays out half the reserve
    /// at each results, sets the fair pay and decides no buy-back, new shares or taking private).
    /// </summary>
    public static class ListingVotes
    {
        // What went against the founder at a meeting, and what was voted on (OwnListing.Votes).
        public const int PayLost = 1, PayoutVoted = 2, BoardLost = 4, DemandPassed = 8, HadPay = 16, HadPayout = 32, HadDemand = 64, Extraordinary = 128;

        /// <summary>What the holders make of the year: the company's return with its dividends against the city index's, the pay, the reserve, the price.</summary>
        public static HolderMood Read(Stock s, int indexNow)
        {
            OwnListing o = s.Own;
            var m = new HolderMood { Scandals = o.YearScandals, Activist = o.ActBp > 0 };
            double price = Math.Max(1, s.Last) / 100.0;
            if (o.YearCents > 0)
            {
                double mine = (s.Last + o.YearDividendCents) / (double)o.YearCents - 1.0;
                double city = o.YearIndex > 0 && indexNow > 0 ? indexNow / (double)o.YearIndex - 1.0 : 0.0;
                m.Lag = Math.Max(0.0, city - mine);
            }
            m.PayOver = Math.Max(0.0, (o.PayBp - ListingCatalogue.PayRefBp) / (double)ListingCatalogue.PayRefBp);
            double held = Math.Max(0.0, o.Rps) / price;
            bool paid = o.YearDividendCents >= Math.Max(0.0, o.Rps) * 100.0 * 0.25;
            m.Hoard = !paid ? MarketValuation.Clamp((held - ListingCatalogue.HoardShare) / ListingCatalogue.HoardShare, 0.0, 1.0) : 0.0;
            m.Cheap = MarketValuation.Clamp((-s.Sent + ListingCatalogue.CheapMis) / -ListingCatalogue.CheapMis, 0.0, 2.0);
            return m;
        }

        public static double PayAgainst(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.PayAgainstBase + ListingCatalogue.PayAgainstPerRef * m.PayOver, 0.0, 0.95);
        }

        /// <summary>Of the votes cast by the other holders, the share for a payout of the reserve they ask for.</summary>
        public static double PayoutFor(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.DividendAgainstBase + ListingCatalogue.DividendAgainstHoard * m.Hoard, 0.0, 0.95);
        }

        public static double BoardAgainst(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.BoardAgainstBase + ListingCatalogue.BoardAgainstLag * m.Lag + ListingCatalogue.BoardAgainstScandal * m.Scandals
                                         + ListingCatalogue.BoardAgainstPay * m.PayOver + (m.Activist ? ListingCatalogue.BoardAgainstActivist : 0.0), 0.0, 0.95);
        }

        /// <summary>Of the votes cast by the other holders, the share behind an investor's demand.</summary>
        public static double DemandFor(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.ActivistSupport + BoardAgainst(m), 0.0, 0.95);
        }

        /// <summary>
        /// One vote against the board's side: the founder's shares on the board's side, the other holders' cast votes
        /// split by the share against, an investor's stake against. True if the board's side carries it.
        /// </summary>
        public static bool BoardCarries(long founder, long others, long investor, double turnout, double against)
        {
            long free = Math.Max(0L, others - investor);
            double cast = free * MarketValuation.Clamp(turnout, 0.0, 1.0);
            double no = cast * against + investor;
            double yes = founder + cast * (1.0 - against);
            return yes >= no;
        }

        /// <summary>The share of the other holders' votes cast at a meeting: drawn on its day.</summary>
        public static double Turnout(ulong secret, int stockId, int day)
        {
            return ListingCatalogue.TurnoutLeast + (ListingCatalogue.TurnoutMost - ListingCatalogue.TurnoutLeast) * MarketRandom.Unit(secret, MarketRandom.Meeting, stockId, day);
        }

        /// <summary>A share against with the day's mood on it.</summary>
        public static double OnTheDay(double against, ulong secret, int stockId, int resolution)
        {
            return MarketValuation.Clamp(against + ListingCatalogue.AgainstNoise * MarketRandom.Normal(secret, MarketRandom.Meeting, stockId, 100 + resolution), 0.01, 0.99);
        }

        /// <summary>
        /// The yearly meeting (or, extraordinary, the one an investor called): what was voted on and what went against
        /// the founder (the bits above), and the share of the votes cast against the board (bp).
        /// </summary>
        public static int Meeting(Stock s, int indexNow, ulong secret, int day, bool extraordinary, out int boardAgainstBp)
        {
            OwnListing o = s.Own;
            HolderMood m = Read(s, indexNow);
            long others = Math.Max(0L, o.Others(s));
            long investor = Math.Min(others, s.Shares * Math.Max(0, o.ActBp) / 10000L);
            double turnout = Turnout(secret, s.Id, day);
            int bits = extraordinary ? Extraordinary : 0;
            double board = OnTheDay(BoardAgainst(m), secret, s.Id, 3);
            boardAgainstBp = (int)Math.Round(board * 10000.0);
            if (!extraordinary)
            {
                bits |= HadPay;
                if (!BoardCarries(o.Founder, others, investor, turnout, OnTheDay(PayAgainst(m), secret, s.Id, 1)))
                {
                    bits |= PayLost;
                }
                if (m.Hoard > 0.0)
                {
                    bits |= HadPayout;
                    if (!BoardCarries(o.Founder, others, investor, turnout, OnTheDay(PayoutFor(m), secret, s.Id, 2)))
                    {
                        bits |= PayoutVoted;
                    }
                }
                if (!BoardCarries(o.Founder, others, investor, turnout, board))
                {
                    bits |= BoardLost;
                }
            }
            if (o.ActBp > 0 && o.ActDemand != ActivistDemand.None && (extraordinary || o.ActDay <= day))
            {
                bits |= HadDemand;
                if (!BoardCarries(o.Founder, others, investor, turnout, OnTheDay(DemandFor(m), secret, s.Id, 4)))
                {
                    bits |= DemandPassed;
                }
            }
            return bits;
        }

        // ---------------------------------------------------------------- investors who press the board

        /// <summary>
        /// After a set of results: whether an investor buys in to press the board, and for what: a price well under what
        /// the company is worth (a sale), a reserve held back (a payout), pay over the fair rate (pay cut back), a year
        /// well under the city index (the board). The likelier the stronger the reason; none for a company under a bid,
        /// listed less than a quarter, or already pressed.
        /// </summary>
        public static ActivistDemand Reason(Stock s, int indexNow, int day, int year, out double strength)
        {
            OwnListing o = s.Own;
            strength = 0.0;
            if (o.ActBp > 0 || s.BidCents > 0 || day - s.ListedDay < Math.Max(8, year) / 4 || o.Others(s) <= 0)
            {
                return ActivistDemand.None;
            }
            HolderMood m = Read(s, indexNow);
            double sale = Math.Min(1.0, m.Cheap), payout = m.Hoard, pay = Math.Min(1.0, m.PayOver), board = MarketValuation.Clamp((m.Lag - ListingCatalogue.LagIndex) / ListingCatalogue.LagIndex, 0.0, 1.0);
            ActivistDemand demand = ActivistDemand.None;
            if (sale > strength)
            {
                strength = sale;
                demand = ActivistDemand.Sale;
            }
            if (payout > strength)
            {
                strength = payout;
                demand = ActivistDemand.Payout;
            }
            if (pay > strength)
            {
                strength = pay;
                demand = ActivistDemand.Pay;
            }
            if (board > strength)
            {
                strength = board;
                demand = ActivistDemand.Board;
            }
            return demand;
        }

        /// <summary>The investor's stake, in bp: drawn on the day they buy in.</summary>
        public static int Stake(ulong secret, int stockId, int day)
        {
            return ListingCatalogue.ActivistStakeLeastBp + (int)Math.Round((ListingCatalogue.ActivistStakeMostBp - ListingCatalogue.ActivistStakeLeastBp)
                                                                            * MarketRandom.Unit(secret, MarketRandom.Activist, stockId, day));
        }

        /// <summary>A payout an investor asks for, a share in cents: so much of the reserve.</summary>
        public static int PayoutCents(OwnListing o)
        {
            return (int)Math.Floor(Math.Max(0.0, o.Rps) * ListingCatalogue.SpecialPayoutBp / 10000.0 * 100.0);
        }

        /// <summary>True once what an investor asks for has been given before their meeting: they drop it.</summary>
        public static bool Met(Stock s)
        {
            OwnListing o = s.Own;
            switch (o.ActDemand)
            {
                case ActivistDemand.Pay:
                    return o.PayBp <= ListingCatalogue.PayRefBp;
                case ActivistDemand.Payout:
                    return Math.Max(0.0, o.Rps) / Math.Max(0.01, s.Last / 100.0) < ListingCatalogue.HoardShare;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- a bid for it

        /// <summary>A bid's chance today for a company this cheap (its holders voted to sell: certain on the day set).</summary>
        public static double BidChance(in HolderMood m, double tradingYear)
        {
            return m.Cheap <= 0.0 ? 0.0 : ListingCatalogue.OwnTakeoverChanceYear / Math.Max(8.0, tradingYear) * m.Cheap;
        }

        /// <summary>The chance the holders take a bid at so much against what the company is worth a share (both in money).</summary>
        public static double Acceptance(double bid, double worth)
        {
            double premium = worth > 0.0 ? bid / worth - 1.0 : 1.0;
            return MarketValuation.Clamp(ListingCatalogue.AcceptBase + ListingCatalogue.AcceptPerPremium * premium, ListingCatalogue.AcceptLeast, ListingCatalogue.AcceptMost);
        }

        /// <summary>True if a bid can win: the founder holds less than the threshold.</summary>
        public static bool CanWin(Stock s)
        {
            return s.Own.StakeBp(s) < ListingCatalogue.TakeoverThresholdBp;
        }

        /// <summary>The tax withheld from a dividend paid to the player, in money: its rate moved by the council's business tax.</summary>
        public static long DividendTax(long dividend, int businessTaxStep)
        {
            int bp = Math.Max(0, ListingCatalogue.DividendTaxBp + Math.Sign(businessTaxStep) * ListingCatalogue.DividendTaxStepBp);
            return Math.Max(0L, dividend) * bp / 10000L;
        }
    }
}
