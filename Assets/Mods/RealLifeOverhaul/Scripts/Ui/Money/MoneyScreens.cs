namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Finance on the kit: your position, then the lines behind it. The old page stays underneath for the switch.
    /// Nothing a frame; the page is laid again when its numbers or its tab change.
    /// </summary>
    public sealed class FinanceScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "finance";

        public FinanceScreen(IMoneySource source) : base(ScreenId, MoneyApp.Finance, UiIcon.Wallet, source) { }
    }

    /// <summary>One bank's counter on the kit. Nothing a frame.</summary>
    public sealed class BanksScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "banks";

        public BanksScreen(IMoneySource source) : base(ScreenId, MoneyApp.Banks, UiIcon.Bank, source) { }
    }

    /// <summary>The bank the player owns, on the kit. Nothing a frame.</summary>
    public sealed class OwnBankScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "own_bank";

        public OwnBankScreen(IMoneySource source) : base(ScreenId, MoneyApp.OwnBank, UiIcon.PiggyBank, source) { }
    }

    /// <summary>Bills and the broadband company, on the kit. Nothing a frame.</summary>
    public sealed class UtilitiesScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "utilities";

        public UtilitiesScreen(IMoneySource source) : base(ScreenId, MoneyApp.Utilities, UiIcon.Globe, source) { }
    }

    /// <summary>The four money screens share one host: a model, a fill when the series changes, and the page drawer.</summary>
    public class MoneyHost
    {
        private readonly string _id;
        private readonly MoneyModel _model = new MoneyModel();
        private readonly IMoneySource _source;
        private bool _dirty = true;
        private int _seen = -1;

        public MoneyHost(string id, MoneyApp app, UiIcon icon, IMoneySource source)
        {
            _id = id;
            _model.App = app;
            _model.Icon = icon;
            _source = source;
        }

        public string Id => _id;

        public bool Dirty => _dirty || (_source != null && _source.Generation != _seen);

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_source != null)
            {
                _source.Fill(_model);
            }
            MoneyPages.Lay(s, _model, frame, dark, keys);
            _dirty = false;
            _seen = _source != null ? _source.Generation : 0;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                _model.Tab = arg;
                _dirty = true;
                return true;
            }
            if (command == UiDataTable.First + UiDataTable.Row)
            {
                if (_model.Table != null)
                {
                    _model.Table.Selected = arg;
                }
                _dirty = true;
                return _source == null || _source.Press(_model, command, arg);
            }
            if (_source != null && _source.Press(_model, command, arg))
            {
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
            if (command == UiDataTable.First + UiDataTable.Row && _model.Table != null && _model.Table.Selected != arg)
            {
                _model.Table.Selected = arg;
                _dirty = true;
            }
        }

        public bool Back() => false;

        public void Tab(int step)
        {
            int n = _model.Nav != null ? _model.Nav.Length : 0;
            if (n == 0)
            {
                return;
            }
            int next = _model.Tab + step;
            if (next < 0)
            {
                next = n - 1;
            }
            else if (next >= n)
            {
                next = 0;
            }
            _model.Tab = next;
            _dirty = true;
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt && _source != null)
            {
                _dirty = true;
                return _source.Press(_model, UiAppWindow.ActionCommand, 0);
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (_model.Statement != null)
            {
                _model.Statement.Reveal = index;
                _dirty = true;
            }
            if (_model.Table != null)
            {
                _model.Table.Reveal = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            if (_model.Statement != null)
            {
                float next = _model.Statement.Offset - notches * UiTokens.TableRow;
                _model.Statement.Offset = next < 0f ? 0f : next;
                _dirty = true;
            }
            if (_model.Table != null)
            {
                float next = _model.Table.Offset - notches * UiTokens.TableRow;
                _model.Table.Offset = next < 0f ? 0f : next;
                _dirty = true;
            }
        }
    }
}
