namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The news screens share one host. Nothing a frame: the page is laid again when its source changes.
    /// </summary>
    public class NewsHost
    {
        private readonly string _id;
        private readonly NewsModel _model = new NewsModel();
        private readonly INewsSource _source;
        private bool _dirty = true;
        private int _seen = -1;

        public NewsHost(string id, NewsApp app, UiIcon icon, INewsSource source)
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
            NewsPages.Lay(s, _model, frame, dark, keys);
            _dirty = false;
            _seen = _source != null ? _source.Generation : 0;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand || command == UiInfoView.ViewCommand)
            {
                _model.Tab = arg;
                _dirty = true;
                if (_source != null)
                {
                    _source.Press(_model, command, arg);
                }
                return true;
            }
            if (_source != null && _source.Press(_model, command, arg))
            {
                _dirty = true;
                return true;
            }
            if (command == UiAppWindow.ActionCommand || command == UiChart.First + UiChart.Point || command == UiDataTable.First + UiDataTable.Row
                || command == UiCatalogue.First + UiCatalogue.Card || command == UiCatalogue.First + UiCatalogue.Action
                || command == UiEdition.LeadCommand || command == UiEdition.StoryCommand
                || command == UiDecision.ChoiceCommand || command == UiDecision.ButtonCommand
                || command == UiInfoPanel.TabCommand || command == UiInfoPanel.LinkCommand || command == UiInfoPanel.FooterCommand
                || command == UiReader.AnchorCommand || command == NewsCommands.Link || command == NewsCommands.Rent
                || command == NewsCommands.Busy || command == NewsCommands.Home)
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

        public bool Back()
        {
            if (NewsLive.CloseList())
            {
                _dirty = true;
                return true;
            }
            if (_model.App == NewsApp.Economy && _model.Tab == 7)
            {
                _model.Tab = 0;
                _dirty = true;
                return true;
            }
            if (_model.Shape == NewsShape.Reading && _model.App == NewsApp.Stand)
            {
                _model.Tab = 0;
                _dirty = true;
                return true;
            }
            return false;
        }

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

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (_model.Table != null)
            {
                _model.Table.Reveal = index;
                _dirty = true;
            }
            if (_model.Catalogue != null)
            {
                _model.Catalogue.Reveal = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            if (_model.Table != null)
            {
                float next = _model.Table.Offset - notches * UiTokens.TableRow;
                _model.Table.Offset = next < 0f ? 0f : next;
                _dirty = true;
            }
            if (_model.Edition != null)
            {
                float next = _model.Edition.Offset - notches * UiTokens.TableRow;
                _model.Edition.Offset = next < 0f ? 0f : next;
                _dirty = true;
            }
            if (_model.Reader != null)
            {
                float next = _model.Reader.Offset - notches * UiTokens.TableRow;
                _model.Reader.Offset = next < 0f ? 0f : next;
                _dirty = true;
            }
        }
    }
}
