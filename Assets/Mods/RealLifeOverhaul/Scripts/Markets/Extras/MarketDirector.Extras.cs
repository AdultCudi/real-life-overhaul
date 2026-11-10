using System;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the stock market's extras on the game's side. The option followed and the bank's and the
    /// rivals' day looked at on the director's one-second timer (a few comparisons while nothing is due); what the Stocks app
    /// asks (units of the fund, a block, the founder's split, the bank's way to the market); the notifications and the tax
    /// on what the fund pays. Nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private int _extrasDay = -1;
        private bool _extrasTold;

        /// <summary>True while the extras may change the market from this machine: the option on and the market its own.</summary>
        public bool ExtrasOpen => Running && Settings.MarketsExtras && !_following && !GoingPublic.IsGuest && !_engine.State.Following;

        private void HookExtras()
        {
            _engine.ExtrasOn = Settings.MarketsExtras;
            _engine.FundPaid = FundPaidHeard;
            _engine.SplitDone = SplitHeard;
            _extrasDay = -1;
        }

        // Once a second: the option, then on a new day the bank's way to the market and the rivals' look at it.
        private void ExtrasSecond()
        {
            if (_engine.ExtrasOn != Settings.MarketsExtras)
            {
                _engine.ExtrasOn = Settings.MarketsExtras;
                if (!_extrasTold || Verbose)
                {
                    _extrasTold = true;
                    Logger?.Info("Stock market: the extras are " + (Settings.MarketsExtras ? "on" : "off: no new split is decided here, no fund unit or block is dealt; what is held is kept") + ".");
                }
                Moved();
            }
            BankSecond();   // a comparison while nothing is due (MarketDirector.ExtrasBank.cs)
            GameInstance save = Save;
            if (save == null || save.Day == _extrasDay || Behind || _engine.Held)
            {
                return;
            }
            _extrasDay = save.Day;
            if (ExtrasOpen)
            {
                RivalsDay(save.Day);
            }
        }

        // ------------------------------------------------------------------------------------------------ the fund

        public FundDeal FundQuote(long units, bool buying)
        {
            return Running ? _engine.FundQuote(units, buying) : new FundDeal { Why = FundWhy.Off };
        }

        /// <summary>Units of the fund bought or sold now; the deal's Why says why not. Told with a notification.</summary>
        public FundDeal FundTrade(long units, bool buying)
        {
            FinishMoment();
            if (!Running || Behind)
            {
                return new FundDeal { Why = Running ? FundWhy.Closed : FundWhy.Off };
            }
            try
            {
                FundDeal d = _engine.FundTrade(units, buying);
                if (d.Why == FundWhy.None)
                {
                    Queue(NotificationType.Success, buying ? "reallife_stx_note_fund_bought" : "reallife_stx_note_fund_sold", "units", d.Units.ToString("N0"), "price", MarketText.Price(d.PriceCents),
                        "total", MarketText.Money(d.Total));
                    if (Verbose)
                    {
                        Logger?.Info("Stock market check (fund): " + (buying ? "bought " : "sold ") + d.Units + " unit(s) at " + d.PriceCents + " cents against " + d.NavCents
                                     + " held by a unit (" + d.CostBp + " bp), " + d.Total + " with a commission of " + d.Fee + "; "
                                     + ((buying ? d.PriceCents >= d.NavCents : d.PriceCents <= d.NavCents) ? "PASS" : "DEALT BETTER THAN WHAT A UNIT HOLDS"));
                    }
                }
                Moved();
                return d;
            }
            catch (Exception e)
            {
                Fault("a deal in the fund", e);
                return new FundDeal { Why = FundWhy.Off };
            }
        }

        // A distribution of the fund paid: taxed as a dividend is, and told.
        private void FundPaidHeard(long money)
        {
            MarketState s = _engine.State;
            long tax = ListingVotes.DividendTax(money, s.Env.Lever(MarketInputs.BusinessTaxLever));
            if (tax > 0)
            {
                _engine.Wallet.Force(tax, ListingMoney.DividendTax, ExtrasCatalogue.FundName);
            }
            if (Settings.MarketsDividendNotes)
            {
                Queue(NotificationType.Success, "reallife_stx_note_fund_paid", "amount", MarketText.Money(money), "tax", MarketText.Money(tax));
            }
            if (Verbose)
            {
                Logger?.Info("Stock market check (fund distribution, day " + s.Day + "): " + money + " paid on " + s.Extras.Units + " unit(s), " + tax + " withheld: PASS");
            }
        }

        // ------------------------------------------------------------------------------------------------ blocks

        public BlockQuote BlockQuote(int stockId, bool buying, long qty)
        {
            return Running ? _engine.BlockQuoteOf(stockId, buying, qty) : new BlockQuote { Why = BlockWhy.Off };
        }

        /// <summary>A block dealt now; the quote's Why says why not. Told with a notification, and once more when it brings the most one holder may have.</summary>
        public BlockQuote Block(int stockId, bool buying, long qty)
        {
            FinishMoment();
            if (!Running || Behind)
            {
                return new BlockQuote { Why = Running ? BlockWhy.Closed : BlockWhy.Off };
            }
            if (!Settings.MarketsMargin && _engine.State.Account.Margin && !Closes(stockId, buying ? OrderSide.Buy : OrderSide.Sell, qty))
            {
                return new BlockQuote { Why = BlockWhy.BelowMargin };   // with margin trading off an open account is only wound down
            }
            try
            {
                BlockQuote q = _engine.Block(stockId, buying, qty);
                Stock k = _engine.State.Stock(stockId);
                if (q.Why == BlockWhy.None && k != null)
                {
                    if (buying && _engine.State.Held(stockId) >= MarketTrading.MostHeld(k))
                    {
                        Queue(NotificationType.Info, "reallife_stx_note_block_most", "name", k.Name);
                    }
                    if (Verbose)
                    {
                        Logger?.Info("Stock market check (block): " + (buying ? "bought " : "sold ") + qty + " of " + k.Name + " at " + q.Cents + " cents (" + q.ImpactBp
                                     + " bp against the block), holding " + q.StakeBp + " bp of at most " + MarketTrading.MostHeldBp(k) + ": "
                                     + (q.StakeBp <= MarketTrading.MostHeldBp(k) ? "PASS" : "PAST THE MOST"));
                    }
                }
                Moved();
                return q;
            }
            catch (Exception e)
            {
                Fault("a block", e);
                return new BlockQuote { Why = BlockWhy.Off };
            }
        }

        // ------------------------------------------------------------------------------------------------ splits

        /// <summary>Why the founder cannot ask their board for a split of this ratio now (None: they can).</summary>
        public SplitWhy SplitWhyNow(int ratio)
        {
            Stock own = MyCompany;
            if (!ExtrasOpen)
            {
                return SplitWhy.Off;
            }
            return own == null ? SplitWhy.NotMine : SplitRules.FounderWhy(own, ratio, _engine.State.Env.PriceLevel);
        }

        /// <summary>The founder's split asked of the board: on the tape for the market's next moment, announced then with the market's usual notice.</summary>
        public SplitWhy AskSplit(int ratio)
        {
            FinishMoment();
            SplitWhy why = SplitWhyNow(ratio);
            if (why != SplitWhy.None)
            {
                return why;
            }
            Stock own = MyCompany;
            why = _engine.AskSplit(own.Id, ratio);
            if (why == SplitWhy.None)
            {
                Logger?.Info("Stock market: the founder asked the board of " + own.Name + " for " + (ratio > 0 ? ratio + " new shares for one" : "one new share for " + -ratio) + " at "
                             + own.Last + " cents; announced at the market's next moment.");
                Moved();
            }
            return why;
        }

        // A split took effect: the player is told if they hold, watch or founded the company.
        private void SplitHeard(Stock s, int fresh, int old, long cash)
        {
            MarketState state = _engine.State;
            bool mine = s.Own != null && _engine.Mine(s);
            bool cares = mine || state.Held(s.Id) != 0 || state.Watching(s.Id) || MarketBots.Held(state, s.Id) > 0;
            if (cares)
            {
                if (old > fresh)
                {
                    Queue(NotificationType.Info, "reallife_stx_note_reverse_done", "name", s.Name, "ratio", old.ToString(), "amount", MarketText.Money(cash));
                }
                else
                {
                    Queue(NotificationType.Info, "reallife_stx_note_split_done", "name", s.Name, "ratio", fresh.ToString());
                }
            }
            if (Verbose)
            {
                Logger?.Info("Stock market check (split, day " + state.Day + "): " + s.Name + " " + fresh + " for " + old + ", now " + s.Shares + " shares at " + s.Last
                             + " cents; the player holds " + state.Held(s.Id) + (cash != 0 ? ", " + cash + " paid for parts of a share" : string.Empty) + ": PASS");
            }
        }

        // ------------------------------------------------------------------------------------------------ the tests

        /// <summary>The test tools: a company's shares are split, or put together, at the next open with nothing asked. False if it cannot be.</summary>
        public bool TestSplit(int stockId, int ratio)
        {
            FinishMoment();
            Stock s = Running && !_following ? _engine.State.Stock(stockId) : null;
            if (s == null || !s.Listed || s.SplitDay > 0 || !SplitRules.Known(ratio) || (s.Own != null && s.Own.Firm))
            {
                return false;
            }
            s.SplitRatio = ratio;
            s.SplitDay = MarketTrading.NextTradingDay(_engine.State.Day);
            Changes();
            return true;
        }
    }
}
