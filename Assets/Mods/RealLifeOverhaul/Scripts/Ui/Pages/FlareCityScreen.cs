using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare in the city as a screen of the kit: the player's account, the trends and the offers as tabs of one feed, a
    /// business's page where an entry leads, Back the way it came (onto the entry it left) and closing at the tabs. The
    /// account's followers stand at the side of every tab. Each view is worked out by Flare's director when it opens.
    /// Cost: a view and a layout a move, nothing per frame.
    /// </summary>
    public sealed class FlareCityScreen : IUiScreen
    {
        private static readonly string[] TabKeys = { "reallife_ui_flare_tab_account", "reallife_ui_flare_tab_trends", "reallife_ui_flare_tab_offers" };

        private readonly Stack<Step> _way = new Stack<Step>();
        private readonly UiDashboardScroll _scroll = new UiDashboardScroll();
        private string _place;     // the business shown, or null for the tab's own view
        private int _tab;
        private FlareCityView _view;
        private bool _dirty = true;
        private bool _fresh = true;
        private int _focusArg = -1;

        private struct Step
        {
            public string Place;
            public int Arg;        // the entry pressed to leave it: Back focuses it again
        }

        public Action Close;

        public string Id => "flare_city";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            FlareDirector d = FlareDirector.Instance;
            _view = d == null ? null : _place != null ? d.CityBusinessView(_place, null) : _tab == 1 ? d.CityTrendsView() : _tab == 2 ? d.CityOffersView() : d.CityAccountView();
            string[] tabs = _place != null ? null : new[] { UiText.T(TabKeys[0]), UiText.T(TabKeys[1]), UiText.T(TabKeys[2]) };
            FlareCityView account = _view != null && _place == null && _tab != 0 && d != null ? d.CityAccountView() : null;
            UiDashboardModel m;
            if (_view != null)
            {
                m = FlareCityPages.Model(_view, UiText.Words, n => UiText.Money(n), d.ResidentName, tabs, _tab, account, _place);
            }
            else
            {
                m = new UiDashboardModel { Shape = UiPageShape.Feed, Tabs = tabs ?? new string[0], Tab = _tab, Table = new UiTableModel { EmptyTitle = UiText.T("reallife_ui_page_empty") } };
                m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") } };
                m.PromptCount = 1;
            }
            _scroll.Apply(m);
            UiDashboard.Lay(s, m, frame, dark, keys);
            _scroll.Keep(m);
            if (_fresh)
            {
                int back = _focusArg >= 0 ? s.FindNode(UiDashboard.RowCommand, _focusArg) : -1;
                s.FocusFirst = back >= 0 ? back : s.FocusFirst;
                s.FocusReset = _focusArg >= 0 || _place != null;
                _fresh = false;
            }
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiDashboard.TabCommand)
            {
                _tab = arg;
                Opened(-1);
                return true;
            }
            string to = _view == null ? null : command == UiDashboard.RowCommand && arg >= 0 && arg < _view.Rows.Count ? _view.Rows[arg].Target
                : command == UiDashboard.ActionCommand ? _view.PrimaryTarget : null;
            if (string.IsNullOrEmpty(to) || to == _place)
            {
                return false;
            }
            _way.Push(new Step { Place = _place, Arg = command == UiDashboard.RowCommand ? arg : -1 });
            _place = to;
            Opened(-1);
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_way.Count > 0)
            {
                Step step = _way.Pop();
                _place = step.Place;
                Opened(step.Arg);
                return true;
            }
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
            if (_place == null)
            {
                _tab = (_tab + step + TabKeys.Length) % TabKeys.Length;
                Opened(-1);
            }
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Press(UiDashboard.ActionCommand, 0);

        public void Reveal(int list, int index)
        {
            _scroll.Reveal(list, index);
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _scroll.Wheel(clip, notches);
            _dirty = true;
        }

        private void Opened(int focusArg)
        {
            _scroll.Reset();
            _fresh = true;
            _focusArg = focusArg;
            _dirty = true;
        }
    }
}
