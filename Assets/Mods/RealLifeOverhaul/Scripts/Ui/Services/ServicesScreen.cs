using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Services screen reads and does in the game: a generation that moves when what it shows may have changed, and the page written for a view.</summary>
    public interface IServicesSource
    {
        /// <summary>Moves when the cleaning service or the sales, the day or the player's choices changed what a page shows.</summary>
        int Generation { get; }

        /// <summary>Writes the page for the view into the model (reset first): the actions as runs that change the view or press the director.</summary>
        void Fill(ServicesModel model, ServicesView view);

        /// <summary>The screen opens: a notice's or a link's want (a tab and a row) is taken into the view.</summary>
        void Opened(ServicesView view);
    }

    /// <summary>
    /// The Services app on the kit: Cleaning, Crew, Contracts and Sell, each with the buttons the old app had, every one a run of the source. The
    /// page is written again when the source's generation moves or the player pressed something, and nothing a frame.
    /// </summary>
    public sealed class ServicesScreen : IUiScreen
    {
        public const string ScreenId = "services";

        private readonly IServicesSource _source;
        private readonly ServicesView _view = new ServicesView();
        private ServicesModel _m = new ServicesModel();
        private int _seen = -1;
        private bool _dirty = true, _opened;

        public ServicesScreen(IServicesSource source)
        {
            _source = source;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        /// <summary>The view, for the checks and the tests.</summary>
        public ServicesView View => _view;

        /// <summary>The model as it was last written, for the checks.</summary>
        public ServicesModel Model => _m;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (!_opened && _source != null)
            {
                _opened = true;
                _source.Opened(_view);
            }
            _m = new ServicesModel();   // a tab never inherits a part (the table, a card, a button) from the one before it
            if (_source != null)
            {
                _seen = _source.Generation;
                _source.Fill(_m, _view);
            }
            UiDataTableModel table = Table(_m);
            if (table != null)
            {
                table.Offset = _view.Offset;
                table.Reveal = _view.Reveal;
            }
            _view.Reveal = -1;
            ServicesFace.Lay(s, _m, frame, dark, keys);
            if (table != null)
            {
                _view.Offset = table.Offset;
            }
            _dirty = false;
        }

        private static UiDataTableModel Table(ServicesModel m) => m.Shape == ServicesShape.Sell && m.Sell != null ? m.Sell.Offers : m.Table;

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
            if (command == ServicesFace.TableBase + UiDataTable.Row)
            {
                string[] keys = _m.Shape == ServicesShape.Sell && _m.Sell != null ? _m.Sell.OfferKeys : _m.RowKeys;
                if (_m.Shape == ServicesShape.Off || arg < 0 || arg >= keys.Length || string.IsNullOrEmpty(keys[arg]))
                {
                    return false;
                }
                _view.Pick = keys[arg];
                _view.Offset = 0f;
                return true;
            }
            if (command == ServicesFace.ActionCommand || command == ServicesFace.StepLess)
            {
                return Run(arg);
            }
            if (command == ServicesFace.StepMore)
            {
                return Run(arg + 1);
            }
            if (command == ServicesFace.BackCommand)
            {
                return Back();
            }
            if (command == ServicesFace.ChipCommand && _m.Shape == ServicesShape.Table)
            {
                if (arg < 0 || arg >= _m.Chips.Length)
                {
                    return false;
                }
                _view.Filter = arg;
                _view.Offset = 0f;
                _view.Pick = null;
                return true;
            }
            if (command == ServicesFace.ViewCommand && _m.Shape == ServicesShape.Sell && _m.Sell != null)
            {
                if (arg < 0 || arg >= _m.Sell.Views.Length)
                {
                    return false;
                }
                _view.SellView = arg;
                _view.Pick = null;
                _view.Offset = 0f;
                return true;
            }
            if (command == ServicesFace.PickerCommand && _m.Shape == ServicesShape.Sell && _m.Sell != null && _m.Sell.Businesses.Length > 1)
            {
                _view.PickerOpen = !_view.PickerOpen;
                return true;
            }
            if (command == ServicesFace.OptionCommand && _m.Shape == ServicesShape.Sell && _m.Sell != null)
            {
                if (arg < 0 || arg >= _m.Sell.BusinessKeys.Length)
                {
                    return false;
                }
                _view.Business = _m.Sell.BusinessKeys[arg];
                _view.PickerOpen = false;
                _view.Pick = null;
                _view.Step = -1;
                _view.Offset = 0f;
                return true;
            }
            if (command == UiChart.First + UiChart.Point && _m.Shape == ServicesShape.Sell && _m.Sell != null && _m.Sell.PriceBars != null)
            {
                if (arg < 0 || arg >= _m.Sell.PriceBars.Points.Length)
                {
                    return false;
                }
                _view.Step = arg;   // a bar of the price chart is the asking price it stands for
                return true;
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
            _view.Filter = 0;
            _view.SellView = 0;
            _view.PickerOpen = false;
            _view.Offset = 0f;
            _view.Selected = -1;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_view.PickerOpen)
            {
                _view.PickerOpen = false;
                _dirty = true;
                return true;
            }
            if (_view.Pick == null)
            {
                return false;
            }
            _view.Pick = null;
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
            bool done = Key(_m.Side?.Actions, action);
            if (_m.Sell != null)
            {
                done = done || Key(_m.Sell.Accept?.Actions, action) || Key(_m.Sell.PriceFoot?.Actions, action);
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
            if (clip == _m.TableClip && Table(_m) != null)
            {
                _view.Offset = Math.Max(0f, _view.Offset - notches * UiTokens.TableRow);
                _dirty = true;
            }
        }
    }
}
