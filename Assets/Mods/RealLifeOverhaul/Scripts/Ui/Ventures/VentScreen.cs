using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a Brands, Ventures or Web shop screen has open: its tab, the thing picked on it, the sub page and the small choices a page makes.</summary>
    public sealed class VentView
    {
        public int Tab;
        public string Pick;        // the row opened on the tab (a key the source gave the row), or null for the tab's own page
        public string From;        // the page before a pick, for Back
        public int PickBase;       // the table the pick came from
        public int Page;           // a sub page of the tab (the steps of raising money), 0 for the tab's own
        public int A, B, C;        // small choices a page makes (a budget step, an investor, a filter)
        public string S1, S2;      // words a page keeps (the business the money is for, its kind)
        public float Offset;
        public int Reveal = -1;
        public string Query = string.Empty;
    }

    /// <summary>What a screen reads and does in the game: a generation that moves when what it shows may have changed, and the page written for a view.</summary>
    public interface IVentSource
    {
        /// <summary>Moves when the app's business, the day or the player's choices changed what a page shows.</summary>
        int Generation { get; }

        /// <summary>Writes the page for the view into the model (reset first): the actions as runs that change the view or press the director.</summary>
        void Fill(VentModel model, VentView view);

        /// <summary>The screen opens: what was done elsewhere since is read, and a notice's want (a tab, an item) is taken into the view.</summary>
        void Opened(VentView view);

        /// <summary>Back pressed on a page the source keeps its own steps for (raising money); false lets the screen step out by its own rule.</summary>
        bool Back(VentView view);
    }

    /// <summary>
    /// The runner of the Brands, Ventures and Web shop apps on the kit: it asks its source for the page of the view and answers what is pressed on it. A press goes to the run
    /// the page drew the target for; a target of one kind never reaches another kind's handler, because each kind has a number of its own (VentFace). The page is written
    /// again when the source's generation moves or the player pressed something, and nothing a frame.
    /// </summary>
    public sealed class VentScreen : IUiScreen, IUiTyping, IUiStepping
    {
        private readonly string _id;
        private readonly IVentSource _source;
        private readonly VentView _view = new VentView();
        private VentModel _m = new VentModel();
        private int _seen = -1, _typeArg = -1;
        private bool _dirty = true, _opened;

        public VentScreen(IVentSource source, string id)
        {
            _source = source;
            _id = id;
        }

        public string Id => _id;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        /// <summary>The view, for the checks and the tests.</summary>
        public VentView View => _view;

        /// <summary>The page last written, for the checks.</summary>
        public VentModel Model => _m;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (!_opened && _source != null)
            {
                _opened = true;
                _source.Opened(_view);
            }
            _m = new VentModel();   // a tab never inherits a part (a table, a switch) from the one before it
            if (_source != null)
            {
                _seen = _source.Generation;
                _source.Fill(_m, _view);
            }
            VentBlock first = Scrolling(_m);
            if (first != null)
            {
                first.Table.Offset = _view.Offset;
                first.Table.Reveal = _view.Reveal;
            }
            _view.Reveal = -1;
            VentFace.Lay(s, _m, frame, dark, keys);
            if (first != null)
            {
                _view.Offset = first.Table.Offset;
            }
            _dirty = false;
        }

        // The first table of a page is the one the wheel scrolls.
        private static VentBlock Scrolling(VentModel m)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                VentBlock[] side = pass == 0 ? m.Left : m.Right;
                for (int i = 0; i < side.Length; i++)
                {
                    if (side[i].Kind == VentBlockKind.Table && side[i].Table != null)
                    {
                        return side[i];
                    }
                }
            }
            return null;
        }

        public bool Press(int command, int arg)
        {
            bool done = Handle(command, arg);
            _dirty |= done;
            return done;
        }

        private bool Handle(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                if (arg < 0 || arg >= _m.Nav.Length)
                {
                    return false;
                }
                ShowTab(arg);
                return true;
            }
            if (command == UiAppWindow.ActionCommand)
            {
                if (arg < 0 || arg >= _m.HeadActions.Length || _m.HeadActions[arg].Disabled)
                {
                    return false;
                }
                int run = _m.HeadActions[arg].Arg;
                return run == VentFace.BackArg ? Back() : Run(run);
            }
            if (command == VentFace.ActionCommand || command == VentFace.ItemCommand || command == VentFace.LinkCommand || command == VentFace.StepLess)
            {
                return Run(arg);
            }
            if (command == VentFace.StepMore)
            {
                return Run(arg + 1);
            }
            if (command == VentFace.StepType)
            {
                _typeArg = arg;   // the host opens a text field for it
                return true;
            }
            if (command == VentFace.BackCommand)
            {
                return Back();
            }
            VentBlock table = _m.TableOf(command);
            if (table == null)
            {
                return false;
            }
            int at = command - table.Base;
            if (at == UiDataTable.Row)
            {
                if (arg < 0 || arg >= table.RowKeys.Length || string.IsNullOrEmpty(table.RowKeys[arg]))
                {
                    return false;
                }
                _view.From = _view.Pick;
                _view.Pick = table.RowKeys[arg];
                _view.PickBase = table.Base;
                _view.Offset = 0f;
                return true;
            }
            if (at == UiDataTable.Act || at == UiDataTable.Less || at == UiDataTable.More)
            {
                if (arg < 0 || arg >= table.RowRuns.Length || table.RowRuns[arg] < 0)
                {
                    return false;
                }
                return Run(table.RowRuns[arg] + (at == UiDataTable.More ? 1 : 0));
            }
            if (at == UiDataTable.Type)
            {
                _typeArg = arg < table.RowRuns.Length ? table.RowRuns[arg] : -1;
                return _typeArg >= 0;
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
            _view.Page = 0;
            _view.Offset = 0f;
            _view.Query = string.Empty;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_source != null && _source.Back(_view))
            {
                _dirty = true;
                return true;
            }
            if (_view.Pick != null)
            {
                _view.Pick = _view.From;
                _view.From = null;
                _view.Offset = 0f;
                _dirty = true;
                return true;
            }
            if (_view.Page != 0)
            {
                _view.Page = 0;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            int n = _m.Nav.Length;
            if (n == 0)
            {
                return;
            }
            ShowTab(((_view.Tab + step) % n + n) % n);
            _dirty = true;
        }

        // F runs the page's main button, P its second: the first action carrying that key, in the head and then in the columns.
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            bool done = false;
            for (int i = 0; !done && i < _m.HeadActions.Length; i++)
            {
                UiActionModel a = _m.HeadActions[i];
                done = a.Key == action && !a.Disabled && (a.Arg == VentFace.BackArg ? Back() : Run(a.Arg));
            }
            for (int pass = 0; !done && pass < 2; pass++)
            {
                VentBlock[] side = pass == 0 ? _m.Left : _m.Right;
                for (int b = 0; !done && b < side.Length; b++)
                {
                    for (int i = 0; !done && side[b].Kind == VentBlockKind.Actions && i < side[b].Actions.Length; i++)
                    {
                        UiActionModel a = side[b].Actions[i];
                        done = a.Key == action && !a.Disabled && Run(a.Arg);
                    }
                }
            }
            _dirty |= done;
            return done;
        }

        public void Reveal(int list, int index)
        {
            VentBlock t = Scrolling(_m);
            if (t != null && t.List == list)
            {
                _view.Reveal = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            VentBlock t = Scrolling(_m);
            if (t != null && t.Clip == clip)
            {
                _view.Offset = Math.Max(0f, _view.Offset - notches * UiTokens.TableRow);
                _dirty = true;
            }
        }

        // A stepper takes Left and Right as its minus and plus.
        public bool Steps(int command) => command == VentFace.StepType;

        public void Step(int command, int arg, int step)
        {
            if (command == VentFace.StepType && Run(step < 0 ? arg : arg + 1))
            {
                _dirty = true;
            }
        }

        /// <summary>A stepper's field takes the keys when it is pressed (the host starts it).</summary>
        public int TypingCommand => VentFace.StepType;

        public string TypingText => _m.TypedText != null && _typeArg >= 0 ? _m.TypedText(_typeArg) ?? string.Empty : string.Empty;

        public void Typed(string text)
        {
            if (_m.Typed != null && _typeArg >= 0 && _m.Typed(_typeArg, text))
            {
                _dirty = true;
            }
        }
    }
}
