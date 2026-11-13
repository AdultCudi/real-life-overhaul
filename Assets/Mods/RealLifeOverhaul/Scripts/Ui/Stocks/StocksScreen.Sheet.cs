using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks screen's sheets: what a row or a button opens, the figures a player sets in it, and what its buttons do.
    /// Every deed goes through the director the old app asks (MarketDirector), so the money and the state move as the engine
    /// says; the sheet only reads what it says and asks again at the moment of the press, as the old app's confirm did.
    /// </summary>
    public sealed partial class StocksScreen
    {
        private readonly StocksSheetState _sheet = new StocksSheetState();
        private readonly StocksSheetParts _parts = new StocksSheetParts();
        private StocksSheetModel _sheetModel;
        private int _typedId = -1;
        private int _typedField = -1;

        // ------------------------------------------------------------------------------------------------ the sheet's model

        private StocksSheetModel BuildSheet()
        {
            _sheetModel = null;
            _typedId = -1;
            _typedField = -1;
            if (_market == null || _market.State == null)
            {
                _sheet.Close();
                return null;
            }
            _sheetModel = StocksBuild.SheetOf(_market, _sheet, _parts, Words);
            if (_sheetModel == null)
            {
                _sheet.Close();
                return null;
            }
            for (int i = 0; i < _sheetModel.Fields.Length; i++)
            {
                StocksFieldModel f = _sheetModel.Fields[i];
                if ((f.Kind == StocksFieldKind.Stepper && f.Typed) || f.Kind == StocksFieldKind.Text)
                {
                    _typedId = f.Id;
                    _typedField = i;
                }
            }
            return _sheetModel;
        }

        private string SheetTypingText()
        {
            if (_typedField < 0 || _sheetModel == null)
            {
                return string.Empty;
            }
            if (_sheetModel.Fields[_typedField].Kind == StocksFieldKind.Text)
            {
                return _parts.Name ?? string.Empty;
            }
            if (_sheet.Kind == StocksSheets.Alert)
            {
                return (_sheet.V[_typedId] / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);   // a price is typed in dollars
            }
            return _sheet.V[_typedId].ToString();
        }

        private void SheetTyped(string text)
        {
            if (_typedField < 0 || _sheetModel == null)
            {
                return;
            }
            StocksFieldModel f = _sheetModel.Fields[_typedField];
            if (f.Kind == StocksFieldKind.Text)
            {
                _parts.Name = text ?? string.Empty;
            }
            else if (_sheet.Kind == StocksSheets.Alert)
            {
                _sheet.V[_typedId] = StocksSheetState.TypedPrice(text, f.Min, f.Max, _sheet.V[_typedId]);
            }
            else
            {
                _sheet.V[_typedId] = StocksSheetState.Typed(text, 0, f.Max);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------ opening

        private void OpenSheet(byte kind, int key, int target = 0)
        {
            _sheet.Close();
            _sheet.Kind = kind;
            _sheet.Key = key;
            _sheet.V[StocksSheets.Target] = target;   // set before the defaults read it: a founder's sheet about a firm of theirs that listed on its own
            _parts.Name = string.Empty;
            MarketState s = _market != null ? _market.State : null;
            if (s != null)
            {
                Defaults(s, kind, key);
            }
            _dirty = true;
        }

        private void Defaults(MarketState s, byte kind, int key)
        {
            long[] v = _sheet.V;
            switch (kind)
            {
                case StocksSheets.BondBuy:
                    v[StocksSheets.Amount] = _market.BondLeast;
                    break;
                case StocksSheets.Ladder:
                {
                    var pieces = new List<BondOffer>(8);
                    StocksBuild.LadderOffers(_market.BondOffers(), pieces);
                    v[StocksSheets.Amount] = _market.BondLeast * Math.Max(1, pieces.Count);
                    break;
                }
                case StocksSheets.BondHeld:
                {
                    BondHolding h = s.Bonds.Holding(key);
                    if (h != null)
                    {
                        v[StocksSheets.Reinvest] = Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, h.Reinvest));
                        v[StocksSheets.Roll] = h.Rollover ? 1 : 0;
                    }
                    break;
                }
                case StocksSheets.Material:
                case StocksSheets.ShareOption:
                    v[StocksSheets.Strike] = 2;
                    v[StocksSheets.Qty] = 2;
                    break;
                case StocksSheets.Firm:
                    FirmDefaults(s, key);
                    break;
                case StocksSheets.Rule:
                    RuleDefaults(s, key);
                    break;
                case StocksSheets.Margin:
                    v[StocksSheets.Sum] = key == 1 ? Math.Max(1, _market.MostWithdrawable()) : Math.Max(1, s.Account.Margin ? 1000 : Prices.At(MarketCatalogue.MarginOpenLeastAtStart));
                    break;
                case StocksSheets.Own:
                    v[StocksSheets.Float] = Math.Max(0, Array.IndexOf(ListingCatalogue.FloatSteps, ListingCatalogue.FloatDefault));
                    break;
                case StocksSheets.Founder:
                    FounderDefaults(s, key);
                    break;
                case StocksSheets.Fund:
                case StocksSheets.Block:
                case StocksSheets.Split:
                case StocksSheets.BankList:
                    ExtrasDefaults(s, kind, key);   // StocksScreen.Extras.cs
                    break;
                case StocksSheets.Alert:
                {
                    Stock k = s.Stock(key);
                    v[StocksSheets.AlertKindAt] = 0;
                    v[StocksSheets.AlertLevel] = k != null ? StocksBuild.AlertStart(k.Last, true) : 1;
                    break;
                }
            }
        }

        private void FirmDefaults(MarketState s, int firm)
        {
            BotFirm f = MarketBots.Firm(firm);
            if (f == null)
            {
                return;
            }
            Bot b = MarketBots.Hired(s, firm);
            long[] v = _sheet.V;
            v[StocksSheets.Capital] = Math.Max(1, _market.BotCapitalLeast);
            v[StocksSheets.Change] = Math.Max(1, _market.BotChangeLeast);
            v[StocksSheets.Size] = Math.Max(0, MarketBots.IndexOf(MarketBots.Sizes, b != null ? b.Size : f.Size));
            v[StocksSheets.Stop] = Math.Max(0, MarketBots.IndexOf(MarketBots.Stops, b != null ? b.Stop : f.Stop));
            v[StocksSheets.Loss] = Math.Max(0, MarketBots.IndexOf(MarketBots.Losses, b != null ? b.Loss : f.Loss));
            v[StocksSheets.Scope] = b != null ? (int)b.Scope : 0;
            v[StocksSheets.PayPct] = Math.Max(0, MarketBots.IndexOf(MarketBots.PayPcts, b != null ? b.PayPct : 0));
            v[StocksSheets.Every] = b != null ? (int)b.PayEvery : (int)BotPayEvery.Monthly;
            _parts.Picks.Clear();
        }

        private static int Nearest(int[] steps, int value)
        {
            int best = 0;
            for (int i = 1; i < steps.Length; i++)
            {
                if (Math.Abs(steps[i] - value) < Math.Abs(steps[best] - value))
                {
                    best = i;
                }
            }
            return best;
        }

        private void RuleDefaults(MarketState s, int key)
        {
            long[] v = _sheet.V;
            RuleBot r = key >= 0 && key < s.Rules.Count ? s.Rules[key] : null;
            if (r != null)
            {
                v[StocksSheets.RuleKind] = (int)r.Kind;
                v[StocksSheets.Level] = r.Level;
                v[StocksSheets.Gain] = Nearest(StocksBuild.RuleGain, r.GainBp);
                v[StocksSheets.OrderKind] = r.Type == OrderType.Limit ? 1 : 0;
                v[StocksSheets.Budget] = r.Budget;
                v[StocksSheets.PerTrade] = r.PerTrade;
                v[StocksSheets.RuleStop] = Nearest(StocksBuild.RuleStops, r.StopBp);
                v[StocksSheets.OpenOnly] = r.OpenOnly ? 1 : 0;
                v[StocksSheets.RuleReinvest] = r.Reinvest ? 1 : 0;
                v[StocksSheets.Tell] = r.Tell ? 1 : 0;
                v[StocksSheets.MaxOpen] = r.MaxOpen;
                return;
            }
            StocksBuild.RuleCompanies(s, _parts.Companies);
            Stock stock = _parts.Companies.Count > 0 ? s.Stock(_parts.Companies[0]) : null;
            v[StocksSheets.RuleKind] = (int)RuleKind.Dip;
            v[StocksSheets.Level] = stock != null ? StocksBuild.DefaultLevel(RuleKind.Dip, stock) : 30;
            v[StocksSheets.Gain] = Nearest(StocksBuild.RuleGain, 800);
            v[StocksSheets.OrderKind] = 0;
            v[StocksSheets.PerTrade] = 10;
            v[StocksSheets.Budget] = BudgetFor(stock, 10);
            v[StocksSheets.RuleStop] = Nearest(StocksBuild.RuleStops, 500);
            v[StocksSheets.OpenOnly] = 1;
            v[StocksSheets.Tell] = 1;
            v[StocksSheets.MaxOpen] = 1;
        }

        // Ten trades' worth of the shares it buys at a time, at least a thousand, never past the player's cash.
        private long BudgetFor(Stock stock, long perTrade)
        {
            long one = stock != null ? stock.Last * perTrade / 100L : 1000L;
            return Math.Max(100L, Math.Min(Math.Max(1000L, one * 10L), Math.Max(100L, _market.Cash)));
        }

        private void FounderDefaults(MarketState s, int key)
        {
            Stock k = _market.FounderStock((int)_sheet.V[StocksSheets.Target]);
            long[] v = _sheet.V;
            if (k == null || k.Own == null)
            {
                return;
            }
            switch ((OwnAction)key)
            {
                case OwnAction.Pay: v[StocksSheets.Sum] = Math.Max(0, Array.IndexOf(ListingCatalogue.PaySteps, k.Own.PayBp)); break;
                case OwnAction.Dividend: v[StocksSheets.Sum] = Math.Max(1, Math.Min(ListingRules.Most(k.Own), k.Own.ProposedCents > 0 ? k.Own.ProposedCents : k.Last / 50)); break;
                case OwnAction.Deal: v[StocksSheets.Sum] = 100; break;
                default: v[StocksSheets.Sum] = 1000; break;
            }
        }

        // ------------------------------------------------------------------------------------------------ presses

        private bool SheetBack()
        {
            if (_sheet.Armed >= 0)
            {
                _sheet.Armed = -1;
            }
            else
            {
                _sheet.Close();
            }
            _dirty = true;
            return true;
        }

        private bool SheetPress(int command, int arg)
        {
            switch (command)
            {
                case StocksSheetLay.Cancel: return SheetBack();
                case StocksSheetLay.Ok: return SheetConfirm();
                case StocksSheetLay.Extra: return SheetExtra(arg);
                case StocksSheetLay.Plain:
                case StocksSheetLay.Type: return true;
            }
            if (_sheetModel == null || arg < 0 || arg >= _sheetModel.Fields.Length)
            {
                return false;
            }
            StocksFieldModel f = _sheetModel.Fields[arg];
            _sheet.Armed = -1;
            switch (command)
            {
                case StocksSheetLay.Pick:
                    _sheet.V[f.Id] = StocksSheetState.Cycled(_sheet.V[f.Id], f, 1);
                    break;
                case StocksSheetLay.Switch:
                    if (_sheet.Kind == StocksSheets.Firm && f.Id == StocksSheets.PickOn)
                    {
                        return TogglePick();
                    }
                    _sheet.V[f.Id] = _sheet.V[f.Id] == 0 ? 1 : 0;
                    break;
                case StocksSheetLay.Less:
                    _sheet.V[f.Id] = StocksSheetState.Stepped(_sheet.V[f.Id], f, -1);
                    break;
                case StocksSheetLay.More:
                    _sheet.V[f.Id] = StocksSheetState.Stepped(_sheet.V[f.Id], f, 1);
                    break;
                default:
                    return false;
            }
            Changed(f.Id);
            _dirty = true;
            return true;
        }

        /// <summary>Left and Right step a field of the sheet that is a choice or a stepper.</summary>
        public bool Steps(int command)
        {
            return _sheet.Open ? command == StocksSheetLay.Pick || command == StocksSheetLay.Type : CompanySteps(command);
        }

        public void Step(int command, int arg, int step)
        {
            if (!_sheet.Open)
            {
                CompanyStep(command, step);
                return;
            }
            if (_sheetModel == null || arg < 0 || arg >= _sheetModel.Fields.Length)
            {
                return;
            }
            StocksFieldModel f = _sheetModel.Fields[arg];
            if (command == StocksSheetLay.Pick)
            {
                _sheet.V[f.Id] = StocksSheetState.Cycled(_sheet.V[f.Id], f, step);
            }
            else if (f.Kind == StocksFieldKind.Stepper)
            {
                _sheet.V[f.Id] = StocksSheetState.Stepped(_sheet.V[f.Id], f, step);
            }
            Changed(f.Id);
            _dirty = true;
        }

        // A figure moved: a thing already held takes it at once, as the old app's selectors did; a new rule's level follows its kind.
        private void Changed(int id)
        {
            MarketState s = _market != null ? _market.State : null;
            if (s == null)
            {
                return;
            }
            switch (_sheet.Kind)
            {
                case StocksSheets.BondHeld:
                    BondHeldChanged(id);
                    break;
                case StocksSheets.Firm:
                    FirmChanged(s, id);
                    break;
                case StocksSheets.Block:
                    BlockChanged(s, id);
                    break;
                case StocksSheets.Alert:
                {
                    Stock k = s.Stock(_sheet.Key);
                    if (id == StocksSheets.AlertKindAt && k != null)
                    {
                        _sheet.V[StocksSheets.AlertLevel] = StocksBuild.AlertStart(k.Last, _sheet.V[StocksSheets.AlertKindAt] == 0);   // the level follows its side
                    }
                    break;
                }
                case StocksSheets.Rule:
                    if (_sheet.Key < 0 && (id == StocksSheets.RuleKind || id == StocksSheets.RuleStock))
                    {
                        StocksBuild.RuleCompanies(s, _parts.Companies);
                        int at = (int)Math.Max(0, Math.Min(_parts.Companies.Count - 1, _sheet.V[StocksSheets.RuleStock]));
                        Stock stock = _parts.Companies.Count > 0 ? s.Stock(_parts.Companies[at]) : null;
                        if (stock != null)
                        {
                            var kind = (RuleKind)(int)Math.Max(0, Math.Min(7, _sheet.V[StocksSheets.RuleKind]));
                            _sheet.V[StocksSheets.Level] = StocksBuild.DefaultLevel(kind, stock);
                            _sheet.V[StocksSheets.Budget] = BudgetFor(stock, Math.Max(1, _sheet.V[StocksSheets.PerTrade]));
                        }
                    }
                    break;
            }
        }

        private void BondHeldChanged(int id)
        {
            BondHolding h = _market.State.Bonds.Holding(_sheet.Key);
            if (h == null)
            {
                return;
            }
            BondWhy why;
            bool done = true;
            if (id == StocksSheets.Reinvest)
            {
                done = _market.SetBondReinvest(h.Id, BondCatalogue.Reinvest[(int)Math.Max(0, Math.Min(BondCatalogue.Reinvest.Length - 1, _sheet.V[StocksSheets.Reinvest]))], out why);
            }
            else
            {
                done = _market.SetBondRollover(h.Id, _sheet.V[StocksSheets.Roll] != 0, out why);
            }
            if (!done)
            {
                Refused(BondText.Why(why));
                Defaults(_market.State, StocksSheets.BondHeld, h.Id);
            }
        }

        private void FirmChanged(MarketState s, int id)
        {
            Bot b = MarketBots.Hired(s, _sheet.Key);
            if (b == null)
            {
                return;
            }
            long[] v = _sheet.V;
            BotWhy why;
            bool done = true;
            int size = MarketBots.Sizes[(int)Math.Max(0, Math.Min(MarketBots.Sizes.Length - 1, v[StocksSheets.Size]))];
            int stop = MarketBots.Stops[(int)Math.Max(0, Math.Min(MarketBots.Stops.Length - 1, v[StocksSheets.Stop]))];
            int loss = MarketBots.Losses[(int)Math.Max(0, Math.Min(MarketBots.Losses.Length - 1, v[StocksSheets.Loss]))];
            switch (id)
            {
                case StocksSheets.Size:
                case StocksSheets.Stop:
                case StocksSheets.Loss:
                    done = _market.SetBot(_sheet.Key, size, stop, loss, out why);
                    break;
                case StocksSheets.Scope:
                    done = _market.SetBotScope(_sheet.Key, (BotScope)(int)Math.Max(0, Math.Min(2, v[StocksSheets.Scope])), out why);
                    break;
                case StocksSheets.PayPct:
                case StocksSheets.Every:
                    done = _market.SetBotPayout(_sheet.Key, MarketBots.PayPcts[(int)Math.Max(0, Math.Min(MarketBots.PayPcts.Length - 1, v[StocksSheets.PayPct]))],
                        (BotPayEvery)(int)Math.Max(0, Math.Min(2, v[StocksSheets.Every])), out why);
                    break;
                default:
                    return;
            }
            if (!done)
            {
                Refused(MarketText.T(StocksBuild.BotWhyKeys[(int)Math.Max(0, Math.Min(StocksBuild.BotWhyKeys.Length - 1, (int)why))]));
                FirmDefaults(s, _sheet.Key);
            }
        }

        // The company shown in the firm's picker, taken on or off the firm's shares.
        private bool TogglePick()
        {
            MarketState s = _market.State;
            Bot b = MarketBots.Hired(s, _sheet.Key);
            List<int> picks = b != null ? b.Picks : _parts.Picks;
            StocksBuild.PickCandidates(s, _market.Engine, picks, _parts.Candidates);
            if (_parts.Candidates.Count == 0)
            {
                return false;
            }
            int id = _parts.Candidates[(int)Math.Max(0, Math.Min(_parts.Candidates.Count - 1, _sheet.V[StocksSheets.PickAt]))];
            bool on = !picks.Contains(id);
            if (b != null)
            {
                if (!_market.PickForBot(_sheet.Key, id, on, out BotWhy why) && why != BotWhy.None)
                {
                    Refused(MarketText.T(StocksBuild.BotWhyKeys[(int)why]));
                }
            }
            else if (!on)
            {
                _parts.Picks.Remove(id);
            }
            else if (_parts.Picks.Count < MarketBots.PicksMost)
            {
                _parts.Picks.Add(id);
            }
            else
            {
                Refused(MarketText.T(StocksBuild.BotWhyKeys[(int)BotWhy.Picks]));
            }
            _dirty = true;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ rows and page buttons

        private bool RowPressed(bool second, int arg)
        {
            MarketState s = _market != null ? _market.State : null;
            if (s == null || arg < 0)
            {
                return false;
            }
            switch (_page)
            {
                case StocksPage.Bonds:
                    return RowBond(s, second, arg);
                case StocksPage.Futures:
                    return RowFuture(s, second, arg);
                case StocksPage.Bots:
                {
                    if (!second)
                    {
                        if (arg >= MarketBots.All.Length)
                        {
                            return false;
                        }
                        OpenSheet(StocksSheets.Firm, MarketBots.All[arg].Id);
                        return true;
                    }
                    var order = new List<int>(12);
                    StocksBuild.RuleOrder(s, order);
                    if (arg >= order.Count)
                    {
                        return false;
                    }
                    return OpenRule(order[arg]);
                }
                case StocksPage.History:
                {
                    // A trade of a company opens that company; a deposit or an interest line has none.
                    int at = second ? -1 : StocksBuild.TradeAt(s, arg);
                    Stock traded = at >= 0 && s.Trades[at].StockId > 0 ? s.Stock(s.Trades[at].StockId) : null;
                    if (traded == null)
                    {
                        return false;
                    }
                    OpenCompany(traded.Id);
                    return true;
                }
                case StocksPage.Orders:
                {
                    if (second)
                    {
                        return false;
                    }
                    var ids = new List<int>(16);
                    StocksBuild.OrderRows(s, _done, ids);
                    if (arg >= ids.Count)
                    {
                        return false;
                    }
                    OpenSheet(StocksSheets.Order, ids[arg]);
                    return true;
                }
            }
            return false;
        }

        private bool RowBond(MarketState s, bool second, int arg)
        {
            if (second)
            {
                if (arg >= s.Bonds.Holdings.Count)
                {
                    return false;
                }
                OpenSheet(StocksSheets.BondHeld, s.Bonds.Holdings[arg].Id);
                return true;
            }
            List<BondOffer> offers = _market.BondOffers();
            if (arg < offers.Count)
            {
                OpenSheet(StocksSheets.BondBuy, arg);
                return true;
            }
            if (arg == offers.Count)
            {
                OpenSheet(StocksSheets.Ladder, 0);
                return true;
            }
            return false;
        }

        private bool RowFuture(MarketState s, bool second, int arg)
        {
            if (second)
            {
                if (arg >= s.Derivs.Contracts.Count)
                {
                    return false;
                }
                OpenSheet(StocksSheets.Contract, s.Derivs.Contracts[arg].Id);
                return true;
            }
            if (arg < s.Materials.Count)
            {
                OpenSheet(StocksSheets.Material, arg);
                return true;
            }
            var stocks = new List<int>(16);
            StocksBuild.OptionStocks(s, stocks);
            int at = arg - s.Materials.Count;
            if (at < 0 || at >= stocks.Count)
            {
                return false;
            }
            OpenSheet(StocksSheets.ShareOption, stocks[at]);
            return true;
        }

        private bool OpenRule(int index)
        {
            MarketState s = _market != null ? _market.State : null;
            if (s == null || (index < 0 && RuleBots.PlayerCount(s) >= RuleBots.PlayerMost))
            {
                return false;
            }
            OpenSheet(StocksSheets.Rule, index);
            return true;
        }

        private bool PageAction(int arg)
        {
            MarketState s = _market != null ? _market.State : null;
            if (s == null)
            {
                return false;
            }
            switch (_page)
            {
                case StocksPage.Futures: return PayCall();
                case StocksPage.Index: return Back();
                case StocksPage.Bots: return OpenRule(-1);
                case StocksPage.Portfolio:
                {
                    var held = new List<int>(4);
                    StocksBuild.PortfolioCodes(s, held);
                    int what = arg >= 0 && arg < held.Count ? held[arg] : 0;
                    if (what == StocksBuild.CodeFund)
                    {
                        OpenSheet(StocksSheets.Fund, 0);
                    }
                    else if (what == StocksBuild.CodeBlock)
                    {
                        OpenSheet(StocksSheets.Block, _stock);   // on the company last opened, when a block may be dealt in it
                    }
                    else
                    {
                        OpenSheet(StocksSheets.Margin, s.Account.Margin ? what : 0);
                    }
                    return true;
                }
                case StocksPage.Listing:
                {
                    var codes = new List<int>(8);
                    StocksBuild.ListingCodes(_market, codes);
                    if (arg < 0 || arg >= codes.Count)
                    {
                        return false;
                    }
                    int code = codes[arg];
                    if (code >= StocksBuild.CodeSplit)
                    {
                        OpenSheet(code == StocksBuild.CodeSplit ? StocksSheets.Split : StocksSheets.BankList, 0);
                    }
                    else if (code >= 100)
                    {
                        OpenSheet(StocksSheets.Founder, code - 100);
                    }
                    else
                    {
                        OpenSheet(StocksSheets.Own, code);
                    }
                    return true;
                }
            }
            return false;
        }

        private bool PayCall()
        {
            if (_market == null || _market.State == null || _market.State.Derivs.CallDay <= 0)
            {
                return false;
            }
            if (!_market.PayCall(out long paid, out DerivWhy why))
            {
                Refused(DerivText.Why(why));
            }
            else
            {
                Told(NotificationType.Success, "reallife_mkt_drv_note_paid", "amount", MarketText.Money(paid));
            }
            _dirty = true;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the sheet's buttons

        private bool SheetConfirm()
        {
            if (_sheetModel == null || _sheetModel.ConfirmOff || string.IsNullOrEmpty(_sheetModel.Confirm) || _market == null)
            {
                return false;
            }
            bool done;
            switch (_sheet.Kind)
            {
                case StocksSheets.BondBuy: done = BuyBond(false); break;
                case StocksSheets.Ladder: done = BuyBond(true); break;
                case StocksSheets.Material: done = OpenContract(false); break;
                case StocksSheets.ShareOption: done = OpenContract(true); break;
                case StocksSheets.Firm: done = HireFirm(); break;
                case StocksSheets.Rule: done = SaveRule(); break;
                case StocksSheets.Margin: done = MoveMargin(); break;
                case StocksSheets.Own: done = OwnStep(); break;
                case StocksSheets.Founder: done = FounderStep(); break;
                case StocksSheets.Alert: done = SetAlert(); break;
                case StocksSheets.Ticket: done = PlaceTicket(); break;
                case StocksSheets.Fund: done = DealFund(); break;
                case StocksSheets.Block: done = DealBlock(); break;
                case StocksSheets.Split: done = AskSplit(); break;
                case StocksSheets.BankList: done = BankStep(); break;
                default: done = false; break;
            }
            _dirty = true;
            return done;
        }

        private bool SheetExtra(int index)
        {
            if (_market == null || _market.State == null)
            {
                return false;
            }
            MarketState s = _market.State;
            bool done = false;
            switch (_sheet.Kind)
            {
                case StocksSheets.BondHeld: done = Armed(index, 0) && SellBond(); break;
                case StocksSheets.Contract: done = Armed(index, 0) && CloseContract(); break;
                case StocksSheets.Firm: done = FirmExtra(s, index); break;
                case StocksSheets.Rule: done = RuleExtra(s, index); break;
                case StocksSheets.Order: done = OrderExtra(s, index); break;
            }
            _dirty = true;
            return done;
        }

        // A destructive button is pressed twice: the first arms it and the sheet says what it will do.
        private bool Armed(int index, int expected)
        {
            if (index != expected)
            {
                return false;
            }
            if (_sheet.Armed != index)
            {
                _sheet.Armed = index;
                _dirty = true;
                return false;
            }
            _sheet.Armed = -1;
            return true;
        }

        private static void Told(NotificationType type, string key, params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            Notifications.Show(type, key, data, 6f, null, null, true, false);
        }

        private static void Refused(string why)
        {
            Notifications.Show(NotificationType.Error, "reallife_mkt_bond_note_refused", new Dictionary<string, string> { { "why", why ?? string.Empty } }, 8f, null, null, true, false);
        }

        // ---- bonds

        private bool BuyBond(bool ladder)
        {
            long amount = _sheet.V[StocksSheets.Amount];
            int reinvest = BondCatalogue.Reinvest[(int)Math.Max(0, Math.Min(BondCatalogue.Reinvest.Length - 1, _sheet.V[StocksSheets.Reinvest]))];
            bool roll = _sheet.V[StocksSheets.Roll] != 0;
            if (ladder)
            {
                var pieces = new List<BondOffer>(8);
                StocksBuild.LadderOffers(_market.BondOffers(), pieces);
                int bought = _market.BuyLadder(amount, reinvest, roll, out BondWhy lwhy);
                if (bought <= 0)
                {
                    Refused(BondText.Why(lwhy));
                    return false;
                }
                Told(NotificationType.Success, "reallife_mkt_bond_note_ladder", "amount", MarketText.Money(amount), "count", bought.ToString(),
                    "first", BondText.Term(pieces[0].Def.TermQuarters), "last", BondText.Term(pieces[pieces.Count - 1].Def.TermQuarters));
                _sheet.Close();
                return true;
            }
            List<BondOffer> offers = _market.BondOffers();
            if (_sheet.Key < 0 || _sheet.Key >= offers.Count)
            {
                return false;
            }
            BondOffer o = offers[_sheet.Key];
            BondHolding h = _market.BuyBond(o, amount, reinvest, roll, out BondWhy why);
            if (h == null)
            {
                Refused(BondText.Why(why));
                return false;
            }
            Told(NotificationType.Success, "reallife_mkt_bond_note_bought", "name", BondText.Name(o.Def, o.Issuer), "amount", MarketText.Money(amount), "rate", BondText.Rate(h.RateBp));
            _sheet.Close();
            return true;
        }

        private bool SellBond()
        {
            BondHolding h = _market.State.Bonds.Holding(_sheet.Key);
            string name = BondText.Name(h);
            if (!_market.SellBond(_sheet.Key, out long money, out BondWhy why))
            {
                Refused(BondText.Why(why));
                return false;
            }
            Told(NotificationType.Success, "reallife_mkt_bond_note_sold", "name", name, "amount", MarketText.Money(money));
            _sheet.Close();
            return true;
        }

        // ---- contracts

        private bool OpenContract(bool shares)
        {
            MarketState s = _market.State;
            long[] v = _sheet.V;
            int days = _market.ContractDays(_parts.Days);
            if (days == 0)
            {
                Refused(DerivText.Why(DerivWhy.Closed));
                return false;
            }
            int settle = _parts.Days[(int)Math.Max(0, Math.Min(days - 1, v[StocksSheets.Week]))];
            int step = StocksBuild.StrikeStep(v[StocksSheets.Strike]);
            DerivContract c;
            DerivWhy why;
            if (shares)
            {
                ContractKind kind = v[StocksSheets.What] == 0 ? ContractKind.Call : ContractKind.Put;
                long qty = DerivCatalogue.ShareQuantities[(int)Math.Max(0, Math.Min(DerivCatalogue.ShareQuantities.Length - 1, v[StocksSheets.Qty]))];
                c = _market.Buy(kind, null, _sheet.Key, settle, step, qty, null, null, out why);
            }
            else
            {
                RawMaterial mat = _sheet.Key >= 0 && _sheet.Key < s.Materials.Count ? s.Materials[_sheet.Key] : null;
                if (mat == null)
                {
                    return false;
                }
                ContractKind kind = StocksBuild.MaterialKind(v[StocksSheets.What]);
                long qty = DerivCatalogue.MaterialQuantities[(int)Math.Max(0, Math.Min(DerivCatalogue.MaterialQuantities.Length - 1, v[StocksSheets.Qty]))];
                string placeKey = null, placeName = null;
                if (kind == ContractKind.Long && !DerivCatalogue.IsBuilding(mat.Name))
                {
                    _market.Places(_parts.PlaceKeys, _parts.PlaceNames);
                    int pl = (int)Math.Max(0, Math.Min(_parts.PlaceKeys.Count, v[StocksSheets.Place]));
                    if (pl > 0)
                    {
                        placeKey = _parts.PlaceKeys[pl - 1];
                        placeName = _parts.PlaceNames[pl - 1];
                    }
                }
                c = _market.Buy(kind, mat.Name, 0, settle, step, qty, placeKey, placeName, out why);
            }
            if (c == null)
            {
                Refused(DerivText.Why(why));
                return false;
            }
            Told(NotificationType.Success, "reallife_mkt_drv_note_bought", "name", DerivText.Name(c));
            _sheet.Close();
            return true;
        }

        private bool CloseContract()
        {
            DerivContract c = _market.State.Derivs.Contract(_sheet.Key);
            if (c == null || !c.IsOpen)
            {
                Refused(DerivText.Why(c == null ? DerivWhy.NotHeld : DerivWhy.Ended));
                return false;
            }
            string name = DerivText.Name(c);
            long result = StocksBuild.ContractGain(c, StocksBuild.ContractWorth(_market, c));
            if (!_market.CloseContract(_sheet.Key, out _, out DerivWhy why))
            {
                Refused(DerivText.Why(why));
                return false;
            }
            DerivContract ended = _market.State.Derivs.Contract(_sheet.Key);
            Told(NotificationType.Success, "reallife_mkt_drv_note_closed", "name", name, "result", DerivText.Signed(ended != null ? ended.Result / 100 : result));
            _sheet.Close();
            return true;
        }

        // ---- firms and rules

        private bool HireFirm()
        {
            BotFirm f = MarketBots.Firm(_sheet.Key);
            if (f == null)
            {
                return false;
            }
            long[] v = _sheet.V;
            int size = MarketBots.Sizes[(int)Math.Max(0, Math.Min(MarketBots.Sizes.Length - 1, v[StocksSheets.Size]))];
            int stop = MarketBots.Stops[(int)Math.Max(0, Math.Min(MarketBots.Stops.Length - 1, v[StocksSheets.Stop]))];
            int loss = MarketBots.Losses[(int)Math.Max(0, Math.Min(MarketBots.Losses.Length - 1, v[StocksSheets.Loss]))];
            Bot b = _market.HireBot(f.Id, v[StocksSheets.Capital], size, stop, loss, out BotWhy why);
            if (b == null)
            {
                Refused(MarketText.T(StocksBuild.BotWhyKeys[(int)Math.Max(0, Math.Min(StocksBuild.BotWhyKeys.Length - 1, (int)why))]));
                return false;
            }
            for (int i = 0; i < _parts.Picks.Count; i++)
            {
                _market.PickForBot(f.Id, _parts.Picks[i], true, out _);
            }
            _market.SetBotScope(f.Id, (BotScope)(int)Math.Max(0, Math.Min(2, v[StocksSheets.Scope])), out _);
            _market.SetBotPayout(f.Id, MarketBots.PayPcts[(int)Math.Max(0, Math.Min(MarketBots.PayPcts.Length - 1, v[StocksSheets.PayPct]))],
                (BotPayEvery)(int)Math.Max(0, Math.Min(2, v[StocksSheets.Every])), out _);
            Told(NotificationType.Success, "reallife_mkt_bot_note_hired", "firm", f.Name);
            _sheet.Close();
            return true;
        }

        private bool FirmExtra(MarketState s, int index)
        {
            BotFirm f = MarketBots.Firm(_sheet.Key);
            Bot b = f != null ? MarketBots.Hired(s, f.Id) : null;
            if (b == null)
            {
                return false;
            }
            // The buttons stand as the sheet lays them: a running firm has add, take out and sell up; a stopped one restart; every one dismiss.
            string[] codes = b.Status == BotStatus.Running ? new[] { "add", "take", "sell",
                "dismiss" } : b.Status == BotStatus.Stopped ? new[] { "restart", "dismiss" } : new[] { "dismiss" };
            if (index < 0 || index >= codes.Length)
            {
                return false;
            }
            BotWhy why;
            bool done;
            long amount = _sheet.V[StocksSheets.Change];
            switch (codes[index])
            {
                case "add":
                    if (!Armed(index, index))
                    {
                        return false;
                    }
                    done = _market.AddBotCapital(f.Id, amount, out why);
                    if (done)
                    {
                        Told(NotificationType.Success, "reallife_mkt_bot_note_added", "firm", f.Name, "amount", MarketText.Money(amount), "capital",
                            MarketText.Money(MarketBots.Hired(_market.State, f.Id).Capital));
                    }
                    break;
                case "take":
                    if (!Armed(index, index))
                    {
                        return false;
                    }
                    done = _market.TakeBotCapital(f.Id, amount, out why);
                    if (done && MarketBots.Hired(_market.State, f.Id) != null)
                    {
                        Told(NotificationType.Success, "reallife_mkt_bot_note_taken", "firm", f.Name, "amount", MarketText.Money(amount), "capital",
                            MarketText.Money(MarketBots.Hired(_market.State, f.Id).Capital));
                    }
                    break;
                case "sell":
                    done = _market.SellUpBot(f.Id, out why);
                    break;
                case "restart":
                    done = _market.RestartBot(f.Id, out why);
                    break;
                default:
                    if (!Armed(index, index))
                    {
                        return false;
                    }
                    done = _market.DismissBot(f.Id, out why);
                    if (done)
                    {
                        _sheet.Close();
                    }
                    break;
            }
            if (!done)
            {
                Refused(MarketText.T(StocksBuild.BotWhyKeys[(int)Math.Max(0, Math.Min(StocksBuild.BotWhyKeys.Length - 1, (int)why))]));
            }
            return done;
        }

        private bool SaveRule()
        {
            MarketState s = _market.State;
            long[] v = _sheet.V;
            var kind = (RuleKind)(int)Math.Max(0, Math.Min(7, v[StocksSheets.RuleKind]));
            int gain = StocksBuild.RuleGain[(int)Math.Max(0, Math.Min(StocksBuild.RuleGain.Length - 1, v[StocksSheets.Gain]))];
            int stop = StocksBuild.RuleStops[(int)Math.Max(0, Math.Min(StocksBuild.RuleStops.Length - 1, v[StocksSheets.RuleStop]))];
            OrderType type = v[StocksSheets.OrderKind] == 0 ? OrderType.Market : OrderType.Limit;
            if (_sheet.Key < 0)
            {
                StocksBuild.RuleCompanies(s, _parts.Companies);
                int id = _parts.Companies.Count > 0 ? _parts.Companies[(int)Math.Max(0, Math.Min(_parts.Companies.Count - 1, v[StocksSheets.RuleStock]))] : 0;
                RuleBot made = RuleBots.NewRule(s, kind, id, (int)v[StocksSheets.Level], gain, type, v[StocksSheets.Budget], v[StocksSheets.PerTrade], stop,
                    v[StocksSheets.OpenOnly] != 0, v[StocksSheets.RuleReinvest] != 0, v[StocksSheets.Tell] != 0, (int)v[StocksSheets.MaxOpen]);
                if (made == null)
                {
                    Refused(Words("reallife_stk_sh_rules_full", "You run as many rules as you can."));
                    return false;
                }
            }
            else
            {
                RuleBot r = _sheet.Key < s.Rules.Count ? s.Rules[_sheet.Key] : null;
                if (r == null || r.Owner != 0)
                {
                    return false;
                }
                long budget = v[StocksSheets.Budget];
                r.Cash = Math.Max(0L, Math.Min(budget, r.Cash + budget - r.Budget));
                r.Budget = budget;
                r.Level = (int)v[StocksSheets.Level];
                r.GainBp = gain;
                r.StopBp = stop;
                r.Type = type;
                r.PerTrade = Math.Max(1L, v[StocksSheets.PerTrade]);
                r.MaxOpen = (int)Math.Max(1, v[StocksSheets.MaxOpen]);
                r.OpenOnly = v[StocksSheets.OpenOnly] != 0;
                r.Reinvest = v[StocksSheets.RuleReinvest] != 0;
                r.Tell = v[StocksSheets.Tell] != 0;
            }
            _sheet.Close();
            return true;
        }

        private bool RuleExtra(MarketState s, int index)
        {
            RuleBot r = _sheet.Key >= 0 && _sheet.Key < s.Rules.Count ? s.Rules[_sheet.Key] : null;
            if (r == null || r.Owner != 0)
            {
                return false;
            }
            if (index == 0)
            {
                r.Status = r.Status == RuleStatus.Running ? RuleStatus.Paused : RuleStatus.Running;
                _sheet.Close();
                return true;
            }
            if (!Armed(index, 1))
            {
                return false;
            }
            bool done = RuleBots.Remove(s, _sheet.Key);
            _sheet.Close();
            return done;
        }

        // ---- orders, the margin account and the listing

        private bool OrderExtra(MarketState s, int index)
        {
            RealLifeOverhaul.Markets.Order o = s.Order(_sheet.Key);
            if (o == null)
            {
                return false;
            }
            Stock k = s.Stock(o.StockId);
            // The buttons stand as laid: open the company, then cancel when the order is open and not forced.
            if (k != null && index == 0)
            {
                OpenCompany(k.Id);
                _sheet.Close();
                return true;
            }
            if (!_market.Cancel(o.Id))
            {
                Refused(MarketText.Why(_market.Behind ? OrderWhy.Behind : OrderWhy.Held, k));
                return false;
            }
            _sheet.Close();
            return true;
        }

        // The alert written in the sheet, set on the chart book: told why when the level is on the wrong side of the price.
        private bool SetAlert()
        {
            MarketState s = _market.State;
            Stock k = s.Stock(_sheet.Key);
            if (k == null)
            {
                return false;
            }
            bool above = _sheet.V[StocksSheets.AlertKindAt] == 0;
            AlertWhy why = s.Charts.SetAlert(k.Id, above ? AlertKind.Above : AlertKind.Below, (int)_sheet.V[StocksSheets.AlertLevel], k.Last);
            if (why != AlertWhy.None)
            {
                Refused(StocksBuild.AlertWhyWords(why, Words));
                return false;
            }
            _sheet.Close();
            return true;
        }

        private bool MoveMargin()
        {
            long amount = _sheet.V[StocksSheets.Sum];
            bool ok = _sheet.Key == 1 ? _market.Withdraw(amount) : _market.Deposit(amount);
            if (!ok)
            {
                Notifications.ShowError(_sheet.Key == 1 ? "reallife_mkt_note_withdraw_refused" : "reallife_mkt_note_deposit_refused", null, false);
                return false;
            }
            _sheet.Close();
            return true;
        }

        private bool OwnStep()
        {
            OwnWhy why;
            switch (_sheet.Key)
            {
                case StocksBuild.OwnEngage: why = _market.Engage(); break;
                case StocksBuild.OwnAudit: why = _market.OrderAudit(); break;
                case StocksBuild.OwnWithdraw:
                    _market.Withdraw();
                    why = OwnWhy.None;
                    break;
                default:
                {
                    ListingFile f = _market.State.Listing;
                    string name = MarketStore.Clean((string.IsNullOrEmpty(_parts.Name) ? f.Name : _parts.Name).Trim());
                    if (name.Length < 2)
                    {
                        Notifications.ShowError("reallife_lst_note_name_short", null, false);
                        return false;
                    }
                    int floatBp = ListingCatalogue.FloatSteps[(int)Math.Max(0, Math.Min(ListingCatalogue.FloatSteps.Length - 1, _sheet.V[StocksSheets.Float]))];
                    why = _market.Publish(name, floatBp, StocksBuild.WalkOf((int)_sheet.V[StocksSheets.Walk]));
                    break;
                }
            }
            return Own(why);
        }

        private bool FounderStep()
        {
            var action = (OwnAction)Math.Max(0, Math.Min((int)OwnAction.Private, _sheet.Key));
            long v = _sheet.V[StocksSheets.Sum];
            long value;
            switch (action)
            {
                case OwnAction.Pay: value = ListingCatalogue.PaySteps[(int)Math.Max(0, Math.Min(ListingCatalogue.PaySteps.Length - 1, v))]; break;
                case OwnAction.Deal: value = _sheet.V[StocksSheets.Side] != 0 ? -v : v; break;
                case OwnAction.Private: value = 0; break;
                default: value = v; break;
            }
            return Own(_market.Ask(action, value, (int)_sheet.V[StocksSheets.Target]));
        }

        private bool Own(OwnWhy why)
        {
            if (why != OwnWhy.None)
            {
                Notifications.Show(NotificationType.Error, "reallife_lst_note_not_done", new Dictionary<string, string> { { "why", ListingText.Why(why) } }, 8f, null, null, true, false);
                return false;
            }
            _sheet.Close();
            return true;
        }
    }
}
