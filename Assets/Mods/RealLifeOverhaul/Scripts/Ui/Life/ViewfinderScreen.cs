namespace RealLifeOverhaul.Ui
{
    /// <summary>What a press on the viewfinder's chrome does: the camera hands these in once, the screen calls them.</summary>
    public sealed class ViewfinderActions
    {
        public System.Action Close, Shoot, Gallery, Flip, Clean;
        public System.Action<int> Mode, Filter, Frame, Tool, Lens, Zoom, Exposure;
    }

    /// <summary>
    /// The viewfinder's chrome: slim bars, the strip of zoom, exposure and tools, the filter and frame chips, the shutter. The
    /// picture stays the camera's. The camera fills <see cref="Model"/> when something on it changes. Keys: the number keys
    /// choose the mode, Q and E the filter, F the shutter, P the Gallery, Esc closes. Cost: a layout on a change.
    /// </summary>
    public sealed class ViewfinderScreen : IUiScreen
    {
        public readonly UiFinderModel Model = new UiFinderModel();
        public ViewfinderActions Actions = new ViewfinderActions();
        private bool _dirty = true;

        public string Id => "viewfinder";

        public bool Dirty => _dirty;

        public void Touch()
        {
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _dirty = false;
            LifePages.Finder(s, Model, frame, dark, keys);
        }

        public bool Press(int command, int arg)
        {
            ViewfinderActions a = Actions;
            switch (command)
            {
                case LifePages.VFilter: a.Filter?.Invoke(arg); return true;
                case LifePages.VFrame: a.Frame?.Invoke(arg); return true;
                case LifePages.VMode: a.Mode?.Invoke(arg); return true;
                case LifePages.VShutter: a.Shoot?.Invoke(); return true;
                case LifePages.VClose: a.Close?.Invoke(); return true;
                case LifePages.VOpen: a.Gallery?.Invoke(); return true;
                case LifePages.VZoomOut: a.Zoom?.Invoke(-1); return true;
                case LifePages.VZoomIn: a.Zoom?.Invoke(1); return true;
                case LifePages.VLens: a.Lens?.Invoke(arg); return true;
                case LifePages.VExposureDown: a.Exposure?.Invoke(-1); return true;
                case LifePages.VExposureUp: a.Exposure?.Invoke(1); return true;
                case LifePages.VTool: a.Tool?.Invoke(arg); return true;
                case LifePages.VFlip: a.Flip?.Invoke(); return true;
                case LifePages.VClean: a.Clean?.Invoke(); return true;
                case LifePages.VFilterPage:
                    Model.FilterFirst = System.Math.Max(0, Model.FilterFirst + arg);
                    _dirty = true;
                    return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            Actions.Close?.Invoke();
            return true;
        }

        public void Tab(int step)
        {
            int n = Model.Filters != null ? Model.Filters.Length : 0;
            if (n == 0)
            {
                return;
            }
            int next = Model.Filter + (step < 0 ? -1 : 1);
            if (next < 0)
            {
                next = n - 1;
            }
            if (next >= n)
            {
                next = 0;
            }
            Actions.Filter?.Invoke(next);
        }

        public bool Act(UiAction action)
        {
            switch (action)
            {
                case UiAction.Alt:
                    Actions.Shoot?.Invoke();
                    return true;
                case UiAction.Option:
                    Actions.Gallery?.Invoke();
                    return true;
                case UiAction.Pick1:
                case UiAction.Pick2:
                case UiAction.Pick3:
                case UiAction.Pick4:
                    Actions.Mode?.Invoke(action - UiAction.Pick1);
                    return true;
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
