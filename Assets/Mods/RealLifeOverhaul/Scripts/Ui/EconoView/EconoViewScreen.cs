using System;
using System.Collections.Generic;
using UI.Smartphone.Apps.EconoView;
using UnityEngine;
using GameEcono = UI.Smartphone.Apps.EconoView.EconoView;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// EconoView as a screen of the kit, mirrored from the game's own app (EconoViewScreen.Read.cs reads it): the overview's
    /// income statement, cash, shares and last transactions, every transaction under its filters, the loans, the funds,
    /// the taxes and one business's statement, each under its tab across. Every press goes to the game: a heading folds
    /// through the game's own toggle and a business's line opens its page as the game's row does, the filters are chosen
    /// in the game's own drop-downs, an amount is typed into the game's own field, Make payment and Withdraw press the
    /// game's buttons and their question is the game's, Export, Set destination, Open in BizMan and the IRS contact are the
    /// game's. Which tab shows is the kit's for the overview, loans, investments and taxes (the game shows them on one page)
    /// and the game's for every transaction and one business. Cost: a look at a few counts and sums four times a second
    /// while it shows; a layout only when they change.
    /// </summary>
    public sealed partial class EconoViewScreen : IUiScreen, IUiTyping, IUiPointing
    {
        private const float Every = 0.25f;   // seconds between looks at the game's app

        private readonly GameEcono _app;
        private readonly UiEconoModel _m = new UiEconoModel();
        private readonly HashSet<int> _folded = new HashSet<int>();
        private int _page = UiEcono.Overview;   // the kit's choice among the overview, loans, investments and taxes
        private int _period, _businessPeriod;   // the day of the four each statement shows
        private int _openList = -1;
        private int _loan, _business;
        private int _typing = -1;               // the control an amount is typed into
        private int _chartFocus = -1;
        private bool _dirty = true;
        private float _next;
        private object _openedBy;               // the overview's days as the game read them when it last opened: a new list, a new opening
        private long _seen;
        private short _statementClip = -1, _pickClip = -1, _optionClip = -1;

        public EconoViewScreen(GameEcono app)
        {
            _app = app;
        }

        public string Id => EconoViewKit.Id;

        public bool Dirty
        {
            get
            {
                float now = Time.unscaledTime;
                object opened = _app.overview != null ? _app.overview.latestFinancialSummaries : null;
                if (opened != _openedBy)
                {
                    _openedBy = opened;
                    Opened();   // the game's overview read its days again: the app opened (or something of the game's reset it)
                }
                if (now >= _next)
                {
                    _next = now + Every;
                    Follow();
                    long sig = Signature();
                    _dirty |= sig != _seen;
                    _seen = sig;
                }
                return _dirty;
            }
        }

        public int TypingCommand => UiEcono.AmountCommand;

        public string TypingText => Raw(Field(_typing));

        public void Typed(string text)
        {
            Amount(_typing, Digits(text), true);
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            EconoState v = Read();
            EconoViews.Page(_m, UiText.Words, v);
            if (_m.Chart != null)
            {
                _m.Chart.Focus = _chartFocus;
            }
            UiEcono.Lay(s, _m, frame, dark, keys);
            _statementClip = ClipOf(s, _m.StatementList);
            _pickClip = ClipOf(s, _m.PickList);
            _optionClip = ClipOf(s, _m.OptionList);
            _dirty = false;
        }

        private static short ClipOf(UiScene s, short list) => list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            _dirty = true;
            int page = Page();
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return ShowTab(arg);
                case UiAppWindow.ActionCommand:
                    return Action(page, arg);
                case UiAppWindow.RowCommand:
                case UiAppWindow.MoreCommand:
                    return ShowAll();
                case UiEcono.StatementFirst + UiStatement.Period:
                    return OpenList(UiEcono.ListPeriod);
                case UiEcono.StatementFirst + UiStatement.Group:
                    return Fold(page, arg);
                case UiEcono.StatementFirst + UiStatement.Line:
                    return Line(page, arg);
                case UiEcono.FilterCommand:
                    return OpenList(arg == 0 ? UiEcono.ListType : UiEcono.ListAmount);
                case UiEcono.ChoiceCommand:
                    return OpenList(UiEcono.ListBusiness);
                case UiEcono.OptionCommand:
                    return Option(page, arg);
                case UiEcono.PickCommand:
                    return Pick(page, arg);
                case UiEcono.LessCommand:
                case UiEcono.MoreCommand:
                    return Step(page, arg, command == UiEcono.MoreCommand ? 1 : -1);
                case UiEcono.SwitchCommand:
                    return AutoInvest(!Auto());
                case UiEcono.ButtonCommand:
                    return Button(page, arg);
                case UiEcono.LinkCommand:
                    return Link(arg);
                case UiChart.First + UiChart.Point:
                    _chartFocus = _chartFocus == arg ? -1 : arg;
                    return true;
                default:
                    return false;
            }
        }

        public void Focused(int command, int arg)
        {
            if (command == UiEcono.AmountCommand)
            {
                _typing = arg;   // the field the next typing goes into: one target a field, pressed after it takes focus
            }
            else if (command == UiChart.First + UiChart.Point && _chartFocus != arg)
            {
                _chartFocus = arg;
                _dirty = true;
            }
        }

        public void Pointed(int command, int arg)
        {
            int focus = command == UiChart.First + UiChart.Point ? arg : -1;
            if (focus != _chartFocus)
            {
                _chartFocus = focus;
                _dirty = true;
            }
        }

        public bool Back()
        {
            if (_openList >= 0)
            {
                _openList = -1;
                _dirty = true;
                return true;
            }
            if (Page() == UiEcono.Transactions)
            {
                _app.overview.fullTransactionsPanel.Close();   // Escape closes the full transactions first, as the game's HandleEscapeClick does
                _dirty = true;
                return Done(true, "transactions closed");
            }
            return false;
        }

        public void Tab(int step)
        {
            int count = _m.Tabs.Length;
            if (count == 0)
            {
                return;
            }
            int next = ((EconoViews.TabOf(Page()) + step) % count + count) % count;
            ShowTab(next);
            _dirty = true;
        }

        public bool Act(UiAction action)
        {
            int page = Page();
            if (action == UiAction.Alt)
            {
                return page == UiEcono.Loans ? Button(page, 1) : page == UiEcono.Investments && Button(page, 0);
            }
            if (action == UiAction.Option)
            {
                return (page == UiEcono.Transactions || page == UiEcono.Business) && Action(page, 0);
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (list == _m.StatementList)
            {
                _m.Statement.Reveal = index;
            }
            else if (list == _m.PickList && _m.Picks != null)
            {
                _m.Picks.Reveal = index;
            }
            else if (list == _m.OptionList)
            {
                _m.OptionOffset = Math.Max(0f, (index - 3) * UiTokens.ControlSmall);
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            if (clip == _statementClip)
            {
                _m.Statement.Offset = Math.Max(0f, _m.Statement.Offset + by);
            }
            else if (clip == _pickClip && _m.Picks != null)
            {
                _m.Picks.Offset = Math.Max(0f, _m.Picks.Offset + by);
            }
            else if (clip == _optionClip)
            {
                _m.OptionOffset = Math.Max(0f, _m.OptionOffset + by);
            }
            _dirty = true;
        }

        // A tab: one business through the game's own SetBusiness (as its choice does), the rest on the game's overview, the
        // loans, funds and taxes on the game's own banking tab.
        private bool ShowTab(int tab)
        {
            _openList = -1;
            int page = EconoViews.PageOf(tab);
            if (page == UiEcono.Business)
            {
                if (_app.selectedBusiness != null)
                {
                    return true;
                }
                return OpenBusiness(_business);
            }
            if (_app.selectedBusiness != null)
            {
                _app.ResetSelectedBusiness();   // the game's breadcrumb back to its dashboard
                _openedBy = _app.overview.latestFinancialSummaries;   // its overview opened again at the kit's own word: not a new opening
            }
            if (_app.overview.fullTransactionsPanel.isActiveAndEnabled)
            {
                _app.overview.fullTransactionsPanel.Close();
            }
            _page = page;
            Banking(page);
            return true;
        }

        /// <summary>The loans tab, for the tests: shown as its tab shows it.</summary>
        internal void ShowLoans()
        {
            ShowTab(EconoViews.TabOf(UiEcono.Loans));
            _dirty = true;
        }

        private bool OpenBusiness(int index)
        {
            List<BuildingRegistration> all = Businesses();
            if (all == null || all.Count == 0)
            {
                return false;
            }
            _business = Math.Max(0, Math.Min(index, all.Count - 1));
            _app.SetBusiness(all[_business]);
            return Done(true, "a business's statement opened");
        }

        private bool ShowAll()
        {
            _openList = -1;
            _app.overview.fullTransactionsPanel.Show();   // the game's own full view, as its button shows it
            return Done(true, "every transaction shown");
        }

        private bool Action(int page, int arg)
        {
            if (page == UiEcono.Transactions)
            {
                if (arg == 0)
                {
                    _app.overview.fullTransactionsPanel.ExportToCsv();   // the game writes the file and opens its folder
                    return Done(true, "transactions exported by the game");
                }
                _app.overview.fullTransactionsPanel.Close();
                return Done(true, "transactions closed");
            }
            if (page == UiEcono.Business && arg == 0 && _app.selectedBusiness != null)
            {
                _app.businessDetails.SetDestination();
                return Done(true, "destination set by the game");
            }
            return false;
        }

        private bool Link(int arg)
        {
            switch (arg)
            {
                case UiEcono.LinkAll:
                    return ShowAll();
                case UiEcono.LinkIrs:
                    return Done(UiWidgets.Press(IrsButton()), "the IRS contact opened in Contacts");
                case UiEcono.LinkBizMan:
                    if (_app.selectedBusiness == null)
                    {
                        return false;
                    }
                    _app.OpenInBizMan();
                    return Done(true, "the business opened in BizMan");
                default:
                    return false;
            }
        }

        private bool OpenList(int list)
        {
            _openList = _openList == list ? -1 : list;
            _m.OptionOffset = 0f;
            return true;
        }

        // An open list's option: the statement's day is the kit's; a filter, the days and the business are the game's own choices.
        private bool Option(int page, int arg)
        {
            int list = _openList;
            _openList = -1;
            switch (list)
            {
                case UiEcono.ListType:
                    return Choose(GameContract.EconoTypes, arg);
                case UiEcono.ListAmount:
                    return Choose(GameContract.EconoAmounts, arg);
                case UiEcono.ListPeriod:
                    if (page == UiEcono.Transactions)
                    {
                        return Choose(GameContract.EconoDayFilter, arg);
                    }
                    if (page == UiEcono.Business)
                    {
                        _businessPeriod = arg;
                    }
                    else
                    {
                        _period = arg;
                    }
                    return true;
                case UiEcono.ListBusiness:
                    return OpenBusiness(arg);
                default:
                    return false;
            }
        }

        private bool Choose(GameMember dropdown, int option)
        {
            var d = Get<global::UI.Elements.Dropdown>(dropdown, _app.overview.fullTransactionsPanel);
            if (d == null)
            {
                return false;
            }
            d.SelectOption(option);   // its listener filters the game's table again
            return true;
        }

        // A heading folds through the game's own toggle (the overview) or its row's own button (a business); a day of every
        // transaction folds on the kit, the game's table having no days to fold.
        private bool Fold(int page, int group)
        {
            if (page == UiEcono.Transactions)
            {
                int day = DayOfGroup(group);
                if (day < 0)
                {
                    return false;
                }
                if (!_folded.Remove(day))
                {
                    _folded.Add(day);
                }
                return true;
            }
            int row = group >= 0 && group < _m.GroupRows.Length ? _m.GroupRows[group] : -1;
            return page == UiEcono.Business ? ToggleBusiness(row) : ToggleIncome(row);
        }

        // A line: a business or a property opens as the game's row opens it; a transaction filters the table to its kind.
        private bool Line(int page, int listRow)
        {
            int g = UiStatement.GroupOf(_m.Statement, listRow, out int line);
            if (g < 0)
            {
                return false;
            }
            if (line < 0)
            {
                return Fold(page, g);
            }
            if (page == UiEcono.Transactions)
            {
                return FilterTo(g, line);
            }
            if (page == UiEcono.Business || g >= _m.GroupLines.Length || line >= _m.GroupLines[g].Length)
            {
                return false;
            }
            return OpenIncome(_m.GroupLines[g][line]);
        }

        private bool Pick(int page, int arg)
        {
            switch (page)
            {
                case UiEcono.Loans:
                    _loan = arg;
                    _typing = -1;
                    return true;
                case UiEcono.Investments:
                    return PressFund(arg);
                case UiEcono.Taxes:
                    return Done(UiWidgets.Press(IrsButton()), "the IRS contact opened in Contacts");
                default:
                    return false;
            }
        }

        // A minus or a plus: the amount stepped by a round tenth within its ends, written into the game's own field.
        private bool Step(int page, int control, int direction)
        {
            if (!Ends(control, out long value, out long least, out long most))
            {
                return false;
            }
            long next = EconoViews.Stepped(value, direction, least, most);
            if (next == value)
            {
                return false;
            }
            Amount(control, next.ToString(System.Globalization.CultureInfo.InvariantCulture), true);
            return true;
        }

        private bool Button(int page, int control)
        {
            if (page == UiEcono.Loans && control == 1)
            {
                return Done(UiWidgets.Press(PayOffButton()), "make payment pressed: the game asks first, in its own box");
            }
            if (page == UiEcono.Investments && control == 0)
            {
                EconoViewInvestments funds = Investments();
                if (funds == null || !funds.isActiveAndEnabled)
                {
                    return false;
                }
                funds.OnWithdrawClick();   // the withdraw button's own method: the game checks the amount and asks first, in its own box
                return Done(true, "withdraw pressed: the game asks first, in its own box");
            }
            return false;
        }

        // One line a press in Player.log, so a report shows each button reaching the game.
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("EconoView (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }

        private static string Digits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    sb.Append(text[i]);
                }
            }
            return sb.ToString();
        }
    }
}
