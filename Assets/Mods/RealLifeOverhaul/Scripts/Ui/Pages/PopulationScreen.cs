using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the people screen reads from the game: the views of the live population, the game's words, its money, its name maker and its places.</summary>
    public sealed class PopulationSource
    {
        public Func<ViewTarget, PopulationView> Open;   // the view a target leads to, or null while it cannot be made
        public UiWords Words;
        public Func<long, string> Money;
        public Func<int, bool, string> NameOf;
        public Func<string, string> Place;
    }

    /// <summary>
    /// The population's views as a screen of the kit: the city first, then wherever a row or the primary action leads
    /// (a district, a building, a household, a person, a business, who contributes), Back stepping back the way it came
    /// and closing at the city. Back puts the focus on the row it left from, so Enter and Back always meet at one place.
    /// Each view is worked out from the live population when it opens (PopulationViews). Cost: a view and a layout a move,
    /// nothing per frame. Game-free: the game's reads come in through <see cref="PopulationSource"/>.
    /// </summary>
    public sealed class PopulationScreen : IUiScreen
    {
        private readonly PopulationSource _source;
        private readonly Stack<Step> _way = new Stack<Step>();
        private ViewTarget _at = ViewTarget.To(ViewKind.City, 0);
        private PopulationView _view;
        private PopulationModel _shown;      // the page on the screen
        private string _name;                // what the row that opened this page called it, or null
        private bool _dirty = true;
        private bool _fresh = true;          // a page just opened: focus goes where it should start, not to the last page's target
        private int _focusCommand = -1, _focusArg;
        private float _offset;               // px the rows are scrolled
        private int _reveal = -1;

        private struct Step
        {
            public ViewTarget At;
            public int Command, Arg;         // what was pressed to leave it: Back focuses it again
            public string Name;
        }

        public PopulationScreen(PopulationSource source)
        {
            _source = source;
        }

        /// <summary>A person the next opening of the page is to show (the work apps' "Open their page"); taken once.</summary>
        internal static void Want(ViewTarget at) => Wanted = at;

        /// <summary>Back at the city closes the screen.</summary>
        public Action Close;

        /// <summary>A page another screen asked for (a landlord's tenant, a person of the pool): taken at the next layout, once.</summary>
        public static ViewTarget? Wanted;

        public string Id => "population";

        public bool Dirty => _dirty;

        /// <summary>The page now on the screen, for the checks.</summary>
        internal ViewTarget At => _at;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (Wanted.HasValue)
            {
                _way.Clear();
                _at = Wanted.Value;
                _name = null;
                _fresh = true;
                Wanted = null;
                _focusCommand = -1;
                _offset = 0f;
            }
            _view = _source.Open(_at) ?? _source.Open(ViewTarget.To(ViewKind.City, 0));
            UiWords t = _source.Words;
            PopulationModel m = _view != null ? PopulationPages.Model(_view, t, _source.Money, _source.NameOf, _source.Place, _name) : Nothing(t);
            _shown = m;
            m.List.Offset = _offset;
            m.List.Reveal = _reveal;
            _reveal = -1;
            PopulationFace.Lay(s, m, frame, dark, keys);
            _offset = m.List.Offset;
            if (_fresh)
            {
                int back = _focusCommand >= 0 ? s.FindNode(_focusCommand, _focusArg) : -1;
                s.FocusFirst = back >= 0 ? back : FirstRow(s);
                s.FocusReset = true;
                _fresh = false;
            }
            _dirty = false;
        }

        // The city is not there (the population is not running): the words alone.
        private static PopulationModel Nothing(UiWords t)
        {
            var m = new PopulationModel { Gone = true, GoneTitle = t("reallife_ui_page_empty", "Nothing here yet") };
            m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") } };
            m.PromptCount = 1;
            return m;
        }

        private static int FirstRow(UiScene s)
        {
            int row = s.FindNode(PopulationFace.RowCommand, 0);
            return row >= 0 ? row : s.FocusFirst;
        }

        public bool Press(int command, int arg)
        {
            if (_view == null || _shown == null)
            {
                return false;
            }
            ViewTarget to = command == PopulationFace.RowCommand && arg >= 0 && arg < _view.Rows.Count ? _view.Rows[arg].Target
                : command == UiAppWindow.ActionCommand ? _view.PrimaryTarget : ViewTarget.None;
            if (!to.Valid)
            {
                return false;
            }
            _way.Push(new Step { At = _at, Command = command, Arg = arg, Name = _name });
            _at = to;
            _name = command == PopulationFace.RowCommand ? RowName(arg) : null;
            Opened(-1, 0);
            return true;
        }

        // What the page on the screen calls a row: a person's or a household's name, or a place's.
        private string RowName(int row) =>
            row >= 0 && row < _shown.List.RowCount && _shown.List.Rows[row].Cells != null && _shown.List.Rows[row].Cells.Length > 0 ? _shown.List.Rows[row].Cells[0] : null;

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
                _reveal = step.Command == PopulationFace.RowCommand ? step.Arg : -1;   // the row it left from scrolls into view first
                return true;
            }
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Press(UiAppWindow.ActionCommand, 0);

        public void Reveal(int list, int index)
        {
            _reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _offset = Math.Max(0f, _offset - notches * UiTokens.S16);
            _dirty = true;
        }

        private void Opened(int focusCommand, int focusArg)
        {
            _offset = 0f;
            _reveal = -1;
            _fresh = true;
            _focusCommand = focusCommand;
            _focusArg = focusArg;
            _dirty = true;
        }
    }
}
