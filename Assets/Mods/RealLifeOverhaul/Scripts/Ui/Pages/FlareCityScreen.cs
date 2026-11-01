using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare in the city as a screen of the kit: the player's account, the trends and the offers as tabs, a business's
    /// page where a row or the primary action leads, Back the way it came and closing at the tabs. Each view is worked
    /// out by Flare's director when it opens. Cost: a view and a layout a move; nothing per frame.
    /// </summary>
    public sealed class FlareCityScreen : IUiScreen
    {
        private static readonly string[] TabKeys = { "reallife_ui_flare_tab_account", "reallife_ui_flare_tab_trends", "reallife_ui_flare_tab_offers" };

        private readonly Stack<string> _way = new Stack<string>();
        private string _place;     // the business shown, or null for the tab's own view
        private int _tab;
        private FlareCityView _view;
        private bool _dirty = true;

        public Action Close;

        public string Id => "flare_city";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            FlareDirector d = FlareDirector.Instance;
            _view = d == null ? null : _place != null ? d.CityBusinessView(_place, null) : _tab == 1 ? d.CityTrendsView() : _tab == 2 ? d.CityOffersView() : d.CityAccountView();
            string[] tabs = _place != null ? null : new[] { UiText.T(TabKeys[0]), UiText.T(TabKeys[1]), UiText.T(TabKeys[2]) };
            UiDashboardModel m = _view != null
                ? FlareCityPages.Model(_view, UiText.Words, n => UiText.Money(n), d.ResidentName, tabs, _tab)
                : new UiDashboardModel { Title = UiText.T("reallife_ui_page_empty"), Tabs = tabs ?? new string[0], Tab = _tab };
            UiDashboard.Lay(s, m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiDashboard.TabCommand)
            {
                _tab = arg;
                _dirty = true;
                return true;
            }
            string to = _view == null ? null : command == UiDashboard.RowCommand && arg >= 0 && arg < _view.Rows.Count ? _view.Rows[arg].Target
                : command == UiDashboard.ActionCommand ? _view.PrimaryTarget : null;
            if (string.IsNullOrEmpty(to))
            {
                return false;
            }
            _way.Push(_place);
            _place = to;
            _dirty = true;
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_way.Count > 0)
            {
                _place = _way.Pop();
                _dirty = true;
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
                _dirty = true;
            }
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Press(UiDashboard.ActionCommand, 0);

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
