using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's face for Career, Campus, Open Roles and the Marketplace: one runner over a source (IWorkSource), so each app builds its pages
    /// and the runner lays them in the app window, routes every press to the action the page names and steps back out of a sub page first.
    /// The page is read again when the source's generation moves or the player's choice changes (a tab, a row, a filter); never every frame.
    /// The old page of each app stays underneath for the per-screen switch.
    /// </summary>
    public sealed class WorkScreen : IUiScreen
    {
        private readonly IWorkSource _source;
        private readonly WorkView _view = new WorkView();
        private WorkPage _page;
        private int _seen = -1;
        private bool _dirty = true, _stale = true;

        public WorkScreen(IWorkSource source)
        {
            _source = source;
        }

        public string Id => _source.Id;

        public bool Dirty => _dirty || _stale || _source.Generation != _seen;

        public WorkView View => _view;

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
            UiAppWindow.Lay(s, m, frame, dark, keys);
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
                case UiAppWindow.RowCommand:
                    if (_page.Has(_page.Rows, arg) && _page.Rows[arg].Opens)
                    {
                        _view.Row = arg;
                        return Run(_page.Rows, arg);
                    }
                    _view.Row = arg;
                    return true;   // a row only chooses what the detail shows
                case UiAppWindow.MoreCommand:
                    return Run(_page.MoreRows, arg);
                case UiAppWindow.PersonCommand:
                    return Run(_page.People, arg);
                case UiAppWindow.AllCommand:
                    return Carry(_page.All);
                case UiAppWindow.NoteCommand:
                    return Carry(_page.Note);
                default:
                    return false;
            }
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

        public void Focused(int command, int arg)
        {
            if (command == UiAppWindow.RowCommand && _view.Row != arg && _page != null && arg >= 0 && arg < _page.Model.Rows.Length)
            {
                _view.Row = arg;
                _stale = true;
                _dirty = true;
            }
        }

        public bool Back()
        {
            if (_page != null && _page.Back && (_view.Sub != 0 || _view.Row >= 0))
            {
                _view.Sub = 0;
                _view.Row = -1;
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
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
