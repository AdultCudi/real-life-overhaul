using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: the stock market's extras. Splits by each board's own level, consolidations and the founder's own
    /// split are decided where the market is played for the first time and put on the tape (<see cref="MarketInputs.EventSplit"/>),
    /// so every machine of a session and every replayed day takes the same ones whatever its own option says. The fund's fee,
    /// its distributions and the fading of the player's own push ride the market's steps and closes. Fund deals and blocks
    /// are made on a press. Nothing per frame; a step fades a list that is usually empty.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>The extras' option (Settings.MarketsExtras, set by the director): off, no new split is decided here and no fund unit or block is dealt.</summary>
        public bool ExtrasOn = true;

        /// <summary>Told of a distribution of the fund paid to the player: whole money, before tax. Null: nobody is told.</summary>
        public Action<long> FundPaid;

        /// <summary>Told of a split that took effect: the company, new shares, old shares, and whole money paid to the player for parts of a share.</summary>
        public Action<Stock, int, int, long> SplitDone;

        // ------------------------------------------------------------------------------------------------ splits

        // A split announced for today takes effect at the open, unless a dividend declared has not left the price yet: then it waits for the morning after the record day.
        private void SplitNow(Stock s, int day)
        {
            int ratio = s.SplitRatio;
            if (!SplitRules.Known(ratio))
            {
                s.SplitDay = 0;
                s.SplitRatio = 0;
                return;
            }
            if (SplitRules.Waits(s))
            {
                return;
            }
            if (ratio > 0)
            {
                MarketLife.Split(State, s, ratio);
                SplitRules.AfterForward(State, s, ratio);
                SplitDone?.Invoke(s, ratio, 1, 0L);
                return;
            }
            string name = s.Name;
            bool mine = s.Own != null && Mine(s);
            SplitRules.Reverse(State, s, -ratio, mine, out ReverseResult r);
            // A part of a share paid for: into the account that holds the shares; the founder's own into their cash.
            if (r.Cash != 0)
            {
                if (State.Account.Margin)
                {
                    State.Account.Cash += r.Cash;
                }
                else if (r.Cash > 0)
                {
                    Wallet.Give(r.Cash, MarketMoney.Sold, name);
                }
                else
                {
                    Wallet.Force(-r.Cash, MarketMoney.Bought, name);
                }
            }
            if (r.FounderCash > 0 && mine)
            {
                Wallet.Give(r.FounderCash, MarketMoney.Sold, name);
            }
            if (r.BotCash > 0)
            {
                Wallet.Give(r.BotCash, MarketMoney.Sold, name);
            }
            if (r.Cancelled > 0)
            {
                for (int i = 0; i < State.Orders.Count; i++)
                {
                    Order o = State.Orders[i];
                    if (o.StockId == s.Id && o.IsOpen && o.Left <= 0)
                    {
                        o.State = OrderState.Cancelled;
                        o.Why = OrderWhy.Cancelled;
                        if (o.Worked)
                        {
                            o.EndDay = day;
                            Settle(o, name);
                        }
                    }
                }
            }
            SplitDone?.Invoke(s, 1, -ratio, r.Cash + (mine ? r.FounderCash : 0L) + r.BotCash);
        }

        // After the companies' own lives at a close: a board whose shares trade over its own level splits them, and a sound company long under a unit puts
        // its shares together. Decided only where this close is played for the first time on a market of its own; everywhere else the tape says it.
        private void ExtrasCorporate(int day)
        {
            if (!ExtrasOn || State.Following || Replaying)
            {
                return;
            }
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (SplitRules.WantsSplit(s))
                {
                    Event(MarketInputs.EventSplit, s.Id, MarketLife.SplitRatioAt(s.Last));
                }
                else if (SplitRules.WantsReverse(s))
                {
                    Event(MarketInputs.EventSplit, s.Id, -SplitRules.ReverseRatio(s.Last, State.Env.PriceLevel));
                }
            }
        }

        // A split from the tape: announced now, to take effect after the market's usual notice. A founder's own is tested again here, as every founder's action is.
        private void SplitEvent(in MarketEvent e)
        {
            Stock s = State.Stock(e.A);
            int ratio = e.B;
            if (s == null || !s.Listed || s.SplitDay > 0 || s.BidCents > 0 || !SplitRules.Known(ratio) || (s.Own != null && s.Own.Firm))
            {
                return;
            }
            if (s.Own != null && SplitRules.FounderWhy(s, ratio, State.Env.PriceLevel) != SplitWhy.None)
            {
                return;
            }
            s.SplitRatio = ratio;
            s.SplitDay = MarketTrading.MarketDays(State.Day, MarketCatalogue.SplitNoticeDays);
            Say(ratio > 0 ? new MarketNews { Day = State.Day, Kind = MarketNewsKind.Split, Stock = s.Id, A = ratio, B = s.SplitDay }
                : new MarketNews { Day = State.Day, Kind = MarketNewsKind.Reverse, Stock = s.Id, A = -ratio, B = s.SplitDay });
        }

        /// <summary>
        /// The founder asks their board to split the company's shares (over nothing: so many new for one) or put them together
        /// (under nothing: so many old for one new). Put on the tape for the next moment; None when it was.
        /// </summary>
        public SplitWhy AskSplit(int stockId, int ratio)
        {
            Finish();
            if (!ExtrasOn || State.Following)
            {
                return SplitWhy.Off;
            }
            Stock s = State.Stock(stockId);
            if (s == null || s.Own == null || !Mine(s))
            {
                return SplitWhy.NotMine;
            }
            SplitWhy why = SplitRules.FounderWhy(s, ratio, State.Env.PriceLevel);
            if (why == SplitWhy.None)
            {
                Event(MarketInputs.EventSplit, s.Id, ratio);
            }
            return why;
        }

        // ------------------------------------------------------------------------------------------------ the fund

        /// <summary>What units of the fund would come to now; nothing is changed.</summary>
        public FundDeal FundQuote(long units, bool buying)
        {
            // Units held can be sold with the option off: it stops what is new, never what is the player's.
            return FundRules.Quote(State, units, buying, Trading ? State.Phase : MarketCatalogue.PhaseOpen, _world.CommissionLeast, Wallet.Cash, State.Extras.Units,
                ExtrasOn || !buying, Trading && !Midway, Held);
        }

        /// <summary>
        /// Units of the fund bought or sold now, from and into the cash. The deal as it was made; its Why says why not. The
        /// money moves first: nothing is booked for a purchase the cash does not pay.
        /// </summary>
        public FundDeal FundTrade(long units, bool buying)
        {
            Finish();
            FundDeal d = FundQuote(units, buying);
            if (d.Why != FundWhy.None)
            {
                return d;
            }
            if (buying)
            {
                if (!Wallet.Ready(d.Total) || !Wallet.Take(d.Value, MarketMoney.Bought, ExtrasCatalogue.FundName))
                {
                    d.Why = FundWhy.NoMoney;
                    return d;
                }
                Wallet.Take(d.Fee, MarketMoney.Commission, ExtrasCatalogue.FundName);
            }
            else
            {
                Wallet.Give(d.Value, MarketMoney.Sold, ExtrasCatalogue.FundName);
                Wallet.Take(d.Fee, MarketMoney.Commission, ExtrasCatalogue.FundName);
            }
            // Each slice counts in the player's recent trading in its company, as an order of it would: the next deal pays for it.
            FundRules.Quote(State, units, buying, State.Phase, _world.CommissionLeast, long.MaxValue, buying ? 0L : long.MaxValue, true, true, false, true);
            State.Account.Commission += d.Fee;
            FundRules.Book(State.Extras, d, buying);
            return d;
        }

        // A close: the fund's fee for the day, the distributions its companies' record days owe the units held, and those due paid.
        private void ExtrasClose(int day)
        {
            FundRules.Close(State, day, TradingYear);
            long money = FundRules.Due(State.Extras, day);
            if (money > 0)
            {
                Wallet.Give(money, MarketMoney.Dividend, ExtrasCatalogue.FundName);
                State.Extras.Paid += money;
                State.Account.Dividends += money;
                FundPaid?.Invoke(money);
            }
            ExtrasCorporate(day);
        }

        // ------------------------------------------------------------------------------------------------ blocks

        /// <summary>What a block would come to now; nothing is changed.</summary>
        public BlockQuote BlockQuoteOf(int stockId, bool buying, long qty)
        {
            return BlockRules.Quote(State, State.Stock(stockId), buying, qty, Wallet.Cash, _world.CommissionLeast, Trading ? State.Phase : MarketCatalogue.PhaseOpen, ExtrasOn,
                Trading && !Midway, Held);
        }

        /// <summary>
        /// A block dealt now as one order at the market's price for its size: paid, booked and on the price as every fill is.
        /// The quote as it was dealt; its Why says why not. A holding that passed a 5% step is told to the market.
        /// </summary>
        public BlockQuote Block(int stockId, bool buying, long qty)
        {
            Finish();
            BlockQuote q = BlockQuoteOf(stockId, buying, qty);
            if (q.Why != BlockWhy.None)
            {
                return q;
            }
            Stock s = State.Stock(stockId);
            var order = new Order
            {
                Id = State.NextOrderId++, StockId = s.Id, Side = buying ? OrderSide.Buy : OrderSide.Sell, Type = OrderType.Market, Life = OrderLife.Day, Qty = qty, PlacedDay = State.Day,
                PlacedPhase = State.Phase, State = OrderState.Open, ExpireDay = State.Day
            };
            State.Orders.Add(order);
            if (!FillNow(s, order, qty, q.Cents, State.Phase))
            {
                q.Why = order.Why == OrderWhy.HoldingLimit ? BlockWhy.HoldingLimit : order.Why == OrderWhy.NoShares ? BlockWhy.NoShares
                    : order.Why == OrderWhy.BelowMargin ? BlockWhy.BelowMargin : BlockWhy.NoMoney;
                return q;
            }
            q.StakeBp = BlockRules.StakeBp(State.Held(s.Id), s.Shares);
            if (!order.Test && BlockRules.Disclose(State.Extras, s.Id, 0, q.StakeBp, out int step))
            {
                Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Block, Stock = s.Id, A = Math.Max(step, q.StakeBp), B = buying ? 1 : -1, C = 0 });
            }
            return q;
        }

        /// <summary>
        /// A rival tycoon's block of a company (the holder is their holding's stock id): its lasting move and its shares go on the tape as a trade does, for the next
        /// moment on every machine; a holding that passed a 5% step is told. False where the market is not this machine's own.
        /// </summary>
        public bool RivalBlock(int holderStock, int stockId, long qty, bool buying, int stakeBp)
        {
            Finish();
            Stock s = State.Stock(stockId);
            if (State.Following || s == null || !s.Listed || qty <= 0)
            {
                return false;
            }
            Event(MarketInputs.EventTrade, s.Id, BlockRules.Micros(State, s, qty, buying));
            Event(MarketInputs.EventVolume, s.Id, (int)Math.Min(int.MaxValue, qty));
            if (BlockRules.Disclose(State.Extras, s.Id, holderStock, stakeBp, out int step))
            {
                Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Block, Stock = s.Id, A = Math.Max(step, stakeBp), B = buying ? 1 : -1, C = holderStock });
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------ a bank of the player's

        /// <summary>The price the bank's book comes to this morning, drawn from the day's secret as a company's is; 0: called off.</summary>
        public int BankPrice(string name, int stockId, int standing)
        {
            Finish();
            Secret(State.Day);
            return BankFloatRules.Price(State.Extras.Bank, name, _secret, stockId, IndexMonthBp(), standing);
        }

        /// <summary>The id the bank's stock will have: the same hash a firm's has, moved to a free one.</summary>
        public int BankStockId()
        {
            return FreeId(ListingFirm.IdFor(State.Me, ExtrasCatalogue.BankPlace));
        }
    }
}
