namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The viewfinder's chrome: slim bars, the filter and frame chips, the shutter. The picture stays the camera's.
    /// The camera fills <see cref="Model"/> when something on it changes. Cost: a layout on that change.
    /// </summary>
    public sealed class ViewfinderScreen : IUiScreen
    {
        public readonly UiFinderModel Model = new UiFinderModel();
        public System.Action<int> OnFilter, OnFrame, OnMode;
        public System.Action OnShutter, OnClose, OnGrid, OnOpenGallery;
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
            if (command == LifePages.VFilter)
            {
                OnFilter?.Invoke(arg);
                return true;
            }
            if (command == LifePages.VFrame)
            {
                OnFrame?.Invoke(arg);
                return true;
            }
            if (command == LifePages.VMode)
            {
                OnMode?.Invoke(arg);
                return true;
            }
            if (command == LifePages.VShutter)
            {
                OnShutter?.Invoke();
                return true;
            }
            if (command == LifePages.VClose)
            {
                OnClose?.Invoke();
                return true;
            }
            if (command == LifePages.VGrid)
            {
                OnGrid?.Invoke();
                return true;
            }
            if (command == LifePages.VOpen)
            {
                OnOpenGallery?.Invoke();
                return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            OnClose?.Invoke();
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
            OnFilter?.Invoke(next);
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                OnShutter?.Invoke();
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
