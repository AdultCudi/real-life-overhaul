using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Tech screen reads and does in the game: a generation that moves when what it shows may have changed, and the page written for a view.</summary>
    public interface ITechSource
    {
        /// <summary>Moves when the firms, the day or the player's choices changed what a page shows.</summary>
        int Generation { get; }

        /// <summary>Writes the page for the view into the model (made new for it): the actions as runs that change the view or press the director.</summary>
        void Fill(TechModel model, TechView view);

        /// <summary>The screen opens: what was done in the game's other apps since is read, and a notice's want is taken into the view.</summary>
        void Opened(TechView view);
    }

    /// <summary>
    /// The Tech app on the kit: the firms, the products, the contracts, the data centres, the startups and the funding, and one firm's own sections, each with the
    /// buttons the old app had, every one a run of the source. The page is written again when the source's generation moves or the player pressed something,
    /// and nothing a frame. The old app's pages stay underneath for the per-screen switch.
    /// </summary>
    public sealed class TechScreen : IUiScreen
    {
        public const string ScreenId = "tech";

        private readonly ITechSource _source;
        private readonly TechView _view = new TechView();
        private TechModel _m = new TechModel();
        private int _seen = -1;
        private bool _dirty = true, _opened;

        public TechScreen(ITechSource source)
        {
            _source = source;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        /// <summary>The view and the model as last laid, for the checks and the tests.</summary>
        public TechView View => _view;

        public TechModel Model => _m;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (!_opened && _source != null)
            {
                _opened = true;
                _source.Opened(_view);
            }
            _m = new TechModel();   // a page never inherits a part (the table, a card, a button) from the one before it
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
            TechFace.Lay(s, _m, frame, dark, keys);
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
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return arg >= 0 && arg < _m.NavKeys.Length && _view.Open(_m.NavKeys[arg]);
                case UiAppWindow.RowCommand:
                    return _m.Shape == TechShape.Overview && arg >= 0 && arg < _m.RowKeys.Length && _view.Open(_m.RowKeys[arg]);
                case UiAppWindow.MoreCommand:
                    return _m.Shape == TechShape.Overview && arg >= 0 && arg < _m.RowKeysMore.Length && _view.Open(_m.RowKeysMore[arg]);
                case UiAppWindow.PersonCommand:
                    return _m.Shape == TechShape.Overview && arg >= 0 && arg < _m.PersonKeys.Length && _view.Open(_m.PersonKeys[arg]);
                case UiAppWindow.AllCommand:
                    return _m.Shape == TechShape.Overview && _view.Open("sec:" + TechSection.Team);
                case UiAppWindow.NoteCommand:
                    return _m.Shape == TechShape.Overview && Run(_m.NoteRun);
                case UiAppWindow.ActionCommand:
                    UiActionModel[] list = _m.Shape == TechShape.Sheet ? _m.HeadActions : _m.Actions;
                    return arg >= 0 && arg < list.Length && !list[arg].Disabled && Run(list[arg].Arg);
                case TechFace.ActionCommand:
                    return Run(arg);
                case TechFace.StepLess:
                    return Run(arg);
                case TechFace.StepMore:
                    return Run(arg + 1);
                case TechFace.BackCommand:
                    return Back();
                case TechFace.QuoteCommand:
                    return _m.Shape == TechShape.Game && arg >= 0 && arg < _m.Quotes.Length;   // a review is read where it stands
                case UiTrack.First + UiTrack.Step:
                    return _m.Shape == TechShape.Game && _m.Track != null && arg >= 0 && arg < _m.Track.Steps.Length;   // a point of the making is read where it stands
                case UiChart.First + UiChart.Point:
                    return _m.Chart != null && arg >= 0;   // a day of the chart is read in its card
                case TechFace.TableBase + UiDataTable.Row:
                    if (_m.Table == null || arg < 0 || arg >= _m.RowKeys.Length || string.IsNullOrEmpty(_m.RowKeys[arg]))
                    {
                        return false;
                    }
                    return _view.Open(_m.RowKeys[arg]);
                default:
                    return false;
            }
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

        public void Focused(int command, int arg)
        {
        }

        /// <summary>Steps out of a sub page to where it was opened from, then out of a firm to the firms; false at the top, where the host closes the app.</summary>
        public bool Back()
        {
            if (_view.Page != 0)
            {
                _view.Page = 0;
                _view.Offset = 0f;
                _dirty = true;
                return true;
            }
            if (_view.Pick != null)
            {
                _view.Pick = _view.From;
                _view.From = null;
                _view.Page = 0;
                _view.Offset = 0f;
                _dirty = true;
                return true;
            }
            if (_view.Firm != null)
            {
                _view.Firm = null;
                _view.Tab = 0;
                _view.Section = TechSection.Overview;
                _view.Offset = 0f;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            int n = _m.NavKeys.Length;
            if (n == 0)
            {
                return;
            }
            // The firm's own list leads with its way out to the firms: Tab moves among the sections after it.
            int first = _view.Firm != null ? 1 : 0;
            int at = Math.Max(first, _m.NavSelected);
            int span = n - first;
            _view.Open(_m.NavKeys[first + ((at - first + step) % span + span) % span]);
            _dirty = true;
        }

        // F runs the page's main button, P its second: the first action carrying that key.
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            bool done = Key(_m.Actions, action) || Key(_m.HeadActions, action);
            for (int i = 0; !done && i < _m.Left.Length; i++)
            {
                done = Key(_m.Left[i].Actions, action);
            }
            for (int i = 0; !done && i < _m.Right.Length; i++)
            {
                done = Key(_m.Right[i].Actions, action);
            }
            done = done || (_m.Side != null && Key(_m.Side.Actions, action));
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
