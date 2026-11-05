using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The population's views as a screen of the kit: the city first, then wherever a row or the primary action leads
    /// (a district, a building, a household, a person, a business, who contributes), Back stepping back the way it came
    /// and closing at the city. Back puts the focus on the row it left from, so Enter and Back always meet at one place.
    /// Each view is worked out from the live population when it opens (PopulationViews). Cost: a view and a layout a move,
    /// nothing per frame.
    /// </summary>
    public sealed class PopulationScreen : IUiScreen
    {
        private readonly Stack<Step> _way = new Stack<Step>();
        private readonly UiDashboardScroll _scroll = new UiDashboardScroll();
        private ViewTarget _at = ViewTarget.To(ViewKind.City, 0);
        private PopulationView _view;
        private UiDashboardModel _shown;     // the page on the screen, whose rows name what they open
        private string _name;                // what the row that opened this page called it, or null
        private bool _dirty = true;
        private bool _fresh = true;          // a page just opened: focus goes where it should start, not to the last page's target
        private int _focusCommand = -1, _focusArg;

        private struct Step
        {
            public ViewTarget At;
            public int Command, Arg;         // what was pressed to leave it: Back focuses it again
            public string Name;
        }

        private static ViewTarget _wanted = ViewTarget.None;

        /// <summary>A person the next opening of the page is to show (the work apps' "Open their page"); taken once.</summary>
        internal static void Want(ViewTarget at) => _wanted = at;

        /// <summary>Back at the city closes the screen.</summary>
        public Action Close;

        public string Id => "population";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_wanted.Valid)
            {
                _way.Clear();
                _at = _wanted;
                _wanted = ViewTarget.None;
                _fresh = true;
                _focusCommand = -1;
            }
            _view = Build(_at) ?? Build(ViewTarget.To(ViewKind.City, 0));
            UiDashboardModel m = _view != null ? PopulationPages.Model(_view, UiText.Words, PopulationText.Money, PopulationGame.NameOf, UiText.Place, _name)
                : new UiDashboardModel { Table = new UiTableModel { EmptyTitle = UiText.T("reallife_ui_page_empty") } };
            _shown = m;
            if (_view == null)
            {
                m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") } };
                m.PromptCount = 1;
            }
            _scroll.Apply(m);
            UiDashboard.Lay(s, m, frame, dark, keys);
            _scroll.Keep(m);
            if (_fresh)
            {
                int back = _focusCommand >= 0 ? s.FindNode(_focusCommand, _focusArg) : -1;
                s.FocusFirst = back >= 0 ? back : s.FocusFirst;
                s.FocusReset = true;
                _fresh = false;
            }
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
            if (!to.Valid)
            {
                return false;
            }
            _way.Push(new Step { At = _at, Command = command, Arg = arg, Name = _name });
            _at = to;
            _name = command == UiDashboard.RowCommand ? RowName(arg) : null;
            Opened(-1, 0);
            return true;
        }

        // What the page on the screen calls a row: a person's or a household's entry, or a table row's first cell.
        private string RowName(int row)
        {
            if (_shown == null || row < 0)
            {
                return null;
            }
            if (row < _shown.EntryCount)
            {
                return _shown.Entries[row].Name;
            }
            string[] cells = row < _shown.Table.Rows.Length ? _shown.Table.Rows[row] : null;
            return cells != null && cells.Length > 0 ? cells[0] : null;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_way.Count > 0)
            {
                Step step = _way.Pop();
                _at = step.At;
                _name = step.Name;
                Opened(step.Command, step.Arg);
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
            _scroll.Reveal(list, index);
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _scroll.Wheel(clip, notches);
            _dirty = true;
        }

        private void Opened(int focusCommand, int focusArg)
        {
            _scroll.Reset();
            _fresh = true;
            _focusCommand = focusCommand;
            _focusArg = focusArg;
            _dirty = true;
        }

        // The demand the population's director shows (null while it is settling or not running).
        private static PopDemandState Demand()
        {
            PopDemandState m = PopulationDirector.Instance != null ? PopulationDirector.Instance.DemandShown : null;
            return m != null && m.AnchorDay >= 0 ? m : null;
        }

        // A household or a person opens through PopulationViews.Open, which checks the slot still holds the one the row
        // was made for, so a slot used again after a death or a move opens as gone, never as somebody else.
        private static PopulationView Build(ViewTarget to)
        {
            PopulationState s = CityPopulation.Live;
            if (s == null)
            {
                return null;
            }
            switch (to.Kind)
            {
                case ViewKind.Demands: return Demand() != null ? PopDemandViews.Demands(Demand()) : null;
                case ViewKind.Demand: return Demand() != null ? PopDemandViews.Demand(Demand(), to.Index) : null;
                default: return PopulationViews.Open(s, CityPopulation.Pools, to);
            }
        }
    }
}
