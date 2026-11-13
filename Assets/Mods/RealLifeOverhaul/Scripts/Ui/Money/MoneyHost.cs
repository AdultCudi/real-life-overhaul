using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The money screens that are a bank's counter (Banks, Your bank) share one host: a model, a fill when the series, the hour or a
    /// press changes it, the page drawer, and what a press does. A press either changes what the player has chosen (a bank, a row, an
    /// amount, a step: <see cref="MoneyView"/>, the host's own), or goes to the source's <see cref="IMoneyActs"/> as one act of the
    /// game. The screens that derive from it (BanksScreen, OwnBankScreen) are the typing screens: they name IUiTypingSubmit and
    /// take its members from here. Nothing a frame.
    /// </summary>
    public class MoneyHost
    {
        private readonly string _id;
        private readonly MoneyModel _model = new MoneyModel();
        private readonly IMoneySource _source;
        private bool _dirty = true;
        private bool _refill = true;   // the page is filled again from the game after a press; a scroll or a focus only lays it out again
        private int _seen = -1;
        private int _typingArg = -1;
        private float _offTable, _offTable2, _offStatement;   // where each list is scrolled to: a fill makes new tables, so the host keeps the place
        private int _revealTable = -1, _revealTable2 = -1, _revealStatement = -1;

        public MoneyHost(string id, MoneyApp app, UiIcon icon, IMoneySource source)
        {
            _id = id;
            _model.App = app;
            _model.Icon = icon;
            _source = source;
        }

        public string Id => _id;

        /// <summary>The model as last filled, for the checks.</summary>
        public MoneyModel Model => _model;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            int wanted = _source is IUiTabWant asked ? asked.TakeWanted() : -1;
            if (wanted >= 0)
            {
                _model.Tab = wanted;   // the fill clamps it to the tabs the app has
                _model.View.Row = null;
                _offTable = _offTable2 = _offStatement = 0f;
                _refill = true;
            }
            if (_source != null && (_refill || _source.Generation != _seen))
            {
                _model.Reset();   // a tab never inherits a part (the statement, a chart, a button) from the one before it
                _source.Fill(_model);
                _refill = false;
            }
            ShowTyping();
            if (_model.Table != null)
            {
                _model.Table.Offset = _offTable;
                _model.Table.Reveal = _revealTable;
            }
            if (_model.Table2 != null)
            {
                _model.Table2.Offset = _offTable2;
                _model.Table2.Reveal = _revealTable2;
            }
            if (_model.Statement != null)
            {
                _model.Statement.Offset = _offStatement;
                _model.Statement.Reveal = _revealStatement;
            }
            MoneyPages.Lay(s, _model, frame, dark, keys);
            _offTable = _model.Table != null ? _model.Table.Offset : 0f;
            _offTable2 = _model.Table2 != null ? _model.Table2.Offset : 0f;
            _offStatement = _model.Statement != null ? _model.Statement.Offset : 0f;
            _revealTable = _revealTable2 = _revealStatement = -1;
            _dirty = false;
            _seen = _source != null ? _source.Generation : 0;
        }

        /// <summary>The page is filled again at the next layout (the checks edit the model's choices directly).</summary>
        public void Invalidate() => _dirty = _refill = true;

        public bool Press(int command, int arg)
        {
            Commit();
            bool done = Route(command, arg);
            if (done)
            {
                _dirty = _refill = true;
            }
            return done;
        }

        private IMoneyActs Acts => _source as IMoneyActs;

        private bool Route(int command, int arg)
        {
            MoneyView view = _model.View;
            if (command == UiAppWindow.NavCommand)
            {
                // The kit's command 1 is shared (a decision's choice, a dashboard's tab): this screen takes it as its own tab only.
                if (arg < 0 || (_model.Nav.Length > 0 && arg >= _model.Nav.Length))
                {
                    return false;
                }
                _model.Tab = arg;
                view.Row = null;
                view.F[MoneyView.FPay] = view.F[MoneyView.FDraw] = view.F[MoneyView.FPut] = view.F[MoneyView.FTake] = 0;
                _offTable = _offTable2 = _offStatement = 0f;
                return true;
            }
            if (command == MoneyCommands.PickBank)
            {
                if (arg < 0 || arg >= _model.PickNames.Length)
                {
                    return false;
                }
                view.Reset(false);
                _offTable = _offTable2 = _offStatement = 0f;
                view.Bank = arg;
                return true;
            }
            if (command == MoneyCommands.Go)
            {
                if (arg < 0 || (_model.Nav.Length > 0 && arg >= _model.Nav.Length))
                {
                    return false;
                }
                _model.Tab = arg;
                view.Row = null;
                return true;
            }
            if (command == MoneyCommands.BackOut)
            {
                return Back();
            }
            if (command == UiDataTable.First + UiDataTable.Row)
            {
                return PickRow(_model.RowIds, arg);
            }
            if (command == UiStatement.First + UiStatement.Period)
            {
                int n = _model.PeriodNames.Length;
                if (n < 2)
                {
                    return false;
                }
                view.F[MoneyView.FPeriod] = (view.F[MoneyView.FPeriod] + 1) % n;
                return true;
            }
            if (command == MoneyCommands.CardAct)
            {
                return Acts != null && Acts.Do((MoneyAct)(arg & 0xFF), view, arg >> 8, 0);
            }
            if (command >= MoneyCommands.CardLess && command < MoneyCommands.CardLess + MoneyCommands.CardsMost)
            {
                return Step(command - MoneyCommands.CardLess, arg % 8, false);
            }
            if (command >= MoneyCommands.CardMore && command < MoneyCommands.CardMore + MoneyCommands.CardsMost)
            {
                return Step(command - MoneyCommands.CardMore, arg % 8, true);
            }
            if (command >= MoneyCommands.CardSwitch && command < MoneyCommands.CardSwitch + MoneyCommands.CardsMost)
            {
                int k = command - MoneyCommands.CardSwitch;
                if (k >= _model.Cards.Length || k >= _model.CardSwitchActs.Length || _model.CardSwitchActs[k] == 0 || _model.Cards[k].SwitchDisabled)
                {
                    return false;
                }
                return Acts != null && Acts.Do((MoneyAct)_model.CardSwitchActs[k], view, k, _model.Cards[k].SwitchOn ? 0 : 1);
            }
            return _source != null && _source.Press(_model, command, arg);
        }

        // A row of the table chosen: the view keeps which, and the page shows what belongs to it.
        private bool PickRow(string[] ids, int row)
        {
            if (row < 0 || row >= ids.Length || string.IsNullOrEmpty(ids[row]))
            {
                return false;
            }
            MoneyView view = _model.View;
            string id = ids[row];
            if (view.Row != id)
            {
                // A new row starts its amounts over.
                view.F[MoneyView.FPay] = view.F[MoneyView.FDraw] = view.F[MoneyView.FPut] = view.F[MoneyView.FTake] = 0;
                if (id.StartsWith("p:", StringComparison.Ordinal))
                {
                    view.F[MoneyView.FAmount] = view.F[MoneyView.FDeposit] = view.F[MoneyView.FTerm] = view.F[MoneyView.FShop] = 0;
                    if (int.TryParse(id.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int product))
                    {
                        view.F[MoneyView.FProduct] = product;
                    }
                }
            }
            view.Row = id;
            return true;
        }

        private bool Step(int card, int index, bool more)
        {
            if (card < 0 || card >= _model.CardSteps.Length || index < 0 || index >= _model.CardSteps[card].Length)
            {
                return false;
            }
            MoneyStep step = _model.CardSteps[card][index];
            if (more ? !step.CanMore : !step.CanLess)
            {
                return false;
            }
            int to = more ? step.More : step.Less;
            if (step.Act == MoneyAct.None)
            {
                _model.View.Set(step.Field, to);
                return true;
            }
            return Acts != null && Acts.Do(step.Act, _model.View, step.Id, to);
        }

        public void Focused(int command, int arg)
        {
            if (command == MoneyCommands.TypeField)
            {
                _typingArg = arg;
                _model.Typing = null;
                return;
            }
            if (command == UiDataTable.First + UiDataTable.Row && _model.Table != null && _model.Table.Selected != arg)
            {
                _model.Table.Selected = arg;
                _dirty = true;
            }
        }

        /// <summary>Back steps out of the typing, then out of a sub page (a row picked), and only then lets the game close the screen.</summary>
        public bool Back()
        {
            if (_model.Typing != null)
            {
                _model.Typing = null;
                _dirty = _refill = true;
                return true;
            }
            if (_model.View.Row != null)
            {
                _model.View.Row = null;
                _dirty = _refill = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            Commit();
            int n = _model.Nav != null ? _model.Nav.Length : 0;
            if (n == 0)
            {
                return;
            }
            int next = _model.Tab + step;
            if (next < 0)
            {
                next = n - 1;
            }
            else if (next >= n)
            {
                next = 0;
            }
            _model.Tab = next;
            _offTable = _offTable2 = _offStatement = 0f;
            _model.View.Row = null;
            _dirty = _refill = true;
        }

        /// <summary>F runs the page's main button and P its second: the first card button of each key.</summary>
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            Commit();
            for (int k = 0; k < _model.Cards.Length; k++)
            {
                UiActionModel[] list = _model.Cards[k].Actions;
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].Key == action && !list[i].Disabled)
                    {
                        bool done = Route(list[i].Command, list[i].Arg);
                        _dirty = _refill = true;
                        return done;
                    }
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (_model.Statement != null && list == _model.StatementList)
            {
                _revealStatement = index;
                _dirty = true;
            }
            if (_model.Table != null && list == _model.TableList)
            {
                _revealTable = index;
                _dirty = true;
            }
            if (_model.Table2 != null && list == _model.Table2List)
            {
                _revealTable2 = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            if (_model.Statement != null && (clip == _model.StatementClip || _model.StatementClip < 0))
            {
                float next = _offStatement - notches * UiTokens.TableRow;
                _offStatement = next < 0f ? 0f : next;
                _dirty = true;
            }
            if (_model.Table != null && (clip == _model.TableClip || _model.TableClip < 0))
            {
                float next = _offTable - notches * UiTokens.TableRow;
                _offTable = next < 0f ? 0f : next;
                _dirty = true;
            }
            if (_model.Table2 != null && clip == _model.Table2Clip)
            {
                float next = _offTable2 - notches * UiTokens.TableRow;
                _offTable2 = next < 0f ? 0f : next;
                _dirty = true;
            }
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => MoneyCommands.TypeField;

        public int TypingLimit => 9;

        public string TypingText => _model.Typing ?? string.Empty;

        public void Typed(string text)
        {
            _model.Typing = Digits(text);
            _dirty = _refill = true;
        }

        public void Submitted(string text)
        {
            _model.Typing = Digits(text);
            Commit();
            _dirty = _refill = true;
        }

        // The digits typed become the step's figure, kept to the range the step names; the page is filled again with it.
        private void Commit()
        {
            string typed = _model.Typing;
            if (typed == null)
            {
                return;
            }
            _model.Typing = null;
            int card = _typingArg / 8, index = _typingArg % 8;
            if (typed.Length == 0 || !int.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || card < 0 || card >= _model.CardSteps.Length
                || index >= _model.CardSteps[card].Length)
            {
                return;
            }
            MoneyStep step = _model.CardSteps[card][index];
            if (!step.Typed)
            {
                return;
            }
            value = Math.Max(step.Low, Math.Min(Math.Max(step.Low, step.High), value));
            if (step.Multiple > 1)
            {
                value = Math.Max(step.Low, value / step.Multiple * step.Multiple);
            }
            if (step.Act == MoneyAct.None)
            {
                _model.View.Set(step.Field, value);
            }
            _dirty = _refill = true;
        }

        // The digits being typed stand in the field of the step they are for, so the player sees what is typed.
        private void ShowTyping()
        {
            if (_model.Typing == null)
            {
                return;
            }
            int card = _typingArg / 8, index = _typingArg % 8;
            if (card >= 0 && card < _model.Cards.Length && index < _model.Cards[card].Steps.Length && _model.Cards[card].Steps[index].TypeCommand != 0)
            {
                _model.Cards[card].Steps[index].Value = _model.Typing;
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
    }
}
