using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What the other holders make of a listed player's company's year, as the votes read it.</summary>
    public struct HolderMood
    {
        public double Lag;               // the index's year's return over the company's, a share of a year; nothing when it did better
        public double PayOver;           // the founder's pay over the fair rate, in fair rates
        public double Hoard;             // the reserve held back against the price, 0 to 1
        public double Cheap;             // the price under what the company is worth, 0 to 2
        public int Scandals;             // the founder's scandals since the last yearly meeting
        public bool Activist;            // an investor is pressing a demand
    }

    /// <summary>
    /// A listed player's company and its holders: the meeting's votes, an investor who presses a demand, a bid while cheap.
    /// Every chance is a draw from the day's secret; a founder with half the shares carries every vote.
    /// </summary>
    public static class ListingVotes
    {
        // What went against the founder at a meeting, and what was voted on (OwnListing.Votes).
        public const int PayLost = 1, PayoutVoted = 2, BoardLost = 4, DemandPassed = 8, HadPay = 16, HadPayout = 32, HadDemand = 64, Extraordinary = 128;

        /// <summary>The year's return with dividends against the index's, the pay, the reserve, the price.</summary>
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

        /// <summary>The share of the other holders' votes for a payout of the reserve.</summary>
        public static double PayoutFor(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.DividendAgainstBase + ListingCatalogue.DividendAgainstHoard * m.Hoard, 0.0, 0.95);
        }

        public static double BoardAgainst(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.BoardAgainstBase + ListingCatalogue.BoardAgainstLag * m.Lag + ListingCatalogue.BoardAgainstScandal * m.Scandals
                                         + ListingCatalogue.BoardAgainstPay * m.PayOver + (m.Activist ? ListingCatalogue.BoardAgainstActivist : 0.0), 0.0, 0.95);
        }

        public static double DemandFor(in HolderMood m)
        {
            return MarketValuation.Clamp(ListingCatalogue.ActivistSupport + BoardAgainst(m), 0.0, 0.95);
        }

        /// <summary>True if the board's side, the founder's shares with it, carries a vote.</summary>
        public static bool BoardCarries(long founder, long others, long investor, double turnout, double against)
        {
            long free = Math.Max(0L, others - investor);
            double cast = free * MarketValuation.Clamp(turnout, 0.0, 1.0);
            double no = cast * against + investor;
            double yes = founder + cast * (1.0 - against);
            return yes >= no;
        }

        public static double Turnout(ulong secret, int stockId, int day)
        {
            return ListingCatalogue.TurnoutLeast + (ListingCatalogue.TurnoutMost - ListingCatalogue.TurnoutLeast) * MarketRandom.Unit(secret, MarketRandom.Meeting, stockId, day);
        }

        public static double OnTheDay(double against, ulong secret, int stockId, int resolution)
        {
            return MarketValuation.Clamp(against + ListingCatalogue.AgainstNoise * MarketRandom.Normal(secret, MarketRandom.Meeting, stockId, 100 + resolution), 0.01, 0.99);
        }

        /// <summary>The yearly or an extraordinary meeting: what went against the founder (the bits above); the share against in bp.</summary>
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

        /// <summary>After results: whether an investor buys in to press the board, and for what; none under a bid or already pressed.</summary>
        public static ActivistDemand Reason(Stock s, int indexNow, int day, int year, out double strength)
        {
            OwnListing o = s.Own;
            strength = 0.0;
            if (o.ActBp > 0 || s.BidCents > 0 || day - s.ListedDay < Math.Max(8, year) / 4 || o.Others(s) <= 0)
            {
                return ActivistDemand.None;
            }
            HolderMood m = Read(s, indexNow);
            double sale = Math.Min(1.0, m.Cheap), payout = m.Hoard, pay = Math.Min(1.0, m.PayOver);
            double board = MarketValuation.Clamp((m.Lag - ListingCatalogue.LagIndex) / ListingCatalogue.LagIndex, 0.0, 1.0);
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

        public static int Stake(ulong secret, int stockId, int day)
        {
            return ListingCatalogue.ActivistStakeLeastBp + (int)Math.Round((ListingCatalogue.ActivistStakeMostBp - ListingCatalogue.ActivistStakeLeastBp)
                                                                            * MarketRandom.Unit(secret, MarketRandom.Activist, stockId, day));
        }

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

        public static double BidChance(in HolderMood m, double tradingYear)
        {
            return m.Cheap <= 0.0 ? 0.0 : ListingCatalogue.OwnTakeoverChanceYear / Math.Max(8.0, tradingYear) * m.Cheap;
        }

        /// <summary>The chance the holders take a bid at so much against the company's worth a share.</summary>
        public static double Acceptance(double bid, double worth)
        {
            double premium = worth > 0.0 ? bid / worth - 1.0 : 1.0;
            return MarketValuation.Clamp(ListingCatalogue.AcceptBase + ListingCatalogue.AcceptPerPremium * premium, ListingCatalogue.AcceptLeast, ListingCatalogue.AcceptMost);
        }

        /// <summary>True while the founder holds less than the threshold.</summary>
        public static bool CanWin(Stock s)
        {
            return s.Own.StakeBp(s) < ListingCatalogue.TakeoverThresholdBp;
        }

        /// <summary>The tax withheld from a dividend paid to the player: the rate moved by the council's business tax.</summary>
        public static long DividendTax(long dividend, int businessTaxStep)
        {
            int bp = Math.Max(0, ListingCatalogue.DividendTaxBp + Math.Sign(businessTaxStep) * ListingCatalogue.DividendTaxStepBp);
            return Math.Max(0L, dividend) * bp / 10000L;
        }
    }
}
