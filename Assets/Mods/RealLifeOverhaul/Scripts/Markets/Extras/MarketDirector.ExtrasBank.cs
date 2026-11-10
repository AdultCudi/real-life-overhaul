using System;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the player's bank on its way to the market and on it, and the rival tycoons' day there.
    /// The bank's stage is looked at once a second (a comparison while nothing is due); its book is priced at the open of
    /// its day; the rivals look once a game day. Money moves only on a press or at the pricing: the retainer from the
    /// player's cash, the offer's money into the bank's capital. Nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private static long _bankFounder, _bankShares;   // the last stake seen while the market ran: what the bank's payouts are shared by when it does not
        private static object _bankSave;                 // the save that stake is of

        /// <summary>The bank's way to the market as the save keeps it; null while the market is not running.</summary>
        public BankFloat BankFloat => Running ? _engine.State.Extras.Bank : null;

        private static BankFacts BankNow()
        {
            Func<BankFacts> bank = ExtrasBridges.Bank;
            return bank != null ? bank() : default(BankFacts);
        }

        /// <summary>True while a bank of the player's own runs.</summary>
        public bool BankRuns => BankNow().Running;

        /// <summary>The bank's own stock while it trades, or null.</summary>
        public Stock BankStock => Running ? _engine.FirmByPlace(ExtrasCatalogue.BankPlace) : null;

        /// <summary>Why the bank cannot engage advisers now (None: it can), and what the market's model makes it worth on its own books.</summary>
        public BankWhy BankWhyNow(out double worth)
        {
            worth = 0.0;
            if (!Running)
            {
                return BankWhy.Off;
            }
            MarketState s = _engine.State;
            BankFacts f = BankNow();
            BankWhy why = BankFloatRules.Ready(s.Extras.Bank, f, Save.Day, Year, ExtrasOpen && FirmsOpen, MyCompany != null, s.Listing.OnTheWay);
            if (f.Running)
            {
                worth = BankFloatRules.Worth(s.Extras.Bank, f, s.Me, Save.Day, Year, s.Env);
            }
            return why == BankWhy.None && worth <= 0.0 ? BankWhy.Profit : why;
        }

        /// <summary>The retainer the advisers ask for the bank now.</summary>
        public long BankRetainer(double worth)
        {
            return Running ? ListingBook.Retainer(worth, Math.Max(100, _engine.State.Env.PriceLevel) / 1000.0) : 0L;
        }

        /// <summary>
        /// The advisers engaged for the bank: the prospectus is written on its own books, the retainer is paid from the player's cash, and the book is
        /// priced a few market days on. Every test is made before any money moves; None when done.
        /// </summary>
        public BankWhy BankEngage(int floatBp)
        {
            FinishMoment();
            BankWhy why = BankWhyNow(out double worth);
            if (why != BankWhy.None)
            {
                return why;
            }
            if (Array.IndexOf(ExtrasCatalogue.BankFloatSteps, floatBp) < 0)
            {
                return BankWhy.Float;
            }
            MarketState s = _engine.State;
            BankFacts f = BankNow();
            long retainer = BankRetainer(worth);
            if (!_engine.Wallet.Take(retainer, ListingMoney.Advisers, f.Name))
            {
                return BankWhy.NoMoney;
            }
            BankFloat b = s.Extras.Bank;
            BankFloatRules.Prospectus(b, f, floatBp, s.Me, Save.Day, Year, s.Env);
            b.Stage = BankStage.Advised;
            b.AdvisedDay = Save.Day;
            b.PriceDay = MarketTrading.MarketDays(s.Day, ListingCatalogue.BookDays);
            b.PriceCents = 0;
            b.Raised = 0;
            b.Fees = retainer;
            b.StockId = 0;
            Queue(NotificationType.Info, "reallife_stx_note_bank_advised", "bank", f.Name, "fee", MarketText.Money(retainer), "day", b.PriceDay.ToString(),
                "low", MarketText.Price(b.LowCents),
                "high", MarketText.Price(b.HighCents));
            Logger?.Info("Stock market: advisers engaged for the bank " + f.Name + " on day " + Save.Day + " for " + retainer + " (worth " + (long)worth + " on its own books, capital "
                         + f.CapitalCents / 100 + "): " + b.NewShares + " new shares beside the founder's " + b.OldShares + ", " + b.LowCents + " to " + b.HighCents + " cents, priced on day "
                         + b.PriceDay + ".");
            Moved();
            return BankWhy.None;
        }

        /// <summary>The bank's offer withdrawn before it is priced: the retainer is spent. True if there was one.</summary>
        public bool BankWithdraw()
        {
            BankFloat b = BankFloat;
            if (b == null || b.Stage != BankStage.Advised)
            {
                return false;
            }
            b.Stage = BankStage.None;
            Logger?.Info("Stock market: the bank's offer withdrawn on day " + Save.Day + "; the retainer of " + b.Fees + " is spent.");
            Moved();
            return true;
        }

        // Once a second: the book priced at the open of its day, a bank that is gone taken off the market, and the stake its payouts are shared by.
        private void BankSecond()
        {
            MarketState s = _engine.State;
            BankFloat b = s.Extras.Bank;
            if (b.Stage == BankStage.Advised && !Behind && !_engine.Held && (s.Day > b.PriceDay || (s.Day == b.PriceDay && s.Phase >= MarketCatalogue.PhaseOpen)))
            {
                BankPriced(s, b);
            }
            if (b.Stage != BankStage.Listed)
            {
                _bankSave = null;
                return;
            }
            Stock k = _engine.FirmByPlace(ExtrasCatalogue.BankPlace);
            if (k == null)
            {
                if (!_following && string.IsNullOrEmpty(s.Listing.Suspended) && s.Listing.SuspendedFirms.Count == 0)
                {
                    b.Stage = BankStage.Ended;   // its stock is gone (swept, or the market made anew)
                }
                return;
            }
            _bankFounder = k.Own.Founder;
            _bankShares = k.Shares;
            _bankSave = _save;
            BankFacts f = BankNow();
            if (f.Gone && !_following)
            {
                // Sold or closed: each share is paid its part of what the bank fetched or left, as the owner was by theirs.
                int each = (int)Math.Max(0L, Math.Min(int.MaxValue, f.CapitalCents / Math.Max(1L, k.Shares)));
                EndFirm(k.Id, each > 0 ? GoneReason.TakenOver : GoneReason.Failed, each);
                b.Stage = BankStage.Ended;
                _bankSave = null;
                Queue(NotificationType.Info, "reallife_stx_note_bank_ended", "bank", k.Name);
                Logger?.Info("Stock market: the bank " + k.Name + " is gone; its stock left the market paying " + each + " cents a share.");
            }
        }

        // The pricing day's open: the book is drawn as a company's is; the offer's money goes into the bank's capital and the bank becomes a stock of its own.
        private void BankPriced(MarketState s, BankFloat b)
        {
            BankFacts f = BankNow();
            double level = Math.Max(100, s.Env.PriceLevel) / 1000.0;
            bool can = f.Running && f.Clear && !f.Test && ExtrasOpen && FirmsOpen && MyCompany == null && !s.Listing.OnTheWay && ExtrasBridges.BankCapital != null;
            int id = can ? _engine.BankStockId() : 0;
            Func<int> standing = ExtrasBridges.Standing;
            int price = can ? _engine.BankPrice(f.Name, id, standing != null ? standing() : -1) : 0;
            if (price <= 0)
            {
                b.Stage = BankStage.CalledOff;
                Queue(NotificationType.Warning, "reallife_stx_note_bank_called_off", "bank", f.Name ?? string.Empty);
                Logger?.Info("Stock market: the bank's offer was called off on day " + Save.Day
                             + (can ? ": the book came in under " + b.LowCents + " cents" : ": the bank or the market could not go on")
                             + "; the retainer is spent.");
                Moved();
                return;
            }
            b.PriceCents = price;
            long raised = BankFloatRules.Raised(b, price, level, out long underwriting, out long exchange);
            Stock k = ListFirm(BankFloatRules.Listing(b, f.Name, s.Day, Year), out FirmWhy why);
            if (k == null || why != FirmWhy.None)
            {
                b.Stage = BankStage.CalledOff;
                Queue(NotificationType.Warning, "reallife_stx_note_bank_called_off", "bank", f.Name ?? string.Empty);
                Logger?.Info("Stock market: the bank's offer was called off on day " + Save.Day + ": the market could not take it (" + why + ").");
                Moved();
                return;
            }
            if (!ExtrasBridges.BankCapital(raised * 100L))
            {
                EndFirm(k.Id, GoneReason.Delisted, 0);   // the bank would not take the money: no offer was made, and nobody holds a share
                b.Stage = BankStage.CalledOff;
                Moved();
                return;
            }
            b.Stage = BankStage.Listed;
            b.StockId = k.Id;
            b.ListDay = s.Day;
            b.Raised = raised;
            b.Fees += underwriting + exchange;
            Queue(NotificationType.Success, "reallife_stx_note_bank_listed", "bank", k.Name, "price", MarketText.Price(price), "amount", MarketText.Money(raised),
                "stake", MarketText.Percent(k.Own.StakeBp(k)));
            Logger?.Info("Stock market: the bank " + k.Name + " (" + k.Ticker + ") listed on day " + s.Day + " at " + price + " cents: " + b.NewShares + " new shares raised " + raised
                         + " into its capital after " + underwriting + " to the underwriters and " + exchange + " to the exchange; the founder keeps " + b.OldShares + " shares.");
            Changes();
        }

        /// <summary>A day of the bank's own books, as the bank booked it (cents): kept for the market's mornings and its quarters.</summary>
        public void BankBooked(int day, long incomeCents, long profitCents)
        {
            if (Running)
            {
                BankFloatRules.AddDay(_engine.State.Extras.Bank, day, incomeCents, profitCents, Year);
            }
        }

        /// <summary>The bank's takings and profit over the days from one up to, not with, another, for the market's morning; nothing while it is not listed.</summary>
        public void BankDays(int from, int to, out long revenue, out long profit)
        {
            revenue = profit = 0;
            if (Running)
            {
                BankFloatRules.Sum(_engine.State.Extras.Bank, from, to, out revenue, out profit);
            }
        }

        /// <summary>True while the bank trades as a stock of its own: its days are then no part of the whole company's books.</summary>
        public bool BankListed => Running && _engine.State.Extras.Bank.Stage == BankStage.Listed;

        /// <summary>
        /// Of a sum the bank pays its owner (cents), the owner's part: all of it until the bank's shares trade, then by the founder's shares. With the market
        /// switched off the last stake seen stands, and a save loaded that way reads it from its own line, so switching the market off shares nothing less.
        /// </summary>
        public static long BankOwnerPart(long cents)
        {
            MarketDirector d = Instance;
            GameInstance save = Save;
            if (cents <= 0 || save == null)
            {
                return Math.Max(0L, cents);
            }
            if (d != null && d.Running)
            {
                BankFloat b = d._engine.State.Extras.Bank;
                Stock k = b.Stage == BankStage.Listed ? d._engine.FirmByPlace(ExtrasCatalogue.BankPlace) : null;
                if (k == null)
                {
                    return cents;
                }
                long part = BankFloatRules.OwnerPart(cents, k.Own.Founder, k.Shares);
                b.PaidOthers += cents - part;
                return part;
            }
            if (!ReferenceEquals(_bankSave, save))
            {
                // The market is not running: what its line holds of the bank's stock (read once a save).
                _bankSave = save;
                _bankFounder = _bankShares = 0;
                MarketState kept = MarketStore.Load(save.modData, out int _);
                if (kept != null)
                {
                    ExtrasStore.Load(save.modData, kept);
                    for (int i = 0; kept.Extras.Bank.Stage == BankStage.Listed && i < kept.Stocks.Count; i++)
                    {
                        Stock k = kept.Stocks[i];
                        if (k.Listed && k.Own != null && k.Own.Firm && BankFloatRules.IsBank(k.Own.Place))
                        {
                            _bankFounder = k.Own.Founder;
                            _bankShares = k.Shares;
                        }
                    }
                }
            }
            return _bankShares > 0 ? BankFloatRules.OwnerPart(cents, _bankFounder, _bankShares) : cents;
        }

        // The founder dealt in the bank's shares: the tax return follows, with what the bank was founded with as the shares' cost. True if the stock is the bank's.
        private bool BankDealt(int stockId, long shares, long money, long fee)
        {
            Stock k = _engine.State.Stock(stockId);
            if (k == null || k.Own == null || !BankFloatRules.IsBank(k.Own.Place))
            {
                return false;
            }
            Func<long> paidIn = ExtrasBridges.BankPaidIn;
            ExtrasBridges.Dealt?.Invoke(ExtrasCatalogue.BankPlace, k.Name, shares, money, fee, k.Own.Founder - shares, paidIn != null ? paidIn() : 0L);
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the rival tycoons

        // Once a game day, on a market of this machine's own: each tycoon who is forward, on their own day of the month, looks at the market.
        private void RivalsDay(int day)
        {
            RivalLook look = ExtrasBridges.Rival;
            if (look == null)
            {
                return;
            }
            MarketState s = _engine.State;
            bool changed = false;
            for (int t = 0; t < s.Extras.Rivals.Length; t++)
            {
                RivalBook book = s.Extras.Rivals[t];
                if (!look(t, out long worth, out int holderStock, out string holding, out string name, out bool listed))
                {
                    continue;
                }
                long swept = RivalMarket.Sweep(s, book);
                if (swept != 0)
                {
                    ExtrasBridges.RivalGain?.Invoke(t, swept);
                    changed = true;
                }
                if (!RivalMarket.Looks(t, day, book))
                {
                    continue;
                }
                book.LookDay = day;
                changed = true;
                long units = RivalMarket.FundUnits(s, book, worth);
                if (units > 0)
                {
                    book.Units += units;
                    book.UnitsCost += (long)(units * FundRules.Nav(s) / 100.0);
                }
                string dealt = string.Empty;
                if (RivalMarket.Sells(s, t, day, book, out int at))
                {
                    RivalStake stake = book.Stakes[at];
                    Stock k = s.Stock(stake.Stock);
                    if (k != null && k.Listed)
                    {
                        long paid = MarketTrading.Value(stake.Shares, RivalMarket.Sold(s, k, stake.Shares), false);
                        ExtrasBridges.RivalGain?.Invoke(t, paid - stake.Cost);
                        _engine.RivalBlock(holderStock, k.Id, stake.Shares, false, 0);
                        dealt = "; sold " + stake.Shares + " of " + k.Name + " for " + paid + " (cost " + stake.Cost + ")";
                    }
                    book.Stakes.RemoveAt(at);
                }
                else
                {
                    dealt = RivalBuys(s, t, book, holderStock, RivalMarket.Pick(s, t, holderStock, day, book), worth * ExtrasCatalogue.RivalBlockBp / 10000L);
                }
                string arm = string.Empty;
                if (RivalMarket.ListsArm(s, t, day, book, listed, out int kind))
                {
                    string armName = RivalMarket.ArmName(holding, kind);
                    Stock listedArm = _engine.ListStartup(RivalMarket.Arm(armName, kind, worth, s.Env.PriceLevel), out FirmWhy why);
                    if (listedArm != null && why == FirmWhy.None)
                    {
                        book.ArmStock = listedArm.Id;
                        book.ArmKind = kind;
                        book.ArmDay = day;
                        arm = "; listed " + listedArm.Name + " (" + listedArm.Ticker + ") at " + listedArm.Last + " cents";
                    }
                }
                Logger?.Info("Stock market: " + name + " looked at the market on day " + day + ": " + units + " unit(s) of the fund bought (" + book.Units + " held)" + dealt + arm + ".");
            }
            if (changed)
            {
                Changes();
            }
        }

        // A tycoon's block of a company bought with so much money: within what they may hold of it, on the tape as a trade is. What was done, for the log.
        private string RivalBuys(MarketState s, int tycoon, RivalBook book, int holderStock, int stock, long money)
        {
            Stock k = stock > 0 ? s.Stock(stock) : null;
            long shares = RivalMarket.BlockShares(s, k, money, out int cents);
            if (k == null || shares <= 0 || book.Stakes.Count >= ExtrasCatalogue.RivalStakesMost)
            {
                return string.Empty;
            }
            long cost = MarketTrading.Value(shares, cents, true);
            book.Stakes.Add(new RivalStake { Stock = k.Id, Shares = shares, Cost = cost, Day = Save.Day });
            _engine.RivalBlock(holderStock, k.Id, shares, true, BlockRules.StakeBp(shares, k.Shares));
            return "; bought " + shares + " of " + k.Name + " at " + cents + " cents for " + cost;
        }

        /// <summary>A rival's announced purchase of shares comes (the rivals' own move): a block of a company for the sum, by the same rule as their monthly look. True if one was bought.</summary>
        public bool RivalShares(int tycoon, long amount)
        {
            RivalLook look = ExtrasBridges.Rival;
            if (!ExtrasOpen || look == null || tycoon < 0 || tycoon >= _engine.State.Extras.Rivals.Length)
            {
                return false;
            }
            FinishMoment();
            MarketState s = _engine.State;
            if (!look(tycoon, out long _, out int holderStock, out string _, out string name, out bool _))
            {
                return false;
            }
            RivalBook book = s.Extras.Rivals[tycoon];
            string dealt = RivalBuys(s, tycoon, book, holderStock, RivalMarket.Pick(s, tycoon, holderStock, Save.Day, book), amount);
            if (dealt.Length == 0)
            {
                return false;
            }
            Logger?.Info("Stock market: " + name + "'s purchase of shares came on day " + Save.Day + dealt + ".");
            Changes();
            return true;
        }

        /// <summary>A line on what a tycoon holds on the market beside their holding, for the rich list and their panel; empty for nothing.</summary>
        public string RivalMarketLine(int tycoon)
        {
            if (!Running || tycoon < 0 || tycoon >= _engine.State.Extras.Rivals.Length)
            {
                return string.Empty;
            }
            MarketState s = _engine.State;
            RivalBook book = s.Extras.Rivals[tycoon];
            long worth = RivalMarket.Worth(s, book);
            Stock arm = book.ArmStock > 0 ? s.Stock(book.ArmStock) : null;
            if (worth <= 0 && arm == null)
            {
                return string.Empty;
            }
            string line = worth > 0 ? MarketText.Fill("reallife_stx_rival_line", "worth", MarketText.Money(worth), "blocks", book.Stakes.Count.ToString(), "units", book.Units.ToString("N0"))
                : string.Empty;
            return arm != null ? (line.Length > 0 ? line + " " : string.Empty) + MarketText.Fill("reallife_stx_rival_arm", "name", arm.Name) : line;
        }
    }
}
