using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The population's views as a screen of the kit: the city first, then wherever a row or the primary action leads
    /// (a district, a building, a household, a person, a business, who contributes), Back stepping back the way it came
    /// and closing at the city. Each view is worked out from the live population when it opens (PopulationViews).
    /// Cost: a view and a layout a move; nothing per frame.
    /// </summary>
    public sealed class PopulationScreen : IUiScreen
    {
        private readonly Stack<ViewTarget> _way = new Stack<ViewTarget>();
        private ViewTarget _at = ViewTarget.To(ViewKind.City, 0);
        private PopulationView _view;
        private bool _dirty = true;

        /// <summary>Back at the city closes the screen.</summary>
        public Action Close;

        public string Id => "population";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _view = Build(_at) ?? Build(ViewTarget.To(ViewKind.City, 0));
            UiDashboardModel m = _view != null ? PopulationPages.Model(_view, UiText.Words, PopulationText.Money,
                PopulationGame.NameOf) : new UiDashboardModel { Title = UiText.T("reallife_ui_page_empty") };
            UiDashboard.Lay(s, m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (_view == null)
            {
                return false;
            }
            ViewTarget to = command == UiDashboard.RowCommand && arg >= 0 && arg < _view.Rows.Count ? _view.Rows[arg].Target
                : command == UiDashboard.ActionCommand ? _view.PrimaryTarget : ViewTarget.None;
            if (!to.Valid || to.Kind == ViewKind.Demands || to.Kind == ViewKind.Demand)
            {
                return false;
            }
            _way.Push(_at);
            _at = to;
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
                _at = _way.Pop();
                _dirty = true;
                return true;
            }
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Press(UiDashboard.ActionCommand, 0);

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private static PopulationView Build(ViewTarget to)
        {
            PopulationState s = CityPopulation.Live;
            if (s == null)
            {
                return null;
            }
            switch (to.Kind)
            {
                case ViewKind.District: return PopulationViews.District(s, to.Index);
                case ViewKind.Building: return PopulationViews.Building(s, to.Index);
                case ViewKind.Household: return CityPopulation.Pools != null ? PopulationViews.Household(s, CityPopulation.Pools, to.Index) : null;
                case ViewKind.Person: return PopulationViews.Person(s, to.Index);
                case ViewKind.Business: return CityPopulation.Pools != null ? PopulationViews.Business(s, CityPopulation.Pools, to.Index) : null;
                case ViewKind.Contributors: return CityPopulation.Pools != null ? PopulationViews.Contributors(s, CityPopulation.Pools) : null;
                default: return PopulationViews.City(s);
            }
        }
    }
}
