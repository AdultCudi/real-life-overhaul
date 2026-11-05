using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Construction screen reads and does in the game: a generation that moves when what it shows may have changed, and the page written for a view.</summary>
    public interface IConstructionSource
    {
        /// <summary>Moves when the builder's business, the day or the player's choices changed what a page shows.</summary>
        int Generation { get; }

        /// <summary>Writes the page into the model (reset first): the actions as runs that change the page or press the director.</summary>
        void Fill(ConstructionModel model, ConstructionView view);

        /// <summary>The screen opens: a notice's tab and pick are taken, and what the game's other apps did since the morning is read.</summary>
        void Opened();

        /// <summary>A section of the sidebar was chosen.</summary>
        void Show(int tab);

        /// <summary>A row of the table was pressed: the page of that thing opens.</summary>
        bool Pick(string key);

        /// <summary>Steps out of a thing's page to its tab; false where the page is the tab's own.</summary>
        bool Back();
    }

    /// <summary>
    /// The Construction app on the kit: the overview, land, projects, tenders, crews, the company, the rival builders and the city, each with the buttons the old app
    /// had, every one a run of the source. The page is written again when the source's generation moves or the player pressed something, and nothing a frame.
    /// </summary>
    public sealed class ConstructionScreen : IUiScreen
    {
        public const string ScreenId = "construction";

        private readonly IConstructionSource _source;
        private readonly ConstructionView _view = new ConstructionView();
        private ConstructionModel _m = new ConstructionModel();
        private int _seen = -1;
        private bool _dirty = true, _opened;

        public ConstructionScreen(IConstructionSource source)
        {
            _source = source;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        /// <summary>The view, for the checks and the tests.</summary>
        public ConstructionView View => _view;

        /// <summary>The page last laid, for the checks.</summary>
        public ConstructionModel Model => _m;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (!_opened && _source != null)
            {
                _opened = true;
                _source.Opened();
            }
            _m = new ConstructionModel();   // a tab never inherits a part (the table, a card, a button) from the one before it
            if (_source != null)
            {
                _seen = _source.Generation;
                _source.Fill(_m, _view);
            }
            if (_m.Table != null)
            {
                _m.Table.Offset = _view.Offset;
                _m.Table.Reveal = _view.Reveal;
            }
            _view.Reveal = -1;
            ConstructionFace.Lay(s, _m, frame, dark, keys);
            if (_m.Table != null)
            {
                _view.Offset = _m.Table.Offset;
            }
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            bool done = Handle(command, arg);
            _dirty |= done;
            return done;
        }

        private bool Handle(int command, int arg)
        {
            // The kit's command numbers repeat across parts (a nav entry is 1, as a decision's choice and a tab are): each is taken by the page it belongs to.
            if (command == UiAppWindow.NavCommand)
            {
                if (_m.Shape == ConstructionShape.Off || arg < 0 || arg >= _m.Nav.Length)
                {
                    return false;
                }
                ShowTab(arg);
                return true;
            }
            if (command == ConstructionFace.TableBase + UiDataTable.Row)
            {
                if (_m.Table == null || arg < 0 || arg >= _m.RowKeys.Length || string.IsNullOrEmpty(_m.RowKeys[arg]))
                {
                    return false;
                }
                _view.Offset = 0f;
                return _source != null && _source.Pick(_m.RowKeys[arg]);
            }
            if (command == ConstructionFace.TableBase + UiDataTable.Head)
            {
                if (_m.Table == null || arg < 0 || arg >= _m.Table.Columns.Length || !_m.Table.Columns[arg].Sortable)
                {
                    return false;
                }
                _view.SortDown = _view.Sort == arg && !_view.SortDown;
                _view.Sort = arg;
                _view.Offset = 0f;
                return true;
            }
            if (command == ConstructionFace.TableBase + UiDataTable.Filter)
            {
                if (_m.Table == null || _m.FilterKinds <= 0)
                {
                    return false;
                }
                _view.Filter = _view.Filter + 1 >= _m.FilterKinds ? -1 : _view.Filter + 1;   // all, then each kind of row, then all again
                _view.Offset = 0f;
                return true;
            }
            if (command == UiTrack.First + UiTrack.Step)
            {
                // a step of the stage track is read: its note and chip say what it needs, and nothing is acted on
                return arg >= 0 && _m.Track != null && arg < _m.Track.Steps.Length;
            }
            if (command == ConstructionFace.BackCommand)
            {
                return Back();
            }
            if (command == ConstructionFace.ActionCommand)
            {
                return Run(arg);
            }
            if (command == ConstructionFace.StepLess)
            {
                return Run(arg);
            }
            if (command == ConstructionFace.StepMore)
            {
                return Run(arg + 1);
            }
            return false;
        }

        private bool Run(int index)
        {
            if (_m.Runs == null || index < 0 || index >= _m.Runs.Length || _m.Runs[index] == null)
            {
                return false;
            }
            _m.Runs[index]();
            return true;   // a press that was refused told why (the director's notice); the page is written again either way
        }

        private void ShowTab(int tab)
        {
            _view.Offset = 0f;
            _view.Sort = -1;
            _view.Filter = -1;
            if (_source != null)
            {
                _source.Show(tab);
            }
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_source == null || !_source.Back())
            {
                return false;
            }
            _view.Offset = 0f;
            _dirty = true;
            return true;
        }

        public void Tab(int step)
        {
            int n = _m.Nav.Length;
            if (n == 0)
            {
                return;
            }
            ShowTab((_m.Tab + step + n) % n);
            _dirty = true;
        }

        // F runs the page's main button, P its second: the first action carrying that key, over the lead, the page's buttons and the cards'.
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            bool done = _m.Lead != null && Key(_m.Lead.Actions, action) || Key(_m.Actions, action) || Key(_m.TableActions,
                action) || (_m.Card != null && Key(_m.Card.Actions, action));
            for (int i = 0; !done && i < _m.Cards.Length; i++)
            {
                done = Key(_m.Cards[i].Actions, action);
            }
            for (int i = 0; !done && i < _m.Side.Length; i++)
            {
                done = Key(_m.Side[i].Actions, action);
            }
            _dirty |= done;
            return done;
        }

        private bool Key(UiActionModel[] actions, UiAction key)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                if (actions[i].Key == key && !actions[i].Disabled)
                {
                    return Run(actions[i].Arg);
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (list == _m.TableList)
            {
                _view.Reveal = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            if (clip == _m.TableClip && _m.Table != null)
            {
                _view.Offset = Math.Max(0f, _view.Offset - notches * UiTokens.TableRow);
                _dirty = true;
            }
        }
    }
}
