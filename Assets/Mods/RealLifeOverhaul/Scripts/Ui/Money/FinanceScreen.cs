using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Finance on the kit: seven tabs, each its own page of its own facts (FinanceFace). The facts are read from the game when its
    /// generation moves or the player's choice changes (a tab, a row, an amount), never every frame; a button goes to the director that
    /// already owns it through <see cref="IFinanceSource.Do"/>. The old page stays underneath for the per-screen switch.
    /// Nothing a frame; nothing allocated while idle.
    /// </summary>
    public sealed class FinanceScreen : IUiScreen, IUiTypingSubmit
    {
        public const string ScreenId = "finance";

        private readonly IFinanceSource _source;
        private readonly UtilitiesWords _words;
        private readonly FinanceFacts _facts = new FinanceFacts();
        private readonly FinanceView _view = new FinanceView();
        private readonly FinanceLayout _layout = new FinanceLayout();
        private int _seen = -1;
        private bool _dirty = true, _stale = true;
        private int _typingArg;

        public FinanceScreen(IFinanceSource source, UtilitiesWords words)
        {
            _source = source;
            _words = words;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || _stale || (_source != null && _source.Generation != _seen);

        /// <summary>The facts as last read, for the checks.</summary>
        public FinanceFacts Facts => _facts;

        public FinanceView View => _view;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            int wanted = _source != null ? _source.TakeWanted() : -1;
            if (wanted >= 0 && wanted < FinanceFace.Tabs)
            {
                _view.Tab = wanted;
                _stale = true;
            }
            if (_source != null && (_stale || _seen != _source.Generation))
            {
                _seen = _source.Generation;
                _source.Read(_facts, _view);
            }
            _stale = false;
            FinanceFace.Lay(s, _facts, _view, _words, _layout, frame, dark, keys);
            _dirty = false;
        }

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            CommitTyping();
            bool done = Route(command, arg);
            if (done)
            {
                _stale = true;
                _dirty = true;
            }
            return done;
        }

        private bool Route(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                return ShowTab(arg);
            }
            if (command == UiAppWindow.ActionCommand)
            {
                FinanceAct[] acts = FinanceFace.ProfileActs(_facts, _view.Tab);
                return arg >= 0 && arg < acts.Length && Run(acts[arg], 0);
            }
            if (command == FinanceFace.TableA + UiDataTable.Row)
            {
                return Select(arg, true);
            }
            if (command == FinanceFace.TableB + UiDataTable.Row || command == FinanceFace.TableC + UiDataTable.Row)
            {
                return true;   // a list to read (who borrowed, what moved the score): a press asks nothing of the game
            }
            switch (command)
            {
                case FinanceFace.CardAct:
                    FinanceFace.Split(arg, out FinanceAct act, out int value);
                    return Run(act, value);
                case FinanceFace.StepLess:
                case FinanceFace.StepMore:
                    return Step(arg, command == FinanceFace.StepMore);
                case FinanceFace.AmountLess:
                case FinanceFace.AmountMore:
                    return Amount(arg, command == FinanceFace.AmountMore);
                case FinanceFace.AmountType:
                    _typingArg = arg;
                    return true;
                case FinanceFace.Period:
                    return arg >= 0 && arg < _facts.Cash.Periods.Length && SetPeriod(arg);
                case UiStatement.First + UiStatement.Period:
                    return SetPeriod((_view.Period + 1) % Math.Max(1, _facts.Cash.Periods.Length));
                case UiStatement.First + UiStatement.Group:
                    return Fold(arg);
                case UiStatement.First + UiStatement.Line:
                    return true;   // a line of the statement is read, a stop for the keys and the pad on the way down the list: a press asks nothing of the game
                case FinanceFace.Answer:
                    return Answer(arg);
                default:
                    return false;
            }
        }

        private bool ShowTab(int tab)
        {
            if (tab < 0 || tab >= FinanceFace.Tabs)
            {
                return false;
            }
            _view.Tab = tab;
            _view.Typing = null;
            return true;
        }

        private bool SetPeriod(int period)
        {
            _view.Period = period;
            return true;
        }

        // A day's band on Cash flow folds its lines away and brings them back, as its mark shows (the band was drawn with the
        // mark and no press answered it: the error sound alone). The player's own choice, kept by the band's words, which the
        // page applies when it lays the statement (FinanceFace.Folds): the facts are written afresh whenever the game's move.
        private bool Fold(int group)
        {
            UiStatementModel statement = _facts.Cash.Statement;
            if (statement == null || group < 0 || group >= statement.Groups.Length)
            {
                return false;
            }
            string day = statement.Groups[group].Label ?? string.Empty;
            if (!_view.Folded.Remove(day))
            {
                _view.Folded.Add(day);
            }
            return true;
        }

        // A row of the table chosen: what the detail beside it shows. On Overview a debt takes you to the accounts.
        private bool Select(int row, bool pressed)
        {
            if (row < 0)
            {
                return false;
            }
            switch (_view.Tab)
            {
                case FinanceFace.Overview:
                    return pressed && ShowTab(FinanceFace.Accounts);
                case FinanceFace.Credit:
                    _view.CreditRow = row;
                    return true;
                case FinanceFace.Borrow:
                    if (_view.Offer != row)
                    {
                        _view.Amount = _view.Deposit = _view.Term = _view.Pick = 0;
                    }
                    _view.Offer = row;
                    return true;
                case FinanceFace.Accounts:
                    if (row >= _facts.Accounts.Ids.Length)
                    {
                        return false;
                    }
                    _view.Account = _facts.Accounts.Ids[row];
                    _view.Pay = 0;
                    return true;
                case FinanceFace.Rivals:
                    if (_view.Rival != row)
                    {
                        _view.Answer = 0;
                    }
                    _view.Rival = row;
                    return true;
                case FinanceFace.Standing:
                    if (row >= _facts.Standing.Ids.Length)
                    {
                        return false;
                    }
                    _view.Standing = _facts.Standing.Ids[row];
                    return true;
                default:
                    return false;
            }
        }

        // A step on the card (a finance's deposit or term, the shop or the bank): the figure moves to the value the facts named.
        private bool Step(int index, bool more)
        {
            FinanceQuoteFacts q = _facts.Borrow.Quote;
            if (_view.Tab != FinanceFace.Borrow || q == null || index < 0 || index >= q.StepMoves.Length)
            {
                return false;
            }
            FinanceStep step = q.StepMoves[index];
            int to = more ? step.Next : step.Prev;
            if (to < 0)
            {
                return false;
            }
            switch (step.Which)
            {
                case FinanceFace.StepDeposit: _view.Deposit = to; break;
                case FinanceFace.StepTerm: _view.Term = to; break;
                default: _view.Pick = to; break;
            }
            return true;
        }

        // Minus and plus on an amount: the Borrow amount (0) or the amount to pay (1).
        private bool Amount(int which, bool more)
        {
            if (which == FinanceFace.ArgAmount && _view.Tab == FinanceFace.Borrow && _facts.Borrow.Quote != null)
            {
                int to = more ? _facts.Borrow.Quote.AmountNext : _facts.Borrow.Quote.AmountPrev;
                if (to < 0)
                {
                    return false;
                }
                _view.Amount = to;
                return true;
            }
            if (which == FinanceFace.ArgPay && _view.Tab == FinanceFace.Accounts)
            {
                int to = more ? _facts.Accounts.PayNext : _facts.Accounts.PayPrev;
                if (to < 0)
                {
                    return false;
                }
                _view.Pay = to;
                return true;
            }
            return false;
        }

        private bool Answer(int which)
        {
            FinanceAnswer[] list = _facts.Rivals.Answers;
            if (which < 0)
            {
                which = 0;
            }
            if (which >= list.Length || !list[which].On)
            {
                return false;
            }
            _view.Answer = which;
            return Run(list[which].Act, list[which].Arg);
        }

        // Every button but the ones that only move the page: the director that owns the action does it, asking first where it spends money.
        private bool Run(FinanceAct act, int arg)
        {
            if (act == FinanceAct.Apply)
            {
                return Apply();
            }
            return _source != null && _source.Do(act, _view, arg);
        }

        // Apply on Credit: the offer chosen opens on Borrow, with its amount.
        private bool Apply()
        {
            if (_facts.Credit.Offers == null || _view.CreditRow < 0 || _view.CreditRow >= _facts.Credit.Offers.RowCount)
            {
                return false;
            }
            _view.Offer = _view.CreditRow;
            _view.Amount = _view.Deposit = _view.Term = _view.Pick = 0;
            _view.Tab = FinanceFace.Borrow;
            return true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => FinanceFace.AmountType;

        public int TypingLimit => 9;

        public string TypingText => _view.Typing ?? string.Empty;

        public void Typed(string text)
        {
            _view.Typing = Digits(text);
            _dirty = true;
        }

        public void Submitted(string text)
        {
            _view.Typing = Digits(text);
            CommitTyping();
            _stale = true;
            _dirty = true;
        }

        private void CommitTyping()
        {
            if (_view.Typing == null)
            {
                return;
            }
            string typed = _view.Typing;
            _view.Typing = null;
            if (typed.Length == 0 || !int.TryParse(typed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value))
            {
                return;
            }
            if (_typingArg == FinanceFace.ArgAmount && _view.Tab == FinanceFace.Borrow)
            {
                _view.Amount = value;
            }
            else if (_typingArg == FinanceFace.ArgPay && _view.Tab == FinanceFace.Accounts)
            {
                _view.Pay = value;
            }
        }

        private static string Digits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var chars = new char[Math.Min(text.Length, 9)];
            int n = 0;
            for (int i = 0; i < text.Length && n < chars.Length; i++)
            {
                if (text[i] >= '0' && text[i] <= '9')
                {
                    chars[n++] = text[i];
                }
            }
            return new string(chars, 0, n);
        }

        // ------------------------------------------------------------------------------------------------- the host's calls

        public void Focused(int command, int arg)
        {
            if (command == FinanceFace.AmountType)
            {
                _typingArg = arg;
                _view.Typing = null;
                return;
            }
            if (command == FinanceFace.TableA + UiDataTable.Row && _view.Tab != FinanceFace.Overview && Select(arg, false))
            {
                _stale = true;
                _dirty = true;
            }
        }

        public bool Back()
        {
            if (_view.Typing == null)
            {
                return false;
            }
            _view.Typing = null;
            _dirty = true;
            return true;
        }

        public void Tab(int step)
        {
            CommitTyping();
            _view.Tab = (_view.Tab + step + FinanceFace.Tabs) % FinanceFace.Tabs;
            _stale = true;
            _dirty = true;
        }

        // F runs the page's main button and P its second: the old app's own keys.
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            CommitTyping();
            bool has = action == UiAction.Alt ? FinanceFace.Main(_facts, _view, _words, out UiActionModel model) : FinanceFace.Second(_facts, _view, _words, out model);
            if (!has || model.Disabled)
            {
                return false;
            }
            bool done = Route(model.Command, model.Arg);
            if (done)
            {
                _stale = true;
                _dirty = true;
            }
            return done;
        }

        public void Reveal(int list, int index)
        {
            int slot = _layout.SlotOfList(list);
            if (slot >= 0)
            {
                _layout.Reveal[slot] = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            int slot = _layout.SlotOfClip(clip);
            if (slot >= 0)
            {
                _layout.Offset[slot] = Math.Max(0f, _layout.Offset[slot] - notches * UiTokens.S16);
                _dirty = true;
            }
        }
    }
}
