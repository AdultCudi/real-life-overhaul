using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's face for Career, Campus, Open Roles and the Marketplace: one runner over a source (IWorkSource), so each app builds its pages
    /// and the runner lays them in the app window, routes every press to the action the page names and steps back out of a sub page first.
    /// The page is read again when the source's generation moves or the player's choice changes (a tab, a row, a filter, a step); never every
    /// frame. The old page of each app stays underneath for the per-screen switch. The screen's own commands (1200 to 1219) and the kit's parts'
    /// are told apart by the page: a number that is another part's is never answered.
    /// </summary>
    public sealed class WorkScreen : IUiScreen, IUiTyping, IUiStepping
    {
        private readonly IWorkSource _source;
        private readonly WorkView _view = new WorkView();
        private WorkPage _page;
        private int _seen = -1;
        private bool _dirty = true, _stale = true;
        private int _typeCommand = WorkCmd.Search, _typeArg;

        public WorkScreen(IWorkSource source)
        {
            _source = source;
        }

        public string Id => _source.Id;

        public bool Dirty => _dirty || _stale || _source.Generation != _seen;

        public WorkView View => _view;

        public IWorkSource Source => _source;

        /// <summary>The page as last built, for the checks.</summary>
        public WorkPage Page => _page;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            int wanted = _source.TakeWanted();
            if (wanted >= 0)
            {
                _view.Tab = wanted;
                _view.Reset();
                _stale = true;
            }
            if (_stale || _seen != _source.Generation || _page == null)
            {
                _seen = _source.Generation;
                _page = _source.Page(_view);
            }
            _stale = false;
            UiAppModel m = _page.Model;
            m.Title = _source.Title;
            m.Icon = _source.Icon;
            m.Nav = _source.Nav(_view);
            m.NavSelected = Math.Max(0, Math.Min(Math.Max(0, m.Nav.Length - 1), _view.Tab));
            m.TabsAcross = _source.Across;
            m.PromptCount = 0;
            m.Prompts = new UiPrompt[4];
            AddPrompt(m, UiAction.Move, "reallife_ui_prompt_move", "Move");
            AddPrompt(m, UiAction.Confirm, "reallife_ui_prompt_select", "Select");
            AddPrompt(m, UiAction.Back, "reallife_ui_prompt_back", "Back");
            if (Main(out UiActionModel main) && !main.Disabled)
            {
                m.Prompts[m.PromptCount++] = new UiPrompt { Action = UiAction.Alt, Label = main.Label };
            }
            if (_page.Body != null)
            {
                s.Begin("app window", frame.Width, frame.Height, dark);
                UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.NavSelected, m.TabsAcross, keys, out float x, out float y, out float w, out float bottom);
                _page.Body(s, at, x, y, w, bottom, keys);
                for (int i = 0; i < m.PromptCount; i++)
                {
                    s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
                }
                UiDraw.PromptBar(s, frame, keys);
            }
            else
            {
                UiAppWindow.Lay(s, m, frame, dark, keys);
            }
            if (_page.Table != null)
            {
                _view.Offset = _page.Table.Offset;
            }
            else if (_page.Cards != null)
            {
                _view.Offset = _page.Cards.Offset;
            }
            _view.Reveal = -1;
            _dirty = false;
        }

        private void AddPrompt(UiAppModel m, UiAction action, string key, string english)
        {
            m.Prompts[m.PromptCount++] = new UiPrompt { Action = action, Label = _source.T(key, english) };
        }

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            if (_page == null)
            {
                return false;
            }
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
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return ShowTab(arg);
                case UiAppWindow.ActionCommand:
                    return Run(_page.Acts, arg);
                case UiDataTable.First + UiDataTable.Row:
                    return Row(arg);
                case WorkCmd.Card:
                    if (_page.Cards == null || arg < 0 || arg >= _page.Cards.Cards.Length)
                    {
                        return false;
                    }
                    _view.Row = arg;
                    return true;   // a card chooses what the detail beside shows
                case UiDataTable.First + UiDataTable.Act:
                    return Run(_page.Locates, arg);
                case UiDataTable.First + UiDataTable.Clear:
                    return Carry(_page.Empty);
                case WorkCmd.Aside:
                    if (arg < 0 || arg >= _page.Asides.Length)
                    {
                        return false;
                    }
                    _view.Aside = arg;
                    _view.Row = -1;
                    _view.Offset = 0f;
                    return true;
                case WorkCmd.PageTab:
                    _view.PageTab = Math.Max(0, arg);
                    return true;
                case WorkCmd.Search:
                    _typeCommand = WorkCmd.Search;
                    _typeArg = 0;
                    return _page.Typing;   // a press on the field starts the typing, which the host carries
                case WorkCmd.Field:
                    _typeCommand = WorkCmd.Field;
                    _typeArg = arg;
                    return true;
                case WorkCmd.Type:
                    _typeCommand = WorkCmd.Type;
                    _typeArg = arg;
                    return arg >= 0 && arg < _page.Amounts.Length;
                case WorkCmd.Less:
                    return Step(arg, -1);
                case WorkCmd.More:
                    return Step(arg, 1);
                case WorkCmd.Pick:
                    return Cycle(arg);
                case UiDiary.DayCommand:
                    if (_page.Diary == null || arg < 1 || arg > _page.Diary.Length)
                    {
                        return false;
                    }
                    _view.Day = arg;
                    return true;
                case UiDiary.MonthCommand:
                    if (_page.Diary == null || (arg < 0 ? !_page.Diary.CanBack : !_page.Diary.CanOn))
                    {
                        return false;
                    }
                    _view.Month += arg < 0 ? -1 : 1;
                    _view.Day = 0;
                    return true;
                case UiDiary.EntryCommand:
                    return Run(_page.Entries, arg);
                case UiAppWindow.RowCommand:
                case UiAppWindow.MoreCommand:
                case UiAppWindow.PersonCommand:
                    return true;   // a line of the recent shifts or the people on the shift is read, never acted on: its figures are its answer
                case UiReader.AnchorCommand:
                case UiReader.RelatedCommand:
                case UiReader.ContentsCommand:
                case UiChart.First + UiChart.Point:
                case UiTrack.First + UiTrack.Step:
                    return _page.Reader != null || command == UiChart.First + UiChart.Point || command == UiTrack.First + UiTrack.Step;   // read, never acted on
                default:
                    return false;
            }
        }

        private bool Row(int arg)
        {
            if (_page.Table == null || arg < 0 || arg >= _page.Table.RowCount)
            {
                return false;
            }
            _view.Row = arg;
            if (_page.Has(_page.Rows, arg) && _page.Rows[arg].Opens)
            {
                return Carry(_page.Rows[arg]);
            }
            return true;   // a row only chooses what the detail shows
        }

        private bool ShowTab(int tab)
        {
            UiNavEntry[] nav = _source.Nav(_view);
            if (tab < 0 || tab >= nav.Length)
            {
                return false;
            }
            _view.Tab = tab;
            _view.Reset();
            return true;
        }

        private bool Run(WorkDo[] list, int i)
        {
            return _page.Has(list, i) && Carry(list[i]);
        }

        private bool Carry(WorkDo act)
        {
            if (!act.Any || act.Why != null)
            {
                return false;
            }
            switch (act.Kind)
            {
                case WorkKinds.Filter:
                    _view.Filter = act.Arg;
                    _view.Row = -1;
                    _view.Offset = 0f;
                    return true;
                case WorkKinds.GoTab:
                    _view.Tab = act.Arg;
                    _view.Reset();
                    return true;
                case WorkKinds.Back:
                    _view.Sub = 0;
                    _view.Row = -1;
                    return true;
            }
            return _source.Do(act, _view);
        }

        // ------------------------------------------------------------------------------------------------- amounts

        private bool Step(int id, int step)
        {
            if (id < 0 || id >= _page.Amounts.Length)
            {
                return false;
            }
            WorkAmount a = _page.Amounts[id];
            int v = Math.Max(a.Min, Math.Min(a.Max, a.Value + step * Math.Max(1, a.Step)));
            if (v == a.Value)
            {
                return false;
            }
            _view.Set(a.Id, v);
            return true;
        }

        // A choice of a few words: a press goes on to the next, from the last back to the first.
        private bool Cycle(int id)
        {
            if (id < 0 || id >= _page.Amounts.Length || _page.Amounts[id].Words == null)
            {
                return false;
            }
            WorkAmount a = _page.Amounts[id];
            _view.Set(a.Id, a.Value >= a.Max ? a.Min : a.Value + 1);
            return true;
        }

        public bool Steps(int command) => command == WorkCmd.Pick && _page != null && _page.Amounts.Length > 0;

        public void Step(int command, int arg, int step)
        {
            if (command != WorkCmd.Pick || _page == null || arg < 0 || arg >= _page.Amounts.Length)
            {
                return;
            }
            WorkAmount a = _page.Amounts[arg];
            int v = a.Value + step;
            v = v > a.Max ? a.Min : v < a.Min ? a.Max : v;
            _view.Set(a.Id, v);
            _stale = true;
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => _typeCommand;

        public string TypingText
        {
            get
            {
                if (_typeCommand == WorkCmd.Search)
                {
                    return _view.Query ?? string.Empty;
                }
                if (_typeCommand == WorkCmd.Field)
                {
                    return _view.Text ?? string.Empty;
                }
                return _page != null && _typeArg >= 0 && _typeArg < _page.Amounts.Length ? _page.Amounts[_typeArg].Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            }
        }

        public void Typed(string text)
        {
            if (_typeCommand == WorkCmd.Search)
            {
                _view.Query = text ?? string.Empty;
                _view.Row = -1;
                _view.Offset = 0f;
            }
            else if (_typeCommand == WorkCmd.Field)
            {
                _view.Text = text ?? string.Empty;
            }
            else if (_page != null && _typeArg >= 0 && _typeArg < _page.Amounts.Length)
            {
                WorkAmount a = _page.Amounts[_typeArg];
                int digits = 0, typed = 0;
                bool any = false;
                for (int i = 0; text != null && i < text.Length && typed < 9; i++)
                {
                    if (text[i] >= '0' && text[i] <= '9')
                    {
                        digits = digits * 10 + (text[i] - '0');
                        typed++;
                        any = true;
                    }
                }
                _view.Set(a.Id, any ? Math.Max(a.Min, Math.Min(a.Max, digits)) : a.Min);
            }
            _stale = true;
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- the host's calls

        public void Focused(int command, int arg)
        {
            if (_page == null)
            {
                return;
            }
            if (command == UiDataTable.First + UiDataTable.Row && _view.Row != arg && _page.Table != null && arg >= 0 && arg < _page.Table.RowCount)
            {
                _view.Row = arg;
                _stale = true;
                _dirty = true;
            }
            else if (command == WorkCmd.Card && _view.Row != arg && _page.Cards != null && arg >= 0 && arg < _page.Cards.Cards.Length)
            {
                _view.Row = arg;
                _stale = true;
                _dirty = true;
            }
        }

        public bool Back()
        {
            if (_view.Sub != 0)
            {
                _view.Sub = 0;
                _view.Row = -1;
                _view.PageTab = 0;
                _stale = true;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            int n = _source.Nav(_view).Length;
            if (n == 0)
            {
                return;
            }
            _view.Tab = (_view.Tab + step + n) % n;
            _view.Reset();
            _stale = true;
            _dirty = true;
        }

        // F runs the page's first button and P its second, as the old apps' keys did.
        public bool Act(UiAction action)
        {
            if (_page == null || (action != UiAction.Alt && action != UiAction.Option))
            {
                return false;
            }
            int at = action == UiAction.Alt ? Index(UiAction.Alt) : Index(UiAction.Option);
            if (at < 0 || _page.Model.Actions[at].Disabled)
            {
                return false;
            }
            return Press(UiAppWindow.ActionCommand, at);
        }

        private int Index(UiAction key)
        {
            UiActionModel[] a = _page.Model.Actions;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Key == key)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool Main(out UiActionModel main)
        {
            main = default(UiActionModel);
            int at = _page != null ? Index(UiAction.Alt) : -1;
            if (at < 0)
            {
                return false;
            }
            main = _page.Model.Actions[at];
            return true;
        }

        public void Reveal(int list, int index)
        {
            if (_page == null)
            {
                return;
            }
            _view.Reveal = _page.Cards != null ? UiCatalogue.CardOf(_page.Cards.Columns, _page.Cards.FirstList, list, index) : index;
            _stale = true;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _view.Offset = Math.Max(0f, _view.Offset - notches * UiTokens.S16);
            _stale = true;
            _dirty = true;
        }
    }
}
