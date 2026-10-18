using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a founder's deal, a buy-back or new shares would come to now; nothing is changed.</summary>
    public struct OwnQuote
    {
        public long Shares;
        public int Cents;            // the price a share
        public long Value;           // money for the shares
        public long Reserve;         // the reserve that goes with them
        public long Fee;             // commission, or the underwriters' for new shares
        public long Total;           // positive: the founder pays; negative: receives
        public OwnWhy Why;           // None: it can be asked for
    }

    /// <summary>
    /// Part of the engine: a player's own company on the market's clock. The founder's asks are tape events, so a reload and
    /// every machine play them at the same moment; money moves only on the founder's machine (<see cref="Mine"/>), never for
    /// a test's company. Costs a field read per company while none is listed.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>True for a player's company whose founder is the player of this machine.</summary>
        public bool Mine(Stock s)
        {
            return MarketBots.Mine(State, s);
        }

        public Stock MineListed()
        {
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && Mine(s))
                {
                    return s;
                }
            }
            return null;
        }

        private double PriceLevel => Math.Max(100, State.Env.PriceLevel) / 1000.0;

        // Money of the founder's, on their machine only and never for a test's company.
        private void Pay(Stock s, long amount, string why)
        {
            if (amount > 0 && Mine(s) && !s.Own.Test)
            {
                Wallet.Force(amount, why, s.Name);
            }
        }

        private void Receive(Stock s, long amount, string why)
        {
            if (amount > 0 && Mine(s) && !s.Own.Test)
            {
                Wallet.Give(amount, why, s.Name);
            }
        }

        /// <summary>
        /// The market plays alone again: every other player's company leaves, paid out at its last price, and the player's own,
        /// set aside while a guest, is back. Returns how many companies came or went.
        /// </summary>
        public int Alone()
        {
            Finish();
            if (State.Following)
            {
                return 0;
            }
            int moved = 0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.Own != null && s.Own.Owner != State.Me)
                {
                    Leave(s, State.Day, GoneReason.Delisted, Math.Max(1, s.Last), MarketNewsKind.OwnAway);
                    moved++;
                }
            }
            ListingFile f = State.Listing;
            if (f != null && !string.IsNullOrEmpty(f.Suspended))
            {
                Stock back = MarketStore.ReadStockLine(f.Suspended, Year);
                f.Suspended = null;
                if (back != null && back.Own != null && back.Own.Owner == State.Me)
                {
                    back.Id = FreeId(back.Id);
                    Stock old = State.Stock(back.Id);
                    if (old != null)
                    {
                        State.Stocks.Remove(old);
                    }
                    back.Quarter = MarketCatalogue.QuarterOf(State.Day, Year);
                    back.PendingQuarter = -1;
                    back.ResultsDay = 0;
                    back.QRev = back.QNi = 0.0;
                    back.Own.Expect = ListingRules.Expected(back, back.Quarter, Year, State.Env);
                    back.BarCount = back.LastBarCount = 0;
                    back.DayVolume = 0;
                    back.BidCents = back.BidEndDay = back.BidDay = 0;
                    back.Last = back.MidCents;
                    Reweigh(back, true);
                    State.Stocks.Add(back);
                    f.StockId = back.Id;
                    f.Stage = ListingStage.Listed;
                    moved++;
                }
            }
            return moved;
        }

        // The book priced at its last open and the company listed on its first day, only by the founder's market playing alone.
        private void OwnMorning(int day, in MarketEnv env)
        {
            ListingFile f = State.Listing;
            if (f == null || State.Following || State.Me == 0UL)
            {
                return;
            }
            if (f.Stage == ListingStage.Building && f.BookEnd > 0 && f.BookEnd <= day)
            {
                PriceTheBook(f, day, env);
            }
            if (f.Stage == ListingStage.Priced && f.ListDay > 0 && f.ListDay <= day)
            {
                ListOwn(f, day, env);
            }
        }

        private void PriceTheBook(ListingFile f, int day, in MarketEnv env)
        {
            f.StockId = FreeId(f.StockId > 0 ? f.StockId : ListingRules.IdFor(State.Me));
            int price = ListingBook.Price(f, _secret, f.StockId, IndexMonthBp(), env.OwnStanding, out double _);
            f.PriceCents = price;
            f.BookEnd = day;
            if (price < f.WalkCents)
            {
                f.Stage = ListingStage.Audited;   // the audit still stands: another prospectus may follow
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.OwnPulled, Stock = f.StockId, A = price, B = f.WalkCents });
                return;
            }
            f.Stage = ListingStage.Priced;
            f.ListDay = MarketTrading.NextTradingDay(day);
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.OwnPriced, Stock = f.StockId, A = price, B = f.ListDay, C = f.FloatBp });
        }

        // An id no company in this market has; another player's company may have the founder's own by hash.
        private int FreeId(int id)
        {
            for (int tries = 0; tries < ListingCatalogue.IdCount; tries++)
            {
                Stock there = State.Stock(id);
                if (there == null || (!there.Listed && (there.Own == null || there.Own.Owner == State.Me)))
                {
                    return id;
                }
                id = ListingCatalogue.IdFirst + (id - ListingCatalogue.IdFirst + 1) % ListingCatalogue.IdCount;
            }
            return id;
        }

        private int IndexMonthBp()
        {
            int n = State.IndexDaily.Count;
            int then = n >= 21 ? State.IndexDaily[n - 21].C : n > 0 ? State.IndexDaily[0].C : 0;
            return then > 0 && State.IndexLast > 0 ? (int)((State.IndexLast - (long)then) * 10000L / then) : 0;
        }

        private void ListOwn(ListingFile f, int day, in MarketEnv env)
        {
            Stock s = ListingBook.Make(f, State.Me, day, Year, env, env.OwnDebt, State.IndexLast);
            s.ListedDay = day;
            Stock old = State.Stock(s.Id);
            if (old != null)
            {
                State.Stocks.Remove(old);
            }
            Reweigh(s, true);   // before it is counted: the indices stay where they were
            State.Stocks.Add(s);
            long raised = MarketTrading.Value(f.NewShares, f.PriceCents, false);
            long underwriters = ListingBook.Underwriting(raised, PriceLevel);
            long exchange = ListingBook.ListingFee(s.Shares * (f.PriceCents / 100.0), PriceLevel);
            Receive(s, raised, ListingMoney.Offer);
            Pay(s, underwriters, ListingMoney.Underwriting);
            Pay(s, exchange, ListingMoney.Exchange);
            f.Raised = f.Test ? 0 : raised - underwriters;
            f.Fees += f.Test ? 0 : underwriters + exchange;
            f.Stage = ListingStage.Listed;
            f.ListDay = day;
            f.FeeYear = MarketCatalogue.YearOf(day, Year);
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = f.PriceCents, B = day });
        }

        // The calendar days since the last open: the statements, quarters and the other holders' part of each day's profit.
        private void OwnBusiness(Stock s, in MarketEnv env, int from, int day)
        {
            double moved = 0.0;
            // Statement days from before the days played now (set aside while its founder was a guest) count all the same.
            for (int d = env.OwnFrom + 1; d < from && d <= env.OwnFrom + env.OwnDays; d++)
            {
                moved += ListingRules.Day(s, env, Year, d);
            }
            for (int d = from; d <= day; d++)
            {
                int quarter = MarketCatalogue.QuarterOf(d, Year);
                if (quarter != s.Quarter)
                {
                    if (s.PendingQuarter >= 0)
                    {
                        Say(ListingRules.Results(s, day, Year, env));   // results never given out after a long gap: out now
                        OwnResults(s, day);
                    }
                    ListingRules.CloseQuarter(s, quarter, Year, env);
                    if (s.ResultsDay <= day)
                    {
                        s.ResultsDay = MarketTrading.NextTradingDay(day);
                    }
                }
                moved += ListingRules.Day(s, env, Year, d);
            }
            ListingRules.Morning(s, env, Year, day - from + 1);
            Holders(s, moved);
        }

        // The other holders' part of the profit between the founder's cash and the reserve; the part under a unit waits.
        private void Holders(Stock s, double money)
        {
            OwnListing o = s.Own;
            o.Carry += money;
            long whole = (long)Math.Truncate(o.Carry);
            if (whole == 0)
            {
                return;
            }
            o.Carry -= whole;
            if (whole > 0)
            {
                Pay(s, whole, ListingMoney.Holders);
            }
            else
            {
                Receive(s, -whole, ListingMoney.HoldersBack);
            }
        }

        private void OwnResults(Stock s, int day)
        {
            OwnListing o = s.Own;
            ActivistDemand demand = ListingVotes.Reason(s, State.IndexLast, day, Year, out double strength);
            if (demand == ActivistDemand.None || MarketRandom.Unit(_secret, MarketRandom.Activist, s.Id, 1000 + day) >= ListingCatalogue.ActivistChanceQuarter * strength)
            {
                return;
            }
            long others = o.Others(s);
            o.ActBp = Math.Min(ListingVotes.Stake(_secret, s.Id, day), (int)(others * 10000L / Math.Max(1L, s.Shares)));
            o.ActDemand = demand;
            o.ActDay = MarketTrading.MarketDays(day, ListingCatalogue.EgmNoticeDays);
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.OwnActivist, Stock = s.Id, A = o.ActBp, B = (int)demand, C = o.ActDay });
        }

        // A bid's last day, its meetings, a bid while it is cheap, a split when its shares trade high.
        private void OwnCorporate(Stock s, int day)
        {
            if (OwnBidDue(s, day))
            {
                return;   // it has left the market
            }
            OwnMeetings(s, day);
            OwnBidChance(s, day);
            if (s.SplitDay <= 0 && s.BidCents <= 0 && s.Last > MarketCatalogue.SplitAboveCents)
            {
                s.SplitRatio = MarketLife.SplitRatioAt(s.Last);
                s.SplitDay = MarketTrading.MarketDays(day, MarketCatalogue.SplitNoticeDays);
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Split, Stock = s.Id, A = s.SplitRatio, B = s.SplitDay });
            }
        }

        private void OwnMeetings(Stock s, int day)
        {
            OwnListing o = s.Own;
            if (o.ActBp > 0 && ListingVotes.Met(s))
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.OwnWithdrawn, Stock = s.Id, A = (int)o.ActDemand });
                o.ActBp = 0;
                o.ActDemand = ActivistDemand.None;
                o.ActDay = 0;
            }
            bool yearly = o.AgmDay > 0 && o.AgmDay <= day;
            if (o.ActBp > 0 && o.ActDay > 0 && o.ActDay <= day && !yearly)
            {
                Meeting(s, day, true);
            }
            if (yearly)
            {
                Meeting(s, day, false);
            }
        }

        private void Meeting(Stock s, int day, bool extraordinary)
        {
            OwnListing o = s.Own;
            int bits = ListingVotes.Meeting(s, State.IndexLast, _secret, day, extraordinary, out int against);
            o.Votes = bits;
            o.VotesAgainstBp = against;
            if ((bits & ListingVotes.PayLost) != 0)
            {
                o.PayBp = Math.Min(o.PayBp, ListingCatalogue.PayRefBp);
            }
            if ((bits & ListingVotes.PayoutVoted) != 0)
            {
                Special(s, day, ListingVotes.PayoutCents(o));
            }
            if (!extraordinary)
            {
                o.Board = (bits & ListingVotes.BoardLost) == 0;
            }
            if ((bits & ListingVotes.DemandPassed) != 0)
            {
                switch (o.ActDemand)
                {
                    case ActivistDemand.Payout:
                        Special(s, day, ListingVotes.PayoutCents(o));
                        break;
                    case ActivistDemand.Pay:
                        o.PayBp = Math.Min(o.PayBp, ListingCatalogue.PayRefBp);
                        break;
                    case ActivistDemand.Board:
                        o.Board = false;
                        break;
                    case ActivistDemand.Sale:
                        o.SaleDay = MarketTrading.MarketDays(day, ListingCatalogue.SaleBidDays);
                        break;
                }
            }
            if (!o.Board)
            {
                o.PayBp = Math.Min(o.PayBp, ListingCatalogue.PayRefBp);   // a board that is not the founder's sets the fair pay
            }
            if ((bits & ListingVotes.HadDemand) != 0)
            {
                o.ActBp = 0;   // the investor has had their vote and sells out
                o.ActDemand = ActivistDemand.None;
                o.ActDay = 0;
            }
            if (!extraordinary)
            {
                o.AgmDay = 0;
                o.YearCents = s.Last;
                o.YearIndex = State.IndexLast;
                o.YearScandals = 0;
                o.YearDividendCents = 0;
            }
            int bp = ((bits & (ListingVotes.PayLost | ListingVotes.PayoutVoted | ListingVotes.BoardLost)) != 0 ? ListingCatalogue.MeetingKickBp : 0)
                     + ((bits & ListingVotes.DemandPassed) != 0 ? ListingCatalogue.ActivistWinKickBp : 0);
            s.Sent += bp / 10000.0;
            s.Mis += bp / 10000.0;
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.OwnMeeting, Stock = s.Id, A = bits, B = against, C = extraordinary ? 1 : 0 });
        }

        // A dividend out of the reserve the holders voted for, on top of one declared and not yet paid.
        private void Special(Stock s, int day, int cents)
        {
            cents = Math.Min(cents, ListingRules.Most(s.Own));
            if (cents <= 0)
            {
                return;
            }
            bool waiting = s.DeclaredCents > 0 && !s.ExDone && s.RecordDay >= day;
            s.DeclaredCents = waiting ? s.DeclaredCents + cents : cents;
            if (!waiting)
            {
                s.RecordDay = MarketTrading.MarketDays(day, MarketCatalogue.DividendRecordDays);
                s.PayDay = MarketTrading.MarketDays(s.RecordDay, MarketCatalogue.DividendPayDays);
            }
            s.ExDone = false;
            s.Own.YearDividendCents += cents;
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Dividend, Stock = s.Id, A = s.DeclaredCents, B = s.RecordDay, C = s.PayDay });
        }

        /// <summary>An investor presses a player company's board for a demand now (the test tools), as one would after results.</summary>
        public bool PressBoard(int stockId, ActivistDemand demand)
        {
            Finish();
            Stock s = State.Stock(stockId);
            if (s == null || s.Own == null || !s.Listed || demand == ActivistDemand.None)
            {
                return false;
            }
            OwnListing o = s.Own;
            o.ActBp = Math.Min(ListingVotes.Stake(_secret, s.Id, State.Day), (int)(o.Others(s) * 10000L / Math.Max(1L, s.Shares)));
            o.ActDemand = demand;
            o.ActDay = MarketTrading.MarketDays(State.Day, 2);
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnActivist, Stock = s.Id, A = o.ActBp, B = (int)demand, C = o.ActDay });
            return true;
        }

        private void OwnBidChance(Stock s, int day)
        {
            OwnListing o = s.Own;
            if (s.BidCents > 0 || day - s.ListedDay <= 10 || o.Others(s) <= 0)
            {
                return;
            }
            double chance = o.SaleDay > 0 && day >= o.SaleDay ? 1.0 : ListingVotes.BidChance(ListingVotes.Read(s, State.IndexLast), TradingYear);
            if (chance > 0.0 && MarketRandom.Unit(_secret, MarketRandom.OwnBid, s.Id, 0) < chance)
            {
                OwnBid(s, day);
            }
        }

        // A bid for a player's company at a premium; as for any company, from the next open its price is the bid's.
        private void OwnBid(Stock s, int day)
        {
            double premium = ListingCatalogue.OwnTakeoverPremiumLeast + (ListingCatalogue.OwnTakeoverPremiumMost - ListingCatalogue.OwnTakeoverPremiumLeast)
                             * MarketRandom.Unit(_secret, MarketRandom.OwnBid, s.Id, 2);
            s.BidCents = Math.Max(1, (int)Math.Round(s.Mid * (1.0 + premium) * 100.0));
            s.Mis -= s.Sent;
            s.Sent = 0.0;
            s.BidDay = day;
            s.BidEndDay = MarketTrading.MarketDays(day, ListingCatalogue.OwnTakeoverDaysLeast + MarketRandom.Below(_secret, MarketRandom.OwnBid, s.Id, 3,
                ListingCatalogue.OwnTakeoverDaysMost - ListingCatalogue.OwnTakeoverDaysLeast + 1));
            s.Own.SaleDay = 0;
            _scratch.Clear();
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock other = State.Stocks[i];
                if (other.Listed && other != s && other.Own == null && other.Sector == s.Sector && other.MarketValue > s.MarketValue * 3.0 && other.BidCents <= 0
                    && (other.Def == null || !other.Def.Holding))
                {
                    _scratch.Add(other);
                }
            }
            s.BidderId = 0;
            Stock named = _bidderNext > 0 ? State.Stock(_bidderNext) : null;
            _bidderNext = 0;
            if (named != null && named.Listed && named != s)
            {
                s.BidderId = named.Id;   // a rival tycoon's holding bids (MarketEngine.Holdings.cs)
                named.Outlook -= 0.02;
            }
            else if (_scratch.Count > 0 && MarketRandom.Unit(_secret, MarketRandom.OwnBid, s.Id, 4) < 0.7)
            {
                Stock bidder = _scratch[MarketRandom.Below(_secret, MarketRandom.OwnBid, s.Id, 5, _scratch.Count)];
                s.BidderId = bidder.Id;
                bidder.Outlook -= 0.02;   // the premium it pays, from the next open
            }
            _scratch.Clear();
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Bid, Stock = s.Id, A = s.BidCents, B = s.BidEndDay, C = s.BidderId });
        }

        // A bid's last day: it wins only while the founder holds under the threshold and the holders take it; the founder's
        // machine hands its businesses over when the director hears BidDone. True when it has left.
        private bool OwnBidDue(Stock s, int day)
        {
            if (s.BidCents <= 0 || s.BidEndDay > day)
            {
                return false;
            }
            OwnListing o = s.Own;
            bool can = ListingVotes.CanWin(s);
            double worth = MarketValuation.Fair(s, State.Env) + Math.Max(0.0, o.Rps);
            bool taken = can && MarketRandom.Unit(_secret, MarketRandom.OwnBid, s.Id, 1) < ListingVotes.Acceptance(s.BidCents / 100.0, worth);
            if (!taken)
            {
                s.BidCents = 0;
                s.BidEndDay = 0;
                s.BidDay = 0;
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.OwnBidBlocked, Stock = s.Id, A = can ? 0 : 1 });
                return false;
            }
            Receive(s, MarketTrading.Value(o.Founder, s.BidCents, false), ListingMoney.Takeover);
            if (Mine(s))
            {
                State.Listing.Stage = ListingStage.None;
            }
            Leave(s, day, GoneReason.TakenOver, s.BidCents);
            return true;
        }

        /// <summary>Why a founder's action cannot be asked for now; the same test is made again at its moment on every machine.</summary>
        public OwnWhy CanAsk(Stock s, OwnAction action, long value)
        {
            if (s == null || !s.Listed || s.Own == null)
            {
                return OwnWhy.NotListed;
            }
            OwnListing o = s.Own;
            if (action == OwnAction.Audit || action == OwnAction.Proceeds)
            {
                return OwnWhy.None;   // the auditors' findings and a business sold are not the founder's to ask
            }
            bool board = action == OwnAction.Pay || action == OwnAction.Dividend || action == OwnAction.BuyBack || action == OwnAction.Issue || action == OwnAction.Private;
            if (board && !o.Board)
            {
                return OwnWhy.Board;
            }
            if (action == OwnAction.Pay || action == OwnAction.Dividend)
            {
                return OwnWhy.None;
            }
            if (!Trading)
            {
                return OwnWhy.Shut;
            }
            switch (action)
            {
                case OwnAction.Deal:
                    return ListingRules.DealBlocked(s, State.Day, Year, value);
                case OwnAction.BuyBack:
                    if (ListingRules.Closed(s, State.Day, Year))
                    {
                        return OwnWhy.Closed;
                    }
                    if (s.BidCents > 0)
                    {
                        return OwnWhy.Bid;
                    }
                    if (value <= 0 || value > MarketTrading.MostPerOrder(s) || value > o.Others(s) - s.Shares * o.ActBp / 10000L)
                    {
                        return OwnWhy.TooLarge;
                    }
                    return ListingRules.YearUsedBp(o, State.Day, Year, o.BackBp) + ListingRules.Bp(value, s.Shares) > ListingCatalogue.BuyBackMostBp ? OwnWhy.YearLimit : OwnWhy.None;
                case OwnAction.Issue:
                    if (ListingRules.Closed(s, State.Day, Year))
                    {
                        return OwnWhy.Closed;
                    }
                    if (s.BidCents > 0)
                    {
                        return OwnWhy.Bid;
                    }
                    return value <= 0 || value > ListingRules.IssueRoom(s, State.Day, Year) ? OwnWhy.TooLarge : OwnWhy.None;
                case OwnAction.Private:
                    if (s.BidCents > 0)
                    {
                        return OwnWhy.Bid;
                    }
                    return o.StakeBp(s) < ListingCatalogue.PrivateLeastBp ? OwnWhy.Control : OwnWhy.None;
            }
            return OwnWhy.None;
        }

        /// <summary>What a founder's deal (positive bought), a buy-back, new shares or going private comes to now.</summary>
        public OwnQuote Quote(Stock s, OwnAction action, long value)
        {
            var q = new OwnQuote { Why = CanAsk(s, action, value) };
            if (s == null || s.Own == null || !s.Listed)
            {
                return q;
            }
            OwnListing o = s.Own;
            int least = _world.CommissionLeast;
            double reserveEach = Math.Max(0.0, o.Rps);
            switch (action)
            {
                case OwnAction.Deal:
                {
                    bool buying = value > 0;
                    q.Shares = Math.Abs(value);
                    q.Cents = q.Shares > 0 ? MarketTrading.MarketPrice(State, s, buying, q.Shares, State.Phase) : 0;
                    q.Value = MarketTrading.Value(q.Shares, q.Cents, buying);
                    q.Reserve = (long)Math.Floor(reserveEach * q.Shares);
                    q.Fee = buying ? MarketTrading.Commission(q.Value, least) : Math.Min(q.Value, MarketTrading.Commission(q.Value, least));
                    q.Total = buying ? q.Value - q.Reserve + q.Fee : -(q.Value - q.Reserve - q.Fee);
                    break;
                }
                case OwnAction.BuyBack:
                    q.Shares = Math.Max(0L, value);
                    q.Cents = q.Shares > 0 ? MarketTrading.MarketPrice(State, s, true, q.Shares, State.Phase) : 0;
                    q.Value = MarketTrading.Value(q.Shares, q.Cents, true);
                    q.Reserve = (long)Math.Floor(reserveEach * q.Shares);
                    q.Fee = MarketTrading.Commission(q.Value, least);
                    q.Total = q.Value - q.Reserve + q.Fee;
                    break;
                case OwnAction.Issue:
                {
                    q.Shares = Math.Max(0L, value);
                    Quote quote = MarketTrading.QuoteOf(State, s, State.Phase);
                    q.Cents = Math.Max(1, (int)Math.Floor(quote.Bid * (1.0 - ListingCatalogue.PlacingDiscount)));
                    q.Value = MarketTrading.Value(q.Shares, q.Cents, false);
                    q.Reserve = (long)Math.Floor(reserveEach * q.Shares);
                    q.Fee = q.Value * ListingCatalogue.UnderwritingBp / 10000L;
                    q.Total = -(q.Value - q.Reserve - q.Fee);
                    break;
                }
                case OwnAction.Private:
                    q.Shares = Math.Max(0L, o.Others(s));
                    q.Cents = ListingRules.PrivateCents(s, State.Day, Year);
                    q.Value = MarketTrading.Value(q.Shares, q.Cents, true);
                    q.Reserve = (long)Math.Floor(reserveEach * q.Shares);
                    q.Total = q.Value - q.Reserve;
                    break;
            }
            return q;
        }

        // A founder's action at its moment, from the tape too: the same test as when asked, then done; a refusal is news.
        private void OwnEvent(in MarketEvent e)
        {
            var action = (OwnAction)(e.Kind - MarketInputs.EventOwn);
            Stock s = State.Stock(e.A);
            if (s == null || s.Own == null || !s.Listed)
            {
                return;
            }
            OwnWhy why = CanAsk(s, action, e.B);
            if (why != OwnWhy.None)
            {
                Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnRefused, Stock = s.Id, A = (int)action, B = (int)why });
                return;
            }
            OwnListing o = s.Own;
            switch (action)
            {
                case OwnAction.Pay:
                    o.PayBp = MarketValuation.Clamp(e.B, 0, ListingCatalogue.PaySteps[ListingCatalogue.PaySteps.Length - 1]);
                    break;
                case OwnAction.Dividend:
                    o.ProposedCents = MarketValuation.Clamp(e.B, 0, 10000000);
                    break;
                case OwnAction.Deal:
                    Deal(s, e.B);
                    break;
                case OwnAction.BuyBack:
                    BuyBack(s, e.B);
                    break;
                case OwnAction.Issue:
                    Issue(s, e.B);
                    break;
                case OwnAction.Private:
                    TakePrivate(s);
                    break;
                case OwnAction.Proceeds:
                {
                    // A company business sold: the price is every holder's by their shares; the others' part goes from the
                    // founder's cash into the reserve.
                    double each = Math.Max(0, e.B) * 1000.0 / Math.Max(1L, s.Shares);
                    Pay(s, (long)Math.Floor(each * Math.Max(0L, o.Others(s))), ListingMoney.Proceeds);
                    o.Rps += each;
                    s.Equity += Math.Max(0, e.B) * 1000.0;
                    break;
                }
                case OwnAction.Audit:
                    if ((e.B & ListingBook.Qualifying) != 0)
                    {
                        s.Outlook += ListingCatalogue.AuditOutlook;
                        s.Mis += ListingCatalogue.AuditKickBp / 10000.0;
                        s.Sent += ListingCatalogue.AuditKickBp / 10000.0;
                        s.Last = s.MidCents;
                        Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnAudit, Stock = s.Id, A = e.B });
                    }
                    break;
            }
        }

        private void Deal(Stock s, long shares)
        {
            OwnListing o = s.Own;
            OwnQuote q = Quote(s, OwnAction.Deal, shares);
            bool buying = shares > 0;
            if (buying)
            {
                Pay(s, q.Value, MarketMoney.Bought);
                Receive(s, q.Reserve, ListingMoney.ReserveIn);
            }
            else
            {
                Receive(s, q.Value, MarketMoney.Sold);
                Pay(s, q.Reserve, ListingMoney.ReserveOut);
            }
            Pay(s, q.Fee, MarketMoney.Commission);
            o.Founder += shares;
            int bp = ListingRules.Bp(Math.Abs(shares), s.Shares);
            ListingRules.Count(o, State.Day, Year, shares > 0 ? bp : 0, shares < 0 ? bp : 0, 0);
            long gain = ListingRules.ShortSwing(o, State.Day, shares, q.Cents, Year);
            if (gain > 0)
            {
                Pay(s, gain, ListingMoney.ShortSwing);
                o.Rps += gain / (double)Math.Max(1L, o.Others(s));
                Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnDisgorged, Stock = s.Id, A = (int)Math.Min(int.MaxValue, gain) });
            }
            Moved(s, buying, q.Shares, ListingRules.Reading(shares, s.Shares, ListingCatalogue.DealKickBp));
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnDeal, Stock = s.Id, A = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, shares)), B = q.Cents });
        }

        private void BuyBack(Stock s, long shares)
        {
            OwnQuote q = Quote(s, OwnAction.BuyBack, shares);
            Pay(s, q.Total, ListingMoney.BuyBack);
            long before = s.Shares;
            ListingRules.Count(s.Own, State.Day, Year, 0, 0, ListingRules.Bp(shares, before));
            Reshare(s, -shares);
            Moved(s, true, shares, ListingRules.Reading(shares, before, ListingCatalogue.BuyBackKickBp));
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnBuyBack, Stock = s.Id, A = (int)Math.Min(int.MaxValue, shares), B = q.Cents });
        }

        private void Issue(Stock s, long shares)
        {
            OwnListing o = s.Own;
            OwnQuote q = Quote(s, OwnAction.Issue, shares);
            Receive(s, q.Value, ListingMoney.Issue);
            Pay(s, q.Fee, ListingMoney.Underwriting);
            Pay(s, q.Reserve, ListingMoney.ReserveOut);
            long before = s.Shares;
            int year = MarketCatalogue.YearOf(State.Day, Year);
            o.IssuedBp = (o.IssuedYear == year ? o.IssuedBp : 0) + (int)(shares * 10000L / Math.Max(1L, before));
            o.IssuedYear = year;
            Reshare(s, shares);
            double reading = ListingRules.Reading(shares, before, ListingCatalogue.IssueKickBp);
            s.Mis += reading;
            s.Sent += reading;
            s.Last = s.MidCents;
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.OwnIssue, Stock = s.Id, A = (int)Math.Min(int.MaxValue, shares), B = q.Cents });
        }

        private void TakePrivate(Stock s)
        {
            OwnQuote q = Quote(s, OwnAction.Private, 0);
            Pay(s, q.Total, ListingMoney.Private);
            if (Mine(s))
            {
                State.Listing.Stage = ListingStage.None;
            }
            Leave(s, State.Day, GoneReason.Delisted, q.Cents, MarketNewsKind.OwnPrivate);
        }

        // Shares in issue changed: the indices take it without a jump, and a day's volume follows the other holders' shares.
        private void Reshare(Stock s, long change)
        {
            long othersBefore = Math.Max(1L, s.Own.Others(s));
            Reweigh(s, false);
            s.Shares = Math.Max(1L, s.Shares + change);
            Reweigh(s, true);
            s.Adv = Math.Max(1000.0, s.Adv * Math.Max(1L, s.Own.Others(s)) / othersBefore);
        }

        // A founder's or the company's deal on the price: the lasting part of its size's move, and the market's reading of it.
        private void Moved(Stock s, bool buying, long shares, double reading)
        {
            double share = Math.Sqrt(shares / Math.Max(1.0, s.Adv));
            double kept = (buying ? 1.0 : -1.0) * MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(State, s) * share;
            s.Mis += kept + reading;
            s.Sent += kept + reading;
            s.DayVolume += shares;
            s.Impact += shares;
            s.Last = s.MidCents;
        }
    }
}
