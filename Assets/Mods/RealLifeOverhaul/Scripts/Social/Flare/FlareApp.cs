using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The screens of the Flare app.</summary>
    internal enum FlareScreen
    {
        Home, Explore, Tag, Notices, Messages, Conversation, Profile, Thread, Business
    }

    /// <summary>
    /// The Flare app: a social network's own look inside the game's phone frame (GameUi.AppShell), an exception to the
    /// native rule: rail, a column of cards, the account's side and a sheet over all. Cost: nothing per frame while it
    /// stands still; rows are made on a click or the director's word, the glide only while it moves.
    /// </summary>
    public sealed partial class FlareApp
    {
        public static IModLogger Logger;

        private readonly FlareDirector _director;
        private readonly FlarePictures _pictures;
        private readonly FlareAvatars _avatars;
        private readonly List<FlareRow> _rows = new List<FlareRow>(128);
        private readonly List<FlareRow> _rowPool = new List<FlareRow>(128);
        private readonly List<State> _back = new List<State>(8);

        private AppShell _shell;
        private RectTransform _root;
        private FlareLook _look = FlareLook.Light;
        private bool _built, _writing, _top;
        private int _composeWanted = -1;

        private struct State
        {
            public FlareScreen Screen;
            public int Tab;
            public int Post;
            public string Key;
        }

        private State _at = new State { Post = -1 };
        private State? _wanted;

        public FlareApp(FlareDirector director)
        {
            _director = director;
            _pictures = new FlarePictures(director, PictureCame);
            _avatars = new FlareAvatars(director, PictureCame);
        }

        /// <summary>The city is unloading or Flare stops: every picture's texture goes.</summary>
        public void Dispose()
        {
            _pictures.Clear();
            _avatars.Clear();
            FlareFacePainter.AppShowing = false;
        }

        /// <summary>The app's panel, asleep: the game's frame with Flare's own screens in it. Null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("flare: the app being built in the game's frame");   // once a city: the frame copied, Flare's own parts made
            _shell = AppShell.Make(menu, dormant, FlareDirector.AppId);
            if (_shell == null)
            {
                return null;
            }
            Transform layout = _shell.Root.transform.Find("Layout 30-70");
            if (layout == null)
            {
                UiClone.Fallback("Flare: the game's frame is not laid out as expected");
                return null;
            }
            for (int i = 0; i < layout.childCount; i++)
            {
                layout.GetChild(i).gameObject.SetActive(false);   // the game's box and table: Flare draws its own screens in their place
            }
            _root = FlareKit.Rect(layout, "Flare");
            _root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;   // the frame's own layout, if it has one, leaves Flare's screens alone
            FlareKit.Stretch(_root, 0f, 0f, 0f, 0f);
            BuildFrame();
            _built = true;
            return _shell.Root;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        public void Opened()
        {
            if (!_built)
            {
                return;
            }
            if (_wanted.HasValue)
            {
                Go(_wanted.Value, false);
                _wanted = null;
            }
            _director.AppOpened();
            FlareFacePainter.AppShowing = true;   // faces in view are drawn while the app is open
            Refresh(true);
        }

        public void Closed()
        {
            FlareFacePainter.AppShowing = false;
            _pictures.Closed();   // kept for the next opening
            _avatars.Closed();
            CloseSheet();
        }

        public void Want(int tab, string pick)
        {
            var s = new State { Post = -1 };
            switch (tab)
            {
                case FlareDirector.TabPost: _composeWanted = -2; s.Screen = FlareScreen.Home; break;
                case FlareDirector.TabMe: s.Screen = FlareScreen.Profile; s.Key = "me"; break;
                case FlareDirector.TabBusiness: s.Screen = FlareScreen.Business; break;
                case FlareDirector.TabCity: s.Screen = FlareScreen.Explore; s.Tab = 1; break;
                case FlareDirector.TabNotices: s.Screen = FlareScreen.Notices; break;
                case FlareDirector.TabMessages: s.Screen = FlareScreen.Messages; break;
                case FlareDirector.TabExplore: s.Screen = FlareScreen.Explore; break;
                default: s.Screen = FlareScreen.Home; break;
            }
            Wanted(s);
        }

        public void WantPost(int id)
        {
            Wanted(new State { Screen = FlareScreen.Thread, Post = id });
        }

        /// <summary>The compose sheet with a draft in it (a moment: a picture), now or when the app opens.</summary>
        public void WantCompose(int draft)
        {
            _composeWanted = draft;
            if (Showing)
            {
                OpenCompose(draft, -1);
                _composeWanted = -1;
            }
        }

        private void Wanted(State s)
        {
            if (Showing)
            {
                Go(s, true);
                Refresh(true);
            }
            else
            {
                _wanted = s;
            }
        }

        /// <summary>The director says something changed: the screen is written again if showing.</summary>
        public void Changed()
        {
            if (Showing && !_writing)
            {
                Refresh(false);
            }
        }

        /// <summary>A comment's words came for this post: its thread is written again if it is the screen showing.</summary>
        public void ThreadChanged(int postId)
        {
            if (_at.Screen == FlareScreen.Thread && _at.Post == postId)
            {
                Changed();
            }
        }

        // A picture came in: the cards in view and the sheet shown again, as a card may grow with it.
        private void PictureCame()
        {
            if (Showing)
            {
                _list?.Rebind();
                SheetPicture();
            }
        }

        private void Go(State s, bool remember)
        {
            if (remember && (s.Screen != _at.Screen || s.Post != _at.Post || s.Key != _at.Key || s.Tab != _at.Tab))
            {
                _back.Add(_at);
                if (_back.Count > 12)
                {
                    _back.RemoveAt(0);
                }
            }
            if (!remember)
            {
                _back.Clear();
            }
            _at = s;
            _top = true;
        }

        private void Open(FlareScreen screen, int tab = 0, int post = -1, string key = null, bool fromRail = false)
        {
            var s = new State { Screen = screen, Tab = tab, Post = post, Key = key };
            if (fromRail)
            {
                _back.Clear();
                _at = s;
                _top = true;
            }
            else
            {
                Go(s, true);
            }
            Refresh(true);
        }

        private void Back()
        {
            if (_back.Count == 0)
            {
                Open(FlareScreen.Home, 0, -1, null, true);
                return;
            }
            _at = _back[_back.Count - 1];
            _back.RemoveAt(_back.Count - 1);
            _top = true;
            Refresh(true);
        }

        private void Tab(int tab)
        {
            if (tab != _at.Tab)
            {
                _at.Tab = tab;
                _top = true;
                Refresh(true);
            }
        }

        private void Refresh(bool all)
        {
            if (!_built || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                float started = Time.realtimeSinceStartup;
                FlareFile file = _director.File;
                int hour = SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 12;
                FlareLook look = FlareLook.Now(hour);
                if (look != _look || all)
                {
                    _look = look;
                    Paint();
                }
                Lay();
                _avatars.Refresh(all);
                WriteRail(file);
                WriteSide(file);
                WriteHeader(file);
                RecycleRows();
                if (file != null)
                {
                    WriteScreen(file);
                }
                else
                {
                    Note(FlareText.T("reallife_flare_off_body"));
                }
                _list.Set(_rows, _top);
                _top = false;
                if (_composeWanted != -1 && file != null)
                {
                    int draft = _composeWanted;
                    _composeWanted = -1;
                    OpenCompose(draft >= 0 ? draft : -1, -1);
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Flare: " + _at.Screen + " written, " + _rows.Count + " row(s); "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        private void RecycleRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Reset();
                _rowPool.Add(_rows[i]);
            }
            _rows.Clear();
        }

        private FlareRow Row(int kind)
        {
            FlareRow row;
            if (_rowPool.Count > 0)
            {
                row = _rowPool[_rowPool.Count - 1];
                _rowPool.RemoveAt(_rowPool.Count - 1);
            }
            else
            {
                row = new FlareRow();
            }
            row.Kind = kind;
            _rows.Add(row);
            return row;
        }

        private void Note(string text)
        {
            Row(CellNote).Text = text;
        }

        private void Section(string text)
        {
            FlareRow r = Row(CellNote);
            r.Text = text;
            r.Number = 1;   // a section's title
        }
    }
}
