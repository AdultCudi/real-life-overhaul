using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Property screen reads and does in the game: a generation that moves when what it shows may have changed, and the page written for a view.</summary>
    public interface IEstateSource
    {
        /// <summary>Moves when the property business, the day or the player's choices changed what a page shows.</summary>
        int Generation { get; }

        /// <summary>Writes the page for the view into the model (reset first): the actions as runs that change the view or press the director.</summary>
        void Fill(EstateModel model, EstateView view);

        /// <summary>The screen opens: what was done in the game's other apps since the morning is read, and a notice's want taken into the view.</summary>
        void Opened(EstateView view);
    }

    /// <summary>
    /// The Property app on the kit: the rent roll, the tenants, the works, the market, the agency, the hotels and the car hire lot, each
    /// with the buttons the old app had, every one a run of the source. The page is written again when the source's generation moves or the
    /// player pressed something, and nothing a frame.
    /// </summary>
    public sealed class EstateScreen : IUiScreen, IUiTyping, IUiPointing
    {
        public const string ScreenId = "estate";

        private readonly string _id;
        private readonly IEstateSource _source;
        private readonly EstateView _view = new EstateView();
        private EstateModel _m = new EstateModel();
        private int _seen = -1;
        private bool _dirty = true, _opened;

        public EstateScreen(IEstateSource source) : this(source, ScreenId)
        {
        }

        /// <summary>The screen under another id: the car showroom runs on the same page shapes.</summary>
        public EstateScreen(IEstateSource source, string id)
        {
            _source = source;
            _id = id;
        }

        public string Id => _id;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        /// <summary>The view, for the checks and the tests.</summary>
        public EstateView View => _view;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (!_opened && _source != null)
            {
                _opened = true;
                _source.Opened(_view);
            }
            _m = new EstateModel();   // a tab never inherits a part (the table, a card, a button) from the one before it
            if (_source != null)
            {
                _seen = _source.Generation;
                _source.Fill(_m, _view);
            }
            if (_m.Table != null)
            {
                _m.Table.Offset = _view.Offset;
                _m.Table.Reveal = _view.Reveal;
                _m.Table.Hover = _view.Hover >= 0 && _view.Hover < _m.Table.RowCount ? _view.Hover : -1;   // the row's card shows while the pointer or the focus is on it
            }
            if (_m.Catalogue != null)
            {
                _m.Catalogue.Offset = _view.Offset;
                _m.Catalogue.Reveal = _view.Reveal;
            }
            _view.Reveal = -1;
            EstateFace.Lay(s, _m, frame, dark, keys);
            if (_m.Table != null)
            {
                _view.Offset = _m.Table.Offset;
            }
            if (_m.Catalogue != null)
            {
                _view.Offset = _m.Catalogue.Offset;
            }
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            bool done = Handle(command, arg);
            if (done)
            {
                _view.Hover = -1;   // the rows are written again, in whatever order: the one the card was for may be elsewhere
            }
            _dirty |= done;
            return done;
        }

        private bool Handle(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                // The info view's own buttons share the number 1 with the window's sections, and the rents view stands without the window:
                // a press there is the view's, never a change of section.
                if (_m.Shape == EstateShape.Rents)
                {
                    if (_m.Rents == null || arg < 0 || arg >= _m.Rents.Views.Length)
                    {
                        return false;
                    }
                    _view.RentUse = arg;   // the view's own icons: the kind of rent the districts are ranked by
                    return true;
                }
                if (arg < 0 || arg >= _m.Nav.Length)
                {
                    return false;
                }
                ShowTab(arg);
                return true;
            }
            if (command == EstateFace.TableBase + UiDataTable.Row)
            {
                if (_m.Shape == EstateShape.Off || _m.Table == null || arg < 0 || arg >= _m.RowKeys.Length || string.IsNullOrEmpty(_m.RowKeys[arg]))
                {
                    return false;
                }
                _view.Pick = _m.RowKeys[arg];
                _view.Offset = 0f;
                return true;
            }
            if (command == EstateFace.TableBase + UiDataTable.Head)
            {
                if (_m.Table == null || arg < 0 || arg >= _m.Table.Columns.Length || !_m.Table.Columns[arg].Sortable)
                {
                    return false;
                }
                _view.SortDown = _view.SortColumn == arg && !_view.SortDown;   // lowest first, then highest first, then lowest again
                _view.SortColumn = arg;
                _view.Offset = 0f;
                return true;
            }
            if (command == EstateFace.TableBase + UiDataTable.Filter)
            {
                if (_m.Table == null || arg < 0 || arg >= _m.Choices.Length || arg >= _view.Chosen.Length)
                {
                    return false;
                }
                _view.Chosen[arg] = TableRules.Next(_view.Chosen[arg], _m.Choices[arg].Options.Length);   // each kind in turn, then all again
                _view.Offset = 0f;
                return true;
            }
            if (command == EstateFace.TableBase + UiDataTable.Act)
            {
                // A row's own button (Locate): the run the row's source gave it.
                return _m.RowRuns != null && arg >= 0 && arg < _m.RowRuns.Length && _m.RowRuns[arg] >= 0 && Run(_m.RowRuns[arg]);
            }
            if (command == UiCatalogue.First + UiCatalogue.Card && _m.Shape == EstateShape.Catalogue)
            {
                if (_m.Catalogue == null || arg < 0 || arg >= _m.RowKeys.Length || string.IsNullOrEmpty(_m.RowKeys[arg]))
                {
                    return false;
                }
                _view.Pick = _m.RowKeys[arg];   // the chosen car: its detail shows beside the cards, and a second press does nothing more
                return true;
            }
            if (command == UiCatalogue.First + UiCatalogue.Action && _m.Shape == EstateShape.Catalogue)
            {
                return arg >= 0 && arg < _m.CatalogueRuns.Length && Run(_m.CatalogueRuns[arg]);
            }
            if (command == EstateFace.BackCommand)
            {
                return Back();
            }
            if (command == EstateFace.ActionCommand)
            {
                return Run(arg);
            }
            if (command == EstateFace.StepLess)
            {
                return Run(arg);
            }
            if (command == EstateFace.StepMore)
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
            _view.Tab = tab;
            _view.Pick = null;
            _view.From = null;
            _view.Offset = 0f;
            _view.Selected = -1;
            _view.ResetTable();
        }

        public void Focused(int command, int arg)
        {
            Aim(command, arg);
        }

        /// <summary>The mouse rests on a row of the table (or has left it): the row's card, where it has one, shows.</summary>
        public void Pointed(int command, int arg)
        {
            Aim(command, arg);
        }

        // Only a row that has a card needs the page written again: a pointer crossing the rows of a table with none costs nothing.
        private void Aim(int command, int arg)
        {
            int row = command == EstateFace.TableBase + UiDataTable.Row ? arg : -1;
            if (row == _view.Hover)
            {
                return;
            }
            bool had = Carded(_view.Hover), has = Carded(row);
            _view.Hover = row;
            _dirty |= had || has;
        }

        private bool Carded(int row) => _m.Table != null && _m.Table.Rows != null && row >= 0 && row < _m.Table.Rows.Length && _m.Table.Rows[row].Card != null;

        /// <summary>The stock's search field takes the keys when it is pressed (the host starts it).</summary>
        public int TypingCommand => EstateFace.SearchCommand;

        public string TypingText => _view.Query ?? string.Empty;

        public void Typed(string text)
        {
            _view.Query = text;
            _view.Offset = 0f;
            _dirty = true;
        }

        public bool Back()
        {
            if (_view.Pick == null || _m.Shape == EstateShape.Catalogue)
            {
                return false;
            }
            _view.Pick = _view.From;
            _view.From = null;
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
            ShowTab((_view.Tab + step + n) % n);
            _dirty = true;
        }

        // F runs the page's main button, P its second: the first action carrying that key.
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            if (_m.Shape == EstateShape.Decide)
            {
                int run = EstateFace.DecisionKey(_m.Decision, action);   // F takes the first choice that can be taken, P the next, as their caps say
                bool answered = run >= 0 && Run(run);
                _dirty |= answered;
                return answered;
            }
            bool done = Key(_m.DetailActions, action) || (_m.Shape == EstateShape.Empty && _m.Side != null && Key(_m.Side.Actions, action)) || (_m.Unit != null && Key(_m.Unit.NeedActions, action));
            for (int i = 0; !done && i < _m.Left.Length; i++)
            {
                done = Key(_m.Left[i].Actions, action);
            }
            for (int i = 0; !done && i < _m.Right.Length; i++)
            {
                done = Key(_m.Right[i].Actions, action);
            }
            done = done || (_m.Side != null && Key(_m.Side.Actions, action)) || (_m.Unit != null && _m.Unit.Empty != null && Key(_m.Unit.Empty.Actions, action));
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
            if (clip == _m.TableClip && (_m.Table != null || _m.Catalogue != null))
            {
                _view.Offset = Math.Max(0f, _view.Offset - notches * UiTokens.TableRow);
                _dirty = true;
            }
        }
    }
}
