using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The transaction keys of the futures' and options' money; "shares_" files them under investments in the Finance app.</summary>
    public static class DerivMoney
    {
        public const string Margin = "reallife_transaction_shares_future_margin";   // paid into the margin account for futures
        public const string Back = "reallife_transaction_shares_future_back";       // paid back out of it: a day's gain, a margin no longer needed
        public const string Bought = "reallife_transaction_shares_option_bought";
        public const string Sold = "reallife_transaction_shares_option_sold";
        public const string Paid = "reallife_transaction_shares_option_paid";       // an option in the money at its day
        public const string Goods = "reallife_transaction_future_importdelivery";   // a future's goods delivered: a cost of stock for the place
    }

    /// <summary>
    /// Part of the engine: the raw materials exchange and the player's futures and options. Each market close sets every
    /// material's price from the day's secret, marks the futures to their new fair prices with the day's money settled in
    /// the margin account, settles what is due (in cash, or by delivery for the director to do), and keeps the margin by
    /// the broker's rules (MarketCatalogue: a call for the next close, the broker closing at the close it is unmet, or at
    /// once far under). A look at each contract at a close and a few dozen operations a material; nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>Told once the frame's work is done: settlements, deliveries to make, calls.</summary>
        public Action<DerivNote> DerivTold;

        /// <summary>A contract's name for the game's transactions, in the language of the moment; null in the checks.</summary>
        public Func<DerivContract, string> ContractName;

        /// <summary>Settings.MarketsDerivatives, set by the director; off, nothing new is bought, what is held runs to its day.</summary>
        public bool DerivsOn = true;

        /// <summary>The city's economy writes the exchange's prices as the importers'; without it no new contract on a material.</summary>
        public bool MaterialsLive = true;

        /// <summary>A future may be delivered: not in a Going Public session, where the goods in a warehouse are the session's to move.</summary>
        public bool Deliveries = true;

        private readonly List<DerivContract> _forcing = new List<DerivContract>(8);

        private DerivBook Book => State.Derivs;

        private string NameOf(DerivContract c)
        {
            return ContractName != null ? ContractName(c) ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// The materials the game has, with their own prices: new ones join at their first price; a Going Public guest keeps
        /// the host's own prices for those it already has. How many joined.
        /// </summary>
        public int KnowMaterials(IList<string> names, IList<long> bases, bool keepBases = false)
        {
            Finish();
            return MaterialRules.Know(State.Materials, names, bases, State.Id, State.Env.PriceLevel, State.Day, keepBases);
        }

        public RawMaterial MaterialOf(string name)
        {
            return MaterialRules.Find(State.Materials, name);
        }

        /// <summary>The days the contracts on offer settle at, the nearest first; how many.</summary>
        public int ContractDays(int[] into)
        {
            return DerivRules.Days(State.Day, State.Phase, into);
        }

        // Micros a unit: a material's price expected at a day's close, from a moment of today (its phase).
        private long MaterialForward(RawMaterial m, int settleDay, int phase)
        {
            int closes = DerivRules.Closes(State.Day, phase, settleDay);
            return closes <= 0 ? m.Fix : MaterialRules.Forward(m, closes, State.Env.PriceLevel, State.Env.InflationBp, settleDay - State.Day, Year);
        }

        /// <summary>A future on a material for a day, as it is offered now, for so many units.</summary>
        public DerivQuote QuoteFuture(string material, int settleDay, bool buying, long qty)
        {
            var q = new DerivQuote();
            RawMaterial m = MaterialOf(material);
            if (m == null)
            {
                q.Why = DerivWhy.Unknown;
                return q;
            }
            q.Fair = q.Forward = MaterialForward(m, settleDay, State.Phase);
            DerivRules.FutureSides(q.Fair, out q.Ask, out q.Bid);
            q.Value = DerivRules.Money(qty, buying ? q.Ask : q.Bid, true);
            q.Margin = DerivRules.MarginMoney(q.Value, m.Group);
            q.Fee = MarketTrading.Commission(q.Value, _world.CommissionLeast);
            q.Spread = MaterialRules.Spread(m.Group, DerivRules.Closes(State.Day, State.Phase, settleDay));
            return q;
        }

        /// <summary>A call or a put on a material (by name) or a company's shares (by id), at a strike step from the price now.</summary>
        public DerivQuote QuoteOption(bool call, string material, int stockId, int settleDay, int strikeStepBp, long qty)
        {
            var q = new DerivQuote();
            long under;
            double fair;
            if (stockId > 0)
            {
                Stock s = State.Stock(stockId);
                q.Why = DerivRules.ShareOffered(s, State.Day);
                if (q.Why != DerivWhy.None)
                {
                    return q;
                }
                under = (long)Math.Round(s.Mid * DerivCatalogue.Micro);
                q.Strike = DerivRules.Strike(under, strikeStepBp);
                fair = OptionRules.ShareFair(State, s, settleDay, call, q.Strike / (double)DerivCatalogue.Micro, out double forward, out q.Spread) * DerivCatalogue.Micro;
                q.Forward = (long)Math.Round(forward * DerivCatalogue.Micro);
            }
            else
            {
                RawMaterial m = MaterialOf(material);
                if (m == null)
                {
                    q.Why = DerivWhy.Unknown;
                    return q;
                }
                int closes = DerivRules.Closes(State.Day, State.Phase, settleDay);
                q.Forward = MaterialForward(m, settleDay, State.Phase);
                under = q.Forward;
                q.Strike = DerivRules.Strike(q.Forward, strikeStepBp);
                MaterialRules.Ahead(m.X, m.Group, closes, out _, out double variance);
                fair = OptionRules.Black(call, q.Forward, q.Strike, variance);
                q.Spread = Math.Sqrt(variance);
            }
            q.Fair = (long)Math.Round(fair);
            DerivRules.OptionSides(fair, under, out q.Ask, out q.Bid);
            q.Value = DerivRules.Money(qty, q.Ask, true);
            q.Fee = MarketTrading.Commission(q.Value, _world.CommissionLeast);
            return q;
        }

        // Whether anything may be traded now: in a session, not held after a load.
        private DerivWhy TradeWhy(bool open)
        {
            return Held ? DerivWhy.Held : !open ? DerivWhy.Closed : DerivWhy.None;
        }

        /// <summary>Whether a new contract can be bought now as quoted; None when it can.</summary>
        public DerivWhy CanOpen(ContractKind kind, string material, int stockId, int settleDay, long qty, in DerivQuote q, bool open)
        {
            return CanOpen(kind, material, stockId, settleDay, qty, q, open, false);
        }

        // The test tools buy past the hold after a load (their contracts are put back by UndoTests); nothing else does.
        private DerivWhy CanOpen(ContractKind kind, string material, int stockId, int settleDay, long qty, in DerivQuote q, bool open, bool pastHold)
        {
            DerivWhy why = pastHold ? (open ? DerivWhy.None : DerivWhy.Closed) : TradeWhy(open);
            if (why != DerivWhy.None)
            {
                return why;
            }
            if (!DerivsOn)
            {
                return DerivWhy.Off;
            }
            if (q.Why != DerivWhy.None)
            {
                return q.Why;
            }
            bool future = kind == ContractKind.Long || kind == ContractKind.Short;
            if (stockId <= 0 && !MaterialsLive && !DerivCatalogue.IsBuilding(material))
            {
                return DerivWhy.NoCity;   // the exchange's own building materials are priced for the builders, not the importers
            }
            if ((future && stockId > 0) || qty <= 0 || !DerivRules.Offered(State.Day, State.Phase, settleDay))
            {
                return DerivWhy.NotOffered;
            }
            if (DerivRules.OpenCount(Book) >= DerivCatalogue.ContractsMost)
            {
                return DerivWhy.TooMany;
            }
            if (stockId > 0)
            {
                Stock s = State.Stock(stockId);
                if (s == null || DerivRules.SharesOn(Book, stockId) + qty > DerivRules.SharesMost(s))
                {
                    return DerivWhy.TooMuch;
                }
            }
            else if (DerivRules.UnitsOn(Book, material) + qty > DerivCatalogue.MaterialUnitsMost)
            {
                return DerivWhy.TooMuch;
            }
            long needs = (future ? q.Margin : q.Value) + q.Fee;
            return needs > Wallet.Cash ? DerivWhy.NoCash : DerivWhy.None;
        }

        /// <summary>
        /// A future or an option bought now at the price offered: a future's margin and the commission, or an option's
        /// premium and the commission, taken from the cash. Null with the reason when it cannot be. A long future names a
        /// warehouse (the game's address and its name) to deliver to, or none for cash.
        /// </summary>
        public DerivContract OpenContract(ContractKind kind, string material, int stockId, int settleDay, int strikeStepBp, long qty, string warehouse, string place, bool open, bool test,
            out DerivWhy why, bool pastHold = false)
        {
            Finish();
            bool future = kind == ContractKind.Long || kind == ContractKind.Short;
            if (future)
            {
                stockId = 0;
            }
            DerivQuote q = future ? QuoteFuture(material, settleDay, kind == ContractKind.Long, qty)
                : QuoteOption(kind == ContractKind.Call, stockId > 0 ? null : material, stockId, settleDay, strikeStepBp, qty);
            why = CanOpen(kind, material, stockId, settleDay, qty, q, open, pastHold && test);
            if (why != DerivWhy.None)
            {
                return null;
            }
            var c = new DerivContract
            {
                Id = Book.NextId, Kind = kind, Material = stockId > 0 ? string.Empty : material ?? string.Empty, StockId = stockId > 0 ? stockId : 0, Qty = qty, OpenedDay = State.Day,
                SettleDay = settleDay, Test = test
            };
            string name = NameOf(c);
            if (future)
            {
                c.Price = c.Mark = kind == ContractKind.Long ? q.Ask : q.Bid;
                c.Margin = q.Margin * 100L;
                if (kind == ContractKind.Long && !string.IsNullOrEmpty(warehouse) && !DerivCatalogue.IsBuilding(material))
                {
                    c.Warehouse = warehouse;
                    c.PlaceName = place ?? string.Empty;
                }
                if (!Wallet.Ready(q.Margin + q.Fee) || !Wallet.Take(q.Margin, DerivMoney.Margin, name))
                {
                    why = DerivWhy.NoCash;
                    return null;
                }
                Book.Balance += q.Margin * 100L;
            }
            else
            {
                c.Price = q.Strike;
                c.Premium = q.Ask;
                c.Mark = Math.Max(1L, q.Fair);   // its fair premium when bought, one micro at least as the save keeps it
                if (!Wallet.Ready(q.Value + q.Fee) || !Wallet.Take(q.Value, DerivMoney.Bought, name))
                {
                    why = DerivWhy.NoCash;
                    return null;
                }
                c.Result = -q.Value * 100L;
            }
            Wallet.Take(q.Fee, MarketMoney.Commission, name);
            Fee(c, q.Fee);
            Book.NextId++;
            Book.Contracts.Add(c);
            return c;
        }

        private void Fee(DerivContract c, long money)
        {
            c.Fees += money * 100L;
            c.Result -= money * 100L;
            Book.Commission += money * 100L;
        }

        /// <summary>Micros a unit an open option is worth now, fair, and the price under it; NaN for one that cannot be priced.</summary>
        public double OptionFair(DerivContract c, out long under)
        {
            under = 0;
            bool call = c.Kind == ContractKind.Call;
            if (c.OnShares)
            {
                Stock s = State.Stock(c.StockId);
                if (s == null || !s.Listed)
                {
                    return double.NaN;
                }
                under = (long)Math.Round(s.Mid * DerivCatalogue.Micro);
                return OptionRules.ShareFair(State, s, c.SettleDay, call, c.Price / (double)DerivCatalogue.Micro, out _, out _) * DerivCatalogue.Micro;
            }
            RawMaterial m = MaterialOf(c.Material);
            if (m == null)
            {
                return double.NaN;
            }
            int closes = DerivRules.Closes(State.Day, State.Phase, c.SettleDay);
            long f = MaterialForward(m, c.SettleDay, State.Phase);
            under = f;
            MaterialRules.Ahead(m.X, m.Group, closes, out _, out double variance);
            return OptionRules.Black(call, f, c.Price, variance);
        }

        /// <summary>Micros a unit an open contract would be sold or bought back at now; 0 for one that cannot be.</summary>
        public long ClosePrice(DerivContract c)
        {
            if (c == null || !c.IsOpen)
            {
                return 0;
            }
            if (c.IsFuture)
            {
                RawMaterial m = MaterialOf(c.Material);
                if (m == null)
                {
                    return 0;
                }
                DerivRules.FutureSides(MaterialForward(m, c.SettleDay, State.Phase), out long ask, out long bid);
                return c.Kind == ContractKind.Long ? bid : ask;
            }
            double fair = OptionFair(c, out long under);
            if (double.IsNaN(fair))
            {
                return 0;
            }
            DerivRules.OptionSides(fair, under, out _, out long sell);
            return sell;
        }

        /// <summary>
        /// A contract ended before its day at the price offered now: a future bought or sold back, its move settled in the
        /// margin account and the margin no longer needed paid back; an option sold back into the cash. What it brought in
        /// money (a future's last move); false with the reason when it cannot be.
        /// </summary>
        public bool CloseContract(int id, bool open, out long money, out DerivWhy why)
        {
            Finish();
            money = 0;
            DerivContract c = Book.Contract(id);
            why = c == null ? DerivWhy.NotHeld : !c.IsOpen ? DerivWhy.Ended : TradeWhy(open);
            if (why != DerivWhy.None)
            {
                return false;
            }
            long price = ClosePrice(c);
            if (price <= 0 && c.IsFuture)
            {
                why = DerivWhy.Unknown;
                return false;
            }
            string name = NameOf(c);
            if (c.IsFuture)
            {
                long value = DerivRules.Money(c.Qty, price, false);
                long fee = MarketTrading.Commission(value, _world.CommissionLeast);
                long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, price);
                Book.Balance += move;
                c.Result += move;
                c.Mark = price;
                PayFee(c, fee, name);
                End(c, ContractEnd.Closed, price);
                money = move / 100;
                Release(State.Day);
                return true;
            }
            money = DerivRules.Money(c.Qty, price, false);
            long cut = Math.Min(money, MarketTrading.Commission(money, _world.CommissionLeast));
            Wallet.Give(money, DerivMoney.Sold, name);
            c.Result += money * 100L;
            PayFee(c, cut, name);
            End(c, ContractEnd.Closed, price);
            return true;
        }

        // A future's commission: from the cash when it is there, else out of the margin account.
        private void PayFee(DerivContract c, long fee, string name)
        {
            if (fee <= 0)
            {
                return;
            }
            if (!Wallet.Take(fee, MarketMoney.Commission, name))
            {
                Book.Balance -= fee * 100L;
            }
            Fee(c, fee);
        }

        private void End(DerivContract c, ContractEnd end, long price)
        {
            c.End = end;
            c.EndDay = State.Day;
            c.EndPrice = Math.Max(0L, price);
            c.Margin = 0;
            Book.Realised += c.Result;
        }

        /// <summary>The margin call paid from the cash into the margin account; false with the reason when it cannot be.</summary>
        public bool PayCall(out long paid, out DerivWhy why)
        {
            Finish();
            paid = 0;
            why = Held ? DerivWhy.Held : Book.CallDay <= 0 ? DerivWhy.NoCall : DerivWhy.None;
            if (why != DerivWhy.None)
            {
                return false;
            }
            long amount = Math.Max(1L, Book.CallAmount);
            if (!Wallet.Take(amount, DerivMoney.Margin, string.Empty))
            {
                why = DerivWhy.NoCash;
                return false;
            }
            Book.Balance += amount * 100L;
            paid = amount;
            if (Book.Balance >= DerivRules.Maintenance(Book))
            {
                Book.CallDay = 0;
                Book.CallAmount = 0;
            }
            return true;
        }

        /// <summary>What the player's contracts are worth now in money: the margin account with today's moves, and the options at their bid.</summary>
        public long DerivWorth()
        {
            long cents = Book.Balance;
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                if (!c.IsOpen)
                {
                    continue;
                }
                long price = ClosePrice(c);
                if (c.IsFuture)
                {
                    cents += price > 0 ? DerivRules.Move(c.Kind, c.Qty, c.Mark, price) : 0;
                }
                else
                {
                    cents += DerivRules.Money(c.Qty, price, false) * 100L;
                }
            }
            return cents / 100;
        }

        // ---------------------------------------------------------------- the close

        private void Derivatives(int day)
        {
            for (int i = 0; i < State.Materials.Count; i++)
            {
                MaterialRules.Close(State.Materials[i], _secret, day, State.Env.PriceLevel);
            }
            if (Book.Contracts.Count == 0 && Book.Balance == 0 && Book.CallDay == 0)
            {
                return;
            }
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                if (!c.IsOpen)
                {
                    continue;
                }
                if (c.IsFuture)
                {
                    RawMaterial m = MaterialOf(c.Material);
                    if (m == null)
                    {
                        if (c.SettleDay <= day)
                        {
                            End(c, ContractEnd.Settled, c.Mark);   // a material the game no longer has: settled at its last price
                        }
                        continue;
                    }
                    long price = c.SettleDay <= day ? m.Fix : MaterialForward(m, c.SettleDay, MarketCatalogue.PhaseClose);
                    long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, price);
                    Book.Balance += move;
                    c.Result += move;
                    c.Mark = price;
                    if (c.SettleDay <= day)
                    {
                        SettleFuture(c, m);
                    }
                }
                else if (c.SettleDay <= day)
                {
                    SettleOption(c);
                }
            }
            FuturesMargin(day);
            DerivRules.Trim(Book, day);
        }

        // A future at its day: its money is all settled; a long one with a place is delivered, for the director to do.
        private void SettleFuture(DerivContract c, RawMaterial m)
        {
            bool deliver = c.Kind == ContractKind.Long && !string.IsNullOrEmpty(c.Warehouse) && Deliveries && !c.Test;
            End(c, deliver ? ContractEnd.Delivered : ContractEnd.Settled, m.Fix);
            if (deliver)
            {
                DerivTold?.Invoke(new DerivNote { Kind = DerivNoteKind.Deliver, Contract = c.Id, Qty = c.Qty, Price = m.Fix, Material = c.Material, Warehouse = c.Warehouse });
            }
            DerivTold?.Invoke(new DerivNote { Kind = DerivNoteKind.Settled, Contract = c.Id, A = c.Result / 100, Price = m.Fix, Material = c.Material });
        }

        // An option at its day: in the money it pays the difference, else it lapses.
        private void SettleOption(DerivContract c)
        {
            long price;
            if (c.OnShares)
            {
                Stock s = State.Stock(c.StockId);
                price = s == null ? c.Price : !s.Listed ? (long)s.GoneCents * (DerivCatalogue.Micro / 100) : DerivRules.SettleMicros(s);
            }
            else
            {
                RawMaterial m = MaterialOf(c.Material);
                price = m != null ? m.Fix : c.Price;
            }
            Pay(c, price, ContractEnd.Exercised);
        }

        private void Pay(DerivContract c, long price, ContractEnd inTheMoney)
        {
            long each = OptionRules.Intrinsic(c.Kind == ContractKind.Call, price, c.Price);
            long money = DerivRules.Money(c.Qty, each, false);
            if (money > 0)
            {
                Wallet.Give(money, DerivMoney.Paid, NameOf(c));
                c.Result += money * 100L;
            }
            ContractEnd end = money > 0 ? inTheMoney : inTheMoney == ContractEnd.Gone ? ContractEnd.Gone : ContractEnd.Lapsed;
            End(c, end, price);
            DerivNoteKind kind = end == ContractEnd.Gone ? DerivNoteKind.Gone : end == ContractEnd.Lapsed ? DerivNoteKind.Lapsed : DerivNoteKind.Exercised;
            DerivTold?.Invoke(new DerivNote { Kind = kind, Contract = c.Id, A = money, B = c.Result / 100, Price = price, Material = c.Material });
        }

        // A company leaves the market: its options settle at what each share is paid out.
        private void OptionsLeave(Stock s)
        {
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                if (c.IsOpen && c.StockId == s.Id)
                {
                    Pay(c, (long)Math.Max(0, s.GoneCents) * (DerivCatalogue.Micro / 100), ContractEnd.Gone);
                }
            }
        }

        // The margin account at a close: over its margins the rest is paid back; under the maintenance a call for the next
        // close, or the broker closes futures when a call is unmet by its day or the account is far under.
        private void FuturesMargin(int day)
        {
            long need = DerivRules.Needed(Book), keep = DerivRules.Maintenance(Book);
            if (need <= 0 || Book.Balance >= keep)
            {
                Book.CallDay = 0;
                Book.CallAmount = 0;
                Release(day);
                return;
            }
            bool far = Book.Balance * 100L < keep * MarketCatalogue.ForcedBelowPct;
            if (far || (Book.CallDay > 0 && day >= Book.CallDay))
            {
                ForceFutures(day);
                return;
            }
            Book.CallAmount = (need - Book.Balance + 99) / 100;
            if (Book.CallDay <= 0)
            {
                Book.CallDay = MarketTrading.MarketDays(day, MarketCatalogue.CallDays);
                DerivTold?.Invoke(new DerivNote { Kind = DerivNoteKind.Called, A = Book.CallAmount, B = Book.CallDay });
            }
        }

        // The broker closes futures at the close's prices, the largest margin first, until the account is a tenth over its
        // maintenance or none is left.
        private void ForceFutures(int day)
        {
            _forcing.Clear();
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                if (c.IsOpen && c.IsFuture)
                {
                    int at = _forcing.Count;
                    while (at > 0 && _forcing[at - 1].Margin < c.Margin)
                    {
                        at--;
                    }
                    _forcing.Insert(at, c);
                }
            }
            for (int i = 0; i < _forcing.Count; i++)
            {
                long keep = DerivRules.Maintenance(Book);
                if (Book.Balance * 100L >= keep * 110L && Book.Balance > 0)
                {
                    break;
                }
                DerivContract c = _forcing[i];
                RawMaterial m = MaterialOf(c.Material);
                long fair = m != null ? MaterialForward(m, c.SettleDay, MarketCatalogue.PhaseClose) : c.Mark;
                DerivRules.FutureSides(fair, out long ask, out long bid);
                long price = c.Kind == ContractKind.Long ? bid : ask;
                long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, price);
                Book.Balance += move;
                c.Result += move;
                c.Mark = price;
                PayFee(c, MarketTrading.Commission(DerivRules.Money(c.Qty, price, false), _world.CommissionLeast), NameOf(c));
                End(c, ContractEnd.Forced, price);
                DerivTold?.Invoke(new DerivNote { Kind = DerivNoteKind.Forced, Contract = c.Id, A = c.Result / 100, Price = price, Material = c.Material });
            }
            _forcing.Clear();
            Book.CallDay = 0;
            Book.CallAmount = 0;
            Release(day);
        }

        // What the margin account holds over the open futures' margins goes back to the cash; with none open, all of it, or
        // what it owes forced from the cash.
        private void Release(int day)
        {
            long need = DerivRules.Needed(Book);
            if (need <= 0)
            {
                if (Book.Balance > 0)
                {
                    long back = Book.Balance / 100;
                    if (back > 0)
                    {
                        Wallet.Give(back, DerivMoney.Back, string.Empty);
                    }
                }
                else if (Book.Balance < 0)
                {
                    long owed = (-Book.Balance + 99) / 100;
                    Wallet.Force(owed, MarketMoney.Shortfall, string.Empty);
                    DerivTold?.Invoke(new DerivNote { Kind = DerivNoteKind.Shortfall, A = owed, B = day });
                }
                Book.Balance = 0;   // under a whole unit kept by the broker: rounded against the player
                Book.CallDay = 0;
                Book.CallAmount = 0;
                return;
            }
            if (Book.Balance > need)
            {
                long back = (Book.Balance - need) / 100;
                if (back > 0)
                {
                    Wallet.Give(back, DerivMoney.Back, string.Empty);
                    Book.Balance -= back * 100L;
                }
            }
        }

        // ---------------------------------------------------------------- leaving, and the test tools

        /// <summary>
        /// Leaving a save: every future closed at its fair price with no spread, the margin account paid out, every option at
        /// its fair price; what that comes to in money (under nothing: owed), each said in the log. Nothing is left.
        /// </summary>
        public long LeaveDerivs(StringBuilder log)
        {
            Finish();
            long cents = Book.Balance;
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                if (!c.IsOpen || c.Test)
                {
                    continue;
                }
                if (c.IsFuture)
                {
                    RawMaterial m = MaterialOf(c.Material);
                    long fair = m != null ? MaterialForward(m, c.SettleDay, State.Phase) : c.Mark;
                    long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, fair);
                    cents += move;
                    log?.Append(" a future of ").Append(c.Qty).Append(' ').Append(c.Material).Append(" for ").Append(move / 100).Append(';');
                }
                else
                {
                    double fair = OptionFair(c, out _);
                    long money = double.IsNaN(fair) ? 0 : DerivRules.Money(c.Qty, (long)fair, false);
                    cents += money * 100L;
                    log?.Append(" an option ").Append(c.Id).Append(" for ").Append(money).Append(';');
                }
            }
            Book.Contracts.Clear();
            Book.Balance = 0;
            Book.CallDay = 0;
            Book.CallAmount = 0;
            return cents >= 0 ? cents / 100 : -((-cents + 99) / 100);
        }

        /// <summary>The test tools: every open contract settles now as at its day, at today's prices. How many.</summary>
        public int SettleContractsNow()
        {
            Finish();
            int count = 0;
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                if (!c.IsOpen)
                {
                    continue;
                }
                count++;
                if (c.IsFuture)
                {
                    RawMaterial m = MaterialOf(c.Material);
                    if (m == null)
                    {
                        continue;
                    }
                    long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, m.Fix);
                    Book.Balance += move;
                    c.Result += move;
                    c.Mark = m.Fix;
                    SettleFuture(c, m);
                }
                else if (c.OnShares)
                {
                    Stock s = State.Stock(c.StockId);
                    Pay(c, s != null && s.Listed ? (long)Math.Round(s.Mid * DerivCatalogue.Micro) : c.Price, ContractEnd.Exercised);
                }
                else
                {
                    RawMaterial m = MaterialOf(c.Material);
                    Pay(c, m != null ? m.Fix : c.Price, ContractEnd.Exercised);
                }
            }
            FuturesMargin(State.Day);
            return count;
        }

        /// <summary>
        /// The test tools: the materials of the player's futures moved against them by a share of their price (a long's
        /// down, a short's up), then the margin account looked at as at a close. How many materials moved.
        /// </summary>
        public int MoveAgainst(double share)
        {
            Finish();
            int moved = 0;
            var done = new List<string>(4);   // a test: each material moved once, the way its first future fears
            for (int i = 0; i < Book.Contracts.Count; i++)
            {
                DerivContract c = Book.Contracts[i];
                RawMaterial m = c.IsOpen && c.IsFuture ? MaterialOf(c.Material) : null;
                if (m == null)
                {
                    continue;
                }
                if (!done.Contains(m.Name))
                {
                    done.Add(m.Name);
                    double ln = Math.Log(Math.Max(0.05, 1.0 + (c.Kind == ContractKind.Long ? -share : share)));
                    m.X = Math.Max(-DerivCatalogue.XMost, Math.Min(DerivCatalogue.XMost, m.X + ln));
                    m.Fix = MaterialRules.Price(m.Base, m.Level, m.X);
                }
                long price = MaterialForward(m, c.SettleDay, MarketCatalogue.PhaseClose);
                long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, price);
                Book.Balance += move;
                c.Result += move;
                c.Mark = price;
                moved++;
            }
            if (moved > 0)
            {
                FuturesMargin(State.Day);
            }
            return moved;
        }
    }
}
